// The free version's limit as the dock shows it (Karpo.Dock\LicenseRows.cs), on a real flyout on a
// desktop of its own, to KARPO_PNG_DIR: the refusal of an add made anyway
// (license-refusal-<look>-<scale>.png). What the rows are is LicenseRowsTests'; §576 the add list at the
// limit is the add page's (SettingsPanelSheets).
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Edit;

public sealed unsafe class LicensePictures
{
    [UiFact]
    public void LicenseSheets()
    {
        WindowShots.OnHiddenDesktop("license", (gfx, instance) =>
        {
            foreach (var dark in new[] { true, false })
            {
                foreach (var scale in new[] { 1f, 1.5f })
                {
                    var look = AddListPictures.LookFor(dark);
                    var work = new RECT { left = 0, top = 0, right = (int)(1920 * scale), bottom = (int)(1032 * scale) };
                    var anchor = new POINT { x = work.right / 2, y = work.bottom };
                    var name = $"{(dark ? "dark" : "light")}-{(int)(scale * 100)}";

                    using (var flyout = new Flyout(gfx, instance, 0))
                    {
                        flyout.Show(gfx, look, scale, LicenseRows.Refusal(20, Karpo.Dock.Dock.CMD_GET_PRO), FlyoutLayout.List, anchor, work, Edge.Bottom, S.DockLicenseTitle);
                        Assert.True(flyout.IsOpen);
                        WindowShots.Settle();
                        Assert.True(WindowShots.Save(flyout.Hwnd, $"license-refusal-{name}.png"), "the refusal came back blank");
                        flyout.Hide();
                    }
                }
            }
        });
    }
}
