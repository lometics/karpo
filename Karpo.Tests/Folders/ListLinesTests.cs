// The list lines of the clock, Security, the calendar, mail and the notification list, at every
// height from one row to their most (the person, 2026-09-27: "the grid ones have been optimized very
// well for all the different sizes. for the list view widgets they have not been optimized ad all.
// they should also be optimized to look good from 1 to 6 cell heights").
//
// Each line lays itself out for the height it is given and says what it laid out (LineSketch,
// ILinePieces), so the rules are held on the pieces as well as on the ink: at 100, 150 and 200 %, at
// ordinary and the largest text, at every height from one row to the kind's most and half-way
// between each two, every line
// - draws something, and nothing outside its line;
// - keeps every piece inside the line and between the list's gutters (the one gutter every line
//   stands on, ListForm.Gutter);
// - stands no piece on another — a plate behind a glyph or a button is behind it, not on it;
// - on one row shows the widget's main reading: the time, where the PC stands, the next event,
//   how much mail is unread, how many notifications wait;
// - at its own height — as tall as its content, where a new line starts and where an Automatic one
//   stays, which is rarely a whole number of rows — draws the form it was measured for;
// - made taller never shows fewer events, messages or notifications;
// - and where the notification list is its own compact list, its × and "Clear all" hide as the
//   panel's do, in a store of the test's own.
//
// What they show is held still: a frozen clock, a sample Security, a made-up busy week, an inbox of
// made-up mail in a store of its own, and made-up notifications — nothing is read from this PC. The
// sheets, one per kind at 150 %, every height side by side, go where the readings' lines' do:
// %TEMP%\list-<kind>.png (KARPO_LISTS_DIR where it is set), dark, and list-<kind>-light.png.
// Needs Direct2D: KARPO_UI_TESTS=1. Nothing is shown on the person's desktop.

using Karpo.Accounts;
using Karpo.Core;
using Karpo.Dock;
using Karpo.Dock.Pim;
using Karpo.Dock.QuickSettings.Services;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using Xunit.Abstractions;
using static Karpo.Tests.Islands.Offscreen;

namespace Karpo.Tests.Folders;

public sealed unsafe class ListLinesTests(ITestOutputHelper output)
{
    /// <summary>The kinds of this half of the list lines.</summary>
    internal static readonly ListKind[] Kinds = [ListKind.Clock, ListKind.Security, ListKind.Calendar, ListKind.Mail, ListKind.NotificationsList];

    /// <summary>The display scales every height is held at.</summary>
    private static readonly float[] Scales = [1f, 1.5f, 2f];

    /// <summary>Ordinary text, and the largest Windows offers.</summary>
    private static readonly float[] TextScales = [1f, 2.25f];

    /// <summary>The moment everything shows: a Tuesday afternoon, the day the sample week starts.</summary>
    private static DateTime Frozen => ListFormCases.Frozen;

    private static Look LookFor(bool dark, float textScale = 1f) => ListFormPictures.LookFor(dark, textScale);

    /// <summary>The style a list draws a line in, at the folder's scale.</summary>
    private static TileStyle StyleFor(float scale, Look look) => FolderFlyout.StyleFor(scale, look, FlyoutTheme.For(look));

    /// <summary>A line's width: the list's own, at its default.</summary>
    private static float Width(float scale) => FolderFlyout.ListWidth(scale);

    /// <summary>The most rows a new line of <paramref name="kind"/> may be given, and so the tallest it is held at.</summary>
    private static int Most(ListKind kind) => ListWidgets.Of(kind).Range.Most;

    // --- The rules -----------------------------------------------------------------------------

    /// <summary>
    /// Every kind at every height from one row to its most, at every scale and both text sizes: it
    /// draws, inside its line; its pieces stand inside the line and between its gutters; and no piece
    /// stands on another. Everything is tried before anything is asserted, and all that is wrong is
    /// said at once.
    /// </summary>
    [UiFact]
    public void EveryHeightIsDrawnInsideItsLineWithNothingOnAnything()
    {
        OnStaThread(gfx =>
        {
            using var stage = new Stage(gfx);
            var wrong = new List<string>();
            foreach (var scale in Scales)
            {
                foreach (var textScale in TextScales)
                {
                    var look = LookFor(dark: true, textScale);
                    var style = StyleFor(scale, look);
                    var width = Width(scale);
                    foreach (var kind in Kinds)
                    {
                        foreach (var (height, said) in Heights(kind, style))
                        {
                            foreach (var fault in Faults(gfx, stage[kind], width, height, style))
                            {
                                wrong.Add($"{kind} at {said}, {scale * 100:0} %, text {textScale * 100:0} %: {fault}");
                            }
                        }
                    }
                }
            }
            foreach (var line in wrong.Take(60))
            {
                output.WriteLine(line);
            }
            Assert.True(wrong.Count == 0, $"{wrong.Count} lines are wrong:\n{string.Join("\n", wrong.Take(60))}");
        });
    }

