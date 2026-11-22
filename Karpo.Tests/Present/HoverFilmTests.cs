// A widget lit under the pointer without a blink (the person, 2026-10-29: "when hovering over a widget in the edit
// mode it flickers quickly before the hover background is shown"). The pointer onto a block of the bar and off it, and
// onto a line of a folder and off it, arranged and at rest, in Windows' look and in Graphite (§405), animations on, as
// the dock tells them (DockWindow.EditHoverChanged and HoverChanged; the folder's window its WM_MOUSEMOVE), filmed
// (FrameFilm) over the widget: every frame on the one way from the before to the end (FrameFilm.Film.OffTheWay).
// Strips to KARPO_PNG_DIR as film-hover-<bar|line>-<look>-<edit|rest>-<in|out>.png.

using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Folders;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using static Karpo.Tests.Present.RetainedBarTests;
using Edge = Karpo.Core.Edge;

namespace Karpo.Tests.Present;

public sealed class HoverFilmTests
{
    /// <summary>Windows' own look and the person's, Graphite, dark, animations on.</summary>
    private static IEnumerable<(string Name, Config Config, Look Look)> Looks() =>
        new[] { (Name: "windows", Theme: "windows"), (Name: "graphite", Theme: "graphite") }.Select(look =>
        {
            var config = LookShorthand.Wearing(palette: look.Theme);
            return (look.Name, config, TestLooks.LookFor(dark: true).Wearing(config) with { Animations = true });
        });

    /// <summary>
    /// A line's wash fades from its colour at rest to the one under the pointer and pressed, and back, by the compositor's
    /// straight interpolation of the brush (FolderFlyout.Retained.cs, washFade): in every theme and mode, over its panel,
    /// what that shows is on the way between the two ends at every step — never lighter or darker than both (the
    /// arranged line's flash: a 70 % card to a 10 % highlight passed through a half-clear light grey).
    /// </summary>
    [Fact]
    public void ALinesWashFadesOnTheWayBetweenItsColours()
    {
        var wrong = new List<string>();
        foreach (var theme in LookShorthand.Palettes)
        {
            foreach (var dark in new[] { true, false })
            {
                var look = TestLooks.LookFor(dark).Wearing(LookShorthand.Wearing(palette: theme));
                var colours = FlyoutTheme.For(look);
                var panel = Windows.UI.Color.FromArgb(255, colours.Base.R, colours.Base.G, colours.Base.B);
                var fills = new[] { FolderFlyout.CardFill.None, FolderFlyout.CardFill.LineHover, FolderFlyout.CardFill.LinePressed };
                foreach (var from in fills)
                {
                    foreach (var to in fills.Where(fill => fill != from))
                    {
                        var (a, b) = (FolderFlyout.InkOf(colours, from, look.Colors), FolderFlyout.InkOf(colours, to, look.Colors));
                        var (start, end) = (Seen(a, panel), Seen(b, panel));
                        for (var step = 1; step < 10; step++)
                        {
                            var t = step / 10f;
                            var mid = Seen(Lerp(a, b, t), panel);
                            for (var channel = 0; channel < 3; channel++)
                            {
                                var (low, high) = (Math.Min(start[channel], end[channel]), Math.Max(start[channel], end[channel]));
                                if (mid[channel] < low - 1.5f || mid[channel] > high + 1.5f)
                                {
                                    wrong.Add($"{theme} {(dark ? "dark" : "light")}, {from} to {to} at {t:P0}: {mid[channel]:F0} in channel {channel}, the ends {start[channel]:F0} and {end[channel]:F0}");
                                    break;
                                }
                            }
                        }
                    }
                }
            }
        }
        Assert.True(wrong.Count == 0, string.Join(Environment.NewLine, wrong.Take(12)));

        // What the compositor shows of a brush over the panel, and its straight interpolation of two.
        static float[] Seen(Windows.UI.Color brush, Windows.UI.Color under)
        {
            var a = brush.A / 255f;
            return [(brush.R * a) + (under.R * (1 - a)), (brush.G * a) + (under.G * (1 - a)), (brush.B * a) + (under.B * (1 - a))];
        }
        static Windows.UI.Color Lerp(Windows.UI.Color a, Windows.UI.Color b, float t) => Windows.UI.Color.FromArgb(
            (byte)MathF.Round(a.A + ((b.A - a.A) * t)), (byte)MathF.Round(a.R + ((b.R - a.R) * t)),
            (byte)MathF.Round(a.G + ((b.G - a.G) * t)), (byte)MathF.Round(a.B + ((b.B - a.B) * t)));
    }

