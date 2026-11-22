// A folder's contents (§380; a pin set to show them from §32 to §380; Karpo.Dock\Islands\FolderContentsLine.cs,
// FolderContentsView.cs) as a line of a list at every height, over made-up folders (FakeFolders): what it
// reads and how soon, what a click opens — once its folder has gone — and where the navigation sends a
// subfolder, what a change in the folder does, that nothing it draws leaves its box — and the pictures,
// every height from one row to seven, in each theme at 100 and 150 %, and a list grouped under headings
// (backlog 29), with a line lit under the pointer (backlog 21).
//
// Nothing here reads a real folder or opens anything: the folder source is the fake's, which writes
// down what would have been opened. Needs Direct2D, so it runs only with KARPO_UI_TESTS=1; the
// pictures go to KARPO_PNG_DIR.

using System.Numerics;
using Karpo.Dock;
using Karpo.Dock.Drives;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using Xunit.Abstractions;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Folders;

[Collection(FolderSourceState.Collection)]
public sealed unsafe class FolderContentsLineTests(ITestOutputHelper output)
{
    private const string Key = "layout:default/folder/files";
    private const string Projects = @"C:\Users\Ann\Projects";

    private static readonly float[] Scales = [1f, 1.5f];

    /// <summary>A folder's contents showing <paramref name="path"/>, its navigation <paramref name="navigation"/>.</summary>
    internal static T Showing<T>(T line, string path, PinFolderNavigation navigation = PinFolderNavigation.Explorer)
        where T : WidgetItem
    {
        line.Pin!.Target = path;
        line.Pin.Navigation = navigation;
        return line;
    }

    private static CellItem Widget(string path, PinFolderNavigation navigation = PinFolderNavigation.Explorer) => Showing(ListKind.FolderContents.New(), path, navigation);

    private static FolderContentsLine Line(Gfx gfx, string path, PinFolderNavigation navigation = PinFolderNavigation.Explorer) =>
        (FolderContentsLine)Karpo.Dock.Islands.CreateLine(Key, Widget(path, navigation), ListKind.FolderContents, gfx, new Config())!;

    /// <summary>A pin to <paramref name="path"/>, as a line of a list.</summary>
    private static PinTile PinLine(Gfx gfx, string path)
    {
        var pin = ListKind.Pin.New();
        pin.Pin!.Target = path;
        return (PinTile)Karpo.Dock.Islands.CreateLine(Key, pin, ListKind.Pin, gfx, new Config())!;
    }

    private static D2D_RECT_F LineBox(int rows, float scale, float textScale) =>
        PopupPaint.Rect(0, 0, PopupPaint.Round(Flyout.PanelWidthDip * scale), ListFormPictures.Rows(rows, scale, textScale));

    private static TileStyle LineStyle(float scale, float textScale, bool dark = true) =>
        new(scale, TestLooks.LookFor(dark, textScale), FlyoutTheme.For(TestLooks.LookFor(dark, textScale)), 1, 1);

    private static Vector2 Centre(D2D_RECT_F box) => new((box.left + box.right) / 2, (box.top + box.bottom) / 2);

    // --- What it reads, and what a click does ----------------------------------------------------

    /// <summary>
    /// It reads its folder as soon as it has one, in the drive flyout's order; a line is as tall as its
    /// folder's row and a row an item; one with no folder asks for one, one whose folder is gone says so;
    /// and a pin to the folder is a pin's line, which lists nothing (§380).
    /// </summary>
    [UiFact]
    public void ItReadsItsFolderAndIsAsTallAsWhatItHolds()
    {
        var folders = FakeFolders.Projects();
        using var stand = new FolderSourceState(folders.Source);
        Offscreen.OnStaThread(gfx =>
        {
            var line = Line(gfx, Projects);
            var shown = line.Contents;
            Assert.True(line.LightsItsRows);
            Assert.False(line.Refresh(gfx));
            Assert.Equal(FolderListState.Listed, shown.Contents.State);
            Assert.Equal("Karpo", shown.Contents.Entries[0].Name);
            Assert.NotNull(shown.Contents.Picture);
            Assert.NotEmpty(shown.Contents.Icons);
            Assert.Equal("Projects", shown.Title);

            var style = LineStyle(1.5f, 1f);
            var height = line.ListHeight(PopupPaint.Round(360 * 1.5f), style);
            Assert.Equal(FolderContentsView.HeadRow(style) + (10 * FolderContentsView.ItemRow(style)), height);

            // No folder yet, a folder that is gone: said in the place of the items, one row under the head.
            using var none = Line(gfx, "");
            using var gone = Line(gfx, @"C:\Gone");
            Assert.Equal(FolderListState.Unset, none.Contents.Contents.State);
            Assert.Equal(FolderListState.Missing, gone.Contents.Contents.State);
            Assert.Equal(FolderContentsView.HeadRow(style) + FolderContentsView.ItemRow(style), gone.ListHeight(540, style));
            // A pin to the folder: a pin's line, its size's form (§432: Compact, one row), lighting as a whole.
            using var plain = PinLine(gfx, Projects);
            Assert.Equal(LineRows.Px(1, style), ((IListContent)plain).ListHeight(540, style), 1);
            Assert.False(plain.LightsItsRows);
            line.Dispose();
        });
    }

