// What the islands share: surfaces kept per style and painted again in
// place, sizes that follow the bar's thickness, colours from the Look, and
// the few text and number helpers their cells draw with.
//
// Surfaces are transparent: an island draws only its glyphs, text, graphs,
// bars and pictures. The scene puts one card behind each whole island and
// pads it; a cell adds only its Edge at each end, half the room between two
// cells, so neighbours (and the hover highlight) have air.
//
// A cell is as long as its content. Numbers are drawn in tabular figures and
// in formats whose width depends on their shape alone ("88%", "8.8M",
// "3h 05m"), and a cell is measured for the shape it shows, from a cache, so
// its length changes only when the shape does.
//
// A class here has a method named System (the IIsland contract), so inside
// one, "System." names that method: namespaces come in through usings only.

using System.Collections.Concurrent;
using System.Globalization;
using System.Numerics;
using Karpo.Core;
using Karpo.Dock.Native;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using Windows.UI.Composition;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

internal abstract unsafe class CellIsland : IIsland
{
    /// <summary>The <see cref="Part"/> of a whole cell, as <see cref="Surface"/> gives it.</summary>
    protected const int WholePart = 0;

    // Per style, the surfaces handed out so far, by cell index.
    private readonly Dictionary<IslandStyle, CompositionDrawingSurface?[]> kept = [];

    // perf: what was kept before the last Forget. A scene built before it may
    // still show these, so they are closed one Forget later, unless the next
    // build asks for the same cell at the same size and takes one back: a
    // rebuild that changed nothing about the island then paints into the
    // surfaces it has instead of making new ones. Surfaces made anew on every
    // rebuild and closed only by the GC held native memory for hours.
    private readonly Dictionary<IslandStyle, CompositionDrawingSurface?[]> previous = [];

    // Blank surfaces handed out for cells that had gone, closed at the next Forget.
    private readonly List<CompositionDrawingSurface> blanks = [];

    // §359: the styles asked for since the dock began its rebuild (Rebuilding); those it asked nothing of
    // are set aside as it ends (Rebuilt) — a scene may still show them — and closed as the next one ends,
    // unless asked for again. And a cell's surface it outgrew, closed as the next rebuild ends.
    private readonly HashSet<IslandStyle> asked = [];
    private readonly Dictionary<IslandStyle, CompositionDrawingSurface?[]> aside = [];
    private readonly List<CompositionDrawingSurface> retired = [];
    private bool rebuilding;

    /// <summary>
    /// §635: made while the samples were shown (Samples.On: the add page's ghost, a look's preview): it shows them for good, not
    /// the person's own data, and is made again for that (Dock.SyncIslands, FolderIsland.Configure).
    /// </summary>
    internal bool Sampled { get; } = Samples.On;

    /// <summary>Tests only: the cells drawn or drawn again whole, over the island's life (§359: a rebuild that changed nothing draws none).</summary>
    internal int Paints { get; private set; }

    // Per style, the cell lengths the dock was last told, to notice when new
    // content needs other lengths and so another layout.
    private readonly Dictionary<IslandStyle, IslandCells> told = [];

    // One delegate for every paint, pointed at a cell through the fields
    // below, so a redraw allocates nothing.
    private readonly Paint paint;
    private int paintIndex;
    private IslandStyle paintStyle;
    private float paintWidth;
    private float paintHeight;
    private bool reported;
    private nint dock;
    private nuint token;
    private int wanted;

    protected CellIsland(string key, WidgetItem island, Gfx gfx)
    {
        Key = key;
        Info = island.Info
            ?? throw new ArgumentOutOfRangeException(nameof(island), island.Id, "names no widget this build knows.");
        Settings = island;
        Gfx = gfx;
        paint = PaintAimed;
        // Where it stands until the factory that made it says (Islands.Create, CreateLine): where its
        // record stands — a taskbar's entry, a list's line — and for a folder's widget that stands
        // nowhere yet, as the taskbar has it, or, for a widget the taskbar does not take, as a list
        // does. A widget made directly — in a test, a preview — keeps this.
        if (island is BarItem { Kind: var bar } && BarEntries.Find(bar) is { } entry)
        {
            StandOn(entry);
        }
        else if (island is CellItem { List: { } line })
        {
            StandIn(ListWidgets.Of(line));
        }
        else if (BarEntries.Find(Info.Id) is { } barForm)
        {
            StandOn(barForm);
        }
        else if (ListWidgets.Find(Info.Id) is { } listForm)
        {
            StandIn(listForm);
        }
    }

    public string Key { get; }

    public WidgetInfo Info { get; }

    /// <summary>
    /// What draws this widget where it stands — its taskbar entry's or its line's <c>Drawing</c> — which is what its size is looked up by (<see cref="WidgetShapes.For"/>).
    /// </summary>
    internal WidgetSource Drawing { get; private set; }

    /// <summary>
    /// Whether its card is the only drawing it has where it stands: a line of a list drawn as the
    /// widget's card (<see cref="ListLine.Card"/>). Such a widget is always its card, at any size,
    /// clipped to its box; any other falls back to the bar's own cells where a card has no room
    /// for its card drawing (<see cref="CardDrawn"/>). Never true on the taskbar.
    /// </summary>
    internal bool CardOnly { get; private set; }

    /// <summary>
    /// pins: what a press or a wheel turn on it may be before it is the surface's
    /// (<see cref="Karpo.Core.Interaction"/>), as its taskbar entry or line declares it: a
    /// slider's drag, a stepper's wheel.
    /// </summary>
    internal Interaction Interaction { get; private set; }

    /// <summary>The factory made it for a taskbar: <paramref name="entry"/>'s drawing and answers.</summary>
    internal void StandOn(BarEntry entry) =>
        (Drawing, CardOnly, Interaction) = (entry.Drawing, false, entry.Interaction);

    /// <summary>The factory made it as a line of a list folder: <paramref name="line"/>'s drawing and answers.</summary>
    internal void StandIn(ListWidget line) =>
        (Drawing, CardOnly, Interaction) = (line.Drawing, line.Line == ListLine.Card, line.Interaction);

    public abstract int IntervalMs { get; }

    protected Gfx Gfx { get; private set; }

    protected abstract int Count { get; }

    /// <summary>How many cells the island has now: what a folder's card shows of an island without a tile of its own.</summary>
    internal int CellCount => Count;

    /// <summary>Which part of a cell <see cref="PaintCell"/> draws now: <see cref="WholePart"/>, or an island's own layer.</summary>
    protected int Part { get; private set; }

    public void Attach(nint dock, nuint token)
    {
        // §658: a message of an earlier window that reused this handle and token never comes: none waits for this one.
        Islands.Taken(dock, token);
        // The token first: a post from another thread reads it once the dock is set.
        Volatile.Write(ref this.token, token);
        Volatile.Write(ref this.dock, dock);
        // Work that finished before the dock knew the island is announced now.
        if (Interlocked.Exchange(ref wanted, 0) == 1)
        {
            Post();
        }
    }

    public abstract void Configure(WidgetItem island, Config config);

    public abstract string? Tooltip(int index);

    public abstract IslandAction Click(int index);

    public virtual void Invoke(int index, uint id)
    {
    }

    public virtual void System(uint msg, WPARAM wParam, LPARAM lParam)
    {
    }

    /// <summary>Nothing moves in a plain island.</summary>
    public virtual void Visible(bool shown)
    {
    }

    /// <summary>
    /// §357: what of its own view of what it shows this line keeps as its folder closes — a month or a
    /// week stepped to, notifications expanded —, read before it is told it is out of sight
    /// (<see cref="Visible"/>), for its folder to open where it was left (FolderMemory); null for nothing.
    /// </summary>
    internal virtual object? ViewMemory() => null;

    /// <summary>§357: its view as <see cref="ViewMemory"/> kept it, its folder opened where it was left; after it is told it shows.</summary>
    internal virtual void RecallView(object memory)
    {
    }

    /// <summary>
    /// The pointer came onto one of its cells on a bar. Nothing for most: a widget whose state has
    /// no news of its own reads it again here (a monitor's brightness, BrightnessIsland).
    /// </summary>
    internal virtual void PointerCame()
    {
    }

    /// <summary>
    /// The widget's settings as the config has them: what a kind whose size is a pure function of
    /// them is measured from (<see cref="WidgetShapes"/>). Given at construction and kept in step
    /// by <see cref="Configure"/>, so there is no moment at which an island exists without the
    /// settings that decide how big it is.
    /// </summary>
    protected internal WidgetItem Settings { get; set; }

    /// <summary>
    /// What this widget still needs before it shows anything, where its "Set up in Settings" goes
    /// and the words that say so (SetupPrompt.cs); null while it has what it needs. The rule is
    /// Core's (<see cref="WidgetSetup.Needs"/>), read off the settings it was last given: a feed
    /// with no address, a stock with no symbol, the weather with no place, a drive widget with no
    /// drive, a pin with nothing to open, a heading with no words. The mail and the contacts, which
    /// need an account rather than a field, answer for themselves. §635: a widget showing the samples needs nothing: what it
    /// lacks — a feed, a symbol, a team — the samples give it.
    /// </summary>
    internal virtual SetupNeed? Unset => !Sampled && WidgetSetup.Needs(Settings) is { } missing ? SetupNeed.Of(Key, missing) : null;

    /// <summary>
    /// "Last updated 14:05 — couldn't refresh" while what the widget shows could not be refreshed
    /// (StaleMark.cs): the tooltip over the mark on its line. Null by default.
    /// </summary>
    internal virtual string? StaleTip => null;

    /// <summary>
    /// Why what this widget stands for is not on this PC — no battery, no display whose brightness
    /// can be set, no radio — as drawn for the display <paramref name="screen"/> (0: none in
    /// particular); null where it is. Such a widget draws its own layout with a stand-in reading,
    /// greyed (<see cref="Unavailable"/>), takes no pointer, wheel or click, and says this as its
    /// tooltip in a folder (docs\widgets.md §247; on the bar none, §367). Null by default.
    /// </summary>
    internal virtual string? UnavailableOn(nint screen) => null;

    /// <summary>
    /// list: whether the line has nothing to show now — now playing while nothing plays (§327), the
    /// line's <see cref="IFaceDrawing.NothingToShow"/>. False by default; a kind that says so has its
    /// <see cref="ListWidget.IdleKey"/>, and its folder is woken when it changes.
    /// </summary>
    internal virtual bool NothingToShow => false;

    /// <summary>
    /// list: whether the line leaves its folder at rest now: it has nothing to show and is set to hide
    /// while it has not (<see cref="CellItem.HidesIdle"/>). At rest it takes no room in its list
    /// (FolderFlyout.Build's <c>resting</c>); arranged it stands where it stands, greyed (<see cref="PaintOn"/>).
    /// </summary>
    internal bool HiddenAtRest => Settings is CellItem { HidesIdle: true } && NothingToShow;

    /// <summary>
    /// Whether a click on the widget on the bar at rest does anything, on the display
    /// <paramref name="screen"/>: not where this PC hasn't got its thing (<see cref="UnavailableOn"/>,
    /// §247). Only then does its press show — the icon giving a little, the pressed fill — and only
    /// then is the click asked of it (Dock.ClickDoes; the person, 2026-10-22: "if a folder does not
    /// have any contents to be viewed / opened it should not have an animation when i click it"; §287).
    /// </summary>
    internal virtual bool ClickDoes(nint screen) => UnavailableOn(screen) is null;

    /// <summary>"Battery isn't available on this PC", and why under it where there are words for that: what <see cref="UnavailableOn"/> says.</summary>
    internal static string NotOnThisPc(string name, string? why = null) =>
        why is { Length: > 0 } line ? $"{S.WidgetSwitchUnavailableTip(name)}\n{line}" : S.WidgetSwitchUnavailableTip(name);

    /// <summary>The glyph its "Set up in Settings" stands beside (SetupFace): its kind's, or none for one that leads with its words.</summary>
    protected virtual string? SetupGlyph => Info.Glyph;

    /// <summary>Where its "Set up in Settings" stands in <paramref name="box"/> (px), as <see cref="PaintOn"/> draws it; null while set up, or where it has no room.</summary>
    internal SetupFace.Plan? SetupPlan(D2D_RECT_F box, in TileStyle style)
    {
        using var typeface = Gfx.Wearing(style.Look.Typeface);
        return Unset is { } need ? SetupFace.Lay(Gfx, box, style, SetupGlyph, need.Short, need.Link.ToAccounts) : null;
    }

