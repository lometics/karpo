// A quick-settings page as a line of a list folder (docs\widgets.md §88): the network's and
// Bluetooth's, which were the network and Bluetooth buttons' flyouts until they became the Wi-Fi
// and Bluetooth presets (2026-10-10, "port the wifi and bluetooth flyouts to the new system as
// well"); and the mobile hotspot's, its button's flyout until that became the mobile hotspot
// preset the same day (§93). And Windows' Cast panel with Win+P's modes, and nearby sharing, the
// cast and nearby sharing presets' (2026-10-11, §114 and §115).
//
// **The page itself, re-housed.** WifiPage, BluetoothPage and HotspotPage are kept whole — what they
// read, what a click does, a network opened in place onto its key, "Connecting…" at once while Windows joins
// off the dock's thread (OffThreadWifi), a device's connect and remove, the hotspot's switch, its
// password shown and copied, the devices on it asked again every few seconds — and this is the host they
// run in instead of QuickFlyout: an IQuickHost that is a card. It builds the page into a PageLayout
// for a line (PageLayout's lined constructor: the list's width, its rows and words on the list's
// gutter, no footer, no list held to a height) and paints the controls the page placed, where it
// placed them. The title row with the radio's switch is the page's own and stays on it; the footer
// is the actions line under it in the preset.
//
// **As tall as what it shows** (IListContent): the title and every row, a network or a device
// opened in place included. Given less — a page in a folder's column is a fixed height
// (FolderPages.PageCells) — its rows scroll under its title row, which stays (IListScroll, ListScroll.cs).
//
// **Open only while the folder is.** The page is made when the folder shows it (Visible) — its
// sources start then, the Wi-Fi's waiting at most 250 ms for its first read so the folder opens
// with its networks — and disposed when the folder closes, its watchers with it, as the flyout's
// page was. A picture or a test that draws it without opening the folder makes it on the first look.
//
// **The pointer.** A press on a control is the card's (CardPress): the control is pressed, takes
// the keyboard where it takes one, and is clicked when the button comes up on it, as QuickPanel
// does — so a key's field takes a drag to select, and a press there never becomes edit mode's drag.
// A press beside the controls is the folder's, and a click there does nothing. The row under the
// pointer lights, never the line (LightsItsRows).
//
// **The keys.** The folder hands the card the keys while it is the card the keys are on (a press on
// it makes it so): a field being typed in takes every key but Tab and Escape, and what it types
// comes as characters (CardChar, the folder's WM_CHAR). Otherwise Up and Down walk the controls,
// Enter and Space click the one ringed, and past either end the keys are the folder's again.
//
// **Time.** What the page asks to be woken for (a scan that never reports, a battery to ask
// again), a busy ring turning and a caret blinking share one timer of the card's own, armed for
// the soonest; its news reaches the dock's thread the way a source's does (Wake), and nothing of it
// outlives the page.

using System.Numerics;
using Karpo.Core;
using Karpo.Dock.QuickSettings;
using Karpo.Dock.QuickSettings.Controls;
using Karpo.Dock.QuickSettings.Services;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

internal sealed unsafe class QuickPageTile : CellIsland, IListContent, IListScroll, IQuickHost
{
    /// <summary>
    /// Where the pages' data comes from, per thread: a test's own; null for Windows', or the samples
    /// on a run that may not reach the person's world (§11), so no picture joins this PC's Wi-Fi.
    /// </summary>
    [ThreadStatic]
    private static QuickSources? sources;

    internal static QuickSources? Sources
    {
        get => sources;
        set => sources = value;
    }

    /// <summary>What opens a page of Settings, per thread: a test's, which writes it down; null for Windows'.</summary>
    [ThreadStatic]
    private static Action<string>? opener;

    internal static Action<string>? Opener
    {
        get => opener;
        set => opener = value;
    }

    /// <summary>The sources in force on this thread: a test's, the samples, or Windows'. The hotspot face reads its hotspot from them too.</summary>
    internal static QuickSources SourcesNow => Sources ?? (Config.IsIsolated || QuickSources.UseSamples ? QuickSources.Samples : QuickSources.Windows);

    // The timer's news, beside the sources': a bit no page uses.
    private const int Ticked = 1 << 30;

    private readonly QuickKind kind;
    private QuickPage? page;

    // What the page's sources posted (its own bits) and the timer, from any thread.
    private int posted;