    /// <summary>
    /// The pin's own icon heads the line of what its folder holds, by the pins' one precedence (§319:
    /// "the icon is not respected, the one from the folder is always shown"): a glyph chosen is drawn in
    /// the folder's icon's place, a file chosen gives its icon, and with none the folder's own is there;
    /// choosing one later reads the line again. Pictured at 150 % in each theme: folder-pin-icon-&lt;dark|light&gt;.png.
    /// </summary>
    [UiFact]
    public void ThePinsOwnIconHeadsItsContents()
    {
        const string Star = "\uE734";
        const string Tool = @"C:\Tools\Builder.exe";
        var folders = FakeFolders.Projects();
        using var stand = new FolderSourceState(folders.Source);
        Offscreen.OnStaThread(gfx =>
        {
            FolderContentsLine Drawn(string icon)
            {
                var widget = Widget(Projects);
                widget.Pin!.Icon = icon;
                var line = (FolderContentsLine)Karpo.Dock.Islands.CreateLine(Key, widget, ListKind.FolderContents, gfx, new Config())!;
                _ = line.Refresh(gfx);
                return line;
            }
            byte[]? Head(FolderContentsLine line) => line.Contents.Contents.Picture?.Data;
            using var own = Drawn("");
            using var glyph = Drawn(Star);
            using var file = Drawn(Tool);
            Assert.Equal(folders.Source.Icon(Projects, FolderContentsView.IconPx)!.Data, Head(own));
            Assert.Null(Head(glyph));
            Assert.Equal(folders.Source.Icon(Tool, FolderContentsView.IconPx)!.Data, Head(file));
            Assert.NotEqual(Head(own), Head(file));

            // Chosen afterwards, on the same folder: read again, the pin's.
            var settings = own.Settings!;
            settings.Pin!.Icon = Tool;
            own.Configure(settings, new Config());
            _ = own.Refresh(gfx);
            Assert.Equal(Head(file), Head(own));

            // Drawn: the glyph where the folder's icon stood, in the line's ink — no folder's yellow
            // there; the line with no icon chosen has the folder's.
            var style = LineStyle(1.5f, 1f);
            var box = LineBox(3, 1.5f, 1f);
            (int Inked, int Yellow) HeadInk(FolderContentsLine line)
            {
                var head = line.Contents!.Board(box, style).Head!.Value.Icon;
                var (inked, yellow) = (0, 0);
                var (width, height) = ((uint)MathF.Ceiling(box.right), (uint)MathF.Ceiling(box.bottom));
                Offscreen.Bitmap(width, height, dc => line.PaintCard(dc, box, style, WidgetSurface.List), bitmap =>
                {
                    var data = new byte[width * height * 4];
                    fixed (byte* into = data)
                    {
                        Karpo.Dock.Native.ComUtil.Check(bitmap->CopyPixels(null, width * 4, (uint)data.Length, into));
                    }
                    for (var y = (int)head.top; y < (int)head.bottom; y++)
                    {
                        for (var x = (int)head.left; x < (int)head.right; x++)
                        {
                            var at = ((y * (int)width) + x) * 4;
                            if (data[at + 3] > 100)
                            {
                                inked++;
                                yellow += data[at + 2] - data[at] > 80 ? 1 : 0;
                            }
                        }
                    }
                });
                return (inked, yellow);
            }
            var drawnGlyph = HeadInk(glyph);
            Assert.True(drawnGlyph.Inked > 20, "nothing drawn in the head's icon box");
            Assert.Equal(0, drawnGlyph.Yellow);
            using var plain = Drawn("");
            Assert.True(HeadInk(plain).Yellow > 20, "the folder's own icon is not drawn where none is chosen");

            foreach (var dark in new[] { true, false })
            {
                var look = ListFormPictures.LookFor(dark);
                var lines = new[] { "", Star, Tool }
                    .Select(icon => ListFormPictures.Line(ListKind.FolderContents, island => { Showing(island, Projects); island.Pin!.Icon = icon; island.Height = LineHeight.Exactly(6); }))
                    .ToArray();
                var folder = ListFormPictures.Folder(gfx, lines);
                foreach (var tile in folder.Tiles)
                {
                    _ = tile.Island.Refresh(gfx);
                }
                var (_, plan) = ListFormPictures.Plan(folder, look, 1.5f);
                var picture = $"folder-pin-icon-{(dark ? "dark" : "light")}.png";
                ListFormPictures.Sheet(gfx, picture, 1.5f, [("Folder contents: its folder's icon, a star chosen, a program's icon chosen", look, folder, plan)]);
                output.WriteLine(Path.Combine(Offscreen.Pictures, picture));
                folder.Closed();
                folder.Dispose();
            }
        });
    }

    /// <summary>
    /// A click on an item's row opens that item — a folder in File Explorer, the navigation a new one
    /// starts with, a file with its own program — only where the pointer is; anywhere else on the line
    /// nothing (2026-10-30); each once its folder has gone (Open), and once.
    /// </summary>
    [UiFact]
    public void AnItemOpensWhereThePointerIsAndNothingElseDoes()
    {
        var folders = new FakeFolders().With(Projects, "Karpo\\", "budget.xlsx", "notes.md").With(Projects + @"\Karpo");
        using var stand = new FolderSourceState(folders.Source);
        Offscreen.OnStaThread(gfx =>
        {
            var line = Line(gfx, Projects);
            line.Refresh(gfx);
            var places = new (string Name, FolderContentsLine Widget, D2D_RECT_F Box, TileStyle Style, WidgetSurface Surface)[]
            {
                ("a line of five rows", line, LineBox(5, 1.5f, 1f), LineStyle(1.5f, 1f), WidgetSurface.List),
            };
            foreach (var (name, widget, box, style, surface) in places)
            {
                folders.Opened.Clear();
                var shown = widget.Contents!;
                var board = shown.Board(box, style);
                Assert.True(board.Rows.Count >= 2, $"{name} shows {board.Rows.Count} rows");
                var folderRow = board.Rows[0];
                var fileRow = board.Rows.First(row => !shown.Contents.Entries[row.Index].Folder);

                // Pressed where the pointer is not: nothing opens.
                var at = Centre(folderRow.Box);
                var target = widget.TargetOn(at, box, style, surface);
                Assert.Equal(FolderContentsView.OpenTarget, target);
                _ = widget.Click(target);
                widget.Open();
                Assert.Empty(folders.Opened);

                widget.HoverOn(at, box, style, surface);
                Assert.Equal(FolderContentsView.OpenTarget, shown.Hot);
                // File Explorer is its navigation: the subfolder opens there.
                _ = widget.Click(target);
                widget.Open();
                Assert.Equal((Projects + @"\Karpo", true), Assert.Single(folders.Opened));
                widget.Open();
                Assert.Single(folders.Opened);

                var fileAt = Centre(fileRow.Box);
                widget.HoverOn(fileAt, box, style, surface);
                _ = widget.Click(widget.TargetOn(fileAt, box, style, surface));
                widget.Open();
                Assert.Equal((FolderReader.PathOf(Projects, shown.Contents.Entries[fileRow.Index].Name), false), folders.Opened[^1]);

                // Beside the items: nothing.
                var beside = new Vector2(box.left + 2, box.top + 2);
                Assert.Equal(FolderContentsView.NoTarget, widget.TargetOn(beside, box, style, surface));
                var opened = folders.Opened.Count;
                Assert.Equal(IslandView.None, widget.Click(FolderContentsView.NoTarget).View);
                widget.Open();
                Assert.Equal(opened, folders.Opened.Count);
                widget.HoverOn(null, box, style, surface);
                Assert.Equal(FolderContentsView.NoTarget, shown.Hot);
            }
            line.Dispose();
        });
    }

