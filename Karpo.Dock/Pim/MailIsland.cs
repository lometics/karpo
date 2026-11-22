// The mail widget: the inbox's line of a list, laid out for the height it is
// given as every list line is (docs\widgets.md §9, "A line at every height"):
// the newest message and how much is unread on one row; taller, the count over
// every message, each its sender over its subject — the count stays and the
// messages scroll under it where there are more than the line holds
// (ListScroll.cs), drawn in the item style chosen (ItemForm). It was a
// taskbar entry too, an envelope with the unread count whose click opened the
// newest mail (MailFlyout), until that became the mail preset (2026-10-10,
// docs\widgets.md §95): a folder wearing the envelope (MailFaceDrawing) and
// holding this line, which shows what the panel showed.
//
// The count is what it draws, so its first draw is the look that starts the
// first read (docs\widgets.md §11) — posted, never awaited: until the answer
// arrives the line has no count.
//
// **A message is a row of its own** (2026-10-11): it lights under the pointer and pressed as a
// Windows 11 list item does, and a click opens it in the account's web mail (MailItem.Link,
// MailLinks) — the message itself where the account names it (Outlook on the web, Gmail), else the
// inbox of the web mail its server is known by — which the folder carries out as it carries out a
// headline's (CellAct.Action, Dock.FolderForward), closing as the browser comes. A message of a
// server Karpo knows no web mail for is no target: it is read, not pressed. With no account that
// gives mail, a line is "Set up in Settings", and a click opens the Accounts page (Unset).
//
// Taller than a row, the messages stand on the list's gutter, under the envelope rather than under
// the count's words: a row begins where the line does, with nothing kept empty before it. The stale
// mark stands at the end of the count's row, or of the one row; the inboxes are read again from
// "Refresh" on its right click (IRefreshable, §451).

using System.Globalization;
using System.Numerics;
using Karpo.Accounts;
using Karpo.Core;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock.Pim;

internal sealed unsafe class MailIsland : CellIsland, IListContent, IListScroll, ILinePieces, IRefreshable, ILineTargets
{
    /// <summary>Segoe Fluent Icons: Mail, a closed envelope.</summary>
    internal const string MailGlyph = "";

    /// <summary>
    /// Messages a new list line asks the list for room for, at most: where a new line starts. A
    /// line made taller shows more, as many as the store has and the height holds.
    /// </summary>
    private const int LineMost = 8;

    private readonly MailStore store;
    private readonly Func<DateTime> now;

    // The message under the pointer (its index from 1, as Click numbers them; 0 for none), the one a
    // press is down on, and the link a click took, for the dock to open once the folder has gone.
    private int hot;
    private RowPress press;
    private string? chosen;

    /// <summary>
    /// Tests only, on the thread that makes the widget or the folder: the store a line made by the
    /// registry and a mail face read, instead of the shared one, so no account is ever reached.
    /// </summary>
    [ThreadStatic]
    internal static MailStore? Stand;

    /// <summary>The store a mail widget or face made on this thread reads: the test's, else the dock's.</summary>
    internal static MailStore Store => Stand ?? (Samples.On ? Samples.Mail() : MailStore.Shared);

    public MailIsland(string key, WidgetItem island, Gfx gfx, Config config)
        : this(key, island, gfx, config, Store)
    {
    }

    /// <summary>An island on <paramref name="store"/>: the dock's is the shared one; tests bring their own.</summary>
    internal MailIsland(string key, WidgetItem island, Gfx gfx, Config config, MailStore store, Func<DateTime>? now = null)
        : base(key, island, gfx)
    {
        this.store = store;
        this.now = now ?? (() => Clock.Real());
        store.Attach(this);
    }

    /// <summary>§455: what this line shows of the store: its messages and its unread count.</summary>
    internal MailView Shown => store.View(SourceFilter.Of(Settings));

    /// <summary>An account that gives mail ("Refresh", IRefreshable).</summary>
    public bool CanRefresh => store.HasAccount;

    /// <summary>The inboxes are being read.</summary>
    public bool Refreshing => store.Reading;

    /// <summary>When the messages shown were read.</summary>
    public DateTime? UpdatedUtc => store.Freshness.UpdatedUtc;

    /// <summary>Why an inbox could not be read, or "Last updated 14:05 — couldn't refresh".</summary>
    string? IRefreshable.Problem => store.Problem ?? store.Freshness.Tip(now());

    /// <summary>"Refresh": every inbox read again now, however recently it was (<see cref="MailStore.Refresh"/>).</summary>
    public void RefreshNow() => store.Refresh(force: true);

