// The power mode of Windows' "Power & battery" page as a line of a list folder (QuickPageTile,
// docs\widgets.md §94): its heading, what it is about, and the three modes, the one in force
// marked. It was a section of the battery and power widgets' flyouts (PowerSection, under the
// charge and under the commands); those widgets are the battery and power presets now, and this
// line is in both. Energy saver, the section's other row, is its own line beside it (the
// energySaver switch), and the flyouts' footers are the power actions line under them.
//
// A click asks Windows for the mode off the dock's thread (WindowsBattery.SetMode): the row is
// marked at once, and the mark follows what Windows then says — back where it was, with a word of
// why, where it refused. A PC whose power plan takes no overlay offers no modes, and the line says so
// rather than showing three rows that do nothing.
//
// **At every height** (2026-10-19, the person: "power mode does not currently adapt nicely to small
// sizes, it should work all the way down to one cell height"; docs\widgets.md §248). The line fits
// itself to the height it is given (QuickPage.Fits) — the fullest of these that fits, every mode a
// click away in each:
//
//   * Whole: the heading, what it is about, the three modes as rows, and why a mode was refused.
//   * Listed: the three modes as rows, 32 to 40 DIP each (at the largest text, a line of its
//     words and a little air at least: RowLeast), under the heading where it fits; what it is
//     about, and a refusal, where they fit too.
//   * Strip: the mode's gauge in the list's icon column and "Power mode" on a row, the mode in
//     force at its end, the three modes as a segmented control under them, worded where all three
//     words fit.
//   * One row: the gauge and the mode in force, the segmented control of the three gauges at the
//     row's end (ListTiers.Row: the control wins, the name takes what is left).

using Karpo.Core;
using Karpo.Dock.QuickSettings.Controls;
using Karpo.Dock.QuickSettings.Services;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock.QuickSettings;

/// <summary>How much of itself the power mode's line shows (<see cref="PowerModePage"/>).</summary>
internal enum PowerModeForm
{
    /// <summary>The heading, what it is about, the modes as rows, a refusal.</summary>
    Whole,

    /// <summary>The modes as rows, under the heading where it fits.</summary>
    Listed,

    /// <summary>"Power mode" on a row, the modes as a segmented control under it.</summary>
    Strip,

    /// <summary>One row: the mode in force, and the segmented control at its end.</summary>
    One,
}

internal sealed class PowerModePage : QuickPage
{
    /// <summary>
    /// The power modes, each with the gauge Segoe Fluent gives its speed: the needle low for the
    /// quiet one, upright for balanced, over to the right for performance. One family, so the three
    /// rows read as one scale. Made when read, so the names are in the language now.
    /// </summary>
    internal static (PowerMode Mode, string Name, string Glyph)[] Modes =>
    [
        (PowerMode.Efficiency, S.DockQuickPowerEfficiency, ""),
        (PowerMode.Balanced, S.DockQuickPowerBalanced, ""),
        (PowerMode.Performance, S.DockQuickPowerPerformance, ""),
    ];

    private readonly IBatterySource source;
    private readonly ListRow[] modeRows;
    private readonly SegmentedItem[] segments;
    private readonly SegmentedTrack track = new();
    private readonly GlyphIcon glyph = new(WidgetInfos.PowerMode.Glyph, 16);
    private readonly Label heading = new(S.DockQuickPowerMode, LabelStyle.Strong) { Name = "Power mode" }; // not-ui: the automation id
    private readonly Label caption = new(S.DockQuickPowerModeCaption, LabelStyle.Caption, lines: 2);
    private readonly Label problem = new("", LabelStyle.Critical, lines: 2);

    // The mode in force on a short line: after the glyph on one row, at the heading's end above the strip.
    private readonly Label said = new("", LabelStyle.Body);
    private readonly Label reading = new("", LabelStyle.Secondary) { Trailing = true };
    private BatteryState state = new(false, 0, false, false, null, null, false, null);

    // The mode asked for and not yet in force (Windows is asked off this thread), and why the last
    // one asked for was not taken.
    private PowerMode? asked;
    private string? refused;

