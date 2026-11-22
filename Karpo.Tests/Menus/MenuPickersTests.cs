// menus: a colour, an icon, a target, a time of day and a length in a widget's menu (2026-10-19, the
// person: "inline every setting of every bar widget and folder widget into its edit menu"; Karpo.Core\
// OptionPickers.cs, Karpo.Dock\MenuPickers.cs, Flyout.Pickers.cs, docs\widgets.md §224). Held here:
//
//  - **The same write as Settings.** A swatch, Automatic and a colour of one's own leave the config as
//    the palette writes it (ColorPalette.Write, ColorPalette.Remember); an alarm's time, a
//    countdown's length, a pin's target and icon and a folder's icon as their pages' controls do.
//  - **The steps.** A time of day round the clock (an hour on from 23:15 is 00:15), its minutes round
//    their hour, across noon for AM/PM; its hour on a 12-hour clock; a length within a minute and
//    twelve hours, greyed at either end.
//  - **The tiles' places.** Eight swatches to a line of a menu and as many 40 DIP icons as fit, each run its own lines,
//    each line a menu row tall (§240).
//  - **The icons.** The whole gallery (§249), each glyph once, each of Settings' gallery and named by its words.
//  - **§523: one picker for every colour.** Every colour row of every menu Karpo has opens the same level: its swatches, then
//    ruled off the spectrum, the hue, the opacity where the value takes one, the hex box; a colour mixed is written with its
//    alpha, kept first among the recent ones and only once kept, a hex typed is the colour it says.
//  - **On a real menu** (KARPO_UI_TESTS=1): a swatch hit by the pointer and walked by the keys; the
//    time's rows (§240: "Hours", "Minutes", "AM or PM", each a row of its own on the time's level)
//    stepped by a press and by the keys; §523 a colour's picker dragged, stepped, put back and typed in, and its tree.
//    Pictures in KARPO_PNG_DIR: menu-pickers-colors-<look>.png, menu-pickers-icons-<look>.png,
//    menu-pickers-time-<look>.png and color-picker-look-<look>.png, at 150 %.

using System.Text.Json;
using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using Karpo.Tests.Localization;
using TerraFX.Interop.Windows;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Menus;

[Collection(InLanguage.Collection)]
public sealed partial class MenuPickersTests
{
    private static readonly Argb Navy = new(0xFF, 0x1E, 0x3A, 0x5F);

    private static string Json(WidgetItem item) => item switch
    {
        BarItem bar => JsonSerializer.Serialize(bar, ConfigJson.Default.BarItem),
        CellItem cell => JsonSerializer.Serialize(cell, ConfigJson.Default.CellItem),
        _ => "",
    };

    /// <summary>A copy of <paramref name="item"/>, as the file would give the page it: the same id.</summary>
    private static T Copy<T>(T item)
        where T : WidgetItem => item switch
        {
            BarItem bar => (T)(WidgetItem)JsonSerializer.Deserialize(Json(bar), ConfigJson.Default.BarItem)!,
            CellItem cell => (T)(WidgetItem)JsonSerializer.Deserialize(Json(cell), ConfigJson.Default.CellItem)!,
            _ => item,
        };

    private static string Json(Config config) => JsonSerializer.Serialize(config, ConfigJson.Default.Config);

    /// <summary>The menu's rows for <paramref name="item"/>'s settings, their picks into <paramref name="picks"/>.</summary>
    private static List<FlyoutItem> Menu(WidgetItem item, List<OptionPick> picks, OptionPlace? place = null) =>
        OptionMenu.Section(OptionRows.Of(item, null, place), picks);

    /// <summary>§507: the four colours of the default layout's Taskbar widgets, as Look › has them, named for their tone.</summary>
    private static IReadOnlyList<OptionRow> Colors(OptionPlace place)
    {
        var rows = LookRows.Editor(place, Mine(place.Config), LookRows.SectionKey(LookPart.Widgets));
        // §513: the pattern's colour stands only while a pattern is in force (LookTests).
        // §568, §613: each colour's half of the mode the place is drawn in.
        return [.. ToneNames.All.Where(tone => tone != Tone.PatternColor).Select(tone => OptionRows.Find(rows, LookRows.ColourKey(LookRows.SectionKey(LookPart.Widgets), tone))! with { Title = ToneNames.Name(tone) })];
    }

    /// <summary>The default layout's Taskbar widgets.</summary>
    private static LookSlot Mine(Config config) => Looks.Section(config, LookPart.Widgets);

    private static string ColorKey(Tone tone) => LookRows.SectionKey(LookPart.Widgets) + "." + ToneNames.JsonName(tone);

    /// <summary>What <paramref name="pick"/> does to the style's colours, read again as they are now.</summary>
    private static bool ApplyColors(OptionPick pick, OptionPlace place) => OptionMenu.ApplyRow(OptionRows.Find(Colors(place), pick.Key), pick);

    private static OptionPick Pick(IReadOnlyList<OptionPick> picks, OptionPickKind kind, string key, int value = 0) =>
        picks.First(pick => pick.Kind == kind && pick.Key == key && pick.Value == value);

    // --- The same write as the palette ------------------------------------------------------------------

    /// <summary>A swatch, None and a colour of one's own write what a look's colour writes (Looks.WritePaint): Background.</summary>
    [Fact]
    public void AColourIsWrittenAsCustomizeWritesIt()
    {
        foreach (var dark in new[] { true, false })
        {
            var menu = new Config();
            _ = menu.DefaultLayout;
            var page = menu.Clone();
            var place = new OptionPlace(menu, dark);
            void Write(Karpo.Core.Paint paint) => Assert.True(Looks.WritePaint(Mine(page), Tone.Background, dark, paint));
            List<OptionPick> picks = [];
            var rows = OptionMenu.Section(Colors(place), picks);
            var row = rows.Single(item => item.Text == S.SettingsAppearanceCustomBackgroundHeader);
            // §574: the colour in force (§547, §568: Karpo's own card of the mode) said at the row's end as any value, its swatch
            // in the row's icon's place; no "Default (…)": None first; then the grid.
            var card = dark ? KarpoLook.Card : KarpoLook.LightCard;
            Assert.Equal(new FlyoutValue(OptionPickers.Say(card)), row.Value);
            Assert.Equal(new FlyoutTile(card, Cell: false), row.Icon);
            Assert.Equal(S.SettingsAppearanceSeparatorStyleNone, row.Children![0].Text);
            Assert.False(row.Children[0].Checked);
            Assert.DoesNotContain(row.Children, child => child.Text.StartsWith(S.SettingsValueDefault("\u0001").Split('\u0001')[0], StringComparison.Ordinal));
            var swatches = row.Children.Where(child => child.Icon is FlyoutTile { Cell: true }).ToList();
            Assert.Equal(ColorPalette.Presets, swatches.Select(child => ((FlyoutTile)child.Icon!).Color!.Value));

            var third = OptionMenu.PickOf(picks, swatches[2].Id)!;
            Assert.Equal(OptionPickKind.Set, third.Kind);
            Assert.True(ApplyColors(third, place));
            Write(new Karpo.Core.Paint(ColorPalette.Presets[2]));
            Assert.Equal(Json(page), Json(menu));
            Assert.False(ApplyColors(third, place));

            // Shown again: that swatch ringed, None not; §509: its swatch in the row's icon's place, its name at its end.
            var shownAgain = OptionMenu.Section(Colors(place), []).Single(item => item.Text == S.SettingsAppearanceCustomBackgroundHeader);
            Assert.Equal(new FlyoutTile(ColorPalette.Presets[2], Cell: false), shownAgain.Icon);
            // §690: a preset its name at the row's end.
            Assert.Equal(ColorPalette.NameOf(ColorPalette.Presets[2]), shownAgain.Value?.Text);
            var again = shownAgain.Children!;
            Assert.False(again[0].Checked);
            Assert.True(again.Where(child => child.Icon is FlyoutTile { Cell: true }).ElementAt(2).Checked);

            // None.
            Assert.True(ApplyColors(OptionMenu.PickOf(picks, row.Children[0].Id)!, place));
            Write(Karpo.Core.Paint.Nothing);
            Assert.Equal(Json(page), Json(menu));

            // §523: a colour of one's own, mixed in the picker: its alpha its own, and kept the first recent colour (opaque).
            var custom = OptionRows.Find(Colors(place), ColorKey(Tone.Background))!;
            Assert.True(custom.SetColor!(Navy with { A = 0x40 }, true));
            Write(new Karpo.Core.Paint(Navy with { A = 0x40 }));
            ColorPalette.Remember(page, Navy);
            Assert.Equal(Json(page), Json(menu));
            var recent = OptionMenu.Section(Colors(place), []).Single(item => item.Text == S.SettingsAppearanceCustomBackgroundHeader).Children!;
            var first = recent.First(child => child.Icon is FlyoutTile { Cell: true });
            Assert.Equal(Navy, ((FlyoutTile)first.Icon!).Color);
            Assert.True(first.Checked);
        }
    }

