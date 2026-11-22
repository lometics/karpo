// The displays' brightness as one slider: on the bar and as a line of a list folder — the volume
// slider's family (VolumeIsland), the sun where the speaker is, the same track and thumb
// (VolumePlan) on the bar and the same line in a list (SliderLine), dragged and turned alike.
//
// **One slider.** Never one per display, and no display's name anywhere (the person: "it should
// dimm the screens brigness together with one slider"). By default it sets every display with a
// brightness (Brightness) to the same level; where somebody chose "Only the screen it's on"
// (BrightnessOptions), the display the widget is on alone. The display it is on is its bar's, and
// in a folder the one the folder opened on, which is the folder's bar's (IslandStyle.Screen,
// TileStyle.Screen). It shows that display's level, or the first display's that has one where its
// own has none (Brightness.Shown). Its flyout, the same one slider again, went on 2026-10-11 (the
// person: "remove the flyout").
//
// **Light or dark on its side** (§605; the person, 2026-11-09: "the screen brigtness selector should have the toggle
// brigth and dark mode on the side instead of the brightness icon"). Where the sun stood is Windows' mode: a sun while
// Windows is light, a moon while it is dark, and a click switches it — the apps and Windows together, as the dark mode
// widget does, through the one switch every dark mode button reads (QuickSwitch.DarkMode, DarkMode.cs: SwitchData, its
// source, held as the widget's own; a test's made-up one through QuickSwitches.Source, so no test turns this PC dark).
// On the bar it is a button of its own beside the slider (Press, Drag, Release: a press on it and the release still on
// it); in a folder the line's button (ModeTarget), named and with its tooltip as a switch's ("Dark mode: on").
//
// **Slow displays.** A monitor's brightness goes over DDC/CI, tens to hundreds of ms a call, so the
// widget never waits for it: a drag asks for every level it passes on every display it sets, the
// service writes only each display's newest off the dock's thread, and the thumb follows the
// pointer (Brightness.Asked) until the display says what it took.
//
// **Where there is none.** A display no way reaches has no brightness to set, and is skipped. Where
// the slider sets none at all, the widget is still the slider, at half, greyed, taking no pointer,
// its tooltip saying why (CellIsland.UnavailableOn, docs\widgets.md §247; the person, 2026-10-19:
// "if there is no brightness to set it should still show the slider just greyed out").
//
// **Changed elsewhere** — the keyboard's brightness keys, Windows' own slider, a monitor's buttons
// (docs\widgets.md §439): the service hears the built-in screen's from Windows and says so (Changed),
// and the widget draws it; a monitor's is read again as the widget comes into view (Visible) and as
// the pointer comes onto it (PointerCame, CardHover). A drag is the pointer's: while one holds the thumb,
// the bar draws the level the drag asked, as the line's slider keeps its own (Slider.Dragging), and a
// level read meanwhile shows only once it lets go.
//
// **Nothing reaches a real screen from a test or a smoke pass** (Brightness.ForWidget).

using System.Numerics;
using Karpo.Core;
using Karpo.Dock.QuickSettings;
using Karpo.Dock.QuickSettings.Controls;
using Karpo.Dock.QuickSettings.Services;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock;

internal sealed unsafe class BrightnessIsland : CellIsland, ISliderIsland, IListContent
{
    /// <summary>list: its size's form, one height for both sliders, the volume's line beside it (§432, SliderLine).</summary>
    public float ListHeight(float width, in TileStyle style) => SliderLine.Height(Gfx, ContextFor(style), style, LineSizes.Of(Settings));

    /// <summary>The click a line asks for when a point on its slider was chosen.</summary>
    internal const int SetLevelTarget = 2;

    /// <summary>The click a line asks for on its mode's button (§605): Windows light or dark.</summary>
    internal const int ModeTarget = 3;

    /// <summary>Segoe Fluent Icons' Brightness, as Windows' own row has it: the mode's while Windows is light.</summary>
    internal static readonly string Sun = char.ConvertFromUtf32(0xE706);

    /// <summary>Segoe Fluent Icons' QuietHours, the moon: the mode's while Windows is dark.</summary>
    internal static readonly string Moon = char.ConvertFromUtf32(0xE708);

    private readonly Brightness brightness;
    private readonly bool owns;

