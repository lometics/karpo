// holidays: a country's public holidays in the calendar, from Nager.Date (2026-10-19, the person: "not a
// widget but add possibility to add public holidays to the calendar from the nager.date api";
// docs\widgets.md §237).
//
// One more calendar beside the accounts' (Karpo.Dock\Calendar\HolidayCalendar.cs): every calendar widget
// shows every calendar, so the holidays are not a widget's setting but the person's, in the config
// (Config.Holidays), turned on and coloured on the Accounts page beside the accounts' calendars and in a
// calendar's menu. What is here is the part both apps and the tests share: the setting, reading the
// service's answer, and which of its holidays are shown.
//
// **The service.** https://date.nager.at/api/v3/PublicHolidays/{year}/{country}: no key, a year of one
// country a request, each holiday its date, its name in the country's language (localName) and in
// English (name), whether it is the whole country's (global) or some regions' (counties, ISO 3166-2:
// "CH-ZH"), and what kind of day it is (types: Public, Bank, School, Authorities, Optional, Observance).
//
// **Which are shown** (Shown):
//  - Public only: a bank's or a school's day off, an observance, is a day off for few. (A switch for the
//    rest, "Also show other days", went on 2026-10-19, the person: "it is not clear what is meant".)
//  - With a region, the whole country's and that region's. Without one, the whole country's alone: a
//    regional holiday shown as if it were everybody's is wrong for most who see it, and marking it
//    "regional" on a busy day list is noise — Switzerland would show some thirty holidays, most of them a
//    few cantons'. Picking the region shows them. One exception, the service's own way of writing a
//    national day: a holiday listed for every region the country has (the United Kingdom's New Year's
//    Day, England, Scotland, Wales and Northern Ireland, global: false) is the whole country's.
//  - A day once: the service lists Epiphany twice for Switzerland, Public in one canton and an observance
//    in two others.
//
// The countries are the service's (its AvailableCountries, 2026-10-19), and so are the regions, gathered
// from its answers for 2026 and 2027 (HolidayPlaces.cs): listed here so that the Settings page and a menu
// offer them without asking the service anything. Nothing is asked of it until the person adds a country.
//
// **Several calendars** (2026-10-20, the person: "you should be able to add multiple holiday calendars";
// §265): the setting is a list of countries, each with a region where it has regions, and none listed is
// off. The calendar shows them all as one, a
// holiday two of them share once, and where several are listed a day's holidays say whose they are
// (Calendars: the country, or the region where two of one country are listed). Where the holidays come
// from is said nowhere the person reads (the person: "dont tell from where the data comes from").

using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Karpo.Core;

/// <summary>
/// holidays: the calendars of public holidays Karpo shows, a country's (and a region's) each, each in its own
/// colour (§423) (<see cref="Config.Holidays"/>). Null in the file until somebody adds a country; none
/// listed is off (§265).
/// </summary>
[JsonConverter(typeof(HolidayOptionsConverter))]
public sealed class HolidayOptions
{
    /// <summary>§265: the calendars, in the order they were added, each once. None is off, which asks the service nothing.</summary>
    public List<HolidayCountry> Calendars { get; set; } = [];

    /// <summary>Whether any calendar is listed: the holidays are shown.</summary>
    public bool On => Calendars.Count > 0;

    /// <summary>A copy held to what it may say: each calendar held to its own (<see cref="HolidayCountry.Normalized"/>), each once.</summary>
    public HolidayOptions Normalized()
    {
        List<HolidayCountry> calendars = [];
        foreach (var calendar in Calendars ?? [])
        {
            if (calendar?.Normalized() is { } kept && !calendars.Any(had => had.Same(kept)))
            {
                calendars.Add(kept);
            }
        }
        return new HolidayOptions { Calendars = calendars };
    }
}

/// <summary>§265: one calendar of public holidays: a country's, and where it has regions one region's besides.</summary>
public sealed class HolidayCountry
{
    /// <summary>The country, ISO 3166-1 alpha-2 ("CH"); empty for the one Windows' region is set to (<see cref="PublicHolidays.Home"/>).</summary>
    public string Country { get; set; } = "";

