// The stopwatch widget, a line of a folder's list, built as the timer is (CountdownTile) and drawn
// in its style.
//
// It was an entry of a taskbar too, its glyph and what it had counted in whole seconds, whose click
// opened its own panel. That became the stopwatch preset (2026-10-10, docs\widgets.md §92): a
// folder holding this line with room for its laps, under a face that says what the entry said
// (FaceDrawings, CountdownFaceDrawing), from this line's own stopwatch.
//
// It is the kit's row (Clock\StopwatchCard.cs, docs\widgets.md §401): the glyph, the name, the time in tenths
// in the Value and its lap, reset and start buttons on one row; its laps under that where the line is taller,
// and where it is not, the lap under way in its name's place once a lap is taken. The tenths tick only while
// the folder is open and the stopwatch runs, on the folder's quick tick, which draws this line alone again and
// nothing else of the folder (FolderFlyout.ArmQuick).
//
// What it is doing is kept in the dock's store under the widget's id (Stopwatches), which every face
// of it reads, so a start from its folder's face is a start on its line, and it runs on while
// nothing shows it.
// A folder's right-click works it through Act, Lap and Reset, as a timer's works its buttons.

using System.Numerics;
using Karpo.Core;
using Karpo.Dock.Native;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock;

internal sealed unsafe class StopwatchTile : CellIsland, IListContent, ILinePieces
{
    private readonly Stopwatches stopwatches;
    private readonly Func<TimeSpan> now;
    private readonly Func<string> separator;

    /// <summary>The button under the pointer.</summary>
    private StopwatchTargetKind hot;

    public StopwatchTile(string key, WidgetItem island, Gfx gfx, Config config)
        : this(key, island, gfx, config, null)
    {
    }

    /// <param name="countdowns">The dock's countdowns, whose stopwatches it runs on; null for a store of its own.</param>
    /// <param name="now">The monotonic clock; <see cref="StopwatchClock.Now"/> when null.</param>
    /// <param name="separator">The decimal sign; the region's when null.</param>
    internal StopwatchTile(string key, WidgetItem island, Gfx gfx, Config config, Countdowns? countdowns, Func<TimeSpan>? now = null, Func<string>? separator = null)
        : base(key, island, gfx)
    {
        stopwatches = (countdowns ?? new Countdowns()).Stopwatches;
        this.now = now ?? StopwatchClock.Now;
        this.separator = separator ?? Panels.Nls.DecimalSeparator;
        Configure(island, config);
    }

    /// <summary>Where its stopwatch is kept: its widget's id, which it keeps wherever it is carried.</summary>
    internal string Id => Settings.Id;

    /// <summary>What its stopwatch is doing.</summary>
    internal StopwatchState State => stopwatches.Of(Id);

    /// <summary>Whether it counts now: the open folder's quick tick then draws its tenths.</summary>
    internal bool Counting => State.Running;

    /// <summary>Its start button, pressed from outside it — a folder's right-click: start, pause, take up again.</summary>
    internal void Act()
    {
        stopwatches.Act(Id, now());
        Redraw(Gfx);
    }

    /// <summary>Its lap button, pressed from outside it: nothing unless it runs.</summary>
    internal void Lap()
    {
        stopwatches.Lap(Id, now());
        Redraw(Gfx);
    }

    /// <summary>Its reset button, pressed from outside it.</summary>
    internal void Reset()
    {
        stopwatches.Reset(Id);
        Redraw(Gfx);
    }

    /// <summary>What its start button says now, and whether its lap and reset would do anything: what its menu shows.</summary>
    internal (string Action, bool Laps, bool Resets) MenuButtons =>
        (StopwatchCard.Action(State).Text, State.CanLap, State.CanReset);

    /// <summary>None: its tenths are the open folder's quick tick (<see cref="Counting"/>), and its face's seconds the dock's.</summary>
    public override int IntervalMs => 0;

    protected override int Count => 1;

    public override void Configure(WidgetItem island, Config config)
    {
        Settings = island;
        _ = config;
        Forget();
    }

