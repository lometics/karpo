// The Displays page's model (Karpo.Core\DisplayLayoutChoice.cs, as SettingsModel.Layouts.cs
// asks it): each display's dropdown offers every named layout, then None, which hides Karpo there -
// but the last display attached that shows Karpo is offered no None, and choosing it there anyway is
// refused (the person, 2026-10-08). The map's bands are the edges a display's layout stands on all
// the time.

using Karpo.Core;

namespace Karpo.Tests.Core;

public sealed class DisplayLayoutChoiceTests
{
    /// <summary>A display's dropdown as the model makes it: the file's layouts, and None where the file lets it be hidden.</summary>
    private static List<string> ChoicesOf(Config config, string id, string[] attached) =>
        DisplayLayoutChoice.Choices(config.Layouts.Select(layout => layout.Name), config.ScreenFor(id).Enabled, config.CanHide(id, attached));

    /// <summary>The entry a display's dropdown shows, as the model reads it.</summary>
    private static int IndexOf(Config config, string id) =>
        DisplayLayoutChoice.IndexOf(config.Layouts.IndexOf(config.ActiveLayout(id)), config.Layouts.Count, config.ScreenFor(id).Enabled);

    private static Config TwoDisplays()
    {
        var config = new Config();
        config.Repair();
        config.AddLayout("Work", []);
        config.AddDisplay("D1", "Dell");
        config.AddDisplay("D2", "LG");
        return config;
    }

    /// <summary>Every layout by name in the file's order, then None.</summary>
    [Fact]
    public void TheLayoutsComeFirstThenNone()
    {
        Assert.Equal(["Default", "Work", "None"], DisplayLayoutChoice.Choices(["Default", "Work"], shown: true, canHide: true));
    }

    /// <summary>The last display that shows Karpo is offered no None; a display showing None already keeps it, so the dropdown can show it.</summary>
    [Fact]
    public void TheLastDisplayShowingKarpoIsOfferedNoNone()
    {
        Assert.Equal(["Default", "Work"], DisplayLayoutChoice.Choices(["Default", "Work"], shown: true, canHide: false));
        Assert.Equal(["Default", "Work", "None"], DisplayLayoutChoice.Choices(["Default", "Work"], shown: false, canHide: false));
    }

    /// <summary>The dropdown shows the layout the display shows, or None after the layouts; a layout it can't find reads as the first.</summary>
    [Fact]
    public void TheDropdownShowsTheLayoutOrNone()
    {
        Assert.Equal(1, DisplayLayoutChoice.IndexOf(1, 2, shown: true));
        Assert.Equal(2, DisplayLayoutChoice.IndexOf(1, 2, shown: false));
        Assert.Equal(0, DisplayLayoutChoice.IndexOf(-1, 2, shown: true));
    }

    /// <summary>A layout is shown; None hides Karpo where another display still shows it, and is refused on the last one.</summary>
    [Fact]
    public void NoneIsRefusedOnTheLastDisplayShowingKarpo()
    {
        Assert.Equal(DisplayPick.Show, DisplayLayoutChoice.Choose(1, now: 0, count: 2, canHide: true));
        Assert.Equal(DisplayPick.Show, DisplayLayoutChoice.Choose(0, now: 2, count: 2, canHide: false));
        Assert.Equal(DisplayPick.Hide, DisplayLayoutChoice.Choose(2, now: 0, count: 2, canHide: true));
        Assert.Equal(DisplayPick.Refused, DisplayLayoutChoice.Choose(2, now: 0, count: 2, canHide: false));
        // The entry it shows, or none at all (a dropdown whose list was just replaced), does nothing.
        Assert.Equal(DisplayPick.Nothing, DisplayLayoutChoice.Choose(0, now: 0, count: 2, canHide: true));
        Assert.Equal(DisplayPick.Nothing, DisplayLayoutChoice.Choose(-1, now: 0, count: 2, canHide: true));
    }

    /// <summary>
    /// Two displays, as the page lists them: both offered None while both show Karpo; once one shows
    /// None the other is the last and loses its None, and the one hidden shows None. Showing a layout
    /// on it again gives the other its None back.
    /// </summary>
    [Fact]
    public void AtLeastOneDisplayKeepsKarpoAcrossThePage()
    {
        var config = TwoDisplays();
        string[] attached = ["D1", "D2"];

        Assert.Equal(["Default", "Work", "None"], ChoicesOf(config, "D1", attached));
        Assert.Equal(["Default", "Work", "None"], ChoicesOf(config, "D2", attached));
        Assert.Equal(0, IndexOf(config, "D2"));

        // D2 chooses None.
        Assert.Equal(DisplayPick.Hide, DisplayLayoutChoice.Choose(2, IndexOf(config, "D2"), config.Layouts.Count, config.CanHide("D2", attached)));
        config.EditScreen("D2", "LG").Enabled = false;

        Assert.Equal(["Default", "Work"], ChoicesOf(config, "D1", attached));
        Assert.Equal(["Default", "Work", "None"], ChoicesOf(config, "D2", attached));
        Assert.Equal(2, IndexOf(config, "D2"));
        // D1 is the last: None is refused there.
        Assert.Equal(DisplayPick.Refused, DisplayLayoutChoice.Choose(2, IndexOf(config, "D1"), config.Layouts.Count, config.CanHide("D1", attached)));

        // D2 shows Work again.
        Assert.Equal(DisplayPick.Show, DisplayLayoutChoice.Choose(1, IndexOf(config, "D2"), config.Layouts.Count, config.CanHide("D2", attached)));
        Assert.True(config.SelectLayout("D2", config.Layouts[1].Id));
        Assert.Equal(1, IndexOf(config, "D2"));
        Assert.Equal(["Default", "Work", "None"], ChoicesOf(config, "D1", attached));
    }

    /// <summary>One display attached: it is the last, offered no None, whatever else the file remembers.</summary>
    [Fact]
    public void ALoneDisplayIsOfferedNoNone()
    {
        var config = TwoDisplays();

        Assert.Equal(["Default", "Work"], ChoicesOf(config, "D1", ["D1"]));
    }

    /// <summary>
    /// The map's bands: the edge the bar of the display's layout stands on all the time, and none where
    /// its bar comes in on hover.
    /// </summary>
    [Fact]
    public void TheMapsBandsAreTheEdgesALayoutStandsOn()
    {
        var config = TwoDisplays();
        var work = config.Layouts[1];

        Assert.True(config.SelectLayout("D2", work.Id));
        Assert.Equal([Edge.Bottom], config.EdgesOf("D2").Standing);
        // The edge is the display's own: a Taskbar stands where the display says.
        config.EditScreen("D2", "").Edge = Edge.Top;
        Assert.Equal([Edge.Top], config.EdgesOf("D2").Standing);
        Assert.True(work.Become(LayoutType.Dock));
        Assert.Equal([Edge.Top], config.EdgesOf("D2").Standing);
        work.Show = EdgeMode.Hover;
        Assert.Empty(config.EdgesOf("D2").Standing);
    }
}
