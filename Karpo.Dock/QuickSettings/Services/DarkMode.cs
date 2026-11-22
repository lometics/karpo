// Dark mode as a toggle (2026-10-29, docs\widgets.md §403): Windows' own mode, dark or light, as
// Settings' Colors page sets it. Windows keeps it in two DWORDs under HKCU:
//
//   Software\Microsoft\Windows\CurrentVersion\Themes\Personalize
//     AppsUseLightTheme      the apps' mode ("Choose your default app mode")     0 dark, absent light
//     SystemUsesLightTheme   Windows' own: the taskbar, Start, the flyouts       0 dark, absent light
//
// WHAT IT SHOWS. On while the apps are dark. The two differ only in Settings' "Custom", and then the
// apps are what most of the screen is and what "dark mode" means to most people; Karpo's own look
// follows Windows' mode (Dock.ReadLook), so a bar in Windows' dark beside light apps shows it off.
//
// WHAT A CLICK DOES. Sets both, dark or light, as Settings' "Dark" and "Light" do: from "Custom" the
// toggle is off and a click makes everything dark. Then it says so to every window, WM_SETTINGCHANGE
// with "ImmersiveColorSet", as Settings does, so Explorer, the apps and Karpo itself change at once.
// The values are written as Windows' Settings writes them; nothing else of the theme is touched.
//
// FOLLOWED THROUGH THE DOCK'S OWN NOTIFICATION. Windows announces a change of mode with that same
// WM_SETTINGCHANGE, which the dock already reads its look again on (Dock.SettingChanged) and hands
// to its widgets' data (DataSources.System); the switch reads the two values again then, on the
// pool. No registry watch of its own.
//
// Karpo's own theming (§396) is not this: with "Follow Windows" Karpo's bars follow the switch, as
// they follow Windows' Settings; a theme that forces a mode keeps it, and the switch says Windows'.

using Karpo.Core;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock.QuickSettings.Services;

/// <summary>Where Windows keeps its mode, and how a change of it is told (see the top of this file).</summary>
internal static unsafe class DarkMode
{
    /// <summary>Windows' Personalize key under HKCU.</summary>
    public const string Key = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    /// <summary>The apps' mode: what the toggle shows.</summary>
    public const string AppsValue = "AppsUseLightTheme";

    /// <summary>Windows' own mode: the taskbar's, Start's, and Karpo's while it follows Windows.</summary>
    public const string SystemValue = "SystemUsesLightTheme";

    /// <summary>What a click opens before the mode has been read: Personalization's Colors page.</summary>
    public static string Page => WindowsPages.Colors.Uri;

    /// <summary>
    /// What <paramref name="value"/>, as the registry hands one of the two back, says: 0 dark, any
    /// other DWORD light, absent light (as Windows reads it), anything else not known. Pure.
    /// </summary>
    public static bool? Dark(object? value) => value switch
    {
        null => false,
        int dword => dword == 0,
        _ => null,
    };

    /// <summary>
    /// Tells every window the mode changed, as Settings does: WM_SETTINGCHANGE with
    /// "ImmersiveColorSet". On the pool: a window that does not answer is passed over after a while,
    /// and the dock's own answers when its thread comes to it. False where Windows refused it.
    /// </summary>
    public static bool Broadcast()
    {
        fixed (char* name = "ImmersiveColorSet")
        {
            nuint answer;
            return SendMessageTimeoutW(HWND.HWND_BROADCAST, WM.WM_SETTINGCHANGE, 0, (LPARAM)name, (uint)SMTO_ABORTIFHUNG, 5000, &answer) != 0;
        }
    }
}

/// <summary>
/// Windows' mode as a toggle (see the top of this file): on while the apps are dark; a click sets the
/// apps and Windows together and tells every window; read again on the dock's WM_SETTINGCHANGE.
/// </summary>
internal sealed class DarkModeSwitch : IQuickSwitch
{
    private readonly ISettingStore store;
    private readonly Func<bool> broadcast;
    private readonly Action<string> open;
    private Action? changed;

    // Where it stands, as last read on the pool: 1 dark, 0 light, -1 not known.
    private volatile int state = -1;
    private volatile string? note;
    private int refusals;

    public DarkModeSwitch(string name)
        : this(name, new RegistrySetting(DarkMode.Key), DarkMode.Broadcast)
    {
    }

    /// <param name="name">What its tooltip calls it.</param>
    /// <param name="store">Where the two values are read and written.</param>
    /// <param name="broadcast">Tells every window; false where Windows refused it. On the pool.</param>
    /// <param name="open">Opens a page: its settings, for a click before it has read.</param>
    internal DarkModeSwitch(string name, ISettingStore store, Func<bool> broadcast, Action<string>? open = null)
    {
        Name = name;
        this.store = store;
        this.broadcast = broadcast;
        this.open = open ?? (static page => Launch.Open(page));
    }

    public string Name { get; }

    /// <summary>Every Windows 11 has it.</summary>
    public bool Available => true;

    public bool? On => state switch { 1 => true, 0 => false, _ => null };

    /// <summary>A click switches it once the mode has been read.</summary>
    public bool Switches => state >= 0;

    public string? Note => note ?? S.DockQuickDarkModeNote;

    public int Refusals => Volatile.Read(ref refusals);

    public void Start(Action onChanged)
    {
        changed = onChanged;
        // On the pool, as every switch's first look (IQuickSwitch.Start).
        _ = Task.Run(Reread);
    }

    /// <summary>Dark from light, light from dark; before it has read, its page.</summary>
    public void Act()
    {
        if (On is not { } dark)
        {
            open(DarkMode.Page);
            return;
        }
        _ = Task.Run(() => Troubles.Guard("Dark mode", () => Switch(!dark)));
    }

    /// <summary>Windows announced a setting changed, its mode among the names it may have: read again.</summary>
    public void Heard(uint msg, nuint wParam)
    {
        if (msg == WM.WM_SETTINGCHANGE)
        {
            _ = Task.Run(Reread);
        }
    }

    /// <summary>Sets the apps and Windows <paramref name="dark"/> and tells every window. On the pool.</summary>
    internal void Switch(bool dark)
    {
        var light = dark ? 0 : 1;
        var done = store.Write(DarkMode.AppsValue, light) && store.Write(DarkMode.SystemValue, light);
        if (done)
        {
            // Told even where nothing answers: the values are written, and what follows reads them.
            _ = broadcast();
        }
        else
        {
            Interlocked.Increment(ref refusals);
        }
        note = done ? null : S.DockQuickDarkModeRefused;
        Reread();
    }

    /// <summary>Where it stands, from where Windows keeps it, and says so. On the pool.</summary>
    private void Reread()
    {
        try
        {
            state = DarkMode.Dark(store.Read(DarkMode.AppsValue)) switch { true => 1, false => 0, _ => -1 };
        }
        catch (Exception error)
        {
            Troubles.Note("Dark mode: reading it", error);
            state = -1;
        }
        changed?.Invoke();
    }

    public void Dispose() => changed = null;
}
