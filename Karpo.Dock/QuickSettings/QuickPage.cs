// What every quick-settings page shares: a title row (with a radio's switch at
// the right), a body of rows, and a footer band with the "More … settings"
// link, placed on the shared panel width (Flyout.PanelWidthDip) the way Windows 11's quick settings place
// theirs. A page keeps its widgets between builds, so what the pointer is over,
// what has the keyboard and what was typed survive the list changing under
// them; it builds again whenever its service reports news.

using Karpo.Dock.QuickSettings.Controls;
using System.Numerics;
using Karpo.Dock.QuickSettings.Services;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock.QuickSettings;

/// <summary>The quick settings' pages: the flyouts the quick-settings host shows, and the ones that are lines.</summary>
public enum QuickKind
{
    /// <summary>The network page: a line of a list folder (QuickPageTile), never a flyout of its own since 2026-10-10.</summary>
    Wifi,

    /// <summary>The Bluetooth page: a line of a list folder, as the network page is.</summary>
    Bluetooth,

    /// <summary>
    /// quick: the power mode of Windows' "Power &amp; battery" page, its three modes as rows. A line of
    /// a folder (QuickPageTile); it was a part of the battery and power flyouts. The power commands
    /// beside it were a page too, until each became a button of its own (PowerButtonTile).
    /// </summary>
    PowerMode,

    /// <summary>The desktop you are on's name, from the right click of the desktops preset's face.</summary>
    RenameDesktop,

    /// <summary>
    /// quick: the mobile hotspot's page: its switch, its network, its devices. A line of a folder
    /// (QuickPageTile) since the button that opened it is the mobile hotspot preset; never a flyout.
    /// </summary>
    Hotspot,

    /// <summary>quick: Windows' Cast panel, the wireless displays in range: a line of the cast preset (QuickPageTile).</summary>
    Cast,

    /// <summary>
    /// quick: Win+P's modes as a line of the cast and project presets (QuickPageTile), which was the
    /// project entry's flyout until that became the project preset (2026-10-11).
    /// </summary>
    Projection,

    /// <summary>quick: Windows' nearby sharing: a line of the nearby sharing preset (QuickPageTile).</summary>
    Nearby,
    /// <summary>
    /// keyboard: the keyboard layouts to switch to, the one in use marked. A line of a folder
    /// (QuickPageTile) since the input indicator that listed them is the keyboard preset; never a flyout.
    /// </summary>
    Keyboard,

    /// <summary>
    /// quick: the VPN connections, each connected or disconnected from its row. A line of a folder
    /// (QuickPageTile, ChoicesPage) since the VPN entry that listed them is the VPN preset; never a flyout.
    /// </summary>
    Vpn,
}

/// <summary>What a page may ask of the flyout it is in.</summary>
internal interface IQuickHost
{
    /// <summary>Posts <paramref name="what"/> to the page on the dock's thread; safe from any thread, and dropped once the showing has ended.</summary>
    void Notify(int what);

    /// <summary>The page's content changed: it is built and placed again.</summary>
    void Rebuild();

    /// <summary>
    /// Asks for <see cref="QuickPage.Notified"/> again in <paramref name="milliseconds"/>, for what the
    /// system doesn't report: a scan that never says it finished. One at a time, and gone when the
    /// flyout closes, so nothing runs while nothing shows.
    /// </summary>
    void Wake(int milliseconds);

    /// <summary>Closes the flyout and has the dock open <paramref name="uri"/> (a Settings page).</summary>
    void Launch(string uri);

    /// <summary>Closes the flyout.</summary>
    void Close();

    /// <summary>Closes the flyout and has the dock run <paramref name="link"/> (sleep, shut down, lock).</summary>
    void Power(QuickLink link);

    /// <summary>Gives <paramref name="widget"/> the keyboard (after the next build has placed it).</summary>
    void Focus(Widget widget);
}

internal abstract class QuickPage : IDisposable
{
    private readonly Backdrop band = new(BackdropKind.Footer);
    private Button? link;