    // The one timer, and what it is armed for (Environment.TickCount64; 0 for nothing).
    private Timer? timer;
    private long wakeAt;
    private long blinkAt;
    private float spin;

    // The last layout: for what width and style, the controls placed in the card's own px, and how
    // tall the page came to. Built again when the page asks (Rebuild) or the room changes.
    private bool dirty = true;
    private float laidWidth = -1;
    private TileStyle laidStyle;
    private float laidRoom = float.PositiveInfinity;
    private QuickContext? context;
    private readonly List<Widget> placed = [];
    private float height;
    private float header;

    // The control under the pointer, the one pressed and where the pointer last was, the one with
    // the keyboard, and the one the page asked to have it next.
    private Widget? hot;
    private Widget? held;
    private Vector2 heldAt;
    private Widget? focused;
    private Widget? focusNext;
    private long lastPress;
    private Vector2 lastPressAt;
    private int clicks;

    public QuickPageTile(string key, WidgetItem island, Gfx gfx, Config config)
        : base(key, island, gfx)
    {
        kind = ListWidgets.Find(island.Info?.Id)?.Kind switch
        {
            ListKind.Bluetooth => QuickKind.Bluetooth,
            ListKind.Hotspot => QuickKind.Hotspot,
            ListKind.PowerMode => QuickKind.PowerMode,
            ListKind.WirelessDisplays => QuickKind.Cast,
            ListKind.Projection => QuickKind.Projection,
            ListKind.NearbySharing => QuickKind.Nearby,
            ListKind.Keyboard => QuickKind.Keyboard,
            ListKind.VpnConnections => QuickKind.Vpn,
            _ => QuickKind.Wifi,
        };
        Configure(island, config);
    }

    /// <summary>
    /// What the page's title row does, the way back, where the page stands in a folder's column in place of
    /// its lines (FolderIsland.Pages.cs); null, and no way back, for a page that is a line of its own.
    /// </summary>
    internal Action? Back
    {
        get => back;
        set
        {
            back = value;
            dirty = true;
        }
    }

    private Action? back;

    /// <summary>The way back: the title row, Windows' back arrow at its start (BackHead).</summary>
    private BackHead? backHead;

    /// <summary>Which page it is: the network's, Bluetooth's, the mobile hotspot's, the power mode, cast's, Win+P's, nearby sharing's or the keyboard layouts.</summary>
    internal QuickKind Page => kind;

    /// <summary>Nothing ticks it: its sources post, and its own timer wakes it.</summary>
    public override int IntervalMs => 0;

    protected override int Count => 1;

    public override void Configure(WidgetItem island, Config config) => Settings = island;

    public override string? Tooltip(int index) => null;

    /// <summary>
    /// Never drawn on a bar: a line of a folder's list and nothing else (<see cref="ListWidgets"/>), so
    /// <see cref="CellIsland.PaintOn"/> refuses a bar drawing before this could be reached.
    /// </summary>
    internal override void PaintCell(ID2D1DeviceContext* dc, int index, in IslandStyle style, float width, float height) =>
        throw new NotSupportedException($"{Info.Id} is a line of a folder's list, not a widget of a bar.");

    // --- The page --------------------------------------------------------------------------------

    /// <summary>The folder opening makes the page and starts its sources; closing it ends them (see the top).</summary>
    public override void Visible(bool shown)
    {
        if (shown)
        {
            Ensure();
        }
        else
        {
            Drop();
        }
    }

    /// <summary>The page, made and started the first time anybody looks.</summary>
    private QuickPage Ensure()
    {
        if (page is { } made)
        {
            return made;
        }
        var from = SourcesNow;
        made = kind switch
        {
            QuickKind.Bluetooth => new BluetoothPage(this, from.Bluetooth()),
            QuickKind.Hotspot => new HotspotPage(this, from.Hotspot()),
            QuickKind.PowerMode => new PowerModePage(this, from.Battery()),
            QuickKind.Cast => new CastPage(this, from.Cast(true)),
            QuickKind.Projection => new ProjectPage(this, from.Project()),
            QuickKind.Nearby => new NearbyPage(this, from.Nearby()),
            QuickKind.Keyboard => new KeyboardPage(this, from.Keyboard()),
            QuickKind.Vpn => new ChoicesPage(this, QuickKind.Vpn, QuickSwitch.Vpn, QuickSwitches.For(QuickSwitch.Vpn)),
            _ => new WifiPage(this, from.Wifi(), from.Wired()),
        };
        page = made;
        dirty = true;
        made.Start();
        return made;
    }

