// camera: what Windows' media stack plays — HLS, MPEG-DASH, a video over HTTP and RTSP — as pictures a
// camera line draws (docs\widgets.md §331).
//
// Windows.Media.Playback's MediaPlayer plays them all: an .m3u8 or .mpd through AdaptiveMediaSource, the
// rest by their address (Media Foundation's http and rtsp scheme handlers, its MP4, MPEG-TS, MKV and WebM
// readers; H.264, HEVC where its extension is installed, and whatever else Windows decodes). Its frame
// server hands each picture over instead of a window: VideoFrameAvailable says one is there, and
// CopyFrameToVideoSurface draws it, scaled by the GPU, into a texture of this class's own D3D11 device, no
// larger than the line shows it. The texture is copied to a staging one and read back into pixels the
// line draws as it draws a snapshot — one way for every camera to reach the line. At most one picture a
// FrameMs is read back, whatever the stream's own rate.
//
// RTSP is Media Foundation's own RTSP source, which carries ASF and MP3 alone: an IP camera's H.264 over
// RTSP opens and then fails to decode (0xC00D003A, seen against VLC's RTSP server on Windows 11 26300), and
// the line says to use the camera's MJPEG or snapshot address instead. A manifest Windows' adaptive reader
// turns down (DASH-IF's "dash-if-simple" profile) is handed to Media Foundation's own reader as a last try.
//
// Its sound is the stream's, muted unless the line says otherwise. A video file plays round and round; a
// live stream that ends, or fails, ends this with why (CameraProblem), and the stream tries again.

using Karpo.Core;
using Karpo.Dock.Native;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using Windows.Graphics.DirectX.Direct3D11;
using WinMediaSource = Windows.Media.Core.MediaSource;
using Windows.Media.Playback;
using Windows.Media.Streaming.Adaptive;
using static TerraFX.Interop.DirectX.DirectX;
using static TerraFX.Interop.Windows.Windows;
using IInspectable = TerraFX.Interop.WinRT.IInspectable;
using WinRTInterop = TerraFX.Interop.WinRT.WinRT;

namespace Karpo.Dock;

/// <summary>A stream Windows plays, as pictures (see the top of the file).</summary>
internal sealed class CameraPlayer : IDisposable
{
    /// <summary>The least time between two pictures read back, in ms: about thirty a second.</summary>
    public const int FrameMs = 33;

    private readonly object gate = new();
    private readonly MediaPlayer player = new();
    private readonly Func<int> most;
    private readonly Action<Pixels> framed;
    private readonly PixelRing ring = new();
    private readonly TaskCompletionSource ended = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private unsafe ID3D11Device* device;
    private unsafe ID3D11DeviceContext* context;
    private unsafe ID3D11Texture2D* target;
    private unsafe ID3D11Texture2D* staging;
    private IDirect3DSurface? surface;
    private int width;
    private int height;
    private long last;
    private bool gone;
    private CameraProtocol protocol;

    private CameraPlayer(Func<int> most, Action<Pixels> framed)
    {
        this.most = most;
        this.framed = framed;
    }

    /// <summary>
    /// Plays <paramref name="address"/> as <paramref name="protocol"/> until <paramref name="cancel"/> or until
    /// it ends or fails, which throws why (<see cref="CameraProblem"/>); each picture to <paramref name="framed"/>,
    /// on Windows' thread, no larger than <paramref name="most"/> says.
    /// </summary>
    public static async Task Play(string address, CameraProtocol protocol, bool mute, Func<int> most, Action<Pixels> framed, CancellationToken cancel)
    {
        using var playing = new CameraPlayer(most, framed);
        await playing.Open(address, protocol, mute, cancel).ConfigureAwait(false);
        await playing.ended.Task.WaitAsync(cancel).ConfigureAwait(false);
    }

    private async Task Open(string address, CameraProtocol protocol, bool mute, CancellationToken cancel)
    {
        this.protocol = protocol;
        var uri = new Uri(address);
        WinMediaSource source;
        if (protocol is CameraProtocol.Hls or CameraProtocol.Dash)
        {
            var made = await AdaptiveMediaSource.CreateFromUriAsync(uri).AsTask(cancel).ConfigureAwait(false);
            if (made.Status == AdaptiveMediaSourceCreationStatus.Success)
            {
                source = WinMediaSource.CreateFromAdaptiveMediaSource(made.MediaSource);
            }
            else if (made.Status == AdaptiveMediaSourceCreationStatus.ManifestDownloadFailure)
            {
                throw new CameraProblem(Why(made));
            }
            else
            {
                // A manifest the adaptive reader turns down (a profile it does not take) may still be one
                // Media Foundation's own reader plays; where it is not, the player says so.
                Troubles.Note("camera: Windows' adaptive reader turned a manifest down", new CameraProblem(made.Status.ToString()));
                source = WinMediaSource.CreateFromUri(uri);
            }
        }
        else
        {
            source = WinMediaSource.CreateFromUri(uri);
        }
        player.IsMuted = mute;
        player.IsLoopingEnabled = protocol == CameraProtocol.Video;
        // A camera is live: as near to now as the stream allows, rather than smooth after a pause.
        player.RealTimePlayback = protocol == CameraProtocol.Rtsp;
        player.IsVideoFrameServerEnabled = true;
        player.VideoFrameAvailable += Arrived;
        player.MediaFailed += Failed;
        player.MediaEnded += Ended;
        player.Source = source;
        player.Play();
    }