    /// <summary>
    /// What this widget is drawn in, where it is drawn in something that holds several
    /// (<see cref="IWidgetContainer"/>, docs\widgets.md §9): a folder sets it on the widgets it
    /// makes. <b>Null everywhere else</b>, and a widget must work with it null — on a bar, in a
    /// preview, in a test. Nothing but the container sets this.
    /// </summary>
    internal IWidgetContainer? Container { get; set; }

    /// <summary>
    /// What the folder's <paramref name="channel"/> holds, where this widget follows it
    /// (<see cref="Channels.Follows"/>: its kind does) and somebody has
    /// published to it; null otherwise — on a bar, in a preview, with nothing published — which the
    /// widget reads as the channel's default (today, for the day). It never needs to know why.
    /// </summary>
    protected internal T? Followed<T>(Channel<T> channel)
        where T : struct =>
        Container is { } container && Channels.Follows(Settings, channel) ? container.Read(channel) : null;

    /// <summary>
    /// Publishes <paramref name="value"/> to the folder's <paramref name="channel"/>. Nothing happens
    /// where there is no folder, and the folder is what draws again — a widget never asks to be redrawn.
    /// </summary>
    protected void Publish<T>(Channel<T> channel, T value)
        where T : struct => Container?.Publish(channel, value);

    /// <summary>Takes back what the folder's <paramref name="channel"/> holds; nothing where there is no folder.</summary>
    protected void Withdraw(Channel channel) => Container?.Withdraw(channel);

    /// <summary>
    /// Whether <paramref name="channel"/> is this widget's to read: it stands in a folder and
    /// follows it. A channel whose unset value means something (no app picked) is read only then,
    /// and the widget's own otherwise.
    /// </summary>
    protected internal bool Following(Channel channel) => Container is not null && Channels.Follows(Settings, channel);

    /// <summary>
    /// Cell <paramref name="index"/>'s length along the dock in px: its content
    /// as it is now, measured from cached templates, plus an
    /// <see cref="Fit.Edge"/> at each end. Called on every redraw, so it lays
    /// out no text it has not measured before.
    /// <para>
    /// A kind whose shape is a pure function of its settings does not implement this at all: its
    /// size is <see cref="WidgetShapes"/>' and never passes through the island, which is what
    /// stops it measuring the value it happens to be showing (docs\widgets.md §4, stage 3).
    /// </para>
    /// <para>
    /// <b>A component has no length here at all.</b> A widget whose card is its only drawing
    /// (<see cref="CardOnly"/>) is only ever a card of a folder or a line of a list, sized by the
    /// room the folder gives it, and there is no dock for it to have a length along. The refusal
    /// says which of the two it is, because "sized by WidgetShapes" sent somebody looking for a
    /// missing entry in that table when the truth was that the question does not apply.
    /// </para>
    /// </summary>
    protected virtual float Length(int index, in IslandStyle style) =>
        throw new NotSupportedException(CardOnly
            ? $"{Info.Id} has no length along a bar: it is a component, sized by the room its folder gives it."
            : $"{Info.Id} is sized by WidgetShapes, not by its island.");

    /// <summary>Reads new values; <paramref name="everything"/> also what rarely changes. True when the cell count changed.</summary>
    protected abstract bool Sample(bool everything);

    /// <summary>
    /// Paints <see cref="Part"/> of cell <paramref name="index"/> into a cleared
    /// <paramref name="width"/> × <paramref name="height"/> px surface.
    /// </summary>
    internal abstract void PaintCell(ID2D1DeviceContext* dc, int index, in IslandStyle style, float width, float height);

    // --- Surfaces ------------------------------------------------------------------
    //
    // One way in for every surface of a folder a widget is drawn on. PaintOn and TargetOn are not
    // virtual, and whether a kind is drawn as its card or as its bar cells is theirs alone to ask:
    // the promise of docs\widgets.md §2, kept by construction rather than by two code paths
    // agreeing.

    /// <summary>
    /// Whether the widget is drawn as its kind's card in <paramref name="box"/> (px), or as the
    /// bar's own cells over the whole of it.
    ///
    /// <para>
    /// The card drawing is used only where the card has the room a card drawing needs
    /// (<see cref="TilePaint.CardFits"/>) — a folder's cell is 36 DIP now, about the size of a bar
    /// cell, and at that size the bar's drawing is the one that reads. One question, asked here and
    /// nowhere else, so the painting, the hit testing and the pointer cannot disagree about which
    /// drawing is on the screen: two code paths each correct about its own half is the defect
    /// docs\widgets.md §2 was written about.
    /// </para>
    /// <para>
    /// <b>A component is always its card.</b> The room question chooses between two drawings, and
    /// a widget whose card is its only drawing where it stands (<see cref="CardOnly"/>) has only
    /// one — so there is nothing to choose, and asking anyway answered "the drawing it does not
    /// have". What came back instead was the kind's glyph in an empty box, which is not a smaller
    /// day switcher but a picture of nothing. Its own card clipped to the room says more at every
    /// size, and a component has to survive being given any height in any case, because that is
    /// what a set height or a range hands it.
    /// </para>
    /// <para>
    /// <b>So is a widget's line in a list, where it has one</b> (<see cref="ListLine.Card"/>, which
    /// the list's factory makes <see cref="CardOnly"/>).
    /// A list line is the panel's width and as tall as its widget asks (<see cref="IListContent"/>),
    /// and its drawing there is the widget's list form: its time, its agenda, its graph. The bar's
    /// cells are no fallback on a line — a list's row that shows nothing of the widget is what a
    /// list line may not be — so a line given less than its content is its drawing cut at the
    /// edge, as a component's card is.
    /// </para>
    /// <para>
    /// It looks at where the widget stands, the style and the card's rectangle only. The first is
    /// fixed when the widget is made and the other two come from the folder's plan, which is a
    /// function of the settings, so no value a widget is showing can change the treatment it gets,
    /// and nothing here can move anything.
    /// </para>
    /// </summary>
    private bool CardDrawn(D2D_RECT_F box, in TileStyle style) =>
        CardOnly || TilePaint.CardFits(Gfx, box, style);

    /// <summary>
    /// Paints the widget on <paramref name="surface"/>, filling <paramref name="box"/> (px): its
    /// card, or its bar cells where the card has no room for a card drawing.
    /// </summary>
    internal void PaintOn(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        using var typeface = Gfx.Wearing(style.Look.Typeface);
        // A widget that is not set up is its "Set up in Settings" wherever that fits, whatever its
        // kind draws otherwise (SetupFace); where it does not, its own drawing says it has nothing.
        if (Unset is { } need && SetupFace.Paint(null, Gfx, box, style, SetupGlyph, need.Short, need.Link.ToAccounts))
        {
            SetupFace.Paint(dc, Gfx, box, style, SetupGlyph, need.Short, need.Link.ToAccounts);
            return;
        }
        // Not on this PC (§247), or there only because the folder is arranged (§327): greyed, as a face is.
        if (UnavailableOn(style.Screen) is not null || HiddenAtRest)
        {
            var greyed = style;
            Unavailable.Paint(dc, style.Look, drawn => PaintDrawn(drawn, box, greyed, surface));
            return;
        }
        PaintDrawn(dc, box, style, surface);
    }

    /// <summary>The widget's own drawing in <paramref name="box"/>: its card, or its bar cells where the card has no room for one (<see cref="PaintOn"/>).</summary>
    private void PaintDrawn(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        if (CardDrawn(box, style))
        {
            // **A line of a list is clipped to its box.** Its drawing is its only one there, used
            // at whatever height the line is set to, and being cut at the edge is the design, not a
            // failure: that is what "there is more here than fits" looks like.
            var clip = box;
            dc->PushAxisAlignedClip(&clip, D2D1_ANTIALIAS_MODE.D2D1_ANTIALIAS_MODE_ALIASED);
            try
            {
                // A line of items given less than they take is laid out whole and scrolled
                // (ListScroll.cs): the items through the window under the head, the head still over them.
                var laid = Scrolled(box, style, surface);
                if (!laid.Scrolls)
                {
                    PaintCard(dc, laid.Box, style, surface);
                    return;
                }
                // The head and the foot first and the items last: they never overlap, and a widget that keeps
                // the box it was last drawn in (a text box's place) keeps the items'.
                var window = laid.Window;
                if (laid.Head > 0)
                {
                    var head = box with { bottom = window.top };
                    dc->PushAxisAlignedClip(&head, D2D1_ANTIALIAS_MODE.D2D1_ANTIALIAS_MODE_ALIASED);
                    try
                    {
                        showing = head;
                        PaintCard(dc, laid.Still, style, surface);
                    }
                    finally
                    {
                        showing = null;
                        dc->PopAxisAlignedClip();
                    }
                }
                if (laid.Foot > 0)
                {
                    // The foot at the line's bottom, where the content ends scrolled all the way.
                    var foot = laid.FootBand;
                    dc->PushAxisAlignedClip(&foot, D2D1_ANTIALIAS_MODE.D2D1_ANTIALIAS_MODE_ALIASED);
                    try
                    {
                        showing = foot;
                        PaintCard(dc, laid.End, style, surface);
                    }
                    finally
                    {
                        showing = null;
                        dc->PopAxisAlignedClip();
                    }
                }
                if (laid.Aside > 0)
                {
                    // §655: the side that stays (a pane beside the items), where the content is not scrolled.
                    var aside = laid.AsideBand;
                    dc->PushAxisAlignedClip(&aside, D2D1_ANTIALIAS_MODE.D2D1_ANTIALIAS_MODE_ALIASED);
                    try
                    {
                        showing = aside;
                        PaintCard(dc, laid.Still, style, surface);
                    }
                    finally
                    {
                        showing = null;
                        dc->PopAxisAlignedClip();
                    }
                }
                dc->PushAxisAlignedClip(&window, D2D1_ANTIALIAS_MODE.D2D1_ANTIALIAS_MODE_ALIASED);
                try
                {
                    showing = window;
                    PaintCard(dc, laid.Moved, style, surface);
                }
                finally
                {
                    showing = null;
                    dc->PopAxisAlignedClip();
                }
                // Windows 11's overlay scrollbar: thin, over the content, while the pointer is on the line.
                if (listPointer)
                {
                    var thumb = laid.Thumb(style);
                    PopupPaint.Fill(dc, thumb, (thumb.right - thumb.left) / 2, FlyoutTheme.Ink(style.Theme.ScrollIndicator));
                }
            }
            finally
            {
                dc->PopAxisAlignedClip();
            }
            return;
        }
        TilePaint.Cells(dc, this, box, style, padded: false);
    }

    /// <summary>
    /// Which of <see cref="Click"/>'s indexes a click at <paramref name="point"/> (px, in
    /// <paramref name="box"/>'s space) stands for on <paramref name="surface"/>: 0 for the widget
    /// as a whole, another cell's index for a button of its own.
    /// </summary>
    internal int TargetOn(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        using var typeface = Gfx.Wearing(style.Look.Typeface);
        // One that is not set up is one target, all of it its "Set up in Settings"; one that is not
        // on this PC is one too, which nothing answers.
        return Unset is not null || UnavailableOn(style.Screen) is not null ? 0
        : CardDrawn(box, style) ? CardTarget(point, Scrolled(box, style, surface).At(point), style, surface)
        : TilePaint.CellAt(this, point, box, style, padded: false);
    }

    /// <summary>
    /// A press at <paramref name="point"/> (px, in <paramref name="box"/>'s space) on
    /// <paramref name="surface"/>: true when the widget took the pointer, so the moves and the
    /// release are its (<see cref="DragOn"/>, <see cref="ReleaseOn"/>) and the press is not a
    /// click. A widget that owns the pointer — the volume slider — has to be asked on every
    /// surface it is drawn on, not only on the bar: the folder asked for a click and nothing else,
    /// so a slider in a card could be clicked but never dragged. Where the card is drawn as the
    /// bar's cells, the widget is handed the cell the point is in, in that cell's own px.
    /// </summary>
    internal bool PressOn(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        using var typeface = Gfx.Wearing(style.Look.Typeface);
        // Not set up, it takes no pointer: a press on it is the click that opens its settings. Not on
        // this PC, it takes none either.
        if (Unset is not null || UnavailableOn(style.Screen) is not null)
        {
            return false;
        }
        if (CardDrawn(box, style))
        {
            return CardPress(point, Scrolled(box, style, surface).At(point), style, surface);
        }
        var cell = TilePaint.CellUnder(this, point, box, style, padded: false);
        // pins: where it stands declares that a drag inside it may be its own (Interaction.Drag);
        // the widget still says where. `is ISliderIsland` alone was an answer given by omission.
        return Interaction.HasFlag(Interaction.Drag) && this is ISliderIsland slider
            && cell.Size.X > 0 && cell.Size.Y > 0
            && slider.Press(cell.Index, cell.Point, cell.Size, cell.Style);
    }

