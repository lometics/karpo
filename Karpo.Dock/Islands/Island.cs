// Islands: small self-contained parts of the dock that draw live content,
// such as the weather, the connected drives and the notifications bell.
//
// Every island a person placed is its own instance, keyed "{owner}/{id}"
// (Config.IslandKey): two weather islands for two places are two objects.
// Bars that follow the same list share their island objects. What is
// expensive to read (the media session manager, calendar feeds, the drive
// list, the notification database) is shared between instances, so a second
// island of a kind adds only its own drawing.
//
// An island draws each of its cells into a composition surface once and then
// paints the same surface again whenever its content changes. Every dock
// window that shows the island with the same style shows the same surfaces,
// so one redraw updates every display, and no visual tree is rebuilt for it.
//
// Work that waits on the network or on a disk that may not answer runs off
// the dock's thread. When it is done, the island posts WM_KARPO_ISLAND with
// its token to the dock window it was attached to, and the dock calls
// Refresh, which picks up the result and redraws.

using System.Numerics;
using Karpo.Core;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using Windows.UI.Composition;

namespace Karpo.Dock;

/// <summary>
/// What an island's cells are drawn for. Dock windows with equal styles share
/// one set of surfaces.
/// </summary>
/// <param name="Scale">DIPs to px.</param>
/// <param name="Vertical">The dock stands on the left or right edge.</param>
/// <param name="Thickness">Px a cell gets across the dock, which is also the grid cell's side (<see cref="Cell"/>).</param>
/// <param name="Icon">The configured icon size in px.</param>
/// <param name="Look">Theme, accent and text scale.</param>
public readonly record struct IslandStyle(float Scale, bool Vertical, float Thickness, float Icon, Look Look)
{
    /// <summary>
    /// Theme, accent, text scale and the text's colour (§396, <see cref="Look.Ink"/>), without the colours somebody
    /// chose for the bar (<see cref="Look.Colors"/>): no widget draws those itself — the bar, its cards and its lines
    /// are the scene's — so a colour being picked recolours the bar and redraws no widget.
    /// </summary>
    public Look Look { get; init; } = Look with { Colors = default };

    /// <summary>
    /// The display the widget is drawn for (its HMONITOR), for the one kind whose drawing depends on
    /// it — the brightness sets the display its bar is on — and 0 for every other, so a layout shown on
    /// two displays keeps drawing each of their surfaces once (Layout.StyleFor).
    /// </summary>
    public nint Screen { get; init; }

    /// <summary>
    /// The bar is a dock's panel (<c>Layout.Connected</c>): its widgets stand flush in one
    /// square band and none has a card's corners, so a picture reaching a widget's edges keeps
    /// square ones (<c>GraphBackdrop.BarRadius</c>). False everywhere else — an islands bar, a
    /// folder's card, a preview.
    /// </summary>
    public bool Square { get; init; }

    /// <summary>
    /// §648: the corners of the plate the bar stands the widget's cell on and lights it in (<c>Layout.PlateRadius</c>): what a
    /// drawing filling its whole cell rounds by, so it is that plate exactly — a switch's accent while it is on. 0 where nobody
    /// said: square.
    /// </summary>
    public float PlateRadius { get; init; }

    /// <summary>
    /// The edge the bar stands on (<c>Layout.StyleFor</c>), for what points the way a flyout opens
    /// from it — the status icons' arrow (<see cref="SystemGlyph.Chevron"/>). The bottom where nobody
    /// said, and its size never depends on it.
    /// </summary>
    public Edge Edge { get; init; }

    /// <summary>
    /// The widget grid's cell in px (<see cref="Layout.CellPx"/>): the square a
    /// widget is measured in, across the dock and along it. The thickness a
    /// cell gets across the dock is that side, so the two are the same number.
    /// </summary>
    public float Cell => Thickness;

    // The one place a width is kept, so the two ways of saying it below cannot
    // disagree. Its default — which is what the struct's own default value gives
    // — reads as any width: a style built without saying means nobody has said,
    // not a widget of no cells.
    private readonly WidgetBounds room;

    /// <summary>
    /// The cells this widget may be given: the kind's measurement with whatever the person
    /// narrowed it to (<see cref="WidgetBounds"/>). The island is given the width its content
    /// works out to, held inside this.
    /// </summary>
    public WidgetBounds Room
    {
        get => room.Most >= WidgetItem.MinWidth ? room : WidgetBounds.Any;
        init => room = value;
    }

    /// <summary>
    /// The width someone chose for this widget, in cells, or
    /// <see cref="WidgetItem.AutomaticWidth"/> for the width its content works out
    /// to. A chosen width wins: the island is given exactly that many cells and
    /// shows what fits in them.
    /// <para>
    /// The same thing <see cref="Room"/> says, said as one number: setting it is setting a range
    /// with its ends together, and reading it is reading a range back where its ends meet. It is
    /// kept because most callers have a width and not a range, and because every question the
    /// islands ask of it — is this widget pinned to one cell, how much room did a chosen width
    /// give me — is a question about that number.
    /// </para>
    /// </summary>
    public int Width
    {
        get => Room.IsFixed && Room.Least >= WidgetItem.MinWidth ? Room.Least : WidgetItem.AutomaticWidth;
        init => room = WidgetBounds.Chosen(value);
    }

    /// <summary>How many whole cells content <paramref name="px"/> px long needs, one at least.</summary>
    public int Cells(float px) => Layout.CellsFor(px, Thickness);

    /// <summary>What those cells measure along the dock, in px: content rounded up to the grid.</summary>
    public float Span(float px) => Layout.CellSpan(px, Thickness);

    /// <summary>Whether content <paramref name="px"/> px long fits in <paramref name="cells"/> cells.</summary>
    public bool FitsIn(float px, int cells) => Cells(px) <= cells;
}