    /// <summary>Why an adaptive stream's manifest could not be fetched: the server's answer, else that it could not be reached.</summary>
    private static string Why(AdaptiveMediaSourceCreationResult made) =>
        made.HttpResponseMessage is { } answer && (int)answer.StatusCode >= 400 ? S.DockCameraRefused((int)answer.StatusCode) : S.DockCameraUnreachable;

    private void Failed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
    {
        try
        {
            Troubles.Note("camera: Windows could not play a stream", new CameraProblem($"{protocol} {args.Error} 0x{args.ExtendedErrorCode?.HResult:X8}: {args.ErrorMessage}"));
            ended.TrySetException(new CameraProblem(args.Error switch
            {
                MediaPlayerError.NetworkError => S.DockCameraUnreachable,
                MediaPlayerError.Aborted => S.DockCameraEnded,
                // Windows' RTSP source carries ASF and MP3 alone: an IP camera's H.264 over RTSP is not
                // played. Most such cameras serve MJPEG or a snapshot over HTTP too, which the line reads.
                _ when protocol == CameraProtocol.Rtsp => S.DockCameraRtsp,
                _ => S.DockCameraUnplayable,
            }));
        }
        catch (Exception error)
        {
            ended.TrySetException(error);
        }
    }

    private void Ended(MediaPlayer sender, object args)
    {
        try
        {
            ended.TrySetException(new CameraProblem(S.DockCameraEnded));
        }
        catch (Exception error)
        {
            ended.TrySetException(error);
        }
    }

    /// <summary>A picture is there: drawn into the texture, read back and handed on, at most one a <see cref="FrameMs"/>.</summary>
    private void Arrived(MediaPlayer sender, object args)
    {
        try
        {
            Pixels? picture = null;
            lock (gate)
            {
                var now = Environment.TickCount64;
                if (gone || now - last < FrameMs)
                {
                    return;
                }
                last = now;
                var session = player.PlaybackSession;
                var (wide, high) = CameraPictures.Fitted((int)session.NaturalVideoWidth, (int)session.NaturalVideoHeight, most());
                if (session.NaturalVideoWidth == 0 || session.NaturalVideoHeight == 0 || !Ready(wide, high))
                {
                    return;
                }
                player.CopyFrameToVideoSurface(surface);
                picture = Read();
            }
            if (picture is not null)
            {
                framed(picture);
            }
        }
        catch (Exception error)
        {
            Troubles.Report("camera: a stream's picture", error);
        }
    }

