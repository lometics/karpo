// App buttons' signals in the dock's real visual tree (Scene.Signals.cs): built by the real Scene,
// then asked where the progress and the corner ended up and what they show, and set in place again
// the way the dock sets a changed mark.
//
// The drawing sheets (SignalOffscreenTests) paint what SignalShape and SignalInk say; this is what
// holds the scene to the same answers, so the two cannot drift.
//
// A Scene needs a compositor on a thread with a dispatcher queue and a window to hang on: these
// run like the other scene tests, only with KARPO_UI_TESTS=1, on a WS_POPUP that is never shown.

using System.Numerics;
using System.Runtime.InteropServices;
using Karpo.Dock;
using Karpo.Dock.Native;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using Windows.UI.Composition;
using Windows.UI.Composition.Desktop;
using static TerraFX.Interop.Windows.Windows;
using Color = Windows.UI.Color;
using ICompositorDesktopInterop = TerraFX.Interop.WinRT.ICompositorDesktopInterop;
using IDesktopWindowTarget = TerraFX.Interop.WinRT.IDesktopWindowTarget;
using IDispatcherQueueController = TerraFX.Interop.WinRT.IDispatcherQueueController;

namespace Karpo.Tests.Signals;

public sealed unsafe class SignalSceneTests
{
    private const string BarClass = "karpo.tests.signals";

