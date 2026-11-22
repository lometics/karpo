// The system widgets on a bar down a side, as the dock's real scene lays them out and draws them (the
// person, 2026-10-13: "make sure that all the widgets look good on the side as well ... dont try to
// flip text"): a Taskbar (islands, a card per widget) and a Dock (one connected panel) on the left
// and on the right of a display 1280 by 1400 DIPs (a tall one, to hold them all) at 150 %, dark and light, 36 and 64 DIPs thick,
// holding the status icons, the tray, Windows' network + sound + battery button, the battery, the
// notifications, now playing, the processor, the volume and the brightness sliders, the power
// button and airplane mode. To KARPO_PNG_DIR as
// side-system-<taskbar|dock>-<left|right>-<thickness>-<dark|light>-150.png.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Dock.QuickSettings.Services;
using Karpo.Tests.Folders;
using Karpo.Tests.Islands;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    [UiFact]
    public void SideSystemBarSheets()
    {
        WindowShots.OnHiddenDesktop("side-system", (gfx, instance) =>
        {
            BatteryIsland.Probe = () => new SystemStatus.Battery(88, Plugged: false, Saver: false);
            VolumeWatch.Stand = new SampleVolume { Level = 60 };
            MediaIsland.Stand = new MediaStand();
            IndicatorSources.Made = () => new SampleIndicators(new IndicatorState { Microphone = ["Teams"], Metered = true, Update = UpdateState.Due });
            try
            {
                var config = new Config();
                foreach (var dark in new[] { true, false })
                {
                    var look = AddListPictures.LookFor(dark) with { Animations = false };
                    foreach (var edge in new[] { Edge.Left, Edge.Right })
                    {
                        foreach (var thickness in new[] { 36, 64 })
                        {
                            foreach (var type in new[] { LayoutType.Taskbar, LayoutType.Dock })
                            {
                                var name = $"side-system-{(type == LayoutType.Dock ? "dock" : "taskbar")}-{edge.ToString().ToLowerInvariant()}-{thickness}-{(dark ? "dark" : "light")}-150.png";
                                EditedDisplay(gfx, instance, 144, config, look, SystemLayout(type, edge, thickness), name, dips: new System.Numerics.Vector2(1280f, 1400f), editing: false);
                            }
                        }
                    }
                }
            }
            finally
            {
                BatteryIsland.Probe = SystemStatus.ReadBatteryNow;
                VolumeWatch.Stand = null;
                MediaIsland.Stand = null;
                IndicatorSources.Made = null;
            }
        });
    }

    /// <summary>The system widgets in a bar's three sections, <paramref name="thickness"/> DIPs thick.</summary>
    private static Picture SystemLayout(LayoutType type, Edge edge, int thickness)
    {
        var machine = new PresetMachine(HasBattery: true, FixedDrives: []);
        BarItem Preset(string id) => Presets.Folder(Presets.Find(id)!, machine);
        var three = BarKind.FolderList.New();
        three.Folder.Faces = [FolderFace.Of(FolderFaces.WifiId), FolderFace.Of(FolderFaces.SoundId), FolderFace.Of(FolderFaces.BatteryId)];
        three.Normalize();
        var islands = new List<BarItem>();
        void Put(BarSection section, params BarItem[] items)
        {
            foreach (var item in items)
            {
                item.Section = section;
                islands.Add(item);
            }
        }
        Put(BarSection.Start, BarKind.AirplaneMode.New(), Preset(Presets.MediaId), Preset(Presets.CpuId), Preset(Presets.BatteryId));
        Put(BarSection.Middle, BarKind.Volume.New(), BarKind.Brightness.New());
        Put(BarSection.End, BarKind.Indicators.New(), BarKind.Tray.New(), three, Preset(Presets.NotificationsId), Preset(Presets.PowerId));
        Sections.Order(islands);
        return new Picture(new TaskbarLayout { Id = "pictures", Name = "Pictures", Type = type, Size = thickness, Show = type == LayoutType.Dock ? EdgeMode.Float : EdgeMode.Reserve, Islands = islands }, edge);
    }
}