    /// <summary>
    /// One row is one line, and what it shows first is the widget's main reading — the time, where the
    /// PC stands, the next event, how much mail is unread, how many notifications wait — at every
    /// scale and both text sizes.
    /// </summary>
    [UiFact]
    public void OneRowShowsTheMainReading()
    {
        OnStaThread(gfx =>
        {
            using var stage = new Stage(gfx);
            var wrong = new List<string>();
            foreach (var scale in Scales)
            {
                foreach (var textScale in TextScales)
                {
                    var style = StyleFor(scale, LookFor(dark: true, textScale));
                    var box = PopupPaint.Rect(0, 0, Width(scale), LineRows.Px(1, style));
                    foreach (var kind in Kinds)
                    {
                        var line = (ILinePieces)stage[kind];
                        var main = line.LinePieces(box, style).Where(piece => piece.Name == line.MainPiece).ToList();
                        if (main.Count == 0 || main.All(piece => piece.Text.Length == 0))
                        {
                            wrong.Add($"{kind} at {scale * 100:0} %, text {textScale * 100:0} %: no {line.MainPiece} on one row");
                        }
                    }
                }
            }
            Assert.True(wrong.Count == 0, string.Join("\n", wrong));
        });
    }

    /// <summary>
    /// A line as tall as its own content (<see cref="IListContent.ListHeight"/>, held to its range) —
    /// where a new line starts, and where it stays while its height is Automatic — draws the form it
    /// was measured for, though that is rarely a whole number of rows: with a few things to show, at
    /// every scale and both text sizes, each kind leaves nothing out and says it in its fuller form —
    /// the date under the time, who is in charge under the standing, each event, message and
    /// notification in two lines.
    /// </summary>
    [UiFact]
    public void AtItsOwnHeightALineDrawsTheFormItWasMeasuredFor()
    {
        OnStaThread(gfx =>
        {
            using var stage = new Stage(gfx, few: true);
            var wrong = new List<string>();
            foreach (var scale in Scales)
            {
                foreach (var textScale in TextScales)
                {
                    var style = StyleFor(scale, LookFor(dark: true, textScale));
                    var width = Width(scale);
                    foreach (var kind in Kinds)
                    {
                        var island = stage[kind];
                        var range = ListWidgets.Of(kind).Range;
                        var natural = ((IListContent)island).ListHeight(width, style);
                        var height = Math.Clamp(natural, LineRows.Px(range.Least, style), LineRows.Px(range.Most, style));
                        var pieces = ((ILinePieces)island).LinePieces(PopupPaint.Rect(0, 0, width, height), style);
                        var at = $"{kind} at its own {height:0} px, {scale * 100:0} %, text {textScale * 100:0} %";
                        // Held to its most, as a week at the largest text is, a line says what it left out.
                        if (natural <= height + 0.5f && pieces.Any(piece => piece.Name == "more"))
                        {
                            wrong.Add($"{at}: says some are left out");
                        }
                        var (upper, lower) = kind switch
                        {
                            ListKind.Clock => ("time", "date"),
                            ListKind.Security => ("standing", "guard"),
                            // The header, the calendar's name (§419), over the first event.
                            ListKind.Calendar => ("head", "event 0"),
                            ListKind.Mail => ("sender 0", "subject 0"),
                            _ => ("title 0", "body 0"),
                        };
                        var over = pieces.FirstOrDefault(piece => piece.Name == upper);
                        var under = pieces.FirstOrDefault(piece => piece.Name == lower);
                        if (over.Name is null || under.Name is null || under.Box.top < over.Box.bottom - 0.5f)
                        {
                            wrong.Add($"{at}: its {lower} is not under its {upper}: {string.Join(", ", pieces.Select(piece => $"{piece.Name} {Say(piece.Box)}"))}");
                        }
                    }
                }
            }
            Assert.True(wrong.Count == 0, string.Join("\n", wrong));
        });
    }

