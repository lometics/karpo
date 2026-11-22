// A window's own menu, which Shift+right click opens on its button as Windows 11's taskbar does, and
// the menu of a button of several windows (WindowMenus). What they hold and what choosing a line does
// are decisions, tested as such: no menu is read from a real window and no command is posted.

using Karpo.Dock;

namespace Karpo.Tests.Pinning;

public sealed class WindowMenuTests
{
    private const uint Restore = WindowMenus.RestoreCommand;
    private const uint Move = WindowMenus.MoveCommand;
    private const uint Size = WindowMenus.SizeCommand;
    private const uint Minimize = WindowMenus.MinimizeCommand;
    private const uint Maximize = WindowMenus.MaximizeCommand;
    private const uint Close = WindowMenus.CloseCommand;

    private const uint RestoreAll = 2901;
    private const uint MinimizeAll = 2902;
    private const uint CloseAll = 4;

    /// <summary>A window's menu as Windows keeps it, before anything greyed its lines.</summary>
    private static List<MenuLine> Standard() =>
    [
        new(Restore, "&Restore"),
        new(Move, "&Move"),
        new(Size, "&Size"),
        new(Minimize, "Mi&nimize"),
        new(Maximize, "Ma&ximize"),
        new(0, "", Separator: true),
        new(Close, "&Close\tAlt+F4"),
    ];

    private static Dictionary<string, bool> Enabled(IEnumerable<FlyoutItem> rows) =>
        rows.Where(row => !row.Separator).ToDictionary(row => row.Text, row => row.Enabled);

    /// <summary>A window as it usually is: everything but Restore.</summary>
    [Fact]
    public void AWindowAsItIsCanDoEverythingButRestore()
    {
        var rows = WindowMenus.Rows(Standard(), new WindowShape());

        Assert.Equal(["Restore", "Move", "Size", "Minimize", "Maximize", "", "Close"], rows.Select(row => row.Text));
        Assert.Equal(
            new Dictionary<string, bool> { ["Restore"] = false, ["Move"] = true, ["Size"] = true, ["Minimize"] = true, ["Maximize"] = true, ["Close"] = true },
            Enabled(rows));
        Assert.Equal("Alt+F4", rows[^1].Accelerator);
    }

    /// <summary>Maximized: it can be restored and minimized, and neither moved, sized nor maximized again.</summary>
    [Fact]
    public void AMaximizedWindowCanBeRestoredButNotMovedOrSized() =>
        Assert.Equal(
            new Dictionary<string, bool> { ["Restore"] = true, ["Move"] = false, ["Size"] = false, ["Minimize"] = true, ["Maximize"] = false, ["Close"] = true },
            Enabled(WindowMenus.Rows(Standard(), new WindowShape(Maximized: true))));

    /// <summary>Minimized: it can be restored, maximized and moved, and not sized or minimized again — as DefWindowProc greys it.</summary>
    [Fact]
    public void AMinimizedWindowCanBeRestoredOrMaximized() =>
        Assert.Equal(
            new Dictionary<string, bool> { ["Restore"] = true, ["Move"] = true, ["Size"] = false, ["Minimize"] = false, ["Maximize"] = true, ["Close"] = true },
            Enabled(WindowMenus.Rows(Standard(), new WindowShape(Minimized: true))));

    /// <summary>A dialog without minimize or maximize buttons and a fixed frame: moved and closed, nothing else.</summary>
    [Fact]
    public void AFixedDialogCanOnlyBeMovedAndClosed() =>
        Assert.Equal(
            new Dictionary<string, bool> { ["Restore"] = false, ["Move"] = true, ["Size"] = false, ["Minimize"] = false, ["Maximize"] = false, ["Close"] = true },
            Enabled(WindowMenus.Rows(Standard(), new WindowShape(CanMinimize: false, CanMaximize: false, Sizable: false))));

