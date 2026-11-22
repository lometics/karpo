// Spatial sound for the output in use, as Windows' sound page and its quick settings offer it:
// off, or one of the formats installed for that output — Windows Sonic for Headphones, which ships
// with Windows, and whatever Dolby Atmos or DTS a person has installed.
//
// WHAT IS ON is documented: Windows.Media.Audio.SpatialAudioDeviceConfiguration for the output,
// asked by its interface path (\\?\SWD#MMDEVAPI#<Core Audio id>#{e6327cad-…}; the bare Core Audio id
// is taken without complaint and answers that nothing is supported). Its active format is a GUID,
// and none — GUID_NULL — is off. It answers an unpackaged app, and says when it changes.
//
// WHAT IS OFFERED is not documented, and is what Windows' page reads: the output's own property
// store keeps a list of the formats there are for it, one blob each under
// {A45429A4-AA63-4480-B7F8-3F2552DAEE93},2 and on, as many as {913BC9A7-624B-4A30-96AC-5064A9FC6589},2
// says. A blob is 834 bytes: the format's name at 0 (UTF-16, as the provider words it), the Store
// page of the app that brings it at 512 (empty for Windows' own), the format's GUID at 768, and at
// 784 whether it is there to be chosen — 1 for Windows Sonic, 0 for a Dolby or DTS format whose app
// is not installed, which Windows' quick list leaves out and so does this. Read on this machine
// (Windows 11 25H2) for a Bluetooth headset: Windows Sonic 1, Dolby Atmos for Headphones and for
// Speakers 0, DTS Headphone:X and DTS Virtual:X 0. A blob that is not that shape is skipped; an
// output with no list at all offers what the documented API says it supports of Windows' own.
//
// SWITCHING is SetDefaultSpatialAudioFormatAsync, which is documented but, its documentation says,
// for the app that owns the format — Windows keeps the choice for Settings, whose own road is
// internal (AudioHandlers.dll's item for it is an action, not a value an app can set). So it is
// asked, and where Windows refuses, the widget says so and a click opens the output's sound
// settings, where the person chooses. It was not asked on this machine: no test and no probe may
// change the person's spatial sound.
//
// Everything is read on the pool, and followed: Core Audio says when the default output changes,
// and the configuration says when its format does.

using System.Runtime.InteropServices;
using Karpo.Core;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
using Audio = Windows.Media.Audio;
using Foundation = Windows.Foundation;

namespace Karpo.Dock.QuickSettings.Services;

/// <summary>One spatial sound format an output offers: its GUID as Windows writes it ("{…}"), and its name.</summary>
internal sealed record SpatialFormat(string Id, string Name);

/// <summary>What the output in use says about spatial sound.</summary>
/// <param name="Supported">Whether it can do spatial sound at all: false for an output that cannot, or no output.</param>
/// <param name="Active">The format in use, as Windows writes it; <see cref="SpatialFormats.Off"/> for none.</param>
/// <param name="Formats">What it offers to be chosen, Windows' own first, as its page lists them.</param>
internal readonly record struct SpatialState(bool Supported, string Active, IReadOnlyList<SpatialFormat> Formats)
{
    public static SpatialState None => new(false, SpatialFormats.Off, []);
}

/// <summary>Spatial sound as the switch asks it: Windows', or made up in the tests.</summary>
internal interface ISpatialSource : IDisposable
{
    /// <summary>Whether it has read the output once.</summary>
    bool Looked { get; }

    /// <summary>What was last read. Any thread may ask.</summary>
    SpatialState State { get; }

    /// <summary>Reads the output and follows it; <paramref name="changed"/> comes from any thread. Blocks: on the pool.</summary>
    void Start(Action changed);

    /// <summary>Asks Windows to make <paramref name="format"/> the output's; false where it refused. Blocks: on the pool.</summary>
    bool Set(string format);
}

/// <summary>What spatial sound's answers mean, apart from Windows: the tests hold these.</summary>
internal static class SpatialFormats
{
    /// <summary>Spatial sound off: GUID_NULL, as Windows writes it.</summary>
    internal const string Off = "{00000000-0000-0000-0000-000000000000}";