    /// <summary>The region of <see cref="Country"/>, ISO 3166-2 ("CH-ZH"); empty for the whole country's holidays alone.</summary>
    public string Region { get; set; } = "";

    /// <summary>§423: the colour its holidays are drawn in, <c>#RRGGBB</c>; null for the holidays' own (<see cref="PublicHolidays.DefaultColour"/>).</summary>
    public string? Colour { get; set; }

    /// <summary>Whether <paramref name="other"/> is the same calendar: the same country and region.</summary>
    public bool Same(HolidayCountry other) => Country == other.Country && Region == other.Region;

    /// <summary>A copy held to what it may say: a country in capitals, a region of that country, or none; a colour that reads, or none.</summary>
    public HolidayCountry Normalized()
    {
        var country = Code(Country);
        var region = (Region ?? "").Trim().ToUpperInvariant();
        // A region belongs to a country the person chose: one of another (or of Windows' region, which may
        // move) is none.
        if (country.Length == 0 || !region.StartsWith(country + "-", StringComparison.Ordinal) || region.Length > 10)
        {
            region = "";
        }
        return new HolidayCountry { Country = country, Region = region, Colour = PublicHolidays.Hex(Colour) };
    }

    /// <summary><paramref name="text"/> as a country's code in capitals ("CH"); empty for anything that is none.</summary>
    internal static string Code(string? text)
    {
        var country = (text ?? "").Trim().ToUpperInvariant();
        return country.Length == 2 && char.IsAsciiLetterUpper(country[0]) && char.IsAsciiLetterUpper(country[1]) ? country : "";
    }
}

/// <summary>
/// §265: a calendar as the calendar shows it: its <paramref name="Country"/> (Windows' region's where it
/// follows it), its <paramref name="Region"/>, and what tells it from the others in a day's list
/// (<paramref name="Label"/>); §423 the colour its holidays are drawn in (<paramref name="Colour"/>, <c>#RRGGBB</c>:
/// the one picked for it, else <see cref="PublicHolidays.DefaultColour"/>).
/// </summary>
public sealed record HolidayShown(string Country, string Region, string Label, string Colour);

/// <summary>One holiday as the service lists it.</summary>
/// <param name="LocalName">Its name in the country's language.</param>
/// <param name="Name">Its name in English.</param>
/// <param name="Global">The whole country's; else <paramref name="Counties"/>'.</param>
/// <param name="Counties">The regions it is a holiday in (ISO 3166-2), where it is not the whole country's.</param>
/// <param name="Types">What kind of day it is: Public, Bank, School, Authorities, Optional, Observance.</param>
public sealed record Holiday(DateOnly Date, string LocalName, string Name, bool Global, IReadOnlyList<string> Counties, IReadOnlyList<string> Types)
{
    /// <summary>A day off for everybody it is a holiday for: one of type Public (or of no type at all).</summary>
    public bool IsPublic => Types.Count == 0 || Types.Contains("Public", StringComparer.OrdinalIgnoreCase);
}

/// <summary>The service, its answers, and which holidays are shown; see the top of the file.</summary>
public static partial class PublicHolidays
{
    /// <summary>The service's address, which every request starts with.</summary>
    public const string Service = "https://date.nager.at/api/v3/";

    /// <summary>A calendar's holidays' own colour while the person picked none for it (§423): Outlook's red, the palette's first, as calendars print holidays.</summary>
    public const string DefaultColour = "#D13438";

    /// <summary>The one request for <paramref name="country"/>'s holidays of <paramref name="year"/>.</summary>
    public static string Url(int year, string country) => $"{Service}PublicHolidays/{year}/{country}";

