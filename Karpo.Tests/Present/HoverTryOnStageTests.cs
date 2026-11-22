// §622 (the person, 2026-11-09: "when hovering over an entry in the palette it should apply it / preview it with the whole
// taskbar and also in the preview itself. the hovering should also work in other selectors like the font selector or color
// selectors"): asked of the real dock, Karpo.exe --stage on a desktop of its own (PanelPopupPlaceTests.Drive). Its stage map
// says what the bars wear ("Worn": the Appearance of the config worn, in words, 1 while a copy is worn over the person's).
//
// §670, §684: the Appearance's choices are inline pickers on the page, tried on under the pointer (EditDeck.Rests: a picker's
// cell the pointer rests on, EditDeckAction.Hover). The pointer is posted to the panel: on the stage's desktop there is no real
// pointer to leave it, so none is said to (FlyoutHost.Track), and leaving is posted too. Held: a choice rested on is worn and
// nothing is written; §697 another after it worn in its turn, the page as it stood; the pointer gone, the person's look is back;
// a click is one change, one step of the undo; the same for a font and a colour's swatch. Pictures (KARPO_PNG_DIR):
// hover-palette (Bloom under the pointer, the bar and the page's preview wearing it), hover-font (a font under it).

using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using Xunit.Abstractions;
using static Karpo.Tests.Present.PanelPopupPlaceTests;

namespace Karpo.Tests.Present;

public sealed class HoverTryOnStageTests(ITestOutputHelper output)
{
    private static readonly string WidgetsTab = "Appearance/" + PanelTabs.Mark + S.SettingsAppearanceStylesWidgets + "/";

    private static readonly string Font = WidgetsTab + S.SettingsAppearanceStylesFont;

    private static readonly string Background = WidgetsTab + S.SettingsAppearanceCustomBackgroundHeader;

    private const string Palette = "Appearance/Palette";

    /// <summary>
    /// Palette open: the palette the pointer rests on is worn — the bars and the page as it would be —, the file untouched;
    /// §697 another after it worn in its turn, the first again as it was, the palettes where they stood; the pointer gone, the
    /// person's look again; a palette rested on and clicked: worn and written once, and one undo puts the person's look back.
    /// </summary>
    [UiFact]
    public void APaletteRestedOnIsWornTillItIsPicked() => Drive(output, Edge.Bottom, null, probe =>
    {
        probe.Open();
        probe.Nav(PanelPage.Appearance);
        var file = probe.ConfigText();
        var persons = Worn(probe).Appearance;
        OpenHead(probe, Palette);

        var cells = Cells(probe, Palette);
        var (first, tried) = RestTillWorn(probe, Palette);
        Assert.NotEqual(persons, tried);
        Assert.Equal(file, probe.ConfigText());

        // §697: one after another, each worn in its turn over the person's, the page standing as it stood.
        var other = cells.First(cell => cell.Key != first.Key && cell.Key != cells[0].Key);
        probe.Move(other.Center);
        var next = Worn(probe);
        Assert.Equal(1, next.Copy);
        Assert.NotEqual(tried, next.Appearance);
        Assert.Equal(cells.Select(cell => (cell.Key, Text(cell.Rect))), Cells(probe, Palette).Select(cell => (cell.Key, Text(cell.Rect))));
        probe.Move(first.Center);
        Assert.Equal((1, tried), Worn(probe));
        Assert.Equal(file, probe.ConfigText());

        // The pointer gone: the person's look again, nothing written.
        probe.Leave(first.Center);
        Assert.Equal((0, persons), Worn(probe));
        Assert.Equal(file, probe.ConfigText());

        // Rested on and clicked: worn and written, once; one undo the person's look again.
        probe.Move(first.Center);
        Assert.Equal((1, tried), Worn(probe));
        probe.Click(first.Center);
        probe.Leave(first.Center);
        Assert.Equal((0, tried), Worn(probe));
        Assert.NotEqual(file, probe.ConfigText());
        probe.Undo();
        Assert.Equal((0, persons), Worn(probe));
    });