    /// <summary>Windows Sonic for Headphones, which ships with Windows.</summary>
    internal const string WindowsSonic = SpatialSoundFormats.WindowsSonic;

    /// <summary>Where a format's blob keeps its name, its app's Store page, its GUID and whether it may be chosen.</summary>
    private const int NameAt = 0;
    private const int NameRoom = 256;
    private const int GuidAt = 768;
    private const int ThereAt = 784;

    /// <summary>
    /// A format as its blob in the output's property store describes it (see the top of Spatial.cs),
    /// or null for one that is not there to be chosen — its app is not installed — or a blob that is
    /// not that shape.
    /// </summary>
    internal static SpatialFormat? From(ReadOnlySpan<byte> blob)
    {
        if (blob.Length < ThereAt + sizeof(uint))
        {
            return null;
        }
        if (MemoryMarshal.Read<uint>(blob[ThereAt..]) == 0)
        {
            return null;
        }
        var text = MemoryMarshal.Cast<byte, char>(blob.Slice(NameAt, NameRoom * sizeof(char)));
        var end = text.IndexOf('\0');
        var name = (end < 0 ? text : text[..end]).ToString().Trim();
        var id = new Guid(blob.Slice(GuidAt, 16));
        return name.Length == 0 || id == Guid.Empty ? null : new SpatialFormat(Id(id), name);
    }

    /// <summary>A GUID as Windows writes a format: braced, upper case.</summary>
    internal static string Id(Guid id) => id.ToString("B").ToUpperInvariant();

    /// <summary>Whether <paramref name="format"/> is off: none at all, or GUID_NULL.</summary>
    internal static bool IsOff(string? format) =>
        string.IsNullOrEmpty(format) || !Guid.TryParse(format, out var id) || id == Guid.Empty;

    /// <summary>What <paramref name="format"/> is called, from what the output offers; null for off.</summary>
    internal static string? NameOf(SpatialState state, string format) =>
        IsOff(format) ? null
        : state.Formats.FirstOrDefault(offered => Same(offered.Id, format))?.Name
        ?? (Same(format, WindowsSonic) ? "Windows Sonic for Headphones" : S.DockQuickSpatialName); // not-ui: Windows Sonic's name, as Windows gives it in English

    /// <summary>A format's name without what it is for: "Windows Sonic for Headphones" is "Windows Sonic".</summary>
    internal static string Short(string name) =>
        name.IndexOf(" for ", StringComparison.OrdinalIgnoreCase) is var cut and > 0 ? name[..cut] : name;

    internal static bool Same(string a, string b) =>
        Guid.TryParse(a, out var left) && Guid.TryParse(b, out var right) && left == right;

    /// <summary>
    /// What a click switches to: off where a format is on; else the format the widget's card names
    /// (<paramref name="chosen"/>) where the output offers it, else the format last used here where it
    /// is still offered, else the first there is — Windows Sonic, which ships with Windows. Null where
    /// there is nothing to switch to.
    /// </summary>
    internal static string? Next(SpatialState state, string? last, string? chosen = null)
    {
        if (!IsOff(state.Active))
        {
            return Off;
        }
        foreach (var wanted in (string?[])[chosen, last])
        {
            if (wanted is not null && state.Formats.FirstOrDefault(format => Same(format.Id, wanted)) is { } offered)
            {
                return offered.Id;
            }
        }
        return state.Formats.FirstOrDefault()?.Id;
    }
}

/// <summary>Spatial sound for the default output, through Core Audio and SpatialAudioDeviceConfiguration (see the top of this file).</summary>
internal sealed unsafe class WindowsSpatial : ISpatialSource
{
    /// <summary>How many formats the output's list has (VT_I2).</summary>
    private static readonly PROPERTYKEY Count = new() { fmtid = new Guid("913BC9A7-624B-4A30-96AC-5064A9FC6589"), pid = 2 };

    /// <summary>The first format's blob; the rest follow it, one pid each.</summary>
    private static readonly Guid Formats = new("A45429A4-AA63-4480-B7F8-3F2552DAEE93");

    /// <summary>The render interface class Windows names an output's interface path with.</summary>
    private const string RenderInterface = "{e6327cad-dcec-4949-ae8a-991e976a79d2}";

