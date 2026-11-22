// The timer's and the focus session's shared words and answers: what a countdown's buttons are, what its start
// button says and shows for what it is doing, its name and its glyph, and how a stepper held down repeats. The
// line itself is the kit's row (CountdownLine, docs\widgets.md §401); the card that was drawn here — its own
// layouts for every room, from a glyph alone to the whole widget — went with it.

using System.Collections.Concurrent;
using Karpo.Core;
using Karpo.Dock.Native;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock;


internal enum CountdownTargetKind
{
    None,

    /// <summary>A minute less, and a minute more (five from an hour up).</summary>
    Less,
    More,

    /// <summary>Start, pause, take up again, start again — on a line too small for a button, the line itself.</summary>
    Action,

    /// <summary>Clear a running or finished countdown.</summary>
    Clear,

    /// <summary>The number itself: the wheel over it steps the length.</summary>
    Value,
}

/// <param name="Box">The line's own px.</param>
internal readonly record struct CountdownTarget(CountdownTargetKind Kind, D2D_RECT_F Box);

/// <summary>What a countdown's line shows: the input of <see cref="CountdownLine.Draw"/>.</summary>
/// <param name="Kind">Timer or focus time: which kind of countdown this card is.</param>
/// <param name="State">What its own countdown is doing now (<see cref="Countdowns.Of"/>): idle while it is not set.</param>
/// <param name="Length">What this card's steppers are set to: its widget's length.</param>
/// <param name="Name">
/// The name its widget was given ("Tea"), which the card's label says where it fits whole; empty,
/// or where it does not fit, the kind's own ("Timer", "Focus time").
/// </param>
internal readonly record struct CountdownInputs(CountdownKind Kind, Countdown State, TimeSpan Length, DateTime Now, string Name = "");

internal static class CountdownCard
{
    /// <summary>
    /// How long a stepper is held before it starts repeating, in ms; then how often it repeats, about
    /// nine steps a second; and how long after the last step a length is written. One feel for a
    /// stepper wherever it is: a line of a list folder (CountdownTile).
    /// </summary>
    public const int HoldBeforeRepeatMs = 400;

    public const int RepeatEveryMs = 110;

    public const int SettleMs = 600;

    // Segoe Fluent Icons, as the rest of Karpo's buttons use them.
    public const string PlayGlyph = "";
    public const string PauseGlyph = "";
    public const string AgainGlyph = "";
    public const string ClearGlyph = "";
    public const string MinusGlyph = "";
    public const string PlusGlyph = "";

    /// <summary>
    /// Every word the action button can say, with its glyph. The room kept for it is measured over
    /// all of them, so the button's word changes inside room that never changes.
    /// </summary>
    private static (string Text, string Glyph)[] Actions =>
    [
        (S.DockClockStart, PlayGlyph),
        (S.DockClockPause, PauseGlyph),
        (S.DockClockResume, PlayGlyph),
        (S.DockClockStartAgain, AgainGlyph),
    ];

    /// <summary>The name a card of <paramref name="kind"/> goes by.</summary>
    public static string Title(CountdownKind kind) => kind == CountdownKind.Focus ? S.DockClockFocusTime : S.DockClockTimer;

    /// <summary>The glyph a card of <paramref name="kind"/> is labelled with: the catalogue's own.</summary>
    public static string GlyphOf(CountdownKind kind) => kind == CountdownKind.Focus ? WidgetInfos.Focus.Glyph : WidgetInfos.Timer.Glyph;

    /// <summary>
    /// folders: what the action button says for <paramref name="state"/>, and whether the clear
    /// button shows — the card's own answers, for its menu (CountdownTile.MenuButtons).
    /// </summary>
    internal static (string Action, bool Clears) MenuButtons(Countdown state, CountdownKind kind) =>
        (Action(state, kind, words: true).Text, !state.Idle && state.Kind == kind);

    /// <summary>
    /// What the action button says and shows for the state the countdown is in. With
    /// <paramref name="words"/> false it is the glyph alone: a card too narrow for the word.
    /// </summary>
    internal static (string Text, string Glyph) Action(Countdown state, CountdownKind kind, bool words)
    {
        var (text, glyph) = state.Idle || state.Kind != kind
            ? Actions[0]
            : state.Phase switch
            {
                CountdownPhase.Running => Actions[1],
                CountdownPhase.Paused when state.Left.Ticks > 0 => Actions[2],
                CountdownPhase.Done => Actions[3],
                _ => Actions[0],
            };
        return words ? (text, glyph) : ("", glyph);
    }

    // --- Text, measured ------------------------------------------------------------------------

    // §459: by the text style Karpo wears too, whose font stands on its own baseline.
    private static readonly ConcurrentDictionary<(float Px, Weight Weight, TextStyle Typeface), float> Baselines = new();

    /// <summary>How far down from its line's origin a line at <paramref name="px"/> stands on its baseline.</summary>
    internal static unsafe float Baseline(Gfx gfx, float px, Weight weight)
    {
        if (Baselines.TryGetValue((px, weight, gfx.Typeface), out var kept))
        {
            return kept;
        }
        var layout = gfx.TextLayout("0", px, weight, null);
        float baseline;
        try
        {
            DWRITE_LINE_METRICS line;
            uint lines;
            ComUtil.Check(layout->GetLineMetrics(&line, 1, &lines));
            baseline = line.baseline;
        }
        finally
        {
            layout->Release();
        }
        if (Baselines.Count > 1024)
        {
            Baselines.Clear();
        }
        Baselines[(px, weight, gfx.Typeface)] = baseline;
        return baseline;
    }
}
