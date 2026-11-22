// edit, §520: where everything of the edit bar and its flyouts stands, apart from any window: pure, so the tests measure
// it. §548: one system (docs\widgets.md §548, PanelMetrics) — one spacing ramp (4 · 8 · 12 · 16 · 24), WinUI's type ramp —,
// every value in one place. §575 (the person, 2026-11-08: "the topbar is not extendible anymore and there is no sidemenu
// too"):
//
//  - The bar: one 48 DIP row of one width (§578: whatever is selected, however many errors) — the grip; an entry for each
//    page, a command bar's buttons (its icon, 8, its words; 4 apart), Karpo, Accounts, Display, Layouts, Taskbar,
//    Appearance; §608 a divider while anything is selected; §596 the selection's entries, a folder's and a widget's, entries as the pages' are, in a room kept for them
//    (256 DIP, the rest of it empty, §584 all of it with nothing selected); the errors' room (their icon button while there
//    are any); Add; Done, the one accent button, ending a gutter in. Groups a gutter apart. The entry whose flyout is open is
//    selected: the subtle fill and the 16 × 3 accent pill at its foot. Too wide for its display, the pages' entries show their
//    icon alone, then the selection's room narrows. No search (§578).
//  - A flyout (its own window, §578 centred under its entry): one page, 580 DIP wide, as tall as it is and no taller than the
//    room on its side, scrolling past it — a gutter round it, its groups 24 apart, each heading (Body Strong 14) 8 over its
//    cards, the cards 4 apart, each 48 tall for one line and 60 for two, its icon at 16 and its name at 48, its control 16 in
//    from its end: every control that holds a value 240 × 32, every chevron 20 in from its control's end; §578 a row that only
//    acts its button alone, no card; the add list's search box over its rows, its widgets tiles side by side; §592 a tab strip
//    40 tall, 8 under the card before it and 8 over the chosen tab's cards, its tabs from the cards' start, each its words
//    and 12 either side, 4 apart.

using System.Numerics;
using Karpo.Core;

namespace Karpo.Dock;

/// <summary>
/// §520, §548: the panel's sizes, in DIP — every one a step of the spacing ramp or a size of WinUI's (docs\widgets.md §548's
/// table); nothing of the layout nor the painter is a number of its own.
/// </summary>
internal static class PanelMetrics
{
    // --- The spacing ramp: nothing else stands between two things -----------------------------------

    /// <summary>Card to card; side item to side item (NavigationView's 2 + 2); icon buttons of a command bar; a plate in from the side list's sides.</summary>
    public const float Tight = 4;

    /// <summary>Button to button; a heading to its cards; the header round its controls; the body's top to its first side item and the page's head.</summary>
    public const float Gap = 8;

    /// <summary>Words to their control (WinUI: 12 between a control and its label); a button's padding.</summary>
    public const float Label = 12;

    /// <summary>A card's padding; the page's gutters, either side; the side list's footer's inset; the page's foot (WinUI: 16 between a surface and its edge text).</summary>
    public const float Gutter = 16;

    /// <summary>One group to the next: the page's head (its title, a header card) to its first group, a card to the next heading.</summary>
    public const float Group = 24;

    // --- The type ramp (WinUI's: size / line) --------------------------------------------------------

    public const float Caption = 12;
    public const float CaptionLine = 16;
    public const float Body = 14;
    public const float BodyLine = 20;

    /// <summary>A header card's name.</summary>
    public const float Subtitle = 20;

    public const float SubtitleLine = 28;

    // --- Geometry ------------------------------------------------------------------------------------

    /// <summary>Every card, control, button, plate and tile (WinUI's ControlCornerRadius).</summary>
    public const float Corner = 4;

    /// <summary>A segment's or a spin button's plate inside its control, 2 in: the control's corners less 1.</summary>
    public const float InnerCorner = 3;

    /// <summary>The panel and its popups, the search's suggestions among them (WinUI's OverlayCornerRadius).</summary>
    public const float Overlay = 8;

    /// <summary>A swatch's corners, inside a control.</summary>
    public const float SwatchCorner = 2;

    /// <summary>
    /// §548: one pill, 3 × 16, fully round — §575 under the entry whose flyout stands open, as WinUI's SelectorBar marks its
    /// item (16 wide, 3 tall).
    /// </summary>
    public const float PillWide = 3;

    public const float PillTall = 16;

    /// <summary>§595: a badge after a name (InfoBadge's as a word): 20 tall, a pill, its word 8 in from either end.</summary>
    public const float BadgeTall = 20;
    public const float BadgeInset = 8;

    /// <summary>Every button, box, dropdown, number box and segmented control.</summary>
    public const float Control = 32;

    /// <summary>Every control that holds a value: one width (§520: "similar or same sizes of texts or elements").</summary>
    public const float ControlWide = 240;

    /// <summary>
    /// §638: a choice standing beside its row's (a font's weight beside its family) at the line's end: room for "Extra black" in
    /// Black and its chevron, so the family keeps the one width before it and the line fits the flyout's 580.
    /// </summary>
    public const float PairWide = 140;

    /// <summary>§521: a row's button at its narrowest (WinUI's).</summary>
    public const float ActionLeast = 120;

    public const float Icon = 16;
    public const float Chevron = 12;

    /// <summary>§548: every chevron's middle this far in from its control's end (a dropdown's, a colour button's, a button with a list's, an expander's, a link's, Settings').</summary>
    public const float ChevronIn = 20;

    /// <summary>The room an expander's or a link's chevron takes at a card's end: a 32 DIP button, 4 in.</summary>
    public const float ChevronRoom = Tight + Control;

    /// <summary>
    /// §535: a colour button (the Community Toolkit's ColorPickerButton, a DropDownButton): its colour a swatch 32 × 20 filling
    /// its start, 6 in from its rim, its chevron after it, §548 20 in from its end as every chevron; every colour of the panel
    /// one (a colour row's, a part's, a calendar's).
    /// </summary>
    public const float ColourWide = ColourInset + ColourSwatch + Gap + Chevron + (ChevronIn - (Chevron / 2));

    public const float ColourSwatch = 32;
    public const float ColourInset = 6;

    /// <summary>
    /// §613: a colour's picker, the one popup wider than a value's box (the person, 2026-11-09: "the color selector should be
    /// wider but not as tall anymore"): §627 its swatches' column (six 32 DIP cells, MenuPickers.SwatchesAcross) beside its
    /// mixer's (the rest, its spectrum Flyout.SpectrumDip tall), a rule's room between, the menu's insets (Flyout.Columns.cs);
    /// §613 had twelve swatches to a line over the mixer. Fixed, whatever its rows say, as every control's popup is.
    /// </summary>
    public const float PickerWide = 400;

    /// <summary>A toggle switch, 40 × 20, "On"/"Off" 12 before it in a room of 40.</summary>
    public const float SwitchWide = 40;

    public const float SwitchTall = 20;
    public const float SwitchWords = 40;

    // --- The bar ---------------------------------------------------------------------------------------

    /// <summary>The bar's row: its 32 DIP controls and 8 round them.</summary>
    public const float Head = Gap + Control + Gap;

    /// <summary>"Done" at its narrowest.</summary>
    public const float DoneLeast = 80;

    /// <summary>
    /// §596: the room kept on the bar for the selection's entries, so the bar is one width whatever is selected (the person,
    /// 2026-11-09: "the top edit bar should not change width"): the folder's entry and the widget's, each an entry as any page's
    /// (the person, 2026-11-09: "they should be displayed the same way the others are"), as wide as its words, from its start;
    /// what they leave empty — nothing drawn, nothing pressed (§584: "there should not be a placeholder shown").
    /// </summary>
    public const float SelectionWide = 256;

    /// <summary>
    /// §608: the divider between the pages' entries and the selection's, a command bar's AppBarSeparator: a hairline this tall,
    /// in the bar's middle, 8 either side of it.
    /// </summary>
    public const float DividerTall = 20;

    /// <summary>The icon column: a card's icon this far in from its start.</summary>
    public const float IconAt = 16;

    /// <summary>§682: how much further in a row stands for each step it is under another (a mail folder under its parent).</summary>
    public const float IndentStep = 28;

    /// <summary>The word column: a card's name this far in from its start.</summary>
    public const float NameAt = 48;

    // --- The add page's tiles (§578) ------------------------------------------------------------------

    /// <summary>
    /// §578: a tile of the add page at its narrowest: as many side by side as fit — §593 across the edit bar's width, no longer
    /// three at most across a flyout's 548 DIP; wide enough for a name such as "Energy saver on/off" and its count.
    /// </summary>
    public const float TileLeast = 200;

    /// <summary>§578: a tile's one line: its 32 DIP of a control and a tight space over and under it.</summary>
    public const float Tile = Control + Gap;

    /// <summary>
    /// §578: a tile with a second line (why it is greyed, where it is): Body over Caption, 8 over and under them; §604 one of
    /// more lines as much taller as they are (PanelLayout.Tiles).
    /// </summary>
    public const float TileTwoLines = Gap + BodyLine + CaptionLine + Gap;

    /// <summary>
    /// §628: a tile picturing what it makes (Add a layout's presets) at its narrowest: a bar is wide and low, so one to a line
    /// across a page's cards (548 DIP), two only across a flyout twice as wide.
    /// </summary>
    public const float PictureTileLeast = 400;

    // --- A page under another's entry (§628) -------------------------------------------------------

    /// <summary>
    /// §628: its head at its flyout's top, standing as the page scrolls under it: one plate the whole of which goes back, as
    /// tall as a control, Windows' back arrow 12 in from its start, the page's title in Body Strong 8 after it (BackHead's).
    /// </summary>
    public const float BackHead = Control;

    // --- A flyout ------------------------------------------------------------------------------------

    /// <summary>§575: a page's flyout: a gutter, its cards (as wide as the panel's page was, §548), a gutter.</summary>
    public const float FlyoutWide = 580;

    /// <summary>A page's row of title buttons (a page of actions with no header card).</summary>
    public const float TitleRow = 36;

    /// <summary>A name cut short is never shorter.</summary>
    public const float NameLeast = 40;

    /// <summary>A segmented control's segments 2 in from its rim.</summary>
    public const float SegmentInset = 2;

    /// <summary>A section's heading: one Body Strong line.</summary>
    public const float Heading = BodyLine;

    /// <summary>§592: a tab strip (WinUI's SelectorBar): a control's 32 and a pill's room under it.</summary>
    public const float Tabs = Control + Gap;

    public const float OneLine = 48;

    /// <summary>
    /// Body over Caption, 12 over and under them; §604 a card whose second line wraps into more, or says more lines of its own,
    /// as much taller as they are (PanelLayout.Tall): never words drawn over each other.
    /// </summary>
    public const float TwoLines = 60;

    /// <summary>§529: the header card's tile: its glyph 24, several 20 a step apart, at most three.</summary>
    public const float HeaderTile = 48;

    public const float HeaderGlyph = 24;
    public const float FannedGlyph = 20;

    /// <summary>§529: how far each icon after the first stands along the tile (the tile as much wider), and the most on it.</summary>
    public const float FanStep = 28;

    public const int FannedMost = 3;

    /// <summary>§529: the header card's top: its tile and a gutter round it.</summary>
    public const float HeaderTall = Gutter + HeaderTile + Gutter;

    /// <summary>The page's scroll indicator: 2 DIP, 4 in from the panel's edge, 8 off the viewport's ends, 40 at its shortest.</summary>
    public const float ScrollBar = 2;

    public const float ScrollLeast = 40;

    /// <summary>§551: a row put in order's handle, its grip's six dots in its middle: this wide, a tight space in, its row's control height.</summary>
    public const float Handle = 16;

