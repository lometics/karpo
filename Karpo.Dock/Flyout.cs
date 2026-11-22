// Karpo's menus, all of them: a Windows 11 context menu (WinUI's MenuFlyout),
// which is also the taskbar's jump list when it carries headers, file icons
// and detail lines, and a Start-folder-like grid of tiles. The window, the
// chrome, the placement and the focus and dismissal rules are FlyoutHost's;
// this is the menu inside.
//
// A menu (FlyoutLayout.List) is Explorer's context menu, measured off a capture of
// Explorer's own on Windows 11 26H2 at 150 % (2026-10-16, docs\widgets.md §178):
// radius 8, a 1 DIP rim (#3B3B3B in the dark) and the flyout shadow; 3 DIP of
// padding; fills 28 DIP high, 32 apart, 5 DIP in from the sides, radius 4; the
// icon 16 DIP in from the edge and the words 44 DIP in, all in the middle of the
// fill; 14 DIP text; the shortcuts 12 DIP in the secondary colour, one column from
// its left, its right 16 DIP in; the thin chevron 16 DIP in, and a check mark in
// its place (a menu with one keeps that slot on every row, the shortcuts in from
// it, docs\widgets.md §§); rules 1 DIP with 4 DIP either side; Explorer's own
// two-colour pictures where Windows has them (ExplorerIcons); the row of buttons on
// top (Flyout.Primary.cs). Every menu is one width (MenuWidthDip), as Windows 11's
// jump lists are, or its row of buttons' where that is wider, and a long label is
// cut with an ellipsis — a widget's menu (Configures) and its levels, which open in place of its
// rows (Flyout.Inline.cs, §271), are one wider width of their own, SettingsWidthDip, whatever they
// say (§230). A list of an island's content (FlyoutLayout.Panel) keeps
// WinUI's MenuFlyout tokens — items 32 DIP high with 2 DIP margins, 2 DIP of
// padding, separators 1 DIP with 1 DIP around them — and has the quick settings'
// 360 DIP, as every content flyout.

using System.Numerics;
using System.Runtime.InteropServices;
using Karpo.Dock.Native;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using Windows.UI.Composition;
using static TerraFX.Interop.Windows.Windows;
using Edge = Karpo.Core.Edge;
using ERROR = TerraFX.Interop.Windows.ERROR;

namespace Karpo.Dock;

public enum FlyoutLayout
{
    Strip,
    /// <summary>Rows: a context menu, or a jump list, <see cref="Flyout.MenuWidthDip"/> wide.</summary>
    List,

    /// <summary>Tiles with the name under a 32 px icon, up to four across, as a Start folder.</summary>
    Grid,

    /// <summary>
    /// Rows as an island's content (keyboard layouts, the weather's first step):
    /// <see cref="Flyout.PanelWidthDip"/> wide, as the quick settings and every other
    /// content flyout.
    /// </summary>
    Panel,
}

public abstract record FlyoutIcon
{
    /// <summary>A Segoe Fluent Icons code point, as text.</summary>
    public sealed record Glyph(string Code) : FlyoutIcon;

    /// <summary>A shell icon.</summary>
    public sealed record Picture(Pixels Pixels) : FlyoutIcon;
}

/// <summary>One line of a flyout: a command, a <paramref name="Header"/> or a <paramref name="Separator"/>.</summary>
/// <param name="Detail">A second, quieter line under the text (List only).</param>
/// <param name="Accelerator">Shortcut text shown at the right, as "Ctrl+C" (List only).</param>
/// <param name="Checked">A check mark when true; null for an item that is not checkable.</param>
/// <param name="Children">A submenu, opened from this item (List only).</param>
/// <param name="Words">edit: what the add page's search finds the item by besides its text — its description and its search words (AddList.Results).</param>
/// <param name="Primary">
/// menus: one of the icon-only buttons of the row a Windows 11 context menu has (Explorer's Cut, Copy,
/// Delete), its text its tooltip and the name a screen reader says. Wherever they stand in the list,
/// the menu draws them as one row on top (<see cref="Flyout.WithPrimaryRow"/>; List only).
/// </param>
/// <param name="Control">
/// menus: a toggle switch or a stepper at the row's end, which acts without closing the menu
/// (<see cref="FlyoutEventKind.Adjust"/>, Flyout.Controls.cs; List only).
/// </param>
/// <param name="Keys">
/// menus: the row's shortcut (MenuChord.cs): shown as <see cref="Accelerator"/> is, and chosen by the
/// keys while the menu stands open.
/// </param>
/// <param name="Stays">
/// menus: picked, the menu stays open, submenus and all, and says so as a control does
/// (<see cref="FlyoutEventKind.Adjust"/>): a setting's choice, an edit made where the menu stands
/// (MenuKeeps.cs, §219).
/// </param>
/// <param name="Reorder">
/// menus: rows of the same number but 0 are put in order by dragging them among each other, or by
/// Ctrl+Up and Ctrl+Down (<see cref="FlyoutEventKind.Reorder"/>, Flyout.Reorder.cs; List only, not in a submenu).
/// §366: a number's negative, a row that stands among them in their columns — a grip's room at its start
/// where they have grips, its chevron's slot — but is neither dragged nor counted among them (a display's
/// None among its layouts).
/// </param>
/// <param name="Value">
/// menus: what a submenu's row is set to, at its end before its chevron, as Windows 11's settings rows
/// say it (Flyout.Values.cs, §228; List only).
/// </param>
/// <param name="Foot">
/// menus: the row, one of the last, stands still at the menu's foot with the one rule over them while the
/// rest scroll over it (Flyout.Inline.cs, §276, §314; List only): a folder line's "Height ›", a widget's "Appearance ›".
/// </param>
/// <param name="Note">
/// menus: words to read (OptionRowKind.Note, §339): drawn as a row that may be used, its
/// <paramref name="Detail"/> wrapped whole, but nothing lights it, picks it or stops the keys on it (List only).
/// </param>
/// <param name="Opens">
/// menus: a link row's (OptionRowKind.Link, §351) what it opens, a screen reader's help on it: "Opens Accounts
/// in Settings"; null for any other row.
/// </param>
/// <param name="PickOne">
/// menus: its submenu is one choice — a country, a unit, a colour, a time zone —: a choice picked on its level
/// in place goes back to the level under it as the menu is shown again for the pick (Flyout.Inline.cs, §392).
/// A level of switches, of rows or of steppers keeps it false, and stays.
/// </param>
/// <param name="Shows">
/// §536: the look the rows under it edit (§585 the taskbar menu's expander, §592 the Appearance's tabs; Karpo.Core's LookRows):
/// the settings panel draws a sample of what it styles over them (LookPreviews). A menu shows nothing of it.
/// </param>
public sealed record FlyoutItem(
    uint Id,
    string Text,
    FlyoutIcon? Icon = null,
    string? Detail = null,
    bool Header = false,
    bool Separator = false,
    bool Enabled = true,
    string? Accelerator = null,
    bool? Checked = null,
    IReadOnlyList<FlyoutItem>? Children = null,
    IReadOnlyList<string>? Words = null,
    bool Primary = false,
    FlyoutControl? Control = null,
    MenuChord? Keys = null,
    bool Stays = false,
    int Reorder = 0,
    FlyoutValue? Value = null,
    bool Foot = false,
    bool Note = false,
    string? Opens = null,
    bool PickOne = false,
    bool Grows = false,
    bool Adds = false,
    Karpo.Core.LookShown? Shows = null)
{
    /// <summary>
    /// menus: the shortcut a row shows at its right and a button of the row in its tooltip — its own
    /// words (<see cref="Accelerator"/>, a window's menu's), or its <see cref="Keys"/>, which the menu
    /// also takes while it stands open.
    /// </summary>
    public string? Shortcut => Accelerator is { Length: > 0 } words ? words : Keys?.Text;

    /// <summary>§528: the setting of several widgets selected that do not agree on it (OptionRow.Mixed): the settings panel's control says "Mixed".</summary>
    public bool Mixed { get; init; }

    /// <summary>§529: a <see cref="Mixed"/> row's widgets' own rows, each named for its widget, set on it alone; null for none.</summary>
    public IReadOnlyList<FlyoutItem>? Theirs { get; init; }

    /// <summary>§529: a row of buttons whose level is its own settings (a folder's face): the settings panel shows them nested under it, no expander.</summary>
    public bool Nests { get; init; }

    /// <summary>§689: a link's words (<see cref="Detail"/>) its second line, under its name — an error's why —, not a line of words of its own.</summary>
    public bool DetailUnder { get; init; }

    /// <summary>§682: how many steps in its row stands under the one before it (a mail folder under its parent: MailTree); 0 for none.</summary>
    public int Indent { get; init; }

    /// <summary>§529: the setting the row shows, by its key (OptionRow.Key, a group's OptionRows.GroupKey); null for none said.</summary>
    public string? Setting { get; init; }

    /// <summary>
    /// §529: the <see cref="Setting"/> of the choice the row only matters after (OptionRow.Under): the settings panel stands
    /// it nested under that row; a menu shows it where it is. Null for a row of its own.
    /// </summary>
    public string? Under { get; init; }

    /// <summary>
    /// §610: stands in the line of the row it is beside, its control beside that row's (OptionRow.Beside; the settings panel's
    /// PanelRow.Pair): the row before it, or, first of its level, the thing whose level it is (§609 a holiday calendar's
    /// region). A menu shows it where it is.
    /// </summary>
    public bool Beside { get; init; }

    /// <summary>§613: a row that acts drawn as an icon button, Windows' subtle one (OptionRow.Subtle: a value's reset beside it).</summary>
    public bool Subtle { get; init; }

    /// <summary>
    /// §627: a <see cref="Separator"/> that starts a column: the rows after it stand beside the ones before it, from the level's
    /// top, a hairline between (Flyout.Columns.cs: a colour's picker, its swatches beside its mixer).
    /// </summary>
    public bool Column { get; init; }

    /// <summary>§556: the font its words are drawn in (a font's choice, its family's heading: OptionRow.ChoiceFaces); null for the menu's own.</summary>
    public Karpo.Core.TextStyle? Face { get; init; }

    /// <summary>§586: the add page's — how many of its widget the layout has, a folder of the tree its widgets' sum (AddCounts); 0 for none.</summary>
    public int Placed { get; init; }

    /// <summary>§595: a thing's badge after its name (OptionThing.Badge, the default layout's "Default"): the settings panel draws it; null for none.</summary>
    public string? Badge { get; init; }
}

public enum FlyoutEventKind
{
    Nothing,

    /// <summary>The item <see cref="FlyoutEvent.Id"/> was chosen (in the flyout or a submenu of it); it has closed.</summary>
    Invoke,

    /// <summary>The flyout closed without a choice.</summary>
    Dismissed,

    /// <summary>
    /// §622: the pointer or the keys came to rest on the item <see cref="FlyoutEvent.Id"/> — 0 for none: off every row, out
    /// of the menu —, a moment ago (<see cref="Flyout.HoverMs"/>). Only where <see cref="Flyout.TellsHover"/> is set.
    /// </summary>
    Hover,

    /// <summary>
    /// The item <see cref="FlyoutEvent.Id"/> was right-clicked in a flyout that takes right
    /// clicks (<see cref="Flyout.ContextClicks"/>); it has closed, as for <see cref="Invoke"/>.
    /// </summary>
    Context,

    /// <summary>
    /// tray: the item <see cref="FlyoutEvent.Id"/> is being dragged, the pointer at
    /// <see cref="FlyoutEvent.At"/> (screen px). Only where <see cref="Flyout.DragItems"/> is set;
    /// the flyout stays open and keeps the pointer.
    /// </summary>
    DragMove,

    /// <summary>tray: the dragged item was let go at <see cref="FlyoutEvent.At"/>; the flyout stays open.</summary>
    DragDrop,

    /// <summary>tray: the drag was called off (Escape); nothing moved, and the flyout stays open.</summary>
    DragCancelled,

    /// <summary>
    /// tray: the pointer moved, over the item <see cref="FlyoutEvent.Id"/>, or over none (0) — or
    /// left the flyout. Only where <see cref="Flyout.TellsPointer"/> is set.
    /// </summary>
    Pointer,

    /// <summary>tray: the pointer came to rest on the item <see cref="FlyoutEvent.Id"/> (the system's hover time); where <see cref="Flyout.TellsPointer"/> is set.</summary>
    Rest,

    /// <summary>tray: the left button went down on the item <see cref="FlyoutEvent.Id"/>; the flyout stays open. Where <see cref="Flyout.TellsPointer"/> is set.</summary>
    Press,

    /// <summary>
    /// tray: the left button came up on the item <see cref="FlyoutEvent.Id"/> it went down on, and
    /// the flyout stays open: what is a choice elsewhere (<see cref="Invoke"/>) where
    /// <see cref="Flyout.TellsPointer"/> is set.
    /// </summary>
    Release,

    /// <summary>
    /// menus: a row's control acted — a toggle flipped (the row's id), a stepper's button pressed (the
    /// id of its step) — or a row that stays picked (<see cref="FlyoutItem.Stays"/>), by the pointer or
    /// the keys; the menu stays open (Flyout.Controls.cs).
    /// </summary>
    Adjust,

    /// <summary>
    /// menus: the row <see cref="FlyoutEvent.Id"/> was dragged, or moved by Ctrl+Up or Ctrl+Down, to
    /// place <see cref="FlyoutEvent.To"/> among the rows of its <see cref="FlyoutItem.Reorder"/>; the menu
    /// stays open, its rows where they were until it is shown again (Flyout.Reorder.cs).
    /// </summary>
    Reorder,

    /// <summary>
    /// menus: the key <see cref="FlyoutEvent.Id"/> (a virtual key), which the menu has no row nor field for and
    /// <see cref="Flyout.PassesKey"/> says is of what it stands over — in edit mode the selection's shortcuts, Ctrl+V,
    /// Ctrl+Z — handed on to it; the menu has closed, as a row's choice closes it (the person, 2026-11-03: "when the
    /// context menu is open i should still be able to perform the same keyboard shortcuts like cut copy etc.").
    /// </summary>
    PassedKey,

    /// <summary>
    /// menus, §523: a colour of one's own being mixed in a colour row's picker (Flyout.Mixer.cs) — its spectrum or a slider
    /// dragged, a key on it —: <see cref="FlyoutEvent.Color"/> from the row <see cref="FlyoutEvent.Id"/>, a look, nothing kept;
    /// the menu stays.
    /// </summary>
    Mix,

    /// <summary>menus, §523: the colour mixed let go, or the keys gone from the picker: <see cref="FlyoutEvent.Color"/> kept; the menu stays.</summary>
    MixKept,

    /// <summary>menus, §523: Escape while a colour was mixed: <see cref="FlyoutEvent.Color"/>, the one before it, back; the menu stays.</summary>
    MixUndone,
}

/// <summary>
/// How tall a flyout's list is, exactly, scrolling past it (<see cref="Flyout.Rows"/>): <paramref name="Dip"/>
/// DIP at a text size of 100 %, growing with the text as its rows do (the widget's
/// a flyout height, gone since 2026-10-11). <paramref name="Tail"/> is how many of its items at the end
/// are not of the list: a VPN's separator and its settings row, which follow the list's room rather
/// than sitting in it.
/// </summary>
public readonly record struct FlyoutList(int Dip, int Tail = 0);

/// <summary>menus, for tests: where a menu's row put its parts (<see cref="Flyout.Looks"/>), in px from the panel's corner.</summary>
internal readonly record struct MenuRowLook(uint Id, Flyout.Area Plate, Flyout.Area Icon, Flyout.Area Words, Flyout.Area Keys, bool Explorer, Flyout.Area Check = default, Flyout.Area Value = default);

/// <param name="At">For the drags: where the pointer is, in screen px.</param>
/// <param name="To">menus: for <see cref="FlyoutEventKind.Reorder"/>, the row's new place among its own.</param>
/// <param name="Color">menus, §523: for <see cref="FlyoutEventKind.Mix"/> and its kin, the colour.</param>
public readonly record struct FlyoutEvent(FlyoutEventKind Kind, uint Id = 0, POINT At = default, int To = 0, Karpo.Core.Argb Color = default);

public sealed unsafe partial class Flyout : IDisposable
{
    /// <summary>The window class of every popup on a <see cref="FlyoutHost"/>. The dock registers it with its own window procedure.</summary>
    public const string ClassName = "karpo.flyout";

    /// <summary>
    /// One width for every menu (a right-click menu, a jump list, a submenu), as Windows 11's
    /// jump lists have one: a long label is cut with an ellipsis instead of widening the menu.
    /// Grows with Windows' text size only.
    /// </summary>
    public const float MenuWidthDip = 256;


    /// <summary>The quick settings' width, which every flyout of an island's content shares (<see cref="FlyoutLayout.Panel"/>).</summary>
    public const float PanelWidthDip = 360;

    /// <summary>
    /// The rows the add list and a menu need beside their anchor before they open on its other side,
    /// or over it (<see cref="FlyoutPlacement.Roomiest"/>).
    /// </summary>
    public const int RoomiestRows = 8;

    // Submenus are windows of a class of the flyout's own, which never take the
    // activation: the keys stay with the flyout the dock knows of.
    private const string SubmenuClassName = "karpo.flyout.submenu";

    // Posted to the root flyout by a submenu: wParam is the showing it belongs
    // to, lParam the id chosen.
    private const uint WM_KARPO_FLYOUT_CHOSEN = WM.WM_USER + 0x6E;

    // Posted to the flyout by Narrator's Invoke on a row that stays (ActUia): wParam is the showing it
    // belongs to, lParam the id; the menu stays open.
    private const uint WM_KARPO_FLYOUT_ADJUST = WM.WM_USER + 0x6F;

    private static readonly string CheckGlyph = char.ConvertFromUtf32(0xE73E);
    private static readonly string ChevronGlyph = char.ConvertFromUtf32(0xE974);

    /// <summary>A submenu's chevron in a mirrored menu (Mirror.cs): it points the way the submenu opens.</summary>
    private static readonly string ChevronBackGlyph = char.ConvertFromUtf32(0xE973);

    /// <summary>menus: Explorer's submenu chevron (ChevronRight), and the mirrored menu's (ChevronLeft).</summary>
    private static readonly string ThinChevronGlyph = char.ConvertFromUtf32(0xE76C);
    private static readonly string ThinChevronBackGlyph = char.ConvertFromUtf32(0xE76B);


    private static bool submenuClassRegistered;

    private readonly Flyout? parent;
    private readonly HINSTANCE instance;
    private readonly FlyoutHost host;
    private GCHandle self;
    private Flyout? submenu;
    // The row whose submenu is open (-1: none). A submenu opens only by a click or the keys, never by
    // the pointer resting on its row (§246).
    private int submenuFor = -1;
    // The keys go to the open submenu: it was opened by them, or the pointer is in it.
    private bool keysInSubmenu;
    private bool disposed;

    // What a submenu needs from the showing.
    private Gfx? gfx;
    private Look look;
    private float scale = 1;
    private RECT work;

    private readonly List<Row> rows = [];
    private int hot = -1;

    // narrator: what the menu is called, for a screen reader: its heading, where it has one.
    private string? spokenTitle;
    private int pressed = -1;
    // The row the right button went down on, while ContextClicks.
    private int rightPressed = -1;
    private FlyoutTheme theme = FlyoutTheme.For(default);
    private CompositionColorBrush? hoverBrush;

    /// <summary>§633: the list shown is a control's popup, drawn as the settings pages are (PanelMetrics.Popup), not as a menu.</summary>
    private bool settingsStyle;
    private CompositionColorBrush? pressedBrush;
    private ContainerVisual? content;
    private SpriteVisual? indicator;

    // tray: the slide an item makes aside for a dragged icon (DropGap); made the first time.
    private ImplicitAnimationCollection? gapSlide;
    private CompositionRoundedRectangleGeometry? indicatorShape;
    // Where the rows show, in window px.
    private Area viewport;

    // §379: the panel's padding over the rows and under them, px, which the first and the last row take (RowAt).
    private (float Above, float Below) padTaken;
    private float contentHeight;
    // Arabic and Urdu: the rows are turned across the panel, a submenu opens to the left, and Left
    // goes on and Right back (Mirror.cs). Not a grid of tiles.
    private bool mirrored;
    private float scroll;
    private float wheelStep;
    // Room kept above or below a row scrolled into view by the keys.
    private float nudge;

    /// <param name="dock">Goes into the flyout's GWLP_USERDATA, so the dock's window procedure
    /// finds the dock from the flyout's messages too.</param>
    /// <param name="className">
    /// The window class; a test registers one with a procedure of its own. A class belongs to
    /// whichever code registers it first and <c>RegisterClassW</c> says nothing when the name is
    /// taken, so two test classes on one name means one of them gets the other's procedure and
    /// every message to its windows is dropped without a word. That is not hypothetical: it was
    /// hiding a real defect in the clock panel and leaving <c>FlyoutRuns</c> exercising nothing at
    /// all (see the note at the top of <c>Karpo.Tests\Islands\FlyoutOffscreenTests.cs</c>).
    /// </param>
    public Flyout(Gfx gfx, HINSTANCE instance, nint dock, FlyoutMode mode = FlyoutMode.Menu, string className = ClassName)
    {
        this.instance = instance;
        tipDock = dock;
        tipClass = className;
        host = new FlyoutHost(gfx, instance, dock, mode, className)
        {
            AlsoInside = InsideSubmenus,
            AlsoKeeps = SubmenuWindows,
        };
    }

