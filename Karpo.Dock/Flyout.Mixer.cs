// menus, §523: a colour of one's own, mixed in a colour row's picker itself (2026-11-07, the person: "improve the color
// picker, the custom color should be integrated into the normal color picker"; docs\widgets.md §523). The rows are
// MenuPickers.Mixer's, under the swatches, ruled off from them; this is how a menu draws them and takes the pointer and the
// keys on them. The arithmetic is the core's (Karpo.Core\ColorMix.cs).
//
//  - **The spectrum** (FlyoutMix, MixPart.Spectrum): a square of the hue in force, white across it from its left, black up
//    it from its bottom — saturation across, value up —, 96 DIP tall (§613), as wide as its column (§627: beside the
//    swatches; as wide as their lines under them until then), 4 DIP round, a
//    hairline round it; a ring where the colour stands.
//  - **The hue** and, where the value takes one, **the opacity** (MixPart.Hue, MixPart.Opacity): a track 12 DIP tall in a
//    row 32 DIP tall — a rainbow; the colour from none to whole over a checker —, a ring where it stands.
//  - **The hex box** (FlyoutField.Hex): a line of text, "#RRGGBB", "#RRGGBBAA" where the value takes an alpha, its swatch
//    32 DIP square before it, of the colour it says; Enter or leaving it keeps a colour typed, Escape puts it back
//    (Flyout.Fields.cs, FieldPicks.KeepRow).
//  - **The pointer**: a press on the spectrum or a track puts the colour where it went and holds it, the colour following
//    the pointer anywhere, held at the edges; the menu says each colour as it changes (FlyoutEventKind.Mix: the bars and
//    the widgets wear it a moment, nothing kept), and the colour let go (MixKept: set, saved, the first recent colour).
//  - **The keys**: Tab and the arrows walk onto them as onto any row; on the spectrum the four arrows move the point a
//    step, Page Up and Page Down the value ten; on a slider Left and Right a step, Page Up and Page Down ten, Home and End to
//    its ends, Up and Down on to the next row. The colour is looked at at each step and kept once the keys leave the picker's
//    rows, or by Enter or Space. Escape while a colour is being mixed puts the one before back (MixUndone).
//  - **What stands still**: the hue the slider stands at, which a grey has none of its own of, is held while the menu is
//    open (mixHeld), so a colour mixed to grey and shown again keeps its hue under the ring.
//  - **A screen reader** finds the hue and the opacity sliders, ranges it can set; the spectrum a slider saying the colour;
//    the hex box a value it can read and set; the swatches a list of colours, the one in force selected (Flyout.BuildUia).

using System.Numerics;
using Karpo.Core;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using Windows.UI.Composition;
using static TerraFX.Interop.Windows.Windows;
using Color = Windows.UI.Color;

namespace Karpo.Dock;

/// <summary>
/// menus, §523: a part of a colour row's picker (Flyout.Mixer.cs): <paramref name="Part"/>, mixing from <paramref name="Color"/>,
/// the colour the row is set to now (or what its picker starts from, MenuPickers.Mixed). The row's own id is its command.
/// </summary>
public sealed record FlyoutMix(MixPart Part, Argb Color) : FlyoutControl;

public sealed unsafe partial class Flyout
{
    /// <summary>
    /// §523: the spectrum's height, a slider's and the hex box's row, the picker's inset from a row's fill, in DIP. §613 the
    /// spectrum 96 (128 until then) as its picker grew wider (PanelMetrics.PickerWide): "wider but not as tall anymore".
    /// </summary>
    internal const float SpectrumDip = 96;
    internal const float MixRowDip = 32;
    internal const float MixInsetDip = 4;

    /// <summary>§523: a slider's track, the ring that says where a colour stands, the room between the hex box's swatch and its box, in DIP.</summary>
    internal const float TrackDip = 12;
    internal const float ThumbDip = 16;
    internal const float HexGapDip = 8;

    /// <summary>
    /// §523: a mix said from a submenu's window, or kept as the keys left the picker: wParam the event's kind (high byte) and
    /// the row's id, lParam the colour, ARGB.
    /// </summary>
    private const uint WM_KARPO_FLYOUT_MIX = WM.WM_USER + 0x73;

    /// <summary>§523: a colour being mixed: from the row <see cref="Id"/>, what it was before, where it is now.</summary>
    private sealed class Mixing
    {
        public required uint Id;

        public required Argb Start;

        public required Hsv Hsv;

        public required byte A;

        /// <summary>The part the pointer holds; null where the keys mix.</summary>
        public MixPart? Held;

