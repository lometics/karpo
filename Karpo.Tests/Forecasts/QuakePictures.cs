// quakes: the earthquakes drawn (docs\widgets.md §240). The face on the bar at every magnitude's class,
// at its three widths, on a quiet day, before a place is set, while nothing has come, gone stale, at
// 150 % and down a bar on its side, dark and light (quake-faces-<dark|light>.png); and the line at one
// to eight rows from Zürich, a busy week and a quiet one (quake-lines(-quiet)(-light).png). The sheets
// go to KARPO_PNG_DIR (else %TEMP%\karpo-tests\pictures) before anything is asserted. Runs only with
// KARPO_UI_TESTS=1, like the other picture tests.
//
// What is held: a face's width is its settings' and never the reading's, its ink stays inside its
// cell, and the line lists as many rows as its height holds.

using Karpo.Dock;
using Karpo.Tests.Folders;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using static Karpo.Tests.Islands.Offscreen;

namespace Karpo.Tests.Forecasts;

public sealed unsafe class QuakePictures
{
    private static Look LookFor(bool dark) => ListFormPictures.LookFor(dark);

    /// <summary>A week of earthquakes round Zürich, newest first: one of each class, and one far off.</summary>
    internal static QuakeHeld Week(double newest = 4.8, DateTime? fetched = null, string? problem = null)
    {
        var now = Clock.Now().ToUniversalTime();
        var feed = SavedQuakes.Feed(
            ("n1", newest, 46.5, 9.9, now.AddMinutes(-42), null),
            ("n2", 3.1, 45.9, 6.9, now.AddHours(-5), null),
            ("n3", 5.6, 42.3, 13.4, now.AddHours(-30), null),
            ("n4", 6.4, 38.1, 20.9, now.AddDays(-2), null),
            ("n5", 4.1, 47.8, 7.6, now.AddDays(-3), null),
            ("n6", 7.3, 40.2, 27.1, now.AddDays(-4), null),
            ("n7", 2.6, 47.1, 8.2, now.AddDays(-5), null),
            ("n8", 8.1, 36.5, 22.0, now.AddDays(-6), null));
        return new QuakeHeld { Quakes = QuakeFeeds.Parse(feed), FetchedUtc = fetched ?? now.AddMinutes(-3), Problem = problem };
    }

    private static BarItem Folder(string width = QuakesFace.Standard, bool place = true, string radius = "km2000", string magnitude = "m25")
    {
        var island = BarKind.FolderList.New();
        island.Id = "quakes";
        var face = QuakesFace.New();
        face.Weather = place ? SavedQuakes.Zurich() : new WeatherOptions();
        face.Set(FolderFaces.Quakes.Option(QuakesFace.Width)!, width);
        face.Set(FolderFaces.Quakes.Option(QuakesFace.Radius)!, radius);
        face.Set(FolderFaces.Quakes.Option(QuakesFace.Magnitude)!, magnitude);
        island.Folder!.Face = face;
        island.Normalize();
        return island;
    }

    private static FolderIsland Make(Gfx gfx, BarItem folder, QuakeHeld? stand)
    {
        FolderIsland.QuakeStand = stand ?? QuakeHeld.None;
        try
        {
            return new FolderIsland(Config.IslandKey(null, folder.Id), folder, gfx, new Config());
        }
        finally
        {
            FolderIsland.QuakeStand = null;
        }
    }

    private sealed record Shot(string Caption, BarItem Folder, QuakeHeld? Stand, float Scale = 1f, bool Vertical = false);

