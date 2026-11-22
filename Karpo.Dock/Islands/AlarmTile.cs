// alarm: the alarm widget, a line of a folder's list (docs\widgets.md §106).
//
// One alarm on the kit's row (§401): the glyph in the list's leading column, in the accent while it is set; its
// name ("Alarm", or its own) where every name begins, and under it what it is doing and when it rings — "Off ·
// Weekdays", "Rings in 8 h 23 min · Tomorrow"; its time as the region writes it ("07:00", "7:00 AM") in the kit's
// Value; and its switch against the trailing gutter, which is how it is set going. One row has no caption; a
// taller line has the row at its top and nothing larger.
//
// While it rings its Snooze and Dismiss stand where the switch was, worded where there is room and their glyphs
// alone where there is not, and the caption says "Ringing"; snoozed, Dismiss alone, and the caption says until
// when. Only the switch and those two take a click; the rest of the line does nothing (CellAct.Nothing). Its
// time, its days and its name are set in its settings (AlarmOptions); the switch here writes its options, and the
// dock saves them (AlarmBook.Switched).
//
// What it is doing is the dock's (Countdowns.Alarms), kept by the widget's id, so it rings with no folder open
// and every line of it shows the same. Nothing ticks here: the book says when it changed, and a new minute
// redraws it only where what it says changed (midnight's "Tomorrow" is "Today").

using System.Numerics;
using Karpo.Core;
using Karpo.Dock.QuickSettings.Controls;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock;

internal enum AlarmTargetKind
{
    None,

    /// <summary>On and off.</summary>
    Switch,

    /// <summary>While it rings: again in ten minutes.</summary>
    Snooze,

    /// <summary>While it rings or is snoozed: over. Snoozed, it stands alone where the switch was.</summary>
    Dismiss,
}

internal sealed unsafe class AlarmTile : CellIsland, IListContent, ILinePieces
{
    // Segoe Fluent Icons: the moon of quiet hours for a snooze, the cross for dismiss.
    public const string SnoozeGlyph = "";
    public const string DismissGlyph = "";

    private readonly AlarmBook book;
    private readonly Func<DateTime> now;
    private readonly Func<DateTime, string> time;
    private readonly ToggleSwitch toggle = new() { Name = S.DockAlarmSwitch };

    /// <summary>The control under the pointer.</summary>
    private AlarmTargetKind hot;

    /// <summary>What it last said, so a new minute redraws it only where that changed.</summary>
    private string said = "";

    public AlarmTile(string key, WidgetItem island, Gfx gfx, Config config)
        : this(key, island, gfx, config, null)
    {
    }

    /// <param name="countdowns">The dock's countdowns, whose alarms it reads; null for a book of its own.</param>
    /// <param name="now">The time, UTC; the system's when null.</param>
    /// <param name="time">How a time of day is written; the region's (Nls.Time) when null.</param>
    internal AlarmTile(string key, WidgetItem island, Gfx gfx, Config config, Countdowns? countdowns, Func<DateTime>? now = null, Func<DateTime, string>? time = null)
        : base(key, island, gfx)
    {
        book = (countdowns ?? new Countdowns()).Alarms;
        this.now = now ?? (() => Clock.RealUtc());
        this.time = time ?? Panels.Nls.Time;
        book.Changed += BookChanged;
        Configure(island, config);
    }

    /// <summary>Where its alarm is kept: its widget's id, which it keeps wherever it is carried.</summary>
    internal string Id => Settings.Id;

    /// <summary>Its options: the widget's own, made where a widget nobody normalized has none.</summary>
    internal AlarmOptions Options => Settings.Alarm ??= new AlarmOptions();

    /// <summary>What its alarm is doing now.</summary>
    internal AlarmStatus Status => book.Of(Id, Options, now());

    /// <summary>Its switch, flipped from outside it.</summary>
    internal void Switch() => book.Switch(Id, Options, now());

    public override int IntervalMs => 0;

    protected override int Count => 1;

    public override void Configure(WidgetItem island, Config config)
    {
        Settings = island;
        _ = config;
        Forget();
    }

    /// <summary>No hover tooltip: its line says everything.</summary>
    public override string? Tooltip(int index) => null;

    /// <summary>A new minute: drawn again where what it says changed (midnight, a day further on).</summary>
    public override bool MinuteChanged(Gfx gfx)
    {
        if (Says() != said)
        {
            Redrawn();
        }
        return false;
    }

    protected override bool Sample(bool everything) => false;

    public override IslandAction Click(int index)
    {
        var at = now();
        switch ((AlarmTargetKind)index)
        {
            case AlarmTargetKind.Switch:
                book.Switch(Id, Options, at);
                break;
            case AlarmTargetKind.Snooze:
                book.Snooze(Id, at);
                break;
            case AlarmTargetKind.Dismiss:
                book.Dismiss(Id, at);
                break;
        }
        return default;
    }