    /// <summary>Never ticks: the store reads on its own clock and wakes this widget.</summary>
    public override int IntervalMs => 0;

    protected override int Count => 1;

    public override void Configure(WidgetItem island, Config config)
    {
        Settings = island;
        Forget();
    }

    /// <summary>No account gives mail, as the last read found: the Accounts page (SetupPrompt.cs).</summary>
    internal override SetupNeed? Unset => !store.HasAccount && !store.Loading ? SetupNeed.Mail : null;

    public override string? Tooltip(int index) => Tip(store, now(), Shown.Unread);

    /// <summary>
    /// What a screen reader says of the mail, the mail face's words as they were the entry's: why an
    /// inbox cannot be read, else how many are unread, and from when where it could not be refreshed.
    /// The bar shows none over the face (§367).
    /// </summary>
    internal static string Tip(MailStore store, DateTime now, int? unread = null)
    {
        var said = store.Problem is { } problem ? problem : (unread ?? store.Unread) switch
        {
            0 => S.DockMailTitle,
            var count => S.DockMailUnreadCount(count),
        };
        return store.Freshness.Tip(now) is { } stale ? $"{said}. {stale}" : said;
    }

    /// <summary>
    /// A message (its index from 1) with a link is an app opening — the message in the browser, which
    /// the folder carries out (<see cref="Open"/>); anywhere else does nothing.
    /// </summary>
    public override IslandAction Click(int index)
    {
        if (press.Up())
        {
            _ = Wake();
        }
        chosen = LinkOf(index);
        return chosen is not null ? new IslandAction(View: IslandView.App) : default;
    }

    /// <summary>The dock, once the folder has gone: opens the message the click took, once.</summary>
    internal void Open()
    {
        if (chosen is { } link)
        {
            chosen = null;
            Opened(link);
        }
    }

    /// <summary>
    /// Where a message opens: the browser, through the shell, as a headline does. A test puts its own
    /// here and writes down what it was handed. Only ever handed an https address (MailLinks.Web).
    /// </summary>
    internal Action<string> Opened { get; set; } = static link => Launch.Open(link);

    /// <summary>Message <paramref name="index"/>'s link (from 1), where it has one Karpo opens; else null.</summary>
    private string? LinkOf(int index) =>
        Showing() && index >= 1 && index <= Shown.Messages.Count ? MailLinks.Web(Shown.Messages[index - 1].Link) : null;

    /// <summary>Closed, nothing is under the pointer or pressed.</summary>
    public override void Visible(bool shown)
    {
        base.Visible(shown);
        if (!shown)
        {
            hot = 0;
            _ = press.Up();
        }
    }

    public override void Dispose()
    {
        store.Release(this);
        base.Dispose();
    }

    /// <summary>The store posted: it raises Changed, so an open list follows.</summary>
    protected override bool Sample(bool everything)
    {
        if (everything)
        {
            store.Deliver();
        }
        return false;
    }

    /// <summary>
    /// Never drawn on a bar: it is a line of a list and nothing else (<see cref="ListWidgets"/>);
    /// on the bar the mail is the preset's face (MailFaceDrawing).
    /// </summary>
    internal override void PaintCell(ID2D1DeviceContext* dc, int index, in IslandStyle style, float width, float height) =>
        throw new NotSupportedException($"{Info.Id} is a line of a folder's list, not a widget of a bar.");

    /// <summary>
    /// "Last updated 14:05 — couldn't refresh" while an inbox could not be read and what it gave
    /// before is shown (StaleMark.cs): the mark's tooltip on the line.
    /// </summary>
    internal override string? StaleTip => store.Freshness.Tip(now());

    /// <summary>The stale mark's tooltip, over the mark (StaleMark.cs).</summary>
    protected internal override (string Text, D2D_RECT_F Part)? CardTip(System.Numerics.Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface) =>
        StaleMark.Tip(LinePieces(box, style), point, StaleTip, style);

    // --- In a folder ---------------------------------------------------------------

    /// <summary>Its messages light under the pointer themselves, as a list's rows do: the line as a whole is not one thing to press.</summary>
    protected internal override bool LightsItsRows => true;

    /// <summary>The message at the point, its index from 1, where it opens; 0 beside them.</summary>
    protected internal override int CardTarget(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface) =>
        LineFocus.At(LineTargets(box, style), point);

    /// <summary>keys: its targets as it lays them out, which the pointer and the keys both read (ILineTargets).</summary>
    public IReadOnlyList<(int Target, D2D_RECT_F Box)> LineTargets(D2D_RECT_F box, in TileStyle style)
    {
        var targets = new List<(int Index, D2D_RECT_F Box)>();
        Line(new LineSketch(null, Gfx), box, style, targets);
        return targets;
    }

