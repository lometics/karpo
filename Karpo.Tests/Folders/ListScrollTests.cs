// A list line's items scroll inside it, and what a line shows stands at its top (the person,
// 2026-10-11: "if content is cut off by the fixed height the widgets should always be scrollable in
// list view. dont just show x more items, let the user scroll down to see all of the other items
// too." and "if the widgets cannot fill the space available to them they should be top aligned and
// not center their content somehow. make sure this is done with all widgets.").
//
// Held here: every kind of line drawn into a tall Fixed line puts its first ink in its first row; a
// line of items given less than they take scrolls rather than saying "N more" — every item reachable,
// a click at the foot of a line scrolled to its end landing on the last item, the head standing still
// while the items move; and in an open folder the wheel scrolls a line's items until their end and
// no further: the folder itself never scrolls.
//
// Sheets: list-scroll-<dark|light>.png (lines at their start, scrolled half-way and to their end, with
// the overlay scrollbar) and list-item-styles-<dark|light>.png (Compact, Standard and Large, and the
// parts left out) in KARPO_PNG_DIR. Needs Direct2D: KARPO_UI_TESTS=1.

using System.Numerics;
using Karpo.Dock;
using Karpo.Dock.Native;
using Karpo.Dock.Pim;
using Karpo.Tests.Edit;
using Karpo.Tests.Forecasts;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using Xunit.Abstractions;
using static Karpo.Tests.Islands.Offscreen;
using FakeClipboard = Karpo.Tests.Clipboard.ClipboardHistoryTests.FakeClipboard;

namespace Karpo.Tests.Folders;

public sealed unsafe class ListScrollTests(ITestOutputHelper output)
{
    private const string Owner = "layout:default";

    private const float Scale = 1.5f;

    private static Look LookFor(bool dark) => ListFormPictures.LookFor(dark);

    private static TileStyle StyleFor(float scale, Look look) => FolderFlyout.StyleFor(scale, look, FlyoutTheme.For(look));

    /// <summary>n rows of a list, in px, as the plan gives a Fixed line.</summary>
    private static float Rows(int rows) => ListFormPictures.Rows(rows, Scale, 1f);

    // --- Top-aligned -------------------------------------------------------------------------------

    /// <summary>
    /// Every kind of line that is its own card, drawn into a line ten rows tall, puts its first ink in
    /// its first row: what does not fill the line stands at its top, never in its middle. Only an empty
    /// state may stand in the middle, as the removable devices' "Nothing plugged in" does by design, and
    /// a line that is not set up is its "Set up in Settings". A sized line (§432) too, in its fullest size — a pin
    /// with what it opens among them, which stood in the middle of a line from three rows.
    /// </summary>
    [UiFact]
    public void EveryLineStandsAtTheTopOfATallLine()
    {
        OnStaThread(gfx =>
        {
            var look = LookFor(dark: true);
            var style = StyleFor(Scale, look) with { Rows = 10 };
            var width = FolderFlyout.ListWidth(Scale);
            var height = Rows(10);
            var row = Rows(1);
            var wrong = new List<string>();
            var seen = new List<string>();
            using var stage = new ListLinesTests.Stage(gfx);
            using var clips = Clips(12);
            using var people = People(12);
            var config = new Config();
            foreach (var line in ListWidgets.All.Where(line => line.Line == ListLine.Card && (line.Kind.HasHeight() || line.Kind.Sized())))
            {
                var kind = line.Kind;
                var item = kind.New();
                item.Id = kind.Id();
                item.Height = LineHeight.Exactly(20);
                item.Size = LineSize.Full;
                if (kind == ListKind.Pin)
                {
                    item.Pin = new PinOptions { Target = "https://example.com/notes", Name = "Notes", Icon = "" };
                }
                item.Normalize();
                CellIsland? own = ListLinesTests.Kinds.Contains(kind) ? null
                    : kind == ListKind.Clipboard ? clips.Line(gfx)
                    : kind == ListKind.Contacts ? people.Line(gfx)
                    : (CellIsland?)Karpo.Dock.Islands.CreateLine(Config.IslandKey(Owner, item.Id), item, kind, gfx, config);
                var island = own ?? stage[kind];
                try
                {
                    _ = island.Refresh(gfx);
                    // An empty state stands in the middle by design; a line not set up is its link; the
                    // toggles' row of buttons is one control, centred (2026-10-12, FlyoutLinesTests).
                    if (island.Unset is not null || kind == ListKind.Toggles || (kind == ListKind.RemovableDevices && island is RemovableIsland { Devices.Count: 0 }))
                    {
                        continue;
                    }
                    var top = InkTop(island, width, height, style);
                    if (top < 0)
                    {
                        continue;
                    }
                    seen.Add($"{kind} {top:0}");
                    if (top > row + 0.5f)
                    {
                        wrong.Add($"{kind}: first ink {top:0} px down a line {height:0} px tall, past its first row ({row:0} px)");
                    }
                }
                finally
                {
                    own?.Dispose();
                }
            }
            output.WriteLine(string.Join(", ", seen));
            Assert.True(seen.Count >= 30, $"only {seen.Count} kinds drew: {string.Join(", ", seen)}");
            Assert.Contains(seen, said => said.StartsWith("Pin ", StringComparison.Ordinal));
            Assert.True(wrong.Count == 0, string.Join("\n", wrong));
        });
    }

