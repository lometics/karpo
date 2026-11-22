// Night light, switched the way Windows' own tile switches it and followed where Windows keeps it.
//
// THE SWITCH is the tile's own Settings handler (SettingHandlers.cs): SettingsHandlers_Display.dll's
// SystemSettings_Display_BlueLight_ManualToggleQuickAction, whose "Value" the tile sets. The handler
// writes night light's state through the account's CloudStore, and the display service follows
// that the moment it changes. It also says whether night light applies here at all — a display
// that cannot do it, a remote session — which is when Windows leaves the tile out, and then so
// does this: the widget says it is not available.
//
// THE STATE is read where the handler writes it, the CloudStore's value
//
//   HKCU\Software\Microsoft\Windows\CurrentVersion\CloudStore\Store\DefaultAccount\Current\
//     default$windows.data.bluelightreduction.bluelightreductionstate\
//     windows.data.bluelightreduction.bluelightreductionstate  "Data" (REG_BINARY)
//
// which is watched (RegistryWatch), so a switch in Windows' own panel, or night light's schedule
// at sunset, redraws the button with no polling. THE VALUE IS WHERE IT STANDS, and the handler
// answers only where the value does not read: the handler's item, made once in this process, kept
// saying "on" after the schedule had turned night light off at sunrise (2026-10-16, the person:
// "night light was on but it was actually off on the computer, it got disabled via schedule and
// then it didnt update") — the value had changed and the watch had fired, but what was asked on it
// was the handler's stale answer. The watch is on the value's parent key and everything under it,
// so the CloudStore writing the value's key again from scratch is heard as well.
//
// The value is Microsoft Bond, compact binary v1, marshalled ("CB", version 1) — which is what lets
// it be read as a structure rather than as bytes at fixed places. Auto Dark Mode reads it this way,
// and kvnxiao/win-nightlight-cli, tiny-screen and nightlight-cli write it; the tools agree on what
// it means and differ only in how much of its shape they assume:
//
//   43 42 01 00                      marshalled compact binary, version 1
//   0A 02 01 00                      field 0: a struct { field 0: true }
//   2A                               field 1: a struct {
//     06 <varint>                      field 0: when it was written, in Unix seconds
//     2A 2B 0E <varint n> <n bytes>    field 1: a struct { field 1: the state, n bytes }
//   00 00 00                         }}}
//
//   the state, marshalled again:
//   43 42 01 00
//   10 00                            field 0 (int32), only while night light is on
//   D0 0A <varint>                   field 10 (int32), on some PCs and not on others
//   C6 14 <varint>                   field 20: when it last changed, as a FILETIME
//   00
//
// Where the handler is not there, a switch is that value written again with field 0 written or left
// out, both times set to now and the one length in front of the state counted again. Anything that
// does not read as exactly this — a value from a Windows that keeps more, or none at all because
// night light was never set up — is never written: the widget shows no state and a click opens night
// light's settings, because a guessed rewrite of a value the display service reads is not a risk
// worth a button.

using Microsoft.Win32;
using Karpo.Core;

namespace Karpo.Dock.QuickSettings.Services;

/// <summary>Night light's state value, read and written as Bond compact binary (see the top of this file). Pure.</summary>
internal static class NightLightBlob
{
    /// <summary>
    /// What a value says: when it was written, whether night light is on, the state's field 10 as
    /// it was stored (null where the value has none), and when it last changed.
    /// </summary>
    internal readonly record struct State(ulong Written, bool On, ulong Changed, ulong? Ten = null);

    private static ReadOnlySpan<byte> Marshalled => [0x43, 0x42, 0x01, 0x00];

    private static ReadOnlySpan<byte> Header => [0x0A, 0x02, 0x01, 0x00];

    private static ReadOnlySpan<byte> Stamp => [0x2A, 0x06];

    private static ReadOnlySpan<byte> Inner => [0x2A, 0x2B, 0x0E];

    private static ReadOnlySpan<byte> Close => [0x00, 0x00, 0x00];

    private static ReadOnlySpan<byte> OnField => [0x10, 0x00];

    private static ReadOnlySpan<byte> TenField => [0xD0, 0x0A];

    private static ReadOnlySpan<byte> ChangedField => [0xC6, 0x14];

    /// <summary>What <paramref name="data"/> says, or null where it is not exactly the shape above.</summary>
    internal static State? Read(ReadOnlySpan<byte> data)
    {
        var at = 0;
        if (!Take(data, ref at, Marshalled) || !Take(data, ref at, Header) || !Take(data, ref at, Stamp)
            || Varint(data, ref at) is not { } written
            || !Take(data, ref at, Inner)
            || Varint(data, ref at) is not { } length || length > (ulong)(data.Length - at))
        {
            return null;
        }
        var inner = data.Slice(at, (int)length);
        at += (int)length;
        if (!Take(data, ref at, Close) || at != data.Length)
        {
            return null;
        }
        var part = 0;
        if (!Take(inner, ref part, Marshalled))
        {
            return null;
        }
        var on = Take(inner, ref part, OnField);
        ulong? ten = null;
        if (Take(inner, ref part, TenField))
        {
            if (Varint(inner, ref part) is not { } stored)
            {
                return null;
            }
            ten = stored;
        }
        if (!Take(inner, ref part, ChangedField) || Varint(inner, ref part) is not { } changed
            || part != inner.Length - 1 || inner[part] != 0x00)
        {
            return null;
        }
        return new State(written, on, changed, ten);
    }

