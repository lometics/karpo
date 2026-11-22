// edit, §638 (the person, 2026-11-09: "font weight should be selectable individually but on the same row. not combined into
// one dropdown"; docs\widgets.md §638), §670 inline, apart from any window: an Appearance tab's Font is its family's picker,
// its weight's choices a second group in the same card (PanelPickers.Beside) — no dropdown, no line of the weight's own —,
// the font's one reset beside its head (PanelRow.Pair).

using Karpo.Dock;
using Karpo.Tests.Menus;

namespace Karpo.Tests.Edit;

public sealed class FontPairTests
{
    private const float Scale = 1.5f;

    private static IReadOnlyList<OptionRow> Rows(Config config) => LookRows.Rows(config, config.DefaultLayout, windows: true);

    private static PanelPageView Page(Config config, List<OptionPick>? picks = null) =>
        PanelRows.Page(PanelPage.Appearance, S.SettingsAppearanceHeader, "", OptionMenu.Rows(Rows(config), picks ?? []));

    private static readonly string Tab = PanelPage.Appearance + "/" + PanelTabs.Mark + LookParts.Name(LookPart.Widgets);

    /// <summary>The page laid out, the Taskbar widgets' tab chosen and <paramref name="open"/> open.</summary>
    private static PanelLayout Laid(PanelPageView page, params string[] open) =>
        new(new SettingsPanelTests.Words(), Scale, new PanelInput(0, new SettingsContent([page]), PanelPage.Appearance, new HashSet<string>([Tab, .. open])), PanelLayout.SizeOf(Scale, 4000));

    private static IEnumerable<PanelLine> Lines(PanelLayout layout) => layout.Blocks.SelectMany(block => block.Lines);

    private static PanelLine Font(PanelLayout layout) => Lines(layout).Single(line => line.Row.Text == S.SettingsAppearanceStylesFont && line.Row.Kind == PanelRowKind.Picker);

    /// <summary>
    /// One card: the family's picker, its head saying the family in its own face; open, its search, the families, then the
    /// weight's group under its name — each weight in its face —; no line, no dropdown of the weight's own. A weight picked
    /// writes the one value.
    /// </summary>
    [Fact]
    public void TheFamilyAndItsWeightStandInOneCard()
    {
        var config = MenuCorpus.Fresh();
        List<OptionPick> picks = [];
        var page = Page(config, picks);
        var shut = Font(Laid(page));
        Assert.Equal(TextStyle.KarpoFamily, shut.Row.Value);
        Assert.Equal([PanelRowKind.Text, PanelRowKind.PickerBody, PanelRowKind.PickerBody], shut.Row.Rows.Select(row => row.Kind));
        var weight = shut.Row.Rows[2];
        Assert.True(weight.Item.Beside);
        Assert.Equal(S.SettingsAppearanceStylesWeight, weight.Text);

        var layout = Laid(page, shut.Row.Key);
        Assert.DoesNotContain(Lines(layout), each => each.Row.Text == S.SettingsAppearanceStylesWeight && each.Row.Kind != PanelRowKind.PickerBody);
        var cells = layout.PickerCells(layout.LineOf(weight.Key)!);
        Assert.Equal((PickerCellKind.Title, S.SettingsAppearanceStylesWeight), (cells[0].Kind, cells[0].Item.Text));
        Assert.All(cells.Skip(1), cell => Assert.Equal(PickerCellKind.Option, cell.Kind));
        var bold = cells.Single(cell => cell.Item.Text == S.SettingsAppearanceStylesWeightBold).Item;
        Assert.Equal(new TextStyle(null, 700), bold.Face);
        // Its list no filter (the family's search is over the families alone).
        Assert.DoesNotContain(weight.Item.Children!, kid => kid.Control is FlyoutField);
        Assert.Contains(shut.Row.Item.Children!, kid => kid.Control is FlyoutField { Filters: true });
        var pick = OptionMenu.PickOf(picks, bold.Id)!;
        Assert.True(OptionMenu.ApplyRow(OptionRows.Find(Rows(config), pick.Key)!, pick));
        Assert.Equal("Segoe UI Variable Text/700", config.WornValues!.Widgets!.Font);
    }

    /// <summary>The font changed: its one reset, an icon button before what its head says, named "Reset Font to default".</summary>
    [Fact]
    public void AChangedFontHasOneResetBesideItsHead()
    {
        var config = MenuCorpus.Fresh();
        Assert.Null(Font(Laid(Page(config))).Row.Pair);
        Assert.True(Looks.WriteFont(Looks.Section(config, LookPart.Widgets), new TextStyle("Georgia", 700)));
        var layout = Laid(Page(config));
        var line = Font(layout);
        var reset = line.Row.Button!;
        Assert.Equal((PanelRowKind.Action, S.SettingsLookReset, true), (reset.Kind, reset.Text, reset.Item.Subtle));
        Assert.Same(reset, line.Row.Pair);
        var controls = layout.Controls(line);
        Assert.Equal([PanelHitKind.Act, PanelHitKind.Head], controls.Select(each => each.Hit.Kind));
        Assert.Equal((layout.P(PanelMetrics.Control), layout.PickerValueLeft(line) - layout.P(PanelMetrics.Label)), (controls[0].Rect.Width, controls[0].Rect.Right));
        Assert.Equal(S.SettingsLookResetValue(S.SettingsAppearanceStylesFont), layout.TipOf(controls[0].Hit));
        Assert.Same(line.Row, layout.HolderOf(reset.Key));
        Assert.Equal("Georgia", line.Row.Value);
    }
}