        /// <summary>The last colour the menu said; whether it said any.</summary>
        public Argb Said;

        public bool Told;

        public Argb Color => ColorMix.ToArgb(Hsv, A);
    }

    // §523: the colour being mixed, null for none; the hue the last one stood at, kept for a grey shown again.
    private Mixing? mixing;
    private (Hsv Hsv, Argb Color)? mixHeld;

    /// <summary>§523: what a part of the picker is drawn with, which a mix moves in place: the box as drawn (content px then).</summary>
    private sealed class MixLook
    {
        /// <summary>The part; null for the hex box's swatch.</summary>
        public required MixPart? Part;

        public required Area Drawn;

        /// <summary>The spectrum's hue; the hex box's swatch's colour.</summary>
        public CompositionColorBrush? Ground;

        public ShapeVisual? Thumb;

        /// <summary>The opacity track's ends: the colour of none, the colour whole.</summary>
        public CompositionColorGradientStop? Clear;
        public CompositionColorGradientStop? Whole;
    }

    private sealed partial class Row
    {
        /// <summary>§523: a part of a colour's picker on it (Flyout.Mixer.cs); null for any other row.</summary>
        public MixLook? Mix;
    }

    /// <summary>§523: whether <paramref name="item"/> is a part of a colour's picker: the spectrum or a slider.</summary>
    internal static bool IsMix(FlyoutItem item) => item.Control is FlyoutMix;

    /// <summary>§523: the fill of a picker's row, in px at <paramref name="scale"/>: the spectrum's, a slider's, the hex box's; 0 for any other row.</summary>
    internal static float MixFill(FlyoutItem item, float scale) => item.Control switch
    {
        FlyoutMix { Part: MixPart.Spectrum } => Round(SpectrumDip * scale),
        FlyoutMix or FlyoutField { Hex: true } => Round(MixRowDip * scale),
        _ => 0,
    };

    /// <summary>
    /// §523: the box of a picker's part on <paramref name="fill"/> (content px): the spectrum the fill less the picker's inset
    /// either side; a slider's track 12 DIP tall in its middle; the hex box's swatch, a square the row tall at its start.
    /// </summary>
    internal static Area MixBox(MixPart? part, Area fill, float scale)
    {
        var inset = Round(MixInsetDip * scale);
        return part switch
        {
            MixPart.Spectrum => new Area(fill.X + inset, fill.Y, fill.W - (2 * inset), fill.H),
            null => new Area(fill.X + inset, fill.Y, fill.H, fill.H),
            _ => new Area(fill.X + inset, fill.Y + Round((fill.H - Round(TrackDip * scale)) / 2), fill.W - (2 * inset), Round(TrackDip * scale)),
        };
    }

    /// <summary>
    /// §523: where the ring stands on <paramref name="box"/> (a part's, content px) for <paramref name="hsv"/> at
    /// <paramref name="alpha"/>: its middle.
    /// </summary>
    internal static Vector2 MixPoint(MixPart part, Area box, Hsv hsv, byte alpha)
    {
        switch (part)
        {
            case MixPart.Spectrum:
            {
                var (x, y) = ColorMix.SpectrumPoint(hsv, box.W, box.H);
                return new Vector2(box.X + (float)x, box.Y + (float)y);
            }
            case MixPart.Hue:
                return new Vector2(box.X + (float)ColorMix.SliderPoint(hsv.H, box.W, ColorMix.MostHue), box.Y + (box.H / 2));
            default:
                return new Vector2(box.X + (float)ColorMix.SliderPoint(ColorMix.Opacity(new Argb(alpha, 0, 0, 0)), box.W, ColorMix.MostOpacity), box.Y + (box.H / 2));
        }
    }

    /// <summary>
    /// §523: the colour a picker of rows saying <paramref name="said"/> shows: a mix under way's, else the rows', its hue the one
    /// held where the rows say the colour it was held for (a grey has none of its own).
    /// </summary>
    private (Hsv Hsv, byte A) MixShown(Argb said) =>
        mixing is { } under ? (under.Hsv, under.A)
        : (mixHeld is { } held && held.Color == (said with { A = 255 }) ? held.Hsv : ColorMix.ToHsv(said), said.A);

    // §523: the picker the rows shown have: the colour they say (their first part's) and whether it takes an alpha (an
    // opacity slider among them); null for none. Found as they are shown, before they are drawn (MixFound).
    private (Argb Said, bool Alpha)? mixFound;

