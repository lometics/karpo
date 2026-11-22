// The mail is a preset (docs\widgets.md §95): what a click on the mail entry gave — its panel, how
// many are unread over the newest messages, "Set up in Settings" with no account, a note while an
// inbox could not be refreshed — is a folder holding the inbox's line (MailIsland) under the mail
// face: the envelope the entry was, the unread count on its corner, dimmed while an inbox cannot be
// read, the stale mark on its lower corner, painted again in place as the mail changes. Every store
// here is a stand-in (MailIsland.Stand, MailStore.Answered, FakeReader): no account is ever reached.
//
// Pictured beside the panel it replaces (rendered before it went: mailref-<inbox|stale|empty|setup>-*.png
// and the entry's stale-envelope-*.png): mail-preset-<dark|light>.png at 150 %, and the face on the
// bar, mail-face-<dark|light>.png.

using Karpo.Accounts;
using Karpo.Dock;
using Karpo.Dock.Pim;
using Karpo.Tests.Islands;
using Karpo.Tests.Pim;
using static Karpo.Tests.Folders.ListFormPictures;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Folders;

public sealed unsafe class MailPresetTests
{
    private static readonly PresetMachine Machine = new(HasBattery: false, FixedDrives: []);

    /// <summary>The mail's preset folder, as the add list makes it.</summary>
    private static BarItem Holder() => Presets.Folder(Presets.Find(Presets.MailId)!, Machine);

    private static List<MailItem> Inbox(int count = 5)
    {
        var now = DateTimeOffset.Now;
        (string From, string Subject, string Preview, bool Unread)[] letters =
        [
            ("Sandra Gerber", "Lunch on Friday? The new place by the lake has a terrace", "Hi! Shall we try it at noon? I can book a table for four.", true),
            ("GitHub", "[karpo-app] CI passed on master", "All checks have passed: build, tests (UI), publish.", true),
            ("Adrian Hager", "Re: Quarterly review — slides attached", "Thanks, looks good. Two small remarks on slide 7 below.", false),
            ("Swiss International Air Lines", "Your boarding pass for LX 1832", "Zurich to London City, Monday, gate opens 06:40.", true),
            ("Beat Hagenbuch", "Photos", "", false),
        ];
        return
        [
            .. Enumerable.Range(0, count).Select(index =>
            {
                var (from, subject, preview, unread) = letters[index % letters.Length];
                // Every one but the fifth opens (a server Karpo knows no web mail for gives none).
                return new MailItem($"{index}", from, $"{index}@example.com", subject, preview.Length > 0 ? preview : null, now.AddMinutes(-4 - (index * 47)), unread,
                    index % 5 == 4 ? null : $"https://outlook.office.com/mail/id/{index}");
            }),
        ];
    }

    /// <summary>The preset's folder over <paramref name="store"/>, never an account's.</summary>
    private static FolderIsland Made(Gfx gfx, MailStore store, string? key = null)
    {
        var holder = Holder();
        MailIsland.Stand = store;
        try
        {
            return new FolderIsland(key ?? Config.IslandKey(null, holder.Id), holder, gfx, new Config());
        }
        finally
        {
            MailIsland.Stand = null;
        }
    }

    // --- The preset ------------------------------------------------------------------------------------

    /// <summary>
    /// The panel's list, the inbox's line, alone and as tall as it shows (Automatic): the panel had no
    /// footer but the stale note, which the line's mark says, so the preset has none. It wears the mail
    /// face, which has no name and nothing to set.
    /// </summary>
    [Fact]
    public void ThePresetIsTheInboxLineUnderTheEnvelope()
    {
        var preset = Presets.Find(Presets.MailId)!;
        Assert.Equal(FolderFaces.MailId, preset.Face!.Kind);
        Assert.Equal(["mail"], preset.Items.Select(item => item.Form));
        Assert.Null(preset.Items[0].Height);

        var folder = Holder().Folder!;
        Assert.Same(FolderFaces.Mail, folder.Face.Info);
        Assert.Empty(folder.Face.Info.Options);
        Assert.Equal(WidgetInfos.Mail.Glyph, folder.Face.Info.Glyph);
        Assert.Equal(ListKind.Mail, Assert.Single(folder.Cells).Island.List);
    }

    /// <summary>The entry is gone from the bar: the line is all there is.</summary>
    [Fact]
    public void TheEntryIsGoneAndTheLineStays()
    {
        Assert.Null(BarEntries.Find("mail"));
        Assert.NotNull(ListWidgets.Find("mail"));
        Assert.DoesNotContain("mail", Enum.GetValues<BarKind>().Select(kind => kind.ToString().ToLowerInvariant()));
    }

