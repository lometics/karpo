// Each ready-made group as it is added (Karpo.Core\Presets.cs): the folder a group makes, opened as
// the bar opens it, on a desktop of its own, at 100 % and 150 %, dark and light, captured as the
// compositor draws it (Edit\WindowShots) to KARPO_PNG_DIR as
// preset-<id>-<dark|light>-<100|150>.png — System, Stocks, Quick settings, Widgets board,
// Notification center, Focus, Media, Storage and Connectivity. A list among them draws a line only
// where its recipe puts a divider, between two groups of its lines (2026-10-08: "add proper
// dividers for the ready made widget groups"); the grids have none.
//
// Everything a group's widgets would read from the world is a stand-in: the time, the calendar,
// the battery, what is playing, the shares' prices, the drives and the notifications, so the
// pictures are the same on every PC and nothing reaches the network or the person's notifications.
// The switches, the sliders and the graphs read this PC as they would on the bar, and change
// nothing.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Dock.QuickSettings.Services;
using Karpo.Tests.Core;
using Karpo.Tests.Edit;
using Karpo.Tests.Forecasts;
using Karpo.Tests.Islands;
using Karpo.Tests.Storage;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Folders;

[Collection(AttachedDrivesCollection.Collection)]
public sealed unsafe class PresetFolderPictures
{
    private const string Owner = "layout:default";

    private const string SystemVolume = @"\\?\Volume{11111111-1111-1111-1111-111111111111}\";
    private const string DataVolume = @"\\?\Volume{22222222-2222-2222-2222-222222222222}\";

    private static ulong Gb(double gigabytes) => (ulong)(gigabytes * 1024 * 1024 * 1024);

    /// <summary>A laptop with the two drives the stand-in lists, as the presets read it.</summary>
    private static readonly PresetMachine Machine = new(
        HasBattery: true,
        FixedDrives:
        [
            new DriveVolume(SystemVolume, "C:", "Windows", DriveKind.Fixed, 0, 0),
            new DriveVolume(DataVolume, "D:", "Data", DriveKind.Fixed, 0, 0),
        ]);