    public const float HandleIn = Tight;

    /// <summary>§551: how much further in a row put in order stands, its icon and its name, past its handle.</summary>
    public const float HandleLead = Label;

    /// <summary>§551: how near the page's top or foot a row dragged into order scrolls the page under it.</summary>
    public const float DragScrollBand = 40;

    // --- §633: a popup of a control (a dropdown's list, a search's, an Add's, the font list) --------------

    /// <summary>
    /// §633: a settings popup's rows as the pages' are, WinUI's ComboBox's items: a control's 32 tall, 4 apart as cards are,
    /// 4 in from the popup's sides and its top and foot; the icon in the icon column (16), the words in the word column (48,
    /// 16 without icons), a label's 12 at a plate's end, so its end stands a gutter in and a chevron 20 in, as on a page; a
    /// rule a gap's row; a heading a control's row in Body Strong, as a page's.
    /// </summary>
    public static readonly RowMetrics Popup = new(
        Pad: Tight / 2,
        Fill: Control,
        Gap: Tight / 2,
        Inset: Tight,
        PadLeft: IconAt - Tight,
        PadRight: Label,
        Column: NameAt - IconAt,
        Rule: Gap,
        Header: Control,
        HeaderSize: Body);
}

/// <summary>
/// §520: what a point of the bar or a flyout is: a button, a control, a page's row (see <see cref="PanelLayout.HitAt"/>,
/// <see cref="PanelLayout.HitIn"/>).
/// </summary>
internal enum PanelHitKind
{
    None,
    Errors,

    /// <summary>"+ Add": §575 its flyout, the add list (<see cref="PanelPage.Add"/>).</summary>
    Add,
    Done,

    /// <summary>§575: a page's entry on the bar (its page): its flyout opens, or closes again.</summary>
    Nav,

    /// <summary>§575: the add list's own search box, at its flyout's top.</summary>
    Filter,

    /// <summary>§628: a page under another's entry its way back, its head at its flyout's top: that entry's page again.</summary>
    Back,
    Head,
    Toggle,
    Fewer,
    More,
    Tool,
    Pop,
    Act,

    /// <summary>§670: an inline picker's spectrum or colour track (its <see cref="PanelHit.Index"/> the MixPart), dragged.</summary>
    Mix,
    Link,
    TitleAct,
    Page,

    /// <summary>§521: a text box, typed in place.</summary>
    Text,

    /// <summary>§521: a number box's value, typed in place.</summary>
    Number,

    /// <summary>§536: a look's preview: an image a screen reader names, nothing to press (never a stop of the keys).</summary>
    Preview,

    /// <summary>§529: a mixed row's button showing or hiding each widget's own value under it.</summary>
    Each,

    /// <summary>§551: a row put in order's handle at its start (its grip): what it is dragged by, and where Ctrl+Up and Ctrl+Down move it.</summary>
    Handle,

    /// <summary>
    /// §592: a tab of a strip (its index, its key the tab's): a click chooses it; the chosen one alone a stop of the keys, Left,
    /// Right, Home and End choosing along the strip.
    /// </summary>
    Tab,
}

/// <summary>
/// §520: a thing of the bar or the panel that a click or the keys reach: its kind, the menu row's id it sends
/// (<paramref name="Id"/>), an index (a segment, a suggestion), a page (a side item), the row's key on its page.
/// </summary>
internal readonly record struct PanelHit(PanelHitKind Kind, uint Id = 0, int Index = -1, PanelPage Page = default, string Key = "")
{
    public static readonly PanelHit None = default;

    public bool IsNone => Kind == PanelHitKind.None;
}

/// <summary>What the panel is measured with: Gfx's text and widgets in production, a stand-in in a test.</summary>
internal interface IPanelText
{
    /// <summary>How wide one line of <paramref name="text"/> is at <paramref name="px"/>.</summary>
    float Width(string text, float px, bool strong = false);

    /// <summary>§604: how tall <paramref name="text"/> at <paramref name="px"/> stands wrapped at <paramref name="width"/>, every line of it shown, its own line breaks kept.</summary>
    float Tall(string text, float px, float width);

    /// <summary>§571: how tall <paramref name="shows"/>' folder's lines stand (DIP) as the open folder lays them out at <paramref name="scale"/>: what its preview's room holds (LookPreview.BoxDip).</summary>
    float LinesDip(LookShown shows, float scale);
}

/// <summary>A button of the bar: what it is, where it stands (bar px), its words and glyph; §575 an entry's page, and whether it shows its icon alone.</summary>
internal sealed record PanelPart(PanelHitKind Kind, RectF Rect, string Words, string Glyph, PanelPage Page = default, bool Compact = false);

/// <summary>
/// A line of a card on a page: its row, where it stands (page px, the page's top 0), the card's head or a row inside it; §578 a
/// tile of the add page (<paramref name="Tile"/>: a widget among others side by side, a card of its own). §588: no line stands
/// further in than another (§529 to §587 a row nested under a choice a step in, §565 on a well).
/// </summary>
internal sealed record PanelLine(PanelRow Row, RectF Rect, bool Inner, bool Tile = false);

/// <summary>A heading, or a card and its lines (its head, an open expander's rows), on a page (page px).</summary>
internal sealed record PanelBlock(PanelRow Row, RectF Rect, IReadOnlyList<PanelLine> Lines);


/// <summary>
/// §520: what the bar and its flyout show: <paramref name="Errors"/> on the bar; §575 the pages (<paramref name="Content"/>),
/// each an entry; the page whose flyout stands open (<paramref name="Shown"/>; null for none), the expanders open, how far its
/// page is scrolled (px); whether Add has somewhere to add to; what is typed in the add list's own search
/// (<paramref name="Filter"/>).
/// </summary>
internal sealed record PanelInput(int Errors, SettingsContent? Content = null, PanelPage? Shown = null, IReadOnlySet<string>? Open = null, float Scroll = 0, bool CanAdd = true, string Filter = "", IReadOnlyDictionary<string, string>? Searches = null,
    IReadOnlyDictionary<string, PanelFinds>? Finds = null, IReadOnlyDictionary<string, float>? Scrolls = null);

/// <summary>§676: what a lookup's search found (MenuSearch.Rows) and what it says meanwhile ("Searching…", nothing found); by its search's key.</summary>
internal sealed record PanelFinds(IReadOnlyList<OptionFound> Rows, string? Said)
{
    /// <summary>
    /// Before its search is asked: what its finder offers and suggests, the one in force first. §682 (the person, 2026-11-12:
    /// "language inline dropdown does not show the languages when opening, only when searching"): a finder that needs no
    /// letters and asks no service (the languages) what it finds for nothing typed too, as its search would at once.
    /// </summary>
    public static PanelFinds Before(OptionFinder finder)
    {
        IEnumerable<OptionFound> all = finder.Offered.Concat(finder.Suggested);
        if (finder.LeastLetters <= 0 && !finder.Remote && finder.FindAsync("", CancellationToken.None) is { IsCompletedSuccessfully: true } found)
        {
            all = all.Concat(found.Result);
        }
        var rows = all.ToList();
        return new([.. rows.Where(row => row.Chosen), .. rows.Where(row => !row.Chosen)], null);
    }
}

/// <summary>§520, §575: the bar, and the flyout of the entry open, laid out in px at a scale (see the top of the file).</summary>
internal sealed partial class PanelLayout
{
    private readonly IPanelText text;
    private readonly Dictionary<string, PanelRow> rows = new(StringComparer.Ordinal);

    /// <summary>§609, §610: the row a pair (<see cref="PanelRow.Pair"/>) is of, by the pair's key.</summary>
    private readonly Dictionary<string, PanelRow> holders = new(StringComparer.Ordinal);

    /// <param name="room">
    /// The most the bar may be wide (X, px: its display's room, EditDeckPlan.Widest) and a flyout tall (Y, px: the room on its
    /// side, PopupPlace.Room); 0 for no most (<see cref="SizeOf"/>).
    /// </param>
    public PanelLayout(IPanelText text, float scale, PanelInput input, Vector2 room = default)
    {
        this.text = text;
        Scale = scale;
        Input = input;
        var widest = room.X > 0 ? room.X : float.MaxValue;
        Header = Parts(widest);
        Size = new Vector2(Header[^1].Rect.Right + P(PanelMetrics.Gutter), P(PanelMetrics.Head));
        var content = input.Content;
        if (input.Shown is not { } open || content?.Of(open) is not { } shown)
        {
            return;
        }
        Shown = shown;
        // §593: the add page as wide as the bar it hangs from the whole of.
        var wide = open == PanelPage.Add ? Size.X : MathF.Min(P(PanelMetrics.FlyoutWide), MathF.Floor(widest));
        var gutter = P(PanelMetrics.Gutter);
        // §575: the add list's own search over its rows, standing as they scroll under it.
        var top = 0f;
        if (shown.Filter is not null)
        {
            Filter = new RectF(gutter, gutter, wide - (2 * gutter), P(PanelMetrics.Control));
            top = Filter.Bottom;
        }
        // §628: a page under another's entry (Add a layout's) its way back over its rows, standing as they scroll under it, as
        // Windows 11's quick settings head a page shown in place of theirs.
        else if (PanelRows.IsUnder(open))
        {
            BackHead = new RectF(gutter, gutter, wide - (2 * gutter), P(PanelMetrics.BackHead));
            top = BackHead.Bottom;
        }
        LayPage(shown, input.Open ?? new HashSet<string>(), wide, top > 0 ? P(PanelMetrics.Gap) : gutter);
        var tall = MathF.Max(top + P(PanelMetrics.OneLine), MathF.Min(top + ContentHeight, room.Y > 0 ? room.Y : float.MaxValue));
        Viewport = new RectF(0, top, wide, tall - top);
        FlyoutSize = new Vector2(wide, tall);
        MostScroll = MathF.Max(0, ContentHeight - Viewport.Height);
        Scroll = Math.Clamp(input.Scroll, 0, MostScroll);
    }

    public float Scale { get; }

    public PanelInput Input { get; }

    /// <summary>§575: a page's flyout stands open (<see cref="Shown"/>).</summary>
    public bool IsOpen => Shown is not null;

    /// <summary>
    /// The bar's size (px). §578: one width whatever is selected and however many errors there are — the selection's slot and
    /// the errors' room always kept —, only a display too narrow for it narrowing it.
    /// </summary>
    public Vector2 Size { get; }

    /// <summary>The bar's buttons and entries, in the order they stand (bar px).</summary>
    public IReadOnlyList<PanelPart> Header { get; private set; } = [];

    /// <summary>§596: the room kept for the selection's entries (bar px): they stand from its start, the rest of it empty; §584 all of it empty with nothing selected.</summary>
    public RectF Selection { get; private set; }

    /// <summary>
    /// §608 (the person, 2026-11-09: "add a little divider between appearance and folder"): the hairline between the pages'
    /// entries and the selection's (bar px), 8 after the last page's entry and 8 before the room, its width the room's; empty
    /// with nothing selected (nothing after it to set apart). Drawn alone: nothing to press, stop on nor name.
    /// </summary>
    public RectF Divider { get; private set; }

    /// <summary>§608: a hairline: a whole pixel or more, never blurred over two.</summary>
    public float Hair => MathF.Max(1, MathF.Floor(Scale));

    /// <summary>§575: the flyout's size (px): its page's; zero for none.</summary>
    public Vector2 FlyoutSize { get; }

    /// <summary>§575: where the page shows in its flyout (flyout px): all of it, under the add list's own search.</summary>
    public RectF Viewport { get; }

    /// <summary>§575: the add list's own search box at its flyout's top (flyout px); empty for a page without one.</summary>
    public RectF Filter { get; }