    /// <summary>
    /// A pin with what it opens stands at the top of a line far taller than its form: its whole widget's own
    /// layout, the icon and the name in the first row (§432; it re-centred from three rows), and either size as the
    /// folder draws it, its first ink in the first row.
    /// </summary>
    [UiFact]
    public void APinsFormStandsAtTheTopOfATallBox()
    {
        OnStaThread(gfx =>
        {
            var style = StyleFor(Scale, LookFor(dark: true));
            var row = Rows(1);
            var width = FolderFlyout.ListWidth(Scale);
            foreach (var size in new[] { LineSize.Compact, LineSize.Full })
            {
                var item = ListKind.Pin.New();
                item.Id = "pin";
                item.Size = size;
                item.Pin = new PinOptions { Target = "https://example.com/notes", Name = "Notes", Icon = "" };
                item.Normalize();
                using var pin = (PinTile)Karpo.Dock.Islands.CreateLine(Config.IslandKey(Owner, item.Id), item, ListKind.Pin, gfx, new Config())!;
                foreach (var rows in new[] { 3, 6, 10 })
                {
                    var top = InkTop(pin, width, Rows(rows), style);
                    Assert.True(top >= 0 && top < row, $"{size} in {rows} rows: first ink at {top:0}, past the first row ({row:0})");
                    if (size == LineSize.Full)
                    {
                        var laid = pin.LineOf(PopupPaint.Rect(0, 0, width, Rows(rows)), style);
                        Assert.True(laid.Icon.top < row && laid.Name.top < row, $"{size} in {rows} rows: icon at {laid.Icon.top:0}, name at {laid.Name.top:0}, past the first row ({row:0})");
                    }
                }
            }
        });
    }

    /// <summary>Where <paramref name="island"/>'s first ink is, drawn into a line <paramref name="height"/> px tall; -1 where it drew nothing.</summary>
    private static float InkTop(CellIsland island, float width, float height, in TileStyle style)
    {
        var look = style;
        (float Left, float Top, float Right, float Bottom) ink = default;
        Bitmap(width, height,
            dc => island.PaintOn(dc, PopupPaint.Rect(0, 0, width, height), look, WidgetSurface.List),
            bitmap => ink = InkOf(bitmap, (uint)MathF.Ceiling(width), (uint)MathF.Ceiling(height)));
        return ink.Right > ink.Left ? ink.Top : -1;
    }

    // --- Scrolling inside a line -------------------------------------------------------------------