    /// <summary>§523: the picker among <paramref name="items"/>, about to be shown (<see cref="mixFound"/>).</summary>
    private void MixFound(IReadOnlyList<FlyoutItem> items) =>
        mixFound = items.FirstOrDefault(item => item.Control is FlyoutMix)?.Control is FlyoutMix mix
            ? (mix.Color, items.Any(item => item.Control is FlyoutMix { Part: MixPart.Opacity }))
            : null;

    /// <summary>§523: the colour the picker's rows on this level say; null where there are none.</summary>
    private Argb? MixSaid => mixFound?.Said;

    /// <summary>§523: whether the picker on this level takes an alpha: it has an opacity slider.</summary>
    private bool MixAlpha => mixFound?.Alpha == true;

    /// <summary>§523: a part of a colour's picker drawn into <paramref name="into"/> on <paramref name="row"/> (see the top of the file).</summary>
    private void DrawMix(Compositor c, ContainerVisual into, Row row, FlyoutMix mix)
    {
        float Px(float dip) => MathF.Round(dip * scale);
        var (hsv, alpha) = MixShown(mix.Color);
        var box = MixBox(mix.Part, row.Bounds, scale);
        var look = new MixLook { Part = mix.Part, Drawn = box };
        var radius = Px(4);
        var hairline = Px(1);
        // Lit — under the pointer, or the keys' — a ring off it, as a swatch's is lit (Flyout.Pickers.cs).
        var gap = Px(2);
        var thick = Px(2);
        var ring = MixShape(c, into, row, new Vector2(box.X - gap - thick, box.Y - gap - thick), new Vector2(box.W + (2 * (gap + thick)), box.H + (2 * (gap + thick))), radius + gap + thick, null, Colour(theme.TextSecondary), thick);
        ring.Opacity = 0;
        row.Lit = on => Try(() => ring.Opacity = on ? 1 : 0);
        var edge = theme.ControlStroke;
        switch (mix.Part)
        {
            case MixPart.Spectrum:
            {
                // The hue, white across it from its left, black up it from its bottom.
                look.Ground = Own(c.CreateColorBrush(Ink(ColorMix.Pure(hsv.H))));
                var across = Gradient(c, new Vector2(0, 0), new Vector2(box.W, 0), (0, Color.FromArgb(255, 255, 255, 255)), (1, Color.FromArgb(0, 255, 255, 255)));
                var down = Gradient(c, new Vector2(0, 0), new Vector2(0, box.H), (0, Color.FromArgb(0, 0, 0, 0)), (1, Color.FromArgb(255, 0, 0, 0)));
                _ = MixShape(c, into, row, new Vector2(box.X, box.Y), new Vector2(box.W, box.H), radius, null, null, 0, look.Ground, across, down);
                break;
            }
            case MixPart.Hue:
            {
                var stops = Enumerable.Range(0, 7).Select(at => (at / 6f, Ink(ColorMix.Pure(at * 60.0)))).ToArray();
                _ = MixShape(c, into, row, new Vector2(box.X, box.Y), new Vector2(box.W, box.H), radius, null, null, 0, Gradient(c, Vector2.Zero, new Vector2(box.W, 0), stops));
                break;
            }
            case MixPart.Opacity:
            {
                // The colour from none to whole, over the checker a see-through swatch has (Flyout.Pickers.cs).
                _ = Checker(c, into, row, box, radius);
                var colour = ColorMix.ToArgb(hsv);
                var track = Gradient(c, Vector2.Zero, new Vector2(box.W, 0), (0, Ink(colour with { A = 0 })), (1, Ink(colour)));
                (look.Clear, look.Whole) = (track.ColorStops[0], track.ColorStops[1]);
                _ = MixShape(c, into, row, new Vector2(box.X, box.Y), new Vector2(box.W, box.H), radius, null, null, 0, track);
                break;
            }
        }
        // A hairline round it, as round a swatch.
        _ = MixShape(c, into, row, new Vector2(box.X, box.Y), new Vector2(box.W, box.H), radius, null, edge, hairline);
        look.Thumb = Thumb(c, into, row);
        row.Mix = look;
        PlaceThumb(look, hsv, alpha);
        if (!row.Item.Enabled)
        {
            foreach (var (part, _) in row.Parts)
            {
                Try(() => part.Opacity = 0.4f);
            }
        }
    }

    /// <summary>§523: the hex box's swatch at the start of its row (<paramref name="box"/>, content px), of <paramref name="colour"/>, over a checker.</summary>
    private void DrawHexSwatch(Compositor c, ContainerVisual into, Row row, Area box, Argb colour)
    {
        var radius = MathF.Round(4 * scale);
        _ = Checker(c, into, row, box, radius);
        var look = new MixLook { Part = null, Drawn = box, Ground = Own(c.CreateColorBrush(Ink(colour))) };
        _ = MixShape(c, into, row, new Vector2(box.X, box.Y), new Vector2(box.W, box.H), radius, null, theme.ControlStroke, MathF.Round(scale), look.Ground);
        row.Mix = look;
    }