    /// <summary>
    /// §628: a page under another's entry (<see cref="PanelRows.IsUnder"/>) its head at its flyout's top (flyout px): one plate
    /// the whole of which goes back, the arrow at its start, the page's title after it; empty for a page of its own entry.
    /// </summary>
    public RectF BackHead { get; }

    /// <summary>§628: where the back arrow stands in <see cref="BackHead"/> (flyout px): its glyph's box, 12 in from the head's start.</summary>
    public RectF BackArrow => BackHead.IsEmpty ? default : new RectF(BackHead.X + P(PanelMetrics.Label), BackHead.Y + MathF.Round((BackHead.Height - P(PanelMetrics.Icon)) / 2), P(PanelMetrics.Icon), P(PanelMetrics.Icon));

    /// <summary>§628: where the page's title stands in <see cref="BackHead"/> (flyout px): 8 after the arrow, to 12 from the head's end.</summary>
    public RectF BackTitle => BackHead.IsEmpty ? default : new RectF(BackArrow.Right + P(PanelMetrics.Gap), BackHead.Y, MathF.Max(0, BackHead.Right - P(PanelMetrics.Label) - (BackArrow.Right + P(PanelMetrics.Gap))), BackHead.Height);

    public PanelPageView? Shown { get; }

    /// <summary>The page's row of title buttons (page px), where it has some and no header card.</summary>
    public RectF Title { get; private set; }

    /// <summary>A page's title buttons (page px); §529 icon buttons at its header card's end.</summary>
    public List<(FlyoutItem Item, RectF Rect)> Actions { get; } = [];

    /// <summary>§529: the header card of a widget's or a folder's page (page px); empty for another page.</summary>
    public RectF HeaderCard { get; private set; }

    /// <summary>§529: the header's tile, its icon (several fanned out) on it (page px).</summary>
    public RectF HeaderTile { get; private set; }

    /// <summary>§529: the header's line under its name: where it stands (page px).</summary>
    public RectF Place { get; private set; }

    public List<PanelBlock> Blocks { get; } = [];

    /// <summary>§551: where each line stands with what is shown under it (an open expander's rows, the rows nested under it), by its row's key (page px).</summary>
    public Dictionary<string, RectF> Spans { get; } = new(StringComparer.Ordinal);

    /// <summary>How tall the page is, its head and its cards (px).</summary>
    public float ContentHeight { get; private set; }

    public float MostScroll { get; }

    public float Scroll { get; }

    public float P(float dip) => MathF.Round(dip * Scale, MidpointRounding.AwayFromZero);

    /// <summary>The row of the shown page at <paramref name="key"/> (at any depth); null for none.</summary>
    public PanelRow? Row(string key) => rows.GetValueOrDefault(key);

    /// <summary>§609, §610: the row whose pair (<see cref="PanelRow.Pair"/>) is at <paramref name="key"/> (a holiday calendar a region's, Palette the palette's reset's); null for none.</summary>
    public PanelRow? HolderOf(string key) => holders.GetValueOrDefault(key);

    /// <summary>
    /// §575: the room a layout is given on a display whose work area is <paramref name="workHeight"/> px tall at
    /// <paramref name="scale"/>: the bar no wider than <paramref name="widest"/> px (a narrow display's, EditDeckPlan.Widest; 0
    /// for none), a flyout no taller than a popup's most (FlyoutPlacement.MostShare of the work area).
    /// </summary>
    public static Vector2 SizeOf(float scale, float workHeight, float widest = 0) =>
        new(widest > 0 && widest < float.MaxValue ? MathF.Floor(widest) : 0, MathF.Floor(workHeight * FlyoutPlacement.MostShare));

    // --- The bar ------------------------------------------------------------------------------------

    /// <summary>A button of glyph and words: 12, the glyph, 8, the words, 12; §548 with a chevron 8 after the words, its middle 20 in from the end.</summary>
    public float ButtonWide(string words, bool chevron) =>
        P(PanelMetrics.Label) + P(PanelMetrics.Icon) + P(PanelMetrics.Gap) + WordsWide(words) + Trail(chevron);

    /// <summary>One line of Body: how wide (px).</summary>
    private float WordsWide(string words) => MathF.Ceiling(text.Width(words, P(PanelMetrics.Body)));

    /// <summary>§548: what follows a button's words: 12; with a chevron 8, the chevron, its middle 20 in from the button's end.</summary>
    private float Trail(bool chevron) =>
        chevron ? P(PanelMetrics.Gap) + P(PanelMetrics.ChevronIn) + (P(PanelMetrics.Chevron) / 2) : P(PanelMetrics.Label);

    /// <summary>§575: a page's entry with its icon alone (a narrow display): 12, the icon, 12.</summary>
    private float IconWide => P(PanelMetrics.Label) + P(PanelMetrics.Icon) + P(PanelMetrics.Label);

    /// <summary>
    /// §575, §578: the bar's buttons, a command bar's: the grip; the pages' entries 4 apart; §608 8, the divider, 8, §596 the selection's
    /// entries (a folder's, a widget's, both, or §584 none) in the room kept for them, a gutter; the errors' room (their icon
    /// button while there are any); Add; Done, ending a gutter in. One width whatever is selected and however many errors
    /// there are; no wider than <paramref name="widest"/> where it can be helped: the pages' entries show their icon alone
    /// first, then the selection's room narrows.
    /// </summary>
    private List<PanelPart> Parts(float widest)
    {
        var pages = Input.Content?.Pages ?? [];
        float Over(List<PanelPart> laid) => laid[^1].Rect.Right + P(PanelMetrics.Gutter) - widest;
        var room = P(PanelMetrics.SelectionWide);
        var parts = Lay(pages, room, compact: false);
        if (Over(parts) > 0)
        {
            parts = Lay(pages, room, compact: true);
            if (Over(parts) is var over and > 0)
            {
                parts = Lay(pages, MathF.Max((2 * IconWide) + P(PanelMetrics.Tight) + Hair, room - over), compact: true);
            }
        }
        return parts;
    }

    /// <summary>§596: an entry with words the least it is cut to: 12, the icon, 8, a name's least, 12.</summary>
    private float CutLeast => P(PanelMetrics.Label) + P(PanelMetrics.Icon) + P(PanelMetrics.Gap) + P(PanelMetrics.NameLeast) + P(PanelMetrics.Label);

    /// <summary>
    /// §596: how wide each of the selection's <paramref name="words"/> stands in <paramref name="room"/> px, and whether it shows
    /// its icon alone: each its natural width, 4 apart; where they do not fit the last (the widget's) cut short, its words no
    /// shorter than a name's least; where even that does not fit the first (the folder's) its icon alone, then all.
    /// </summary>
    internal (float Wide, bool Icon)[] Fit(IReadOnlyList<string> words, float room)
    {
        var n = words.Count;
        var natural = words.Select(each => ButtonWide(each, chevron: false)).ToArray();
        for (var icons = 0; icons < n; icons++)
        {
            var fitted = natural.Select((wide, at) => (Wide: at < icons ? IconWide : wide, Icon: at < icons)).ToArray();
            var left = room - fitted[..^1].Sum(each => each.Wide) - ((n - 1) * P(PanelMetrics.Tight));
            if (left >= MathF.Min(natural[^1], CutLeast))
            {
                fitted[^1].Wide = MathF.Floor(MathF.Min(natural[^1], left));
                return fitted;
            }
        }
        return [.. words.Select(_ => (IconWide, true))];
    }

    /// <summary>§578: the bar laid with the selection's room <paramref name="roomW"/> px wide, the pages' entries <paramref name="compact"/> or not.</summary>
    private List<PanelPart> Lay(IReadOnlyList<PanelPageView> pages, float roomW, bool compact)
    {
        List<PanelPart> parts = [];
        var pad = P(PanelMetrics.Gap);
        var tall = P(PanelMetrics.Control);
        var x = pad;
        void Add(PanelPart part, float after)
        {
            parts.Add(part);
            x += part.Rect.Width + after;
        }
        RectF At(float w) => new(x, pad, w, tall);
        // §575: a widget's and a folder's entries named for what is selected ("Clock", "3 widgets", "Folder").
        static string WordsOf(PanelPageView view) => view.Page is PanelPage.Widget or PanelPage.Folder ? view.Title : view.Side;
        PanelPart Entry(PanelPageView view, bool icon, float? wide = null) =>
            new(PanelHitKind.Nav, At(wide ?? (icon ? IconWide : ButtonWide(WordsOf(view), chevron: false))), WordsOf(view), view.Glyph, view.Page, icon);
        // §628: a page under another's entry (Add a layout's) none of its own.
        var general = pages.Where(view => view.Page is not (PanelPage.Widget or PanelPage.Folder or PanelPage.Add or PanelPage.Errors) && !PanelRows.IsUnder(view.Page)).ToList();
        foreach (var view in general)
        {
            Add(Entry(view, compact), P(PanelMetrics.Tight));
        }
        x += P(PanelMetrics.Gap) - (general.Count > 0 ? P(PanelMetrics.Tight) : 0);
        // §608: the divider, 8 either side (the gutter that stood here), its hairline taken from the room after it, so nothing
        // moves whether it shows or not.
        var divider = new RectF(x, MathF.Round((P(PanelMetrics.Head) - P(PanelMetrics.DividerTall)) / 2), Hair, P(PanelMetrics.DividerTall));
        x += Hair + P(PanelMetrics.Gap);
        // §596: the selection's entries, the folder's then the widget's, each an entry as the pages' are, 4 apart, from the
        // start of the room kept for them whether anything is selected or not; the rest of it empty.
        Selection = At(MathF.Floor(roomW) - Hair);
        var chosen = pages.Where(view => view.Page is PanelPage.Folder or PanelPage.Widget).OrderBy(view => view.Page).ToList();
        Divider = chosen.Count > 0 ? divider : default;
        foreach (var (view, (wide, icon)) in chosen.Zip(Fit([.. chosen.Select(WordsOf)], Selection.Width)))
        {
            Add(Entry(view, icon, wide), P(PanelMetrics.Tight));
        }
        x = Selection.Right + P(PanelMetrics.Gutter);
        // §578: the errors' room, kept whether there are any or not: their icon button while there are (its words its tooltip).
        if (Input.Errors > 0)
        {
            parts.Add(new PanelPart(PanelHitKind.Errors, At(IconWide), S.DockEditErrors(Input.Errors), PanelGlyphs.ErrorRing, Compact: true));
        }
        x += IconWide + P(PanelMetrics.Tight);
        Add(new PanelPart(PanelHitKind.Add, At(ButtonWide(S.DockEditAdd, chevron: false)), S.DockEditAdd, PanelGlyphs.Add, PanelPage.Add), P(PanelMetrics.Gap));
        Add(new PanelPart(PanelHitKind.Done, At(MathF.Max(P(PanelMetrics.DoneLeast), ButtonWide(S.DockEditDone, chevron: false))), S.DockEditDone, PanelGlyphs.Done), 0);
        return parts;
    }

    /// <summary>§575: the bar's part of <paramref name="hit"/> (an entry by its page); null for none.</summary>
    public PanelPart? PartOf(PanelHit hit) =>
        Header.FirstOrDefault(part => part.Kind == hit.Kind && (hit.Kind != PanelHitKind.Nav || part.Page == hit.Page));

    /// <summary>§575: the hit of a part of the bar: an entry its page's.</summary>
    public static PanelHit HitOf(PanelPart part) => part.Kind == PanelHitKind.Nav ? new PanelHit(PanelHitKind.Nav, Page: part.Page) : new PanelHit(part.Kind);

    /// <summary>§575: whether <paramref name="kind"/> stands on the bar (else in the flyout).</summary>
    public static bool OnBar(PanelHitKind kind) => kind is PanelHitKind.Errors or PanelHitKind.Add or PanelHitKind.Done or PanelHitKind.Nav;