    /// <summary>The folder opened: the inbox's line, over the face's own store.</summary>
    [UiFact]
    public void TheFolderHoldsTheInboxLine()
    {
        Offscreen.OnStaThread(gfx =>
        {
            using var store = new MailStore(new AccountSet([]));
            store.Answered(Inbox(), unread: 3);
            using var folder = Made(gfx, store, $"{Owner}/{Holder().Id}");
            folder.Opened(gfx);
            var line = Assert.IsType<MailIsland>(Assert.Single(folder.Tiles).Island);
            Assert.Equal(MailIsland.Tip(store, DateTime.Now), line.Tooltip(0));
            Assert.Same(store, folder.Mail);
            folder.Closed();
        });
    }

    /// <summary>
    /// An empty inbox says so, as the panel did ("Your inbox is empty."), where the line said "All
    /// read" over nothing; with mail and none unread it is still "All read".
    /// </summary>
    [UiFact]
    public void AnEmptyInboxSaysSo()
    {
        Offscreen.OnStaThread(gfx =>
        {
            using var store = new MailStore(new AccountSet([]));
            var line = ListKind.Mail.New();
            line.Id = "mail";
            using var island = new MailIsland(Config.IslandKey(null, line.Id), line, gfx, new Config(), store);
            var look = LookFor(dark: true);
            var style = FolderFlyout.StyleFor(1.5f, look, FlyoutTheme.For(look));
            var box = PopupPaint.Rect(0, 0, FolderFlyout.ListWidth(1.5f), LineRows.Px(4, style));
            // §388: nothing to list is the kit's Status; with mail, the header.
            string Head() => island.LinePieces(box, style).Single(piece => piece.Name is "head" or "status").Text;

            store.Answered([], unread: 0);
            Assert.Equal(S.DockMailInboxEmpty, Head());
            store.Answered(Inbox(), unread: 0);
            Assert.Equal(S.DockMailAllRead, Head());
        });
    }

    // --- The face --------------------------------------------------------------------------------------

    /// <summary>
    /// What the face draws, as the entry drew it: the count from one ("99+" past 99) on a plain
    /// envelope; none with nothing unread; dimmed with no count where an inbox cannot be read; the
    /// stale mark while it shows what could not be refreshed; the plain envelope alone with no account
    /// (the entry's "Set up" was its glyph alone) and with no store at all.
    /// </summary>
    [Fact]
    public void TheFaceDrawsTheCountTheDimAndTheMark()
    {
        Assert.Equal((false, null, false), MailFaceDrawing.Look(null));
        using var store = new MailStore(new AccountSet([]));
        store.Answered(Inbox(), unread: 3);
        Assert.Equal((false, "3", false), MailFaceDrawing.Look(store));
        store.Answered(Inbox(), unread: 0);
        Assert.Equal((false, null, false), MailFaceDrawing.Look(store));
        store.Answered(Inbox(), unread: 120);
        Assert.Equal((false, "99+", false), MailFaceDrawing.Look(store));
        store.Answered(Inbox(), unread: 2, new Freshness(DateTime.UtcNow.AddHours(-1), true));
        Assert.Equal((false, "2", true), MailFaceDrawing.Look(store));
        store.NoAccountFound();
        Assert.Equal((false, null, false), MailFaceDrawing.Look(store));
    }

    /// <summary>
    /// An inbox that cannot be read at all, with nothing kept: the envelope dimmed with no count, and
    /// the hover says why — read through a real store over an account that fails, as the entry did.
    /// </summary>
    [Fact]
    public void AnInboxThatCannotBeReadDimsTheEnvelope()
    {
        var reader = new FakeReader { Failure = new HttpRequestException("offline") };
        using var store = new MailStore(new AccountSet([reader]));
        store.Refresh();
        store.Idle().GetAwaiter().GetResult();
        Assert.NotNull(store.Problem);
        Assert.Equal((true, null, false), MailFaceDrawing.Look(store));
        Assert.StartsWith(store.Problem!, MailIsland.Tip(store, DateTime.Now), StringComparison.Ordinal);
    }