    private static Look Dressed(bool animations) => new(
        Glass: false,
        Dark: true,
        Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4),
        AccentLight2: Color.FromArgb(255, 0x99, 0xEB, 0xFF),
        Animations: animations);

    private static Pixels Dot() => new() { Data = [0x1C, 0x2B, 0xC4, 255], Width = 1, Height = 1 };

    private static Item App(int index) => new() { Target = $"https://app{index}.example", Name = $"App {index}", Kind = ItemKind.Link, Keys = [] };

    private static Mark Running(TileSignal signal = default) => new(1, false, false, signal);

    [UiFact]
    public void ProgressTakesTheIndicatorsPlace()
    {
        OnABar([Running(), Running(new TileSignal(ProgressKind.Normal, 0.45f))], animations: false, (layout, scene, look) =>
        {
            Assert.Null(scene.ProgressAt(0));
            var shown = scene.ProgressAt(1);
            Assert.NotNull(shown);
            var (run, done, runner, ink, pill) = shown!.Value;
            Assert.Equal(RunOf(layout, 1), run);
            Assert.Equal(SignalShape.Done(new RectF(0, 0, run.Width, run.Height), false, 0.45f), done);
            Assert.False(runner);
            Assert.Equal(SignalInk.Progress(ProgressKind.Normal, look), ink);
            Assert.False(pill, "the running indicator gives way to the progress");
        });
    }

    [UiFact]
    public void EachStateHasItsColour()
    {
        OnABar(
            [Running(new TileSignal(ProgressKind.Paused, 0.7f)), Running(new TileSignal(ProgressKind.Error, 0.3f))],
            animations: false,
            (_, scene, look) =>
            {
                Assert.Equal(SignalInk.Progress(ProgressKind.Paused, look), scene.ProgressAt(0)!.Value.Ink);
                Assert.Equal(SignalInk.Progress(ProgressKind.Error, look), scene.ProgressAt(1)!.Value.Ink);
            });
    }

    [UiTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void ARunWithoutAnEndShowsTheRunnerAndNoShareDone(bool animations)
    {
        OnABar([Running(new TileSignal(ProgressKind.Indeterminate))], animations, (layout, scene, _) =>
        {
            var (run, done, runner, _, pill) = scene.ProgressAt(0)!.Value;
            Assert.Equal(RunOf(layout, 0), run);
            Assert.True(done.IsEmpty);
            Assert.True(runner);
            Assert.False(pill);
        });
    }

    [UiFact]
    public void AChangedMarkIsSetInPlace()
    {
        OnABar([Running(new TileSignal(ProgressKind.Normal, 0.2f))], animations: false, (layout, scene, _) =>
        {
            var before = scene.ProgressAt(0)!.Value.Done;

            scene.SetMark(0, Running(new TileSignal(ProgressKind.Normal, 0.9f)));
            Assert.True(scene.ProgressAt(0)!.Value.Done.Width > before.Width);

            scene.SetMark(0, Running(new TileSignal(ProgressKind.Indeterminate)));
            Assert.True(scene.ProgressAt(0)!.Value.Runner);

            // No progress: the run goes and the running indicator is back.
            scene.SetMark(0, Running());
            Assert.Null(scene.ProgressAt(0));
        });
    }

    /// <summary>
    /// Spotify's pin: a progress with nothing done is no progress on the bar — no run, no bare
    /// track, and the running indicator where it was. A share done that drops back to nothing
    /// brings the indicator back too.
    /// </summary>
    [UiFact]
    public void NothingDoneKeepsTheRunningIndicator()
    {
        OnABar(
            [Running(new TileSignal(ProgressKind.Normal, 0f)), Running(new TileSignal(ProgressKind.Paused, 0f)), Running(new TileSignal(ProgressKind.Normal, 0.001f))],
            animations: false,
            (_, scene, _) =>
            {
                Assert.Null(scene.ProgressAt(0));
                Assert.Null(scene.ProgressAt(1));
                // Less than a pixel of the run done is an empty fill on screen.
                Assert.Null(scene.ProgressAt(2));
                Assert.True(scene.PillShownAt(0), "the running indicator gave way to nothing");

                scene.SetMark(0, Running(new TileSignal(ProgressKind.Normal, 0.45f)));
                Assert.NotNull(scene.ProgressAt(0));
                Assert.False(scene.PillShownAt(0));

                scene.SetMark(0, Running(new TileSignal(ProgressKind.Normal, 0f)));
                Assert.Null(scene.ProgressAt(0));
                Assert.True(scene.PillShownAt(0), "the running indicator is not back");
            });
    }

    [UiFact]
    public void TheOverlaySitsOnTheIconsCorner()
    {
        OnABar([Running(new TileSignal(Overlay: Dot(), OverlayText: "Away"))], animations: false, (layout, scene, _) =>
        {
            var corner = scene.CornerAt(0);
            Assert.NotNull(corner);
            var icon = layout.IconBox(Hit.Tile(0), layout.Icon);
            var side = SignalShape.CornerSide(layout.Icon);
            var expected = SignalShape.Corner(icon, new Vector2(side, side));
            // On the icon's own sprite, so relative to the icon.
            Assert.Equal(new RectF(expected.X - icon.X, expected.Y - icon.Y, side, side), corner!.Value);

            scene.SetMark(0, Running());
            Assert.Null(scene.CornerAt(0));
        });
    }

    [UiFact]
    public void ABadgeTakesTheCornerAndGrowsLeftForMoreFigures()
    {
        OnABar(
            [Running(new TileSignal(Overlay: Dot(), Badge: Badge.Of("3"))), new Mark(0, false, false, new TileSignal(Badge: Badge.Of("120")))],
            animations: false,
            (layout, scene, _) =>
            {
                var one = scene.CornerAt(0)!.Value;
                var many = scene.CornerAt(1)!.Value;
                var side = SignalShape.CornerSide(layout.Icon);
                Assert.Equal(side, one.Height);
                Assert.Equal(side, one.Width);
                // "99+" is longer, and ends where the one-figure badge ends.
                Assert.True(many.Width > one.Width);
                Assert.Equal(one.Right, many.Right);
            });
    }

    [UiFact]
    public void ATileThatNeverHadASignalMakesNothing()
    {
        OnABar([Running(), Running()], animations: false, (_, scene, _) =>
        {
            Assert.Null(scene.ProgressAt(0));
            Assert.Null(scene.CornerAt(1));
        });
    }

    [UiFact]
    public void OnAWindowsOwnButtonTheRunSpansTheButtonAndTheCornerSitsOnItsIcon()
    {
        // Buttons a window each, as a bar that never combines has them: labelled with their titles.
        var content = new Content
        {
            Tiles = [(App(0), Running(new TileSignal(ProgressKind.Normal, 0.5f, Dot(), "Recording"))), (App(1), Running())],
            TileParts = [null, null],
            Labels = ["Quarterly report - Word", "Inbox - Mail"],
        };
        OnItsOwnThread(gfx =>
        {
            var look = Dressed(animations: false);
            var config = new Config { Height = 48 };
            var area = new RECT { left = 0, top = 0, right = 1920, bottom = 1080 };
            var layout = new Layout(gfx, 96, area, area, Edge.Bottom, config, look, [new PartSpec(Config.IslandKey(null, "apps"), BarKind.Apps)], content, editing: false);
            Assert.True(layout.Labelled(0), "the first button is a labelled one");
            var window = MakeWindow();
            DesktopWindowTarget? target = null;
            try
            {
                target = Target(gfx, window);
                using var scene = Scenes.Build(gfx, target, layout, content, look, new Icons(), shown: true);
                var button = layout.ItemRect(Hit.Tile(0));
                var run = scene.ProgressAt(0)!.Value.Run;
                // Along the whole button, label and all, as its running indicator is centred under it.
                Assert.Equal(RunOf(layout, 0), run);
                Assert.True(run.Width > button.Width / 2f, $"the run {run} is short of the button {button}");
                var icon = layout.IconBox(Hit.Tile(0), layout.Icon);
                var side = SignalShape.CornerSide(layout.Icon);
                var corner = SignalShape.Corner(icon, new Vector2(side, side));
                Assert.Equal(new RectF(corner.X - icon.X, corner.Y - icon.Y, side, side), scene.CornerAt(0));
            }
            finally
            {
                _ = DestroyWindow(window);
            }
        });
    }

    /// <summary>
    /// A change of display (another mode, another scale) lays the bar out again: the icon's sprite and
    /// the part's containers the signals hang in are made anew and the old ones closed. The signals
    /// move to the new ones — their visuals are never asked about a closed parent (RO_E_CLOSED, which
    /// karpo.log had from Scene.PlaceSignals and Scene.Sweep at every mode change of a display with a
    /// badged or progressing app) — and when the tile and its part go, so do they.
    /// </summary>
    [UiTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void SignalsFollowTheirTileThroughARescaleAndGoWithIt(bool animations)
    {
        var config = new Config { Height = 48 };
        var area = new RECT { left = 0, top = 0, right = 1920, bottom = 1080 };
        var look = Dressed(animations);
        var marks = new[] { Running(new TileSignal(ProgressKind.Normal, 0.5f, Dot(), "Recording", Badge.Of("3"))), Running(new TileSignal(ProgressKind.Indeterminate)) };
        var content = new Content { Tiles = [.. marks.Select((mark, index) => (App(index), mark))] };
        Layout At(uint dpi, int tiles) => Layout.ForTests(
            dpi, area, Edge.Bottom, config, tiles > 0 ? [new PartSpec(Config.IslandKey(null, "apps"), BarKind.Apps)] : [], Layout.CellPx(dpi, config), tiles: tiles);
        OnItsOwnThread(gfx =>
        {
            var window = MakeWindow();
            Scene? scene = null;
            try
            {
                var target = Target(gfx, window);
                var icons = new Icons();
                scene = Scenes.Build(gfx, target, At(96, 2), content, look, icons, shown: true);
                Assert.NotNull(scene.CornerAt(0));

                // The display at 150 %, then back: every tile's sprite is another.
                var large = At(144, 2);
                scene.Update(large, content, look, icons, shown: true);
                var icon = large.IconBox(Hit.Tile(0), large.Icon);
                Assert.NotNull(scene.CornerAt(0));
                Assert.Equal(RunOf(large, 0), scene.ProgressAt(0)!.Value.Run);
                Assert.True(scene.CornerAt(0)!.Value.Right <= icon.Width + 1, "the corner is not on the new icon");
                scene.Update(At(96, 2), content, look, icons, shown: true);
                Assert.NotNull(scene.ProgressAt(1));

                // The apps gone from the bar, part and all: their signals go with them, and come back new.
                scene.Update(At(96, 0), new Content(), look, icons, shown: true);
                scene.Update(At(144, 2), content, look, icons, shown: true);
                Assert.NotNull(scene.CornerAt(0));
                Assert.NotNull(scene.ProgressAt(1));
            }
            finally
            {
                scene?.Dispose();
                _ = DestroyWindow(window);
            }
        });
    }

    private static RectF RunOf(Layout layout, int index) =>
        SignalShape.Run(layout.ItemRect(Hit.Tile(index)), layout.Edge, layout.Pill, layout.Stroke, layout.UnitPx(4));

    // --- Harness (as SceneTreeTests') ------------------------------------------------------------

    /// <summary>A 48-DIP bottom bar at 100 % with one app button per mark, built as the dock builds it.</summary>
    private static void OnABar(Mark[] marks, bool animations, Action<Layout, Scene, Look> body)
    {
        var config = new Config { Height = 48 };
        var area = new RECT { left = 0, top = 0, right = 1920, bottom = 1080 };
        var look = Dressed(animations);
        OnItsOwnThread(gfx =>
        {
            var layout = Layout.ForTests(
                96, area, Edge.Bottom, config, [new PartSpec(Config.IslandKey(null, "apps"), BarKind.Apps)], Layout.CellPx(96, config), tiles: marks.Length);
            var content = new Content { Tiles = [.. marks.Select((mark, index) => (App(index), mark))] };
            var window = MakeWindow();
            DesktopWindowTarget? target = null;
            Scene? scene = null;
            try
            {
                target = Target(gfx, window);
                scene = Scenes.Build(gfx, target, layout, content, look, new Icons(), shown: true);
                body(layout, scene, look);
            }
            finally
            {
                scene?.Dispose();
                _ = DestroyWindow(window);
            }
        });
    }

    private static HWND MakeWindow()
    {
        var instance = (HINSTANCE)(void*)GetModuleHandleW(null);
        fixed (char* className = BarClass)
        fixed (char* title = "Karpo signal tests")
        {
            var type = new WNDCLASSW { lpfnWndProc = &Procedure, hInstance = instance, lpszClassName = className };
            _ = RegisterClassW(&type);
            // Never shown: no WS_VISIBLE, and the tree only has to have somewhere to hang.
            var hwnd = CreateWindowExW(
                WS.WS_EX_TOOLWINDOW | WS.WS_EX_NOACTIVATE | WS.WS_EX_NOREDIRECTIONBITMAP,
                className,
                title,
                WS.WS_POPUP,
                0,
                1008,
                1920,
                72,
                HWND.NULL,
                HMENU.NULL,
                instance,
                null);
            Assert.True(hwnd != HWND.NULL, $"CreateWindowExW failed: error {Marshal.GetLastSystemError()}");
            return hwnd;
        }
    }

    [UnmanagedCallersOnly]
    private static LRESULT Procedure(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam) =>
        DefWindowProcW(hwnd, msg, wParam, lParam);

    private static DesktopWindowTarget Target(Gfx gfx, HWND hwnd)
    {
        var desktop = ComUtil.Query<ICompositorDesktopInterop>(gfx.Compositor);
        try
        {
            IDesktopWindowTarget* raw = null;
            ComUtil.Check(desktop->CreateDesktopWindowTarget(hwnd, BOOL.FALSE, &raw));
            return ComUtil.Adopt<DesktopWindowTarget>(raw);
        }
        finally
        {
            desktop->Release();
        }
    }

    private static void OnItsOwnThread(Action<Gfx> body)
    {
        Exception? failed = null;
        var thread = new Thread(() =>
        {
            try
            {
                var options = new DispatcherQueueOptions
                {
                    dwSize = (uint)sizeof(DispatcherQueueOptions),
                    threadType = DISPATCHERQUEUE_THREAD_TYPE.DQTYPE_THREAD_CURRENT,
                    apartmentType = DISPATCHERQUEUE_THREAD_APARTMENTTYPE.DQTAT_COM_STA,
                };
                IDispatcherQueueController* controller = null;
                ComUtil.Check(CreateDispatcherQueueController(options, &controller));
                try
                {
                    using var gfx = new Gfx(gpu: false);
                    body(gfx);
                }
                finally
                {
                    controller->Release();
                }
            }
            catch (Exception error)
            {
                failed = error;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failed is not null)
        {
            throw new Xunit.Sdk.XunitException("On the scene's thread: " + failed);
        }
    }
}
