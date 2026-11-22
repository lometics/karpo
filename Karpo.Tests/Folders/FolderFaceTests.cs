// folders: a folder's face, what its button on the bar shows (Karpo.Core\FolderFaces.cs,
// Karpo.Dock\Islands\FaceDrawings.cs, docs\widgets.md §67).
//
// The file: a face is its word and a bag of its options in JSON's own values, read through the
// face's descriptor — what this build does not know (a face, an option, a value of the wrong kind)
// is kept as it is and read as the default.
//
// The bar: every face is measured on its settings and templates, never on the time or the date it
// shows, so the button is as wide at one o'clock as at ten and on the 1st as on the 28th; every face
// draws inside its cell at every height from 32 to 64 DIP, dark, light and in high contrast; a clock
// is painted again in place when its minute (or its second) changes and not otherwise, and the
// date turns over at midnight — never a rebuild.
//
// The sheets (folder-faces-<40|48>-<dark|light>.png, folder-faces-more.png) go to KARPO_PNG_DIR.

using System.Text.Json;
using Karpo.Dock;
using Karpo.Dock.Drives;
using Karpo.Dock.Native;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using static Karpo.Tests.Sizing.OneRowTests;

namespace Karpo.Tests.Folders;

public sealed unsafe class FolderFaceTests
{
    // --- The file -------------------------------------------------------------------------------

    /// <summary>A face and its options are written in their own words and JSON's own values, and read back as written.</summary>
    [Fact]
    public void AFaceRoundTripsThroughTheFileInItsOwnWords()
    {
        var island = BarKind.FolderList.New();
        island.Id = "time";
        var face = FolderFace.Of(FolderFaces.ClockId);
        face.Set(FolderFaces.Clock.Option(ClockFace.Seconds)!, true);
        face.Set(FolderFaces.Clock.Option(ClockFace.Hours)!, ClockFace.TwentyFour);
        face.Set(FolderFaces.Clock.Option(ClockFace.Label)!, "  Tokyo ");
        island.Folder!.Face = face;

        var config = Karpo.Tests.Core.Configs.With(island);
        using (var written = JsonDocument.Parse(config.ToJson()))
        {
            var folder = written.RootElement.GetProperty("layouts")[0].GetProperty("islands")[0].GetProperty("folder");
            var options = folder.GetProperty("face").GetProperty("options");
            Assert.Equal("clock", folder.GetProperty("face").GetProperty("kind").GetString());
            Assert.Equal(JsonValueKind.True, options.GetProperty("seconds").ValueKind);
            Assert.Equal("twentyFour", options.GetProperty("hours").GetString());
            Assert.Equal("Tokyo", options.GetProperty("label").GetString());
        }

        var back = Karpo.Tests.Core.Configs.RoundTrip(config, out var changed);
        Assert.False(changed);
        var read = ClockFace.Read(Karpo.Core.Folders.FolderIn(Karpo.Tests.Core.Configs.Islands(back), "time")!.Face);
        Assert.Equal(new ClockFaceSettings(Seconds: true, Hours: ClockFace.TwentyFour, Zone: "", Label: "Tokyo", Timer: true), read);
    }

    /// <summary>
    /// What this build does not know is read as the default and kept as it was: an option it has
    /// never heard of, a value of the wrong kind, a choice that is not one of its words.
    /// </summary>
    [Fact]
    public void WhatThisBuildDoesNotKnowIsReadAsTheDefaultAndKept()
    {
        var config = Karpo.Tests.Core.Configs.Repaired("""
            { "layouts": [ { "id": "default", "name": "Default", "islands": [
              { "id": "time", "kind": "folderList", "folder": { "name": "Time",
                "face": { "kind": "clock", "options": { "seconds": "yes", "hours": "36", "show": 5, "glow": { "hue": 3 } } } } } ] } ] }
            """);

        var face = Karpo.Core.Folders.FolderIn(Karpo.Tests.Core.Configs.Islands(config), "time")!.Face;
        Assert.Equal(new ClockFaceSettings(Seconds: false, Hours: ClockFace.Region, Zone: "", Label: "", Timer: true), ClockFace.Read(face));
        using var written = JsonDocument.Parse(config.ToJson());
        var options = written.RootElement.GetProperty("layouts")[0].GetProperty("islands")[0].GetProperty("folder").GetProperty("face").GetProperty("options");
        Assert.Equal("yes", options.GetProperty("seconds").GetString());
        Assert.Equal(3, options.GetProperty("glow").GetProperty("hue").GetInt32());
    }

