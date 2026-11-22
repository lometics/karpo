// The kinds that came in on 2026-09-26 as shells: "go ahead and implement just the shells for these
// even if there is no functionality yet". Each is a registry member in the place it was asked for,
// and a drawing that says which kind it is — the shell's glyph and title
// (Karpo.Dock\Islands\ShellIsland.cs), or the island its own line is being built in — at every size
// it can be given, with a click that does nothing.
//
// Every one of them is built now. The timer's and the focus time's line are
// Karpo.Tests\Calendars\CountdownFormsTests' (their entries became presets, docs\widgets.md §92);
// where they stand is still held here, and so is the shell itself — what the next kind added before
// its drawing is — drawn on the taskbar and in a list.
//
// The drawing half needs Direct2D, so it runs only with KARPO_UI_TESTS=1; the rest is pure.

using Karpo.Dock;
using static Karpo.Tests.Islands.Offscreen;

namespace Karpo.Tests.Islands;

public sealed unsafe class ShellTests
{
    /// <summary>
    /// The kinds the taskbar's shell is drawn for: the timer and the focus time were its last shells,
    /// and their entries are gone (the timer and focus presets), so one that stays stands in.
    /// </summary>
    private static readonly BarKind[] BarShells = [BarKind.NightLight];

    /// <summary>The kinds a list's shell is drawn for: the same two, its last shells too.</summary>
    private static readonly ListKind[] ListShells = [ListKind.Timer, ListKind.Focus];

    private static Look LookFor(bool dark) => new(Glass: false, Dark: dark, Accent: Windows.UI.Color.FromArgb(255, 0, 0x78, 0xD4));

    /// <summary>
    /// Each stands where it was asked for: the timer and focus time after the clock in a list;
    /// removable devices after the network drive.
    /// </summary>
    [Fact]
    public void EachStandsWhereItWasAskedFor()
    {
        var list = ListWidgets.All.Select(widget => widget.Kind).ToList();
        Assert.Equal(list.IndexOf(ListKind.Clock) + 1, list.IndexOf(ListKind.Timer));
        Assert.Equal(list.IndexOf(ListKind.Clock) + 2, list.IndexOf(ListKind.Focus));
        Assert.Equal(list.IndexOf(ListKind.NetworkDrive) + 1, list.IndexOf(ListKind.RemovableDevices));
    }

    /// <summary>
    /// What each declares: the new lines a click that does nothing, one row at the least. What the
    /// timer and the focus time do is CountdownFormsTests'.
    /// </summary>
    [Fact]
    public void EachDeclaresWhatItWasAskedFor()
    {
        foreach (var kind in ListShells)
        {
            var line = ListWidgets.Of(kind);
            Assert.Equal(CellAct.Nothing, line.Act);
            Assert.Equal(ListLine.Card, line.Line);
            Assert.Equal(LineHeight.Fewest, line.Range.Least);
        }
        var removable = ListWidgets.Of(ListKind.RemovableDevices);
        Assert.Equal(WidgetSource.Removable, removable.Drawing);
        Assert.Equal(CellAct.Nothing, removable.Act);
        Assert.Equal(ListLine.Card, removable.Line);
        Assert.Equal(LineHeight.Fewest, removable.Range.Least);
    }

    /// <summary>
    /// A taskbar shell at one cell to four, on a horizontal bar and a vertical one: it draws, its
    /// ink inside its cell, and a click does nothing. No entry is a shell now, so it is made and
    /// stood on an entry as the factory did while the timer's and the focus time's were.
    /// </summary>
    [UiFact]
    public void ATaskbarShellDrawsAtEveryWidth()
    {
        OnStaThread(gfx =>
        {
            var config = new Config { Height = 48 };
            foreach (var kind in BarShells)
            {
                foreach (var vertical in new[] { false, true })
                {
                    for (var cells = 1; cells <= 4; cells++)
                    {
                        var widget = new BarItem { Id = $"{kind}{cells}", Kind = kind };
                        widget.Normalize();
                        using var island = new ShellIsland(Config.IslandKey(null, widget.Id), widget, gfx, config);
                        island.StandOn(BarEntries.Of(kind) with { Drawing = WidgetSource.Shell, Click = BarClick.Nothing });
                        var style = new IslandStyle(1f, vertical, Layout.CellPx(96, config), Layout.IconPx(96, config), LookFor(true)) with { Width = cells };
                        _ = island.Refresh(gfx);
                        Assert.Equal(1, island.Measure(style).Count);
                        var (width, height) = island.CellSize(0, style);
                        var inked = false;
                        Bitmap(width, height, dc => island.PaintCell(dc, 0, style, width, height), bitmap =>
                        {
                            var (left, top, right, bottom) = InkOf(bitmap, (uint)MathF.Ceiling(width), (uint)MathF.Ceiling(height));
                            inked = right > left && bottom > top;
                        });
                        Assert.True(inked, $"{kind} at {cells} cells{(vertical ? " on a vertical bar" : "")} drew nothing");
                        Assert.Equal(default, island.Click(0));
                        Assert.Equal(WidgetInfos.Of(kind.Id()).Title, island.Tooltip(0));
                    }
                }
            }
        });
    }

    /// <summary>
    /// A list's shell draws from one row up, at 100 % and 150 %: something is drawn, and all of it
    /// stands between the list's two gutters (docs\widgets.md §9, "One gutter"), its glyph in the
    /// icon column the gutter starts. No line is a shell now, so it is made and stood in its line
    /// as the factory did while the timer's and the focus time's were.
    /// </summary>
    [UiFact]
    public void AListShellDrawsFromOneRowUp()
    {
        OnStaThread(gfx =>
        {
            var config = new Config();
            foreach (var kind in ListShells)
            {
                var widget = kind.New();
                using var island = new ShellIsland(Config.IslandKey(null, widget.Id), widget, gfx, config);
                island.StandIn(ListWidgets.Of(kind) with { Drawing = WidgetSource.Shell, Interaction = Interaction.Click });
                foreach (var scale in new[] { 1f, 1.5f })
                {
                    var look = LookFor(true);
                    var theme = FlyoutTheme.For(look);
                    var style = FolderFlyout.StyleFor(scale, look, theme);
                    var gutter = ListForm.Gutter(style);
                    var icon = FolderFlyout.RowIcon(style.Text(14));
                    var width = 360 * scale;
                    for (var rows = 1; rows <= 4; rows++)
                    {
                        var height = rows * LineHeight.Row * FolderOptions.CellDip * scale;
                        var ink = (Left: 0f, Top: 0f, Right: 0f, Bottom: 0f);
                        Bitmap(width, height, dc => island.PaintOn(dc, PopupPaint.Rect(0, 0, width, height), style, WidgetSurface.List), bitmap =>
                            ink = InkOf(bitmap, (uint)MathF.Ceiling(width), (uint)MathF.Ceiling(height)));
                        var at = $"{kind} at {rows} rows, {scale * 100:0} %";
                        Assert.True(ink.Right > ink.Left && ink.Bottom > ink.Top, $"{at} drew nothing");
                        Assert.InRange(ink.Left, gutter - 1, gutter + (icon / 2));
                        Assert.True(ink.Right <= width - gutter + 1, $"{at} inks the trailing gutter, to {ink.Right} of {width}");
                    }
                }
                Assert.Equal(default, island.Click(0));
            }
        });
    }
}