    public override void Dispose()
    {
        book.Changed -= BookChanged;
        base.Dispose();
    }

    private void BookChanged(string id)
    {
        if (string.Equals(id, Id, StringComparison.OrdinalIgnoreCase))
        {
            Redrawn();
        }
    }

    /// <summary>The control under the pointer lights; the folder draws the line it told.</summary>
    protected internal override void CardHover(Vector2? point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var at = point is { } where ? (AlarmTargetKind)CardTarget(where, box, style, surface) : AlarmTargetKind.None;
        if (at == hot)
        {
            return;
        }
        hot = at;
        Redraw(Gfx);
    }

    /// <summary>A line of a folder's list, never a widget of a bar.</summary>
    internal override void PaintCell(ID2D1DeviceContext* dc, int index, in IslandStyle style, float width, float height) =>
        throw new NotSupportedException($"{Info.Id} is a line of a folder's list, not a widget of a bar.");

    // --- In a list ------------------------------------------------------------------------

    /// <summary>The names it goes by: its own, else "Alarm".</summary>
    private string Called => Options.Name.Trim() is { Length: > 0 } own ? own : S.DockAlarmTitle;

    /// <summary>Its time as the region writes it, 12- or 24-hour as the clock is.</summary>
    private string TimeText => time(Clock.Real().Date.AddMinutes(AlarmTimes.Minutes(Options.Time)));

    /// <summary>What it says under (or after) its time now.</summary>
    internal string Hint(AlarmStatus status)
    {
        var zone = book.Zone;
        return AlarmWords.Hint(status, AlarmPlan.Of(Options), now(), zone, Panels.Nls.FirstDayOfWeek(), Panels.Nls.ShortDayName, time);
    }

    /// <summary>Everything it says, as one string: what a redraw is for.</summary>
    private string Says()
    {
        var status = Status;
        return $"{status.Phase}|{TimeText}|{Caption(status)}|{Options.Name}";
    }

    /// <summary>
    /// What the Clock app's card says under its time now: while it is set, when it rings ("Rings in
    /// 8 h 23 min"); else what its one-row line says ("Off", "Ringing", "Snoozed until 7:10").
    /// </summary>
    internal string Secondary(AlarmStatus status) =>
        status is { Phase: AlarmPhase.Set, Next: { } next } ? AlarmCardText.RingsIn(next - now()) : Hint(status);

    /// <summary>
    /// The caption under its name (§401): what it is doing and when it rings — "Off · Weekdays", "Rings in
    /// 8 h 23 min · Mon, Wed", "Rings in 9 h · Tomorrow", "Ringing", "Snoozed until 7:10".
    /// </summary>
    internal string Caption(AlarmStatus status)
    {
        var plan = AlarmPlan.Of(Options);
        var days = plan.Repeats ? AlarmWords.Days(plan.Days, Panels.Nls.FirstDayOfWeek(), Panels.Nls.ShortDayName) : null;
        return status.Phase switch
        {
            AlarmPhase.Off => LineRow.Join(S.DockAlarmOff, days),
            AlarmPhase.Set => LineRow.Join(Secondary(status), Hint(status)),
            _ => Hint(status),
        };
    }

    /// <summary>Where an alarm's row put its controls, in the box's px: its switch, or while it rings Snooze and Dismiss.</summary>
    internal readonly record struct AlarmRow(LineRow.Laid Row, D2D_RECT_F Switch, D2D_RECT_F Snooze, D2D_RECT_F Dismiss, bool Worded);