    /// <summary>
    /// §575: whether <paramref name="hit"/> is the entry whose flyout stands open (Add's, the add list's: §593 whatever opened it;
    /// §628 the Layouts page's while Add a layout's page, under it, shows).
    /// </summary>
    public bool Opened(PanelHit hit) =>
        Input.Shown is { } open && Shown is not null && ((hit.Kind == PanelHitKind.Nav && hit.Page == PanelRows.EntryOf(open)) || (hit.Kind == PanelHitKind.Add && open == PanelPage.Add)
            // §689: the errors' indicator while their page stands open.
            || (hit.Kind == PanelHitKind.Errors && open == PanelPage.Errors));
    // --- The page -----------------------------------------------------------------------------------

    /// <summary>
    /// The page in its flyout <paramref name="wide"/> px wide (page px), §548 in one rhythm: from <paramref name="top"/> a gutter
    /// in from its sides, §575 no title (its entry names it): §529 a header card, or a row of title buttons where it has some;
    /// each group 24 after what stands before it (the head, the last card of the group before), its heading 8 over its cards,
    /// the cards 4 apart; a gutter under the last. §575 the add list's rows those for what is typed in its own search.
    /// </summary>
    private void LayPage(PanelPageView page, IReadOnlySet<string> open, float wide, float top)
    {
        var left = P(PanelMetrics.Gutter);
        var right = wide - P(PanelMetrics.Gutter);
        var w = right - left;
        var y = top;
        var first = true;
        if (page.Header is { } header)
        {
            y = LayHeader(header, page.Actions, left, w, y);
            first = false;
        }
        else if (page.Actions.Count > 0)
        {
            Title = new RectF(left, y, w, P(PanelMetrics.TitleRow));
            var at = right;
            foreach (var action in page.Actions.Reverse())
            {
                var bw = ButtonWide(action.Text, chevron: false);
                at -= bw;
                Actions.Insert(0, (action, new RectF(at, y + MathF.Round((Title.Height - P(PanelMetrics.Control)) / 2), bw, P(PanelMetrics.Control))));
                at -= P(PanelMetrics.Gap);
            }
            y = Title.Bottom;
            first = false;
        }
        // What stands between the last thing laid and the next: a group's 24 after the head and before a heading, 8 under a
        // heading, 4 between two cards; nothing before the first.
        var gap = first ? 0 : P(PanelMetrics.Group);
        // §592: a tab strip, then its chosen tab's rows as the page's own.
        List<PanelRow> shown = [.. page.RowsFor(Input.Filter).SelectMany(row => row.Kind == PanelRowKind.Tabs && row.Rows.Count > 0
            ? row.Rows[PanelTabs.Chosen(row, open)].Rows.Prepend(row)
            : [row])];
        for (var index = 0; index < shown.Count; index++)
        {
            var row = shown[index];
            rows[row.Key] = row;
            // §592: the strip on the page's ground, no card: 8 under what stands before it, 8 over the chosen tab's cards.
            if (row.Kind == PanelRowKind.Tabs)
            {
                y += first ? 0 : P(PanelMetrics.Gap);
                first = false;
                var strip = new RectF(left, y, w, Tall(row, left, w));
                foreach (var tab in row.Rows)
                {
                    rows[tab.Key] = tab;
                }
                Blocks.Add(new PanelBlock(row, strip, [new PanelLine(row, strip, Inner: false)]));
                y = strip.Bottom;
                gap = P(PanelMetrics.Gap);
                continue;
            }
            if (row.Kind == PanelRowKind.Heading)
            {
                y += first ? 0 : P(PanelMetrics.Group);
                first = false;
                // §553: a caption under it where it has one, §604 every line of it.
                var tall = P(PanelMetrics.Heading) + CaptionTall(row.Detail, w);
                Blocks.Add(new PanelBlock(row, new RectF(left, y, w, tall), []));
                y += tall;
                gap = P(PanelMetrics.Gap);
                continue;
            }
            // §578: the add page's widgets tiles side by side, each a card of its own; a row that only acts (Add an entry, Reset
            // to default) its button alone, no card, the next such beside it.
            if (row.Kind == PanelRowKind.Pick || row.Foot)
            {
                var foot = row.Foot;
                var run = shown.Skip(index).TakeWhile(each => foot ? each.Foot : each.Kind == PanelRowKind.Pick).ToList();
                y += foot && !first ? MathF.Max(gap, P(PanelMetrics.Gap)) : gap;
                first = false;
                // §599: a row that only acts at the end of the page's lines, where a card's controls end.
                var laid = foot ? Foots(run, left, left + w - P(PanelMetrics.Gutter), y) : Tiles(run, left, w, y);
                foreach (var (each, rect) in laid)
                {
                    rows[each.Key] = each;
                    Spans[each.Key] = rect;
                    Blocks.Add(new PanelBlock(each, rect, [new PanelLine(each, rect, Inner: false, Tile: !foot)]));
                }
                y = laid.Max(each => each.Rect.Bottom);
                gap = P(foot ? PanelMetrics.Gap : PanelMetrics.Tight);
                index += run.Count - 1;
                continue;
            }
            y += gap;
            gap = P(PanelMetrics.Tight);
            first = false;
            List<PanelLine> lines = [];
            var under = y;
            // §609: a line, its thing's choice in it (PanelRow.Pair) where its name stands whole before it, else a line of its
            // own right under it (§594's way: a row of the card, as far in).
            // §638: a pair's own pair (a font's weight, its reset) goes with it.
            void Line(PanelRow of, bool nested)
            {
                var line = new PanelLine(of, new RectF(left, under, w, Tall(of, left, w)), nested);
                if (of.Pair is not { } pair)
                {
                    lines.Add(line);
                    under += line.Rect.Height;
                    return;
                }
                foreach (var each in of.Pairs)
                {
                    holders[each.Key] = of;
                }
                // A button beside (a reset) never stacks: it is an icon's square where words would not fit (BesideWide).
                var stacks = pair.Kind != PanelRowKind.Action && !Whole(line);
                lines.Add(stacks ? line with { Row = of with { Pair = null } } : line);
                under += line.Rect.Height;
                if (stacks)
                {
                    Line(pair, nested: true);
                }
            }
            Line(row, nested: false);
            var inside = Inside(row, open).ToList();
            for (var at = 0; at < inside.Count; at++)
            {
                var inner = inside[at];
                // §578: an add page's folder's widgets tiles in its card, on its rows' ground, a space round them.
                if (inner.Kind == PanelRowKind.Pick)
                {
                    var run = inside.Skip(at).TakeWhile(each => each.Kind == PanelRowKind.Pick).ToList();
                    var pad = P(PanelMetrics.Gap);
                    var tiles = Tiles(run, left + pad, w - (2 * pad), under + pad);
                    lines.AddRange(tiles.Select(each => new PanelLine(each.Row, each.Rect, Inner: true, Tile: true)));
                    under = tiles.Max(each => each.Rect.Bottom) + pad;
                    at += run.Count - 1;
                    continue;
                }
                Line(inner, nested: true);
            }
            foreach (var line in lines)
            {
                rows[line.Row.Key] = line.Row;
                // §610: a pair, its own key in its holder's line.
                foreach (var pair in line.Row.Pairs)
                {
                    rows[pair.Key] = pair;
                }
            }
            // §551: each line with what stands under it, a row put in order's room as it is dragged.
            for (var at = 0; at < lines.Count; at++)
            {
                var below = at == 0 ? lines.Count - 1 : Inside(lines[at].Row, open).Count();
                var last = lines[Math.Min(lines.Count - 1, at + below)];
                Spans[lines[at].Row.Key] = new RectF(left, lines[at].Rect.Y, w, last.Rect.Bottom - lines[at].Rect.Y);
            }
            Blocks.Add(new PanelBlock(row, new RectF(left, y, w, under - y), lines));
            y = under;
        }
        ContentHeight = y + P(PanelMetrics.Gutter);
    }

    /// <summary>
    /// §578 (the person, 2026-11-09: "add widgets flyout is structured to sparsely … maybe also place items beside each
    /// other"): <paramref name="run"/>'s widgets as tiles from (<paramref name="x"/>, <paramref name="y"/>) across
    /// <paramref name="w"/> px, row by row: as many side by side as 200 DIP each fit (§593: across the edit bar's width), sharing the width, 4 apart
    /// either way; 40 DIP tall, 52 where one of them says more under its name (why it is greyed, where it is), §604 as tall as
    /// the most any of them says there, wrapped at its room. Page px.
    /// </summary>
    public List<(PanelRow Row, RectF Rect)> Tiles(IReadOnlyList<PanelRow> run, float x, float w, float y)
    {
        var tight = P(PanelMetrics.Tight);
        // §628: tiles that picture what they make (a layout preset's bar) as wide as 400 DIP each fit, their picture over their
        // words (PictureOf).
        var pictured = run.Any(row => row.Picture is not null);
        var columns = Math.Max(1, (int)MathF.Floor((w + tight) / (P(pictured ? PanelMetrics.PictureTileLeast : PanelMetrics.TileLeast) + tight)));
        var each = MathF.Floor((w - ((columns - 1) * tight)) / columns);
        var over = pictured ? P(PanelMetrics.Gap) + PictureTall(each - (2 * P(PanelMetrics.Gap))) : 0;
        RectF At(int at, float tall) => new(x + (at % columns * (each + tight)), y + (at / columns * (tall + tight)), each, tall);
        var tall = over + run.Select((row, at) => row.Second is null ? P(PanelMetrics.Tile)
            : MathF.Max(P(PanelMetrics.TileTwoLines), P(PanelMetrics.Gap) + P(PanelMetrics.BodyLine) + SecondTall(new PanelLine(row, At(at, 0), Inner: false, Tile: true)) + P(PanelMetrics.Gap))).DefaultIfEmpty(0).Max();
        return [.. run.Select((row, at) => (row, At(at, tall)))];
    }

    /// <summary>
    /// §628: how tall a tile's picture of a bar stands at <paramref name="wide"/> px: as a strip of a desktop the bar stands at the
    /// foot of (LookPreview.ItselfWideDip by ItselfTallDip), scaled to its width.
    /// </summary>
    public static float PictureTall(float wide) => MathF.Round(MathF.Max(0, wide) * LookPreview.ItselfTallDip / LookPreview.ItselfWideDip);

    /// <summary>§628: where a tile's picture stands (page px): 8 in from its top and sides; empty for a tile of words alone.</summary>
    public RectF PictureOf(PanelLine line)
    {
        if (!line.Tile || line.Row.Picture is null)
        {
            return default;
        }
        var pad = P(PanelMetrics.Gap);
        var w = line.Rect.Width - (2 * pad);
        return new RectF(line.Rect.X + pad, line.Rect.Y + pad, w, PictureTall(w));
    }

    /// <summary>§628: a tile's line of words (page px): under its picture, the tile's words as a tile of words alone has them; the line itself for every other.</summary>
    public PanelLine WordsOf(PanelLine line) =>
        PictureOf(line) is { IsEmpty: false } picture ? line with { Rect = line.Rect with { Y = picture.Bottom, Height = line.Rect.Bottom - picture.Bottom } } : line;

