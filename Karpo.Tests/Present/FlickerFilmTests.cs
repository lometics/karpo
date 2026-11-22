// The transitions the commit fence fixes (docs\widgets.md §354), filmed on a hidden desktop as the
// dock's handler runs them (FrameFilm): a menu, a folder and the edit bar come up drawn in their first
// frame; a folder switched for another never leaves neither; a block dropped is never drawn nowhere;
// edit mode comes and goes in one change, the dim never over the bars nor gone while they wear its
// dress; a bar replaced stays until its replacement shows. Animations off (tier 1: every frame the
// before or the end), and on for the switch (tier 2: always one of the two folders in sight).
//
// Each is filmed twice: as Karpo did before the fence (Surfaces.Unfenced) — and the film must see that
// flicker, or it would pass having seen nothing — and with the fence, which must hold. The strips and
// numbers of both go to KARPO_PNG_DIR (film-<name>.png, film-<name>.txt). Edit mode's chrome coming and
// going in one change, every frame, is EditChromeFilmTests' (§359). Not a first show (a menu, a
// folder, the edit bar, the dim): a window shown before its content shows nothing there, which the
// capture gave as black and the film took for a blank frame — on the screen it is clear, the window a
// frame late rather than empty (§355) — so the fence is filmed alone there.

using System.Numerics;
using System.Runtime.InteropServices;
using Karpo.Dock;
using Karpo.Dock.Native;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using Windows.UI.Composition;
using Windows.UI.Composition.Desktop;
using static TerraFX.Interop.Windows.Windows;
using Color = Windows.UI.Color;
using Edge = Karpo.Core.Edge;
using Film = Karpo.Tests.Present.FrameFilm.Film;
using ICompositorDesktopInterop = TerraFX.Interop.WinRT.ICompositorDesktopInterop;
using IDesktopWindowTarget = TerraFX.Interop.WinRT.IDesktopWindowTarget;

namespace Karpo.Tests.Present;

public sealed unsafe class FlickerFilmTests
{
    private const string Owner = "layout:default";

    private const string FilmClass = "karpo.tests.film";

    private static readonly Look Still = AddListPictures.LookFor(dark: true) with { Animations = false };

    private static readonly RECT Display = new() { right = 1280, bottom = 720 };

    [UiFact]
    public void AMenuComesUpDrawnInItsFirstFrame()
    {
        OnHiddenDesktop("menu-first-show", (gfx, instance) =>
        {
            Film Shoot(bool unfenced)
            {
                using var menu = new Flyout(gfx, instance, 0);
                var area = new RECT { left = 340, top = 220, right = 940, bottom = 720 };
                return FrameFilm.Shoot(
                    Named("menu-first-show", unfenced),
                    gfx,
                    area,
                    () => [menu.Hwnd],
                    () => menu.Show(gfx, Still, 1f, Rows(), FlyoutLayout.List, new POINT { x = 640, y = 720 }, Display, Edge.Bottom, null),
                    unfenced);
            }
            Shoot(unfenced: false).Holds();
        });
    }

    [UiFact]
    public void AFolderComesUpDrawnInItsFirstFrame()
    {
        OnHiddenDesktop("folder-first-show", (gfx, instance) =>
        {
            Film Shoot(bool unfenced)
            {
                var island = Readings("glance");
                using var folder = new FolderIsland($"{Owner}/{island.Id}", island, gfx, new Config());
                using var flyout = new FolderFlyout(gfx, instance, 0);
                return FrameFilm.Shoot(
                    Named("folder-first-show", unfenced),
                    gfx,
                    new RECT { left = 240, top = 120, right = 1040, bottom = 720 },
                    () => [flyout.Hwnd],
                    () => flyout.Show(gfx, Still, 1f, new POINT { x = 640, y = 720 }, Display, Edge.Bottom, folder),
                    unfenced);
            }
            Shoot(unfenced: false).Holds();
        });
    }