    [UiFact]
    public void TheFaceIsDrawnAtEveryClassAsWideAsItReads()
    {
        var failures = new List<string>();
        SavedWeather.AtAFixedTime(() => OnStaThread(gfx =>
        {
            var shots = new List<Shot>();
            foreach (var width in new[] { QuakesFace.Compact, QuakesFace.Standard, QuakesFace.Wide })
            {
                foreach (var magnitude in new[] { 2.7, 4.8, 5.6, 6.4, 7.3, 8.2 })
                {
                    shots.Add(new($"{width}, M{magnitude}", Folder(width), Week(magnitude)));
                }
                shots.Add(new($"{width}, a quiet day", Folder(width, magnitude: "m7"), Week()));
            }
            shots.Add(new("No place", Folder(place: false), null));
            shots.Add(new("Waiting", Folder(), null));
            shots.Add(new("Stale", Folder(), Week(fetched: Clock.Now().ToUniversalTime().AddHours(-2))));
            shots.Add(new("Worldwide, wide", Folder(QuakesFace.Wide, radius: "world"), Week(7.3)));
            shots.Add(new("150 %, wide", Folder(QuakesFace.Wide), Week(), 1.5f));
            shots.Add(new("150 %, standard", Folder(), Week(6.4), 1.5f));
            shots.Add(new("On its side", Folder(), Week(5.6), 1.5f, Vertical: true));
            shots.Add(new("On its side, quiet", Folder(magnitude: "m7"), Week(), 1.5f, Vertical: true));
            foreach (var dark in new[] { true, false })
            {
                Faces(gfx, shots, LookFor(dark), Path.Combine(Pictures, $"quake-faces-{(dark ? "dark" : "light")}.png"), failures);
            }

            // As wide as what it reads (docs\widgets.md §244): an earthquake, whatever its magnitude, one
            // width (tabular figures); nothing read yet — the dash — the narrowest.
            foreach (var width in new[] { QuakesFace.Compact, QuakesFace.Standard, QuakesFace.Wide })
            {
                var style = new IslandStyle(1.5f, false, 72, 36, LookFor(true));
                var sizes = new List<float>();
                foreach (var stand in new QuakeHeld?[] { Week(2.7), Week(8.8), Week(), null })
                {
                    using var island = Make(gfx, Folder(width), stand);
                    sizes.Add(island.CellSize(0, style).Width);
                }
                foreach (var stand in new QuakeHeld?[] { Week() })
                {
                    using var island = Make(gfx, Folder(width, magnitude: "m7"), stand);
                    sizes.Add(island.CellSize(0, style).Width);
                }
                if (sizes[0] != sizes[1] || sizes[1] != sizes[2] || sizes[3] != sizes.Min())
                {
                    failures.Add($"{width}: {string.Join(" / ", sizes)} px for its readings");
                }
            }
            {
                var style = new IslandStyle(1f, false, 48, 24, LookFor(true));
                float Width(string width)
                {
                    using var island = Make(gfx, Folder(width), Week());
                    return island.CellSize(0, style).Width;
                }
                Assert.True(Width(QuakesFace.Compact) < Width(QuakesFace.Standard) && Width(QuakesFace.Standard) < Width(QuakesFace.Wide));
            }

            // What the face says and its tooltip.
            using (var face = Make(gfx, Folder(QuakesFace.Wide), Week()))
            {
                var style = new IslandStyle(1f, false, 48, 24, LookFor(true));
                var context = new FaceContext(gfx, style, face.Options, face);
                Assert.StartsWith("Zürich · Within 2,000 km\nM4.8 · ", FaceDrawings.Of(face.Options.Face).Tooltip(context));
                Assert.StartsWith("Ready|n1|M4.8|141 km SE", FaceDrawings.Of(face.Options.Face).Reading(context));
            }
            using (var none = Make(gfx, Folder(place: false), null))
            {
                var style = new IslandStyle(1f, false, 48, 24, LookFor(true));
                var context = new FaceContext(gfx, style, none.Options, none);
                Assert.Equal("unset", FaceDrawings.Of(none.Options.Face).Reading(context));
                Assert.Null(FaceDrawings.Of(none.Options.Face).Tooltip(context));
            }
        }));
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [UiFact]
    public void TheLineListsWhatItsHeightHolds()
    {
        SavedWeather.AtAFixedTime(() => OnStaThread(gfx =>
        {
            QuakesLine Line(QuakeHeld held)
            {
                var item = ListKind.Quakes.New();
                item.Id = "quakes";
                item.Weather = SavedQuakes.Zurich();
                item.Weather.Quakes = new QuakeOptions { Radius = 2000, Magnitude = 2.5 };
                var tile = (QuakesLine)Karpo.Dock.Islands.CreateLine(Config.IslandKey("layout:default", "quakes"), item, ListKind.Quakes, gfx, new Config())!;
                tile.Source = () => held;
                return tile;
            }
            using var busy = Line(Week());
            using var quiet = Line(new QuakeHeld { Quakes = [], FetchedUtc = Clock.Now().ToUniversalTime().AddMinutes(-1) });
            foreach (var dark in new[] { true, false })
            {
                ListLinesTests.Sheet(gfx, busy, ListKind.Quakes, 1.5f, LookFor(dark), Path.Combine(Pictures, $"quake-lines{(dark ? "" : "-light")}.png"));
                ListLinesTests.Sheet(gfx, quiet, ListKind.Quakes, 1.5f, LookFor(dark), Path.Combine(Pictures, $"quake-lines-quiet{(dark ? "" : "-light")}.png"));
            }
            ListLinesTests.Sheet(gfx, busy, ListKind.Quakes, 1f, LookFor(true), Path.Combine(Pictures, "quake-lines-100.png"));

            var style = FolderFlyout.StyleFor(1.5f, LookFor(true), FlyoutTheme.For(LookFor(true)));
            QuakesPlan At(int rows) => busy.Plan(PopupPaint.Rect(0, 0, FolderFlyout.ListWidth(1.5f), LineRows.Px(rows, style)), style, busy.Read());
            Assert.True(At(1).One);
            Assert.False(At(2).One);
            var counts = Enumerable.Range(2, 7).Select(rows => At(rows).Rows.Count).ToList();
            Assert.True(counts.Zip(counts.Skip(1)).All(pair => pair.Second >= pair.First), string.Join(",", counts));
            // Eight rows: the heading and four quakes, a row with its caption three cells each (§442).
            Assert.True(counts[^1] >= 4, string.Join(",", counts));

            // A row's click opens its page once the folder has gone.
            string? opened = null;
            busy.Opened = link => opened = link;
            Assert.Equal(IslandView.App, busy.Click(2).View);
            busy.Open();
            Assert.Equal("https://earthquake.usgs.gov/earthquakes/eventpage/n2", opened);
            Assert.Equal(IslandView.None, busy.Click(99).View);

            // "Open on map" over an earthquake: that one, marked on the map chosen (§319); over the head,
            // USGS's map of them all.
            var box = PopupPaint.Rect(0, 0, FolderFlyout.ListWidth(1.5f), LineRows.Px(6, style));
            string? MapRow() => Assert.Single(Karpo.Dock.Dock.ActRows([WidgetAct.OpenMap], busy).Rows).On;
            busy.CardHover(new System.Numerics.Vector2(box.left + 4, box.top + 2), box, style, WidgetSurface.List);
            Assert.Equal(Karpo.Dock.Dock.QuakeMap, MapRow());
            var second = busy.Plan(box, style, busy.Read()).Rows[1];
            busy.CardHover(new System.Numerics.Vector2((second.left + second.right) / 2, (second.top + second.bottom) / 2), box, style, WidgetSurface.List);
            var quake = busy.Read().Rows[1].Quake;
            Assert.Same(quake, busy.MenuQuake);
            Assert.Equal(LinkProviders.Place(quake.Latitude, quake.Longitude, Karpo.Dock.Dock.QuakeZoom, quake.Place), MapRow());
        }));
    }

    /// <summary>Each shot's folder drawn on the bar's ground under its caption; its ink held inside its cell.</summary>
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
                var island = Make(gfx, shot.Folder, shot.Stand);
                var thickness = 48 * shot.Scale;
                var style = new IslandStyle(shot.Scale, shot.Vertical, thickness, MathF.Round(thickness / 2), look);
                var (w, h) = island.CellSize(0, style);
                made.Add((shot, island, style, w, h));
                failures.AddRange(Inside(island, style, $"{shot.Caption} ({(look.Dark ? "dark" : "light")})"));
            }
            var column = made.Max(item => MathF.Max(item.W, 220)) + pad;
            var rows = (made.Count + across - 1) / across;
            var rowHeight = made.Max(item => item.H) + label + pad;
            Bitmap(pad + (across * column), pad + (rows * rowHeight), dc =>
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
                    Shifted(dc, x, top, () => island.PaintCell(drawn, 0, cellStyle, w, h));
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
