// The person's bar (2026-10-22, docs\widgets.md §275): "some of the faces in the taskbar have space
// around them although they dont need it". Their folders as their config has them, on a 41 DIP bar,
// drawn by the dock's real scene on a desktop of its own at 150 % in the dark, to KARPO_PNG_DIR as
// face-slack-bar-dark-150.png, with face-slack-bar-150.txt beside it: each folder's button, each face's
// box and its ink. Held: every button is its faces, the room between them and its two ends, nothing more,
// and every face draws something (the keyboard face among the "Tides" folder's drew nothing, §277).
//
// The flyouts' lines are left out (they never size a face), and the air quality's place is a saved one.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using System.Globalization;
using System.Text;
using Karpo.Dock;
using Karpo.Dock.Native;
using Karpo.Dock.Performance;
using Karpo.Dock.QuickSettings.Services;
using Karpo.Tests.Folders;
using Karpo.Tests.Forecasts;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    /// <summary>The person's bar's folders and show desktop, as their config.json has them (2026-10-22; the "Tides" folder's faces as of §277).</summary>
    private const string PersonsFaces = """
        {
          "separators": { "shape": "dot", "space": 0 },
          "layouts": [ { "id": "default", "name": "Default", "type": "taskbar", "size": 41, "islands": [
            { "id": "fb6c6ad8", "kind": "folderList", "section": "middle", "folder": {
              "faces": [ { "kind": "power", "options": {} }, { "kind": "gpu", "options": {} } ], "face": { "kind": "power", "options": {} } } },
            { "id": "94cde9b9", "kind": "folderList", "section": "middle", "folder": {
              "faces": [ { "kind": "removable", "options": {} }, { "kind": "drive", "options": { "name": true } },
                { "kind": "airQuality", "options": { "width": "standard", "index": "us" },
                  "weather": { "place": "Zürich, Switzerland", "latitude": 47.37, "longitude": 8.54, "useDeviceLocation": false, "isSet": true } },
                { "kind": "icon", "options": {}, "glyph": "" } ], "face": { "kind": "removable", "options": {} } } },
            { "id": "295cee67", "kind": "folderList", "section": "middle", "folder": {
              "faces": [ { "kind": "tides", "options": { "show": "countdown" },
                  "weather": { "place": "Venice, Veneto, Italy", "latitude": 45.4371, "longitude": 12.3326, "useDeviceLocation": false, "isSet": true } },
                { "kind": "calendar", "options": {} }, { "kind": "keyboard", "options": {} }, { "kind": "desktops", "options": {} },
                { "kind": "clipboard", "options": {} } ],
              "face": { "kind": "tides", "options": { "show": "countdown" },
                "weather": { "place": "Venice, Veneto, Italy", "latitude": 45.4371, "longitude": 12.3326, "useDeviceLocation": false, "isSet": true } } } },
            { "id": "1563f251", "kind": "folderList", "section": "end", "folder": {
              "faces": [ { "kind": "wifi", "options": {} }, { "kind": "sound", "options": {} }, { "kind": "nowPlaying", "options": { "hideIdle": true, "art": true } } ],
              "face": { "kind": "wifi", "options": {} } } },
            { "id": "2d5256f7", "kind": "folderList", "section": "end", "folder": {
              "faces": [ { "kind": "dateTime", "options": { "seconds": "windows" } }, { "kind": "notifications", "options": { "count": false } } ],
              "face": { "kind": "dateTime", "options": { "seconds": "windows" } } } },
            { "id": "241081ce", "kind": "showDesktop", "section": "end" }
          ] } ],
          "defaultLayoutId": "default"
        }
        """;

    /// <summary>The person's config with their bar (<see cref="PersonsFaces"/>), at its thickness.</summary>
    internal static (Config Config, List<BarItem> Bar) PersonsBar()
    {
        var config = Config.Parse(PersonsFaces);
        var layout = config.Layouts[0];
        config.Height = layout.Size;
        return (config, layout.Islands);
    }

    [UiFact]
    public void ThePersonsBarIsItsFacesAndTheirPadding()
    {
        WindowShots.OnHiddenDesktop("face-slack", (gfx, _) =>
        {
            Clock.Now = () => new DateTime(2026, 10, 22, 21, 30, 0);
            WindowsClock.SecondsStand = false;
            IndicatorSources.Made = () => new SampleIndicators(new IndicatorState());
            FolderIsland.AirStand = SavedAir.Cache(32, 72);
            FolderIsland.TidesStand = SavedTides.Cache();
            FolderIsland.DevicesStand = [];
            // The graphics card at 5 %, in the room of "88%" (PerformanceIsland.FaceLength).
            FolderIsland.PerformanceSources = new DataSources((_, widget) =>
                new PerformanceData(widget.Info!.Metric!.Value, PerformanceData.DeviceOf(widget.Info.Metric.Value, widget.Performance ?? new PerformanceOptions())) { Reading = () => 0.05f });
            MediaIsland.Stand = new MediaStand(Title: "Clair de lune", Artist: "Claude Debussy", AppName: "Spotify", IsPlaying: true, Art: Karpo.Tests.QuickSettings.QuickFlyoutOffscreenTests.Cover());
            try
            {
                const uint dpi = 144;
                var (config, bar) = PersonsBar();
                var look = AddListPictures.LookFor(dark: true) with { Animations = false };
                var area = new RECT { left = 0, top = 0, right = 2560, bottom = (int)(dpi * 10) };
                var (specs, islands) = Made(gfx, config, bar);
                try
                {
                    var content = new Content { Islands = islands };
                    var layout = new Layout(gfx, dpi, area, area, Edge.Bottom, config, look, specs, content);
                    var failures = new List<string>();
                    var table = new StringBuilder();
                    table.AppendLine(CultureInfo.InvariantCulture, $"The person's bar at {dpi * 100 / 96} %, {config.Height} DIP: each folder's button, each face's box and its ink, in px from the button's start.");
                    for (var part = 0; part < specs.Count; part++)
                    {
                        if (!islands.TryGetValue(specs[part].Key, out var made) || made is not FolderIsland folder)
                        {
                            continue;
                        }
                        var style = layout.StyleFor(part, look);
                        var fit = new Fit(style);
                        var shown = folder.FacesOnBar;
                        var sizes = FolderIsland.FaceSizes(gfx, folder.Options, style, faces: shown, folder: folder);
                        var faces = sizes.Sum(size => size.Along + size.Trail) + (FolderIsland.Between(fit) * (sizes.Count - 1));
                        var (width, height) = folder.CellSize(0, style);
                        var block = layout.BlockRect(part).Width;
                        var wanted = FaceWidthTests.Button(style, faces);
                        table.AppendLine(CultureInfo.InvariantCulture, $"{folder.Options.Face.Kind}: button {width:0} px (laid out {block:0}), faces {faces:0.#} px + ends 2 × {FolderIsland.Ends(fit):0.#} = {wanted:0}");
                        if (MathF.Abs(width - wanted) > 0.5f || MathF.Abs(block - width) > 0.5f)
                        {
                            failures.Add($"{folder.Options.Face.Kind}: {width} px laid out {block}, its faces and ends {wanted} px");
                        }
                        var columns = Columns(folder, style, width, height);
                        var boxes = FolderIsland.FaceBoxes(gfx, folder.Options, style, width, height, shown, folder);
                        for (var at = 0; at < sizes.Count; at++)
                        {
                            var box = boxes[at];
                            var from = at == 0 ? 0 : (int)(boxes[at - 1].right + sizes[at - 1].Trail);
                            var to = at == sizes.Count - 1 ? (int)width : (int)box.left + (int)sizes[at].Along + (int)sizes[at].Trail + 1;
                            var ink = Enumerable.Range(from, Math.Max(0, Math.Min(to, columns.Length) - from)).Where(x => columns[x]).ToList();
                            var (first, last) = ink.Count > 0 ? (ink[0], ink[^1] + 1) : (0, 0);
                            if (last == first)
                            {
                                // A face that draws nothing: the keyboard face among the "Tides" folder's did (§277).
                                failures.Add($"{folder.Options.Face.Kind}: its {shown[at].Kind} face draws nothing");
                            }
                            table.AppendLine(CultureInfo.InvariantCulture, $"  {shown[at].Kind,-14} box {box.left,5:0}-{box.right,-5:0} ({sizes[at].Along:0} px{(sizes[at].Trail > 0 ? $" + {sizes[at].Trail:0} count" : "")})  ink {first,4}-{last,-4} ({last - first} px)");
                        }
                    }
                    File.WriteAllText(Path.Combine(Offscreen.Pictures, "face-slack-bar-150.txt"), table.ToString());
                    var firstFolder = specs.FindIndex(spec => spec.Kind == BarKind.FolderList);
                    OverADesktop(gfx, layout, look, content, null, "face-slack-bar-dark-150.png", (int)layout.BlockRect(firstFolder).X - (int)(24 * dpi / 96f));
                    Assert.True(failures.Count == 0, string.Join("\n", failures));
                }
                finally
                {
                    foreach (var island in islands.Values)
                    {
                        island.Dispose();
                    }
                }
            }
            finally
            {
                MediaIsland.Stand = null;
                FolderIsland.PerformanceSources = null;
                FolderIsland.DevicesStand = null;
                FolderIsland.TidesStand = null;
                FolderIsland.AirStand = null;
                IndicatorSources.Made = null;
                WindowsClock.SecondsStand = null;
                Clock.Now = Clock.Real;
            }
        });
    }

    /// <summary>Which columns of <paramref name="folder"/>'s button have ink on them, painted as the bar paints it.</summary>
    private static bool[] Columns(FolderIsland folder, in IslandStyle style, float width, float height)
    {
        folder.Forget();
        var columns = new bool[(int)MathF.Ceiling(width)];
        var drawn = style;
        Offscreen.Bitmap(width, height, dc => folder.PaintCell(dc, 0, drawn, width, height), bitmap =>
        {
            IWICBitmapLock* locked = null;
            try
            {
                var all = new WICRect { X = 0, Y = 0, Width = columns.Length, Height = (int)MathF.Ceiling(height) };
                ComUtil.Check(bitmap->Lock(&all, (uint)WICBitmapLockFlags.WICBitmapLockRead, &locked));
                uint stride = 0;
                ComUtil.Check(locked->GetStride(&stride));
                uint size = 0;
                byte* data = null;
                ComUtil.Check(locked->GetDataPointer(&size, &data));
                for (var y = 0; y < all.Height; y++)
                {
                    for (var x = 0; x < all.Width; x++)
                    {
                        columns[x] |= data[(y * stride) + (x * 4) + 3] > 24;
                    }
                }
            }
            finally
            {
                ComUtil.Release(ref locked);
            }
        });
        return columns;
    }
}
