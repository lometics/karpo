// A widget's size, as a function of its settings (docs\widgets.md §4, stage 3).
//
// The rule the user gave is that a widget's size follows its settings and never
// what it happens to be showing. Until now that was a rule kept by hand and
// guarded by a test per kind: every island's Length was swept once and
// Karpo.Tests\Sizing\StateSizingTests.cs holds it there. A sweep is a patch.
//
// The fix is that Shape is not a method on the island at all. It is a function
// here, called with the widget's own settings and the bar's numbers, and it has
// nothing live to read: no store, no clock, no disk, no field of the instance.
// A kind converted to it cannot measure the value it is showing, because it
// cannot reach it.
//
// The one exception is a folder's faces (docs\widgets.md §244, the person,
// 2026-10-19: "size all faces to their current content"): the bar measures a
// folder with the folder itself (ShapeContext.Island), and its faces are as wide
// as what they show now. No other kind reads it.
//
// Gfx is passed because text is measured through it, but only as a template
// (TextLine.Template normalises digits, so only the shape of the words counts)
// and nothing is drawn. It is a device, not state.
//
// Kinds are converted one at a time, and a shape is found by what draws the
// widget (its Drawing). For(drawing) returns null for one that has not been,
// and CellIsland falls back to its own Length — the adapter §6 asks for. It
// returns null rather than a guess, because a lookup that hands back somebody
// else's answer is the defect this project has now paid for four times.
//
// Appearing and disappearing is a different thing and stays live: a media
// widget with nothing playing takes itself off the bar (CellIsland.Count = 0)
// rather than changing size. The shape says what the widget is when it is
// there.

using Karpo.Core;

namespace Karpo.Dock;

/// <summary>
/// What a widget is, before the layout rounds it to the grid (where it is not <see cref="Exact"/>): the content length of each of its
/// cells, along the dock, in px. A widget whose cells differ (the now-playing info beside its
/// buttons) is why this is a list and not one number.
/// </summary>
internal readonly record struct WidgetShape
{
    /// <summary>Each cell's content length in px, in the order the island paints them.</summary>
    public required IReadOnlyList<float> Cells { get; init; }

    /// <summary>
    /// Whether each cell is exactly its content, in whole px, and never held to the grid: not
    /// rounded up to whole cells, not one cell at the least, not held in the widget's range. A
    /// folder's button is the case (docs\widgets.md §275): its faces and its ends, and nothing
    /// more. Otherwise every cell is at least one grid cell and held in the range (<see cref="Layout.AllotCells"/>).
    /// </summary>
    public bool Exact { get; init; }

    /// <summary>How many cells the widget has when it is shown.</summary>
    public int Count => Cells.Count;

    /// <summary>A widget of one cell.</summary>
    public static WidgetShape One(float length) => new() { Cells = [length] };

    /// <summary>A widget whose cells differ, in the order they are painted.</summary>
    public static WidgetShape Of(params float[] cells) => new() { Cells = cells };

    /// <summary>A widget of one cell exactly <paramref name="length"/> long (<see cref="Exact"/>).</summary>
    public static WidgetShape Hug(float length) => new() { Cells = [length], Exact = true };
}

/// <summary>
/// Facts about the machine that a shape may depend on. The entry price is that each is
/// <b>stable for the life of the process</b>, like the scale factor: something that can change
/// while Karpo runs is state, and a widget draws state into the room its shape already gave it.
/// Keep this small — it is not a way in for anything live.
/// </summary>
internal readonly record struct WidgetMachine
{
    /// <summary>
    /// Whether this PC has a battery at all. On a PC without one the battery face shows the power
    /// bolt in a cell as tight as the Wi-Fi face beside it, and one with a battery keeps room for
    /// "100%" (BatteryFaceDrawing) — the
    /// hardware, not the charge, which is why the cell never moves as the charge falls.
    /// </summary>
    public required bool HasBattery { get; init; }

    /// <summary>This PC, read once. The battery probe is the islands' own, so the tests can fake it.</summary>
    public static WidgetMachine Read() => new() { HasBattery = BatteryIsland.Probe() is not null };
}

