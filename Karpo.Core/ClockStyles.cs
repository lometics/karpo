// A clock's looks (2026-10-30, docs\widgets.md §443). The person: "the clock should also have the mode where it just
// shows the time without the date. add some more clock styles as well. (digital, two numbers etc...)".
//
// The clock's Style row (WidgetStyles.Clock) was its two forms, the clock and the time and date. The clock's form now
// comes in looks, entries of the same choice, as the performance graph's figure is the graph's form drawing no graph
// (WidgetStyles.LooksOf): the time and the date beside a face, as it was; the time alone, no date at any size; the
// hours over the minutes, large; the face alone. Each is the clock's form (ClockTile) drawing its look, so a look
// changed keeps the line, its id, its size and its options. "Show seconds", the zone and the label apply to every one.
//
// The file: the clock's options say the look, "clock": { "style": "stacked" }; the clock as it was says nothing, so a
// file of clocks that never had a look reads as they were. A word this build does not know is the clock as it was.

using System.Text.Json.Serialization;

namespace Karpo.Core;

/// <summary>How a clock's line shows its time (<see cref="ClockOptions.Style"/>, <see cref="ClockStyles"/>).</summary>
[JsonConverter(typeof(TolerantEnumConverter<ClockStyle>))]
public enum ClockStyle
{
    /// <summary>The time over the date beside a face: the clock's look before there were looks.</summary>
    [JsonStringEnumMemberName("analogAndDigital")] AnalogAndDigital,

    /// <summary>The time, and where it stands for another zone; no date at any size.</summary>
    [JsonStringEnumMemberName("timeOnly")] TimeOnly,

    /// <summary>The hours over the minutes, large ("14" over "20").</summary>
    [JsonStringEnumMemberName("stacked")] Stacked,

    /// <summary>The face alone.</summary>
    [JsonStringEnumMemberName("analog")] Analog,
}

/// <summary>A clock's looks, their words, names and icons (see the top of the file).</summary>
public static class ClockStyles
{
    /// <summary>The look <paramref name="item"/>'s clock is in; the clock as it was for none or a word not known.</summary>
    public static ClockStyle Of(WidgetItem? item) => Known(item?.Clock?.Style ?? default);

    /// <summary><paramref name="style"/> where it is a look, else the clock as it was.</summary>
    public static ClockStyle Known(ClockStyle style) => Enum.IsDefined(style) ? style : ClockStyle.AnalogAndDigital;

    /// <summary>What config.json says for <paramref name="style"/> ("timeOnly"); never changes once released.</summary>
    public static string Word(ClockStyle style) => style switch
    {
        ClockStyle.TimeOnly => "timeOnly",
        ClockStyle.Stacked => "stacked",
        ClockStyle.Analog => "analog",
        _ => "analogAndDigital",
    };

    /// <summary>What the Style row calls <paramref name="style"/>: "Time only".</summary>
    public static string Title(ClockStyle style) => Strings.Text($"core.clockStyle.{Word(style)}");

    /// <summary>
    /// Its icon in the Style row, each its own (R13), and none drawn like another (the font's clock, history and time
    /// are one face): the face for the face alone, a figure for the time alone, lines over each other for Stacked, a
    /// clock in a frame for the time and the date beside a face.
    /// </summary>
    public static string Glyph(ClockStyle style) => style switch
    {
        ClockStyle.TimeOnly => OptionGlyphs.Figure,
        ClockStyle.Stacked => OptionGlyphs.Rows,
        ClockStyle.Analog => OptionGlyphs.Clock,
        _ => OptionGlyphs.ClockAndTime,
    };
}
