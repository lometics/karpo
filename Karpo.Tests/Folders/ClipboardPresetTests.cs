// The clipboard history is a preset (docs\widgets.md §101): what a click on the clipboard history
// entry gave — Win+V's panel, "Clear all" over a card per item, each to pin and delete, a click
// pasting it where the person was, and while Windows' own history is off why, with the way to its
// setting — is a folder holding the clipboard's line (ClipboardLine) over the premade clipboard
// actions, under the glyph the entry was. Every store here is a stand-in over a fake clipboard
// (ClipboardHistoryTests.FakeClipboard, ClipboardLine.Stand): no test reads or writes the person's
// clipboard, and none pastes: the paste is the dock's (Dock.Clipboard.cs), asked for here and never sent.
//
// Pictured beside the flyout it replaces (rendered before it went: clipboard-<karpo|windows|empty>-*.png):
// clipboard-preset-<dark|light>.png at 150 %, and the face on the bar, clipboard-face-<dark|light>.png.

using System.Numerics;
using Karpo.Dock;
using Karpo.Tests.Clipboard;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using static Karpo.Tests.Folders.ListFormPictures;
using FakeClipboard = Karpo.Tests.Clipboard.ClipboardHistoryTests.FakeClipboard;

namespace Karpo.Tests.Folders;

public sealed unsafe class ClipboardPresetTests
{
    private static readonly PresetMachine Machine = new(HasBattery: false, FixedDrives: []);

    /// <summary>The clipboard's preset folder, as the add list makes it.</summary>
    private static BarItem Holder() => Presets.Folder(Presets.Find(Presets.ClipboardId)!, Machine);

    private static readonly string[] Texts =
    [
        "https://learn.microsoft.com/en-us/windows/apps/develop/launch/launch-snipping-tool",
        "Meeting notes\r\n- the agenda for Thursday\r\n- the budget, which is late\r\n- hiring\r\n- anything else",
        "Karpo",
        "    var store = new ClipboardStore(fake);\r\n    store.Listen(true);",
    ];

    /// <summary>A screenshot-like picture: a sky over a field, with a sun, 480 by 270 (the flyout's own).</summary>
    private static byte[] Picture() => ClipboardHistoryTests.Dib(480, 270, 32, colour: (x, y) =>
    {
        var sun = ((x - 360) * (x - 360)) + ((y - 70) * (y - 70)) < 40 * 40;
        if (sun)
        {
            return (40, 200, 255, 255);
        }
        return y < 180
            ? ((byte)(230 - (y / 2)), (byte)(150 + (y / 4)), (byte)(60 + (y / 6)), (byte)255)
            : ((byte)40, (byte)(140 + ((x + y) % 20)), (byte)60, (byte)255);
    });

    /// <summary>Karpo's own history as copies would leave it, the newest first: text, a picture, a pinned word, more text.</summary>
    private static (ClipboardStore Store, FakeClipboard Fake) Karpos(int more = 0)
    {
        var fake = new FakeClipboard { HistoryEnabled = false };
        var store = new ClipboardStore(fake);
        store.Listen(true);
        void Copy(ClipSnapshot snapshot)
        {
            fake.Copy(snapshot);
            store.Idle().GetAwaiter().GetResult();
        }
        for (var index = 0; index < more; index++)
        {
            Copy(new ClipSnapshot(ClipMarkers.None, $"Older copy {index + 1}", null));
        }
        foreach (var text in Texts)
        {
            Copy(new ClipSnapshot(ClipMarkers.None, text, null));
        }
        Copy(new ClipSnapshot(ClipMarkers.None, null, Picture()));
        Copy(new ClipSnapshot(ClipMarkers.None, "Hello, world", null));
        store.Refresh();
        store.Pin(store.Items.Single(item => item.Text == "Karpo"), true);
        return (store, fake);
    }