/// <summary>
/// How many cells an island has, and how long each is along the dock, in px.
/// The lengths are what the content needs; the layout holds each to one grid cell at the least
/// (<see cref="Layout.AllotCells"/>) unless they are <c>Exact</c>. An island is always one cell
/// across the dock.
/// </summary>
/// <param name="CellLength">The length of every cell, or of the longest when they differ.</param>
/// <param name="Lengths">
/// Each cell's own length, for an island whose cells differ (the now-playing
/// info beside its buttons); null when every cell is <paramref name="CellLength"/> long.
/// </param>
/// <param name="Exact">
/// The lengths are exactly the widget's content in whole px, off the grid (<see cref="WidgetShape.Exact"/>:
/// a folder's button, its faces and its ends). The layout takes them as they are: rounding a lone
/// glyph's button up to a cell is what stood it in room it did not need (docs\widgets.md §275).
/// </param>
public readonly record struct IslandCells(int Count, float CellLength, IReadOnlyList<float>? Lengths = null, bool Exact = false);

/// <summary>
/// One layer of a cell. The scene stacks a cell's layers bottom to top. Each
/// shows the rectangle <paramref name="Offset"/> px from the cell's top-left and
/// <paramref name="Size"/> px large, and nothing outside it: the scene clips the
/// layer to that rectangle. The surface is drawn 1:1 with its top-left at
/// <paramref name="Offset"/>, and may be larger than <paramref name="Size"/> (a
/// scrolling graph is one sample step wider).
/// </summary>
/// <param name="Scroll">
/// When set, the surface moves left by this property set's scalar "x" inside
/// the layer's rectangle, along the cell's own x axis also on a vertical dock
/// (for example with an expression animation "-scroll.x" on the offset of a
/// sprite under a clipping container). The island animates it, so a graph
/// glides between samples without this process drawing a frame.
/// </param>
public sealed record IslandLayer(CompositionDrawingSurface Surface, Vector2 Offset, Vector2 Size, CompositionPropertySet? Scroll);

/// <summary>A flyout of the dock's own that a click on a cell opens there.</summary>
public enum IslandView
{
    None,

    /// <summary>A folder's list of widgets (FolderFlyout), the folder island's own.</summary>
    Folder,

    /// <summary>pins: an app was clicked (a pin in a folder); the dock follows the one rule (<see cref="AppClicks"/>).</summary>
    App,

    /// <summary>
    /// folders: the widget's own menu, at the pointer, over the open folder, as its right click opens
    /// it — the now-playing line's picker of the apps playing, on its app's name (MediaLine.cs). Only a
    /// line in an open folder asks for it, and the folder alone answers (FolderFlyout.Activate).
    /// </summary>
    Menu,

    /// <summary>
    /// desktops: a desktop chosen in the desktops' line (DesktopsLine): the folder goes without
    /// giving the focus back, and the dock switches to it (Dock.Folders.cs, FolderForward). It was
    /// the desktop switcher's flyout until the switcher became the desktops preset (2026-10-11).
    /// </summary>
    Desktops,

    /// <summary>
    /// layouts: a layout chosen in the layouts' line (LayoutsLine): the folder goes, and the dock
    /// switches this display to it (Dock.Folders.cs, FolderForward) — the bar's own menu's act. It
    /// was the layout switcher's flyout until the switcher became the layouts preset (2026-10-11).
    /// </summary>
    Layouts,

