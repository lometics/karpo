// Where an open folder stands along its bar (the person, 2026-10-29: "you should be able to set the
// alignment of a folder. e.g. left right center and auto, in auto it means that the folder alignes to
// the face that opens the folder"; §385). Held here:
//
//  - **The rule** (pure geometry: the work area, the bar's edge, the face, the folder's size and its
//    alignment → where the folder stands): Automatic centred on its face, as it always stood and as
//    Windows 11's jump lists stand on their button, kept inside the screen; Left and Right against the
//    work area's ends, the one margin every popup keeps there (12 DIP, §391), where
//    Automatic pushed against that end stands too; Center in the middle of the work area. Top and bottom
//    bars, a display beside another, a face at either end, a folder wider than the screen, a side bar (start, middle and end down it).
//  - **The file**: "align" in the folder's options, written only where it is not Automatic; a file
//    without it, or with a word this build does not know, is Automatic — nothing moves.
//  - **The menu**: the folder's own row, Alignment, four buttons in the row (§253), Automatic in force.
//  - **The open folder** (KARPO_UI_TESTS=1): each alignment where the rule puts it; one changed while
//    it stands open moves it there; grown, it stays against its end or round its middle. Pictures
//    folder-align-<bottom|top>.png in KARPO_PNG_DIR: Automatic, Left, Center and Right, a red mark at
//    the face.

using System.Numerics;
using System.Text.Json;
using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using Color = Windows.UI.Color;

namespace Karpo.Tests.Folders;