    /// <summary>The pointer moved while the widget holds it (see <see cref="PressOn"/>).</summary>
    internal void DragOn(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        using var typeface = Gfx.Wearing(style.Look.Typeface);
        if (CardDrawn(box, style))
        {
            CardDrag(point, Scrolled(box, style, surface).At(point), style, surface);
            return;
        }
        var cell = TilePaint.CellUnder(this, point, box, style, padded: false);
        if (Interaction.HasFlag(Interaction.Drag) && this is ISliderIsland slider && cell.Size.X > 0 && cell.Size.Y > 0)
        {
            slider.Drag(cell.Index, cell.Point, cell.Size, cell.Style);
        }
    }

    /// <summary>
    /// The widget lets the pointer go: the button went up, or the capture went elsewhere. Both
    /// halves are told — whichever took the pointer may have lost the card it was drawn in by the
    /// time it is let go, and a slider left holding the pointer is worse than one told twice.
    /// </summary>
    internal void ReleaseOn(WidgetSurface surface)
    {
        // pins: not gated on Interaction.Drag, and deliberately. Letting go is the half that
        // must happen whatever the answer is now — a widget that took the pointer under one
        // answer and is released under another would hold it for ever.
        (this as ISliderIsland)?.Release();
        CardRelease();
    }

    /// <summary>
    /// The wheel turned <paramref name="notches"/> over the widget at <paramref name="point"/>:
    /// true when the widget took it, and then the surface it is drawn on does not scroll. A
    /// stepper inside a scrolling folder is unusable otherwise - the wheel over its value would
    /// move the panel instead of the number. The bar has nothing to scroll, so nothing to lose.
    /// </summary>
    internal bool WheelOn(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface, int notches)
    {
        using var typeface = Gfx.Wearing(style.Look.Typeface);
        return Unset is null && UnavailableOn(style.Screen) is null && CardDrawn(box, style) && CardWheel(point, Scrolled(box, style, surface).At(point), style, surface, notches);
    }

    /// <summary>
    /// §424: the wheel as it turned, <paramref name="delta"/> in WHEEL_DELTA's units — a touchpad's fine steps and
    /// not only whole notches — over a line that moves by what it says (the month's weeks); true when it took it.
    /// </summary>
    internal bool WheelDeltaOn(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface, int delta)
    {
        using var typeface = Gfx.Wearing(style.Look.Typeface);
        return DrawsCard(box, style) && CardWheelDelta(point, box, style, surface, delta);
    }

    /// <summary>§424: whether the widget draws its own card in <paramref name="box"/>: set up, on this PC, and room for its card.</summary>
    internal bool DrawsCard(D2D_RECT_F box, in TileStyle style) =>
        Unset is null && UnavailableOn(style.Screen) is null && !HiddenAtRest && CardDrawn(box, style);

    /// <summary>
    /// Where the pointer rests over the widget, in <paramref name="box"/>'s space, or null when it
    /// has left: the widget lights the part of itself under it. Called as the pointer moves, so a
    /// widget that draws nothing for hover can ignore it, which is the default.
    /// </summary>
    internal void HoverOn(Vector2? point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        using var typeface = Gfx.Wearing(style.Look.Typeface);
        // A card too small to be drawn as a card has no parts to light, and the pointer leaving is
        // told either way, so a part lit before the card shrank is put out.
        var drawn = Unset is null && UnavailableOn(style.Screen) is null && CardDrawn(box, style);
        listPointer = drawn && point is not null;
        CardHover(drawn ? point : null, point is { } at ? Scrolled(box, style, surface).At(at) : box, style, surface);
    }

    /// <summary>
    /// A key pressed while the widget's card has the focus: true when the widget took it, and then
    /// the surface it is drawn on does not act on it. A slider's arrows have to change its level
    /// rather than move the focus to the next card, which is the keyboard's half of the same
    /// question the pointer asks in <see cref="PressOn"/>.
    /// </summary>
    internal bool KeyOn(int key, WidgetSurface surface) => CardKey(key, surface);

    /// <summary>
    /// keys: the line walks its own interior with the keys (<see cref="CardKey"/>) — a calendar's days, a
    /// list with its own editor, a page of controls — and is one stop of the folder, its keys its own first.
    /// False, the default: the folder walks the targets its click reaches (LineFocus).
    /// </summary>
    protected internal virtual bool WalksItsOwnKeys => false;

    /// <summary>
    /// keys: the target of this line the folder's keys are on (FolderFlyout.Keys.cs), 0 for none: what a line whose
    /// menu is about one of its items (a process) acts on where the pointer is on none.
    /// </summary>
    internal int KeyedPart { get; set; }

    /// <summary>keys: whether the line draws the part the folder's keys are on lit (<see cref="KeyedPart"/>; a clipboard's row with its buttons), and is drawn again as they move.</summary>
    protected internal virtual bool LightsKeyedPart => false;

    /// <summary>
    /// keys: for a line that walks its own keys (<see cref="WalksItsOwnKeys"/>), the target of its click its keys are
    /// on, 0 for none — what the sweep checks they reach (KeyboardSweepTests); null where the line does not say.
    /// </summary>
    protected internal virtual int? OwnKeyedTarget => null;

    /// <summary>
    /// keys: for a line that walks its own keys and lays its targets out as one list (<see cref="ILineTargets"/>),
    /// whether <paramref name="target"/> is one its own keys reach (a calendar's day) rather than one the folder's
    /// keys stop at beside them (its arrows, a Join). Every one by default: the line is one stop (LineFocus).
    /// </summary>
    protected internal virtual bool OwnKeysReach(int target) => true;

    /// <summary>
    /// keys: whether the line rings what its own keys are on now (a day, a row); the folder then draws no ring round
    /// it, so one ring shows (§471). None by default.
    /// </summary>
    protected internal virtual bool RingsOwnKeys => false;

    /// <summary>
    /// keys: the folder's keys came onto the line's own (<paramref name="on"/>) or left them, for a stop beside them
    /// or another line: a line that rings what its keys are on rings it as they come where it has something to
    /// ring, and puts its ring out as they go (§471). Nothing by default.
    /// </summary>
    protected internal virtual void OwnKeys(bool on)
    {
    }

    /// <summary>
    /// keys: a slider's target <paramref name="target"/> stepped <paramref name="steps"/> on <paramref name="screen"/>'s
    /// display — Left and Right one, Page Up and Down ten, Home and End to its ends (FolderFlyout.Keys.cs); true where
    /// the target is a slider and took it. None by default.
    /// </summary>
    protected internal virtual bool CardStep(int target, int steps, nint screen) => false;

    /// <summary>
    /// The tooltip of the part of the widget at <paramref name="point"/> and where that part is, in
    /// <paramref name="box"/>'s space; null where it has none, which is every widget but those that
    /// name an icon-only part (FolderFlyout.Tips.cs).
    /// </summary>
    internal (string Text, D2D_RECT_F Part)? TipOn(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        using var typeface = Gfx.Wearing(style.Look.Typeface);
        // One not on this PC says so over the whole of it.
        return UnavailableOn(style.Screen) is { } why ? (why, box)
        : Unset is null && CardDrawn(box, style) ? CardTip(point, Scrolled(box, style, surface).At(point), style, surface) : null;
    }

    // --- Scrolling inside a line (ListScroll.cs) ----------------------------------------

    // How far the line's items are scrolled, in px, and whether the pointer is over the line, which
    // shows the overlay scrollbar. Held to what there is to scroll wherever it is read (Scrolled).
    private float listScroll;
    private bool listPointer;

    /// <summary>Tests only: whether the surface it is drawn on last told it the pointer is on it (<see cref="HoverOn"/>).</summary>
    internal bool Pointed => listPointer;

    // While a scrolled line is drawn: the part of its box that shows (px), so its items out of
    // sight are not drawn at all (Hidden).
    private D2D_RECT_F? showing;

    /// <summary>
    /// Whether a band from <paramref name="top"/> to <paramref name="bottom"/> (px, in the box the card
    /// is drawn in) is wholly out of sight: only while a scrolled line is drawn, so a line of a hundred
    /// items draws the few that show. False everywhere else, a layout without a device included.
    /// </summary>
    protected bool Hidden(float top, float bottom) =>
        showing is { } shows && (bottom <= shows.top || top >= shows.bottom);

    /// <summary>
    /// How far the line's items are scrolled, in px (<see cref="IListScroll"/>): set by the folder as
    /// the wheel turns, and by a test or a picture directly. Held to what there is to scroll where it
    /// is read, so content that shrank is never scrolled past its end.
    /// </summary>
    internal float ListScroll
    {
        get => listScroll;
        set => listScroll = MathF.Max(0, value);
    }

    /// <summary>
    /// Where the line stands in <paramref name="box"/> (px) on <paramref name="surface"/>: laid out
    /// whole and scrolled where it is a line of items given less than they take, else as it was
    /// given. The one question the drawing and every hit test ask, so they cannot disagree.
    /// </summary>
    internal ListScrolled Scrolled(D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        using var typeface = Gfx.Wearing(style.Look.Typeface);
        var height = box.bottom - box.top;
        // §432, §442: a line of one form or a few is given its form's whole cells, and draws and is hit in the form
        // alone, in their middle: what is left of its last cell, less than one, shared over and under it as the
        // form's own padding — never a band of air at its foot, nor its form drawn a tier taller. A box taller than
        // its cells (a picture's, a test's) has the cells at its top.
        if (surface == WidgetSurface.List && Unset is null && Formed(box, style) is { } formed && formed < height - 0.5f)
        {
            var top = box.top + Over(MathF.Min(height, LineRows.Whole(formed, style)), formed);
            var form = box with { top = top, bottom = top + formed };
            return new ListScrolled(form, formed, 0, 0);
        }
        if (surface != WidgetSurface.List || Unset is not null || this is not IListScroll scroller || !CardDrawn(box, style))
        {
            return new ListScrolled(box, height, 0, 0);
        }
        var width = box.right - box.left;
        var content = scroller.ScrollHeight(width, height, style);
        if (!(content > height + 0.5f))
        {
            return new ListScrolled(box, height, 0, 0);
        }
        // The head keeps a little air under it, so an item passing under it is not cut against its words.
        var head = scroller.ScrollHead(width, content, style);
        head = Math.Clamp(head > 0 ? head + style.Px(2) : 0, 0, height);
        var foot = Math.Clamp(scroller.ScrollFoot(width, content, style), 0, height - head);
        var aside = Math.Clamp(scroller.ScrollAside(width, style), 0, width);
        var laid = new ListScrolled(box, content, head, 0, foot, aside);
        laid = laid with { Offset = Math.Clamp(listScroll, 0, laid.Most) };
        // A row just opened or chosen by the keys is brought into sight (contacts).
        if (scroller.ScrollReveal(width, style) is { } band)
        {
            listScroll = laid.Revealing(band.Top, band.Bottom);
            laid = laid with { Offset = listScroll };
        }
        return laid;
    }

    /// <summary>§432: how far a form <paramref name="formed"/> px tall stands under the top of the <paramref name="room"/> px it is given: half of what is left.</summary>
    private static float Over(float room, float formed) => PopupPaint.Round((room - formed) / 2);

    /// <summary>
    /// §611: the whole line a card drawn in <paramref name="form"/> stands in, px. A sized line draws in its form alone, in
    /// the middle of its whole cells (<see cref="Scrolled"/>): those cells, the room over and under the form given back;
    /// any other line's box is its line. What an item's band reaches — the line's edges, top to foot, no padding (the
    /// person, 2026-11-09: "it should span to the top and bottom and left and right with no padding there") — while what
    /// it shows keeps to the form; the line's clip keeps what lies past a box a test gave shorter than its cells.
    /// </summary>
    protected D2D_RECT_F LineAround(D2D_RECT_F form, in TileStyle style)
    {
        if (Settings is not CellItem { List: { } kind } || !kind.Sized())
        {
            return form;
        }
        var formed = form.bottom - form.top;
        var whole = LineRows.Whole(formed, style);
        var top = form.top - Over(whole, formed);
        return form with { top = top, bottom = top + whole };
    }

    /// <summary>
    /// §432: what the open folder last measured of this line (FolderFlyout.Natural): a sized line's form, in the
    /// middle of the whole cells it is given. 0 before anything measured it.
    /// </summary>
    internal float FormMeasured { get; set; }

