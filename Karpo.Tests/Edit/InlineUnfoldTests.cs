// edit, §682 (the person, 2026-11-12): "the color picker in the accounts should also be inlined somehow", "public holidays should
// also be moved to the inline dropdowns", "icon selector should show the icons way denser, without title", "the inline color
// selector ... should be more in a column layout": a button's picker and a dropdown beside another's controls unfold under their
// line; the icon gallery a dense grid of glyphs its search filters; a colour's swatches beside its mixer where there is room.

using System.Numerics;
using Karpo.Dock;

namespace Karpo.Tests.Edit;

public sealed class InlineUnfoldTests
{
    private static List<FlyoutItem> Swatches(uint first) =>
    [
        // §690: as a picker has them: the recent line (here one), a rule, the presets.
        new FlyoutItem(first + 20, "Recent", new FlyoutTile(new Argb(255, 1, 2, 3), CellDip: 0), Checked: false),
        new FlyoutItem(0, "", Separator: true),
        .. Enumerable.Range(0, 6).Select(at => new FlyoutItem(first + (uint)at, "Colour " + at, new FlyoutTile(new Argb(255, (byte)(at * 40), 0, 0), CellDip: 0), Checked: at == 0)),
        new FlyoutItem(first + 10, "", Control: new FlyoutMix(MixPart.Spectrum, new Argb(255, 0, 0, 0))),
        new FlyoutItem(first + 11, "", Control: new FlyoutMix(MixPart.Hue, new Argb(255, 0, 0, 0))),
    ];

    private static PanelLayout Laid(PanelPageView page, IEnumerable<string> open, float wideDip = 580, Dictionary<string, string>? typed = null, Dictionary<string, float>? scrolls = null) =>
        new(new SettingsPanelTests.Words(), 1.5f, new PanelInput(0, new SettingsContent([page]), page.Page, new HashSet<string>(open), Searches: typed, Scrolls: scrolls), new Vector2(wideDip * 1.5f, 4000));

    /// <summary>
    /// §682 (the person, 2026-11-12: "the inline selectors should be limited to a maximum height after which they have to
    /// scroll"): a long list's body no taller than the most, the rest scrolled to; scrolled to its end, its last shows, its
    /// first gone; scrolled past its end, at its end.
    /// </summary>
    [Fact]
    public void ALongPickerScrollsInsideItsHeight()
    {
        var choices = Enumerable.Range(0, 80).Select(at => new FlyoutItem(200 + (uint)at, "Choice " + at, Checked: at == 0)).ToList();
        var picker = new FlyoutItem(199, "Many", Children: choices, PickOne: true);
        var page = PanelRows.Page(PanelPage.Widget, "", "", [picker]);
        var row = page.Rows[0];
        var body = row.Rows.Single(each => each.Kind == PanelRowKind.PickerBody).Key;
        var top = Laid(page, [row.Key]);
        var line = top.LineOf(body)!;
        Assert.True(line.Rect.Height <= top.P(PickerMetrics.MostTall + PickerMetrics.Pad) + 0.5f);
        var over = top.PickerOverflow(line);
        Assert.True(over > 0);
        Assert.Contains(top.PickerCells(line), cell => cell.Item.Text == "Choice 0");
        Assert.DoesNotContain(top.PickerCells(line), cell => cell.Item.Text == "Choice 79");
        var end = Laid(page, [row.Key], scrolls: new() { [body] = 1e6f });
        var endLine = end.LineOf(body)!;
        Assert.Equal(over, end.PickerScroll(endLine));
        Assert.Contains(end.PickerCells(endLine), cell => cell.Item.Text == "Choice 79");
        Assert.DoesNotContain(end.PickerCells(endLine), cell => cell.Item.Text == "Choice 0");
        Assert.All(end.PickerCells(endLine), cell => Assert.True(cell.Rect.Bottom > endLine.Rect.Y && cell.Rect.Y < end.PickerBand(endLine).Bottom));
        // The page under it never moves as it scrolls.
        Assert.Equal(line.Rect, endLine.Rect);
    }

    /// <summary>A row's colour button (an account's calendar's) its picker: folded, nothing under the line; unfolded, its body there, swatches and mixer.</summary>
    [Fact]
    public void AButtonsColourUnfoldsUnderItsLine()
    {
        var tool = new FlyoutTool(40, "", "Colour", Level: Swatches(50));
        var item = new FlyoutItem(30, "Work", Control: new FlyoutControl.Tools([tool]));
        var page = PanelRows.Page(PanelPage.Accounts, "", "", [item]);
        var row = page.Rows[0];
        var folded = Assert.Single(row.Folded);
        Assert.Equal(PanelRow.FoldedKey(row.Key, 40), folded.Key);
        Assert.True(PanelPickers.Inline(folded));
        var body = folded.Rows.Single(each => each.Kind == PanelRowKind.PickerBody).Key;
        Assert.Null(Laid(page, []).LineOf(body));
        var open = Laid(page, [folded.Key]);
        var line = open.LineOf(body)!;
        Assert.True(line.Rect.Y >= open.LineOf(row.Key)!.Rect.Bottom);
        var cells = open.PickerCells(line);
        Assert.Equal(7, cells.Count(cell => cell.Kind == PickerCellKind.Swatch));
        Assert.Equal(2, cells.Count(cell => cell.Kind == PickerCellKind.Mix));
    }

