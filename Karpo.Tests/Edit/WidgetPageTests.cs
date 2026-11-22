// edit, §529: a widget's and a folder's page of the settings panel (docs\widgets.md §529; the person, 2026-11-07, of the
// flow study: "header of the 3rd one, but the main body content of the second one"), apart from any window:
//
//  - **The header card**: the widget's icon on a tile (several fanned out), its name ("3 widgets", "3 × Clock"), where
//    it stands, Cut, Copy and Remove as icon buttons at its end, each saying its name in a tooltip; §550 nothing more
//    for widgets of several kinds.
//  - **§645 plain sections**: What it shows and Size, a heading each, every row a card, nothing at the page's top to fold;
//    a menu's level an expander in What it shows, a folder's faces its one group.

using System.Numerics;
using Karpo.Dock;
using Karpo.Tests.Menus;

namespace Karpo.Tests.Edit;

public sealed class WidgetPageTests
{
    private const float Scale = 1.5f;

    private static float Px(float dip) => MathF.Round(dip * Scale, MidpointRounding.AwayFromZero);

    private static readonly Vector2 Size = PanelLayout.SizeOf(Scale, 1400);

    private static PanelLayout Laid(SettingsContent content, PanelPage page, IReadOnlySet<string>? open = null) =>
        new(new SettingsPanelTests.Words(), Scale, new PanelInput(0, content, page, open), Size);

    // --- The header card -----------------------------------------------------------------------------

    [Fact]
    public void AWidgetsPageOpensWithItsHeaderCard()
    {
        var layout = Laid(SettingsPanelTests.Content(widget: true), PanelPage.Widget);
        var page = layout.Shown!;
        Assert.Equal(IslandEdits.Kind(ListKind.Calendar.New()), page.Header!.Name);
        Assert.Equal(S.DockPanelPlaceBarFolder, page.Header.Place);
        Assert.Equal([WidgetInfos.Calendar.Glyph], page.Header.Glyphs);
        // The card: as wide as the page's cards, its tile 48 DIP, 16 in.
        var card = layout.HeaderCard;
        Assert.False(card.IsEmpty);
        Assert.Equal(Px(PanelMetrics.HeaderTall), card.Height);
        Assert.Equal((Px(48), Px(48)), (layout.HeaderTile.Width, layout.HeaderTile.Height));
        Assert.Equal(card.X + Px(16), layout.HeaderTile.X);
        Assert.True(layout.Blocks[0].Rect.Y >= card.Bottom + Px(PanelMetrics.Group), "the first card stands under the header");
        // Cut, Copy and Remove: 32 DIP icon buttons, 4 apart, inside the card at its end, each its name in a tooltip.
        Assert.Equal([S.DockMenuCut, S.DockMenuCopy, S.DockMenuRemove], layout.Actions.Select(each => each.Item.Text));
        foreach (var (item, rect) in layout.Actions)
        {
            Assert.Equal((Px(32), Px(32)), (rect.Width, rect.Height));
            Assert.True(rect.X > layout.Title.X && rect.Right <= card.Right - Px(16) + 1 && rect.Y >= card.Y && rect.Bottom <= card.Bottom);
            Assert.Equal(item.Text, layout.TipOf(new PanelHit(PanelHitKind.TitleAct, item.Id, Key: item.Text)));
        }
        Assert.Equal(Px(4), layout.Actions[1].Rect.X - layout.Actions[0].Rect.Right);
        // The name ends before the buttons start.
        Assert.True(layout.Title.Right <= layout.Actions[0].Rect.X);
        // A page with no header keeps its title row, and its buttons no tooltip.
        var karpo = Laid(SettingsPanelTests.Content(), PanelPage.Karpo);
        Assert.True(karpo.HeaderCard.IsEmpty);
    }

    [Fact]
    public void SeveralWidgetsAreOneHeader()
    {
        var clock = (WidgetInfos.Clock.Glyph, "Clock");
        var one = PanelRows.HeaderOf([clock], S.DockPanelPlaceBar);
        Assert.Equal("Clock", one.Name);
        // Of one kind: "3 × Clock".
        var clocks = PanelRows.HeaderOf([clock, clock, clock], S.DockPanelPlaceBar);
        Assert.Equal(S.DockEditSelectedKind(count: 3, kind: "Clock"), clocks.Name);
        Assert.Equal(3, clocks.Glyphs.Count);
        // Of several kinds: "3 widgets".
        var kinds = PanelRows.HeaderOf([clock, (OptionGlyphs.Device, "Weather"), (OptionGlyphs.Pin, "Pin")], S.DockPanelPlaceBar);
        Assert.Equal(S.WidgetFolderWidgets(3), kinds.Name);
        var page = new PanelPageView(PanelPage.Widget, "", "", [], []) { Header = kinds };

        var layout = Laid(new SettingsContent([page]), PanelPage.Widget);
        // Fanned: three icons, a step apart.
        Assert.Equal(Px(48) + (2 * Px(PanelMetrics.FanStep)), layout.HeaderTile.Width);
        // §550: no line of buttons selecting each alone under it: the card its header's height, nothing in it to press or
        // to name but its own buttons (none here).
        Assert.Equal(Px(PanelMetrics.HeaderTall), layout.HeaderCard.Height);
        var card = layout.OnPanel(layout.HeaderCard);
        Assert.DoesNotContain(layout.Stops().Where(stop => !PanelLayout.OnBar(stop.Kind)), stop => layout.RectOf(stop) is { IsEmpty: false } at && card.Contains(at.Center.X, at.Center.Y));
        Assert.DoesNotContain(DeckUia.Things(layout, bar: false), thing => thing.Name is "Clock" or "Weather" or "Pin");
    }

    // --- A row under the choice it follows --------------------------------------------------------------

