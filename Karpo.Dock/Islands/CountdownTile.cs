// The timer widget and the focus-time widget: one class, told by its kind which
// of the two it is: a line of a folder's list.
//
// It was an entry of a taskbar too, one row, its glyph and what it says, whose click
// opened its own panel, its card at full size. That became the timer and focus
// presets (2026-10-10, docs\widgets.md §92): a folder holding this line at the height
// of its whole card, under a face that says what the entry said (FaceDrawings,
// CountdownFaceDrawing), from this line's own countdown.
//
// It is the kit's one row (Clock\CountdownLine.cs, docs\widgets.md §401) at every height, the timer's and
// the focus session's alike: the glyph, the name, the time in the Value, a subtle minus and plus, and the
// accent start button at the trailing gutter; a taller line has the row at its top and nothing larger.
// This is the widget around it: what a click on each of its buttons does, and when
// the card is drawn again.
//
// Each one is a countdown of its own: "you should be able to add many different
// timers with their own default times" (the person, 2026-09-27). What it is doing
// is kept in the dock's Countdowns under the widget's id, in memory, so two timers
// run, pause and end apart, a timer and a focus session run at once, and one that
// is running goes on where the widget is carried — from one list folder to another
// (the move keeps the id). Its length and its name are the widget's own
// settings (CountdownOptions), in config.json beside the rest of its options: its
// steppers and the wheel set its length, and the Settings page sets both.
//
// The pointer: the user asked for "two buttons for increment and decrement and when
// scrolling with the mouse it should change the value as well". All of it goes
// through the folder's own routing (docs\widgets.md §9, "The pointer in a folder"),
// which was already built for the volume slider and the media buttons — this widget
// simply had not asked for it. Three overrides and a declaration:
//
//   * CardWheel      — a turn anywhere on the card steps the length.
//   * CardPress/Drag/Release — a stepper held down repeats, and lets go when the
//                      pointer leaves it, as a button does.
//   * CardHover      — the button under the pointer lights.
//
// and the descriptor says Interaction.Drag | Interaction.Wheel, because taking a
// press and taking the wheel are both things a kind has to declare before it is
// asked (Karpo.Tests\Edit\InteractionTests.cs holds both directions of that).
//
// The step itself is Countdown.StepMinutes, which is the one rule and was already
// there: the wheel, a click and a repeat all call it, so a minute means the same
// thing however somebody asks for it, and the clamp at either end is one clamp.
//
// A length is **not** written per step: a held stepper changes the number nine
// times a second. A step sets it in the widget's options and tells the dock
// (Countdowns.Remember), which writes the config once the steps have settled.
//
// A menu — a folder's right-click — works it through Act and Stop: its start button
// and the button that clears it, the same rules its buttons press, on its own
// countdown; and through Longer and Shorter, its plus and its minus, which a line one
// row tall has no room for.

using System.Numerics;
using Karpo.Core;
using Karpo.Dock.Native;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock;

internal sealed unsafe class CountdownTile : CellIsland, IListContent, ILinePieces
{
    /// <summary>How long a stepper is held before it starts repeating, in ms (its own panel shares it).</summary>
    private const int HoldBeforeRepeatMs = CountdownCard.HoldBeforeRepeatMs;

    /// <summary>How often it repeats after that, in ms: about nine steps a second.</summary>
    private const int RepeatEveryMs = CountdownCard.RepeatEveryMs;

    private readonly CountdownKind kind;

    // The countdowns and the wall clock: the dock's, which its islands, its folders and the clock's
    // panes share (the dock's Countdowns, Clock.Now). One made without them keeps a store of its own
    // that nobody else sees — a test's, or a widget drawn for a picture.
    private readonly Countdowns countdowns;
    private readonly Func<DateTime> now;

    // The pointer's own state, all of it this card's and none of it the folder's: which button it
    // is on, which stepper is held, whether the pointer is still on the held one, and when the
    // hold and the last step happened.
    private CountdownTargetKind hot;
    private CountdownTargetKind held;
    private bool onHeld;
    private long heldAt;
    private long steppedAt;

    public CountdownTile(string key, WidgetItem island, Gfx gfx, Config config)
        : this(key, island, gfx, config, null)
    {
    }

    /// <param name="countdowns">The dock's countdowns; null for a store of this widget's own.</param>
    /// <param name="now">The wall clock; <see cref="Clock.Now"/> when null.</param>
    internal CountdownTile(string key, WidgetItem island, Gfx gfx, Config config, Countdowns? countdowns, Func<DateTime>? now = null)
        : base(key, island, gfx)
    {
        kind = island.Info == WidgetInfos.Focus ? CountdownKind.Focus : CountdownKind.Timer;
        this.countdowns = countdowns ?? new Countdowns();
        this.now = now ?? (() => Clock.Now());
        Configure(island, config);
    }

