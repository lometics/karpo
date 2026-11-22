// The one-button widgets: airplane mode, the mobile hotspot, energy saver, live
// captions, project, night light, nearby sharing, accessibility, spatial sound,
// the Wi-Fi, Bluetooth, sound and microphone toggles, game mode, color
// filters and mono audio, and keep awake. Each is
// a glyph on the bar (with its name beside it when the person asks for one) that
// looks like Windows 11's quick-settings tiles: the accent behind it while what
// it stands for is on, nothing behind it while it is off, and the disabled ink
// for something this PC hasn't got — its tooltip then says why.
//
// A click switches it where Windows' own tile does (airplane mode, the hotspot,
// night light, energy saver) and otherwise does the one thing Windows offers (a
// shortcut, or its settings page); the tooltip says which. On the bar, project
// and cast open quick-settings pages of their own instead, and have no tooltip
// (docs\widgets.md §46). The hotspot is a folder's line alone: its button on the
// bar is the mobile hotspot preset (§93). Nothing is read on a
// timer: each switch follows the events of what it stands for
// (QuickSettings\Services\Switches.cs).
//
// ONE SWITCH, HOWEVER MANY BUTTONS. The switch is a data source's (SwitchData),
// started at the dock's start for every button of it the config names — on a
// bar, in a folder nobody opened, in a layout no display shows — so a folder that
// opens shows where each switch stands at once. It used to be every button's
// own, started the first time that button was drawn, hovered or clicked: the
// mobile hotspot's start is 8-11 ms of live network-stack queries (60 ms the
// first time in a process), measured, and sixteen hotspot buttons were sixteen of
// them; now they are one, paid once and off the dock's own work (Start does not
// block). A button a test hands a switch of its own (QuickSwitches.Source) still
// starts it the first time it is looked at (Follow). The widgets have no tick:
// IntervalMs is 0, because what they stand for reports its own changes.
//
// Being drawn is not the same as being cheap, so the other half of it is in the
// switches themselves: Start must not block (IQuickSwitch.Start), because for a
// widget that is on the bar the first draw IS the first frame. Until the answer
// arrives the switch says it does not know — On is null, which draws as no state
// at all. A button that is briefly blank, never one that is briefly wrong.
//
// In a list folder a switch is a line of its own at every height it is given, in the lists' one
// design (ListTiers): the glyph, the name and the switch itself on one row — or the state in words,
// where a click opens a page rather than switching anything — and, taller, what it is doing and what
// a click does under the name. The whole line is the button — but on a line with a page, where the
// switch alone switches and the rest opens the page (see "Its page" below). The Wi-Fi and Bluetooth
// lines say what they are on beside the name, on its row and baseline, smaller, in the secondary ink (RadioLine).

using Karpo.Core;
using Karpo.Dock.Native;
using Karpo.Dock.QuickSettings.Services;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using System.Numerics;

namespace Karpo.Dock;

internal sealed unsafe partial class SwitchIsland : CellIsland, IListContent
{
    private readonly IQuickSwitch action;
    private readonly string glyph;
    private readonly DataSources sources;

    // The one source every button of this switch reads (SwitchData), started at the dock's start;
    // null for a button a test has handed a switch of its own (QuickSwitches.Source), which it
    // starts itself the first time it is looked at.
    private readonly SwitchData? data;
    private SwitchOptions options;
    private bool following;

    /// <summary>The switch a list's line shows at its end, where a click switches what it stands for.</summary>
    private readonly QuickSettings.Controls.ToggleSwitch toggle = new() { Name = S.WidgetSwitchToggle };

    /// <summary>What the Wi-Fi and Bluetooth lines say beside their names, followed while shown in a folder (RadioLine); null for the rest.</summary>
    private readonly RadioLine? radioLine;

    public SwitchIsland(string key, WidgetItem island, Gfx gfx, Config config)
        : this(key, island, gfx, config, DataSources.Shared)
    {
    }

    internal SwitchIsland(string key, WidgetItem island, Gfx gfx, Config config, DataSources sources)
        : base(key, island, gfx)
    {
        options = island.Switch ?? new SwitchOptions();
        glyph = Info.Glyph;
        this.sources = sources;
        var which = Info.Switch
            ?? throw new ArgumentOutOfRangeException(nameof(island), Info.Id, "is not a one-button widget.");
        radioLine = which switch
        {
            QuickSwitch.WifiRadio => new RadioLine(RadioLineKind.Wifi),
            QuickSwitch.BluetoothRadio => new RadioLine(RadioLineKind.Bluetooth),
            _ => null,
        };
        var held = QuickSwitches.Source is null ? sources.Hold(island) : null;
        if (held is SwitchData shared)
        {
            data = shared;
            action = shared.Switch;
            // Followed since the source started: what the switch knows is there to read at once.
            following = true;
            shared.Listen(Post);
        }
        else
        {
            sources.Release(held);
            action = QuickSwitches.For(which);
        }
        pending = Pendings.Of(action);
        pending.Listen(Post);
        clock = new PendingClock(this);
    }

    /// <summary>The switch's click on its way, shared with every button of it (SwitchPending).</summary>
    private readonly SwitchPending pending;

    private readonly PendingClock clock;

    /// <summary>
    /// Where the switch is drawn now: where a click sent it while that is on its way and shows, else
    /// where it stands; and whether it is on its way (<paramref name="busy"/>). Settles the click first.
    /// </summary>
    private bool? Showing(out bool busy)
    {
        var now = SwitchPending.Now;
        pending.Settle(action.On, action.Refusals, now);
        busy = pending.Shows(now);
        return pending.Shown(action.On, now);
    }