    /// <summary>
    /// §523: a rounded box at <paramref name="at"/> of <paramref name="size"/>, filled with each of <paramref name="fills"/> in
    /// turn (over <paramref name="body"/>), its rim <paramref name="rim"/> <paramref name="thick"/> px; made into the row.
    /// </summary>
    private ShapeVisual MixShape(Compositor c, ContainerVisual into, Row row, Vector2 at, Vector2 size, float radius, Color? body, Color? rim, float thick, params CompositionBrush[] fills)
    {
        var visual = Own(c.CreateShapeVisual());
        visual.Size = size;
        visual.Offset = new Vector3(at, 0);
        CompositionSpriteShape Shape()
        {
            var geometry = Own(c.CreateRoundedRectangleGeometry());
            geometry.Offset = new Vector2(thick / 2);
            geometry.Size = size - new Vector2(thick);
            geometry.CornerRadius = new Vector2(MathF.Max(0, radius - (thick / 2)));
            var shape = Own(c.CreateSpriteShape(geometry));
            visual.Shapes.Add(shape);
            return shape;
        }
        if (body is { } inside)
        {
            Shape().FillBrush = Own(c.CreateColorBrush(inside));
        }
        foreach (var brush in fills)
        {
            Shape().FillBrush = brush;
        }
        if (rim is { } edge)
        {
            var shape = Shape();
            shape.StrokeBrush = Own(c.CreateColorBrush(edge));
            shape.StrokeThickness = thick;
        }
        into.Children.InsertAtTop(visual);
        row.Parts.Add((visual, visual.Offset));
        return visual;
    }

    /// <summary>§523: a straight gradient from <paramref name="from"/> to <paramref name="to"/> (px of the shape it fills) through <paramref name="stops"/>.</summary>
    private CompositionLinearGradientBrush Gradient(Compositor c, Vector2 from, Vector2 to, params (float At, Color Ink)[] stops)
    {
        var brush = Own(c.CreateLinearGradientBrush());
        brush.MappingMode = CompositionMappingMode.Absolute;
        brush.StartPoint = from;
        brush.EndPoint = to;
        foreach (var (at, ink) in stops)
        {
            brush.ColorStops.Add(Own(c.CreateColorGradientStop(at, ink)));
        }
        return brush;
    }

    /// <summary>§523: the checker under what is see-through (§509's, Flyout.Pickers.cs) on <paramref name="box"/>, its corners <paramref name="radius"/>.</summary>
    private SpriteVisual? Checker(Compositor c, ContainerVisual into, Row row, Area box, float radius)
    {
        if (gfx is not { } g || box.W < 1 || box.H < 1)
        {
            return null;
        }
        var secondary = Colour(theme.TextSecondary);
        var pattern = new Patterned(Pattern.Checks, new Argb(CheckerAlpha, secondary.R, secondary.G, secondary.B));
        var (w, h) = (box.W, box.H);
        var sprite = Own(c.CreateSpriteVisual());
        sprite.Brush = Own(c.CreateSurfaceBrush(Own(g.Draw(w, h, dc => PatternPaint.Fill(dc, pattern, scale, new D2D_RECT_F(0, 0, w, h), Vector2.Zero, radius)))));
        sprite.Size = new Vector2(w, h);
        sprite.Offset = new Vector3(box.X, box.Y, 0);
        into.Children.InsertAtTop(sprite);
        row.Parts.Add((sprite, sprite.Offset));
        return sprite;
    }

    /// <summary>§523: the ring that says where a colour stands: white, a dark hairline round it, so it shows on every colour.</summary>
    private ShapeVisual Thumb(Compositor c, ContainerVisual into, Row row)
    {
        var side = MathF.Round(ThumbDip * scale);
        var visual = Own(c.CreateShapeVisual());
        visual.Size = new Vector2(side);
        void Ring(float radius, Color ink, float thick)
        {
            var round = Own(c.CreateEllipseGeometry());
            round.Center = new Vector2(side / 2);
            round.Radius = new Vector2(radius);
            var shape = Own(c.CreateSpriteShape(round));
            shape.StrokeBrush = Own(c.CreateColorBrush(ink));
            shape.StrokeThickness = thick;
            visual.Shapes.Add(shape);
        }
        var hairline = MathF.Max(1, MathF.Round(scale));
        var white = MathF.Max(2, MathF.Round(2 * scale));
        Ring((side / 2) - (hairline / 2), Color.FromArgb(0x99, 0, 0, 0), hairline);
        Ring((side / 2) - hairline - (white / 2), Color.FromArgb(255, 255, 255, 255), white);
        Ring((side / 2) - hairline - white - (hairline / 2), Color.FromArgb(0x66, 0, 0, 0), hairline);
        into.Children.InsertAtTop(visual);
        row.Parts.Add((visual, visual.Offset));
        return visual;
    }

