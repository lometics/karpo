// §642: Windows search (beta), a folder's line: a search box over Windows' own index, Start's apps, Settings' pages and
// an answer worked out on the PC, everything found as it is typed (Karpo.Core\WinSearchEngine.cs), listed as Windows 11's
// search lists it — the best match first, then a group each (Apps, Settings, Documents, Folders, Photos, Videos, Music,
// Email, the web's row where it is switched on) — with Karpo's own on top of Windows': chips that filter to one group
// and say which groups found anything, typed prefixes ("documents: tax"), the keys for everything, quick actions on every
// row (Open file location, Copy as path, Run as administrator, Open with) at its end — §659 the best match's too, every
// row of every section drawn alike (RowPaint.Hit, RowActs); the home's tiles have none: theirs are the pane's and the keys'.
//
// §655 (the person, 2026-11-09): before anything is typed, its home — sections the person chooses, orders and counts on
// its page (WinSearchHome: recent searches, Top apps and Pinned as tiles, recent files and folders, events coming up,
// unread mail, notes), each from what Karpo already reads (WinSearchHomes). From 560 DIP wide (Wide) the list stands on
// the left and the chosen result's pane on the right, as Windows 11's search has it — its icon large, its name, what it
// is, what is known of it and its actions, a row each —, at a split its width alone decides (Split), the pane staying
// while the list scrolls (IListScroll.ScrollAside); it shows the row the keys are on, else the one the pointer was last
// on, else the best match (the home's first). Narrower, one list. Either way each row's actions at its end. The way to
// Windows' Searching Windows page is on the widget's page now, no more under the results. Open with is Windows' own
// dialog (Launch.OpenWith).
//
// It is ContactsLine's shape (the box at the top, the rows under it scrolling under it, the keys in the box as the folder
// opens), its rows the folder's own items (ListForm, ItemForm, LineItem). What opens, opens once the folder has gone
// (CellAct.Action, Dock.FolderForward → OpenChosen), through the one launcher (§491); a copy stays in the folder and says
// "Copied". The box and its keys:
//
//   * Typing searches at once; Up and Down walk the rows (the best match is where Enter lands until they do);
//   * Enter opens; Ctrl+Enter opens the file's location; Ctrl+Shift+Enter runs it as administrator; Ctrl+C (with nothing
//     selected in the box) or Ctrl+Shift+C copies the path, an answer's value;
//   * Ctrl+Tab and Ctrl+Shift+Tab (or Ctrl+Right and Ctrl+Left) step through the chips;
//   * Delete on a search made lately forgets it; Escape empties the box, and on an empty box is the folder's (it closes).
//
// §652: what it searches is its sources (WinSearchSystem: Windows' and Karpo's own — the mail, the calendars, the contacts,
// the notes, Karpo itself), each switched on or off under "Search in" on its page (WinSearchOptions); every row wears
// Windows' own icon — a file's and a folder's its own, an app's, Settings' for its pages — read off the dock's thread and
// kept (TargetPictures.Sized, Start's too, at the size each is drawn), its glyph while it comes and where it has none. It stands at one height
// (ListWidgets: fixed), what it finds scrolling under the box.
//
// A ghost on the add page or a preview, made showing the samples (§635: CellIsland.Sampled), asks WinSearchSamples' made-up
// sources: its home the samples' (§655), and what is typed into it (a picture's "team") the samples' answers — no question
// asked of this PC, its files' icons their kinds' (no file read).

using System.Numerics;
using Karpo.Core;
using Karpo.Dock.Pim;
using Karpo.Dock.QuickSettings.Controls;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock.WinSearch;

/// <summary>§642: what a row of Windows search does.</summary>
internal enum WinSearchAct
{
    /// <summary>Opens it: the app, the page, the file, the folder, the mail, the web's page.</summary>
    Open,

    /// <summary>File Explorer at its folder, it selected.</summary>
    Location,

    /// <summary>Its path on the clipboard, in quotes as File Explorer's "Copy as path" puts it; an answer's value bare.</summary>
    Copy,

    /// <summary>Runs it as administrator.</summary>
    Admin,

    /// <summary>Windows' "Open with" for a file.</summary>
    OpenWith,
}

internal sealed unsafe class WinSearchLine : CellIsland, IListContent, IListScroll, ILinePieces
{
    /// <summary>The most characters the box takes.</summary>
    private const int MostTyped = 256;

    /// <summary>How long a row says "Copied", in ms: the clipboard's.</summary>
    internal const int CopiedMs = ClipboardLine.CopiedMs;

    // Segoe Fluent Icons.
    internal const string HistoryGlyph = "\uE81C";
    internal const string LocationGlyph = "\uE838";
    internal const string CopyGlyph = "\uE8C8";
    internal const string AdminGlyph = "\uE7EF";
    internal const string OpenWithGlyph = "\uE7AC";
    internal const string OpenGlyph = "\uE8A7";
    internal const string ForgetGlyph = "\uE711";

    [ThreadStatic]
    private static Func<WinSearchSources>? stand;

    [ThreadStatic]
    private static WinSearchHistory? standHistory;

    [ThreadStatic]
    private static Func<string, float, Action, Pixels?>? standIcons;

    /// <summary>tests: the sources a line made on this thread reads, in place of Windows' (WinSearchSystem); null for Windows'.</summary>
    internal static Func<WinSearchSources>? Stand
    {
        get => stand;
        set => stand = value;
    }

    /// <summary>tests: the history a line made on this thread keeps, in place of this PC's file; null for the file's.</summary>
    internal static WinSearchHistory? HistoryStand
    {
        get => standHistory;
        set => standHistory = value;
    }

    /// <summary>tests: the icons a line made on this thread draws (an icon's key, how many px across it is drawn, who is told once it comes), in place of the shell's; null for the shell's.</summary>
    internal static Func<string, float, Action, Pixels?>? IconStand
    {
        get => standIcons;
        set => standIcons = value;
    }

    private static readonly Lock SharedGate = new();
    private static WinSearchHistory? shared;

    /// <summary>The one history of this process, read from this PC's file once and written back on every change.</summary>
    private static WinSearchHistory Shared()
    {
        lock (SharedGate)
        {
            if (shared is null)
            {
                var path = WinSearchHistory.Path;
                shared = WinSearchHistory.Load(path);
                shared.Changed += () => shared.Save(path);
            }
            return shared;
        }
    }

    private readonly WinSearchEngine engine;
    private readonly WinSearchHistory history;
    private readonly Func<string, float, Action, Pixels?> pictures;
    private TextField box;
    private bool listening;

    // The chip chosen (null: All).
    private WinSearchCategory? chip;

    // The row the keys are on, by what it is, and whether the keys chose it (it wears the ring then).
    private Target? selected;
    private bool keyed;
    private Target? reveal;

    // What is under the pointer, the numbers given out for the folder, and the press held.
    private Target hot;
    private int hotId;
    private readonly List<Target> given = [];
    private RowPress press;

    // What was copied last and until when it says so; what a click left for the dock to open once the folder has gone.
    private (string Key, long Until)? copied;
    private (WinSearchAct Act, WinSearchHit Hit)? opening;

    // The act the keys' Enter holds for the folder's click (Ctrl+Enter: the location; Ctrl+Shift+Enter: as administrator).
    private WinSearchAct? keyAct;

    // The rows the last layout showed, in order: what the arrows walk.
    private readonly List<Target> shown = [];

    // §655: the result the pointer was last on, which a wide line's pane shows while the pointer crosses over to it; how
    // tall the line was last given (what of it shows, its pane's foot).
    private WinSearchHit? paneHover;
    private float tall;

    // The box as the last layout placed it, in the card's own px; whether the pointer holds it, and its presses.
    private D2D_RECT_F boxAt;
    private bool pressing;
    private long lastPress;
    private Vector2 lastPressAt;
    private int clicks;

    public WinSearchLine(string key, WidgetItem island, Gfx gfx, Config config)
        : this(key, island, gfx, config, Stand?.Invoke(), HistoryStand, IconStand)
    {
    }

    /// <summary>
    /// A line on <paramref name="sources"/> — none for this PC's (WinSearchSystem), a test's own —, keeping
    /// <paramref name="history"/> (none for this PC's file), its icons from <paramref name="icons"/> (none for the shell's). A
    /// ghost's (made showing the samples) asks the samples' and keeps a history of their own whatever it is given.
    /// </summary>
    internal WinSearchLine(string key, WidgetItem island, Gfx gfx, Config config, WinSearchSources? sources, WinSearchHistory? history, Func<string, float, Action, Pixels?>? icons)
        : base(key, island, gfx)
    {
        this.config = config;
        this.history = Sampled ? WinSearchSamples.History() : history ?? Shared();
        pictures = icons ?? Picture;
        engine = new WinSearchEngine(
            Sampled ? WinSearchSamples.Sources(config) : sources ?? WinSearchSystem.Sources(() => this.config, text => SearchUrls.Search(this.config, text)),
            this.history);
        box = NewBox();
        Configure(island, config);
    }

