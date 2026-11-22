// Windows' audio for the sound flyout, through Core Audio: the outputs to choose
// from (IMMDeviceEnumerator, followed with IMMNotificationClient), the default
// output's volume and mute (IAudioEndpointVolume, followed with its callback),
// switching the default output (the undocumented IPolicyConfig, as Windows' own
// picker and Karpo's settings app did before), and each app's own volume (the
// sessions Windows' volume mixer lists: IAudioSessionManager2 and
// ISimpleAudioVolume, followed with session notifications).
//
// Session notifications are only delivered to a registration made from the
// multithreaded apartment (IAudioSessionManager2::RegisterSessionNotification's
// remarks), so that registration is made, and undone, on a pool thread with
// objects of its own. Everything is let go when the flyout closes.

using System.Runtime.InteropServices;
using Karpo.Core;
using Karpo.Dock.Native;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
using static TerraFX.Interop.Windows.S;

namespace Karpo.Dock.QuickSettings.Services;

/// <summary>An output: its endpoint id, its name, a glyph for its kind, and whether it is the default.</summary>
internal sealed record AudioOutput(string Id, string Name, string Glyph, bool Default);

/// <summary>
/// An app with audio sessions on the default output. <paramref name="Key"/> groups its sessions (the
/// AUMID of a packaged app, else its executable, else its process); <paramref name="System"/> is
/// Windows' "System sounds".
/// </summary>
internal sealed record AudioApp(string Key, string Name, float Volume, bool Muted, bool System, string? Exe, string? Aumid);

/// <summary>
/// What the sound flyout shows. Not <paramref name="Present"/>: no output device at all.
/// <paramref name="Inputs"/> are the microphones Windows has, with the default marked, and
/// <paramref name="InputMuted"/> is that one's mute — a PC with no microphone has an empty list,
/// which the page says in words rather than offering a list with nothing in it.
/// </summary>
internal sealed record SoundState(
    bool Present,
    float Volume,
    bool Muted,
    IReadOnlyList<AudioOutput> Outputs,
    IReadOnlyList<AudioApp> Apps,
    IReadOnlyList<AudioOutput>? Inputs = null,
    bool InputMuted = false)
{
    /// <summary>The microphones, never null.</summary>
    public IReadOnlyList<AudioOutput> Microphones => Inputs ?? [];

    /// <summary>The device in use of a list, when it has one.</summary>
    public static AudioOutput? Chosen(IReadOnlyList<AudioOutput> devices)
    {
        foreach (var device in devices)
        {
            if (device.Default)
            {
                return device;
            }
        }
        return null;
    }
}

internal interface ISoundSource : IDisposable
{
    /// <summary>Starts following the outputs, the volume and the apps; what changes goes to <paramref name="changes"/>.</summary>
    void Start(Changes changes);

    /// <summary>The state now; <paramref name="news"/> is what was heard since the last read (SoundNews), 0 the first time.</summary>
    SoundState Read(int news);

    void SetVolume(float value);

    void SetMuted(bool muted);

    void SetDefault(string id);

    /// <summary>Makes <paramref name="id"/> the default microphone, for the same roles as the output.</summary>
    void SetDefaultInput(string id);

    /// <summary>Mutes or unmutes the default microphone.</summary>
    void SetInputMuted(bool muted);

    void SetAppVolume(string key, float value);

    void SetAppMuted(string key, bool muted);
}

/// <summary>What <see cref="Changes"/> carries for sound.</summary>
internal static class SoundNews
{
    public const int Devices = 1;
    public const int Volume = 2;
    public const int Sessions = 4;
    public const int Looks = 8;

    /// <summary>A session's own change: its volume, its state, its name.</summary>
    public const int SessionChanged = 16;
}

internal sealed unsafe class WindowsSound : ISoundSource
{
    private const uint DeviceStateActive = 1;

    // Segoe Fluent Icons for an output's form factor.
    private static readonly string Speakers = char.ConvertFromUtf32(0xE7F5);
    private static readonly string Headphones = char.ConvertFromUtf32(0xE7F6);
    private static readonly string Headset = char.ConvertFromUtf32(0xE95B);
    private static readonly string Display = char.ConvertFromUtf32(0xE7F4);
    private static readonly string Microphone = char.ConvertFromUtf32(0xE720);

