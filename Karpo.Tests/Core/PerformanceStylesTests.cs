// The performance widget's reading and style (2026-10-19, docs\widgets.md §228; Karpo.Core\WidgetStyles.cs).
// Held here:
//
//  - **The file.** A performance line is the one widget with its reading and its style ("performance",
//    "reading": "gpu", "style": "graph"); the performance details say their reading alone (§318).
//    Words this build does not know read tolerantly.
//  - **Two choices that do not touch each other**: the reading picked keeps the style (the figure alone
//    included), the style picked keeps the reading.
//  - **The pace ladder**: a second to a minute, any other number the nearest step.

using System.Text.Json;

namespace Karpo.Tests.Core;

public sealed class PerformanceStylesTests
{
    private static string File(string lines) => $$"""
        { "version": {{Config.Shape}}, "layouts": [ { "id": "L", "islands": [
          { "id": "f", "kind": "folderList", "section": "end", "folder": { "lists": [ [ {{lines}} ] ] } } ] } ] }
        """;

    private static List<CellItem> Lines(Config config) =>
        [.. config.Layouts.Single().Islands.Single().Folder!.Cells.Select(cell => cell.Island)];

    /// <summary>
    /// A performance line's words and what they read as: its form, and the file's kind, reading and
    /// style — a reading's details the performance details, with no style (§318). The words may stand
    /// in any order; a widget's word alone is its first reading's first style, and a reading or a style
    /// this build does not know is the first.
    /// </summary>
    [Theory]
    [InlineData(""" "kind": "performance" """, ListKind.Cpu, "performance", "cpu", "graph")]
    [InlineData(""" "kind": "performance", "reading": "gpu", "style": "graph" """, ListKind.Gpu, "performance", "gpu", "graph")]
    [InlineData(""" "style": "graph", "reading": "network", "kind": "performance" """, ListKind.Network, "performance", "network", "graph")]
    [InlineData(""" "kind": "performanceDetails" """, ListKind.CpuDetails, "performanceDetails", "cpu", null)]
    [InlineData(""" "kind": "performanceDetails", "reading": "disk" """, ListKind.DiskDetails, "performanceDetails", "disk", null)]
    [InlineData(""" "kind": "performance", "reading": "fan", "style": "graph" """, ListKind.Cpu, "performance", "cpu", "graph")]
    [InlineData(""" "kind": "performance", "reading": "gpu", "style": "sparkline" """, ListKind.Gpu, "performance", "gpu", "graph")]
    public void TheWordsReadAsTheWidgetWithItsReadingAndStyle(string words, ListKind form, string kind, string reading, string? style)
    {
        var config = Configs.Repaired(File($$"""
            { "island": { "id": "above", "kind": "divider" } },
            { "island": { "id": "it", {{words}}, "height": { "fixed": true, "cells": 8 }, "performance": { "showGraph": false, "seconds": 5, "gpuAdapter": "GPU 1: Arc", "disk": "Disk 1" } } },
            { "island": { "id": "below", "kind": "battery" } }
            """));

        var line = Lines(config)[1];
        // The graphs keep their height; the details, a kind of a few forms (§432), keep none.
        Assert.Equal(("it", form, form.Sized() ? (LineHeight?)null : LineHeight.Exactly(8)), (line.Id, line.List!.Value, line.Height));
        Assert.Equal((false, 5), (line.Performance!.ShowGraph, line.Performance.Seconds));
        Assert.Equal((kind, reading, style), (line.FileKind, line.FileReading, line.FileStyle));

        // Read again: the same line.
        var again = Configs.Repaired(config.ToJson(), out var twice);
        Assert.False(twice);
        Assert.Equal(["above", "it", "below"], Lines(again).Select(cell => cell.Id));
        Assert.Equal(JsonSerializer.Serialize(line, ConfigJson.Default.CellItem), JsonSerializer.Serialize(Lines(again)[1], ConfigJson.Default.CellItem));
    }

    /// <summary>A file's words read as written, and a save writes them back the same.</summary>
    [Theory]
    [InlineData(""" "kind": "performance", "reading": "disk", "style": "graph" """, ListKind.Disk)]
    [InlineData(""" "kind": "performanceDetails", "reading": "disk" """, ListKind.DiskDetails)]
    public void TodaysWordsNeedNothing(string words, ListKind form)
    {
        var config = Configs.Repaired(File($$"""{ "island": { "id": "it", {{words}} } }"""));
        Assert.Equal(form, Lines(config).Single().List);
        _ = Configs.RoundTrip(config, out var changed);
        Assert.False(changed);
    }

