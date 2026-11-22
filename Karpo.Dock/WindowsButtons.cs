// What Windows' own buttons open, beside Start, search and task view: the widgets board, the pen
// menu, the touch keyboard and the virtual touchpad (BarEntries.IsWindowsButton). The bar
// draws each as one glyph in a button's well, as it draws those three (Scene), and a click opens
// what Windows' taskbar opens, found the way Windows finds it:
//
//   * the widgets board: Win+W, the shortcut itself, as search is Win+S;
//   * the pen menu and the virtual touchpad: the addresses Windows registers for them
//     (ms-penworkspace:, ms-virtualtouchpad:), which the shell hands to their own hosts;
//   * the touch keyboard: its own switch (ITipInvocation.Toggle), which shows it and hides it again
//     as the taskbar's button does — and, where nothing answers it yet, its host started, which
//     opens it.
//
// And two shortcuts to Windows' apps the person asked for, drawn the same way (the backlog, items
// 10 and 18), which a list's line opens too (ShortcutTile). Phone Link was a third
// (item 23) until the person asked for it gone: a pin to the app does the same. Copilot had a button
// here too until the person asked for it gone (2026-10-09).
//
//   * the Snipping Tool: a snip started straight away, as Win+Shift+S does. Microsoft's documented
//     way in for an app (ms-screenclip://capture/…, 2025) answers a packaged app only — it needs a
//     redirect-uri that only a package can receive, and without one the Snipping Tool shows nothing
//     — so Karpo, unpackaged, uses the address the Snipping Tool has always answered, bare
//     ms-screenclip:, which opens the snip overlay as the Print Screen key does. Its older
//     parameters are still read by the installed app (its own buttons build them: delayInSeconds,
//     type=recording), and they are the menu's delayed snips and its "Record screen" — the
//     recording since Snipping Tool 11.2307. Where the Snipping Tool is not installed the button
//     says so, as a pin to an app that is not here does: faint with a warning badge, "This app
//     isn't installed on this PC", and a way to get it from the Microsoft Store — Win+Shift+S is
//     the Snipping Tool's own overlay, so pressing it instead would do nothing at all;
//   * Settings: ms-settings:, as Win+I.
//
// And Windows' menus (§466), a button each, grouped as "Windows menus" in the add list, each opened by
// the shortcut Windows itself answers to — which is also what a person presses, so a second press
// closes it as the shortcut does — or, with no shortcut, by its address:
//
//   * the quick link menu: Win+X;
//   * the notifications and calendar: Win+N; quick settings: Win+A; its sound output page:
//     Win+Ctrl+V; its Wi-Fi page: ms-availablenetworks:, which has no shortcut;
//   * cast: Win+K; clipboard history: Win+V; the emoji panel: Win+. (VK_OEM_PERIOD). The bar never
//     takes the focus, so what clipboard history pastes and the emoji panel types goes to the app
//     the person was in.
//
// Which of these a kind asks for is Target, a question with no side effects, so the tests hold it
// without pressing a key or starting anything (WindowsButtonsTests); Open is Target handed to
// whatever does it, which is the dock's own launching on the bar. The Widgets button and Windows' menus
// wear faces rather than a glyph (§474, §490, WindowsFaces: the weather; quick settings' network, volume
// and battery; the clock and the bell; the volume, the network, cast, the clipboard, the quick link menu's
// and the emoji panel's glyph); what they open is this table's all the same.

using Karpo.Core;
using Microsoft.Win32;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

/// <summary>How a click on one of Windows' buttons opens what it opens (<see cref="ShellTarget"/>).</summary>
internal enum OpenBy
{
    /// <summary>
    /// The Windows key with another, as the keyboard shortcut is pressed: <see cref="ShellTarget.Key"/>,
    /// and <see cref="ShellTarget.Held"/> held with them where there is one.
    /// </summary>
    Shortcut,

    /// <summary>An address the shell opens (<see cref="ShellTarget.Address"/>): an app's, a protocol's, a web page's.</summary>
    Address,

    /// <summary>The touch keyboard's own switch.</summary>
    TouchKeyboard,

    /// <summary>
    /// Nothing to open: the app is not installed. The bar says so, and <see cref="ShellTarget.Address"/>
    /// is where to get it (its page in the Microsoft Store).
    /// </summary>
    NotInstalled,
}

/// <summary>What a click on one of Windows' buttons opens, and how.</summary>
/// <param name="By">How it is opened.</param>
/// <param name="Address">The address the shell opens, for <see cref="OpenBy.Address"/>.</param>
/// <param name="Key">The key pressed with the Windows key, for <see cref="OpenBy.Shortcut"/>.</param>
/// <param name="Held">A key held as well, for <see cref="OpenBy.Shortcut"/>, or 0: Ctrl, for Win+Ctrl+V.</param>
internal readonly record struct ShellTarget(OpenBy By, string Address = "", ushort Key = 0, ushort Held = 0);

