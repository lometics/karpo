// A style shows the same thing (2026-10-20, docs\widgets.md §270, §318; Karpo.Core\WidgetStyles.cs). The
// person: "a style should not be input or output device, these dont show the same type of data ... only
// same data shown (or similar data, sometimes more verbose or less verbose) should be combined in the
// style property"; and (§318) "the volume slider and volume mixer ... show something different / have
// different behaviour. widgets like these should therefore not be grouped together". Held here:
//
//  - **Every style of a widget shows its one subject**, by an explicit list of what each form shows: a
//    form a widget is given as a style must be listed, and every style of a widget the same subject. A
//    widget that picks a device is never a style.
//  - **Widgets of their own**: the output devices and the microphones (§270), the volume and the mixer,
//    today's date, and the performance details (§318), each in the add list as rows of their own, none
//    with a Style row.
//  - **The file**: each of them is read and written by its own word.
//  - **Presets** say each widget by its own word.

using System.Text.Json;
using Karpo.Dock;

namespace Karpo.Tests.Core;

public sealed class StylesSameDataTests
{
    /// <summary>
    /// What each form a widget has as a style shows, by subject: its styles draw that subject more or
    /// less fully. A new style is listed here before it is one, with what it shows.
    /// </summary>
    private static readonly Dictionary<ListKind, string> Shows = new()
    {
        // The calendar, its days and their events: listed (the agenda, a day's), laid on a day's hours or a week's, a
        // month's days, a year's (its days alone, the least of it).
        [ListKind.Calendar] = "calendar", [ListKind.Events] = "calendar", [ListKind.DayView] = "calendar",
        [ListKind.WeekView] = "calendar", [ListKind.Month] = "calendar", [ListKind.YearView] = "calendar",
        // §590: one place's hours, today's on a curve or the next ones in a row; its days ahead, in a list or on a graph.
        [ListKind.WeatherHours] = "hours", [ListKind.WeatherHoursList] = "hours",
        [ListKind.WeatherDays] = "days", [ListKind.WeatherDaysGraph] = "days",
        // How busy a part of the PC is, as a graph or its figure (the reading is which part, no style).
        [ListKind.Cpu] = "usage", [ListKind.Gpu] = "usage", [ListKind.Memory] = "usage", [ListKind.Disk] = "usage", [ListKind.Network] = "usage",
        // The facts behind a part's figure: its speed, its processes, the memory in use, what it reads and sends.
        [ListKind.CpuDetails] = "facts", [ListKind.GpuDetails] = "facts", [ListKind.MemoryDetails] = "facts",
        [ListKind.DiskDetails] = "facts", [ListKind.NetworkDetails] = "facts",
        // Now, in a zone: the time, the time with its seconds over the date.
        [ListKind.Clock] = "now", [ListKind.TimeHeader] = "now",
        // One place's sun today (§368): its path, its day as a bar; each says the sunrise, the daylight and the sunset.
        [ListKind.SunArc] = "sun", [ListKind.SunDayBar] = "sun",
    };

    /// <summary>A file with one list folder, its lines <paramref name="lines"/> (JSON, comma-separated).</summary>
    private static string File(string lines) => $$"""
        { "version": {{Config.Shape}}, "layouts": [ { "id": "L", "islands": [
          { "id": "f", "kind": "folderList", "section": "end", "folder": { "lists": [ [ {{lines}} ] ] } } ] } ] }
        """;

    private static List<CellItem> Lines(Config config) =>
        [.. config.Layouts.Single().Islands.Single().Folder!.Cells.Select(cell => cell.Island)];

    /// <summary>The file's line <paramref name="id"/>, as JSON.</summary>
    private static JsonElement Written(Config config, string id)
    {
        using var document = JsonDocument.Parse(config.ToJson());
        foreach (var cell in document.RootElement.GetProperty("layouts")[0].GetProperty("islands")[0].GetProperty("folder").GetProperty("lists")[0].EnumerateArray())
        {
            var island = cell.GetProperty("island");
            if (island.GetProperty("id").GetString() == id)
            {
                return island.Clone();
            }
        }
        throw new Xunit.Sdk.XunitException($"no line {id} in the file");
    }

    private static string? Word(JsonElement line, string key) => line.TryGetProperty(key, out var word) ? word.GetString() : null;