    public PowerModePage(IQuickHost host, IBatterySource source)
        : base(host)
    {
        this.source = source;
        var modes = Modes;
        modeRows = [.. modes.Select(entry => new ListRow { Name = entry.Name, Title = entry.Name, Glyph = entry.Glyph })];
        segments = [.. modes.Select(entry => new SegmentedItem(entry.Name, entry.Glyph) { Name = entry.Name })];
        for (var i = 0; i < modes.Length; i++)
        {
            var mode = modes[i].Mode;
            modeRows[i].Clicked = () => Choose(mode);
            segments[i].Clicked = () => Choose(mode);
        }
    }

    public override QuickKind Kind => QuickKind.PowerMode;

    public override bool Fits => true;

    /// <summary>The mode the line marks: the one asked for until Windows has answered, else the one in force.</summary>
    internal PowerMode? Shown => asked ?? state.Mode;

    /// <summary>The form the last build placed, for a test.</summary>
    internal PowerModeForm Form { get; private set; }

    /// <summary>Whether this PC's plan offers the modes at all (one is in force, or one was asked for).</summary>
    private bool Offered => state.Mode is not null || asked is not null;

    public override void Start()
    {
        source.Start(Changes);
        state = source.Read();
        Host.Rebuild();
    }

    protected override void Refresh(int news)
    {
        state = source.Read();
        if ((news & BatteryNews.Refused) != 0)
        {
            // Windows kept the mode it had: the mark goes back to it, and the line says so.
            refused = S.DockQuickPowerModeRefused;
            asked = null;
        }
        else if (asked is { } waiting && state.Mode == waiting)
        {
            asked = null;
        }
    }

    public override void Build(PageLayout layout)
    {
        var modes = Modes;
        var shown = Shown;
        for (var i = 0; i < modes.Length; i++)
        {
            modeRows[i].Selected = segments[i].Selected = modes[i].Mode == shown;
        }
        glyph.Glyph = modes.FirstOrDefault(each => each.Mode == shown).Glyph ?? WidgetInfos.PowerMode.Glyph;
        Form = Fit(layout);
    }

    /// <summary>The fullest form that fits the line's height, placed (see the top of the file).</summary>
    private PowerModeForm Fit(PageLayout layout)
    {
        if (layout.Style is not { } style || float.IsPositiveInfinity(layout.Room))
        {
            Whole(layout);
            return PowerModeForm.Whole;
        }
        var trial = layout.Whole();
        Whole(trial);
        var whole = trial.Finish();
        if (whole.HeaderHeight + whole.BodyHeight <= layout.Room + 0.5f)
        {
            Whole(layout);
            return PowerModeForm.Whole;
        }
        if (Offered ? Listed(layout) : Headed(layout))
        {
            return PowerModeForm.Listed;
        }
        if (Offered && Strip(layout, style))
        {
            return PowerModeForm.Strip;
        }
        One(layout, style);
        return PowerModeForm.One;
    }

    /// <summary>The whole page: the heading, what it is about, the three modes as rows, why a mode was refused.</summary>
    private void Whole(PageLayout layout)
    {
        var context = layout.Context;
        layout.Heading(heading);
        if (!Offered)
        {
            // This PC's plan takes no overlay: Windows offers no modes, so none is shown.
            caption.Text = S.DockQuickPowerModeNone;
            QuickPage.Text(layout, caption, layout.TextLeft, layout.TextRight);
            return;
        }
        caption.Text = S.DockQuickPowerModeCaption;
        QuickPage.Text(layout, caption, layout.TextLeft, layout.TextRight);
        layout.Gap(6);
        foreach (var row in modeRows)
        {
            layout.Row(row, context.T(40));
            layout.Gap(2);
        }
        if (refused is { } trouble)
        {
            problem.Text = trouble;
            layout.Gap(4);
            QuickPage.Text(layout, problem, layout.TextLeft, layout.TextRight);
        }
    }