    private Config config = new();

    private TextField NewBox() => new(maxLength: MostTyped)
    {
        Name = S.DockWinSearchPlaceholder,
        Glyph = SearchPill.Glyph,
        SearchBox = true,
        Placeholder = S.DockWinSearchPlaceholder,
        TextChanged = Typed,
        Submitted = () => Act(WinSearchAct.Open, Chosen()),
    };

    /// <summary>The caret blinks while the box has the keys; nothing ticks otherwise.</summary>
    public override int IntervalMs => box.Focused ? 500 : 0;

    protected override int Count => 1;

    public override void Configure(WidgetItem island, Config config)
    {
        Settings = island;
        this.config = config;
        Forget();
    }

    /// <summary>Its rows and its box say it all.</summary>
    public override string? Tooltip(int index) => null;

    /// <summary>Never drawn on a bar: on the bar it is its group's search box face.</summary>
    internal override void PaintCell(ID2D1DeviceContext* dc, int index, in IslandStyle style, float width, float height) =>
        throw new NotSupportedException($"{Info.Id} is a line of a folder's list, not a widget of a bar.");

    // --- The showing --------------------------------------------------------------------------------------

    /// <summary>
    /// The folder opened: the box empty with the keys, the engine warmed (the sources made ready, the files used lately, what
    /// the index covers); a ghost's (made showing the samples, §635) asks its samples what they have for their words, and
    /// takes no keys. Closed: everything typed and chosen goes.
    /// </summary>
    public override void Visible(bool shown)
    {
        base.Visible(shown);
        if (shown)
        {
            if (box.Name != S.DockWinSearchPlaceholder)
            {
                box = NewBox();
            }
            Rest();
            if (!listening)
            {
                listening = true;
                engine.Changed += Arrived;
            }
            engine.Warm();
            if (!Sampled)
            {
                Focus(true);
            }
            return;
        }
        if (listening)
        {
            listening = false;
            engine.Changed -= Arrived;
        }
        Focus(false);
        Rest();
        engine.Forget();
    }

    /// <summary>Nothing typed, chosen, copied or under the pointer.</summary>
    private void Rest()
    {
        chip = null;
        box.Text = "";
        selected = null;
        keyed = false;
        reveal = null;
        copied = null;
        hot = default;
        hotId = 0;
        paneHover = null;
        pressing = false;
        _ = press.Up();
        box.Pressed = false;
    }

    public override void Dispose()
    {
        if (listening)
        {
            listening = false;
            engine.Changed -= Arrived;
        }
        engine.Dispose();
        base.Dispose();
    }

    /// <summary>The engine or an icon posted: drawn again. A tick: the caret blinks.</summary>
    protected override bool Sample(bool everything)
    {
        if (!everything && box.Focused)
        {
            _ = box.Blink();
        }
        return false;
    }

    /// <summary>The engine has news, on any thread: the line is drawn again on the dock's.</summary>
    private void Arrived() => _ = Wake();

    /// <summary>What is typed changed (or the chip): asked again at once; nothing chosen.</summary>
    private void Typed()
    {
        selected = null;
        keyed = false;
        paneHover = null;
        engine.Ask(new WinSearchTyped(box.Text, chip, Settings.WinSearch));
        Redrawn();
    }

    /// <summary>tests: the box.</summary>
    internal TextField Box => box;

    /// <summary>tests: the engine.</summary>
    internal WinSearchEngine Engine => engine;

    /// <summary>tests: the chip chosen.</summary>
    internal WinSearchCategory? Chip => chip;

    /// <summary>What is listed now: the engine's last (a ghost's, its samples').</summary>
    internal WinSearchResults Results => engine.Latest;

    /// <summary>What the box says now.</summary>
    private string Typing => box.Text;

    /// <summary>§655: the home (nothing typed): every section as the engine last listed it (a ghost's, the samples').</summary>
    internal WinSearchHome Home => engine.Home;

    // --- Acting ----------------------------------------------------------------------------------------

    /// <summary>tests: the clock "Copied" is timed by, in ms.</summary>
    internal Func<long> Ticks { get; set; } = static () => Environment.TickCount64;

    /// <summary>tests: what "Copied" being over does; the dock's line is drawn again.</summary>
    internal Action<Action, int> After { get; set; } = static (then, ms) =>
        _ = Task.Delay(ms).ContinueWith(
            _ => Troubles.Guard("Windows search: copied said", then), // not-ui: a trouble's name
            CancellationToken.None,
            TaskContinuationOptions.None,
            TaskScheduler.Default);

    [ThreadStatic]
    private static Action<string>? copier;

    /// <summary>tests: what puts a path on the clipboard, per thread; null for Windows' clipboard.</summary>
    internal static Action<string>? Copier
    {
        get => copier;
        set => copier = value;
    }

    /// <summary>Where an act goes once the folder has gone: the one launcher (§491), <see cref="Launches"/>. A test puts its own here.</summary>
    internal Action<WinSearchAct, WinSearchHit> Launched { get; set; } = Launches;

    /// <summary>
    /// <paramref name="act"/> on <paramref name="hit"/> handed to Windows through the one launcher (§491): opened, its place
    /// in File Explorer, run as administrator, or §655 Windows' own Open with dialog for the file (the "openas" verb opened
    /// nothing on Windows 11).
    /// </summary>
    internal static void Launches(WinSearchAct act, WinSearchHit hit)
    {
        switch (act)
        {
            case WinSearchAct.Location:
                Launch.Select(hit.Target);
                break;
            case WinSearchAct.Admin:
                Launch.Elevated(hit.Target);
                break;
            case WinSearchAct.OpenWith:
                Launch.OpenWith(hit.Target);
                break;
            default:
                _ = Launch.Open(hit.Target);
                break;
        }
    }

    /// <summary>tests: what a click left for the dock to open once the folder has gone.</summary>
    internal (WinSearchAct Act, WinSearchHit Hit)? Opening => opening;

    /// <summary>The hit Enter acts on: the row the keys chose, else the best match.</summary>
    private WinSearchHit? Chosen() => selected?.Hit ?? (selected is null && Typing.Length > 0 ? Results.Best : null);

    /// <summary>Whether <paramref name="act"/> can be done to <paramref name="hit"/>.</summary>
    internal static bool Can(WinSearchAct act, WinSearchHit hit) => act switch
    {
        WinSearchAct.Open => hit.Target.Length > 0,
        WinSearchAct.Location => hit.OnDisk,
        WinSearchAct.Copy => hit.Copies is not null,
        WinSearchAct.Admin => hit.Elevates,
        WinSearchAct.OpenWith => hit.OnDisk && hit.Category != WinSearchCategory.Folders,
        _ => false,
    };

    /// <summary>
    /// <paramref name="act"/> on <paramref name="hit"/>: a copy at once, in place, saying "Copied"; an answer's Open is its
    /// copy; anything else left for the dock to do once the folder has gone (<see cref="OpenChosen"/>), the search
    /// remembered. Whether there was anything to do — the folder goes where something opens.
    /// </summary>
    private IslandAction Act(WinSearchAct act, WinSearchHit? hit)
    {
        if (hit is null || !Can(act, hit))
        {
            return default;
        }
        if (act == WinSearchAct.Copy || (act == WinSearchAct.Open && hit.Category == WinSearchCategory.Answer))
        {
            Copy(hit);
            return default;
        }
        opening = (act, hit);
        return new IslandAction(View: IslandView.App);
    }

    /// <summary>
    /// The dock, once the folder has gone: does what the click or the keys left, once, and remembers it. §652 an address of
    /// Karpo's own (a settings page, a widget to add, a layout, a note) is the dock's to open: handed back to it.
    /// </summary>
    internal KarpoTarget? OpenChosen()
    {
        if (opening is not { } chosen)
        {
            return null;
        }
        opening = null;
        if (!Sampled)
        {
            history.Open(chosen.Hit, Typing, Clock.RealUtc());
            // §655: an app opened from here counts with those opened from Start: the home's Top apps.
            if (chosen.Hit.Category == WinSearchCategory.Apps && chosen.Act is WinSearchAct.Open or WinSearchAct.Admin)
            {
                StartWidgets.StartSources.Now.Opened(chosen.Hit.Target);
            }
        }
        if (KarpoTargets.Parse(chosen.Hit.Target) is { } karpo)
        {
            return karpo;
        }
        Launched(chosen.Act, chosen.Hit);
        return null;
    }

    /// <summary>
    /// What <paramref name="hit"/> copies on the clipboard (<see cref="WinSearchHit.Copies"/>: a path in quotes, as "Copy as
    /// path" puts it; an answer's value; a person's address). Says so; the folder stays.
    /// </summary>
    private void Copy(WinSearchHit hit)
    {
        if (hit.Copies is not { } text)
        {
            return;
        }
        if (Copier is { } test)
        {
            test(text);
        }
        else
        {
            Clipboard.SetText(GetActiveWindow(), text);
        }
        copied = (hit.Key, Ticks() + CopiedMs);
        Redrawn();
        After(() => _ = Wake(), CopiedMs + 20);
    }

