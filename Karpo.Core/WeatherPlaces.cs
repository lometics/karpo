// A weather widget's place (docs\widgets.md §80). Every weather widget has its own: each weather
// line of a folder, and the weather face, which keeps its place in the face (FolderFace.Weather).
// Nothing is shared across a folder (the person, 2026-10-10: "location should not actually be
// shared in the folder, this means you have to set it multiple times but you are actually able to
// show different places per folder which is a feature i want"): one without a place shows its
// "Set up in Settings", whatever the others in its folder show.
//
// One thing still copies a place, once, and leaves no link behind: the weather preset just added,
// whose first place chosen is given to its other pieces still without one (Spread).

namespace Karpo.Core;

/// <summary>A weather widget's place as it is shown, and the one-off copy of a place.</summary>
public static class WeatherPlaces
{
    /// <summary>The place the weather face of <paramref name="folder"/> is for — its first, among its faces; null where it has none set.</summary>
    public static WeatherOptions? OfFace(FolderOptions folder) => folder.FaceOf(FolderFaces.WeatherId)?.Weather is { IsSet: true } set ? set : null;

    /// <summary>
    /// <paramref name="set"/> as a line or a face shows it: a copy, with the place filled in. The
    /// scale is not in it — it is Localization's, for every weather widget alike — nor how often it
    /// asks, which is every online widget's (<see cref="Config.OnlineUpdateMinutes"/>).
    /// </summary>
    public static WeatherOptions Settled(WeatherOptions? set)
    {
        var next = set ?? new WeatherOptions();
        return new WeatherOptions
        {
            Place = next.Place ?? "",
            Latitude = next.Latitude,
            Longitude = next.Longitude,
            UseDeviceLocation = next.UseDeviceLocation,
            AirIndex = AirIndexes.Chosen(next.AirIndex),
        };
    }

    /// <summary>
    /// The weather preset's one question (2026-10-10): the place just chosen for one of its pieces,
    /// <paramref name="from"/>, copied into every other weather piece of <paramref name="folder"/>
    /// that has none yet — its lines and a weather face. Asked once, as the preset is added; a copy,
    /// so each piece's place is its own from then on. The number of pieces given it.
    /// </summary>
    public static int Spread(FolderOptions folder, WeatherOptions from)
    {
        if (!from.IsSet)
        {
            return 0;
        }
        var given = 0;
        foreach (var cell in Folders.ReadingOrder(folder))
        {
            if (cell.Island is { Weather: { IsSet: false } } line && !ReferenceEquals(line.Weather, from))
            {
                var quakes = line.Weather.Quakes;
                line.Weather = Given(from, line.Weather.AirIndex);
                // quakes: an earthquake line keeps its radius, magnitude and alert (§240).
                line.Weather.Quakes = quakes;
                given++;
            }
        }
        // air: an air-quality face is for a place as the weather face is, and given it alike (§210);
        // tides: so is a tides face (§236).
        // quakes: and an earthquake face (§240); sun: and a sun face (§368).
        foreach (var face in folder.Faces.Where(face => face?.Kind is not null && FolderFaces.HasPlace(face.Kind)))
        {
            if (face.Weather is not { IsSet: true } && !ReferenceEquals(face.Weather, from))
            {
                face.Weather = Given(from, null);
                given++;
            }
        }
        return given;
    }

    /// <summary>
    /// <paramref name="label"/> at <paramref name="latitude"/>, <paramref name="longitude"/> chosen for
    /// <paramref name="weather"/> — a line's or a face's —, as the settings app's place box and a
    /// weather menu's place field set it (docs\widgets.md §219): its words as found, and where it is
    /// to four decimals, some eleven metres.
    /// </summary>
    public static void Choose(WeatherOptions weather, string label, double latitude, double longitude)
    {
        weather.Place = label;
        weather.Latitude = Math.Round(latitude, 4);
        weather.Longitude = Math.Round(longitude, 4);
    }

    /// <summary>The place of <paramref name="from"/> as another piece is given it: the place alone, the piece keeping its own index (<paramref name="index"/>).</summary>
    private static WeatherOptions Given(WeatherOptions from, string? index)
    {
        var given = Settled(from);
        given.AirIndex = index;
        return given;
    }
}
