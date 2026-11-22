// §497 (the person, 2026-11-03: "sunrise widget and sunset widget and other widgets in this category should also
// show the more detailed information in the compact view as well. i think it could fit on the right side still.
// (e.g. in how much time the event is.)"): a row's detail (RowSpec.Detail) — how soon, how much is left — stands
// under its name where the row is tall enough for a caption, and at its end, before its reading, in the secondary
// ink where it is one row; where the row is short the detail goes first, the name kept whole. One rule of the kit
// (LineRow), worn by the sun's times, the tides and the alarm.
//
// Everything here measures text: KARPO_UI_TESTS=1. The sheet goes to KARPO_PNG_DIR as compact-details-<dark|light>.png.

using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Forecasts;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using static Karpo.Tests.Islands.Offscreen;

namespace Karpo.Tests.Folders;

public sealed unsafe class RowDetailTests
{
    private const string Rise = "Sunrise";

    private const string Soon = "in 2 h 15 min";

    private static TileStyle StyleAt(float scale, bool dark = true)
    {
        var look = ListFormPictures.LookFor(dark);
        return FolderFlyout.StyleFor(scale, look, FlyoutTheme.For(look));
    }

    private static RowSpec Row => new(Rise) { Glyph = "", Detail = Soon, Reading = "07:28", Look = RowReading.Body };

    private static LinePiece? Piece(LineSketch sketch, string name) =>
        sketch.Pieces.Where(piece => piece.Name == name).Select(piece => (LinePiece?)piece).FirstOrDefault();

    /// <summary>
    /// One row: the detail at its end, ending a gap short of the reading, after the name, on the row's middle, and no
    /// caption. Taller: the detail is the caption under the name, and the row is a captioned row's height.
    /// </summary>
    [UiFact]
    public void TheDetailStandsAtTheEndOfOneRowAndUnderTheNameOfATallerOne()
    {
        OnStaThread(gfx =>
        {
            foreach (var scale in new[] { 1f, 1.5f })
            {
                var style = StyleAt(scale);
                var width = FolderFlyout.ListWidth(scale);
                var gap = style.Px(LineSpace.GapDip);
                var at = $"{scale * 100:0} %";

                var one = new LineSketch(null, gfx);
                var laid = LineRow.Draw(one, 0, width, 0, style, Row, LineRow.Height(style, caption: false));
                var found = Piece(one, "detail");
                Assert.True(found is not null, $"{at}: one row shows no detail");
                var detail = found!.Value;
                Assert.Equal(Soon, detail.Text);
                var reading = laid.Reading!.Value;
                var name = laid.Name!.Value;
                Assert.True(detail.Box.right <= reading.left - gap + 0.5f, $"{at}: the detail ends at {detail.Box.right}, the reading starts at {reading.left}");
                Assert.True(detail.Box.left >= name.right + gap - 0.5f, $"{at}: the detail starts at {detail.Box.left}, over the name ending at {name.right}");
                Assert.True(MathF.Abs(((detail.Box.top + detail.Box.bottom) / 2) - laid.Middle) <= 1f, $"{at}: the detail stands off the row's middle");
                Assert.Null(laid.Caption);
                Assert.Null(Piece(one, "caption"));

                var tall = new LineSketch(null, gfx);
                Assert.Equal(LineRow.Height(style, caption: true), LineRow.Height(style, Row), 0.5f);
                var under = LineRow.Draw(tall, 0, width, 0, style, Row);
                Assert.Equal(Soon, Piece(tall, "caption")?.Text);
                Assert.True(under.Caption is { } caption && caption.top >= under.Name!.Value.bottom - 0.5f, $"{at}: the detail is not under the name");
                Assert.Null(Piece(tall, "detail"));
            }
        });
    }