    /// <summary>
    /// A clipboard of twenty items in a line three rows tall: no "N more" — it is laid out as tall as
    /// every item and scrolls. At its start the first item is under the header; scrolled to its end a
    /// press at the line's foot lands on the last item, and the header has not moved.
    /// </summary>
    [UiFact]
    public void ALineShorterThanItsItemsScrollsToEveryOne()
    {
        OnStaThread(gfx =>
        {
            using var clips = Clips(20);
            using var line = clips.Line(gfx);
            var style = StyleFor(Scale, LookFor(dark: true));
            var width = FolderFlyout.ListWidth(Scale);
            var box = PopupPaint.Rect(0, 0, width, Rows(3));

            var laid = line.Scrolled(box, style, WidgetSurface.List);
            Assert.True(laid.Scrolls, "twenty items do not fit three rows");
            Assert.Equal(line.ListHeight(width, style), laid.Content, 1);
            Assert.True(laid.Head > 0 && laid.Head < Rows(3) / 2, $"the header stays: {laid.Head:0} px");
            var pieces = ((ILinePieces)line).LinePieces(laid.Moved, style);
            Assert.DoesNotContain(pieces, piece => piece.Name == "more");
            Assert.Equal(20, pieces.Count(piece => piece.Name.StartsWith("text ", StringComparison.Ordinal)));

            // At its start: a press just under the header is the first item.
            var first = new Vector2(width / 2, laid.Head + style.Px(12));
            Assert.Equal(clips.Store.Items[0].Id, line.TargetOf(line.TargetOn(first, box, style, WidgetSurface.List)).Item);

            // Scrolled to its end: the foot is the last item, and the header has not moved.
            var head = Band(line, box, style, 0, laid.Head);
            line.ListScroll = float.MaxValue;
            var end = line.Scrolled(box, style, WidgetSurface.List);
            Assert.Equal(end.Most, end.Offset, 1);
            var last = ((ILinePieces)line).LinePieces(end.Moved, style).Single(piece => piece.Name == "text 19").Box;
            Assert.True(last.bottom <= box.bottom + 0.5f && last.top >= end.Window.top, "scrolled to its end, the last item shows");
            var foot = new Vector2(width / 2, (last.top + last.bottom) / 2);
            Assert.Equal(clips.Store.Items[^1].Id, line.TargetOf(line.TargetOn(foot, box, style, WidgetSurface.List)).Item);
            Assert.Equal(head, Band(line, box, style, 0, laid.Head));
            // The header is still the header's to press: "Clear all" where it was.
            var clear = ((ILinePieces)line).LinePieces(laid.Still, style).Single(piece => piece.Name == "clear all").Box;
            var middle = new Vector2((clear.left + clear.right) / 2, (clear.top + clear.bottom) / 2);
            Assert.Equal(ClipboardLine.Spot.Clear, line.TargetOf(line.TargetOn(middle, box, style, WidgetSurface.List)).Spot);
        });
    }

    /// <summary>
    /// The foot stays at the line's bottom as the head stays at its top (2026-10-11): the items show
    /// between the two, the foot is drawn and read in the content scrolled to its end, and a press on
    /// it lands there at any offset.
    /// </summary>
    [Fact]
    public void TheFootStaysAtTheBottom()
    {
        var box = PopupPaint.Rect(0, 100, 300, 200);
        foreach (var offset in new[] { 0f, 50f, 200f })
        {
            var laid = new ListScrolled(box, 400, 40, offset, 30);
            Assert.Equal(200, laid.Most);
            Assert.Equal(140, laid.Window.top);
            Assert.Equal(270, laid.Window.bottom);
            Assert.Equal(270, laid.FootBand.top);
            Assert.Equal(300, laid.FootBand.bottom);
            // The content's end on the line's bottom, wherever the items are.
            Assert.Equal(-100, laid.End.top);
            Assert.Equal(300, laid.End.bottom);
            Assert.Equal(laid.End, laid.At(new System.Numerics.Vector2(10, 285)));
            Assert.Equal(laid.Still, laid.At(new System.Numerics.Vector2(10, 120)));
            Assert.Equal(laid.Moved, laid.At(new System.Numerics.Vector2(10, 200)));
        }
        // Without a foot, the window reaches the bottom as it always did.
        Assert.Equal(300, new ListScrolled(box, 400, 40, 0).Window.bottom);
    }