    /// <summary>A calendar of the corpus, its menu's rows as the panel reads them.</summary>
    private static PanelPageView Calendar(Action<WidgetItem>? set = null)
    {
        var calendar = ListKind.Calendar.New();
        set?.Invoke(calendar);
        var place = new OptionPlace(MenuCorpus.Fresh(), Dark: true);
        return PanelRows.Page(PanelPage.Widget, "", "", MenuCorpus.Arranged(calendar, place));
    }

    [Fact]
    public void ARowStandsUnderTheChoiceItFollows()
    {
        // The model says it: an agenda's Days ahead and a clock's seconds follow their style.
        var calendar = ListKind.Calendar.New();
        var rows = OptionRows.Of(calendar);
        Assert.Equal(OptionRows.Style, rows.Single(row => row.Key == OptionRows.CalendarDaysAhead).Under);
        Assert.Equal(OptionRows.Style, OptionRows.Of(ListKind.Clock.New()).Single(row => row.Key == OptionRows.ClockSeconds).Under);
        // The menu row carries it; the panel stands it under the Style row, whichever came first.
        var page = Calendar();
        var all = PanelRows.All(page.Rows).ToList();
        var style = all.Single(row => row.Item.Setting == OptionRows.Style && row.Kind != PanelRowKind.PickerBody);
        Assert.Contains(style.Nested, row => row.Item.Setting == OptionRows.CalendarDaysAhead);
        Assert.DoesNotContain(page.Rows, row => row.Item.Setting == OptionRows.CalendarDaysAhead);
        // A line's rows under its Height, in a folder.
        var height = all.Single(row => row.Item.Setting == LineHeightRows.HeightSetting && row.Kind != PanelRowKind.PickerBody);
        Assert.Equal([LineHeightRows.RowsSetting], height.Nested.Select(row => row.Item.Setting));
        // Nothing is lost: every row once.
        Assert.Equal(all.Count, all.Select(row => row.Key).Distinct().Count());
    }

    /// <summary>
    /// §660 (the person, 2026-11-10: "when fill is selected as height there should not be a height property anymore as it is
    /// ignored"): on the page, the Height's rows under it only for the modes that use them — Max rows under Automatic, Rows
    /// under Fixed —, nothing under Fill; the rows kept, and back under Automatic or Fixed at what they were.
    /// </summary>
    [Fact]
    public void UnderFillTheHeightHasNoRows()
    {
        PanelRow Height(LineHeight height) =>
            PanelRows.All(Calendar(calendar => calendar.Height = height).Rows).Single(row => row.Item.Setting == LineHeightRows.HeightSetting && row.Kind != PanelRowKind.PickerBody);
        PanelRow? Rows(LineHeight height) => PanelRows.All(Calendar(calendar => calendar.Height = height).Rows).SingleOrDefault(row => row.Item.Setting == LineHeightRows.RowsSetting && row.Kind != PanelRowKind.PickerBody);

        var fill = LineHeight.Exactly(13).As(LineHeightMode.Fill);
        Assert.Empty(Height(fill).Nested);
        Assert.Null(Rows(fill));
        foreach (var (height, title) in new[] { (LineHeight.Automatic(13), "Max rows"), (LineHeight.Exactly(13), "Rows") })
        {
            Assert.Equal([LineHeightRows.RowsSetting], Height(height).Nested.Select(row => row.Item.Setting));
            Assert.Equal((title, "6½ rows"), (Rows(height)!.Text, Rows(height)!.Stepper!.Value));
        }
        // The rows survive Fill: the line keeps them, and Fixed picked again shows them.
        var calendar = ListKind.Calendar.New();
        calendar.Height = LineHeight.Exactly(13);
        Assert.True(LineHeightRows.Resize(calendar, calendar.Height.Value.As(LineHeightMode.Fill)));
        Assert.Equal(13, calendar.Height!.Value.Cells);
        Assert.True(LineHeightRows.Resize(calendar, calendar.Height.Value.As(LineHeightMode.Fixed)));
        Assert.Equal("6½ rows", Rows(calendar.Height!.Value)!.Stepper!.Value);
    }

    [Fact]
    public void ARowWhoseChoiceIsNotThereStaysWhereItIs()
    {
        FlyoutItem Row(string text, string? setting, string? under) => new(1, text) { Setting = setting, Under = under };
        List<PanelRow> rows = [.. new[] { Row("A", "a", null), Row("B", "b", "nowhere"), Row("C", "c", "a"), Row("D", "d", "a") }.Select(item => PanelRows.One(item, "p")!)];
        var nested = PanelRows.Nest(rows);
        Assert.Equal(["A", "B"], nested.Select(row => row.Text));
        Assert.Equal(["C", "D"], nested[0].Nested.Select(row => row.Text));
    }

    /// <summary>
    /// §588, kept by §594 (the person, 2026-11-09: "i dont like that the public holidays region configurations have a different
    /// (darker) background"; "i basically just did not like the third level"): a row nested under a choice is a row of its
    /// choice's card as any, right after it, as far in as it, ruled off by the card's divider as every row: no step in, no well
    /// (§529 to §587).
    /// </summary>
    [Fact]
    public void ANestedRowIsAPlainRowOfItsChoicesCardRightAfterIt()
    {
        var content = new SettingsContent([Calendar()]);
        var layout = Laid(content, PanelPage.Widget);
        var lines = layout.Blocks.SelectMany(block => block.Lines).ToList();
        var style = lines.Single(line => line.Row.Item.Setting == OptionRows.Style);
        var days = lines.Single(line => line.Row.Item.Setting == OptionRows.CalendarDaysAhead);
        Assert.Equal(style.Rect.Bottom, days.Rect.Y);
        Assert.Same(layout.Blocks.Single(block => block.Lines.Contains(style)), layout.Blocks.Single(block => block.Lines.Contains(days)));
        Assert.True(days.Inner);
        Assert.Equal(layout.NameX(style), layout.NameX(days));
        Assert.Equal(layout.ControlEnd(style), layout.ControlEnd(days));
        // Its control is a number box still, reached by a click and the keys.
        Assert.Contains(layout.Controls(days), each => each.Hit.Kind == PanelHitKind.More);
        Assert.Contains(layout.Stops(), stop => stop.Key == days.Row.Key);
    }