    protected QuickPage(IQuickHost host)
    {
        Host = host;
        Changes = new Changes(host.Notify);
    }

    public abstract QuickKind Kind { get; }

    protected IQuickHost Host { get; }

    /// <summary>What the page's service heard; its first news posts to the page.</summary>
    protected Changes Changes { get; }

    /// <summary>The flyout is up: start listening and reading.</summary>
    public virtual void Start()
    {
    }

    /// <summary>A post from the service, on the dock's thread: read what changed, and build again.</summary>
    public void Notified(int what)
    {
        Refresh(Changes.Take() | what);
        Host.Rebuild();
    }

    /// <summary>Reads the service again after <paramref name="news"/> (the service's own bits).</summary>
    protected virtual void Refresh(int news)
    {
    }

    /// <summary>Places the page's widgets for <paramref name="layout"/>.</summary>
    public abstract void Build(PageLayout layout);

    /// <summary>
    /// Whether the page, as a line, fits itself to the height it is given (<see cref="PageLayout.Room"/>)
    /// rather than scrolling under its title: a form for every height, from one row up. Its line
    /// lays it out again whenever that height changes. False by default.
    /// </summary>
    public virtual bool Fits => false;

    /// <summary>The flyout closed: stop listening, let go of what the service holds.</summary>
    public virtual void Dispose()
    {
    }

    /// <summary>The footer with its "More … settings" link.</summary>
    protected void Footer(PageLayout layout, string text, string uri, Button? action = null)
    {
        if (link is null || link.Text != text)
        {
            link = new Button(text, ButtonStyle.Link) { Name = text, Clicked = () => Host.Launch(uri) };
        }
        layout.Footer(band, link, action);
    }

    /// <summary>
    /// A message where a list would be (nothing found, no access, no device): a big faint glyph, a line, and
    /// a quieter wrapped explanation, centred.
    /// </summary>
    internal static void Notice(PageLayout layout, Notice notice)
    {
        var context = layout.Context;
        var width = layout.TextRight - layout.TextLeft;
        layout.Gap(16);
        layout.Place(notice.Icon, layout.TextLeft, width, context.D(40));
        layout.Gap(8);
        layout.Place(notice.Title, layout.TextLeft, width, MathF.Ceiling(Label.Measure(context, notice.Title.Text, notice.Title.Style, width, notice.Title.Lines)));
        if (notice.Detail.Text.Length > 0)
        {
            layout.Gap(4);
            layout.Place(notice.Detail, layout.TextLeft, width, MathF.Ceiling(Label.Measure(context, notice.Detail.Text, notice.Detail.Style, width, notice.Detail.Lines)));
        }
        layout.Gap(12);
    }

    /// <summary>A wrapped line of text across the text column, as high as it needs.</summary>
    internal static void Text(PageLayout layout, Label label, float left, float right)
    {
        var width = MathF.Max(1, right - left);
        layout.Place(label, left, width, MathF.Ceiling(MathF.Max(Label.Measure(layout.Context, label.Text, label.Style, width, label.Lines), 1)));
    }
}

/// <summary>A notice's three parts, kept by its page.</summary>
internal sealed class Notice
{
    public Notice(string glyph)
    {
        Icon = new GlyphIcon(glyph, 32, tertiary: true);
        Title = new Label("", LabelStyle.Body, lines: 2, centred: true);
        Detail = new Label("", LabelStyle.Caption, lines: 4, centred: true);
    }

    public GlyphIcon Icon { get; }

    public Label Title { get; }

    public Label Detail { get; }

    /// <summary>Says <paramref name="title"/> and <paramref name="detail"/> with <paramref name="glyph"/>.</summary>
    public Notice Say(string glyph, string title, string detail)
    {
        Icon.Glyph = glyph;
        Title.Text = title;
        Detail.Text = detail;
        return this;
    }
}

