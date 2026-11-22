// A clock's styles (2026-10-30, docs\widgets.md §443). The person: "the clock should also have the mode where it just
// shows the time without the date. add some more clock styles as well. (digital, two numbers etc...)". So the clock's
// Style row offers the clock as it was (Analog and digital), the time alone, the hours over the minutes (Stacked), the
// face alone (Analog), and the time and date (its other form): one choice, each entry its own icon, a pick going back.
// Held here: the choice, a pick, the file's "style" in the clock's options (absent is the clock as it was), and an
// unknown word read as that.

using System.Text.Json;
using Karpo.Dock;

namespace Karpo.Tests.Core;

public sealed class ClockStylesTests
{
    private static readonly string[] Titles = ["Analog and digital", "Time only", "Stacked", "Analog", "Time and date"];

    /// <summary>The clock's Style row: every look, each its own icon, the clock as it was first and ticked on a new one.</summary>
    [Fact]
    public void TheClocksStyleRowOffersEachLook()
    {
        var clock = ListKind.Clock.New();
        Assert.Equal(Titles, WidgetStyles.LookNames(clock));
        var glyphs = WidgetStyles.LookGlyphs(clock);
        Assert.Equal(Titles.Length, glyphs.Distinct().Count());
        Assert.Equal(0, WidgetStyles.LookIndex(clock));
        Assert.Equal(ClockStyle.AnalogAndDigital, ClockStyles.Of(clock));
    }

    /// <summary>
    /// A pick changes the look and keeps everything else; the time and date is the other form, and back from it the
    /// clock is the look it was given. Each pick ticks itself.
    /// </summary>
    [Fact]
    public void APickIsTheLookAndKeepsTheRest()
    {
        var clock = ListKind.Clock.New();
        clock.Id = "tokyo";
        clock.Clock!.TimeZone = "Tokyo Standard Time";
        clock.Clock.Label = "Tokyo";
        clock.Size = LineSize.Compact;
        // From the clock as it was through every other look and back.
        foreach (var index in Enumerable.Range(1, Titles.Length - 1).Append(0))
        {
            Assert.True(WidgetStyles.ChooseLook(clock, index), Titles[index]);
            Assert.Equal(index, WidgetStyles.LookIndex(clock));
            Assert.False(WidgetStyles.ChooseLook(clock, index));
            Assert.Equal(("tokyo", (LineSize?)LineSize.Compact, "Tokyo"), (clock.Id, clock.Size, clock.Clock?.Label));
        }
        Assert.Equal((ListKind.Clock, ClockStyle.AnalogAndDigital), (clock.List!.Value, ClockStyles.Of(clock)));
        Assert.True(WidgetStyles.ChooseLook(clock, 4));
        Assert.Equal(ListKind.TimeHeader, clock.List);
        Assert.True(WidgetStyles.ChooseLook(clock, 1));
        Assert.Equal((ListKind.Clock, ClockStyle.TimeOnly), (clock.List!.Value, ClockStyles.Of(clock)));
        Assert.True(WidgetStyles.ChooseLook(clock, 2));
        Assert.Equal(ClockStyle.Stacked, ClockStyles.Of(clock));
    }

    /// <summary>
    /// The file: the clock's options say its look, "clock": { "style": "stacked" }; the clock as it was says nothing,
    /// so a file of clocks that never had one reads as they were and is written back the same; an unknown word is the
    /// clock as it was.
    /// </summary>
    [Theory]
    [InlineData("""{ "seconds": false, "timeZone": "", "label": "", "view": "month" }""", ClockStyle.AnalogAndDigital)]
    [InlineData("""{ "style": "timeOnly" }""", ClockStyle.TimeOnly)]
    [InlineData("""{ "style": "stacked" }""", ClockStyle.Stacked)]
    [InlineData("""{ "style": "analog" }""", ClockStyle.Analog)]
    [InlineData("""{ "style": "sundial" }""", ClockStyle.AnalogAndDigital)]
    public void TheFileSaysTheLook(string options, ClockStyle style)
    {
        var config = Configs.Repaired($$"""
            { "version": {{Config.Shape}}, "layouts": [ { "id": "L", "islands": [
              { "id": "f", "kind": "folderList", "section": "end", "folder": { "lists": [ [
                { "island": { "id": "c", "kind": "clock", "size": "full", "clock": {{options}} } } ] ] } } ] } ] }
            """);
        var line = config.Layouts.Single().Islands.Single().Folder!.Cells.Single().Island;
        Assert.Equal(style, ClockStyles.Of(line));
        var written = JsonSerializer.Serialize(line.Clock!, ConfigJson.Default.ClockOptions);
        if (style == ClockStyle.AnalogAndDigital)
        {
            Assert.DoesNotContain("\"style\"", written, StringComparison.Ordinal);
        }
        else
        {
            Assert.Contains($"\"style\": \"{ClockStyles.Word(style)}\"", written, StringComparison.Ordinal);
        }
    }

    /// <summary>The words a look is written as, which never change once released.</summary>
    [Fact]
    public void TheWords()
    {
        Assert.Equal(["analogAndDigital", "timeOnly", "stacked", "analog"], Enum.GetValues<ClockStyle>().Select(ClockStyles.Word));
    }
}