    // §605: Windows' mode, the one switch every dark mode button reads (SwitchData), or a test's own, which it starts
    // the first time it is looked at (FollowMode).
    private readonly IQuickSwitch mode;
    private readonly SwitchData? modeData;
    private readonly DataSources sources;
    private bool following;

    // A press on the bar's mode button: down on it, and whether the pointer is still on it.
    private bool modeHeld;
    private bool modeInside;

    // A drag: the display the widget was on when it took hold, where it took hold of the thumb, and
    // the level it last asked, which the bar draws until it lets go.
    private nint? held;
    private float grab;
    private int? dragged;

    /// <summary>tests: whether the pointer is on the line's slider, whose thumb's core grows as the panel's slider's does.</summary>
    internal bool SliderHot => lineSlider.Hot;

    // A card's last click on the slider: the display it was on and the level it pointed at.
    private (nint Screen, int Level)? chosen;

    public BrightnessIsland(string key, WidgetItem island, Gfx gfx, Config config)
        : this(key, island, gfx, config, DataSources.Shared)
    {
    }

    internal BrightnessIsland(string key, WidgetItem island, Gfx gfx, Config config, DataSources sources)
        : base(key, island, gfx)
    {
        owns = Brightness.Stand is not null;
        brightness = Brightness.ForWidget();
        brightness.Changed += Post;
        this.sources = sources;
        var held = QuickSwitches.Source is null ? sources.Hold(island) : null;
        if (held is SwitchData shared)
        {
            modeData = shared;
            mode = shared.Switch;
            following = true;
            shared.Listen(Post);
        }
        else
        {
            sources.Release(held);
            mode = QuickSwitches.For(QuickSwitch.DarkMode);
        }
    }

    /// <summary>Starts following Windows' mode the first time a widget with a switch of its own (a test's) is looked at.</summary>
    private void FollowMode()
    {
        if (!following)
        {
            following = true;
            mode.Start(Post);
        }
    }

    /// <summary>§605: Windows is dark (true), light (false), or not known yet (null).</summary>
    internal bool? Dark
    {
        get
        {
            FollowMode();
            return mode.On;
        }
    }

    /// <summary>The mode's glyph: the moon while Windows is dark, else the sun.</summary>
    internal string ModeGlyph => Dark == true ? Moon : Sun;

    /// <summary>What the mode's button says, as a dark mode switch's tooltip does: "Dark mode: on" and what a click does.</summary>
    internal string ModeTip => SwitchIsland.Say(mode.Name, mode.Available, Dark, mode.Note);

    /// <summary>Windows switched light or dark (§605), the apps and Windows together; before its mode is read, its settings.</summary>
    internal void SwitchMode()
    {
        FollowMode();
        mode.Act();
        Post();
    }

    /// <summary>Nothing ticks: the service says when a level or the displays changed.</summary>
    public override int IntervalMs => 0;

    protected override int Count => 1;

    public override void Configure(WidgetItem island, Config config) => Settings = island;

    /// <summary>Which displays its slider sets: every display unless somebody chose its own (<see cref="BrightnessOptions"/>).</summary>
    internal BrightnessScreens Screens => Settings.Brightness?.Screens ?? BrightnessScreens.All;

    /// <summary>No hover tooltip: the level is on the slider, and the bar's widgets keep quiet (§367).</summary>
    public override string? Tooltip(int index) => null;

    /// <summary>Where the slider sets no display: the slider at half, greyed, and why.</summary>
    internal override string? UnavailableOn(nint screen) =>
        LevelOn(screen) is null ? NotOnThisPc(WidgetInfos.Brightness.Title, S.WidgetBrightnessNoneToSet) : null;

    /// <summary>The level a slider that sets no display is drawn at (<see cref="UnavailableOn"/>).</summary>
    internal const int UnavailableLevel = 50;

    /// <summary>
    /// A card's click on its slider (<see cref="SetLevelTarget"/>) sets the level it pointed at, on its mode's button
    /// (<see cref="ModeTarget"/>) switches Windows light or dark; any other — beside them in a folder — does nothing.
    /// On the bar the mode's button takes its press itself (<see cref="Press"/>), so no click reaches here from it.
    /// </summary>
    public override IslandAction Click(int index)
    {
        modeButton.Pressed = false;
        if (index == SetLevelTarget && chosen is { } click)
        {
            _ = brightness.Set(click.Screen, Screens, click.Level);
            chosen = null;
            Post();
        }
        else if (index == ModeTarget)
        {
            SwitchMode();
        }
        return default;
    }