    /// <summary>
    /// The three modes as rows, as tall as the room leaves them (RowLeast to 40 DIP), with what fits of
    /// the heading, a refusal and what it is about over and under them, in that order of what
    /// stays; false where even the rows alone do not fit.
    /// </summary>
    private bool Listed(PageLayout layout)
    {
        var context = layout.Context;
        var width = layout.TextRight - layout.TextLeft;
        caption.Text = S.DockQuickPowerModeCaption;
        problem.Text = refused ?? "";
        var about = Tall(context, caption, width) + context.D(6);
        var wrong = refused is null ? 0 : context.D(4) + Tall(context, problem, width);
        (bool Head, bool About, bool Wrong)[] choices = [(true, true, true), (true, false, true), (true, false, false), (false, false, true), (false, false, false)];
        foreach (var (head, says, warns) in choices)
        {
            var rest = (head ? context.T(32) : context.D(4)) + (says ? about : 0) + (warns ? wrong : 0) + (modeRows.Length * context.D(2)) + context.D(8);
            var row = MathF.Floor(MathF.Min(context.T(40), (layout.Room - rest) / modeRows.Length));
            if (row < RowLeast(context))
            {
                continue;
            }
            if (head)
            {
                layout.Place(heading, layout.TextLeft, width, context.T(32));
            }
            else
            {
                layout.Gap(4);
            }
            if (says)
            {
                QuickPage.Text(layout, caption, layout.TextLeft, layout.TextRight);
                layout.Gap(6);
            }
            foreach (var each in modeRows)
            {
                layout.Row(each, row);
                layout.Gap(2);
            }
            if (warns && refused is not null)
            {
                layout.Gap(4);
                QuickPage.Text(layout, problem, layout.TextLeft, layout.TextRight);
            }
            return true;
        }
        return false;
    }

    /// <summary>
    /// The least a mode's row is: WinUI's compact 32 DIP, or a line of the text with 4 DIP over and
    /// under it where that is more — at the largest text a row of 32 DIP grown with the text would be
    /// twice what its words need, and the strip would stand where the rows fit.
    /// </summary>
    private static float RowLeast(QuickContext context) =>
        MathF.Max(context.D(32), MathF.Ceiling(TilePaint.LineHeight(context.Gfx, context.TextPx)) + context.D(8));

    /// <summary>A plan with no modes, on a short line: the heading over why, where both fit.</summary>
    private bool Headed(PageLayout layout)
    {
        var context = layout.Context;
        var width = layout.TextRight - layout.TextLeft;
        caption.Text = S.DockQuickPowerModeNone;
        if (context.T(32) + Tall(context, caption, width) + context.D(8) > layout.Room + 0.5f)
        {
            return false;
        }
        layout.Place(heading, layout.TextLeft, width, context.T(32));
        QuickPage.Text(layout, caption, layout.TextLeft, layout.TextRight);
        return true;
    }

    /// <summary>
    /// The gauge and "Power mode" on a row, the mode in force at its end (or why one was refused),
    /// and the segmented control under them from gutter to gutter; false where they do not fit.
    /// </summary>
    private bool Strip(PageLayout layout, in TileStyle style)
    {
        var context = layout.Context;
        var form = ListForm.Of(context.Gfx, style);
        var head = MathF.Max(form.Line, form.Glyph);
        var gap = context.D(4);
        var tall = SegmentedItem.HeightFor(context);
        var block = head + gap + tall;
        if (block > layout.Room + 0.5f)
        {
            return false;
        }
        var top = LineRows.Top(0, layout.Room, block, form.Pad);
        var left = form.Inset;
        var right = layout.Width - form.Inset;
        var worded = segments.All(each => SegmentedItem.WordedWidth(context, each.Text) <= (right - left) / segments.Length);

        // The mode in force is said at the row's end where the segments do not say it; a refusal always is.
        reading.Text = refused ?? (worded ? "" : NameOf(Shown));
        reading.Style = refused is null ? LabelStyle.Secondary : LabelStyle.Critical;
        var field = reading.Text.Length == 0 ? 0 : MathF.Ceiling(Label.WidthOf(context, reading.Text, reading.Style));
        var row = ListTiers.Row(context.Gfx, PopupPaint.Rect(0, top, layout.Width, head), style, [S.DockQuickPowerMode], Weight.Semibold, field);
        PutGlyph(layout, form, top, head);
        if (row.Name is not null)
        {
            layout.Put(heading, PopupPaint.Rect(row.NameLeft, row.TextTop, row.NameRoom, form.Line));
        }
        if (reading.Text.Length > 0 && row.Name is null)
        {
            // No room for "Power mode": the mode stands where the name would, after the gauge, not at the far end of an empty row.
            said.Text = reading.Text;
            said.Style = reading.Style;
            layout.Put(said, PopupPaint.Rect(row.NameLeft, row.TextTop, MathF.Max(1, row.FieldRight - row.NameLeft), form.Line));
        }
        else if (reading.Text.Length > 0)
        {
            layout.Put(reading, PopupPaint.Rect(row.FieldLeft, row.TextTop, MathF.Max(1, row.Field), form.Line));
        }
        PutSegments(layout, PopupPaint.Rect(left, PopupPaint.Round(top + head + gap), right - left, tall), worded);
        return true;
    }