    [UiFact]
    public void TheEditBarComesUpDrawnInItsFirstFrame()
    {
        OnHiddenDesktop("deck-first-show", (gfx, instance) =>
        {
            Film Shoot(bool unfenced)
            {
                using var deck = new EditDeck(gfx, instance, 0);
                return FrameFilm.Shoot(
                    Named("deck-first-show", unfenced),
                    gfx,
                    new RECT { left = 0, top = 0, right = 1280, bottom = 120 },
                    () => [deck.Hwnd],
                    () => deck.Show(gfx, Still, 1f, new EditDeckRoom(new RectI(0, 0, Display.right, Display.bottom), 0, 48)),
                    unfenced);
            }
            Shoot(unfenced: false).Holds();
        });
    }

    /// <summary>
    /// The dock's switch (Dock.ShowFolder): the open folder hidden, the other one shown, in one handler.
    /// Never a frame with neither: with animations off every frame is one or the other; with them on the
    /// old one fades out over the new one, one of them always in sight. Before the fence, and before the
    /// dock kept a folder's view (§357), the old one was let go of and the new one made: filmed so,
    /// unfenced, it shows the gap. Fenced, the switch is the dock's now: each folder's view kept, drawn
    /// already, shown again — never a window made in the switch, whose first frame a loaded machine
    /// could be late with (a frame of neither at 185 ms under the full suite, before §357).
    /// </summary>
    [UiFact]
    public void AFolderSwitchedForAnotherNeverLeavesNeither()
    {
        OnHiddenDesktop("folder-switch", (gfx, instance) =>
        {
            Film Shoot(bool unfenced, Look look, string name)
            {
                var (one, two) = (Readings("first"), Readings("second", heading: "Later"));
                using var first = new FolderIsland($"{Owner}/{one.Id}", one, gfx, new Config());
                using var second = new FolderIsland($"{Owner}/{two.Id}", two, gfx, new Config());
                var open = new FolderFlyout(gfx, instance, 0);
                var closing = HWND.NULL;
                FolderFlyout? next = null;
                try
                {
                    // §357: the other folder's view kept, as the dock keeps it: opened before, closed.
                    if (!unfenced)
                    {
                        next = new FolderFlyout(gfx, instance, 0);
                        next.Show(gfx, look, 1f, new POINT { x = 900, y = 720 }, Display, Edge.Bottom, second);
                        next.Hide();
                    }
                    open.Show(gfx, look, 1f, new POINT { x = 380, y = 720 }, Display, Edge.Bottom, first);
                    FrameFilm.Settle();
                    var (a, b) = (open.PanelOnScreen, default(RECT));
                    // Its window, which the folder no longer names once it is let go of.
                    closing = open.Hwnd;
                    var film = FrameFilm.Shoot(
                        Named(name, unfenced),
                        gfx,
                        new RECT { left = 0, top = 180, right = 1280, bottom = 720 },
                        () => [closing, next?.Hwnd ?? HWND.NULL],
                        () =>
                        {
                            open.Hide();
                            if (unfenced)
                            {
                                open.Dispose();
                                next = new FolderFlyout(gfx, instance, 0);
                            }
                            next!.Show(gfx, look, 1f, new POINT { x = 900, y = 720 }, Display, Edge.Bottom, second);
                        },
                        unfenced);
                    b = next!.PanelOnScreen;
                    film.Places = [Inset(a), Inset(b)];
                    return film;
                }
                finally
                {
                    open.Dispose();
                    next?.Dispose();
                    FrameFilm.Settle();
                }
            }
            var old = Shoot(unfenced: true, Still, "folder-switch");
            Assert.NotEmpty(old.Gaps(0.6, old.Places));
            var still = Shoot(unfenced: false, Still, "folder-switch");
            still.Holds();
            still.StaysInSight(0.6, still.Places);
            var moving = Shoot(unfenced: false, Still with { Animations = true }, "folder-switch-animated");
            moving.StaysInSight(0.6, moving.Places);
        });
    }