    private readonly object gate = new();
    private readonly Foundation.TypedEventHandler<Audio.SpatialAudioDeviceConfiguration, object> configurationChanged;
    private Changes? news;
    private IMMDeviceEnumerator* devices;
    private QuickSink* sink;
    private Audio.SpatialAudioDeviceConfiguration? configuration;
    private SpatialState state = SpatialState.None;
    private Action? changed;
    private volatile bool looked;
    private bool disposed;

    public WindowsSpatial()
    {
        configurationChanged = (_, _) => Heard();
    }

    public bool Looked => looked;

    public SpatialState State
    {
        get
        {
            lock (gate)
            {
                return state;
            }
        }
    }

    public void Start(Action onChanged)
    {
        changed = onChanged;
        // A new default output, or an output's properties changed — its list among them — reads again.
        news = new Changes(_ => Task.Run(Heard));
        var classId = CLSID.CLSID_MMDeviceEnumerator;
        IMMDeviceEnumerator* made = null;
        if (CoCreateInstance(&classId, null, (uint)CLSCTX_ALL, __uuidof<IMMDeviceEnumerator>(), (void**)&made).SUCCEEDED && made != null)
        {
            lock (gate)
            {
                if (disposed)
                {
                    made->Release();
                    return;
                }
                devices = made;
                sink = QuickSink.Create(QuickSink.DeviceTable, *(Guid*)__uuidof<IMMNotificationClient>(), news, 1);
                _ = devices->RegisterEndpointNotificationCallback((IMMNotificationClient*)sink);
            }
        }
        Read();
        looked = true;
    }

    public bool Set(string format)
    {
        Audio.SpatialAudioDeviceConfiguration? now;
        lock (gate)
        {
            now = configuration;
        }
        if (now is null)
        {
            return false;
        }
        try
        {
            var result = now.SetDefaultSpatialAudioFormatAsync(format).AsTask().GetAwaiter().GetResult();
            Read();
            return result.Status == Audio.SetDefaultSpatialAudioFormatStatus.Succeeded;
        }
        catch (Exception error)
        {
            Troubles.Note("Spatial sound: switching", error);
            return false;
        }
    }

    public void Dispose()
    {
        Audio.SpatialAudioDeviceConfiguration? last;
        lock (gate)
        {
            if (disposed)
            {
                return;
            }
            disposed = true;
            last = configuration;
            configuration = null;
            if (devices != null)
            {
                if (sink != null)
                {
                    _ = devices->UnregisterEndpointNotificationCallback((IMMNotificationClient*)sink);
                }
                devices->Release();
                devices = null;
            }
            QuickSink.Drop(ref sink);
        }
        Unfollow(last);
    }

    /// <summary>On the pool: something changed; read again, and say so.</summary>
    private void Heard()
    {
        if (disposed)
        {
            return;
        }
        // Called from WinRT's event and from the pool: nothing thrown may leave it.
        try
        {
            _ = news?.Take();
            Read();
            changed?.Invoke();
        }
        catch (Exception error)
        {
            Troubles.Note("Spatial sound: following the output", error);
        }
    }

    /// <summary>Reads the default output: its id, what it offers and what is on; follows its configuration.</summary>
    private void Read()
    {
        string? id;
        List<SpatialFormat> offered;
        lock (gate)
        {
            if (disposed || devices == null)
            {
                return;
            }
            (id, offered) = Output();
        }
        Audio.SpatialAudioDeviceConfiguration? now = null;
        var supported = false;
        var active = SpatialFormats.Off;
        if (id is not null)
        {
            try
            {
                now = Audio.SpatialAudioDeviceConfiguration.GetForDeviceId($@"\\?\SWD#MMDEVAPI#{id}#{RenderInterface}");
                supported = now.IsSpatialAudioSupported;
                active = now.ActiveSpatialAudioFormat is { Length: > 0 } format ? format : SpatialFormats.Off;
                if (supported && offered.Count == 0 && now.IsSpatialAudioFormatSupported(SpatialFormats.WindowsSonic))
                {
                    // An output with no list of its own still has Windows' own.
                    offered.Add(new SpatialFormat(SpatialFormats.WindowsSonic, "Windows Sonic for Headphones")); // not-ui: Windows Sonic's name
                }
            }
            catch (Exception error)
            {
                Troubles.Note("Spatial sound: reading the output", error);
                now = null;
                supported = false;
            }
        }
        Audio.SpatialAudioDeviceConfiguration? before;
        lock (gate)
        {
            if (disposed)
            {
                return;
            }
            before = configuration;
            configuration = now;
            state = new SpatialState(supported, active, offered);
        }
        if (!ReferenceEquals(before, now))
        {
            Unfollow(before);
            if (now is not null)
            {
                try
                {
                    now.ConfigurationChanged += configurationChanged;
                }
                catch (Exception error)
                {
                    Troubles.Note("Spatial sound: following the output", error);
                }
            }
        }
    }