    /// <summary>
    /// §690: the recent line its own, however few in it; the presets from the line under it, where they always stand.
    /// </summary>
    [Fact]
    public void ThePresetsStandUnderTheRecentLineHoweverFewItHolds()
    {
        var picker = new FlyoutItem(60, "Background", new FlyoutTile(new Argb(255, 1, 2, 3), Cell: false), Children: Swatches(70), PickOne: true);
        var page = PanelRows.Page(PanelPage.Appearance, "", "", [picker]);
        var row = page.Rows[0];
        var body = row.Rows.Single(each => each.Kind == PanelRowKind.PickerBody).Key;
        var laid = Laid(page, [row.Key]);
        var cells = laid.PickerCells(laid.LineOf(body)!).Where(cell => cell.Kind == PickerCellKind.Swatch).ToList();
        var recent = cells.Single(cell => cell.Item.Text == "Recent");
        var presets = cells.Where(cell => cell.Item.Text != "Recent").ToList();
        Assert.True(presets.Min(cell => cell.Rect.Y) > recent.Rect.Y);
        Assert.Equal(recent.Rect.X, presets[0].Rect.X);
    }

    /// <summary>Wide enough, the mixer stands beside the swatches, its top theirs; narrow, under them.</summary>
    [Fact]
    public void AColoursMixerStandsBesideItsSwatchesWhereThereIsRoom()
    {
        var picker = new FlyoutItem(60, "Background", new FlyoutTile(new Argb(255, 1, 2, 3), Cell: false), Children: Swatches(70), PickOne: true);
        var page = PanelRows.Page(PanelPage.Appearance, "", "", [picker]);
        var row = page.Rows[0];
        Assert.Equal(PanelRowKind.Picker, row.Kind);
        var body = row.Rows.Single(each => each.Kind == PanelRowKind.PickerBody).Key;
        List<PickerCell> Cells(float wide)
        {
            var laid = Laid(page, [row.Key], wide);
            return laid.PickerCells(laid.LineOf(body)!);
        }
        var wideCells = Cells(580);
        var swatches = wideCells.Where(cell => cell.Kind == PickerCellKind.Swatch).ToList();
        var spectrum = wideCells.First(cell => cell.Kind == PickerCellKind.Mix);
        Assert.True(spectrum.Rect.X > swatches.Max(cell => cell.Rect.Right));
        Assert.Equal(swatches.Min(cell => cell.Rect.Y), spectrum.Rect.Y);
        var narrow = Cells(300);
        Assert.True(narrow.First(cell => cell.Kind == PickerCellKind.Mix).Rect.Y > narrow.Where(cell => cell.Kind == PickerCellKind.Swatch).Max(cell => cell.Rect.Bottom));
    }

    /// <summary>The icon gallery: a square of its glyph each, no names, many to a line; what is typed in its search keeps those it matches.</summary>
    [Fact]
    public void TheIconGalleryIsADenseGridItsSearchFilters()
    {
        string[] names = ["Folder", "Document", "Music", "Video", "Photo", "Mail", "Star", "Heart", "Home", "Work", "Cloud", "Globe", "Bus", "Car"];
        var search = new FlyoutItem(81, "", Control: new FlyoutField("", "Search icons", Filters: true));
        var icons = names.Select((name, at) => new FlyoutItem(90 + (uint)at, name, new FlyoutTile(Code: "", CellDip: 32), Checked: at == 0));
        var picker = new FlyoutItem(80, "Icon", Children: [search, .. icons], PickOne: true);
        var page = PanelRows.Page(PanelPage.Layouts, "", "", [picker]);
        var row = page.Rows[0];
        var body = row.Rows.Single(each => each.Kind == PanelRowKind.PickerBody).Key;
        var laid = Laid(page, [row.Key]);
        var cells = laid.PickerCells(laid.LineOf(body)!);
        Assert.All(cells, cell => Assert.Equal(PickerCellKind.Glyph, cell.Kind));
        Assert.Equal(names.Length, cells.Count);
        Assert.True(cells.Count(cell => cell.Rect.Y == cells[0].Rect.Y) >= 8, "not dense");
        Assert.All(cells, cell => Assert.Equal(cell.Rect.Width, cell.Rect.Height));
        var found = Laid(page, [row.Key], typed: new Dictionary<string, string> { [PanelPickers.SearchOf(body)] = "mu" });
        var music = found.PickerCells(found.LineOf(body)!);
        Assert.Equal(["Music"], music.Select(cell => cell.Item.Text));
        Assert.Equal(92, (int)music[0].Hit.Id);
    }
}