    /// <summary>
    /// §655: a side that stays (Windows search's pane beside its list): the items show through the window left of it, the
    /// side from the head's foot to the line's bottom is read in the content as it stands unscrolled, the scrollbar stands
    /// at the window's right; without one, the window is the line's width as it always was.
    /// </summary>
    [Fact]
    public void TheSideStaysBesideTheItems()
    {
        var box = PopupPaint.Rect(0, 100, 600, 200);
        foreach (var offset in new[] { 0f, 50f, 200f })
        {
            var laid = new ListScrolled(box, 400, 40, offset, Aside: 240);
            Assert.Equal(360, laid.Window.right);
            Assert.Equal(new D2D_RECT_F(360, 140, 600, 300), laid.AsideBand);
            // Over the side: the content unscrolled; beside it the items as they are scrolled; over the head the head.
            Assert.Equal(laid.Still, laid.At(new System.Numerics.Vector2(500, 250)));
            Assert.Equal(laid.Moved, laid.At(new System.Numerics.Vector2(100, 250)));
            Assert.Equal(laid.Still, laid.At(new System.Numerics.Vector2(100, 120)));
            Assert.True(laid.Thumb(StyleFor(1, LookFor(dark: true))).right <= laid.Window.right);
        }
        Assert.Equal(600, new ListScrolled(box, 400, 40, 0).Window.right);
        Assert.Equal(600, new ListScrolled(box, 400, 40, 0).AsideBand.left);
    }

    /// <summary>
    /// The clipboard's items and the contacts' people stand on the list's gutter (2026-10-11: "the
    /// clipboard items have a little unnecessary space on the left ... i dont want this anywhere"):
    /// an item's words, or its glyph where it has one, begin there, and its plate reaches past it,
    /// as the mail's and the news' rows do.
    /// </summary>
    [UiFact]
    public void ItemRowsStandOnTheGutter()
    {
        OnStaThread(gfx =>
        {
            using var clips = Clips(4);
            using var line = clips.Line(gfx);
            using var people = People(3);
            using var contacts = people.Line(gfx);
            foreach (var scale in new[] { 1f, 1.5f })
            {
                var style = StyleFor(scale, LookFor(dark: true));
                var width = FolderFlyout.ListWidth(scale);
                var gutter = ListForm.Gutter(style);
                var box = PopupPaint.Rect(0, 0, width, line.ListHeight(width, style));
                var texts = ((ILinePieces)line).LinePieces(box, style).Where(piece => piece.Name.StartsWith("text ", StringComparison.Ordinal)).ToList();
                Assert.NotEmpty(texts);
                Assert.All(texts, piece => Assert.Equal(gutter, piece.Box.left, 1));
                var rows = PopupPaint.Rect(0, 0, width, contacts.ListHeight(width, style));
                var glyphs = ((ILinePieces)contacts).LinePieces(rows, style).Where(piece => piece.Name.StartsWith("glyph ", StringComparison.Ordinal)).ToList();
                Assert.NotEmpty(glyphs);
                Assert.All(glyphs, piece => Assert.Equal(gutter, piece.Box.left, 1));
            }
        });
    }

    /// <summary>
    /// Content that fits does not scroll, and a line of one row keeps its one-row reading rather than
    /// becoming a window onto a list.
    /// </summary>
    [UiFact]
    public void ALineThatFitsOrIsOneRowDoesNotScroll()
    {
        OnStaThread(gfx =>
        {
            using var clips = Clips(2);
            using var line = clips.Line(gfx);
            var style = StyleFor(Scale, LookFor(dark: true));
            var width = FolderFlyout.ListWidth(Scale);
            Assert.False(line.Scrolled(PopupPaint.Rect(0, 0, width, Rows(8)), style, WidgetSurface.List).Scrolls);

            using var many = Clips(20);
            using var short1 = many.Line(gfx);
            Assert.False(short1.Scrolled(PopupPaint.Rect(0, 0, width, Rows(1)), style, WidgetSurface.List).Scrolls);
        });
    }