    /// <summary>
    /// Taskbar widgets' Font: a font the pointer rests on worn, nothing written, the pointer gone the person's look back. Its
    /// Background's picker: a swatch rested on worn, nothing written; clicked, written once, one undo back.
    /// </summary>
    [UiFact]
    public void AFontAndAColoursSwatchRestedOnAreWornToo() => Drive(output, Edge.Bottom, null, probe =>
    {
        probe.Open();
        probe.Nav(PanelPage.Appearance);
        probe.Tab(LookPart.Widgets);
        var file = probe.ConfigText();
        var persons = Worn(probe).Appearance;

        OpenHead(probe, Font);
        var (font, _) = RestTillWorn(probe, Font);
        Assert.Equal(file, probe.ConfigText());
        probe.Leave(font.Center);
        Assert.Equal((0, persons), Worn(probe));
        Assert.Equal(file, probe.ConfigText());
        CloseHead(probe, Font);

        OpenHead(probe, Background);
        var (swatch, tried) = RestTillWorn(probe, Background);
        Assert.NotEqual(persons, tried);
        Assert.Equal(file, probe.ConfigText());
        probe.Click(swatch.Center);
        probe.Leave(swatch.Center);
        Assert.Equal((0, tried), Worn(probe));
        Assert.NotEqual(file, probe.ConfigText());
        probe.Undo();
        Assert.Equal((0, persons), Worn(probe));
    });

    /// <summary>
    /// The pictures: Palette open, Bloom under the pointer, the bar and the page's preview wearing it (hover-palette); Taskbar
    /// widgets' Font open, a font under the pointer, the bar's words in it (hover-font). The display whole, dark.
    /// </summary>
    [UiFact]
    public void HoverTryOnPictures() => Drive(output, Edge.Bottom, null, probe =>
    {
        probe.Open();
        probe.Nav(PanelPage.Appearance);
        OpenHead(probe, Palette);
        var bloom = Array.FindIndex(LookPalettes.Choices, palette => palette.Id == LookPalettes.BloomId);
        var cell = Cells(probe, Palette).Single(each => each.Key.EndsWith("#" + bloom, StringComparison.Ordinal));
        probe.Move(cell.Center);
        Assert.Equal(1, Worn(probe).Copy);
        probe.Shot("hover-palette", Display(probe));
        probe.Leave(cell.Center);
        CloseHead(probe, Palette);

        probe.Tab(LookPart.Widgets);
        OpenHead(probe, Font);
        var font = Cells(probe, Font)[6];
        probe.Move(font.Center);
        Assert.Equal(1, Worn(probe).Copy);
        probe.Shot("hover-font", Display(probe));
        probe.Leave(font.Center);
    });

    /// <summary>The stage map's "Worn": 1 while a copy is worn over the person's config, and the Appearance worn in words.</summary>
    private static (int Copy, string Appearance) Worn(Probe probe)
    {
        var worn = probe.Read("stage-edit.json").Single(spot => spot.Hit == "Worn");
        return (worn.Index, worn.Key);
    }

    /// <summary>The expander of row <paramref name="key"/> wheeled into sight and opened, its picker's cells in sight.</summary>
    private static void OpenHead(Probe probe, string key)
    {
        probe.WheelTo(PanelHitKind.Head, key);
        probe.Click(probe.InSight(PanelHitKind.Head, key)[0].Center);
        probe.WheelTo(PanelHitKind.Act, key + "/#picker");
    }

    /// <summary>The expander of row <paramref name="key"/> wheeled back into sight and shut, the page above it in sight again.</summary>
    private static void CloseHead(Probe probe, string key)
    {
        for (var notch = 0; notch < 40 && probe.InSight(PanelHitKind.Head, key).Count == 0; notch++)
        {
            probe.Wheel(1);
        }
        probe.Click(probe.InSight(PanelHitKind.Head, key)[0].Center);
        probe.Wheel(20);
    }

    /// <summary>The cells of row <paramref name="key"/>'s inline picker in sight, top first.</summary>
    private static List<Spot> Cells(Probe probe, string key) => probe.InSight(PanelHitKind.Act, key + "/#picker");

    /// <summary>
    /// The pointer on each cell of row <paramref name="key"/>'s picker in turn till the bars wear a copy (the choice in force
    /// changes nothing, so the first may be it); the cell and the Appearance worn then.
    /// </summary>
    private static (Spot Cell, string Appearance) RestTillWorn(Probe probe, string key)
    {
        foreach (var cell in Cells(probe, key).Take(8))
        {
            probe.Move(cell.Center);
            if (Worn(probe) is { Copy: 1 } worn)
            {
                return (cell, worn.Appearance);
            }
        }
        Assert.Fail($"no choice of {key} the pointer rested on was worn");
        return default;
    }

    private static string Text(RECT r) => $"{r.left},{r.top}..{r.right},{r.bottom}";

    /// <summary>The stage's display, whole: the bar, the panel and the picker.</summary>
    private static RECT Display(Probe probe)
    {
        var work = probe.Read("stage-edit.json").First(spot => spot.Hit == "Work").Rect;
        return new RECT { left = work.left, top = 0, right = work.right, bottom = 1440 };
    }
}
