// §554: the public holidays set on the real dock's settings panel, Karpo.exe --stage on a desktop of its own, driven as the
// pointer and the keys drive it (PanelPopupPlaceTests.Probe): Add a country's popup, a country found by its name and
// picked with the keys, the popup closing on it, as a ComboBox's list closes (§520 to §553 it stayed, the next click one
// more country); the new calendar's region its dropdown, a state typed to (type-to-jump) and picked, the popup closing;
// the config the dock writes holding each. A stage run's config is its own (isolated): no holidays are fetched, nothing
// reaches the network.

using System.Text.Json.Nodes;
using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using Xunit.Abstractions;
using static Karpo.Tests.Present.PanelPopupPlaceTests;

namespace Karpo.Tests.Present;

public sealed class HolidaysPanelUiTests(ITestOutputHelper output)
{
    private static readonly string Holidays = "Karpo/" + S.SettingsSectionsServices + "/" + S.CoreHolidaysName;

    /// <summary>The calendars the dock's config file holds now, a country and a region each.</summary>
    private static List<(string, string)> Calendars(Probe probe) =>
        JsonNode.Parse(probe.ConfigText())?["holidays"]?["calendars"] is JsonArray calendars
            ? [.. calendars.Select(each => ((string?)each?["country"] ?? "", (string?)each?["region"] ?? ""))]
            : [];

    /// <summary>
    /// <paramref name="words"/> typed into the popup open, then Enter: a search's field its first find picked (Down onto it
    /// first, <paramref name="down"/>), a ComboBox's list the row they start (§521: type-to-jump).
    /// </summary>
    private static void Pick(Probe probe, string words, bool down)
    {
        var popup = Assert.Single(probe.Menus());
        foreach (var c in words)
        {
            _ = TerraFX.Interop.Windows.Windows.PostMessageW(popup, WM.WM_CHAR, c, 0);
        }
        probe.Pump(1500);
        if (down)
        {
            probe.Key(popup, VK.VK_DOWN);
            probe.Pump(300);
        }
        probe.Key(popup, VK.VK_RETURN);
    }

    [UiFact]
    public void ACountryAddedAndItsRegionPicked() => Drive(output, Edge.Bottom, null, probe =>
    {
        probe.Open();
        probe.WheelTo(PanelHitKind.Head, Holidays);
        probe.Click(probe.Spot(PanelHitKind.Head, Holidays).Center);

        // Add a country: "Germ", Down, Enter: Germany, the popup gone.
        probe.Click(probe.Pop(Holidays + "/" + S.CoreHolidaysAdd).Center);
        Assert.True(probe.WaitMenu(open: true), "Add a country opened nothing");
        Pick(probe, "Germ", down: true);
        if (!probe.WaitMenu(open: false))
        {
            probe.Wrong("Add a country's popup stayed open after a country was picked");
            probe.Escape();
        }
        Assert.Equal([("DE", "")], Calendars(probe));

        // Its region its dropdown: "Bay" jumps to Bayern, Enter: Bayern, the popup gone.
        var region = probe.Pop(Holidays + "/" + HolidayPlaces.Name("DE"));
        probe.Click(region.Center);
        Assert.True(probe.WaitMenu(open: true), "Germany's region opened nothing");
        Pick(probe, "Bay", down: false);
        if (!probe.WaitMenu(open: false))
        {
            probe.Wrong("the region's popup stayed open after a region was picked");
            probe.Escape();
        }
        Assert.Equal([("DE", "DE-BY")], Calendars(probe));
        probe.Finish();
    }, dress: config => config.Holidays = null);
}