    /// <summary>The device and the two textures, <paramref name="wide"/> × <paramref name="high"/>; made again where the size changed.</summary>
    private unsafe bool Ready(int wide, int high)
    {
        if (device == null)
        {
            ID3D11Device* made = null;
            ID3D11DeviceContext* immediate = null;
            const uint flags = (uint)(D3D11_CREATE_DEVICE_FLAG.D3D11_CREATE_DEVICE_BGRA_SUPPORT | D3D11_CREATE_DEVICE_FLAG.D3D11_CREATE_DEVICE_VIDEO_SUPPORT);
            if (D3D11CreateDevice(null, D3D_DRIVER_TYPE.D3D_DRIVER_TYPE_HARDWARE, HMODULE.NULL, flags, null, 0, D3D11.D3D11_SDK_VERSION, &made, null, &immediate).FAILED
                && D3D11CreateDevice(null, D3D_DRIVER_TYPE.D3D_DRIVER_TYPE_WARP, HMODULE.NULL, (uint)D3D11_CREATE_DEVICE_FLAG.D3D11_CREATE_DEVICE_BGRA_SUPPORT, null, 0, D3D11.D3D11_SDK_VERSION, &made, null, &immediate).FAILED)
            {
                return false;
            }
            device = made;
            context = immediate;
            // Media Foundation may reach the device from its own threads while it copies a picture.
            ID3D11Multithread* guarded = null;
            if (device->QueryInterface(__uuidof<ID3D11Multithread>(), (void**)&guarded).SUCCEEDED)
            {
                _ = guarded->SetMultithreadProtected(BOOL.TRUE);
                guarded->Release();
            }
        }
        if (target != null && wide == width && high == height)
        {
            return true;
        }
        Textures();
        var description = new D3D11_TEXTURE2D_DESC
        {
            Width = (uint)wide,
            Height = (uint)high,
            MipLevels = 1,
            ArraySize = 1,
            Format = DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM,
            SampleDesc = new DXGI_SAMPLE_DESC(1, 0),
            Usage = D3D11_USAGE.D3D11_USAGE_DEFAULT,
            BindFlags = (uint)(D3D11_BIND_FLAG.D3D11_BIND_RENDER_TARGET | D3D11_BIND_FLAG.D3D11_BIND_SHADER_RESOURCE),
        };
        ID3D11Texture2D* drawn = null;
        if (device->CreateTexture2D(&description, null, &drawn).FAILED)
        {
            return false;
        }
        target = drawn;
        description.Usage = D3D11_USAGE.D3D11_USAGE_STAGING;
        description.BindFlags = 0;
        description.CPUAccessFlags = (uint)D3D11_CPU_ACCESS_FLAG.D3D11_CPU_ACCESS_READ;
        ID3D11Texture2D* read = null;
        if (device->CreateTexture2D(&description, null, &read).FAILED)
        {
            Textures();
            return false;
        }
        staging = read;
        IDXGISurface* dxgi = null;
        IInspectable* inspectable = null;
        try
        {
            if (target->QueryInterface(__uuidof<IDXGISurface>(), (void**)&dxgi).FAILED
                || WinRTInterop.CreateDirect3D11SurfaceFromDXGISurface(dxgi, &inspectable).FAILED)
            {
                Textures();
                return false;
            }
            surface = ComUtil.Adopt<IDirect3DSurface>(inspectable);
        }
        finally
        {
            ComUtil.Release(ref dxgi);
        }
        (width, height) = (wide, high);
        return true;
    }

    /// <summary>The picture drawn into the texture, read back as premultiplied pixels (a video's are opaque).</summary>
    private unsafe Pixels? Read()
    {
        context->CopyResource((ID3D11Resource*)staging, (ID3D11Resource*)target);
        D3D11_MAPPED_SUBRESOURCE mapped;
        if (context->Map((ID3D11Resource*)staging, 0, D3D11_MAP.D3D11_MAP_READ, 0, &mapped).FAILED)
        {
            return null;
        }
        try
        {
            var picture = ring.Next(width, height);
            var row = width * 4;
            fixed (byte* into = picture.Data)
            {
                for (var y = 0; y < height; y++)
                {
                    var from = (byte*)mapped.pData + ((long)y * mapped.RowPitch);
                    var to = into + ((long)y * row);
                    Buffer.MemoryCopy(from, to, row, row);
                    for (var x = 3; x < row; x += 4)
                    {
                        to[x] = 0xFF;
                    }
                }
            }
            return picture;
        }
        finally
        {
            context->Unmap((ID3D11Resource*)staging, 0);
        }
    }

    /// <summary>The textures and the surface over them gone, for another size or for good.</summary>
    private unsafe void Textures()
    {
        surface?.Dispose();
        surface = null;
        ComUtil.Release(ref staging);
        ComUtil.Release(ref target);
        (width, height) = (0, 0);
    }

    public unsafe void Dispose()
    {
        player.VideoFrameAvailable -= Arrived;
        player.MediaFailed -= Failed;
        player.MediaEnded -= Ended;
        try
        {
            player.Pause();
            player.Source = null;
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Troubles.Note("camera: a stream let go", error);
        }
        player.Dispose();
        lock (gate)
        {
            gone = true;
            Textures();
            ComUtil.Release(ref context);
            ComUtil.Release(ref device);
        }
    }
}

/// <summary>Why a camera shows no picture, in the person's words; the stream tries again after it.</summary>
internal sealed class CameraProblem(string message) : Exception(message);

/// <summary>
/// Three pictures' pixels taken in turn, so a stream does not make a large array for every picture: the one
/// the line draws, the one before it (which a paint may still be reading for a moment) and the one filled now.
/// </summary>
internal sealed class PixelRing
{
    private readonly Pixels?[] pixels = new Pixels?[3];
    private int at;

    /// <summary>The next of the three, <paramref name="width"/> × <paramref name="height"/>: its array kept where it is that size.</summary>
    public Pixels Next(int width, int height)
    {
        at = (at + 1) % pixels.Length;
        if (pixels[at] is { } kept && kept.Width == width && kept.Height == height)
        {
            return kept;
        }
        return pixels[at] = new Pixels { Data = new byte[width * height * 4], Width = width, Height = height };
    }
}