    /// <summary>
    /// The navigation applies to the right folder: with "Show its contents" a subfolder's row goes into that
    /// subfolder — never the pinned folder, never a file, never the space beside the rows, which opens nothing; a
    /// file's row always just opens the file. A pin to the folder never goes in
    /// (§380): it opens the folder.
    /// </summary>
    [UiFact]
    public void TheNavigationAppliesToTheFolderClicked()
    {
        var folders = FakeFolders.Projects();
        using var stand = new FolderSourceState(folders.Source);
        Offscreen.OnStaThread(gfx =>
        {
            var (style, surface) = (LineStyle(1f, 1f), WidgetSurface.List);
            using var probe = Line(gfx, Projects, PinFolderNavigation.Contents);
            probe.Refresh(gfx);
            // As tall as every item, so each is clicked where it stands unscrolled.
            var width = TilePaint.Width(LineBox(7, 1f, 1f));
            var box = PopupPaint.Rect(0, 0, width, probe.ListHeight(width, style));
            foreach (var row in probe.Contents.Board(box, style).Rows)
            {
                // A line of its own each time, on the pinned folder.
                using var pin = Line(gfx, Projects, PinFolderNavigation.Contents);
                pin.Refresh(gfx);
                var shown = pin.Contents;
                folders.Opened.Clear();
                var entry = shown.Contents.Entries[row.Index];
                var at = Centre(row.Box);
                pin.HoverOn(at, box, style, surface);
                var clicked = pin.Click(pin.TargetOn(at, box, style, surface));
                pin.Open();
                var path = FolderReader.PathOf(Projects, entry.Name);
                if (entry.Folder)
                {
                    Assert.Equal(IslandView.None, clicked.View);
                    Assert.Equal(path, shown.FolderPath);
                    Assert.Empty(folders.Opened);
                }
                else
                {
                    Assert.Equal(IslandView.App, clicked.View);
                    Assert.Equal(Projects, shown.FolderPath);
                    Assert.Equal((path, false), folders.Opened[^1]);
                }
            }
            // Beside the rows: nothing, on the folder's own items and a subfolder gone into (NothingButAnItemOpensAnything).
            using var line = Line(gfx, Projects, PinFolderNavigation.Contents);
            line.Refresh(gfx);
            folders.Opened.Clear();
            Assert.Equal(IslandView.None, line.Click(FolderContentsView.NoTarget).View);
            line.Contents.Into(Projects + @"\Karpo");
            Assert.Equal(IslandView.None, line.Click(FolderContentsView.NoTarget).View);
            line.Open();
            Assert.Empty(folders.Opened);
            using var plain = PinLine(gfx, Projects);
            Assert.Equal(IslandView.App, plain.Click(0).View);
        });
    }

    /// <summary>
    /// 2026-10-29, the person: "the folder widget should also change the contents inline on navigation and not
    /// actually open the submenus on the left or the right." A click on a subfolder's row, the navigation showing the
    /// contents, shows that subfolder's items in the line's place: nothing opens, and the folder stays open.
    /// </summary>
    [UiFact]
    public void ASubfolderOpensInPlaceOfTheItems()
    {
        var folders = FakeFolders.Projects();
        using var stand = new FolderSourceState(folders.Source);
        Offscreen.OnStaThread(gfx =>
        {
            using var line = Line(gfx, Projects, PinFolderNavigation.Contents);
            _ = line.Refresh(gfx);
            var (box, style, surface) = (LineBox(7, 1f, 1f), LineStyle(1f, 1f), WidgetSurface.List);
            var shown = line.Contents;
            var karpo = shown.Board(box, style).Rows.First(row => shown.Contents.Entries[row.Index].Name == "Karpo");
            var at = Centre(karpo.Box);
            line.HoverOn(at, box, style, surface);
            var clicked = line.Click(line.TargetOn(at, box, style, surface));
            line.Open();
            Assert.Equal(IslandView.None, clicked.View);
            Assert.Empty(folders.Opened);
            Assert.Equal(Projects + @"\Karpo", shown.FolderPath);
            Assert.Equal(["docs", "src", "tests", "Karpo.slnx", "README.md"], shown.Contents.Entries.Select(entry => entry.Name));
            Assert.Equal("Karpo", shown.Title);
            // The folder followed is the one shown.
            Assert.Equal([Projects + @"\Karpo"], folders.Watched);

            // Its head: the way back, its arrow "Back" its tooltip, where the folder's picture stood; the name where it begins.
            var board = shown.Board(box, style);
            var head = board.Head!.Value;
            var back = Assert.IsType<D2D_RECT_F>(head.Arrow);
            Assert.Equal("Karpo", head.Name!.Value.Text);
            Assert.Equal(FolderContentsView.BackTarget, line.TargetOn(Centre(back), box, style, surface));
            Assert.Equal(S.DockPageBack, line.TipOn(Centre(back), box, style, surface)?.Text);
            Assert.Null(line.TipOn(new Vector2(head.Name!.Value.Left + 4, Centre(back).Y), box, style, surface));
            Assert.True(back.left >= box.left && back.right <= head.Name!.Value.Left, "the arrow runs into the name");
            // A subfolder's row has the chevron that says it goes in; a file's none.
            Assert.All(board.Rows, row => Assert.Equal(shown.Contents.Entries[row.Index].Folder, row.Chevron is not null));

            // Back: the folder's own items again, at once, as they were.
            line.HoverOn(Centre(back), box, style, surface);
            _ = line.PressOn(Centre(back), box, style, surface);
            Assert.Equal(IslandView.None, line.Click(FolderContentsView.BackTarget).View);
            Assert.Equal(Projects, shown.FolderPath);
            Assert.Equal(0, shown.Depth);
            Assert.Equal(FolderListState.Listed, shown.Contents.State);
            Assert.Equal("Projects", shown.Title);
            Assert.Null(shown.Board(box, style).Head!.Value.Back);
            Assert.Equal([Projects], folders.Watched);
            Assert.Empty(folders.Opened);
        });
    }