    /// <summary>
    /// In an open folder the wheel over a line of items scrolls them, a notch at a time, until their
    /// end — and the next turn that way does nothing: the folder itself never scrolls (2026-10-12: "a
    /// folder should never show a scrollbar"); turned back, they scroll back.
    /// </summary>
    [UiFact]
    public void TheWheelScrollsALineToItsEndAndNoFurther()
    {
        WindowShots.OnHiddenDesktop("list-scroll-wheel", (gfx, instance) =>
        {
            using var clips = Clips(20);
            var holder = BarKind.FolderList.New();
            var cell = ListKind.Clipboard.New();
            cell.Height = LineHeight.Exactly(6);
            Assert.NotNull(Karpo.Core.Folders.Add(BarKind.FolderList, holder.Folder!, cell));
            ClipboardLine.Stand = clips.Store;
            FolderIsland folder;
            try
            {
                folder = new FolderIsland($"{Owner}/{holder.Id}", holder, gfx, new Config());
            }
            finally
            {
                ClipboardLine.Stand = null;
            }
            using var flyout = new FolderFlyout(gfx, instance, 0);
            flyout.Show(gfx, LookFor(dark: true), 1f, new POINT { x = 960, y = 1040 }, new RECT { right = 1920, bottom = 1040 }, Edge.Bottom, folder, editing: false);
            var line = Assert.IsType<ClipboardLine>(folder.Tiles[0].Island);
            var over = flyout.ScreenOfCard(0);

            Assert.Equal(0, line.ListScroll);
            Assert.False(flyout.WheelLine(over, 120), "at its start, a turn up is the folder's");
            Assert.True(flyout.WheelLine(over, -120), "a turn down scrolls the items");
            flyout.GlideDone();
            var one = line.ListScroll;
            Assert.True(one > 0);
            // A touchpad's small step moves them by as little.
            Assert.True(flyout.WheelLine(over, -12));
            flyout.GlideDone();
            Assert.InRange(line.ListScroll - one, 0.5f, one / 2);
            var turns = 0;
            while (flyout.WheelLine(over, -120) && turns++ < 200)
            {
                flyout.GlideDone();
            }
            Assert.True(turns < 200, "the items reach their end");
            var end = line.ListScroll;
            Assert.True(end > one);
            Assert.False(flyout.WheelLine(over, -120), "at their end, the next turn is nobody's");
            // And the wheel itself, at their end, moves nothing: the folder never scrolls (2026-10-12).
            var pieces = flyout.PiecesShown!.ToList();
            _ = flyout.Message(WM.WM_MOUSEWHEEL, unchecked((nuint)(uint)(-120 << 16)), (nint)((over.y << 16) | (over.x & 0xFFFF)));
            flyout.GlideDone();
            Assert.Equal(end, line.ListScroll);
            Assert.Equal(over, flyout.ScreenOfCard(0));
            Assert.Equal(pieces, flyout.PiecesShown);
            Assert.True(flyout.WheelLine(over, 120), "turned back, they scroll back");
            flyout.GlideDone();
            Assert.True(line.ListScroll < end);
            flyout.Hide();
            folder.Dispose();
        });
    }

    /// <summary>The pixels of a band of the line, drawn as it is now.</summary>
    private static byte[] Band(CellIsland line, D2D_RECT_F box, in TileStyle style, float top, float bottom)
    {
        var look = style;
        var width = box.right - box.left;
        var height = box.bottom - box.top;
        byte[] band = [];
        Bitmap(width, height, dc => line.PaintOn(dc, box, look, WidgetSurface.List), bitmap => band = Pixels(bitmap, (uint)MathF.Ceiling(width), (uint)top, (uint)MathF.Floor(bottom)));
        return band;
    }

    /// <summary>The bytes of rows <paramref name="from"/> to <paramref name="to"/> of <paramref name="bitmap"/>.</summary>
    private static byte[] Pixels(IWICBitmap* bitmap, uint width, uint from, uint to)
    {
        IWICBitmapLock* locked = null;
        try
        {
            var rect = new WICRect { X = 0, Y = (int)from, Width = (int)width, Height = (int)Math.Max(1, to - from) };
            ComUtil.Check(bitmap->Lock(&rect, (uint)WICBitmapLockFlags.WICBitmapLockRead, &locked));
            uint stride = 0;
            ComUtil.Check(locked->GetStride(&stride));
            uint size = 0;
            byte* data = null;
            ComUtil.Check(locked->GetDataPointer(&size, &data));
            var bytes = new byte[width * 4 * (uint)rect.Height];
            for (var y = 0; y < rect.Height; y++)
            {
                new ReadOnlySpan<byte>(data + (y * stride), (int)(width * 4)).CopyTo(bytes.AsSpan((int)(y * width * 4)));
            }
            return bytes;
        }
        finally
        {
            ComUtil.Release(ref locked);
        }
    }

    // --- Item styles -------------------------------------------------------------------------------

