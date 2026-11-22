// A line that reads what it shows again is refreshed from its right click (IRefreshable; docs\widgets.md §451; the
// person, 2026-10-31: "the refresh buttons should be removed and they should go into the right click context menu"):
//
//   * no line draws a refresh control any more — no glyph, no tooltip, no press of its own — and a press anywhere
//     on it reads nothing again;
//   * every line with something to read again ends its menu at rest with "Refresh", the refresh glyph and when it
//     was last read under it; the pick reads it again and leaves the folder open. None while a read is on its way,
//     none for a line with nothing to read (§308: left out, not greyed), none for a line that reads nothing.
//
// Pictured at 150 %, dark and light, to KARPO_PNG_DIR: refresh-headers-<look>.png, every refreshing line on one row
// and at four; refresh-menu-<line>-<look>-150.png, a line's menu at rest with its Refresh.

using System.Numerics;
using Karpo.Accounts;
using Karpo.Core;
using Karpo.Dock;
using Karpo.Dock.Markets;
using Karpo.Dock.Pim;
using Karpo.Dock.Rss;
using Karpo.Dock.Sports;
using Karpo.Dock.Weather;
using Karpo.Tests.Edit;
using Karpo.Tests.Forecasts;
using Karpo.Tests.Islands;
using Karpo.Tests.Rss;
using Karpo.Tests.Sports;
using TerraFX.Interop.DirectX;
using static Karpo.Tests.Islands.Offscreen;
using TheDock = Karpo.Dock.Dock;
using Win = TerraFX.Interop.Windows;

namespace Karpo.Tests.Folders;

public sealed unsafe class RefreshMenuTests
{
    private const string Owner = "layout:default";

    /// <summary>Segoe Fluent Icons: Refresh.</summary>
    private const string RefreshGlyph = "";

    /// <summary>A fetch the test holds: how many times it was asked, and whether one is on its way; and how many times the market's store asked for a price.</summary>
    private sealed class Asked
    {
        public int Count;
        public bool Fetching;
        public int Prices;
    }

    /// <summary>Every kind of line that reads what it shows again, made on fakes the test holds, nothing from the network.</summary>
    private static List<(string Name, CellIsland Line)> Lines(Gfx gfx, Asked weatherAsks)
    {
        T Made<T>(ListKind kind, Action<CellItem>? set = null, Action<T>? then = null)
            where T : CellIsland
        {
            var item = kind.New();
            item.Id = kind.Id();
            item.Weather = SavedQuakes.Zurich();
            set?.Invoke(item);
            var tile = (T)Karpo.Dock.Islands.CreateLine(Config.IslandKey(Owner, item.Id), item, kind, gfx, new Config())!;
            then?.Invoke(tile);
            return tile;
        }
        var cache = SavedWeather.Cache();
        var now = Clock.Now().ToUniversalTime();
        var quakes = new QuakeHeld
        {
            Quakes = QuakeFeeds.Parse(SavedQuakes.Feed(("n1", 4.8, 46.5, 9.9, now.AddMinutes(-42), null), ("n2", 3.1, 45.9, 6.9, now.AddHours(-5), null))),
            FetchedUtc = now.AddMinutes(-3),
        };
        var mail = new MailStore(new AccountSet([]));
        mail.Answered(ListLinesTests.Inbox(Clock.Now()), unread: 3);
        var people = new ContactStore(new AccountSet([]));
        people.Answered([new Person("a", "Adrian Hager", "Contoso", ["adrian.hager@contoso.com"], ["+41 79 123 45 67"])]);
        CalendarIsland.Made = ListFormCases.Store(ListFormCases.Week);
        try
        {
            return
            [
                ("Weather", Made<WeatherLineTile>(ListKind.WeatherNow, then: tile => tile.Source = new WeatherSource
                {
                    Read = () => (cache, null, weatherAsks.Fetching),
                    Ask = () =>
                    {
                        weatherAsks.Count++;
                        weatherAsks.Fetching = true;
                        return true;
                    },
                })),
                ("Air quality", Made<AirQualityTile>(ListKind.AirQuality, then: tile => tile.Source = SavedAir.Source(SavedAir.Cache(european: 46, us: 54, pollen: "12.0")))),
                ("Tides", Made<TidesTile>(ListKind.Tides, then: tile =>
                {
                    tile.Source = SavedTides.Source(SavedTides.Cache());
                    tile.Imperial = false;
                })),
                ("Earthquakes", Made<QuakesLine>(ListKind.Quakes, then: tile => tile.Source = () => quakes)),
                ("Sports", Made<SportsTile>(ListKind.Sports, set: item => item.Sports = SavedSports.Monza(), then: tile =>
                    tile.Source = new SportsSource { Read = () => new SportsHeld(SavedSports.MonzaLive(), SavedSports.SavedUtc, null, false) })),
                ("Markets", MarketOffscreenTests.Made(gfx, new Config(), new MarketStore(new MarketAnswers((_, _) =>
                {
                    Interlocked.Increment(ref weatherAsks.Prices);
                    return Task.FromResult(MarketQuotes.Parse(MarketOffscreenTests.Answer()));
                }), MarketDisk.None), "s", MarketOffscreenTests.Held("AAPL"))),
                ("News", RssSamples.Made(gfx, ListKind.Rss.New(), RssSamples.Settled(Clock.Now()))),
                ("Mail", new MailIsland(Config.IslandKey(Owner, "mail"), ListKind.Mail.New(), gfx, new Config(), mail)),
                ("Contacts", new ContactsLine(Config.IslandKey(Owner, "contacts"), ListKind.Contacts.New(), gfx, new Config(), people)),
                ("Calendar", Made<CalendarIsland>(ListKind.Calendar)),
                ("Events", Made<EventsTile>(ListKind.Events)),
                ("Week view", Made<CalendarViewTile>(ListKind.WeekView)),
            ];
        }
        finally
        {
            CalendarIsland.Made = null;
        }
    }