    /// <summary>Where its countdown is kept: its widget's id, which it keeps wherever it is carried.</summary>
    internal string Id => Settings.Id;

    /// <summary>Which of the two it is.</summary>
    internal CountdownKind Kind => kind;

    /// <summary>What its own countdown is doing: what a menu reads its start button's word by (<see cref="Act"/>).</summary>
    internal CountdownPhase Phase => Mine.Phase;

    /// <summary>
    /// Its start button, pressed from outside the card — a folder's right-click, where the card is too
    /// small for its buttons: start, pause, take up again, or start again once it is over, at its own
    /// length, on its own countdown (<see cref="Countdowns.Act"/>).
    /// </summary>
    internal void Act()
    {
        countdowns.Act(Id, kind, Chosen, now());
        Redraw(Gfx);
    }

    /// <summary>The button that clears it, pressed from outside the card: its own countdown stopped, and back at its length.</summary>
    internal void Stop()
    {
        countdowns.Clear(Id);
        Redraw(Gfx);
    }

    /// <summary>
    /// Its plus, pressed from outside the card — a menu, for a line one row tall, which shows its start
    /// button and not its steppers: a minute longer (five from an hour up), for the next time it
    /// starts. Nothing while it runs, as its steppers are not there then; false where nothing changed.
    /// </summary>
    internal bool Longer() => Stepped(1);

    /// <summary>Its minus, pressed from outside the card: a minute shorter (five from an hour up), as <see cref="Longer"/>.</summary>
    internal bool Shorter() => Stepped(-1);

    /// <summary>
    /// Whether its minus and its plus would change its length now — what a menu shows them enabled by:
    /// neither while it runs, and not past the least or the most a length is.
    /// </summary>
    internal (bool Shorter, bool Longer) Steps =>
        Mine is { Phase: CountdownPhase.Running }
            ? (false, false)
            : (Countdown.StepMinutes(Chosen, -1) != Countdown.Clamp(Chosen), Countdown.StepMinutes(Chosen, 1) != Countdown.Clamp(Chosen));

    /// <summary>A step pressed from outside the card, where it may be pressed: whether the length changed.</summary>
    private bool Stepped(int direction)
    {
        if (Mine is { Phase: CountdownPhase.Running })
        {
            return false;
        }
        var was = Chosen;
        Step(direction);
        Redraw(Gfx);
        return Chosen != was;
    }

    /// <summary>Whether a stepper is being held down.</summary>
    private bool Holding => held is CountdownTargetKind.Less or CountdownTargetKind.More;

    /// <summary>
    /// How often this card wants the folder's tick.
    /// <list type="bullet">
    /// <item>A held stepper: the repeat, which is what makes setting 45 minutes bearable.</item>
    /// <item>A running countdown: a second, so the number on it falls.</item>
    /// <item>Otherwise nothing — an idle card costs the folder no timer at all.</item>
    /// </list>
    /// The folder reads this afresh on every arm (<c>FolderIsland.Interval</c>), so the card asks
    /// for a fast tick while it needs one and gives it back the moment it does not. No timer of
    /// its own: the folder already owns ticking while it is open, and a second one would keep
    /// running behind a closed folder.
    /// </summary>
    public override int IntervalMs =>
        Holding ? RepeatEveryMs
        : Mine is { Phase: CountdownPhase.Running } ? 1000
        : 0;

    protected override int Count => 1;

    public override void Configure(WidgetItem island, Config config)
    {
        // The shape is a function of the settings, so the shape must be given the
        // settings just handed over: keeping the ones this island was born with
        // left the size measured for the old options while the drawing used the
        // new ones (a folder whose name was switched on kept its square).
        Settings = island;
        _ = island;
        _ = config;
        Forget();
    }

    /// <summary>No hover tooltip: a folder's cards have none, and the card says everything.</summary>
    public override string? Tooltip(int index) => null;

    /// <summary>
    /// A press on one of the card's buttons: the steppers set the length (and remember it), the
    /// action button starts, pauses, takes up again or starts again, and the last one clears it.
    /// Nothing is asked of the dock — the folder redraws the card when the widget says it changed.
    /// </summary>
    public override IslandAction Click(int index)
    {
        // Beside the buttons, nothing: a folder opens no popup (docs\widgets.md §15).
        var now = this.now();
        switch ((CountdownTargetKind)index)
        {
            case CountdownTargetKind.Less:
                Step(-1);
                break;
            case CountdownTargetKind.More:
                Step(1);
                break;
            case CountdownTargetKind.Action:
                // The one rule, which its face's menu presses too.
                countdowns.Act(Id, kind, Chosen, now);
                break;
            case CountdownTargetKind.Clear:
                countdowns.Clear(Id);
                break;
            default:
                return default;
        }
        return default;
    }

