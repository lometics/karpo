// The default output's volume and mute for the volume slider on the bar, through
// Core Audio: read once when the island appears, then only when Windows says it
// changed (IAudioEndpointVolume's callback for the level and the mute, the device
// enumerator's for a new default output). Nothing polls, and everything is let go
// when the island goes.
//
// The callbacks come on the system's threads and only say "changed"; the island
// posts to the dock and reads again on the dock's thread. The sound's and the
// microphone's toggles (MuteSwitch.cs) follow their endpoint's mute the same way.

using Karpo.Dock.Native;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock.QuickSettings.Services;

internal sealed unsafe class VolumeWatch : IDisposable
{
    /// <summary>
    /// Tests only, on the thread that makes the island: a stand-in for the PC's output. With one
    /// set the watch never opens Core Audio and never moves the volume anybody is listening to,
    /// while everything above it — the slider, a folder's card, a drag — runs as it does for real.
    /// </summary>
    [ThreadStatic]
    internal static SampleVolume? Stand;

    private readonly SampleVolume? stand = Stand;
    private readonly Changes changes;
    private readonly EDataFlow flow;
    private IMMDeviceEnumerator* devices;
    private IAudioEndpointVolume* volume;
    private QuickSink* deviceSink;
    private QuickSink* volumeSink;
    private string? attached;
    private bool disposed;

    /// <param name="changed">Called from any thread when the level, the mute or the output changed.</param>
    /// <param name="flow">The default output's (render), or the default microphone's (capture): the microphone's toggle.</param>
    public VolumeWatch(Action changed, EDataFlow flow = EDataFlow.eRender)
    {
        this.flow = flow;
        changes = new Changes(_ => changed());
        if (stand is not null)
        {
            _ = Read();
            return;
        }
        var classId = CLSID.CLSID_MMDeviceEnumerator;
        IMMDeviceEnumerator* made = null;
        if (CoCreateInstance(&classId, null, (uint)CLSCTX_ALL, __uuidof<IMMDeviceEnumerator>(), (void**)&made).FAILED || made == null)
        {
            return;
        }
        devices = made;
        deviceSink = QuickSink.Create(QuickSink.DeviceTable, *(Guid*)__uuidof<IMMNotificationClient>(), changes, SoundNews.Devices);
        _ = devices->RegisterEndpointNotificationCallback((IMMNotificationClient*)deviceSink);
        Attach();
        Read();
    }

    /// <summary>Whether there is an output device at all.</summary>
    public bool Present { get; private set; }

    /// <summary>The level of the default output, 0 to 100.</summary>
    public int Level { get; private set; }

    public bool Muted { get; private set; }

    /// <summary>Takes what changed and reads the level again; true when anything the bar shows moved.</summary>
    public bool Read()
    {
        if (disposed)
        {
            return false;
        }
        if (stand is not null)
        {
            var had = (Present, Level, Muted);
            (Present, Level, Muted) = (stand.Present, stand.Level, stand.Muted);
            return had != (Present, Level, Muted);
        }
        // A new default output: follow that one from now on.
        if (changes.Take() != 0 || volume == null)
        {
            Attach();
        }
        var (present, level, muted) = (Present, Level, Muted);
        if (volume == null)
        {
            (Present, Level, Muted) = (false, 0, false);
            return present != Present || level != Level || muted != Muted;
        }
        float scalar = 0;
        BOOL mute = BOOL.FALSE;
        _ = volume->GetMasterVolumeLevelScalar(&scalar);
        _ = volume->GetMute(&mute);
        Present = true;
        Level = (int)MathF.Round(Math.Clamp(scalar, 0, 1) * 100, MidpointRounding.AwayFromZero);
        Muted = mute;
        return present != Present || level != Level || muted != Muted;
    }

    /// <summary>Sets the default output's level, 0 to 100.</summary>
    public void SetLevel(int percent)
    {
        if (stand is not null)
        {
            Level = stand.Level = stand.Present ? Math.Clamp(percent, 0, 100) : stand.Level;
            return;
        }
        if (volume == null)
        {
            return;
        }
        var level = Math.Clamp(percent, 0, 100);
        if (volume->SetMasterVolumeLevelScalar(level / 100f, null).SUCCEEDED)
        {
            Level = level;
        }
    }

    public void SetMuted(bool muted)
    {
        if (stand is not null)
        {
            if (stand.Present)
            {
                Muted = stand.Muted = muted;
            }
            return;
        }
        if (volume != null && volume->SetMute(muted ? BOOL.TRUE : BOOL.FALSE, null).SUCCEEDED)
        {
            Muted = muted;
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }
        disposed = true;
        Detach();
        if (devices != null)
        {
            if (deviceSink != null)
            {
                _ = devices->UnregisterEndpointNotificationCallback((IMMNotificationClient*)deviceSink);
            }
            devices->Release();
            devices = null;
        }
        QuickSink.Drop(ref deviceSink);
        QuickSink.Drop(ref volumeSink);
    }

    private void Attach()
    {
        IMMDevice* device = null;
        if (devices == null || devices->GetDefaultAudioEndpoint(flow, ERole.eMultimedia, &device).FAILED || device == null)
        {
            Detach();
            return;
        }
        try
        {
            char* id = null;
            var endpointId = device->GetId(&id).SUCCEEDED ? ComUtil.FromTaskMem(id) : null;
            if (volume != null && endpointId is not null && endpointId == attached)
            {
                return;
            }
            Detach();
            attached = endpointId;
            IAudioEndpointVolume* endpoint = null;
            if (device->Activate(__uuidof<IAudioEndpointVolume>(), (uint)CLSCTX_ALL, null, (void**)&endpoint).FAILED || endpoint == null)
            {
                return;
            }
            volume = endpoint;
            if (volumeSink == null)
            {
                volumeSink = QuickSink.Create(QuickSink.VolumeTable, *(Guid*)__uuidof<IAudioEndpointVolumeCallback>(), changes, SoundNews.Volume);
            }
            _ = volume->RegisterControlChangeNotify((IAudioEndpointVolumeCallback*)volumeSink);
        }
        finally
        {
            device->Release();
        }
    }

    private void Detach()
    {
        attached = null;
        if (volume == null)
        {
            return;
        }
        if (volumeSink != null)
        {
            _ = volume->UnregisterControlChangeNotify((IAudioEndpointVolumeCallback*)volumeSink);
        }
        volume->Release();
        volume = null;
    }
}

/// <summary>
/// A make-believe output for the tests (<see cref="VolumeWatch.Stand"/>): the level and the mute
/// the watch reads and writes, kept in memory. Nothing here reaches the PC's own audio.
/// </summary>
internal sealed class SampleVolume
{
    public bool Present { get; set; } = true;

    public int Level { get; set; } = 50;

    public bool Muted { get; set; }
}