    [Fact]
    public void ShowFromSaysWhatItDrawsOnAndTheWayToTheAccounts()
    {
        var calendar = ListKind.Calendar.New();
        var rows = OptionRows.Of(calendar, MenuCorpus.Sources, new OptionPlace(MenuCorpus.Fresh(), Dark: true));
        // One way to the accounts, no link of the holidays' own: a line of what it draws on, under Show from.
        Assert.DoesNotContain(rows, row => row is { Kind: OptionRowKind.Link, Link: OptionLink.Holidays });
        var words = Assert.Single(rows, row => row is { Kind: OptionRowKind.Link, Link: OptionLink.Accounts });
        Assert.Equal(S.DockWidgetMenuManageAccounts, words.Title);
        Assert.Equal("Ada Lovelace, Contoso, Switzerland", words.Summary);
        Assert.Equal(OptionRows.GroupKey(S.DockMenuShowFromTitle), words.Under);
        // The panel: a line of words, its link at its end, nested under Show from.
        var items = OptionMenu.Rows(rows, []);
        var page = PanelRows.Page(PanelPage.Widget, "", "", items);
        var showFrom = PanelRows.All(page.Rows).Single(row => row.Text == S.DockMenuShowFromTitle);
        var line = Assert.Single(showFrom.Nested);
        Assert.Equal(PanelRowKind.Words, line.Kind);
        var layout = Laid(new SettingsContent([page]), PanelPage.Widget);
        var laid = layout.LineOf(line.Key)!;
        var (hit, rect) = Assert.Single(layout.Controls(laid));
        Assert.Equal(PanelHitKind.Link, hit.Kind);
        Assert.True(rect.X > layout.NameX(laid) && rect.Right <= laid.Rect.Right - Px(16));
    }

    // --- Sections ------------------------------------------------------------------------------------------

    /// <summary>A widget's page in its sections, its menu as the corpus has it.</summary>
    private static PanelPageView Sectioned(WidgetItem item) =>
        PanelGroups.Page(PanelPage.Widget, "", item.Info?.Glyph ?? "", MenuCorpus.Arranged(item, new OptionPlace(MenuCorpus.Fresh(), Dark: true)));

    /// <summary>The rows of <paramref name="page"/>'s section headed <paramref name="title"/>, up to the next heading.</summary>
    private static List<PanelRow> SectionOf(PanelPageView page, string title) =>
        [.. page.Rows.SkipWhile(row => !(row.Kind == PanelRowKind.Heading && row.Text == title)).Skip(1).TakeWhile(row => row.Kind != PanelRowKind.Heading)];

    /// <summary>
    /// §645 (the person, 2026-11-09: "the widget settings should have normal sections like the other settings pages not just the
    /// expanding sections. for all widgets"): a calendar's page is two plain sections, What it shows and Size, each a heading and
    /// its rows, every row a card of its own on the page, always open; no group, nothing at the page's top to fold. Its menu's
    /// level (Show from) an expander in What it shows still, shut.
    /// </summary>
    [Fact]
    public void AWidgetsPageIsPlainSectionsEachRowACard()
    {
        var page = Sectioned(ListKind.Calendar.New());
        Assert.DoesNotContain(page.Rows, row => row.Kind == PanelRowKind.Group);
        // §567: no Look section: one look, Karpo's Appearance.
        Assert.Equal([S.DockPanelGroupShows, S.DockEditSizeTitle], page.Rows.Where(row => row.Kind == PanelRowKind.Heading).Select(row => row.Text));
        Assert.Equal(PanelRowKind.Heading, page.Rows[0].Kind);
        Assert.Contains(SectionOf(page, S.DockPanelGroupShows), row => row.Item.Setting == OptionRows.Style);
        Assert.Equal([LineHeightRows.HeightSetting], SectionOf(page, S.DockEditSizeTitle).Select(row => row.Item.Setting));
        var showFrom = Assert.Single(page.Rows, row => row.Text == S.DockMenuShowFromTitle);
        Assert.Equal(PanelRowKind.Expander, showFrom.Kind);
        // Every row of the flat page there, by its key (a popup, the search and a link find it so), once.
        var flat = PanelRows.Page(PanelPage.Widget, "", "", MenuCorpus.Arranged(ListKind.Calendar.New(), new OptionPlace(MenuCorpus.Fresh(), Dark: true)));
        var keys = PanelRows.All(page.Rows).Select(row => row.Key).ToList();
        Assert.Equal(keys.Count, keys.Distinct().Count());
        Assert.All(PanelRows.All(flat.Rows).Where(row => row.Kind != PanelRowKind.Heading), row => Assert.Contains(row.Key, keys));
        // Laid with nothing opened: each heading a block of its own, no line, never pressed; each row its own card under it, 4
        // apart, the heading 8 over its first; the Height's Max rows a row of its card (§594).
        var layout = Laid(new SettingsContent([page]), PanelPage.Widget);
        var blocks = layout.Blocks;
        var heading = blocks.Single(block => block.Row.Text == S.DockEditSizeTitle && block.Row.Kind == PanelRowKind.Heading);
        Assert.Empty(heading.Lines);
        Assert.DoesNotContain(layout.Stops(), stop => stop.Key == heading.Row.Key);
        var at = blocks.ToList().IndexOf(heading);
        var height = blocks[at + 1];
        Assert.Equal(heading.Rect.Bottom + Px(PanelMetrics.Gap), height.Rect.Y);
        Assert.Equal([LineHeightRows.HeightSetting, LineHeightRows.RowsSetting], height.Lines.Select(line => line.Row.Item.Setting));
        var shows = blocks.TakeWhile(block => block != heading).Skip(1).ToList();
        Assert.True(shows.Count > 1, "What it shows' rows each a card");
        Assert.All(shows.Zip(shows.Skip(1)), pair => Assert.Equal(pair.First.Rect.Bottom + Px(PanelMetrics.Tight), pair.Second.Rect.Y));
        Assert.Equal(shows[^1].Rect.Bottom + Px(PanelMetrics.Group), heading.Rect.Y);
    }

