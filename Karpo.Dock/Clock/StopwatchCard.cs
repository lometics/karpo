// The stopwatch as a line of a list folder: the kit's one Row, as the timer's (CountdownLine; docs\widgets.md
// §401, the style proposal's "Timer, Focus and Stopwatch become the same Row").
//
// The row reads as a countdown's does: the glyph in the list's leading column, the name where every name begins,
// the time in tenths in the kit's Value — "0:00.0" — against its buttons, and the buttons at the trailing gutter:
// a subtle lap and reset, and the accent one that starts, pauses and takes it up again. The lap and reset are
// always there, greyed where they do nothing (no lap while it is paused, nothing to reset before it starts), as
// Windows' own Clock has them.
//
// A taller line lists its laps under the row, newest first, a denser row of the kit's each (32 DIP): "Lap 3 ·
// 1:02.5" — the lap and where it ended — and how long the lap was against the trailing gutter. A taller line shows more laps,
// never larger figures. A line of one row has no room for them, so once a lap is taken the lap under way stands
// in its name's place in the quieter ink: the lap button shows what it did at every height (the person,
// 2026-10-11).
//
// Nothing moves as it counts: the buttons stand where they stand whatever it reads, and the name's room is
// reckoned from the widest the time can say (StopwatchText.Template). Where the name would not keep four of its
// letters, the lap and reset fold into the line's menu and the start button stands alone.

using Karpo.Core;
using Karpo.Dock.QuickSettings.Controls;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock;

internal enum StopwatchTargetKind
{
    None,

    /// <summary>A lap ends: while it runs.</summary>
    Lap,

    /// <summary>Back to nought, laps and all: once it has started.</summary>
    Reset,

    /// <summary>Start, pause, take up again.</summary>
    Action,
}

/// <param name="Box">In the line's own px.</param>
/// <param name="Enabled">Whether a press does anything now: a greyed button is drawn but takes nothing.</param>
internal readonly record struct StopwatchTarget(StopwatchTargetKind Kind, D2D_RECT_F Box, bool Enabled);

/// <summary>What a stopwatch's face shows: its state now, at <paramref name="Now"/> on <see cref="StopwatchClock"/>.</summary>
/// <param name="Separator">The region's decimal sign, before the tenth.</param>
internal readonly record struct StopwatchInputs(StopwatchState State, TimeSpan Now, string Separator = ".");

/// <summary>A lap as a face writes it: its name, how long it was, and where it ended.</summary>
internal readonly record struct StopwatchLapText(string Name, string Length, string Total);

/// <summary>A stopwatch's line with what it says now (<see cref="StopwatchCard.Draw"/>), in the line's own px.</summary>
internal sealed record StopwatchLinePlan
{
    public required StopwatchPhase Phase { get; init; }

    /// <summary>Where the row's pieces stood.</summary>
    public required LineRow.Laid Row { get; init; }

    public required string ValueText { get; init; }
    public required string ActionText { get; init; }
    public required string ActionGlyph { get; init; }

    /// <summary>How many lap rows the line has under its row, and the laps it lists in them, newest first.</summary>
    public required int LapRows { get; init; }

    public required IReadOnlyList<StopwatchLapText> Laps { get; init; }

    /// <summary>Where the last lap's row ends; the row's foot where it lists none.</summary>
    public required float Foot { get; init; }

    /// <summary>Its buttons, left to right, greyed ones too: the pointer takes only the enabled.</summary>
    public required IReadOnlyList<StopwatchTarget> Targets { get; init; }
}

internal static class StopwatchCard
{
    // Segoe Fluent Icons: a flag for a lap, and the arrow back for reset, as Windows' Clock has them.
    public const string LapGlyph = "";
    public const string ResetGlyph = "";

    /// <summary>The names a stopwatch's line goes by.</summary>
    public static IReadOnlyList<string> Names => [S.DockStopwatchTitle];

