// A bar's tree kept, and its window standing still (docs\widgets.md §358, the flicker design's phase 4):
// laid out again with nothing changed a bar makes nothing, draws nothing and holds nothing more; a widget
// added makes its own and moves the rest, the "+" with them, gliding from where each stood; edit mode's
// "+" come at once with motion off, §612 fade in and out with its dress with it on; the highlight and the tooltip stay where they are through a rebuild;
// a colour chosen is set in place; another height or another type of bar is the scene laid out again in
// a window that stands still, and another edge a new window shown with the old one gone in the same plan.
// Tier 3 (what is made, counted: Scene.Churn, Scene.Glides, Scene.FadesIn), tier 1 and 2 films (FrameFilm),
// against the old way where there is one to film: a window sized to its bar at every layout, unfenced.

using System.Numerics;
using System.Runtime.InteropServices;
using Karpo.Dock;
using Karpo.Dock.Native;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using Windows.UI.Composition.Desktop;
using static TerraFX.Interop.Windows.Windows;
using Color = Windows.UI.Color;
using Edge = Karpo.Core.Edge;
using Film = Karpo.Tests.Present.FrameFilm.Film;
using ICompositorDesktopInterop = TerraFX.Interop.WinRT.ICompositorDesktopInterop;
using IDesktopWindowTarget = TerraFX.Interop.WinRT.IDesktopWindowTarget;

namespace Karpo.Tests.Present;

public sealed unsafe class RetainedBarTests
{
    private const string BarClass = "karpo.tests.retained-bar";

    private static readonly RECT Display = new() { right = 1280, bottom = 720 };

    internal static readonly Look Still = AddListPictures.LookFor(dark: true) with { Animations = false };

    internal static readonly Config Islands = new() { Height = 48, Style = BarStyle.Islands };

    // --- Tier 3: what an update makes ----------------------------------------------------------------

    /// <summary>
    /// Laid out again with nothing changed — a new layout and new content of the same, as the dock lays a
    /// bar out at every rebuild —, at rest and arranged, still and animated, a bar and a dock: nothing made,
    /// nothing drawn, nothing more held.
    /// </summary>
    [UiFact]
    public void AnUpdateWithNothingChangedMakesNothing()
    {
        OnHiddenDesktop("retained-bar-same", (gfx, instance) =>
        {
            foreach (var config in new[] { Islands, new Config { Height = 48, Style = BarStyle.Floating } })
            {
                foreach (var editing in new[] { false, true })
                {
                    foreach (var look in new[] { Still, Still with { Animations = true } })
                    {
                        using var bar = new Bar(gfx, instance);
                        bar.Lay(Parts(), Things(3), look, config, editing);
                        var churn = bar.Scene.Churn;
                        Assert.True(churn.Made > 10, $"only {churn.Made} elements made");
                        bar.Lay(Parts(), Things(3), look, config, editing);
                        bar.Lay(Parts(), Things(3), look, config, editing);
                        Assert.Equal(churn, bar.Scene.Churn);
                    }
                }
            }
        });
    }

