// A row of toggle icons in a list folder (docs\widgets.md §9, "Toggles"; Karpo.Core\Toggles.cs).
//
// The person, 2026-10-09: "one list line holding several toggle icons … The user picks which
// toggles and their order. Icon-only row (wraps to more rows if many). No details/arrow: pure
// on/off." So each toggle is a button of Windows 11's quick settings without its words: its glyph on
// the control fill with the control's outline while it is off, on the accent with the on-accent ink
// while it is on, and the disabled ink where this PC has not got it. The buttons stand on the list's
// gutters on one grid — as many to a row as the width holds at their least, sharing the width
// evenly, the rest wrapped under them in the same columns — so they line up whatever wraps.
//
// Each button is its own target: a click switches that toggle (IQuickSwitch.Act) and the folder
// stays open; the button under the pointer lights, and its tooltip says its name and where it
// stands (FolderFlyout.Tips.cs). With the keyboard on the line, Left and Right walk the buttons and
// Enter or Space switches the one ringed; past either end the folder's own keys take over.
//
// The switches are the one-button widgets' own, read from their data sources (SwitchData), so a row
// and an airplane button on the bar read one switch, followed from the dock's start; Wi-Fi and
// Bluetooth are their radios (ToggleSwitches). A test hands made-up ones (ToggleSwitches.Source,
// QuickSwitches.Source) and they are this row's alone.

using System.Numerics;
using Karpo.Core;
using Karpo.Dock.QuickSettings.Controls;
using Karpo.Dock.QuickSettings.Services;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;

namespace Karpo.Dock;

/// <summary>A toggles row laid out: each button's box in the line's px, how many to a row, and how tall it all is.</summary>
internal readonly record struct TogglesPlan(IReadOnlyList<D2D_RECT_F> Buttons, int PerRow, float Height);

internal sealed unsafe class TogglesTile : CellIsland, IListContent, ILinePieces
{
    /// <summary>One toggle of the row: what it is, its switch, and the source it came from (null for a test's own).</summary>
    private sealed record Held(ToggleInfo Info, IQuickSwitch Switch, SwitchData? Data)
    {
        /// <summary>Its click on its way, shared with every button of the switch (SwitchPending).</summary>
        public SwitchPending Pending { get; } = Pendings.Of(Switch);
    }

    private readonly DataSources sources;
    private List<Held> held = [];

    /// <summary>Wakes the row while a toggle is on its way: when it shows, each turn of its ring, its timeout.</summary>
    private readonly PendingClock clock;

    // Whether the open folder shows the row, and whether Windows' animations were on at its last paint:
    // the ring turns only while both are, and stands still with animations off.
    private bool showing;
    private bool animate = true;

    // The button under the pointer; -1 for none. The keys' is the folder's ring (FolderFlyout.Keys.cs).
    private int hot = -1;

    public TogglesTile(string key, WidgetItem island, Gfx gfx, Config config)
        : this(key, island, gfx, config, DataSources.Shared)
    {
    }

    internal TogglesTile(string key, WidgetItem island, Gfx gfx, Config config, DataSources sources)
        : base(key, island, gfx)
    {
        this.sources = sources;
        clock = new PendingClock(this);
        Configure(island, config);
    }

    /// <summary>Nothing ticks: each switch says when it changed.</summary>
    public override int IntervalMs => 0;

    protected override int Count => 1;

    /// <summary>The toggles it shows, in their order.</summary>
    internal IReadOnlyList<Toggle> Shown => [.. held.Select(entry => entry.Info.Toggle)];

    /// <summary>Where toggle <paramref name="index"/> stands: on, off, or null when Windows has not said.</summary>
    internal bool? OnAt(int index) => (uint)index < (uint)held.Count ? held[index].Switch.On : null;

