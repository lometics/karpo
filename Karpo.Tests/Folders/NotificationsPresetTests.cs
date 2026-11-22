// The notifications are a preset (docs\widgets.md §100): what a click on the bell gave — Karpo's
// notification center, grouped by app with a × per app and every notification listed, a card expanded to
// its buttons and its reply box, "Clear all" and do not disturb's switch in its header, the footer's
// ways to Windows' settings — is a folder holding the notification list's line, which does all of it
// now, over the premade notification actions, under the bell face: the bell, how many are new, the
// "zZ" while do not disturb is on.
//
// Every store here reads a database the test builds in a folder of its own (never Windows' own),
// every activation goes to a made-up activator (ToastActivator.Made) and do not disturb is a made-up
// one (DoNotDisturb.Made): no test starts an app, clears the person's list or switches their setting.
//
// Pictured beside the flyout it replaces (rendered before it went: notifications-grouped-*.png,
// notifications-expanded-*.png, notifications-quiet-*.png, and the bell's notification-badge-*.png):
// notifications-preset-<dark|light>-<100|150>.png, Windows' notification center's layout (§185), and
// the face, notifications-face-<dark|light>.png.

using System.Numerics;
using System.Text;
using Karpo.Dock;
using Karpo.Dock.QuickSettings.Services;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using static Karpo.Tests.Folders.ListFormPictures;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Folders;

public sealed unsafe class NotificationsPresetTests
{
    private static readonly PresetMachine Machine = new(HasBattery: false, FixedDrives: []);

    /// <summary>The notifications' preset folder, as the add list makes it.</summary>
    private static BarItem Holder() => Presets.Folder(Presets.Find(Presets.NotificationsId)!, Machine);

    private const string LongBody = "The review moved to Thursday. Bring the notes on the store and the mill, the diagrams of the "
        + "carriage, and whatever you have on the punched cards — we will go through all of it, and then lunch.";

    /// <summary>A toast of words alone, with launch arguments of its own.</summary>
    private static string Toast(string title, string body, string launch = "") =>
        $"<toast launch=\"{launch}\"><visual><binding template=\"ToastGeneric\"><text>{title}</text><text>{body}</text></binding></visual></toast>";

    /// <summary>
    /// Two apps: chat with a reply (NotificationCentreTests.Reply: a box, a selection, Send, Like, Open
    /// the thread and Windows' Dismiss) and a long message, then mail with five — newest first.
    /// </summary>
    internal static readonly (long Id, int App, int Minutes, string Payload)[] Centre =
    [
        (31, 1, 2, NotificationCentreTests.Reply),
        (32, 1, 30, Toast("Charles Babbage", LongBody, "action=view&amp;id=77")),
        (33, 2, 5, Toast("Grace Hopper", "Found the bug.")),
        (34, 2, 60, Toast("Weekly report", "Attached.")),
        (35, 2, 90, Toast("Invoice", "Due Friday.")),
        (36, 2, 120, Toast("Newsletter", "Short.")),
        (37, 2, 200, Toast("Receipt", "Thanks.")),
    ];

    /// <summary>Twelve, three for each of four apps: a notification center past any screen's room.</summary>
    internal static readonly (long Id, int App, int Minutes, string Payload)[] Twelve =
    [
        (41, 1, 2, Toast("Ada Lovelace", "Are the tables ready?")),
        (42, 1, 9, Toast("Ada Lovelace", "The second note is attached.")),
        (43, 1, 25, Toast("Charles Babbage", LongBody)),
        (44, 2, 5, Toast("Grace Hopper", "Found the bug.")),
        (45, 2, 60, Toast("Weekly report", "Attached.")),
        (46, 2, 90, Toast("Invoice", "Due Friday.")),
        (47, 3, 10, Toast("Design review", "In 15 minutes, Room 4.")),
        (48, 3, 70, Toast("Lunch with Alan", "12:30, the usual place.")),
        (49, 3, 130, Toast("Dentist", "Tomorrow at 9:00.")),
        (50, 4, 40, Toast("Updates installed", "Three apps were updated.")),
        (51, 4, 150, Toast("Download finished", "Your game is ready to play.")),
        (52, 4, 300, Toast("Wish list", "An item on your wish list is on sale.")),
    ];