    /// <summary>
    /// 2026-10-30, the person: "for the wifi submenu or bluetooth submenu or also the folder view there is the back
    /// button, but the back button is only on the topleft, i want the whole header area to be the back button." A
    /// subfolder gone into: every point of its head — the arrow, the name, the count and the air between — is the way
    /// back, lit under the pointer, and a click anywhere on it goes back. On the folder's own items the head is
    /// nobody's: no way back, and no File Explorer.
    /// </summary>
    [UiFact]
    public void ASubfoldersWholeHeadIsTheWayBack()
    {
        var folders = FakeFolders.Projects();
        using var stand = new FolderSourceState(folders.Source);
        Offscreen.OnStaThread(gfx =>
        {
            using var line = Line(gfx, Projects, PinFolderNavigation.Contents);
            _ = line.Refresh(gfx);
            var (box, style, surface) = (LineBox(7, 1.5f, 1f), LineStyle(1.5f, 1f), WidgetSurface.List);
            var shown = line.Contents;
            var row = FolderContentsView.HeadRow(style);
            void EveryPoint(Action<Vector2> each)
            {
                for (var y = box.top + 1; y < box.top + row - 1; y += 3)
                {
                    for (var x = box.left + 1; x < box.right - 1; x += 3)
                    {
                        each(new Vector2(x, y));
                    }
                }
            }
            EveryPoint(at => Assert.Equal(FolderContentsView.NoTarget, line.TargetOn(at, box, style, surface)));
            shown.Into(Projects + @"\Karpo");
            EveryPoint(at =>
            {
                Assert.Equal(FolderContentsView.BackTarget, line.TargetOn(at, box, style, surface));
                line.HoverOn(at, box, style, surface);
                Assert.Equal(FolderContentsView.BackTarget, shown.Hot);
            });
            // A click on the count, at the head's far end: back.
            var count = shown.Board(box, style).Head!.Value.Count!.Value;
            var far = new Vector2(count.Left + (count.Room / 2), box.top + (row / 2));
            line.HoverOn(far, box, style, surface);
            _ = line.PressOn(far, box, style, surface);
            Assert.Equal(IslandView.None, line.Click(line.TargetOn(far, box, style, surface)).View);
            Assert.Equal(Projects, shown.FolderPath);
            Assert.Empty(folders.Opened);
        });
    }

    /// <summary>
    /// 2026-10-30, the person: "the folder opens in file explorer when i click somewhere inside of it. this should not
    /// be." Only an item's row opens anything: its head, the air under its rows and a click with no row the keys are
    /// on open nothing — on its own folder and on a subfolder gone into, whatever the navigation.
    /// </summary>
    [UiFact]
    public void NothingButAnItemOpensAnything()
    {
        var folders = FakeFolders.Projects();
        using var stand = new FolderSourceState(folders.Source);
        Offscreen.OnStaThread(gfx =>
        {
            foreach (var navigation in new[] { PinFolderNavigation.Explorer, PinFolderNavigation.Contents })
            {
                using var line = Line(gfx, Projects, navigation);
                _ = line.Refresh(gfx);
                // Taller than its rows, so there is air under them.
                var (box, style, surface) = (LineBox(16, 1f, 1f), LineStyle(1f, 1f), WidgetSurface.List);
                foreach (var inside in new[] { false, true })
                {
                    if (inside)
                    {
                        line.Contents.Into(Projects + @"\Karpo");
                    }
                    var board = line.Contents.Board(box, style);
                    var head = FolderContentsView.HeadRow(style);
                    var under = board.Rows[^1].Box.bottom;
                    Assert.True(under < box.bottom - 4, "no air under the rows");
                    foreach (var at in new[] { new Vector2(box.right - 6, head / 2), new Vector2(box.left + 2, box.top + 2), new Vector2(box.right / 2, (under + box.bottom) / 2), new Vector2(box.left + 2, box.bottom - 2) })
                    {
                        line.HoverOn(at, box, style, surface);
                        _ = line.PressOn(at, box, style, surface);
                        var target = line.TargetOn(at, box, style, surface);
                        if (target == FolderContentsView.BackTarget)
                        {
                            // The head of a subfolder gone into is the way back (ASubfoldersWholeHeadIsTheWayBack); in again.
                            Assert.True(inside);
                            Assert.Equal(IslandView.None, line.Click(target).View);
                            line.Contents.Into(Projects + @"\Karpo");
                            continue;
                        }
                        Assert.Equal(IslandView.None, line.Click(target).View);
                        line.Open();
                        Assert.Empty(folders.Opened);
                    }
                    // Enter with no row the keys are on.
                    Assert.Equal(IslandView.None, line.Click(FolderContentsView.NoTarget).View);
                    line.Open();
                    Assert.Empty(folders.Opened);
                }
            }
        });
    }

    /// <summary>
    /// File Explorer is the right click's (2026-10-30): no click on the line opens the folder there any more, so its
    /// menu at rest has "Open in File Explorer", on the folder shown — a subfolder gone into, that subfolder — which
    /// opens it there, handing the screen away.
    /// </summary>
    [UiFact]
    public void TheRightClickOpensTheFolderShownInFileExplorer()
    {
        var folders = FakeFolders.Projects();
        using var stand = new FolderSourceState(folders.Source);
        Offscreen.OnStaThread(gfx =>
        {
            using var line = Line(gfx, Projects, PinFolderNavigation.Contents);
            _ = line.Refresh(gfx);
            foreach (var shown in new[] { Projects, Projects + @"\Karpo" })
            {
                if (shown != Projects)
                {
                    line.Contents.Into(shown);
                }
                var (items, rows) = TheDock.FolderMenuRows(line.Settings, line);
                var row = Assert.Single(rows);
                Assert.Equal(S.DockDrivesOpenInExplorer, Assert.Single(items).Text);
                Assert.Equal(shown, row.On);
                Assert.True(TheDock.HandsAway(row.Act));
                TheDock.RunAct(row, line, (_, _) => Assert.Fail("opened through the shell, not as a folder in File Explorer"));
                Assert.Equal((shown, true), folders.Opened[^1]);
            }
        });
    }