    /// <summary>
    /// Made taller, a line never shows fewer events, messages or notifications: at every height from
    /// one row to its most and half-way between each two, at every scale and both text sizes, each
    /// shows at least as many as the height under it.
    /// </summary>
    [UiFact]
    public void ATallerLineNeverShowsFewer()
    {
        OnStaThread(gfx =>
        {
            using var stage = new Stage(gfx);
            var wrong = new List<string>();
            foreach (var scale in Scales)
            {
                foreach (var textScale in TextScales)
                {
                    var style = StyleFor(scale, LookFor(dark: true, textScale));
                    var width = Width(scale);
                    foreach (var kind in (ListKind[])[ListKind.Calendar, ListKind.Mail, ListKind.NotificationsList])
                    {
                        var (shown, said) = (0, "");
                        foreach (var (height, at) in Heights(kind, style))
                        {
                            var now = Items(kind, ((ILinePieces)stage[kind]).LinePieces(PopupPaint.Rect(0, 0, width, height), style));
                            if (now < shown)
                            {
                                wrong.Add($"{kind} at {scale * 100:0} %, text {textScale * 100:0} %: {now} at {at}, {shown} at {said}");
                            }
                            (shown, said) = (now, at);
                        }
                    }
                }
            }
            Assert.True(wrong.Count == 0, string.Join("\n", wrong));
        });
    }

    /// <summary>
    /// The notification list's line is a list of its own at every height, and its controls work there
    /// as on the panel's cards: on two rows the newest notification's × — at the trailing gutter,
    /// where its time was, under the pointer — hides it, the pointer on its title finds it (and is not
    /// clicked: a notification's body really opens its app), and on one row "Clear all" hides the
    /// rest. Every target is asked for before it is clicked. In a store of the test's own, over a
    /// database the test builds in a folder of its own.
    /// </summary>
    [UiFact]
    public void ACompactNotificationLinesCrossAndClearAllHide()
    {
        OnStaThread(gfx =>
        {
            var folder = Path.Combine(Path.GetTempPath(), "karpo-tests", "notification-lines-" + Guid.NewGuid().ToString("N"));
            try
            {
                var database = Path.Combine(folder, "lines", "wpndatabase.db");
                FlyoutOffscreenTests.BuildDatabase(database, 3);
                var store = new NotificationStore(database, Path.Combine(folder, "lines-copy"), Path.Combine(folder, "lines-state"), app => (app, null));
                store.Prepare();
                Assert.Equal(3, store.Items.Count);
                var cell = ListKind.NotificationsList.New();
                cell.Id = "notifications-" + Guid.NewGuid().ToString("N")[..6];
                using var list = new NotificationPartTile(Config.IslandKey("layout:default", cell.Id), cell, gfx, new Config(), store);
                list.StandIn(ListWidgets.Of(ListKind.NotificationsList));
                var style = StyleFor(1f, LookFor(dark: true));
                var width = Width(1f);
                var form = ListForm.Of(gfx, style);
                var newest = NotificationText.Newest(store.Items)[0];

                // Three rows: the count over the newest app's heading and its notification, and the
                // rest scrolling under them (ListScroll.cs) — the line laid out as tall as all of it.
                var three = PopupPaint.Rect(0, 0, width, LineRows.Px(3, style));
                var laid = list.Scrolled(three, style, WidgetSurface.List);
                Assert.True(laid.Scrolls);
                var two = laid.Moved;
                var title = Assert.Single(((ILinePieces)list).LinePieces(two, style), piece => piece.Name == "title 0");
                Assert.True(title.Box.bottom <= three.bottom, "the newest notification's title shows at three rows");
                Assert.Equal(newest.Title, title.Text);
                var onTitle = new System.Numerics.Vector2((title.Box.left + title.Box.right) / 2, (title.Box.top + title.Box.bottom) / 2);
                list.CardHover(onTitle, two, style, WidgetSurface.List);
                var found = list.CardTarget(onTitle, two, style, WidgetSurface.List);
                Assert.True(found != 0 && !Hides(list, found), $"the newest notification's title is on target {found}");
                var close = Assert.Single(((ILinePieces)list).LinePieces(two, style), piece => piece.Name == "close 0").Box;
                Press(list, new System.Numerics.Vector2((close.left + close.right) / 2, (close.top + close.bottom) / 2), two, style);
                Assert.DoesNotContain(store.Items, item => item.Id == newest.Id);
                Assert.Equal(2, store.Items.Count);

                var one = PopupPaint.Rect(0, 0, width, LineRows.Px(1, style));
                var clear = Assert.Single(((ILinePieces)list).LinePieces(one, style), piece => piece.Name == "clear all").Box;
                Press(list, new System.Numerics.Vector2((clear.left + clear.right) / 2, (clear.top + clear.bottom) / 2), one, style);
                Assert.Empty(store.Items);
            }
            finally
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
        });
    }

