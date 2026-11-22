// One widget, several styles (2026-10-17, docs\widgets.md §209; Karpo.Core\WidgetStyles.cs). Held here:
//
//  - **The file.** A line is written as its widget's word and its style, and reads back as the same
//    form; a style this build does not know is the widget's first.
//  - **A style changed** keeps everything else: the id, the height, the alignment, every option —
//    one the new style does not read too, so changing back finds it.
//  - **The add list** offers each widget once, as its first style, named as the widget and found by
//    each style's words; **the menu** has a "Style" row whose submenu lists the styles, the one the
//    line is in ticked, and a pick there keeps the folder open.
//  - **Presets** say the widget's word and its style.

using System.Text.Json;
using Karpo.Dock;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Core;

public sealed class WidgetStylesTests
{
    /// <summary>Every form of every widget with styles, by its own word.</summary>
    public static TheoryData<string> Forms()
    {
        var forms = new TheoryData<string>();
        foreach (var style in WidgetStyles.All.SelectMany(widget => widget.Styles))
        {
            forms.Add(ListWidgets.Of(style.Form).Info.Id);
        }
        return forms;
    }

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

    /// <summary>A line reads as the form its style names, and is written back the same.</summary>
    [Theory]
    [MemberData(nameof(Forms))]
    public void TheWordsReadAsTheirForm(string form)
    {
        var (widget, style) = WidgetStyles.Of(form)!.Value;
        var styleWord = WidgetStyles.FileStyle(form) is { } word ? $""", "style": "{word}" """ : "";
        var readingWord = WidgetStyles.FileReading(form) is { } measures ? $""", "reading": "{measures}" """ : "";
        var config = Configs.Repaired(File($$"""{ "island": { "id": "it", "kind": "{{widget.Id}}"{{readingWord}}{{styleWord}}, "height": { "fixed": true, "cells": 4 } } }"""));

        Assert.Equal(style.Form, Lines(config).Single().List);
        _ = Configs.RoundTrip(config, out var changed);
        Assert.False(changed);
    }

    /// <summary>The style may come before the kind in a file, as a hand may write it; a style this build does not know is the widget's first.</summary>
    [Fact]
    public void TheStyleIsReadWhereverItStandsAndAnUnknownOneIsTheFirst()
    {
        var config = Configs.Repaired(File("""
            { "island": { "id": "a", "style": "year", "kind": "calendar" } },
            { "island": { "id": "b", "kind": "calendar", "style": "fortnight" } },
            { "island": { "id": "c", "kind": "dailyForecast", "style": "graph" } }
            """));

        Assert.Equal([ListKind.YearView, ListKind.Calendar, ListKind.WeatherDaysGraph], Lines(config).Select(line => line.List!.Value));
    }

    /// <summary>
    /// A style changed keeps the line's id, its height and its options, one the new
    /// style does not read too; changed back, the line is what it was.
    /// </summary>
    [Fact]
    public void AStyleChangedKeepsEverythingElse()
    {
        var clock = ListKind.Clock.New();
        clock.Id = "tokyo";
        clock.Clock!.TimeZone = "Tokyo Standard Time";
        clock.Clock.Label = "Tokyo";
        clock.Size = LineSize.Compact;
        var was = JsonSerializer.Serialize(clock, ConfigJson.Default.CellItem);

        Assert.True(WidgetStyles.Restyle(clock, WidgetStyles.Clock.StyleOf("time")));
        Assert.Equal(ListKind.TimeHeader, ListWidgets.Find(clock.Kind)!.Kind);
        Assert.Equal(("tokyo", (LineSize?)LineSize.Compact), (clock.Id, clock.Size));
        // The time and date reads no label, and keeps it for when it is a clock again.
        Assert.Equal(("Tokyo Standard Time", "Tokyo"), (clock.Clock?.TimeZone, clock.Clock?.Label));
        Assert.Equal(("clock", "time"), (clock.FileKind, clock.FileStyle));

        Assert.True(WidgetStyles.Restyle(clock, WidgetStyles.Clock.Default));
        Assert.Equal(was, JsonSerializer.Serialize(clock, ConfigJson.Default.CellItem));
    }