    // folders: its menu (Dock.WidgetMenus.cs) presses the card's own buttons (Act, Stop, above), and
    // says what they say — the one seam the menu has into a countdown, so it never reaches for the
    // countdowns itself.

    /// <summary>What its own action button says now ("Start", "Pause", "Resume", "Start again"), and whether it has its clear button.</summary>
    internal (string Action, bool Clears) MenuButtons => CountdownCard.MenuButtons(Mine, kind);

    /// <summary>A minute changing means nothing here; a running countdown redraws on its own tick.</summary>
    public override bool MinuteChanged(Gfx gfx) => false;

    /// <summary>
    /// The folder's tick, which this card asked for (<see cref="IntervalMs"/>): a held stepper
    /// takes its next step.
    /// <para>
    /// This is where the repeat lives because a hold is a value changing on a tick, which is what
    /// Sample is for, and because the alternative is a timer of this card's own — running behind a
    /// closed folder, for a button nobody is holding.
    /// </para>
    /// </summary>
    protected override bool Sample(bool everything)
    {
        var now = Environment.TickCount64;
        if (Holding && onHeld)
        {
            // The first step happened on the press; the repeat starts after the hold and then runs
            // at its own pace, which the tick is already close to.
            if (now - heldAt >= HoldBeforeRepeatMs && now - steppedAt >= RepeatEveryMs)
            {
                steppedAt = now;
                Step(held == CountdownTargetKind.More ? 1 : -1);
            }
        }
        // The cell count never changes: this card is one cell whatever it is showing.
        return false;
    }

    /// <summary>
    /// The wheel over the card steps its length, up for more and down for less — the user asked
    /// for it beside the two buttons, and it is the same <see cref="Countdown.StepMinutes"/> rule
    /// they press.
    /// <para>
    /// <b>Anywhere on the card</b>, not only over the number: a wheel that worked over the number
    /// and nowhere else is a wheel people decide is broken. Nothing while it is counting: what the steppers set is the
    /// length the <i>next</i> run starts at, and the card does not show them then either.
    /// </para>
    /// </summary>
    protected internal override bool CardWheel(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface, int notches)
    {
        if (notches == 0 || Mine is { Phase: CountdownPhase.Running })
        {
            return false;
        }
        steppedAt = Environment.TickCount64;
        Step(Math.Sign(notches));
        Redraw(Gfx);
        return true;
    }

    /// <summary>
    /// A press on a stepper: it steps at once and then holds, so a click is one step and a hold
    /// runs. Only the two steppers take the pointer — the action and the clear buttons are plain
    /// clicks and stay <see cref="Click"/>'s, so a press on them can still become a move of the
    /// card when the folder is being arranged.
    /// </summary>
    protected internal override bool CardPress(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var at = (CountdownTargetKind)CardTarget(point, box, style, surface);
        if (at is not (CountdownTargetKind.Less or CountdownTargetKind.More))
        {
            return false;
        }
        held = hot = at;
        onHeld = true;
        heldAt = steppedAt = Environment.TickCount64;
        Step(at == CountdownTargetKind.More ? 1 : -1);
        Redraw(Gfx);
        return true;
    }

    /// <summary>
    /// The pointer moving while a stepper is held: off the button it stops repeating and the
    /// button goes out, and back on it starts again — which is how a button behaves everywhere,
    /// and it is the way out of a repeat somebody did not mean to start.
    /// </summary>
    protected internal override void CardDrag(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        if (!Holding)
        {
            return;
        }
        var on = (CountdownTargetKind)CardTarget(point, box, style, surface) == held;
        if (on == onHeld)
        {
            return;
        }
        onHeld = on;
        hot = on ? held : CountdownTargetKind.None;
        // Coming back on does not step at once: the hold it left is the hold it returns to.
        steppedAt = Environment.TickCount64;
        Redraw(Gfx);
    }

    /// <summary>Letting go: the repeat stops, and the dock writes the length once it has settled.</summary>
    protected internal override void CardRelease()
    {
        if (!Holding)
        {
            return;
        }
        held = CountdownTargetKind.None;
        onHeld = false;
        Redraw(Gfx);
    }

    /// <summary>
    /// The button under the pointer lights, as a button does. The folder draws the card it has
    /// just told (<c>FolderFlyout.HoverOver</c>), so this only has to remember where the pointer
    /// is — asking for a redraw of a whole folder per row boundary is the cost that rule avoids.
    /// </summary>
    protected internal override void CardHover(Vector2? point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var at = point is { } where
            ? (CountdownTargetKind)CardTarget(where, box, style, surface)
            : CountdownTargetKind.None;
        if (at == hot)
        {
            return;
        }
        hot = at;
        Redraw(Gfx);
    }

    /// <summary>A stepper held when the card goes away is let go; the length it set is the dock's to write.</summary>
    public override void Dispose()
    {
        held = CountdownTargetKind.None;
        base.Dispose();
    }