    /// <summary>Whether night light is on, or null where the value is not one this reads.</summary>
    internal static bool? IsOn(ReadOnlySpan<byte> data) => Read(data)?.On;

    /// <summary>
    /// <paramref name="data"/> with night light <paramref name="on"/>, written at
    /// <paramref name="now"/>; null where the value is not one this reads, which is then left
    /// alone. Both times move forward even on a clock set back, since the service takes a value
    /// that is not newer than the one it has for no change at all.
    /// </summary>
    internal static byte[]? Switched(ReadOnlySpan<byte> data, bool on, DateTimeOffset now)
    {
        if (Read(data) is not { } state)
        {
            return null;
        }
        var written = Math.Max((ulong)Math.Max(0, now.ToUnixTimeSeconds()), state.Written + 1);
        var changed = Math.Max((ulong)Math.Max(0, now.ToFileTime()), state.Changed + 1);
        return Write(state with { On = on, Written = written, Changed = changed });
    }

    /// <summary>A value of the shape above.</summary>
    internal static byte[] Write(State state)
    {
        var inner = new List<byte>(32);
        inner.AddRange(Marshalled);
        if (state.On)
        {
            inner.AddRange(OnField);
        }
        if (state.Ten is { } ten)
        {
            inner.AddRange(TenField);
            Varint(inner, ten);
        }
        inner.AddRange(ChangedField);
        Varint(inner, state.Changed);
        inner.Add(0x00);
        var outer = new List<byte>(64);
        outer.AddRange(Marshalled);
        outer.AddRange(Header);
        outer.AddRange(Stamp);
        Varint(outer, state.Written);
        outer.AddRange(Inner);
        Varint(outer, (ulong)inner.Count);
        outer.AddRange(inner);
        outer.AddRange(Close);
        return [.. outer];
    }

    /// <summary>Whether <paramref name="data"/> goes on with <paramref name="expected"/>; moves past it if so.</summary>
    private static bool Take(ReadOnlySpan<byte> data, ref int at, ReadOnlySpan<byte> expected)
    {
        if (data.Length - at < expected.Length || !data.Slice(at, expected.Length).SequenceEqual(expected))
        {
            return false;
        }
        at += expected.Length;
        return true;
    }

    /// <summary>A little-endian base-128 number, as compact binary writes every integer; null where it runs off the end.</summary>
    private static ulong? Varint(ReadOnlySpan<byte> data, ref int at)
    {
        ulong value = 0;
        for (var shift = 0; shift < 64 && at < data.Length; shift += 7)
        {
            var next = data[at++];
            value |= (ulong)(next & 0x7F) << shift;
            if ((next & 0x80) == 0)
            {
                return value;
            }
        }
        return null;
    }

    private static void Varint(List<byte> into, ulong value)
    {
        do
        {
            var next = (byte)(value & 0x7F);
            value >>= 7;
            into.Add(value == 0 ? next : (byte)(next | 0x80));
        }
        while (value != 0);
    }
}

/// <summary>Where night light's state is kept: the registry, or a made-up one in the tests.</summary>
internal interface INightLightStore
{
    /// <summary>The value as it is now, or null where there is none. Called on the pool.</summary>
    byte[]? Read();

    /// <summary>Writes the value; false where Windows refused.</summary>
    bool Write(byte[] data);

    /// <summary>Follows the value; <paramref name="changed"/> comes on the pool. Null where it cannot be watched.</summary>
    IDisposable? Watch(Action changed);
}

/// <summary>Night light's state where Windows keeps it (see the top of this file).</summary>
internal sealed class RegistryNightLight : INightLightStore
{
    internal const string Key =
        @"Software\Microsoft\Windows\CurrentVersion\CloudStore\Store\DefaultAccount\Current\"
        + @"default$windows.data.bluelightreduction.bluelightreductionstate\"
        + @"windows.data.bluelightreduction.bluelightreductionstate";

    private const string Value = "Data";

    public byte[]? Read()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(Key, writable: false);
            return key?.GetValue(Value) as byte[];
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return null;
        }
    }

    public bool Write(byte[] data)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(Key, writable: true);
            if (key is null)
            {
                return false;
            }
            key.SetValue(Value, data, RegistryValueKind.Binary);
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            Troubles.Note("Night light: writing its state", error);
            return false;
        }
    }

    /// <summary>The key the value's key stands in, watched with everything under it (see the top of this file).</summary>
    internal static string Parent => Key[..Key.LastIndexOf('\\')];

    public IDisposable? Watch(Action changed) =>
        RegistryWatch.Open(RegistryHive.CurrentUser, Parent, subtree: true, changed);
}