    /// <summary>
    /// folders: a line's ">" (Karpo.Core\FolderPages.cs): its page in place of its column, as Windows'
    /// quick settings tiles open theirs — not a popup, so the folder rule allows it (§15). Only a line
    /// in an open folder at rest asks for it, and the folder alone answers (FolderFlyout.Activate).
    /// </summary>
    Page,

    /// <summary>
    /// folders: the footer's Edit, Windows' pencil: the folder is arranged where it stands, its bar's
    /// edit mode on (FolderFlyout.ArrangeFromDrag's road). Only a line in an open folder asks for it.
    /// </summary>
    Arrange,

    /// <summary>
    /// folders: a line's dropdown (the sound page's Output and Input, SoundPartTile): its list as a
    /// menu under its field, over the open folder, which stays open behind it. Only a line in an open
    /// folder asks for it, and the folder alone answers (FolderFlyout.Activate, Dock.FolderChoices).
    /// </summary>
    Choices,
}

/// <summary>
/// How big the room a widget is drawn in away from the bar is, from the cells it is said to cover:
/// a list's line is one (<see cref="Small"/>), a pane of Karpo's clock four by several
/// (<see cref="Large"/>). It decides the room at the edge (<see cref="TileStyle.Pad"/>). The grid
/// folder's cards, which came in every shape, went on 2026-10-09.
/// </summary>
public enum TileShape
{
    /// <summary>One cell: a list's line.</summary>
    Small,

    /// <summary>Two cells or more across, one down.</summary>
    Wide,

    /// <summary>One cell across, two or more down.</summary>
    Tall,

    /// <summary>Two by two or more: a pane of Karpo's clock.</summary>
    Large,
}

/// <summary>How a widget is drawn away from the bar (see <see cref="CellIsland.PaintOn"/>).</summary>
/// <param name="Scale">DIPs to px.</param>
/// <param name="Look">Theme, accent and text scale.</param>
/// <param name="Theme">The folder's colour tokens (text, secondary text, accent, cards…).</param>
/// <param name="Columns">How many cells the room covers across (<see cref="TileShape"/>): one for a list's line.</param>
/// <param name="Rows">How many it covers down.</param>
public readonly record struct TileStyle(float Scale, Look Look, FlyoutTheme Theme, int Columns, int Rows)
{
    /// <summary>The display the folder opened on (its HMONITOR), which the brightness sets; 0 where no folder says.</summary>
    public nint Screen { get; init; }

    public TileShape Shape => (Columns, Rows) switch
    {
        ( <= 1, <= 1) => TileShape.Small,
        (_, <= 1) => TileShape.Wide,
        ( <= 1, _) => TileShape.Tall,
        _ => TileShape.Large,
    };

    /// <summary>The Look's text scale, 1 to 2.25.</summary>
    public float TextScale => Math.Clamp(Look.TextScale > 0 ? Look.TextScale : 1, 1, 2.25f);

    /// <summary><paramref name="dip"/> DIPs in whole px.</summary>
    public float Px(float dip) => MathF.Round(dip * Scale, MidpointRounding.AwayFromZero);

    /// <summary>A text size of <paramref name="dip"/> DIPs, grown with the Look's text scale.</summary>
    public float Text(float dip) => dip * Scale * TextScale;

    /// <summary>Between the room's edge and its content: less on a line than on a pane.</summary>
    public float Pad => Px(Shape == TileShape.Small ? 10 : 14);

    /// <summary>
    /// Between two things on a card: a glyph and the text beside it, a heading and the block under
    /// it, one row and the next. Eight DIP, and one number rather than two.
    /// <para>
    /// It was written at each site, 8 in sixteen places and 6 in fourteen, and the first reading of
    /// that was that they were two quantities — the pair the bar already names, a gap between a
    /// glyph and its text (<see cref="Fit.Gap"/>) and a leading between two lines
    /// (<see cref="Fit.Leading"/>). The full count killed it: a drive's card put 6 beside a glyph
    /// where the weather's put 8, and the notifications' card put 8 between rows where the
    /// calendar's put 6. <b>The bar's distinction between a gap and a leading is real; the card
    /// painters were not making it.</b> They were guessing, and a quantity guessed twice is one
    /// quantity with no home.
    /// </para>
    /// <para>
    /// Eight rather than six on two grounds, neither of them counting: the project's own control
    /// layer already names this quantity — a WinUI button puts 8 between its glyph and its text —
    /// and 8 sits on the four-DIP grid Windows spaces on, where 6 does not.
    /// </para>
    /// </summary>
    public float Gap => Px(8);

    /// <summary>
    /// The card template's caption: the small line that says what the card is — "Local time", the
    /// place a forecast is for, the weekday over a date, a drive's name, "On battery". Every card
    /// draws it at this size, so the room a card needs is measured from it rather than guessed
    /// (<see cref="TilePaint.CardRoom"/>).
    /// </summary>
    public float CaptionPx => Text(12);

    /// <summary>
    /// The card template's value line at its smallest: what a card's big number is shrunk to
    /// before it is cut (<see cref="TilePaint.Fitting"/>'s floor). A card that cannot hold a
    /// caption and one of these is not a card.
    /// </summary>
    public float BodyPx => Text(14);

    /// <summary>
    /// quick: what to draw on the accent — black on the light accent of dark mode, white on the dark
    /// one of light mode, and the system's own pair in high contrast, so a glyph keeps its contrast
    /// whatever the accent is.
    /// </summary>
    public DXGI_RGBA OnAccent => Look.HighContrast
        ? FlyoutTheme.Ink(Look.HcHighlightText)
        : Look.Dark ? new DXGI_RGBA(0, 0, 0, 1) : new DXGI_RGBA(1, 1, 1, 1);
}