    /// <summary>
    /// §594 (the person, 2026-11-09: "the tray icons have two levels in their configuration so you have to actually expand to
    /// configure"), §645: a tray's page is its icons' section, its heading Tray icons, every icon's place a card of it in sight
    /// as the page opens, keyed as the level's rows were; nothing to expand.
    /// </summary>
    [Fact]
    public void ATraysIconsAreItsPagesSection()
    {
        var page = Sectioned(BarKind.Tray.New());
        Assert.Equal((PanelRowKind.Heading, S.SettingsTrayIconsHeader), (page.Rows[0].Kind, page.Rows[0].Text));
        var icons = page.Rows.Skip(1).ToList();
        Assert.Equal(["Microsoft OneDrive", "Steam"], icons.Select(row => row.Text));
        Assert.All(icons, row => Assert.Equal(PanelRowKind.Picker, row.Kind));
        var flat = PanelRows.Page(PanelPage.Widget, "", "", MenuCorpus.Arranged(BarKind.Tray.New(), new OptionPlace(MenuCorpus.Fresh(), Dark: true)));
        var level = Assert.Single(flat.Rows, row => row.Kind == PanelRowKind.Expander);
        Assert.Equal(level.Rows.Select(row => row.Key), icons.Select(row => row.Key));
        // The heading's key is never a row's, though it bears the level's name.
        Assert.NotEqual(level.Key, page.Rows[0].Key);
        var layout = Laid(new SettingsContent([page]), PanelPage.Widget);
        Assert.All(icons, row => Assert.Single(layout.Blocks, block => block.Lines.Any(line => line.Row.Key == row.Key)));
    }

    /// <summary>
    /// §645: a row of a section is found where it is: the search's and a link's way to it nothing to open (a section never
    /// folds); a row in a level (Show from) that level opened; the Size row of a weather keyed apart from its section's heading.
    /// </summary>
    [Fact]
    public void ALandingInASectionOpensNothingButItsLevel()
    {
        var page = Sectioned(ListKind.Calendar.New());
        var days = PanelRows.All(page.Rows).Single(row => row.Item.Setting == OptionRows.CalendarDaysAhead);
        Assert.Equal([], PanelRows.PathTo(page.Rows, days.Key));
        Assert.NotNull(Laid(new SettingsContent([page]), PanelPage.Widget).LineOf(days.Key));
        var showFrom = page.Rows.Single(row => row.Text == S.DockMenuShowFromTitle);
        var first = showFrom.Rows.First(row => row.Kind != PanelRowKind.Heading);
        Assert.Equal([showFrom.Key], PanelRows.PathTo(page.Rows, first.Key));
        Assert.Null(Laid(new SettingsContent([page]), PanelPage.Widget).LineOf(first.Key));
        var memory = new PanelMemory();
        memory.Open(PanelRows.PathTo(page.Rows, first.Key)!);
        Assert.NotNull(Laid(new SettingsContent([page]), PanelPage.Widget, memory.Opened).LineOf(first.Key));
        // A weather: its heading Size and its row Size two keys, the row's what a search or a popup finds.
        var weather = Sectioned(ListKind.WeatherNow.New());
        var size = weather.Rows.Single(row => row.Item.Setting == LineHeightRows.SizeSetting);
        var heading = weather.Rows.Single(row => row.Kind == PanelRowKind.Heading && row.Text == S.DockEditSizeTitle);
        Assert.NotEqual(size.Key, heading.Key);
        Assert.Equal(PanelGroups.HeadingOf(PanelPage.Widget, S.DockEditSizeTitle), heading.Key);
        Assert.NotNull(Laid(new SettingsContent([weather]), PanelPage.Widget).LineOf(size.Key));
    }

    /// <summary>§645: to a screen reader each section a group named for its heading, its rows' controls in it, in the page's order.</summary>
    [Fact]
    public void ASectionIsAGroupNamedForItsHeadingToAScreenReader()
    {
        var page = Sectioned(ListKind.Calendar.New());
        var layout = Laid(new SettingsContent([page]), PanelPage.Widget);
        var tree = DeckUia.Tree(layout, new TerraFX.Interop.Windows.RECT { right = 2000, bottom = 2000 }, PanelHit.None, bar: false);
        var groups = tree.Root.Children.Where(node => node.Role == UiaRole.Group).ToList();
        Assert.Equal([S.DockPanelGroupShows, S.DockEditSizeTitle], groups.Select(node => node.Name));
        // Size's: the Height's picker (§672: shut, its head) and its Max rows' number box.
        Assert.Contains(groups[1].Children, node => node.Name == S.DockEditHeightTitle && node.Pattern == UiaPattern.Expand);
        Assert.Contains(groups[1].Children, node => node.Role == UiaRole.Spinner);
        Assert.DoesNotContain(groups[0].Children, node => node.Role == UiaRole.Spinner && node.Name.Contains(S.DockEditHeightTitle, StringComparison.Ordinal));
        // No heading of a group's head under the page's: a widget's page has none.
        Assert.DoesNotContain(Nodes(tree.Root), node => node.Heading > 0);

        static IEnumerable<UiaNode> Nodes(UiaNode node) => node.Children.SelectMany(Nodes).Prepend(node);
    }

