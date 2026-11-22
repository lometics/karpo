// The volume as a slider on the bar itself: a speaker that mutes and unmutes,
// and a track the person drags, clicks or turns the wheel over. It follows the
// default output live (VolumeWatch: Core Audio's own callbacks, no polling) and
// sets it straight away, so it is the same volume the tray's icon shows.
//
// The slider runs along the bar by default (sideways on a bar at the bottom or
// top, upright on one at a side) and can be set to either direction: a slider
// across the bar is as long as the bar is thick. Its length is the person's
// (VolumeOptions.Length), clamped to what the bar has room for.
//
// Sizes follow the rules every island uses (Fit): the speaker on one of the icon
// font's sizes, a 4 DIP track with a thumb that stays inside the bar's thickness.

using System.Numerics;
using Karpo.Core;
using Karpo.Dock.Native;
using Karpo.Dock.QuickSettings.Controls;
using Karpo.Dock.QuickSettings.Services;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock;

internal sealed unsafe partial class VolumeIsland : CellIsland, ISliderIsland
{
    // Segoe Fluent Icons: the volume levels and muted, as the taskbar's own button.
    private static readonly string[] Levels = [Glyph(0xE992), Glyph(0xE993), Glyph(0xE994), Glyph(0xE995)];
    private static readonly string Muted = Glyph(0xE74F);

    private readonly VolumeWatch watch;
    private readonly DataSources sources;

    // The one source every volume widget reads (VolumeData), followed since the dock's start; null
    // for a widget a test has handed a stand-in output (VolumeWatch.Stand), which it watches itself.
    private readonly VolumeData? data;
    private VolumeOptions options;

    // Where a drag took hold of the thumb.
    private float grab;
    private bool dragging;
    private bool moved;

    public VolumeIsland(string key, WidgetItem island, Gfx gfx, Config config)
        : this(key, island, gfx, config, DataSources.Shared)
    {
    }

    internal VolumeIsland(string key, WidgetItem island, Gfx gfx, Config config, DataSources sources)
        : base(key, island, gfx)
    {
        options = island.Volume ?? new VolumeOptions();
        this.sources = sources;
        var held = VolumeWatch.Stand is null ? sources.Hold(island) : null;
        if (held is VolumeData shared)
        {
            data = shared;
            watch = shared.Watch;
            shared.Listen(Post);
        }
        else
        {
            sources.Release(held);
            watch = new VolumeWatch(Post);
        }
    }

    /// <summary>Nothing ticks: Core Audio says when the level or the output changed.</summary>
    public override int IntervalMs => 0;

    protected override int Count => 1;

    /// <summary>The level as it is now, 0 to 100 (for tests).</summary>
    internal int Level => watch.Level;

    internal bool IsMuted => watch.Muted;

    public override void Configure(WidgetItem island, Config config)
    {
        // Its size is worked out from these (WidgetShapes), so they are kept in step.
        Settings = island;
        options = island.Volume ?? new VolumeOptions();
    }

    public override string? Tooltip(int index) => !watch.Present
        ? UnavailableOn(0)
        : watch.Muted ? S.WidgetVolumeMutedTip : S.WidgetVolumeLevel(watch.Level);

    /// <summary>With no output: the slider at half, greyed, and why (docs\widgets.md §247).</summary>
    internal override string? UnavailableOn(nint screen) => watch.Present ? null : NotOnThisPc(Info.Title, S.WidgetVolumeNoDevice);

    /// <summary>The level the slider is drawn at with no output (<see cref="UnavailableOn"/>).</summary>
    internal const int UnavailableLevel = 50;

    /// <summary>
    /// Mutes and unmutes: a click only reaches here off the slider (a press on it becomes a drag).
    /// </summary>
    public override IslandAction Click(int index)
    {
        watch.SetMuted(!watch.Muted);
        Post();
        return default;
    }

    /// <summary>Reads what the audio callbacks reported; the cell's length never depends on the level.</summary>
    protected override bool Sample(bool everything)
    {
        _ = watch.Read();
        return false;
    }