    private void Unfollow(Audio.SpatialAudioDeviceConfiguration? was)
    {
        if (was is null)
        {
            return;
        }
        try
        {
            was.ConfigurationChanged -= configurationChanged;
        }
        catch (Exception)
        {
        }
    }

    /// <summary>The default output's Core Audio id and the formats its property store lists; under the lock.</summary>
    private (string? Id, List<SpatialFormat> Formats) Output()
    {
        var found = new List<SpatialFormat>();
        IMMDevice* device = null;
        if (devices->GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, &device).FAILED || device == null)
        {
            return (null, found);
        }
        IPropertyStore* store = null;
        try
        {
            char* raw = null;
            var id = device->GetId(&raw).SUCCEEDED ? Karpo.Dock.Native.ComUtil.FromTaskMem(raw) : null;
            if (device->OpenPropertyStore(STGM.STGM_READ, &store).FAILED || store == null)
            {
                return (id, found);
            }
            PROPVARIANT value = default;
            var key = Count;
            var count = 0;
            if (store->GetValue(&key, &value).SUCCEEDED)
            {
                if (value.vt == (ushort)VARENUM.VT_I2)
                {
                    count = Math.Clamp((int)value.Anonymous.Anonymous.Anonymous.iVal, 0, 32);
                }
                _ = PropVariantClear(&value);
            }
            for (uint index = 0; index < count; index++)
            {
                key = new PROPERTYKEY { fmtid = Formats, pid = 2 + index };
                value = default;
                if (store->GetValue(&key, &value).FAILED)
                {
                    continue;
                }
                try
                {
                    if (value.vt == (ushort)VARENUM.VT_BLOB)
                    {
                        var blob = value.Anonymous.Anonymous.Anonymous.blob;
                        if (blob.pBlobData != null && SpatialFormats.From(new ReadOnlySpan<byte>(blob.pBlobData, (int)blob.cbSize)) is { } format
                            && !found.Any(other => SpatialFormats.Same(other.Id, format.Id)))
                        {
                            found.Add(format);
                        }
                    }
                }
                finally
                {
                    _ = PropVariantClear(&value);
                }
            }
            return (id, found);
        }
        finally
        {
            if (store != null)
            {
                store->Release();
            }
            device->Release();
        }
    }
}

/// <summary>A made-up output for the tests and the pictures: switching only pretends, or is refused.</summary>
internal sealed class SampleSpatial(SpatialState state, bool allow = true) : ISpatialSource
{
    private SpatialState now = state;

    public List<string> Asked { get; } = [];

    public bool Looked { get; private set; }

    public SpatialState State => now;

    public void Start(Action changed) => Looked = true;

    public bool Set(string format)
    {
        Asked.Add(format);
        if (allow)
        {
            now = now with { Active = format };
        }
        return allow;
    }

    public void Dispose()
    {
    }
}

/// <summary>
/// Spatial sound, as a toggle (see the top of this file): on while a format is, and named by it. A
/// click turns it off, or on in the format the widget's card names, else the one last used here — on
/// the bar and in a folder alike: it lists no formats (2026-10-11, "remove the spacial sound flyout").
/// Where Windows refuses the switch, the widget says so and opens the output's sound settings.
/// </summary>
internal sealed class SpatialSwitch : IQuickSwitch
{
    private readonly Func<ISpatialSource> source;
    private readonly object gate = new();
    private ISpatialSource? spatial;
    private Action? changed;
    private volatile bool looked;
    private volatile string? last;
    private volatile string? note;
    private bool disposed;

