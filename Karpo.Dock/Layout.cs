// Where everything sits, in physical pixels and window coordinates.
//
// The dock covers one whole edge of its display, flush with it, like the
// taskbar. Two axes describe it: the main axis runs along the edge (x on the
// top and bottom edges, y on the left and right ones) and the cross axis
// points away from the edge. The bar shows the islands the person placed on
// it (PartSpec) in three sections (BarSection), each in its own order and each
// laid out on its own: the start's run from the bar's start, the end's run to
// its end, and the middle's is centred on the bar. A size change in one section
// moves nothing in the others: the middle stays exactly centred as long as it
// fits between them, then moves as little as keeps it clear of them. Everything
// is centred across the bar, on one centre line. The window is the bar plus
// room on its inner side for tooltips.
//
// The proportions are the Windows 11 taskbar's, measured on its own window at
// 150 % (see the table at the top of Scene.cs): on a 48-DIP bar with 24-DIP
// icons, 44-DIP app buttons with a 40-DIP highlight, a 45-DIP Start button,
// 32-DIP tray cells and a 12-DIP
// show-desktop sliver, all side by side with no
// gaps. The widgets have since moved onto the cell grid below, so only the
// taskbar's own buttons still land on exactly the pixels Explorer uses.
//
// The bar can be any height from Config.MinHeight to Config.MaxHeight, and the
// icon size can follow it. The app buttons scale with the icon — the measured
// DIPs are for a 24-DIP icon — and the margins with the bar. Everything else
// (text, the tray, the desktops) grows with the square root of the
// bar's height, so a tall bar has large app icons without its tray taking the
// room they need. A thin bar keeps what must stay legible: tray glyphs never
// go below 16 DIPs, text never
// below 11, and a highlight keeps 2 DIPs from the bar's edges. The bar's inner
// edge is a grip for dragging its height.
//
// Widgets stand on a grid of squares. One cell is a square whose side is the
// bar's usable thickness — the bar less the breathing room it keeps at each
// edge, which is the height every highlight on it already has — so a widget
// is always one cell across the bar and a whole number of cells along it:
// 1x1, 1x2, 1x3. A vertical bar is the same rule turned a quarter: one cell
// across its width, a whole number down. Content that needs part of a cell
// rounds up, because a part-cell gap has nothing to put in it and would
// break the grid; a widget that cannot fit its content in the cells it asked
// for shortens the content rather than overflowing. That one square is also
// a list folder's row (FolderFlyout), so a widget keeps its proportions
// wherever it is shown. The room between widgets is unchanged, so the bar
// still spaces like Windows 11's. See Cell, CellsFor and CellSpan: the rule
// lives there and everything else asks them.
//
// Parts are sized by their content, rounded up to whole cells: islands cell
// by cell as the island says. The taskbar's own rows of
// icons — the apps, the pins, the tray — keep their
// icon-sized steps instead, as Windows does, and so are naturally a row of
// buttons rather than a widget.
// When the sections run out of room, the middle first moves off the centre as
// little as keeps it clear of the start's and the end's; then the app buttons
// fold into "…"; then whole islands give way, the last placed first; past that
// the sections run one after the other from the start and the tail falls off
// the end.
//
// In edit mode every part is a block the person can drag: an empty part keeps
// a small placeholder, each block has a remove badge, and DropSlot and Arrange
// say where a dragged block would go — into which section, and where in it —
// and where the others move to make room. In edit mode an empty section keeps
// room of its own (EmptyRooms), three cells long, which is where a block is
// dropped into it; at rest it takes no room at all. Every section has a "+" at its
// inner ends in edit mode, in room of its own beside its widgets, and an empty one
// a "+" alone in its room (Adds, AddEnds). InsertionRect draws the line that says
// where the one being added will land.
//
// Nothing grows or moves under the pointer, so these rest positions are the
// only positions: hit-testing, highlights and tooltips all read the same
// rectangles, and a point inside ItemRect(hit) hit-tests back to that hit,
// except in the grip band, which is the grip's.
//
// Every length is a whole number of pixels, so part edges never fall between
// two pixels. Rounding is half away from zero, as Rust's `f32::round` is, and
// as XAML's layout rounding does for these sizes: MathF.Round on its own
// rounds halves to even, which puts a 1.5-px edge where Windows doesn't.

using System.Numerics;
using Karpo.Core;
using TerraFX.Interop.Windows;

namespace Karpo.Dock;

/// <summary>A rectangle in window pixels.</summary>
public readonly record struct RectF(float X, float Y, float Width, float Height)
{
    public float Right => X + Width;

    public float Bottom => Y + Height;

    public Vector2 Center => new(X + Width / 2f, Y + Height / 2f);

    public Vector2 Size => new(Width, Height);

    public bool IsEmpty => Width <= 0f || Height <= 0f;

    public bool Contains(float x, float y) => x >= X && x < X + Width && y >= Y && y < Y + Height;
}

/// <summary>
/// edit: a bar as it would stand with a carried block landed in it, or taken off it: the room the
/// block takes where it lands (<see cref="Layout.BlockRect"/>; empty where it does not), where every
/// part of the bar would start along it, by part, and the sections' empty rooms and a floating bar's
/// docks then (<see cref="Layout.SectionPanels()"/>, window px).
/// </summary>
public sealed record Landed(RectF Rect, float[] Lefts, IReadOnlyList<RectF> Rooms, IReadOnlyList<RectF> Panels);

/// <summary>edit: which end of its section a "+" stands at (<see cref="Layout.AddEnds"/>), along the bar; or alone in an empty one's room.</summary>
public enum AddEnd
{
    Before,
    After,
    Alone,
}

/// <summary>
/// edit: one "+" of a bar (<see cref="Layout.Adds"/>): its section, the end of it it stands at, where a
/// widget it adds goes (<paramref name="Slot"/>, counted as <see cref="Layout.DropSlot"/> counts), and
/// where it stands in window px.
/// </summary>
public readonly record struct BarAdd(BarSection Section, AddEnd End, BarSlot Slot, RectF Rect);

/// <summary>One island placed on a bar, as the layout takes it.</summary>
/// <param name="Key">"{owner}/{id}" (see <see cref="Config.IslandKey"/>); unique on the bar.</param>
/// <param name="Room">
/// How many cells of the widget grid (<see cref="Layout.CellPx"/>) the widget may be given: its
/// kind's fewest and most (<see cref="WidgetBounds.For(WidgetItem)"/>), since nobody sets a width
/// on the bar. The widget is given the width its content works out to, held inside this — so a
/// range with its ends together is one fixed width and wins outright, and between two ends the
/// content decides.
/// </param>

public readonly record struct PartSpec(string Key, BarKind Kind, WidgetBounds Room)
{
    /// <summary>A part with the widest range there is: as wide as its content works out to.</summary>
    public PartSpec(string Key, BarKind Kind) : this(Key, Kind, WidgetBounds.Any)
    {
    }

    /// <summary>A part at a width said as one number, <see cref="WidgetItem.AutomaticWidth"/> for none.</summary>
    public PartSpec(string Key, BarKind Kind, int Cells) : this(Key, Kind, WidgetBounds.Chosen(Cells))
    {
    }

    /// <summary>
    /// The one width the range pins the widget to, or <see cref="WidgetItem.AutomaticWidth"/> where its
    /// ends differ and the content decides: the range read as the one number the bar asked for
    /// before ranges, for the places that still ask that question — is this widget pinned to a
    /// width, and which.
    /// </summary>
    public int Cells => Room.IsFixed && Room.Least >= WidgetItem.MinWidth ? Room.Least : WidgetItem.AutomaticWidth;

    /// <summary>
    /// Whether the part shows nothing on this PC: an input button where Windows would not show it —
    /// the pen menu with no pen, the virtual touchpad with no touch screen — and nobody set it to
    /// Always (<see cref="InputButtons"/>). It takes no room then, and edit mode shows its placeholder.
    /// </summary>
    public bool Absent { get; init; }

    /// <summary>
    /// search: whether the part is asked to be Windows 11's search box rather than the search icon —
    /// Windows' search button set to the box (<see cref="WindowsSearchOptions"/>). The bar shows it
    /// where it has room for one (<see cref="SearchPill.Fits"/>).
    /// </summary>
    public bool Box { get; init; }

    /// <summary>
    /// §302: whether the part draws no icon at rest — Show desktop as Windows 11's bare corner
    /// (<see cref="ShowDesktopOptions"/>): its room and its hit as ever, lit under the pointer, its icon
    /// greyed while the bar is arranged.
    /// </summary>
    public bool Bare { get; init; }

    /// <summary>
    /// Which of the bar's three sections the part stands in (<see cref="BarSection"/>): the start's
    /// run from the bar's start, the end's to its end, the middle's centred on it, each laid out on
    /// its own (<see cref="Layout"/>).
    /// </summary>
    public BarSection Section { get; init; }

    /// <summary>
    /// The part <paramref name="island"/> is on a bar, keyed <paramref name="key"/>: its kind's range,
    /// and whether an input button shows on a PC with
    /// <paramref name="inputs"/> (this one where it is left out). The one place an island becomes a
    /// part, so the dock and anything that wants to lay a bar out the way the dock does — a test
    /// above all — take the same branch: a part built by hand with another range is laid out to
    /// other inputs than the dock's.
    /// </summary>
    public static PartSpec For(string key, BarItem island, InputHardware? inputs = null) =>
        new(key, island.Kind, WidgetBounds.For(island))
        {
            Section = Sections.Known(island.Section),
            Absent = InputButtons.Is(island.Kind) && !InputButtons.Shows(island, inputs ?? InputHardware.Read()),
            Box = island.Kind == BarKind.Search && island.WindowsSearch?.ShowBox == true,
            Bare = island.Kind == BarKind.ShowDesktop && island.ShowDesktop?.Style == ShowDesktopStyle.Corner,
        };
}

/// <summary>
/// One placed island's stretch of the dock. <see cref="Left"/> and <see cref="Width"/>
/// are along the main axis: x and width on a horizontal dock, y and height on a
/// vertical one. A part with nothing to show, or one that gave way for room,
/// is 0 long.
/// </summary>
/// <param name="Count">Tiles, desktops (plus the add button), visible tray icons, system glyphs or island cells.</param>
public readonly record struct Part(string Key, BarKind Kind, float Left, float Width, int Count)
{
    public float Right => Left + Width;

    public bool Contains(float x) => Width > 0f && x >= Left && x < Right;
}

public enum HitKind
{
    Tile,
    Tray,

    /// <summary>The bar itself, between and around the things on it.</summary>
    Panel,

    /// <summary>The Windows button.</summary>
    Start,

    /// <summary>A cell of the island in <see cref="Hit.Part"/>.</summary>
    Island,

    Search,
    TaskView,

    /// <summary>
    /// One of Windows' buttons that came after those three — the widgets board, the pen
    /// menu, the touch keyboard, the virtual touchpad (<see cref="BarEntries.IsWindowsButton"/>) —
    /// which one in <see cref="Hit.Index"/>, its <see cref="BarKind"/>.
    /// </summary>
    WindowsButton,

    /// <summary>The chevron that shows the hidden tray icons.</summary>
    TrayOverflow,


    /// <summary>The sliver at the very end.</summary>
    ShowDesktop,

    /// <summary>The band along the bar's inner edge that drags its height.</summary>
    ResizeGrip,

    /// <summary>app buttons: "…", for the buttons the bar had no room for (<see cref="Layout.Overflowed"/>).</summary>
    AppOverflow,

    /// <summary>edit: a section's "+", which one in <see cref="Hit.Index"/>, its index in <see cref="Layout.Adds"/>.</summary>
    Add,
}

/// <summary>
/// What is under the pointer. <see cref="Index"/> only means something for a
/// tile, a desktop, a tray icon, an island cell or which of Windows' buttons, and <see cref="Part"/>
/// is the index of the part in the bar's <see cref="PartSpec"/>s (-1 for the bar,
/// the grip, and in hits made without one: those name the first part of their
/// kind). Both are left at their defaults otherwise so that equality works like
/// the Rust enum's.
/// </summary>
public readonly record struct Hit(HitKind Kind, int Index = 0, int Part = -1)
{
    public static Hit Tile(int index) => new(HitKind.Tile, index);

    public static Hit Tray(int index) => new(HitKind.Tray, index);

    /// <summary>Cell <paramref name="index"/> of the island in part <paramref name="part"/>.</summary>
    public static Hit Cell(int part, int index) => new(HitKind.Island, index, part);

    public static Hit Start => new(HitKind.Start);

    public static Hit Search => new(HitKind.Search);

    public static Hit TaskView => new(HitKind.TaskView);

    /// <summary>Windows' button of <paramref name="kind"/> (<see cref="HitKind.WindowsButton"/>).</summary>
    public static Hit WindowsButton(BarKind kind) => new(HitKind.WindowsButton, (int)kind);

    public static Hit TrayOverflow => new(HitKind.TrayOverflow);

    /// <summary>app buttons: "…", the buttons the bar had no room for.</summary>
    public static Hit AppOverflow => new(HitKind.AppOverflow);

    /// <summary>edit: the "+" at <paramref name="index"/> of <see cref="Layout.Adds"/>.</summary>
    public static Hit Add(int index) => new(HitKind.Add, index);

    public static Hit ShowDesktop => new(HitKind.ShowDesktop);

    public static Hit ResizeGrip => new(HitKind.ResizeGrip);

    /// <summary>The bar itself, between and around the things on it.</summary>
    public static Hit Panel => new(HitKind.Panel);


    /// <summary>This hit in part <paramref name="part"/>.</summary>
    public Hit In(int part) => this with { Part = part };
}

public sealed class Layout
{
    // Button sizes in DIPs for a 24-DIP icon, measured on the Windows 11
    // taskbar; they scale with the icon.
    private const float BaseIconDip = 24;
    private const float WellDip = 40;          // highlight
    private const float SpaceDip = 2;          // highlight to its cell's ends
    private const float StartExtraDip = 1;     // Start is 45 DIPs, the apps 44
    private const float SmallIconDip = 16;     // system glyphs, and their least
    private const float ShowDesktopDip = 32;

    // app buttons: a labelled button — a window's own, where they are not combined — takes the whole
    // cells its icon and its label need, at most four (176 DIP: Windows 11's labelled button is about
    // as long). Where the bar is short of room the labelled buttons narrow alike, to any whole px, and
    // none below two cells (88 DIP); only then does a button go behind "…". Its label ends this far
    // before it does.
    private const int LabelCellsMost = 4;
    private const int LabelCellsLeast = 2;
    private const float LabelEndDip = 8;
    private const float PillDip = 3;

    // A system glyph's padding and step as parts of the glyph: 8 and 24 for 16.

    // The highlight's margin to the bar's edges: 4 DIPs of 48, a twelfth, and
    // never less than the 2 DIPs of breathing room a thin bar still keeps.
    private const float MarginShare = 1f / 12f;
    private const float BreathDip = 2;

    // The islands look keeps a little room between a card and the bar: a tenth of the bar's height
    // (4 DIPs of 40), never under the 2 DIPs of breath and never over 10, and never so much that a
    // card is shorter than the icon (CardInsetPx). It is the card's room above and below it and the
    // room at the two ends of a section; the floating dock keeps its 2 and its own panel room.
    private const float CardInsetShare = 1f / 10f;
    private const float MaxCardInsetDip = 10;

    // The least room between an icon and the bar's edges: a bar is never
    // thinner than its icons need.
    private const float IconMarginDip = Config.IconMargin;

    // Text: 12 DIPs on the 48-DIP bar, growing with the square root of the
    // bar's height in whole DIPs, never below 11. Segoe UI Variable's lines are
    // 1.33 em tall.
    private const float BaseHeightDip = 48;
    private const float BaseTextDip = 12;
    private const float MinTextDip = 11;

    // A widget's text in its card: 12.5 DIPs in the 48-DIP bar's 38.4-DIP card, a third of it, in
    // proportion on any other. The card is the bar less the room it keeps off its edges
    // (CardInsetPx), so the text is sized from what the widget really has.
    private const float CardTextDip = 12.5f;
    private const float LineFactor = 1.33f;

    // The widget grid's cell on the taskbar's own 48-DIP bar, and the largest
    // a widget's text grows to. A widget's text is the taskbar's 12 DIPs on
    // that cell and in proportion on any other, so content fills the square it
    // was given instead of sitting small in the middle of it; past the cap a
    // taskbar would read as a poster.
    private const float BaseCellDip = BaseHeightDip - (2 * 4.8f);
    private const float MaxCellTextDip = 26;

    // Content that overshoots a cell by less than this counts as fitting: a
    // text measured at 88.02 px asks for two cells otherwise.
    private const float CellSlack = 0.05f;

    // Below this a bar is thin: desktop buttons count their apps instead of
    // showing them, and edit badges are smaller.
    /// <summary>
    /// Below this a bar is thin. Shared: the desktop buttons count their apps
    /// instead of showing them, and edit badges are smaller.
    /// </summary>
    internal const float ThinDip = Config.ThinHeight;

    // Island cards: the least room between two sections and the corner radius, each
    // scaling with the icon between these bounds. A card has no padding of its
    // own: it is exactly the cells its widget was given, and the room at its
    // edges is the cells' (Fit.Edge, Fit.Inset), so cards stay on the grid.
    private const float SectionGapDip = 14;
    private const float MinSectionGapDip = 4;
    private const float CardRadiusDip = 6;
    private const float MinCardRadiusDip = 4;

    /// <summary>§516: the room between two tray icons, Windows 11's own: a 16-DIP icon in a 24-DIP cell.</summary>
    private const float TrayGapDip = 8;

    // §501, §515: the room between two neighbouring widgets of a section — every widget on its own card (§512), a dock's
    // widgets in its panel, an app button and the next — so each widget's plate stands apart (the person, 2026-11-04:
    // "give the individual taskbar widgets some space as well between them"). §533, §589: the layout's (TaskbarLayout.Spacing),
    // a number of DIPs, 6 where nobody chose (Config.DefaultSpacing; §506: over the 5 between two faces on one button,
    // FolderIsland.Between, so faces on one button read as one and two buttons as two), 0 none. Scaling with the icon as every
    // room on the bar does, never over its DIP nor under a third of it (2 px for 6, as before), at every bar size.

    // Floating (BarStyle.Floating): each section is a panel of its own, 8 DIPs off the screen's edge
    // and 8 from either end of it, with 8 at least between two panels, its corners 8 DIPs round —
    // Windows 11's own for its floating surfaces (Start, the Widgets board, flyouts: OverlayCornerRadius).
    // Inside a panel the widgets keep the bar's own room around them: the room across the bar
    // between a cell and the panel's edge, never less than 4 DIPs, is kept along it too.
    private const float FloatMarginDip = 8;
    private const float FloatGapDip = 8;
    private const float FloatRadiusDip = 8;
    private const float FloatPadDip = 4;

    // Edit mode: a placeholder block for an empty part, 40 DIPs along; the "Add"
    // marker runs across the bar 4 DIPs clear of its edges, as an empty section's
    // room does (EmptyCells).
    private const float EmptyDip = 40;
    private const float ZoneInsetDip = 4;

    /// <summary>edit: how many of the widget grid's cells an empty section is long while the bar is arranged.</summary>
    internal const int EmptyCells = 3;

    // The grip: 6 DIPs deep, an eighth of a thinner bar, never under 3.
    private const float GripDip = 6;
    private const float MinGripDip = 3;
    private const float GripShare = 1f / 8f;

    // Tooltips, which don't grow with the bar.
    private const float TooltipGapDip = 8;
    private const float TooltipPadXDip = 9;
    private const float TooltipPadYDip = 6;
    private const float TooltipTextDip = 12;
    private const float TooltipLineFactor = 1.4f;

    // The room above a horizontal dock is sized for six lines. It was three, and a folder's tip ran past
    // it, cut off (2026-10-11, the person: "when hovering over quick settings the tooltip is cutoff at the
    // top"); the widgets and folders show none any more (§367), but a tray icon's tip is the app's own
    // and may run as long; a longer one is cut to the room (TooltipMostHeight).
    internal const int TooltipLines = 6;

    // A vertical dock's tooltips open sideways, so the room beside it is
    // their width. Longer names are cut with an ellipsis.
    private const float TooltipWidthVerticalDip = 240;
    private const float TooltipWidthHorizontalDip = 420;

    private readonly List<PartSpec> specs;
    private readonly float space;
    private readonly float sectionGap;
    private readonly float widgetGap;
    private readonly float length;
    private readonly float chevron;

    /// <summary>
    /// tray: whether each part's tray starts with the chevron — a tray's own answer, as each tray of
    /// a bar holding two shows the whole tray in the room it has (TraySpills); false for any other part.
    /// </summary>
    private readonly bool[] chevrons;

    // tray: the cells each tray part's block was given beyond its icons while one is dragged (TrayMoreOf),
    // and the icons a tray of a set size has room for (TrayHolds).
    private readonly int[] trayMore;
    private readonly int[] trayHolds;

    /// <summary>tray: a tray cell's length along the bar, so one cell's start to the next (<see cref="TrayStep"/>).</summary>
    private readonly float trayStep;

    /// <summary>tray: the picture drawn in a tray cell, in px.</summary>
    private readonly float trayIcon;

    private readonly float emptyLength;

    /// <summary>
    /// edit: how long a section with nothing in it is while the bar is arranged
    /// (<see cref="EmptyRooms()"/>): <see cref="EmptyCells"/> cells of the widget grid and the gaps
    /// between them, as a widget of three cells would be — room a block is plainly dropped into,
    /// and plainly a section. 0 at rest, where an empty section takes no room.
    /// </summary>
    private readonly float emptyRoom;

    /// <summary>
    /// edit: a section's "+" (<see cref="Adds"/>): its side, the round button of a folder's columns
    /// (<see cref="FolderFlyout.AddDip"/>) and never more than a cell; and the room one takes along the
    /// bar beside the section's widgets, the button and the room between it and them. Both 0 at rest.
    /// </summary>
    private readonly float addSize;