    /// <summary>
    /// The holidays in the service's answer, in its order; an entry without a date or a name is passed
    /// by. Null for an answer that is not a list of holidays at all (an error page, a cut-off text).
    /// </summary>
    public static IReadOnlyList<Holiday>? Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return null;
            }
            var found = new List<Holiday>();
            foreach (var entry in document.RootElement.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object
                    || Text(entry, "date") is not { } date
                    || !DateOnly.TryParseExact(date, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var day))
                {
                    continue;
                }
                var local = Text(entry, "localName")?.Trim() ?? "";
                var english = Text(entry, "name")?.Trim() ?? "";
                if (local.Length == 0 && english.Length == 0)
                {
                    continue;
                }
                var global = !entry.TryGetProperty("global", out var flag) || flag.ValueKind != JsonValueKind.False;
                found.Add(new Holiday(day, local.Length > 0 ? local : english, english.Length > 0 ? english : local, global, Texts(entry, "counties"), Texts(entry, "types")));
            }
            return found;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// The holidays of <paramref name="holidays"/> the calendar shows for <paramref name="region"/> of
    /// <paramref name="country"/> (none: the whole country's), the public ones, by date, each day's name
    /// once. See the top of the file.
    /// </summary>
    public static IReadOnlyList<Holiday> Shown(IEnumerable<Holiday> holidays, string country, string region)
    {
        var known = HolidayPlaces.Regions(country);
        var seen = new HashSet<(DateOnly, string)>();
        var shown = new List<Holiday>();
        foreach (var holiday in holidays)
        {
            if (!holiday.IsPublic)
            {
                continue;
            }
            var here = holiday.Global || holiday.Counties.Count == 0
                || (region.Length > 0 ? holiday.Counties.Contains(region, StringComparer.OrdinalIgnoreCase) : Everywhere(holiday, known));
            if (here && seen.Add((holiday.Date, holiday.LocalName)))
            {
                shown.Add(holiday);
            }
        }
        shown.Sort(static (a, b) => a.Date.CompareTo(b.Date));
        return shown;
    }

    /// <summary>A regional holiday every region of the country has, as the service writes some national days: two regions at the least, or "every" says nothing.</summary>
    private static bool Everywhere(Holiday holiday, IReadOnlyList<HolidayRegion> regions) =>
        regions.Count >= 2 && regions.All(region => holiday.Counties.Contains(region.Code, StringComparer.OrdinalIgnoreCase));

    /// <summary>
    /// What the calendar calls <paramref name="holiday"/> in <paramref name="language"/> (Karpo's): its
    /// English name where Karpo speaks English, else its name in the country's language, which a person
    /// living there reads it by.
    /// </summary>
    public static string Title(Holiday holiday, string language) => English(language) ? holiday.Name : holiday.LocalName;

    /// <summary>Its other name, where it has one that is not <see cref="Title"/>'s: shown under it where the calendar has room. Null for none.</summary>
    public static string? Other(Holiday holiday, string language)
    {
        var other = English(language) ? holiday.LocalName : holiday.Name;
        return string.Equals(other, Title(holiday, language), StringComparison.OrdinalIgnoreCase) ? null : other;
    }

    private static bool English(string language) => language == "en" || language.StartsWith("en-", StringComparison.OrdinalIgnoreCase);

    /// <summary>The country <paramref name="calendar"/> shows: the one chosen, else Windows' region's where the service has it; empty for none.</summary>
    public static string CountryOf(HolidayCountry calendar) => calendar.Country.Length > 0 ? calendar.Country : Home();

    /// <summary>
    /// §265: the calendars <paramref name="options"/> shows, each once in the order listed — one following
    /// Windows' region and one of that country chosen are one —, a country the service has none of left
    /// out. Each is labelled by its country's name, or by its region's (the whole country's by the
    /// country's) where another of the same country is shown too: "Zürich" and "Bern", not "Switzerland" twice.
    /// §423: each in its own colour, the first listed's of two that are one.
    /// </summary>
    public static IReadOnlyList<HolidayShown> Calendars(HolidayOptions? options)
    {
        List<(string Country, string Region, string Colour)> places = [];
        foreach (var calendar in options?.Calendars ?? [])
        {
            var country = CountryOf(calendar);
            if (country.Length > 0 && !places.Exists(place => place.Country == country && place.Region == calendar.Region))
            {
                places.Add((country, calendar.Region, Hex(calendar.Colour) ?? DefaultColour));
            }
        }
        return [.. places.Select(place => new HolidayShown(
            place.Country,
            place.Region,
            place.Region.Length > 0 && places.Count(other => other.Country == place.Country) > 1 ? HolidayPlaces.RegionName(place.Region) : HolidayPlaces.Name(place.Country),
            place.Colour))];
    }

    /// <summary>The country Windows' region is set to, where the service has its holidays ("CH"); empty for one it has not.</summary>
    public static unsafe string Home()
    {
        try
        {
            const int room = 16;
            var name = stackalloc char[room];
            var length = GetUserDefaultGeoName(name, room);
            // A region Windows writes as a number (UN M49, "001" for the world) is no country.
            var code = length > 1 ? new string(name, 0, length - 1).ToUpperInvariant() : "";
            return HolidayPlaces.Has(code) ? code : "";
        }
        catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException)
        {
            return "";
        }
    }

    /// <summary><c>#RRGGBB</c> in capitals for a colour written so; null for anything else.</summary>
    public static string? Hex(string? colour)
    {
        var text = colour?.Trim() ?? "";
        if (text.Length != 7 || text[0] != '#' || !text.Skip(1).All(char.IsAsciiHexDigit))
        {
            return null;
        }
        return text.ToUpperInvariant();
    }

    private static string? Text(JsonElement entry, string name) =>
        entry.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static IReadOnlyList<string> Texts(JsonElement entry, string name) =>
        entry.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? [.. value.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString()!.Trim()).Where(text => text.Length > 0)]
            : [];

    [LibraryImport("kernel32.dll")]
    private static unsafe partial int GetUserDefaultGeoName(char* geoName, int geoNameCount);
}