    /// <summary>
    /// §432: the tier a sized folder line draws whatever its height — one row where its size is Compact, else
    /// <paramref name="full"/> — for a kind whose tiers are its sizes; null for a line that is not sized, which
    /// draws the fullest tier its height holds.
    /// </summary>
    private protected ListTier? SizedTier(ListTier full) =>
        Settings is CellItem { List: { } kind } && kind.Sized() ? LineSizes.Of(Settings) == LineSize.Compact ? ListTier.One : full : null;

    /// <summary>
    /// §432: how tall a line of one form or a few draws its form in <paramref name="box"/>, px — what the
    /// folder measured of it (<see cref="FormMeasured"/>), else what it says now (IListContent) — or null for a line
    /// whose content grows, which draws in the whole of its box.
    /// </summary>
    private float? Formed(D2D_RECT_F box, in TileStyle style)
    {
        if (Settings is not CellItem { List: { } kind } || !kind.Sized() || this is not IListContent content)
        {
            return null;
        }
        var formed = FormMeasured > 0 ? FormMeasured : content.ListHeight(box.right - box.left, style);
        return formed > 0 ? formed : null;
    }

    /// <summary>The tooltip of the kind's own card's part at the point, and where it is; none by default.</summary>
    protected internal virtual (string Text, D2D_RECT_F Part)? CardTip(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface) => null;

    /// <summary>A key on the kind's own card; true when it takes it. The default takes none.</summary>
    protected internal virtual bool CardKey(int key, WidgetSurface surface) => false;

    /// <summary>
    /// clipboard: a key let go while the widget's card has the focus: true where the card took its
    /// press to act on its release, and the surface then clicks it (<see cref="IIsland.Click"/> with
    /// no part: what the keys are on).
    /// </summary>
    internal bool KeyUpOn(int key, WidgetSurface surface) => Unset is null && CardKeyUp(key, surface);

    /// <summary>A key let go on the kind's own card; true where it acts now. The default never does.</summary>
    protected internal virtual bool CardKeyUp(int key, WidgetSurface surface) => false;

    /// <summary>
    /// notes: whether the card has a text box with the keys — a task being typed, a note being edited
    /// (InlineEditor). While it has, the folder hands it every key first, Escape and Tab too, and
    /// every character typed (<see cref="CharOn"/>); a press anywhere else in the folder ends it
    /// (<see cref="StopTypingOn"/>), as a click away from a text box does.
    /// </summary>
    internal bool TypingOn => Unset is null && CardTyping;

    /// <summary>A character typed (WM_CHAR) while the card has the keys or the focus; true when it took it.</summary>
    internal bool CharOn(char c, WidgetSurface surface) => Unset is null && CardChar(c, surface);

    /// <summary>Somewhere else was pressed, or the folder is closing: the card's text box keeps what was typed and lets the keys go.</summary>
    internal void StopTypingOn() => CardStopTyping();

    /// <summary>notes: the kind's own text box has the keys. None by default.</summary>
    protected internal virtual bool CardTyping => false;

    /// <summary>notes: a character typed on the kind's own card; true when it takes it. The default takes none.</summary>
    protected internal virtual bool CardChar(char c, WidgetSurface surface) => false;

    /// <summary>notes: the kind's own text box lets the keys go, keeping what was typed.</summary>
    protected internal virtual void CardStopTyping()
    {
    }

    /// <summary>
    /// notes: where the caret of the text box with the keys was last drawn, in its box's space; the
    /// folder puts the IME's composition window there. Null while none has the keys.
    /// </summary>
    internal D2D_RECT_F? CaretOn => Unset is null && CardTyping ? CardCaret : null;

    /// <summary>notes: the kind's own caret, as last drawn. None by default.</summary>
    protected internal virtual D2D_RECT_F? CardCaret => null;

    /// <summary>notes: whether the pointer at <paramref name="point"/> is over text to type in, and shows the text cursor.</summary>
    internal bool TextOn(Vector2 point, D2D_RECT_F box, in TileStyle style)
    {
        using var typeface = Gfx.Wearing(style.Look.Typeface);
        return Unset is null && CardDrawn(box, style) && CardText(point, box, style);
    }

    /// <summary>notes: the kind's own text under the pointer. None by default.</summary>
    protected internal virtual bool CardText(Vector2 point, D2D_RECT_F box, in TileStyle style) => false;

    /// <summary>desktops: whether the pointer at <paramref name="point"/> is over a handle that drags a row, and shows the move cursor.</summary>
    internal bool GripOn(Vector2 point, D2D_RECT_F box, in TileStyle style)
    {
        using var typeface = Gfx.Wearing(style.Look.Typeface);
        return Unset is null && CardDrawn(box, style) && CardGrip(point, box, style);
    }

    /// <summary>desktops: the kind's own drag handle under the pointer. None by default.</summary>
    protected internal virtual bool CardGrip(Vector2 point, D2D_RECT_F box, in TileStyle style) => false;

    /// <summary>desktops: whether the pointer it holds is dragging a row by its handle: the move cursor wherever it goes.</summary>
    internal virtual bool Gripping => false;

    /// <summary>
    /// desktops: Escape while the widget holds the pointer: true where it took it — a row being
    /// dragged put back — and then the folder stays open.
    /// </summary>
    internal bool EscapeOn() => CardEscape();

    /// <summary>desktops: the kind's own Escape while it holds the pointer. None by default.</summary>
    protected internal virtual bool CardEscape() => false;

    /// <summary>
    /// desktops: whether something in the card is moving by itself — a row sliding into its place —
    /// so the folder draws it on its quick tick until it says it has stopped (FolderFlyout.ArmQuick).
    /// </summary>
    internal virtual bool Animating => false;

    /// <summary>
    /// notes: what the card shows changed by the person's own hand — a task ticked, a letter typed —
    /// and the container it is drawn in draws it again now, laid out again where its height changed.
    /// Nothing where it stands in none.
    /// </summary>
    protected void Redrawn() => Container?.Redraw();

    // §489: which way the line's content was last swapped in place by the person (TakeSwap), and whether it was ever drawn.
    private int swapped;
    private bool swapDrawn;

    /// <summary>
    /// §489: what the line shows was swapped in place by the person — a subfolder gone into (+1) or back out (−1), another
    /// view of the same line: the folder drawing it brings it in by the one motion's swap, from that way. Nothing before the
    /// line was first drawn (its first content is no swap).
    /// </summary>
    protected void Swapped(int way)
    {
        if (swapDrawn)
        {
            swapped = Math.Sign(way);
        }
    }

    /// <summary>§489: the way the line's content was swapped since it was last drawn (0: none), told once, as it is drawn.</summary>
    internal int TakeSwap()
    {
        swapDrawn = true;
        var way = swapped;
        swapped = 0;
        return way;
    }

    /// <summary>The wheel over the kind's own card; true when it takes it. The default takes none.</summary>
    protected internal virtual bool CardWheel(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface, int notches) => false;

    /// <summary>§424: the wheel as it turned, in WHEEL_DELTA's units, over the kind's own card; true when it takes it. None by default.</summary>
    protected internal virtual bool CardWheelDelta(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface, int delta) => false;

    /// <summary>Where the pointer is over the kind's own card, or null when it has left.</summary>
    protected internal virtual void CardHover(Vector2? point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
    }

    /// <summary>
    /// A press on the kind's own card: true when it takes the pointer, as the volume slider does.
    /// The default takes nothing, so a card of any other kind is the click it always was.
    /// </summary>
    protected internal virtual bool CardPress(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        CardDown(point, box, style, surface);
        return false;
    }

    /// <summary>
    /// folders: a press on the kind's own card that it does not take, which stays the folder's click:
    /// told so a row can light pressed until that click (RowPress). Taking nothing, it is no drag.
    /// </summary>
    protected internal virtual void CardDown(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
    }

    /// <summary>The pointer moved while the card holds it.</summary>
    protected internal virtual void CardDrag(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
    }

    /// <summary>The card lets the pointer go.</summary>
    protected internal virtual void CardRelease()
    {
    }

    /// <summary>
    /// The kind's own drawing as a line of a list folder or a pane of a flyout, at any size it is
    /// given (<see cref="TileStyle.Shape"/>). The default is the bar's cells
    /// with room at their edges, which is what a kind that has nothing else to show has always
    /// drawn there. Overriding this is the kind's own business; it is one method, not a second
    /// class, and it is never reached for the bar.
    /// </summary>
    protected internal virtual void PaintCard(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, WidgetSurface surface) =>
        TilePaint.Cells(dc, this, box, style, padded: true);

    /// <summary>
    /// folders: the widget is made of rows that light under the pointer themselves — a folder's
    /// items, the devices plugged in, the notifications, the sound devices and apps — so the card or
    /// the line as a whole does not light as well: the folder leaves it as it is at rest, under the
    /// pointer and pressed, and only the row under the pointer shows what a press would do. Two lit
    /// at once, the line and a row on it, read as two things to press where there is one. A click
    /// beside the rows still does what the widget's click does. The default lights the whole line where
    /// its click acts and nothing else does (FolderFlyout.LightsWhole), which is what a widget that is
    /// one thing to press wants.
    /// </summary>
    protected internal virtual bool LightsItsRows => false;

    /// <summary>
    /// list: what its line draws runs to the line's very edges, under a folder's rim, as it did
    /// before the rim lay outside the material: a graph. Everything else, the actions line's footer
    /// band too, stops inside the rim (FolderFlyout.CardClip; docs\widgets.md §9, "One gutter").
    /// </summary>
    protected internal virtual bool UnderRim => false;

    /// <summary>
    /// What a click on that card means: 0, the widget's own click, unless the kind says where its
    /// card has buttons of its own.
    /// <para>
    /// <b>Never the bar's cells.</b> This used to answer with the cell under the point, which is the
    /// bar's drawing's question: a kind that drew its own card and said nothing of its targets had
    /// its clicks measured against cells nobody drew, and a component, which has no cells along a bar
    /// at all, threw there (<see cref="Length"/>) — the dock caught it, and the sports line, the air
    /// quality's, the tides' and a network drive's did nothing when clicked (§312). Eleven kinds had
    /// written <c>=> 0</c> by hand to get away from it. A card is one target where it says nothing,
    /// which is what every one of them meant; the bar's cells are asked only where the bar's drawing
    /// is what is on the screen (<see cref="TargetOn"/>).
    /// </para>
    /// </summary>
    protected internal virtual int CardTarget(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface) => 0;

    /// <summary>
    /// keys: where the stop of <paramref name="target"/> stands in a line laid out in <paramref name="box"/>, where its hit
    /// test answers it over more than the control it is — the volume's line's room, which is its ">"'s (§605): the ring goes
    /// round the control, so Enter there and a click there are one. Null: the band the hit test answers it over (LineFocus).
    /// </summary>
    protected internal virtual D2D_RECT_F? StopBox(int target, D2D_RECT_F box, in TileStyle style) => null;

    /// <summary>
    /// edit: what this widget stands on is being arranged: the folder it is a line of (FolderFlyout
    /// sets it as it opens and as it is arranged), the bar it is on (the dock sets it on every
    /// rebuild, which edit mode's start and end are). A widget whose content edit mode shows
    /// differently reads it — the sound devices, every one listed and the hidden ones greyed
    /// (docs\widgets.md §159); a folder's faces with nothing to show, on the bar only then (§290).
    /// </summary>
    internal bool Arranged
    {
        get => arranged;
        set
        {
            if (value == arranged)
            {
                return;
            }
            arranged = value;
            // §359: what it shows may change with it, which no rebuild draws again any more.
            Forget();
        }
    }

    private bool arranged;

    /// <summary>
    /// edit: what of the card's own a click at <paramref name="point"/> edits while the folder is
    /// arranged, one up; 0, the default, where it edits nothing of the card's and the click selects
    /// the widget, as every click in edit mode does (§145). In the card's px, as <see cref="CardTarget"/>.
    /// </summary>
    protected internal virtual int CardEditTarget(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface) => 0;

    /// <summary>
    /// edit: <see cref="CardEditTarget"/> on <paramref name="surface"/>, as <see cref="TargetOn"/> asks
    /// for a click: nothing where the widget is not set up or is drawn as its bar cells.
    /// </summary>
    internal int EditTargetOn(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        using var typeface = Gfx.Wearing(style.Look.Typeface);
        return Unset is null && CardDrawn(box, style) ? CardEditTarget(point, Scrolled(box, style, surface).At(point), style, surface) : 0;
    }

    /// <summary>
    /// edit: what a click on edit target <paramref name="target"/> changes, written into
    /// <paramref name="item"/>, a copy of this widget's settings that the dock saves; false where it
    /// changes nothing. The widget's own settings are the dock's to replace (Configure).
    /// </summary>
    protected internal virtual bool EditClick(int target, WidgetItem item) => false;

