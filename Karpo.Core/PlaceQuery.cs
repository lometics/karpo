// The weather widget's place box: when what was typed is worth searching for,
// and how a place reads in the box and under it.
//
// The box searches as you type: a short wait after the last keystroke, so a
// name isn't looked up letter by letter, and never twice for the same words.
//
// In the core: the settings app's place box and the place field of a weather menu on the bar
// (docs\widgets.md §219) wait and search alike.

namespace Karpo.Core;

public static class PlaceQuery
{
    /// <summary>Fewer letters than this find every town in the world: the box waits for more.</summary>
    public const int LeastLetters = 2;

    /// <summary>How long the box waits after the last keystroke before it asks.</summary>
    public static readonly TimeSpan Wait = TimeSpan.FromMilliseconds(300);

    /// <summary>What a query really asks for: trimmed, and with the spaces inside it squeezed.</summary>
    public static string Clean(string? text) => string.Join(' ', (text ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries));

    /// <summary>
    /// Whether what is in the box now is worth asking the service for: long enough, and not what
    /// the last search already asked (<paramref name="asked"/>, null when none has).
    /// </summary>
    public static bool Searches(string? text, string? asked)
    {
        var clean = Clean(text);
        return clean.Length >= LeastLetters && !string.Equals(clean, Clean(asked), StringComparison.CurrentCultureIgnoreCase);
    }

    /// <summary>
    /// §368: a latitude and a longitude typed into the box instead of a name — "47.37, 8.54", "47.37 8.54",
    /// "-33.87; 151.21", a degree sign after either — and whether both are on the Earth (-90..90,
    /// -180..180); null where the words are not two numbers, and are a name to search for.
    /// </summary>
    public static (double Latitude, double Longitude, bool InRange)? Coordinates(string? text)
    {
        var parts = Clean(text).Replace('−', '-').Replace("°", " ", StringComparison.Ordinal)
            .Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts is not [var first, var second]
            || !double.TryParse(first, System.Globalization.NumberStyles.AllowLeadingSign | System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture, out var latitude)
            || !double.TryParse(second, System.Globalization.NumberStyles.AllowLeadingSign | System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture, out var longitude))
        {
            return null;
        }
        return (latitude, longitude, latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180);
    }

    /// <summary>A place known by its coordinates alone, as it is written: "47.377, 8.542".</summary>
    public static string CoordinatesText(double latitude, double longitude) =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{latitude:0.###}, {longitude:0.###}");

    /// <summary>
    /// A place as the box shows it once it is chosen: its name, and where it is after it
    /// ("Zürich, Zurich, Switzerland"); either alone when the other is missing.
    /// </summary>
    public static string Label(string? name, string? detail)
    {
        var place = Clean(name);
        var where = Clean(detail);
        return place.Length == 0 ? where : where.Length == 0 ? place : $"{place}, {where}";
    }
}