    /// <summary>
    /// §459: None is a background's and an outline's (not there at all), never a text's; the opacity is the colour's own,
    /// §523 the picker's slider under the swatches, mixing from Windows' blue where it is None, a swatch picked after
    /// keeping it.
    /// </summary>
    [Fact]
    public void NoneAndTheOpacityAreTheColoursOwn()
    {
        var config = new Config();
        var place = new OptionPlace(config, Dark: false);

        // §574: an outline Karpo draws none of is None, its first choice ticked.
        var outline = OptionRows.Find(Colors(place), ColorKey(Tone.Outline))!;
        Assert.Equal((S.SettingsAppearanceSeparatorStyleNone, 0), (outline.Tiles[0].Name, outline.Chosen));
        Assert.DoesNotContain(OptionRows.Find(Colors(place), ColorKey(Tone.Text))!.Tiles, tile => tile.Name == S.SettingsAppearanceSeparatorStyleNone);

        // None: no plate at all, said at the row's end, no opacity to step.
        Assert.True(OptionRows.Find(Colors(place), ColorKey(Tone.Background))!.Set!(0));
        Assert.Equal(Karpo.Core.Paint.Nothing, Looks.Of(config, place.Dark).Widgets.Background);
        List<OptionPick> picks = [];
        var row = OptionMenu.Section(Colors(place), picks).Single(item => item.Text == S.SettingsAppearanceCustomBackgroundHeader);
        Assert.Equal(S.SettingsAppearanceSeparatorStyleNone, row.Value!.Text);
        // §509: no colour, a slashed swatch in its icon's place; §574 an accent the file says none of, §547 Karpo's blue in the
        // shade for the mode the taskbar reads in, as any colour.
        Assert.Equal(new FlyoutTile(null, Cell: false) { Slashed = true }, row.Icon);
        var rows = OptionMenu.Section(Colors(place), []);
        Assert.Equal(new FlyoutTile(KarpoLook.AccentDark1, Cell: false), rows.Single(item => item.Text == S.SettingsAppearanceCustomAccentHeader).Icon);
        // §523: the picker's opacity slider; None mixes from Windows' blue, opaque.
        var opacity = Assert.IsType<FlyoutMix>(row.Children!.Single(child => child.Text == S.SettingsAppearanceStylesOpacity).Control);
        Assert.Equal(MixPart.Opacity, opacity.Part);
        Assert.Equal(ColorPalette.Blue, opacity.Color);

        // A colour of one's own at 90 %, a swatch picked after keeping it.
        Assert.True(OptionRows.Find(Colors(place), ColorKey(Tone.Background))!.SetColor!(Navy.At(0.9), true));
        Assert.Equal(90, (int)Math.Round(Looks.Of(config, place.Dark).Widgets.Background.Color!.Value.A * 100 / 255.0));
        // §509: a see-through colour's swatch is that colour, its opacity said at the row's end.
        row = OptionMenu.Section(Colors(place), []).Single(item => item.Text == S.SettingsAppearanceCustomBackgroundHeader);
        var seeThrough = Looks.Of(config, place.Dark).Widgets.Background.Color!.Value;
        Assert.Equal(new FlyoutTile(seeThrough, Cell: false), row.Icon);
        Assert.Equal(S.SettingsAppearanceCustomValue("#1E3A5F", 90), row.Value!.Text);
        Assert.True(OptionRows.Find(Colors(place), ColorKey(Tone.Background))!.Set!(1 + ColorPalette.Recent(config).Count));
        var picked = Looks.Of(config, place.Dark).Widgets.Background.Color!.Value;
        Assert.Equal((ColorPalette.Presets[0] with { A = 0 }, 90), (picked with { A = 0 }, (int)Math.Round(picked.A * 100 / 255.0)));
    }

    /// <summary>
    /// §523: the picker's own colour is the colour row's: the spectrum, the hue and the opacity mix from the colour in force
    /// (alpha and all), the hex box says it ("#RRGGBBAA" see-through), each its pick on the row's key; a value that takes no
    /// alpha (a calendar's colour) has no opacity slider and an opaque "#RRGGBB" box.
    /// </summary>
    [Fact]
    public void ThePickersRowsMixTheColourInForce()
    {
        var config = new Config();
        var place = new OptionPlace(config, Dark: true);
        Assert.True(OptionRows.Find(Colors(place), ColorKey(Tone.Background))!.SetColor!(Navy.At(0.9), true));
        List<OptionPick> picks = [];
        var row = OptionMenu.Section(Colors(place), picks).Single(item => item.Text == S.SettingsAppearanceCustomBackgroundHeader);
        var tail = row.Children!.SkipWhile(child => child.Control is not FlyoutMix).ToList();
        Assert.True(row.Children![row.Children.Count - tail.Count - 1].Separator);
        Assert.Equal([S.DockEditColorSpectrum, S.DockEditColorHue, S.SettingsAppearanceStylesOpacity, S.DockEditColorHex], tail.Select(child => child.Text));
        Assert.Equal([MixPart.Spectrum, MixPart.Hue, MixPart.Opacity], tail.Take(3).Select(child => ((FlyoutMix)child.Control!).Part));
        Assert.All(tail.Take(3), child => Assert.Equal(Navy.At(0.9), ((FlyoutMix)child.Control!).Color));
        var hex = Assert.IsType<FlyoutField>(tail[3].Control);
        Assert.Equal(("#1E3A5FE6", "#RRGGBBAA", true), (hex.Text, hex.Placeholder, hex.Hex));
        Assert.Equal(
            [OptionPickKind.Mix, OptionPickKind.Mix, OptionPickKind.Mix, OptionPickKind.Field],
            tail.Select(child => OptionMenu.PickOf(picks, child.Id)!.Kind));
        Assert.All(tail, child => Assert.Equal(ColorKey(Tone.Background), OptionMenu.PickOf(picks, child.Id)!.Key));
        // Nothing of Windows' own dialog any more: no pick of a colour's opens one.
        Assert.DoesNotContain(picks, pick => pick.Kind == OptionPickKind.Browse);

        // A calendar's colour takes no alpha: no opacity, an opaque hex box.
        var calendar = PartColours.Row("c", "Colour", "#E3008C", "#038387", _ => true);
        var opaque = OptionMenu.Rows([calendar], []).Single().Children!.SkipWhile(child => child.Control is not FlyoutMix).ToList();
        Assert.Equal([MixPart.Spectrum, MixPart.Hue], opaque.Where(child => child.Control is FlyoutMix).Select(child => ((FlyoutMix)child.Control!).Part));
        Assert.Equal(("#E3008C", "#RRGGBB"), (((FlyoutField)opaque[^1].Control!).Text, ((FlyoutField)opaque[^1].Control!).Placeholder));
    }

    /// <summary>
    /// §523: a colour mixed is the value as it is dragged and nothing else; kept, it goes first among the recent colours (opaque);
    /// §574 a swatch picked afterwards replaces it, as any value.
    /// </summary>
    [Fact]
    public void AColourMixedIsRememberedOnceKept()
    {
        var config = new Config();
        var place = new OptionPlace(config, Dark: false);
        OptionRow Background() => OptionRows.Find(Colors(place), ColorKey(Tone.Background))!;
        var teal = new Argb(0xB3, 0x12, 0x80, 0x80);
        // A look while it is dragged: written, not remembered.
        Assert.True(Background().SetColor!(teal, false));
        Assert.Equal(teal, Looks.Of(config, place.Dark).Widgets.Background.Color);
        Assert.Empty(ColorPalette.Recent(config));
        // Kept: the first recent colour, opaque; the same again changes nothing.
        Assert.True(Background().SetColor!(teal, true));
        Assert.Equal([teal with { A = 255 }], ColorPalette.Recent(config));
        Assert.False(Background().SetColor!(teal, true));
        Assert.True(Background().SetColor!(Navy, true));
        Assert.Equal([Navy, teal with { A = 255 }], ColorPalette.Recent(config));
        // Shown again: the recent colour ringed, and said at the row's end.
        var row = OptionMenu.Section(Colors(place), []).Single(item => item.Text == S.SettingsAppearanceCustomBackgroundHeader);
        Assert.True(row.Children!.First(child => child.Icon is FlyoutTile { Cell: true }).Checked);
        Assert.Equal(OptionPickers.Say(Navy), row.Value!.Text);
        // A swatch of the grid: that colour, the recent ones kept.
        var grid = ColorPalette.Presets[0];
        Assert.True(Background().Set!(Background().Tiles.ToList().FindIndex(tile => tile.Color == grid && tile.Group != S.SettingsAppearanceCustomRecent)));
        Assert.Equal(grid, Looks.Of(config, place.Dark).Widgets.Background.Color);
        Assert.Equal([Navy, teal with { A = 255 }], ColorPalette.Recent(config));
        row = OptionMenu.Section(Colors(place), []).Single(item => item.Text == S.SettingsAppearanceCustomBackgroundHeader);
        Assert.False(row.Value!.Implied);
    }

