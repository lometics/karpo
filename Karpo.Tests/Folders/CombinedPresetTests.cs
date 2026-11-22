// The combined groups (Karpo.Core\Presets.cs; 2026-10-11: "add some combined folder / widget groups /
// premade groups now. for example one that combine the time and calendar stuff ... it just needs to
// look good an make sense ... they can also have two or more columns as well"): each recipe line for
// line, its lists side by side and its faces; each standing whole on a 1440p screen at 150 % with
// every line at the most it may grow to; and pictured, dark and light at 150 %, with every line on a
// stand-in — the calendar's sample week, a saved forecast, the shares, a feed, an inbox, what is
// playing, a battery — to KARPO_PNG_DIR as combined-<id>.png. The switches, the sliders, the radios and
// the devices read this PC, and change nothing. Nothing reaches the network or the person's accounts.

using Karpo.Accounts;
using Karpo.Dock;
using Karpo.Dock.Pim;
using Karpo.Dock.QuickSettings.Services;
using Karpo.Dock.Rss;
using Karpo.Dock.Weather;
using Karpo.Tests.Core;
using Karpo.Tests.Forecasts;
using Karpo.Tests.Islands;
using Karpo.Tests.Rss;
using static Karpo.Tests.Folders.ListFormPictures;

namespace Karpo.Tests.Folders;

public sealed unsafe class CombinedPresetTests
{
    public static TheoryData<string> Ids()
    {
        var data = new TheoryData<string>();
        foreach (var preset in Presets.All.Where(preset => preset.Combined))
        {
            data.Add(preset.Id);
        }
        return data;
    }

    /// <summary>The kinds of each list's lines a preset makes on <paramref name="machine"/>.</summary>
    private static string[][] ListsOf(string id, PresetMachine machine) =>
        [.. Presets.Lists(Presets.Find(id)!, machine).Select(list => list.Select(cell => cell.Island.Kind).ToArray())];

    private static string[] FacesOf(string id, PresetMachine machine) =>
        [.. Presets.Folder(Presets.Find(id)!, machine).Folder!.Faces.Select(face => face.Kind)];