    /// <summary>
    /// A block dropped (Dock.EndDrag): the carry window hidden and the bar drawn with the block where it
    /// landed, in one handler. The block is in sight in every frame: in the carry window until the bar
    /// draws it, never in neither.
    /// </summary>
    [UiFact]
    public void ABlockDroppedIsNeverDrawnNowhere()
    {
        OnHiddenDesktop("drop", (gfx, instance) =>
        {
            Film Shoot(bool unfenced)
            {
                // Black, as nothing drawn: the block is what is looked for where it lands.
                using var bar = new Plain(gfx, instance, new RECT { left = 0, top = 648, right = 1280, bottom = 720 }, Color.FromArgb(255, 0, 0, 0));
                // The lifted block, drawn where it was lifted from: outside the bar's window, as a scene's
                // lifted block is out of sight, the carry window drawing it from there.
                var c = gfx.Compositor;
                var lifted = c.CreateSpriteVisual();
                lifted.Brush = c.CreateColorBrush(Color.FromArgb(255, 0xE0, 0xA0, 0x20));
                lifted.Size = new Vector2(96, 48);
                lifted.Offset = new Vector3(-400, 0, 0);
                bar.Root.Children.InsertAtTop(lifted);
                // Where it lands on the bar: drawn by the bar once dropped.
                var landed = c.CreateSpriteVisual();
                landed.Brush = c.CreateColorBrush(Color.FromArgb(255, 0xE0, 0xA0, 0x20));
                landed.Size = new Vector2(96, 48);
                landed.Offset = new Vector3(592, 12, 0);
                landed.IsVisible = false;
                bar.Root.Children.InsertAtTop(landed);
                using var carry = new EditCarry(gfx, instance);
                // Held just over where it lands.
                carry.Show(lifted, new RectF(0, 0, 96, 48), new POINT { x = 600, y = 600 });
                var carried = new RECT { left = 600, top = 600, right = 696, bottom = 648 };
                var spot = new RECT { left = 592, top = 660, right = 688, bottom = 708 };
                var film = FrameFilm.Shoot(
                    Named("drop", unfenced),
                    gfx,
                    new RECT { left = 400, top = 560, right = 900, bottom = 720 },
                    () => [bar.Hwnd, carry.Hwnd],
                    () =>
                    {
                        carry.Hide();
                        landed.IsVisible = true;
                    },
                    unfenced);
                film.Places = [Inset(carried), Inset(spot)];
                return film;
            }
            var old = Shoot(unfenced: true);
            Assert.NotEmpty(old.Gaps(0.6, old.Places));
            var now = Shoot(unfenced: false);
            now.Holds();
            now.StaysInSight(0.6, now.Places);
        });
    }

    /// <summary>
    /// A bar's window replaced by an arrange (another edge, type or way of showing; Dock.Arrange): the
    /// new one made and shown, the old one let go of, in one handler. The bar is in sight in every frame.
    /// </summary>
    [UiFact]
    public void ABarReplacedStaysUntilItsReplacementShows()
    {
        OnHiddenDesktop("bar-replaced", (gfx, instance) =>
        {
            var strip = new RECT { left = 0, top = 648, right = 1280, bottom = 720 };
            Film Shoot(bool unfenced)
            {
                var old = new Plain(gfx, instance, strip, Color.FromArgb(255, 0x20, 0x40, 0x80));
                Plain? made = null;
                try
                {
                    var film = FrameFilm.Shoot(
                        Named("bar-replaced", unfenced),
                        gfx,
                        new RECT { left = 0, top = 600, right = 1280, bottom = 720 },
                        () => [old.Hwnd, made?.Hwnd ?? HWND.NULL],
                        () =>
                        {
                            made = new Plain(gfx, instance, strip, Color.FromArgb(255, 0x30, 0x90, 0x50), shown: false);
                            Surfaces.Show(made.Hwnd);
                            old.Dispose();
                        },
                        unfenced);
                    film.Places = [Inset(strip)];
                    return film;
                }
                finally
                {
                    old.Dispose();
                    made?.Dispose();
                    FrameFilm.Settle();
                }
            }
            var before = Shoot(unfenced: true);
            Assert.NotEmpty(before.Between());
            var now = Shoot(unfenced: false);
            now.Holds();
            now.StaysInSight(0.9, now.Places);
        });
    }

