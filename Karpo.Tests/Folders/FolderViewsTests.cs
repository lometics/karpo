// A folder's contents in File Explorer's views (§498; Karpo.Dock\Islands\FolderContentsView.cs; the person, 2026-11-03:
// "the folder contents widget should also get different styles like grid, details etc. (similar to the ones in the file
// explorer)"), over made-up folders (FakeFolders): each view's layout, what a press and the keys reach in it, how tall
// it is and how it scrolls, what it reads for its view, the thumbnails off the dock's thread — and the pictures, every
// view in each theme at 100 and 150 %, to KARPO_PNG_DIR. The settings' row is FolderViewSettingsTests'.
//
// Nothing here reads a real folder or opens anything. Needs Direct2D, so it runs only with KARPO_UI_TESTS=1.

using System.Numerics;
using Karpo.Dock;
using Karpo.Dock.Drives;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using Xunit.Abstractions;

namespace Karpo.Tests.Folders;

[Collection(FolderSourceState.Collection)]
public sealed unsafe class FolderViewsTests(ITestOutputHelper output)
{
    private const string Key = "layout:default/folder/files";
    private const string Projects = @"C:\Users\Ann\Projects";
    private const string Photos = @"C:\Users\Ann\Pictures\Holiday";

    private static readonly float[] Scales = [1f, 1.5f];

    private static readonly PinFolderView[] Views = [.. PinFolders.Views];

    private static CellItem Widget(string path, PinFolderView view, PinFolderNavigation navigation = PinFolderNavigation.Contents)
    {
        var item = FolderContentsLineTests.Showing(ListKind.FolderContents.New(), path, navigation);
        item.Pin!.View = view;
        return item;
    }

    private static FolderContentsLine Line(Gfx gfx, string path, PinFolderView view, PinFolderNavigation navigation = PinFolderNavigation.Contents)
    {
        var line = (FolderContentsLine)Karpo.Dock.Islands.CreateLine(Key, Widget(path, view, navigation), ListKind.FolderContents, gfx, new Config())!;
        _ = line.Refresh(gfx);
        return line;
    }

    private static TileStyle Style(float scale, bool dark = true) =>
        new(scale, TestLooks.LookFor(dark, 1f), FlyoutTheme.For(TestLooks.LookFor(dark, 1f)), 1, 1);

    /// <summary>A line <paramref name="widthDip"/> wide at <paramref name="scale"/>, as tall as the line asks to be at that width.</summary>
    private static D2D_RECT_F Box(FolderContentsLine line, float widthDip, float scale, TileStyle style)
    {
        var width = PopupPaint.Round(widthDip * scale);
        return PopupPaint.Rect(0, 0, width, line.ListHeight(width, style));
    }

    private static Vector2 Centre(D2D_RECT_F box) => new((box.left + box.right) / 2, (box.top + box.bottom) / 2);

    private static bool Inside(D2D_RECT_F inner, D2D_RECT_F outer) =>
        inner.left >= outer.left - 0.5f && inner.top >= outer.top - 0.5f && inner.right <= outer.right + 0.5f && inner.bottom <= outer.bottom + 0.5f;

    private static bool Overlap(D2D_RECT_F one, D2D_RECT_F two) =>
        MathF.Min(one.right, two.right) - MathF.Max(one.left, two.left) > 0.5f && MathF.Min(one.bottom, two.bottom) - MathF.Max(one.top, two.top) > 0.5f;