    public override void Configure(WidgetItem island, Config config)
    {
        Settings = island;
        var wanted = Toggles.Known(island.Toggles?.Shown);
        if (!wanted.SequenceEqual(held.Select(entry => entry.Info.Toggle)))
        {
            // The row changed: the switches it no longer shows are let go, the new ones taken.
            var kept = held.ToDictionary(entry => entry.Info.Toggle);
            var next = new List<Held>(wanted.Count);
            foreach (var toggle in wanted)
            {
                next.Add(kept.Remove(toggle, out var had) ? had : Take(Toggles.Find(toggle)!));
            }
            foreach (var gone in kept.Values)
            {
                Let(gone);
            }
            held = next;
            hot = -1;
        }
        Forget();
    }

    /// <summary>
    /// The switch behind <paramref name="info"/>: its data source's, followed since the dock's start;
    /// a test's own, started here.
    /// </summary>
    private Held Take(ToggleInfo info)
    {
        Held taken;
        if (ToggleSwitches.Source is null && QuickSwitches.Source is null
            && sources.Hold(SwitchData.KeyOf(info.Toggle), Settings) is SwitchData data)
        {
            data.Listen(Post);
            taken = new Held(info, data.Switch, data);
        }
        else
        {
            var own = ToggleSwitches.For(info.Toggle);
            own.Start(Post);
            taken = new Held(info, own, null);
        }
        taken.Pending.Listen(Post);
        return taken;
    }

    private void Let(Held gone)
    {
        gone.Pending.Unlisten(Post);
        if (gone.Data is { } data)
        {
            data.Unlisten(Post);
            sources.Release(data);
        }
        else
        {
            gone.Switch.Dispose();
        }
    }

    public override void Dispose()
    {
        foreach (var entry in held)
        {
            Let(entry);
        }
        held = [];
        clock.Dispose();
        base.Dispose();
    }

    /// <summary>A switch of its own hears the dock's broadcasts through the row; a shared one hears them from its source.</summary>
    public override void System(uint msg, WPARAM wParam, LPARAM lParam)
    {
        foreach (var entry in held.Where(entry => entry.Data is null))
        {
            entry.Switch.Heard(msg, (nuint)wParam);
        }
    }

    public override string? Tooltip(int index) => null;

    /// <summary>A click on button <paramref name="index"/> less one switches it; anywhere else does nothing.</summary>
    public override IslandAction Click(int index)
    {
        Flip(index - 1);
        return default;
    }

    /// <summary>Switches toggle <paramref name="index"/>, where this PC has it.</summary>
    internal void Flip(int index)
    {
        if ((uint)index >= (uint)held.Count || !held[index].Switch.Available)
        {
            return;
        }
        // A click while the last is on its way is ignored (SwitchPending).
        var toggle = held[index].Switch;
        Pendings.Act(toggle, toggle.Act);
        Post();
    }

    /// <summary>Whether toggle <paramref name="index"/> is on its way and shows it: for the tests.</summary>
    internal bool PendingAt(int index)
    {
        if ((uint)index >= (uint)held.Count)
        {
            return false;
        }
        _ = Showing(held[index], SwitchPending.Now, out var busy);
        return busy;
    }

    /// <summary>
    /// Where toggle <paramref name="entry"/> is drawn: where a click sent it while that is on its way
    /// and shows, else where it stands; and whether it is on its way. Settles the click first.
    /// </summary>
    private static bool? Showing(Held entry, long now, out bool busy)
    {
        entry.Pending.Settle(entry.Switch.On, entry.Switch.Refusals, now);
        busy = entry.Pending.Shows(now);
        return entry.Pending.Shown(entry.Switch.On, now);
    }

    /// <summary>
    /// The pending toggles settle, and the clock is set for the soonest thing one has to change by
    /// itself — the ring's next turn where one shows and turns.
    /// </summary>
    protected override bool Sample(bool everything)
    {
        var now = SwitchPending.Now;
        long? due = null;
        var turning = false;
        foreach (var entry in held)
        {
            _ = Showing(entry, now, out var busy);
            turning |= busy;
            if (entry.Pending.Due(now) is { } at)
            {
                due = Math.Min(due ?? long.MaxValue, at);
            }
        }
        if (turning && showing && animate)
        {
            clock.Step();
            due = Math.Min(due ?? long.MaxValue, now + Spinner.TickMs);
        }
        clock.Arm(due, now);
        return false;
    }

