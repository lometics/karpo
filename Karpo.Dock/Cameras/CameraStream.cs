// camera: one camera line's stream, read while the line shows (docs\widgets.md §331).
//
// It runs only between Start and Stop, which the line calls as its folder shows it and stops showing it:
// nothing is asked of a camera, decoded or held open while nobody looks. Started, it reads its camera
// the way its protocol says — chosen, guessed from the address, else from the camera's answer:
//
//   mjpeg      one request whose answer never ends; its parts read as they come (MjpegReader) and the
//              newest decoded, at most one a FrameMs, while older ones still waiting are dropped
//   snapshot   one request a pace, asked with the last answer's ETag and Last-Modified so a picture that
//              did not change is not sent again (a 304)
//   hls, dash, video, rtsp   Windows plays them (CameraPlayer)
//   a page     its own stream where it names one (og:video, an .m3u8 in it), read the same way; else why not
//
// Whatever ends it — the camera gone, an error, the stream ended — is said (Shot.Problem) over the last
// picture, which stays, and it tries again after 5, 10, 30 and then every 60 seconds; a picture resets
// that. A user name and a password in the address are the camera's: sent to it (Basic or Digest, as it
// asks) and to nobody else.
//
// Its one callback (told) is called from its own threads when what the line shows changed: how it is,
// a snapshot's picture, a moving stream's first picture. A moving stream's later pictures are only
// taken (Shot): the open folder draws them on its quick tick (FolderFlyout.ArmQuick), not through the dock.

using System.Net;
using System.Net.Http.Headers;
using Karpo.Core;

namespace Karpo.Dock;

/// <summary>How a camera's stream is now.</summary>
internal enum CameraState
{
    /// <summary>Not started: nobody looks.</summary>
    Idle,

    /// <summary>Asking the camera, or waiting for its first picture.</summary>
    Connecting,

    /// <summary>Its pictures are coming.</summary>
    Live,

    /// <summary>It could not be read (<see cref="CameraShot.Problem"/>); it tries again after a while.</summary>
    Failed,
}

/// <summary>What a camera's stream holds now: its newest picture (and its number, which counts up), how it is and why not.</summary>
internal sealed record CameraShot(Pixels? Frame, long Number, CameraState State, string? Problem, CameraProtocol? Protocol, DateTime? FrameUtc)
{
    public static CameraShot None { get; } = new(null, 0, CameraState.Idle, null, null, null);

    /// <summary>Whether its pictures move — a stream, not a snapshot asked for again — and come now: the open folder draws them as they do.</summary>
    public bool Moving => State == CameraState.Live && Protocol is { } protocol && Cameras.Moves(protocol);
}

/// <summary>One camera line's stream (see the top of the file).</summary>
internal sealed class CameraStream(Action told) : IDisposable
{
    /// <summary>The least time between two MJPEG pictures decoded, in ms: about thirty a second.</summary>
    public const int FrameMs = CameraPlayer.FrameMs;

    /// <summary>How long a camera may say nothing before it is taken as gone, in s.</summary>
    public const int QuietSeconds = 20;

    /// <summary>The waits before trying again, in s; the last for every try after.</summary>
    public static readonly int[] Retries = [5, 10, 30, 60];

    /// <summary>The most a snapshot or a page is read, in bytes.</summary>
    private const int MostPicture = 16 << 20;

    private const int MostPage = 4 << 20;

    private readonly object gate = new();
    private CameraShot shot = CameraShot.None;
    private CancellationTokenSource? running;
    private (string Url, CameraProtocol? Protocol, int Seconds, bool Mute)? reading;

    /// <summary>What it holds now.</summary>
    public CameraShot Shot => Volatile.Read(ref shot);

    /// <summary>The longer side the line draws pictures at, in px: what they are decoded at, at most.</summary>
    public int Most { get; set; } = 640;

    /// <summary>tests: the waits before trying again, in ms; the real ones where null.</summary>
    internal Func<int, int>? Wait { get; set; }

    /// <summary>tests: a snapshot's pace in ms; the line's where null.</summary>
    internal int? PaceMs { get; set; }

    /// <summary>tests: whether a run is on its way.</summary>
    internal bool Running => running is not null;