    public override void Dispose()
    {
        if (data is not null)
        {
            data.Unlisten(Post);
            sources.Release(data);
        }
        else
        {
            watch.Dispose();
        }
        base.Dispose();
    }

    // --- Size and shape ----------------------------------------------------------

    // stage 3: the size is WidgetShapes' now, a function of the settings and the bar's
    // numbers; there is no Length here to reach anything live (docs\widgets.md §4).

    /// <summary>How much room the cell has across the bar: its thickness.</summary>
    internal static float CellAcross(in IslandStyle style) => style.Thickness;

    /// <summary>
    /// Where the speaker and the track go for <paramref name="style"/>, with <paramref name="across"/> px
    /// across the bar. Pure: the tests check it, and the tile uses it too.
    /// </summary>
    internal VolumePlan Plan(in IslandStyle style, float across, float along = 0) => Plan(options, style, across, along);

    /// <summary>
    /// The plan for <paramref name="options"/>, without an island: what the tests measure.
    /// <paramref name="along"/> is the room the cell really got along the bar, where that is known:
    /// the grid rounds a widget up to whole cells (sizing), and a slider running along the bar
    /// fills what it was given rather than leaving the rest of its cell empty.
    /// </summary>
    internal static VolumePlan Plan(VolumeOptions options, in IslandStyle style, float across, float along = 0)
    {
        var fit = new Fit(style);
        var speaker = fit.Glyph();
        var glyph = options.ShowSpeaker ? speaker : 0;
        var gap = options.ShowSpeaker ? fit.Gap : 0;
        var thumb = MathF.Min(fit.Px(16), MathF.Max(fit.Px(10), across - 2 * fit.Px(2)));
        var track = MathF.Max(2, fit.Px(4));
        var upright = options.Orientation switch
        {
            SliderOrientation.Horizontal => false,
            SliderOrientation.Vertical => true,
            _ => style.Vertical,
        };
        // Along the bar the slider is as long as it was set to; across it, as long as the bar is thick.
        var room = MathF.Max(thumb, across - (2 * fit.Side));
        var wanted = fit.Px(options.Length);
        var alongBar = upright == style.Vertical;
        var length = alongBar ? wanted : MathF.Min(wanted, room);
        var span = 2 * fit.Edge + glyph + gap + (alongBar ? length : thumb);
        if (alongBar && along > 0)
        {
            // The cell got exactly this much room — the grid rounds a widget up to whole cells,
            // and a width somebody chose is given exactly that many — so the track takes what is
            // left of it, whether that is more than the slider asked for or less. It used to only
            // ever grow, so a slider squeezed into fewer cells than it wanted was drawn at its
            // full length and cut off partway.
            length = MathF.Max(0, along - 2 * fit.Edge - glyph - gap);
            span = along;
        }
        if (length < 2 * thumb)
        {
            // No slider here. The thumb has to have somewhere to go: a track shorter than two
            // thumbs gives less travel than the thumb's own width, so one twitch of the pointer
            // crosses the whole range and the control cannot be aimed. What is left is the
            // speaker, which mutes — the widget's short form, the way the weather has one.
            //
            // The speaker is drawn whether or not the settings asked for one: a widget that can
            // show neither a track nor a glyph is a blank cell, and a blank cell is never an
            // answer. The length is a setting in DIPs, so this is the only thing that can give.
            glyph = speaker;
            gap = 0;
            length = 0;
            span = GlyphAndText.Square(style, fit, speaker);
        }
        return new VolumePlan(upright, alongBar, span, across, glyph, gap, length, thumb, track, fit.Edge, style.Vertical);
    }

    // --- Drawing -------------------------------------------------------------------

    internal override void PaintCell(ID2D1DeviceContext* dc, int index, in IslandStyle style, float width, float height)
    {
        var plan = Plan(style, style.Vertical ? width : height, style.Vertical ? height : width);
        // No output: the slider as it would be, which the bar greys (UnavailableOn).
        var present = watch.Present;
        Paint(dc, plan, new D2D_RECT_F(0, 0, width, height), new IslandInk(style.Look), style, present ? watch.Level : UnavailableLevel, present && watch.Muted);
    }