    private readonly float addRoom;

    /// <summary>
    /// Where each section starts along the bar, and how long it is: its parts, the gaps between
    /// them and, in edit mode, an empty one's own room — in the order of <see cref="BarSection"/>. An empty section
    /// at rest is 0 long, at the place it would grow from: the bar's start, its middle, its end.
    /// </summary>
    private readonly float[] sectionFrom = new float[3];

    private readonly float[] sectionLength = new float[3];

    /// <summary>
    /// floating: how far the panels stand off the screen's edge, across the bar (<see cref="Lift"/>);
    /// how far the first and the last stand off the screen's ends, along it; and the room a panel
    /// keeps along the bar between its edge and its first and last widget. All 0 on a bar that
    /// does not float, which is one band flush with the edge from end to end.
    /// </summary>
    private readonly float lift;

    private readonly float ends;

    private readonly float pad;

    private readonly float panelGap;

    /// <summary>
    /// pins: what this bar was laid out from, so it can be laid out again with one more pin in it —
    /// where files dropped on it would land, at the size the new pin will have (<see cref="LandingRect"/>).
    /// </summary>
    private readonly Remake remake;

    private readonly float[] widths;

    /// <summary>How many cells each part holds — the tray's icons, a desktop strip's buttons.</summary>
    private readonly int[] counts;

    private readonly bool[] empty;

    /// <summary>search: which parts show Windows 11's search box (<see cref="SearchBoxRect"/>).</summary>
    private readonly bool[] boxed;

    /// <summary>
    /// search: which parts are Windows' search button shown as the box: the box is
    /// the part's card (<see cref="SearchBoxed"/>).
    /// </summary>
    private readonly bool[] searchBoxes;

    private readonly float[]?[] cellLengths;
    private readonly float[]?[] cellStarts;

    // Each part's first tile among the bar's tiles (Content.Tiles): a pin is one
    // tile — or one per window, where its windows are not combined —, the Apps
    // widget as many as it shows, in the parts' order; 0 for the others.
    private readonly int[] tileStarts;

    // app buttons: each tile's length along the bar — one cell, or the whole cells its label takes
    // (LabelCellsMost), less what a short bar cut from it (FitTiles) —, the cells a labelled one's
    // label asked for (0 for an icon alone), and where it starts in its part; -1 for a tile behind
    // "…" (Overflowed).
    private readonly float[] tileLengths;
    private readonly int[] tileCells;
    private readonly float[] tileOffsets;

    // app buttons: the part "…" stands in, and where in it; -1 while every button has room.
    private readonly int overflowPart = -1;
    private readonly float overflowOffset;
    private readonly List<int> placed = [];

    public Edge Edge { get; }

    /// <summary>§489: the layout whose bar this is (its id; empty for a bar of none): another one on the same bar is a swap.</summary>
    public string LayoutId { get; } = "";

    public bool Vertical { get; }

    /// <summary>Laid out for edit mode: parts with nothing to show keep a placeholder block.</summary>
    public bool Editing { get; }

    /// <summary>
    /// places: the one section a place along an edge shows (its own, <see cref="EdgePlace.Section"/>);
    /// null for a bar, which has all three. Every point along such a bar is in it
    /// (<see cref="SectionAt"/>), and in edit mode only it has an empty room.
    /// </summary>
    public BarSection? Only { get; }

    public float TextScale { get; }

    public float Scale { get; }

    /// <summary>
    /// Pixels per button DIP: <see cref="Scale"/> times the icon's size against
    /// the taskbar's 24 DIPs. Every app button metric is measured in these.
    /// </summary>
    public float Unit { get; }

    /// <summary>
    /// Pixels per DIP for the bar's parts other than the app buttons: <see cref="Scale"/> times how
    /// much those parts grow, the square root of the bar's height over the taskbar's 48 DIPs (1 on
    /// the default bar).
    /// </summary>
    public float Soft { get; }

    /// <summary>The app icon size in px, see <see cref="IconPx"/>.</summary>
    public float Icon { get; }

    /// <summary>An app button's length along the dock: its highlight and 2 DIPs either side.</summary>
    public float Slot { get; }

    /// <summary>
    /// §501, §515: the room between two neighbouring widgets of a section, every one alike — a bare corner and a search box
    /// too, on every kind of bar —, and between two app buttons of a part (<see cref="TileGap"/>). The bar shows in it, each
    /// widget its own card (§512, <see cref="Plate"/>); the pointer is each neighbour's up to its middle (§160, <see cref="Reach"/>).
    /// §533, §589: as the layout chose it (<see cref="Spacing"/>, DIPs on the default bar), 0 where it chose none. On a smaller
    /// icon it shrinks with it (<c>U</c>), never under a third of itself; the room between two sections never under it
    /// (<see cref="SectionGap"/>), so two sections still read as two.
    /// </summary>
    public float WidgetGap => widgetGap;

    /// <summary>The Start button's length along the dock.</summary>
    public float StartCell { get; }

    /// <summary>
    /// tray: one notification icon's cell across the bar: the thickness every small widget has
    /// (<see cref="CellSide"/>), centred across it, at every height, DPI and edge. Along the bar it
    /// is <see cref="TrayStep"/>, shorter: the icon and its padding, as a face is (docs\widgets.md
    /// §290). Its hover and press light that cell, on the tray's one card in the islands look.
    /// </summary>
    public float TrayCell { get; }

    /// <summary>
    /// tray: one tray cell's length along the bar, and so from one cell's start to the next, with no
    /// gap between them: the picture (<see cref="TrayIconSize"/>) and the room between two faces
    /// side by side (FolderIsland.Between), half of it either side — Windows 11's 24 DIP slot on its
    /// 48-DIP bar. Never longer than <see cref="TrayCell"/>.
    /// </summary>
    public float TrayStep => trayStep;

    /// <summary>
    /// tray: the picture drawn inside a tray cell, in px: the taskbar's own icons' size
    /// (<see cref="TrayIconPx"/>), centred in its cell.
    /// </summary>
    public float TrayIconSize => trayIcon;

    /// <summary>
    /// tray: the side of one hidden icon's square in the overflow: the bar's thickness
    /// (<see cref="TrayCell"/>), the grid Windows 11's hidden icon menu has; the picture is the bar's
    /// own size, so an icon dragged between the two keeps it.
    /// </summary>
    public float TrayOverflowCell => TrayCell;

    /// <summary>tray: how many hidden icons a row of the overflow holds, as Windows 11's does.</summary>
    public const int TrayOverflowColumns = 5;

    /// <summary>
    /// tray: the overflow chevron's cell at the start of the tray block, 0 when the bar shows none
    /// (see <see cref="TrayOverflow"/>); the shown icons follow it (Dock.Tray.cs).
    /// </summary>
    public float TrayChevron => chevron;

    /// <summary>tray: the chevron's cell at the start of tray part <paramref name="part"/>, 0 where it has none.</summary>
    public float TrayChevronOf(int part) => part >= 0 && part < chevrons.Length && chevrons[part] ? chevron : 0f;

    /// <summary>
    /// tray: the cells tray part <paramref name="part"/>'s block takes beyond one per icon while an
    /// icon is dragged (<see cref="Content.TrayMore"/>): one for an icon coming onto it, minus one for
    /// one going behind the chevron; 0 at rest, and for a tray of a set size, whose block keeps it.
    /// </summary>
    public int TrayMoreOf(int part) => part >= 0 && part < trayMore.Length ? trayMore[part] : 0;

    /// <summary>
    /// tray: how many icons tray part <paramref name="part"/>'s block has room for — the cells of a
    /// tray of a set size, past the chevron's; <see cref="int.MaxValue"/> for one as long as its icons,
    /// which a dragged icon lengthens instead (<see cref="TrayMoreOf"/>).
    /// </summary>
    public int TrayHolds(int part) => part >= 0 && part < trayHolds.Length ? trayHolds[part] : int.MaxValue;

    /// <summary>
    /// tray: the bar's tray parts that take room, in the order along it. A layout may hold the tray
    /// more than once (two in one bar); each shows the whole tray, and each is its own place for a
    /// click, a drag and the overflow its chevron opens.
    /// </summary>
    public IReadOnlyList<int> TrayParts => [.. placed.Where(index => specs[index].Kind == BarKind.Tray).Order()];

    /// <summary>
    /// tray: <paramref name="hit"/> (an icon or the chevron) in tray part <paramref name="part"/>, for
    /// a list of the bar's things — the keyboard's stops, a screen reader's elements. Named by its
    /// part only where the bar holds the tray twice: one tray's are the bare hits they always were,
    /// which name the bar's tray.
    /// </summary>
    public Hit InTray(Hit hit, int part) => TrayParts.Count > 1 ? hit.In(part) : hit;

    /// <summary>A system glyph's size: see <see cref="SmallIconPx"/>.</summary>
    public float SmallIcon { get; }

    /// <summary>Between the bar's edge-side border and an icon.</summary>
    public float PadY { get; }

    /// <summary>
    /// The side of an app button's highlight: the icon and 8 DIPs around it, as
    /// far as the bar allows (see <see cref="WellPx"/>). Island cells, their
    /// cards and edit blocks are this tall too.
    /// </summary>
    public float Well { get; }

    /// <summary>
    /// The height of the tray's and the clock's highlights:
    /// 40 DIPs grown with the bar, never taller than <see cref="Well"/>, so they
    /// keep the taskbar's proportions on a tall bar.
    /// </summary>
    public float SmallWell { get; }

    /// <summary>The bar's size across the dock: the configured height, or what the icon needs.</summary>
    public float Thickness { get; }

    /// <summary>What an island cell gets across the dock: the highlight's height, see <see cref="IslandThicknessPx"/>.</summary>
    public float IslandThickness { get; }

    /// <summary>
    /// The widget grid's cell in px: the side of the square every widget is
    /// measured in, across the bar and along it. See <see cref="CellPx"/>.
    /// </summary>
    public float CellSide => IslandThickness;

    /// <summary>An island card's, and an edit block's, corner radius: 6 DIPs, 4 on a thin bar, none on a flush one.</summary>
    public float CardRadius { get; }

    /// <summary>The look the bar wears (<see cref="Config.Style"/>): a bar's islands, or a dock's floating panel.</summary>
    public BarStyle Style { get; }

    /// <summary>
    /// Whether the widgets stand flush, square and with no card each: a floating dock's, whose panel
    /// holds them so. Every rule about gaps, corners, cards and how a hover lights asks this.
    /// </summary>
    public bool Connected { get; }

    /// <summary>
    /// An islands bar spaced 0 (<see cref="FlushFor"/>, §533), at any height: its cards fill it edge to
    /// edge with no room off the screen's edge or its ends, none between them, and every card, plate, box and
    /// ring on it is square. Its cards stay. (§123 to §532 every islands bar under <see cref="ThinDip"/>, whatever it chose.)
    /// </summary>
    public bool Flush { get; }

    /// <summary>§533, §589: the room this bar keeps between its widgets, its layout's choice in DIPs (<see cref="WidgetGap"/>).</summary>
    public int Spacing { get; }

    /// <summary>A plate's or a ring's corner of <paramref name="radius"/> px on this bar: none on a flush one.</summary>
    public float Corner(float radius) => Flush ? 0f : radius;

    /// <summary>The islands look: a card behind every widget, always (<see cref="BarStyle.Islands"/>).</summary>
    public bool Cards => Style == BarStyle.Islands;

    /// <summary>A highlight's corner in DIPs on the 48-DIP bar, grown with the bar (<see cref="SoftPx"/>) or the icon (<see cref="UnitPx"/>).</summary>
    internal const float HighlightRadiusDip = 4;

    /// <summary>
    /// §648: the corners of the plate a widget's cell stands on, is lit in under the pointer and fills when it is on: the cards'
    /// on a bar of cards, else its highlight's (§376); none on a flush bar. One number for the scene's cards, plates and
    /// highlight and the widget's own drawing (<see cref="IslandStyle.PlateRadius"/>), so a switch's accent is its plate exactly.
    /// </summary>
    public float PlateRadius => Cards ? CardRadius : Corner(SoftPx(HighlightRadiusDip));

    /// <summary>
    /// floating: each section that holds a widget is a panel of its own (<see cref="SectionPanels()"/>),
    /// <see cref="Lift"/> off the screen's edge, and the desktop shows around them.
    /// </summary>
    public bool Floating => Style == BarStyle.Floating;

    /// <summary>
    /// floating: how far the bar stands off the screen's edge, in px: 8 DIPs while it floats, 0
    /// otherwise. The room between it and the edge is still the bar's — windows keep out of it
    /// (<see cref="Reserve"/>) and a press there is the bar's, the widget's above it included.
    /// </summary>
    public float Lift => lift;

    /// <summary>
    /// How far the first and the last of the bar's sections stand off its ends, along it, in px: on
    /// an islands bar the room cards keep off the bar's edge across it (<see cref="CardInsetPx"/>),
    /// and on a floating one the panels' room off the screen's ends. The bar's usable length is its
    /// length less this twice.
    /// </summary>
    public float EndRoom => ends;

    /// <summary>
    /// The strip of the display the bar keeps for itself, across the edge, in px: its thickness and
    /// the room it floats over.
    /// </summary>
    public float Reserve => Thickness + lift;

    /// <summary>floating: a panel's corners, Windows 11's 8 DIPs for its floating surfaces.</summary>
    public float PanelRadius { get; }

    /// <summary>§704: the corners, in DIPs, of the Liquid glass the bar was laid out in (its cards' and a floating panel's); null for another look.</summary>
    public float? GlassCorner { get; }

    /// <summary>
    /// floating: a panel's shadow, a soft one under it as Windows 11's floating surfaces have: how
    /// far it blurs, and how far it falls down the screen, in px. None on a bar that does not float.
    /// </summary>
    public float PanelShadowBlur => Floating ? Round(16f * Scale) : 0f;

    /// <inheritdoc cref="PanelShadowBlur"/>
    public float PanelShadowDrop => Floating ? Round(2f * Scale) : 0f;

    /// <summary>floating: how far past a panel its shadow can show.</summary>
    private float PanelShadowReach => PanelShadowBlur + PanelShadowDrop;

    /// <summary>A hairline at this scale: one pixel below 200 %.</summary>
    public float Stroke { get; }

    /// <summary>The bar's border on its inner side: one DIP, rounded as XAML rounds it (2 px at 150 %).</summary>
    public float Border { get; }

    /// <summary>A running indicator's thickness: 3 DIPs at the icon's scale, never under 2 px.</summary>
    public float Pill { get; }

    /// <summary>The bar's text size in DIPs before the text scale, see <see cref="TextDipsFor"/>.</summary>
    public float TextDips { get; }

    /// <summary>A thin bar, under 36 DIPs.</summary>
    public bool Thin { get; }

    /// <summary>Whether a tray of the bar starts with the chevron for hidden icons (<see cref="TrayChevronOf"/>: which).</summary>
    public bool TrayOverflow { get; }

    /// <summary>The bar's corner radius: 0, the dock is flush with its edge.</summary>
    public float Radius => 0;

    /// <summary>Between the bar and a tooltip, at every height.</summary>
    public float LabelGap { get; }

    /// <summary>Between the bar and the screen edge: none, the dock is flush.</summary>
    public float Gap => 0;

    /// <summary>The reveal strip along the screen edge.</summary>
    public float Hot { get; }

    /// <summary>The widest a tooltip's text may be; longer text is cut with an ellipsis.</summary>
    public float TooltipMaxWidth { get; }

    /// <summary>The bar's length along its edge, in px: the window's, from end to end.</summary>
    public float Length => length;

    public int Width { get; }

    public int Height { get; }

    public int X { get; }

    public int Y { get; }

    public RectF PanelRect { get; }

    public float PanelLeft => PanelRect.X;

    public float PanelRight => PanelRect.Right;

    public float PanelTop => PanelRect.Y;

    public float PanelBottom => PanelRect.Bottom;

    /// <summary>
    /// Where an app icon starts across the dock: its top on a horizontal dock,
    /// its left on a vertical one.
    /// </summary>
    public float IconTop { get; }

    /// <summary>The strip along the screen edge that reveals a hidden dock.</summary>
    public RectF HotStrip { get; }

    /// <summary>
    /// The band along the bar's inner edge, the whole length, that drags the
    /// bar's height: 6 DIPs deep, less on a thin bar so it takes no more than
    /// the margin above the highlights. It is part of the bar, and is hit
    /// before anything under it.
    /// </summary>
    public RectF GripRect { get; }

    /// <summary>The islands this bar shows, in the order it was given them.</summary>
    public IReadOnlyList<PartSpec> Specs => specs;

    /// <summary>
    /// The widgets this bar holds but had no room to show, in the order they
    /// sit in the list.
    ///
    /// A bar too short for its widgets drops them from the end rather than
    /// squeezing everything past being readable, and it does that silently: a
    /// badge on a bar already too crowded to hold its widgets is the last thing
    /// that bar needs. So the bar records what it dropped and says nothing, and
    /// the edit rail — where the person is already doing the thing that would
    /// fix it — is what tells them.
    ///
    /// Only widgets appear here. A part with nothing to show is not hidden, it
    /// is empty.
    /// </summary>
    public IReadOnlyList<PartSpec> NotShown { get; }


    /// <summary>
    /// app buttons: the ones the bar has no room for, by their place among the tiles, in order. They
    /// stand behind "…" (<see cref="HitKind.AppOverflow"/>), whose click lists them, as Windows 11's
    /// taskbar does with the apps it cannot fit; their own places on the bar are empty.
    /// </summary>
    public IReadOnlyList<int> Overflowed { get; }

    /// <summary>
    /// places: how long the bar wants to be along its edge to show all it holds at its own size, in
    /// whole px — every part, the app buttons with their whole labels and none behind "…", in edit mode
    /// an empty section's room, and a floating bar's ends —, however long it was given; 0 where it
    /// shows nothing. What a hover place's stretch of its edge follows (<see cref="HoverPlaces.Share"/>).
    /// </summary>
    public int Natural { get; }

    /// <summary>
    /// places: the least room between two places along one edge that both hold something, as between
    /// a bar's sections; none on a floating edge, whose places each keep their ends inside them.
    /// </summary>
    public int PlaceGap => Floating ? 0 : (int)MathF.Ceiling(SectionGap);

    /// <summary>One part per spec, in the same order: <see cref="Hit.Part"/> indexes both.</summary>
    public List<Part> Parts { get; }

    /// <summary>
    /// The layout of a bar showing <paramref name="parts"/> in that order, each
    /// measured from <paramref name="content"/>: islands through
    /// <see cref="Content.Islands"/>.
    /// <paramref name="area"/>
    /// is the rectangle the dock stands in, in screen pixels: the display's
    /// bounds while the taskbar is hidden, its work area otherwise. The dock
    /// covers all of that edge; <paramref name="monitor"/> gives the desktop
    /// buttons the display's shape. With <paramref name="editing"/>, parts with
    /// nothing to show keep a placeholder, so they can be moved and removed.
    /// </summary>
    public Layout(
        Gfx gfx,
        uint dpi,
        RECT monitor,
        RECT area,
        Edge edge,
        Config config,
        Look look,
        IReadOnlyList<PartSpec> parts,
        Content content,
        bool editing = false,
        BarSection? only = null)
        : this(dpi, monitor, area, edge, config, look.TextScale, editing, parts,
            Measure(gfx, dpi, config, look, edge, parts, content), only, look.Liquid?.RadiusDip)
    {
    }

