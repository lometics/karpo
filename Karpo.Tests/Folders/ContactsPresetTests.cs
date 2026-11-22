// The contacts are a preset (docs\widgets.md §102): what a click on the contacts entry gave — a
// search box over everyone in the person's accounts, typing narrowing them at once, how many there are
// under the box — is a folder holding the contacts' line (ContactsLine), under the glyph the entry was.
// A person's details (2026-10-11): Email and Call under the pointer, a row opened in place with a line
// per detail, Copy, the right click's rows, Ctrl+C. Every store here is filled as a
// read of the accounts would fill it (ContactStore.Answered) over an empty account set, or over a
// FakeReader: no test reaches an account, and none writes the person's clipboard (ContactsLine.Copier).
//
// Pictured beside the flyout it replaces (rendered before it went: contacts-flyout-*.png):
// contacts-preset-<dark|light>.png and contacts-details-<dark|light>.png at 150 %, and the face on the bar, contacts-face-<dark|light>.png.
// The people's pictures (§457): contacts-photos-<dark|light>.png.

using System.Numerics;
using Karpo.Accounts;
using Karpo.Dock;
using Karpo.Dock.Pim;
using Karpo.Tests.Islands;
using Karpo.Tests.Pim;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using static Karpo.Tests.Folders.ListFormPictures;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Folders;

public sealed unsafe class ContactsPresetTests
{
    private static readonly PresetMachine Machine = new(HasBattery: false, FixedDrives: []);

    /// <summary>The contacts' preset folder, as the add list makes it.</summary>
    private static BarItem Holder() => Presets.Folder(Presets.Find(Presets.ContactsId)!, Machine);

    /// <summary>Five people, as the store sorts them: an address, a number alone, an organization alone.</summary>
    private static List<Person> Five() =>
    [
        new("a", "Adrian Hager", "Contoso", ["adrian.hager@contoso.com"], ["+41 79 123 45 67"]),
        new("b", "Beatrice Keller", null, ["bea@example.com"], []),
        new("c", "Carlos Mendes", null, [], ["+41 44 555 01 02"]),
        new("d", "Dana Schläpfer", "Fabrikam", [], []),
        new("e", "Elif Yilmaz", null, ["elif.yilmaz@fabrikam.com"], []),
    ];

    /// <summary><paramref name="count"/> people, every one with an address.</summary>
    private static List<Person> Many(int count) =>
        [.. Enumerable.Range(0, count).Select(i => new Person($"p{i}", $"Person {i:00}", null, [$"person{i}@example.com"], []))];

    /// <summary>A store as a read that found <paramref name="people"/> leaves it; nothing is due after it.</summary>
    private static ContactStore Answered(IReadOnlyList<Person> people)
    {
        var store = new ContactStore(new AccountSet([]));
        store.Answered(people);
        return store;
    }

    /// <summary>The preset's folder over <paramref name="store"/>.</summary>
    private static FolderIsland Made(Gfx gfx, ContactStore store, string? key = null, BarItem? holder = null)
    {
        holder ??= Holder();
        ContactsLine.Stand = store;
        try
        {
            return new FolderIsland(key ?? $"{Owner}/{holder.Id}", holder, gfx, new Config());
        }
        finally
        {
            ContactsLine.Stand = null;
        }
    }

    /// <summary>The line, alone, over <paramref name="store"/>, shown as an opening folder shows it.</summary>
    private static ContactsLine Line(Gfx gfx, ContactStore store, bool shown = true, ItemStyleOptions? items = null)
    {
        var cell = ListKind.Contacts.New();
        cell.Id = "contacts-" + Guid.NewGuid().ToString("N")[..6];
        cell.Items = items;
        var line = new ContactsLine(Config.IslandKey(Owner, cell.Id), cell, gfx, new Config(), store);
        line.StandIn(ListWidgets.Of(ListKind.Contacts));
        if (shown)
        {
            line.Visible(true);
        }
        return line;
    }

    private static TileStyle StyleFor(float scale, Look look) => FolderFlyout.StyleFor(scale, look, FlyoutTheme.For(look));

    /// <summary>The line as tall as it asks to be, at the list's own width.</summary>
    private static D2D_RECT_F Whole(ContactsLine line, in TileStyle style)
    {
        var width = FolderFlyout.ListWidth(style.Scale);
        return PopupPaint.Rect(0, 0, width, line.ListHeight(width, style));
    }

    private static IReadOnlyList<LinePiece> Pieces(ContactsLine line, D2D_RECT_F box, in TileStyle style) =>
        ((ILinePieces)line).LinePieces(box, style);

    private static Vector2 Middle(D2D_RECT_F box) => new((box.left + box.right) / 2, (box.top + box.bottom) / 2);

    /// <summary>The names the rows show, in order.</summary>
    private static List<string> Names(ContactsLine line, in TileStyle style) =>
        [.. Pieces(line, Whole(line, style), style).Where(piece => piece.Name.StartsWith("name ", StringComparison.Ordinal)).Select(piece => piece.Text)];

    /// <summary>Types <paramref name="text"/> as the folder hands it on: a character at a time.</summary>
    private static void Type(ContactsLine line, string text)
    {
        foreach (var c in text)
        {
            Assert.True(line.CharOn(c, WidgetSurface.List));
        }
    }

    /// <summary>A click on <paramref name="piece"/>, as the folder gives it: the pointer onto it, then its number.</summary>
    private static IslandAction Click(ContactsLine line, in TileStyle style, string piece)
    {
        var box = Whole(line, style);
        var at = Middle(Assert.Single(Pieces(line, box, style), each => each.Name == piece).Box);
        line.CardHover(at, box, style, WidgetSurface.List);
        Assert.False(line.PressOn(at, box, style, WidgetSurface.List), "a press on a row is the folder's");
        var id = line.CardTarget(at, box, style, WidgetSurface.List);
        Assert.NotEqual(0, id);
        return line.Click(id);
    }

    /// <summary>What the line puts on the clipboard, while <paramref name="body"/> runs.</summary>
    private static List<string> Copies(Action body)
    {
        var copied = new List<string>();
        ContactsLine.Copier = copied.Add;
        try
        {
            body();
        }
        finally
        {
            ContactsLine.Copier = null;
        }
        return copied;
    }

    // --- The preset ------------------------------------------------------------------------------------

    /// <summary>
    /// The flyout, made of widgets: the contacts' line, as tall as its people up to the most a line
    /// may be, and nothing under it — the flyout's footer had no link — under the contacts face, the
    /// entry's glyph, which has nothing to set.
    /// </summary>
    [Fact]
    public void ThePresetIsTheLineUnderTheGlyph()
    {
        var preset = Presets.Find(Presets.ContactsId)!;
        Assert.Equal(FolderFaces.ContactsId, preset.Face!.Kind);
        Assert.Equal(["contacts"], preset.Items.Select(item => item.Form));
        Assert.Equal(LineHeight.Automatic(FolderOptions.MostCells), preset.Items[0].Height);

        var folder = Holder().Folder!;
        Assert.Same(FolderFaces.Contacts, folder.Face.Info);
        Assert.Contains(FolderFaces.Contacts, FolderFaces.All);
        Assert.Empty(folder.Face.Info.Options);
        Assert.Equal(WidgetInfos.Contacts.Glyph, folder.Face.Info.Glyph);
        Assert.Contains(FolderFaces.ContactsId, FaceDrawings.Ids);
    }