/// <summary>A page's widgets by key, kept from one build to the next; what a build didn't ask for is let go.</summary>
internal sealed class Keyed<T>
    where T : Widget
{
    private Dictionary<string, T> kept = [];
    private Dictionary<string, T> asked = [];

    public T Get(string key, Func<T> make)
    {
        if (!asked.TryGetValue(key, out var widget))
        {
            if (!kept.TryGetValue(key, out widget))
            {
                widget = make();
            }
            asked[key] = widget;
        }
        return widget;
    }

    /// <summary>The build is over: only what it asked for is kept.</summary>
    public void Done()
    {
        (kept, asked) = (asked, kept);
        asked.Clear();
    }
}

/// <summary>Where a page's widgets go: the header, the body (from the top, down) and the footer, in panel px.</summary>
internal sealed class PageLayout
{
    private readonly List<Widget> header = [];
    private readonly List<Widget> body = [];
    private readonly List<Widget> footer = [];
    private readonly List<Widget> over = [];
    private float headerHeight;
    private float footerHeight;

    // How much of the body is a scroll away however tall the panel may be: what a list has past the
    // rows it shows (List).
    private float hidden;

    // Where rows and text start and end across, and where the title's switch ends: the panel's own
    // insets, or a list's gutter for a page that is a line (Lined).
    private readonly float rowInset;
    private readonly float textInset;
    private readonly float trailingInset;

    /// <param name="scrolled">How far the body is scrolled now, so what hangs over the page lands where its row is.</param>
    public PageLayout(QuickContext context, float maxHeight, float scrolled = 0)
    {
        Context = context;
        Width = context.D(Flyout.PanelWidthDip);
        MaxHeight = maxHeight;
        Scrolled = scrolled;
        rowInset = context.D(8);
        textInset = context.D(20);
        trailingInset = context.D(16);
    }

    /// <summary>
    /// A page laid out as a line of a list folder (QuickPageTile): <paramref name="width"/> px wide,
    /// its rows, its words and its title's switch all on the list's <paramref name="gutter"/>, as tall
    /// as what it shows — no list held to a height, nothing scrolled — and no footer, which is the
    /// actions line under it.
    /// </summary>
    /// <param name="back">The way back the title row is, for a page open in a folder's column (<see cref="Back"/>).</param>
    /// <param name="style">The list's style (<see cref="Style"/>).</param>
    /// <param name="room">The height the line gives a page that fits itself to it (<see cref="Room"/>).</param>
    public static PageLayout ForLine(QuickContext context, float width, float gutter, BackHead? back = null, TileStyle? style = null, float room = float.PositiveInfinity) =>
        new(context, width, gutter, lined: true) { Back = back, Style = style, Room = room };

    private PageLayout(QuickContext context, float width, float gutter, bool lined)
    {
        Context = context;
        Width = width;
        MaxHeight = float.MaxValue;
        Lined = lined;
        rowInset = gutter;
        textInset = gutter;
        trailingInset = gutter;
    }

    /// <summary>Whether the page is a line of a list rather than a flyout (see the constructor).</summary>
    public bool Lined { get; }

    /// <summary>
    /// The height the line gives the page, in px, for a page that fits itself to it (<see cref="QuickPage.Fits"/>);
    /// infinite where it is measured for all it shows, and in a flyout.
    /// </summary>
    public float Room { get; init; } = float.PositiveInfinity;

    /// <summary>The list's style, for a page that is a line: what its one-row and compact forms take the list's edges from (ListForm, ListTiers).</summary>
    public TileStyle? Style { get; init; }

    /// <summary>A layout of the same line with no room set: the page measured whole (<see cref="Room"/>), placed nowhere.</summary>
    public PageLayout Whole() => new(Context, Width, rowInset, lined: true) { Back = Back, Style = Style };

    /// <summary>The way back the title row is, for a page open in a folder's column in place of its lines; null for none.</summary>
    public BackHead? Back { get; init; }

    /// <summary>How far the body is scrolled: body px plus the header, less this, are panel px.</summary>
    public float Scrolled { get; }