    /// <summary>
    /// §468, the person: "it should not change its height dependent on level / contents except for the root level of
    /// course". A line is as tall as its own folder's items at every level — Projects' ten in Karpo's five and in an
    /// empty folder — while what it lays out, and scrolls, is the folder shown's; back out, the ten again. Its own
    /// folder changed while a subfolder is shown sizes it once it is shown again.
    /// </summary>
    [UiFact]
    public void ALinesHeightIsItsOwnFoldersAtEveryLevel()
    {
        var folders = FakeFolders.Projects();
        using var stand = new FolderSourceState(folders.Source);
        Offscreen.OnStaThread(gfx =>
        {
            using var line = Line(gfx, Projects, PinFolderNavigation.Contents);
            _ = line.Refresh(gfx);
            var style = LineStyle(1.5f, 1f);
            var width = PopupPaint.Round(360 * 1.5f);
            var (head, row) = (FolderContentsView.HeadRow(style), FolderContentsView.ItemRow(style));
            var own = head + (10 * row);
            Assert.Equal(own, line.ListHeight(width, style));
            Assert.Equal(own, line.ScrollHeight(width, own, style));
            line.Contents.Into(Projects + @"\Karpo");
            Assert.Equal(own, line.ListHeight(width, style));
            Assert.Equal(head + (5 * row), line.ScrollHeight(width, own, style));
            line.Contents.Into(Projects + @"\Karpo\tests");
            Assert.Equal(FolderListState.Empty, line.Contents.Contents.State);
            Assert.Equal(own, line.ListHeight(width, style));
            Assert.Equal(head + row, line.ScrollHeight(width, own, style));
            Assert.True(line.Contents.Back());
            Assert.True(line.Contents.Back());
            Assert.False(line.Contents.Back());
            Assert.Equal(own, line.ListHeight(width, style));

            // Its own folder gets an item while Karpo is shown: still the ten's height there, the eleven's back out.
            line.Contents.Into(Projects + @"\Karpo");
            folders.Change(Projects, "Karpo\\", "Notes\\", "Receipts 2026\\", "Website\\",
                "budget.xlsx", "Holiday plan.docx", "logo.png", "Meeting notes 2.md", "Meeting notes 10.md", "setup.exe", "zebra.txt");
            Assert.Equal(own, line.ListHeight(width, style));
            Assert.True(line.Contents.Back());
            Assert.True(Patience.Until(() => !line.Refresh(gfx) && line.Contents.Contents.Entries.Count == 11), "the item come never showed");
            Assert.Equal(own + row, line.ListHeight(width, style));
        });
    }

    /// <summary>
    /// A folder of more items than its line has rows says no "N more items" and lists no fewer than it holds (the
    /// person, 2026-10-31: "when a folder has more items to show it should actually not say that but just scroll"):
    /// its line is as tall as all 130, held to five rows it scrolls under its head, which stays, and scrolled to its
    /// end the last item shows and a press on it is that item. A hundred and thirty was past the hundred the list held.
    /// </summary>
    [UiFact]
    public void AFolderOfMoreThanFitsScrollsToItsLastItem()
    {
        var names = Enumerable.Range(1, 130).Select(index => $"photo {index}.jpg").ToArray();
        var folders = new FakeFolders().With(Projects, names);
        using var stand = new FolderSourceState(folders.Source);
        Offscreen.OnStaThread(gfx =>
        {
            using var line = Line(gfx, Projects);
            _ = line.Refresh(gfx);
            var style = LineStyle(1.5f, 1f);
            var box = LineBox(5, 1.5f, 1f);
            var width = TilePaint.Width(box);
            var (head, row) = (FolderContentsView.HeadRow(style), FolderContentsView.ItemRow(style));
            Assert.Equal(130, line.Contents.Contents.Entries.Count);
            Assert.Equal(head + (130 * row), line.ListHeight(width, style));

            var laid = line.Scrolled(box, style, WidgetSurface.List);
            Assert.True(laid.Scrolls, "130 items do not fit five rows");
            Assert.Equal(head + (130 * row), laid.Content, 1);
            Assert.True(laid.Head >= head, "the folder's own row stays");

            line.ListScroll = float.MaxValue;
            var said = new List<string>();
            try
            {
                PaintWatch.Said = (text, _) => said.Add(text);
                Offscreen.Bitmap(width, TilePaint.Height(box), dc => line.PaintOn(dc, box, style, WidgetSurface.List), _ => { });
            }
            finally
            {
                PaintWatch.Said = null;
            }
            Assert.Contains("photo 130.jpg", said);
            Assert.Contains("Projects", said);
            Assert.DoesNotContain(said, text => System.Text.RegularExpressions.Regex.IsMatch(text, @"\d+ more"));
            var foot = new Vector2(width / 2, box.bottom - (row / 2));
            Assert.Equal(FolderContentsView.OpenTarget + 129, line.TargetOn(foot, box, style, WidgetSurface.List));
        });
    }

    /// <summary>
    /// The keys, with the line's focus: Down and Up walk the rows it shows, past either end the folder's; Enter on a
    /// subfolder goes in, the keys on its first row; Backspace and Left go back, the keys on the subfolder left; on
    /// the folder's own items both are the folder's; Enter on a file is the folder's click, which opens the file the
    /// keys are on. The pointer moving takes the keys' ring away.
    /// </summary>
    [UiFact]
    public void TheKeysWalkInAndBack()
    {
        var folders = FakeFolders.Projects();
        using var stand = new FolderSourceState(folders.Source);
        Offscreen.OnStaThread(gfx =>
        {
            using var line = Line(gfx, Projects, PinFolderNavigation.Contents);
            _ = line.Refresh(gfx);
            var (box, style, surface) = (LineBox(12, 1f, 1f), LineStyle(1f, 1f), WidgetSurface.List);
            var shown = line.Contents;
            // Laid out once, as the folder draws it: the rows the keys walk.
            _ = shown.Board(box, style);
            string? Keyed() => shown.Keyed()?.Entry.Name;

            Assert.True(line.KeyOn(VK.VK_DOWN, surface));
            Assert.Equal("Karpo", Keyed());
            Assert.False(line.KeyOn(VK.VK_UP, surface));
            Assert.Null(Keyed());
            Assert.True(line.KeyOn(VK.VK_DOWN, surface));
            Assert.True(line.KeyOn(VK.VK_DOWN, surface));
            Assert.Equal("Notes", Keyed());

            // Enter on a subfolder: in, the keys on its first row.
            Assert.True(line.KeyOn(VK.VK_RETURN, surface));
            Assert.Equal(Projects + @"\Notes", shown.FolderPath);
            _ = shown.Board(box, style);
            Assert.Equal("ideas.txt", Keyed());
            // Backspace: back, the keys on the folder left.
            Assert.True(line.KeyOn(VK.VK_BACK, surface));
            Assert.Equal(Projects, shown.FolderPath);
            Assert.Equal("Notes", Keyed());
            // Left the same, from Karpo.
            Assert.True(line.KeyOn(VK.VK_UP, surface));
            Assert.Equal("Karpo", Keyed());
            Assert.True(line.KeyOn(VK.VK_RETURN, surface));
            Assert.Equal(Projects + @"\Karpo", shown.FolderPath);
            _ = shown.Board(box, style);
            Assert.True(line.KeyOn(VK.VK_LEFT, surface));
            Assert.Equal(Projects, shown.FolderPath);
            Assert.Equal("Karpo", Keyed());
            // On the folder's own items Left and Backspace are the folder's.
            Assert.False(line.KeyOn(VK.VK_LEFT, surface));
            Assert.False(line.KeyOn(VK.VK_BACK, surface));

            // Enter on a file: the folder's click, which opens the file the keys are on, once the folder has gone.
            _ = shown.Board(box, style);
            while (Keyed() != "budget.xlsx")
            {
                Assert.True(line.KeyOn(VK.VK_DOWN, surface));
            }
            Assert.False(line.KeyOn(VK.VK_RETURN, surface));
            Assert.Equal(IslandView.App, line.Click(FolderContentsView.NoTarget).View);
            line.Open();
            Assert.Equal((Projects + @"\budget.xlsx", false), Assert.Single(folders.Opened));

            // The pointer moving takes the ring away.
            line.HoverOn(new Vector2(box.left + 2, box.top + 2), box, style, surface);
            Assert.Null(Keyed());
        });
    }