    /// <summary>
    /// The entry is gone from the bar, its flyout height with it; the line is all there is, a press
    /// on its box and a drag in it its own, and its click acts in place (the folder stays).
    /// </summary>
    [Fact]
    public void TheEntryIsGoneAndTheLineActsInPlace()
    {
        Assert.Null(BarEntries.Find("contacts"));
        Assert.DoesNotContain("contacts", Enum.GetValues<BarKind>().Select(kind => kind.ToString().ToLowerInvariant()));
        var line = ListWidgets.Of(ListKind.Contacts);
        Assert.Same(WidgetInfos.Contacts, line.Info);
        Assert.Equal("contacts", line.Info.Id);
        Assert.Equal(CellAct.Action, line.Act);
        Assert.True(line.Interaction.HasFlag(Interaction.Drag));
        Assert.True(Config.Shape >= 37);
        Assert.Equal([WidgetAct.ContactEmail, WidgetAct.ContactCall, WidgetAct.ContactCopy, WidgetAct.ManageAccounts], TheDock.ActsFor(ListKind.Contacts));
    }

    // --- The face --------------------------------------------------------------------------------------

    /// <summary>The face is the glyph alone, one square cell, which never changes by itself; its words the entry's name.</summary>
    [UiFact]
    public void TheFaceIsTheGlyphInOneSquare()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var store = Answered(Five());
            using var island = Made(gfx, store);
            Assert.IsType<ContactsLine>(island.Tiles[0].Island);
            Assert.Equal(WidgetInfos.Contacts.Title, island.Tooltip(0));
            foreach (var height in new[] { 28, 48, 72 })
            {
                foreach (var vertical in new[] { false, true })
                {
                    var config = new Config { Height = height };
                    var style = new IslandStyle(1.5f, vertical, Layout.CellPx(144, config), Layout.IconPx(144, config), LookFor(dark: true));
                    var (width, tall) = island.CellSize(0, style);
                    Assert.Equal(FaceWidthTests.GlyphButton(style), vertical ? tall : width, 0.5f);
                    store.Answered([]);
                    Assert.Equal((width, tall), island.CellSize(0, style));
                }
            }
            Assert.Equal(0, island.IntervalMs);
        });
    }

    // --- The line --------------------------------------------------------------------------------------

    /// <summary>
    /// Open, the box has the keys — the folder hands it every key and character first — and every
    /// person is a row, the name over the address, else the number, else the organization, right
    /// under the box. Typing narrows them at once, a phone matched on its digits; nobody matching says so.
    /// </summary>
    [UiFact]
    public void TypingNarrowsTheRowsAtOnce()
    {
        Offscreen.OnStaThread(gfx =>
        {
            using var line = Line(gfx, Answered(Five()));
            var style = StyleFor(1f, LookFor(dark: true));
            Assert.True(line.TypingOn);
            Assert.True(line.Box.Focused);
            Assert.Equal(500, line.IntervalMs);
            Assert.Equal(["Adrian Hager", "Beatrice Keller", "Carlos Mendes", "Dana Schläpfer", "Elif Yilmaz"], Names(line, style));
            var pieces = Pieces(line, Whole(line, style), style);
            Assert.Equal(["adrian.hager@contoso.com", "bea@example.com", "+41 44 555 01 02", "Fabrikam", "elif.yilmaz@fabrikam.com"],
                pieces.Where(piece => piece.Name.StartsWith("detail ", StringComparison.Ordinal)).Select(piece => piece.Text));
            var all = line.ListHeight(FolderFlyout.ListWidth(style.Scale), style);

            Type(line, "schla");
            Assert.Equal("schla", line.Box.Text);
            Assert.Equal(["Dana Schläpfer"], Names(line, style));
            Assert.True(line.ListHeight(FolderFlyout.ListWidth(style.Scale), style) < all);

            line.Box.Text = "";
            Type(line, "4455");
            Assert.Equal(["Carlos Mendes"], Names(line, style));

            line.Box.Text = "";
            Type(line, "q");
            Assert.Empty(Names(line, style));
            Assert.Equal(S.DockContactsNoMatch("q"), Assert.Single(Pieces(line, Whole(line, style), style), piece => piece.Name == "nothing").Text);
            Assert.True(line.KeyOn(VK.VK_BACK, WidgetSurface.List));
            Assert.Equal("", line.Box.Text);
            Assert.Equal(5, Names(line, style).Count);
        });
    }

    /// <summary>
    /// The line says neither how many people there are nor whose they are (2026-10-31, the person:
    /// "contacts widget should not show the amount of contacts and the account from which they are
    /// shown"): one account's people, all or narrowed, are the box and the rows right under it.
    /// </summary>
    [UiFact]
    public void TheLineSaysNeitherHowManyNorWhose()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var reader = new FakeReader(AccountKind.Dav, "ann@example.com");
            reader.People.AddRange(Five());
            using var store = new ContactStore(new AccountSet([reader]));
            store.Refresh();
            store.Idle().GetAwaiter().GetResult();
            store.Deliver();
            using var line = Line(gfx, store);
            var style = StyleFor(1f, LookFor(dark: true));
            foreach (var typed in new[] { "", "a" })
            {
                line.Box.Text = "";
                Type(line, typed);
                var pieces = Pieces(line, Whole(line, style), style);
                Assert.NotEmpty(Names(line, style));
                Assert.DoesNotContain(pieces, piece => piece.Name == "count");
                Assert.DoesNotContain(pieces, piece => piece.Text.Contains("ann@example.com", StringComparison.Ordinal));
                // Nothing between the box and the first row but the row's room above it.
                var search = Assert.Single(pieces, piece => piece.Name == "search").Box;
                var first = Assert.Single(pieces, piece => piece.Name == "name 0").Box;
                Assert.DoesNotContain(pieces, piece => !piece.Behind && piece.Box.top >= search.bottom - 0.5f && piece.Box.bottom <= first.top + 0.5f);
            }
        });
    }

    /// <summary>
    /// A stand-in for a person's picture, <paramref name="side"/> px square: a head and shoulders, lighter,
    /// on a ground of the hue <paramref name="seed"/> picks — opaque, premultiplied, as ContactPhotos keeps one.
    /// </summary>
    internal static Pixels Face(int seed, int side = 64)
    {
        var data = new byte[side * side * 4];
        var hue = Math.Abs(seed * 47 % 360) / 60f;
        float Channel(float n)
        {
            var k = (n + hue) % 6;
            return 1 - Math.Max(0, Math.Min(Math.Min(k, 4 - k), 1));
        }
        var (r, g, b) = (0.35f + (0.4f * Channel(5)), 0.35f + (0.4f * Channel(3)), 0.35f + (0.4f * Channel(1)));
        for (var y = 0; y < side; y++)
        {
            for (var x = 0; x < side; x++)
            {
                var (u, v) = ((x + 0.5f) / side, (y + 0.5f) / side);
                var head = ((u - 0.5f) * (u - 0.5f)) + ((v - 0.4f) * (v - 0.4f)) < 0.04f;
                var shoulders = ((u - 0.5f) * (u - 0.5f) / 0.12f) + ((v - 1.05f) * (v - 1.05f) / 0.12f) < 1;
                var light = head || shoulders ? 1.45f : 1f;
                var at = ((y * side) + x) * 4;
                data[at] = (byte)Math.Min(255, b * light * 255);
                data[at + 1] = (byte)Math.Min(255, g * light * 255);
                data[at + 2] = (byte)Math.Min(255, r * light * 255);
                data[at + 3] = 255;
            }
        }
        return new Pixels { Data = data, Width = side, Height = side };
    }

    /// <summary>Five people, each with a picture's version: Adrian and Carlos are pictured (<see cref="Face"/>), the rest are not.</summary>
    private static ContactStore Pictured(IReadOnlyList<Person>? people = null)
    {
        var versioned = (people ?? Five()).Select(person => person with { Photo = "v1" }).ToList();
        var store = Answered(versioned);
        store.Pictured(versioned[0], Face(0));
        store.Pictured(versioned[2], Face(3));
        return store;
    }

    /// <summary>
    /// A person with a picture wears it in the leading column, a circle at the kit's Feature size where the
    /// glyph stood, on their name and the line under it; one without wears the glyph as before. With the
    /// item style's Pictures off, everyone wears the glyph.
    /// </summary>
    [UiFact]
    public void ARowWearsThePersonsPictureElseTheGlyph()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var style = StyleFor(1.5f, LookFor(dark: true));
            using var store = Pictured();
            using (var line = Line(gfx, store))
            {
                var pieces = Pieces(line, Whole(line, style), style);
                var photo = Assert.Single(pieces, piece => piece.Name == "photo 0").Box;
                Assert.DoesNotContain(pieces, piece => piece.Name == "glyph 0");
                Assert.Contains(pieces, piece => piece.Name == "photo 2");
                Assert.Contains(pieces, piece => piece.Name == "glyph 1");
                Assert.DoesNotContain(pieces, piece => piece.Name == "photo 1");

                // A square of the Feature size, before the name, on the name and its detail's middle.
                var side = MathF.Round(style.Text(LineGlyph.FeatureDip));
                Assert.Equal(side, photo.right - photo.left, 0.01f);
                Assert.Equal(side, photo.bottom - photo.top, 0.01f);
                var name = Assert.Single(pieces, piece => piece.Name == "name 0").Box;
                var detail = Assert.Single(pieces, piece => piece.Name == "detail 0").Box;
                Assert.True(photo.right <= name.left, "the picture stands before the name");
                Assert.True(photo.left >= ListForm.Gutter(style) - 0.5f, "and never in the gutter");
                Assert.InRange((photo.top + photo.bottom) / 2, name.top, detail.bottom);
            }
            using (var large = Line(gfx, store, items: new ItemStyleOptions { Size = ItemSize.Large }))
            {
                // Large items' pictures keep the kit's gap before the name: they touched it.
                var pieces = Pieces(large, Whole(large, style), style);
                var photo = Assert.Single(pieces, piece => piece.Name == "photo 0").Box;
                var name = Assert.Single(pieces, piece => piece.Name == "name 0").Box;
                Assert.True(photo.right + style.Px(LineSpace.GapDip) <= name.left + 0.5f, "a gap before the name");
            }
            using (var off = Line(gfx, store, items: new ItemStyleOptions { Pictures = false }))
            {
                var pieces = Pieces(off, Whole(off, style), style);
                Assert.DoesNotContain(pieces, piece => piece.Name.StartsWith("photo ", StringComparison.Ordinal));
                Assert.Contains(pieces, piece => piece.Name == "glyph 0");
            }
        });
    }

    /// <summary>
    /// Up and Down choose a row while the caret stays in the box, neither end wrapping; Enter opens
    /// it in place and Enter again closes it; Ctrl+C copies the address, the number where there is
    /// none, and nothing where there is neither.
    /// Escape and Tab are the folder's — Tab taking the keys from the box — and then the arrows walk
    /// the rows, past either end the folder's again, Space opening; a letter goes back into the box.
    /// </summary>
    [UiFact]
    public void TheArrowsChooseEnterOpensAndCtrlCCopies()
    {
        Offscreen.OnStaThread(gfx =>
        {
            using var line = Line(gfx, Answered(Five()));
            var style = StyleFor(1f, LookFor(dark: true));
            line.PaintCard(null, Whole(line, style), style, WidgetSurface.List);
            ContactsLine.ControlHeld = true;
            try
            {
                var copied = Copies(() =>
                {
                    // Nothing chosen: Enter opens nothing, Ctrl+C copies nothing.
                    Assert.True(line.KeyOn(VK.VK_RETURN, WidgetSurface.List));
                    Assert.Null(line.Open);
                    Assert.True(line.KeyOn('C', WidgetSurface.List));
                    Assert.True(line.KeyOn(VK.VK_DOWN, WidgetSurface.List));
                    Assert.Equal("Adrian Hager", line.Selected?.DisplayName);
                    Assert.True(line.KeyOn(VK.VK_UP, WidgetSurface.List));
                    Assert.Equal("Adrian Hager", line.Selected?.DisplayName);
                    Assert.True(line.KeyOn('C', WidgetSurface.List));
                    Assert.True(line.KeyOn(VK.VK_DOWN, WidgetSurface.List));
                    Assert.True(line.KeyOn(VK.VK_DOWN, WidgetSurface.List));
                    Assert.True(line.KeyOn('C', WidgetSurface.List));
                    Assert.True(line.KeyOn(VK.VK_DOWN, WidgetSurface.List));
                    Assert.True(line.KeyOn('C', WidgetSurface.List));
                    Assert.True(line.TypingOn, "the caret stays in the box");
                });
                // Dana has neither an address nor a number.
                Assert.Equal(["adrian.hager@contoso.com", "+41 44 555 01 02"], copied);
                Assert.Equal("Dana Schläpfer", line.Selected?.DisplayName);
                Assert.Null(line.Open);

                // Enter opens the row chosen in place, and closes it again.
                Assert.True(line.KeyOn(VK.VK_RETURN, WidgetSurface.List));
                Assert.Equal("Dana Schläpfer", line.Open?.DisplayName);
                Assert.Equal("Fabrikam", Assert.Single(Pieces(line, Whole(line, style), style), piece => piece.Name == "value 3.0").Text);
                Assert.True(line.KeyOn(VK.VK_RETURN, WidgetSurface.List));
                Assert.Null(line.Open);
            }
            finally
            {
                ContactsLine.ControlHeld = null;
            }

            // The last row is the end: Down stays on it.
            for (var index = 0; index < 8; index++)
            {
                line.KeyOn(VK.VK_DOWN, WidgetSurface.List);
            }
            Assert.Equal("Elif Yilmaz", line.Selected?.DisplayName);

            Assert.False(line.KeyOn(VK.VK_ESCAPE, WidgetSurface.List));
            Assert.True(line.TypingOn);
            Assert.False(line.KeyOn(VK.VK_TAB, WidgetSurface.List));
            Assert.False(line.TypingOn);

            // Without the box: the arrows walk the rows, and past the last the keys are the folder's.
            Assert.False(line.KeyOn(VK.VK_DOWN, WidgetSurface.List));
            Assert.Null(line.Selected);
            Assert.True(line.KeyOn(VK.VK_DOWN, WidgetSurface.List));
            Assert.Equal("Adrian Hager", line.Selected?.DisplayName);
            Assert.True(line.KeyOn(VK.VK_SPACE, WidgetSurface.List));
            Assert.Equal("Adrian Hager", line.Open?.DisplayName);
            Assert.False(line.KeyOn(VK.VK_UP, WidgetSurface.List));

            // A letter takes the box again; a control character never does.
            Assert.False(line.CharOn('\r', WidgetSurface.List));
            Assert.False(line.TypingOn);
            Assert.True(line.CharOn('e', WidgetSurface.List));
            Assert.True(line.TypingOn);
            Assert.Equal("e", line.Box.Text);

            // A press elsewhere in the folder lets the box go and keeps what was typed.
            line.StopTypingOn();
            Assert.False(line.TypingOn);
            Assert.Equal("e", line.Box.Text);
        });
    }

    /// <summary>
    /// A click on a row opens it in place — one at a time, a click on its name closing it — and the
    /// click is the person, not the place: a row that moved under a new search between the hover and
    /// the click is still the one that was pointed at. A press on the box is the line's: the caret
    /// goes there, the box has the keys.
    /// </summary>
    [UiFact]
    public void AClickOpensThePersonNotThePlace()
    {
        Offscreen.OnStaThread(gfx =>
        {
            using var line = Line(gfx, Answered(Five()));
            var style = StyleFor(1.5f, LookFor(dark: false));
            var closed = line.ListHeight(FolderFlyout.ListWidth(style.Scale), style);
            IslandAction action = default;
            Assert.Empty(Copies(() => action = Click(line, style, "name 1")));
            Assert.Equal(default, action);
            Assert.Equal("Beatrice Keller", line.Open?.DisplayName);
            Assert.True(line.ListHeight(FolderFlyout.ListWidth(style.Scale), style) > closed, "an open row is taller, and so is the line");

            // Another row: it opens and the first closes.
            Click(line, style, "name 0");
            Assert.Equal("Adrian Hager", line.Open?.DisplayName);
            Assert.DoesNotContain(Pieces(line, Whole(line, style), style), piece => piece.Name.StartsWith("value 1.", StringComparison.Ordinal));
            Click(line, style, "name 0");
            Assert.Null(line.Open);
            Assert.Equal(closed, line.ListHeight(FolderFlyout.ListWidth(style.Scale), style));

            var box = Whole(line, style);
            var at = Middle(Assert.Single(Pieces(line, box, style), piece => piece.Name == "name 4").Box);
            line.CardHover(at, box, style, WidgetSurface.List);
            var id = line.CardTarget(at, box, style, WidgetSurface.List);
            Assert.Equal("Elif Yilmaz", line.TargetOf(id).Person?.DisplayName);
            Type(line, "e");
            line.Click(id);
            Assert.Equal("Elif Yilmaz", line.Open?.DisplayName);

            // Narrowed away, the open row closes.
            line.Box.Text = "";
            Type(line, "Carlos");
            _ = Pieces(line, Whole(line, style), style);
            Assert.Null(line.Open);

            // A press on the box: the line's, and it keeps the keys.
            line.StopTypingOn();
            var search = Assert.Single(Pieces(line, Whole(line, style), style), piece => piece.Name == "search").Box;
            Assert.True(line.PressOn(Middle(search), Whole(line, style), style, WidgetSurface.List));
            line.ReleaseOn(WidgetSurface.List);
            Assert.True(line.TypingOn);
            Assert.Equal(0, line.CardTarget(Middle(search), Whole(line, style), style, WidgetSurface.List));
        });
    }

    /// <summary>
    /// At rest a row is the name and its detail and nothing more; under the pointer it shows Email and
    /// Call at its end, each only where the person has one — Adrian both, Beatrice Email, Carlos Call,
    /// Dana (an organization alone) neither. Email hands the dock mailto: once the folder has gone,
    /// Call tel:; the folder's click is the act that forwards it (CellAct.Action).
    /// </summary>
    [UiFact]
    public void UnderThePointerARowOffersEmailAndCall()
    {
        Offscreen.OnStaThread(gfx =>
        {
            using var line = Line(gfx, Answered(Five()));
            var style = StyleFor(1f, LookFor(dark: true));
            var rest = Pieces(line, Whole(line, style), style);
            Assert.DoesNotContain(rest, piece => piece.Name.StartsWith("email ", StringComparison.Ordinal) || piece.Name.StartsWith("call ", StringComparison.Ordinal) || piece.Name.StartsWith("copy ", StringComparison.Ordinal));

            List<string> Buttons(int row)
            {
                var box = Whole(line, style);
                line.CardHover(Middle(Assert.Single(Pieces(line, box, style), piece => piece.Name == $"name {row}").Box), box, style, WidgetSurface.List);
                return [.. Pieces(line, Whole(line, style), style).Select(piece => piece.Name).Where(name => name.StartsWith("email ", StringComparison.Ordinal) || name.StartsWith("call ", StringComparison.Ordinal))];
            }
            Assert.Equal(["call 0", "email 0"], Buttons(0));
            Assert.Equal(["email 1"], Buttons(1));
            Assert.Equal(["call 2"], Buttons(2));
            Assert.Empty(Buttons(3));

            Buttons(0);
            var action = Click(line, style, "email 0");
            Assert.Equal(IslandView.App, action.View);
            Assert.True(FolderFlyout.Forwards(action, ListWidgets.Of(ListKind.Contacts).Act));
            Assert.Equal("mailto:adrian.hager@contoso.com", line.Opening);
            Assert.Null(line.Open);
            var opened = new List<string>();
            line.Opened = opened.Add;
            line.OpenChosen();
            line.OpenChosen();
            Assert.Equal(["mailto:adrian.hager@contoso.com"], opened);

            Buttons(0);
            Assert.Equal(IslandView.App, Click(line, style, "call 0").View);
            line.OpenChosen();
            Assert.Equal("tel:+41791234567", opened[^1]);
        });
    }

    /// <summary>
    /// Open, a row lists every detail on a line of its own — each address, each number, the
    /// organization — with Copy at its end and Email or Call before it; Copy, or a click on the line,
    /// puts it on the clipboard and the line says "Copied" in the Copy button's place until the
    /// clipboard's moment has gone. The folder stays.
    /// </summary>
    [UiFact]
    public void AnOpenRowCopiesEachDetail()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var adrian = new Person("a", "Adrian Hager", "Contoso", ["adrian.hager@contoso.com", "adrian@hager.ch"], ["+41 79 123 45 67", "044 555 01 02"]);
            using var line = Line(gfx, Answered([adrian]));
            var style = StyleFor(1f, LookFor(dark: true));
            var now = 1000L;
            line.Ticks = () => now;
            var woken = new List<int>();
            line.After = (_, ms) => woken.Add(ms);
            Click(line, style, "name 0");
            var pieces = Pieces(line, Whole(line, style), style);
            Assert.Equal(["adrian.hager@contoso.com", "adrian@hager.ch", "+41 79 123 45 67", "044 555 01 02", "Contoso"],
                pieces.Where(piece => piece.Name.StartsWith("value 0.", StringComparison.Ordinal)).Select(piece => piece.Text));
            Assert.Equal(["call 0.2", "call 0.3", "copy 0.0", "copy 0.1", "copy 0.2", "copy 0.3", "copy 0.4", "email 0.0", "email 0.1"],
                pieces.Select(piece => piece.Name).Where(name => name.StartsWith("email ", StringComparison.Ordinal) || name.StartsWith("call ", StringComparison.Ordinal) || name.StartsWith("copy ", StringComparison.Ordinal)).Order(StringComparer.Ordinal));
            Assert.All(pieces.Where(piece => piece.Name.StartsWith("value ", StringComparison.Ordinal)), piece =>
                Assert.True(piece.Box.right <= Assert.Single(pieces, other => other.Name == "copy " + piece.Name[6..]).Box.left, $"{piece.Name} runs under its buttons"));

            var action = default(IslandAction);
            Assert.Equal(["044 555 01 02"], Copies(() => action = Click(line, style, "copy 0.3")));
            Assert.Equal(default, action);
            Assert.True(line.SaysCopied(adrian, "044 555 01 02"));
            var said = Pieces(line, Whole(line, style), style);
            Assert.Equal(S.DockClipboardCopied, Assert.Single(said, piece => piece.Name == "copied 0.3").Text);
            Assert.DoesNotContain(said, piece => piece.Name == "copy 0.3");
            Assert.Equal([ContactsLine.CopiedMs + 20], woken);
            Assert.Equal("Adrian Hager", line.Open?.DisplayName);

            // A click on a line itself copies it too; the moment over, "Copied" goes.
            Assert.Equal(["Contoso"], Copies(() => Click(line, style, "value 0.4")));
            Assert.Single(Pieces(line, Whole(line, style), style), piece => piece.Name == "copied 0.4");
            Assert.DoesNotContain(Pieces(line, Whole(line, style), style), piece => piece.Name == "copied 0.3");
            now += ContactsLine.CopiedMs;
            Assert.DoesNotContain(Pieces(line, Whole(line, style), style), piece => piece.Name.StartsWith("copied ", StringComparison.Ordinal));

            // A line's own Email: its own address.
            Assert.Equal(IslandView.App, Click(line, style, "email 0.1").View);
            Assert.Equal("mailto:adrian@hager.ch", line.Opening);
            Assert.Equal(IslandView.App, Click(line, style, "call 0.3").View);
            Assert.Equal("tel:0445550102", line.Opening);
        });
    }

    /// <summary>
    /// The right click on a row: the person's name, Send email and Call, then Copy email, Copy phone
    /// number and Copy contact details — each only where they have it — then the accounts. Over an open
    /// row's second address it is that address. Copy picked says "Copied" on their row; the folder
    /// stays for a copy and goes for Email and Call. Over nobody, the accounts alone.
    /// </summary>
    [UiFact]
    public void TheRightClickOffersThePersonsActs()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var adrian = new Person("a", "Adrian Hager", "Contoso", ["adrian.hager@contoso.com", "adrian@hager.ch"], ["+41 79 123 45 67"]);
            var bea = new Person("b", "Beatrice Keller", null, ["bea@example.com"], []);
            using var line = Line(gfx, Answered([adrian, bea]));
            var style = StyleFor(1f, LookFor(dark: true));

            var (nobody, nobodyRows) = TheDock.ActRows(TheDock.ActsFor(ListKind.Contacts), line);
            Assert.Equal([WidgetAct.ManageAccounts], nobodyRows.Select(row => row.Act));

            var box = Whole(line, style);
            line.CardHover(Middle(Assert.Single(Pieces(line, box, style), piece => piece.Name == "name 0").Box), box, style, WidgetSurface.List);
            var (items, rows) = TheDock.ActRows(TheDock.ActsFor(ListKind.Contacts), line);
            Assert.Equal("Adrian Hager", items[0].Text);
            Assert.True(items[0].Header);
            Assert.Equal(
                [
                    (WidgetAct.ContactEmail, "mailto:adrian.hager@contoso.com"),
                    (WidgetAct.ContactCall, "tel:+41791234567"),
                    (WidgetAct.ContactCopy, "adrian.hager@contoso.com"),
                    (WidgetAct.ContactCopy, "+41 79 123 45 67"),
                    (WidgetAct.ContactCopy, "Adrian Hager\r\nContoso\r\nadrian.hager@contoso.com\r\nadrian@hager.ch\r\n+41 79 123 45 67"),
                    (WidgetAct.ManageAccounts, (string?)null),
                ],
                rows.Select(row => (row.Act, row.On)));
            Assert.Equal(
                [S.DockContactsSendEmail, S.DockContactsCall, S.DockContactsCopyEmail, S.DockContactsCopyPhone, S.DockContactsCopyAll],
                items.Where(item => item.Id != 0).Take(5).Select(item => item.Text));
            Assert.True(TheDock.HandsAway(WidgetAct.ContactEmail));
            Assert.True(TheDock.HandsAway(WidgetAct.ContactCall));
            Assert.False(TheDock.HandsAway(WidgetAct.ContactCopy));

            // Copy picked: on the clipboard, and their row says so.
            var opened = new List<string>();
            Assert.Equal(["+41 79 123 45 67"], Copies(() => TheDock.RunAct(rows[3], line, (target, _) => opened.Add(target))));
            Assert.Empty(opened);
            Assert.True(line.SaysCopied(adrian));
            Assert.Single(Pieces(line, Whole(line, style), style), piece => piece.Name == "copied 0");
            // Email picked: Windows is handed the mailto:.
            TheDock.RunAct(rows[0], line, (target, _) => opened.Add(target));
            Assert.Equal(["mailto:adrian.hager@contoso.com"], opened);

            // Over an open row's second address, that address.
            Click(line, style, "name 0");
            box = Whole(line, style);
            line.CardHover(Middle(Assert.Single(Pieces(line, box, style), piece => piece.Name == "value 0.1").Box), box, style, WidgetSurface.List);
            var (_, second) = TheDock.ActRows(TheDock.ActsFor(ListKind.Contacts), line);
            Assert.Equal("mailto:adrian@hager.ch", second[0].On);
            Assert.Equal("adrian@hager.ch", second.First(row => row.Act == WidgetAct.ContactCopy).On);

            // Beatrice has no number: no Call, no Copy phone number.
            box = Whole(line, style);
            line.CardHover(Middle(Assert.Single(Pieces(line, box, style), piece => piece.Name == "name 1").Box), box, style, WidgetSurface.List);
            var (_, beas) = TheDock.ActRows(TheDock.ActsFor(ListKind.Contacts), line);
            Assert.Equal([WidgetAct.ContactEmail, WidgetAct.ContactCopy, WidgetAct.ContactCopy, WidgetAct.ManageAccounts], beas.Select(row => row.Act));
        });
    }

    /// <summary>
    /// Held short, a row the keys choose past the window is scrolled into sight, and so is a row
    /// opened near the foot — its details with it — as little as it takes.
    /// </summary>
    [UiFact]
    public void ARowChosenOrOpenedIsScrolledIntoSight()
    {
        Offscreen.OnStaThread(gfx =>
        {
            using var line = Line(gfx, Answered(Many(30)));
            var style = StyleFor(1f, LookFor(dark: true));
            var box = PopupPaint.Rect(0, 0, FolderFlyout.ListWidth(style.Scale), LineRows.Px(6, style));
            Assert.True(line.Scrolled(box, style, WidgetSurface.List).Scrolls);
            Assert.Equal(0, line.ListScroll);

            void InSight(string name)
            {
                var laid = line.Scrolled(box, style, WidgetSurface.List);
                var piece = Assert.Single(Pieces(line, laid.Moved, style), each => each.Name == name).Box;
                Assert.True(piece.top >= laid.Window.top - 0.5f && piece.bottom <= laid.Window.bottom + 0.5f, $"{name} is out of sight");
            }

            for (var index = 0; index < 12; index++)
            {
                line.KeyOn(VK.VK_DOWN, WidgetSurface.List);
            }
            Assert.Equal("Person 11", line.Selected?.DisplayName);
            InSight("name 11");
            var after = line.ListScroll;
            Assert.True(after > 0);
            // Up once: still in sight, so nothing moves.
            line.KeyOn(VK.VK_UP, WidgetSurface.List);
            _ = line.Scrolled(box, style, WidgetSurface.List);
            Assert.Equal(after, line.ListScroll);

            // Opened, its last detail is brought into sight with it.
            line.KeyOn(VK.VK_DOWN, WidgetSurface.List);
            line.KeyOn(VK.VK_DOWN, WidgetSurface.List);
            line.KeyOn(VK.VK_RETURN, WidgetSurface.List);
            Assert.Equal("Person 12", line.Open?.DisplayName);
            InSight("value 12.0");
            InSight("name 12");
        });
    }

    /// <summary>
    /// Held short it shows whole rows from the top under the box, and the rest scroll under the box
    /// rather than being counted (ListScroll.cs); on one row the box alone; and what is typed narrows
    /// what is held.
    /// </summary>
    [UiFact]
    public void HeldShortItShowsWholeRowsAndScrollsToTheRest()
    {
        Offscreen.OnStaThread(gfx =>
        {
            using var line = Line(gfx, Answered(Many(60)));
            var style = StyleFor(1.5f, LookFor(dark: true));
            var width = FolderFlyout.ListWidth(style.Scale);
            Assert.True(line.ListHeight(width, style) > LineRows.Px(FolderOptions.MostCells, style));
            foreach (var rows in new[] { 5, 8, FolderOptions.MostCells })
            {
                var box = PopupPaint.Rect(0, 0, width, LineRows.Px(rows, style));
                var pieces = Pieces(line, box, style);
                Assert.All(pieces, piece => Assert.True(piece.Box.top >= -0.5f && piece.Box.bottom <= box.bottom + 0.5f, $"{rows} rows: {piece.Name}"));
                var shown = pieces.Count(piece => piece.Name.StartsWith("name ", StringComparison.Ordinal));
                Assert.True(shown > 0, $"{rows} rows show nobody");
                Assert.DoesNotContain(pieces, piece => piece.Name == "more");
                Assert.Single(pieces, piece => piece.Name == "search");
                var laid = line.Scrolled(box, style, WidgetSurface.List);
                Assert.True(laid.Scrolls);
                Assert.Equal(60, Pieces(line, laid.Moved, style).Count(piece => piece.Name.StartsWith("name ", StringComparison.Ordinal)));
            }

            var one = PopupPaint.Rect(0, 0, width, LineRows.Px(1, style));
            var single = Pieces(line, one, style);
            var search = Assert.Single(single, piece => piece.Name == "search").Box;
            Assert.True(search.top >= 0 && search.bottom <= one.bottom + 0.5f);
            Assert.DoesNotContain(single, piece => piece.Name.StartsWith("name ", StringComparison.Ordinal));

            Type(line, "Person 33");
            var narrowed = Pieces(line, PopupPaint.Rect(0, 0, width, LineRows.Px(FolderOptions.MostCells, style)), style);
            Assert.DoesNotContain(narrowed, piece => piece.Name == "more");
            Assert.Equal("Person 33", Assert.Single(narrowed, piece => piece.Name.StartsWith("name ", StringComparison.Ordinal)).Text);
        });
    }

    /// <summary>
    /// Nobody listed is not always the same nobody: being read, an account that would not answer, no
    /// contacts at all; with no account that gives contacts the line is "Set up in Settings".
    /// </summary>
    [UiFact]
    public void NobodyListedSaysWhy()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var style = StyleFor(1f, LookFor(dark: true));
            void Says(ContactsLine line, string words)
            {
                var pieces = Pieces(line, Whole(line, style), style);
                Assert.Equal(words, Assert.Single(pieces, piece => piece.Name == "nothing").Text);
                Assert.DoesNotContain(pieces, piece => piece.Name is "count" or "more" || piece.Name.StartsWith("name ", StringComparison.Ordinal));
                Assert.Single(pieces, piece => piece.Name == "search");
            }

            using var empty = Line(gfx, Answered([]));
            Says(empty, S.DockContactsNone);

            var reader = new FakeReader(AccountKind.Dav, hold: true);
            var reading = new ContactStore(new AccountSet([reader]));
            using var waiting = Line(gfx, reading);
            Says(waiting, S.DockContactsReading);
            Assert.Null(waiting.Unset);
            reader.Failure = new InvalidOperationException("The server said no.");
            reader.Answer();
            reading.Idle().GetAwaiter().GetResult();
            reading.Deliver();
            Says(waiting, reading.Problem!);

            var none = new ContactStore(new AccountSet([]));
            using var unset = Line(gfx, none);
            none.NoAccountFound();
            Assert.Equal(SetupNeed.Contacts, unset.Unset);
        });
    }

    /// <summary>
    /// The contacts are synced from the dock's start with no contacts line anywhere (PimSync,
    /// 2026-10-29); a line placed and opened afterwards shows them at once and reads nothing itself.
    /// </summary>
    [UiFact]
    public void ALinePlacedAfterTheSyncShowsItsPeopleWithNoReadOfItsOwn()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var reader = new FakeReader(AccountKind.Dav);
            reader.People.AddRange(Five());
            var set = new AccountSet([reader]);
            var calendars = new CalendarStore(new AccountCalendars(set));
            using var mail = new MailStore(set);
            using var store = new ContactStore(set);
            PimSync.Start(calendars, mail, contacts: store);
            store.Idle().GetAwaiter().GetResult();
            Assert.Equal(1, reader.ContactReads);

            using var line = Line(gfx, store, shown: false);
            line.Visible(true);
            store.Idle().GetAwaiter().GetResult();
            Assert.Equal(1, reader.ContactReads);
            Assert.Equal(5, line.Matches.Count);
        });
    }

    /// <summary>Opening the folder reads the accounts, as the flyout's opening did; its "Refresh" reads them again however fresh.</summary>
    [UiFact]
    public void OpeningReadsAndRefreshReadsAgain()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var reader = new FakeReader(AccountKind.Dav);
            reader.People.AddRange(Five());
            var store = new ContactStore(new AccountSet([reader]));
            using var line = Line(gfx, store, shown: false);
            Assert.Equal(0, reader.ContactReads);
            line.Visible(true);
            store.Idle().GetAwaiter().GetResult();
            Assert.Equal(1, reader.ContactReads);
            store.Deliver();
            Assert.Equal(5, line.Matches.Count);

            // Fresh: opening again reads nothing; "Refresh" does.
            line.Visible(false);
            line.Visible(true);
            store.Idle().GetAwaiter().GetResult();
            Assert.Equal(1, reader.ContactReads);
            ((IRefreshable)line).RefreshNow();
            store.Idle().GetAwaiter().GetResult();
            Assert.Equal(2, reader.ContactReads);

            // Closed, what was typed and chosen goes.
            Type(line, "ad");
            line.KeyOn(VK.VK_DOWN, WidgetSurface.List);
            line.Visible(false);
            Assert.Equal("", line.Box.Text);
            Assert.Null(line.Selected);
            Assert.False(line.TypingOn);
        });
    }

    /// <summary>The next row: nothing chosen starts at an end, and neither end wraps.</summary>
    [Theory]
    [InlineData(-1, 5, true, 0)]
    [InlineData(-1, 5, false, 4)]
    [InlineData(0, 5, false, 0)]
    [InlineData(4, 5, true, 4)]
    [InlineData(2, 5, true, 3)]
    [InlineData(-1, 0, true, -1)]
    public void TheNextRowNeverWraps(int selected, int count, bool down, int next) =>
        Assert.Equal(next, ContactsLine.Move(selected, count, down));

    // --- Pictures --------------------------------------------------------------------------------------

    /// <summary>
    /// The preset as it opens at 150 %, to stand beside contacts-flyout-*.png: everyone, a row under the
    /// pointer, typed with a row chosen and opened, nobody matching, sixty held short, no account, and
    /// at 225 % text.
    /// </summary>
    [UiFact]
    public void ContactsPresetPictures()
    {
        foreach (var dark in new[] { true, false })
        {
            Offscreen.OnStaThread(gfx =>
            {
                var folders = new List<FolderIsland>();
                ContactsLine.Copier = _ => { };
                try
                {
                    var panels = new List<(string, Look, FolderIsland, FolderPlan)>();
                    void Add(string caption, ContactStore store, Action<ContactsLine, TileStyle>? set = null, float text = 1f, int? rows = null)
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
                        store.Deliver();
                        var line = (ContactsLine)island.Tiles[0].Island;
                        var style = StyleFor(1.5f, look);
                        set?.Invoke(line, style);
                        panels.Add((caption, look, island, Plan(island, look, 1.5f).Plan));
                    }

                    Add("Everyone", Answered(Five()));
                    Add("A row under the pointer", Answered(Five()), (line, style) =>
                    {
                        var box = Whole(line, style);
                        line.CardHover(Middle(Assert.Single(Pieces(line, box, style), piece => piece.Name == "name 2").Box), box, style, WidgetSurface.List);
                    });
                    Add("Typed, chosen and opened by the keys", Answered(Five()), (line, _) =>
                    {
                        Type(line, "e");
                        line.KeyOn(VK.VK_DOWN, WidgetSurface.List);
                        line.KeyOn(VK.VK_RETURN, WidgetSurface.List);
                    });
                    Add("Nobody matches", Answered(Five()), (line, _) => Type(line, "q"));
                    Add("Sixty held to eight rows", Answered(Many(60)), rows: 8);
                    var none = new ContactStore(new AccountSet([]));
                    none.NoAccountFound();
                    Add("No account", none);
                    Add("225 % text", Answered(Five()), text: 2.25f);
                    Sheet(gfx, $"contacts-preset-{(dark ? "dark" : "light")}.png", 1.5f, panels);
                }
                finally
                {
                    ContactsLine.Copier = null;
                    foreach (var island in folders)
                    {
                        island.Closed();
                        island.Dispose();
                    }
                }
            });
        }
    }

    /// <summary>Five people, Adrian with two addresses and two numbers, as an address book of work holds them.</summary>
    private static List<Person> Rich() =>
    [
        new("a", "Adrian Hager", "Contoso", ["adrian.hager@contoso.com", "adrian@hager.ch"], ["+41 79 123 45 67", "+41 44 555 01 02"]),
        .. Five().Skip(1),
    ];

    /// <summary>
    /// A person's details at 150 % — contacts-details-&lt;dark|light&gt;.png: at rest, a row under the
    /// pointer (Email and Call), a row open with a line under the pointer, a detail just copied, Ctrl+C
    /// on a closed row, open in a narrow list, opened near the foot of a line held short (scrolled into
    /// sight), Compact items, and at 225 % text.
    /// </summary>
    [UiFact]
    public void ContactsDetailsPictures()
    {
        foreach (var dark in new[] { true, false })
        {
            Offscreen.OnStaThread(gfx =>
            {
                var folders = new List<FolderIsland>();
                ContactsLine.Copier = _ => { };
                ContactsLine.ControlHeld = true;
                try
                {
                    var panels = new List<(string, Look, FolderIsland, FolderPlan)>();
                    void Add(string caption, Action<ContactsLine, TileStyle>? set = null, float text = 1f, Action<BarItem>? holding = null, IReadOnlyList<Person>? people = null)
                    {
                        var look = LookFor(dark, text);
                        var holder = Holder();
                        holding?.Invoke(holder);
                        var store = Answered(people ?? Rich());
                        var island = Made(gfx, store, $"{Owner}/{holder.Id}-{panels.Count}", holder);
                        folders.Add(island);
                        island.Opened(gfx);
                        store.Deliver();
                        var line = (ContactsLine)island.Tiles[0].Island;
                        line.After = (_, _) => { };
                        var style = StyleFor(1.5f, look);
                        set?.Invoke(line, style);
                        panels.Add((caption, look, island, Plan(island, look, 1.5f).Plan));
                    }
                    void Hover(ContactsLine line, in TileStyle style, string piece)
                    {
                        var box = Whole(line, style);
                        line.CardHover(Middle(Assert.Single(Pieces(line, box, style), each => each.Name == piece).Box), box, style, WidgetSurface.List);
                    }

                    Add("At rest");
                    Add("A row under the pointer", (line, style) => Hover(line, style, "name 0"));
                    Add("Open, a line under the pointer", (line, style) =>
                    {
                        Click(line, style, "name 0");
                        Hover(line, style, "value 0.1");
                    });
                    Add("A number just copied", (line, style) =>
                    {
                        Click(line, style, "name 0");
                        Click(line, style, "copy 0.2");
                    });
                    Add("Ctrl+C on a closed row", (line, _) =>
                    {
                        line.StopTypingOn();
                        line.KeyOn(VK.VK_DOWN, WidgetSurface.List);
                        line.KeyOn(VK.VK_DOWN, WidgetSurface.List);
                        line.KeyOn('C', WidgetSurface.List);
                    });
                    Add("Narrow, open", (line, style) =>
                    {
                        Click(line, style, "name 0");
                        Hover(line, style, "value 0.0");
                    }, holding: holder => holder.Folder!.Widths = [FolderListWidth.Narrow]);
                    Add("Held to 6 rows, the last opened by the keys", (line, _) =>
                    {
                        for (var index = 0; index < 5; index++)
                        {
                            line.KeyOn(VK.VK_DOWN, WidgetSurface.List);
                        }
                        line.KeyOn(VK.VK_RETURN, WidgetSurface.List);
                    }, holding: holder => holder.Folder!.Lists[0][0].Island.Height = LineHeight.Exactly(12), people: [.. Rich(), .. Many(6)]);
                    Add("Compact items, open", (line, style) => Click(line, style, "name 0"),
                        holding: holder => holder.Folder!.Lists[0][0].Island.Items = new ItemStyleOptions { Size = ItemSize.Compact });
                    Add("225 % text, open", (line, style) => Click(line, style, "name 0"), text: 2.25f);
                    Sheet(gfx, $"contacts-details-{(dark ? "dark" : "light")}.png", 1.5f, panels);
                }
                finally
                {
                    ContactsLine.Copier = null;
                    ContactsLine.ControlHeld = null;
                    foreach (var island in folders)
                    {
                        island.Closed();
                        island.Dispose();
                    }
                }
            });
        }
    }

    /// <summary>
    /// The people's pictures at 150 % — contacts-photos-&lt;dark|light&gt;.png: Adrian and Carlos pictured among
    /// glyphs, a pictured row under the pointer, opened, of two books (the glyphs in their colours), Compact
    /// and Large items, the item style's Pictures off, and at 225 % text.
    /// </summary>
    [UiFact]
    public void ContactsPhotoPictures()
    {
        foreach (var dark in new[] { true, false })
        {
            Offscreen.OnStaThread(gfx =>
            {
                var folders = new List<FolderIsland>();
                ContactsLine.Copier = _ => { };
                try
                {
                    var panels = new List<(string, Look, FolderIsland, FolderPlan)>();
                    void Add(string caption, Action<ContactsLine, TileStyle>? set = null, float text = 1f, ItemStyleOptions? items = null, IReadOnlyList<Person>? people = null)
                    {
                        var look = LookFor(dark, text);
                        var holder = Holder();
                        holder.Folder!.Lists[0][0].Island.Items = items;
                        var store = Pictured(people);
                        var island = Made(gfx, store, $"{Owner}/{holder.Id}-{panels.Count}", holder);
                        folders.Add(island);
                        island.Opened(gfx);
                        store.Deliver();
                        var line = (ContactsLine)island.Tiles[0].Island;
                        line.After = (_, _) => { };
                        var style = StyleFor(1.5f, look);
                        set?.Invoke(line, style);
                        panels.Add((caption, look, island, Plan(island, look, 1.5f).Plan));
                    }

                    Add("Pictures among glyphs");
                    Add("A pictured row under the pointer", (line, style) =>
                    {
                        var box = Whole(line, style);
                        line.CardHover(Middle(Assert.Single(Pieces(line, box, style), piece => piece.Name == "name 0").Box), box, style, WidgetSurface.List);
                    });
                    Add("A pictured row open", (line, style) => Click(line, style, "name 0"));
                    Add("Two books", people: [.. Five().Select((person, index) => person with { Colour = index % 2 == 0 ? "#2E86DE" : "#E67E22" })]);
                    Add("Compact items", items: new ItemStyleOptions { Size = ItemSize.Compact });
                    Add("Large items", items: new ItemStyleOptions { Size = ItemSize.Large });
                    Add("Pictures off", items: new ItemStyleOptions { Pictures = false });
                    Add("Preview off", items: new ItemStyleOptions { Preview = false });
                    Add("225 % text", text: 2.25f);
                    Sheet(gfx, $"contacts-photos-{(dark ? "dark" : "light")}.png", 1.5f, panels);
                }
                finally
                {
                    ContactsLine.Copier = null;
                    foreach (var island in folders)
                    {
                        island.Closed();
                        island.Dispose();
                    }
                }
            });
        }
    }

    /// <summary>The face on bars of 40 and 48 DIP and at 150 % — contacts-face-&lt;dark|light&gt;.png.</summary>
    [UiFact]
    public void ContactsFacePictures()
    {
        foreach (var dark in new[] { true, false })
        {
            Offscreen.OnStaThread(gfx =>
            {
                var look = LookFor(dark);
                var theme = FlyoutTheme.For(look);
                using var island = Made(gfx, Answered(Five()));
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
                }, bitmap => Offscreen.Save(bitmap, Path.Combine(Offscreen.Pictures, $"contacts-face-{(dark ? "dark" : "light")}.png")));
            });
        }
    }
}