    internal override void PaintCell(ID2D1DeviceContext* dc, int index, in IslandStyle style, float width, float height) =>
        throw new NotSupportedException($"{Info.Id} is a list's line; the bar has no toggles row.");

    public override void Visible(bool shown)
    {
        base.Visible(shown);
        showing = shown;
        if (!shown)
        {
            hot = -1;
        }
        else if (held.Exists(entry => entry.Pending.Busy))
        {
            // A ring that was on its way while the folder was shut turns again.
            Post();
        }
    }

    // --- Laid out -----------------------------------------------------------------------------------

    /// <summary>A button's least side, in DIPs before the text scale: Windows' quick-settings button height.</summary>
    private const float ButtonDip = 48;

    /// <summary>
    /// <paramref name="count"/> buttons on a line <paramref name="width"/> px wide: as many to a row as
    /// fit at their least, each as wide as the row's share between the list's gutters, wrapped in the
    /// same columns; the line's air above and below. Pure, so a test reads what the drawing draws.
    /// </summary>
    internal static TogglesPlan Lay(int count, float width, in TileStyle style)
    {
        var gutter = ListForm.Gutter(style);
        var room = MathF.Max(1, width - (2 * gutter));
        var gap = style.Px(8);
        var side = MathF.Max(style.Px(ButtonDip), PopupPaint.Round(style.Text(16) + style.Px(32)));
        var perRow = Math.Max(1, (int)MathF.Floor((room + gap) / (side + gap)));
        var across = MathF.Floor((room - ((perRow - 1) * gap)) / perRow);
        var pad = style.Px(4);
        var buttons = new List<D2D_RECT_F>(count);
        for (var index = 0; index < count; index++)
        {
            var column = index % perRow;
            var row = index / perRow;
            buttons.Add(PopupPaint.Rect(gutter + (column * (across + gap)), pad + (row * (side + gap)), across, side));
        }
        var rows = Math.Max(1, (count + perRow - 1) / perRow);
        return new TogglesPlan(buttons, perRow, count == 0 ? 0 : pad + (rows * side) + ((rows - 1) * gap) + pad);
    }

    /// <summary>
    /// §432: every row of its buttons — a toggle more is a row more where the last is full — in the whole rows of
    /// the list that hold them, the buttons in their middle; or one row of the list, saying none were chosen.
    /// </summary>
    public float ListHeight(float width, in TileStyle style) =>
        held.Count > 0 ? LineRows.Whole(Lay(held.Count, width, style).Height, style) : LineRows.Px(1, style);

    /// <summary>
    /// The plan in <paramref name="box"/>: the buttons in the middle of a line taller than they are.
    /// A line held shorter than its rows (a fixed height, docs\widgets.md §76) shows the whole rows
    /// that fit and none cut in half; shorter than one row, that row's buttons are as tall as the
    /// line leaves them.
    /// </summary>
    internal TogglesPlan Plan(D2D_RECT_F box, in TileStyle style) => Fit(Lay(held.Count, box.right - box.left, style), box, style);