    /// <summary>Where the row is short, the detail goes first: the name stays whole and the reading stays.</summary>
    [UiFact]
    public void WhereTheRowIsShortTheDetailGoesFirst()
    {
        OnStaThread(gfx =>
        {
            var style = StyleAt(1f);
            var form = ListForm.Of(gfx, style);
            var named = PopupPaint.Measure(gfx, Rise, form.TextPx, Weight.Regular).X;
            var sketch = new LineSketch(null, gfx);
            var laid = LineRow.Draw(sketch, 0, style.Px(180), 0, style, Row, LineRow.Height(style, caption: false));
            Assert.Null(Piece(sketch, "detail"));
            Assert.NotNull(laid.Reading);
            Assert.True(laid.Name is { } name && name.right - name.left >= named - 0.5f, "the name is cut while the detail is left out");
        });
    }

    /// <summary>
    /// The sun's times in their compact form (one row) say what their full form says under the name — how soon the
    /// sunrise and the sunset are, what is left of the daylight, how high the sun stands at noon — at the row's end.
    /// </summary>
    [UiFact]
    public void TheSunsCompactTimesSayTheirDetailAtTheEnd()
    {
        OnStaThread(gfx =>
        {
            var was = Clock.Now;
            Clock.Now = () => new DateTime(2026, 10, 26, 15, 40, 0, DateTimeKind.Local);
            try
            {
                var style = StyleAt(1.5f);
                var width = FolderFlyout.ListWidth(1.5f);
                foreach (var kind in new[] { ListKind.Sunrise, ListKind.Sunset, ListKind.Daylight, ListKind.SolarNoon })
                {
                    using var full = Sun(gfx, kind, LineSize.Full);
                    using var compact = Sun(gfx, kind, LineSize.Compact);
                    var tall = full.TimePieces(PopupPaint.Rect(0, 0, width, full.ListHeight(width, style)), style);
                    var said = tall.Single(piece => piece.Name == "caption").Text;
                    var height = compact.ListHeight(width, style);
                    Assert.Equal(LineRow.Height(style, caption: false), height, 0.5f);
                    var one = compact.TimePieces(PopupPaint.Rect(0, 0, width, height), style);
                    Assert.DoesNotContain(one, piece => piece.Name == "caption");
                    Assert.True(one.Any(piece => piece.Name == "detail" && piece.Text == said), $"{kind}: compact, no \"{said}\" at its end ({string.Join(", ", one.Select(piece => $"{piece.Name} '{piece.Text}'"))})");
                }
            }
            finally
            {
                Clock.Now = was;
            }
        });
    }

    /// <summary>The tides now at Compact, one row: the next turn and how soon at the end, before the height now (§603).</summary>
    [UiFact]
    public void TheTidesOnOneRowSayTheNextTurnAtTheEnd()
    {
        OnStaThread(gfx => SavedWeather.AtAFixedTime(() =>
        {
            var item = ListKind.Tides.New();
            item.Id = "tides";
            item.Weather = SavedTides.Options();
            item.Size = LineSize.Compact;
            using var tile = (TidesTile)Karpo.Dock.Islands.CreateLine(Config.IslandKey("layout:default", "tides"), item, ListKind.Tides, gfx, new Config())!;
            tile.Source = SavedTides.Source(SavedTides.Cache());
            tile.Imperial = false;
            var style = StyleAt(1.5f);
            var width = FolderFlyout.ListWidth(1.5f);
            var one = tile.RowPieces(PopupPaint.Rect(0, 0, width, LineRow.Height(style, caption: false)), style);
            var reading = tile.Read();
            var next = Assert.Single(one, piece => piece.Name == "next");
            Assert.Contains(reading.NextAtHeight(), next.Text, StringComparison.Ordinal);
            Assert.True(next.Box.left > Assert.Single(one, piece => piece.Name == "name").Box.right, "the next turn after the name");
            Assert.True(next.Box.right <= Assert.Single(one, piece => piece.Name == "height").Box.left, "the next turn before the height now");
        }));
    }