    /// <summary>§523: the ring of <paramref name="look"/> where <paramref name="hsv"/> at <paramref name="alpha"/> stands.</summary>
    private void PlaceThumb(MixLook look, Hsv hsv, byte alpha)
    {
        if (look is not { Part: { } part, Thumb: { } thumb })
        {
            return;
        }
        var side = MathF.Round(ThumbDip * scale);
        var middle = MixPoint(part, look.Drawn, hsv, alpha);
        Try(() => thumb.Offset = new Vector3(MathF.Round(middle.X - (side / 2)), MathF.Round(middle.Y - (side / 2)), 0));
    }

    /// <summary>
    /// §523: the picker shows the colour being mixed (or, none, what its rows say), in place: the spectrum's hue, every ring,
    /// the opacity's track, the hex box's swatch and its words where it has not the keys.
    /// </summary>
    private void MixShow()
    {
        if (MixSaid is not { } said)
        {
            return;
        }
        var (hsv, alpha) = MixShown(said);
        var colour = ColorMix.ToArgb(hsv, alpha);
        foreach (var row in rows)
        {
            if (row.Mix is not { } look)
            {
                continue;
            }
            switch (look.Part)
            {
                case MixPart.Spectrum when look.Ground is { } ground:
                    Try(() => ground.Color = Ink(ColorMix.Pure(hsv.H)));
                    break;
                case MixPart.Opacity when look is { Clear: { } clear, Whole: { } whole }:
                    Try(() =>
                    {
                        clear.Color = Ink(colour with { A = 0 });
                        whole.Color = Ink(colour with { A = 255 });
                    });
                    break;
                case null when look.Ground is { } swatch:
                    Try(() => swatch.Color = Ink(colour));
                    if (fieldStates.TryGetValue(row.Item.Id, out var state) && !state.Field.Focused)
                    {
                        state.Field.Show(ColorMix.Hex(colour, MixAlpha));
                        RedrawField(state);
                    }
                    break;
            }
            PlaceThumb(look, hsv, alpha);
        }
    }

    /// <summary>§523: the hex box's swatch shows <paramref name="state"/>'s words where they are a colour: what Enter would keep.</summary>
    private void HexTyped(FieldState state)
    {
        if (state.Control.Hex && rows.Find(row => fieldStates.GetValueOrDefault(row.Item.Id) == state) is { Mix.Ground: { } swatch }
            && ColorMix.ParseHex(state.Field.Text, MixAlpha) is { } typed)
        {
            Try(() => swatch.Color = Ink(typed));
        }
    }

    /// <summary>§523: a mix of the picker's row <paramref name="row"/> begun, or the one under way; from the colour shown.</summary>
    private Mixing Begin(Row row, FlyoutMix mix)
    {
        if (mixing is { } under)
        {
            under.Id = row.Item.Id;
            return under;
        }
        var (hsv, alpha) = MixShown(mix.Color);
        var start = ColorMix.ToArgb(hsv, alpha);
        mixing = new Mixing { Id = row.Item.Id, Start = start, Hsv = hsv, A = alpha, Said = start };
        return mixing;
    }

    /// <summary>
    /// §523: a press at <paramref name="x"/>, <paramref name="y"/> (window px) on the spectrum or a slider: the pointer holds it,
    /// the colour where it went; null where it is on neither.
    /// </summary>
    private FlyoutEvent? MixPress(int x, int y)
    {
        var index = RowAt(x, y);
        if (index < 0 || rows[index] is not { Item: { Enabled: true, Control: FlyoutMix mix } } row)
        {
            return null;
        }
        var under = Begin(row, mix);
        under.Held = mix.Part;
        SetHot(index);
        _ = SetCapture(Hwnd);
        return MixTo(row, mix.Part, x, y);
    }

    /// <summary>§523: the pointer moved to <paramref name="x"/>, <paramref name="y"/> (window px), holding a part: the colour follows it.</summary>
    private FlyoutEvent MixMove(int x, int y)
    {
        if (mixing is not { Held: { } part } under || rows.Find(row => row.Item.Id == under.Id) is not { } row)
        {
            return new FlyoutEvent(FlyoutEventKind.Nothing);
        }
        return MixTo(row, part, x, y);
    }