    private Layout(
        uint dpi,
        RECT monitor,
        RECT area,
        Edge edge,
        Config config,
        float textScale,
        bool editing,
        IReadOnlyList<PartSpec> given,
        Measures measures,
        BarSection? only = null,
        float? glassCorner = null)
    {
        Only = only;
        GlassCorner = glassCorner;
        if (!(textScale > 0f))
        {
            textScale = 1f;
        }
        Screen = ScreenOf(monitor);
        specs = [.. given];
        var count = specs.Count;
        var scale = dpi / 96f;
        var unit = UnitFor(dpi, config);
        var growth = GrowthFor(config);
        var soft = scale * growth;
        float Px(float dip) => Round(dip * scale);
        float U(float dip) => Round(dip * unit);
        float S(float dip) => Round(dip * soft);
        float Within(float value, float least, float most) => MathF.Min(MathF.Max(value, least), most);
        var vertical = edge.IsVertical();
        var icon = IconPx(dpi, config);
        var thickness = ThicknessPx(dpi, config);
        var well = WellPx(dpi, config);
        var smallWell = SmallWellPx(dpi, config);
        // Two questions that used to be one. "Thin" is the room the bar has: under ThinDip there
        // is none to spare, and the text, the cell, the grip and the desktop buttons follow it and
        // always will. The look is what the bar is (Config.Style): a bar wears islands, a dock floats.
        var thin = EffectiveHeight(config) < ThinDip;
        Style = config.StyleOrDefault;
        LayoutId = config.Bar?.Id ?? "";
        // A floating dock's panel holds its widgets flush, square, with no card each ("connected"):
        // the panel is what stands apart, and cards inside it would be a frame in a frame.
        var connected = Style == BarStyle.Floating;
        Connected = connected;
        // floating: the panels' room off the screen's edge and its ends, and inside each its widgets'.
        var floating = Style == BarStyle.Floating;
        lift = floating ? Px(FloatMarginDip) : 0f;
        ends = floating ? Px(FloatMarginDip) : CardInsetPx(dpi, config);
        pad = floating ? MathF.Max(Px(FloatPadDip), MathF.Floor((thickness - smallWell) / 2f)) : 0f;
        // islands: the first and the last card keep the same room off the bar's ends the cards keep
        // above and below (CardInsetPx).
        panelGap = floating ? Px(FloatGapDip) : 0f;
        // §704: a Liquid glass panel its glass's corners, never more than half the panel across.
        PanelRadius = !floating ? 0f : glassCorner is { } corner ? MathF.Min(Px(corner), MathF.Floor(thickness / 2f)) : Px(FloatRadiusDip);
        space = U(SpaceDip);
        // A taskbar button is one grid cell, like everything else on the bar, so its card is 1×1 at
        // every bar height. It used to be its highlight and 2 DIPs either side, which is a second
        // rule for the same question: the two agreed on the common bar heights and parted at both
        // ends — above 48 DIPs the highlight stops at 40 while the cell keeps growing, so a 64-DIP
        // bar gave a button a card 44 px wide and 60 px tall, and on a 28-DIP bar the 2 DIPs either
        // side put it 2 px wider than the cell. The highlight inside it is still the taskbar's own
        // (Button); only the room it stands in is the grid's.
        var slot = smallWell;
        var startCell = slot + U(StartExtraDip);
        var smallIcon = SmallIconPx(dpi, config);
        // tray: the picture in a cell, the taskbar's own icons' size (TrayIconPx). A notification
        // icon is its picture and TrayGapDip between two (§516, the person, 2026-11-05: "the tray icons should be just a
        // tad more wide"; §506 had given it the faces' 5 DIP, a 21-DIP cell), half of it either side, along the bar —
        // 24 DIP on the 48-DIP bar, Windows 11's own tray slot — and the whole of the bar's usable thickness across it, the
        // hover's and the press's room (CellPx). Never longer than that thickness. The chevron is
        // one more such cell.
        trayIcon = TrayIconPx(dpi, config);
        var trayCell = MathF.Max(1f, smallWell);
        var trayFit = new Fit(new IslandStyle(scale, vertical, smallWell, icon, default));
        trayStep = MathF.Min(trayCell, MathF.Max(1f, trayIcon + trayFit.Px(TrayGapDip)));
        // The gap and the corner belong to the look, not to the room. A dock's panel has neither by
        // definition. The islands look keeps both at any height: the clamps below floor
        // them at their minimums, so a 28 DIP bar asked for islands gets the smallest gap and the
        // smallest corner the design allows — cramped, and honestly so, rather than quietly
        // falling back to the other look under a different name.
        sectionGap = connected ? 0 : Within(U(SectionGapDip), Px(MinSectionGapDip), Px(SectionGapDip));
        Spacing = config.BarSpacing;
        widgetGap = Spacing > 0 ? Within(U(Spacing), Px(Spacing / 3f), Px(Spacing)) : 0f;
        // §589: a spacing chosen wider than the room between two sections widens that too: two sections never closer than two widgets.
        if (!connected)
        {
            sectionGap = MathF.Max(sectionGap, widgetGap);
        }
        // flush: an islands bar spaced 0 — and only that, at any height (§533) — has cards that fill it, square (FlushFor).
        Flush = FlushFor(config);
        // §704: Liquid glass's cards its corners, never more than half a card across.
        var cardRadius = connected || Flush ? 0
            : glassCorner is { } glassy ? MathF.Min(Px(glassy), MathF.Floor(smallWell / 2f))
            : Within(U(CardRadiusDip), Px(MinCardRadiusDip), Px(CardRadiusDip));
        emptyLength = editing ? S(EmptyDip) : 0f;
        // tray: the chevron also where a tray of a set size has more icons to show than it has room
        // for: those go behind it (Dock.TrayOverflowOf), as on Windows 11, where before they showed
        // nowhere at all.
        // Each tray its own (§347): its own icons, its chevron for what is behind it or for what it
        // is too short for.
        chevrons = [.. specs.Select(spec => spec.Kind == BarKind.Tray && (measures.TrayOverflowOf(spec.Key) || TraySpills(spec, measures.TrayOf(spec.Key), smallWell, trayStep)))];
        var trayOverflow = chevrons.Any(has => has);
        trayMore = new int[specs.Count];
        trayHolds = [.. specs.Select(_ => int.MaxValue)];
        // tray: the chevron's own cell. One tray cell's start is the last one's end, in every look:
        // the icons stand side by side with no gap between their cells, as Windows 11's do (the
        // person, 2026-10-08: "tray icons sit connected, with no gap between them"); the room
        // between two pictures is the cells' own.
        chevron = trayOverflow ? trayStep : 0f;
        var textDips = TextDipsFor(config);

        // The widget grid's cell: the square every widget is measured in.
        var gridCell = smallWell;
        // edit: an empty section's room, three cells and the gaps between them (EmptyRooms).
        emptyRoom = editing ? EmptyCells * gridCell + (EmptyCells - 1) * sectionGap : 0f;
        // edit: a section's "+" (Adds), apart from its widgets by their own gap — a dock's by the room
        // between two docks, so it stands beside the dock, not in it.
        addSize = editing ? MathF.Min(Px(FolderFlyout.AddDip), gridCell) : 0f;
        addRoom = editing ? addSize + (floating ? panelGap : MathF.Max(sectionGap, Px(MinSectionGapDip))) : 0f;

        widths = new float[count];
        empty = new bool[count];
        boxed = new bool[count];
        searchBoxes = [.. specs.Select(spec => spec is { Box: true, Kind: BarKind.Search } && SearchPill.Fits(vertical, gridCell, scale))];
        cellLengths = new float[]?[count];
        cellStarts = new float[]?[count];
        tileStarts = new int[count];
        counts = new int[count];
        // app buttons: how many each part holds, in the parts' order — as the content says, or a pin
        // one each and the Apps widget the rest —, and each one's length: its cell, or the whole
        // cells its label takes, from two to LabelCellsMost.
        var perPart = new int[count];
        if (measures.TileCounts is { } told && told.Length == count)
        {
            Array.Copy(told, perPart, count);
        }
        else
        {
            var rest = Math.Max(0, measures.Tiles - specs.Count(spec => spec.Kind == BarKind.Pin));
            for (var index = 0; index < count; index++)
            {
                (perPart[index], rest) = specs[index].Kind switch
                {
                    BarKind.Pin => (1, rest),
                    BarKind.Apps => (rest, 0),
                    _ => (0, rest),
                };
            }
        }
        var tileCount = Math.Max(0, perPart.Sum());
        remake = new Remake(dpi, monitor, area, config, measures, perPart);
        tileLengths = new float[tileCount];
        tileCells = new int[tileCount];
        tileOffsets = new float[tileCount];
        var labelEnd = S(LabelEndDip);
        for (var tile = 0; tile < tileCount; tile++)
        {
            var label = measures.Labels is { } labels && tile < labels.Length ? labels[tile] : 0f;
            tileCells[tile] = label > 0f && !vertical
                ? Math.Clamp(CellsFor(slot + label + labelEnd, slot), LabelCellsLeast, LabelCellsMost)
                : 0;
            tileLengths[tile] = tileCells[tile] > 0 ? tileCells[tile] * slot : slot;
        }
        // app buttons: a part's chosen room, where it has one, which its buttons are held inside, and
        // whether it is the range's most (a room they may come in under) rather than its least.
        var heldRoom = new float[count];
        var heldMost = new bool[count];
        var tileAt = 0;
        for (var index = 0; index < count; index++)
        {
            var spec = specs[index];
            var (partLength, partCount) = (0f, 0);
            switch (spec.Kind)
            {
                case BarKind.Start:
                    (partLength, partCount) = (startCell, 1);
                    break;
                // search: Windows 11's search box where the button is set to it and the bar has room
                // for one, as long as its magnifier and words (SearchPill.Length, §510); else the one button.
                case BarKind.Search when spec.Box && SearchPill.Fits(vertical, gridCell, scale):
                    boxed[index] = true;
                    (partLength, partCount) = (SearchPill.Length(gridCell, measures.SearchWords), 1);
                    break;
                case BarKind.Search or BarKind.TaskView:
                    (partLength, partCount) = (slot, 1);
                    break;
                // Windows' other buttons are the same one button — and an input button this PC would
                // not show is nothing to show, so no room and a placeholder in edit mode (below).
                case var button when button.IsWindowsButton():
                    (partLength, partCount) = spec.Absent ? (0f, 0) : (slot, 1);
                    break;
                case var buttons when buttons.IsButtons():
                    // A pin is an app button of its own — or one per window, where its windows are
                    // not combined —, and the Apps widget the open apps nothing pins: nothing open
                    // that isn't pinned is no room, as on the taskbar. Each button its length, a
                    // widget's gap from the next (TileGap) — the shape a run of pins has on the bar,
                    // because these are the same buttons.
                    tileStarts[index] = tileAt;
                    partCount = perPart[index];
                    partLength = TileRun(tileAt, partCount);
                    tileAt += partCount;
                    break;
                case BarKind.Tray:
                    var icons = Math.Max(measures.TrayOf(spec.Key), 0);
                    if (icons > 0 || chevrons[index])
                    {
                        // tray: a cell per icon and one for the chevron, side by side (trayStep).
                        // The block ends on its last cell, so the gap to the next widget is that
                        // widget's to keep, like any other pair. An icon dragged onto it brings a
                        // cell, one dragged behind the chevron takes its own away (TrayMoreOf), so
                        // the block grows and shrinks with the drag and the bar makes room as it does
                        // for any widget; its icons are still the ones it has (partCount).
                        var more = measures.TrayMore is { } mores && mores.TryGetValue(spec.Key, out var by) ? by : 0;
                        trayMore[index] = Math.Max(more, -icons);
                        var trayCells = icons + trayMore[index] + (chevrons[index] ? 1 : 0);
                        (partLength, partCount) = (trayCells * trayStep, icons);
                    }
                    break;
                case BarKind.ShowDesktop:
                    (partLength, partCount) = (S(ShowDesktopDip), 1);
                    break;
                default:
                    if (measures.Cells[index] is { Count: > 0 } cells)
                    {
                        // An island's cells are each one grid cell at the least and
                        // shared out as its range says, so a click, a highlight and
                        // a drawn surface all meet on the same rectangle; except
                        // where the island is exactly its content (IslandCells.Exact:
                        // a folder's button, §275), which is taken as it is.
                        var content = new float[cells.Count];
                        for (var cell = 0; cell < cells.Count; cell++)
                        {
                            content[cell] = cells.Lengths is { } list && cell < list.Count ? list[cell] : cells.CellLength;
                        }
                        var lengths = cells.Exact ? content : AllotCells(content, RoomOf(spec), gridCell);
                        var starts = new float[cells.Count];
                        var at = 0f;
                        for (var cell = 0; cell < cells.Count; cell++)
                        {
                            starts[cell] = at;
                            at += lengths[cell];
                        }
                        cellLengths[index] = lengths;
                        cellStarts[index] = starts;
                        (partLength, partCount) = (at, cells.Count);
                    }
                    break;
            }
            // The size of a kind measured outside the island branch — Start, Search, Task View, a
            // pin, the open apps: its natural size in cells held inside its range, the
            // rule the islands keep in AllotCells (WidgetBounds). Natural is the whole cells the
            // part's length takes — the open apps' run of buttons and the gaps
            // between them — except Start's, which is one button a hair wider than a cell
            // (StartExtraDip) and counts as the one. Where the content lies inside the range the
            // part keeps its exact length, so Start stays 45 DIP against a 44-DIP cell; only a
            // range that leaves the content out changes it, to exactly the cells the range allows,
            // and the open apps then show the buttons that fit in them. A pin's most and the open
            // apps' untouched most hold no buttons (ButtonRoom): the bar's free room is theirs.
            // The tray keeps a size of its own kind (Sections.HasSize): its width is its pair with the
            // ends together, when it has one, and no range.
            int? held = null;
            // search: not the search box, which is Windows' one length: the one width a person sets
            // for the search button is the button's, and the settings app offers none for the box.
            if (partLength > 0f && !spec.Kind.IsIsland() && !boxed[index])
            {
                if (spec.Kind.HasSize())
                {
                    var natural = spec.Kind == BarKind.Start ? 1 : CellsFor(partLength, gridCell);
                    var inRange = ButtonRoom(spec.Kind, RoomOf(spec)).Clamp(natural);
                    held = inRange == natural ? null : inRange;
                }
                else if (spec.Kind is BarKind.Tray && WidthOf(spec) is var chosen and >= WidgetItem.MinWidth)
                {
                    held = chosen;
                }
            }
            if (held is { } heldCells)
            {
                var room = heldCells * gridCell;
                if (spec.Kind.IsButtons())
                {
                    // The buttons that fit in the room, as the taskbar fills a stretch of bar; the
                    // others go behind "…" (FitTiles), which takes its cell of the room. A room its
                    // most set is a most, which the buttons may come in under where the bar is short;
                    // one its least set is kept whole.
                    heldRoom[index] = room;
                    heldMost[index] = room < partLength;
                }
                partCount = spec.Kind switch
                {
                    // tray: whole tray cells of the chosen room, the chevron's among them, side by side.
                    BarKind.Tray => Math.Clamp(
                        (int)MathF.Floor(room / MathF.Max(1f, trayStep)) - (chevrons[index] ? 1 : 0),
                        0,
                        partCount),
                    _ => partCount,
                };
                if (spec.Kind is BarKind.Tray)
                {
                    // tray: a set size is kept through a drag too: the icons it has no room for go
                    // behind the chevron, as they do at rest (Scene.TrayDragIn).
                    trayMore[index] = 0;
                    trayHolds[index] = Math.Max(0, (int)MathF.Floor(room / MathF.Max(1f, trayStep)) - (chevrons[index] ? 1 : 0));
                }
                partLength = room;
            }
            if (partLength <= 0f)
            {
                // Nothing to show: no room, and no gap either, but a placeholder
                // block in edit mode.
                empty[index] = true;
                partLength = emptyLength;
            }
            widths[index] = partLength;
            counts[index] = partCount;
        }

        length = vertical ? area.bottom - area.top : area.right - area.left;
        var shownParts = Enumerable.Range(0, count).Where(index => widths[index] > 0f).ToList();
        // places: what it wants before anything gives way, whatever room it has (Natural).
        var wanted = Needed(shownParts);
        Natural = wanted > 0f ? (int)MathF.Ceiling(wanted + (2f * ends)) : 0;
        // What the parts have to fit in: the whole bar. In edit mode an empty section's room is part
        // of it (Needed), so a full bar makes room for it as it does for any part. A floating bar
        // keeps its ends off the screen's.
        var fit = length - 2f * ends;
        // edit: the placeholder block an empty part wears is edit mode's own chrome, and it
        // reserves nothing. A bar with room to spare shows one; a full bar gives it up, the last
        // one first, before it gives up a widget — entering edit mode to rearrange your bar
        // should show you your bar, not a shorter one.
        for (var at = shownParts.Count - 1; at >= 0 && Needed(shownParts) > fit; at--)
        {
            if (empty[shownParts[at]])
            {
                shownParts.RemoveAt(at);
            }
        }
        // The sections first make room among themselves: the middle leaves the centre as little as
        // keeps it clear of the others (SectionStarts), which costs nothing here. Then the app
        // buttons: a part held to a chosen width keeps its buttons inside it, and a bar still too
        // short makes its app buttons smaller and folds them into "…" before any widget gives way —
        // Windows 11's taskbar makes room in its apps and never in the rest of it (FitTiles). In
        // edit mode that is also how an empty section gets its room on a busy bar: the widgets
        // are what the person placed, and an app's button is still one click away in "…".
        var hidden = new bool[tileCount];
        var overflowAt = -1;
        var overflowFrom = 0f;
        FitTiles();
        // The widgets that give way below, so the bar can be asked afterwards
        // which ones it is holding but not showing.
        var gaveWay = new List<int>();
        // When the bar still doesn't fit, islands give way, the last placed first, before any of
        // the taskbar's own buttons is hidden. A bar with nothing left to give (its parts all
        // Windows' own, the clock, show desktop) runs its sections one after another from the start
        // and the tail falls off the end (SectionStarts) — the empty rooms with it in edit mode.
        while (Needed(shownParts) > fit
            && shownParts.FindLastIndex(index => specs[index].Kind.IsIsland()) is var drop and >= 0)
        {
            gaveWay.Add(shownParts[drop]);
            shownParts.RemoveAt(drop);
        }
        // Back into the order the person arranged them, because that is the
        // order they will be read in.
        gaveWay.Sort();
        NotShown = [.. gaveWay.Select(index => specs[index])];
        Overflowed = [.. Enumerable.Range(0, tileCount).Where(tile => hidden[tile])];
        overflowPart = overflowAt;
        overflowOffset = overflowFrom;
        // A part's count is the buttons it shows, which are always its first ones — a button goes
        // behind "…" from the end of the list, and of a held part —, so a tile past it is in no part.
        for (var index = 0; index < count; index++)
        {
            if (specs[index].Kind.IsButtons())
            {
                counts[index] = Enumerable.Range(tileStarts[index], perPart[index]).TakeWhile(tile => !hidden[tile]).Count();
            }
        }
        var shownSet = shownParts.ToHashSet();
        for (var index = 0; index < count; index++)
        {
            if (!shownSet.Contains(index))
            {
                widths[index] = 0f;
                cellLengths[index] = null;
                cellStarts[index] = null;
            }
        }
        var lefts = Positions(shownParts, sectionFrom, sectionLength);

        var parts = new List<Part>(count);
        for (var index = 0; index < count; index++)
        {
            var spec = specs[index];
            parts.Add(new Part(spec.Key, spec.Kind, lefts[index], widths[index], counts[index]));
        }
        placed = [.. shownParts];

        TooltipMaxWidth = vertical
            ? Px(TooltipWidthVerticalDip)
            : MathF.Min(Px(TooltipWidthHorizontalDip), length - 2f * Px(TooltipPadXDip));
        var depth = DepthFor(area, vertical, thickness + lift, TooltipRoomFor(dpi, edge, textScale));
        var alongWindow = (int)length;

        Edge = edge;
        Vertical = vertical;
        Editing = editing;
        TextScale = textScale;
        Scale = scale;
        Unit = unit;
        Soft = soft;
        Icon = icon;
        Slot = slot;
        StartCell = startCell;
        TrayCell = trayCell;
        SmallIcon = smallIcon;
        Well = well;
        SmallWell = smallWell;
        Thickness = thickness;
        IslandThickness = smallWell;
        CardRadius = cardRadius;
        Stroke = EditLook.Hair(scale);
        Border = MathF.Max(1f, Px(1f));
        Pill = MathF.Max(U(PillDip), 2f);
        TextDips = textDips;
        LabelPx = LabelPxFor(dpi, config, textScale);
        Thin = thin;
        TrayOverflow = trayOverflow;
        LabelGap = Px(TooltipGapDip);
        Hot = MathF.Max(Px(2f), 2f);
        Width = vertical ? depth : alongWindow;
        Height = vertical ? alongWindow : depth;
        X = edge == Edge.Right ? area.right - Width : area.left;
        Y = edge == Edge.Bottom ? area.bottom - Height : area.top;
        // The bar's strip, end to end; a floating bar's panels stand in it (SectionPanels), and the
        // room between it and the screen's edge is the bar's too (Lift).
        PanelRect = ToWindow(0f, 0f, length, thickness);
        // At the screen's edge itself, where the pointer goes, whether or not the bar floats.
        HotStrip = OnEdge(0f, 0f, length, Hot);
        var grip = MathF.Min(Within(Round(thickness * GripShare), Px(MinGripDip), Px(GripDip)), thickness);
        GripRect = ToWindow(0f, thickness - grip, length, grip);
        PadY = MathF.Floor((thickness - icon) / 2f);
        var iconRect = ToWindow(0f, PadY, 0f, icon);
        IconTop = vertical ? iconRect.X : iconRect.Y;
        Parts = parts;

        // app buttons: what the bar has room for, a step at a time. A part held to a chosen width is
        // made to fit first, then the bar: each step the labelled buttons give up what is short,
        // evenly — the longest cut to one length, none below LabelCellsLeast cells (Shrink) —, else,
        // all of them that short already, the last button still shown goes behind "…". So "…" only
        // comes once every labelled button is at its least, as Windows 11's taskbar narrows its
        // labelled buttons alike before it overflows. "…" stands at the end of the Apps widget where
        // it has buttons, else of the part that holds the last one, as Windows 11's stands at the end
        // of its apps.
        void FitTiles()
        {
            if (tileCount == 0)
            {
                return;
            }
            while (true)
            {
                LayTiles();
                var held = Enumerable.Range(0, count).FirstOrDefault(index => heldRoom[index] > 0f && PartRun(index) > heldRoom[index] + 0.5f, -1);
                if (held >= 0)
                {
                    var over = PartRun(held) - heldRoom[held];
                    if (!Shrink(tileStarts[held], perPart[held], over) && !Hide(tileStarts[held], perPart[held]))
                    {
                        return;
                    }
                    continue;
                }
                var needed = Needed(shownParts);
                if (needed <= fit || (!Shrink(0, tileCount, needed - fit) && !Hide(0, tileCount)))
                {
                    return;
                }
            }
        }

        // The part tile <paramref name="tile"/> stands in.
        int TilePart(int tile)
        {
            for (var index = 0; index < count; index++)
            {
                if (tile >= tileStarts[index] && tile < tileStarts[index] + perPart[index])
                {
                    return index;
                }
            }
            return -1;
        }

        // Where "…" stands, with buttons behind it: at the end of the Apps widget whose button went
        // behind it last — a bar holding the open apps twice gives up the second's first, and "…"
        // takes their place there —, else of the bar's last Apps widget with buttons, else of the
        // part that holds the last button gone, as Windows 11's stands at the end of its apps.
        int Home()
        {
            var last = Array.LastIndexOf(hidden, true);
            if (last < 0)
            {
                return -1;
            }
            var holder = TilePart(last);
            if (holder >= 0 && specs[holder].Kind == BarKind.Apps)
            {
                return holder;
            }
            for (var index = count - 1; index >= 0; index--)
            {
                if (specs[index].Kind == BarKind.Apps && perPart[index] > 0)
                {
                    return index;
                }
            }
            return holder;
        }

        // How long part index's buttons are, "…" among them where it stands there.
        float PartRun(int index)
        {
            var first = tileStarts[index];
            var run = TileRun(first, perPart[index], hidden);
            if (index == overflowAt)
            {
                run += (run > 0f ? TileGap : 0f) + slot;
            }
            return run;
        }

        // Every app-button part's length and each of its buttons' place in it, as things stand; a part
        // with no button left and no "…" leaves the bar.
        void LayTiles()
        {
            overflowAt = Home();
            for (var index = 0; index < count; index++)
            {
                if (!specs[index].Kind.IsButtons() || perPart[index] == 0)
                {
                    continue;
                }
                var at = 0f;
                var shown = 0;
                for (var tile = tileStarts[index]; tile < tileStarts[index] + perPart[index]; tile++)
                {
                    if (hidden[tile])
                    {
                        tileOffsets[tile] = -1f;
                        continue;
                    }
                    at += shown++ > 0 ? TileGap : 0f;
                    tileOffsets[tile] = at;
                    at += tileLengths[tile];
                }
                if (index == overflowAt)
                {
                    overflowFrom = at + (shown > 0 ? TileGap : 0f);
                }
                var run = PartRun(index);
                widths[index] = heldRoom[index] <= 0f ? run
                    : heldMost[index] ? MathF.Min(heldRoom[index], run)
                    : MathF.Max(heldRoom[index], run);
                if (run <= 0f)
                {
                    widths[index] = 0f;
                    shownParts.Remove(index);
                }
                else if (!shownParts.Contains(index))
                {
                    // "…" moving into a part that gave all its buttons up: it is back on the bar, in its place.
                    var place = shownParts.FindIndex(other => other > index);
                    shownParts.Insert(place < 0 ? shownParts.Count : place, index);
                }
            }
        }

        // The labelled buttons among tiles [from, from + many) give up `over` px between them, evenly:
        // the longest are cut to one length, a whole px, none below LabelCellsLeast cells (EvenCut).
        // False where every one of them is that short already.
        bool Shrink(int from, int many, float over)
        {
            var least = LabelCellsLeast * slot;
            var lengths = new List<float>();
            for (var tile = from; tile < from + many; tile++)
            {
                if (!hidden[tile] && tileCells[tile] > 0 && tileLengths[tile] > least + 0.01f)
                {
                    lengths.Add(tileLengths[tile]);
                }
            }
            if (lengths.Count == 0)
            {
                return false;
            }
            var cut = MathF.Max(least, MathF.Floor(EvenCut(lengths, over, least)));
            for (var tile = from; tile < from + many; tile++)
            {
                if (!hidden[tile] && tileCells[tile] > 0 && tileLengths[tile] > cut)
                {
                    tileLengths[tile] = cut;
                }
            }
            return true;
        }

        // The last button still shown among tiles [from, from + many) goes behind "…".
        bool Hide(int from, int many)
        {
            for (var tile = from + many - 1; tile >= from; tile--)
            {
                if (!hidden[tile])
                {
                    hidden[tile] = true;
                    return true;
                }
            }
            return false;
        }

        // The whole bar: each section's parts with the room between them, an empty one's room in edit
        // mode, and the least room between two sections that both hold something. The bar keeps nothing
        // at its two ends, so a widget at either end is flush with the display.
        float Needed(List<int> shown) => Total(SectionLengths(shown, -1, default));
    }