    /// <summary>
    /// The hover is the entry's: nothing over the bar (§367); to a screen reader how many are unread,
    /// and from when ("Last updated 14:05 — couldn't refresh").
    /// </summary>
    [UiFact]
    public void TheHoverIsTheEntrys()
    {
        Offscreen.OnStaThread(gfx =>
        {
            using var store = new MailStore(new AccountSet([]));
            store.Answered(Inbox(), unread: 3);
            using var island = Made(gfx, store);
            Assert.Equal(S.DockMailUnreadCount(3), island.Tooltip(0));

            var when = DateTime.UtcNow.AddHours(-1);
            store.Answered(Inbox(), unread: 3, new Freshness(when, true));
            var stale = store.Freshness.Tip(DateTime.Now);
            Assert.NotNull(stale);
            Assert.Equal($"{S.DockMailUnreadCount(3)}. {stale}", island.Tooltip(0));

            store.Answered(Inbox(), unread: 0);
            Assert.Equal(S.DockMailTitle, island.Tooltip(0));
        });
    }

    /// <summary>
    /// The store's news paints the face again in place — the count, the dim, the mark — never a
    /// rebuild, and it stays one size, its count's room kept (§276); a refresh with nothing new paints
    /// the same. Read through a
    /// real store over an account that answers what the test gives it, as the shared store reads.
    /// </summary>
    [UiFact]
    public void TheFacePaintsAgainInPlaceAsMailArrives()
    {
        var reader = new FakeReader { Inbox = new Inbox(1, Inbox(2)) };
        using var store = new MailStore(new AccountSet([reader]));
        Offscreen.OnStaThread(gfx =>
        {
            using var island = Made(gfx, store);
            Assert.True(island.ShowsMail);
            Assert.Equal(0, island.IntervalMs);
            var config = new Config { Height = 48 };
            var style = new IslandStyle(1f, false, Layout.CellPx(96, config), Layout.IconPx(96, config), LookFor(dark: true));
            var size = island.CellSize(0, style);
            _ = island.Measure(style);
            // The first look starts the first read (docs\widgets.md §11).
            _ = island.Surface(0, style);
            store.Idle().GetAwaiter().GetResult();
            Assert.Equal(1, reader.InboxReads);
            Assert.False(((IIsland)island).Refresh(gfx));
            Assert.StartsWith("False|1|False|", island.FaceShown, StringComparison.Ordinal);
            Assert.Equal(size, island.CellSize(0, style));

            // Mail arrives: the count, in place.
            reader.Inbox = new Inbox(4, Inbox(5));
            store.Refresh(force: true);
            store.Idle().GetAwaiter().GetResult();
            Assert.False(((IIsland)island).Refresh(gfx));
            Assert.StartsWith("False|4|False|", island.FaceShown, StringComparison.Ordinal);
            Assert.Equal(size, island.CellSize(0, style));

            // The account fails now: what it gave stays, the mark stands on the envelope.
            reader.Failure = new HttpRequestException("offline");
            store.Refresh(force: true);
            store.Idle().GetAwaiter().GetResult();
            Assert.False(((IIsland)island).Refresh(gfx));
            Assert.StartsWith("False|4|True|", island.FaceShown, StringComparison.Ordinal);
            Assert.Equal(size, island.CellSize(0, style));
            Assert.Equal(3, reader.InboxReads);
        });
    }

    /// <summary>
    /// The face is its glyph, its widest count's and the stale mark's room across a bar and the button's
    /// ends whatever it shows (§276), at every bar, across it and down it.
    /// </summary>
    [UiFact]
    public void TheFaceIsOneSizeWhateverItShows()
    {
        Offscreen.OnStaThread(gfx =>
        {
            using var store = new MailStore(new AccountSet([]));
            using var island = Made(gfx, store);
            foreach (var height in new[] { 28, 48, 72, 115 })
            {
                var config = new Config { Height = height };
                foreach (var vertical in new[] { false, true })
                {
                    foreach (var text in new[] { 1f, 2.25f })
                    {
                        var style = new IslandStyle(1.5f, vertical, Layout.CellPx(144, config), Layout.IconPx(144, config), LookFor(dark: true, text));
                        var where = $"{height} DIP, text ×{text}{(vertical ? ", vertical" : "")}";
                        var trail = GlyphBadge.Trail(gfx, style, stale: true);
                        Assert.True(trail > 0 || vertical, $"{where}: no room for the count");
                        void Holds(string state)
                        {
                            var (width, tall) = island.CellSize(0, style);
                            Assert.True(MathF.Abs(FaceWidthTests.GlyphButton(style, trail) - (vertical ? tall : width)) <= 0.5f, $"{where}, {state}: {width}×{tall}");
                        }
                        foreach (var unread in new[] { 0, 3, 42, 1000 })
                        {
                            store.Answered(Inbox(), unread);
                            Holds($"{unread} unread");
                        }
                        store.NoAccountFound();
                        Holds("no account");
                    }
                }
            }
        });
    }