    public QuickContext Context { get; }

    /// <summary>
    /// The panel's width: <see cref="Flyout.PanelWidthDip"/>, which every flyout of an island's
    /// content shares (Windows' own quick settings are that wide). Read from there rather than
    /// written again here, so changing it changes every panel.
    /// </summary>
    public float Width { get; }

    public float MaxHeight { get; }

    /// <summary>The body's next free y.</summary>
    public float Y { get; set; }

    /// <summary>Where rows start and end across: 8 DIP in from the panel's sides, or on a list's gutter.</summary>
    public float RowLeft => rowInset;

    public float RowRight => Width - rowInset;

    public float RowWidth => RowRight - RowLeft;

    /// <summary>Where text lines up with the title: 20 DIP in, or on a list's gutter.</summary>
    public float TextLeft => textInset;

    public float TextRight => Width - textInset;

    /// <summary>
    /// The title row: the title 20 DIP in, and <paramref name="trailing"/> (a switch, a button) at the right,
    /// right to left, each with its own width.
    /// </summary>
    public void Title(Label title, params (Widget Widget, float Width)[] trailing)
    {
        var height = Context.T(52);
        var right = Width - trailingInset;
        var left = TextLeft;
        // A page open in a folder's column (FolderPages): the whole row its way back, Windows' back arrow
        // before the title, lit as the list's rows are, edge to edge (LineItem); the trailing controls stand over it.
        if (Back is { } back)
        {
            var side = MathF.Min(height, Context.D(36));
            var at = MathF.Max(0, RowLeft - Context.D(10));
            var arrow = PopupPaint.Rect(at, PopupPaint.Round((height - side) / 2), side, side);
            var row = new D2D_RECT_F(0, 0, Width, height);
            back.Place(row, row, arrow);
            back.Title = title.Text;
            header.Add(back);
            left = arrow.right + Context.D(4);
        }
        foreach (var (widget, width) in trailing)
        {
            var box = MathF.Min(height, Context.D(40));
            widget.Bounds = new D2D_RECT_F(right - width, PopupPaint.Round((height - box) / 2), right, PopupPaint.Round((height - box) / 2) + box);
            header.Add(widget);
            right -= width + Context.D(8);
        }
        title.Bounds = new D2D_RECT_F(left, 0, MathF.Max(left + 1, right - Context.D(8)), height);
        // Over the way back, under what stands at its end.
        header.Insert(Back is null ? 0 : 1, title);
        headerHeight = height;
    }

    /// <summary>
    /// Where a list's row (<see cref="ListRow"/>) has its glyph's column: on a list's line the gutter, so its words
    /// stand where every name begins (§388); in a flyout <see cref="ListRow.PadDip"/> in from the rows.
    /// </summary>
    public float ListLead => Lined ? rowInset : rowInset + Context.D(ListRow.PadDip);

    /// <summary>Where a list's row's words begin, and what stands under them (an opened network's offer).</summary>
    public float ListTextLeft => ListLead + ListRow.TextAfter(Context);

    /// <summary>Where a list's row's last piece ends, and what stands under it.</summary>
    public float ListTextRight => Width - ListLead;

    // The list's item placed last — a row, or an opened row's card — where it ended and how many widgets the body
    // held then: a row placed after it with nothing but room between them meets it (Row).
    private Widget? item;
    private float itemEnd;
    private int itemCount;

    /// <summary>
    /// A widget across the rows' width, <paramref name="height"/> px high, at the body's cursor. A list's row is an
    /// item (LineItem, §428): its bounds — what lights and takes the press — the page's whole width, and where it
    /// follows the item before it with nothing but room between them, the room is halved between the two, so the
    /// rows of a list touch; what it shows stands where it would have.
    /// </summary>
    public void Row(Widget widget, float height)
    {
        if (widget is not ListRow row)
        {
            Place(widget, RowLeft, RowWidth, height);
            return;
        }
        row.Lead = ListLead;
        row.Over = row.Under = row.Below = 0;
        row.Open = false;
        var top = Y;
        // Nothing between: only room.
        if (item is not null && Y > itemEnd && body.Skip(itemCount).All(between => between.Width <= 0))
        {
            top = PopupPaint.Round((itemEnd + Y) / 2);
            Meet(item, top - itemEnd);
            row.Over = Y - top;
        }
        row.Bounds = new D2D_RECT_F(0, top, Width, Y + height);
        body.Add(row);
        Y += height;
        Ended(row);
    }