    /// <summary>
    /// §523: the hex box's words kept (Enter, or left) are the colour they say, as a colour mixed is kept; anything else changes
    /// nothing; a value that takes no alpha keeps none typed.
    /// </summary>
    [Fact]
    public void AHexTypedIsKeptAsTheColourItSays()
    {
        var config = new Config();
        var place = new OptionPlace(config, Dark: true);
        OptionRow Background() => OptionRows.Find(Colors(place), ColorKey(Tone.Background))!;
        Assert.True(FieldPicks.KeepRow(Background(), new FieldCommit(0, "#12345680", null)));
        Assert.Equal(new Argb(0x80, 0x12, 0x34, 0x56), Looks.Of(config, dark: true).Widgets.Background.Color);
        Assert.Equal(new Argb(255, 0x12, 0x34, 0x56), ColorPalette.Recent(config)[0]);
        foreach (var wrong in new[] { "", "#12", "#1234567", "navy", "#GG0000" })
        {
            Assert.False(FieldPicks.KeepRow(Background(), new FieldCommit(0, wrong, null)));
        }
        Assert.Equal(new Argb(0x80, 0x12, 0x34, 0x56), Looks.Of(config, dark: true).Widgets.Background.Color);
        string? written = null;
        var calendar = PartColours.Row("c", "Colour", null, "#038387", picked => (written = picked) is not null);
        Assert.True(FieldPicks.KeepRow(calendar, new FieldCommit(0, "abcdef80", null)));
        Assert.Equal("#ABCDEF", written);
    }