internal static unsafe class WindowsButtons
{
    /// <summary>The address Windows registers for its pen menu (ShellExperienceHost).</summary>
    internal const string PenMenu = "ms-penworkspace:";

    /// <summary>The address Windows registers for its virtual touchpad.</summary>
    internal const string VirtualTouchpad = "ms-virtualtouchpad:";

    /// <summary>The widgets board's shortcut, Win+W: the virtual-key code of W.</summary>
    internal const ushort WidgetsKey = 'W';

    /// <summary>windowsMenus: quick settings' Wi-Fi page, the networks around (no shortcut opens it).</summary>
    internal const string Networks = "ms-availablenetworks:";

    /// <summary>windowsMenus: the period key, VK_OEM_PERIOD: Win+. is the emoji panel.</summary>
    internal const ushort PeriodKey = 0xBE;

    /// <summary>The Snipping Tool's package family: Microsoft's, preinstalled and from the Store alike.</summary>
    internal const string SnippingFamily = "Microsoft.ScreenSketch_8wekyb3d8bbwe";

    /// <summary>The Snipping Tool app itself, its window rather than a snip.</summary>
    internal const string SnippingApp = @"shell:AppsFolder\" + SnippingFamily + "!App";

    /// <summary>A snip, now: the overlay to drag a rectangle on, as the Print Screen key opens it.</summary>
    internal const string Snip = "ms-screenclip:";

    /// <summary>A recording of the screen: the Snipping Tool's recorder, an area to drag first.</summary>
    internal const string Record = "ms-screenclip:?type=recording";

    /// <summary>The first Snipping Tool whose ms-screenclip: answers type=recording.</summary>
    internal static readonly Version RecordsSince = new(11, 2307);

    /// <summary>The Snipping Tool's page in the Microsoft Store, by its package family: where to get it.</summary>
    internal const string SnippingStore = "ms-windows-store://pdp/?PFN=" + SnippingFamily;

    /// <summary>Windows' Settings app, as Win+I opens it.</summary>
    internal const string SettingsApp = "ms-settings:";

    /// <summary>What a shortcut whose app is not here says: a pin's words for an app that is not installed.</summary>
    internal static string NotInstalledReason => S.DockPinNotInstalled;

    /// <summary>A snip after <paramref name="seconds"/> seconds (1 to 30), as the Snipping Tool's own delay.</summary>
    internal static string SnipIn(int seconds) => $"ms-screenclip:?delayInSeconds={Math.Clamp(seconds, 1, 30)}";

    /// <summary>
    /// What a click on Windows' button of <paramref name="kind"/> opens, where
    /// <paramref name="installed"/> says whether a package family is installed for this person; null
    /// for a kind that is not one of these buttons.
    /// </summary>
    public static ShellTarget? Target(BarKind kind, Func<string, bool> installed) => kind switch
    {
        BarKind.WidgetsBoard => new ShellTarget(OpenBy.Shortcut, Key: WidgetsKey),
        BarKind.PenMenu => new ShellTarget(OpenBy.Address, PenMenu),
        BarKind.TouchKeyboard => new ShellTarget(OpenBy.TouchKeyboard),
        BarKind.VirtualTouchpad => new ShellTarget(OpenBy.Address, VirtualTouchpad),
        BarKind.SnippingTool => installed(SnippingFamily)
            ? new ShellTarget(OpenBy.Address, Snip)
            : new ShellTarget(OpenBy.NotInstalled, SnippingStore),
        BarKind.WindowsSettings => new ShellTarget(OpenBy.Address, SettingsApp),
        BarKind.QuickLink => new ShellTarget(OpenBy.Shortcut, Key: 'X'),
        BarKind.WindowsNotifications => new ShellTarget(OpenBy.Shortcut, Key: 'N'),
        BarKind.WindowsQuickSettings => new ShellTarget(OpenBy.Shortcut, Key: 'A'),
        BarKind.WindowsSound => new ShellTarget(OpenBy.Shortcut, Key: 'V', Held: (ushort)VK.VK_CONTROL),
        BarKind.WindowsNetworks => new ShellTarget(OpenBy.Address, Networks),
        BarKind.WindowsCast => new ShellTarget(OpenBy.Shortcut, Key: 'K'),
        BarKind.WindowsClipboard => new ShellTarget(OpenBy.Shortcut, Key: 'V'),
        BarKind.EmojiPanel => new ShellTarget(OpenBy.Shortcut, Key: PeriodKey),
        _ => null,
    };