    /// <summary>Whether <paramref name="hit"/> says "Copied" now.</summary>
    internal bool SaysCopied(WinSearchHit hit) => copied is { } was && string.Equals(was.Key, hit.Key, StringComparison.OrdinalIgnoreCase) && Ticks() < was.Until;

    /// <summary>A chip chosen: the filter, asked again at once.</summary>
    private void Choose(WinSearchCategory? category)
    {
        if (chip == category)
        {
            return;
        }
        chip = category;
        Typed();
    }

    /// <summary>The chips a person steps through: All, then every group but the answer.</summary>
    private static readonly WinSearchCategory?[] Steps = [null, .. WinSearchQuery.Filters.Select(category => (WinSearchCategory?)category)];

    // --- Clicks -----------------------------------------------------------------------------------------

    /// <summary>What a press lands on.</summary>
    internal enum Spot
    {
        None,

        /// <summary>A result's row: opens it.</summary>
        Row,

        /// <summary>A result's button: its act.</summary>
        Button,

        /// <summary>A search made lately: typed again.</summary>
        Search,

        /// <summary>A search made lately's ×: forgotten.</summary>
        Forget,

        /// <summary>A chip: its filter.</summary>
        Chip,

        /// <summary>A group's "Show all": its filter.</summary>
        ShowAll,

        /// <summary>The recent searches' "Clear search history": everything remembered forgotten.</summary>
        Clear,

        /// <summary>A row's whole box: what is scrolled into sight; never hit.</summary>
        Block,
    }

    /// <summary>A thing a click acts on: what it is and what it is about; §659 <paramref name="Pane"/> a button of a wide line's pane, not of its row.</summary>
    internal readonly record struct Target(Spot Spot, WinSearchHit? Hit = null, WinSearchAct Act = WinSearchAct.Open, string? Search = null, WinSearchCategory? Category = null, bool Pane = false);

    public override IslandAction Click(int index)
    {
        if (press.Up())
        {
            _ = Wake();
        }
        // The keys' Enter (Ctrl+Enter, Ctrl+Shift+Enter): the act they held; a click's is Open.
        var act = keyAct ?? WinSearchAct.Open;
        keyAct = null;
        if (index == 0)
        {
            return Act(act, Chosen());
        }
        // The pointer's target, or the row the keys are on (the folder's Enter); nothing else (a folder being arranged).
        if (index > given.Count || (index != hotId && !(keyed && selected is { } chosen && IdOf(chosen) == index)))
        {
            return default;
        }
        var target = given[index - 1];
        keyed = false;
        return target.Spot == Spot.Row ? Act(act, target.Hit) : Do(target);
    }

    /// <summary>What <paramref name="target"/> does, from a click or the keys.</summary>
    private IslandAction Do(Target target)
    {
        switch (target.Spot)
        {
            case Spot.Row:
                return Act(WinSearchAct.Open, target.Hit);
            case Spot.Button:
                return Act(target.Act, target.Hit);
            case Spot.Search when target.Search is { } search:
                Focus(true);
                box.Text = search;
                box.SelectAll();
                return default;
            case Spot.Forget when target.Search is { } search:
                if (!Sampled)
                {
                    history.Forget(search);
                }
                Redrawn();
                return default;
            case Spot.Chip or Spot.ShowAll:
                Choose(target.Category);
                return default;
            case Spot.Clear:
                if (!Sampled)
                {
                    history.Clear();
                }
                Redrawn();
                return default;
            default:
                return default;
        }
    }

    // --- The keys ------------------------------------------------------------------------------------------

    [ThreadStatic]
    private static Mods? held;

    /// <summary>tests: the modifier keys held, per thread, in place of the keyboard's; null for the keyboard's.</summary>
    internal static Mods? Held
    {
        get => held;
        set => held = value;
    }

    private static Mods Mods() => Held ?? ((KeyState.Down(VK.VK_SHIFT) ? QuickSettings.Controls.Mods.Shift : QuickSettings.Controls.Mods.None)
        | (KeyState.Down(VK.VK_CONTROL) ? QuickSettings.Controls.Mods.Control : QuickSettings.Controls.Mods.None));

    protected internal override bool CardTyping => box.Focused;

    protected internal override void CardStopTyping()
    {
        Focus(false);
        _ = Wake();
    }

    private void Focus(bool on)
    {
        if (box.Focused == on)
        {
            return;
        }
        box.Focused = on;
        box.FocusVisible = false;
        box.FocusChanged();
    }

    protected internal override bool WalksItsOwnKeys => true;

    protected internal override int? OwnKeyedTarget => keyed && selected is { } chosen ? IdOf(chosen) : 0;

    protected internal override bool RingsOwnKeys => keyed && selected is not null;

    protected internal override void OwnKeys(bool on)
    {
        if (!on && keyed)
        {
            keyed = false;
            Redrawn();
        }
    }

    /// <summary>A key (see the top of the file).</summary>
    protected internal override bool CardKey(int key, WidgetSurface surface)
    {
        var mods = Mods();
        var control = mods.HasFlag(QuickSettings.Controls.Mods.Control);
        var shift = mods.HasFlag(QuickSettings.Controls.Mods.Shift);
        var rows = shown.Count > 0 ? [.. shown] : Walk();
        var at = selected is { } chosen ? rows.IndexOf(chosen) : -1;
        switch (key)
        {
            case VK.VK_DOWN or VK.VK_UP:
            {
                // Nothing walked yet: Enter is on the best match already, so Down goes to the row after it.
                var first = Typing.Length > 0 && Results.Best is not null ? Math.Min(1, rows.Count - 1) : 0;
                var next = at < 0 ? (key == VK.VK_DOWN ? first : rows.Count - 1) : at + (key == VK.VK_DOWN ? 1 : -1);
                if (rows.Count == 0 || (!box.Focused && (next < 0 || next >= rows.Count)))
                {
                    Pick(null);
                    return box.Focused;
                }
                Pick(rows[Math.Clamp(next, 0, rows.Count - 1)]);
                return true;
            }
            case VK.VK_RETURN:
            {
                if (selected is { Spot: not Spot.Row } other)
                {
                    // A search made lately, a chip: in place.
                    _ = Do(other);
                    return true;
                }
                var act = control ? (shift ? WinSearchAct.Admin : WinSearchAct.Location) : WinSearchAct.Open;
                if (Chosen() is not { } hit || !Can(act, hit))
                {
                    return true;
                }
                if (act == WinSearchAct.Open && hit.Category == WinSearchCategory.Answer)
                {
                    Copy(hit);
                    return true;
                }
                // What leaves the folder is the folder's click on this line (FolderFlyout.Activate → Click), the act held for it.
                keyAct = act;
                return false;
            }
            case 'C' when control && (shift || !box.HasSelection) && Chosen() is { } hit && Can(WinSearchAct.Copy, hit):
                Copy(hit);
                return true;
            case VK.VK_TAB when control:
            case VK.VK_RIGHT or VK.VK_LEFT when control && box.Text.Length > 0:
            {
                var step = Array.IndexOf(Steps, chip);
                var back = key == VK.VK_LEFT || (key == VK.VK_TAB && shift);
                Choose(Steps[(step + (back ? Steps.Length - 1 : 1)) % Steps.Length]);
                return true;
            }
            case VK.VK_DELETE when selected is { Spot: Spot.Search, Search: { } search } && box.Text.Length == 0:
                history.Forget(search);
                Pick(null);
                return true;
            case VK.VK_ESCAPE:
                if (box.Text.Length > 0 || chip is not null)
                {
                    chip = null;
                    box.Text = "";
                    Focus(true);
                    return true;
                }
                return false;
            case VK.VK_TAB:
                Focus(false);
                _ = Wake();
                return false;
        }
        if (!box.Focused)
        {
            return false;
        }
        _ = box.Key(key, mods);
        Redrawn();
        return true;
    }

    /// <summary>A character: the box's; a letter while the line has the folder's keys but not the box gives the box the keys and types it.</summary>
    protected internal override bool CardChar(char c, WidgetSurface surface)
    {
        if (!box.Focused)
        {
            if (char.IsControl(c))
            {
                return false;
            }
            Focus(true);
        }
        _ = box.Character(c);
        Redrawn();
        return true;
    }

    /// <summary>The rows the arrows walk where nothing is laid out yet: the results in order, else the home's sections'.</summary>
    private List<Target> Walk()
    {
        if (Typing.Length > 0)
        {
            return [.. Results.All.Select(hit => new Target(Spot.Row, hit))];
        }
        return [.. Home.For(Settings.WinSearch).SelectMany(section => section.Info.Look == WinSearchHomeLook.Searches
            ? section.Searches.Select(search => new Target(Spot.Search, Search: search))
            : section.Hits.Select(hit => new Target(Spot.Row, hit)))];
    }