    /// <summary>
    /// A picture file of <paramref name="width"/> by <paramref name="height"/>, a diagonal from one colour
    /// to another (BMP, which WIC reads), in <paramref name="folder"/>: a toast's logo, hero or inline image.
    /// </summary>
    internal static string Picture(string folder, string name, int width, int height, uint from, uint to)
    {
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, name + ".bmp");
        var stride = ((width * 3) + 3) & ~3;
        var data = new byte[54 + (stride * height)];
        void Put(int at, int value) => BitConverter.GetBytes(value).CopyTo(data, at);
        data[0] = (byte)'B';
        data[1] = (byte)'M';
        Put(2, data.Length);
        Put(10, 54);
        Put(14, 40);
        Put(18, width);
        Put(22, height);
        data[26] = 1;
        data[28] = 24;
        Put(34, stride * height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var t = (x + (float)y) / (width + height);
                var at = 54 + (y * stride) + (x * 3);
                for (var channel = 0; channel < 3; channel++)
                {
                    var a = (from >> (8 * channel)) & 0xFF;
                    var b = (to >> (8 * channel)) & 0xFF;
                    data[at + channel] = (byte)(a + ((b - (float)a) * t));
                }
            }
        }
        File.WriteAllBytes(path, data);
        return path;
    }

    /// <summary>A toast with its own logo, a hero and an inline picture, in that order of what it carries; each null for none.</summary>
    internal static string Pictured(string title, string body, string? logo, string? hero, string? inline, string actions = "")
    {
        static string Image(string? path, string placement) => path is null ? "" : $"<image{placement} src=\"{new Uri(path).AbsoluteUri}\"/>";
        return $"<toast><visual><binding template=\"ToastGeneric\"><text>{title}</text><text>{body}</text>{Image(logo, " placement=\"appLogoOverride\"")}"
            + $"{Image(hero, " placement=\"hero\"")}{Image(inline, "")}</binding></visual>{(actions.Length > 0 ? $"<actions>{actions}</actions>" : "")}</toast>";
    }

    /// <summary>A database of Windows' shape holding <paramref name="rows"/>, in a folder of the test's own.</summary>
    private static void Build(string path, IEnumerable<(long Id, int App, int Minutes, string Payload)> rows)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        Assert.Equal(Sqlite.OK, Sqlite.Open(path, Sqlite.OPEN_READWRITE | Sqlite.OPEN_CREATE, out var db));
        try
        {
            var now = DateTime.UtcNow.ToFileTimeUtc();
            var sql = new StringBuilder(
                "CREATE TABLE Notification (\"Order\" INTEGER PRIMARY KEY AUTOINCREMENT, Id INTEGER NOT NULL, HandlerId INTEGER, Type TEXT NOT NULL, "
                + "Payload BLOB, ExpiryTime INT64, ArrivalTime INT64, PayloadType TEXT NOT NULL);"
                + "CREATE TABLE NotificationHandler (RecordId INTEGER PRIMARY KEY, PrimaryId TEXT NOT NULL); INSERT INTO NotificationHandler VALUES (1, 'chat'), (2, 'mail'), (3, 'calendar'), (4, 'store');");
            foreach (var (id, app, minutes, payload) in rows)
            {
                sql.Append($"INSERT INTO Notification (Id, HandlerId, Type, Payload, ExpiryTime, ArrivalTime, PayloadType) VALUES ({id}, {app}, 'toast', "
                    + $"X'{Convert.ToHexString(Encoding.UTF8.GetBytes(payload))}', 0, {now - minutes * TimeSpan.TicksPerMinute}, 'Xml');");
            }
            Assert.Equal(Sqlite.OK, Sqlite.Execute(db, sql.ToString()));
        }
        finally
        {
            Sqlite.Close(db);
        }
    }

    /// <summary>A square of one colour, an app's icon.</summary>
    private static Pixels Solid(uint argb)
    {
        var data = new byte[64 * 64 * 4];
        for (var i = 0; i < data.Length; i += 4)
        {
            data[i] = (byte)argb;
            data[i + 1] = (byte)(argb >> 8);
            data[i + 2] = (byte)(argb >> 16);
            data[i + 3] = (byte)(argb >> 24);
        }
        return new Pixels { Data = data, Width = 64, Height = 64 };
    }

    /// <summary>A store over a database of <paramref name="rows"/> in <paramref name="folder"/>: chat's icon purple, mail's blue, the calendar's (app 3) and the store's (app 4) green and orange.</summary>
    internal static NotificationStore Store(string folder, IEnumerable<(long Id, int App, int Minutes, string Payload)> rows, string name = "db",
        Func<string, (string Name, Pixels? Icon)>? resolve = null)
    {
        var database = Path.Combine(folder, name, "wpndatabase.db");
        Build(database, rows);
        var store = new NotificationStore(database, Path.Combine(folder, name + "-copy"), Path.Combine(folder, name + "-state"),
            resolve ?? (app => app switch
            {
                "chat" => ("Chat", Solid(0xFF5B5FC7)),
                "calendar" => ("Calendar", Solid(0xFF107C41)),
                "store" => ("Store", Solid(0xFFCA5010)),
                _ => ("Mail", Solid(0xFF0F6CBD)),
            }));
        store.Prepare();
        return store;
    }

    /// <summary>The preset's folder over <paramref name="store"/>, never Windows' own.</summary>
    private static FolderIsland Made(Gfx gfx, NotificationStore store, string? key = null)
    {
        var holder = Holder();
        NotificationPartTile.Stand = store;
        try
        {
            return new FolderIsland(key ?? $"{Owner}/{holder.Id}", holder, gfx, new Config());
        }
        finally
        {
            NotificationPartTile.Stand = null;
        }
    }

    /// <summary>The line, alone, over <paramref name="store"/> and a made-up do not disturb.</summary>
    private static NotificationPartTile Line(Gfx gfx, NotificationStore store, DoNotDisturb? quiet = null)
    {
        var cell = ListKind.NotificationsList.New();
        cell.Id = "notifications-" + Guid.NewGuid().ToString("N")[..6];
        var line = new NotificationPartTile(Config.IslandKey(Owner, cell.Id), cell, gfx, new Config(), store, quiet);
        line.StandIn(ListWidgets.Of(ListKind.NotificationsList));
        return line;
    }

    private static TileStyle StyleFor(float scale, Look look) => FolderFlyout.StyleFor(scale, look, FlyoutTheme.For(look));

    /// <summary>The line as tall as it asks to be, at the list's own width.</summary>
    private static D2D_RECT_F Whole(NotificationPartTile line, in TileStyle style)
    {
        var width = FolderFlyout.ListWidth(style.Scale);
        return PopupPaint.Rect(0, 0, width, line.ListHeight(width, style));
    }

    private static IReadOnlyList<LinePiece> Pieces(NotificationPartTile line, D2D_RECT_F box, in TileStyle style) =>
        ((ILinePieces)line).LinePieces(box, style);

    private static Vector2 Middle(D2D_RECT_F box) => new((box.left + box.right) / 2, (box.top + box.bottom) / 2);

    /// <summary>The pointer onto <paramref name="piece"/>, and what is under it.</summary>
    private static (int Id, NotificationPartTile.Target Target) Point(NotificationPartTile line, D2D_RECT_F box, in TileStyle style, string piece)
    {
        var all = Pieces(line, box, style);
        // A notification's chevron and × stand only under the pointer: the pointer onto the notification first.
        if (!all.Any(each => each.Name == piece) && piece.Split(' ') is ["chevron" or "close", var number])
        {
            line.CardHover(Middle(Assert.Single(all, each => each.Name == $"title {number}").Box), box, style, WidgetSurface.List);
            all = Pieces(line, box, style);
        }
        Assert.True(all.Count(each => each.Name == piece) == 1, $"{piece} in {string.Join(", ", all.Select(each => each.Name))}");
        var at = Middle(Assert.Single(all, each => each.Name == piece).Box);
        line.CardHover(at, box, style, WidgetSurface.List);
        var id = line.CardTarget(at, box, style, WidgetSurface.List);
        return (id, line.TargetOf(id));
    }

    /// <summary>A click on <paramref name="piece"/>, as the folder gives it: the pointer onto it, then its number.</summary>
    private static IslandAction Click(NotificationPartTile line, in TileStyle style, string piece)
    {
        var box = Whole(line, style);
        var (id, _) = Point(line, box, style, piece);
        Assert.NotEqual(0, id);
        return line.Click(id);
    }

    internal static void Clean(string folder)
    {
        try
        {
            Directory.Delete(folder, recursive: true);
        }
        catch (IOException)
        {
            // A store may still hold its copy open; the temp folder keeps it until then.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    internal static string Temp() => Path.Combine(Path.GetTempPath(), "karpo-tests", "notifications-preset-" + Guid.NewGuid().ToString("N"));

    // --- The preset ------------------------------------------------------------------------------------

    /// <summary>
    /// The bell's flyout, made of widgets: the notification list's line, as tall as its notifications up
    /// to the most a line may be, and under it in the same card the premade notification actions — under the bell
    /// face, whose one option is the bell's count, on.
    /// </summary>
    [Fact]
    public void ThePresetIsTheListOverItsFooterUnderTheBell()
    {
        var preset = Presets.Find(Presets.NotificationsId)!;
        Assert.Equal(FolderFaces.NotificationsId, preset.Face!.Kind);
        Assert.Equal(["notificationsList", "actions"], preset.Items.Select(item => item.Form));
        Assert.Equal(LineHeight.Automatic(FolderOptions.MostCells), preset.Items[0].Height);
        Assert.Same(PremadeWidgets.NotificationActions, preset.Items[^1].Premade);

        var folder = Holder().Folder!;
        Assert.Same(FolderFaces.Notifications, folder.Face.Info);
        Assert.Contains(FolderFaces.Notifications, FolderFaces.All);
        Assert.True(NotificationsFace.ShowsCount(folder.Face));
        Assert.Equal(WidgetInfos.NotificationsList.Glyph, folder.Face.Info.Glyph);
        Assert.Null(Presets.Find("notificationCenterGroup"));
    }

    /// <summary>
    /// The bell is gone from the bar, its options and its flyout height with it; the line is all there
    /// is, its click on a card an app opening, and a selection in edit mode opens no folder.
    /// </summary>
    [Fact]
    public void TheBellIsGoneAndTheLineActs()
    {
        Assert.Null(BarEntries.Find("notifications"));
        Assert.DoesNotContain("notifications", Enum.GetValues<BarKind>().Select(kind => kind.ToString().ToLowerInvariant()));
        Assert.DoesNotContain(WidgetInfos.All, info => info.Id == "notifications");
        Assert.Equal(CellAct.Action, ListWidgets.Of(ListKind.NotificationsList).Act);
        Assert.Contains(BarKind.FolderList, SelectedFlyout.Never);
        Assert.True(Config.Shape >= 35);
    }

    // --- The line --------------------------------------------------------------------------------------

    /// <summary>
    /// Grouped by app, as Windows 11's is: the app something last came from first, its heading over its
    /// newest. A long group lists every one of its notifications — no "See more (2)", no "See fewer"
    /// (the person, 2026-10-31: "when a folder has more items to show it should actually not say that but
    /// just scroll"): held shorter than all of them, the line scrolls, and at its end the last shows.
    /// </summary>
    [UiFact]
    public void ALongGroupListsEveryNotificationAndTheLineScrolls()
    {
        var folder = Temp();
        try
        {
            Offscreen.OnStaThread(gfx =>
            {
                var store = Store(folder, Centre);
                using var line = Line(gfx, store);
                var style = StyleFor(1f, LookFor(dark: true));
                var pieces = Pieces(line, Whole(line, style), style);
                var headings = pieces.Where(piece => piece.Name.StartsWith("heading ", StringComparison.Ordinal) && !piece.Name.StartsWith("heading close", StringComparison.Ordinal)).ToList();
                Assert.Equal(["Chat", "Mail"], headings.Select(piece => piece.Text));
                var titles = pieces.Where(piece => piece.Name.StartsWith("title ", StringComparison.Ordinal)).Select(piece => piece.Text).ToList();
                Assert.Equal(["Ada", "Charles Babbage", "Grace Hopper", "Weekly report", "Invoice", "Newsletter", "Receipt"], titles);
                Assert.DoesNotContain(pieces, piece => piece.Name.StartsWith("fold", StringComparison.Ordinal));

                // Four rows: the line scrolls, and scrolled to its end the last notification shows whole.
                var box = PopupPaint.Rect(0, 0, FolderFlyout.ListWidth(style.Scale), Rows(4, style.Scale, 1f));
                Assert.True(line.Scrolled(box, style, WidgetSurface.List).Scrolls, "seven notifications do not fit four rows");
                line.ListScroll = float.MaxValue;
                var end = line.Scrolled(box, style, WidgetSurface.List);
                var shown = Pieces(line, end.Moved, style);
                Assert.DoesNotContain(shown, piece => piece.Name.StartsWith("fold", StringComparison.Ordinal));
                var last = Assert.Single(shown, piece => piece.Name.StartsWith("title ", StringComparison.Ordinal) && piece.Text == "Receipt").Box;
                Assert.True(last.top >= end.Window.top - 0.5f && last.bottom <= box.bottom + 0.5f, "scrolled to its end, the last notification shows");
            });
        }
        finally
        {
            Clean(folder);
        }
    }

    /// <summary>
    /// The line is the kit's, as mail's and the feed's are (the person, 2026-10-30: "notifications dont look good yet. they
    /// should be redesigned with the edge to edge style as well"): the Header — the bell in the leading column,
    /// "Notifications" strong at the text edge, "Clear all" ending on the trailing gutter — over each app's heading, its
    /// icon in the leading column and its name at the text edge, over its notifications as the kit's items: no card, their
    /// words at the text edge, each its band of the line, edge to edge, touching the next; under the pointer its × on the
    /// trailing gutter, its own target inside the band.
    /// </summary>
    [UiFact]
    public void TheLineIsTheKitsHeaderOverItsItems()
    {
        var folder = Temp();
        try
        {
            Offscreen.OnStaThread(gfx =>
            {
                var store = Store(folder, Centre);
                using var line = Line(gfx, store);
                foreach (var scale in new[] { 1f, 1.5f })
                {
                    var style = StyleFor(scale, LookFor(dark: true));
                    var form = ListForm.Of(gfx, style);
                    var box = Whole(line, style);
                    var pieces = Pieces(line, box, style);
                    bool InColumn(D2D_RECT_F at) => (at.left + at.right) / 2 is var middle && middle > form.Inset - 0.5f && middle < form.TextLeft - 0.5f;
                    Assert.DoesNotContain(pieces, piece => piece.Name.StartsWith("card ", StringComparison.Ordinal));
                    Assert.True(InColumn(Assert.Single(pieces, piece => piece.Name == "glyph").Box), "the bell in the leading column");
                    var head = Assert.Single(pieces, piece => piece.Name == "head");
                    Assert.Equal(S.WidgetNotificationsName, head.Text);
                    Assert.Equal(form.TextLeft, head.Box.left, 1);
                    Assert.Equal(box.right - form.Inset, Assert.Single(pieces, piece => piece.Name == "clear all plate").Box.right, 1);
                    Assert.True(InColumn(Assert.Single(pieces, piece => piece.Name == "app chat").Box), "chat's icon in the leading column");
                    Assert.Equal(form.TextLeft, Assert.Single(pieces, piece => piece.Name == "heading chat").Box.left, 1);
                    var words = pieces.Where(piece => piece.Name.StartsWith("title ", StringComparison.Ordinal) || piece.Name.StartsWith("body ", StringComparison.Ordinal)).ToList();
                    Assert.Equal(14, words.Count);
                    Assert.All(words, piece => Assert.Equal(form.TextLeft, piece.Box.left, 1));

                    // The bands, down the line's leading edge: chat's two touching, mail's five touching (§461: all of them).
                    var bands = Bands(line, box, style);
                    Assert.Equal([31L, 32, 33, 34, 35, 36, 37], bands.Select(band => band.Item));
                    Assert.Equal(bands[0].Bottom, bands[1].Top);
                    for (var next = 3; next < bands.Count; next++)
                    {
                        Assert.Equal(bands[next - 1].Bottom, bands[next].Top);
                    }
                    foreach (var (item, top, bottom) in bands)
                    {
                        // Edge to edge: the line's trailing edge on the item's middle row is the item's too.
                        var target = line.TargetOf(line.CardTarget(new Vector2(box.right - 0.5f, (top + bottom) / 2f), box, style, WidgetSurface.List));
                        Assert.Equal((NotificationPartTile.Spot.Body, item), (target.Spot, target.Item));
                    }

                    // Under the pointer, the × on the trailing gutter, inside the band, its own target.
                    var index = bands.FindIndex(band => band.Item == 33);
                    Point(line, box, style, $"title {index}");
                    var cross = Assert.Single(Pieces(line, box, style), piece => piece.Name == $"close {index}").Box;
                    Assert.Equal(box.right - form.Inset, cross.right, 1);
                    Assert.True(cross.top >= bands[index].Top - 0.5f && cross.bottom <= bands[index].Bottom + 0.5f, $"the × {cross.top}-{cross.bottom} out of its band {bands[index].Top}-{bands[index].Bottom}");
                    Assert.Equal(new NotificationPartTile.Target(NotificationPartTile.Spot.Close, "mail", 33), Point(line, box, style, $"close {index}").Target);
                    line.CardHover(null, box, style, WidgetSurface.List);
                }
            });
        }
        finally
        {
            Clean(folder);
        }
    }

    /// <summary>
    /// A notification's band as tall as what it shows (the person, 2026-10-30, §436: "it should still have the same size as
    /// the other items"): two of one line's body the same; one whose body runs to two lines a body line taller, never more
    /// — the body is cut to two; one with buttons taller by its row of buttons and the gap over it. Its buttons, Windows'
    /// own, are the line's standard buttons under its body at the text edge, inside its band.
    /// </summary>
    [UiFact]
    public void ANotificationIsAsTallAsWhatItShows()
    {
        var folder = Temp();
        try
        {
            Offscreen.OnStaThread(gfx =>
            {
                var store = Store(folder, []);
                using var line = Line(gfx, store);
                line.Shown = ItemHoverSweepTests.Sampled(Clock.Now());
                foreach (var scale in new[] { 1f, 1.5f })
                {
                    var style = StyleFor(scale, LookFor(dark: true));
                    var items = ItemForm.For(gfx, style, line.Settings);
                    var form = ListForm.Of(gfx, style);
                    var buttons = LineButton.Of(gfx, style);
                    var box = Whole(line, style);
                    var bands = Bands(line, box, style);
                    Assert.Equal([1L, 2, 3, 4], bands.Select(band => band.Item));
                    float Tall(int at) => bands[at].Bottom - bands[at].Top;
                    Assert.Equal(Tall(0), Tall(3));
                    Assert.Equal(Tall(0) + items.SmallLine, Tall(1), 1.5f);
                    Assert.Equal(Tall(0) + buttons.Height + style.Px(LineSpace.GapDip), Tall(2), 1.5f);
                    var pieces = Pieces(line, box, style);
                    var body = Assert.Single(pieces, piece => piece.Name == "body 1");
                    Assert.EndsWith("…", body.Text);
                    Assert.Equal(2 * items.SmallLine, body.Box.bottom - body.Box.top, 1.5f);
                    var shown = pieces.Where(piece => piece.Name.StartsWith("part 2 ", StringComparison.Ordinal) && !piece.Name.EndsWith(" plate", StringComparison.Ordinal)).ToList();
                    Assert.Equal(["Reply", "Mark as read"], shown.Select(piece => piece.Text));
                    Assert.Equal(form.TextLeft, shown[0].Box.left, 1);
                    Assert.All(pieces.Where(piece => piece.Name.StartsWith("part 2 ", StringComparison.Ordinal) && piece.Name.EndsWith(" plate", StringComparison.Ordinal)),
                        plate => Assert.Equal("Standard", plate.Text));
                    Assert.All(shown, piece => Assert.True(piece.Box.top > Assert.Single(pieces, each => each.Name == "body 2").Box.bottom - 0.5f
                        && piece.Box.bottom <= bands[2].Bottom + 0.5f));
                }
            });
        }
        finally
        {
            Clean(folder);
        }
    }

    /// <summary>
    /// The line does not say how many notifications there are (2026-10-31, the person: "notifications widget should not
    /// show the amount of notifications on the top"): its header, whole or on one row, is the bell, "Notifications", do not
    /// disturb's toggle and "Clear all", no number beside them.
    /// </summary>
    [UiFact]
    public void TheLineSaysNotHowMany()
    {
        var folder = Temp();
        try
        {
            Offscreen.OnStaThread(gfx =>
            {
                var store = Store(folder, Centre);
                using var line = Line(gfx, store, new DoNotDisturb(new SampleDoNotDisturb(on: false)));
                foreach (var scale in new[] { 1f, 1.5f })
                {
                    var style = StyleFor(scale, LookFor(dark: true));
                    var whole = Whole(line, style);
                    var one = PopupPaint.Rect(0, 0, whole.right, LineRows.Px(1, style));
                    foreach (var box in new[] { whole, one })
                    {
                        var pieces = Pieces(line, box, style);
                        Assert.DoesNotContain(pieces, piece => piece.Name == "count");
                        Assert.Equal(S.WidgetNotificationsName, Assert.Single(pieces, piece => piece.Name == "head").Text);
                        Assert.DoesNotContain(pieces, piece => piece.Text.Contains(Centre.Length.ToString(global::System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal));
                        Assert.Contains(pieces, piece => piece.Name == "clear all");
                        Assert.Contains(pieces, piece => piece.Name == "quiet");
                    }
                }
            });
        }
        finally
        {
            Clean(folder);
        }
    }

    /// <summary>With nothing to show, the line is the kit's Status, one row: the bell and "No new notifications".</summary>
    [UiFact]
    public void NothingNewIsTheKitsStatus()
    {
        var folder = Temp();
        try
        {
            Offscreen.OnStaThread(gfx =>
            {
                var store = Store(folder, []);
                using var line = Line(gfx, store, new DoNotDisturb(new SampleDoNotDisturb(on: false)));
                var style = StyleFor(1.5f, LookFor(dark: true));
                var box = Whole(line, style);
                Assert.Equal(LineRows.Px(1, style), box.bottom, 0.5f);
                line.PaintCard(null, box, style, WidgetSurface.List);
                var pieces = Pieces(line, box, style);
                Assert.Equal(S.WidgetNotificationsNone, Assert.Single(pieces, piece => piece.Name == "status").Text);
                Assert.Contains(pieces, piece => piece.Name == "status glyph");
                Assert.Contains(pieces, piece => piece.Name == "quiet");
            });
        }
        finally
        {
            Clean(folder);
        }
    }

    /// <summary>The notifications' bands down the line's leading edge, in order: which each is, from where to where (px).</summary>
    private static List<(long Item, int Top, int Bottom)> Bands(NotificationPartTile line, D2D_RECT_F box, in TileStyle style)
    {
        var bands = new List<(long Item, int Top, int Bottom)>();
        for (var y = 0; y < (int)box.bottom; y++)
        {
            var target = line.TargetOf(line.CardTarget(new Vector2(0.5f, y + 0.5f), box, style, WidgetSurface.List));
            if (target.Spot != NotificationPartTile.Spot.Body)
            {
                continue;
            }
            if (bands.Count > 0 && bands[^1].Item == target.Item && bands[^1].Bottom == y)
            {
                bands[^1] = bands[^1] with { Bottom = y + 1 };
            }
            else
            {
                bands.Add((target.Item, y, y + 1));
            }
        }
        return bands;
    }

    /// <summary>
    /// The kit's clamps, as mail's: a title on one line and a body on two, cut with an ellipsis, the
    /// chevron where either says more, and all of both once expanded. A short toast has no chevron.
    /// </summary>
    [UiFact]
    public void ATitleTakesOneLineAndABodyTwoUntilExpanded()
    {
        var folder = Temp();
        try
        {
            Offscreen.OnStaThread(gfx =>
            {
                var longTitle = "The analytical engine weaves algebraic patterns just as the Jacquard loom weaves flowers and leaves";
                var store = Store(folder, [(71, 1, 1, Toast(longTitle, LongBody + " " + LongBody)), (72, 1, 2, Toast("Short", "Fits."))]);
                using var line = Line(gfx, store);
                foreach (var text in TextScales)
                {
                    var style = StyleFor(1f, LookFor(dark: true, text));
                    var items = ItemForm.For(gfx, style, line.Settings);
                    int Lines(string name, float px, Weight weight)
                    {
                        var piece = Assert.Single(Pieces(line, Whole(line, style), style), each => each.Name == name);
                        return (int)MathF.Round((piece.Box.bottom - piece.Box.top) / TilePaint.LineHeight(gfx, px, weight));
                    }
                    var shownTitle = Assert.Single(Pieces(line, Whole(line, style), style), each => each.Name == "title 0");
                    Assert.True(NotificationPartTile.TitleLines == Lines("title 0", items.TextPx, Weight.Semibold),
                        $"text {text}: {shownTitle.Text} {shownTitle.Box.left},{shownTitle.Box.top},{shownTitle.Box.right},{shownTitle.Box.bottom}, line {TilePaint.LineHeight(gfx, items.TextPx, Weight.Semibold)}");
                    Assert.Equal(NotificationPartTile.BodyLines, Lines("body 0", items.SmallPx, Weight.Regular));
                    Assert.EndsWith("…", Assert.Single(Pieces(line, Whole(line, style), style), each => each.Name == "body 0").Text);
                    Point(line, Whole(line, style), style, "title 1");
                    Assert.DoesNotContain(Pieces(line, Whole(line, style), style), piece => piece.Name == "chevron 1");
                    Click(line, style, "chevron 0");
                    Assert.True(Lines("body 0", items.SmallPx, Weight.Regular) > NotificationPartTile.BodyLines);
                    Assert.Equal(longTitle, Assert.Single(Pieces(line, Whole(line, style), style), each => each.Name == "title 0").Text);
                    Click(line, style, "chevron 0");
                }
            });
        }
        finally
        {
            Clean(folder);
        }
    }

    /// <summary>
    /// Nothing stands on anything, and everything of a notification stays inside its band and between
    /// the gutters: four apps, a logo, a hero, an inline picture, an expanded notification with its box
    /// and buttons, one under the pointer with its chevron and ×, at the list's width and narrower, 100
    /// and 150 %, ordinary and 225 % text.
    /// </summary>
    [UiFact]
    public void NothingOverlapsAtNarrowWidthsAndLargeText()
    {
        var folder = Temp();
        try
        {
            Offscreen.OnStaThread(gfx =>
            {
                var images = Path.Combine(folder, "images");
                var face = Picture(images, "face", 96, 96, 0xFFE8A87C, 0xFF85586F);
                var wide = Picture(images, "hero", 728, 360, 0xFF2E86AB, 0xFFF6C85F);
                var photo = Picture(images, "photo", 480, 320, 0xFF6A994E, 0xFFBC4749);
                var rows = Centre.Concat(
                [
                    (81, 3, 3, Pictured("Your trip to Lisbon", "Check in is open.", null, wide, null, "<action content=\"Check in\" arguments=\"c\"/>")),
                    (82, 4, 4, Pictured("Ada Lovelace", "Here is the diagram.", face, null, photo)),
                ]);
                var store = Store(folder, rows);
                using var line = Line(gfx, store);
                var wrong = new List<string>();
                foreach (var scale in new[] { 1f, 1.5f })
                {
                    foreach (var text in TextScales)
                    {
                        var style = StyleFor(scale, LookFor(dark: true, text));
                        foreach (var dip in new[] { 360f, 280f, 220f })
                        {
                            var width = PopupPaint.Round(dip * scale);
                            var box = PopupPaint.Rect(0, 0, width, line.ListHeight(width, style));
                            if (line.View.Expanded.Count == 0)
                            {
                                Point(line, box, style, "chevron 0");
                                line.Click(line.CardTarget(Middle(Assert.Single(Pieces(line, box, style), piece => piece.Name == "chevron 0").Box), box, style, WidgetSurface.List));
                                box = PopupPaint.Rect(0, 0, width, line.ListHeight(width, style));
                            }
                            Point(line, box, style, "title 1");
                            var at = $"{dip} DIP at {scale * 100:0} %, text {text * 100:0} %";
                            var pieces = Pieces(line, box, style);
                            var gutter = ListForm.Gutter(style);
                            var bands = Bands(line, box, style);
                            foreach (var piece in pieces)
                            {
                                if (piece.Box.left < gutter - 0.5f || piece.Box.right > width - gutter + 0.5f || piece.Box.top < -0.5f || piece.Box.bottom > box.bottom + 0.5f)
                                {
                                    wrong.Add($"{at}: {piece.Name} past the gutters or the line");
                                }
                                var number = piece.Name.Split(' ') is [_, var n, ..] && int.TryParse(n, out var index) ? index : -1;
                                if (number >= 0 && number < bands.Count && (piece.Box.top < bands[number].Top - 0.5f || piece.Box.bottom > bands[number].Bottom + 0.5f))
                                {
                                    wrong.Add($"{at}: {piece.Name} outside its band");
                                }
                            }
                            var standing = pieces.Where(piece => !piece.Behind && piece.Box.right > piece.Box.left && piece.Box.bottom > piece.Box.top).ToList();
                            for (var one = 0; one < standing.Count; one++)
                            {
                                for (var other = one + 1; other < standing.Count; other++)
                                {
                                    var (a, b) = (standing[one].Box, standing[other].Box);
                                    if (a.left < b.right - 0.5f && b.left < a.right - 0.5f && a.top < b.bottom - 0.5f && b.top < a.bottom - 0.5f)
                                    {
                                        wrong.Add($"{at}: {standing[one].Name} and {standing[other].Name} overlap");
                                    }
                                }
                            }
                            Assert.Contains(pieces, piece => piece.Name == "close 1");
                        }
                    }
                }
                Assert.True(wrong.Count == 0, string.Join("\n", wrong.Take(40)));
                Assert.Equal(7 + 2, store.Items.Count);
            });
        }
        finally
        {
            Clean(folder);
        }
    }

    /// <summary>
    /// Under the pointer, a notification with more to it than its two lines has the chevron, and so
    /// does one with a box; the chevron expands it — the whole body; the box, the selection and every
    /// button — and folds it again. One of words that fit has none. Folded, its buttons but the box's
    /// stand under its words.
    /// </summary>
    [UiFact]
    public void TheChevronExpandsANotification()
    {
        var folder = Temp();
        try
        {
            Offscreen.OnStaThread(gfx =>
            {
                var store = Store(folder, Centre);
                using var line = Line(gfx, store);
                var style = StyleFor(1f, LookFor(dark: true));
                bool Has(string title, string piece)
                {
                    Point(line, Whole(line, style), style, title);
                    return Pieces(line, Whole(line, style), style).Any(each => each.Name == piece);
                }
                Assert.True(Has("title 0", "chevron 0"));
                Assert.True(Has("title 1", "chevron 1"));
                Assert.False(Has("title 2", "chevron 2"));
                static bool Part(LinePiece piece) => piece.Name.StartsWith("part 0 ", StringComparison.Ordinal) && !piece.Name.EndsWith(" plate", StringComparison.Ordinal);
                var pieces = Pieces(line, Whole(line, style), style);
                // The reply folded: Like, Open the thread and Dismiss; no box, no Send.
                Assert.Equal(["Like", "Open the thread", "Dismiss"], pieces.Where(Part).Select(piece => piece.Text));
                var folded = Assert.Single(pieces, piece => piece.Name == "body 1").Box;

                Click(line, style, "chevron 1");
                Click(line, style, "chevron 0");
                pieces = Pieces(line, Whole(line, style), style);
                Assert.True(Assert.Single(pieces, piece => piece.Name == "body 1").Box.bottom - Assert.Single(pieces, piece => piece.Name == "body 1").Box.top > folded.bottom - folded.top);
                // The reply: its box and its selection, then Send, Like, Open the thread and Dismiss.
                Assert.Equal(6, pieces.Count(Part));
                Assert.Equal("Remind me", Assert.Single(pieces, piece => piece.Name == "label 0 1").Text);

                Click(line, style, "chevron 0");
                Assert.Equal(3, Pieces(line, Whole(line, style), style).Count(Part));
            });
        }
        finally
        {
            Clean(folder);
        }
    }

    /// <summary>
    /// A click on a card brings its app forward with the toast's own launch arguments: it is an app
    /// opening for the folder (it goes), and the dock hands the activation on once it has gone; the
    /// notification is cleared. A background button (Like) is handed on at once and the folder stays.
    /// Never a real app: the activator is the test's.
    /// </summary>
    [UiFact]
    public void ACardAndItsButtonsActivateTheApp()
    {
        var folder = Temp();
        try
        {
            Offscreen.OnStaThread(gfx =>
            {
                var activator = new FakeActivator();
                ToastActivator.Made = activator;
                try
                {
                    var store = Store(folder, Centre);
                    using var line = Line(gfx, store);
                    var style = StyleFor(1f, LookFor(dark: true));

                    var opened = Click(line, style, "title 1");
                    Assert.Equal(IslandView.App, opened.View);
                    Assert.True(FolderFlyout.Forwards(opened, ListWidgets.Of(ListKind.NotificationsList).Act));
                    Assert.Empty(activator.Activated);
                    Assert.Equal("action=view&id=77", line.Pending!.Arguments);
                    line.ActivatePending();
                    var launch = Assert.Single(activator.Activated);
                    Assert.Equal(("chat", "action=view&id=77", ToastActivationKind.Foreground), (launch.AppId, launch.Arguments, launch.Kind));
                    Assert.Null(line.Pending);
                    Assert.DoesNotContain(store.Items, item => item.Id == 32);

                    Click(line, style, "chevron 0");
                    var liked = Click(line, style, "part 0 3");
                    Assert.Equal(default, liked);
                    Assert.Equal(2, activator.Activated.Count);
                    Assert.Equal(("action=like&id=9318", ToastActivationKind.Background), (activator.Activated[1].Arguments, activator.Activated[1].Kind));
                    Assert.DoesNotContain(store.Items, item => item.Id == 31);
                }
                finally
                {
                    ToastActivator.Made = null;
                }
            });
        }
        finally
        {
            Clean(folder);
        }
    }

    /// <summary>
    /// Regression (the person, 2026-10-23: "when clicking on a notification nothing happens"): a click
    /// on a card in the open folder, made as the person makes it, closes the folder and still reaches
    /// the app. The folder hides before it forwards the click (FolderFlyout.Activate), and the line
    /// dropped what it was to activate as it was hidden, so the dock had nothing left to hand on.
    /// </summary>
    [UiFact]
    public void AClickOnACardInTheOpenFolderStillReachesItsAppOnceTheFolderHasGone()
    {
        var folder = Temp();
        try
        {
            WindowShots.OnHiddenDesktop("notifications-click", (gfx, instance) =>
            {
                var activator = new FakeActivator();
                ToastActivator.Made = activator;
                var held = KeyState.Held;
                KeyState.Held = static _ => false;
                try
                {
                    var store = Store(folder, Centre);
                    using var island = Made(gfx, store);
                    using var flyout = new FolderFlyout(gfx, instance, 0);
                    var look = LookFor(dark: true);
                    flyout.Show(gfx, look, 1f, new POINT { x = 300, y = 1040 }, new RECT { right = 1920, bottom = 1040 }, Karpo.Core.Edge.Bottom, island, FlyoutAlign.Start);
                    var line = Assert.IsType<NotificationPartTile>(island.Tiles[0].Island);
                    var card = flyout.Plan!.Cards[0];
                    var style = StyleFor(1f, look);
                    var title = Middle(Assert.Single(Pieces(line, PopupPaint.Rect(0, 0, card.right - card.left, card.bottom - card.top), style), piece => piece.Name == "title 1").Box);
                    var panel = flyout.PanelOnScreen;
                    var point = new POINT { x = panel.left + (int)(card.left + title.X), y = panel.top + (int)(card.top + title.Y) };
                    Assert.True(TerraFX.Interop.Windows.Windows.ScreenToClient(flyout.Hwnd, &point));
                    var at = (LPARAM)(nint)((point.y << 16) | (point.x & 0xFFFF));

                    _ = flyout.Message(WM.WM_MOUSEMOVE, 0, at);
                    _ = flyout.Message(WM.WM_LBUTTONDOWN, 1, at);
                    var clicked = flyout.Message(WM.WM_LBUTTONUP, 0, at);

                    Assert.Equal(FolderFlyoutAction.Forward, clicked.Kind);
                    Assert.Equal(IslandView.App, clicked.Action.View);
                    Assert.False(flyout.IsOpen);
                    Assert.Same(line, clicked.Widget);
                    Assert.Empty(activator.Activated);
                    // What the dock does once the folder has gone (Dock.FolderForward).
                    line.ActivatePending();
                    var launch = Assert.Single(activator.Activated);
                    Assert.Equal(("chat", "action=view&id=77", ToastActivationKind.Foreground), (launch.AppId, launch.Arguments, launch.Kind));
                    Assert.DoesNotContain(store.Items, item => item.Id == 32);
                }
                finally
                {
                    KeyState.Held = held;
                    ToastActivator.Made = null;
                }
            });
        }
        finally
        {
            Clean(folder);
        }
    }

    /// <summary>
    /// The reply box takes the keys from a click (the folder hands them to a line typing, Escape
    /// first): what is typed is the box's, Enter sends it with the button it belongs to and every
    /// input's value, in the background, and the folder stays. Escape lets the keys go — the box
    /// keeps what was typed — and a second Escape is the folder's again: typing, then the folder.
    /// </summary>
    [UiFact]
    public void AReplyIsTypedAndSentWithEnter()
    {
        var folder = Temp();
        try
        {
            Offscreen.OnStaThread(gfx =>
            {
                var activator = new FakeActivator();
                ToastActivator.Made = activator;
                try
                {
                    var store = Store(folder, Centre);
                    using var line = Line(gfx, store);
                    var style = StyleFor(1f, LookFor(dark: true));
                    line.PaintCard(null, Whole(line, style), style, WidgetSurface.List);
                    Click(line, style, "chevron 0");
                    Assert.False(line.CardTyping);
                    Click(line, style, "part 0 0");
                    Assert.True(line.CardTyping);
                    Assert.Equal(500, line.IntervalMs);
                    foreach (var c in "On my way")
                    {
                        Assert.True(line.CardChar(c, WidgetSurface.List));
                    }
                    Assert.True(line.CardKey(VK.VK_ESCAPE, WidgetSurface.List));
                    Assert.False(line.CardTyping);
                    Assert.False(line.CardKey(VK.VK_ESCAPE, WidgetSurface.List));
                    Assert.Empty(activator.Activated);

                    Click(line, style, "part 0 0");
                    Assert.True(line.CardTyping);
                    Assert.True(line.CardKey(VK.VK_RETURN, WidgetSurface.List));
                    var sent = Assert.Single(activator.Activated);
                    Assert.Equal(("action=reply&id=9318", ToastActivationKind.Background), (sent.Arguments, sent.Kind));
                    Assert.Contains(new KeyValuePair<string, string>("reply", "On my way"), sent.Inputs);
                    Assert.Contains(new KeyValuePair<string, string>("when", "15"), sent.Inputs);
                    Assert.False(line.CardTyping);
                    Assert.Equal(0, line.IntervalMs);
                    Assert.DoesNotContain(store.Items, item => item.Id == 31);
                }
                finally
                {
                    ToastActivator.Made = null;
                }
            });
        }
        finally
        {
            Clean(folder);
        }
    }

    /// <summary>
    /// A heading's ×, which shows only under the pointer, clears the app's notifications; a card's ×
    /// clears the one; "Clear all" the rest. None of them activates anything.
    /// </summary>
    [UiFact]
    public void TheCrossesAndClearAllClear()
    {
        var folder = Temp();
        try
        {
            Offscreen.OnStaThread(gfx =>
            {
                var activator = new FakeActivator();
                ToastActivator.Made = activator;
                try
                {
                    var store = Store(folder, Centre);
                    using var line = Line(gfx, store);
                    var style = StyleFor(1f, LookFor(dark: true));
                    var box = Whole(line, style);
                    Assert.DoesNotContain(Pieces(line, box, style), piece => piece.Name == "heading close chat");
                    Point(line, box, style, "heading chat");
                    Assert.Equal(NotificationPartTile.Spot.GroupClose, Point(line, box, style, "heading close chat").Target.Spot);
                    Assert.Equal(default, Click(line, style, "heading close chat"));
                    Assert.DoesNotContain(store.Items, item => item.AppId == "chat");

                    box = Whole(line, style);
                    Point(line, box, style, "title 0");
                    var (_, cross) = Point(line, box, style, "close 0");
                    Assert.Equal(new NotificationPartTile.Target(NotificationPartTile.Spot.Close, "mail", 33), cross);
                    Click(line, style, "close 0");
                    Assert.DoesNotContain(store.Items, item => item.Id == 33);

                    Click(line, style, "clear all");
                    Assert.Empty(store.Items);
                    Assert.Empty(activator.Activated);
                    Assert.Equal(S.WidgetNotificationsNone, Assert.Single(Pieces(line, Whole(line, style), style), piece => piece.Name == "status").Text);
                }
                finally
                {
                    ToastActivator.Made = null;
                }
            });
        }
        finally
        {
            Clean(folder);
        }
    }

    /// <summary>
    /// A click is the thing under the pointer, not its place: a card cleared above the one the
    /// pointer is on moves it up, and the click still lands on the card the pointer was over — never
    /// on the card that moved into its place.
    /// </summary>
    [UiFact]
    public void AClickIsTheThingNotItsPlace()
    {
        var folder = Temp();
        try
        {
            Offscreen.OnStaThread(gfx =>
            {
                var activator = new FakeActivator();
                ToastActivator.Made = activator;
                try
                {
                    var store = Store(folder, Centre);
                    using var line = Line(gfx, store);
                    var style = StyleFor(1f, LookFor(dark: true));
                    Point(line, Whole(line, style), style, "title 3");
                    var (id, target) = Point(line, Whole(line, style), style, "close 3");
                    Assert.Equal(34, target.Item);
                    store.Dismiss(33);
                    _ = line.Click(id);
                    Assert.DoesNotContain(store.Items, item => item.Id == 34);
                    Assert.Contains(store.Items, item => item.Id == 35);
                    Assert.Empty(activator.Activated);
                }
                finally
                {
                    ToastActivator.Made = null;
                }
            });
        }
        finally
        {
            Clean(folder);
        }
    }

    /// <summary>
    /// Do not disturb's switch in the line's header, as the flyout's: on the accent while it is on, a
    /// click switches it (a made-up one here), and an empty list says "Do not disturb is on".
    /// </summary>
    [UiFact]
    public void TheHeadersSwitchIsDoNotDisturb()
    {
        var folder = Temp();
        try
        {
            Offscreen.OnStaThread(gfx =>
            {
                var quiet = new DoNotDisturb(new SampleDoNotDisturb(on: false));
                var store = Store(folder, Centre);
                using var line = Line(gfx, store, quiet);
                var style = StyleFor(1f, LookFor(dark: true));
                line.PaintCard(null, Whole(line, style), style, WidgetSurface.List);
                // WinUI's ToggleButton: the standard button off, the accent's on — "Clear all"'s size either way (§353).
                var buttons = LineButton.Of(gfx, style);
                var off = Pieces(line, Whole(line, style), style);
                Assert.Equal("Standard", Assert.Single(off, piece => piece.Name == "quiet plate").Text);
                foreach (var name in new[] { "quiet", "clear all" })
                {
                    Assert.Equal(buttons.Height, Assert.Single(off, piece => piece.Name == name).Box.bottom - Assert.Single(off, piece => piece.Name == name).Box.top, 0.5f);
                }
                var bell = Assert.Single(off, piece => piece.Name == "quiet").Box;
                Assert.Equal(buttons.Height, bell.right - bell.left, 0.5f);
                Click(line, style, "quiet");
                Assert.True(quiet.On);
                Assert.Equal("Accent", Assert.Single(Pieces(line, Whole(line, style), style), piece => piece.Name == "quiet plate").Text);
                store.DismissAll();
                Assert.Equal(S.DockNotificationsQuietTitle, Assert.Single(Pieces(line, Whole(line, style), style), piece => piece.Name == "status").Text);
                Click(line, style, "quiet");
                Assert.False(quiet.On);
            });
        }
        finally
        {
            Clean(folder);
        }
    }

    /// <summary>
    /// Held shorter than it is, the line shows what fits, whole, from the top, never leaves a heading
    /// alone at its foot, and scrolls to the rest under its count (ListScroll.cs) rather than saying how
    /// many more; held as tall as it asks, it shows every one.
    /// </summary>
    [UiFact]
    public void HeldShortTheLineScrollsToTheRest()
    {
        var folder = Temp();
        try
        {
            Offscreen.OnStaThread(gfx =>
            {
                var store = Store(folder, Centre);
                using var line = Line(gfx, store);
                var wrong = new List<string>();
                foreach (var text in TextScales)
                {
                    var style = StyleFor(1.5f, LookFor(dark: true, text));
                    var whole = Whole(line, style);
                    Assert.DoesNotContain(Pieces(line, whole, style), piece => piece.Name == "more");
                    for (var rows = 1; rows <= FolderOptions.MostCells; rows++)
                    {
                        var box = PopupPaint.Rect(0, 0, whole.right, MathF.Min(LineRows.Px(rows, style), whole.bottom));
                        var pieces = Pieces(line, box, style);
                        if (pieces.Any(piece => piece.Box.bottom > box.bottom + 0.5f || piece.Box.top < -0.5f))
                        {
                            wrong.Add($"{rows} rows at text {text}: a piece outside the line");
                        }
                        var last = pieces.Where(piece => piece.Name.StartsWith("heading ", StringComparison.Ordinal) || piece.Name.StartsWith("title ", StringComparison.Ordinal))
                            .OrderBy(piece => piece.Box.top).LastOrDefault();
                        if (last.Name?.StartsWith("heading ", StringComparison.Ordinal) == true)
                        {
                            wrong.Add($"{rows} rows at text {text}: a heading alone at the foot");
                        }
                        if (pieces.Any(piece => piece.Name == "more"))
                        {
                            wrong.Add($"{rows} rows at text {text}: it says how many more");
                        }
                        // Where it lists them at all and they do not fit, it scrolls to every one.
                        var laid = line.Scrolled(box, style, WidgetSurface.List);
                        var every = Pieces(line, laid.Moved, style).Count(piece => piece.Name.StartsWith("title ", StringComparison.Ordinal));
                        if (laid.Scrolls && every != Pieces(line, whole, style).Count(piece => piece.Name.StartsWith("title ", StringComparison.Ordinal)))
                        {
                            wrong.Add($"{rows} rows at text {text}: scrolled, {every} notifications");
                        }
                    }
                }
                Assert.True(wrong.Count == 0, string.Join("\n", wrong));
            });
        }
        finally
        {
            Clean(folder);
        }
    }

    /// <summary>
    /// The count's line starts at the line's top inset whatever the line's height, as every line's
    /// content does: held short with the rest left out, or with a card expanded past the most a line
    /// may be, the room left over is at the foot. It was centred, a band above the header (2026-10-11).
    /// </summary>
    [UiFact]
    public void TheHeaderStartsAtTheLinesTop()
    {
        var folder = Temp();
        try
        {
            Offscreen.OnStaThread(gfx =>
            {
                var store = Store(folder, Centre);
                using var line = Line(gfx, store);
                var wrong = new List<string>();
                foreach (var text in TextScales)
                {
                    var style = StyleFor(1.5f, LookFor(dark: true, text));
                    var whole = Whole(line, style);
                    var at = Assert.Single(Pieces(line, whole, style), piece => piece.Name == "head").Box.top;
                    void Check(string what, D2D_RECT_F box)
                    {
                        var pieces = Pieces(line, box, style);
                        if (!pieces.Any(piece => piece.Name.StartsWith("title ", StringComparison.Ordinal)))
                        {
                            return;
                        }
                        var top = Assert.Single(pieces, piece => piece.Name == "head").Box.top - box.top;
                        if (MathF.Abs(top - at) > 0.5f)
                        {
                            wrong.Add($"{what} at text {text}: the header {top} from the top, not {at}");
                        }
                    }
                    for (var rows = 1; rows <= FolderOptions.MostCells; rows++)
                    {
                        Check($"{rows} rows", PopupPaint.Rect(0, 0, whole.right, MathF.Min(LineRows.Px(rows, style), whole.bottom)));
                    }
                    Click(line, style, "chevron 0");
                    Check("a card expanded, held to the most", PopupPaint.Rect(0, 0, whole.right, LineRows.Px(FolderOptions.MostCells, style)));
                    Click(line, style, "chevron 0");
                }
                Assert.True(wrong.Count == 0, string.Join("\n", wrong));
            });
        }
        finally
        {
            Clean(folder);
        }
    }

    /// <summary>What is expanded and typed is the showing's: gone when the folder closes, as Windows' own forgets it.</summary>
    [UiFact]
    public void TheShowingIsForgottenWhenTheFolderCloses()
    {
        var folder = Temp();
        try
        {
            Offscreen.OnStaThread(gfx =>
            {
                var store = Store(folder, Centre);
                using var line = Line(gfx, store);
                var style = StyleFor(1f, LookFor(dark: true));
                Click(line, style, "chevron 0");
                Click(line, style, "part 0 0");
                Assert.True(line.CardTyping);
                line.Visible(false);
                Assert.False(line.CardTyping);
                Assert.Empty(line.View.Expanded);
                Assert.Empty(line.View.Boxes);
            });
        }
        finally
        {
            Clean(folder);
        }
    }

    // --- The folder and the face -----------------------------------------------------------------------

    /// <summary>
    /// Opening the folder to look at it counts everything in it as seen, as opening the bell's list did:
    /// the bell's count goes. The face reads the same store as the line.
    /// </summary>
    [UiFact]
    public void OpeningTheFolderMarksTheNotificationsSeen()
    {
        var folder = Temp();
        try
        {
            Offscreen.OnStaThread(gfx =>
            {
                var store = Store(folder, Centre);
                using var island = Made(gfx, store);
                island.Opened(gfx);
                var line = Assert.IsType<NotificationPartTile>(island.Tiles[0].Island);
                Assert.Same(store, line.Store);
                Assert.Same(store, island.Notifications);
                Assert.Equal(7, store.Unseen);
                Assert.Equal((false, "7"), NotificationsFaceDrawing.Look(store, count: true));
                island.MarkSeen();
                Assert.Equal(0, store.Unseen);
                Assert.Equal((false, null), NotificationsFaceDrawing.Look(store, count: true));
                island.Closed();
            });
        }
        finally
        {
            Clean(folder);
        }
    }

    /// <summary>
    /// What the face draws, as the bell drew it: how many are new ("99+" past 99), none where none
    /// are or the face is set not to say; dimmed with no count where they cannot be read; the plain
    /// bell with no store (Settings' picture). Its words to a screen reader are the bell's.
    /// </summary>
    [UiFact]
    public void TheFaceDrawsTheCountAndTheDim()
    {
        var folder = Temp();
        try
        {
            Offscreen.OnStaThread(gfx =>
            {
                Assert.Equal((false, null), NotificationsFaceDrawing.Look(null, count: true));
                var store = Store(folder, Centre);
                Assert.Equal((false, "7"), NotificationsFaceDrawing.Look(store, count: true));
                Assert.Equal((false, null), NotificationsFaceDrawing.Look(store, count: false));
                Assert.Equal(S.WidgetNotificationsNew(7), NotificationsFaceDrawing.Tip(store));
                var many = Store(folder, Enumerable.Range(1, 120).Select(index => ((long)index, 2, index, Toast($"Message {index}", "Body."))), "many");
                Assert.Equal((false, "99+"), NotificationsFaceDrawing.Look(many, count: true));
                var unreadable = new NotificationStore(Path.Combine(folder, "none", "wpndatabase.db"), Path.Combine(folder, "none-copy"), Path.Combine(folder, "none-state"), app => (app, null));
                unreadable.Prepare();
                Assert.Equal((true, null), NotificationsFaceDrawing.Look(unreadable, count: true));
                Assert.Equal(S.WidgetNotificationsUnavailable, NotificationsFaceDrawing.Tip(unreadable));
                store.MarkSeen();
                Assert.Equal(S.WidgetNotificationsName, NotificationsFaceDrawing.Tip(store));
            });
        }
        finally
        {
            Clean(folder);
        }
    }

    /// <summary>
    /// The face is its glyph, its widest count's room across a bar and the button's ends whatever it
    /// shows (§276) — nothing new, a few, do not disturb on — and is painted again in place as
    /// notifications are seen or do not disturb is switched, never rebuilt.
    /// </summary>
    [UiFact]
    public void TheFaceIsOneSizeAndPaintsAgainInPlace()
    {
        var folder = Temp();
        try
        {
            Offscreen.OnStaThread(gfx =>
            {
                var made = new SampleDoNotDisturb(on: false);
                DoNotDisturb.Made = () => made;
                try
                {
                    var store = Store(folder, Centre);
                    using var island = Made(gfx, store);
                    Assert.True(island.ShowsNotifications);
                    Assert.Equal(0, island.IntervalMs);
                    var styles = new List<IslandStyle>();
                    foreach (var height in new[] { 28, 48, 72 })
                    {
                        var config = new Config { Height = height };
                        foreach (var vertical in new[] { false, true })
                        {
                            styles.Add(new IslandStyle(1.5f, vertical, Layout.CellPx(144, config), Layout.IconPx(144, config), LookFor(dark: true)));
                        }
                    }
                    var sizes = styles.Select(style => island.CellSize(0, style)).ToList();
                    foreach (var (style, size) in styles.Zip(sizes))
                    {
                        var trail = style.Vertical ? 0 : GlyphBadge.Trail(gfx, style);
                        Assert.Equal(FaceWidthTests.GlyphButton(style, trail), style.Vertical ? size.Height : size.Width, 0.5f);
                    }
                    _ = island.Surface(0, styles[2]);
                    Assert.Equal("False|7|False", island.FaceShown);

                    store.MarkSeen();
                    Assert.False(((IIsland)island).Refresh(gfx));
                    Assert.Equal("False||False", island.FaceShown);
                    DoNotDisturb.Shared.Set(true);
                    Assert.False(((IIsland)island).Refresh(gfx));
                    Assert.Equal("False||True", island.FaceShown);
                    Assert.Equal(sizes, styles.Select(style => island.CellSize(0, style)).ToList());
                }
                finally
                {
                    DoNotDisturb.Made = null;
                }
            });
        }
        finally
        {
            Clean(folder);
        }
    }

    /// <summary>
    /// The face's right click has the list's Clear all, Do not disturb and Notification settings, worked
    /// on the face's own store, with or without the line open: the bell's header and footer had them.
    /// </summary>
    [UiFact]
    public void TheFacesRightClickClearsAndSwitches()
    {
        var folder = Temp();
        try
        {
            Offscreen.OnStaThread(gfx =>
            {
                var made = new SampleDoNotDisturb(on: false);
                DoNotDisturb.Made = () => made;
                try
                {
                    var holder = Holder();
                    Assert.Equal([WidgetAct.ClearAll, WidgetAct.DoNotDisturb, WidgetAct.NotificationSettings], TheDock.ActsFor(holder.Folder!.Face.Info));
                    var store = Store(folder, Centre);
                    using var island = Made(gfx, store);
                    var acts = Assert.IsAssignableFrom<INotificationActs>(island.FaceWidget);
                    var (items, rows) = TheDock.ActRows(TheDock.ActsFor(holder.Folder!.Face.Info), island.FaceWidget);
                    Assert.True(items[0].Enabled, string.Join(", ", items.Select(item => $"{item.Text}:{item.Enabled}")));
                    // Do not disturb's rows work the face's service, the made-up one a test run has: on
                    // for an hour, and nothing of this PC's is switched.
                    Assert.Same(DoNotDisturb.Shared, acts.Dnd);
                    var hour = items.Single(item => item.Text == S.DockDndForAnHour);
                    TheDock.RunAct(TheDock.ActRowOf(rows, hour.Id)!, island.FaceWidget, (_, _) => { });
                    Assert.True(DoNotDisturb.Shared.On);
                    Assert.NotNull(DoNotDisturb.Shared.Until);
                    acts.ClearAll();
                    Assert.Empty(store.Items);
                    Assert.False(acts.HasAny);
                }
                finally
                {
                    DoNotDisturb.Made = null;
                }
            });
        }
        finally
        {
            Clean(folder);
        }
    }

    // --- Pictures --------------------------------------------------------------------------------------

    /// <summary>
    /// The preset as it opens, at 150 %, to stand beside the flyout's notifications-grouped-*.png and
    /// notifications-expanded-*.png: grouped; a card under the pointer (its ×); the chat expanded with a
    /// reply typed, the long message expanded and the mail opened whole; do not disturb on; empty; and
    /// at 225 % text.
    /// </summary>
    [UiFact]
    public void NotificationsPresetPictures()
    {
        foreach (var (dark, scale) in new[] { (true, 1f), (true, 1.5f), (false, 1f), (false, 1.5f) })
        {
            var folder = Temp();
            try
            {
                Offscreen.OnStaThread(gfx =>
                {
                    var folders = new List<FolderIsland>();
                    try
                    {
                        var panels = new List<(string, Look, FolderIsland, FolderPlan)>();
                        void Add(string caption, Action<NotificationPartTile, TileStyle> set, float text = 1f, IEnumerable<(long, int, int, string)>? rows = null, bool quiet = false,
                            Func<string, (string Name, Pixels? Icon)>? resolve = null)
                        {
                            // Each panel its own do not disturb: the sheet is drawn once they are all made.
                            var made = new SampleDoNotDisturb(on: quiet);
                            DoNotDisturb.Made = () => made;
                            var look = LookFor(dark, text);
                            var store = Store(folder, rows ?? Centre, $"db{panels.Count}", resolve);
                            var island = Made(gfx, store, $"{Owner}/{Holder().Id}-{panels.Count}");
                            folders.Add(island);
                            island.Opened(gfx);
                            var line = (NotificationPartTile)island.Tiles[0].Island;
                            var style = StyleFor(scale, look);
                            line.PaintCard(null, Whole(line, style), style, WidgetSurface.List);
                            set(line, style);
                            panels.Add((caption, look, island, Plan(island, look, scale).Plan));
                        }

                        // Pictures of the toasts' own: a person's logo, a hero, an inline image, a hero with buttons.
                        var images = Path.Combine(folder, "images");
                        var face = Picture(images, "face", 96, 96, 0xFFE8A87C, 0xFF85586F);
                        var wide = Picture(images, "hero", 728, 360, 0xFF2E86AB, 0xFFF6C85F);
                        var photo = Picture(images, "photo", 480, 320, 0xFF6A994E, 0xFFBC4749);
                        (long, int, int, string)[] pictured =
                        [
                            (61, 1, 5, Pictured("Ada Lovelace", "Here is the diagram of the engine I promised you.", face, null, photo)),
                            (62, 3, 1, Pictured("Your trip to Lisbon", "Check in is open. Choose your seat now.", null, wide, null,
                                "<action content=\"Check in\" arguments=\"checkin\"/><action content=\"Later\" arguments=\"later\"/>")),
                            (63, 2, 40, Pictured("Grace Hopper", "Found the bug.", face, null, null)),
                            (64, 4, 90, Toast("Updates installed", "Three apps were updated.")),
                        ];

                        Add("Grouped by app", (_, _) => { });
                        Add("A card under the pointer", (line, style) => Point(line, Whole(line, style), style, "title 2"));
                        Add("Expanded, a reply typed", (line, style) =>
                        {
                            Click(line, style, "chevron 1");
                            Click(line, style, "chevron 0");
                            Click(line, style, "part 0 0");
                            foreach (var c in "On my way")
                            {
                                line.CardChar(c, WidgetSurface.List);
                            }
                        });
                        Add("Pictures: a logo, an inline image, a hero with buttons", (line, style) => Click(line, style, "chevron 0"), rows: pictured);
                        Add("Four apps", (_, _) => { }, rows: Twelve);
                        Add("Do not disturb on", (_, _) => { }, quiet: true);
                        Add("Nothing new", (_, _) => { }, rows: []);
                        Add("Do not disturb on, nothing new", (_, _) => { }, rows: [], quiet: true);
                        Add("225 % text", (_, _) => { }, text: 2.25f);
                        // Windows' own senders as this PC registers them: Windows Security's shield, and
                        // Bluetooth's white glyph in the text colour (NotificationApps).
                        Add("Windows Security and Bluetooth, this PC's", (_, _) => { }, resolve: app => NotificationApps.Resolve(
                            app == "chat" ? "Windows.Defender.SecurityCenter" : "Windows.SystemToast.BthQuickPair", NotificationApps.Windows));
                        Sheet(gfx, $"notifications-preset-{(dark ? "dark" : "light")}-{scale * 100:0}.png", scale, panels);
                    }
                    finally
                    {
                        foreach (var island in folders)
                        {
                            island.Closed();
                            island.Dispose();
                        }
                        DoNotDisturb.Made = null;
                    }
                });
            }
            finally
            {
                Clean(folder);
            }
        }
    }

    /// <summary>
    /// The face on bars of 40 and 48 DIP and at 150 %: five new, none, 120, do not disturb on with
    /// three and with none, and unreadable — notifications-face-&lt;dark|light&gt;.png, beside the
    /// bell's notification-badge-*.png and bell-quiet-*.png before it went.
    /// </summary>
    [UiFact]
    public void NotificationsFacePictures()
    {
        foreach (var dark in new[] { true, false })
        {
            var folder = Temp();
            try
            {
                Offscreen.OnStaThread(gfx =>
                {
                    var look = LookFor(dark);
                    var theme = FlyoutTheme.For(look);
                    var states = new List<(NotificationStore Store, bool Quiet)>
                    {
                        (Store(folder, Centre.Take(5), "five"), false),
                        (Store(folder, [], "none"), false),
                        (Store(folder, Enumerable.Range(1, 120).Select(index => ((long)index, 2, index, Toast($"Message {index}", "Body."))), "many"), false),
                        (Store(folder, Centre.Take(3), "quiet"), true),
                        (Store(folder, [], "quiet-none"), true),
                    };
                    var unreadable = new NotificationStore(Path.Combine(folder, "absent", "wpndatabase.db"), Path.Combine(folder, "absent-copy"), Path.Combine(folder, "absent-state"), app => (app, null));
                    unreadable.Prepare();
                    states.Add((unreadable, false));
                    var styles = new[] { (40, 96u), (48, 96u), (48, 144u) }
                        .Select(pair => new IslandStyle(pair.Item2 / 96f, false, Layout.CellPx(pair.Item2, new Config { Height = pair.Item1 }), Layout.IconPx(pair.Item2, new Config { Height = pair.Item1 }), look))
                        .ToList();
                    var islands = new List<(FolderIsland Island, bool Quiet)>();
                    foreach (var (store, quiet) in states)
                    {
                        var made = new SampleDoNotDisturb(on: quiet);
                        DoNotDisturb.Made = () => made;
                        islands.Add((Made(gfx, store, $"{Owner}/bell-{islands.Count}"), quiet));
                    }
                    try
                    {
                        const float gap = 12;
                        var width = gap + styles.Sum(style => (islands.Count * (islands[0].Island.CellSize(0, style).Width + gap)) + gap);
                        var height = gap + styles.Max(style => islands[0].Island.CellSize(0, style).Height) + gap;
                        Offscreen.Bitmap(width, height, dc =>
                        {
                            PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, width, height), 0, PopupPaint.Ink(theme.Base));
                            var x = gap;
                            foreach (var style in styles)
                            {
                                foreach (var (island, quiet) in islands)
                                {
                                    var made = new SampleDoNotDisturb(on: quiet);
                                    DoNotDisturb.Made = () => made;
                                    var (w, h) = island.CellSize(0, style);
                                    PopupPaint.Fill(dc, PopupPaint.Rect(x, gap, w, h), 4 * style.Scale, PopupPaint.Ink(theme.CardBackground));
                                    Offscreen.Shifted(dc, x, gap, () => island.PaintCell(dc, 0, style, w, h));
                                    x += w + gap;
                                }
                                x += gap;
                            }
                        }, bitmap => Offscreen.Save(bitmap, Path.Combine(Offscreen.Pictures, $"notifications-face-{(dark ? "dark" : "light")}.png")));
                    }
                    finally
                    {
                        foreach (var (island, _) in islands)
                        {
                            island.Dispose();
                        }
                        DoNotDisturb.Made = null;
                    }
                });
            }
            finally
            {
                Clean(folder);
            }
        }
    }
}
