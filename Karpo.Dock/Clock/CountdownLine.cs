// A timer or a focus session as a line of a list folder: the kit's one Row (docs\widgets.md §401; the person,
// "for the time widget flyout make the focus time and timer one liners", 2026-10-04, and the style proposal's
// "Timer, Focus and Stopwatch become the same Row"). It is the line at every height: a taller line has the row
// at its top and nothing larger.
//
// The row reads left to right: the kind's glyph in the list's leading column, its name where every name begins,
// the time in the kit's Value — the length set ("25 min") or what is left ("24:13") — against its buttons, and
// the buttons at the trailing gutter: a subtle minus and plus, and the accent button that starts, pauses, takes
// up again and starts again, the line's one main action. While it runs the minus's place is empty, the plus's
// holds the subtle button that stops it, and the kit's meter under the words shows how much is left — in the
// accent while it counts, in the quieter ink while it is paused.
//
// Nothing moves as it runs: the buttons stand in the same three places whatever it is doing, and the name's
// room is reckoned from the widest the time can say, so it is cut the same way at rest and running. Where the
// name would not keep four of its letters beside the steppers, the steppers fold into the line's menu
// (CountdownTile.Longer and Shorter) and the start button stands alone.
//
// One pass lays it out, draws it and writes down where each piece stood (LineSketch, LineRow): what the
// pointer finds is where it was drawn.

using Karpo.Dock.QuickSettings.Controls;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock;

/// <summary>A countdown's row laid out with what it says now (<see cref="CountdownLine.Draw"/>), in the line's own px.</summary>
internal sealed record CountdownLinePlan
{
    public required CountdownKind Kind { get; init; }

    /// <summary>Whether its own countdown is set: running, paused or over and not yet cleared.</summary>
    public required bool Running { get; init; }

    public required CountdownPhase Phase { get; init; }

    /// <summary>Where the row's pieces stood.</summary>
    public required LineRow.Laid Row { get; init; }

    /// <summary>The time as the row says it: the length set with its unit, or what is left.</summary>
    public required string ValueText { get; init; }

    /// <summary>What the start button says (for its menu and for Narrator) and shows.</summary>
    public required string ActionText { get; init; }

    public required string ActionGlyph { get; init; }

    /// <summary>The steppers, the stop button and the start button; empty where not drawn.</summary>
    public required D2D_RECT_F Less { get; init; }

    public required D2D_RECT_F More { get; init; }

    public required D2D_RECT_F Clear { get; init; }

    public required D2D_RECT_F Action { get; init; }

    /// <summary>The share of the length that is left, 0 to 1, while it runs or is paused; null when no meter is drawn.</summary>
    public required float? Left { get; init; }

    /// <summary>Everything the pointer acts on, left to right.</summary>
    public required IReadOnlyList<CountdownTarget> Targets { get; init; }
}

internal static class CountdownLine
{
    /// <summary>Segoe Fluent Icons' Stop, for the button that stops a countdown.</summary>
    public const string StopGlyph = "";

    /// <summary>The unit after the length set.</summary>
    private static string Unit => S.DockClockMinutesUnit;

    /// <summary>The names a line of <paramref name="kind"/> goes by, fullest first, or the one its widget was given.</summary>
    public static string[] NamesOf(CountdownKind kind, string given) =>
        given.Trim() is { Length: > 0 } called ? [called]
        : kind == CountdownKind.Focus ? [CountdownCard.Title(kind), S.DockClockFocus]
        : [CountdownCard.Title(kind)];

    /// <summary>How tall the line is: one of the list's rows, whatever it is doing.</summary>
    public static float Height(in TileStyle style) => LineRow.Height(style, caption: false);