    // PKEY_Device_FriendlyName and PKEY_AudioEndpoint_FormFactor, which TerraFX does not carry.
    private static readonly PROPERTYKEY FriendlyName = new() { fmtid = new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), pid = 14 };
    private static readonly PROPERTYKEY FormFactor = new() { fmtid = new Guid("1DA5D803-D492-4EDD-8C23-E0C0FFEE7F0E"), pid = 0 };

    // IPolicyConfig (Windows 7 and later), the interface Windows' own output picker uses.
    private static readonly Guid PolicyConfigClass = new("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9");
    private static readonly Guid PolicyConfigInterface = new("F8679F50-850A-41CF-9C72-430F290290C8");
    private const int SetDefaultEndpointSlot = 13;

    private IMMDeviceEnumerator* devices;
    private IAudioEndpointVolume* volume;
    private IAudioEndpointVolume* capture;
    private string? listening;
    private IAudioSessionManager2* sessions;
    private string? attached;
    private Changes? changes;
    private QuickSink* deviceSink;
    private QuickSink* volumeSink;
    private QuickSink* eventsSink;
    private readonly List<nint> followed = [];
    private CrossApartment? created;
    private bool disposed;

    public void Start(Changes news)
    {
        changes = news;
        if (!Enumerator())
        {
            return;
        }
        deviceSink = QuickSink.Create(QuickSink.DeviceTable, *(Guid*)__uuidof<IMMNotificationClient>(), news, SoundNews.Devices);
        _ = devices->RegisterEndpointNotificationCallback((IMMNotificationClient*)deviceSink);
        Attach();
    }

    public SoundState Read(int news)
    {
        if (!Enumerator())
        {
            return new SoundState(false, 0, false, [], []);
        }
        var outputs = Outputs(out var defaultId);
        if (defaultId != attached)
        {
            Attach();
        }
        var inputs = Inputs(out var defaultInput);
        if (defaultInput != listening)
        {
            Listen(defaultInput);
        }
        var micMuted = false;
        if (capture != null)
        {
            BOOL mic = BOOL.FALSE;
            _ = capture->GetMute(&mic);
            micMuted = mic;
        }
        if (volume == null)
        {
            return new SoundState(outputs.Count > 0, 0, false, outputs, [], inputs, micMuted);
        }
        float level = 0;
        BOOL muted = BOOL.FALSE;
        _ = volume->GetMasterVolumeLevelScalar(&level);
        _ = volume->GetMute(&muted);
        var apps = Apps();
        // A new session (or a new device) is followed from now on; a volume
        // moving in one that is followed already needs nothing new.
        if (eventsSink == null || (news & (SoundNews.Sessions | SoundNews.Devices)) != 0)
        {
            Follow();
        }
        return new SoundState(true, Math.Clamp(level, 0, 1), muted, outputs, apps, inputs, micMuted);
    }

    public void SetVolume(float value)
    {
        if (volume != null)
        {
            _ = volume->SetMasterVolumeLevelScalar(Math.Clamp(value, 0, 1), null);
        }
    }

    public void SetMuted(bool muted)
    {
        if (volume != null)
        {
            _ = volume->SetMute(muted ? BOOL.TRUE : BOOL.FALSE, null);
        }
    }

    /// <summary>
    /// Makes <paramref name="id"/> the default output for every role, as Windows' picker does, through
    /// IPolicyConfig's SetDefaultEndpoint (slot 13 of its Windows 7 and later layout). A Windows
    /// without that interface refuses the creation, and nothing happens.
    /// <para>
    /// On the pool, in turn, never on the dock's thread: the audio service answers once it has moved
    /// the streams over, which with a Bluetooth or USB device is a good part of a second, and the
    /// flyout (and, with a menu's hook in, the pointer) stood still for it. The page hears the new
    /// default from Windows (DefaultDeviceChanged).
    /// </para>
    /// </summary>
    public void SetDefault(string id)
    {
        var endpoint = EndpointId(id);
        lock (SwitchGate)
        {
            switching = switching.ContinueWith(
                _ => Troubles.Guard("Sound: switching the default device", () => Switch(endpoint)),
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default);
        }
    }

    // The switches asked for, one after another in the order asked.
    private static readonly object SwitchGate = new();
    private static Task switching = Task.CompletedTask;

    /// <summary>The last switch asked for (for a test).</summary>
    internal static Task Switching
    {
        get
        {
            lock (SwitchGate)
            {
                return switching;
            }
        }
    }

    private static void Switch(string endpoint)
    {
        // A pool thread's own apartment, made here where the pool has not made one.
        var initialized = CoInitializeEx(null, (uint)COINIT.COINIT_MULTITHREADED).SUCCEEDED;
        try
        {
            SwitchNow(endpoint);
        }
        finally
        {
            if (initialized)
            {
                CoUninitialize();
            }
        }
    }

    private static void SwitchNow(string endpoint)
    {
        var classId = PolicyConfigClass;
        var interfaceId = PolicyConfigInterface;
        void* policy = null;
        if (CoCreateInstance(&classId, null, (uint)CLSCTX.CLSCTX_INPROC_SERVER, &interfaceId, &policy).FAILED || policy == null)
        {
            return;
        }
        try
        {
            var set = (delegate* unmanaged[MemberFunction]<void*, char*, int, int>)(*(void***)policy)[SetDefaultEndpointSlot];
            fixed (char* text = endpoint)
            {
                // eConsole, eMultimedia, eCommunications.
                for (var role = 0; role < 3; role++)
                {
                    _ = set(policy, text, role);
                }
            }
        }
        finally
        {
            ((IUnknown*)policy)->Release();
        }
    }

    /// <summary>The same call the output picker makes: the endpoint id says which flow it is.</summary>
    public void SetDefaultInput(string id) => SetDefault(id);

    public void SetInputMuted(bool muted)
    {
        if (capture != null)
        {
            _ = capture->SetMute(muted ? BOOL.TRUE : BOOL.FALSE, null);
            changes?.Add(SoundNews.Devices);
        }
    }

    /// <summary>
    /// Follows the default microphone's mute. Its own callback would need a second sink; the
    /// device enumerator already says when a default changes, and the flyout reads on every
    /// change it hears, so the mute shown is never more than one notification old.
    /// </summary>
    private void Listen(string? id)
    {
        if (capture != null)
        {
            capture->Release();
            capture = null;
        }
        listening = id;
        if (devices == null || id is null)
        {
            return;
        }
        IMMDevice* device = null;
        if (devices->GetDefaultAudioEndpoint(EDataFlow.eCapture, ERole.eMultimedia, &device).FAILED || device == null)
        {
            return;
        }
        try
        {
            IAudioEndpointVolume* endpoint = null;
            if (device->Activate(__uuidof<IAudioEndpointVolume>(), (uint)CLSCTX_ALL, null, (void**)&endpoint).SUCCEEDED && endpoint != null)
            {
                capture = endpoint;
            }
        }
        finally
        {
            device->Release();
        }
    }

    public void SetAppVolume(string key, float value) =>
        ForApp(key, simple => simple->SetMasterVolume(Math.Clamp(value, 0, 1), null));

    public void SetAppMuted(string key, bool muted) =>
        ForApp(key, simple => simple->SetMute(muted ? BOOL.TRUE : BOOL.FALSE, null));

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }
        disposed = true;
        Unfollow();
        Detach();
        if (capture != null)
        {
            capture->Release();
            capture = null;
        }
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
        QuickSink.Drop(ref eventsSink);
    }

    /// <summary>A device path handed out by WinRT carries the endpoint id inside; IPolicyConfig wants the id alone.</summary>
    internal static string EndpointId(string id)
    {
        const string marker = "#MMDEVAPI#";
        var start = id.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return id;
        }
        var endpoint = id[(start + marker.Length)..];
        var end = endpoint.IndexOf('#');
        return end >= 0 ? endpoint[..end] : endpoint;
    }

    // --- Devices ---------------------------------------------------------------

    private bool Enumerator()
    {
        if (devices != null)
        {
            return true;
        }
        if (disposed)
        {
            return false;
        }
        var classId = CLSID.CLSID_MMDeviceEnumerator;
        IMMDeviceEnumerator* made = null;
        if (CoCreateInstance(&classId, null, (uint)CLSCTX_ALL, __uuidof<IMMDeviceEnumerator>(), (void**)&made).FAILED || made == null)
        {
            return false;
        }
        devices = made;
        return true;
    }

    private List<AudioOutput> Outputs(out string? defaultId) => Endpoints(EDataFlow.eRender, out defaultId);

    /// <summary>The microphones Windows has, the one in use marked.</summary>
    private List<AudioOutput> Inputs(out string? defaultId) => Endpoints(EDataFlow.eCapture, out defaultId);

    /// <summary>
    /// The active endpoints of one flow, named as Windows names them (the friendly name, which
    /// carries the endpoint: "Speakers (Realtek(R) Audio)"), with the default marked.
    /// </summary>
    private List<AudioOutput> Endpoints(EDataFlow flow, out string? defaultId)
    {
        defaultId = null;
        var outputs = new List<AudioOutput>();
        IMMDevice* fallback = null;
        if (devices->GetDefaultAudioEndpoint(flow, ERole.eMultimedia, &fallback).SUCCEEDED && fallback != null)
        {
            defaultId = IdOf(fallback);
            fallback->Release();
        }
        IMMDeviceCollection* collection = null;
        if (devices->EnumAudioEndpoints(flow, DeviceStateActive, &collection).FAILED || collection == null)
        {
            return outputs;
        }
        try
        {
            uint count = 0;
            _ = collection->GetCount(&count);
            for (uint i = 0; i < count; i++)
            {
                IMMDevice* device = null;
                if (collection->Item(i, &device).FAILED || device == null)
                {
                    continue;
                }
                try
                {
                    var id = IdOf(device) ?? "";
                    var (name, form) = Describe(device);
                    var fallbackName = flow == EDataFlow.eCapture ? S.DockQuickSoundMicrophone : S.DockQuickSoundSpeakers;
                    var glyph = flow == EDataFlow.eCapture ? Microphone : GlyphFor(form);
                    outputs.Add(new AudioOutput(id, name ?? fallbackName, glyph, id == defaultId));
                }
                finally
                {
                    device->Release();
                }
            }
        }
        finally
        {
            collection->Release();
        }
        return outputs;
    }

    /// <summary>A glyph for an endpoint's form factor (EndpointFormFactor).</summary>
    internal static string GlyphFor(int form) => form switch
    {
        3 => Headphones,
        5 or 6 => Headset,
        9 => Display,
        _ => Speakers,
    };

    private static string? IdOf(IMMDevice* device)
    {
        char* id = null;
        return device->GetId(&id).SUCCEEDED ? ComUtil.FromTaskMem(id) : null;
    }

    private static (string? Name, int Form) Describe(IMMDevice* device)
    {
        IPropertyStore* store = null;
        if (device->OpenPropertyStore(STGM.STGM_READ, &store).FAILED || store == null)
        {
            return (null, -1);
        }
        try
        {
            string? name = null;
            var form = -1;
            PROPVARIANT value = default;
            var key = FriendlyName;
            if (store->GetValue(&key, &value).SUCCEEDED)
            {
                char* text = null;
                if (PropVariantToStringAlloc(&value, &text).SUCCEEDED)
                {
                    name = ComUtil.FromTaskMem(text);
                }
                _ = PropVariantClear(&value);
            }
            key = FormFactor;
            value = default;
            if (store->GetValue(&key, &value).SUCCEEDED)
            {
                if (value.vt == (ushort)VARENUM.VT_UI4)
                {
                    form = (int)value.Anonymous.Anonymous.Anonymous.ulVal;
                }
                _ = PropVariantClear(&value);
            }
            return (name, form);
        }
        finally
        {
            store->Release();
        }
    }

    /// <summary>Follows the default output: its volume, and its sessions.</summary>
    private void Attach()
    {
        Unfollow();
        Detach();
        if (devices == null || changes is null)
        {
            return;
        }
        IMMDevice* device = null;
        if (devices->GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, &device).FAILED || device == null)
        {
            attached = null;
            return;
        }
        try
        {
            attached = IdOf(device);
            IAudioEndpointVolume* endpoint = null;
            if (device->Activate(__uuidof<IAudioEndpointVolume>(), (uint)CLSCTX_ALL, null, (void**)&endpoint).SUCCEEDED && endpoint != null)
            {
                volume = endpoint;
                if (volumeSink == null)
                {
                    volumeSink = QuickSink.Create(QuickSink.VolumeTable, *(Guid*)__uuidof<IAudioEndpointVolumeCallback>(), changes, SoundNews.Volume);
                }
                _ = volume->RegisterControlChangeNotify((IAudioEndpointVolumeCallback*)volumeSink);
            }
            IAudioSessionManager2* manager = null;
            if (device->Activate(__uuidof<IAudioSessionManager2>(), (uint)CLSCTX_ALL, null, (void**)&manager).SUCCEEDED && manager != null)
            {
                sessions = manager;
            }
        }
        finally
        {
            device->Release();
        }
        created?.Dispose();
        created = attached is { } id ? CrossApartment.Watch(id, changes) : null;
    }

    private void Detach()
    {
        if (volume != null)
        {
            if (volumeSink != null)
            {
                _ = volume->UnregisterControlChangeNotify((IAudioEndpointVolumeCallback*)volumeSink);
            }
            volume->Release();
            volume = null;
        }
        if (sessions != null)
        {
            sessions->Release();
            sessions = null;
        }
        created?.Dispose();
        created = null;
        attached = null;
    }

    // --- Apps ------------------------------------------------------------------

    private delegate void SessionVisit(IAudioSessionControl* control, IAudioSessionControl2* control2, ISimpleAudioVolume* simple);

    private void Sessions(SessionVisit visit)
    {
        if (sessions == null)
        {
            return;
        }
        IAudioSessionEnumerator* list = null;
        if (sessions->GetSessionEnumerator(&list).FAILED || list == null)
        {
            return;
        }
        try
        {
            var count = 0;
            _ = list->GetCount(&count);
            for (var i = 0; i < count; i++)
            {
                IAudioSessionControl* control = null;
                IAudioSessionControl2* control2 = null;
                ISimpleAudioVolume* simple = null;
                try
                {
                    if (list->GetSession(i, &control).FAILED || control == null)
                    {
                        continue;
                    }
                    AudioSessionState state;
                    if (control->GetState(&state).FAILED || state == AudioSessionState.AudioSessionStateExpired)
                    {
                        continue;
                    }
                    if (((IUnknown*)control)->QueryInterface(__uuidof<IAudioSessionControl2>(), (void**)&control2).FAILED
                        || ((IUnknown*)control)->QueryInterface(__uuidof<ISimpleAudioVolume>(), (void**)&simple).FAILED)
                    {
                        continue;
                    }
                    visit(control, control2, simple);
                }
                finally
                {
                    ComUtil.Release(ref simple);
                    ComUtil.Release(ref control2);
                    ComUtil.Release(ref control);
                }
            }
        }
        finally
        {
            list->Release();
        }
    }

    private List<AudioApp> Apps()
    {
        var apps = new List<AudioApp>();
        Sessions((session, control2, simple) =>
        {
            var system = control2->IsSystemSoundsSession() == S_OK;
            uint pid = 0;
            _ = control2->GetProcessId(&pid);
            float level = 0;
            BOOL muted = BOOL.FALSE;
            if (simple->GetMasterVolume(&level).FAILED || simple->GetMute(&muted).FAILED)
            {
                return;
            }
            string key;
            string? exe = null;
            string? aumid = null;
            if (system)
            {
                key = "system";
            }
            else
            {
                (exe, aumid) = ProcessOf(pid);
                if (exe is null && aumid is null)
                {
                    // Gone, or not ours to look at.
                    return;
                }
                key = KeyOf(aumid, exe, pid);
            }
            if (apps.Exists(app => app.Key == key))
            {
                return;
            }
            var name = system ? S.DockQuickSoundSystem : AppLooks.NameOf(key, aumid, exe, changes);
            apps.Add(new AudioApp(key, name, Math.Clamp(level, 0, 1), muted, system, exe, aumid));
        });
        return Order(apps);
    }

    /// <summary>System sounds first, as Windows' mixer lists them, then the apps by name.</summary>
    internal static List<AudioApp> Order(List<AudioApp> apps) =>
        [.. apps.OrderByDescending(app => app.System).ThenBy(app => app.Name, StringComparer.OrdinalIgnoreCase)];

    /// <summary>What groups an app's sessions: a packaged app's AUMID, else its executable's path, else its process.</summary>
    internal static string KeyOf(string? aumid, string? exe, uint pid) =>
        aumid is { Length: > 0 } ? "aumid:" + aumid.ToLowerInvariant()
        : exe is { Length: > 0 } ? "exe:" + exe.ToLowerInvariant()
        : "pid:" + pid.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private delegate HRESULT VolumeEdit(ISimpleAudioVolume* simple);

    /// <summary>Every session of the app <paramref name="key"/>: an app playing on several streams is one app to the person.</summary>
    private void ForApp(string key, VolumeEdit edit)
    {
        Sessions((session, control2, simple) =>
        {
            var system = control2->IsSystemSoundsSession() == S_OK;
            uint pid = 0;
            _ = control2->GetProcessId(&pid);
            var mine = system ? key == "system" : !system && ProcessOf(pid) is var (exe, aumid) && (exe is not null || aumid is not null) && KeyOf(aumid, exe, pid) == key;
            if (mine)
            {
                _ = edit(simple);
            }
        });
    }

    private static (string? Exe, string? Aumid) ProcessOf(uint pid)
    {
        if (pid == 0)
        {
            return (null, null);
        }
        var process = OpenProcess(PROCESS.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (process == HANDLE.NULL)
        {
            return (null, null);
        }
        try
        {
            string? exe = null;
            var path = stackalloc char[1024];
            uint length = 1024;
            if (QueryFullProcessImageNameW(process, 0, path, &length))
            {
                exe = new string(path, 0, (int)length);
            }
            string? aumid = null;
            var id = stackalloc char[256];
            uint idLength = 256;
            if (GetApplicationUserModelId(process, &idLength, id) == 0 && idLength > 1)
            {
                aumid = new string(id, 0, (int)idLength - 1);
            }
            return (exe, aumid);
        }
        finally
        {
            _ = CloseHandle(process);
        }
    }

    /// <summary>Hears each session's own changes (its volume set by the app itself, its end), while the flyout is open.</summary>
    private void Follow()
    {
        if (changes is null)
        {
            return;
        }
        Unfollow();
        eventsSink = QuickSink.Create(QuickSink.SessionEventsTable, *(Guid*)__uuidof<IAudioSessionEvents>(), changes, SoundNews.SessionChanged);
        var sink = eventsSink;
        var held = followed;
        Sessions((control, _, _) =>
        {
            if (control->RegisterAudioSessionNotification((IAudioSessionEvents*)sink).SUCCEEDED)
            {
                // Kept to unregister from: the session itself holds the sink until then.
                ((IUnknown*)control)->AddRef();
                held.Add((nint)control);
            }
        });
    }

    private void Unfollow()
    {
        foreach (var pointer in followed)
        {
            var control = (IAudioSessionControl*)pointer;
            if (eventsSink != null)
            {
                _ = control->UnregisterAudioSessionNotification((IAudioSessionEvents*)eventsSink);
            }
            control->Release();
        }
        followed.Clear();
        QuickSink.Drop(ref eventsSink);
    }

    /// <summary>
    /// The session-created registration, made on a pool thread in the multithreaded apartment
    /// with objects of its own, and undone there too.
    /// </summary>
    private sealed class CrossApartment : IDisposable
    {
        private readonly Task<nint> registered;
        private QuickSink* sink;

        private CrossApartment(string deviceId, Changes changes)
        {
            var made = QuickSink.Create(QuickSink.SessionCreatedTable, *(Guid*)__uuidof<IAudioSessionNotification>(), changes, SoundNews.Sessions);
            sink = made;
            var address = (nint)made;
            // The pool's own reference, given up once it has unregistered: the sink
            // lives until then however soon the flyout closes.
            QuickSink.Hold(made);
            registered = Task.Run(() => Register(deviceId, address));
        }

        public static CrossApartment Watch(string deviceId, Changes changes) => new(deviceId, changes);

        private static nint Register(string deviceId, nint address)
        {
            IMMDeviceEnumerator* enumerator = null;
            IMMDevice* device = null;
            IAudioSessionManager2* manager = null;
            try
            {
                var classId = CLSID.CLSID_MMDeviceEnumerator;
                if (CoCreateInstance(&classId, null, (uint)CLSCTX_ALL, __uuidof<IMMDeviceEnumerator>(), (void**)&enumerator).FAILED)
                {
                    return 0;
                }
                fixed (char* id = deviceId)
                {
                    if (enumerator->GetDevice(id, &device).FAILED || device == null)
                    {
                        return 0;
                    }
                }
                if (device->Activate(__uuidof<IAudioSessionManager2>(), (uint)CLSCTX_ALL, null, (void**)&manager).FAILED || manager == null)
                {
                    return 0;
                }
                // Notifications only start once the sessions have been enumerated.
                IAudioSessionEnumerator* list = null;
                if (manager->GetSessionEnumerator(&list).SUCCEEDED && list != null)
                {
                    list->Release();
                }
                if (manager->RegisterSessionNotification((IAudioSessionNotification*)address).FAILED)
                {
                    return 0;
                }
                var kept = manager;
                manager = null;
                return (nint)kept;
            }
            catch (Exception)
            {
                return 0;
            }
            finally
            {
                ComUtil.Release(ref manager);
                ComUtil.Release(ref device);
                ComUtil.Release(ref enumerator);
            }
        }

        public void Dispose()
        {
            var made = sink;
            if (made == null)
            {
                return;
            }
            sink = null;
            var address = (nint)made;
            // The sink stops reporting now; the manager lets go of it on the pool.
            QuickSink* dropped = made;
            QuickSink.Drop(ref dropped);
            registered.ContinueWith(
                static (task, state) =>
                {
                    try
                    {
                        var held = (QuickSink*)(nint)state!;
                        if (task.IsCompletedSuccessfully && task.Result != 0)
                        {
                            var manager = (IAudioSessionManager2*)task.Result;
                            _ = manager->UnregisterSessionNotification((IAudioSessionNotification*)held);
                            manager->Release();
                        }
                        QuickSink.Let(held);
                    }
                    catch (Exception error)
                    {
                        Troubles.Report("Sound: letting go of the session manager", error);
                    }
                },
                address,
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default);
        }
    }
}

