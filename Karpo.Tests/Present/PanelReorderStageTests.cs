// §551: a row of the settings panel dragged into order, asked of the real dock (Karpo.exe --stage on a desktop of its own,
// PanelPopupPlaceTests.Drive): the taskbar menu's Task Manager dragged by its handle over the layouts (§581 its first) is its first —
// the file says so and the bar's menu draws it so (Dock.BarRows) —, and Ctrl+Z puts the menu back as it was, the default,
// which the file holds nothing of.

using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using Xunit.Abstractions;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Present;

public sealed class PanelReorderStageTests(ITestOutputHelper output)
{
    [UiFact]
    public void AnEntryDraggedMovesTheBarMenuAndCtrlZPutsItBack() => PanelPopupPlaceTests.Drive(output, Edge.Bottom, null, probe =>
    {
        probe.Open();
        probe.Nav(PanelPage.Taskbar);
        Assert.DoesNotContain("\"barMenu\"", probe.ConfigText(), StringComparison.Ordinal);
        // §585: the menu an expander, Behavior's last row, the page's: scrolled to the page's foot, opened, its entries in sight.
        var menu = "Taskbar/" + S.DockTaskbarBehaviour + "/" + S.SettingsBarMenuHeader;
        probe.Wheel(-20);
        probe.Click(probe.Spot(PanelHitKind.Head, menu).Center);
        var section = menu + "/";
        probe.WheelTo(PanelHitKind.Handle, section + S.CoreBarMenuTaskManager);
        var taskManager = probe.Spot(PanelHitKind.Handle, section + S.CoreBarMenuTaskManager);
        var layouts = probe.Spot(PanelHitKind.Handle, section + S.CoreBarMenuLayouts);
        probe.Drag(taskManager.Center, new POINT { x = layouts.Center.x, y = layouts.Rect.top + 2 });

        var config = Wait(probe, text => text.Contains("\"barMenu\"", StringComparison.Ordinal));
        Assert.Equal(BarMenuAction.TaskManager, config.BarMenuEntries[0].Action);
        var drawn = TheDock.BarRows(config.BarMenuEntries, [], windowsTerminal: true, ShortcutLooks.None).Items;
        Assert.Equal(S.CoreBarMenuTaskManager, drawn[0].Text);
        Assert.Equal(S.CoreBarMenuTaskbarSettings, drawn[^1].Text);

        probe.Undo();
        config = Wait(probe, text => !text.Contains("\"barMenu\"", StringComparison.Ordinal));
        Assert.Equal(BarMenus.Default().Select(entry => entry.Action), config.BarMenuEntries.Select(entry => entry.Action));
        probe.Finish();
    });

    /// <summary>The dock's config once <paramref name="done"/> holds of its file, within five seconds.</summary>
    private static Config Wait(PanelPopupPlaceTests.Probe probe, Func<string, bool> done)
    {
        var until = Environment.TickCount64 + 5000;
        var text = probe.ConfigText();
        while (!done(text) && Environment.TickCount64 < until)
        {
            probe.Pump(100);
            text = probe.ConfigText();
        }
        Assert.True(done(text), "the config never came to it: " + text);
        return Config.Parse(text);
    }
}