    /// <summary>The fence is the compositor's commit (RequestCommitAsync), answered on this thread; the time limit never had to step in.</summary>
    [UiFact]
    public void TheFenceLandsByItsCommit()
    {
        OnHiddenDesktop("fence", (gfx, instance) =>
        {
            using var window = new Plain(gfx, instance, new RECT { left = 0, top = 0, right = 200, bottom = 100 }, Color.FromArgb(255, 0x80, 0x20, 0x20), shown: false);
            var before = Surfaces.Fences;
            using (Surfaces.Begin(gfx.Compositor))
            {
                Surfaces.Show(window.Hwnd);
                Assert.True(Surfaces.Visible(window.Hwnd));
            }
            Assert.False(IsWindowVisible(window.Hwnd), "shown before its commit");
            var until = Environment.TickCount64 + 2000;
            while (!Surfaces.Settled && Environment.TickCount64 < until)
            {
                FrameFilm.Pump();
                Thread.Sleep(1);
            }
            Assert.True(IsWindowVisible(window.Hwnd));
            var after = Surfaces.Fences;
            Assert.Equal("commit", after.Way);
            Assert.Equal(before.Committed + 1, after.Committed);
            Assert.Equal(before.TimedOut, after.TimedOut);
            Assert.True(after.LastMs < Surfaces.FenceMs, $"the commit took {after.LastMs} ms");
        });
    }

    /// <summary>
    /// §359: the plan goes to the screen in one batch (DeferWindowPos), by strata whatever order the frame
    /// asked in — a menu over a bar over a dim — and in a stratum the one shown last on top.
    /// </summary>
    [UiFact]
    public void ThePlanGoesInOneBatchByStrata()
    {
        OnHiddenDesktop("stack", (gfx, instance) =>
        {
            var at = new RECT { left = 0, top = 0, right = 100, bottom = 100 };
            using var dim = new Plain(gfx, instance, at, Color.FromArgb(255, 255, 0, 0), shown: false);
            using var bar = new Plain(gfx, instance, at, Color.FromArgb(255, 0, 255, 0), shown: false);
            using var menu = new Plain(gfx, instance, at, Color.FromArgb(255, 0, 0, 255), shown: false);
            using var submenu = new Plain(gfx, instance, at, Color.FromArgb(255, 0, 255, 255), shown: false);
            Surfaces.Enlist(dim.Hwnd, Stratum.Dim);
            Surfaces.Enlist(bar.Hwnd, Stratum.Bar);
            Surfaces.Enlist(menu.Hwnd, Stratum.Menu);
            Surfaces.Enlist(submenu.Hwnd, Stratum.Menu);
            using (Surfaces.Begin(gfx.Compositor))
            {
                Surfaces.Show(menu.Hwnd);
                Surfaces.Show(submenu.Hwnd);
                Surfaces.Show(bar.Hwnd);
                Surfaces.Show(dim.Hwnd);
                // Asked, not yet done: the frame's plan answers as it will leave the order.
                Assert.Equal(submenu.Hwnd, Surfaces.PlannedAfter(menu.Hwnd));
                Assert.Equal(bar.Hwnd, Surfaces.PlannedAfter(dim.Hwnd));
            }
            var waited = System.Diagnostics.Stopwatch.StartNew();
            FrameFilm.Settle();
            Assert.True(Surfaces.Settled, $"not settled after {waited.ElapsedMilliseconds} ms: {Surfaces.Fences}");
            Assert.True(Z.Above(submenu.Hwnd, menu.Hwnd), "the submenu under its menu");
            Assert.True(Z.Above(menu.Hwnd, bar.Hwnd), "the menu under the bar");
            Assert.True(Z.Above(bar.Hwnd, dim.Hwnd), "the bar under the dim");
            Assert.Equal(submenu.Hwnd, GetTopWindow(HWND.NULL));
            // The menu brought to the front of its stratum: over its submenu, still over the bar.
            Surfaces.Front(menu.Hwnd);
            Assert.True(Z.Above(menu.Hwnd, submenu.Hwnd) && Z.Above(submenu.Hwnd, bar.Hwnd));
        });
    }

    // --- Helpers ---------------------------------------------------------------------------------

    private static string Named(string name, bool unfenced) => unfenced ? $"{name}-unfenced" : name;

    /// <summary><paramref name="rect"/> a few px in on every side: the part of a panel that is its own, clear of its rim's antialiasing.</summary>
    private static RECT Inset(RECT rect) => new() { left = rect.left + 6, top = rect.top + 6, right = rect.right - 6, bottom = rect.bottom - 6 };