    /// <summary>
    /// Reads <paramref name="options"/>' camera from now, unless it reads that already the same way; a
    /// camera of another address, protocol, pace or sound starts again, keeping the last picture only
    /// where the address is the same.
    /// </summary>
    public void Start(CameraOptions options)
    {
        if (Cameras.Address(options.Url) is not { } url)
        {
            Stop();
            return;
        }
        var wanted = (url, options.Protocol, Cameras.PaceOf(options), options.Mute != false);
        lock (gate)
        {
            if (running is not null && reading == wanted)
            {
                return;
            }
        }
        var same = reading?.Url == url;
        Stop();
        lock (gate)
        {
            reading = wanted;
            Set(shot => (same ? shot : CameraShot.None) with { State = CameraState.Connecting, Problem = null });
            var cancel = new CancellationTokenSource();
            running = cancel;
            _ = Task.Run(() => Troubles.Guard("camera: a stream", () => Run(wanted, cancel.Token)));
        }
    }

    /// <summary>Stops reading: the request closed, the player let go. The last picture stays, to show at once next time.</summary>
    public void Stop()
    {
        CancellationTokenSource? cancel;
        lock (gate)
        {
            cancel = running;
            running = null;
        }
        if (cancel is null)
        {
            return;
        }
        cancel.Cancel();
        cancel.Dispose();
        Set(shot => shot with { State = CameraState.Idle });
    }

    /// <summary>"Refresh": read again from the start, now.</summary>
    public void Restart(CameraOptions options)
    {
        Stop();
        reading = null;
        Start(options);
    }

    public void Dispose() => Stop();

    private void Set(Func<CameraShot, CameraShot> change)
    {
        lock (gate)
        {
            Volatile.Write(ref shot, change(shot));
        }
    }

    /// <summary>How it is now, said to the line where it changed.</summary>
    private void Say(CameraState state, string? problem, CancellationToken cancel)
    {
        if (cancel.IsCancellationRequested)
        {
            return;
        }
        var was = Shot;
        Set(shot => shot with { State = state, Problem = problem });
        if (was.State != state || was.Problem != problem)
        {
            told();
        }
    }

    /// <summary>A picture: kept as the newest, said to the line where it is a snapshot's or the first of a moving stream.</summary>
    private void Took(Pixels picture, CameraProtocol protocol, CancellationToken cancel)
    {
        if (cancel.IsCancellationRequested)
        {
            return;
        }
        var was = Shot;
        Set(shot => shot with { Frame = picture, Number = shot.Number + 1, State = CameraState.Live, Problem = null, Protocol = protocol, FrameUtc = Clock.RealUtc() });
        if (!Cameras.Moves(protocol) || !was.Moving)
        {
            told();
        }
    }

