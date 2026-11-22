// The toggles row offers every switch Karpo flips (2026-11-03, the person: "game mode on and off should go
// into the toggle widget, see if you forgot another toggle as well"): every widget whose click flips what it
// stands for (WidgetInfo.Flips) is a toggle of the row too, one switch behind both — game mode, color
// filters, mono audio, the magnifier, the narrator and sticky keys joined. A toggle reads and writes through
// its widget's own switch (SettingToggle, AssistiveSwitch); made up here (a store, a handler and the
// accessibility tools of the test's): nothing reads or changes this PC's settings.

using System.Text.Json;
using Karpo.Dock;
using Karpo.Dock.QuickSettings.Services;
using Karpo.Tests.Islands;
using static Karpo.Tests.Folders.ListFormPictures;

namespace Karpo.Tests.QuickSettings;

public sealed class TogglesRowSwitchesTests
{
    /// <summary>A switch a click flips where it stands can be put in a toggles row, once.</summary>
    [Fact]
    public void EveryWidgetThatFlipsIsAToggleOfTheRow()
    {
        var flipping = Enum.GetValues<QuickSwitch>().Where(which => WidgetInfos.For(which).Flips);
        Assert.All(flipping, which => Assert.Single(Toggles.All, toggle => toggle.Switch == which));
        // No switch is two toggles.
        Assert.Equal(Toggles.All.Length, Toggles.All.Select(toggle => toggle.Switch).Distinct().Count());
    }

    /// <summary>
    /// The new ones, by the word the file keeps: each its widget's switch, glyph and name, read from one
    /// source with the widget, offered in the row's picker and not on a new row.
    /// </summary>
    [Theory]
    [InlineData("gameMode", QuickSwitch.GameMode, "Game mode")]
    [InlineData("colorFilters", QuickSwitch.ColorFilters, "Color filters")]
    [InlineData("monoAudio", QuickSwitch.MonoAudio, "Mono audio")]
    [InlineData("magnifier", QuickSwitch.Magnifier, "Magnifier")]
    [InlineData("narrator", QuickSwitch.Narrator, "Narrator")]
    [InlineData("stickyKeys", QuickSwitch.StickyKeys, "Sticky keys")]
    public void TheNewTogglesAreTheirWidgetsSwitch(string word, QuickSwitch which, string name)
    {
        var json = $$$"""{"id":"a1","kind":"toggles","toggles":{"shown":["wifi","{{{word}}}"]}}""";
        var row = JsonSerializer.Deserialize(json, ConfigJson.Default.CellItem)!;
        row.View = FolderView.List;
        row.Normalize();
        Assert.Equal(2, row.Toggles!.Shown.Count);

        var toggle = Toggles.Find(row.Toggles.Shown[1])!;
        var widget = WidgetInfos.For(which);
        Assert.Equal(which, toggle.Switch);
        Assert.Equal(name, toggle.Name);
        Assert.Equal(widget.Title, toggle.Name);
        Assert.Equal(widget.Glyph, toggle.Glyph);
        Assert.Equal(SwitchData.KeyOf(which), SwitchData.KeyOf(toggle.Toggle));
        Assert.DoesNotContain(toggle.Toggle, Toggles.Default);
        Assert.Contains($"\"{word}\"", JsonSerializer.Serialize(row, ConfigJson.Default.CellItem));
    }

    /// <summary>
    /// A row's game mode is switched as the game mode widget is, through its Settings handler, and says
    /// what the value says; sticky keys and the magnifier through the accessibility tools.
    /// </summary>
    [UiFact]
    public void ARowsTogglesSwitchThroughTheirWidgetsSwitch() =>
        Offscreen.OnStaThread(gfx =>
        {
            var store = new SettingToggleTests.Store();
            var game = new SettingToggle("Game mode", "What it does.", SettingPlace.GameMode, store,
                () => new SettingToggleTests.Handler(store, "AutoGameModeEnabled"));
            var tools = new QuickPagesTests.FakeTools();
            QuickSwitches.Source = which => which switch
            {
                QuickSwitch.GameMode => game,
                QuickSwitch.StickyKeys or QuickSwitch.Magnifier => new AssistiveSwitch(which, tools),
                _ => null,
            };
            FolderIsland? folder = null;
            try
            {
                folder = Folder(gfx, Line(ListKind.Toggles, line => line.Toggles!.Shown =
                    [Toggle.GameMode, Toggle.StickyKeys, Toggle.Magnifier]));
                var tile = (TogglesTile)folder.Tiles[0].Island;
                Assert.Equal([Toggle.GameMode, Toggle.StickyKeys, Toggle.Magnifier], tile.Shown);
                // Game mode is on until somebody turns it off; the row says so once its switch has looked.
                Assert.True(SpinWait.SpinUntil(() => game.Switches, TimeSpan.FromSeconds(10)));
                Assert.True(tile.OnAt(0));
                Assert.False(tile.OnAt(1));

                tile.Click(1);
                Assert.True(SpinWait.SpinUntil(() => store.Written.Count > 0, TimeSpan.FromSeconds(10)));
                Assert.Equal(["AutoGameModeEnabled=0"], store.Written);
                Assert.False(tile.OnAt(0));

                tile.Click(2);
                Assert.Equal([true], tools.Stuck);
                Assert.True(tile.OnAt(1));

                tile.Click(3);
                Assert.Equal([true], tools.Magnified);
                Assert.True(tile.OnAt(2));
            }
            finally
            {
                QuickSwitches.Source = null;
                folder?.Closed();
                folder?.Dispose();
            }
        });
}