/// <summary>
/// The names (and, for the mixer, icons) of apps playing sound, looked up once each on a pool
/// thread: the shell's descriptions come from the file system and may take a moment. Bounded:
/// the oldest entry goes past <see cref="Most"/>.
/// </summary>
internal static class AppLooks
{
    public const int Most = 48;

    private static readonly object Gate = new();
    private static readonly Dictionary<string, (string Name, Pixels? Icon, int IconSize)> Known = [];
    private static readonly LinkedList<string> Order = [];
    private static readonly HashSet<string> Asked = [];

    /// <summary>
    /// Looks are loaded on the thread that asks for them, rather than on the pool — for a test's
    /// pictures, and nothing else. Per thread; false wherever a shipped build runs.
    /// <para>
    /// A sheet drawn while the pool was still extracting the icons drew some apps with their icon
    /// and some with the stand-in glyph, depending on which loads had happened to finish: thirteen
    /// of the seventeen sound sheets differed from one run to the next. Loading inline makes the
    /// first paint the finished one, with the real icons from the same files the dock reads — so
    /// the sheet is the same every run <i>and</i> shows what the mixer looks like, which are two
    /// requirements and not one. It goes through the very load the pool runs (<see cref="Look"/>),
    /// so a sheet cannot show an icon the dock would not.
    /// </para>
    /// </summary>
    [ThreadStatic]
    private static bool inline;