    /// <summary>
    /// The face's right click reads nothing again (§451: the line's own menu's "Refresh" does): the accounts
    /// alone, with or without the line in the folder, and the inboxes are not read for it.
    /// </summary>
    [UiFact]
    public void TheFacesMenuIsTheAccountsAlone()
    {
        var reader = new FakeReader { Inbox = new Inbox(1, Inbox(1)) };
        using var store = new MailStore(new AccountSet([reader]));
        Offscreen.OnStaThread(gfx =>
        {
            var holder = Holder();
            using var island = Made(gfx, store);
            var (items, acts) = TheDock.BarMenuRows(holder, island);
            Assert.Equal([WidgetAct.ManageAccounts], acts.Select(act => act.Act));
            Assert.DoesNotContain(items, item => item.Text == S.DockWidgetMenuRefresh);
            Assert.Equal(0, reader.InboxReads);

            // Without the line, the same.
            holder.Folder!.Lists = [[]];
            island.Configure(holder, new Config());
            Assert.Empty(island.Tiles);
            Assert.Equal([WidgetAct.ManageAccounts], TheDock.BarMenuRows(holder, island).Acts.Select(act => act.Act));
        });
    }

    /// <summary>Another face put on lets go of the store, and the mail face put back takes it again.</summary>
    [UiFact]
    public void AnotherFaceLetsGoOfTheStore()
    {
        Offscreen.OnStaThread(gfx =>
        {
            using var store = new MailStore(new AccountSet([]));
            MailIsland.Stand = store;
            try
            {
                var holder = Holder();
                using var island = new FolderIsland(Config.IslandKey(null, holder.Id), holder, gfx, new Config());
                Assert.Same(store, island.Mail);
                holder.Folder!.Face = FolderFace.Of(FolderFaces.IconId);
                island.Configure(holder, new Config());
                Assert.Null(island.Mail);
                Assert.Null(island.FaceWidget);
                holder.Folder.Face = FolderFace.Of(FolderFaces.MailId);
                island.Configure(holder, new Config());
                Assert.Same(store, island.Mail);
            }
            finally
            {
                MailIsland.Stand = null;
            }
        });
    }

    // --- Pictures --------------------------------------------------------------------------------------

    /// <summary>
    /// The preset as it opens: an inbox with unread mail, the same could not be refreshed, an empty
    /// one, no account, and at 225 % text — at 150 %, to stand beside mailref-*.png of the panel.
    /// </summary>
    [UiFact]
    public void MailPresetPictures()
    {
        foreach (var dark in new[] { true, false })
        {
            Offscreen.OnStaThread(gfx =>
            {
                var stores = new List<MailStore>();
                var folders = new List<FolderIsland>();
                try
                {
                    var panels = new List<(string, Look, FolderIsland, FolderPlan)>();
                    void Add(string caption, Action<MailStore> fill, float text = 1f, string? hover = null, bool press = false)
                    {
                        var store = new MailStore(new AccountSet([]));
                        stores.Add(store);
                        fill(store);
                        var look = LookFor(dark, text);
                        var folder = Made(gfx, store, $"{Owner}/{Holder().Id}-{panels.Count}");
                        folders.Add(folder);
                        folder.Opened(gfx);
                        var (_, plan) = Plan(folder, look, 1.5f);
                        if (hover is not null)
                        {
                            // The pointer on a message, as the folder tells the line: in the line's own px.
                            var line = (MailIsland)folder.Tiles[0].Island;
                            var style = FolderFlyout.StyleFor(1.5f, look, FlyoutTheme.For(look));
                            var card = plan.Cards[0];
                            var box = PopupPaint.Rect(0, 0, card.right - card.left, card.bottom - card.top);
                            var piece = Assert.Single(((ILinePieces)line).LinePieces(box, style), each => each.Name == hover);
                            var at = new System.Numerics.Vector2((piece.Box.left + piece.Box.right) / 2, (piece.Box.top + piece.Box.bottom) / 2);
                            line.CardHover(at, box, style, WidgetSurface.List);
                            if (press)
                            {
                                Assert.False(line.CardPress(at, box, style, WidgetSurface.List));
                            }
                        }
                        panels.Add((caption, look, folder, plan));
                    }

                    Add("Three unread", store => store.Answered(Inbox(), unread: 3));
                    Add("A message under the pointer", store => store.Answered(Inbox(), unread: 3), hover: "sender 1");
                    Add("A message pressed", store => store.Answered(Inbox(), unread: 3), hover: "subject 2", press: true);
                    Add("Could not be refreshed", store => store.Answered(Inbox(), unread: 3, new Freshness(DateTime.UtcNow.AddHours(-1), true)));
                    Add("Twelve, eight shown", store => store.Answered(Inbox(12), unread: 7));
                    Add("Empty", store => store.Answered([], unread: 0));
                    Add("No account", store => store.NoAccountFound());
                    Add("Three unread: 225 % text", store => store.Answered(Inbox(), unread: 3), 2.25f);
                    Sheet(gfx, $"mail-preset-{(dark ? "dark" : "light")}.png", 1.5f, panels);
                }
                finally
                {
                    foreach (var folder in folders)
                    {
                        folder.Closed();
                        folder.Dispose();
                    }
                    foreach (var store in stores)
                    {
                        store.Dispose();
                    }
                }
            });
        }
    }