    /// <summary>
    /// About ten of them, each its subjects a list side by side, under each subject's face: what the
    /// add list makes of each, on a laptop and on a desktop PC.
    /// </summary>
    [Fact]
    public void EachRecipeIsItsSubjectsSideBySide()
    {
        var laptop = PresetsTests.Laptop;
        var desktop = PresetsTests.Desktop;
        Assert.Equal(
            [Presets.ClockCalendarId, Presets.NotificationCenterId, Presets.TodayId, Presets.WorldClockId, Presets.MediaCenterId, Presets.QuickPanelId, Presets.NetworkHubId,
             Presets.DevicesId, Presets.InboxId, Presets.NotebookId, Presets.MarketsNewsId, Presets.PowerBatteryId],
            Presets.All.Where(preset => preset.Combined).Select(preset => preset.Id));
        // Last in the library, so the add list heads them on their own.
        Assert.All(Presets.All.SkipWhile(preset => !preset.Combined), preset => Assert.True(preset.Combined, preset.Id));

        // Windows' clock button: the notifications over the calendar, one column, two cards; the
        // time over the date with the bell to its right.
        Assert.Equal([["notificationsList", "divider", "dateHeader", "month", "focus"]], ListsOf(Presets.NotificationCenterId, laptop));
        Assert.Equal([FolderFaces.DateTimeId, FolderFaces.NotificationsId], FacesOf(Presets.NotificationCenterId, laptop));

        Assert.Equal([["timeHeader", "events", "actions"], ["weatherNow", "weatherHours", "weatherDays"]], ListsOf(Presets.TodayId, laptop));
        Assert.Equal([FolderFaces.DateTimeId, FolderFaces.WeatherId], FacesOf(Presets.TodayId, laptop));

        Assert.Equal([["clock", "clock", "clock", "clock"]], ListsOf(Presets.WorldClockId, laptop));
        Assert.Equal([FolderFaces.ClockId], FacesOf(Presets.WorldClockId, laptop));
        var clocks = Presets.Folder(Presets.Find(Presets.WorldClockId)!, laptop).Folder!.Cells.Select(cell => cell.Island.Clock!).ToList();
        Assert.Equal(["", "Eastern Standard Time", "GMT Standard Time", "Tokyo Standard Time"], clocks.Select(clock => clock.TimeZone));
        Assert.Equal(["", "New York", "London", "Tokyo"], clocks.Select(clock => clock.Label));
        // Every zone is one Windows knows.
        Assert.All(clocks.Skip(1), clock => Assert.NotNull(TimeZoneInfo.FindSystemTimeZoneById(clock.TimeZone)));

        Assert.Equal([["media", "mediaSources"], ["soundVolume", "soundOutput", "divider", "soundMixer", "actions"]], ListsOf(Presets.MediaCenterId, laptop));
        Assert.Equal([FolderFaces.NowPlayingId, FolderFaces.SoundId], FacesOf(Presets.MediaCenterId, laptop));

        // What is playing over the tiles, a divider between; Wi-Fi and Bluetooth first, as Windows'
        // panel has them, and no power mode; the battery's line, its divider and its face only where
        // there is one.
        Assert.Equal([["media", "divider", "wifiSwitch", "bluetoothSwitch", "accessibility", "liveCaptions", "cast", "project", "toggles", "brightness", "soundVolume", "divider", "battery", "actions"]], ListsOf(Presets.QuickPanelId, laptop));
        Assert.Equal([["media", "divider", "wifiSwitch", "bluetoothSwitch", "accessibility", "liveCaptions", "cast", "project", "toggles", "brightness", "soundVolume", "actions"]], ListsOf(Presets.QuickPanelId, desktop));
        // The row of toggles holds the panel's other tiles, not Wi-Fi and Bluetooth a second time;
        // rotation lock only on a screen that turns.
        var row = Presets.Folder(Presets.Find(Presets.QuickPanelId)!, laptop).Folder!.Cells.Single(cell => cell.Island.Kind == "toggles").Island.Toggles!.Shown;
        Assert.Equal([Toggle.AirplaneMode, Toggle.Vpn, Toggle.EnergySaver, Toggle.NightLight, Toggle.MobileHotspot, Toggle.NearbyShare], row);
        var tablet = laptop with { Rotates = true };
        Assert.Contains(Toggle.RotationLock, Presets.Folder(Presets.Find(Presets.QuickPanelId)!, tablet).Folder!.Cells.Single(cell => cell.Island.Kind == "toggles").Island.Toggles!.Shown);
        Assert.DoesNotContain(Toggle.Wifi, row);
        Assert.DoesNotContain(Toggle.Bluetooth, row);
        Assert.Equal([FolderFaces.WifiId, FolderFaces.SoundId, FolderFaces.BatteryId], FacesOf(Presets.QuickPanelId, laptop));
        Assert.Equal([FolderFaces.WifiId, FolderFaces.SoundId], FacesOf(Presets.QuickPanelId, desktop));

        Assert.Equal([["wifi", "actions"], ["network", "divider", "vpnConnections", "actions", "divider", "airplaneMode", "mobileHotspot", "nearbyShare"]], ListsOf(Presets.NetworkHubId, laptop));
        Assert.Equal([FolderFaces.WifiId, FolderFaces.VpnId], FacesOf(Presets.NetworkHubId, laptop));

        Assert.Equal([["bluetooth", "actions"], ["wirelessDisplays", "actions", "divider", "removableDevices"]], ListsOf(Presets.DevicesId, laptop));
        Assert.Equal([FolderFaces.BluetoothId, FolderFaces.RemovableId], FacesOf(Presets.DevicesId, laptop));

        Assert.Equal([["mail"], ["notificationsList", "actions"]], ListsOf(Presets.InboxId, laptop));
        Assert.Equal([FolderFaces.MailId, FolderFaces.NotificationsId], FacesOf(Presets.InboxId, laptop));

        // Two pages, no clipboard history (2026-10-12): that is a preset of its own.
        Assert.Equal([["todo"], ["note"]], ListsOf(Presets.NotebookId, laptop));
        Assert.Equal([FolderFaces.IconId], FacesOf(Presets.NotebookId, laptop));

        Assert.Equal([["markets", "markets", "markets", "markets"], ["rss"]], ListsOf(Presets.MarketsNewsId, laptop));
        Assert.Equal([FolderFaces.MarketsId, FolderFaces.NewsId], FacesOf(Presets.MarketsNewsId, laptop));
        var markets = Presets.Folder(Presets.Find(Presets.MarketsNewsId)!, laptop).Folder!;
        Assert.Equal([FolderListWidth.Wide, FolderListWidth.Medium], markets.Widths);
        // The face follows the market as a whole, its own share, set from the start.
        Assert.Equal("^GSPC", markets.Face.Market!.Symbol);

        Assert.Equal([["battery", "energySaver", "powerMode", "actions"], ["signOut", "lock", "sleep", "restart", "shutDown"]], ListsOf(Presets.PowerBatteryId, laptop));
        Assert.Equal([["energySaver", "powerMode", "actions"], ["signOut", "lock", "sleep", "restart", "shutDown"]], ListsOf(Presets.PowerBatteryId, desktop));
        Assert.Equal([FolderFaces.BatteryId, FolderFaces.PowerId], FacesOf(Presets.PowerBatteryId, laptop));
        Assert.Equal([FolderListWidth.Medium, FolderListWidth.Narrow], Presets.Folder(Presets.Find(Presets.PowerBatteryId)!, laptop).Folder!.Widths);
    }