/// <summary>
/// quick: an island whose cells take the pointer themselves rather than only a click — the volume
/// slider on the bar. The dock hands it a press inside a cell, the moves while it holds the
/// pointer, the release, and a wheel turn over it (Dock.Quick.cs).
/// </summary>
internal interface ISliderIsland
{
    /// <summary>A press at <paramref name="point"/> in a cell <paramref name="size"/> px large; true when it took the pointer.</summary>
    bool Press(int index, Vector2 point, Vector2 size, in IslandStyle style);

    /// <summary>The pointer moved while it holds it.</summary>
    void Drag(int index, Vector2 point, Vector2 size, in IslandStyle style);

    /// <summary>It lost the pointer: the button went up, or the capture went elsewhere.</summary>
    void Release();

    /// <summary>A wheel turn over one of its cells, on a bar drawn in <paramref name="style"/>; true when it used it.</summary>
    bool Wheel(int delta, in IslandStyle style);
}

/// <summary>
/// Where in a folder a widget is being drawn (docs\widgets.md §2): a line of its list. The taskbar
/// is not one of them — the bar draws a widget's cells (<see cref="CellIsland.PaintCell"/>) and
/// never asks for its card. A grid folder's card was the other surface until the grid went
/// (2026-10-09).
/// </summary>
internal enum WidgetSurface
{
    /// <summary>A row of a folder's list, its content beside the name the list always shows.</summary>
    List,
}

/// <summary>
/// What a click on a cell asks of the dock. The default (no items, no view)
/// means the island handled it. With items, the dock shows them in a flyout over
/// the cell (an island's content: <see cref="FlyoutLayout.Panel"/>, the quick
/// settings' width, unless it says otherwise) and hands a click on one to
/// <see cref="IIsland.Invoke"/>. With a <paramref name="View"/>, the dock opens
/// that flyout of its own at the cell instead.
/// </summary>
/// <param name="Panel">
/// Nothing asks for the settings app's panels any more: the dock's own flyouts
/// (<see cref="IslandView.Desktops"/> and the others) replaced them, and the dock
/// ignores this. It goes once nothing reads it.
/// </param>
/// <param name="Rows">
/// The rows the items' list shows, exactly, scrolling past them (<see cref="Flyout.Rows"/>): a VPN's
/// connections. Null for a list as tall as what it shows.
/// </param>
/// <param name="Icons">
/// The items are icons, shown as the tray's overflow shows its hidden ones: the bar's squares, five
/// to a row, no names, the panel no larger than they are, and a right click on one its own menu
/// (<see cref="IWidgetMenu.MenuCellOf"/>). The status icons' arrow.
/// </param>
public readonly record struct IslandAction(
    IReadOnlyList<FlyoutItem>? Items = null,
    FlyoutLayout Layout = FlyoutLayout.Panel,
    string? Title = null,
    IslandView View = IslandView.None,
    string? Panel = null,
    FlyoutList? Rows = null,
    bool Icons = false);

public interface IIsland : IDisposable
{
    /// <summary>"{owner}/{id}" (<see cref="Config.IslandKey"/>), given at creation and never changed.</summary>
    string Key { get; }

    /// <summary>
    /// What this widget is, wherever it stands (<see cref="WidgetInfo"/>): its name, its glyph, its
    /// options record and the subject a shared drawing takes — a graph's metric, a switch's radio.
    /// Not which taskbar entry, card or line it is: the bar keeps that beside the widget it made
    /// (<see cref="PartSpec.Kind"/>), and a folder beside its cell.
    /// </summary>
    WidgetInfo Info { get; }