    private static TileStyle StyleFor(float scale, Look look) => FolderFlyout.StyleFor(scale, look, FlyoutTheme.For(look));

    /// <summary>Runs <paramref name="body"/> over every refreshing line, the clock frozen; the lines disposed after.</summary>
    private static void WithLines(Action<Gfx, List<(string Name, CellIsland Line)>, Asked> body) => OnStaThread(gfx => WithLines(gfx, body));

    private static void WithLines(Gfx gfx, Action<Gfx, List<(string Name, CellIsland Line)>, Asked> body)
    {
        Clock.Now = () => ListFormCases.Frozen;
        var asks = new Asked();
        var lines = new List<(string Name, CellIsland Line)>();
        try
        {
            lines = Lines(gfx, asks);
            body(gfx, lines, asks);
        }
        finally
        {
            foreach (var (_, line) in lines)
            {
                line.Dispose();
            }
            Clock.Now = Clock.Real;
        }
    }

    // --- No line draws one --------------------------------------------------------------------------

    /// <summary>
    /// Every refreshing line, on one row and on four, at 100 % and 150 %: no refresh glyph drawn, no tooltip saying
    /// "Refresh" anywhere on it; and a press anywhere on the weather's, let go where it came down, reads nothing again.
    /// </summary>
    [UiFact]
    public void NoLineDrawsARefreshControl() => WithLines((gfx, lines, asks) =>
    {
        var failures = new List<string>();
        foreach (var scale in new[] { 1f, 1.5f })
        {
            var style = StyleFor(scale, ListFormPictures.LookFor(true));
            var width = FolderFlyout.ListWidth(scale);
            foreach (var (name, line) in lines)
            {
                line.Visible(true);
                foreach (var rows in new[] { 1, 4 })
                {
                    var box = PopupPaint.Rect(0, 0, width, LineRows.Px(rows, style));
                    var glyphs = 0;
                    PaintWatch.Said = (text, _) => glyphs += text.Contains(RefreshGlyph, StringComparison.Ordinal) ? 1 : 0;
                    try
                    {
                        Bitmap(width, box.bottom, dc => line.PaintOn(dc, box, style, WidgetSurface.List), _ => { });
                    }
                    finally
                    {
                        PaintWatch.Said = null;
                    }
                    if (glyphs > 0)
                    {
                        failures.Add($"{name}, {rows} row(s) at {scale:0.##}: draws the refresh glyph");
                    }
                    var tipped = Points(box).FirstOrDefault(point =>
                        line.TipOn(point, box, style, WidgetSurface.List) is { Text: var tip } && tip.StartsWith(S.DockWidgetMenuRefresh, StringComparison.Ordinal));
                    if (tipped != default)
                    {
                        failures.Add($"{name}, {rows} row(s) at {scale:0.##}: says \"{S.DockWidgetMenuRefresh}\" under the pointer at ({tipped.X}, {tipped.Y})");
                    }
                }
            }
        }
        var weather = lines.Single(each => each.Name == "Weather").Line;
        var at = StyleFor(1.5f, ListFormPictures.LookFor(true));
        var four = PopupPaint.Rect(0, 0, FolderFlyout.ListWidth(1.5f), LineRows.Px(4, at));
        foreach (var point in Points(four))
        {
            if (weather.PressOn(point, four, at, WidgetSurface.List))
            {
                weather.ReleaseOn(WidgetSurface.List);
            }
        }
        if (asks.Count > 0)
        {
            failures.Add($"Weather: a press on it read it again {asks.Count} time(s)");
        }
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    });

