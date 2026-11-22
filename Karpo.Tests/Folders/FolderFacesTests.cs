// folders: a folder's button wearing several faces side by side (docs\widgets.md §118; the person,
// 2026-10-11: "for a folder you should be actually able to give him multiple faces, so that i can
// also combine widgets as i want"), and the time over the date as Windows' clock shows them
// (FolderFaces.DateTime, DateTimeFaceDrawing) with the clock and the calendar together
// (Presets.ClockCalendarId).
//
// The file: one face is written as it always was ("face"), so no file of before changes; several are
// "faces", with the first as "face" too for a build that knows one. The bar: the button is as long
// as its faces side by side with the room between them, one block that a press anywhere on opens,
// each face reading what it shows. The sheet (folder-faces-side-by-side.png) goes to KARPO_PNG_DIR.

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Karpo.Dock;
using Karpo.Dock.QuickSettings.Services;
using Karpo.Tests.Forecasts;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using static Karpo.Tests.Sizing.OneRowTests;

namespace Karpo.Tests.Folders;

public sealed unsafe class FolderFacesTests
{
    // --- The file -------------------------------------------------------------------------------

    private static BarItem Wearing(params FolderFace[] faces)
    {
        var island = BarKind.FolderList.New();
        island.Id = "faces";
        island.Folder.Faces = [.. faces];
        island.Normalize();
        return island;
    }

    private static JsonObject FolderNode(Config config) =>
        JsonNode.Parse(config.ToJson())!["layouts"]![0]!["islands"]![0]!["folder"]!.AsObject();

    /// <summary>
    /// One face is written as a folder's face always was, "face" and nothing more: a file of before
    /// reads as one face, and is written back the very same.
    /// </summary>
    [Fact]
    public void OneFaceIsWrittenAsItAlwaysWas()
    {
        var config = Karpo.Tests.Core.Configs.With(Wearing(ClockFace.New(seconds: true)));
        var node = FolderNode(config);
        Assert.Equal("clock", (string?)node["face"]!["kind"]);
        Assert.False(node.ContainsKey("faces"));

        var back = Karpo.Tests.Core.Configs.RoundTrip(config, out var changed);
        Assert.False(changed);
        Assert.Equal(config.ToJson(), back.ToJson());
        var folder = Karpo.Core.Folders.FolderIn(Karpo.Tests.Core.Configs.Islands(back), "faces")!;
        Assert.Single(folder.Faces);
        Assert.True(ClockFace.Read(folder.Face).Seconds);
    }

    /// <summary>
    /// Several faces are "faces", in their order with their own options, and the first as "face"
    /// too, which a build of one face wears; read back, each is what it was.
    /// </summary>
    [Fact]
    public void SeveralFacesRoundTripInTheirOrder()
    {
        var clock = ClockFace.New();
        clock.Set(FolderFaces.Clock.Option(ClockFace.Zone)!, "Tokyo Standard Time");
        var config = Karpo.Tests.Core.Configs.With(Wearing(WeatherFace.New(WeatherFace.Detailed), FolderFace.Of(FolderFaces.DateTimeId), clock));
        var node = FolderNode(config);
        Assert.Equal("weather", (string?)node["face"]!["kind"]);
        Assert.Equal(["weather", "dateTime", "clock"], node["faces"]!.AsArray().Select(face => (string?)face!["kind"]));

        var back = Karpo.Tests.Core.Configs.RoundTrip(config, out var changed);
        Assert.False(changed);
        var folder = Karpo.Core.Folders.FolderIn(Karpo.Tests.Core.Configs.Islands(back), "faces")!;
        Assert.Equal([FolderFaces.WeatherId, FolderFaces.DateTimeId, FolderFaces.ClockId], folder.Faces.Select(face => face.Kind));
        Assert.Equal(WeatherFace.Detailed, WeatherFace.LookOf(folder.Faces[0]));
        Assert.Equal("Tokyo Standard Time", ClockFace.Read(folder.Faces[2]).Zone);
        Assert.Same(folder.Faces[0], folder.Face);
        // Taken down to one again, the file is one face's again.
        folder.Faces.RemoveRange(1, 2);
        Assert.False(FolderNode(back).ContainsKey("faces"));
    }

