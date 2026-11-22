// The add list's Quick settings (Karpo.Core\AddTree.cs) holds the toggles and nothing else (2026-10-11:
// "the ones in the toggles group should only be the ones that you can click and click again to
// untoggle, directly on the taskbar without opening a flyout or folder or anything"). What a toggle is
// is a fact of the widget (WidgetInfo.Flips), and these hold the folder, the bar, the folder's line and
// the switch behind it to it — so a widget whose click opens something cannot join it, and one
// that flips cannot be left out of it. And spatial sound, a toggle since that day, loads from a file
// as it was.

using Karpo.Dock;
using Karpo.Dock.QuickSettings.Services;

namespace Karpo.Tests.Core;

public sealed class QuickSettingsFolderTests
{
    /// <summary>What Quick settings names, in its order.</summary>
    private static List<string> Quick => [.. AddTree.QuickSettings.Rows.OfType<AddLeaf>().Select(leaf => leaf.Id)];

    /// <summary>Its toggles, after the line under what they can be put in.</summary>
    private static IEnumerable<WidgetInfo> Members =>
        AddTree.QuickSettings.Rows.SkipWhile(row => row is not AddLine).OfType<AddLeaf>().Select(leaf => WidgetInfos.Of(leaf.Id));

    /// <summary>
    /// Quick settings is what the toggles are put in — the Quick settings ready-made folder, the row
    /// of toggles — a line, then every widget that flips, each once.
    /// </summary>
    [Fact]
    public void QuickSettingsIsTheRowThenEveryToggle()
    {
        Assert.Equal([Presets.QuickPanelId, "toggles"], Quick.Take(2));
        Assert.IsType<AddLine>(AddTree.QuickSettings.Rows[2]);
        Assert.Equal(
            WidgetInfos.All.Where(info => info.Flips).Select(info => info.Id).Order(),
            Members.Select(info => info.Id).Order());
        Assert.Equal(Quick.Count, Quick.Distinct().Count());
    }

    /// <summary>
    /// Every toggle flips where it stands: a switch, a taskbar entry whose click acts — no flyout, no
    /// panel, no folder —, and a folder's line whose click acts.
    /// </summary>
    [Fact]
    public void EveryToggleFlipsOnTheBarAndInAFolder()
    {
        Assert.All(Members, info =>
        {
            Assert.True(info.Flips, info.Id);
            Assert.NotNull(info.Switch);
            var entry = BarEntries.Find(info.Id);
            Assert.NotNull(entry);
            Assert.Equal(BarClick.Action, entry!.Click);
            Assert.Null(entry.Opens);
            Assert.Equal(WidgetSource.Switch, entry.Drawing);
            var line = ListWidgets.All.Single(widget => widget.Info == info);
            Assert.Equal(CellAct.Action, line.Act);
            Assert.Equal(WidgetSource.Switch, line.Drawing);
        });
    }

    /// <summary>
    /// The switch behind every toggle switches with a click and has no list to open on the bar, nor a
    /// page of its own (Windows' own switch for each; making one asks Windows nothing). Do not disturb
    /// is left out only because making its switch takes Windows' shared one.
    /// </summary>
    [Fact]
    public void NoToggleOpensAListOrAPage()
    {
        foreach (var which in Members.Select(info => info.Switch!.Value).Where(which => which != QuickSwitch.DoNotDisturb))
        {
            using var real = QuickSwitches.Windows(which);
            Assert.Null(real.Choices);
        }
    }

    /// <summary>
    /// The switches whose click opens something — VPN's list on the bar, live captions' and project's
    /// shortcuts, accessibility's page — and the hotspot's and nearby sharing's, which are lines alone
    /// (on the bar each is its preset), are not toggles, and stand in their own groups.
    /// </summary>
    [Fact]
    public void WhatOpensSomethingIsNoToggle()
    {
        foreach (var id in (string[])["vpn", "liveCaptions", "accessibility", "mobileHotspot", "nearbyShare", "security"])
        {
            var info = WidgetInfos.Of(id);
            Assert.False(info.Flips, id);
            Assert.DoesNotContain(id, Quick);
        }
        Assert.Null(BarEntries.Find("vpn"));
        Assert.Null(BarEntries.Find("mobileHotspot"));
        Assert.Null(BarEntries.Find("nearbyShare"));
    }

    /// <summary>The new toggles: the Wi-Fi and Bluetooth radios, the sound's and the microphone's mute.</summary>
    [Fact]
    public void TheRadiosAndTheSoundAreToggles()
    {
        Assert.Equal(QuickSwitch.WifiRadio, WidgetInfos.Of("wifiSwitch").Switch);
        Assert.Equal(QuickSwitch.BluetoothRadio, WidgetInfos.Of("bluetoothSwitch").Switch);
        Assert.Equal(QuickSwitch.Sound, WidgetInfos.Of("soundSwitch").Switch);
        Assert.Equal(QuickSwitch.Microphone, WidgetInfos.Of("microphoneSwitch").Switch);
        // A row of toggles holds them too, reading the widgets' own switches.
        Assert.Equal(QuickSwitch.WifiRadio, Toggles.Find(Toggle.Wifi)!.Switch);
        Assert.Equal(QuickSwitch.Sound, Toggles.Find(Toggle.Sound)!.Switch);
        Assert.Equal(QuickSwitch.Microphone, Toggles.Find(Toggle.Microphone)!.Switch);
    }

    /// <summary>
    /// A file of today's shape with spatial sound on the bar and as a line — written while it opened a
    /// list — loads with both where they were, set as they were: a toggle now, nothing to convert. A
    /// format for the card is kept as Windows writes it; anything else, or any other switch's, is none.
    /// </summary>
    [Fact]
    public void SpatialSoundInAFileLoadsAsAToggle()
    {
        var config = Configs.Repaired($$"""
            { "version": {{Config.Shape}}, "layouts": [ { "id": "default", "name": "Default", "islands": [
              { "id": "s", "kind": "spatialSound", "switch": { "showLabel": true } },
              { "id": "f", "kind": "spatialSound", "switch": { "spatialFormat": "{1459ac38-3875-49bf-bb59-0fe80f4d395d}" } },
              { "id": "a", "kind": "airplaneMode", "switch": { "spatialFormat": "{1459ac38-3875-49bf-bb59-0fe80f4d395d}" } },
              { "id": "l", "kind": "folderList", "folder": { "lists": [ [
                { "island": { "id": "p", "kind": "spatialSound", "switch": { "spatialFormat": "not a format" } } },
                { "island": { "id": "w", "kind": "wifiSwitch" } } ] ] } } ] } ] }
            """);

        var bar = Configs.Islands(config);
        Assert.Equal(["s", "f", "a", "l"], bar.Select(island => island.Id));
        Assert.Equal(BarClick.Action, BarEntries.Of(bar[0].Kind).Click);
        Assert.True(bar[0].Switch!.ShowLabel);
        Assert.Null(bar[0].Switch!.SpatialFormat);
        Assert.Equal("{1459AC38-3875-49BF-BB59-0FE80F4D395D}", bar[1].Switch!.SpatialFormat);
        Assert.Null(bar[2].Switch!.SpatialFormat);
        var lines = bar[3].Folder!.Cells.Select(cell => cell.Island).ToList();
        Assert.Equal(["p", "w"], lines.Select(line => line.Id));
        Assert.Null(lines[0].Switch!.SpatialFormat);
        Assert.Same(WidgetInfos.WifiSwitch, lines[1].Info);
    }
}
