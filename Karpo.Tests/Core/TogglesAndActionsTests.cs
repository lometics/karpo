// The toggles row's and the actions line's options (Karpo.Core\Toggles.cs, SettingsActions.cs):
// what a new one holds, what the file keeps and what it drops, and the curated list of pages.

using System.Text.Json;

namespace Karpo.Tests.Core;

public sealed class TogglesAndActionsTests
{
    private static CellItem RoundTrip(CellItem item)
    {
        var back = JsonSerializer.Deserialize(JsonSerializer.Serialize(item, ConfigJson.Default.CellItem), ConfigJson.Default.CellItem)!;
        back.View = FolderView.List;
        back.Normalize();
        return back;
    }

    [Fact]
    public void ANewTogglesRowHoldsTheDefaults()
    {
        var row = ListKind.Toggles.New();

        Assert.Equal(Toggles.Default, row.Toggles!.Shown);
        Assert.Null(row.Actions);
    }

    /// <summary>The toggles and their order are the person's, and the file keeps them as they were picked.</summary>
    [Fact]
    public void TheTogglesAndTheirOrderRoundTrip()
    {
        var row = ListKind.Toggles.New();
        row.Toggles!.Shown = [Toggle.NightLight, Toggle.Wifi, Toggle.DoNotDisturb];

        var json = JsonSerializer.Serialize(row, ConfigJson.Default.CellItem);
        Assert.Contains("\"shown\":[\"nightLight\",\"wifi\",\"doNotDisturb\"]", System.Text.RegularExpressions.Regex.Replace(json, @"\s", ""), StringComparison.OrdinalIgnoreCase);
        Assert.Equal([Toggle.NightLight, Toggle.Wifi, Toggle.DoNotDisturb], RoundTrip(row).Toggles!.Shown);
    }

    /// <summary>An emptied row stays empty: none is a choice, not a file that lost its list.</summary>
    [Fact]
    public void AnEmptyRowStaysEmpty()
    {
        var row = ListKind.Toggles.New();
        row.Toggles!.Shown = [];

        Assert.Empty(RoundTrip(row).Toggles!.Shown);
    }

    /// <summary>A word this build does not know, and a toggle twice, go; the rest keep their order.</summary>
    [Fact]
    public void UnknownAndRepeatedTogglesAreDropped()
    {
        const string json = """{"id":"a1","kind":"toggles","toggles":{"shown":["location","bluetooth","wifi","bluetooth"]}}""";
        var row = JsonSerializer.Deserialize(json, ConfigJson.Default.CellItem)!;
        row.View = FolderView.List;
        row.Normalize();

        Assert.Equal([Toggle.Bluetooth, Toggle.Wifi], row.Toggles!.Shown);
    }

    /// <summary>
    /// Every toggle is in the registry once, in the enum's order, with a glyph and a name; and every
    /// one that is a one-button widget's switch is that widget's, its glyph included.
    /// </summary>
    [Fact]
    public void EveryToggleIsDescribed()
    {
        Assert.Equal(Enum.GetValues<Toggle>(), Toggles.All.Select(info => info.Toggle));
        Assert.All(Toggles.All, info =>
        {
            Assert.Single(info.Glyph);
            Assert.False(string.IsNullOrWhiteSpace(info.Name));
            Assert.NotEqual(info.NameKey, info.Name);
            var which = info.Switch!.Value;
            Assert.Equal(WidgetInfos.For(which).Glyph, info.Glyph);
            // A toggle is called what its widget is: the "on/off" is the add list's alone.
            Assert.Equal(WidgetInfos.For(which).Title, info.Name);
        });
        // Every toggle is a one-button widget's switch since the radios and the sound became widgets,
        // and goes by the thing's name.
        Assert.All(Toggles.All, info => Assert.NotNull(info.Switch));
        Assert.Equal(["Wi-Fi", "Bluetooth", "Sound", "Microphone"],
            new[] { Toggle.Wifi, Toggle.Bluetooth, Toggle.Sound, Toggle.Microphone }.Select(toggle => Toggles.Find(toggle)!.Name));
    }

    // --- Actions -----------------------------------------------------------------------------------

