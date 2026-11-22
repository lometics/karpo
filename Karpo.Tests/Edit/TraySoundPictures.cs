// tray: Explorer's volume icon on the bar's tray, and gone from it once its row in the tray's menu says
// "Hide completely" (OptionTray.VolumeIcon, docs\widgets.md §202), on the dock's real scene, to
// KARPO_PNG_DIR:
//   tray-sound-shown-<look>-<scale>.png   the speaker first, as the person dragged it, then two apps;
//   tray-sound-hidden-<look>-<scale>.png  the same tray split by the file the menu wrote: no speaker.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
using static TerraFX.Interop.Windows.S;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    /// <summary>Windows' speaker (the Volume Mixer's icon) at <paramref name="size"/> px, standing in for Explorer's volume icon.</summary>
    private static Pixels? SpeakerIcon(int size)
    {
        var icon = HICON.NULL;
        fixed (char* name = Path.Combine(Environment.SystemDirectory, "SndVol.exe"))
        {
            if (SHDefExtractIconW(name, 0, 0, &icon, null, (uint)size).Value != S_OK || icon == HICON.NULL)
            {
                return null;
            }
        }
        try
        {
            return Tray.Render(icon, size);
        }
        finally
        {
            _ = DestroyIcon(icon);
        }
    }

    [UiFact]
    public void TraySoundSheets()
    {
        WindowShots.OnHiddenDesktop("tray-sound", (gfx, _) =>
        {
            foreach (var dark in new[] { true, false })
            {
                foreach (var dpi in new uint[] { 96, 144 })
                {
                    var look = AddListPictures.LookFor(dark) with { Animations = false };
                    var config = new Config { Height = 48, Style = BarStyle.Islands };
                    var area = new RECT { left = 0, top = 0, right = (int)(480 * dpi / 96), bottom = (int)(dpi * 10) };
                    var name = $"{(dark ? "dark" : "light")}-{dpi * 100 / 96}";
                    var px = (int)MathF.Ceiling(Layout.TrayIconPx(dpi, config));
                    var apps = DraggedTrayIcons(2, px);
                    var speaker = new TrayIcon("pictures/volume", HWND.NULL, 0, new Guid(OptionTray.VolumeIcon), 0x8000, 4,
                        SpeakerIcon(px), "Headphones (Bluetooth): 22%", Hidden: true, Identity: OptionTray.VolumeIcon);
                    List<TrayIcon> all = [speaker, .. apps];
                    List<TrayEntry> entries = [.. all.Select(icon => new TrayEntry(icon.Identity, icon.Hidden))];

                    // The person's file: the speaker dragged onto the taskbar, first.
                    var file = Config.Parse($$"""{ "layouts": [ { "id": "home", "name": "Home", "islands": [ { "id": "t", "kind": "tray", "trayIcons": { "states": { "{{OptionTray.VolumeIcon}}": "Taskbar" }, "order": [ "{{OptionTray.VolumeIcon}}" ] } } ] } ] }""");
                    file.Repair();
                    List<TrayIcon> Shown(Config read) =>
                        [.. TrayLayout.Split(TrayLayout.Trays(read).Single().TrayIcons, entries).Shown.Select(entry => all.First(icon => icon.Identity == entry.Identity))];
                    var before = Shown(file);
                    Assert.Equal(OptionTray.VolumeIcon, before[0].Identity);
                    var shown = new Content { Tray = before, TrayOverflow = false };
                    TrayShot(gfx, new Layout(gfx, dpi, area, area, Edge.Bottom, config, look, TrayBar(), shown), shown, look, null, $"tray-sound-shown-{name}.png");

                    // "Hide completely" on its row, written and read back as the dock reads the file.
                    Assert.True(TrayLayout.Set(TrayLayout.Trays(file).Single().TrayIcons!, OptionTray.VolumeIcon, TrayState.Hidden));
                    var after = Shown(Config.Parse(file.ToJson()));
                    Assert.DoesNotContain(after, icon => icon.Identity == OptionTray.VolumeIcon);
                    Assert.Equal(2, after.Count);
                    var hidden = new Content { Tray = after, TrayOverflow = false };
                    TrayShot(gfx, new Layout(gfx, dpi, area, area, Edge.Bottom, config, look, TrayBar(), hidden), hidden, look, null, $"tray-sound-hidden-{name}.png");
                }
            }
        });
    }
}