    /// <summary>Windows' own history, as the fake gives it: text and a picture, no pin; read as a folder opening reads it.</summary>
    private static (ClipboardStore Store, FakeClipboard Fake) Windows()
    {
        var picture = DibPictures.Thumbnail(Picture(), ClipboardStore.ThumbnailWidth, ClipboardStore.ThumbnailHeight);
        var fake = new FakeClipboard
        {
            HistoryEnabled = true,
            History =
            [
                new ClipView("w1", ClipKind.Text, "Copied from Windows' own history", null, false),
                new ClipView("w2", ClipKind.Image, "", picture, false),
                new ClipView("w3", ClipKind.Text, Texts[1], null, false),
            ],
        };
        var store = new ClipboardStore(fake);
        store.Refresh();
        store.Idle().GetAwaiter().GetResult();
        store.Deliver();
        return (store, fake);
    }

    /// <summary>The preset's folder over <paramref name="store"/>.</summary>
    private static FolderIsland Made(Gfx gfx, ClipboardStore store, string? key = null, BarItem? holder = null)
    {
        holder ??= Holder();
        ClipboardLine.Stand = store;
        try
        {
            return new FolderIsland(key ?? $"{Owner}/{holder.Id}", holder, gfx, new Config());
        }
        finally
        {
            ClipboardLine.Stand = null;
        }
    }

    /// <summary>The line, alone, over <paramref name="store"/>.</summary>
    private static ClipboardLine Line(Gfx gfx, ClipboardStore store)
    {
        var cell = ListKind.Clipboard.New();
        cell.Id = "clipboard-" + Guid.NewGuid().ToString("N")[..6];
        var line = new ClipboardLine(Config.IslandKey(Owner, cell.Id), cell, gfx, new Config(), store);
        line.StandIn(ListWidgets.Of(ListKind.Clipboard));
        // "Copied" timed by nothing that runs on: a test sets its own clock where it times it.
        line.After = static (_, _) => { };
        return line;
    }

    private static TileStyle StyleFor(float scale, Look look) => FolderFlyout.StyleFor(scale, look, FlyoutTheme.For(look));

    /// <summary>The line as tall as it asks to be, at the list's own width.</summary>
    private static D2D_RECT_F Whole(ClipboardLine line, in TileStyle style)
    {
        var width = FolderFlyout.ListWidth(style.Scale);
        return PopupPaint.Rect(0, 0, width, line.ListHeight(width, style));
    }

    private static IReadOnlyList<LinePiece> Pieces(ClipboardLine line, D2D_RECT_F box, in TileStyle style) =>
        ((ILinePieces)line).LinePieces(box, style);

    private static Vector2 Middle(D2D_RECT_F box) => new((box.left + box.right) / 2, (box.top + box.bottom) / 2);

    /// <summary>The pointer onto <paramref name="piece"/>, and what is under it.</summary>
    private static (int Id, ClipboardLine.Target Target) Point(ClipboardLine line, D2D_RECT_F box, in TileStyle style, string piece)
    {
        var at = Middle(Assert.Single(Pieces(line, box, style), each => each.Name == piece).Box);
        line.CardHover(at, box, style, WidgetSurface.List);
        var id = line.CardTarget(at, box, style, WidgetSurface.List);
        return (id, line.TargetOf(id));
    }

    /// <summary>A click on <paramref name="piece"/>, as the folder gives it: the pointer onto it, then its number.</summary>
    private static IslandAction Click(ClipboardLine line, in TileStyle style, string piece)
    {
        var box = Whole(line, style);
        var (id, _) = Point(line, box, style, piece);
        Assert.NotEqual(0, id);
        return line.Click(id);
    }

    // --- The preset ------------------------------------------------------------------------------------