    private static List<FlyoutItem> Rows() =>
    [
        new(1, "Open", new FlyoutIcon.Glyph("")),
        new(2, "Pin to taskbar", new FlyoutIcon.Glyph("")),
        new(0, "", Separator: true),
        new(3, "Properties"),
        new(4, "Close window", new FlyoutIcon.Glyph("")),
    ];

    /// <summary>A list folder of widgets that only read — a clock, the battery, a heading — fetching nothing, launching nothing.</summary>
    internal static BarItem Readings(string id, string heading = "Notes")
    {
        var folder = new FolderOptions();
        var island = BarKind.FolderList.New();
        island.Id = id;
        island.Folder = folder;
        foreach (var (line, kind) in new[] { ("clock", ListKind.Clock), ("battery", ListKind.Battery) })
        {
            var made = kind.New();
            made.Id = $"{id}-{line}";
            folder.Put(made);
        }
        var title = ListKind.Heading.New();
        title.Id = $"{id}-heading";
        title.Heading = new HeadingOptions { Text = heading };
        folder.Put(title);
        return island;
    }

    /// <summary>On a hidden desktop of its own, the windows' classes there: the popups', and the plain one of the films' own windows.</summary>
    internal static void OnHiddenDesktop(string what, Action<Gfx, HINSTANCE> body) =>
        WindowShots.OnHiddenDesktop($"film-{what}", (gfx, instance) =>
        {
            Karpo.Tests.Signals.PreviewToolbarTests.Register(Preview.ClassName, instance);
            Karpo.Tests.Signals.PreviewToolbarTests.Register(FilmClass, instance);
            body(gfx, instance);
        });

    /// <summary>A window of one colour, as a bar stands for a film: topmost, never activated, composition only.</summary>
    internal sealed class Plain : IDisposable
    {
        private readonly DesktopWindowTarget target;
        private readonly CompositionColorBrush brush;
        private bool gone;

        public Plain(Gfx gfx, HINSTANCE instance, RECT at, Color color, bool shown = true)
        {
            fixed (char* name = FilmClass)
            fixed (char* title = "Karpo film")
            {
                Hwnd = CreateWindowExW(
                    WS.WS_EX_TOOLWINDOW | WS.WS_EX_TOPMOST | WS.WS_EX_NOACTIVATE | WS.WS_EX_NOREDIRECTIONBITMAP,
                    name,
                    title,
                    WS.WS_POPUP,
                    at.left,
                    at.top,
                    at.right - at.left,
                    at.bottom - at.top,
                    HWND.NULL,
                    HMENU.NULL,
                    instance,
                    null);
            }
            Assert.True(Hwnd != HWND.NULL, $"CreateWindowExW failed: {Marshal.GetLastSystemError()}");
            var c = gfx.Compositor;
            var desktop = ComUtil.Query<ICompositorDesktopInterop>(c);
            try
            {
                IDesktopWindowTarget* raw = null;
                ComUtil.Check(desktop->CreateDesktopWindowTarget(Hwnd, BOOL.TRUE, &raw));
                target = ComUtil.Adopt<DesktopWindowTarget>(raw);
            }
            finally
            {
                desktop->Release();
            }
            Root = c.CreateContainerVisual();
            Root.Size = new Vector2(at.right - at.left, at.bottom - at.top);
            brush = c.CreateColorBrush(color);
            var fill = c.CreateSpriteVisual();
            fill.Brush = brush;
            fill.Size = Root.Size;
            Root.Children.InsertAtTop(fill);
            target.Root = Root;
            if (shown)
            {
                // Outside a frame: at once, drawn by the time anything is filmed (FrameFilm.Settle).
                Surfaces.Show(Hwnd);
                FrameFilm.Settle();
            }
        }

        public HWND Hwnd { get; }

        public ContainerVisual Root { get; }

        public void Paint(Color color) => brush.Color = color;

        public void Dispose()
        {
            if (gone)
            {
                return;
            }
            gone = true;
            Surfaces.Destroy(Hwnd, () =>
            {
                target.Root = null;
                target.Dispose();
                Root.Dispose();
            });
        }
    }
}