    /// <summary>
    /// A widget added before the apps, arranged: its own made, and nothing else — every other part's
    /// card, every button, every tray icon and every "+" the very element it was, the ones after it
    /// moved and gliding there from where they stood. The cards a colour of their own: the Appearance's own
    /// tokens set none, and a bar with no card colour draws no card at all (Scene.ComposeCards).
    /// </summary>
    [UiFact]
    public void AWidgetAddedMakesItsOwnAndMovesTheRest()
    {
        OnHiddenDesktop("retained-bar-added", (gfx, instance) =>
        {
            using var bar = new Bar(gfx, instance);
            var look = Still with { Animations = true, Colors = new Palette(Card: new Argb(0x20, 0xFF, 0xFF, 0xFF)) };
            bar.Lay(Parts(), Things(3), look, Islands, editing: true);
            var adds = bar.Scene.AddsShown();
            Assert.NotEmpty(adds);
            var made = bar.Scene.Churn.Made;
            var before = bar.Layout;

            var added = Parts(extra: Spec("snip", BarKind.SnippingTool));
            bar.Lay(added, Things(3), look, Islands, editing: true);
            var after = bar.Layout;
            Assert.True(after.PartIndex(Key("snip")) >= 0);
            var made2 = bar.Scene.Churn.Made - made;
            // Its card, its button and its edit dress, and nothing else.
            Assert.InRange(made2, 1, 9);
            Assert.True(bar.Scene.MadeLast($"windows:{Key("snip")}"));
            // §602: the apps' buttons each on a card of its own, the first keyed as the part.
            foreach (var key in new[] { $"card:{Key("apps")}", $"card:{Key("apps")}#1", $"card:{Key("tray")}", "start", $"tray:{Key("tray")}|{TrayIcons[0].Key}" })
            {
                Assert.NotNull(bar.Scene.Registered(key));
                Assert.False(bar.Scene.MadeLast(key), $"{key} made again");
            }
            // Every "+" kept; those that moved glide from where they stood, as the blocks beside them do.
            var addKeys = after.Adds().Select(add => $"add:{add.Section}:{add.End}").ToList();
            Assert.All(addKeys, key => Assert.False(bar.Scene.MadeLast(key), $"{key} made again"));
            var moved = after.Adds().Where(add => before.Adds().FirstOrDefault(old => old.Section == add.Section && old.End == add.End).Rect.X != add.Rect.X).ToList();
            Assert.NotEmpty(moved);
            foreach (var add in moved)
            {
                var was = before.Adds().First(old => old.Section == add.Section && old.End == add.End).Rect;
                var glide = Assert.Contains($"add:{add.Section}:{add.End}", bar.Scene.Glides);
                Assert.Equal(new Vector3(was.X, was.Y, 0f), glide.From);
                Assert.Equal(new Vector3(add.Rect.X, add.Rect.Y, 0f), glide.To);
            }
            // The apps' buttons moved as much as the layout moved them, gliding with the "+": one tree, gliding as one.
            var tile = Assert.Contains($@"tile:{Key("apps")}:C:\Karpo\apps\app0.exe#0", bar.Scene.Glides);
            Assert.Equal(after.IconBox(Hit.Tile(0), after.Icon).X - before.IconBox(Hit.Tile(0), before.Icon).X, tile.To.X - tile.From.X, 0.5f);
            Assert.True(tile.To.X > tile.From.X);
        });
    }