    /// <summary>
    /// §578 (the person, 2026-11-09: "reset to default and add an entry should not get the dark big background buttons"): a
    /// run of rows that only act, with nothing to say before them (<see cref="PanelRow.Foot"/>), as Windows' Settings sets such
    /// commands under a list: each its button alone, no card round it, in order along one line, 8 apart, on to the next line
    /// where the room from <paramref name="x"/> to <paramref name="end"/> ends; §599 (the person, 2026-11-09: "they should be
    /// aligned on the right side") each line ending at <paramref name="end"/>, the value controls' end. Page px.
    /// </summary>
    public List<(PanelRow Row, RectF Rect)> Foots(IReadOnlyList<PanelRow> run, float x, float end, float y)
    {
        List<(PanelRow, RectF)> laid = [];
        var tall = P(PanelMetrics.Control);
        var room = end - x;
        List<(PanelRow Row, float Wide)> line = [];
        void Close()
        {
            // The line's buttons in order, the last ending at the end.
            var at = end - line.Sum(each => each.Wide) - ((line.Count - 1) * P(PanelMetrics.Gap));
            foreach (var (row, wide) in line)
            {
                laid.Add((row, new RectF(at, y, wide, tall)));
                at += wide + P(PanelMetrics.Gap);
            }
            line.Clear();
        }
        foreach (var row in run)
        {
            var wide = MathF.Min(FootWide(row), room);
            if (line.Count > 0 && line.Sum(each => each.Wide + P(PanelMetrics.Gap)) + wide > room)
            {
                Close();
                y += tall + P(PanelMetrics.Tight);
            }
            line.Add((row, wide));
        }
        Close();
        return laid;
    }

    /// <summary>§578: a row that only acts as its button (<see cref="Foots"/>): 12, its glyph, 8, its words, 12; a way to add one's chevron after them.</summary>
    public float FootWide(PanelRow row) =>
        P(PanelMetrics.Label) + (FootGlyph(row).Length > 0 ? P(PanelMetrics.Icon) + P(PanelMetrics.Gap) : 0) + WordsWide(row.Text) + Trail(row.Adds);

    /// <summary>§578: the glyph a row that only acts wears on its button: its own, a way to add one's "+" where it has none.</summary>
    public static string FootGlyph(PanelRow row) => row.Glyph.Length > 0 ? row.Glyph : row.Adds ? PanelGlyphs.Add : "";

    /// <summary>
    /// §578: where a row that only acts has its button (page px): the line itself, on a page; in an expander's card, §599 at
    /// the end of its line where the value controls end, in its middle, never further in than where the rows' names start.
    /// </summary>
    public RectF FootAt(PanelLine line)
    {
        if (!line.Inner)
        {
            return line.Rect;
        }
        var end = ControlEnd(line);
        var wide = MathF.Min(FootWide(line.Row), end - NameX(line) + P(PanelMetrics.Label));
        return new RectF(end - wide, line.Rect.Y + MathF.Round((line.Rect.Height - P(PanelMetrics.Control)) / 2), wide, P(PanelMetrics.Control));
    }

    /// <summary>
    /// §529: the header card at the page's top (<paramref name="y"/>, page px): its tile (fanned for several) a gutter in, its
    /// name (Subtitle) over where it stands (Caption) a gutter after the tile, §548 the two lines' block centred on the tile;
    /// the page's buttons as a command bar's 32 DIP icon buttons 4 apart at its end, centred on the tile too. Where the card
    /// ends (page px).
    /// </summary>
    private float LayHeader(PanelHeader header, IReadOnlyList<FlyoutItem> actions, float left, float w, float y)
    {
        var pad = P(PanelMetrics.Gutter);
        var tile = P(PanelMetrics.HeaderTile);
        var fanned = Math.Min(header.Glyphs.Count, PanelMetrics.FannedMost);
        HeaderTile = new RectF(left + pad, y + pad, tile + ((fanned - 1) * P(PanelMetrics.FanStep)), tile);
        var button = P(PanelMetrics.Control);
        var at = left + w - pad;
        foreach (var action in actions.Reverse())
        {
            at -= button;
            Actions.Insert(0, (action, new RectF(at, HeaderTile.Y + MathF.Round((tile - button) / 2), button, button)));
            at -= P(PanelMetrics.Tight);
        }
        var nameX = HeaderTile.Right + pad;
        var room = MathF.Max(P(PanelMetrics.NameLeast), (Actions.Count > 0 ? Actions[0].Rect.X - P(PanelMetrics.Gap) : left + w - pad) - nameX);
        var lines = P(PanelMetrics.SubtitleLine) + P(PanelMetrics.CaptionLine);
        Title = new RectF(nameX, HeaderTile.Y + MathF.Round((tile - lines) / 2), room, P(PanelMetrics.SubtitleLine));
        Place = new RectF(nameX, Title.Bottom, room, P(PanelMetrics.CaptionLine));
        HeaderCard = new RectF(left, y, w, P(PanelMetrics.HeaderTall));
        return HeaderCard.Bottom;
    }

    /// <summary>
    /// An open expander's rows as its card shows them: no heading (its rows' names say it) but §685 (the person, 2026-11-12: "for
    /// the accounts it should be more obivous / clearer what belongs to email, calendar, etc. right now everything is just
    /// displayed in the same list") one with a glyph of its own (an account's Calendars, Mail), which heads a group: it stands
    /// as a line, its glyph and its name, the rows under it to the next a step in.
    /// </summary>
    private static IEnumerable<PanelRow> Grouped(IReadOnlyList<PanelRow> rows)
    {
        var deeper = 0;
        foreach (var each in rows)
        {
            if (each.Kind == PanelRowKind.Heading)
            {
                deeper = 0;
                if (each.Item.Icon is not null)
                {
                    yield return each;
                    deeper = 1;
                }
                continue;
            }
            yield return deeper == 0 ? each : each with { Item = each.Item with { Indent = each.Item.Indent + deeper } };
        }
    }

    /// <summary>
    /// The lines under <paramref name="row"/> in its card: an open expander's rows — no heading (its rows' names say it), an
    /// open expander's in it after it —; §529 a mixed row's widgets' own while shown, and the rows nested under it, each with
    /// what is under it in turn. §588: every one a row of the card as any, as far in as its choice (§529 to §587 a step in).
    /// </summary>
    private static IEnumerable<PanelRow> Inside(PanelRow row, IReadOnlySet<string> open)
    {
        IEnumerable<PanelRow> under = [
            .. Opens(row, open) ? Grouped(row.Rows) : [],
            // §682: a dropdown standing in this row's line (an account's colour) its picker under the line, open.
            .. row.Pairs.Where(pair => PanelPickers.Inline(pair) && open.Contains(pair.Key)).SelectMany(pair => pair.Rows),
            // §682: a button's picker (an account's colour) under the line, open.
            .. row.Folded.Where(folded => open.Contains(folded.Key)).SelectMany(folded => folded.Rows),
            .. row.Theirs.Count > 0 && open.Contains(PanelGroups.EachOf(row.Key)) ? row.Theirs : [],
            .. row.Nested];
        foreach (var inner in under)
        {
            yield return inner;
            foreach (var deeper in Inside(inner, open))
            {
                yield return deeper;
            }
        }
    }

    /// <summary>
    /// The height of <paramref name="row"/>'s line from <paramref name="left"/> across <paramref name="w"/> px: 48 for one line;
    /// 60 for its name over a Caption line, §604 as much taller as its second line says more lines, wrapped at the room its
    /// words have there — a line as tall as its words, so none is drawn over another; §536 a preview as tall as its sample asks;
    /// §578 a folder of the add page (an expander of widgets) as tall as its tiles, 40.
    /// </summary>
    public float Tall(PanelRow row, float left, float w) =>
        row.Shows is { } shows ? P(row.Bare ? LookPreview.BoxDip(shows, text, Scale) : LookPreview.TallDip(shows, text, Scale))
        : row.Kind == PanelRowKind.PickerBody ? PickerTall(row, left, w)
        : row.Kind == PanelRowKind.Tabs ? P(PanelMetrics.Tabs)
        : row is { Kind: PanelRowKind.Expander, Tall: false, Rows.Count: > 0 } && row.Rows.All(each => each.Kind is PanelRowKind.Pick or PanelRowKind.Expander) ? P(PanelMetrics.Tile)
        : !row.Tall ? P(PanelMetrics.OneLine)
        : MathF.Max(P(PanelMetrics.TwoLines), P(PanelMetrics.Label) + P(PanelMetrics.BodyLine) + SecondTall(new PanelLine(row, new RectF(left, 0, w, 0), Inner: false)) + P(PanelMetrics.Label));

    /// <summary>
    /// §604: how tall <paramref name="line"/>'s second line stands (px): every line of it, wrapped at its name's room
    /// (<see cref="NameRoom"/>), Caption's line each, at least one; none for a line without one.
    /// </summary>
    public float SecondTall(PanelLine line) => line.Row.Second is { } said ? CaptionTall(said, NameRoom(line)) : 0;

    /// <summary>§604: how tall <paramref name="said"/> stands in Caption wrapped at <paramref name="room"/> px, at least one Caption line; none for none.</summary>
    private float CaptionTall(string? said, float room) =>
        said is null ? 0 : MathF.Max(P(PanelMetrics.CaptionLine), MathF.Ceiling(text.Tall(said, P(PanelMetrics.Caption), room)));

    /// <summary>
    /// §604: where <paramref name="line"/>'s words stand (page px): its name, one Body line, from its name's start across its
    /// room, over its second line, every line of it (<see cref="SecondTall"/>), the two in the line's middle — what the painter
    /// draws them in.
    /// </summary>
    public (RectF Name, RectF Second) WordsAt(PanelLine line)
    {
        var x = NameX(line);
        var room = NameRoom(line);
        var second = SecondTall(line);
        var top = line.Rect.Y + MathF.Round((line.Rect.Height - P(PanelMetrics.BodyLine) - second) / 2);
        var name = new RectF(x, top, room, P(PanelMetrics.BodyLine));
        return (name, new RectF(x, name.Bottom, room, second));
    }

    /// <summary>
    /// §536: where a preview's sample stands in its <paramref name="line"/> (page px): under its Caption, 16 in from the card's
    /// sides as a row's icon is, the card's whole width else (LookPreview's metrics).
    /// </summary>
    public RectF PreviewBox(PanelLine line)
    {
        // §628: a tile's picture its own room.
        if (line.Tile)
        {
            return PictureOf(line);
        }
        var r = line.Rect;
        // §671: a bare preview its whole line.
        if (line.Row.Bare)
        {
            return r;
        }
        var top = r.Y + P(LookPreview.TopDip);
        return new RectF(r.X + P(PanelMetrics.Gutter), top, r.Width - (2 * P(PanelMetrics.Gutter)), MathF.Max(0, r.Bottom - P(LookPreview.FootDip) - top));
    }


    /// <summary>
    /// §529: whether <paramref name="row"/> stands open: an expander opened (<paramref name="open"/> holds it), a group not shut
    /// (it holds it shut).
    /// </summary>
    public static bool Opens(PanelRow row, IReadOnlySet<string> open) =>
        row.Kind is PanelRowKind.Expander or PanelRowKind.Picker || PanelPickers.Inline(row) ? open.Contains(row.Key) : row.Kind == PanelRowKind.Group && !open.Contains(row.Key);

    /// <summary>§529: whether <paramref name="row"/> stands open in this layout (<see cref="Opens(PanelRow, IReadOnlySet{string})"/>).</summary>
    public bool StandsOpen(PanelRow row) => Opens(row, Input.Open ?? new HashSet<string>());

    /// <summary>§592: which of tab strip <paramref name="strip"/>'s tabs is chosen in this layout (<see cref="PanelTabs.Chosen"/>).</summary>
    public int Chosen(PanelRow strip) => PanelTabs.Chosen(strip, Input.Open ?? new HashSet<string>());