    /// <summary>
    /// A folder's contents line in a list folder, before and after going in, in each theme at 100 and 150 %: on its
    /// own folder, inside Karpo (the way back, "Karpo", its five items), inside Karpo\docs, the pointer on its head's
    /// name, all of the head lit, inside Receipts 2026's thirty scrolled to the last, and back out again:
    /// folder-nav-&lt;dark|light&gt;-&lt;100|150&gt;.png in KARPO_PNG_DIR. Automatic, so each is as tall as Projects' own items (§468).
    /// </summary>
    [UiFact]
    public void GoingInAndBackIsPictured()
    {
        var folders = FakeFolders.Projects().With(Projects + @"\Receipts 2026", [.. Enumerable.Range(1, 30).Select(at => $"receipt {at:00}.pdf")]);
        using var stand = new FolderSourceState(folders.Source);
        foreach (var dark in new[] { true, false })
        {
            foreach (var scale in Scales)
            {
                Offscreen.OnStaThread(gfx =>
                {
                    var look = ListFormPictures.LookFor(dark);
                    var steps = new List<(string Title, Look Look, FolderIsland Folder, FolderPlan Plan)>();
                    var made = new List<FolderIsland>();
                    (FolderIsland Folder, FolderContentsLine Line) Make()
                    {
                        var folder = ListFormPictures.Folder(gfx, ListFormPictures.Line(ListKind.FolderContents, island =>
                        {
                            Showing(island, Projects, PinFolderNavigation.Contents);
                            island.Height = LineHeight.Automatic(ListKind.FolderContents.ListRange().Most);
                        }));
                        made.Add(folder);
                        var line = (FolderContentsLine)folder.Tiles[0].Island;
                        _ = line.Refresh(gfx);
                        return (folder, line);
                    }
                    void Shot(string title, FolderIsland folder) => steps.Add((title, look, folder, ListFormPictures.Plan(folder, look, scale).Plan));

                    var (own, _) = Make();
                    Shot("Projects, its own items", own);
                    var (karpo, inKarpo) = Make();
                    inKarpo.Contents.Into(Projects + @"\Karpo");
                    Shot("Into Karpo: the way back, its name, its five items", karpo);
                    var (docs, inDocs) = Make();
                    inDocs.Contents.Into(Projects + @"\Karpo");
                    inDocs.Contents.Into(Projects + @"\Karpo\docs");
                    var plan = ListFormPictures.Plan(docs, look, scale).Plan;
                    var card = plan.Cards[0];
                    var box = PopupPaint.Rect(0, 0, card.right - card.left, card.bottom - card.top);
                    var style = LineStyle(scale, 1f, dark);
                    var name = inDocs.Contents.Board(box, style).Head!.Value.Name!.Value;
                    inDocs.HoverOn(new Vector2(name.Left + 20, Centre(inDocs.Contents.Board(box, style).Head!.Value.Back!.Value).Y), box, style, WidgetSurface.List);
                    Shot("Into docs, the pointer on its name: the whole head the way back", docs);
                    var (receipts, inReceipts) = Make();
                    inReceipts.Contents.Into(Projects + @"\Receipts 2026");
                    inReceipts.ListScroll = float.MaxValue;
                    Shot("Into Receipts 2026: thirty in the room of ten, scrolled to the last", receipts);
                    var (back, outAgain) = Make();
                    outAgain.Contents.Into(Projects + @"\Karpo");
                    _ = outAgain.Contents.Back();
                    Shot("Back out: Projects again", back);
                    var file = $"folder-nav-{(dark ? "dark" : "light")}-{scale * 100:0}.png";
                    ListFormPictures.Sheet(gfx, file, scale, steps);
                    output.WriteLine(Path.Combine(Offscreen.Pictures, file));
                    foreach (var folder in made)
                    {
                        folder.Closed();
                        folder.Dispose();
                    }
                });
            }
        }
    }

    /// <summary>
    /// The folder is followed: a file saved into it shows once the folder settles, without another
    /// look, and a widget given another folder stops watching the last one.
    /// </summary>
    [UiFact]
    public void AChangeInTheFolderShows()
    {
        var folders = FakeFolders.Projects();
        using var stand = new FolderSourceState(folders.Source);
        Offscreen.OnStaThread(gfx =>
        {
            var line = Line(gfx, Projects + @"\Notes");
            line.Refresh(gfx);
            Assert.Equal(["ideas.txt"], line.Contents!.Contents.Entries.Select(entry => entry.Name));
            Assert.Equal([Projects + @"\Notes"], folders.Watched);
            var reads = folders.Reads;

            // A tick with a watcher does not read: the watcher says when there is something to read.
            Assert.False(line.Tick(gfx));
            Assert.Equal(reads, folders.Reads);

            folders.Change(Projects + @"\Notes", "ideas.txt", "plan.md");
            Assert.True(Patience.Until(() => !line.Refresh(gfx) && line.Contents!.Contents.Entries.Count == 2), "the change never showed");
            Assert.Equal(["ideas.txt", "plan.md"], line.Contents!.Contents.Entries.Select(entry => entry.Name));

            line.Configure(Widget(Projects + @"\Website"), new Config());
            line.Refresh(gfx);
            Assert.Equal(["index.html"], line.Contents!.Contents.Entries.Select(entry => entry.Name));
            Assert.Equal([Projects + @"\Website"], folders.Watched);

            // Given no folder: it stops reading and following it.
            line.Configure(Widget(""), new Config());
            Assert.Equal(FolderListState.Unset, line.Contents.Contents.State);
            Assert.Empty(folders.Watched);
            line.Configure(Widget(Projects + @"\Website"), new Config());
            line.Refresh(gfx);
            Assert.Equal([Projects + @"\Website"], folders.Watched);
            line.Dispose();
            Assert.Empty(folders.Watched);
        });
    }

    // --- How soon it shows ------------------------------------------------------------------------