    /// <summary>
    /// "faces" wins over "face" wherever it stands in the folder; one with none in it is not there
    /// at all, and the folder wears its "face"; nulls in it are left out, and however many there are,
    /// every one is worn (§259).
    /// </summary>
    [Theory]
    [InlineData("""{ "faces": [ { "kind": "wifi" }, { "kind": "sound" } ], "face": { "kind": "clock" } }""", "wifi,sound")]
    [InlineData("""{ "face": { "kind": "clock" }, "faces": [ { "kind": "wifi" }, { "kind": "sound" } ] }""", "wifi,sound")]
    [InlineData("""{ "face": { "kind": "clock" }, "faces": [] }""", "clock")]
    [InlineData("""{ "face": { "kind": "clock" }, "faces": [ null, { "kind": "battery" }, null ] }""", "battery")]
    [InlineData("""{ "faces": [ null ] }""", "icon")]
    [InlineData("""{ }""", "icon")]
    [InlineData("""{ "faces": [ {"kind":"clock"},{"kind":"clock"},{"kind":"clock"},{"kind":"clock"},{"kind":"clock"},{"kind":"clock"},{"kind":"clock"},{"kind":"clock"},{"kind":"calendar"} ] }""", "clock,clock,clock,clock,clock,clock,clock,clock,calendar")]
    public void TheFacesAreReadWhateverTheFileHolds(string folder, string worn)
    {
        var json = $$"""{ "layouts": [ { "id": "default", "name": "Default", "islands": [ { "id": "f", "kind": "folderList", "folder": {{folder}} } ] } ], "defaultLayoutId": "default" }""";
        var config = Karpo.Tests.Core.Configs.Repaired(json);
        var read = Karpo.Core.Folders.FolderIn(Karpo.Tests.Core.Configs.Islands(config), "f")!;
        Assert.Equal(worn, string.Join(",", read.Faces.Select(face => face.Kind)));
    }

    /// <summary>
    /// The person's own file (their two layouts, every folder of one face) reads with each folder
    /// wearing the face it wore, and is written with no "faces" anywhere: nothing of theirs changes.
    /// </summary>
    [Fact]
    public void ThePersonsFileReadsAsOneFaceEachAndKeepsItsShape()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Karpo.Tests.Core.PersonsLayouts.json")!;
        using var reader = new StreamReader(stream);
        var fixture = reader.ReadToEnd();
        Assert.DoesNotContain("\"faces\"", fixture, StringComparison.Ordinal);