    /// <summary>
    /// perf: a graph following a graphics card, made the processor's and the graphics' again, follows
    /// the same card. (The microphones' hidden devices were §209's case too: the microphones are no
    /// style since §270, StylesSameDataTests.)
    /// </summary>
    [Fact]
    public void AnOptionOnlyOneStyleReadsIsKeptByTheOthers()
    {
        var graph = ListKind.Gpu.New();
        graph.Performance!.GpuAdapter = "Radeon";
        Assert.True(WidgetStyles.Restyle(graph, WidgetStyles.Performance.StyleOf(WidgetStyles.GraphWord, "cpu")));
        var read = Configs.Repaired(File($$"""{ "island": {{JsonSerializer.Serialize(graph, ConfigJson.Default.CellItem)}} }"""));
        var line = Lines(read).Single();
        Assert.Equal((ListKind.Cpu, "Radeon"), (line.List!.Value, line.Performance!.GpuAdapter));
        Assert.True(WidgetStyles.Restyle(line, WidgetStyles.Performance.StyleOf(WidgetStyles.GraphWord, "gpu")));
        Assert.Equal("Radeon", line.Performance.GpuAdapter);
    }

    /// <summary>A style is changed only to one of the widget's own, and not to the one it is in.</summary>
    [Fact]
    public void AStyleIsOnlyEverTheWidgetsOwn()
    {
        var week = ListKind.WeekView.New();
        Assert.False(WidgetStyles.Restyle(week, WidgetStyles.Daily.StyleOf("graph")));
        Assert.False(WidgetStyles.Restyle(week, WidgetStyles.Calendar.StyleOf("week")));
        Assert.False(WidgetStyles.Restyle(ListKind.Battery.New(), WidgetStyles.Calendar.StyleOf("week")));
        Assert.Equal(ListKind.WeekView, ListWidgets.Find(week.Kind)!.Kind);
    }

    /// <summary>
    /// The add list offers each widget with styles once, as its first style, named as the widget and
    /// found by each style's words; the other styles are no rows of their own. A widget with readings
    /// is a row a reading, its first style, named as the reading and found by that reading's styles'
    /// words alone (§283).
    /// </summary>
    [Fact]
    public void TheAddListOffersEachWidgetOnceAReading()
    {
        var rows = Catalogue.On(WidgetType.List);
        var picks = AddTree.Picks(AddTree.On(WidgetType.List).SelectMany(section => section.Rows)).Select(pick => pick.Key).ToHashSet(StringComparer.Ordinal);
        foreach (var widget in WidgetStyles.All)
        {
            var its = rows.Where(row => row.List is { } form && WidgetStyles.Of(form)?.Widget == widget).ToList();
            Assert.Equal(widget.Offered.Select(style => (ListKind?)style.Form), its.Select(row => row.List));
            foreach (var (row, offered) in its.Zip(widget.Offered))
            {
                // §318: a reading with no style left to choose is its form's row ("Graphics details").
                Assert.Equal(
                    offered.Reading is not { } reading ? widget.Title : WidgetStyles.Chooses(widget, offered) ? widget.ReadingTitle(reading) : ListWidgets.Of(offered.Form).Info.Title,
                    row.Title);
                Assert.Contains(row.Key, picks);
                foreach (var style in widget.Styles)
                {
                    Assert.Equal(style.Reading == offered.Reading, row.Keywords.Contains(ListWidgets.Of(style.Form).Info.Title, StringComparer.CurrentCultureIgnoreCase));
                    if (!widget.Offered.Contains(style))
                    {
                        Assert.DoesNotContain(CatalogueEntry.KeyOf(WidgetType.List, ListWidgets.Of(style.Form).Info.Id), picks);
                    }
                }
            }
        }
        Assert.Equal(["Calendar", "Hourly forecast", "Daily forecast", "Performance", "Performance details", "Clock", "Sunrise and sunset"], WidgetStyles.All.Select(widget => widget.Title));
        Assert.Equal(["Processor", "Graphics", "Memory", "Disk", "Network activity"], WidgetStyles.Performance.Offered.Select(WidgetStyles.Performance.TitleOf));
        Assert.Equal(["Processor details", "Graphics details", "Memory details", "Disk details", "Network details"],
            WidgetStyles.PerformanceDetails.Offered.Select(WidgetStyles.PerformanceDetails.TitleOf));
    }