    /// <summary>
    /// A combined group wears its faces as a folder may: never a face that does not repeat twice, at
    /// most as many as a folder wears, each its own copy, so two folders made never share one.
    /// </summary>
    [Theory]
    [MemberData(nameof(Ids))]
    public void ItsFacesAreOnesAFolderMayWear(string id)
    {
        var preset = Presets.Find(id)!;
        var one = Presets.Folder(preset, PresetsTests.Laptop).Folder!;
        var two = Presets.Folder(preset, PresetsTests.Laptop).Folder!;

        Assert.NotEmpty(one.Faces);
        for (var index = 0; index < one.Faces.Count; index++)
        {
            Assert.True(FolderFaces.CanWear(one, one.Faces[index].Info, replacing: index), $"{id} wears {one.Faces[index].Kind} twice");
            Assert.NotSame(one.Faces[index], two.Faces[index]);
        }
        // What a file keeps of them is what was made.
        var read = Configs.Repaired(Configs.With(Presets.Folder(preset, PresetsTests.Laptop)).ToJson()).Layouts![0]!.Islands.Single().Folder!;
        Assert.Equal(one.Faces.Select(face => face.Kind), read.Faces.Select(face => face.Kind));
    }

    // --- Pictures -----------------------------------------------------------------------------------

    /// <summary>The day pictured: a Tuesday afternoon of the calendar's sample week.</summary>
    private static readonly DateTime Frozen = new(2026, 9, 22, 14, 40, 7, DateTimeKind.Local);

    /// <summary>A store of the calendar's sample week, a calendar for each colour among its events.</summary>
    private static CalendarStore SampleCalendars()
    {
        var store = new CalendarStore();
        foreach (var calendar in CalendarWeatherFolderTests.Sample.GroupBy(item => item.Colour))
        {
            store.Add(new ListCalendar($"test:{calendar.Key}", calendar.Key.ToString(), calendar.Key, [.. calendar]));
        }
        return store;
    }

    /// <summary>An inbox of five, three unread.</summary>
    private static List<MailItem> Inbox()
    {
        (string From, string Subject, bool Unread)[] letters =
        [
            ("Sandra Gerber", "Lunch on Friday? The new place by the lake has a terrace", true),
            ("GitHub", "[karpo-app] CI passed on master", true),
            ("Adrian Hager", "Re: Quarterly review — slides attached", false),
            ("Swiss International Air Lines", "Your boarding pass for LX 1832", true),
            ("Beat Hagenbuch", "Photos", false),
        ];
        var now = new DateTimeOffset(Frozen);
        return [.. letters.Select((letter, index) => new MailItem($"{index}", letter.From, $"{index}@example.com", letter.Subject, null, now.AddMinutes(-4 - (index * 47)), letter.Unread, null))];
    }