    /// <summary>Whether a click on it is on its way and shows: for the tests.</summary>
    internal bool Pending
    {
        get
        {
            _ = Showing(out var busy);
            return busy;
        }
    }

    /// <summary>
    /// Starts following what this stands for, the first time anybody looks at a button with a
    /// switch of its own (a test's) — drawn on the bar, drawn as a folder's card, hovered for its
    /// tooltip or clicked. The dock's buttons read the one switch their source has followed since
    /// the dock started (SwitchData), and this does nothing for them.
    /// <para>
    /// Called on the dock's own thread, so what it starts must not block — see IQuickSwitch.Start.
    /// </para>
    /// </summary>
    private void Follow()
    {
        if (following)
        {
            return;
        }
        following = true;
        action.Start(Post);
    }

    /// <summary>Nothing ticks: what it stands for reports its own changes.</summary>
    public override int IntervalMs => 0;

    /// <summary>One cell, also where this PC hasn't got what it stands for: greyed then (<see cref="UnavailableOn"/>).</summary>
    protected override int Count => 1;

    /// <summary>Where this PC hasn't got it: drawn off, greyed, and the tooltip saying so and why.</summary>
    internal override string? UnavailableOn(nint screen) =>
        action.Available ? null : Say(action.Name, available: false, on: null, action.Note);

    /// <summary>What it stands for: on, off, or null when Windows tells no app.</summary>
    internal bool? On => action.On;

    internal bool Available => action.Available;

    /// <summary>The glyph it shows now: the switch's where that follows its state (the speaker struck through while muted), else its own.</summary>
    internal string Shown => action.Glyph ?? glyph;

    public override void Configure(WidgetItem island, Config config)
    {
        // The shape is a function of the settings, so the shape must be given the
        // settings just handed over: keeping the ones this island was born with
        // left the size measured for the old options while the drawing used the
        // new ones (a folder whose name was switched on kept its square).
        Settings = island;
        options = island.Switch ?? new SwitchOptions();
    }

    public override string? Tooltip(int index)
    {
        Follow();
        var on = Showing(out var busy);
        return Say(action.Name, action.Available, on, action.Note, action.Detail, busy);
    }

    /// <summary>
    /// What the tooltip says: where it stands, and the line under it. A state Windows tells no app
    /// is left out rather than made up; one that is on as something says what (<paramref name="detail"/>:
    /// the VPN it is connected to); one on its way (<paramref name="busy"/>, <paramref name="on"/>
    /// where it is going) says "turning on…". Pure: the tests check the words.
    /// </summary>
    internal static string Say(string name, bool available, bool? on, string? note, string? detail = null, bool busy = false)
    {
        var state = !available
            ? S.WidgetSwitchUnavailableTip(name)
            : busy && on is { } going
            ? going ? S.WidgetSwitchTurningOnTip(name) : S.WidgetSwitchTurningOffTip(name)
            : on switch
            {
                true => detail is { Length: > 0 } what ? S.WidgetSwitchDetailTip(name: name, detail: what) : S.WidgetSwitchOnTip(name),
                false => S.WidgetSwitchOffTip(name),
                _ => name,
            };
        return note is { Length: > 0 } line ? $"{state}\n{line}" : state;
    }

    /// <summary>
    /// The words under a list line's name: where it stands, or nothing where Windows tells no app —
    /// a state is not made up. On as something, it says what (<paramref name="detail"/>). Where this
    /// PC hasn't got it, the line says "Off", greyed (<see cref="UnavailableOn"/>).
    /// </summary>
    internal static string State(bool? on, string? detail = null) => on switch
    {
        true => detail is { Length: > 0 } what ? what : S.WidgetSwitchOn,
        false => S.WidgetSwitchOff,
        _ => "",
    };

    /// <summary>
    /// Switches it, or does what Windows offers instead; the island handles the click itself, on the
    /// bar and in a folder alike. None opens a popup of Karpo's: the second screen's page and VPN's
    /// connections were the last, and are their presets' lines since 2026-10-11, as the mobile
    /// hotspot's page is since 2026-10-10. The switches flip a radio, start captions or open a page
    /// of Settings.
    /// </summary>
    public override IslandAction Click(int index)
    {
        Follow();
        press.Up();
        // A line with a page (FolderPages): all of it but the switch opens the page in the folder, the
        // ">" its sign; a line whose click switches nothing (accessibility) is all page.
        if (PageShown && (index == PageTarget || !action.Switches))
        {
            return new IslandAction(View: IslandView.Page);
        }
        if (action.Available)
        {
            // With the widget's options: spatial sound's format, color filters' filter. A click while
            // the last is on its way is ignored (SwitchPending).
            var with = options;
            Pendings.Act(action, () => action.Act(with));
            Post();
        }
        return default;
    }

    /// <summary>A pending click settles as the switch says it moved; its clock wakes the button when it shows, and when it times out.</summary>
    protected override bool Sample(bool everything)
    {
        var now = SwitchPending.Now;
        pending.Settle(action.On, action.Refusals, now);
        clock.Arm(pending.Due(now), now);
        return false;
    }

    /// <summary>
    /// A switch of its own hears the dock's broadcasts through its button; the shared one hears them
    /// once, from the dock (DataSources.System), however many buttons of it there are.
    /// </summary>
    public override void System(uint msg, WPARAM wParam, LPARAM lParam)
    {
        if (data is null)
        {
            action.Heard(msg, (nuint)wParam);
        }
    }

