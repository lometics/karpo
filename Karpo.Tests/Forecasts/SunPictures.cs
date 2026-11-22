// sun: the sun drawn (docs\widgets.md §368). The face on the bar in its three forms at 100 % and 150 %,
// inside the polar circles, before a place is set, while this PC's position is not known, and down a bar
// on its side, dark and light (sun-faces-<dark|light>.png); the day bar at one to eight rows, Zürich on an
// autumn afternoon (sun-dayBar(-light|-100).png; the arc at its sizes is TidesSunSplitTests'), Tromsø at midsummer and midwinter
// (sun-arc-polar-day.png, sun-dayBar-polar-night.png); and the times, a widget each (§404), under the arc
// on that afternoon and evening (sun-times-<dark|light>-<afternoon|evening>.png). The sheets go to
// KARPO_PNG_DIR (else %TEMP%\karpo-tests\pictures) before anything is asserted. Runs only with
// KARPO_UI_TESTS=1, like the other picture tests.
//
// What is held: a face's width is its settings' and never more, its ink stays inside its cell; and each style's plan at
// its size (§603) — the arc its picture over the strip of its times at both, the day bar its next event on a row at
// Compact and its bar over the strip with how soon at Full.

using Karpo.Dock;
using Karpo.Tests.Folders;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using static Karpo.Tests.Islands.Offscreen;

namespace Karpo.Tests.Forecasts;

public sealed unsafe class SunPictures
{
    private static Look LookFor(bool dark) => ListFormPictures.LookFor(dark);

    /// <summary>Zürich on an autumn afternoon: the sun low and setting soon.</summary>
    private static readonly DateTime Autumn = new(2026, 10, 26, 15, 40, 0, DateTimeKind.Local);

    private static readonly DateTime Midsummer = new(2026, 6, 21, 14, 0, 0, DateTimeKind.Local);

    private static readonly DateTime Midwinter = new(2026, 12, 21, 11, 0, 0, DateTimeKind.Local);

    private static WeatherOptions Zurich() => new() { Place = "Zürich, Zurich, Switzerland", Latitude = 47.3769, Longitude = 8.5417 };

    private static WeatherOptions Tromso() => new() { Place = "Tromsø, Troms, Norway", Latitude = 69.6492, Longitude = 18.9553 };

    /// <summary>Runs <paramref name="body"/> with the clock at <paramref name="at"/>.</summary>
    private static void At(DateTime at, Action body)
    {
        var was = Clock.Now;
        Clock.Now = () => at;
        try
        {
            body();
        }
        finally
        {
            Clock.Now = was;
        }
    }

    /// <summary>A folder wearing the sun face, set to <paramref name="show"/>, for <paramref name="place"/>.</summary>
    private static BarItem Folder(string show, WeatherOptions place)
    {
        var island = BarKind.FolderList.New();
        island.Id = "sun";
        var face = SunFace.New();
        face.Weather = place;
        if (show != SunFace.Next)
        {
            face.Set(FolderFaces.Sun.Option(SunFace.Show)!, show);
        }
        island.Folder!.Face = face;
        island.Normalize();
        return island;
    }

    private static FolderIsland Make(Gfx gfx, BarItem folder) => new(Config.IslandKey(null, folder.Id), folder, gfx, new Config());

    private sealed record Shot(string Caption, BarItem Folder, DateTime When, float Scale = 1f, bool Vertical = false);

    private static readonly string[] Shows = [SunFace.Next, SunFace.Named, SunFace.Countdown];