    /// <summary>
    /// Each view lays out every item of Projects' ten under the folder's row, in a line 360 DIP wide at 150 %: the list a
    /// band a row as before; the details the column names (Name, Date modified, Size — no room for the type) over a
    /// compact row each, the date and the time, a file's size and no folder's; the tiles one across, a file's type and size
    /// under its name, a folder's "File folder"; the icons four across and the large two, each name under its picture.
    /// Every item stands inside the line, none over another, its middle its own target; the line as tall as its rows.
    /// Twice as wide, the details find room for the type; the tiles stand two across at 480 DIP, three at 720.
    /// </summary>
    [UiFact]
    public void EachViewLaysOutItsItems()
    {
        var folders = FakeFolders.Projects();
        using var stand = new FolderSourceState(folders.Source);
        Offscreen.OnStaThread(gfx =>
        {
            var style = Style(1.5f);
            var (head, row) = (FolderContentsView.HeadRow(style), FolderContentsView.ItemRow(style));
            foreach (var view in Views)
            {
                using var line = Line(gfx, Projects, view);
                var shown = line.Contents;
                Assert.Equal(view, shown.View);
                var box = Box(line, 360, 1.5f, style);
                var board = shown.Board(box, style);
                Assert.Equal(10, board.Rows.Count);
                var shape = shown.Shape(view, box.right - box.left, style);
                var rows = (10 + shape.Columns - 1) / shape.Columns;
                Assert.Equal(head + shape.Over + (rows * shape.Row) + shape.Under, box.bottom - box.top, 1);
                Assert.Equal(shape.Columns, board.Across);
                for (var index = 0; index < board.Rows.Count; index++)
                {
                    var item = board.Rows[index];
                    Assert.True(Inside(item.Box, box), $"{view}: item {index} at {item.Box.left:0},{item.Box.top:0}..{item.Box.right:0},{item.Box.bottom:0} outside the line");
                    Assert.True(Inside(item.Icon, item.Box), $"{view}: item {index}'s picture outside its item");
                    Assert.True(item.Box.top >= box.top + head - 0.5f, $"{view}: item {index} over the folder's row");
                    Assert.All(board.Rows.Skip(index + 1), other => Assert.False(Overlap(item.Box, other.Box), $"{view}: items {index} and {other.Index} overlap"));
                    Assert.Equal(FolderContentsView.OpenTarget + item.Index, line.TargetOn(Centre(item.Box), box, style, WidgetSurface.List));
                    Assert.NotNull(item.Name);
                }
                var files = board.Rows.Where(item => !shown.Contents.Entries[item.Index].Folder).ToList();
                var subfolders = board.Rows.Where(item => shown.Contents.Entries[item.Index].Folder).ToList();
                switch (view)
                {
                    case PinFolderView.List:
                        Assert.Equal((1, row), (shape.Columns, shape.Row));
                        Assert.Empty(board.Columns);
                        Assert.All(subfolders, item => Assert.NotNull(item.Chevron));
                        break;
                    case PinFolderView.Details:
                        Assert.Equal([S.DockFolderDetailsName, S.DockFolderDetailsModified, S.DockFolderDetailsSize], board.Columns.Select(words => words.Text));
                        Assert.NotNull(board.Rule);
                        Assert.All(board.Rows, item => Assert.Equal(FolderContentsView.ModifiedAt(shown.Contents.Entries[item.Index]), item.Date!.Value.Text));
                        Assert.All(files, item => Assert.Equal(FolderContentsView.SizeOf(shown.Contents.Entries[item.Index]), Assert.Single(item.More!).Text));
                        Assert.All(subfolders, item => Assert.Empty(item.More!));
                        Assert.All(board.Rows, item => Assert.Null(item.Chevron));
                        Assert.Equal(head + shape.Over, line.ScrollHead(box.right - box.left, box.bottom - box.top, style));
                        break;
                    case PinFolderView.Tiles:
                        Assert.Equal(1, shape.Columns);
                        var budget = files.First(item => shown.Contents.Entries[item.Index].Name == "budget.xlsx");
                        Assert.Equal(["Microsoft Excel Worksheet", FolderContentsView.SizeOf(shown.Contents.Entries[budget.Index])], budget.More!.Select(words => words.Text));
                        Assert.All(subfolders, item => Assert.Equal([S.DockFolderTypeFolder], item.More!.Select(words => words.Text)));
                        // One across: bands edge to edge, as a list's.
                        Assert.All(board.Rows, item => Assert.Null(item.Plate));
                        Assert.All(board.Rows, item => Assert.Equal((box.left, box.right), (item.Box.left, item.Box.right)));
                        break;
                    case PinFolderView.MediumIcons:
                        Assert.Equal(4, shape.Columns);
                        Assert.All(board.Rows, item => Assert.NotNull(item.Plate));
                        Assert.All(board.Rows, item => Assert.True(item.Name!.Value.Top >= item.Icon.bottom, "a name over its picture"));
                        Assert.All(board.Rows, item => Assert.Equal(PopupPaint.Round(48 * 1.5f), item.Icon.right - item.Icon.left, 1));
                        break;
                    case PinFolderView.LargeIcons:
                        Assert.Equal(2, shape.Columns);
                        Assert.All(board.Rows, item => Assert.Equal(PopupPaint.Round(96 * 1.5f), item.Icon.right - item.Icon.left, 1));
                        break;
                }
                // The pictures and the videos wear their thumbnails in the tiles and the icons only.
                var logo = board.Rows.Single(item => shown.Contents.Entries[item.Index].Name == "logo.png");
                Assert.Equal(view is PinFolderView.Tiles or PinFolderView.MediumIcons or PinFolderView.LargeIcons, logo.Thumb);
            }

            // Twice as wide the type has its column; the tiles stand two across at a wide folder's 480, three at 720.
            using var details = Line(gfx, Projects, PinFolderView.Details);
            var wide = Box(details, 720, 1.5f, style);
            Assert.Equal([S.DockFolderDetailsName, S.DockFolderDetailsModified, S.DockFolderDetailsType, S.DockFolderDetailsSize],
                details.Contents.Board(wide, style).Columns.Select(words => words.Text));
            using var tiles = Line(gfx, Projects, PinFolderView.Tiles);
            Assert.Equal(2, tiles.Contents.Board(Box(tiles, 480, 1.5f, style), style).Across);
            Assert.Equal(3, tiles.Contents.Board(Box(tiles, 720, 1.5f, style), style).Across);
        });
    }