    /// <summary>
    /// A layout measured without a Gfx, for tests: <paramref name="area"/> is
    /// also the display; every island part is one cell <paramref name="cellLength"/>
    /// px long.
    /// <paramref name="tray"/> notification icons stand on the bar, with the overflow chevron
    /// before them when <paramref name="trayOverflow"/> is set — in every tray part, but those
    /// <paramref name="trays"/> names by key, each its own (§347). <paramref name="tiles"/> is the
    /// bar's app buttons all told, pins included, so an Apps part shows what is left over after
    /// the pins have taken theirs; none by default. <paramref name="tileCounts"/> says how many each
    /// part holds instead (a pin with its windows not combined holds one per window), and
    /// <paramref name="labels"/> gives each tile a label, measured as the fake text is.
    /// <paramref name="only"/> is a place's one section (<see cref="Only"/>).
    /// </summary>
    internal static Layout ForTests(
        uint dpi,
        RECT area,
        Edge edge,
        Config config,
        IReadOnlyList<PartSpec> specs,
        float cellLength,
        bool editing = false,
        int tray = 0,
        bool trayOverflow = false,
        int tiles = 0,
        IReadOnlyList<int>? cells = null,
        IReadOnlyList<string?>? labels = null,
        IReadOnlyList<int>? tileCounts = null,
        BarSection? only = null,
        IReadOnlyDictionary<string, int>? trayMore = null,
        IReadOnlyDictionary<string, (int Count, bool Overflow)>? trays = null) =>
        new(dpi, area, area, edge, config, 1f, editing, specs, new Measures
        {
            Trays = trays,
            Tiles = tileCounts?.Sum() ?? tiles,
            TileCounts = tileCounts is null ? null : [.. tileCounts],
            // A label as the fake text below measures it, at the bar's label size.
            Labels = labels is null || edge.IsVertical()
                ? null
                : [.. labels.Select(label => string.IsNullOrWhiteSpace(label) ? 0f : MathF.Ceiling(label.Length * LabelPxFor(dpi, config, 1f) * 0.6f))],
            Tray = tray,
            TrayOverflow = trayOverflow,
            TrayMore = trayMore,
            // One cell each unless `cells` says otherwise: a widget of several (now playing is
            // seven) is what tells a cell's rectangle apart from its widget's block.
            Cells =
            [
                .. specs.Select((spec, index) => spec.Kind.IsIsland()
                    ? new IslandCells(cells is not null && index < cells.Count ? Math.Max(cells[index], 1) : 1, cellLength)
                    : (IslandCells?)null),
            ],
            Text = (text, px, _) => new Vector2(MathF.Ceiling(text.Length * px * 0.6f), MathF.Ceiling(px * LineFactor)),
            SearchWords = MathF.Ceiling(SearchPill.WindowsPlaceholder.Length * SearchPill.TextPx(CellPx(dpi, config), default) * 0.6f),
        }, only);

    /// <summary>
    /// pins: what a layout was made from beyond its specs, edge, text scale and edit mode, which it
    /// keeps itself: enough to lay the same bar out again with a part added (<see cref="WithNewPin"/>).
    /// <paramref name="TilesPer"/> is how many app buttons each part holds, in the parts' order.
    /// </summary>
    private sealed record Remake(uint Dpi, RECT Monitor, RECT Area, Config Config, Measures Measures, int[] TilesPer);

    /// <summary>What the layout needs to know about the content, per part where it differs.</summary>
    private sealed class Measures
    {
        public required int Tiles;

        /// <summary>app buttons: how many each part holds, per part; null for a pin one each and the Apps widget the rest.</summary>
        public int[]? TileCounts;

        /// <summary>app buttons: each tile's label as measured, in px; 0 for an icon alone, and null where none has one.</summary>
        public float[]? Labels;
        public required int Tray;
        public required bool TrayOverflow;

        /// <summary>§347: each tray part's own icons on the bar and chevron, by its key (Content.Trays); one it doesn't name has <see cref="Tray"/> and <see cref="TrayOverflow"/>.</summary>
        public IReadOnlyDictionary<string, (int Count, bool Overflow)>? Trays;

        /// <summary>How many icons the tray part <paramref name="key"/> shows (<see cref="Trays"/>).</summary>
        public int TrayOf(string key) => Trays is { } trays && trays.TryGetValue(key, out var own) ? own.Count : Tray;

        /// <summary>Whether the tray part <paramref name="key"/> has icons behind its chevron (<see cref="Trays"/>).</summary>
        public bool TrayOverflowOf(string key) => Trays is { } trays && trays.TryGetValue(key, out var own) ? own.Overflow : TrayOverflow;

        /// <summary>tray: the cells each tray part takes beyond its icons while one is dragged, by its key (Content.TrayMore).</summary>
        public IReadOnlyDictionary<string, int>? TrayMore;
        public required IslandCells?[] Cells;
        public required Func<string, float, Weight, Vector2> Text;

        /// <summary>search: how long the search box's words are, in px, as it draws them (<see cref="SearchPill.WordsPx"/>); 0 where no part is a box.</summary>
        public float SearchWords;
    }

    /// <summary>
    /// The app icon size in px a layout for <paramref name="dpi"/> uses, so an
    /// island's style can be settled before the layout exists.
    /// </summary>
    public static float IconPx(uint dpi, Config config) => Round(config.IconDips() * (dpi / 96f));

    /// <summary>
    /// tray: the size a notification icon is drawn at — the taskbar's own icons' (<see cref="Fit.SystemIconFor"/>),
    /// Windows 11's 16 DIP on its 48-DIP bar beside the network's, the sound's and the bell, in
    /// proportion on any other, never larger than an app's icon. Its cell is that and a face's
    /// padding along the bar (<see cref="TrayStep"/>), the bar's thickness across it (<see cref="TrayCell"/>).
    /// <para>
    /// The person, 2026-10-09: "the icons that are not pins, like network, tray icons,
    /// notifications, etc in the taskbar are too big." It was an app icon's 24 DIP since 2026-10-08,
    /// Windows' 16 DIP before that, and before that four tenths of its cell.
    /// </para>
    /// <para>
    /// The dock asks each app for its picture at this size as well (<see cref="Tray.IconSize"/>),
    /// because a tray icon is one bitmap an app hands over rather than a group the shell can pick
    /// a size from: drawn at anything but the size it was captured at, it softens.
    /// </para>
    /// </summary>
    public static float TrayIconPx(uint dpi, Config config) =>
        MathF.Min(MathF.Min(IconPx(dpi, config), SmallWellPx(dpi, config)), Fit.SystemIconFor(SmallWellPx(dpi, config), dpi / 96f));

    /// <summary>
    /// The bar's thickness in px: the configured height, but never less than
    /// the icon with a 4-DIP margin either side.
    /// </summary>
    public static float ThicknessPx(uint dpi, Config config) =>
        MathF.Max(ThicknessFor(dpi, HeightOf(config)), IconPx(dpi, config) + 2f * Round(IconMarginDip * (dpi / 96f)));

    /// <summary>
    /// The strip a bar laid out with <paramref name="config"/> keeps along its edge, in px, as
    /// <see cref="Reserve"/> says once it is laid out: its thickness, and the room a floating bar
    /// stands off the edge.
    /// </summary>
    public static float ReserveFor(uint dpi, Config config) =>
        ThicknessPx(dpi, config) + (config.StyleOrDefault == BarStyle.Floating ? Round(FloatMarginDip * (dpi / 96f)) : 0f);

    /// <summary>
    /// The thickness in px of a bar <paramref name="heightDips"/> tall, within
    /// <see cref="Config.MinHeight"/> and <see cref="Config.MaxHeight"/>: what a
    /// height drag shows before the icon's minimum is applied.
    /// </summary>
    public static float ThicknessFor(uint dpi, float heightDips) =>
        Round(Math.Clamp(float.IsFinite(heightDips) ? heightDips : BaseHeightDip, Config.MinHeight, Config.MaxHeight) * (dpi / 96f));

    /// <summary>The height in whole DIPs, within the allowed range, of a bar <paramref name="thicknessPx"/> thick: a drag's result.</summary>
    public static int HeightFor(uint dpi, float thicknessPx) =>
        (int)Math.Clamp(Round(thicknessPx / (dpi / 96f)), Config.MinHeight, Config.MaxHeight);

    /// <summary>
    /// The window of a bar <paramref name="thickness"/> px thick standing in
    /// <paramref name="area"/> on <paramref name="edge"/>, in screen px, as a
    /// layout places it: the bar and the tooltip room on its inner side. A
    /// floating bar's thickness is the room it floats over too (<see cref="Reserve"/>). While
    /// a bar's height is dragged the dock keeps every window at the tallest
    /// bar's (see <see cref="Config.MaxHeight"/>), so no window changes size
    /// with every step of the drag.
    /// </summary>
    public static RECT WindowFor(uint dpi, RECT area, Edge edge, float textScale, float thickness)
    {
        var vertical = edge.IsVertical();
        var depth = DepthFor(area, vertical, thickness, TooltipRoomFor(dpi, edge, textScale));
        var along = vertical ? area.bottom - area.top : area.right - area.left;
        var width = vertical ? depth : along;
        var height = vertical ? along : depth;
        var x = edge == Edge.Right ? area.right - width : area.left;
        var y = edge == Edge.Bottom ? area.bottom - height : area.top;
        return new RECT { left = x, top = y, right = x + width, bottom = y + height };
    }

    /// <summary>
    /// §358: the window a bar stands in on <paramref name="edge"/> of <paramref name="area"/> for as long as
    /// the area stays as it is (the flicker design's rule R2): the most thickness the edge can take —
    /// a bar <see cref="Config.MaxHeight"/> thick, and the room a floating bar stands off the edge —
    /// with the tooltips' room on its inner side (<see cref="WindowFor"/>). Another height, another
    /// type of bar, a height dragged: the bar is laid out in it again, the window still.
    /// </summary>
    public static RECT FrameFor(uint dpi, RECT area, Edge edge, float textScale) =>
        WindowFor(dpi, area, edge, textScale, ThicknessFor(dpi, Config.MaxHeight) + Round(FloatMarginDip * (dpi / 96f)));

    /// <summary>
    /// The room kept on the bar's inner side for tooltips, in px: three lines
    /// above a horizontal bar, a tooltip's width beside a vertical one.
    /// </summary>
    private static float TooltipRoomFor(uint dpi, Edge edge, float textScale)
    {
        var scale = dpi / 96f;
        float Px(float dip) => Round(dip * scale);
        if (edge.IsVertical())
        {
            return Px(TooltipGapDip) + 2f * Px(TooltipPadXDip) + Px(TooltipWidthVerticalDip);
        }
        var lines = TooltipLines * MathF.Ceiling(TooltipTextDip * (textScale > 0f ? textScale : 1f) * TooltipLineFactor);
        return MathF.Ceiling((lines + 2f * TooltipPadYDip + TooltipGapDip) * scale);
    }

    /// <summary>
    /// The window's depth across the bar: the bar and the tooltip room, but
    /// never more than the display has across, so a tall vertical bar on a
    /// narrow display keeps less room for tooltips, which stay inside.
    /// </summary>
    private static int DepthFor(RECT area, bool vertical, float thickness, float tooltipRoom) =>
        Math.Min(
            (int)MathF.Ceiling(thickness + tooltipRoom),
            Math.Max(vertical ? area.right - area.left : area.bottom - area.top, (int)MathF.Ceiling(thickness)));

    /// <summary>
    /// What an island cell gets across the bar, in px: as tall as every other
    /// highlight on the bar (40 DIPs on the default 48-DIP bar), so island
    /// cells line up with the buttons beside them. The dock builds its island
    /// styles with this before the layout exists.
    /// </summary>
    public static float IslandThicknessPx(uint dpi, Config config) => SmallWellPx(dpi, config);

    /// <summary>
    /// The style the island of part <paramref name="part"/> draws its cells for
    /// on this bar: the bar's cell and look, with the range that part may be
    /// given — the one it was measured and laid out with (<see cref="Measure"/>)
    /// — so an island paints for the room it got. The scene asks for this, so it
    /// shows surfaces of exactly the cells the layout gave.
    /// <para>
    /// It carried only a width somebody had pinned. A range that is not one width
    /// but still decides the size — a minimum over what the content asks for, a
    /// maximum under it — was measured with and drawn without: the widget painted
    /// itself at its natural size into a slot the range had made larger, and the
    /// rest of the slot stood empty. On the now-playing widget that was the words
    /// cut to a few letters and a third of the widget blank before its buttons.
    /// </para>
    /// </summary>
    public IslandStyle StyleFor(int part, in Look look) => new(Scale, Vertical, IslandThickness, Icon, look)
    {
        Room = part >= 0 && part < specs.Count ? RoomOf(specs[part]) : WidgetBounds.Any,
        Screen = part >= 0 && part < specs.Count && OnItsDisplay(specs[part].Kind) ? Screen : 0,
        // A connected bar's widgets have no corners (IslandStyle.Square), nor a flush one's.
        Square = Connected || Flush,
        PlateRadius = PlateRadius,
        Edge = Edge,
    };

    /// <summary>The display this bar stands on (its HMONITOR); 0 where the rectangle it was given is on none.</summary>
    public nint Screen { get; }

    /// <summary>
    /// Whether a part's drawing depends on the display it is drawn for: the brightness, which shows
    /// its own display's level, and may set that display alone. Every other widget draws the same on any display, so a layout
    /// shown on two keeps one surface a cell (the surfaces are kept by style).
    /// </summary>
    private static bool OnItsDisplay(BarKind kind) => kind == BarKind.Brightness;

    /// <summary>The display whose bounds are <paramref name="monitor"/>; 0 for a rectangle on none (a test's made-up one).</summary>
    private static unsafe nint ScreenOf(RECT monitor) =>
        TerraFX.Interop.Windows.Windows.MonitorFromRect(&monitor, MONITOR.MONITOR_DEFAULTTONULL);

    /// <summary>
    /// The widget grid's cell in px: a square whose side is the bar's usable
    /// thickness, the bar less the breathing room it keeps at each edge. A
    /// widget is one cell across the bar and <see cref="CellsFor"/> cells
    /// along it, and a list folder's row is the same square tall, so a widget looks
    /// the same on the bar and in a folder. The dock settles island styles
    /// with this before a layout exists.
    /// </summary>
    public static float CellPx(uint dpi, Config config) => IslandThicknessPx(dpi, config);

    /// <summary>
    /// How many whole cells content <paramref name="contentPx"/> px long needs
    /// in cells <paramref name="cellPx"/> px long: always at least one, and
    /// always rounded up, because a part cell would leave a gap nothing can
    /// fill. A hair of overshoot (a text measured at 88.02 px in 88-px cells)
    /// counts as fitting.
    /// </summary>
    public static int CellsFor(float contentPx, float cellPx)
    {
        if (!(cellPx > 0f) || !float.IsFinite(contentPx) || contentPx <= cellPx + CellSlack)
        {
            return 1;
        }
        return Math.Max(1, (int)MathF.Ceiling(contentPx / cellPx - CellSlack / cellPx));
    }

    /// <summary>
    /// The length in px a widget's cell takes for <paramref name="contentPx"/> of content: exactly
    /// what the content needs, never less than one cell. Not whole cells: a widget has the size it
    /// has, one cell and a bit if that is what its content needs (docs\widgets.md §56).
    /// </summary>
    public static float ExactSpan(float contentPx, float cellPx) =>
        cellPx > 0f
            ? MathF.Max(cellPx, float.IsFinite(contentPx) ? WholeSpan(contentPx) : cellPx)
            : WholeSpan(contentPx);

    /// <summary>
    /// The length in px <paramref name="contentPx"/> of content takes off the grid: its whole px,
    /// a hair of overshoot counting as fitting, one px at least (WidgetShape.Exact, docs\widgets.md §275).
    /// </summary>
    public static float WholeSpan(float contentPx) =>
        float.IsFinite(contentPx) ? MathF.Max(1f, MathF.Ceiling(contentPx - CellSlack)) : 1f;

    /// <summary>The length in px of the cells <paramref name="contentPx"/> of content is given: see <see cref="CellsFor"/>.</summary>
    public static float CellSpan(float contentPx, float cellPx) =>
        cellPx > 0f ? CellsFor(contentPx, cellPx) * cellPx : MathF.Max(1f, MathF.Ceiling(contentPx));

    /// <summary>
    /// How a widget's cells share a width said as one number: the same rule as the range below,
    /// for the callers that have one width and not a range.
    /// <see cref="WidgetItem.AutomaticWidth"/> — or anything under <see cref="WidgetItem.MinWidth"/> —
    /// is no one width, which is any width at all.
    /// </summary>
    public static float[] AllotCells(IReadOnlyList<float> contentPx, int cells, float cellPx) =>
        AllotCells(contentPx, WidgetBounds.Chosen(cells), cellPx);

    /// <summary>
    /// How a widget's cells share the room it was given, in px, one entry per
    /// cell of <paramref name="contentPx"/> (each cell's content length).
    ///
    /// The widget's own width comes first, and it is one line: what its content
    /// works out to, held inside what it may be given
    /// (<c>room.Clamp(natural)</c>). A range nobody narrowed leaves the content
    /// deciding; a range with its ends together is a width someone chose and
    /// wins outright; a minimum above the content gives the widget room it did
    /// not ask for, and a maximum below it makes the widget shorten.
    /// <para>
    /// The maximum is the reason this is a clamp and not a starting point. A
    /// widget already at its maximum must not be handed one more cell because a
    /// cell happened to be going: a maximum that a spare cell can overrule is
    /// not a maximum, and the person who said "never wider than this" would
    /// watch it grow anyway.
    /// </para>
    /// <para>
    /// Then that width is shared out: each of the widget's cells keeps one grid
    /// cell, so the floor is one per cell shown, and the rest go to the cells
    /// that wanted most, largest first, so a widget made wider grows where it
    /// has something to show. Cells past the width are left at 0 and are not
    /// drawn — a widget shortens rather than spilling out.
    /// </para>
    /// <para>
    /// Room that nobody asked for goes to the cell that can use it — the one
    /// that wanted most — and is spread between them where several wanted the
    /// same. It used to go to the last cell, which is invisible when that cell
    /// holds a title and plainly wrong when it holds a button: the media
    /// widget's next-track button came out two cells wide with its glyph
    /// adrift in the middle of the slack.
    /// </para>
    /// </summary>
    public static float[] AllotCells(IReadOnlyList<float> contentPx, WidgetBounds room, float cellPx)
    {
        var count = contentPx.Count;
        var lengths = new float[count];
        if (count == 0)
        {
            return lengths;
        }
        // What the content works out to: the widget's natural width, measured
        // here and never stored, because it is a fact about what the widget has
        // to show and not something anybody types.
        var natural = 0;
        for (var index = 0; index < count; index++)
        {
            natural += CellsFor(contentPx[index], cellPx);
        }
        var cells = room.Clamp(natural);
        if (cells == natural && natural >= count && !room.IsFixed)
        {
            // Nothing to settle: every cell takes what its content needs. Kept
            // apart from the sharing below so that a widget nobody has narrowed
            // comes out to the px its content measured, and not to that rounded
            // up and shared out again.
            for (var index = 0; index < count; index++)
            {
                lengths[index] = ExactSpan(contentPx[index], cellPx);
            }
            return lengths;
        }
        var shown = Math.Min(count, cells);
        var wanted = new int[shown];
        var given = new int[shown];
        for (var index = 0; index < shown; index++)
        {
            wanted[index] = CellsFor(contentPx[index], cellPx);
            given[index] = 1;
        }
        var spare = cells - shown;
        while (spare > 0)
        {
            var most = 0;
            var best = -1;
            for (var index = 0; index < shown; index++)
            {
                if (wanted[index] - given[index] > most)
                {
                    most = wanted[index] - given[index];
                    best = index;
                }
            }
            if (best < 0)
            {
                break;
            }
            given[best]++;
            spare--;
        }
        // Nobody asked for the rest, and the widget is still as wide as it was
        // told to be, so some cell must take it. It goes to the cell that can
        // use it: the one that wanted most, which is the elastic part of a
        // widget — a title, a label, a search box — and never a button, whose
        // glyph would sit in a cell twice its size with the spacing of the row
        // gone uneven and the card dead after it. Where several wanted the same
        // (a row of drives, a row of buttons) it is spread one cell at a time,
        // so no single cell ends in a lump of room it cannot fill.
        if (spare > 0)
        {
            var most = 0;
            for (var index = 0; index < shown; index++)
            {
                most = Math.Max(most, wanted[index]);
            }
            var takers = new List<int>();
            for (var index = 0; index < shown; index++)
            {
                if (wanted[index] == most)
                {
                    takers.Add(index);
                }
            }
            for (var turn = 0; spare > 0; turn++)
            {
                given[takers[turn % takers.Count]]++;
                spare--;
            }
        }
        var side = cellPx > 0f ? cellPx : 1f;
        for (var index = 0; index < shown; index++)
        {
            lengths[index] = given[index] * side;
        }
        return lengths;
    }