    /// <summary>§523: the colour at <paramref name="x"/>, <paramref name="y"/> (window px) of <paramref name="part"/> on <paramref name="row"/>, held at its edges.</summary>
    private FlyoutEvent MixTo(Row row, MixPart part, int x, int y)
    {
        var under = mixing!;
        var box = MixBox(part, row.Bounds, scale);
        var (cx, cy) = (x - viewport.X - box.X, y - viewport.Y + scroll - box.Y);
        switch (part)
        {
            case MixPart.Spectrum:
                under.Hsv = ColorMix.Spectrum(under.Hsv.H, cx, cy, box.W, box.H);
                break;
            case MixPart.Hue:
                under.Hsv = under.Hsv with { H = ColorMix.Slider(cx, box.W, ColorMix.MostHue) };
                break;
            case MixPart.Opacity:
                under.A = ColorMix.Alpha(ColorMix.Slider(cx, box.W, ColorMix.MostOpacity));
                break;
        }
        return MixChanged();
    }

    /// <summary>§523: the colour being mixed shown, and said where it is another: a look, nothing kept yet.</summary>
    private FlyoutEvent MixChanged()
    {
        var under = mixing!;
        MixShow();
        var now = under.Color;
        if (now == under.Said)
        {
            return new FlyoutEvent(FlyoutEventKind.Nothing);
        }
        under.Said = now;
        under.Told = true;
        return MixSay(FlyoutEventKind.Mix, under.Id, now);
    }

    /// <summary>§523: the pointer let go of the part it held: the colour kept; null where it held none.</summary>
    private FlyoutEvent? MixRelease()
    {
        if (mixing is not { Held: not null } under)
        {
            return null;
        }
        under.Held = null;
        if (GetCapture() == Hwnd)
        {
            _ = ReleaseCapture();
        }
        return MixKeep();
    }

    /// <summary>§523: the colour being mixed kept: said where it changed (MixKept); nothing where it did not.</summary>
    private FlyoutEvent MixKeep()
    {
        if (mixing is not { } under)
        {
            return new FlyoutEvent(FlyoutEventKind.Nothing);
        }
        mixing = null;
        mixHeld = (under.Hsv, under.Color with { A = 255 });
        return under.Told ? MixSay(FlyoutEventKind.MixKept, under.Id, under.Color) : new FlyoutEvent(FlyoutEventKind.Nothing);
    }

    /// <summary>§523: Escape while a colour is mixed: the one before it back (MixUndone), the picker showing it.</summary>
    private FlyoutEvent MixUndo()
    {
        var under = mixing!;
        mixing = null;
        if (GetCapture() == Hwnd)
        {
            _ = ReleaseCapture();
        }
        MixShow();
        return under.Told ? MixSay(FlyoutEventKind.MixUndone, under.Id, under.Start) : new FlyoutEvent(FlyoutEventKind.Nothing);
    }

    /// <summary>§523: the keys left the picker's rows (to <paramref name="index"/>) while they mixed a colour: it is kept, said once the key is done.</summary>
    private void MixLeft(int index)
    {
        if (mixing is { Held: null } && (index < 0 || index >= rows.Count || rows[index].Item.Control is not FlyoutMix) && MixKeep() is { Kind: FlyoutEventKind.MixKept } kept)
        {
            Post(kept);
        }
    }