    /// <summary>
    /// The keys walk a grid in two dimensions (§470, §498): in Projects' ten as medium icons four across, Down from none
    /// is the first, Right the next, Down the one under it, Down from the end of a full row into the shorter last row its
    /// last item, Down past the last row the folder's; Left from the first the folder's — never the way back; Enter on a
    /// subfolder goes in, the keys on its first item; Left there walks; Backspace goes back, the keys on the subfolder left.
    /// A list's Left is still the way back.
    /// </summary>
    [UiFact]
    public void TheKeysWalkAGridInTwoDimensions()
    {
        var folders = FakeFolders.Projects();
        using var stand = new FolderSourceState(folders.Source);
        Offscreen.OnStaThread(gfx =>
        {
            var style = Style(1.5f);
            var surface = WidgetSurface.List;
            using var line = Line(gfx, Projects, PinFolderView.MediumIcons);
            var shown = line.Contents;
            var box = Box(line, 360, 1.5f, style);
            _ = shown.Board(box, style);
            Assert.True(shown.Grid);
            int? At() => shown.KeyedTarget is var keyed && keyed >= FolderContentsView.OpenTarget ? keyed - FolderContentsView.OpenTarget : null;

            Assert.True(line.KeyOn(VK.VK_DOWN, surface));
            Assert.Equal(0, At());
            Assert.True(line.KeyOn(VK.VK_RIGHT, surface));
            Assert.Equal(1, At());
            Assert.True(line.KeyOn(VK.VK_DOWN, surface));
            Assert.Equal(5, At());
            Assert.True(line.KeyOn(VK.VK_RIGHT, surface));
            Assert.True(line.KeyOn(VK.VK_RIGHT, surface));
            Assert.Equal(7, At());
            // The row under holds two: its last.
            Assert.True(line.KeyOn(VK.VK_DOWN, surface));
            Assert.Equal(9, At());
            Assert.False(line.KeyOn(VK.VK_DOWN, surface));
            Assert.Null(At());
            Assert.True(line.KeyOn(VK.VK_UP, surface));
            Assert.Equal(9, At());
            Assert.True(line.KeyOn(VK.VK_UP, surface));
            Assert.Equal(5, At());
            Assert.True(line.KeyOn(VK.VK_UP, surface));
            Assert.True(line.KeyOn(VK.VK_LEFT, surface));
            Assert.Equal(0, At());
            Assert.False(line.KeyOn(VK.VK_LEFT, surface));
            Assert.Equal(Projects, shown.FolderPath);

            // Enter on Karpo: in, the keys on its first; Left walks there, Backspace goes back.
            Assert.True(line.KeyOn(VK.VK_RIGHT, surface));
            Assert.True(line.KeyOn(VK.VK_RETURN, surface));
            Assert.Equal(Projects + @"\Karpo", shown.FolderPath);
            _ = shown.Board(box, style);
            Assert.Equal(0, At());
            Assert.True(line.KeyOn(VK.VK_RIGHT, surface));
            Assert.True(line.KeyOn(VK.VK_LEFT, surface));
            Assert.Equal(Projects + @"\Karpo", shown.FolderPath);
            Assert.True(line.KeyOn(VK.VK_BACK, surface));
            Assert.Equal(Projects, shown.FolderPath);
            Assert.Equal("Karpo", shown.Keyed()?.Entry.Name);

            // The row the keys come to is brought into sight: the cells' row it stands in.
            _ = shown.Board(box, style);
            Assert.True(line.KeyOn(VK.VK_DOWN, surface));
            var shape = shown.Shape(PinFolderView.MediumIcons, box.right - box.left, style);
            var reveal = line.ScrollReveal(box.right - box.left, style);
            Assert.Equal(FolderContentsView.HeadRow(style) + shape.Over + shape.Row, reveal!.Value.Top, 1);

            // A list: Left is the way back, as before.
            using var list = Line(gfx, Projects, PinFolderView.List);
            _ = list.Contents.Board(Box(list, 360, 1.5f, style), style);
            Assert.False(list.Contents.Grid);
            Assert.True(list.KeyOn(VK.VK_DOWN, surface));
            Assert.True(list.KeyOn(VK.VK_RETURN, surface));
            Assert.Equal(Projects + @"\Karpo", list.Contents.FolderPath);
            Assert.True(list.KeyOn(VK.VK_LEFT, surface));
            Assert.Equal(Projects, list.Contents.FolderPath);
        });
    }