    /// <summary>
    /// The text size in DIPs a widget uses in a cell <paramref name="cellDips"/>
    /// DIPs tall: the taskbar's 12 DIPs on the 48-DIP bar's 44-DIP cell, and in
    /// proportion on any other, so a widget's content grows with the square it
    /// fills. Never below 11, and capped so a very tall bar stays a taskbar.
    /// </summary>
    public static float CellTextDips(float cellDips) =>
        Math.Clamp(CardTextDip * (float.IsFinite(cellDips) ? cellDips : BaseCellDip) / BaseCellDip, MinTextDip, MaxCellTextDip);

    /// <summary>
    /// app buttons: a label's font size in px — the cell's text size (<see cref="CellTextDips"/>),
    /// so it fills the square it stands in rather than keeping the same 12 DIPs on a bar three
    /// times as thick.
    /// </summary>
    public static float LabelPxFor(uint dpi, Config config, float textScale) =>
        CellTextDips(SmallWellDips(config)) * (textScale > 0f ? textScale : 1f) * (dpi / 96f);

    /// <summary>app buttons: the size this bar's labels are drawn and measured at (<see cref="LabelPxFor"/>).</summary>
    public float LabelPx { get; }

    /// <summary>
    /// The bar's text size in DIPs: 12 on the 48-DIP bar, stepping with the
    /// square root of the bar's height (14 at 64, 17 at 96, 22 at 160) and
    /// never below 11 (a bar up to 44 DIPs).
    /// </summary>
    public static float TextDipsFor(Config config) =>
        MathF.Max(MinTextDip, Round(BaseTextDip * GrowthFor(config)));

    /// <summary>
    /// A system glyph's size in px: 16 DIPs grown with the bar, never less than
    /// 16 DIPs, never more than the tray's highlight. A notification icon is not
    /// one of these — it is an app's icon and is drawn an app icon's size
    /// (<see cref="TrayIconPx"/>).
    /// </summary>
    public static float SmallIconPx(uint dpi, Config config)
    {
        var scale = dpi / 96f;
        var grown = MathF.Max(Round(SmallIconDip * scale), Round(SmallIconDip * scale * GrowthFor(config)));
        return MathF.Min(grown, SmallWellPx(dpi, config));
    }

    /// <summary>The index of the part keyed <paramref name="key"/>, or -1.</summary>
    public int PartIndex(string key) => specs.FindIndex(spec => spec.Key == key);

    /// <summary>
    /// The first part of <paramref name="kind"/> with anything on the bar;
    /// null for a section that isn't placed, has nothing to show, or is an
    /// island that gave way for room.
    /// </summary>
    public Part? GetPart(BarKind kind)
    {
        foreach (var index in placed)
        {
            if (specs[index].Kind == kind)
            {
                return PartAt(index);
            }
        }
        return null;
    }

    /// <summary>Part <paramref name="index"/> of the specs, whatever order <see cref="Parts"/> lists.</summary>
    public Part PartAt(int index) => new(specs[index].Key, specs[index].Kind, LeftOf(index), widths[index], CountOf(index));

    /// <summary>Whether part <paramref name="index"/> takes room on the bar.</summary>
    public bool Placed(int index) => index >= 0 && index < specs.Count && widths[index] > 0f;

    /// <summary>Whether part <paramref name="index"/> has nothing to show and only holds its place in edit mode.</summary>
    public bool Empty(int index) => index >= 0 && index < specs.Count && empty[index];

    /// <summary>search: whether part <paramref name="index"/> is Windows' search button shown as Windows 11's search box.</summary>
    public bool Boxed(int index) => index >= 0 && index < specs.Count && boxed[index];

    /// <summary>
    /// search: whether part <paramref name="index"/> shows a search box, Windows' search button's.
    /// The box fills the part's whole slot, edge to edge and the card's height: it
    /// is the part's card, so the part wears no other (Scene); a widget's gap from its neighbours as every widget (§515).
    /// A box kept in from its slot showed the card round it as a faint second edge on all four sides
    /// (the person, 2026-10-11: "there is a faint edge on the top and bottom and left and right").
    /// </summary>
    public bool SearchBoxed(int index) => index >= 0 && index < specs.Count && searchBoxes[index] && Placed(index) && !empty[index];

    /// <summary>
    /// search: a search box's corners: the card's where the widgets wear cards, as the box is the
    /// part's card, so no other card's corner shows past it; a control's elsewhere (SearchPill.Radius).
    /// </summary>
    public float SearchBoxRadius => Flush ? 0f : Cards && CardRadius > 0f ? CardRadius : SearchPill.Radius(CellSide);

    /// <summary>
    /// search: the search box <paramref name="hit"/> is on, in window px, which lights up as a pill
    /// rather than as a button or a card: Windows' search button shown as the box. Empty for
    /// anything else.
    /// </summary>
    public RectF SearchBoxRect(Hit hit)
    {
        var index = PartOf(hit);
        if (index < 0 || empty[index])
        {
            return default;
        }
        return hit.Kind switch
        {
            HitKind.Search when boxed[index] => ItemRect(hit),
            _ => default,
        };
    }

    /// <summary>Pixels for <paramref name="dip"/> button DIPs at this layout's icon size.</summary>
    public float UnitPx(float dip) => Round(dip * Unit);

    /// <summary>Pixels for <paramref name="dip"/> DIPs grown with the bar, as the tray, clock and desktops are.</summary>
    public float SoftPx(float dip) => Round(dip * Soft);

    /// <summary>
    /// tray: where the <paramref name="index"/>th shown icon's cell starts inside the tray block,
    /// the chevron's cell counted first when the bar has one.
    /// </summary>
    private float TrayStart(int index, int part) => (TrayChevronOf(part) > 0f ? trayStep : 0f) + index * trayStep;

    /// <summary><paramref name="r"/> (this layout's px) on the screen, every pixel it touches.</summary>
    public RECT OnScreen(RectF r) => new()
    {
        left = X + (int)MathF.Floor(r.X),
        top = Y + (int)MathF.Floor(r.Y),
        right = X + (int)MathF.Ceiling(r.X + r.Width),
        bottom = Y + (int)MathF.Ceiling(r.Y + r.Height),
    };

    /// <summary>
    /// The rectangle of what <paramref name="hit"/> names, at rest, in window
    /// pixels: a button's highlight (app, Start, search, task view, tray icon,
    /// chevron, system, clock), a desktop button, an island cell, the
    /// show-desktop sliver, the resize grip, an edit badge, or the bar.
    /// Empty when there is no such thing, and for an item that doesn't fit:
    /// anything past the bar's ends.
    /// </summary>
    public RectF ItemRect(Hit hit)
    {
        switch (hit.Kind)
        {
            case HitKind.Panel:
                return PanelRect;
            case HitKind.ResizeGrip:
                return GripRect;
            case HitKind.Add:
                return hit.Index >= 0 && hit.Index < Adds().Count ? Adds()[hit.Index].Rect : default;
        }
        var index = PartOf(hit);
        if (index < 0)
        {
            return default;
        }
        var rect = Locate(hit, index);
        return !rect.IsEmpty && Fits(rect) ? rect : default;
    }

    /// <summary>A <paramref name="size"/> px square centred in <paramref name="hit"/>'s rectangle; empty when that is.</summary>
    public RectF IconBox(Hit hit, float size)
    {
        var item = ItemRect(hit);
        if (item.IsEmpty)
        {
            return default;
        }
        // app buttons: a labelled one's icon stands where a button's own would, at its start, and
        // its label after it (TileLabel).
        if (hit.Kind == HitKind.Tile && Labelled(hit.Index))
        {
            item = Vertical ? item with { Height = Well } : item with { Width = Well };
        }
        return Centred(item, size, size);
    }

    /// <summary>The picture in tray cell <paramref name="index"/>: <see cref="TrayIconSize"/> px, centred in its cell.</summary>
    public RectF TrayIconRect(int index) => IconBox(Hit.Tray(index), TrayIconSize);

    /// <summary>
    /// The picture in cell <paramref name="index"/> of tray part <paramref name="part"/> (<see cref="TrayParts"/>).
    /// A tray a drag has made shorter (<see cref="TrayMoreOf"/> below 0) keeps every icon's place, the
    /// last past its end, as the others close up into the cell it gave (Scene.TrayDragIn): at the
    /// bar's end that place is past the bar, so the last icon wasn't drawn at all while the dragged
    /// one was over the chevron or the overflow. It is, where its closed-up place fits.
    /// </summary>
    public RectF TrayIconRect(int index, int part)
    {
        var hit = Hit.Tray(index).In(part);
        var box = IconBox(hit, TrayIconSize);
        if (!box.IsEmpty || TrayMoreOf(part) >= 0 || part < 0 || part >= specs.Count || !Placed(part))
        {
            return box;
        }
        var rest = Locate(hit, part);
        var back = TrayMoreOf(part) * trayStep;
        return !rest.IsEmpty && Fits(Vertical ? rest with { Y = rest.Y + back } : rest with { X = rest.X + back })
            ? Centred(rest, TrayIconSize, TrayIconSize)
            : default;
    }

    /// <summary>
    /// App button <paramref name="index"/>'s whole cell — one grid square, which is what it wears a
    /// card over. <see cref="ItemRect"/> of the same button is its highlight, which is smaller and
    /// is the taskbar's own; the card is the room, as it is for every other widget. Empty where the
    /// button is, including one past the bar's end.
    /// </summary>
    public RectF TileCell(int index)
    {
        var hit = Hit.Tile(index);
        var part = PartOf(hit);
        // The same answer the button itself gives, so a card is never drawn for one that is not.
        if (part < 0 || ItemRect(hit).IsEmpty)
        {
            return default;
        }
        var left = LeftOf(part) + tileOffsets[index];
        return Along(left, left + tileLengths[index]);
    }

    /// <summary>app buttons: the cell "…" stands in, which its card is drawn over; empty while every button has room.</summary>
    public RectF OverflowCell()
    {
        if (overflowPart < 0 || ItemRect(Hit.AppOverflow).IsEmpty)
        {
            return default;
        }
        var left = LeftOf(overflowPart) + overflowOffset;
        return Along(left, left + Slot);
    }

    /// <summary>
    /// app buttons: where tile <paramref name="index"/>'s label is drawn — after its icon's square,
    /// to a little before the button's end, as tall as its highlight. Empty for a button without a
    /// label (<see cref="Labelled"/>) and for one the bar has no room for.
    /// </summary>
    public RectF TileLabel(int index)
    {
        if (!Labelled(index) || ItemRect(Hit.Tile(index)) is not { IsEmpty: false } item)
        {
            return default;
        }
        var end = SoftPx(LabelEndDip);
        return Vertical
            ? new RectF(item.X, item.Y + Well, item.Width, MathF.Max(0f, item.Height - Well - end))
            : new RectF(item.X + Well, item.Y, MathF.Max(0f, item.Width - Well - end), item.Height);
    }

    /// <summary>app buttons: whether tile <paramref name="index"/> has a label beside its icon, and so is longer than a cell.</summary>
    public bool Labelled(int index) => index >= 0 && index < tileCells.Length && tileCells[index] > 0;

    /// <summary>How many cells the widget in <paramref name="part"/> has; 0 for anything that is not a widget of cells.</summary>
    public int CellCount(int part) => part >= 0 && part < cellLengths.Length ? cellLengths[part]?.Length ?? 0 : 0;

    /// <summary>
    /// The card behind the island in part <paramref name="part"/>: its whole
    /// part, as tall as a highlight, so the card is exactly the widget's cells
    /// — a square for a one-cell widget. When some cells don't fit, the card
    /// holds the ones that do, as far as the bar's end allows; empty when none do.
    /// </summary>
    public RectF CardRect(int part)
    {
        if (!Placed(part) || cellLengths[part] is not { } lengths)
        {
            return default;
        }
        var first = -1;
        var last = -1;
        for (var cell = 0; cell < lengths.Length; cell++)
        {
            if (!ItemRect(Hit.Cell(part, cell)).IsEmpty)
            {
                first = first < 0 ? cell : first;
                last = cell;
            }
        }
        if (first < 0)
        {
            return default;
        }
        var starts = cellStarts[part]!;
        var left = LeftOf(part);
        return Along(left + starts[first], left + starts[last] + lengths[last]);
    }

    /// <summary>
    /// Part <paramref name="part"/>'s block in edit mode: its whole length (as
    /// much as is shown), as tall as a highlight. Empty for a part not on the bar.
    /// </summary>
    public RectF BlockRect(int part)
    {
        if (!Placed(part))
        {
            return default;
        }
        var left = LeftOf(part);
        return Along(left, left + widths[part]);
    }

    /// <summary>
    /// What a widget's own flyout opens under: the whole block of the part <paramref name="hit"/>
    /// landed in, never the one cell that was pressed. A widget of one cell has the same rectangle
    /// either way, which is why this went unseen: the now-playing widget is seven cells, so its
    /// panel opened under the artwork when the artwork was clicked and under the next-track button
    /// when that was — "the now playing flyover is not properly centered above the widget".
    /// <para>
    /// This is for a popup that belongs to the whole widget. A popup about one item — a pinned
    /// app's jump list, a running window's preview, a tray icon's menu — keeps <see
    /// cref="ItemRect"/>: the pins' tiles and the tray's icons are cells of one part, so a block
    /// there would point at the middle of the whole run instead of at the button being pointed at.
    /// </para>
    /// </summary>
    public RectF FlyoutRect(Hit hit)
    {
        var part = PartOf(hit);
        if (part < 0)
        {
            return default;
        }
        // A part with no block (it gave way for room) leaves the cell as the best answer there is.
        var block = BlockRect(part);
        return block.IsEmpty ? ItemRect(hit) : block;
    }

    /// <summary>
    /// folders: the "Add" marker for a widget going to <paramref name="slot"/> (counted as
    /// <see cref="DropSlot"/> counts): a line across the bar between the two parts of its section it
    /// goes between, just before the section's first or after its last, or in the middle of an empty
    /// section's room — its own room in edit mode, the point it would grow from at rest.
    /// <para>
    /// It is all "along the bar", never "left": on a vertical bar the end is the bottom, and on a
    /// mirrored (right-to-left) bar the start is where its own reading begins.
    /// </para>
    /// </summary>
    public RectF InsertionRect(BarSlot slot)
    {
        var thick = MathF.Max(2f, Round(3f * Scale));
        var across = MathF.Max(1f, Thickness - 2f * Round(ZoneInsetDip * Scale));
        var members = Members(-1).Where(member => specs[member].Section == slot.Section).ToList();
        float along;
        if (members.Count == 0)
        {
            along = sectionFrom[(int)slot.Section] + sectionLength[(int)slot.Section] / 2f;
        }
        else
        {
            var before = members.FindIndex(member => RankOf(member, -1) >= slot.Index);
            // §501, §515: past a section's last widget or before its first, the line stands where the new widget's gap
            // will be; between two, in their gap's middle.
            along = before < 0 ? RightOf(members[^1]) + (widgetGap / 2f)
                : before == 0 ? LeftOf(members[0]) - (widgetGap / 2f)
                : (RightOf(members[before - 1]) + LeftOf(members[before])) / 2f;
        }
        along = Math.Clamp(along, thick / 2f, MathF.Max(thick / 2f, length - thick / 2f));
        return Across(Round(along - thick / 2f), thick, across);
    }

    /// <summary>
    /// pins: where one new pin dropped at <paramref name="slot"/> (counted as <see cref="DropSlot"/>
    /// counts) would stand, at the size it will have: its button's cell in this bar laid out again
    /// with the pin in it (<see cref="WithNewPin"/>) — one cell of the bar, or what a pin's room makes
    /// it — at the very place it will land. Empty where the bar would have no room to show it.
    /// </summary>
    public RectF LandingRect(BarSlot slot) => Landing(slot).Rect;

    /// <summary>
    /// pins: <see cref="LandingRect"/>, with where every part of this bar would start along it once
    /// the pin is in — the room it would open, by part (a part the new layout does not show keeps
    /// its own start). Only its own section makes room: the others stand where they stood, the middle
    /// staying centred as it grows.
    /// </summary>
    public (RectF Rect, float[] Lefts) Landing(BarSlot slot)
    {
        var index = Math.Clamp(slot.Index, 0, specs.Count);
        var later = WithNewPin(slot);
        var lefts = LeftsWith(later, index);
        // The pin's button where the bar shows it; none where it would give way, or go behind "…".
        var tile = later.tileStarts[index];
        var cell = later.Placed(index) && tile < later.tileLengths.Length ? later.TileCell(tile) : default;
        return (cell, lefts);
    }

    /// <summary>
    /// places: what a widget carried from this bar onto another surface, of any display, takes with
    /// it — its part and what it was measured at here, and this bar's scale (§519): its cells, its app
    /// buttons and their labels, a tray's icons — so the surface it is over can lay itself out again with it
    /// in (<see cref="Landing(BarSlot, Carried)"/>) and open the room it will take. Null for a key
    /// this bar has no part for.
    /// </summary>
    public Carried? Carry(string key)
    {
        var part = PartIndex(key);
        if (part < 0)
        {
            return null;
        }
        var measures = remake.Measures;
        var before = remake.TilesPer.Take(part).Sum();
        var tiles = remake.TilesPer[part];
        return new Carried
        {
            Spec = specs[part],
            Cells = measures.Cells[part],
            Tiles = tiles,
            Labels = measures.Labels is { } words ? [.. words.Skip(before).Take(tiles)] : new float[tiles],
            Tray = specs[part].Kind == BarKind.Tray ? measures.TrayOf(specs[part].Key) : 0,
            TrayOverflow = specs[part].Kind == BarKind.Tray && measures.TrayOverflowOf(specs[part].Key),
            Scale = Scale,
        };
    }

    /// <summary>
    /// places: <see cref="LandingRect"/> for a widget carried here from another surface of the display
    /// (<see cref="Carry"/>): its block in this bar laid out again with it in at <paramref name="slot"/>,
    /// at the size it will have here — empty where the bar would have no room to show it — where
    /// every part of this bar would start along it then (the room it opens), and the empty rooms and
    /// a floating bar's panels then.
    /// </summary>
    public Landed Landing(BarSlot slot, Carried carried)
    {
        var index = Math.Clamp(slot.Index, 0, specs.Count);
        var later = With(slot, carried);
        return new Landed(later.Placed(index) ? later.BlockRect(index) : default, LeftsWith(later, index), later.EmptyRooms(), later.SectionPanels());
    }

    /// <summary>
    /// edit: <see cref="Landing(BarSlot, Carried)"/> for several widgets carried together (the person,
    /// 2026-10-07: "dragging one drags all of them together"), side by side in their order from
    /// <paramref name="slot"/> on: their blocks then, as one rectangle round them all — the landing
    /// sized for the group, empty where none would show —, where every part of this bar would start,
    /// and the empty rooms and a floating bar's panels then.
    /// </summary>
    public Landed Landing(BarSlot slot, IReadOnlyList<Carried> group)
    {
        if (group.Count == 0)
        {
            return new Landed(default, [.. Enumerable.Range(0, specs.Count).Select(LeftOf)], EmptyRooms(), SectionPanels());
        }
        var index = Math.Clamp(slot.Index, 0, specs.Count);
        var later = With(slot, group);
        RectF? round = null;
        for (var at = index; at < index + group.Count; at++)
        {
            if (!later.Placed(at))
            {
                continue;
            }
            var block = later.BlockRect(at);
            round = round is not { } was ? block
                : new RectF(MathF.Min(was.X, block.X), MathF.Min(was.Y, block.Y), MathF.Max(was.Right, block.Right) - MathF.Min(was.X, block.X), MathF.Max(was.Bottom, block.Bottom) - MathF.Min(was.Y, block.Y));
        }
        return new Landed(round ?? default, LeftsWith(later, index, group.Count), later.EmptyRooms(), later.SectionPanels());
    }

