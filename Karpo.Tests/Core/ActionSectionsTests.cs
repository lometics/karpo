namespace Karpo.Tests.Core;

/// <summary>
/// The actions an actions line's slot can open, as its picker lists them (2026-10-11: "the actions
/// in the action widget should be searchable and shown as subsections"): in their sections of
/// Windows' Settings, in Settings' order, and narrowed by the search (SettingsActions.Search).
/// </summary>
public sealed class ActionSectionsTests
{
    /// <summary>With nothing typed, every action once, each in its section, the sections in Settings' order and none empty.</summary>
    [Fact]
    public void EveryActionIsInOneSectionInSettingsOrder()
    {
        var all = SettingsActions.Search(null);
        Assert.Equal(SettingsActions.All.OrderBy(action => SettingsActions.SectionOf(action)).Select(action => action.Id).Order(StringComparer.Ordinal),
            all.SelectMany(group => group.Actions).Select(action => action.Id).Order(StringComparer.Ordinal));
        Assert.Equal(all.Select(group => group.Section).Order(), all.Select(group => group.Section));
        Assert.All(all, group => Assert.NotEmpty(group.Actions));
        Assert.All(all, group => Assert.All(group.Actions, action => Assert.Equal(group.Section, SettingsActions.SectionOf(action))));
        Assert.Equal(Enum.GetValues<ActionSection>(), all.Select(group => group.Section));
        Assert.All(Enum.GetValues<ActionSection>(), section => Assert.False(string.IsNullOrWhiteSpace(section.Title())));
    }

    /// <summary>Where Settings keeps them: Wi-Fi under Network &amp; internet, Add device under Bluetooth &amp; devices, Date &amp; time under Time &amp; language.</summary>
    [Fact]
    public void AnActionIsWhereSettingsKeepsIt()
    {
        Assert.Equal(ActionSection.Network, SettingsActions.SectionOf(SettingsActions.Find("wifi")!));
        Assert.Equal(ActionSection.Devices, SettingsActions.SectionOf(SettingsActions.Find(SettingsActions.AddDeviceId)!));
        Assert.Equal(ActionSection.Time, SettingsActions.SectionOf(SettingsActions.Find("dateTime")!));
        Assert.Equal(ActionSection.Update, SettingsActions.SectionOf(SettingsActions.Find("windowsUpdate")!));
        Assert.Equal("Network & internet", ActionSection.Network.Title());
    }

    /// <summary>
    /// Typing narrows the actions by their names, a section with none found going with them, and a
    /// section's name finds all of it.
    /// </summary>
    [Fact]
    public void TheSearchNarrowsByNameAndBySection()
    {
        var wifi = Assert.Single(SettingsActions.Search("wi-fi"));
        Assert.Equal(ActionSection.Network, wifi.Section);
        Assert.Equal(["wifi"], wifi.Actions.Select(action => action.Id));

        var network = SettingsActions.Search("internet");
        Assert.Contains(network, group => group.Section == ActionSection.Network
            && group.Actions.Select(action => action.Id).SequenceEqual(SettingsActions.All.Where(action => SettingsActions.SectionOf(action) == ActionSection.Network).Select(action => action.Id)));

        Assert.Empty(SettingsActions.Search("nothing is called this"));
    }
}