    internal static bool Inline
    {
        get => inline;
        set => inline = value;
    }

    /// <summary>The app's name when known; its executable's file name meanwhile, and <paramref name="changes"/> hears when it is known.</summary>
    public static string NameOf(string key, string? aumid, string? exe, Changes? changes)
    {
        lock (Gate)
        {
            if (Known.TryGetValue(key, out var look))
            {
                return look.Name;
            }
        }
        Ask(key, aumid, exe, 0, changes);
        return exe is not null ? Items.FileStem(exe) ?? exe : aumid ?? key;
    }

    /// <summary>The app's icon at <paramref name="size"/> px when known; null meanwhile.</summary>
    public static Pixels? IconOf(string key, string? aumid, string? exe, int size, Changes? changes)
    {
        lock (Gate)
        {
            if (Known.TryGetValue(key, out var look) && look.IconSize == size)
            {
                return look.Icon;
            }
        }
        if (Inline)
        {
            // A test's pictures (Inline): loaded here and now, so this paint has it.
            if (exe is null && aumid is null)
            {
                return null;
            }
            var (name, icon) = Look(key, aumid, exe, size);
            Keep(key, name, icon, size);
            return icon;
        }
        Ask(key, aumid, exe, size, changes);
        return null;
    }

    private static void Ask(string key, string? aumid, string? exe, int size, Changes? changes)
    {
        lock (Gate)
        {
            if (!Asked.Add(key + "|" + size))
            {
                return;
            }
        }
        _ = Task.Run(() => Troubles.Guard("Sound: an app's name and icon", () =>
        {
            if (exe is null && aumid is null)
            {
                // Nothing to look anything up by, and it stays asked: it would find nothing again.
                return;
            }
            var (name, icon) = Look(key, aumid, exe, size);
            Keep(key, name, icon, size);
            changes?.Add(SoundNews.Looks);
        }));
    }