    /// <summary>
    /// <paramref name="row"/> opened in place onto what was placed under it since (a network onto its key, a Bluetooth device
    /// onto Connect and Remove; §605): one item with it, its band grown down to the cursor (<see cref="ListRow.Below"/>), so
    /// all of it lights under the pointer, behind what it offers too, and wears the chosen look and its pill. What it offers
    /// stands over its band and takes the pointer first.
    /// </summary>
    public void Opened(ListRow row)
    {
        row.Below = MathF.Max(0, Y - row.Bounds.bottom);
        row.Bounds = row.Bounds with { bottom = MathF.Max(row.Bounds.bottom, Y) };
        row.Open = true;
        Ended(row);
    }

    private void Ended(Widget widget)
    {
        item = widget;
        itemEnd = Y;
        itemCount = body.Count;
    }

    /// <summary>
    /// A line's list from its edges (LineItem, §436): its first row, with nothing over it but room — no title, or the
    /// way back, an item itself — starts at the body's top, and its last, with nothing under it but room, ends the
    /// body. Each keeps the band every row has, as <see cref="Row"/> halves the room between two rows: the last has
    /// half the room the page left under it, and the room over the first is cut to the half it has under it,
    /// everything moving up with it. The body's foot, where the last item ends it; null where something else does.
    /// </summary>
    private float? Edges()
    {
        var shown = body.Where(widget => widget.Width > 0).ToList();
        if (shown.Count == 0)
        {
            return null;
        }
        float? foot = null;
        if (item is not null && !shown.Any(widget => widget != item && widget.Bounds.bottom > itemEnd + 0.5f) && body.Skip(itemCount).All(after => after.Width <= 0))
        {
            var half = PopupPaint.Round((Y - itemEnd) / 2);
            if (item is ListRow last)
            {
                last.Under = half;
                last.Bounds = last.Bounds with { bottom = itemEnd + half };
            }
            foot = item.Bounds.bottom;
        }
        var first = shown.MinBy(widget => widget.Bounds.top)!;
        if (first is ListRow lead && lead.Bounds.top > 0 && (headerHeight == 0 || Back is not null))
        {
            var top = lead.Bounds.top;
            var by = top - lead.Under;
            foreach (var widget in body.Concat(over))
            {
                var at = widget.Bounds;
                widget.Bounds = at with { top = at.top - by, bottom = at.bottom - by };
            }
            Y -= by;
            foot -= by;
            // The row from the top.
            foreach (var widget in shown.Where(widget => widget.Bounds.top == top - by))
            {
                widget.Bounds = widget.Bounds with { top = 0 };
            }
            lead.Over = lead.Under;
        }
        return foot;
    }

    /// <summary><paramref name="widget"/>'s band grown <paramref name="by"/> px down into the room under it.</summary>
    private static void Meet(Widget widget, float by)
    {
        var at = widget.Bounds;
        widget.Bounds = at with { bottom = at.bottom + by };
        if (widget is ListRow row)
        {
            row.Under += by;
        }
    }

    /// <summary>A widget at the body's cursor, from <paramref name="x"/>; the cursor moves down.</summary>
    public void Place(Widget widget, float x, float width, float height)
    {
        widget.Bounds = new D2D_RECT_F(x, Y, x + width, Y + height);
        body.Add(widget);
        Y += height;
    }

    /// <summary>A widget in the body at <paramref name="bounds"/>, leaving the cursor.</summary>
    public void Put(Widget widget, D2D_RECT_F bounds)
    {
        widget.Bounds = bounds;
        body.Add(widget);
    }