    /// <summary><paramref name="plan"/> (<see cref="Lay"/>) held to <paramref name="box"/>: see <see cref="Plan"/>.</summary>
    internal static TogglesPlan Fit(TogglesPlan plan, D2D_RECT_F box, in TileStyle style)
    {
        var tall = box.bottom - box.top;
        if (plan.Buttons.Count > 0 && plan.Height > tall + 0.5f)
        {
            var pad = style.Px(4);
            var gap = style.Px(8);
            var side = plan.Buttons[0].bottom - plan.Buttons[0].top;
            var rows = (int)MathF.Floor((tall - (2 * pad) + gap + 0.5f) / (side + gap));
            if (rows >= 1)
            {
                plan = plan with
                {
                    Buttons = [.. plan.Buttons.Take(rows * plan.PerRow)],
                    Height = pad + (rows * side) + ((rows - 1) * gap) + pad,
                };
            }
            else
            {
                var squeezed = MathF.Max(1, MathF.Floor(tall - (2 * pad)));
                plan = plan with
                {
                    Buttons = [.. plan.Buttons.Take(plan.PerRow).Select(at => new D2D_RECT_F(at.left, at.top, at.right, at.top + squeezed))],
                    Height = squeezed + (2 * pad),
                };
            }
        }
        // In the middle of a line taller than the rows, on a whole px (2026-10-12, the person: "the
        // toggles should be vertically centered"): the one kind §109's top rule does not hold, as a
        // row of buttons is one control, not a list of items read from the top.
        var down = box.top + MathF.Max(0, MathF.Floor((tall - plan.Height) / 2));
        return plan with { Buttons = [.. plan.Buttons.Select(at => new D2D_RECT_F(box.left + at.left, down + at.top, box.left + at.right, down + at.bottom))] };
    }

    // --- Drawn -----------------------------------------------------------------------------------

    protected internal override void PaintCard(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var theme = QuickTheme.For(style.Theme);
        if (held.Count == 0)
        {
            var gutter = ListForm.Gutter(style);
            var px = style.Text(14);
            var line = TilePaint.LineHeight(Gfx, px);
            TilePaint.Line(dc, Gfx, S.WidgetTogglesNone, px, Weight.Regular, theme.TextTertiary, box.left + gutter,
                LineRows.Top(box.top, box.bottom - box.top, line, ListForm.Of(Gfx, style).Pad), MathF.Max(1, box.right - box.left - (2 * gutter)));
            return;
        }
        var plan = Plan(box, style);
        var radius = style.Px(4);
        var hairline = MathF.Max(1, style.Px(1));
        var glyph = style.Text(16);
        var now = SwitchPending.Now;
        animate = style.Look.Animations;
        for (var index = 0; index < plan.Buttons.Count; index++)
        {
            var at = plan.Buttons[index];
            var toggle = held[index].Switch;
            var shown = Showing(held[index], now, out var busy);
            busy &= toggle.Available;
            // On its way it is lit by nothing: a click on it now does nothing.
            var lit = index == hot && !busy;
            DXGI_RGBA ink;
            if (!toggle.Available)
            {
                PopupPaint.Fill(dc, at, radius, theme.ControlFillDisabled);
                ink = theme.TextDisabled;
            }
            else if (shown == true)
            {
                // Windows 11's own tile: the accent behind the glyph while it is on — and from the
                // moment a click sends it on, as the tile does, with the ring until Windows has done.
                PopupPaint.Fill(dc, at, radius, lit ? theme.AccentHover : theme.Accent);
                ink = theme.TextOnAccent;
            }
            else
            {
                PopupPaint.Fill(dc, at, radius, lit ? theme.ControlFillHover : theme.ControlFill);
                PopupPaint.Stroke(dc, Inset(at, hairline / 2), radius, hairline, theme.ControlStroke);
                ink = theme.Text;
            }
            PopupPaint.Glyph(dc, Gfx, held[index].Switch.Glyph ?? held[index].Info.Glyph, glyph, ink, at);
            if (busy)
            {
                BusyRing(dc, at, style, theme, shown == true, clock.Spin);
            }
        }
    }

    private static D2D_RECT_F Inset(D2D_RECT_F box, float by) => new(box.left + by, box.top + by, box.right - by, box.bottom - by);

    /// <summary>
    /// A pending toggle's ring's side and its air from the button's top right corner, in DIPs: not
    /// grown with the text, as the glyph is, so it stays in the corner clear of it.
    /// </summary>
    internal const float RingDip = 12;

