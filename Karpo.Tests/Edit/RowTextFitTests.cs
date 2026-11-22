// edit, §604: no row of the settings panel draws its words over each other or past its card. A row is as tall as its words,
// measured: its name one line, its second line (a description, a group's sentence, a mixed row's split, a note's words)
// wrapped at the room its row really has, every line of it shown — Karpo Pro's "What counts as a widget" four lines of it
// under its name, not four lines drawn in a row laid out for two. Held over every page the panel shows (Karpo, Accounts,
// Layouts, Taskbar, Appearance, a folder's, a widget's, several widgets', the add page, every kind's page),
// every expander open and every tab chosen, at every scale, measured with SettingsPanelTests.Words (words wrapped as a
// stand-in for DirectWrite).

using System.Numerics;
using Karpo.Dock;
using Karpo.Tests.Menus;

namespace Karpo.Tests.Edit;

public sealed class RowTextFitTests
{
    public static TheoryData<float> Scales => [1f, 1.25f, 1.5f, 1.75f, 2f, 2.5f];

    /// <summary>Every page the panel shows, as the dock builds it, from the corpus's fresh config.</summary>
    private static IEnumerable<PanelPageView> Pages()
    {
        foreach (var page in SettingsPanelTests.Content(widget: true).Pages)
        {
            yield return page;
        }
        foreach (var page in SettingsPanelTests.Content(several: true).Pages.Where(page => page.Page == PanelPage.Widget))
        {
            yield return page;
        }
        yield return SettingsPanelTests.AddPage();
        var place = new OptionPlace(MenuCorpus.Fresh(), Dark: true);
        foreach (var kind in Enum.GetValues<BarKind>())
        {
            var item = kind.New();
            yield return item is BarItem { Folder: not null } ? SettingsPanelTests.FolderPage(item, place) : WidgetPage(item, place);
        }
        foreach (var kind in Enum.GetValues<ListKind>())
        {
            yield return WidgetPage(kind.New(), place);
        }
    }

    private static PanelPageView WidgetPage(WidgetItem item, OptionPlace place) =>
        PanelGroups.Page(PanelPage.Widget, IslandEdits.Kind(item), item.Info?.Glyph ?? "", MenuCorpus.Arranged(item, place),
            header: PanelRows.HeaderOf([(item.Info?.Glyph ?? "", IslandEdits.Kind(item))], S.DockPanelPlaceBar));

    /// <summary>What stands open on <paramref name="page"/>: every expander and every mixed row's own, and each strip's tab <paramref name="tab"/>.</summary>
    private static HashSet<string> Opened(PanelPageView page, int tab)
    {
        var all = PanelRows.All(page.Rows).ToList();
        HashSet<string> open = new(StringComparer.Ordinal);
        foreach (var row in all)
        {
            if (row.Kind == PanelRowKind.Expander)
            {
                open.Add(row.Key);
            }
            if (row.Theirs.Count > 0)
            {
                open.Add(PanelGroups.EachOf(row.Key));
            }
            if (row.Kind == PanelRowKind.Tabs && tab < row.Rows.Count)
            {
                open.Add(row.Rows[tab].Key);
            }
        }
        return open;
    }

    /// <summary>
    /// §604: every line of every page holds its words: its name one line (no line break in it), its second line's every line
    /// wrapped at the room it is drawn in, the two at least 8 DIP in from the line's top and foot; a heading its caption's.
    /// </summary>
    [Theory]
    [MemberData(nameof(Scales))]
    public void NoRowsWordsRunPastItsLine(float scale)
    {
        var words = new SettingsPanelTests.Words();
        float P(float dip) => MathF.Round(dip * scale, MidpointRounding.AwayFromZero);
        List<string> failures = [];
        foreach (var page in Pages())
        {
            var tabs = PanelRows.All(page.Rows).Where(row => row.Kind == PanelRowKind.Tabs).Select(row => row.Rows.Count).DefaultIfEmpty(1).Max();
            for (var tab = 0; tab < tabs; tab++)
            {
                var layout = new PanelLayout(words, scale, new PanelInput(0, new SettingsContent([page]), page.Page, Opened(page, tab)));
                foreach (var block in layout.Blocks)
                {
                    if (block.Row.Kind == PanelRowKind.Heading)
                    {
                        var need = P(PanelMetrics.Heading) + (block.Row.Detail is { } caption ? words.Tall(caption, P(PanelMetrics.Caption), block.Rect.Width) : 0);
                        if (need > block.Rect.Height)
                        {
                            failures.Add($"{page.Page} {page.Title} @{scale}: heading \"{block.Row.Text}\" needs {need} px, has {block.Rect.Height}");
                        }
                        continue;
                    }
                    foreach (var line in block.Lines.Where(line => line.Row.Kind is not (PanelRowKind.Tabs or PanelRowKind.Preview) && !line.Row.Foot))
                    {
                        var row = line.Row;
                        var name = row.Kind is PanelRowKind.Action or PanelRowKind.Words || row.Adds ? row.Detail ?? "" : row.Text;
                        if (name.Contains('\n', StringComparison.Ordinal))
                        {
                            failures.Add($"{page.Page} {page.Title} @{scale}: \"{row.Text}\"'s one line breaks");
                        }
                        if (row.Second is not { } second)
                        {
                            continue;
                        }
                        var need = P(PanelMetrics.BodyLine) + words.Tall(second, P(PanelMetrics.Caption), layout.NameRoom(line)) + (2 * P(PanelMetrics.Gap));
                        if (need > line.Rect.Height)
                        {
                            failures.Add($"{page.Page} {page.Title} @{scale}: \"{row.Text}\" needs {need} px, has {line.Rect.Height}");
                        }
                    }
                }
            }
        }
        Assert.True(failures.Count == 0, $"{failures.Count} rows overdrawn:\n{string.Join("\n", failures.Distinct().Take(40))}");
    }

    /// <summary>
    /// §604: Karpo Pro's "What counts as a widget" on the Karpo page: its name over its four lines, its card as tall as they
    /// are (12 + 20 + 4 × 16 + 12 DIP where each fits its room), the cards after it pushed down by as much.
    /// </summary>
    [Fact]
    public void WhatCountsAsAWidgetIsAsTallAsItsFourLines()
    {
        const float scale = 1.5f;
        float P(float dip) => MathF.Round(dip * scale, MidpointRounding.AwayFromZero);
        var words = new SettingsPanelTests.Words();
        var content = SettingsPanelTests.Content();
        var layout = new PanelLayout(words, scale, new PanelInput(0, content, PanelPage.Karpo, Opened(content.Of(PanelPage.Karpo)!, 0)), PanelLayout.SizeOf(scale, 4000));
        var line = layout.Blocks.SelectMany(block => block.Lines).Single(line => line.Row.Text == S.SettingsLicenseCountsHeader);
        var said = line.Row.Second!;
        Assert.Equal(4, said.Split('\n').Length);
        var room = layout.NameRoom(line);
        Assert.All(said.Split('\n'), each => Assert.True(words.Width(each, P(PanelMetrics.Caption)) <= room, $"\"{each}\" wraps at {room} px"));
        Assert.Equal(P(PanelMetrics.Label) + P(PanelMetrics.BodyLine) + (4 * P(PanelMetrics.CaptionLine)) + P(PanelMetrics.Label), line.Rect.Height);
    }
}
