// The four scenes the style proposal drew its mockups of (docs\widgets.md §388, §401), as Karpo draws them:
// time, media and drives; a place's weather; empty and error states; the machine. Each a list folder of the
// kinds the mockup shows, at the heights a new line of each kind gets, on the gutter tests' stand-ins, at
// 150 % (the mockups' scale), dark and light. They go to KARPO_PNG_DIR as style-scene-<name>-<theme>.png, for
// a person to set beside the mockups. Needs Direct2D, so it runs only with KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Tests.Forecasts;
using Karpo.Tests.Islands;
using Karpo.Tests.Perf;
using static Karpo.Tests.Folders.ListFormPictures;
using static Karpo.Tests.Islands.Offscreen;

namespace Karpo.Tests.Folders;

[Collection(SamplerState.Collection)]
public sealed class StyleScenesPictures
{
    /// <summary>The mockups' scenes: their file's name and the kinds they show, top to bottom.</summary>
    internal static readonly (string Name, ListKind[] Kinds)[] Scenes =
    [
        ("time", [ListKind.Clock, ListKind.Timer, ListKind.Focus, ListKind.Stopwatch, ListKind.Alarm, ListKind.Calendar, ListKind.Media, ListKind.Drive, ListKind.NetworkDrive, ListKind.RemovableDevices]),
        ("places", [ListKind.WeatherNow, ListKind.AirQuality, ListKind.Tides, ListKind.Sunrise, ListKind.Sunset, ListKind.Daylight, ListKind.Quakes]),
        ("states", [ListKind.Sports, ListKind.AirQuality, ListKind.Contacts, ListKind.Camera, ListKind.Clipboard, ListKind.Mail, ListKind.Layouts, ListKind.VpnConnections, ListKind.ScreenTimeApps]),
        ("system", [ListKind.Cpu, ListKind.Battery, ListKind.Security, ListKind.Mail]),
    ];

    [UiFact]
    public void TheMockupsScenesArePictured() =>
        ListGutterTests.StoodIn<Gfx>(SavedWeather.Fixed, OnStaThread, gfx =>
        {
            const float scale = 1.5f;
            foreach (var (name, kinds) in Scenes)
            {
                foreach (var dark in new[] { true, false })
                {
                    var look = LookFor(dark);
                    // The places' air has a reading, as the mockup's; the states' is out of reach. The system scene's
                    // mail has messages, as the mockup's inbox (a stand-in: no account is reached).
                    using var inbox = name == "system" ? Inbox() : null;
                    Karpo.Dock.Pim.MailIsland.Stand = inbox;
                    var folder = Folder(gfx, made =>
                    {
                        ListGutterTests.Before(made);
                        foreach (var tile in made.Tiles)
                        {
                            if (tile.Island is AirQualityTile air)
                            {
                                air.Source = name == "places" ? SavedAir.Source(SavedAir.Cache()) : SavedAir.Source(null, "Open-Meteo can't be reached");
                            }
                        }
                    }, [.. kinds.Select(ListGutterTests.Made)]);
                    Karpo.Dock.Pim.MailIsland.Stand = null;
                    try
                    {
                        ListGutterTests.Ready(folder);
                        var (_, plan) = Plan(folder, look, scale);
                        Sheet(gfx, $"style-scene-{name}-{(dark ? "dark" : "light")}.png", scale, [($"{name}: as Karpo draws it, 150 %", look, folder, plan)]);
                    }
                    finally
                    {
                        folder.Closed();
                        folder.Dispose();
                    }
                }
            }
        });

    /// <summary>The mockup's inbox: three unread, two messages shown.</summary>
    private static Karpo.Dock.Pim.MailStore Inbox()
    {
        var store = new Karpo.Dock.Pim.MailStore(new Karpo.Dock.Pim.AccountSet([]));
        var now = DateTimeOffset.Now;
        store.Answered(
        [
            new Karpo.Accounts.MailItem("1", "Vera Holm", "vera@example.com", "Re: the roof — a second quote", null, now, true, null),
            new Karpo.Accounts.MailItem("2", "Dad", "dad@example.com", "Landed! Getting a coffee", null, now.AddMinutes(-28), false, null),
        ], unread: 3);
        return store;
    }
}