    /// <summary>
    /// §514: what this showing is is a flyout a widget opens (a widget's list, the tray's and the app buttons' overflow), drawn
    /// as a folder is, on a taskbar flyout's surface (<see cref="FlyoutHost.Taskbar"/>); a menu otherwise. Set before each
    /// showing.
    /// </summary>
    internal bool Taskbar
    {
        get => host.Taskbar;
        set => host.Taskbar = value;
    }

    /// <summary>
    /// folders: a menu over a folder uses up the press that closes it, outside it and its submenus:
    /// the folder, a bar, another app hear nothing of it, and only the menu closes
    /// (<see cref="FlyoutHost.UsesUpPresses"/>). Set before each showing.
    /// </summary>
    internal bool UsesUpPresses
    {
        get => host.UsesUpPresses;
        set => host.UsesUpPresses = value;
    }

    // A menu's submenus' windows are part of it for the hook, open or not: a hidden one is under no point.
    private IEnumerable<HWND> SubmenuWindows()
    {
        for (var menu = submenu; menu is not null; menu = menu.submenu)
        {
            yield return menu.Hwnd;
        }
    }

    private Flyout(Flyout parent, Gfx gfx)
    {
        this.parent = parent;
        instance = parent.instance;
        tipDock = parent.tipDock;
        tipClass = parent.tipClass;
        self = GCHandle.Alloc(this);
        RegisterSubmenuClass();
        // A submenu is its menu's: over what the menu opened from (§359).
        host = new FlyoutHost(gfx, instance, GCHandle.ToIntPtr(self), FlyoutMode.Tool, SubmenuClassName) { Stratum = Stratum.Menu };
    }

    /// <summary>The flyout's window. A new one if Alt+F4 destroyed the last, so read it afresh.</summary>
    public HWND Hwnd => host.Hwnd;

    /// <summary>The tokens the menu is drawn in now: its window's (<see cref="FlyoutHost.Theme"/>).</summary>
    internal FlyoutTheme Theme => host.Theme;

    /// <summary>§526, stage: the font its words are in (<see cref="Look.MenuTypeface"/>).</summary>
    internal Karpo.Core.TextStyle Font => look.MenuTypeface;

    public bool IsOpen => host.IsOpen;

    /// <summary>Whether this showing is a look (<see cref="FlyoutHost.Previewed"/>): a list of choices opened for the settings app or the selection in edit mode.</summary>
    internal bool Previewed => host.Previewed;

    /// <summary>Where the press that closed it the last time was; null where a key, another window or the dock closed it (<see cref="FlyoutHost.DismissedAt"/>).</summary>
    internal POINT? DismissedAt => host.DismissedAt;
    public FlyoutMode Mode => host.Mode;

    /// <summary>
    /// The left button is down on one of its rows, or a row is being dragged: its rows are numbered by
    /// their place, so it is not shown again with other rows until that is over (Dock.FollowOverflow).
    /// </summary>
    internal bool Pressing => pressed >= 0 || dragging;

    /// <summary>The panel's size in px, for tests: what the host gave it.</summary>
    internal Vector2 PanelSize => host.PanelSize;

    /// <summary>How tall its rows or tiles are in all, in px, for tests: what it shows with room for everything.</summary>
    internal float ContentHeight => contentHeight;

    /// <summary>How much of its rows or tiles shows, in px, for tests: all of them unless it scrolls.</summary>
    internal float ShownHeight => viewport.H;

    public Func<POINT, bool>? AlsoInside { get; set; }
    private long outsideSince;
    public POINT ItemAnchor(uint id, Karpo.Core.Edge edge)
    {
        var row = rows.FirstOrDefault(row => row.Item.Id == id);
        var panel = host.Panel;
        var bounds = row?.Bounds ?? new Area(panel.X, panel.Y, panel.W, panel.H);
        var at = edge switch
        {
            Karpo.Core.Edge.Top => new POINT((int)(viewport.X+bounds.X+bounds.W/2), (int)(panel.Y+panel.H)),
            Karpo.Core.Edge.Left => new POINT((int)(panel.X+panel.W), (int)(viewport.Y+bounds.Y+bounds.H/2-scroll)),
            Karpo.Core.Edge.Right => new POINT((int)panel.X, (int)(viewport.Y+bounds.Y+bounds.H/2-scroll)),
            _ => new POINT((int)(viewport.X+bounds.X+bounds.W/2), (int)panel.Y)
        };
        ClientToScreen(Hwnd, &at); return at;
    }

    /// <summary>The icons of a strip or of a group's content, in px: the bar's own size.</summary>
    public float StripIconSize { get; set; } = 24;

    /// <summary>The most tiles a row of a grid holds unless told otherwise (<see cref="GridColumns"/>).</summary>
    public const int DefaultGridColumns = 4;

    /// <summary>
    /// The side, in px, of one icon's square in a group's content without titles; 0 for the icon and
    /// 10 DIP either side. tray: the overflow's is the bar's own tray square
    /// (<see cref="Layout.TrayOverflowCell"/>).
    /// </summary>
    public float StripCell { get; set; }

    /// <summary>The most tiles a row of a grid holds: the tray's overflow five, as Windows 11's does.</summary>
    public int GridColumns { get; set; } = DefaultGridColumns;

    /// <summary>
    /// tray: a grid as large as its icons' squares and <see cref="FittedPadDip"/> around them, and no
    /// larger (<see cref="FittedSize"/>), as Windows 11's overflow is: the tray's hidden icons. A grid
    /// is otherwise at least 120 DIP wide with its tiles centred in it, and keeps 8 DIP around them,
    /// which round one or two icons was a panel twice their size.
    /// </summary>
    public bool FitsItsIcons { get; set; }

    /// <summary>
    /// tray: the empty squares a grid that fits its icons keeps after them: the overflow opened by a
    /// drag from the bar has room for the icon it may take, and is one empty square where it holds
    /// none, somewhere to let the first go (Dock.Tray.cs). 0 otherwise.
    /// </summary>
    public int SpareTiles { get; set; }

    /// <summary>tray: the room a grid that fits its icons keeps between its squares and its edge, in DIP.</summary>
    internal const float FittedPadDip = 4;

    /// <summary>
    /// tray: the panel of a grid that fits its icons (<see cref="FitsItsIcons"/>): <paramref name="columns"/>
    /// by <paramref name="rows"/> squares of <paramref name="cell"/> px, and the room around them.
    /// </summary>
    internal static Vector2 FittedSize(int columns, int rows, float cell, float scale)
    {
        var pad = Round(FittedPadDip * scale);
        return new Vector2(MathF.Ceiling(2 * pad + columns * cell), 2 * pad + rows * cell);
    }

    /// <summary>The content is a pin group's members or the tray's hidden icons: icons, at <see cref="StripIconSize"/>.</summary>
    public bool GroupContent { get; set; }

    /// <summary>A group's members show their names under their icons.</summary>
    public bool GroupShowTitles { get; set; }

    /// <summary>
    /// §622: the menu tells the row the pointer or the keys rest on (<see cref="FlyoutEventKind.Hover"/>), a moment after
    /// they came to it (<see cref="HoverMs"/>), and 0 once they rest on none: what the dock tries a look on for. Its
    /// submenus' rows too.
    /// </summary>
    public bool TellsHover { get; set; }

    /// <summary>
    /// §622: how long the pointer or the keys rest on a row before the menu tells it, in ms: sweeping through a list tells
    /// only where it stops.
    /// </summary>
    internal const uint HoverMs = 50;

    /// <summary>
    /// What a screen reader calls a menu shown without a title: a widget's menu draws none (the
    /// person, 2026-10-10: "remove their title, e.g. Folder - Calendar") but is still named for it.
    /// A drawn title wins; with neither the menu is "Menu".
    /// </summary>
    public string? SpokenName { get; set; }

    /// <summary>narrator: the name the menu is read out as (<see cref="SpokenName"/>).</summary>
    internal string UiaName => spokenTitle is { Length: > 0 } named ? named : S.DockUiaMenu;

    /// <summary>
    /// A right click on an item (button down and up on the same one) closes the flyout and says
    /// so (<see cref="FlyoutEventKind.Context"/>): the tray's hidden icons, whose apps open menus of
    /// their own. Otherwise right clicks do nothing.
    /// </summary>
    public bool ContextClicks { get; set; }


    /// <summary>
    /// tray: an item held and moved past the drag threshold is dragged, not chosen
    /// (<see cref="FlyoutEventKind.DragMove"/>): the tray's hidden icons, which can be dragged onto
    /// the bar or into another place in the overflow. The flyout keeps the pointer until it is let go.
    /// </summary>
    public bool DragItems { get; set; }

    /// <summary>
    /// tray: the flyout tells where the pointer is among its items as it moves and when it rests
    /// (<see cref="FlyoutEventKind.Pointer"/>, <see cref="FlyoutEventKind.Rest"/>), and a left
    /// button's down and up on an item as they happen (<see cref="FlyoutEventKind.Press"/>,
    /// <see cref="FlyoutEventKind.Release"/>), and stays open when one is clicked: the tray's hidden
    /// icons, whose apps hear all of that as they do on the bar — a second press within the
    /// double-click time a double click. It closes as a menu does, when the app takes the foreground
    /// or a press lands elsewhere; the keyboard still chooses (<see cref="FlyoutEventKind.Invoke"/>).
    /// </summary>
    public bool TellsPointer { get; set; }

    /// <summary>§521: the rows a control's list (a ComboBox's, <see cref="FlyoutPlacement.Control"/>) shows at most before it scrolls.</summary>
    internal const int ComboRows = 10;

    /// <summary>§521: the letters typed into a control's list in a moment, and when the last came: the row they start goes lit (type-to-jump).</summary>
    private (string Typed, long At) jumping;

    /// <summary>
    /// A list of choices of an exact height (<see cref="FlyoutList"/>): a VPN's connections, the
    /// keyboard's layouts. It is as tall as its <see cref="FlyoutList.Dip"/> however few or many it
    /// lists, and scrolls past it. Null, every other flyout: as tall as what it shows. Rows only.
    /// </summary>
    public FlyoutList? Rows { get; set; }

    /// <summary>
    /// A menu's width in px where it is not <see cref="MenuWidthDip"/>: a dropdown's list, its field's
    /// width, as WinUI's ComboBox opens it (SoundPartTile). Null, every other menu.
    /// </summary>
    public float? ListWidth { get; set; }

    /// <summary>
    /// menus: a widget's menu — its settings, its faces —, <see cref="SettingsWidthDip"/> wide whatever it
    /// says (Flyout.Width.cs, §230), its levels opening in place of its rows (Flyout.Inline.cs, §271);
    /// false, every other menu (a jump list keeps Windows' one width, the add list's subjects cascade).
    /// Set before each showing.
    /// </summary>
    public bool Configures { get; set; }

    /// <summary>
    /// menus: a configuration menu — edit mode's menu of a widget, its settings and its faces
    /// (Dock.WidgetMenu) —, <see cref="SettingsHeightDip"/> tall on every level, a field's too, to the room
    /// there is (Flyout.Width.cs, §288); false, a menu as tall as its rows (§286). Set before each showing.
    /// </summary>
    public bool Configuration { get; set; }

    /// <summary>
    /// menus, §517: one of the edit bar's menus (⚙ Karpo, Display, Taskbar, Look): as tall as its rows and as wide as its
    /// widest row needs — its words and what it is set to whole, every level's (a title is never cut, R14) —, from
    /// <see cref="SettingsWidthDip"/> up to the work area's width (Flyout.Width.cs); false, every other menu. §587 never a
    /// control's popup, which is its box's one width. Set before each showing.
    /// </summary>
    public bool FitsContent { get; set; }

    /// <summary>§517: the width a <see cref="FitsContent"/> menu's rows need, its whole tree's, worked out as it is shown and kept for its levels.</summary>
    private float fitWidth;

    /// <summary>
    /// edit: what a configuration menu is of, its type alone ("Clock", "Folder", "Taskbar"), at the start
    /// of its top row on its own rows (a level's head names the level), and the name Narrator reads it by
    /// (Flyout.Primary.cs, §323); null, none. Set before each showing.
    /// </summary>
    public string? TypeTitle { get; set; }

    /// <summary>
    /// menus: whether a key (a virtual key; Ctrl and Shift read as they are) that no row, field or control of the menu takes
    /// is of what the menu stands over, which then has it (<see cref="FlyoutEventKind.PassedKey"/>): the dock's, in edit mode
    /// the selection's shortcuts (EditKeys.OfTheSelection). Null, a menu that keeps every key.
    /// </summary>
    public Func<int, bool>? PassesKey { get; set; }

    /// <summary>
    /// edit: a widget's menu stands beside the widget, on the first of its sides with the room
    /// (<see cref="MenuSides"/>), rather than off the anchor; null, every other flyout. Set before each showing.
    /// </summary>
    public MenuBeside? BesideWidget { get; set; }

    // tray: the item the pointer was last told to be over, while TellsPointer; -1 for none.
    private int told = -1;

    // tray: the row being dragged and where it was pressed, in client px; -1 for none.
    private int dragRow = -1;
    private POINT dragFrom;
    private bool dragging;

    // tray: a row is held (pressed, or dragged): the flyout stays open whoever takes the foreground
    // meanwhile (Hold), and what StaysOpen was before.
    private bool holding;
    private bool heldOpen;

    // A pin group's row sits just above the bar, as close as the window list,
    // rather than a menu's 12 DIPs away.
    private const float StripGapDip = 8;

    /// <summary>§415: the room between a field's box and the line under it that says what it needs, in DIPs.</summary>
    private const float FieldDetailGapDip = 4;

