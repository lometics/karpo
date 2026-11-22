// The weather's lines are pressed by their parts, never as a whole (2026-10-11: "you should be able
// to hover / click the individual days ... same for the graph, you should be able to hover the
// individual hours. some parts you should also not be able to click / hover"; docs\widgets.md §131):
// each day and each hour is a target of its own, the conditions now one,
// and the place, the age, what it feels like and the air around them are none. A day searches the
// weather on that day; the keys walk the targets and Enter does what the one they are on does.
// Needs Direct2D: KARPO_UI_TESTS=1. Nothing reaches the network.

using System.Numerics;
using Karpo.Core;
using Karpo.Dock;
using Karpo.Dock.Panels;
using Karpo.Dock.QuickSettings.Controls;
using Karpo.Dock.Weather;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Forecasts;

public sealed class WeatherTargetsTests
{
    private static T Line<T>(Gfx gfx, ListKind kind, Func<string, WidgetItem, T> make, LineSize? size = null)
        where T : WeatherLineTile
    {
        var island = kind.New();
        island.Weather = SavedWeather.Options();
        if (size is { } chosen)
        {
            island.Size = chosen;
        }
        island.Normalize();
        var line = make(Config.IslandKey(null, island.Id), island);
        line.Source = SavedWeather.Source();
        return line;
    }

    private static TileStyle Style()
    {
        var look = new Look(Glass: false, Dark: true, Accent: Windows.UI.Color.FromArgb(255, 0, 0x78, 0xD4));
        return FolderFlyout.StyleFor(1f, look, FlyoutTheme.For(look));
    }

    /// <summary>
    /// Every day of the week is its own target, from 1 down the rows, each a band of the line from edge to edge,
    /// the first from its top and the last to its foot (§611); a day's click searches the weather on that day, today's the plain
    /// search, and a click beside them nothing.
    /// </summary>
    [UiFact]
    public void EachDayIsATargetAndItsClickSearchesThatDay()
    {
        Offscreen.OnStaThread(gfx => SavedWeather.AtAFixedTime(() =>
        {
            var opened = new List<string>();
            WeatherLineTile.Opening = opened.Add;
            using var days = Line(gfx, ListKind.WeatherDays, (key, island) => new WeatherDaysTile(key, island, gfx, new Config()));
            try
            {
                var style = Style();
                var height = days.ListHeight(360, style);
                var box = PopupPaint.Rect(0, 0, 360, height);
                var reading = WeatherReading.From(SavedWeather.Cache(), SavedWeather.Options(), Clock.Now().ToUniversalTime(), null);
                Assert.Equal(7, reading.Days.Count);

                // Down the middle of the line: the days in order, each once, a band each from the line's top to its foot
                // and from edge to edge, no room of the line nobody's (§611).
                var found = new List<int>();
                for (var y = 0.5f; y < height; y += 1)
                {
                    var target = days.CardTarget(new Vector2(180, y), box, style, WidgetSurface.List);
                    Assert.True(target != 0, $"({180}, {y}) of a {height:0} px line is no day");
                    if (found.Count == 0 || found[^1] != target)
                    {
                        found.Add(target);
                    }
                }
                Assert.Equal(Enumerable.Range(1, 7), found);
                Assert.Equal(1, days.CardTarget(new Vector2(180, 0.5f), box, style, WidgetSurface.List));
                Assert.Equal(7, days.CardTarget(new Vector2(180, height - 0.5f), box, style, WidgetSurface.List));
                Assert.Equal(days.CardTarget(new Vector2(180, height / 2), box, style, WidgetSurface.List),
                    days.CardTarget(new Vector2(0.5f, height / 2), box, style, WidgetSurface.List));
                Assert.Equal(days.CardTarget(new Vector2(180, height / 2), box, style, WidgetSurface.List),
                    days.CardTarget(new Vector2(359.5f, height / 2), box, style, WidgetSurface.List));

                // The third day: searched on its own day, by the region's name for it; today plainly.
                _ = days.Click(3);
                _ = days.Click(1);
                _ = days.Click(0);
                var place = SavedWeather.Options().Place;
                Assert.Equal(
                    [SearchUrls.Weather(new Config(), place, Nls.DayName(reading.Days[2].On.DayOfWeek)), SearchUrls.Weather(new Config(), place)],
                    opened);

                // The tooltip names the day in full and says its condition, which the row draws as a glyph.
                var middle = new Vector2(180, height / 2);
                var tip = days.CardTip(middle, box, style, WidgetSurface.List);
                Assert.NotNull(tip);
                Assert.Contains(reading.Days[days.CardTarget(middle, box, style, WidgetSurface.List) - 1].Condition, tip.Value.Text, StringComparison.Ordinal);
            }
            finally
            {
                WeatherLineTile.Opening = null;
            }
        }));
    }