    /// <summary>A set alarm in its compact form: when it rings, at the end before its time; its full form keeps it under its name.</summary>
    [UiFact]
    public void ACompactAlarmSaysWhenItRingsAtTheEnd()
    {
        OnStaThread(gfx =>
        {
            var now = new DateTime(2026, 10, 12, 8, 0, 0, DateTimeKind.Utc);
            var countdowns = new Countdowns();
            var item = ListKind.Alarm.New();
            item.Id = "probe";
            item.Normalize();
            item.Alarm!.On = true;
            item.Alarm.Time = "06:30";
            item.Alarm.Normalize();
            countdowns.Alarms.Sync([("probe", item.Alarm)], now.AddMinutes(-2));
            using var tile = new AlarmTile(Config.IslandKey(null, "probe"), item, gfx, new Config(), countdowns, () => now,
                at => at.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture));
            var style = StyleAt(1.5f);
            var width = FolderFlyout.ListWidth(1.5f);
            var soon = tile.Secondary(tile.Status);
            var one = tile.LinePieces(PopupPaint.Rect(0, 0, width, LineRow.Height(style, caption: false)), style);
            Assert.Contains(one, piece => piece.Name == "detail" && piece.Text == soon);
            var tall = tile.LinePieces(PopupPaint.Rect(0, 0, width, LineRow.Height(style, caption: true)), style);
            Assert.DoesNotContain(tall, piece => piece.Name == "detail");
            Assert.Contains(tall, piece => piece.Name == "hint" && piece.Text.Contains(soon, StringComparison.Ordinal));
        });
    }

    /// <summary>The sun's times, the tides and an alarm, compact over full, dark and light: compact-details-&lt;dark|light&gt;.png.</summary>
    [UiFact]
    public void CompactDetailsSheet()
    {
        ListKind[] kinds = [ListKind.Sunrise, ListKind.Sunset, ListKind.Daylight, ListKind.SolarNoon, ListKind.Dawn, ListKind.Dusk, ListKind.Tides, ListKind.Alarm];
        foreach (var dark in new[] { true, false })
        {
            ListGutterTests.StoodIn<Gfx>(SavedWeather.Fixed, OnStaThread, gfx =>
            {
                var look = ListFormPictures.LookFor(dark);
                var panels = new List<(string Caption, Look Look, FolderIsland Folder, FolderPlan Plan)>();
                try
                {
                    foreach (var size in new[] { LineSize.Compact, LineSize.Full })
                    {
                        var folder = ListFormPictures.Folder(gfx, ListGutterTests.Before, [.. kinds.Select(kind =>
                        {
                            var item = ListGutterTests.Made(kind);
                            item.Size = size;
                            if (kind == ListKind.Alarm)
                            {
                                item.Alarm!.On = true;
                            }
                            return item;
                        })]);
                        ListGutterTests.Ready(folder);
                        var (_, plan) = ListFormPictures.Plan(folder, look, 1.5f);
                        panels.Add((size.ToString(), look, folder, plan));
                    }
                    ListFormPictures.Sheet(gfx, $"compact-details-{(dark ? "dark" : "light")}.png", 1.5f, panels);
                }
                finally
                {
                    foreach (var (_, _, folder, _) in panels)
                    {
                        folder.Closed();
                        folder.Dispose();
                    }
                }
            });
        }
    }

    /// <summary>A sun time's line in Zürich, at <paramref name="size"/>.</summary>
    private static SunTile Sun(Gfx gfx, ListKind kind, LineSize size)
    {
        var item = kind.New();
        item.Id = "sun";
        item.Weather = new WeatherOptions { Place = "Zürich, Zurich, Switzerland", Latitude = 47.3769, Longitude = 8.5417 };
        item.Size = size;
        item.Normalize();
        var tile = (SunTile)Karpo.Dock.Islands.CreateLine(Config.IslandKey("layout:default", "sun"), item, kind, gfx, new Config())!;
        tile.Stand = (47.3769, 8.5417);
        return tile;
    }
}