    /// <summary>The page ends: its sources stop, its timer goes, and nothing it held is kept.</summary>
    private void Drop()
    {
        timer?.Dispose();
        timer = null;
        wakeAt = blinkAt = 0;
        Interlocked.Exchange(ref posted, 0);
        if (page is { } gone)
        {
            page = null;
            try
            {
                gone.Dispose();
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                Console.Error.WriteLine($"Karpo: the {Info.Id} page would not stop: {error.Message}");
            }
        }
        placed.Clear();
        hot = held = focused = focusNext = null;
        context = null;
        dirty = true;
    }

    public override void Dispose()
    {
        Drop();
        base.Dispose();
    }

    /// <summary>What the sources and the timer posted, on the dock's thread: the page reads again, and the card is drawn again.</summary>
    protected override bool Sample(bool everything)
    {
        Take();
        return false;
    }

    /// <summary>For a test, which has no dock to post to: what the sources and the timer posted, taken now.</summary>
    internal void Pump() => Take();

    private void Take()
    {
        var got = Interlocked.Exchange(ref posted, 0);
        if (page is not { } current)
        {
            return;
        }
        var now = Environment.TickCount64;
        if ((got & ~Ticked) != 0)
        {
            current.Notified(got & ~Ticked);
        }
        if (wakeAt != 0 && now >= wakeAt)
        {
            // What the page asked to be woken for: nothing to read, which it knows the wake by.
            wakeAt = 0;
            current.Notified(0);
        }
        if (context is { } turning && placed.Exists(widget => widget.Visible && widget.Spinning))
        {
            spin = Spinner.Next(spin);
            turning.Spin = spin;
        }
        if (blinkAt != 0 && now >= blinkAt && focused is TextField field)
        {
            _ = field.Blink();
            blinkAt = 0;
        }
        Arm();
    }

    /// <summary>The timer, for the soonest of the page's wake, the next turn of a busy ring and the caret's next blink.</summary>
    private void Arm()
    {
        if (page is null)
        {
            return;
        }
        var now = Environment.TickCount64;
        if (focused is TextField { Focused: true } && blinkAt == 0 && GetCaretBlinkTime() is var blink and > 0 and < INFINITE)
        {
            blinkAt = now + blink;
        }
        var due = long.MaxValue;
        if (wakeAt != 0)
        {
            due = wakeAt;
        }
        if (blinkAt != 0)
        {
            due = Math.Min(due, blinkAt);
        }
        if (placed.Exists(widget => widget.Visible && widget.Spinning))
        {
            due = Math.Min(due, now + Spinner.TickMs);
        }
        if (due == long.MaxValue)
        {
            timer?.Change(Timeout.Infinite, Timeout.Infinite);
            return;
        }
        var wait = (int)Math.Clamp(due - now, 1, 60_000);
        timer ??= new Timer(static state => ((QuickPageTile)state!).Fire(), this, Timeout.Infinite, Timeout.Infinite);
        timer.Change(wait, Timeout.Infinite);
    }

    /// <summary>The timer, on a thread of the pool: the dock's thread takes it (Take).</summary>
    private void Fire()
    {
        try
        {
            Interlocked.Or(ref posted, Ticked);
            _ = Wake();
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            // A thread of the pool lets nothing out (NativeBoundaryTests).
            Console.Error.WriteLine($"Karpo: the {Info.Id} line's timer failed: {error.Message}");
        }
    }

    // --- What the page may ask (IQuickHost) ------------------------------------------------------

    void IQuickHost.Notify(int what)
    {
        // A page's first news, from a source's thread: the dock's thread reads it (Take).
        Interlocked.Or(ref posted, what & ~Ticked);
        _ = Wake();
    }

    void IQuickHost.Rebuild()
    {
        dirty = true;
        _ = Wake();
    }

    /// <summary>The soonest asked for wins: a page that asks on every build must not put its wake off for ever.</summary>
    void IQuickHost.Wake(int milliseconds)
    {
        var at = Environment.TickCount64 + Math.Clamp(milliseconds, 50, 60_000);
        if (wakeAt == 0 || at < wakeAt)
        {
            wakeAt = at;
            Arm();
        }
    }