    /// <summary>The keys onto <paramref name="row"/> (none: off every row): ringed, brought into sight, §655 the pane showing it.</summary>
    private void Pick(Target? row)
    {
        selected = row;
        keyed = row is not null;
        reveal = row;
        paneHover = null;
        Redrawn();
    }

    // --- The pointer -----------------------------------------------------------------------------------------

    protected internal override bool LightsItsRows => true;

    protected internal override bool CardPress(Vector2 point, D2D_RECT_F card, in TileStyle style, WidgetSurface surface)
    {
        Lay(new LineSketch(null, Gfx), card, style, null);
        var local = InBox(point, card);
        if (!box.Contains(local))
        {
            if (press.Down(IdOf(LineTarget(point, card, style))))
            {
                _ = Wake();
            }
            return false;
        }
        var now = Environment.TickCount64;
        clicks = now - lastPress <= GetDoubleClickTime() && Vector2.Distance(point, lastPressAt) <= GetSystemMetrics(SM.SM_CXDOUBLECLK) ? clicks + 1 : 1;
        lastPress = now;
        lastPressAt = point;
        Focus(true);
        pressing = true;
        box.Pressed = true;
        _ = box.PointerPressed(local, Mods(), clicks);
        _ = Wake();
        return true;
    }

    protected internal override void CardDrag(Vector2 point, D2D_RECT_F card, in TileStyle style, WidgetSurface surface)
    {
        if (pressing)
        {
            box.PointerMoved(InBox(point, card));
            _ = Wake();
        }
    }

    protected internal override void CardRelease()
    {
        if (pressing)
        {
            pressing = false;
            box.Pressed = false;
            box.PointerReleased(default, inside: true);
            _ = Wake();
        }
    }

    private Vector2 InBox(Vector2 point, D2D_RECT_F card) => new(point.X - card.left - boxAt.left, point.Y - card.top - boxAt.top);

    protected internal override void CardHover(Vector2? point, D2D_RECT_F card, in TileStyle style, WidgetSurface surface)
    {
        var under = point is { } at ? LineTarget(at, card, style) : default;
        var overBox = point is { } over && box.Contains(InBox(over, card));
        if (box.Hot != overBox)
        {
            box.Hot = overBox;
            _ = Wake();
        }
        if (point is not null && under.Spot != Spot.None)
        {
            keyed = false;
        }
        // §655: a wide line's pane shows the result the pointer is on, and keeps it as the pointer crosses to the pane.
        if (under is { Spot: Spot.Row or Spot.Button, Hit: { } onto } && !ReferenceEquals(onto, paneHover))
        {
            paneHover = onto;
            _ = Wake();
        }
        var let = press.Over(IdOf(under));
        if (under == hot && !let)
        {
            return;
        }
        hot = under;
        hotId = IdOf(under);
        _ = Wake();
    }

    protected internal override int CardTarget(Vector2 point, D2D_RECT_F card, in TileStyle style, WidgetSurface surface) =>
        IdOf(LineTarget(point, card, style));

    /// <summary>The buttons name themselves, being glyphs alone.</summary>
    protected internal override (string Text, D2D_RECT_F Part)? CardTip(Vector2 point, D2D_RECT_F card, in TileStyle style, WidgetSurface surface)
    {
        var targets = new List<(Target Target, D2D_RECT_F Box)>();
        Lay(new LineSketch(null, Gfx), card, style, targets);
        foreach (var (target, at) in targets)
        {
            if (target.Spot is Spot.Button or Spot.Forget && LineRows.Inside(at, point))
            {
                return (target.Spot == Spot.Forget ? S.DockMenuRemove : ActName(target.Act, target.Hit), at);
            }
        }
        return null;
    }

    /// <summary>What an act's button is called: §655 a copy by what it copies — a path, an answer, anything else's words.</summary>
    internal static string ActName(WinSearchAct act, WinSearchHit? hit = null) => act switch
    {
        WinSearchAct.Location => S.DockWidgetMenuOpenFileLocation,
        WinSearchAct.Copy when hit is { OnDisk: false, Category: WinSearchCategory.Answer } => S.DockWinSearchCopyResult,
        WinSearchAct.Copy when hit is { OnDisk: false } => S.DockMenuCopy,
        WinSearchAct.Copy => S.DockWidgetMenuCopyAsPath,
        WinSearchAct.Admin => S.DockWidgetMenuRunAsAdministrator,
        WinSearchAct.OpenWith => S.DockWinSearchOpenWith,
        _ => S.DockWidgetMenuOpen,
    };

    internal static string ActGlyph(WinSearchAct act) => act switch
    {
        WinSearchAct.Location => LocationGlyph,
        WinSearchAct.Copy => CopyGlyph,
        WinSearchAct.Admin => AdminGlyph,
        WinSearchAct.OpenWith => OpenWithGlyph,
        _ => OpenGlyph,
    };

    private int IdOf(Target target)
    {
        if (target.Spot == Spot.None)
        {
            return 0;
        }
        var at = given.IndexOf(target);
        if (at < 0)
        {
            given.Add(target);
            at = given.Count - 1;
        }
        return at + 1;
    }

    /// <summary>tests: what a number <see cref="CardTarget"/> gave stands for.</summary>
    internal Target TargetOf(int id) => id > 0 && id <= given.Count ? given[id - 1] : default;

    /// <summary>What is at <paramref name="point"/>: a button before the row it stands on.</summary>
    internal Target LineTarget(Vector2 point, D2D_RECT_F card, in TileStyle style)
    {
        var targets = new List<(Target Target, D2D_RECT_F Box)>();
        Lay(new LineSketch(null, Gfx), card, style, targets);
        foreach (var precise in new[] { true, false })
        {
            foreach (var (target, at) in targets)
            {
                if (target.Spot != Spot.Block && (target.Spot is Spot.Button or Spot.Forget or Spot.Chip or Spot.ShowAll or Spot.Clear) == precise && LineRows.Inside(at, point))
                {
                    return target;
                }
            }
        }
        return default;
    }

    // --- Scrolled ---------------------------------------------------------------------------------------------

    public (float Top, float Bottom)? ScrollReveal(float width, in TileStyle style)
    {
        if (reveal is not { } row)
        {
            return null;
        }
        reveal = null;
        var targets = new List<(Target Target, D2D_RECT_F Box)>();
        Lay(new LineSketch(null, Gfx), PopupPaint.Rect(0, 0, width, ListHeight(width, style)), style, targets);
        foreach (var (target, at) in targets)
        {
            if (target.Spot == Spot.Block && Same(target, row))
            {
                var ring = style.Px(4);
                return (at.top - ring, at.bottom + ring);
            }
        }
        return null;
    }

    private static bool Same(Target block, Target row) =>
        (row.Hit is { } hit && ReferenceEquals(block.Hit, hit)) || (row.Search is { } search && block.Search == search);

    // --- Drawn -------------------------------------------------------------------------------------------------

    protected internal override void PaintCard(ID2D1DeviceContext* dc, D2D_RECT_F card, in TileStyle style, WidgetSurface surface)
    {
        Lay(new LineSketch(dc, Gfx), card, style, null);
        reveal = null;
    }

    public string MainPiece => "search";

    public IReadOnlyList<LinePiece> LinePieces(D2D_RECT_F card, in TileStyle style)
    {
        var sketch = new LineSketch(null, Gfx);
        Lay(sketch, card, style, null);
        return sketch.Pieces;
    }

    /// <summary>As tall as everything it lists: the box, the chips, the rows, §655 or a wide line's pane where it is taller.</summary>
    public float ListHeight(float width, in TileStyle style) =>
        Lay(new LineSketch(null, Gfx), PopupPaint.Rect(0, 0, width, 100_000), style, null) - 0;

    /// <summary>Everything under the box scrolls where the line is held shorter than it.</summary>
    public float ScrollHeight(float width, float height, in TileStyle style)
    {
        tall = height;
        var whole = ListHeight(width, style);
        return height + 0.5f < whole && height + 0.5f >= Head(width, style) + LineRows.Px(1, style) ? whole : 0;
    }

    /// <summary>The box and the chips stay while the rows scroll.</summary>
    public float ScrollHead(float width, float content, in TileStyle style) => Head(width, style);

    private float Head(float width, in TileStyle style)
    {
        var sizes = SizesOf(style);
        var chips = ChipBoxes(width, style, sizes);
        return sizes.Form.Pad + sizes.Box + (chips.Count > 0 ? style.Px(8) + chips.Max(each => each.At.bottom) : 0) + style.Px(4);
    }