    public override void Dispose()
    {
        pending.Unlisten(Post);
        clock.Dispose();
        if (data is not null)
        {
            data.Unlisten(Post);
            sources.Release(data);
        }
        else
        {
            action.Dispose();
        }
        radioLine?.Dispose();
        base.Dispose();
    }

    // stage 3: the size is WidgetShapes.Switch. The label is the kind's own title from its
    // descriptor, not the name the switch service happens to give it — the same string, except
    // where that service's default arm hands a kind somebody else's.

    /// <summary>The side of the square the glyph sits in, which the button's length is measured with (WidgetShapes.Switch).</summary>
    internal static float GlyphSide(in Fit fit, float glyph) =>
        MathF.Max(glyph, MathF.Min(glyph + (2 * fit.Px(6)), fit.Cell - (2 * fit.Px(2))));

    internal override void PaintCell(ID2D1DeviceContext* dc, int index, in IslandStyle style, float width, float height)
    {
        Follow();
        var ink = new IslandInk(style.Look);
        var shown = Showing(out var busy);
        var on = action.Available && shown == true;
        // Where this PC hasn't got it, drawn off and greyed by the bar (UnavailableOn). Windows 11's own tile: the accent
        // while it is on — and from the moment a click sends it on, as the tile does, quietened until Windows has done it.
        var quiet = busy && action.Available;
        DXGI_RGBA? fill = on ? Quieted(ink.Accent, quiet) : null;
        var color = Quieted(on ? OnAccent(style.Look) : ink.Text, quiet);
        PaintButton(dc, Gfx, style, width, height, options.ShowLabel, Shown, color, action.Name, on ? color : ink.Text, fill);
    }

    private static DXGI_RGBA Quieted(DXGI_RGBA color, bool quiet) => quiet ? QuickSettings.Controls.QuickTheme.WithAlpha(color, BusyAlpha) : color;