    /// <summary>Close keeps what the menu says — an app that greyed it meant it —, and a class without closing greys it whatever it says.</summary>
    [Fact]
    public void CloseKeepsWhatTheMenuSaysUnlessTheClassForbidsIt()
    {
        List<MenuLine> greyed = [new(Close, "&Close", Enabled: false)];
        List<MenuLine> open = [new(Close, "&Close")];

        Assert.False(WindowMenus.Rows(greyed, new WindowShape()).Single().Enabled);
        Assert.True(WindowMenus.Rows(open, new WindowShape()).Single().Enabled);
        Assert.False(WindowMenus.Rows(open, new WindowShape(CanClose: false)).Single().Enabled);
    }

    /// <summary>
    /// Lines the app added are kept where it put them, with the state it gave them; a submenu, a line
    /// without text and one without a command are left out, and no separator stands at either end or
    /// beside another.
    /// </summary>
    [Fact]
    public void TheAppsOwnLinesAreKeptAndTheRestTidied()
    {
        List<MenuLine> lines =
        [
            new(0, "", Separator: true),
            new(Restore, "&Restore"),
            new(Move, "&Move"),
            new(0, "", Separator: true),
            new(0, "", Separator: true),
            new(0x10, "&Always on top"),
            new(0x20, "Pro&perties", Enabled: false),
            new(0x30, "&Edit", Submenu: true),
            new(0x40, ""),
            new(0, "Nothing to do"),
            new(0, "", Separator: true),
            new(Close, "&Close\tAlt+F4"),
            new(0, "", Separator: true),
        ];

        var rows = WindowMenus.Rows(lines, new WindowShape(Maximized: true));

        Assert.Equal(["Restore", "Move", "", "Always on top", "Properties", "", "Close"], rows.Select(row => row.Text));
        Assert.True(rows.Single(row => row.Text == "Always on top").Enabled);
        Assert.False(rows.Single(row => row.Text == "Properties").Enabled);
        Assert.Equal(0x10u, rows.Single(row => row.Text == "Always on top").Id);
        // The standard ones keep Windows' commands, which is what is posted when one is chosen.
        Assert.Equal(Close, rows[^1].Id);
    }

    /// <summary>The four lines Windows draws a picture for get one; Move, Size and the app's own lines none.</summary>
    [Fact]
    public void RestoreMinimizeMaximizeAndCloseHaveTheirGlyphs()
    {
        var rows = WindowMenus.Rows([.. Standard(), new MenuLine(0x10, "&Always on top")], new WindowShape());

        foreach (var text in new[] { "Restore", "Minimize", "Maximize", "Close" })
        {
            Assert.IsType<FlyoutIcon.Glyph>(rows.Single(row => row.Text == text).Icon);
        }
        foreach (var text in new[] { "Move", "Size", "Always on top" })
        {
            Assert.Null(rows.Single(row => row.Text == text).Icon);
        }
    }

    /// <summary>A line's &amp; marks go, a doubled one stays as one, and its shortcut after a tab moves to the right.</summary>
    [Theory]
    [InlineData("&Close\tAlt+F4", "Close", "Alt+F4")]
    [InlineData("Mi&nimize", "Minimize", null)]
    [InlineData("Save && quit", "Save & quit", null)]
    [InlineData("&Move\t", "Move", null)]
    [InlineData("  Size  ", "Size", null)]
    public void ALinesTextIsReadAsTheMenuShowsIt(string text, string shown, string? keys) =>
        Assert.Equal((shown, keys), WindowMenus.Split(text));

    /// <summary>
    /// The lower bits of a system command are Windows' own, so a command is known by its upper ones:
    /// SC_MOVE with the mouse's bits is still Move.
    /// </summary>
    [Fact]
    public void ACommandIsKnownByItsUpperBits()
    {
        Assert.False(WindowMenus.Enabled(Move | 0x2, new WindowShape(Maximized: true)));
        Assert.Null(WindowMenus.Enabled(0x10, new WindowShape()));
    }

