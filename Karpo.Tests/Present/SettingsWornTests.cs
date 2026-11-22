// §526: the settings never wear the person's look (the person, 2026-11-07: "the settings should never take the style of the
// user, they should always take the style that we have for it"). Asked of the real dock, Karpo.exe --stage on a desktop of
// its own (PanelPopupPlaceTests.Drive): the panel open by a second start, a dropdown of the Karpo page and one of the Look
// page open, and what the edit bar and the popup are drawn in (stage-edit.json's "DeckWorn" and "MenuWorn": their tokens and
// their font) set against the same with a plain config — the default layout dressed in everything odd changes none of it.

using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using Xunit.Abstractions;

namespace Karpo.Tests.Present;

public sealed class SettingsWornTests(ITestOutputHelper output)
{
    [UiFact]
    public void TheSettingsAndTheirPopupsWearWindowsOwnWhateverTheLayoutWears()
    {
        var plain = Worn(null);
        // The bar's ground light: a dark Windows' bar reads light, so its look's mode is not Windows' either.
        var odd = Worn(config => SettingsLookTests.Dress(config, dark: true));
        output.WriteLine($"plain: {string.Join(" | ", plain)}");
        output.WriteLine($"odd:   {string.Join(" | ", odd)}");
        Assert.Equal(plain, odd);
    }

    /// <summary>What the edit bar, its panel open, and two of its popups are drawn in, the stage's config as <paramref name="dress"/> has it.</summary>
    private List<string> Worn(Action<Config>? dress)
    {
        List<string> worn = [];
        PanelPopupPlaceTests.Drive(output, Edge.Bottom, null, probe =>
        {
            probe.Open();
            worn.Add("deck " + Said(probe, "DeckWorn"));
            Open(probe, probe.Pop("Karpo/"));
            worn.Add("karpo " + Said(probe, "MenuWorn"));
            probe.Escape();
            probe.Nav(PanelPage.Appearance);
            // §592: the Taskbar widgets tab chosen, its Font under the preview and the tabs: brought into sight.
            probe.Tab(LookPart.Widgets);
            var font = "Appearance/" + PanelTabs.Mark + S.SettingsAppearanceStylesWidgets + "/";

            probe.WheelTo(PanelHitKind.Pop, font);
            Open(probe, probe.InSight(PanelHitKind.Pop, font)[0]);
            worn.Add("look " + Said(probe, "MenuWorn"));
            probe.Escape();
            worn.Add("deck after " + Said(probe, "DeckWorn"));
        }, dress);
        return worn;
    }

    /// <summary><paramref name="control"/> pressed, until its popup is in sight.</summary>
    private static void Open(PanelPopupPlaceTests.Probe probe, PanelPopupPlaceTests.Spot control)
    {
        probe.Click(control.Center);
        Assert.True(probe.WaitMenu(open: true), $"{control} opened nothing");
    }

    /// <summary>The words of the stage's entry <paramref name="hit"/>, there once.</summary>
    private static string Said(PanelPopupPlaceTests.Probe probe, string hit)
    {
        var until = Environment.TickCount64 + 5000;
        PanelPopupPlaceTests.Spot? found;
        while ((found = probe.Read("stage-edit.json").FirstOrDefault(spot => spot.Hit == hit)) is null && Environment.TickCount64 < until)
        {
            probe.Pump(100);
        }
        Assert.NotNull(found);
        return found.Key;
    }
}