    /// <summary>
    /// One row: the gauge and the mode in force (or why one was refused, or that there are none), the
    /// segmented control of the three gauges at the row's end, in the row's middle band.
    /// </summary>
    private void One(PageLayout layout, in TileStyle style)
    {
        var context = layout.Context;
        var form = ListForm.Of(context.Gfx, style);
        var box = PopupPaint.Rect(0, 0, layout.Width, layout.Room);
        said.Text = !Offered ? S.DockQuickPowerModeNone : refused ?? NameOf(Shown);
        said.Style = !Offered ? LabelStyle.Secondary : refused is null ? LabelStyle.Body : LabelStyle.Critical;
        var field = Offered ? segments.Length * SegmentedItem.GlyphWidth(context) : 0;
        var row = ListTiers.Row(context.Gfx, box, style, [said.Text], Weight.Regular, field);
        PutGlyph(layout, form, row.TextTop, form.Line);
        if (row.Name is not null)
        {
            layout.Put(said, PopupPaint.Rect(row.NameLeft, row.TextTop, row.NameRoom, form.Line));
        }
        if (Offered)
        {
            var tall = MathF.Max(1, MathF.Min(SegmentedItem.HeightFor(context), layout.Room - (2 * context.D(2))));
            PutSegments(layout, PopupPaint.Rect(row.FieldLeft, PopupPaint.Round(row.Middle - (tall / 2)), MathF.Max(1, row.Field), tall), worded: false);
        }
    }

    /// <summary>The gauge of the mode in force in the list's icon column, on a row <paramref name="height"/> px tall at <paramref name="top"/>.</summary>
    private void PutGlyph(PageLayout layout, in ListForm form, float top, float height)
    {
        glyph.Px = form.Glyph;
        layout.Put(glyph, ListTiers.GlyphBox(form, 0, top, height, form.Glyph));
    }

    /// <summary>The track across <paramref name="box"/> and the three segments on it, side by side, of an equal width.</summary>
    private void PutSegments(PageLayout layout, D2D_RECT_F box, bool worded)
    {
        layout.Put(track, box);
        var each = (box.right - box.left) / segments.Length;
        for (var i = 0; i < segments.Length; i++)
        {
            segments[i].Worded = worded;
            var from = PopupPaint.Round(box.left + (i * each));
            var to = i == segments.Length - 1 ? box.right : PopupPaint.Round(box.left + ((i + 1) * each));
            layout.Put(segments[i], new D2D_RECT_F(from, box.top, to, box.bottom));
        }
    }

    /// <summary>The name of <paramref name="mode"/>, or none.</summary>
    private static string NameOf(PowerMode? mode) => Modes.FirstOrDefault(each => each.Mode == mode).Name ?? "";

    /// <summary>How tall <paramref name="label"/> is across <paramref name="width"/> px, as <see cref="QuickPage.Text"/> places it.</summary>
    private static float Tall(QuickContext context, Label label, float width) =>
        MathF.Ceiling(MathF.Max(Label.Measure(context, label.Text, label.Style, MathF.Max(1, width), label.Lines), 1));

    private void Choose(PowerMode mode)
    {
        if (Shown == mode)
        {
            return;
        }
        asked = mode;
        refused = null;
        source.SetMode(mode);
        Host.Rebuild();
    }

    public override void Dispose() => source.Dispose();
}