    /// <summary>
    /// The chips laid out on a line <paramref name="width"/> wide, each box from the line's left and the chips' top: side by side
    /// between the gutters, wrapping onto the next line where the line ends.
    /// </summary>
    private List<(WinSearchCategory? Category, string Name, float Words, D2D_RECT_F At)> ChipBoxes(float width, in TileStyle style, in Sizes sizes)
    {
        List<(WinSearchCategory?, string, float, D2D_RECT_F)> laid = [];
        var form = sizes.Form;
        var x = form.Inset;
        var top = 0f;
        var gap = style.Px(6);
        foreach (var category in Chips())
        {
            var name = category is { } one ? WinSearchWords.Category(one) : WinSearchWords.All;
            var words = PopupPaint.Measure(Gfx, name, form.SmallPx, Weight.Regular).X;
            var across = MathF.Ceiling(words) + (2 * style.Px(10));
            if (x > form.Inset && x + across > width - form.Inset)
            {
                x = form.Inset;
                top += sizes.Chip + gap;
            }
            laid.Add((category, name, words, PopupPaint.Rect(x, top, across, sizes.Chip)));
            x += across + gap;
        }
        return laid;
    }

    /// <summary>The sizes the line is laid out on.</summary>
    private readonly record struct Sizes(ListForm Form, ItemForm Items, float Box, float Pad, float Above, float Side, float Chip, LineButton Button);

    private Sizes SizesOf(in TileStyle style)
    {
        var form = ListForm.Of(Gfx, style);
        var items = ItemForm.Of(Gfx, style, new ItemLook(ItemSize.Standard, Preview: true, Pictures: true, Time: true));
        var button = LineButton.Of(Gfx, style);
        return new Sizes(form, items, MathF.Round(style.Text(32)), items.Space(style, 6), items.Space(style, 2), button.Height, MathF.Round(style.Text(26)), button);
    }

    /// <summary>The chips shown: none with nothing typed; else All, then each group that found something, the one chosen always.</summary>
    internal List<WinSearchCategory?> Chips()
    {
        if (Typing.Length == 0)
        {
            return [];
        }
        var counts = Results.Counts;
        return [null, .. WinSearchQuery.Filters.Where(category => category == chip || counts.ContainsKey(category)).Select(category => (WinSearchCategory?)category)];
    }

    /// <summary>§655: how wide a line is before its chosen result's pane stands beside its list, DIP.</summary>
    internal const float WideDip = 560;

    /// <summary>§655: whether a line <paramref name="width"/> px wide is wide (larger text needing more): its list on the left, the chosen result's pane on the right.</summary>
    internal static bool Wide(float width, in TileStyle style) => width + 0.5f >= style.Px(WideDip) * MathF.Max(1, style.TextScale);

    /// <summary>
    /// §655: where a wide line <paramref name="width"/> px wide splits, px from its left: its list's right and its pane's left —
    /// the pane two fifths of it, 240 to 360 DIP, the gutter's inset at its far side; its width alone decides it, so nothing
    /// in either side moves the other.
    /// </summary>
    internal static (float ListRight, float PaneLeft) Split(float width, in TileStyle style, in ListForm form)
    {
        var pane = MathF.Round(Math.Clamp(width * 0.4f, style.Px(240), style.Px(360)));
        var left = width - form.Inset - pane;
        return (left - style.Px(12), left);
    }

    /// <summary>§655: the pane stays while the list beside it scrolls: from the split's middle to the line's right.</summary>
    public float ScrollAside(float width, in TileStyle style)
    {
        if (!Wide(width, style))
        {
            return 0;
        }
        var (listRight, paneLeft) = Split(width, style, ListForm.Of(Gfx, style));
        return width - PopupPaint.Round((listRight + paneLeft) / 2);
    }

    /// <summary>
    /// The line at whatever height it was given, drawn where the sketch draws (and with <paramref name="targets"/> what a
    /// click lands on): the box; under it the chips; then the home or the results, a row each — §655 a wide line's beside
    /// the chosen one's pane. Its bottom, px.
    /// </summary>
    private float Lay(LineSketch sketch, D2D_RECT_F card, TileStyle style, List<(Target Target, D2D_RECT_F Box)>? targets)
    {
        var sizes = SizesOf(style);
        var form = sizes.Form;
        var theme = style.Theme;
        var width = TilePaint.Width(card);
        var y = card.top + form.Pad;
        var oneRow = card.bottom - card.top + 0.5f < form.Pad + sizes.Box + form.Pad + LineRows.Px(1, style);
        if (oneRow)
        {
            y = PopupPaint.Round(card.top + MathF.Max(0, (card.bottom - card.top - sizes.Box) / 2));
        }

        // The box: across the line between its gutters.
        boxAt = new D2D_RECT_F(form.Inset, y - card.top, width - form.Inset, y - card.top + sizes.Box);
        box.Bounds = boxAt;
        var context = new QuickContext
        {
            Gfx = Gfx,
            Theme = QuickTheme.For(theme),
            Scale = style.Scale,
            TextScale = style.TextScale,
        };
        box.Host = context;
        var typing = Typing;
        sketch.Placed("search", PopupPaint.Rect(card.left + boxAt.left, card.top + boxAt.top, boxAt.right - boxAt.left, sizes.Box), box.Text);
        if (sketch.Device is var dc && dc != null)
        {
            var field = box;
            TilePaint.Placed(dc, card.left + boxAt.left, card.top + boxAt.top, 1, () => field.Paint(dc, context));
        }
        shown.Clear();
        y += sizes.Box;
        if (oneRow)
        {
            return card.top + form.Pad + sizes.Box + form.Pad;
        }

        // The chips: All and each group that found something, the chosen one in the accent, wrapping onto a second line.
        var chips = ChipBoxes(width, style, sizes);
        if (chips.Count > 0)
        {
            y += style.Px(8);
            foreach (var (category, name, words, at) in chips)
            {
                var chipBox = PopupPaint.Rect(card.left + at.left, y + at.top, at.right - at.left, at.bottom - at.top);
                var target = new Target(Spot.Chip, Category: category);
                targets?.Add((target, chipBox));
                var on = chip == category;
                var lit = hot == target;
                var radius = sizes.Chip / 2;
                if (sketch.Draws)
                {
                    if (on)
                    {
                        sketch.Plate($"chip {name} plate", chipBox, radius, TilePaint.Accent(style));
                    }
                    else
                    {
                        sketch.Plate($"chip {name} plate", chipBox, radius, PopupPaint.Ink(lit ? theme.ControlFillHover : theme.ControlFill));
                        PopupPaint.Stroke(sketch.Device, chipBox, radius, MathF.Max(1, style.Px(1)), PopupPaint.Ink(theme.ControlStroke));
                    }
                }
                sketch.Text($"chip {name}", name, form.SmallPx, Weight.Regular, on ? theme.TextOnAccent : theme.Text, chipBox.left + style.Px(10),
                    PopupPaint.Round(chipBox.top + ((sizes.Chip - form.SmallLine) / 2)), words + 1);
            }
            y += chips.Max(each => each.At.bottom);
        }
        y += style.Px(4);
        var head = y;

        // §655: wide, the list on the left of the split and the pane on its right; else the list across the line.
        var wide = Wide(width, style);
        var (listRight, paneLeft) = wide ? Split(width, style, form) : (width, width);
        var list = card with { right = card.left + listRight };
        var row = new RowPaint(this, sketch, targets, sizes, style, context, list, list.left + form.TextLeft, list.right - form.Inset);
        y = typing.Length == 0 ? row.Home(y, Home.For(Settings.WinSearch)) : row.Results(y, Results, typing);
        var bottom = y + form.Pad;
        if (wide)
        {
            // To the foot of what shows of the line, however far its list is scrolled: the pane stays (ScrollAside).
            var foot = this.tall > 0 ? MathF.Min(card.bottom, card.top + this.tall) : card.bottom;
            var pane = new D2D_RECT_F(card.left + paneLeft, head + style.Px(4), card.right - form.Inset, MathF.Max(foot - form.Pad, head + style.Px(4)));
            bottom = MathF.Max(bottom, row.Pane(pane, PaneHit) + form.Pad);
        }
        return bottom;
    }

    /// <summary>
    /// §655: the result the pane shows: the row the keys are on, else the one the pointer was last on (it stays as the
    /// pointer crosses to the pane), else the one the keys chose, else the best match — with nothing typed the home's first.
    /// None that is no longer listed.
    /// </summary>
    internal WinSearchHit? PaneHit
    {
        get
        {
            bool Listed(WinSearchHit? hit) => hit is not null && shown.Any(each => ReferenceEquals(each.Hit, hit));
            if (keyed && Listed(selected?.Hit))
            {
                return selected!.Value.Hit;
            }
            if (Listed(paneHover))
            {
                return paneHover;
            }
            if (Listed(selected?.Hit))
            {
                return selected!.Value.Hit;
            }
            return Typing.Length > 0 ? Results.Best : shown.Select(each => each.Hit).FirstOrDefault(hit => hit is not null);
        }
    }

    /// <summary>
    /// §652: a hit's icon — Windows' own (<see cref="WinSearchHit.Icon"/>) as the shared cache has it (TargetPictures.Sized, at the size drawn:
    /// read off the dock's thread once, kept; the line woken as it comes) —; null while it comes, and for a hit with none: its
    /// glyph meanwhile. A ghost's files and folders wear their kinds' (<see cref="IconOf"/>): no file of this PC is read.
    /// </summary>
    private Pixels? Icon(WinSearchHit hit, float px) => IconOf(hit) is { } key ? pictures(key, px, Arrived) : null;

