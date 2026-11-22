// tides: the tides drawn (docs\widgets.md §236). The face on the bar in its four forms, before a place
// is set, while nothing has come, off the sea, gone stale, at 100 % and 150 % and down a bar on its
// side, dark and light (tides-faces-<dark|light>.png); the tides now and the tide graph at their sizes are
// TidesSunSplitTests' (§603). The sheets go to KARPO_PNG_DIR (else
// %TEMP%\karpo-tests\pictures) before anything is asserted. Runs only with KARPO_UI_TESTS=1, like the
// other picture tests.
//
// What is held: a face's width is its settings' and never the reading's, and its ink stays inside its cell.

using Karpo.Dock;
using Karpo.Tests.Folders;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using static Karpo.Tests.Islands.Offscreen;

namespace Karpo.Tests.Forecasts;

public sealed unsafe class TidesPictures
{
    private static Look LookFor(bool dark) => ListFormPictures.LookFor(dark);

    /// <summary>A folder wearing the tides face, set to <paramref name="show"/>, for Brest or for no place.</summary>
    private static BarItem Folder(string show = TidesFace.Next, bool place = true)
    {
        var island = BarKind.FolderList.New();
        island.Id = "tides";
        var face = TidesFace.New();
        face.Weather = place ? SavedTides.Options() : new WeatherOptions();
        if (show != TidesFace.Next)
        {
            face.Set(FolderFaces.Tides.Option(TidesFace.Show)!, show);
        }
        island.Folder!.Face = face;
        island.Normalize();
        return island;
    }

    private static FolderIsland Make(Gfx gfx, BarItem folder, TideCache? stand)
    {
        FolderIsland.TidesStand = stand;
        try
        {
            return new FolderIsland(Config.IslandKey(null, folder.Id), folder, gfx, new Config());
        }
        finally
        {
            FolderIsland.TidesStand = null;
        }
    }

    private sealed record Shot(string Caption, BarItem Folder, TideCache? Stand, float Scale = 1f, bool Vertical = false);

    private static readonly string[] Shows = [TidesFace.Next, TidesFace.NextHeight, TidesFace.Countdown, TidesFace.Level];

    [UiFact]
    public void TheFaceIsDrawnInEveryFormAsWideAsItReads()
    {
        var failures = new List<string>();
        OnStaThread(gfx => SavedWeather.AtAFixedTime(() =>
        {
            var shots = new List<Shot>();
            foreach (var show in Shows)
            {
                shots.Add(new($"{show}", Folder(show), SavedTides.Cache()));
                shots.Add(new($"{show}, 150 %", Folder(show), SavedTides.Cache(), 1.5f));
            }
            shots.Add(new("No place", Folder(place: false), null));
            shots.Add(new("Waiting", Folder(), null));
            shots.Add(new("Inland", Folder(), SavedTides.Cache(inland: true)));
            shots.Add(new("Stale", Folder(TidesFace.NextHeight), SavedTides.Cache(fetchedUtc: Clock.Now().ToUniversalTime().AddDays(-1))));
            shots.Add(new("On its side", Folder(), SavedTides.Cache(), 1.5f, Vertical: true));
            shots.Add(new("On its side, level", Folder(TidesFace.Level), SavedTides.Cache(), 1.5f, Vertical: true));
            foreach (var dark in new[] { true, false })
            {
                Faces(gfx, shots, LookFor(dark), Path.Combine(Pictures, $"tides-faces-{(dark ? "dark" : "light")}.png"), failures);
            }

            // As wide as what it reads (docs\widgets.md §244): nothing yet and off the sea — a dash — one
            // width, and no wider than the sea's reading.
            foreach (var show in Shows)
            {
                var style = new IslandStyle(1.5f, false, 72, 36, LookFor(true));
                var sizes = new List<float>();
                foreach (var stand in new[] { SavedTides.Cache(), null, SavedTides.Cache(inland: true) })
                {
                    using var island = Make(gfx, Folder(show), stand);
                    sizes.Add(island.CellSize(0, style).Width);
                }
                if (sizes[1] != sizes[2] || sizes[1] > sizes[0])
                {
                    failures.Add($"{show}: {string.Join(" / ", sizes)} px for its readings");
                }
            }

            using (var face = Make(gfx, Folder(TidesFace.NextHeight), SavedTides.Cache()))
            {
                var style = new IslandStyle(1f, false, 48, 24, LookFor(true));
                var context = new FaceContext(gfx, style, face.Options, face);
                Assert.StartsWith("Brest\n", FaceDrawings.Of(face.Options.Face).Tooltip(context));
                Assert.StartsWith("Ready|True|High ", FaceDrawings.Of(face.Options.Face).Reading(context));
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