    /// <summary>Each face's options and what they are where nobody chose; a text is trimmed and cut at its length.</summary>
    [Fact]
    public void EachFaceHasItsOwnOptionsAndDefaults()
    {
        Assert.Empty(FolderFaces.Icon.Options);
        Assert.Equal(new ClockFaceSettings(Seconds: false, Hours: ClockFace.Region, Zone: "", Label: "", Timer: true), ClockFace.Read(FolderFace.Of(FolderFaces.ClockId)));
        var calendar = FolderFace.Of(FolderFaces.CalendarId);
        Assert.False(CalendarFace.IsPage(calendar));
        Assert.False(CalendarFace.WithDay(calendar));

        var clock = FolderFace.Of(FolderFaces.ClockId);
        var label = FolderFaces.Clock.Option(ClockFace.Label)!;
        clock.Set(label, new string('x', 60));
        Assert.Equal(FaceOption.TextLength, clock.Text(label).Length);
        // A switch set from its word.
        clock.Set(FolderFaces.Clock.Option(ClockFace.Timer)!, "false");
        Assert.False(ClockFace.Read(clock).Timer);
        // An option of one face means nothing to another: the calendar's look is not the clock's.
        Assert.Null(FolderFaces.Clock.Option(CalendarFace.Look));
    }

    /// <summary>A face this build does not know is its icon, and a copy is its own.</summary>
    [Fact]
    public void AnUnknownFaceIsItsIconAndACopyIsItsOwn()
    {
        // "bluetooth" was the example until it became a face (2026-10-10).
        Assert.Same(FolderFaces.Icon, FolderFace.Of("radar").Info);
        Assert.Same(FolderFaces.Bluetooth, FolderFace.Of("bluetooth").Info);
        Assert.Same(FolderFaces.Icon, FolderFaces.Of(null));
        var clock = FolderFace.Of(FolderFaces.ClockId);
        var copy = clock.Clone();
        copy.Set(FolderFaces.Clock.Option(ClockFace.Seconds)!, true);
        Assert.False(ClockFace.Read(clock).Seconds);
    }

