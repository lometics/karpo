// Game mode, color filters and mono audio as toggles (2026-10-11, docs\widgets.md §117): switched
// through the Settings handler Windows' own switch for each goes through (SettingHandlers.cs), and
// followed where Windows keeps each, a DWORD under HKCU:
//
//   game mode      Software\Microsoft\GameBar             AutoGameModeEnabled        absent = on
//   color filters  Software\Microsoft\ColorFiltering      Active                     absent = off
//                                                         FilterType 0-5 (ColorFilter)
//   mono audio     Software\Microsoft\Multimedia\Audio    AccessibilityMonoMixState  absent = off
//
// WHY THE HANDLER AND NOT THE VALUE. The value alone is what Windows reads at the next sign-in or
// game, not what it applies now: color filters stay as they are until Win+Ctrl+C or the tile, and
// mono audio until the audio service is told. The handlers are what the quick settings' tiles (color
// filters, mono audio) and game mode's Settings page call, and they apply it there and then and write
// the value as they do — each was read and switched once on Windows 11 (26300), and put back: game
// mode wrote AutoGameModeEnabled 0 and 1, color filters Active 1 and 0 (creating the key), mono audio
// AccessibilityMonoMixState 1 and 0. Where the handler is not there, the widget still shows the
// state and a click opens the setting's page.
//
// The filter a click turns color filters on in is the widget's (SwitchOptions.ColorFilter), written
// to FilterType just before the handler turns them on, which is where Windows' own page writes it
// and what the filter reads as it starts. Turning them off writes nothing but the switch.
//
// Each value is watched (RegistryWatch), so a switch flipped in Windows' own Settings, the tile, or
// Win+Ctrl+C redraws the widget with no polling. Color filters' key is not there until they have
// been used once; until it is, its parent is watched for it to appear.

using Karpo.Core;
using Microsoft.Win32;

namespace Karpo.Dock.QuickSettings.Services;

/// <summary>
/// Where one of the three is switched and kept (see the top of this file).
/// </summary>
/// <param name="Handler">The Settings handler that switches it.</param>
/// <param name="Key">Its key under HKCU.</param>
/// <param name="Value">The DWORD that says whether it is on.</param>
/// <param name="Absent">What it is while the value is not there.</param>
/// <param name="Page">The Settings page a click opens where the handler is not there.</param>
/// <param name="Filter">The DWORD the widget's color filter is written to before turning it on; null for the others.</param>
internal sealed record SettingPlace((string Library, string Id) Handler, string Key, string Value, bool Absent, string Page, string? Filter = null)
{
    public static readonly SettingPlace GameMode = new(
        SettingHandler.GameMode, @"Software\Microsoft\GameBar", "AutoGameModeEnabled", Absent: true, "ms-settings:gaming-gamemode");

    public static readonly SettingPlace ColorFilters = new(
        SettingHandler.ColorFilters, @"Software\Microsoft\ColorFiltering", "Active", Absent: false, "ms-settings:easeofaccess-colorfilter", Filter: "FilterType");

    public static readonly SettingPlace MonoAudio = new(
        SettingHandler.MonoAudio, @"Software\Microsoft\Multimedia\Audio", "AccessibilityMonoMixState", Absent: false, "ms-settings:easeofaccess-audio");

    /// <summary>
    /// What <paramref name="value"/>, as the registry hands a value back, says: absent is
    /// <see cref="Absent"/>, a DWORD is on when it is not 0, and anything else is not known. Pure.
    /// </summary>
    public bool? State(object? value) => value switch
    {
        null => Absent,
        int dword => dword != 0,
        _ => null,
    };
}

/// <summary>Where a setting's values are: the registry, or a made-up one in the tests.</summary>
internal interface ISettingStore
{
    /// <summary>The value <paramref name="name"/> as the registry hands it back, null where it is not there. On the pool.</summary>
    object? Read(string name);

    /// <summary>Writes <paramref name="name"/> as a DWORD; false where Windows refused. On the pool.</summary>
    bool Write(string name, int data);

    /// <summary>Follows the key; <paramref name="changed"/> comes on the pool. Null where it cannot be watched.</summary>
    IDisposable? Watch(Action changed);
}

/// <summary>A setting's key under HKCU, watched even before it is there (see the top of this file).</summary>
internal sealed class RegistrySetting(string key) : ISettingStore
{
    public object? Read(string name)
    {
        try
        {
            using var opened = Registry.CurrentUser.OpenSubKey(key, writable: false);
            return opened?.GetValue(name);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return null;
        }
    }

    public bool Write(string name, int data)
    {
        try
        {
            using var opened = Registry.CurrentUser.CreateSubKey(key, writable: true);
            opened.SetValue(name, data, RegistryValueKind.DWord);
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            Troubles.Note($"Writing {name}", error);
            return false;
        }
    }

    public IDisposable? Watch(Action changed) => new Following(key, changed);

    /// <summary>The key's watch, or — until the key is there — its parent's, for the key to appear.</summary>
    private sealed class Following : IDisposable
    {
        private readonly string key;
        private readonly Action changed;
        private readonly object gate = new();
        private IDisposable? own;
        private IDisposable? parent;
        private bool disposed;