    public SpatialSwitch()
        : this(static () => new WindowsSpatial())
    {
    }

    /// <param name="source">What the output is read from and switched through; made on the pool.</param>
    internal SpatialSwitch(Func<ISpatialSource> source)
    {
        this.source = source;
    }

    public string Name => S.DockQuickSpatialName;

    private SpatialState State => Volatile.Read(ref spatial)?.State ?? SpatialState.None;

    /// <summary>How many formats the output offers: for the smoke pass.</summary>
    internal int Count => State.Formats.Count;

    /// <summary>Offered until it has looked; then where the output can do spatial sound.</summary>
    public bool Available => !looked || State.Supported;

    /// <summary>
    /// Whether a click switches it: the output has a format to switch to, and Windows has not refused
    /// Karpo a switch — after that a click opens the sound settings, which is not a switch.
    /// </summary>
    public bool Switches => looked && !refused && State.Supported && SpatialFormats.Next(State, last) is not null;

    /// <summary>Windows refused a switch, which it will again: Settings keeps the choice.</summary>
    private volatile bool refused;

    public bool? On => looked ? !SpatialFormats.IsOff(State.Active) : null;

    /// <summary>
    /// The format on, in its short form ("Windows Sonic", "Dolby Atmos"): what a card has room to say under
    /// its name, where the whole of it ("Windows Sonic for Headphones") would be left out.
    /// </summary>
    public string? Detail => looked && SpatialFormats.NameOf(State, State.Active) is { } name ? SpatialFormats.Short(name) : null;

    public string? Note => note ?? (looked && !State.Supported
        ? S.DockQuickSpatialUnsupportedNote
        : S.DockQuickSpatialNote);

    public void Start(Action onChanged)
    {
        changed = onChanged;
        // On the pool: Core Audio and the configuration are asked there (see the top of this file).
        _ = Task.Run(() =>
        {
            var made = source();
            try
            {
                made.Start(() => changed?.Invoke());
            }
            catch (Exception error)
            {
                Troubles.Note("Spatial sound", error);
            }
            lock (gate)
            {
                if (disposed)
                {
                    made.Dispose();
                    return;
                }
                spatial = made;
            }
            looked = true;
            changed?.Invoke();
        });
    }

    /// <summary>A click: off, or on again with the format last used here (<see cref="SpatialFormats.Next"/>).</summary>
    public void Act() => Act(null);

    /// <summary>
    /// A click on a widget set as <paramref name="options"/> says: off, or on in the format its card
    /// names where the output offers it, else the one last used here (<see cref="SpatialFormats.Next"/>).
    /// Before the output has been read, or where it has nothing to turn on, its sound settings.
    /// </summary>
    public void Act(SwitchOptions? options)
    {
        if (!looked || SpatialFormats.Next(State, last, options?.SpatialFormat) is not { } format)
        {
            Launch.Open("ms-settings:sound");
            return;
        }
        _ = Task.Run(() => Troubles.Guard("Spatial sound: switching the format", () => Switch(format)));
    }

    /// <summary>Asks Windows for <paramref name="format"/>; where it refuses, says so and opens the sound settings. On the pool.</summary>
    internal void Switch(string format)
    {
        if (Volatile.Read(ref spatial) is not { } through)
        {
            return;
        }
        var before = through.State.Active;
        if (through.Set(format))
        {
            note = null;
            refused = false;
            if (!SpatialFormats.IsOff(format))
            {
                last = format;
            }
            else if (!SpatialFormats.IsOff(before))
            {
                last = before;
            }
        }
        else
        {
            note = S.DockQuickSpatialRefused;
            refused = true;
            Interlocked.Increment(ref refusals);
            Refused();
        }
        changed?.Invoke();
    }

    private int refusals;

    public int Refusals => Volatile.Read(ref refusals);

    /// <summary>What a refusal opens: the sound settings, where the person chooses. A seam for the tests.</summary>
    internal Action Refused { get; init; } = static () => Launch.Open("ms-settings:sound");

    public void Dispose()
    {
        ISpatialSource? made;
        lock (gate)
        {
            disposed = true;
            made = spatial;
            spatial = null;
        }
        made?.Dispose();
    }
}