    /// <summary>
    /// The flyout, made of widgets: the clipboard's line, ten rows fixed as the person set it
    /// (2026-10-11), and under it in the same card the premade clipboard actions (its footer's "Clipboard settings") — under the
    /// clipboard face, the entry's glyph, which has nothing to set.
    /// </summary>
    [Fact]
    public void ThePresetIsTheLineOverItsFooterUnderTheGlyph()
    {
        var preset = Presets.Find(Presets.ClipboardId)!;
        Assert.Equal(FolderFaces.ClipboardId, preset.Face!.Kind);
        Assert.Equal(["clipboard", "actions"], preset.Items.Select(item => item.Form));
        Assert.Equal(LineHeight.Exactly(20), preset.Items[0].Height);
        Assert.Same(PremadeWidgets.ClipboardActions, preset.Items[^1].Premade);
        Assert.Equal(((string?)null, "clipboard"), PremadeWidgets.ClipboardActions.Actions);
        Assert.Equal("ms-settings:clipboard", SettingsActions.Find("clipboard")!.Uri);

        var folder = Holder().Folder!;
        Assert.Same(FolderFaces.Clipboard, folder.Face.Info);
        Assert.Contains(FolderFaces.Clipboard, FolderFaces.All);
        Assert.Empty(folder.Face.Info.Options);
        Assert.Equal(WidgetInfos.Clipboard.Glyph, folder.Face.Info.Glyph);
        Assert.Contains(FolderFaces.ClipboardId, FaceDrawings.Ids);
    }

    /// <summary>
    /// The entry is gone from the bar, its flyout height with it; the line is all there is, and its
    /// click is an act the folder forwards to the dock once it has gone.
    /// </summary>
    [Fact]
    public void TheEntryIsGoneAndTheLineActs()
    {
        Assert.Null(BarEntries.Find("clipboardHistory"));
        Assert.DoesNotContain("clipboardhistory", Enum.GetValues<BarKind>().Select(kind => kind.ToString().ToLowerInvariant()));
        Assert.DoesNotContain(WidgetInfos.All, info => info.Id == "clipboardHistory");
        var line = ListWidgets.Of(ListKind.Clipboard);
        Assert.Same(WidgetInfos.Clipboard, line.Info);
        Assert.Equal(CellAct.Action, line.Act);
        Assert.True(FolderFlyout.Forwards(new IslandAction(View: IslandView.App), line.Act));
        Assert.True(Config.Shape >= 36);
    }

    // --- The folder and the face -----------------------------------------------------------------------

    /// <summary>
    /// The face, as the entry did, hears the clipboard while it is on — so what was copied before the
    /// folder is first opened is there — and lets go when it is taken off or the folder goes; the line
    /// inside reads the same store.
    /// </summary>
    [UiFact]
    public void TheFaceHearsTheClipboardAndTheLineReadsTheSameStore()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var fake = new FakeClipboard { HistoryEnabled = false };
            var store = new ClipboardStore(fake);
            var holder = Holder();
            var folder = Made(gfx, store, holder: holder);
            Assert.True(folder.ShowsClipboard);
            Assert.Same(store, folder.Clipboard);
            Assert.True(store.Listening);
            var line = Assert.IsType<ClipboardLine>(folder.Tiles[0].Island);
            Assert.Same(store, line.Store);

            // Copied before the folder ever opened: there when it does.
            fake.Copy(new ClipSnapshot(ClipMarkers.None, "Copied early", null));
            store.Idle().GetAwaiter().GetResult();
            folder.Opened(gfx);
            Assert.Equal("Copied early", Assert.Single(store.Items).Text);
            folder.Closed();