    /// <summary>Its click on the bar, off the slider and the mode's button, does nothing (<see cref="Click"/>): no press shows there (§287).</summary>
    internal override bool ClickDoes(nint screen) => false;

    protected override bool Sample(bool everything) => false;

    /// <summary>Coming into view — its bar shown, its folder opened — it reads the displays again: a monitor's buttons tell nobody.</summary>
    public override void Visible(bool shown)
    {
        base.Visible(shown);
        if (shown)
        {
            brightness.Look();
        }
    }

    /// <summary>The pointer came onto it on a bar: the displays are read again, as they are when it comes into view.</summary>
    internal override void PointerCame() => brightness.Look();

    /// <summary>tests: the brightness it reads and sets, which a test tells of a change made elsewhere.</summary>
    internal Brightness Service => brightness;

    public override void Dispose()
    {
        brightness.Changed -= Post;
        if (owns)
        {
            brightness.Dispose();
        }
        if (modeData is not null)
        {
            modeData.Unlisten(Post);
            sources.Release(modeData);
        }
        else
        {
            mode.Dispose();
        }
        base.Dispose();
    }

    /// <summary>The level the slider shows on <paramref name="screen"/>; null where it sets no display.</summary>
    private int? LevelOn(nint screen) => brightness.LevelOf(screen, Screens);

    /// <summary>
    /// The level the bar's thumb stands at on <paramref name="screen"/>: the one a drag asked while it
    /// holds the thumb — a level read meanwhile, from the keys or a monitor, must not move it under
    /// the pointer — else the displays'; the stand-in where it sets none.
    /// </summary>
    internal int BarLevel(nint screen) => (held is not null ? dragged : null) ?? LevelOn(screen) ?? UnavailableLevel;

    /// <summary>
    /// The display a press took hold of, until the pointer lets go: the one whose bar or folder the
    /// drag was on. For the tests, which find what the pointer reaches without waiting on the worker.
    /// </summary>
    internal nint? Holding => held;

    // --- On the bar -----------------------------------------------------------------------------

    /// <summary>
    /// Where the sun and the track go: the volume slider's plan (VolumeIsland.Plan) at its own
    /// settings' defaults, since the two are one family and share their size (WidgetShapes).
    /// </summary>
    internal static VolumePlan Plan(in IslandStyle style, float across, float along = 0) =>
        VolumeIsland.Plan(new VolumeOptions(), style, across, along);

    internal override void PaintCell(ID2D1DeviceContext* dc, int index, in IslandStyle style, float width, float height)
    {
        var plan = Plan(style, style.Vertical ? width : height, style.Vertical ? height : width);
        var box = new D2D_RECT_F(0, 0, width, height);
        var ink = new IslandInk(style.Look);
        // No display to set: the slider as it would be, which the bar greys (UnavailableOn).
        var shown = BarLevel(style.Screen);
        if (plan.Glyph > 0)
        {
            dc->SetTextAntialiasMode(D2D1_TEXT_ANTIALIAS_MODE.D2D1_TEXT_ANTIALIAS_MODE_GRAYSCALE);
            PopupPaint.Glyph(dc, Gfx, ModeGlyph, plan.Glyph, ink.Text, plan.GlyphBox(box));
        }
        if (!plan.Slider)
        {
            // The short form: the mode's button alone.
            return;
        }
        var radius = plan.Track / 2;
        PopupPaint.Fill(dc, plan.TrackBox(box), radius, ink.Track);
        var filled = plan.Filled(box, shown);
        if (filled.right > filled.left && filled.bottom > filled.top)
        {
            PopupPaint.Fill(dc, filled, radius, ink.Accent);
        }
        var centre = plan.ThumbCentre(box, shown);
        PopupPaint.Circle(dc, centre, plan.Thumb / 2, ink.Text);
        PopupPaint.Circle(dc, centre, MathF.Max(1, (plan.Thumb / 2) - MathF.Max(1, radius)), ink.Accent);
    }