    /// <summary>Opens Windows' page through the one launcher, which closes the folder first (§491).</summary>
    void IQuickHost.Launch(string uri)
    {
        if (Opener is { } test)
        {
            test(uri);
            return;
        }
        _ = Launch.Open(uri);
    }

    /// <summary>
    /// The page is done (a dialog of Windows' opened over it, a keyboard layout chosen): the folder
    /// closes, as the flyout did — Windows' own cancel (WM_CANCELMODE), posted, so the folder is not
    /// taken apart under the click still running in it.
    /// </summary>
    void IQuickHost.Close() => CloseFolder();

    /// <summary>
    /// A power command asked by a page (none since the power commands are buttons of their own,
    /// PowerButtonTile): the folder closes first and the command runs as a power button's does, on
    /// the pool after it.
    /// </summary>
    void IQuickHost.Power(QuickLink link)
    {
        CloseFolder();
        PowerButtonTile.Run(link);
    }

    /// <summary>Closes the folder the card stands in, whose window has the keys while a click on it runs.</summary>
    private static void CloseFolder()
    {
        var folder = GetActiveWindow();
        if (folder != HWND.NULL)
        {
            _ = PostMessageW(folder, WM.WM_CANCELMODE, default, default);
        }
    }

    void IQuickHost.Focus(Widget widget)
    {
        focusNext = widget;
        dirty = true;
        _ = Wake();
    }

    // --- The layout ------------------------------------------------------------------------------

    /// <summary>The context the controls paint with: the card's own scales, and the folder's theme.</summary>
    private QuickContext ContextFor(in TileStyle style) =>
        new() { Gfx = Gfx, Theme = QuickTheme.For(style.Theme), Scale = style.Scale, TextScale = style.TextScale, Spin = spin };

    /// <summary>
    /// The page laid out for a card <paramref name="width"/> px wide and, for a page that fits itself
    /// to its line (<see cref="QuickPage.Fits"/>), <paramref name="room"/> px tall (infinite: measured
    /// whole): built again where it asked to be or the room changed, else as it was. Returns the
    /// context its controls paint with.
    /// </summary>
    private QuickContext Lay(float width, in TileStyle style, float room)
    {
        var current = Ensure();
        if (!current.Fits)
        {
            room = float.PositiveInfinity;
        }
        if (!dirty && context is { } kept && laidWidth == width && laidStyle == style && laidRoom == room)
        {
            return kept;
        }
        var made = ContextFor(style);
        if (back is not null)
        {
            backHead ??= new BackHead(() => back?.Invoke());
        }
        var layout = PageLayout.ForLine(made, width, ListForm.Gutter(style), back is not null ? backHead : null, style, room);
        current.Build(layout);
        var content = layout.Finish();
        placed.Clear();
        placed.AddRange(content.Header);
        headed = content.Header.Count;
        foreach (var widget in content.Body)
        {
            // The body under the title, in the card's own px.
            var at = widget.Bounds;
            widget.Bounds = new D2D_RECT_F(at.left, at.top + content.HeaderHeight, at.right, at.bottom + content.HeaderHeight);
            placed.Add(widget);
        }
        foreach (var widget in placed)
        {
            // A slider measures its thumb, and a field its caret, against the card's scale.
            widget.Host = made;
        }
        height = MathF.Ceiling(content.HeaderHeight + content.BodyHeight);
        header = content.HeaderHeight;
        context = made;
        laidWidth = width;
        laidStyle = style;
        laidRoom = room;
        dirty = false;
        Settle();
        return made;
    }

    /// <summary>The layout is in: the keyboard goes where the page asked, and a control no longer there lets go.</summary>
    private void Settle()
    {
        if (focusNext is { } next && placed.Contains(next))
        {
            focusNext = null;
            Focus(next, visible: false);
        }
        if (held is not null && !placed.Contains(held))
        {
            held.Pressed = false;
            held = null;
        }
        if (hot is not null && !placed.Contains(hot))
        {
            hot = null;
        }
        if (focused is not null && !placed.Contains(focused))
        {
            Focus(null, visible: false);
        }
        Arm();
    }

