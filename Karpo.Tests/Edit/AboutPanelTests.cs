// edit, §638 (the person, 2026-11-09: "the about should show the name Lometics GmbH as title over the description from where
// we are. remove the exact location gais."; docs\widgets.md §638): About Karpo's Lometics line as the settings panel lays it.

using Karpo.Dock;

namespace Karpo.Tests.Edit;

public sealed class AboutPanelTests
{
    private const float Scale = 1.5f;

    /// <summary>
    /// Lometics GmbH a line as Karpo's is: its name over its one line ("The company behind Karpo, based in Switzerland"), its
    /// site's button — "lometics.com", its words' width — at the line's end where every control ends, in its line (its pair:
    /// no line of its own); the name and the line whole before it; a screen reader hears the button by its words.
    /// </summary>
    [Fact]
    public void LometicsIsItsNameOverItsLineItsSiteAButtonBesideIt()
    {
        var layout = new PanelLayout(new SettingsPanelTests.Words(), Scale,
            new PanelInput(0, SettingsPanelTests.Content(), PanelPage.Karpo, new HashSet<string>()), PanelLayout.SizeOf(Scale, 20000));
        var lines = layout.Blocks.SelectMany(block => block.Lines).ToList();
        var karpo = lines.Single(line => line.Row.Text == "Karpo");
        var line = lines.Single(each => each.Row.Text == "Lometics GmbH");
        Assert.DoesNotContain(lines, each => each.Row.Text == KarpoMenu.MakerSiteName);
        Assert.Equal((PanelRowKind.Note, S.SettingsAboutMakerDescription), (line.Row.Kind, line.Row.Second));
        Assert.Equal((karpo.Row.Kind, karpo.Rect.Height), (line.Row.Kind, line.Rect.Height));
        var site = line.Row.Pair!;
        Assert.Equal((PanelRowKind.Action, KarpoMenu.MakerSiteName), (site.Kind, site.Text));
        var (hit, rect) = Assert.Single(layout.Controls(line));
        Assert.Equal((PanelHitKind.Act, site.Key), (hit.Kind, hit.Key));
        Assert.Equal(layout.ControlEnd(line), rect.Right, 3);
        Assert.Equal(layout.ActionWide(site), rect.Width, 3);
        Assert.Equal(layout.P(PanelMetrics.Control), rect.Height);
        Assert.True(layout.NameX(line) + layout.NameRoom(line) + layout.P(PanelMetrics.Gutter) <= rect.X + 0.5f);
        var said = DeckUia.Things(layout, bar: false).Single(thing => thing.Hit == hit);
        Assert.Equal((KarpoMenu.MakerSiteName, UiaRole.Button, true), (said.Name, said.Role, said.Enabled));
        var middle = layout.OnPanel(rect).Center;
        Assert.Equal(hit, layout.HitIn(new System.Numerics.Vector2(middle.X, middle.Y)));
    }
}
