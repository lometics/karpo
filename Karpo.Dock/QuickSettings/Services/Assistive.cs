// The accessibility page's magnifier, narrator and sticky keys (2026-10-14, docs\widgets.md §170),
// switched as Windows' own accessibility tile's page switches them:
//
//   magnifier    on while its window (MagUIClass) stands; started by its program, Magnify.exe, and
//                stopped by Win+Esc, the shortcut Windows gives it — a window of the magnifier's
//                (which runs with uiAccess) takes no WM_CLOSE from a program that does not
//   narrator     on while Windows says a screen reader runs (SPI_GETSCREENREADER, which the narrator
//                sets); Win+Ctrl+Enter starts and stops it, as its tile's switch does
//   sticky keys  SPI_GETSTICKYKEYS' SKF_STICKYKEYSON, switched with SPI_SETSTICKYKEYS and told to
//                every window, as Settings' own switch does
//
// Each is read where it is asked, cheaply (a window found, a setting read), so what is drawn is
// what stands now; the narrator's and sticky keys' changes come as WM_SETTINGCHANGE, which the dock
// hands on (Dock.SettingChanged), and the magnifier is read again a moment after a click. What
// reaches Windows is behind IAssistiveTools: a test's records what it was asked and changes nothing.
//
// The magnifier started or stopped elsewhere — Win+Plus, Win+Esc, Windows' own tile — says nothing
// to anybody, and the widget stayed as it was (docs\widgets.md §439). Its window does: the shell hook
// the dock has for the taskbar's buttons tells of MagUIClass coming and going, seen on this PC
// (2026-10-20, a probe that started the magnifier and pressed Win+Esc: HSHELL_WINDOWCREATED and
// HSHELL_WINDOWDESTROYED, the class still readable in both). The dock hands that on as
// WM_KARPO_MAGNIFIER, and the switches read it a moment later, when the window going is gone.

using Karpo.Core;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock.QuickSettings.Services;

/// <summary>What the three switches ask of Windows (see the top of this file).</summary>
internal interface IAssistiveTools
{
    /// <summary>Whether the magnifier runs.</summary>
    bool Magnifying { get; }

    /// <summary>Starts the magnifier, or stops it.</summary>
    void Magnify(bool on);

    /// <summary>Whether a screen reader runs (the narrator), or null where Windows would not say.</summary>
    bool? Reading { get; }

    /// <summary>Starts the narrator where it is off, stops it where it is on: Win+Ctrl+Enter.</summary>
    void ToggleNarrator();

    /// <summary>Whether sticky keys are on, or null where Windows would not say.</summary>
    bool? Sticky { get; }

    /// <summary>Turns sticky keys <paramref name="on"/>; false where Windows refused.</summary>
    bool Stick(bool on);
}

/// <summary>Windows' own (see the top of this file).</summary>
internal sealed unsafe class WindowsAssistive : IAssistiveTools
{
    public static readonly WindowsAssistive Shared = new();

    private const uint SkfStickyKeysOn = 0x00000001;

    public bool Magnifying
    {
        get
        {
            fixed (char* name = "MagUIClass")
            {
                return FindWindowW(name, null) != HWND.NULL;
            }
        }
    }

    /// <summary>Whether <paramref name="window"/> is the magnifier's: what the dock's shell hook hands on (see the top of this file).</summary>
    public static bool IsMagnifier(HWND window)
    {
        if (window == HWND.NULL)
        {
            return false;
        }
        var name = stackalloc char[16];
        var length = GetClassNameW(window, name, 16);
        return length > 0 && new ReadOnlySpan<char>(name, length).SequenceEqual("MagUIClass");
    }

    public void Magnify(bool on)
    {
        if (on)
        {
            _ = Launch.Open("magnify.exe");
            return;
        }
        QuickSwitches.Chord([VK.VK_LWIN, VK.VK_ESCAPE]);
    }

    public bool? Reading
    {
        get
        {
            BOOL on = false;
            return SystemParametersInfoW(SPI.SPI_GETSCREENREADER, 0, &on, 0) ? on != 0 : null;
        }
    }

    public void ToggleNarrator() => QuickSwitches.Chord([VK.VK_LWIN, VK.VK_CONTROL, VK.VK_RETURN]);

    public bool? Sticky
    {
        get
        {
            var keys = new STICKYKEYS { cbSize = (uint)sizeof(STICKYKEYS) };
            return SystemParametersInfoW(SPI.SPI_GETSTICKYKEYS, keys.cbSize, &keys, 0) ? (keys.dwFlags & SkfStickyKeysOn) != 0 : null;
        }
    }