    /// <summary>
    /// What a view reads for itself: the icons at its size (the list's 40 px, the medium icons' 96, the large icons' 192)
    /// and, for the details and the tiles, what each kind of file is called — read again when the view changes, the items
    /// shown meanwhile, never "Reading…"; a kind the shell names nothing is its extension's "MD File"... here
    /// "Markdown Source File", a kind it does not know "TXT File".
    /// </summary>
    [UiFact]
    public void AViewReadsItsIconsAtItsSizeAndItsTypes()
    {
        var folders = FakeFolders.Projects().With(Projects + @"\Notes", "ideas.txt", "plan");
        using var stand = new FolderSourceState(folders.Source);
        Offscreen.OnStaThread(gfx =>
        {
            using var line = Line(gfx, Projects, PinFolderView.List);
            int Px() => line.Contents.Contents.Icons.Values.First().Width;
            Assert.Equal(FolderContentsView.IconPx, Px());
            Assert.Empty(line.Contents.Contents.Types);

            var settings = line.Settings!;
            foreach (var (view, px) in new[] { (PinFolderView.MediumIcons, 96), (PinFolderView.LargeIcons, 192), (PinFolderView.Details, FolderContentsView.IconPx) })
            {
                var reads = folders.Reads;
                settings.Pin!.View = view;
                line.Configure(settings, new Config());
                Assert.Equal(view, line.Contents.View);
                Assert.Equal(FolderListState.Listed, line.Contents.Contents.State);
                _ = line.Refresh(gfx);
                Assert.Equal(px, Px());
                Assert.True(folders.Reads > reads, $"{view} was not read again");
            }
            Assert.Equal("Microsoft Excel Worksheet", line.Contents.Contents.Types[".xlsx"]);
            var entries = line.Contents.Contents.Entries;
            Assert.Equal(S.DockFolderTypeFolder, FolderReader.TypeOf(entries.Single(entry => entry.Name == "Karpo"), line.Contents.Contents));
            Assert.Equal("Markdown Source File", FolderReader.TypeOf(entries.Single(entry => entry.Name == "Meeting notes 2.md"), line.Contents.Contents));
            line.Contents.Into(Projects + @"\Notes");
            _ = line.Refresh(gfx);
            var notes = line.Contents.Contents;
            Assert.Equal(S.DockFolderTypeFile("TXT"), FolderReader.TypeOf(notes.Entries.Single(entry => entry.Name == "ideas.txt"), notes));
            Assert.Equal(S.DockFolderTypePlain, FolderReader.TypeOf(notes.Entries.Single(entry => entry.Name == "plan"), notes));

            // The same view again reads nothing.
            var before = folders.Reads;
            line.Configure(settings, new Config());
            Assert.Equal(before, folders.Reads);
        });
    }

