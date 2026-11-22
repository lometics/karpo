// §637: a layout switch filmed as the screen shows it (FrameFilm, KARPO_UI_TESTS=1), the bar a real window on a hidden desktop
// laid out and switched as the dock's DockWindow does (RetainedBarTests.Bar.Switch): a taskbar into a dock and back, an islands
// taskbar into a flush one with other widgets, a bar moving edge — each by the one morph with motion on: the same window, a new
// scene over the old, the widgets on both gliding, the rest fading, frames between for about the morph's time and no jump in
// them; with motion off every frame the before or the end, nothing morphing. And the numbers: what a switch costs the dock's
// thread on two 4K displays at 150 %, and what DWM said meanwhile. Strips: film-layout-*.png in KARPO_PNG_DIR.

using System.Diagnostics;
using System.Text;
using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using Xunit.Abstractions;
using static TerraFX.Interop.Windows.Windows;
using Bar = Karpo.Tests.Present.RetainedBarTests.Bar;
using Edge = Karpo.Core.Edge;
using Film = Karpo.Tests.Present.FrameFilm.Film;

namespace Karpo.Tests.Present;

public sealed unsafe class BarMorphFilmTests(ITestOutputHelper output)
{
    private static readonly RECT Display = new() { right = 1280, bottom = 720 };

    private static readonly Look Moving = RetainedBarTests.Still with { Animations = true, Colors = new Palette(Card: new Argb(0x40, 0xFF, 0xFF, 0xFF)) };

    private static readonly Look Still = Moving with { Animations = false };

    private static string Key(string id) => Config.IslandKey(null, id);

    private static PartSpec Spec(string id, BarKind kind, BarSection section) => new(Key(id), kind) { Section = section };

    /// <summary>A bar of layout <paramref name="id"/> as the dock lays it out at <paramref name="edge"/>: its type's look, its spacing, its id.</summary>
    private static Config As(string id, LayoutType type, int spacing = Config.DefaultSpacing, Edge edge = Edge.Bottom)
    {
        var layout = new TaskbarLayout { Id = id, Type = type, Spacing = spacing };
        return new Config().WithEdge(layout.Edges(edge)[edge]);
    }

    /// <summary>Karpo's taskbar: Start, Task view and the apps at the left, the tray and Show desktop at the right.</summary>
    private static readonly List<PartSpec> Taskbar =
    [
        Spec("start", BarKind.Start, BarSection.Start),
        Spec("task", BarKind.TaskView, BarSection.Start),
        Spec("apps", BarKind.Apps, BarSection.Start),
        Spec("tray", BarKind.Tray, BarSection.End),
        Spec("desk", BarKind.ShowDesktop, BarSection.End),
    ];

    /// <summary>A dock of another layout: the snipping tool alone at the left, Start (another one) and the same apps in the middle, a tray of its own at the right.</summary>
    private static readonly List<PartSpec> Docked =
    [
        Spec("d-snip", BarKind.SnippingTool, BarSection.Start),
        Spec("d-start", BarKind.Start, BarSection.Middle),
        Spec("apps", BarKind.Apps, BarSection.Middle),
        Spec("d-tray", BarKind.Tray, BarSection.End),
    ];

    /// <summary>A flush taskbar of a third layout: everything in the middle, Windows 11's way, and Show desktop.</summary>
    private static readonly List<PartSpec> Centred =
    [
        Spec("w-start", BarKind.Start, BarSection.Middle),
        Spec("w-search", BarKind.Search, BarSection.Middle),
        Spec("w-task", BarKind.TaskView, BarSection.Middle),
        Spec("w-apps", BarKind.Apps, BarSection.Middle),
        Spec("w-desk", BarKind.ShowDesktop, BarSection.End),
    ];

    /// <summary>Three open apps and the tray's icons, for every tray of the films' bars.</summary>
    private static Content Things()
    {
        var things = RetainedBarTests.Things(3);
        var trays = (Dictionary<string, BarTray>)things.Trays!;
        trays[Key("d-tray")] = trays[Key("tray")];
        return things;
    }

    /// <summary>Pumped until the bar's morph is over (its batch ended), a second at most.</summary>
    private static void Over(Bar bar)
    {
        var until = Environment.TickCount64 + 2000;
        while (bar.Morph?.Running == true && Environment.TickCount64 < until)
        {
            FrameFilm.Pump();
            Thread.Sleep(5);
        }
        Assert.False(bar.Morph?.Running == true, "the morph never ended: its batch never said so");
    }