    /// <summary>How often <see cref="Tick"/> wants to run, in milliseconds; 0 = never.</summary>
    int IntervalMs { get; }

    /// <summary>
    /// Called once after <see cref="Islands.Create(string, Island, Gfx, Config)"/> with the dock window that
    /// <see cref="Islands.WM_KARPO_ISLAND"/> is posted to, and the <paramref name="token"/> it carries
    /// as wParam, which the dock maps back to this island.
    /// </summary>
    void Attach(nint dock, nuint token);

    /// <summary>
    /// The island with this key, possibly with other options (or in another
    /// group). May change the layout; the dock rebuilds anyway.
    /// </summary>
    void Configure(WidgetItem island, Config config);

    IslandCells Measure(IslandStyle style);

    /// <summary>
    /// Cell <paramref name="index"/>, its length × Thickness px (Thickness × length
    /// when vertical). The island keeps it and paints it again in place on
    /// <see cref="Tick"/>, <see cref="Refresh"/> and <see cref="MinuteChanged"/>,
    /// until <see cref="Forget"/>.
    /// </summary>
    CompositionDrawingSurface Surface(int index, IslandStyle style);

    /// <summary>
    /// Cell <paramref name="index"/> as layers, bottom to top: what the scene shows. An
    /// island that does not scroll gives one layer, its <see cref="Surface"/>.
    /// Kept and redrawn like <see cref="Surface"/>, until <see cref="Forget"/>.
    /// </summary>
    IReadOnlyList<IslandLayer> Layers(int index, IslandStyle style);

    /// <summary>
    /// Whether any dock window shows the island. Hidden, an island stops its
    /// animations, so the compositor has nothing of it to move.
    /// </summary>
    void Visible(bool shown);

    /// <summary>
    /// Samples and redraws the kept surfaces. True when the layout changed (the
    /// number of cells or their length), so the dock rebuilds.
    /// </summary>
    bool Tick(Gfx gfx);

    /// <summary>
    /// Reads everything again now (device change, resume), or takes the result
    /// of the work that posted <see cref="Islands.WM_KARPO_ISLAND"/>. True when the
    /// layout changed. Shared stores raise their Changed event from here.
    /// </summary>
    bool Refresh(Gfx gfx);

    /// <summary>
    /// The dock's clock moved to a new minute. Islands that show the time of
    /// day or a date redraw; true when the layout changed.
    /// </summary>
    bool MinuteChanged(Gfx gfx);

    /// <summary>
    /// A system message the dock forwards to every island:
    /// WM_WTSSESSION_CHANGE (lock, unlock) and WM_POWERBROADCAST (suspend,
    /// resume, a change of the power status).
    /// </summary>
    void System(uint msg, WPARAM wParam, LPARAM lParam);

    /// <summary>The hover text for a cell; may have several lines.</summary>
    string? Tooltip(int index);

    IslandAction Click(int index);

    /// <summary>
    /// A flyout item from <see cref="Click"/> was clicked. The island acts on it
    /// itself; the dock does nothing more.
    /// </summary>
    void Invoke(int index, uint id);

    /// <summary>Drops the kept surfaces: what it shows changed in a way only it knows (its settings, edit mode).</summary>
    void Forget();

    /// <summary>
    /// §359: the dock begins a rebuild: the surfaces it asks for in it are kept as they are, drawn again only
    /// where what they show changed (each is redrawn as it changes, Tick, Refresh) — a rebuild that changed
    /// nothing of the island draws nothing.
    /// </summary>
    void Rebuilding()
    {
    }

    /// <summary>§359: the rebuild is over: the styles it asked nothing of are set aside, closed as the next ends.</summary>
    void Rebuilt()
    {
    }
}

public static class Islands
{
    /// <summary>
    /// Posted to the attached dock window when an island (or a store through
    /// it) finished work off the dock's thread; wParam is the token given to
    /// <see cref="IIsland.Attach(nint, nuint)"/>. The dock answers with
    /// <see cref="IIsland.Refresh"/> on that island.
    /// </summary>
    public const uint WM_KARPO_ISLAND = 0x8000 + 5;

    /// <summary>§658: the islands (their dock window, their token) with a <see cref="WM_KARPO_ISLAND"/> posted and not yet taken.</summary>
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(nint Dock, nuint Token), byte> Waiting = new();