    /// <summary>
    /// Across the graph every hour is its own target, from 1 left to right, each once, each the line's whole
    /// height, the first and the last to its edges (§611). The hour's tooltip says its time and its temperature, and its click searches the weather.
    /// </summary>
    [UiFact]
    public void EachHourIsATarget()
    {
        Offscreen.OnStaThread(gfx => SavedWeather.AtAFixedTime(() =>
        {
            var opened = new List<string>();
            WeatherLineTile.Opening = opened.Add;
            using var hours = Line(gfx, ListKind.WeatherHours, (key, island) => new WeatherHoursTile(key, island, gfx, new Config()));
            try
            {
                var style = Style();
                var height = hours.ListHeight(360, style);
                var box = PopupPaint.Rect(0, 0, 360, height);
                var reading = WeatherReading.From(SavedWeather.Cache(), SavedWeather.Options(), Clock.Now().ToUniversalTime(), null);
                Assert.True(reading.Hours.Count > 20, $"{reading.Hours.Count} hours");

                var found = new List<int>();
                for (var x = 0.5f; x < 360; x += 0.5f)
                {
                    var target = hours.CardTarget(new Vector2(x, height / 2), box, style, WidgetSurface.List);
                    if (target != 0 && (found.Count == 0 || found[^1] != target))
                    {
                        found.Add(target);
                    }
                }
                Assert.Equal(Enumerable.Range(1, reading.Hours.Count), found);
                // §611: an hour's band is the line's whole height and the first and last reach its edges: over the graph,
                // in the line's air, the hour under the pointer still; at either end the first hour and the last.
                Assert.Equal(hours.CardTarget(new Vector2(180, height / 2), box, style, WidgetSurface.List),
                    hours.CardTarget(new Vector2(180, 0.5f), box, style, WidgetSurface.List));
                Assert.Equal(1, hours.CardTarget(new Vector2(0.5f, height - 0.5f), box, style, WidgetSurface.List));
                Assert.Equal(reading.Hours.Count, hours.CardTarget(new Vector2(359.5f, 0.5f), box, style, WidgetSurface.List));

                var at = new Vector2(180, height / 2);
                var hour = reading.Hours[hours.CardTarget(at, box, style, WidgetSurface.List) - 1];
                var tip = hours.CardTip(at, box, style, WidgetSurface.List);
                Assert.NotNull(tip);
                Assert.StartsWith($"{hour.Time} · {hour.Temperature}", tip.Value.Text, StringComparison.Ordinal);

                _ = hours.Click(0);
                Assert.Empty(opened);
                _ = hours.Click(5);
                Assert.Equal([SearchUrls.Weather(new Config(), SavedWeather.Options().Place)], opened);
            }
            finally
            {
                WeatherLineTile.Opening = null;
            }
        }));
    }