    /// <summary>
    /// §661: the picture an icon's <paramref name="key"/> asks for, drawn <paramref name="px"/> px across: a person's the contacts
    /// line keeps (ContactPhotos: in memory, nothing fetched for it), anything else the shell's at that size, the one File
    /// Explorer shows (TargetPictures.Sized, Start's too).
    /// </summary>
    internal static Pixels? Picture(string key, float px, Action came) =>
        key.StartsWith(ContactPhotos.Scheme, StringComparison.Ordinal) ? ContactsLine.StoreNow.Photo(key) : TargetPictures.Sized.Get(PinTargets.PictureOf(key), came, px);

    /// <summary>
    /// §652: what <paramref name="hit"/>'s icon is read from: its own (<see cref="WinSearchHit.Icon"/>), but a ghost's file or
    /// folder its kind's (<see cref="WinSearchHit.KindIcon"/>), as a sample is nowhere on a disk.
    /// </summary>
    internal string? IconOf(WinSearchHit hit) => Sampled && hit.OnDisk ? WinSearchHit.KindIcon(hit.Name, hit.Category == WinSearchCategory.Folders) : hit.Icon;

    /// <summary>The line under a hit's name: the answer's question, a file's folder and when it changed, a group's word.</summary>
    private static string DetailOf(WinSearchHit hit, bool best)
    {
        var detail = hit.Detail ?? WinSearchWords.Category(hit.Category);
        if (!best || !hit.OnDisk)
        {
            return detail;
        }
        return Facts(hit) is { Length: > 0 } facts ? facts : detail;
    }

    /// <summary>What a file is, when it changed and how big it is ("Documents · Modified 3.11.2026 · 24 KB"); empty for anything else.</summary>
    private static string Facts(WinSearchHit hit)
    {
        if (!hit.OnDisk)
        {
            return "";
        }
        var parts = new List<string?> { WinSearchWords.Category(hit.Category) };
        if (hit.Modified is { } modified)
        {
            parts.Add(S.DockWinSearchModified(Panels.Nls.ShortDate(DateOnly.FromDateTime(modified.ToLocalTime()))));
        }
        if (hit.Size is { } size && hit.Category != WinSearchCategory.Folders)
        {
            parts.Add(WinSearchRank.SizeText(size));
        }
        return LineRow.Join([.. parts]) ?? "";
    }

    /// <summary>The acts a hit's buttons offer, in the order they stand: Open, then what else it can have done to it.</summary>
    internal static List<WinSearchAct> Actions(WinSearchHit hit) =>
        [.. new[] { WinSearchAct.Open, WinSearchAct.Admin, WinSearchAct.Location, WinSearchAct.Copy, WinSearchAct.OpenWith }
            .Where(act => act != WinSearchAct.Open || hit.Category != WinSearchCategory.Answer)
            .Where(act => Can(act, hit))];

    /// <summary>
    /// §659: the buttons at the end of a row <paramref name="room"/> px wide for its words — every section's row and the best
    /// match alike: its actions but Open (the row itself opens), in <see cref="Actions"/>' order, as many as stand in half
    /// that room, <paramref name="side"/> px each <paramref name="gap"/> apart. None for a row Open is all of (a page, the
    /// web's row, an event).
    /// </summary>
    internal static List<WinSearchAct> RowActs(WinSearchHit hit, float room, float side, float gap) =>
        [.. Actions(hit).Where(act => act != WinSearchAct.Open).Take(Math.Max(1, (int)(((room / 2) + gap) / (side + gap))))];