    /// <summary>
    /// A line's menu has a "Style" row whose submenu is its widget's styles, the one it is in ticked;
    /// a pick names that style, and keeps the folder open, as a change made where the line stands.
    /// A widget of one style has no such row.
    /// </summary>
    [Fact]
    public void TheMenusStyleRowTicksTheStyleTheLineIsIn()
    {
        // The style is one of the line's settings in its menu (Karpo.Core\OptionRows.cs, §214), after the
        // link to the accounts it reads (§351).
        var week = ListKind.WeekView.New();
        List<OptionPick> picks = [];
        var row = OptionMenu.Rows(OptionRows.Of(week), picks).First(item => item.Text == "Style");
        Assert.Equal("Style", row.Text);
        Assert.Equal(["Agenda", "Day's events", "Day", "Week", "Month", "Year"], row.Children!.Select(child => child.Text));
        Assert.Equal([false, false, false, true, false, false], row.Children!.Select(child => child.Checked == true));
        Assert.All(row.Children!, child => Assert.True(OptionMenu.Holds(child.Id)));
        Assert.False(TheDock.PickHandsAway([], row.Children![4].Id));
        // A pick restyles the line where it stands; its own style again changes nothing.
        Assert.True(OptionMenu.Apply(week, OptionMenu.PickOf(picks, row.Children![4].Id)!));
        Assert.Equal(ListKind.Month, WidgetStyles.Of(week)!.Value.Style.Form);
        Assert.False(OptionMenu.Apply(week, OptionMenu.PickOf(picks, row.Children![4].Id)!));
        Assert.DoesNotContain(OptionRows.Of(ListKind.Battery.New()), option => option.Key == OptionRows.Style);
        // A folder has no style: its rows are its own name and widths (§219).
        Assert.DoesNotContain(OptionRows.Of(BarKind.FolderList.New()), option => option.Key == OptionRows.Style);
    }

    /// <summary>A preset's lines say the widget's word and its style, never a form's own word.</summary>
    [Fact]
    public void PresetsSayTheWidgetAndItsStyle()
    {
        var forms = WidgetStyles.All.SelectMany(widget => widget.Styles).Select(style => ListWidgets.Of(style.Form).Info.Id)
            .Except(WidgetStyles.All.Select(widget => widget.Id)).ToHashSet(StringComparer.Ordinal);
        foreach (var preset in Presets.All)
        {
            Assert.All(preset.Items, item => Assert.DoesNotContain(item.Kind, forms));
            // What its folder writes: a line's "kind" (its face's is a face's word, "cpu" among them).
            Assert.All(Presets.Folder(preset, PresetsTests.Laptop).Folder!.Cells, cell => Assert.DoesNotContain(cell.Island.FileKind, forms));
        }
        var calendar = Presets.Find(Presets.CalendarId)!;
        Assert.Contains(calendar.Items, item => item is { Kind: "calendar", Style: "month" } && item.Form == "month");
    }

    /// <summary>The page in Settings has its style to choose, so a widget with styles is never one with nothing else to set.</summary>
    [Fact]
    public void EveryStyleBelongsToOneWidgetAndOneForm()
    {
        var styles = WidgetStyles.All.SelectMany(widget => widget.Styles).ToList();
        Assert.Equal(styles.Count, styles.Select(style => style.Form).Distinct().Count());
        Assert.All(WidgetStyles.All, widget =>
        {
            Assert.Equal(widget.Styles.Count, widget.Styles.Select(style => (style.Reading, style.Word)).Distinct().Count());
            // A widget with readings has every style for every reading (§228).
            Assert.All(widget.Readings, reading => Assert.Equal(widget.StylesBeside(widget.Default).Select(style => style.Word), widget.StylesBeside(widget.StyleOf(null, reading)).Select(style => style.Word)));
            Assert.All(widget.Styles, style => Assert.Equal(widget.Id, style.Of));
            // A widget's word is no other form's: "calendar" and "clock" are their first styles' own.
            Assert.True(ListWidgets.Find(widget.Id) is null || ListWidgets.Find(widget.Id)!.Kind == widget.Default.Form, widget.Id);
        });
    }
}