    /// <summary>
    /// edit: this bar laid out again without <paramref name="parts"/> — several widgets carried
    /// together, which close up behind them wherever they go (<see cref="Landing(BarSlot, IReadOnlyList{Carried})"/>
    /// on what is left) — everything else measured as it was. Its parts are this bar's but those, in
    /// their order (<see cref="Remaining"/>).
    /// </summary>
    public Layout Without(IReadOnlyCollection<int> parts)
    {
        var gone = parts.Where(part => part >= 0 && part < specs.Count).ToHashSet();
        if (gone.Count == 0)
        {
            return this;
        }
        var keep = Enumerable.Range(0, specs.Count).Where(part => !gone.Contains(part)).ToList();
        var measures = remake.Measures;
        var starts = new int[specs.Count];
        for (var part = 1; part < specs.Count; part++)
        {
            starts[part] = starts[part - 1] + remake.TilesPer[part - 1];
        }
        float[]? labels = measures.Labels is { } words
            ? [.. keep.SelectMany(part => Enumerable.Range(starts[part], remake.TilesPer[part]).Select(tile => tile < words.Length ? words[tile] : 0f))]
            : null;
        // tray: each tray left keeps its own icons (§347, by its key).
        var again = new Measures
        {
            Tiles = keep.Sum(part => remake.TilesPer[part]),
            TileCounts = [.. keep.Select(part => remake.TilesPer[part])],
            Labels = labels,
            Tray = measures.Tray,
            TrayOverflow = measures.TrayOverflow,
            Trays = measures.Trays,
            TrayMore = measures.TrayMore,
            Cells = [.. keep.Select(part => measures.Cells[part])],
            Text = measures.Text,
        };
        return new Layout(remake.Dpi, remake.Monitor, remake.Area, Edge, remake.Config, TextScale, Editing, [.. keep.Select(part => specs[part])], again, Only, GlassCorner);
    }

    /// <summary>
    /// edit: where every part of this bar would start along it as <paramref name="without"/> has
    /// them — this bar laid out again without <paramref name="parts"/> (<see cref="Without"/>), or
    /// that with a group landed in it, by its parts but the landed ones — by this bar's own parts:
    /// one of <paramref name="parts"/>, or one the other does not show, keeps its own start.
    /// </summary>
    public float[] Remaining(IReadOnlyCollection<int> parts, IReadOnlyList<float> without)
    {
        var lefts = new float[specs.Count];
        var at = 0;
        for (var part = 0; part < specs.Count; part++)
        {
            if (parts.Contains(part))
            {
                lefts[part] = LeftOf(part);
                continue;
            }
            lefts[part] = at < without.Count ? without[at] : LeftOf(part);
            at++;
        }
        return lefts;
    }

    /// <summary>Where every part of this bar starts in <paramref name="later"/>, this bar with one more part at <paramref name="index"/>; one it does not show keeps its own.</summary>
    private float[] LeftsWith(Layout later, int index) => LeftsWith(later, index, 1);

    /// <summary>The same with <paramref name="added"/> more parts from <paramref name="index"/> on.</summary>
    private float[] LeftsWith(Layout later, int index, int added)
    {
        var lefts = new float[specs.Count];
        for (var part = 0; part < specs.Count; part++)
        {
            var moved = part < index ? part : part + added;
            lefts[part] = Placed(part) && later.Placed(moved) ? later.LeftOf(moved) : LeftOf(part);
        }
        return lefts;
    }

    /// <summary>
    /// pins: this bar laid out again with one more pin at <paramref name="slot"/> — a pin as a new one
    /// is made (<see cref="BarKind.Pin"/>, its own range), in the slot's section, one app button, no
    /// label, no window — and everything else measured as it was.
    /// </summary>
    internal Layout WithNewPin(BarSlot slot) =>
        With(slot, new Carried { Spec = PartSpec.For(NewPinKey, BarKind.Pin.New()), Tiles = 1, Labels = [0f] });

    /// <summary>
    /// This bar laid out again with <paramref name="carried"/> in it at <paramref name="slot"/>, in the
    /// slot's section and measured as it was where it came from, and everything else measured as it was.
    /// </summary>
    private Layout With(BarSlot slot, Carried carried) => With(slot, [carried]);

    /// <summary>The same with several, side by side in their order from <paramref name="slot"/> on.</summary>
    private Layout With(BarSlot slot, IReadOnlyList<Carried> group)
    {
        // §519: measured on another display, at its scale: in this bar's px.
        group = [.. group.Select(carried => carried.At(Scale))];
        var index = Math.Clamp(slot.Index, 0, specs.Count);
        var given = new List<PartSpec>(specs);
        given.InsertRange(index, group.Select(carried => carried.Spec with { Section = slot.Section }));
        var measures = remake.Measures;
        // Their buttons go among the tiles after those of the parts before them.
        var tile = remake.TilesPer.Take(index).Sum();
        var carriedLabels = group.SelectMany(carried => carried.Labels).ToArray();
        var carriedTiles = group.Sum(carried => carried.Tiles);
        var labels = measures.Labels is { } words ? [.. words.Take(tile), .. carriedLabels, .. words.Skip(tile)]
            : carriedLabels.Any(label => label > 0f) ? [.. new float[tile], .. carriedLabels, .. new float[measures.Tiles - tile]]
            : (float[]?)null;
        // tray: a tray carried in keeps the icons it had where it came from (§347, by its key).
        var trays = new Dictionary<string, (int Count, bool Overflow)>(measures.Trays ?? new Dictionary<string, (int, bool)>());
        foreach (var carried in group.Where(carried => carried.Spec.Kind == BarKind.Tray))
        {
            trays[carried.Spec.Key] = (carried.Tray, carried.TrayOverflow);
        }
        var again = new Measures
        {
            Tiles = measures.Tiles + carriedTiles,
            TileCounts = [.. remake.TilesPer.Take(index), .. group.Select(carried => carried.Tiles), .. remake.TilesPer.Skip(index)],
            Labels = labels,
            Tray = measures.Tray,
            TrayOverflow = measures.TrayOverflow,
            Trays = trays,
            TrayMore = measures.TrayMore,
            Cells = [.. measures.Cells.Take(index), .. group.Select(carried => carried.Cells), .. measures.Cells.Skip(index)],
            Text = measures.Text,
        };
        return new Layout(remake.Dpi, remake.Monitor, remake.Area, Edge, remake.Config, TextScale, Editing, given, again, Only, GlassCorner);
    }

    /// <summary>
    /// edit: this bar with part <paramref name="moving"/> taken off it — carried over another surface
    /// of the display, or a folder standing open: where every other part would start, its sections
    /// closed up behind it (a section it leaves with nothing else in it an empty room again), and a
    /// floating bar's panels then.
    /// </summary>
    public Landed Closed(int moving)
    {
        var order = Members(moving);
        var from = new float[3];
        var lengths = new float[3];
        var lefts = Positions(order, from, lengths);
        var held = Held(order, -1, default);
        return new Landed(default, lefts, EmptyRooms(from, lengths, held), SectionPanels(from, lengths, held));
    }

    /// <summary>places: a widget carried from one surface of the display to another, as <see cref="Carry"/> measured it.</summary>
    public sealed class Carried
    {
        public required PartSpec Spec { get; init; }
        internal IslandCells? Cells { get; init; }
        internal int Tiles { get; init; }
        internal float[] Labels { get; init; } = [];
        internal int Tray { get; init; }
        internal bool TrayOverflow { get; init; }

        /// <summary>§519: the scale of the bar it was measured on (<see cref="Layout.Scale"/>), px per DIP.</summary>
        internal float Scale { get; init; } = 1f;

        /// <summary>
        /// §519: what it measured, in the px of a bar at <paramref name="scale"/> — another display's, at
        /// another DPI: its cells' lengths and its labels grown or shrunk by the two scales' ratio; itself
        /// where they are one scale.
        /// </summary>
        internal Carried At(float scale)
        {
            var ratio = Scale > 0f ? scale / Scale : 1f;
            if (MathF.Abs(ratio - 1f) < 0.001f)
            {
                return this;
            }
            return new Carried
            {
                Spec = Spec,
                Cells = Cells is { } cells ? cells with { CellLength = cells.CellLength * ratio, Lengths = cells.Lengths?.Select(length => length * ratio).ToArray() } : null,
                Tiles = Tiles,
                Labels = [.. Labels.Select(label => MathF.Ceiling(label * ratio))],
                Tray = Tray,
                TrayOverflow = TrayOverflow,
                Scale = scale,
            };
        }
    }

    /// <summary>pins: the key the pin <see cref="WithNewPin"/> lays out stands under: no island's.</summary>
    private const string NewPinKey = "\u0001new-pin";

    /// <summary>
    /// The section a point <paramref name="along"/> the bar is in: the one whose stretch holds it, and
    /// between two, the nearer — so the room between the sections belongs to the sections either
    /// side of it, half each. An empty section's stretch is its own room in edit mode and the point it would
    /// grow from at rest (the bar's start, its centre, its end), so something dragged near the middle
    /// of a bar with nothing in the middle lands there.
    /// </summary>
    public BarSection SectionAt(float along)
    {
        // places: a place's bar is its one section from end to end.
        if (Only is { } one)
        {
            return one;
        }
        var nearest = BarSection.Start;
        var best = float.MaxValue;
        foreach (var section in AllSections)
        {
            var from = sectionFrom[(int)section];
            var to = from + sectionLength[(int)section];
            var distance = along < from ? from - along : along > to ? along - to : 0f;
            if (distance < best)
            {
                (nearest, best) = (section, distance);
            }
        }
        return nearest;
    }

    /// <summary>
    /// Where a block dragged to <paramref name="along"/> would land: the section under the pointer
    /// (<see cref="SectionAt"/>), and in it the nearest gap — before the first of the section's
    /// shown parts whose middle is past the point, else after its last. The index counts every spec
    /// but <paramref name="dragging"/>, those that gave way for room included; in an empty section
    /// it is where that section's entries begin in the list.
    /// <para>
    /// <paramref name="along"/> is <b>the pointer</b>, and it matters that it is not the dragged
    /// block's own middle. The block is kept on the bar while it is carried, so its middle can
    /// only reach half its width from either end — short of the end widget's middle whenever the
    /// block is the wider of the two, which made the first and last slots unreachable by
    /// arithmetic rather than by any rule. The pointer is inside the block it carries, so the gap
    /// still opens where the block is, and both ends are reachable at every width and grab.
    /// </para>
    /// <para>
    /// The half of a part the point falls in is what decides which side of it the block goes, and
    /// the middle half of a part is what offers a folder of the two (<c>PinDrags.InMiddle</c>).
    /// The two meet rather than fight: a point in a part's outer quarter inserts past it and never
    /// joins it, so the end of a section — the outer quarter of its last widget — always inserts.
    /// </para>
    /// </summary>
    public BarSlot DropSlot(float along, int dragging)
    {
        var section = SectionAt(along);
        var members = Members(dragging).Where(member => specs[member].Section == section).ToList();
        var before = members.Count(member => LeftOf(member) + widths[member] / 2f < along);
        var index = before < members.Count ? RankOf(members[before], dragging)
            : members.Count > 0 ? RankOf(members[^1], dragging) + 1
            : Enumerable.Range(0, specs.Count).Count(part => part != dragging && specs[part].Section < section);
        return new BarSlot(section, index);
    }

    /// <summary>
    /// Every part's start along the bar if part <paramref name="moving"/> were moved to
    /// <paramref name="slot"/>, counted as <see cref="DropSlot"/> counts; the moving part's own entry
    /// is its new place. Only the sections it leaves and joins move: each is laid out on its own.
    /// Parts not on the bar keep their entry.
    /// </summary>
    public float[] Arrange(int moving, BarSlot slot) => Positions(Arranged(moving, slot), moving: moving, movedTo: slot.Section);

    /// <summary>The placed parts in bar order with part <paramref name="moving"/> moved to <paramref name="slot"/> (<see cref="Arrange"/>).</summary>
    private List<int> Arranged(int moving, BarSlot slot)
    {
        var order = Members(moving);
        if (Placed(moving))
        {
            var at = order.FindIndex(member => specs[member].Section == slot.Section && RankOf(member, moving) >= slot.Index);
            if (at < 0)
            {
                // After the last of its new section, or where that section would begin.
                var last = order.FindLastIndex(member => specs[member].Section <= slot.Section);
                at = last + 1;
            }
            order.Insert(at, moving);
        }
        return order;
    }

    /// <summary>
    /// edit: the room of every section that holds nothing while the bar is arranged, in the order
    /// of <see cref="BarSection"/> — <see cref="EmptyCells"/> cells long (<see cref="emptyRoom"/>),
    /// as an edit block stands across the bar — where the scene draws an empty block, so a section
    /// with nothing in it shows where it is and where a block dropped into it goes. None at rest,
    /// and none for a place's other sections (<see cref="Only"/>).
    /// </summary>
    public IReadOnlyList<RectF> EmptyRooms() => EmptyRooms(sectionFrom, sectionLength, Held(Members(-1), -1, default));

    /// <summary>
    /// edit: <see cref="EmptyRooms()"/> while part <paramref name="moving"/> is carried to
    /// <paramref name="slot"/>: a section it is carried into holds it and has no empty room, and one
    /// it leaves with nothing else in it has, so the room follows the block.
    /// </summary>
    public IReadOnlyList<RectF> EmptyRooms(int moving, BarSlot slot)
    {
        if (!Placed(moving))
        {
            return EmptyRooms();
        }
        var order = Arranged(moving, slot);
        var from = new float[3];
        var lengths = new float[3];
        Positions(order, from, lengths, moving, slot.Section);
        return EmptyRooms(from, lengths, Held(order, moving, slot.Section));
    }

    /// <summary>
    /// edit: where section <paramref name="section"/> stands, in window px: from its start to its end
    /// along the bar, and the bar's whole thickness across it — an empty one's own room while the bar
    /// is arranged. The section's edit bar stands beside it (Dock.EditRail.cs). Empty for a section
    /// of no length.
    /// </summary>
    public RectF SectionRect(BarSection section)
    {
        var index = (int)section;
        var start = MathF.Max(sectionFrom[index], 0f);
        var end = MathF.Min(sectionFrom[index] + sectionLength[index], length);
        return end > start ? ToWindow(start, 0f, end - start, Thickness) : default;
    }

    /// <summary>
    /// edit: the place at the end of <paramref name="section"/>, counted as <see cref="DropSlot"/>
    /// counts — after its last entry in the list, those that gave way for room included: where the
    /// section's "+" and its direct adds put a widget.
    /// </summary>
    public BarSlot EndSlot(BarSection section) => new(section, specs.Count(spec => spec.Section <= section));

    /// <summary>
    /// edit: the place at the start of <paramref name="section"/>, counted as <see cref="DropSlot"/>
    /// counts — before its first entry in the list: where a "+" before its widgets puts one.
    /// </summary>
    public BarSlot StartSlot(BarSection section) => new(section, specs.Count(spec => spec.Section < section));

    private IReadOnlyList<BarAdd>? adds;

    /// <summary>
    /// edit: every "+" of the bar (the person, 2026-10-23), in its order along it: each section's at
    /// the ends <see cref="AddEnds"/> gives it — a held section's in the room it keeps for them beside
    /// its widgets (<see cref="addRoom"/>), outside a dock's panel; an empty one's alone in the middle
    /// of its room — round, <see cref="addSize"/> across, centred across the bar; with where a widget
    /// it adds goes: before the section's first entry, after its last. None at rest, none for a place's
    /// other sections, none past the end of a bar too full for its sections.
    /// </summary>
    public IReadOnlyList<BarAdd> Adds() => adds ??= AddsOf();

    private List<BarAdd> AddsOf()
    {
        var found = new List<BarAdd>();
        if (addSize <= 0f)
        {
            return found;
        }
        var held = Held(Members(-1), -1, default);
        foreach (var section in AllSections)
        {
            var index = (int)section;
            var (from, to) = (sectionFrom[index], sectionFrom[index] + sectionLength[index]);
            if (to <= from || (Only is { } one && one != section))
            {
                continue;
            }
            foreach (var end in AddEnds(section, held[index]))
            {
                // Its middle along the bar: in its room at the section's end, the button at the
                // room's outer edge, the gap between it and the widgets; or the empty room's middle.
                var middle = end switch
                {
                    AddEnd.Before => from + (addSize / 2f),
                    AddEnd.After => to - (addSize / 2f),
                    _ => (from + to) / 2f,
                };
                var start = Round(middle - (addSize / 2f));
                if (start < 0f || start + addSize > length)
                {
                    continue;
                }
                var slot = end == AddEnd.After ? EndSlot(section) : StartSlot(section);
                found.Add(new BarAdd(section, end, slot, Across(start, addSize, addSize)));
            }
        }
        return found;
    }

    /// <summary>edit: the "+" at window px (<paramref name="x"/>, <paramref name="y"/>), by its index in <see cref="Adds"/>; -1 for none.</summary>
    public int AddAt(float x, float y)
    {
        var all = Adds();
        for (var index = 0; index < all.Count; index++)
        {
            if (all[index].Rect.Contains(x, y))
            {
                return index;
            }
        }
        return -1;
    }

    /// <summary>
    /// floating: each section's panel, its one dock (§511: nothing else splits a section), in the order of
    /// <see cref="BarSection"/> — three, always —,
    /// in window px: from the section's start to its end along the bar, its widgets and the room
    /// it keeps inside either end, and the bar's whole thickness across
    /// it, <see cref="Lift"/> off the screen's edge. A section that holds no widget has none (an
    /// empty rectangle): at rest it takes no room, and in edit mode its empty room stands on its own. All
    /// three empty on a bar that does not float, which is one band from end to end.
    /// </summary>
    public IReadOnlyList<RectF> SectionPanels() => SectionPanels(sectionFrom, sectionLength, Held(Members(-1), -1, default));

    /// <summary>
    /// floating: <see cref="SectionPanels()"/> while part <paramref name="moving"/> is carried to
    /// <paramref name="slot"/>, as <see cref="Arrange"/> lays the sections out then: the one it joins
    /// grows for it, and the one it left shrinks, or loses its panel where it held nothing else.
    /// </summary>
    public IReadOnlyList<RectF> SectionPanels(int moving, BarSlot slot)
    {
        if (!Placed(moving))
        {
            return SectionPanels();
        }
        var order = Arranged(moving, slot);
        var from = new float[3];
        var lengths = new float[3];
        Positions(order, from, lengths, moving, slot.Section);
        return SectionPanels(from, lengths, Held(order, moving, slot.Section));
    }

    private RectF[] SectionPanels(float[] from, float[] lengths, bool[] held)
    {
        var panels = new RectF[3];
        if (!Floating)
        {
            return panels;
        }
        foreach (var section in AllSections)
        {
            var index = (int)section;
            // edit: the section's "+" stand beside its panel, not on it.
            var start = MathF.Max(from[index] + AddLead(section, held[index]), 0f);
            var end = MathF.Min(from[index] + lengths[index] - AddTrail(section, held[index]), length);
            if (held[index] && end > start)
            {
                panels[index] = ToWindow(start, 0f, end - start, Thickness);
            }
        }
        return panels;
    }

    /// <summary>
    /// floating: what a press at rest lands on: each dock (<see cref="SectionPanels()"/>, a section that holds a
    /// widget) and the room under it, to the screen's edge, in window px. Between two docks, and past either end,
    /// the press is the desktop's (Dock's window region).
    /// </summary>
    public IReadOnlyList<RectF> PanelReach() =>
        [.. SectionPanels().Where(rect => !rect.IsEmpty).Select(rect => Edge switch
        {
            Edge.Top => rect with { Y = 0f, Height = rect.Bottom },
            Edge.Left => rect with { X = 0f, Width = rect.Right },
            Edge.Right => rect with { Width = Width - rect.X },
            _ => rect with { Height = Height - rect.Y },
        })];

    /// <summary>Which of the three sections hold at least one of <paramref name="shown"/>.</summary>
    private bool[] Held(IEnumerable<int> shown, int moving, BarSection movedTo)
    {
        var held = new bool[3];
        foreach (var index in shown)
        {
            held[(int)SectionOf(index, moving, movedTo)] = true;
        }
        return held;
    }

    /// <summary>
    /// The empty rooms of a bar whose sections start at <paramref name="from"/> and are
    /// <paramref name="lengths"/> long, <paramref name="held"/> saying which hold a widget: each
    /// section of some length that holds none, standing as an edit block stands (<see cref="Along"/>).
    /// </summary>
    private List<RectF> EmptyRooms(float[] from, float[] lengths, bool[] held)
    {
        var rooms = new List<RectF>();
        foreach (var section in AllSections)
        {
            var index = (int)section;
            var (start, end) = (MathF.Max(from[index], 0f), MathF.Min(from[index] + lengths[index], length));
            // A bar too full for its sections runs them past its end: a room there is not shown.
            if (end > start && !held[index])
            {
                rooms.Add(Along(start, end));
            }
        }
        return rooms;
    }

    /// <summary>The placed parts in bar order, leaving out <paramref name="except"/>.</summary>
    private List<int> Members(int except) => [.. placed.Where(index => index != except)];

    /// <summary>Part <paramref name="part"/>'s index in the list, not counting <paramref name="except"/>.</summary>
    private int RankOf(int part, int except)
    {
        var rank = 0;
        for (var index = 0; index < part && index < specs.Count; index++)
        {
            if (index != except)
            {
                rank++;
            }
        }
        return rank;
    }

    /// <summary>
    /// tray: how far along the bar <paramref name="screen"/> (screen px) lies, where it lies on the bar
    /// itself — its strip, and the room between it and the screen's edge, as <see cref="HitTest"/>
    /// counts them — and null anywhere else: off this bar's window, and in the room the window keeps
    /// over the bar for its tooltips.
    /// <para>
    /// A dragged tray icon asks this (Dock.TrayTargetAt, <see cref="TrayDrags.OnLayout"/>). It measured
    /// the point across the bar from the window's top and took anything up to the bar's thickness,
    /// but the window is deeper than the bar by the tooltips' room (DepthFor), which a bottom bar keeps
    /// above it: the bar itself lay past its thickness from the top and was never a place to drop, and
    /// the empty room over it was. No icon could be dragged out of the overflow onto a bottom bar, nor
    /// moved along it, nor held on its chevron.
    /// </para>
    /// </summary>
    public float? AlongOnBar(POINT screen)
    {
        float x = screen.x - X;
        float y = screen.y - Y;
        var on = Edge switch
        {
            Edge.Top => y >= 0f && y < PanelBottom,
            Edge.Left => x >= 0f && x < PanelRight,
            Edge.Right => x >= PanelLeft && x < Width,
            _ => y >= PanelTop && y < Height,
        };
        var along = Vertical ? y : x;
        return on && along >= 0f && along < length ? along : null;
    }

