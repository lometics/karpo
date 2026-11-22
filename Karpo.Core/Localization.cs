// How Karpo writes dates, times, numbers and units.
//
// Everything Karpo shows has always been written the way Windows' regional
// format says, read from the NLS functions rather than from .NET's culture data
// (Karpo.Dock\Panels\Nls.cs: the dock runs with InvariantGlobalization). That is
// still the default, and the default is not a value here: **null means follow
// Windows**, and a followed format keeps following while Karpo runs, so changing
// the region format in Windows changes Karpo's dates without a restart.
//
// A setting here overrides one of them. Two rules keep this from becoming the
// usual mess:
//
//   1. **One place decides.** Nls is the only thing that answers "how is a date
//      written here", and it is where the override is read. No widget formats a
//      date of its own, so a setting cannot reach half the app.
//   2. **A pattern is chosen, not typed.** The date and time patterns offered
//      are the ones the region itself has (Nls.DateFormats, Nls.TimeFormats,
//      which is what Windows' own "Change data formats" page lists), so nobody
//      can type "dd/MM/yy" and get a subtly wrong year. The few settings that
//      are not patterns — the first day of the week, the units — are lists.
//
// A format is a **setting**, so a widget may legitimately be a different size
// under a different format; what must never happen is a size that follows the
// formatted *value*, which is what WidgetShapes' templates already hold.

using System.Text.Json.Serialization;

namespace Karpo.Core;

/// <summary>Which scale a temperature is shown in.</summary>
public enum TemperatureUnit
{
    /// <summary>Whatever the region says (Windows' LOCALE_IMEASURE).</summary>
    [JsonStringEnumMemberName("follow")] Follow,

    [JsonStringEnumMemberName("celsius")] Celsius,
    [JsonStringEnumMemberName("fahrenheit")] Fahrenheit,
}

/// <summary>How a size in bytes is written.</summary>
public enum ByteUnit
{
    /// <summary>What Windows itself shows in Explorer: KB, MB, GB for powers of 1024.</summary>
    [JsonStringEnumMemberName("windows")] Windows,

    /// <summary>Powers of 1000, named for them: kB, MB, GB.</summary>
    [JsonStringEnumMemberName("decimal")] Decimal,

    /// <summary>Powers of 1024, named for them: KiB, MiB, GiB.</summary>
    [JsonStringEnumMemberName("binary")] Binary,
}

/// <summary>
/// The formats Karpo writes things in. Every member is "follow Windows" until somebody sets it:
/// null for the patterns and the first day, <see cref="TemperatureUnit.Follow"/> and
/// <see cref="ByteUnit.Windows"/> for the units. Nothing here is written to the file while it
/// follows, so a config from before this existed reads as following everything.
/// </summary>
public sealed class LocalizationOptions
{
    /// <summary>
    /// The short date's pattern ("dd/MM/yyyy"), one of <c>Nls.DateFormats(long: false)</c>; null
    /// follows Windows. It is the date the calendar shows on the taskbar and what a calendar's short dates use.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ShortDate { get; set; }

    /// <summary>The long date's pattern ("dddd, d MMMM yyyy"), one of <c>Nls.DateFormats(long: true)</c>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LongDate { get; set; }

    /// <summary>
    /// The time's pattern ("HH:mm:ss", "h:mm:ss tt"), one of <c>Nls.TimeFormats()</c>. A widget that
    /// shows no seconds has them taken out of this pattern rather than asking for another one, so
    /// 12- or 24-hour is chosen once and holds everywhere.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Time { get; set; }

    /// <summary>The day a week starts on in the calendar; null follows Windows.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DayOfWeek? FirstDayOfWeek { get; set; }

    /// <summary>The scale temperatures are shown in, for every widget that shows one.</summary>
    public TemperatureUnit Temperature { get; set; }

    /// <summary>How sizes in bytes are written, for every widget that shows one.</summary>
    public ByteUnit Sizes { get; set; }

    /// <summary>Whether every one of them still follows Windows, which is what a fresh Karpo does.</summary>
    [JsonIgnore]
    public bool Follows =>
        ShortDate is null
        && LongDate is null
        && Time is null
        && FirstDayOfWeek is null
        && Temperature == TemperatureUnit.Follow
        && Sizes == ByteUnit.Windows;

    /// <summary>
    /// Whether a temperature is written in Fahrenheit, where Windows' region says
    /// <paramref name="regionSays"/> (its measurement system: Karpo.Dock's <c>Nls.Fahrenheit</c>
    /// asks it). <b>One answer for every temperature</b>: the scale somebody set here, else the
    /// region's. The weather widget had a scale of its own, which this overrode once it was set;
    /// the person had it taken out (2026-10-05: "remove the own setting completely"), so nothing
    /// can disagree with this any more.
    /// </summary>
    public bool IsFahrenheit(bool regionSays) => Temperature switch
    {
        TemperatureUnit.Fahrenheit => true,
        TemperatureUnit.Celsius => false,
        _ => regionSays,
    };

    public LocalizationOptions Clone() => new()
    {
        ShortDate = ShortDate,
        LongDate = LongDate,
        Time = Time,
        FirstDayOfWeek = FirstDayOfWeek,
        Temperature = Temperature,
        Sizes = Sizes,
    };
}
