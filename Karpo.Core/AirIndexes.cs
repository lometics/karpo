// The air quality's index (docs\widgets.md §210): the European Environment Agency's European AQI or
// the US EPA's AQI, both of which Open-Meteo's air-quality service answers with for any place. Where
// nobody chose, the place picks: the European index in Europe, the US one everywhere else, as MSN
// Weather (Windows' own weather) shows a 0-500 AQI outside Europe.
//
// Chosen per widget: the air-quality face keeps it among its options (AirQualityFace.Index), an
// air-quality line in its place's record (WeatherOptions.AirIndex), written only where somebody chose.

namespace Karpo.Core;

/// <summary>The words an air-quality widget's index is chosen with.</summary>
public static class AirIndexes
{
    /// <summary>The place's region's: the European index in Europe, the US one elsewhere. Never written.</summary>
    public const string Auto = "auto";

    /// <summary>The European Environment Agency's European Air Quality Index, 0 to 100 and past it.</summary>
    public const string European = "european";

    /// <summary>The US EPA's Air Quality Index, 0 to 500.</summary>
    public const string Us = "us";

    /// <summary>The three, in the order offered.</summary>
    public static IReadOnlyList<string> All { get; } = [Auto, European, Us];

    /// <summary>A chosen index as the file keeps it: European or US; null — the region's — for anything else.</summary>
    public static string? Chosen(string? word) => word is European or Us ? word : null;

    /// <summary>
    /// The place the air-quality face of <paramref name="folder"/> is for, its own as the weather
    /// face's is (<see cref="FolderFace.Weather"/>); null where it has none set or wears no such face.
    /// </summary>
    public static WeatherOptions? PlaceOfFace(FolderOptions folder) =>
        folder.FaceOf(FolderFaces.AirQualityId)?.Weather is { IsSet: true } set ? set : null;
}
