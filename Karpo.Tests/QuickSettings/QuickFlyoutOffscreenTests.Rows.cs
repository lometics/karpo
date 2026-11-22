// A flyout opened for a look takes no focus, and the media flyout's words are at the left, the art
// at the right. (The network's and Bluetooth's lists that kept a height of their own are lines of
// their presets' folders now: QuickPageLineTests; so is the sound flyout's mixer: SoundPresetTests.)

using Karpo.Dock;
using Karpo.Dock.QuickSettings;
using Karpo.Dock.QuickSettings.Controls;
using Karpo.Dock.QuickSettings.Services;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Tests.QuickSettings;

public sealed unsafe partial class QuickFlyoutOffscreenTests
{
    /// <summary>
    /// A flyout opened for the settings app's look at its height (FlyoutHost.Previewing) takes no
    /// focus and stays when another window has it — the settings app, its field being typed in — where
    /// one opened by a click closes then. Shown again for the next height, it is still a look; opened
    /// afresh by a click, it is not.
    /// </summary>
    [UiFact]
    public void AFlyoutOpenedForALookTakesNoFocusAndStays()
    {
        OnHiddenDesktop((gfx, instance) =>
        {
            var samples = new Samples();
            using var flyout = new QuickFlyout(gfx, instance, 0, samples.Sources, ClassName);
            Target = message => flyout.Message(message.Msg, message.WParam, message.LParam);
            var tall = new RECT { left = 0, top = 0, right = 1920, bottom = 1600 };
            var anchor = new POINT { x = 900, y = 1600 };
            var look = LookFor(true);

            FlyoutHost.Previewing = true;
            try
            {
                flyout.Show(gfx, look, 1.5f, anchor, tall, Karpo.Core.Edge.Bottom, QuickKind.RenameDesktop);
            }
            finally
            {
                FlyoutHost.Previewing = false;
            }
            Pump();
            Assert.NotEqual(flyout.Hwnd, GetForegroundWindow());
            _ = flyout.Message(WM.WM_ACTIVATE, (WPARAM)(nuint)WA_INACTIVE, 0);
            Pump();
            Assert.True(flyout.IsOpen, "a look closed when another window had the focus");
            flyout.Hide();
            Pump();

            // Opened by a click: another window taking the focus closes it, as every flyout's does.
            flyout.Show(gfx, look, 1.5f, anchor, tall, Karpo.Core.Edge.Bottom, QuickKind.RenameDesktop);
            Pump();
            _ = flyout.Message(WM.WM_ACTIVATE, (WPARAM)(nuint)WA_INACTIVE, 0);
            Pump();
            Assert.False(flyout.IsOpen, "a flyout a click opened stayed when another window had the focus");
            Target = null;
            Assert.Empty(Failures);
        });
    }
}
