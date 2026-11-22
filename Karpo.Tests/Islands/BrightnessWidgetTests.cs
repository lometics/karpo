// quick: the brightness widget on each surface it is drawn on — a taskbar entry and a list line —
// as one slider (the person: "it should dimm the screens brigness together with one
// slider"): never one per display, setting every display with a brightness together, or the display
// it is on alone where that is chosen (BrightnessOptions). The display it is on is its bar's, and in
// a folder the one the folder opened on (IslandStyle.Screen, TileStyle.Screen). A display no way
// reaches is skipped; where the slider sets none, the widget keeps its place with nothing to drag.
//
// Every display here is a stand-in (Brightness.Stand, SampleScreens) with made-up handles: nothing
// in these tests reads or sets a real screen's brightness.
//
// Needs Direct2D, so it runs only with KARPO_UI_TESTS=1.

using System.Numerics;
using Karpo.Dock;
using Karpo.Dock.QuickSettings;
using Karpo.Dock.QuickSettings.Controls;
using Karpo.Dock.QuickSettings.Services;
using Karpo.Tests.Folders;
using TerraFX.Interop.DirectX;
using Color = Windows.UI.Color;

namespace Karpo.Tests.Islands;

public sealed unsafe class BrightnessWidgetTests
{
    private const string Samsung = @"\\.\DISPLAY1";
    private const string Dell = @"\\.\DISPLAY2";
    private const string Television = @"\\.\DISPLAY3";

    // The displays' HMONITORs, made up: nothing here asks Windows about them.
    private const nint SamsungScreen = 0x10;
    private const nint DellScreen = 0x20;
    private const nint TelevisionScreen = 0x30;

    private const float Scale = 1.5f;

    private static readonly Look Dark = TestLooks.LookFor(dark: true);

    /// <summary>A desk, made up: two monitors that answer DDC/CI and a television that does not.</summary>
    private static SampleScreens Desk() => new SampleScreens()
        .Add(SamsungScreen, Samsung, "SAMSUNG", 60)
        .Add(DellScreen, Dell, "DELL U2720Q", 35)
        .Add(TelevisionScreen, Television, "TV", null);

    /// <summary>Desks of one, two and three displays with a brightness, and the television beside them.</summary>
    private static IEnumerable<SampleScreens> Desks()
    {
        yield return new SampleScreens().Add(SamsungScreen, Samsung, "SAMSUNG", 60);
        yield return Desk();
        yield return Desk().Add(0x40, @"\\.\DISPLAY4", "Built-in display", 80, builtIn: true);
    }

    // --- On the bar ------------------------------------------------------------------------------

    /// <summary>A bottom bar at <see cref="Scale"/>, standing on <paramref name="screen"/>.</summary>
    private static IslandStyle Bar(nint screen) => new(Scale, false, 48 * Scale, 24 * Scale, Dark) { Screen = screen };

    /// <summary>The widget's cell on <paramref name="style"/>'s bar, at the length its shape asks, and the plan it is drawn and hit by.</summary>
    private static (Vector2 Size, D2D_RECT_F Box, VolumePlan Plan) Cell(in IslandStyle style)
    {
        var along = BrightnessIsland.Plan(style, style.Thickness).Along;
        return (new Vector2(along, style.Thickness), new D2D_RECT_F(0, 0, along, style.Thickness), BrightnessIsland.Plan(style, style.Thickness, along));
    }

    /// <summary>
    /// On the bar, by default, the one slider sets every display with a brightness to the same
    /// level, whichever bar it is on, and skips the television: a press near the end of the track,
    /// then the wheel, two a notch from the level of the display the bar is on, held to 0 and 100.
    /// </summary>
    [UiFact]
    public void TheBarsSliderSetsEveryDisplayTogether()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var desk = Desk();
            using var island = Made(gfx, desk);
            ISliderIsland slider = island;
            Assert.Equal(BrightnessScreens.All, island.Screens);

            // The bar on the Dell: a press near the end of its track sets both monitors.
            var dell = Bar(DellScreen);
            var (size, box, plan) = Cell(dell);
            Assert.True(plan.Slider, "a bar at its defaults carries the slider");
            Assert.True(slider.Press(0, plan.ThumbCentre(box, 90), size, dell));
            Assert.Equal(DellScreen, island.Holding);
            slider.Release();
            Assert.Null(island.Holding);
            Wait(() => desk.LevelOf(Dell) is >= 88 and <= 92 && desk.LevelOf(Samsung) == desk.LevelOf(Dell));