    /// <summary>
    /// A press on the bar's slider takes the pointer, for the display the bar is on; one on its mode's button takes it too,
    /// and switches the mode where the release comes still on it (§605), as a button does.
    /// </summary>
    public bool Press(int index, Vector2 point, Vector2 size, in IslandStyle style)
    {
        var plan = Plan(style, style.Vertical ? size.X : size.Y, style.Vertical ? size.Y : size.X);
        var box = new D2D_RECT_F(0, 0, size.X, size.Y);
        // With no display to set the widget is greyed whole, its mode's button with it (§247).
        if (LevelOn(style.Screen) is not null && OnMode(plan, box, point))
        {
            modeHeld = modeInside = true;
            return true;
        }
        if (LevelOn(style.Screen) is not { } level || !plan.OnSlider(box, point))
        {
            return false;
        }
        Take(style.Screen, level, plan, box, point);
        return true;
    }

    /// <summary>
    /// §605: whether <paramref name="point"/> is on the bar's mode button: its glyph and the air round it short of the
    /// slider, or anywhere off the slider in the short form, where the button is the widget.
    /// </summary>
    internal static bool OnMode(VolumePlan plan, D2D_RECT_F box, Vector2 point) =>
        plan.Glyph > 0 && !plan.OnSlider(box, point) && (!plan.Slider || plan.OnGlyph(box, point));

    public void Drag(int index, Vector2 point, Vector2 size, in IslandStyle style)
    {
        var plan = Plan(style, style.Vertical ? size.X : size.Y, style.Vertical ? size.Y : size.X);
        var box = new D2D_RECT_F(0, 0, size.X, size.Y);
        if (modeHeld)
        {
            modeInside = OnMode(plan, box, point);
            return;
        }
        if (held is not { } screen)
        {
            return;
        }
        Ask(screen, plan.LevelAt(box, point, grab));
    }

    public void Release()
    {
        // A press on the mode's button released on it: Windows light or dark.
        if (modeHeld && modeInside)
        {
            SwitchMode();
        }
        modeHeld = modeInside = false;
        (held, dragged) = (null, null);
        // What the displays say now, which a level read during the drag may have moved.
        Post();
    }

    /// <summary>A wheel turn over the bar's widget: two per notch, as the volume slider beside it steps.</summary>
    public bool Wheel(int delta, in IslandStyle style) => Step(style.Screen, delta);

    private bool Step(nint screen, int delta)
    {
        if (LevelOn(screen) is not { } level)
        {
            return false;
        }
        var notches = delta / 120;
        if (notches == 0)
        {
            notches = delta > 0 ? 1 : -1;
        }
        Ask(screen, level + (notches * 2));
        return true;
    }

    /// <summary>keys: the slider stepped by the folder's keys, a step a level of one, as Windows' own slider steps.</summary>
    protected internal override bool CardStep(int target, int steps, nint screen)
    {
        if (target != SetLevelTarget || LevelOn(screen) is not { } level)
        {
            return false;
        }
        Ask(screen, level + steps);
        return true;
    }

    /// <summary>The pointer took the thumb, on <paramref name="screen"/>'s bar or folder: where it held, and the level it points at.</summary>
    private void Take(nint screen, int level, VolumePlan plan, D2D_RECT_F box, Vector2 point)
    {
        var at = plan.ThumbCentre(box, level);
        var along = plan.Upright ? point.Y - at.Y : point.X - at.X;
        // On the thumb the drag keeps where it took hold; elsewhere the thumb jumps there.
        grab = MathF.Abs(along) <= plan.Thumb / 2 ? along : 0;
        held = screen;
        Ask(screen, plan.LevelAt(box, point, grab));
    }

    /// <summary>Asks for a level on every display the slider sets; the widget shows it at once, and the display's own answer when it comes.</summary>
    private void Ask(nint screen, int level)
    {
        level = Math.Clamp(level, 0, 100);
        if (held is not null)
        {
            dragged = level;
        }
        _ = brightness.Set(screen, Screens, level);
        Post();
    }

    // --- As a line of a list ---------------------------------------------------------------------

    // The volume's line and this one are one design (SliderLine, 2026-10-11, the person: "the volume
    // and brigness slider look different in terms of layout, this should not be"): the same controls
    // — the mode on the volume's button (§605), the panel's slider, the level in the volume's field —
    // where the same layout puts them at every height.

    private readonly Button modeButton = new("", ButtonStyle.Subtle, Sun) { Name = WidgetInfos.DarkMode.Title };
    private readonly Slider lineSlider = new() { Name = WidgetInfos.Brightness.Title };
    // The level against the trailing gutter, as every row's reading ends (§401).
    private readonly Label lineLevel = new("", LabelStyle.Body) { Trailing = true };