    /// <summary>
    /// A one-button widget on the bar — a switch's, the security's: its glyph in its square (<see cref="GlyphSide"/>), in the
    /// middle of the cell or, <paramref name="named"/>, at its start with its name after it. §648: lit (<paramref name="fill"/>)
    /// it fills its whole cell — which is its card, its plate at rest and its highlight (Layout.Plates) — edge to edge in the
    /// plate's corners (<see cref="IslandStyle.PlateRadius"/>), as Windows 11's quick-settings tile is the accent to its edges;
    /// it was a square round the glyph, a ring of the plate showing between it and the button's edge.
    /// </summary>
    internal static void PaintButton(ID2D1DeviceContext* dc, Gfx gfx, in IslandStyle style, float width, float height, bool named, string glyph, DXGI_RGBA glyphInk, string name, DXGI_RGBA nameInk, DXGI_RGBA? fill)
    {
        var fit = new Fit(style);
        var size = fit.Glyph();
        var side = GlyphSide(fit, size);
        var alone = style.Vertical || !named;
        var box = alone
            ? PopupPaint.Rect((width - side) / 2, (height - side) / 2, side, side)
            : PopupPaint.Rect(fit.Edge, (height - side) / 2, side, side);
        if (fill is { } lit)
        {
            PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, width, height), style.PlateRadius, lit);
        }
        dc->SetTextAntialiasMode(D2D1_TEXT_ANTIALIAS_MODE.D2D1_TEXT_ANTIALIAS_MODE_GRAYSCALE);
        PopupPaint.Glyph(dc, gfx, glyph, size, glyphInk, box);
        if (alone)
        {
            return;
        }
        var text = TextLine.Lay(gfx, name, fit.Primary, Weight.Regular, MathF.Max(1, width - box.right - fit.Gap - fit.Edge));
        try
        {
            ID2D1SolidColorBrush* brush = null;
            try
            {
                brush = Gfx.Brush(dc, nameInk);
                text.Draw(dc, brush, nameInk, box.right + fit.Gap, (height - text.Height) / 2);
            }
            finally
            {
                ComUtil.Release(ref brush);
            }
        }
        finally
        {
            text.Release();
        }
    }

    /// <summary>How much of its ink a button on its way keeps (SwitchPending): there, and plainly not done.</summary>
    internal const float BusyAlpha = 0.6f;

    /// <summary>Text on the accent: black on the light accent of dark mode, white on the dark one of light mode.</summary>
    internal static DXGI_RGBA OnAccent(in Look look) =>
        look.HighContrast
            ? FlyoutTheme.Ink(look.HcHighlightText)
            : look.Dark ? new DXGI_RGBA(0, 0, 0, 1) : new DXGI_RGBA(1, 1, 1, 1);

    // --- In a folder ------------------------------------------------------------

    /// <summary>The switch's own line (<see cref="PaintLine"/>).</summary>
    protected internal override unsafe void PaintCard(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        Follow();
        PaintLine(dc, box, style);
    }

    /// <summary>What a line too narrow for the whole title calls it, where there is a shorter word for it.</summary>
    private string? ShortName => Info.Switch switch
    {
        QuickSwitch.AirplaneMode => S.WidgetSwitchAirplaneShort,
        QuickSwitch.MobileHotspot => S.WidgetSwitchHotspotShort,
        QuickSwitch.LiveCaptions => S.WidgetSwitchCaptionsShort,
        QuickSwitch.NearbyShare => S.WidgetSwitchNearbyShort,
        QuickSwitch.RotationLock => S.WidgetSwitchRotationShort,
        _ => null,
    };

    /// <summary>
    /// The whole line is the button; with a page, the switch is the switch's (<see cref="ToggleTarget"/>)
    /// and all the rest the page's (<see cref="PageTarget"/>).
    /// </summary>
    protected internal override int CardTarget(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface) =>
        !Split ? 0 : LineRows.Inside(ToggleHit(box, style), point) ? ToggleTarget : PageTarget;

    // --- Its page ----------------------------------------------------------------------------------
    // Windows' tiles for Wi-Fi, Bluetooth, the VPN, nearby sharing, the hotspot and accessibility have
    // a ">" that opens a page (Karpo.Core\FolderPages.cs). A switch's line in an open folder has it
    // too, just before the switch's column, or on the gutter where it draws none (the trailing columns, below). The switch is switched where it is clicked and nowhere else; the rest of the
    // line, the ">" with it, opens the page in the folder's column (2026-10-16, the person: "the
    // toggle button should only be clicked when you click it directly, everywhere else it opens the
    // sub flyout"). A switch whose click switches nothing (accessibility) is one target, the page,
    // its ">" the sign of it. A line with a switch and no page stays one target that switches, as
    // Windows' tile without a ">" is: there is nothing else for a click there to do. Not on the bar,
    // and not on a line that is a page's own.

    /// <summary>The switch's target, on a line of two.</summary>
    internal const int ToggleTarget = 1;

    /// <summary>The page's target: the line but its switch.</summary>
    internal const int PageTarget = 2;

    /// <summary>The ">"'s glyph, in DIPs; the air before it, what the line's words keep from it; and the air after it, before the switch's column.</summary>
    internal const float ChevronGlyphDip = 12;

    internal const float ChevronAirDip = 14;

    internal const float ChevronToSwitchDip = 12;

    /// <summary>The switch's track, in DIPs: WinUI's ToggleSwitch's 40 by 20.</summary>
    internal const float ToggleWidthDip = 40;

    internal const float ToggleHeightDip = 20;

    /// <summary>Segoe Fluent Icons' ChevronRight.</summary>
    internal const string ChevronGlyph = "\uE76C";

    // The trailing columns (2026-10-16, the person: "the toggles should actually always align to the
    // right. then the angled buttons are directly do the left of them"): every line's switch ends on
    // the trailing gutter, the same x on every line, page or none, and a line with a page and a switch
    // drawn has its ">" just before the switch's column. A line that draws no switch (accessibility's,
    // Cast's, a switch whose state is not known yet or that this PC hasn't got, the volume's) has its
    // ">" on the trailing gutter itself, no empty column after it (2026-10-16, the person: "the angled
    // icons should not have a blank space on the right of them if they dont have a toggle button to
    // the right"); the ">"s of the two kinds of line stand in two columns.

    /// <summary>The switch's column on a line <paramref name="box"/>: its width, ending on the trailing gutter, the line's height.</summary>
    internal static D2D_RECT_F SwitchColumn(D2D_RECT_F box, in TileStyle style)
    {
        var width = style.Px(ToggleWidthDip);
        return PopupPaint.Rect(box.right - ListForm.Gutter(style) - width, box.top, width, box.bottom - box.top);
    }

    /// <summary>
    /// The ">"'s glyph's box on a line <paramref name="box"/>: its size, the line's height; just before
    /// the switch's column where the line draws a switch (<paramref name="switched"/>), else ending on
    /// the trailing gutter.
    /// </summary>
    internal static D2D_RECT_F ChevronGlyphBox(D2D_RECT_F box, in TileStyle style, bool switched)
    {
        var size = style.Px(ChevronGlyphDip);
        var right = switched ? SwitchColumn(box, style).left - style.Px(ChevronToSwitchDip) : box.right - ListForm.Gutter(style);
        return PopupPaint.Rect(right - size, box.top, size, box.bottom - box.top);
    }

    /// <summary>
    /// The band of <paramref name="box"/> the ">" stands in the middle of: the switch's row where
    /// <paramref name="line"/> draws one, else its words, from the name to the last line under it — so
    /// on a line taller than its words (§192: no most of its own) the ">" stays beside them, at the
    /// top, and not alone in the middle of the line.
    /// </summary>
    private static D2D_RECT_F ChevronBand(D2D_RECT_F box, in SwitchLine line)
    {
        if (line.Switch.right > line.Switch.left)
        {
            return box with { top = line.Switch.top, bottom = line.Switch.bottom };
        }
        var bottom = MathF.Max(line.Name.bottom, line.State.bottom);
        foreach (var (under, _, _) in line.Under)
        {
            bottom = MathF.Max(bottom, under.bottom);
        }
        return bottom > line.Name.top ? box with { top = line.Name.top, bottom = bottom } : box;
    }

    /// <summary>
    /// Whether the line draws its switch: a click switches it and its state is known — or what it stands
    /// for is not here, when it is drawn off, greyed (<see cref="UnavailableOn"/>).
    /// </summary>
    internal bool DrawsSwitch => action.Switches && (!action.Available || Showing(out _) is not null);

    /// <summary>Whether the line has a page to open: in an open folder, not on a page itself.</summary>
    internal bool PageShown =>
        Container is FolderIsland && !Key.Contains("/page/", StringComparison.OrdinalIgnoreCase)
        && ListWidgets.Find(Info.Id)?.Kind is { } kind && FolderPages.Has(kind);

    /// <summary>Whether the line is two targets, the switch and the page's ">".</summary>
    private bool Split => PageShown && action.Switches;

    /// <summary>
    /// Where the line's words end on a line with a page: the air before its ">". The switch and the
    /// ">" stand in the room after it; the name, the radio's words and the state in words before it.
    /// </summary>
    internal static float WordsEnd(D2D_RECT_F box, in TileStyle style, bool switched) =>
        ChevronGlyphBox(box, style, switched).left - style.Px(ChevronAirDip);

    /// <summary>
    /// Where a press is the switch's on a line of two: the switch drawn, and a little air round it, as
    /// WinUI's ToggleSwitch takes the pointer a few px past its track. Empty where no switch is drawn.
    /// </summary>
    internal D2D_RECT_F ToggleHit(D2D_RECT_F box, in TileStyle style)
    {
        var drawn = LineOf(box, style).Switch;
        if (TilePaint.Width(drawn) <= 0)
        {
            return default;
        }
        var air = style.Px(4);
        return new D2D_RECT_F(drawn.left - air, drawn.top - air, drawn.right + air, drawn.bottom + air);
    }

    // The part under the pointer, and the one a press is down on, for a line of two targets.
    private int hotPart = -1;
    private RowPress press;

    /// <summary>A line of two targets lights each itself; a line of one is lit by the folder as ever.</summary>
    protected internal override bool LightsItsRows => Split;

    protected internal override void CardHover(Vector2? point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        if (!Split)
        {
            return;
        }
        var at = point is { } where ? CardTarget(where, box, style, surface) : -1;
        var changed = press.Over(at);
        if (at != hotPart || changed)
        {
            hotPart = at;
            Redraw(Gfx);
        }
    }

    protected internal override void CardDown(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        if (Split && press.Down(CardTarget(point, box, style, surface)))
        {
            Redraw(Gfx);
        }
    }

    public override void Visible(bool shown)
    {
        base.Visible(shown);
        if (!shown)
        {
            hotPart = -1;
            press.Up();
            radioLine?.Stop();
        }
        else if (Container is FolderIsland)
        {
            // The network or the devices beside the name, from the pages' sources, while the folder shows it.
            radioLine?.Start(QuickPageTile.SourcesNow, Post);
        }
    }

    // --- As a list's line -------------------------------------------------------
    // A switch's line in a list folder at whatever height it is given (docs\widgets.md §9, "A line
    // at every height"), drawn from the switch's own data — its glyph, its name, its state and what a
    // click does — so a switch added to the list has its line with nothing of its own:
    //
    //   * One row: the glyph in the list's leading column, in the accent while what it stands for is
    //     on; the name; and against the trailing gutter the reading — the switch itself where a click
    //     switches what it stands for (IQuickSwitch.Switches), else the state in words where Windows
    //     says it, or what it is on as (the VPN it is connected to). The reading wins its row: the
    //     name takes what is left, whole, short or cut while it still reads (ListTiers.Row).
    //   * The compact line: the glyph larger, the name, and under it the state in words where no
    //     switch says it and what a click does; the switch against the trailing gutter.
    //   * The whole widget: the name over the state, large, with the switch beside it, and what a
    //     click does under them.
    //
    // Which one a line draws is what fits its height at the text size in force (ListTiers.Of), and
    // the whole widget never says less than the compact line would at the same height. What is
    // shorter than the line stands at its top. The whole line is the button.

    /// <summary>
    /// Where a switch's line puts its parts at one height, in the box's px: its tier; the glyph's box
    /// and size; the name's line and the form of it that reads there (none where none does); the
    /// state in words, its box and size (empty where the line says none there); what is said under
    /// them (each line's box, words and the lines it may wrap to); and the switch (empty where the
    /// line has none).
    /// </summary>
    internal readonly record struct SwitchLine(
        ListTier Tier,
        D2D_RECT_F Glyph,
        float GlyphPx,
        D2D_RECT_F Name,
        string? NameText,
        D2D_RECT_F State,
        string StateText,
        float StatePx,
        IReadOnlyList<(D2D_RECT_F Box, string Text, int Lines)> Under,
        D2D_RECT_F Switch)
    {
        /// <summary>How much the line says: its name, its state or switch, and each line under them, whole or not.</summary>
        public int Says => (NameText is null ? 0 : 1) + (StateText.Length > 0 || Switch.right > Switch.left ? 1 : 0) + Under.Count + (SubText is null ? 0 : 1);

        /// <summary>The radio's words beside the name, on its row and its baseline, at the caption size (RadioLine): their box and words; none where it says nothing or has no room.</summary>
        public D2D_RECT_F Sub { get; init; }

        /// <summary>The words <see cref="Sub"/> holds.</summary>
        public string? SubText { get; init; }
    }

    /// <summary>
    /// Its form for its size (§432): its row (Compact); or its name over its state in words where no switch says
    /// it and what a click does, on the lines that takes, two at the most (Full, the compact line) — so a switch
    /// whose description wraps gets the rows it needs.
    /// </summary>
    public float ListHeight(float width, in TileStyle style)
    {
        if (LineSizes.Of(Settings) == LineSize.Compact)
        {
            return LineRows.Px(1, style);
        }
        Follow();
        var form = ListForm.Of(Gfx, style);
        var switches = DrawsSwitch;
        var box = PopupPaint.Rect(0, 0, width, LineRows.Px(1, style));
        var right = width - form.Inset;
        var end = PageShown ? WordsEnd(box, style, switches) : (float?)null;
        var room = MathF.Max(1, (end ?? (switches ? right - style.Px(ToggleWidthDip) - style.Gap : right)) - form.TextLeft);
        var state = action.Available ? State(action.On, action.Detail) : State(false);
        List<(string Text, int Lines)> said = [];
        if (!switches && state.Length > 0)
        {
            said.Add((state, 1));
        }
        if (action.Note is { Length: > 0 } note)
        {
            said.Add((note, 2));
        }
        var lines = ListTiers.Lines(Gfx, said, room, float.PositiveInfinity, form.SmallPx);
        return MathF.Max(LineRows.Px(1, style), (2 * form.Pad) + form.Line + lines.Sum(each => each.Height));
    }

    /// <summary>The names a line may call the switch by, fullest first.</summary>
    private string[] Names => ShortName is { } shorter ? [Info.Title, shorter] : [Info.Title];

    /// <summary>
    /// The line's layout in <paramref name="box"/>, the drawing's and a test's one question (see the
    /// note above).
    /// </summary>
    internal SwitchLine LineOf(D2D_RECT_F box, in TileStyle style)
    {
        Follow();
        var form = ListForm.Of(Gfx, style);
        var height = box.bottom - box.top;
        var available = action.Available;
        var switches = DrawsSwitch;
        var shown = Showing(out var busy);
        // On its way, the line says so where it says its state, else beside its name (below).
        var turning = busy && available && shown is { } going ? (going ? S.WidgetSwitchTurningOn : S.WidgetSwitchTurningOff) : null;
        var state = turning ?? (available ? State(action.On, action.Detail) : State(false));
        var note = action.Note ?? "";
        // Wi-Fi's network, Bluetooth's devices: beside the name at every height, never a line of their own.
        IReadOnlyList<string> sub = turning is not null ? [] : available && radioLine is { Following: true } radio ? radio.Says(action.On) : [];
        var toggleWidth = style.Px(ToggleWidthDip);
        var toggleHeight = style.Px(ToggleHeightDip);
        // A line with a page keeps its words before the ">"'s column; the switch, where it draws one, is
        // in the switch's column after it either way, on the trailing gutter.
        float? end = PageShown ? WordsEnd(box, style, switches) : null;
        var reading = switches || state.Length > 0;
        // The compact line holds the name and one line under it; the whole widget the name, the state
        // at the compact line's figure size and a line of what a click does — and, having no state to
        // show large, the name and two lines under it.
        var compact = (2 * form.Pad) + form.Line + form.SmallLine;
        var valueLine = MathF.Max(TilePaint.LineHeight(Gfx, ListTiers.ValuePx(style), Weight.Semibold), switches ? toggleHeight : 0);
        var full = reading
            ? (2 * form.Pad) + form.Line + form.Gap + valueLine + (note.Length > 0 ? form.Gap + form.SmallLine : 0)
            : (2 * form.Pad) + form.Line + (2 * form.SmallLine);
        // §432: in a folder its size's form, whatever the height: its row (Compact), or the compact line, its name over
        // what a click does (Full); anywhere else the fullest that fits.
        var tier = SizedTier(ListTier.Compact) ?? ListTiers.Of(height, compact, MathF.Max(full, compact + form.Gap));
        SwitchLine laid;
        if (tier == ListTier.One)
        {
            laid = OneLine(box, style, form, switches, state, toggleWidth, toggleHeight, end);
        }
        else
        {
            var lean = CompactLine(box, style, form, switches, state, note, toggleWidth, toggleHeight, end);
            var whole = tier == ListTier.Compact ? lean : WholeLine(box, style, form, switches, state, note, toggleWidth, toggleHeight, end);
            laid = whole.Says >= lean.Says ? whole : lean;
        }
        if (turning is not null && laid.StateText.Length == 0)
        {
            // A line whose switch says its state has no words for it: "Turning on…" beside the name,
            // where Windows' tile says it on its second line.
            sub = [turning];
        }
        else if (turning is null && laid.StateText.Length == 0 && available && Info.Switch == QuickSwitch.KeepAwake && action.Detail is { Length: > 0 } left)
        {
            // keep awake: a timed one's time left beside the name, where the line says no state in
            // words, as a radio's network is (§269).
            sub = [left];
        }
        return Beside(laid, sub, form, style, end);
    }

    /// <summary>
    /// The radio's words (<paramref name="sub"/>, fullest first) beside the name, on its row, in the
    /// secondary ink, a gap after the name's end (2026-10-16, the person: "show the name of the wifi or
    /// connected devices to the right of the title and not underneath"), at Windows' caption size (12
    /// at 100 % text to the name's 14; the person: "a little smaller") and standing on the name's
    /// baseline: the first form that fits whole in what the row has left before the switch or the
    /// state, else the last cut with an ellipsis where its first four letters still show, else
    /// nothing. The name is laid first and never gives way to them.
    /// </summary>
    private SwitchLine Beside(SwitchLine line, IReadOnlyList<string> sub, in ListForm form, in TileStyle style, float? end)
    {
        if (sub.Count == 0 || line.NameText is not { } name)
        {
            return line;
        }
        var weight = line.Tier == ListTier.One ? Weight.Regular : Weight.Semibold;
        var nameRight = MathF.Min(line.Name.right, line.Name.left + PopupPaint.Measure(Gfx, name, form.TextPx, weight).X);
        var left = PopupPaint.Round(nameRight + style.Gap);
        // On one row the switch or the state ends it; taller, the name's own line, which the switch leaves;
        // on a line with a page never past the air before its ">".
        var right = line.Tier == ListTier.One && TilePaint.Width(line.State) > 0 ? line.State.left - style.Gap
            : line.Tier == ListTier.One && TilePaint.Width(line.Switch) > 0 ? line.Switch.left - style.Gap
            : line.Name.right;
        right = MathF.Min(right, end ?? float.MaxValue);
        if (ListTiers.Name(Gfx, sub, form.SmallPx, Weight.Regular, right - left) is not { } words)
        {
            return line;
        }
        // The smaller line's top, so its baseline is the name's.
        var top = line.Name.top + PopupPaint.Round(CountdownCard.Baseline(Gfx, form.TextPx, weight) - CountdownCard.Baseline(Gfx, form.SmallPx, Weight.Regular));
        return line with
        {
            Name = PopupPaint.Rect(line.Name.left, line.Name.top, MathF.Max(1, nameRight - line.Name.left), form.Line),
            Sub = PopupPaint.Rect(left, top, right - left, form.SmallLine),
            SubText = words,
        };
    }

    /// <summary>One row: the glyph, the name, and the switch or the state against the trailing gutter.</summary>
    private SwitchLine OneLine(D2D_RECT_F box, in TileStyle style, in ListForm form, bool switches, string state, float toggleWidth, float toggleHeight, float? end)
    {
        var said = state.Length > 0 ? PopupPaint.Measure(Gfx, state, form.TextPx, Weight.Regular).X : 0;
        // With a page the words' field ends before the ">", and the switch stands in its column apart.
        var one = end is { } words
            ? ListTiers.Row(Gfx, box, style, Names, Weight.Regular, switches ? 0 : said, MathF.Max(0, box.right - form.Inset - words))
            : ListTiers.Row(Gfx, box, style, Names, Weight.Regular, switches ? toggleWidth : said);
        var glyphBox = form.IconBox(box.left, one.Middle - (form.Line / 2), form.Line);
        var toggleBox = switches
            ? PopupPaint.Rect(box.right - form.Inset - toggleWidth, PopupPaint.Round(one.Middle - (toggleHeight / 2)), toggleWidth, toggleHeight)
            : default;
        var stateBox = !switches && state.Length > 0 ? PopupPaint.Rect(one.FieldLeft, one.TextTop, one.Field, form.Line) : default;
        return new SwitchLine(
            ListTier.One,
            glyphBox,
            form.Glyph,
            PopupPaint.Rect(one.NameLeft, one.TextTop, one.NameRoom, form.Line),
            one.Name,
            stateBox,
            TilePaint.Width(stateBox) > 0 ? state : "",
            form.TextPx,
            [],
            toggleBox);
    }

    /// <summary>
    /// The compact line: the name, and under it the state in words where no switch says it and what
    /// a click does on up to two lines, as many as the height holds; the switch against the trailing
    /// gutter, beside them.
    /// </summary>
    private SwitchLine CompactLine(D2D_RECT_F box, in TileStyle style, in ListForm form, bool switches, string state, string note, float toggleWidth, float toggleHeight, float? end)
    {
        var height = box.bottom - box.top;
        var left = box.left + form.TextLeft;
        var right = box.right - form.Inset;
        var room = MathF.Max(1, (end ?? (switches ? right - toggleWidth - style.Gap : right)) - left);
        var name = ListTiers.Name(Gfx, Names, form.TextPx, Weight.Semibold, room);
        List<(string Text, int Lines)> said = [];
        if (!switches && state.Length > 0)
        {
            said.Add((state, 1));
        }
        if (note.Length > 0)
        {
            said.Add((note, 2));
        }
        var fitted = ListTiers.Lines(Gfx, said, room, height - (2 * form.Pad) - form.Line, form.SmallPx);
        var block = form.Line + fitted.Sum(each => each.Height);
        var top = LineRows.Top(box.top, height, block, form.Pad);
        // The switch beside the words, in the middle of them.
        var toggleBox = switches
            ? PopupPaint.Rect(right - toggleWidth, PopupPaint.Round(top + ((block - toggleHeight) / 2)), toggleWidth, toggleHeight)
            : default;
        return new SwitchLine(
            ListTier.Compact,
            ListTiers.GlyphBox(form, box.left, top, form.Line, ListTiers.GlyphPx(form, style, ListTier.Compact)),
            ListTiers.GlyphPx(form, style, ListTier.Compact),
            PopupPaint.Rect(left, top, room, form.Line),
            name,
            default,
            "",
            form.TextPx,
            ListTiers.Stacked(fitted, left, room, top + form.Line),
            toggleBox);
    }

    /// <summary>
    /// The whole widget: the name on its line; the state under it, large, with the switch on its row
    /// against the trailing gutter; and what a click does under them on up to three lines, as many as
    /// the height holds.
    /// </summary>
    private SwitchLine WholeLine(D2D_RECT_F box, in TileStyle style, in ListForm form, bool switches, string state, string note, float toggleWidth, float toggleHeight, float? end)
    {
        var height = box.bottom - box.top;
        var left = box.left + form.TextLeft;
        var right = box.right - form.Inset;
        var room = MathF.Max(1, (end ?? right) - left);
        var name = ListTiers.Name(Gfx, Names, form.TextPx, Weight.Semibold, room);
        var reading = switches || state.Length > 0;
        // The state as large as its height makes it where what a click does keeps a line.
        var rest = (2 * form.Pad) + form.Line + form.Gap + (note.Length > 0 ? form.Gap + form.SmallLine : 0);
        var statePx = reading ? ListTiers.HeroPx(Gfx, style, height, rest) : form.TextPx;
        var stateRoom = end is null && switches ? MathF.Max(1, room - toggleWidth - (2 * style.Gap)) : room;
        var stateWidth = state.Length > 0 ? MathF.Min(stateRoom, PopupPaint.Measure(Gfx, state, statePx, Weight.Semibold).X) : 0;
        var stateLine = reading ? MathF.Max(TilePaint.LineHeight(Gfx, statePx, Weight.Semibold), switches ? toggleHeight : 0) : 0;
        var above = form.Line + (reading ? form.Gap + stateLine : 0);
        var fitted = ListTiers.Lines(Gfx, note.Length > 0 ? [(note, 3)] : [], room, height - (2 * form.Pad) - above - form.Gap, form.SmallPx);
        var block = above + (fitted.Count > 0 ? form.Gap + fitted.Sum(each => each.Height) : 0);
        var top = LineRows.Top(box.top, height, block, form.Pad);
        var stateTop = top + form.Line + form.Gap;
        var stateBox = state.Length > 0 ? PopupPaint.Rect(left, stateTop, stateWidth, stateLine) : default;
        var toggleBox = switches && right - toggleWidth >= left
            ? PopupPaint.Rect(right - toggleWidth, stateTop + PopupPaint.Round((stateLine - toggleHeight) / 2), toggleWidth, toggleHeight)
            : default;
        return new SwitchLine(
            ListTier.Full,
            ListTiers.GlyphBox(form, box.left, top, form.Line, ListTiers.GlyphPx(form, style, ListTier.Full)),
            ListTiers.GlyphPx(form, style, ListTier.Full),
            PopupPaint.Rect(left, top, room, form.Line),
            name,
            stateBox,
            TilePaint.Width(stateBox) > 0 ? state : "",
            statePx,
            ListTiers.Stacked(fitted, left, room, top + above + form.Gap),
            toggleBox);
    }

    /// <summary>
    /// The line (<see cref="LineOf"/>): the glyph, in the accent while what it stands for is on; the
    /// name; the state in words or the switch; and what is said under them in the secondary ink, all
    /// cut with an ellipsis inside the gutter where the room runs out. Something this PC hasn't got is
    /// drawn off, and the folder greys the whole line (<see cref="UnavailableOn"/>).
    /// </summary>
    private void PaintLine(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style)
    {
        var theme = style.Theme;
        var quick = QuickSettings.Controls.QuickTheme.For(theme);
        var line = LineOf(box, style);
        if (PageShown)
        {
            if (Split && (press.Held == PageTarget || (press.Held == 0 && hotPart == PageTarget)))
            {
                // The page's part is the whole line but its switch: lit as the folder lights a line, to its
                // edges round the form (§611), the switch lighting itself (below).
                PopupPaint.Fill(dc, LineAround(box, style), 0, press.Held == PageTarget ? quick.SubtlePressed : quick.SubtleHover);
            }
            PopupPaint.Glyph(dc, Gfx, ChevronGlyph, style.Px(ChevronGlyphDip), theme.Text, ChevronGlyphBox(ChevronBand(box, line), style, DrawsSwitch));
        }
        var available = action.Available;
        var shown = Showing(out var busy);
        var on = available && shown == true;
        PopupPaint.Glyph(dc, Gfx, Shown, line.GlyphPx, on ? quick.Accent : theme.Text, line.Glyph);
        var ink = theme.Text;
        if (line.NameText is { } name)
        {
            TilePaint.Line(dc, Gfx, name, ListForm.Of(Gfx, style).TextPx, line.Tier == ListTier.One ? Weight.Regular : Weight.Semibold, ink, line.Name.left, line.Name.top, TilePaint.Width(line.Name));
        }
        if (line.SubText is { } sub)
        {
            TilePaint.Line(dc, Gfx, sub, ListForm.Of(Gfx, style).SmallPx, Weight.Regular, theme.TextSecondary, line.Sub.left, line.Sub.top, TilePaint.Width(line.Sub));
        }
        if (line.StateText.Length > 0)
        {
            if (line.Tier == ListTier.One)
            {
                TilePaint.LineRight(dc, Gfx, line.StateText, line.StatePx, Weight.Regular, theme.TextSecondary, line.State.right, line.State.top, TilePaint.Width(line.State));
            }
            else
            {
                var stateLine = TilePaint.LineHeight(Gfx, line.StatePx, Weight.Semibold);
                TilePaint.Line(dc, Gfx, line.StateText, line.StatePx, Weight.Semibold, ink, line.State.left, line.State.top + PopupPaint.Round((TilePaint.Height(line.State) - stateLine) / 2), TilePaint.Width(line.State) + 1);
            }
        }
        foreach (var (at, words, lines) in line.Under)
        {
            PopupPaint.Wrapped(dc, Gfx, words, ListForm.Of(Gfx, style).SmallPx, Weight.Regular, theme.TextSecondary, at.left, at.top, TilePaint.Width(at), lines);
        }
        if (TilePaint.Width(line.Switch) > 0)
        {
            toggle.On = on;
            // On its way, where it is going and greyed, as Settings greys its switch until Windows has done.
            toggle.Enabled = !(available && busy);
            // On a line of two the switch lights itself, apart from the line: WinUI's hover and press.
            toggle.Hot = Split && (press.Held == ToggleTarget || (press.Held == 0 && hotPart == ToggleTarget));
            toggle.Pressed = Split && press.Held == ToggleTarget;
            toggle.Bounds = PopupPaint.Rect(0, 0, TilePaint.Width(line.Switch), TilePaint.Height(line.Switch));
            var context = new QuickSettings.Controls.QuickContext { Gfx = Gfx, Theme = quick, Scale = style.Scale, TextScale = style.TextScale };
            TilePaint.Placed(dc, line.Switch.left, line.Switch.top, 1, () => toggle.Paint(dc, context));
        }
    }
}