    /// <summary>The speaker and the slider in <paramref name="box"/> px, as the bar and a folder's card both draw them.</summary>
    internal void Paint(ID2D1DeviceContext* dc, in VolumePlan plan, D2D_RECT_F box, in IslandInk ink, in IslandStyle style, int level, bool muted)
    {
        var glyphBox = plan.GlyphBox(box);
        if (plan.Glyph > 0)
        {
            dc->SetTextAntialiasMode(D2D1_TEXT_ANTIALIAS_MODE.D2D1_TEXT_ANTIALIAS_MODE_GRAYSCALE);
            var code = muted ? Muted : Levels[Math.Clamp(level, 0, 100) switch
            {
                <= 0 => 0,
                <= 33 => 1,
                <= 66 => 2,
                _ => 3,
            }];
            PopupPaint.Glyph(dc, Gfx, code, plan.Glyph, ink.Text, glyphBox);
        }
        if (!plan.Slider)
        {
            // The short form: the speaker said it all.
            return;
        }
        var track = plan.TrackBox(box);
        var radius = plan.Track / 2;
        PopupPaint.Fill(dc, track, radius, ink.Track);
        var filled = plan.Filled(box, level);
        if (filled.right > filled.left && filled.bottom > filled.top)
        {
            PopupPaint.Fill(dc, filled, radius, !muted ? ink.Accent : ink.Disabled);
        }
        var centre = plan.ThumbCentre(box, level);
        var thumb = plan.Thumb / 2;
        // The thumb as the quick settings draw it: a pale disc with the accent inside.
        PopupPaint.Circle(dc, centre, thumb, ink.Text);
        PopupPaint.Circle(dc, centre, MathF.Max(1, thumb - MathF.Max(1, plan.Track / 2)), !muted ? ink.Accent : ink.Disabled);
    }

    // --- The pointer ------------------------------------------------------------------

    /// <summary>
    /// A press at <paramref name="point"/> (cell px) on a cell <paramref name="size"/> px large: true when
    /// the slider took it (the press was on its track or thumb, and a drag follows).
    /// </summary>
    public bool Press(int index, Vector2 point, Vector2 size, in IslandStyle style)
    {
        var plan = Plan(style, style.Vertical ? size.X : size.Y, style.Vertical ? size.Y : size.X);
        var box = new D2D_RECT_F(0, 0, size.X, size.Y);
        if (!plan.OnSlider(box, point) || !watch.Present)
        {
            return false;
        }
        var at = plan.ThumbCentre(box, watch.Level);
        var along = plan.Upright ? point.Y - at.Y : point.X - at.X;
        // On the thumb the drag keeps where it took hold; elsewhere the thumb jumps there.
        grab = MathF.Abs(along) <= plan.Thumb / 2 ? along : 0;
        dragging = true;
        // A drag starts unmoved: a wheel notch before it moved the level too, and its tick is the
        // wheel's (Feedback.Notch), not this drag's.
        moved = false;
        Set(plan.LevelAt(box, point, grab));
        return true;
    }

    /// <summary>The pointer moved while the slider has it.</summary>
    public void Drag(int index, Vector2 point, Vector2 size, in IslandStyle style)
    {
        if (!dragging)
        {
            return;
        }
        var plan = Plan(style, style.Vertical ? size.X : size.Y, style.Vertical ? size.Y : size.X);
        Set(plan.LevelAt(new D2D_RECT_F(0, 0, size.X, size.Y), point, grab));
    }

    public void Release()
    {
        // The end of a drag is one settled level, however many it passed through: one tick.
        if (dragging && moved)
        {
            Feedback.Volume(watch.Level, watch.Muted);
        }
        dragging = false;
        moved = false;
    }

