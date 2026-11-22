// edit: the selected widget's flyout (the person, 2026-10-09: "when clicking on a widget it should
// actually also open its flyout when it is selected as individual widget"; of the clock: "the flyout
// should basically be kept open as long as the widget is selected"). The rule (SelectedFlyout) and
// which kinds it opens for, held to the dock's own table (Dock.HasFlyout) and the registry's
// (BarEntries). Pure: no window. The flyout standing through the settings app having the focus and
// clear of the edit bar is FlyoutOffscreenTests.Selected.cs.

using Karpo.Core;
using Karpo.Dock;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Edit;

public sealed class SelectedFlyoutTests
{
    private static string Key(string id) => Config.IslandKey("display:home", id);

    // The clock and the weather have a flyout; the separator has none.
    private static bool Opens(string key) => key == Key("clock") || key == Key("weather");

    [Fact]
    public void OneSelectedWithAFlyoutHasItOpen()
    {
        Assert.Equal(Key("clock"), SelectedFlyout.For(editing: true, carrying: false, [Key("clock")], Opens));
    }

    /// <summary>Another selected alone: that one's flyout instead, or none where it has none.</summary>
    [Fact]
    public void AnotherSelectedHasItsOwnOrNone()
    {
        var selection = new EditSelection();
        selection.Only(Key("clock"));
        Assert.Equal(Key("clock"), SelectedFlyout.For(true, false, selection.Keys, Opens));
        selection.Only(Key("weather"));
        Assert.Equal(Key("weather"), SelectedFlyout.For(true, false, selection.Keys, Opens));
        selection.Only(Key("separator"));
        Assert.Null(SelectedFlyout.For(true, false, selection.Keys, Opens));
    }

    /// <summary>Several selected, none, one carried, or edit mode over: no flyout.</summary>
    [Fact]
    public void SeveralNoneCarriedOrOutOfEditModeHaveNone()
    {
        var selection = new EditSelection();
        selection.Only(Key("clock"));
        selection.Toggle(Key("weather"));
        Assert.Null(SelectedFlyout.For(true, false, selection.Keys, Opens));
        selection.Toggle(Key("weather"));
        Assert.Equal(Key("clock"), SelectedFlyout.For(true, false, selection.Keys, Opens));

        selection.Clear();
        Assert.Null(SelectedFlyout.For(true, false, selection.Keys, Opens));

        Assert.Null(SelectedFlyout.For(editing: true, carrying: true, [Key("clock")], Opens));
        Assert.Null(SelectedFlyout.For(editing: false, carrying: false, [Key("clock")], Opens));
    }

    /// <summary>
    /// A kind with a flyout of Karpo's own opens it for its selection. None on the bar has one today:
    /// the last, the web search box's, went with it (2026-10-11), and every other flyout is a folder's
    /// or the tray's, which never open. The rule stands for the next one.
    /// </summary>
    [Fact]
    public void AWidgetWithAFlyoutOfItsOwnOpensItAndNoneHasOneToday()
    {
        Assert.True(SelectedFlyout.Opens(BarKind.Volume, own: true));
        Assert.False(SelectedFlyout.Opens(BarKind.Volume, own: false));
        foreach (var entry in BarEntries.All)
        {
            Assert.False(SelectedFlyout.Opens(entry.Kind, TheDock.HasFlyout(entry.Kind)), $"{entry.Kind} opens for its selection");
        }
    }

    /// <summary>
    /// Never Windows' own panels (Start, Search, task view...) nor an act: they have no flyout of
    /// Karpo's, which is what the dock's table says, and a selection must neither open Windows' Start
    /// nor launch an app.
    /// </summary>
    [Fact]
    public void NoPanelOfWindowsNorAnActOpens()
    {
        foreach (var entry in BarEntries.All.Where(entry => entry.Click is BarClick.Panel or BarClick.Action or BarClick.Nothing))
        {
            Assert.False(SelectedFlyout.Opens(entry.Kind, TheDock.HasFlyout(entry.Kind)), $"{entry.Kind} ({entry.Click}) opens for its selection");
        }
    }

    /// <summary>
    /// Nor the flyouts wrong to open for a selection, whatever the dock has for them: the notification
    /// list marks everything seen as it opens; a folder's click in edit mode opens the folder itself; the
    /// tray's click is one of its icons'. Each is a kind whose click does open one of Karpo's flyouts, so
    /// the list names nothing that could not open anyway.
    /// </summary>
    [Fact]
    public void TheKindsWrongToOpenNeverDo()
    {
        Assert.Equal(new[] { BarKind.FolderList, BarKind.Tray }.Order(), SelectedFlyout.Never.Order());
        foreach (var kind in SelectedFlyout.Never)
        {
            Assert.Equal(BarClick.Flyout, BarEntries.Of(kind).Click);
            Assert.False(SelectedFlyout.Opens(kind, own: true), $"{kind} opens for its selection");
        }
    }
}