    /// <summary>
    /// What the start button says and shows: Start while idle, Pause while it runs, Resume while it
    /// is paused — the countdown's words and glyphs.
    /// </summary>
    public static (string Text, string Glyph) Action(StopwatchState state) => state.Phase switch
    {
        StopwatchPhase.Running => (S.DockClockPause, CountdownCard.PauseGlyph),
        StopwatchPhase.Paused => (S.DockClockResume, CountdownCard.PlayGlyph),
        _ => (S.DockClockStart, CountdownCard.PlayGlyph),
    };

    /// <summary>The laps of <paramref name="state"/>, newest first, at most <paramref name="count"/>, as a face writes them.</summary>
    public static IReadOnlyList<StopwatchLapText> LapsOf(StopwatchState state, int count, string separator)
    {
        var laps = new List<StopwatchLapText>();
        for (var index = state.Splits.Length - 1; index >= 0 && laps.Count < count; index--)
        {
            var (length, total) = state.LapAt(index)!.Value;
            laps.Add(new StopwatchLapText(S.DockStopwatchLapNumber(index + 1), StopwatchText.Tenths(length, separator), StopwatchText.Tenths(total, separator)));
        }
        return laps;
    }

    /// <summary>How tall its row is: one of the list's rows.</summary>
    public static float RowHeight(in TileStyle style) => LineRow.Height(style, caption: false);

    /// <summary>How tall a lap's row is: a list's denser row, 32 DIP, so a line of two rows lists one.</summary>
    public static float LapHeight(in TileStyle style) => PopupPaint.Round(LapDip * style.Scale * style.TextScale);

    private const float LapDip = 32;

    /// <summary>How many laps' rows a line <paramref name="height"/> px tall has under its row.</summary>
    public static int LapRowsIn(float height, in TileStyle style) =>
        Math.Max(0, (int)MathF.Floor((height - RowHeight(style) + 0.5f) / LapHeight(style)));