    /// <summary>
    /// The app's name and, at <paramref name="size"/> above 0, its icon, from the shell. The one
    /// load there is: the pool runs it for the dock, and a test's pictures run it inline.
    /// </summary>
    private static (string Name, Pixels? Icon) Look(string key, string? aumid, string? exe, int size)
    {
        try
        {
            var item = Item.Running(aumid, exe ?? "");
            return (item.Name, size > 0 ? item.Icon(size) : null);
        }
        catch (Exception)
        {
            return (exe is not null ? Items.FileStem(exe) ?? exe : aumid ?? key, null);
        }
    }

    /// <summary>Keeps what <see cref="Look"/> found, the oldest going past <see cref="Most"/>.</summary>
    private static void Keep(string key, string name, Pixels? icon, int size)
    {
        lock (Gate)
        {
            Asked.Remove(key + "|" + size);
            if (Known.TryGetValue(key, out var had) && size == 0)
            {
                // A name looked up after the icon: the icon stays.
                (icon, size) = (had.Icon, had.IconSize);
            }
            Known[key] = (name, icon, size);
            Order.Remove(key);
            Order.AddLast(key);
            while (Order.Count > Most && Order.First is { } oldest)
            {
                Known.Remove(oldest.Value);
                Order.RemoveFirst();
            }
        }
    }

