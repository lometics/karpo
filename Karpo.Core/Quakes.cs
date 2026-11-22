// quakes: the earthquake widget's settings (docs\widgets.md §240): how far from its place it looks,
// the least magnitude it shows, and whether a new earthquake is said as it comes.
//
// The place is the weather's (WeatherOptions, this PC's location for a new one), so the weather's
// place picker, device switch and "Set up in Settings" serve it unchanged. The rest is the line's
// QuakeOptions, kept in its place's record (WeatherOptions.Quakes) as the air quality's index is
// (§211), each field written only where somebody chose other than the default; the face keeps the
// same three among its options (QuakesFace). Defaults are quiet: within 1,000 km, magnitude 4 and
// up — felt where it strikes, rare enough that a sound for one means something.

using System.Globalization;
using System.Text.Json.Serialization;

namespace Karpo.Core;

/// <summary>quakes: an earthquake line's own settings beside its place; a field left out is its default.</summary>
public sealed class QuakeOptions
{
    /// <summary>How far from the place it looks, in km (<see cref="QuakeChoices.Radii"/>); 0 for the whole world; null for <see cref="QuakeChoices.DefaultRadius"/>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Radius { get; set; }

    /// <summary>The least magnitude it shows and says (<see cref="QuakeChoices.Magnitudes"/>); null for <see cref="QuakeChoices.DefaultMagnitude"/>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Magnitude { get; set; }

    /// <summary>False where somebody turned the alert off; null, its default, for on.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Alerts { get; set; }

    /// <summary>
    /// As the file keeps it: each value the nearest of its choices, a default not written, and none at
    /// all where everything is its default. A number from a hand-edited file or a newer build's list
    /// reads as the nearest step, as a performance graph's pace does.
    /// </summary>
    public QuakeOptions? Normalized()
    {
        var radius = QuakeChoices.NearestRadius(Radius);
        var magnitude = QuakeChoices.NearestMagnitude(Magnitude);
        var kept = new QuakeOptions
        {
            Radius = radius == QuakeChoices.DefaultRadius ? null : radius,
            Magnitude = magnitude == QuakeChoices.DefaultMagnitude ? null : magnitude,
            Alerts = Alerts == false ? false : null,
        };
        return kept.Radius is null && kept.Magnitude is null && kept.Alerts is null ? null : kept;
    }
}

/// <summary>What an earthquake widget is set to: its radius (0 for the world), its least magnitude and its alert.</summary>
public readonly record struct QuakeSettings(int Radius, double Magnitude, bool Alerts)
{
    /// <summary>A new widget's: within 1,000 km, magnitude 4 and up, said as it comes.</summary>
    public static QuakeSettings Default { get; } = new(QuakeChoices.DefaultRadius, QuakeChoices.DefaultMagnitude, true);

    /// <summary>Whether it looks at the whole world, wherever its place is.</summary>
    public bool Worldwide => Radius == QuakeChoices.World;
}

/// <summary>The choices an earthquake widget is set with, their words in the file and on the page.</summary>
public static class QuakeChoices
{
    /// <summary>The radius that is the whole world.</summary>
    public const int World = 0;

    public const int DefaultRadius = 1000;

    public const double DefaultMagnitude = 4;

    /// <summary>How far from its place a widget may look, in km, nearest first; the world last.</summary>
    public static IReadOnlyList<int> Radii { get; } = [100, 250, 500, 1000, 2000, World];

    /// <summary>The least magnitudes a widget may show: 2.5, the feed's least, to 7.</summary>
    public static IReadOnlyList<double> Magnitudes { get; } = [2.5, 3, 4, 4.5, 5, 6, 7];

    /// <summary>The radius of the list nearest <paramref name="km"/>, by ratio; the world for 0; the default for none or a nonsense one.</summary>
    public static int NearestRadius(int? km)
    {
        if (km is not { } asked || asked < 0)
        {
            return DefaultRadius;
        }
        if (asked == World)
        {
            return World;
        }
        var best = DefaultRadius;
        var nearest = double.MaxValue;
        foreach (var radius in Radii.Where(radius => radius != World))
        {
            var far = Math.Abs(Math.Log((double)asked / radius));
            if (far < nearest - 1e-9)
            {
                (best, nearest) = (radius, far);
            }
        }
        return best;
    }

    /// <summary>The magnitude of the list nearest <paramref name="magnitude"/>, the lower of two as near; the default for none or not a number.</summary>
    public static double NearestMagnitude(double? magnitude)
    {
        if (magnitude is not { } asked || double.IsNaN(asked) || double.IsInfinity(asked))
        {
            return DefaultMagnitude;
        }
        var best = Magnitudes[0];
        foreach (var each in Magnitudes)
        {
            if (Math.Abs(each - asked) < Math.Abs(best - asked) - 1e-9)
            {
                best = each;
            }
        }
        return best;
    }