    /// <summary>
    /// The row of <paramref name="inputs"/> at the top of <paramref name="box"/>, drawn on <paramref name="sketch"/>
    /// (laid out only where it has no device): <paramref name="hot"/> is the button under the pointer, held
    /// down with <paramref name="pressed"/>.
    /// </summary>
    public static CountdownLinePlan Draw(LineSketch sketch, D2D_RECT_F box, in TileStyle style, CountdownInputs inputs,
        CountdownTargetKind hot = CountdownTargetKind.None, bool pressed = false)
    {
        var gfx = sketch.Gfx;
        var form = ListForm.Of(gfx, style);
        var buttons = LineButton.Of(gfx, style);
        var kind = inputs.Kind;
        var state = inputs.State;
        var running = !state.Idle && state.Kind == kind;
        var left = state.Phase == CountdownPhase.Done ? TimeSpan.Zero : state.Remaining(inputs.Now);
        var valuePx = style.Text(LineType.ValueDip);
        var text = running ? Countdown.Span(left) : $"{Countdown.MinutesOf(inputs.Length)} {Unit}";
        var height = MathF.Min(TilePaint.Height(box), Height(style));
        var gap = style.Px(LineSpace.GapDip);

        // The widest the time can say, so the name's room does not move as it runs.
        var hours = inputs.Length >= TimeSpan.FromHours(1) || (running && state.Length >= TimeSpan.FromHours(1));
        var hundreds = inputs.Length >= TimeSpan.FromMinutes(100);
        var widest = MathF.Max(
            PopupPaint.Measure(gfx, hours ? "88:88:88" : "88:88", valuePx, Weight.Semibold).X,
            PopupPaint.Measure(gfx, $"{(hundreds ? "888" : "88")} {Unit}", valuePx, Weight.Semibold).X);
        var names = NamesOf(kind, inputs.Name);
        float NameRoom(int squares) =>
            TilePaint.Width(box) - form.TextLeft - form.Inset - (squares * buttons.Height) - (squares * gap) - widest - gap;
        var steppers = ListTiers.Name(gfx, names, form.TextPx, Weight.Regular, NameRoom(3)) is not null;
        var name = ListTiers.Name(gfx, names, form.TextPx, Weight.Regular, NameRoom(steppers ? 3 : 1)) ?? "";

        var action = CountdownCard.Action(state, kind, words: true);
        RowControl Glyph(string piece, string glyph, ButtonStyle look, CountdownTargetKind part) =>
            RowControl.Button(gfx, buttons, piece, "", glyph, look, hot == part, pressed && hot == part);
        var controls = new List<RowControl>();
        if (steppers)
        {
            controls.Add(running ? RowControl.Room("", buttons.Height, buttons.Height) : Glyph("minus", CountdownCard.MinusGlyph, ButtonStyle.Subtle, CountdownTargetKind.Less));
            controls.Add(running ? Glyph("clear", StopGlyph, ButtonStyle.Subtle, CountdownTargetKind.Clear) : Glyph("plus", CountdownCard.PlusGlyph, ButtonStyle.Subtle, CountdownTargetKind.More));
        }
        controls.Add(Glyph("start", action.Glyph, ButtonStyle.Accent, CountdownTargetKind.Action));

        var row = LineRow.Draw(sketch, box.left, box.right, box.top, style, new RowSpec(name)
        {
            Glyph = CountdownCard.GlyphOf(kind),
            Reading = text,
            ReadingInk = running && state.Phase == CountdownPhase.Paused ? style.Theme.TextSecondary : null,
            Controls = controls,
            ReadingPiece = "time",
        }, height);

        D2D_RECT_F At(int index) => steppers ? row.Controls[index] : default;
        var less = running ? default : At(0);
        var more = running ? default : At(1);
        var clear = running ? At(1) : default;
        var start = row.Controls[^1];

        // What is left, as the kit's meter under the words, a button's gap short of the buttons (§449).
        float? share = running && state.Phase is CountdownPhase.Running or CountdownPhase.Paused && state.Length > TimeSpan.Zero
            ? (float)Math.Clamp(left.TotalSeconds / state.Length.TotalSeconds, 0, 1)
            : null;
        if (share is { } shown)
        {
            var fill = state.Phase == CountdownPhase.Paused ? style.Theme.TextSecondary : TilePaint.Accent(style);
            LineMeter.Foot(sketch, row, box.left, box.right, style, shown, fill);
        }

        var targets = new List<CountdownTarget>();
        void Add(CountdownTargetKind part, D2D_RECT_F at)
        {
            if (at.right > at.left)
            {
                targets.Add(new CountdownTarget(part, part == CountdownTargetKind.Value ? at : buttons.Hit(at, row.Top, row.Bottom)));
            }
        }
        Add(CountdownTargetKind.Less, less);
        if (!running && row.Reading is { } reading)
        {
            // The wheel over the time sets the length.
            Add(CountdownTargetKind.Value, reading);
        }
        Add(CountdownTargetKind.More, more);
        Add(CountdownTargetKind.Clear, clear);
        Add(CountdownTargetKind.Action, start);

        return new CountdownLinePlan
        {
            Kind = kind,
            Running = running,
            Phase = running ? state.Phase : CountdownPhase.Idle,
            Row = row,
            ValueText = text,
            ActionText = action.Text,
            ActionGlyph = action.Glyph,
            Less = less,
            More = more,
            Clear = clear,
            Action = start,
            Left = share,
            Targets = targets,
        };
    }
}