    /// <summary>A section's heading ("Output device"), in line with the title.</summary>
    public void Heading(Label heading)
    {
        Y += Context.D(8);
        Place(heading, TextLeft, TextRight - TextLeft, Context.T(32));
    }

    public void Gap(float dip) => Y += Context.D(dip);

    /// <summary>
    /// A line's page with nothing to list (§401): <paramref name="widget"/> alone, one of the list's rows, with no
    /// title over it and no air under it — the kit's one Status, one row tall —; <paramref name="action"/>, the
    /// Status's one action (§404), a line's standard button on the row's middle against the trailing gutter, the
    /// row's words before it.
    /// </summary>
    public void Alone(Widget widget, Button? action = null)
    {
        var height = Context.T(40);
        if (action is not null)
        {
            var buttons = LineButton.Of(Context.Gfx, Context.Scale, Context.TextScale);
            buttons.Dress(action);
            var width = buttons.Width(Context.Gfx, action.Text);
            var right = Width - rowInset;
            Put(action, buttons.At(right - width, Y + PopupPaint.Round(height / 2), width));
            if (widget is ListRow row)
            {
                row.Lead = ListLead;
            }
            Place(widget, 0, MathF.Max(1, action.Bounds.left), height);
        }
        else
        {
            Row(widget, height);
        }
        alone = true;
    }

    private bool alone;

    /// <summary>
    /// Ends a list that began at <paramref name="top"/>, exactly <paramref name="dip"/> DIP tall (at
    /// a text size of 100 %, growing with the text as its rows do): the widget's flyout height
    /// (a flyout height, gone since 2026-10-11). The cursor goes under that room however little the list
    /// shows — what stands in it (a notice, an opened row's card) takes it rather than adding to it —
    /// and whatever the list has past it is a scroll away, so the flyout's height never changes as
    /// rows come and go. A <paramref name="dip"/> of null — Automatic — leaves the list as tall as
    /// its rows: the panel holds it to the most a flyout may be, and past that it scrolls.
    /// </summary>
    public void List(float top, int? dip)
    {
        // A line is as tall as what it shows: the folder scrolls, never a list inside it.
        if (dip is not { } set || Lined)
        {
            return;
        }
        var end = top + Context.T(Math.Max(1, set));
        if (Y > end)
        {
            // The panel as tall as the room and no more; the next row's top shows under the last,
            // where the body's foot would be, which says the list goes on.
            hidden += Y - end;
        }
        else
        {
            Y = end;
        }
    }

    /// <summary>
    /// The footer band, with its link 8 DIP in (its text then lines up with the title), and an
    /// <paramref name="action"/> at its right end where the page has one (Bluetooth's "Add device"):
    /// in the band, it stays in sight however long the list above it runs.
    /// </summary>
    public void Footer(Backdrop band, Button link, Button? action = null)
    {
        // A line's footer is the actions line under it.
        if (Lined)
        {
            return;
        }
        var height = Context.T(48);
        band.Bounds = new D2D_RECT_F(0, 0, Width, height);
        footer.Add(band);
        var buttonHeight = Context.T(32);
        var top = PopupPaint.Round((height - buttonHeight) / 2);
        var room = Width - 2 * RowLeft;
        if (action is not null)
        {
            var actionWidth = MathF.Min(action.NaturalWidth(Context), room / 2);
            action.Bounds = PopupPaint.Rect(RowRight - actionWidth, top, actionWidth, buttonHeight);
            footer.Add(action);
            room -= actionWidth + Context.D(8);
        }
        var linkWidth = MathF.Min(link.NaturalWidth(Context), room);
        link.Bounds = PopupPaint.Rect(Context.D(9), top, linkWidth, buttonHeight);
        footer.Add(link);
        footerHeight = height;
    }