    /// <summary>
    /// Why the button of <paramref name="kind"/> opens nothing on this PC — its app is not installed —
    /// or null where it opens what it stands for. Only the Snipping Tool can be missing: the rest
    /// are Windows' own.
    /// </summary>
    public static string? Missing(BarKind kind, Func<string, bool> installed) =>
        Target(kind, installed) is { By: OpenBy.NotInstalled } ? NotInstalledReason : null;

    /// <summary>
    /// <see cref="Missing(BarKind, Func{string, bool})"/> on this PC, by the packages installed for
    /// this person as <see cref="VersionOf"/> reads them — at most every few seconds, since the bar
    /// asks each time it is drawn.
    /// </summary>
    public static string? Missing(BarKind kind) => kind.IsWindowsOwn() ? Missing(kind, InstalledNow) : null;

    /// <summary>
    /// What a widget's own click opens — a card's or a line's in a folder, which has no dock to hand
    /// (ShortcutTile): <see cref="Target(BarKind, Func{string, bool})"/> pressed or opened there and
    /// then. A test puts a list in its place, so nothing is pressed or started on the machine.
    /// </summary>
    internal static Action<ShellTarget> Press = PressNow;

    /// <summary>A target carried out: its keys pressed, its address opened, the keyboard switched.</summary>
    private static void PressNow(ShellTarget target)
    {
        switch (target.By)
        {
            case OpenBy.Shortcut:
                Keys(target.Key, target.Held);
                break;
            // Where an app is not installed, its page in the Store: a card cannot say more than it
            // shows, and that is the way to get it.
            case OpenBy.Address or OpenBy.NotInstalled when target.Address.Length > 0:
                _ = Launch.Open(target.Address);
                break;
            case OpenBy.TouchKeyboard:
                _ = ToggleTouchKeyboard();
                break;
        }
    }

    /// <summary>
    /// The Windows key with <paramref name="key"/> (and <paramref name="held"/>, where it is not 0),
    /// pressed and let go, as the keyboard sends them.
    /// </summary>
    internal static void Keys(ushort key, ushort held = 0)
    {
        var count = held != 0 ? 3 : 2;
        var keys = stackalloc ushort[3];
        keys[0] = (ushort)VK.VK_LWIN;
        keys[1] = held != 0 ? held : key;
        keys[2] = key;
        var inputs = stackalloc INPUT[6];
        for (var index = 0; index < count; index++)
        {
            var extended = keys[index] == (ushort)VK.VK_LWIN ? (uint)KEYEVENTF_EXTENDEDKEY : 0;
            inputs[index].type = INPUT.INPUT_KEYBOARD;
            inputs[index].ki.wVk = keys[index];
            inputs[index].ki.dwFlags = extended;
            var up = (count * 2) - 1 - index;
            inputs[up].type = INPUT.INPUT_KEYBOARD;
            inputs[up].ki.wVk = keys[index];
            inputs[up].ki.dwFlags = extended | (uint)KEYEVENTF_KEYUP;
        }
        _ = SendInput((uint)(count * 2), inputs, sizeof(INPUT));
    }

    /// <summary>
    /// Whether the Snipping Tool installed records the screen through its address
    /// (<see cref="Record"/>): version 11.2307 or later. <paramref name="version"/> is the version
    /// installed, null for none.
    /// </summary>
    internal static bool Records(Version? version) => version is { } installed && installed >= RecordsSince;

    /// <summary>
    /// The version of a package family installed for this person, or null where none is: the dock's
    /// is <see cref="Version(string)"/>; a test puts made-up answers in its place.
    /// </summary>
    internal static Func<string, Version?> VersionOf = Version;

    /// <summary>Whether a package of <paramref name="family"/> is installed, by <see cref="VersionOf"/>.</summary>
    internal static bool InstalledNow(string family) => VersionOf(family) is not null;

    // What the per-user repository held when it was last read, and when: the bar asks for each of
    // its shortcut buttons every time it is drawn, and an app installed meanwhile shows within this.
    private const long PackagesFreshMs = 5000;
    private static readonly Lock PackagesGate = new();
    private static string[] packagesRead = [];
    private static long packagesAt = long.MinValue / 2;

    /// <summary>
    /// The version of <paramref name="family"/> installed for this person, from the full names of the
    /// packages in Windows' per-user repository (read at most every few seconds); null where none is.
    /// </summary>
    public static Version? Version(string family)
    {
        string[] packages;
        lock (PackagesGate)
        {
            if (Environment.TickCount64 - packagesAt > PackagesFreshMs)
            {
                packagesRead = Packages();
                packagesAt = Environment.TickCount64;
            }
            packages = packagesRead;
        }
        return Version(family, packages);
    }