/// <summary>
/// Everything a shape may look at. Nothing here is live: the settings come from the config, the
/// metrics from the bar, and the machine facts hold still for as long as Karpo runs.
/// <para>
/// <see cref="Gfx"/> is a device, not state — a font metric is a property of the machine and the
/// theme, the same class of thing as the DPI. The line is that a shape measures a
/// <b>template</b> (<c>TextLine.Template</c> normalises digits, so only the shape of the words
/// counts) and <b>never the value the widget is showing</b>. Do not reach through it for
/// anything live: the clock, a reading, a count, a title. What holds that line is
/// <c>IslandCreationTests.AConvertedKindIsSizedByItsShapeAndNotByItsIsland</c> together with the
/// state tests, which measure a kind in every state it can be in and expect one answer.
/// </para>
/// </summary>
/// <param name="Options">The widget's own settings, as the config has them.</param>
/// <param name="Info">
/// Which widget it is (<see cref="IIsland.Info"/>): the name a one-button widget keeps room for, the
/// metric a graph reads. A drawing serves several widgets, and this is how it tells them apart.
/// </param>
/// <param name="Metrics">The bar's numbers: scale, thickness, orientation, look, chosen width.</param>
/// <param name="Machine">What this PC is, for as long as it is running.</param>
/// <param name="Gfx">For measuring text templates only.</param>
internal readonly record struct ShapeContext(WidgetItem Options, WidgetInfo Info, IslandStyle Metrics, WidgetMachine Machine, Gfx Gfx)
{
    public ShapeContext(WidgetItem options, WidgetInfo info, IslandStyle metrics, Gfx gfx)
        : this(options, info, metrics, WidgetMachine.Read(), gfx)
    {
    }

    public Fit Fit => new(Metrics);

    public bool Vertical => Metrics.Vertical;

    /// <summary>
    /// A folder's faces on the bar now, where one of them has taken itself off it (§237: a now-playing
    /// face hidden while nothing plays); null for all of them. Which faces there are is a live question,
    /// as how many cells a widget has is (CellIsland.Count).
    /// </summary>
    public IReadOnlyList<FolderFace>? Faces { get; init; }

    /// <summary>
    /// The widget the bar is measuring, where it measures one. Only a folder reads it: its faces take
    /// the room what they show now needs (docs\widgets.md §244). Null where only settings are measured.
    /// </summary>
    public CellIsland? Island { get; init; }
}

internal static class WidgetShapes
{
    /// <summary>How a kind works out its size from its settings and the bar's numbers.</summary>
    internal delegate WidgetShape Measure(in ShapeContext context);

    /// <summary>
    /// The drawings converted so far, and the function that sizes each — keyed by what draws a
    /// widget (its taskbar entry's, card's or line's <c>Drawing</c>), not by which widget it is, so a
    /// drawing that serves several (the five graphs, the one-button widgets) is sized in one place
    /// and told which one it is (<see cref="ShapeContext.Info"/>). A drawing that is not here is
    /// still sized by its island's own Length, and <see cref="For"/> says so by answering null.
    /// </summary>
    private static readonly (WidgetSource Drawing, Measure Shape)[] Converted =
    [
        // quick: the slider's own plan, which was already a pure function of its options.
        (WidgetSource.Volume, Volume),
        // quick: the brightness slider is the volume slider's size, at its settings' defaults.
        (WidgetSource.Brightness, Brightness),
        // pins: a pin's card too small for its card drawing, at the bar's own icon size.
        (WidgetSource.PinTile, Pin),
        // Task view in a folder: a glyph and nothing else, as the bar's own button is.
        (WidgetSource.TaskView, GlyphButton),
        // The Snipping Tool and Settings in a folder, the same; and the clipboard's button.
        (WidgetSource.WindowsShortcut, GlyphButton),
        // The one left of the three that reached a pure answer through a live read (docs\widgets.md §4).
        (WidgetSource.ClockTile, ClockTime),
        // folders: the last kind to size itself. Both views share one button.
        (WidgetSource.Folder, Folder),
        // §474: Windows' own faces, a folder's button's shape.
        (WidgetSource.WindowsFaces, Folder),
        // The two switchers: a glyph and room for a name, never the name itself.
        // quick: the one-button widgets.
        (WidgetSource.Switch, Switch),
        // Whether this PC is protected. Not a switch — its button switches nothing — but the same
        // shape: a glyph in its pill and room for its own name where the setting asks.
        (WidgetSource.Security, Switch),
        // A kind before its drawing is built: its glyph and its title.
        (WidgetSource.Shell, Shell),
    ];

    /// <summary>
    /// How a widget drawn by <paramref name="drawing"/> is sized, or null where the island still
    /// sizes itself. Never a stand-in: a drawing nobody has converted has no shape, and saying so
    /// is the whole point.
    /// </summary>
    internal static Measure? For(WidgetSource drawing)
    {
        foreach (var (candidate, shape) in Converted)
        {
            if (candidate == drawing)
            {
                return shape;
            }
        }
        return null;
    }

    /// <summary>Whether a widget drawn by <paramref name="drawing"/> is no longer sized through its island.</summary>
    internal static bool Has(WidgetSource drawing) => For(drawing) is not null;

    /// <summary>
    /// A widget that is a glyph and nothing else: the Wi-Fi, Bluetooth, volume and power buttons.
    /// The room at its ends is a sliver where the full <see cref="Fit.Edge"/> would push it into
    /// another cell, which is what makes it one cell on every bar (docs\widgets.md §12.2).
    /// </summary>
    private static WidgetShape GlyphButton(in ShapeContext context)
    {
        var fit = context.Fit;
        return WidgetShape.One(GlyphAndText.Square(context.Metrics, fit, fit.Glyph()));
    }