    /// <summary>Every fourth pixel of <paramref name="box"/>, at its middle.</summary>
    private static IEnumerable<Vector2> Points(D2D_RECT_F box)
    {
        for (var y = box.top + 1; y < box.bottom; y += 4)
        {
            for (var x = box.left + 1; x < box.right; x += 4)
            {
                yield return new Vector2(x + 0.5f, y + 0.5f);
            }
        }
    }

    // --- Its menu has it ----------------------------------------------------------------------------

    /// <summary>
    /// Every refreshing line's menu at rest ends with "Refresh": the refresh glyph, when it was last read under it
    /// where it knows, and a pick that reads it again in Karpo — the folder left open.
    /// </summary>
    [UiFact]
    public void EveryRefreshingLinesMenuEndsWithRefresh() => WithLines((gfx, lines, asks) =>
    {
        var failures = new List<string>();
        foreach (var (name, line) in lines)
        {
            var (items, rows) = TheDock.FolderMenuRows(line.Settings, line);
            var last = items.LastOrDefault(item => !item.Separator && !item.Header);
            if (last is null || last.Text != S.DockWidgetMenuRefresh)
            {
                failures.Add($"{name}: its menu ends with \"{last?.Text}\", not \"{S.DockWidgetMenuRefresh}\" ({string.Join(", ", items.Select(item => item.Text))})");
                continue;
            }
            if (last.Icon is not FlyoutIcon.Glyph { Code: RefreshGlyph })
            {
                failures.Add($"{name}: its Refresh wears {last.Icon}, not the refresh glyph");
            }
            var updated = ((IRefreshable)line).UpdatedUtc is { } read ? WeatherAge.Updated(read.ToLocalTime(), Clock.Now()) : null;
            if (last.Detail != updated)
            {
                failures.Add($"{name}: its Refresh says \"{last.Detail}\" under it, not \"{updated}\"");
            }
            if (TheDock.ActRowOf(rows, last.Id) is not { } row || TheDock.HandsAway(row.Act))
            {
                failures.Add($"{name}: its Refresh is no act of Karpo's own");
            }
        }
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));

        // The pick reads the weather again, as its button did; none to pick while that read is on its way.
        var weather = lines.Single(each => each.Name == "Weather").Line;
        var (menu, acts) = TheDock.FolderMenuRows(weather.Settings, weather);
        var refresh = TheDock.ActRowOf(acts, menu.Single(item => item.Text == S.DockWidgetMenuRefresh).Id)!;
        var opened = new List<string>();
        TheDock.RunAct(refresh, weather, (target, _) => opened.Add(target));
        Assert.Equal(1, asks.Count);
        Assert.Empty(opened);
        Assert.DoesNotContain(TheDock.FolderMenuRows(weather.Settings, weather).Items, item => item.Text == S.DockWidgetMenuRefresh);
        asks.Fetching = false;