    /// <summary>Whether a click on <paramref name="target"/> hides — a ×, or "Clear all" — rather than opening an app.</summary>
    private static bool Hides(NotificationPartTile list, int target) =>
        list.TargetOf(target).Spot is NotificationPartTile.Spot.Clear or NotificationPartTile.Spot.Close or NotificationPartTile.Spot.GroupClose;

    /// <summary>Hovers <paramref name="point"/> on a line and clicks what is there, after making sure the click can only hide.</summary>
    private static void Press(NotificationPartTile list, System.Numerics.Vector2 point, D2D_RECT_F box, in TileStyle style)
    {
        list.CardHover(point, box, style, WidgetSurface.List);
        var target = list.CardTarget(point, box, style, WidgetSurface.List);
        Assert.True(Hides(list, target), $"the point {point} is on target {target}, which does not hide: not clicked");
        _ = list.Click(target);
    }

    /// <summary>
    /// The notification list's line is its own list at every height, with two notifications or with
    /// eight, its look set by its height and never by how many there are (2026-09-27): on one row its
    /// Header alone — the bell in the icon column, "Notifications" and "Clear all", never how many there are (§454);
    /// taller that Header over each app's heading and each notification it shows, its title over its body, no card.
    /// </summary>
    [UiFact]
    public void TheNotificationLineIsItsOwnListAtEveryHeight()
    {
        OnStaThread(gfx =>
        {
            var wrong = new List<string>();
            foreach (var few in new[] { true, false })
            {
                using var stage = new Stage(gfx, few: few);
                foreach (var scale in Scales)
                {
                    var style = StyleFor(scale, LookFor(dark: true));
                    foreach (var (height, said) in Heights(ListKind.NotificationsList, style))
                    {
                        var pieces = ((ILinePieces)stage[ListKind.NotificationsList]).LinePieces(PopupPaint.Rect(0, 0, Width(scale), height), style);
                        var one = !pieces.Any(piece => piece.Name.StartsWith("heading ", StringComparison.Ordinal));
                        foreach (var name in new[] { "glyph", "head", "clear all" })
                        {
                            if (!pieces.Any(piece => piece.Name == name))
                            {
                                wrong.Add($"{stage.Notifications} notifications at {said}, {scale * 100:0} %: no {name}");
                            }
                        }
                        var titles = pieces.Count(piece => piece.Name.StartsWith("title ", StringComparison.Ordinal));
                        var bodies = pieces.Count(piece => piece.Name.StartsWith("body ", StringComparison.Ordinal));
                        if (one ? titles + bodies > 0 : titles != bodies || pieces.Any(piece => piece.Name.StartsWith("card ", StringComparison.Ordinal)))
                        {
                            wrong.Add($"{stage.Notifications} notifications at {said}, {scale * 100:0} %: {titles} titles, {bodies} bodies");
                        }
                    }
                }
            }
            Assert.True(wrong.Count == 0, string.Join("\n", wrong));
        });
    }

    /// <summary>
    /// The heights a kind is held at: every whole number of rows from one to its most, and half-way
    /// between each two, where a line as tall as its content stands.
    /// </summary>
    private static IEnumerable<(float Height, string Said)> Heights(ListKind kind, TileStyle style)
    {
        for (var rows = 1; rows <= Most(kind); rows++)
        {
            var height = LineRows.Px(rows, style);
            yield return (height, $"{rows} row{(rows == 1 ? "" : "s")}");
            if (rows < Most(kind))
            {
                var between = PopupPaint.Round((height + LineRows.Px(rows + 1, style)) / 2);
                if (between > height)
                {
                    yield return (between, $"{rows}½ rows ({between:0} px)");
                }
            }
        }
    }