    /// <summary>§529, §552: what a face's group's head says it shows follows its rows' values: a clock's zone, its seconds.</summary>
    [Fact]
    public void WhatItShowsFollowsItsValues()
    {
        var clock = ListKind.Clock.New();
        var rows = PanelRows.All(Sectioned(clock).Rows).ToList();
        var zone = PanelSentences.Said(rows.Single(row => row.Item.Setting == OptionRows.ClockZone && row.Kind == PanelRowKind.Picker))!;
        Assert.Equal(S.DockPanelClockTime(zone), PanelSentences.Shows(rows));
        (clock.Clock ??= new ClockOptions()).Seconds = true;
        Assert.Equal(S.DockPanelClockSeconds(zone), PanelSentences.Shows([.. PanelRows.All(Sectioned(clock).Rows)]));
        // A kind with nothing to say: nothing.
        Assert.Null(PanelSentences.Shows([.. PanelRows.All(Sectioned(ListKind.Battery.New()).Rows)]));
    }

    /// <summary>§604: a face's sentence longer than its room wraps, every line of it shown, its head as tall as they are; no tooltip says it again.</summary>
    [Fact]
    public void ASentenceTooLongForItsLineWrapsWhole()
    {
        var long1 = new PanelRow(PanelRowKind.Group, new FlyoutItem(0, "Group"), PanelGroups.KeyOf(PanelPage.Folder, "Group")) { Sentence = string.Join(' ', Enumerable.Repeat("xxxxxxxxx", 40)), Rows = [] };
        var short1 = long1 with { Key = PanelGroups.KeyOf(PanelPage.Folder, "Other"), Sentence = "Short" };
        var layout = Laid(new SettingsContent([new PanelPageView(PanelPage.Folder, "", "", [long1, short1], [])]), PanelPage.Folder);
        Assert.Null(layout.TipOf(new PanelHit(PanelHitKind.Head, Key: long1.Key)));
        Assert.Null(layout.TipOf(new PanelHit(PanelHitKind.Head, Key: short1.Key)));
        var line = layout.LineOf(long1.Key)!;
        var lines = new SettingsPanelTests.Words().Tall(long1.Sentence, Px(PanelMetrics.Caption), layout.NameRoom(line)) / Px(PanelMetrics.CaptionLine);
        Assert.True(lines > 1, $"{lines} lines");
        Assert.Equal(Px(12) + Px(20) + (lines * Px(16)) + Px(12), line.Rect.Height);
        Assert.Equal(Px(60), layout.LineOf(short1.Key)!.Rect.Height);
        // Its words where the layout measured them: the name over every line of the sentence, the two in the line's middle.
        var (name, second) = layout.WordsAt(line);
        Assert.Equal((line.Rect.Y + Px(12), Px(20), lines * Px(16)), (name.Y, name.Height, second.Height));
        Assert.Equal(name.Bottom, second.Y);
    }

    // --- A mixed value, each one's ----------------------------------------------------------------------

    [Fact]
    public void AMixedValueOpensIntoEachOnesOwn()
    {
        // Two clocks, the first's seconds on: the seconds mixed.
        var place = new OptionPlace(MenuCorpus.Fresh(), Dark: true);
        WidgetItem[] clocks = [ListKind.Clock.New(), ListKind.Clock.New()];
        (clocks[0].Clock ??= new ClockOptions()).Seconds = true;
        List<OptionTarget> targets = [.. clocks.Select(clock => new OptionTarget(clock, MenuCorpus.Sources, place))];
        var items = OptionMenu.Settings(targets, [], FolderSide.Face);
        var page = PanelGroups.Page(PanelPage.Widget, "", "", items);
        var seconds = PanelRows.All(page.Rows).Single(row => row.Item.Setting == OptionRows.ClockSeconds && row.Item.Mixed);
        // Shut it says how they split, each one's own under it, named.
        Assert.Equal(2, seconds.Theirs.Count);
        Assert.Equal([IslandEdits.Header(clocks[0]) + " (1)", IslandEdits.Header(clocks[1]) + " (2)"], seconds.Theirs.Select(row => row.Text));
        Assert.Equal(PanelSentences.SplitOf(seconds.Theirs), seconds.Split);
        Assert.Equal(S.DockPanelSplit(2, "A") + ", " + S.DockPanelSplit(1, "B"), PanelSentences.Splits(["A", "B", "A"]));
        Assert.Equal("A, B", PanelSentences.Splits(["A", "B"]));
        // Laid: its button before its control opens each one's under it, §588 (kept by §594) rows of its card right after it, as
        // far in as it; the search finds them, opening it.
        var memory = new PanelMemory();
        var shut = Laid(new SettingsContent([page]), PanelPage.Widget, memory.Opened);
        var line = shut.LineOf(seconds.Key)!;
        var each = shut.Controls(line).Single(control => control.Hit.Kind == PanelHitKind.Each).Hit;
        Assert.Null(shut.LineOf(seconds.Theirs[0].Key));
        Assert.Equal(S.DockPanelEach, shut.TipOf(each));
        memory.Toggle(PanelGroups.EachOf(each.Key));
        var open = Laid(new SettingsContent([page]), PanelPage.Widget, memory.Opened);
        var card = open.Blocks.Single(block => block.Lines.Any(each => each.Row.Key == seconds.Key)).Lines.ToList();
        var at = card.FindIndex(each => each.Row.Key == seconds.Key);
        Assert.Equal(seconds.Theirs.Select(own => own.Key), card.Skip(at + 1).Take(seconds.Theirs.Count).Select(each => each.Row.Key));
        Assert.All(card.Skip(at + 1).Take(seconds.Theirs.Count), own => Assert.Equal(open.NameX(open.LineOf(seconds.Key)!), open.NameX(own)));
        Assert.Contains(PanelGroups.EachOf(seconds.Key), PanelRows.PathTo(page.Rows, seconds.Theirs[0].Key)!);
    }

    // --- The folder's page --------------------------------------------------------------------------------