    /// <summary>
    /// §658: tells <paramref name="dock"/>, from any thread, that island <paramref name="token"/> has news: one message waits
    /// for an island at a time, however much news comes before the dock takes it (<see cref="Taken"/>), and the one refresh
    /// reads it all. A message per piece of news flooded the dock's queue — posted messages go before the keys and the
    /// mouse — and each was a whole redraw: Windows search's first icons froze the dock. False where it could not be posted.
    /// </summary>
    internal static unsafe bool Post(nint dock, nuint token)
    {
        if (!Waiting.TryAdd((dock, token), 0))
        {
            return true;
        }
        if (TerraFX.Interop.Windows.Windows.PostMessageW((TerraFX.Interop.Windows.HWND)(void*)dock, WM_KARPO_ISLAND, token, default))
        {
            return true;
        }
        _ = Waiting.TryRemove((dock, token), out _);
        return false;
    }

    /// <summary>§658: the dock took island <paramref name="token"/>'s message: news after this posts again. Before the refresh, so none is missed.</summary>
    internal static void Taken(nint dock, nuint token) => Waiting.TryRemove((dock, token), out _);

    // --- The factories ------------------------------------------------------------------
    //
    // One per registry: the taskbar's and a list folder's. Each reads its own
    // descriptor — which drawing, whether a card is all it has, what a press on it may be — and
    // tells the widget it made where it stands (CellIsland.StandOn, StandIn). What class draws a
    // drawing is one table below, shared, so a drawing that serves all three (a graph, a switch,
    // Security) is one class wherever it stands and a new widget is one registry record.

    /// <summary>
    /// A taskbar entry's widget, keyed <paramref name="key"/> ("{owner}/{id}"): what its entry
    /// names (<see cref="BarEntry.Drawing"/>). Null for an entry the scene draws itself — Windows'
    /// own parts (Start, the apps, the clock…) — and for a widget no taskbar takes.
    /// </summary>
    public static IIsland? Create(string key, BarItem island, Gfx gfx, Config config) => Create(key, island, gfx, config, null);

    /// <param name="countdowns">
    /// The dock's countdowns, which a timer, a focus session and a folder that holds them run on;
    /// null for a store of each one's own.
    /// </param>
    internal static IIsland? Create(string key, BarItem island, Gfx gfx, Config config, Countdowns? countdowns)
    {
        if (BarEntries.Find(island.Kind) is not { } entry)
        {
            return null;
        }
        var made = Make(entry.Drawing, key, island, gfx, config, countdowns);
        (made as CellIsland)?.StandOn(entry);
        return made;
    }

    /// <summary>
    /// A line of a list folder, keyed <paramref name="key"/> ("{owner}/{folder id}/{id}"): what
    /// <paramref name="kind"/>'s line names (<see cref="ListWidget.Drawing"/>) — a piece of a
    /// panel drawn as its card, or the widget whose click the list's own row carries.
    /// </summary>
    internal static IIsland? CreateLine(string key, CellItem island, ListKind kind, Gfx gfx, Config config, Countdowns? countdowns = null)
    {
        if (ListWidgets.Find(kind) is not { } line)
        {
            return null;
        }
        var made = Make(line.Drawing, key, island, gfx, config, countdowns);
        (made as CellIsland)?.StandIn(line);
        return made;
    }

    /// <summary>
    /// The one place a widget is made: which class draws each drawing. Which drawing a widget is,
    /// is its descriptor's answer (<see cref="BarEntry.Drawing"/>, <see cref="ListWidget.Drawing"/>), so the taskbar and a folder cannot disagree about a class
    /// and a new widget cannot be left out of one of them. It used to be two switches on the one
    /// kind every widget had (<c>Section</c>), and the list folder had fallen out of the first: a folder the
    /// catalogue offers, and it had no island on the bar at all.
    /// </summary>
    private static IIsland? Make(WidgetSource source, string key, WidgetItem island, Gfx gfx, Config config, Countdowns? countdowns)
    {
        // §635: the add page's ghost and everything inside it made showing the samples, not the person's own data.
        using var samples = Samples.Show(key);
        return Of(source, key, island, gfx, config, countdowns);
    }