    /// <summary>
    /// The line at whatever height it is given, on the kit's row (§401): the glyph in the leading column — the
    /// accent while it is set — its name where every name begins with what it is doing and when under it, its
    /// time in the Value, and its switch against the trailing gutter; while it rings Snooze and Dismiss there
    /// instead, worded where they fit. One row has no caption. A taller line has the row at its top.
    /// </summary>
    internal AlarmRow Draw(LineSketch sketch, D2D_RECT_F box, in TileStyle style)
    {
        var status = Status;
        var theme = style.Theme;
        var height = TilePaint.Height(box);
        var captioned = height + 0.5f >= LineRow.Height(style, caption: true);
        var on = status.Phase != AlarmPhase.Off;
        var buttons = LineButton.Of(Gfx, style);
        var controls = new List<RowControl>();
        var ringing = status.Phase is AlarmPhase.Ringing or AlarmPhase.Snoozed;
        var worded = false;
        if (ringing)
        {
            // Snoozed, Dismiss alone: its ring is put off, and a second snooze is no answer to nothing ringing.
            var snoozes = status.Phase == AlarmPhase.Ringing;
            var form = ListForm.Of(Gfx, style);
            var room = TilePaint.Width(box) - form.TextLeft - form.Inset - PopupPaint.Measure(Gfx, TimeText, style.Text(LineType.ValueDip), Weight.Semibold).X;
            var wide = (snoozes ? buttons.Width(Gfx, S.DockAlarmSnooze) + buttons.Gap : 0) + buttons.Width(Gfx, S.DockAlarmDismiss) + style.Px(LineSpace.GapDip);
            worded = wide <= room - style.Px(48);
            if (snoozes)
            {
                controls.Add(RowControl.Button(Gfx, buttons, "snooze", worded ? S.DockAlarmSnooze : "", worded ? null : SnoozeGlyph, ButtonStyle.Standard, hot == AlarmTargetKind.Snooze));
            }
            controls.Add(RowControl.Button(Gfx, buttons, "dismiss", worded ? S.DockAlarmDismiss : "", worded ? null : DismissGlyph, ButtonStyle.Accent, hot == AlarmTargetKind.Dismiss));
        }
        else
        {
            controls.Add(RowControl.Room("switch", style.Px(40), style.Px(20)));
        }
        var row = LineRow.Draw(sketch, box.left, box.right, box.top, style, new RowSpec(Called)
        {
            Glyph = Info.Glyph,
            GlyphInk = on ? PopupPaint.Ink(theme.Accent) : null,
            Caption = captioned ? Caption(status) : null,
            // §497: one row says when it rings (or till when it is snoozed) at its end, as its caption does under a taller one.
            Detail = status.Phase is AlarmPhase.Set or AlarmPhase.Snoozed ? Secondary(status) : null,
            Reading = TimeText,
            // Off, its time is the quieter ink, as Windows' Clock greys an alarm that is off.
            ReadingInk = on ? null : theme.TextSecondary,
            Controls = controls,
            CaptionPiece = "hint",
            ReadingPiece = "time",
        }, captioned ? 0 : height);
        if (ringing)
        {
            var snooze = controls.Count > 1 ? row.Controls[0] : default;
            return new AlarmRow(row, default, snooze, row.Controls[^1], worded);
        }
        var at = row.Controls[0];
        if (sketch.Device is var dc && dc != null)
        {
            var context = new QuickContext { Gfx = Gfx, Theme = QuickTheme.For(theme), Scale = style.Scale, TextScale = style.TextScale };
            toggle.On = on;
            toggle.Enabled = true;
            toggle.Hot = hot == AlarmTargetKind.Switch;
            toggle.Bounds = PopupPaint.Rect(0, 0, TilePaint.Width(at), TilePaint.Height(at));
            TilePaint.Placed(dc, at.left, at.top, 1, () => toggle.Paint(dc, context));
        }
        return new AlarmRow(row, at, default, default, false);
    }

    protected internal override void PaintCard(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        said = Says();
        Draw(new LineSketch(dc, Gfx), box, style);
    }

    /// <summary>Its switch, or while it rings its Snooze and Dismiss, at <paramref name="point"/>; 0 (None) for the rest of it.</summary>
    protected internal override int CardTarget(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var line = Draw(new LineSketch(null, Gfx), box, style);
        static bool In(D2D_RECT_F rect, Vector2 at, float slack) =>
            rect.right > rect.left && at.X >= rect.left - slack && at.X < rect.right + slack && at.Y >= rect.top - slack && at.Y < rect.bottom + slack;
        // The switch is 20 px tall: a little round it takes the pointer too, as WinUI's does; the buttons keep the lines' reach.
        var buttons = LineButton.Of(Gfx, style);
        return In(line.Switch, point, style.Px(6)) ? (int)AlarmTargetKind.Switch
            : In(buttons.Hit(line.Snooze), point, 0) ? (int)AlarmTargetKind.Snooze
            : In(buttons.Hit(line.Dismiss), point, 0) ? (int)AlarmTargetKind.Dismiss
            : (int)AlarmTargetKind.None;
    }

    /// <summary>Its form for its size (§432): the kit's row (Compact), or with its caption (Full).</summary>
    public float ListHeight(float width, in TileStyle style) => LineRow.Height(style, caption: LineSizes.Of(Settings) != LineSize.Compact);

    public string MainPiece => "time";

    public IReadOnlyList<LinePiece> LinePieces(D2D_RECT_F box, in TileStyle style)
    {
        var sketch = new LineSketch(null, Gfx);
        Draw(sketch, box, style);
        return [.. sketch.Pieces.Where(piece => !piece.Name.EndsWith(" plate", StringComparison.Ordinal))];
    }
}
