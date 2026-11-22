// The keyboard layouts for the keyboard's line (KeyboardPage): the layouts installed, in the order
// Windows lists them, which one the window the person types in uses, and switching that window to
// another, as Windows' input switcher does.
//
// A layout belongs to a thread (KeyboardIsland), so "the one in use" is the one of the window the
// folder opened over: the window in front as the page starts — the folder has not taken the keys
// yet then — or, where that is one of Karpo's own, the last window in front that was not
// (KeyboardIsland.Front). Switching posts WM_INPUTLANGCHANGEREQUEST to that window, the request
// Windows' own switcher makes, and gives it the keys back, which closes the folder.
//
// Each read is GetKeyboardLayoutList and a name or two from the locale and the registry, so it is
// made on the dock's thread as the folder opens. A test never reaches it: the page is handed a
// SampleKeyboard, which switches nothing.

using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock.QuickSettings.Services;

/// <summary>One layout installed.</summary>
/// <param name="Layout">Its handle (an HKL), what a switch asks for.</param>
/// <param name="Language">The language's name in the person's language: "English (United States)".</param>
/// <param name="Detail">The codes the indicator shows and the layout's name: "ENG US · US keyboard".</param>
internal sealed record KeyboardLayoutRow(nint Layout, string Language, string Detail);

/// <param name="Layouts">The layouts installed, in Windows' order.</param>
/// <param name="Current">The one the window the person types in uses; 0 where it could not be read.</param>
internal sealed record KeyboardState(IReadOnlyList<KeyboardLayoutRow> Layouts, nint Current);

internal interface IKeyboardSource : IDisposable
{
    KeyboardState Read();

    /// <summary>Switches the window the person types in to <paramref name="layout"/> and gives it the keys back.</summary>
    void Choose(nint layout);
}

/// <summary>This PC's layouts, and the window the folder opened over (see the top).</summary>
internal sealed unsafe class WindowsKeyboard : IKeyboardSource
{
    private readonly HWND target;

    public WindowsKeyboard()
    {
        var front = GetForegroundWindow();
        uint process = 0;
        _ = front == HWND.NULL ? 0 : GetWindowThreadProcessId(front, &process);
        target = front != HWND.NULL && process != GetCurrentProcessId() ? front : KeyboardIsland.Front;
    }

    public KeyboardState Read()
    {
        var list = new HKL[Math.Max(0, GetKeyboardLayoutList(0, null))];
        int count;
        fixed (HKL* values = list)
        {
            count = GetKeyboardLayoutList(list.Length, values);
        }
        Array.Resize(ref list, Math.Max(0, count));
        var rows = new List<KeyboardLayoutRow>(list.Length);
        foreach (var layout in list)
        {
            var (language, name) = KeyboardIsland.Names(layout);
            var (code, region) = KeyboardIsland.Label(layout);
            rows.Add(new KeyboardLayoutRow((nint)layout, language, KeyboardLayouts.Detail(code, region, name)));
        }
        return new KeyboardState(rows, IsWindow(target) ? (nint)KeyboardIsland.LayoutOf(target) : 0);
    }

    public void Choose(nint layout)
    {
        if (!IsWindow(target))
        {
            return;
        }
        _ = PostMessageW(target, WM.WM_INPUTLANGCHANGEREQUEST, default, (LPARAM)layout);
        _ = SetForegroundWindow(target);
    }

    public void Dispose()
    {
    }
}

/// <summary>Made-up layouts for pictures and tests: a pick is written down and switches nothing.</summary>
internal sealed class SampleKeyboard(KeyboardState? state = null) : IKeyboardSource
{
    /// <summary>English (United States), German (Switzerland) and French (France), the first in use.</summary>
    internal static KeyboardState Three => new(
    [
        new(0x04090409, "English (United States)", KeyboardLayouts.Detail("ENG", "US", "US")), // not-ui: a sample layout
        new(0x08070807, "German (Switzerland)", KeyboardLayouts.Detail("DEU", "CH", "Swiss German")), // not-ui: a sample layout
        new(0x040C040C, "French (France)", KeyboardLayouts.Detail("FRA", "FR", "French")), // not-ui: a sample layout
    ], 0x04090409);

    private KeyboardState shown = state ?? Three;

    /// <summary>What was chosen, in order.</summary>
    internal List<nint> Chosen { get; } = [];

    public KeyboardState Read() => shown;

    public void Choose(nint layout)
    {
        Chosen.Add(layout);
        shown = shown with { Current = layout };
    }

    public void Dispose()
    {
    }
}

/// <summary>What a layout's row says. Pure: the tests check it.</summary>
internal static class KeyboardLayouts
{
    /// <summary>"ENG US · Swiss German keyboard": the indicator's codes, then the layout's name where it has one.</summary>
    internal static string Detail(string language, string region, string layout)
    {
        var codes = $"{language} {region}".Trim();
        return layout.Length > 0 ? $"{codes} · {S.WidgetKeyboardLayoutKeyboard(layout)}" : codes;
    }
}