    [Fact]
    public void AFoldersPageHoldsItsFacesAlignmentAndLook()
    {
        var folder = BarKind.FolderList.New();
        folder.Folder!.Faces = [ClockFace.New(), FolderFace.Of(FolderFaces.IconId)];
        var place = new OptionPlace(MenuCorpus.Fresh(), Dark: true);
        var page = SettingsPanelTests.FolderPage(folder, place);
        Assert.Equal(S.CoreWidgetFolderListTitle, page.Header!.Name);
        Assert.Equal([S.DockMenuCut, S.DockMenuCopy, S.DockMenuRemove], page.Actions.Select(action => action.Text));
        // §552: each face a section of its own, in order; Add a face under them. §563: the faces and Add a face under the heading
        // Faces, the panel's rows under the heading Folder; §588, kept by §594 (the person, 2026-11-09: "the alignment is burried
        // two levels deep") Alignment a card straight under it, no group When it opens. §567: no Look.
        var titles = folder.Folder.Faces.Select(TheDockTitle).ToList();
        Assert.Equal([S.DockPanelSectionFaces, .. titles, S.SettingsFolderFacesAddHeader, S.DockPanelSectionFolder, S.DockFolderStyleTitle, S.CoreAlignTitle], page.Rows.Select(row => row.Text));
        Assert.Equal([0, titles.Count + 2], page.Rows.Select((row, at) => (row, at)).Where(each => each.row.Kind == PanelRowKind.Heading).Select(each => each.at));
        var clock = page.Rows[1];
        Assert.Equal(PanelRowKind.Group, clock.Kind);
        Assert.Equal([S.DockMenuRemove], clock.Tools.Select(tool => tool.Name));
        Assert.NotEmpty(clock.Rows);
        Assert.True(page.Rows[titles.Count + 1].Adds);
        // Alignment, its four choices its picker inline (§672).
        var alignment = page.Rows[^1];
        Assert.Equal(PanelRowKind.Picker, alignment.Kind);
        Assert.Equal(4, alignment.Item.Children!.Count(PanelPickers.Choice));
        // A face's settings are found and their popups reached by their keys, from the page's one menu.
        var nested = clock.Rows[0];
        var items = (List<FlyoutItem>)[.. Karpo.Dock.Dock.WidgetButtons(), .. OptionMenu.Settings(folder, [], MenuCorpus.Sources, place, FolderSide.Face, null), .. OptionMenu.Settings(folder, [], MenuCorpus.Sources, place, FolderSide.Panel, null)];
        Assert.NotNull(PanelRows.Find(items, PanelPage.Folder.ToString(), nested.Key));
        Assert.NotNull(PanelRows.Find(items, PanelPage.Folder.ToString(), alignment.Key));

        static string TheDockTitle(FolderFace face) => Karpo.Dock.Dock.TitleOf(face);
    }

    /// <summary>
    /// §552 (the person, 2026-11-07: "the folder faces should have their own sections"): a folder of one face and of three, each
    /// face a section of its own in the faces' order — its name and icon, its Remove at its head's end (greyed for the one
    /// face a folder keeps), its settings the very rows §529 nested under its row, by the same keys, each once —; Add a face
    /// right after them; a face's setting found under its section, and shown by opening it.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void EachFaceIsASectionOfItsOwnInTheFacesOrder(int count)
    {
        var folder = BarKind.FolderList.New();
        List<FolderFace> faces = [ClockFace.New(), FolderFace.Of(FolderFaces.WeatherId), FolderFace.Of(FolderFaces.IconId)];
        folder.Folder!.Faces = [.. faces.Take(count)];
        var place = new OptionPlace(MenuCorpus.Fresh(), Dark: true);
        var page = SettingsPanelTests.FolderPage(folder, place);
        // The face rows as the menu gives them, each its settings nested under it (§529's On the bar).
        List<FlyoutItem> items = [.. Karpo.Dock.Dock.WidgetButtons(), .. OptionMenu.Settings(folder, [], MenuCorpus.Sources, place, FolderSide.Face, null), .. OptionMenu.Settings(folder, [], MenuCorpus.Sources, place, FolderSide.Panel, null)];
        var rows = PanelRows.Page(PanelPage.Folder, "", "", items).Rows.Where(row => row.Item.Reorder == OptionMenu.FaceGroup).ToList();
        Assert.Equal(count, rows.Count);
        var all = PanelRows.All(page.Rows).Select(row => row.Key).ToList();
        for (var at = 0; at < count; at++)
        {
            var section = page.Rows[at + 1];
            Assert.Equal((PanelRowKind.Group, rows[at].Text, rows[at].Glyph), (section.Kind, section.Text, section.Glyph));
            Assert.True(PanelGroups.IsGroup(section.Key));
            var remove = Assert.Single(section.Tools);
            Assert.Equal((S.DockMenuRemove, rows[at].Tools[0].Id, count > 1), (remove.Name, remove.Id, remove.Enabled));
            // Its rows the face's settings, by their keys, in their order, nested no more under a row of the face; each once.
            Assert.Equal(PanelRows.All(rows[at].Nested).Select(row => row.Key), PanelRows.All(section.Rows).Select(row => row.Key));
            Assert.All(PanelRows.All(rows[at].Nested), row => Assert.Single(all, key => key == row.Key));
            Assert.DoesNotContain(page.Rows, row => row.Key == rows[at].Key);
        }
        // §563: under the heading Faces; Add a face after the last, a button of its own; then the heading Folder, §588 Alignment.
        Assert.Equal((PanelRowKind.Heading, S.DockPanelSectionFaces), (page.Rows[0].Kind, page.Rows[0].Text));
        Assert.True(page.Rows[count + 1] is { Adds: true, Kind: PanelRowKind.Choice } add && add.Text == S.SettingsFolderFacesAddHeader);
        Assert.Equal((PanelRowKind.Heading, S.DockPanelSectionFolder), (page.Rows[count + 2].Kind, page.Rows[count + 2].Text));
        Assert.Equal([S.DockFolderStyleTitle, S.CoreAlignTitle], page.Rows.Skip(count + 3).Take(2).Select(row => row.Text));
        // The clock's setting under the clock's section, which a landing opens; shut, its rows are not shown.
        var clock = page.Rows[1];
        var zone = clock.Rows.First(row => row.Kind != PanelRowKind.Heading);
        var path = PanelRows.PathTo(page.Rows, zone.Key)!;
        Assert.Equal([clock.Key], path);
        Assert.NotNull(Laid(new SettingsContent([page]), PanelPage.Folder).LineOf(zone.Key));
        var memory = new PanelMemory();
        memory.Toggle(clock.Key);
        Assert.Null(Laid(new SettingsContent([page]), PanelPage.Folder, memory.Opened).LineOf(zone.Key));
        memory.Open(path);
        Assert.NotNull(Laid(new SettingsContent([page]), PanelPage.Folder, memory.Opened).LineOf(zone.Key));
    }