            // The wheel on the Samsung's bar: both step together, two a notch.
            var at = desk.LevelOf(Samsung)!.Value;
            Assert.True(slider.Wheel(-3 * 120, Bar(SamsungScreen)));
            Wait(() => desk.LevelOf(Samsung) == at - 6 && desk.LevelOf(Dell) == at - 6);
            Assert.True(slider.Wheel(40 * 120, Bar(SamsungScreen)));
            Wait(() => desk.LevelOf(Samsung) == 100 && desk.LevelOf(Dell) == 100);
            Assert.True(slider.Wheel(-60 * 120, dell));
            Wait(() => desk.LevelOf(Samsung) == 0 && desk.LevelOf(Dell) == 0);
            Assert.All(desk.Writes, write => Assert.InRange(write.Level, 0, 100));
            Assert.DoesNotContain(desk.Writes, write => write.Device == Television);

            // Off the track the widget's click does nothing, its flyout gone (2026-10-11); its leading button is Windows'
            // light or dark mode (§605), which takes its own press (QuickSliderLineTests).
            Assert.True(slider.Press(0, Centre(plan.GlyphBox(box)), size, dell));
            slider.Release();
            Assert.Equal(default, island.Click(0));
            Assert.Equal(BarClick.Nothing, BarEntries.Find(BarKind.Brightness)!.Click);
            Assert.False(global::Karpo.Dock.Dock.HasFlyout(BarKind.Brightness));
        });
    }

    /// <summary>Set to "Only the screen it's on", the bar's slider sets the display its bar is on and no other.</summary>
    [UiFact]
    public void OnlyItsOwnScreenSetsTheBarsDisplayAlone()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var desk = Desk();
            using var island = Made(gfx, desk, BrightnessScreens.Own);
            ISliderIsland slider = island;
            Assert.Equal(BrightnessScreens.Own, island.Screens);

            var dell = Bar(DellScreen);
            var (size, box, plan) = Cell(dell);
            Assert.True(slider.Press(0, plan.ThumbCentre(box, 90), size, dell));
            slider.Release();
            Wait(() => desk.LevelOf(Dell) is >= 88 and <= 92);
            Assert.True(slider.Wheel(120, Bar(SamsungScreen)));
            Wait(() => desk.LevelOf(Samsung) == 62);
            Thread.Sleep(50);
            Assert.All(desk.Writes.Where(write => write.Level is >= 88 and <= 92), write => Assert.Equal(Dell, write.Device));
            Assert.All(desk.Writes.Where(write => write.Level == 62), write => Assert.Equal(Samsung, write.Device));
            Assert.DoesNotContain(desk.Writes, write => write.Device == Television);
        });
    }

    /// <summary>
    /// A bar on a display no way reaches. Setting every display, the slider shows the first display
    /// that has a brightness, and a press there sets them; setting its own alone, it keeps the size
    /// it has everywhere — a widget's size follows its settings, never its state — with its slider
    /// greyed, and nothing takes the pointer or the wheel.
    /// </summary>
    [UiFact]
    public void OnADisplayWithNoBrightnessTheSliderShowsTheFirstOrNothing()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var television = Bar(TelevisionScreen);
            var (size, box, plan) = Cell(television);
            Assert.Equal(Cell(Bar(DellScreen)).Size, size);
            var sun = plan.GlyphBox(box);

            var desk = Desk();
            using (var every = Made(gfx, desk))
            {
                Assert.True(InkRight(gfx, every, television, size) > sun.right + Scale * 8, "setting every display, the television's bar shows the first display's track");
                Assert.True(((ISliderIsland)every).Press(0, plan.ThumbCentre(box, 20), size, television));
                ((ISliderIsland)every).Release();
                Wait(() => desk.LevelOf(Samsung) is >= 18 and <= 22 && desk.LevelOf(Dell) == desk.LevelOf(Samsung));
            }

            var other = Desk();
            using var own = Made(gfx, other, BrightnessScreens.Own);
            ISliderIsland slider = own;
            Assert.True(InkRight(gfx, own, Bar(DellScreen), size) > sun.right + Scale * 8, "the Dell's bar shows its track");
            // The television's bar shows the slider all the same, at half and greyed, which nothing moves (§247).
            Assert.NotNull(own.UnavailableOn(TelevisionScreen));
            Assert.True(InkRight(gfx, own, television, size) > sun.right + Scale * 8, "the television's bar shows its slider greyed");
            for (var x = 0f; x < size.X; x += 2)
            {
                Assert.False(slider.Press(0, new Vector2(x, size.Y / 2), size, television), $"a press at {x:0} took the pointer");
            }
            Assert.False(slider.Wheel(120, television));
            Thread.Sleep(50);
            Assert.Empty(other.Writes);
        });
    }

    // --- In a folder -----------------------------------------------------------------------------

    /// <summary>
    /// Set to "Only the screen it's on", a line sets the display the folder opened on — the folder's
    /// bar's — alone; on a display with none it has nothing to set, and setting every display it
    /// still offers the first that has one.
    /// </summary>
    [UiFact]
    public void ALineSetsItsFoldersDisplayAloneWhereThatIsChosen()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var box = PopupPaint.Rect(0, 0, FolderFlyout.ListWidth(Scale), PopupPaint.Round(40 * Scale));
            var desk = Desk();
            using (var line = MadeLine(gfx, desk, BrightnessScreens.Own))
            {
                Assert.Equal([DellScreen], Sliders(line, box, Line(DellScreen), WidgetSurface.List));
                Wait(() => desk.Writes.Count > 0);
                Thread.Sleep(50);
                Assert.All(desk.Writes, write => Assert.Equal(Dell, write.Device));
                Assert.Equal(60, desk.LevelOf(Samsung));
                Assert.Empty(Sliders(line, box, Line(TelevisionScreen), WidgetSurface.List));
            }

            using var every = MadeLine(gfx, Desk());
            Assert.Equal([TelevisionScreen], Sliders(every, box, Line(TelevisionScreen), WidgetSurface.List));
        });
    }

    /// <summary>
    /// In a folder the line does not light as a whole (FolderFlyout.LightsWhole): its slider is what
    /// lights, the thumb under the pointer anywhere on the slider, and nothing beside it.
    /// </summary>
    [UiFact]
    public void ALinesSliderLightsUnderThePointer()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var box = PopupPaint.Rect(0, 0, FolderFlyout.ListWidth(Scale), PopupPaint.Round(40 * Scale));
            using var line = MadeLine(gfx, Desk());
            var style = Line(SamsungScreen);
            var middle = (box.top + box.bottom) / 2;
            line.HoverOn(new Vector2((box.left + box.right) / 2, middle), box, style, WidgetSurface.List);
            Assert.True(line.SliderHot);
            // The sun at the leading end and the level at the trailing one are not the slider.
            line.HoverOn(new Vector2(box.left + 2, middle), box, style, WidgetSurface.List);
            Assert.False(line.SliderHot);
            line.HoverOn(new Vector2(box.right - 2, middle), box, style, WidgetSurface.List);
            Assert.False(line.SliderHot);
            line.HoverOn(new Vector2((box.left + box.right) / 2, middle), box, style, WidgetSurface.List);
            line.HoverOn(null, box, style, WidgetSurface.List);
            Assert.False(line.SliderHot);
            Assert.False(FolderFlyout.LightsWhole(new FolderCell { Island = ListKind.Brightness.New() }, line));
        });
    }

    /// <summary>
    /// A list line is one row of the list whatever the displays — it no longer grows a row for
    /// each — with its one slider in it, its Compact form one row (§432); with no display to set, it says
    /// so and takes no pointer.
    /// </summary>
    [UiFact]
    public void ALineHasOneSliderWhateverTheDisplays()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var width = FolderFlyout.ListWidth(Scale);
            var row = PopupPaint.Round(40 * Scale);
            foreach (var desk in Desks())
            {
                using var line = MadeLine(gfx, desk);
                Assert.Equal(row, ((IListContent)line).ListHeight(width, FolderFlyout.StyleFor(Scale, ListFormPictures.LookFor(dark: true), FlyoutTheme.For(ListFormPictures.LookFor(dark: true)))), 1);
                Assert.Equal([SamsungScreen], Sliders(line, PopupPaint.Rect(0, 0, width, row), Line(SamsungScreen), WidgetSurface.List));
                // A line set taller keeps the one slider, in its middle.
                Assert.Equal([SamsungScreen], Sliders(line, PopupPaint.Rect(0, 0, width, row * 3), Line(SamsungScreen), WidgetSurface.List));
            }

            var none = new SampleScreens().Add(TelevisionScreen, Television, "TV", null);
            using (var line = MadeLine(gfx, none))
            {
                Assert.Empty(Sliders(line, PopupPaint.Rect(0, 0, width, row), Line(TelevisionScreen), WidgetSurface.List));
            }
            Assert.Empty(none.Writes);
        });
    }

    // --- Changed elsewhere (docs\widgets.md §439) -------------------------------------------------

    private const string Panel = @"\\.\DISPLAY4";
    private const nint PanelScreen = 0x40;

    /// <summary>A laptop with a monitor: its own screen at 30, which Windows tells of, and the Samsung at 60, which tells nobody.</summary>
    private static SampleScreens Laptop() => new SampleScreens()
        .Add(PanelScreen, Panel, "Built-in display", 30, builtIn: true)
        .Add(SamsungScreen, Samsung, "SAMSUNG", 60);

    /// <summary>
    /// The person's case: the keyboard's brightness keys move the laptop's screen, Windows says so,
    /// and the widget — the bar's slider and a folder's line — asks to be drawn again and draws the
    /// new level: the thumb where the keys left it, not where it was.
    /// </summary>
    [UiFact]
    public void ALevelChangedElsewhereIsDrawn()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var laptop = Laptop();
            using var island = Made(gfx, laptop);
            using var line = MadeLine(gfx, laptop);
            var window = MessageWindow();
            try
            {
                island.Attach((nint)window.Value, 7);
                line.Attach((nint)window.Value, 8);
                var bar = Bar(PanelScreen);
                var (size, box, plan) = Cell(bar);
                Assert.Equal(30, island.BarLevel(PanelScreen));
                Assert.True(ThumbAt(island, bar, size, plan.ThumbCentre(box, 30), plan.Thumb), "the thumb is not at 30");
                Assert.False(ThumbAt(island, bar, size, plan.ThumbCentre(box, 80), plan.Thumb), "the thumb is at 80 already");
                SavePair(island, bar, size, "before");
                Drain(window);

                // The keys, on the laptop's own screen: the line has its own brightness, and hears it too.
                laptop.Turn(Panel, 80);
                Assert.True(Notice(island.Service, 80));
                Assert.True(Notice(line.Service, 80));
                Wait(() => island.BarLevel(PanelScreen) == 80);
                Assert.Equal(new uint[] { 7, 8 }, Posted(window, 2).Order().ToArray());
                Assert.True(ThumbAt(island, bar, size, plan.ThumbCentre(box, 80), plan.Thumb), "the thumb did not move to 80");
                Assert.False(ThumbAt(island, bar, size, plan.ThumbCentre(box, 30), plan.Thumb), "the thumb stayed at 30");
                SavePair(island, bar, size, "after");
                var lineBox = PopupPaint.Rect(0, 0, FolderFlyout.ListWidth(Scale), PopupPaint.Round(40 * Scale));
                Wait(() => LineValue(line, lineBox, Line(PanelScreen)) == 80);
                Assert.Empty(laptop.Writes);
            }
            finally
            {
                DestroyWindow(window);
            }
        });
    }

    /// <summary>
    /// A level read during a drag — the keys pressed while the slider is held, a monitor's own buttons —
    /// does not move the thumb under the pointer: the bar draws the drag's level until it lets go, then
    /// the displays'. The line's slider keeps its own the same way.
    /// </summary>
    [UiFact]
    public void AChangeDuringADragDoesNotMoveTheThumb()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var laptop = Laptop();
            using var island = Made(gfx, laptop);
            ISliderIsland slider = island;
            var bar = Bar(PanelScreen);
            var (size, box, plan) = Cell(bar);
            Assert.True(slider.Press(0, plan.ThumbCentre(box, 80), size, bar));
            Wait(() => laptop.LevelOf(Panel) is >= 78 and <= 82 && island.Service.Asked(Panel) is null);
            var held = island.BarLevel(PanelScreen);
            Assert.InRange(held, 78, 82);

            // The keys, while the thumb is held: the displays say 20, the thumb stays under the pointer.
            laptop.Turn(Panel, 20);
            Assert.True(Notice(island.Service, 20));
            Wait(() => island.Service.On(PanelScreen)?.Level == 20);
            Assert.Equal(held, island.BarLevel(PanelScreen));
            Assert.True(ThumbAt(island, bar, size, plan.ThumbCentre(box, held), plan.Thumb), "the thumb left the pointer");

            // The drag goes on from where the pointer is, and the display follows it, not the keys.
            slider.Drag(0, plan.ThumbCentre(box, 60), size, bar);
            var dragged = island.BarLevel(PanelScreen);
            Assert.InRange(dragged, 58, 62);
            Wait(() => laptop.LevelOf(Panel) == dragged && island.Service.Asked(Panel) is null);

            // The keys once more, still held; let go, the thumb shows what the displays say.
            laptop.Turn(Panel, 15);
            Assert.True(Notice(island.Service, 15));
            Wait(() => island.Service.On(PanelScreen)?.Level == 15);
            Assert.Equal(dragged, island.BarLevel(PanelScreen));
            slider.Release();
            Assert.Equal(15, island.BarLevel(PanelScreen));
            Assert.Equal(dragged, laptop.Writes.Last().Level);

            // A line's slider, pressed: a level read meanwhile does not move it either.
            using var line = MadeLine(gfx, laptop);
            var lineBox = PopupPaint.Rect(0, 0, FolderFlyout.ListWidth(Scale), PopupPaint.Round(40 * Scale));
            var style = Line(PanelScreen);
            line.LayOut(lineBox, style);
            var at = line.LinePlaced.OfType<Slider>().Single().Bounds;
            Assert.True(line.PressOn(new Vector2(at.right - ((at.right - at.left) / 4), (at.top + at.bottom) / 2), lineBox, style, WidgetSurface.List));
            Wait(() => line.Service.Asked(Panel) is null && line.Service.On(PanelScreen)?.Level is > 50);
            var pressed = LineValue(line, lineBox, style);
            laptop.Turn(Panel, 5);
            Assert.True(Notice(line.Service, 5));
            Wait(() => line.Service.On(PanelScreen)?.Level == 5);
            Assert.Equal(pressed, LineValue(line, lineBox, style));
            line.ReleaseOn(WidgetSurface.List);
            Assert.Equal(5, LineValue(line, lineBox, style));
        });
    }

    /// <summary>
    /// A monitor's own buttons tell nobody: the widget reads every display again as it comes into
    /// view (its bar shown, its folder opened) and as the pointer comes onto it — the bar's cell, a
    /// line — at most every few seconds, so a pointer passing to and fro is not a stream of DDC/CI reads.
    /// </summary>
    [UiFact]
    public void AMonitorsButtonsAreReadAsTheWidgetComesIntoViewOrUnderThePointer()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var laptop = Laptop();
            var clock = new Karpo.Tests.QuickSettings.TestClock();
            Brightness.StandClock = clock;
            BrightnessIsland island;
            BrightnessIsland line;
            try
            {
                island = Made(gfx, laptop);
                line = MadeLine(gfx, laptop);
            }
            finally
            {
                Brightness.StandClock = null;
            }
            using (island)
            using (line)
            {
                // Just made, which read them all: nothing more yet.
                var reads = laptop.Reads;
                laptop.Turn(Samsung, 25);
                island.Visible(true);
                island.PointerCame();
                Thread.Sleep(50);
                Assert.Equal(reads, laptop.Reads);

                clock.Advance(TimeSpan.FromMilliseconds(Brightness.LookEveryMs));
                island.Visible(true);
                Wait(() => island.Service.On(SamsungScreen)?.Level == 25);
                // Moments later the pointer comes onto it: read already.
                Thread.Sleep(50);
                reads = laptop.Reads;
                island.PointerCame();
                Thread.Sleep(50);
                Assert.Equal(reads, laptop.Reads);

                laptop.Turn(Samsung, 45);
                clock.Advance(TimeSpan.FromMilliseconds(Brightness.LookEveryMs));
                island.PointerCame();
                Wait(() => island.Service.On(SamsungScreen)?.Level == 45);

                // A line: the pointer coming onto it reads; moving over it does not.
                var lineBox = PopupPaint.Rect(0, 0, FolderFlyout.ListWidth(Scale), PopupPaint.Round(40 * Scale));
                var style = Line(SamsungScreen);
                laptop.Turn(Samsung, 55);
                line.HoverOn(new Vector2(20, 20), lineBox, style, WidgetSurface.List);
                Wait(() => line.Service.On(SamsungScreen)?.Level == 55);
                laptop.Turn(Samsung, 65);
                clock.Advance(TimeSpan.FromMilliseconds(Brightness.LookEveryMs));
                Thread.Sleep(50);
                reads = laptop.Reads;
                line.HoverOn(new Vector2(30, 20), lineBox, style, WidgetSurface.List);
                Thread.Sleep(50);
                Assert.Equal(reads, laptop.Reads);
                line.HoverOn(null, lineBox, style, WidgetSurface.List);
                line.HoverOn(new Vector2(30, 20), lineBox, style, WidgetSurface.List);
                Wait(() => line.Service.On(SamsungScreen)?.Level == 65);
                Assert.Empty(laptop.Writes);
            }
        });
    }

    /// <summary>The notification Windows sends for the built-in screen's level, heard by <paramref name="brightness"/>.</summary>
    private static bool Notice(Brightness brightness, uint level)
    {
        var raw = stackalloc byte[24];
        var notice = (TerraFX.Interop.Windows.POWERBROADCAST_SETTING*)raw;
        notice->PowerSetting = TerraFX.Interop.Windows.GUID.GUID_VIDEO_CURRENT_MONITOR_BRIGHTNESS;
        notice->DataLength = 4;
        *(uint*)(raw + 20) = level;
        return brightness.Heard(notice);
    }

    /// <summary>Whether the bar's thumb is drawn at <paramref name="centre"/>: ink above the track there, where only the thumb reaches.</summary>
    private static bool ThumbAt(CellIsland island, IslandStyle style, Vector2 size, Vector2 centre, float thumb)
    {
        var inked = false;
        Offscreen.Bitmap(size.X, size.Y, dc => island.PaintBar(dc, 0, style, size.X, size.Y), bitmap =>
        {
            var (width, height) = ((uint)MathF.Ceiling(size.X), (uint)MathF.Ceiling(size.Y));
            Assert.True(Offscreen.InkOf(bitmap, width, height) is var ink && ink.Right > ink.Left, "the cell drew nothing");
            inked = Offscreen.AlphaAt(bitmap, width, height, (int)MathF.Round(centre.X), (int)MathF.Round(centre.Y - (thumb * 0.35f))) > 128;
        });
        return inked;
    }

    /// <summary>The bar's cell as the keys left it, for the eye: brightness-followed-{before,after}.png in the pictures folder.</summary>
    private static void SavePair(CellIsland island, IslandStyle style, Vector2 size, string when) =>
        Offscreen.Bitmap(size.X, size.Y, dc =>
        {
            PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, size.X, size.Y), 0, PopupPaint.Ink(Color.FromArgb(255, 0x20, 0x20, 0x20)));
            island.PaintBar(dc, 0, style, size.X, size.Y);
        }, bitmap => Offscreen.Save(bitmap, Path.Combine(Offscreen.Pictures, $"brightness-followed-{when}.png")));

    /// <summary>The level a line's slider stands at, laid out as a paint would.</summary>
    private static int LineValue(BrightnessIsland line, D2D_RECT_F box, in TileStyle style)
    {
        line.LayOut(box, style);
        return (int)MathF.Round(line.LinePlaced.OfType<Slider>().Single().Value);
    }

    /// <summary>A message-only window of this thread's, standing for the dock's, which the widgets post to.</summary>
    private static TerraFX.Interop.Windows.HWND MessageWindow()
    {
        fixed (char* cls = "STATIC")
        {
            var window = TerraFX.Interop.Windows.Windows.CreateWindowExW(0, cls, null, 0, 0, 0, 0, 0, TerraFX.Interop.Windows.HWND.HWND_MESSAGE, TerraFX.Interop.Windows.HMENU.NULL, TerraFX.Interop.Windows.HINSTANCE.NULL, null);
            Assert.NotEqual(TerraFX.Interop.Windows.HWND.NULL, window);
            return window;
        }
    }

    private static void DestroyWindow(TerraFX.Interop.Windows.HWND window) => _ = TerraFX.Interop.Windows.Windows.DestroyWindow(window);

    /// <summary>What the widgets posted until now, let go (taken, as the dock takes it: §658).</summary>
    private static void Drain(TerraFX.Interop.Windows.HWND window)
    {
        TerraFX.Interop.Windows.MSG msg;
        while (TerraFX.Interop.Windows.Windows.PeekMessageW(&msg, window, Karpo.Dock.Islands.WM_KARPO_ISLAND, Karpo.Dock.Islands.WM_KARPO_ISLAND, TerraFX.Interop.Windows.PM.PM_REMOVE))
        {
            Karpo.Dock.Islands.Taken((nint)window, (nuint)msg.wParam);
        }
    }

    /// <summary>The tokens of the widgets that asked to be drawn again, once <paramref name="count"/> have; the test fails after five seconds.</summary>
    private static List<uint> Posted(TerraFX.Interop.Windows.HWND window, int count)
    {
        var tokens = new HashSet<uint>();
        var until = DateTime.UtcNow.AddSeconds(5);
        while (tokens.Count < count)
        {
            Assert.True(DateTime.UtcNow < until, "the widget did not ask to be drawn again");
            TerraFX.Interop.Windows.MSG msg;
            while (TerraFX.Interop.Windows.Windows.PeekMessageW(&msg, window, Karpo.Dock.Islands.WM_KARPO_ISLAND, Karpo.Dock.Islands.WM_KARPO_ISLAND, TerraFX.Interop.Windows.PM.PM_REMOVE))
            {
                Karpo.Dock.Islands.Taken((nint)window, (nuint)msg.wParam);
                _ = tokens.Add((uint)(nuint)msg.wParam);
            }
            Thread.Sleep(2);
        }
        return [.. tokens];
    }

    // --- Pictures --------------------------------------------------------------------------------

    /// <summary>
    /// The widget as the bar and a list draw it over two monitors and a television, at 100 and
    /// 150 %, dark and light: brightness-widget-{100,150}-{dark,light}.png in the pictures folder
    /// (KARPO_PNG_DIR). Each shows one slider and no display's name: the bar's cell, and a line one
    /// row tall and three. Asserts only that each drew something; the pictures are for the eye.
    /// </summary>
    [UiFact]
    public void ThePicturesShowOneSlider()
    {
        Offscreen.OnStaThread(gfx =>
        {
            foreach (var (scale, dark) in new[] { (1f, true), (1f, false), (1.5f, true), (1.5f, false) })
            {
                var look = TestLooks.LookFor(dark);
                var theme = FlyoutTheme.For(look);
                var pad = PopupPaint.Round(12 * scale);
                var bar = new IslandStyle(scale, false, 48 * scale, 24 * scale, look) { Screen = DellScreen };
                var along = BrightnessIsland.Plan(bar, bar.Thickness).Along;
                var lineWidth = FolderFlyout.ListWidth(scale);
                var row = PopupPaint.Round(40 * scale);
                var width = pad + MathF.Max(along, lineWidth) + pad;
                var height = pad + bar.Thickness + pad + row + pad + (row * 3) + pad;
                using var island = Made(gfx, Desk());
                using var line = MadeLine(gfx, Desk());
                Offscreen.Bitmap(width, height, dc =>
                {
                    PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, width, height), 0, PopupPaint.Ink(dark ? Color.FromArgb(255, 0x20, 0x20, 0x20) : Color.FromArgb(255, 0xF3, 0xF3, 0xF3)));
                    var y = pad;
                    Offscreen.Shifted(dc, pad, y, () => island.PaintCell(dc, 0, bar, along, bar.Thickness));
                    y += bar.Thickness + pad;
                    var lineStyle = FolderFlyout.StyleFor(scale, look, theme) with { Screen = DellScreen };
                    foreach (var rows in new[] { 1, 3 })
                    {
                        var at = PopupPaint.Rect(pad, y, lineWidth, row * rows);
                        PopupPaint.Fill(dc, at, PopupPaint.Round(8 * scale), PopupPaint.Ink(theme.Base));
                        Offscreen.Shifted(dc, pad, y, () => line.PaintCard(dc, PopupPaint.Rect(0, 0, lineWidth, row * rows), lineStyle, WidgetSurface.List));
                        y += (row * rows) + pad;
                    }
                }, bitmap =>
                {
                    var ink = Offscreen.InkOf(bitmap, (uint)MathF.Ceiling(width), (uint)MathF.Ceiling(height));
                    Assert.True(ink.Right > ink.Left, "the sheet drew nothing");
                    Offscreen.Save(bitmap, Path.Combine(Offscreen.Pictures, $"brightness-widget-{(int)MathF.Round(scale * 100)}-{(dark ? "dark" : "light")}.png"));
                });
            }
        });
    }

    // --- Helpers ---------------------------------------------------------------------------------

    /// <summary>The bar's widget, made over <paramref name="machine"/> through the dock's own table, set to <paramref name="screens"/>.</summary>
    private static BrightnessIsland Made(Gfx gfx, IScreenMachine machine, BrightnessScreens screens = BrightnessScreens.All) => Over(machine, () =>
    {
        var settings = BarKind.Brightness.New();
        settings.Id = "brightness";
        Choose(settings, screens);
        return Karpo.Dock.Islands.Create(Config.IslandKey(null, settings.Id), settings, gfx, new Config());
    });

    /// <summary>The widget as a list line, made over <paramref name="machine"/>.</summary>
    private static BrightnessIsland MadeLine(Gfx gfx, IScreenMachine machine, BrightnessScreens screens = BrightnessScreens.All) => Over(machine, () =>
    {
        var cell = ListKind.Brightness.New();
        cell.Id = "brightness";
        Choose(cell, screens);
        return Karpo.Dock.Islands.CreateLine("layout:default/folder/brightness", cell, ListKind.Brightness, gfx, new Config());
    });

    /// <summary>The widget's setting, as the settings app writes it: no options for every display.</summary>
    private static void Choose(WidgetItem item, BrightnessScreens screens)
    {
        item.Brightness = new BrightnessOptions { Screens = screens };
        item.Normalize();
    }

    /// <summary>Makes a widget with <paramref name="machine"/> standing in for the displays, which it keeps.</summary>
    private static BrightnessIsland Over(IScreenMachine machine, Func<IIsland?> make)
    {
        Brightness.Stand = machine;
        try
        {
            return Assert.IsType<BrightnessIsland>(make());
        }
        finally
        {
            Brightness.Stand = null;
        }
    }

    /// <summary>A list line's style, in a folder opened on <paramref name="screen"/>.</summary>
    private static TileStyle Line(nint screen) =>
        FolderFlyout.StyleFor(Scale, Dark, FlyoutTheme.For(Dark)) with { Screen = screen };

    /// <summary>
    /// The sliders a line shows, top to bottom — found the way the pointer finds them:
    /// pressed down the middle of the box a pixel at a time, and the display each run of presses
    /// took hold of written down, then let go. One entry per slider: two runs of presses with a gap
    /// between them are two sliders, whichever display they hold. Each press asks for a level as a
    /// drag's would; the worker writes them in its own time.
    /// </summary>
    private static List<nint> Sliders(BrightnessIsland island, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var found = new List<nint>();
        var x = (box.left + box.right) / 2;
        var inside = false;
        for (var y = box.top; y < box.bottom; y++)
        {
            if (!island.PressOn(new Vector2(x, y), box, style, surface))
            {
                Assert.Null(island.Holding);
                inside = false;
                continue;
            }
            var screen = island.Holding ?? throw new Xunit.Sdk.XunitException($"a press at {y:0} took the pointer and holds no display");
            island.ReleaseOn(surface);
            Assert.Null(island.Holding);
            if (!inside)
            {
                found.Add(screen);
            }
            inside = true;
        }
        return found;
    }

    /// <summary>How far right the widget's ink reaches in its bar cell of <paramref name="size"/>.</summary>
    private static float InkRight(Gfx gfx, CellIsland island, IslandStyle style, Vector2 size)
    {
        (float Left, float Top, float Right, float Bottom) ink = default;
        Offscreen.Bitmap(
            size.X,
            size.Y,
            dc => island.PaintBar(dc, 0, style, size.X, size.Y),
            bitmap => ink = Offscreen.InkOf(bitmap, (uint)MathF.Ceiling(size.X), (uint)MathF.Ceiling(size.Y)));
        return ink.Right;
    }

    private static Vector2 Centre(D2D_RECT_F box) => new((box.left + box.right) / 2, (box.top + box.bottom) / 2);

    private static void Wait(Func<bool> done)
    {
        var until = DateTime.UtcNow.AddSeconds(5);
        while (!done())
        {
            Assert.True(DateTime.UtcNow < until, "the brightness worker did not get there");
            Thread.Sleep(2);
        }
    }
}
