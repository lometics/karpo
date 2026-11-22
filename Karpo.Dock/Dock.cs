// The dock: one window on every display, and everything those windows
// share — when each one shows, what the mouse does to it, what a click opens,
// and when the process may sleep.
//
// Every window the dock owns (the hidden host, one dock window per display,
// the window list and the flyout) points at the same Dock through one
// GCHandle and is dispatched from here, so there is one owner of the state and
// one handler running at a time.
//
// The host is never shown. It holds the shell hook, the tray and system
// status notifications and the islands' wake-ups, and hears what Windows
// broadcasts to top-level windows (settings, displays, power, session), once,
// however many displays there are; and it outlives the dock windows, which
// come and go with displays and with the config.
//
// The settings are the edit bar's menus (Taskbar, Folders, ⚙ Settings): every way to them from
// outside edit mode — Win+Alt+Enter, "Get Karpo Pro", a second start of Karpo.exe — opens edit
// mode with the right one open (Dock.EditEntries.cs, docs\widgets.md §340). The dock watches
// config.json's folder and applies whatever is saved there. "Quit Karpo" posts WM_CLOSE to the host
// (class karpo.host, Karpo.Core's DockHost); a bar's close quits nothing (Closing.cs).

using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Karpo.Core;
using Karpo.Dock.Native;
using TerraFX.Interop.Windows;
using Windows.UI.Composition;
using Windows.UI.Composition.Desktop;
using Windows.UI.ViewManagement;
using static TerraFX.Interop.Windows.Windows;
using Color = Windows.UI.Color;
// `using static ...Windows` also brings in a region constant named ERROR.
using ERROR = TerraFX.Interop.Windows.ERROR;
using ICompositorDesktopInterop = TerraFX.Interop.WinRT.ICompositorDesktopInterop;
using IDesktopWindowTarget = TerraFX.Interop.WinRT.IDesktopWindowTarget;

namespace Karpo.Dock;

public sealed class Options
{
    /// <summary>
    /// Holds the hover state with the pointer parked at this fraction of the
    /// apps, so it can be screenshotted without moving anyone's mouse.
    /// </summary>
    public float? Demo;

    public bool Gpu;

    /// <summary>
    /// Glass is allowed: not turned off by flag, and DWM lets windows use the
    /// host backdrop. Whether it shows also depends on the user's transparency
    /// and contrast settings, which can change while Karpo runs.
    /// </summary>
    public bool Glass;

    /// <summary>
    /// Started by Windows at sign-in (<c>--startup</c>, the Run entry of "Start with Windows", or the
    /// logon task that starts it sooner):
    /// Explorer may not have made its taskbar yet (see <see cref="Dock.Open"/>).
    /// </summary>
    public bool Startup;

    /// <summary>
    /// Started again by the guard of a dock that ended (<c>--restarts n@first</c>): how often so far,
    /// which this dock hands to its own guard (GuardPolicy.cs).
    /// </summary>
    internal Restarts Restarts;

    /// <summary>The flags a restarted dock keeps (<see cref="DockGuard.Kept"/>), as given.</summary>
    internal string[] Kept = [];

    /// <summary>The command line without the program name.</summary>
    public static Options FromArgs(string[] args)
    {
        float? demo = null;
        var at = Array.IndexOf(args, "--demo-hover");
        if (at >= 0
            && at + 1 < args.Length
            && float.TryParse(args[at + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            demo = value;
        }
        return new Options
        {
            Demo = demo,
            Gpu = args.Contains("--gpu"),
            Glass = !args.Contains("--no-glass"),
            Startup = args.Contains(StartupRegistration.Argument),
            Restarts = Restarts.In(args),
            Kept = [.. args.Where(DockGuard.Kept.Contains).Distinct()],
        };
    }
}

public sealed unsafe partial class Dock : IDisposable
{
    public const uint WM_KARPO_APPBAR = WM.WM_APP + 1;

    private const string ClassName = "karpo.dock";

    /// <summary>The resource id the SDK gives ApplicationIcon (IDI_APPLICATION's number).</summary>
    private const nint AppIcon = 32512;

    // Timers of a dock window.
    private const uint SETTLE = 1;
    private const uint CONCEAL = 2;
    private const uint PREVIEW_OPEN = 5;
    private const uint PREVIEW_CLOSE = 6;

    // preview: the look at where the pointer really is while a window list is up (PreviewHover.Watched).
    private const uint PREVIEW_WATCH = 19;

    // The look at where the pointer really is while a bar thinks it is on it (DockWindow.HoverWatched):
    // a leave Windows owed and never sent — dropped while a modal loop had the dock busy, lost after a
    // resize, never coming for a tap — leaves no tooltip or lit button behind for longer than this.
    private const uint HOVER_WATCH = 24;

    /// <summary>
    /// §701: a bar's morph not over this long after it began (BarMorph's batch never said it ended): ended then. Past the
    /// slowest morph (Animation Slower) with room to spare.
    /// </summary>
    private const uint MORPH_WATCH = 25;

    /// <summary>§703: Windows' panels off the bars placed once they are still (ShellPopups.SettleMs).</summary>
    private const uint POPUPS_SETTLE = 92;

    private const uint MORPH_WATCH_MS = 3000;
    private const uint HoverWatchMs = 250;
    private const uint RESIZE = 10;
    private const uint DRAG = 12;

    // quick: the wait before a rested pointer opens a widget's flyout, and the grace before one it
    // opened closes again (WidgetHover). Only with its layout's OpenOnHover on (§345, §514); nothing is armed without it.
    private const uint FLYOUT_OPEN = 16;
    private const uint FLYOUT_CLOSE = 17;

    /// <summary>At most one live rebuild per this while the bar is dragged to a new height.</summary>
    private const uint RESIZE_MS = 33;

    /// <summary>How often a block dragged in edit mode checks Escape, which the bar may not have the keys to hear.</summary>
    private const uint DRAG_MS = 50;

    // Timers of the host.
    private const uint LAUNCH = 3;
    private const uint CLOCK = 4;
    private const uint ARRANGE = 7;
    private const uint ISLANDS = 8;
    private const uint REBUILD = 9;
    private const uint DESKTOPS = 11;
    private const uint DESKTOP_RESTORE = 15;

    /// <summary>
    /// appbar: the one look more after a reconcile that sent something, or a disturbance (BarKeeper:
    /// Explorer can take a position and not act on it, and says nothing). One-shot; nothing runs while
    /// all is well. §272.
    /// </summary>
    private const uint RECONCILE = 18;

    private long desktopRevealUntil;

    /// <summary>§714: until when the bars are looked at again after the desktop came to the front (<see cref="DesktopCameToFront"/>).</summary>
    private long desktopWatchUntil;

    /// <summary>
    /// After the dock switched or added a desktop: long enough for Explorer to
    /// have written the new list.
    /// </summary>
    private const uint DESKTOPS_AFTER_MS = 60;

    /// <summary>A window activation reads the desktops at most this often.</summary>
    private const long DESKTOPS_EVERY_MS = 500;

    /// <summary>Long enough that brushing past the dock's edge doesn't make it jump.</summary>
    private const uint HIDE_DELAY_MS = 450;
    /// <summary>
    /// How long an app asking for attention shows a hidden dock.
    /// </summary>
    internal const uint PEEK_MS = 2600;

    // dbt.h and wtsapi32.h, which TerraFX leaves out.
    private const uint DBT_DEVICEARRIVAL = 0x8000;
    private const uint DBT_DEVICEREMOVECOMPLETE = 0x8004;
    private const uint NOTIFY_FOR_THIS_SESSION = 0;
    private const uint WTS_SESSION_LOCK = 7;
    private const uint WTS_SESSION_UNLOCK = 8;

    private const ushort VK_D = 0x44;
    private const ushort VK_R = 0x52;
    private const ushort VK_S = 0x53;

    private const string Personalize = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    private const uint CMD_DELETE_DESKTOP = 5009;
    private const uint CMD_RENAME_DESKTOP = 5090;

    // Edit mode: the bar's menu's "Taskbar settings" (§581; §366 to §580 "Edit"), and the block options, bar and
    // confirmation flyouts of a bar being edited. 5003 and 5004 were the bar menu's "Add" and "Done editing" (gone in §366).
    internal const uint CMD_TASKBAR_SETTINGS = 5000;
    private const uint CMD_REMOVE_ISLAND = 5001;
    private const uint CMD_COPY_ISLAND = 5002;

    /// <summary>menus: a widget's "Cut" (Dock.EditCopy.cs, §220); 5005 was the tray's "Settings" (gone in §261), 5006 and 5007 were edit mode's "Copy" and "Paste" rows once, 5008 a folder's face's "Open" (§481, gone in §527).</summary>
    private const uint CMD_CUT_ISLAND = 5006;

    /// <summary>edit: one per widget in the rail's "not shown" list, by its place in it.</summary>
    private const uint CMD_NOT_SHOWN = 5300;

    /// <summary>edit: one per row of the add list, by its place in the list's entries (AddList.Rows).</summary>
    internal const uint CMD_ADD_WIDGET = 6000;

    /// <summary>layouts: the bar's menu's and the layouts' line's rows, a hundred from here, one per named layout.</summary>
    private const uint CMD_LAYOUT = 5600;
    private const uint CMD_LAUNCH = 1;
    private const uint CMD_PIN = 2;
    private const uint CMD_UNPIN = 3;
    private const uint CMD_CLOSE = 4;
    internal const uint CMD_TASK_MANAGER = 6;
    private const uint CMD_NEW_DESKTOP = 8;
    private const uint CMD_TASK_VIEW = 9;
    /// <summary>pins: the pin whose target is gone mended or removed.</summary>
    private const uint CMD_LOCATE = 15;
    private const uint CMD_JUMP = 100;
    private const uint CMD_MEMBER = 1000;
    private const uint CMD_TRAY = 2000;
    /// <summary>app buttons: one per button behind "…", by its place among the bar's tiles.</summary>
    private const uint CMD_APP_OVERFLOW = 2500;

    // Segoe Fluent Icons, as the Windows 11 taskbar's menus use them.
    private const string GlyphPin = "\uE718";
    private const string GlyphUnpin = "\uE77A";
    private const string GlyphClose = "\uE8BB";
    private const string GlyphSettings = "\uE713";
    private const string GlyphTaskManager = "\uE9D9";
    private const string GlyphAdd = "\uE710";
    private const string GlyphTaskView = "\uE7C4";
    private const string GlyphClock = "\uE823";
    private const string GlyphEdit = "\uE70F";
    // Segoe Fluent Icons: CaretSolidUp and CaretSolidDown, which Windows' own "move" rows use.
    private const string GlyphUp = "\uEDDB";
    private const string GlyphDown = "\uEDDC";
    // Edit mode's: Copy (a widget's Delete is Dock.Menus.cs's).
    private static readonly string GlyphCopy = char.ConvertFromUtf32(0xE8C8);
    // menus: AppIconDefault and Document for a tray icon or a jump list line
    // that brought no picture of its own.
    private static readonly string GlyphApp = char.ConvertFromUtf32(0xECAA);
    private static readonly string GlyphFile = char.ConvertFromUtf32(0xE8A5);

    // Only ever touched on the UI thread, like the Rust thread-locals.

    /// <summary>
    /// Messages that arrived while the dock was busy, inside a menu's or a
    /// dialog's modal loop. Dropping them would leave the window list stale.
    /// </summary>
    private static readonly List<(HWND Hwnd, uint Msg, WPARAM WParam, LPARAM LParam)> Deferred = [];
    private static HWND MainWindow;
    private static uint ShellHookMessage;
    private static uint TaskbarCreatedMessage;

    /// <summary>A second start of Karpo.exe asking this dock for ⚙ Settings (<see cref="SecondStart"/>).</summary>
    private static uint SettingsMessage;

    /// <summary>
    /// One app button of a bar: a pin widget (<paramref name="key"/> its island key), or an open app
    /// the Apps widget shows because nothing on that bar pins it.
    /// </summary>
    private sealed class Tile(Item item, bool pinned, string? key = null)
    {
        public Item Item { get; } = item;
        public bool Pinned { get; } = pinned;

        /// <summary>The pin widget's key ("layout:&lt;id&gt;/&lt;island id&gt;"); null for an open app.</summary>
        public string? Key { get; } = key;

        /// <summary>
        /// An open app's: the key of the Apps widget it stands in — a bar may hold the open apps more
        /// than once, and each widget shows every one of them (Dock.TilesFor); null for a pin's button.
        /// </summary>
        public string? Apps { get; init; }

        /// <summary>The part this button stands in: its pin's, or its Apps widget's.</summary>
        public string? Part => Key ?? Apps;

        /// <summary>Why this PC can't open it (a pin whose target is gone); null when it opens.</summary>
        public string? Broken => Item.Broken;

        /// <summary>The tile's open windows, most recently active first.</summary>
        public List<HWND> Open { get; } = [];
        public Mark Mark { get; set; }

        /// <summary>
        /// app buttons: the one window this button stands for, where a bar's windows are not
        /// combined (<see cref="ButtonCombine"/>); null for an app's own button.
        /// </summary>
        public HWND Window { get; init; }

        /// <summary>app buttons: what the button says beside its icon — its window's title —; null for an icon alone.</summary>
        public string? Label { get; init; }
    }

    internal enum Topic { LayoutEditor, App, Panel, Desktops, Island, Overflow, Missing, Confirm, Start, Broken, NotShown, AppOverflow, Window, WidgetOwn, Library, FolderWidth, FolderHidden, Faults }

    /// <summary>
    /// What every popup a click on a bar opens files itself under when it is dismissed
    /// (<see cref="dismissed"/>): one topic for all of them, because the topic decides the
    /// light-dismiss role and only a flyout lets the press that closed it go on to the widget it
    /// was on. Karpo's clock once filed itself under the clock's <em>menu</em>'s topic when it had
    /// been opened from the bar's own clock, so it swallowed the press: switching from it to another
    /// widget took two clicks, one to close it and one to open the next, while every other flyout
    /// switched on the first. That is the whole of the user's "sometimes".
    /// </summary>
    internal const Topic OpenedByClick = Topic.Island;

    /// <summary>
    /// The rules closing a popup about <paramref name="topic"/> follows (<see cref="LightDismiss"/>).
    /// An arm for every topic and no fall-back: a new one has to say which it is, because the
    /// wrong answer is a popup that eats the click meant for the widget beside it.
    /// </summary>
    internal static PopupRole RoleOf(Topic topic) => topic switch
    {
        // Content a click opened: an island's own flyout (the calendar, the notification list, the
        // quick-settings pages, a folder, a countdown…) and the tray's hidden icons.
        Topic.Island => PopupRole.Flyout,
        Topic.Overflow => PopupRole.Flyout,
        // app buttons: "…"'s list of the buttons the bar has no room for, as the tray's hidden icons.
        Topic.AppOverflow => PopupRole.Flyout,
        // Menus: a right click's, the Start button's quick links, a broken pin's choices, a
        // confirmation, and the report that the settings app is nowhere to be found. A press that
        // closes one of these is used up by that, as it is on the taskbar.
        Topic.LayoutEditor => PopupRole.Menu,
        Topic.App => PopupRole.Menu,
        Topic.Panel => PopupRole.Menu,
        Topic.Desktops => PopupRole.Menu,
        Topic.Missing => PopupRole.Menu,
        Topic.Confirm => PopupRole.Menu,
        Topic.Start => PopupRole.Menu,
        Topic.Broken => PopupRole.Menu,
        // edit: the rail's list of widgets the bar had no room for. A menu: a press that closes it
        // is used up by that, as every other menu of the rail's and the bar's is.
        Topic.NotShown => PopupRole.Menu,
        // A widget's right click with rows of its own (IWidgetMenu): a menu like any widget's.
        Topic.WidgetOwn => PopupRole.Menu,
        // app clicks: a window's own menu, or its windows', from Shift+right click (Dock.AppClicks.cs).
        Topic.Window => PopupRole.Menu,
        // edit: the add list a "+" opens. A menu: a press that closes it is used up by that, so a
        // click away from it doesn't also pick up the block it landed on.
        Topic.Library => PopupRole.Menu,
        // edit: a folder list's width menu, beside its "+" (Dock.Folders.cs, FolderWidthMenu).
        Topic.FolderWidth => PopupRole.Menu,
        // edit: the widgets a folder list has no room for, beside its warning (Dock.Folders.cs, FolderNotShown).
        Topic.FolderHidden => PopupRole.Menu,
        // edit: the errors Karpo knows of, under the edit bar's indicator (Dock.Faults.cs, §394).
        Topic.Faults => PopupRole.Menu,
        _ => throw new ArgumentOutOfRangeException(nameof(topic), topic, "no light-dismiss rules for this popup"),
    };

    /// <summary>What the open flyout is about, so a choice in it can be acted on later.</summary>
    private sealed class Pending(Topic topic, DockWindow? owner)
    {
        public bool Hover { get; init; }
        public Topic Topic { get; } = topic;

        /// <summary>
        /// The rules closing it follows (<see cref="LightDismiss"/>): one that opened on hover is a
        /// hover popup, and the rest is its topic's (<see cref="RoleOf"/>).
        /// </summary>
        public PopupRole Role => Hover ? PopupRole.Hover : RoleOf(Topic);

        /// <summary>
        /// The window it opened from; §356: a configuration menu's is handed on to the window its display
        /// has when an arrange makes that one again (Dock.HandOn).
        /// </summary>
        public DockWindow? Owner { get; set; } = owner;

        /// <summary>
        /// §356: a configuration menu's rows read again from the config as it is now — its settings' rows, their
        /// commands into the list given — and what its top row names it (Dock.MenuAgain); null for a menu that
        /// reads nothing again.
        /// </summary>
        public Func<List<OptionPick>, (List<FlyoutItem> Rows, string? Type)>? Again { get; set; }

        /// <summary>
        /// §374: where the menu stood as a pick in it closed it — its levels, scroll, lit row, words typed —,
        /// for a dialog of Windows' it asked for to show it there again (<see cref="MenuBrowse"/>); null before.
        /// </summary>
        public MenuMemory? Stood { get; set; }
        public Hit? Hit { get; init; }

        /// <summary>
        /// §430: what opened it (<see cref="Openers"/>), which one click on again closes it and opens nothing: the
        /// thing the click it opened on went down on (stamped as it shows, <see cref="ShowFlyout"/>), or the edit bar's
        /// button it hangs from (<see cref="DeckOpened"/>); else its bar's <see cref="Hit"/>, clicked.
        /// </summary>
        public PopupOpener? Opener
        {
            get => opener ?? (Owner is { } window && Hit is { } hit ? new PopupOpener((nint)window.Hwnd, hit, PressButton.Left) : null);
            set => opener = value;
        }

        private PopupOpener? opener;

        /// <summary>§430: what opened it, where nothing said so before (<see cref="Opener"/>).</summary>
        public void OpenedBy(PopupOpener? press) => opener ??= press;
        public string Target { get; init; } = "";

        /// <summary>
        /// §528: the widgets a page of several selected is of (<see cref="Target"/> the first): what its settings read in
        /// common and set on them all (OptionRows.Common.cs); empty for the one <see cref="Target"/>.
        /// </summary>
        public IReadOnlyList<string> Targets { get; init; } = [];

        /// <summary>§528: the widgets its settings are of: <see cref="Targets"/>, else <see cref="Target"/> alone; none for none.</summary>
        public IReadOnlyList<string> Keys => Targets.Count > 0 ? Targets : Target.Length > 0 ? [Target] : [];
        public List<HWND> Open { get; init; } = [];
        public List<JumpEntry> Jumps { get; init; } = [];
        public List<TrayIcon> Hidden { get; init; } = [];

        /// <summary>
        /// tray: how many of the overflow's first rows (<see cref="Hidden"/>) are icons its bar would
        /// show but has no room for (a tray of a set size), rather than hidden ones.
        /// </summary>
        public int Spilled { get; init; }

        /// <summary>
        /// "…" and the tray's overflow: what their rows stood for when they were shown, by what each
        /// is (<see cref="AppOverflowRows"/>, <see cref="TrayOverflowRows"/>), to tell whether the
        /// bar has changed under them (<see cref="FollowOverflow"/>).
        /// </summary>
        public List<string> Rows { get; init; } = [];

        /// <summary>
        /// edit: the widgets the bar holds and has no room to show, for the rail's list of them
        /// (<see cref="Layout.NotShown"/>). A row opens that widget's own menu, which is the only
        /// way to reach a widget that has no block to be clicked.
        /// </summary>
        public List<PartSpec> NotShown { get; init; } = [];

        /// <summary>edit: the widgets a folder list has no room to show, for its warning's list of them (Dock.Folders.cs).</summary>
        public List<IIsland> FolderHidden { get; init; } = [];

        /// <summary>§394: the errors the edit bar's list shows, a row each (Dock.Faults.cs).</summary>
        public List<Fault> Faults { get; init; } = [];
        public int Cell { get; init; }

        /// <summary>edit: a folder's width menu: the list it is for, or the empty slot (<see cref="FolderPlace.Slot"/>).</summary>
        public FolderPlace Column { get; init; }

        /// <summary>
        /// An island's flyout of icons, shown as the tray's overflow is (<see cref="IslandAction.Icons"/>):
        /// the status icons behind their arrow.
        /// </summary>
        public bool Icons { get; init; }

        /// <summary>pins: the pin widget a menu or a broken pin's flyout is about ("layout:&lt;id&gt;/&lt;id&gt;"), if any.</summary>
        public string? Pin { get; init; }

        /// <summary>
        /// folders: the open folder a widget's own menu stands over, which holds itself open while
        /// the menu does and is let go when it ends (Dock.Folders.cs, FolderMenuEnded). Null in
        /// every menu that is not about a widget in a folder.
        /// </summary>
        public OpenFolder? Folder { get; init; }

        /// <summary>folders: the rows of the widget's own acts, by command (Dock.WidgetMenus.cs).</summary>
        public List<WidgetActRow> Acts { get; init; } = [];

        /// <summary>
        /// menus: the rows of the widget's settings and its folder's faces, by command (OptionMenu.cs):
        /// filled again as the menu is shown again after a change.
        /// </summary>
        public List<OptionPick> Options { get; init; } = [];

        /// <summary>edit: the bar whose looks the menu is (Dock.BarLooks.cs, §290); null in every other menu.</summary>
        public BarSpot? Looks { get; init; }

        /// <summary>edit: which of <see cref="Looks"/> the menu has (§330): every one, or an edit bar's button's half.</summary>
        public LooksScope LooksScope { get; init; }

        /// <summary>edit: the edit bar's ⚙ Settings, and the display whose edit bar it hangs from (Dock.SettingsMenu.cs, §335); null in every other menu.</summary>
        public SettingsMenu? SettingsMenu { get; init; }

        /// <summary>edit: the add list's widgets, by command (<see cref="CMD_ADD_WIDGET"/>), and the place it adds to.</summary>
        public List<CatalogueEntry> Library { get; init; } = [];

        /// <summary>
        /// edit: the add list's ready-made groups, by command, counted on from the widgets' last
        /// (<see cref="CMD_ADD_WIDGET"/> + <see cref="Library"/>'s count + n).
        /// </summary>
        public IReadOnlyList<Preset> Presets { get; init; } = [];

        public AddSpot? Spot { get; init; }

        /// <summary>
        /// The edge the flyout opens away from, where it is not its owner's bar's: a folder's dropdown's
        /// (Dock.FolderChoices), a looks' menu's. A popup of the edit bar hangs from its control instead (<see cref="From"/>).
        /// </summary>
        public Edge? Edge { get; init; }

        /// <summary>
        /// edit: the room the flyout stays in and the scale it is drawn at, where they are not its
        /// owner's: a popup of the settings panel on a display that shows None, owned by another display's bar
        /// (Dock.PanelPopup, §326, §520). Null for the owner's.
        /// </summary>
        public RECT? Work { get; init; }

        /// <inheritdoc cref="Work"/>
        public float? Scale { get; init; }

        /// <summary>
        /// folders: a dropdown's list (Dock.FolderChoices): its field's width in px, and the gap in DIP
        /// from the field's middle that clears the field. Null in every other flyout.
        /// </summary>
        public float? ListWidth { get; init; }

        /// <inheritdoc cref="ListWidth"/>
        public float? Gap { get; init; }

        /// <summary>edit: a widget's menu a click opened, standing beside the widget (<see cref="MenuSides"/>); null in every other flyout.</summary>
        public MenuBeside? Beside { get; set; }

        /// <summary>edit: a widget's configuration menu, edit mode's, one height on every level (<see cref="Flyout.Configuration"/>, §288).</summary>
        public bool Configuration { get; init; }

        /// <summary>edit: what a configuration menu is of, its type alone, at the start of its top row (<see cref="Flyout.TypeTitle"/>, §323); null in every other menu.</summary>
        public string? TypeTitle { get; init; }

        /// <summary>edit, §517: one of the edit bar's menus, as tall and as wide as its rows need (<see cref="Flyout.FitsContent"/>).</summary>
        public bool FitsContent { get; init; }

        /// <summary>§520: a popup of the settings panel's (a dropdown's list, a colour's swatches): a choice made closes it (Dock.PanelDropped).</summary>
        public bool Drops { get; init; }

        /// <summary>
        /// §521: the control of a display's edit bar or its panel a popup hangs from — a dropdown, "+ Add", the errors'
        /// indicator (§531) —: under it, flush with its left, as a ComboBox's list (PopupPlace); its display's work area and
        /// scale. Null for every other popup.
        /// </summary>
        public DeckControl? From { get; init; }

        /// <summary>
        /// The bar's own menu's rows that are the person's — a built-in place of Windows', a
        /// shortcut — by command (<see cref="CMD_BAR_ENTRY"/>, Dock.BarMenu.cs). Empty in every other menu.
        /// </summary>
        public List<BarMenuRow> BarMenu { get; init; } = [];

    }

    private readonly HWND host;
    private readonly HINSTANCE instance;
    /// <summary>What GWLP_USERDATA of every window of the dock holds.</summary>
    private GCHandle cell;
    private readonly Options options;
    private Config config;
    private readonly Gfx gfx;

    /// <summary>
    /// How Windows is dressed, as read last (ReadLook); every bar and what opens from it put Karpo's one Appearance on it
    /// (§567): <see cref="TaskbarLook"/>.
    /// </summary>
    private Look look;
    private readonly Icons icons = new();
    /// <summary>Unpinned open apps, resolved once per identity.</summary>
    private readonly Dictionary<string, Item> known = [];
    private readonly Apps apps;
    private readonly Desktops? desktops;
    /// <summary>When the desktops were last read outside the watch, in <see cref="Environment.TickCount64"/> ms.</summary>
    private long desktopsChecked = long.MinValue / 2;
    private Taskbar? taskbar;

    /// <summary>
    /// appbar: no arrange has run yet with Explorer's taskbar to be hidden: the first one decides
    /// whether the new bars wait for the takeover to ask for their strips (<see cref="holdBack"/>).
    /// </summary>
    private bool firstTakeOver;

    /// <summary>
    /// appbar: the first arrange is building the bars while Explorer's taskbar still holds its strip
    /// and stays in view: they ask for nothing until the reconcile that takes over, straight after the
    /// build (<see cref="ArrangeSteps"/>).
    /// </summary>
    private bool holdBack;

    /// <summary>appbar: Windows is signing out or shutting down: Explorer's taskbar is put back and stays so.</summary>
    private bool sessionEnding;

    /// <summary>appbar: what the next arrange's reconcile was asked for by; the strongest wins (a disturbance over a consequence).</summary>
    private Stir? arrangeStir = Stir.Start;

    /// <summary>appbar: what the bars ask the shell about the space they hold (AppbarShell), one for all of them.</summary>
    private readonly IAppbarShell appbarShell = Win32AppbarShell.Instance;

    /// <summary>appbar: the record of what the bars asked and found, %LOCALAPPDATA%\Karpo\appbar.log (AppbarTrace).</summary>
    private readonly AppbarTrace appbarTrace = AppbarTrace.ForDock();

    /// <summary>appbar: which window is in front, and the hooks that say when that changes (FrontWatch).</summary>
    private readonly FrontWatch frontWatch;

    /// <summary>appbar: the one reconcile of the bars, their strips, the full screen apps and Explorer's taskbar (BarKeeper, §272).</summary>
    private readonly BarKeeper keeper;

    /// <summary>appbar: Windows' notifications and panels moved off the strips the bars hold (ShellPopups, §540, §580).</summary>
    private readonly ShellPopups shellPopups;

    /// <summary>appbar: Explorer's taskbar should be hidden now: always while Karpo runs (§465), but on a made-up display and while the session ends.</summary>
    private bool HidesTaskbar => !Stage.On && !sessionEnding;
    private HANDLE? watch;
    private bool sessionNotify;

    // §557: the host told before a drive the folders follow on goes (DriveHolds); null on a config of its own.
    private Drives.WindowNotices? driveNotices;

    // power: the host's registration for energy saver's status (QuickSettings\Services\EnergySaver.cs).
    private nint? energySaverNotify;

    // quick: the host's registration for the built-in screen's brightness (QuickSettings\Services\Brightness.cs).
    private nint? brightnessNotify;
    private List<DockWindow> windows = [];
    /// <summary>The window of the last click, where a flyout nobody pointed at opens.</summary>
    private DockWindow? lastWindow;
    private Preview? preview;
    /// <summary>The dock window the window list was opened from.</summary>
    private DockWindow? previewOwner;
    private List<(HWND Hwnd, string Title)> previewWindows = [];
    /// <summary>
    /// quick: the widget whose flyout a <em>hover</em> opened, and the bar it is on; null when none
    /// is open or a click opened it. A click's is pinned — only a dismissal closes it — so this is
    /// what makes "it closes when I stop pointing at it" true of the one and not of the other
    /// (<see cref="WidgetHover"/>, <see cref="TaskbarLayout.OpenOnHover"/>).
    /// </summary>
    private (DockWindow Window, Hit Hit)? hoverOpen;
    private Flyout? flyout;
    private Pending? pending;
    /// <summary>
    /// Edit mode: the bar that has the keys while every display is edited (null: edit mode is
    /// off). Edit mode is the dock's, not this window's: a build that makes the bar again hands it
    /// to the new one (<see cref="EditHolder"/>, §296).
    /// </summary>
    private DockWindow? editor;

    /// <summary>
    /// edit: the surface last pressed on in edit mode — a bar or a place — which has the keys (<see cref="Keyed"/>): Tab, the arrows and Ctrl+arrows act on the
    /// widget focused there. Null for the bar edit mode began on.
    /// </summary>
    private DockWindow? editKeys;

    /// <summary>edit: the surface the keys act on: the one last pressed on while it is still edited, else the bar edit mode began on.</summary>
    private DockWindow? Keyed => editKeys is { } surface && Arranging(surface) ? surface : editor;

    /// <summary>edit: the window whose right press just called off a drag, whose release then opens no menu.</summary>
    private DockWindow? cancelledRight;
    private HWND editReturn;
    /// <summary>
    /// The release still to come of a press on a bar that closing a popup used up
    /// (<see cref="PressedOn"/>): it does nothing either, so the click never opens, launches or drags.
    /// </summary>
    private (DockWindow Window, uint Up)? swallowed;
    /// <summary>
    /// The popup that closed last by itself, and when: what it was about and what opened it
    /// (its <see cref="Pending.Hit"/>). Each popup's closing records itself here when it was
    /// dismissed rather than chosen from, so a press on a bar that closes it can tell whether it
    /// was on what opened it.
    /// </summary>
    private (Pending What, uint At)? dismissed;
    private Tray? tray;

    /// <summary>§667: Karpo's answer to where a window's button is, for Windows' minimize and restore animations; null where it gives none.</summary>
    private MinRect? minRect;
    /// <summary>§347: each placed tray's icons on its bar and behind its chevron, by its key (<see cref="ReadTray"/>): each its widget's own.</summary>
    private Dictionary<string, TraySplit> traySplits = [];
    /// <summary>tray: the button held on a tray icon, on a bar or in the overflow: a click as it comes up, or a drag and nothing (<see cref="TrayClicks"/>).</summary>
    private readonly TrayClicks trayClicks = new();

    /// <summary>tray: the pointer over the overflow's hidden icons, as each bar keeps it over its own (<see cref="OverflowPointer"/>).</summary>
    private readonly TrayHover overflowHover = new();
    private SystemStatus? systemStatus;
    private readonly IslandCollection islands = new();
    private readonly Dictionary<nuint, string> islandTokens = [];
    private nuint nextIslandToken = 1;
    /// <summary>When each island last ticked, in <see cref="Environment.TickCount64"/> ms.</summary>
    private readonly Dictionary<string, long> islandTicks = [];
    /// <summary>What each island was last told by <see cref="IIsland.Visible"/>.</summary>
    private readonly Dictionary<string, bool> islandShown = [];
    /// <summary>The period ISLANDS runs at, 0 while it is stopped.</summary>
    private uint islandPeriod;

    /// <summary>Whether the islands tick on the islands' timer now: a bar is on screen and none is fullscreen.</summary>
    private bool islandsTicking;
    /// <summary>The minute the islands were last told about, in minutes since 0001.</summary>
    private long islandMinute;
    private readonly uint shellHook;
    private readonly uint taskbarCreated;
    private Action? launching;
    /// <summary>
    /// A handler is running. The Rust dock is a RefCell, and this is its
    /// borrow: whatever arrives meanwhile (from a modal loop the handler
    /// entered) is deferred or takes the default path, never handled twice
    /// over.
    /// </summary>
    private bool busy;
    private bool disposed;

    private Dock(
        HWND host,
        HINSTANCE instance,
        Options options,
        Config config,
        Gfx gfx,
        HANDLE? watch,
        uint shellHook,
        uint taskbarCreated)
    {
        this.host = host;
        // The dock's thread runs above the apps': it only ever runs to answer a change or the person, and on a machine
        // whose cores are all busy it must still answer them at once (docs/resilience.md §4). Idle it costs nothing.
        _ = SetThreadPriority(GetCurrentThread(), 1); // THREAD_PRIORITY_ABOVE_NORMAL
        // app clicks: a close an app run as administrator refused is told here (Apps.Close).
        Apps.Refusals = host;
        this.instance = instance;
        this.options = options;
        this.config = config;
        this.gfx = gfx;
        // stage: a made-up display has no taskbar to take over (Stage.cs).
        firstTakeOver = HidesTaskbar;
        this.watch = watch;
        this.shellHook = shellHook;
        this.taskbarCreated = taskbarCreated;
        frontWatch = new FrontWatch(host);
        keeper = new BarKeeper(new LiveExplorer(this), frontWatch, appbarTrace);
        // §666: Windows lays Start out in the middle of its host unless its taskbar is aligned left (TaskbarAl 0), read as Start shows;
        // Start hangs from a Start button of Karpo's on the display it opens on.
        shellPopups = new ShellPopups(appbarTrace.Write, () => Advanced("TaskbarAl") is not 0, StartButtonOn, () => Arm(POPUPS_SETTLE, ShellPopups.SettleMs));
        // appbar: each start is a line of its own, so a failed reservation can be told apart
        // from the start before it (AppbarTrace).
        appbarTrace.Write($"start: pid {Environment.ProcessId}, "
            + $"Explorer's taskbar {(firstTakeOver ? "to be hidden as the bars take over" : "left alone")}, its tray {(Taskbar.ExplorerTray() == HWND.NULL ? "NOT FOUND" : "found")}");
        look = ReadLook();
        // §499, §507: the font and weight Karpo's own chrome is in (the default layout's menus', Look.Configured), which what
        // wears no font of its own is measured and drawn in (each widget, bar and menu puts its own on, Gfx.Wearing);
        // Rebuild sets it again with every look read.
        gfx.Typeface = look.Typeface;
        // calendar: how dates, times and units are written — the settings someone chose, or nothing
        // at all while they follow Windows. Nls is the one place that reads it.
        Panels.Nls.Apply(config.Formats);
        // online: how often everything online updates — the weather, the stocks, the accounts —
        // one interval for all of it (Karpo.Core\OnlineUpdates.cs), before any of them is made.
        OnlineUpdates.Shared.Set(config.OnlineUpdateMinutes);
        // links: the map, the weather site and the finance site a widget opens on (LinkProviders, §319, §322).
        LinkProviders.Use(config);
        // holidays: the public holidays' calendar as the config has it; off, it asks nothing (§237).
        CalendarStore.Shared.Holidays?.Configure(config.Holidays);
        // The language Karpo's own words are in (S), chosen in Settings or Windows' own, before
        // anything is measured or drawn.
        Languages.Apply(config.Language);
        // places: what the device's position was called last, in that language, for every widget
        // for it (§253), before any is drawn.
        WeatherStore.Shared.Restore();
        // Islands made now draw today's date anyway.
        islandMinute = Clock.Real().Ticks / TimeSpan.TicksPerMinute;
        apps = new Apps();
        desktops = Desktops.Open();
        PublishDesktops();
        // desktops: the desktops' line reads and acts through the dock (Dock.Switchers.cs).
        DesktopState.Service = desktopsService = new DesktopsService(this);
        // calendar: every timer and focus session is a widget of its own, on the bar and in folders,
        // so the dock listens to its countdowns from the start (Dock.Countdowns.cs).
        HookCountdowns();
        // quakes: and to the earthquakes' store, so a new one is said with no folder open (Dock.Quakes.cs).
        HookQuakes();
        // network: and to the network coming back, so a fetch that failed before it was up is tried again (Dock.Network.cs).
        HookNetwork();
        // §491: every launch closes what of Karpo's is open first (Dock.Leave.cs).
        HookLaunches();
        // pins: and to the pictures pinned sites hand back, on this thread, so one that arrives is
        // drawn (Dock.Favicons.cs).
        HookFavicons();
        // §394: the errors Karpo knows of, for the edit bar (Dock.Faults.cs).
        HookFaults();
        // A config.json another program held as Karpo started is read again soon (Dock.KeepConfig.cs).
        ReadConfigSoon(ConfigReload.Unchanged);
        // sports: a search's teams and leagues in a menu wear their logos, fetched and kept as the lines' are (§369).
        // §380: and a pin's targets' icons, in "What it opens" (TargetLooks.cs).
        Flyout.Pictures = static (address, dark, came) =>
            TargetPictures.Owns(address) ? TargetPictures.Shared.Get(address, came) : Sports.SportsLogos.Shared.Get(address, dark, came);
        // pins: and to Windows' own taskbar pins, kept in step with the bars' (Dock.WindowsPins.cs).
        WatchWindowsPins();
        // licence: the Store asked from the start, so an add in edit mode already knows (Dock.License.cs).
        StartLicense();
    }

    /// <summary>
    /// A dock window on every enabled display, each shown once its first
    /// scene is built. The dock must be disposed after the message loop: every
    /// window procedure reaches it.
    /// </summary>
    public static Dock Open(Options options)
    {
        Program.TraceMemory("start");
        var atSignIn = options.Startup;
        try
        {
            Directory.CreateDirectory(Config.Folder);
        }
        catch (Exception)
        {
        }
        var config = Config.Load();
        if (config.Recovery is { } recovery)
        {
            Console.Error.WriteLine($"Karpo: {recovery}");
        }
        // pins: a first run pins what Windows' own taskbar has (Sections.DefaultLayout).
        // "Start with Windows": the Run entry follows the setting, for this copy of Karpo.
        SyncStartup(config.StartWithWindows);
        // appbar: Explorer's taskbar is hidden by the first arrange, once the bars are registered and
        // with their strips asked for in the same breath (BarKeeper), not here: hidden here, its
        // strip was gone for the half second the device and the bars take to make, and maximized
        // windows grew to the whole display and back.
        var host = HWND.NULL;
        Gfx? device = null;
        Dock? dock = null;
        try
        {
            var instance = (HINSTANCE)(void*)GetModuleHandleW(null);
            var cursor = LoadCursorW(HINSTANCE.NULL, IDC.IDC_ARROW);
            // The exe's own icon (resource 32512, see Karpo.Dock.csproj), so every
            // window of the dock carries the logo wherever Windows shows one.
            var logo = LoadIconW(instance, (char*)AppIcon);
            foreach (var name in new[] { ClassName, DockHost.ClassName, Preview.ClassName, Flyout.ClassName })
            {
                fixed (char* className = name)
                {
                    var wc = new WNDCLASSW
                    {
                        lpfnWndProc = &WndProc,
                        hInstance = instance,
                        hIcon = logo,
                        hCursor = cursor,
                        lpszClassName = className,
                    };
                    RegisterClassW(&wc);
                }
            }
            // A class of its own: the settings app and Karpo.exe --quit find the
            // host by it, never a bar, however many displays there are.
            fixed (char* className = DockHost.ClassName)
            fixed (char* title = "Karpo")
            {
                host = CreateWindowExW(
                    WS.WS_EX_TOOLWINDOW | WS.WS_EX_NOACTIVATE,
                    className,
                    title,
                    WS.WS_POPUP,
                    0,
                    0,
                    0,
                    0,
                    HWND.NULL,
                    HMENU.NULL,
                    instance,
                    null);
            }
            if (host == HWND.NULL)
            {
                throw LastError("CreateWindowExW");
            }
            MainWindow = host;

            options = new Options { Demo = options.Demo, Gpu = options.Gpu, Glass = options.Glass, Restarts = options.Restarts, Kept = options.Kept };
            if (options.Glass)
            {
                // Lets a plain Win32 window use the host backdrop brush:
                // DWM's own blur of whatever is behind it. Windows 11 only.
                // Asked of the host, so the answer doesn't wait for a display.
                BOOL on = BOOL.TRUE;
                options.Glass = DwmSetWindowAttribute(
                    host,
                    (uint)DWMWINDOWATTRIBUTE.DWMWA_USE_HOSTBACKDROPBRUSH,
                    &on,
                    (uint)sizeof(BOOL)).SUCCEEDED;
            }
            Program.TraceMemory("window");

            var gfx = device = new Gfx(options.Gpu);
            Program.TraceMemory("device");
            if (atSignIn)
            {
                // Started at sign-in, by the logon task as the session begins: Explorer may still be
                // making its taskbar, which must exist before it can be hidden or the bars' space
                // reserved beside it. Waited for only now, so the config, the window and the device
                // are made meanwhile, and looked for often, so the bars follow it at once. A taskbar
                // that comes later still is handled by TaskbarRestarted.
                for (var waited = 0; waited < StartupWaitMs && Taskbar.ExplorerTray() == HWND.NULL; waited += StartupLookMs)
                {
                    Thread.Sleep(StartupLookMs);
                }
            }

            uint shellHook;
            uint taskbarCreated;
            fixed (char* name = "SHELLHOOK")
            {
                shellHook = RegisterWindowMessageW(name);
            }
            fixed (char* name = "TaskbarCreated")
            {
                taskbarCreated = RegisterWindowMessageW(name);
            }
            ShellHookMessage = shellHook;
            TaskbarCreatedMessage = taskbarCreated;
            SettingsMessage = SecondStart.Message;
            HANDLE? watch = null;
            fixed (char* folder = Config.Folder)
            {
                var handle = FindFirstChangeNotificationW(
                    folder,
                    BOOL.FALSE,
                    FILE.FILE_NOTIFY_CHANGE_LAST_WRITE | FILE.FILE_NOTIFY_CHANGE_FILE_NAME);
                if (handle != HANDLE.INVALID_VALUE && handle != HANDLE.NULL)
                {
                    watch = handle;
                }
            }
            dock = new Dock(host, instance, options, config, gfx, watch, shellHook, taskbarCreated);
            dock.cell = GCHandle.Alloc(dock);
            SetWindowLongPtrW(host, GWLP.GWLP_USERDATA, GCHandle.ToIntPtr(dock.cell));
            // menus: a press on a bar closes the open popups there (PressedOn), not in their mouse hook.
            FlyoutHost.PressesHandledBy = dock.IsBar;
            // search: every flyout of Karpo's closes Windows Search and Start as it opens — the
            // person's own, so never from a dock on a config of its own (a test, --smoke).
            FlyoutHost.Opening = Config.IsIsolated ? null : foreground => WindowsPanels.CloseFor(WindowsPanels.Instance, foreground);

            dock.busy = true;
            try
            {
                RegisterShellHookWindow(host);
                // §667: a window minimizes into its button on Karpo's bars and is restored out of it (MinRect.cs): on the
                // person's desktop from their own dock alone; a stage on a desktop of its own answers there.
                if (!Config.IsIsolated || !InputDesktop.Here())
                {
                    dock.minRect = MinRect.Start(window => WindowButtons.Shared.For((HWND)window));
                }
                // appbar: which window is in front, for the full screen apps the bars make way for,
                // and the end of the Explorer whose taskbar is hidden (BarKeeper, Taskbar).
                if (!Stage.On)
                {
                    dock.frontWatch.Start();
                }
                Taskbar.Host = host;
                // Lock and unlock: a lock ends edit mode, and an unlock can bring displays back.
                dock.sessionNotify = WTSRegisterSessionNotification(host, NOTIFY_FOR_THIS_SESSION) != 0;
                // §557: Windows asks the host before a drive goes, and the folders followed on it let go
                // (DriveHolds) — the person's own folders, so never from a dock on a config of its own.
                if (!Config.IsIsolated)
                {
                    dock.driveNotices = new Drives.WindowNotices(host);
                    Drives.DriveHolds.Shared.Notices = dock.driveNotices;
                }
                // power: energy saver's own status, which the power status's flag no longer tells since
                // 24H2; every view of energy saver reads it again on the broadcast this brings.
                dock.energySaverNotify = Karpo.Dock.QuickSettings.Services.EnergySaver.Follow(host);
                // quick: the built-in screen's brightness, moved by the keyboard's keys or Windows' own
                // slider; the brightness widgets follow it (Brightness.cs, docs\widgets.md §439).
                dock.brightnessNotify = Karpo.Dock.QuickSettings.Services.Brightness.Follow(host);
                // screentime: counted from the dock's start, whether or not a line shows it; never on
                // a config of its own (a test, --smoke), which must not touch the person's files.
                if (!Config.IsIsolated)
                {
                    // What earlier versions left that nothing reads any more goes, off this thread (RetiredFiles).
                    _ = Task.Run(() => Troubles.Guard("Retired files", RetiredFiles.Sweep));
                    ScreenTimeTracker.Shared.Start(host, Config.LocalFolder);
                    // start, search: when Windows' Start and Search last went, for a click on their
                    // buttons whose press sent them away (WindowsPanels.Watch).
                    WindowsPanels.Instance.Watch();
                    // §474: the button of Windows' menu in front lit while it is.
                    WindowsPanels.Instance.FrontChanged += dock.FacesShowOpen;
                    // pim: the accounts' calendars and mail are synced from the start, widget or not.
                    Karpo.Dock.Pim.PimSync.Start();
                }
                Menu.Theme(dock.look.Dark);
                // The shortcuts jump lists fall back on, read off this thread
                // now, so the first right-click doesn't wait for them.
                JumpLists.Prepare();
                // A frame's plan runs as the dock's handlers do (Surfaces, §354).
                Surfaces.Around = dock.Presenting;
                // Arrange makes the bars, then the islands and services they show; each bar shows
                // once its first scene is on the screen.
                using (Surfaces.Begin(gfx.Compositor, dock.PresentChrome))
                {
                    // desktops: every desktop's windows listed where a layout shows them (§362).
                    dock.SyncAllDesktops();
                    dock.Arrange();
                    // preview: and the window list's popup, hidden, so the first hover makes no window.
                    dock.WarmPreview();
                }
            }
            finally
            {
                dock.busy = false;
            }
            Program.TraceMemory("scene");
            return dock;
        }
        catch
        {
            // What Karpo changed outside itself goes back, as the Rust drop of
            // a half-built dock does.
            if (dock is not null)
            {
                dock.Dispose();
            }
            else
            {
                device?.Dispose();
                if (host != HWND.NULL)
                {
                    MainWindow = HWND.NULL;
                    Surfaces.Gone(host);
                }
            }
            throw;
        }
    }

    /// <summary>Handles the message loop waits on besides messages.</summary>
    public HANDLE[] Handles()
    {
        var handles = new List<HANDLE>(2);
        if (watch is HANDLE folder)
        {
            handles.Add(folder);
        }
        if (desktops is not null)
        {
            handles.Add(desktops.Event);
        }
        return [.. handles];
    }

    public void Signaled(HANDLE handle)
    {
        if (busy)
        {
            return;
        }
        busy = true;
        try
        {
            using var frame = Surfaces.Begin(gfx.Compositor, PresentChrome);
            if (watch == handle)
            {
                FindNextChangeNotification(handle);
                Report(ConfigChanged);
            }
            else if (desktops is not null && desktops.Event == handle)
            {
                Report(DesktopsChanged);
            }
        }
        finally
        {
            busy = false;
        }
    }

    /// <summary>Handles what was deferred during a modal loop.</summary>
    public void Drain()
    {
        if (busy)
        {
            return;
        }
        busy = true;
        try
        {
            using var frame = Surfaces.Begin(gfx.Compositor, PresentChrome);
            DrainNow();
        }
        finally
        {
            busy = false;
        }
    }

    /// <summary>
    /// What runs a frame's plan (Surfaces.Around): as busy, so the messages its windows are sent take
    /// the default path as they did inside a handler; and in a frame of its own, for what waited for
    /// the screen (Surfaces.AfterPresent) and asks of a window in turn.
    /// </summary>
    private void Presenting(Action run)
    {
        var was = busy;
        busy = true;
        try
        {
            using var frame = Surfaces.Begin(gfx.Compositor, PresentChrome);
            run();
        }
        finally
        {
            busy = was;
        }
    }

    private void DrainNow()
    {
        if (Deferred.Count == 0)
        {
            return;
        }
        var pendingMessages = Deferred.ToArray();
        Deferred.Clear();
        foreach (var (window, msg, wParam, lParam) in pendingMessages)
        {
            try
            {
                DispatchNow(window, msg, wParam, lParam);
            }
            catch (Exception error)
            {
                Report(error);
            }
        }
    }

    private LRESULT? Dispatch(HWND window, uint msg, WPARAM wParam, LPARAM lParam)
    {
        DrainNow();
        return DispatchNow(window, msg, wParam, lParam);
    }

    private LRESULT? DispatchNow(HWND window, uint msg, WPARAM wParam, LPARAM lParam)
    {
        try
        {
            return DispatchOne(window, msg, wParam, lParam);
        }
        finally
        {
            // §430: a release handled, and what its click opened with it: the click is over.
            if (msg is WM.WM_LBUTTONUP or WM.WM_RBUTTONUP or WM.WM_MBUTTONUP)
            {
                openers.Released();
            }
        }
    }

    private LRESULT? DispatchOne(HWND window, uint msg, WPARAM wParam, LPARAM lParam)
    {
        // narrator: what a screen reader asked of an element, done as a click would be (Accessibility\Uia.cs).
        if (msg == Uia.WM_KARPO_UIA_ACT)
        {
            Uia.Act(window, wParam, lParam);
            return (LRESULT)0;
        }
        // edit: a second start of Karpo.exe (the Start menu, Explorer, a pin): edit mode on the main
        // display, ⚙ Settings open under its gear (Dock.EditEntries.cs).
        if (SettingsMessage != 0 && msg == SettingsMessage && window == host)
        {
            Report(() => OpenEditMenu(null, EditEntry.SecondStart));
            return (LRESULT)0;
        }
        // edit: a press or Escape the hooks heard while a widget followed the pointer (EditFollowHook.cs).
        if (window == host && msg == EditFollowHook.WM_KARPO_FOLLOW)
        {
            Report(() => FollowHeard((FollowInput)(uint)(nuint)wParam, EditFollowHook.Unpack(lParam)));
            return (LRESULT)0;
        }
        // This thread's keyboard layout changed (lParam), in whichever
        // window of Karpo's had the keys. The window still gets it.
        if (msg == WM.WM_INPUTLANGCHANGE)
        {
            foreach (var keyboard in Keyboards())
            {
                keyboard.Switched((HKL)(nint)lParam);
            }
        }
        // The host's close from Karpo quits; Alt+F4 on a bar asks for Windows' Shut Down dialog; no
        // other close quits anything (Closing.cs).
        if (msg is WM.WM_CLOSE or WM.WM_SYSCOMMAND
            && Closing.Answer(window == host ? Closing.Role.Host : windows.Exists(bar => bar.Hwnd == window) ? Closing.Role.Bar : Closing.Role.Other, msg, wParam, Quit) is { } closed)
        {
            return closed;
        }
        if (window == host)
        {
            return HostMessage(msg, wParam, lParam);
        }
        if (flyout is not null && flyout.Hwnd == window)
        {
            return FlyoutMessage(msg, wParam, lParam);
        }
        // quick: Karpo's quick settings and the rename-desktop dialog (a Menu host: it takes the focus).
        if (quickFlyout is { } quick && quick.Hwnd == window)
        {
            var happened = quick.Message(msg, wParam, lParam);
            if (happened.Action != QuickSettings.QuickFlyoutAction.None)
            {
                QuickClosed(happened);
            }
            return null;
        }
        // folders: a folder's list (FolderFlyout, a Menu host: it takes the focus; Dock.Folders.cs).
        // Arranged it spans the screen, and a press on its room outside its own things goes through.
        if (msg == WM.WM_NCHITTEST && FolderPassesThrough(window, lParam))
        {
            return (LRESULT)HTTRANSPARENT;
        }
        if (FolderMessage(window, msg, wParam, lParam))
        {
            return null;
        }
        if (preview is not null && preview.Hwnd == window)
        {
            return PreviewMessage(msg, wParam, lParam);
        }
        // edit: the sections' edit bars, and their tooltips (Tool hosts: they never take the focus
        // and never close by themselves; Dock.EditRail.cs).
        if (RailWindow(window))
        {
            return RailMessage(window, msg, wParam, lParam);
        }
        // edit: every display's frame, and its edit bar (Dock.EditDeck.cs).
        if (DeckWindow(window))
        {
            return DeckMessage(window, msg, wParam, lParam);
        }
        // §623: a folder's backdrop over every display (Dock.Backdrop.cs).
        if (BackdropWindow(window))
        {
            return BackdropMessage(msg);
        }

        foreach (var dockWindow in windows)
        {
            if (dockWindow.Hwnd == window)
            {
                return WindowMessage(dockWindow, msg, wParam, lParam);
            }
        }
        return null;
    }

    /// <summary>
    /// What the host hears: broadcasts, the shell hook, what the tray, the
    /// system status and the islands post, and the dock's shared timers.
    /// </summary>
    private LRESULT? HostMessage(uint msg, WPARAM wParam, LPARAM lParam)
    {
        try
        {
            switch (msg)
            {
                case WM.WM_TIMER:
                    HostTimer((uint)(nuint)wParam);
                    break;
                // A burst of these comes with one change of displays; they are
                // answered once, when the queue is through them.
                case WM.WM_DISPLAYCHANGE:
                case WM.WM_DPICHANGED:
                    // Edit mode ends with a change of displays.
                    EndEdit(rebuild: false);
                    // §547: the desktops read again for the bars the arrange lays out.
                    wallpaperDesktops = null;
                    // appbar: a full screen app's own change of display mode is one of these: its
                    // display's bars make way now, before the arrange lays anything out on the new
                    // mode (BarKeeper.Look); the arrange's reconcile then looks after the strips.
                    keeper.Look(windows, editor is not null, Stir.Displays);
                    RequestArrange(msg == WM.WM_DISPLAYCHANGE ? Stir.Displays : Stir.Scale);
                    // quick: what the brightness learnt about the displays — their handles, which way
                    // reaches each — is stale; it finds them again, off this thread.
                    QuickSettings.Services.Brightness.DisplaysChangedIfMade();
                    break;
                case WM.WM_SETTINGCHANGE:
                    SettingChanged(wParam);
                    break;
                // §556: a font installed or removed: a look's Font lists this PC's families again when next built.
                case WM.WM_FONTCHANGE:
                    InstalledFonts.Forget();
                    break;
                case WM.WM_SYSCOLORCHANGE:
                case WM.WM_THEMECHANGED:
                case WM.WM_DWMCOLORIZATIONCOLORCHANGED:
                    LookChanged();
                    break;
                case WM.WM_TIMECHANGE:
                    // alarm: a new zone moves an alarm's next ring with the wall clock.
                    TimeZoneInfo.ClearCachedData();
                    AlarmsDue();
                    TickClock();
                    break;
                case WM.WM_POWERBROADCAST:
                    // quick: the built-in screen's brightness was heard already (WndProc); the battery and
                    // energy saver, which read again on any power setting, have nothing new to read.
                    if (BrightnessOnly(wParam, lParam))
                    {
                        return (LRESULT)1;
                    }
                    // power: energy saver's status was kept already (WndProc), so what reads it now reads the new one.
                    ToIslands(msg, wParam, lParam);
                    if ((nuint)wParam == PBT.PBT_APMRESUMEAUTOMATIC)
                    {
                        Report(TickClock);
                        // alarm: one the PC slept through rings now if it is only just late.
                        Report(AlarmsDue);
                        Report(DesktopsChanged);
                        Report(RefreshIslands);
                        // keys: a low-level hook that answered slowly once is taken away without a word, and
                        // a wake, its pages coming back from disk, is when it answers slowly: in again.
                        Report(SyncKeys);
                        // appbar: displays come back from sleep one by one, and the work areas with them.
                        Reconcile(Stir.Resumed);
                    }
                    return (LRESULT)1;
                case WM.WM_WTSSESSION_CHANGE:
                    ToIslands(msg, wParam, lParam);
                    // Edit mode ends when the session locks.
                    if ((uint)(nuint)wParam == WTS_SESSION_LOCK)
                    {
                        EndEdit(rebuild: true);
                    }
                    // appbar: an unlock can come with displays that were off, or a remote session's.
                    else if ((uint)(nuint)wParam == WTS_SESSION_UNLOCK)
                    {
                        // keys: as after a wake, the hook in again (a lock can last the night).
                        Report(SyncKeys);
                        Reconcile(Stir.Unlocked);
                    }
                    break;
                // appbar: the foreground changed, or a window the full screen look depends on moved,
                // showed, hid or ended (FrontWatch).
                case FrontWatch.WM_KARPO_FRONT:
                    frontWatch.Handled(moved: (nuint)wParam != 0);
                    Reconcile((nuint)wParam != 0 ? Stir.Moved : Stir.Front);
                    // §714: the desktop came to the front: Show desktop may have raised it over the bars.
                    if ((nuint)wParam == 0)
                    {
                        DesktopCameToFront();
                    }
                    break;
                // §619: a window minimized or restored: the app buttons' active plate read again (Front).
                case FrontWatch.WM_KARPO_MINIMIZED:
                    frontWatch.MinimizedHandled();
                    WindowsChanged(Change.States);
                    break;
                // appbar: the Explorer whose taskbar is hidden ended; its next taskbar is hidden as it shows.
                case Taskbar.WM_KARPO_EXPLORER_GONE:
                    if (taskbar is not null)
                    {
                        appbarTrace.Write("Explorer ended: its next taskbar is hidden the moment it shows");
                        taskbar.ExplorerGone();
                    }
                    break;
                case WM.WM_DEVICECHANGE:
                    if ((uint)(nuint)wParam is DBT_DEVICEARRIVAL or DBT_DEVICEREMOVECOMPLETE)
                    {
                        RefreshIslands();
                        InputsChanged();
                        return (LRESULT)1;
                    }
                    return null;
                case WM.WM_QUERYENDSESSION:
                    SessionEnding();
                    return (LRESULT)1;
                case WM.WM_ENDSESSION:
                    SessionEnded((nuint)wParam != 0);
                    break;
                case Tray.WM_KARPO_TRAY:
                    TrayChanged();
                    break;
                case Taskband.WM_KARPO_SIGNALS:
                    SignalsChanged();
                    break;
                // app clicks: an app run as administrator would not be closed (Apps.Close, Dock.AppClicks.cs).
                case Apps.WM_KARPO_REFUSED:
                    CantClose((HWND)(nint)(nuint)wParam);
                    break;
                // keys: a chord of the taskbar's, which the hook took (TaskbarKeys.cs, Dock.Keys.cs).
                case TaskbarKeys.WM_KARPO_KEYS:
                    ChordMade((nuint)wParam);
                    break;
                case SystemStatus.WM_KARPO_SYSTEM:
                    SystemChanged();
                    break;
                case Islands.WM_KARPO_ISLAND:
                    IslandReady((nuint)wParam);
                    break;
                // pins: Windows' pins changed (Dock.WindowsPins.cs).
                // network: an address came or went (Dock.Network.cs).
                case WM_KARPO_NETWORK:
                    NetworkHeard();
                    break;
                // §491: a launch off the dock's thread: what of Karpo's is open closes (Dock.Leave.cs).
                case Launch.WM_KARPO_LEAVE:
                    LeaveForLaunch();
                    break;
                // appbar: the guard ended while the dock runs; another is started (Dock.Guard.cs).
                case WM_KARPO_GUARD_GONE:
                    GuardGone();
                    break;
                case WM_KARPO_PINS:
                    WindowsPinsHeard();
                    break;
                // §547: a bar's wallpaper strip composed off this thread (Dock.Wallpaper.cs).
                case WM_KARPO_WALLPAPER:
                    WallpapersComposed();
                    break;
                default:
                    if (shellHook != 0 && msg == shellHook)
                    {
                        ShellMessage((uint)(nuint)wParam, (HWND)(nint)lParam);
                    }
                    else if (taskbarCreated != 0 && msg == taskbarCreated)
                    {
                        TaskbarRestarted();
                    }
                    else
                    {
                        return null;
                    }
                    break;
            }
        }
        catch (Exception error)
        {
            Report(error);
        }
        return (LRESULT)0;
    }

    /// <summary>What a dock window hears: the pointer, its timers, its appbar and drops.</summary>
    private LRESULT? WindowMessage(DockWindow window, uint msg, WPARAM wParam, LPARAM lParam)
    {
        // §358: the pointer in the layout's px: the bar's window stands still round it (DockWindow.Shift).
        var local = window.FromClient((short)((nint)lParam & 0xffff), (short)(((nint)lParam >> 16) & 0xffff));
        var (x, y) = (local.X, local.Y);
        try
        {
            switch (msg)
            {
                case WM.WM_MOUSEACTIVATE:
                    return (LRESULT)MA.MA_NOACTIVATE;
                // edit: losing the activation does not end edit mode, nor does a click away: only
                // the edit bar's "Done" and Escape end it (Dock.EditRail.cs).
                case WM.WM_SETCURSOR:
                    if (((nint)lParam & 0xffff) == HTCLIENT && window.SetsCursor())
                    {
                        return (LRESULT)1;
                    }
                    return null;
                case WM.WM_CAPTURECHANGED:
                    window.CaptureChanged((HWND)(nint)lParam);
                    break;
                case WM.WM_MOUSEMOVE:
                    window.PointerMoved(x, y);
                    break;
                case WM.WM_MOUSELEAVE:
                    window.Leave();
                    break;
                // The pointer came to rest, which a tray icon under it waited for.
                case WM.WM_MOUSEHOVER:
                    window.Rested();
                    break;
                // quick: the wheel over a widget that takes it (the volume slider); anything
                // else leaves the wheel alone (Dock.Quick.cs).
                case WM.WM_MOUSEWHEEL:
                    if (!window.SliderWheel(wParam, lParam))
                    {
                        return null;
                    }
                    break;
                case WM.WM_LBUTTONDOWN:
                case WM.WM_MBUTTONDOWN:
                    window.Pointed(x, y);
                    // start: whether Windows' Start shows as the Start button goes down, and when that
                    // was — a Start the press itself sent away went after it — for its click.
                    var pressedAt = unchecked((uint)GetMessageTime());
                    startToggle.Pressed(msg == WM.WM_LBUTTONDOWN && window.HitAt(x, y) is { Kind: HitKind.Start }, pressedAt);
                    // search: the same for Windows Search and the Search button.
                    searchToggle.Pressed(msg == WM.WM_LBUTTONDOWN && window.HitAt(x, y) is { Kind: HitKind.Search }, pressedAt);
                    // windowsMenus: and for Windows' menus, by their buttons and Windows' own faces (§466, §474).
                    windowsMenus.Pressed(msg == WM.WM_LBUTTONDOWN ? WindowsButtonAt(window, window.HitAt(x, y)) : null, pressedAt);
                    // §619: the app button active as it goes down, before closing the popups hands the focus back.
                    FrontAtPress(window, window.HitAt(x, y));
                    // menus: the open popups close first; a press that used up does nothing else.
                    if (PressedOn(window, window.HitAt(x, y), msg == WM.WM_MBUTTONDOWN ? PressButton.Middle : PressButton.Left, ScreenOf(window.Hwnd, lParam)))
                    {
                        pressedFront = null;
                        break;
                    }
                    window.Pressed(x, y, msg == WM.WM_MBUTTONDOWN);
                    // A tray icon hears its press now and its release on the click.
                    // Never in edit mode, where a press only picks a block up.
                    if (msg == WM.WM_LBUTTONDOWN && !Arranging(window))
                    {
                        TrayPress(window, window.HitAt(x, y), right: false, x, y);
                    }
                    break;
                case WM.WM_LBUTTONUP:
                case WM.WM_MBUTTONUP:
                    if (Swallowed(window, msg))
                    {
                        break;
                    }
                    window.Released(x, y, msg == WM.WM_MBUTTONUP);
                    // §619: the active button kept at the press goes with its release, clicked or not.
                    pressedFront = null;
                    break;
                case WM.WM_RBUTTONDOWN:
                    // tray: a right press calls a tray icon's drag off, and its release opens nothing.
                    if (window.TrayDragging)
                    {
                        window.CancelTrayDrag();
                        cancelledRight = window;
                        break;
                    }
                    // §534: so does the bar's carry to another edge, the bar staying where it stood.
                    if (window.BarMoving)
                    {
                        window.CancelBarMove();
                        cancelledRight = window;
                        break;
                    }
                    window.Pointed(x, y);
                    if (PressedOn(window, window.HitAt(x, y), PressButton.Right, ScreenOf(window.Hwnd, lParam)))
                    {
                        break;
                    }
                    // In edit mode a right press only cancels a drag; its release, then, opens nothing.
                    // Else its release is a click's (EditRightClick), but on the widget whose menu the
                    // press closed, which it only closes.
                    if (Arranging(window))
                    {
                        rightClosed = OptionsShownFor(window);
                        cancelledRight = window.CancelDrag() ? window : null;
                        break;
                    }
                    // The right press, its release comes with the context request.
                    TrayPress(window, window.HitAt(x, y), right: true, x, y);
                    break;
                case WM.WM_RBUTTONUP:
                    if (Swallowed(window, msg))
                    {
                        break;
                    }
                    if (cancelledRight == window)
                    {
                        cancelledRight = null;
                        break;
                    }
                    Context(window, window.HitAt(x, y), x, y);
                    break;
                // The keys of a bar in edit mode, which has the focus then.
                case WM.WM_KEYDOWN:
                case WM.WM_SYSKEYDOWN:
                    // §534: Escape, where the bar has the keys, leaves a bar carried where it stood (its timer hears it otherwise).
                    if (window.BarMoving && (nuint)wParam == VK.VK_ESCAPE)
                    {
                        window.CancelBarMove();
                        return (LRESULT)0;
                    }
                    // Bit 30 of lParam: the key was down already, a repeat of a key held down.
                    if (editor is not null && Keyed == window && EditKey(window, (int)(nuint)wParam, ((nint)lParam & (1 << 30)) != 0))
                    {
                        return (LRESULT)0;
                    }
                    // keys: and of a bar the keyboard is on (Win+T, Win+B; Dock.Keys.cs).
                    if (!Arranging(window) && KeysKey(window, (uint)(nuint)wParam))
                    {
                        return (LRESULT)0;
                    }
                    return null;
                // app clicks: Esc, taken while a click or the keys hold the window list up (Dock.AppClicks.cs).
                case WM.WM_HOTKEY when (nint)wParam == EscapeHotkey:
                    PreviewEscape();
                    break;
                // keys: the keys went elsewhere, and the keyboard leaves the bar (Dock.Keys.cs).
                case WM.WM_ACTIVATE:
                    if (((nuint)wParam & 0xFFFF) == WA_INACTIVE)
                    {
                        KeysDeactivated(window, (HWND)(nint)lParam);
                    }
                    return null;
                case WM.WM_TIMER:
                    window.Timer((uint)(nuint)wParam);
                    break;
                case WM.WM_DPICHANGED:
                    // Edit mode ends with a change of its display's scale.
                    if (Arranging(window))
                    {
                        EndEdit(rebuild: false);
                    }
                    RequestArrange(Stir.Scale);
                    break;
                case WM_KARPO_APPBAR:
                    window.Appbar((uint)(nuint)wParam, (nint)lParam != 0);
                    break;
                // The drop target decides the effect under the cursor itself
                // and does not read the reply.
                case DropTarget.WM_KARPO_DRAG:
                    window.Dragged(DropTarget.FromLParam(lParam));
                    break;
                default:
                    return null;
            }
        }
        catch (Exception error)
        {
            Report(error);
        }
        return (LRESULT)0;
    }

    private void HostTimer(uint id)
    {
        switch (id)
        {
            case LAUNCH:
                Kill(LAUNCH);
                if (launching is { } launched)
                {
                    launching = null;
                    launched();
                }
                break;
            case CLOCK:
                TickClock();
                break;
            case ARRANGE:
                Arrange();
                break;
            case REBUILD:
                Kill(REBUILD);
                Rebuild();
                break;
            case DESKTOP_RESTORE:
                // The corner's own press keeps the bars up whatever stands where; Show desktop from anywhere
                // else (§714) only where the desktop stands over a bar.
                if (Environment.TickCount64 < desktopRevealUntil)
                {
                    foreach (var bar in windows) bar.KeepAboveDesktop();
                }
                else
                {
                    BarsOverDesktop();
                }
                if (Environment.TickCount64 >= Math.Max(desktopRevealUntil, desktopWatchUntil)) Kill(DESKTOP_RESTORE);
                break;
            // appbar: the one look more after a reconcile that sent something, or a disturbance (BarKeeper).
            case RECONCILE:
                Kill(RECONCILE);
                var settling = settleStir;
                settleStir = Stir.Settle;
                Reconcile(settling);
                break;
            case DESKTOPS:
                Kill(DESKTOPS);
                CheckDesktops();
                break;
            // The desktop buttons' apps, read again after window events.
            case DESKTOP_APPS:
                DesktopAppsDue();
                break;
            // signals: a button asking for attention blinks, or its flashing may have stopped (Dock.AppClicks.cs).
            case ATTENTION:
                AttentionDue();
                break;
            // Windows read once more after one came (§204).
            case APPS_AGAIN:
                Kill(APPS_AGAIN);
                if (apps.Scan() == Change.Set)
                {
                    WindowsChanged(Change.Set);
                }
                break;
            case ISLANDS:
                TickIslands();
                break;
            // The keyboard layout, read once more after an activation.
            case KEYBOARD:
                Kill(KEYBOARD);
                KeyboardChanged(again: false);
                break;
            // calendar: a length stepped on a countdown has settled and is written.
            case COUNTDOWN_SAVE:
                CountdownSettled();
                break;
            // alarm: something of an alarm's is due, or a minute has gone (Dock.Alarms.cs).
            case ALARM:
                AlarmsDue();
                break;
            // quakes: a minute has gone; the earthquakes' store says whether a fetch is due (Dock.Quakes.cs).
            // network: the addresses settled (Dock.Network.cs).
            case NETWORK:
                NetworkSettled();
                break;
            case QUAKES:
                QuakesDue();
                break;
            // pins: Windows' pins, read once their writes settled.
            case WINDOWS_PINS:
                WindowsPinsDue();
                break;
            // clipboard: the window a chosen item goes to, read again until its focus settles (Dock.Clipboard.cs).
            case PASTE:
                PasteDue();
                break;
            // edit: the selected widget's flyout, once a drag's landing is applied (Dock.FlyoutPreview.cs).
            case SELECTED_FLYOUT:
                Kill(SELECTED_FLYOUT);
                SyncSelectedFlyout(selectionChanged: false);
                break;
            // A change config.json did not take, tried again (Dock.KeepConfig.cs).
            case CONFIG_SAVE:
                Kill(CONFIG_SAVE);
                SaveConfigAgain();
                break;
            // config.json read again while another program holds it (Dock.KeepConfig.cs).
            case CONFIG_READ:
                Kill(CONFIG_READ);
                ConfigChanged();
                break;
            // §703: Windows' panels heard moving are still: moved off the bars now.
            case POPUPS_SETTLE:
                Kill(POPUPS_SETTLE);
                shellPopups.Settled();
                break;

        }
    }

    private void Arm(uint id, uint ms) => SetTimer(host, id, ms, null);

    private void Kill(uint id) => KillTimer(host, id);

    // --- Displays ----------------------------------------------------------

    /// <summary>
    /// Arranges once the messages already queued are handled: a timer message
    /// is only generated when the queue is empty. <paramref name="stir"/>: what the arrange's
    /// reconcile was asked for by (BarKeeper); a disturbance is never lost to a later consequence.
    /// </summary>
    private void RequestArrange(Stir stir = Stir.Build)
    {
        Stirred(stir);
        // A change to answer: at full speed until it is done (EcoQoS again as the arrange ends, Rest). Idle, the dock
        // runs slowly on purpose; a change of displays answered slowly is a bar missing for seconds on a busy machine.
        Efficiency(false);
        Arm(ARRANGE, 0);
    }

    /// <summary>appbar: <paramref name="stir"/> kept for the next arrange's reconcile, unless a disturbance is kept already.</summary>
    private void Stirred(Stir stir)
    {
        if (arrangeStir is not { } kept || (stir.Disturbs() && !kept.Disturbs()))
        {
            arrangeStir = stir;
        }
    }

    /// <summary>Rebuilds once, however many changes come in a row, for the same reason.</summary>
    private void RequestRebuild() => Arm(REBUILD, 0);

    /// <summary>
    /// appbar: the one reconcile (BarKeeper, §272): which displays have a full screen app, the bars
    /// hidden or shown to match, their strips held, Explorer's taskbar hidden or put back — and one
    /// look more <see cref="BarKeeper.SettleMs"/> later where it sent something or something was
    /// disturbed. Then the windows the full screen look depends on are watched (FrontWatch).
    /// </summary>
    private void Reconcile(Stir stir)
    {
        if (disposed)
        {
            return;
        }
        if (keeper.Reconcile(windows, stir, HidesTaskbar, editor is not null))
        {
            Arm(RECONCILE, BarKeeper.SettleMs);
        }
        // stage: a made-up display has no full screen app to watch for (Stage.cs).
        if (!Stage.On)
        {
            frontWatch.Watch(keeper.Watched());
        }
        // appbar: Windows' notifications and panels kept off the bars (§540, §580); never the person's own windows
        // from a dock on a config of its own (a test, --smoke), nor beside a made-up display.
        if (!Stage.On && !Config.IsIsolated)
        {
            shellPopups.Ensure();
        }
    }

    /// <summary>
    /// appbar: a display lit again (GUID_SESSION_DISPLAY_STATUS on), heard as it comes, whatever the
    /// dock is doing: a dark screen can end with no change of displays, no resume and no unlock.
    /// Reconciled once the queue is through (the RECONCILE timer at 0, which a timer message only
    /// comes for then), never in the middle of whatever the dock is doing.
    /// </summary>
    private void DisplayLit()
    {
        settleStir = Stir.DisplayLit;
        Arm(RECONCILE, 0);
    }

    // appbar: what the RECONCILE timer reconciles for: a settle, unless a display lit asked first.
    private Stir settleStir = Stir.Settle;

    /// <summary>
    /// A dock window for every enabled display, each on its own edge, then
    /// everything rebuilt. Windows of displays that stay are kept, so their
    /// hover and hide state carries over.
    /// </summary>
    private void Arrange()
    {
        Kill(ARRANGE);
        Lap("start");
        List<MonitorInfo> monitors;
        try
        {
            monitors = Stage.On ? Stage.Monitors() : Monitors.List();
        }
        catch (Exception error)
        {
            // Without a list, the windows there are stay as they are.
            Report(error);
            Rebuild();
            return;
        }
        // A display seen for the first time shows the layout for new displays (Config.DefaultLayout).
        var added = false;
        foreach (var monitor in monitors.OrderByDescending(monitor => monitor.Primary))
        {
            added |= config.AddDisplay(monitor.Id, monitor.Name);
        }
        if (added)
        {
            SaveConfig();
        }
        // layouts: at least one display shows Karpo (the settings app never offers None on the last);
        // should a file say None for every display attached, the primary one shows its layout all the same.
        var shownOn = monitors.Where(monitor => config.ScreenFor(monitor.Id).Enabled).Select(monitor => monitor.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (shownOn.Count == 0 && (monitors.Find(monitor => monitor.Primary) ?? monitors.FirstOrDefault()) is { } fallback)
        {
            shownOn.Add(fallback.Id);
        }
        arranged = monitors;
        Lap("displays");
        var next = new List<DockWindow>();
        var created = new List<DockWindow>();
        foreach (var monitor in monitors)
        {
            var screen = config.ScreenFor(monitor.Id);
            if (!shownOn.Contains(monitor.Id) || next.Exists(window => window.Id == monitor.Id))
            {
                continue;
            }
            // How each edge shows: as the layout the display shows has it (TaskbarLayout.Edges).
            var edges = config.EdgesOf(monitor.Id);
            // A bar for every edge shown all the time, holding its strip or not, those that hold one
            // first, so the display's first window is its main bar; a display whose every edge comes
            // in on hover has one window all the same, its anchor, which shows nothing.
            DockWindow? bar = null;
            foreach (var edge in StandingEdges(edges))
            {
                var kept = windows.Find(window => window.Id == monitor.Id && window.Place is null && window.Standing == edge);
                if (kept is not null)
                {
                    kept.Relist(monitor, screen);
                    next.Add(kept);
                    bar ??= kept;
                    continue;
                }
                try
                {
                    var made = new DockWindow(this, monitor, screen, standing: edge);
                    next.Add(made);
                    created.Add(made);
                    bar ??= made;
                }
                catch (Exception error)
                {
                    Report(error);
                }
            }
            // places: a window for every place along the display's hover edges that has something in
            // it and can come in; the others have none (HoverPlaces.Wanted) — but while the display is
            // edited every place has one, an empty one with its empty room, and one on an edge another
            // display shares as any other: filled there, it shows only while the display is edited.
            // One that stays keeps its window.
            var filled = FilledOn(monitor.Id);
            bool Filled(Edge edge, BarSection section) => filled(edge, section);
            // edit: edit mode is every display's at once (the person, 2026-10-07: "all displays are in
            // edit mode together, or none").
            var edited = editor is not null;
            // An edge in a look with a band comes in as one bar: its empty places have a window too,
            // with only the band in it (HoverPlaces.Banded).
            bool Banded(Edge edge) => HoverPlaces.Banded(SizedFor(edges, edge).StyleOrDefault, edge, Filled);
            foreach (var place in HoverPlacesOf(monitor, edges, monitors, Filled))
            {
                // layouts: only the places the layout has — none on an edge it has no bar on, a
                // dock's middle alone (ScreenEdges.Has) — in edit mode too.
                if (!edges.Has(place.Edge, place.Section) || !HoverPlaces.Wanted(place, Filled(place.Edge, place.Section), edited, Banded(place.Edge)))
                {
                    continue;
                }
                // places: an empty place whose edge the others use all of (HoverPlaces.Share) has no band to carry.
                if (!edited && place.Run.Length <= 0 && !Filled(place.Edge, place.Section))
                {
                    continue;
                }
                var at = new EdgePlace(place.Edge, place.Section);
                var kept = windows.Find(window => window.Id == monitor.Id && window.Place == at);
                if (kept is not null)
                {
                    kept.Relist(monitor, screen);
                    kept.Hover(place);
                    next.Add(kept);
                    continue;
                }
                try
                {
                    var made = new DockWindow(this, monitor, screen, place);
                    next.Add(made);
                    created.Add(made);
                }
                catch (Exception error)
                {
                    Report(error);
                }
            }
        }
        // edit: edit mode stays on through a build that makes the bar holding it again (§296, the person:
        // "when changing position of the taskbar it ends the edit mode"): another edge, type or way of
        // showing gives the display a new window, which holds it now and comes in edit-dressed (Show).
        if (editor is { } held && EditHolder(held, next.FindAll(window => window.HoldsAppbar), window => window.Id) is { } holder)
        {
            editor = holder;
        }
        // §637: a display that showed a bar a moment ago and shows another now (another edge, another way of showing) sees the
        // old one go and the new one come by the motion, in the same commit; a display gone, or new, at once.
        var stood = windows.Where(window => window.Shown && !window.AutoHides).Select(window => window.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var window in windows)
        {
            if (next.Contains(window))
            {
                continue;
            }
            Forget(window, next);
            if (monitors.Exists(monitor => string.Equals(monitor.Id, window.Id, StringComparison.OrdinalIgnoreCase)))
            {
                window.GoByMotion();
            }
            else
            {
                window.Dispose();
            }
        }
        windows = next;
        Lap("windows");
        // appbar: a full screen app in front keeps every bar of its display out of its way, a new
        // one too, before anything is laid out: after a change of display mode the build lays out no
        // bar on a mode that is the game's (BarKeeper.Look; DockWindow.Build).
        keeper.Look(windows, editor is not null, Stir.Build);
        Lap("keeper");
        // appbar: the takeover waits for the build only where Explorer's taskbar holds a strip to
        // keep in view meanwhile. Auto-hidden — left so by a Karpo that ended with its guard, or by
        // the person's own choice — it holds none, and waiting would leave every display's work area
        // whole through the build (2026-09-27, 21:11: 3.1 s): the bars ask for theirs first.
        // Explorer's taskbar not there yet (at sign-in): its TaskbarCreated takes over.
        var waiting = false;
        firstTakeOver &= HidesTaskbar && taskbar is null;
        if (firstTakeOver && Taskbar.ExplorerTray() != HWND.NULL)
        {
            waiting = Taskbar.HoldsStrip();
            firstTakeOver = false;
            if (!waiting)
            {
                appbarTrace.Write("Explorer's taskbar holds no strip (auto-hidden): the bars ask for theirs before the build, and take over after it");
            }
        }
        holdBack = waiting;
        var stir = arrangeStir ?? Stir.Build;
        arrangeStir = null;
        foreach (var step in ArrangeSteps(waiting))
        {
            switch (step)
            {
                case ArrangeStep.ReserveNew:
                    // appbar: a new bar asks for its space before the tray starts. The tray's first act
                    // is to broadcast TaskbarCreated, which makes every app with an icon hand it over
                    // again — through Karpo's tray and on to Explorer — and a strip asked for in the
                    // thick of that is the one Explorer has most reason to be slow about. The thickness
                    // comes from the settings and the display alone, so the empty layout the window was
                    // made with already has it; the build only sends something if it differs
                    // (AppbarHolder.Hold).
                    foreach (var window in created)
                    {
                        window.Reserve();
                    }
                    break;
                case ArrangeStep.Build:
                    // The islands and services follow what the bars now show; the widgets' data
                    // follows the whole config, shown or not (DataSources).
                    SyncServices();
                    Lap("services");
                    SyncSources();
                    Lap("sources");
                    SyncIslands();
                    Lap("islands");
                    Rebuild();
                    Lap("rebuild");
                    // tray: the size the bars just laid out draws icons at; SyncServices read the
                    // layouts from before (Tray.Resize waits for the bars to settle).
                    tray?.Resize(TrayIconPx());
                    break;
                case ArrangeStep.Reconcile:
                    // appbar: the space each bar holds follows the arrange itself, not only the
                    // rebuild inside it — a display that came or went, a new edge or height, a display
                    // switched off, an Explorer just back — and Explorer's taskbar is hidden or put
                    // back as the settings say: the one reconcile (BarKeeper), straight after the build
                    // and just before the new bars show. At the first takeover every bar asks for its
                    // strip beside Explorer's taskbar before the taskbar lets go of its own.
                    holdBack = false;
                    Reconcile(stir);
                    Lap("reconcile");
                    break;
                case ArrangeStep.Show:
                    foreach (var window in created)
                    {
                        window.Show();
                        if (stood.Contains(window.Id))
                        {
                            window.ComeByMotion();
                        }
                        // places: a place made for edit mode comes in edit-dressed, as the display's others are.
                        if (Arranging(window))
                        {
                            window.BeginEditing();
                        }
                        // places: the demo shows the bars; the places stay where they come in from.
                        if (options.Demo is float at && window.Place is null)
                        {
                            window.Demo(at);
                        }
                    }
                    break;
            }
        }
        UpdateIslands();
        // edit: every display's chrome follows the arrange, now, from the bars as they stand: a display come or
        // gone, another edge or height (§359).
        StirChrome();
        PresentChrome();
        Lap("islands updated, chrome");
        // tray: hiding Explorer's taskbar (the takeover) can put it above the spy just after the tray asked
        // every app for its icons; any still answering would hand them to Explorer alone, so this first
        // look after the asking asks once more (TrayFirst).
        tray?.EnsureFirst();
        // keys: the hook in while the bars are the taskbar, in again after every arrange (Dock.Keys.cs).
        SyncKeys();
        // §357: a folder whose bar was made again is handed on to the window its display shows it on now,
        // a menu over it with it.
        FoldersFollow();
        Lap("keys, folders");
        // §356: a menu handed on to a window made again stays open, its rows as the config has them now.
        if (handedOn)
        {
            handedOn = false;
            if (pending is { } open)
            {
                MenuAgain(open);
            }
        }
        // §637: the widgets gone, let go of now where no bar morphs past them.
        MorphsOver();
        // The change answered: slow again (RequestArrange took it to full speed), unless the pointer is on a bar.
        Rest();
        Lap("rest");
    }

    /// <summary>What an arrange does after it has made the bars, step by step (<see cref="ArrangeSteps"/>).</summary>
    internal enum ArrangeStep
    {
        /// <summary>The new bars ask for their strips.</summary>
        ReserveNew,

        /// <summary>The services, the islands and every bar's scene.</summary>
        Build,

        /// <summary>The one reconcile (BarKeeper): every bar's strip as the build left it, Explorer's taskbar as the settings say.</summary>
        Reconcile,

        /// <summary>The new bars are shown.</summary>
        Show,
    }

    /// <summary>
    /// appbar: the order an arrange ends in, after the bars are made. A new bar asks for its strip
    /// before the build (ReserveNew says why), and the reconcile comes after it and just before the
    /// new bars show. At the first takeover with Explorer's taskbar holding its strip
    /// (<paramref name="waiting"/>), the new bars ask for nothing before the build: Explorer's taskbar
    /// keeps its strip and stays in view through the build, which a large config makes a second and a
    /// half long — it used to be hidden first, leaving that long with no taskbar at all — and the
    /// windows are not short by both strips meanwhile.
    /// </summary>
    internal static ArrangeStep[] ArrangeSteps(bool waiting) => waiting
        ? [ArrangeStep.Build, ArrangeStep.Reconcile, ArrangeStep.Show]
        : [ArrangeStep.ReserveNew, ArrangeStep.Build, ArrangeStep.Reconcile, ArrangeStep.Show];

    /// <summary>
    /// The edges of a display (<paramref name="edges"/>) shown all the time, each a bar of its own:
    /// those that hold their strip first, then those over the windows, each in
    /// <see cref="ScreenEdges.All"/>'s order, so the first is the display's main bar. One null for a
    /// display with none, its anchor.
    /// </summary>
    internal static IReadOnlyList<Edge?> StandingEdges(ScreenEdges edges)
    {
        var standing = edges.Standing
            .OrderBy(edge => edges[edge].Mode == EdgeMode.Reserve ? 0 : 1)
            .Select(edge => (Edge?)edge)
            .ToList();
        return standing.Count > 0 ? standing : [null];
    }

    /// <summary>
    /// What popups opened from a bar stay inside: <paramref name="work"/>, the display's work area,
    /// short of every other bar of the display in sight (<paramref name="bars"/>, each by its edge and
    /// the strip it takes, its panel and the room off its edge). A bar that holds its strip is out of
    /// the work area already; one over the windows is not, and a popup clamped into the work area
    /// would run over it. Never less than nothing.
    /// </summary>
    internal static RECT PopupRoom(RECT work, IEnumerable<(Edge Edge, RECT Takes)> bars)
    {
        foreach (var (edge, takes) in bars)
        {
            switch (edge)
            {
                case Edge.Top:
                    work.top = Math.Max(work.top, takes.bottom);
                    break;
                case Edge.Left:
                    work.left = Math.Max(work.left, takes.right);
                    break;
                case Edge.Right:
                    work.right = Math.Min(work.right, takes.left);
                    break;
                default:
                    work.bottom = Math.Min(work.bottom, takes.top);
                    break;
            }
        }
        work.bottom = Math.Max(work.bottom, work.top);
        work.right = Math.Max(work.right, work.left);
        return work;
    }

    /// <summary>
    /// places: the hover places of <paramref name="monitor"/>'s display (<see cref="HoverPlaces.For"/>):
    /// on its hover edges, each along the whole edge; none on an edge another display touches; and
    /// each edge shared among its places by what each wants, as their windows last measured it
    /// (<see cref="WantsOf"/>).
    /// </summary>
    private IReadOnlyList<HoverPlace> HoverPlacesOf(MonitorInfo monitor, ScreenEdges edges, List<MonitorInfo> monitors, Func<Edge, BarSection, bool> filled)
    {
        var others = monitors.Where(other => other.Id != monitor.Id).Select(other => other.Bounds).ToList();
        return HoverPlaces.For(monitor.Bounds, edges.Standing, filled, others, edge => WantsOf(monitor.Id, edge, monitor.Dpi, filled));
    }

    /// <summary>places: whether a place of display <paramref name="displayId"/> has anything in it, as the config says now.</summary>
    private Func<Edge, BarSection, bool> FilledOn(string displayId)
    {
        var layout = config.ActiveLayout(displayId);
        var screen = config.EdgeOfScreen(displayId);
        return (edge, section) => edge == screen && layout.Islands.Exists(island => island is not null && Sections.Of(island) == section);
    }

    /// <summary>
    /// places: how long each place along <paramref name="edge"/> of display <paramref name="displayId"/>
    /// wants to be at <paramref name="dpi"/> (<see cref="EdgeWants"/>), as its window last laid it out
    /// (<see cref="DockWindow.Wants"/>): nothing for a place with no window and, by
    /// <paramref name="filled"/>, nothing in it; not known for one with something in it and no window
    /// yet, or not laid out at that DPI yet — the edge's thirds until it is.
    /// </summary>
    private EdgeWants WantsOf(string displayId, Edge edge, uint dpi, Func<Edge, BarSection, bool>? filled = null)
    {
        var gap = 0;
        int? Of(BarSection section)
        {
            var at = new EdgePlace(edge, section);
            if (windows.Find(window => window.Id == displayId && window.Place == at) is not { } window)
            {
                return filled is { } holds && !holds(edge, section) ? 0 : null;
            }
            if (window.Wants is not { } wants || wants.Dpi != dpi)
            {
                return null;
            }
            gap = Math.Max(gap, wants.Gap);
            return wants.Length;
        }
        var (start, middle, end) = (Of(BarSection.Start), Of(BarSection.Middle), Of(BarSection.End));
        return new EdgeWants(start, middle, end, gap);
    }

    /// <summary>places: the displays as the last arrange listed them, which the places' inner edges are worked out against.</summary>
    private List<MonitorInfo> arranged = [];

    /// <summary>places: <see cref="Reshare(string, Edge)"/> held while every bar is built, and the edges it was asked for meanwhile.</summary>
    private bool holdReshare;

    private readonly HashSet<(string Id, Edge Edge)> reshareHeld = [];

    /// <summary>places: how deep a share of an edge laying out its places again has gone (a guard: what a place wants never follows its room).</summary>
    private int resharing;

    /// <summary>
    /// places: <paramref name="window"/>, a place, wants another length of its edge than it did
    /// (<see cref="DockWindow.Wants"/>): the edge is shared again, and every place on it that stands
    /// elsewhere now laid out there (<see cref="Reshare(string, Edge)"/>) — at once, or once every bar
    /// is built where a rebuild is under way.
    /// </summary>
    private void Reshare(DockWindow window)
    {
        if (window.Place is not { } at)
        {
            return;
        }
        if (holdReshare)
        {
            _ = reshareHeld.Add((window.Id, at.Edge));
            return;
        }
        Reshare(window.Id, at.Edge);
    }

    /// <summary>
    /// places: <paramref name="edge"/> of display <paramref name="displayId"/> shared again among its
    /// places by what each wants now (<see cref="HoverPlaces.Share"/>), and each place's window laid out again where its stretch
    /// moved. An empty place left with none asks for an arrange, which drops its window.
    /// </summary>
    private void Reshare(string displayId, Edge edge)
    {
        var siblings = windows.Where(window => window.Id == displayId && window.Place?.Edge == edge).ToList();
        if (siblings.Count == 0 || resharing > 3)
        {
            return;
        }
        var monitor = siblings[0].Monitor;
        var filled = FilledOn(displayId);
        var monitors = arranged.Exists(listed => listed.Id == displayId) ? arranged : [monitor];
        var places = HoverPlacesOf(monitor, config.EdgesOf(displayId), monitors, filled);
        resharing++;
        try
        {
            foreach (var sibling in siblings)
            {
                var section = sibling.Place!.Value.Section;
                var found = places.Where(place => place.Edge == edge && place.Section == section).Cast<HoverPlace?>().FirstOrDefault();
                if (found is not { } place || (editor is null && place.Run.Length <= 0 && !filled(edge, section)))
                {
                    RequestArrange();
                    continue;
                }
                sibling.Reshared(place);
            }
        }
        catch (Exception error)
        {
            Report(error);
        }
        finally
        {
            resharing--;
        }
    }

    /// <summary>
    /// places: what a window of a display whose edge is <paramref name="screen"/> shows of
    /// <paramref name="layout"/>: at <paramref name="place"/> on that edge, that section of its bar; a bar
    /// shown all the time on <paramref name="standing"/>, that edge, the whole of it; nothing for an
    /// anchor, which has neither, nor for another edge (DockWindow.Items).
    /// </summary>
    internal static List<BarItem> Shows(TaskbarLayout layout, Edge screen, EdgePlace? place, Edge? standing)
    {
        if (place is { } at)
        {
            return at.Edge == screen ? Sections.Along(layout.Islands).FindAll(island => Sections.Of(island) == at.Section) : [];
        }
        return standing == screen ? Sections.Along(layout.Islands) : [];
    }

    /// <summary>
    /// places: one window a display — its main bar (<see cref="StandingEdges"/>), or its anchor —
    /// for what is done once a display: which display a handle names, which one says its number. Edit
    /// mode's dims and edit bars are every display's, one that shows None too (Dock.EditedDisplays, §326).
    /// </summary>
    private List<DockWindow> Homes => windows.FindAll(window => window.Place is null && BarOf(window.Id) == window);

    /// <summary>places: the main bar, or the anchor, of the display <paramref name="id"/>; null where Karpo has none there.</summary>
    private DockWindow? BarOf(string id) => windows.Find(window => window.Id == id && window.HoldsAppbar);

    /// <summary>
    /// What still points at a window that is going away; §356: an arrange's, the windows standing after it
    /// <paramref name="next"/>, which an open configuration menu is handed on to (<see cref="HandOn"/>).
    /// </summary>
    private void Forget(DockWindow window, IReadOnlyList<DockWindow>? next = null)
    {
        if (previewOwner == window)
        {
            ClosePreview(window);
            previewOwner = null;
        }
        if (pending?.Owner == window && !HandOn(window, next))
        {
            CloseFlyout();
        }
        if (swallowed?.Window == window)
        {
            swallowed = null;
        }
        if (dismissed?.What.Owner == window)
        {
            dismissed = null;
        }
        if (lastWindow == window)
        {
            lastWindow = null;
        }
        if (editKeys == window)
        {
            editKeys = null;
        }
        // Edit mode ends with the last bar: while one is left, the arrange handed it on (EditHolder).
        if (editor == window)
        {
            EndEdit(rebuild: false);
        }
    }

    /// <summary>
    /// edit: edit mode is the dock's, every display's at once, never a window's (§296): who holds it once
    /// the bars are <paramref name="bars"/> (each display's bar or anchor, by its display,
    /// <paramref name="display"/>), <paramref name="held"/> having held it: still that bar where it is one
    /// of them; else its display's bar now — made again for another edge, type or way of showing, from
    /// its menu or the settings app —; else another display's; none where no bar is left.
    /// </summary>
    internal static T? EditHolder<T>(T held, IReadOnlyList<T> bars, Func<T, string> display) where T : class =>
        bars.Contains(held) ? held : bars.FirstOrDefault(bar => display(bar) == display(held)) ?? bars.FirstOrDefault();

    // --- Config and settings -----------------------------------------------

    /// <summary>
    /// Makes <paramref name="next"/> the config: saved first when <paramref name="save"/> (an edit made
    /// here; the settings app's changes merge in, and come back through <see cref="ConfigChanged"/>),
    /// then everything that follows from it.
    /// </summary>
    private void Apply(Config next, bool save = true)
    {
        // §696 (the person, 2026-11-12: "when changing the color it is very laggy ... it is laggy when changing other settings as
        // well"): where a slow change's time goes, in karpo.log.
        var timing = System.Diagnostics.Stopwatch.StartNew();
        var marks = new List<(string Step, double Ms)>();
        (unrecoloured, laying, laps) = (null, default, []);
        lapWatch = timing;
        void Mark(string step) => marks.Add((step, timing.Elapsed.TotalMilliseconds));
        try
        {
            Applying(next, save, Mark);
        }
        finally
        {
            lapWatch = null;
            if (timing.Elapsed.TotalMilliseconds > SlowMs)
            {
                var last = 0.0;
                var steps = string.Join(", ", marks.Select(mark => { var took = mark.Ms - last; last = mark.Ms; return $"{mark.Step} {took:0}"; }));
                var laid = laying.Bars > 0 ? $"; {laying.Bars} bars laid: layouts {laying.Layouts:0} ms, scenes {laying.Scenes:0} ms" : "";
                var why = unrecoloured is { } refused ? $"; not recoloured: {refused}" : "";
                var lapped = 0.0;
                var arranged = laps is { Count: > 0 } each ? "; arrange: " + string.Join(", ", each.Select(lap => { var took = lap.Ms - lapped; lapped = lap.Ms; return $"{lap.Step} {took:0}"; })) : "";
                Troubles.Write($"Slow: a change ({(save ? "kept" : "worn")}) {timing.Elapsed.TotalMilliseconds:0} ms — {steps} ms{laid}{why}{arranged}"); // not-ui: karpo.log
            }
        }
    }

    /// <summary>§696: the steps of an arrange during a change being applied, at the change's time (Lap), for karpo.log.</summary>
    private List<(string Step, double Ms)>? laps;

    private System.Diagnostics.Stopwatch? lapWatch;

    /// <summary>§696: an arrange's step done, while a change is timed.</summary>
    private void Lap(string step)
    {
        if (lapWatch is { } watch)
        {
            laps?.Add((step, watch.Elapsed.TotalMilliseconds));
        }
    }

    /// <summary>§696: why the last colours changed were not taken in place (Recolour), for the slow change's line in karpo.log.</summary>
    private string? unrecoloured;

    /// <summary>§696: the bars laid out during the change being applied, and the time their layouts and scenes took.</summary>
    private (int Bars, double Layouts, double Scenes) laying;

    private void Applying(Config next, bool save, Action<string> mark)
    {
        // §622: a change of the person's is made on their config, never on a copy worn a moment (Dock.TryOn.cs); whatever
        // is applied is what the bars show from now on.
        if (save)
        {
            Settle();
        }
        shownStale = false;
        // §520: the edit bar shows its pages as the config has them now, as the frame ends.
        if (panelOn is not null)
        {
            StirChrome();
        }
        // tray: a tray icon chosen for the bar or the overflow in one tray, every other keeping it where
        // it shows it, before the file is written (§347, Dock.Tray.cs).
        var told = TrayChoices(config, next);
        var changed = !Same(next, config);
        // edit: every change made in edit mode is a step of its history, undone from the config as it
        // was (EditHistory.cs, §352); an undo's or a redo's own is not.
        if (save && changed && editor is not null && !stepping)
        {
            history.Record(config);
        }
        mark("prepared");
        if (save && changed)
        {
            // Kept and tried again where the file does not take it (Dock.KeepConfig.cs).
            KeepConfig(next);
        }
        mark("saved");
        var startupChanged = next.StartWithWindows != config.StartWithWindows;
        var before = config;
        config = next;
        // A colour being picked, and nothing else: the bars are recoloured where they stand, with
        // nothing built again (Recolour). A picker sends a colour for every step it is dragged. §691: a colour kept, every bar
        // then checked against what it should show (ShowsItsLook), one that does not built again. (§696: §691 built every bar
        // for every colour kept, which lagged; what stuck was §694's — the settings writing the other mode's colour.)
        var colours = OnlyColorsDiffer(before, next);
        if (colours && Recolour())
        {
            mark("recoloured");
            if (save)
            {
                Shown();
            }
            // §396: and every popup standing open with them: the open folder, the menu the colour was picked in.
            Relooked();
            mark("popups");
            return;
        }
        // pins: a pin no layout holds any more comes off Windows' taskbar too (Dock.WindowsPins.cs).
        ReleaseWindowsPins(before);
        // tray: a tray icon chosen for the bar or the overflow is written into Windows' own record of
        // it as well (Dock.Tray.cs).
        PromoteChoices(told);
        // calendar: a timer or a focus session that is gone takes its countdown with it;
        // one carried elsewhere keeps its id, and them.
        countdowns.Keep(Countdowns.KeysIn(config));
        // alarm: an alarm added, set or switched in Settings, or removed, is the book's at once.
        SyncAlarms();
        // quakes: an earthquake widget added or removed starts or stops the dock's watch (Dock.Quakes.cs).
        SyncQuakes();
        // calendar: and a focus session whose "Turn on do not disturb" changed takes hold of it or lets go.
        focusQuiet?.Want(FocusQuiet.KeysIn(config));
        // calendar: a format someone changed reaches every widget through Nls, before anything is
        // measured or drawn again below.
        Panels.Nls.Apply(config.Formats);
        // online: a new interval reaches every online service at once: the stores ask it each time
        // they decide what is due, the calendars and the mail re-arm their timers (its Changed), and
        // the islands' ticks are read again by Arrange below.
        OnlineUpdates.Shared.Set(config.OnlineUpdateMinutes);
        // links: another map, weather or finance site is the one the next menu opens on (LinkProviders, §322); a menu is made when it opens.
        LinkProviders.Use(config);
        // holidays: turned on or off, another country, region or colour: the calendar follows at once (§237).
        CalendarStore.Shared.Holidays?.Configure(config.Holidays);
        // Another language: S reads its table from now on. A menu is made when it opens, so it
        // speaks it already; the bars are built again by Arrange below (its Rebuild), which re-measures
        // every widget in the new words, as a region format's change does (SettingChanged). §356: an open
        // configuration menu stays, its rows read again in the new words once the bars are built, on the
        // level it stood on (found by its rows' ids); any other open flyout was made in the old words and closes.
        if (before.Language != config.Language && Languages.Apply(config.Language))
        {
            if (pending is not { Again: not null } || !FlyoutOpen)
            {
                CloseFlyout();
            }
        }
        if (startupChanged)
        {
            SyncStartup(config.StartWithWindows);
        }
        ReadTray();
        // pins: the pins the bars show now, resolved once each (PinItems).
        KeepPinItems();
        // No settings window to refresh: the settings app reads the file this
        // just saved. Displays may have been switched on or off, or moved to
        // another edge, so they are listed again, and the islands and services
        // follow what the bars show then. Its reconcile hides Explorer's taskbar or puts it back at
        // once as "Hide the Windows taskbar" says now (BarKeeper).
        // desktops: a layout showing every desktop's windows has them listed first (§362).
        SyncAllDesktops();
        mark("services");
        Stirred(Stir.Settings);
        Arrange();
        mark("bars built");
        if (colours && save)
        {
            Shown();
        }
        // §356: an open configuration menu shows the config as it is now, wherever the change came from
        // (a pick, the file changed, an import), in the new words; its levels found again by their rows (§365).
        if (pending is { } open)
        {
            MenuAgain(open);
        }
        // A switcher's list standing open: its rows come from the config, which has just changed.
        RefreshSwitcher();
        // preview: previews switched on now have their popup ready as well.
        WarmPreview();
        // edit: the selected widget's flyout shows its settings as they are now, from where it stands now.
        if (editor is not null)
        {
            SyncSelectedFlyout(selectionChanged: false);
        }
    }

    /// <summary>
    /// Saves the config as it is; a file that won't save keeps the change, says so (§394) and tries
    /// again (Dock.KeepConfig.cs), and the dock goes on. What someone else saved meanwhile is merged
    /// into the file, and comes back through ConfigChanged.
    /// </summary>
    private void SaveConfig() => KeepConfig(Persons);

    /// <summary>
    /// The config folder changed. Only someone else's change to config.json counts: the dock's own
    /// saves, and the other files there, read as unchanged, so nothing is saved back in a loop.
    /// </summary>
    private void ConfigChanged()
    {
        // §622: the file read against the person's config, never a copy worn a moment (Dock.TryOn.cs), which a change of the
        // file's takes off; the dock's own files beside it, written as a copy is worn, change nothing.
        var wasUnread = Persons.Unread;
        var reload = Persons.Reload(out var fresh);
        if (reload == ConfigReload.Changed)
        {
            Settle();
        }
        // §394: a file that does not read is an error of the settings' until it reads again.
        if (reload == ConfigReload.Broken)
        {
            SettingsFault(FaultSources.Broken, S.DockFaultBroken);
        }
        else if (reload != ConfigReload.Busy)
        {
            SettingsFine(FaultSources.Broken);
        }
        switch (reload)
        {
            case ConfigReload.Changed:
                // edit: someone else's change is no step of edit mode's, and an undo would take it back (EditHistory.cs).
                history.Clear();
                // pins: only an edit made through Karpo lets go of Windows' pins, never a reload of
                // the file as such, an import or a recovery (Dock.WindowsPins.cs).
                TakeTheirs(fresh);
                // A change of the dock's not saved yet is in the config taken (Config.Reload): tried again now.
                SaveConfigAgain();
                break;
            case ConfigReload.Broken:
                // Probably a hand edit in progress: the bars keep what they show.
                Console.Error.WriteLine("Karpo: config.json doesn't read; keeping the settings from before");
                break;
        }
        // A file another program holds is read again soon, not when it next changes (Dock.KeepConfig.cs);
        // the defaults that stood in for one held at the start are no error of the settings' once it read.
        ReadConfigSoon(reload);
        if (wasUnread && !Persons.Unread)
        {
            SettingsFine(FaultSources.Recovered);
            SaveConfigAgain();
        }
    }

    /// <summary>How long a start at sign-in waits for Explorer's taskbar.</summary>
    private const int StartupWaitMs = 20_000;

    /// <summary>How often a start at sign-in looks for Explorer's taskbar meanwhile.</summary>
    private const int StartupLookMs = 50;

    /// <summary>
    /// "Start with Windows": the Run entry follows the setting, for this copy of Karpo, and the logon
    /// task that starts it sooner, off this thread (schtasks takes a while; StartupTask.cs).
    /// </summary>
    private static void SyncStartup(bool enabled)
    {
        // §710: the Store's copy has its package's startup task instead, and may keep neither of these.
        if (PackageIdentity.IsPackaged)
        {
            PackagedStartup.Sync(enabled);
            return;
        }
        if (StartupRegistration.SyncThisCopy(enabled) is { } message)
        {
            Console.Error.WriteLine($"Karpo: {message}");
        }
        StartupRegistration.SyncTaskSoon(enabled, line => Console.Error.WriteLine($"Karpo: {line}"));
    }

    // --- Tray, system status and the other buttons -------------------------

    /// <summary>
    /// The size to ask apps for their notification pictures at: the largest any of this dock's
    /// bars draws one, which is that bar's system icons' size (<see cref="Layout.TrayIconPx"/>). The
    /// largest, because one capture serves every display: a bar shrinks a picture it was given
    /// well, and stretching one it was not is what shows.
    /// </summary>
    private int TrayIconPx()
    {
        var wanted = 0f;
        foreach (var window in windows)
        {
            wanted = MathF.Max(wanted, window.Layout.TrayIconSize);
        }
        if (wanted > 0f)
        {
            return (int)MathF.Ceiling(wanted);
        }
        // No bar yet: the config's own number at the dock's own DPI, which is the primary
        // display's. A bar that appears afterwards sets it again from its own layout.
        var dpi = GetDpiForWindow(host);
        return (int)MathF.Ceiling(Layout.TrayIconPx(dpi == 0 ? 96u : dpi, config));
    }

    /// <summary>The tray and the system status run only while their sections are on the dock.</summary>
    private void SyncServices()
    {
        // signals: the tray's window is also where apps find the band their progress and overlays
        // reach Karpo through (Taskband.cs), so a bar of app buttons keeps it too.
        // stage: no tray window of its own, which the person's own dock would take for Explorer's (Stage.cs).
        if (!Stage.On && PlacedIslands().Any(entry => entry.Island.Kind is BarKind.Tray or BarKind.Pin or BarKind.Apps))
        {
            if (tray is null)
            {
                try
                {
                    tray = Tray.Start(host, TrayIconPx());
                    // tray: the order the icons stood in last time, before any of them is read, so
                    // the apps' adds after the announcement don't shuffle the bar (TrayArrivals).
                    tray?.SeedOrder(LastTrayOrder());
                }
                catch (Exception error)
                {
                    Report(error);
                }
                ReadTray();
                AskAppsForSignals();
            }
            else
            {
                // A bar resized, a display came or went, or the DPI changed: apps are asked for
                // their pictures at the new size (Tray.Resize).
                tray.Resize(TrayIconPx());
            }
        }
        else if (tray is not null)
        {
            tray.Dispose();
            tray = null;
            ReadTray();
            AskAppsForSignals();
        }
        _ = SyncBadges();
        // A folder wearing the Wi-Fi, Bluetooth or sound face shows what it reads.
        if (PlacedIslands().Any(entry => entry.Island.Folder is { } folder
            && (folder.Wears(FolderFaces.Wifi) || folder.Wears(FolderFaces.Bluetooth) || folder.Wears(FolderFaces.Sound))))
        {
            if (systemStatus is null)
            {
                try
                {
                    systemStatus = SystemStatus.Start(host);
                }
                catch (Exception error)
                {
                    Report(error);
                }
            }
        }
        else if (systemStatus is not null)
        {
            systemStatus.Dispose();
            systemStatus = null;
        }
    }

    /// <summary>True when what the tray shows changed.</summary>
    string trayCatalogue = "";
    /// <summary>
    /// tray: the identities in the list of icons the dock last wrote for the settings app
    /// (tray-icons.json), in its order, which is the order they came in; none when it can't be read.
    /// </summary>
    private static List<string> LastTrayOrder() =>
        // No list yet, or one being written: the icons come in the order their apps answer.
        [.. (TrayCatalogue.Read(Config.Folder) ?? []).Select(icon => icon.Key)];

    private bool ReadTray()
    {
        var available = tray?.Icons ?? [];
        // tray: the icons for the settings app (TrayCatalogue): each one's name, its app's executable,
        // Windows' record of it — where its picture is kept — and Windows' own choice for it, which
        // is where an icon with no state of its own stands.
        // perf: written when icons come or go, or one gets its first name, or Windows' word on one
        // changes, not for every new tooltip: a live one (Task Manager's CPU use) rewrote the file
        // every second, and each write woke both apps' watches on the folder.
        var catalogueKey = string.Join("\n", available.Select(icon =>
            $"{icon.Identity}\t{(FirstLine(icon.Tip).Length > 0 ? "" : "?")}\t{icon.Path}\t{icon.Record}\t{!icon.Hidden}"));
        if (catalogueKey != trayCatalogue)
        {
            try
            {
                TrayCatalogue.Write(Config.Folder, available.Select(icon =>
                    new TrayCatalogueEntry(icon.Identity, FirstLine(icon.Tip), icon.Path, icon.Record, !icon.Hidden)));
                trayCatalogue = catalogueKey;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
            }
        }
        // tray: Windows' own update icon is the status icons' to show where a bar has them (TrayHeld).
        TellWindowsUpdateIcon(available);
        available = TrayHeld(available);
        // tray: the state per icon — on the bar, in the hidden icon menu, or nowhere — in the order
        // they were dragged into (TrayLayout, Karpo.Core), each placed tray its own widget's (§347).
        var splits = new Dictionary<string, TraySplit>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, island) in PlacedIslands().Where(entry => entry.Island.Kind == BarKind.Tray))
        {
            splits[key] = TraySplit.Of(island.TrayIcons, available);
        }
        if (splits.Count == traySplits.Count && splits.All(split => traySplits.TryGetValue(split.Key, out var was) && split.Value.Same(was)))
        {
            return false;
        }
        traySplits = splits;
        return true;
    }

    private void TrayChanged()
    {
        // tray: Windows' own choice for an icon changed (Settings): it becomes Karpo's, and the
        // config applied reads the tray again and rebuilds what it must.
        if (FollowWindowsChoices())
        {
            return;
        }
        var before = traySplits;
        if (!ReadTray())
        {
            return;
        }
        // perf: new pictures or tooltips of the same icons are painted in place;
        // a rebuild of every bar for each was most of an idle dock's CPU.
        if (before.Count == traySplits.Count && traySplits.All(split => before.TryGetValue(split.Key, out var was)
            && TrayShape.Same(was.Shown, was.Hidden, split.Value.Shown, split.Value.Hidden)))
        {
            foreach (var window in windows)
            {
                window.Scene.SetTray(gfx, key => TrayOf(key).Shown);
            }
            return;
        }
        // Icons change in bursts, an animated one many times a second.
        RequestRebuild();
    }

    private void SystemChanged()
    {
        if (systemStatus is null)
        {
            return;
        }
        var changed = false;
        foreach (var island in islands.Values)
        {
            changed |= GiveStatus(island);
        }
        if (changed)
        {
            RequestRebuild();
        }
    }

    /// <summary>
    /// What SystemStatus reads now, handed to <paramref name="island"/> where it shows it: a folder
    /// wearing the Wi-Fi, Bluetooth or sound face, which paints itself again in place. True only
    /// where that face wants another size, which a face of one glyph never does.
    /// </summary>
    private bool GiveStatus(IIsland island)
    {
        if (systemStatus is null || island is not FolderIsland { ShowsStatus: true } folder)
        {
            return false;
        }
        try
        {
            return folder.SetStatus(systemStatus.State, gfx);
        }
        catch (Exception error)
        {
            Report(error);
            return false;
        }
    }

    // A button down on a tray icon: kept, and nothing sent to its app — the click goes whole as the
    // button comes up on it (TrayClick), and a drag sends nothing (TrayClicks).
    private void TrayPress(DockWindow window, Hit? hit, bool right, float x = 0, float y = 0)
    {
        if (hit is not { } on || TrayIconAt(window, on) is not { } icon)
        {
            trayClicks.Forget();
            return;
        }
        trayClicks.Pressed(icon, right, window.ScreenPoint(new(x, y)), GetTickCount(), GetDoubleClickTime());
        window.Scene.Tooltip(gfx, null, null);
        // The app's hover popup goes before the press, as a tooltip does.
        window.TrayPressed();
        PublishTrayRects(window, on);
        // tray: a left press may become a drag between the bar and the overflow (Dock.Tray.cs).
        if (!right)
        {
            window.TrayGrabbed(icon, window.Layout.PartOf(ChevronOf(window.Layout, on)));
        }
    }

    /// <summary>
    /// Whether a left press on the tray icon <paramref name="key"/> at <paramref name="at"/> (ms) is
    /// the second half of a double click — the same icon's last left press no more than
    /// <paramref name="within"/> ms before — and what the next press is measured against: this one,
    /// or nothing after a double click, so a third press starts a pair of its own. The bar's icons and
    /// the overflow's share the one record (<see cref="TrayClicks"/>), as Windows' taskbar and its
    /// overflow share the one mouse.
    /// </summary>
    internal static bool Doubled(ref (string Key, uint At)? last, string key, uint at, uint within)
    {
        var doubled = last is { } before && before.Key == key && at - before.At <= within;
        last = doubled ? null : (key, at);
        return doubled;
    }

    // The tray's hover messages.
    /// <summary>
    /// What the pointer did over a bar's tray icons (<see cref="TrayHover"/>), sent to their apps
    /// as Explorer sends it. Never in edit mode: the bar being edited reports no icon.
    /// </summary>
    private void SendTrayHover(IReadOnlyList<(TrayIcon Icon, TrayGesture Gesture)> sends)
    {
        if (tray is null)
        {
            return;
        }
        foreach (var (icon, gesture) in sends)
        {
            tray.Hover(icon, gesture);
        }
    }

    private void TrayClick(DockWindow window, Hit hit, bool right)
    {
        if (tray is null || TrayIconAt(window, hit) is not { } icon)
        {
            return;
        }
        window.Scene.Tooltip(gfx, null, null);
        // The whole click, now the button is up on the icon it went down on and did not drag it.
        if (trayClicks.Released(icon.Key, right) is { } click)
        {
            tray.Click(click, window.Hwnd);
        }
    }

    // Where this window draws each tray icon and the overflow
    // button, on screen, for the apps that ask the tray where their icon is: in the tray `at` is
    // in, where the bar holds two — the one the icon was pressed in, so its app's popup or menu
    // opens by the icon clicked.
    private void PublishTrayRects(DockWindow window, Hit? at = null)
    {
        if (tray is null)
        {
            return;
        }
        var layout = window.Layout;
        var part = ChevronOf(layout, at).Part;
        // §347: that tray's own icons.
        var shown = TrayAt(window, at).Shown;
        var rects = new Dictionary<string, RECT>(shown.Count);
        for (var index = 0; index < shown.Count; index++)
        {
            if (TrayScreenRect(layout, Hit.Tray(index).In(part)) is { } rect)
            {
                rects[shown[index].Key] = rect;
            }
        }
        // tray: the overflow's icons where it stands open from this bar, so an app places its popup
        // or its menu by its own icon there, as Windows' overflow lets it; closed, by its button.
        if (flyout is { IsOpen: true } list && pending is { Topic: Topic.Overflow } open && open.Owner == window)
        {
            for (var index = 0; index < open.Hidden.Count; index++)
            {
                if (list.ItemOnScreen(CMD_TRAY + (uint)index) is { } rect)
                {
                    rects[open.Hidden[index].Key] = rect;
                }
            }
        }
        var chevron = ChevronOf(layout, at);
        tray.PublishRects(rects, TrayOverflowOf(window, chevron).Icons.Count > 0 ? TrayScreenRect(layout, chevron) : null);
    }

    private static RECT? TrayScreenRect(Layout layout, Hit hit)
    {
        var r = layout.ItemRect(hit);
        return r.IsEmpty ? null : layout.OnScreen(r);
    }

    /// <summary>
    /// app buttons: "…"'s list — the buttons the bar has no room for (<see cref="Layout.Overflowed"/>),
    /// each as its icon, in the bar's order. Choosing one is clicking its button; right-clicking one
    /// opens its jump list, as on Windows 11's taskbar.
    /// </summary>
    private void AppOverflowFlyout(DockWindow window)
    {
        var items = new List<FlyoutItem>();
        foreach (var (index, tile) in OverflowedTiles(window))
        {
            items.Add(new FlyoutItem(
                CMD_APP_OVERFLOW + (uint)index,
                tile.Label ?? tile.Item.Name,
                tile.Item.Icon((int)window.Layout.Icon) is { } pixels ? new FlyoutIcon.Picture(pixels) : Glyph(GlyphApp)));
        }
        if (items.Count == 0)
        {
            return;
        }
        var what = new Pending(Topic.AppOverflow, window) { Hit = Hit.AppOverflow, Rows = AppOverflowRows(window) };
        ShowFlyout(what, items, FlyoutLayout.Grid, window.Anchor(Hit.AppOverflow));
    }

    /// <summary>
    /// app buttons: what "…"'s rows stand for on <paramref name="window"/> now, by what each is: the
    /// button's place (which its row's id is), its app or pin, its window and its name.
    /// </summary>
    private static List<string> AppOverflowRows(DockWindow window)
    {
        var rows = new List<string>();
        foreach (var (index, tile) in OverflowedTiles(window))
        {
            rows.Add(string.Join('\u001f', index, tile.Item.Target, tile.Key, tile.Window, tile.Label ?? tile.Item.Name));
        }
        return rows;
    }

    /// <summary>
    /// app buttons: the buttons behind "…" on <paramref name="window"/>, by their places, each app or
    /// window once: a bar holding the open apps twice may have no room for a button in either, and
    /// the list is of what there is to open, not of the widgets it was missing from.
    /// </summary>
    private static IEnumerable<(int Index, Tile Tile)> OverflowedTiles(DockWindow window)
    {
        var listed = new HashSet<string>();
        foreach (var index in window.Layout.Overflowed)
        {
            if (window.TileAt(index) is { } tile && listed.Add($"{NumberedAs(tile)}|{tile.Window}"))
            {
                yield return (index, tile);
            }
        }
    }

    /// <summary>tray: what the overflow's rows stand for on <paramref name="window"/> now, by what each is: its icon and its name, and how many are spilled.</summary>
    private List<string> TrayOverflowRows(DockWindow window, Hit? at = null)
    {
        var (icons, spilled) = TrayOverflowOf(window, at);
        return icons.Count == 0 ? [] : [$"{spilled}", .. icons.Select(icon => icon.Identity + '\u001f' + FirstLine(icon.Tip))];
    }

    /// <summary>What an open overflow does when the bar behind it may have changed (<see cref="OverflowAfter"/>).</summary>
    internal enum OverflowFollow
    {
        /// <summary>It shows what the bar has: nothing to do, or a press or a drag in it is under way.</summary>
        Stays,

        /// <summary>Shown again in place with the rows the bar has now.</summary>
        ShowAgain,

        /// <summary>Nothing is left for it to show.</summary>
        Close,
    }

    /// <summary>
    /// "…" or the tray's overflow standing open, which showed <paramref name="shown"/>, and the bar
    /// behind it now asking for <paramref name="now"/>: it follows at once — shown again, or closed
    /// with nothing left — unless a button is down in it or something is being dragged
    /// (<paramref name="held"/>). Its rows are numbered by their place, so a release after it was
    /// shown again would land on whatever took the pressed row's number, and a tray app has already
    /// heard its icon's button go down; it follows as the button comes up.
    /// </summary>
    internal static OverflowFollow OverflowAfter(IReadOnlyList<string> shown, IReadOnlyList<string> now, bool held) =>
        held || now.SequenceEqual(shown, StringComparer.Ordinal) ? OverflowFollow.Stays
        : now.Count == 0 ? OverflowFollow.Close
        : OverflowFollow.ShowAgain;

    /// <summary>
    /// "…" and the tray's overflow follow the bar while they stand open: shown again in place when
    /// the buttons or icons behind them changed, closed when none are left (the person: "it should
    /// always update the layout"). They used to show what they opened with until they closed, and
    /// "…"'s rows, numbered by the buttons' places, then clicked whichever button had come to
    /// stand there. Asked after <paramref name="window"/> is built, and when a button comes up in
    /// one (<see cref="FlyoutMessage"/>). A tray icon being dragged shows the overflow its own way
    /// (Dock.Tray.cs), so nothing follows then.
    /// </summary>
    private void FollowOverflow(DockWindow window)
    {
        if (flyout is not { IsOpen: true } list
            || pending is not { Topic: Topic.Overflow or Topic.AppOverflow } open
            || open.Owner != window)
        {
            return;
        }
        var ofTray = open.Topic == Topic.Overflow;
        switch (OverflowAfter(open.Rows, ofTray ? TrayOverflowRows(window, open.Hit) : AppOverflowRows(window), list.Pressing || trayDrag is not null))
        {
            case OverflowFollow.Close:
                CloseFlyout();
                break;
            case OverflowFollow.ShowAgain when ofTray:
                OverflowFlyout(window, open.Hit);
                break;
            case OverflowFollow.ShowAgain:
                AppOverflowFlyout(window);
                break;
        }
    }

    /// <summary>
    /// tray: the overflow, opened from the chevron of the tray <paramref name="at"/> is in — the one
    /// clicked, where the bar holds two —, else of its first tray.
    /// </summary>
    private void OverflowFlyout(DockWindow window, Hit? at = null)
    {
        var items = new List<FlyoutItem>();
        var chevron = ChevronOf(window.Layout, at);
        // tray: the icons the bar has no room for first, then the hidden ones (TrayOverflowOf).
        var (rows, spilled) = TrayOverflowOf(window, chevron);
        for (var index = 0; index < rows.Count; index++)
        {
            var icon = rows[index];
            var tip = FirstLine(icon.Tip);
            // menus: an app that gave no picture still gets a glyph where its icon goes.
            items.Add(new FlyoutItem(
                CMD_TRAY + (uint)index,
                tip.Length > 0 ? tip : S.DockTrayIconShort,
                icon.Icon is { } pixels ? new FlyoutIcon.Picture(pixels) : Glyph(GlyphApp)));
        }
        // tray: an empty overflow opens only for a drag, to be let go in (Dock.Tray.cs).
        if (items.Count == 0 && trayDrag is null)
        {
            return;
        }
        var what = new Pending(Topic.Overflow, window) { Hit = chevron, Hidden = rows, Spilled = spilled, Rows = TrayOverflowRows(window, chevron) };
        ShowFlyout(what, items, FlyoutLayout.Grid, window.Anchor(chevron));
    }

    /// <summary>
    /// Shell.Application's ToggleDesktop, which is what the taskbar's own
    /// corner does: a second press puts the windows back. Win+D otherwise.
    /// </summary>
    private void ToggleDesktop()
    {
        // peek: a click on the corner ends its peek first, as Windows' does.
        LivePeek.EndDesktop();
        ToggleShellDesktop();
        desktopRevealUntil = Environment.TickCount64 + 600;
        foreach (var bar in windows) bar.KeepAboveDesktop();
        Arm(DESKTOP_RESTORE, 80);
    }

    /// <summary>
    /// §714, the person (2026-11-22): "the taskbar does hide when doing win+d". Show desktop from the keyboard (or
    /// Explorer's own corner, a touchpad swipe) tells nobody; what is heard is the desktop taking the foreground. It
    /// raises the desktop's window over every window, Karpo's always-on-top bars with them (read live: Progman over
    /// both karpo.dock windows in the z-order 30 ms after Win+D, and there until the next window came up). So the
    /// bars are put back over it as the corner's own press does, now and for the 600 ms the shell may take to raise
    /// it, and only where the desktop does stand over a bar: a click on the wallpaper moves nothing.
    /// </summary>
    private void DesktopCameToFront()
    {
        if (disposed || !keeper.DesktopInFront())
        {
            return;
        }
        desktopWatchUntil = Environment.TickCount64 + 600;
        BarsOverDesktop();
        Arm(DESKTOP_RESTORE, 80);
    }

    /// <summary>§714: the bars back over a desktop that stands over one of them (<see cref="BarKeeper.DesktopOver"/>); nothing otherwise.</summary>
    private void BarsOverDesktop()
    {
        if (!keeper.DesktopOver(windows.Select(bar => (nint)bar.Hwnd)))
        {
            return;
        }
        appbarTrace.Write("Show desktop raised the desktop over the bars: they go back over it");
        foreach (var bar in windows) bar.KeepAboveDesktop();
    }
    private static void ToggleShellDesktop()
    {
        var shellClass = new Guid("13709620-C279-11CE-A49E-444553540000");
        IShellDispatch4* shell = null;
        if (CoCreateInstance(&shellClass, null, (uint)CLSCTX.CLSCTX_INPROC_SERVER, __uuidof<IShellDispatch4>(), (void**)&shell).SUCCEEDED)
        {
            var done = shell->ToggleDesktop().SUCCEEDED;
            shell->Release();
            if (done)
            {
                return;
            }
        }
        Chord((ushort)VK.VK_LWIN, VK_D);
    }

    /// <summary>
    /// Presses the keys in order and releases them in reverse, as typed.
    /// Another key goes down while Win is held, so Windows doesn't also open Start.
    /// </summary>
    private static void Chord(params ushort[] keys)
    {
        var inputs = stackalloc INPUT[keys.Length * 2];
        for (var index = 0; index < keys.Length; index++)
        {
            var extended = keys[index] == (ushort)VK.VK_LWIN ? (uint)KEYEVENTF_EXTENDEDKEY : 0;
            inputs[index].type = (uint)INPUT.INPUT_KEYBOARD;
            inputs[index].ki.wVk = keys[index];
            inputs[index].ki.dwFlags = extended;
            var up = keys.Length * 2 - 1 - index;
            inputs[up].type = (uint)INPUT.INPUT_KEYBOARD;
            inputs[up].ki.wVk = keys[index];
            inputs[up].ki.dwFlags = extended | (uint)KEYEVENTF_KEYUP;
        }
        SendInput((uint)(keys.Length * 2), inputs, sizeof(INPUT));
    }

    // --- Clicks and flyouts ------------------------------------------------

    /// <summary>
    /// Windows' own buttons the bar draws, pressed: Start (on display <paramref name="monitor"/>, the
    /// button's, as that display's taskbar button opens it), search (Win+S) and Task view (Win+Tab,
    /// <see cref="Desktops.TaskView"/> — the one seam a folder's Task view card and line press as
    /// well). False for any other hit, which presses nothing.
    /// </summary>
    internal static bool PressWindowsButton(HitKind kind, nint monitor = 0)
    {
        switch (kind)
        {
            case HitKind.Start:
                Desktops.StartMenu(monitor);
                return true;
            case HitKind.Search:
                Chord((ushort)VK.VK_LWIN, VK_S);
                return true;
            case HitKind.TaskView:
                Desktops.TaskView();
                return true;
            default:
                return false;
        }
    }

    /// <summary>search: Windows Search, by what was open as the Search button went down (<see cref="SearchToggle"/>).</summary>
    private readonly SearchToggle searchToggle = new(WindowsPanels.Instance);

    /// <summary>start: Windows' Start, by what was open as the Start button went down, as Search's.</summary>
    private readonly SearchToggle startToggle = new(WindowsPanels.Instance, WindowsPanel.Start);

    /// <summary>
    /// windowsMenus: the notifications, quick settings, the quick link menu and the Widgets board, each by what was open as
    /// its button went down, as Start's (§466, §473).
    /// </summary>
    private readonly WindowsMenus windowsMenus = new(WindowsPanels.Instance);

    /// <summary>
    /// windowsMenus: the button of Windows' own <paramref name="hit"/> is (§466): one of Windows' glyph buttons, or one of
    /// Windows' own widgets that wear Windows' face (§474, <see cref="WindowsFaces"/>), a widget of the bar; null for
    /// anything else — a folder of Karpo's never.
    /// </summary>
    private static BarKind? WindowsButtonAt(DockWindow window, Hit? hit) =>
        WindowsButtonOf(hit, hit is { } at ? window.KindAt(at) : TolerantEnumConverter<BarKind>.Unknown);

    /// <summary>
    /// windowsMenus: the button of Windows' own <paramref name="hit"/> is, where the entry it is in is of
    /// <paramref name="kind"/> (<see cref="WindowsButtonAt"/>): a glyph button's own, or a widget's that wears
    /// Windows' face (§474); null for anything else. Its click opens Windows' menu, and a pointer resting on it nothing.
    /// </summary>
    internal static BarKind? WindowsButtonOf(Hit? hit, BarKind kind) => hit switch
    {
        { Kind: HitKind.WindowsButton, Index: var button } => (BarKind)button,
        { Kind: HitKind.Island } when kind.WearsWindowsFace() => kind,
        _ => null,
    };

    /// <summary>
    /// start: the Start button on <paramref name="window"/> was clicked: Windows' Start opens on that
    /// bar's display (<see cref="Desktops.StartMenu"/>; the person, 2026-10-28: "sometimes when clicking
    /// the start button the start just appears no the wrong screen"), or closes where it was open as
    /// the button went down and still is; a press that closed it already leaves it closed. §601 It hangs
    /// from the button pressed (<see cref="StartButtonOn"/>, ShellPopups) — what it shows is Windows' own
    /// (the person, 2026-10-08: "the Start button just opens Start").
    /// </summary>
    private void StartClicked(DockWindow window)
    {
        switch (startToggle.Clicked())
        {
            case SearchClick.Open:
                startPressed = (window, Environment.TickCount64);
                PressWindowsButton(HitKind.Start, MonitorFromWindow(window.Hwnd, MONITOR.MONITOR_DEFAULTTONEAREST));
                break;
            case SearchClick.Close:
                _ = WindowsPanels.Instance.Dismiss(WindowsPanel.Start);
                break;
        }
    }

    /// <summary>§601: the bar whose Start button opened Start last, and when: Start hangs from that button.</summary>
    private (DockWindow Window, long At)? startPressed;

    /// <summary>
    /// §601: the Start button of Karpo's that Start, opened on display <paramref name="monitor"/>, hangs from: the one
    /// pressed to open it (a few seconds ago, on that display), else the first bar's standing there (the display's main
    /// bar first) that has one — the Windows key's; null where no bar there has a Start button. A bar that slides in is
    /// only the pressed one: hidden, its button is nowhere to hang from.
    /// </summary>
    private StartButton? StartButtonOn(nint monitor)
    {
        if (startPressed is ({ } pressed, var at) && Environment.TickCount64 - at < StartPressMs && windows.Contains(pressed)
            && pressed.Monitor.Handle == monitor && pressed.StartButton() is { } button)
        {
            return button;
        }
        var standing = windows.Where(window => window.Monitor.Handle == monitor && window.Place is null)
            .OrderBy(window => BarOf(window.Id) == window ? 0 : 1);
        foreach (var window in standing)
        {
            if (window.StartButton() is { } found)
            {
                return found;
            }
        }
        return null;
    }

    /// <summary>§601: how long a press of a Start button of Karpo's is what Start, opening, hangs from.</summary>
    private const long StartPressMs = 5_000;

    /// <summary>
    /// search: the Search button was clicked: Windows Search opens (Win+S), or closes where it was
    /// open as the button went down and still is; a press that closed it already leaves it closed.
    /// </summary>
    private void SearchClicked()
    {
        switch (searchToggle.Clicked())
        {
            case SearchClick.Open:
                PressWindowsButton(HitKind.Search);
                break;
            case SearchClick.Close:
                _ = WindowsPanels.Instance.Dismiss(WindowsPanel.Search);
                break;
        }
    }

    private void Click(DockWindow window, Hit hit, bool middle)
    {
        lastWindow = window;
        switch (hit.Kind)
        {
            // pins: a pin or an open app, by the one rule for every app entry (Dock.Pins.cs).
            case HitKind.Tile when window.TileAt(hit.Index) is { } tile:
                TileClicked(window, hit.Index, tile, middle);
                break;
            // start: Windows' Start, opened or closed as Windows' own Start button does.
            case HitKind.Start:
                StartClicked(window);
                break;
            // search: Windows Search open as the button went down closes, as on Windows' taskbar.
            case HitKind.Search:
                SearchClicked();
                break;
            // Task view, Windows' own, pressed through the keys Windows answers to.
            case HitKind.TaskView:
                PressWindowsButton(hit.Kind);
                break;
            case HitKind.WindowsButton:
                OpenWindowsButton(window, hit, (BarKind)hit.Index);
                break;
            case HitKind.Tray:
                TrayClick(window, hit, right: false);
                break;
            case HitKind.TrayOverflow:
                OverflowFlyout(window, hit);
                break;
            case HitKind.AppOverflow:
                AppOverflowFlyout(window);
                break;
            case HitKind.ShowDesktop:
                ToggleDesktop();
                break;
            case HitKind.Island:
                // A middle click a widget takes as its own — the sound icon mutes, as Windows 11's
                // does — and otherwise the click it has always been: §474, one of Windows' own widgets
                // opens Windows' menu as its glyph button would.
                if (middle && window.IslandAt(hit) is IMiddleClickIsland own && own.MiddleClick(hit.Index))
                {
                    break;
                }
                if (WindowsButtonAt(window, hit) is { } windowsOwn)
                {
                    OpenWindowsButton(window, hit, windowsOwn);
                    break;
                }
                IslandClick(window, hit);
                break;
        }
    }

    /// <summary>
    /// One of Windows' buttons after Start, search and task view, or a shortcut to one of Windows'
    /// apps: what Windows' taskbar opens for it (<see cref="WindowsButtons"/>) — its shortcut
    /// pressed, its address opened once the press is on screen, or the touch keyboard's own switch.
    /// A shortcut whose app is not installed says so instead, as a pin to an app that is not here
    /// does (<see cref="NotInstalledFlyout"/>). Where a panel answers for its menu, a second click
    /// closes it (<see cref="WindowsMenus"/>). Windows' own widgets that wear Windows' face come here
    /// as their glyph buttons would (§474).
    /// </summary>
    private void OpenWindowsButton(DockWindow window, Hit hit, BarKind kind)
    {
        if (windowsMenus.Clicked(kind))
        {
            return;
        }
        WindowsButtons.Open(kind, WindowsButtons.InstalledNow, target =>
        {
            switch (target.By)
            {
                case OpenBy.Shortcut:
                    WindowsButtons.Keys(target.Key, target.Held);
                    break;
                case OpenBy.Address:
                    LaunchLater(target.Address);
                    break;
                case OpenBy.TouchKeyboard:
                    _ = WindowsButtons.ToggleTouchKeyboard();
                    break;
                case OpenBy.NotInstalled:
                    NotInstalledFlyout(window, hit, kind);
                    break;
            }
        });
    }

    /// <summary>
    /// A shortcut whose app is not installed, clicked: what is wrong, as a broken pin says it, and
    /// the way to get the app — its page in the Microsoft Store (WidgetAct.GetSnippingTool), the row its own
    /// menu has while it is missing.
    /// </summary>
    private void NotInstalledFlyout(DockWindow window, Hit hit, BarKind kind)
    {
        var (own, acts) = ActRows(ActsFor(kind), widget: null);
        var lines = new List<FlyoutItem> { new(0, WindowsButtons.NotInstalledReason, Enabled: false) };
        // Only what can be done: the Snipping Tool's own rows, greyed while it is missing, are not.
        var able = own.Where(row => row.Enabled).ToList();
        if (able.Count > 0)
        {
            lines.Add(new(0, "", Separator: true));
            lines.AddRange(able);
        }
        var at = window.Layout.ItemRect(hit).IsEmpty ? window.PointerAnchor() : window.Anchor(hit);
        ShowFlyout(new Pending(Topic.Missing, window) { Hit = hit, Acts = acts }, lines, FlyoutLayout.List, at, kind.Title());
    }

    // --- Karpo's settings panels -------------


    /// <summary>
    /// Whether a click at rest on <paramref name="hit"/> of <paramref name="window"/> does anything —
    /// one rule for the click (<see cref="IslandClick"/>) and for its press, which shows only where it
    /// does (DockWindow.Pressed; the person, 2026-10-22: "if a folder does not have any contents to be
    /// viewed / opened it should not have an animation when i click it"; §287). Windows' buttons, the
    /// apps and the tray always do; a widget says (<see cref="CellIsland.ClickDoes"/>): not one this PC
    /// hasn't got (§247), an empty folder (OneFolder.Opens), or brightness's sun.
    /// </summary>
    private bool ClickDoes(DockWindow window, Hit hit) =>
        window.IslandAt(hit) is not CellIsland island || island.ClickDoes(window.Layout.Screen);

    /// <summary>
    /// What an island's click asks for: one of Karpo's own flyouts at the cell (a quick-settings
    /// page, a folder…: <see cref="IslandAction.View"/>), or a flyout of items.
    /// </summary>
    private void IslandClick(DockWindow window, Hit hit)
    {
        // One whose click does nothing is not asked (ClickDoes): greyed, this PC hasn't got its thing
        // (§247); an empty folder; brightness's sun.
        if (window.IslandAt(hit) is not { } island || !ClickDoes(window, hit))
        {
            return;
        }
        var action = island.Click(hit.Index);
        // One table, in Dock.Widgets.cs, keyed by the taskbar entry that was clicked. The widget
        // says its click opens something; which flyout that is, is its entry's.
        if (action.View != IslandView.None && OpenWidgetFlyout(window, hit, island, hit.Index))
        {
            return;
        }
        if (action.Items is not { Count: > 0 } items)
        {
            return;
        }
        var what = new Pending(Topic.Island, window) { Hit = hit, Target = island.Key, Cell = hit.Index, Icons = action.Icons };
        // The list belongs to the widget, not to the cell it was asked for from: under the middle
        // of the whole widget, like every other flyout a widget opens. Icons behind an arrow are
        // the arrow's, as the tray's hidden ones are its chevron's.
        ShowFlyout(what, items.ToList(), action.Layout, action.Icons ? window.Anchor(hit) : window.WidgetAnchor(hit), action.Title, action.Rows);
    }

    // --- quick: flyouts opened by pointing (a layout's OpenOnHover, off by default) -------------
    //
    // The rules are WidgetHover's, so they can be tested without a bar; this is the dock's side:
    // open the widget's own flyout, and let it go when the pointer is neither on it nor on the
    // panel. Everything here does nothing at all while the setting is off.

    /// <summary>
    /// The pointer rested on <paramref name="hit"/> long enough: that widget's flyout opens, and is
    /// remembered as hover's, so leaving closes it again. A click that came first has already
    /// killed the wait, so this never reopens what a click just closed.
    /// </summary>
    private void OpenHoverFlyout(DockWindow window, Hit hit)
    {
        if (editor is not null || !windows.Contains(window) || window.IslandAt(hit) is not { } island)
        {
            return;
        }
        // §474: Windows' own opens on a click alone, never on a pointer passing by.
        if (WindowsButtonAt(window, hit) is not null)
        {
            return;
        }
        // Q7, §514: whatever the widget opens — a folder, a widget's flyout — opens as its click opens it, by the one switch
        // (WidgetHover.Opens) and the one table (OpenWidgetFlyout); hover's while it stands open.
        if (window.OpensOnHover(hit) && OpenWidgetFlyout(window, hit, island, hit.Index) && FlyoutHost.OpenMenus.Length > 0)
        {
            hoverOpen = (window, hit);
        }
    }

    /// <summary>
    /// The grace after the pointer left ran out: a hover-opened flyout stays only while the pointer
    /// is on its panel or back on the widget it belongs to (<see cref="WidgetHover.Stays"/>). The
    /// panel never tells the bar that the pointer has left it, so while the pointer is on it the
    /// check is made again after the same grace, and only while one is open.
    /// </summary>
    private void HoverFlyoutTick(DockWindow window, bool onBar, Hit? hovered)
    {
        if (hoverOpen is not { } open || open.Window != window)
        {
            return;
        }
        if (!AnyPopupOpen)
        {
            // It went by itself (Escape, something chosen in it): nothing of hover's is open.
            hoverOpen = null;
            return;
        }
        var inside = PointerOnPopup();
        if (WidgetHover.Stays(inside, onBar && hovered is { } on && on == open.Hit))
        {
            if (inside)
            {
                window.ArmHoverClose();
            }
            return;
        }
        CloseHoverFlyout();
    }

    /// <summary>Closes what a hover opened; a flyout a click opened is not this one's to close.</summary>
    private void CloseHoverFlyout()
    {
        if (hoverOpen is null)
        {
            return;
        }
        hoverOpen = null;
        CloseFlyout();
        CloseIslandPopups();
        // Q7: and a folder a rest opened (§334).
        CloseFolder();
    }

    /// <summary>Whether the pointer is on the panel of one of Karpo's open popups.</summary>
    private static bool PointerOnPopup()
    {
        var at = Cursor();
        foreach (var host in FlyoutHost.OpenMenus)
        {
            if (host.Contains(at))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>A popup of the islands' is about to open: the menu, the window list, the tooltip and the other popup go.</summary>
    private void PopupOpening(DockWindow window)
    {
        lastWindow = window;
        // quick: whatever hover had open is going with the rest; the one opening is hover's only
        // if the hover itself opens it, and OpenHoverFlyout says so after this has run.
        hoverOpen = null;
        CloseFlyout();
        ClosePreview(window);
        window.Scene.Tooltip(gfx, null, null);
        CloseIslandPopups();
    }

    /// <summary>
    /// Where along the edge a popup goes: quick: centred on the widget that opened it, wherever it
    /// stands. It slides inward only where it would leave the work area (FlyoutHost.Arrange keeps a
    /// margin), rather than sitting flush with the screen's end as Windows' own calendar does.
    /// </summary>
    private static FlyoutAlign PopupAlign(DockWindow window, Hit hit) => FlyoutAlign.Center;

    // quick: Karpo's quick settings (sound, battery and the rest) and the rename-desktop
    // dialog, one Menu-mode popup made when first shown (QuickSettings.QuickFlyout).
    private QuickSettings.QuickFlyout? quickFlyout;

    /// <summary>What the quick-settings popup was opened from: its bar (auto-hide) and its hit (a second click closes it).</summary>
    private (DockWindow Window, Hit Hit)? quickFrom;

    /// <summary>
    /// The quick-settings page <paramref name="kind"/> at <paramref name="hit"/>; <paramref name="desktop"/>
    /// is the virtual desktop to rename, for <see cref="QuickSettings.QuickKind.RenameDesktop"/>.
    /// </summary>
    private void ShowQuick(DockWindow window, Hit hit, QuickSettings.QuickKind kind, Guid desktop = default)
    {
        PopupOpening(window);
        try
        {
            quickFlyout ??= new QuickSettings.QuickFlyout(gfx, instance, GCHandle.ToIntPtr(cell));
            quickFrom = (window, hit);
            quickFlyout.Show(gfx, window.Look, window.Layout.Scale, window.WidgetAnchor(hit), window.WorkArea(), window.Layout.Edge,
                kind, PopupAlign(window, hit), desktop);
        }
        catch (Exception error)
        {
            quickFrom = null;
            Report(error);
        }
    }

    /// <summary>The quick-settings popup closed: the bar may hide again, and a link chosen in it opens.</summary>
    private void QuickClosed(QuickSettings.QuickFlyoutEvent happened)
    {
        var from = quickFrom;
        quickFrom = null;
        if (from is { } opened)
        {
            // A flyout, which a second click on its island closes (PressedOn reads this).
            if (happened.Action == QuickSettings.QuickFlyoutAction.Dismissed)
            {
                dismissed = (new Pending(OpenedByClick, opened.Window) { Hit = opened.Hit }, GetTickCount());
            }
            if (!disposed && windows.Contains(opened.Window))
            {
                opened.Window.FlyoutClosed();
            }
        }
        if (happened is { Action: QuickSettings.QuickFlyoutAction.Launch, Uri: { Length: > 0 } uri })
        {
            LaunchLater(uri);
        }
        else if (happened.Action == QuickSettings.QuickFlyoutAction.Power)
        {
            // The power widget's rows: the very code the quick links' power lines run.
            OpenQuickLink(happened.Link);
        }
    }

    /// <summary>Whether <paramref name="hwnd"/> is one of the islands' popups, whose own posts are kept through a modal loop.</summary>
    private bool IslandPopupOwns(HWND hwnd) =>
        (quickFlyout is { } quick && quick.Hwnd == hwnd) // quick
        || FolderOwns(hwnd); // folders

    /// <summary>An islands' popup opened from <paramref name="window"/> is open: an auto-hiding bar stays up.</summary>
    private bool IslandPopupOpenFor(DockWindow window) =>
        (quickFlyout is { IsOpen: true } && quickFrom?.Window == window) // quick
        || FolderOpenFor(window); // folders

    /// <summary>Closes the islands' popups that are open, as another popup or a panel opens (or, §491, something is launched: <paramref name="closer"/>).</summary>
    private void CloseIslandPopups(FolderCloser closer = FolderCloser.OtherPopup)
    {
        if (quickFlyout is { IsOpen: true } quick) // quick
        {
            quick.Hide();
            QuickClosed(default);
        }
        CloseFolder(closer); // folders
    }

    /// <summary>The islands' popups go with the dock: their windows point at it.</summary>
    private void DisposeIslandPopups()
    {
        // quick: the quick-settings popup, and its page's service.
        quickFrom = null;
        var quick = quickFlyout;
        quickFlyout = null;
        quick?.Dispose();
        DisposeCountdowns(); // calendar
        DisposeQuakes(); // quakes
        DisposeNetwork(); // network
        DisposeFolder(); // folders
        // drives: nothing read about a drive outlives the dock, its pictures included.
        Drives.DriveIcons.Forget();
        pasting = null; // clipboard
    }

    /// <summary>
    /// Composition commits once the handler returns to the loop, and a shell
    /// launch can block for a moment. A timer message is only generated once
    /// the queue is empty, so the press feedback is on screen before the
    /// launch starts. §491: it goes through the one launcher, which closes what of Karpo's is open first.
    /// </summary>
    private void LaunchLater(string launched, bool elevated = false)
    {
        if (elevated)
        {
            // app clicks: Ctrl+Shift, as administrator (Launch.Elevated).
            Later(() => Launch.Elevated(launched));
        }
        else
        {
            Later(() => Launch.Open(launched));
        }
    }

    /// <summary>app clicks: <paramref name="work"/> — a start, files opened with an app — once the queue is empty, as <see cref="LaunchLater"/>.</summary>
    private void Later(Action work)
    {
        launching = work;
        Arm(LAUNCH, 0);
    }

    /// <summary>
    /// A button went down on a bar (<paramref name="hit"/> what it is on): every popup a click
    /// outside it closes goes now, as it would for a click anywhere else. The bars never take the
    /// activation, so the popups' own light dismiss can't hear this press, and their mouse hook
    /// leaves it to the bar (<see cref="FlyoutHost.PressesHandledBy"/>). Menu-mode popups (menus,
    /// the calendar, the notification list, quick settings, folders) close through their own
    /// window's handling, as for the hook, so whoever opened them records what they were
    /// (<see cref="dismissed"/>); the window list and the desktop flyout go unless the press is on
    /// their own button, whose click deals with them. True when closing used the press up
    /// (<see cref="LightDismiss.Consumes(PopupRole, bool, PressButton)"/>): it and its release then
    /// do nothing else, so the click that closes a menu, or a flyout on what opened it, never also
    /// launches, opens or drags.
    /// </summary>
    private bool PressedOn(DockWindow window, Hit? hit, PressButton button, POINT screen)
    {
        lastWindow = window;
        swallowed = null;
        // quick: a press settles what is open — the click's rules take over from here. Nothing is
        // left waiting to open by hover, so a click that closes a flyout cannot have it open again
        // under the pointer, and a flyout this click opens is pinned rather than hover's.
        hoverOpen = null;
        window.KillHoverTimers();
        var closed = new List<ClosedPopup>();
        // §430: what it went down on; what its click opens is that thing's, and a popup of that thing's it closes stays closed.
        var (press, byHook) = Pressing(window.Hwnd, hit, button, screen);
        menuClosedBy = null;
        void Closed(Pending? what)
        {
            if (what is not null)
            {
                openers.Shut(what.Opener);
                closed.Add(new ClosedPopup(what.Role, what.Opener is { } by && by.On(press)));
                // edit: a widget's menu on this bar, which a click on that widget only closes (§212).
                if (what is { Topic: Topic.LayoutEditor, Folder: null, Target.Length: > 0 } && what.Owner == window && editor is not null)
                {
                    menuClosedBy = (window, what.Target);
                }
            }
        }
        // §522: the one the popups' hook closed before the press got here (the dock busy as it heard it, IsBar).
        Closed(byHook);
        // A popup that doesn't take the focus (a pin group's members, the media controls) has no hook either.
        if (flyout is { IsOpen: true } open && open.Mode != FlyoutMode.Menu)
        {
            Closed(pending);
            CloseFlyout();
        }
        foreach (var host in FlyoutHost.OpenMenus)
        {
            // edit: a folder standing open while its bar is arranged is not closed by a press on
            // the bar - working on the bar with folders open is the whole point of it. It answers
            // the dismiss by staying open (FlyoutHost.StaysOpen), and the fallback below can't
            // tell that from a window nobody answered for, so it is skipped here instead.
            if (host.StaysOpen)
            {
                continue;
            }
            // edit: nor the selected widget's flyout, which stands while it is the one selected: a
            // press on another widget selects that one, whose own flyout takes its place
            // (Dock.FlyoutPreview.cs).
            if (editor is not null && host.Previewed)
            {
                continue;
            }
            dismissed = null;
            // What the hook would have posted: dismissed, with the focus back where it was.
            _ = DispatchNow(host.Hwnd, FlyoutHost.WM_KARPO_HOST_DISMISS, (WPARAM)(nuint)host.Generation, (LPARAM)(nint)1);
            if (host.IsOpen)
            {
                // One no window procedure of the dock's answers for still goes.
                host.Hide();
            }
            Closed(dismissed?.What);
        }
        dismissed = null;
        if (PreviewShowing && !PreviewShownFor(window, hit))
        {
            ClosePreview(previewOwner);
            closed.Add(new ClosedPopup(PopupRole.Hover, false));
        }
        if (!LightDismiss.Consumes(closed, button))
        {
            return false;
        }
        swallowed = (window, button switch
        {
            PressButton.Middle => (uint)WM.WM_MBUTTONUP,
            PressButton.Right => (uint)WM.WM_RBUTTONUP,
            _ => (uint)WM.WM_LBUTTONUP,
        });
        return true;
    }

    /// <summary>
    /// Whether <paramref name="hwnd"/> is one of the bars and the dock will hear a press on it: the
    /// popups' mouse hook then leaves the press to <see cref="PressedOn"/>. Inside a modal loop the
    /// dock is <see cref="busy"/> and a press on a bar takes the default path, so the hook closes
    /// the popups itself, and its post waits in <see cref="Deferred"/>. §522: a press the dock hears
    /// once it is no longer busy knows what the hook closed for it all the same (<see cref="Pressing"/>):
    /// a click on what opened a popup closes it, never closes and opens it again. edit: an edit bar's
    /// too, which closes them itself as well, knowing which of its buttons' menus a press closed (§343,
    /// DeckPressed).
    /// </summary>
    private bool IsBar(HWND hwnd) => !busy && (windows.Exists(window => window.Hwnd == hwnd) || chromes.Values.Any(chrome => chrome.Deck.Hwnd == hwnd || chrome.Deck.FlyoutHwnd == hwnd));

    /// <summary>Whether a release is the one of a press that closing a popup used up (<see cref="swallowed"/>).</summary>
    private bool Swallowed(DockWindow window, uint up)
    {
        if (swallowed is { } kept && kept.Window == window && kept.Up == up)
        {
            swallowed = null;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Whether the window list or the desktop flyout is open for what <paramref name="hit"/> is
    /// on: its tile, its desktop button, or its pin on a pins island.
    /// </summary>
    private bool PreviewShownFor(DockWindow window, Hit? hit) => previewOwner == window && hit switch
    {
        { Kind: HitKind.Tile, Index: var index } => preview is { IsOpen: true } list && list.Tile == index,
        { Kind: HitKind.Island } on => preview is { IsOpen: true } && previewItem is not null
            && window.Anchor(on) is var at && at.x == previewAnchor.x && at.y == previewAnchor.y,
        _ => false,
    };

    /// <summary>
    /// The key of the part <paramref name="hit"/> landed in and the island the config has for it;
    /// an empty key and no island where it is in no part of this bar.
    /// </summary>
    private (string Key, WidgetItem? Island) WidgetAt(DockWindow window, Hit? hit)
    {
        var layout = window.Layout;
        if (hit is not { } on || layout.PartOf(on) is var part && (part < 0 || part >= layout.Specs.Count))
        {
            return ("", null);
        }
        var key = layout.Specs[part].Key;
        return (key, config.FindIsland(Islands.OwnerOf(key), Islands.IdOf(key)));
    }

    /// <summary>
    /// A right-click on <paramref name="window"/>'s bar at <paramref name="hit"/>. Where it opens which
    /// menu is the person's rule (2026-10-04): "edit taskbar and taskbar settings should only appear
    /// when right clicking on the taskbar area that is not a widget". So the bar's own menu opens on
    /// the bar alone — the room between and around the widgets and the sections, the grip band
    /// (<see cref="PlaceOf"/>) — and a widget's right click is its own actions alone (§253), never
    /// the bar's: nothing that copies, removes or restyles it, which are edit mode's. In edit mode a
    /// widget's right click is its click's (<see cref="EditRightClick"/>, §214), the room's the bar's looks (§290).
    /// </summary>
    private void Context(DockWindow window, Hit? hit, float x, float y)
    {
        lastWindow = window;
        if (Arranging(window))
        {
            EditRightClick(window, hit);
            return;
        }
        var kind = hit?.Kind ?? HitKind.Panel;
        var index = hit?.Index ?? 0;
        switch (kind)
        {
            // The Start button's menu is Windows' quick links (Win+X), drawn by
            // Karpo: with glyphs, and closing like the bar's other menus.
            case HitKind.Start:
                ShowFlyout(
                    new Pending(Topic.Start, window) { Hit = hit },
                    QuickLinks.Items(QuickLinkMachine.Read()),
                    FlyoutLayout.List,
                    window.Anchor(hit ?? Hit.Start));
                return;
            // A tray icon's menu is its app's.
            case HitKind.Tray when hit is { } on:
                TrayClick(window, on, right: true);
                return;
            // pins: an app's jump list, with pinning and unpinning (Dock.Pins.cs); with Shift held,
            // the window's own menu, or its windows' (app clicks, Dock.AppClicks.cs).
            case HitKind.Tile when window.TileAt(index) is { } tile:
                if (AppClicks.MenuFor(KeyState.Down(VK.VK_SHIFT), tile.Open.Count) == AppMenuKind.JumpList)
                {
                    AppMenu(window, index);
                }
                else
                {
                    WindowMenu(window, index, tile);
                }
                return;
        }
        // A widget with rows of its own for the cell right-clicked (IWidgetMenu) — a status icon's
        // page and its way to turn it off — shows them alone, as every widget's menu at rest is
        // its own acts (§253).
        if (hit is { Kind: HitKind.Island } owned && window.IslandAt(owned) is IWidgetMenu own
            && own.MenuRows(owned.Index) is { Count: > 0 } ownRows && WidgetAt(window, owned) is { Island: not null } ownPlaced)
        {
            ShowFlyout(
                new Pending(Topic.WidgetOwn, window) { Hit = hit, Target = ownPlaced.Key, Cell = owned.Index },
                [.. ownRows],
                FlyoutLayout.List,
                window.PointerAnchor());
            return;
        }
        // Every other widget shows its own menu: an island's cells, and the taskbar's own buttons
        // (search, task view, the system icons, the tray's chevron, show desktop). At rest that is
        // the kind's own acts, the floor's where it has none (WidgetMenu, §253) — never the bar's,
        // which is the bar's own place's alone (2026-10-04, reversing F1/G5's fallback).
        if (PlaceOf(hit) == MenuPlace.Widget)
        {
            if (hit is { } on && window.Layout.PartOf(on) is var part and >= 0)
            {
                OptionsFlyout(window, part, window.PointerAnchor(), on);
            }
            return;
        }
        // The bar itself: the room between and around the widgets and the sections, and the grip band
        // (Dock.Menus.cs). This is the one menu "Edit" appears in, and its rows are fixed (Dock.BarMenu.cs).
        ShowBarMenu(window, hit, window.PointerAnchor());
    }

    // --- Edit mode --------------------------------------------------------
    //
    // One bar at a time. Its widgets stay as they are with a block drawn over
    // each (Scene.Edit), and clicks never reach them: a press picks a block up
    // and it follows the pointer at once, into whichever section it is carried,
    // and a gap opens where it would land; a release without moving opens its
    // settings page, a folder itself and its page (EditClicks.cs).
    // A "+" at the end of every section opens the add list for that section's
    // end (Dock.Folders.cs). §537: it begins with its settings panel open. The bar takes the keys: Escape
    // folds the panel, the next ends edit mode, as does its own menu's "Done" and a click on another
    // window (the settings app's window is Karpo's own, and doesn't end it).
    // Every change is saved as it happens, and the settings app follows the file.

    /// <summary>
    /// Puts <paramref name="window"/>'s bar into edit mode; a bar already in it leaves it first. §537: every way in comes
    /// here; §575 the edit bar in use that of <paramref name="panel"/>'s display where given (a way in from outside,
    /// <see cref="OpenEditMenu"/>: a display that shows None has no bar of its own), else the bar's.
    /// </summary>
    private void BeginEdit(DockWindow window, string? panel = null)
    {
        if (!windows.Contains(window))
        {
            return;
        }
        // places: edit mode is the display's bar's (or its anchor's); its places go out of sight.
        if (window.Place is not null && BarOf(window.Id) is { } bar)
        {
            window = bar;
        }
        if (editor is not null)
        {
            // edit: already on, every display: the keys go to this bar.
            editor = window;
            FocusBar(window);
            StirChrome();
            return;
        }
        EndEdit(rebuild: false);
        CloseFlyout();
        ClosePreview(window);
        editor = window;
        editSelection.Clear();
        // edit: one history an edit mode (EditHistory.cs).
        history.Clear();
        // §357: a new edit session: where its folders were left forgotten.
        folderMemories.Clear();
        // §575 (the person, 2026-11-08: "the topbar is not extendible anymore"): nothing open as edit mode begins but a
        // selection's flyout (§537 to §574 its settings panel open); the bar of the display it was begun on the one in use —
        // set before the arrange, so the bar lands with edit mode's dress in its first commit, never a frame later (§359).
        panelOn = panel ?? window.Id;
        panelMemory.Reset();
        // §339: ⚙'s backup words start afresh.
        ForgetBackupSaid();
        // §490: Windows' menus close as edit mode comes in, before its chrome shows — one thing open at a time.
        editReturn = Config.IsIsolated ? GetForegroundWindow() : WindowsPanels.ForEditMode(WindowsPanels.Instance, GetForegroundWindow());
        // appbar: edit mode shows every bar, a full screen app in front or not (BarKeeper.Look).
        keeper.Look(windows, editing: true);
        // places: edit mode is every display's at once (the person, 2026-10-07), where the bars stand
        // (2026-10-08: "edit mode doesn't shrink the screen any more"): each bar, and every place of
        // the layout's bars that come in on hover brought in, the empty ones with their empty room
        // (HoverPlaces.Wanted). The bar it was begun on has the keys.
        foreach (var other in windows)
        {
            other.Withdraw();
            if (Arranging(other))
            {
                other.BeginEditing();
            }
        }
        // The display's places that had no window get one, edit-dressed as it is made (Arrange's
        // Show), and every bar is built again.
        Arrange();
        // edit: every display dimmed behind its bars, its edit bar and its sections' edit bars (Dock.EditDeck.cs):
        // one model a display, in this frame, its windows and its content landing with the bars' edit dress in
        // one commit (§359). The arrange showed them already; here they are, in case it found nothing to arrange.
        // §394: the errors as they are now, on the edit bars from the first frame.
        HearWidgetFaults();
        HearAccountFaults();
        StirChrome();
        PresentChrome();
        FocusBar(window);
        // edit: the keys go to the bar being edited, which the arrows and Tab need; Escape leaves edit
        // mode wherever the keys are (EditFollowHook.cs).
        if (Keyed is { } keyed)
        {
            FocusBar(keyed);
        }
        EditHook().Edit(true);
    }

    /// <summary>
    /// places: a block carried from <paramref name="from"/> in edit mode (<paramref name="carried"/>) is
    /// at <paramref name="at"/> on the screen: the surface nearest it — a place along an edge, or the
    /// bar, of the display it is over (<see cref="DropSurfaces"/>, <see cref="EditDrops.Nearest"/>) — so
    /// a drag anywhere on the screen has somewhere to land (the person, 2026-10-07). Another surface then
    /// shows where it would land (<see cref="DockWindow.Offer"/>), every other one showing nothing; null
    /// where <paramref name="from"/> is the nearest itself. The counterpart of <see cref="FolderOffer"/>:
    /// the window carrying the block holds the mouse, so the others are asked. §519: every display's
    /// surfaces, the block's ghost there at that display's scale (<see cref="Layout.Landing(BarSlot, Layout.Carried)"/>).
    /// </summary>
    private DockWindow? BarOffer(DockWindow from, POINT at, Layout.Carried carried)
    {
        var surfaces = DropSurfaces(from);
        var nearest = NearestSurface(surfaces, at);
        DockWindow? over = nearest > 0 ? surfaces[nearest] : null;
        foreach (var window in surfaces.Skip(1))
        {
            if (window == over)
            {
                window.Offer(at, carried);
            }
            else
            {
                window.NoOffer();
            }
        }
        return over;
    }

    /// <summary>
    /// places: every surface a block carried from <paramref name="from"/> may land on, it first (a tie is
    /// its): every bar and place edit mode is on, of every display (§519).
    /// </summary>
    private List<DockWindow> DropSurfaces(DockWindow from) =>
        [from, .. windows.Where(window => window != from && Arranging(window))];

    /// <summary>
    /// places: which of <paramref name="surfaces"/> a block carried at <paramref name="at"/> goes to: the
    /// nearest of the display it is over (<see cref="EditDrops.Nearest"/>); -1 for none.
    /// </summary>
    private static int NearestSurface(IReadOnlyList<DockWindow> surfaces, POINT at) =>
        EditDrops.Nearest(
            at,
            [.. surfaces.Select(window => window.DropArea() ?? default)],
            [.. surfaces.Select(window => window.DropArea() is not null)],
            [.. surfaces.Select(window => window.ScreenBounds)]);

    /// <summary>
    /// places: whether <paramref name="window"/> is one of the surfaces edit mode is on: every bar,
    /// anchor and place of every display — edit mode is all of them at once, or none (the person,
    /// 2026-10-07).
    /// </summary>
    private bool Arranging(DockWindow window) => editor is not null && windows.Contains(window);

    /// <summary>
    /// pins: a drag that began on a bar <b>outside</b> edit mode has passed the drag threshold, so
    /// edit mode turns on and takes the drag over. True when the bar is now editing; carrying the
    /// block keyed <paramref name="key"/> where it still has one.
    /// <para>
    /// At the threshold, not at the press: a press that turns out to be a click would otherwise
    /// flash the whole edit dress, which is the flicker this ordering exists to prevent. And the
    /// drag is <em>adopted</em> rather than started afresh (<see cref="DockWindow.AdoptDrag"/>): the
    /// person is already holding the widget, and a drag that let go of it as edit mode arrived
    /// would make them start over (the person, 2026-10-08: dragging a widget into edit mode keeps
    /// the drag). Edit mode is edited where the bars stand, so nothing moves under the pointer.
    /// </para>
    /// <para>
    /// It stays on once the drag is over. The person is arranging: the settings app is up, and
    /// "Done" or Escape ends it; leaving on release would be a second flash
    /// of the same dress for someone who is probably about to move another widget.
    /// </para>
    /// </summary>
    private bool BeginEditFromDrag(DockWindow window, string key)
    {
        // places: a drag in a place carries over as one on the bar does: edit mode is every
        // display's, the place's own window with it, which goes on carrying the block.
        if (!windows.Contains(window) || Arranging(window))
        {
            return false;
        }
        BeginEdit(window);
        if (!Arranging(window))
        {
            return false;
        }
        // BeginEdit rebuilt the bars, so the layout the block is measured in is edit mode's own.
        // Without a block left to carry, the widget stays where it stood and the release does nothing.
        _ = window.AdoptDrag(key);
        return true;
    }

    /// <summary>
    /// Ends edit mode: Done, Escape, the session locking, a change of
    /// displays, the bar's display going. <paramref name="rebuild"/> redraws
    /// the bars without it; the caller rebuilds otherwise. <paramref name="keepFolder"/>
    /// (§348, §487: edit mode ended as the person asked, <see cref="LeaveEdit"/>): a folder open stays open, at rest, where
    /// it has something to show (<see cref="OneFolder.Rests"/>), and takes the keys (<see cref="OneFolder.KeysBack"/>);
    /// otherwise — the displays changing, the session locking, the bar going — it closes with edit mode.
    /// </summary>
    private void EndEdit(bool rebuild, bool keepFolder = false)
    {
        if (editor is not { } window)
        {
            return;
        }
        var hadKeys = Keyed?.Hwnd ?? window.Hwnd;
        editor = null;
        editKeys = null;
        // edit: nothing is selected once the bar is no longer arranged, nor carried together, nor
        // being selected by a drag over the dim (Dock.EditSelect.cs); and nothing wears the marching
        // ants (Dock.EditCopy.cs).
        editSelection.Clear();
        copiedKeys.Clear();
        history.Clear();
        // §357: and where its folders were left.
        folderMemories.Clear();
        // §520: and the edit bar's flyout with it; §535 what was typed in an account's form goes with it.
        panelOn = null;
        panelMemory.Reset();
        MenuAccountsClosed();
        // edit: and the selected widget's flyout goes with it (SelectedFlyout.cs).
        SyncSelectedFlyout(selectionChanged: true);
        GroupDone();
        BandCancelled();
        // edit: the carry window goes with edit mode.
        HideCarry();
        // edit: the frames and the edit bars go with it; a widget still following the pointer from
        // the add list was never placed, and goes (Dock.EditFollow.cs). Escape is the window's with
        // the keys again.
        DropFollow();
        followHook?.Edit(false);
        // edit: the dims, the edit bars and the sections' edit bars go, taking their tooltips and their focus
        // with them: clear in the commit the bars come to rest in, out of sight after it (§359).
        StirChrome();
        PresentChrome();
        // folders: the add list's place and the folders being arranged go with edit mode. This is one
        // of the four things that close a folder in edit mode, so it says so.
        EndAdding();
        var resting = keepFolder ? openFolders.Where(open => open.Flyout.IsOpen && OneFolder.Rests(open.Flyout.Folder?.Empty ?? true)).ToList() : [];
        if (resting.Count == 0)
        {
            CloseFolder(FolderCloser.EditEnds);
        }
        if (pending is { Topic: Topic.LayoutEditor or Topic.Confirm or Topic.Library })
        {
            CloseFlyout();
        }
        // places: every surface of every display leaves it; the places go back out of sight, and
        // those with nothing in them lose their windows at the arrange.
        foreach (var other in windows)
        {
            if (other != window)
            {
                other.EndEditing();
            }
        }
        window.EndEditing();
        // The keys go back to the window that had them, if the bar still has them — §487: unless a folder stays open, which
        // takes them below (Rest), as a folder opened at rest has them: handed back, that window took the focus from the
        // folder now at rest, and a folder at rest closes for that.
        var foreground = GetForegroundWindow();
        if (OneFolder.KeysBack(foreground == window.Hwnd || foreground == hadKeys, resting.Count > 0)
            && editReturn != HWND.NULL && editReturn != foreground && IsWindow(editReturn))
        {
            SetForegroundWindow(editReturn);
        }
        editReturn = HWND.NULL;
        // One update: the arrange the places need (those with nothing in them lose their windows),
        // which rebuilds every bar at rest, in this frame, so the dims and the edit bars go in the
        // same plan as the bars come out of their edit dress (§354). It used to be a rebuild here and
        // the arrange's own after it. The caller rebuilds otherwise, and the arrange follows.
        if (rebuild)
        {
            Stirred(Stir.Build);
            Arrange();
        }
        else
        {
            RequestArrange();
        }
        // §348: the folder kept open takes off edit mode's dress where it stands, and the keys, as a
        // folder opened at rest has them.
        foreach (var open in resting)
        {
            open.Flyout.Rest();
        }
    }

    /// <summary>
    /// The keyboard focus to <paramref name="window"/>, a surface being edited, so Tab, the arrows and
    /// Ctrl+arrows act on the widget focused there (<see cref="Keyed"/>): a place as much as a bar. Escape is heard wherever the keys are
    /// (EditFollowHook.cs). The bar never takes it by a click (MA_NOACTIVATE), and the click or the
    /// settings app's message is what allows this.
    /// </summary>
    private void FocusBar(DockWindow window)
    {
        if (editor is null || !Arranging(window))
        {
            return;
        }
        editKeys = window;
        if (GetForegroundWindow() != window.Hwnd)
        {
            SetForegroundWindow(window.Hwnd);
        }
    }

    /// <summary>
    /// The block whose options flyout is open on <paramref name="window"/>, or
    /// was until the press being handled closed it (the flyout's hook closes
    /// it before the dock hears of the press).
    /// </summary>
    private string? OptionsShownFor(DockWindow window) =>
        FlyoutOpenFor(window) && pending is { Topic: Topic.LayoutEditor } open ? open.Target
        : dismissed is { What: { Topic: Topic.LayoutEditor } closed, At: var at } && closed.Owner == window && GetTickCount() - at <= GetDoubleClickTime() ? closed.Target
        // edit: the press itself closed it (PressedOn), before the bar heard of it (§212).
        : menuClosedBy is { } by && by.Window == window ? by.Key
        : null;

    /// <summary>edit: the widget menu the press being handled closed on the bar it went down on (<see cref="PressedOn"/>); null for none.</summary>
    private (DockWindow Window, string Key)? menuClosedBy;

    /// <summary>A small flyout asking <paramref name="question"/>, with the one button that does it.</summary>
    private void Confirm(DockWindow window, POINT anchor, string question, uint command, string action) =>
        ShowFlyout(
            new Pending(Topic.Confirm, window),
            [new(0, question, Enabled: false), new(0, "", Separator: true), new(command, action, Glyph(GlyphEdit))],
            FlyoutLayout.List,
            anchor);

    /// <summary>
    /// A right click on a bar in edit mode (the person, 2026-10-19: "the separate right click menus
    /// in the edit mode should be removed"): on a widget its one menu beside it, a folder's too, which
    /// a left click opens instead (§253) — selecting it alone where it was not selected; on one of
    /// several selected the menu for them all, as Windows' lists keep a selection a right click lands
    /// in (<see cref="WidgetMenu"/>); a click on the widget whose menu stands only closes it, as a
    /// click there does (<see cref="rightClosed"/>). On a bar locked under a folder open arranged (§212) the
    /// folder closes first, as by Escape, and the menu opens at once — the open folder's own face's,
    /// another folder's, any widget's (the person, 2026-10-22: "when i have the folder open and i right
    /// click on the folder or any other folder it should close the folder and open the context menu of
    /// the folder"; §287). On the room round the widgets the bar's own looks, every one the settings
    /// app has for it (<see cref="LooksMenu"/>, §290: "when i right click in edit mode onto the taskbar
    /// all the appearance settings should appear in the context menu"), a locked bar's folder closed
    /// first as for a widget's.
    /// </summary>
    private void EditRightClick(DockWindow window, Hit? hit)
    {
        var closed = rightClosed;
        rightClosed = null;
        var part = hit is { Kind: not (HitKind.Panel or HitKind.ResizeGrip) } on ? window.Layout.PartOf(on) : -1;
        var key = part >= 0 && part < window.Layout.Specs.Count ? window.Layout.Specs[part].Key : null;
        var opens = EditClicks.OnRight(key is not null, string.Equals(closed, key, StringComparison.OrdinalIgnoreCase));
        if (opens == EditRight.None)
        {
            return;
        }
        if (BarLocked)
        {
            EscapeTo(EditEscape.CloseFolder);
        }
        // §520: the bar's own right click opens §575 the Taskbar entry's flyout (the Taskbar ▾ rows and "Look ↗", "Display ↗"
        // until §520), from where its Appearance and Display are a click away.
        if (opens == EditRight.Bar)
        {
            OpenPanel(window.Id, PanelPage.Taskbar);
            return;
        }
        BesideMenu(window, part);
    }

    /// <summary>edit: the widget whose menu a right press on the bar closed (<see cref="OptionsShownFor"/>), whose release then opens nothing.</summary>
    private string? rightClosed;

    /// <summary>
    /// A block's menu (<see cref="WidgetMenu"/>): arranged, its row of "Cut", "Copy" and "Remove" and
    /// its settings; at rest the widget's own acts alone (§253).
    /// </summary>
    private void OptionsFlyout(DockWindow window, int part, POINT anchor, Hit? hit = null, MenuBeside? beside = null)
    {
        var layout = window.Layout;
        if (part < 0 || part >= layout.Specs.Count)
        {
            return;
        }
        WidgetMenu(window, layout.Specs[part].Key, anchor, hit, layout.Specs[part].Kind.Title(), beside: beside);
    }

    /// <summary>
    /// folders: a widget's own menu, wherever the widget is. The key says which widget
    /// ("layout:&lt;id&gt;/&lt;id&gt;" on the bar, "layout:&lt;id&gt;/&lt;folder id&gt;/&lt;id&gt;" in a
    /// folder), and the menu is keyed on that rather than on a part of a bar — a widget in a
    /// folder is no part of a bar, which is why it had no menu at all until this existed. The
    /// rows are the same either way (<see cref="WidgetRows"/>), and so is the topic, so the
    /// folder's menu light-dismisses and behaves in edit mode exactly as the bar's does.
    /// <para>
    /// This is the hook a folder's flyout calls for a right-click on one of its widgets, with the
    /// folder's cell as <paramref name="anchor"/>. Nothing shows for a key the config has no
    /// island for.
    /// </para>
    /// </summary>
    /// <param name="folder">
    /// folders: the open folder the widget is in, which the menu stands over and holds open. At rest
    /// the menu is the widget's kind's own acts there (Dock.WidgetMenus.cs), read from
    /// <paramref name="widget"/> as it is now, and nothing else (§253); while the bar is arranged it is
    /// the rows arranging takes, and its settings, as it is on the bar.
    /// </param>
    /// <param name="widget">The widget's island, where the menu is about one in an open folder.</param>
    /// <param name="beside">edit: the menu a click opened, beside the widget rather than at <paramref name="anchor"/> (<see cref="MenuSides"/>).</param>
    /// <param name="configure">§340: a widget not set up asked for: its settings alone, a configuration menu, at rest too.</param>
    /// <param name="side">
    /// §481: a folder's two menus: its face's (its right click), or its panel's (once it stands open). Every other widget
    /// has one.
    /// </param>
    private void WidgetMenu(DockWindow window, string key, POINT anchor, Hit? hit = null, string? unknown = null, OpenFolder? folder = null, IIsland? widget = null, MenuBeside? beside = null, bool configure = false, FolderSide side = FolderSide.Face)
    {
        var island = config.FindIsland(Islands.OwnerOf(key), Islands.IdOf(key));
        List<FlyoutItem> rows;
        List<WidgetActRow> acts = [];
        List<OptionPick> options = [];
        // §528: the widgets its settings are of: it alone, or with the others selected where it is one of several.
        List<string> keys = [key];
        // menus: the one place that says which menu a widget has (§253): edit mode's, where all that
        // changes the widget is, or at rest its own acts alone (the person, 2026-10-20: "the right
        // click context menu actions that are copy, remove, duplicate etc whatever, or change the
        // appearance of a widget should only appear in edit mode").
        var arranged = Arranging(window);
        if (configure && !arranged)
        {
            // §340: "Set up" at rest: the widget's settings alone, where what it needs is (Dock.Folders.cs).
            rows = OptionMenu.Settings(TargetsOf(config, keys), options, side, pictures: TrayPicture);
            if (rows.Count == 0)
            {
                return;
            }
        }
        else if (arranged)
        {
            // menus: the buttons (Dock.MenuFloor.cs).
            rows = WidgetRowsFor(island, CanDuplicate(config, key));
            // edit: the menu of a widget not selected selects it alone, as Windows' lists do; on one
            // of several selected, they stay so, and its menu acts on them all. A folder's line as a
            // block of the bar (it was the bar's alone once).
            if (!editSelection.Contains(key))
            {
                EditPick(key);
            }
            // menus: the widget's settings, as its page in Settings has them — a line's style, a graph's
            // adapter or disk, graph and pace — and a folder's of its side, its faces or its panel (OptionMenu.cs, §481);
            // §528 on one of several selected, the settings they have in common, set on them all (the settings panel's page).
            keys = [key, .. SelectedHere(ArrangedFolder()).Where(other => !string.Equals(other, key, StringComparison.OrdinalIgnoreCase))];
            rows = [.. rows, .. OptionMenu.Settings(TargetsOf(config, keys), options, side, folder?.Flyout.RoomCells(), TrayPicture)];
            if (rows.Count == 0 && unknown is null)
            {
                return;
            }
        }
        else
        {
            // At rest, its own acts alone (RestMenuRows). Only a widget the file has lost has no menu.
            (rows, acts) = folder is not null
                ? RestMenuRows(island, inFolder: true, widget)
                : RestMenuRows(island, inFolder: false, islands.GetValueOrDefault(key), FacePartsFor(window));
            if (RestMenuFor(MenuPlace.Widget, rows.Count) == RestMenu.None)
            {
                return;
            }
        }
        // edit: a configuration menu is one height (§288) and says what it is of in its top row, its type alone (§314, §323).
        var what = new Pending(Topic.LayoutEditor, window)
        {
            Target = key, Targets = keys.Count > 1 ? keys : [], Hit = hit, Folder = folder, Acts = acts, Beside = beside, Options = options,
            Configuration = arranged || configure,
            TypeTitle = !(arranged || configure) ? null : island is not null ? IslandEdits.Kind(island) : unknown,
        };
        // §356: its settings' rows as the config has them now, on the bar it stands on now (Dock.HandOn).
        if (arranged || configure)
        {
            what.Again = again =>
            {
                var now = config.FindIsland(Islands.OwnerOf(key), Islands.IdOf(key));
                var editing = what.Owner is { } on && Arranging(on);
                var rows = OptionMenu.Settings(TargetsOf(config, what.Keys), again, side,
                    editing && what.Folder is not null ? RoomCells(what) : null, TrayPicture);
                return (rows, now is not null ? IslandEdits.Kind(now) : unknown);
            };
        }
        ShowFlyout(what, rows, FlyoutLayout.List, anchor, spoken: WidgetMenuName(island, unknown));
        // §425: a widget that still needs its one thing — a pin what it opens, a new one added from the list too — has
        // its settings open where that is set (a pin's Target: its kinds), Back to the rest.
        if ((arranged || configure) && island is not null && WidgetSetup.Needs(island) is { } need)
        {
            OpenShownLevel(WidgetSetup.RowOf(need));
        }
    }

    /// <summary>The graphics adapters and the disks this PC has, as the graphs' own samplers list them.</summary>
    private static OptionDevices MenuDevices() => new(AttachedDevices.Adapters().Names, AttachedDevices.Disks());

    /// <summary>
    /// menus: what <paramref name="island"/>'s rows choose from, listed only where a row offers it
    /// (OptionRows, §219): the adapters and the disks; the drives attached; the To Do lists as the
    /// accounts last reported them, never the network, and where they are renamed and deleted (§324); the
    /// notebook the lines read, and the one their lists and notes are made, renamed and deleted in (§228);
    /// the zones the clocks show, which a zone's search offers first (OptionFields,
    /// §219); a tray's icons as it has them now (§261); the views a sports line's competition offers now (§484).
    /// </summary>
    private OptionDevices MenuSources(WidgetItem? island)
    {
        // menus: the zones, places and shares in use, which a search offers before anything is typed (§230).
        var devices = (OptionMenu.NeedsDevices(island) ? MenuDevices() : new OptionDevices()) with
        {
            Zones = OptionFields.ZonesInUse(config),
            Places = OptionFields.PlacesInUse(config),
            Shares = OptionFields.SharesInUse(config),
            // §655: a row's way to a page of Windows' Settings (Windows search's Searching Windows), through the one launcher.
            Open = static address => _ = Launch.Open(address),
            // §304: what the person's widgets hold in each field of text, which the same field offers.
            Words = WordsInUse.Of(config),
            // tray: where each icon shows, a row per live icon (OptionTray, Dock.Tray.cs).
            TrayIcons = island is BarItem { Kind: BarKind.Tray } ? TrayMenuIcons() : null,
            // pins: what "What it opens" offers and how it names a target: the folders, the apps (§292, §380, TargetLooks.cs).
            Targets = island?.Pin is not null ? TargetLooks.Choices(config) : null,
            // §455: what a calendar, a mail or a contacts widget may show from: the accounts' parts as last synced, and a
            // calendar's public holidays.
            Parts = WidgetSources.KindOf(island) is var kind and not AccountShows.None
                ? [.. Karpo.Accounts.SourceParts.Here(kind), .. kind == AccountShows.Calendars ? WidgetSources.HolidayParts(config.Holidays) : []]
                : null,
        };
        var options = island?.Info?.Options;
        // §477: a drive face's own drive is picked among the drives as a drive line's is.
        if ((island as BarItem)?.Folder?.Faces.Select(face => FolderFaces.DriveOf(face.Info)).OfType<WidgetInfo>().ToList() is { Count: > 0 } driveFaces)
        {
            devices = devices with { Drives = DriveTargets.Attached(network: driveFaces.Any(kind => kind.Drives == DriveFilter.Network), space: false) };
        }
        if (options is not (WidgetOptionsKind.Drive or WidgetOptionsKind.Todo or WidgetOptionsKind.Note))
        {
            return devices;
        }
        return devices with
        {
            Drives = options == WidgetOptionsKind.Drive ? DriveTargets.Attached(network: island!.Info!.Drives == DriveFilter.Network, space: false) : null,
            TodoSources = options == WidgetOptionsKind.Todo ? Karpo.Accounts.TodoSources.Here() : null,
            TodoLists = options == WidgetOptionsKind.Todo ? MenuTodoLists.Shared : null,
            Notebook = Notebook.Shared.Current,
            Book = options is WidgetOptionsKind.Todo or WidgetOptionsKind.Note ? Notebook.Shared : null,
        };
    }


    /// <summary>
    /// folders: whether picking <paramref name="id"/> in a widget's menu over a folder hands the
    /// screen to another program (<see cref="FolderAfterMenu"/>). The widget's own acts
    /// (<paramref name="acts"/>) say so themselves (<see cref="HandsAway"/>). Of the rest only what
    /// changes the widget where it stands — copying it, removing it, unpinning it —, closing or
    /// ending its app's windows, and "Add", which adds into the folder, stay in Karpo; the settings app, an app, a jump list's line, a page,
    /// finding a lost pin and "Edit taskbar" all take the screen. A setting is set where it stands
    /// (<paramref name="options"/>, OptionMenu.cs), but Windows' own dialog (a pin's target).
    /// </summary>
    internal static bool PickHandsAway(IReadOnlyList<WidgetActRow> acts, uint id, IReadOnlyList<OptionPick>? options = null) =>
        ActRowOf(acts, id) is { } act ? HandsAway(act.Act)
        : OptionMenu.Holds(id) ? OptionMenu.PickOf(options ?? [], id)?.Kind is OptionPickKind.Browse
        // "Add" adds into the folder, arranged where it stands (AddIntoFolder).
        : id is not (CMD_CUT_ISLAND or CMD_COPY_ISLAND or CMD_REMOVE_ISLAND or CMD_UNPIN or CMD_CLOSE or CMD_END_TASK);

    /// <summary>
    /// folders: what a widget's menu hands Windows (<see cref="Opener"/>), handed over as the rest of
    /// the dock hands it: a target opened after the press has shown (<see cref="LaunchLater"/>), a drive's
    /// verb through the drives' one way to it, Properties and a file's place through the shell.
    /// </summary>
    private void OpenFromMenu(string target, string? verb)
    {
        switch (verb)
        {
            case null:
                LaunchLater(target);
                break;
            case MenuVerbs.Eject:
                DriveWatch.Eject(target);
                break;
            case MenuVerbs.Disconnect:
                DriveWatch.Disconnect(target);
                break;
            case MenuVerbs.Properties:
                Launch.Properties(target);
                break;
            case MenuVerbs.Select:
                Launch.Select(target);
                break;
            case MenuVerbs.Explorer:
                Launch.Explorer(target);
                break;
            // As the bar's Ctrl+Shift+click runs it (Dock.AppClicks.cs): a No at UAC opens nothing.
            case MenuVerbs.RunAs:
                Launch.Elevated(target);
                break;
            // pins: "Copy as path", "Copy link": the clipboard's, owned by a bar's window (a null owner's is refused).
            case MenuVerbs.Copy:
                QuickSettings.Controls.Clipboard.SetText(windows.Count > 0 ? windows[0].Hwnd : HWND.NULL, target);
                break;
            default:
                Launch.Open(target, verb);
                break;
        }
    }

    /// <summary>A choice in edit mode's flyouts; false for one that is none of theirs.</summary>
    private bool EditChosen(Pending what, uint id)
    {
        if (what.Owner is not { } window)
        {
            return false;
        }
        switch (what.Topic)
        {
            case Topic.LayoutEditor:
            // The desktops' menu carries that widget's rows too, and keeps a topic of its own for the
            // light-dismiss rules. Its own commands are not among a widget's, so the ones this
            // doesn't know fall through to them below. A widget's own (Topic.WidgetOwn) is its rows
            // alone, at rest (§253).
            case Topic.Desktops when what.Target.Length > 0:
                return IslandChosen(window, what, id);
        }
        return false;
    }

    /// <summary>
    /// A choice of a widget's menu's row: cutting, copying and removing it.
    /// folders: the path <see cref="Pending.Target"/> carries names a widget on the bar or one in
    /// a folder of it, and every one of these means the same thing in both places — "Remove" takes
    /// the widget out of wherever it is, and "Copy" hands a copy to the pointer in the same place.
    /// </summary>
    private bool IslandChosen(DockWindow window, Pending what, uint id)
    {
        switch (id)
        {
            // edit: "Remove" on one of several selected widgets removes them all, asked first in
            // Windows' own dialog where several go (Dock.EditSelect.cs, RemoveSelected, §253) — as Delete does (SelectionAct).
            case CMD_REMOVE_ISLAND:
                return SelectionAct(EditKeyAct.Remove, window, what.Target);
            // edit mode on, and the widget on the clipboard and out (Dock.EditCopy.cs).
            case CMD_CUT_ISLAND:
                return CutFromMenu(window, what.Target, what.Folder);
            // edit mode on, and a copy following the pointer until a click puts it down (Dock.EditCopy.cs).
            case CMD_COPY_ISLAND:
                return CopyToPointer(window, what.Target, what.Folder);
            default:
                return false;
        }
    }

    /// <summary>
    /// The keys of the bar being edited, by edit mode's one keymap (<see cref="EditKeys"/>, an open
    /// folder's too): Tab and Shift+Tab walk the blocks round, the arrows along the bar to its ends
    /// and with Ctrl move the block, Home and End to the first and last, Delete removes it, Space
    /// does what a click on it does (<see cref="EditActivate"/>), Enter opens its page in front,
    /// Shift+F10 or the menu key open its menu, Ctrl+C, Ctrl+V and Ctrl+A copy, paste and select
    /// all, Escape drops a block being dragged, else closes an open folder, else ends edit mode. True when the key was edit
    /// mode's. A key held down (<paramref name="repeat"/>) does nothing more than its first press
    /// did, but for the arrows.
    /// </summary>
    private bool EditKey(DockWindow window, int key, bool repeat = false)
    {
        var shift = KeyState.Down(VK.VK_SHIFT);
        var control = KeyState.Down(VK.VK_CONTROL);
        var vertical = window.Layout.Vertical;
        // edit: the one keymap, the bar's and a folder's alike (EditKeys.cs).
        var stroke = EditKeys.Of(key, control, shift, repeat);
        // edit: a bar locked under a folder open arranged takes Escape, which closes the folder,
        // Ctrl+V, which pastes into it, and undo and redo, and nothing else of edit mode's (§212, §352).
        if (BarLocked && stroke.Act is not (EditKeyAct.Escape or EditKeyAct.Paste or EditKeyAct.Undo or EditKeyAct.Redo))
        {
            return stroke.Act != EditKeyAct.None;
        }
        switch (stroke.Act)
        {
            case EditKeyAct.Escape:
                // edit: a widget following the pointer may be carried by another surface than the one
                // with the keys: called off wherever it is (Dock.EditFollow.cs).
                EscapeTo(EscapeStep(window.CancelDrag() || DropFollow()));
                return true;
            case EditKeyAct.Next or EditKeyAct.Previous:
                // edit: the bar's blocks and then the edit bars' buttons, as one ring (Dock.EditRail.cs).
                RailTab(window, stroke.Act == EditKeyAct.Previous ? -1 : 1);
                return true;
            case EditKeyAct.Focus or EditKeyAct.Move:
            {
                var step = (stroke.Key, vertical) switch
                {
                    (VK.VK_LEFT, false) or (VK.VK_UP, true) => -1,
                    (VK.VK_RIGHT, false) or (VK.VK_DOWN, true) => 1,
                    _ => 0,
                };
                if (step == 0)
                {
                    return true;
                }
                // edit: an arrow while the rail has the focus walks the rail and stays in it.
                if (RailArrow(step))
                {
                    return true;
                }
                if (stroke.Act == EditKeyAct.Move && window.FocusedKey is { } focused)
                {
                    StepIsland(focused, step);
                }
                else
                {
                    // edit: an arrow stops at the bar's end, as one in a folder stops at its edge; Tab goes round.
                    window.MoveFocus(step, round: false);
                }
                return true;
            }
            // edit: Shift+arrows, the block with the focus a size larger or smaller as its menu sizes it (EditResize.cs, §471).
            case EditKeyAct.Resize:
                if (window.FocusedKey is { } sized)
                {
                    ResizeByKeys(window, sized, stroke.Key, LineHeight.Most);
                }
                return true;
            case EditKeyAct.First or EditKeyAct.Last:
                window.FocusEnd(last: stroke.Act == EditKeyAct.Last);
                return true;
            // edit: Delete, Ctrl+X, Ctrl+C, Ctrl+V, Ctrl+A, Ctrl+Z and Ctrl+Y: the selection's acts (Dock.EditSelect.cs), on
            // every selected widget — but the one the keys walked to where it is not one of them, or the one pressed where
            // none is (EditSelection.KeysOn). Ctrl+Z, Ctrl+Y and Ctrl+Shift+Z here where edit mode's hook does not hear them first.
            case var act when EditKeys.OfTheSelection(act):
                SelectionAct(act, window, editSelection.KeysOn(window.WalkedKey, window.FocusedKey));
                return true;
            case EditKeyAct.Click or EditKeyAct.Open or EditKeyAct.Menu:
                // edit: an edit bar's button with the focus is pressed by the keys as by the pointer.
                if (RailChosen())
                {
                    return true;
                }
                if (window.FocusedPart is var chosen and >= 0)
                {
                    // edit: Space and Enter press the block as a click does — it is selected, its menu
                    // opens beside it (§407), a folder opens; the menu key and Shift+F10 open its menu
                    // beside it, as a right click does.
                    if (stroke.Act == EditKeyAct.Menu)
                    {
                        BesideMenu(window, chosen);
                    }
                    else
                    {
                        EditActivate(window, chosen, null);
                    }
                }
                return true;
            // A key held down that edit mode reads once (EditKeys.Of): taken, so it does nothing else either.
            case EditKeyAct.None when repeat && EditKeys.Of(key, control, shift, repeat: false).Act != EditKeyAct.None:
                return true;
        }
        return false;
    }

    /// <summary>
    /// edit: the widget keyed <paramref name="key"/> is the selected one, alone — the one last
    /// clicked — or none (null): the bar being
    /// arranged and every folder standing open show it at once, the bar's block or the folder's card
    /// or line ringed in the accent. By its key, so it keeps its look through the rebuilds a click or
    /// a save brings. Ctrl+click and Shift+click select more (Dock.EditSelect.cs). §576: its entry's flyout opens, the widget
    /// selected already picked again too (PanelMemory.Picked).
    /// </summary>
    private void EditPick(string? key)
    {
        editSelection.Only(key);
        panelMemory.Picked();
        // places: on every surface of the display being edited.
        ShowSelection();
    }

    /// <summary>
    /// edit: a click on the block of part <paramref name="part"/> of the bar being arranged — or
    /// Space, Enter or Narrator's Invoke on it, where <paramref name="at"/> is null: it is selected (<see cref="EditClicks"/>),
    /// and nothing pops up (§520 decision 3: its settings are the settings panel's Widget page, which the panel shows where it
    /// stands open, following the selection); a folder's face opens its folder arranged too, the panel on its Folder page
    /// (§527), its face closing it again (<see cref="EditClicks.OnLocked"/>). A click whose press closed the block's menu
    /// (<paramref name="closedItsMenu"/>) does nothing. With Ctrl or Shift held it goes into the selection or out of it, or the
    /// run to it is selected, and nothing opens. Nothing while a folder is open arranged: the bar is locked under it
    /// (<see cref="BarLocked"/>).
    /// </summary>
    private void EditActivate(DockWindow window, int part, POINT? at, bool closedItsMenu = false)
    {
        var layout = window.Layout;
        if (part < 0 || part >= layout.Specs.Count || BarLocked)
        {
            return;
        }
        var key = layout.Specs[part].Key;
        var act = EditClicks.OnClick(layout.Specs[part].Kind, closedItsMenu);
        if (act == EditClickAct.None || at is not null && EditClickWith(key))
        {
            return;
        }
        // edit: the block clicked is the selected one now, a folder too ("it should also show the
        // widget as selected").
        EditPick(key);
        if (act == EditClickAct.Open)
        {
            OpenArranged(window, key);
        }
    }

    /// <summary>
    /// edit: the menu of part <paramref name="part"/>'s widget while the bar is arranged, beside its block —
    /// away from the bar's edge where there is the room, else along the bar (<see cref="MenuSides"/>) —
    /// never at the pointer: the one path of a click on it, Space, Enter, Narrator's Invoke (§407), a right
    /// click, the menu key and Shift+F10. Widget ▾ on the edit bar opens the same menu from there (§352).
    /// </summary>
    private void BesideMenu(DockWindow window, int part)
    {
        var beside = new MenuBeside(window.BlockOnScreen(part), MenuSides.Order(onBar: true, window.Layout.Edge, Mirror.On));
        menuComing = true;
        try
        {
            OptionsFlyout(window, part, window.BlockAnchor(part), beside: beside);
        }
        finally
        {
            menuComing = false;
        }
    }

    /// <summary>
    /// edit: a menu is coming for a click, whose widget is selected first: the selection's own flyout
    /// stays shut meanwhile (<see cref="SelectedFlyoutWanted"/>).
    /// </summary>
    private bool menuComing;

    /// <summary>
    /// edit: a widget's menu stands open while the bar is arranged — one a click opened, or a right
    /// click — or one is coming: the selected widget's own flyout stays shut under it, opening again
    /// once a pick from the menu is done (§212).
    /// </summary>
    private bool EditMenuStands => menuComing || editor is not null && FlyoutOpen && pending is { Topic: Topic.LayoutEditor, Hover: false };

    /// <summary>
    /// edit: the folder keyed <paramref name="key"/> on <paramref name="window"/>'s display opened,
    /// arranged in place: a click on its face (§527, <see cref="EditActivate"/>), a line's error picked (§394);
    /// its face closes it again (§252). Nothing where it stands open already.
    /// </summary>
    private void OpenArranged(DockWindow window, string key)
    {
        if (Opened(key) is not null)
        {
            return;
        }
        var bar = windows.Find(each => each.Id == window.Id && Arranging(each) && each.Layout.PartIndex(key) >= 0);
        if (bar is not null && bar.Layout.PartIndex(key) is var part and >= 0 && bar.IslandAt(Hit.Cell(part, 0)) is FolderIsland)
        {
            ShowFolder(bar, Hit.Cell(part, 0));
        }
    }

    /// <summary>The island keyed <paramref name="key"/> one place along the bar (Ctrl+arrow).</summary>
    private void StepIsland(string key, int step)
    {
        var next = config.Clone();
        var list = next.ListFor(Islands.OwnerOf(key));
        var owner = Islands.OwnerOf(key);
        if (list.FirstOrDefault(island => string.Equals(island.Id, Islands.IdOf(key), StringComparison.OrdinalIgnoreCase)) is not { } island
            || IslandEdits.Step(list, island, step) is not { } to)
        {
            return;
        }
        if (IslandEdits.Move(list, island, to))
        {
            Apply(next);
        }
    }

    /// <summary>
    /// The one drop of edit mode: the blocks <paramref name="keys"/> — one, or several carried together —
    /// let go on <paramref name="on"/>, go to <paramref name="to"/> of its list: a place — the bar's, or
    /// one along an edge — and the index in the list without them, as that window worked it out from
    /// its own parts (<see cref="DockWindow.ListSlot"/>). Along their bar, onto another surface of their
    /// display or onto any surface of another display alike (§519, <see cref="IslandEdits.MoveTo"/>): from
    /// one layout's list to another's, the licence asked as an add asks it (<see cref="LicenseAllows"/>),
    /// and what was selected selected still under its new key. The bars are rebuilt even when nothing
    /// moved: the bar's next update glides the block from where it was let go into its slot.
    /// </summary>
    private void MoveIslands(IReadOnlyList<string> keys, DockWindow on, BarSlot to)
    {
        var owner = on.Owner;
        var next = config.Clone();
        var across = keys.Any(key => !ReferenceEquals(config.LayoutOf(Islands.OwnerOf(key)), config.LayoutOf(owner)));
        IslandEdits.MoveTo(next, keys, owner, to);
        if (across && LicenseGate.Widgets(license.Status, config.LayoutOf(owner), next.LayoutOf(owner)) is not null)
        {
            // Refused: everything stays, the bars show it so, and the refusal says why.
            Apply(config.Clone());
            _ = LicenseAllows(next, owner, on);
            return;
        }
        Apply(next);
        var renamed = keys
            .Where(key => !string.Equals(Islands.OwnerOf(key), owner, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToDictionary(key => key, key => Config.IslandKey(owner, Islands.IdOf(key)), StringComparer.OrdinalIgnoreCase);
        if (renamed.Count > 0)
        {
            editSelection.Rename(renamed);
            ShowSelection();
        }
    }

    /// <summary>
    /// An app's jump list, as the taskbar shows it: its categories, then the app itself, pinning or
    /// unpinning, and closing its windows. pins: a pin whose target this PC doesn't have says so
    /// instead, and offers to mend or remove it.
    /// </summary>
    private void AppMenu(DockWindow window, int index)
    {
        if (window.TileAt(index) is not { } tile)
        {
            return;
        }
        if (tile.Broken is not null)
        {
            BrokenFlyout(window, index, tile);
            return;
        }
        AppMenuAt(window, Hit.Tile(index), tile.Item, tile.Open, tile.Key, tile.Pinned);
    }

    /// <summary>
    /// An app's jump list at <paramref name="anchor"/>, for <paramref name="item"/> and its
    /// <paramref name="open"/> windows: a pin (<paramref name="pinKey"/>, which "Unpin" takes off the
    /// bar or out of its folder) or an open app the Apps widget shows.
    /// </summary>
    /// <param name="at">folders: where the menu opens instead of at <paramref name="anchor"/> — the pointer, over a folder.</param>
    /// <param name="folder">folders: the open folder the pin is in, which the menu stands over and holds open.</param>
    private void AppMenuAt(DockWindow window, Hit anchor, Item item, IReadOnlyList<HWND> open, string? pinKey, bool pinned, POINT? at = null, OpenFolder? folder = null)
    {
        // The shell's rows, kept apart from Karpo's: the order they are in is Windows' (Dock.Menus.cs).
        var shell = new List<FlyoutItem>();
        var jumps = new List<JumpEntry>();
        List<JumpCategory> categories = [];
        try
        {
            categories = JumpLists.Read(AumidOf(item, open), ExeOf(item, open), 12, window.Px(16f));
        }
        catch (Exception error)
        {
            Report(error);
        }
        // menus: a line whose own icon didn't come shows the app's (a task,
        // as on the taskbar), a document's glyph (a file) or the app glyph.
        var own = item.Icon(window.Px(16f));
        FlyoutIcon ownIcon = own is null ? Glyph(GlyphApp) : new FlyoutIcon.Picture(own);
        foreach (var category in categories)
        {
            if (category.Entries.Count == 0)
            {
                continue;
            }
            shell.Add(new FlyoutItem(0, category.Title, Header: true));
            foreach (var entry in category.Entries)
            {
                shell.Add(new FlyoutItem(
                    CMD_JUMP + (uint)jumps.Count,
                    entry.Name,
                    entry.Icon is { } pixels ? new FlyoutIcon.Picture(pixels) : entry.IsTask ? ownIcon : Glyph(GlyphFile)));
                jumps.Add(entry);
            }
        }
        // pins: Windows' rows for what a pin opens, on the bar as in a folder (PinRows); none for an open app.
        var (file, acts) = pinned ? PinRows(item) : ([], []);
        // A pin's row of buttons on top while its bar is arranged alone (PinButtons): at rest its menu
        // is Windows' own for an app, "Unpin from taskbar" a row of it.
        var pinsOwn = pinKey is { Length: > 0 } && config.FindIsland(Islands.OwnerOf(pinKey), Islands.IdOf(pinKey)) is not null;
        var arranged = pinsOwn && Arranging(window);
        var buttons = PinButtons(pinsOwn, arranged, arranged && CanDuplicate(config, pinKey!));
        var items = AppRows(shell, item.Name, ownIcon, pinned, open.Count, buttons, window.Layout.Edge, open.Count > 0 && EndTask.Enabled, file, inFolder: folder is not null);
        var what = new Pending(Topic.App, window)
        {
            Hit = anchor,
            Target = item.Target,
            Open = open.ToList(),
            Jumps = jumps,
            Pin = pinKey,
            Folder = folder,
            Acts = acts,
        };
        ShowFlyout(what, items, FlyoutLayout.List, at ?? window.Anchor(anchor));
    }

    /// <summary>
    /// The ID the app's jump list is filed under. An open window's, as the
    /// shell resolves it, comes first: some apps (VS Code, Spotify) set theirs
    /// only while running, so their pin doesn't know it.
    /// </summary>
    private string? AumidOf(Item item, IReadOnlyList<HWND> open)
    {
        foreach (var window in open)
        {
            if (apps.Get(window)?.Aumid is { } found)
            {
                return found;
            }
        }
        return item.Aumid;
    }

    private string? ExeOf(Item item, IReadOnlyList<HWND> open)
    {
        foreach (var window in open)
        {
            if (apps.Get(window) is { Exe.Length: > 0 } win)
            {
                return win.Exe;
            }
        }
        if (item.Target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            return item.Target;
        }
        foreach (var key in item.Keys)
        {
            if (key.StartsWith("exe:", StringComparison.Ordinal))
            {
                return key["exe:".Length..];
            }
        }
        return null;
    }

    private static FlyoutIcon Glyph(string code) => new FlyoutIcon.Glyph(code);

    /// <param name="rows">The room a list of choices keeps (<see cref="Flyout.Rows"/>); null for every other flyout.</param>
    /// <param name="spoken">The name a screen reader reads a menu with no <paramref name="title"/> as (<see cref="Flyout.SpokenName"/>).</param>
    private void ShowFlyout(Pending what, List<FlyoutItem> items, FlyoutLayout kind, POINT anchor, string? title = null, FlyoutList? rows = null, string? spoken = null)
    {
        var window = what.Owner;
        // tray: the overflow a drag opens is shown empty too, somewhere to let the icon go (Dock.Tray.cs).
        if (window is null || items.Count == 0 && !(what.Topic == Topic.Overflow && trayDrag is not null))
        {
            return;
        }
        // §430: what the click being handled went down on opened it; where its press closed that thing's own, it
        // stays closed — one click on an opener closes what it opened, never closes and opens it again.
        what.OpenedBy(openers.Press);
        if (openers.Reopens(what.Opener))
        {
            return;
        }
        ClosePreview(window);
        window.Scene.Tooltip(gfx, null, null);
        // Karpo's own menus are flyouts only, never Win32 menus; one that
        // can't be shown is reported and not replaced.
        try
        {
            var mode = what.Hover ? FlyoutMode.Hover : FlyoutMode.Menu;
            if (flyout is not null && flyout.Mode != mode) { CloseFlyout(); flyout.Dispose(); flyout = null; }
            flyout ??= new Flyout(gfx, instance, GCHandle.ToIntPtr(cell), mode);
            // folders: a menu over a folder uses up the press that closes it, so the folder stays
            // for it and closes for the next (docs\widgets.md §185).
            // edit: not over a folder being arranged, where a press on another line closes the menu
            // and goes on to the line, which is selected with its menu in the one click (§212).
            flyout.UsesUpPresses = what.Folder is { } over && !Arranging(over.Window);
            flyout.AlsoInside = point => what.Hover &&
                (WindowFromPoint(point) == window.Hwnd || preview is { IsOpen: true } shown && WindowFromPoint(point) == shown.Hwnd);
            // An open flyout takes the new contents in place; closing it first
            // would hand the foreground away and straight back.
            var replaced = flyout.IsOpen ? pending : null;
            dismissed = null;
            pending = what;
            if (replaced is not null && replaced.Owner != window)
            {
                FlyoutClosed(replaced);
            }
            // folders: a widget's menu that held its folder open lets it go when another menu takes
            // its place on the same bar, which the line above leaves alone.
            else if (replaced?.Folder is { } held && !ReferenceEquals(held, what.Folder))
            {
                FolderMenuEnded(held);
            }
            // tray: the overflow's icons are the bar's tray icons' size, the size they were captured at
            // (Layout.TrayIconSize), in the bar's own squares, five to a row, and the panel no larger
            // than they are, as Windows 11's overflow is (Flyout.FitsItsIcons).
            // The status icons behind their arrow are the same, at the size they are on the bar
            // (Fit.SystemIcon) — glyphs, not pictures — in the bar's squares (IslandAction.Icons).
            var icons = what.Topic == Topic.Overflow || what.Icons;
            flyout.StripIconSize = what.Topic == Topic.Overflow ? window.Layout.TrayIconSize
                : what.Icons ? Fit.SystemIconFor(window.Layout.CellSide, window.Layout.Scale)
                : window.Layout.Icon;
            flyout.StripCell = icons ? window.Layout.TrayOverflowCell : 0f;
            flyout.GridColumns = icons ? Layout.TrayOverflowColumns : Flyout.DefaultGridColumns;
            flyout.FitsItsIcons = icons;
            flyout.GroupContent = icons || what.Topic == Topic.AppOverflow;
            flyout.GroupShowTitles = false;
            // A hidden tray icon's right click is its app's menu, and a hidden app button's its jump
            // list; a status icon's behind its arrow its own rows.
            flyout.ContextClicks = icons || what.Topic == Topic.AppOverflow;
            // tray: its icons are dragged onto the bar or into another place here (Dock.Tray.cs); opened
            // by a drag from the bar, it has room for the icon too, an empty cell where it is empty.
            flyout.DragItems = what.Topic == Topic.Overflow;
            flyout.SpareTiles = what.Topic == Topic.Overflow && trayDrag is { FromOverflow: false } ? 1 : 0;
            // tray: its icons hear the pointer and the buttons as on the bar (OverflowPointer).
            flyout.TellsPointer = what.Topic == Topic.Overflow;
            // §622: a menu holding a choice of the Appearance tells the row the pointer or the keys rest on, to try it on.
            flyout.TellsHover = what.Options.Exists(pick => pick.Kind == OptionPickKind.Set && LookRows.TriedOn(pick.Key));
            flyout.Rows = rows;
            flyout.SpokenName = spoken;
            flyout.ListWidth = what.ListWidth;
            // menus: a widget's menu one width of its own, whatever it says (§230), its levels opening in
            // place of its rows (Flyout.Inline.cs, §271); every other menu's submenus cascade.
            flyout.Configures = what.Topic == Topic.LayoutEditor;
            // menus: edit mode's menu of a widget one height on every level, every widget's the same; every
            // other menu as tall as its rows (Flyout.Width.cs, §288).
            flyout.Configuration = what.Configuration;
            // edit, §517: the edit bar's menus as tall and as wide as their rows (Flyout.Width.cs).
            flyout.FitsContent = what.FitsContent;
            // edit: and named at the start of its top row by what it is of (Flyout.Primary.cs, §323).
            flyout.TypeTitle = what.TypeTitle;
            // edit: the selection's shortcuts it has no row for go on to what it stands over (KeyFromMenu).
            flyout.PassesKey = PassedFromMenu;
            flyout.BesideWidget = what.Beside;
            // §514: a flyout a click on the bar opens is one as an open folder is: a taskbar flyout's surface, its look ShownIn's.
            flyout.Taskbar = RoleOf(what.Topic) == PopupRole.Flyout;
            flyout.Show(gfx, ShownIn(what, window), what.From?.Scale ?? what.Scale ?? window.UiScale, items, kind, anchor, what.From?.Work ?? what.Work ?? window.WorkArea(), what.Edge ?? window.Layout.Edge, title, what.Gap, what.From?.Box, what.From?.Within);
            // §374: at its top, as every menu opens; one the dock shows again after its own dialog where it stood.
            if (resuming is { } stood)
            {
                resuming = null;
                flyout.Recall(stood);
            }
        }
        catch (Exception error)
        {
            pending = null;
            Report(error);
            FlyoutClosed(what);
        }
    }

    /// <summary>
    /// §514 (the person, 2026-11-05: "open widget flyouts and open folders should be the same setting and not treated
    /// differently"): what <paramref name="what"/>, opened from <paramref name="window"/>, is drawn in. A flyout a click on the
    /// bar opens (<see cref="RoleOf"/>: a widget's list, the tray's and the app buttons' overflow) as an open folder is
    /// (<see cref="DockWindow.FlyoutLook"/>: the layout's Flyouts' outline, its widget's own Flyout outline over it); a menu in
    /// its window's look — §526 but a menu of Karpo's settings, every menu in edit mode (the settings panel's dropdowns,
    /// colours and boxes, the add list, the errors, a widget's or the bar's menu, a confirmation) and a widget's settings asked
    /// for at rest (§340, <see cref="Pending.Configuration"/>): those in Windows' own (<see cref="SettingsLook"/>).
    /// </summary>
    private Look ShownIn(Pending what, DockWindow window) =>
        RoleOf(what.Topic) == PopupRole.Flyout ? window.FlyoutLook
        : editor is not null || what.Configuration ? SettingsLook
        : window.Look;

    private void CloseFlyout()
    {
        var what = pending;
        pending = null;
        dismissed = null;
        if (flyout is { IsOpen: true })
        {
            flyout.Hide();
        }
        if (what is not null)
        {
            FlyoutClosed(what);
        }
    }

    private LRESULT? FlyoutMessage(uint msg, WPARAM wParam, LPARAM lParam)
    {
        var list = flyout;
        if (list is null)
        {
            return null;
        }
        // §525: a dropdown's popup closes by the keys that open it from its control (F4, Alt+Down, Alt+Up), as a
        // ComboBox's does; the keys go back to its control, as for Escape.
        if (msg is WM.WM_KEYDOWN or WM.WM_SYSKEYDOWN && pending is { Drops: true } && list.IsOpen
            && LightDismiss.TogglesDrop((int)(nuint)wParam, LightDismiss.AltOf(msg, lParam)))
        {
            CloseFlyout();
            return (LRESULT)0;
        }
        var happened = list.Message(msg, wParam, lParam);
        var what = pending;
        // §622: a choice tried on under the pointer comes off before anything done in the menu is (Dock.TryOn.cs).
        var settles = Settles(happened.Kind);
        if (settles)
        {
            Settle();
        }
        switch (happened.Kind)
        {
            // §622: the row the pointer or the keys rest on: its choice of the Appearance tried on, or the config worn again.
            case FlyoutEventKind.Hover:
                if (what is not null)
                {
                    Report(() => TryOn(what, happened.Id));
                }
                break;
            case FlyoutEventKind.Invoke:
                pending = null;
                if (list.IsOpen)
                {
                    // §374: where it stood, for a dialog the pick asks for to show it there again.
                    what?.Stood = list.Remembered();
                    list.Hide();
                }
                if (what is not null)
                {
                    // folders: a snip from a widget's menu waits for every folder to be off the
                    // screen, as the snip a click starts does (FolderForward), so none is in it.
                    var snip = ActRowOf(what.Acts, happened.Id) is { } picked && PicturesTheScreen(picked.Act) && openFolders.Count > 0;
                    if (snip)
                    {
                        CloseFolder();
                    }
                    // folders: what the pick does to the folder the menu stood over, read before
                    // the pick acts — "Edit taskbar" is a pick that starts arranging the bar.
                    var after = what.Folder is { } under
                        ? FolderAfterMenu(Arranging(under.Window), PickHandsAway(what.Acts, happened.Id, what.Options), focusElsewhere: false)
                        : AfterMenu.Stays;
                    // The snip once the screen shows the folders and the menu gone (Surfaces.AfterPresent).
                    if (snip)
                    {
                        var id = happened.Id;
                        Surfaces.AfterPresent(() => Report(() => Chosen(what, id)));
                    }
                    else
                    {
                        Report(() => Chosen(what, happened.Id));
                    }
                    if (after == AfterMenu.Closes && what.Folder is { } left && openFolders.Exists(open => ReferenceEquals(open, left)))
                    {
                        CloseFolder(left);
                    }
                    FlyoutClosed(what);
                    // edit: the selected widget's own flyout, shut under its menu, opens again (§212).
                    if (what.Topic == Topic.LayoutEditor)
                    {
                        SelectedFlyoutSoon();
                    }
                }
                break;
            case FlyoutEventKind.Dismissed:
                pending = null;
                if (what is not null)
                {
                    // menus: words typed in a field and left there as the menu closed, kept (Dock.MenuFields.cs).
                    Report(() => FieldsLeft(what, list));
                    dismissed = (what, GetTickCount());
                    FlyoutClosed(what);
                }
                break;
            // tray: one of the overflow's icons dragged onto the bar or to another place (Dock.Tray.cs).
            case FlyoutEventKind.DragMove:
                Report(() => TrayDragFromOverflow(happened.Id, happened.At, drop: false, cancelled: false));
                break;
            case FlyoutEventKind.DragDrop:
                Report(() => TrayDragFromOverflow(happened.Id, happened.At, drop: true, cancelled: false));
                break;
            case FlyoutEventKind.DragCancelled:
                Report(() => TrayDragFromOverflow(happened.Id, happened.At, drop: false, cancelled: true));
                break;
            // tray: the pointer and the left button over the overflow's hidden icons.
            case FlyoutEventKind.Pointer:
            case FlyoutEventKind.Rest:
            case FlyoutEventKind.Press:
            case FlyoutEventKind.Release:
                Report(() => OverflowPointer(what, happened));
                break;
            // A right click in a flyout that takes them (the overflow's hidden tray icons).
            case FlyoutEventKind.Context:
                pending = null;
                if (what is not null)
                {
                    Report(() => ContextChosen(what, happened.Id));
                    FlyoutClosed(what);
                }
                break;
            // menus: a row's control acted — a folder line's height — and the menu stays (Flyout.Controls.cs).
            case FlyoutEventKind.Adjust:
                if (what is not null)
                {
                    Report(() => MenuAdjusted(what, list, happened.Id));
                }
                break;
            // edit: a shortcut of the selection's the menu has no row for: the menu gone, as a pick closes it, the key where
            // it would have gone with the menu closed (KeyFromMenu).
            case FlyoutEventKind.PassedKey:
                pending = null;
                if (what is not null)
                {
                    FlyoutClosed(what);
                    Report(() => KeyFromMenu((int)happened.Id));
                    if (what.Topic == Topic.LayoutEditor)
                    {
                        SelectedFlyoutSoon();
                    }
                }
                break;
            // menus, §523: a colour of one's own mixed in a colour's picker: worn a moment, kept, or put back (Dock.MenuPickers.cs).
            case FlyoutEventKind.Mix or FlyoutEventKind.MixKept or FlyoutEventKind.MixUndone:
                if (what is not null)
                {
                    Report(() => MenuMixed(what, happened));
                }
                break;
            // menus: a folder's face dragged into order in its menu, which stays (OptionMenu.cs, Flyout.Reorder.cs).
            case FlyoutEventKind.Reorder:
                if (what is not null)
                {
                    Report(() => MenuReordered(what, list, happened.Id, happened.To));
                }
                break;
        }
        // §622: what was done applied nothing: the bars as the config is again, the choice tried on gone.
        if (settles)
        {
            Report(ShowSettled);
        }
        // "…" and the tray's overflow wait for a button that is down in them to come up before they
        // follow the bar (FollowOverflow); this is it coming up.
        if (msg is WM.WM_LBUTTONUP or WM.WM_CAPTURECHANGED && pending is { Owner: { } shownFrom })
        {
            Report(() => FollowOverflow(shownFrom));
        }
        // Then the default handling, as the flyout expects: Alt+F4 destroys
        // its window, and the next showing makes another.
        return null;
    }

    // The overflow's right click.
    /// <summary>
    /// An item right-clicked in a flyout. A hidden tray icon gets all a right click on the bar
    /// sends, button down, up and the menu request, while the dock still holds the foreground its
    /// flyout had, so the app may bring its menu to the front.
    /// </summary>
    private void ContextChosen(Pending what, uint id)
    {
        // app buttons: a button behind "…" right-clicked is its jump list, by "…".
        if (what.Topic == Topic.AppOverflow)
        {
            if (what.Owner is { } bar && id >= CMD_APP_OVERFLOW && bar.TileAt((int)(id - CMD_APP_OVERFLOW)) is { } hidden)
            {
                AppMenuAt(bar, Hit.AppOverflow, hidden.Item, hidden.Open, hidden.Key, hidden.Pinned);
            }
            return;
        }
        // A status icon behind its arrow: its own rows, as on the bar.
        if (what is { Topic: Topic.Island, Icons: true, Owner: { } from })
        {
            if (islands.TryGetValue(what.Target, out var statusIcons) && statusIcons is IWidgetMenu menu
                && menu.MenuCellOf(what.Cell, id) is var cell and >= 0 && menu.MenuRows(cell) is { Count: > 0 } rows)
            {
                ShowFlyout(
                    new Pending(Topic.WidgetOwn, from) { Hit = what.Hit, Target = what.Target, Cell = cell },
                    [.. rows],
                    FlyoutLayout.List,
                    from.PointerAnchor());
            }
            return;
        }
        if (what.Topic != Topic.Overflow || tray is null || id < CMD_TRAY || id - CMD_TRAY >= (uint)what.Hidden.Count)
        {
            return;
        }
        // The app places its menu by the overflow button.
        if (what.Owner is { } owner)
        {
            PublishTrayRects(owner, what.Hit);
        }
        tray.Click(what.Hidden[(int)(id - CMD_TRAY)], right: true, doubleClick: false, Cursor());
    }

    /// <summary>
    /// tray: what the pointer does over the overflow's hidden icons, sent to their apps as on the bar
    /// (the bar's TrayPointer, TrayPress and TrayClick): every move over an icon, its popup when the
    /// pointer rests there and gone again when it leaves or presses, and a click as the left button
    /// comes up on the icon it went down on without dragging it — the second of two on the same icon
    /// within the double-click time a double click (<see cref="TrayClicks"/>). The app places its popup or its menu by its own icon in the
    /// overflow (<see cref="PublishTrayRects"/>). The overflow stays open through all of it, and the
    /// app taking the foreground closes it, as Windows' overflow does.
    /// </summary>
    private void OverflowPointer(Pending? what, FlyoutEvent happened)
    {
        if (tray is null || what is not { Topic: Topic.Overflow } open)
        {
            return;
        }
        var icon = happened.Id >= CMD_TRAY && happened.Id - CMD_TRAY < (uint)open.Hidden.Count ? open.Hidden[(int)(happened.Id - CMD_TRAY)] : null;
        if (open.Owner is { } owner && happened.Kind is FlyoutEventKind.Rest or FlyoutEventKind.Press or FlyoutEventKind.Release)
        {
            PublishTrayRects(owner, open.Hit);
        }
        switch (happened.Kind)
        {
            case FlyoutEventKind.Pointer:
                SendTrayHover(overflowHover.Moved(icon, out _));
                break;
            case FlyoutEventKind.Rest:
                SendTrayHover(overflowHover.Rested());
                break;
            // Kept, and nothing sent: the click goes whole as the button comes up on it, and a
            // drag out of the overflow sends nothing (TrayClicks, TrayDragFromOverflow).
            case FlyoutEventKind.Press when icon is not null:
                SendTrayHover(overflowHover.Pressed());
                trayClicks.Pressed(icon, right: false, Cursor(), GetTickCount(), GetDoubleClickTime());
                break;
            case FlyoutEventKind.Release:
                if (trayClicks.Released(icon?.Key, right: false) is { } click)
                {
                    tray.Click(click, HWND.NULL);
                }
                break;
        }
    }

    /// <param name="what">The menu that closed.</param>
    private void FlyoutClosed(Pending what)
    {
        // §523: a colour being mixed in it is kept as it was last worn, as letting it go would (Dock.MenuPickers.cs).
        if (mixing is { } mixed && ReferenceEquals(mixed.What, what))
        {
            Report(() => MixDone(keep: true));
        }
        // §622: a choice tried on in it comes off with it (Dock.TryOn.cs).
        Report(TakeOff);
        // §338: what was typed in an account's form goes with the menu, its password wiped (Dock.MenuAccounts.cs); §535 not with
        // a popup of the settings panel (a mail form's Security), the form standing on its Accounts page: it goes as the panel folds.
        if (what.Options.Count > 0 && !what.Drops)
        {
            MenuAccountsClosed();
        }
        // tray: an overflow closing closes the popup of the icon the pointer was on in it, and its
        // icons are where its button is again for an app that asks.
        if (what.Topic == Topic.Overflow)
        {
            // tray: an icon being dragged out of it goes back where it was with it, rather than
            // staying half-dragged with no flyout left to hear the button come up (Dock.Tray.cs).
            if (trayDrag is { FromOverflow: true })
            {
                EndTrayDrag(cancelled: true);
            }
            SendTrayHover(overflowHover.Left());
            if (what.Owner is { } shown && !disposed && windows.Contains(shown))
            {
                PublishTrayRects(shown, what.Hit);
            }
        }
        // folders: a widget's menu held its folder open the same way (Dock.Folders.cs, FolderMenu).
        if (what.Folder is { } held)
        {
            FolderMenuEnded(held);
        }
        var window = what.Owner;
        if (!disposed && window is not null && windows.Contains(window))
        {
            window.FlyoutClosed();
        }
    }

    private bool FlyoutOpen => flyout is { IsOpen: true };

    /// <summary>
    /// preview: any popup that has the screen — a menu, a jump list, or an island's own flyout
    /// (calendar, notifications, quick settings, a folder, …), on this bar or another. A hover
    /// popup never opens over one, as on the taskbar; <see cref="FlyoutOpen"/> alone sees only
    /// the generic list, which is how the desktop flyout used to open on top of the calendar.
    /// </summary>
    // preview: a popup has the screen, so a hover popup must not open over it (docs\widgets.md §14).
    // folders: while a bar is being edited that is always true — a folder may stand open through
    // every press, and nothing at all should open by hovering while someone is arranging the bar.
    private bool AnyPopupOpen => FlyoutOpen || FlyoutHost.OpenMenus.Length > 0 || editor is not null;

    private bool FlyoutOpenFor(DockWindow window) => (FlyoutOpen && pending?.Owner == window) || IslandPopupOpenFor(window);

    private void Chosen(Pending what, uint id)
    {
        // app clicks: a window's own menu carries the window's commands, not Karpo's, so nothing
        // below may read them (Dock.AppClicks.cs).
        if (what.Topic == Topic.Window)
        {
            WindowChosen(what, id);
            return;
        }
        // edit: a width picked for a list of an open folder (Dock.Folders.cs).
        if (what.Topic == Topic.FolderWidth)
        {
            FolderWidthChosen(what, id);
            return;
        }
        // edit: a widget a folder list has no room for, picked from its warning's list (Dock.Folders.cs).
        if (what.Topic == Topic.FolderHidden)
        {
            FolderHiddenChosen(what, id, what.Owner?.PointerAnchor() ?? Cursor());
            return;
        }
        // menus: a setting picked from a widget's menu, or a folder's face added, taken off or moved (OptionMenu.cs).
        if (OptionMenu.PickOf(what.Options, id) is { } option)
        {
            OptionPicked(what, option, null);
            return;
        }
        // §573: a folder line's Height or Size on its page of the settings panel, acted as its menu acts it (MenuAdjusted).
        if (LineHeightActed(what, id))
        {
            return;
        }
        // folders: a row of a widget's own acts, done to the widget in its open folder (Dock.WidgetMenus.cs).
        if (ActRowOf(what.Acts, id) is { } act)
        {
            // screentime: an app of the day's, by the pins' one rule: its windows forward, else started.
            if (act is { Act: WidgetAct.OpenApp, On: { } key })
            {
                if (ScreenTimeNames.Of(key).Item is { } app)
                {
                    OpenApp(app, OpenWindowsOf(app), AppAsk.Usual);
                }
                return;
            }
            // perf: an app of the top processes ended, once Windows' own question is answered.
            if (act is { Act: WidgetAct.EndTask, On: { } ending })
            {
                EndProcesses(what.Owner, ending);
                return;
            }
            // A face's row that is Karpo's own: a folder edited, its page, the accounts, the layouts.
            if (what.Owner is { } faced && FaceActChosen(faced, act.Act, what.Target))
            {
                return;
            }
            // A face's row works the widget its face shows (Dock.FaceMenus.cs); a line's, the line.
            var widget = act.Works ?? (what.Folder is { } open ? open.Flyout.Folder?.Widget(what.Target) : null);
            RunAct(act, widget, OpenFromMenu);
            return;
        }
        // A row of the bar's own menu that is the person's: a place of Windows', a shortcut (Dock.BarMenu.cs).
        if (BarRowOf(what.BarMenu, id) is { } barRow)
        {
            RunBarEntry(barRow.Entry, what.Owner);
            return;
        }
        // §581: the bar's menu's "Taskbar settings": edit mode on this bar, the edit bar's Taskbar page open, once the menu
        // has gone (Dock.EditEntries.cs), as Win+Alt+Enter on a bar's button.
        if (id == CMD_TASKBAR_SETTINGS && what.Owner is { } edited)
        {
            OpenEditMenuLater(edited, EditEntry.TaskbarSettings);
            return;
        }
        // edit: a row of the rail's list of widgets the bar had no room for (Dock.EditRail.cs).
        if (NotShownChosen(what, id, what.Owner?.PointerAnchor() ?? Cursor()))
        {
            return;
        }
        if (EditChosen(what, id))
        {
            return;
        }
        // licence: "Get Karpo Pro", in the add list or a refusal (Dock.License.cs).
        if (LicenseChosen(id, what.Owner))
        {
            return;
        }
        // edit: a row of the add list (Dock.Folders.cs).
        if (AddChosen(what, id))
        {
            return;
        }
        // pins: pinning, unpinning, mending a broken pin (Dock.Pins.cs).
        if (PinChosen(what, id))
        {
            return;
        }
        // quick: the rename dialog is a flyout of the dock's own, at the desktops face: the desktop
        // you are on, which is the one the face names.
        if (id == CMD_RENAME_DESKTOP && what.Owner is { } renameOwner && desktops is { } names && names.Current is var renamed && renamed >= 0 && renamed < names.Ids.Count)
        {
            ShowQuick(renameOwner, what.Hit ?? Hit.Panel, QuickSettings.QuickKind.RenameDesktop, names.Ids[renamed]);
            return;
        }
        if (id == CMD_DELETE_DESKTOP && desktops is { } deleting && deleting.Current is var closing && closing >= 0)
        {
            deleting.Remove(closing);
            CheckDesktops();
            return;
        }
        switch (what.Topic)
        {
            case Topic.WidgetOwn:
                if (islands.TryGetValue(what.Target, out var menuIsland) && menuIsland is IWidgetMenu menu)
                {
                    menu.MenuChosen(what.Cell, id);
                }
                return;
            case Topic.Island:
                if (islands.TryGetValue(what.Target, out var island))
                {
                    island.Invoke(what.Cell, id);
                }
                return;
            // app buttons: a button behind "…" chosen is that button clicked.
            case Topic.AppOverflow:
                if (what.Owner is { } bar && id >= CMD_APP_OVERFLOW && bar.TileAt((int)(id - CMD_APP_OVERFLOW)) is { } chosen)
                {
                    TileClicked(bar, (int)(id - CMD_APP_OVERFLOW), chosen, middle: false);
                }
                return;
            case Topic.Overflow:
                if (tray is not null && id >= CMD_TRAY && id - CMD_TRAY < (uint)what.Hidden.Count)
                {
                    // The app places its flyout by the overflow button.
                    if (what.Owner is { } owner)
                    {
                        PublishTrayRects(owner, what.Hit);
                    }
                    tray.Click(what.Hidden[(int)(id - CMD_TRAY)], false, false, Cursor());
                }
                return;
            // The Start button's quick links.
            case Topic.Start:
                if (QuickLinks.Of(id) is { } link)
                {
                    OpenQuickLink(link);
                }
                return;
        }
        if (id >= CMD_JUMP && id - CMD_JUMP < (uint)what.Jumps.Count)
        {
            JumpLists.Invoke(what.Jumps[(int)(id - CMD_JUMP)]);
            return;
        }
        switch (id)
        {
            case CMD_LAUNCH:
                LaunchLater(what.Target);
                break;
            // pins: a pin widget where the open apps are, or the widget off the bar (Dock.Pins.cs).
            case CMD_PIN:
                PinApp(what);
                break;
            case CMD_UNPIN:
                Unpin(what.Pin);
                break;
            case CMD_LOCATE:
                Locate(what);
                break;
            case CMD_CLOSE:
                foreach (var open in what.Open)
                {
                    Apps.Close(open);
                }
                break;
            // app clicks: the app's processes ended, as Task Manager's End task does (EndTask).
            case CMD_END_TASK:
                EndTask.End(what.Open);
                break;
            case CMD_TASK_MANAGER:
                Launch.Open("taskmgr.exe");
                break;
            // §534: the bars locked or unlocked (Dock.BarMove.cs).
            case CMD_LOCK_TASKBAR:
                ToggleLock();
                break;
            // The settings not saved, from the bar's menu: tried again now (Dock.KeepConfig.cs).
            case CMD_SAVE_AGAIN:
                SaveConfigAgain();
                break;
            // layouts: the bar shows the layout chosen in its menu (Dock.Folders.cs).
            case >= CMD_LAYOUT and < CMD_LAYOUT + 100 when what.Owner is { } chosen:
                ChooseLayout(chosen, (int)(id - CMD_LAYOUT));
                break;
            case CMD_NEW_DESKTOP:
                desktops?.Add();
                CheckDesktopsSoon();
                break;
            case CMD_TASK_VIEW:
                Desktops.TaskView();
                break;
        }
    }

    /// <summary>
    /// A line of the Start button's quick links, doing what Windows' Win+X menu does: Search and
    /// Run are their keys, Desktop is the Show desktop button, the power lines end the session or
    /// put the PC to sleep, and the rest open their page, console or app.
    /// </summary>
    private void OpenQuickLink(QuickLink link)
    {
        switch (link)
        {
            case QuickLink.Search:
                Chord((ushort)VK.VK_LWIN, VK_S);
                return;
            case QuickLink.Run:
                Chord((ushort)VK.VK_LWIN, VK_R);
                return;
            case QuickLink.Desktop:
                ToggleDesktop();
                return;
        }
        if (QuickLinks.IsPower(link))
        {
            QuickLinks.Power(link);
        }
        else if (QuickLinks.Target(link, QuickLinkMachine.Read()) is { } target)
        {
            // "(Admin)" asks through the shell's elevation; the rest open as a tile does.
            if (target.Verb is { } verb)
            {
                Launch.Open(target.File, verb);
            }
            else
            {
                LaunchLater(target.File);
            }
        }
    }

    private static POINT Cursor()
    {
        POINT at;
        GetCursorPos(&at);
        return at;
    }

    /// <summary>
    /// A tray icon's tooltip on the bar: the whole of the app's tip, every line of it, as the taskbar
    /// shows a standard tooltip — the app's name first and what it has to say under it ("Speakers
    /// (Realtek Audio)", "50%") — and "Tray icon" where it gave none. None at all where the app draws a
    /// popup of its own when the pointer rests on its icon (<see cref="TrayIcon.StandardTip"/>): a
    /// tooltip of Karpo's would stand over it.
    /// </summary>
    internal static string? TrayTip(TrayIcon icon)
    {
        if (!icon.StandardTip)
        {
            return null;
        }
        var lines = icon.Tip.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').Select(line => line.TrimEnd()).ToList();
        while (lines.Count > 0 && lines[^1].Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }
        while (lines.Count > 0 && lines[0].Length == 0)
        {
            lines.RemoveAt(0);
        }
        return lines.Count > 0 ? string.Join("\n", lines) : S.DockTrayIcon;
    }

    private static string FirstLine(string text)
    {
        var end = text.AsSpan().IndexOfAny('\r', '\n');
        return (end < 0 ? text : text[..end]).Trim();
    }

    // --- Leaving -----------------------------------------------------------

    private void Quit()
    {
        Shutdown();
        foreach (var window in windows)
        {
            window.Dispose();
        }
        windows = [];
        Surfaces.Gone(host);
    }

    /// <summary>Puts back what Karpo changed outside itself. Safe to call twice.</summary>
    private void Shutdown()
    {
        // A change config.json did not take yet gets its last try (Dock.KeepConfig.cs).
        FlushConfig();
        // The settings app is a process of its own and is left alone: its
        // "Quit Karpo", the only way to quit from the outside, asks the dock
        // and leaves the app open.
        if (preview is not null)
        {
            // Unhooks the popup from the dock, then destroys it.
            var list = preview;
            preview = null;
            previewOwner = null;
            list.Dispose();
        }
        if (flyout is not null)
        {
            var menu = flyout;
            flyout = null;
            pending = null;
            menu.Dispose();
        }
        // The calendar and the notification list go the same way.
        DisposeIslandPopups();
        // The edit bars go with edit mode, and the carry window.
        editor = null;
        DisposeRails();
        carry?.Dispose();
        carry = null;
        trayCarry?.Dispose();
        trayCarry = null;
        DisposeChromes();
        DisposeBackdrops();
        RestoreTaskbar();
        // Apps find the taskbar's tray by its window; it goes, so they find
        // Explorer's again.
        if (tray is not null)
        {
            tray.Dispose();
            tray = null;
            // signals: and their taskbar list finds Explorer's band again once asked to look.
            AskAppsForSignals();
        }
        foreach (var window in windows)
        {
            window.Release();
        }
        DeregisterShellHookWindow(host);
        // §667: Explorer's answer is the one left.
        minRect?.Dispose();
        minRect = null;
    }

    private void RestoreTaskbar()
    {
        PutTaskbarBack();
        // keys: Explorer's taskbar answers its keys again.
        SyncKeys();
    }

    /// <summary>
    /// Windows asks before a log-off or shutdown, while Explorer is still
    /// there to take its settings back, and the process may be ended before
    /// it hears the answer. So the taskbar goes back now, and no reconcile hides
    /// it again until the session goes on (<see cref="SessionEnded"/>).
    /// </summary>
    private void SessionEnding()
    {
        sessionEnding = true;
        RestoreTaskbar();
        // And a change config.json did not take yet gets its last try now, for the same reason (Dock.KeepConfig.cs).
        FlushConfig();
    }

    /// <summary>
    /// appbar: Explorer's taskbar put back as the person had it (Taskbar.PutItBack), and what that
    /// came to on the trace: a put-back that left nothing behind left no clue either.
    /// </summary>
    private void PutTaskbarBack()
    {
        if (taskbar?.PutItBack() is { } said)
        {
            appbarTrace.Write(said);
        }
        taskbar = null;
    }

    private void SessionEnded(bool ending)
    {
        if (ending)
        {
            Troubles.Write(Closing.Line(QuitWay.SessionEnd));
            Shutdown();
            return;
        }
        // Someone cancelled: hidden again, as the config still asks (the reconcile).
        sessionEnding = false;
        Reconcile(Stir.Settings);
    }

    /// <summary>
    /// appbar: Explorer's taskbar as the reconcile hides it and puts it back (BarKeeper): the live
    /// <see cref="Taskbar"/>, the guard that puts it back should Karpo end without doing it, and the
    /// trace of each.
    /// </summary>
    private sealed class LiveExplorer(Dock dock) : IExplorerTaskbar
    {
        public bool Found => Taskbar.ExplorerTray() != HWND.NULL;

        public bool Taken => dock.taskbar is not null;

        public string Take()
        {
            // The strips were asked for just before this, beside the taskbar while it still holds its
            // own: the shell puts each beside it, and no display is ever without one; the keeper asks
            // for them again straight after, on the edge.
            var since = System.Diagnostics.Stopwatch.StartNew();
            dock.taskbar = Taskbar.Hide();
            if (dock.taskbar is null)
            {
                return "no Explorer taskbar to hide";
            }
            dock.SpawnGuard();
            // keys: the bars take the taskbar's keys (Dock.Keys.cs).
            dock.SyncKeys();
            return $"took over from Explorer's taskbar in {since.Elapsed.TotalMilliseconds:0} ms"
                + (dock.taskbar.Parked is { } parked ? "; " + parked : "");
        }

        public string? Keep() => dock.taskbar?.Keep();

        public string Give()
        {
            var said = dock.taskbar?.PutItBack() ?? "Explorer's taskbar was shown again already"; // not-ui: the appbar trace
            dock.taskbar = null;
            // keys: Explorer's taskbar answers its keys again.
            dock.SyncKeys();
            return said;
        }
    }

    // --- The window list ---------------------------------------------------

    /// <summary>
    /// A button's windows as its list shows them: in the order they were opened (first seen, as
    /// <see cref="Apps.All"/> keeps them), so the cards stand still as windows are switched between,
    /// as Windows' do. The button keeps them by recency for its clicks (<see cref="AppClicks"/>).
    /// </summary>
    private List<(HWND Hwnd, string Title)> Listed(Tile tile) =>
        [.. InOpenOrder(apps.All, tile.Open).Select(win => (win.Hwnd, win.Title))];

    /// <summary>Those of <paramref name="all"/> (in the order windows were first seen) that are among <paramref name="open"/>.</summary>
    internal static IEnumerable<Win> InOpenOrder(IEnumerable<Win> all, IReadOnlyCollection<HWND> open) =>
        all.Where(win => open.Contains(win.Hwnd));

    // What the open window list stands for beyond a tile (the pin
    // or group member whose windows it lists) and where it opened, so it can
    // be listed again as windows come and go (RefreshPreview).
    private Item? previewItem;
    private POINT previewAnchor;

    // preview: how long the button it opened from is along the bar, in px: the gap between that
    // button and the list is the list's, and only that gap (Preview.Bridge).
    private int previewSpan;

    /// <summary>The window list is open.</summary>
    private bool PreviewShowing => preview is { IsOpen: true };

    /// <summary>The pointer is over the window list.</summary>
    private bool PreviewInside => preview is { Inside: true };

    /// <summary>
    /// A listed window's media session, for its media buttons: found by the
    /// window's own AUMID and exe, which is what Windows files the session
    /// under, never by a pin's shortcut.
    /// </summary>
    private MediaControls? MediaOf(HWND window) =>
        apps.Get(window) is { } win ? MediaSessions.Shared.For(win.Aumid, win.Exe) : null;

    /// <summary>An app's open windows on this desktop, as the window list shows them.</summary>
    private List<(HWND Hwnd, string Title)> WindowsOf(Item item) =>
        apps.All.Where(win => win.Keys().Any(item.Matches)).Select(win => (win.Hwnd, win.Title)).ToList();

    /// <summary>
    /// The window list of button <paramref name="index"/>: by pointing, or <paramref name="held"/> — a
    /// click's or the keys' (<see cref="HoldPreview"/>) — kept whatever the pointer does.
    /// </summary>
    private void OpenPreview(DockWindow window, int index, bool held = false)
    {
        // One popup at a time, as on the taskbar: a jump list keeps the
        // thumbnails away.
        if (window.TileAt(index) is not { } shown || FlyoutOpen)
        {
            return;
        }
        var listed = Listed(shown);
        if (listed.Count == 0)
        {
            ClosePreview(window);
            return;
        }
        // A list laid out again keeps how it was opened.
        var keep = previewHeld && previewOwner == window && preview is { IsOpen: true } open && open.Tile == index;
        ShowList(window, index, listed, window.Anchor(Hit.Tile(index)), null, window.Span(Hit.Tile(index)));
        window.Scene.Tooltip(gfx, null, null);
        if (held || keep)
        {
            HoldPreview();
        }
        // keys: the card they were on, lit again in the list laid out anew (Dock.AppList.cs).
        if (keep && previewKeyCard >= 0 && preview is { } again)
        {
            previewKeyCard = Math.Min(previewKeyCard, again.Cards - 1);
            again.Select(previewKeyCard);
        }
    }

    /// <summary>
    /// preview: the window list's popup is made, hidden, as soon as previews are on, and kept: the
    /// window and its composition target cost nothing to keep and a few ms to make, and that was
    /// paid by the first hover, on top of the wait. A popup that won't be made now is only made
    /// later, when a list is first shown, as before.
    /// </summary>
    private void WarmPreview()
    {
        if (preview is not null || !config.Layouts.Any(layout => layout.Previews) || disposed)
        {
            return;
        }
        try
        {
            preview = Made(new Preview(gfx, instance, GCHandle.ToIntPtr(cell)));
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Console.Error.WriteLine($"Karpo: the window list not made ahead: {error.Message}");
        }
    }

    /// <summary>
    /// The window list at <paramref name="anchor"/>, for <paramref name="tile"/>, or for the pin
    /// or group member <paramref name="item"/> (tile int.MinValue); the desktop flyout gives way.
    /// </summary>
    private void ShowList(DockWindow window, int tile, List<(HWND Hwnd, string Title)> listed, POINT anchor, Item? item, int span)
    {
        // Made at start where previews are on (WarmPreview); here otherwise, for a click's list.
        preview ??= Made(new Preview(gfx, instance, GCHandle.ToIntPtr(cell)));
        if (previewOwner is not null && previewOwner != window)
        {
            previewOwner.KillPreviewTimers();
            // app clicks: another bar's list takes over: what held this one lets go (Dock.AppList.cs).
            LetGoPreview();
        }
        previewOwner = window;
        // preview: the last argument is this bar, which a peek keeps on top (LivePeek.Show); with
        // several displays it has to be the one the list was opened from.
        // signals: a window that registered tabs is listed as its tabs, and each card shows what its
        // app said of its preview: toolbar, tooltip and clip (Dock.Signals.cs).
        preview.Show(gfx, window.Look, window.Layout.Scale, tile, WithTabs(listed), anchor, window.WorkArea(), window.Layout.Edge, MediaOf, window.Hwnd, ThumbOf, buttonSpan: span);
        // preview: and watched while it is up, so a leave no message told of still closes it — unless
        // a click or the keys hold it, which the pointer's whereabouts leave (Dock.AppList.cs).
        if (preview.IsOpen && !previewHeld)
        {
            window.PreviewShown();
        }
        previewWindows = listed;
        previewItem = item;
        previewAnchor = anchor;
        previewSpan = span;
    }

    /// <summary>
    /// The app of a window on any desktop, resolved as the taskbar's tiles are
    /// (Item.Running, whose Registered check keeps an unpackaged app's derived
    /// ID from becoming an "S" letter tile) and kept by app key with them.
    /// </summary>
    private Item? DesktopAppItem(PreviewWindow window)
    {
        if (window.Key.Length == 0)
        {
            return null;
        }
        if (known.TryGetValue(window.Key, out var item))
        {
            return item;
        }
        if (window.Exe is not { Length: > 0 } exe)
        {
            return null;
        }
        item = Item.Running(window.Aumid, exe, (HWND)window.Hwnd);
        known[window.Key] = item;
        return item;
    }

    // §204: an app may take a new window's button away (ITaskbarList::DeleteTab) or make it a tool
    // window just after showing it, with no word from the shell hook; the windows are read once
    // more this long after the last one came. A host timer.
    private const uint APPS_AGAIN = 46;
    private const uint APPS_AGAIN_MS = 500;

    // The desktop buttons' apps and the desktop flyout follow window
    // events, read at most once per DESKTOP_APPS_MS. A host timer.
    private const uint DESKTOP_APPS = 40;
    private const uint DESKTOP_APPS_MS = 250;
    private bool desktopAppsArmed;

    /// <summary>A shell hook message: a window came, went or was activated, so the desktops' apps are read again soon.</summary>
    private void DesktopAppsSoon(uint code)
    {
        if (code is (uint)HSHELL.HSHELL_WINDOWCREATED or (uint)HSHELL.HSHELL_WINDOWDESTROYED
            or (uint)HSHELL.HSHELL_WINDOWACTIVATED or Apps.HSHELL_RUDEAPPACTIVATED)
        {
            DesktopAppsSoon();
        }
    }

    /// <summary>The desktops' apps are read again soon: a bar came into view, or windows changed.</summary>
    private void DesktopAppsSoon()
    {
        if (desktops is null || desktopAppsArmed)
        {
            return;
        }
        desktopAppsArmed = true;
        Arm(DESKTOP_APPS, DESKTOP_APPS_MS);
    }

    /// <summary>
    /// The desktops' apps are stale: a window came, went or moved. Nothing on a bar shows them any
    /// more — the switcher's button is a name — so nothing is measured or built again; the list is
    /// read afresh the next time it is opened, and an open one follows at once.
    /// </summary>
    private void DesktopAppsDue()
    {
        Kill(DESKTOP_APPS);
        desktopAppsArmed = false;
        foreach (var window in windows)
        {
            window.DesktopAppsChanged();
        }
        RefreshSwitcher();
    }

    private void ClosePreview(DockWindow? from)
    {
        from?.KillPreviewTimers();
        previewOwner?.KillPreviewTimers();
        // app clicks: a list a click or the keys held lets go of the presses and Esc (Dock.AppClicks.cs).
        LetGoPreview();
        if (preview is { IsOpen: true })
        {
            preview.Hide();
        }
        // A new list rather than a cleared one: the preview may still hold
        // the one it was shown.
        previewWindows = [];
        previewItem = null;
    }

    private LRESULT? PreviewMessage(uint msg, WPARAM wParam, LPARAM lParam)
    {
        var x = (int)(short)((nint)lParam & 0xffff);
        var y = (int)(short)(((nint)lParam >> 16) & 0xffff);
        var list = preview;
        if (list is null)
        {
            return null;
        }
        PreviewEvent happened;
        switch (msg)
        {
            // The peek's delay, or a media session that changed.
            // app clicks: or files dragged onto a card rested long enough (Dock.AppClicks.cs).
            case WM.WM_TIMER when (nuint)wParam == CardDragTimer:
                CardDragDue(list);
                return (LRESULT)0;
            case WM.WM_TIMER: list.Timer((nuint)wParam); return (LRESULT)0;
            case WM.WM_MOUSEACTIVATE:
                return (LRESULT)MA.MA_NOACTIVATE;
            case WM.WM_MOUSEMOVE:
                happened = list.Moved(x, y);
                break;
            case WM.WM_MOUSELEAVE:
                happened = list.Left();
                break;
            case WM.WM_LBUTTONUP:
                happened = list.Clicked(x, y, false);
                break;
            case WM.WM_MBUTTONUP:
                happened = list.Clicked(x, y, true);
                break;
            // app clicks: a card's right click is its window's own menu, as on Windows' taskbar.
            case WM.WM_RBUTTONUP:
                happened = list.RightClicked(x, y);
                break;
            case WM.WM_LBUTTONDOWN:
            case WM.WM_MBUTTONDOWN:
                happened = PreviewEvent.Nothing;
                break;
            // app clicks: files dragged over a card bring its window forward after a rest (Dock.AppClicks.cs).
            case DropTarget.WM_KARPO_DRAG:
                happened = PreviewDragged(list, DropTarget.FromLParam(lParam));
                break;
            default:
                return null;
        }
        var owner = previewOwner;
        switch (happened.Kind)
        {
            case PreviewEventKind.Entered:
                owner?.PreviewEntered();
                break;
            case PreviewEventKind.Left:
                owner?.PreviewLeft();
                break;
            case PreviewEventKind.Activate:
                // app clicks: a card follows the one rule of a button of one window: the window in
                // front minimizes, any other comes forward — from another desktop too (Dock.AppClicks.cs).
                CardClicked(owner, happened.Window);
                break;
            case PreviewEventKind.Menu:
                CardMenu(owner, happened.Window);
                break;
            case PreviewEventKind.Dismiss:
                ClosePreview(owner);
                break;
            case PreviewEventKind.Close:
                Apps.Close(happened.Window);
                break;
        }
        return (LRESULT)0;
    }

    /// <summary>
    /// The open window list, listed again after windows came, went or were
    /// renamed, whatever it was opened from (a tile, a pin, a group member):
    /// a closed window's card goes, and a list left empty closes.
    /// </summary>
    private void RefreshPreview()
    {
        if (preview is not { IsOpen: true, Tile: int index } || previewOwner is not { } owner)
        {
            return;
        }
        List<(HWND Hwnd, string Title)> listed;
        if (index >= 0)
        {
            listed = owner.TileAt(index) is { } tile ? Listed(tile) : [];
        }
        else if (previewItem is { } item)
        {
            listed = WindowsOf(item);
        }
        else
        {
            return;
        }
        if (listed.Count == 0)
        {
            ClosePreview(owner);
        }
        else if (!listed.SequenceEqual(previewWindows))
        {
            if (index >= 0)
            {
                OpenPreview(owner, index);
            }
            else
            {
                ShowList(owner, index, listed, previewAnchor, previewItem, previewSpan);
            }
        }
    }

    // --- Windows, desktops and the clock -----------------------------------

    private void ShellMessage(uint code, HWND window)
    {
        var activation = code == HSHELL.HSHELL_WINDOWACTIVATED || code == Apps.HSHELL_RUDEAPPACTIVATED;
        // Another window in front, or a layout changed.
        if (activation || code == HSHELL.HSHELL_LANGUAGE)
        {
            KeyboardChanged(again: activation);
        }
        // tray: apps find the tray by the first Shell_TrayWnd in z-order, and Explorer's taskbar puts
        // itself above the spy when it is shown or activated. From then on every icon added or changed
        // went to Explorer alone and never showed on Karpo's bar — on a bar that never hides, which is
        // the only other time the spy is put first (Reveal), until Karpo started again. A window coming
        // to the front is when that is checked: one FindWindow, and the spy put first again where it was
        // not (Tray.EnsureFirst).
        if (activation)
        {
            tray?.EnsureFirst();
            // edit: the settings app, over everything in edit mode, came to the front: the selected
            // widget's flyout stands over it again (Dock.FlyoutPreview.cs).
            if (selectedShown is not null)
            {
                FlyoutHost.RaiseLooks();
            }
        }
        // A window nobody listed becoming active usually means a desktop
        // switch uncloaked a whole set of windows at once.
        Change change;
        if (activation && window != HWND.NULL && apps.Find(window) is null)
        {
            var scanned = apps.Scan();
            change = Max(scanned, apps.Shell(code, window));
        }
        else
        {
            change = apps.Shell(code, window);
        }
        // quick: the magnifier came or went, which nothing but its window says: its switches read it
        // again (Assistive.cs, docs\widgets.md §439).
        if (code is (uint)HSHELL.HSHELL_WINDOWCREATED or (uint)HSHELL.HSHELL_WINDOWDESTROYED
            && Karpo.Dock.QuickSettings.Services.WindowsAssistive.IsMagnifier(window))
        {
            ToIslands(Karpo.Dock.QuickSettings.Services.AssistiveSwitch.WM_KARPO_MAGNIFIER, 0, 0);
        }
        if (code == HSHELL.HSHELL_WINDOWDESTROYED)
        {
            WindowGone(window);
        }
        else if (code == HSHELL.HSHELL_WINDOWCREATED)
        {
            Arm(APPS_AGAIN, APPS_AGAIN_MS);
        }
        // app clicks: what an activation, a flash or a redraw changes of the buttons' own states (Dock.AppClicks.cs).
        AppsHeard(code, window);
        WindowsChanged(change);
        // Windows on other desktops count too, which Apps leaves out.
        DesktopAppsSoon(code);
        // A desktop switch activates a window on the new desktop; the
        // registry watch has been known to miss one.
        if (activation && Environment.TickCount64 - desktopsChecked >= DESKTOPS_EVERY_MS)
        {
            CheckDesktops();
        }
    }

    /// <summary>
    /// Windows came, went or changed: every bar's app buttons are worked out again (its pins and
    /// the open apps it shows, Dock.Pins.cs). A bar whose buttons themselves changed is built
    /// again; where only the running indicators changed, they are set in place.
    /// </summary>
    private void WindowsChanged(Change change)
    {
        if (change == Change.None)
        {
            return;
        }
        var rebuild = false;
        foreach (var window in windows)
        {
            rebuild |= window.DeriveTiles();
        }
        if (rebuild)
        {
            Rebuild();
        }
        else
        {
            // perf: a bar whose buttons only say something new is built again on its own.
            foreach (var window in windows.Where(window => window.Stale))
            {
                window.Build();
            }
        }
        RefreshPreview();
    }

    private void DesktopsChanged()
    {
        if (desktops is null)
        {
            return;
        }
        if (!desktops.Refresh())
        {
            return;
        }
        // A switch cloaks one set of windows and uncloaks another, without a
        // shell hook message for any of them.
        RefreshDesktopApplications();
    }

    /// <summary>
    /// Reads the desktops again without the watch, which once missed three new
    /// desktops and left one button until a restart, and rebuilds when they
    /// changed: when a bar comes into view, after the dock's own switch or new
    /// desktop, and on window activations.
    /// </summary>
    private void CheckDesktops()
    {
        if (desktops is null)
        {
            return;
        }
        desktopsChecked = Environment.TickCount64;
        if (!desktops.Check())
        {
            return;
        }
        RefreshDesktopApplications();
    }

    /// <summary>Once Explorer has had the moment it takes to write the new list down.</summary>
    private void RefreshDesktopApplications()
    {
        Guid? current = desktops is { } state && state.Current < state.Ids.Count ? state.Ids[state.Current] : null;
        // What the desktop switcher draws: the names and which of them you are on, put where a
        // widget can read it (DesktopState). Before the rebuild, which is what redraws the button.
        PublishDesktops();
        apps.Scan(current); Rebuild();
        // A list of them standing open (renamed in Task View, another desktop made) follows.
        RefreshSwitcher();
    }

    /// <summary>
    /// The desktops as the dock has just read them, for the widget that names the one you are on.
    /// A widget is made per bar and per folder and cannot be handed the dock's own reader, so it
    /// takes them from here, as the notification and media widgets take theirs from their stores.
    /// </summary>
    private void PublishDesktops()
    {
        if (desktops is { } read)
        {
            _ = DesktopState.Set(read.Names, read.Current);
        }
    }

    private void CheckDesktopsSoon() => Arm(DESKTOPS, DESKTOPS_AFTER_MS);

    private void TaskbarRestarted()
    {
        appbarTrace.Write("Explorer's taskbar was made again (TaskbarCreated): every bar registers again");
        // A new Explorer knows none of the appbars, nor the space they held. The reconcile registers
        // every bar again, asks for the strips, and hides the new taskbar and sets its auto-hide again
        // (Taskbar.Keep) — or, at sign-in, takes over from a taskbar that wasn't there yet when the
        // dock started.
        Reconcile(Stir.ExplorerBack);
        // The new taskbar sits above the tray's window and has a new band.
        tray?.Restarted();
        // §667: and answers where a window's button is after Karpo, so Karpo's answer goes last again.
        minRect?.Again();
        var change = apps.Scan();
        WindowsChanged(Max(change, Change.States));
        // signals: apps answering the new Explorer's TaskbarButtonCreated before the tray's window
        // was first again found its band; asked again, they find Karpo's.
        if (tray is not null)
        {
            AskAppsForSignals();
        }
        // perf: the new Explorer's taskbars on displays Karpo is off on are hidden
        // and watched again (Taskbar watches their threads only, not every app's).
        RequestArrange();
    }

    private void SettingChanged(WPARAM wParam)
    {
        // §547: the wallpaper changed — a picture set, a slideshow's next, Spotlight's —: every see-through bar's blurred again.
        if ((nuint)wParam == (nuint)SPI.SPI_SETDESKWALLPAPER)
        {
            WallpaperChanged();
            return;
        }
        // quick: the narrator came or went, or sticky keys were switched: their switches redraw (Assistive.cs).
        if ((nuint)wParam is (nuint)SPI.SPI_SETSCREENREADER or (nuint)SPI.SPI_SETSTICKYKEYS)
        {
            ToIslands(WM.WM_SETTINGCHANGE, wParam, 0);
            return;
        }
        // appbar: a work area changed — the shell acting on a strip, someone else's appbar, a display
        // back: the bars laid out again where it moved them, and the arrange's reconcile looks (BarKeeper).
        if ((nuint)wParam == (nuint)SPI.SPI_SETWORKAREA)
        {
            RequestArrange(Stir.WorkArea);
            return;
        }
        // A laptop folded into a tablet or back is announced here ("ConvertibleSlateMode"), and the
        // touch keyboard's button follows it (built again already where it did). tablet: so does the
        // bar at touch size, and Windows' "Optimize taskbar for touch" switching (Dock.PeekTablet.cs).
        var inputsChanged = InputsChanged();
        var touchChanged = TouchChanged();
        if (touchChanged && !inputsChanged)
        {
            Rebuild();
        }
        if (inputsChanged || touchChanged)
        {
            return;
        }
        // calendar: the region format is announced here too ("intl"), and a deferred message has
        // lost its name, so what changed is worked out by asking Windows what the formats are now.
        // A format that is followed has to keep following, and the dates on the bar are measured
        // from templates, so a new format reaches their widths only at a rebuild.
        if (Panels.Nls.Signature() is var formats && formats != regionFormats)
        {
            regionFormats = formats;
            Clock.ForgetZones();
            Rebuild();
            return;
        }
        // calendar: Windows' clock switched its seconds on or off, which a time-and-date face
        // follows, and is measured for (DateTimeFaceDrawing); §485: or its battery's percentage,
        // which Windows' own quick settings button follows (WindowsFaces).
        if (WindowsTaskbarFollowed.Changed())
        {
            Rebuild();
            return;
        }
        // Theme, accent, transparency, contrast, animation and text size are
        // announced here under many names, and a deferred message has lost
        // its name anyway, so the style is read again either way.
        LookChanged();
        // quick: and the dark mode switch reads Windows' mode again (DarkMode.cs), its one notice of a
        // change made in Windows' Settings ("ImmersiveColorSet").
        ToIslands(WM.WM_SETTINGCHANGE, wParam, 0);
        // signals: and so is "Show badges on taskbar apps".
        BadgeSettingChanged();
    }

    // calendar: Windows' regional format as it was when the dock last looked (Nls.Signature).
    private string regionFormats = Panels.Nls.Signature();

    /// <summary>What this PC had for the input buttons when the bars were last built (<see cref="InputButtons"/>).</summary>
    private InputHardware inputs = InputHardware.Read();

    /// <summary>
    /// The bars again where what the input buttons show by has changed — a pen, a touch screen, a
    /// keyboard attached or taken off — and one of them stands on a bar; whether they were.
    /// </summary>
    private bool InputsChanged()
    {
        var now = InputHardware.Read();
        if (now == inputs)
        {
            return false;
        }
        inputs = now;
        if (!config.AllIslands.Any(island => InputButtons.Is(island.Kind)))
        {
            return false;
        }
        Rebuild();
        return true;
    }

    /// <summary>
    /// Windows' look read again. §547: the taskbar takes nothing of Windows' accent or transparency effects
    /// (<see cref="Look.KarpoBase"/>), so where only those changed and edit mode is not on — whose settings follow Windows (§526) —
    /// nothing of the taskbar is built or drawn again: the look is kept for the settings, and a widget's settings standing open
    /// at rest (§340) are shown again in it. §568: Windows' mode it takes again, so a mode changed is another taskbar: every bar,
    /// folder and menu built and drawn again in the Appearance's colours of that mode, live. §613 unless the Appearance pins
    /// the taskbar to a mode of its own: then Windows' mode changed is the settings' alone, as its accent is.
    /// </summary>
    private void LookChanged()
    {
        var fresh = ReadLook();
        if (fresh == look)
        {
            return;
        }
        if (fresh.Wearing(config) == look.Wearing(config) && editor is null)
        {
            if (fresh.Dark != look.Dark)
            {
                Menu.Theme(fresh.Dark);
            }
            look = fresh;
            if (pending is { Configuration: true } settings)
            {
                MenuAgain(settings);
            }
            return;
        }
        Rebuild();
        // §396: the menu standing open too, in the look as it is now.
        Relooked();
    }

    /// <summary>
    /// Tells the islands that show a time or a date when the minute has changed, paints the folders'
    /// clock faces again, advances the countdowns, and schedules the next tick, but only while
    /// something can be seen.
    /// </summary>
    private void TickClock()
    {
        Kill(CLOCK);
        // calendar: a clock face showing its seconds, and a running timer or focus session, tick
        // every second. A countdown's open panel does not: it ticks itself while it is open
        // (CountdownFlyout), so the bar is not dragged to a second's cadence by a popup.
        var fast = SecondsTick;
        countdowns.Tick(Clock.Real());
        if (islands.Count == 0 && !fast)
        {
            return;
        }
        // A hidden dock catches up when it shows.
        var minute = Clock.Real().Ticks / TimeSpan.TicksPerMinute;
        if (minute != islandMinute)
        {
            islandMinute = minute;
            var changed = false;
            foreach (var island in islands.Values)
            {
                try
                {
                    changed |= island.MinuteChanged(gfx);
                }
                catch (Exception error)
                {
                    Report(error);
                }
            }
            if (changed)
            {
                RequestRebuild();
            }
        }
        // folders: a face that tells the time is painted again in place where what it shows has
        // changed — each second while one shows seconds or a countdown runs, else on the minute,
        // and when Windows' clock was set (FolderIsland.FaceTick).
        FaceTicks();
        if (windows.Exists(window => window.Shown || !window.AutoHides))
        {
            // calendar: SecondsTick counts a running countdown too.
            Arm(CLOCK, SecondsTick ? Clock.UntilNextSecond() : Clock.UntilNextMinute());
        }
    }

    /// <summary>
    /// folders: every folder on the bar paints its face again where what it shows has changed. A
    /// face is as wide as what it shows now (docs\widgets.md §244): where one now wants another
    /// width, the bar is laid out again, once for every folder that changed in the tick, and glides.
    /// </summary>
    private void FaceTicks()
    {
        var changed = false;
        foreach (var island in islands.Values)
        {
            if (island is not FolderIsland folder)
            {
                continue;
            }
            try
            {
                changed |= folder.FaceTick(gfx);
            }
            catch (Exception error)
            {
                Report(error);
            }
        }
        if (changed)
        {
            RequestRebuild();
        }
    }

    /// <summary>A window slid away; the clock stops once none is left to read it.</summary>
    private void ClockHidden()
    {
        if (!windows.Exists(window => window.Shown))
        {
            Kill(CLOCK);
        }
    }

    /// <summary>A window's bar is being dragged to a new height.</summary>
    private bool Resizing => windows.Exists(window => window.Resizing);

    /// <summary>
    /// The thickness of the bar of the layout the display <paramref name="displayId"/> shows, in DIPs, as
    /// the config has it now (<see cref="TaskbarLayout.Size"/>).
    /// </summary>
    private int HeightOf(string displayId) => config.ActiveLayout(displayId).Size;

    /// <summary>
    /// The bar of the layout the display <paramref name="displayId"/> shows at <paramref name="height"/>
    /// DIPs, live, not saved: the drag either ends in one save or puts the old height back. A bar has its
    /// layout's size, on every display that shows it.
    /// </summary>
    private void LiveHeight(string displayId, int height)
    {
        if (HeightOf(displayId) == height)
        {
            return;
        }
        config.ActiveLayout(displayId).Size = height;
        Rebuild();
    }

    /// <summary>
    /// The drag ended: the height is kept, once, and the space held for the bar follows it. In edit mode
    /// a step of its history, from the height it had (EditHistory.cs): the drag wrote it in place.
    /// </summary>
    private void KeepHeight(string displayId, int from)
    {
        if (HeightOf(displayId) != from)
        {
            if (editor is not null)
            {
                var before = config.Clone();
                before.ActiveLayout(displayId).Size = from;
                history.Record(before);
            }
            // Kept and tried again where the file does not take it (Dock.KeepConfig.cs).
            KeepConfig(config);
        }
        Rebuild();
    }

    /// <summary>EcoQoS again, unless a window is still in use.</summary>
    private void Rest()
    {
        if (!windows.Exists(window => window.Engaged))
        {
            Efficiency(true);
        }
    }

    // --- Islands -----------------------------------------------------------

    /// <summary>One island per enabled island section, told about the options.</summary>
    /// <summary>
    /// The islands some bar shows, keyed as its bar keys them: each display's
    /// list (its own, its named layout's or the shared one) and its layout
    /// switcher. Lists no bar shows (other named layouts, displays that are
    /// off or gone) make no island objects and nothing ticks for them. §593 the add page's ghost among them where one stands
    /// (<see cref="Seen"/>), made as any widget is.
    /// </summary>
    private IEnumerable<(string Key, BarItem Island)> PlacedIslands()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var window in windows)
        {
            var owner = Config.DisplayOwner(window.Id);
            foreach (var island in Seen.IslandsFor(window.Id).Where(island => island is not null))
            {
                var key = Config.IslandKey(owner, island.Id);
                if (seen.Add(key))
                {
                    yield return (key, island);
                }
            }
        }
    }

    /// <summary>
    /// The widgets' data follows the config: a source for every widget it names, on every bar, in
    /// every folder and in every layout, shown or not, from the dock's start; the sources no widget
    /// needs any more stop (DataSources). The islands take theirs when they are made.
    /// </summary>
    private void SyncSources()
    {
        try
        {
            DataSources.Shared.Want(config);
        }
        catch (Exception error)
        {
            Report(error);
        }
    }

    /// <summary>
    /// §359: what each island was last configured from — its own settings and the config's besides its layouts
    /// (<see cref="Config.SharedSaid"/>) — so it draws again only where that changed: a rebuild no longer makes
    /// every widget forget what it drew.
    /// </summary>
    private readonly Dictionary<string, string> islandsSaid = new(StringComparer.Ordinal);

    private void SyncIslands()
    {
        // §635: the add page's pick standing as a ghost is made showing the samples, everything inside it too (Islands.Make);
        // put in, it is a ghost no more and made again (Samples.Outlived).
        Samples.Ghost = Ghost is { Empty: false } shown ? shown.Key : null;
        // The performance islands (and their GPU-per-adapter split) are gone for now; 2298bf5 has them.
        var wanted = PlacedIslands().Where(entry => entry.Island.Kind.IsIsland()).ToDictionary(entry => entry.Key, entry => entry.Island);
        var shared = config.SharedSaid();
        foreach (var key in islandsSaid.Keys.Except(wanted.Keys).ToArray())
        {
            islandsSaid.Remove(key);
        }
        foreach (var key in islands.Keys.Except(wanted.Keys).ToArray())
        {
            // §637: a widget the bars no longer show is let go of once every bar has morphed past it: the leaving bar shows it
            // fading out meanwhile, from what it drew (MorphsOver).
            if (TaskbarLook.Motion.On)
            {
                lingering.Add(islands[key]);
            }
            else
            {
                islands[key].Dispose();
            }
            islands.Remove(key);
            islandTicks.Remove(key);
            islandShown.Remove(key);
            foreach (var token in islandTokens.Where(pair => pair.Value == key).Select(pair => pair.Key).ToArray()) islandTokens.Remove(token);
        }
        foreach (var (key, placed) in wanted)
        {
            // folders: a kind is fixed once an island is made (a folder's view is its kind), so an
            // island whose kind changed under the same key is made again rather than reconfigured.
            // §635: so is the add page's ghost put in: it showed the samples, the widget placed reads the person's own.
            if (islands.TryGetValue(key, out var current) && (current.Info != placed.Info || Samples.Outlived(current, key)))
            {
                current.Dispose();
                islands.Remove(key);
                islandTicks.Remove(key);
                islandShown.Remove(key);
                foreach (var token in islandTokens.Where(pair => pair.Value == key).Select(pair => pair.Key).ToArray()) islandTokens.Remove(token);
                current = null;
            }
            var said = $"{Config.Said(placed)}\n{shared}";
            if (current is not null)
            {
                current.Configure(placed, config);
                // Drawn again where what it draws from changed; a widget whose Configure says so itself forgets anyway.
                if (!islandsSaid.TryGetValue(key, out var was) || was != said)
                {
                    current.Forget();
                }
                islandsSaid[key] = said;
                GiveStatus(current);
                continue;
            }
            islandsSaid[key] = said;
            try
            {
                if (Islands.Create(key, placed, gfx, config, countdowns) is not { } made) continue;
                var token = nextIslandToken++;
                islandTokens[token] = key;
                islands[key] = made;
                made.Attach((nint)host, token);
                GiveStatus(made);
            }
            catch (Exception error) { Report(error); }
        }
        // §394: a widget gone takes its error with it; one renamed or moved is named as it is now.
        HearWidgetFaults();
    }
    /// <summary>§637: the widgets no bar shows any more, kept for the bars morphing past them; let go of once none is (<see cref="MorphsOver"/>).</summary>
    private readonly List<IIsland> lingering = [];

    /// <summary>
    /// §637: a bar's morph is over, or an arrange that may have started one is: once no bar morphs, the widgets the bars no longer
    /// show are let go of.
    /// </summary>
    private void MorphsOver()
    {
        if (lingering.Count == 0 || windows.Exists(window => window.IsMorphing))
        {
            return;
        }
        var gone = lingering.ToList();
        lingering.Clear();
        foreach (var island in gone)
        {
            try
            {
                island.Dispose();
            }
            catch (Exception error)
            {
                Report(error);
            }
        }
    }

    private void UpdateIslands()
    {
        // Which islands a bar on screen shows; hidden ones stop their
        // animations, so the compositor has nothing of them to move.
        var showing = new HashSet<string>(StringComparer.Ordinal);
        foreach (var window in windows)
        {
            if (!window.Shown || window.Fullscreen)
            {
                continue;
            }
            var shownLayout = window.Layout;
            for (var part = 0; part < shownLayout.Specs.Count; part++)
            {
                if (shownLayout.Placed(part))
                {
                    showing.Add(shownLayout.Specs[part].Key);
                }
            }
        }
        foreach (var (key, island) in islands)
        {
            var visible = showing.Contains(key);
            if (islandShown.TryGetValue(key, out var was) && was == visible)
            {
                continue;
            }
            islandShown[key] = visible;
            try
            {
                island.Visible(visible);
            }
            catch (Exception error)
            {
                Report(error);
            }
        }
        uint period = 0;
        var ticking = windows.Exists(window => window.Shown) && !windows.Exists(window => window.Fullscreen);
        if (ticking)
        {
            foreach (var island in islands.Values)
            {
                var interval = island.IntervalMs;
                if (interval > 0 && (period == 0 || (uint)interval < period))
                {
                    period = (uint)interval;
                }
            }
        }
        // The widgets' data is gathered from the dock's start, bars on screen or not, so a folder
        // opened or a layout shown later draws what was gathered meanwhile (DataSources).
        var gathering = (uint)Math.Max(0, DataSources.Shared.Period);
        if (gathering > 0 && (period == 0 || gathering < period))
        {
            period = gathering;
        }
        var back = ticking && !islandsTicking;
        islandsTicking = ticking;
        if (period != islandPeriod)
        {
            islandPeriod = period;
            if (period == 0)
            {
                Kill(ISLANDS);
                return;
            }
            Arm(ISLANDS, period);
        }
        // Back on screen: what the islands show is as old as the time they
        // were away.
        if (back && period > 0)
        {
            TickIslands();
        }
    }

    private void TickIslands()
    {
        if (islandPeriod == 0)
        {
            Kill(ISLANDS);
            return;
        }
        var at = Environment.TickCount64;
        // The sources first, so an island that ticks in this pass draws what was just read.
        DataSources.Shared.Tick(at, slack: islandPeriod / 2);
        if (!islandsTicking)
        {
            return;
        }
        var changed = false;
        foreach (var (section, island) in islands)
        {
            var interval = island.IntervalMs;
            if (interval <= 0)
            {
                continue;
            }
            // Each island waits for its own interval, less half a period for
            // the timer's jitter.
            if (islandTicks.TryGetValue(section, out var last) && at - last + islandPeriod / 2 < interval)
            {
                continue;
            }
            islandTicks[section] = at;
            try
            {
                changed |= island.Tick(gfx);
            }
            catch (Exception error)
            {
                Report(error);
            }
        }
        if (changed)
        {
            Rebuild();
        }
    }

    /// <summary>A drive came or went, or the machine woke: the islands read everything again.</summary>
    private void RefreshIslands()
    {
        // drives: a device came or went, so the list every drive widget reads is stale — forgotten
        // here once, for the bar's widgets and a folder's alike (DriveWatch.Changes).
        DriveWatch.Forget();
        // Every source measures from here, whether or not a widget of it is drawn.
        DataSources.Shared.Refresh(Environment.TickCount64);
        var changed = false;
        foreach (var island in islands.Values)
        {
            try
            {
                changed |= island.Refresh(gfx);
            }
            catch (Exception error)
            {
                Report(error);
            }
        }
        if (changed)
        {
            Rebuild();
        }
    }

    /// <summary>An island finished work it started elsewhere and posted WM_KARPO_ISLAND.</summary>
    private void IslandReady(nuint token)
    {
        // §658: taken before the refresh, so news that comes while it reads posts again (Islands.Post).
        Islands.Taken((nint)host, token);
        if (!islandTokens.TryGetValue(token, out var key) || !islands.TryGetValue(key, out var island)) return;
        // A shared store's Changed is raised from here, and an open
        // calendar or notification list follows it by itself.
        if (island.Refresh(gfx)) Rebuild();
        // §394: a read that failed, or worked again, is the edit bar's errors' news.
        HearWidgetFaults();
    }

    /// <summary>Session and display power changes, for the islands that count time.</summary>
    private void ToIslands(uint msg, WPARAM wParam, LPARAM lParam)
    {
        // The widgets' data hears it once, whether or not a widget of it is on a bar (DataSources).
        DataSources.Shared.System(msg, (nuint)wParam);
        foreach (var island in islands.Values)
        {
            try
            {
                island.System(msg, wParam, lParam);
            }
            catch (Exception error)
            {
                Report(error);
            }
        }
    }

    // The keyboard islands follow the window in front and its layout
    // without polling. KEYBOARD, a host timer, reads once more a moment
    // after an activation: with one input method for every window (Windows'
    // default), the window in front takes the current one only once it is there.
    private const uint KEYBOARD = 41;
    private const uint KEYBOARD_AGAIN_MS = 250;

    /// <summary>
    /// The shell hook said another window came to the front or a layout changed: the keyboard
    /// islands read the layout in use again, and with <paramref name="again"/> once more after
    /// <see cref="KEYBOARD_AGAIN_MS"/>.
    /// </summary>
    private void KeyboardChanged(bool again)
    {
        var any = false;
        foreach (var keyboard in Keyboards())
        {
            keyboard.Follow();
            any = true;
        }
        if (any && again)
        {
            Arm(KEYBOARD, KEYBOARD_AGAIN_MS);
        }
    }

    /// <summary>
    /// Every input indicator there is: the keyboard faces of the folders on the bars. A face's
    /// indicator is made by its folder rather than by the dock, and a layout belongs to a thread
    /// with no message to say it changed, so one the dock did not tell would show the layout it was
    /// made with for as long as it stood there.
    /// </summary>
    private IEnumerable<KeyboardIsland> Keyboards() =>
        islands.Values.OfType<FolderIsland>().Select(folder => folder.Keyboard).OfType<KeyboardIsland>();

    // --- Building ----------------------------------------------------------


    /// <summary>
    /// §612: the dress edit mode puts on the bars, the folders open and the dim now: none out of it, the Appearance's preview
    /// while its flyout is open (EditDress.cs).
    /// </summary>
    private EditDress Dress => EditDresses.Of(editor is not null, panelMemory.Page);

    /// <summary>§612: the dress the bars were last laid out in (<see cref="Rebuild"/>), which every bar's layout wears.</summary>
    private EditDress laidDress;

    /// <summary>Tests only: the dress the bars wear now.</summary>
    internal EditDress LaidDress => laidDress;

    private void Rebuild()
    {
        Kill(REBUILD);
        var fresh = ReadLook();
        if (fresh.Dark != look.Dark)
        {
            Menu.Theme(fresh.Dark);
        }
        look = fresh;
        // §499: the font Karpo's text is in, as the config says now (the look's): a font changed is another look, so every bar,
        // widget, open folder and menu below is measured and drawn in it again (the measures are kept by font, the kept
        // surfaces and elements by look or by font).
        gfx.Typeface = look.Typeface;
        // §612: every bar laid out in the dress edit mode wears now — the Appearance's preview drawn as out of it.
        laidDress = Dress;
        // §357: the views kept of folders no bar shows any more go.
        PruneFolderViews();
        // §334: a folder standing open wears the folders' look as it is now, where it stands; §612 an arranged one plain in the
        // Appearance's preview, as at rest.
        foreach (var open in openFolders)
        {
            open.Flyout.Relook(open.Window.Look);
            open.Flyout.Dress(laidDress != EditDress.Preview);
        }
        // §359: their surfaces are kept by style through the rebuild, drawn again only where what they show
        // changed (Rebuilding, Rebuilt). edit: and each hears whether the bars are arranged, which edit mode's
        // start and end rebuild for: a face with nothing to show is on the bar only then (§290); §612 not in the preview.
        foreach (var island in islands.Values)
        {
            if (island is CellIsland cells)
            {
                cells.Arranged = laidDress.Worn();
            }
            island.Rebuilding();
        }
        // places: an edge whose places want other lengths now is shared again once all are built.
        holdReshare = true;
        try
        {
            foreach (var window in windows)
            {
                try
                {
                    // pins: this bar's app buttons, before it is laid out for them.
                    window.DeriveTiles();
                    window.Build();
                }
                catch (Exception error)
                {
                    Report(error);
                }
            }
        }
        finally
        {
            holdReshare = false;
        }
        var held = reshareHeld.ToList();
        reshareHeld.Clear();
        foreach (var (id, edge) in held)
        {
            Reshare(id, edge);
        }
        // §359: every bar laid out: the styles none asked for are set aside.
        foreach (var island in islands.Values)
        {
            island.Rebuilt();
        }
        if (tray is not null && windows.Count > 0)
        {
            // Tray icons at the size the densest display draws them, from
            // the next picture an app sends.
            // tray: a cell's picture is the bar's system icons' size, so the icon is asked for at that
            // size and the app's own artwork is used rather than another size stretched.
            tray.IconSize = (int)MathF.Ceiling(windows.Max(window => window.Layout.TrayIconSize));
            // The tray's places as this layout draws them.
            // places: the window last used where it shows the tray, else the first that does.
            bool Tray(DockWindow window) => window.Layout.Specs.Any(spec => spec.Kind == BarKind.Tray);
            PublishTrayRects(lastWindow is { } recent && windows.Contains(recent) && Tray(recent) ? recent : windows.Find(Tray) ?? windows[0]);
        }
        // After every window: icons one display didn't use, another may have.
        icons.Sweep();
        TickClock();
        UpdateIslands();
        // tray: a drag going on shows itself again on the bars laid out again (the chevron it brought, Dock.Tray.cs).
        if (trayDrag is not null)
        {
            ShowTrayDrag(Cursor());
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }
        disposed = true;
        // §394: the account layer's news is no longer heard here.
        UnhookFaults();
        // §491: a launch closes nothing of this dock's any more.
        UnhookLaunches();
        // Nothing may reach the dock from here on: messages the teardown
        // causes take the default path.
        busy = true;
        // What the last frames asked of a window, done now: the loop that would have waited for
        // their commits is over. Everything after this is done at once.
        Surfaces.Flush();
        Surfaces.Around = null;
        Shutdown();
        // desktops: no line reads this dock's desktops any more.
        if (DesktopState.Service == desktopsService)
        {
            DesktopState.Service = null;
        }
        foreach (var window in windows)
        {
            window.Dispose();
        }
        windows = [];
        // §534: where a bar carried would have landed.
        landing?.Dispose();
        landing = null;
        foreach (var island in islands.Values)
        {
            try
            {
                island.Dispose();
            }
            catch (Exception error)
            {
                Report(error);
            }
        }
        islands.Clear();
        // §637: and those kept for a morph.
        foreach (var island in lingering)
        {
            try
            {
                island.Dispose();
            }
            catch (Exception error)
            {
                Report(error);
            }
        }
        lingering.Clear();
        // The widgets' data goes with the dock.
        DataSources.Shared.Clear();
        // screentime: counted up to now and saved.
        ScreenTimeTracker.Shared.Stop();
        // appbar: the full screen look's hooks let go.
        frontWatch.Stop();
        shellPopups.Stop();
        WindowsPanels.Instance.FrontChanged -= FacesShowOpen;
        WindowsPanels.Instance.Unwatch();
        systemStatus?.Dispose();
        systemStatus = null;
        StopWatchingWindowsPins();
        if (sessionNotify)
        {
            WTSUnRegisterSessionNotification(host);
            sessionNotify = false;
        }
        if (driveNotices is { } notices)
        {
            if (ReferenceEquals(Drives.DriveHolds.Shared.Notices, notices))
            {
                Drives.DriveHolds.Shared.Notices = null;
            }
            driveNotices = null;
        }
        if (energySaverNotify is { } following)
        {
            Karpo.Dock.QuickSettings.Services.EnergySaver.Unfollow(following);
            energySaverNotify = null;
        }
        if (brightnessNotify is { } brightnessFollowing)
        {
            Karpo.Dock.QuickSettings.Services.EnergySaver.Unfollow(brightnessFollowing);
            brightnessNotify = null;
        }
        if (watch is HANDLE folder)
        {
            FindCloseChangeNotification(folder);
            watch = null;
        }
        desktops?.Dispose();
        apps.Dispose();
        // menus: the popups' hook no longer asks this dock about its bars.
        FlyoutHost.PressesHandledBy = null;
        FlyoutHost.Opening = null;
        SetWindowLongPtrW(host, GWLP.GWLP_USERDATA, 0);
        if (MainWindow == host)
        {
            // Destroyed here only when quit didn't already; no second quit.
            MainWindow = HWND.NULL;
        }
        Surfaces.Gone(host);
        if (cell.IsAllocated)
        {
            cell.Free();
        }
        gfx.Dispose();
    }

    [UnmanagedCallersOnly]
    private static LRESULT WndProc(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        if (msg == WM.WM_DESTROY && hwnd == MainWindow)
        {
            PostQuitMessage(0);
            return 0;
        }
        // The announcement Karpo's own tray makes to other apps reaches this
        // thread's windows before it returns. It is not an Explorer restart,
        // and taken for one it would be announced again.
        if (TaskbarCreatedMessage != 0 && msg == TaskbarCreatedMessage && Tray.Broadcasting)
        {
            return 0;
        }
        // §667: Windows asking the host, a shell hook window, where a window's button is: MinRect's own window answers
        // that, after it; the host lets the question go by, rather than have DefWindowProc ask it again as a shell message.
        if (msg == MinRect.WM_KLUDGEMINRECT)
        {
            return 0;
        }
        // Every window's place in the z-order the plan's, whatever the dock is doing — busy, a menu closing
        // hands the arranged folder the focus back (§342), a menu shown again takes it (§361).
        if (msg == WM.WM_WINDOWPOSCHANGING)
        {
            Surfaces.Keep(hwnd, (WINDOWPOS*)(nint)lParam);
        }
        try
        {
            var raw = GetWindowLongPtrW(hwnd, GWLP.GWLP_USERDATA);
            if (raw != 0 && GCHandle.FromIntPtr(raw).Target is Dock dock)
            {
                // narrator: a screen reader asking for a window's elements, and UI Automation's own
                // threads asking for its tree — answered whatever the dock is doing, from what the
                // window registered (Accessibility\Uia.cs); a window going takes its elements with it.
                if (msg == WM.WM_GETOBJECT && Uia.Answer(hwnd, wParam, lParam) is { } provided)
                {
                    return provided;
                }
                if (msg == Uia.WM_KARPO_UIA_TREE)
                {
                    return Uia.Rebuild(hwnd, now: !dock.busy);
                }
                if (msg == WM.WM_NCDESTROY)
                {
                    Uia.Forget(hwnd);
                }
                // power: energy saver's status, kept whatever the dock is doing — busy in a menu it
                // would be lost with the broadcast, and every view reads it (EnergySaver.cs).
                if (msg == WM.WM_POWERBROADCAST && (uint)(nuint)wParam == Karpo.Dock.QuickSettings.Services.EnergySaver.PbtPowerSettingChange)
                {
                    _ = Karpo.Dock.QuickSettings.Services.EnergySaver.Shared.Heard((POWERBROADCAST_SETTING*)(nint)lParam);
                    // quick: so is the built-in screen's brightness, which its worker reads off this thread.
                    _ = Karpo.Dock.QuickSettings.Services.Brightness.HeardIfMade((POWERBROADCAST_SETTING*)(nint)lParam);
                }
                // screentime: the lock, the sleep and a display going dark, counted whatever the dock
                // is doing — one lost in a menu's loop would count the wrong time (ScreenTimeTracker).
                if (hwnd == dock.host && msg is WM.WM_POWERBROADCAST or WM.WM_WTSSESSION_CHANGE)
                {
                    ScreenTimeTracker.Shared.System(msg, wParam, lParam);
                    // appbar: the same notice says a display lit again; the bars look after their strips.
                    if (Lit(msg, wParam, lParam))
                    {
                        dock.DisplayLit();
                    }
                }
                // §557: a drive about to go — ejected, locked, dismounted — is let go of whatever the dock is
                // doing: Windows waits for the answer, and a folder followed on it would stop the eject
                // (DriveHolds). What the change means for the islands is the host's as before.
                if (hwnd == dock.host && msg == WM.WM_DEVICECHANGE)
                {
                    var (what, notice) = Drives.DriveHolds.Read(wParam, lParam);
                    if (Drives.DriveHolds.Shared.Heard(what, notice) && what == Drives.DriveNotice.LetGo)
                    {
                        return 1;
                    }
                }
                if (!dock.busy)
                {
                    dock.busy = true;
                    try
                    {
                        // Every message handled is a frame: what it asks of a window reaches the
                        // screen once its content has (Surfaces, §354).
                        // §359: and what it changed of the edit chrome reconciled once, as it ends.
                        using var frame = Surfaces.Begin(dock.gfx.Compositor, dock.PresentChrome);
                        if (dock.Dispatch(hwnd, msg, wParam, lParam) is LRESULT result)
                        {
                            return result;
                        }
                    }
                    finally
                    {
                        dock.busy = false;
                    }
                }
                // The session ends with the process, before any modal loop
                // gives the dock back: the taskbar has to go back now.
                else if (hwnd == dock.host && msg is WM.WM_QUERYENDSESSION or WM.WM_ENDSESSION)
                {
                    if (msg == WM.WM_QUERYENDSESSION)
                    {
                        dock.SessionEnding();
                        return 1;
                    }
                    dock.SessionEnded((nuint)wParam != 0);
                    return 0;
                }
                // Inside a menu's or dialog's modal loop the dock is already
                // busy. What changes state is kept for afterwards; the rest
                // takes the default path.
                else if (Deferrable(msg, wParam)
                    // The flyout's own posts (a click outside, a submenu's
                    // choice): a shell call pumping messages meanwhile must
                    // not lose them.
                    || (dock.flyout is { } menu && hwnd == menu.Hwnd && msg is >= WM.WM_USER and < WM.WM_APP)
                    // The calendar's and the notification list's hosts post the same way.
                    || (dock.IslandPopupOwns(hwnd) && msg is >= WM.WM_USER and < WM.WM_APP))
                {
                    Deferred.Add((hwnd, msg, wParam, Kept(msg, lParam)));
                    return 0;
                }
            }
        }
        catch (Exception error)
        {
            // An exception must not unwind into user32.
            Report(error);
        }
        // A close never goes on to destroy a window of the dock's (Closing.cs).
        return Closing.Default(hwnd, msg, wParam, lParam);
    }

    /// <summary>
    /// appbar: a display lit again — the power setting notice ScreenTimeTracker asks for
    /// (GUID_SESSION_DISPLAY_STATUS), saying on (1). Dimmed (2) and off (0) are not.
    /// </summary>
    internal static unsafe bool Lit(uint msg, WPARAM wParam, LPARAM lParam)
    {
        if (msg != WM.WM_POWERBROADCAST || (uint)(nuint)wParam != PBT.PBT_POWERSETTINGCHANGE || (nint)lParam == 0)
        {
            return false;
        }
        var setting = (POWERBROADCAST_SETTING*)(nint)lParam;
        return setting->PowerSetting == GUID.GUID_SESSION_DISPLAY_STATUS
            && setting->DataLength >= sizeof(uint)
            && *(uint*)&setting->Data == 1;
    }

    /// <summary>quick: a power setting notification of the built-in screen's brightness, which Brightness follows alone (HeardIfMade).</summary>
    internal static unsafe bool BrightnessOnly(WPARAM wParam, LPARAM lParam) =>
        (uint)(nuint)wParam == PBT.PBT_POWERSETTINGCHANGE
        && Karpo.Dock.QuickSettings.Services.Brightness.Read((POWERBROADCAST_SETTING*)(nint)lParam) is not null;

    /// <summary>
    /// What a message kept for later keeps of its lParam: nothing where it points at data that is
    /// gone by then (the text of a setting, a device's, a power setting's), else all of it.
    /// </summary>
    internal static LPARAM Kept(uint msg, LPARAM lParam) =>
        msg is WM.WM_SETTINGCHANGE or WM.WM_DEVICECHANGE or WM.WM_POWERBROADCAST or WM.WM_DPICHANGED ? (LPARAM)0 : lParam;

    internal static bool Deferrable(uint msg, WPARAM wParam) =>
        (ShellHookMessage != 0 && msg == ShellHookMessage)
        || (TaskbarCreatedMessage != 0 && msg == TaskbarCreatedMessage)
        || msg is WM_KARPO_APPBAR or WM.WM_SETTINGCHANGE or WM.WM_DISPLAYCHANGE or WM.WM_TIMECHANGE
        // A display's scale changed while a menu was open: the arrange it asks for is owed afterwards
        // (it reads nothing of the suggested rectangle its lParam points at, so none is kept).
        || msg == WM.WM_DPICHANGED
        // The machine came back from sleep or hibernation while a menu or dialog was open (one left
        // open over a sleep): the clock, the alarms, the islands and the strips are put right after it.
        || (msg == WM.WM_POWERBROADCAST && (uint)(nuint)wParam is PBT.PBT_APMRESUMEAUTOMATIC or PBT.PBT_APMRESUMESUSPEND)
        // appbar: the window in front changed or moved, Explorer ended (FrontWatch, Taskbar); §619: a window minimized.
        || msg is FrontWatch.WM_KARPO_FRONT or FrontWatch.WM_KARPO_MINIMIZED or Taskbar.WM_KARPO_EXPLORER_GONE
        // network: an address came or went while a menu was open (Dock.Network.cs).
        || msg == WM_KARPO_NETWORK
        // §491: a launch off the dock's thread while a menu was open: what is open closes afterwards (Dock.Leave.cs).
        || msg == Launch.WM_KARPO_LEAVE
        // Like WM_SETTINGCHANGE, these change what the dock looks like.
        || msg is WM.WM_SYSCOLORCHANGE or WM.WM_THEMECHANGED or WM.WM_DWMCOLORIZATIONCOLORCHANGED
        // What the tray, the system status and the islands post, and the lock
        // and unlock edit mode and the bars follow.
        || msg is Tray.WM_KARPO_TRAY or SystemStatus.WM_KARPO_SYSTEM or Islands.WM_KARPO_ISLAND or Taskband.WM_KARPO_SIGNALS
        || msg is TaskbarKeys.WM_KARPO_KEYS or Uia.WM_KARPO_UIA_ACT
        || msg is WM.WM_WTSSESSION_CHANGE
        // A second start of Karpo.exe asking for ⚙ Settings (Dock.EditEntries.cs).
        || (SettingsMessage != 0 && msg == SettingsMessage)
        // A keyboard layout switched while a flyout of Karpo's had the keys.
        || msg == WM.WM_INPUTLANGCHANGE
        // A drive coming or going changes the drives island.
        || (msg == WM.WM_DEVICECHANGE && (uint)(nuint)wParam is DBT_DEVICEARRIVAL or DBT_DEVICEREMOVECOMPLETE)
        // The charger, the charge or energy saver changed while a menu was open: the battery, its
        // status icon and the energy saver switch draw it afterwards (energy saver's status itself
        // is kept at once, EnergySaver.cs). They read only wParam.
        || (msg == WM.WM_POWERBROADCAST && (uint)(nuint)wParam is PBT.PBT_APMPOWERSTATUSCHANGE or Karpo.Dock.QuickSettings.Services.EnergySaver.PbtPowerSettingChange)
        // A quit asked (Karpo.exe --quit), arriving while a menu is open, still has
        // to take the way out that puts the taskbar back (Closing.cs).
        || msg == WM.WM_CLOSE;

    private static void Report(Action action)
    {
        try
        {
            action();
        }
        catch (Exception error)
        {
            Report(error);
        }
    }

    /// <summary>
    /// An error the dock caught and carries on from: to karpo.log, with its stack the first time,
    /// and rate-limited by what it is, so one that comes on every frame can't fill the file
    /// (Troubles.Report). Until it was written down, all of these went to a console a windowed
    /// program doesn't have.
    /// </summary>
    private static void Report(Exception error) => Troubles.Report("dock, caught and carried on", error);

    /// <summary>Throws for a failed Win32 call, as the Rust <c>?</c> does.</summary>
    private static void Check(BOOL done, [CallerArgumentExpression(nameof(done))] string? call = null)
    {
        if (!done)
        {
            throw LastError(call);
        }
    }

    private static NativeException LastError(string? call)
    {
        var code = Marshal.GetLastSystemError();
        return new NativeException($"{call} failed: error {code}", HRESULT_FROM_WIN32(code).Value);
    }

    private static Change Max(Change a, Change b) => (Change)Math.Max((int)a, (int)b);

    /// <summary>
    /// The Rust compares whole configs. Comparing what would be written keeps
    /// that true for every setting, including ones added later.
    /// </summary>
    private static bool Same(Config a, Config b) =>
        JsonSerializer.Serialize(a, DockJson.Default.Config) == JsonSerializer.Serialize(b, DockJson.Default.Config);

    /// <summary>
    /// <paramref name="b"/> is <paramref name="a"/> with other recent colours (the custom colours mixed last) and nothing else
    /// changed: what <see cref="Recolour"/> can show without building anything. §459: a style's colours are built — they make
    /// cards, a plate's text, the panels' and each part's own look —, as a text or an accent always were.
    /// </summary>
    internal static bool OnlyColorsDiffer(Config a, Config b)
    {
        // §696 (the person, 2026-11-12: "when changing the color it is very laggy"): the Appearance's colours too — a
        // background, an outline, a text, an accent, a backdrop, an attached folder's ground, in any palette and either mode —,
        // which counted as any change until now, so every step of a colour dragged or tried on built every bar again (60 to
        // 80 ms). Its pattern's colour is no colour here: a pattern's ink is drawn into its layer at a build.
        if (Same(a, b))
        {
            return false;
        }
        static Config Colourless(Config of)
        {
            var copy = of.Clone();
            copy.RecentColors = null;
            if (copy.Appearance?.Palettes is { } palettes)
            {
                foreach (var (id, palette) in palettes.ToList())
                {
                    LookValues? Off(LookValues? part)
                    {
                        if (part is not null)
                        {
                            (part.Background, part.Outline, part.Text, part.Accent, part.Backdrop, part.Ground) = (null, null, null, null, null, null);
                        }
                        // What held colours alone holds nothing now: as never written.
                        return part is { IsEmpty: false } ? part : null;
                    }
                    (palette.Taskbar, palette.Widgets, palette.Flyouts, palette.FolderWidgets) = (Off(palette.Taskbar), Off(palette.Widgets), Off(palette.Flyouts), Off(palette.FolderWidgets));
                    if (palette is { Taskbar: null, Widgets: null, Flyouts: null, FolderWidgets: null })
                    {
                        palettes.Remove(id);
                    }
                }
                if (palettes.Count == 0)
                {
                    copy.Appearance!.Palettes = null;
                }
            }
            // An Appearance holding nothing now: as none.
            if (copy.Appearance is { } appearance && Config.AppearanceSaid(copy) == Config.AppearanceSaid(new Config { Appearance = new Appearance() }))
            {
                copy.Appearance = null;
            }
            return copy;
        }
        return Same(Colourless(a), Colourless(b));
    }

    /// <summary>
    /// §696: whether colours <paramref name="laid"/> become <paramref name="now"/> by their brushes alone (Scene.Recolour): the
    /// grounds, the cards and the outlines another colour, each still there or still not; never what a widget draws into its
    /// own surface (its text, its accent, its highlight, the panels' text) nor a pattern's ink (drawn into its layer).
    /// </summary>
    internal static bool InPlace(Palette laid, Palette now) =>
        now.Text == laid.Text && now.Accent == laid.Accent && now.Highlight == laid.Highlight && now.PanelText == laid.PanelText
            && now.TaskbarPattern == laid.TaskbarPattern && now.CardPattern == laid.CardPattern
            && now.Taskbar.HasValue == laid.Taskbar.HasValue && now.Flyout.HasValue == laid.Flyout.HasValue
            && now.Card.HasValue == laid.Card.HasValue && now.CardOutline.HasValue == laid.CardOutline.HasValue
            && now.FlyoutOutline.HasValue == laid.FlyoutOutline.HasValue && now.TaskbarOutline.HasValue == laid.TaskbarOutline.HasValue;

    /// <summary>§691: a colour kept shows on every bar: one that does not is said in karpo.log and built again.</summary>
    private void Shown()
    {
        foreach (var window in windows.Where(window => !window.ShowsItsLook))
        {
            Troubles.Write($"Look: a bar ({window.Name}) did not show the colours just kept; built again"); // not-ui: karpo.log
            window.Build();
        }
    }

    /// <summary>
    /// The config's colours changed and nothing else: every bar's brushes set to them in place
    /// (<see cref="Scene.Recolour"/>). False, with nothing done, when Windows' own look changed too
    /// since the bars were built, or a bar's look changed by more than its colours (a see-through
    /// colour settles its material otherwise, <see cref="Scene.Settle"/>): then they are built again,
    /// as for any change.
    /// </summary>
    private bool Recolour()
    {
        if (ReadLook() != look)
        {
            unrecoloured = "Windows' look changed"; // not-ui: karpo.log
            return false;
        }
        if (windows.FirstOrDefault(window => !window.Recolours) is { } refusing)
        {
            unrecoloured = refusing.WhyNotRecoloured();
            return false;
        }
        foreach (var window in windows)
        {
            window.Recolour();
        }
        return true;
    }


    private static RECT Rect(RectI rect) => new()
    {
        left = rect.Left,
        top = rect.Top,
        right = rect.Right,
        bottom = rect.Bottom,
    };

    /// <summary>
    /// How Windows is styled right now, as the dock follows it: Windows mode
    /// (not app mode) for dark, the accent shades, transparency, contrast,
    /// animations (and Karpo's own, §489, Look.Moving) and text size, and the bar's material as the config asks,
    /// settled against what Windows allows. Glass only when transparency
    /// effects are on and contrast is not high, as Windows does for its own
    /// surfaces; with effects off the host backdrop comes out black.
    /// </summary>
    private Look ReadLook() => ReadLook(options.Glass).Configured(config);

    /// <inheritdoc cref="ReadLook()"/>
    private static Look ReadLook(bool allowed)
    {
        UISettings? settings = null;
        try
        {
            settings = new UISettings();
        }
        catch (Exception)
        {
        }
        var effects = false;
        var animations = true;
        var textScale = 1f;
        // Windows 11's own blue, for when the settings can't be read: Karpo's (§547).
        static Color Of(Argb color) => Color.FromArgb(color.A, color.R, color.G, color.B);
        var (accent, light2, dark1, dark2) = (Of(KarpoLook.Accent), Of(KarpoLook.AccentLight2), Of(KarpoLook.AccentDark1), Of(KarpoLook.AccentDark2));
        var (light3, dark3) = (Of(KarpoLook.AccentLight3), Of(KarpoLook.AccentDark3));
        if (settings is not null)
        {
            Try(() => effects = settings.AdvancedEffectsEnabled);
            Try(() => animations = settings.AnimationsEnabled);
            Try(() => textScale = (float)settings.TextScaleFactor);
            Try(() => accent = settings.GetColorValue(UIColorType.Accent));
            Try(() => light2 = settings.GetColorValue(UIColorType.AccentLight2));
            Try(() => dark1 = settings.GetColorValue(UIColorType.AccentDark1));
            Try(() => dark2 = settings.GetColorValue(UIColorType.AccentDark2));
            Try(() => light3 = settings.GetColorValue(UIColorType.AccentLight3));
            Try(() => dark3 = settings.GetColorValue(UIColorType.AccentDark3));
        }
        // Missing means light, as Windows reads it.
        var dark = ReadDword(Personalize, "SystemUsesLightTheme") == 0;
        var prevalence = ReadDword(Personalize, "ColorPrevalence") == 1;
        var contrast = new HIGHCONTRASTW { cbSize = (uint)sizeof(HIGHCONTRASTW) };
        bool read = SystemParametersInfoW(SPI.SPI_GETHIGHCONTRAST, contrast.cbSize, &contrast, 0);
        var high = read && (contrast.dwFlags & HCF.HCF_HIGHCONTRASTON) != 0;
        var glass = allowed && effects && !high;
        // High contrast draws the system's colours and nobody's own, as Windows asks of every app.
        // §567: the taskbar's look is the Appearance over Karpo's own (§547), which each bar puts on (TaskbarLook); this one is
        // Windows' alone (Plain), what the settings wear (§526).
        return new Look(
            Glass: glass,
            Dark: dark,
            Accent: accent,
            AccentLight2: light2,
            AccentDark1: dark1,
            AccentDark2: dark2,
            AccentLight3: light3,
            AccentDark3: dark3,
            AccentOnSurface: prevalence && dark,
            HighContrast: high,
            HcWindow: SystemColor(COLOR.COLOR_WINDOW),
            HcText: SystemColor(COLOR.COLOR_WINDOWTEXT),
            HcHighlight: SystemColor(COLOR.COLOR_HIGHLIGHT),
            HcHighlightText: SystemColor(COLOR.COLOR_HIGHLIGHTTEXT),
            Animations: animations,
            TextScale: textScale > 0 ? textScale : 1f).Plain();
    }

    /// <summary>
    /// What every bar is drawn in, and every folder, menu and flyout opened from it: §547 Karpo's own look, of Windows' look as
    /// the dock read it last (<see cref="look"/>) only its high contrast, text size and motion, in §567 Karpo's one Appearance
    /// (<see cref="Look.Wearing(Config)"/>).
    /// </summary>
    internal Look TaskbarLook => look.Wearing(config);

    /// <summary>
    /// §526 (the person, 2026-11-07: "the settings should never take the style of the user, they should always take the style
    /// that we have for it"): what Karpo's settings are drawn in — the edit bar and its panel, the sections' edit bars, every
    /// menu of edit mode (<see cref="ShownIn"/>) and what opens from them: <see cref="SettingsLookOf"/> of Windows' look as read.
    /// </summary>
    private Look SettingsLook => SettingsLookOf(look);

    /// <summary>
    /// §526: Karpo's settings' look of <paramref name="windows"/>, Windows' look as the dock reads it (<see cref="ReadLook()"/>):
    /// Windows' own alone — its mode, its accent, high contrast, transparency effects, its text size, and Karpo's motion —, no
    /// Appearance over it (no colours, no outline, no text colour, its default font Segoe UI Variable, not the Appearance's
    /// menus'). An accent once put on (<see cref="Look.Dressed"/>) is not taken off again: of Windows' look alone.
    /// </summary>
    internal static Look SettingsLookOf(Look windows) => windows.Plain();

    /// <summary>The Rust ignores a style it can't read and keeps the default; so does this.</summary>
    private static void Try(Action read)
    {
        try
        {
            read();
        }
        catch (Exception)
        {
        }
    }

    private static uint? ReadDword(string subkey, string value)
    {
        uint data = 0;
        var size = (uint)sizeof(uint);
        fixed (char* key = subkey)
        fixed (char* name = value)
        {
            var status = RegGetValueW(HKEY.HKEY_CURRENT_USER, key, name, RRF.RRF_RT_REG_DWORD, null, &data, &size);
            return status == ERROR.ERROR_SUCCESS ? data : null;
        }
    }

    /// <summary>A COLORREF is 0x00BBGGRR.</summary>
    private static Color SystemColor(int index)
    {
        var bgr = GetSysColor(index);
        return Color.FromArgb(255, (byte)bgr, (byte)(bgr >> 8), (byte)(bgr >> 16));
    }

    /// <summary>
    /// EcoQoS while idle. If anything does wake the process, the scheduler is
    /// free to run it slowly on an efficiency core.
    /// </summary>
    private static void Efficiency(bool on)
    {
        var state = new PROCESS_POWER_THROTTLING_STATE
        {
            Version = PROCESS.PROCESS_POWER_THROTTLING_CURRENT_VERSION,
            ControlMask = PROCESS.PROCESS_POWER_THROTTLING_EXECUTION_SPEED,
            StateMask = on ? (uint)PROCESS.PROCESS_POWER_THROTTLING_EXECUTION_SPEED : 0,
        };
        SetProcessInformation(
            GetCurrentProcess(),
            PROCESS_INFORMATION_CLASS.ProcessPowerThrottling,
            &state,
            (uint)sizeof(PROCESS_POWER_THROTTLING_STATE));
    }

    // wtsapi32, which TerraFX has no binding for.
    [DllImport("wtsapi32.dll", ExactSpelling = true)]
    private static extern int WTSRegisterSessionNotification(HWND hWnd, uint dwFlags);

    [DllImport("wtsapi32.dll", ExactSpelling = true)]
    private static extern int WTSUnRegisterSessionNotification(HWND hWnd);

    /// <summary>
    /// The dock on one display: its window, where it stands, what it shows,
    /// and whether the pointer is on it. (folders: its part in folders and the
    /// edit mode's "Add" is in Dock.Folders.cs.)
    /// </summary>
    private sealed partial class DockWindow : IDisposable, IPreviewTimers, IEdgeWindow, IKeptBar
    {
        private readonly Dock dock;
        private MonitorInfo monitor;
        private Screen screen;
        private DesktopWindowTarget? target;

        /// <summary>What the window's target shows, its scene inside it, kept from scene to scene.</summary>
        private ContainerVisual? host;

        /// <summary>§359: the window its tooltip is drawn in, standing where it stands: its own region shows nothing past the panel.</summary>
        private BarTip? tipWindow;
        private Layout layout;
        private Scene scene;
        private bool dropTarget;

        /// <summary>
        /// appbar: this bar's registration with the shell and the strip it holds, kept held by
        /// looking at the work area rather than trusting the shell's answers (AppbarHolder, BarKeeper).
        /// </summary>
        private readonly AppbarHolder holder;

        /// <summary>
        /// The shell keeps a strip of this display out of the work area for this bar: what
        /// <see cref="Area"/> asks, since the empty strip a hiding bar holds takes nothing.
        /// </summary>
        private bool HoldsSpace => holder.HoldsSpace;
        private bool hovering;
        private bool leaving;
        // Kept as the Rust keeps it: a peek in progress, which nothing reads yet.
#pragma warning disable CS0414
        private bool peeking;
#pragma warning restore CS0414
        private bool dragging;

        public Screen Screen => screen;
        private Vector2 pointer;
        private Hit? hovered;
        private Hit? pressed;
        private Sizing? sizing;
        private bool disposed;

        // Edit mode: the block being dragged, by key so a rebuild keeps
        // it, where it was taken hold of along it, where the press was and
        // whether the pointer has left the drag threshold since; a press on a
        // badge or zone waiting for its release; the block with the keyboard
        // focus and whether the keys moved it there (the focus rectangle shows
        // then only).
        private string? dragKey;
        private float grabOffset;
        private Vector2 pressAt;
        private bool dragMoved;

        // folders: whether the block being carried is over an open folder, which marks the place.
        private bool overFolder;
        private string? focusKey;

        private bool focusShown;
        private string? optionsClosed;

        // Desktop buttons dragged to a new place get the same live feedback.

        // §358: the window as it stands, still at its edge's most thickness (Frame); null until laid out.
        private RECT? frame;

        // The tray icon under the pointer and whether its popup is open.
        private readonly TrayHover trayHover = new();

        /// <summary>A drag of the bar's inner edge in progress.</summary>
        /// <param name="From">The height, in DIPs, before the drag.</param>
        /// <param name="Offset">The bar's thickness less the pointer's distance from the screen edge, in px, when it began.</param>
        private sealed class Sizing(int from, float offset)
        {
            public int From { get; } = from;
            public float Offset { get; } = offset;
            /// <summary>The height, in DIPs, the pointer asks for now.</summary>
            public int Target { get; set; } = from;
        }

        public HWND Hwnd { get; }

        public bool Resizing => sizing is not null;

        public bool Shown { get; private set; }

        /// <summary>
        /// appbar: out of the way of a full screen app on its display (<see cref="MakeWay"/>): hidden,
        /// laid out no more, its strip and its z-order left alone until the app is gone (BarKeeper).
        /// </summary>
        public bool Fullscreen { get; private set; }

        /// <summary>appbar: the display this window is on, as the full screen look names it (\\.\DISPLAYn).</summary>
        string IKeptBar.Display => Stage.On ? "" : monitor.DeviceName;

        /// <summary>appbar: this window's appbar, where it holds one (<see cref="HoldsAppbar"/>).</summary>
        AppbarHolder? IKeptBar.Holder => HoldsAppbar && !Stage.On ? holder : null;

        bool IKeptBar.MadeWay => Fullscreen;

        public string Id => monitor.Id;

        /// <summary>The owner of the list this window shows, as its keys name it: its display's (<see cref="Config.DisplayOwner"/>).</summary>
        public string Owner => Config.DisplayOwner(screen.Id);

        /// <summary>§519: the display this window stands on, in screen px: which display a carried block is over.</summary>
        public RECT ScreenBounds => new() { left = monitor.Bounds.Left, top = monitor.Bounds.Top, right = monitor.Bounds.Right, bottom = monitor.Bounds.Bottom };

        public Layout Layout => layout;

        public Scene Scene => scene;

        /// <summary>The scene's colours set to its look's (<see cref="Look"/>), which differs from what it was built with in those alone (Dock.Recolour).</summary>
        public void Recolour()
        {
            laidLook = Dressed();
            scene?.Recolour(laidLook);
            // §547: the Taskbar's Background blur is a colour's change too: the strip blurred again.
            Wallpaper();
        }

        /// <summary>§691: whether its scene shows the look it is to wear now (none built yet: nothing to show).</summary>
        internal bool ShowsItsLook => scene is not { } shown || !laidOut || shown.Shown == Dressed();

        /// <summary>
        /// Whether its look now differs from the one it was laid out in by its colours alone, which <see cref="Recolour"/> shows
        /// in place. §696: its patterns' inks as they were (a pattern is drawn in its ink into its layer at a build).
        /// </summary>
        /// <summary>§696: what of its look changed beyond what <see cref="Recolour"/> shows in place, for karpo.log.</summary>
        internal string WhyNotRecoloured()
        {
            var now = Dressed();
            var differ = typeof(Look).GetProperties()
                .Where(property => property.Name is not (nameof(Look.Dressing) or nameof(Look.Worn)) && property.GetIndexParameters().Length == 0)
                .Where(property => !Equals(property.GetValue(now), property.GetValue(laidLook)))
                .Select(property => property.Name)
                .Concat(typeof(Palette).GetProperties()
                    .Where(property => !Equals(property.GetValue(now.Colors), property.GetValue(laidLook.Colors)))
                    .Select(property => "Colors." + property.Name));
            return $"{Name}: {string.Join(", ", differ)}";
        }

        public bool Recolours => Dressed() is var now && now with { Colors = laidLook.Colors, Dressing = laidLook.Dressing, Worn = laidLook.Worn } == laidLook
            && Dock.InPlace(laidLook.Colors, now.Colors);

        /// <summary>The look it is laid out in: its own (<see cref="Look"/>); a place, and a bar over the windows, never clear (HoverPlaces.Dressed).</summary>
        private Look Dressed() => Mode == EdgeMode.Reserve || IsAnchor ? Look : HoverPlaces.Dressed(Look);

        // §345: the look it was last laid out or recoloured in (Recolours).
        private Look laidLook;

        /// <summary>§345: the layout its display shows, whose look and app buttons this bar wears.</summary>
        public TaskbarLayout ShownLayout => dock.config.ActiveLayout(Id);

        /// <summary>What this bar is drawn in, and every menu and flyout opened from it (<see cref="Dock.TaskbarLook"/>).</summary>
        public Look Look => dock.TaskbarLook;

        /// <summary>The pointer, a drop or a resize is on this window.</summary>
        public bool Engaged => hovering || dragging || sizing is not null || dragKey is not null;

        /// <summary>edit: a block of this bar is being carried — over the bar, or over an open folder.</summary>
        public bool Carrying => dragKey is not null;

        /// <summary>The display's friendly name, for the settings it has in the config.</summary>
        public string Name => monitor.Name;

        /// <summary>The display, in screen px.</summary>
        public RECT Bounds => Rect(monitor.Bounds);

        /// <summary>The display as the last arrange listed it.</summary>
        internal MonitorInfo Monitor => monitor;

        /// <summary>The display's own scale, which the popups opened from this window are drawn at.</summary>
        public float UiScale => Scale;

        /// <summary>
        /// Edit mode is on here: it is every display's at once, its bars, anchors and places.
        /// </summary>
        private bool Editing => dock.editor is not null;

        /// <summary>
        /// places: the place along an edge this window shows (<see cref="HoverPlaces"/>), which
        /// slides in when the pointer reaches its strip and holds no room; null for one of the
        /// display's bars shown all the time (<see cref="Standing"/>) or, on a display with none, its
        /// anchor.
        /// </summary>
        public EdgePlace? Place { get; }

        /// <summary>
        /// The edge this window is the bar of, shown all the time (<see cref="EdgeBar.Standing"/>):
        /// holding its strip as Windows' taskbar does, or over the windows. Null for a place, and for
        /// the anchor of a display whose every edge comes in on hover.
        /// </summary>
        public Edge? Standing { get; }

        /// <summary>Whether it holds an appbar: a bar shown all the time, or a display's anchor — not a place.</summary>
        public bool HoldsAppbar => Place is null;

        /// <summary>How this window's edge shows, as the config says now; a place's edge comes in on hover.</summary>
        private EdgeMode Mode => Place is null && Standing is { } edge ? dock.config.EdgesOf(Id)[edge].Mode : EdgeMode.Hover;

        // places: where the place lies — its third of the edge and its strip —, as the last arrange
        // worked it out; and whether the window has been laid out there since, which a place out of
        // sight needs before it can be built later (Build).
        private HoverPlace hover;
        private bool laidOut;

        /// <summary>places: the rectangle the window was last laid out in (<see cref="Area"/>).</summary>
        private RECT laidArea;

        /// <summary>
        /// places: how long this place's bar wants to be along its edge (<see cref="Layout.Natural"/>,
        /// the longer of its app buttons a window each and an app each), the room it keeps from the next
        /// place (<see cref="Layout.PlaceGap"/>) and the DPI it was laid out at; null until it is.
        /// What its edge is shared by (<see cref="Dock.WantsOf"/>).
        /// </summary>
        public (int Length, int Gap, uint Dpi)? Wants { get; private set; }

        /// <summary>
        /// places: the window of a display with no bar shown all the time: it shows nothing and takes
        /// no clicks, and is there to hold the display's appbar, a strip of nothing, so the shell
        /// tells it of full screen apps (BarKeeper). In edit mode it holds the keys and the rail, and still
        /// shows nothing: every edge is a place.
        /// </summary>
        public bool IsAnchor => Place is null && Standing is null;

        /// <summary>
        /// places: a place with nothing in it out of edit mode, there to carry its edge's band over its
        /// third (<see cref="HoverPlaces.Banded"/>): it comes in only with the rest of its edge.
        /// </summary>
        private bool BandOnly => Place is not null && !Editing && Items().Count == 0;

        /// <summary>
        /// Whether the window hides when the pointer leaves it: every place along an edge that comes
        /// in on hover, and the anchor, which never shows. A bar shown all the time never hides.
        /// </summary>
        public bool AutoHides => Place is not null || IsAnchor;

        /// <summary>The edge this window stands on: its place's, else its bar's; the anchor's is its display's own (<see cref="Config.EdgeOfScreen"/>), where it holds nothing.</summary>
        internal Edge BarEdge => Place?.Edge ?? Standing ?? dock.config.EdgeOfScreen(screen.Id);

        /// <summary>The section where a widget goes: <paramref name="section"/> on a bar, a place's own section on a place.</summary>
        internal BarSection SectionIn(BarSection section) => Place?.Section ?? section;

        /// <summary>The key of the block with the keyboard focus in edit mode, if it is still on the bar.</summary>
        public string? FocusedKey => focusKey is { } key && layout.PartIndex(key) >= 0 ? key : null;

        /// <summary>The block the keys walked the focus to (Tab, the arrows, Home, End), shown ringed; null where a press put it there, or none has it.</summary>
        public string? WalkedKey => focusShown ? FocusedKey : null;

        /// <summary>The part with the keyboard focus in edit mode, or -1.</summary>
        public int FocusedPart => FocusedKey is { } key ? layout.PartIndex(key) : -1;


        private float Scale => monitor.Dpi / 96f;

        public DockWindow(Dock dock, MonitorInfo monitor, Screen screen, HoverPlace? place = null, Edge? standing = null)
        {
            this.dock = dock;
            this.monitor = monitor;
            this.screen = screen;
            if (place is { } at)
            {
                Place = new EdgePlace(at.Edge, at.Section);
                hover = at;
            }
            else
            {
                Standing = standing;
            }
            var title = monitor.Primary ? "Karpo" : $"Karpo ({monitor.Name})"; // not-ui: the window's title, a name
            if (place is { } shows)
            {
                title = $"{title} {shows.Edge} {shows.Section}"; // not-ui: the window's title, a name
            }
            else if (standing is { } edge)
            {
                title = $"{title} {edge}"; // not-ui: the window's title, a name
            }
            fixed (char* className = ClassName)
            fixed (char* text = title)
            {
                Hwnd = CreateWindowExW(
                    // No taskbar button, never takes focus from the app in use,
                    // and no GDI surface, since composition draws everything.
                    WS.WS_EX_TOOLWINDOW | WS.WS_EX_TOPMOST | WS.WS_EX_NOACTIVATE | WS.WS_EX_NOREDIRECTIONBITMAP,
                    className,
                    text,
                    WS.WS_POPUP,
                    // On its own display from the start, so it is made at that
                    // display's DPI.
                    monitor.Bounds.Left,
                    monitor.Bounds.Top,
                    1,
                    1,
                    HWND.NULL,
                    HMENU.NULL,
                    dock.instance,
                    null);
            }
            if (Hwnd == HWND.NULL)
            {
                throw LastError("CreateWindowExW");
            }
            // appbar: made before anything asks it what the bar holds (RegisterAppbar, Area).
            holder = new AppbarHolder(dock.appbarShell, Hwnd, WM_KARPO_APPBAR, dock.appbarTrace, Label(monitor));
            try
            {
                SetWindowLongPtrW(Hwnd, GWLP.GWLP_USERDATA, GCHandle.ToIntPtr(dock.cell));
                // peek: every bar, each place and row on every display, stays in sight while a
                // card's window or the desktop is peeked, as Windows' own taskbars do (LivePeek).
                _ = LivePeek.StaysThrough(Hwnd);
                // §547: no host backdrop: a bar's see-through colour is over the wallpaper Karpo blurs (Scene.Wallpaper).
                var desktop = ComUtil.Query<ICompositorDesktopInterop>(dock.gfx.Compositor);
                IDesktopWindowTarget* raw = null;
                try
                {
                    ComUtil.Check(desktop->CreateDesktopWindowTarget(Hwnd, BOOL.FALSE, &raw));
                }
                finally
                {
                    desktop->Release();
                }
                var made = ComUtil.Adopt<DesktopWindowTarget>(raw);
                target = made;
                host = dock.gfx.Compositor.CreateContainerVisual();
                Shown = !AutoHides;
                // places: a place along an edge is no appbar: it holds no room, and the display's bar
                // or anchor hears the shell for it.
                RegisterAppbar();
                var drop = DropTarget.Create(Hwnd);
                if (RegisterDragDrop(Hwnd, drop).SUCCEEDED)
                {
                    dropTarget = true;
                }
                drop->Release();
                // An empty scene until the dock builds the real one, as the
                // Rust dock starts.
                var empty = new Content();
                layout = new Layout(dock.gfx, monitor.Dpi, Rect(monitor.Bounds), Area(), BarEdge, dock.config, Look, [], empty);
                tipWindow = new BarTip(dock.gfx, dock.instance);
                scene = NewScene();
                scene.Update(layout, empty, Look, dock.icons, shown: false);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        /// <summary>The bar's scene in its window, over any it shows already (§637: the one a morph leaves), its tooltip in the tooltip's window.</summary>
        private Scene NewScene() => new(dock.gfx, target!, host, tipWindow?.Root) { TipPlaced = tipWindow is { } tips ? tips.At : null };

        // §637: the morph running on this bar's window, where one is (BarMorph.cs); ended at its batch's end, by the next lay out, or by the window going.
        private Morphing? morph;

        // §637: a morph runs, or is about to in this lay out: the window's region its whole frame, a strip growing held back till it is over.
        private bool morphing;

        // §637: a lay out ends the morph running (Lay).
        private bool relaying;

        /// <summary>tests (§637): whether a morph runs on this bar now.</summary>
        internal bool IsMorphing => morph?.Running == true;

        /// <summary>
        /// §637: the morph over: the window's region the new bar's alone again, and the strip it holds its own where it waited for
        /// the bar to grow into it (<see cref="BarMorph.HoldsNow"/>); the dock lets go of what waited for every morph.
        /// </summary>
        private void Morphed()
        {
            (morph, morphing) = (null, false);
            // Ended by a lay out, which places it and holds its strip itself; the dock's arrange lets go of what waited.
            if (disposed || relaying)
            {
                return;
            }
            Region();
            Reserve();
            dock.MorphsOver();
        }

        /// <summary>
        /// appbar: the display's bounds, work area and DPI read again as they are now (GetMonitorInfo),
        /// where its handle still names it; laid out again at the next build where they changed.
        /// </summary>
        private void Remeasure()
        {
            var info = new MONITORINFO { cbSize = (uint)sizeof(MONITORINFO) };
            if (monitor.Handle == 0 || !GetMonitorInfoW((HMONITOR)(void*)monitor.Handle, &info))
            {
                return;
            }
            uint dpiX = monitor.Dpi, dpiY = monitor.Dpi;
            _ = GetDpiForMonitor((HMONITOR)(void*)monitor.Handle, MONITOR_DPI_TYPE.MDT_EFFECTIVE_DPI, &dpiX, &dpiY);
            var now = monitor with
            {
                Bounds = new RectI(info.rcMonitor.left, info.rcMonitor.top, info.rcMonitor.right, info.rcMonitor.bottom),
                Work = new RectI(info.rcWork.left, info.rcWork.top, info.rcWork.right, info.rcWork.bottom),
                Dpi = dpiX,
            };
            Relist(now, screen);
        }

        /// <summary>The display was listed again; its size, DPI or settings may be new.</summary>
        public void Relist(MonitorInfo monitor, Screen screen)
        {
            laidOut &= monitor.Bounds == this.monitor.Bounds && monitor.Dpi == this.monitor.Dpi;
            this.monitor = monitor;
            this.screen = screen;
            holder.Label = Label(monitor);
        }

        /// <summary>
        /// places: where this window's place lies now (<see cref="HoverPlaces.For"/>); one that
        /// moved is laid out there at the next build, in sight or not.
        /// </summary>
        public void Hover(HoverPlace place)
        {
            if (place != hover)
            {
                hover = place;
                laidOut = false;
            }
        }

        /// <summary>
        /// places: its edge was shared again (<see cref="Dock.Reshare(string, Edge)"/>): laid out again
        /// at once where its stretch or its strip moved.
        /// </summary>
        public void Reshared(HoverPlace place)
        {
            Hover(place);
            if (laidOut && Area().Equals(laidArea))
            {
                return;
            }
            laidOut = false;
            Build();
        }

        /// <summary>
        /// places: the entries this window shows, in its sections' order: a place's own section of
        /// its edge; a bar's, everything along the edge it stands on (<see cref="Sections.Along"/>);
        /// none for an anchor, whose display's edges are all places. §593 the add page's ghost in them where one stands
        /// (<see cref="Seen"/>).
        /// </summary>
        public List<BarItem> Items() => Shows(dock.Seen.ActiveLayout(screen.Id), dock.config.EdgeOfScreen(screen.Id), Place, Standing);

        /// <summary>What the appbar trace calls the bar on <paramref name="monitor"/>: its display.</summary>
        private static string Label(MonitorInfo monitor) =>
            monitor.DeviceName.Length > 0 ? monitor.DeviceName.TrimStart('\\', '.') : monitor.Name;

        /// <summary>Shown once the frame's content is on the screen (Surfaces): a new bar comes up drawn, the one it replaces going in the same plan.</summary>
        public void Show()
        {
            if (!Fullscreen)
            {
                Surfaces.Show(Hwnd);
            }
        }

        /// <summary>DIPs to whole pixels on this display, rounded as the Rust rounds.</summary>
        public int Px(float dips) => (int)MathF.Round(dips * Scale, MidpointRounding.AwayFromZero);

        /// <summary>
        /// The rectangle the dock stands in: the whole display while the
        /// taskbar is hidden or about to be, else what the taskbar leaves free.
        /// </summary>
        private RECT Area()
        {
            // places: a place stands in its stretch of the edge (HoverPlaces.Share), the whole display across.
            if (Place is not null)
            {
                return Rect(HoverPlaces.Area(monitor.Bounds, hover));
            }
            // appbar: with Explorer's taskbar hidden, or about to be by the takeover straight after
            // the first build, the bar stands on the display's edge. Laid out in a work area that
            // still kept the taskbar's strip, it was built 72 px up and showed there until the next
            // arrange moved it down.
            // A bar stands along the whole edge, as Windows' taskbar does.
            if (dock.HidesTaskbar)
            {
                return Rect(monitor.Bounds);
            }
            var area = Rect(monitor.Work);
            // The space this dock holds is itself left out of the work area.
            // Standing inside what is left would move the dock off its own
            // space, and the next reservation further in, without end.
            if (HoldsSpace && holder.Reserved is RECT own)
            {
                switch (holder.ReservedEdge)
                {
                    case Edge.Top:
                        area.top = Math.Min(area.top, own.top);
                        break;
                    case Edge.Left:
                        area.left = Math.Min(area.left, own.left);
                        break;
                    case Edge.Right:
                        area.right = Math.Max(area.right, own.right);
                        break;
                    default:
                        area.bottom = Math.Max(area.bottom, own.bottom);
                        break;
                }
            }
            return area;
        }

        /// <summary>
        /// §514: what a flyout opened from this bar is drawn in — a folder, a widget's flyout alike —: this bar's look as an open
        /// flyout (<see cref="Look.Flyout"/>), outlined by the Appearance's Flyouts'.
        /// </summary>
        internal Look FlyoutLook => Look.Flyout();

        public IIsland? IslandAt(Hit hit)
        {
            var index = layout.PartOf(hit);
            return index >= 0 && index < layout.Specs.Count ? dock.islands.GetValueOrDefault(layout.Specs[index].Key) : null;
        }

        /// <summary>
        /// The taskbar entry the part at <paramref name="hit"/> is, which is what the click table
        /// and the tooltips ask; a value outside the enum where the hit is on no part.
        /// </summary>
        public BarKind KindAt(Hit hit)
        {
            var index = layout.PartOf(hit);
            return index >= 0 && index < layout.Specs.Count ? layout.Specs[index].Kind : TolerantEnumConverter<BarKind>.Unknown;
        }

        // What is open on each desktop of this display. Read when it may have changed (window
        // events, see Dock.DesktopAppsDue; a switch; a new list of desktops), never on a timer.
        private List<DesktopPreview> desktopSnapshot = [];
        private string desktopSnapshotKey = "";
        private bool desktopSnapshotStale = true;

        private List<DesktopPreview> DesktopSnapshot()
        {
            if (dock.desktops is not { } desktops)
            {
                return [];
            }
            // A switch changes which window is on top, and so the icons' order.
            var key = string.Join(",", desktops.Ids) + "|" + desktops.Current;
            if (desktopSnapshotStale || key != desktopSnapshotKey)
            {
                desktopSnapshot = DesktopPreviews.Read(Rect(monitor.Bounds), desktops.Ids);
                desktopSnapshotKey = key;
                desktopSnapshotStale = false;
            }
            return desktopSnapshot;
        }

        /// <summary>Desktop <paramref name="index"/>'s windows on this display, as last read; null for no such desktop.</summary>
        public DesktopPreview? DesktopPreviewAt(int index)
        {
            var snapshot = DesktopSnapshot();
            return index >= 0 && index < snapshot.Count ? snapshot[index] : null;
        }

        /// <summary>
        /// The desktops as the switcher's list shows them on this display: each one's name, the
        /// apps open on it — one per app, the top of the stack first, up to
        /// <see cref="Switchers.RowIcons"/> — and how many more there are.
        /// <para>
        /// Read when the list is built rather than when the bar is: what is open on a desktop is
        /// not on the bar any more, so a window opening or closing no longer measures, lays out or
        /// rebuilds anything. That it once did is why this used to have a "would the buttons lay
        /// out differently?" answer beside it.
        /// </para>
        /// </summary>
        public DesktopStrip DesktopsNow()
        {
            if (dock.desktops is not { } desktops)
            {
                return new DesktopStrip([], 0);
            }
            var snapshot = DesktopSnapshot();
            var buttons = new DesktopButton[desktops.Names.Count];
            for (var index = 0; index < buttons.Length; index++)
            {
                var apps = new List<Item>(Switchers.RowIcons);
                var more = 0;
                if (index < snapshot.Count)
                {
                    var (icons, beyond) = DesktopApps.Split(DesktopApps.Distinct(snapshot[index].Windows), Switchers.RowIcons);
                    foreach (var window in icons)
                    {
                        if (dock.DesktopAppItem(window) is { } item)
                        {
                            apps.Add(item);
                        }
                    }
                    more = beyond;
                }
                // With Windows' own id: a desktop moved in the list keeps its row (SwitchRow.Key).
                var id = index < desktops.Ids.Count ? desktops.Ids[index].ToString("N") : "";
                buttons[index] = new DesktopButton(desktops.Names[index], apps, more, id);
            }
            return new DesktopStrip(buttons, desktops.Current);
        }

        /// <summary>The apps on this display's desktops are read again the next time they are asked for.</summary>
        public void DesktopAppsChanged() => desktopSnapshotStale = true;

        public void Build()
        {
            // appbar: out of a full screen app's way, nothing is laid out — not on a display mode that
            // is the game's, nor lifted topmost in front of it — until the app is gone (MakeWay).
            if (Fullscreen)
            {
                Stale = true;
                return;
            }
            // places: a place out of sight is built when it next comes in (Reveal), not with every
            // change to any bar; until then it needs only its strip, which its place gives. One that
            // moved is laid out at once, so its window stands where its strip is.
            if (Place is not null && laidOut && !Shown && !hovering && !dock.options.Demo.HasValue)
            {
                Stale = true;
                return;
            }
            Lay();
        }

        /// <summary>The scene built again from the config, the content and the look as they are now.</summary>
        private void Lay()
        {
            Stale = false;
            laidOut = true;
            // §637: a morph still running ends where it ends, before anything is laid out: the bar is laid out again, all of it below.
            relaying = true;
            try
            {
                morph?.End();
            }
            finally
            {
                relaying = false;
            }
            var d = dock;
            // places: what this window shows (Items): its edge's entries, or its place's. A part's index
            // is not the entry's in the list, which holds every edge: edit mode maps one to the other
            // (ListSlot).
            var placed = Items();
            var owner = Config.DisplayOwner(screen.Id);
            // The edge's own thickness and look, whatever its mode (Config.WithEdge); a place, and a
            // bar over the windows, is never clear (HoverPlaces.Dressed).
            var config = d.SizedFor(d.config.EdgesOf(Id), BarEdge);
            var look = laidLook = Dressed();
            // sizing: WidgetBounds.For is how many grid cells the widget may be given — its kind's
            // fewest and most; a taskbar entry has no width of its own to set.
            var specs = placed
                .Select(island => PartSpec.For(Config.IslandKey(owner, island.Id), island))
                .ToList();
            var trayOn = d.tray is not null;
            // tray: each tray its own icons, as its widget splits them (§347, TraySplit); the chevron
            // also while one of its icons is dragged, to be dropped onto (Dock.Tray.cs).
            var trays = specs.Where(spec => spec.Kind == BarKind.Tray).DistinctBy(spec => spec.Key).ToDictionary(
                spec => spec.Key,
                spec => d.TrayOf(spec.Key) is var split && trayOn
                    ? new BarTray(split.Shown, split.Hidden.Count > 0 || d.trayDrag?.Tray == spec.Key)
                    : new BarTray([], false));
            Content ContentFor(List<Tile> tiles) => new()
            {
                // pins: this bar's own app buttons — its pins, and the open apps it shows —, which
                // part each stands in, and what a window's own button says (Layout.TileLabel).
                Tiles = tiles.Select(tile => (tile.Item, tile.Mark)).ToList(),
                TileParts = tiles.Select(tile => tile.Part).ToList(),
                Labels = tiles.Select(tile => tile.Label).ToList(),
                Islands = d.islands.Count > 0 ? d.islands : null,
                Trays = trays,
                // tray: the cell an icon dragged onto a tray brings, or one dragged off it takes away.
                TrayMore = trayOn ? d.TrayRooms(this) : null,
            };
            // places: an anchor is never dressed for edit mode; it only holds the keys for its display. §612: nor is any bar in
            // the Appearance's preview — laid out as out of edit mode, edit mode on all the same.
            var editing = d.laidDress.Worn() && !IsAnchor;
            // app buttons: a window each while they fit, and an app each once they don't, where the
            // buttons combine "when taskbar is full" — laid out both ways only then (AppButtons.Lay).
            // One setting for every bar, the main display's and the others (TaskbarApps.cs).
            var area = Area();
            // places: what the place wants of its edge is the longer of the two (Layout.Natural), so a
            // place whose buttons combined in a short stretch still asks for the room to show them a
            // window each.
            var natural = 0;
            // places: a place has its one section: no empty room for the others in edit mode. A dock's
            // bar has all three, each a dock of its own (Layout.SectionPanels).
            var only = Place?.Section;
            var timing = System.Diagnostics.Stopwatch.StartNew();
            (var shownTiles, layout, full) = AppButtons.Lay(
                config.BarLayout.Combine,
                expanded,
                combined,
                tiles =>
                {
                    var made = new Layout(d.gfx, monitor.Dpi, Rect(monitor.Bounds), area, BarEdge, config, look, specs, ContentFor(tiles), editing, only);
                    natural = Math.Max(natural, made.Natural);
                    return made;
                });
            laidArea = area;
            var content = ContentFor(shownTiles);
            Tiles = shownTiles;
            if (dragKey is { } held && (!editing || layout.PartIndex(held) < 0))
            {
                // The block being dragged is gone (the settings app removed it).
                EndDrag();
            }
            var visible = Shown || hovering || d.options.Demo.HasValue;
            // §637: another bar in this window — another layout shown, the layout another type — morphs into this one, by the one
            // motion: the scene as it stood is the morph's leaving picture, a new one laid out over it (BarMorph.cs).
            var morphs = visible && scene.Presented && !IsAnchor && !Fullscreen && look.Motion.On && BarMorph.Between(scene.Laid, layout);
            var (leaving, picture, stood) = morphs ? (scene, scene.Picture(), frame) : (null, null, null);
            morphing = morphs;
            Reserve();
            // §358: the window stands still at its edge's most thickness (rule R2): another height, a
            // height dragged, a widget come or gone, a place's stretch shared out again is the scene laid
            // out in it again, whole in one commit. It moves only when its display or its edge's room does.
            var place = Frame();
            if (frame is not { } standing || !Same(standing, place))
            {
                // §359: its tooltip's window with it, standing where it stands.
                foreach (var each in (ReadOnlySpan<HWND>)[Hwnd, tipWindow?.Hwnd ?? HWND.NULL])
                {
                    if (each != HWND.NULL)
                    {
                        Check(Surfaces.Place(each, place.left, place.top, place.right - place.left, place.bottom - place.top));
                    }
                }
                frame = place;
            }
            // §359: where it stands in the z-order is its stratum's: a bar, a place with only its edge's band
            // in it under its display's places that hold something, so where two edges meet in a corner the
            // band never stands over a widget of the other edge.
            Surfaces.Enlist(Hwnd, Stratum.Bar, BandOnly ? 0 : 1);
            Moved(place);
            if (leaving is not null)
            {
                scene = NewScene();
                // §547: the wallpaper it showed until the new strip comes.
                scene.Wallpaper(wallpaperSurface);
                scene.Backdrop(wallpaperLight);
            }
            // §358: the scene kept, laid out again: what stays is kept, what moved glides, what changed is
            // drawn again; the highlight, the tooltip and a press stay where they are.
            var laidIn = timing.Elapsed.TotalMilliseconds;
            scene.Update(layout, content, look, d.icons, visible, editing ? EditView() : null, Shift);
            // §696: for the slow change's line in karpo.log.
            d.laying = (d.laying.Bars + 1, d.laying.Layouts + laidIn, d.laying.Scenes + timing.Elapsed.TotalMilliseconds - laidIn);
            if (leaving is not null && picture is not null)
            {
                // The old picture in this window's px as it stands now.
                var by = stood is RECT was ? new Vector2(was.left - place.left, was.top - place.top) : Vector2.Zero;
                morph = Karpo.Dock.Morphing.Start(d.gfx.Compositor, leaving, picture.Moved(by), scene, look.Motion, Morphed);
                morphing = morph.Running;
                // §701: ended however its batch fares.
                if (morphing)
                {
                    Arm(MORPH_WATCH, MORPH_WATCH_MS);
                }
            }
            // §547: the wallpaper blurred under it, where it is see-through.
            Wallpaper();
            // keys: the keyboard stays where it was on the bar (Dock.Keys.cs).
            KeepKeyFocus();
            // narrator: a screen reader finds the bar's entries (Dock.Uia.cs).
            RegisterUia();
            if (hovering)
            {
                // The pointer where it is, in the layout as it is now: another height moves the layout's corner.
                if (dragKey is null && sizing is null && !dragging && !dock.options.Demo.HasValue)
                {
                    pointer = Local(Cursor());
                }
                hovered = layout.HitTest(pointer.X, pointer.Y, editing);
                HoverChanged();
            }
            Region();
            // edit: the sections' edit bars stand beside them, so they are placed again wherever the
            // sections are now — a height drag, a widget added, a bar coming out of hiding (Dock.EditRail.cs).
            if (Editing)
            {
                d.StirChrome();
            }
            // stage: where the entries are now, for the recording's script (Stage.cs).
            MapForStage();
            // §667: and where its buttons are, for Windows' minimize and restore animations.
            PublishButtons();
            // "…" or the tray's overflow standing open from this bar shows what it has now.
            d.FollowOverflow(this);
            // tray: an icon dragged as the bar was laid out again is shown where the drag has it.
            d.TrayDragRebuilt(this);
            // places: a place that wants another length of its edge has the edge shared again, which
            // lays out each place whose stretch moved, this one too (Dock.Reshare).
            if (Place is not null && Wants != (natural, layout.PlaceGap, monitor.Dpi))
            {
                Wants = (natural, layout.PlaceGap, monitor.Dpi);
                d.Reshare(this);
            }
        }

        /// <summary>
        /// Where the layout's window lies in the real one (<see cref="Frame"/>), which stands still at its
        /// edge's most thickness: what the scene is moved by in it, and the region with it. Everything
        /// else here is in the layout's px — the pointer too, taken from the window's as it comes
        /// (<see cref="Dock.WindowMessage"/>) — and on screen at the layout's corner (<see cref="Layout.X"/>).
        /// </summary>
        private Vector2 Shift => frame is RECT kept ? new Vector2(layout.X - kept.left, layout.Y - kept.top) : Vector2.Zero;

        /// <summary>A point in the window's own px as the layout's, which <see cref="Shift"/> sets apart.</summary>
        public Vector2 FromClient(float x, float y) => new Vector2(x, y) - Shift;

        /// <summary>A point on the screen as the layout's px.</summary>
        private Vector2 Local(POINT screen) => new(screen.x - layout.X, screen.y - layout.Y);

        /// <summary>
        /// §358: the window a bar stands in for as long as its display and its edge's room stay as they
        /// are (rule R2): its edge's strip at the most thickness the edge can take (<see cref="Layout.FrameFor"/>),
        /// as the height drag's window was, so another height or another type of bar moves nothing; a
        /// place's along the whole edge, so sharing the edge out again moves nothing either. Never short of
        /// the layout's own window.
        /// </summary>
        private RECT Frame()
        {
            var area = Place is not null ? Rect(monitor.Bounds) : Area();
            var room = Layout.FrameFor(monitor.Dpi, area, BarEdge, dock.look.TextScale);
            return new RECT
            {
                left = Math.Min(room.left, layout.X),
                top = Math.Min(room.top, layout.Y),
                right = Math.Max(room.right, layout.X + layout.Width),
                bottom = Math.Max(room.bottom, layout.Y + layout.Height),
            };
        }

        private static bool Same(RECT a, RECT b) => a.left == b.left && a.top == b.top && a.right == b.right && a.bottom == b.bottom;

        // --- The space it holds --------------------------------------------

        /// <summary>
        /// Registers the bar with the shell afresh (ABM_REMOVE, ABM_NEW): the window is new, or
        /// Explorer restarted. Registered to hold space while the bar is always there; whether the
        /// shell took it, and whether the space is really held, is what the reconcile finds out
        /// (BarKeeper, AppbarHolder.Verify).
        /// </summary>
        public void RegisterAppbar()
        {
            if (!HoldsAppbar || Stage.On)
            {
                return;
            }
            holder.Register();
        }

        /// <summary>
        /// The space this bar holds along its edge, which is where maximized
        /// windows stop: its whole physical thickness while its edge holds its strip
        /// (<see cref="EdgeMode.Reserve"/>), and none of it for a bar over the windows
        /// or an anchor, so windows have the display to themselves there. Each bar
        /// holds its own edge's space, several on one display where several edges
        /// hold theirs, at every build and every reconcile (BarKeeper). What that takes is
        /// <see cref="AppbarSpace.Plan"/>'s, and only a change is sent; that it is held is the
        /// reconcile's to find out. Never for a display a full screen app holds, nor while the first
        /// takeover waits for the build (<see cref="Dock.holdBack"/>). True when something was sent.
        /// </summary>
        public bool Reserve()
        {
            // Every change of the space held makes windows move; while the
            // bar is dragged, the space follows once, at the end.
            if (dock.Resizing || !HoldsAppbar || Stage.On || Fullscreen || dock.holdBack)
            {
                return false;
            }
            // The panel, flush with the edge; the tooltip room over it is not held. A floating bar
            // holds the room it floats over too, so no window goes under its panels. An anchor holds
            // none: its display has no bar.
            var thickness = IsAnchor ? 0 : AppbarSpace.Thickness(Mode, layout.Reserve);
            // §637: a bar growing as it morphs holds its new strip once it has grown into it (Morphed).
            if (!BarMorph.HoldsNow(morphing, Held, thickness))
            {
                return false;
            }
            return holder.Hold(BarEdge, thickness, Rect(monitor.Bounds), monitor.Handle);
        }

        /// <summary>§637: how thick a strip of its display this bar holds now, in px across its edge; 0 for none.</summary>
        private int Held => HoldsSpace && holder.Reserved is RECT strip
            ? holder.ReservedEdge is Edge.Left or Edge.Right ? strip.right - strip.left : strip.bottom - strip.top
            : 0;

        /// <summary>
        /// Tells the shell where the bar's window is now, which an appbar is
        /// meant to do whenever it moves (ABM_WINDOWPOSCHANGED). Without it the
        /// shell goes on from where the window was when it last heard, and what
        /// it works out for the other windows can go stale.
        /// </summary>
        private void Moved(RECT place) => holder.Moved(place);

        /// <summary>
        /// appbar: what the shell says to this bar. None of it is taken as truth; each is a reason to
        /// reconcile (BarKeeper): a full screen app came or went (the full screen look decides, from the
        /// window in front: the shell counts the desktop and Task view too); another appbar or the
        /// taskbar moved, or the taskbar's auto-hide changed, which can change the work area.
        /// </summary>
        public void Appbar(uint code, bool on)
        {
            if (code == ABN_FULLSCREENAPP)
            {
                dock.Reconcile(Stir.Front);
            }
            else if (code == ABN_POSCHANGED)
            {
                // The bars are laid out again where the work area moved (a bar beside Explorer's
                // taskbar while it was shown), and the arrange ends in the reconcile.
                dock.RequestArrange(Stir.Neighbours);
            }
            else if (code == ABN_STATECHANGE)
            {
                dock.Reconcile(Stir.Neighbours);
            }
        }

        /// <summary>What <see cref="Dock.Shutdown"/> puts back for this window.</summary>
        public void Release()
        {
            holder.Release();
            if (dropTarget)
            {
                dropTarget = false;
                RevokeDragDrop(Hwnd);
            }
        }

        /// <summary>
        /// §637: let go of as the display it stood on shows another bar or none — another edge, another way of showing —: where
        /// motion is on and it was in sight it goes as a bar going into hiding goes (Scene.Present, a large surface's Hide), its
        /// window destroyed once it has; else at once (<see cref="Dispose"/>).
        /// </summary>
        public void GoByMotion()
        {
            if (!disposed && Look.Motion.On && Shown && !Fullscreen && !IsAnchor && scene.Presented)
            {
                morph?.End();
                scene.Present(false, animate: true);
                goneAfterMs = (int)Look.Motion.Ms(MotionKind.Hide);
            }
            Dispose();
        }

        /// <summary>
        /// §637: made for a display that showed another bar a moment ago (another edge, another way of showing): where motion is on
        /// it comes in as a bar out of hiding comes (Scene.Present, a large surface's Show), while the one it replaces goes (<see cref="GoByMotion"/>).
        /// </summary>
        public void ComeByMotion()
        {
            if (!disposed && Look.Motion.On && Shown && !Fullscreen && !IsAnchor && scene.Presented)
            {
                scene.Present(false, animate: false);
                scene.Present(true, animate: true);
            }
        }

        // §637: how long its window waits, once let go of, for its scene's going (GoByMotion); 0: at once.
        private int goneAfterMs;

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }
            disposed = true;
            // §637: a morph running ends: the scene it left let go of with it.
            morph?.End();
            Stage.Gone((nint)Hwnd);
            // §667: no window minimizes into a bar that went.
            PublishButtons();
            // §534: a bar carried that goes (its display gone) lands nowhere.
            CancelBarMove();
            if (sizing is not null)
            {
                sizing = null;
                if (GetCapture() == Hwnd)
                {
                    ReleaseCapture();
                }
            }
            Release();
            ForgetWallpaper();
            SetWindowLongPtrW(Hwnd, GWLP.GWLP_USERDATA, 0);
            // The window and its scene go once the frame's content is on the screen (Surfaces): a bar
            // replaced by an arrange stays in sight until the new one shows, in the same plan. Until
            // then it is whole, for whatever the handler that let it go still asks of it.
            // The tooltip's window with it; what it shows closed after the scene, which holds a part of it.
            var tips = tipWindow;
            tipWindow = null;
            tips?.Go();
            Surfaces.Destroy(Hwnd, () =>
            {
                try
                {
                    ((object?)scene as IDisposable)?.Dispose();
                    target?.Dispose();
                    host?.Dispose();
                    tips?.Close();
                }
                catch (Exception error)
                {
                    Report(error);
                }
                target = null;
                host = null;
            }, goneAfterMs);
        }

        /// <summary>Where the bar's own window stands on the screen, in px — its layout's, in the window that stands still round it (§358): what the edit bar keeps clear of (Dock.EditDeck.cs).</summary>
        public RectI WindowRect => new(layout.X, layout.Y, layout.X + layout.Width, layout.Y + layout.Height);

        // --- Showing and hiding --------------------------------------------

        /// <summary>
        /// Over the panel, or over the edge beyond it; the tooltip room on the
        /// panel's inner side is not the dock.
        /// </summary>
        private bool Over(float x, float y)
        {
            var p = layout.PanelRect;
            return layout.Edge switch
            {
                Edge.Top => y < p.Y + p.Height,
                Edge.Left => x < p.X + p.Width,
                Edge.Right => x >= p.X,
                _ => y >= p.Y,
            };
        }

        public Hit? HitAt(float x, float y) => hovering ? layout.HitTest(x, y, Editing) : null;

        /// <summary>
        /// §576: a press at (<paramref name="x"/>, <paramref name="y"/>) is the pointer on the bar, whatever the hover says: a
        /// leave Windows posted between the pointer's last move and the press (the pointer it tracks elsewhere a moment, a
        /// window shown or placed meanwhile — a flyout coming in) had ended the hover, and the press found nothing under it
        /// (<see cref="HitAt"/>): read as one on the bar's room, it cleared the selection instead of opening a folder.
        /// </summary>
        public void Pointed(float x, float y)
        {
            if (!hovering)
            {
                Moved(x, y);
            }
        }

        /// <summary>
        /// places: a place this window's parts name (<paramref name="slot"/>, counted as
        /// <see cref="Layout.DropSlot"/> counts, all but <paramref name="moving"/> — one widget, or several
        /// carried together, counted as this window laid out without them counts, <see cref="Layout.Without"/>),
        /// as the display's list has it: the section a widget put there joins, and its index in the list
        /// without <paramref name="moving"/> (<see cref="IslandEdits.ToList"/>, <see cref="IslandEdits.MoveTo"/>).
        /// A place shows only its own section of its edge, so its parts' indexes are not the list's own.
        /// §519: what is moving is found by its id, whichever display's key names it — an id is unique across
        /// every layout — so one carried from a display showing the same layout is not counted twice.
        /// </summary>
        public BarSlot ListSlot(BarSlot slot, IReadOnlyList<string>? moving = null)
        {
            var list = dock.config.IslandsFor(screen.Id);
            BarItem? Find(string key) => list.Find(island => island is not null
                && string.Equals(island.Id, Islands.IdOf(key), StringComparison.OrdinalIgnoreCase));
            var carried = (moving ?? []).Select(Find).OfType<BarItem>().ToList();
            var rest = list.Where(island => !carried.Contains(island)).ToList();
            var shown = layout.Specs.Select(spec => Find(spec.Key)).OfType<BarItem>().Where(island => !carried.Contains(island)).ToList();
            return IslandEdits.ToList(rest, shown, slot, null, SectionIn(slot.Section));
        }

        /// <summary>
        /// §701 (the person, 2026-11-14: "i also cannot click the topright area of the screen as it is blocked by something,
        /// when i click a pin it also immeadiately goes into edit mode ... it also opens the application"): the pointer moved
        /// over the bar, as Windows says it (WM_MOUSEMOVE) — which it never does while something is dragged over it (the drag's
        /// own loop has the pointer): a drag still held to be over it is one whose leaving never came, let go of now. While held
        /// the bar took its whole window (Region), read the pointer at the drag's last spot, and a press was a drag's.
        /// </summary>
        public void PointerMoved(float x, float y)
        {
            if (dragging)
            {
                Troubles.Write($"Drop: a drag over the bar ({Name}) never said it left; let go of at the pointer's move"); // not-ui: karpo.log
                dragging = false;
                DropLeft();
                Region();
            }
            Moved(x, y);
        }

        public void Moved(float x, float y)
        {
            pointer = new Vector2(x, y);
            if (sizing is not null)
            {
                ResizeMoved();
                return;
            }
            // A dragged block, or desktop button, follows the pointer wherever
            // it goes: the bar has the mouse captured.
            if (dragKey is not null)
            {
                DragMoved();
                return;
            }
            // edit: the selection rectangle from a press on the empty room, the bar holding the mouse.
            if (emptyPressed is not null && Editing)
            {
                dock.BandMoved(OnScreen(pointer));
                return;
            }
            // quick: the volume slider, while it holds the pointer (Dock.Quick.cs).
            if (SliderDragMoved())
            {
                return;
            }
            // pins: a pin or a folder dragged along the bar outside edit mode (Dock.Pins.cs).
            if (PinDragMoved())
            {
                return;
            }
            // tray: a tray icon dragged towards the overflow or along the tray (Dock.Tray.cs).
            if (TrayDragMoved())
            {
                return;
            }
            // §534: the bar carried to another edge (Dock.BarMove.cs).
            if (BarMoveMoved())
            {
                return;
            }
            if (dock.options.Demo.HasValue || Fullscreen)
            {
                return;
            }
            // places: an anchor never comes in, and a place does not while another display is edited,
            // nor while the left button is held: a window or a selection dragged to the edge.
            if (!Shown && (IsAnchor || (Place is not null && ((dock.editor is not null && !Editing) || ButtonHeld()))))
            {
                return;
            }
            // places: the whole edge comes in, whichever third's strip the pointer reached (EdgeReveal).
            if (!Shown)
            {
                EdgeReveal.Reveal(this, dock.windows);
            }
            // Coming in takes the panel or the edge beyond it, and with
            // nothing magnified, so does staying.
            if (!Over(x, y))
            {
                Leave();
                return;
            }
            if (!hovering)
            {
                Enter();
            }
            var hit = layout.HitTest(x, y, Editing);
            if (hit != hovered)
            {
                hovered = hit;
                HoverChanged();
            }
            // §382: from one status icon of a folder's button to the next, or off them, is the same hit
            // and another tooltip.
            else if (hit is { Kind: HitKind.Island } && !Editing && !OwnsOpenPreview && !dock.FlyoutOpen && TooltipFor(hit).Part != tipPart)
            {
                ShowTooltip(hit);
            }
            // Every move over a tray icon is its app's to hear.
            TrayPointer(hit);
        }

        private void Enter()
        {
            hovering = true;
            leaving = false;
            peeking = false;
            Kill(SETTLE);
            Region();
            var track = new TRACKMOUSEEVENT
            {
                cbSize = (uint)sizeof(TRACKMOUSEEVENT),
                dwFlags = TME.TME_LEAVE,
                hwndTrack = Hwnd,
                dwHoverTime = 0,
            };
            Check(TrackMouseEvent(&track));
            // And watched, whatever messages come or don't (HoverWatched).
            Arm(HOVER_WATCH, HoverWatchMs);
            Efficiency(false);
            // The first hover after a while: the desktop buttons should be right.
            dock.CheckDesktops();
        }

        /// <summary>
        /// The hover's watch (HOVER_WATCH): the pointer is asked of Windows, not of the messages. Still on
        /// the bar, its leave is asked for again — the one Windows owed may be the one that was lost; gone,
        /// the hover ends as a leave would have ended it. A resize holds the pointer and ends the hover itself.
        /// </summary>
        private void HoverWatched()
        {
            if (!hovering)
            {
                Kill(HOVER_WATCH);
                return;
            }
            if (sizing is not null || BarMoving || dock.options.Demo.HasValue)
            {
                return;
            }
            if (PointerOn())
            {
                var track = new TRACKMOUSEEVENT
                {
                    cbSize = (uint)sizeof(TRACKMOUSEEVENT),
                    dwFlags = TME.TME_LEAVE,
                    hwndTrack = Hwnd,
                    dwHoverTime = 0,
                };
                _ = TrackMouseEvent(&track);
                return;
            }
            Leave();
        }

        /// <summary>
        /// The pointer is on this bar, as Windows says now: the window under it is this bar's and the
        /// pointer on its panel (not the tooltip room), or it is this bar's tooltip, or the window list it
        /// opened. Asking which window is there, not only rectangles, is what tells a pointer on another
        /// display beyond the bar's outer edge from one on the bar. A pointer Windows will not say (the
        /// secure desktop) is taken as on: the messages decide then.
        /// </summary>
        private bool PointerOn()
        {
            POINT at;
            // Another desktop has the input (the secure desktop; a test's hidden one): its pointer is not this one's.
            if (!InputDesktop.Here() || !GetCursorPos(&at))
            {
                return true;
            }
            var root = GetAncestor(WindowFromPoint(at), GA.GA_ROOT);
            if (root == Hwnd)
            {
                var local = Local(at);
                return Over(local.X, local.Y);
            }
            return (tipWindow is { } tip && root == tip.Hwnd)
                || (OwnsOpenPreview && dock.preview is { IsOpen: true } list && root == list.Hwnd);
        }

        public void Leave()
        {
            // The pointer runs off the bar while dragging its edge, or the bar itself (§534); the drag
            // holds the mouse and decides when the hover ends.
            if (!hovering || dock.options.Demo.HasValue || sizing is not null || BarMoving)
            {
                return;
            }
            hovering = false;
            Kill(HOVER_WATCH);
            hovered = null;
            if (pressed is not null)
            {
                scene.Press(Lit(pressed), false);
            }
            pressed = null;
            scene.Highlight(null, false);
            scene.Tooltip(dock.gfx, null, null);
            // And a tray icon's popup with it.
            dock.SendTrayHover(trayHover.Left());
            // preview: nothing waits to open, and a list up goes after the grace (PreviewHover).
            PreviewRules.Off(OwnsOpenPreview);
            // peek: and the desktop's, off the corner.
            DesktopPeekHover(null);
            // quick: the pointer left the bar altogether — on its way to the panel, or away. The
            // grace decides which (WidgetHover.Stays).
            HoverFlyout(null, Editing);
            leaving = true;
            var delay = !AutoHides ? (uint)Scene.HoverOutMs + 60 : HIDE_DELAY_MS;
            Arm(SETTLE, delay);
        }

        /// <summary>The pointer is gone for good, or the peek is over: back to rest.</summary>
        private void Settle()
        {
            Kill(SETTLE);
            // places: the edge goes together, once nothing on it holds any of it up (EdgeReveal).
            EdgeReveal.Settle(this, dock.windows);
        }

        /// <summary>Whether something keeps this window up when its time to go comes (<see cref="Settle"/>).</summary>
        private bool KeptUp
        {
            get
            {
                // A bar being edited stays up however it hides otherwise, and so does one the keyboard is on.
                if (dock.options.Demo.HasValue || hovering || dragging || sizing is not null || Editing || keyHit is not null)
                {
                    return true;
                }
                // The pointer on the desktop flyout keeps the bar up too, and so does a list a click held.
                if (dock.previewOwner == this && (dock.PreviewInside || (dock.previewHeld && dock.PreviewShowing)))
                {
                    return true;
                }
                // A taskbar that hides by itself stays up while its jump list is
                // open; closing the flyout settles it again.
                return dock.FlyoutOpenFor(this);
            }
        }

        string IEdgeWindow.Display => Id;

        bool IEdgeWindow.Holds => KeptUp;

        /// <summary>
        /// places: brought in with the rest of its edge, it takes its panels at once. Revealed alone,
        /// the strip it took while out of sight stayed its region, and a region clips what a window
        /// shows too: only the third the pointer reached could be seen, the others came in hidden
        /// (the person, 2026-10-07: "they appear independently when hovering").
        /// </summary>
        void IEdgeWindow.Show()
        {
            Reveal();
            Region();
        }

        void IEdgeWindow.Hide() => ToRest();

        /// <summary>Back to rest now, nothing holding it: out of sight if it hides, its list closed.</summary>
        private void ToRest()
        {
            Kill(SETTLE);
            var owns = dock.previewOwner == this;
            leaving = false;
            peeking = false;
            // Another display's window list is not this window's to close.
            if (owns)
            {
                dock.ClosePreview(this);
            }
            else
            {
                KillPreviewTimers();
            }
            if (!AutoHides)
            {
                Region();
                dock.Rest();
            }
            else
            {
                Conceal();
            }
        }

        private void Reveal()
        {
            // places: a place built last before it went out of sight is built now, out of sight still,
            // and slides in from there.
            if (Place is not null && (Stale || !laidOut))
            {
                Lay();
            }
            Shown = true;
            Kill(CONCEAL);
            // Over whatever came up in front since — another app's always-on-top window — as the plan has it
            // (§359): a place with only its edge's band in it under its display's places that hold something.
            Surfaces.Enlist(Hwnd, Stratum.Bar, BandOnly ? 0 : 1);
            Surfaces.Restack();
            // Checked when the dock shows rather than on a timer, so an idle
            // dock stays asleep: apps must find the tray's window first.
            dock.tray?.EnsureFirst();
            // Coming into view is when the desktops are read again, not a timer.
            dock.CheckDesktops();
            // And their apps, which may have moved while it was away.
            dock.DesktopAppsSoon();
            dock.TickClock();
            scene.Present(true, true);
            dock.UpdateIslands();
        }

        private void Conceal()
        {
            Shown = false;
            scene.Present(false, true);
            Region();
            Arm(CONCEAL, 260);
            dock.ClockHidden();
            dock.UpdateIslands();
            dock.Rest();
        }

        /// <summary>
        /// Shows a hidden dock for a moment, the way an auto-hidden taskbar
        /// shows itself for an app that needs attention.
        /// </summary>
        public void Peek()
        {
            // places: not while another display's edit mode keeps the places out of sight.
            // appbar: nor over a full screen app, which has its display to itself (BarKeeper).
            if ((Place is not null && dock.editor is not null && !Editing) || Fullscreen)
            {
                return;
            }
            Reveal();
            // places: the rest of its edge comes with it, and goes with it as the peek ends (Settle).
            EdgeReveal.Reveal(this, dock.windows);
            peeking = true;
            Region();
            Arm(SETTLE, PEEK_MS);
        }

        public void KeepAboveDesktop()
        {
            // appbar: a full screen app's display is left to it; showing the desktop brings the bars
            // back by the reconcile, once the desktop is in front there (BarKeeper).
            if (Fullscreen)
            {
                return;
            }
            Surfaces.Show(Hwnd);
            // Over the desktop and whatever came up in front, as the plan has it (§359).
            Surfaces.Restack();
            // places: a place and an anchor stay out of sight: only the bar comes up with the desktop.
            if (!Shown && Place is null && !IsAnchor) { Shown = true; scene.Present(true, false); Region(); }
        }

        /// <summary>
        /// places: a place along an edge goes out of sight at once, and stays so while edit mode is on
        /// (<see cref="Moved"/>); an anchor too once it is no longer being edited.
        /// </summary>
        public void Withdraw()
        {
            if ((Place is null && !IsAnchor) || Editing)
            {
                return;
            }
            if (hovering)
            {
                Leave();
            }
            Kill(SETTLE);
            leaving = false;
            if (Shown)
            {
                Conceal();
            }
            else
            {
                Region();
            }
        }

        /// <summary>places: the left mouse button (the primary one, where the buttons are swapped) is down.</summary>
        private static bool ButtonHeld() =>
            GetAsyncKeyState(GetSystemMetrics(SM.SM_SWAPBUTTON) != 0 ? VK.VK_RBUTTON : VK.VK_LBUTTON) < 0;

        /// <summary>The flyout this window opened is gone; the dock may hide again.</summary>
        public void FlyoutClosed()
        {
            if (!hovering)
            {
                leaving = true;
                Arm(SETTLE, HIDE_DELAY_MS);
            }
        }

        /// <summary>
        /// Which part of the window takes the mouse. Only what is needed right
        /// now, so the rest of the window never eats a click meant for what is
        /// behind it. The Rust dock took the whole window while the pointer was
        /// on it, for the magnified icons over the panel; nothing grows here,
        /// so the panel is enough, and the tooltip room over it stays
        /// click-through. §358: through Surfaces, as a popup's (<see cref="Surfaces.Region(HWND, IReadOnlyList{RECT})"/>):
        /// grown at once, shrunk once the frame's content is on the screen, in the window that stands
        /// still round the bar (<see cref="Shift"/>).
        /// </summary>
        public void Region()
        {
            var m = layout;
            List<RECT> rects;
            // places: an anchor takes nothing, edited or not: it shows nothing.
            if (Fullscreen || IsAnchor)
            {
                rects = [default];
            }
            else if (dragging || dock.options.Demo.HasValue || morphing)
            {
                // The whole window, as it stands.
                Surfaces.Region(Hwnd, [frame is RECT whole ? new RECT { right = whole.right - whole.left, bottom = whole.bottom - whole.top } : new RECT { right = m.Width, bottom = m.Height }]);
                return;
            }
            else if (!Shown && !hovering && !leaving)
            {
                // A hidden bar takes nothing but the reveal strip along the edge; a place, its own
                // stretch of it (HoverPlace.Strip, in screen px).
                rects = Place is null
                    ? [Whole(m.HotStrip)]
                    : [new RECT { left = hover.Strip.Left - m.X, top = hover.Strip.Top - m.Y, right = hover.Strip.Right - m.X, bottom = hover.Strip.Bottom - m.Y }];
            }
            else if ((Place is not null || m.Floating) && !Editing && m.PanelReach() is { Count: > 0 } reach)
            {
                // places: a place's panels and the room between them and the edge, and nothing of the
                // rest of its third, which the windows under it keep. A dock's bar likewise: its
                // sections are docks of their own, and a press between two is the desktop's.
                rects = [.. reach.Select(Whole)];
            }
            else if (m.Floating && !Editing)
            {
                // A dock with nothing in it shows nothing, and takes no press.
                rects = [default];
            }
            else
            {
                // The panel and the edge beyond it.
                var p = m.PanelRect;
                var panel = Whole(p);
                switch (m.Edge)
                {
                    case Edge.Top:
                        panel.top = 0;
                        break;
                    case Edge.Left:
                        panel.left = 0;
                        break;
                    case Edge.Right:
                        panel.right = m.Width;
                        break;
                    default:
                        panel.bottom = m.Height;
                        break;
                }
                rects = [panel];
                // The grip on the bar's inner edge, should it reach past the panel.
                if (!m.GripRect.IsEmpty)
                {
                    rects.Add(Whole(m.GripRect));
                }
            }
            // Where the bar stands in its window.
            var (dx, dy) = ((int)Shift.X, (int)Shift.Y);
            Surfaces.Region(Hwnd, [.. rects.Select(rect => new RECT { left = rect.left + dx, top = rect.top + dy, right = rect.right + dx, bottom = rect.bottom + dy })]);
        }

        /// <summary><paramref name="r"/> in whole px, every pixel it touches.</summary>
        private static RECT Whole(RectF r) => new()
        {
            left = (int)MathF.Floor(r.X),
            top = (int)MathF.Floor(r.Y),
            right = (int)MathF.Ceiling(r.X + r.Width),
            bottom = (int)MathF.Ceiling(r.Y + r.Height),
        };

        public void Demo(float at)
        {
            Shown = true;
            hovering = true;
            Region();
            scene.Present(true, false);
            var count = Tiles.Count;
            var rect = count > 0 && layout.GetPart(BarKind.Apps) is not null
                ? layout.ItemRect(Hit.Tile(Math.Clamp((int)(Math.Clamp(at, 0f, 1f) * count), 0, count - 1)))
                : layout.PanelRect;
            pointer = rect.Center;
            hovered = layout.HitTest(pointer.X, pointer.Y);
            HoverChanged();
        }

        /// <summary>
        /// appbar: out of the way of a full screen app on this display, or back once it is gone, as
        /// the reconcile decided (BarKeeper.Look). Out of its way the window is hidden — not only put
        /// behind, so the app has the display to itself — with whatever it had open there closed, and it
        /// is laid out, raised and given its strip no more (Build, Reserve, Peek). Back, it is laid out
        /// first where anything changed meanwhile (a display mode the app had), then shown, never
        /// activated; the reconcile that brought it back holds its strip again.
        /// </summary>
        public void MakeWay(bool on)
        {
            if (on == Fullscreen)
            {
                return;
            }
            Fullscreen = on;
            if (on)
            {
                if (dock.previewOwner == this)
                {
                    dock.ClosePreview(this);
                }
                else
                {
                    KillPreviewTimers();
                }
                if (dock.FlyoutOpen && dock.pending?.Owner == this)
                {
                    dock.CloseFlyout();
                }
                // A folder or the quick settings opened from it.
                if (dock.IslandPopupOpenFor(this))
                {
                    dock.CloseIslandPopups();
                }
                hovering = false;
                leaving = false;
                hovered = null;
                // Or they would still be up when the app leaves full screen.
                scene.Highlight(null, false);
                scene.Tooltip(dock.gfx, null, null);
                // A tray icon's popup too.
                dock.SendTrayHover(trayHover.Left());
                Kill(SETTLE);
                Region();
                Surfaces.Hide(Hwnd);
            }
            else
            {
                Shown = !AutoHides;
                // The display as it is now: an app that changed its mode may have put it back only
                // just now, before the arrange that will list it again.
                Remeasure();
                if (Stale || !laidOut)
                {
                    // Built as it was asked to be while it made way (Build), before it shows.
                    Build();
                }
                scene.Present(Shown, false);
                Region();
                // Shown once what it built is on the screen: back from a full screen app, it comes up drawn.
                Surfaces.Show(Hwnd);
            }
            dock.UpdateIslands();
        }

        // --- What the pointer is over --------------------------------------

        /// <summary>The window list this bar opened is up.</summary>
        private bool OwnsOpenPreview => dock.previewOwner == this && dock.PreviewShowing;

        /// <summary>
        /// preview: when the window list opens and closes by pointing (PreviewHover), over this
        /// window's PREVIEW_OPEN and PREVIEW_CLOSE timers.
        /// </summary>
        private PreviewHover PreviewRules => previewRules ??= new PreviewHover(this);

        private PreviewHover? previewRules;

        void IPreviewTimers.Arm(PreviewTimer timer, uint ms) => Arm(PreviewTimerId(timer), ms);

        void IPreviewTimers.Kill(PreviewTimer timer) => Kill(PreviewTimerId(timer));

        private static uint PreviewTimerId(PreviewTimer timer) => timer switch
        {
            PreviewTimer.Open => PREVIEW_OPEN,
            PreviewTimer.Close => PREVIEW_CLOSE,
            _ => PREVIEW_WATCH,
        };

        /// <summary>
        /// preview: whether the pointer, as the cursor says now, is in this bar's window list (its
        /// cards or the way up to them) or on the very button it lists (anywhere on the bar, for a
        /// list opened from something that is no tile). What the watch asks (PreviewHover.Watched),
        /// past every message that may not have come. Rectangles only, as the messages' own tests
        /// are: what window is on top there is not asked, so a list is never closed under a pointer
        /// that is in it.
        /// </summary>
        private bool PointerHome()
        {
            POINT at;
            if (dock.preview is not { IsOpen: true } list || !GetCursorPos(&at))
            {
                return false;
            }
            if (list.Holds(at))
            {
                return true;
            }
            // On the button: the bar's window is under the pointer, on its panel — not a display beyond the
            // bar's outer edge, which the rectangles alone took for the button (PointerOn).
            var root = GetAncestor(WindowFromPoint(at), GA.GA_ROOT);
            var client = Local(at);
            if ((InputDesktop.Here() && root != Hwnd && !(tipWindow is { } tip && root == tip.Hwnd)) || !Over(client.X, client.Y))
            {
                return false;
            }
            return list.Tile is not { } tile || tile < 0
                || layout.HitTest(client.X, client.Y, Editing) is { Kind: HitKind.Tile } over && over.Index == tile;
        }

        /// <summary>The pointer is on this bar, as Windows says now (PointerOn).</summary>
        private bool PointerOnBar() => PointerOn();

        /// <summary>preview: this bar's list came up, or was laid out again; it is watched while it is up.</summary>
        public void PreviewShown() => PreviewRules.Shown();

        /// <summary>What lights up under the pointer: anything but the bare panel and its resize grip.</summary>
        private static Hit? Lit(Hit? hit) => hit is { Kind: HitKind.Panel or HitKind.ResizeGrip } ? null : hit;

        private void HoverChanged()
        {
            var d = dock;
            var hit = hovered;
            // peek: resting on Show desktop peeks at the desktop; never while the bar is arranged.
            DesktopPeekHover(Editing ? null : hit);
            // In edit mode nothing reacts to hover but the blocks, and
            // only a badge or a zone has a tooltip.
            if (Editing)
            {
                // quick: nothing opens by pointing while a bar is being arranged, and anything
                // that was open by pointing goes.
                HoverFlyout(null, editing: true);
                EditHoverChanged();
                return;
            }
            // quick: a widget's own flyout, where that is switched on (WidgetHover); nothing at all
            // otherwise, which is the default.
            HoverFlyout(hit, editing: false);
            // quick: a widget whose state has no news of its own reads it as the pointer comes onto it
            // (a monitor's brightness, docs\widgets.md §439).
            if (hit is { Kind: HitKind.Island } onto && IslandAt(onto) is CellIsland pointed)
            {
                pointed.PointerCame();
            }
            var menu = d.FlyoutOpen;
            // preview: a rest on an app button opens its window list, where previews are on, after
            // a short guard; with the list up, the next button's replaces it at once (PreviewHover).
            // app clicks: a list a click or the keys put up is the pointer's no more (Dock.AppClicks.cs).
            if (OwnsOpenPreview && d.previewHeld)
            {
                PreviewRules.Stop();
            }
            else if (!menu
                && hit is { Kind: HitKind.Tile } over
                && ShownLayout.Previews
                && TileAt(over.Index) is { Open.Count: > 0 })
            {
                if (PreviewRules.OnButton(over.Index, OwnsOpenPreview ? d.preview?.Tile : null))
                {
                    d.OpenPreview(this, over.Index);
                }
            }
            else
            {
                PreviewRules.Off(OwnsOpenPreview);
            }
            var lit = Lit(hit);
            scene.Highlight(lit, lit is not null && lit == PressShown);
            // No tooltip over an open window list or flyout, as on the taskbar.
            if (OwnsOpenPreview || menu)
            {
                tipPart = null;
                scene.Tooltip(d.gfx, null, null);
                return;
            }
            ShowTooltip(hit);
        }

        /// <summary>
        /// quick: what the pointer at <paramref name="hit"/> does to a widget's flyout or a folder, where
        /// opening them by pointing is switched on (<see cref="TaskbarLayout.OpenOnHover"/>, off by
        /// default). The rules are <see cref="WidgetHover"/>'s; this only arms the timers.
        /// </summary>
        private void HoverFlyout(Hit? hit, bool editing)
        {
            var d = dock;
            var shown = d.hoverOpen is { } open && open.Window == this ? open.Hit : (Hit?)null;
            switch (WidgetHover.Pointer(WidgetHover.On(ShownLayout), editing, OpensOnHover(hit), hit, shown, d.AnyPopupOpen))
            {
                case WidgetHoverAct.Arm:
                    Kill(FLYOUT_CLOSE);
                    Arm(FLYOUT_OPEN, WidgetHover.OpenMs);
                    break;
                case WidgetHoverAct.Switch:
                    Kill(FLYOUT_OPEN);
                    Kill(FLYOUT_CLOSE);
                    d.OpenHoverFlyout(this, hit!.Value);
                    break;
                case WidgetHoverAct.Leave:
                    Kill(FLYOUT_OPEN);
                    Arm(FLYOUT_CLOSE, WidgetHover.CloseMs);
                    break;
                default:
                    // On the widget whose flyout is up, or while another popup has the screen:
                    // nothing opens, nothing closes, nothing is waiting.
                    Kill(FLYOUT_OPEN);
                    Kill(FLYOUT_CLOSE);
                    break;
            }
        }

        /// <summary>
        /// Whether a rest on the widget at <paramref name="hit"/> opens something (<see cref="WidgetHover.Opens"/>), where its
        /// layout opens what the widgets open on hover (§514: one switch): a folder with something to show (Q7, §334), or a
        /// flyout of Karpo's own — asked of the kind (<see cref="Dock.HasFlyout"/>), never of the widget: a widget's
        /// <c>Click</c> does things, and pointing at one must do none of them.
        /// </summary>
        internal bool OpensOnHover(Hit? hit) =>
            hit is { Kind: HitKind.Island } cell && IslandAt(cell) is { } island
            && WidgetHover.Opens(ShownLayout, island is FolderIsland { IsFolder: true }, island is FolderIsland { IsFolder: true } folder && OneFolder.Opens(folder.Empty, arranging: false), HasFlyout(KindAt(cell)));

        /// <summary>quick: looks again after the grace, while the pointer is still on the open panel.</summary>
        internal void ArmHoverClose() => Arm(FLYOUT_CLOSE, WidgetHover.CloseMs);

        /// <summary>quick: nothing is waiting to open or close by pointing (a press, a bar going away).</summary>
        internal void KillHoverTimers()
        {
            Kill(FLYOUT_OPEN);
            Kill(FLYOUT_CLOSE);
        }

        // --- The tray icon under the pointer ----------------------------------

        /// <summary>
        /// The pointer at <paramref name="hit"/>: a tray icon hears every move over it, and the
        /// one it came onto is watched for the pointer to rest (WM_MOUSEHOVER, after the system's
        /// hover time within its hover rectangle). A bar being edited reports no icon, nor
        /// does one dragging a tile or a pin across its icons.
        /// </summary>
        private void TrayPointer(Hit? hit)
        {
            var d = dock;
            var icon = !Editing && pinDrag is null ? d.TrayIconAt(this, hit) : null;
            var sends = trayHover.Moved(icon, out var entered);
            if (entered)
            {
                // tray: where a bar holds two, the app's popup opens by the icon the pointer is on.
                if (layout.TrayParts.Count > 1)
                {
                    d.PublishTrayRects(this, hit);
                }
                // Leave tracking stays on with it; failing, the icon only misses its popup.
                var track = new TRACKMOUSEEVENT
                {
                    cbSize = (uint)sizeof(TRACKMOUSEEVENT),
                    dwFlags = TME.TME_HOVER | TME.TME_LEAVE,
                    hwndTrack = Hwnd,
                    dwHoverTime = HOVER_DEFAULT,
                };
                _ = TrackMouseEvent(&track);
            }
            d.SendTrayHover(sends);
        }

        /// <summary>
        /// The pointer rests: the tray icon under it may open its popup, but not over an open
        /// flyout or window list, where no tooltip shows either.
        /// </summary>
        public void Rested()
        {
            if (!hovering || Editing || dock.FlyoutOpen || OwnsOpenPreview)
            {
                return;
            }
            dock.SendTrayHover(trayHover.Rested());
        }

        /// <summary>A button went down on the tray icon under the pointer: its popup goes, and doesn't come back while the pointer stays.</summary>
        public void TrayPressed() => dock.SendTrayHover(trayHover.Pressed());

        /// <summary>
        /// The tooltip over <paramref name="hit"/> (BarTooltips): an app's button its window's title or
        /// its name, a tray icon the whole of its standard tooltip, as the taskbar shows
        /// it, and none where the app draws a popup of its own in its place (TrayTip). Over a widget or
        /// a folder only the status icon under the pointer has one (§367, §382), standing over that
        /// icon (<c>Part</c>, in the cell's px).
        /// </summary>
        private (string? Text, RectF? Part) TooltipFor(Hit? hit)
        {
            RectF? part = null;
            var text = BarTooltips.For(hit,
                index => TileAt(index) is not { } tile ? null
                    : BarTooltips.ForTile(tile.Item.Name, tile.Label, tile.Broken),
                icon => dock.TrayIconAt(this, icon) is { } at ? TrayTip(at) : null,
                cell =>
                {
                    var (point, size) = CellPoint(cell, pointer.X, pointer.Y);
                    if (size.X <= 0 || size.Y <= 0)
                    {
                        return null;
                    }
                    var island = IslandAt(cell);
                    var status = island is FolderIsland folder ? folder.StatusTipAt(point, size, IslandLook()) : null;
                    // Words the face drew cut short say themselves whole, first where a status icon has its own (CutWords).
                    var cut = (island as CellIsland)?.CutAt(cell.Index, point);
                    if (CutWords.Said(cut?.Text, status?.Text) is not { } said)
                    {
                        return null;
                    }
                    var box = status?.Box ?? cut!.Value.Box;
                    part = new RectF(box.left, box.top, box.right - box.left, box.bottom - box.top);
                    return said;
                });
            return (text, part);
        }

        /// <summary>
        /// The tooltip for where the pointer is now, and which status icon it was for: the pointer
        /// going from one status icon of a folder's button to the next changes no hit, only this.
        /// </summary>
        private void ShowTooltip(Hit? hit)
        {
            var (text, part) = TooltipFor(hit);
            tipPart = part;
            scene.Tooltip(dock.gfx, text, text is null ? null : hit, part);
        }

        /// <summary>§382: the status icon whose tooltip is up, in its cell's px; null for none or a whole item's.</summary>
        private RectF? tipPart;

        // --- Clicks ----------------------------------------------------------

        public void Pressed(float x, float y, bool middle = false)
        {
            var hit = hovering ? layout.HitTest(x, y, Editing) : null;
            // A press on the block whose options are open closes them,
            // and its release must not open them again, as on the taskbar.
            // (menus: the dock's PressedOn already closed them and took that press.)
            optionsClosed = Editing ? dock.OptionsShownFor(this) : null;
            // In edit mode a press never reaches a widget.
            if (Editing)
            {
                if (!middle)
                {
                    EditPressed(x, y, hit);
                }
                return;
            }
            // §534: a locked bar's grip drags nothing at rest; it is still the bar's, for its menu (Dock.BarMove.cs).
            if (hit is { Kind: HitKind.ResizeGrip } && Arranges)
            {
                BeginResize();
                return;
            }
            // quick: a press on the volume slider grabs its thumb and takes the pointer (Dock.Quick.cs).
            if (SliderPressed(hit, x, y))
            {
                return;
            }
            // pins: a press on a pin or a folder may become a drag along the bar (Dock.Pins.cs); §663 not on a locked bar.
            PinPressed(hit, x, y);
            // §534: one on the bar's own room, unlocked, may become the bar carried to another edge (Dock.BarMove.cs).
            if (!middle)
            {
                BarMovePressed(hit, x, y);
            }
            pressed = hit;
            // The Windows 11 press: the icon gives a little, and springs back
            // on release.
            scene.Press(PressShown, true);
            var lit = Lit(hovered);
            scene.Highlight(lit, lit is not null && lit == PressShown);
        }

        /// <summary>
        /// What the press shows on: what was pressed, where its click does something (Dock.ClickDoes,
        /// §287) — on an empty folder or a widget greyed nothing gives and nothing wears the pressed fill.
        /// </summary>
        private Hit? PressShown => pressed is { } down && dock.ClickDoes(this, down) ? Lit(down) : null;

        public void Released(float x, float y, bool middle)
        {
            if (Editing && sizing is null)
            {
                if (!middle)
                {
                    EditReleased(x, y);
                }
                return;
            }
            // quick: the volume slider lets the pointer go; its click is its own (Dock.Quick.cs).
            if (SliderReleased())
            {
                pressed = null;
                return;
            }
            // pins: a pin dragged along the bar lands where it showed it would (Dock.Pins.cs).
            if (PinReleased(x, y))
            {
                pressed = null;
                return;
            }
            // tray: a tray icon dragged to the bar's tray or to the overflow (Dock.Tray.cs).
            if (TrayReleased(x, y))
            {
                pressed = null;
                return;
            }
            // §534: the bar carried lands on the edge it showed (Dock.BarMove.cs); one that never moved was a click.
            if (!middle && BarMoveReleased(x, y))
            {
                pressed = null;
                return;
            }
            if (sizing is not null)
            {
                EndResize(keep: true);
                return;
            }
            var hit = hovering ? layout.HitTest(x, y) : null;
            var down = pressed;
            pressed = null;
            scene.Press(Lit(down), false);
            scene.Highlight(Lit(hovered), false);
            if (hit is Hit up && up == down)
            {
                // A button inside a widget's cell takes its own click: the next meeting's Join (Dock.Quick.cs).
                if (!middle && PointClicked(up, x, y))
                {
                    return;
                }
                dock.Click(this, up, middle);
            }
        }

        // --- Edit mode -------------------------------------------------------

        /// <summary>This bar goes into edit mode: shown whatever hides it otherwise, nothing picked up.</summary>
        public void BeginEditing()
        {
            EndDrag();
            focusKey = null;
            focusShown = false;
            // appbar: a bar out of a full screen app's way is back already: edit mode shows every bar
            // (BarKeeper.Look, from BeginEdit), and the reconcile after it hides it again.
            KillPreviewTimers();
            Kill(SETTLE);
            leaving = false;
            scene.Tooltip(dock.gfx, null, null);
            // places: an anchor stays out of sight; its display's places come in, and stay, taking
            // the pointer over their whole bar (Region) for their "+" and what is dropped on them.
            if (!Shown && !IsAnchor)
            {
                Reveal();
                Region();
            }
        }

        /// <summary>
        /// Edit mode is over: a bar that hides by itself does so again (and a full screen app that
        /// came meanwhile hides it by the arrange's reconcile, BarKeeper).
        /// </summary>
        public void EndEditing()
        {
            EndDrag();
            focusKey = null;
            focusShown = false;
            scene.Tooltip(dock.gfx, null, null);
            if (IsAnchor)
            {
                // places: an anchor shows nothing once it is no longer edited.
                Withdraw();
            }
            else if (!hovering && AutoHides)
            {
                leaving = true;
                Arm(SETTLE, HIDE_DELAY_MS);
            }
        }

        /// <summary>
        /// A press on the bar being edited. On a block it picks the block up at
        /// once, no threshold: the block follows the pointer from here. A badge
        /// acts on its release, as a button does; the grip still drags the
        /// height.
        /// </summary>
        private void EditPressed(float x, float y, Hit? hit)
        {
            // edit: a widget following the pointer (Dock.EditFollow.cs) is put down by the click,
            // where it shows it would land; the release that comes after finds nothing carried.
            if (dock.Follows(dragKey))
            {
                EditReleased(x, y);
                return;
            }
            // edit: a "+" standing aside for a folder of this bar open (§397) is not there: a press where it
            // stood is on the bar's room, which closes the folder (§366).
            if (hit is { Kind: HitKind.Add } && dock.FolderOpenOn(this))
            {
                hit = null;
            }
            // §617: the add page's ghost, empty or a pick's, is in no config yet: a press on it is on the bar's room.
            if (hit is { } struck && layout.PartOf(struck) is var ghosted and >= 0 && dock.GhostPart(layout) == ghosted)
            {
                hit = null;
            }
            var part = hit is { } on ? layout.PartOf(on) : -1;
            // edit: a section's "+" (Layout.Adds) takes the press as a widget does.
            var press = EditClicks.OnPress(hit is { Kind: HitKind.ResizeGrip }, (part >= 0 && layout.Placed(part)) || hit is { Kind: HitKind.Add });
            // edit: a folder open arranged has the edit mode to itself; the bars under it are locked
            // (§212): a press on its face closes it, as a second click on a folder at rest does (§251,
            // §267); anywhere else the folder closes, as by Escape, and the press goes on as on a bar not
            // locked — another folder opens in the one click, any other widget's menu, as a right click's
            // does (§287, §289); on the room round the widgets the folder alone closes, edit mode staying
            // (§366: "when clicking away on the taskbar it should close the current folder").
            if (dock.BarLocked)
            {
                if (EditClicks.OnLocked(part, dock.ClosingPart(this)))
                {
                    LockedPressed();
                    return;
                }
                dock.EscapeTo(EditEscape.CloseFolder);
            }
            dock.FocusBar(this);
            // edit: the bar has the focus now, so an edit bar's own ring goes — one ring, one place.
            dock.ClearRailFocus();
            scene.Tooltip(dock.gfx, null, null);
            emptyPressed = null;
            switch (press)
            {
                case EditPress.Resize:
                    BeginResize();
                    return;
                // edit: a press on the bar's or the place's empty room: a drag draws the selection
                // rectangle, as on the dim; a click clears the selection (Dock.EditSelect.cs).
                case EditPress.Select:
                    emptyPressed = new Vector2(x, y);
                    dock.BandPressed(Id, OnScreen(emptyPressed.Value), KeyState.Down(VK.VK_CONTROL) || KeyState.Down(VK.VK_SHIFT));
                    SetCapture(Hwnd);
                    return;
            }
            // edit: a "+" acts on its release, as a button does (AddReleased).
            if (hit is { Kind: HitKind.Add } plus)
            {
                addPressed = plus.Index;
                SetCapture(Hwnd);
                scene.Edit(EditView());
                return;
            }
            dragKey = layout.Specs[part].Key;
            // edit: one of several selected carries them all (Dock.EditSelect.cs).
            group = dock.GroupFor(dragKey);
            focusKey = dragKey;
            focusShown = false;
            grabOffset = (layout.Vertical ? y : x) - layout.PartAt(part).Left;
            grabAt = GrabIn(part, new Vector2(x, y));
            pressAt = new Vector2(x, y);
            dragMoved = false;
            SetCapture(Hwnd);
            SetCursor(LoadCursorW(HINSTANCE.NULL, IDC.IDC_SIZEALL));
            Arm(DRAG, DRAG_MS);
            scene.Edit(EditView());
            MoveCarry();
        }

        /// <summary>
        /// pins: edit mode takes over a drag that began outside it (<see cref="Dock.BeginEditFromDrag"/>):
        /// the same block, held where the pointer holds it, past the drag threshold already, so its
        /// ghost follows the pointer, where it would land shows at once, and the release lands it.
        /// False where the widget has no block to carry: the build edit mode brought may have left it
        /// without one.
        /// <para>
        /// Where the block is held is <b>measured again here</b> (<see cref="PinDrags.GrabOffset"/>)
        /// rather than carried over: the build gives a widget with nothing to show room to be grabbed
        /// by, and every section its "+", either of which may move the block a little. Measured from
        /// where the pointer is now, it cannot jump on the next move.
        /// </para>
        /// </summary>
        public bool AdoptDrag(string key)
        {
            var part = layout.PartIndex(key);
            if (part < 0 || !layout.Placed(part))
            {
                return false;
            }
            var block = layout.PartAt(part);
            var at = pointer;
            emptyPressed = null;
            dragKey = key;
            // edit: a widget dragged in at rest is carried alone: nothing was selected before edit mode.
            group = null;
            focusKey = key;
            focusShown = false;
            grabOffset = PinDrags.GrabOffset(layout.Vertical ? at.Y : at.X, block.Left, block.Width);
            grabAt = GrabIn(part, at);
            pressAt = pointer;
            // Already past the threshold: that is the whole reason edit mode is on. So the release
            // lands the block rather than reading as a click and opening its options.
            dragMoved = true;
            SetCapture(Hwnd);
            SetCursor(LoadCursorW(HINSTANCE.NULL, IDC.IDC_SIZEALL));
            // The hold that makes a folder, and Escape, which the bar may not have the keys to hear.
            Arm(DRAG, DRAG_MS);
            scene.Edit(EditView());
            DragMoved();
            return true;
        }

        /// <summary>A dragged block follows the pointer, and where it would land shows as it goes.</summary>
        private void DragMoved()
        {
            if (!dragMoved)
            {
                dragMoved = Math.Abs(pointer.X - pressAt.X) > GetSystemMetrics(SM.SM_CXDRAG)
                    || Math.Abs(pointer.Y - pressAt.Y) > GetSystemMetrics(SM.SM_CYDRAG);
            }
            // edit: the selected widget's flyout closes while anything is carried, and opens again
            // where it lands (Dock.FlyoutPreview.cs).
            if (dragMoved && dock.selectedShown is not null)
            {
                dock.SyncSelectedFlyout(selectionChanged: false);
            }
            // edit: several carried together: the surface nearest the pointer, every surface they came
            // from closing up behind them (Dock.EditSelect.cs). Never into an open folder, nor made a
            // folder of.
            if (group is { } together)
            {
                if (dragMoved)
                {
                    dock.GroupOffer(this, OnScreen(pointer), together);
                }
                scene.Edit(EditView());
                MoveCarry();
                return;
            }
            // folders: over a folder standing open, the folder has the block: it shows where the
            // widget would land, or says why it won't, and the bar's own marks give way to it
            // (EditView). The bar holds the mouse, so the folder is asked here — it sees no pointer.
            var wasOver = overFolder;
            overFolder = dragMoved && dragKey is { } key
                && dock.config.FindIsland(Islands.OwnerOf(key), Islands.IdOf(key)) is { } carried
                && dock.FolderOffer(OnScreen(pointer), carried);
            if (wasOver && !overFolder)
            {
                dock.FolderNoOffer();
            }
            // places: anywhere on the screen the block lands somewhere: on the surface nearest the
            // pointer of the display it is over, any display (§519) — a place along an edge, or the bar
            // (Dock.BarOffer, EditDrops) —
            // which shows its ghost where it would land and opens the room for it, while this one
            // closes up behind it. This window holds the mouse, so the others are asked here: they
            // see no pointer. While this one is the nearest, the drag goes on here as ever.
            var target = dragMoved && !overFolder && dragKey is { } carriedKey && layout.Carry(carriedKey) is { } measured
                ? dock.BarOffer(this, OnScreen(pointer), measured)
                : null;
            if (overBar is { } before && before != target)
            {
                before.NoOffer();
            }
            overBar = target;
            scene.Edit(EditView());
            MoveCarry();
        }

        /// <summary>
        /// edit: where the pointer took hold of the block being carried, from its block's corner
        /// (layout px), so the carry window holds it under the pointer at the same place.
        /// </summary>
        private Vector2 grabAt;

        /// <summary>
        /// edit: the carried block in the carry window, at the pointer wherever it is on the screen
        /// (Dock.EditCarry.cs): drawn from where the scene lifted it, held where it was taken hold of.
        /// </summary>
        private void MoveCarry()
        {
            // edit: several carried together, each drawn at the pointer once the drag is under way.
            if (group is { } together && dragKey is { } held)
            {
                if (dragMoved)
                {
                    dock.ShowGroupCarry(this, together, held, grabAt, OnScreen(pointer));
                }
                return;
            }
            if (dragKey is null || scene.Carried() is not { } carried)
            {
                return;
            }
            var at = OnScreen(pointer);
            dock.ShowCarry(carried.Source, carried.Block, new POINT { x = at.x - (int)MathF.Round(grabAt.X), y = at.y - (int)MathF.Round(grabAt.Y) });
        }

        /// <summary>edit: where <paramref name="at"/> (layout px) is in block <paramref name="part"/>, held inside it.</summary>
        private Vector2 GrabIn(int part, Vector2 at)
        {
            // What the carry window shows of it (Scene.Carried): its block.
            var block = layout.BlockRect(part);
            return new Vector2(Math.Clamp(at.X - block.X, 0f, block.Width), Math.Clamp(at.Y - block.Y, 0f, block.Height));
        }

        /// <summary>
        /// places: the other surface — of this display or another (§519) — a block carried from here goes
        /// to, which has it (<see cref="Dock.BarOffer"/>); null while this one is the nearest.
        /// </summary>
        private DockWindow? overBar;

        /// <summary>
        /// places: where a block carried from another surface — of any display (§519) — would land here,
        /// as this window's parts count it, and this bar laid out with it in (its ghost, and the room it opens).
        /// </summary>
        private (BarSlot Slot, Landed Landed)? offered;

        /// <summary>
        /// places: a block carried from another surface (<paramref name="carried"/>, as that one measured
        /// it, at its display's scale) is at <paramref name="at"/> on the screen, and this is the surface
        /// nearest it: this one shows where it would land — the nearest place along it, however far
        /// off the pointer is — its ghost at the size it will have here, at this display's scale (§519),
        /// the others making room.
        /// </summary>
        public void Offer(POINT at, Layout.Carried carried)
        {
            var slot = layout.DropSlot(AlongAt(at), -1);
            if (offered is { } was && was.Slot == slot)
            {
                return;
            }
            offered = (slot, layout.Landing(slot, carried));
            scene.Edit(EditView());
        }

        /// <summary>places: no block carried from elsewhere is over this window any more.</summary>
        public void NoOffer()
        {
            var had = offered is not null;
            offered = null;
            if (had && Editing)
            {
                scene.Edit(EditView());
            }
        }

        /// <summary>
        /// places: the block <paramref name="key"/>, carried from another surface — of this display or
        /// another, whose layout it then leaves for this one's (§519) — let go with this one the nearest: it
        /// lands where this window showed it would, in this window's place (<see cref="ListSlot"/>). False
        /// where it showed nothing.
        /// </summary>
        public bool TakeOffered(string key)
        {
            var slot = offered?.Slot;
            NoOffer();
            if (slot is not { } at)
            {
                return false;
            }
            dock.MoveIslands([key], this, ListSlot(at, [key]));
            return true;
        }

        /// <summary>
        /// places: what a carried block over the display goes to this window by: its bar and the room
        /// between it and the screen's edge, in screen px (<see cref="EditDrops.Nearest"/>). An anchor,
        /// or a window out of sight, takes nothing.
        /// </summary>
        public RECT? DropArea() => !Editing || IsAnchor || !Shown ? null : Takes();

        /// <summary>
        /// places: this window's bar and the room between it and the screen's edge, in screen px:
        /// where a carried block is over it.
        /// </summary>
        public RECT Takes()
        {
            var panel = PanelOnScreen();
            var bounds = monitor.Bounds;
            switch (layout.Edge)
            {
                case Edge.Top:
                    panel.top = bounds.Top;
                    break;
                case Edge.Left:
                    panel.left = bounds.Left;
                    break;
                case Edge.Right:
                    panel.right = bounds.Right;
                    break;
                default:
                    panel.bottom = bounds.Bottom;
                    break;
            }
            return panel;
        }

        /// <summary>A point in the layout's px, on the screen: where a folder standing open is asked about it.</summary>
        private POINT OnScreen(Vector2 local) =>
            new() { x = layout.X + (int)MathF.Round(local.X), y = layout.Y + (int)MathF.Round(local.Y) };

        /// <summary>
        /// A release on the bar being edited: a dragged block lands where it
        /// showed it would; one that never left the drag threshold opens its
        /// settings instead (<see cref="Dock.EditActivate"/>).
        /// Nothing on the bar removes a widget: that is the block's own menu and the
        /// Delete key (<see cref="RemoveIsland"/>).
        /// </summary>
        private void EditReleased(float x, float y)
        {
            // edit: the open folder's face on a bar locked under it, whose click closes it (§251).
            if (CloseReleased(x, y))
            {
                return;
            }
            // edit: a "+" released on itself opens the add list for its end of its section.
            if (AddReleased(x, y))
            {
                return;
            }
            if (dragKey is not { } key)
            {
                // edit: a drag from the bar's or the place's empty room selected what it ran over; a
                // click there clears the selection (Dock.EditSelect.cs).
                if (emptyPressed is not null)
                {
                    emptyPressed = null;
                    if (GetCapture() == Hwnd)
                    {
                        _ = ReleaseCapture();
                    }
                    if (!dock.BandReleased(OnScreen(new Vector2(x, y))))
                    {
                        dock.EmptyClicked(this);
                    }
                }
                return;
            }
            // edit: a widget that followed the pointer lands as any carried block does, and follows
            // no more.
            dock.FollowLanded(key);
            // edit: several carried together land side by side where the landing showed
            // (Dock.EditSelect.cs).
            if (dragMoved && group is { } together)
            {
                var landing = dock.GroupLanding();
                EndDrag();
                if (landing is { } to)
                {
                    dock.MoveIslands(together, to.Window, to.Window.ListSlot(to.Slot, together));
                }
                else
                {
                    scene.Edit(EditView());
                }
                return;
            }

            // folders: let go over a folder standing open, the folder has it — and refuses it, as
            // nothing moves from the bar into a folder, saying why (Dock.DropIntoOpenFolder). What it
            // offered is read before the drag ends, which takes the offer back.
            if (dragMoved && overFolder && dock.FolderOffered() is { } letGo)
            {
                EndDrag();
                dock.DropIntoOpenFolder(letGo);
                // The block goes back into its slot, as a drop on nothing does.
                scene.Edit(EditView());
                return;
            }
            // places: let go with another surface the nearest — of any display (§519) — that surface
            // has it, where it showed, in its place.
            if (dragMoved && overBar is { } other)
            {
                overBar = null;
                EndDrag();
                if (!other.TakeOffered(key))
                {
                    scene.Edit(EditView());
                }
                return;
            }
            var view = EditView();
            var moved = dragMoved;
            EndDrag();
            var part = layout.PartIndex(key);
            if (!moved || part < 0)
            {
                // A click, not a drag: nothing was lifted (§387), the block keeps the focus the press
                // gave it and is selected, a folder's face opening its folder (EditClicks.OnClick, §527) —
                // unless this click closed its menu, which is all it does then.
                // edit: with Ctrl held it goes into the selection or out of it, with Shift the run
                // from the one last clicked is selected (Dock.EditSelect.cs).
                scene.Edit(EditView());
                dock.EditActivate(this, part, OnScreen(new Vector2(x, y)), closedItsMenu: key == optionsClosed);
                optionsClosed = null;
                return;
            }
            // No scene.Edit here: the rebuild's scene glides the block from
            // where it was let go into its slot.
            dock.MoveIslands([key], this, ListSlot(view.DropIndex, [key]));
        }

        /// <summary>A dragged block goes back where it was (Escape, a right press); false when none was dragged.</summary>
        public bool CancelDrag()
        {
            if (dragKey is null)
            {
                return false;
            }
            // edit: a widget following the pointer, called off, goes back where it came from.
            var followed = dock.Follows(dragKey);
            EndDrag();
            scene.Edit(EditView());
            if (followed)
            {
                dock.DropFollow();
            }
            return true;
        }

        /// <summary>Forgets the dragged block: no more capture or watching Escape.</summary>
        private void EndDrag()
        {
            dragKey = null;
            dragMoved = false;
            // edit: nor several carried together.
            EndGroup();
            // edit: the carry window goes with the drag; the block is back on its bar, or landing.
            dock.HideCarry();
            // edit: and the selected widget's flyout opens again, where it landed.
            dock.SelectedFlyoutSoon();
            // The next drag starts its hold and its carry afresh, even from the same press point.
            editHoldFor = new Vector2(float.NaN, float.NaN);
            if (overFolder)
            {
                // folders: nothing is being carried over the open folder any more.
                overFolder = false;
                dock.FolderNoOffer();
            }
            // places: nor over another surface of the display.
            if (overBar is { } other)
            {
                overBar = null;
                other.NoOffer();
            }
            Kill(DRAG);
            if (GetCapture() == Hwnd && sizing is null)
            {
                ReleaseCapture();
            }
        }

        /// <summary>edit: the "+" held down (<see cref="Layout.Adds"/>), whose release on it adds there; -1 for none.</summary>
        private int addPressed = -1;

        /// <summary>
        /// edit: the release of a press on a "+": on it, the add list opens for that end of its section
        /// (<see cref="Dock.AddAtPlus"/>); off it, nothing. False where no "+" was pressed.
        /// </summary>
        private bool AddReleased(float x, float y)
        {
            if (addPressed < 0)
            {
                return false;
            }
            var index = addPressed;
            addPressed = -1;
            if (GetCapture() == Hwnd)
            {
                _ = ReleaseCapture();
            }
            if (hovering && layout.HitTest(x, y, true) is { Kind: HitKind.Add } on && on.Index == index)
            {
                dock.AddAtPlus(this, index);
            }
            scene.Edit(EditView());
            return true;
        }

        /// <summary>edit: a "+" held down lost the mouse: it lets go, and nothing opens.</summary>
        private void AddCaptureLost()
        {
            if (addPressed >= 0)
            {
                addPressed = -1;
                scene.Edit(EditView());
            }
        }

        /// <summary>
        /// edit: where the add list of a "+" at <paramref name="rect"/> (layout px) hangs from: the
        /// button's middle along the bar, the bar's inner side across it, as <see cref="PointerAnchor"/>
        /// takes the pointer's.
        /// </summary>
        public POINT AnchorAt(RectF rect) => Anchor(OnScreen(rect.Center));

        /// <summary>What edit mode shows on this bar now (see <see cref="Scene.Edit"/>).</summary>
        private EditView EditView()
        {
            var at = pointer;
            var along = layout.Vertical ? at.Y : at.X;
            // edit: several carried together are this window's group view's, not one drag's. §387: a press is
            // no carry until the pointer leaves the drag threshold — a click lifts nothing, opens no gap and
            // hides no "+" for as long as it is held (the person: "when i open a folder in edit mode the blue
            // plus buttons flicker").
            var dragging = dragKey is { } key && group is null && dragMoved ? layout.PartIndex(key) : -1;
            BarSlot index = default;
            if (dragging >= 0 && layout.Placed(dragging))
            {
                // The pointer decides where the block lands, not the block's own middle. The
                // middle was kept on the bar, as the scene keeps the block itself — and a block
                // kept on the bar can only reach half its width from either end, which is short of
                // the end widget's middle whenever the block is the wider of the two. The last
                // slot was then unreachable by arithmetic rather than by rule, and the pointer sat on
                // the end widget with nowhere left to go ("moving a widget all the way to the left or
                // right is not possible"). The pointer is inside the block it carries, so the gap
                // still opens where the block is. folders: an open folder has the drop — the bar
                // stays as it stood when the pointer went onto the folder (BarCarry.cs).
                var shown = FolderDrop(dragging, along);
                along = shown.Along;
                index = shown.Index;
            }
            else
            {
                dragging = -1;
            }
            // edit: a block lights under the pointer, on a bar locked under an open folder (§212) too,
            // whose widgets a click still reaches (§267, §289).
            var hit = hovering && dragging < 0 && group is null ? layout.HitTest(at.X, at.Y, true) : null;
            int? block = hit switch
            {
                { Kind: not (HitKind.Panel or HitKind.ResizeGrip) } on when layout.PartOf(on) is var part and >= 0 => part,
                _ => null,
            };
            var focused = focusShown ? FocusedPart : -1;
            var view = new EditView(
                dragging >= 0 ? dragging : null,
                along,
                grabOffset,
                index,
                focused >= 0 ? focused : null,
                block,
                // edit: the widget last clicked, ringed as selected while it is on this bar.
                Selected: EditClicks.SelectedPart(dock.editPicked, layout.Specs.Select(spec => spec.Key).ToList()),
                // places: carried over another surface of the display, or an open folder, which has it.
                Away: dragging >= 0 && (overFolder || overBar is not null),
                // places: a block carried from another surface of the display shows here where it
                // would land, at its size here, the others opening the room for it.
                Offered: offered?.Landed,
                // edit: the others selected with it, each ringed; and several carried together, this
                // window's own of them out of sight and the bar standing as the group leaves it
                // (Dock.EditSelect.cs).
                AlsoSelected: dock.AlsoSelectedOn(layout),
                Group: groupShown?.Parts,
                GroupLanding: groupShown?.Landing,
                // edit: what is on the clipboard, in the marching ants (Dock.EditCopy.cs).
                Copied: dock.CopiedOn(layout),
                // edit: the "+" under the pointer, and the one held down.
                AddLit: hit is { Kind: HitKind.Add } plus ? plus.Index : null,
                AddPressed: addPressed >= 0 ? addPressed : null,
                // edit: a folder of this bar open, whose own "+" are the ones to add with (§397).
                FolderOpen: dock.FolderOpenOn(this),
                // §593: the add page's ghost, where it stands on this bar; §617 the empty one from the page's opening.
                Ghost: dock.GhostPart(layout),
                GhostEmpty: dock.Ghost?.Empty == true);
            return view;
        }

        /// <summary>
        /// Hover in edit mode: the block under the pointer lights up and says nothing — the blocks
        /// are what you are moving, and a name over one is in the way of seeing where it would go.
        /// </summary>
        private void EditHoverChanged()
        {
            Kill(PREVIEW_OPEN);
            scene.Highlight(null, false);
            scene.Edit(EditView());
            scene.Tooltip(dock.gfx, null, null);
        }

        /// <summary>edit: what the keys walk through, in their order along the bar: every block, by its part, where it stands.</summary>
        private List<int> FocusOrder() => EditStops.Order(layout);

        /// <summary>edit: the keyboard focus to block <paramref name="part"/>, shown.</summary>
        private void FocusStop(int part)
        {
            focusKey = part >= 0 ? layout.Specs[part].Key : null;
            focusShown = true;
            scene.Edit(EditView());
        }

        /// <summary>
        /// The keyboard focus to the next block along the bar (Tab, the arrows), round from the end.
        /// </summary>
        public void MoveFocus(int step, bool round = true)
        {
            var order = FocusOrder();
            if (order.Count == 0)
            {
                return;
            }
            var at = order.IndexOf(FocusedPart);
            at = at < 0 ? (step > 0 ? 0 : order.Count - 1)
                : round ? (at + step + order.Count) % order.Count
                : Math.Clamp(at + step, 0, order.Count - 1);
            FocusStop(order[at]);
        }

        /// <summary>edit: the keyboard focus to the first block along the bar (Home) or the last (End).</summary>
        public void FocusEnd(bool last)
        {
            var order = FocusOrder();
            if (order.Count > 0)
            {
                FocusStop(last ? order[^1] : order[0]);
            }
        }

        /// <summary>edit: the keyboard focus to the block keyed <paramref name="key"/> on this surface now, shown.</summary>
        public void FocusKey(string key) => FocusStop(layout.PartIndex(key));

        /// <summary>The focus to the block keyed <paramref name="key"/>, which may only come with the next build.</summary>
        public void FocusOn(string key)
        {
            focusKey = key;
            focusShown = false;
        }

        /// <summary>The block keyed <paramref name="key"/> is going: the focus moves to its neighbour.</summary>
        public void Removed(string key)
        {
            if (focusKey != key)
            {
                return;
            }
            var order = FocusOrder();
            var at = order.IndexOf(layout.PartIndex(key));
            var next = at < 0 || order.Count < 2 ? -1 : order[at + 1 < order.Count ? at + 1 : at - 1];
            focusKey = next >= 0 ? layout.Specs[next].Key : null;
        }

        // --- Resizing --------------------------------------------------------

        /// <summary>
        /// The resize cursor over the grip, and all through a drag, which holds
        /// the mouse and so gets no WM_SETCURSOR of its own.
        /// </summary>
        public bool SetsCursor()
        {
            if (sizing is null)
            {
                var local = Local(Cursor());
                var hit = layout.HitTest(local.X, local.Y, Editing);
                // A block moves wherever it is taken hold of.
                if (Editing && (dragKey is not null || hit is { Kind: not (HitKind.Panel or HitKind.ResizeGrip) }))
                {
                    SetCursor(LoadCursorW(HINSTANCE.NULL, IDC.IDC_SIZEALL));
                    return true;
                }
                // §534: no resize cursor over a locked bar's grip at rest: it drags nothing.
                if (hit is not { Kind: HitKind.ResizeGrip } || !Arranges)
                {
                    return false;
                }
            }
            SetCursor(LoadCursorW(HINSTANCE.NULL, layout.Vertical ? IDC.IDC_SIZEWE : IDC.IDC_SIZENS));
            return true;
        }

        /// <summary>How far <paramref name="at"/> is from the screen edge the bar stands on, in px.</summary>
        private float Distance(POINT at) => layout.Edge switch
        {
            Edge.Top => at.y - layout.Y,
            Edge.Left => at.x - layout.X,
            Edge.Right => layout.X + layout.Width - at.x,
            _ => layout.Y + layout.Height - at.y,
        };

        private void BeginResize()
        {
            // Measured from where the pointer took hold, so the edge doesn't
            // jump to the pointer on the first move.
            sizing = new Sizing(dock.HeightOf(Id), layout.Thickness - Distance(Cursor()));
            SetCapture(Hwnd);
            SetsCursor();
            // Throttles the live rebuilds, and watches Escape: the dock never
            // has the keyboard focus to hear it.
            Arm(RESIZE, RESIZE_MS);
        }

        private void ResizeMoved()
        {
            if (sizing is null)
            {
                return;
            }
            var px = Distance(Cursor()) + sizing.Offset;
            var dips = (int)MathF.Round(px / (monitor.Dpi / 96f), MidpointRounding.AwayFromZero);
            sizing.Target = Math.Clamp(dips, Config.MinHeight, Config.MaxHeight);
            SetsCursor();
        }

        private void ResizeTick()
        {
            if (sizing is null)
            {
                Kill(RESIZE);
                return;
            }
            if (KeyState.DownNow(VK.VK_ESCAPE))
            {
                EndResize(keep: false);
                return;
            }
            // A rebuild only when the rounded height changed.
            dock.LiveHeight(Id, sizing.Target);
        }

        /// <summary>Losing the mouse to anyone else ends the drag, and the old height comes back.</summary>
        public void CaptureChanged(HWND taker)
        {
            if (taker != Hwnd)
            {
                // quick: a slider whose drag lost the mouse stops where it is (Dock.Quick.cs).
                SliderCaptureLost();
            }
            // edit: the selection rectangle from the empty room lost the mouse: it goes, what it selected stays.
            if (taker != Hwnd && emptyPressed is not null)
            {
                emptyPressed = null;
                dock.BandCancelled();
            }
            if (taker != Hwnd)
            {
                CloseCaptureLost();
                AddCaptureLost();
                // §534: the bar carried, the mouse lost: it stays where it stood.
                CancelBarMove();
            }
            if (taker != Hwnd && (pinDrag is not null || trayGrab is not null))
            {
                // pins: a pin whose drag lost the mouse goes back where it was (Dock.Pins.cs).
                CancelPinDrag();
                // tray: so does a tray icon (Dock.Tray.cs).
                CancelTrayDrag();
                pressed = null;
                scene.Highlight(null, false);
            }
            // Losing the mouse drops a dragged block back where it was. Not a widget following the
            // pointer with no button held: the mouse is taken from it by any app that comes to the
            // front — the add list's going hands the front to one — and it is followed by the drag's
            // tick and put down or called off by the hooks without it (Dock.EditFollow.cs).
            if (taker != Hwnd && dragKey is not null && !dock.Follows(dragKey))
            {
                dragKey = null;
                dragMoved = false;
                Kill(DRAG);
                // edit: and several carried together go back where they stood.
                EndGroup();
                dock.HideCarry();
                scene.Edit(EditView());
            }
            if (sizing is not null && taker != Hwnd)
            {
                EndResize(keep: false);
            }
        }

        private void EndResize(bool keep)
        {
            if (sizing is not { } ended)
            {
                return;
            }
            sizing = null;
            Kill(RESIZE);
            if (GetCapture() == Hwnd)
            {
                ReleaseCapture();
            }
            if (keep)
            {
                dock.LiveHeight(Id, ended.Target);
                dock.KeepHeight(Id, ended.From);
            }
            else
            {
                dock.config.ActiveLayout(Id).Size = ended.From;
                dock.Rebuild();
            }
            RecheckPointer();
        }

        public void RecheckPointer()
        {
            POINT at;
            Check(GetCursorPos(&at));
            if (WindowFromPoint(at) != Hwnd)
            {
                Leave();
            }
        }

        public void Dragged(Drag drag)
        {
            var at = Local(drag.At);
            var (x, y) = (at.X, at.Y);
            // During a drag anything over the window counts as over the dock:
            // people aim at the screen edge, not at the panel. Only the
            // position along the dock matters.
            var p = layout.PanelRect;
            if (layout.Vertical)
            {
                x = p.X + p.Width / 2f;
            }
            else
            {
                y = p.Y + p.Height / 2f;
            }
            switch (drag.Phase)
            {
                case DragPhase.Enter:
                    dragging = true;
                    Moved(x, y);
                    // pins: the line where the pins would land, and the pin or folder they would go to.
                    DropMoved(x, y);
                    break;
                case DragPhase.Over:
                    Moved(x, y);
                    DropMoved(x, y);
                    break;
                case DragPhase.Leave:
                    dragging = false;
                    DropLeft();
                    Leave();
                    break;
                case DragPhase.Drop:
                    dragging = false;
                    DropLeft();
                    // pins: onto a folder, into it; onto an app's button, opened with it (app clicks); else pinned where the line showed (Dock.Pins.cs).
                    if (drag.Targets.Count > 0)
                    {
                        dock.Dropped(this, drag.Targets, new Vector2(x, y));
                    }
                    Leave();
                    break;
            }
        }

        // --- Popups ----------------------------------------------------------

        /// <summary>
        /// The middle of an item's inner side, on screen: where a popup about <em>that item</em>
        /// opens — a pinned app's jump list, a running window's preview, a tray icon's menu. A
        /// widget's own flyout uses <see cref="WidgetAnchor"/> instead.
        /// </summary>
        public POINT Anchor(Hit hit) => InnerSide(layout.ItemRect(WithPart(hit)));

        /// <summary>
        /// How long an item is along the bar, in px: across a horizontal bar, down a vertical one.
        /// With <see cref="Anchor"/> it says where the gap between the item and its popup is.
        /// </summary>
        public int Span(Hit hit)
        {
            var r = layout.ItemRect(WithPart(hit));
            return r.IsEmpty ? 0 : (int)MathF.Round(layout.Vertical ? r.Height : r.Width);
        }

        /// <summary>
        /// Where a widget's own flyout opens: the middle of the inner side of the widget's whole
        /// block (<see cref="Layout.FlyoutRect"/>), so a widget several cells wide opens its panel
        /// under its middle wherever on it the press landed.
        /// </summary>
        public POINT WidgetAnchor(Hit hit) => InnerSide(layout.FlyoutRect(WithPart(hit)));

        /// <summary>
        /// A hit made without a part (<c>Hit.Start</c>, <c>Hit.Tile</c>…) takes the hovered hit's,
        /// where that is the same thing: it is the part the pointer is actually in, and a bar can
        /// hold two widgets of one kind.
        /// </summary>
        private Hit WithPart(Hit hit) =>
            hit.Part < 0 && hovered is { } over && over.Kind == hit.Kind && over.Index == hit.Index ? hit.In(over.Part) : hit;

        /// <summary>The middle of part <paramref name="part"/>'s block's inner side, on screen: where its options open.</summary>
        public POINT BlockAnchor(int part) => InnerSide(layout.BlockRect(part));

        /// <summary>
        /// edit: part <paramref name="part"/>'s block on the screen, which the menu a click on it opens
        /// stands beside (<see cref="MenuSides"/>, §212).
        /// </summary>
        public RECT BlockOnScreen(int part)
        {
            var r = layout.BlockRect(part);
            if (r.IsEmpty)
            {
                var at = PointerAnchor();
                return new RECT { left = at.x, top = at.y, right = at.x + 1, bottom = at.y + 1 };
            }
            return OnScreen(r);
        }

        /// <summary><paramref name="r"/> (the layout's px) on the screen, every pixel it touches: §576 a "+" its add page hangs from.</summary>
        public RECT OnScreen(RectF r) => layout.OnScreen(r);

        /// <summary>
        /// §629: the add page's ghost of part <paramref name="part"/> on this bar as the page lets it show through: the bar's tree,
        /// the ghost's rectangles in it (§621 <see cref="Layout.GhostRects"/>); null for none.
        /// </summary>
        public GhostSight? GhostSightOf(int part) => layout.GhostRects(part) is { Count: > 0 } rects
            ? new GhostSight(scene.Visual, new POINT { x = layout.X, y = layout.Y }, rects, scene.GhostRadius)
            : null;

        /// <summary>The middle of <paramref name="r"/>'s inner side, on screen; the pointer's place for an empty one.</summary>
        private POINT InnerSide(RectF r)
        {
            if (r.IsEmpty)
            {
                return PointerAnchor();
            }
            var (x, y) = layout.Edge switch
            {
                Edge.Top => (r.X + r.Width / 2f, r.Y + r.Height),
                Edge.Left => (r.X + r.Width, r.Y + r.Height / 2f),
                Edge.Right => (r.X, r.Y + r.Height / 2f),
                _ => (r.X + r.Width / 2f, r.Y),
            };
            return new POINT { x = layout.X + (int)x, y = layout.Y + (int)y };
        }

        /// <summary>
        /// §601: this bar's Start button on the screen — the one the pointer is on where it holds two — its panel, and its
        /// edge: what Windows' Start hangs from; null where it has none.
        /// </summary>
        public StartButton? StartButton()
        {
            var button = layout.ItemRect(WithPart(Hit.Start));
            return button.IsEmpty ? null : new Karpo.Dock.StartButton(OnScreen(button), PanelOnScreen(), layout.Edge);
        }

        /// <summary>The bar's panel in screen pixels (pins: what a drag outside it is measured against).</summary>
        public RECT PanelOnScreen() => layout.OnScreen(layout.PanelRect);

        /// <summary>The pointer's place along the dock, on the panel's inner side: where the panel's menu opens.</summary>
        public POINT PointerAnchor() => Anchor(Cursor());

        /// <summary>The point a popup for <paramref name="at"/> (screen px) hangs from: there along the bar, the bar's inner side across it.</summary>
        private POINT Anchor(POINT at)
        {
            var p = layout.PanelRect;
            return layout.Edge switch
            {
                Edge.Top => new POINT { x = at.x, y = layout.Y + (int)(p.Y + p.Height) },
                Edge.Left => new POINT { x = layout.X + (int)(p.X + p.Width), y = at.y },
                Edge.Right => new POINT { x = layout.X + (int)p.X, y = at.y },
                _ => new POINT { x = at.x, y = layout.Y + (int)p.Y },
            };
        }

        /// <summary>
        /// What popups opened from this window stay inside (<see cref="PopupRoom"/>): the display's work
        /// area, less every other bar of the display in sight — one over the windows holds no strip of
        /// the work area, and is no place for a popup. The edit bar a popup
        /// of a top bar keeps clear of by itself (<see cref="FlyoutHost.Avoided"/>).
        /// </summary>
        public RECT WorkArea() => PopupRoom(
            DisplayWork(),
            dock.windows.Where(other => other != this && other.Id == Id && other.Place is null && !other.IsAnchor && other.Shown)
                .Select(other => (other.BarEdge, other.Takes())));

        /// <summary>
        /// folders: <see cref="WorkArea"/> less this bar's own strip too (<see cref="Takes"/>): what a
        /// folder opened from it is placed in, and what of it stands outside its panel keeps inside
        /// while it is arranged — its "+" buttons, its slots and its guide lines, which end at the
        /// bars' edges (the person, 2026-10-22: "the column lines in edit mode still go over the
        /// taskbar, this should not be"; §287). The panel stands off the anchor as before. The same
        /// in edit mode (the person, 2026-10-23: "show full height, in edit mode just display the edit
        /// bar over the folder"; §329): the edit bar stands in front of the folder (FlyoutHost.Stand, §342),
        /// so nothing of the folder moves as edit mode starts or ends.
        /// </summary>
        public RECT FolderWork() => PopupRoom(WorkArea(), [(BarEdge, Takes())]);

        /// <summary>The display's work area now, as Windows has it: what the sections' edit bars keep inside.</summary>
        public RECT DisplayWork()
        {
            var info = new MONITORINFO { cbSize = (uint)sizeof(MONITORINFO) };
            return GetMonitorInfoW((HMONITOR)(void*)monitor.Handle, &info) ? info.rcWork : Rect(monitor.Work);
        }

        public void PreviewEntered()
        {
            PreviewRules.Entered();
            Kill(SETTLE);
        }

        public void PreviewLeft()
        {
            PreviewRules.LeftList();
            if (!hovering)
            {
                leaving = true;
                Arm(SETTLE, HIDE_DELAY_MS);
            }
        }

        public void KillPreviewTimers() => PreviewRules.Stop();

        // --- Timers ----------------------------------------------------------

        public void Timer(uint id)
        {
            switch (id)
            {
                case SETTLE:
                    Settle();
                    break;
                // §701: a morph whose batch never said it ended (the old bar left standing under the new, the whole window
                // taking the pointer) ends now.
                case MORPH_WATCH:
                    Kill(MORPH_WATCH);
                    if (morph is { Running: true } stuck)
                    {
                        Troubles.Write($"Morph: the bar ({Name}) still morphing {MORPH_WATCH_MS} ms on; ended"); // not-ui: karpo.log
                        stuck.End();
                    }
                    else if (morphing)
                    {
                        Troubles.Write($"Morph: the bar ({Name}) held as morphing with none running; let go"); // not-ui: karpo.log
                        Morphed();
                    }
                    break;
                case HOVER_WATCH:
                    HoverWatched();
                    break;
                // peek: the pointer rested on Show desktop (Dock.PeekTablet.cs).
                case DESKTOP_PEEK:
                    DesktopPeekDue();
                    break;
                case RESIZE:
                    ResizeTick();
                    break;
                case DRAG:
                    // The bar may not have the keys (a click elsewhere took
                    // them): Escape still drops a dragged block.
                    if (dragKey is null && !PinDragging && !TrayDragging && !BarMoving)
                    {
                        Kill(DRAG);
                    }
                    else if (KeyState.DownNow(VK.VK_ESCAPE))
                    {
                        // §534: and leaves a bar carried where it stood.
                        CancelBarMove();
                        if (!CancelDrag())
                        {
                            CancelPinDrag();
                            // tray: Escape leaves the icon where it was (Dock.Tray.cs).
                            CancelTrayDrag();
                        }
                    }
                    else
                    {
                        // edit: a widget following the pointer with no button held (Dock.EditFollow.cs).
                        FollowTick();
                        // pins: the hold over a widget may have come of age since the last move.
                        ShowPinDrag();
                        ShowEditDrag();
                        // tray: so may an icon's rest on the chevron, or its time away from the overflow.
                        if (TrayDragging)
                        {
                            dock.TrayDragTick();
                        }
                    }
                    break;
                // app clicks: files rested on an app's button (Dock.AppClicks.cs).
                case DROP_HOVER:
                    DropHoverDue();
                    break;
                // app clicks: what a button said for a moment goes (Dock.AppList.cs).
                case SAID:
                    Unsay();
                    break;
                case CONCEAL:
                    Kill(CONCEAL);
                    if (!Shown)
                    {
                        scene.Conceal();
                    }
                    break;
                case PREVIEW_OPEN:
                    Kill(PREVIEW_OPEN);
                    if (hovering && hovered is { } waiting && !(OwnsOpenPreview && dock.previewHeld))
                    {
                        // quick: hovering a widget opens nothing. The now-playing widget used to open a
                        // strip of its controls here, with the track's name as its heading, which read as a
                        // tooltip that wouldn't go away; its flyout is a click now, as every other widget's is.
                        if (waiting.Kind == HitKind.Tile) dock.OpenPreview(this, waiting.Index);
                    }
                    break;
                // quick: the pointer has rested on a widget long enough for its own flyout.
                case FLYOUT_OPEN:
                    Kill(FLYOUT_OPEN);
                    if (hovering && hovered is { } rested && OpensOnHover(rested))
                    {
                        dock.OpenHoverFlyout(this, rested);
                    }
                    break;
                // quick: the grace after the pointer left one is up.
                case FLYOUT_CLOSE:
                    Kill(FLYOUT_CLOSE);
                    dock.HoverFlyoutTick(this, hovering, hovered);
                    break;
                // preview: the pointer away from the list and its button for the grace closes the
                // list, whatever messages did or did not come (PreviewHover.Watched).
                case PREVIEW_WATCH:
                {
                    Kill(PREVIEW_WATCH);
                    // Another bar's list, or none, ends this one's watch (OwnsOpenPreview).
                    var owns = OwnsOpenPreview;
                    if (PreviewRules.Watched(owns, owns && PointerHome()))
                    {
                        dock.ClosePreview(this);
                        // A bar that never heard the pointer go still thinks it is on the button.
                        if (hovering && !PointerOnBar())
                        {
                            Leave();
                        }
                        else if (!hovering)
                        {
                            leaving = true;
                            Arm(SETTLE, HIDE_DELAY_MS);
                        }
                    }
                    break;
                }
                case PREVIEW_CLOSE:
                {
                    Kill(PREVIEW_CLOSE);
                    // Another window may have taken the list over since, or a click held it (Dock.AppClicks.cs).
                    if (dock.previewOwner != this || dock.previewHeld)
                    {
                        break;
                    }
                    var inside = dock.PreviewInside;
                    var overSame = hovering && hovered is { Kind: HitKind.Tile } over
                        && dock.preview is { IsOpen: true } && dock.preview.Tile == over.Index;
                    if (!PreviewHover.Stays(inside, overSame))
                    {
                        dock.ClosePreview(this);
                        if (!hovering)
                        {
                            leaving = true;
                            Arm(SETTLE, HIDE_DELAY_MS);
                        }
                    }
                    break;
                }
            }
        }

        private void Arm(uint id, uint ms) => SetTimer(Hwnd, id, ms, null);

        private void Kill(uint id) => KillTimer(Hwnd, id);
    }
}

/// <summary>Config as the file has it, for comparing two of them.</summary>
[JsonSerializable(typeof(Config))]
internal sealed partial class DockJson : JsonSerializerContext;