    /// <summary>
    /// list: as tall as the page shows itself at <paramref name="width"/> — its title and every row,
    /// whatever is open in it (<see cref="IListContent"/>).
    /// </summary>
    public float ListHeight(float width, in TileStyle style)
    {
        if (Ensure().Fits)
        {
            // §432: a page that fits itself to its line (the power mode) is its one row (Compact) or whole (Full).
            return LineSizes.Of(Settings) == LineSize.Compact ? LineRows.Px(1, style) : Whole(width, style);
        }
        _ = Lay(width, style, float.PositiveInfinity);
        return height;
    }

    /// <summary>
    /// How tall a page that fits itself to its line is whole at <paramref name="width"/>: built into
    /// a layout of its own, so the controls the line placed for the height it has stay placed — the
    /// keys keep their control, as a folder measures its lines whenever it likes. The build moved the
    /// controls, so the next paint, press or key lays them where they were.
    /// </summary>
    private float Whole(float width, in TileStyle style)
    {
        var layout = PageLayout.ForLine(ContextFor(style), width, ListForm.Gutter(style), back is not null ? backHead : null, style);
        Ensure().Build(layout);
        var content = layout.Finish();
        dirty = true;
        return MathF.Ceiling(content.HeaderHeight + content.BodyHeight);
    }

    /// <summary>
    /// list: all of the page, title and rows, where the line is given less (a page standing in a
    /// folder's column is a fixed height, FolderPages): the rows scroll under the title, which stays
    /// (ListScroll.cs). A page that fits itself to its line (<see cref="QuickPage.Fits"/>) never
    /// scrolls: it is laid out for the height given, and is no taller.
    /// </summary>
    public float ScrollHeight(float width, float height, in TileStyle style)
    {
        if (!Ensure().Fits)
        {
            return ListHeight(width, style);
        }
        _ = Lay(width, style, height);
        return MathF.Min(this.height, height);
    }

    /// <summary>The title's row, with the radio's switch and the way back, stays while the rows scroll.</summary>
    public float ScrollHead(float width, float content, in TileStyle style)
    {
        _ = Lay(width, style, content);
        return header;
    }

    /// <summary>What the last layout placed, in the card's own px: a test's view of it.</summary>
    internal IReadOnlyList<Widget> Placed => placed;

    /// <summary>The page, made on the first look: a test's view of it.</summary>
    internal QuickPage Current => Ensure();

    /// <summary>Lays the card out for <paramref name="box"/> as a paint would, without painting: for a test.</summary>
    internal void LayOut(D2D_RECT_F box, in TileStyle style) => _ = Lay(box.right - box.left, style, box.bottom - box.top);

    /// <summary>The control the keyboard is on, for a test.</summary>
    internal Widget? Focused => focused;

    /// <summary>keys: the control its keys are on, by its target (CardTarget: its place in the page from 1).</summary>
    protected internal override int? OwnKeyedTarget => focused is null ? 0 : placed.IndexOf(focused) + 1;

    /// <summary>keys: the control its keys are on wears its own ring (one ring, §471).</summary>
    protected internal override bool RingsOwnKeys => focused is { FocusVisible: true };

    /// <summary>keys: gone to another line, the control they were on is ringed no more (and has the keys no more).</summary>
    protected internal override void OwnKeys(bool on)
    {
        if (!on && focused is not null)
        {
            Focus(null, visible: false);
            Redrawn();
        }
    }

    // --- The drawing -----------------------------------------------------------------------------

    protected internal override void PaintCard(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        Take();
        var paint = Lay(box.right - box.left, style, box.bottom - box.top);
        shownTo = box.bottom - box.top;
        foreach (var widget in placed)
        {
            if (!Shown(widget))
            {
                continue;
            }
            var at = widget.Bounds;
            TilePaint.Placed(dc, box.left + at.left, box.top + at.top, 1, () => widget.Paint(dc, paint));
        }
    }

    // How tall the card was last drawn; a control of the body that does not fit in it is not drawn.
    // The title's row, the first placed (headed), always is: the line's own box holds it at one row.
    private float shownTo = float.MaxValue;
    private int headed;

    /// <summary>
    /// Whether <paramref name="widget"/> is drawn and answers the pointer: visible, and whole inside the
    /// line it was last drawn in, or the title's. A line given fewer rows than the page (no kind has a fewest, §166)
    /// shows the rows that fit and leaves the rest out, never a row cut through its words.
    /// </summary>
    private bool Shown(Widget widget) =>
        widget.Visible && (widget.Bounds.bottom <= shownTo + 0.5f || placed.IndexOf(widget) is >= 0 and var at && at < headed);