    /// <summary>
    /// A morph as the film saw it: at least <paramref name="frames"/> frames between the before and the end (the captures of a
    /// loaded machine come every 30 to 70 ms), over at least <paramref name="share"/> of the morph's time; each further along the way
    /// from the before to the end than the last, never back (a flicker), and no jump of more than 70 % of the way between two —
    /// measured over black (§705: the capture flattens what is see-through on black, which over the backdrop's colour read a
    /// ground at a tenth of its strength as nearly all of it).
    /// </summary>
    private string Morphed(Film film, Bar? bar = null, int frames = 3, double share = 0.4)
    {
        Assert.True(film.Differs(film.Before, film.End) > 0.02, $"{film.Name}: the before and the end are the same picture");
        // How far along each frame is: from the before against to the end, 0 to 1.
        double Along(FrameFilm.Shot shot)
        {
            // §705: over black, as a fading ground is seen, not flattened onto the backdrop's colour.
            var (from, to) = (film.DistanceOverBlack(film.Before, shot, film.Area), film.DistanceOverBlack(shot, film.End, film.Area));
            return from + to <= 0 ? 1 : from / (from + to);
        }
        var between = film.Frames.Where(shot => film.Differs(shot, film.Before) > 0.002 && film.Differs(shot, film.End) > 0.002).ToList();
        Assert.True(between.Count >= frames, $"{film.Name}: only {between.Count} frames between the before and the end — no morph{Environment.NewLine}{film.Report()}");
        var lasted = between[^1].At - between[0].At;
        Assert.True(lasted >= share * Motion.Normal.Ms(MotionKind.Morph), $"{film.Name}: the frames between lasted {lasted:F0} ms{Environment.NewLine}{film.Report()}");
        var steps = new[] { 0.0 }.Concat(between.Select(Along)).Append(1.0).ToList();
        var pairs = steps.Zip(steps.Skip(1), (a, b) => b - a).ToList();
        Assert.True(pairs.Min() > -0.2, $"{film.Name}: a frame went back {-pairs.Min():P0} of the way: {string.Join(" ", steps.Select(step => step.ToString("F2")))}");
        Assert.True(pairs.Max() < 0.7, $"{film.Name}: a jump of {pairs.Max():P0} of the way: {string.Join(" ", steps.Select(step => step.ToString("F2")))}");
        var said = $"{film.Name}: {between.Count} frames between over {lasted:F0} ms, the most between two {pairs.Max():P0} of the way; {string.Join(" ", steps.Select(step => step.ToString("F2")))}";
        output.WriteLine(said);
        if (bar is not null)
        {
            Over(bar);
        }
        return said;
    }
    /// <summary>
    /// A taskbar into a dock of another layout and back (the person's "dock to normal taskbar"): the same window, a new scene over
    /// the old one, the apps gliding by their key, Start and the tray by their kind, the rest fading; frames between for about a
    /// third of a second, no jump; once over, the old scene let go of and the new one where it ends. Motion off: every frame the
    /// before or the end, the scene laid out again in place.
    /// </summary>
    [UiFact]
    public void ATaskbarMorphsIntoADockAndBack()
    {
        RetainedBarTests.OnHiddenDesktop("morph-taskbar-dock", (gfx, instance) =>
        {
            using var bar = new Bar(gfx, instance);
            var window = bar.Hwnd;
            bar.Lay(Taskbar, Things(), Moving, As("taskbar", LayoutType.Taskbar));
            FrameFilm.Settle();
            var taskbarScene = bar.Scene;
            var there = FrameFilm.Shoot("layout-taskbar-to-dock", gfx, bar.Frame, () => [bar.Hwnd], () => bar.Switch(Docked, Things(), Moving, As("dock", LayoutType.Dock)), filmMs: 500);
            Assert.Equal(window, bar.Hwnd);
            Assert.NotSame(taskbarScene, bar.Scene);
            var plan = Assert.IsType<Morphing>(bar.Morph).Plan;
            Assert.Equal(3, plan.Glides.Count);
            Assert.Equal(2, plan.Leaving.Count);
            Assert.Single(plan.Entering);
            Assert.Equal(3, plan.GroundsFrom.Count);
            Morphed(there, bar);
            Assert.False(bar.Scene.Morphing);

            var dockScene = bar.Scene;
            var back = FrameFilm.Shoot("layout-dock-to-taskbar", gfx, bar.Frame, () => [bar.Hwnd], () => bar.Switch(Taskbar, Things(), Moving, As("taskbar", LayoutType.Taskbar)), filmMs: 500);
            Assert.NotSame(dockScene, bar.Scene);
            Assert.Single(bar.Morph!.Plan.GroundsFrom);
            Morphed(back, bar);

            // Motion off: laid out again in place, at once.
            var still = bar.Scene;
            var off = FrameFilm.Shoot("layout-taskbar-to-dock-off", gfx, bar.Frame, () => [bar.Hwnd], () => bar.Switch(Docked, Things(), Still, As("dock", LayoutType.Dock)));
            Assert.Same(still, bar.Scene);
            off.Holds();
        });
    }