    /// <summary>
    /// 2026-10-28, the person: "when showing folder contents the reading text is there for around 10
    /// seconds or so". The names come first: a shell slow to draw the folder's picture and the items'
    /// icons holds none of them back; the icons fill in as they come.
    /// </summary>
    [UiFact]
    public void TheNamesShowBeforeTheIcons()
    {
        var folders = FakeFolders.Projects();
        using var slow = new ManualResetEventSlim();
        var icon = folders.Source.Icon;
        var source = new FolderSource
        {
            Enumerate = folders.Source.Enumerate,
            Exists = folders.Source.Exists,
            Icon = (path, px) =>
            {
                slow.Wait(TimeSpan.FromSeconds(30));
                return icon(path, px);
            },
            Watch = folders.Source.Watch,
            Open = folders.Source.Open,
            Run = work => new Thread(() => work()) { IsBackground = true }.Start(),
        };
        using var stand = new FolderSourceState(source);
        Offscreen.OnStaThread(gfx =>
        {
            var line = Line(gfx, Projects);
            _ = line.Refresh(gfx);
            var clock = System.Diagnostics.Stopwatch.StartNew();
            Assert.True(SpinWait.SpinUntil(() => line.Contents!.Contents.State == FolderListState.Listed, 2000),
                $"the names waited on the icons: still {line.Contents!.Contents.State} after {clock.ElapsedMilliseconds} ms");
            Assert.Equal("Karpo", line.Contents!.Contents.Entries[0].Name);
            Assert.Empty(line.Contents.Contents.Icons);
            slow.Set();
            Assert.True(SpinWait.SpinUntil(() => line.Contents!.Contents.Icons.Count > 0, 5000), "the icons never came");
            Assert.NotNull(line.Contents.Contents.Picture);
            line.Dispose();
        });
    }

    /// <summary>
    /// A line given its folder reads it there and then — set up in its menu, or made while its folder
    /// stands open — rather than at the next tick of anything, which in an open folder may be many
    /// seconds away.
    /// </summary>
    [UiFact]
    public void ALineReadsItsFolderAsSoonAsItHasOne()
    {
        var folders = FakeFolders.Projects();
        using var stand = new FolderSourceState(folders.Source);
        Offscreen.OnStaThread(gfx =>
        {
            var line = Line(gfx, Projects);
            Assert.Equal(FolderListState.Listed, line.Contents!.Contents.State);
            line.Configure(Widget(Projects + @"\Website"), new Config());
            Assert.Equal(["index.html"], line.Contents!.Contents.Entries.Select(entry => entry.Name));
            line.Dispose();
        });
    }

    /// <summary>
    /// 2026-10-28, the person: "when i click to open a system folder in file explorer the file explorer
    /// opens behind the karpo folder and the karpo folder does not close". What a click on the line
    /// opens is an app opening, as every other line's is: the folder hears it (IslandView.App,
    /// FolderFlyout.Forwards), closes, and only then is it opened — never at the click, behind the folder.
    /// </summary>
    [UiFact]
    public void WhatAClickOpensClosesTheFolderFirst()
    {
        var folders = new FakeFolders().With(Projects, "Karpo\\", "budget.xlsx").With(Projects + @"\Karpo");
        using var stand = new FolderSourceState(folders.Source);
        Offscreen.OnStaThread(gfx =>
        {
            var line = Line(gfx, Projects);
            line.Refresh(gfx);
            var (box, style) = (LineBox(5, 1.5f, 1f), LineStyle(1.5f, 1f));
            var shown = line.Contents!;
            var act = ListWidgets.Find(ListKind.FolderContents)!.Act;
            foreach (var row in shown.Board(box, style).Rows)
            {
                var at = Centre(row.Box);
                line.HoverOn(at, box, style, WidgetSurface.List);
                var clicked = line.Click(line.TargetOn(at, box, style, WidgetSurface.List));
                Assert.Equal(IslandView.App, clicked.View);
                Assert.True(FolderFlyout.Forwards(clicked, act), "the folder stays open over what was opened");
                Assert.Empty(folders.Opened);
            }
            // Nothing else opens anything (NothingButAnItemOpensAnything).
            Assert.Equal(IslandView.None, line.Click(FolderContentsView.NoTarget).View);
            Assert.Empty(folders.Opened);
            line.Dispose();
        });
    }

    // --- Nothing past its box -------------------------------------------------------------------

    /// <summary>
    /// No line from one row to seven draws past its box — with its folder
    /// listed, empty and missing, at 100 and 150 %, at the list's text and the largest — and every
    /// row it shows is inside it.
    /// </summary>
    [UiFact]
    public void NothingIsDrawnPastItsBox()
    {
        var folders = FakeFolders.Projects().With(@"C:\Empty");
        using var stand = new FolderSourceState(folders.Source);
        var failures = new List<string>();
        Offscreen.OnStaThread(gfx =>
        {
            foreach (var path in new[] { Projects, @"C:\Empty", @"C:\Gone" })
            {
                var line = Line(gfx, path);
                line.Refresh(gfx);
                foreach (var scale in Scales)
                {
                    foreach (var textScale in new[] { 1f, 2.25f })
                    {
                        for (var rows = 1; rows <= 7; rows++)
                        {
                            Watch(failures, $"{path}: a line of {rows} row(s) at {scale * 100:0} %, text {textScale * 100:0} %", line,
                                LineBox(rows, scale, textScale), LineStyle(scale, textScale), WidgetSurface.List);
                        }
                    }
                }
                line.Dispose();
            }
        });
        Assert.True(failures.Count == 0, $"{failures.Count} wrong:\n{string.Join("\n", failures.Take(40))}");
    }

    private static void Watch(List<string> failures, string where, FolderContentsLine widget, D2D_RECT_F box, TileStyle style, WidgetSurface surface)
    {
        const float margin = 24;
        var boxWidth = TilePaint.Width(box);
        var boxHeight = TilePaint.Height(box);
        var width = boxWidth + (2 * margin);
        var height = boxHeight + (2 * margin);
        (float Left, float Top, float Right, float Bottom) ink = default;
        Offscreen.Bitmap(width, height,
            dc => Offscreen.Shifted(dc, margin, margin, () => widget.PaintCard(dc, box, style, surface)),
            bitmap => ink = Offscreen.InkOf(bitmap, (uint)MathF.Ceiling(width), (uint)MathF.Ceiling(height)));
        if (ink.Right > ink.Left
            && (ink.Left < margin - 1 || ink.Top < margin - 1 || ink.Right > margin + boxWidth + 1 || ink.Bottom > margin + boxHeight + 1))
        {
            failures.Add($"{where}: ink at {ink.Left - margin:0},{ink.Top - margin:0}..{ink.Right - margin:0},{ink.Bottom - margin:0}, past its {boxWidth:0}x{boxHeight:0} box");
        }
        if (ink.Right <= ink.Left)
        {
            failures.Add($"{where}: draws nothing");
        }
        if (widget.Contents?.Board(box, style) is { } board)
        {
            foreach (var row in board.Rows)
            {
                if (row.Box.top < box.top - 0.5f || row.Box.bottom > box.bottom + 0.5f)
                {
                    failures.Add($"{where}: row {row.Index} at {row.Box.top:0}..{row.Box.bottom:0} is not inside the box");
                }
            }
        }
    }