    public IslandCells Measure(IslandStyle style)
    {
        var cells = Cells(style);
        told[style] = cells;
        Asked(style);
        return cells;
    }

    /// <summary>A style asked for in a rebuild: kept, and one set aside by the last one taken back.</summary>
    private void Asked(in IslandStyle style)
    {
        if (!rebuilding)
        {
            return;
        }
        asked.Add(style);
        if (!kept.ContainsKey(style) && aside.Remove(style, out var back))
        {
            // Not drawn again as what it shows changed while it was aside: drawn again as it is taken back (Reuse).
            previous[style] = back;
        }
    }

    public void Rebuilding()
    {
        rebuilding = true;
        asked.Clear();
    }

    public void Rebuilt()
    {
        rebuilding = false;
        foreach (var surfaces in aside.Values)
        {
            CloseAll(surfaces);
        }
        aside.Clear();
        foreach (var surface in retired)
        {
            surface.Dispose();
        }
        retired.Clear();
        // What a Forget set aside and this rebuild took nothing back of shows nowhere now: every bar was laid out.
        foreach (var surfaces in previous.Values)
        {
            CloseAll(surfaces);
        }
        previous.Clear();
        foreach (var style in kept.Keys.Where(style => !asked.Contains(style)).ToList())
        {
            aside[style] = kept[style];
            kept.Remove(style);
            told.Remove(style);
        }
        asked.Clear();
    }

    private static void CloseAll(CompositionDrawingSurface?[] surfaces)
    {
        foreach (var surface in surfaces)
        {
            surface?.Dispose();
        }
    }

    public CompositionDrawingSurface Surface(int index, IslandStyle style)
    {
        var (width, height) = CellSize(index, style);
        if ((uint)index >= (uint)Count)
        {
            // A cell that has gone since the layout was measured: blank, and
            // not kept, so the rebuild that follows asks again.
            var blank = Gfx.Draw(width, height, static _ => { });
            blanks.Add(blank);
            return blank;
        }
        Asked(style);
        if (!kept.TryGetValue(style, out var surfaces) || surfaces.Length < Count)
        {
            Array.Resize(ref surfaces, Count);
            kept[style] = surfaces;
        }
        if (surfaces[index] is { } standing && (standing.Size.Width != width || standing.Size.Height != height))
        {
            // §359: kept through the rebuilds, a cell given another length gets a surface of its own size;
            // the one it had is closed as the next rebuild ends, the scene showing the new one by then.
            retired.Add(standing);
            surfaces[index] = null;
        }
        if (surfaces[index] is not { } surface)
        {
            surface = Reuse(index, style, width, height) ?? DrawPart(index, style, WholePart, width, height);
            surfaces[index] = surface;
            Paints++;
        }
        return surface;
    }

    /// <summary>
    /// Cell <paramref name="index"/>'s surface from before the last <see cref="Forget"/>, painted
    /// again with what the island shows now, when it has the size the cell has now; else null.
    /// </summary>
    private CompositionDrawingSurface? Reuse(int index, in IslandStyle style, float width, float height)
    {
        if (!previous.TryGetValue(style, out var old) || index >= old.Length || old[index] is not { } surface)
        {
            return null;
        }
        var size = surface.Size;
        if (size.Width != width || size.Height != height)
        {
            return null;
        }
        old[index] = null;
        RedrawPart(surface, index, style, WholePart, width, height);
        return surface;
    }

    public virtual IReadOnlyList<IslandLayer> Layers(int index, IslandStyle style)
    {
        var (width, height) = CellSize(index, style);
        return [new IslandLayer(Surface(index, style), Vector2.Zero, new Vector2(width, height), null)];
    }

    public bool Tick(Gfx gfx) => Update(gfx, everything: false);

    public bool Refresh(Gfx gfx) => Update(gfx, everything: true);

    /// <summary>Nothing here depends on the time of day unless an island says so.</summary>
    public virtual bool MinuteChanged(Gfx gfx) => false;

    public void Forget()
    {
        // What the last Forget set aside and no build took back shows nowhere now.
        Close(previous);
        foreach (var (style, surfaces) in kept)
        {
            previous[style] = surfaces;
        }
        // What a rebuild set aside too: drawn again where it is taken back.
        foreach (var (style, surfaces) in aside)
        {
            if (!previous.TryAdd(style, surfaces))
            {
                CloseAll(surfaces);
            }
        }
        aside.Clear();
        kept.Clear();
        told.Clear();
        ForgetLayers();
    }

    public virtual void Dispose()
    {
        Forget();
        Close(previous);
        foreach (var surface in retired)
        {
            surface.Dispose();
        }
        retired.Clear();
    }

    /// <summary>Closes every surface in <paramref name="set"/>, and the blanks handed out.</summary>
    private void Close(Dictionary<IslandStyle, CompositionDrawingSurface?[]> set)
    {
        foreach (var surfaces in set.Values)
        {
            foreach (var surface in surfaces)
            {
                surface?.Dispose();
            }
        }
        set.Clear();
        foreach (var blank in blanks)
        {
            blank.Dispose();
        }
        blanks.Clear();
    }

    /// <summary>
    /// A cell's surface size for a style: the length along the dock the cell is given
    /// (<see cref="Allot"/>), the thickness across it. The same lengths the layout gives the cells,
    /// so the surface is exactly the rectangle the cell was given and the island paints into all of
    /// it.
    /// </summary>
    internal (float Width, float Height) CellSize(int index, in IslandStyle style)
    {
        var length = (uint)index < (uint)Count ? MathF.Max(1, Allot(style).Lengths[index]) : 1;
        var thickness = Whole(style.Thickness);
        return style.Vertical ? (thickness, length) : (length, thickness);
    }

    /// <summary>
    /// What each of the island's cells gets along the dock, in px, in the room
    /// <paramref name="style"/> says the widget may have: each cell's content rounded up to whole
    /// grid cells and shared out where the range asks for another number of them
    /// (<see cref="Layout.AllotCells"/>); a cell left at 0 does not fit and is not shown.
    /// <para>
    /// A widget whose shape is exactly its content (<see cref="WidgetShape.Exact"/>, a folder's
    /// button) is that in whole px instead, and says so (<c>Exact</c>): the layout takes the
    /// lengths as they are rather than hold them to the grid.
    /// </para>
    /// </summary>
    private (float[] Lengths, bool Exact) Allot(in IslandStyle style)
    {
        var (content, exact) = Content(style);
        return Share(content, exact, style.Room, style.Thickness);
    }

    /// <summary>
    /// How cells of <paramref name="content"/> px share <paramref name="room"/> in cells of
    /// <paramref name="cell"/> px: each exactly its content in whole px where <paramref name="exact"/>
    /// (<see cref="Layout.WholeSpan"/>), else each a whole number of cells, one at least
    /// (<see cref="Layout.AllotCells"/>). The one rule, for the island and for whoever checks its
    /// size against its shape.
    /// </summary>
    internal static (float[] Lengths, bool Exact) Share(IReadOnlyList<float> content, bool exact, WidgetBounds room, float cell) =>
        exact ? ([.. content.Select(Layout.WholeSpan)], true) : (Layout.AllotCells(content, room, cell), false);

    /// <summary>
    /// Each cell's content length before the grid rounds it. For a kind whose shape is a function
    /// of its settings the answer comes from <see cref="WidgetShapes"/> and the island is never
    /// asked; for the rest it still comes from <see cref="Length"/>, which is the adapter that
    /// lets the kinds be converted one at a time (docs\widgets.md §6, stage 3).
    /// <para>
    /// How many cells there are stays the island's own: a widget with nothing at all to show takes
    /// itself off the bar rather than changing size, and that is a live question by design.
    /// </para>
    /// <para>
    /// <c>Exact</c> is whether the shape is exactly its content (<see cref="WidgetShape.Exact"/>),
    /// for the cells there are.
    /// </para>
    /// </summary>
    private (float[] Content, bool Exact) Content(in IslandStyle style)
    {
        var count = Count;
        var content = new float[count];
        var shape = count > 0 && Settings is { } settings && WidgetShapes.For(Drawing) is { } measure
            ? measure(Shaped(new ShapeContext(settings, Info, style, Gfx) { Island = this }))
            : (WidgetShape?)null;
        for (var index = 0; index < count; index++)
        {
            var own = shape is { } cells && index < cells.Count ? cells.Cells[index] : Length(index, style);
            content[index] = float.IsFinite(own) ? own : 1;
        }
        return (content, shape is { Exact: true } exact && exact.Count == count);
    }

    /// <summary>
    /// Paints every kept surface again with what the island knows now. True
    /// when a cell now wants another length than the dock laid out, which
    /// only a rebuild can give it.
    /// </summary>
    protected bool Redraw(Gfx gfx)
    {
        Gfx = gfx;
        foreach (var (style, cells) in told)
        {
            if (Moved(style, cells))
            {
                return true;
            }
        }
        try
        {
            foreach (var (style, surfaces) in kept)
            {
                for (int index = 0; index < surfaces.Length && index < Count; index++)
                {
                    if (surfaces[index] is { } surface)
                    {
                        var (width, height) = CellSize(index, style);
                        RedrawPart(surface, index, style, WholePart, width, height);
                    }
                }
            }
            RedrawLayers();
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            // A lost device or a closed surface. The dock runs on; the next
            // rebuild makes new surfaces, the next tick tries again.
            if (!reported)
            {
                reported = true;
                Console.Error.WriteLine($"Karpo: {Info.Id} island not redrawn: {error.Message}");
            }
        }
        return false;
    }

    /// <summary>A new surface with <paramref name="part"/> of a cell drawn into it.</summary>
    protected CompositionDrawingSurface DrawPart(int index, in IslandStyle style, int part, float width, float height)
    {
        using var typeface = Gfx.Wearing(style.Look.Typeface);
        Aim(index, style, part, width, height);
        if (part != WholePart)
        {
            return Gfx.Draw(width, height, paint);
        }
        CompositionDrawingSurface? drawn = null;
        cutCells[index] = CutWords.While(() => drawn = Gfx.Draw(width, height, paint));
        return drawn!;
    }

    /// <summary><paramref name="part"/> of a cell painted again into its surface.</summary>
    protected void RedrawPart(CompositionDrawingSurface surface, int index, in IslandStyle style, int part, float width, float height)
    {
        using var typeface = Gfx.Wearing(style.Look.Typeface);
        Aim(index, style, part, width, height);
        if (part != WholePart)
        {
            Gfx.Redraw(surface, paint);
            return;
        }
        cutCells[index] = CutWords.While(() => Gfx.Redraw(surface, paint));
    }

    /// <summary>The words each cell drew cut short as it was last painted whole, in the cell's px (CutWords).</summary>
    private readonly Dictionary<int, IReadOnlyList<CutRun>> cutCells = [];

    /// <summary>
    /// The words cell <paramref name="index"/> drew cut short at <paramref name="point"/> (the cell's px), whole, and where
    /// they stand: what the bar's tooltip says over them (Dock.TooltipFor, CutWords); null where none is.
    /// </summary>
    internal CutRun? CutAt(int index, Vector2 point) =>
        cutCells.TryGetValue(index, out var runs) ? CutWords.At(runs, point) : null;

    /// <summary>An island with layers of its own redraws them here, after the whole cells.</summary>
    protected virtual void RedrawLayers()
    {
    }

    /// <summary>An island with layers of its own drops them here.</summary>
    protected virtual void ForgetLayers()
    {
    }

    /// <summary>Tells the dock, from any thread, that work done elsewhere is ready for <see cref="Refresh"/>.</summary>
    protected void Post()
    {
        var target = Volatile.Read(ref dock);
        if (target == 0)
        {
            Interlocked.Exchange(ref wanted, 1);
            // Attach may have run in between and missed the flag.
            target = Volatile.Read(ref dock);
            if (target == 0 || Interlocked.Exchange(ref wanted, 0) == 0)
            {
                return;
            }
        }
        _ = Islands.Post(target, Volatile.Read(ref token));
    }

    /// <summary>
    /// For a shared store with news: posts WM_KARPO_ISLAND when the island is
    /// attached, so the dock refreshes it. False when it is not attached yet;
    /// it posts once it is.
    /// </summary>
    internal bool Wake()
    {
        var target = Volatile.Read(ref dock);
        if (target == 0)
        {
            // Not attached yet (a store read that beat the dock's Attach): Attach posts
            // then, so the island still draws the news the store takes meanwhile.
            Interlocked.Exchange(ref wanted, 1);
            target = Volatile.Read(ref dock);
            if (target == 0 || Interlocked.Exchange(ref wanted, 0) == 0)
            {
                return false;
            }
        }
        return Islands.Post(target, Volatile.Read(ref token));
    }