    /// <summary>
    /// One item style for every line of items: Compact makes a line shorter than Standard, Large
    /// taller, and a part left out takes its room with it — the clipboard's further lines, the mail's
    /// time — while a line nobody set draws as it always did.
    /// </summary>
    [UiFact]
    public void TheItemStyleSizesALinesItems()
    {
        OnStaThread(gfx =>
        {
            var style = StyleFor(Scale, LookFor(dark: true));
            var width = FolderFlyout.ListWidth(Scale);
            using var clips = Clips(6);
            float Height(Action<CellItem> set)
            {
                using var line = clips.Line(gfx, set);
                return line.ListHeight(width, style);
            }
            var standard = Height(_ => { });
            var compact = Height(item => item.Items = new ItemStyleOptions { Size = ItemSize.Compact });
            var large = Height(item => item.Items = new ItemStyleOptions { Size = ItemSize.Large });
            var flat = Height(item => item.Items = new ItemStyleOptions { Preview = false });
            output.WriteLine($"compact {compact:0}, standard {standard:0}, large {large:0}, no preview {flat:0}");
            Assert.True(compact < standard && standard < large, $"compact {compact}, standard {standard}, large {large}");
            Assert.True(flat < standard, "a copied text of several lines shows one without its preview");
            Assert.Equal(standard, Height(item => item.Items = new ItemStyleOptions()), 1);
        });
    }

    /// <summary>
    /// The item size is offered only where it changes the drawing (2026-10-29: "the item size for a lot
    /// of widgets gets ignored. remove it from the widgets if it does not make a difference anyways"):
    /// every kind whose options have the size, drawn with things to show at Compact, Standard and Large,
    /// draws each differently. The agenda's rows are the kit's (§401) and the removable devices' Large was
    /// their Standard, so neither offers it.
    /// </summary>
    [UiFact]
    public void AnItemSizeIsOfferedOnlyWhereItChangesTheDrawing()
    {
        ListGutterTests.StoodIn<Gfx>(SavedWeather.Fixed, OnStaThread, gfx =>
        {
            var style = StyleFor(Scale, LookFor(dark: true));
            var width = FolderFlyout.ListWidth(Scale);
            var height = Rows(6);
            var box = PopupPaint.Rect(0, 0, width, height);
            using var stage = new ListLinesTests.Stage(gfx);
            using var clips = Clips(6);
            using var people = People(6);
            // The mail, the clipboard and the contacts over stores of the test's own; every other kind in an
            // opened folder of its own on the gutter tests' stand-ins, so it has read what it shows.
            byte[] Drawn(ListKind kind, ItemSize size)
            {
                var items = size == ItemSize.Standard ? null : new ItemStyleOptions { Size = size };
                using CellIsland? own = kind switch
                {
                    ListKind.Mail => stage.Mail(gfx, items),
                    ListKind.Clipboard => clips.Line(gfx, cell => cell.Items = items),
                    ListKind.Contacts => people.Line(gfx, cell => cell.Items = items),
                    _ => null,
                };
                if (own is not null)
                {
                    return Band(own, box, style, 0, height);
                }
                var line = ListGutterTests.Made(kind);
                line.Items = items;
                var folder = ListFormPictures.Folder(gfx, made =>
                {
                    ListGutterTests.Before(made);
                    foreach (var tile in made.Tiles)
                    {
                        ScreenTimeLinesTests.Feed(tile.Island, ScreenTimeLinesTests.Fortnight());
                    }
                }, line);
                try
                {
                    return Band((CellIsland)folder.Tiles[0].Island, box, style, 0, height);
                }
                finally
                {
                    folder.Closed();
                    folder.Dispose();
                }
            }
            var sized = Enum.GetValues<ListKind>().Where(kind => kind.HasHeight() && OptionRows.Of(kind.New()).Any(row => row.Key == OptionRows.ItemSize)).ToList();
            var same = new List<string>();
            foreach (var kind in sized)
            {
                var standard = Drawn(kind, ItemSize.Standard);
                foreach (var size in new[] { ItemSize.Compact, ItemSize.Large })
                {
                    if (Drawn(kind, size).AsSpan().SequenceEqual(standard))
                    {
                        same.Add($"{kind}: {size} draws as Standard");
                    }
                }
            }
            output.WriteLine($"offered on {string.Join(", ", sized)}");
            Assert.True(sized.Count >= 9, $"only {sized.Count} kinds offer a size: {string.Join(", ", sized)}");
            Assert.True(same.Count == 0, string.Join("\n", same));
        });
    }

