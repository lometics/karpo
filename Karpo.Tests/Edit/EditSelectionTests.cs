// edit: the widget clicked while the bar is arranged is the selected one (the person, 2026-10-05:
// "when you left click on a widget in the edit mode it should open its settings even if the
// settings app is closed. it should also show the widget as selected"; Karpo.Dock\EditClicks.cs).
//
// The rules held here, without windows:
//
//  - **By key.** The selection is the clicked widget's key, so it stays on that widget through the
//    rebuilds a click and a save bring, wherever the widget's part moves to (EditClicks.SelectedPart).
//  - **One place.** A widget in a folder is selected in its folder, as its card or line, and no part
//    of the bar is (FolderFlyout.PickedCard); a widget gone from the bar or the folder is selected
//    nowhere.
//
// The rings themselves, as they are drawn: SceneTreeTests.SelectedSheets (the bar) and
// Folders\FolderEditClickTests.TheWidgetClickedWearsTheSelectedRing (a folder). Why a closed
// settings app opened nothing: Settings\SettingsStartTests.

using System.Reflection;
using Karpo.Dock;

namespace Karpo.Tests.Edit;

public sealed class EditSelectionTests
{
    private const string Owner = "layout:default";

    /// <summary>The selected part is the one keyed as the clicked widget, wherever it stands now; none before a click.</summary>
    [Fact]
    public void TheSelectionIsTheClickedWidgetWhereverItStands()
    {
        string[] bar = [$"{Owner}/start", $"{Owner}/clock", $"{Owner}/box"];

        Assert.Null(EditClicks.SelectedPart(null, bar));
        Assert.Equal(1, EditClicks.SelectedPart($"{Owner}/clock", bar));
        // The bar rebuilt with the clock moved to the front: the selection went with it.
        string[] moved = [$"{Owner}/clock", $"{Owner}/start", $"{Owner}/box"];
        Assert.Equal(0, EditClicks.SelectedPart($"{Owner}/clock", moved));
        // A folder clicked on the bar is selected as a block, as any widget is.
        Assert.Equal(2, EditClicks.SelectedPart($"{Owner}/box", bar));
        // Keys are compared as the dock compares them everywhere: without case.
        Assert.Equal(1, EditClicks.SelectedPart($"{Owner}/CLOCK", bar));
    }

    /// <summary>A widget in a folder is selected in its folder, never on the bar; one gone is selected nowhere.</summary>
    [Fact]
    public void AWidgetInAFolderIsSelectedInItsFolderAlone()
    {
        string[] bar = [$"{Owner}/start", $"{Owner}/box"];
        var inFolder = $"{Owner}/box/battery";

        Assert.Null(EditClicks.SelectedPart(inFolder, bar));
        IReadOnlyList<FolderTile> tiles = [Tile($"{Owner}/box/clock"), Tile(inFolder)];
        Assert.Equal(1, FolderFlyout.PickedCard(inFolder, tiles));
        Assert.Equal(-1, FolderFlyout.PickedCard($"{Owner}/start", tiles));
        Assert.Equal(-1, FolderFlyout.PickedCard(null, tiles));
        // Removed from the bar: nothing on it is selected any more.
        Assert.Null(EditClicks.SelectedPart($"{Owner}/gone", bar));
    }

    private static FolderTile Tile(string key)
    {
        var island = DispatchProxy.Create<IIsland, Keyed>();
        ((Keyed)(object)island).Key = key;
        return new FolderTile(island, new FolderCell());
    }

    /// <summary>A widget that answers its key and nothing else.</summary>
    public class Keyed : DispatchProxy
    {
        public string Key { get; set; } = "";

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            targetMethod?.Name == "get_Key" ? Key : null;
    }
}