    /// <summary>
    /// quick: the volume slider. Its plan was already a pure function of its options and the bar,
    /// which is why it is one line here.
    /// </summary>
    private static WidgetShape Volume(in ShapeContext context)
    {
        var style = context.Metrics;
        // The room a chosen width gives, in the terms the drawing measures it in: the whole cells,
        // edges and all. Nothing at the automatic size, where the slider's own length decides and
        // the grid rounds that up. Handing it over is what lets the widget answer with its short
        // form — the speaker alone — where a track would have no travel, instead of a full-length
        // track drawn into a cell that cuts it off (VolumeIsland.Plan).
        var along = style.Width >= WidgetItem.MinWidth ? style.Width * style.Cell : 0;
        return WidgetShape.One(VolumeIsland.Plan(
            context.Options.Volume ?? new VolumeOptions(),
            style,
            VolumeIsland.CellAcross(style),
            along).Along);
    }

    /// <summary>quick: the brightness slider: the volume slider's plan at its defaults (BrightnessIsland.Plan).</summary>
    private static WidgetShape Brightness(in ShapeContext context)
    {
        var style = context.Metrics;
        var along = style.Width >= WidgetItem.MinWidth ? style.Width * style.Cell : 0;
        return WidgetShape.One(BrightnessIsland.Plan(style, VolumeIsland.CellAcross(style), along).Along);
    }

    /// <summary>pins: one app button, at the bar's icon size with room to hit it.</summary>
    private static WidgetShape Pin(in ShapeContext context)
    {
        var style = context.Metrics;
        return WidgetShape.One(MathF.Max(style.Icon + (16 * style.Scale), 32 * style.Scale));
    }

    /// <summary>
    /// The clock, as a folder's card: the longest the time ever reads in this format, not the
    /// time showing. It used to measure what the clock said, so a 12-hour clock was one figure
    /// narrower before ten in the morning and the card moved at ten.
    /// </summary>
    private static WidgetShape ClockTime(in ShapeContext context)
    {
        var fit = context.Fit;
        var options = context.Options.Clock ?? new ClockOptions();
        var (width, _) = TextLine.Template(context.Gfx, Karpo.Dock.Clock.Widest(options.Seconds), fit.Primary, Weight.Regular);
        return WidgetShape.One((2 * fit.Edge) + MathF.Max(width, fit.Px(24)));
    }

    /// <summary>
    /// A shell (<see cref="ShellIsland"/>): the kind's glyph and its title beside it, the room the
    /// title needs and no more — a shell shows nothing that changes, so this is its whole size. On
    /// a vertical bar, the glyph alone in one cell.
    /// </summary>
    private static WidgetShape Shell(in ShapeContext context)
    {
        var fit = context.Fit;
        var glyph = fit.Glyph();
        if (context.Vertical)
        {
            return WidgetShape.One(GlyphAndText.Square(context.Metrics, fit, glyph));
        }
        var title = TextLine.Template(context.Gfx, context.Info.Title, fit.Primary, Weight.Regular).Width;
        return WidgetShape.One((2 * fit.Edge) + glyph + fit.Gap + title);
    }

    /// <summary>
    /// quick: a one-button widget. Its glyph in a pill, and beside it its own name where the
    /// setting asks for one — the name from its <see cref="WidgetInfo"/>, which is what the widget
    /// paints, so it cannot be measured for one label and painted with another. A switch the taskbar
    /// has no entry for (the mobile hotspot's, since its button became the mobile hotspot preset) is
    /// a folder's line alone, and has no length along a bar to answer.
    /// </summary>
    private static WidgetShape Switch(in ShapeContext context)
    {
        if (BarEntries.Find(context.Info.Id) is null)
        {
            throw new NotSupportedException($"{context.Info.Id} has no length along a bar: it is a component, sized by the room its folder gives it.");
        }
        var fit = context.Fit;
        var pill = SwitchIsland.GlyphSide(fit, fit.Glyph());
        var options = context.Options.Switch ?? new SwitchOptions();
        if (context.Vertical || !options.ShowLabel)
        {
            return WidgetShape.One(GlyphAndText.Square(context.Metrics, fit, pill));
        }
        var title = context.Info.Title;
        var label = TextLine.Template(context.Gfx, title, fit.Primary, Weight.Regular).Width;
        return WidgetShape.One((2 * fit.Edge) + pill + fit.Gap + label);
    }

    /// <summary>
    /// A folder's button: its faces (<see cref="FaceDrawings"/>) side by side, each the room what it
    /// shows now needs (docs\widgets.md §244), read from the folder being measured
    /// (<see cref="ShapeContext.Island"/>), its name where it shows one, and the button's ends —
    /// exactly that, across a bar and down one, never a cell for a lone glyph (§275). It never
    /// counts what is inside: a folder with one widget is as wide as one with nine. §474: Windows' own faces
    /// (<see cref="WindowsFaces"/>) the same, their faces the fixed ones (<see cref="FolderIsland.FacesOf"/>).
    /// </summary>
    private static WidgetShape Folder(in ShapeContext context)
    {
        var machine = context.Machine;
        var options = FolderIsland.FacesOf(context.Options, () => machine.HasBattery);
        return WidgetShape.Hug(FolderIsland.Length(context.Gfx, options, context.Metrics, context.Machine, context.Faces, context.Island as FolderIsland));
    }
}