    /// <summary>The reading picked keeps the style — the figure alone too — and the style picked keeps the reading.</summary>
    [Fact]
    public void TheReadingAndTheStyleAreIndependent()
    {
        var line = ListKind.Cpu.New();
        line.Id = "x";
        line.Size = LineSize.Full;
        Assert.Equal(["Processor", "Graphics", "Memory", "Disk", "Network activity"], WidgetStyles.ReadingNames(line));
        Assert.Equal(["Graph", "Numbers"], WidgetStyles.LookNames(line));
        Assert.Equal((0, 0), (WidgetStyles.ReadingIndex(line), WidgetStyles.LookIndex(line)));

        // The figure alone: the graph's form, drawing none.
        Assert.True(WidgetStyles.ChooseLook(line, 1));
        Assert.Equal((ListKind.Cpu, false), (line.List!.Value, line.Performance!.ShowGraph));
        // Another reading: still the figure alone.
        Assert.True(WidgetStyles.ChooseReading(line, 1));
        Assert.Equal((ListKind.Gpu, 1, 1), (line.List!.Value, WidgetStyles.ReadingIndex(line), WidgetStyles.LookIndex(line)));
        Assert.True(WidgetStyles.ChooseReading(line, 4));
        Assert.Equal((ListKind.Network, 1), (line.List!.Value, WidgetStyles.LookIndex(line)));
        // The graph again, drawing it; the same reading picked again changes nothing.
        Assert.True(WidgetStyles.ChooseLook(line, 0));
        Assert.Equal((ListKind.Network, true), (line.List!.Value, line.Performance.ShowGraph));
        Assert.False(WidgetStyles.ChooseReading(line, 4));
        Assert.False(WidgetStyles.ChooseLook(line, 0));
        Assert.Equal(("x", LineSize.Full), (line.Id, line.Size));

        // §318: the details are a widget of their own, its readings the same names, a pick keeping them details.
        var details = ListKind.GpuDetails.New();
        Assert.Equal(WidgetStyles.ReadingNames(line), WidgetStyles.ReadingNames(details));
        Assert.Equal(["Details"], WidgetStyles.LookNames(details));
        Assert.True(WidgetStyles.ChooseReading(details, 2));
        Assert.Equal(ListKind.MemoryDetails, details.List);
        Assert.False(WidgetStyles.Restyle(details, WidgetStyles.Performance.StyleOf(WidgetStyles.GraphWord, "memory")));

        // A widget without readings has no reading choice; its styles are its own.
        Assert.Empty(WidgetStyles.ReadingNames(ListKind.WeekView.New()));
        Assert.Equal(WidgetStyles.Calendar.Styles.Select(style => style.Title), WidgetStyles.LookNames(ListKind.WeekView.New()));
        Assert.False(WidgetStyles.HasReadings(ListKind.WeekView.New()));
    }

    /// <summary>A second to a minute; any other number the nearest step, the faster of two as near.</summary>
    [Fact]
    public void APaceIsTheNearestStepOfTheLadder()
    {
        Assert.Equal([1, 2, 5, 10, 30, 60], PerformanceChoices.Paces);
        Assert.Equal([1, 1, 2, 2, 5, 5, 5, 5, 10, 10, 10], Enumerable.Range(0, 11).Select(PerformanceChoices.Nearest));
        Assert.Equal([10, 30, 30, 60, 60], new[] { 20, 25, 45, 50, 600 }.Select(PerformanceChoices.Nearest));
        Assert.Equal("Every minute", PerformanceChoices.Pace(60));

        // A line's pace from the old list reads as the nearest; a face's too.
        var config = Configs.Repaired(File("""{ "island": { "id": "it", "kind": "cpu", "performance": { "seconds": 7 } } }"""));
        Assert.Equal(5, Lines(config).Single().Performance!.Seconds);
        var face = FolderFace.Of(FolderFaces.CpuId);
        face.Set(FolderFaces.Cpu.Option(PerformanceFace.Pace)!, "3");
        Assert.Equal(2, PerformanceFace.Options(face).Seconds);
        Assert.Equal(Array.IndexOf(PerformanceChoices.Paces, 2), PerformanceChoices.FacePaceIndex("3"));
    }
}