    /// <summary>
    /// §523: a key on the picker's row lit, <paramref name="mix"/>: on the spectrum the arrows move the point, Page Up and Page
    /// Down the value; on a slider Left and Right a step, Page Up and Page Down ten, Home and End its ends; Enter and Space keep
    /// it. Null for a key it leaves to the menu (Up and Down on a slider, Tab).
    /// </summary>
    private FlyoutEvent? MixKey(Row row, FlyoutMix mix, int key)
    {
        if (key is VK.VK_RETURN or VK.VK_SPACE)
        {
            return MixKeep();
        }
        var (s, v) = (ColorMix.Step / 100, ColorMix.PageStep / 100);
        Func<Mixing, bool>? step = (mix.Part, key) switch
        {
            (MixPart.Spectrum, VK.VK_LEFT) => m => Set(m, m.Hsv with { S = Math.Clamp(m.Hsv.S - s, 0, 1) }),
            (MixPart.Spectrum, VK.VK_RIGHT) => m => Set(m, m.Hsv with { S = Math.Clamp(m.Hsv.S + s, 0, 1) }),
            (MixPart.Spectrum, VK.VK_UP) => m => Set(m, m.Hsv with { V = Math.Clamp(m.Hsv.V + s, 0, 1) }),
            (MixPart.Spectrum, VK.VK_DOWN) => m => Set(m, m.Hsv with { V = Math.Clamp(m.Hsv.V - s, 0, 1) }),
            (MixPart.Spectrum, VK.VK_PRIOR) => m => Set(m, m.Hsv with { V = Math.Clamp(m.Hsv.V + v, 0, 1) }),
            (MixPart.Spectrum, VK.VK_NEXT) => m => Set(m, m.Hsv with { V = Math.Clamp(m.Hsv.V - v, 0, 1) }),
            (MixPart.Hue, VK.VK_LEFT or VK.VK_RIGHT or VK.VK_PRIOR or VK.VK_NEXT or VK.VK_HOME or VK.VK_END) =>
                m => Set(m, m.Hsv with { H = Slide(m.Hsv.H, key, ColorMix.MostHue) }),
            (MixPart.Opacity, VK.VK_LEFT or VK.VK_RIGHT or VK.VK_PRIOR or VK.VK_NEXT or VK.VK_HOME or VK.VK_END) =>
                m => SetAlpha(m, ColorMix.Alpha(Slide(ColorMix.Opacity(new Argb(m.A, 0, 0, 0)), key, ColorMix.MostOpacity))),
            _ => null,
        };
        if (step is null)
        {
            return null;
        }
        var under = Begin(row, mix);
        return step(under) ? MixChanged() : new FlyoutEvent(FlyoutEventKind.Nothing);

        static bool Set(Mixing m, Hsv next)
        {
            var changed = next != m.Hsv;
            m.Hsv = next;
            return changed;
        }

        static bool SetAlpha(Mixing m, byte next)
        {
            var changed = next != m.A;
            m.A = next;
            return changed;
        }

        // A slider's value by a key: a step, ten, its ends.
        static double Slide(double value, int key, double most) => key switch
        {
            VK.VK_HOME => 0,
            VK.VK_END => most,
            VK.VK_LEFT => ColorMix.Stepped(Math.Round(value), -ColorMix.Step, most),
            VK.VK_RIGHT => ColorMix.Stepped(Math.Round(value), ColorMix.Step, most),
            VK.VK_NEXT => ColorMix.Stepped(Math.Round(value), -ColorMix.PageStep, most),
            _ => ColorMix.Stepped(Math.Round(value), ColorMix.PageStep, most),
        };
    }

    /// <summary>
    /// §523: a screen reader set the slider <paramref name="index"/> to <paramref name="value"/> (degrees, percent): set and kept
    /// at once, said to the menu as the pointer's let go is.
    /// </summary>
    private void MixSetValue(int index, double value)
    {
        if (rows[index] is not { Item.Control: FlyoutMix { Part: not MixPart.Spectrum } mix } row)
        {
            return;
        }
        var under = Begin(row, mix);
        if (mix.Part == MixPart.Hue)
        {
            under.Hsv = under.Hsv with { H = Math.Clamp(value, 0, ColorMix.MostHue) };
        }
        else
        {
            under.A = ColorMix.Alpha(value);
        }
        if (MixChanged() is { Kind: FlyoutEventKind.Mix } live)
        {
            Post(live);
        }
        if (MixKeep() is { Kind: FlyoutEventKind.MixKept } kept)
        {
            Post(kept);
        }
    }

    /// <summary>
    /// §523: <paramref name="kind"/> of the colour <paramref name="colour"/> from row <paramref name="id"/>: the root says it; a
    /// submenu tells the root, which says it as it hears it.
    /// </summary>
    private FlyoutEvent MixSay(FlyoutEventKind kind, uint id, Argb colour)
    {
        if (parent is null)
        {
            return new FlyoutEvent(kind, id, Color: colour);
        }
        Post(new FlyoutEvent(kind, id, Color: colour));
        return default;
    }

    /// <summary>§523: <paramref name="said"/> posted to the root's window, which says it as the queue comes to it.</summary>
    private void Post(FlyoutEvent said)
    {
        var root = Root;
        var packed = ((uint)said.Color.A << 24) | ((uint)said.Color.R << 16) | ((uint)said.Color.G << 8) | said.Color.B;
        _ = PostMessageW(root.Hwnd, WM_KARPO_FLYOUT_MIX, (WPARAM)(((nuint)(uint)said.Kind << 24) | (said.Id & 0xFFFFFF)), (LPARAM)(nint)unchecked((int)packed));
    }