    protected internal override void CardHover(Vector2? point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var target = point is { } at ? CardTarget(at, box, style, surface) : 0;
        var let = press.Over(target);
        if (target == hot && !let)
        {
            return;
        }
        hot = target;
        _ = Wake();
    }

    /// <summary>A press on a message lights it pressed; the press stays the folder's, which makes it the click (RowPress).</summary>
    protected internal override void CardDown(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        if (press.Down(CardTarget(point, box, style, surface)))
        {
            _ = Wake();
        }
    }

    /// <summary>tests: the message under the pointer and the one pressed, from 1; 0 for none.</summary>
    internal (int Hot, int Pressed) Lighted => (hot, press.Held);

    protected internal override void PaintCard(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        store.Refresh();
        Line(new LineSketch(dc, Gfx), box, style, null);
    }

    /// <summary>Whether the store has an inbox to show: an account, a read that came back, and no failure with nothing in it.</summary>
    private bool Showing() =>
        store.HasAccount && !store.Loading && !(store.Problem is not null && Shown.Messages.Count == 0);

    /// <summary>The count as a line shows it: "999+" past that.</summary>
    internal static string CountText(int unread) =>
        unread > 999 ? "999+" : unread.ToString(CultureInfo.InvariantCulture);

    // --- As a line of a list --------------------------------------------------------

    /// <summary>What a line of one row shows first: how much is unread, or why nothing can be said.</summary>
    public string MainPiece => "head";

    public IReadOnlyList<LinePiece> LinePieces(D2D_RECT_F box, in TileStyle style)
    {
        var sketch = new LineSketch(null, Gfx);
        Line(sketch, box, style, null);
        return sketch.Pieces;
    }

    /// <summary>
    /// How tall a new line is: the count over the newest messages, each its sender over its subject,
    /// up to <see cref="LineMost"/> of them; the rest scroll.
    /// </summary>
    public float ListHeight(float width, in TileStyle style)
    {
        var form = ListForm.Of(Gfx, style);
        var items = ItemForm.For(Gfx, style, Settings);
        var count = Showing() ? Shown.Messages.Count : 0;
        // Nothing to list: the kit's one Status, one row (§388).
        return count == 0 ? LineRows.Px(1, style) : Whole(style, form, items, Math.Min(LineMost, count));
    }

    /// <summary>Every message, where the line lists them: the rest scroll under the count (ListScroll.cs).</summary>
    public float ScrollHeight(float width, float height, in TileStyle style)
    {
        var form = ListForm.Of(Gfx, style);
        var items = ItemForm.For(Gfx, style, Settings);
        var count = Showing() ? Shown.Messages.Count : 0;
        return count == 0 || height + 0.5f < Lists(style, form, items) ? 0 : Whole(style, form, items, count);
    }

    /// <summary>The count stays while the messages scroll.</summary>
    public float ScrollHead(float width, float content, in TileStyle style) => HeadLine(style, ListForm.Of(Gfx, style));

    /// <summary>The line with the count over <paramref name="count"/> messages.</summary>
    private float Whole(in TileStyle style, ListForm form, ItemForm items, int count) =>
        HeadLine(style, form) + (count * Each(style, items));

    /// <summary>The least a line lists the messages at: the header and the first message's first line; under it, the line of one row.</summary>
    private float Lists(in TileStyle style, ListForm form, ItemForm items) =>
        HeadLine(style, form) + items.Space(style, ItemAir) + items.Line;

    /// <summary>The header on a line taller than a row: the kit's Header, one of the list's rows (§401).</summary>
    private float HeadLine(in TileStyle style, ListForm form) => LineRow.Height(style, caption: false);

    /// <summary>The air over and under a message's words, DIP at the item style's size: a message is the kit's item, 52 DIP at the list's.</summary>
    private const float ItemAir = 8;

    /// <summary>A message: its sender over its subject, and its first words under them where they show, with its air.</summary>
    private static float Each(in TileStyle style, ItemForm items) =>
        (2 * items.Space(style, ItemAir)) + items.Line + items.SmallLine + (items.Preview ? items.SmallLine : 0);

    /// <summary>The count's words: how much is unread, or why nothing can be said.</summary>
    private string Head(IReadOnlyList<MailItem> messages) =>
        !store.HasAccount ? S.DockMailNoAccount
        : store.Loading ? S.DockMailReading
        : store.Problem is not null && messages.Count == 0 ? S.DockMailCannotRead
        // Nothing in any inbox: said so, as the panel said it, rather than "All read" over nothing.
        : messages.Count == 0 ? S.DockMailInboxEmpty
        : Shown.Unread switch { 0 => S.DockMailAllRead, var count => S.DockMailUnreadShort(CountText(count)) };