    /// <summary>A wheel turn over the cell: two per notch, as Windows' volume does.</summary>
    public bool Wheel(int delta, in IslandStyle style)
    {
        if (!watch.Present)
        {
            return false;
        }
        var notches = delta / 120;
        if (notches == 0)
        {
            notches = delta > 0 ? 1 : -1;
        }
        if (Set(watch.Level + notches * 2))
        {
            // One of a run of notches: the tick waits for the wheel to rest, as Windows' own does.
            Feedback.Notch(watch.Level, watch.Muted);
        }
        return true;
    }

    /// <summary>narrator: a screen reader set the slider (RangeValuePattern): the level, and the tick a settled level plays.</summary>
    internal void SetLevel(int level)
    {
        if (Set(level))
        {
            Feedback.Volume(watch.Level, watch.Muted);
        }
    }

    /// <summary>Sets the level; true when it moved.</summary>
    private bool Set(int level)
    {
        var next = Math.Clamp(level, 0, 100);
        if (next == watch.Level)
        {
            return false;
        }
        watch.SetLevel(next);
        moved = true;
        // The audio callback follows, but the bar shows it now.
        Post();
        return true;
    }

    private static string Glyph(int code) => char.ConvertFromUtf32(code);

    /// <summary>The speaker for a level, as the taskbar's own button draws it.</summary>
    internal static string Speaker(int level, bool muted, bool present) => !present || muted
        ? char.ConvertFromUtf32(0xE74F)
        : char.ConvertFromUtf32(Math.Clamp(level, 0, 100) switch
        {
            <= 0 => 0xE992,
            <= 33 => 0xE993,
            <= 66 => 0xE994,
            _ => 0xE995,
        });
}