    /// <summary>Whole px, one at least.</summary>
    private static float Whole(float px) => float.IsFinite(px) ? MathF.Max(MathF.Ceiling(px), 1) : 1;

    /// <summary>What its shape is measured from: its settings; a folder adds which of its faces are on the bar now (§237).</summary>
    private protected virtual ShapeContext Shaped(ShapeContext context) => context;

    /// <summary>Whether the cells now differ from what the dock was told, without building a new list.</summary>
    private bool Moved(in IslandStyle style, in IslandCells cells)
    {
        var count = Count;
        if (count != cells.Count)
        {
            return true;
        }
        // Told in whole cells, and laid out in whole cells: a cell only moves
        // when its content crosses into another number of them.
        var (now, exact) = Allot(style);
        if (exact != cells.Exact)
        {
            return true;
        }
        for (var index = 0; index < count; index++)
        {
            var was = cells.Lengths is { } lengths && index < lengths.Count ? lengths[index] : cells.CellLength;
            if (now[index] != was)
            {
                return true;
            }
        }
        return false;
    }

    private IslandCells Cells(in IslandStyle style)
    {
        using var typeface = Gfx.Wearing(style.Look.Typeface);
        var count = Count;
        if (count == 0)
        {
            return new IslandCells(0, 0);
        }
        var (lengths, exact) = Allot(style);
        var longest = 0f;
        var uniform = true;
        for (var index = 0; index < count; index++)
        {
            longest = MathF.Max(longest, lengths[index]);
            uniform &= lengths[index] == lengths[0];
        }
        return new IslandCells(count, longest, uniform ? null : lengths, exact);
    }

    private bool Update(Gfx gfx, bool everything)
    {
        var changed = Sample(everything);
        return Redraw(gfx) | changed;
    }

    private void Aim(int index, in IslandStyle style, int part, float width, float height)
    {
        paintIndex = index;
        paintStyle = style;
        paintWidth = width;
        paintHeight = height;
        Part = part;
    }

    private void PaintAimed(ID2D1DeviceContext* dc) => PaintBar(dc, paintIndex, paintStyle, paintWidth, paintHeight);

    /// <summary>
    /// Cell <paramref name="index"/> as the bar draws it: the kind's own drawing, or, for a widget
    /// with one cell that is not set up, its "Set up in Settings" words centred in the cell
    /// (<see cref="SetupFace.OnBar"/>), as on its line in a folder; an own layer of such a widget
    /// is left empty.
    /// </summary>
    internal void PaintBar(ID2D1DeviceContext* dc, int index, in IslandStyle style, float width, float height)
    {
        using var typeface = Gfx.Wearing(style.Look.Typeface);
        if (Count == 1 && Unset is { } need)
        {
            if (Part == WholePart)
            {
                SetupFace.OnBar(dc, Gfx, style, width, height, SetupGlyph);
            }
            return;
        }
        if (UnavailableOn(style.Screen) is not null)
        {
            var greyed = style;
            Unavailable.Paint(dc, style.Look, drawn => PaintCell(drawn, index, greyed, width, height));
            return;
        }
        PaintCell(dc, index, style, width, height);
    }
}

/// <summary>
/// Sizes that follow the bar: text, glyphs and spacing for a cell as thick as
/// the style says, from a 20-px sliver to a 160-DIP slab. All in px.
///
/// The cell is a square of that thickness (see <see cref="Layout.CellPx"/>),
/// and the sizes here are shares of it, so a widget's content grows with the
/// square it was given instead of staying taskbar-sized in the middle of it.
/// </summary>
internal readonly struct Fit
{
    // Sizes Segoe Fluent Icons is drawn for; in between them a glyph blurs.
    // Past 48 the font has no design size, but it is an outline font and
    // scales cleanly, and a tall bar's cell needs the steps: a glyph that
    // stopped at 48 would be a speck in a 156-DIP square.
    private static readonly float[] GlyphSteps = [16, 20, 24, 32, 40, 48, 56, 64, 80];

    // The taskbar's own icons (the network's, the sound's, the battery's, the bell, the status
    // icons, a tray icon) and a widget's glyph: Windows 11's 16 DIP on its 48-DIP bar, a third of
    // the bar, which is five twelfths of the 38.4-DIP card that bar gives — on the icon font's own
    // sizes: 16 DIP up to a 54-DIP bar, 20 on a 64-DIP one, 24 on an 80-DIP one. An app's icon is
    // half the bar (24 DIP of 48, Config.IconDips), so the two keep Windows' proportion.
    // The person, 2026-10-09: "the icons that are not pins ... are too big" — they were 0.55, 20 DIP.
    // Taken to the step at or under it, with no slack (StepSlack): a floating dock's 44-DIP card
    // would round its 18 DIP up to 20, and its icons would be larger than the islands' for no reason.
    private const float SystemIconShare = 5f / 12f;

    // How far above a step a glyph may be and still take it: a glyph that is nearly a step is that
    // step, instead of the one under it (a 35-DIP card's 19.3 is 20, not 16).
    private const float StepSlack = 1.1f;

    public readonly float Scale;
    public readonly float TextScale;

    /// <summary>The cell's thickness in DIPs, which is also the grid cell's side.</summary>
    public readonly float Thick;

    public readonly bool Vertical;

    private readonly float cell;

    public Fit(in IslandStyle style)
    {
        Scale = style.Scale > 0 ? style.Scale : 1;
        TextScale = TextLine.Scale(style.Look);
        Thick = style.Thickness / Scale;
        Vertical = style.Vertical;
        cell = MathF.Max(1, style.Thickness);
    }

    /// <summary>The grid cell's side in px.</summary>
    public float Cell => cell;

    /// <summary>How many whole cells content <paramref name="px"/> px long needs, one at least.</summary>
    public int Cells(float px) => Layout.CellsFor(px, cell);

    /// <summary>What those cells measure along the dock, in px.</summary>
    public float Span(float px) => Layout.CellSpan(px, cell);

    /// <summary>
    /// The main text: the taskbar's 12 DIP on the 48-DIP bar's cell, and in
    /// proportion on any other, so it fills a thicker one (see
    /// <see cref="Layout.CellTextDips"/>).
    /// </summary>
    public float Primary => Layout.CellTextDips(Thick) * Scale * TextScale;

    /// <summary>
    /// A step under the main size, never under 11 DIP: a rung of the ladders a line steps down
    /// through where the main size does not fit (VerticalFit). It named the quieter second line
    /// once; a taskbar entry has none across a horizontal bar (docs\widgets.md §12.5).
    /// </summary>
    public float Secondary => MathF.Max(11, Primary / (Scale * TextScale) - 1) * Scale * TextScale;

    /// <summary>The smallest text anything uses: a little under the main one, never below 11 DIP.</summary>
    public float Small => MathF.Max(11, 0.85f * Layout.CellTextDips(Thick)) * Scale * TextScale;

    /// <summary>
    /// A glyph of about <paramref name="share"/> of the cell, on one of the icon
    /// font's own sizes. It grows with the cell: a glyph that stopped at 24 DIP
    /// left a tall bar's widget a speck in a large square.
    /// </summary>
    public float Glyph(float share) => Snap(Thick * share);

    /// <summary>A widget's glyph: the taskbar's own icons' size (<see cref="SystemIcon"/>), beside its text or alone.</summary>
    public float Glyph() => SystemIcon;

    /// <summary>
    /// The taskbar's own icons' glyph: the network's, the sound's, a battery-less PC's power and the
    /// status icons beside them, the bell, and a tray icon (Layout.TrayIconPx). Windows' tray draws
    /// them at 16 DIP on its 48-DIP bar, which this is, and in proportion on a thicker one. One
    /// property, so the icons that stand side by side are one size.
    /// </summary>
    public float SystemIcon => SnapAt(Thick * SystemIconShare, Scale, slack: 1f);

    /// <summary>
    /// <see cref="SystemIcon"/> for a cell <paramref name="cellPx"/> px thick at <paramref name="scale"/>,
    /// for a size wanted before any style exists.
    /// </summary>
    public static float SystemIconFor(float cellPx, float scale) =>
        SnapAt(MathF.Max(1, cellPx) / (scale > 0 ? scale : 1) * SystemIconShare, scale > 0 ? scale : 1, slack: 1f);

    /// <summary>The largest of the icon font's sizes that fits in <paramref name="px"/>, 16 DIP at least.</summary>
    public float GlyphFor(float px) => Snap(px / Scale);

    /// <summary>A usage bar's thickness.</summary>
    public float Bar => MathF.Max(1, Px(Math.Clamp(Thick * 0.075f, 2, 8)));

    /// <summary>A graph's line.</summary>
    public float Stroke => (Thick >= 80 ? 2f : 1.5f) * Scale;

    /// <summary>How far apart a graph's samples are: 2 DIP, in whole px, so every display shows the same stretch of time.</summary>
    public float Spacing => MathF.Max(1, Px(2));

    /// <summary>
    /// Along the dock, at each end of a cell: the room the content keeps from
    /// the card's edge, since the cells fill the card. A share of the cell, so
    /// a thin bar's 24-DIP cell does not spend a third of itself on padding
    /// and a thick one's does not look crowded; 4 DIPs on the taskbar's bar.
    /// </summary>
    public float Edge => Px(Math.Clamp(Thick * 0.18f, 4, 12));

    /// <summary>
    /// Across the dock, at each side of a line of text that is read across it (down a vertical
    /// bar, or the one square cell's language code): the narrow room a line keeps, 4 DIPs on the
    /// taskbar's bar. <see cref="Edge"/> is the room along the run, which a text widget needs
    /// more of, and is too much across a cell that is only as wide as the bar is thick.
    /// </summary>
    public float Side => Px(Math.Clamp(Thick * 0.09f, 2, 8));

    /// <summary>Between the cell's edge and its content, across the dock.</summary>
    public float Inset => Px(Math.Clamp(Thick * 0.12f, 2, 14));

    /// <summary>Between a glyph and its text: a share of the cell, 6 DIPs on the taskbar's bar.</summary>
    public float Gap => Px(Math.Clamp(Thick * 0.135f, 4, 12));

    /// <summary>How wide text may be across a vertical dock: all of it but a pixel each side.</summary>
    public float Across => Thick * Scale - 2 * MathF.Max(1, Px(1));

    /// <summary>Between two lines of a block, which only a vertical bar stacks (docs\widgets.md §12.5).</summary>
    public float Leading => Px(Math.Clamp(Thick * 0.05f, 0, 6));

    public float Px(float dips) => TextLine.Round(dips * Scale);

    private float Snap(float dips) => SnapAt(dips, Scale, StepSlack);

    private static float SnapAt(float dips, float scale, float slack)
    {
        var size = GlyphSteps[0];
        foreach (var step in GlyphSteps)
        {
            if (step <= dips * slack)
            {
                size = step;
            }
        }
        return TextLine.Round(size * scale);
    }

}

/// <summary>
/// What a widget shows at the width someone chose for it.
///
/// A kind whose content has more than one shape — the screen time's full
/// "8h 88m" and its short "1h", the weather with and without the condition's
/// words — picks the fullest shape that fits the room the chosen width gives
/// it. The choice is made from the **settings** alone: the chosen width and
/// the cell's size, never the value the widget happens to be showing, so the
/// content's shape is as steady as the widget's size is (see CellPair and the
/// rule in Layout's header). At the automatic size there is no width to fit,
/// and the content decides instead: the fullest shape wins and the widget is
/// measured for it.
/// </summary>
internal static class CellRoom
{
    /// <summary>
    /// The room a widget's content has along the dock at the width someone
    /// chose, or null at the automatic size, where the content sets the width
    /// rather than the other way round.
    /// </summary>
    public static float? Of(in IslandStyle style, in Fit fit) =>
        style.Width >= WidgetItem.MinWidth ? MathF.Max(0f, style.Width * style.Cell - 2 * fit.Edge) : null;

    /// <summary>
    /// The fullest of <paramref name="shapes"/> — shortest first, each the
    /// length that shape needs — that fits the room the width gives: the last
    /// at the automatic size, and the shortest where not even that fits, since
    /// a widget shows what it can rather than nothing.
    /// </summary>
    public static int Fullest(in IslandStyle style, in Fit fit, ReadOnlySpan<float> shapes)
    {
        if (shapes.Length == 0)
        {
            return 0;
        }
        if (Of(style, fit) is not { } room)
        {
            return shapes.Length - 1;
        }
        for (var index = shapes.Length - 1; index > 0; index--)
        {
            if (shapes[index] <= room)
            {
                return index;
            }
        }
        return 0;
    }
}