        var config = Karpo.Tests.Core.Configs.Repaired(fixture);
        var folders = config.Layouts!.SelectMany(layout => Karpo.Core.Folders.All(layout.Islands)).Select(entry => entry.Island).OfType<BarItem>()
            .Where(island => island.Folder is not null).ToList();
        Assert.NotEmpty(folders);
        Assert.All(folders, island => Assert.Single(island.Folder!.Faces));
        Assert.DoesNotContain("\"faces\"", config.ToJson(), StringComparison.Ordinal);
    }

    /// <summary>
    /// §427 (the person, 2026-10-30: "the folder name should basically be removed as it is not needed
    /// anywhere"): a folder has no name. Nothing holds one, its faces take none beside them, and its menu
    /// has no "Name" and no "Show name": where it stands as it opens is its one row of its own (its panel's menu's,
    /// §481); its face's menu its faces, "Add a face" last. The icon face's rows are its icon's alone, as they were (§364).
    /// </summary>
    [Fact]
    public void AFolderHasNoName()
    {
        Assert.Null(typeof(FolderOptions).GetProperty("Name"));
        Assert.Null(typeof(FolderOptions).GetProperty("ShowName"));
        Assert.Null(typeof(FaceInfo).GetProperty("Named"));
        Assert.Equal([FaceRowKind.Icon], FaceRows.Rows(FolderFaces.Icon).Select(row => row.Kind));

        var item = BarKind.FolderList.New();
        var folder = item.Folder!;
        Assert.Equal([OptionRows.FolderStyle, OptionRows.FolderAlign], OptionRows.Of(item).Select(row => row.Key));
        folder.Faces.Add(FolderFace.Of(FolderFaces.ClockId));
        Assert.Equal([OptionRows.FolderStyle, OptionRows.FolderAlign], OptionRows.Of(item).Select(row => row.Key));

        folder.Faces = [new FolderFace()];
        var menu = OptionMenu.Settings(item, [], null, null, FolderSide.Face, roomCells: null).Where(row => !row.Separator).Select(row => row.Text).ToList();
        Assert.DoesNotContain(S.SettingsWidgetCountdownNameHeader, menu);
        Assert.DoesNotContain(S.SettingsWidgetSwitchLabelHeader, menu);
        Assert.Equal(S.SettingsFolderFacesAddHeader, menu[^1]);
    }

    /// <summary>
    /// A face whose settings make each one another — an icon (§259), a clock, a date, a reading — may
    /// stand twice; a face showing what is the folder's one of a kind — its weather's place, the Wi-Fi,
    /// its first drive — once. Its own place is always its own to take again.
    /// </summary>
    [Fact]
    public void AFaceOfTheFoldersOneOfAKindIsWornOnce()
    {
        var folder = new FolderOptions { Faces = [WeatherFace.New(), ClockFace.New()] };
        Assert.True(FolderFaces.CanWear(folder, FolderFaces.Clock));
        Assert.True(FolderFaces.CanWear(folder, FolderFaces.DateTime));
        Assert.True(FolderFaces.CanWear(folder, FolderFaces.Cpu));
        Assert.False(FolderFaces.CanWear(folder, FolderFaces.Weather));
        Assert.True(FolderFaces.CanWear(folder, FolderFaces.Weather, replacing: 0));
        Assert.True(FolderFaces.CanWear(folder, FolderFaces.Wifi));
        Assert.True(FolderFaces.CanWear(new FolderOptions(), FolderFaces.Icon));
        // Each that repeats has something to tell two apart: its options, or the icon's own icon (FaceRowKind.Icon).
        Assert.All(FolderFaces.All.Where(face => face.Repeats), face => Assert.True(
            face.Options.Count > 0 || FaceRows.Rows(face).Any(row => row.Kind == FaceRowKind.Icon), $"{face.Id} repeats with nothing to tell two apart"));
        Assert.Equal(
            [FolderFaces.IconId, FolderFaces.ClockId, FolderFaces.CalendarId, FolderFaces.DateTimeId, FolderFaces.CpuId, FolderFaces.GpuId, FolderFaces.MemoryId, FolderFaces.DiskId, FolderFaces.NetworkActivityId],
            FolderFaces.All.Where(face => face.Repeats).Select(face => face.Id));
    }

    /// <summary>
    /// The page's edits of the faces: one added at the end (one remembered from earlier keeping its
    /// options), as many as you like (§259) but never a second of a face worn once; one taken off, never the last;
    /// the order a drag or Alt+Up and Alt+Down leaves.
    /// </summary>
    [Fact]
    public void TheFacesAreAddedRemovedAndReordered()
    {
        var folder = new FolderOptions { Face = FolderFace.Of(FolderFaces.WifiId) };
        var remembered = new Dictionary<string, FolderFace> { [FolderFaces.ClockId] = ClockFace.New(seconds: true) };

        Assert.True(FaceRows.AddFace(folder, FolderFaces.Sound));
        Assert.True(FaceRows.AddFace(folder, FolderFaces.Clock, remembered));
        Assert.True(ClockFace.Read(folder.Faces[2]).Seconds);
        Assert.NotSame(remembered[FolderFaces.ClockId], folder.Faces[2]);
        Assert.False(FaceRows.AddFace(folder, FolderFaces.Wifi));
        Assert.False(FaceGallery.Tile(folder, 1, FolderFaces.Wifi).Available);
        Assert.True(FaceGallery.Tile(folder, 0, FolderFaces.Wifi).Available);

        Assert.True(FaceRows.MoveFace(folder, 2, 0));
        Assert.Equal([FolderFaces.ClockId, FolderFaces.WifiId, FolderFaces.SoundId], folder.Faces.Select(face => face.Kind));
        Assert.False(FaceRows.MoveFace(folder, 1, 1));
        Assert.False(FaceRows.MoveFace(folder, 0, 3));
        Assert.False(FaceRows.MoveFace(folder, -1, 0));

        Assert.True(FaceRows.RemoveFace(folder, 1));
        Assert.True(FaceRows.RemoveFace(folder, 0));
        Assert.False(FaceRows.RemoveFace(folder, 0));
        Assert.Equal([FolderFaces.SoundId], folder.Faces.Select(face => face.Kind));

        while (folder.Faces.Count < 24)
        {
            Assert.True(FaceRows.AddFace(folder, FolderFaces.Clock));
        }
        Assert.True(FaceRows.AddFace(folder, FolderFaces.Icon));
        Assert.True(FaceRows.AddFace(folder, FolderFaces.Icon));
        Assert.False(FaceRows.CanAdd(folder, FolderFaces.Sound));
        Assert.Equal(26, folder.Faces.Count);
    }

    /// <summary>
    /// The weather's place and what the markets face tracks are their first face's, wherever it stands
    /// among the faces: what the folder follows, and what a preset's first place is given to.
    /// </summary>
    [Fact]
    public void TheWeathersPlaceIsItsFacesWhereverItStands()
    {
        var weather = WeatherFace.New();
        weather.Weather = new WeatherOptions { Place = "Bern", Latitude = 46.95, Longitude = 7.45 };
        var folder = new FolderOptions { Faces = [ClockFace.New(), weather] };
        Assert.Equal("Bern", WeatherPlaces.OfFace(folder)!.Place);

        var stock = new FolderFace { Kind = FolderFaces.MarketsId, Market = new MarketOptions { Symbol = "MSFT" } };
        var shares = new FolderOptions { Faces = [FolderFace.Of(FolderFaces.DateTimeId), stock] };
        Assert.Equal("MSFT", MarketFaces.Of(shares)!.Symbol);
    }

    /// <summary>
    /// A folder wearing two readings' faces gathers both readings from the dock's start, as a line
    /// of each would (SourceKinds.Wanted).
    /// </summary>
    [Fact]
    public void EachReadingsFaceIsGathered()
    {
        var island = Wearing(FolderFace.Of(FolderFaces.CpuId), FolderFace.Of(FolderFaces.MemoryId));
        var wanted = SourceKinds.Wanted(island).Select(pair => pair.Key).ToList();
        Assert.Equal(2, wanted.Distinct().Count());
    }

    /// <summary>The time-and-date face's seconds: as Windows' clock has them unless set always or never.</summary>
    [Fact]
    public void TheTimeAndDatesSecondsFollowWindowsUnlessSet()
    {
        var face = FolderFace.Of(FolderFaces.DateTimeId);
        Assert.True(DateTimeFace.ShowsSeconds(face, windows: true));
        Assert.False(DateTimeFace.ShowsSeconds(face, windows: false));
        face.Set(FolderFaces.DateTime.Option(DateTimeFace.Seconds)!, DateTimeFace.Shown);
        Assert.True(DateTimeFace.ShowsSeconds(face, windows: false));
        face.Set(FolderFaces.DateTime.Option(DateTimeFace.Seconds)!, DateTimeFace.Hidden);
        Assert.False(DateTimeFace.ShowsSeconds(face, windows: true));
    }

    /// <summary>
    /// The clock and the calendar together: the time over the date on its button, the clock's list
    /// beside the calendar's inside, each with its footer; the date header left out, since the time
    /// header says the date. It is in the add list's presets and in the layout of every preset.
    /// </summary>
    [Fact]
    public void TheClockAndCalendarPresetIsBothSideBySide()
    {
        var preset = Presets.Find(Presets.ClockCalendarId)!;
        var folder = Presets.Folder(preset, new PresetMachine(false, [])).Folder!;
        Assert.Equal(FolderFaces.DateTimeId, folder.Face.Kind);
        Assert.Single(folder.Faces);
        Assert.Equal(2, folder.Lists.Count);
        Assert.Equal(["timeHeader", "timer", "stopwatch", "focus", "actions"], folder.Lists[0].Select(cell => cell.Island.Kind));
        Assert.Equal(["month", "events", "actions"], folder.Lists[1].Select(cell => cell.Island.Kind));
        Assert.Equal(LineHeight.Exactly(12), folder.Lists[1][1].Island.Height);
        Assert.Equal("Clock and calendar", preset.Title);
        Assert.Contains(preset, Presets.On(WidgetType.Bar));
        Assert.Contains(PresetLayout.Make(new PresetMachine(false, [])).Islands, island => island.Folder is not null && Karpo.Tests.Core.PresetsTests.MadeBy(island, new PresetMachine(false, [])) == preset);
    }

    // --- The bar --------------------------------------------------------------------------------

    private static IslandStyle Style(int height, uint dpi, Look look, bool vertical = false)
    {
        var config = new Config { Height = height };
        return new IslandStyle(dpi / 96f, vertical, Layout.CellPx(dpi, config), Layout.IconPx(dpi, config), look);
    }

    private static readonly SystemStatus.Battery Charge = new(76, false, false);

    /// <summary>The folder made on this thread, every face showing a stand-in's: the radios, the battery, a saved forecast.</summary>
    private static FolderIsland Make(Gfx gfx, BarItem folder)
    {
        FolderIsland.StatusStand = FolderFaceTests.Radios;
        FolderIsland.WeatherStand = SavedWeather.Cache();
        var probe = BatteryIsland.Probe;
        BatteryIsland.Probe = () => Charge;
        try
        {
            return new(Config.IslandKey(null, folder.Id), folder, gfx, new Config());
        }
        finally
        {
            FolderIsland.StatusStand = null;
            FolderIsland.WeatherStand = null;
            BatteryIsland.Probe = probe;
        }
    }

    private static BarItem Placed(BarItem folder)
    {
        if (folder.Folder!.FaceOf(FolderFaces.WeatherId) is { } weather)
        {
            weather.Weather = SavedWeather.Options();
        }
        return folder;
    }

    private static float Length(FolderIsland island, in IslandStyle style) => island.CellSize(0, style) is var (w, h) && style.Vertical ? h : w;

    /// <summary>
    /// The button is as long as its faces side by side, the room between each two, and the room at
    /// its ends one face has: across a bar and down one, faces of words and glyphs' squares alike.
    /// </summary>
    [UiFact]
    public void TheButtonIsAsLongAsItsFacesSideBySide()
    {
        Offscreen.OnStaThread(gfx =>
        {
            foreach (var vertical in new[] { false, true })
            {
                foreach (var height in new[] { 32, 48, 64 })
                {
                    var style = Style(height, 96, LookFor(dark: true), vertical);
                    var fit = new Fit(style);
                    var ends = 2 * MathF.Max(fit.Edge, fit.Px(8));
                    foreach (var faces in new[]
                    {
                        new[] { FolderFaces.WeatherId, FolderFaces.DateTimeId },
                        new[] { FolderFaces.WifiId, FolderFaces.SoundId, FolderFaces.BatteryId },
                        new[] { FolderFaces.CpuId, FolderFaces.MemoryId, FolderFaces.ClockId },
                    })
                    {
                        var folder = Placed(Wearing([.. faces.Select(FolderFace.Of)]));
                        using var island = Make(gfx, folder);
                        var sizes = FolderIsland.FaceSizes(gfx, folder.Folder!, style, folder: island);
                        var expected = sizes.Sum(size => size.Along) + (FolderIsland.Between(fit) * (faces.Length - 1)) + ends;
                        var length = Length(island, style);
                        var where = $"{string.Join(" + ", faces)} at {height} DIP{(vertical ? ", vertical" : "")}";
                        // The bar gives a widget whole pixels, a cell's at least.
                        Assert.True(length >= expected - 0.5f && length <= MathF.Max(expected, style.Cell) + 1.5f, $"{where}: {length} px for {expected} px of faces");
                        // And each face alone is shorter than them all.
                        foreach (var face in faces)
                        {
                            using var alone = Make(gfx, Placed(Wearing(FolderFace.Of(face))));
                            Assert.True(Length(alone, style) < length, $"{where}: {face} alone is {Length(alone, style)} px, all of them {length}");
                        }
                    }
                }
            }
        });
    }

    /// <summary>
    /// The button of several faces is one block: a press anywhere along it — on its first face, in
    /// the room between two, on its last — is its one cell, which opens the folder, and its hover
    /// and its press cover the whole of it.
    /// </summary>
    [UiFact]
    public void APressAnywhereOnTheFacesOpensTheFolder()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var style = Style(48, 96, LookFor(dark: true));
            var folder = Placed(Wearing(WeatherFace.New(), FolderFace.Of(FolderFaces.DateTimeId)));
            using var island = Make(gfx, folder);
            var length = Length(island, style);
            Assert.Equal(1, island.Measure(style).Count);
            Assert.Equal(IslandView.Folder, island.Click(0).View);

            var specs = new List<PartSpec> { new(Config.IslandKey("layout:default", folder.Id), BarKind.FolderList) };
            var layout = Layout.ForTests(96, new RECT { left = 0, top = 0, right = 1920, bottom = 1080 }, Edge.Bottom, new Config(), specs, length);
            var part = layout.PartAt(0);
            Assert.Equal(length, part.Width, 0.5f);
            // The bar stands at the bottom of the screen, its middle half its thickness above the edge.
            var across = layout.Thickness / 2f;
            foreach (var x in new[] { part.Left + 1, part.Left + (part.Width / 2), part.Left + part.Width - 1 })
            {
                Assert.Equal(Hit.Cell(0, 0), layout.HitTest(x, 1080 - across));
            }
        });
    }

    /// <summary>Twelve faces of mixed kinds, three of them icons, each its own icon (§259).</summary>
    private static BarItem Many()
    {
        FolderFace Icon(string? glyph) => new() { Glyph = glyph };
        return Placed(Wearing(
            Icon(""), ClockFace.New(), WeatherFace.New(), Icon(""), FolderFace.Of(FolderFaces.WifiId), FolderFace.Of(FolderFaces.SoundId),
            FolderFace.Of(FolderFaces.BatteryId), FolderFace.Of(FolderFaces.CpuId), FolderFace.Of(FolderFaces.MemoryId), Icon(null),
            FolderFace.Of(FolderFaces.DateTimeId), FolderFace.Of(FolderFaces.NotificationsId)));
    }

    /// <summary>
    /// A button of many faces is as long as all of them, however many (§259): twelve of mixed kinds at
    /// 100 and 150 %, across a bar and down one, in the room the bar gives a folder (PartSpec.For) —
    /// which held it to eight cells and cut the faces past them off — and the bar lays it out that long.
    /// </summary>
    [UiFact]
    public void AButtonOfManyFacesIsAsLongAsAllOfThem()
    {
        Offscreen.OnStaThread(gfx =>
        {
            WindowsClock.SecondsStand = false;
            try
            {
                var folder = Many();
                Assert.Equal(12, folder.Folder!.Faces.Count);
                using var island = Make(gfx, folder);
                var spec = PartSpec.For(Config.IslandKey("layout:default", folder.Id), folder);
                foreach (var (dpi, vertical) in new[] { (96u, false), (144u, false), (96u, true) })
                {
                    var style = Style(48, dpi, LookFor(dark: true), vertical) with { Room = spec.Room };
                    var fit = new Fit(style);
                    var sizes = FolderIsland.FaceSizes(gfx, folder.Folder, style, folder: island);
                    var ends = 2 * MathF.Max(fit.Edge, fit.Px(8));
                    var faces = sizes.Sum(size => size.Along + size.Trail) + (FolderIsland.Between(fit) * (sizes.Count - 1)) + ends;
                    island.Forget();
                    var length = Length(island, style);
                    var where = $"{dpi} dpi{(vertical ? ", vertical" : "")}";
                    Assert.True(length >= faces - 0.5f, $"{where}: {length} px for {faces} px of faces");
                    Assert.True(length > 8 * style.Cell, $"{where}: {length} px, no longer than the eight cells that cut it off");
                    // The last face's box ends inside the button: nothing is cut.
                    var (w, h) = island.CellSize(0, style);
                    var last = FolderIsland.FaceBoxes(gfx, folder.Folder, style, w, h, folder: island)[^1];
                    Assert.True((vertical ? last.bottom : last.right) <= (vertical ? h : w) + 0.5f, $"{where}: the last face ends at {(vertical ? last.bottom : last.right)} of {(vertical ? h : w)} px");
                    if (!vertical)
                    {
                        var screen = new RECT { right = (int)(3840 * dpi / 96), bottom = (int)(1080 * dpi / 96) };
                        var layout = Layout.ForTests(dpi, screen, Edge.Bottom, new Config { Height = 48 }, [spec], length);
                        Assert.Equal(length, layout.PartAt(0).Width, 1f);
                    }
                }
            }
            finally
            {
                WindowsClock.SecondsStand = null;
            }
        });
    }

    /// <summary>
    /// Each icon face has its own icon (§259): its picker sets that face's, the others' as they were;
    /// the file keeps each on its face, and nothing on the folder.
    /// </summary>
    [Fact]
    public void EachIconFaceHasItsOwnIcon()
    {
        var item = Wearing(FolderFace.Of(FolderFaces.IconId), ClockFace.New(), FolderFace.Of(FolderFaces.IconId));
        var folder = item.Folder!;
        Assert.True(FaceRows.AddFace(folder, FolderFaces.Icon));
        Assert.Equal(4, folder.Faces.Count);
        foreach (var (at, glyph) in new[] { (0, ""), (2, "") })
        {
            var row = OptionRows.OfFace(folder, at).Single(row => row.Key == OptionPickers.FolderIcon);
            Assert.True(row.Set!(row.Tiles.ToList().FindIndex(tile => tile.Glyph == glyph)));
        }
        Assert.Equal(["", null, "", null], folder.Faces.Select(face => face.Glyph));
        Assert.Equal(["", "", FolderOptions.FolderGlyph], folder.Faces.Where(face => face.Kind == FolderFaces.IconId).Select(face => face.GlyphOrDefault));
        // The default again is none of its own.
        var first = OptionRows.OfFace(folder, 0).Single(row => row.Key == OptionPickers.FolderIcon);
        Assert.True(first.Set!(0));
        Assert.Null(folder.Faces[0].Glyph);

        var node = FolderNode(Karpo.Tests.Core.Configs.With(item));
        Assert.False(node.ContainsKey("glyph"));
        Assert.Equal("", (string?)node["faces"]![2]!["glyph"]);
        Assert.False(node["faces"]![0]!.AsObject().ContainsKey("glyph"));
    }

    /// <summary>
    /// Each face reads what it shows and is painted again when it changes: the time-and-date face
    /// beside the weather follows the minute and the day, the weather its forecast, and what the
    /// button shows is both.
    /// </summary>
    [UiFact]
    public void EachFaceKeepsReading()
    {
        Offscreen.OnStaThread(gfx =>
        {
            try
            {
                Clock.Now = () => new DateTime(2026, 12, 31, 23, 58, 0);
                var style = Style(48, 96, LookFor(dark: true));
                using var island = Make(gfx, Placed(Wearing(WeatherFace.New(), FolderFace.Of(FolderFaces.DateTimeId), FolderFace.Of(FolderFaces.BatteryId))));
                _ = island.Measure(style);
                _ = island.Surface(0, style);
                var before = island.FaceShown;
                Assert.Equal(3, before.Split('\u001F').Length);
                Assert.Contains("76%", before, StringComparison.Ordinal);
                Assert.False(island.MinuteChanged(gfx));
                Assert.Equal(before, island.FaceShown);
                Clock.Now = () => new DateTime(2027, 1, 1, 0, 0, 0);
                Assert.False(island.MinuteChanged(gfx));
                Assert.NotEqual(before, island.FaceShown);
                Assert.Contains("2027-01-01", island.FaceShown, StringComparison.Ordinal);
                // The weather's stays as it was, beside it.
                Assert.Equal(before.Split('\u001F')[0], island.FaceShown.Split('\u001F')[0]);
                // Each face's words on hover, on lines of their own.
                Assert.True((island.Tooltip(0) ?? "").Split('\n').Length >= 2, island.Tooltip(0));
            }
            finally
            {
                Clock.Now = Clock.Real;
            }
        });
    }

    /// <summary>
    /// The time over the date is right-aligned as Windows' clock has it, in the middle of the bar, and
    /// draws inside its cell at every height, across and down a bar; with seconds it is wider, as wide
    /// at one o'clock as at ten to midnight, on the 1st as on the 28th.
    /// </summary>
    [UiFact]
    public void TheTimeAndDateFitsAndKeepsItsWidth()
    {
        Offscreen.OnStaThread(gfx =>
        {
            try
            {
                WindowsClock.SecondsStand = false;
                foreach (var vertical in new[] { false, true })
                {
                    foreach (var height in new[] { 32, 40, 48, 56, 64 })
                    {
                        var style = Style(height, 96, LookFor(dark: true), vertical);
                        using var plain = Make(gfx, Wearing(FolderFace.Of(FolderFaces.DateTimeId)));
                        var lengths = new[] { new DateTime(2026, 1, 1, 1, 0, 0), new DateTime(2026, 12, 28, 23, 59, 59) }.Select(moment =>
                        {
                            Clock.Now = () => moment;
                            plain.Forget();
                            return Length(plain, style);
                        }).Distinct().ToList();
                        Assert.Single(lengths);
                        var plan = DateTimeFaceDrawing.Plan(gfx, style, FolderFace.Of(FolderFaces.DateTimeId));
                        // Two lines wherever the bar is 40 DIP or more, as Windows' own clock is.
                        Assert.True(vertical || height < 40 || plan.Stacked, $"{height} DIP: one line, {plan.TimeHeight} px lines at {plan.TimePx} px in a {style.Cell} px cell");
                        if (!vertical && plan.Stacked)
                        {
                            Assert.True(plan.TimeHeight + plan.DateHeight <= style.Cell, $"{height} DIP: two lines of {plan.TimeHeight} px in a {style.Cell} px bar");
                        }
                        var seconds = FolderFace.Of(FolderFaces.DateTimeId);
                        seconds.Set(FolderFaces.DateTime.Option(DateTimeFace.Seconds)!, DateTimeFace.Shown);
                        using var withSeconds = Make(gfx, Wearing(seconds));
                        // Across a bar never narrower (the date may be the wider line either way); down one, where
                        // it is a step smaller to fit, it may be shorter.
                        Assert.True(vertical || Length(withSeconds, style) >= Length(plain, style), $"{height} DIP: {Length(withSeconds, style)} px with seconds, {Length(plain, style)} without");
                    }
                }
                // As Windows' clock has its seconds, where the face follows it.
                using var follows = Make(gfx, Wearing(FolderFace.Of(FolderFaces.DateTimeId)));
                Assert.False(follows.FaceSeconds);
                WindowsClock.SecondsStand = true;
                Assert.True(follows.FaceSeconds);
            }
            finally
            {
                Clock.Now = Clock.Real;
                WindowsClock.SecondsStand = null;
            }
        });
    }

    // --- The sheet ------------------------------------------------------------------------------

    /// <summary>
    /// Faces side by side on bars of 32 to 64 DIP, dark and light, across and down a bar: the weather
    /// beside the time and date, the network, the sound and the battery as Windows' one button of the
    /// three, readings beside a clock; and the time and date alone, as Windows' clock.
    /// </summary>
    [UiFact]
    public void SideBySideSheet()
    {
        Offscreen.OnStaThread(gfx =>
        {
            Clock.Now = () => new DateTime(2026, 10, 9, 14, 7, 42);
            WindowsClock.SecondsStand = false;
            var samples = new (string Name, BarItem Folder)[]
            {
                ("time and date", Wearing(FolderFace.Of(FolderFaces.DateTimeId))),
                ("weather + time and date", Placed(Wearing(WeatherFace.New(), FolderFace.Of(FolderFaces.DateTimeId)))),
                ("wifi + sound + battery", Wearing(FolderFace.Of(FolderFaces.WifiId), FolderFace.Of(FolderFaces.SoundId), FolderFace.Of(FolderFaces.BatteryId))),
                ("cpu + memory + clock", Wearing(FolderFace.Of(FolderFaces.CpuId), FolderFace.Of(FolderFaces.MemoryId), ClockFace.New())),
                ("time and date + bell", Wearing(FolderFace.Of(FolderFaces.DateTimeId), FolderFace.Of(FolderFaces.NotificationsId))),
            };
            var made = samples.Select(sample => (sample.Name, Island: Make(gfx, sample.Folder))).ToList();
            try
            {
                var items = new List<(string, FolderIsland, IslandStyle)>();
                foreach (var (name, island) in made)
                {
                    foreach (var height in new[] { 32, 40, 48, 64 })
                    {
                        items.Add(($"{name} {height}", island, Style(height, 96, LookFor(dark: true))));
                    }
                    items.Add(($"{name} light 48", island, Style(48, 96, LookFor(dark: false))));
                    items.Add(($"{name} 48 @150%", island, Style(48, 144, LookFor(dark: true))));
                    items.Add(($"{name} vertical", island, Style(48, 96, LookFor(dark: true), vertical: true)));
                }
                Sheet(gfx, items, "folder-faces-side-by-side.png");
            }
            finally
            {
                foreach (var (_, island) in made)
                {
                    island.Dispose();
                }
                Clock.Now = Clock.Real;
                WindowsClock.SecondsStand = null;
            }
        });
    }

    /// <summary>
    /// Twelve faces on one button, three of them icons each its own (§259), in the room the bar gives a
    /// folder: dark at 150 %, and at 100 % dark and light on bars of 32 and 48 DIP.
    /// </summary>
    [UiFact]
    public void ManyFacesSheet()
    {
        Offscreen.OnStaThread(gfx =>
        {
            Clock.Now = () => new DateTime(2026, 10, 9, 14, 7, 42);
            WindowsClock.SecondsStand = false;
            var folder = Many();
            var room = PartSpec.For(Config.IslandKey("layout:default", folder.Id), folder).Room;
            var island = Make(gfx, folder);
            try
            {
                Sheet(gfx,
                [
                    ("12 faces dark 48 @150%", island, Style(48, 144, LookFor(dark: true)) with { Room = room }),
                    ("12 faces dark 32 @150%", island, Style(32, 144, LookFor(dark: true)) with { Room = room }),
                    ("12 faces dark 48", island, Style(48, 96, LookFor(dark: true)) with { Room = room }),
                    ("12 faces light 48", island, Style(48, 96, LookFor(dark: false)) with { Room = room }),
                    ("12 faces dark 32", island, Style(32, 96, LookFor(dark: true)) with { Room = room }),
                ], "folder-many-faces.png");
            }
            finally
            {
                island.Dispose();
                Clock.Now = Clock.Real;
                WindowsClock.SecondsStand = null;
            }
        });
    }

    private static void Sheet(Gfx gfx, List<(string Name, FolderIsland Island, IslandStyle Style)> items, string file)
    {
        const float pad = 14f;
        const float label = 16f;
        const float most = 1400f;
        var places = new List<(float X, float Y, float W, float H)>();
        var x = pad;
        var y = pad;
        var rowHeight = 0f;
        var widest = 0f;
        foreach (var (_, island, style) in items)
        {
            island.Forget();
            var (w, h) = island.CellSize(0, style);
            var slot = MathF.Max(w, 150f);
            if (x > pad && x + slot > most)
            {
                x = pad;
                y += label + rowHeight + pad;
                rowHeight = 0;
            }
            places.Add((x, y, w, h));
            rowHeight = MathF.Max(rowHeight, h);
            x += slot + pad;
            widest = MathF.Max(widest, x);
        }
        var total = y + label + rowHeight + pad;
        var dark = FlyoutTheme.For(LookFor(dark: true));
        Offscreen.Bitmap(widest, total, dc =>
        {
            PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, widest, total), 0, PopupPaint.Ink(dark.Base));
            for (var index = 0; index < items.Count; index++)
            {
                var (name, island, style) = items[index];
                var (left, top, w, h) = places[index];
                var theme = FlyoutTheme.For(style.Look);
                PopupPaint.Text(dc, gfx, name, 11f, Weight.Regular, dark.TextSecondary, left, top, 220f);
                var cardTop = top + label;
                PopupPaint.Fill(dc, PopupPaint.Rect(left, cardTop, w, h), 4f * style.Scale, PopupPaint.Ink(theme.CardBackground));
                var style2 = style;
                var island2 = island;
                Offscreen.Shifted(dc, left, cardTop, () => island2.PaintCell(dc, 0, style2, w, h));
            }
        }, bitmap => Offscreen.Save(bitmap, Path.Combine(Offscreen.Pictures, file)));
    }
}