    /// <summary>Only a row and the controls light under the pointer, never the line around them.</summary>
    protected internal override bool LightsItsRows => true;

    // --- The pointer -----------------------------------------------------------------------------

    /// <summary>The control under <paramref name="point"/> that answers a pointer, or null.</summary>
    private Widget? Under(Vector2 point)
    {
        for (var index = placed.Count - 1; index >= 0; index--)
        {
            var widget = placed[index];
            if (Shown(widget) && widget.Enabled && widget.HitTestVisible && widget.Contains(Local(widget, point)))
            {
                return widget;
            }
        }
        return null;
    }

    private static Vector2 Local(Widget widget, Vector2 point) => new(point.X - widget.Bounds.left, point.Y - widget.Bounds.top);

    /// <summary>
    /// <paramref name="point"/> in the card's own px, from the box it was read in: the line's, or the
    /// line's moved up by how far its rows are scrolled (ListScroll.cs).
    /// </summary>
    private static Vector2 Into(Vector2 point, D2D_RECT_F box) => new(point.X - box.left, point.Y - box.top);

    public override IslandAction Click(int index) => default;

    /// <summary>The control at <paramref name="point"/>, from 1 by its place in the page; 0 beside them: what is there, as a hit test asks it.</summary>
    protected internal override int CardTarget(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        _ = Lay(box.right - box.left, style, box.bottom - box.top);
        return Under(Into(point, box)) is { } under ? placed.IndexOf(under) + 1 : 0;
    }

    /// <summary>A press on a control is the card's (see the top); beside them it is the folder's.</summary>
    protected internal override bool CardPress(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        _ = Lay(box.right - box.left, style, box.bottom - box.top);
        point = Into(point, box);
        if (Under(point) is not { } under)
        {
            // A press on nothing in particular ends the editing, as in Windows 11.
            Focus(null, visible: false);
            _ = Wake();
            return false;
        }
        // Two presses in Windows' double-click time and room are a double click: a word selected.
        var now = Environment.TickCount64;
        clicks = now - lastPress <= GetDoubleClickTime() && Vector2.Distance(point, lastPressAt) <= GetSystemMetrics(SM.SM_CXDOUBLECLK) ? clicks + 1 : 1;
        lastPress = now;
        lastPressAt = point;
        held = under;
        heldAt = point;
        under.Pressed = true;
        Focus(under.Focusable ? under : null, visible: false);
        _ = under.PointerPressed(Local(under, point), Held(), clicks);
        _ = Wake();
        return true;
    }

    protected internal override void CardDrag(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        if (held is not { } widget)
        {
            return;
        }
        point = Into(point, box);
        heldAt = point;
        widget.Hot = widget.Contains(Local(widget, point));
        widget.PointerMoved(Local(widget, point));
        _ = Wake();
    }

    /// <summary>
    /// The button came up: a click where it came up on the control it went down on. The folder does
    /// not say where it came up, so it is where the pointer was last dragged to.
    /// </summary>
    protected internal override void CardRelease()
    {
        if (held is not { } was)
        {
            return;
        }
        held = null;
        var local = Local(was, heldAt);
        var inside = was.Contains(local);
        was.Pressed = false;
        was.PointerReleased(local, inside);
        if (inside && was.Enabled)
        {
            was.Clicked?.Invoke();
        }
        _ = Wake();
    }

    protected internal override void CardHover(Vector2? point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        _ = Lay(box.right - box.left, style, box.bottom - box.top);
        var under = point is { } at ? Under(Into(at, box)) : null;
        if (point is { } over && under is not null)
        {
            under.PointerMoved(Local(under, Into(over, box)));
        }
        if (under == hot)
        {
            return;
        }
        if (hot is not null)
        {
            hot.Hot = false;
        }
        hot = under;
        if (hot is not null)
        {
            hot.Hot = true;
        }
        _ = Wake();
    }