    /// <summary>
    /// The face on a bar of 40 and 48 DIP and at 150 %: three unread, none, 120, could not be
    /// refreshed, cannot be read, no account — mail-face-&lt;dark|light&gt;.png, beside the entry's
    /// stale-envelope-*.png before it went.
    /// </summary>
    [UiFact]
    public void MailFacePictures()
    {
        var failing = new FakeReader { Failure = new HttpRequestException("offline") };
        using var unreadable = new MailStore(new AccountSet([failing]));
        unreadable.Refresh();
        unreadable.Idle().GetAwaiter().GetResult();
        foreach (var dark in new[] { true, false })
        {
            Offscreen.OnStaThread(gfx =>
            {
                var look = LookFor(dark);
                var theme = FlyoutTheme.For(look);
                var stores = new List<MailStore>();
                MailStore Filled(Action<MailStore> fill)
                {
                    var store = new MailStore(new AccountSet([]));
                    fill(store);
                    stores.Add(store);
                    return store;
                }
                MailStore[] states =
                [
                    Filled(store => store.Answered(Inbox(), unread: 3)),
                    Filled(store => store.Answered(Inbox(), unread: 0)),
                    Filled(store => store.Answered(Inbox(), unread: 120)),
                    Filled(store => store.Answered(Inbox(), unread: 3, new Freshness(DateTime.UtcNow.AddHours(-1), true))),
                    unreadable,
                    Filled(store => store.NoAccountFound()),
                ];
                var styles = new[] { (40, 96u), (48, 96u), (48, 144u) }
                    .Select(pair => new IslandStyle(pair.Item2 / 96f, false, Layout.CellPx(pair.Item2, new Config { Height = pair.Item1 }), Layout.IconPx(pair.Item2, new Config { Height = pair.Item1 }), look))
                    .ToList();
                var islands = states.Select(store => Made(gfx, store)).ToList();
                try
                {
                    const float gap = 12;
                    var width = gap + styles.Sum(style => (islands.Count * (islands[0].CellSize(0, style).Width + gap)) + gap);
                    var height = gap + styles.Max(style => islands[0].CellSize(0, style).Height) + gap;
                    Offscreen.Bitmap(width, height, dc =>
                    {
                        PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, width, height), 0, PopupPaint.Ink(theme.Base));
                        var x = gap;
                        foreach (var style in styles)
                        {
                            foreach (var island in islands)
                            {
                                var (w, h) = island.CellSize(0, style);
                                PopupPaint.Fill(dc, PopupPaint.Rect(x, gap, w, h), 4 * style.Scale, PopupPaint.Ink(theme.CardBackground));
                                Offscreen.Shifted(dc, x, gap, () => island.PaintCell(dc, 0, style, w, h));
                                x += w + gap;
                            }
                            x += gap;
                        }
                    }, bitmap => Offscreen.Save(bitmap, Path.Combine(Offscreen.Pictures, $"mail-face-{(dark ? "dark" : "light")}.png")));
                }
                finally
                {
                    foreach (var island in islands)
                    {
                        island.Dispose();
                    }
                    foreach (var store in stores)
                    {
                        store.Dispose();
                    }
                }
            });
        }
    }
}