    /// <summary>
    /// The thumbnails are never asked for on the dock's thread (§498): a large-icons line of photos drawn asks the shell
    /// nothing as it draws — the cells wear their kind's icon — and hands the asking to the folder source's run, which
    /// here a test holds and then lets go on a thread of its own; the line is woken as they come, and drawn again wears
    /// them; drawn again after that, nothing is asked twice. Only the pictures shown are asked for, and none for a file
    /// that has no thumbnail; what is kept stays in its budget.
    /// </summary>
    [UiFact]
    public void ThumbnailsAreReadOffTheDocksThreadAndKept()
    {
        var names = Enumerable.Range(1, 40).Select(at => $"beach {at:00}.jpg").Append("notes.txt").ToArray();
        var folders = new FakeFolders().With(Photos, names);
        var held = new List<Action>();
        var source = folders.RunBy(work =>
        {
            lock (held)
            {
                held.Add(work);
            }
        });
        void LetGo(bool away)
        {
            while (true)
            {
                Action[] works;
                lock (held)
                {
                    works = [.. held];
                    held.Clear();
                }
                if (works.Length == 0)
                {
                    return;
                }
                foreach (var work in works)
                {
                    if (away)
                    {
                        Task.Factory.StartNew(work, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default).Wait();
                    }
                    else
                    {
                        work();
                    }
                }
            }
        }
        using var stand = new FolderSourceState(source);
        Offscreen.OnStaThread(gfx =>
        {
            var woken = 0;
            var style = Style(1f);
            using var line = (FolderContentsLine)Karpo.Dock.Islands.CreateLine(Key, Widget(Photos, PinFolderView.LargeIcons), ListKind.FolderContents, gfx, new Config())!;
            // The listing, read as the dock's own reads are, apart from the thumbnails.
            LetGo(away: false);
            Assert.Equal(41, line.Contents.Contents.Entries.Count);
            var thumbnails = line.Contents.Thumbnails;
            // Five rows of cells: twelve or so photos shown of forty.
            var width = PopupPaint.Round(360f);
            var box = PopupPaint.Rect(0, 0, width, FolderContentsView.HeadRow(style) + (3 * line.Contents.Shape(PinFolderView.LargeIcons, width, style).Row));
            var dock = Environment.CurrentManagedThreadId;
            void Draw() => Offscreen.Bitmap(width, box.bottom, dc => line.Contents.PaintCard(dc, box, style), _ => { });
            Draw();
            Assert.Equal(0, folders.Thumbnails);
            Assert.NotEmpty(held);

            // Let go on a thread of their own: asked there, for the photos shown alone.
            LetGo(away: true);
            Assert.True(folders.Thumbnails > 0, "no thumbnail was asked for");
            Assert.DoesNotContain(dock, folders.ThumbnailThreads);
            var shown = line.Contents.Board(box, style).Rows.Count;
            Assert.True(folders.Thumbnails <= shown, $"{folders.Thumbnails} asked for, {shown} shown");
            var asked = folders.Thumbnails;
            var first = line.Contents.Contents.Entries[0];
            Assert.NotNull(thumbnails.Of(FolderReader.PathOf(Photos, first.Name), first.Modified, FolderContentsView.IconPxOf(PinFolderView.LargeIcons)));

            // Drawn again: the thumbnails from what is kept, nothing asked.
            Draw();
            LetGo(away: true);
            Assert.Equal(asked, folders.Thumbnails);
            Assert.True(thumbnails.Bytes is > 0 and <= FolderThumbnails.Budget);
            _ = woken;
        });
    }

