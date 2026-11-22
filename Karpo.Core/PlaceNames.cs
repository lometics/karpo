// A place-based widget's place as it says it (docs\widgets.md §253): the weather, the air quality,
// the tides and the earthquakes, a line or a face, each the same way. A place picked is kept as it
// was called when it was picked ("Zürich, Zurich, Switzerland") and said by its town alone, "Zürich"
// (§283; the person, 2026-10-22: "the location name that is shown should generally just be the name
// of the city or village"); its region and country only where places are told apart, in the search's
// results and the places offered before it. The device's location is called
// what the device's position is (the person, 2026-10-20: "if the widgets use the current location
// they should not just show current location on the widget but the actual location that was
// picked"), looked up once per position by the dock (DeviceNames) and written the same way, a name
// and where it is after it. "Current location" only while that name is not known yet, or could not be
// found.

namespace Karpo.Core;

/// <summary>What a place-based widget calls its place, and what the device's position is called.</summary>
public static class PlaceNames
{
    // One record swapped whole, so a reader on the dock's thread never sees a name with another
    // position's "off".
    private static volatile Where device = new(null, false);

    private sealed record Where(string? Name, bool Off);

    /// <summary>What the device's position is called, as last looked up; null until it is known, or where nothing was found.</summary>
    public static string? Device => device.Name;

    /// <summary>Whether Windows gave no position the last time it was asked: a widget with a place kept beside the switch shows that one then (WeatherStore.Where).</summary>
    public static bool DeviceOff => device.Off;

    /// <summary>Windows gave the device's position, and it is called <paramref name="name"/> (null: not known).</summary>
    public static void Located(string? name) => device = new(name is { Length: > 0 } ? name : null, false);

    /// <summary>Windows gave no position this time: what the position was called is kept for when it does.</summary>
    public static void Unlocated() => device = device with { Off = true };

    /// <summary>Tests: nothing known about the device's position.</summary>
    internal static void Forget() => device = new(null, false);

    /// <summary>
    /// What <paramref name="options"/>' place is called, its town alone ("Zürich"), empty where it has
    /// no name: the place picked; the device's position's name while the device's location is on — or,
    /// while Windows gives no position, the place kept beside the switch, which is what is shown then.
    /// </summary>
    public static string Known(WeatherOptions? options) => Short(Whole(options));

    /// <summary>
    /// <see cref="Known"/>'s place with where it is after it, "Zürich, Zurich, Switzerland": never
    /// shown, but what a web search for its weather asks, where a town's name alone could be another's.
    /// </summary>
    public static string Whole(WeatherOptions? options) => options switch
    {
        null => "",
        { UseDeviceLocation: true } when DeviceOff => options is { Latitude: not null, Longitude: not null } ? Clean(options.Place) : "",
        { UseDeviceLocation: true } => device.Name ?? "",
        _ => Clean(options.Place),
    };

    /// <summary>What a widget for <paramref name="options"/> calls its place: its name, else "Current location"; empty for one with no place set.</summary>
    public static string Of(WeatherOptions? options) =>
        Known(options) is { Length: > 0 } known ? known
        // §368: a place typed as its coordinates is called by them.
        : options is { UseDeviceLocation: false, Latitude: { } latitude, Longitude: { } longitude } ? PlaceQuery.CoordinatesText(latitude, longitude)
        : options is { IsSet: true } ? S.WidgetWeatherHere : "";

    /// <summary>
    /// A place's words as they are shown: its town, village or city alone, "Gais" for "Gais, Appenzell
    /// Ausserrhoden, Schweiz"; a place that is itself a region or a country is that ("Appenzell
    /// Ausserrhoden, Schweiz" → "Appenzell Ausserrhoden"); empty for none.
    /// </summary>
    public static string Short(string? place) => Parts(place).Name;

    /// <summary>
    /// A place kept as its words (<paramref name="place"/>) the way the search lists it: its town, and
    /// where it is after it — "Gais" over "Appenzell Ausserrhoden, Schweiz". A place with nothing
    /// before its first comma is named by the first thing it has.
    /// </summary>
    public static (string Name, string Detail) Parts(string? place) =>
        (place ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) is [var town, .. var rest]
            ? (town, string.Join(", ", rest))
            : ("", "");

    /// <summary>
    /// A place's words as a search gives them and the device's position is called alike: its
    /// <paramref name="name"/>, then its <paramref name="region"/> and <paramref name="country"/>
    /// where they say something the name does not ("Zürich, Zurich, Switzerland", "Paris, France").
    /// </summary>
    public static string Label(string? name, string? region, string? country) => PlaceQuery.Label(name, Detail(name, region, country));

    /// <summary>Where a place is, after its name: its region and its country, each where it is something and not the name again.</summary>
    public static string Detail(string? name, string? region, string? country)
    {
        var named = PlaceQuery.Clean(name);
        string[] parts = [PlaceQuery.Clean(region), PlaceQuery.Clean(country)];
        return string.Join(", ", parts.Where(part => part.Length > 0 && part != named).Distinct());
    }

    private static string Clean(string? place) => (place ?? "").Trim();
}