    /// <summary>
    /// §552: a face's section's head: its Remove a button before its chevron, then the head itself; its name and sentence end
    /// before the button; to a screen reader a heading ("Remove · Clock" its button).
    /// </summary>
    [Fact]
    public void AFacesSectionHasItsRemoveAtItsHeadAndIsAHeading()
    {
        var folder = BarKind.FolderList.New();
        folder.Folder!.Faces = [ClockFace.New(), FolderFace.Of(FolderFaces.IconId)];
        var page = SettingsPanelTests.FolderPage(folder, new OptionPlace(MenuCorpus.Fresh(), Dark: true));
        var layout = Laid(new SettingsContent([page]), PanelPage.Folder);
        var clock = page.Rows[1];
        var head = layout.LineOf(clock.Key)!;
        var controls = layout.Controls(head);
        // §551: its handle first (the faces dragged into order), its Remove, the head.
        Assert.Equal([PanelHitKind.Handle, PanelHitKind.Tool, PanelHitKind.Head], controls.Select(each => each.Hit.Kind));
        var remove = controls[1].Rect;
        Assert.True(remove.Right <= layout.ControlEnd(head) - Px(PanelMetrics.ChevronRoom) + 1, "the Remove stands before the chevron");
        Assert.Equal(remove.X, layout.ControlLeft(head));
        // A click on the Remove is the Remove's, not the head's.
        Assert.Equal(PanelHitKind.Tool, layout.HitIn(new Vector2(layout.OnPanel(remove).X + 4, layout.OnPanel(remove).Y + 4)).Kind);
        var things = DeckUia.Things(layout, bar: false);
        Assert.Contains(things, thing => thing.Hit.Kind == PanelHitKind.Tool && thing.Name == S.DockMenuRemove + " · " + clock.Text);
        var tree = DeckUia.Tree(layout, new TerraFX.Interop.Windows.RECT { right = 2000, bottom = 2000 }, PanelHit.None, bar: false);
        var heads = Nodes(tree.Root).Where(node => node.Heading > 0).Select(node => node.Name).ToList();
        Assert.Equal([.. page.Rows.Where(row => row.Kind == PanelRowKind.Group).Select(row => row.Text)], heads);

        static IEnumerable<UiaNode> Nodes(UiaNode node) => node.Children.SelectMany(Nodes).Prepend(node);
    }

    /// <summary>
    /// §672, the person (2026-11-12): "the dropdown should also be integrated for the switches that switch between even only 2
    /// values". A row of segments, two or more, is an inline picker of its choices: each its glyph and its words, the one in
    /// force ticked and said in its head; a click on one sends that segment's own pick.
    /// </summary>
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void SegmentsAreAnInlinePickerOfTheirChoices(int count)
    {
        var choices = Enumerable.Range(0, count).Select(at => "C" + at).ToList();
        var row = new OptionRow("seg", OptionRowKind.Choice, "Align") { Choices = choices, ChoiceGlyphs = [.. choices.Select(_ => OptionGlyphs.AlignLeft)], Segmented = true, Chosen = 1, Set = _ => true };
        List<OptionPick> picks = [];
        var item = Assert.Single(OptionMenu.Rows([row], picks));
        var panel = PanelRows.One(item, "p")!;
        Assert.Equal((PanelRowKind.Picker, "C1"), (panel.Kind, panel.Value));
        var body = Assert.Single(panel.Rows, each => each.Kind == PanelRowKind.PickerBody);
        var kids = body.Item.Children!.Where(PanelPickers.Choice).ToList();
        Assert.Equal(choices, kids.Select(kid => kid.Text));
        Assert.Equal([.. Enumerable.Range(0, count).Select(at => at == 1)], kids.Select(kid => kid.Checked == true));
        Assert.All(kids, kid => Assert.Equal(new FlyoutIcon.Glyph(OptionGlyphs.AlignLeft), kid.Icon));
        Assert.Equal([.. Enumerable.Range(0, count)], kids.Select(kid => OptionMenu.PickOf(picks, kid.Id)!.Value));
    }

    // --- Every kind ---------------------------------------------------------------------------------------

    /// <summary>Every widget of the bar, every line of a folder, each a page as the dock builds it: a folder's its Folder page.</summary>
    public static TheoryData<string> Kinds => [.. Enum.GetValues<BarKind>().Select(kind => "bar " + kind), .. Enum.GetValues<ListKind>().Select(kind => "line " + kind)];