    // --- A button of several windows ---------------------------------------------------------------

    /// <summary>
    /// Several windows: restore them all where any is minimized, minimize them all where any is not,
    /// close them all — that last row nearest the pointer, after a separator, as the jump list's is.
    /// </summary>
    [Fact]
    public void SeveralWindowsCanAllBeRestoredMinimizedOrClosed()
    {
        var rows = WindowMenus.GroupRows([new WindowShape(Minimized: true), new WindowShape()], Edge.Bottom, RestoreAll, MinimizeAll, CloseAll);

        Assert.Equal(["Restore all windows", "Minimize all windows", "", "Close all windows"], rows.Select(row => row.Text));
        Assert.Equal([RestoreAll, MinimizeAll, 0u, CloseAll], rows.Select(row => row.Id));
        Assert.All(rows, row => Assert.True(row.Enabled));
    }

    [Fact]
    public void NoneMinimizedHasNothingToRestore()
    {
        var rows = WindowMenus.GroupRows([new WindowShape(), new WindowShape(Maximized: true)], Edge.Bottom, RestoreAll, MinimizeAll, CloseAll);

        Assert.False(rows.Single(row => row.Id == RestoreAll).Enabled);
        Assert.True(rows.Single(row => row.Id == MinimizeAll).Enabled);
    }

    /// <summary>All minimized, or none that can be, leaves nothing to minimize.</summary>
    [Fact]
    public void NothingLeftToMinimizeGreysMinimizeAll()
    {
        var minimized = WindowMenus.GroupRows([new WindowShape(Minimized: true), new WindowShape(Minimized: true)], Edge.Bottom, RestoreAll, MinimizeAll, CloseAll);
        var fixedOnes = WindowMenus.GroupRows([new WindowShape(CanMinimize: false), new WindowShape(CanMinimize: false)], Edge.Bottom, RestoreAll, MinimizeAll, CloseAll);

        Assert.False(minimized.Single(row => row.Id == MinimizeAll).Enabled);
        Assert.False(fixedOnes.Single(row => row.Id == MinimizeAll).Enabled);
    }

    /// <summary>On a top bar the menu grows downward, so closing them all is the first row.</summary>
    [Fact]
    public void OnATopBarClosingThemAllComesFirst()
    {
        var rows = WindowMenus.GroupRows([new WindowShape(), new WindowShape()], Edge.Top, RestoreAll, MinimizeAll, CloseAll);

        Assert.Equal(["Close all windows", "", "Restore all windows", "Minimize all windows"], rows.Select(row => row.Text));
    }

    // --- Choosing a line ---------------------------------------------------------------------------

    /// <summary>
    /// Minimize and Close want nothing in front and are only posted; Restore on a minimized window is
    /// the window brought back as it was, with nothing posted to restore it further — a window that
    /// was maximized comes back maximized —; the rest have the window in front first.
    /// </summary>
    [Theory]
    [InlineData(WindowMenus.MinimizeCommand, false, false, true)]
    [InlineData(WindowMenus.CloseCommand, false, false, true)]
    [InlineData(WindowMenus.CloseCommand, true, false, true)]
    [InlineData(WindowMenus.RestoreCommand, true, true, false)]
    [InlineData(WindowMenus.RestoreCommand, false, true, true)]
    [InlineData(WindowMenus.MaximizeCommand, true, true, true)]
    [InlineData(WindowMenus.MoveCommand, false, true, true)]
    [InlineData(WindowMenus.SizeCommand, false, true, true)]
    [InlineData(0x10u, false, true, true)]
    public void ChoosingALineBringsTheWindowWhereTheCommandWantsIt(uint command, bool minimized, bool activate, bool post) =>
        Assert.Equal(new WindowSteps(activate, post), WindowMenus.Steps(command, new WindowShape(Minimized: minimized, Maximized: !minimized && command == WindowMenus.RestoreCommand)));
}