    /// <summary>
    /// A view of more than fits scrolls under its head (§461) and keeps its own folder's height at every level (§468):
    /// the details of a hundred and thirty held to five rows scroll under the folder's row and the column names, which
    /// stay, the last item reached at the foot; a grid of them as large icons the same; gone into a subfolder of three,
    /// the line as tall as its own folder's grid.
    /// </summary>
    [UiFact]
    public void AViewScrollsUnderItsHeadAndKeepsItsRootsHeight()
    {
        var names = Enumerable.Range(1, 130).Select(index => $"photo {index}.jpg").Prepend("Trip\\").ToArray();
        var folders = new FakeFolders().With(Photos, names).With(Photos + @"\Trip", "a.jpg", "b.jpg", "c.jpg");
        using var stand = new FolderSourceState(folders.Source);
        Offscreen.OnStaThread(gfx =>
        {
            var style = Style(1.5f);
            foreach (var view in new[] { PinFolderView.Details, PinFolderView.LargeIcons, PinFolderView.Tiles })
            {
                using var line = Line(gfx, Photos, view);
                var width = PopupPaint.Round(360 * 1.5f);
                var box = PopupPaint.Rect(0, 0, width, ListFormPictures.Rows(5, 1.5f, 1f));
                var own = line.ListHeight(width, style);
                var laid = line.Scrolled(box, style, WidgetSurface.List);
                Assert.True(laid.Scrolls, $"{view}: 131 items do not scroll in five rows");
                Assert.Equal(own, laid.Content, 1);
                // The head keeps its little air under it, as every scrolled line's does (CellIsland.Scrolled).
                Assert.Equal(line.Contents.Head(width, style) + style.Px(2), laid.Head, 1);
                line.ListScroll = float.MaxValue;
                var said = new List<string>();
                try
                {
                    PaintWatch.Said = (text, _) => said.Add(text);
                    Offscreen.Bitmap(width, box.bottom, dc => line.PaintOn(dc, box, style, WidgetSurface.List), _ => { });
                }
                finally
                {
                    PaintWatch.Said = null;
                }
                Assert.Contains(said, text => text.StartsWith("photo 130", StringComparison.Ordinal));
                if (view == PinFolderView.Details)
                {
                    Assert.Contains(S.DockFolderDetailsName, said);
                }

                line.ListScroll = 0;
                line.Contents.Into(Photos + @"\Trip");
                Assert.Equal(own, line.ListHeight(width, style));
                Assert.True(line.Contents.Back());
            }
        });
    }

    // --- The pictures --------------------------------------------------------------------------

    /// <summary>A list folder of <paramref name="lines"/> at <paramref name="width"/>, opened.</summary>
    private static FolderIsland Folder(Gfx gfx, FolderListWidth width, params CellItem[] lines)
    {
        var holder = BarKind.FolderList.New();
        holder.Folder!.Widths = [width];
        foreach (var line in lines)
        {
            Assert.NotNull(Karpo.Core.Folders.Add(BarKind.FolderList, holder.Folder!, line));
        }
        var folder = new FolderIsland($"layout:default/{holder.Id}", holder, gfx, new Config());
        folder.Opened(gfx);
        foreach (var tile in folder.Tiles)
        {
            _ = tile.Island.Refresh(gfx);
        }
        return folder;
    }

