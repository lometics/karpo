// edit: the selected widget's flyout, the person's own case (the clock's, which is a folder now; the
// timer preset's folder stands in for it here, as a timer's own panel did until it became that
// folder, docs\widgets.md §92): clicked in edit mode, a widget opens Karpo's
// settings on its page and its flyout too, which stays as long as it is the one selected
// (SelectedFlyout.cs, Dock.FlyoutPreview.cs). Opened as a look (FlyoutHost.Previewing), from a widget
// on a bar along the top right under the edit bar: a folder stands where it stands at rest, the edit bar over it
// (§329), just off its own bar,
// takes no focus, and stays through the settings app taking the focus and a press elsewhere — where
// the same flyout opened by a click closes. Run on a hidden desktop.

using Karpo.Dock;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Tests.Islands;

public sealed unsafe partial class FlyoutOffscreenTests
{
    [UiFact]
    public void TheSelectedClocksFlyoutStandsUnderTheEditBarAndThroughTheSettingsApp()
    {
        OnStaThread(desktop: true, gfx =>
        {
            var instance = (HINSTANCE)(void*)GetModuleHandleW(null);
            Register(instance, CountdownClassName);
            // A 2880 x 1620 display at 150 %, a bar 72 px tall along its top, the clock in its middle
            // and the edit bar right under it (Dock.ShowDecks), where a popup of that bar opens.
            var work = new RECT { left = 0, top = 72, right = 2880, bottom = 1620 };
            var anchor = new POINT { x = 1440, y = 72 };
            var deck = new RECT { left = 1080, top = 84, right = 1800, bottom = 156 };
            FlyoutHost.Avoided = [deck];
            try
            {
                var holder = Karpo.Core.Presets.Folder(Karpo.Core.Presets.Find(Karpo.Core.Presets.TimerId)!, new Karpo.Core.PresetMachine(false, []));
                using var folder = new FolderIsland($"layout:default/{holder.Id}", holder, gfx, new Karpo.Core.Config(), new Countdowns());
                using var clock = new FolderFlyout(gfx, instance, 0, CountdownClassName);
                Target = message => clock.Message(message.Msg, message.WParam, message.LParam);
                FlyoutHost.Previewing = true;
                try
                {
                    clock.Show(gfx, LookFor(true), 1.5f, anchor, work, Karpo.Core.Edge.Top, folder);
                }
                finally
                {
                    FlyoutHost.Previewing = false;
                }
                Pump();
                Assert.True(clock.IsOpen);
                var panel = clock.PanelOnScreen;
                // §329: the edit bar stands over a folder; the folder is not pushed past it, it stays just off its bar.
                Assert.True(panel.top - anchor.y <= 24, $"the edit bar pushed the clock's flyout off its bar: from {panel.top}, its bar to {anchor.y}");
                Assert.True(panel.top >= anchor.y, "the clock's flyout stands over its bar, and the clock selected on it");
                Assert.True(panel.bottom <= work.bottom, "the clock's flyout runs off the display");
                Assert.NotEqual(clock.Hwnd, GetForegroundWindow());

                // The settings app the click opened takes the focus, and the person clicks in it: the flyout stays.
                _ = clock.Message(WM.WM_ACTIVATE, (WPARAM)(nuint)WA_INACTIVE, 0);
                _ = clock.Message(WM.WM_LBUTTONDOWN, 0, (LPARAM)(nint)((2 << 16) | 2));
                Pump();
                Assert.True(clock.IsOpen, "the selected clock's flyout closed when the settings app had the focus");
                clock.Hide();
                Pump();

                // Opened by a click outside edit mode, the same flyout closes then, as every flyout does.
                clock.Show(gfx, LookFor(true), 1.5f, anchor, work, Karpo.Core.Edge.Top, folder);
                Pump();
                _ = clock.Message(WM.WM_ACTIVATE, (WPARAM)(nuint)WA_INACTIVE, 0);
                Pump();
                Assert.False(clock.IsOpen, "a clock a click opened stayed when another window had the focus");
                Target = null;
                Assert.Empty(Failures);
            }
            finally
            {
                FlyoutHost.Avoided = [];
            }
        });
    }
}