    /// <summary>
    /// The lines whose height is what they hold — a list of networks, devices, apps or messages —
    /// which a recipe leaves to grow: measured at the most they may be.
    /// </summary>
    private static readonly HashSet<string> Growing =
        ["mail", "notificationsList", "vpnConnections", "removableDevices", "wirelessDisplays", "mediaSources", "rss", "soundOutput", "soundInput", "soundMixer", "bluetooth", "wifi", "events", "clipboard", "todo"];

    /// <summary>
    /// Each combined group as it opens at 150 %, dark beside light, every line on its stand-in:
    /// combined-&lt;id&gt;.png. And each stands whole in the 70 % of a 1440p screen at 150 % over a
    /// 48-DIP bar a folder may take (the room to its top with a Fill line, §173), with every line that grows with what it holds at the most it may
    /// grow to (a full inbox, every network in range), and fits across the screen: no list of it ever
    /// scrolls. Needs Direct2D: KARPO_UI_TESTS=1.
    /// </summary>
    [UiFact]
    public void CombinedGroupPicturesAndFit()
    {
        Offscreen.OnStaThread(gfx => WithStandIns(() =>
        {
            foreach (var preset in Presets.All.Where(preset => preset.Combined))
            {
                var folders = new List<FolderIsland>();
                try
                {
                    var panels = new List<(string, Look, FolderIsland, FolderPlan)>();
                    foreach (var dark in new[] { true, false })
                    {
                        var folder = Opened(gfx, Presets.Folder(preset, PresetsTests.Laptop), dark ? "dark" : "light");
                        folders.Add(folder);
                        var look = LookFor(dark);
                        var (natural, plan) = Plan(folder, look, 1.5f);
                        // On the screen itself: a Fill line gives way under the folder's most (§171),
                        // so what stands whole is that nothing is left out there.
                        var worst = FolderFlyout.Build(look, 1.5f, folder.View, folder.Options, [.. folder.Tiles.Select(tile => tile.Cell)], 1440 - 72, natural: AtTheirMost(folder, natural));
                        Assert.True(!worst.Hidden.Any(), $"{preset.Id} leaves out {string.Join(",", worst.Hidden)}");
                        // Under the room to the top of the screen less an arranged list's "+" (§173, §185).
                        var cap = FolderFlyout.Reach(1440 - 72, 0, 1.5f) / 1.5f;
                        Assert.True(worst.Height / 1.5f <= cap + 0.5f, $"{preset.Id} stands {worst.Height / 1.5f} DIP of {cap}");
                        Assert.True(worst.Width / 1.5f <= 2560 / 1.5f, $"{preset.Id} is {worst.Width / 1.5f} DIP wide");
                        panels.Add(($"{preset.Title}: {(dark ? "dark" : "light")}, {plan.Width / 1.5f:0} × {plan.Height / 1.5f:0} DIP", look, folder, plan));
                    }
                    Sheet(gfx, $"combined-{preset.Id}.png", 1.5f, panels);
                }
                finally
                {
                    foreach (var folder in folders)
                    {
                        folder.Closed();
                        folder.Dispose();
                    }
                }
            }
        }));
    }