    /// <summary>
    /// Shows <paramref name="items"/> beside <paramref name="anchor"/>, the middle of the inner side
    /// of what was clicked, away from <paramref name="edge"/>, kept inside <paramref name="work"/>,
    /// and takes the focus. Showing again while open replaces the contents.
    /// </summary>
    public void Show(
        Gfx gfx,
        Look look,
        float scale,
        IReadOnlyList<FlyoutItem> items,
        FlyoutLayout layout,
        POINT anchor,
        RECT work,
        Edge edge,
        string? title = null,
        float? gapDip = null,
        RECT? control = null,
        RECT? within = null)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        // §521: a popup of a control (the edit bar's, its panel's) hangs from it, as a ComboBox's list, WinUI's 4 DIP off it,
        // inside the sides of what it stands in where it fits (PopupPlace, §531).
        // No bar's edge: the side of its control it takes is its edge once placed (FlyoutHost.Arrange), under it to begin with.
        var placement = control is null
            ? new FlyoutPlacement(anchor, work, edge)
            : new FlyoutPlacement(anchor, work, Edge.Top, PopupPlace.GapDip, Control: control, Within: within);
        // A dropdown's list hangs off its field's middle by half the field and WinUI's 4 of air, so
        // it clears the field whichever side it opens to.
        if (gapDip is { } gap)
        {
            placement = placement with { GapDip = gap };
        }
        if (GroupContent && layout == FlyoutLayout.Strip)
        {
            placement = placement with { GapDip = StripGapDip };
        }
        // menus: a new showing starts on the menu's own rows (§268), nothing lit (§356), nothing picked (§392).
        levels.Clear();
        fitWidth = 0;
        pickedOne = false;
        choseOne = false;
        pointerScreen = null;
        fresh = true;
        try
        {
            Open(gfx, look, scale, items, layout, title, placement, null, 0);
        }
        finally
        {
            fresh = false;
        }
        // §521: a control's list (a ComboBox's) opens with its chosen row lit and in sight.
        if (control is not null && ComboList(layout, items) && rows.FindIndex(row => row.Item.Checked == true) is var chosen and >= 0)
        {
            Focus(chosen);
        }
        // A search's list: the caret in its field, what it finds under it as one types; §556 a filtered list (a font's) opens
        // on its choice, in sight under its filter.
        else if (control is not null && FieldOnOpening() is var field and >= 0)
        {
            Focus(field);
            ChosenTileInView();
        }
    }

    /// <summary>
    /// §559: the field a control's popup opens with the keys in: a search's or a filter's, what the popup is for; else a line's
    /// that leads it (a text box's, a number's), as WinUI puts the keys on a flyout's first control. None for a field among other
    /// controls: a colour's picker's hex box, under its swatches and its mixer, waits for a click or Tab — with the keys it took
    /// Escape for its own words, and the picker stood open. -1 for none.
    /// </summary>
    private int FieldOnOpening()
    {
        var finds = rows.FindIndex(row => row.Item.Control is FlyoutField { Finder: not null } or FlyoutField { Filters: true });
        if (finds >= 0)
        {
            return finds;
        }
        var first = rows.FindIndex(row => !row.Item.Separator && !row.Item.Header && !row.Item.Primary && row.Item.Enabled);
        return first >= 0 && rows[first].Item.Control is FlyoutField ? first : -1;
    }

    /// <summary>
    /// §521: a letter typed into a control's list (WinUI's ComboBox): the next row whose words start with what was typed in
    /// the moment (a second, the letters run together) goes lit and in sight; the same letter again the one after.
    /// </summary>
    /// <summary>
    /// §521: whether <paramref name="items"/> shown as <paramref name="layout"/> are a ComboBox's list: a list of rows to pick,
    /// none a tile, a field or a control (a colour's picker is none). §531: not the add list's columns, which hang from "+ Add"
    /// as a ComboBox's list hangs but keep their own height and their search.
    /// </summary>
    private static bool ComboList(FlyoutLayout layout, IReadOnlyList<FlyoutItem> items) =>
        layout == FlyoutLayout.List && items.All(item => item.Separator || item.Header || (item.Control is null && !IsTile(item)));

    /// <summary>§556: whether <paramref name="items"/> are a control's list with a filter over its rows (a font's): a ComboBox's list but for its filter.</summary>
    private static bool FilteredList(FlyoutLayout layout, IReadOnlyList<FlyoutItem> items) =>
        layout == FlyoutLayout.List && items.Any(item => item.Control is FlyoutField { Filters: true })
        && items.All(item => item.Separator || item.Header || item.Control is FlyoutField { Filters: true } || (item.Control is null && !IsTile(item)));

    private FlyoutEvent Jump(char c)
    {
        if (char.IsControl(c) || char.IsWhiteSpace(c) && jumping.Typed.Length == 0)
        {
            return default;
        }
        var now = Environment.TickCount64;
        var typed = now - jumping.At < 1000 && jumping.Typed.Length > 0 && !(jumping.Typed.Length == 1 && jumping.Typed[0] == c) ? jumping.Typed + c : c.ToString();
        jumping = (typed, now);
        var from = typed.Length > 1 ? Math.Max(0, hot) : hot + 1;
        for (var step = 0; step < rows.Count; step++)
        {
            var at = (from + step) % rows.Count;
            var item = rows[at].Item;
            if (!item.Separator && !item.Header && item.Enabled && item.Text.StartsWith(typed, StringComparison.CurrentCultureIgnoreCase))
            {
                Focus(at);
                break;
            }
        }
        return default;
    }

    /// <summary>
    /// How many of a pin group's <paramref name="count"/> members one page of its row shows:
    /// all of them when they fit in <paramref name="room"/> px of <paramref name="cell"/> px
    /// cells, and paging only when they don't, with as many as fit beside the two paging arrows.
    /// </summary>
    public static int StripPageSize(int count, float room, float cell)
    {
        if (count <= 0)
        {
            return 1;
        }
        var fit = cell > 0 && float.IsFinite(room) ? (int)MathF.Floor(room / cell) : count;
        return count <= fit ? count : Math.Max(1, fit - 2);
    }

    public void Hide() => Close(restore: true);

    /// <summary>tray: the open flyout moved to hang off <paramref name="anchor"/>, as it is (<see cref="FlyoutHost.Reanchor"/>).</summary>
    public void Reanchor(POINT anchor) => host.Reanchor(anchor);

    /// <summary>
    /// What a message to the flyout's window means. The dock passes every one of them here and
    /// then its default handling.
    /// </summary>
    public FlyoutEvent Message(uint msg, WPARAM wParam, LPARAM lParam)
    {
        // §526: what it measures and draws again as it hears it — a field typed in, a row lit — in the font it was opened in
        // (Open: its look's, a menu's), never Karpo's chrome's (Gfx.Typeface, the default layout's menus').
        if (gfx is null)
        {
            return Heard(msg, wParam, lParam);
        }
        using var typeface = gfx.Wearing(look.MenuTypeface);
        return Heard(msg, wParam, lParam);
    }

    /// <summary><see cref="Message"/>, in its look's font.</summary>
    private FlyoutEvent Heard(uint msg, WPARAM wParam, LPARAM lParam)
    {
        Driven(msg, lParam);
        // menus, §523: a colour mixed, said from a submenu or kept as the keys left the picker (Flyout.Mixer.cs).
        if (msg == WM_KARPO_FLYOUT_MIX)
        {
            return IsOpen && parent is null ? MixHeard(wParam, lParam) : default;
        }
        // menus: a row dragged into order is put back by Escape or a right press, and the menu stays (Flyout.Reorder.cs).
        if (reordering && (msg == WM.WM_RBUTTONDOWN || msg == WM.WM_KEYDOWN && (int)(nuint)wParam == VK.VK_ESCAPE))
        {
            ReorderCancel();
            return default;
        }
        // tray: a drag is called off by Escape or a right press, and the flyout stays open.
        if (dragging && (msg == WM.WM_RBUTTONDOWN || msg == WM.WM_KEYDOWN && (int)(nuint)wParam == VK.VK_ESCAPE))
        {
            var was = dragRow >= 0 && dragRow < rows.Count ? rows[dragRow].Item.Id : 0;
            EndDrag();
            return new FlyoutEvent(FlyoutEventKind.DragCancelled, was);
        }
        // menus: a text or a search field's answers, timers, IME and pointer (Flyout.Fields.cs).
        if (FieldMessage(msg, wParam, lParam) is { } fielded)
        {
            return fielded;
        }
        // The keys first: Escape closes an open submenu before the menu.
        if (msg == WM.WM_KEYDOWN)
        {
            return IsOpen && parent is null ? Key((int)(nuint)wParam) : default;
        }
        // menus: what is typed goes into a field (Flyout.Fields.cs), or a control's list jumps to the row it starts.
        if (msg == WM.WM_CHAR)
        {
            return IsOpen && parent is null ? FieldChar((char)(nuint)wParam) ?? (host.Placement.Control is not null && shownWith is { } combo && ComboList(combo.Layout, combo.Items) ? Jump((char)(nuint)wParam) : default) : default;
        }
        if (msg == WM_KARPO_FLYOUT_ADJUST)
        {
            return IsOpen && parent is null && (uint)(nuint)wParam == host.Generation
                ? new FlyoutEvent(FlyoutEventKind.Adjust, (uint)(nint)lParam)
                : default;
        }
        if (msg == WM_KARPO_FLYOUT_CHOSEN)
        {
            if (IsOpen && parent is null && (uint)(nuint)wParam == host.Generation)
            {
                Close(restore: false);
                return new FlyoutEvent(FlyoutEventKind.Invoke, (uint)(nint)lParam);
            }
            return default;
        }
        var happened = host.Message(msg, wParam, lParam);
        if (happened.Kind == FlyoutHostEventKind.Dismissed)
        {
            Close(restore: false);
            return parent is null ? new FlyoutEvent(FlyoutEventKind.Dismissed) : default;
        }
        switch (msg)
        {
            case WM.WM_MOUSEMOVE:
            {
                // menus, §523: a colour's spectrum or slider held follows the pointer (Flyout.Mixer.cs).
                if (mixing is { Held: not null })
                {
                    return MixMove(X(lParam), Y(lParam));
                }
                // menus: a row held and moved far enough is dragged into order among its own (Flyout.Reorder.cs).
                if (ReorderMove(X(lParam), Y(lParam)))
                {
                    return default;
                }
                // tray: an item held and moved far enough is dragged, not chosen.
                if (dragRow >= 0 && dragRow < rows.Count)
                {
                    var at = new POINT(X(lParam), Y(lParam));
                    var far = Math.Abs(at.x - dragFrom.x) > GetSystemMetrics(SM.SM_CXDRAG)
                        || Math.Abs(at.y - dragFrom.y) > GetSystemMetrics(SM.SM_CYDRAG);
                    if (dragging || far)
                    {
                        if (!dragging)
                        {
                            dragging = true;
                            SetPressed(-1);
                            SetHot(-1);
                        }
                        SetCursor(LoadCursorW(HINSTANCE.NULL, IDC.IDC_SIZEALL));
                        return new FlyoutEvent(FlyoutEventKind.DragMove, rows[dragRow].Item.Id, Screen(at));
                    }
                }
                Moved(X(lParam), Y(lParam));
                return TellsPointer && IsOpen ? Told(RowAt(X(lParam), Y(lParam))) : default;
            }
            case WM.WM_MOUSELEAVE:
                SetHotTool(-1, -1);
                SetHot(-1);
                return TellsPointer ? Told(-1) : default;
            case WM.WM_MOUSEHOVER:
                // tray: the pointer rests on the item it was told to be over.
                return TellsPointer && IsOpen && told >= 0 && told < rows.Count
                    ? new FlyoutEvent(FlyoutEventKind.Rest, rows[told].Item.Id)
                    : default;
            case WM.WM_LBUTTONDOWN:
            case WM.WM_RBUTTONDOWN:
                if (!IsOpen)
                {
                    return default;
                }
                if (!host.Panel.Contains(X(lParam), Y(lParam)))
                {
                    // In a submenu's shadow margin: outside the whole menu.
                    return Outside();
                }
                // menus: a press on a field's row puts the keys in it (Flyout.Fields.cs).
                if (msg == WM.WM_LBUTTONDOWN && FieldPressed(X(lParam), Y(lParam)))
                {
                    return default;
                }
                // menus, §523: a press on a colour's spectrum or slider holds it, the colour where it went (Flyout.Mixer.cs).
                if (msg == WM.WM_LBUTTONDOWN && MixPress(X(lParam), Y(lParam)) is { } mixed)
                {
                    return mixed;
                }
                // menus: a stepper steps as its button goes down, and again while held (Flyout.Pickers.cs).
                if (msg == WM.WM_LBUTTONDOWN && StepperPress(X(lParam), Y(lParam)) is { } stepped)
                {
                    return stepped;
                }
                if (msg == WM.WM_LBUTTONDOWN)
                {
                    SetPressed(RowAt(X(lParam), Y(lParam)));
                    if (pressed >= 0)
                    {
                        _ = SetCapture(Hwnd);
                    }
                    // menus: it may become a drag into order among its own (Flyout.Reorder.cs).
                    ReorderPress(pressed, X(lParam), Y(lParam));
                    // tray: it may become a drag onto the bar or to another place here.
                    dragRow = DragItems ? pressed : -1;
                    dragFrom = new POINT(X(lParam), Y(lParam));
                    dragging = false;
                    if (dragRow >= 0)
                    {
                        Hold();
                    }
                    // tray: the dock is told the button went down; its app hears of it only as a
                    // click, as the button comes up without a drag (Dock's TrayClicks).
                    if (TellsPointer && pressed >= 0)
                    {
                        return new FlyoutEvent(FlyoutEventKind.Press, rows[pressed].Item.Id);
                    }
                }
                else if (ContextClicks && parent is null)
                {
                    rightPressed = RowAt(X(lParam), Y(lParam));
                }
                return default;
            case WM.WM_RBUTTONUP:
            {
                // Down and up on the same item, where right clicks count (the tray's hidden icons).
                int was = rightPressed;
                rightPressed = -1;
                if (!IsOpen || !ContextClicks || parent is not null || was < 0 || was != RowAt(X(lParam), Y(lParam))
                    || rows[was].Item.Children is not null)
                {
                    return default;
                }
                var id = rows[was].Item.Id;
                Close(restore: false);
                return new FlyoutEvent(FlyoutEventKind.Context, id);
            }
            case WM.WM_LBUTTONUP:
            {
                // menus, §523: a colour's spectrum or slider let go: the colour kept (Flyout.Mixer.cs).
                if (MixRelease() is { } kept)
                {
                    return kept;
                }
                // tray: the row held is let go, so the flyout closes again for another window (Hold).
                LetGo();
                // menus: a held stepper steps no more (Flyout.Pickers.cs).
                StepperRelease();
                // menus: a row dragged into order is let go where it is (Flyout.Reorder.cs).
                if (ReorderRelease() is { } placed)
                {
                    SetPressed(-1);
                    if (GetCapture() == Hwnd)
                    {
                        _ = ReleaseCapture();
                    }
                    return placed;
                }
                // tray: it was dragged, so it is let go where it is, and nothing is chosen.
                if (dragging && dragRow >= 0 && dragRow < rows.Count)
                {
                    var dropped = rows[dragRow].Item.Id;
                    var at = Screen(new POINT(X(lParam), Y(lParam)));
                    EndDrag();
                    return new FlyoutEvent(FlyoutEventKind.DragDrop, dropped, at);
                }
                dragRow = -1;
                int was = pressed;
                SetPressed(-1);
                if (GetCapture() == Hwnd)
                {
                    _ = ReleaseCapture();
                }
                int under = RowAt(X(lParam), Y(lParam));
                if (!IsOpen || was < 0 || was != under)
                {
                    return default;
                }
                // tray: the button came up on the item it went down on, and the flyout stays open.
                if (TellsPointer)
                {
                    return new FlyoutEvent(FlyoutEventKind.Release, rows[under].Item.Id);
                }
                // menus: a row's tool is its own button, beside the row's submenu (Flyout.Controls.cs).
                if (rows[under].Item.Control is FlyoutControl.Tools && ToolAt(under, X(lParam), Y(lParam)) is { Grip: false })
                {
                    return ControlClicked(under, X(lParam), Y(lParam));
                }
                // menus: a level's head shown in place goes back (Flyout.Inline.cs, §268).
                if (rows[under].Item.Id == InlineBackId && BackInPlace())
                {
                    return default;
                }
                if (rows[under].Item.Children is not null)
                {
                    ParentClicked(under);
                    return default;
                }
                // menus: a row's control acts, and the menu stays (Flyout.Controls.cs).
                if (rows[under].Item.Control is not null)
                {
                    return ControlClicked(under, X(lParam), Y(lParam));
                }
                // menus: a row that stays is picked, and the menu stays, submenus and all (§219); a level of
                // one choice goes back (§392).
                if (rows[under].Item.Stays)
                {
                    return Picked(rows[under].Item);
                }
                return Chosen(rows[under].Item.Id);
            }
            // menus: the mouse's back button goes back from a level shown in place (Flyout.Inline.cs, §268).
            case WM.WM_XBUTTONUP when IsOpen && OnInlineLevel && parent is null && (((nuint)wParam >> 16) & 0xFFFF) == XBUTTON1:
                BackInPlace();
                return default;
            case WM.WM_CAPTURECHANGED:
                // menus, §523: a colour held whose pointer was taken away is kept where it was (Flyout.Mixer.cs).
                if ((HWND)(void*)(nint)lParam != Hwnd && MixRelease() is { } taken)
                {
                    return taken;
                }
                if ((HWND)(void*)(nint)lParam != Hwnd)
                {
                    ReorderCancel();
                    LetGo();
                    SetPressed(-1);
                    if (dragging && dragRow >= 0 && dragRow < rows.Count)
                    {
                        var was = rows[dragRow].Item.Id;
                        EndDrag();
                        return new FlyoutEvent(FlyoutEventKind.DragCancelled, was);
                    }
                    dragRow = -1;
                }
                return default;
            case WM.WM_MOUSEWHEEL:
            {
                int delta = (short)(((nuint)wParam >> 16) & 0xFFFF);
                var at = new POINT { x = X(lParam), y = Y(lParam) };
                var over = ScreenToClient(Hwnd, &at);
                // menus (§502): over a stepper the wheel steps it, as its buttons do; anywhere else it scrolls.
                if (over && IsOpen && StepperWheel(at.x, at.y, delta) is { } wheeled)
                {
                    return wheeled;
                }
                ScrollTo(scroll - delta / 120f * wheelStep);
                if (over)
                {
                    Moved(at.x, at.y);
                }
                return default;
            }
            case WM.WM_TIMER:
                if ((nuint)wParam == 2)
                {
                    KillTimer(Hwnd, 2);
                    return IsOpen && Root.TellsHover ? new FlyoutEvent(FlyoutEventKind.Hover, hot >= 0 && hot < rows.Count ? rows[hot].Item.Id : 0) : default;
                }
                if ((nuint)wParam == 3 && IsOpen)
                {
                    POINT cursor; GetCursorPos(&cursor);
                    if (host.Contains(cursor) || InsideSubmenus(cursor)) outsideSince = 0;
                    else if (outsideSince == 0) outsideSince = Environment.TickCount64;
                    else if (Environment.TickCount64-outsideSince >= 500) return Dismiss(false);
                    return default;
                }
                if ((nuint)wParam == TipTimer)
                {
                    TipDue();
                }
                if ((nuint)wParam == StepperTimer)
                {
                    return StepperRepeat();
                }
                return default;
            default:
                return default;
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }
        disposed = true;
        Close(restore: parent is null);
        submenu?.Dispose();
        submenu = null;
        tip?.Dispose();
        tip = null;
        host.Dispose();
        if (self.IsAllocated)
        {
            self.Free();
        }
    }

    private void Open(
        Gfx gfx,
        Look look,
        float scale,
        IReadOnlyList<FlyoutItem> items,
        FlyoutLayout layout,
        string? title,
        FlyoutPlacement placement,
        RECT? besideItem,
        int besideTop)
    {
        // §507: a menu's words are measured and drawn in its layout's Widgets in folders' font (Look.MenuTypeface).
        using var typeface = gfx.Wearing(look.MenuTypeface);
        // menus: a level swapped in place, or the menu shown again by a Refresh, stands where the menu
        // stood (§226), its foot kept where it stands nearer the bottom, never placed afresh, and its rows
        // sliding in (Flyout.Inline.cs, §268).
        var hold = holdAt;
        holdAt = null;
        var foot = holdFoot;
        holdFoot = null;
        var way = slideWay;
        slideWay = 0;
        if (swapping && placedAt is { } placed)
        {
            placement = placed;
        }
        // §356: an open submenu stays through a Refresh, shown again with its rows as they are now; any
        // other showing closes it.
        var keepSubmenu = refreshing && submenu is { IsOpen: true } && submenuFor >= 0 && submenuFor < rows.Count;
        var submenuKey = keepSubmenu ? rows[submenuFor].Key : null;
        if (!keepSubmenu)
        {
            CloseSubmenu();
        }
        // narrator: a screen reader finds the menu's items (MenuAccess; Accessibility\Uia.cs).
        spokenTitle = title is { Length: > 0 } ? title : Typed ?? SpokenName;
        if (parent is null)
        {
            Uia.Register(host.Hwnd, new MenuAccess(this));
        }
        // §356: the lit row and the tool under the pointer stay with their row, by its key; a menu shown
        // afresh (Show) starts with nothing lit.
        if (fresh)
        {
            SetHotTool(-1, -1);
            if (hot >= 0 && hot < rows.Count)
            {
                Light(rows[hot], false);
            }
        }
        var litKey = hot >= 0 && hot < rows.Count ? rows[hot].Key : null;
        (string Key, int Tool, ShapeVisual Plate)? toolWas = hotTool.Row >= 0 && hotTool.Row < rows.Count && hotTool.Tool >= 0 && hotTool.Tool < rows[hotTool.Row].Tools.Count
            ? (rows[hotTool.Row].Key, hotTool.Tool, rows[hotTool.Row].Tools[hotTool.Tool].Plate)
            : null;
        var segmentWas = keyedSegment;
        // A window whose content another showing emptied keeps nothing of this menu's.
        if (!host.IsOpen || frameWindow is null)
        {
            ReleaseKept();
        }
        RowsAgain();
        // menus: what a control's change shows the menu again with (Flyout.Controls.cs).
        Remember(items, layout, title, placement, besideItem is not null);
        this.gfx = gfx;
        this.look = look;
        this.scale = scale;
        work = placement.Work;
        theme = host.ThemeFor(look);
        bool animate = look.Animations;
        float Px(float dip) => Round(dip * scale);
        float textScale = look.TextScale > 0 ? look.TextScale : 1;
        bool strip = layout == FlyoutLayout.Strip;
        outsideSince = 0;
        bool grid = strip || layout == FlyoutLayout.Grid;
        mirrored = Mirror.On && !grid;
        // menus: the icon buttons, as one row on top, a configuration menu's type at its start on its
        // own rows (Flyout.Primary.cs, §323).
        string? typeTitle = null;
        if (layout == FlyoutLayout.List && parent is null)
        {
            typeTitle = IsLevel(items) ? null : Typed;
            items = WithPrimaryRow(items, typeTitle);
        }
        // menus: a search's results under its field (Flyout.Fields.cs).
        if (layout == FlyoutLayout.List)
        {
            items = WithFields(items);
            // Never a rule on top: a folder's faces where nothing stands over them (§225).
            items = WithoutLeadingRules(items);
        }
        // menus, §523: a colour's picker among them (Flyout.Mixer.cs).
        MixFound(items);
        bool flows = grid;

        // Shared measures.
        float away = Px(placement.GapDip);
        float margin = Px(placement.MarginDip);
        // tray: a grid that fits its icons keeps less room around them (FitsItsIcons).
        bool fitted = FitsItsIcons && grid && !strip;
        // menus: a menu's rows are Explorer's, measured off its own (Windows 11 26H2, at 150 %): 28 DIP
        // fills 32 apart, 5 DIP in from the menu's sides, 3 DIP above the first and under the last, 9
        // DIP a rule; the icon 16 DIP and the words 44 DIP in from the menu's edge, everything in the
        // middle of its fill; its row of buttons hard under the menu's rim (Flyout.Primary.cs). MenuMetrics holds them, §551 the
        // taskbar menu's preview drawn by them too.
        bool compact = layout == FlyoutLayout.List;
        // §521, §587, §633: a control's popup — a dropdown's list, a search's, an Add's, the font list, a colour's picker, a
        // button's level, the errors' list — hung from its control on the edit bar or a settings page: drawn as the settings
        // pages are (PanelMetrics.Popup), never as a right-click menu (Explorer's, MenuMetrics).
        var popup = placement.Control is not null && parent is null && besideItem is null && compact;
        settingsStyle = popup;
        var measures = popup ? PanelMetrics.Popup : RowMetrics.Menu;
        float pad = fitted ? Px(FittedPadDip) : grid ? Px(8) : compact ? Px(measures.Pad) : Px(2);
        float padAbove = compact && items.Count > 0 && items[0].Primary ? Px(1) : pad;
        float gap = Px(measures.Gap);
        float inset = compact ? Px(measures.Inset) : Px(4);
        float padLeft = Px(measures.PadLeft);
        float padRight = Px(measures.PadRight);
        float column = Px(measures.Column);
        float after = Px(24);
        float padTop = Px(4);
        float padBottom = Px(7);
        float glyphBox = Px(MenuMetrics.Glyph);
        float iconSize = GroupContent || strip ? StripIconSize : grid ? Px(32) : Px(16);
        float glyphPx = GroupContent || strip ? StripIconSize : (grid ? 32 : 16) * scale;
        // A group's icon without a title: its square, the one set (the tray's overflow) or the icon
        // and 10 DIP either side.
        float IconCell() => StripCell > 0 ? MathF.Max(StripCell, StripIconSize) : StripIconSize + Px(20);
        float smallGlyphPx = 12 * scale;
        float textPx = (grid ? 12 : 14) * textScale * scale;
        float smallPx = 12 * textScale * scale;
        float titlePx = 14 * textScale * scale;
        // §633: a heading a menu's in the type ramp's caption (12) in the quieter ink; a settings popup's a page's heading, Body Strong in the text's.
        float headerPx = measures.HeaderSize * textScale * scale;
        // §633: what a row is set to, at its end: a menu's in its words' size; a settings popup's the type ramp's caption (12), as a page's value.
        float valuePx = popup ? smallPx : textPx;
        var headerInk = popup ? theme.Text : theme.TextSecondary;
        // Rows are one width whatever they say: a menu Windows 11's, an island's
        // content the quick settings'. Tiles take what their columns need.
        float widest = grid
            ? Round(456 * scale * MathF.Max(1, textScale))
            : layout == FlyoutLayout.Panel ? Px(PanelWidthDip)
            : layout == FlyoutLayout.List && ListWidth is { } wide ? MathF.Round(wide)
            : Round(MenuWidthDip * scale * MathF.Max(1, textScale));
        float cellW = (strip || GroupContent) && !GroupShowTitles ? IconCell() : Round(96 * scale * MathF.Max(1, textScale));

        bool anyIcon = false;
        bool anyCheck = false;
        int tiles = 0;
        foreach (var item in items)
        {
            if (!item.Header && !item.Separator && !item.Primary && !IsTile(item))
            {
                anyIcon |= item.Icon is not null;
                anyCheck |= item.Checked is not null;
                tiles++;
            }
        }
        // The icons' column, then the words. A check mark stands at the row's far end, in the slot a
        // submenu's chevron has (the person, 2026-10-16: "move that tick symbol all the way to the
        // right"); every row of a menu with one keeps that slot, so the shortcuts stay one column.
        float iconLeft = inset + padLeft;
        float textLeft = inset + padLeft + (anyIcon ? column : 0);
        // §633: a settings popup marks its choice as WinUI's ComboBox does, a pill at its start: no tick's slot at the end.
        anyCheck &= !grid && !popup;
        // §253: a row dragged into order among rows with a submenu, without one itself (a folder's face
        // with no settings), keeps the chevron's slot empty, so the rows' bins stand in one column.
        var chevronGroups = items.Where(item => item.Reorder > 0 && item.Children is not null).Select(item => item.Reorder).ToHashSet();
        bool SlotBeside(FlyoutItem item) => item.Reorder != 0 && item.Children is null && chevronGroups.Contains(Math.Abs(item.Reorder));
        // §366: a row standing among rows dragged into order (a negative Reorder) has a grip's room where they have grips.
        var gripGroups = items.Where(item => item.Reorder > 0 && Leads(item)).Select(item => item.Reorder).ToHashSet();
        bool Leading(FlyoutItem item) => Leads(item) || (item.Reorder < 0 && gripGroups.Contains(-item.Reorder));
        float lead = inset + padLeft;
        float trail = padRight + inset;
        float textRoom = grid ? cellW - Px(8) : widest - textLeft - trail;

        // All text first: the widest decides the width, the tallest the rows. §356: drawn once for what it
        // says and shared (Drawn), each row's kept for the row that shows it.
        var measured = new List<Drawn>?[items.Count];
        List<Drawn> titleUses = [];
        List<Drawn> typeUses = [];
        List<Drawn> UsesOf(int i) => measured[i] ??= [];
        (CompositionDrawingSurface Surface, Vector2 Size) Text(List<Drawn> uses, string text, float px, Weight weight, DXGI_RGBA color, float limit, bool wrapped = false)
        {
            var label = TextOf(gfx, text, px, weight, color, limit, wrapped);
            uses.Add(label);
            return (label.Surface!, label.Size);
        }

        var labels = new (CompositionDrawingSurface Surface, Vector2 Size)?[items.Count];
        var hotLabels = new (CompositionDrawingSurface Surface, Vector2 Size)?[items.Count];
        var details = new (CompositionDrawingSurface Surface, Vector2 Size)?[items.Count];
        var keys = new (CompositionDrawingSurface Surface, Vector2 Size)?[items.Count];
        var hotKeys = new (CompositionDrawingSurface Surface, Vector2 Size)?[items.Count];
        // menus: what a submenu's row is set to, at its end (Flyout.Values.cs, §228): its words, cut
        // to the room left beside the row's own, or its swatch or glyph.
        var values = new (CompositionDrawingSurface Surface, Vector2 Size)?[items.Count];
        var hotValues = new (CompositionDrawingSurface Surface, Vector2 Size)?[items.Count];
        // A row's words, and what it is set to, where they were cut short: said whole in its tooltip (CutWords).
        var cuts = new string?[items.Count];
        (CompositionDrawingSurface Surface, Vector2 Size)? heading = null;
        (CompositionDrawingSurface Surface, Vector2 Size)? typeText = null;
        float width = fitted ? 0 : grid ? Px(120) : widest;
        float keysWidth = 0;
        // menus: the row of buttons' rim and edge room at either end, each button's least width, and all of it.
        float primaryEdge = Px(1) + Px(PrimaryEdgeDip);
        var primaryLeast = new List<float>();
        float primaryNeed = 0;
        float lineH = 0;
        float headerLineH = 0;
        // menus: the shortcuts first: they stand in one column at the rows' right, each from the
        // column's left as Explorer's do ("Enter", "Ctrl+Shift+C", "Alt+Enter" one under another),
        // the column as wide as the widest of them.
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (grid || item.Separator || item.Header || item.Primary || item.Shortcut is not { Length: > 0 } accelerator)
            {
                continue;
            }
            var shortcut = Text(UsesOf(i), accelerator, smallPx, Weight.Regular, item.Enabled ? theme.TextSecondary : theme.TextDisabled, widest / 2);
            keys[i] = shortcut;
            keysWidth = MathF.Max(keysWidth, MathF.Ceiling(shortcut.Size.X));
            if (theme.HighContrast && item.Enabled)
            {
                hotKeys[i] = Text(UsesOf(i), accelerator, smallPx, Weight.Regular, theme.TextOnHover, widest / 2);
            }
        }
        // menus: a widget's settings menu and its levels one width of their own, whatever they say
        // (Configures, Flyout.Width.cs, §230).
        if (compact && ListWidth is null && Configures && SettingsWidth(scale, textScale) is var settings && settings > widest)
        {
            textRoom += settings - widest;
            widest = settings;
        }
        // §521, §587: a control's popup — a dropdown's list, a colour's picker, a button's level, the errors' list — one width
        // whatever its rows say, as a ComboBox's list is its box's: a value's box's 240 DIP (a button's, Add a country's, as wide
        // as a value's box), §587 a search's its own fixed one. Never as wide as its rows, so never kept at the widest a showing
        // grew to (§517's fitted width, which it took to §587, grew as a search's finds came and kept its widest); a longer row
        // is cut, whole in its tooltip.
        if (popup)
        {
            // §613: a colour's picker (its swatches' grid, its mixer) its own fixed width, the one exception: 400 DIP.
            var picker = items.Any(item => item.Control is FlyoutMix || item.Icon is FlyoutTile { Cell: true, CellDip: <= 0, Color: not null });
            var across = Px(picker ? PanelMetrics.PickerWide : PanelMetrics.ControlWide);
            textRoom += across - widest;
            widest = across;
        }
        // menus, §517: an edit bar's menu as wide as its widest row needs, every level's (FitsContent), within the work area.
        else if (compact && ListWidth is null && FitsContent)
        {
            fitWidth = MathF.Max(fitWidth, FitWidth(gfx, items, scale, textPx, column, after, glyphBox, inset + padLeft, trail));
            var room = MathF.Max(widest, placement.Work.right - placement.Work.left - (2 * Px(12)));
            var fit = MathF.Min(MathF.Ceiling(fitWidth), room);
            if (fit > widest)
            {
                textRoom += fit - widest;
                widest = fit;
            }
        }
        // §627: a colour's picker, its swatches' column beside its mixer's (Flyout.Columns.cs), its own width wherever it
        // opens: a submenu's level too.
        if (compact && !popup && items.Any(StartsColumn) && Px(PanelMetrics.PickerWide) is var columned && columned > widest)
        {
            textRoom += columned - widest;
            widest = columned;
        }
        // menus: a button of the row is its icon alone, a square (Flyout.Primary.cs); the row's
        // width is the menu's least where it is wider than the rows would make it.
        for (int i = 0; i < items.Count; i++)
        {
            if (IsButton(items[i]))
            {
                primaryLeast.Add(Px(PrimaryButtonDip));
            }
        }
        primaryNeed = primaryLeast.Count == 0 ? 0 : 2 * primaryEdge + primaryLeast.Sum() + (primaryLeast.Count - 1) * Px(1);
        if (!grid && primaryNeed > widest)
        {
            textRoom += primaryNeed - widest;
            widest = primaryNeed;
        }
        // §627: a level in columns side by side (Flyout.Columns.cs): each row's column, its tiles' cells a menu row's square.
        var lanes = compact ? Lanes(items, inset, widest - (2 * inset), Px(TileCellDip), Px(MenuMetrics.Rule)) : null;
        float Narrower(int i) => lanes is null ? 0 : widest - (2 * inset) - lanes[i].W;
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            // menus: a tile says nothing beside its own (Flyout.Pickers.cs), nor a field, which fills its
            // row on its own level (Flyout.Fields.cs, §240, §246).
            if (item.Separator || item.Primary || IsTile(item) || FieldFillsRow(item) || IsMix(item))
            {
                // §415: a field says what it needs on a quieter line under its box, as long as the box's words run.
                if (!grid && FieldFillsRow(item) && item.Detail is { Length: > 0 } needs)
                {
                    details[i] = Text(UsesOf(i), needs, smallPx, Weight.Regular, item.Enabled ? theme.TextSecondary : theme.TextDisabled, MathF.Max(Px(24), widest - (2 * inset) - Narrower(i) - (2 * Px(MenuTextField.PadDip))), wrapped: true);
                }
                continue;
            }
            // §556: a font's choice, its family's heading, in its own font (on the menu's line, Gfx.Typeface).
            using var face = gfx.Wearing(item.Face ?? gfx.Typeface);
            if (item.Header)
            {
                var header = Text(UsesOf(i), item.Text, headerPx, Weight.Semibold, headerInk, widest - lead - trail);
                labels[i] = header;
                headerLineH = MathF.Max(headerLineH, header.Size.Y);
                continue;
            }
            // What sits at the right (the shortcut, a submenu's chevron) comes
            // off the text's room: the row is as wide as every other.
            float right = 0;
            if (keys[i] is not null)
            {
                right += after + keysWidth;
            }
            // The far slot (a chevron, or the check marks' column), and one more in from it for a
            // row both checked and with a submenu.
            if (!grid && (FarSlot(item, anyCheck) || SlotBeside(item)))
            {
                right += after + glyphBox;
            }
            // menus: a value of a row with nothing in the far slot (a search's result) at the row's
            // very end, a gap from its words (Flyout.Values.cs, §240).
            else if (!grid && item.Value is not null)
            {
                right += Px(ValueGapDip);
            }
            // §333, §336: a tickable row with a submenu (a layout) keeps the tick's slot ticked or not, its tools one column.
            if (!grid && !popup && item.Checked is not null && item.Children is not null)
            {
                right += after + glyphBox;
            }
            // menus: a toggle switch or a stepper at the row's end (Flyout.Controls.cs).
            if (!grid && item.Control is { } held && AtEnd(held))
            {
                var valueWidth = held is FlyoutControl.Stepper stepper ? StepValueWidth(gfx, stepper, textPx) : 0;
                right += after + ControlWidth(held, scale, valueWidth);
            }
            // menus: a grip at the row's start takes a column before its words (Flyout.Reorder.cs).
            if (!grid && Leading(item))
            {
                right += column;
            }
            var room = grid ? textRoom : MathF.Max(Px(24), textRoom - right - Narrower(i));
            // menus: the row's words and its value share the row (Flyout.Values.cs, §228, §315).
            if (!grid && item.Value is { } value)
            {
                // §396: a swatch and words both ("Inherited" and what it inherits): the swatch at the end, the words before it.
                if (value.Icon is not null)
                {
                    room = MathF.Max(Px(24), room - iconSize - (value.Text.Length > 0 ? Px(ValueGapDip) : 0));
                }
                if (value.Text.Length > 0)
                {
                    var words = MathF.Ceiling(gfx.Measure(item.Text, textPx, Weight.Regular).X);
                    // §556: a font's value in that font.
                    using var valueFace = gfx.Wearing(value.Face ?? gfx.Typeface);
                    var natural = MathF.Ceiling(gfx.Measure(value.Text, valuePx, Weight.Regular).X);
                    var (_, given) = Share(room, words, natural);
                    if (given > 0)
                    {
                        if (gfx.Cuts(value.Text, valuePx, Weight.Regular, given))
                        {
                            cuts[i] = value.Text;
                        }
                        values[i] = Text(UsesOf(i), value.Text, valuePx, Weight.Regular, item.Enabled && !value.Implied ? theme.TextSecondary : theme.TextDisabled, given);
                        room = MathF.Max(Px(24), room - MathF.Min(given, MathF.Ceiling(values[i]!.Value.Size.X)));
                        if (theme.HighContrast && item.Enabled)
                        {
                            hotValues[i] = Text(UsesOf(i), value.Text, valuePx, Weight.Regular, theme.TextOnHover, given);
                        }
                    }
                }
            }
            var color = item.Enabled ? theme.Text : theme.TextDisabled;
            // menus: a level's head shown in place says its name in the semibold, as a page's title (§268).
            var weight = item.Id == InlineBackId ? Weight.Semibold : Weight.Regular;
            var label = Text(UsesOf(i), item.Text, textPx, weight, color, room);
            labels[i] = label;
            if (gfx.Cuts(item.Text, textPx, weight, room))
            {
                cuts[i] = cuts[i] is { } valueCut ? $"{item.Text}\n{valueCut}" : item.Text;
            }
            lineH = MathF.Max(lineH, label.Size.Y);
            if (theme.HighContrast && item.Enabled)
            {
                hotLabels[i] = Text(UsesOf(i), item.Text, textPx, weight, theme.TextOnHover, room);
            }
            // edit: an add list's greyed row says why it can't be added, as a second line under
            // its name (Catalogue.WhyNot); its column flows round the taller row. pins: wrapped,
            // never cut — a missing pin's why and its target are read whole (BrokenRows).
            if (!grid && item.Detail is { Length: > 0 } detail)
            {
                details[i] = Text(UsesOf(i), detail, smallPx, Weight.Regular, item.Enabled ? theme.TextSecondary : theme.TextDisabled, room, wrapped: true);
            }
        }
        if (title is { Length: > 0 })
        {
            heading = Text(titleUses, title, titlePx, Weight.Semibold, theme.Text, widest - 2 * Px(16));
            if (grid)
            {
                width = MathF.Max(width, Px(16) + heading.Value.Size.X + Px(16));
            }
        }
        // edit: a configuration menu's type, in a flyout title's semibold, in the room its row's
        // buttons leave at its start (Flyout.Primary.cs, §323).
        if (typeTitle is { } typed)
        {
            var buttons = primaryLeast.Count == 0 ? trail : primaryNeed - primaryEdge + Px(PrimaryTitleGapDip);
            typeText = Text(typeUses, typed, titlePx, Weight.Semibold, theme.Text, MathF.Max(Px(24), MathF.Max(widest, primaryNeed) - lead - buttons));
        }
        float fillH = compact
            ? MathF.Max(Px(measures.Fill), MathF.Ceiling(lineH) + Px(9))
            : MathF.Max(Px(32), MathF.Ceiling(lineH) + padTop + padBottom);
        float headerH = MathF.Max(Px(measures.Header), MathF.Ceiling(headerLineH) + Px(14));
        float separatorH = compact ? Px(measures.Rule) : Px(3);
        float hairline = Px(1);
        // menus: the tiles, in lines across the menu, each line a row tall and as many to it as the
        // menu holds up to their own most (Flyout.Pickers.cs, §240).
        // §627: in columns, each run of tiles in its column's room.
        var tileSpots = lanes is null ? TileSpots(items, widest - 2 * inset, fillH + 2 * gap, scale) : TileSpots(items, i => lanes[i].W, Px(TileCellDip), scale);
        // menus: the row of buttons (Flyout.Primary.cs): square buttons of an icon each, from the row's
        // start, a hairline between two, the rule under the row the menu's full width with only a
        // little air under it.
        float primaryH = Px(PrimaryButtonDip);
        float SeparatorH(int i) => compact && i > 0 && items[i - 1].Primary ? hairline + Px(3) : separatorH;
        float cellH = (strip || GroupContent) && !GroupShowTitles ? IconCell() : Px(14) + iconSize + Px(8) + MathF.Ceiling(lineH) + Px(14);
        float titleH = heading is { } h ? MathF.Max(Px(40), MathF.Ceiling(h.Size.Y) + Px(16)) : 0;

        // How much of it fits: the room for the rows, the panel less its title and padding.
        float top0 = titleH + padAbove;
        float chrome = top0 + pad;
        // menus: a configuration menu one height on every level it opens in place, every widget's the same
        // (Flyout.Width.cs, §276, §288).
        float? oneHeight = compact && besideItem is null && parent is null && Configuration ? SettingsHeight(scale, textScale) : null;

        // A list of choices whose room is kept (Rows): where its own items end and the rest (a
        // separator, a settings row) begin, and the room left under its rows to make up its least.
        int tailFrom = items.Count;
        float listPad = 0;

        // A menu opens where it has room (FlyoutPlacement.Roomiest). Wanted: all of it, to the most a
        // panel may be; needed: eight rows.
        // menus: a level swapped in place keeps the place the menu took (§268).
        bool roomy = besideItem is null && parent is null && BesideWidget is null && !swapping && layout == FlyoutLayout.List;
        if (roomy && placement.Edge is Edge.Top or Edge.Bottom)
        {
            float content = oneHeight is { } one ? one - chrome
                : Heights(1);
            float wanted = chrome + MathF.Min(content, FlyoutPlacement.MostHeight(work.bottom - work.top, float.MaxValue, scale));
            float needed = chrome + MathF.Min(content, RoomiestRows * (fillH + 2 * gap));
            placement = placement.Roomiest(MathF.Ceiling(wanted), MathF.Ceiling(needed), scale);
        }
        float space = besideItem is not null
            ? work.bottom - work.top - 2 * margin
            : placement.Room(scale);
        // A flyout of a widget's content — a Panel's rows, a Grid's tiles: the keyboard's layouts,
        // the tray's hidden icons — is as tall as what it shows up to the most every flyout has
        // (FlyoutPlacement.MostHeight), and scrolls past it. A menu or a jump list (List) may take
        // all the room there is, as Windows' own menus do, and a strip is one row.
        if (besideItem is null && layout is FlyoutLayout.Panel or FlyoutLayout.Grid)
        {
            space = MathF.Min(space, placement.MostHeight(scale));
        }
        // §521: a control's list (a ComboBox's: rows to pick, no picker of tiles or fields) about ten rows tall at most, and scrolls past them.
        // §556: a filtered one (a font's) its filter over them.
        if (placement.Control is not null && parent is null && besideItem is null && (ComboList(layout, items) || FilteredList(layout, items)))
        {
            space = MathF.Min(space, chrome + ((ComboRows + (FilteredList(layout, items) ? 1 : 0)) * (fillH + 2 * gap)) + Px(4));
        }
        float comfort = besideItem is null ? MathF.Min(space, placement.MostHeight(scale)) : space;
        int columns = strip ? Math.Max(1, tiles)
            : Math.Clamp(tiles + (fitted ? Math.Max(0, SpareTiles) : 0), 1, Math.Max(1, GridColumns));
        if (strip) width = 2 * pad + columns * cellW;
        if (grid)
        {
            width = MathF.Max(width, 2 * pad + columns * cellW);
        }
        width = MathF.Ceiling(flows ? width : MathF.Max(widest, primaryNeed));
        // menus: the buttons together at the row's far end, in every menu (§350).
        var primarySpans = PrimarySpans(primaryLeast, primaryEdge, hairline, width);
        float gridLeft = strip || fitted ? pad : Round((width - columns * cellW) / 2);

        // The rows' heights, before anything is drawn: the height decides
        // where the panel goes.
        float Heights(int across)
        {
            float tall = 0;
            // §627: in columns, each from the top, the level as tall as its tallest.
            float tallest = 0;
            int step = 0;
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (i == tailFrom)
                {
                    tall += listPad;
                }
                if (lanes is not null && StartsColumn(item))
                {
                    (tallest, tall) = (MathF.Max(tallest, tall), 0);
                    continue;
                }
                if (item.Separator || item.Header)
                {
                    if (flows && step > 0)
                    {
                        step = 0;
                        tall += cellH;
                    }
                    tall += item.Separator ? SeparatorH(i) : labels[i] is not null ? headerH : 0;
                    continue;
                }
                if (flows)
                {
                    if (++step == across)
                    {
                        step = 0;
                        tall += cellH;
                    }
                }
                else if (item.Primary)
                {
                    // The row of them is one line, however many.
                    tall += i > 0 && items[i - 1].Primary ? 0 : primaryH;
                }
                else if (IsTile(item))
                {
                    // menus: a line of tiles is one cell tall (Flyout.Pickers.cs).
                    tall += tileSpots[i].EndsLine ? tileSpots[i].Cell : 0;
                }
                else
                {
                    tall += RowHeight(i) + 2 * gap;
                }
            }
            if (flows && step > 0)
            {
                tall += cellH;
            }
            if (tailFrom >= items.Count)
            {
                tall += listPad;
            }
            return MathF.Max(tall, tallest);
        }
        // §415: a field's box a row tall, what it needs to know a line under it, as WinUI's TextBox says its Description.
        // §523: a colour's picker: its spectrum, its sliders and its hex box their own heights (Flyout.Mixer.cs).
        float RowHeight(int i) => MixFill(items[i], scale) is > 0 and var mixed ? mixed
            : details[i] is { } second && FieldFillsRow(items[i]) ? fillH + Px(FieldDetailGapDip) + MathF.Ceiling(second.Size.Y)
            : details[i] is { } under && labels[i] is { } first ? MathF.Max(Px(48), MathF.Ceiling(first.Size.Y + under.Size.Y) + Px(12))
            : fillH;
        // The list is exactly as tall as it is set to, however few or many rows it lists, and past
        // that room it scrolls at once, whatever follows it a scroll away too.
        float most = float.MaxValue;
        if (Rows is { } fixedRows && !flows)
        {
            tailFrom = Math.Clamp(items.Count - fixedRows.Tail, 0, items.Count);
            float listed = 0;
            float trailing = 0;
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var down = item.Separator ? SeparatorH(i) : item.Header ? (labels[i] is not null ? headerH : 0) : RowHeight(i) + 2 * gap;
                if (i >= tailFrom)
                {
                    trailing += down;
                }
                else
                {
                    listed += down;
                }
            }
            var room = Round(MathF.Max(1, fixedRows.Dip) * scale * textScale);
            listPad = MathF.Max(0, room - listed);
            if (listed > room)
            {
                // As tall as its rows and what follows them, and no taller: the rest of the list,
                // and what follows it, a scroll away.
                most = room + trailing;
            }
        }
        float y = Heights(columns);
        if (fitted && (SpareTiles > 0 || tiles == 0))
        {
            // tray: the lines its icons and the empty squares after them take, one at least.
            y = MathF.Max(1, MathF.Ceiling((tiles + Math.Max(0, SpareTiles)) / (float)columns)) * cellH;
        }
        contentHeight = y;
        // edit: beside the widget, on the first of its sides with the room for all of it (MenuSides); §357:
        // again where the widget moved under it (Follow).
        if (BesideWidget is { } widget && besideItem is null && parent is null && (!swapping || following))
        {
            placement = MenuSides.Place(widget, work, new Vector2(width, oneHeight ?? chrome + MathF.Min(contentHeight, most)), scale);
            space = placement.Room(scale);
        }
        float least = MathF.Min(contentHeight, flows ? cellH : fillH + 2 * gap);
        // menus: a level's head, and a search's or an icon picker's field, stand still at its top while the
        // rest scroll under them (Flyout.Inline.cs, §249, §271).
        int pinThrough = PinnedThrough(items);
        // menus: and the rows that say so stand still at its foot, the rest scrolling over them (§276).
        int footFrom = compact ? FootFrom(items) : items.Count;
        // How tall the rows from one to another are, one under another.
        float RowsHeight(int from, int to)
        {
            float tall = 0;
            for (int i = from; i < to; i++)
            {
                tall += items[i].Separator ? SeparatorH(i) : items[i].Header ? (labels[i] is not null ? headerH : 0) : RowHeight(i) + 2 * gap;
            }
            return tall;
        }
        footH = RowsHeight(footFrom, items.Count);
        // menus: a configuration menu its one height, its rows at its top and its foot at its foot (§288);
        // any other as tall as its rows, to the room there is; a level a field searches or filters, what
        // stands over its rows and room for ListFilterFrom rows, whatever it finds (Flyout.Inline.cs, §271, §286).
        float wants = oneHeight is { } fixedHeight ? fixedHeight - chrome
            : compact && pinThrough >= 0 && items[pinThrough].Control is FlyoutField ? RowsHeight(0, pinThrough + 1) + ListFilterFrom * (fillH + 2 * gap)
            : MathF.Min(contentHeight, most);
        float shownHeight = MathF.Min(wants, Round(MathF.Max(space - chrome, least)));
        float height = chrome + shownHeight;
        // Off a side bar it is the width that has to fit beside the anchor (FlyoutPlacement.Roomiest).
        if (roomy && placement.Edge is Edge.Left or Edge.Right)
        {
            placement = placement.Roomiest(width, width, scale);
        }
        // menus: where the menu stands, which its levels keep (§268); the foot kept, the corner from it.
        if (parent is null && besideItem is null && (!swapping || following))
        {
            placedAt = placement;
        }
        if (hold is { } corner && foot is { } footAt)
        {
            hold = corner with { y = footAt - (int)MathF.Ceiling(height) };
        }
        scroll = 0;
        wheelStep = Px(48);
        nudge = gap;

        // §633: a settings popup's rim the settings flyout's (the theme's), not Explorer's light one.
        host.MenuRim = compact && !popup;
        // §356: a showing of the open menu again keeps the window's content — the frame round the rows and
        // every row that draws the same —; a first showing starts from nothing.
        var keep = host.IsOpen && frameWindow is not null;
        if (besideItem is { } beside)
        {
            // A mirrored menu's submenus open to its left, as its chevrons point (Mirror.cs).
            host.ShowBeside(gfx, look, scale, new Vector2(width, height), beside, besideTop, pad + gap, work, leftward: mirrored, at: hold, keep: keep);
        }
        else
        {
            host.Show(gfx, look, scale, new Vector2(width, height), placement, hold, keep: keep);
            if (host.Mode == FlyoutMode.Hover) SetTimer(Hwnd, 3, 200, null);
        }
        var panel = host.Panel;
        viewport = new Area(panel.X, panel.Y + top0, width, shownHeight);
        // §379: the panel's padding over the rows, where no title or field stands there, and under them.
        padTaken = (titleH == 0 ? top0 : 0, pad);

        var built = new List<Row>();
        try
        {
            var c = host.Compositor;
            Frame(c, animate);
            var rowsVisual = frameRows!;
            var pinnedVisual = framePinned!;
            var divider = frameDivider!;
            var fades = animate ? frameFades : null;
            content = rowsVisual;
            foreach (var element in kept.Values)
            {
                element.Seen = false;
            }
            // What every row is drawn from past itself (§356): a row kept where this and it are the same.
            var menuSig = new MenuSig(layout, scale, textScale, theme, mirrored, width, widest, textRoom, keysWidth, anyIcon, anyCheck, fillH, headerH, cellW, cellH, gridLeft, GroupContent || strip, GroupShowTitles, iconSize, animate, gfx.Typeface, popup);
            // An element's key: the level it is of, its kind and id, and which of that id it is.
            var level = parent is null ? string.Join("/", levels.Select(each => each.Holder.Id)) : "";
            var prefix = level.Length > 0 ? level + ":" : "";
            drawingLevel = level;
            var counted = new Dictionary<(char Kind, uint Id), int>();
            string KeyOf(char kind, uint id)
            {
                var n = counted.GetValueOrDefault((kind, id)) + 1;
                counted[(kind, id)] = n;
                return $"{prefix}{kind}{id}.{n}";
            }
            CompositionBrush Glyph(string code, float px, float box, bool bright, bool enabled, bool quiet = false)
            {
                var color = bright ? theme.TextOnHover : !enabled ? theme.TextDisabled : quiet ? theme.TextSecondary : theme.Text;
                var entry = BrushOf(new DrawnKey('g', code, px, Weight.Regular, color.r, color.g, color.b, color.a, box), () => gfx.Glyph(code, px, color, box))!;
                Uses(entry);
                return entry.Brush!;
            }
            CompositionBrush? ExplorerPicture(string code, float box)
            {
                var tone = theme.Dark ? Karpo.Core.ExplorerIcons.Tone.Dark : Karpo.Core.ExplorerIcons.Tone.Light;
                var entry = BrushOf(
                    new DrawnKey('e', code, box, Weight.Regular, theme.Dark ? 1 : 0, 0, 0, 0, 0),
                    () => Karpo.Core.ExplorerIcons.For(code, tone) is { } svg ? gfx.Svg(svg, box) : null);
                Uses(entry);
                return entry?.Brush;
            }

            // The rows, in content coordinates: x from the panel's left edge, y from the top of the
            // scrolling part; each element's own in its container, as it stood when it was drawn.
            ContainerVisual into = rowsVisual;

            // tray: the row being placed, whose visuals move as one when it makes room (DropGap).
            Row? building = null;
            SpriteVisual Place(CompositionBrush brush, Vector2 size, float x, float at)
            {
                var sprite = Own(c.CreateSpriteVisual());
                sprite.Brush = brush;
                sprite.Size = size;
                sprite.Offset = new Vector3(x, at, 0);
                into.Children.InsertAtTop(sprite);
                building?.Parts.Add((sprite, sprite.Offset));
                return sprite;
            }
            // §633: a rounded plate of one colour (a settings popup's choice's fill and pill), moving with its row.
            void Plaque(Area at, float radius, Windows.UI.Color color)
            {
                var visual = Own(c.CreateShapeVisual());
                visual.Size = new Vector2(at.W, at.H);
                visual.Offset = new Vector3(at.X, at.Y, 0);
                var shape = Own(c.CreateRoundedRectangleGeometry());
                shape.Size = visual.Size;
                shape.CornerRadius = new Vector2(radius);
                var sprite = Own(c.CreateSpriteShape(shape));
                sprite.FillBrush = Own(c.CreateColorBrush(color));
                visual.Shapes.Add(sprite);
                into.Children.InsertAtTop(visual);
                building?.Parts.Add((visual, visual.Offset));
            }
            SpriteVisual Rule(Vector2 size, float x, float at)
            {
                var rule = Own(c.CreateSpriteVisual());
                rule.Brush = divider;
                rule.Size = size;
                rule.Offset = new Vector3(x, at, 0);
                into.Children.InsertAtTop(rule);
                return rule;
            }

            y = 0;
            // §627: the tallest column so far (Flyout.Columns.cs).
            float tallest = 0;
            int step = 0;
            int primaryAt = 0;
            typeWords = default;
            // menus: what stands still at a level's top, over its rows (Flyout.Inline.cs, §249, §271): where
            // it stood unscrolled, as the rows are laid out again.
            foreach (var (still, home) in pinned)
            {
                still.Bounds = still.Bounds with { Y = home };
            }
            pinned.Clear();
            pinnedH = 0;
            var anyPinned = pinThrough >= 0 || footFrom < items.Count;
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var holder = anyPinned && (i <= pinThrough || i >= footFrom) ? pinnedVisual : rowsVisual;
                into = holder;
                building = null;
                if (i == pinThrough + 1)
                {
                    pinnedH = y;
                }
                if (i == footFrom)
                {
                    // At the foot of the rows' room, unscrolled (§276).
                    y = shownHeight - footH;
                }
                if (i == tailFrom)
                {
                    // The room the list keeps, under its rows, before what follows it.
                    y += listPad;
                }
                // §627: a row's column (Flyout.Columns.cs); the menu's width in a level of one.
                var lane = lanes?[i] ?? new Lane(0, inset, width - (2 * inset), 1);
                if (lanes is not null && StartsColumn(item))
                {
                    // The next column from the top, a hairline down the middle of the rule's room before it, the level's height.
                    var down = Round(lane.X - (Px(MenuMetrics.Rule) / 2) - (hairline / 2));
                    if (!BeginElement(c, KeyOf('s', item.Id), new ElementSig(menuSig, null, down, 0, hairline, contentHeight, "column"), holder, 0, out var rules, out var none))
                    {
                        into = rules.Visual;
                        Rule(new Vector2(hairline, contentHeight), down, 0);
                        EndElement(rules, width);
                    }
                    none?.Dispose();
                    tallest = MathF.Max(tallest, y);
                    y = 0;
                    continue;
                }
                if (item.Separator || item.Header)
                {
                    if (flows && step > 0)
                    {
                        step = 0;
                        y += cellH;
                    }
                    if (item.Separator)
                    {
                        // Inset like the fills, with air on either side; under the row of buttons the
                        // menu's full width, hard under it, as Explorer rules its row off.
                        var under = SeparatorH(i);
                        var across = compact && i > 0 && items[i - 1].Primary;
                        if (!BeginElement(c, KeyOf('s', item.Id), new ElementSig(menuSig, null, lane.X, 0, lane.W, under, across), holder, y, out var rules, out var none))
                        {
                            into = rules.Visual;
                            Rule(new Vector2(across ? width : lane.W, hairline), across ? 0 : lane.X, across ? y : y + Round((under - hairline) / 2));
                            EndElement(rules, width);
                        }
                        none?.Dispose();
                        y += under;
                        // §379: a level's head takes the pointer down to the rows under its rule, the rule too.
                        if (i > 0 && items[i - 1].Id == InlineBackId && built.Count > 0 && built[^1].Item.Id == InlineBackId)
                        {
                            built[^1].Reach.Bottom += under;
                        }
                    }
                    else if (labels[i] is { } header)
                    {
                        // A little lower than centred: a header belongs to what follows it.
                        if (!BeginElement(c, KeyOf('h', item.Id), new ElementSig(menuSig, item, 0, 0, 0, 0), holder, y, out var heads, out var none))
                        {
                            into = heads.Visual;
                            measured[i]?.ForEach(Uses);
                            Place(Own(c.CreateSurfaceBrush(header.Surface)), header.Size, lead, y + Round((headerH - header.Size.Y) / 2) + Px(2));
                            EndElement(heads, width);
                        }
                        none?.Dispose();
                        y += headerH;
                    }
                    continue;
                }

                Area fill;
                Area button = default;
                // §379: how far past its fill the row takes the pointer (Row.Reach): a list's row and an icon button.
                (float Left, float Top, float Right, float Bottom) reach = default;
                float rowH;
                // menus: the hairline before a button of the row, but its first (Flyout.Primary.cs).
                var ruleBefore = false;
                if (grid)
                {
                    var cell = new Area(gridLeft + step * cellW, y, cellW, cellH);
                    fill = new Area(cell.X + Px(2), cell.Y + Px(2), cell.W - Px(4), cell.H - Px(4));
                    rowH = cellH;
                }
                else if (item.Primary && typeText is { } typeAt && item.Id == TypeTitleId)
                {
                    // edit: the menu's type at the row's start, where a flyout's title stands, upright in
                    // its middle; nothing to light or pick (Flyout.Primary.cs, §323).
                    fill = new Area(lead, y, typeAt.Size.X, primaryH);
                    rowH = i + 1 < items.Count && items[i + 1].Primary ? 0 : primaryH;
                }
                else if (item.Primary)
                {
                    // menus: side by side at the row's far end, each its span (PrimarySpans), a hairline
                    // between two; its plate inset in it, lit under the pointer as Explorer's is.
                    var (spanX, spanW) = primarySpans[primaryAt];
                    button = new Area(spanX, y, spanW, primaryH);
                    ruleBefore = primaryAt > 0;
                    primaryAt++;
                    fill = new Area(
                        spanX + Px(PrimaryPlateDip),
                        y + Px(PrimaryPlateDip),
                        spanW - 2 * Px(PrimaryPlateDip),
                        primaryH - 2 * Px(PrimaryPlateDip));
                    reach = (Px(PrimaryPlateDip), Px(PrimaryPlateDip), Px(PrimaryPlateDip), Px(PrimaryPlateDip));
                    rowH = i + 1 < items.Count && items[i + 1].Primary ? 0 : primaryH;
                }
                else if (IsTile(item))
                {
                    // menus: a tile, in its line across the menu (Flyout.Pickers.cs).
                    fill = TileFill(tileSpots[i], lane.X, lane.W, y, scale);
                    rowH = tileSpots[i].EndsLine ? tileSpots[i].Cell : 0;
                }
                else
                {
                    float tall = RowHeight(i);
                    fill = new Area(lane.X, y + gap, lane.W, tall);
                    rowH = tall + 2 * gap;
                    // §627: to the menu's sides from its outer columns alone, never over the next column.
                    reach = (lane.First ? fill.X : 0, gap, lane.Last ? width - fill.X - fill.W : 0, gap);
                }

                // §356: the row kept where it draws the same, or drawn again, alone, its plate carried over.
                var fieldState = item.Control is FlyoutField shownField ? (fieldStates.GetValueOrDefault(item.Id), shownField.Finds) : default;
                var extra = (button.X, button.W, ruleBefore, IsTile(item) ? tileSpots[i] : default, SlotBeside(item), fieldState.Item1, fieldState.Item2);
                var key = KeyOf(item.Primary ? 'p' : 'r', item.Id);
                Row row;
                if (BeginElement(c, key, new ElementSig(menuSig, Drawable(item), fill.X, 0, fill.W, fill.H, extra), holder, fill.Y, out var element, out var plate))
                {
                    row = element.Row!;
                    Retool(row, item);
                    if (item.Id == TypeTitleId)
                    {
                        typeWords = element.Words;
                    }
                    // menus: a field's box shows what it holds now (Flyout.Fields.cs).
                    if (fieldState.Item1 is { } shownState)
                    {
                        RedrawField(shownState);
                    }
                }
                else
                {
                    into = element.Visual;
                    measured[i]?.ForEach(Uses);
                    row = new Row { Item = item, Bounds = fill, Key = key };
                    element.Row = row;
                    building = row;
                    if (ruleBefore)
                    {
                        Rule(new Vector2(hairline, Px(PrimaryRuleDip)), button.X - hairline, y + Round((primaryH - Px(PrimaryRuleDip)) / 2));
                    }
                    // menus: a field's row lights no plate; its box does (Flyout.Fields.cs). A note none at all (§339).
                    if (item.Enabled && !item.Note && item.Control is not (FlyoutField or FlyoutMix))
                    {
                        // §356: the plate of the row it takes the place of, lit as it was: never faded in again.
                        var lit = plate ?? NewPlate(c);
                        plate = null;
                        lit.Visual.Size = new Vector2(fill.W, fill.H);
                        lit.Visual.Offset = new Vector3(fill.X, fill.Y, 0);
                        lit.Shape.Size = new Vector2(fill.W, fill.H);
                        lit.Shape.CornerRadius = new Vector2(Px(4), Px(4));
                        lit.Fill.FillBrush = hoverBrush;
                        lit.Visual.ImplicitAnimations = fades;
                        into.Children.InsertAtTop(lit.Visual);
                        element.Plate = lit;
                        row.Highlight = lit.Visual;
                        row.Parts.Add((lit.Visual, lit.Visual.Offset));
                        row.Fill = lit.Fill;
                    }
                    plate?.Dispose();
                    DrawRow();
                    EndElement(element, width);
                    if (item.Id == TypeTitleId)
                    {
                        typeWords = element.Words;
                    }
                }
                row.Reach = reach;
                row.Lane = lane.Index;
                row.Cut = cuts[i];
                if (holder == pinnedVisual)
                {
                    pinned.Add((row, row.Bounds.Y));
                }
                if (item.Enabled && !item.Note)
                {
                    built.Add(row);
                }
                if (flows)
                {
                    step++;
                    if (step == columns)
                    {
                        step = 0;
                        y += cellH;
                    }
                }
                else
                {
                    y += rowH;
                }

                // A row's parts, drawn into its element: its icon, words, value, tick, control and shortcut.
                void DrawRow()
                {
                    if (item.Primary && typeText is { } typeShown && item.Id == TypeTitleId)
                    {
                        typeUses.ForEach(Uses);
                        var words = Place(Own(c.CreateSurfaceBrush(typeShown.Surface)), typeShown.Size, lead, y + Round((primaryH - typeShown.Size.Y) / 2));
                        element.Words = new Area(words.Offset.X, words.Offset.Y, typeShown.Size.X, typeShown.Size.Y);
                        return;
                    }
                    // Where a glyph or a line of text sits: centred between the
                    // fill's uneven top and bottom padding, as WinUI's are.
                    // A menu's (compact) all in the middle, as Explorer's are.
                    float Middle(float size) => grid || compact || item.Primary || details[i] is not null
                        ? fill.Y + Round((fill.H - size) / 2)
                        : fill.Y + padTop + Round((fill.H - padTop - padBottom - size) / 2);
                    // A glyph or a picture, which stands in the middle of a menu's fill.
                    float Upright(float size) => compact ? fill.Y + Round((fill.H - size) / 2) : Middle(size);
                    // menus: a button's icon in its middle.
                    float ButtonIconTop() => button.Y + Round((button.H - iconSize) / 2);

                    // §633: a settings popup's choice marked as WinUI's ComboBox marks its selected item, as the pages mark
                    // what stands open: its plate in the subtle fill at rest and the 3 × 16 accent pill at its start — no tick
                    // at its end. In high contrast the pill alone: the subtle fill is the highlight there, under words not
                    // in the highlight's text.
                    if (popup && !grid && item.Checked == true && !IsTile(item))
                    {
                        if (!theme.HighContrast)
                        {
                            Plaque(fill, Px(PanelMetrics.Corner), theme.SubtleHover);
                        }
                        var pillTall = Px(PanelMetrics.PillTall);
                        var pill = new Area(fill.X, fill.Y + Round((fill.H - pillTall) / 2), Px(PanelMetrics.PillWide), pillTall);
                        Plaque(pill, pill.W / 2, item.Enabled ? theme.Accent : theme.AccentDisabled);
                        row.Check = pill;
                    }

                    // A row's columns, from its own fill: the menu's inset in a list; §627 its column's start in columns.
                    float rowLeft = lane.X - inset;
                    // menus: a row dragged into order has its grip at its start, its icon and its words a
                    // column further in (Flyout.Reorder.cs, §226).
                    float gripLead = !grid && Leading(item) ? column : 0;
                    if (gripLead > 0 && item.Control is FlyoutControl.Tools leading)
                    {
                        DrawGrips(c, value => Own(value), into, row, leading, rowLeft + iconLeft + Round((iconSize - Px(ToolDip)) / 2));
                    }
                    if (item.Icon is FlyoutIcon.Glyph glyph)
                    {
                        // menus: Explorer's own two-colour picture of the command, where it has one (ExplorerIcons).
                        // §633: a settings popup's glyphs the pages' own, in their ink: no Explorer pictures.
                        var explorer = compact && !popup && !theme.HighContrast ? ExplorerPicture(glyph.Code, iconSize) : null;
                        var brush = explorer ?? Glyph(glyph.Code, glyphPx, iconSize, false, item.Enabled);
                        var image = grid
                            ? Place(brush, new Vector2(iconSize), fill.X + Round((fill.W - iconSize) / 2), (strip || GroupContent) && !GroupShowTitles ? fill.Y + (fill.H-iconSize)/2 : fill.Y - Px(2) + Px(14))
                            : item.Primary ? Place(brush, new Vector2(iconSize), button.X + Round((button.W - iconSize) / 2), ButtonIconTop())
                            : Place(brush, new Vector2(iconSize), rowLeft + iconLeft + gripLead, Upright(iconSize));
                        row.Icon = new Area(image.Offset.X, image.Offset.Y, iconSize, iconSize);
                        row.Explorer = explorer is not null;
                        if (explorer is not null && !item.Enabled)
                        {
                            image.Opacity = 0.4f;
                        }
                        if (theme.HighContrast && item.Enabled)
                        {
                            row.Swaps.Add((image, brush, Glyph(glyph.Code, glyphPx, iconSize, true, true)));
                        }
                    }
                    else if (item.Icon is FlyoutIcon.Picture picture)
                    {
                        var brush = Own(c.CreateSurfaceBrush(Own(gfx.Icon(picture.Pixels, iconSize))));
                        var image = grid
                            ? Place(brush, new Vector2(iconSize), fill.X + Round((fill.W - iconSize) / 2), (strip || GroupContent) && !GroupShowTitles ? fill.Y + (fill.H-iconSize)/2 : fill.Y - Px(2) + Px(14))
                            : item.Primary ? Place(brush, new Vector2(iconSize), button.X + Round((button.W - iconSize) / 2), ButtonIconTop())
                            : Place(brush, new Vector2(iconSize), rowLeft + iconLeft + gripLead, Upright(iconSize));
                        row.Icon = new Area(image.Offset.X, image.Offset.Y, iconSize, iconSize);
                        if (!item.Enabled)
                        {
                            image.Opacity = 0.4f;
                        }
                    }
                    else if (item.Icon is FlyoutTile tile)
                    {
                        // menus: a swatch or an icon's tile, or a row's own swatch (Flyout.Pickers.cs).
                        DrawTile(c, into, row, tile, fill, new Vector2(rowLeft + iconLeft + gripLead, Upright(iconSize)), iconSize, item.Checked == true, item.Enabled);
                    }

                    if ((!GroupContent && !strip || GroupShowTitles) && labels[i] is { } label)
                    {
                        SpriteVisual text;
                        var brush = Own(c.CreateSurfaceBrush(label.Surface));
                        if (grid)
                        {
                            text = Place(brush, label.Size, fill.X + Round((fill.W - label.Size.X) / 2), fill.Y - Px(2) + Px(14) + iconSize + Px(8));
                        }
                        else if (details[i] is { } second)
                        {
                            float lineTop = Middle(label.Size.Y + second.Size.Y);
                            text = Place(brush, label.Size, rowLeft + textLeft + gripLead, lineTop);
                            Place(Own(c.CreateSurfaceBrush(second.Surface)), second.Size, rowLeft + textLeft + gripLead, lineTop + label.Size.Y);
                        }
                        else
                        {
                            text = Place(brush, label.Size, rowLeft + textLeft + gripLead, Middle(label.Size.Y));
                        }
                        row.Words = new Area(text.Offset.X, text.Offset.Y, label.Size.X, label.Size.Y);
                        if (hotLabels[i] is { } bright)
                        {
                            row.Swaps.Add((text, brush, Own(c.CreateSurfaceBrush(bright.Surface))));
                        }
                    }

                    float rightEdge = width - trail;
                    // The far slot's glyph box: a menu's middle 16 DIP in from the fill's end, as Explorer's
                    // chevron; elsewhere at the row's end. The next slot in, one glyph and the gap further.
                    float slotLeft = compact ? fill.X + fill.W - Px(16) - Round(glyphBox / 2) : rightEdge - glyphBox;
                    // menus: a value's right edge, in from the chevron's box (Flyout.Values.cs, §228).
                    // §453: a switch's as far in from the switch at the row's end, and from a swatch before it (an account's calendar's count, §452).
                    float valueRight = FarSlot(item, anyCheck) ? slotLeft - Px(ValueGapDip)
                        : !grid && item.Control is FlyoutControl.Toggle or FlyoutControl.Tools { Switch: not null } ? rightEdge - ControlWidth(item.Control, scale, 0) - Px(ValueGapDip)
                        : rightEdge;
                    if (!grid && item.Children is not null)
                    {
                        // A menu's is Explorer's: the thin chevron in the secondary colour.
                        var pointing = compact ? (mirrored ? ThinChevronBackGlyph : ThinChevronGlyph) : mirrored ? ChevronBackGlyph : ChevronGlyph;
                        var brush = Glyph(pointing, smallGlyphPx, glyphBox, false, item.Enabled, quiet: compact && !popup);
                        var chevron = Place(brush, new Vector2(glyphBox), slotLeft, Upright(glyphBox));
                        if (theme.HighContrast && item.Enabled)
                        {
                            row.Swaps.Add((chevron, brush, Glyph(pointing, smallGlyphPx, glyphBox, true, true)));
                        }
                        rightEdge -= glyphBox + after;
                        slotLeft -= glyphBox + after;
                    }
                    // §253: the chevron's slot kept empty, in line with its group's (SlotBeside).
                    else if (!grid && SlotBeside(item) && !FarSlot(item, anyCheck))
                    {
                        rightEdge -= glyphBox + after;
                    }
                    // The check mark, in the words' colour, in the far slot (just in from a chevron); a row
                    // without one keeps the slot where another row has one, the shortcuts one column.
                    if (!grid && !popup && item.Checked == true && !IsTile(item))
                    {
                        var check = Place(Glyph(CheckGlyph, smallGlyphPx, glyphBox, false, item.Enabled), new Vector2(glyphBox), slotLeft, Upright(glyphBox));
                        row.Check = new Area(check.Offset.X, check.Offset.Y, glyphBox, glyphBox);
                        if (theme.HighContrast && item.Enabled)
                        {
                            row.Swaps.Add((check, check.Brush, Glyph(CheckGlyph, smallGlyphPx, glyphBox, true, true)));
                        }
                    }
                    // §425: a tickable row with a level (a pin's kind in force) says what it is set to in from the tick's slot.
                    if (!grid && !popup && item.Checked is not null && item.Children is not null)
                    {
                        valueRight = slotLeft - Px(ValueGapDip);
                    }
                    // menus: what the row is set to, before its chevron (Flyout.Values.cs, §228).
                    if (!grid && item.Value is { } setTo)
                    {
                        // §396: words with a swatch after them (Inherited, and what it inherits): the swatch at the end.
                        var wordsRight = valueRight;
                        Area? swatchArea = null;
                        if (values[i] is not null && setTo.Icon is FlyoutTile after)
                        {
                            var icon = row.Icon;
                            DrawTile(c, into, row, after with { Cell = false }, fill, new Vector2(valueRight - iconSize, Upright(iconSize)), iconSize, false, item.Enabled);
                            (swatchArea, row.Icon) = (row.Icon, icon);
                            wordsRight = valueRight - iconSize - Px(ValueGapDip);
                        }
                        if (values[i] is { } said)
                        {
                            var brush = Own(c.CreateSurfaceBrush(said.Surface));
                            var words = Place(brush, said.Size, wordsRight - said.Size.X, Middle(said.Size.Y));
                            row.Value = swatchArea is { } swatch
                                ? new Area(words.Offset.X, MathF.Min(words.Offset.Y, swatch.Y), swatch.X + swatch.W - words.Offset.X, MathF.Max(said.Size.Y, swatch.H))
                                : new Area(words.Offset.X, words.Offset.Y, said.Size.X, said.Size.Y);
                            // §425: words with their picture before them (a pin's Target: the app, the folder, the site).
                            if (setTo.Icon is FlyoutIcon.Picture shown)
                            {
                                var image = Place(Own(c.CreateSurfaceBrush(Own(gfx.Icon(shown.Pixels, iconSize)))), new Vector2(iconSize), words.Offset.X - Px(ValueGapDip) - iconSize, Upright(iconSize));
                                image.Opacity = item.Enabled ? 1 : 0.4f;
                                row.Value = new Area(image.Offset.X, MathF.Min(words.Offset.Y, image.Offset.Y), words.Offset.X + said.Size.X - image.Offset.X, MathF.Max(said.Size.Y, iconSize));
                            }
                            if (hotValues[i] is { } brightValue)
                            {
                                row.Swaps.Add((words, brush, Own(c.CreateSurfaceBrush(brightValue.Surface))));
                            }
                        }
                        else if (setTo.Icon is FlyoutIcon.Glyph valueGlyph)
                        {
                            var brush = Glyph(valueGlyph.Code, glyphPx, iconSize, false, item.Enabled);
                            var image = Place(brush, new Vector2(iconSize), valueRight - iconSize, Upright(iconSize));
                            row.Value = new Area(image.Offset.X, image.Offset.Y, iconSize, iconSize);
                            if (theme.HighContrast && item.Enabled)
                            {
                                row.Swaps.Add((image, brush, Glyph(valueGlyph.Code, glyphPx, iconSize, true, true)));
                            }
                        }
                        else if (setTo.Icon is FlyoutTile swatch)
                        {
                            // A row's own swatch (Flyout.Pickers.cs), where its icon would stand otherwise.
                            var icon = row.Icon;
                            DrawTile(c, into, row, swatch with { Cell = false }, fill, new Vector2(valueRight - iconSize, Upright(iconSize)), iconSize, false, item.Enabled);
                            (row.Value, row.Icon) = (row.Icon, icon);
                        }
                    }
                    // A toggle, a radio button or a stepper stands at the row's end, as in a menu without ticks (§226).
                    if (anyCheck && (item.Children is null || item.Checked is not null) && !ClearsCheckSlot(item))
                    {
                        rightEdge -= glyphBox + after;
                    }
                    // menus: a text field's box across the row (Flyout.Fields.cs, §246).
                    if (!grid && item.Control is FlyoutField)
                    {
                        DrawField(c, into, row, textPx, details[i] is null ? fill.H : fillH);
                        // §415: what it needs under its box, in line with its words.
                        if (details[i] is { } needs)
                        {
                            Place(Own(c.CreateSurfaceBrush(needs.Surface)), needs.Size, fill.X + Px(MenuTextField.PadDip), fill.Y + fillH + Px(FieldDetailGapDip));
                        }
                    }
                    // menus, §523: a part of a colour's picker across the row (Flyout.Mixer.cs).
                    else if (!grid && item.Control is FlyoutMix mix)
                    {
                        DrawMix(c, into, row, mix);
                    }
                    // menus: a toggle switch or a stepper (Flyout.Controls.cs).
                    else if (!grid && item.Control is { } control && AtEnd(control))
                    {
                        rightEdge -= DrawControl(c, value => Own(value), into, row, control, rightEdge, textPx) + after;
                    }
                    if (keys[i] is { } shortcut)
                    {
                        // In the shortcuts' column, from its left, on the row's words' baseline (the same
                        // face, so the baseline is the same share of each line).
                        var brush = Own(c.CreateSurfaceBrush(shortcut.Surface));
                        var keysLeft = rightEdge - keysWidth;
                        var keysTop = labels[i] is { } words
                            ? Middle(words.Size.Y) + Round(BaselineShare * (words.Size.Y - shortcut.Size.Y))
                            : Middle(shortcut.Size.Y);
                        var keysText = Place(brush, shortcut.Size, keysLeft, keysTop);
                        row.Keys = new Area(keysLeft, keysTop, shortcut.Size.X, shortcut.Size.Y);
                        if (hotKeys[i] is { } brightKeys)
                        {
                            row.Swaps.Add((keysText, brush, Own(c.CreateSurfaceBrush(brightKeys.Surface))));
                        }
                    }
                }
            }

            y = MathF.Max(y, tallest);
            if (pinThrough == items.Count - 1)
            {
                pinnedH = y;
            }

            // What stands over the rows is no level's.
            drawingLevel = null;
            if (heading is { } named)
            {
                var sig = new ElementSig(menuSig, null, Px(16), Round((titleH - named.Size.Y) / 2), named.Size.X, named.Size.Y, title);
                if (!BeginElement(c, "title", sig, host.Content, 0, out var titled, out var none))
                {
                    into = titled.Visual;
                    titleUses.ForEach(Uses);
                    Place(Own(c.CreateSurfaceBrush(named.Surface)), named.Size, Px(16), Round((titleH - named.Size.Y) / 2));
                    EndElement(titled, width);
                }
                none?.Dispose();
            }

            making = null;

            var window = frameWindow!;
            window.StopAnimation("Offset");
            pinnedVisual.StopAnimation("Offset");
            window.Size = new Vector2(viewport.W, viewport.H);
            window.Offset = new Vector3(0, top0, 0);
            // menus: the rows scroll under what stands still at a level's top (Flyout.Inline.cs, §249, §271).
            frameClip!.TopInset = anyPinned ? pinnedH : 0;
            frameClip.BottomInset = anyPinned ? footH : 0;
            pinnedVisual.Offset = new Vector3(0, top0, 0);
            // menus: a level swapped in place slides in the way it goes, its head with it (§268).
            if (way != 0 && animate)
            {
                FlyoutHost.CloseAll(slideOwned);
                SlideIn(c, value => { slideOwned.Add(value); return value; }, window, top0, way);
                if (anyPinned)
                {
                    SlideIn(c, value => { slideOwned.Add(value); return value; }, pinnedVisual, top0, way);
                }
            }

            var scrolls = contentHeight > viewport.H + 0.5f;
            frameIndicator!.IsVisible = scrolls;
            indicator = scrolls ? frameIndicator : null;
            indicatorShape = scrolls ? frameIndicatorShape : null;
            // §356: what this showing did not show goes, in the same commit; a level's under this one waits.
            Sweep(level);
            PlaceContent();
        }
        catch
        {
            making = null;
            Close(restore: false);
            throw;
        }
        rows.AddRange(built);
        LitAgain(litKey, toolWas, segmentWas);
        if (keepSubmenu)
        {
            SubmenuAgain(submenuKey);
        }
        FieldsShown();
    }

    /// <summary>
    /// §356: the rows laid out again: nothing pressed, dragged or told; what is lit and under the pointer
    /// found again after (<see cref="LitAgain"/>).
    /// </summary>
    private void RowsAgain()
    {
        if (pressed >= 0 && pressed < rows.Count)
        {
            SetFill(pressed, down: false);
        }
        ReorderForget();
        rows.Clear();
        hot = -1;
        pressed = -1;
        rightPressed = -1;
        told = -1;
        hotTool = (-1, -1);
        keyedSegment = -1;
    }

    /// <summary>
    /// §356: the row lit before the showing, <paramref name="litKey"/>, lit again where it still is — its
    /// plate as it was, never faded in again —, and the tool under the pointer, <paramref name="toolWas"/>;
    /// the tooltip stays with either, and goes where neither is there.
    /// </summary>
    private void LitAgain(string? litKey, (string Key, int Tool, ShapeVisual Plate)? toolWas, int segmentWas)
    {
        if (litKey is not null && rows.FindIndex(row => row.Key == litKey) is var lit and >= 0)
        {
            hot = lit;
            keyedSegment = segmentWas;
            Light(rows[lit], true);
        }
        var toolAgain = false;
        if (toolWas is { } was && rows.FindIndex(row => row.Key == was.Key) is var at and >= 0 && was.Tool < rows[at].Tools.Count)
        {
            hotTool = (at, was.Tool);
            var (_, button, plate) = rows[at].Tools[was.Tool];
            if (button.Enabled && !button.Grip)
            {
                Try(() => plate.Opacity = 1f);
            }
            toolAgain = plate == was.Plate;
        }
        if (toolWas is { } gone && !toolAgain)
        {
            Try(() => gone.Plate.Opacity = 0f);
        }
        if (hot < 0 && hotTool.Row < 0)
        {
            HideTip();
        }
    }

    /// <summary>
    /// §356: the submenu open over row <paramref name="key"/> before a Refresh shown again with that row's
    /// rows as they are now, where it stood; closed where the row or its rows are gone.
    /// </summary>
    private void SubmenuAgain(string? key)
    {
        var at = key is null ? -1 : rows.FindIndex(row => row.Key == key);
        if (at >= 0 && rows[at].Item.Children is { } children && submenu is { IsOpen: true } open && gfx is { } g)
        {
            submenuFor = at;
            Light(rows[at], true);
            open.ShownAgainBeside(g, children, SubmenuBeside(at));
            return;
        }
        submenuFor = -1;
        submenu?.Close(restore: false);
        keysInSubmenu = false;
    }

    /// <summary>§356: this submenu shown again where it stands, beside <paramref name="item"/>, with <paramref name="children"/>: a Refresh of its menu.</summary>
    private void ShownAgainBeside(Gfx g, IReadOnlyList<FlyoutItem> children, RECT item)
    {
        var panel = host.PanelOnScreen;
        holdAt = new POINT(panel.left, panel.top);
        refreshing = true;
        try
        {
            Open(g, look, scale, children, FlyoutLayout.List, null, new FlyoutPlacement(default, work, default), item, item.top);
        }
        finally
        {
            refreshing = false;
        }
    }

    /// <summary>§356: a row kept takes the row as it is now: what it holds below (a submenu's rows, a tool's level) may be new.</summary>
    private static void Retool(Row row, FlyoutItem item)
    {
        row.Item = item;
        IReadOnlyList<FlyoutTool> tools = item.Control switch
        {
            FlyoutControl.Tools those => [.. those.Buttons.Where(tool => tool.Grip), .. those.Buttons.Where(tool => !tool.Grip)],
            FlyoutControl.Segments segments => segments.Buttons,
            _ => [],
        };
        for (var at = 0; at < row.Tools.Count && at < tools.Count; at++)
        {
            row.Tools[at] = row.Tools[at] with { Tool = tools[at] };
        }
    }

    /// <summary>§356: a row's lit plate, made out of sight.</summary>
    private Plate NewPlate(Compositor c)
    {
        objectsMade += 3;
        var visual = c.CreateShapeVisual();
        var shape = c.CreateRoundedRectangleGeometry();
        var fill = c.CreateSpriteShape(shape);
        visual.Shapes.Add(fill);
        visual.Opacity = 0;
        return new Plate { Visual = visual, Shape = shape, Fill = fill };
    }

    private FlyoutEvent Key(int key)
    {
        // The Windows key closes the whole menu, submenus and all, and leaves the
        // foreground to Start (or whatever the shortcut opens), as Windows' menus do.
        if (LightDismiss.ClosesOnKey(key) && !LightDismiss.RestoresFocus(key))
        {
            return Dismiss(restore: false);
        }
        // menus: a text or a search field has the keys (Flyout.Fields.cs).
        if (FieldKeyDown(key) is { } typed)
        {
            return typed;
        }
        // menus, §523: Escape while a colour is mixed puts the one before back (Flyout.Mixer.cs).
        if (key == VK.VK_ESCAPE && mixing is not null)
        {
            return MixUndo();
        }
        // menus: a row's control has its keys, and the menu stays (Flyout.Controls.cs).
        if (ControlKey(key) is { } acted)
        {
            return acted;
        }
        var (chosen, id, dismiss) = Press(key);
        // menus: a key no row, field or control of it took, of what the menu stands over: handed on, the menu gone (PassesKey).
        if (!chosen && !dismiss && PassesKey?.Invoke(key) == true)
        {
            Close(restore: false);
            return new FlyoutEvent(FlyoutEventKind.PassedKey, (uint)key);
        }
        // menus: a search's result is picked, and the menu stays (Flyout.Fields.cs).
        if (chosen && PickFieldResult(id))
        {
            return default;
        }
        // menus: a row that stays, picked from the keys: the menu stays, as for the pointer (§219, §392).
        if (chosen && rows.Find(row => row.Item.Id == id && row.Item.Stays) is { } stays)
        {
            return Picked(stays.Item);
        }
        if (chosen)
        {
            Close(restore: false);
            return new FlyoutEvent(FlyoutEventKind.Invoke, id);
        }
        return dismiss ? Dismiss(restore: true) : default;
    }

    /// <summary>A key, for this flyout or the submenu the keys are in.</summary>
    private (bool Chosen, uint Id, bool Dismiss) Press(int key)
    {
        if (submenu is { IsOpen: true } sub && keysInSubmenu)
        {
            // The deepest open submenu takes Left (Right in a mirrored menu) and Escape; this one closes it.
            if (!(sub.submenu is { IsOpen: true } && sub.keysInSubmenu) && (key == VK.VK_ESCAPE || key == (sub.mirrored ? VK.VK_RIGHT : VK.VK_LEFT)))
            {
                int item = submenuFor;
                CloseSubmenu();
                SetHot(item);
                return default;
            }
            return sub.Press(key);
        }
        bool shift = KeyState.Down(VK.VK_SHIFT);
        // In a mirrored menu Left goes on — into a submenu, along the row of buttons — and Right
        // back; a grid and the add list's columns move by where things stand, which is mirrored.
        bool onward = key == (mirrored ? VK.VK_LEFT : VK.VK_RIGHT);
        switch (key)
        {
            // menus: a level shown in place goes back by Escape and Backspace, as a submenu shuts (Flyout.Inline.cs, §268).
            case VK.VK_ESCAPE or VK.VK_BACK when OnInlineLevel && parent is null:
                BackInPlace();
                return default;
            case VK.VK_ESCAPE:
                return (false, 0, parent is null);
            case VK.VK_RIGHT or VK.VK_LEFT when hot >= 0 && rows[hot].Item.Primary:
                Focus(AlongPrimaries(hot, onward ? +1 : -1));
                return default;
            // menus: along a line of tiles (Flyout.Pickers.cs).
            case VK.VK_RIGHT or VK.VK_LEFT when hot >= 0 && IsTile(rows[hot].Item):
                Focus(AlongTiles(hot, onward ? +1 : -1));
                return default;
            case VK.VK_RIGHT or VK.VK_LEFT when onward && hot >= 0 && rows[hot].Item.Children is not null:
                OpenSubmenu(hot, keys: true);
                return default;
            // §268: and by Left (Right in a mirrored menu), as it would shut its submenu.
            case VK.VK_RIGHT or VK.VK_LEFT when !onward && OnInlineLevel && parent is null && !IsGrid:
                BackInPlace();
                return default;
            case VK.VK_RIGHT:
                if (rows.Count > 0 && IsGrid)
                {
                    Focus(Step(hot, +1, wrap: false));
                }
                return default;
            case VK.VK_LEFT:
                if (IsGrid)
                {
                    Focus(Step(hot, -1, wrap: false));
                }
                return default;
            case VK.VK_RETURN:
            case VK.VK_SPACE:
                if (hot < 0)
                {
                    return default;
                }
                // §268: on a level's head, Enter and Space go back.
                if (rows[hot].Item.Id == InlineBackId && BackInPlace())
                {
                    return default;
                }
                if (rows[hot].Item.Children is not null)
                {
                    OpenSubmenu(hot, keys: true);
                    return default;
                }
                return (true, rows[hot].Item.Id, false);
            case VK.VK_DOWN:
                Focus(Nearest(hot, down: true));
                return default;
            case VK.VK_UP:
                Focus(Nearest(hot, down: false));
                return default;
            case VK.VK_TAB:
                Focus(Step(hot, shift ? -1 : +1, wrap: true));
                return default;
            case VK.VK_HOME:
                Focus(rows.Count > 0 ? 0 : -1);
                return default;
            case VK.VK_END:
                Focus(rows.Count - 1);
                return default;
            default:
                // menus: a row's shortcut chooses it while the menu stands open (MenuChord.cs).
                return Shortcut(key, KeyState.Down(VK.VK_CONTROL), KeyState.Down(VK.VK_MENU)) is { } row
                    ? (true, rows[row].Item.Id, false)
                    : default;
        }
    }

    /// <summary>menus: the row whose shortcut (<see cref="FlyoutItem.Keys"/>) <paramref name="key"/> is, with Ctrl and Alt as they are; null for none.</summary>
    private int? Shortcut(int key, bool control, bool alt)
    {
        var at = rows.FindIndex(row => row.Item.Keys is { } keys && keys.Is(key, control, alt, KeyState.Down(VK.VK_SHIFT)) && row.Item.Children is null && row.Item.Control is null);
        return at >= 0 ? at : null;
    }

    // A grid's rows sit side by side; a list's never do, but for a menu's icon buttons.
    // §627: rows of another column beside them are no grid (Flyout.Columns.cs).
    private bool IsGrid => rows.Count > 1 && !rows[0].Item.Primary && rows.Exists(r => r.Bounds.Y == rows[0].Bounds.Y && r != rows[0] && r.Lane == rows[0].Lane);

    /// <summary>The next row in reading order, or the first when none is lit.</summary>
    private int Step(int from, int by, bool wrap)
    {
        if (rows.Count == 0)
        {
            return -1;
        }
        if (from < 0)
        {
            return by > 0 ? 0 : rows.Count - 1;
        }
        int next = from + by;
        return wrap ? (next + rows.Count) % rows.Count : Math.Clamp(next, 0, rows.Count - 1);
    }

    /// <summary>
    /// The row above or below: the nearest one on the next line, nearest across it. In a list
    /// that is simply the next row; in a grid it keeps the column, and so does the add list, whose
    /// columns' rows don't line up across (a row of the same column is taken before a nearer one of
    /// another).
    /// </summary>
    private int Nearest(int from, bool down)
    {
        if (rows.Count == 0)
        {
            return -1;
        }
        if (from < 0)
        {
            return down ? 0 : rows.Count - 1;
        }
        var here = rows[from].Bounds;
        int best = -1;
        float bestDy = float.MaxValue;
        float bestDx = float.MaxValue;
        for (int i = 0; i < rows.Count; i++)
        {
            var there = rows[i].Bounds;
            float dy = down ? there.Y - here.Y : here.Y - there.Y;
            // §627: in its own column (Flyout.Columns.cs).
            if (dy < 1 || rows[i].Lane != rows[from].Lane)
            {
                continue;
            }
            float dx = MathF.Abs(there.X + there.W / 2 - (here.X + here.W / 2));
            if (dy < bestDy - 0.5f || (MathF.Abs(dy - bestDy) <= 0.5f && dx < bestDx))
            {
                best = i;
                bestDy = dy;
                bestDx = dx;
            }
        }
        // §627: past its column's end, on to the next column or back to the one before.
        best = AcrossColumns(from, down, best);
        // menus: from a row into the icon buttons, onto the first of them, as Windows 11's menus go.
        if (best >= 0 && rows[best].Item.Primary && !rows[from].Item.Primary)
        {
            best = rows.FindIndex(row => row.Item.Primary);
        }
        // menus: onto a line of tiles, onto its first (Flyout.Pickers.cs).
        best = IntoTiles(from, best);
        return best >= 0 ? best : from;
    }

    private void Focus(int index)
    {
        if (index < 0 || index >= rows.Count)
        {
            return;
        }
        SetHot(index);
        InView(index);
        // menus: a field's row takes the keys into its field (Flyout.Fields.cs).
        FieldFocusFollows(index);
    }

    /// <summary>Row <paramref name="index"/> scrolled into view where it is out of it: the keys' focus on it, a test's click on it.</summary>
    private void InView(int index)
    {
        var bounds = rows[index].Bounds;
        // menus: what stands still at a level's top stands over its rows (§249, §271): a row under it is out of view.
        // §276: and at its foot, over which they scroll.
        var still = IsPinned(rows[index]);
        var over = still ? float.MinValue : pinnedH;
        var under = still ? 0 : footH;
        if (bounds.Y < scroll + over)
        {
            ScrollTo(bounds.Y - over - nudge);
        }
        else if (bounds.Y + bounds.H > scroll + viewport.H - under)
        {
            ScrollTo(bounds.Y + bounds.H - viewport.H + under + nudge);
        }
    }

    private void Moved(int x, int y)
    {
        if (!IsOpen)
        {
            return;
        }
        if (parent is not null)
        {
            // The pointer made it into the submenu: it has the keys.
            parent.keysInSubmenu = true;
        }
        int under = RowAt(x, y);
        if (under >= 0 || host.Panel.Contains(x, y))
        {
            keysInSubmenu = false;
        }
        // menus: the pointer only lights a row; an open submenu stays open wherever it goes (§246).
        SetHot(under);
        // menus: a row's tool under the pointer shows its plate and its tooltip (Flyout.Controls.cs);
        // one of segments the pointer goes to is lit rather than the one the keys lit (§253).
        var tool = under >= 0 ? ToolIndexAt(under, x, y) : -1;
        if (hotTool != (under, tool))
        {
            keyedSegment = -1;
        }
        SetHotTool(under, tool);
        if (pressed >= 0)
        {
            // Dragged off the pressed row, it looks released, and letting go
            // there chooses nothing.
            SetFill(pressed, down: under == pressed);
        }
    }

    /// <summary>tray: the drag is over; the pointer goes back to the flyout.</summary>
    private void EndDrag()
    {
        dragRow = -1;
        dragging = false;
        LetGo();
        if (GetCapture() == Hwnd)
        {
            _ = ReleaseCapture();
        }
    }

    /// <summary>
    /// tray: a row is pressed and may be dragged. Should another window come to the front meanwhile
    /// (its app did, when it heard the press as it happened, before TrayClicks): a menu closes when
    /// another window takes the foreground, and the overflow closed under the icon held in it, so the
    /// drag out onto the bar died where it began. Until the button comes up it stays open.
    /// </summary>
    private void Hold()
    {
        if (holding)
        {
            return;
        }
        holding = true;
        heldOpen = host.StaysOpen;
        host.StaysOpen = true;
    }

    /// <summary>
    /// tray: the held row is let go (<see cref="Hold"/>). Where another window took the foreground
    /// meanwhile, the flyout goes now, as it would have then; a drop onto the bar closes it anyway.
    /// </summary>
    private void LetGo()
    {
        if (!holding)
        {
            return;
        }
        holding = false;
        host.StaysOpen = heldOpen;
        if (IsOpen && !heldOpen && Mode == FlyoutMode.Menu && GetForegroundWindow() != Hwnd)
        {
            _ = PostMessageW(Hwnd, FlyoutHost.WM_KARPO_HOST_DISMISS, (WPARAM)(nuint)host.Generation, default);
        }
    }

    /// <summary>
    /// tray: whether <paramref name="screen"/> lies on the open panel itself — not on the room the
    /// window keeps around it for the shadow, which on the bar's side reaches down to the bar.
    /// </summary>
    public bool OnPanel(POINT screen) => host.Contains(screen);

    /// <summary>A point of this window, on screen.</summary>
    private POINT Screen(POINT at)
    {
        var point = at;
        _ = ClientToScreen(Hwnd, &point);
        return point;
    }

    /// <summary>
    /// tray: the pointer is over row <paramref name="row"/> (-1 for none): what to tell, and when it
    /// comes onto another row, the system's hover time to wait afresh for it to rest there
    /// (WM_MOUSEHOVER), as a bar waits on a tray icon.
    /// </summary>
    private FlyoutEvent Told(int row)
    {
        if (row != told && row >= 0)
        {
            var track = new TRACKMOUSEEVENT
            {
                cbSize = (uint)sizeof(TRACKMOUSEEVENT),
                dwFlags = TME.TME_HOVER | TME.TME_LEAVE,
                hwndTrack = Hwnd,
                dwHoverTime = HOVER_DEFAULT,
            };
            _ = TrackMouseEvent(&track);
        }
        told = row;
        return new FlyoutEvent(FlyoutEventKind.Pointer, row >= 0 && row < rows.Count ? rows[row].Item.Id : 0);
    }

    /// <summary>
    /// menus, for tests: each row that can be chosen, where its plate, icon, words and shortcut stand
    /// in px from the panel's top left corner (empty where it has none), and whether its icon is Explorer's.
    /// </summary>
    internal IReadOnlyList<MenuRowLook> Looks => [.. rows.Select(row =>
    {
        Area At(Area part) => part.W > 0 ? new Area(part.X + viewport.X - host.Panel.X, part.Y + viewport.Y - host.Panel.Y - scroll, part.W, part.H) : default;
        return new MenuRowLook(row.Item.Id, At(row.Bounds), At(row.Icon), At(row.Words), At(row.Keys), row.Explorer, At(row.Check), At(row.Value));
    })];

    /// <summary>
    /// tray: where the item <paramref name="id"/> stands on the screen, in px, as far as it shows:
    /// what an app asks to place its popup or its menu by its icon in the overflow. None where the
    /// flyout is closed or has no such item.
    /// </summary>
    public RECT? ItemOnScreen(uint id)
    {
        if (!IsOpen || rows.FirstOrDefault(row => row.Item.Id == id) is not { } found)
        {
            return null;
        }
        var top = MathF.Max(viewport.Y + found.Bounds.Y - scroll, viewport.Y);
        var bottom = MathF.Min(viewport.Y + found.Bounds.Y + found.Bounds.H - scroll, viewport.Y + viewport.H);
        if (bottom <= top)
        {
            return null;
        }
        var near = new POINT((int)MathF.Floor(viewport.X + found.Bounds.X), (int)MathF.Floor(top));
        var far = new POINT((int)MathF.Ceiling(viewport.X + found.Bounds.X + found.Bounds.W), (int)MathF.Ceiling(bottom));
        _ = ClientToScreen(Hwnd, &near);
        _ = ClientToScreen(Hwnd, &far);
        return new RECT { left = near.x, top = near.y, right = far.x, bottom = far.y };
    }

    /// <summary>
    /// tray: where a drop at <paramref name="screen"/> (screen px) would land among the items, as
    /// <see cref="DropAt"/> reads the point over them; the count itself when it is outside the panel.
    /// <paramref name="held"/> is the row dragged out of this flyout (-1 for an icon from the bar), and
    /// <paramref name="current"/> what the last move answered, which holds near its edge.
    /// </summary>
    public int DropIndex(POINT screen, int held = -1, int current = -1)
    {
        var at = screen;
        if (rows.Count == 0 || !ScreenToClient(Hwnd, &at) || !viewport.Contains(at.x, at.y))
        {
            return rows.Count;
        }
        return DropAt(Fills(), at.x - viewport.X, at.y - viewport.Y + scroll, IsGrid || rows.Count == 1, held, current);
    }

    /// <summary>The rows' fills, in content px, for the geometry below.</summary>
    private List<(float X, float Y, float W, float H)> Fills() =>
        [.. rows.Select(row => (row.Bounds.X, row.Bounds.Y, row.Bounds.W, row.Bounds.H))];

    /// <summary>
    /// tray: how far, in cells, the pointer goes past the line between two places before the drop
    /// moves on from the place it has, so a pointer resting on that line doesn't flick the gap
    /// between the two (as <see cref="TrayDrags.Hysteresis"/> holds it on the bar).
    /// </summary>
    internal const float DropHysteresis = 0.2f;

    /// <summary>
    /// tray: where a drop at (<paramref name="x"/>, <paramref name="y"/>), content px, lands among
    /// items with these <paramref name="fills"/>: an index before which it goes, the count for after
    /// the last. The nearest item decides, and which half of it the point is in — across a grid's
    /// line, down a list.
    /// <para>
    /// An icon taken from these items (<paramref name="held"/> its row) is a slot instead: the item
    /// the point is over is where it goes, as on the bar (<see cref="TrayDrags.SlotAt"/>), since the
    /// others close up behind it as it leaves; the answer is still an index among all of them, its
    /// own counted (<see cref="TrayDrags.InOverflow"/> takes it out again).
    /// </para>
    /// <para>
    /// <paramref name="current"/>, the last answer, holds until the point is
    /// <see cref="DropHysteresis"/> of a cell past the line it was given at.
    /// </para>
    /// </summary>
    internal static int DropAt(IReadOnlyList<(float X, float Y, float W, float H)> fills, float x, float y, bool grid, int held = -1, int current = -1)
    {
        var count = fills.Count;
        if (count == 0)
        {
            return 0;
        }
        // The nearest line first, then the nearest item on it: a point past a grid's last icon is on
        // that icon's line, not on the fuller line above it. Outside a fill, how far out it is.
        var near = 0;
        var (bestY, bestX) = (float.MaxValue, float.MaxValue);
        for (var index = 0; index < count; index++)
        {
            var (fx, fy, fw, fh) = fills[index];
            var dx = MathF.Max(0, MathF.Max(fx - x, x - (fx + fw)));
            var dy = MathF.Max(0, MathF.Max(fy - y, y - (fy + fh)));
            if (dy < bestY - 0.5f || (MathF.Abs(dy - bestY) <= 0.5f && dx < bestX))
            {
                (near, bestY, bestX) = (index, dy, dx);
            }
        }
        if (held >= 0 && held < count)
        {
            // A slot: the item the point is nearest, unless it is still near the one it had.
            var had = current < 0 ? -1 : current <= held ? current : current - 1;
            if (had >= 0 && had < count && had != near)
            {
                var (hx, hy, hw, hh) = fills[had];
                var slackX = DropHysteresis * hw;
                var slackY = DropHysteresis * hh;
                if (x >= hx - slackX && x < hx + hw + slackX && y >= hy - slackY && y < hy + hh + slackY)
                {
                    near = had;
                }
            }
            return near < held ? near : near + 1;
        }
        var (nx, ny, nw, nh) = fills[near];
        var along = grid ? x - (nx + (nw / 2f)) : y - (ny + (nh / 2f));
        var slack = DropHysteresis * (grid ? nw : nh);
        if (current == near && along < slack)
        {
            return near;
        }
        if (current == near + 1 && along >= -slack)
        {
            return near + 1;
        }
        return along >= 0 ? near + 1 : near;
    }

    /// <summary>
    /// tray: the items make room where a dragged icon would land, before item <paramref name="index"/>
    /// (<see cref="DropIndex"/>; the count for after the last, -1 for nowhere here): the ones after it
    /// slide a cell on, as Windows 11's overflow opens a gap, and an icon taken from here
    /// (<paramref name="held"/>) leaves its place closed up behind it. It is
    /// <paramref name="carried"/> under the pointer in a window of its own (Dock.Tray.cs, TrayCarry),
    /// so its own row is out of sight meanwhile, its place standing empty while the pointer is off
    /// the panel. -1 and -1 put everything back.
    /// </summary>
    public void DropGap(int index, int held = -1, bool carried = false)
    {
        if (!IsOpen || content is null || rows.Count == 0)
        {
            return;
        }
        var fills = Fills();
        var grid = IsGrid || rows.Count == 1;
        var places = GapPlaces(fills, index, held, grid);
        if (look.Animations && gapSlide is null)
        {
            var c = host.Compositor;
            var slide = Own(c.CreateVector3KeyFrameAnimation());
            slide.Target = "Offset";
            slide.InsertExpressionKeyFrame(1f, "this.FinalValue", Own(MotionEase.Of(c, MotionKind.Move)));
            slide.Duration = look.Motion.Time(MotionKind.Move);
            gapSlide = Own(c.CreateImplicitAnimationCollection());
            gapSlide["Offset"] = slide;
        }
        for (var row = 0; row < rows.Count; row++)
        {
            // §356: drawn again rather than kept, where it shows again.
            rows[row].Displaced = true;
            var bounds = rows[row].Bounds;
            var move = new Vector3(places[row].X - bounds.X, places[row].Y - bounds.Y, 0);
            var opacity = 1f;
            if (row == held)
            {
                // It goes with the pointer; here its place is empty.
                move = Vector3.Zero;
                opacity = carried ? 0f : 1f;
            }
            foreach (var (visual, home) in rows[row].Parts)
            {
                if (visual == rows[row].Highlight)
                {
                    // Out of sight while anything is dragged (SetHot(-1)), and its fade is its own.
                    visual.Offset = home + move;
                    continue;
                }
                visual.ImplicitAnimations = gapSlide;
                visual.Offset = home + move;
                visual.Opacity = opacity;
            }
        }
    }


    /// <summary>
    /// tray: where each item of <paramref name="fills"/> stands (its fill's top left, content px) while
    /// a dragged icon would land before item <paramref name="index"/>: the items in their order with
    /// a cell left empty there — the next cell of the grid's line, or of the list, past the last one
    /// where the gap is at the end. The icon taken from here (<paramref name="held"/>) is counted out,
    /// so the others close up behind it, and stays where it is; its own entry is its place. An index
    /// under 0 is no gap: everything in its place.
    /// </summary>
    internal static List<(float X, float Y)> GapPlaces(IReadOnlyList<(float X, float Y, float W, float H)> fills, int index, int held, bool grid)
    {
        var count = fills.Count;
        var places = fills.Select(fill => (fill.X, fill.Y)).ToList();
        if (index < 0 || count == 0)
        {
            return places;
        }
        // The cells in their order, and one more after the last.
        var cells = new List<(float X, float Y)>(places);
        var last = fills[count - 1];
        if (grid)
        {
            var line = fills.Count(fill => fill.Y == fills[0].Y);
            var stepX = count > 1 && fills[1].Y == fills[0].Y ? fills[1].X - fills[0].X : last.W;
            // A line's step down is its step across less the fills' width, plus their height.
            var stepY = count > line ? fills[line].Y - fills[0].Y : last.H + stepX - last.W;
            cells.Add(count % line != 0 ? (last.X + stepX, last.Y) : (fills[0].X, last.Y + stepY));
        }
        else
        {
            cells.Add((last.X, last.Y + (count > 1 ? fills[1].Y - fills[0].Y : last.H)));
        }
        var mine = held >= 0 && held < count;
        var gap = Math.Clamp(mine && index > held ? index - 1 : index, 0, mine ? count - 1 : count);
        var slot = 0;
        for (var item = 0; item < count; item++)
        {
            if (mine && item == held)
            {
                continue;
            }
            places[item] = cells[slot >= gap ? slot + 1 : slot];
            slot++;
        }
        return places;
    }

    /// <summary>
    /// The row the pointer at (<paramref name="x"/>, <paramref name="y"/>), window px, is on, -1 for none: the
    /// one question the lit row, the pressed one and the one a release picks are all asked by. §379: a row is
    /// its whole slot (Row.Reach), and the panel's padding over the first row and under the last is theirs —
    /// so every pixel between the rule over a run of rows and the rule under it is one of them.
    /// </summary>
    private int RowAt(int x, int y)
    {
        if (!viewport.Contains(x, y))
        {
            if (x < viewport.X || x >= viewport.X + viewport.W || y < viewport.Y - padTaken.Above || y >= viewport.Y + viewport.H + padTaken.Below || viewport.H < 1)
            {
                return -1;
            }
            y = (int)Math.Clamp(y, viewport.Y, viewport.Y + viewport.H - 1);
        }
        float cx = x - viewport.X;
        float cy = y - viewport.Y + scroll;
        // menus: over what stands still at a level's top or the menu's foot only it, never a row scrolled under it (§249, §271, §276).
        var under = y - viewport.Y < pinnedH || y - viewport.Y >= viewport.H - footH;
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i].Takes(cx, cy) && (!under || IsPinned(rows[i])))
            {
                return i;
            }
        }
        return -1;
    }

    private void SetHot(int index)
    {
        if (index == hot)
        {
            return;
        }
        // §622: the row the pointer or the keys rest on, told a moment later (HoverMs), none too, where the menu tells it.
        KillTimer(Hwnd, 2);
        if (Root.TellsHover) SetTimer(Hwnd, 2, HoverMs, null);
        int old = hot;
        hot = index;
        // menus, §523: the keys gone from a colour's picker keep the colour they mixed (Flyout.Mixer.cs).
        MixLeft(index);
        // menus: the keys leave a row of segments: the one they lit is not lit any more (§253).
        if (keyedSegment >= 0)
        {
            keyedSegment = -1;
            SetHotTool(-1, -1);
        }
        // narrator: the row the keys or the pointer are on is the menu's focus, and a screen reader says it.
        if (parent is null && index >= 0 && GetForegroundWindow() == Hwnd)
        {
            Uia.FocusMoved(Hwnd);
        }
        // The item whose submenu is open stays lit.
        if (old >= 0 && old < rows.Count && old != submenuFor)
        {
            Light(rows[old], false);
        }
        if (hot >= 0 && hot < rows.Count)
        {
            Light(rows[hot], true);
        }
        // menus: an icon button lit says its name in a tooltip, a moment later (Flyout.Primary.cs).
        TipFor(hot);
    }

    private void SetPressed(int index)
    {
        if (pressed >= 0 && pressed < rows.Count)
        {
            SetFill(pressed, down: false);
        }
        pressed = index;
        if (pressed >= 0 && pressed < rows.Count)
        {
            SetHot(pressed);
            SetFill(pressed, down: true);
        }
    }

    private void SetFill(int index, bool down)
    {
        if (index < 0 || index >= rows.Count || rows[index].Fill is not { } fill)
        {
            return;
        }
        var brush = down ? pressedBrush : hoverBrush;
        if (brush is not null)
        {
            Try(() => fill.FillBrush = brush);
        }
    }

    // High contrast swaps text and glyphs for the selection's own colour.
    private static void Light(Row row, bool on)
    {
        if (row.Highlight is { } highlight)
        {
            Try(() => highlight.Opacity = on ? 1 : 0);
        }
        foreach (var (sprite, plain, bright) in row.Swaps)
        {
            Try(() => sprite.Brush = on ? bright : plain);
        }
        // menus: a swatch's ring (Flyout.Pickers.cs).
        row.Lit?.Invoke(on);
    }

    private void ScrollTo(float value)
    {
        float most = MathF.Max(0, contentHeight - viewport.H);
        float next = MathF.Round(Math.Clamp(value, 0, most));
        if (next == scroll)
        {
            return;
        }
        scroll = next;
        CloseSubmenu();
        HideTip();
        PlaceContent();
    }

    /// <summary>Puts the rows and the scroll indicator where <see cref="scroll"/> says.</summary>
    private void PlaceContent()
    {
        if (content is { } inner)
        {
            Try(() => inner.Offset = new Vector3(0, -scroll, 0));
        }
        // menus: what stands still at a level's top stays where it is, over the rows (§249, §271).
        foreach (var (still, home) in pinned)
        {
            // §502: all its boxes with it (Row.Shift) — a stepper's buttons, its tools — not its bounds alone, which
            // left a stepper at the menu's foot pressed and turned where it stood before the menu scrolled.
            still.Shift(home + scroll - still.Bounds.Y);
        }
        if (indicator is { } bar && indicatorShape is { } shape && contentHeight > pinnedH + footH)
        {
            // Beside the rows that scroll, between what stands still over and under them.
            var panel = host.Panel;
            float seen = viewport.H - pinnedH - footH;
            float thickness = MathF.Max(2, MathF.Round(scale * 2));
            float length = MathF.Max(thickness * 4, MathF.Round(seen * seen / (contentHeight - pinnedH - footH)));
            float travel = seen - length;
            float most = MathF.Max(1, contentHeight - viewport.H);
            float at = viewport.Y + pinnedH - panel.Y + MathF.Round(travel * scroll / most);
            // At the panel's end: its left in a mirrored menu, as Windows puts a scroll bar there.
            // §633: a settings popup's a tight space in, as the page's (PanelMetrics.ScrollBar).
            float edge = MathF.Round(scale * (settingsStyle ? PanelMetrics.Tight : 2));
            float x = mirrored ? edge : viewport.W - thickness - edge;
            Try(() =>
            {
                bar.Size = new Vector2(thickness, length);
                bar.Offset = new Vector3(x, at, 0);
                shape.Size = new Vector2(thickness, length);
                shape.CornerRadius = new Vector2(thickness / 2, thickness / 2);
            });
        }
    }

    private void OpenSubmenu(int index, bool keys)
    {
        if (index < 0 || index >= rows.Count || rows[index].Item.Children is not { } children || gfx is null)
        {
            return;
        }
        // menus: a settings menu's level opens in place of its rows, in its own window (Flyout.Inline.cs, §268, §271).
        if (Configures)
        {
            OpenInPlace(index, keys);
            return;
        }
        if (submenuFor == index && submenu is { IsOpen: true } already)
        {
            keysInSubmenu |= keys;
            if (keys)
            {
                already.Focus(0);
            }
            return;
        }
        CloseSubmenu();
        submenu ??= new Flyout(this, gfx);
        var item = SubmenuBeside(index);
        submenu.Open(gfx, look, scale, children, FlyoutLayout.List, null, new FlyoutPlacement(default, work, default), item, item.top);
        // Its window may be new: the press on it is the menu's.
        Root.host.Keeps();
        submenuFor = index;
        keysInSubmenu = keys;
        Light(rows[index], true);
        if (keys)
        {
            submenu.Focus(0);
        }
    }

    /// <summary>
    /// menus: a click on row <paramref name="index"/>, which has a submenu: opened where it is shut (in
    /// place of another open one), shut where it is open, its row still lit and this menu staying (§219, §246).
    /// </summary>
    private void ParentClicked(int index)
    {
        if (submenuFor == index && submenu is { IsOpen: true })
        {
            CloseSubmenu();
            SetHot(index);
            Light(rows[index], true);
        }
        else
        {
            OpenSubmenu(index, keys: false);
        }
    }

    /// <summary>Tests only: the open level of the menu, deepest first, with row <paramref name="id"/>, and where the row is; null for none.</summary>
    private (Flyout Menu, int Index)? LevelWith(uint id)
    {
        List<Flyout> levels = [];
        for (var menu = this; menu is { IsOpen: true }; menu = menu.submenu)
        {
            levels.Insert(0, menu);
        }
        foreach (var menu in levels)
        {
            if (menu.rows.FindIndex(row => row.Item.Id == id) is var at and >= 0)
            {
                return (menu, at);
            }
        }
        return null;
    }

    /// <summary>The middle of row <paramref name="index"/>, as a mouse message's lParam (window px).</summary>
    private LPARAM MiddleOf(int index)
    {
        var box = rows[index].Bounds;
        var x = (int)MathF.Round(viewport.X + box.X + (box.W / 2));
        var y = (int)MathF.Round(viewport.Y + box.Y + (box.H / 2) - scroll);
        return (LPARAM)(nint)((y << 16) | (x & 0xFFFF));
    }

    /// <summary>Tests only: the pointer onto row <paramref name="id"/> of whichever open level has it.</summary>
    internal void PointTo(uint id)
    {
        if (LevelWith(id) is { } found)
        {
            _ = found.Menu.Message(WM.WM_MOUSEMOVE, 0, found.Menu.MiddleOf(found.Index));
        }
    }

    /// <summary>
    /// Tests only: a click on row <paramref name="id"/> of whichever open level has it, as the pointer
    /// makes one, the row scrolled into view first as a person scrolls to it (a long level: §532's Karpo page); a submenu's
    /// word to this menu taken off the queue and heard, as the dock would.
    /// </summary>
    internal FlyoutEvent Click(uint id)
    {
        if (LevelWith(id) is not var (menu, at))
        {
            return default;
        }
        menu.InView(at);
        var middle = menu.MiddleOf(at);
        _ = menu.Message(WM.WM_LBUTTONDOWN, 1, middle);
        var happened = menu.Message(WM.WM_LBUTTONUP, 0, middle);
        MSG posted;
        return menu != this && PeekMessageW(&posted, Hwnd, WM_KARPO_FLYOUT_CHOSEN, WM_KARPO_FLYOUT_ADJUST, PM.PM_REMOVE)
            ? Message(posted.message, posted.wParam, posted.lParam)
            : happened;
    }

    /// <summary>Tests only: how far each open level is scrolled, root first, in px.</summary>
    internal IReadOnlyList<float> Scrolls
    {
        get
        {
            List<float> scrolls = [];
            for (var menu = this; menu is { IsOpen: true }; menu = menu.submenu)
            {
                scrolls.Add(menu.scroll);
            }
            return scrolls;
        }
    }

    /// <summary>Tests only: this menu scrolled to <paramref name="value"/> px, as the wheel would.</summary>
    internal void ScrollForTest(float value) => ScrollTo(value);

    /// <summary>The item the keys are on, for tests: a row's id, or 0 where they are on none.</summary>
    internal uint KeysOn => hot >= 0 && hot < rows.Count ? rows[hot].Item.Id : 0;

    /// <summary>For tests: the panel on screen, and the open submenu's, or null.</summary>
    internal (RECT Panel, (HWND Window, RECT Panel)? Submenu) PanelsOnScreen =>
        (host.PanelOnScreen, submenu is { IsOpen: true } open ? (open.Hwnd, open.host.PanelOnScreen) : null);

    /// <summary>Row <paramref name="index"/> on the screen, which its submenu opens beside.</summary>
    private RECT SubmenuBeside(int index)
    {
        var fill = rows[index].Bounds;
        var panel = host.PanelOnScreen;
        var origin = host.Origin;
        return new RECT
        {
            left = panel.left,
            top = origin.y + (int)(viewport.Y + fill.Y - scroll),
            right = panel.right,
            bottom = origin.y + (int)(viewport.Y + fill.Y + fill.H - scroll),
        };
    }

    private void CloseSubmenu()
    {
        submenu?.Close(restore: false);
        int was = submenuFor;
        submenuFor = -1;
        keysInSubmenu = false;
        if (was >= 0 && was < rows.Count && was != hot)
        {
            Light(rows[was], false);
        }
    }

    // A menu's open submenus count as inside it for the hook.
    private bool InsideSubmenus(POINT screen)
    {
        if (AlsoInside?.Invoke(screen) == true) return true;
        for (var menu = submenu; menu is { IsOpen: true }; menu = menu.submenu)
        {
            if (menu.host.Contains(screen))
            {
                return true;
            }
        }
        return false;
    }

    private Flyout Root => parent?.Root ?? this;

    /// <summary>A choice: the root closes and says so; a submenu tells the root.</summary>
    private FlyoutEvent Chosen(uint id)
    {
        // menus: a search's result is picked, and the menu stays (Flyout.Fields.cs).
        if (PickFieldResult(id))
        {
            return default;
        }
        if (parent is null)
        {
            Close(restore: false);
            return new FlyoutEvent(FlyoutEventKind.Invoke, id);
        }
        var root = Root;
        _ = PostMessageW(root.Hwnd, WM_KARPO_FLYOUT_CHOSEN, (WPARAM)(nuint)root.host.Generation, (LPARAM)(nint)id);
        return default;
    }

    /// <summary>A click outside, seen by a submenu's window: the whole menu goes.</summary>
    private FlyoutEvent Outside()
    {
        if (parent is null)
        {
            return Dismiss(restore: true);
        }
        var root = Root;
        _ = PostMessageW(root.Hwnd, FlyoutHost.WM_KARPO_HOST_DISMISS, (WPARAM)(nuint)root.host.Generation, (LPARAM)(nint)1);
        return default;
    }

    private FlyoutEvent Dismiss(bool restore)
    {
        Close(restore);
        return new FlyoutEvent(FlyoutEventKind.Dismissed);
    }

    /// <summary>Ends the showing, submenus first; the host fades the window out.</summary>
    private void Close(bool restore)
    {
        KillTimer(Hwnd, 2); KillTimer(Hwnd, 3);
        // tray: a drag or a press going on goes with the flyout. It was kept before, so the rows were
        // gone and the drag was not: the next showing took a plain move over it for a drag of
        // whatever row now stood where the old one had (Dock: the drag is called off, FlyoutClosed).
        if (holding)
        {
            holding = false;
            host.StaysOpen = heldOpen;
        }
        dragRow = -1;
        dragging = false;
        ReorderForget();
        // menus, §523: a colour being mixed goes with the menu; whoever showed it keeps what it last said (Dock.MenuPickers.cs).
        (mixing, mixHeld) = (null, null);
        if (GetCapture() == Hwnd)
        {
            _ = ReleaseCapture();
        }
        CloseSubmenu();
        EndFields();
        ForgetRows();
        // menus: the next showing starts on the menu's own rows (§268).
        levels.Clear();
        // §356: what the menu kept goes with the showing, once the window no longer shows it.
        ReleaseKept();
        pinned.Clear();
        host.Close(restore);
        content = null;
        indicator = null;
        indicatorShape = null;
        gapSlide = null;
        hoverBrush = null;
        pressedBrush = null;
    }

    private void ForgetRows()
    {
        HideTip();
        hotTool = (-1, -1);
        keyedSegment = -1;
        ReorderForget();
        rows.Clear();
        hot = -1;
        pressed = -1;
        rightPressed = -1;
        told = -1;
    }

    private void RegisterSubmenuClass()
    {
        if (submenuClassRegistered)
        {
            return;
        }
        fixed (char* name = SubmenuClassName)
        {
            var info = new WNDCLASSEXW
            {
                cbSize = (uint)sizeof(WNDCLASSEXW),
                lpfnWndProc = &SubmenuProc,
                hInstance = instance,
                hCursor = LoadCursorW(HINSTANCE.NULL, IDC.IDC_ARROW),
                lpszClassName = name,
            };
            // Already registered (by an earlier flyout of this process) is as good.
            if (RegisterClassExW(&info) == 0 && GetLastError() != ERROR.ERROR_CLASS_ALREADY_EXISTS)
            {
                var hr = HRESULT_FROM_WIN32(GetLastError());
                throw new NativeException($"RegisterClassExW failed: 0x{hr.Value:X8}", hr.Value);
            }
        }
        submenuClassRegistered = true;
    }

    [UnmanagedCallersOnly]
    private static LRESULT SubmenuProc(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        if (msg == WM.WM_MOUSEACTIVATE)
        {
            return (LRESULT)MA.MA_NOACTIVATE;
        }
        // §361: its place in the z-order the plan's, as every window's (Dock.WndProc).
        if (msg == WM.WM_WINDOWPOSCHANGING)
        {
            Surfaces.Keep(hwnd, (WINDOWPOS*)(nint)lParam);
        }
        nint cookie = GetWindowLongPtrW(hwnd, GWLP.GWLP_USERDATA);
        if (cookie != 0)
        {
            try
            {
                if (GCHandle.FromIntPtr(cookie).Target is Flyout submenu && submenu.Hwnd == hwnd)
                {
                    _ = submenu.Message(msg, wParam, lParam);
                }
            }
            catch (Exception)
            {
                // Nothing may escape into the window manager's callback.
            }
        }
        return Closing.Default(hwnd, msg, wParam, lParam);
    }

    // Composition calls on a flyout mid-teardown fail harmlessly; a mouse move
    // or a key is not worth an exception.
    private static void Try(Action change)
    {
        try
        {
            change();
        }
        catch (Exception)
        {
        }
    }

    private static int X(LPARAM lParam) => (short)((nint)lParam & 0xFFFF);

    private static int Y(LPARAM lParam) => (short)(((nint)lParam >> 16) & 0xFFFF);

    // Rust's f32::round, as everywhere in Karpo.
    private static float Round(float value) => MathF.Round(value, MidpointRounding.AwayFromZero);

    internal readonly record struct Area(float X, float Y, float W, float H)
    {
        public bool Contains(float x, float y) => x >= X && x < X + W && y >= Y && y < Y + H;
    }

    /// <summary>An item that can be chosen, and the visuals that show its state.</summary>
    /// <summary>
    /// narrator: the menu's tree — a menu of its rows (a list, for a grid or a panel of rows), each its
    /// text, its detail as its status, a check mark as a toggle —, and what a screen reader asks of a
    /// row, done as the keys would: chosen, or its submenu opened.
    /// </summary>
    private sealed class MenuAccess(Flyout menu) : IUiaSource
    {
        public UiaTree Build() => menu.BuildUia();

        public void Act(UiaAct act, int id, double value) => menu.ActUia(act, id - 1, value);
    }

    /// <summary>narrator: the tree a screen reader is given now, for a test.</summary>
    internal UiaTree AccessTree => BuildUia();

    private UiaTree BuildUia()
    {
        var origin = host.Origin;
        var grid = IsGrid;
        var children = new List<UiaNode>(rows.Count + 1);
        // §523: a run of tiles — a colour's swatches, an icon picker's icons — is a list of them, the one in force selected.
        List<UiaNode>? tiles = null;
        var tilesFrom = -1;
        void TilesDone()
        {
            if (tiles is { Count: > 0 } run)
            {
                var box = run.Select(node => node.Bounds).Aggregate((a, b) => new RECT { left = Math.Min(a.left, b.left), top = Math.Min(a.top, b.top), right = Math.Max(a.right, b.right), bottom = Math.Max(a.bottom, b.bottom) });
                children.Add(new UiaNode(TilesUiaId + tilesFrom, UiaName, UiaRole.List, box) { Children = run, AutomationId = $"Tiles{tilesFrom}" });
            }
            tiles = null;
        }
        for (var index = 0; index < rows.Count; index++)
        {
            var item = rows[index].Item;
            if (item.Separator || item.Text.Length == 0)
            {
                continue;
            }
            var fill = rows[index].Bounds;
            var left = origin.x + (int)(viewport.X + fill.X);
            var top = origin.y + (int)(viewport.Y + fill.Y - scroll);
            // menus: a level's head is its back button, the menu named for the level (Flyout.Inline.cs).
            var back = item.Id == InlineBackId;
            // §523: a tile among rows is an item of its run's list (a grid of them alone is the menu's own list).
            var tile = !grid && IsTile(item);
            // §517: a section's heading read as the text it is, over its rows.
            var node = new UiaNode(index + 1, back ? S.DockPageBack : item.Text, item.Header ? UiaRole.Text : grid || tile ? UiaRole.ListItem : item.Primary || back ? UiaRole.Button : item.Control is FlyoutField ? UiaRole.Edit : UiaRole.MenuItem, new RECT
            {
                left = left,
                top = top,
                right = left + (int)MathF.Ceiling(fill.W),
                bottom = top + (int)MathF.Ceiling(fill.H),
            })
            {
                Pattern = item.Header ? UiaPattern.None : tile ? UiaPattern.Select : item.Checked is not null ? UiaPattern.Toggle : UiaPattern.Invoke,
                On = tile ? item.Checked == true : item.Checked,
                Enabled = item.Enabled,
                Focusable = item.Enabled && !item.Header,
                Status = string.Join(", ", new[] { item.Value?.Text, item.Detail }.Where(part => !string.IsNullOrWhiteSpace(part))) is { Length: > 0 } said ? said : null,
                Help = item.Opens ?? item.Accelerator,
                AutomationId = item.Id != 0 ? $"Item{item.Id}" : "",
                Password = item.Control is FlyoutField { Masked: true },
            };
            // §523: a colour's picker: its sliders ranges, its spectrum the colour it stands at; a line of text's words its value.
            node = item.Control switch
            {
                FlyoutMix mix => MixNode(node, mix),
                FlyoutField { Masked: false } line when Line(line) && fieldStates.TryGetValue(item.Id, out var state) => node with { Pattern = UiaPattern.Value, Value = state.Field.Text },
                _ => node,
            };
            if (tile)
            {
                if (tiles is null)
                {
                    (tiles, tilesFrom) = ([], index);
                }
                tiles.Add(node);
                continue;
            }
            TilesDone();
            children.Add(node);
        }
        TilesDone();
        var focused = GetForegroundWindow() != Hwnd ? -1
            : hot >= 0 && hot < rows.Count ? hot + 1 : -1;
        // §355: the menu is its panel, not its window, which spans the display.
        return new UiaTree(new UiaNode(0, UiaName, grid ? UiaRole.List : UiaRole.Menu, host.IsOpen ? host.PanelOnScreen : default) { Children = children, AutomationId = "Karpo.Menu" }, focused);
    }

    private void ActUia(UiaAct act, int index, double value = 0)
    {
        if (!IsOpen || index < 0 || index >= rows.Count || !rows[index].Item.Enabled)
        {
            return;
        }
        var item = rows[index].Item;
        byKeys = true;
        switch (act)
        {
            case UiaAct.Invoke or UiaAct.Toggle:
                // §268: a level's head shown in place goes back.
                if (item.Id == InlineBackId && BackInPlace())
                {
                    break;
                }
                if (item.Children is { Count: > 0 })
                {
                    SetHot(index);
                    OpenSubmenu(index, keys: true);
                }
                else
                {
                    // As a submenu's choice reaches the root: the dock hears it as a choice, or as an
                    // Adjust for a row that stays (§219, §392).
                    if (item.Stays)
                    {
                        PickedOne(PickedAsOne(item));
                    }
                    _ = PostMessageW(Hwnd, item.Stays ? WM_KARPO_FLYOUT_ADJUST : WM_KARPO_FLYOUT_CHOSEN, (WPARAM)(nuint)host.Generation, (LPARAM)(nint)item.Id);
                }
                break;
            case UiaAct.Focus:
                SetHot(index);
                break;
            // §523: a colour's hue or opacity set by a screen reader (Flyout.Mixer.cs).
            case UiaAct.SetValue when item.Control is FlyoutMix:
                MixSetValue(index, value);
                break;
            // §523: a line's words set by a screen reader: kept, as Enter keeps them (Flyout.Fields.cs).
            case UiaAct.SetText when Uia.TakeText(Hwnd, index + 1) is { } text:
                FieldSet(item.Id, text);
                break;
        }
    }

    private sealed partial class Row
    {
        public required FlyoutItem Item;

        /// <summary>The fill's rectangle, in content coordinates.</summary>
        public required Area Bounds;

        /// <summary>
        /// §379: how far past <see cref="Bounds"/> the row takes the pointer, px: a row of a list its whole slot
        /// — out to the menu's sides and halfway to the rows over and under it, so no pixel between two rows is
        /// nobody's —, a level's head its rule too, an icon button its span (<see cref="Takes"/>).
        /// </summary>
        public (float Left, float Top, float Right, float Bottom) Reach;

        /// <summary>§379: whether the row takes the pointer at (<paramref name="x"/>, <paramref name="y"/>), content px: its fill and its <see cref="Reach"/>.</summary>
        public bool Takes(float x, float y) =>
            x >= Bounds.X - Reach.Left && x < Bounds.X + Bounds.W + Reach.Right && y >= Bounds.Y - Reach.Top && y < Bounds.Y + Bounds.H + Reach.Bottom;

        /// <summary>menus: a stepper's two buttons, in content coordinates (Flyout.Controls.cs); empty for any other row.</summary>
        public Area Fewer;
        public Area More;

        /// <summary>menus: its tools, each's box in content coordinates and the plate that shows under the pointer (Flyout.Controls.cs); none for any other row.</summary>
        public List<(Area Box, FlyoutTool Tool, ShapeVisual Plate)> Tools = [];

        public ShapeVisual? Highlight;
        public CompositionSpriteShape? Fill;

        /// <summary>menus: where its icon, its words and its shortcut stand, in content coordinates, for tests (<see cref="Looks"/>); empty where it has none.</summary>
        public Area Icon;
        public Area Words;
        public Area Keys;
        public Area Check;

        /// <summary>menus: where what it is set to stands (Flyout.Values.cs); empty where it says none.</summary>
        public Area Value;

        /// <summary>menus: its icon is Explorer's picture (ExplorerIcons), not a glyph.</summary>
        public bool Explorer;

        /// <summary>Its words, and what it is set to, where they were drawn cut short, whole (CutWords); null where none was.</summary>
        public string? Cut;

        /// <summary>Text and glyphs that take other colours while lit (high contrast).</summary>
        public List<(SpriteVisual Sprite, CompositionBrush Plain, CompositionBrush Bright)> Swaps = [];

        /// <summary>tray: every visual of the row and where it stands, to move it as one (DropGap).</summary>
        public List<(Visual Visual, Vector3 Home)> Parts = [];
    }
}