    private static IIsland? Of(WidgetSource source, string key, WidgetItem island, Gfx gfx, Config config, Countdowns? countdowns) => source switch
    {
        // The scene draws it: Start, search, the apps, the tray, the clock, the bar's own strips.
        WidgetSource.None => null,
        // drives: one widget per drive, and the line that lists the removable devices.
        WidgetSource.Drive => new DriveIsland(key, island, gfx, config),
        WidgetSource.Removable => new RemovableIsland(key, island, gfx, config),
        // markets: a share, a rate or a coin, priced through MarketStore (§246).
        WidgetSource.Markets => new MarketIsland(key, island, gfx, config),
        // rss: one feed's headlines, read through RssStore.
        WidgetSource.Rss => new Rss.RssIsland(key, island, gfx, config),
        // calendar: the calendar's agenda line; on the bar the date is a folder's calendar face.
        WidgetSource.Calendar => new CalendarIsland(key, island, gfx, config),
        WidgetSource.Media => new MediaIsland(key, island, gfx, config),
        WidgetSource.Battery => new BatteryIsland(key, island, gfx, config),
        // quick: the volume slider on the bar, and the one-button widgets.
        WidgetSource.Volume => new VolumeIsland(key, island, gfx, config),
        WidgetSource.Brightness => new BrightnessIsland(key, island, gfx, config),
        WidgetSource.Switch => new SwitchIsland(key, island, gfx, config),
        // Whether this PC is protected, and a click to Windows Security.
        WidgetSource.Security => new SecurityIsland(key, island, gfx, config),
        // The status icons beside the tray, a cell each while it is so.
        WidgetSource.Indicators => new IndicatorsIsland(key, island, gfx, config),
        // pim: the contacts, a search box over them, and the inbox, as lines of a list.
        WidgetSource.Contacts => new Pim.ContactsLine(key, island, gfx, config),
        // §642: Windows search, a search box over the index, the apps, Settings and answers, as a line of a list.
        WidgetSource.WinSearch => new WinSearch.WinSearchLine(key, island, gfx, config),
        WidgetSource.Mail => new Pim.MailIsland(key, island, gfx, config),
        // layouts: the taskbar layouts, as a line of a list.
        WidgetSource.Layouts => new LayoutsLine(key, island, gfx, config),
        // folders: the list folder's button and its list.
        WidgetSource.Folder => new FolderIsland(key, island, gfx, config, countdowns),
        // §474: Windows' own widgets' faces, drawn as a folder's are, fixed and holding nothing (WindowsFaces).
        WidgetSource.WindowsFaces => new FolderIsland(key, island, gfx, config, countdowns),
        // Only a folder asks for these: the two kinds the scene draws on the bar itself.
        WidgetSource.ClockTile => new ClockTile(key, island, gfx, config),
        // calendar: a timer and a focus session, each on a countdown of its own in the dock's.
        WidgetSource.Countdown => new CountdownTile(key, island, gfx, config, countdowns),
        // calendar: a stopwatch, on a stopwatch of its own in the dock's countdowns.
        WidgetSource.Stopwatch => new StopwatchTile(key, island, gfx, config, countdowns),
        // alarm: an alarm, on the dock's alarm book in its countdowns.
        WidgetSource.Alarm => new AlarmTile(key, island, gfx, config, countdowns),
        // weather: the weather panel's header, the conditions now, as a line of a list.
        WidgetSource.WeatherPart => new Weather.WeatherPartTile(key, island, gfx, config),
        // sound: the sound panel's output, input and mixer, drawn by the page's own controls.
        WidgetSource.SoundPart => new SoundPartTile(key, island, gfx, config),
        // folders: the notification panel's list, as a line of a list.
        WidgetSource.NotificationPart => new NotificationPartTile(key, island, gfx, config),
        // pins: a pin, which the bar draws as one of its app buttons.
        WidgetSource.PinTile => new PinTile(key, island, gfx, config),
        // folder contents: what one folder on this PC holds, as a line of a list (§380).
        WidgetSource.FolderContents => new FolderContentsLine(key, island, gfx, config),
        // Task view as a folder's line; the bar draws its button in the scene.
        WidgetSource.TaskView => new TaskViewTile(key, island, gfx, config),
        // The Snipping Tool and Settings as a folder's line; the bar draws their
        // buttons in the scene.
        WidgetSource.WindowsShortcut => new ShortcutTile(key, island, gfx, config),
        // power: a command of Windows' power menu as a folder's line.
        WidgetSource.PowerButton => new PowerButtonTile(key, island, gfx, config),
        // clipboard: what was copied lately, as a line of a list.
        WidgetSource.Clipboard => new ClipboardLine(key, island, gfx, config),
        // desktops: the virtual desktops, as a line of a list.
        WidgetSource.Desktops => new DesktopsLine(key, island, gfx, config),
        // perf: the five live graphs, one class for all of them; which reading it takes is its kind.
        WidgetSource.Performance => new PerformanceIsland(key, island, gfx, config),
        // perf: the processes busiest on the processor now, a row per app.
        WidgetSource.Processes => new ProcessesLine(key, island, gfx, config),
        // A kind in its registry before its drawing is built: its glyph and its title.
        WidgetSource.Shell => new ShellIsland(key, island, gfx, config),
        // list: a title over the lines under it.
        WidgetSource.Heading => new HeadingTile(key, island, gfx, config),
        // list: a line across the list, where somebody put one.
        WidgetSource.Divider => new DividerTile(key, island, gfx, config),
        // list: a row of toggles, a flyout's footer, a month, and the clock flyout's header.
        WidgetSource.Toggles => new TogglesTile(key, island, gfx, config),
        WidgetSource.Actions => new ActionsTile(key, island, gfx, config),
        WidgetSource.Month => new MonthTile(key, island, gfx, config),
        WidgetSource.TimeHeader => new TimeHeaderTile(key, island, gfx, config),
        // weather, §590: the hourly forecast's graph and list, the daily forecast's list and graph.
        WidgetSource.WeatherHours => new Weather.WeatherHoursTile(key, island, gfx, config),
        WidgetSource.WeatherHoursList => new Weather.WeatherHoursListTile(key, island, gfx, config),
        WidgetSource.WeatherDays => new Weather.WeatherDaysTile(key, island, gfx, config),
        WidgetSource.WeatherDaysGraph => new Weather.WeatherDaysGraphTile(key, island, gfx, config),
        // air: the air quality for a place, its index, pollutants, pollen or day, a kind each (§210, §590).
        WidgetSource.AirQuality => new AirQualityTile(key, island, gfx, config),
        // tides: the tides for a place, the sea now, the day's curve and the next turns (§236).
        WidgetSource.Tides => new TidesTile(key, island, gfx, config),
        // sports: a team's live score or next match, and its matches around it (§239).
        WidgetSource.Sports => new Sports.SportsTile(key, island, gfx, config),
        // quakes: the earthquakes near a place, a row each (§240).
        WidgetSource.Quakes => new QuakesLine(key, island, gfx, config),
        // sun: the sun's day for a place, its arc, day bar or times, worked out on this PC (§368).
        WidgetSource.Sun => new SunTile(key, island, gfx, config),
        // camera: a live webcam's picture (§331).
        WidgetSource.Camera => new CameraLine(key, island, gfx, config),
        // currency: a converter, two amounts typed either way at its pair's rate (§242).
        // calendar: the calendar flyout's header, its day's events and its day, week and year views.
        WidgetSource.DateHeader => new DateHeaderTile(key, island, gfx, config),
        WidgetSource.Events => new EventsTile(key, island, gfx, config),
        WidgetSource.CalendarView => new CalendarViewTile(key, island, gfx, config),
        // calendar: the next meetings, today's still to come with their Join (§205).
        WidgetSource.Meeting => new MeetingTile(key, island, gfx, config),
        // screentime: the day's total and its graph, or its apps, as lines of a list.
        WidgetSource.ScreenTime => new ScreenTimeLine(key, island, gfx, config),
        WidgetSource.ScreenTimeChart => new ScreenTimeDonut(key, island, gfx, config),
        // notes: a to-do list and a note of the notebook's, each a line of a list.
        WidgetSource.Todo => new TodoTile(key, island, gfx, config),
        WidgetSource.Note => new NoteTile(key, island, gfx, config),
        // quick: the network's and Bluetooth's quick-settings pages, as lines of a list.
        WidgetSource.QuickPage => new QuickPageTile(key, island, gfx, config),
        // §641: Start's pinned apps and every app, Recommended, and its account and power.
        WidgetSource.StartApps => new StartWidgets.StartAppsLine(key, island, gfx, config),
        WidgetSource.StartRecommended => new StartWidgets.StartRecommendedLine(key, island, gfx, config),
        WidgetSource.StartProfile => new StartWidgets.StartProfileLine(key, island, gfx, config),
        // A source named in the registry that nothing here builds. Loud, because the quiet
        // version of this — a kind falling through to null — is the bug this method replaced.
        _ => throw new ArgumentOutOfRangeException(nameof(source), source, $"{island.Info?.Id} names a widget source nothing builds."),
    };

    /// <summary>
    /// The owner part of a key: "layout:&lt;id&gt;" for an island of a layout (or a widget of one of
    /// its folders), a display's id for a part the dock places itself; null for a key without one.
    /// </summary>
    internal static string? OwnerOf(string key)
    {
        var cut = key.IndexOf('/');
        return cut <= 0 ? null : key[..cut];
    }

    /// <summary>The id part of a key: the island's id, or "&lt;folder id&gt;/&lt;id&gt;" for a widget in a folder (as the settings app finds it).</summary>
    internal static string IdOf(string key)
    {
        var cut = key.IndexOf('/');
        return cut < 0 ? key : key[(cut + 1)..];
    }
}