    /// <summary>
    /// What is under the pointer, in window pixels. The grip band comes first;
    /// an item that doesn't fit (see <see cref="ItemRect"/>) is never hit: the
    /// bar is. The grip comes before the blocks; in edit mode
    /// (<paramref name="editing"/>) a block answers over all of its own room,
    /// where at rest parts of it do nothing.
    /// </summary>
    public Hit? HitTest(float x, float y, bool editing = false)
    {
        // Past the bar's inner side the pointer has left the dock; between
        // the bar and the screen edge it has not, whatever the window says.
        var inside = Edge switch
        {
            Edge.Top => y < PanelBottom,
            Edge.Left => x < PanelRight,
            Edge.Right => x >= PanelLeft,
            _ => y >= PanelTop,
        };
        var along = Vertical ? y : x;
        if (!inside || along < 0f || along >= length)
        {
            return null;
        }
        if (GripRect.Contains(x, y))
        {
            return Hit.ResizeGrip;
        }
        // edit: a section's "+" (Adds), in room no widget reaches over.
        if (editing && AddAt(x, y) is var add and >= 0)
        {
            return Hit.Add(add);
        }

        var under = -1;
        // Backwards, so the last part wins where there wasn't room for all.
        for (var at = placed.Count - 1; at >= 0 && under < 0; at--)
        {
            var index = placed[at];
            if (along >= LeftOf(index) && along < RightOf(index))
            {
                under = index;
            }
        }
        // Past the blocks, the room a widget reaches over (Reach): the gap to its neighbour, the bar's end.
        if (under < 0 && OwnerOf(along) is var owner and >= 0)
        {
            under = owner;
        }
        if (under < 0)
        {
            return Hit.Panel;
        }
        // Into its own block, so the outermost thing in it — a first or last button, icon, cell —
        // takes the room the widget reaches over on that side.
        var offset = Math.Clamp(along - LeftOf(under), 0f, MathF.Max(0f, widths[under] - 0.5f));
        var spec = specs[under];
        Hit hit;
        if (empty[under])
        {
            // A placeholder in edit mode: the part itself.
            hit = KindHit(spec.Kind, tileStarts[under]);
        }
        else
        {
            switch (spec.Kind)
            {
                case var buttons when buttons.IsButtons():
                {
                    // Each button owns the gap after it, so a press between two still reaches one;
                    // "…" as much as any.
                    if (TileAt(under, offset) is not { } button)
                    {
                        return editing ? Hit.Tile(tileStarts[under]).In(under) : Hit.Panel;
                    }
                    hit = button;
                    break;
                }
                case BarKind.Tray:
                    // tray: each cell owns its length along the bar, so no point between two
                    // of them falls through.
                    hit = offset < TrayStart(0, under) || CountOf(under) == 0
                        ? Hit.TrayOverflow
                        : Hit.Tray(Math.Min((int)((offset - TrayStart(0, under)) / MathF.Max(1f, trayStep)), CountOf(under) - 1));
                    break;
                default:
                    if (spec.Kind.IsIsland() || cellStarts[under] is not null)
                    {
                        if (cellStarts[under] is not { } starts)
                        {
                            return Hit.Panel;
                        }
                        // The cells fill the card, so every point in it is one cell's.
                        var cell = starts.Length - 1;
                        while (cell > 0 && offset < starts[cell])
                        {
                            cell--;
                        }
                        hit = new Hit(HitKind.Island, cell);
                    }
                    else
                    {
                        hit = KindHit(spec.Kind, tileStarts[under]);
                    }
                    break;
            }
        }
        hit = Tag(hit, under);
        return ItemRect(hit).IsEmpty ? (editing ? Tag(KindHit(spec.Kind, tileStarts[under]), under) : Hit.Panel) : hit;
    }

    /// <summary>
    /// The part whose reach (<see cref="Reach"/>) <paramref name="along"/> is in, past every block;
    /// -1 where it is the bar's own room.
    /// </summary>
    private int OwnerOf(float along)
    {
        var reach = Reach();
        for (var at = placed.Count - 1; at >= 0; at--)
        {
            var index = placed[at];
            if (reach[index] is var (from, to) && along >= from && along < to)
            {
                return index;
            }
        }
        return -1;
    }

    private (float From, float To)[]? reach;

    /// <summary>
    /// How far along the bar each part takes the pointer, in px: its block, and past it the room to
    /// its neighbour up to the middle of the gap between them, and at the bar's ends the room to the
    /// end — the drawn widgets keep their gaps and inset, the targets do not (Fitts, as Windows 11's
    /// taskbar has it; docs\widgets.md §160). Two parts are neighbours in one section, on a floating
    /// bar its one dock (<see cref="SectionPanels()"/>). The room between two sections — what a spacer grows
    /// to — stays the bar's, for its menu and the edit lasso, and so does the desktop between two
    /// floating docks; a floating dock's widgets reach to its ends and no further. Across the bar every
    /// part already reaches the whole thickness and the screen's edge (<see cref="HitTest"/>). (0, 0)
    /// for a part that is not placed.
    /// </summary>
    private (float From, float To)[] Reach()
    {
        if (this.reach is { } known)
        {
            return known;
        }
        var reach = new (float From, float To)[specs.Count];
        var parts = placed.OrderBy(LeftOf).ToList();
        var docks = SectionPanels();
        foreach (var index in parts)
        {
            reach[index] = (LeftOf(index), RightOf(index));
        }
        for (var at = 0; at < parts.Count; at++)
        {
            var index = parts[at];
            var section = specs[index].Section;
            var previous = at > 0 && specs[parts[at - 1]].Section == section ? parts[at - 1] : -1;
            var next = at + 1 < parts.Count && specs[parts[at + 1]].Section == section ? parts[at + 1] : -1;
            var dock = docks[(int)section];
            var (from, to) = reach[index];
            if (previous >= 0)
            {
                // Two that overlap (a bar with no room for all) keep their blocks.
                from = RightOf(previous) <= LeftOf(index) ? (RightOf(previous) + LeftOf(index)) / 2f : from;
            }
            else if (Floating)
            {
                from = dock.IsEmpty ? from : MathF.Min(from, AlongOf(dock).From);
            }
            else if (specs[index].Section == BarSection.Start)
            {
                from = 0f;
            }
            if (next >= 0)
            {
                to = RightOf(index) <= LeftOf(next) ? (RightOf(index) + LeftOf(next)) / 2f : to;
            }
            else if (Floating)
            {
                to = dock.IsEmpty ? to : MathF.Max(to, AlongOf(dock).To);
            }
            else if (specs[index].Section == BarSection.End)
            {
                to = length;
            }
            reach[index] = (from, to);
        }
        return this.reach = reach;
    }

    /// <summary>A window rectangle's stretch along the bar.</summary>
    private (float From, float To) AlongOf(RectF rect) => Vertical ? (rect.Y, rect.Bottom) : (rect.X, rect.Right);

    /// <summary>
    /// app buttons: the button of part <paramref name="part"/> at <paramref name="offset"/> px into it,
    /// each owning the gap after it, or "…" where it stands there; null past the last of them.
    /// </summary>
    private Hit? TileAt(int part, float offset)
    {
        for (var tile = tileStarts[part]; tile < tileStarts[part] + CountOf(part); tile++)
        {
            if (tileOffsets[tile] >= 0f && offset < tileOffsets[tile] + tileLengths[tile] + TileGap)
            {
                return Hit.Tile(tile);
            }
        }
        return part == overflowPart && offset < overflowOffset + Slot + TileGap ? Hit.AppOverflow : null;
    }

    /// <summary>
    /// Where a dropped pin goes: the gap between tiles nearest <paramref name="along"/>,
    /// a coordinate along the main axis (x on a horizontal dock, y on a vertical
    /// one). <see cref="int.MaxValue"/> when there are no tiles to drop between.
    /// </summary>
    public int Insertion(float along)
    {
        if (GetPart(BarKind.Apps) is not { } part)
        {
            return int.MaxValue;
        }
        var index = PartIndex(part.Key);
        if (index < 0)
        {
            return int.MaxValue;
        }
        // Before the first button whose middle is past the point, by the buttons' own lengths.
        for (var slot = 0; slot < part.Count; slot++)
        {
            var tile = tileStarts[index] + slot;
            if (tileOffsets[tile] >= 0f && along < part.Left + tileOffsets[tile] + tileLengths[tile] / 2f)
            {
                return slot;
            }
        }
        return part.Count;
    }

    /// <summary><see cref="Insertion(float)"/> for a point in window pixels, on any edge.</summary>
    public int Insertion(float x, float y) => Insertion(Vertical ? y : x);

    /// <summary>
    /// How far the whole dock moves to hide: away from the screen centre, right
    /// out of the window, so nothing of it is left to see.
    /// </summary>
    public Vector2 HiddenOffset()
    {
        // A floating bar's panels go past the edge with the room under them, and their shadow.
        var distance = Thickness + 2f + (Floating ? lift + PanelShadowReach : 0f);
        return Edge switch
        {
            Edge.Top => new Vector2(0f, -distance),
            Edge.Left => new Vector2(-distance, 0f),
            Edge.Right => new Vector2(distance, 0f),
            _ => new Vector2(0f, distance),
        };
    }

    /// <summary>
    /// Where a <paramref name="size"/> tooltip for <paramref name="hit"/> goes:
    /// centred on the item, <see cref="LabelGap"/> beyond the bar's inner
    /// side whatever its height, and inside the window.
    /// </summary>
    public RectF TooltipAnchor(Hit hit, Vector2 size) => TooltipAnchor(ItemRect(hit), size);

    /// <summary>
    /// How tall a tooltip may be and stand clear of the bar inside the window: the room on a
    /// horizontal bar's inner side (<see cref="TooltipLines"/> lines), the window's height beside a
    /// vertical one. A taller one is cut to fit (BarTooltips.Fitted): it would cover the bar, or
    /// run past the window and be cut off.
    /// </summary>
    public float TooltipMostHeight => MathF.Max(0f, Edge switch
    {
        Edge.Top => Height - PanelBottom - LabelGap,
        Edge.Bottom => PanelTop - LabelGap,
        _ => Height,
    });

    /// <summary><see cref="TooltipAnchor(Hit, Vector2)"/> for any rectangle on the dock.</summary>
    public RectF TooltipAnchor(RectF item, Vector2 size)
    {
        var centre = item.Center;
        var (x, y) = Edge switch
        {
            Edge.Top => (centre.X - size.X / 2f, PanelBottom + LabelGap),
            Edge.Left => (PanelRight + LabelGap, centre.Y - size.Y / 2f),
            Edge.Right => (PanelLeft - LabelGap - size.X, centre.Y - size.Y / 2f),
            _ => (centre.X - size.X / 2f, PanelTop - LabelGap - size.Y),
        };
        // Min before Max: a tooltip wider than the window starts at its edge.
        x = MathF.Max(0f, MathF.Min(x, Width - size.X));
        y = MathF.Max(0f, MathF.Min(y, Height - size.Y));
        return new RectF(MathF.Floor(x), MathF.Floor(y), size.X, size.Y);
    }

    /// <summary>
    /// A rectangle given along the main axis (<paramref name="along"/>, <paramref name="alongLength"/>)
    /// and centred across the bar (<paramref name="acrossLength"/>), in window pixels.
    /// </summary>
    public RectF Across(float along, float alongLength, float acrossLength) =>
        ToWindow(MathF.Floor(along), MathF.Floor((Thickness - acrossLength) / 2f), alongLength, acrossLength);

    /// <summary>The part <paramref name="hit"/> is in: its own, or the first placed part of its kind.</summary>
    public int PartOf(Hit hit)
    {
        if (hit.Part >= 0)
        {
            return hit.Part < specs.Count ? hit.Part : -1;
        }
        // A tile is one of the pins' or the Apps widget's, by its place among the bar's tiles.
        if (hit.Kind == HitKind.Tile)
        {
            foreach (var index in placed)
            {
                if (specs[index].Kind.IsButtons()
                    && hit.Index >= tileStarts[index] && hit.Index < tileStarts[index] + CountOf(index))
                {
                    return index;
                }
            }
        }
        if (hit.Kind == HitKind.AppOverflow)
        {
            return overflowPart;
        }
        // An island cell always names its part; the taskbar's own buttons are
        // one of a kind on a bar.
        var kind = hit.Kind switch
        {
            HitKind.Tile => BarKind.Apps,
            HitKind.Tray or HitKind.TrayOverflow => BarKind.Tray,
            HitKind.Start => BarKind.Start,
            HitKind.Search => BarKind.Search,
            HitKind.TaskView => BarKind.TaskView,
            HitKind.WindowsButton => (BarKind)hit.Index,
            HitKind.ShowDesktop => BarKind.ShowDesktop,
            _ => (BarKind?)null,
        };
        if (kind is not { } wanted)
        {
            return -1;
        }
        foreach (var index in placed)
        {
            if (specs[index].Kind == wanted)
            {
                return index;
            }
        }
        return -1;
    }

    /// <summary>Pixels per button DIP for <paramref name="config"/>'s icon size.</summary>
    private static float UnitFor(uint dpi, Config config) => dpi / 96f * config.IconDips() / BaseIconDip;

    private static float HeightOf(Config config) => (config.Height > 0 ? config.Height : BaseHeightDip) - LiftDip(config);

    /// <summary>The bar's thickness in DIPs, unrounded: the height, or what the icon needs.</summary>
    private static float EffectiveHeight(Config config) =>
        config.BarThickness - LiftDip(config);

    /// <summary>
    /// §688 (the person, 2026-11-12: "the taskbar and dock even though both are set to 42px size take up a different amount of
    /// space, the windows move. this should not be the case"): the room a floating dock stands off the edge, DIP, taken out of
    /// its size — its panel that much thinner — so the strip it keeps (<see cref="Reserve"/>, its thickness and that room) is
    /// its size, as a taskbar's is; none for a bar that does not float.
    /// </summary>
    private static float LiftDip(Config config) => config.StyleOrDefault == BarStyle.Floating ? FloatMarginDip : 0f;

    /// <summary>
    /// The highlight's side in px: the icon and 8 DIPs around it at the icon's
    /// scale, but no closer to the bar's edges than a twelfth of the bar, and
    /// no less than 2 DIPs, and never smaller than the icon.
    /// </summary>
    private static float WellPx(uint dpi, Config config)
    {
        var thickness = ThicknessPx(dpi, config);
        var margin = MathF.Max(Round(thickness * MarginShare), CardInsetPx(dpi, config));
        return MathF.Max(IconPx(dpi, config), MathF.Min(Round(WellDip * UnitFor(dpi, config)), thickness - 2f * margin));
    }

    /// <summary><see cref="WellPx"/> in DIPs, unrounded, for decisions every display must agree on.</summary>
    private static float WellDips(Config config)
    {
        var height = EffectiveHeight(config);
        var icon = config.IconDips();
        var margin = MathF.Max(height * MarginShare, CardInsetDips(config));
        return MathF.Max(icon, MathF.Min(WellDip * icon / BaseIconDip, height - 2f * margin));
    }

    /// <summary>The square root of the bar's height over 48 DIPs: how much text, tray, clock and desktops grow.</summary>
    private static float GrowthFor(Config config) => MathF.Min(1f, MathF.Sqrt(EffectiveHeight(config) / BaseHeightDip));

    /// <summary>
    /// The room in px between a card and the bar's edge across it, and between the first or last card
    /// of a section and the bar's end: <see cref="CardInsetShare"/> of the thickness, between 2 and
    /// <see cref="MaxCardInsetDip"/> DIPs, held so a card stays at least as tall as the icon. The
    /// floating dock has the 2 DIPs of breath only (its panel keeps the rest, <see cref="pad"/>). A
    /// flush bar (<see cref="FlushFor"/>) keeps none: its cards fill it edge to edge.
    /// </summary>
    internal static float CardInsetPx(uint dpi, Config config)
    {
        var scale = dpi / 96f;
        var breath = Round(BreathDip * scale);
        if (config.StyleOrDefault == BarStyle.Floating)
        {
            return breath;
        }
        if (FlushFor(config))
        {
            return 0f;
        }
        var thickness = ThicknessPx(dpi, config);
        var wanted = MathF.Min(Round(thickness * CardInsetShare), Round(MaxCardInsetDip * scale));
        var room = MathF.Floor((thickness - IconPx(dpi, config)) / 2f);
        return MathF.Max(breath, MathF.Min(wanted, room));
    }

    /// <summary>
    /// Whether a bar of <paramref name="config"/> is flush: an islands bar its layout spaced 0 (§533), at any height. Its
    /// cards keep no room off the screen's edge, across the bar or at its ends, nor between them, and fill it edge to edge,
    /// and every card, plate and box on it is square — §123's look (the person, 2026-10-11: "the island should basically go
    /// away … the edges of the widgets should then also be sharp without any corner radius"), which every islands bar under
    /// <see cref="ThinDip"/> wore by itself until §533 (the person, 2026-11-07: "the automatic switch to no space when the
    /// taskbar is small should not happen anymore"). A floating dock is never flush: its panel is what stands apart.
    /// </summary>
    internal static bool FlushFor(Config config) =>
        config.StyleOrDefault == BarStyle.Islands && config.BarSpacing == 0;

    /// <summary><see cref="CardInsetPx"/> in DIPs, unrounded.</summary>
    internal static float CardInsetDips(Config config)
    {
        if (config.StyleOrDefault == BarStyle.Floating)
        {
            return BreathDip;
        }
        if (FlushFor(config))
        {
            return 0f;
        }
        var height = EffectiveHeight(config);
        var wanted = MathF.Min(height * CardInsetShare, MaxCardInsetDip);
        var room = (height - config.IconDips()) / 2f;
        return MathF.Max(BreathDip, MathF.Min(wanted, room));
    }

    /// <summary>
    /// The tray's and clock's highlight height in px: 40 DIPs
    /// grown with the bar, but no taller than an app's highlight.
    /// </summary>
    private static float SmallWellPx(uint dpi, Config config) =>
        MathF.Max(1, ThicknessPx(dpi, config) - 2f * CardInsetPx(dpi, config));

    /// <summary><see cref="SmallWellPx"/> in DIPs, unrounded.</summary>
    private static float SmallWellDips(Config config) =>
        MathF.Max(1, EffectiveHeight(config) - 2f * CardInsetDips(config));

    /// <summary>The display's width over its height, within reason; 16:9 when unknown.</summary>
    private static float Aspect(RECT monitor)
    {
        var width = monitor.right - monitor.left;
        var height = monitor.bottom - monitor.top;
        return width > 0 && height > 0 ? Math.Clamp((float)width / height, 0.5f, 3.5f) : 16f / 9f;
    }

    /// <summary>
    /// tray: how many cells the tray part takes — one per icon on this bar. <see cref="Content.Tray"/>
    /// is the split's answer already (Dock.ReadTray), so nothing here asks Windows again: an icon
    /// whose app Windows keeps in the overflow, but which the person dragged onto the bar or set to
    /// Show, needs a cell like every other. Counting only the ones Windows promotes left those
    /// icons without one, and the bar drew nothing where they stood.
    /// </summary>
    internal static int TrayCells(Content content) => content.Tray?.Count ?? 0;

    /// <summary>
    /// app buttons: how many of the bar's buttons each of <paramref name="parts"/> holds, each button
    /// standing in the part its <paramref name="owners"/> entry names — a pin's key, or an Apps
    /// widget's. A layout may hold the open apps twice, and each widget is named by its own buttons
    /// (Dock.TilesFor), so each shows them all; it was the first alone that did, as though the Apps
    /// widget were one of a kind on a bar. A button that names no part is the first Apps widget's.
    /// </summary>
    internal static int[] TileCountsOf(IReadOnlyList<PartSpec> parts, IReadOnlyList<string?> owners)
    {
        var counts = new int[parts.Count];
        var first = -1;
        for (var index = 0; index < parts.Count; index++)
        {
            var key = parts[index].Key;
            counts[index] = parts[index].Kind.IsButtons() ? owners.Count(owner => owner == key) : 0;
            if (first < 0 && parts[index].Kind == BarKind.Apps)
            {
                first = index;
            }
        }
        if (first >= 0)
        {
            counts[first] += owners.Count(owner => owner is null);
        }
        return counts;
    }