    /// <summary>
    /// Buttons at the right end of a line at the body's cursor, right to left in the order given
    /// (Cancel first puts it rightmost, with the default one before it, as Windows' dialogs have them);
    /// the cursor moves under them.
    /// </summary>
    public void Buttons(float right, params Button[] buttons)
    {
        var height = Context.T(32);
        var gap = Context.D(8);
        var x = right;
        foreach (var button in buttons)
        {
            var width = MathF.Max(button.NaturalWidth(Context), Context.D(96));
            x -= width;
            Put(button, PopupPaint.Rect(x, Y, width, height));
            x -= gap;
        }
        Y += height;
    }

    /// <summary>
    /// Puts <paramref name="widget"/> over the page at <paramref name="bounds"/> (the panel's own px,
    /// the header's top-left being 0, 0): a dropdown's list, which hangs over what is under it rather
    /// than pushing it down. It takes no part in the layout, so nothing moves when it opens.
    /// </summary>
    public void Over(Widget widget, D2D_RECT_F bounds)
    {
        widget.Bounds = bounds;
        over.Add(widget);
    }

    /// <summary>Where a body widget sits in the panel's own px: what <see cref="Over"/> is measured in.</summary>
    public float PanelTop(float bodyY) => headerHeight + bodyY - Scrolled;

    /// <summary>
    /// The page as placed. The body is as tall as its rows, and nothing more: a page is as tall as
    /// what it shows. Pages whose rows open and close (a Wi-Fi network onto its key, a Bluetooth
    /// device onto its buttons) used to keep the room their tallest state needed whatever was open,
    /// so that closing one never resized the flyout under the pointer. The person asked for the
    /// layout to follow at once whatever the pointer is doing, so a page is as tall as it is now and
    /// the flyout follows it there and then (FlyoutHost.Fit), without the empty room under a closed
    /// list; a press stays on the control it went down on (QuickPanel).
    /// </summary>
    public PanelContent Finish()
    {
        // The body ends 8 DIP under its last row, as the quick settings' lists do; a line's under its last item at
        // the item's band's foot (Edges).
        var bodyHeight = body.Count == 0 ? 0 : MathF.Ceiling(Lined && Edges() is { } foot ? foot : Y + (alone ? 0 : Context.D(8)));
        // A list longer than the rows it shows: the panel stops there, and the rest scrolls.
        var most = hidden > 0 ? MathF.Min(MaxHeight, headerHeight + bodyHeight - hidden + footerHeight) : MaxHeight;
        if (over.Count > 0)
        {
            Fit(bodyHeight, most);
        }
        return new PanelContent(Width, header, headerHeight, body, bodyHeight, footer, footerHeight, most, over);
    }

    /// <summary>
    /// Keeps what hangs over the page inside it: the panel is as tall as its content up to its
    /// maximum, so a list dropped near the bottom is lifted until it fits rather than being cut off.
    /// </summary>
    private void Fit(float bodyHeight, float most)
    {
        // The height QuickPanel will settle on for this content.
        var room = MathF.Max(0, most - headerHeight - footerHeight);
        var viewport = MathF.Ceiling(MathF.Min(bodyHeight, MathF.Max(room, MathF.Min(bodyHeight, Context.D(48)))));
        var panel = headerHeight + viewport + footerHeight;
        var edge = Context.D(4);
        var top = float.MaxValue;
        var bottom = float.MinValue;
        foreach (var widget in over)
        {
            top = MathF.Min(top, widget.Bounds.top);
            bottom = MathF.Max(bottom, widget.Bounds.bottom);
        }
        var shift = 0f;
        if (bottom > panel - edge)
        {
            shift = -(bottom - (panel - edge));
        }
        if (top + shift < edge)
        {
            shift = edge - top;
        }
        if (MathF.Abs(shift) < 0.5f)
        {
            return;
        }
        foreach (var widget in over)
        {
            var at = widget.Bounds;
            widget.Bounds = new D2D_RECT_F(at.left, at.top + shift, at.right, at.bottom + shift);
        }
    }
}