    /// <summary>A radius's word, as a face's option keeps it and its string is keyed: "km1000", "world".</summary>
    public static string RadiusWord(int km) => km == World ? "world" : FormattableString.Invariant($"km{km}");

    /// <summary>A magnitude's word: "m25" for 2.5, "m4" for 4.</summary>
    public static string MagnitudeWord(double magnitude) =>
        "m" + (magnitude * 10 % 10 == 0 ? ((int)magnitude).ToString(CultureInfo.InvariantCulture) : ((int)Math.Round(magnitude * 10)).ToString(CultureInfo.InvariantCulture));

    /// <summary>Every radius's word, in the order offered.</summary>
    public static IReadOnlyList<string> RadiusWords { get; } = [.. Radii.Select(RadiusWord)];

    /// <summary>Every magnitude's word, in the order offered.</summary>
    public static IReadOnlyList<string> MagnitudeWords { get; } = [.. Magnitudes.Select(MagnitudeWord)];

    /// <summary>The radius <paramref name="word"/> names; the default for a word this build does not know.</summary>
    public static int RadiusOf(string? word) => RadiusWords.ToList().IndexOf(word ?? "") is >= 0 and var at ? Radii[at] : DefaultRadius;

    /// <summary>The magnitude <paramref name="word"/> names; the default for a word this build does not know.</summary>
    public static double MagnitudeOf(string? word) => MagnitudeWords.ToList().IndexOf(word ?? "") is >= 0 and var at ? Magnitudes[at] : DefaultMagnitude;

    /// <summary>A radius in words: "1,000 km", "Worldwide".</summary>
    public static string RadiusName(int km) => Strings.Text($"core.face.quakes.radius.{RadiusWord(km)}");

    /// <summary>A least magnitude in words: "4.5+".</summary>
    public static string MagnitudeName(double magnitude) => Strings.Text($"core.face.quakes.magnitude.{MagnitudeWord(magnitude)}");

    /// <summary>An earthquake line's settings, its defaults where it has none.</summary>
    public static QuakeSettings Of(WeatherOptions? place) => place?.Quakes is not { } own
        ? QuakeSettings.Default
        : new(NearestRadius(own.Radius ?? DefaultRadius), NearestMagnitude(own.Magnitude ?? DefaultMagnitude), own.Alerts != false);

    /// <summary>A line's record of its own, made where it has none: what a change from the page or the menu writes into.</summary>
    public static QuakeOptions Kept(WidgetItem item) => (item.Weather ??= new WeatherOptions()).Quakes ??= new QuakeOptions();
}

/// <summary>quakes: the earthquake face's option keys and words (<see cref="FolderFaces.Quakes"/>, docs\widgets.md §240).</summary>
public static class QuakesFace
{
    /// <summary>How far from its place it looks (<see cref="QuakeChoices.RadiusWords"/>).</summary>
    public const string Radius = "radius";

    /// <summary>The least magnitude it shows (<see cref="QuakeChoices.MagnitudeWords"/>).</summary>
    public const string Magnitude = "magnitude";

    /// <summary>Whether a new earthquake is said as it comes.</summary>
    public const string Alerts = "alerts";

    /// <summary>How much its words say.</summary>
    public const string Width = "width";

    /// <summary>"M4.8".</summary>
    public const string Compact = "compact";

    /// <summary>"M4.8 · 120 km".</summary>
    public const string Standard = "standard";

    /// <summary>"M4.8 · 120 km NE · 2 h ago".</summary>
    public const string Wide = "wide";

    /// <summary>An earthquake face as it starts: for where the device is, within 1,000 km, 4 and up, alerts on, "M4.8 · 120 km".</summary>
    public static FolderFace New() => FolderFace.Of(FolderFaces.QuakesId);

    /// <summary>What <paramref name="face"/> is set to.</summary>
    public static QuakeSettings SettingsOf(FolderFace face)
    {
        var info = FolderFaces.Quakes;
        return new(
            QuakeChoices.RadiusOf(face.Text(info.Option(Radius)!)),
            QuakeChoices.MagnitudeOf(face.Text(info.Option(Magnitude)!)),
            face.Switch(info.Option(Alerts)!));
    }

    /// <summary>How much <paramref name="face"/>'s words say (<see cref="Compact"/>, <see cref="Standard"/> or <see cref="Wide"/>).</summary>
    public static string WidthOf(FolderFace face) => face.Text(FolderFaces.Quakes.Option(Width)!);

    /// <summary>
    /// The place the earthquake face of <paramref name="folder"/> is for, its own as the weather
    /// face's is (<see cref="FolderFace.Weather"/>); null where it has none set or wears no such face.
    /// </summary>
    public static WeatherOptions? PlaceOfFace(FolderOptions folder) =>
        folder.FaceOf(FolderFaces.QuakesId)?.Weather is { IsSet: true } set ? set : null;
}