    /// <summary>
    /// The line, at whatever height it was given (docs\widgets.md §9, "A line at every height"), drawn
    /// where the sketch draws:
    /// <list type="bullet">
    /// <item>One row: the envelope in the list's leading column, the newest message — its sender and
    /// its subject, shorter as the row needs — where every name begins, and how much is unread
    /// against the trailing gutter (ListTiers.Row); with no message, only why.</item>
    /// <item>Taller: the count at the list's figure size, and under it the messages, each its sender
    /// and when it came over its subject — and its first words, where the item style shows them —
    /// from the top down. Given less than they take, the line is laid out as tall as all of them and
    /// scrolled (ListScroll.cs).</item>
    /// </list>
    /// Taller than a row, the messages stand on the list's gutter. Each message that opens is put in
    /// <paramref name="targets"/> (its index from 1) where they are asked for, and lights under the
    /// pointer and pressed.
    /// </summary>
    private void Line(LineSketch sketch, D2D_RECT_F box, in TileStyle style, List<(int Index, D2D_RECT_F Box)>? targets)
    {
        var form = ListForm.Of(Gfx, style);
        var items = ItemForm.For(Gfx, style, Settings);
        var theme = style.Theme;
        var height = box.bottom - box.top;
        var left = box.left + form.TextLeft;
        // Stale, the mark at the trailing gutter: the line's words kept clear of it (StaleMark.cs).
        var stale = store.Freshness.Stale;
        var mark = StaleMark.Room(style, form, stale);
        var room = MathF.Max(1, form.Room(TilePaint.Width(box)) - mark);
        var messages = Showing() ? Shown.Messages : [];
        var head = Head(Shown.Messages);
        var clock = now();
        var envelope = Shown.Unread > 0 ? TilePaint.Accent(style) : theme.TextSecondary;
        var headLine = HeadLine(style, form);
        if (messages.Count == 0 || height + 0.5f < Lists(style, form, items))
        {
            var newest = messages.Count > 0 ? messages[0] : null;
            string[] names = newest is null ? [head] : [$"{Sender(newest)} — {newest.Subject}", Sender(newest)];
            var field = newest is null ? 0 : PopupPaint.Measure(Gfx, head, form.TextPx, Weight.Semibold).X;
            if (newest is null)
            {
                // Nothing to list, or still reading: the kit's one Status, on the top row (§388).
                LineStatus.Draw(sketch, box, style, MailGlyph, head, [], mark);
                if (stale)
                {
                    StaleMark.Sketch(sketch, box, style, form, box.top, MathF.Min(height, LineRows.Px(1, style)));
                }
                return;
            }
            var one = ListTiers.Row(Gfx, box, style, names, newest is null ? Weight.Semibold : Weight.Regular, field, mark);
            if (newest?.Link is not null && one.Name is not null)
            {
                // The newest message is the line's one item: all of it (LineItem).
                var band = LineItem.Band(box, box.top, box.bottom);
                targets?.Add((1, band));
                Lit(sketch, 1, band, style);
            }
            sketch.Glyph("glyph", MailGlyph, form.Glyph, envelope, form.IconBox(box.left, one.TextTop, form.Line));
            if (stale)
            {
                StaleMark.Sketch(sketch, box, style, form, one.TextTop, form.Line);
            }
            if (newest is null)
            {
                sketch.Text("head", one.Name ?? "", form.TextPx, Weight.Semibold, theme.Text, one.NameLeft, one.TextTop, one.NameRoom);
                return;
            }
            if (one.Name is { } name)
            {
                sketch.Text("newest", name, form.TextPx, Weight.Regular, theme.TextSecondary, one.NameLeft, one.TextTop, one.NameRoom);
            }
            sketch.TextRight("head", head, form.TextPx, Weight.Semibold, theme.Text, one.FieldRight, one.TextTop, one.Field);
            return;
        }
        // The header, the kit's (§401): the envelope, the mail's name in the Body's strong weight, how much is unread
        // as its meta, the stale mark at its end. Content shorter than the line stands at its top.
        var header = LineRow.Draw(sketch, box.left, box.right, box.top, style, new RowSpec(S.DockMailTitle)
        {
            Glyph = MailGlyph,
            GlyphInk = envelope,
            Strong = true,
            Reading = head,
            Look = RowReading.Meta,
            Trailing = mark,
            NamePiece = "title",
            ReadingPiece = "head",
        }, headLine);
        if (stale)
        {
            StaleMark.Sketch(sketch, box, style, form, box.top, headLine);
        }
        var y = header.Bottom;
        // The messages, the kit's items: their words where every name begins, an inbox's colour as a bar in the
        // leading column where there are several, when each came against the trailing gutter on its sender's line.
        var rowRight = box.right - form.Inset;
        var rowRoom = MathF.Max(1, rowRight - left);
        var bar = MathF.Max(2, style.Px(3));
        var column = form.IconBox(box.left, 0, form.Icon);
        var barLeft = PopupPaint.Round(((column.left + column.right) / 2) - (bar / 2));
        var each = Each(style, items);
        var air = items.Space(style, ItemAir);
        for (var index = 0; index < messages.Count; index++)
        {
            // Only what the line's box holds: the rest is under the window it scrolls through.
            if (y + each > box.bottom + 0.5f)
            {
                break;
            }
            if (sketch.Draws && Hidden(y, y + each))
            {
                y += each;
                continue;
            }
            var message = messages[index];
            if (message.Link is not null)
            {
                // The message with its air over and under it, edge to edge: the next one's begins where it ends (LineItem).
                var band = LineItem.Band(box, y, y + each);
                targets?.Add((index + 1, band));
                Lit(sketch, index + 1, band, style);
            }
            y += air;
            var weight = message.Unread ? Weight.Semibold : Weight.Regular;
            var tall = items.Line + items.SmallLine + (items.Preview ? items.SmallLine : 0);
            var stamp = Stamp(message, clock, items.Time);
            var time = stamp.Length > 0
                ? sketch.TextRight($"time {index}", stamp, items.SmallPx, Weight.Regular, theme.TextSecondary, rowRight,
                    y + PopupPaint.Round((items.Line - items.SmallLine) / 2), message.Folder is null ? rowRoom / 3 : rowRoom / 2)
                : default;
            if (message.Colour is { } colour)
            {
                sketch.Plate($"inbox {index}", PopupPaint.Rect(barLeft, y, bar, tall), bar / 2, CalendarPainter.Solid(CalendarColours.Of(colour), theme));
            }
            var end = time.right > time.left ? time.left - style.Px(LineSpace.GapDip) : rowRight;
            sketch.Text($"sender {index}", Sender(message), items.TextPx, weight, theme.Text, left, y, MathF.Max(1, end - left));
            sketch.Text($"subject {index}", message.Subject, items.SmallPx, Weight.Regular, theme.TextSecondary, left, y + items.Line, rowRoom);
            y += items.Line + items.SmallLine;
            if (items.Preview)
            {
                sketch.Text($"preview {index}", Previewed(message), items.SmallPx, Weight.Regular, theme.TextTertiary, left, y, rowRoom);
                y += items.SmallLine;
            }
            y += air;
        }
    }