    /// <summary>
    /// <paramref name="body"/> with every line a preset holds on its stand-in — the calendar's sample week, the shares, a feed,
    /// an inbox, what is playing, a battery, no notifications of the person's — each put back after. The switches, the
    /// sliders, the radios and the devices read this PC, and change nothing; nothing reaches the network or the accounts.
    /// </summary>
    internal static void WithStandIns(Action body)
    {
        var was = (Clock.Now, CalendarIsland.Made, BatteryIsland.Probe, BatteryIsland.Made, MediaIsland.Stand, MarketIsland.Made, RssIsland.Made, MailIsland.Stand);
        using var mail = new MailStore(new AccountSet([]));
        mail.Answered(Inbox(), unread: 3);
        Clock.Now = () => Frozen;
        CalendarIsland.Made = SampleCalendars();
        CalendarApp.Stand = () => "outlookcal:";
        BatteryIsland.Probe = () => new SystemStatus.Battery(76, false, false);
        BatteryIsland.Made = () => new SampleBattery();
        MediaIsland.Stand = new MediaStand();
        MarketIsland.Made = MarketOffscreenTests.Settled(new()
        {
            ["MSFT"] = MarketOffscreenTests.Answer("MSFT", "Microsoft Corporation", price: 412.34, percent: 1.24, previous: 407.3),
            ["AAPL"] = MarketOffscreenTests.Answer(),
            ["NVDA"] = MarketOffscreenTests.Answer("NVDA", "NVIDIA Corporation", price: 181.2, percent: 2.1, previous: 177.47),
            ["^GSPC"] = MarketOffscreenTests.Answer("^GSPC", "S&P 500", price: 6417.29, percent: 0.02, previous: 6416.02),
            ["MSFT 5y"] = MarketRangeTests.Ranged("MSFT", 261, 7 * 86400, 289, 412.34, 289.1, wander: 40),
            ["AAPL 5y"] = MarketRangeTests.Ranged("AAPL", 261, 7 * 86400, 142, 336.16, 140.2, wander: 22, name: "Apple Inc."),
            ["NVDA 5y"] = MarketRangeTests.Ranged("NVDA", 261, 7 * 86400, 14.5, 181.2, 14.1, wander: 9, name: "NVIDIA Corporation"),
            ["^GSPC 5y"] = MarketRangeTests.Ranged("^GSPC", 261, 7 * 86400, 3270, 6417.29, 3240.5, wander: 260, name: "S&P 500"),
        });
        RssIsland.Made = RssSamples.Settled(Frozen);
        MailIsland.Stand = mail;
        // No notifications of the person's: the store reads a database that is not there.
        var empty = Path.Combine(Isolation.Folder, "combined-notifications");
        var shared = typeof(NotificationStore).GetField("shared", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        var sharedWas = shared.GetValue(null);
        shared.SetValue(null, new NotificationStore(Path.Combine(empty, "none.db"), Path.Combine(empty, "copy"), null, app => (app, null)));
        try
        {
            body();
        }
        finally
        {
            shared.SetValue(null, sharedWas);
            CalendarApp.Stand = null;
            (Clock.Now, CalendarIsland.Made, BatteryIsland.Probe, BatteryIsland.Made, MediaIsland.Stand, MarketIsland.Made, RssIsland.Made, MailIsland.Stand) = was;
        }
    }

    /// <summary>
    /// The folder <paramref name="holder"/> (a preset's) opened as <paramref name="name"/>, its weather on a saved forecast and
    /// its feed on the sample's; inside <see cref="WithStandIns"/>.
    /// </summary>
    internal static FolderIsland Opened(Gfx gfx, BarItem holder, string name)
    {
        foreach (var cell in holder.Folder!.Cells)
        {
            if (cell.Island.Weather is not null)
            {
                cell.Island.Weather = SavedWeather.Options();
            }
            if (cell.Island.Rss is not null)
            {
                cell.Island.Rss = new RssOptions { Address = RssSamples.Address, Title = "Example News" };
            }
        }
        var folder = new FolderIsland($"{Owner}/{holder.Id}-{name}", holder, gfx, new Config());
        foreach (var line in folder.Tiles.Select(tile => tile.Island).OfType<WeatherLineTile>())
        {
            line.Source = SavedWeather.Source();
        }
        _ = ((IIsland)folder).Refresh(gfx);
        folder.Opened(gfx);
        return folder;
    }

    /// <summary>
    /// The lines of <paramref name="folder"/> at their own heights (<paramref name="natural"/>), every one that grows with what
    /// it holds (<see cref="Growing"/>) at the most it may grow to.
    /// </summary>
    internal static List<float> AtTheirMost(FolderIsland folder, IReadOnlyList<float> natural) =>
        [.. natural.Select((height, index) => Growing.Contains(folder.Tiles[index].Cell.Island.Kind) ? 100_000f : height)];
}