/// <summary>
/// The volume slider's parts, in the cell's px: pure, so the layout and what a point on it means
/// are tested without a window.
/// </summary>
/// <param name="Upright">The slider runs up and down (its 0 is at the bottom).</param>
/// <param name="AlongBar">It runs along the bar, rather than across it.</param>
/// <param name="Along">How long the whole cell is along the bar.</param>
/// <param name="Across">How much room the cell has across the bar.</param>
/// <param name="Glyph">The speaker's size; 0 when it is off.</param>
/// <param name="BarVertical">The bar itself stands on a side.</param>
internal readonly record struct VolumePlan(
    bool Upright,
    bool AlongBar,
    float Along,
    float Across,
    float Glyph,
    float Gap,
    float Length,
    float Thumb,
    float Track,
    float Edge,
    bool BarVertical)
{
    /// <summary>
    /// Whether there is a slider at all. False is the widget's <b>short form</b>: the speaker
    /// alone, which mutes. A track that gives less travel than the thumb's own width cannot be
    /// aimed, so below that the slider is not drawn, not hit-tested and not dragged - one answer,
    /// read by the size, the drawing and the pointer alike (see <see cref="VolumeIsland.Plan"/>).
    /// </summary>
    public bool Slider => Length > 0;

    /// <summary>A sideways slider <paramref name="length"/> px long, standing alone in a line; none where the thumb could not travel its own width.</summary>
    internal static VolumePlan Sideways(float length, float thumb, float track, float across) =>
        new(false, true, 0, across, 0, 0, length >= 2 * thumb ? length : 0, thumb, track, 0, false);

    /// <summary>The speaker's square in <paramref name="box"/> (the cell).</summary>
    public D2D_RECT_F GlyphBox(D2D_RECT_F box)
    {
        if (Glyph <= 0)
        {
            return new D2D_RECT_F(box.left, box.top, box.left, box.top);
        }
        var (width, height) = (box.right - box.left, box.bottom - box.top);
        if (!Slider)
        {
            // The short form: the speaker is the whole widget, so it sits in the middle of the
            // cell rather than first along a bar with nothing after it.
            return Rect(box.left + (width - Glyph) / 2, box.top + (height - Glyph) / 2, Glyph, Glyph);
        }
        // The speaker sits first along the bar, in the middle across it.
        return BarVertical
            ? Rect(box.left + (width - Glyph) / 2, box.top + Edge, Glyph, Glyph)
            : Rect(box.left + Edge, box.top + (height - Glyph) / 2, Glyph, Glyph);
    }

    /// <summary>The track's rectangle: the line the thumb runs along.</summary>
    public D2D_RECT_F TrackBox(D2D_RECT_F box)
    {
        var (width, height) = (box.right - box.left, box.bottom - box.top);
        var after = Glyph > 0 ? Glyph + Gap : 0;
        if (Upright)
        {
            // Upright: its length runs down the cell, half a thumb in at each end.
            var top = BarVertical ? box.top + Edge + after : box.top + (height - Run) / 2;
            var x = BarVertical
                ? box.left + width / 2
                : box.left + Edge + after + Thumb / 2;
            return Rect(x - Track / 2, top, Track, Run);
        }
        // Sideways: across a bar at a side (under the speaker), along one at the bottom or top.
        var left = BarVertical ? box.left + (width - Run) / 2 : box.left + Edge + after;
        var y = BarVertical ? box.top + Edge + after + Thumb / 2 : box.top + height / 2;
        return Rect(left, y - Track / 2, Run, Track);
    }

    /// <summary>How long the track is: the length that was asked for, or the room across the bar.</summary>
    public float Run => Length;

    /// <summary>Where the thumb's centre is for <paramref name="level"/>.</summary>
    public Vector2 ThumbCentre(D2D_RECT_F box, int level)
    {
        var track = TrackBox(box);
        var fraction = Math.Clamp(level, 0, 100) / 100f;
        if (Upright)
        {
            var low = track.bottom - Thumb / 2;
            var high = track.top + Thumb / 2;
            return new Vector2((track.left + track.right) / 2, low + (high - low) * fraction);
        }
        var start = track.left + Thumb / 2;
        var end = track.right - Thumb / 2;
        return new Vector2(start + (end - start) * fraction, (track.top + track.bottom) / 2);
    }

    /// <summary>The filled part of the track, from its start to the thumb.</summary>
    public D2D_RECT_F Filled(D2D_RECT_F box, int level)
    {
        var track = TrackBox(box);
        var at = ThumbCentre(box, level);
        return Upright
            ? new D2D_RECT_F(track.left, at.Y, track.right, track.bottom)
            : new D2D_RECT_F(track.left, track.top, at.X, track.bottom);
    }

    /// <summary>Whether <paramref name="point"/> is on the slider (its track, thumb and the air around them).</summary>
    public bool OnSlider(D2D_RECT_F box, Vector2 point)
    {
        if (!Slider)
        {
            return false;
        }
        var track = TrackBox(box);
        var reach = Thumb / 2 + Track;
        return point.X >= track.left - reach && point.X <= track.right + reach
            && point.Y >= track.top - reach && point.Y <= track.bottom + reach;
    }

    /// <summary>Whether <paramref name="point"/> is on the speaker.</summary>
    public bool OnGlyph(D2D_RECT_F box, Vector2 point)
    {
        if (Glyph <= 0)
        {
            return false;
        }
        var glyph = GlyphBox(box);
        var reach = Gap / 2;
        return point.X >= glyph.left - reach && point.X <= glyph.right + reach
            && point.Y >= glyph.top - reach && point.Y <= glyph.bottom + reach;
    }

    /// <summary>The level <paramref name="point"/> stands for, with <paramref name="grab"/> px taken off (where the thumb was held).</summary>
    public int LevelAt(D2D_RECT_F box, Vector2 point, float grab = 0)
    {
        var track = TrackBox(box);
        if (Upright)
        {
            var low = track.bottom - Thumb / 2;
            var high = track.top + Thumb / 2;
            return Percent(low - (point.Y - grab), low - high);
        }
        var start = track.left + Thumb / 2;
        var end = track.right - Thumb / 2;
        return Percent(point.X - grab - start, end - start);
    }

    private static int Percent(float along, float run) =>
        run <= 0 ? 0 : (int)MathF.Round(Math.Clamp(along / run, 0, 1) * 100, MidpointRounding.AwayFromZero);

    private static D2D_RECT_F Rect(float x, float y, float width, float height) => new(x, y, x + width, y + height);
}