public sealed class FolderAlignTests
{
    private const string Owner = "layout:default";
    private const float Scale = 1.5f;
    private static readonly Look Dark = new(Glass: false, Dark: true, Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4));

    /// <summary>A 1920 × 1080 display at 150 %, its bar 48 DIP (72 px) along the bottom.</summary>
    private static readonly RECT Display = new() { right = 1920, bottom = 1080 };

    private const int Bar = 72;

    /// <summary>12 DIP at 150 %: what a popup keeps from the work area's start and from its end (one margin, §391); the gap off the bar.</summary>
    private const int Start = 18, End = 18, Gap = 18;

    /// <summary>The work area of a bar along <paramref name="edge"/> of <paramref name="display"/>.</summary>
    private static RECT WorkOf(RECT display, Edge edge) => edge switch
    {
        Edge.Top => display with { top = display.top + Bar },
        Edge.Left => display with { left = display.left + Bar },
        Edge.Right => display with { right = display.right - Bar },
        _ => display with { bottom = display.bottom - Bar },
    };

    /// <summary>The middle of the inner side of a face <paramref name="from"/> to <paramref name="to"/> along the bar: what the dock hangs a folder off (DockWindow.WidgetAnchor).</summary>
    private static POINT FaceAnchor(RECT work, Edge edge, int from, int to) => edge switch
    {
        Edge.Top => new POINT { x = (from + to) / 2, y = work.top },
        Edge.Left => new POINT { x = work.left, y = (from + to) / 2 },
        Edge.Right => new POINT { x = work.right, y = (from + to) / 2 },
        _ => new POINT { x = (from + to) / 2, y = work.bottom },
    };

    /// <summary>Where a <paramref name="width"/> × <paramref name="height"/> folder stands for the face <paramref name="from"/>–<paramref name="to"/>.</summary>
    private static RECT Placed(RECT work, Edge edge, int from, int to, FolderAlign align, int width = 540, int height = 400) =>
        FolderFlyout.PlacementOf(FaceAnchor(work, edge, from, to), work, edge, align).PanelAt(width, height, Scale);

    [Fact]
    public void ANewFolderAndAFileWithoutOneAreAutomaticWhichIsWhereAFolderAlwaysStood()
    {
        Assert.Equal(FolderAlign.Auto, new FolderOptions().Align);
        Assert.Equal(FolderAlign.Auto, BarKind.FolderList.New().Folder!.Align);
        // Automatic is the placement every folder had: centred on the anchor (Dock.PopupAlign).
        var work = WorkOf(Display, Edge.Bottom);
        Assert.Equal(FlyoutAlign.Center, FolderFlyout.PlacementOf(default, work, Edge.Bottom, FolderAlign.Auto).Align);
        Assert.Equal(new FlyoutPlacement(new POINT { x = 700, y = work.bottom }, work, Edge.Bottom).PanelAt(540, 400, Scale), Placed(work, Edge.Bottom, 664, 736, FolderAlign.Auto));
    }

    [Theory]
    [InlineData(Edge.Bottom)]
    [InlineData(Edge.Top)]
    public void EachAlignmentStandsWhereItSaysAlongATopOrBottomBar(Edge edge)
    {
        var work = WorkOf(Display, edge);
        // Away from the bar by the gap every popup keeps, whatever the alignment.
        foreach (var align in FolderAligns.All)
        {
            var at = Placed(work, edge, 600, 672, align);
            Assert.Equal((540, 400), (at.right - at.left, at.bottom - at.top));
            Assert.Equal(edge == Edge.Top ? work.top + Gap : work.bottom - Gap, edge == Edge.Top ? at.top : at.bottom);
        }
        // Automatic: centred on the face that opened it.
        var auto = Placed(work, edge, 600, 672, FolderAlign.Auto);
        Assert.Equal(636, (auto.left + auto.right) / 2);
        // Left and Right: against the work area's ends, its margins kept; Center: the work area's middle.
        Assert.Equal(work.left + Start, Placed(work, edge, 600, 672, FolderAlign.Left).left);
        Assert.Equal(work.right - End, Placed(work, edge, 600, 672, FolderAlign.Right).right);
        Assert.Equal((1920 - 540) / 2, Placed(work, edge, 600, 672, FolderAlign.Center).left);
        // None of the three follows the face.
        foreach (var align in new[] { FolderAlign.Left, FolderAlign.Center, FolderAlign.Right })
        {
            Assert.Equal(Placed(work, edge, 600, 672, align), Placed(work, edge, 1700, 1772, align));
        }
    }

    [Fact]
    public void AutomaticNextToTheScreensEdgeIsKeptOnTheScreen()
    {
        var work = WorkOf(Display, Edge.Bottom);
        // A face at the left end: the folder against the start's margin, never past the screen.
        Assert.Equal(work.left + Start, Placed(work, Edge.Bottom, 0, 72, FolderAlign.Auto).left);
        // At the right end: against the margin there, the one either end keeps.
        Assert.Equal(work.right - Start, Placed(work, Edge.Bottom, 1848, 1920, FolderAlign.Auto).right);
        // In between it follows the face.
        Assert.Equal(1300, (Placed(work, Edge.Bottom, 1264, 1336, FolderAlign.Auto).left + Placed(work, Edge.Bottom, 1264, 1336, FolderAlign.Auto).right) / 2);
    }

    /// <summary>
    /// The person: "an automatic aligned folder all the way on the right side and a right aligned folder are not at
    /// the same position seen from the right side". A face against an end pushes an Automatic folder exactly where
    /// that end's alignment stands it: one margin at either end, and for the push.
    /// </summary>
    [Theory]
    [InlineData(Edge.Bottom)]
    [InlineData(Edge.Top)]
    [InlineData(Edge.Left)]
    [InlineData(Edge.Right)]
    public void PushedAgainstAnEndAutomaticStandsWhereThatEndsAlignmentPutsIt(Edge edge)
    {
        var work = WorkOf(Display, edge);
        var length = edge is Edge.Left or Edge.Right ? 1080 : 1920;
        Assert.Equal(Sides(Placed(work, edge, length - 72, length, FolderAlign.Right)), Sides(Placed(work, edge, length - 72, length, FolderAlign.Auto)));
        Assert.Equal(Sides(Placed(work, edge, 0, 72, FolderAlign.Left)), Sides(Placed(work, edge, 0, 72, FolderAlign.Auto)));

        static (int Left, int Top, int Right, int Bottom) Sides(RECT r) => (r.left, r.top, r.right, r.bottom);
    }

    [Fact]
    public void OnADisplayBesideAnotherTheAlignmentIsThatDisplays()
    {
        // The right of two, and one left of the main display (its px negative).
        foreach (var left in new[] { 1920, -1920 })
        {
            var display = new RECT { left = left, right = left + 1920, bottom = 1080 };
            var work = WorkOf(display, Edge.Bottom);
            Assert.Equal(left + Start, Placed(work, Edge.Bottom, left + 600, left + 672, FolderAlign.Left).left);
            Assert.Equal(left + ((1920 - 540) / 2), Placed(work, Edge.Bottom, left + 600, left + 672, FolderAlign.Center).left);
            Assert.Equal(left + 1920 - End, Placed(work, Edge.Bottom, left + 600, left + 672, FolderAlign.Right).right);
            Assert.Equal(left + 636, (Placed(work, Edge.Bottom, left + 600, left + 672, FolderAlign.Auto).left + Placed(work, Edge.Bottom, left + 600, left + 672, FolderAlign.Auto).right) / 2);
            Assert.Equal(left + Start, Placed(work, Edge.Bottom, left, left + 72, FolderAlign.Auto).left);
        }
    }

    [Fact]
    public void AFolderWiderThanTheScreenStartsAtItsStartWhateverItsAlignment()
    {
        // As every popup: the far end is what gives (§320: it runs off the screen and says so), the start
        // kept by the one margin (FlyoutPlacement.MarginDip).
        var work = WorkOf(Display, Edge.Bottom);
        foreach (var align in FolderAligns.All)
        {
            Assert.Equal(work.left + Start, Placed(work, Edge.Bottom, 1700, 1772, align, width: 2400).left);
        }
    }

    [Theory]
    [InlineData(Edge.Left)]
    [InlineData(Edge.Right)]
    public void DownASideBarLeftIsItsTopAndRightItsBottom(Edge edge)
    {
        var work = WorkOf(Display, edge);
        Assert.Equal(work.top + Start, Placed(work, edge, 300, 372, FolderAlign.Left).top);
        Assert.Equal(work.bottom - End, Placed(work, edge, 300, 372, FolderAlign.Right).bottom);
        Assert.Equal((1080 - 400) / 2, Placed(work, edge, 300, 372, FolderAlign.Center).top);
        var auto = Placed(work, edge, 300, 372, FolderAlign.Auto);
        Assert.Equal(336, (auto.top + auto.bottom) / 2);
        // Off the bar's inner side by the gap, whatever the alignment.
        foreach (var align in FolderAligns.All)
        {
            var at = Placed(work, edge, 300, 372, align);
            Assert.Equal(edge == Edge.Left ? work.left + Gap : work.right - Gap, edge == Edge.Left ? at.left : at.right);
        }
    }

    [Fact]
    public void TheFileKeepsAnAlignmentOnlyWhereItIsNotAutomatic()
    {
        var folder = BarKind.FolderList.New();
        Assert.DoesNotContain("\"align\"", JsonSerializer.Serialize(folder, ConfigJson.Default.BarItem), StringComparison.Ordinal);
        foreach (var (align, word) in new[] { (FolderAlign.Left, "left"), (FolderAlign.Center, "center"), (FolderAlign.Right, "right") })
        {
            folder.Folder!.Align = align;
            var written = JsonSerializer.Serialize(folder, ConfigJson.Default.BarItem);
            Assert.Contains($"\"align\":\"{word}\"", written.Replace(" ", "", StringComparison.Ordinal), StringComparison.Ordinal);
            var again = JsonSerializer.Deserialize(written, ConfigJson.Default.BarItem)!;
            again.Normalize();
            Assert.Equal(align, again.Folder!.Align);
        }
        // A word this build does not know is Automatic, as a file without one.
        folder.Folder!.Align = FolderAlign.Right;
        var odd = JsonSerializer.Serialize(folder, ConfigJson.Default.BarItem).Replace("\"right\"", "\"sideways\"", StringComparison.Ordinal);
        var read = JsonSerializer.Deserialize(odd, ConfigJson.Default.BarItem)!;
        read.Normalize();
        Assert.Equal(FolderAlign.Auto, read.Folder!.Align);
    }

    /// <summary>
    /// The folder's own row, after its name: Alignment, its four as buttons in the row (§253) —
    /// Automatic, Left, Center, Right, each its icon —, Automatic in force; a pick written on the
    /// folder, Automatic written as nothing. No line and no other widget has it.
    /// </summary>
    [Fact]
    public void TheFoldersMenuSetsItsAlignment()
    {
        var item = BarKind.FolderList.New();
        var folder = item.Folder!;
        var row = OptionRows.Find(OptionRows.Of(item), OptionRows.FolderAlign)!;
        Assert.Equal((OptionRowKind.Choice, OptionSlot.Placement, S.CoreAlignTitle, true, 0), (row.Kind, row.Slot, row.Title, row.Segmented, row.Chosen));
        Assert.Equal([S.DockFolderAlignAuto, S.CoreEdgeLeft, S.CoreAlignCenter, S.CoreEdgeRight], row.Choices);
        Assert.Equal([OptionGlyphs.Automatic, OptionGlyphs.AlignLeft, OptionGlyphs.AlignCenter, OptionGlyphs.AlignRight], row.ChoiceGlyphs);
        Assert.False(row.Set!(0));
        Assert.True(row.Set!(3));
        Assert.Equal(FolderAlign.Right, folder.Align);
        Assert.Equal(3, OptionRows.Find(OptionRows.Of(item), OptionRows.FolderAlign)!.Chosen);
        Assert.True(OptionRows.Find(OptionRows.Of(item), OptionRows.FolderAlign)!.Set!(2));
        Assert.Equal(FolderAlign.Center, folder.Align);
        Assert.True(OptionRows.Find(OptionRows.Of(item), OptionRows.FolderAlign)!.Set!(0));
        Assert.Equal(FolderAlign.Auto, folder.Align);

        // Only a folder's.
        Assert.DoesNotContain(OptionRows.Of(BarKind.Pin.New()), each => each.Key == OptionRows.FolderAlign);
        Assert.DoesNotContain(OptionRows.Of(ListKind.Heading.New()), each => each.Key == OptionRows.FolderAlign);
    }

    /// <summary>
    /// §589: on a bar on a display's left or right edge (OptionPlace.Side), the row reads along it: Automatic, Top, Center,
    /// Bottom, Word's three a quarter turn (Top's lines hang from the top) — the same values (Top is Left, the work area's
    /// start), so a pick writes what Left, Center and Right write. The person, 2026-11-09: "when the taskbar is shown on a side
    /// the alignment options dont read correctly anymore".
    /// </summary>
    [Fact]
    public void OnASideBarItsAlignmentReadsTopCenterBottom()
    {
        var item = BarKind.FolderList.New();
        var side = new OptionPlace(new Config(), Dark: true) { Side = true };
        OptionRow Row() => OptionRows.Find(OptionRows.Of(item, null, side), OptionRows.FolderAlign)!;
        var row = Row();
        Assert.Equal([S.DockFolderAlignAuto, S.CoreEdgeTop, S.CoreAlignCenter, S.CoreEdgeBottom], row.Choices);
        Assert.Equal([OptionGlyphs.Automatic, OptionGlyphs.AlignTop, OptionGlyphs.AlignMiddle, OptionGlyphs.AlignBottom], row.ChoiceGlyphs);
        Assert.Equal(OptionGlyphs.AlignTop, row.Glyph);
        Assert.Equal([OptionGlyphs.Turned(OptionGlyphs.AlignLeft), OptionGlyphs.Turned(OptionGlyphs.AlignCenter), OptionGlyphs.Turned(OptionGlyphs.AlignRight)],
            [OptionGlyphs.AlignTop, OptionGlyphs.AlignMiddle, OptionGlyphs.AlignBottom]);
        Assert.True(OptionGlyphs.IsTurned(OptionGlyphs.AlignBottom, out var upright) && upright == OptionGlyphs.AlignRight);
        Assert.False(OptionGlyphs.IsTurned(OptionGlyphs.AlignRight, out upright));
        Assert.Equal(OptionGlyphs.AlignRight, upright);

        Assert.True(row.Set!(1));
        Assert.Equal(FolderAlign.Left, item.Folder!.Align);
        Assert.True(Row().Set!(3));
        Assert.Equal(FolderAlign.Right, item.Folder.Align);
        // Drawn a quarter turn clockwise about the glyph's middle (Mirror.Quarter), on top of what is in force: the box's
        // left middle goes to its top middle, its middle stays, a translation in force still applies.
        static Vector2 Through(D2D_MATRIX_3X2_F m, Vector2 p) => new((p.X * m._11) + (p.Y * m._21) + m._31, (p.X * m._12) + (p.Y * m._22) + m._32);
        var identity = new D2D_MATRIX_3X2_F(1, 0, 0, 1, 0, 0);
        var turn = Mirror.QuarterOf(identity, 20, 20);
        Assert.Equal(new Vector2(20, 20), Through(turn, new Vector2(20, 20)));
        Assert.Equal(new Vector2(20, 10), Through(turn, new Vector2(10, 20)));
        Assert.Equal(new Vector2(30, 20), Through(turn, new Vector2(20, 10)));
        Assert.Equal(new Vector2(120, 15), Through(Mirror.QuarterOf(new D2D_MATRIX_3X2_F(1, 0, 0, 1, 100, 5), 20, 20), new Vector2(10, 20)));

        // A bottom bar's, as ever.
        Assert.Equal([S.DockFolderAlignAuto, S.CoreEdgeLeft, S.CoreAlignCenter, S.CoreEdgeRight], OptionRows.Find(OptionRows.Of(item, null, side with { Side = false }), OptionRows.FolderAlign)!.Choices);
    }

    // --- The open folder ------------------------------------------------------------------------

    /// <summary>"a b c | d e": columns of airplane-mode lines; a blank part an empty column.</summary>
    private static BarItem Shaped(string shape, FolderAlign align)
    {
        var island = BarKind.FolderList.New();
        island.Id = "box";
        island.Folder!.Align = align;
        island.Folder.Lists.Clear();
        island.Folder.Widths?.Clear();
        foreach (var part in shape.Split('|'))
        {
            island.Folder.Lists.Add([.. part.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(id =>
            {
                var line = ListKind.AirplaneMode.New();
                line.Id = id;
                line.View = FolderView.List;
                return new FolderCell { Island = line };
            })]);
        }
        return island;
    }

    /// <summary>Where the rule puts the open folder's panel, the size it has now.</summary>
    private static RECT Expected(FolderFlyout flyout, RECT work, Edge edge, POINT anchor, FolderAlign align)
    {
        var on = flyout.PanelOnScreen;
        return FolderFlyout.PlacementOf(anchor, work, edge, align).PanelAt(on.right - on.left, on.bottom - on.top, Scale);
    }

    /// <summary>
    /// A real folder of two columns, its face at 1264–1336 px along a bottom and a top bar, each
    /// alignment set on the folder itself (the dock hands it none): it stands where the rule puts it,
    /// at rest and arranged alike. Picture folder-align-&lt;edge&gt;.png: Automatic, Left, Center and Right.
    /// </summary>
    [UiTheory]
    [InlineData(Edge.Bottom)]
    [InlineData(Edge.Top)]
    public void TheOpenFolderStandsWhereItsAlignmentSays(Edge edge)
    {
        WindowShots.OnHiddenDesktop("folder-align", (gfx, instance) =>
        {
            var work = WorkOf(Display, edge);
            var anchor = FaceAnchor(work, edge, 1264, 1336);
            List<(byte[], int, int)> pictures = [];
            foreach (var align in FolderAligns.All)
            {
                using var folder = new FolderIsland($"{Owner}/box", Shaped("a b c | d e", align), gfx, new Config());
                using var flyout = new FolderFlyout(gfx, instance, 0);
                foreach (var editing in new[] { true, false })
                {
                    flyout.Show(gfx, Dark, Scale, anchor, work, edge, folder, editing: editing, display: Display);
                    WindowShots.Settle();
                    Assert.Equal(Expected(flyout, work, edge, anchor, align), flyout.PanelOnScreen);
                    if (!editing)
                    {
                        pictures.Add(Picture(flyout, edge, anchor, $"folder-align-{edge}-{align}.png"));
                    }
                    flyout.Hide();
                }
            }
            SaveGrid(pictures, $"folder-align-{edge.ToString().ToLowerInvariant()}.png");
        });
    }

    /// <summary>
    /// Changed while the folder stands open (its menu, which a folder's change configures it again
    /// from, §334): it moves to where the new alignment puts it. Grown by a column, it stays against
    /// its end, round its middle or on its face — re-placed by the same rule.
    /// </summary>
    [UiFact]
    public void ChangedOrGrownWhileOpenTheFolderIsPlacedAgainByItsAlignment()
    {
        WindowShots.OnHiddenDesktop("folder-align-change", (gfx, instance) =>
        {
            var work = WorkOf(Display, Edge.Bottom);
            var anchor = FaceAnchor(work, Edge.Bottom, 1264, 1336);
            var holder = Shaped("a b", FolderAlign.Auto);
            var config = new Config();
            using var folder = new FolderIsland($"{Owner}/box", holder, gfx, config);
            using var flyout = new FolderFlyout(gfx, instance, 0);
            flyout.Show(gfx, Dark, Scale, anchor, work, Edge.Bottom, folder, display: Display);
            WindowShots.Settle();
            Assert.Equal(Expected(flyout, work, Edge.Bottom, anchor, FolderAlign.Auto), flyout.PanelOnScreen);

            foreach (var align in new[] { FolderAlign.Right, FolderAlign.Left, FolderAlign.Center, FolderAlign.Auto })
            {
                holder.Folder!.Align = align;
                folder.Configure(holder, config);
                WindowShots.Settle();
                Assert.Equal(Expected(flyout, work, Edge.Bottom, anchor, align), flyout.PanelOnScreen);
            }

            foreach (var align in new[] { FolderAlign.Right, FolderAlign.Center })
            {
                holder.Folder!.Align = align;
                folder.Configure(holder, config);
                WindowShots.Settle();
                var before = flyout.PanelOnScreen;
                var line = ListKind.EnergySaver.New();
                line.View = FolderView.List;
                Assert.NotNull(Karpo.Core.Folders.InsertLine(BarKind.FolderList, holder.Folder, line, FolderPlace.NewList(holder.Folder.Lists.Count)));
                folder.Configure(holder, config);
                WindowShots.Settle();
                var after = flyout.PanelOnScreen;
                Assert.True(after.right - after.left > before.right - before.left, $"grown: {before.right - before.left} → {after.right - after.left}");
                Assert.Equal(Expected(flyout, work, Edge.Bottom, anchor, align), after);
                if (align == FolderAlign.Right)
                {
                    Assert.Equal(before.right, after.right);
                }
                else
                {
                    Assert.InRange((after.left + after.right) / 2 - ((before.left + before.right) / 2), -1, 1);
                }
            }
            flyout.Hide();
        });
    }

    /// <summary>The display round the open folder on a dark backdrop: the bar a grey strip, the face on it red.</summary>
    private static (byte[] Canvas, int Width, int Height) Picture(FolderFlyout flyout, Edge edge, POINT anchor, string file)
    {
        var frame = Display;
        var (canvas, _) = WindowShots.Together([(flyout.Hwnd, frame)], frame, file, (0x30, 0x30, 0x30), over: [flyout.Hwnd]);
        var width = frame.right - frame.left;
        var (barTop, barBottom) = edge == Edge.Top ? (0, Bar) : (1080 - Bar, 1080);
        for (var y = barTop; y < barBottom; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var at = ((y * width) + x) * 4;
                var face = x >= anchor.x - 36 && x < anchor.x + 36 && y >= barTop + 8 && y < barBottom - 8;
                (canvas[at], canvas[at + 1], canvas[at + 2]) = face ? ((byte)0x20, (byte)0x20, (byte)0xE0) : ((byte)0x50, (byte)0x50, (byte)0x50);
            }
        }
        return (canvas, width, frame.bottom - frame.top);
    }

    /// <summary>Four pictures two by two, a light gap between them.</summary>
    private static void SaveGrid(List<(byte[] Canvas, int Width, int Height)> pictures, string file)
    {
        const int gap = 16;
        var (w, h) = (pictures[0].Width, pictures[0].Height);
        var width = (2 * w) + gap;
        var height = (2 * h) + gap;
        var canvas = new byte[width * height * 4];
        Array.Fill(canvas, (byte)0xC0);
        for (var index = 0; index < pictures.Count; index++)
        {
            var (x0, y0) = ((index % 2) * (w + gap), (index / 2) * (h + gap));
            for (var y = 0; y < h; y++)
            {
                Buffer.BlockCopy(pictures[index].Canvas, y * w * 4, canvas, (((y0 + y) * width) + x0) * 4, w * 4);
            }
        }
        WindowShots.WritePng(canvas, width, height, Path.Combine(Offscreen.Pictures, file));
    }
}