    /// <summary>§523: what a mix posted to the root says (<see cref="Post"/>).</summary>
    private static FlyoutEvent MixHeard(WPARAM wParam, LPARAM lParam)
    {
        var packed = (uint)(nint)lParam;
        var kind = (FlyoutEventKind)(int)((nuint)wParam >> 24);
        return new FlyoutEvent(kind, (uint)((nuint)wParam & 0xFFFFFF), Color: new Argb((byte)(packed >> 24), (byte)(packed >> 16), (byte)(packed >> 8), (byte)packed));
    }

    /// <summary>§523: the ids of the lists a run of tiles is in the menu's tree (Flyout.BuildUia), past every row's.</summary>
    private const int TilesUiaId = 0x400000;

    /// <summary>
    /// §523: <paramref name="node"/>, a part of a colour's picker, as a screen reader is told of it: the hue and the opacity
    /// sliders, ranges it can set; the spectrum a slider saying the colour it stands at.
    /// </summary>
    private UiaNode MixNode(UiaNode node, FlyoutMix mix)
    {
        var (hsv, alpha) = MixShown(mix.Color);
        var said = OptionPickers.Say(ColorMix.ToArgb(hsv, alpha));
        return mix.Part switch
        {
            MixPart.Hue => node with { Role = UiaRole.Slider, Pattern = UiaPattern.Range, Status = said, Range = new UiaRange(Math.Round(hsv.H), 0, ColorMix.MostHue, ColorMix.Step, ColorMix.PageStep) },
            MixPart.Opacity => node with { Role = UiaRole.Slider, Pattern = UiaPattern.Range, Status = said, Range = new UiaRange(Math.Round(ColorMix.Opacity(new Argb(alpha, 0, 0, 0))), 0, ColorMix.MostOpacity, ColorMix.Step, ColorMix.PageStep) },
            _ => node with { Role = UiaRole.Slider, Pattern = UiaPattern.None, Status = said },
        };
    }

    /// <summary>Tests only: the middle of the picker's part on row <paramref name="id"/> (its box), in window px, and its size; null where there is none.</summary>
    internal (POINT Middle, float W, float H)? MixBoxOf(uint id)
    {
        if (!IsOpen || rows.Find(row => row.Item.Id == id) is not { } found || (found.Item.Control is not FlyoutMix && found.Mix is null))
        {
            return null;
        }
        var box = MixBox((found.Item.Control as FlyoutMix)?.Part, found.Bounds, scale);
        return (new POINT((int)MathF.Round(viewport.X + box.X + (box.W / 2)), (int)MathF.Round(viewport.Y + box.Y + (box.H / 2) - scroll)), box.W, box.H);
    }

    /// <summary>Tests and the stage: each part of the picker in sight and the hex box, on the screen: its part (the hex box's null) and its box.</summary>
    internal IReadOnlyList<(MixPart? Part, RECT Box)> MixOnScreen
    {
        get
        {
            List<(MixPart?, RECT)> parts = [];
            if (!IsOpen)
            {
                return parts;
            }
            var origin = host.Origin;
            foreach (var row in rows)
            {
                Area? box = row.Item.Control switch
                {
                    FlyoutMix mix => MixBox(mix.Part, row.Bounds, scale),
                    FlyoutField { Hex: true } when FieldBox(row) is { } hex => hex,
                    _ => null,
                };
                if (box is { } at)
                {
                    var left = origin.x + (int)MathF.Round(viewport.X + at.X);
                    var top = origin.y + (int)MathF.Round(viewport.Y + at.Y - scroll);
                    parts.Add(((row.Item.Control as FlyoutMix)?.Part, new RECT { left = left, top = top, right = left + (int)MathF.Round(at.W), bottom = top + (int)MathF.Round(at.H) }));
                }
            }
            return parts;
        }
    }

    /// <summary>
    /// Tests only: what the menu posted to its own window — a mix (<see cref="Post"/>), a field's keep — taken off its queue
    /// and heard, the first that says something; null where none does.
    /// </summary>
    internal FlyoutEvent? Posted()
    {
        MSG posted;
        while (PeekMessageW(&posted, Hwnd, WM.WM_USER, WM.WM_USER + 0xFF, PM.PM_REMOVE))
        {
            if (Message(posted.message, posted.wParam, posted.lParam) is { Kind: not FlyoutEventKind.Nothing } said)
            {
                return said;
            }
        }
        return null;
    }

    /// <summary>Tests only: the colour being mixed, null for none.</summary>
    internal Argb? MixingNow => mixing?.Color;
}