    /// <summary>
    /// An icon-only button names itself, as the refresh beside the Wi-Fi's switch; so does a segment of its glyph alone,
    /// and the way back's arrow, "Back".
    /// </summary>
    protected internal override (string Text, D2D_RECT_F Part)? CardTip(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        _ = Lay(box.right - box.left, style, box.bottom - box.top);
        var at = Into(point, box);
        var under = Under(at);
        if (under is BackHead head)
        {
            var arrow = head.ArrowAt;
            return head.OnArrow(Local(head, at)) ? (head.Name, PopupPaint.Rect(box.left + arrow.left, box.top + arrow.top, TilePaint.Width(arrow), TilePaint.Height(arrow))) : null;
        }
        return under is (Button { Text.Length: 0, Name.Length: > 0 } or SegmentedItem { Worded: false, Name.Length: > 0 }) and var named
            ? (named.Name, PopupPaint.Rect(box.left + named.Bounds.left, box.top + named.Bounds.top, TilePaint.Width(named.Bounds), TilePaint.Height(named.Bounds)))
            : null;
    }

    // --- The keys --------------------------------------------------------------------------------

    private static Mods Held() =>
        (KeyState.Down(VK.VK_SHIFT) ? QuickSettings.Controls.Mods.Shift : QuickSettings.Controls.Mods.None)
        | (KeyState.Down(VK.VK_CONTROL) ? QuickSettings.Controls.Mods.Control : QuickSettings.Controls.Mods.None);

    /// <summary>Gives <paramref name="widget"/> the keyboard (null: nothing here has it); <paramref name="visible"/> rings it.</summary>
    private void Focus(Widget? widget, bool visible)
    {
        if (widget is not null && (!widget.Enabled || !widget.Visible))
        {
            return;
        }
        if (focused == widget)
        {
            if (widget is not null)
            {
                widget.FocusVisible = visible;
            }
            return;
        }
        if (focused is { } old)
        {
            old.Focused = false;
            old.FocusVisible = false;
            old.FocusChanged();
        }
        focused = widget;
        blinkAt = 0;
        if (widget is not null)
        {
            widget.Focused = true;
            widget.FocusVisible = visible;
            widget.FocusChanged();
        }
        Arm();
    }

    /// <summary>What the keys can reach, in the order the page placed them.</summary>
    private List<Widget> Stops() =>
        [.. placed.Where(widget => widget.Visible && widget.Enabled && (widget.Focusable || widget.Clicked is not null))];

    /// <summary>keys: the line walks its page's controls itself (CellIsland.WalksItsOwnKeys).</summary>
    protected internal override bool WalksItsOwnKeys => true;

    /// <summary>
    /// A key on the card (see the top): true where it took it, and then the folder does nothing with
    /// it. Tab and Escape never come here: they are the folder's whatever has the focus.
    /// </summary>
    protected internal override bool CardKey(int key, WidgetSurface surface)
    {
        if (context is null)
        {
            return false;
        }
        Catch();
        var used = focused is { Enabled: true } widget && widget.Key(key, Held());
        if (!used && focused is TextField && key is not (VK.VK_UP or VK.VK_DOWN))
        {
            // The keys that type come as characters (CardChar): none of them is the folder's.
            used = true;
        }
        if (!used)
        {
            used = Walk(key);
        }
        _ = Wake();
        return used;
    }

    /// <summary>Up and Down walk the controls, Enter and Space click the one ringed; past an end, the folder's.</summary>
    private bool Walk(int key)
    {
        var stops = Stops();
        var at = focused is null ? -1 : stops.IndexOf(focused);
        switch (key)
        {
            case VK.VK_DOWN or VK.VK_UP:
            {
                var next = at < 0 ? (key == VK.VK_DOWN ? 0 : stops.Count - 1) : at + (key == VK.VK_DOWN ? 1 : -1);
                if (next < 0 || next >= stops.Count)
                {
                    Focus(null, visible: false);
                    return false;
                }
                Focus(stops[next], visible: true);
                return true;
            }
            case VK.VK_RETURN or VK.VK_SPACE when focused is { Enabled: true, Clicked: { } click }:
                click();
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// The layout the page asked for since the last paint, laid now: a key typed straight after a
    /// click (Connect, and the key's field asked for) goes to the field the page gave the keyboard,
    /// not to nothing because the folder has not painted in between.
    /// </summary>
    private void Catch()
    {
        if (dirty && context is not null && laidWidth > 0)
        {
            _ = Lay(laidWidth, laidStyle, laidRoom);
        }
    }

    protected internal override bool CardChar(char typed, WidgetSurface surface)
    {
        Catch();
        if (focused is not { Enabled: true } widget || !widget.Character(typed))
        {
            return false;
        }
        _ = Wake();
        return true;
    }
}