    /// <summary>
    /// Every style of every widget is a form listed with what it shows, and a widget's styles all
    /// show one subject; no style picks a device (the output devices, the microphones).
    /// </summary>
    [Fact]
    public void EveryStyleOfAWidgetShowsTheSameThing()
    {
        foreach (var widget in WidgetStyles.All)
        {
            Assert.All(widget.Styles, style => Assert.True(Shows.ContainsKey(style.Form), $"{widget.Id}'s {style.Word} ({style.Form}) is no form listed with what it shows"));
            Assert.Single(widget.Styles.Select(style => Shows[style.Form]).Distinct());
            Assert.All(widget.Styles, style => Assert.False(WidgetItem.HidesDevices(ListWidgets.Of(style.Form).Info.Id), $"{widget.Id}'s {style.Word} picks a device"));
        }
        // Each subject is one widget's: two widgets of one subject would be one widget's styles.
        Assert.Equal(WidgetStyles.All.Length, WidgetStyles.All.Select(widget => Shows[widget.Default.Form]).Distinct().Count());
    }

    /// <summary>The styles as the audits leave them (§270, §318): what each widget may be shown as, in the order offered.</summary>
    [Fact]
    public void TheStylesAreTheAuditsKeptOnes()
    {
        Assert.Equal(
            [
                "calendar: agenda events day week month year",
                "hourlyForecast: graph list",
                "dailyForecast: list graph",
                "performance: graph",
                "performanceDetails: details",
                "clock: clock time",
                "sun: arc dayBar",
            ],
            WidgetStyles.All.Select(widget => $"{widget.Id}: {string.Join(' ', widget.Styles.Select(style => style.Word).Distinct())}"));
        Assert.Equal(["Calendar", "Hourly forecast", "Daily forecast", "Performance", "Performance details", "Clock", "Sunrise and sunset"], WidgetStyles.All.Select(widget => widget.Title));
        Assert.Equal(["Graph", "List"], WidgetStyles.Hourly.Styles.Select(style => style.Title));
        Assert.Equal(["List", "Graph"], WidgetStyles.Daily.Styles.Select(style => style.Title));
        Assert.Equal(["Clock", "Time and date"], WidgetStyles.Clock.Styles.Select(style => style.Title));
        foreach (var own in new[] { ListKind.SoundOutput, ListKind.SoundInput, ListKind.SoundVolume, ListKind.SoundMixer, ListKind.DateHeader })
        {
            Assert.Null(WidgetStyles.Of(own));
        }
        Assert.Null(WidgetStyles.Find("sound"));
    }

    /// <summary>The split widgets are read by their own words, and written back the same.</summary>
    [Theory]
    [InlineData("""{ "island": { "id": "it", "kind": "soundOutput" } }""", ListKind.SoundOutput)]
    [InlineData("""{ "island": { "id": "it", "kind": "soundInput", "hiddenDevices": [ "mic" ] } }""", ListKind.SoundInput)]
    [InlineData("""{ "island": { "id": "it", "kind": "soundVolume" } }""", ListKind.SoundVolume)]
    [InlineData("""{ "island": { "id": "it", "kind": "soundMixer" } }""", ListKind.SoundMixer)]
    [InlineData("""{ "island": { "id": "it", "kind": "dateHeader", "clock": { "timeZone": "Tokyo Standard Time" } } }""", ListKind.DateHeader)]
    [InlineData("""{ "island": { "id": "it", "kind": "performanceDetails", "reading": "gpu" } }""", ListKind.GpuDetails)]
    public void TheSplitWidgetsAreReadByTheirOwnWords(string line, ListKind form)
    {
        var config = Configs.Repaired(File(line));

        Assert.Equal(form, Lines(config).Single().List);
        _ = Configs.RoundTrip(config, out var changed);
        Assert.False(changed);
    }

