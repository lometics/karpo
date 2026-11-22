// The dock's real visual tree, built by the real Scene, asked where things are rather than what
// they look like.
//
// Why this file exists. Every other test of the bar's look measures a Layout and then draws the
// answer itself, so the tree Scene builds was never exercised — and a whole class of fault lives
// only there. The one that was shipped: a compact bar's band went into the first part's own
// container, so in edit mode it turned with that part's wobble and the whole background of the
// taskbar swung about one widget's middle. Every rectangle was right; every test was green. What
// makes it a fault is the tree, and only the tree says so.
//
// The rule these tests hold to is one line: nothing that spans more than one part may live inside
// a part's container, because a part's container is what edit mode slides.
//
// The fault was found through the wobble, which turned every widget's container. The wobble is
// gone — the widgets hold still while the bar is arranged — so what is left to catch it is the
// drag, which moves the same containers along the bar. That is the half that still
// bites, and it is the half these tests hold.
//
// A Scene needs a compositor, which needs a dispatcher queue, which needs a thread of its own, and
// a window to hang the tree on — so these run like the other offscreen tests, only with
// KARPO_UI_TESTS=1. Nothing is ever shown: the window is a WS_POPUP that is never made visible.

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

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    private const string BarClass = "karpo.tests.scene";
    private const int ScreenWidth = 1920;
    private const int ScreenHeight = 1080;

    private static Look Dressed => new(
        Glass: false,
        Dark: true,
        Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4),
        Animations: false);

    private static PartSpec Spec(string id, BarKind kind) => new(Config.IslandKey(null, id), kind);

    /// <summary>
    /// floating: the scene shows a panel exactly where the layout puts each section's, and none for
    /// an empty section; and no card at all, nor the band a connected bar with backgrounds once wore.
    /// </summary>
    [UiFact]
    public void AFloatingBarShowsThePanelsTheLayoutSays()
    {
        OnABar(48, [Spec("w", BarKind.WidgetsBoard), Spec("c", BarKind.NightLight) with { Section = BarSection.End }], (layout, scene) =>
        {
            var panels = layout.SectionPanels();
            Assert.False(panels[(int)BarSection.Start].IsEmpty);
            Assert.True(panels[(int)BarSection.Middle].IsEmpty, "the empty middle has no panel");
            Assert.False(panels[(int)BarSection.End].IsEmpty);
            Assert.Equal(panels.Where(panel => !panel.IsEmpty), scene.FloatingPanelRects());
            Assert.Null(scene.Registered("card:band"));
            Assert.Null(scene.Registered($"card:{Config.IslandKey(null, "w")}"));
        }, editing: false, style: BarStyle.Floating);
    }

    /// <summary>
    /// floating: while a block is carried the panels follow the sections as they open and close for
    /// it — the clock carried out of the end leaves it with no panel — and go back when it is put down.
    /// </summary>
    [UiFact]
    public void AFloatingBarsPanelsFollowACarriedBlock()
    {
        OnABar(48, [Spec("w", BarKind.WidgetsBoard), Spec("c", BarKind.NightLight) with { Section = BarSection.End }], (layout, scene) =>
        {
            var slot = new BarSlot(BarSection.Start, 0);

            scene.Edit(new EditView(Dragging: 1, PointerAlong: 40f, DropIndex: slot));

            var carried = layout.SectionPanels(1, slot);
            Assert.True(carried[(int)BarSection.End].IsEmpty, "the end holds nothing while its clock is carried away");
            Assert.True(carried[(int)BarSection.Start].Width > layout.SectionPanels()[(int)BarSection.Start].Width);
            Assert.Equal(carried.Where(panel => !panel.IsEmpty), scene.FloatingPanelRects());

            scene.Edit(null);

            Assert.Equal(layout.SectionPanels().Where(panel => !panel.IsEmpty), scene.FloatingPanelRects());
        }, style: BarStyle.Floating);
    }

    /// <summary>
    /// floating, §511: a section is one dock whatever it holds — nothing splits it —, and the scene shows its one
    /// panel where the layout puts it, at rest and in edit mode alike.
    /// </summary>
    [UiFact]
    public void ASectionIsOneDocksPanelInTheScene()
    {
        List<PartSpec> specs = [Spec("w", BarKind.WidgetsBoard), Spec("t", BarKind.TouchKeyboard), Spec("c", BarKind.EnergySaver)];
        foreach (var editing in new[] { false, true })
        {
            OnABar(48, specs, (layout, scene) =>
            {
                Assert.Single(layout.SectionPanels(), panel => !panel.IsEmpty);
                Assert.Equal(layout.SectionPanels().Where(panel => !panel.IsEmpty), scene.FloatingPanelRects());
            }, editing: editing, style: BarStyle.Floating);
        }
    }

    /// <summary>A bar that does not float has no panels: its material is one strip from end to end.</summary>
    [UiFact]
    public void ABarThatDoesNotFloatHasNoPanels()
    {
        foreach (var style in new[] { BarStyle.Islands })
        {
            OnABar(48, [Spec("w", BarKind.WidgetsBoard)], (layout, scene) =>
            {
                Assert.All(layout.SectionPanels(), panel => Assert.True(panel.IsEmpty));
                Assert.Empty(scene.FloatingPanelRects());
            }, editing: false, style: style);
        }
    }

    /// <summary>Edit mode draws no remove badge any more: a block comes off by its menu or by Delete.</summary>
    [UiFact]
    public void NoBlockWearsARemoveBadge()
    {
        OnABar(41, [Spec("w", BarKind.WidgetsBoard), Spec("c", BarKind.NightLight)], (_, scene) =>
        {
            foreach (var id in new[] { "w", "c" })
            {
                var key = Config.IslandKey(null, id);
                Assert.Null(scene.Registered($"edit:{key}:badge"));
                Assert.Null(scene.Registered($"edit:{key}:badge-glyph"));
            }
        });
    }

    /// <summary>How far <paramref name="visual"/> stands turned, its containers counted with it.</summary>
    private static float TurnOf(Visual visual)
    {
        var turn = 0f;
        for (Visual? at = visual; at is not null; at = at.Parent)
        {
            turn += at.RotationAngleInDegrees;
        }
        return turn;
    }

    /// <summary>How far <paramref name="visual"/> stands from its place, its containers counted with it.</summary>
    private static Vector3 ShiftOf(Visual visual)
    {
        var shift = Vector3.Zero;
        for (Visual? at = visual.Parent; at is not null; at = at.Parent)
        {
            shift += at.Offset;
        }
        return shift;
    }

    /// <summary>
    /// Builds a bar of <paramref name="specs"/> in edit mode (at rest where <paramref name="editing"/>
    /// is off), on a thread with a dispatcher queue and a window of its own, in
    /// <paramref name="look"/> (<see cref="Dressed"/> by default, animations off), and hands
    /// <paramref name="body"/> the layout and the scene.
    /// </summary>
    private static void OnABar(int height, IReadOnlyList<PartSpec> specs, Action<Layout, Scene> body, Look? look = null, bool editing = true, BarStyle? style = null, int spacing = Config.DefaultSpacing, int tiles = 0)
    {
        var config = new Config { Height = height, Style = style, Layouts = [new TaskbarLayout { Spacing = spacing }] };
        var area = new RECT { left = 0, top = 0, right = ScreenWidth, bottom = ScreenHeight };
        OnItsOwnThread(gfx =>
        {
            var layout = Layout.ForTests(96, area, Edge.Bottom, config, specs, Layout.CellPx(96, config), editing: editing, tiles: tiles);
            var window = MakeWindow();
            DesktopWindowTarget? target = null;
            Scene? scene = null;
            try
            {
                target = Target(gfx, window);
                scene = Scenes.Build(gfx, target, layout, new Content(), look ?? Dressed, new Icons(), shown: true, edit: editing ? new EditView() : null);
                body(layout, scene);
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
        fixed (char* title = "Karpo scene tests")
        {
            var type = new WNDCLASSW
            {
                lpfnWndProc = &Procedure,
                hInstance = instance,
                lpszClassName = className,
                hCursor = LoadCursorW(HINSTANCE.NULL, IDC.IDC_ARROW),
            };
            _ = RegisterClassW(&type);
            // Never shown: no WS_VISIBLE, and the tree only has to have somewhere to hang.
            var hwnd = CreateWindowExW(
                WS.WS_EX_TOOLWINDOW | WS.WS_EX_NOACTIVATE | WS.WS_EX_NOREDIRECTIONBITMAP,
                className,
                title,
                WS.WS_POPUP,
                0,
                ScreenHeight - 72,
                ScreenWidth,
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

    /// <summary>
    /// A Compositor needs a dispatcher queue on the thread that makes it, and xunit's threads have
    /// none — so the body runs on an STA thread of this test's own, as the dock's does.
    /// </summary>
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