    /// <summary>
    /// §592: where tab strip <paramref name="line"/>'s tabs stand (page px), each with whether its words are cut short: from the
    /// cards' start, each its words (Body Strong, as the chosen one's are drawn, so none moves as it is chosen) and 12 either side,
    /// as tall as the strip (the pill at their foot), 4 apart; narrowed alike where they would run past the strip's end.
    /// </summary>
    public List<(PanelRow Tab, RectF Rect, bool Cut)> TabsAt(PanelLine line)
    {
        var tabs = line.Row.Rows;
        var tight = P(PanelMetrics.Tight);
        var wide = tabs.Select(tab => P(PanelMetrics.Label) + MathF.Ceiling(text.Width(tab.Text, P(PanelMetrics.Body), strong: true)) + P(PanelMetrics.Label)).ToList();
        // §673: the strip's button (the palette's reset) at its right end, a gutter off the last tab.
        var button = line.Row.Button is null ? 0 : BesideWide(line) + P(PanelMetrics.Gutter);
        var room = line.Rect.Width - button - (tight * Math.Max(0, tabs.Count - 1));
        var share = wide.Sum() > room ? room / wide.Sum() : 1;
        List<(PanelRow, RectF, bool)> laid = [];
        var x = line.Rect.X;
        for (var at = 0; at < tabs.Count; at++)
        {
            var w = MathF.Floor(wide[at] * share);
            laid.Add((tabs[at], new RectF(x, line.Rect.Y, w, line.Rect.Height), share < 1));
            x += w + tight;
        }
        return laid;
    }

    /// <summary>§592: the tabs of the strip <paramref name="tab"/> is a tab of, in their order; none for another hit.</summary>
    public List<PanelHit> TabsAlong(PanelHit tab) =>
        // §673: the tabs alone, never the reset standing at the strip's end.
        tab.Kind != PanelHitKind.Tab || LineOf(tab.Key) is not { Row.Kind: PanelRowKind.Tabs } strip ? [] : [.. Controls(strip).Select(each => each.Hit).Where(hit => hit.Kind == PanelHitKind.Tab)];


    /// <summary>A rect of the page (page px) in the panel (panel px), as scrolled.</summary>
    public RectF OnPanel(RectF page) => page with { X = page.X + Viewport.X, Y = page.Y + Viewport.Y - Scroll };

    /// <summary>
    /// The things of <paramref name="line"/> a click or the keys reach, where they stand (page px), its control first:
    /// a switch, a number box's − and +, a segmented control's segments, a dropdown's box, a row of buttons; the whole
    /// line for an expander's head, a link and a row that acts.
    /// </summary>
    public List<(PanelHit Hit, RectF Rect)> Controls(PanelLine line)
    {
        var row = line.Row;
        var r = line.Rect;
        var right = ControlEnd(line);
        var tall = P(PanelMetrics.Control);
        var box = ValueBox(line);
        var chevron = right - P(PanelMetrics.ChevronIn);
        List<(PanelHit, RectF)> found = [];
        // §578: a row that only acts its button alone: a way to add one's list under it, as a dropdown's.
        // §628: one leading to a page of its own (Add a layout) a link.
        if (row.Foot)
        {
            found.Add((new PanelHit(row.Kind == PanelRowKind.Link ? PanelHitKind.Link : row.Adds ? PanelHitKind.Pop : PanelHitKind.Act, row.Item.Id, Key: row.Key), FootAt(line)));
            return found;
        }
        // §551: a row put in order its handle at its start, the first thing of it the keys reach.
        if (row.Drags)
        {
            found.Add((new PanelHit(PanelHitKind.Handle, row.Item.Id, Key: row.Key), HandleOf(line)));
        }
        switch (row.Kind)
        {
            case PanelRowKind.Toggle:
                found.Add((new PanelHit(PanelHitKind.Toggle, row.Item.Id, Key: row.Key), SwitchAt(right, r)));
                break;
            case PanelRowKind.Stepper when row.Stepper is { } stepper:
            {
                // − and + in two 32 DIP cells at the box's end, + where a chevron stands.
                var fewer = new RectF(chevron - tall - (tall / 2), box.Y, tall, tall);
                // §521: its value typed in place, where its row takes a number typed: the box before − and +.
                if (row.Field is { } typed)
                {
                    found.Add((new PanelHit(PanelHitKind.Number, typed.Id, Key: row.Key), new RectF(box.X, box.Y, fewer.X - box.X, tall)));
                }
                found.Add((new PanelHit(PanelHitKind.Fewer, stepper.Fewer, Key: row.Key), fewer));
                found.Add((new PanelHit(PanelHitKind.More, stepper.More, Key: row.Key), new RectF(chevron - (tall / 2), box.Y, tall, tall)));
                break;
            }
            // §521: a way to add one a button at the row's end, its list under it as a dropdown's.
            case PanelRowKind.Choice when row.Adds:
            {
                var w = ActionWide(row);
                found.Add((new PanelHit(PanelHitKind.Pop, row.Item.Id, Key: row.Key), new RectF(right - w, box.Y, w, tall)));
                break;
            }
            // §638: a choice standing beside a choice (a font's weight beside its family) at the line's end, a box of its own
            // width (PanelMetrics.PairWide); the row's own box, the one dropdown width, 8 before it.
            case PanelRowKind.Choice when row.Pair is { Kind: PanelRowKind.Choice } beside:
            {
                var after = new RectF(right - P(PanelMetrics.PairWide), box.Y, P(PanelMetrics.PairWide), tall);
                found.Add((new PanelHit(PanelHitKind.Pop, row.Item.Id, Key: row.Key), box with { X = after.X - P(PanelMetrics.Gap) - box.Width }));
                found.Add((new PanelHit(PanelHitKind.Pop, beside.Item.Id, Key: beside.Key), after));
                break;
            }
            case PanelRowKind.Choice:
                found.Add((new PanelHit(PanelHitKind.Pop, row.Item.Id, Key: row.Key), box));
                break;
            // §535: a colour a colour button at the row's end, what it is set to said before it.
            case PanelRowKind.Color:
                found.Add((new PanelHit(PanelHitKind.Pop, row.Item.Id, Key: row.Key), ColourAt(right, box.Y)));
                break;
            // §521: a text box typed in place, sending its field's words.
            case PanelRowKind.Text when row.Field is { } field:
                found.Add((new PanelHit(PanelHitKind.Text, field.Id, Key: row.Key), box));
                break;
            // §535: a row of buttons with its switch (an account's calendar): the switch at its end, its buttons before it.
            case PanelRowKind.Item when row.Switches:
            {
                var toggle = SwitchAt(right, r);
                found.Add((new PanelHit(PanelHitKind.Toggle, row.Item.Id, Key: row.Key), toggle));
                Tools(toggle.X - P(PanelMetrics.Gap));
                break;
            }
            case PanelRowKind.Item:
            {
                Tools(right);
                // §609: its one choice's dropdown (a holiday calendar's region) 8 before its buttons, the one dropdown width.
                if (row.Pair is { Kind: PanelRowKind.Choice } choice)
                {
                    var at = found.Where(each => each.Item1.Kind == PanelHitKind.Tool).Select(each => each.Item2.X).DefaultIfEmpty(right + P(PanelMetrics.Gap)).Min();
                    var wide = P(PanelMetrics.ControlWide);
                    found.Insert(row.Drags ? 1 : 0, (new PanelHit(PanelHitKind.Pop, choice.Item.Id, Key: choice.Key), new RectF(at - P(PanelMetrics.Gap) - wide, box.Y, wide, tall)));
                }
                break;
            }
            // §592: a tab strip each of its tabs, keyed the tab's (no id: the strip's place, not the menu's count, says which).
            case PanelRowKind.Tabs:
                found.AddRange(TabsAt(line).Select((tab, at) => (new PanelHit(PanelHitKind.Tab, 0, at, Key: tab.Tab.Key), tab.Rect)));
                break;
            // §529: a group's head the whole line, as an expander's; §552 a face's its Remove before its chevron (none for a
            // face with nothing to set: nothing to fold).
            case PanelRowKind.Group:
                Tools(row.Folds ? right - P(PanelMetrics.ChevronRoom) : right);
                found.Add((new PanelHit(PanelHitKind.Head, row.Item.Id, Key: row.Key), r));
                break;
            case PanelRowKind.Expander:
                // §521: a thing's buttons before its chevron, then the head itself.
                Tools(right - P(PanelMetrics.ChevronRoom));
                found.Add((new PanelHit(PanelHitKind.Head, row.Item.Id, Key: row.Key), r));
                break;
            case PanelRowKind.Link:
                found.Add((new PanelHit(PanelHitKind.Link, row.Item.Id, Key: row.Key), r));
                break;
            // §670: an inline picker's head the whole line, as an expander's; its body each of its cells.
            case PanelRowKind.Picker:
                found.Add((new PanelHit(PanelHitKind.Head, row.Item.Id, Key: row.Key), r));
                break;
            case PanelRowKind.PickerBody:
                found.AddRange(PickerCells(line).Where(cell => !cell.Hit.IsNone).Select(cell => (cell.Hit, cell.Rect)));
                break;
            // §575: a widget of the add list the whole card, a click adding it.
            case PanelRowKind.Pick:
                found.Add((new PanelHit(PanelHitKind.Act, row.Item.Id, Key: row.Key), r));
                break;
            // §529: a line of words, its link the words at its end.
            case PanelRowKind.Words:
                found.Add((new PanelHit(PanelHitKind.Link, row.Item.Id, Key: row.Key), WordsLink(line)));
                break;
            // §521: a row that acts a button at its end, its words the row's.
            case PanelRowKind.Action:
            {
                var w = ActionWide(row);
                found.Add((new PanelHit(PanelHitKind.Act, row.Item.Id, Key: row.Key), new RectF(right - w, box.Y, w, tall)));
                break;
            }
        }
        // §610, §613: a button standing beside the row's controls (PanelRow.Button: the palette's reset, a value's, §638 the
        // font's after its weight) before them, 8 off: the first the keys reach, as it reads; §638 beside a row of no control
        // of its own (Lometics' site beside its name) at the controls' end.
        if (row.Kind == PanelRowKind.Tabs && row.Button is { } ends)
        {
            // §673: a tab strip's at its right end, where the cards end.
            var w = BesideWide(line);
            found.Add((new PanelHit(PanelHitKind.Act, ends.Item.Id, Key: ends.Key), new RectF(r.Right - w, r.Y + MathF.Round((r.Height - tall) / 2), w, tall)));
        }
        else if (row.Button is { } button)
        {
            // §670: an inline picker's before what its head says (its head the whole line: no control to stand before).
            var at = found.Where(each => each.Item1.Kind is not (PanelHitKind.Handle or PanelHitKind.Head)).Select(each => each.Item2.X)
                .DefaultIfEmpty(row.Kind == PanelRowKind.Picker ? PickerValueLeft(line) - P(PanelMetrics.Label) + P(PanelMetrics.Gap) : right + P(PanelMetrics.Gap)).Min();
            var w = BesideWide(line);
            found.Insert(row.Drags ? 1 : 0, (new PanelHit(PanelHitKind.Act, button.Item.Id, Key: button.Key), new RectF(at - P(PanelMetrics.Gap) - w, box.Y, w, tall)));
        }
        // §529: a mixed row its widgets' own values' button, before its control (§548: 8 off it, as one button off the next).
        if (row.Pair is null && row.Theirs.Count > 0)
        {
            var at = found.Where(each => each.Item1.Kind != PanelHitKind.Handle).Select(each => each.Item2.X).DefaultIfEmpty(right).Min();
            var before = at - P(PanelMetrics.Gap) - tall;
            found.Insert(row.Drags ? 1 : 0, (new PanelHit(PanelHitKind.Each, row.Item.Id, Key: row.Key), new RectF(before, box.Y, tall, tall)));
        }
        return found;

        // A row's buttons, the last ending at <paramref name="end"/>, 4 apart as a command bar's: §551 each a 32 DIP square icon
        // button (Remove, Delete, Rename, Sign in again, Duplicate: a thing's own actions), its name its tooltip; §535 a
        // colour's a colour button.
        void Tools(float end)
        {
            var at = end;
            for (var index = row.Tools.Count - 1; index >= 0; index--)
            {
                var tool = row.Tools[index];
                var w = PanelSwatch.Of(tool) is not null ? P(PanelMetrics.ColourWide) : tall;
                at -= w;
                found.Insert(row.Drags ? 1 : 0, (new PanelHit(PanelHitKind.Tool, tool.Id, index, Key: row.Key), new RectF(at, box.Y, w, tall)));
                at -= P(PanelMetrics.Tight);
            }
        }
    }