    // A drag on a line: the box it was pressed in, and where the pointer was last, in the line's px.
    private D2D_RECT_F lineBox;
    private Vector2 lineAt;

    // Whether the pointer is on the line, so that coming onto it is told from moving over it.
    private bool lineIn;

    /// <summary>What a line's last layout placed, in its own px: the mode's button, the slider and the level.</summary>
    internal IReadOnlyList<Widget> LinePlaced => linePlaced;

    /// <summary>tests: lays a line out in <paramref name="box"/> as a paint would, without painting (<see cref="LinePlaced"/>).</summary>
    internal void LayOut(D2D_RECT_F box, in TileStyle style) => _ = LayLine(box, style, Shown(style.Screen), out _);

    /// <summary>The level the slider shows on <paramref name="screen"/>: its own, or the stand-in where it sets no display.</summary>
    private int Shown(nint screen) => LevelOn(screen) ?? UnavailableLevel;

    private readonly List<Widget> linePlaced = [];

    /// <summary>The context a line's controls paint with: the card's own scales, and the folder's theme.</summary>
    private QuickContext ContextFor(in TileStyle style) =>
        new() { Gfx = Gfx, Theme = QuickTheme.For(style.Theme), Scale = style.Scale, TextScale = style.TextScale };

    /// <summary>
    /// Lays a line out in <paramref name="box"/> (its own px from its top-left), placing its controls
    /// for <paramref name="level"/>, as the volume's line places its own (<see cref="SliderLine"/>).
    /// </summary>
    private SliderLine.Boxes LayLine(D2D_RECT_F box, in TileStyle style, int level, out QuickContext context)
    {
        context = ContextFor(style);
        linePlaced.Clear();
        var width = TilePaint.Width(box);
        var height = TilePaint.Height(box);
        lineSlider.Host = context;
        if (!lineSlider.Dragging)
        {
            lineSlider.Value = level;
        }
        lineLevel.Text = SoundRows.Number(lineSlider.Value);
        var line = SliderLine.Lay(Gfx, context, style, width, height);
        modeButton.Glyph = ModeGlyph;
        modeButton.Clicked ??= SwitchMode;
        modeButton.Bounds = line.Button;
        modeButton.GlyphSize = line.GlyphPx;
        lineSlider.Bounds = line.Slider;
        linePlaced.Add(modeButton);
        linePlaced.Add(lineSlider);
        if (line.Form != SliderLineForm.Tall)
        {
            lineLevel.Bounds = line.Number;
            linePlaced.Add(lineLevel);
        }
        return line;
    }

    protected internal override void PaintCard(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var shown = LayLine(box, style, Shown(style.Screen), out var context);
        foreach (var control in linePlaced)
        {
            var at = control.Bounds;
            TilePaint.Placed(dc, box.left + at.left, box.top + at.top, 1, () => control.Paint(dc, context));
        }
        // What the slider sets, quiet beside the sun, and the level large in the tall form: as the
        // volume's line says its output and its level.
        if (TilePaint.Width(shown.Name) > 1)
        {
            PopupPaint.Text(dc, Gfx, WidgetInfos.Brightness.Title, context.TextPx, Weight.Regular, context.Theme.TextSecondary,
                box.left + shown.Name.left, box.top + shown.Name.top, TilePaint.Width(shown.Name));
        }
        if (shown.Form == SliderLineForm.Tall)
        {
            PopupPaint.Text(dc, Gfx, lineLevel.Text, shown.BigPx, Weight.Semibold, context.Theme.Text,
                box.left + shown.Big.left, box.top + shown.Big.top, TilePaint.Width(shown.Big));
        }
    }

    /// <summary><paramref name="point"/> in the line's own px.</summary>
    private static Vector2 Own(Vector2 point, D2D_RECT_F box) => new(point.X - box.left, point.Y - box.top);

    /// <summary>Whether <paramref name="point"/> is on a line's slider, laid out for the level it shows; false where it sets no display.</summary>
    private bool OnLineSlider(Vector2 point, D2D_RECT_F box, in TileStyle style, out QuickContext context)
    {
        _ = LayLine(box, style, Shown(style.Screen), out context);
        if (LevelOn(style.Screen) is null)
        {
            return false;
        }
        var own = Own(point, box);
        var at = lineSlider.Bounds;
        return own.X >= at.left && own.X < at.right && own.Y >= at.top && own.Y < at.bottom;
    }