    /// <summary>
    /// An islands taskbar (spaced, a card each) into a flush taskbar of another layout, everything in the middle: its strip stays
    /// where it is under the new one, the widgets glide by their kind from the left to the middle, the rest fade.
    /// </summary>
    [UiFact]
    public void AnIslandsTaskbarMorphsIntoAFlushOne()
    {
        RetainedBarTests.OnHiddenDesktop("morph-islands-flush", (gfx, instance) =>
        {
            using var bar = new Bar(gfx, instance);
            bar.Lay(Taskbar, Things(), Moving, As("islands", LayoutType.Taskbar, spacing: 8));
            FrameFilm.Settle();
            var film = FrameFilm.Shoot("layout-islands-to-flush", gfx, bar.Frame, () => [bar.Hwnd], () => bar.Switch(Centred, Things(), Moving, As("flush", LayoutType.Taskbar, spacing: 0)), filmMs: 500);
            var plan = bar.Morph!.Plan;
            Assert.Equal(4, plan.Glides.Count);
            // The strip from the strip: the islands bar's, end to end.
            Assert.Equal(1280f, Assert.Single(plan.GroundsFrom).Rect.Width);
            Morphed(film, bar);
            var back = FrameFilm.Shoot("layout-flush-to-islands", gfx, bar.Frame, () => [bar.Hwnd], () => bar.Switch(Taskbar, Things(), Moving, As("islands", LayoutType.Taskbar, spacing: 8)), filmMs: 500);
            Morphed(back, bar);
        });
    }

    /// <summary>
    /// The bar moving edge (the display's edge another; its new window made, the old one let go of, in one change, §358): the
    /// old bar goes into its edge as a bar going into hiding, its window gone once it has; the new one comes out of its own as a
    /// bar out of hiding; one of the two in sight in every frame. Motion off: every frame the before or the end.
    /// </summary>
    [UiFact]
    public void ABarMovingEdgeGoesAndComesByTheMotion()
    {
        RetainedBarTests.OnHiddenDesktop("morph-edge", (gfx, instance) =>
        {
            foreach (var look in new[] { Moving, Still })
            {
                var bottom = new Bar(gfx, instance);
                bottom.Lay(Taskbar, Things(), look, As("taskbar", LayoutType.Taskbar));
                FrameFilm.Settle();
                var strip = bottom.OnScreen(bottom.Layout.PanelRect, inset: 6);
                Bar? left = null;
                try
                {
                    var film = FrameFilm.Shoot(look.Animations ? "layout-edge-bottom-to-left" : "layout-edge-bottom-to-left-off", gfx, Display, () => [bottom.Hwnd, left?.Hwnd ?? HWND.NULL], () =>
                    {
                        left = new Bar(gfx, instance, Edge.Left, shown: false);
                        left.Lay(Taskbar, Things(), look, As("taskbar", LayoutType.Taskbar, edge: Edge.Left));
                        left.Show();
                        if (look.Animations)
                        {
                            left.ComeByMotion();
                            bottom.GoByMotion(look);
                        }
                        else
                        {
                            bottom.Dispose();
                        }
                    }, filmMs: 450);
                    var side = left!.OnScreen(left.Layout.PanelRect, inset: 6);
                    film.StaysInSight(0.3, strip, side);
                    if (look.Animations)
                    {
                        Morphed(film, frames: 2, share: 0.2);
                    }
                    else
                    {
                        film.Holds();
                    }
                }
                finally
                {
                    bottom.Dispose();
                    left?.Dispose();
                    FrameFilm.Settle();
                }
            }
        });
    }