    /// <summary>How many apps it knows now, for tests.</summary>
    internal static int Count
    {
        get
        {
            lock (Gate)
            {
                return Known.Count;
            }
        }
    }
}

/// <summary>Made-up outputs and apps for pictures and tests; nothing reaches the audio system.</summary>
internal sealed class SampleSound : ISoundSource
{
    private float volume = 0.45f;
    private bool muted;
    private string output = "speakers";
    private string input = "array";
    private bool micMuted;
    private readonly Dictionary<string, (float Volume, bool Muted)> apps = new()
    {
        ["system"] = (1f, false),
        ["explorer"] = (0.8f, false),
        ["notepad"] = (0.6f, true),
        ["paint"] = (0.35f, false),
        ["prompt"] = (0.9f, false),
    };
    private Changes? changes;

    public void Start(Changes news) => changes = news;

    /// <summary>The microphones the samples have; empty stands for a PC with none.</summary>
    public bool Microphones { get; init; } = true;

    /// <summary>Only one output, for the picture of a PC with nothing to choose between.</summary>
    public bool OneOutput { get; init; }

    /// <summary>
    /// Exactly this many apps in the mixer, the samples' own first and made-up ones past them, for
    /// the measures of a mixer from none to past its height (FlyoutHeight); null for the usual five.
    /// </summary>
    public int? Apps { get; init; }