    /// <summary>Whether <paramref name="point"/> is on a line's mode button (§605), laid out as it shows.</summary>
    private bool OnLineMode(Vector2 point, D2D_RECT_F box, in TileStyle style)
    {
        _ = LayLine(box, style, Shown(style.Screen), out _);
        var own = Own(point, box);
        var at = modeButton.Bounds;
        return own.X >= at.left && own.X < at.right && own.Y >= at.top && own.Y < at.bottom;
    }

    /// <summary>
    /// The pointer on a line's slider lights its thumb, as the volume's does, and on its mode's button the button; the line
    /// around them does not light (FolderFlyout.LightsWhole).
    /// </summary>
    protected internal override void CardHover(Vector2? point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        // The pointer coming onto the line reads the displays again, as it does on the bar (PointerCame).
        if (point is not null && !lineIn)
        {
            brightness.Look();
        }
        lineIn = point is not null;
        var on = point is { } at && OnLineSlider(at, box, style, out _);
        var onMode = point is { } over && OnLineMode(over, box, style);
        if (on != lineSlider.Hot || onMode != modeButton.Hot)
        {
            lineSlider.Hot = on;
            modeButton.Hot = onMode;
            if (!onMode)
            {
                modeButton.Pressed = false;
            }
            _ = Wake();
        }
    }

    /// <summary>The mode's button names itself and says where Windows stands, as a dark mode switch's tooltip does (§605).</summary>
    protected internal override (string Text, D2D_RECT_F Part)? CardTip(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        if (!OnLineMode(point, box, style))
        {
            return null;
        }
        var at = modeButton.Bounds;
        return (ModeTip, PopupPaint.Rect(box.left + at.left, box.top + at.top, TilePaint.Width(at), TilePaint.Height(at)));
    }

    protected internal override int CardTarget(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        if (OnLineMode(point, box, style))
        {
            return ModeTarget;
        }
        if (!OnLineSlider(point, box, style, out var context))
        {
            return 0;
        }
        var half = context.D(20) / 2;
        var x = Own(point, box).X - lineSlider.Bounds.left;
        chosen = (style.Screen, SliderMath.ValueAt(x, half, MathF.Max(half, lineSlider.Width - half), 0, 100));
        return SetLevelTarget;
    }

    /// <summary>A press on a line's slider takes the pointer, for the display the folder opened on, as one on the bar's does.</summary>
    protected internal override bool CardPress(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        if (OnLineMode(point, box, style))
        {
            // The folder's click (ModeTarget): the button pressed until it comes.
            modeButton.Pressed = true;
            _ = Wake();
            return false;
        }
        if (!OnLineSlider(point, box, style, out _))
        {
            return false;
        }
        // The click path's level is the press's now: it must not be set again on the release.
        chosen = null;
        held = style.Screen;
        lineBox = box;
        lineAt = Own(point, box);
        // Pressed first, as the volume's line does: it keeps the level the displays read back from
        // moving the thumb under the pointer.
        var screen = style.Screen;
        lineSlider.Pressed = true;
        lineSlider.Changed = level => Ask(screen, level);
        if (!lineSlider.PointerPressed(Local(lineAt), Mods.None, 1))
        {
            lineSlider.Pressed = false;
            held = null;
            return false;
        }
        return true;
    }

    protected internal override void CardDrag(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        if (held is null)
        {
            return;
        }
        lineAt = Own(point, lineBox);
        lineSlider.PointerMoved(Local(lineAt));
    }

    protected internal override void CardRelease()
    {
        if (lineSlider.Pressed)
        {
            lineSlider.Pressed = false;
            lineSlider.PointerReleased(Local(lineAt), inside: true);
            lineSlider.Changed = null;
        }
        Release();
        _ = Wake();
    }

    /// <summary><paramref name="own"/> on the line's slider, from its top-left.</summary>
    private Vector2 Local(Vector2 own) => new(own.X - lineSlider.Bounds.left, own.Y - lineSlider.Bounds.top);

    /// <summary>The wheel over a line: the slider, two a notch.</summary>
    protected internal override bool CardWheel(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface, int notches) =>
        Step(style.Screen, notches * 120);
}