    [UiFact]
    public void TheFaceIsDrawnInEveryFormAsWideAsItReads()
    {
        SunPlaces.Forget();
        var failures = new List<string>();
        OnStaThread(gfx =>
        {
            var shots = new List<Shot>();
            foreach (var show in Shows)
            {
                shots.Add(new($"{show}", Folder(show, Zurich()), Autumn));
                shots.Add(new($"{show}, 150 %", Folder(show, Zurich()), Autumn, 1.5f));
            }
            shots.Add(new("Sunrise next, 150 %", Folder(SunFace.Named, Zurich()), new DateTime(2026, 10, 26, 22, 0, 0, DateTimeKind.Local), 1.5f));
            shots.Add(new("Polar day", Folder(SunFace.Named, Tromso()), Midsummer));
            shots.Add(new("Polar night", Folder(SunFace.Countdown, Tromso()), Midwinter));
            shots.Add(new("No place", Folder(SunFace.Next, new WeatherOptions()), Autumn));
            shots.Add(new("This PC, not found", Folder(SunFace.Next, WeatherOptions.New()), Autumn));
            shots.Add(new("On its side", Folder(SunFace.Next, Zurich()), Autumn, 1.5f, Vertical: true));
            foreach (var dark in new[] { true, false })
            {
                Faces(gfx, shots, LookFor(dark), Path.Combine(Pictures, $"sun-faces-{(dark ? "dark" : "light")}.png"), failures);
            }

            // As wide as what it reads (§244), never past its room: the polar words are in the room.
            foreach (var show in Shows)
            {
                var style = new IslandStyle(1.5f, false, 72, 36, LookFor(true));
                var sizes = new List<float>();
                foreach (var (place, when) in new[] { (Zurich(), Autumn), (Tromso(), Midsummer), (Tromso(), Midwinter) })
                {
                    At(when, () =>
                    {
                        using var island = Make(gfx, Folder(show, place));
                        sizes.Add(island.CellSize(0, style).Width);
                    });
                }
                var context = new FaceContext(gfx, style, new FolderOptions(), null, Worn: Folder(show, Zurich()).Folder!.Face);
                var most = SunFaceDrawing.Room(gfx, context.Face, context.Fit.Primary) + context.Fit.Glyph() + context.Fit.Gap;
                if (sizes.Any(size => size > most + 40))
                {
                    failures.Add($"{show}: {string.Join(" / ", sizes)} px for its readings, its room {most}");
                }
            }

            At(Autumn, () =>
            {
                using var face = Make(gfx, Folder(SunFace.Named, Zurich()));
                var style = new IslandStyle(1f, false, 48, 24, LookFor(true));
                var context = new FaceContext(gfx, style, face.Options, face);
                var drawing = FaceDrawings.Of(face.Options.Face);
                Assert.StartsWith("Zürich\nSunset ", drawing.Tooltip(context));
                Assert.Contains("Daylight 10 h", drawing.Tooltip(context));
                Assert.StartsWith(SunText.Sunset + "|Sunset ", drawing.Reading(context));
            });
            At(Midsummer, () =>
            {
                using var face = Make(gfx, Folder(SunFace.Next, Tromso()));
                var context = new FaceContext(gfx, new IslandStyle(1f, false, 48, 24, LookFor(true)), face.Options, face);
                Assert.Equal(SunText.Sun + "|Polar day", FaceDrawings.Of(face.Options.Face).Reading(context));
            });
            using (var none = Make(gfx, Folder(SunFace.Next, new WeatherOptions())))
            {
                var context = new FaceContext(gfx, new IslandStyle(1f, false, 48, 24, LookFor(true)), none.Options, none);
                Assert.Equal("unset", FaceDrawings.Of(none.Options.Face).Reading(context));
                Assert.Null(FaceDrawings.Of(none.Options.Face).Tooltip(context));
            }
        });
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    private static SunTile Line(Gfx gfx, ListKind kind, WeatherOptions place, LineSize? size = null)
    {
        var item = kind.New();
        item.Id = "sun";
        item.Weather = place;
        item.Size = size;
        item.Normalize();
        return (SunTile)Karpo.Dock.Islands.CreateLine(Config.IslandKey("layout:default", "sun"), item, kind, gfx, new Config())!;
    }

    [UiFact]
    public void EachStyleDrawsItsSize()
    {
        OnStaThread(gfx =>
        {
            At(Autumn, () =>
            {
                using var line = Line(gfx, ListKind.SunDayBar, Zurich());
                foreach (var dark in new[] { true, false })
                {
                    ListLinesTests.Sheet(gfx, line, ListKind.SunDayBar, 1.5f, LookFor(dark), Path.Combine(Pictures, $"sun-dayBar{(dark ? "" : "-light")}.png"));
                }
                ListLinesTests.Sheet(gfx, line, ListKind.SunDayBar, 1f, LookFor(true), Path.Combine(Pictures, "sun-dayBar-100.png"));
            });
            At(Midsummer, () =>
            {
                using var line = Line(gfx, ListKind.SunArc, Tromso());
                ListLinesTests.Sheet(gfx, line, ListKind.SunArc, 1.5f, LookFor(true), Path.Combine(Pictures, "sun-arc-polar-day.png"));
            });
            At(Midwinter, () =>
            {
                using var line = Line(gfx, ListKind.SunDayBar, Tromso());
                ListLinesTests.Sheet(gfx, line, ListKind.SunDayBar, 1.5f, LookFor(true), Path.Combine(Pictures, "sun-dayBar-polar-night.png"));
            });

            // §603: each style's plan is its size's, the picture taking what the strip leaves of the line, which the strip
            // ends — the arc at both sizes with its times alone, the day bar's Compact its one row ([S-170]).
            var style = FolderFlyout.StyleFor(1.5f, LookFor(true), FlyoutTheme.For(LookFor(true)));
            var width = FolderFlyout.ListWidth(1.5f);
            foreach (var kind in new[] { ListKind.SunArc, ListKind.SunDayBar })
            {
                foreach (var size in new[] { LineSize.Compact, LineSize.Full })
                {
                    using var line = Line(gfx, kind, Zurich(), size);
                    var height = line.ListHeight(width, style);
                    var plan = line.Plan(PopupPaint.Rect(0, 0, width, height), style);
                    if (kind == ListKind.SunDayBar && size == LineSize.Compact)
                    {
                        Assert.True(plan.One);
                        Assert.Equal(LineRow.Height(style, caption: false), height, 0.5f);
                        continue;
                    }
                    Assert.False(plan.One, $"{kind} {size}");
                    Assert.Equal(kind == ListKind.SunDayBar, plan.StripCountdown);
                    Assert.Equal(height, plan.GraphicHeight + plan.StripHeight, 0.5f);
                    Assert.True(plan.GraphicHeight >= style.Px(46) - 0.5f, $"{kind} {size}: the picture {plan.GraphicHeight} under its least");
                }
            }
        });
    }

    /// <summary>
    /// §657: the day bar is always the clock's 24 hours, midnight to midnight, noon in its middle — on an ordinary
    /// day, just after midnight (before the place's solar day has turned), on the day the clocks go forward, for a
    /// place in another zone (New York, Tokyo: its daylight across this PC's midnight), and in polar day and night.
    /// sun-dayBar-24h-&lt;dark|light&gt;.png at 150 %, Full, each shot on this PC's zone (W. Europe).
    /// </summary>
    [UiFact]
    public void TheDayBarIsAlwaysTwentyFourHoursPictured()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");
        var shots = new (string Caption, WeatherOptions Place, DateTime When)[]
        {
            ("Zürich, Nov 10, 14:00", Zurich(), new(2026, 11, 10, 14, 0, 0)),
            ("Zürich, Nov 10, 00:10", Zurich(), new(2026, 11, 10, 0, 10, 0)),
            ("Zürich, Mar 29 (23 h), 13:30", Zurich(), new(2026, 3, 29, 13, 30, 0)),
            ("New York, Nov 10, 03:00 here", new() { Place = "New York, United States", Latitude = 40.7143, Longitude = -74.006 }, new(2026, 11, 10, 3, 0, 0)),
            ("Tokyo, Nov 10, 12:00 here", new() { Place = "Tokyo, Japan", Latitude = 35.6762, Longitude = 139.6503 }, new(2026, 11, 10, 12, 0, 0)),
            ("Tromsø, midsummer", Tromso(), Midsummer),
            ("Tromsø, midwinter", Tromso(), Midwinter),
        };
        OnStaThread(gfx =>
        {
            foreach (var dark in new[] { true, false })
            {
                const float scale = 1.5f;
                var look = LookFor(dark);
                var style = FolderFlyout.StyleFor(scale, look, FlyoutTheme.For(look));
                var theme = FlyoutTheme.For(look);
                var width = FolderFlyout.ListWidth(scale);
                var pad = PopupPaint.Round(16 * scale);
                var label = PopupPaint.Round(11 * scale);
                var labelLine = TilePaint.LineHeight(gfx, label);
                var lines = shots.Select(shot =>
                {
                    var line = Line(gfx, ListKind.SunDayBar, shot.Place, LineSize.Full);
                    line.Zone = zone;
                    line.Stand = (shot.Place.Latitude!.Value, shot.Place.Longitude!.Value);
                    return line;
                }).ToList();
                try
                {
                    var height = lines[0].ListHeight(width, style);
                    const int across = 4;
                    var rows = (shots.Length + across - 1) / across;
                    var sheetWidth = pad + (across * (width + pad));
                    var sheetHeight = pad + (rows * (labelLine + height + pad));
                    Bitmap(sheetWidth, sheetHeight, dc =>
                    {
                        PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, sheetWidth, sheetHeight), 0,
                            PopupPaint.Ink(dark ? Windows.UI.Color.FromArgb(255, 0x1C, 0x1C, 0x1C) : Windows.UI.Color.FromArgb(255, 0xE8, 0xE8, 0xE8)));
                        for (var index = 0; index < shots.Length; index++)
                        {
                            var x = pad + (index % across * (width + pad));
                            var y = pad + (index / across * (labelLine + height + pad));
                            PopupPaint.Text(dc, gfx, shots[index].Caption, label, Weight.Regular, theme.TextSecondary, x, y);
                            var plate = PopupPaint.Rect(x, y + labelLine, width, height);
                            PopupPaint.Fill(dc, plate, PopupPaint.Round(8 * scale), PopupPaint.Ink(theme.Base));
                            PopupPaint.Stroke(dc, plate, PopupPaint.Round(8 * scale), MathF.Max(1, MathF.Round(scale)), PopupPaint.Ink(theme.Stroke));
                            var context = dc;
                            var line = lines[index];
                            At(shots[index].When, () => PopupPaint.Clipped(context, plate, PopupPaint.Round(8 * scale), () => Shifted(context, plate.left, plate.top, () =>
                                FolderFlyout.PaintContent(context, line, PopupPaint.Rect(0, 0, width, height), style))));
                        }
                    }, bitmap => Save(bitmap, Path.Combine(Pictures, $"sun-dayBar-24h-{(dark ? "dark" : "light")}.png")));
                }
                finally
                {
                    foreach (var line in lines)
                    {
                        line.Dispose();
                    }
                }
            }
        });
    }

    /// <summary>
    /// §404: the times, each a widget of its own, under the arc as the ready-made folder has them — sunrise, sunset,
    /// the daylight, solar noon, dawn and dusk —, Zürich on an autumn afternoon (the sun up: "1 h 37 min left") and
    /// evening (down: the change in words), at 150 %, dark and light: sun-times-&lt;dark|light&gt;.png.
    /// </summary>
    [UiFact]
    public void TheTimesArePictured()
    {
        ListKind[] kinds = [ListKind.SunArc, ListKind.Sunrise, ListKind.Sunset, ListKind.Daylight, ListKind.SolarNoon, ListKind.Dawn, ListKind.Dusk];
        foreach (var dark in new[] { true, false })
        {
            foreach (var (caption, when) in new[] { ("Afternoon, 15:40", Autumn), ("Evening, 19:00", Autumn.Date.AddHours(19)) })
            {
                ListGutterTests.StoodIn<Gfx>(when, OnStaThread, gfx =>
                {
                    var look = LookFor(dark);
                    var folder = ListFormPictures.Folder(gfx, ListGutterTests.Before, [.. kinds.Select(kind =>
                    {
                        var item = ListGutterTests.Made(kind);
                        item.Weather = Zurich();
                        return item;
                    })]);
                    try
                    {
                        ListGutterTests.Ready(folder);
                        var (_, plan) = ListFormPictures.Plan(folder, look, 1.5f);
                        ListFormPictures.Sheet(gfx, $"sun-times-{(dark ? "dark" : "light")}-{(when.Hour < 18 ? "afternoon" : "evening")}.png", 1.5f, [(caption, look, folder, plan)]);
                    }
                    finally
                    {
                        folder.Closed();
                        folder.Dispose();
                    }
                });
            }
        }
    }

    /// <summary>Each shot's folder drawn on the bar's ground under its caption, at its own time; its ink held inside its cell.</summary>
    private static void Faces(Gfx gfx, IReadOnlyList<Shot> shots, Look look, string path, List<string> failures)
    {
        const float pad = 14;
        const float label = 18;
        const int across = 3;
        var made = new List<(Shot Shot, FolderIsland Island, IslandStyle Style, float W, float H)>();
        try
        {
            foreach (var shot in shots)
            {
                At(shot.When, () =>
                {
                    var island = Make(gfx, shot.Folder);
                    var thickness = 48 * shot.Scale;
                    var style = new IslandStyle(shot.Scale, shot.Vertical, thickness, MathF.Round(thickness / 2), look);
                    var (w, h) = island.CellSize(0, style);
                    made.Add((shot, island, style, w, h));
                    failures.AddRange(Inside(island, style, $"{shot.Caption} ({(look.Dark ? "dark" : "light")})"));
                });
            }
            var column = made.Max(item => MathF.Max(item.W, 220)) + pad;
            var rows = (made.Count + across - 1) / across;
            var rowHeight = made.Max(item => item.H) + label + pad;
            var width = pad + (across * column);
            var height = pad + (rows * rowHeight);
            Bitmap(width, height, dc =>
            {
                var ground = look.Dark ? new DXGI_RGBA(0x20 / 255f, 0x20 / 255f, 0x20 / 255f, 1) : new DXGI_RGBA(0xEE / 255f, 0xEE / 255f, 0xEE / 255f, 1);
                dc->Clear(&ground);
                var caption = look.Dark ? new DXGI_RGBA(1, 1, 1, 0.6f) : new DXGI_RGBA(0, 0, 0, 0.6f);
                for (var index = 0; index < made.Count; index++)
                {
                    var (shot, island, style, w, h) = made[index];
                    var x = pad + ((index % across) * column);
                    var y = pad + ((index / across) * rowHeight);
                    PopupPaint.Text(dc, gfx, shot.Caption, 12, Weight.Regular, caption, x, y);
                    var top = y + label;
                    PopupPaint.Stroke(dc, PopupPaint.Rect(x - 1, top - 1, w + 2, h + 2), 2, 1, look.Dark ? new DXGI_RGBA(1, 1, 1, 0.12f) : new DXGI_RGBA(0, 0, 0, 0.12f));
                    var drawn = dc;
                    var cellStyle = style;
                    At(shot.When, () => Shifted(drawn, x, top, () => island.PaintCell(drawn, 0, cellStyle, w, h)));
                }
            }, bitmap => Save(bitmap, path));
        }
        finally
        {
            foreach (var item in made)
            {
                item.Island.Dispose();
            }
        }
    }

    /// <summary>Where the face's ink is drawn into its cell: inside it.</summary>
    private static List<string> Inside(FolderIsland island, IslandStyle style, string where)
    {
        var failures = new List<string>();
        island.Forget();
        var (width, height) = island.CellSize(0, style);
        const float margin = 12f;
        (float Left, float Top, float Right, float Bottom) ink = default;
        Bitmap(
            width + (2 * margin),
            height + (2 * margin),
            dc => Shifted(dc, margin, margin, () => island.PaintCell(dc, 0, style, width, height)),
            bitmap => ink = InkOf(bitmap, (uint)MathF.Ceiling(width + (2 * margin)), (uint)MathF.Ceiling(height + (2 * margin))));
        var (left, top, right, bottom) = (ink.Left - margin, ink.Top - margin, ink.Right - margin, ink.Bottom - margin);
        if (right <= left)
        {
            failures.Add($"{where}: draws nothing");
        }
        else if (left < -0.5f || top < -0.5f || right > width + 0.5f || bottom > height + 0.5f)
        {
            failures.Add($"{where}: draws {left:0.#},{top:0.#}-{right:0.#},{bottom:0.#} outside its {width:0.#} by {height:0.#} cell");
        }
        return failures;
    }
}