    /// <summary>§548: where <paramref name="line"/>'s controls end (page px): a gutter in from its card's end; §578 a tile's 12.</summary>
    public float ControlEnd(PanelLine line) => line.Rect.Right - P(line.Tile ? PanelMetrics.Label : PanelMetrics.Gutter);

    /// <summary>
    /// §548: the one box of <paramref name="line"/>'s value (page px), whatever holds it — a dropdown, a text box, a number
    /// box, a segmented control —: 240 × 32 at its end, in its middle; a colour's words stand in it before their button.
    /// </summary>
    public RectF ValueBox(PanelLine line)
    {
        var right = ControlEnd(line);
        var tall = P(PanelMetrics.Control);
        var wide = P(PanelMetrics.ControlWide);
        // §670: an inline picker's search across its column, from where the head's words start.
        if (PanelPickers.Searches(line.Row))
        {
            var x = NameX(line);
            return new RectF(x, line.Rect.Y + ((line.Rect.Height - tall) / 2), right - x, tall);
        }
        return new RectF(right - wide, line.Rect.Y + ((line.Rect.Height - tall) / 2), wide, tall);
    }

    /// <summary>
    /// §587: how far a dropdown's words run in its one box (px): from 12 in — after its choice's icon where it wears one
    /// (§578) — to its chevron's room; past it they are cut short, whole in its tooltip (<see cref="TipOf"/>). §638 a box of
    /// its own width (a font's weight beside its family) its own.
    /// </summary>
    public float ValueRoom(PanelRow row) =>
        BoxWide(row) - P(PanelMetrics.Label) - (row.ValueIcon is null ? 0 : P(PanelMetrics.Icon) + P(PanelMetrics.Gap)) - P(PanelMetrics.ChevronRoom);

    /// <summary>§638: how wide <paramref name="row"/>'s dropdown's box is where it is shown (px); the one width, 240 DIP, elsewhere.</summary>
    private float BoxWide(PanelRow row) => LineOf(row.Key) is { } line
        ? Controls(line).Where(each => each.Hit.Kind == PanelHitKind.Pop && each.Hit.Key == row.Key).Select(each => each.Rect.Width).DefaultIfEmpty(P(PanelMetrics.ControlWide)).First()
        : P(PanelMetrics.ControlWide);

    /// <summary>
    /// §609: whether <paramref name="line"/>'s name stands whole before its controls, a gutter off them (a holiday calendar's
    /// with its region's dropdown beside its buttons); where not, that dropdown takes a line of its own under it.
    /// </summary>
    private bool Whole(PanelLine line) => NameX(line) + MathF.Ceiling(text.Width(line.Row.Text, P(PanelMetrics.Body))) + P(PanelMetrics.Gutter) <= ControlLeft(line);

    /// <summary>
    /// §551: the row at <paramref name="key"/> lifted to be dragged into order, the pointer at page px <paramref name="y"/>: its
    /// list's rows, where each stands with what is shown under it; null where it is put in order with nothing here.
    /// </summary>
    public PanelDrag? DragOf(string key, float y)
    {
        if (Shown is null || PanelRows.Order(Shown.Rows, key) is not { Count: > 1 } order || order.Any(row => !Spans.ContainsKey(row.Key)))
        {
            return null;
        }
        List<string> keys = [.. order.Select(row => row.Key)];
        List<RectF> spans = [.. keys.Select(each => Spans[each])];
        var from = keys.IndexOf(key);
        return new PanelDrag(key, order[from].Item.Id, keys, spans, from, y - spans[from].Y) { Y = y };
    }

    /// <summary>§551: where the row at <paramref name="key"/> stands among the rows it is put in order with (from 0), and how many; null for none.</summary>
    public (int At, int Count)? PlaceOf(string key) =>
        Shown is not null && PanelRows.Order(Shown.Rows, key) is { Count: > 1 } order ? (order.Select(row => row.Key).ToList().IndexOf(key), order.Count) : null;

    /// <summary>The line of the page at <paramref name="point"/> (panel px), as scrolled; null off the page's lines.</summary>
    public PanelLine? LineAt(Vector2 point) =>
        !IsOpen || !Viewport.Contains(point.X, point.Y) ? null
        : Blocks.SelectMany(block => block.Lines).FirstOrDefault(line => line.Rect.Contains(point.X - Viewport.X, point.Y - Viewport.Y + Scroll));

    /// <summary>§551: a row put in order's handle (page px): 16 DIP wide a tight space in, as tall as a control, in its line's middle.</summary>
    public RectF HandleOf(PanelLine line) =>
        new(line.Rect.X + P(PanelMetrics.HandleIn), line.Rect.Y + MathF.Round((line.Rect.Height - P(PanelMetrics.Control)) / 2), P(PanelMetrics.Handle), P(PanelMetrics.Control));

    /// <summary>A switch's room at <paramref name="right"/> of row <paramref name="r"/>: the switch and "On"/"Off" 12 before it, the row's height.</summary>
    private RectF SwitchAt(float right, RectF r)
    {
        var w = SwitchRoom;
        return new RectF(right - w, r.Y, w, r.Height);
    }

    /// <summary>A switch and its "On"/"Off" 12 before it (px).</summary>
    private float SwitchRoom => P(PanelMetrics.SwitchWide) + P(PanelMetrics.Label) + P(PanelMetrics.SwitchWords);

    /// <summary>
    /// §644: what a switch says before its "On"/"Off" (its <see cref="PanelRow.Value"/>: a Show from folder's "Not synced",
    /// "Syncing…") in Caption, a gap off it, as wide as the value column at most; nothing for none.
    /// </summary>
    public float SummaryWide(PanelRow row) =>
        row is { Kind: PanelRowKind.Toggle, Value: { } said } ? MathF.Min(MathF.Ceiling(text.Width(said, P(PanelMetrics.Caption))), P(PanelMetrics.ControlWide)) + P(PanelMetrics.Gap) : 0;

    /// <summary>§644: where a switch's words (<see cref="SummaryWide"/>) end, a gap before its "On"/"Off" (page px), from its control's end.</summary>
    public float SummaryRight(float right) => right - SwitchRoom - P(PanelMetrics.Gap);

    /// <summary>§535: a colour button ending at <paramref name="right"/>, its top at <paramref name="top"/>.</summary>
    public RectF ColourAt(float right, float top) => new(right - P(PanelMetrics.ColourWide), top, P(PanelMetrics.ColourWide), P(PanelMetrics.Control));

    /// <summary>§529: where <paramref name="line"/>'s name starts (page px): 48 DIP in; §578 a tile's 12 after its icon.</summary>
    public float NameX(PanelLine line) =>
        line.Tile ? line.Rect.X + P(PanelMetrics.Label) + P(PanelMetrics.Icon) + P(PanelMetrics.Label)
        : line.Rect.X + LeadOf(line) + P(PanelMetrics.NameAt);

    /// <summary>§551: how much further in <paramref name="line"/>'s icon and name stand: a row put in order's handle's room; none for another.</summary>
    public float LeadOf(PanelLine line) => (line.Row.Drags ? P(PanelMetrics.HandleLead) : 0) + (line.Row.Item.Indent * P(PanelMetrics.IndentStep));

    /// <summary>§548: where <paramref name="line"/>'s icon's middle stands (page px): the icon column, 16 in; §578 a tile's 12 in.</summary>
    public float IconX(PanelLine line) => line.Rect.X + LeadOf(line) + P(line.Tile ? PanelMetrics.Label : PanelMetrics.IconAt) + (P(PanelMetrics.Icon) / 2);

    /// <summary>
    /// §529: a line of words' link (page px): its words in Caption right after the line's own, 8 apart, never past the line's
    /// end (the line's own cut short before it).
    /// </summary>
    public RectF WordsLink(PanelLine line)
    {
        var row = line.Row;
        var caption = P(PanelMetrics.Caption);
        var link = MathF.Ceiling(text.Width(row.Text, caption));
        var right = ControlEnd(line);
        var x = MathF.Min(NameX(line) + MathF.Ceiling(text.Width(row.Detail ?? "", caption)) + P(PanelMetrics.Gap), right - link);
        var mid = line.Rect.Y + (line.Rect.Height / 2);
        var tall = P(PanelMetrics.BodyLine);
        return new RectF(x, mid - (tall / 2), link, tall);
    }

    /// <summary>
    /// Where <paramref name="line"/>'s control column starts (page px): its leftmost control, a value's 240 DIP box (a colour's
    /// words stand in it before their button), a switch's "On"/"Off", an expander's chevron's room.
    /// </summary>
    public float ControlLeft(PanelLine line)
    {
        var right = ControlEnd(line);
        var own = line.Row.Kind switch
        {
            PanelRowKind.Toggle => right - SwitchRoom - SummaryWide(line.Row),
            PanelRowKind.Action or PanelRowKind.Choice when line.Row.Kind == PanelRowKind.Action || line.Row.Adds => right - ActionWide(line.Row),
            PanelRowKind.Stepper or PanelRowKind.Choice or PanelRowKind.Color or PanelRowKind.Text => right - P(PanelMetrics.ControlWide),
            PanelRowKind.Item => Controls(line).Where(each => each.Hit.Kind != PanelHitKind.Handle).Select(each => each.Rect.X).DefaultIfEmpty(right).Min(),
            PanelRowKind.Expander or PanelRowKind.Group when line.Row.Tools.Count > 0 => Controls(line).Where(each => each.Hit.Kind == PanelHitKind.Tool).Min(each => each.Rect.X),
            PanelRowKind.Expander or PanelRowKind.Link => right - P(PanelMetrics.ChevronRoom),
            // §670: an inline picker's head what it is set to before its chevron, open or shut, its picture before that.
            PanelRowKind.Picker => PickerValueLeft(line),
            PanelRowKind.Group when line.Row.Folds => right - P(PanelMetrics.ChevronRoom),
            _ => right,
        };
        // §638: a shut expander's value (an account's items synced) before its buttons, 12 off, at most the value column's
        // width: the name and its line never run under it.
        if (line.Row is { Kind: PanelRowKind.Expander, Value: { } said } && !StandsOpen(line.Row))
        {
            own -= P(PanelMetrics.Label) + MathF.Min(MathF.Ceiling(text.Width(said, P(PanelMetrics.Caption))), P(PanelMetrics.ControlWide));
        }
        // §610, §613: a button standing beside its control (the palette's reset, a value's): the column from it where it stands
        // before (a dropdown's, a number's); a colour's stands in the value column, as the colour's words do. §638 any pair: the
        // column from the line's first control (a font's family, its weight beside it).
        if (line.Row.Pair is not null && line.Row.Kind != PanelRowKind.Tabs)
        {
            own = MathF.Min(own, Controls(line).Where(each => each.Hit.Kind is not (PanelHitKind.Handle or PanelHitKind.Head)).Select(each => each.Rect.X).DefaultIfEmpty(own).Min());
        }
        // §529: its widgets' own values' button before its control (§535: a colour's words stand in the value column before its
        // button all the same, the button between them).
        return line.Row.Pair is null && line.Row.Theirs.Count > 0 ? MathF.Min(own, Controls(line).Where(each => each.Hit.Kind != PanelHitKind.Handle).Min(each => each.Rect.X)) : own;
    }