            // Another face, and no line: nothing hears the clipboard.
            holder.Folder!.Face = FolderFace.Of(FolderFaces.IconId);
            holder.Folder.Lists = [[]];
            folder.Configure(holder, new Config());
            Assert.Null(folder.Clipboard);
            Assert.False(store.Listening);
            holder.Folder.Face = FolderFace.Of(FolderFaces.ClipboardId);
            folder.Configure(holder, new Config());
            Assert.True(store.Listening);
            folder.Dispose();
            Assert.False(store.Listening);
        });
    }

    /// <summary>The face is the glyph alone, one square cell, which never changes by itself; its words the entry's name.</summary>
    [UiFact]
    public void TheFaceIsTheGlyphInOneSquare()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var (store, _) = Karpos();
            using var island = Made(gfx, store);
            Assert.Equal(WidgetInfos.Clipboard.Title, island.Tooltip(0));
            foreach (var height in new[] { 28, 48, 72 })
            {
                foreach (var vertical in new[] { false, true })
                {
                    var config = new Config { Height = height };
                    var style = new IslandStyle(1.5f, vertical, Layout.CellPx(144, config), Layout.IconPx(144, config), LookFor(dark: true));
                    var (width, tall) = island.CellSize(0, style);
                    Assert.Equal(FaceWidthTests.GlyphButton(style), vertical ? tall : width, 0.5f);
                    store.ClearAll();
                    Assert.Equal((width, tall), island.CellSize(0, style));
                }
            }
            Assert.Equal(0, island.IntervalMs);
        });
    }

    // --- The line --------------------------------------------------------------------------------------

    /// <summary>
    /// Karpo's own history: the count and "Clear all" over a row per item, the newest first — text in
    /// its first lines, the picture as its thumbnail, the pinned one wearing its pin — and, as Windows'
    /// history is off, why and "Turn on". Each item's pin and delete show under the pointer alone.
    /// </summary>
    [UiFact]
    public void KarposOwnHistoryIsRowsWithTheNoteAndTurnOn()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var (store, _) = Karpos();
            using var line = Line(gfx, store);
            var style = StyleFor(1.5f, LookFor(dark: true));
            var box = Whole(line, style);
            var pieces = Pieces(line, box, style);
            Assert.Equal(S.DockClipboardCount(6), Assert.Single(pieces, piece => piece.Name == "count").Text);
            Assert.Single(pieces, piece => piece.Name == "clear all");
            Assert.Equal(ClipboardStore.HistoryOffWords, Assert.Single(pieces, piece => piece.Name == "note").Text);
            Assert.Single(pieces, piece => piece.Name == "turn on");
            Assert.Equal("Hello, world", Assert.Single(pieces, piece => piece.Name == "text 0").Text);
            Assert.Single(pieces, piece => piece.Name == "picture 1");
            Assert.Single(pieces, piece => piece.Name == "pinned 3");
            Assert.Equal(5, pieces.Count(piece => piece.Name.StartsWith("text ", StringComparison.Ordinal)));
            Assert.DoesNotContain(pieces, piece => piece.Name.StartsWith("pin ", StringComparison.Ordinal) || piece.Name.StartsWith("delete ", StringComparison.Ordinal));
            Assert.DoesNotContain(pieces, piece => piece.Name == "more");
            // Everything inside the line, between its gutters.
            Assert.All(pieces, piece => Assert.True(piece.Box.left >= box.left - 0.5f && piece.Box.right <= box.right + 0.5f && piece.Box.bottom <= box.bottom + 0.5f, piece.Name));

            // Under the pointer: that row's pin and delete, nobody else's.
            var (_, target) = Point(line, box, style, "text 2");
            Assert.Equal(ClipboardLine.Spot.Row, target.Spot);
            pieces = Pieces(line, box, style);
            Assert.Single(pieces, piece => piece.Name == "pin 2");
            Assert.Single(pieces, piece => piece.Name == "delete 2");
            Assert.DoesNotContain(pieces, piece => piece.Name == "pin 0");
        });
    }

    /// <summary>Windows' own history: its rows with delete and no pin — Windows gives a program none — and no note.</summary>
    [UiFact]
    public void WindowsOwnHistoryHasDeleteAloneAndNoNote()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var (store, fake) = Windows();
            Assert.Equal(ClipSource.Windows, store.Source);
            using var line = Line(gfx, store);
            var style = StyleFor(1f, LookFor(dark: false));
            var box = Whole(line, style);
            Point(line, box, style, "text 0");
            var pieces = Pieces(line, box, style);
            Assert.DoesNotContain(pieces, piece => piece.Name is "note" or "turn on" or "pin 0");
            Assert.Single(pieces, piece => piece.Name == "delete 0");
            Assert.Single(pieces, piece => piece.Name == "picture 1");

            // A click puts the item back through Windows' own history and says so; the folder stays.
            var action = Click(line, style, "text 0");
            Assert.Equal(IslandView.None, action.View);
            Assert.Equal(["w1"], fake.Restored);
            Assert.Empty(fake.Put);
            Assert.False(line.TakePaste());
            Assert.True(line.SaysCopied("w1"));

            // A delete takes it out of Windows' history, in place.
            Point(line, Whole(line, style), style, "text 2");
            Assert.Equal(default, Click(line, style, "delete 2"));
            Assert.Equal(["w3"], fake.Deleted);
            Assert.False(line.TakePaste());
        });
    }

    /// <summary>
    /// A click on a row puts that item itself on the clipboard, and the folder stays: the row wears the
    /// check and "Copied" where its buttons stand for <see cref="ClipboardLine.CopiedMs"/>, then is as
    /// it was, and the line is woken to be drawn again then; nothing is pasted (the user, 2026-10-11:
    /// "when clicking on a clipboard item it should show a copied text"). Pin, delete and "Clear all"
    /// act in place, ask for nothing, and the pinned stay through "Clear all".
    /// </summary>
    [UiFact]
    public void ARowCopiesAndSaysSoAndTheButtonsActInPlace()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var (store, fake) = Karpos();
            using var line = Line(gfx, store);
            var clock = 10_000L;
            var after = new List<int>();
            line.Ticks = () => clock;
            line.After = (_, ms) => after.Add(ms);
            var style = StyleFor(1.25f, LookFor(dark: true));

            var action = Click(line, style, "text 2");
            Assert.False(FolderFlyout.Forwards(action, CellAct.Action));
            Assert.Equal(Texts[3], Assert.Single(fake.Put).Text);
            Assert.False(line.TakePaste());
            var copied = store.Items.Single(item => item.Text == Texts[3]).Id;
            Assert.True(line.SaysCopied(copied));
            var pieces = Pieces(line, Whole(line, style), style);
            var row = Assert.Single(pieces, piece => piece.Name == "text 2").Box;
            var said = Assert.Single(pieces, piece => piece.Name == "copied 2");
            Assert.Equal(S.DockClipboardCopied, said.Text);
            Assert.Single(pieces, piece => piece.Name == "copied 2 check");
            // On the row's first line, at its end, where its buttons stood; and no buttons under it.
            Assert.True(said.Box.top >= row.top - 0.5f && said.Box.bottom <= row.bottom + 0.5f);
            Assert.DoesNotContain(pieces, piece => piece.Name is "delete 2" or "pin 2");
            Assert.True(Assert.Single(after) >= ClipboardLine.CopiedMs);

            // Its time over, the row is as it was.
            clock += ClipboardLine.CopiedMs;
            Assert.False(line.SaysCopied(copied));
            Assert.DoesNotContain(Pieces(line, Whole(line, style), style), piece => piece.Name.StartsWith("copied", StringComparison.Ordinal));

            // Pin the newest: it stays, pinned, and the row wears the pin.
            Point(line, Whole(line, style), style, "text 0");
            Assert.Equal(default, Click(line, style, "pin 0"));
            Assert.True(store.Items[0].Pinned);
            Assert.Single(Pieces(line, Whole(line, style), style), piece => piece.Name == "pinned 0");

            // Delete the second (the picture): one row fewer.
            Point(line, Whole(line, style), style, "picture 1");
            Assert.Equal(default, Click(line, style, "delete 1"));
            Assert.Equal(5, store.Items.Count);
            Assert.DoesNotContain(store.Items, item => item.Kind == ClipKind.Image);

            // Clear all: the two pinned stay, and "Clear all" has nothing more to clear.
            Assert.Equal(default, Click(line, style, "clear all"));
            Assert.Equal(["Hello, world", "Karpo"], store.Items.Select(item => item.Text));
            var box = Whole(line, style);
            var clear = Assert.Single(Pieces(line, box, style), piece => piece.Name == "clear all");
            Assert.Equal(0, line.CardTarget(Middle(clear.Box), box, style, WidgetSurface.List));
            Assert.Single(fake.Put);
            Assert.False(line.TakePaste());
        });
    }

    /// <summary>A click is matched by what it is on, never by its place: an item gone from under the pointer is no click on its neighbour.</summary>
    [UiFact]
    public void AClickIsTheItemNotItsPlace()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var (store, fake) = Karpos();
            using var line = Line(gfx, store);
            var style = StyleFor(1f, LookFor(dark: true));
            var box = Whole(line, style);
            var (id, target) = Point(line, box, style, "text 0");
            store.Delete(store.Items.Single(item => item.Id == target.Item));
            Assert.Equal(default, line.Click(id));
            Assert.Empty(fake.Put);
        });
    }

    /// <summary>"Turn on" opens Windows' clipboard settings, where the history is switched on; the folder stays for Settings to take the focus.</summary>
    [UiFact]
    public void TurnOnOpensWindowsClipboardSettings()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var opened = new List<string>();
            ClipboardLine.Opener = opened.Add;
            try
            {
                var (store, _) = Karpos();
                using var line = Line(gfx, store);
                Assert.Equal(default, Click(line, StyleFor(1f, LookFor(dark: true)), "turn on"));
                Assert.Equal([ClipboardLine.SettingsPage], opened);
            }
            finally
            {
                ClipboardLine.Opener = null;
            }
        });
    }

    /// <summary>
    /// The keys (§471): the folder's walk the line, its every button and row a stop (LineTargets). On a row,
    /// Enter is kept — its repeats too — and nothing is chosen until it is let go, when the folder clicks it
    /// (no part: the row the keys are on) and the item goes on the clipboard, to be pasted. Space the same. On
    /// a row's pin, Enter is the folder's: the pin's click, in place.
    /// </summary>
    [UiFact]
    public void TheKeysChooseARowWhenLetGoAndPressItsButtons()
    {
        Offscreen.OnStaThread(gfx =>
        {
            foreach (var key in new[] { VK.VK_RETURN, VK.VK_SPACE })
            {
                var (store, fake) = Karpos();
                using var line = Line(gfx, store);
                var style = StyleFor(1f, LookFor(dark: true));
                var box = Whole(line, style);
                line.PaintCard(null, box, style, WidgetSurface.List);
                var stops = line.LineTargets(box, style).Select(stop => stop.Target).ToList();
                int Stop(ClipboardLine.Spot spot, int item) => stops.Single(stop => line.TargetOf(stop) == new ClipboardLine.Target(spot, store.Items[item].Id));

                // Every row and its pin and delete is a stop, and "Clear all".
                Assert.Contains(stops, stop => line.TargetOf(stop).Spot == ClipboardLine.Spot.Clear);
                foreach (var spot in new[] { ClipboardLine.Spot.Row, ClipboardLine.Spot.Pin, ClipboardLine.Spot.Delete })
                {
                    Assert.Equal(store.Items.Count, stops.Count(stop => line.TargetOf(stop).Spot == spot));
                }

                // Held on the second row: kept, repeats and all, and nothing chosen yet; another key let go is nothing.
                line.KeyedPart = Stop(ClipboardLine.Spot.Row, 1);
                Assert.True(line.KeyOn(key, WidgetSurface.List));
                Assert.True(line.KeyOn(key, WidgetSurface.List));
                Assert.True(line.KeyOn(VK.VK_DOWN, WidgetSurface.List));
                Assert.False(line.KeyUpOn(VK.VK_DOWN, WidgetSurface.List));
                Assert.Empty(fake.Put);

                // Let go: the folder clicks what the keys are on.
                Assert.True(line.KeyUpOn(key, WidgetSurface.List));
                var action = line.Click(0);
                Assert.True(FolderFlyout.Forwards(action, CellAct.Action));
                Assert.Equal(ClipKind.Image, Assert.Single(fake.Put).Kind);
                Assert.True(line.TakePaste());

                // On the first row's pin: the key is the folder's, whose click pins it in place.
                var first = store.Items[0].Id;
                var pin = Stop(ClipboardLine.Spot.Pin, 0);
                line.KeyedPart = pin;
                Assert.False(line.KeyOn(key, WidgetSurface.List));
                Assert.False(FolderFlyout.Forwards(line.Click(pin), CellAct.Action));
                Assert.True(store.Items.Single(item => item.Id == first).Pinned);
            }
        });
    }

    /// <summary>
    /// Held short it shows whole rows from the top, and the rest scroll under its header rather than
    /// being counted (ListScroll.cs): laid out as tall as every item, nothing says how many more; on one
    /// row the glyph, the count and "Clear all"; with nothing copied it says so.
    /// </summary>
    [UiFact]
    public void HeldShortItShowsWholeRowsAndScrollsToTheRest()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var (store, _) = Karpos(more: 6);
            using var line = Line(gfx, store);
            var style = StyleFor(1.5f, LookFor(dark: true));
            var width = FolderFlyout.ListWidth(style.Scale);
            Assert.True(line.ListHeight(width, style) > LineRows.Px(FolderOptions.MostCells, style) / 2);
            foreach (var rows in new[] { 5, 6, 8, 10 })
            {
                var box = PopupPaint.Rect(0, 0, width, LineRows.Px(rows, style));
                var pieces = Pieces(line, box, style);
                Assert.All(pieces, piece => Assert.True(piece.Box.top >= -0.5f && piece.Box.bottom <= box.bottom + 0.5f, $"{rows} rows: {piece.Name}"));
                Assert.DoesNotContain(pieces, piece => piece.Name == "more");
                var laid = line.Scrolled(box, style, WidgetSurface.List);
                Assert.True(laid.Scrolls, $"{rows} rows hold fewer than all");
                var every = Pieces(line, laid.Moved, style);
                Assert.Equal(store.Items.Count, every.Count(piece => piece.Name.StartsWith("text ", StringComparison.Ordinal) || piece.Name.StartsWith("picture ", StringComparison.Ordinal)));
            }

            var one = PopupPaint.Rect(0, 0, width, LineRows.Px(1, style));
            var single = Pieces(line, one, style);
            Assert.Equal(S.DockClipboardCount(store.Items.Count), Assert.Single(single, piece => piece.Name == "count").Text);
            Assert.Single(single, piece => piece.Name == "clear all");
            Assert.DoesNotContain(single, piece => piece.Name.StartsWith("text ", StringComparison.Ordinal));

            using var empty = Line(gfx, new ClipboardStore(new FakeClipboard { HistoryEnabled = false }));
            var nothing = Pieces(empty, Whole(empty, style), style);
            // §388: nothing copied is the kit's Status.
            Assert.Equal(S.DockClipboardNone, Assert.Single(nothing, piece => piece.Name == "status").Text);
            Assert.DoesNotContain(nothing, piece => piece.Name == "clear all");
        });
    }

    /// <summary>Opening the folder reads Windows' history afresh, as the flyout's opening did; closing reads nothing.</summary>
    [UiFact]
    public void OpeningReadsWindowsHistoryAgain()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var (store, fake) = Windows();
            using var line = Line(gfx, store);
            var reads = fake.Reads;
            fake.History = [new ClipView("w9", ClipKind.Text, "Newer", null, false)];
            line.Visible(true);
            store.Idle().GetAwaiter().GetResult();
            store.Deliver();
            Assert.Equal("Newer", Assert.Single(store.Items).Text);
            var style = StyleFor(1f, LookFor(dark: true));
            line.PaintCard(null, Whole(line, style), style, WidgetSurface.List);
            line.Visible(false);
            Assert.Equal(reads, fake.Reads);
        });
    }

    // --- Pictures --------------------------------------------------------------------------------------

    /// <summary>
    /// The preset as it opens at 150 %, to stand beside clipboard-*.png of the flyout: Karpo's own
    /// history with its note, a row under the pointer, Windows' own, the keys on a row, nothing copied,
    /// held to five rows, and at 225 % text.
    /// </summary>
    [UiFact]
    public void ClipboardPresetPictures()
    {
        foreach (var dark in new[] { true, false })
        {
            Offscreen.OnStaThread(gfx =>
            {
                var folders = new List<FolderIsland>();
                try
                {
                    var panels = new List<(string, Look, FolderIsland, FolderPlan)>();
                    void Add(string caption, ClipboardStore store, Action<ClipboardLine, TileStyle>? set = null, float text = 1f, int? rows = null)
                    {
                        var look = LookFor(dark, text);
                        var holder = Holder();
                        if (rows is { } held)
                        {
                            holder.Folder!.Lists[0][0].Island.Height = LineHeight.Exactly(held * LineHeight.Row);
                        }
                        var island = Made(gfx, store, $"{Owner}/{holder.Id}-{panels.Count}", holder);
                        folders.Add(island);
                        island.Opened(gfx);
                        store.Idle().GetAwaiter().GetResult();
                        store.Deliver();
                        var line = (ClipboardLine)island.Tiles[0].Island;
                        var style = StyleFor(1.5f, look);
                        set?.Invoke(line, style);
                        panels.Add((caption, look, island, Plan(island, look, 1.5f).Plan));
                    }

                    Add("Karpo's own history", Karpos().Store);
                    Add("A row under the pointer", Karpos().Store, (line, style) => Point(line, Whole(line, style), style, "text 2"));
                    Add("A row pressed", Karpos().Store, (line, style) =>
                    {
                        var box = Whole(line, style);
                        var at = Middle(Assert.Single(Pieces(line, box, style), each => each.Name == "text 2").Box);
                        line.CardHover(at, box, style, WidgetSurface.List);
                        Assert.False(line.CardPress(at, box, style, WidgetSurface.List));
                    });
                    Add("A row just copied", Karpos().Store, (line, style) =>
                    {
                        line.Ticks = static () => 0;
                        line.After = static (_, _) => { };
                        Click(line, style, "text 2");
                        Point(line, Whole(line, style), style, "text 0");
                    });
                    Add("Windows' own history", Windows().Store, (line, style) => Point(line, Whole(line, style), style, "picture 1"));
                    Add("The keys on a row", Karpos().Store, (line, style) =>
                    {
                        // The folder's keys on the second row: lit, its buttons showing (the ring is the folder's).
                        var box = Whole(line, style);
                        line.KeyedPart = line.LineTargets(box, style).Select(stop => stop.Target)
                            .Where(stop => line.TargetOf(stop).Spot == ClipboardLine.Spot.Row).Skip(1).First();
                    });
                    Add("Nothing copied", new ClipboardStore(new FakeClipboard { HistoryEnabled = false }));
                    Add("Held to five rows", Karpos(more: 4).Store, rows: 5);
                    Add("225 % text", Karpos().Store, text: 2.25f);
                    Sheet(gfx, $"clipboard-preset-{(dark ? "dark" : "light")}.png", 1.5f, panels);
                }
                finally
                {
                    foreach (var island in folders)
                    {
                        island.Closed();
                        island.Dispose();
                    }
                }
            });
        }
    }

    /// <summary>The face on bars of 40 and 48 DIP and at 150 % — clipboard-face-&lt;dark|light&gt;.png.</summary>
    [UiFact]
    public void ClipboardFacePictures()
    {
        foreach (var dark in new[] { true, false })
        {
            Offscreen.OnStaThread(gfx =>
            {
                var look = LookFor(dark);
                var theme = FlyoutTheme.For(look);
                using var island = Made(gfx, Karpos().Store);
                var styles = new[] { (40, 96u), (48, 96u), (48, 144u) }
                    .Select(pair => new IslandStyle(pair.Item2 / 96f, false, Layout.CellPx(pair.Item2, new Config { Height = pair.Item1 }), Layout.IconPx(pair.Item2, new Config { Height = pair.Item1 }), look))
                    .ToList();
                const float gap = 12;
                var width = gap + styles.Sum(style => island.CellSize(0, style).Width + gap);
                var height = gap + styles.Max(style => island.CellSize(0, style).Height) + gap;
                Offscreen.Bitmap(width, height, dc =>
                {
                    PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, width, height), 0, PopupPaint.Ink(theme.Base));
                    var x = gap;
                    foreach (var style in styles)
                    {
                        var (w, h) = island.CellSize(0, style);
                        PopupPaint.Fill(dc, PopupPaint.Rect(x, gap, w, h), 4 * style.Scale, PopupPaint.Ink(theme.CardBackground));
                        Offscreen.Shifted(dc, x, gap, () => island.PaintCell(dc, 0, style, w, h));
                        x += w + gap;
                    }
                }, bitmap => Offscreen.Save(bitmap, Path.Combine(Offscreen.Pictures, $"clipboard-face-{(dark ? "dark" : "light")}.png")));
            });
        }
    }
}
