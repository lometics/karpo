// What a switch's line says beside its name (SwitchIsland.Beside, docs\widgets.md §196): Wi-Fi's
// network and Bluetooth's devices, in Windows' caption size on the name's baseline. Pictured to
// KARPO_PNG_DIR as switch-detail-<dark|light>-<100|150>.png — a long network's name and two devices,
// at 100 % and 225 % text — and switch-detail-rtl-<100|150>.png, the same lines in Arabic, mirrored as
// the open folder mirrors them. "Turning on…" beside the name is pictured with the rest of a switch on
// its way (switch-pending-*.png). Needs KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Dock.QuickSettings;
using Karpo.Dock.QuickSettings.Services;
using Karpo.Tests.Islands;
using Karpo.Tests.Localization;
using static Karpo.Tests.Folders.ListFormPictures;
using static Karpo.Tests.Islands.Offscreen;

namespace Karpo.Tests.QuickSettings;

[Collection(InLanguage.Collection)]
public sealed unsafe class SwitchDetailPictures
{
    [UiFact]
    public void TheDetailBesideTheNameIsPictured() =>
        OnStaThread(gfx =>
        {
            QuickSwitches.Source = which => which switch
            {
                QuickSwitch.WifiRadio or QuickSwitch.BluetoothRadio => new SampleSwitch(which.ToString(), on: true, switches: true),
                _ => new SampleSwitch(which.ToString(), on: false, switches: true),
            };
            QuickPageTile.Sources = new QuickSources
            {
                Wifi = LongNamed,
                Bluetooth = static () => new SampleBluetooth(),
                Battery = static () => new SampleBattery(),
                Desktops = static () => new SampleDesktopNames(),
            };
            try
            {
                foreach (var scale in new[] { 1f, 1.5f })
                {
                    foreach (var dark in new[] { true, false })
                    {
                        Pictured($"switch-detail-{(dark ? "dark" : "light")}-{scale * 100:0}.png", scale, dark, mirrored: false);
                    }
                    using (InLanguage.Use("ar"))
                    {
                        Pictured($"switch-detail-rtl-{scale * 100:0}.png", scale, dark: true, mirrored: true);
                    }
                }
            }
            finally
            {
                QuickPageTile.Sources = null;
                QuickSwitches.Source = null;
            }

            void Pictured(string file, float scale, bool dark, bool mirrored)
            {
                var panels = new List<(string, Look, FolderIsland, FolderPlan)>();
                try
                {
                    foreach (var textScale in TextScales)
                    {
                        var folder = Folder(gfx, Line(ListKind.WifiSwitch), Line(ListKind.BluetoothSwitch), Line(ListKind.AirplaneMode));
                        var look = LookFor(dark, textScale);
                        var (_, plan) = Plan(folder, look, scale);
                        panels.Add(($"Beside the name: {textScale * 100:0} % text", look, folder, plan));                    }
                    if (!mirrored)
                    {
                        Sheet(gfx, file, scale, panels);
                        return;
                    }
                    // Mirrored as the open folder mirrors a line (FolderFlyout, Mirror.Begin): each panel alone.
                    const float margin = 24;
                    var width = margin + panels.Sum(panel => panel.Item4.Width + margin);
                    var height = (2 * margin) + panels.Max(panel => panel.Item4.Height);
                    Bitmap(width, height, dc =>
                    {
                        var backdrop = new TerraFX.Interop.DirectX.DXGI_RGBA(0.20f, 0.24f, 0.30f, 1);
                        dc->Clear(&backdrop);
                        var left = margin;
                        foreach (var (_, look, folder, plan) in panels)
                        {
                            var context = dc;
                            Shifted(context, left, margin, () =>
                            {
                                var was = Mirror.Begin(context, 0, plan.Width);
                                try
                                {
                                    Panel(context, gfx, folder, plan, look, FlyoutTheme.For(look), scale);
                                }
                                finally
                                {
                                    Mirror.End(context, was);
                                }
                            });
                            left += plan.Width + margin;
                        }
                    }, bitmap => Save(bitmap, Path.Combine(Pictures, file)));
                }
                finally
                {
                    foreach (var (_, _, folder, _) in panels)
                    {
                        folder.Closed();
                        folder.Dispose();
                    }
                }
            }
        });

    /// <summary>The sample networks, on the one with the longest name.</summary>
    private static IWifiSource LongNamed()
    {
        var wifi = new SampleWifi();
        var longest = wifi.Read().Networks.OrderByDescending(network => network.Ssid.Length).First(network => network.Security != WifiSecurity.Enterprise);
        wifi.Connect(longest, "carrots!", automatic: false);
        return wifi;
    }
}
