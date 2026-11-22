// Open-Meteo's forecast response, as far as Karpo reads it (WeatherStore fetches it, WeatherReading
// words it): the conditions now, the next hours and the next week, for one place.

using System.Text.Json.Serialization;

namespace Karpo.Dock;

/// <summary>The parts of Open-Meteo's forecast response Karpo reads.</summary>
internal sealed class Forecast
{
    [JsonPropertyName("latitude")]
    public double Latitude { get; set; }

    [JsonPropertyName("longitude")]
    public double Longitude { get; set; }

    [JsonPropertyName("timezone")]
    public string? Timezone { get; set; }

    /// <summary>How far the place's own clock is from UTC, so the flyout can tell the time there.</summary>
    [JsonPropertyName("utc_offset_seconds")]
    public int? UtcOffsetSeconds { get; set; }

    [JsonPropertyName("current_units")]
    public ForecastUnits? CurrentUnits { get; set; }

    [JsonPropertyName("current")]
    public ForecastNow? Current { get; set; }

    [JsonPropertyName("daily")]
    public ForecastDays? Daily { get; set; }

    /// <summary>The next hours, for the weather's card in a folder (<see cref="ForecastHours"/>).</summary>
    [JsonPropertyName("hourly")]
    public ForecastHours? Hourly { get; set; }
}

internal sealed class ForecastUnits
{
    [JsonPropertyName("temperature_2m")]
    public string? Temperature { get; set; }

    [JsonPropertyName("wind_speed_10m")]
    public string? Wind { get; set; }
}

internal sealed class ForecastNow
{
    [JsonPropertyName("time")]
    public string? Time { get; set; }

    [JsonPropertyName("temperature_2m")]
    public double? Temperature { get; set; }

    [JsonPropertyName("apparent_temperature")]
    public double? Apparent { get; set; }

    [JsonPropertyName("weather_code")]
    public int? WeatherCode { get; set; }

    [JsonPropertyName("wind_speed_10m")]
    public double? Wind { get; set; }

    [JsonPropertyName("is_day")]
    public int? IsDay { get; set; }
}

internal sealed class ForecastDays
{
    [JsonPropertyName("time")]
    public string?[]? Time { get; set; }

    [JsonPropertyName("weather_code")]
    public int?[]? WeatherCode { get; set; }

    [JsonPropertyName("temperature_2m_max")]
    public double?[]? High { get; set; }

    [JsonPropertyName("temperature_2m_min")]
    public double?[]? Low { get; set; }

    /// <summary>The greatest chance of precipitation the day holds, in per cent.</summary>
    [JsonPropertyName("precipitation_probability_max")]
    public double?[]? PrecipitationChance { get; set; }
}

/// <summary>Open-Meteo's hourly forecast, as far as a folder's card reads it.</summary>
internal sealed class ForecastHours
{
    [JsonPropertyName("time")]
    public string?[]? Time { get; set; }

    [JsonPropertyName("temperature_2m")]
    public double?[]? Temperature { get; set; }

    [JsonPropertyName("weather_code")]
    public int?[]? WeatherCode { get; set; }

    [JsonPropertyName("is_day")]
    public int?[]? IsDay { get; set; }

    /// <summary>weather: the chance of precipitation in that hour, in per cent, for the flyout's strip.</summary>
    [JsonPropertyName("precipitation_probability")]
    public double?[]? PrecipitationChance { get; set; }
}