/// <summary>
/// How long a glyph and a short value are along the dock, side by side or the glyph over the value.
/// <para>
/// <b>Across a horizontal bar they are side by side, always</b>: a taskbar entry is one row of
/// content at every height (docs\widgets.md §12.5). The glyph used to go over the value wherever
/// that saved a cell — a battery's "88%" under its glyph took one cell of the 48 DIP bar instead of
/// two — and that was a second row. A pair now costs the cells its row costs.
/// </para>
/// <para>
/// <b>Down a vertical bar the glyph stands over the value</b>, which is the one place a pair stacks:
/// a row would run across the bar's width. The painters draw whichever of the two
/// <see cref="IslandStyle.Vertical"/> says.
/// </para>
/// </summary>
internal static class CellPair
{
    /// <summary>
    /// The content length along the dock, before the layout rounds it up to cells, of a
    /// <paramref name="glyph"/> px glyph and a value <paramref name="textWidth"/> ×
    /// <paramref name="textHeight"/> px in a cell of <paramref name="style"/>. A value of no width (a
    /// widget with nothing to say yet) leaves the glyph alone.
    /// </summary>
    public static float Length(in IslandStyle style, in Fit fit, float glyph, float textWidth, float textHeight) =>
        Padded(fit, style.Vertical ? glyph + textHeight : glyph + (textWidth > 0 ? fit.Gap + textWidth : 0));

    /// <summary>
    /// <paramref name="content"/> px of content with the room a cell keeps at
    /// its ends, tightened to a sliver where the full room would push the
    /// widget into another whole cell. A pair is drawn centred in the cells it
    /// was given, so the tighter padding costs nothing: it only stops a widget
    /// spending a cell on air.
    /// </summary>
    private static float Padded(in Fit fit, float content)
    {
        var padded = content + (2 * fit.Edge);
        // A glyph and a little air is one square cell; the full edge is only where the content
        // is wider than that (a widget is as long as its content needs, docs 56).
        var tight = content + (2 * MathF.Max(1, fit.Px(2)));
        return padded > fit.Cell && tight <= fit.Cell ? tight : padded;
    }
}

/// <summary>The colours an island cell uses, from REVISION 1's tokens.</summary>
internal readonly struct IslandInk
{
    public readonly DXGI_RGBA Text;
    public readonly DXGI_RGBA Secondary;

    /// <summary>Graph lines and usage bars.</summary>
    public readonly DXGI_RGBA Accent;

    /// <summary>The empty part of a usage bar.</summary>
    public readonly DXGI_RGBA Track;

    /// <summary>A usage bar that is nearly full.</summary>
    public readonly DXGI_RGBA Alarm;

    /// <summary>A command that cannot be used now.</summary>
    public readonly DXGI_RGBA Disabled;

    /// <summary>The system's colours alone: every warning is the highlight.</summary>
    public readonly bool HighContrast;

    public IslandInk(in Look look)
    {
        HighContrast = look.HighContrast;
        if (look.HighContrast)
        {
            // Only the system's own colours; a translucent track is the one
            // blend left, and it only frames the bar's system-coloured fill.
            var text = Solid(look.HcText, look.Dark ? Rgb(0xFFFFFF) : Rgb(0x000000));
            Text = text;
            Secondary = text;
            Accent = Solid(look.HcHighlight, text);
            Track = Faded(text, 0.25f);
            Alarm = Accent;
            Disabled = Faded(text, 0.5f);
        }
        else if (look.Dark)
        {
            Text = new DXGI_RGBA(1, 1, 1, 1);
            Secondary = new DXGI_RGBA(1, 1, 1, 0.78f);
            Accent = Solid(look.AccentLight2, Solid(look.Accent, Rgb(0x4CC2FF)));
            Track = Faded(Secondary, 0.25f);
            Alarm = Rgb(0xFF99A4);
            Disabled = new DXGI_RGBA(1, 1, 1, 0.36f);
        }
        else
        {
            Text = new DXGI_RGBA(0, 0, 0, 0.9f);
            Secondary = new DXGI_RGBA(0, 0, 0, 0.62f);
            Accent = Solid(look.AccentDark1, Solid(look.Accent, Rgb(0x005FB8)));
            Track = Faded(Secondary, 0.25f);
            Alarm = Rgb(0xC42B1C);
            Disabled = new DXGI_RGBA(0, 0, 0, 0.36f);
        }
        // §396: the text the layout's colours set, the quieter ones it, fainter by the same steps.
        if (!look.HighContrast && look.Ink is { } ink)
        {
            float Share(float alpha) => ink.A / 255f * alpha;
            Text = new DXGI_RGBA(ink.R / 255f, ink.G / 255f, ink.B / 255f, Share(1));
            Secondary = Text with { a = Share(0.78f) };
            Track = Faded(Secondary, 0.25f);
            Disabled = Text with { a = Share(0.36f) };
        }
    }

    /// <summary>
    /// The taskbar's own icons: the network, the sound and the battery, and the status icons beside
    /// them (IndicatorsIsland). Windows 11 draws every one in its primary text colour
    /// (TextFillColorPrimary: its quick-settings button's OmniButtonForeground, at rest, under the
    /// pointer and pressed alike), the clock's colour and never a quieter one; in high contrast,
    /// the system's text colour. One property, so the icons that stand side by side cannot drift
    /// apart.
    /// </summary>
    public DXGI_RGBA SystemIcon => Text;

    /// <summary>
    /// SystemFillColorCritical: Windows' critical red (#C42B1C on light, #FF99A4 on dark), the dot
    /// its tray icon for a restart waiting wears (RestartDot); the system's highlight in high
    /// contrast. The same colour as <see cref="Alarm"/>, named for what Windows calls it.
    /// </summary>
    public DXGI_RGBA Critical => Alarm;

    public static DXGI_RGBA Faded(DXGI_RGBA color, float alpha) => new(color.r, color.g, color.b, color.a * alpha);

    /// <summary>
    /// The system colour, or <paramref name="fallback"/> where the Look left it
    /// unset: a Look built with only an accent would otherwise draw every
    /// graph in transparent black.
    /// </summary>
    private static DXGI_RGBA Solid(Windows.UI.Color color, DXGI_RGBA fallback) =>
        color.A == 0 ? fallback : new DXGI_RGBA(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);

    private static DXGI_RGBA Rgb(uint rgb) =>
        new(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1);
}

/// <summary>
/// A laid-out line of text and its size. Released by its owner. Drawing happens on the dock's
/// one thread, but <b>measuring does not</b>: the offscreen tests run a class per thread, each
/// with its own device, and a widget's shape measures templates (WidgetShapes). So the two
/// things shared between threads — the template cache and the tabular-figures typography — are
/// made safe here rather than assumed away.
/// </summary>
internal unsafe struct TextLine
{
    /// <summary>Templates measured so far; a handful per style, cleared if it ever grows past this.</summary>
    private const int MeasuredLimit = 512;

    // Concurrent rather than locked: this is read on almost every call and written once per new
    // template, so the reads are the hot path and a ConcurrentDictionary leaves them lock-free.
    // A lock would put every measurement of an already-known template behind one gate for the
    // sake of the rare first one. Two threads measuring the same new template both lay it out
    // and agree on the answer, so the duplicated work is a wasted layout and nothing worse.
    //
    // Keyed by the language too: DirectWrite lays a text out in the language's locale
    // (Languages.TextLocale), which picks the fallback font for what Segoe UI has no glyph of, so
    // the same template can measure otherwise after a language change (docs\widgets.md §36). §459: and by the text style
    // Karpo wears, whose font and weight measure otherwise.
    private static readonly ConcurrentDictionary<(string Template, float Px, Weight Weight, string Language, TextStyle Typeface), (float Width, float Height)> Measured = new();

    // The typography object is made once and then only read; DirectWrite's own objects are
    // free-threaded, so sharing the pointer is fine and only the making needs a gate. A lock
    // here rather than a concurrent collection: it is a one-off, off every hot path, and there
    // is nothing to look up.
    private static readonly object TabularGate = new();
    private static IDWriteTypography* tabular;
    private static bool triedTabular;

    public IDWriteTextLayout* Layout;
    public float Width;
    public float Height;

    /// <summary>The words it was laid out from, where it has a room to be cut at: said whole where it is cut (CutWords).</summary>
    public string? Words;

    public readonly bool IsEmpty => Layout == null;

    /// <summary>
    /// One line in the dock's font with tabular figures, so every digit is as
    /// wide as every other; or in <paramref name="family"/>, the icon font for glyphs.
    /// </summary>
    public static TextLine Lay(Gfx gfx, string text, float px, Weight weight, float? maxWidth, string? family = null)
    {
        var line = new TextLine { Layout = gfx.TextLayout(text, px, weight, maxWidth, family), Words = maxWidth.HasValue ? text : null };
        if (family is null && text.Length > 0 && Tabular() is var figures && figures != null)
        {
            _ = line.Layout->SetTypography(figures, new DWRITE_TEXT_RANGE { startPosition = 0, length = (uint)text.Length });
        }
        DWRITE_TEXT_METRICS metrics;
        if (line.Layout->GetMetrics(&metrics).SUCCEEDED)
        {
            line.Width = MathF.Ceiling(metrics.width);
            line.Height = MathF.Ceiling(metrics.height);
        }
        return line;
    }

    /// <summary>
    /// Tabular figures on all of <paramref name="layout"/>'s <paramref name="length"/> characters, as
    /// <see cref="Lay"/> gives its lines: for a layout made elsewhere (PopupPaint.Figures).
    /// </summary>
    public static void Figures(IDWriteTextLayout* layout, int length)
    {
        if (length > 0 && Tabular() is var figures && figures != null)
        {
            _ = layout->SetTypography(figures, new DWRITE_TEXT_RANGE { startPosition = 0, length = (uint)length });
        }
    }

    /// <summary>
    /// The size of every text shaped like <paramref name="text"/>: its digits
    /// read as eights, which in tabular figures are as wide as any other. Laid
    /// out once per shape, size and weight, then taken from a cache, so a
    /// cell can ask on every redraw. For short, recurring texts only: numbers,
    /// dates, drive letters, weather words.
    /// </summary>
    public static (float Width, float Height) Template(Gfx gfx, string text, float px, Weight weight)
    {
        var template = Shape(text);
        var key = (template, px, weight, Strings.Language, gfx.Typeface);
        if (Measured.TryGetValue(key, out var known))
        {
            return known;
        }
        if (Measured.Count >= MeasuredLimit)
        {
            Measured.Clear();
        }
        var line = Lay(gfx, template, px, weight, null);
        var size = (line.Width, line.Height);
        line.Release();
        Measured[key] = size;
        return size;
    }

    /// <summary>
    /// Where the ink of every text shaped like <paramref name="text"/> lies across its line: the top
    /// of its tallest figure and the bottom of its lowest, in px from the line's top. Taken from the
    /// template (<see cref="Template"/>), never from the value showing, so a graph stood beside a
    /// reading is as tall as its figures whatever they say.
    /// </summary>
    public static (float Top, float Bottom) Ink(Gfx gfx, string text, float px, Weight weight)
    {
        var line = Lay(gfx, Shape(text), px, weight, null);
        try
        {
            DWRITE_OVERHANG_METRICS overhang;
            if (line.Layout == null || line.Layout->GetOverhangMetrics(&overhang).FAILED)
            {
                return (0f, line.Height);
            }
            // Overhangs are measured outward from the edges of the layout's own box.
            return (-overhang.top, line.Layout->GetMaxHeight() + overhang.bottom);
        }
        finally
        {
            line.Release();
        }
    }

    /// <summary>The width of text that is content, not a number: a title, measured each time it is asked for.</summary>
    public static float Measure(Gfx gfx, string text, float px, Weight weight, float? maxWidth)
    {
        var line = Lay(gfx, text, px, weight, maxWidth);
        var width = line.Width;
        line.Release();
        return width;
    }

    /// <summary>Scales text sizes by the Look's text scale, which the dock reads from Windows.</summary>
    public static float Scale(in Look look) => look.TextScale is > 0 and < 10 ? look.TextScale : 1;

    public readonly void Draw(ID2D1DeviceContext* dc, ID2D1SolidColorBrush* brush, DXGI_RGBA color, float x, float y)
    {
        if (Layout == null)
        {
            return;
        }
        var origin = new D2D_POINT_2F(Round(x), Round(y));
        // Tests only: a test painting on this thread sees each line go by (PaintWatch).
        var shown = PaintWatch.Seen(Layout, origin, color);
        brush->SetColor(&shown);
        // In a mirrored line, turned back to read (Mirror.cs).
        Mirror.Text(
            dc,
            origin,
            Layout,
            (ID2D1Brush*)brush,
            D2D1_DRAW_TEXT_OPTIONS.D2D1_DRAW_TEXT_OPTIONS_NONE);
        // A line cut at its room, said whole in its host's tooltip (CutWords).
        CutWords.Drawn(Layout, Words, origin);
    }