    private async Task Run((string Url, CameraProtocol? Protocol, int Seconds, bool Mute) wanted, CancellationToken cancel)
    {
        var tries = 0;
        while (!cancel.IsCancellationRequested)
        {
            var before = Shot.Number;
            try
            {
                Say(CameraState.Connecting, Shot.Problem, cancel);
                await Read(wanted.Url, wanted.Protocol, wanted.Seconds, wanted.Mute, page: true, cancel).ConfigureAwait(false);
                Say(CameraState.Failed, S.DockCameraEnded, cancel);
            }
            catch (OperationCanceledException) when (cancel.IsCancellationRequested)
            {
                return;
            }
            catch (CameraProblem problem)
            {
                Say(CameraState.Failed, problem.Message, cancel);
            }
            catch (Exception error) when (error is HttpRequestException or IOException or OperationCanceledException or TimeoutException)
            {
                Say(CameraState.Failed, S.DockCameraUnreachable, cancel);
            }
            catch (Exception error)
            {
                Troubles.Report("camera: reading a stream", error);
                Say(CameraState.Failed, S.DockCameraUnreadable, cancel);
            }
            tries = Shot.Number > before ? 0 : tries + 1;
            var wait = Wait?.Invoke(tries) ?? Retries[Math.Clamp(tries, 1, Retries.Length) - 1] * 1000;
            try
            {
                await Task.Delay(wait, cancel).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>
    /// <paramref name="url"/> read as <paramref name="protocol"/>, or as its answer says where none is chosen
    /// or guessed; a page's own stream where <paramref name="page"/> lets it be looked for (once).
    /// </summary>
    private async Task Read(string url, CameraProtocol? protocol, int seconds, bool mute, bool page, CancellationToken cancel)
    {
        var known = protocol ?? Cameras.Guess(url);
        if (known is { } played && Cameras.Played(played))
        {
            await Play(url, played, mute, cancel).ConfigureAwait(false);
            return;
        }
        using var client = Client(url);
        using var answer = await Ask(client, url, null, cancel).ConfigureAwait(false);
        await using var body = await answer.Content.ReadAsStreamAsync(cancel).ConfigureAwait(false);
        var type = answer.Content.Headers.ContentType?.ToString();
        var head = await Head(body, cancel).ConfigureAwait(false);
        if (known is null)
        {
            var said = Cameras.FromAnswer(type, head);
            if (said.Page)
            {
                var html = System.Text.Encoding.UTF8.GetString([.. head, .. await Rest(body, MostPage - head.Length, cancel, cut: true).ConfigureAwait(false)]);
                if (page && Cameras.StreamIn(html, answer.RequestMessage?.RequestUri?.AbsoluteUri ?? url) is { } stream)
                {
                    await Read(stream, null, seconds, mute, page: false, cancel).ConfigureAwait(false);
                    return;
                }
                throw new CameraProblem(S.DockCameraPage);
            }
            known = said.Protocol ?? throw new CameraProblem(S.DockCameraUnreadable);
            if (Cameras.Played(known.Value))
            {
                await Play(url, known.Value, mute, cancel).ConfigureAwait(false);
                return;
            }
        }
        if (known == CameraProtocol.Mjpeg)
        {
            await Mjpeg(body, Core.Cameras.Boundary(type), head, cancel).ConfigureAwait(false);
            return;
        }
        // A snapshot: this one, then one a pace, each only where it changed.
        var picture = (byte[])[.. head, .. await Rest(body, MostPicture - head.Length, cancel).ConfigureAwait(false)];
        var tags = Tags(answer);
        Shown(picture, cancel);
        while (true)
        {
            await Task.Delay(PaceMs ?? seconds * 1000, cancel).ConfigureAwait(false);
            using var again = await Ask(client, url, tags, cancel).ConfigureAwait(false);
            if (again.StatusCode == HttpStatusCode.NotModified)
            {
                Set(shot => shot with { FrameUtc = Clock.RealUtc() });
                continue;
            }
            await using var next = await again.Content.ReadAsStreamAsync(cancel).ConfigureAwait(false);
            tags = Tags(again);
            Shown(await Rest(next, MostPicture, cancel).ConfigureAwait(false), cancel);
        }
    }

    /// <summary>A snapshot decoded and kept; one Windows cannot read says so.</summary>
    private void Shown(byte[] picture, CancellationToken cancel) =>
        Took(CameraPictures.Decode(picture, Most) ?? throw new CameraProblem(S.DockCameraUnreadable), CameraProtocol.Snapshot, cancel);

    private Task Play(string url, CameraProtocol protocol, bool mute, CancellationToken cancel) =>
        CameraPlayer.Play(url, protocol, mute, () => Most, picture => Took(picture, protocol, cancel), cancel);

    /// <summary>An MJPEG answer's parts as they come, the newest decoded at most one a <see cref="FrameMs"/>; a camera quiet for <see cref="QuietSeconds"/> is gone.</summary>
    private async Task Mjpeg(Stream body, string? boundary, byte[] head, CancellationToken cancel)
    {
        var reader = new MjpegReader(boundary);
        var ring = new PixelRing();
        var chunk = new byte[64 << 10];
        long last = 0;
        byte[]? waiting = null;
        void Decode(List<byte[]> frames)
        {
            waiting = frames.Count > 0 ? frames[^1] : waiting;
            var now = Environment.TickCount64;
            if (waiting is null || now - last < FrameMs)
            {
                return;
            }
            last = now;
            // Into one of three arrays taken in turn rather than a new one a picture (PixelRing).
            var picture = CameraPictures.Decode(waiting, Most, ring);
            waiting = null;
            if (picture is not null)
            {
                Took(picture, CameraProtocol.Mjpeg, cancel);
            }
        }
        Decode(reader.Feed(head));
        while (true)
        {
            using var quiet = CancellationTokenSource.CreateLinkedTokenSource(cancel);
            quiet.CancelAfter(TimeSpan.FromSeconds(QuietSeconds));
            var read = await body.ReadAsync(chunk, quiet.Token).ConfigureAwait(false);
            if (read <= 0)
            {
                return;
            }
            Decode(reader.Feed(chunk.AsSpan(0, read)));
        }
    }

    /// <summary>A client for <paramref name="url"/>: with its user name and password for the camera where it carries them, else the islands' own.</summary>
    private static HttpClient Client(string url)
    {
        var address = new Uri(url);
        var handler = new SocketsHttpHandler
        {
            UseCookies = false,
            AutomaticDecompression = DecompressionMethods.All,
            ConnectTimeout = TimeSpan.FromSeconds(QuietSeconds),
        };
        if (address.UserInfo.Length > 0)
        {
            var parts = address.UserInfo.Split(':', 2);
            handler.Credentials = new NetworkCredential(Uri.UnescapeDataString(parts[0]), parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : "");
            handler.PreAuthenticate = true;
        }
        var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Karpo/1.0");
        return client;
    }

    /// <summary>The camera asked, its headers awaited for <see cref="QuietSeconds"/> at most; an answer that is no success says which.</summary>
    private static async Task<HttpResponseMessage> Ask(HttpClient client, string url, (EntityTagHeaderValue? Tag, DateTimeOffset? Modified)? tags, CancellationToken cancel)
    {
        var address = new Uri(url);
        var request = new HttpRequestMessage(HttpMethod.Get, address.UserInfo.Length == 0 ? address : new UriBuilder(address) { UserName = "", Password = "" }.Uri);
        if (tags is { } known)
        {
            if (known.Tag is { } tag)
            {
                request.Headers.IfNoneMatch.Add(tag);
            }
            request.Headers.IfModifiedSince = known.Modified;
        }
        using var headers = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        headers.CancelAfter(TimeSpan.FromSeconds(QuietSeconds));
        var answer = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, headers.Token).ConfigureAwait(false);
        if (answer.IsSuccessStatusCode || answer.StatusCode == HttpStatusCode.NotModified)
        {
            return answer;
        }
        var status = (int)answer.StatusCode;
        answer.Dispose();
        throw new CameraProblem(S.DockCameraRefused(status));
    }

    private static (EntityTagHeaderValue? Tag, DateTimeOffset? Modified) Tags(HttpResponseMessage answer) =>
        (answer.Headers.ETag, answer.Content.Headers.LastModified);

    /// <summary>The answer's first bytes, up to 4 KB: what tells what it is.</summary>
    private static async Task<byte[]> Head(Stream body, CancellationToken cancel)
    {
        var head = new byte[4096];
        var filled = 0;
        using var quiet = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        quiet.CancelAfter(TimeSpan.FromSeconds(QuietSeconds));
        while (filled < head.Length)
        {
            var read = await body.ReadAsync(head.AsMemory(filled), quiet.Token).ConfigureAwait(false);
            if (read <= 0)
            {
                break;
            }
            filled += read;
            // A multipart answer's first part needs no more to tell what it is; a picture's start neither.
            if (filled >= 512)
            {
                break;
            }
        }
        return head[..filled];
    }

    /// <summary>
    /// The rest of a picture or a page, <paramref name="most"/> bytes at most: a longer picture is none this
    /// line shows; a longer page is read as far as that (<paramref name="cut"/>), where its stream is named if anywhere.
    /// </summary>
    private static async Task<byte[]> Rest(Stream body, int most, CancellationToken cancel, bool cut = false)
    {
        using var kept = new MemoryStream();
        var chunk = new byte[64 << 10];
        using var quiet = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        quiet.CancelAfter(TimeSpan.FromSeconds(QuietSeconds * 3));
        int read;
        while ((read = await body.ReadAsync(chunk, quiet.Token).ConfigureAwait(false)) > 0)
        {
            if (kept.Length + read > most)
            {
                if (!cut)
                {
                    throw new CameraProblem(S.DockCameraUnreadable);
                }
                kept.Write(chunk, 0, (int)(most - kept.Length));
                break;
            }
            kept.Write(chunk, 0, read);
        }
        return kept.ToArray();
    }
}