    /// <summary>
    /// The sheets: the mail, the clipboard and the notifications in lines shorter than their items —
    /// at their start, half-way and at their end, the pointer over them so the overlay scrollbar shows —
    /// and the clipboard and the mail in each item style.
    /// </summary>
    [UiFact]
    public void TheLinesArePictured()
    {
        OnStaThread(gfx =>
        {
            foreach (var dark in new[] { true, false })
            {
                var look = LookFor(dark);
                var style = StyleFor(Scale, look);
                var width = FolderFlyout.ListWidth(Scale);
                using var stage = new ListLinesTests.Stage(gfx);
                using var clips = Clips(12);
                using var clip = clips.Line(gfx);
                var lines = new (string Name, CellIsland Line)[]
                {
                    ("Mail", stage[ListKind.Mail]),
                    ("Clipboard", clip),
                    ("Notifications", stage[ListKind.NotificationsList]),
                };
                var height = Rows(5);
                var panels = new List<(string Caption, CellIsland Line, float Height, float Scroll, bool Pointer)>();
                foreach (var (name, line) in lines)
                {
                    var most = line.Scrolled(PopupPaint.Rect(0, 0, width, height), style, WidgetSurface.List).Most;
                    Assert.True(most > 0, $"{name} scrolls in five rows");
                    panels.Add(($"{name}: at its start", line, height, 0, false));
                    panels.Add(($"{name}: half-way, pointer on it", line, height, most / 2, true));
                    panels.Add(($"{name}: at its end", line, height, most, true));
                }
                Sheet(gfx, $"list-scroll-{(dark ? "dark" : "light")}.png", look, width, panels);

                var styled = new List<(string Caption, CellIsland Line, float Height, float Scroll, bool Pointer)>();
                var made = new List<CellIsland>();
                try
                {
                    foreach (var (caption, set) in new (string, ItemStyleOptions?)[]
                    {
                        ("Compact", new ItemStyleOptions { Size = ItemSize.Compact }),
                        ("Standard", null),
                        ("Large", new ItemStyleOptions { Size = ItemSize.Large }),
                        ("Standard: no preview, no pictures", new ItemStyleOptions { Preview = false, Pictures = false }),
                    })
                    {
                        var line = clips.Line(gfx, item => item.Items = set);
                        made.Add(line);
                        styled.Add(($"Clipboard: {caption}", line, Rows(6), 0, false));
                    }
                    foreach (var (caption, set) in new (string, ItemStyleOptions?)[]
                    {
                        ("Compact", new ItemStyleOptions { Size = ItemSize.Compact }),
                        ("Large, with previews", new ItemStyleOptions { Size = ItemSize.Large, Preview = true }),
                        ("Standard, no time", new ItemStyleOptions { Time = false }),
                    })
                    {
                        var copy = stage.Mail(gfx, set);
                        made.Add(copy);
                        styled.Add(($"Mail: {caption}", copy, Rows(6), 0, false));
                    }
                    Sheet(gfx, $"list-item-styles-{(dark ? "dark" : "light")}.png", look, width, styled);
                }
                finally
                {
                    foreach (var line in made)
                    {
                        line.Dispose();
                    }
                }
            }
        });
    }