    /// <summary>
    /// §632 (the person, 2026-11-09: "the items in the hourly forecast should all have the same width. (right now the
    /// ones on the edge are wider)"): across every forecast line laid in columns — the hourly Graph and List, the daily
    /// List at Compact and the daily Graph, at each of their sizes — every hour and day takes the pointer on a band as
    /// wide as every other's, the first from the line's leading edge and the last to its trailing one, a px apart at most
    /// by rounding.
    /// </summary>
    [UiFact]
    public void EveryHourAndDayIsAsWideAsTheOthers()
    {
        Offscreen.OnStaThread(gfx => SavedWeather.AtAFixedTime(() =>
        {
            var style = Style();
            var reading = WeatherReading.From(SavedWeather.Cache(), SavedWeather.Options(), Clock.Now().ToUniversalTime(), null);
            foreach (var size in new[] { LineSize.Compact, LineSize.Full })
            {
                foreach (var (name, line, count) in new (string, WeatherLineTile, int)[]
                {
                    ("hourly graph", Line(gfx, ListKind.WeatherHours, (key, island) => new WeatherHoursTile(key, island, gfx, new Config()), size), reading.Hours.Count),
                    ("hourly list", Line(gfx, ListKind.WeatherHoursList, (key, island) => new WeatherHoursListTile(key, island, gfx, new Config()), size), reading.Next.Count),
                    ("daily graph", Line(gfx, ListKind.WeatherDaysGraph, (key, island) => new WeatherDaysGraphTile(key, island, gfx, new Config()), size), reading.Days.Count),
                    ("daily list", Line(gfx, ListKind.WeatherDays, (key, island) => new WeatherDaysTile(key, island, gfx, new Config()), LineSize.Compact), reading.Days.Count),
                })
                {
                    using (line)
                    {
                        foreach (var width in new[] { 300f, 360f, 517f })
                        {
                            var height = ((IListContent)line).ListHeight(width, style);
                            var box = PopupPaint.Rect(0, 0, width, height);
                            // Each target's run along the line's middle, from its first px to past its last.
                            var runs = new List<(int Target, float From, float To)>();
                            for (var x = 0.25f; x < width; x += 0.5f)
                            {
                                var target = line.CardTarget(new Vector2(x, height / 2), box, style, WidgetSurface.List);
                                if (runs.Count > 0 && runs[^1].Target == target)
                                {
                                    runs[^1] = runs[^1] with { To = x + 0.25f };
                                }
                                else
                                {
                                    runs.Add((target, x - 0.25f, x + 0.25f));
                                }
                            }
                            var said = $"{name} {size} {width:0} px";
                            Assert.True(runs.All(run => run.Target != 0), $"{said}: room of the line no item's");
                            Assert.Equal(Enumerable.Range(1, count), runs.Select(run => run.Target));
                            var widths = runs.Select(run => run.To - run.From).ToList();
                            Assert.True(widths.Max() - widths.Min() <= 1, $"{said}: its items {string.Join(", ", widths.Select(each => $"{each:0.#}"))} px wide");
                        }
                    }
                }
            }
        }));
    }

    /// <summary>
    /// The conditions (§370, §379): the whole line, the line's own click, which the folder lights whole
    /// (FolderFlyout.LightsWhole) — the place, the glyph, the temperature, the age at the header's end, the
    /// air round them and, on a line taller than its header, the room under it, with no refresh button
    /// carved out of it any more (§451); on the whole header and on one row. A click on it searches the weather. It was the header alone, a plate inset in it: the
    /// person, 2026-10-28, "there are some spaces that when hovering over dont get highlighted".
    /// </summary>
    [UiFact]
    public void TheConditionsAreTheWholeLine()
    {
        Offscreen.OnStaThread(gfx => SavedWeather.AtAFixedTime(() =>
        {
            var opened = new List<string>();
            WeatherLineTile.Opening = opened.Add;
            try
            {
                var cell = ListKind.WeatherNow.New();
                using var now = Line(gfx, ListKind.WeatherNow, (key, island) => new WeatherPartTile(key, island, gfx, new Config()));
                Assert.True(FolderFlyout.LightsWhole(new FolderCell { Island = cell }, now));
                var style = Style();
                var height = now.ListHeight(360, style);
                foreach (var box in new[] { PopupPaint.Rect(0, 0, 360, height + 120), PopupPaint.Rect(0, 0, 360, 40) })
                {
                    int At(float x, float y) => now.TargetOn(new Vector2(x, y), box, style, WidgetSurface.List);
                    for (var y = box.top + 0.5f; y < box.bottom; y += 2)
                    {
                        for (var x = box.left + 0.5f; x < box.right; x += 2)
                        {
                            var target = At(x, y);
                            Assert.True(target == 0, $"({x}, {y}) of a {box.bottom} px line is not the line but {target}");
                        }
                    }
                }
                _ = now.Click(0);
                Assert.Equal([SearchUrls.Weather(new Config(), SavedWeather.Options().Place)], opened);
            }
            finally
            {
                WeatherLineTile.Opening = null;
            }
        }));
    }

    /// <summary>
    /// The keys walk a line's targets and then leave it for the folder; Enter does what the one they
    /// are on does, and the first target's where they are on none.
    /// </summary>
    [UiFact]
    public void TheKeysWalkTheTargets()
    {
        Offscreen.OnStaThread(gfx => SavedWeather.AtAFixedTime(() =>
        {
            var opened = new List<string>();
            WeatherLineTile.Opening = opened.Add;
            try
            {
                using var now = Line(gfx, ListKind.WeatherNow, (key, island) => new WeatherPartTile(key, island, gfx, new Config()));
                // The conditions are the whole line (§379): no parts to walk; Enter is the folder's, its click.
                Assert.False(now.KeyOn(VK.VK_RETURN, WidgetSurface.List));
                Assert.False(now.KeyOn(VK.VK_RIGHT, WidgetSurface.List));
                Assert.Equal(0, now.Lighted.Keyed);
                Assert.False(now.KeyOn(VK.VK_DOWN, WidgetSurface.List));
                Assert.Empty(opened);

                using var days = Line(gfx, ListKind.WeatherDays, (key, island) => new WeatherDaysTile(key, island, gfx, new Config()));
                Assert.True(days.KeyOn(VK.VK_DOWN, WidgetSurface.List));
                Assert.True(days.KeyOn(VK.VK_DOWN, WidgetSurface.List));
                Assert.Equal(2, days.Lighted.Keyed);
                opened.Clear();
                Assert.True(days.KeyOn(VK.VK_RETURN, WidgetSurface.List));
                Assert.Single(opened);
                Assert.True(days.KeyOn(VK.VK_UP, WidgetSurface.List));
                Assert.False(days.KeyOn(VK.VK_UP, WidgetSurface.List));
                Assert.False(days.KeyOn(VK.VK_LEFT, WidgetSurface.List));
            }
            finally
            {
                WeatherLineTile.Opening = null;
            }
        }));
    }
}