    /// <summary>No hover tooltip, as a timer has none: its line says everything.</summary>
    public override string? Tooltip(int index) => null;

    /// <summary>A minute changing means nothing here.</summary>
    public override bool MinuteChanged(Gfx gfx) => false;

    protected override bool Sample(bool everything) => false;

    /// <summary>
    /// A press: its lap, reset and start buttons do what they say, and a click beside them nothing.
    /// </summary>
    public override IslandAction Click(int index)
    {
        switch ((StopwatchTargetKind)index)
        {
            case StopwatchTargetKind.Lap:
                stopwatches.Lap(Id, now());
                break;
            case StopwatchTargetKind.Reset:
                stopwatches.Reset(Id);
                break;
            case StopwatchTargetKind.Action:
                stopwatches.Act(Id, now());
                break;
        }
        return default;
    }

    /// <summary>The button under the pointer lights; the folder draws the line it told.</summary>
    protected internal override void CardHover(Vector2? point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var at = point is { } where ? (StopwatchTargetKind)CardTarget(where, box, style, surface) : StopwatchTargetKind.None;
        if (at == hot)
        {
            return;
        }
        hot = at;
        Redraw(Gfx);
    }

    /// <summary>A line of a folder's list, never a widget of a bar: its face on the bar is its folder's (CountdownFaceDrawing).</summary>
    internal override void PaintCell(ID2D1DeviceContext* dc, int index, in IslandStyle style, float width, float height) =>
        throw new NotSupportedException($"{Info.Id} is a line of a folder's list, not a widget of a bar.");

    // --- In a list ------------------------------------------------------------------------

    /// <summary>Its line laid out in <paramref name="box"/> with what it reads now, in the line's own px, drawing nothing.</summary>
    internal StopwatchLinePlan Plan(D2D_RECT_F box, in TileStyle style) =>
        StopwatchCard.Draw(new LineSketch(null, Gfx), box, style, new StopwatchInputs(State, now(), separator()));

    /// <summary>The kit's row at the line's top, and its laps under it where the line is taller (<see cref="StopwatchCard"/>).</summary>
    protected internal override void PaintCard(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, WidgetSurface surface) =>
        StopwatchCard.Draw(new LineSketch(dc, Gfx), box, style, new StopwatchInputs(State, now(), separator()), hot);

    /// <summary>Which of its buttons is at <paramref name="point"/>, enabled; 0 (None) for the rest of it.</summary>
    protected internal override int CardTarget(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var plan = Plan(box, style);
        var buttons = LineButton.Of(Gfx, style);
        foreach (var target in plan.Targets)
        {
            if (target.Enabled && LineRows.Inside(buttons.Hit(target.Box, plan.Row.Top, plan.Row.Bottom), point))
            {
                return (int)target.Kind;
            }
        }
        return (int)StopwatchTargetKind.None;
    }

    /// <summary>
    /// list: as tall as what it draws at the most its line may be (IListContent, §388): its row while it has
    /// no laps, else its row and as many of its laps as its most holds, a lap's row each.
    /// </summary>
    public float ListHeight(float width, in TileStyle style)
    {
        var row = StopwatchCard.RowHeight(style);
        var laps = State.Laps.IsDefault ? 0 : State.Laps.Length;
        var fit = StopwatchCard.LapRowsIn(LineRows.Most(Settings, style), style);
        return MathF.Max(LineRows.Px(1, style), row + (Math.Min(laps, fit) * StopwatchCard.LapHeight(style)));
    }

    public string MainPiece => "time";

    public IReadOnlyList<LinePiece> LinePieces(D2D_RECT_F box, in TileStyle style)
    {
        var sketch = new LineSketch(null, Gfx);
        StopwatchCard.Draw(sketch, box, style, new StopwatchInputs(State, now(), separator()));
        return [.. sketch.Pieces.Where(piece => !piece.Name.EndsWith(" plate", StringComparison.Ordinal))];
    }
}