    /// <summary>
    /// The add list has every widget split off as rows of their own, named, iconed and found as
    /// themselves: the devices, the volume and the mixer, today's date, and the details a reading each.
    /// </summary>
    [Fact]
    public void TheAddListHasEachSplitWidgetAsARowOfItsOwn()
    {
        var rows = Catalogue.On(WidgetType.List);
        var picks = AddTree.Picks(AddTree.On(WidgetType.List).SelectMany(section => section.Rows)).Select(pick => pick.Key).ToHashSet(StringComparer.Ordinal);
        foreach (var (form, title, word) in new[]
        {
            (ListKind.SoundOutput, "Output device", "headphones"), (ListKind.SoundInput, "Input device", "microphone"),
            (ListKind.SoundVolume, "Volume", "volume"), (ListKind.SoundMixer, "Volume mixer", "mixer"), (ListKind.DateHeader, "Today's date", "today"),
            (ListKind.CpuDetails, "Processor details", "threads"), (ListKind.GpuDetails, "Graphics details", "gpu"), (ListKind.MemoryDetails, "Memory details", "ram"),
            (ListKind.DiskDetails, "Disk details", "disk"), (ListKind.NetworkDetails, "Network details", "network"),
        })
        {
            var info = ListWidgets.Of(form).Info;
            var row = Assert.Single(rows, row => row.List == form);
            Assert.Equal((title, info.Glyph), (row.Title, row.Glyph));
            Assert.False(string.IsNullOrWhiteSpace(row.Glyph));
            Assert.False(string.IsNullOrWhiteSpace(row.Blurb));
            Assert.Contains(row.Key, picks);
            Assert.True(CatalogueSearch.Matches(word, row.Title, row.Keywords), $"\"{word}\" finds no {title}");
        }
        // The volume is found by no microphone and no mixer; the mixer by its apps.
        var volume = Assert.Single(rows, row => row.List == ListKind.SoundVolume);
        Assert.False(CatalogueSearch.Matches("microphone", volume.Title, volume.Keywords));
        Assert.False(CatalogueSearch.Matches("mixer", volume.Title, volume.Keywords));
        // A reading's graph and its details are two rows: "Processor" and "Processor details".
        Assert.Equal("Processor", Assert.Single(rows, row => row.List == ListKind.Cpu).Title);
    }

    /// <summary>
    /// None of the widgets split off has a Style row: the volume, the mixer, the devices, today's
    /// date; the details have their reading alone, which keeps them details.
    /// </summary>
    [Fact]
    public void NoSplitWidgetHasAStyleRow()
    {
        foreach (var form in new[] { ListKind.SoundOutput, ListKind.SoundInput, ListKind.SoundVolume, ListKind.SoundMixer, ListKind.DateHeader, ListKind.CpuDetails })
        {
            Assert.DoesNotContain(OptionRows.Of(form.New()), option => option.Key == OptionRows.Style);
        }
        Assert.False(WidgetStyles.Restyle(ListKind.SoundOutput.New(), WidgetStyles.Clock.Default));

        var details = ListKind.CpuDetails.New();
        List<OptionPick> picks = [];
        var reading = OptionMenu.Rows(OptionRows.Of(details), picks).First();
        Assert.Equal("Reading", reading.Text);
        Assert.Equal(["Processor", "Graphics", "Memory", "Disk", "Network activity"], reading.Children!.Select(child => child.Text));
        Assert.True(OptionMenu.Apply(details, OptionMenu.PickOf(picks, reading.Children![2].Id)!));
        Assert.Equal(ListKind.MemoryDetails, details.List);

        // The clock's Style is its looks (§443) and the time and date; no date.
        List<OptionPick> clockPicks = [];
        var style = Assert.Single(OptionMenu.Rows(OptionRows.Of(ListKind.Clock.New()), clockPicks), row => row.Text == "Style");
        Assert.Equal(["Analog and digital", "Time only", "Stacked", "Analog", "Time and date"], style.Children!.Select(child => child.Text));
    }

    /// <summary>The presets say each split widget by its own word: the devices, the volume, the mixer, today's date and the details.</summary>
    [Fact]
    public void PresetsSayTheSplitWidgetsByTheirOwnWords()
    {
        foreach (var id in new[] { Presets.SoundId, Presets.MediaCenterId })
        {
            var preset = Presets.Find(id)!;
            Assert.Contains(preset.Items, item => item is { Kind: "soundOutput", Style: null });
            Assert.Contains(preset.Items, item => item is { Kind: "soundMixer", Style: null });
            Assert.Contains(Presets.Folder(preset, PresetsTests.Laptop).Folder!.Cells, cell => cell.Island.FileKind == "soundOutput" && cell.Island.FileStyle is null);
        }
        Assert.Contains(Presets.Find(Presets.SoundId)!.Items, item => item.Kind == "soundInput");
        // No preset says a widget that is gone, or a style that is a widget of its own.
        Assert.All(Presets.All, preset => Assert.DoesNotContain(preset.Items, item => item.Kind == "sound" || (item.Kind, item.Style) is ("clock", "date") or ("performance", "details")));
        Assert.Contains(Presets.Find(Presets.CalendarId)!.Items, item => item is { Kind: "dateHeader", Style: null });
        Assert.Contains(Presets.Find(Presets.CpuId)!.Items, item => item is { Kind: "performanceDetails", Reading: "cpu", Style: null });
        Assert.Equal(
            ["heading", "soundOutput", "soundInput", "spatialSound", "divider", "soundMixer", "actions"],
            FolderPages.Cells(FolderPages.Of(ListKind.SoundVolume)!).Select(cell => cell.Island.Kind));
    }
}