/// <summary>
/// Night light, on and off as Windows' own tile switches it, and followed where Windows keeps it
/// (see the top of this file). Where Windows leaves the tile out, it is not available; where
/// neither the handler nor the value can be used, it shows no state and a click opens night
/// light's settings.
/// </summary>
internal sealed class NightLightSwitch : IQuickSwitch
{
    private readonly INightLightStore store;
    private readonly Func<ISettingToggle?> handler;
    private readonly Func<DateTimeOffset> now;
    private readonly object gate = new();
    private ISettingToggle? tile;
    private IDisposable? watch;
    private Action? changed;

    // Where it stands, as last read on the pool: 1 on, 0 off, -1 not known.
    private volatile int state = -1;
    private volatile bool usable = true;
    private volatile bool looked;
    private volatile string? note;
    private bool disposed;

    public NightLightSwitch()
        : this(new RegistryNightLight(), static () => SettingHandler.Open(SettingHandler.NightLight), static () => Clock.RealOffsetUtc())
    {
    }

    /// <param name="store">Where the state is read, followed, and written where there is no handler.</param>
    /// <param name="handler">The tile's own handler, or null where it is not there; called on the pool.</param>
    /// <param name="now">The time a written value carries.</param>
    internal NightLightSwitch(INightLightStore store, Func<ISettingToggle?> handler, Func<DateTimeOffset> now)
    {
        this.store = store;
        this.handler = handler;
        this.now = now;
    }

    public string Name => S.DockQuickNightLightName;

    /// <summary>Whether it goes through the tile's own handler, once it has looked: for the smoke pass.</summary>
    internal bool ThroughTile => Volatile.Read(ref tile) is not null;

    /// <summary>Offered until it has looked; then wherever Windows offers the tile.</summary>
    public bool Available => usable;

    public bool? On => state switch { 1 => true, 0 => false, _ => null };

    /// <summary>Whether a click switches it (<see cref="Act"/>): once it has looked, where Windows offers it and says its state.</summary>
    public bool Switches => looked && usable && state >= 0;

    /// <summary>
    /// What a click does, once that is known: switches it, or — where nothing here can — opens its
    /// settings. While the look is still going the line is the plain one.
    /// </summary>
    public string? Note => note ?? (!usable
        ? S.DockQuickNightLightNone
        : looked && state < 0
            ? S.DockQuickNightLightUnread
            : S.DockQuickNightLightNote);

    public void Start(Action onChanged)
    {
        changed = onChanged;
        // On the pool: the handler starts up on a thread of its own, and nothing is asked of
        // Windows on the dock's thread (IQuickSwitch.Start).
        _ = Task.Run(() =>
        {
            ISettingToggle? found = null;
            try
            {
                found = handler();
            }
            catch (Exception error)
            {
                Troubles.Note("Night light", error);
            }
            var following = store.Watch(() =>
            {
                Read();
                changed?.Invoke();
            });
            lock (gate)
            {
                if (disposed)
                {
                    following?.Dispose();
                    return;
                }
                tile = found;
                watch = following;
            }
            Read();
            looked = true;
            changed?.Invoke();
        });
    }

    /// <summary>Switches it where anything here can; otherwise, and before it has looked, opens its settings.</summary>
    public void Act()
    {
        if (!looked || !usable || state < 0)
        {
            Launch.Open("ms-settings:nightlight");
            return;
        }
        var on = state != 1;
        _ = Task.Run(() => Troubles.Guard("Night light: switching it", () => Switch(on)));
    }

    /// <summary>Switches night light <paramref name="on"/>: through the tile's handler, else by its value. On the pool.</summary>
    internal void Switch(bool on)
    {
        bool done;
        var through = Volatile.Read(ref tile);
        if (through is not null)
        {
            done = through.Set(on);
        }
        else if (store.Read() is { } data && NightLightBlob.Switched(data, on, now()) is { } next)
        {
            done = store.Write(next);
        }
        else
        {
            state = -1;
            note = null;
            changed?.Invoke();
            return;
        }
        note = done ? null : S.DockQuickNightLightRefused;
        if (!done)
        {
            Interlocked.Increment(ref refusals);
        }
        Read();
        if (done && through is not null)
        {
            // The handler writes the value on its own time; until the watch hears it, it is where
            // the handler just set it, not where the value still says it was.
            state = on ? 1 : 0;
        }
        changed?.Invoke();
    }

    private int refusals;

    public int Refusals => Volatile.Read(ref refusals);

    /// <summary>
    /// Where it stands: the value, else the tile's handler where the value does not read (see the top
    /// of this file); and whether Windows offers it. On the pool.
    /// </summary>
    private void Read()
    {
        var through = Volatile.Read(ref tile);
        if (through is not null)
        {
            usable = through.Usable;
        }
        var said = (store.Read() is { } data ? NightLightBlob.IsOn(data) : null) ?? through?.Value();
        state = said switch { true => 1, false => 0, _ => -1 };
    }

    public void Dispose()
    {
        ISettingToggle? found;
        lock (gate)
        {
            disposed = true;
            watch?.Dispose();
            watch = null;
            found = tile;
            tile = null;
        }
        (found as SettingHandler)?.Release();
    }
}