    /// <summary>How many events, messages or notifications a line's pieces show.</summary>
    private static int Items(ListKind kind, IReadOnlyList<LinePiece> pieces)
    {
        string[] first = kind switch
        {
            ListKind.Calendar => ["event "],
            ListKind.Mail => ["sender "],
            _ => ["title "],
        };
        return pieces.Count(piece => first.Any(name => piece.Name.StartsWith(name, StringComparison.Ordinal)));
    }

    /// <summary>What is wrong with <paramref name="island"/>'s line <paramref name="height"/> px tall; nothing for nothing.</summary>
    private static List<string> Faults(Gfx gfx, CellIsland island, float width, float height, TileStyle style)
    {
        var wrong = new List<string>();
        var box = PopupPaint.Rect(0, 0, width, height);
        var pieces = ((ILinePieces)island).LinePieces(box, style);
        var gutter = ListForm.Gutter(style);
        if (pieces.Count == 0)
        {
            wrong.Add("nothing laid out");
        }
        foreach (var piece in pieces)
        {
            var at = piece.Box;
            if (at.top < -0.5f || at.bottom > height + 0.5f)
            {
                wrong.Add($"its {piece.Name} ({Say(at)}) runs out of its line, {height:0} px tall");
            }
            if (at.left < gutter - 0.5f || at.right > width - gutter + 0.5f)
            {
                wrong.Add($"its {piece.Name} ({Say(at)}) runs past the list's gutters at {gutter:0} and {width - gutter:0}");
            }
        }
        var standing = pieces.Where(piece => !piece.Behind && piece.Box.right > piece.Box.left && piece.Box.bottom > piece.Box.top).ToList();
        for (var one = 0; one < standing.Count; one++)
        {
            for (var other = one + 1; other < standing.Count; other++)
            {
                if (Overlap(standing[one].Box, standing[other].Box))
                {
                    wrong.Add($"its {standing[one].Name} ({Say(standing[one].Box)}) and its {standing[other].Name} ({Say(standing[other].Box)}) overlap");
                }
            }
        }
        if (InkFault(island, width, height, style) is { } ink)
        {
            wrong.Add(ink);
        }
        return wrong;
    }

    /// <summary>Where the line inks, unclipped, with room around it: what the folder's clip would hide shows here. Null for inside.</summary>
    private static string? InkFault(CellIsland island, float width, float height, TileStyle style)
    {
        var box = PopupPaint.Rect(0, 0, width, height);
        var margin = PopupPaint.Round(12 * style.Scale);
        (float Left, float Top, float Right, float Bottom) ink = default;
        Bitmap(width + (2 * margin), height + (2 * margin),
            dc => Shifted(dc, margin, margin, () => island.PaintOn(dc, box, style, WidgetSurface.List)),
            bitmap => ink = InkOf(bitmap, (uint)MathF.Ceiling(width + (2 * margin)), (uint)MathF.Ceiling(height + (2 * margin))));
        if (ink.Right <= ink.Left)
        {
            return "nothing drawn";
        }
        return ink.Left < margin - 1 || ink.Top < margin - 1 || ink.Right > margin + width + 1 || ink.Bottom > margin + height + 1
            ? $"drawn past its line: ink {ink.Left - margin:0},{ink.Top - margin:0} to {ink.Right - margin:0},{ink.Bottom - margin:0} in a line {width:0} by {height:0}"
            : null;
    }

    private static bool Overlap(D2D_RECT_F a, D2D_RECT_F b) =>
        a.left < b.right - 0.5f && b.left < a.right - 0.5f && a.top < b.bottom - 0.5f && b.top < a.bottom - 0.5f;

    private static string Say(D2D_RECT_F rect) => $"{rect.left:0},{rect.top:0} to {rect.right:0},{rect.bottom:0}";

    // --- The pictures --------------------------------------------------------------------------

    /// <summary>Where the sheets go, as the readings' lines' do: KARPO_LISTS_DIR, else %TEMP%.</summary>
    private static string Sheets
    {
        get
        {
            var folder = Environment.GetEnvironmentVariable("KARPO_LISTS_DIR") is { Length: > 0 } named ? named : Path.GetTempPath();
            Directory.CreateDirectory(folder);
            return folder;
        }
    }