    /// <summary>A line of a folder's list, never a widget of a bar: its face on the bar is its folder's (CountdownFaceDrawing).</summary>
    internal override void PaintCell(ID2D1DeviceContext* dc, int index, in IslandStyle style, float width, float height) =>
        throw new NotSupportedException($"{Info.Id} is a line of a folder's list, not a widget of a bar.");

    /// <summary>The line: the kit's row of its glyph, its name, the time and its buttons (<see cref="CountdownLine"/>), at the line's top.</summary>
    protected internal override void PaintCard(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, WidgetSurface surface) =>
        CountdownLine.Draw(new LineSketch(dc, Gfx), box, style, Inputs, hot, Holding && onHeld);

    /// <summary>The row laid out in <paramref name="box"/> with what it says now, drawing nothing.</summary>
    internal CountdownLinePlan Plan(D2D_RECT_F box, in TileStyle style) => CountdownLine.Draw(new LineSketch(null, Gfx), box, style, Inputs);

    /// <summary>Which of the card's buttons is at <paramref name="point"/>; 0 (None) for the rest of it.</summary>
    protected internal override int CardTarget(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        foreach (var target in Plan(box, style).Targets)
        {
            if (LineRows.Inside(target.Box, point))
            {
                return (int)target.Kind;
            }
        }
        return (int)CountdownTargetKind.None;
    }

    /// <summary>A line of a list where nobody set its height: its one row (<see cref="CountdownLine.Height"/>).</summary>
    public float ListHeight(float width, in TileStyle style) => CountdownLine.Height(style);

    /// <summary>What a line of a list shows first, on one row as at every height: its time.</summary>
    public string MainPiece => "time";

    /// <summary>
    /// What its line lays out in <paramref name="box"/> (LineSketch's ILinePieces), in the line's own
    /// px: its glyph, its name, its time and its buttons, where the row put them.
    /// </summary>
    public IReadOnlyList<LinePiece> LinePieces(D2D_RECT_F box, in TileStyle style)
    {
        var sketch = new LineSketch(null, Gfx);
        CountdownLine.Draw(sketch, box, style, Inputs);
        return [.. sketch.Pieces.Where(piece => piece.Name.Length > 0 && !piece.Name.EndsWith(" plate", StringComparison.Ordinal) && piece.Name != "meter")];
    }

    /// <summary>What this card shows now: its kind, its countdown, the length it is set to and its name.</summary>
    private CountdownInputs Inputs => new(kind, Mine, Chosen, now(), Called);

    /// <summary>Its own countdown: idle while it is not set.</summary>
    private Countdown Mine => countdowns.Of(Id);

    /// <summary>The length a timer or a focus session <paramref name="item"/> starts at: its own, or its kind's where it has none.</summary>
    internal static TimeSpan LengthOf(WidgetItem item, CountdownKind kind) =>
        CountdownSource.Minutes(item.Countdown?.Minutes, kind == CountdownKind.Focus ? CountdownOptions.FocusMinutes : CountdownOptions.TimerMinutes);

    /// <summary>Its own options: its length and its name.</summary>
    private CountdownOptions? Options => Settings.Countdown;

    /// <summary>The length it starts at: its widget's own, or its kind's where it has none.</summary>
    private TimeSpan Chosen => LengthOf(Settings, kind);

    /// <summary>The name it was given, or empty for its kind's.</summary>
    internal string Called => Options?.Name?.Trim() ?? "";

    /// <summary>
    /// A minute on or back (five from an hour up), set in its widget's options for the next time.
    /// <para>
    /// It sets the length and does <b>not</b> write it. A held stepper steps nine times a second
    /// and a wheel spin more than that, so a write per step is a write per frame of somebody's
    /// thumb: it tells the dock (<see cref="Countdowns.Remember"/>), which writes the config once
    /// the steps have settled.
    /// </para>
    /// </summary>
    private void Step(int direction)
    {
        // Its options are made where nobody had set them: this is the first length somebody chose.
        var minutes = CountdownSource.Whole(Countdown.StepMinutes(Chosen, direction));
        (Settings.Countdown ??= new CountdownOptions()).Minutes = minutes;
        countdowns.Remember(Id);
    }
}

/// <summary>A countdown's length as its widget's options keep it, in whole minutes, and back.</summary>
internal static class CountdownSource
{
    /// <summary><paramref name="minutes"/> held to what a timer may be, or <paramref name="fallback"/> where none are set.</summary>
    internal static TimeSpan Minutes(int? minutes, int fallback) => TimeSpan.FromMinutes(CountdownOptions.Clamp(minutes, fallback));

    internal static int Whole(TimeSpan length) => (int)Math.Round(Countdown.Clamp(length).TotalMinutes);
}