    public bool Stick(bool on)
    {
        var keys = new STICKYKEYS { cbSize = (uint)sizeof(STICKYKEYS) };
        if (!SystemParametersInfoW(SPI.SPI_GETSTICKYKEYS, keys.cbSize, &keys, 0))
        {
            return false;
        }
        keys.dwFlags = on ? keys.dwFlags | SkfStickyKeysOn : keys.dwFlags & ~SkfStickyKeysOn;
        return SystemParametersInfoW(SPI.SPI_SETSTICKYKEYS, keys.cbSize, &keys, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
    }
}

/// <summary>The magnifier, the narrator or sticky keys as a switch (see the top of this file).</summary>
internal sealed class AssistiveSwitch : IQuickSwitch
{
    /// <summary>
    /// The magnifier's window came or went (the dock's shell hook, WindowsAssistive.IsMagnifier).
    /// Never posted: the dock hands it to the islands (Dock.ToIslands), which hand it to their switches.
    /// </summary>
    internal const uint WM_KARPO_MAGNIFIER = WM.WM_APP + 0x2E;

    /// <summary>How long after the magnifier's window came or went it is read again, in ms: going, it still stands as the news comes.</summary>
    internal const int MagnifierAgainMs = 300;

    private readonly QuickSwitch which;
    private readonly IAssistiveTools tools;
    private Action? changed;
    private Timer? again;
    private bool disposed;

    /// <param name="which"><see cref="QuickSwitch.Magnifier"/>, <see cref="QuickSwitch.Narrator"/> or <see cref="QuickSwitch.StickyKeys"/>.</param>
    public AssistiveSwitch(QuickSwitch which, IAssistiveTools tools)
    {
        if (which is not (QuickSwitch.Magnifier or QuickSwitch.Narrator or QuickSwitch.StickyKeys))
        {
            throw new ArgumentOutOfRangeException(nameof(which), which, "is not one of the accessibility page's three.");
        }
        this.which = which;
        this.tools = tools;
    }

    public string Name => WidgetInfos.For(which).Title;

    public bool Available => true;

    public bool? On => which switch
    {
        QuickSwitch.Magnifier => tools.Magnifying,
        QuickSwitch.Narrator => tools.Reading,
        _ => tools.Sticky,
    };

    public string? Note => which switch
    {
        QuickSwitch.Magnifier => S.DockQuickMagnifierNote,
        QuickSwitch.Narrator => S.DockQuickNarratorNote,
        _ => S.DockQuickStickyKeysNote,
    };

    /// <summary>Each switches what it stands for.</summary>
    public bool Switches => true;

    public void Start(Action onChanged) => changed = onChanged;

    public void Act()
    {
        switch (which)
        {
            case QuickSwitch.Magnifier:
                tools.Magnify(!tools.Magnifying);
                // No message says the magnifier came or went: it is read again once it has.
                Again(700);
                break;
            case QuickSwitch.Narrator:
                tools.ToggleNarrator();
                Again(1500);
                break;
            default:
                _ = tools.Stick(tools.Sticky != true);
                changed?.Invoke();
                break;
        }
    }

    /// <summary>
    /// The narrator and sticky keys say they changed with WM_SETTINGCHANGE (the dock hands it on); the
    /// magnifier's window coming or going is the dock's <see cref="WM_KARPO_MAGNIFIER"/>, read a moment later.
    /// </summary>
    public void Heard(uint msg, nuint wParam)
    {
        if (msg == WM.WM_SETTINGCHANGE && wParam is SPI.SPI_SETSCREENREADER or SPI.SPI_SETSTICKYKEYS)
        {
            changed?.Invoke();
        }
        else if (msg == WM_KARPO_MAGNIFIER && which == QuickSwitch.Magnifier)
        {
            Again(MagnifierAgainMs);
        }
    }

    /// <summary>What it stands for read again in <paramref name="ms"/> ms, once: the widget draws what stands then.</summary>
    private void Again(int ms)
    {
        if (disposed)
        {
            return;
        }
        again ??= new Timer(static state => ((AssistiveSwitch)state!).Read(), this, Timeout.Infinite, Timeout.Infinite);
        again.Change(ms, Timeout.Infinite);
    }

    /// <summary>On a thread of the pool, which lets nothing out.</summary>
    private void Read()
    {
        try
        {
            changed?.Invoke();
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Console.Error.WriteLine($"Karpo: {which} not read again: {error.Message}");
        }
    }

    public void Dispose()
    {
        disposed = true;
        again?.Dispose();
        again = null;
        changed = null;
    }
}