    /// <summary>
    /// What a switch costs on two 4K displays at 150 % (3840 × 2160 at 144 DPI, side by side), the dock's way: each bar's new
    /// scene laid out and its morph started in one frame of the dock's, five times each way; and what DWM counted meanwhile
    /// (DwmGetCompositionTimingInfo: frames composed, missed, dropped). The numbers go to the test's output and
    /// layout-morph-4k.txt in KARPO_PNG_DIR. Held: a switch of both bars under 100 ms of the thread (the morph itself runs in DWM,
    /// not on it), and every morph over within a second.
    /// </summary>
    [UiFact]
    public void TwoFourKDisplaysAt150PercentSwitchTogether()
    {
        RetainedBarTests.OnHiddenDesktop("morph-4k", (gfx, instance) =>
        {
            RECT[] displays = [new() { right = 3840, bottom = 2160 }, new() { left = 3840, right = 7680, bottom = 2160 }];
            var bars = displays.Select(display => new Bar(gfx, instance, dpi: 144, on: display)).ToList();
            var said = new StringBuilder();
            try
            {
                foreach (var bar in bars)
                {
                    bar.Lay(Taskbar, Things(), Moving, As("taskbar", LayoutType.Taskbar));
                }
                FrameFilm.Settle();
                var acts = new List<double>();
                var dwm = new List<string>();
                for (var round = 0; round < 10; round++)
                {
                    var (specs, config) = round % 2 == 0 ? (Docked, As("dock", LayoutType.Dock)) : (Taskbar, As("taskbar", LayoutType.Taskbar));
                    var before = Timing();
                    var clock = Stopwatch.StartNew();
                    using (Surfaces.Begin(gfx.Compositor))
                    {
                        foreach (var bar in bars)
                        {
                            bar.Switch(specs, Things(), Moving, config);
                        }
                    }
                    acts.Add(clock.Elapsed.TotalMilliseconds);
                    Assert.All(bars, bar => Assert.True(bar.Morph?.Running == true, "no morph started"));
                    var until = Environment.TickCount64 + 1000;
                    while (bars.Any(bar => bar.Morph?.Running == true) && Environment.TickCount64 < until)
                    {
                        FrameFilm.Pump();
                        Thread.Sleep(2);
                    }
                    var lasted = clock.Elapsed.TotalMilliseconds;
                    Assert.All(bars, bar => Assert.False(bar.Morph?.Running == true, "a morph never ended"));
                    var after = Timing();
                    dwm.Add(after is { } a && before is { } b
                        ? $"round {round}: {(round % 2 == 0 ? "taskbar→dock" : "dock→taskbar")} act {acts[^1]:F1} ms, over at {lasted:F0} ms; DWM composed {a.cFrame - b.cFrame} frames at {1000.0 * a.qpcRefreshPeriod / Frequency():F2} ms a refresh, missed {a.cFramesMissed - b.cFramesMissed}, dropped {a.cFramesDropped - b.cFramesDropped}, late {a.cFramesLate - b.cFramesLate}"
                        : $"round {round}: act {acts[^1]:F1} ms, over at {lasted:F0} ms; DWM said nothing");
                }
                said.AppendLine($"Two 3840x2160 displays at 144 DPI, both bars switched in one frame, 10 rounds alternating taskbar and dock.");
                said.AppendLine($"The dock's thread per switch of both: median {Median(acts):F1} ms, most {acts.Max():F1} ms, least {acts.Min():F1} ms.");
                foreach (var line in dwm)
                {
                    said.AppendLine(line);
                }
                output.WriteLine(said.ToString());
                File.WriteAllText(Path.Combine(Offscreen.Pictures, "layout-morph-4k.txt"), said.ToString());
                Assert.True(Median(acts) < 100, said.ToString());
            }
            finally
            {
                foreach (var bar in bars)
                {
                    bar.Dispose();
                }
                FrameFilm.Settle();
            }
        });

        static double Median(List<double> values) => values.Order().ElementAt(values.Count / 2);

        static DWM_TIMING_INFO? Timing()
        {
            var info = new DWM_TIMING_INFO { cbSize = (uint)sizeof(DWM_TIMING_INFO) };
            return DwmGetCompositionTimingInfo(HWND.NULL, &info).SUCCEEDED ? info : null;
        }

        static double Frequency()
        {
            LARGE_INTEGER frequency;
            QueryPerformanceFrequency(&frequency);
            return frequency.QuadPart;
        }
    }
}