    [UiFact]
    public void ABlockOfTheBarLightsOneWay()
    {
        OnHiddenDesktop("hover-bar", (gfx, instance) =>
        {
            var wrong = new List<string>();
            foreach (var (name, _, look) in Looks())
            {
                foreach (var editing in new[] { true, false })
                {
                    using var bar = new RetainedBarTests.Bar(gfx, instance);
                    bar.Lay(Parts(), Things(3), look, RetainedBarTests.Islands, editing);
                    bar.Clip();
                    FrameFilm.Settle();
                    var part = bar.Layout.PartIndex(Config.IslandKey(null, "task"));
                    var block = bar.OnScreen(bar.Layout.BlockRect(part), inset: 2);
                    var middle = bar.Layout.BlockRect(part).Center;
                    var hit = Assert.NotNull(bar.Layout.HitTest(middle.X, middle.Y, editing));
                    void Point(bool on)
                    {
                        if (editing)
                        {
                            // As DockWindow.EditHoverChanged tells it.
                            bar.Scene.Highlight(null, false);
                            bar.Scene.Edit(new EditView(Hovered: on ? part : null));
                        }
                        else
                        {
                            bar.Scene.Highlight(on ? hit : null, false);
                        }
                    }
                    foreach (var on in new[] { true, false })
                    {
                        Film($"hover-bar-{name}-{(editing ? "edit" : "rest")}-{(on ? "in" : "out")}", gfx, block, bar.Hwnd, () => Point(on), wrong);
                    }
                }
            }
            Assert.True(wrong.Count == 0, string.Join(Environment.NewLine, wrong));
        });
    }

    [UiFact]
    public void ALineOfAFolderLightsOneWay()
    {
        WindowShots.OnHiddenDesktop("hover-line", (gfx, instance) =>
        {
            var wrong = new List<string>();
            var work = new RECT { left = 0, top = 0, right = 1600, bottom = 1000 };
            var anchor = new POINT { x = 800, y = 1000 };
            foreach (var (name, config, look) in Looks())
            {
                foreach (var editing in new[] { true, false })
                {
                    var island = Folder();
                    using var folder = new FolderIsland($"layout:default/{island.Id}", island, gfx, config);
                    using var flyout = new FolderFlyout(gfx, instance, 0);
                    flyout.Show(gfx, look, 1f, anchor, work, Edge.Bottom, folder, FlyoutAlign.Center, editing);
                    Assert.True(flyout.IsOpen);
                    FrameFilm.Settle();
                    // The second switch, between two others; off it, beside the panel.
                    var line = flyout.ScreenOfCard(1);
                    var panel = flyout.PanelOnScreen;
                    var away = new POINT { x = panel.right + 30, y = line.y };
                    var block = Around(flyout, line);
                    _ = flyout.Message(WM.WM_MOUSEMOVE, 0, FolderEditClickTests.Client(flyout.Hwnd, away));
                    FrameFilm.Settle();
                    foreach (var on in new[] { true, false })
                    {
                        Film($"hover-line-{name}-{(editing ? "edit" : "rest")}-{(on ? "in" : "out")}", gfx, block, flyout.Hwnd,
                            () => _ = flyout.Message(WM.WM_MOUSEMOVE, 0, FolderEditClickTests.Client(flyout.Hwnd, on ? line : away)), wrong);
                    }
                    flyout.Hide();
                }
            }
            Assert.True(wrong.Count == 0, string.Join(Environment.NewLine, wrong));
        });
    }

    /// <summary>
    /// <paramref name="act"/> filmed over <paramref name="block"/> (screen px) of <paramref name="window"/>: what is not
    /// one way to <paramref name="wrong"/>, and nothing changed at all too.
    /// </summary>
    private static void Film(string name, Gfx gfx, RECT block, HWND window, Action act, List<string> wrong)
    {
        var area = new RECT { left = block.left - 8, top = block.top - 8, right = block.right + 8, bottom = block.bottom + 8 };
        var film = FrameFilm.Shoot(name, gfx, area, () => [window], act, filmMs: 300);
        var off = film.OffTheWay(block, out var steps);
        Console.WriteLine($"{name}: {steps}");
        if (film.Distance(film.Before, film.End, block) < 0.5)
        {
            wrong.Add($"{name}: the widget looks the same before and after");
        }
        wrong.AddRange(off.Select(line => $"{name}: {line}"));
    }

    /// <summary>The line round <paramref name="at"/> (screen px), 2 px in: its card, as the folder's lines stand edge to edge (§410).</summary>
    private static RECT Around(FolderFlyout flyout, POINT at)
    {
        var panel = flyout.PanelOnScreen;
        var (top, bottom) = (at.y, at.y);
        // A line is as tall as the next line's middle is from its own, at most; a third of the way each side is its own.
        var next = flyout.ScreenOfCard(2);
        var half = Math.Max(4, (next.y - at.y) / 3);
        return new RECT { left = panel.left + 4, top = top - half, right = panel.right - 4, bottom = bottom + half };
    }

    /// <summary>A list of three switches, the battery and the clock.</summary>
    private static BarItem Folder()
    {
        var island = BarKind.FolderList.New();
        island.Id = "hover";
        island.Folder!.Lists = [];
        foreach (var kind in new[] { ListKind.NightLight, ListKind.AirplaneMode, ListKind.EnergySaver, ListKind.Battery, ListKind.Clock })
        {
            island.Folder.Put(kind.New(), 0);
        }
        return island;
    }
}