    /// <summary>
    /// The icon face is "Icon" (2026-10-20: "its icon face should be renamed to just icon"), its id the
    /// word it always was; in every language its own word, and the face of what is inside has none left.
    /// </summary>
    [Fact]
    public void TheIconFaceIsCalledIcon()
    {
        Assert.Equal("icon", FolderFaces.IconId);
        Assert.Equal("Icon", Strings.Lookup("core.face.icon.title", "en"));
        Assert.Equal("Symbol", Strings.Lookup("core.face.icon.title", "de"));
        foreach (var language in Strings.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(Strings.Lookup("core.face.icon.title", language)), language);
            Assert.Null(Strings.Lookup("core.face.content.title", language));
        }
    }

    /// <summary>Every face the settings app offers has a drawing on the bar.</summary>
    [Fact]
    public void EveryFaceHasADrawing()
    {
        Assert.Equal(FolderFaces.All.Select(face => face.Id).Order(), FaceDrawings.Ids.Order());
    }

    /// <summary>
    /// A 12- or a 24-hour clock is the region's pattern with its hours changed: the AM/PM marker
    /// comes or goes with the space before it, quoted text is left alone, and the region's own
    /// clock is its pattern unchanged.
    /// </summary>
    [Theory]
    [InlineData("HH:mm:ss", ClockFace.Twelve, "h:mm:ss tt")]
    [InlineData("H:mm", ClockFace.Twelve, "h:mm tt")]
    [InlineData("h:mm:ss tt", ClockFace.TwentyFour, "H:mm:ss")]
    [InlineData("hh:mm tt", ClockFace.TwentyFour, "HH:mm")]
    [InlineData("tt h:mm", ClockFace.TwentyFour, "H:mm")]
    [InlineData("tt h:mm", ClockFace.Twelve, "tt h:mm")]
    [InlineData("HH 'h' mm", ClockFace.Twelve, "h 'h' mm tt")]
    [InlineData("h:mm tt", ClockFace.Region, "h:mm tt")]
    public void TheHoursAreTheRegionsPatternWithItsHoursChanged(string region, string hours, string expected)
    {
        Assert.Equal(expected, ClockFace.Pattern(region, hours));
    }

    // --- The bar --------------------------------------------------------------------------------

    /// <summary>One face to measure and draw: what the sheet calls it, and the folder wearing it.</summary>
    internal sealed record Sample(string Name, BarItem Folder);

    /// <summary>Every face, and every look and option that changes what it draws.</summary>
    internal static List<Sample> Samples()
    {
        var samples = new List<Sample>
        {
            new("icon", Wearing(FolderFaces.IconId, glyph: char.ConvertFromUtf32(0xE80F))),
            new("clock", Folder(FolderFaces.ClockId)),
            new("clock 12h + seconds", Folder(FolderFaces.ClockId, (ClockFace.Hours, ClockFace.Twelve), (ClockFace.Seconds, "true"))),
            new("clock 24h, Tokyo, timer", Wearing(FolderFaces.ClockId, timer: true, options: [(ClockFace.Hours, ClockFace.TwentyFour), (ClockFace.Label, "Tokyo")])),
            new("calendar", Folder(FolderFaces.CalendarId)),
            new("calendar day + date", Folder(FolderFaces.CalendarId, (CalendarFace.Form, CalendarFace.DayAndDate))),
            new("calendar page", Folder(FolderFaces.CalendarId, (CalendarFace.Look, CalendarFace.Page))),
            // The time over the date, as Windows' clock (§118); and with its seconds always.
            new("time and date", Folder(FolderFaces.DateTimeId)),
            new("time and date + seconds", Folder(FolderFaces.DateTimeId, (DateTimeFace.Seconds, DateTimeFace.Shown))),
            new("icon, chosen", Wearing(FolderFaces.IconId, glyph: char.ConvertFromUtf32(0xE80F))),
            // The network's and Bluetooth's glyphs, in the state Make stands them in (Radios).
            new("wifi", Folder(FolderFaces.WifiId)),
            new("bluetooth", Folder(FolderFaces.BluetoothId)),
            // The sound's speaker, at the level Radios has (40 %: two waves).
            new("sound", Folder(FolderFaces.SoundId)),
            // The removable devices' stick, with the two Sticks plugged in: its count on the corner.
            new("removable", Folder(FolderFaces.RemovableId)),
            // The timer's, the focus session's and the stopwatch's, at rest: the length set, or nought.
            new("timer", Wearing(FolderFaces.TimerId, timer: true)),
            new("focus", Folder(FolderFaces.FocusId)),
            new("stopwatch", Folder(FolderFaces.StopwatchId)),
            // The mobile hotspot's antenna, on the samples' made-up hotspot (on, two devices on it).
            new("hotspot", Folder(FolderFaces.HotspotId)),
            // The battery's charge, as this PC reads it (none: the power bolt), and the power glyph.
            new("battery", Folder(FolderFaces.BatteryId)),
            new("power", Folder(FolderFaces.PowerId)),
            // The mail's envelope, over Letters: three unread on its corner.
            new("mail", Folder(FolderFaces.MailId)),
            // What is playing, the stand-in's track: the art's note and the title over the artist
            // where two lines fit; and the words alone.
            new("now playing", Folder(FolderFaces.NowPlayingId)),
            new("now playing, no art", Folder(FolderFaces.NowPlayingId, (NowPlayingFace.Art, "false"))),
        };
        return samples;
    }

    /// <summary>
    /// What the Wi-Fi and Bluetooth faces show in every sheet: two bars of four, the arcs the signal
    /// does not reach faded, with a VPN's shield; Bluetooth on. Never this PC's.
    /// </summary>
    internal static readonly SystemState Radios = SystemStatus.Compose(
        new SystemStatus.Connection(SystemStatus.Medium.WiFi, true, "Home", 2),
        new SystemStatus.Audio(true, "Speakers", 40, false),
        bluetooth: 1,
        vpn: "Work");

    /// <summary>What the removable face shows in every sheet: two sticks, so its count stands on it. Never this PC's.</summary>
    internal static readonly Drive[] Sticks =
    [
        new() { Letter = 4, Kind = DriveKind.Removable, Volume = @"\\?\Volume{7e570004-0000-0000-0000-000000000000}\", Label = "SANDISK", Total = 30_000_000_000, Free = 12_000_000_000 },
        new() { Letter = 5, Kind = DriveKind.Removable, Volume = @"\\?\Volume{7e570005-0000-0000-0000-000000000000}\", Label = "SD Card", Total = 64_000_000_000, Free = 60_000_000_000 },
    ];

    /// <summary>What the mail face shows in every sheet: an inbox with three unread. Never an account's.</summary>
    internal static readonly Karpo.Dock.Pim.MailStore Letters = MadeLetters();

    private static Karpo.Dock.Pim.MailStore MadeLetters()
    {
        var store = new Karpo.Dock.Pim.MailStore(new Karpo.Dock.Pim.AccountSet([]));
        store.Answered([new("1", "Sandra Gerber", "sandra@example.ch", "Lunch on Friday?", null, DateTimeOffset.Now, true)], unread: 3);
        return store;
    }

    internal static BarItem Folder(string face, params (string Key, string Value)[] options) => Wearing(face, options: options);

    internal static BarItem Wearing(string face, string glyph = "", bool timer = false, (string Key, string Value)[]? options = null)
    {
        var island = BarKind.FolderList.New();
        island.Id = "face";
        var folder = island.Folder!;
        folder.Face = FolderFace.Of(face);
        folder.Face.Glyph = glyph;
        foreach (var (key, value) in options ?? [])
        {
            folder.Face.Set(folder.Face.Info.Option(key) ?? throw new ArgumentException(key), value);
        }
        foreach (var kind in timer ? new[] { ListKind.Clock, ListKind.Timer } : [ListKind.Clock, ListKind.Battery])
        {
            var line = kind.New();
            line.Id = kind.ToString().ToLowerInvariant();
            folder.Put(line);
        }
        island.Normalize();
        return island;
    }

    private static readonly int[] Heights = [32, 40, 48, 56, 64];

    private static IslandStyle Style(int height, uint dpi, Look look, bool vertical = false)
    {
        var config = new Config { Height = height };
        return new IslandStyle(dpi / 96f, vertical, Layout.CellPx(dpi, config), Layout.IconPx(dpi, config), look);
    }

    private static Look HighContrast() => new(
        Glass: false,
        Dark: true,
        Accent: Windows.UI.Color.FromArgb(255, 0x00, 0x78, 0xD4),
        HighContrast: true,
        HcWindow: Windows.UI.Color.FromArgb(255, 0, 0, 0),
        HcText: Windows.UI.Color.FromArgb(255, 0xFF, 0xFF, 0xFF),
        HcHighlight: Windows.UI.Color.FromArgb(255, 0x1A, 0xEB, 0xFF),
        HcHighlightText: Windows.UI.Color.FromArgb(255, 0, 0, 0));

    internal static FolderIsland Make(Gfx gfx, BarItem folder)
    {
        FolderIsland.StatusStand = Radios;
        FolderIsland.DevicesStand = Sticks;
        Karpo.Dock.Pim.MailIsland.Stand = Letters;
        // The now-playing face's track: never this PC's.
        MediaIsland.Stand = new MediaStand();
        try
        {
            return new(Config.IslandKey(null, folder.Id), folder, gfx, new Config());
        }
        finally
        {
            FolderIsland.StatusStand = null;
            FolderIsland.DevicesStand = null;
            Karpo.Dock.Pim.MailIsland.Stand = null;
            MediaIsland.Stand = null;
        }
    }

    /// <summary>The button's length along the bar, as the layout is told it.</summary>
    private static float Length(FolderIsland island, in IslandStyle style) => island.CellSize(0, style) is var (w, h) && style.Vertical ? h : w;

    private static void At(DateTime when) => Clock.Now = () => when;

    /// <summary>
    /// A face is as long as what it shows now (docs\widgets.md §244), and its figures are tabular: so
    /// every face is one length at every moment that reads the same shape — the minutes and the
    /// seconds ticking, another hour of two figures, another Monday of the month — across a bar and
    /// down one. A clock never moves the bar as it ticks.
    /// </summary>
    [UiFact]
    public void AFaceTicksInOneLengthWhileItReadsTheSameShape()
    {
        Offscreen.OnStaThread(gfx =>
        {
            DateTime[] moments = [new(2026, 10, 12, 10, 0, 0), new(2026, 10, 19, 11, 59, 59), new(2026, 10, 26, 10, 11, 11), new(2026, 10, 19, 11, 48, 37)];
            try
            {
                foreach (var sample in Samples())
                {
                    using var island = Make(gfx, sample.Folder);
                    foreach (var vertical in new[] { false, true })
                    {
                        foreach (var height in Heights)
                        {
                            var style = Style(height, 96, LookFor(dark: true), vertical);
                            var lengths = moments.Select(moment =>
                            {
                                At(moment);
                                island.Forget();
                                return Length(island, style);
                            }).Distinct().ToList();
                            Assert.True(lengths.Count == 1, $"{sample.Name}, {height} DIP{(vertical ? ", vertical" : "")}: {string.Join(", ", lengths)} px as the time goes by");
                        }
                    }
                }
            }
            finally
            {
                Clock.Now = Clock.Real;
            }
        });
    }

    /// <summary>
    /// Every face draws inside its cell at every height from 32 to 64 DIP, at 100 and 150 %, dark,
    /// light and in high contrast; across a bar its ink stands in the middle of the cell; a square
    /// face showing no name is one square cell, and a face of text is as long as it reads and no
    /// longer than a cell or two of air.
    /// </summary>
    [UiFact]
    public void EveryFaceDrawsInsideItsCellAtEveryHeight()
    {
        var failures = new List<string>();
        Offscreen.OnStaThread(gfx =>
        {
            At(new DateTime(2026, 12, 23, 10, 30, 0));
            try
            {
                foreach (var sample in Samples())
                {
                    using var island = Make(gfx, sample.Folder);
                    foreach (var (lookName, look) in new[] { ("dark", LookFor(dark: true)), ("light", LookFor(dark: false)), ("high contrast", HighContrast()) })
                    {
                        foreach (var dpi in new uint[] { 96, 144 })
                        {
                            foreach (var vertical in new[] { false, true })
                            {
                                foreach (var height in Heights)
                                {
                                    var style = Style(height, dpi, look, vertical);
                                    var where = $"{sample.Name} at {height} DIP, {dpi} DPI, {lookName}{(vertical ? ", vertical" : "")}";
                                    failures.AddRange(Check(gfx, island, style, where, sample));
                                }
                            }
                        }
                    }
                }
            }
            finally
            {
                Clock.Now = Clock.Real;
            }
        });
        Assert.True(failures.Count == 0, $"{failures.Count} faces do not fit their cell:\n{string.Join("\n", failures.Take(40))}");
    }

    private static List<string> Check(Gfx gfx, FolderIsland island, IslandStyle style, string where, Sample sample)
    {
        var failures = new List<string>();
        island.Forget();
        var (width, height) = island.CellSize(0, style);
        const float margin = 12f;
        (float Left, float Top, float Right, float Bottom) ink = default;
        Offscreen.Bitmap(
            width + (2 * margin),
            height + (2 * margin),
            dc => Offscreen.Shifted(dc, margin, margin, () => island.PaintCell(dc, 0, style, width, height)),
            bitmap => ink = Offscreen.InkOf(bitmap, (uint)MathF.Ceiling(width + (2 * margin)), (uint)MathF.Ceiling(height + (2 * margin))));
        var (left, top, right, bottom) = (ink.Left - margin, ink.Top - margin, ink.Right - margin, ink.Bottom - margin);
        if (right <= left)
        {
            failures.Add($"{where}: draws nothing");
            return failures;
        }
        if (left < -0.5f || top < -0.5f || right > width + 0.5f || bottom > height + 0.5f)
        {
            failures.Add($"{where}: draws {left:0.#},{top:0.#}-{right:0.#},{bottom:0.#} outside its {width:0.#} by {height:0.#} cell");
        }
        if (!style.Vertical && MathF.Abs(((top + bottom) / 2) - (height / 2)) > MathF.Max(2f, 0.1f * height))
        {
            failures.Add($"{where}: its ink's middle is {((top + bottom) / 2) - (height / 2):+0.0;-0.0} px off the cell's");
        }
        var face = FaceDrawings.Of(sample.Folder.Folder!.Face).Measure(new FaceContext(gfx, style, sample.Folder.Folder!, island));
        var along = style.Vertical ? height : width;
        var wanted = FaceWidthTests.Button(style, face.Along + face.Trail);
        if (face.Square && MathF.Abs(along - wanted) > 0.5f)
        {
            failures.Add($"{where}: a square face's button is {along:0.#} px long, its glyph and its ends {wanted:0.#} px (§275)");
        }
        return failures;
    }

    /// <summary>
    /// A clock face is painted again in place when what it shows changes — a new minute, a new
    /// second where it shows them — and not at a tick that changes nothing. A tick that reads the same
    /// shape never asks for the bar to be built again: the figures are tabular (docs\widgets.md §244).
    /// </summary>
    [UiFact]
    public void AClockFaceTicksInPlace()
    {
        Offscreen.OnStaThread(gfx =>
        {
            try
            {
                foreach (var seconds in new[] { false, true })
                {
                    At(new DateTime(2026, 12, 23, 10, 59, 30));
                    var folder = seconds ? Folder(FolderFaces.ClockId, (ClockFace.Seconds, "true")) : Folder(FolderFaces.ClockId);
                    using var island = Make(gfx, folder);
                    Assert.Equal(seconds, island.FaceSeconds);
                    var style = Style(40, 96, LookFor(dark: true));
                    _ = island.Measure(style);
                    _ = island.Surface(0, style);
                    var painted = 0;
                    PaintWatch.Watching = (_, _, color) =>
                    {
                        painted++;
                        return color;
                    };
                    try
                    {
                        // The same moment: nothing to paint.
                        Assert.False(island.FaceTick(gfx));
                        Assert.Equal(0, painted);
                        // A second later: painted again only where the face shows seconds.
                        At(new DateTime(2026, 12, 23, 10, 59, 31));
                        Assert.False(island.FaceTick(gfx));
                        Assert.Equal(seconds, painted > 0);
                        // Eleven o'clock, the same shape: painted, in place.
                        painted = 0;
                        At(new DateTime(2026, 12, 23, 11, 0, 0));
                        Assert.False(island.MinuteChanged(gfx));
                        Assert.True(painted > 0, "a new minute was not painted");
                        Assert.Contains(seconds ? Clock.At(11, 0, 0, true) : Clock.At(11, 0, 0, false), island.FaceShown.Replace(FaceDrawings.Ratio, ':'), StringComparison.Ordinal);
                    }
                    finally
                    {
                        PaintWatch.Watching = null;
                    }
                }
            }
            finally
            {
                Clock.Now = Clock.Real;
            }
        });
    }

    /// <summary>
    /// The clock face is only the clock (the person, 2026-10-09: "the clock face should only show the
    /// clock and not the calendar stuff as well"): it has no option for the date, a file's leftover
    /// "show": "timeDate" from before is kept and shows nothing, and what it shows is the time with
    /// no date in it, as is the clock preset's.
    /// </summary>
    [UiFact]
    public void TheClockFaceShowsOnlyTheClock()
    {
        Assert.Equal(
            [ClockFace.Seconds, ClockFace.Hours, ClockFace.Zone, ClockFace.Label, ClockFace.Timer],
            FolderFaces.Clock.Options.Select(option => option.Key));
        Assert.Empty(Presets.Find(Presets.ClockId)!.Face!.Options);
        Offscreen.OnStaThread(gfx =>
        {
            try
            {
                At(new DateTime(2026, 12, 23, 9, 59, 30));
                var style = Style(48, 96, LookFor(dark: true));
                var plain = Folder(FolderFaces.ClockId);
                var old = Folder(FolderFaces.ClockId);
                old.Folder!.Face.Options["show"] = JsonDocument.Parse("\"timeDate\"").RootElement.Clone();
                Assert.Equal("timeDate", old.Folder!.Face.Options["show"].GetString());
                using var island = Make(gfx, old);
                using var same = Make(gfx, plain);
                Assert.Equal(Length(same, style), Length(island, style));
                _ = island.Surface(0, style);
                Assert.DoesNotContain("2026", island.FaceShown, StringComparison.Ordinal);
                Assert.DoesNotContain(FaceDrawings.Dot, island.FaceShown, StringComparison.Ordinal);
            }
            finally
            {
                Clock.Now = Clock.Real;
            }
        });
    }

    /// <summary>
    /// The calendar turns over at midnight — both its looks — painted in place on the minute's tick,
    /// and not before: a minute to midnight paints nothing new.
    /// </summary>
    [UiFact]
    public void TheCalendarTurnsOverAtMidnight()
    {
        Offscreen.OnStaThread(gfx =>
        {
            try
            {
                foreach (var page in new[] { false, true })
                {
                    At(new DateTime(2026, 12, 31, 23, 58, 0));
                    var folder = page ? Folder(FolderFaces.CalendarId, (CalendarFace.Look, CalendarFace.Page)) : Folder(FolderFaces.CalendarId);
                    using var island = Make(gfx, folder);
                    var style = Style(48, 96, LookFor(dark: false));
                    _ = island.Measure(style);
                    _ = island.Surface(0, style);
                    var before = island.FaceShown;
                    At(new DateTime(2026, 12, 31, 23, 59, 0));
                    Assert.False(island.MinuteChanged(gfx));
                    Assert.Equal(before, island.FaceShown);
                    At(new DateTime(2027, 1, 1, 0, 0, 0));
                    Assert.False(island.MinuteChanged(gfx));
                    Assert.NotEqual(before, island.FaceShown);
                    Assert.Contains("2027-01-01", island.FaceShown, StringComparison.Ordinal);
                }
            }
            finally
            {
                Clock.Now = Clock.Real;
            }
        });
    }

    /// <summary>
    /// A clock face whose folder holds a timer keeps no room for what it has left while none runs
    /// (docs\widgets.md §244): as long as a clock with none, and as one set not to show it.
    /// </summary>
    [UiFact]
    public void AClockFaceKeepsNoRoomForATimerThatIsNotRunning()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var style = Style(48, 96, LookFor(dark: true));
            float LengthOf(BarItem folder)
            {
                using var island = Make(gfx, folder);
                return Length(island, style);
            }
            var plain = LengthOf(Folder(FolderFaces.ClockId));
            var timed = LengthOf(Wearing(FolderFaces.ClockId, timer: true));
            var off = LengthOf(Wearing(FolderFaces.ClockId, timer: true, options: [(ClockFace.Timer, "false")]));
            Assert.Equal(plain, timed);
            Assert.Equal(plain, off);
        });
    }

    // --- The sheets -----------------------------------------------------------------------------

    /// <summary>
    /// Every face and look on a bar of 40 and 48 DIP, dark and light, each on its card with its
    /// name over it; and one more sheet of the thinnest and the tallest bars, a vertical bar, 150 %
    /// and high contrast.
    /// </summary>
    [UiFact]
    public void FaceSheets()
    {
        Offscreen.OnStaThread(gfx =>
        {
            At(new DateTime(2026, 10, 9, 14, 7, 42));
            try
            {
                var samples = Samples();
                var made = samples.Select(sample => (sample.Name, Island: Make(gfx, sample.Folder))).ToList();
                try
                {
                    foreach (var height in new[] { 40, 48 })
                    {
                        foreach (var dark in new[] { true, false })
                        {
                            var look = LookFor(dark);
                            Sheet(gfx, look, made.Select(item => (item.Name, item.Island, Style(height, 96, look))).ToList(), $"folder-faces-{height}-{(dark ? "dark" : "light")}.png");
                        }
                    }
                    var more = new List<(string, FolderIsland, IslandStyle)>();
                    foreach (var (name, island) in made)
                    {
                        more.Add(($"{name} 32", island, Style(32, 96, LookFor(dark: true))));
                        more.Add(($"{name} 64", island, Style(64, 96, LookFor(dark: true))));
                        more.Add(($"{name} 48 @150%", island, Style(48, 144, LookFor(dark: true))));
                        more.Add(($"{name} HC", island, Style(48, 96, HighContrast())));
                        more.Add(($"{name} vertical", island, Style(48, 96, LookFor(dark: true), vertical: true)));
                    }
                    Sheet(gfx, LookFor(dark: true), more, "folder-faces-more.png");
                }
                finally
                {
                    foreach (var (_, island) in made)
                    {
                        island.Dispose();
                    }
                }
            }
            finally
            {
                Clock.Now = Clock.Real;
            }
        });
    }

    private static void Sheet(Gfx gfx, Look look, List<(string Name, FolderIsland Island, IslandStyle Style)> items, string file)
    {
        const float pad = 14f;
        const float label = 16f;
        const float most = 1500f;
        var theme = FlyoutTheme.For(look);
        var places = new List<(float X, float Y, float W, float H)>();
        var x = pad;
        var y = pad;
        var rowHeight = 0f;
        var widest = 0f;
        foreach (var (_, island, style) in items)
        {
            island.Forget();
            var (w, h) = island.CellSize(0, style);
            var slot = MathF.Max(w, 130f);
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
        Offscreen.Bitmap(widest, total, dc =>
        {
            PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, widest, total), 0, look.HighContrast ? new DXGI_RGBA(0, 0, 0, 1) : PopupPaint.Ink(theme.Base));
            for (var index = 0; index < items.Count; index++)
            {
                var (name, island, style) = items[index];
                var (left, top, w, h) = places[index];
                PopupPaint.Text(dc, gfx, name, 11f, Weight.Regular, theme.TextSecondary, left, top, 200f);
                var cardTop = top + label;
                var card = style.Look.HighContrast ? new DXGI_RGBA(0, 0, 0, 1) : PopupPaint.Ink(theme.CardBackground);
                PopupPaint.Fill(dc, PopupPaint.Rect(left, cardTop, w, h), 4f * style.Scale, card);
                if (style.Look.HighContrast)
                {
                    PopupPaint.Stroke(dc, PopupPaint.Rect(left + 0.5f, cardTop + 0.5f, w - 1, h - 1), 4f, 1f, new DXGI_RGBA(1, 1, 1, 1));
                }
                var style2 = style;
                var island2 = island;
                Offscreen.Shifted(dc, left, cardTop, () => island2.PaintCell(dc, 0, style2, w, h));
            }
        }, bitmap => Offscreen.Save(bitmap, Path.Combine(Offscreen.Pictures, file)));
    }
}