    public SoundState Read(int news) => new(
        true,
        volume,
        muted,
        OneOutput
            ?
            [
                new("speakers", "Speakers (Realtek(R) Audio)", WindowsSound.GlyphFor(1), true), // not-ui: a sample device's name
            ]
            :
            [
                new("speakers", "Speakers (Realtek(R) Audio)", WindowsSound.GlyphFor(1), output == "speakers"), // not-ui: a sample device's name
                new("headphones", "Headphones (WH-1000XM5)", WindowsSound.GlyphFor(3), output == "headphones"), // not-ui: a sample device's name
                new("display", "LU28R55 (NVIDIA High Definition Audio)", WindowsSound.GlyphFor(9), output == "display"), // not-ui: a sample device's name
            ],
        Mixes(),
        Microphones
            ?
            [
                new("array", "Microphone Array (Realtek(R) Audio)", Mic, input == "array"), // not-ui: a sample device's name
                new("headset", "Headset (WH-1000XM5 Hands-Free)", Mic, input == "headset"), // not-ui: a sample device's name
            ]
            : [],
        micMuted);

    /// <summary>The mixer's apps: the samples' own five, or exactly <see cref="Apps"/> of them and more.</summary>
    private List<AudioApp> Mixes()
    {
        List<AudioApp> mixes =
        [
            new("system", S.DockQuickSoundSystem, apps["system"].Volume, apps["system"].Muted, true, null, null),
            // Windows' own apps, so the samples show real icons in the mixer's rows; the last
            // name is longer than any row could show, which is why a row shows none.
            new("explorer", "File Explorer", apps["explorer"].Volume, apps["explorer"].Muted, false, null, Explorer), // not-ui: a sample app's name
            new("notepad", "Notepad", apps["notepad"].Volume, apps["notepad"].Muted, false, null, Notepad),
            new("paint", "Paint", apps["paint"].Volume, apps["paint"].Muted, false, null, Paint),
            new("prompt", "Command Prompt — a window title far longer than any row could ever show", apps["prompt"].Volume, apps["prompt"].Muted, false, null, Prompt), // not-ui: a sample app's name
        ];
        if (Apps is not { } count)
        {
            return mixes;
        }
        for (var index = mixes.Count; index < count; index++)
        {
            mixes.Add(new($"more{index}", $"Sample app {index}", 0.5f, false, false, null, Notepad)); // not-ui: a sample app's name
        }
        return mixes.Take(Math.Max(0, count)).ToList();
    }