    /// <summary>
    /// The highest version of <paramref name="family"/> among <paramref name="packages"/>, the full
    /// names of the packages installed, or null where none is of that family. A resource package
    /// ("…_neutral_split.scale-400_…") is the app's own and counts as installed with it.
    /// </summary>
    internal static Version? Version(string family, IEnumerable<string> packages)
    {
        Version? best = null;
        foreach (var package in packages)
        {
            if (!Installed(family, [package]))
            {
                continue;
            }
            var version = System.Version.TryParse(package.Split('_')[1], out var parsed) ? parsed : new Version(0, 0);
            if (best is null || version > best)
            {
                best = version;
            }
        }
        return best;
    }

    /// <summary>The full names of the packages in Windows' per-user repository; none where it cannot be read.</summary>
    private static string[] Packages()
    {
        try
        {
            using var packages = Registry.CurrentUser.OpenSubKey(PinTargets.PackagesKey);
            return packages?.GetSubKeyNames() ?? [];
        }
        catch (Exception error) when (error is System.Security.SecurityException or IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>
    /// Opens what Windows' button of <paramref name="kind"/> opens, through <paramref name="open"/>;
    /// false where the kind is not one of these buttons and nothing was asked.
    /// </summary>
    public static bool Open(BarKind kind, Func<string, bool> installed, Action<ShellTarget> open)
    {
        if (Target(kind, installed) is not { } target)
        {
            return false;
        }
        open(target);
        return true;
    }

    /// <summary>
    /// Whether a package of <paramref name="family"/> is installed for this person, by the packages
    /// Windows keeps in its per-user repository (<see cref="Installed(string, IEnumerable{string})"/>).
    /// A registry that cannot be read says no.
    /// </summary>
    public static bool Installed(string family)
    {
        try
        {
            using var packages = Registry.CurrentUser.OpenSubKey(
                @"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages"); // not-ui: a registry key
            return packages is not null && Installed(family, packages.GetSubKeyNames());
        }
        catch (Exception error) when (error is System.Security.SecurityException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// Whether <paramref name="packages"/>, the full names of the packages installed, hold one of
    /// <paramref name="family"/>: a full name is "&lt;name&gt;_&lt;version&gt;_&lt;architecture&gt;_&lt;resource&gt;_&lt;publisher&gt;",
    /// and a family is the name and the publisher — "Microsoft.ScreenSketch_8wekyb3d8bbwe" for
    /// "Microsoft.ScreenSketch_11.2307.0.0_x64__8wekyb3d8bbwe".
    /// </summary>
    internal static bool Installed(string family, IEnumerable<string> packages)
    {
        var cut = family.LastIndexOf('_');
        if (cut <= 0 || cut == family.Length - 1)
        {
            return false;
        }
        var name = family[..cut];
        var publisher = family[(cut + 1)..];
        foreach (var package in packages)
        {
            var parts = package.Split('_');
            if (parts.Length == 5
                && string.Equals(parts[0], name, StringComparison.OrdinalIgnoreCase)
                && string.Equals(parts[4], publisher, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    // The touch keyboard's switch (ITipInvocation, TabTip's own interface since Windows 8): the
    // class that answers without starting the keyboard's host, and the interface.
    private static readonly Guid UIHostNoLaunch = new("4CE576FA-83DC-4F88-951C-9D0782B4E376");
    private static readonly Guid TipInvocation = new("37C994E7-432B-4834-A2F7-DCE1F13B834B");

    /// <summary>
    /// Shows the touch keyboard, or hides it where it shows, as the taskbar's button does; where its
    /// host is not running yet, starts it, which opens the keyboard. False where neither could be done.
    /// </summary>
    public static bool ToggleTouchKeyboard()
    {
        void* tip = null;
        var type = UIHostNoLaunch;
        var face = TipInvocation;
        if (CoCreateInstance(&type, null, (uint)CLSCTX.CLSCTX_LOCAL_SERVER, &face, &tip).SUCCEEDED && tip != null)
        {
            try
            {
                // IUnknown's three, then Toggle(HWND).
                var toggle = (delegate* unmanaged<void*, nint, int>)(*(void***)tip)[3];
                if (toggle(tip, (nint)GetDesktopWindow()) >= 0)
                {
                    return true;
                }
            }
            finally
            {
                ((IUnknown*)tip)->Release();
            }
        }
        var host = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles), @"microsoft shared\ink\TabTip.exe");
        if (!File.Exists(host))
        {
            return false;
        }
        // One of Windows' input panels (§466): no launch (Launch.IsWindowsPanel).
        _ = Launch.Open(host);
        return true;
    }
}