    /// <summary>Message <paramref name="index"/>'s band (from 1, LineItem) while the pointer is on it: pressed while a press is down on it.</summary>
    private void Lit(LineSketch sketch, int index, D2D_RECT_F band, in TileStyle style)
    {
        if (index == hot)
        {
            LineItem.Light(sketch, band, style.Theme, press.On(index));
        }
    }

    /// <summary>A message's first words on one line: its preview with its breaks as spaces.</summary>
    private static string Previewed(MailItem message) =>
        string.Join(' ', (message.Preview ?? "").Split((char[])['\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    /// <summary>
    /// What stands at the end of a message's sender's line: when it came (where the item style shows the time), after its
    /// folder's name where the line shows more than one folder (§639: "Archive · 3h", <see cref="MailItem.Folder"/>).
    /// </summary>
    internal static string Stamp(MailItem message, DateTime now, bool time) => (message.Folder, time) switch
    {
        ({ } folder, true) when When(message, now) is { Length: > 0 } when => S.SettingsAccountsSyncPair(first: folder, second: when),
        ({ } folder, _) => folder,
        (null, true) => When(message, now),
        _ => "",
    };

    /// <summary>When a message came, as the notification list says it: "5m", "3h", "Yesterday", "12 Sep".</summary>
    internal static string When(MailItem message, DateTime now) =>
        message.Received == DateTimeOffset.MinValue
            ? ""
            : NotificationText.Relative(message.Received.LocalDateTime, now, Panels.Nls.MonthDay, Panels.Nls.ShortDate);

    /// <summary>Who a message is from: their name, else their address.</summary>
    private static string Sender(MailItem message) => message.From is { Length: > 0 } name ? name : message.FromAddress ?? "";
}