    /// <summary>One layout's way of laying out the rows: what every row shares, worked out once.</summary>
    private readonly ref struct RowPaint(
        WinSearchLine line,
        LineSketch sketch,
        List<(Target Target, D2D_RECT_F Box)>? targets,
        Sizes sizes,
        TileStyle style,
        QuickContext context,
        D2D_RECT_F card,
        float left,
        float right)
    {
        private readonly ListForm form = sizes.Form;
        private readonly ItemForm look = sizes.Items;
        private readonly FlyoutTheme theme = style.Theme;

        /// <summary>
        /// §655: the home (nothing typed): each section shown, under its heading, its items as rows or as tiles; with none, what
        /// typing does.
        /// </summary>
        public float Home(float y, IReadOnlyList<WinSearchHomeSection> sections)
        {
            if (sections.Count == 0)
            {
                y += style.Px(8);
                var said = sketch.Wrapped("home", S.DockWinSearchHome, form.SmallPx, Weight.Regular, theme.TextSecondary, left, y, MathF.Max(1, right - left), 3);
                return said.bottom + style.Px(4);
            }
            foreach (var section in sections)
            {
                var id = section.Info.Id;
                switch (section.Info.Look)
                {
                    case WinSearchHomeLook.Searches:
                        y = Heading(y, $"heading {id}", section.Info.Title, (S.DockWinSearchClearHistory, null), Spot.Clear);
                        for (var at = 0; at < section.Searches.Count; at++)
                        {
                            y = Search(y, at, section.Searches[at]);
                        }
                        break;
                    case WinSearchHomeLook.Tiles:
                        y = Heading(y, $"heading {id}", section.Info.Title, null);
                        y = Tiles(y, id, section.Hits);
                        break;
                    default:
                        y = Heading(y, $"heading {id}", section.Info.Title, null);
                        for (var at = 0; at < section.Hits.Count; at++)
                        {
                            y = Hit(y, $"{id} {at}", section.Hits[at], best: false);
                        }
                        break;
                }
            }
            return y;
        }

        /// <summary>The results: the best match, the groups; or why there are none.</summary>
        public float Results(float y, WinSearchResults results, string typed)
        {
            if (results.Best is { } best)
            {
                y = Heading(y, "heading best", WinSearchWords.BestMatch, null);
                y = Hit(y, "best", best, best: true);
            }
            foreach (var group in results.Groups)
            {
                var name = WinSearchWords.Category(group.Category);
                var more = results.Ask.Filter is null && group.Found > group.Hits.Count + (ReferenceEquals(results.Best, null) || results.Best.Category != group.Category ? 0 : 1)
                    ? group.Category : (WinSearchCategory?)null;
                y = Heading(y, $"heading {name}", name, more is { } category ? (S.DockWinSearchShowAll(group.Found), category) : null);
                for (var at = 0; at < group.Hits.Count; at++)
                {
                    y = Hit(y, $"{name} {at}", group.Hits[at], best: false);
                }
            }
            // Under them: still asking, why the index could not answer, nothing found.
            var note = results.Problem ?? (results.Searching ? S.DockWinSearchSearching : results.Nothing ? S.DockWinSearchNoMatch(typed) : null);
            if (note is not null)
            {
                y += style.Px(8);
                y = sketch.Wrapped("note", note, form.SmallPx, Weight.Regular, theme.TextSecondary, left, y, MathF.Max(1, right - left), 3).bottom;
            }
            return y;
        }

        /// <summary>A group's heading, and its "Show all" at its end where it found more than it lists (the recent searches' "Clear search history").</summary>
        private float Heading(float y, string name, string text, (string Text, WinSearchCategory? Category)? more, Spot spot = Spot.ShowAll)
        {
            y += style.Px(10);
            var room = right - left;
            if (more is { } all)
            {
                var words = PopupPaint.Measure(line.Gfx, all.Text, form.SmallPx, Weight.Regular).X;
                var at = PopupPaint.Rect(right - style.Px(4) - words, y, words + 1, form.SmallLine);
                var target = new Target(spot, Category: all.Category);
                targets?.Add((target, at));
                var lit = line.hot == target;
                sketch.Text($"{name} all", all.Text, form.SmallPx, Weight.Regular, PopupPaint.Ink(lit ? theme.AccentTextHover : theme.AccentText), at.left, y, words + 1);
                room = at.left - left - style.Px(8);
            }
            sketch.Text(name, text, form.SmallPx, Weight.Semibold, theme.TextSecondary, card.left + form.Inset, y, MathF.Max(1, room + left - card.left - form.Inset));
            return y + form.SmallLine + style.Px(2);
        }

        /// <summary>A search made lately: the clock's glyph, the words; under the pointer or the keys an × that forgets it.</summary>
        private float Search(float y, int at, string search)
        {
            var height = (2 * sizes.Pad) + look.Line;
            var top = y + sizes.Above;
            var rowBox = LineItem.Around(card, top, top + height, sizes.Above);
            var target = new Target(Spot.Search, Search: search);
            targets?.Add((target with { Spot = Spot.Block }, rowBox));
            targets?.Add((target, rowBox));
            line.shown.Add(target);
            if (sketch.Draws && targets is null && line.Hidden(rowBox.top, rowBox.bottom))
            {
                return top + height;
            }
            var hotHere = line.hot.Search == search && line.hot.Spot is Spot.Search or Spot.Forget;
            var ringed = line.keyed && line.selected is { Spot: Spot.Search } chosen && chosen.Search == search;
            if (hotHere || ringed)
            {
                LineItem.Light(sketch, rowBox, theme, line.hot.Spot == Spot.Search && line.press.On(line.hotId));
            }
            Ring(rowBox, ringed);
            var text = top + sizes.Pad;
            sketch.Glyph($"search glyph {at}", HistoryGlyph, look.Side(form.Glyph) * 0.8f, theme.TextSecondary, form.IconBox(card.left, text, look.Line));
            var end = right - style.Px(4);
            if (hotHere || ringed)
            {
                var side = MathF.Min(sizes.Side, height);
                var forget = PopupPaint.Rect(end - side, PopupPaint.Round(top + ((height - side) / 2)), side, side);
                Button($"forget {at}", new Target(Spot.Forget, Search: search), forget, ForgetGlyph);
                end = forget.left - sizes.Button.Gap;
            }
            sketch.Text($"search {at}", search, look.TextPx, Weight.Regular, theme.Text, left, text, MathF.Max(1, end - left));
            return top + height;
        }

        /// <summary>
        /// §655: a home section's apps as tiles, Windows 11's "Top apps": as many across as the list has room for at 88 DIP or
        /// more each (three at the least), each its icon over its name, the row's band lit under the pointer or the keys.
        /// </summary>
        private float Tiles(float y, string id, IReadOnlyList<WinSearchHit> hits)
        {
            var room = card.right - card.left - (2 * form.Inset);
            var across = Math.Max(3, (int)(room / style.Px(88)));
            var icon = MathF.Round(style.Px(32));
            var tall = MathF.Round(style.Px(10) + icon + style.Px(6) + look.SmallLine + style.Px(8));
            var top = y + style.Px(4);
            // §661: the kit's items, edge to edge: the list's whole width in cells as wide as each other, the rows touching.
            var cells = LineItem.Grid(card, top, hits.Count, across, tall);
            for (var at = 0; at < hits.Count; at++)
            {
                var hit = hits[at];
                var tile = cells[at];
                var target = new Target(Spot.Row, hit);
                targets?.Add((target with { Spot = Spot.Block }, tile));
                targets?.Add((target, tile));
                line.shown.Add(target);
                if (sketch.Draws && targets is null && line.Hidden(tile.top, tile.bottom))
                {
                    continue;
                }
                var hotHere = ReferenceEquals(line.hot.Hit, hit) && line.hot.Spot == Spot.Row;
                var ringed = line.keyed && ReferenceEquals(line.selected?.Hit, hit);
                if (hotHere || ringed)
                {
                    LineItem.Light(sketch, tile, theme, hotHere && line.press.On(line.hotId));
                }
                if (ringed && sketch.Device != null)
                {
                    TogglesTile.FocusRing(sketch.Device, LineItem.Inside(tile, MathF.Max(1, style.Px(1))), style.Px(4), style, context.Theme);
                }
                var middle = (tile.left + tile.right) / 2;
                var iconBox = PopupPaint.Rect(PopupPaint.Round(middle - (icon / 2)), tile.top + style.Px(10), icon, icon);
                Wear($"{id} {at}", hit, iconBox, icon * 0.75f, theme.Text);
                var (wordsLeft, wordsRight) = LineItem.Words(tile, card.left, card.right, form.Inset, style.Px(4));
                var words = MathF.Max(1, wordsRight - wordsLeft);
                var said = MathF.Min(words, PopupPaint.Measure(line.Gfx, hit.Name, look.SmallPx, Weight.Regular).X + 1);
                middle = Math.Clamp(middle, wordsLeft + (said / 2), wordsRight - (said / 2));
                sketch.Text($"{id} {at} name", hit.Name, look.SmallPx, Weight.Regular, theme.Text, PopupPaint.Round(middle - (said / 2)), iconBox.bottom + style.Px(6), said);
            }
            var rows = (hits.Count + across - 1) / across;
            return top + (rows * tall);
        }

        /// <summary>
        /// A result, every one alike in every section (§659): its icon, its name, the line under it, and at its end its
        /// actions (<see cref="RowActs"/>) while it is under the pointer or the keys or is where Enter lands — the best match
        /// until the keys move, so its stand there as the results come. The best match taller: a larger icon, its name
        /// strong, what it is and when it changed, its folder; an answer's figure large. Wide, the pane beside the list names
        /// the chosen one's actions again, a row each, as Windows' does.
        /// </summary>
        private float Hit(float y, string name, WinSearchHit hit, bool best)
        {
            var answer = hit.Category == WinSearchCategory.Answer;
            var nameLine = answer && best ? TilePaint.LineHeight(line.Gfx, style.Text(28)) : look.Line;
            var lines = nameLine + look.SmallLine + (best && hit.OnDisk && hit.Detail is not null ? look.SmallLine : 0);
            var height = (2 * sizes.Pad) + lines;
            var top = y + sizes.Above;
            var rowBox = LineItem.Around(card, top, top + height, sizes.Above);
            var target = new Target(Spot.Row, hit);
            targets?.Add((target with { Spot = Spot.Block }, rowBox));
            targets?.Add((target, rowBox));
            line.shown.Add(target);
            if (sketch.Draws && targets is null && line.Hidden(rowBox.top, rowBox.bottom))
            {
                return top + height;
            }
            // The pointer on the row or its buttons — not on the pane's, which stand for the same acts.
            var hotHere = ReferenceEquals(line.hot.Hit, hit) && !line.hot.Pane;
            var ringed = line.keyed && ReferenceEquals(line.selected?.Hit, hit);
            // The best match is where Enter lands until the keys move: lit as chosen, as Windows' is.
            var chosenAtRest = best && line.selected is null && line.Typing.Length > 0;
            if (best && sketch.Draws)
            {
                sketch.Plate($"{name} plate", rowBox, 0, PopupPaint.Ink(theme.ControlFill));
            }
            if ((hotHere && line.hot.Spot == Spot.Row) || ringed || chosenAtRest)
            {
                LineItem.Light(sketch, rowBox, theme, hotHere && line.hot.Spot == Spot.Row && line.press.On(line.hotId));
            }
            Ring(rowBox, ringed);

            var text = top + sizes.Pad;
            var side = best ? MathF.Min(look.Side(LineGlyph.Feature(style)), form.TextLeft - form.Inset - style.Px(LineSpace.GapDip)) : look.Side(form.Glyph);
            var iconBox = ListTiers.GlyphBox(form, card.left, text, MathF.Min(lines, nameLine + look.SmallLine), side);
            Wear(name, hit, iconBox, side * (best ? 0.75f : 1f), answer ? TilePaint.Accent(style) : theme.Text);

            // At its end, in the middle of its words: "Copied" once copied; else its actions while it is the one in hand.
            var end = right - style.Px(4);
            var buttonSide = MathF.Min(sizes.Side, nameLine + look.SmallLine);
            var buttonTop = PopupPaint.Round(text + ((lines - buttonSide) / 2));
            if (line.SaysCopied(hit))
            {
                end = line.CopiedChip(sketch, $"{name} copied", end, buttonTop, buttonSide, form, style).left - style.Px(4);
            }
            else if (hotHere || ringed || chosenAtRest)
            {
                var acts = RowActs(hit, right - left, buttonSide, sizes.Button.Gap);
                for (var at = acts.Count - 1; at >= 0; at--)
                {
                    var box = PopupPaint.Rect(end - buttonSide, buttonTop, buttonSide, buttonSide);
                    Button($"{name} {acts[at]}", new Target(Spot.Button, hit, acts[at]), box, ActGlyph(acts[at]));
                    end = box.left - sizes.Button.Gap;
                }
            }
            var room = MathF.Max(1, end - left - style.Px(4));
            sketch.Text($"{name} name", hit.Name, answer && best ? style.Text(28) : look.TextPx, best ? Weight.Semibold : Weight.Regular, theme.Text, left, text, room);
            var under = text + nameLine;
            sketch.Text($"{name} detail", DetailOf(hit, best), look.SmallPx, Weight.Regular, theme.TextSecondary, left, under, room);
            if (best && hit.OnDisk && hit.Detail is { } folder)
            {
                under += look.SmallLine;
                sketch.Text($"{name} folder", folder, look.SmallPx, Weight.Regular, theme.TextSecondary, left, under, room);
            }
            return top + height;
        }

        /// <summary>
        /// §655: the chosen result's pane beside a wide line's list, as Windows 11's search shows it (the person, 2026-11-09:
        /// "the actions should be shown on the right side if there is enough space. it should be designed in a win11 similar
        /// style."): on a card, its icon large and its name under it, what it is, then what is known of it — a file's folder,
        /// when it changed and how big it is; anything else's line —, then its actions, a row each, its glyph and its name.
        /// Its bottom, px; nothing for no result.
        /// </summary>
        public float Pane(D2D_RECT_F pane, WinSearchHit? hit)
        {
            var radius = style.Px(8);
            sketch.Card("pane", pane, radius, PopupPaint.Ink(theme.ControlFill), PopupPaint.Ink(theme.ControlStroke), MathF.Max(1, style.Px(1)));
            if (hit is null)
            {
                return pane.top;
            }
            var pad = style.Px(16);
            var inner = pane.left + pad;
            var room = MathF.Max(1, pane.right - pad - inner);
            var middle = (pane.left + pane.right) / 2;
            var y = pane.top + style.Px(20);

            // Its icon, large, in the middle; its glyph while it comes.
            var side = MathF.Round(style.Px(48));
            var iconBox = PopupPaint.Rect(PopupPaint.Round(middle - (side / 2)), y, side, side);
            var answer = hit.Category == WinSearchCategory.Answer;
            Wear("pane", hit, iconBox, side * 0.75f, answer ? TilePaint.Accent(style) : theme.Text);
            y = iconBox.bottom + style.Px(12);

            // Its name, strong, in the middle where it fits on a line, else wrapped on two; what it is under it.
            var namePx = style.Text(answer ? 24 : 16);
            y = Centred("pane name", hit.Name, namePx, Weight.Semibold, theme.Text, middle, inner, room, y, 2);
            var kind = hit.Category == WinSearchCategory.Apps ? WinSearchWords.AppDetail : WinSearchWords.Category(hit.Category);
            y = Centred("pane kind", kind, look.SmallPx, Weight.Regular, theme.TextSecondary, middle, inner, room, y + style.Px(2), 1);

            // What is known of it.
            var facts = new List<(string Name, string Text)>();
            if (hit.OnDisk)
            {
                if (hit.Detail is { } folder)
                {
                    facts.Add(("pane folder", folder));
                }
                if (Facts(hit) is { Length: > 0 } said)
                {
                    facts.Add(("pane facts", said));
                }
            }
            else if (hit.Detail is { Length: > 0 } detail && detail != kind)
            {
                facts.Add(("pane detail", detail));
            }
            y += style.Px(12);
            Rule(inner, room, y);
            y += style.Px(12);
            foreach (var (name, said) in facts)
            {
                y = sketch.Wrapped(name, said, look.SmallPx, Weight.Regular, theme.TextSecondary, inner, y, room, 2).bottom + style.Px(4);
            }
            if (facts.Count > 0)
            {
                y += style.Px(8);
                Rule(inner, room, y);
                y += style.Px(8);
            }

            // Its actions, a row each, as Windows' pane lists them.
            var tall = MathF.Round(MathF.Max(style.Px(36), look.Line + style.Px(14)));
            var glyph = MathF.Round(style.Text(16));
            foreach (var act in Actions(hit))
            {
                // §661: a row of the pane is the kit's item, its band the pane's whole width inside its outline, the rows touching.
                var band = LineItem.Band(LineItem.Inside(pane, MathF.Max(1, style.Px(1))), y, y + tall);
                var target = new Target(Spot.Button, hit, act, Pane: true);
                targets?.Add((target, band));
                if (line.hot == target)
                {
                    LineItem.Light(sketch, band, theme, line.press.On(line.hotId));
                }
                var copied = act == WinSearchAct.Copy && line.SaysCopied(hit);
                var textTop = PopupPaint.Round(band.top + ((tall - look.Line) / 2));
                sketch.Glyph($"pane {act} glyph", copied ? ClipboardLine.CopiedGlyph : ActGlyph(act), glyph, copied ? TilePaint.Accent(style) : theme.Text,
                    PopupPaint.Rect(inner, PopupPaint.Round(band.top + ((tall - glyph) / 2)), glyph, glyph));
                sketch.Text($"pane {act}", copied ? S.DockClipboardCopied : ActName(act, hit), look.TextPx, Weight.Regular, theme.Text, inner + glyph + style.Px(12), textTop,
                    MathF.Max(1, band.right - style.Px(10) - (inner + glyph + style.Px(12))));
                y = band.bottom;
            }
            return y + style.Px(8);
        }

        /// <summary>One line of words in the middle of the pane where it fits, else wrapped from its left on up to <paramref name="lines"/>: its bottom.</summary>
        private float Centred(string name, string text, float px, Weight weight, DXGI_RGBA ink, float middle, float inner, float room, float y, int lines)
        {
            var wide = PopupPaint.Measure(line.Gfx, text, px, weight).X + 1;
            if (wide <= room || lines == 1)
            {
                var said = MathF.Min(room, wide);
                var box = sketch.Text(name, text, px, weight, ink, PopupPaint.Round(middle - (said / 2)), y, said);
                return MathF.Max(box.bottom, y + TilePaint.LineHeight(line.Gfx, px));
            }
            return sketch.Wrapped(name, text, px, weight, ink, inner, y, room, lines).bottom;
        }

        /// <summary>
        /// §661: what <paramref name="hit"/> wears in <paramref name="box"/> — the row's, the tile's, the pane's alike —: its icon
        /// read at the size it is drawn (Windows' own, the one File Explorer shows), a person's picture in a circle as the
        /// contacts line draws it; its glyph (<paramref name="glyph"/> px, in <paramref name="ink"/>) while it comes and where
        /// it has none — a person without a picture as in the contacts line.
        /// </summary>
        private void Wear(string name, WinSearchHit hit, D2D_RECT_F box, float glyph, DXGI_RGBA ink)
        {
            if (line.Icon(hit, box.right - box.left) is { } picture)
            {
                var round = line.IconOf(hit)?.StartsWith(ContactPhotos.Scheme, StringComparison.Ordinal) == true;
                sketch.Picture($"{name} {(round ? "photo" : "icon")}", box, (dc, at) =>
                {
                    if (round)
                    {
                        PopupPaint.Covered(dc, picture, at, (at.right - at.left) / 2);
                    }
                    else
                    {
                        PopupPaint.Picture(dc, picture, at, 0);
                    }
                });
                return;
            }
            sketch.Glyph($"{name} glyph", hit.Glyph ?? WinSearchWords.Glyph(hit.Category), glyph, ink, box);
        }

        /// <summary>A hairline across the pane between its parts.</summary>
        private void Rule(float inner, float room, float y)
        {
            if (sketch.Draws)
            {
                PopupPaint.Fill(sketch.Device, new D2D_RECT_F(inner, y, inner + room, y + MathF.Max(1, style.Px(1))), 0, PopupPaint.Ink(theme.ControlStroke));
            }
        }

        /// <summary>The keys' ring round a row they are on.</summary>
        private void Ring(D2D_RECT_F rowBox, bool ringed)
        {
            if (sketch.Draws && ringed && sketch.Device != null)
            {
                TogglesTile.FocusRing(sketch.Device, new D2D_RECT_F(card.left + form.Inset - style.Px(6), rowBox.top, right + style.Px(6), rowBox.bottom), style.Px(4), style, context.Theme);
            }
        }

        /// <summary>A subtle glyph button: lit under the pointer, pressed while held.</summary>
        private void Button(string name, Target target, D2D_RECT_F at, string glyph)
        {
            targets?.Add((target, at));
            var lit = line.hot == target;
            sketch.Button(name, sizes.Button, theme, at, "", glyph, ButtonStyle.Subtle, lit, lit && line.press.On(line.hotId));
        }
    }

