// Points Karpo's folders at a temporary folder for the whole test run, before
// any test can reach Config: Config.Folder and Config.LocalFolder follow
// KARPO_CONFIG_DIR. No test may ever read or write the person's own
// %APPDATA%\Karpo or %LOCALAPPDATA%\Karpo, where the running Karpo lives. Nor may any test
// read or write the person's clipboard: the clipboard widgets' shared store is given a stand-in.

using System.Runtime.CompilerServices;

namespace Karpo.Tests;

internal static class Isolation
{
    /// <summary>The temporary folder standing in for %APPDATA%\Karpo during this run.</summary>
    public static string Folder { get; private set; } = "";

#pragma warning disable CA2255 // A test assembly is exactly where a module initializer belongs.
    [ModuleInitializer]
#pragma warning restore CA2255
    internal static void Isolate()
    {
        Folder = Path.Combine(Path.GetTempPath(), "karpo-tests", $"{Environment.ProcessId}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Folder);
        Environment.SetEnvironmentVariable("KARPO_CONFIG_DIR", Folder);
        // Karpo's words in English, whatever this PC's display language: the tests say what Karpo
        // writes in English. A config that names no language follows KARPO_LANGUAGE in an isolated
        // run (Languages.WindowsPreferred), in this process and in every Karpo a test starts; a
        // test of another language takes it for itself (Localization\InLanguage).
        Environment.SetEnvironmentVariable("KARPO_LANGUAGE", "en");
        Strings.Use("en");
        // clipboard: a clipboard widget made by any test hears and reads a stand-in, never the
        // person's clipboard or Windows' history of it.
        Karpo.Dock.ClipboardStore.SharedSystem = new Clipboard.ClipboardHistoryTests.FakeClipboard();
        // weather: the shared store asks neither Windows for this PC's location nor the network, which
        // a new weather widget, for the device's location, would; it answers that it is offline.
        Karpo.Dock.WeatherStore.SharedFetch = (_, _) => Task.FromException<Karpo.Dock.WeatherCache>(new HttpRequestException("No network in a test."));
        // air: the same for the air quality's store (§210).
        Karpo.Dock.AirQualityStore.SharedFetch = (_, _) => Task.FromException<Karpo.Dock.AirCache>(new HttpRequestException("No network in a test."));
        // tides: the same for the tides' store (§236).
        Karpo.Dock.TideStore.SharedFetch = (_, _) => Task.FromException<Karpo.Dock.TideCache>(new HttpRequestException("No network in a test."));
        // sports: the same for the sports' store (§239).
        Karpo.Dock.Sports.SportsStore.SharedFetch = (_, _) => Task.FromException<SportsAnswer>(new HttpRequestException("No network in a test."));
        // quakes: and for the earthquakes' (§240), which keeps no file then.
        Karpo.Dock.QuakeStore.SharedFetch = _ => Task.FromException<string>(new HttpRequestException("No network in a test."));
        // sun: this PC's position is not asked of Windows; it gives none (§368).
        Karpo.Dock.SunPlaces.SharedLocate = _ => Task.FromResult<(double Latitude, double Longitude)?>(null);
    }
}

/// <summary>
/// The one config.json of the test run. Tests that write it and read it back
/// share it, so their classes carry <c>[Collection(ConfigFile.Collection)]</c>
/// and run one at a time.
/// </summary>
[CollectionDefinition(Collection, DisableParallelization = true)]
public sealed class ConfigFile
{
    public const string Collection = "config.json";

    /// <summary>Writes <paramref name="json"/> as config.json and loads it the way the dock does.</summary>
    public static Config Load(string json)
    {
        Directory.CreateDirectory(Config.Folder);
        File.WriteAllText(Config.FilePath, json);
        return Config.Load();
    }

    /// <summary>What config.json holds now.</summary>
    public static string Text() => File.ReadAllText(Config.FilePath);

    /// <summary>Removes config.json, so the next load starts from nothing.</summary>
    public static void Delete()
    {
        if (File.Exists(Config.FilePath))
        {
            File.Delete(Config.FilePath);
        }
    }
}