    [Fact]
    public void ANewActionsLineOpensDateAndTime()
    {
        var line = ListKind.Actions.New();

        Assert.Equal("dateTime", line.Actions!.Left);
        Assert.Null(line.Actions.Right);
    }

    /// <summary>Both slots are kept as chosen, an emptied one empty — not back at its default.</summary>
    [Fact]
    public void TheActionsRoundTripAndAnEmptySlotStaysEmpty()
    {
        var line = ListKind.Actions.New();
        line.Actions!.Left = null;
        line.Actions.Right = "wifi";

        var back = RoundTrip(line);

        Assert.Null(back.Actions!.Left);
        Assert.Equal("wifi", back.Actions.Right);
    }

    [Fact]
    public void AnActionThisBuildDoesNotKnowIsEmptied()
    {
        const string json = """{"id":"a2","kind":"actions","actions":{"left":"teleporter","right":"bluetooth"}}""";
        var line = JsonSerializer.Deserialize(json, ConfigJson.Default.CellItem)!;
        line.View = FolderView.List;
        line.Normalize();

        Assert.Null(line.Actions!.Left);
        Assert.Equal("bluetooth", line.Actions.Right);
    }

    /// <summary>
    /// The curated list: each page once, an ms-settings: address of letters, digits and dashes, one
    /// Segoe Fluent Icons glyph from its private-use area, and a name in every language Karpo speaks.
    /// </summary>
    [Fact]
    public void EveryCuratedPageIsAValidSettingsPage()
    {
        Assert.Equal(SettingsActions.All.Length, SettingsActions.All.Select(action => action.Id).Distinct().Count());
        // One page each, but for Add device, Settings' button at the top of the Bluetooth page, and
        // Task view and quick settings' Edit, which have no address.
        Assert.Equal(SettingsActions.All.Length - 2, SettingsActions.All.Select(action => action.Uri).Distinct().Count());
        Assert.Equal(SettingsActions.Find("bluetooth")!.Uri, SettingsActions.Find(SettingsActions.AddDeviceId)!.Uri);
        foreach (var action in SettingsActions.All)
        {
            // Two are no page of Settings: the calendar app, by the calendar's own link, and Task
            // Manager, by its program, which the shell finds on the path.
            if (action.Id == SettingsActions.TaskManagerId)
            {
                Assert.Equal("taskmgr.exe", action.Uri);
            }
            // desktops: Task view is Win+Tab, pressed as the taskbar's button presses it, and has no address.
            else if (action.Id is SettingsActions.TaskViewId or SettingsActions.EditId)
            {
                Assert.Equal("", action.Uri);
            }
            // Quick settings' gear: Settings' own home.
            else if (action.Id == SettingsActions.AllSettingsId)
            {
                Assert.Equal("ms-settings:", action.Uri);
            }
            else
            {
                var (pattern, scheme) = action.Id switch
                {
                    SettingsActions.CalendarId => ("^outlookcal:$", "outlookcal"),
                    _ => ("^ms-settings:[a-z0-9-]+$", "ms-settings"),
                };
                Assert.Matches(pattern, action.Uri);
                Assert.True(Uri.TryCreate(action.Uri, UriKind.Absolute, out var parsed) && parsed.Scheme == scheme, action.Uri);
            }
            Assert.Single(action.Glyph);
            Assert.InRange(action.Glyph[0], '', '');
            Assert.Same(action, SettingsActions.Find(action.Id));
        }
        // The pages the flyouts end with are there.
        Assert.All(["dateTime", "wifi", "bluetooth", "sound", "display", "notifications", "taskbar", "power", "network", "vpn", "focus", "clipboard"],
            id => Assert.NotNull(SettingsActions.Find(id)));
    }

    [Fact]
    public void EveryCuratedPageIsNamedInEveryLanguage()
    {
        foreach (var language in Localization.StringFiles.Codes)
        {
            var words = Localization.StringFiles.Read(language).ToDictionary(pair => pair.Key, pair => pair.Value);
            foreach (var action in SettingsActions.All)
            {
                Assert.True(words.TryGetValue($"core.action.{action.Id}", out var name) && name.Length > 0, $"{language}.json has no name for {action.Id}");
            }
        }
    }
}