    /// <summary>
    /// The bar's card colour (§311, §405; §567 the Appearance's, every widget's alike): the bar laid out again in another one
    /// makes nothing and draws nothing; the cards' brushes take the colour where they stand.
    /// </summary>
    [UiFact]
    public void AColourChosenIsSetInPlace()
    {
        OnHiddenDesktop("retained-bar-colour", (gfx, instance) =>
        {
            using var bar = new Bar(gfx, instance);
            var look = Still with { Colors = new Palette(Card: new Argb(0x20, 0xFF, 0xFF, 0xFF)) };
            bar.Lay(Parts(), Things(3), look, Islands);
            var churn = bar.Scene.Churn;
            var swatches = bar.Scene.SwatchColors;
            bar.Lay(Parts(), Things(3), look with { Colors = new Palette(Card: new Argb(0x40, 0xFF, 0xFF, 0xFF)) }, Islands);
            Assert.Equal(churn, bar.Scene.Churn);
            Assert.NotEqual(swatches, bar.Scene.SwatchColors);
            // §546: a see-through card at its opacity, transparency effects or none.
            Assert.Contains(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF), bar.Scene.SwatchColors);
        });
    }

    // --- Films ------------------------------------------------------------------------------------------

    /// <summary>
    /// The pointer on an app's button, its tooltip up, and the bar laid out again as the dock does at every
    /// rebuild (the highlight and the tooltip asked for again): every frame is the before, and neither fades
    /// in again — nor when a widget added before the button moves it, the highlight and the tooltip going
    /// with it. A scene made anew per rebuild, as before §358, faded both in again every time.
    /// </summary>
    [UiFact]
    public void TheHighlightAndTheTooltipStayThroughARebuild()
    {
        OnHiddenDesktop("retained-bar-hover", (gfx, instance) =>
        {
            var look = Still with { Animations = true };
            using var bar = new Bar(gfx, instance);
            bar.Lay(Parts(), Things(3), look, Islands);
            Hit Over()
            {
                var item = bar.Layout.ItemRect(Hit.Tile(1));
                return Assert.NotNull(bar.Layout.HitTest(item.Center.X, item.Center.Y));
            }
            bar.Scene.Highlight(Over(), pressed: false);
            bar.Scene.Tooltip(gfx, "App 1", Over());
            var fades = bar.Scene.FadesIn;
            var film = FrameFilm.Shoot("bar-hover-rebuilt", gfx, bar.Frame, () => [bar.Hwnd], () =>
            {
                bar.Lay(Parts(), Things(3), look, Islands);
                bar.Scene.Highlight(Over(), pressed: false);
                bar.Scene.Tooltip(gfx, "App 1", Over());
            }, filmMs: 250);
            var moved = film.Frames.Where(frame => film.Differs(frame, film.Before) > 0.0005).Select(frame => $"{frame.At:F0} ms: {film.Differs(frame, film.Before):P2}").ToList();
            Assert.True(moved.Count == 0, $"the hover changed in a rebuild: {string.Join(", ", moved)}");
            Assert.Equal(fades, bar.Scene.FadesIn);

            // The button moved by a widget added before it: the highlight and the tooltip go with it, not faded in again.
            bar.Lay(Parts(extra: Spec("snip", BarKind.SnippingTool)), Things(3), look, Islands);
            bar.Scene.Highlight(Over(), pressed: false);
            bar.Scene.Tooltip(gfx, "App 1", Over());
            Assert.Equal(fades, bar.Scene.FadesIn);
            // Taken off it, the highlight fades out and the next one in, as ever.
            bar.Scene.Highlight(null, pressed: false);
            bar.Scene.Highlight(Over(), pressed: false);
            Assert.Equal(fades + 1, bar.Scene.FadesIn);
        });
    }

    /// <summary>
    /// §612 (the person, 2026-11-09: "the transition into edit mode and out of edit mode should also be animated if enabled in
    /// settings"): edit mode on and off, the "+" with the rest of its dress. Animations off: every "+" in every frame as it was
    /// before (none there) or as it ends — at once, never a frame between (§354: "the add buttons flicker"). Animations on: each
    /// fades in and out on the straight way, never back, never another picture (a flicker) — and frames between there are.
    /// </summary>
    [UiFact]
    public void ThePlusesComeAndGoWithEditModeByTheMotion()
    {
        OnHiddenDesktop("retained-bar-adds", (gfx, instance) =>
        {
            foreach (var animated in new[] { false, true })
            {
                var look = Still with { Animations = animated };
                using var bar = new Bar(gfx, instance);
                bar.Lay(Parts(), Things(3), look, Islands);
                var on = FrameFilm.Shoot($"bar-adds-on{(animated ? "-animated" : "")}", gfx, bar.Frame, () => [bar.Hwnd],
                    () => bar.Lay(Parts(), Things(3), look, Islands, editing: true), filmMs: 300);
                // Inside each round button, where nothing but it is drawn: an empty section's stands on its empty block, which fades in.
                var pluses = bar.Layout.Adds().Where(add => add.End != AddEnd.Alone).Select(add => bar.OnScreen(add.Rect, inset: (int)(add.Rect.Width * 0.3f))).ToList();
                Assert.NotEmpty(pluses);
                var off = FrameFilm.Shoot($"bar-adds-off{(animated ? "-animated" : "")}", gfx, bar.Frame, () => [bar.Hwnd],
                    () => bar.Lay(Parts(), Things(3), look, Islands), filmMs: 300);
                foreach (var film in new[] { on, off })
                {
                    var wrong = new List<string>();
                    var between = 0;
                    foreach (var plus in pluses)
                    {
                        if (animated)
                        {
                            wrong.AddRange(film.OffTheWay(plus, out _));
                        }
                        foreach (var (frame, index) in film.Frames.Select((frame, index) => (frame, index)))
                        {
                            var (before, end) = (Off(film, frame, film.Before, plus), Off(film, frame, film.End, plus));
                            if (before > 0.05 && end > 0.05)
                            {
                                between++;
                                if (!animated)
                                {
                                    wrong.Add($"f{index} at {frame.At:F0} ms, the + at {plus.left}: {before:P0} off the before, {end:P0} off the end");
                                }
                            }
                        }
                    }
                    Assert.True(wrong.Count == 0, $"{film.Name}:{Environment.NewLine}{string.Join(Environment.NewLine, wrong)}");
                    Assert.True(!animated || between > 0, $"{film.Name}: the + came at once, animations on");
                    if (animated)
                    {
                        film.Save();
                    }
                }
            }
        });
    }

    /// <summary>
    /// §612: edit mode's dress taken off — edit mode over, or the Appearance's preview —, animations on: the "+" are no longer
    /// the bar's (none shown, none hit), their marks fade out where they stood and are let go of once they have; coming in, the
    /// marks and the "+" fade in. Animations off: nothing fades either way, everything let go of at once.
    /// </summary>
    [UiFact]
    public void TheDressFadesInAndOutByTheMotion()
    {
        OnHiddenDesktop("retained-bar-dress", (gfx, instance) =>
        {
            foreach (var animated in new[] { false, true })
            {
                var look = Still with { Animations = animated };
                using var bar = new Bar(gfx, instance);
                bar.Lay(Parts(), Things(3), look, Islands);
                var fades = bar.Scene.FadesIn;
                bar.Lay(Parts(), Things(3), look, Islands, editing: true);
                Assert.Equal(animated, bar.Scene.FadesIn - fades >= 3);
                Assert.NotEmpty(bar.Scene.AddsShown());
                bar.Lay(Parts(), Things(3), look, Islands);
                Assert.Empty(bar.Scene.AddsShown());
                Assert.Empty(bar.Layout.Adds());
                Assert.Equal(animated, bar.Scene.Fading > 0);
                // Once faded, let go of at the next update; the bar holds what it held before edit mode.
                Thread.Sleep(400);
                bar.Lay(Parts(), Things(3), look, Islands);
                Assert.Equal(0, bar.Scene.Fading);
            }
        });
    }

    /// <summary>
    /// Another height (§358: the window stands still at its edge's most thickness): tier 1, animations off,
    /// every frame the before or the end. The old way — the window sized to the bar at every layout, the
    /// scene laid out in it — showed the old bar in the new window for a frame.
    /// </summary>
    [UiFact]
    public void AnotherHeightNeverShowsTheOldBarInTheNewWindow()
    {
        OnHiddenDesktop("retained-bar-height", (gfx, instance) =>
        {
            var tall = new Config { Height = 64, Style = BarStyle.Islands };
            Film Shoot(bool old)
            {
                using var bar = new Bar(gfx, instance, sized: old);
                bar.Lay(Parts(), Things(3), Still, Islands);
                FrameFilm.Settle();
                return FrameFilm.Shoot(old ? "bar-height-old" : "bar-height", gfx, Layout.FrameFor(96, Display, Edge.Bottom, 1f), () => [bar.Hwnd],
                    () => bar.Lay(Parts(), Things(3), Still, tall), unfenced: old);
            }
            var then = Shoot(old: true);
            Assert.NotEmpty(then.Between());
            Shoot(old: false).Holds();
        });
    }

    /// <summary>
    /// Another type of bar on the same edge — the bar a dock — keeps its window (Dock.Arrange keeps a window
    /// whose edge and row stay) and lays the scene out again in it: every frame the before or the end.
    /// </summary>
    [UiFact]
    public void AnotherTypeIsTheSceneLaidOutAgain()
    {
        OnHiddenDesktop("retained-bar-type", (gfx, instance) =>
        {
            using var bar = new Bar(gfx, instance);
            bar.Lay(Parts(), Things(3), Still, Islands);
            var made = bar.Scene.Churn.Made;
            var film = FrameFilm.Shoot("bar-type", gfx, bar.Frame, () => [bar.Hwnd],
                () => bar.Lay(Parts(), Things(3), Still, new Config { Height = 48, Style = BarStyle.Floating }));
            film.Holds();
            // The buttons and the tray kept; the panel, the docks' and the cards' looks made for the new type.
            Assert.False(bar.Scene.MadeLast("start"));
            Assert.False(bar.Scene.MadeLast($"tray:{Key("tray")}|{TrayIcons[1].Key}"));
            Assert.True(bar.Scene.Churn.Made - made < 12, $"{bar.Scene.Churn.Made - made} made");
        });
    }

    /// <summary>
    /// Another edge (Dock.Arrange: the new edge's window made, laid out, shown, the old one let go of, in one
    /// handler): every frame the before or the end, and one of the two bars in sight in each. Unfenced, as
    /// before §354, there was a frame with neither.
    /// </summary>
    [UiFact]
    public void AnotherEdgeHandsTheBarOverInOneChange()
    {
        OnHiddenDesktop("retained-bar-edge", (gfx, instance) =>
        {
            Film Shoot(bool unfenced)
            {
                var bottom = new Bar(gfx, instance);
                bottom.Lay(Parts(), Things(3), Still, Islands);
                var bottomStrip = bottom.OnScreen(bottom.Layout.PanelRect, inset: 6);
                Bar? left = null;
                try
                {
                    var film = FrameFilm.Shoot(unfenced ? "bar-edge-unfenced" : "bar-edge", gfx, Display, () => [bottom.Hwnd, left?.Hwnd ?? HWND.NULL],
                        () =>
                        {
                            left = new Bar(gfx, instance, Edge.Left, shown: false);
                            left.Lay(Parts(), Things(3), Still, Islands);
                            left.Show();
                            bottom.Dispose();
                        },
                        unfenced);
                    film.Places = [bottomStrip, left!.OnScreen(left.Layout.PanelRect, inset: 6)];
                    return film;
                }
                finally
                {
                    bottom.Dispose();
                    left?.Dispose();
                    FrameFilm.Settle();
                }
            }
            var then = Shoot(unfenced: true);
            Assert.NotEmpty(then.Gaps(0.6, then.Places));
            var now = Shoot(unfenced: false);
            now.Holds();
            now.StaysInSight(0.6, now.Places);
        });
    }

    /// <summary>
    /// §359: a bar's tooltip stands beside the bar, past its window's region — the bar's panel, all the
    /// pointer takes and all DWM shows of the window (§355's spike; FrameFilm cuts each window so). Drawn in
    /// the bar's own window, as §358 left it, it was cut off: nothing of it on the screen. In a window of its
    /// own (BarTip) it shows whole, and comes, moves to another button and goes in one change: every frame
    /// the before or the end.
    /// </summary>
    [UiFact]
    public void TheTooltipIsNotCutByTheBarsRegion()
    {
        OnHiddenDesktop("retained-bar-tip", (gfx, instance) =>
        {
            foreach (var own in new[] { false, true })
            {
                using var bar = new Bar(gfx, instance, tips: own);
                bar.Lay(Parts(), Things(3), Still, Islands);
                bar.Clip();
                FrameFilm.Settle();
                Hit Over(int tile)
                {
                    var item = bar.Layout.ItemRect(Hit.Tile(tile));
                    return Assert.NotNull(bar.Layout.HitTest(item.Center.X, item.Center.Y));
                }
                HWND[] Windows() => [bar.Hwnd, bar.TipHwnd];
                // Round the two buttons and the room over them, so a tip is a share of the film it can be told by.
                var (one, two) = (bar.OnScreen(bar.Layout.ItemRect(Hit.Tile(1))), bar.OnScreen(bar.Layout.ItemRect(Hit.Tile(2))));
                var area = new RECT { left = one.left - 80, top = one.top - 80, right = two.right + 80, bottom = one.bottom };
                var show = FrameFilm.Shoot(own ? "bar-tip-show" : "bar-tip-cut", gfx, area, Windows, () => bar.Scene.Tooltip(gfx, "App 1", Over(1)));
                var at = bar.OnScreen(Assert.NotNull(bar.Scene.TipRect), inset: 3);
                var shown = show.Shows(show.End, at);
                if (!own)
                {
                    Assert.True(shown < 0.05, $"drawn in the bar's window, {shown:P0} of the tooltip showed past its region");
                    continue;
                }
                Assert.True(shown > 0.6, $"in its own window, only {shown:P0} of the tooltip showed");
                show.Holds(everyFrame: true);
                var move = FrameFilm.Shoot("bar-tip-move", gfx, area, Windows, () => bar.Scene.Tooltip(gfx, "App 2", Over(2)));
                move.Holds(everyFrame: true);
                var hide = FrameFilm.Shoot("bar-tip-hide", gfx, area, Windows, () => bar.Scene.Tooltip(gfx, null, null));
                hide.Holds(everyFrame: true);
                Assert.Null(bar.Scene.TipRect);
            }
        });
    }

    // --- The bar -----------------------------------------------------------------------------------------

    private static string Key(string id) => Config.IslandKey(null, id);

    private static PartSpec Spec(string id, BarKind kind, BarSection section = BarSection.Start) => new(Key(id), kind) { Section = section };

    /// <summary>Start, Task view, <paramref name="extra"/> where given, the apps; the tray and Show desktop at the end.</summary>
    internal static List<PartSpec> Parts(PartSpec? extra = null) =>
    [
        Spec("start", BarKind.Start),
        Spec("task", BarKind.TaskView),
        .. extra is { } more ? [more] : Array.Empty<PartSpec>(),
        Spec("apps", BarKind.Apps),
        Spec("tray", BarKind.Tray, BarSection.End),
        Spec("desk", BarKind.ShowDesktop, BarSection.End),
    ];

    /// <summary>Three icons for the tray, drawn once: a live app's sends the same picture until it has another.</summary>
    private static readonly IReadOnlyList<TrayIcon> TrayIcons =
    [
        .. Enumerable.Range(0, 3).Select(index => new TrayIcon(
            $"retained/{index}", HWND.NULL, (uint)index, Guid.Empty, 0x8000, 4, Solid(16, index), $"App {index}", Hidden: false, Identity: $"retained{index}|0")),
    ];

    private static Pixels Solid(int side, int index)
    {
        var data = new byte[side * side * 4];
        for (var at = 0; at < data.Length; at += 4)
        {
            (data[at], data[at + 1], data[at + 2], data[at + 3]) = ((byte)(40 + (index * 60)), (byte)(200 - (index * 50)), (byte)(90 + (index * 40)), 255);
        }
        return new Pixels { Data = data, Width = side, Height = side };
    }

    /// <summary><paramref name="apps"/> open apps, their pictures glyphs (nothing read from the shell), the second focused, and the tray's icons.</summary>
    internal static Content Things(int apps) => new()
    {
        Tiles =
        [
            .. Enumerable.Range(0, apps).Select(index => (new Item
            {
                Target = $@"C:\Karpo\apps\app{index}.exe",
                Name = $"App {index}",
                Kind = ItemKind.Shell,
                Keys = [],
                Picture = index % 2 == 0 ? "\uE737" : "\uE8A5",
            }, new Mark(1, Active: index == 1, Attention: false))),
        ],
        Trays = new Dictionary<string, BarTray> { [Key("tray")] = new BarTray(TrayIcons, Overflow: true) },
    };

    /// <summary>The share of <paramref name="rect"/> (screen px) where <paramref name="shot"/> and <paramref name="other"/> differ.</summary>
    private static double Off(Film film, FrameFilm.Shot shot, FrameFilm.Shot other, RECT rect)
    {
        var (differ, all) = (0, 0);
        for (var y = Math.Max(rect.top, film.Area.top); y < Math.Min(rect.bottom, film.Area.bottom); y++)
        {
            for (var x = Math.Max(rect.left, film.Area.left); x < Math.Min(rect.right, film.Area.right); x++)
            {
                var at = (((y - film.Area.top) * film.Width) + x - film.Area.left) * 4;
                all++;
                if (Math.Abs(shot.Pixels[at] - other.Pixels[at]) > 24 || Math.Abs(shot.Pixels[at + 1] - other.Pixels[at + 1]) > 24 || Math.Abs(shot.Pixels[at + 2] - other.Pixels[at + 2]) > 24)
                {
                    differ++;
                }
            }
        }
        return all == 0 ? 0 : differ / (double)all;
    }

    /// <summary>
    /// A bar on a window of its own on the hidden desktop, as the dock stands one: its window its edge's
    /// frame (<see cref="Layout.FrameFor"/>), the scene laid out in it by <see cref="Lay"/>, a new layout of
    /// the same each time. <c>sized</c>: the old way, the window sized to the bar at every layout.
    /// </summary>
    internal sealed class Bar : IDisposable
    {
        private readonly Gfx gfx;
        private readonly DesktopWindowTarget target;

        /// <summary>What the target shows, the scene's root in it — two while a morph runs (§637), as the dock's bars have it.</summary>
        private readonly Windows.UI.Composition.ContainerVisual host;

        /// <summary>§359: the tooltip's own window, as the dock's bars have one; null: drawn in the bar's window.</summary>
        private readonly BarTip? tip;
        private readonly Icons icons = new();
        private readonly Edge edge;
        private readonly bool sized;
        private readonly uint dpi;
        private readonly RECT display;
        private bool gone;

        /// <summary>On <paramref name="on"/> (screen px; the made-up display where none is given) at <paramref name="dpi"/>.</summary>
        public Bar(Gfx gfx, HINSTANCE instance, Edge edge = Edge.Bottom, bool shown = true, bool sized = false, bool tips = false, uint dpi = 96, RECT? on = null)
        {
            (this.gfx, this.edge, this.sized, this.dpi, display) = (gfx, edge, sized, dpi, on ?? Display);
            Frame = Layout.FrameFor(dpi, display, edge, 1f);
            fixed (char* name = BarClass)
            fixed (char* title = "Karpo retained bar")
            {
                Hwnd = CreateWindowExW(
                    WS.WS_EX_TOOLWINDOW | WS.WS_EX_TOPMOST | WS.WS_EX_NOACTIVATE | WS.WS_EX_NOREDIRECTIONBITMAP,
                    name,
                    title,
                    WS.WS_POPUP,
                    Frame.left,
                    Frame.top,
                    Frame.right - Frame.left,
                    Frame.bottom - Frame.top,
                    HWND.NULL,
                    HMENU.NULL,
                    instance,
                    null);
            }
            Assert.True(Hwnd != HWND.NULL, $"CreateWindowExW failed: {Marshal.GetLastSystemError()}");
            var desktop = ComUtil.Query<ICompositorDesktopInterop>(gfx.Compositor);
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
            if (tips)
            {
                // The tooltip's window class, which no other test may have made yet: its procedure is Windows' own, as the dock's leaves a tooltip's messages.
                Karpo.Tests.Signals.PreviewToolbarTests.Register(Preview.ClassName, instance);
                tip = new BarTip(gfx, instance);
                _ = Surfaces.Place(tip.Hwnd, Frame.left, Frame.top, Frame.right - Frame.left, Frame.bottom - Frame.top);
            }
            host = gfx.Compositor.CreateContainerVisual();
            Scene = NewScene();
            if (shown)
            {
                Show();
                FrameFilm.Settle();
            }
        }

        public HWND Hwnd { get; }

        /// <summary>The tooltip's own window; none where it is drawn in the bar's.</summary>
        public HWND TipHwnd => tip?.Hwnd ?? HWND.NULL;

        /// <summary>The window's region the bar's panel and the edge past it, as the dock's (DockWindow.Region): all DWM shows of it.</summary>
        public void Clip()
        {
            var panel = Layout.PanelRect;
            var (x, y) = (Layout.X - Frame.left, Layout.Y - Frame.top);
            Surfaces.Region(Hwnd, new RECT
            {
                left = x + (int)MathF.Floor(panel.X),
                top = y + (int)MathF.Floor(panel.Y),
                right = x + (int)MathF.Ceiling(panel.Right),
                bottom = Frame.bottom - Frame.top,
            });
        }

        /// <summary>Where the window stands on the screen.</summary>
        public RECT Frame { get; private set; }

        public Scene Scene { get; private set; }

        public Layout Layout { get; private set; } = null!;

        public void Show() => Surfaces.Show(Hwnd);

        /// <summary>Laid out as <paramref name="specs"/> show <paramref name="content"/>, arranged where <paramref name="editing"/>, as the dock lays a bar out.</summary>
        public Layout Lay(IReadOnlyList<PartSpec> specs, Content content, Look look, Config config, bool editing = false)
        {
            Layout = new Layout(gfx, dpi, display, display, edge, config, look, specs, content, editing);
            if (sized)
            {
                // The old way: the window the bar's own, moved and sized at once, the scene at its corner.
                Frame = new RECT { left = Layout.X, top = Layout.Y, right = Layout.X + Layout.Width, bottom = Layout.Y + Layout.Height };
                _ = Surfaces.Place(Hwnd, Frame.left, Frame.top, Layout.Width, Layout.Height);
            }
            Scene.Update(Layout, content, look, icons, shown: true, editing ? new EditView() : null, new Vector2(Layout.X - Frame.left, Layout.Y - Frame.top));
            return Layout;
        }

        private Scene NewScene() => new(gfx, target, host, tip?.Root) { TipPlaced = tip is null ? null : tip.At };

        /// <summary>§637: the morph the last <see cref="Switch"/> started; null for none.</summary>
        public Morphing? Morph { get; private set; }

        /// <summary>
        /// §637: laid out as another bar — another layout, another type —, as the dock lays out a bar's window whose display shows
        /// another (DockWindow.Lay): by the one morph where motion is on and the bar is in sight, its old scene the leaving picture
        /// and a new one laid out over it in the same window; laid out again in place otherwise.
        /// </summary>
        public Layout Switch(IReadOnlyList<PartSpec> specs, Content content, Look look, Config config)
        {
            Morph?.End();
            var next = new Layout(gfx, dpi, display, display, edge, config, look, specs, content);
            var before = Scene;
            if (!look.Motion.On || !before.Presented || !BarMorph.Between(before.Laid, next))
            {
                return Lay(specs, content, look, config);
            }
            var picture = before.Picture();
            Layout = next;
            Scene = NewScene();
            Scene.Update(Layout, content, look, icons, shown: true, null, new Vector2(Layout.X - Frame.left, Layout.Y - Frame.top));
            Morph = Morphing.Start(gfx.Compositor, before, picture, Scene, look.Motion, () => { });
            return Layout;
        }

        /// <summary>§637: gone by the motion, as a bar its display no longer shows goes (DockWindow.GoByMotion): its window destroyed once it has.</summary>
        public void GoByMotion(Look look)
        {
            Scene.Present(false, animate: true);
            Dispose((int)look.Motion.Ms(MotionKind.Hide));
        }

        /// <summary>§637: come by the motion, as a bar its display shows instead of another comes (DockWindow.ComeByMotion).</summary>
        public void ComeByMotion()
        {
            Scene.Present(false, animate: false);
            Scene.Present(true, animate: true);
        }

        /// <summary><paramref name="rect"/> (the layout's px) on the screen, <paramref name="inset"/> px in on every side.</summary>
        public RECT OnScreen(RectF rect, int inset = 0) => new()
        {
            left = Layout.X + (int)MathF.Ceiling(rect.X) + inset,
            top = Layout.Y + (int)MathF.Ceiling(rect.Y) + inset,
            right = Layout.X + (int)MathF.Floor(rect.Right) - inset,
            bottom = Layout.Y + (int)MathF.Floor(rect.Bottom) - inset,
        };

        public void Dispose() => Dispose(0);

        private void Dispose(int afterMs)
        {
            if (gone)
            {
                return;
            }
            gone = true;
            Morph?.End();
            var scene = Scene;
            // As the dock lets a bar go: the tooltip's content closed after the scene, which holds a part of it.
            tip?.Go();
            Surfaces.Destroy(Hwnd, () =>
            {
                scene.Dispose();
                target.Dispose();
                host.Dispose();
                tip?.Close();
            }, afterMs);
        }
    }

    /// <summary>On a hidden desktop of its own, the bars' window class there.</summary>
    internal static void OnHiddenDesktop(string what, Action<Gfx, HINSTANCE> body) =>
        WindowShots.OnHiddenDesktop(what, (gfx, instance) =>
        {
            Karpo.Tests.Signals.PreviewToolbarTests.Register(BarClass, instance);
            body(gfx, instance);
        });
}