    // --- The pictures --------------------------------------------------------------------------

    /// <summary>
    /// Lines one to seven rows tall in a list folder, in each theme, at 100 and 150 %, with the folder
    /// listed, and under them a pin showing just its folder:
    /// folder-pin-lines-&lt;dark|light&gt;-&lt;100|150&gt;.png in KARPO_PNG_DIR.
    /// </summary>
    [UiFact]
    public void EveryLineHeightIsPictured()
    {
        var folders = FakeFolders.Projects();
        using var stand = new FolderSourceState(folders.Source);
        foreach (var dark in new[] { true, false })
        {
            foreach (var scale in Scales)
            {
                Offscreen.OnStaThread(gfx =>
                {
                    var look = ListFormPictures.LookFor(dark);
                    var lines = Enumerable.Range(1, 7)
                        .Select(rows => ListFormPictures.Line(ListKind.FolderContents, island => { Showing(island, Projects); island.Height = LineHeight.Exactly(rows * LineHeight.Row); }))
                        // Just the folder, the pin is the shell's item, which the made-up Projects is not: a folder every PC has.
                        .Append(ListFormPictures.Line(ListKind.Pin, island => { island.Pin!.Target = Environment.GetFolderPath(Environment.SpecialFolder.Windows); island.Height = LineHeight.Exactly(4); }))
                        .ToArray();
                    var folder = ListFormPictures.Folder(gfx, lines);
                    foreach (var tile in folder.Tiles)
                    {
                        _ = tile.Island.Refresh(gfx);
                    }
                    var (_, plan) = ListFormPictures.Plan(folder, look, scale);
                    var file = $"folder-pin-lines-{(dark ? "dark" : "light")}-{scale * 100:0}.png";
                    ListFormPictures.Sheet(gfx, file, scale, [($"Folder contents: 1 to 7 rows, and a pin to a folder", look, folder, plan)]);
                    output.WriteLine(Path.Combine(Offscreen.Pictures, file));
                    folder.Closed();
                    folder.Dispose();
                });
            }
        }
    }

    /// <summary>
    /// A list grouped under two headings — a title over the lines it heads — with a divider put over
    /// the folder's line and no line of the list's own anywhere, and a pin's line lit under the pointer as the open folder lights it (FolderFlyout.InkOf), in
    /// each theme at 100 and 150 %: list-headings-&lt;dark|light&gt;-&lt;100|150&gt;.png in KARPO_PNG_DIR.
    /// </summary>
    [UiFact]
    public void AListUnderHeadingsIsPictured()
    {
        var folders = FakeFolders.Projects();
        using var stand = new FolderSourceState(folders.Source);
        foreach (var dark in new[] { true, false })
        {
            foreach (var scale in Scales)
            {
                Offscreen.OnStaThread(gfx =>
                {
                    var look = ListFormPictures.LookFor(dark);
                    CellItem Pin(string name, string glyph) => ListFormPictures.Line(ListKind.Pin, island =>
                    {
                        island.Pin!.Target = $"https://example.com/{name.ToLowerInvariant()}";
                        island.Pin.Name = name;
                        island.Pin.Icon = glyph;
                    });
                    var folder = ListFormPictures.Folder(gfx,
                        ListFormPictures.Line(ListKind.Heading, island => island.Heading!.Text = "Work"),
                        Pin("Mail", "\uE715"),
                        Pin("Calendar", "\uE787"),
                        // A divider somebody put over the folder's line: the one line between two lines.
                        ListFormPictures.Line(ListKind.Divider),
                        ListFormPictures.Line(ListKind.FolderContents, island => { Showing(island, Projects); island.Height = LineHeight.Exactly(6); }),
                        ListFormPictures.Line(ListKind.Heading, island => island.Heading!.Text = "Evenings"),
                        Pin("Music", "\uEC4F"),
                        Pin("Photos", "\uEB9F"),
                        ListFormPictures.Line(ListKind.Heading));
                    foreach (var tile in folder.Tiles)
                    {
                        _ = tile.Island.Refresh(gfx);
                    }
                    var (_, plan) = ListFormPictures.Plan(folder, look, scale);
                    // The open folder lights the line under the pointer; the sheet draws the fill the
                    // folder's brush is made of, under the second pin's line.
                    var lit = 2;
                    var theme = FlyoutTheme.For(look);
                    var file = $"list-headings-{(dark ? "dark" : "light")}-{scale * 100:0}.png";
                    ListFormPictures.Sheet(gfx, file, scale, [($"Headings: Work, Evenings and one unnamed; a divider over the folder; Calendar under the pointer", look, folder, plan)],
                        under: (lit, PopupPaint.Ink(FolderFlyout.InkOf(theme, FolderFlyout.CardFill.LineHover))));
                    output.WriteLine(Path.Combine(Offscreen.Pictures, file));
                    // No line of the list's own: the one split is the divider, where it was put.
                    Assert.Equal(FolderFlyout.SeparatorRoom(scale), plan.Cards[3].bottom - plan.Cards[3].top, 3);
                    folder.Closed();
                    folder.Dispose();
                });
            }
        }
    }
}

/// <summary>
/// The folder source every folder pin reads (<see cref="FolderSource.Current"/>),
/// stood in for while it lives. Its tests run one class at a time (<see cref="FolderSourceCollection"/>).
/// </summary>
internal sealed class FolderSourceState : IDisposable
{
    public const string Collection = FolderSourceCollection.Name;

    private readonly FolderSource was;

    public FolderSourceState(FolderSource stand)
    {
        was = FolderSource.Current;
        FolderSource.Current = stand;
    }

    public void Dispose() => FolderSource.Current = was;
}

/// <summary>The tests that stand in for the folder source, one class at a time.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class FolderSourceCollection
{
    public const string Name = "folder source";
}
