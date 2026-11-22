// Finding a place for the weather widget: Open-Meteo's geocoding API, which
// needs no account. It is asked only when the person searches — a moment after
// the last keystroke (PlaceQuery.Wait) or on Enter — and never on its own: the
// settings app's place box and the place field of a weather menu on the bar
// (docs\widgets.md §219) alike.

using System.Globalization;
using System.Text.Json;

namespace Karpo.Core;

public sealed record Place(string Name, string Detail, double Latitude, double Longitude)
{
    /// <summary>What the settings file keeps, and the weather tooltip shows.</summary>
    public string Label => Detail.Length == 0 ? Name : $"{Name}, {Detail}";

    // The search box shows this when a result is picked.
    public override string ToString() => Label;
}

public static class Places
{
    static readonly HttpClient Http = CreateClient();

    static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("karpo");
        return client;
    }

    /// <summary>Up to <paramref name="count"/> places (five, as the page lists them), best match first. Throws on network and service errors.</summary>
    public static async Task<List<Place>> SearchAsync(string text, CancellationToken cancel, int count = 5)
    {
        // Names come back in the language asked for; the service falls back to
        // English for one it doesn't have.
        var language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        if (language.Length != 2)
        {
            language = "en";
        }
        var url = "https://geocoding-api.open-meteo.com/v1/search"
            + $"?name={Uri.EscapeDataString(text)}&count={count}&language={language}&format=json";
        using var response = await Http.GetAsync(url, cancel);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancel);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancel);

        var places = new List<Place>();
        // Nothing found comes back without a "results" member at all.
        if (!json.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
        {
            return places;
        }
        foreach (var result in results.EnumerateArray())
        {
            if (!Number(result, "latitude", out var latitude) || !Number(result, "longitude", out var longitude))
            {
                continue;
            }
            var name = Text(result, "name");
            if (name.Length == 0)
            {
                continue;
            }
            // The device's position is called the same way (PlaceNames, §253).
            places.Add(new Place(name, PlaceNames.Detail(name, Text(result, "admin1"), Text(result, "country")), latitude, longitude));
        }
        return places;
    }

    static string Text(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()?.Trim() ?? ""
            : "";

    static bool Number(JsonElement element, string property, out double number)
    {
        number = 0;
        return element.TryGetProperty(property, out var value)
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetDouble(out number)
            && double.IsFinite(number);
    }
}