    /// <summary>The microphone glyph the service gives an input.</summary>
    private static string Mic => char.ConvertFromUtf32(0xE720);

    private static string Windows => Environment.GetFolderPath(Environment.SpecialFolder.Windows);

    private static string Explorer => System.IO.Path.Combine(Windows, "explorer.exe");

    private static string Notepad => System.IO.Path.Combine(Windows, "System32", "notepad.exe");

    private static string Paint => System.IO.Path.Combine(Windows, "System32", "mspaint.exe");

    private static string Prompt => System.IO.Path.Combine(Windows, "System32", "cmd.exe");

    public void SetVolume(float value)
    {
        volume = value;
        changes?.Add(SoundNews.Volume);
    }

    public void SetMuted(bool value)
    {
        muted = value;
        changes?.Add(SoundNews.Volume);
    }

    public void SetDefault(string id)
    {
        output = id;
        changes?.Add(SoundNews.Devices);
    }

    public void SetDefaultInput(string id)
    {
        input = id;
        changes?.Add(SoundNews.Devices);
    }

    public void SetInputMuted(bool muted)
    {
        micMuted = muted;
        changes?.Add(SoundNews.Devices);
    }

    public void SetAppVolume(string key, float value)
    {
        apps[key] = (value, apps[key].Muted);
        changes?.Add(SoundNews.Sessions);
    }

    public void SetAppMuted(string key, bool value)
    {
        apps[key] = (apps[key].Volume, value);
        changes?.Add(SoundNews.Sessions);
    }

    public void Dispose()
    {
    }
}