    /// <summary>
    /// <paramref name="length"/> characters from <paramref name="start"/> drawn in
    /// <paramref name="brush"/>'s colour instead of the line's own: two inks on one line — a weekday
    /// in the accent before its day, an artist quieter than the title — with the font's own spacing
    /// between them rather than a gap guessed at. The layout keeps the brush until it is released.
    /// </summary>
    public readonly void Tint(int start, int length, ID2D1SolidColorBrush* brush)
    {
        if (Layout == null || brush == null || start < 0 || length <= 0)
        {
            return;
        }
        _ = Layout->SetDrawingEffect((IUnknown*)brush, new DWRITE_TEXT_RANGE { startPosition = (uint)start, length = (uint)length });
    }

    public void Release()
    {
        ComUtil.Release(ref Layout);
        this = default;
    }

    /// <summary>Halves away from zero, like the rest of the dock.</summary>
    public static float Round(float value) => MathF.Round(value, MidpointRounding.AwayFromZero);

    /// <summary><paramref name="text"/> with every digit an eight; the text itself when it has none.</summary>
    private static string Shape(string text)
    {
        if (!text.AsSpan().ContainsAnyInRange('0', '9'))
        {
            return text;
        }
        return string.Create(text.Length, text, static (span, source) =>
        {
            for (var i = 0; i < span.Length; i++)
            {
                span[i] = char.IsAsciiDigit(source[i]) ? '8' : source[i];
            }
        });
    }

    /// <summary>
    /// The typography that turns on tabular figures, made once and kept for
    /// the process's life; null if DirectWrite will not make it, and text
    /// then keeps its default figures.
    /// </summary>
    private static IDWriteTypography* Tabular()
    {
        if (Volatile.Read(ref triedTabular))
        {
            return tabular;
        }
        lock (TabularGate)
        {
            return triedTabular ? tabular : Make();
        }
    }

    /// <summary>Called once, under <see cref="TabularGate"/>: tried once whether or not it worked.</summary>
    private static IDWriteTypography* Make()
    {
        Volatile.Write(ref triedTabular, true);
        IDWriteFactory* factory = null;
        IDWriteTypography* made = null;
        try
        {
            if (DirectX.DWriteCreateFactory(DWRITE_FACTORY_TYPE.DWRITE_FACTORY_TYPE_SHARED, __uuidof<IDWriteFactory>(), (IUnknown**)&factory).FAILED
                || factory->CreateTypography(&made).FAILED)
            {
                return null;
            }
            var feature = new DWRITE_FONT_FEATURE
            {
                nameTag = DWRITE_FONT_FEATURE_TAG.DWRITE_FONT_FEATURE_TAG_TABULAR_FIGURES,
                parameter = 1,
            };
            if (made->AddFontFeature(feature).FAILED)
            {
                ComUtil.Release(ref made);
                return null;
            }
            tabular = made;
            return tabular;
        }
        finally
        {
            ComUtil.Release(ref factory);
        }
    }
}

/// <summary>The last samples of one value, oldest dropped first.</summary>
internal sealed class History(int capacity)
{
    private readonly float[] values = new float[capacity];
    private int next;

    public int Count { get; private set; }

    public int Capacity => values.Length;

    /// <summary>How many samples were ever added, since the last <see cref="Clear"/>: what a <see cref="Series"/> counts its steps from.</summary>
    public long Total { get; private set; }

    public void Add(float value)
    {
        values[next] = value;
        next = (next + 1) % values.Length;
        Count = Math.Min(Count + 1, values.Length);
        Total++;
    }

    public void Clear()
    {
        next = 0;
        Count = 0;
        Total = 0;
    }

    /// <summary>The value <paramref name="age"/> samples before the newest; 0 is the newest.</summary>
    public float Ago(int age) => values[(((next - 1 - age) % values.Length) + values.Length) % values.Length];
}

/// <summary>
/// A <see cref="History"/> taken a sample every <see cref="Pace"/> of its own: each step the mean
/// of <see cref="Pace"/> samples, the steps counted from the history's first sample so a step never
/// changes once it is whole, and the samples of the step still filling left out. At a pace of one it
/// is the history itself. A graph set to read every five seconds draws this of the one source that
/// reads every second, rather than a source of its own.
/// </summary>
internal readonly struct Series(History history, int pace, int most)
{
    private readonly int pace = Math.Max(1, pace);

    public History History => history;

    public int Pace => pace;

    /// <summary>How many whole steps the history holds, never more than <c>most</c>.</summary>
    public int Count
    {
        get
        {
            var filling = (int)(history.Total % pace);
            return Math.Min(Math.Max(0, (history.Count - filling) / pace), most);
        }
    }

    /// <summary>The step <paramref name="age"/> steps before the newest whole one; 0 is the newest.</summary>
    public float Ago(int age)
    {
        if (pace == 1)
        {
            return history.Ago(age);
        }
        var from = (int)(history.Total % pace) + (age * pace);
        var sum = 0f;
        for (var at = from; at < from + pace; at++)
        {
            sum += history.Ago(at);
        }
        return sum / pace;
    }
}

/// <summary>
/// Sizes, rates and durations as people read them. The short forms a cell
/// shows keep one shape over a range of values ("8.8K", "88K", "0.9M"), and
/// each comes with the shape a cell is measured for.
/// </summary>
internal static class Units
{
    /// <summary>What every short rate is measured as: two digits, a point, and the widest unit letter.</summary>
    public const string RateShape = "8.8M";

    /// <summary>
    /// What every rate in full (<see cref="Rate"/>) is measured as: three figures and the widest
    /// unit, "888 MB/s" — wider than any "9.9 KB/s" or "999 GB/s" it stands for. In the language's
    /// units ("888 Mo/s"), so it measures what is drawn.
    /// </summary>
    public static string LongRateShape => $"888 {UnitName(2)}/s";

    /// <summary>What every size (<see cref="Size"/>) is measured as: "88.8 MB", wider than "888 GB".</summary>
    public static string SizeShape => $"88.8 {UnitName(2)}";

    /// <summary>What every part of its whole (<see cref="Of"/>) is measured as.</summary>
    public static string OfShape => S.WidgetCommonSizeOf(part: "88.8", whole: "88.8", unit: UnitName(2));

    /// <summary>What every used/total pair is measured as, in any unit.</summary>
    public const string PairShape = "8.8/8.8M";

    /// <summary>How many units there are, bytes to exabytes.</summary>
    private const int UnitCount = 7;

    /// <summary>
    /// "B", "KB" … "EB" by the power of 1024, in the language Karpo speaks ("Ko" in French). Read
    /// each time, never kept: a language change swaps them.
    /// </summary>
    internal static string UnitName(int unit) => unit switch
    {
        0 => S.WidgetCommonBytes,
        1 => S.WidgetCommonKilobytes,
        2 => S.WidgetCommonMegabytes,
        3 => S.WidgetCommonGigabytes,
        4 => S.WidgetCommonTerabytes,
        5 => S.WidgetCommonPetabytes,
        _ => S.WidgetCommonExabytes,
    };

    private static readonly string[] Percents = Enumerable.Range(0, 101).Select(p => $"{p}%").ToArray();

    /// <summary>"0%" to "100%", without allocating.</summary>
    public static string Percent(float fraction) =>
        Percents[(int)MathF.Round(Math.Clamp(fraction, 0f, 1f) * 100f, MidpointRounding.AwayFromZero)];

    /// <summary>
    /// What a percentage is measured as: <b>a full hundred, always</b>. So a widget reading 4 %
    /// reserves exactly what one reading 100 % reserves, and a processor going flat out does not
    /// shove the bar along at the moment somebody is watching it.
    /// <para>
    /// This used to be a function of the value — two figures below a hundred, three at it — and
    /// that is the state-dependent sizing docs\widgets.md §4 is about: the room a widget keeps
    /// cannot be a reading of the widget's own state, however cheap the saving looks. The figure
    /// that stands in it is right-aligned, so what a shorter reading leaves over is at its left.
    /// </para>
    /// </summary>
    public const string PercentRoom = "100%";

    /// <summary>
    /// Like Explorer: "412 GB", "97.6 GB", "1.8 TB". Each unit holds values up to
    /// 999; from 100 there are no decimals, below one.
    /// </summary>
    public static string Size(ulong bytes)
    {
        var unit = UnitOf(bytes);
        return $"{Number(bytes / Math.Pow(1024, unit))} {UnitName(unit)}";
    }

    /// <summary>"19.5 of 31.8 GB": a part in the unit of its whole.</summary>
    public static string Of(ulong part, ulong whole)
    {
        var unit = UnitOf(whole);
        var divisor = Math.Pow(1024, unit);
        return S.WidgetCommonSizeOf(part: Number(part / divisor), whole: Number(whole / divisor), unit: UnitName(unit));
    }

    /// <summary>
    /// "1.3/1.8T", "12/61M": used and total in two significant digits, in the
    /// unit that keeps the total under 100. Measured as <see cref="PairShape"/>.
    /// </summary>
    public static string Pair(ulong used, ulong total)
    {
        var (value, unit) = TwoDigits(total, 0);
        return $"{Digits(Math.Min(used, total) / Math.Pow(1024, unit))}/{Digits(value)}{UnitName(unit)[0]}";
    }

    /// <summary>"1.2 MB/s", "40 KB/s", for tooltips.</summary>
    public static string Rate(double bytesPerSecond)
    {
        var value = Math.Max(bytesPerSecond, 0) / 1024;
        var unit = 1;
        while (value >= 999.5 && unit < UnitCount - 1)
        {
            value /= 1024;
            unit++;
        }
        var number = value == 0 ? "0" : value.ToString(value >= 9.95 ? "0" : "0.0", CultureInfo.InvariantCulture);
        return $"{number} {UnitName(unit)}/s";
    }

    /// <summary>"0K", "8.8K", "88K", "0.9M": a rate in two significant digits, in kilobytes at least. Measured as <see cref="RateShape"/>.</summary>
    public static string ShortRate(double bytesPerSecond)
    {
        var (value, unit) = TwoDigits(Math.Max(bytesPerSecond, 0) / 1024, 1);
        return $"{Digits(value)}{UnitName(unit)[0]}";
    }

    /// <summary>"8m", "42m", "3h 05m": minutes zero-padded once hours show, so the width changes only at 10m, 1h and 10h.</summary>
    public static string Duration(int minutes) =>
        minutes < 60 ? $"{Math.Max(minutes, 0)}m" : $"{minutes / 60}h {minutes % 60:00}m";

    /// <summary>
    /// The same time in one unit, for a cell with room for no more: "13m",
    /// "30m", "1h", "26h" — the largest unit that applies, rounded **down**,
    /// so it reads as how much has gone by. It says nothing about the minutes
    /// once an hour has passed, which is the trade a one-cell widget makes;
    /// the full form is a cell wider, and the flyout has the whole day.
    /// </summary>
    public static string ShortDuration(int minutes) =>
        minutes < 60 ? $"{Math.Max(minutes, 0)}m" : $"{minutes / 60}h";

    /// <summary>What <see cref="ShortDuration"/> is measured as: two figures and a unit, in either unit.</summary>
    public static readonly string[] ShortDurationShapes = ["88m", "88h"];

    /// <summary>A value moved up from <paramref name="unit"/> until it is under 100.</summary>
    private static (double Value, int Unit) TwoDigits(double value, int unit)
    {
        while (value >= 99.5 && unit < UnitCount - 1)
        {
            value /= 1024;
            unit++;
        }
        return (value, unit);
    }

    /// <summary>Two significant digits: "8.8", "88", "0.9"; "0" for next to nothing.</summary>
    private static string Digits(double value) => value switch
    {
        not >= 0.05 => "0",
        < 9.95 => value.ToString("0.0", CultureInfo.InvariantCulture),
        _ => Math.Min(Math.Round(value, MidpointRounding.AwayFromZero), 99).ToString(CultureInfo.InvariantCulture),
    };

    private static int UnitOf(ulong bytes)
    {
        double value = bytes;
        var unit = 0;
        // Switched at 1000, not 1024, so no value takes four digits.
        while (value >= 999.5 && unit < UnitCount - 1)
        {
            value /= 1024;
            unit++;
        }
        return unit;
    }

    private static string Number(double value) =>
        value.ToString(value >= 99.95 ? "0" : "0.0", CultureInfo.InvariantCulture);
}