    [Theory]
    [MemberData(nameof(Kinds))]
    public void EveryKindsPageIsSectionsLosesNoRowAndLandsOnEach(string kind)
    {
        var place = new OptionPlace(MenuCorpus.Fresh(), Dark: true);
        WidgetItem item = kind.StartsWith("bar ", StringComparison.Ordinal) ? Enum.Parse<BarKind>(kind[4..]).New() : Enum.Parse<ListKind>(kind[5..]).New();
        if (item.Pin is { } pin)
        {
            pin.Target = @"C:\Users\Ada\Documents";
        }
        var folder = (item as BarItem)?.Folder is not null;
        List<FlyoutItem> items = folder
            ? [.. Karpo.Dock.Dock.WidgetButtons(), .. OptionMenu.Settings(item, [], MenuCorpus.Sources, place, FolderSide.Face, null), .. OptionMenu.Settings(item, [], MenuCorpus.Sources, place, FolderSide.Panel, null)]
            : MenuCorpus.Arranged(item, place);
        var page = folder
            ? SettingsPanelTests.FolderPage(item, place)
            : PanelGroups.Page(PanelPage.Widget, IslandEdits.Kind(item), item.Info?.Glyph ?? "", items, header: PanelRows.HeaderOf([(item.Info?.Glyph ?? "", IslandEdits.Kind(item))], S.DockPanelPlaceBar));
        Assert.NotNull(page.Header);
        Assert.Contains(S.DockMenuRemove, page.Actions.Select(action => action.Text));
        // §645: plain sections: a heading first, each section's rows cards of the page; no group at its top but a folder's faces
        // (§552: a list of the person's things, each its settings). §563 a folder's two headings, Faces and Folder.
        if (page.Rows.Count > 0)
        {
            Assert.Equal(PanelRowKind.Heading, page.Rows[0].Kind);
        }
        Assert.All(page.Rows.Where(row => row.Kind == PanelRowKind.Group), row => Assert.True(folder && PanelGroups.IsGroup(row.Key) && row.Item.Reorder == OptionMenu.FaceGroup, row.Key));
        var headings = page.Rows.Where(row => row.Kind == PanelRowKind.Heading).Select(row => row.Text).ToList();
        if (folder)
        {
            Assert.Equal([S.DockPanelSectionFaces, S.DockPanelSectionFolder], headings);
        }
        else
        {
            // What it shows (§594 a level alone: its name; §655 levels and ordered lists alone: theirs, a section each), then
            // Size where it has one, last; a kind with nothing to set none.
            Assert.True(headings.Count >= (page.Rows.Count == 0 ? 0 : 1), string.Join(", ", headings));
            Assert.Equal(headings.Count, headings.Distinct().Count());
            Assert.True(!headings.Contains(S.DockEditSizeTitle) || headings[^1] == S.DockEditSizeTitle, string.Join(", ", headings));
            Assert.True(headings.Count <= 2 || item.Info == WidgetInfos.WinSearch, string.Join(", ", headings));
        }
        // §594: no expander alone in a section: a level that is all What it shows holds is the section itself.
        Assert.Empty(page.Rows.Where((row, at) => row.Kind == PanelRowKind.Expander && row.Tools.Count == 0
            && (at == 0 || page.Rows[at - 1].Kind == PanelRowKind.Heading) && (at + 1 == page.Rows.Count || page.Rows[at + 1].Kind == PanelRowKind.Heading)));
        // No row lost, none twice: every row of its menu as the panel reads it is on the page, by its key; every setting of its own too.
        var all = PanelRows.All(page.Rows).ToList();
        var keys = all.Select(row => row.Key).ToList();
        Assert.Equal(keys.Count, keys.Distinct(StringComparer.Ordinal).Count());
        var flat = PanelRows.Page(folder ? PanelPage.Folder : PanelPage.Widget, "", "", items);
        // §552: a face's row is its group, its settings that group's rows. §594: a level alone in What it shows the section it
        // is, its rows the section's.
        Assert.All(PanelRows.All(flat.Rows).Where(row => row.Kind != PanelRowKind.Heading && row.Item.Reorder != OptionMenu.FaceGroup), row =>
            Assert.True(keys.Contains(row.Key) || (row.Kind == PanelRowKind.Expander && row.Rows.All(each => each.Kind == PanelRowKind.Heading || keys.Contains(each.Key))), row.Key));
        Assert.All(flat.Rows.Where(row => row.Item.Reorder == OptionMenu.FaceGroup), row => Assert.Contains(PanelGroups.FaceOf(PanelPage.Folder, row).Key, keys));
        var settings = all.Select(row => row.Item.Setting).OfType<string>().ToHashSet(StringComparer.Ordinal);
        var own = OptionRows.Of(item, MenuCorpus.Sources, place);
        // §594: a level that is its section has no row of its own: its rows are there, each its setting.
        Assert.All(own.Where(row => row.Within is null && row.Kind != OptionRowKind.Heading && !row.Ordered), row =>
            Assert.True(settings.Contains(row.Key) || (row.Group is { } group && settings.Contains(OptionRows.GroupKey(group))), row.Key));
        // Every row landed on (EditDeck.Land: its path opened) stands laid on the page; a section's never needs opening.
        foreach (var row in all.Where(row => row.Kind != PanelRowKind.Heading))
        {
            var path = PanelRows.PathTo(page.Rows, row.Key);
            Assert.NotNull(path);
            var memory = new PanelMemory();
            memory.Open(path!);
            Assert.NotNull(Laid(new SettingsContent([page]), page.Page, memory.Opened).LineOf(row.Key));
        }
        // A face's sentence says something, §604 whole, wrapped where it is too long for a line (RowTextFitTests).
        var layout = Laid(new SettingsContent([page]), page.Page);
        foreach (var group in page.Rows.Where(row => row.Kind == PanelRowKind.Group))
        {
            Assert.NotEqual("", group.Sentence);
            var head = layout.LineOf(group.Key)!;
            Assert.True(group.Sentence is null || layout.WordsAt(head).Second.Height >= new SettingsPanelTests.Words().Tall(group.Sentence, Px(PanelMetrics.Caption), layout.NameRoom(head)));
        }
    }

    [Theory]
    [InlineData(LayoutType.Taskbar, false)]
    [InlineData(LayoutType.Taskbar, true)]
    [InlineData(LayoutType.Dock, false)]
    [InlineData(LayoutType.Dock, true)]
    public void AHeaderSaysWhereItStands(LayoutType type, bool inFolder) =>
        Assert.Equal(
            (type, inFolder) switch
            {
                (LayoutType.Taskbar, false) => S.DockPanelPlaceBar,
                (LayoutType.Taskbar, true) => S.DockPanelPlaceBarFolder,
                (_, false) => S.DockPanelPlaceDock,
                _ => S.DockPanelPlaceDockFolder,
            },
            PanelRows.PlaceOf(type, inFolder));
}