    /// <summary>
    /// The groups drawn: every one, or those KARPO_PRESETS names ("todayGroup,inboxGroup", or
    /// "combined" for the combined groups), to look at a few without drawing them all.
    /// </summary>
    internal static IEnumerable<Preset> Drawn()
    {
        var names = Environment.GetEnvironmentVariable("KARPO_PRESETS");
        if (string.IsNullOrWhiteSpace(names))
        {
            return Presets.All;
        }
        var ids = names.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.Ordinal);
        return Presets.All.Where(preset => ids.Contains(preset.Id) || (preset.Combined && ids.Contains("combined")));
    }

    [UiFact]
    public void EachGroupAsAdded()
    {
        var clock = Clock.Now;
        var calendar = CalendarIsland.Made;
        var probe = BatteryIsland.Probe;
        var battery = BatteryIsland.Made;
        var media = MediaIsland.Stand;
        var stocks = MarketIsland.Made;
        var drives = DriveWatch.Source;
        try
        {
            Clock.Now = () => ListFormCases.Frozen;
            CalendarIsland.Made = ListFormCases.Store(ListFormCases.Week);
            BatteryIsland.Probe = () => new SystemStatus.Battery(76, false, false);
            BatteryIsland.Made = () => new SampleBattery();
            MediaIsland.Stand = new MediaStand();
            MarketIsland.Made = MarketOffscreenTests.Settled(new()
            {
                ["MSFT"] = MarketOffscreenTests.Answer("MSFT", "Microsoft Corporation", price: 412.34, percent: 1.24, previous: 407.3),
                ["AAPL"] = MarketOffscreenTests.Answer(),
                ["NVDA"] = MarketOffscreenTests.Answer("NVDA", "NVIDIA Corporation", price: 181.2, percent: 2.1, previous: 177.47),
                ["^GSPC"] = MarketOffscreenTests.Answer("^GSPC", "S&P 500", price: 6417.29, percent: 0.02, previous: 6416.02),
                // Five years, which a ready-made group's shares are over: never set, so the default.
                ["MSFT 5y"] = MarketRangeTests.Ranged("MSFT", 261, 7 * 86400, 289, 412.34, 289.1, wander: 40),
                ["AAPL 5y"] = MarketRangeTests.Ranged("AAPL", 261, 7 * 86400, 142, 336.16, 140.2, wander: 22, name: "Apple Inc."),
                ["NVDA 5y"] = MarketRangeTests.Ranged("NVDA", 261, 7 * 86400, 14.5, 181.2, 14.1, wander: 9, name: "NVIDIA Corporation"),
                ["^GSPC 5y"] = MarketRangeTests.Ranged("^GSPC", 261, 7 * 86400, 3270, 6417.29, 3240.5, wander: 260, name: "S&P 500"),
            });
            Drive[] attached =
            [
                new() { Letter = -1, Kind = DriveKind.Fixed, Volume = SystemVolume, Label = "Windows", Total = Gb(931), Free = Gb(419) },
                new() { Letter = -1, Kind = DriveKind.Fixed, Volume = DataVolume, Label = "Data", Total = Gb(1863), Free = Gb(96) },
            ];
            DriveWatch.Source = (_, _) => attached;

            WindowShots.OnHiddenDesktop("preset-folders", (gfx, instance) =>
            {
                // No notifications of the person's: this thread's store reads a database that is not there.
                var empty = Path.Combine(Isolation.Folder, "preset-notifications");
                typeof(NotificationStore).GetField("shared", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
                    .SetValue(null, new NotificationStore(Path.Combine(empty, "none.db"), Path.Combine(empty, "copy"), null, app => (app, null)));
                foreach (var preset in Drawn())
                {
                    foreach (var dark in new[] { true, false })
                    {
                        foreach (var scale in new[] { 1f, 1.5f })
                        {
                            // Added as the bar's "+" adds it: on a bar, with ids of its own.
                            var bar = new List<BarItem>();
                            var id = Presets.AddToBar(bar, new BarSlot(BarSection.Start, 0), preset, Machine);
                            Assert.Single(bar);
                            var holder = bar.Single(island => island.Id == id);
                            Assert.Equal(Presets.ItemsOn(preset, Machine).Count, holder.Folder!.Cells.Count);

                            var look = AddListPictures.LookFor(dark);
                            var work = new RECT { right = (int)(1920 * scale), bottom = (int)(1040 * scale) };
                            var anchor = new POINT { x = work.right / 2, y = work.bottom };
                            var folder = new FolderIsland($"{Owner}/{holder.Id}", holder, gfx, new Config());
                            using var flyout = new FolderFlyout(gfx, instance, 0);
                            try
                            {
                                flyout.Show(gfx, look, scale, anchor, work, Edge.Bottom, folder);
                                Assert.True(flyout.IsOpen, $"{preset.Title} did not open");
                                // Every widget of the group is in the folder that opened.
                                Assert.Equal(holder.Folder.Cells.Count, folder.Tiles.Count);
                                WindowShots.Settle(300);
                                var file = $"preset-{preset.Id}-{(dark ? "dark" : "light")}-{(int)(scale * 100)}.png";
                                Assert.True(WindowShots.Save(flyout.Hwnd, file), $"{file} came back blank");
                            }
                            finally
                            {
                                flyout.Hide();
                                folder.Dispose();
                            }
                        }
                    }
                }
            });
        }
        finally
        {
            Clock.Now = clock;
            CalendarIsland.Made = calendar;
            BatteryIsland.Probe = probe;
            BatteryIsland.Made = battery;
            MediaIsland.Stand = media;
            MarketIsland.Made = stocks;
            DriveWatch.Source = drives;
        }
    }
}