    /// <summary>Everything a part's length depends on, read from <paramref name="content"/>.</summary>
    private static Measures Measure(Gfx gfx, uint dpi, Config config, Look look, Edge edge, IReadOnlyList<PartSpec> parts, Content content)
    {
        // §507: the bar's own text (an app's label) in its layout's Taskbar widgets' font; each widget measures in its own.
        using var typeface = gfx.Wearing(look.Typeface);
        var textScale = look.TextScale > 0f ? look.TextScale : 1f;
        var style = new IslandStyle(dpi / 96f, edge.IsVertical(), IslandThicknessPx(dpi, config), IconPx(dpi, config), look);
        var cells = new IslandCells?[parts.Count];
        for (var index = 0; index < parts.Count; index++)
        {
            var part = parts[index];
            if (part.Kind.IsIsland() && content.Islands?.Find(part) is { } island)
            {
                // With the width chosen for this part, so the island measures
                // and paints for the room it will get (see StyleFor).
                cells[index] = island.Measure(style with { Room = RoomOf(part) });
            }
        }
        // app buttons: which part each tile stands in — a pin's own, or the Apps widget's —, and how
        // long each label is, at the size it is drawn (LabelPx). None across a vertical bar, which has
        // no room along it for a word beside an icon.
        var tileCounts = content.TileParts is { } owners && owners.Count == content.Tiles.Count ? TileCountsOf(parts, owners) : null;
        float[]? labels = null;
        if (content.Labels is { } words && words.Count == content.Tiles.Count && !edge.IsVertical() && words.Any(word => !string.IsNullOrWhiteSpace(word)))
        {
            var px = LabelPxFor(dpi, config, textScale);
            labels = [.. words.Select(word => string.IsNullOrWhiteSpace(word) ? 0f : MathF.Max(1f, gfx.Measure(word, px, Weight.Regular).X))];
        }
        // search: the box's words, in the font they are drawn in, for its length (§510).
        var searchWords = parts.Any(part => part is { Box: true, Kind: BarKind.Search }) ? SearchPill.WordsPx(gfx, CellPx(dpi, config), look) : 0f;
        return new Measures
        {
            SearchWords = searchWords,
            Tiles = content.Tiles.Count,
            TileCounts = tileCounts,
            Labels = labels,
            // tray: one cell per icon the split left on this bar, and the chevron only for what is
            // actually behind it (TrayCells).
            Tray = TrayCells(content),
            TrayOverflow = content.TrayOverflow,
            // §347: each tray its own, by its key.
            Trays = content.Trays?.ToDictionary(tray => tray.Key, tray => (tray.Value.Shown.Count, tray.Value.Overflow)),
            TrayMore = content.TrayMore,
            Cells = cells,
            Text = gfx.Measure,
        };
    }

    /// <summary>
    /// The cells <paramref name="spec"/> may be given: its own range. A fixed width needs no gate
    /// here — clamping into a range whose ends meet is that range.
    /// <para>
    /// The default value of the struct reads as any width. A part of no cells is not a thing, and
    /// a caller that built a spec without saying meant "I have not said", not "none".
    /// </para>
    /// </summary>
    private static WidgetBounds RoomOf(in PartSpec spec) =>
        spec.Room.Most >= WidgetItem.MinWidth ? spec.Room : WidgetBounds.Any;

    /// <summary>
    /// app buttons: the cells a part of app buttons is held to, out of its own
    /// <paramref name="range"/>. Its fewest always: a room kept whole. Its most only where it says
    /// something about the buttons:
    /// <list type="bullet">
    /// <item>a pin's never — its width is its one button's, the icon's, and where its windows are
    /// not combined their buttons take the room they need, as the open apps' do. Held to its one
    /// cell, a pin with a labelled window's button put it behind "…" on a bar with room to spare
    /// (the person, 2026-10-05: "it shows me the three ... even though there is plenty of
    /// space").</item>
    /// </list>
    /// Anything else keeps its range: the open apps' most is the most there is
    /// (<see cref="WidgetItem.MostCells"/>), which holds nothing (<see cref="WidgetBounds.Open"/>), so the
    /// bar's free room is theirs, as on Windows 11's taskbar, whose apps take what the bar has.
    /// </summary>
    internal static WidgetBounds ButtonRoom(BarKind kind, WidgetBounds range) => kind switch
    {
        BarKind.Pin => range with { Most = int.MaxValue },
        _ => range,
    };

    /// <summary>
    /// app buttons: the one length labelled buttons of <paramref name="lengths"/> are cut to so that
    /// together they give up <paramref name="over"/> px — the longest first, each down to the next,
    /// so the ones cut all come out alike, as Windows 11's labelled buttons narrow together as the
    /// taskbar fills —, never below <paramref name="least"/>; that is the answer where even all of
    /// them at it would not give up enough. Nothing to give up is the longest length.
    /// </summary>
    internal static float EvenCut(IReadOnlyList<float> lengths, float over, float least)
    {
        var longest = lengths.OrderByDescending(length => length).ToList();
        if (longest.Count == 0)
        {
            return least;
        }
        if (over <= 0f)
        {
            return MathF.Max(least, longest[0]);
        }
        var sum = 0f;
        for (var cut = 1; cut <= longest.Count; cut++)
        {
            sum += longest[cut - 1];
            var next = cut < longest.Count ? MathF.Max(least, longest[cut]) : least;
            var length = (sum - over) / cut;
            if (length >= next)
            {
                return length;
            }
        }
        return least;
    }

    /// <summary>
    /// The one width <paramref name="spec"/> is pinned to, in cells, or
    /// <see cref="WidgetItem.AutomaticWidth"/> where its range lets the content decide: its range read
    /// as the one number the places that still ask this question want.
    /// </summary>
    private static int WidthOf(in PartSpec spec) => spec.Cells;

    /// <summary>
    /// tray: whether the tray <paramref name="spec"/>, where it has a set width, has room for fewer
    /// than the <paramref name="icons"/> it has to show, without a chevron: whole <paramref name="cell"/>
    /// squares of its <paramref name="gridCell"/> cells, side by side (the sum the held branch of the
    /// constructor makes). Its last icons then go into the overflow, so the chevron must stand there
    /// to open it. Each tray of a bar is asked on its own.
    /// </summary>
    internal static bool TraySpills(in PartSpec spec, int icons, float gridCell, float cell)
    {
        if (icons <= 0 || spec.Kind != BarKind.Tray)
        {
            return false;
        }
        var chosen = WidthOf(spec);
        return chosen >= WidgetItem.MinWidth && (int)MathF.Floor(chosen * gridCell / MathF.Max(1f, cell)) < icons;
    }

    /// <summary><paramref name="hit"/> in part <paramref name="part"/>.</summary>
    private static Hit Tag(Hit hit, int part) => hit.In(part);

    /// <summary>The hit that stands for a whole part of <paramref name="kind"/>.</summary>
    private static Hit KindHit(BarKind kind, int index) => kind switch
    {
        BarKind.Start => Hit.Start,
        BarKind.Search => Hit.Search,
        BarKind.TaskView => Hit.TaskView,
        _ when kind.IsWindowsButton() => Hit.WindowsButton(kind),
        BarKind.Apps or BarKind.Pin => Hit.Tile(index),
        BarKind.Tray => Hit.TrayOverflow,
        BarKind.ShowDesktop => Hit.ShowDesktop,
        _ => new Hit(HitKind.Island, index),
    };

    // Parts is only there once the constructor has placed them.
    private float LeftOf(int index) => Parts is null ? 0f : Parts[index].Left;

    private float RightOf(int index) => LeftOf(index) + widths[index];

    private int CountOf(int index) => Parts is null ? 0 : Parts[index].Count;

    /// <summary>Where <paramref name="hit"/>'s item would be in part <paramref name="index"/>, whether or not it fits.</summary>
    private RectF Locate(Hit hit, int index)
    {
        if (!Placed(index))
        {
            return default;
        }
        var left = LeftOf(index);
        var width = widths[index];
        var kind = specs[index].Kind;
        if (empty[index])
        {
            // A placeholder answers for its part as a whole.
            return BlockRect(index);
        }
        switch (hit.Kind)
        {
            // search: the box itself, which is what lights up and what is pressed: its whole slot (SearchBoxed).
            case HitKind.Search when kind == BarKind.Search && boxed[index]:
                return Across(left, width, IslandThickness);
            case HitKind.Start when kind == BarKind.Start:
            case HitKind.Search when kind == BarKind.Search:
            case HitKind.TaskView when kind == BarKind.TaskView:
            case HitKind.WindowsButton when kind == (BarKind)hit.Index:
                return Button(left, width);
            case HitKind.Tile when kind.IsButtons()
                && hit.Index >= tileStarts[index] && hit.Index < tileStarts[index] + CountOf(index):
                return tileOffsets[hit.Index] < 0f ? default : TileButton(left + tileOffsets[hit.Index], tileLengths[hit.Index]);
            case HitKind.AppOverflow when index == overflowPart:
                return Button(left + overflowOffset, Slot);
            // tray: the whole cell, its card, its plate and its place: the icon and its padding along
            // the bar (TrayStep), the bar's usable thickness across it (TrayCell), centred across. It
            // was a square until the person, 2026-10-22: "they dont have to have the 1by1 ratio
            // anymore" (§290).
            case HitKind.Tray when kind == BarKind.Tray && hit.Index >= 0 && hit.Index < CountOf(index):
                return Across(left + TrayStart(hit.Index, index), trayStep, TrayCell);
            case HitKind.TrayOverflow when kind == BarKind.Tray && TrayChevronOf(index) > 0f:
                return Across(left, trayStep, TrayCell);
            case HitKind.ShowDesktop when kind == BarKind.ShowDesktop:
                return Across(left, width, Thickness);
            case HitKind.Island when cellLengths[index] is { } lengths && hit.Index >= 0 && hit.Index < lengths.Length:
                return Across(left + cellStarts[index]![hit.Index], lengths[hit.Index], IslandThickness);
        }
        return default;
    }

    /// <summary>A button's highlight: a <see cref="Well"/> square centred in its cell.</summary>
    private RectF Button(float along, float cellLength) =>
        Across(along + MathF.Floor((cellLength - Well) / 2f), Well, Well);

    /// <summary>
    /// app buttons: a button's highlight, <paramref name="length"/> px of cells long: a square for an
    /// icon alone (<see cref="Button"/>), and for a labelled one the whole of it, kept in from both
    /// ends as much as a square is from its cell's.
    /// </summary>
    private RectF TileButton(float along, float length)
    {
        if (length <= Slot)
        {
            return Button(along, Slot);
        }
        var inset = MathF.Floor((Slot - Well) / 2f);
        return Across(along + inset, length - 2f * inset, Well);
    }

    /// <summary>
    /// app buttons: the room between two buttons of one part, and before its "…": a widget's
    /// (<see cref="WidgetGap"/>, §501). The buttons of a part stand side by side, each on a card of its own and lit on
    /// its own cell (<see cref="Plates"/>, §602). The same as between two pins, so a pin and its running app take the same
    /// room, and an open app stands as far from the next as a pin does.
    /// </summary>
    private float TileGap => widgetGap;

    /// <summary>
    /// app buttons: how long tiles [<paramref name="first"/>, first + <paramref name="many"/>) are in a
    /// row: each its length, a member's gap from the next (<see cref="TileGap"/>); those
    /// <paramref name="hidden"/> says are behind "…" take nothing.
    /// </summary>
    private float TileRun(int first, int many, bool[]? hidden = null)
    {
        var run = 0f;
        var shown = 0;
        for (var tile = first; tile < first + many && tile < tileLengths.Length; tile++)
        {
            if (hidden is not null && hidden[tile])
            {
                continue;
            }
            run += (shown++ > 0 ? TileGap : 0f) + tileLengths[tile];
        }
        return run;
    }

    /// <summary>
    /// From <paramref name="from"/> to <paramref name="to"/> along the bar, as tall as a
    /// highlight, cut to the bar's ends: a bar with no room left runs its sections one after the
    /// other from the start (SectionStarts), so what runs off the end simply runs off it.
    /// </summary>
    private RectF Along(float from, float to)
    {
        from = MathF.Max(from, 0f);
        to = MathF.Min(to, length);
        return to > from ? Across(from, to - from, SmallWell) : default;
    }

    private static RectF Centred(RectF outer, float width, float height) => new(
        outer.X + MathF.Floor((outer.Width - width) / 2f),
        outer.Y + MathF.Floor((outer.Height - height) / 2f),
        width,
        height);

    /// <summary>
    /// Whether something at <paramref name="rect"/> is shown. An icon cut by the bar's end would
    /// still look clickable, so it goes entirely.
    /// </summary>
    private bool Fits(RectF rect)
    {
        var from = Vertical ? rect.Y : rect.X;
        var to = Vertical ? rect.Bottom : rect.Right;
        return from >= 0f && to <= length;
    }

    /// <summary>
    /// From the bar's own space — <paramref name="along"/> the edge, <paramref name="away"/> from
    /// the bar's outer side — to window pixels. A floating bar's outer side is <see cref="Lift"/>
    /// off the screen's edge.
    /// </summary>
    private RectF ToWindow(float along, float away, float alongLength, float awayLength) =>
        OnEdge(along, away + lift, alongLength, awayLength);

    /// <summary>
    /// From edge space — <paramref name="along"/> the edge, <paramref name="away"/>
    /// from the screen's edge itself — to window pixels.
    /// </summary>
    private RectF OnEdge(float along, float away, float alongLength, float awayLength) => Edge switch
    {
        Edge.Top => new RectF(along, away, alongLength, awayLength),
        Edge.Left => new RectF(away, along, awayLength, alongLength),
        Edge.Right => new RectF(Width - away - awayLength, along, awayLength, alongLength),
        _ => new RectF(along, Height - away - awayLength, alongLength, awayLength),
    };

    /// <summary>
    /// Where every part starts. Each section is laid out on its own, its parts in the order they are
    /// in with the room between them: the start's from the bar's start, the end's to the bar's end,
    /// the middle's centred on the bar (<see cref="SectionStarts"/>). <paramref name="moving"/>, where
    /// it is a part, stands in <paramref name="movedTo"/> instead of its own section, as a dragged
    /// block does over another. <paramref name="from"/> and <paramref name="lengths"/>, where given,
    /// are filled with where each section starts and how long it is.
    /// </summary>
    private float[] Positions(IEnumerable<int> shown, float[]? from = null, float[]? lengths = null, int moving = -1, BarSection movedTo = default)
    {
        var lefts = new float[specs.Count];
        if (Parts is not null)
        {
            for (var index = 0; index < specs.Count; index++)
            {
                lefts[index] = LeftOf(index);
            }
        }
        var order = shown.ToList();
        var sized = SectionLengths(order, moving, movedTo);
        var starts = SectionStarts(sized);
        var held = Held(order, moving, movedTo);
        foreach (var section in AllSections)
        {
            // floating: a panel's widgets start its own room in from its edge; edit: past the section's
            // "+" before them (AddEnds).
            var at = starts[(int)section] + (held[(int)section] ? pad : 0f) + AddLead(section, held[(int)section]);
            var previous = -1;
            foreach (var index in order.Where(index => SectionOf(index, moving, movedTo) == section))
            {
                at += previous >= 0 ? widgetGap : 0f;
                lefts[index] = at;
                at += widths[index];
                previous = index;
            }
            if (from is not null && lengths is not null)
            {
                from[(int)section] = starts[(int)section];
                lengths[(int)section] = sized[(int)section];
            }
        }
        return lefts;
    }

    /// <summary>The three sections, in the order the bar shows them.</summary>
    private static readonly BarSection[] AllSections = [BarSection.Start, BarSection.Middle, BarSection.End];

    /// <summary>The section part <paramref name="index"/> stands in: its own, or <paramref name="movedTo"/> where it is the one <paramref name="moving"/>.</summary>
    private BarSection SectionOf(int index, int moving = -1, BarSection movedTo = default) =>
        index == moving ? movedTo : specs[index].Section;

    /// <summary>
    /// How long each section is with the parts <paramref name="shown"/>: its parts and the room
    /// between them. An empty section is nothing at rest and its own room in edit mode
    /// (<see cref="emptyRoom"/>), a place's other sections nothing at all. A floating bar's
    /// section that holds a widget is its panel: the room the panel keeps inside either end of it
    /// is part of it (<see cref="pad"/>); an empty one has no panel and no such room.
    /// </summary>
    private float[] SectionLengths(IReadOnlyList<int> shown, int moving, BarSection movedTo)
    {
        var lengths = new float[3];
        var last = new[] { -1, -1, -1 };
        foreach (var index in shown)
        {
            var section = (int)SectionOf(index, moving, movedTo);
            lengths[section] += (last[section] >= 0 ? widgetGap : 0f) + widths[index];
            last[section] = index;
        }
        for (var section = 0; section < 3; section++)
        {
            if (emptyRoom > 0f && last[section] < 0 && (Only is null || (int)Only == section))
            {
                lengths[section] = emptyRoom;
            }
            if (last[section] >= 0)
            {
                // edit: and its "+" at the ends the rule gives it (AddEnds).
                lengths[section] += (2f * pad) + AddLead((BarSection)section, true) + AddTrail((BarSection)section, true);
            }
        }
        return lengths;
    }

    /// <summary>
    /// edit: which ends of a section have a "+" (the person, 2026-10-23: "they should be left and right
    /// in the center bar ... (if no widget is there only show one add button) and they should be on
    /// the right of the left group and on the left of the right group"): its inner ends, the ones
    /// facing the bar's middle — after the start's last widget, before the end's first, either side
    /// of the middle's — and one alone, in its room, for a section that holds nothing. Along the bar,
    /// so the start is the top of a bar on a side; the bar is not mirrored right to left (§36), and
    /// neither is this.
    /// </summary>
    public static IReadOnlyList<AddEnd> AddEnds(BarSection section, bool held) =>
        !held ? [AddEnd.Alone]
        : section switch
        {
            BarSection.Start => [AddEnd.After],
            BarSection.End => [AddEnd.Before],
            _ => [AddEnd.Before, AddEnd.After],
        };

    /// <summary>edit: the room a "+" before <paramref name="section"/>'s widgets takes, where it <paramref name="held"/> any; 0 at rest.</summary>
    private float AddLead(BarSection section, bool held) => held && AddEnds(section, held).Contains(AddEnd.Before) ? addRoom : 0f;

    /// <summary>edit: the room a "+" after <paramref name="section"/>'s widgets takes, where it <paramref name="held"/> any; 0 at rest.</summary>
    private float AddTrail(BarSection section, bool held) => held && AddEnds(section, held).Contains(AddEnd.After) ? addRoom : 0f;

    /// <summary>
    /// The least room between two sections that both hold something: 14 DIP on a docked bar (§515: the one place
    /// the bar keeps more than a widget's gap), and on a floating one the room between two docks.
    /// </summary>
    public float SectionGap => Floating ? panelGap : sectionGap;

    /// <summary>
    /// The room three sections of <paramref name="lengths"/> take, one after the other: their
    /// lengths, and the gap between each two that both hold something.
    /// </summary>
    private float Total(float[] lengths)
    {
        var total = 0f;
        var before = false;
        foreach (var length in lengths)
        {
            if (length <= 0f)
            {
                continue;
            }
            total += (before ? SectionGap : 0f) + length;
            before = true;
        }
        return total;
    }

    /// <summary>
    /// Where each section of <paramref name="lengths"/> starts along the bar, each on its own: the
    /// start's at the bar's start, the end's ending at the bar's end, and the middle's centred on
    /// the bar — exactly, as long as that keeps it clear of the other two by a gap; otherwise as
    /// near the centre as does (the person's decision, 2026-10-05: exactly centred as long as it
    /// fits, then shifted as little as keeps it off the left and the right). An empty
    /// section starts where it would grow from: the bar's start, its centre, its end. Where the
    /// three cannot fit at all — the bar has given up all it can (the constructor) — they run one
    /// after the other from the bar's start and the tail falls off the end.
    /// </summary>
    private float[] SectionStarts(float[] lengths)
    {
        var (start, middle, end) = (lengths[0], lengths[1], lengths[2]);
        var gap = SectionGap;
        // floating: the panels keep off the screen's ends; the middle is still centred on the bar.
        var (first, last) = (ends, length - ends);
        if (Total(lengths) > last - first)
        {
            var middleAt = first + start + (start > 0f && middle > 0f ? gap : 0f);
            var endAt = middleAt + middle + (end > 0f && (start > 0f || middle > 0f) ? gap : 0f);
            return [first, middleAt, endAt];
        }
        var endFrom = last - end;
        var least = start > 0f ? first + start + gap : first;
        var most = (end > 0f ? endFrom - gap : last) - middle;
        var centred = Round((length - middle) / 2f);
        return [first, Math.Clamp(centred, least, MathF.Max(least, most)), endFrom];
    }

    /// <summary>
    /// §512: where part <paramref name="part"/>'s own card stands — its background, its outline, its pattern, its plate at rest
    /// (§506) and its highlight all in this one rectangle —: its card's room (<see cref="CardRect"/>), else its block. Every
    /// widget its own (§515: there are no groups on the bar).
    /// </summary>
    public RectF Plate(int part) => part < 0 ? default : CardRect(part) is { IsEmpty: false } card ? card : BlockRect(part);

    /// <summary>
    /// §602: the cards part <paramref name="part"/> wears, each in its own rectangle: an app button's part — a pin's, the Apps
    /// widget's — one a button (<see cref="TileCell"/>) and one for its "…" (<see cref="OverflowCell"/>), so every two app
    /// buttons stand the same gap apart on the bar, a pin from a pin as an open app from an open app; every other part its one
    /// <see cref="Plate"/>. A part of buttons with none shown (edit mode's placeholder) is its block.
    /// </summary>
    public IReadOnlyList<RectF> Plates(int part)
    {
        if (part < 0 || part >= specs.Count)
        {
            return [];
        }
        var plates = new List<RectF>();
        if (specs[part].Kind.IsButtons() && !empty[part])
        {
            for (var tile = tileStarts[part]; tile < tileStarts[part] + CountOf(part); tile++)
            {
                if (TileCell(tile) is { IsEmpty: false } cell)
                {
                    plates.Add(cell);
                }
            }
            if (part == overflowPart && OverflowCell() is { IsEmpty: false } more)
            {
                plates.Add(more);
            }
        }
        if (plates.Count == 0 && Plate(part) is { IsEmpty: false } plate)
        {
            plates.Add(plate);
        }
        return plates;
    }

    /// <summary>
    /// §621: what the add page's ghost of part <paramref name="part"/> outlines (Scene.ShowGhost): each of its cards on its own
    /// where it wears several (<see cref="Plates"/>: the app buttons a pick of them shows), in its own shape with the gaps between
    /// them, never one outline round them all; one card, its block, as a carried block's ghost. None for a part not on the bar.
    /// </summary>
    public IReadOnlyList<RectF> GhostRects(int part) =>
        Plates(part) is { Count: > 1 } plates ? plates
        : BlockRect(part) is { IsEmpty: false } block ? [block]
        : [];

    private static float Round(float value) => MathF.Round(value, MidpointRounding.AwayFromZero);
}