    /// <summary>
    /// Every view of Projects (and a folder of photos for the thumbnails) in a list folder, in each theme at 100 and 150 %:
    /// folder-views-&lt;dark|light&gt;-&lt;100|150&gt;.png — the five views in a column of the folder's own width, the
    /// pointer on one item and the keys' ring on another; folder-views-wide-…png — the details, the tiles and the large
    /// icons in a wide column, and the large icons of photos with their thumbnails.
    /// </summary>
    [UiFact]
    public void EveryViewIsPictured()
    {
        var folders = FakeFolders.Projects().With(Photos, [.. Enumerable.Range(1, 9).Select(at => $"beach {at:00}.jpg"), "Day trip.mp4", "notes.txt"]);
        using var stand = new FolderSourceState(folders.Source);
        foreach (var dark in new[] { true, false })
        {
            foreach (var scale in Scales)
            {
                Offscreen.OnStaThread(gfx =>
                {
                    var look = ListFormPictures.LookFor(dark);
                    var style = Style(scale, dark);
                    var tone = dark ? "dark" : "light";
                    CellItem Of(string path, PinFolderView view) => ListFormPictures.Line(ListKind.FolderContents, island =>
                    {
                        FolderContentsLineTests.Showing(island, path, PinFolderNavigation.Contents);
                        island.Pin!.View = view;
                        island.Height = LineHeight.Automatic(ListKind.FolderContents.ListRange().Most);
                    });
                    var panels = new List<(string Caption, Look Look, FolderIsland Folder, FolderPlan Plan)>();
                    var made = new List<FolderIsland>();
                    foreach (var view in Views)
                    {
                        var folder = Folder(gfx, FolderListWidth.Medium, Of(Projects, view));
                        made.Add(folder);
                        var plan = ListFormPictures.Plan(folder, look, scale).Plan;
                        var line = (FolderContentsLine)folder.Tiles[0].Island;
                        var card = plan.Cards[0];
                        var box = PopupPaint.Rect(0, 0, card.right - card.left, card.bottom - card.top);
                        var board = line.Contents.Board(box, style);
                        // The pointer on the third item, the keys on the sixth (a grid's fourth: in sight in its first two rows).
                        line.HoverOn(Centre(board.Rows[2].Box), box, style, WidgetSurface.List);
                        _ = line.Contents.Board(box, style);
                        var grid = view is PinFolderView.MediumIcons or PinFolderView.LargeIcons;
                        for (var step = 0; step < (grid ? 4 : 6); step++)
                        {
                            _ = line.KeyOn(grid ? VK.VK_RIGHT : VK.VK_DOWN, WidgetSurface.List);
                        }
                        panels.Add(($"{view.Title()}: {(view == PinFolderView.List ? "as before" : "Projects")}", look, folder, plan));
                    }
                    var file = $"folder-views-{tone}-{scale * 100:0}.png";
                    ListFormPictures.Sheet(gfx, file, scale, panels);
                    output.WriteLine(Path.Combine(Offscreen.Pictures, file));

                    var wide = new List<(string Caption, Look Look, FolderIsland Folder, FolderPlan Plan)>();
                    foreach (var (path, view, caption) in new[]
                    {
                        (Projects, PinFolderView.Details, "Details: wide, every column"),
                        (Projects, PinFolderView.Tiles, "Tiles: three across"),
                        (Photos, PinFolderView.LargeIcons, "Large icons: the photos' thumbnails"),
                        (Photos, PinFolderView.MediumIcons, "Medium icons: the photos' thumbnails"),
                    })
                    {
                        var folder = Folder(gfx, FolderListWidth.Widest, Of(path, view));
                        made.Add(folder);
                        // The thumbnails are asked for as a line is drawn: drawn once, then pictured.
                        var plan = ListFormPictures.Plan(folder, look, scale).Plan;
                        ListFormPictures.Sheet(gfx, "folder-views-scratch.png", scale, [(caption, look, folder, plan)]);
                        wide.Add((caption, look, folder, plan));
                    }
                    var wideFile = $"folder-views-wide-{tone}-{scale * 100:0}.png";
                    ListFormPictures.Sheet(gfx, wideFile, scale, wide);
                    output.WriteLine(Path.Combine(Offscreen.Pictures, wideFile));
                    File.Delete(Path.Combine(Offscreen.Pictures, "folder-views-scratch.png"));
                    foreach (var folder in made)
                    {
                        folder.Closed();
                        folder.Dispose();
                    }
                });
            }
        }
    }
}