    /// <summary>Each line on a panel of its own under its caption, drawn as the folder draws it — the pointer on it where said.</summary>
    private static void Sheet(Gfx gfx, string file, Look look, float width, IReadOnlyList<(string Caption, CellIsland Line, float Height, float Scroll, bool Pointer)> panels)
    {
        const float margin = 24;
        var style = StyleFor(Scale, look);
        var theme = FlyoutTheme.For(look);
        var caption = PopupPaint.Round(13 * Scale);
        var band = caption * 3;
        var columns = 3;
        var rows = (panels.Count + columns - 1) / columns;
        var tallest = panels.Max(panel => panel.Height);
        var sheetWidth = margin + (columns * (width + margin));
        var sheetHeight = margin + (rows * (band + tallest + margin));
        Bitmap(sheetWidth, sheetHeight, dc =>
        {
            var backdrop = look.Dark ? new DXGI_RGBA(0.20f, 0.24f, 0.30f, 1) : new DXGI_RGBA(0.72f, 0.78f, 0.86f, 1);
            dc->Clear(&backdrop);
            var ink = look.Dark ? new DXGI_RGBA(1, 1, 1, 0.9f) : new DXGI_RGBA(0, 0, 0, 0.9f);
            for (var index = 0; index < panels.Count; index++)
            {
                var (words, line, height, scroll, pointer) = panels[index];
                var left = margin + ((index % columns) * (width + margin));
                var top = margin + ((index / columns) * (band + tallest + margin));
                PopupPaint.Text(dc, gfx, words, caption, Weight.Semibold, ink, left, top, width);
                var box = PopupPaint.Rect(left, top + band, width, height);
                PopupPaint.Fill(dc, box, PopupPaint.Round(8 * Scale), PopupPaint.Ink(theme.Base));
                PopupPaint.Stroke(dc, box, PopupPaint.Round(8 * Scale), MathF.Max(1, MathF.Round(Scale)), PopupPaint.Ink(theme.Stroke));
                line.ListScroll = scroll;
                var own = PopupPaint.Rect(0, 0, width, height);
                line.HoverOn(pointer ? new Vector2(width - 2, height - 2) : null, own, style, WidgetSurface.List);
                var context = dc;
                Shifted(context, box.left, box.top, () => line.PaintOn(context, own, style, WidgetSurface.List));
                line.HoverOn(null, own, style, WidgetSurface.List);
                line.ListScroll = 0;
            }
        }, bitmap => Save(bitmap, Path.Combine(Pictures, file)));
    }

    // --- Stand-ins ---------------------------------------------------------------------------------

    /// <summary><paramref name="count"/> items of Windows' clipboard history, in a store of the test's own.</summary>
    private static ClipStage Clips(int count) => new(count);

    /// <summary><paramref name="count"/> people in a contacts store of the test's own.</summary>
    private static PeopleStage People(int count) => new(count);

    /// <summary>A clipboard store over a fake history, and lines over it.</summary>
    private sealed class ClipStage : IDisposable
    {
        public ClipStage(int count)
        {
            var fake = new FakeClipboard
            {
                HistoryEnabled = true,
                History = [.. Enumerable.Range(0, count).Select(index => new ClipView($"c{index}", ClipKind.Text,
                    index % 3 == 1 ? $"Copied text {index + 1}\nwith a second line\nand a third" : $"Copied text {index + 1}", null, false))],
            };
            Store = new ClipboardStore(fake);
            Store.Refresh();
            Store.Idle().GetAwaiter().GetResult();
            Store.Deliver();
        }

        public ClipboardStore Store { get; }

        public ClipboardLine Line(Gfx gfx, Action<CellItem>? set = null)
        {
            var cell = ListKind.Clipboard.New();
            cell.Id = "clipboard-" + Guid.NewGuid().ToString("N")[..6];
            set?.Invoke(cell);
            var line = new ClipboardLine(Config.IslandKey(Owner, cell.Id), cell, gfx, new Config(), Store);
            line.StandIn(ListWidgets.Of(ListKind.Clipboard));
            return line;
        }

        public void Dispose()
        {
        }
    }

    /// <summary>A contacts store answered with made-up people, and lines over it.</summary>
    private sealed class PeopleStage(int count) : IDisposable
    {
        private readonly ContactStore store = Answered(count);

        private static ContactStore Answered(int count)
        {
            var store = new ContactStore(new AccountSet([]));
            store.Answered([.. Enumerable.Range(0, count).Select(i => new Karpo.Accounts.Person($"p{i}", $"Person {i:00}", null, [$"person{i}@example.com"], []))]);
            return store;
        }

        public ContactsLine Line(Gfx gfx, Action<CellItem>? set = null)
        {
            var cell = ListKind.Contacts.New();
            cell.Id = "contacts-" + Guid.NewGuid().ToString("N")[..6];
            set?.Invoke(cell);
            var line = new ContactsLine(Config.IslandKey(Owner, cell.Id), cell, gfx, new Config(), store);
            line.StandIn(ListWidgets.Of(ListKind.Contacts));
            line.Visible(true);
            return line;
        }

        public void Dispose()
        {
        }
    }
}