    /// <summary>
    /// Each kind at 150 %, every height from one row to its most side by side, six to a row, each on
    /// the panel's own ground under its height — the sheet the readings' lines have
    /// (ListLineReadingsTests): list-&lt;kind&gt;.png (dark) and list-&lt;kind&gt;-light.png.
    /// </summary>
    [UiFact]
    public void EveryKindIsPicturedAtEveryHeight()
    {
        OnStaThread(gfx =>
        {
            using var stage = new Stage(gfx);
            foreach (var kind in Kinds)
            {
                foreach (var dark in new[] { true, false })
                {
                    Sheet(gfx, stage[kind], kind, 1.5f, LookFor(dark), Path.Combine(Sheets, $"list-{kind.Id()}{(dark ? "" : "-light")}.png"));
                }
            }
        });
    }

    /// <summary>One kind's sheet: its line at every height, six to a row, each under its height's name.</summary>
    internal static unsafe void Sheet(Gfx gfx, CellIsland island, ListKind kind, float scale, Look look, string path)
    {
        var theme = FlyoutTheme.For(look);
        var style = StyleFor(scale, look);
        var width = Width(scale);
        var most = Most(kind);
        var across = Math.Min(6, most);
        var pad = PopupPaint.Round(16 * scale);
        var label = PopupPaint.Round(11 * scale);
        var labelLine = TilePaint.LineHeight(gfx, label);
        var bands = Enumerable.Range(0, (most + across - 1) / across)
            .Select(band => LineRows.Px(Math.Min(most, (band + 1) * across), style))
            .ToList();
        var sheetWidth = pad + (across * (width + pad));
        var sheetHeight = pad + bands.Sum(band => labelLine + band + pad);
        Bitmap(sheetWidth, sheetHeight, dc =>
        {
            PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, sheetWidth, sheetHeight), 0,
                PopupPaint.Ink(look.Dark ? Windows.UI.Color.FromArgb(255, 0x1C, 0x1C, 0x1C) : Windows.UI.Color.FromArgb(255, 0xE8, 0xE8, 0xE8)));
            var y = pad;
            for (var band = 0; band < bands.Count; band++)
            {
                for (var slot = 0; slot < across; slot++)
                {
                    var rows = (band * across) + slot + 1;
                    if (rows > most)
                    {
                        break;
                    }
                    var x = pad + (slot * (width + pad));
                    var height = LineRows.Px(rows, style);
                    PopupPaint.Text(dc, gfx, $"{rows} row{(rows == 1 ? "" : "s")}", label, Weight.Regular, theme.TextSecondary, x, y);
                    var plate = PopupPaint.Rect(x, y + labelLine, width, height);
                    PopupPaint.Fill(dc, plate, PopupPaint.Round(8 * scale), PopupPaint.Ink(theme.Base));
                    PopupPaint.Stroke(dc, plate, PopupPaint.Round(8 * scale), MathF.Max(1, MathF.Round(scale)), PopupPaint.Ink(theme.Stroke));
                    var context = dc;
                    PopupPaint.Clipped(context, plate, PopupPaint.Round(8 * scale), () => Shifted(context, plate.left, plate.top, () =>
                        FolderFlyout.PaintContent(context, island, PopupPaint.Rect(0, 0, width, height), style)));
                }
                y += labelLine + bands[band] + pad;
            }
        }, bitmap => Save(bitmap, path));
    }

    // --- What the lines show -------------------------------------------------------------------

    /// <summary>
    /// Every kind of this half made once, with what it shows held still — on the drawing thread, whose
    /// seams are its own (the clock's, Security's, the calendar's), with the mail in a store of its own
    /// and the notifications handed in.
    /// </summary>
    internal sealed class Stage : IDisposable
    {
        private const string Owner = "layout:default";

        private readonly Dictionary<ListKind, CellIsland> made = [];
        private readonly MailStore mail = new(new AccountSet([]));

        /// <summary>
        /// With <paramref name="few"/>, a few things to show — the sample week's events, three
        /// messages, two notifications — and else many: a busy week, twenty messages, eight
        /// notifications.
        /// </summary>
        public Stage(Gfx gfx, bool zone = false, bool few = false)
        {
            Clock.Now = () => Frozen;
            Security.Made = () => new SampleSecurity();
            CalendarIsland.Made = ListFormCases.Store(few ? ListFormCases.Week : ListFormCases.Busy);
            mail.Answered(few ? [.. Inbox(Frozen).Take(3)] : Inbox(Frozen), unread: few ? 2 : 14);
            Notifications = few ? 2 : 8;
            var config = new Config();
            try
            {
                foreach (var kind in Kinds)
                {
                    var line = kind.New();
                    line.Id = kind.Id();
                    if (kind == ListKind.Clock && zone)
                    {
                        line.Clock = new ClockOptions { TimeZone = "Tokyo Standard Time", Label = "Tokyo" };
                    }
                    var key = Config.IslandKey(Owner, line.Id);
                    CellIsland island = kind == ListKind.Mail
                        ? new MailIsland(key, line, gfx, config, mail, () => Frozen)
                        : (CellIsland)Karpo.Dock.Islands.CreateLine(key, line, kind, gfx, config)!;
                    if (island is NotificationPartTile list)
                    {
                        list.Shown = [.. ListLinesTests.Notifications(Frozen).Take(Notifications)];
                    }
                    _ = island.Refresh(gfx);
                    made[kind] = island;
                }
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public CellIsland this[ListKind kind] => made[kind];

        /// <summary>Another mail line over the stage's inbox, in the item style <paramref name="items"/>; the caller disposes it.</summary>
        public MailIsland Mail(Gfx gfx, ItemStyleOptions? items)
        {
            var line = ListKind.Mail.New();
            line.Id = "mail-" + Guid.NewGuid().ToString("N")[..6];
            line.Items = items;
            var island = new MailIsland(Config.IslandKey(Owner, line.Id), line, gfx, new Config(), mail, () => Frozen);
            _ = island.Refresh(gfx);
            return island;
        }

        /// <summary>How many notifications the list is handed.</summary>
        public int Notifications { get; }

        public void Dispose()
        {
            foreach (var island in made.Values)
            {
                island.Dispose();
            }
            made.Clear();
            mail.Dispose();
            CalendarIsland.Made = null;
            Security.Made = null;
            Clock.Now = Clock.Real;
        }
    }

    /// <summary>An inbox of twenty made-up messages, the newest four minutes old, a few of them unread.</summary>
    internal static List<MailItem> Inbox(DateTime now)
    {
        (string From, string Address, string Subject, bool Unread)[] said =
        [
            ("Sandra Gerber", "sandra@example.ch", "Lunch on Friday? The new place by the lake has a terrace", true),
            ("GitHub", "noreply@github.com", "[karpo-app] CI passed on master", true),
            ("Adrian Hager", "adrian@example.com", "Re: Quarterly review — slides attached", false),
            ("Swiss International Air Lines", "news@swiss.com", "Your boarding pass for LX 1832", true),
            ("Beat Hagenbuch", "beat@example.ch", "Photos", false),
        ];
        return [.. Enumerable.Range(0, 20).Select(index =>
        {
            var one = said[index % said.Length];
            return new MailItem($"{index}", one.From, one.Address, one.Subject, $"Hi, {one.From.Split(' ')[0]} here. A few words on {one.Subject.ToLowerInvariant()} before the meeting.",
                new DateTimeOffset(now.AddMinutes(-4 - (index * 37))), one.Unread && index < 10);
        })];
    }

    /// <summary>Eight made-up notifications from five apps, the newest three minutes before <paramref name="at"/>.</summary>
    internal static List<NotificationEntry> Notifications(DateTime at)
    {
        (string App, string Title, string Body)[] said =
        [
            ("Mail", "Vera Holm", "Re: the roof — I've asked them for a second quote, and they say Thursday at the earliest."),
            ("Calendar", "Standup in 10 minutes", "Daily · Team room"),
            ("Messages", "Dad", "Landed! Getting a coffee then I'll find the taxi rank."),
            ("Photos", "3 years ago today", "A morning at the lake."),
            ("Mail", "Nils Ahlberg", "Invoice 2214 attached."),
            ("Updates", "Restart to finish updating", "Karpo will reopen where you left it."),
            ("Messages", "Mia", "Are we still on for tonight?"),
            ("Calendar", "Dentist tomorrow at 8:00", "Bring the insurance card."),
        ];
        return [.. said.Select((line, index) => new NotificationEntry(
            index + 1, line.App.ToLowerInvariant(), line.App, null, line.Title, line.Body, at.AddMinutes(-3 - (index * index * 4)), null))];
    }
}