/// <summary>
/// <see cref="HolidayOptions"/> read tolerantly: an object's fields of the kind they should be, the rest
/// passed by; anything but an object is none. A hand edit there costs the holidays, or one calendar of
/// them, never the config.
/// </summary>
public sealed class HolidayOptionsConverter : JsonConverter<HolidayOptions?>
{
    public override HolidayOptions? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            reader.Skip();
            return null;
        }
        var read = new HolidayOptions();
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            var name = reader.GetString() ?? "";
            reader.Read();
            switch (name.ToLowerInvariant())
            {
                case "calendars" when reader.TokenType == JsonTokenType.StartArray:
                    read.Calendars = ReadCalendars(ref reader);
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }
        return read.Normalized();
    }

    /// <summary>The list's calendars, each an object with a country; one whose country is none, or is no object, is passed by.</summary>
    private static List<HolidayCountry> ReadCalendars(ref Utf8JsonReader reader)
    {
        List<HolidayCountry> calendars = [];
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                reader.Skip();
                continue;
            }
            string? country = null;
            var region = "";
            string? colour = null;
            while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
            {
                var name = reader.GetString() ?? "";
                reader.Read();
                switch (name.ToLowerInvariant())
                {
                    case "country" when reader.TokenType == JsonTokenType.String:
                        country = reader.GetString() ?? "";
                        break;
                    case "region" when reader.TokenType == JsonTokenType.String:
                        region = reader.GetString() ?? "";
                        break;
                    case "colour" when reader.TokenType == JsonTokenType.String:
                        colour = reader.GetString();
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }
            // Empty is Windows' region's; words that are no country are a calendar of nothing.
            if (country is not null && (country.Trim().Length == 0 || HolidayCountry.Code(country).Length > 0))
            {
                calendars.Add(new HolidayCountry { Country = country, Region = region, Colour = colour });
            }
        }
        return calendars;
    }

    public override void Write(Utf8JsonWriter writer, HolidayOptions? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }
        writer.WriteStartObject();
        writer.WriteStartArray("calendars");
        foreach (var calendar in value.Calendars)
        {
            writer.WriteStartObject();
            writer.WriteString("country", calendar.Country);
            writer.WriteString("region", calendar.Region);
            if (calendar.Colour is { } colour)
            {
                writer.WriteString("colour", colour);
            }
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
    }
}