    /// <summary>How wide the "Copied" chip is.</summary>
    private float ChipWidth(in ListForm form, in TileStyle style) =>
        style.Px(8) + style.Text(14) + style.Px(6) + PopupPaint.Measure(Gfx, S.DockClipboardCopied, form.TextPx, Weight.Regular).X + style.Px(8);

    /// <summary>The chip a row wears while it says it was copied, as the contacts' and the clipboard's: its box.</summary>
    private D2D_RECT_F CopiedChip(LineSketch sketch, string name, float end, float top, float side, in ListForm form, in TileStyle style)
    {
        var theme = style.Theme;
        var inset = style.Px(8);
        var glyph = style.Text(14);
        var words = PopupPaint.Measure(Gfx, S.DockClipboardCopied, form.TextPx, Weight.Regular);
        var chipBox = new D2D_RECT_F(end - ChipWidth(form, style), top, end, top + side);
        var radius = style.Px(4);
        if (sketch.Draws)
        {
            sketch.Plate($"{name} base", chipBox, radius, PopupPaint.Ink(theme.Base));
            sketch.Plate($"{name} plate", chipBox, radius, PopupPaint.Ink(theme.ControlFill));
            PopupPaint.Stroke(sketch.Device, chipBox, radius, MathF.Max(1, style.Px(1)), PopupPaint.Ink(theme.ControlStroke));
        }
        sketch.Glyph($"{name} check", ClipboardLine.CopiedGlyph, glyph, TilePaint.Accent(style), PopupPaint.Rect(chipBox.left + inset, top + PopupPaint.Round((side - glyph) / 2), glyph, glyph));
        sketch.Text(name, S.DockClipboardCopied, form.TextPx, Weight.Regular, theme.Text, chipBox.left + inset + glyph + style.Px(6), top + PopupPaint.Round((side - form.Line) / 2), words.X + 1);
        return chipBox;
    }
}
