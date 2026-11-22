// §612: edit mode's dress as one state (EditDress): None out of edit mode, Edit in it, Preview while the Appearance page's
// flyout is open — the bars, the folders and the desktop drawn as out of edit mode, edit mode on all the same. Held here: the
// state itself, and the real dock (Karpo.exe --stage on a desktop of its own, PanelPopupPlaceTests.Drive) wearing it — the
// Appearance opened: the bars laid out exactly as before edit mode, no "+", the dim clear, the edit bar and its flyout still
// there; another page, or the flyout closed: the dress back. The transitions themselves are held where they are drawn:
// RetainedBarTests (the bar's marks and "+"), EditChromeFilmTests (the dim, an arranged folder plain in the preview).

using Karpo.Dock;
using Karpo.Tests.Islands;
using Karpo.Tests.Present;
using Xunit.Abstractions;
using Edge = Karpo.Core.Edge;
using Spot = Karpo.Tests.Present.PanelPopupPlaceTests.Spot;

namespace Karpo.Tests.Edit;

public sealed class EditDressTests(ITestOutputHelper output)
{
    /// <summary>Out of edit mode none; in it, edit mode's dress whatever is open but the Appearance, whose preview is drawn as out of it.</summary>
    [Fact]
    public void TheDressIsEditModesButWhileTheAppearanceIsOpen()
    {
        Assert.Equal(EditDress.None, EditDresses.Of(editing: false, null));
        Assert.Equal(EditDress.None, EditDresses.Of(editing: false, PanelPage.Appearance));
        Assert.Equal(EditDress.Edit, EditDresses.Of(editing: true, null));
        foreach (var page in Enum.GetValues<PanelPage>().Where(page => page != PanelPage.Appearance))
        {
            Assert.Equal(EditDress.Edit, EditDresses.Of(editing: true, page));
        }
        Assert.Equal(EditDress.Preview, EditDresses.Of(editing: true, PanelPage.Appearance));
        Assert.True(EditDress.Edit.Worn());
        Assert.False(EditDress.Preview.Worn());
        Assert.False(EditDress.None.Worn());
    }

    /// <summary>
    /// The real dock: edit mode in its dress (the bars laid out arranged, their "+", the dim); the Appearance's flyout opened:
    /// the bars laid out as they were before edit mode — every widget where it stood —, no "+", the dim clear, the edit bar and
    /// the flyout still there; the Taskbar's opened: the dress back; the Appearance again and its flyout closed: the dress back.
    /// </summary>
    [UiFact]
    public void TheAppearanceShowsTheBarsAsOutOfEditModeAndAnotherPageDressesThemAgain() => PanelPopupPlaceTests.Drive(output, Edge.Bottom, null, probe =>
    {
        var rest = Bars(probe);
        Assert.NotEmpty(rest);
        probe.Open();
        Dressed(probe, EditDress.Edit, "edit mode begun");
        probe.Nav(PanelPage.Appearance);
        Dressed(probe, EditDress.Preview, "the Appearance opened");
        Assert.Equal(nameof(PanelPage.Appearance), probe.PageShown());
        Assert.Contains(probe.Read("stage-edit.json"), spot => spot.Hit == "EditBar");
        Assert.Equal(Said(rest), Said(Bars(probe)));
        probe.Nav(PanelPage.Taskbar);
        Dressed(probe, EditDress.Edit, "the Taskbar opened");
        probe.Nav(PanelPage.Appearance);
        Dressed(probe, EditDress.Preview, "the Appearance opened again");
        probe.Close();
        Dressed(probe, EditDress.Edit, "the Appearance closed");
    });

    /// <summary>The bars' widgets where they stand (stage-map.json), as words.</summary>
    private static List<Spot> Bars(PanelPopupPlaceTests.Probe probe) => probe.Read("stage-map.json", wait: true);

    private static string Said(List<Spot> spots) => string.Join("\n", spots.Select(spot => spot.ToString()).Order(StringComparer.Ordinal));

    /// <summary>The dock wears <paramref name="dress"/> (stage-edit.json): every bar laid out so, its "+" and the dim with it.</summary>
    private static void Dressed(PanelPopupPlaceTests.Probe probe, EditDress dress, string when)
    {
        var spots = probe.Read("stage-edit.json");
        var worn = dress.Worn();
        Assert.True(spots.Single(spot => spot.Hit == "Dress").Index == (int)dress, $"{when}: the dock wears {spots.Single(spot => spot.Hit == "Dress").Key}, not {dress}");
        Assert.All(spots.Where(spot => spot.Hit == "Dressed"), spot => Assert.True(spot.Index == (worn ? 1 : 0), $"{when}: the bar on {spot.Key} laid out {(worn ? "at rest" : "arranged")}"));
        Assert.All(spots.Where(spot => spot.Hit == "Dim"), spot => Assert.True(spot.Index == (worn ? 1 : 0), $"{when}: the dim on {spot.Key} {(worn ? "clear" : "dimmed")}"));
        Assert.True(spots.Any(spot => spot.Hit == "BarAdd") == worn, $"{when}: the bars' \"+\" {(worn ? "gone" : "shown")}");
    }
}
