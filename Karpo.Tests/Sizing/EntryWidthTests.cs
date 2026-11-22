// A taskbar entry takes the size it needs, and that size follows its settings and templates, never
// the value it shows (docs\widgets.md §12.2, §12.3).
//
// The person, 2026-10-08: "remove the ability to resize widgets on the taskbar", and with it no
// layout shift. A width somebody chose used to be what held a few entries still — a share pinned so
// a price crossing 1,000 did not move the bar, a clock pinned so ten o'clock did not. With no width
// to choose, each entry has to be still on its own. Held here:
//
// - every taskbar kind a widget class draws is sized from its settings alone (WidgetShapes, which
//   IslandCreationTests and StateSizingTests hold per kind), or is one of the two below, held here;
// - the clock is as wide at every hour and minute, with and without seconds, with and without a
//   label, across the bar and down one;
// - a share is as wide whatever its price, and before its first price arrives.
//
// The status icons are the one entry whose length is what it shows — as many icons as are on, as
// Windows' own — and the open apps' and the tray's are how many there are; those are what they
// are, not readings of a value.

using System.Numerics;
using Karpo.Dock;
using Karpo.Dock.Markets;
using Karpo.Tests.Forecasts;
using Karpo.Tests.Islands;
using Color = Windows.UI.Color;

namespace Karpo.Tests.Sizing;

public sealed class EntryWidthTests
{
    private static Look LookFor() => new(
        Glass: false,
        Dark: true,
        Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4),
        AccentLight2: Color.FromArgb(255, 0x99, 0xEB, 0xFF),
        AccentDark1: Color.FromArgb(255, 0x00, 0x5F, 0xB8));

    /// <summary>
    /// Every taskbar kind a widget class draws is sized from its settings and templates
    /// (<see cref="WidgetShapes"/>), but the share, held below, and the status icons, which are as
    /// many as are on. A new kind lands in one or the other on purpose.
    /// </summary>
    [Fact]
    public void EveryEntryIsSizedFromItsSettings()
    {
        foreach (var entry in BarEntries.All.Where(entry => entry.Drawing != WidgetSource.None))
        {
            Assert.True(
                WidgetShapes.Has(entry.Drawing) || entry.Drawing is WidgetSource.Markets or WidgetSource.Indicators,
                $"{entry.Kind} is sized by its island, and nothing holds its width still as its value changes");
        }
    }
}