    internal const float RingInDip = 5;

    /// <summary>Where a pending toggle's ring stands on button <paramref name="at"/>: its top right corner, clear of the glyph.</summary>
    internal static D2D_RECT_F RingBox(D2D_RECT_F at, in TileStyle style)
    {
        var side = style.Px(RingDip);
        var air = style.Px(RingInDip);
        return PopupPaint.Rect(at.right - air - side, at.top + air, side, side);
    }

    /// <summary>
    /// The turning ring of a toggle on its way (SwitchPending), in its corner: in the ink on the accent
    /// on a button going on, in the accent on one going off. It turns at <paramref name="spin"/>, which
    /// stands still with Windows' animations off.
    /// </summary>
    private static void BusyRing(ID2D1DeviceContext* dc, D2D_RECT_F at, in TileStyle style, QuickTheme theme, bool onAccent, float spin)
    {
        var arc = onAccent ? theme.TextOnAccent : theme.Accent;
        // On the accent the ink is strong: its track fainter, so the turning quarter still reads.
        var track = onAccent ? QuickTheme.WithAlpha(theme.TextOnAccent, 0.2f) : QuickTheme.WithAlpha(theme.StrongFill, 0.35f);
        Progress.Ring(dc, RingBox(at, style), spin, style.Scale, arc, track);
    }

    /// <summary>The Windows focus rectangle round a button, as the quick settings' buttons draw it.</summary>
    internal static void FocusRing(ID2D1DeviceContext* dc, D2D_RECT_F box, float radius, in TileStyle style, QuickTheme theme)
    {
        var margin = style.Px(3);
        var outer = MathF.Max(1, style.Px(2));
        var inner = MathF.Max(1, style.Px(1));
        var ring = new D2D_RECT_F(box.left - margin, box.top - margin, box.right + margin, box.bottom + margin);
        PopupPaint.Stroke(dc, Inset(ring, outer / 2), radius + margin, outer, theme.FocusOuter);
        PopupPaint.Stroke(dc, Inset(ring, outer + (inner / 2)), MathF.Max(0, radius + margin - outer), inner, theme.FocusInner);
    }

    /// <summary>Which button is at <paramref name="point"/>, plus one; 0 for the room between them.</summary>
    protected internal override int CardTarget(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var plan = Plan(box, style);
        for (var index = 0; index < plan.Buttons.Count; index++)
        {
            if (LineRows.Inside(plan.Buttons[index], point))
            {
                return index + 1;
            }
        }
        return 0;
    }

    /// <summary>The button under the pointer lights; the folder draws what it just told.</summary>
    protected internal override void CardHover(Vector2? point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var at = point is { } where ? CardTarget(where, box, style, surface) - 1 : -1;
        if (at != hot)
        {
            hot = at;
            Redraw(Gfx);
        }
    }

    /// <summary>The rows of buttons light themselves; the line does not light as well.</summary>
    protected internal override bool LightsItsRows => true;

    /// <summary>A button's tooltip: its name and where it stands ("Wi-Fi: on").</summary>
    protected internal override (string Text, D2D_RECT_F Part)? CardTip(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var at = CardTarget(point, box, style, surface) - 1;
        if (at < 0)
        {
            return null;
        }
        var toggle = held[at].Switch;
        var shown = Showing(held[at], SwitchPending.Now, out var busy);
        return (SwitchIsland.Say(held[at].Info.Name, toggle.Available, shown, note: null, busy: busy), Plan(box, style).Buttons[at]);
    }

    // --- As pieces ----------------------------------------------------------------------------------

    public string MainPiece => "toggle 0";

    public IReadOnlyList<LinePiece> LinePieces(D2D_RECT_F box, in TileStyle style)
    {
        var plan = Plan(box, style);
        return [.. plan.Buttons.Select((at, index) => new LinePiece($"toggle {index}", at, held[index].Info.Name))];
    }
}