        // The market's store is asked for its share again.
        var market = (MarketIsland)lines.Single(each => each.Name == "Markets").Line;
        var prices = asks.Prices;
        var (marketMenu, marketActs) = TheDock.FolderMenuRows(market.Settings, market);
        TheDock.RunAct(TheDock.ActRowOf(marketActs, marketMenu.Single(item => item.Text == S.DockWidgetMenuRefresh).Id)!, market, (_, _) => { });
        Assert.True(Patience.Until(() => asks.Prices > prices), "the market's store was not asked again");
    });

    /// <summary>
    /// Every kind a list holds, made as a folder makes it: its menu at rest has "Refresh" exactly where it reads
    /// what it shows again and has something to read now — a weather line without a place has none, a clock none.
    /// </summary>
    [UiFact]
    public void OnlyALineWithSomethingToReadAgainHasRefresh()
    {
        var failures = new List<string>();
        var refreshing = 0;
        OnStaThread(gfx => SavedWeather.AtAFixedTime(() =>
        {
            foreach (var widget in ListWidgets.All)
            {
                var item = ListFormPictures.Line(widget.Kind, _ => { });
                var island = Karpo.Dock.Islands.CreateLine($"{Owner}/folder/{item.Id}", item, widget.Kind, gfx, new Config());
                try
                {
                    var can = island is IRefreshable { CanRefresh: true, Refreshing: false };
                    refreshing += can ? 1 : 0;
                    var says = TheDock.FolderMenuRows(item, island).Items.Any(row => row.Text == S.DockWidgetMenuRefresh);
                    // A shared store's read that began or ended meanwhile (the earthquakes') decides nothing.
                    if (says != can && can == island is IRefreshable { CanRefresh: true, Refreshing: false })
                    {
                        failures.Add($"{widget.Kind}: {(can ? "reads again, and its menu has no Refresh" : "has nothing to read again, and its menu has Refresh")}");
                    }
                }
                finally
                {
                    island?.Dispose();
                }
            }
        }));
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
        Assert.True(refreshing > 0, "no kind made as a folder makes it had anything to read again: the sweep proves nothing");
    }

    // --- Pictures -----------------------------------------------------------------------------------

    /// <summary>Every refreshing line at 150 %, dark and light, on one row and at four: refresh-headers-&lt;dark|light&gt;.png.</summary>
    [UiFact]
    public void TheHeadersArePictured() => WithLines((gfx, lines, _) =>
    {
        foreach (var dark in new[] { true, false })
        {
            Sheet(gfx, lines, dark, Path.Combine(Pictures, $"refresh-headers-{(dark ? "dark" : "light")}.png"));
        }
    });

    /// <summary>The sheet: a row per line, its one row beside its four.</summary>
    private static void Sheet(Gfx gfx, List<(string Name, CellIsland Line)> lines, bool dark, string path)
    {
        const float scale = 1.5f;
        var look = ListFormPictures.LookFor(dark);
        var theme = FlyoutTheme.For(look);
        var style = StyleFor(scale, look);
        var width = FolderFlyout.ListWidth(scale);
        var pad = PopupPaint.Round(16 * scale);
        var label = PopupPaint.Round(11 * scale);
        var labelLine = TilePaint.LineHeight(gfx, label);
        var one = LineRows.Px(1, style);
        var four = LineRows.Px(4, style);
        var band = labelLine + four + pad;
        var sheetWidth = pad + (2 * (width + pad));
        var sheetHeight = pad + (lines.Count * band);
        Bitmap(sheetWidth, sheetHeight, dc =>
        {
            PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, sheetWidth, sheetHeight), 0,
                PopupPaint.Ink(dark ? Windows.UI.Color.FromArgb(255, 0x1C, 0x1C, 0x1C) : Windows.UI.Color.FromArgb(255, 0xE8, 0xE8, 0xE8)));
            void Plate(CellIsland line, float x, float y, float height, string caption)
            {
                PopupPaint.Text(dc, gfx, caption, label, Weight.Regular, theme.TextSecondary, x, y);
                var plate = PopupPaint.Rect(x, y + labelLine, width, height);
                PopupPaint.Fill(dc, plate, PopupPaint.Round(8 * scale), PopupPaint.Ink(theme.Base));
                PopupPaint.Stroke(dc, plate, PopupPaint.Round(8 * scale), MathF.Max(1, MathF.Round(scale)), PopupPaint.Ink(theme.Stroke));
                var context = dc;
                var box = PopupPaint.Rect(0, 0, width, height);
                PopupPaint.Clipped(context, plate, PopupPaint.Round(8 * scale), () => Shifted(context, plate.left, plate.top, () =>
                    FolderFlyout.PaintContent(context, line, box, style)));
            }
            var y = pad;
            foreach (var (name, line) in lines)
            {
                line.Visible(true);
                Plate(line, pad, y, one, $"{name}: one row");
                Plate(line, pad + width + pad, y, four, $"{name}: four rows");
                y += band;
            }
        }, bitmap => Save(bitmap, path));
    }

    /// <summary>The weather's, the news' and the mail's menus at rest, at 150 %, dark and light: refresh-menu-&lt;line&gt;-&lt;look&gt;-150.png.</summary>
    [UiFact]
    public void TheMenusArePictured() => WindowShots.OnHiddenDesktop("refresh-menu", (gfx, instance) => WithLines(gfx, (_, lines, _) =>
    {
        const float scale = 1.5f;
        foreach (var name in new[] { "Weather", "News", "Mail", "Calendar" })
        {
            var line = lines.Single(each => each.Name == name).Line;
            var rows = TheDock.RestMenuRows(line.Settings, inFolder: true, line).Items;
            foreach (var dark in new[] { true, false })
            {
                using var menu = new Flyout(gfx, instance, 0);
                var work = new Win.RECT { right = (int)(1920 * scale), bottom = (int)(1040 * scale) };
                menu.Show(gfx, AddListPictures.LookFor(dark), scale, rows, FlyoutLayout.List, new Win.POINT { x = work.right / 2, y = 0 }, work, Edge.Top, null);
                WindowShots.Settle();
                var file = $"refresh-menu-{name.ToLowerInvariant()}-{(dark ? "dark" : "light")}-150.png";
                Assert.True(WindowShots.Save(menu.Hwnd, file), $"{file} came back blank");
                menu.Hide();
            }
        }
    }));
}