    /// <summary>
    /// The row of <paramref name="inputs"/> at the top of <paramref name="box"/> and its laps under it, drawn on
    /// <paramref name="sketch"/> (laid out only where it has no device); <paramref name="hot"/> is the button
    /// under the pointer.
    /// </summary>
    public static StopwatchLinePlan Draw(LineSketch sketch, D2D_RECT_F box, in TileStyle style, StopwatchInputs inputs,
        StopwatchTargetKind hot = StopwatchTargetKind.None)
    {
        var gfx = sketch.Gfx;
        var form = ListForm.Of(gfx, style);
        var buttons = LineButton.Of(gfx, style);
        var theme = style.Theme;
        var state = inputs.State;
        var gap = style.Px(LineSpace.GapDip);
        var valuePx = style.Text(LineType.ValueDip);
        var text = StopwatchText.Tenths(state.Elapsed(inputs.Now), inputs.Separator);
        var height = TilePaint.Height(box);
        var row = MathF.Min(height, RowHeight(style));
        var lapRows = LapRowsIn(height, style);

        var widest = PopupPaint.Measure(gfx, StopwatchText.Template(inputs.Separator), valuePx, Weight.Semibold).X;
        float NameRoom(int squares) =>
            TilePaint.Width(box) - form.TextLeft - form.Inset - (squares * buttons.Height) - (squares * gap) - widest - gap;
        DXGI_RGBA? nameInk = null;
        bool both;
        string name;
        // One row and a lap taken: the lap under way in the name's place, in the quieter ink, whole — its room
        // judged by the widest its shape can say, so it does not come and go as it counts.
        if (lapRows == 0 && state.Splits.Length > 0)
        {
            var lap = StopwatchText.Tenths(state.CurrentLap(inputs.Now), inputs.Separator);
            var shape = PopupPaint.Measure(gfx, string.Concat(lap.Select(c => char.IsAsciiDigit(c) ? '8' : c)), form.TextPx, Weight.Regular).X;
            both = shape <= NameRoom(3);
            name = both || shape <= NameRoom(1) ? lap : "";
            nameInk = theme.TextSecondary;
        }
        else
        {
            both = ListTiers.Name(gfx, Names, form.TextPx, Weight.Regular, NameRoom(3)) is not null;
            name = ListTiers.Name(gfx, Names, form.TextPx, Weight.Regular, NameRoom(both ? 3 : 1)) ?? "";
        }

        var (actionText, actionGlyph) = Action(state);
        RowControl Glyph(string piece, string glyph, ButtonStyle look, StopwatchTargetKind part, bool enabled) =>
            RowControl.Button(gfx, buttons, piece, "", glyph, look, enabled && hot == part, enabled: enabled);
        var controls = new List<RowControl>();
        if (both)
        {
            controls.Add(Glyph("lap", LapGlyph, ButtonStyle.Subtle, StopwatchTargetKind.Lap, state.CanLap));
            controls.Add(Glyph("reset", ResetGlyph, ButtonStyle.Subtle, StopwatchTargetKind.Reset, state.CanReset));
        }
        controls.Add(Glyph("start", actionGlyph, ButtonStyle.Accent, StopwatchTargetKind.Action, true));

        var laid = LineRow.Draw(sketch, box.left, box.right, box.top, style, new RowSpec(name)
        {
            Glyph = WidgetInfos.Stopwatch.Glyph,
            NameInk = nameInk,
            Reading = text,
            ReadingInk = state.Phase == StopwatchPhase.Paused ? theme.TextSecondary : null,
            Controls = controls,
            NamePiece = nameInk is null ? "name" : "split",
            ReadingPiece = "time",
        }, row);

        // The laps, newest first, a row each; "No laps yet" where it has rows for them and none.
        var laps = LapsOf(state, lapRows, inputs.Separator);
        var y = laid.Bottom;
        if (lapRows > 0 && laps.Count == 0)
        {
            LineRow.Draw(sketch, box.left, box.right, y, style, new RowSpec(S.DockStopwatchNoLaps) { NameInk = theme.TextSecondary, NamePiece = "no laps" }, LapHeight(style));
        }
        foreach (var lap in laps)
        {
            LineRow.Draw(sketch, box.left, box.right, y, style, new RowSpec(LineRow.Join(lap.Name, lap.Total))
            {
                NameInk = theme.TextSecondary,
                Reading = lap.Length,
                Look = RowReading.Body,
                NamePiece = "laps",
                ReadingPiece = "lap length",
            }, LapHeight(style));
            y += LapHeight(style);
        }

        var targets = new List<StopwatchTarget>();
        if (both)
        {
            targets.Add(new StopwatchTarget(StopwatchTargetKind.Lap, laid.Controls[0], state.CanLap));
            targets.Add(new StopwatchTarget(StopwatchTargetKind.Reset, laid.Controls[1], state.CanReset));
        }
        targets.Add(new StopwatchTarget(StopwatchTargetKind.Action, laid.Controls[^1], true));
        return new StopwatchLinePlan
        {
            Phase = state.Phase,
            Row = laid,
            ValueText = text,
            ActionText = actionText,
            ActionGlyph = actionGlyph,
            LapRows = lapRows,
            Laps = laps,
            Foot = y,
            Targets = targets,
        };
    }

    /// <summary>
    /// The enabled target at <paramref name="point"/>, or None: a greyed button takes nothing. With
    /// <paramref name="reach"/>, a button takes the hit area the lines' buttons keep (LineButton.Hit).
    /// </summary>
    public static StopwatchTargetKind TargetAt(IReadOnlyList<StopwatchTarget> targets, System.Numerics.Vector2 point, LineButton? reach = null)
    {
        foreach (var target in targets)
        {
            var box = reach is { } sizes ? sizes.Hit(target.Box) : target.Box;
            if (target.Enabled && box.right > box.left && point.X >= box.left && point.X < box.right && point.Y >= box.top && point.Y < box.bottom)
            {
                return target.Kind;
            }
        }
        return StopwatchTargetKind.None;
    }
}
