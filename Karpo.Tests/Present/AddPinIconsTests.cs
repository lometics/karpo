// §699 (the person, 2026-11-13: "when click add all the pins disappear ... they still show actually, just only with the pin icon
// and not the real icon"): asked of the real dock, Karpo.exe --stage on a desktop of its own (PanelPopupPlaceTests.Drive). The add
// page's ghost stands in a copy of the config (Dock.Seen), which the bars are built from; the app buttons worked out again while
// it stands — a window came, went, was minimized, as the person's do all the time — found no pin of the copy among the config's,
// and every pin fell back to the pin widget's glyph. Held: each pin's button as it was, its app's own icon, with the add page open.

using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using Xunit.Abstractions;
using static Karpo.Tests.Present.PanelPopupPlaceTests;

namespace Karpo.Tests.Present;

public sealed class AddPinIconsTests(ITestOutputHelper output)
{
    private static readonly string[] Targets = [@"C:\Windows\notepad.exe", @"C:\Windows\explorer.exe", @"C:\Windows\regedit.exe"];

    [UiFact]
    public void ThePinsKeepTheirIconsWhileTheAddPageIsOpen()
    {
        List<string> ids = [];
        Drive(output, Edge.Bottom, null, probe =>
    {
        probe.Open();
        probe.Close();
        probe.Pump(1000);
        var pins = Pins(probe, ids);
        Assert.True(Targets.Length == pins.Count, string.Join(" | ", probe.Read("stage-map.json")));
        var before = pins.Select(pin => probe.Pixels(pin.Rect)).ToList();

        probe.Click(probe.Spot(PanelHitKind.Add).Center);
        Assert.Equal(nameof(PanelPage.Add), probe.PageShown());
        // The app buttons worked out again, as a window minimized or restored has them.
        probe.WindowsChanged();

        var after = Pins(probe, ids);
        Assert.Equal(pins.Select(pin => (pin.Key, pin.Rect)), after.Select(pin => (pin.Key, pin.Rect)));
        for (var at = 0; at < pins.Count; at++)
        {
            var now = probe.Pixels(after[at].Rect);
            var differ = Enumerable.Range(0, now.Length / 4).Count(px => Math.Abs(now[px * 4] - before[at][px * 4]) + Math.Abs(now[(px * 4) + 1] - before[at][(px * 4) + 1]) + Math.Abs(now[(px * 4) + 2] - before[at][(px * 4) + 2]) > 24);
            Assert.True(differ < now.Length / 4 / 50, $"{pins[at].Key}: {differ} of {now.Length / 4} px changed with the add page open (its app's icon gone?)");
        }
    }, config =>
    {
        var islands = config.DefaultLayout.Islands;
        var at = islands.FindIndex(island => island.Kind == BarKind.Start) + 1;
        foreach (var target in Targets.Reverse())
        {
            var pin = BarKind.Pin.New();
            pin.Section = BarSection.Start;
            pin.Pin = new PinOptions { Target = target };
            islands.Insert(at, pin);
            ids.Add(pin.Id);
        }
    });
    }

    /// <summary>The bar's pins (stage-map.json), in their order.</summary>
    private static List<Spot> Pins(Probe probe, List<string> ids) =>
        [.. probe.Read("stage-map.json").Where(spot => spot.Hit == "Tile" && ids.Contains(Karpo.Dock.Islands.IdOf(spot.Key))).OrderBy(spot => spot.Rect.left)];
}