    /// <summary>
    /// §523: every colour row of every menu Karpo has (MenuCorpus: a look's, a widget's own, a folder face's, a calendar's and
    /// an account's, ⚙'s) opens one picker, the same: its swatches, then ruled off the spectrum, the hue, the opacity where the
    /// value takes one, and the hex box last; nothing else mixes a colour, and no row says "Custom color…" any more.
    /// </summary>
    [Fact]
    public void EveryColourRowOpensTheOnePicker()
    {
        var levels = 0;
        var alpha = 0;
        foreach (var menu in MenuCorpus.All())
        {
            foreach (var level in Levels(menu.Items))
            {
                // §634: a look's tiles (a pattern's, cells of their own) are no colour's swatches.
                var swatches = level.Any(item => item.Icon is FlyoutTile { Cell: true, Color: not null, CellDip: <= 0 });
                var mixer = level.Where(item => item.Control is FlyoutMix).ToList();
                Assert.DoesNotContain(level, item => item.Text.StartsWith("Custom color", StringComparison.Ordinal));
                if (!swatches && mixer.Count == 0)
                {
                    continue;
                }
                levels++;
                var tail = level.SkipWhile(item => item.Control is not FlyoutMix).ToList();
                var parts = tail.Where(item => item.Control is FlyoutMix).Select(item => ((FlyoutMix)item.Control!).Part).ToList();
                Assert.True(parts.SequenceEqual([MixPart.Spectrum, MixPart.Hue]) || parts.SequenceEqual([MixPart.Spectrum, MixPart.Hue, MixPart.Opacity]), $"{menu.Name}: {string.Join(", ", parts)}");
                Assert.Equal(parts.Count + 1, tail.Count);
                Assert.True(Assert.IsType<FlyoutField>(tail[^1].Control).Hex, menu.Name);
                Assert.True(level[level.Count - tail.Count - 1].Separator, menu.Name);
                Assert.True(swatches, $"{menu.Name}: a picker without swatches");
                alpha += parts.Count == 3 ? 1 : 0;
            }
        }
        // Many of them, of both kinds: a look's colours take an alpha, a calendar's do not (§567: the Appearance's alone, no widget's).
        Assert.True(levels > 10, $"only {levels} colour levels");
        Assert.InRange(alpha, 1, levels - 1);

        static IEnumerable<IReadOnlyList<FlyoutItem>> Levels(IReadOnlyList<FlyoutItem> items)
        {
            yield return items;
            foreach (var item in items)
            {
                if (item.Children is { } children)
                {
                    foreach (var level in Levels(children))
                    {
                        yield return level;
                    }
                }
                if (item.Control is FlyoutControl.Tools tools)
                {
                    foreach (var tool in tools.Buttons.Where(tool => tool.Level is not null))
                    {
                        foreach (var level in Levels(tool.Level!))
                        {
                            yield return level;
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// No widget's own rows are colours (§228: they are Appearance's, at the foot, WidgetLooks); a separator's look is its
    /// kind's, §478 its layout's style's, picked in Layout ▾, its menu linking there (§351); none without a place.
    /// </summary>
    [Fact]
    public void NoWidgetsRowsAreColours()
    {
        var place = new OptionPlace(new Config(), Dark: true);
        foreach (var item in Enum.GetValues<BarKind>().Select(kind => (WidgetItem)kind.New()).Concat(Enum.GetValues<ListKind>().Select(kind => kind.New())))
        {
            Assert.DoesNotContain(OptionRows.Of(item, null, place), row => row.Kind == OptionRowKind.Color);
        }
        Assert.DoesNotContain(OptionRows.Of(BarKind.Volume.New()), row => row.Kind == OptionRowKind.Color);
    }

    /// <summary>An alarm's time stepped in the menu is the time the page's picker writes.</summary>
    [Fact]
    public void AnAlarmsTimeIsWrittenAsThePagesPickerWritesIt()
    {
        var menu = ListKind.Alarm.New();
        menu.Alarm = new AlarmOptions { Time = "23:15" };
        var page = Copy(menu);
        List<OptionPick> picks = [];
        // On a 12-hour clock, which has AM/PM.
        _ = Menu(menu, picks, new OptionPlace(new Config { Formats = new() { Time = "h:mm tt" } }, Dark: true));
        // An hour on: past midnight.
        Assert.True(OptionMenu.Apply(menu, Pick(picks, OptionPickKind.Step, OptionRows.AlarmTime, TimeSteps.Hour)));
        page.Alarm!.Time = AlarmTimes.Picked(page.Alarm.Time, TimeSpan.FromMinutes(15))!;
        Assert.Equal(Json(page), Json(menu));
        Assert.Equal("00:15", menu.Alarm!.Time);
        // A minute back from :00 is :59 of the same hour; across noon.
        menu.Alarm.Time = page.Alarm.Time = "07:00";
        Assert.True(OptionMenu.Apply(menu, Pick(picks, OptionPickKind.Step, OptionRows.AlarmTime, -TimeSteps.Minute)));
        Assert.Equal("07:59", menu.Alarm.Time);
        Assert.True(OptionMenu.Apply(menu, Pick(picks, OptionPickKind.Step, OptionRows.AlarmTime, TimeSteps.Noon)));
        Assert.Equal("19:59", menu.Alarm.Time);
        page.Alarm.Time = AlarmTimes.Picked(page.Alarm.Time, new TimeSpan(19, 59, 0))!;
        Assert.Equal(Json(page), Json(menu));

        // An alarm nobody set gets its record at its first change, as the page's picker gives it one.
        var fresh = ListKind.Alarm.New();
        fresh.Alarm = null;
        List<OptionPick> first = [];
        _ = Menu(fresh, first);
        Assert.True(OptionMenu.Apply(fresh, Pick(first, OptionPickKind.Step, OptionRows.AlarmTime, TimeSteps.Hour)));
        Assert.Equal(AlarmTimes.Text(AlarmTimes.Default + 60), fresh.Alarm!.Time);
    }

    /// <summary>A countdown's length stepped in the menu is the length the page's number box writes, a minute to twelve hours.</summary>
    [Fact]
    public void ACountdownsLengthIsWrittenAsThePagesBoxWritesIt()
    {
        var menu = ListKind.Timer.New();
        var page = Copy(menu);
        List<OptionPick> picks = [];
        _ = Menu(menu, picks);
        var hour = Pick(picks, OptionPickKind.Step, OptionRows.CountdownMinutes, TimeSteps.Hour);
        Assert.True(OptionMenu.Apply(menu, hour));
        (page.Countdown ??= new CountdownOptions()).Minutes = CountdownLength.Minutes(CountdownLength.Of(page) + 60)!.Value;
        Assert.Equal(Json(page), Json(menu));
        Assert.Equal(65, menu.Countdown!.Minutes);

        // At the ends: an hour on from 11 h 30 is twelve hours, and no more; a minute back from one is nothing.
        menu.Countdown.Minutes = 690;
        Assert.True(OptionMenu.Apply(menu, hour));
        Assert.Equal(CountdownOptions.MostMinutes, menu.Countdown.Minutes);
        Assert.False(OptionMenu.Apply(menu, hour));
        menu.Countdown.Minutes = 1;
        Assert.False(OptionMenu.Apply(menu, Pick(picks, OptionPickKind.Step, OptionRows.CountdownMinutes, -TimeSteps.Minute)));
        // ...and the menu greys the steps that can't: the length's level, a row for its hours and one
        // for its minutes, each one row (§240), the length at the row's end.
        var length = Menu(menu, []).Single(item => item.Text == S.SettingsWidgetCountdownLengthHeader);
        Assert.Equal(S.WidgetCommonMinutes(1), length.Value!.Text);
        var parts = length.Children!.Select(row => (FlyoutControl.Stepper)row.Control!).ToList();
        Assert.Equal([S.DockEditHours, S.DockEditMinutes], length.Children!.Select(row => row.Text));
        Assert.Equal([(false, true), (false, true)], parts.Select(part => (part.CanFewer, part.CanMore)));
        Assert.Equal([S.WidgetCommonHours(0), S.WidgetCommonMinutes(1)], parts.Select(part => part.Value));
        Assert.Equal($"{S.WidgetCommonHours(1)} {S.WidgetCommonMinutes(25)}", MenuPickers.LengthSaid(85));
        Assert.Equal(S.WidgetCommonHours(2), MenuPickers.LengthSaid(120));
    }

    /// <summary>A pin's target from Windows' dialog and its icon from the grid, as its page sets them; its own icon again.</summary>
    [Fact]
    public void APinsTargetAndIconAreWrittenAsItsPageWritesThem()
    {
        var menu = BarKind.Pin.New();
        menu.Pin ??= new PinOptions();
        var page = Copy(menu);
        List<OptionPick> picks = [];
        var rows = Menu(menu, picks);
        // What it opens is one row (§292): what it opens at its end, never a line under it; §425 its kinds under it,
        // a file's "Browse…" over its field.
        var target = rows.Single(row => OptionMenu.PickOf(picks, row.Id)?.Key == OptionPickers.PinTarget);
        Assert.Null(target.Detail);
        Assert.Equal(S.SettingsPinTargetNone, target.Value!.Text);
        Assert.Equal("notepad.exe", PinTargets.NameOf(@"C:\Windows\notepad.exe"));
        Assert.Equal("Tools", PinTargets.NameOf(@"D:\Tools\"));
        var file = target.Children!.Single(row => OptionMenu.PickOf(picks, row.Id)?.Key == OptionPickers.PinFile);
        var browse = file.Children![0];
        Assert.Equal((S.SettingsPinTargetBrowse, OptionPickKind.Browse), (browse.Text, OptionMenu.PickOf(picks, browse.Id)!.Kind));
        Assert.IsType<FlyoutField>(file.Children[1].Control);
        Assert.True(OptionMenu.Row(menu, OptionMenu.PickOf(picks, browse.Id)!)!.SetText!(@"C:\Windows\notepad.exe"));
        page.Pin!.Target = @"C:\Windows\notepad.exe";
        Assert.Equal(Json(page), Json(menu));

        var icons = rows.Single(row => OptionMenu.PickOf(picks, row.Id)?.Key == OptionPickers.PinIcon).Children!;
        var star = icons.Single(child => child.Icon is FlyoutTile { Code: "\uE734" });
        Assert.True(OptionMenu.Apply(menu, OptionMenu.PickOf(picks, star.Id)!));
        page.Pin.Icon = "\uE734";
        Assert.Equal(Json(page), Json(menu));
        // The cross, the first tile under the field (§306), takes it away: its own icon again.
        Assert.Equal((S.SettingsValueDefault(S.SettingsIconPickerOwnIcon), OptionGlyphs.Clear), (icons[1].Text, ((FlyoutTile)icons[1].Icon!).Code));
        Assert.True(OptionMenu.Apply(menu, OptionMenu.PickOf(picks, icons[1].Id)!));
        page.Pin.Icon = "";
        Assert.Equal(Json(page), Json(menu));
        // Over them the field filtering them, which sets nothing (§249).
        Assert.Equal(OptionPickKind.None, OptionMenu.PickOf(picks, icons[0].Id)!.Kind);
        Assert.True(Assert.IsType<FlyoutField>(icons[0].Control).Filters);
    }

    /// <summary>
    /// A folder's icon, a level of its own under its face's submenu (\u00a7226: a third level): its default
    /// and its tiles under the gallery's headings, then the whole gallery's search; set as the folder
    /// page's icon card set it.
    /// </summary>
    [Fact]
    public void AFoldersIconIsWrittenAsItsPageWritesIt()
    {
        var folder = BarKind.FolderList.New();
        folder.Folder!.Faces = [FolderFace.Of(FolderFaces.IconId)];
        var page = Copy(folder);
        List<OptionPick> picks = [];
        var shown = OptionMenu.FaceSection(folder.Folder, picks).First(row => row.Reorder != 0).Children!;
        var row = shown.Single(row => OptionMenu.PickOf(picks, row.Id)?.Key == OptionPickers.FolderIcon && row.Children is not null);
        // The icon in force at the row's end (§228): the folder's own glyph for none.
        Assert.Equal(new FlyoutIcon.Glyph(OptionGlyphs.Icon), row.Icon);
        Assert.Equal(new FlyoutIcon.Glyph(FolderOptions.FolderGlyph), row.Value!.Icon);
        Assert.DoesNotContain(row.Children!, child => child.Header);
        var group = row.Children!.Where(child => !child.Separator).ToList();
        // The field first (\u00A7246), then the cross, ringed, and the whole gallery.
        Assert.Equal(S.SettingsIconPickerSearch, group[0].Text);
        Assert.Equal(OptionPickKind.None, OptionMenu.PickOf(picks, group[0].Id)!.Kind);
        Assert.True(group[1].Checked);
        Assert.Equal(MenuIcons.All.Count + 1, group.Count(row => row.Icon is FlyoutTile { Cell: true }));
        var home = group.Single(child => child.Icon is FlyoutTile { Code: "\uE80F" });
        Assert.True(OptionMenu.Apply(folder, OptionMenu.PickOf(picks, home.Id)!));
        page.Folder!.Face.Glyph = "\uE80F";
        Assert.Equal(Json(page), Json(folder));
        Assert.True(OptionMenu.Apply(folder, OptionMenu.PickOf(picks, group[1].Id)!));
        page.Folder.Face.Glyph = null;
        Assert.Equal(Json(page), Json(folder));
    }

    // --- The steps -------------------------------------------------------------------------------------

    [Theory]
    [InlineData(23 * 60 + 15, TimeSteps.Hour, 15)]
    [InlineData(0, -TimeSteps.Hour, 23 * 60)]
    [InlineData(7 * 60 + 59, TimeSteps.Minute, 7 * 60)]
    [InlineData(7 * 60, -TimeSteps.Minute, 7 * 60 + 59)]
    [InlineData(11 * 60 + 30, TimeSteps.Noon, 23 * 60 + 30)]
    [InlineData(23 * 60 + 30, TimeSteps.Noon, 11 * 60 + 30)]
    [InlineData(-30, TimeSteps.Hour, 23 * 60 + 30 + 60 - 1440)]
    public void ATimeOfDayStepsRoundTheClock(int minutes, int by, int expected) =>
        Assert.Equal(expected, TimeSteps.Clock(minutes, by));

    [Fact]
    public void ATimeOfDaySaysItsHourOnTheClockKarpoShows()
    {
        Assert.Null(TimeSteps.Clock(60, 5));
        Assert.Equal(("12", "07"), (TimeSteps.HourText(0, twelveHour: true), TimeSteps.MinuteText(7)));
        Assert.Equal(("1", true), (TimeSteps.HourText(13 * 60, twelveHour: true), TimeSteps.Afternoon(13 * 60)));
        Assert.Equal(("12", true), (TimeSteps.HourText(12 * 60, twelveHour: true), TimeSteps.Afternoon(12 * 60)));
        Assert.Equal(("13", "00"), (TimeSteps.HourText(13 * 60, twelveHour: false), TimeSteps.HourText(0, twelveHour: false)));
        Assert.False(TimeSteps.Afternoon(11 * 60 + 59));

        // A 12-hour clock has AM/PM, a row of its own with a button that crosses noon; a 24-hour one
        // hasn't. Each part one row of the time's level (§240), the time at the row's end.
        var alarm = ListKind.Alarm.New();
        alarm.Alarm = new AlarmOptions { Time = "19:05" };
        var twelve = new Config { Formats = new() { Time = "h:mm tt" } };
        var time = Menu(alarm, [], new OptionPlace(twelve, true)).Single(item => item.Text == S.SettingsWidgetAlarmTimeHeader);
        Assert.Equal([S.DockEditHours, S.DockEditMinutes, S.DockEditMeridiem], time.Children!.Select(row => row.Text));
        var noon = Assert.IsType<FlyoutControl.Stepper>(time.Children![2].Control);
        Assert.True(noon.Switch);
        Assert.Equal(noon.Fewer, noon.More);
        Assert.Equal(time.Children![2].Id, noon.Fewer);
        Assert.Equal($"7:05 {noon.Value}", time.Value!.Text);
        var day = new Config { Formats = new() { Time = "HH:mm" } };
        var daytime = Menu(alarm, [], new OptionPlace(day, true)).Single(item => item.Text == S.SettingsWidgetAlarmTimeHeader);
        Assert.Equal(2, daytime.Children!.Count);
        Assert.Equal("19:05", daytime.Value!.Text);
    }

    [Theory]
    [InlineData(25, TimeSteps.Hour, 85)]
    [InlineData(690, TimeSteps.Hour, 720)]
    [InlineData(30, -TimeSteps.Hour, 1)]
    [InlineData(59, TimeSteps.Minute, 60)]
    public void ALengthStepsWithinAMinuteAndTwelveHours(int minutes, int by, int expected) =>
        Assert.Equal(expected, TimeSteps.Length(minutes, by, CountdownOptions.FewestMinutes, CountdownOptions.MostMinutes));

    [Fact]
    public void ALengthAtItsEndStepsNoFurther()
    {
        Assert.Null(TimeSteps.Length(720, TimeSteps.Minute, 1, 720));
        Assert.Null(TimeSteps.Length(1, -TimeSteps.Hour, 1, 720));
        Assert.False(TimeSteps.CanStep(1, -1, 1, 720));
        Assert.True(TimeSteps.CanStep(1, 1, 1, 720));
    }

    // --- The tiles' places and the icons ---------------------------------------------------------------

    /// <summary>
    /// Tiles as many to a line as fit at 150 %, up to their own most (a settings menu's 320 DIP holds nine; §627 a colour's
    /// swatches six, their column's in its picker), each line a menu row tall (§240); icons as many as fit in their 40 DIP
    /// cells, seven, each line a cell tall (§249); a run of each wraps on its own.
    /// </summary>
    [Fact]
    public void TheTilesStandInLinesAcrossTheMenu()
    {
        const float scale = 1.5f;
        var room = (Flyout.SettingsWidthDip - 10) * scale;
        var cell = Flyout.TileCellDip * scale;
        List<FlyoutItem> items =
        [
            new(1, "Automatic"),
            .. Enumerable.Range(0, 48).Select(at => new FlyoutItem((uint)(10 + at), $"#{at}", new FlyoutTile(Navy, Across: 12))),
            new(0, "Files", Header: true),
            .. Enumerable.Range(0, 15).Select(at => new FlyoutItem((uint)(100 + at), $"icon {at}", new FlyoutTile(Code:"\uE8B7", SideDip: MenuPickers.IconTileDip, Across: MenuPickers.IconsAcross, CellDip: MenuPickers.IconCellDip))),
            new(2, "More icons…"),
        ];
        var spots = Flyout.TileSpots(items, room, cell, scale);
        Assert.All(spots.Skip(1).Take(48), spot => Assert.Equal(48, spot.Cell));
        Assert.Equal(default, spots[0]);
        Assert.All(spots.Skip(1).Take(48), spot => Assert.Equal(9, spot.Columns));
        Assert.Equal((2, 5, true), (spots[48].Column, spots[48].Line, spots[48].EndsLine));
        Assert.Equal((0, 1), (spots[10].Column, spots[10].Line));
        Assert.True(spots[9].EndsLine);
        Assert.False(spots[8].EndsLine);
        // A wider room holds up to their most; §627 a colour's swatches six to a line, however wide the room.
        var picker = (PanelMetrics.PickerWide - 10) * scale;
        Assert.Equal(12, Flyout.TileSpots(items, picker, cell, scale)[1].Columns);
        Assert.Equal(6, Flyout.TileSpots([new(1, "#", new FlyoutTile(Navy, Across: MenuPickers.SwatchesAcross))], picker, cell, scale)[0].Columns);
        var icons = spots.Skip(50).Take(15).ToList();
        Assert.All(icons, spot => Assert.Equal((7, 60f), (spot.Columns, spot.Cell)));
        Assert.Equal((0, 2, true), (icons[^1].Column, icons[^1].Line, icons[^1].EndsLine));
        Assert.Equal(7, Flyout.TileSpots(items, room / scale, Flyout.TileCellDip).Skip(50).First().Columns);
        // A narrow menu holds fewer.
        Assert.Equal(4, Flyout.TileSpots(items, 4.5f * cell, cell)[1].Columns);

        // §302: a line spans the rows' own room, its first fill where a row's starts and its last where
        // a row's ends, the rest evenly between, none over another: no margin of its own.
        foreach (var line in new[] { spots.Skip(1).Take(9).ToList(), icons.Take(7).ToList() })
        {
            var fills = line.Select(spot => Flyout.TileFill(spot, 7, room, 0, scale)).ToList();
            Assert.Equal(7, fills[0].X);
            Assert.InRange(fills[^1].X + fills[^1].W - (7 + room), -1, 1);
            var steps = fills.Zip(fills.Skip(1), (a, b) => b.X - a.X).ToList();
            Assert.InRange(steps.Max() - steps.Min(), 0, 1);
            Assert.True(steps.Min() > fills[0].W);
        }
    }

    /// <summary>The menu's icons are Settings' whole gallery (§249), each glyph once, named by its words.</summary>
    [Fact]
    public void TheIconsAreTheGallerysOwn()
    {
        Assert.Equal(IconSearch.All.Select(icon => icon.Glyph).Distinct().Order(), MenuIcons.All.Select(icon => icon.Glyph).Order());
        Assert.All(MenuIcons.All, icon => Assert.Contains(IconSearch.All, choice => choice.Glyph == icon.Glyph && choice.Id == icon.Id));
        Assert.All(MenuIcons.All, icon => Assert.Equal(IconSearch.All.First(choice => choice.Id == icon.Id).Name, icon.Name));
    }

    // --- On a real menu --------------------------------------------------------------------------------

    private static bool Save(Flyout menu, string name, bool dark) =>
        WindowShots.SaveTogether([.. menu.OpenPanels], name, dark ? ((byte)0, (byte)0, (byte)0) : ((byte)243, (byte)243, (byte)243));

    private static nint At(POINT point) => (nint)((point.y << 16) | (point.x & 0xFFFF));

    private static void KeysTo(Flyout menu, uint id)
    {
        for (var step = 0; step < 80 && menu.KeysOn != id; step++)
        {
            _ = menu.Message(WM.WM_KEYDOWN, VK.VK_DOWN, 0);
        }
        Assert.Equal(id, menu.KeysOn);
    }

    /// <summary>
    /// The swatches in a submenu and a folder's icons in a level under its face's (their pictures), a swatch hit by the pointer and walked
    /// by the keys in a menu of them alone; the time's steppers stepped by a press and the keys.
    /// </summary>
    [UiFact]
    public void ThePickersActOnARealMenuAndThePicturesAreDrawn()
    {
        WindowShots.OnHiddenDesktop("menu-pickers", (gfx, instance) =>
        {
            const float scale = 1.5f;
            foreach (var dark in new[] { true, false })
            {
                var look = AddListPictures.LookFor(dark);
                var tone = dark ? "dark" : "light";
                var work = new RECT { right = (int)(1920 * scale), bottom = (int)(1040 * scale) };
                var anchor = new POINT { x = work.right / 3, y = work.bottom };
                var config = new Config();
                ColorPalette.Remember(config, Navy);
                Assert.True(LookShorthand.Both(Mine(config), Tone.Accent, new Karpo.Core.Paint(ColorPalette.Blue)));
                var place = new OptionPlace(config, dark);

                // Customize's accent: its swatches open, Windows' 48 colours.
                var folder = BarKind.FolderList.New();
                folder.Folder!.Faces = [FolderFace.Of(FolderFaces.IconId)];
                folder.Folder.Face.Glyph = "\uE80F";
                using (var menu = new Flyout(gfx, instance, 0) { Configures = true })
                {
                    List<OptionPick> picks = [];
                    List<FlyoutItem> items = [.. TheDock.WidgetButtons(), .. OptionMenu.Section(Colors(place), picks)];
                    menu.Show(gfx, look, scale, items, FlyoutLayout.List, anchor, work, Edge.Bottom, null);
                    WindowShots.Settle();
                    var outline = items.Single(item => item.Text == S.SettingsAppearanceCustomAccentHeader);
                    KeysTo(menu, outline.Id);
                    _ = menu.Message(WM.WM_KEYDOWN, VK.VK_RIGHT, 0);
                    WindowShots.Settle();
                    Assert.Equal(1, menu.InlineDepth);
                    // §574: no "Default (…)" first: the recent colours' swatch first, then down a line, along it and down again.
                    var swatches = outline.Children!.Where(child => child.Icon is FlyoutTile { Cell: true }).ToList();
                    Assert.Equal(swatches[0].Id, menu.HotId);
                    _ = menu.Message(WM.WM_KEYDOWN, VK.VK_DOWN, 0);
                    Assert.Equal(swatches[1].Id, menu.HotId);
                    _ = menu.Message(WM.WM_KEYDOWN, VK.VK_RIGHT, 0);
                    _ = menu.Message(WM.WM_KEYDOWN, VK.VK_RIGHT, 0);
                    Assert.Equal(swatches[3].Id, menu.HotId);
                    _ = menu.Message(WM.WM_KEYDOWN, VK.VK_DOWN, 0);
                    Assert.Equal(swatches[3 + ColorPalette.Columns].Id, menu.HotId);
                    WindowShots.Settle();
                    Assert.True(Save(menu, $"menu-pickers-colors-{tone}.png", dark));
                    var chosen = menu.Message(WM.WM_KEYDOWN, VK.VK_RETURN, 0);
                    Assert.Equal(new FlyoutEvent(FlyoutEventKind.Adjust, swatches[3 + ColorPalette.Columns].Id), chosen);
                    Assert.Equal((OptionPickKind.Set, 3 + ColorPalette.Columns), (OptionMenu.PickOf(picks, chosen.Id)!.Kind, OptionMenu.PickOf(picks, chosen.Id)!.Value));
                    Assert.True(menu.IsOpen);
                    // Set, and shown again in place (§219): a colour is one choice, so back on the rows it opened
                    // from (§392), its row lit with the swatch in its icon's place (§509), the swatch ringed under it.
                    Assert.True(ApplyColors(OptionMenu.PickOf(picks, chosen.Id)!, place));
                    List<OptionPick> again = [];
                    menu.Refresh(OptionMenu.Updated(menu.ShownItems, [.. OptionMenu.Section(Colors(place), again)]));
                    Assert.Equal((0, outline.Id), (menu.InlineDepth, menu.HotId!.Value));
                    Assert.IsType<FlyoutTile>(menu.ShownItems.Single(item => item.Id == outline.Id).Icon);
                    Assert.True(menu.ShownItems.Single(item => item.Id == outline.Id).Children!.Single(child => child.Id == swatches[3 + ColorPalette.Columns].Id).Checked);
                    menu.Hide();
                }

                // The folder's icon, a level of its own under its face's submenu (§226); its tiles walked by the keys.
                using (var menu = new Flyout(gfx, instance, 0) { Configures = true })
                {
                    List<OptionPick> picks = [];
                    var items = OptionMenu.FaceSection(folder.Folder, picks);
                    menu.Show(gfx, look, scale, items, FlyoutLayout.List, anchor, work, Edge.Bottom, null);
                    WindowShots.Settle();
                    var face = items.First(item => item.Reorder != 0);
                    KeysTo(menu, face.Id);
                    _ = menu.Message(WM.WM_KEYDOWN, VK.VK_RIGHT, 0);
                    WindowShots.Settle();
                    Assert.Equal(1, menu.InlineDepth);
                    var icon = face.Children!.Single(child => child.Children is not null && OptionMenu.PickOf(picks, child.Id)?.Key == OptionPickers.FolderIcon);
                    for (var step = 0; step < 20 && menu.HotId != icon.Id; step++)
                    {
                        _ = menu.Message(WM.WM_KEYDOWN, VK.VK_DOWN, 0);
                    }
                    Assert.Equal(icon.Id, menu.HotId);
                    _ = menu.Message(WM.WM_KEYDOWN, VK.VK_RIGHT, 0);
                    WindowShots.Settle();
                    Assert.Equal(2, menu.InlineDepth);
                    var tiles = icon.Children!.Where(child => child.Icon is FlyoutTile { Cell: true }).ToList();
                    // §249: the keys in its field; Down onto the cross, the first tile of the one grid
                    // (§306); along its line and down one (seven to a line).
                    Assert.Equal(icon.Children![0].Id, menu.HotId);
                    Assert.Equal(icon.Children![0].Id, menu.FieldKeys);
                    _ = menu.Message(WM.WM_KEYDOWN, VK.VK_DOWN, 0);
                    Assert.Equal(tiles[0].Id, menu.HotId);
                    _ = menu.Message(WM.WM_KEYDOWN, VK.VK_RIGHT, 0);
                    Assert.Equal(tiles[1].Id, menu.HotId);
                    _ = menu.Message(WM.WM_KEYDOWN, VK.VK_RIGHT, 0);
                    _ = menu.Message(WM.WM_KEYDOWN, VK.VK_RIGHT, 0);
                    Assert.Equal(tiles[3].Id, menu.HotId);
                    _ = menu.Message(WM.WM_KEYDOWN, VK.VK_DOWN, 0);
                    Assert.Equal(tiles[3 + 7].Id, menu.HotId);
                    Assert.Equal(2, menu.InlineDepth);
                    WindowShots.Settle();
                    Assert.True(Save(menu, $"menu-pickers-icons-{tone}.png", dark));
                    var chosen = menu.Message(WM.WM_KEYDOWN, VK.VK_RETURN, 0);
                    Assert.Equal(new FlyoutEvent(FlyoutEventKind.Adjust, tiles[3 + 7].Id), chosen);
                    Assert.Equal(OptionPickers.FolderIcon, OptionMenu.PickOf(picks, chosen.Id)!.Key);
                    Assert.True(menu.IsOpen);
                    menu.Hide();
                }

                // A menu of swatches alone: one hit by the pointer, a press and its release.
                using (var menu = new Flyout(gfx, instance, 0) { Configures = true })
                {
                    List<OptionPick> picks = [];
                    var grid = OptionMenu.Section(Colors(place), picks).Single(item => item.Text == S.SettingsAppearanceCustomAccentHeader).Children!;
                    menu.Show(gfx, look, scale, grid, FlyoutLayout.List, anchor, work, Edge.Bottom, null);
                    WindowShots.Settle();
                    var tiles = grid.Where(child => child.Icon is FlyoutTile { Cell: true }).ToList();
                    // The recent colour first, a line of its own; then Windows' colours, six to a line (§627).
                    var (middle, wide, tall) = menu.RowFill(tiles[10].Id)!.Value;
                    Assert.Equal(wide, tall);
                    Assert.Equal(menu.RowFill(tiles[9].Id)!.Value.Middle.y, middle.y);
                    // §302: the lines one under another, from where a row starts; §627 the mixer's column (the hue's) beside them.
                    static (float Left, float Right) Edges((POINT Middle, float W, float H) fill) => (fill.Middle.x - (fill.W / 2), fill.Middle.x + (fill.W / 2));
                    var hue = Edges(menu.RowFill(grid.First(child => child.Control is FlyoutMix { Part: MixPart.Hue }).Id)!.Value);
                    Assert.InRange(Edges(menu.RowFill(tiles[1].Id)!.Value).Left - Edges(menu.RowFill(tiles[7].Id)!.Value).Left, -1, 1);
                    Assert.InRange(Edges(menu.RowFill(tiles[6].Id)!.Value).Right - Edges(menu.RowFill(tiles[12].Id)!.Value).Right, -1, 1);
                    Assert.True(Edges(menu.RowFill(tiles[6].Id)!.Value).Right < hue.Left, "the swatches do not stand before the mixer");
                    Assert.True(middle.x - menu.RowFill(tiles[9].Id)!.Value.Middle.x > wide);
                    _ = menu.Message(WM.WM_LBUTTONDOWN, 0, At(middle));
                    var hit = menu.Message(WM.WM_LBUTTONUP, 0, At(middle));
                    Assert.Equal(new FlyoutEvent(FlyoutEventKind.Adjust, tiles[10].Id), hit);
                }

                // An alarm's time and a timer's length, on a 12-hour clock: each a row saying it, its
                // level a row for each part (§240). A press on the hour's "+" steps at once; its release
                // does nothing more; the keys step the part their row is.
                var alarm = ListKind.Alarm.New();
                alarm.Alarm = new AlarmOptions { Time = "07:30" };
                var timer = ListKind.Timer.New();
                var twelve = new OptionPlace(new Config { Formats = new() { Time = "h:mm tt" } }, dark);
                using (var menu = new Flyout(gfx, instance, 0) { Configures = true })
                {
                    List<OptionPick> picks = [];
                    var time = Menu(alarm, picks, twelve).Single(item => item.Text == S.SettingsWidgetAlarmTimeHeader);
                    var (hour, minute, noon) = (time.Children![0], time.Children[1], time.Children[2]);
                    menu.Show(gfx, look, scale, time.Children, FlyoutLayout.List, anchor, work, Edge.Bottom, null);
                    WindowShots.Settle();
                    var more = ((FlyoutControl.Stepper)hour.Control!).More;
                    var plus = menu.StepperButton(hour.Id, more: true)!.Value;
                    Assert.Equal(new FlyoutEvent(FlyoutEventKind.Adjust, more), menu.Message(WM.WM_LBUTTONDOWN, 0, At(plus)));
                    Assert.Equal(default, menu.Message(WM.WM_LBUTTONUP, 0, At(plus)));
                    Assert.Equal((OptionPickKind.Step, TimeSteps.Hour), (OptionMenu.PickOf(picks, more)!.Kind, OptionMenu.PickOf(picks, more)!.Value));
                    // Every row the same height, a menu row's.
                    Assert.All(time.Children, row => Assert.Equal(menu.RowFill(hour.Id)!.Value.H, menu.RowFill(row.Id)!.Value.H));
                    KeysTo(menu, hour.Id);
                    Assert.Equal(new FlyoutEvent(FlyoutEventKind.Adjust, more), menu.Message(WM.WM_KEYDOWN, VK.VK_ADD, 0));
                    KeysTo(menu, minute.Id);
                    Assert.Equal(new FlyoutEvent(FlyoutEventKind.Adjust, minute.Id), menu.Message(WM.WM_KEYDOWN, VK.VK_LEFT, 0));
                    KeysTo(menu, noon.Id);
                    Assert.Equal(new FlyoutEvent(FlyoutEventKind.Adjust, noon.Id), menu.Message(WM.WM_KEYDOWN, VK.VK_SPACE, 0));
                    var press = menu.StepperButton(noon.Id, more: false)!.Value;
                    Assert.Equal(new FlyoutEvent(FlyoutEventKind.Adjust, noon.Id), menu.Message(WM.WM_LBUTTONDOWN, 0, At(press)));
                    _ = menu.Message(WM.WM_LBUTTONUP, 0, At(press));
                    Assert.True(menu.IsOpen);
                    menu.Hide();
                }

                // The picture: the alarm's and the timer's menu, the time's level open beside it.
                using (var menu = new Flyout(gfx, instance, 0) { Configures = true })
                {
                    List<OptionPick> picks = [];
                    List<FlyoutItem> items = [.. Menu(alarm, picks, twelve), .. Menu(timer, picks, twelve)];
                    menu.Show(gfx, look, scale, items, FlyoutLayout.List, anchor, work, Edge.Bottom, null);
                    WindowShots.Settle();
                    var time = items.Single(item => item.Text == S.SettingsWidgetAlarmTimeHeader);
                    KeysTo(menu, time.Id);
                    _ = menu.Message(WM.WM_KEYDOWN, VK.VK_RIGHT, 0);
                    WindowShots.Settle();
                    Assert.Equal(1, menu.InlineDepth);
                    Assert.Equal(time.Children![0].Id, menu.HotId);
                    Assert.True(Save(menu, $"menu-pickers-time-{tone}.png", dark));
                    menu.Hide();
                }
            }
        });
    }
    /// <summary>
    /// §559: a colour's picker as the settings panel opens it, hung from its colour button (a control's popup, §521) low on a
    /// display at 150 %: over its button, directly, never over it; no field has the keys as it opens (its hex box, among its
    /// swatches and its mixer, is no search), so Escape closes it, as it closes every popup of the panel — with the hex box
    /// holding the keys Escape only put its words back and the picker stood, hung from where its button had been.
    /// </summary>
    [UiFact]
    public void AColoursPickerFromItsButtonHangsOffItAndEscapeClosesIt()
    {
        WindowShots.OnHiddenDesktop("color-button", (gfx, instance) =>
        {
            const float scale = 1.5f;
            var look = AddListPictures.LookFor(true) with { Animations = false };
            var work = new RECT { left = -2560, top = 0, right = 0, bottom = 1368 };
            var panel = new RECT { left = -1850, top = 12, right = -710, bottom = 1356 };
            var button = new RECT { left = -866, top = 1100, right = -758, bottom = 1148 };
            var place = new OptionPlace(new Config(), true);
            List<OptionPick> picks = [];
            List<FlyoutItem> level = [.. OptionMenu.Section(Colors(place), picks).Single(item => item.Text == S.SettingsAppearanceCustomBackgroundHeader).Children!];
            using var menu = new Flyout(gfx, instance, 0);
            menu.Show(gfx, look, scale, level, FlyoutLayout.List, new POINT { x = button.left, y = button.bottom }, work, Edge.Bottom, null, control: button, within: panel);
            WindowShots.Settle();
            var shown = menu.PanelOnScreen;
            Assert.True(shown.bottom == button.top - 6, $"the picker {shown.top}..{shown.bottom} is not directly over its button {button.top}..{button.bottom}");
            Assert.Equal(0u, menu.FieldKeys);
            Assert.Equal(FlyoutEventKind.Dismissed, menu.Message(WM.WM_KEYDOWN, VK.VK_ESCAPE, 0).Kind);
            Assert.False(menu.IsOpen);
        });
    }

    /// <summary>Tab onto row <paramref name="id"/> of <paramref name="menu"/>: the arrows are the spectrum's own (§523).</summary>
    private static void TabTo(Flyout menu, uint id)
    {
        for (var step = 0; step < 120 && menu.HotId != id; step++)
        {
            _ = menu.Message(WM.WM_KEYDOWN, VK.VK_TAB, 0);
        }
        Assert.Equal(id, menu.HotId);
    }

    /// <summary>
    /// §523: a colour's picker as the settings panel's popup shows it (a Look colour row's level, at 150 %; its pictures
    /// color-picker-look-dark.png and -light.png): a press on the spectrum and a drag past its corner say the colour as it
    /// goes, held at the edge, the release keeps it; the hue stepped by the keys and put back by Escape; the opacity to none by
    /// Home, kept as the keys leave for the hex box, where a colour typed and Enter is kept as typed; a screen reader's hue set
    /// and kept; the tree: the sliders ranges, the spectrum a slider, the hex box a value, the swatches a list.
    /// </summary>
    [UiFact]
    public void AColoursPickerMixesOnARealMenu()
    {
        WindowShots.OnHiddenDesktop("color-picker", (gfx, instance) =>
        {
            const float scale = 1.5f;
            foreach (var dark in new[] { true, false })
            {
                var look = AddListPictures.LookFor(dark) with { Animations = false };
                var tone = dark ? "dark" : "light";
                var work = new RECT { right = (int)(1920 * scale), bottom = (int)(1040 * scale) };
                var anchor = new POINT { x = work.right / 2, y = (int)(160 * scale) };
                var config = new Config();
                ColorPalette.Remember(config, ColorPalette.Presets[1]);
                var place = new OptionPlace(config, dark);
                OptionRow Background() => OptionRows.Find(Colors(place), ColorKey(Tone.Background))!;
                var start = Navy.At(0.8);
                Assert.True(Background().SetColor!(start, true));
                List<OptionPick> picks = [];
                List<FlyoutItem> Level(List<OptionPick> into) => [.. OptionMenu.Section(Colors(place), into).Single(item => item.Text == S.SettingsAppearanceCustomBackgroundHeader).Children!];
                var level = Level(picks);
                using var menu = new Flyout(gfx, instance, 0) { Configures = true, FitsContent = true };
                menu.Show(gfx, look, scale, level, FlyoutLayout.List, anchor, work, Edge.Top, null, 4);
                WindowShots.Settle();
                Assert.True(Save(menu, $"color-picker-look-{tone}.png", dark));
                uint IdOf(Func<FlyoutItem, bool> which) => level.Single(which).Id;
                var spectrum = IdOf(item => item.Control is FlyoutMix { Part: MixPart.Spectrum });
                var hue = IdOf(item => item.Control is FlyoutMix { Part: MixPart.Hue });
                var opacity = IdOf(item => item.Control is FlyoutMix { Part: MixPart.Opacity });
                var hex = IdOf(item => item.Control is FlyoutField { Hex: true });
                // Every part of it in sight, the spectrum as wide as the hue's track, the hex box's beside its swatch.
                var (middle, wide, tall) = menu.MixBoxOf(spectrum)!.Value;
                Assert.Equal((wide, Flyout.SpectrumDip * scale), (menu.MixBoxOf(hue)!.Value.W, tall));
                Assert.Equal(4, menu.MixOnScreen.Count);

                // A press in the spectrum's middle: half saturated, half bright, at the hue and the opacity in force.
                var pressed = menu.Message(WM.WM_LBUTTONDOWN, 1, At(middle));
                Assert.Equal((FlyoutEventKind.Mix, spectrum), (pressed.Kind, pressed.Id));
                var mixed = ColorMix.ToHsv(pressed.Color);
                Assert.Equal(start.A, pressed.Color.A);
                Assert.InRange(mixed.S, 0.48, 0.52);
                Assert.InRange(mixed.V, 0.48, 0.52);
                Assert.InRange(mixed.H, ColorMix.ToHsv(Navy).H - 1, ColorMix.ToHsv(Navy).H + 1);
                // Dragged far past its top right corner: held there, the hue whole.
                var corner = menu.Message(WM.WM_MOUSEMOVE, 1, At(new POINT { x = middle.x + 2000, y = middle.y - 2000 }));
                Assert.Equal(FlyoutEventKind.Mix, corner.Kind);
                var pure = ColorMix.Pure(ColorMix.ToHsv(Navy).H) with { A = start.A };
                Assert.Equal(pure, corner.Color);
                Assert.Equal(FlyoutEventKind.Nothing, menu.Message(WM.WM_MOUSEMOVE, 1, At(new POINT { x = middle.x + 2100, y = middle.y - 2100 })).Kind);
                var kept = menu.Message(WM.WM_LBUTTONUP, 0, At(new POINT { x = middle.x + 2100, y = middle.y - 2100 }));
                Assert.Equal((FlyoutEventKind.MixKept, pure), (kept.Kind, kept.Color));
                Assert.Null(menu.MixingNow);
                // As the dock keeps it: set, the first recent colour, and the level shown again with it.
                Assert.True(Background().SetColor!(kept.Color, true));
                picks = [];
                level = Level(picks);
                menu.Refresh(level);
                Assert.Equal(pure.Rgb, ColorPalette.Recent(config)[0].Rgb);
                // A recent colour more: the rows after the swatches have other ids now.
                (spectrum, hue, opacity, hex) = (IdOf(item => item.Control is FlyoutMix { Part: MixPart.Spectrum }), IdOf(item => item.Control is FlyoutMix { Part: MixPart.Hue }),
                    IdOf(item => item.Control is FlyoutMix { Part: MixPart.Opacity }), IdOf(item => item.Control is FlyoutField { Hex: true }));

                // The hue a step on by the keys, then put back by Escape.
                TabTo(menu, hue);
                var stepped = menu.Message(WM.WM_KEYDOWN, VK.VK_RIGHT, 0);
                Assert.Equal(FlyoutEventKind.Mix, stepped.Kind);
                Assert.NotEqual(pure, stepped.Color);
                Assert.Equal((FlyoutEventKind.MixUndone, pure), (menu.Message(WM.WM_KEYDOWN, VK.VK_ESCAPE, 0) is var undone ? undone.Kind : default, undone.Color));
                Assert.True(menu.IsOpen);

                // The opacity to none by Home; Tab on to the hex box keeps it.
                TabTo(menu, opacity);
                var none = menu.Message(WM.WM_KEYDOWN, VK.VK_HOME, 0);
                Assert.Equal((FlyoutEventKind.Mix, (byte)0), (none.Kind, none.Color.A));
                _ = menu.Message(WM.WM_KEYDOWN, VK.VK_TAB, 0);
                Assert.Equal(hex, menu.HotId);
                Assert.Equal((FlyoutEventKind.MixKept, pure with { A = 0 }), menu.Posted() is { } left ? (left.Kind, left.Color) : default);
                // The hex box has the keys; a colour typed over its words and Enter is kept as typed.
                Assert.Equal(hex, menu.FieldKeys);
                foreach (var c in "#12345680")
                {
                    _ = menu.Message(WM.WM_CHAR, c, 0);
                }
                Assert.Equal(FlyoutEventKind.Nothing, menu.Message(WM.WM_KEYDOWN, VK.VK_RETURN, 0).Kind);
                Assert.Equal(new FlyoutEvent(FlyoutEventKind.Adjust, hex), menu.Posted());
                var typed = menu.TakeFieldCommit(hex)!;
                Assert.Equal("#12345680", typed.Text);
                Assert.Equal(OptionPickKind.Field, OptionMenu.PickOf(picks, hex)!.Kind);
                Assert.True(FieldPicks.KeepRow(Background(), typed));
                Assert.Equal(new Argb(0x80, 0x12, 0x34, 0x56), Looks.Of(config, dark).Widgets.Background.Color);
                Assert.True(menu.IsOpen);

                // Its tree: the hue and the opacity ranges, the spectrum a slider saying its colour, the hex box a value, the swatches a list.
                var tree = menu.AccessTree;
                var hueNode = tree.All().Single(node => node.Name == S.DockEditColorHue);
                Assert.Equal((UiaRole.Slider, UiaPattern.Range, 360.0), (hueNode.Role, hueNode.Pattern, hueNode.Range!.Value.Maximum));
                Assert.Equal((UiaRole.Slider, UiaPattern.Range, 100.0), tree.All().Single(node => node.Name == S.SettingsAppearanceStylesOpacity) is var o ? (o.Role, o.Pattern, o.Range!.Value.Maximum) : default);
                Assert.Equal(UiaRole.Slider, tree.All().Single(node => node.Name == S.DockEditColorSpectrum).Role);
                var hexNode = tree.All().Single(node => node.Name == S.DockEditColorHex);
                Assert.Equal((UiaRole.Edit, UiaPattern.Value), (hexNode.Role, hexNode.Pattern));
                Assert.StartsWith("#", hexNode.Value);
                var swatches = tree.All().Single(node => node.Role == UiaRole.List);
                Assert.True(swatches.Children.Count >= ColorPalette.Presets.Count);
                Assert.All(swatches.Children, node => Assert.Equal((UiaRole.ListItem, UiaPattern.Select), (node.Role, node.Pattern)));
                // A screen reader sets the hue: looked at and kept at once.
                Uia.Act(menu.Hwnd, (nuint)(((uint)UiaAct.SetValue << 24) | (uint)hueNode.Id), (nint)(120 * 1000));
                var said = menu.Posted();
                Assert.Equal(FlyoutEventKind.Mix, said?.Kind);
                Assert.InRange(ColorMix.ToHsv(said!.Value.Color).H, 119, 121);
                Assert.Equal(FlyoutEventKind.MixKept, menu.Posted()?.Kind);
                menu.Hide();
            }
        });
    }
}