    /// <summary>§548: how wide <paramref name="line"/>'s name and second line may run (px): to a gutter before its control column, never under 40 DIP.</summary>
    public float NameRoom(PanelLine line) => MathF.Max(P(PanelMetrics.NameLeast), line.Tile ? ControlEnd(line) - PlacedWide(line) - NameX(line) : ControlLeft(line) - P(PanelMetrics.Gutter) - NameX(line));

    /// <summary>§586: what a tile's count takes at its end, 8 off its name; nothing for a tile that says none.</summary>
    private float PlacedWide(PanelLine line) => line.Row.Value is { } placed ? text.Width(placed, P(PanelMetrics.Caption)) + P(PanelMetrics.Gap) : 0;

    /// <summary>
    /// §521: a row's button: its words and 12 either side (§548 a way to add one's chevron 8 after its words, 20 in from its
    /// end), at least 120 DIP (WinUI's), at most the value column's 240.
    /// </summary>
    public float ActionWide(PanelRow row) =>
        Math.Clamp(P(PanelMetrics.Label) + WordsWide(row.Text) + Trail(row.Adds), P(PanelMetrics.ActionLeast), P(PanelMetrics.ControlWide));

    /// <summary>
    /// §610: the button standing before <paramref name="line"/>'s control (PanelRow.Pair): §613 a subtle one (a value's reset)
    /// its icon alone, a 32 DIP square; one of words (the palette worn's reset) its words' button (<see cref="ActionWide"/>)
    /// where the row's name still stands whole before it, a gutter off it, else — too narrow for both: a flyout narrowed by its
    /// display, a language's longer words — its icon alone too. Its whole name its tooltip. Never over the row's name.
    /// </summary>
    public float BesideWide(PanelLine line)
    {
        var beside = line.Row.Button!;
        if (beside.Item.Subtle)
        {
            return P(PanelMetrics.Control);
        }
        var words = ActionWide(beside);
        // §673: a tab strip's its words where every tab still stands whole beside them.
        if (line.Row.Kind == PanelRowKind.Tabs)
        {
            var tabs = line.Row.Rows.Sum(tab => P(PanelMetrics.Label) + MathF.Ceiling(text.Width(tab.Text, P(PanelMetrics.Body), strong: true)) + P(PanelMetrics.Label) + P(PanelMetrics.Tight));
            return tabs + P(PanelMetrics.Gutter) + words <= line.Rect.Width ? words : P(PanelMetrics.Control);
        }
        var named = NameX(line) + WordsWide(line.Row.Text) + P(PanelMetrics.Gutter);
        // §638: beside a note (Lometics' site), which holds no control, the room to the controls' end.
        var room = line.Row.Kind == PanelRowKind.Note ? ControlEnd(line) + P(PanelMetrics.Gap) : ValueBox(line).X;
        return named + words + P(PanelMetrics.Gap) <= room ? words : P(PanelMetrics.Control);
    }

    // --- Hits ---------------------------------------------------------------------------------------

    /// <summary>What stands at <paramref name="point"/> of the bar (bar px): an entry, a button.</summary>
    public PanelHit HitAt(Vector2 point)
    {
        foreach (var part in Header)
        {
            if (part.Rect.Contains(point.X, point.Y))
            {
                return part.Kind == PanelHitKind.Add && !Input.CanAdd ? PanelHit.None : HitOf(part);
            }
        }
        return PanelHit.None;
    }

    /// <summary>§575: what stands at <paramref name="point"/> of the flyout (flyout px): the add list's search, a page's control.</summary>
    public PanelHit HitIn(Vector2 point)
    {
        if (!IsOpen)
        {
            return PanelHit.None;
        }
        if (!Filter.IsEmpty && Filter.Contains(point.X, point.Y))
        {
            return new PanelHit(PanelHitKind.Filter);
        }
        if (!BackHead.IsEmpty && BackHead.Contains(point.X, point.Y))
        {
            return new PanelHit(PanelHitKind.Back);
        }
        if (!Viewport.Contains(point.X, point.Y))
        {
            return PanelHit.None;
        }
        var x = point.X - Viewport.X;
        var y = point.Y - Viewport.Y + Scroll;
        foreach (var (item, rect) in Actions)
        {
            if (rect.Contains(x, y))
            {
                return new PanelHit(PanelHitKind.TitleAct, item.Id, Key: item.Text);
            }
        }
        foreach (var block in Blocks)
        {
            if (!block.Rect.Contains(x, y))
            {
                continue;
            }
            foreach (var line in block.Lines)
            {
                if (!line.Rect.Contains(x, y))
                {
                    continue;
                }
                foreach (var (hit, rect) in Controls(line))
                {
                    // §610: greyed as the row the control is: its line's pair's own (the palette's reset).
                    if (rect.Contains(x, y) && !line.Row.Own(hit.Key).Greyed)
                    {
                        return hit;
                    }
                }
            }
        }
        return new PanelHit(PanelHitKind.Page);
    }

    /// <summary>Where <paramref name="hit"/> stands — on the bar (bar px) where it is the bar's (<see cref="OnBar"/>), else in the flyout (flyout px), as scrolled —; empty where it is not shown.</summary>
    public RectF RectOf(PanelHit hit)
    {
        switch (hit.Kind)
        {
            case PanelHitKind.Errors or PanelHitKind.Add or PanelHitKind.Done or PanelHitKind.Nav:
                return PartOf(hit)?.Rect ?? default;
            case PanelHitKind.Filter:
                return Filter;
            case PanelHitKind.Back:
                return BackHead;
            case PanelHitKind.TitleAct:
                return Actions.FirstOrDefault(each => each.Item.Id == hit.Id) is { Item: not null } action ? OnPanel(action.Rect) : default;
            case PanelHitKind.Preview:
                return LineOf(hit.Key) is { } preview ? OnPanel(PreviewBox(preview)) : default;
        }
        if (LineOf(hit.Key) is { } line)
        {
            foreach (var (each, rect) in Controls(line))
            {
                if (each == hit)
                {
                    return OnPanel(rect);
                }
            }
        }
        return default;
    }

    /// <summary>
    /// §529: what <paramref name="hit"/> says in a tooltip: a header's icon button its name (it shows no words), §551 a row's
    /// too, §575 an entry showing its icon alone its words, §578 the errors' icon button and §596 a selection's entry whose
    /// words are cut short too; null for anything that says itself.
    /// </summary>
    public string? TipOf(PanelHit hit) => hit.Kind switch
    {
        PanelHitKind.Nav or PanelHitKind.Errors when PartOf(hit) is { } entry && (entry.Compact || ButtonWide(entry.Words, chevron: false) > entry.Rect.Width) => entry.Words,
        PanelHitKind.TitleAct when HeaderCard is { IsEmpty: false } => Actions.FirstOrDefault(each => each.Item.Id == hit.Id).Item?.Text,
        PanelHitKind.Each => S.DockPanelEach,
        // §551: a row's icon button its name (greyed, why: the last layout's Delete), a colour button its row's colour's.
        // §551: a row put in order's handle how it is moved: "Drag to move, or press Ctrl+Up or Ctrl+Down".
        PanelHitKind.Handle => S.DockEditFaceDrag,
        PanelHitKind.Tool when Row(hit.Key) is { } row && hit.Index >= 0 && hit.Index < row.Tools.Count => row.Tools[hit.Index].Name,
        // §610: a button beside a dropdown (its line's pair) its whole name ("Reset Graphite to default"): its words shorter, or its icon alone.
        // §638 one of words alone (Lometics' site) its words where it is its icon alone.
        PanelHitKind.Act when LineOf(hit.Key) is { Row.Button: { } beside } line && beside.Key == hit.Key =>
            beside.Detail ?? (BesideWide(line) <= P(PanelMetrics.Control) ? beside.Text : null),
        // §587: a dropdown's value cut short in its one box: whole.
        PanelHitKind.Pop when Row(hit.Key) is { Kind: PanelRowKind.Choice, Adds: false, Value: { } said } row && text.Width(said, P(PanelMetrics.Body)) > ValueRoom(row) => said,
        // §672: an inline picker's head what it is set to, cut short at a control's width, whole in its tooltip.
        PanelHitKind.Head when Row(hit.Key) is { Kind: PanelRowKind.Picker, Value: { } said } && text.Width(said, P(PanelMetrics.Body)) > P(PanelMetrics.ControlWide) => said,
        // §592: a tab whose words are cut short (a strip too narrow for them all): whole.
        PanelHitKind.Tab when LineOf(hit.Key) is { } strip && TabsAt(strip) is var tabs && hit.Index >= 0 && hit.Index < tabs.Count && tabs[hit.Index].Cut => tabs[hit.Index].Tab.Text,
        // §586: an add page's tile or folder that says how many of it the layout has, what the number is.
        PanelHitKind.Act or PanelHitKind.Head when Row(hit.Key) is { Item.Placed: > 0 and var placed } => S.DockAddPlaced(placed),
        _ => null,
    };

    /// <summary>
    /// The line of the page at <paramref name="key"/> — a pair's (<see cref="PanelRow.Pair"/>) too, the line it stands in: §609
    /// a holiday calendar's region's its calendar's, §610 the palette's reset's Palette's, §613 a value's reset's its value's;
    /// §592 a tab's, its strip's —; null where it is not shown (in a shut expander, another page).
    /// </summary>
    public PanelLine? LineOf(string key) => Blocks.SelectMany(block => block.Lines).FirstOrDefault(line =>
        line.Row.Key == key || line.Row.Pairs.Any(pair => pair.Key == key) || (line.Row.Kind == PanelRowKind.Tabs && line.Row.Rows.Any(tab => tab.Key == key)));

    /// <summary>
    /// What the keys stop on, in order (§520 decision 11): §575 the bar's — its entries, the selection's, the errors, Add, Done,
    /// as they stand —, then the flyout's: the add list's search, the page's title buttons and each control in its order. Greyed
    /// ones too: they are in the tree, and say so. §592 of a tab strip its chosen tab alone (the arrows go along it).
    /// </summary>
    public List<PanelHit> Stops()
    {
        List<PanelHit> stops = [];
        foreach (var part in Header)
        {
            stops.Add(HitOf(part));
        }
        if (!IsOpen)
        {
            return stops;
        }
        if (!Filter.IsEmpty)
        {
            stops.Add(new(PanelHitKind.Filter));
        }
        // §628: a page under another's entry its way back first, as it reads.
        if (!BackHead.IsEmpty)
        {
            stops.Add(new(PanelHitKind.Back));
        }
        foreach (var (item, _) in Actions)
        {
            stops.Add(new(PanelHitKind.TitleAct, item.Id, Key: item.Text));
        }
        foreach (var line in Blocks.SelectMany(block => block.Lines))
        {
            stops.AddRange(Controls(line).Select(each => each.Hit).Where(hit => hit.Kind != PanelHitKind.Tab || hit.Index == Chosen(line.Row)));
        }
        return stops;
    }
}

/// <summary>§520: the bar's and the panel's glyphs (Segoe Fluent Icons).</summary>
internal static class PanelGlyphs
{
    public const string Add = "";
    public const string Done = "";
    public const string Down = "";
    public const string Up = "";
    public const string Right = "";
    public const string Left = "";
    public const string Minus = "";
    public const string ErrorRing = "";
    public const string ErrorX = "";
    public const string Power = "";
    public const string Search = "";
}