        public Following(string key, Action changed)
        {
            this.key = key;
            this.changed = changed;
            if (Watched())
            {
                return;
            }
            var waiting = RegistryWatch.Open(RegistryHive.CurrentUser, key[..Math.Max(0, key.LastIndexOf('\\'))], subtree: false, Appeared);
            lock (gate)
            {
                if (!disposed && own is null)
                {
                    parent = waiting;
                    waiting = null;
                }
            }
            waiting?.Dispose();
            // The key may have come while the parent's watch was being set up.
            Appeared();
        }

        /// <summary>Watches the key itself, and no longer its parent; false where it is not there yet.</summary>
        private bool Watched()
        {
            var made = RegistryWatch.Open(RegistryHive.CurrentUser, key, subtree: false, changed);
            if (made is null)
            {
                return false;
            }
            IDisposable? dropped;
            lock (gate)
            {
                if (disposed || own is not null)
                {
                    dropped = made;
                }
                else
                {
                    own = made;
                    dropped = parent;
                    parent = null;
                }
            }
            dropped?.Dispose();
            return true;
        }

        /// <summary>Something under the parent changed: once the key is there, it is watched instead, and read.</summary>
        private void Appeared()
        {
            lock (gate)
            {
                if (disposed || own is not null)
                {
                    return;
                }
            }
            if (Watched())
            {
                changed();
            }
        }

        public void Dispose()
        {
            IDisposable? first;
            IDisposable? second;
            lock (gate)
            {
                disposed = true;
                (first, second) = (own, parent);
                (own, parent) = (null, null);
            }
            first?.Dispose();
            second?.Dispose();
        }
    }
}

/// <summary>
/// Game mode, color filters or mono audio as a toggle (see the top of this file): on and off through
/// the Settings handler, followed where Windows keeps it. Until the handler is found a click opens
/// its page; where Windows says the setting does not apply here, it is not available.
/// </summary>
internal sealed class SettingToggle : IQuickSwitch
{
    private readonly SettingPlace place;
    private readonly ISettingStore store;
    private readonly Func<ISettingToggle?> handler;
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

    public SettingToggle(string name, string about, SettingPlace place)
        : this(name, about, place, new RegistrySetting(place.Key), () => SettingHandler.Open(place.Handler))
    {
    }

    /// <param name="name">What its tooltip calls it.</param>
    /// <param name="about">What it does, under the state in its tooltip.</param>
    /// <param name="place">Where it is switched and kept.</param>
    /// <param name="store">Where its values are read, followed and written.</param>
    /// <param name="handler">The Settings handler, or null where it is not there; called on the pool.</param>
    internal SettingToggle(string name, string about, SettingPlace place, ISettingStore store, Func<ISettingToggle?> handler)
    {
        Name = name;
        About = about;
        this.place = place;
        this.store = store;
        this.handler = handler;
    }

    public string Name { get; }

    private string About { get; }

    /// <summary>Whether it goes through the Settings handler, once it has looked: for the smoke pass.</summary>
    internal bool ThroughTile => Volatile.Read(ref tile) is not null;

    public bool Available => usable;

    public bool? On => state switch { 1 => true, 0 => false, _ => null };

    /// <summary>Whether a click switches it: once the handler has been found and the state read.</summary>
    public bool Switches => looked && Volatile.Read(ref tile) is not null && state >= 0;

    public string? Note => note ?? (looked && Volatile.Read(ref tile) is null ? S.DockQuickSettingNotHere : About);

    public void Start(Action onChanged)
    {
        changed = onChanged;
        // On the pool: the handler starts up on a thread of its own (IQuickSwitch.Start).
        _ = Task.Run(() =>
        {
            ISettingToggle? found = null;
            try
            {
                found = handler();
            }
            catch (Exception error)
            {
                Troubles.Note(place.Handler.Id, error);
            }
            var offered = found?.Usable ?? true;
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
                    (found as SettingHandler)?.Release();
                    return;
                }
                tile = found;
                watch = following;
            }
            usable = offered;
            Read();
            looked = true;
            changed?.Invoke();
        });
    }

    public void Act() => Act(null);

    /// <summary>Flips it, color filters on in the filter <paramref name="options"/> names; before it can, opens its page.</summary>
    public void Act(SwitchOptions? options)
    {
        if (!Switches)
        {
            Launch.Open(place.Page);
            return;
        }
        var on = state != 1;
        var filter = ColorFilterChoices.Known(options?.ColorFilter ?? ColorFilter.Grayscale);
        _ = Task.Run(() => Troubles.Guard(place.Handler.Id, () => Switch(on, filter)));
    }

    /// <summary>Switches it <paramref name="on"/>, color filters in <paramref name="filter"/>. On the pool.</summary>
    internal void Switch(bool on, ColorFilter filter)
    {
        if (Volatile.Read(ref tile) is not { } through)
        {
            return;
        }
        if (on && place.Filter is { } value)
        {
            _ = store.Write(value, (int)filter);
        }
        var done = through.Set(on);
        note = done ? null : S.DockQuickRadioRefused;
        if (!done)
        {
            Interlocked.Increment(ref refusals);
        }
        Read();
        changed?.Invoke();
    }

    private int refusals;

    public int Refusals => Volatile.Read(ref refusals);

    /// <summary>Where it stands, from where Windows keeps it. On the pool.</summary>
    private void Read() => state = place.State(store.Read(place.Value)) switch { true => 1, false => 0, _ => -1 };

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
