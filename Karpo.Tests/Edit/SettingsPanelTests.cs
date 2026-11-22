// edit, §520: the settings panel's model, apart from any window (Karpo.Dock\SettingsPanel.cs, SettingsPanel.Layout.cs):
// the person, 2026-11-06: "i like how it looks. build it like that now." Held here:
//
//  - **The page builder**: every row the edit bar's menus had (⚙ Karpo, Display, Taskbar, Look, a folder's panel, a
//    calendar's) is a control on its page — a toggle, a dropdown, a colour, a segmented control, a number box, an expander,
//    a row of buttons, a link, a row that acts —, once each; a widget's Cut, Copy and Remove its title's buttons.
//  - **The search**: a setting found on every page, inside the expanders too, by its name whatever its case and accents,
//    those that start with it first; each with where it is ("Look › Taskbar widgets").
//  - **What it remembers**: the expanders open; §575 the flyout open, nothing as edit mode begins; a selection opens its
//    entry's flyout, and closes it as it goes.
//  - **The bar** (§575): a command bar of the pages' entries, the search, the errors, the selection's entries, Add and Done,
//    narrowing on a narrow display; **a flyout** its page alone, 580 wide, as tall as it is and no taller than its room,
//    hung from its entry; every metric of one ramp; every value control one size; the add list a page of its own.
//  - **Escape**, from the inside out: what is carried, the ants, the search's words, the flyout, a folder, edit mode.

using Karpo.Dock;
using Karpo.Tests.Menus;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Edit;

public sealed class SettingsPanelTests
{
    private const float Scale = 1.5f;

    /// <summary>Words as wide as 0.55 of their size a letter: a stand-in for DirectWrite, the same everywhere.</summary>
    internal sealed class Words : IPanelText
    {
        public float Width(string text, float px, bool strong = false) => MathF.Ceiling(text.Length * px * 0.55f);

        /// <summary>§571: a folder's lines this tall (DIP), whatever their look: a stand-in for the plan the open folder lays out.</summary>
        internal const float Lines = 280;

        public float LinesDip(LookShown shows, float scale) => Lines;

        /// <summary>
        /// §604: words wrapped at <paramref name="width"/> as DirectWrite wraps them, each line of their own kept (a line break
        /// a line): at a space where one is, else where the line ends; each line 4/3 of the size tall (Caption's 12/16).
        /// </summary>
        public float Tall(string text, float px, float width) => text.Split('\n').Sum(said => LinesOf(said, px, width)) * MathF.Ceiling(px * 4 / 3);

        private int LinesOf(string text, float px, float width)
        {
            // How many letters a line holds, as Width measures them.
            var most = Math.Max(1, (int)MathF.Floor(width / (px * 0.55f)));
            while (most > 1 && Width(new string('x', most), px) > width)
            {
                most--;
            }
            var lines = 1;
            var used = 0;
            foreach (var word in text.Split(' '))
            {
                var need = used == 0 ? word.Length : used + 1 + word.Length;
                if (need <= most)
                {
                    used = need;
                    continue;
                }
                lines += used > 0 ? 1 : 0;
                // A word longer than a line broken where each line ends.
                lines += Math.Max(0, word.Length - 1) / most;
                used = word.Length == 0 ? 0 : ((word.Length - 1) % most) + 1;
            }
            return lines;
        }
    }

    private static float Px(float dip) => MathF.Round(dip * Scale, MidpointRounding.AwayFromZero);

    /// <summary>§545: the free version, as the Store says it.</summary>
    internal static readonly LicenseStatus Free = new(LicenseTier.Free, LicenseBasis.StoreFree);

    /// <summary>
    /// The pages of a display as the dock builds them (Dock.PanelOf), from a fresh config: Karpo, Accounts, §585 (§624) Layouts, Taskbar (§585 the taskbar menu an expander of Behavior) and Appearance;
    /// and with <paramref name="widget"/>, a folder open and a calendar in it selected: Folder and Widget.
    /// </summary>
    internal static SettingsContent Content(bool widget = false, bool dark = true, ListKind? kind = null, bool several = false, Action<Config>? edit = null)
    {
        var config = MenuCorpus.Fresh();
        // §532: a config of the scene's own (the taskbar menu changed).
        edit?.Invoke(config);
        var bar = new BarSpot(MenuCorpus.Display, "One") { Attached = [MenuCorpus.Display] };
        List<PanelPageView> pages =
        [
            PanelRows.Page(PanelPage.Karpo, PanelRows.SideOf(PanelPage.Karpo), PanelRows.GlyphOf(PanelPage.Karpo), TheDock.SettingsItems(config, MenuCorpus.Services(), [])),
            PanelRows.Page(PanelPage.Accounts, S.SettingsNavAccounts, PanelRows.GlyphOf(PanelPage.Accounts), TheDock.SettingsItems(config, MenuCorpus.Services(), [], page: KarpoPage.Accounts)),
            PanelRows.Page(PanelPage.Layouts, S.CoreBarMenuLayouts, PanelRows.GlyphOf(PanelPage.Layouts), TheDock.LooksItems(config, bar, dark, [], LooksScope.Layouts)),
            PanelRows.Page(PanelPage.Taskbar, S.DockEditTaskbar, PanelRows.GlyphOf(PanelPage.Taskbar), TheDock.LooksItems(config, bar, dark, [], LooksScope.Taskbar, services: MenuCorpus.Services())),
            PanelRows.Page(PanelPage.Appearance, S.SettingsAppearanceHeader, PanelRows.GlyphOf(PanelPage.Appearance), TheDock.LooksItems(config, bar, dark, [], LooksScope.Appearance)),
        ];
        if (widget)
        {
            var place = new OptionPlace(config, Dark: dark);
            // §529: the folder's page its faces, its panel's rows and its look, under its header.
            pages.Add(FolderPage(BarKind.FolderList.New(), place));
            // §521: the widget named, a calendar where none is.
            var calendar = (kind ?? ListKind.Calendar).New();
            var glyph = calendar.Info?.Glyph ?? WidgetInfos.Calendar.Glyph;
            pages.Add(PanelGroups.Page(PanelPage.Widget, IslandEdits.Kind(calendar), glyph, MenuCorpus.Arranged(calendar, place)) with
            {
                Header = PanelRows.HeaderOf([(glyph, IslandEdits.Kind(calendar))], PanelRows.PlaceOf(LayoutType.Taskbar, inFolder: true)),
            });
        }
        if (several)
        {
            // §528: several selected, their page in common (Dock.PanelOf): two clocks, or a clock and the weather where a kind
            // is named; the second's first switch flipped, so they are mixed.
            var place = new OptionPlace(config, Dark: dark);
            WidgetItem[] items = [ListKind.Clock.New(), (kind ?? ListKind.Clock).New()];
            if (OptionRows.Of(items[1], MenuCorpus.Sources, place).FirstOrDefault(row => row.Kind == OptionRowKind.Toggle && row.Set is not null) is { } toggle)
            {
                _ = toggle.Set!(toggle.On ? 0 : 1);
            }
            List<OptionTarget> targets = [.. items.Select(item => new OptionTarget(item, MenuCorpus.Sources, place))];
            var kinds = items.Select(IslandEdits.Kind).Distinct().ToList();
            var title = kinds.Count == 1 ? S.DockEditSelectedKind(count: items.Length, kind: kinds[0]) : S.WidgetFolderWidgets(items.Length);
            // §645: in sections, its header theirs.
            var header = PanelRows.HeaderOf([.. items.Select(item => (item.Info?.Glyph ?? "", IslandEdits.Kind(item)))], PanelRows.PlaceOf(LayoutType.Taskbar, inFolder: true));
            pages.Add(PanelGroups.Page(PanelPage.Widget, title, PanelRows.GlyphOf(PanelPage.Widget), [.. TheDock.WidgetButtons(), .. OptionMenu.Settings(targets, [], FolderSide.Face)], title, header));
        }
        return new SettingsContent(pages);
    }

    /// <summary>§585: the key of the Taskbar page's expander Taskbar menu, Behavior's last row.</summary>
    internal static string BarMenuKey => "Taskbar/" + S.DockTaskbarBehaviour + "/" + S.SettingsBarMenuHeader;

    /// <summary>§585: the Taskbar page with its expander Taskbar menu open.</summary>
    internal static IReadOnlySet<string> BarMenuOpen => new HashSet<string> { BarMenuKey };

    /// <summary>§575: the edit bar's entries alone, as the dock builds them on a display whose bar is not in use (Dock.PanelOf): Karpo to Appearance, no rows.</summary>
    internal static SettingsContent Entries() => new([.. new[] { PanelPage.Karpo, PanelPage.Accounts, PanelPage.Layouts, PanelPage.Taskbar, PanelPage.Appearance }
        .Select(page => new PanelPageView(page, PanelRows.SideOf(page), PanelRows.GlyphOf(page), [], []))]);

    /// <summary>§529: <paramref name="folder"/>'s page as the dock builds it (Dock.PanelOf): its buttons, its face's side and its panel's, grouped, its header.</summary>
    internal static PanelPageView FolderPage(WidgetItem folder, OptionPlace place, List<OptionPick>? picks = null)
    {
        picks ??= [];
        List<FlyoutItem> items = [.. TheDock.WidgetButtons(), .. OptionMenu.Settings(folder, picks, MenuCorpus.Sources, place, FolderSide.Face, null), .. OptionMenu.Settings(folder, picks, MenuCorpus.Sources, place, FolderSide.Panel, null)];
        var header = PanelRows.HeaderOf([(PanelRows.GlyphOf(PanelPage.Folder), S.CoreWidgetFolderListTitle)], PanelRows.PlaceOf(LayoutType.Taskbar, inFolder: false));
        return PanelGroups.FolderPage(PanelPage.Folder, S.CoreWidgetFolderListTitle, PanelRows.GlyphOf(PanelPage.Folder), items, header);
    }

    // --- The page builder ---------------------------------------------------------------------------

    /// <summary>
    /// Every row of every menu the panel replaced is a control of its page, at any depth: no menu row is lost (a rule, an
    /// empty heading and a context menu's buttons excepted, those its title's), and no row is shown twice.
    /// </summary>
    [Fact]
    public void EveryMenuRowIsAControlOnce()
    {
        var config = MenuCorpus.Fresh();
        var place = new OptionPlace(config, Dark: true);
        var bar = new BarSpot(MenuCorpus.Display, "One") { Attached = [MenuCorpus.Display] };
        IReadOnlyList<FlyoutItem>[] menus =
        [
            TheDock.SettingsItems(config, MenuCorpus.Services(), []),
            TheDock.SettingsItems(config, MenuCorpus.Services(), [], page: KarpoPage.Accounts),
            TheDock.LooksItems(config, bar, true, [], LooksScope.Layouts),
            TheDock.LooksItems(config, bar, true, [], LooksScope.Taskbar, services: MenuCorpus.Services()),
            TheDock.LooksItems(config, bar, true, [], LooksScope.Appearance),
            MenuCorpus.Arranged(BarKind.FolderList.New(), place, FolderSide.Panel),
            MenuCorpus.Arranged(ListKind.Calendar.New(), place),
        ];
        // And every widget's, line's and folder face's, as the corpus of menus has them.
        foreach (var items in menus.Concat(MenuCorpus.All().Where(menu => menu.Settings).Select(menu => menu.Items)))
        {
            var page = PanelRows.Page(PanelPage.Widget, "", "", items);
            var rows = PanelRows.All(page.Rows).ToList();
            // A row's level is its control's (a dropdown's choices, a colour's swatches, a text's field): the popup's.
            static IEnumerable<FlyoutItem> Shown(IEnumerable<FlyoutItem> items, Func<FlyoutItem, bool> pops) =>
                items.Where(item => !item.Separator && !item.Primary && !(item.Header && item.Text.Length == 0))
                    .SelectMany(item => (IEnumerable<FlyoutItem>)[item, .. pops(item) || (item.Control is not null && !Thing(item)) ? [] : Shown(item.Children ?? [], pops)]);
            // §521: one of the person's things with rows of its own is an expander of them.
            static bool Thing(FlyoutItem item) => item.Control is FlyoutControl.Tools && item.Children is { Count: > 0 } && !item.PickOne;
            // §521: a text box types in place, its field its own: no row of it.
            var popping = rows.SelectMany(row => row.Pair is { } pair ? [row, pair] : (IEnumerable<PanelRow>)[row])
                .Where(row => row.Pops || row.Kind is PanelRowKind.Text or PanelRowKind.Picker || (row.Kind == PanelRowKind.PickerBody && row.Item.Beside)).Select(row => row.Item.Id).ToHashSet();
            var expected = Shown(items, item => popping.Contains(item.Id)).ToList();
            // No heading with nothing under it.
            // §536: a look's preview no menu row, but its level's picture.
            // §549: a part's heading of a Look page's section its group's head.
            // §610: a row's pair (the palette's reset, §613 a value's) a control of its holder's row.
            // §592: a tab strip no menu row, but its tabs' (each a level's); §673 its button (the palette's reset) one.
            // §670, §672: an inline picker's level its control, as a dropdown's: its body, its search and its hex no rows of their own.
            // A choice standing beside a picker (a font's weight) a body of its own in it: a menu row all the same.
            static bool InPicker(PanelRow row) => (row.Kind == PanelRowKind.PickerBody && !row.Item.Beside)
                || (row.Kind == PanelRowKind.Text && (row.Key.EndsWith("/" + PanelPickers.SearchKey, StringComparison.Ordinal) || row.Key.EndsWith("/" + PanelPickers.HexKey, StringComparison.Ordinal)));
            Assert.Equal(expected.Where(item => !item.Header).Select(item => item.Id).Order(),
                rows.Where(row => row.Kind is not (PanelRowKind.Heading or PanelRowKind.Preview or PanelRowKind.Group) && !InPicker(row))
                    .SelectMany(row => (row.Kind == PanelRowKind.Tabs ? Enumerable.Empty<uint>() : [row.Item.Id]).Concat(row.Pair is { } pair ? [pair.Item.Id] : Enumerable.Empty<uint>())).Order());
            Assert.Equal(rows.Count, rows.Select(row => row.Key).Distinct().Count());
            Assert.Equal(items.Where(item => item.Primary).Select(item => item.Id), page.Actions.Select(action => action.Id));
        }
    }

    /// <summary>Each kind of menu row is its control (§520 decision 6), found on the pages the person sees.</summary>
    [Fact]
    public void EachKindOfRowIsItsControl()
    {
        var content = Content(widget: true);
        var all = content.Pages.SelectMany(page => PanelRows.All(page.Rows)).ToList();
        PanelRow Named(PanelPage page, string text) => PanelRows.All(content.Of(page)!.Rows).First(row => row.Text == text);
        Assert.Equal(PanelRowKind.Toggle, Named(PanelPage.Karpo, S.SettingsGeneralStartupHeader).Kind);
        // §549: Animations a choice of its four; §672 its picker inline; §682 the Appearance's.
        Assert.Equal(PanelRowKind.Picker, Named(PanelPage.Appearance, S.SettingsGeneralAnimationsHeader).Kind);
        // §676: a lookup (the language) its search over what it finds, inline.
        Assert.Equal(PanelRowKind.Picker, Named(PanelPage.Karpo, S.SettingsLanguageSection).Kind);
        Assert.Equal(PanelRowKind.Expander, Named(PanelPage.Karpo, S.CoreHolidaysName).Kind);
        Assert.Equal(PanelRowKind.Stepper, Named(PanelPage.Taskbar, S.SettingsLayoutsSizeHeader).Kind);
        // §670: a colour on the Appearance page its picker inline, its swatch in its head.
        Assert.Contains(all, row => row.Kind == PanelRowKind.Picker && PanelPickers.Colours(row.Item) && row.Swatch is not null);
        // §672: a choice of two or three (Show desktop's style) its picker inline, as any choice of one: no dropdown of choices.
        Assert.DoesNotContain(all, row => row.Kind == PanelRowKind.Choice && PanelPickers.Takes(row.Item, row.Item.Children ?? []));
        // §529: a calendar's way to its accounts a line of what it draws on, its link at its end.
        Assert.Contains(all, row => row.Kind == PanelRowKind.Words && row.Detail is { Length: > 0 });
        Assert.Contains(all, row => row.Kind == PanelRowKind.Item && row.Tools.Count > 0);
        Assert.Contains(all, row => row.Kind == PanelRowKind.Action);
        // A shut expander says what it holds: the calendars by name.
        Assert.Equal(HolidayPlaces.Name("CH"), Named(PanelPage.Karpo, S.CoreHolidaysName).Value);
        // A widget's Cut, Copy and Remove are its title's.
        Assert.Equal([S.DockMenuCut, S.DockMenuCopy, S.DockMenuRemove], content.Of(PanelPage.Widget)!.Actions.Select(action => action.Text));
        Assert.Null(content.Of(PanelPage.Karpo)!.Rows.FirstOrDefault(row => row.Item.Foot));
    }

    /// <summary>A row's key is where it is: its page, the expanders it is in, its name; the path to it the expanders over it.</summary>
    [Fact]
    public void ARowsKeyIsWhereItIs()
    {
        var look = Content().Of(PanelPage.Appearance)!;
        var font = PanelRows.All(look.Rows).First(row => row.Text == S.SettingsAppearanceStylesFont);
        var path = PanelRows.PathTo(look.Rows, font.Key)!;
        Assert.NotEmpty(path);
        Assert.StartsWith("Appearance/", font.Key, StringComparison.Ordinal);
        Assert.All(path, key => Assert.StartsWith(key + "/", font.Key, StringComparison.Ordinal));
        Assert.Null(PanelRows.PathTo(look.Rows, "Appearance/nothing"));
    }

    // --- §703: no Quit Karpo on the settings panel ------------------------------------------------------

    /// <summary>
    /// §703 (the owner, 2026-11-15: "dont show quit karpo"; §545 until §703 it was General's last row): the Karpo page has no Quit
    /// Karpo, under General or anywhere else; the taskbar menu's (§532) still asks and quits (Menus.BarMenuRowsTests).
    /// </summary>
    [Fact]
    public void TheKarpoPageHasNoQuitKarpo()
    {
        var config = MenuCorpus.Fresh();
        List<OptionPick> picks = [];
        var page = PanelRows.Page(PanelPage.Karpo, PanelRows.SideOf(PanelPage.Karpo), PanelRows.GlyphOf(PanelPage.Karpo), TheDock.SettingsItems(config, MenuCorpus.Services(), picks));
        Assert.DoesNotContain(PanelRows.All(page.Rows), row => row.Text == S.SettingsAboutQuitHeader);
        Assert.DoesNotContain(picks, pick => pick.Key == KarpoMenu.QuitRow);
        var general = page.Rows.SkipWhile(row => !(row.Kind == PanelRowKind.Heading && row.Text == S.SettingsGeneralHeader)).Skip(1).TakeWhile(row => row.Kind != PanelRowKind.Heading).ToList();
        Assert.NotEmpty(general);
    }

    // --- What it remembers --------------------------------------------------------------------------

    /// <summary>
    /// §575: no flyout open as edit mode begins; a widget selected opens its entry's flyout, in place of another's; the same
    /// selection again opens nothing new (a flyout closed while it stands stays closed); another widget opens it again; the
    /// selection gone closes it; a folder opened opens its, a line in it the widget's, the line let go closes it; a flyout of a
    /// page stays as the selection goes. The expanders open stay open for the run.
    /// </summary>
    [Fact]
    public void ItFollowsTheSelection()
    {
        var memory = new PanelMemory();
        Assert.Null(memory.Page);
        memory.Show(PanelPage.Appearance);
        Assert.Equal(PanelPage.Widget, memory.Follow("w1", null));
        Assert.Equal(PanelPage.Widget, memory.Page);
        Assert.Null(memory.Follow("w1", null));
        memory.Show(null);
        Assert.Null(memory.Follow("w1", null));
        Assert.Null(memory.Page);
        Assert.Equal(PanelPage.Widget, memory.Follow("w2", null));
        Assert.Null(memory.Follow(null, null));
        Assert.Null(memory.Page);
        Assert.Equal(PanelPage.Folder, memory.Follow(null, "f"));
        Assert.Equal(PanelPage.Widget, memory.Follow("line", "f"));
        Assert.Null(memory.Follow(null, "f"));
        Assert.Null(memory.Page);
        memory.Show(PanelPage.Taskbar);
        Assert.Null(memory.Follow(null, null));
        Assert.Equal(PanelPage.Taskbar, memory.Page);
        // A new edit session: nothing open, nothing followed — the widget selected as it begins opens its.
        memory.Reset();
        Assert.Null(memory.Page);
        Assert.Equal(PanelPage.Widget, memory.Follow("w2", null));
        // The expanders open stay open for the run.
        memory.Toggle("Appearance/Taskbar widgets");
        memory.Open(["Karpo/Accounts"]);
        Assert.True(memory.IsOpen("Appearance/Taskbar widgets") && memory.IsOpen("Karpo/Accounts"));
        memory.Toggle("Appearance/Taskbar widgets");
        Assert.False(memory.IsOpen("Appearance/Taskbar widgets"));
    }

    /// <summary>
    /// §576 (the bug: a click on the widget selected never brought its flyout back, the press having closed it and the
    /// selection being the one followed): the selection picked again — clicked — opens its entry's flyout as it is followed
    /// next, the same widget, a line of a folder open too; picked with its flyout open, it stays open.
    /// </summary>
    [Fact]
    public void TheSelectionClickedAgainOpensItsFlyout()
    {
        var memory = new PanelMemory();
        Assert.Equal(PanelPage.Widget, memory.Follow("w1", null));
        memory.Show(null);
        Assert.Null(memory.Follow("w1", null));
        memory.Picked();
        Assert.Equal(PanelPage.Widget, memory.Follow("w1", null));
        // Open, picked again: it stays.
        memory.Picked();
        Assert.Equal(PanelPage.Widget, memory.Follow("w1", null));
        Assert.Equal(PanelPage.Widget, memory.Page);
        // A line of the folder open, its flyout closed, clicked again.
        Assert.Equal(PanelPage.Widget, memory.Follow("line", "f"));
        memory.Show(null);
        memory.Picked();
        Assert.Equal(PanelPage.Widget, memory.Follow("line", "f"));
    }

    // --- The bar ------------------------------------------------------------------------------------

    /// <summary>
    /// §575 (the person, 2026-11-08: "all of the menu entries we have on the sidemenu get promoted to menu entries on the
    /// topbar"): the bar one 48 DIP row — §630 no grip — an entry for each page, the first a gutter in, Karpo to Appearance in the side list's old order,
    /// a command bar's buttons 4 apart, each its icon and its words; a gutter, §596 the selection's room 256 × 32 (the folder's
    /// entry and the widget's, named for what is selected, entries as the pages' are from its start), a gutter; the errors'
    /// icon button; Add; Done, ending a gutter in. No search. No flyout with none open; the open one's entry selected.
    /// </summary>
    [Fact]
    public void TheBarIsACommandBarOfThePages()
    {
        var content = Content(widget: true);
        var bar = new PanelLayout(new Words(), Scale, new PanelInput(2, content));
        PanelHitKind[] kinds = [.. Enumerable.Repeat(PanelHitKind.Nav, 5), PanelHitKind.Nav, PanelHitKind.Nav, PanelHitKind.Errors, PanelHitKind.Add, PanelHitKind.Done];
        Assert.Equal(kinds, bar.Header.Select(part => part.Kind));
        Assert.Equal([PanelPage.Karpo, PanelPage.Accounts, PanelPage.Layouts, PanelPage.Taskbar, PanelPage.Appearance, PanelPage.Folder, PanelPage.Widget],
            bar.Header.Where(part => part.Kind == PanelHitKind.Nav).Select(part => part.Page));
        Assert.Equal(content.Of(PanelPage.Widget)!.Title, bar.Header.Single(part => part.Page == PanelPage.Widget && part.Kind == PanelHitKind.Nav).Words);
        Assert.Equal(S.SettingsAppearanceHeader, bar.Header.Single(part => part.Page == PanelPage.Appearance).Words);
        Assert.Equal(Px(48), bar.Size.Y);
        Assert.All(bar.Header, part => Assert.Equal((Px(8), Px(32)), (part.Rect.Y, part.Rect.Height)));
        var entries = bar.Header.Take(5).ToList();
        Assert.Equal(Px(8), entries[0].Rect.X);
        Assert.All(entries.Zip(entries.Skip(1)), pair => Assert.Equal(Px(4), pair.Second.Rect.X - pair.First.Rect.Right));
        Assert.All(entries, entry => Assert.Equal(bar.ButtonWide(entry.Words, chevron: false), entry.Rect.Width));
        // §608: 8, the divider (a hairline 20 DIP tall in the bar's middle), 8, the room — the hairline the room's.
        Assert.Equal(1, bar.Hair);
        Assert.Equal(new RectF(entries[^1].Rect.Right + Px(8), Px(14), 1, Px(20)), bar.Divider);
        Assert.Equal((Px(256) - 1, Px(32), Px(8)), (bar.Selection.Width, bar.Selection.Height, bar.Selection.Y));
        Assert.Equal((Px(8), Px(16)), (bar.Selection.X - bar.Divider.Right, bar.Header[7].Rect.X - bar.Selection.Right));
        // §596: the folder's entry and the widget's two entries as the pages' are — each its words, as wide as they are, 4 apart
        // — from the room's start, the rest of it empty.
        var (folder, widget) = (bar.Header[5], bar.Header[6]);
        Assert.Equal(new RectF(bar.Selection.X, Px(8), bar.ButtonWide(S.CoreWidgetFolderListTitle, chevron: false), Px(32)), folder.Rect);
        Assert.Equal(new RectF(folder.Rect.Right + Px(4), Px(8), bar.ButtonWide(widget.Words, chevron: false), Px(32)), widget.Rect);
        Assert.False(folder.Compact || widget.Compact);
        Assert.True(widget.Rect.Right < bar.Selection.Right);
        // To a screen reader each a button that expands, as a page's entry is, named its words.
        var said = DeckUia.Things(bar, bar: true).Where(thing => thing.Hit.Page is PanelPage.Folder or PanelPage.Widget).ToList();
        Assert.Equal(new[] { (S.CoreWidgetFolderListTitle, UiaRole.Button, UiaPattern.Expand), (widget.Words, UiaRole.Button, UiaPattern.Expand) },
            said.Select(thing => (thing.Name, thing.Role, thing.Pattern)));
        Assert.Equal((Px(40), true), (bar.Header[7].Rect.Width, bar.Header[7].Compact));
        Assert.Equal(bar.Size.X - Px(16), bar.Header[^1].Rect.Right);
        // None open: no flyout; no entry selected.
        Assert.False(bar.IsOpen);
        Assert.Equal(System.Numerics.Vector2.Zero, bar.FlyoutSize);
        Assert.DoesNotContain(bar.Header, part => bar.Opened(PanelLayout.HitOf(part)));
        // Taskbar's open: its entry selected, the bar as it stood.
        var open = new PanelLayout(new Words(), Scale, new PanelInput(2, content, PanelPage.Taskbar), PanelLayout.SizeOf(Scale, 1548));
        Assert.Equal(bar.Header.Select(part => part.Rect), open.Header.Select(part => part.Rect));
        Assert.Equal([PanelPage.Taskbar], open.Header.Where(part => open.Opened(PanelLayout.HitOf(part))).Select(part => part.Page));
        // Every part is hit where it stands.
        Assert.All(bar.Header, part => Assert.Equal(PanelLayout.HitOf(part), bar.HitAt(part.Rect.Center)));
        Assert.All(bar.Header, part => Assert.True(PanelLayout.OnBar(part.Kind)));
    }

    /// <summary>
    /// §578 (the person, 2026-11-09: "the top edit bar should not change width, it should have the same width always"): the
    /// bar is one width whether nothing is selected, a widget, a folder or both, with errors or without, a flyout open or not —
    /// every part before the selection's slot and every part after it where it stood; with nothing selected §584 nothing in the
    /// slot (the person, 2026-11-09: "there should not be a placeholder shown "Widget" in the topbar"), nothing to press; an
    /// entry whose name is cut short in it says it whole in a tooltip.
    /// </summary>
    [Fact]
    public void TheBarIsOneWidthWhateverIsSelected()
    {
        var both = Content(widget: true);
        var widget = new SettingsContent([.. both.Pages.Where(page => page.Page != PanelPage.Folder)]);
        var folder = new SettingsContent([.. both.Pages.Where(page => page.Page != PanelPage.Widget)]);
        PanelLayout Bar(SettingsContent content, int errors = 0, PanelPage? open = null) =>
            new(new Words(), Scale, new PanelInput(errors, content, open), PanelLayout.SizeOf(Scale, 1548));
        var none = Bar(Content());
        PanelLayout[] all = [none, Bar(widget), Bar(folder), Bar(both), Bar(both, errors: 12), Bar(Content(), errors: 1), Bar(widget, open: PanelPage.Widget)];
        Assert.All(all, bar => Assert.Equal(none.Size, bar.Size));
        Assert.All(all, bar => Assert.Equal(none.Selection, bar.Selection));
        var after = (PanelLayout bar) => bar.Header.Where(part => part.Kind is PanelHitKind.Add or PanelHitKind.Done).Select(part => part.Rect).ToList();
        var before = (PanelLayout bar) => bar.Header.Where(part => part.Rect.Right <= bar.Selection.X).Select(part => part.Rect).ToList();
        Assert.All(all, bar => Assert.Equal(after(none), after(bar)));
        Assert.All(all, bar => Assert.Equal(before(none), before(bar)));
        // §608 (the person, 2026-11-09: "add a little divider between appearance and folder"): a divider while anything is
        // selected, between Appearance's entry and the first of the selection's, always where it stands; none with nothing.
        Assert.True(none.Divider.IsEmpty);
        Assert.True(Bar(Content(), errors: 1).Divider.IsEmpty);
        var divided = all.Where(bar => bar.Header.Any(part => part.Page is PanelPage.Folder or PanelPage.Widget)).ToList();
        Assert.Equal(all.Length - 2, divided.Count);
        Assert.All(divided, bar =>
        {
            var header = bar.Header.ToList();
            var appearance = header.FindIndex(part => part.Page == PanelPage.Appearance);
            var first = header[appearance + 1];
            Assert.True(first.Page is PanelPage.Folder or PanelPage.Widget);
            Assert.Equal((Px(8), Px(8)), (bar.Divider.X - header[appearance].Rect.Right, first.Rect.X - bar.Divider.Right));
            Assert.Equal(divided[0].Divider, bar.Divider);
            // Drawn alone: no part of the bar there, nothing to press, stop on nor name.
            Assert.DoesNotContain(header, part => part.Rect.X < bar.Divider.Right && bar.Divider.X < part.Rect.Right);
            Assert.Equal(PanelHit.None, bar.HitAt(bar.Divider.Center));
        });
        // §584 nothing selected: nothing in the slot — no part drawn nor named there, nothing to press nor stop on —, its room kept.
        Assert.DoesNotContain(none.Header, part => part.Rect.X < none.Selection.Right && none.Selection.X < part.Rect.Right);
        Assert.Equal(PanelHit.None, none.HitAt(none.Selection.Center));
        Assert.DoesNotContain(none.Stops(), stop => stop.Page is PanelPage.Widget or PanelPage.Folder);
        // §596 (the person, 2026-11-09: "folder and the widget/s that are selected should be independent items in the topbar
        // just like the other items are"): one selected, its entry as wide as its words from the room's start, the rest empty —
        // a widget's, a folder's, several widgets' one entry ("2 × Clock").
        var several = Content(several: true);
        foreach (var (content, page) in new[] { (widget, PanelPage.Widget), (folder, PanelPage.Folder), (several, PanelPage.Widget) })
        {
            var laid = Bar(content);
            Assert.Equal(none.Size, laid.Size);
            var alone = laid.Header.Single(part => part.Kind == PanelHitKind.Nav && part.Page is PanelPage.Widget or PanelPage.Folder);
            Assert.Equal((page, content.Of(page)!.Title), (alone.Page, alone.Words));
            Assert.Equal(new RectF(none.Selection.X, none.Selection.Y, laid.ButtonWide(alone.Words, chevron: false), none.Selection.Height), alone.Rect);
            Assert.False(alone.Compact);
            Assert.Null(laid.TipOf(PanelLayout.HitOf(alone)));
            Assert.Equal(PanelHit.None, laid.HitAt(new System.Numerics.Vector2((alone.Rect.Right + none.Selection.Right) / 2, none.Selection.Center.Y)));
        }
        Assert.Equal(S.DockEditSelectedKind(count: 2, kind: IslandEdits.Kind(ListKind.Clock.New())), several.Of(PanelPage.Widget)!.Title);
        // A name longer than the room: cut short at its end, whole in its tooltip; beside the folder's, the folder's whole.
        var longer = new SettingsContent([.. both.Pages.Select(page => page.Page == PanelPage.Widget ? page with { Title = new string('W', 60) } : page)]);
        foreach (var content in new[] { new SettingsContent([.. longer.Pages.Where(page => page.Page != PanelPage.Folder)]), longer })
        {
            var cut = Bar(content);
            var entry = cut.Header.Single(part => part.Page == PanelPage.Widget);
            Assert.Equal(none.Size, cut.Size);
            Assert.Equal(cut.Selection.Right, entry.Rect.Right);
            Assert.Equal(entry.Words, cut.TipOf(PanelLayout.HitOf(entry)));
            Assert.All(cut.Header.Where(part => part.Page == PanelPage.Folder), part => Assert.Equal(cut.ButtonWide(part.Words, chevron: false), part.Rect.Width));
        }
    }

    /// <summary>
    /// §596: the selection's entries in a room narrower than their words: each its words' width where they fit; else the last
    /// (the widget's) cut short, its name no shorter than its least (40 DIP); else the folder's its icon alone and the widget's
    /// cut in what is left; else both icons alone (the room never narrower than two icons).
    /// </summary>
    [Fact]
    public void TheSelectionsEntriesFitTheirRoom()
    {
        var bar = new PanelLayout(new Words(), Scale, new PanelInput(0, Content()));
        string[] words = ["Folder", "Calendar"];
        var (folder, widget) = (bar.ButtonWide(words[0], chevron: false), bar.ButtonWide(words[1], chevron: false));
        var least = Px(12) + Px(16) + Px(8) + Px(40) + Px(12);
        Assert.Equal(new[] { (folder, false), (widget, false) }, bar.Fit(words, folder + Px(4) + widget + Px(30)));
        Assert.Equal(new[] { (folder, false), (least, false) }, bar.Fit(words, folder + Px(4) + least));
        // The folder's its icon alone: the widget's whole again where that leaves room enough.
        Assert.Equal(new[] { (Px(40), true), (widget, false) }, bar.Fit(words, folder + Px(4) + least - 1));
        Assert.Equal(new[] { (Px(40), true), (least, false) }, bar.Fit(words, Px(40) + Px(4) + least));
        Assert.Equal(new[] { (Px(40), true), (Px(40), true) }, bar.Fit(words, Px(40) + Px(4) + least - 1));
        Assert.Equal(new[] { (least + 1, false) }, bar.Fit([new string('W', 60)], least + 1));
        Assert.Equal(new[] { (Px(40), true) }, bar.Fit([new string('W', 60)], least - 1));
        Assert.Empty(bar.Fit([], Px(256)));
    }

    /// <summary>
    /// §575: on a display too narrow for the bar, the pages' entries show their icon alone, 40 DIP each, their words their
    /// tooltip and a screen reader's; §578 narrower still, the selection's slot narrows, never under two icons' room. Each width
    /// the same whatever is selected.
    /// </summary>
    [Fact]
    public void TheBarNarrowsOnANarrowDisplay()
    {
        var content = Content();
        var wide = new PanelLayout(new Words(), Scale, new PanelInput(0, content));
        var some = wide.Size.X - Px(100);
        var narrower = new PanelLayout(new Words(), Scale, new PanelInput(0, content), new System.Numerics.Vector2(some, 0));
        Assert.True(narrower.Size.X <= some);
        Assert.All(narrower.Header.Where(part => part.Kind == PanelHitKind.Nav), part => Assert.True(part.Compact));
        Assert.Equal(Px(256) - narrower.Hair, narrower.Selection.Width);
        Assert.Equal(narrower.Size, new PanelLayout(new Words(), Scale, new PanelInput(3, Content(widget: true)), new System.Numerics.Vector2(some, 0)).Size);
        var least = new PanelLayout(new Words(), Scale, new PanelInput(0, content), new System.Numerics.Vector2(Px(600), 0));
        Assert.True(least.Selection.Width < Px(256) && least.Selection.Width >= Px(84));
        var both = new PanelLayout(new Words(), Scale, new PanelInput(3, Content(widget: true)), new System.Numerics.Vector2(Px(600), 0));
        Assert.Equal(least.Size, both.Size);
        // §596: the selection's entries in a room too narrow for their words their icons first, their words their tooltips.
        var chosen = both.Header.Where(part => part.Page is PanelPage.Folder or PanelPage.Widget).ToList();
        Assert.Equal(2, chosen.Count);
        Assert.True(chosen[0].Compact);
        Assert.All(chosen, part => Assert.True(part.Rect.X >= both.Selection.X && part.Rect.Right <= both.Selection.Right));
        // §608: the icons alone, the divider as it stands on a wide display: 8 after Appearance's, 8 before the room; none
        // with nothing selected.
        var appearance = both.Header.Single(part => part.Page == PanelPage.Appearance);
        Assert.Equal((Px(8), Px(8)), (both.Divider.X - appearance.Rect.Right, both.Selection.X - both.Divider.Right));
        Assert.True(least.Divider.IsEmpty);
        Assert.All(chosen.Where(part => part.Rect.Width < both.ButtonWide(part.Words, chevron: false)), part => Assert.Equal(part.Words, both.TipOf(PanelLayout.HitOf(part))));
        Assert.Equal(chosen[0].Words, both.TipOf(PanelLayout.HitOf(chosen[0])));
        var icons = least.Header.Where(part => part.Kind == PanelHitKind.Nav).ToList();
        Assert.All(icons, part => Assert.Equal((true, Px(40)), (part.Compact, part.Rect.Width)));
        Assert.All(icons, part => Assert.Equal(part.Words, least.TipOf(PanelLayout.HitOf(part))));
        Assert.Null(wide.TipOf(PanelLayout.HitOf(wide.Header[0])));
    }

    /// <summary>
    /// §394 (§578): the errors' indicator, an icon button saying how many in its tooltip, stands after the selection's room and
    /// before "Add", only while there are any — its room kept always, the bar one width with errors or without; it asks for its
    /// list. §630: nothing before the pages' entries (§395 to §629 a grip led).
    /// </summary>
    [Fact]
    public void TheErrorsStandBeforeAdd()
    {
        var text = new Words();
        var none = new PanelLayout(text, Scale, new PanelInput(0));
        var some = new PanelLayout(text, Scale, new PanelInput(3));
        Assert.Equal([PanelHitKind.Add, PanelHitKind.Done], none.Header.Select(part => part.Kind));
        Assert.Equal([PanelHitKind.Errors, PanelHitKind.Add, PanelHitKind.Done], some.Header.Select(part => part.Kind));
        Assert.Equal(S.DockEditErrors(3), some.Header[0].Words);
        Assert.True(some.Header[0].Compact);
        Assert.Equal(S.DockEditErrors(3), some.TipOf(new PanelHit(PanelHitKind.Errors)));
        Assert.Equal(PanelHitKind.Errors, some.HitAt(some.Header[0].Rect.Center).Kind);
        Assert.Equal(none.Size.X, some.Size.X);
        Assert.Equal(none.Header[0].Rect, some.Header[1].Rect);
        Assert.True(some.Selection.Right < some.Header[0].Rect.X && some.Header[0].Rect.Right < some.Header[1].Rect.X);
        Assert.Equal(PanelHitKind.None, none.HitAt(some.Header[0].Rect.Center).Kind);
    }

    // --- A flyout -----------------------------------------------------------------------------------

    /// <summary>
    /// §575: a page's flyout its page alone — no side list, no title: 580 DIP wide, its cards a gutter in from its sides, as
    /// tall as its page and no taller than its room, scrolling past it; every line 48 or 60 tall, cards 4 apart. A page's
    /// controls are the flyout's, never the bar's.
    /// </summary>
    [Fact]
    public void AFlyoutIsItsPageAlone()
    {
        var words = new Words();
        var size = PanelLayout.SizeOf(Scale, 1548);
        var open = new PanelLayout(words, Scale, new PanelInput(0, Content(), PanelPage.Karpo), size);
        Assert.True(open.IsOpen);
        Assert.Equal(PanelPage.Karpo, open.Shown!.Page);
        Assert.Equal(Px(580), open.FlyoutSize.X);
        Assert.Equal(MathF.Min(open.ContentHeight, size.Y), open.FlyoutSize.Y);
        Assert.Equal(new RectF(0, 0, Px(580), open.FlyoutSize.Y), open.Viewport);
        Assert.True(open.Title.IsEmpty && open.HeaderCard.IsEmpty);
        var cards = open.Blocks.Where(block => block.Row.Kind != PanelRowKind.Heading).ToList();
        Assert.All(cards, card => Assert.Equal((Px(16), Px(580) - Px(16)), (open.OnPanel(card.Rect).X, open.OnPanel(card.Rect).Right)));
        Assert.All(cards.SelectMany(card => card.Lines), line => Assert.Contains(line.Rect.Height, new[] { Px(48), Px(60) }));
        Assert.Contains(cards.Zip(cards.Skip(1)), pair => pair.Second.Rect.Y - pair.First.Rect.Bottom == Px(4));
        Assert.All(open.Stops().Where(stop => !PanelLayout.OnBar(stop.Kind)), stop => Assert.False(PanelLayout.OnBar(stop.Kind)));
        // A short room: as tall as it, its page scrolling.
        var low = new PanelLayout(words, Scale, new PanelInput(0, Content(), PanelPage.Karpo), new System.Numerics.Vector2(0, Px(300)));
        Assert.Equal(Px(300), low.FlyoutSize.Y);
        Assert.Equal(low.ContentHeight - Px(300), low.MostScroll);
        // A page not among the bar's (a display that shows None has no Taskbar): no flyout.
        var none = new PanelLayout(words, Scale, new PanelInput(0, new SettingsContent([Content().Of(PanelPage.Karpo)!]), PanelPage.Taskbar), size);
        Assert.False(none.IsOpen);
        Assert.Equal(System.Numerics.Vector2.Zero, none.FlyoutSize);
    }

    /// <summary>
    /// §548: every space of the shell is a step of one ramp — 4, 8, 12, 16, 24 —, and what is built of them adds up: the bar
    /// its controls and 8 round them, a flyout a gutter either side of its cards, a header card its tile and a gutter round
    /// it, a colour button's chevron 20 in from its end.
    /// </summary>
    [Fact]
    public void EverySpaceIsAStepOfOneRamp()
    {
        float[] ramp = [4, 8, 12, 16, 24];
        Assert.Equal(ramp, new[] { PanelMetrics.Tight, PanelMetrics.Gap, PanelMetrics.Label, PanelMetrics.Gutter, PanelMetrics.Group });
        Assert.Contains(PanelMetrics.IconAt, ramp);
        Assert.Equal(PanelMetrics.Gap + PanelMetrics.Control + PanelMetrics.Gap, PanelMetrics.Head);
        Assert.Equal(PanelMetrics.Gutter + PanelMetrics.HeaderTile + PanelMetrics.Gutter, PanelMetrics.HeaderTall);
        // §575: a flyout's cards as wide as the panel's page's were (§548: 760 − 196 − 16).
        Assert.Equal(PanelMetrics.Gutter + (760 - 196 - 16) + PanelMetrics.Gutter, PanelMetrics.FlyoutWide);
        // WinUI's type ramp: each style its line.
        Assert.Equal((12f, 16f, 14f, 20f, 20f, 28f), (PanelMetrics.Caption, PanelMetrics.CaptionLine, PanelMetrics.Body, PanelMetrics.BodyLine, PanelMetrics.Subtitle, PanelMetrics.SubtitleLine));
        Assert.Equal(PanelMetrics.BodyLine, PanelMetrics.Heading);
        // A two-line card: Body over Caption, 12 over and under them.
        Assert.Equal(PanelMetrics.Label + PanelMetrics.BodyLine + PanelMetrics.CaptionLine + PanelMetrics.Label, PanelMetrics.TwoLines);
        // A colour button: its swatch 6 in, its chevron 8 after it, its middle 20 in from the button's end, as every chevron.
        Assert.Equal(PanelMetrics.ColourWide - PanelMetrics.ChevronIn, PanelMetrics.ColourInset + PanelMetrics.ColourSwatch + PanelMetrics.Gap + (PanelMetrics.Chevron / 2));
    }

    /// <summary>
    /// §548: one rhythm down every page's flyout: its first group a gutter in from its top (a header card's 24 under it),
    /// a card to the next heading 24, a heading to its cards 8, card to card 4, a gutter under the last; §578 a row that only
    /// acts 8 off what is before it and after it, the next such beside it; a header card's name and place centred on its tile.
    /// </summary>
    [Fact]
    public void EveryPageKeepsOneRhythm()
    {
        var words = new Words();
        var size = PanelLayout.SizeOf(Scale, 1548);
        var content = Content(widget: true);
        foreach (var view in content.Pages)
        {
            var layout = new PanelLayout(words, Scale, new PanelInput(0, content, view.Page), size);
            var headed = !layout.HeaderCard.IsEmpty;
            var previous = (Kind: PanelRowKind.Heading, Top: 0f, Bottom: headed ? layout.HeaderCard.Bottom : Px(PanelMetrics.Gutter), First: true, Foot: false);
            foreach (var block in layout.Blocks)
            {
                var foot = block.Lines is [{ Row.Foot: true, Inner: false }];
                // §578: beside the one before it on its line.
                if (foot && previous.Foot && block.Rect.Y == previous.Top)
                {
                    continue;
                }
                var gap = block.Rect.Y - previous.Bottom;
                // §592: a tab strip 8 under what is before it, 8 over the chosen tab's cards.
                var want = previous.First ? (headed ? Px(PanelMetrics.Group) : 0)
                    : block.Row.Kind == PanelRowKind.Heading ? Px(PanelMetrics.Group)
                    : previous.Kind is PanelRowKind.Heading or PanelRowKind.Tabs || block.Row.Kind == PanelRowKind.Tabs ? Px(PanelMetrics.Gap)

                    : foot && previous.Foot ? Px(PanelMetrics.Tight)
                    : foot || previous.Foot ? Px(PanelMetrics.Gap)
                    : Px(PanelMetrics.Tight);
                Assert.True(want == gap, $"{view.Page}: {block.Row.Key} stands {gap} px after what is before it, not {want}");
                previous = (block.Row.Kind, block.Rect.Y, block.Rect.Bottom, false, foot);
            }
            Assert.Equal(previous.Bottom + Px(PanelMetrics.Gutter), layout.ContentHeight);
            if (headed)
            {
                Assert.Equal(Px(PanelMetrics.Gutter), layout.HeaderCard.Y);
                var lines = (layout.Title.Y + layout.Place.Bottom) / 2;
                Assert.InRange(lines, layout.HeaderTile.Center.Y - 1, layout.HeaderTile.Center.Y + 1);
                Assert.All(layout.Actions, action => Assert.Equal(layout.HeaderTile.Center.Y, action.Rect.Center.Y));
            }
        }
    }

    /// <summary>
    /// §520 (the person: "try to use similar or same sizes of texts or elements"): every control that holds a value is one
    /// size, 240 × 32, its right end 16 in from its card's; every chevron 20 in from it — on every page, open or shut.
    /// </summary>
    [Fact]
    public void EveryControlIsOneSize()
    {
        var words = new Words();
        var content = Content(widget: true);
        var size = PanelLayout.SizeOf(Scale, 1548);
        var open = content.Pages.SelectMany(page => PanelRows.All(page.Rows)).Where(row => row.Kind == PanelRowKind.Expander).Select(row => row.Key).ToHashSet();
        foreach (var page in content.Pages)
        {
            var layout = new PanelLayout(words, Scale, new PanelInput(0, content, page.Page, open), size);
            foreach (var line in layout.Blocks.SelectMany(block => block.Lines))
            {
                var controls = layout.Controls(line);
                var right = line.Rect.Right - Px(16);
                switch (line.Row.Kind)
                {
                    case PanelRowKind.Choice or PanelRowKind.Text when !line.Row.Adds:
                    {
                        var box = new RectF(right - Px(240), line.Rect.Y + ((line.Rect.Height - Px(32)) / 2), Px(240), Px(32));
                        Assert.Equal(box, controls.Single(each => each.Hit.Key == line.Row.Key).Rect);
                        // §610: a button beside it (the palette's reset) 8 before its box, 32 tall, its words' 120 to 240 wide.
                        if (line.Row.Pair is { Kind: PanelRowKind.Action } beside)
                        {
                            var button = controls.Single(each => each.Hit.Key == beside.Key).Rect;
                            Assert.Equal((box.X - Px(8), box.Y, Px(32)), (button.Right, button.Y, button.Height));
                            Assert.InRange(button.Width, Px(120), Px(240));
                            Assert.Equal(button.X, layout.ControlLeft(line));
                        }
                        else
                        {
                            Assert.Single(controls);
                        }
                        break;
                    }
                    // §568: a colour of both modes two colour buttons, the dark one at the row's end, the light one 8 before it
                    // (§574: no reset between them); §577 the two as wide as one dropdown together, 240: 116 × 32 each.
                    case PanelRowKind.Color when line.Row.Pair is { } dark:
                    {
                        var pops = controls.Where(each => each.Hit.Kind == PanelHitKind.Pop).Select(each => each.Rect).ToList();
                        var top = line.Rect.Y + ((line.Rect.Height - Px(32)) / 2);
                        Assert.Equal([new RectF(right - Px(116) - Px(8) - Px(116), top, Px(116), Px(32)), new RectF(right - Px(116), top, Px(116), Px(32))], pops);
                        Assert.Equal(controls.Select(each => each.Rect), pops);
                        Assert.Equal(right - Px(240), layout.ControlLeft(line));
                        Assert.Equal(right - Px(240), pops[0].X);
                        break;
                    }
                    // §535: a colour a colour button at the row's end, §548 72 × 32, what it is set to said before it in the 240.
                    case PanelRowKind.Color:
                        Assert.Equal(new RectF(right - Px(72), line.Rect.Y + ((line.Rect.Height - Px(32)) / 2), Px(72), Px(32)), controls.Single().Rect);
                        Assert.Equal(right - Px(240), layout.ControlLeft(line));
                        break;
                    // §578: a row that only acts its button alone, as wide as its glyph and words: on the page the line itself,
                    // §599 the last of its line ending where a card's controls end; in an expander at its line's end.
                    case PanelRowKind.Action or PanelRowKind.Choice when line.Row.Foot:
                    {
                        var button = controls.Single().Rect;
                        Assert.Equal((Px(32), layout.FootWide(line.Row)), (button.Height, button.Width));
                        if (line.Inner)
                        {
                            Assert.Equal(right, button.Right);
                        }
                        else
                        {
                            Assert.Equal(line.Rect, button);
                            var cards = layout.Blocks.Where(block => block.Lines is not [{ Row.Foot: true, Inner: false }]).Max(block => block.Rect.Right);
                            var along = layout.Blocks.Where(block => block.Lines is [{ Row.Foot: true, Inner: false }] && block.Rect.Y == button.Y).Max(block => block.Rect.Right);
                            Assert.Equal(cards - Px(16), along);
                        }
                        break;
                    }
                    // §521: a row's button 32 tall at its end, 120 to 240 wide; a way to add one's too.
                    case PanelRowKind.Action or PanelRowKind.Choice when line.Row.Kind == PanelRowKind.Action || line.Row.Adds:
                        Assert.Equal((right, Px(32)), (controls.Single().Rect.Right, controls.Single().Rect.Height));
                        Assert.InRange(controls.Single().Rect.Width, Px(120), Px(240));
                        break;
                    case PanelRowKind.Stepper when line.Row.Field is null:
                        // A time's, an opacity's: − and + alone.
                        Assert.Equal(right - Px(20), controls[1].Rect.Center.X);
                        break;
                    case PanelRowKind.Stepper:
                        // §521: its value typed in the box before − and +, the three one 240 × 32.
                        Assert.Equal([PanelHitKind.Number, PanelHitKind.Fewer, PanelHitKind.More], controls.Select(each => each.Hit.Kind));
                        Assert.Equal(right - Px(20), controls[2].Rect.Center.X);
                        Assert.Equal(Px(32), controls[2].Rect.Width);
                        Assert.Equal((right - Px(240), controls[1].Rect.X), (controls[0].Rect.X, controls[0].Rect.Right));
                        break;
                    // §535: its buttons 32 tall (a colour's 72 wide), its switch (an account's calendar's) at its end.
                    case PanelRowKind.Item:
                        Assert.All(controls.Where(each => each.Hit.Kind == PanelHitKind.Tool), each => Assert.Equal(Px(32), each.Rect.Height));
                        Assert.Equal(line.Row.Switches, controls.Count > 0 && controls[^1].Hit.Kind == PanelHitKind.Toggle && controls[^1].Rect.Right == right);
                        break;
                }
                // §578: a row that only acts has no name: its button is all of it.
                Assert.True(line.Row.Foot || layout.ControlLeft(line) >= line.Rect.X + Px(48), $"{line.Row.Key}: its control runs over its name");
            }
        }
    }

    /// <summary>
    /// §521 (the person, 2026-11-07: "layouts should get their own separate entry in the settings."), §585 (2026-11-09: "merge
    /// display settings together with layout settings"; §624: "displays and layouts should only be called Layouts"): one entry, Layouts, its page in two sections — Displays,
    /// each display an expander of its Layout (a dropdown of the layouts and None) and Position, "This display" under the one the
    /// flyout is shown on; Layouts, each layout an expander — its name, its icon, §595 whether it is the default (a switch), a copy
    /// and its Delete at its head's end, "This display" under the one shown, the default's badge after its name —, "Add a layout"
    /// under them. A landing at a layout's or a display's rows opens its expander.
    /// </summary>
    [Fact]
    public void TheLayoutsPageHoldsTheDisplaysAndTheLayouts()
    {
        var config = MenuCorpus.Fresh();
        var bar = new BarSpot(MenuCorpus.Display, "One") { Attached = [MenuCorpus.Display] };
        var content = Content();
        Assert.Equal([PanelPage.Karpo, PanelPage.Accounts, PanelPage.Layouts, PanelPage.Taskbar, PanelPage.Appearance], content.Shown);
        Assert.Equal((S.CoreBarMenuLayouts, OptionGlyphs.Monitor), (PanelRows.SideOf(PanelPage.Layouts), PanelRows.GlyphOf(PanelPage.Layouts)));
        var layouts = PanelRows.Page(PanelPage.Layouts, S.CoreBarMenuLayouts, PanelRows.GlyphOf(PanelPage.Layouts), TheDock.LooksItems(config, bar, true, [], LooksScope.Layouts));
        Assert.Equal(
            [S.SettingsSectionsDisplays, "One", S.CoreBarMenuLayouts, .. config.Layouts.Select(each => each.Name), S.SettingsLayoutsAdd],
            layouts.Rows.Select(row => row.Text));
        Assert.Equal(
            [PanelRowKind.Heading, PanelRowKind.Expander, PanelRowKind.Heading, .. config.Layouts.Select(_ => PanelRowKind.Expander), PanelRowKind.Link],
            layouts.Rows.Select(row => row.Kind));
        // The display: an expander of its two, "This display" under its name; §529 Position's four a dropdown.
        var one = layouts.Rows[1];
        Assert.Equal(("Layouts/" + S.SettingsSectionsDisplays + "/One", (string?)S.SettingsSectionsThisDisplay, false, 0), (one.Key, one.Detail, one.Drags, one.Tools.Count));
        Assert.Equal([(S.DockEditLayout, PanelRowKind.Picker), (S.SettingsDisplaysEdgeHeader, PanelRowKind.Picker)], one.Rows.Select(row => (row.Text, row.Kind)));
        Assert.Equal([one.Key], PanelRows.PathTo(layouts.Rows, one.Rows[1].Key));
        // §595: no Default layout row; §636 each layout's expander what the default is for before its button, Set as default.
        var work = layouts.Rows.Single(row => row.Text == "Work");
        Assert.Equal([S.SettingsLayoutsNameHeader, S.SettingsLayoutsIconHeader, S.SettingsLayoutsDefaultSet], work.Rows.Select(row => row.Text));
        var defaults = work.Rows[^1];
        Assert.Equal((PanelRowKind.Action, false, S.SettingsLayoutsDefaultDescription, false), (defaults.Kind, defaults.Greyed, defaults.Detail, defaults.Foot));
        Assert.Equal(work.Key + "/" + S.SettingsLayoutsDefaultSet, defaults.Key);
        // §551: dragged into order by its handle (no ↑ ↓), its Duplicate and its Delete icon buttons at its end.
        Assert.True(work.Drags);
        Assert.Equal([S.SettingsLayoutsDuplicate, S.SettingsLayoutsDelete], work.Tools.Select(tool => tool.Name));
        Assert.Equal((S.SettingsSectionsThisDisplay, (string?)null), (layouts.Rows[3].Detail, work.Detail));
        Assert.Equal((S.SettingsLayoutsDefaultBadge, (string?)null), (layouts.Rows[3].Badge, work.Badge));
        Assert.True(layouts.Rows[3].Rows[^1].Greyed);
        // Every key once.
        Assert.Equal(PanelRows.All(layouts.Rows).Count(), PanelRows.All(layouts.Rows).Select(row => row.Key).Distinct().Count());
        // A layout's name field in its expander: a landing there opens it.
        Assert.Equal([work.Key], PanelRows.PathTo(layouts.Rows, work.Rows[0].Key));
        // R18: none of its settings on another page.
        Assert.All(content.Pages.Where(page => page.Page != PanelPage.Layouts), page => Assert.DoesNotContain(PanelRows.All(page.Rows), row => row.Text == S.SettingsLayoutsDuplicate || row.Text == S.SettingsLayoutsAdd));
    }

    /// <summary>
    /// Every place the pointer reaches is what the keys reach: §575 the bar's entries, the search, the errors, Add and Done
    /// first, as they stand, then each stop of the flyout's page, hit where it stands; greyed rows in the stops (a screen
    /// reader finds them) but not hit by the pointer.
    /// </summary>
    [Fact]
    public void WhatIsHitIsWhatTheKeysReach()
    {
        var words = new Words();
        var size = PanelLayout.SizeOf(Scale, 1548);
        var layout = new PanelLayout(words, Scale, new PanelInput(1, Content(), PanelPage.Accounts, new HashSet<string> { "Accounts/" + S.SettingsAccountsYours + "/me@outlook.com" }), size);
        var stops = layout.Stops();
        Assert.Equal([.. Enumerable.Repeat(PanelHitKind.Nav, 5), PanelHitKind.Errors, PanelHitKind.Add, PanelHitKind.Done], stops.Take(8).Select(stop => stop.Kind));
        foreach (var stop in stops)
        {
            var rect = layout.RectOf(stop);
            if (rect.IsEmpty || layout.Row(stop.Key) is { Greyed: true } || (!PanelLayout.OnBar(stop.Kind) && rect.Bottom > layout.Viewport.Bottom))
            {
                continue;
            }
            Assert.Equal(stop, PanelLayout.OnBar(stop.Kind) ? layout.HitAt(rect.Center) : layout.HitIn(rect.Center));
        }
        Assert.Equal(new PanelHit(PanelHitKind.Nav, Page: PanelPage.Appearance), layout.HitAt(layout.PartOf(new PanelHit(PanelHitKind.Nav, Page: PanelPage.Appearance))!.Rect.Center));
    }

    /// <summary>
    /// §520 decision 11, the keyboard sweep of a flyout (KeyboardSweepTests' rule for a folder's lines): on every page of
    /// every settings menu Karpo has, every expander open, whatever a click reaches — asked every 6 px across the flyout — is
    /// one of the stops the keys walk; nothing is the pointer's alone.
    /// </summary>
    [Fact]
    public void TheKeysReachWhateverAClickReaches()
    {
        var words = new Words();
        var size = new System.Numerics.Vector2(0, 20_000);
        List<string> unreached = [];
        foreach (var menu in MenuCorpus.All().Where(menu => menu.Settings))
        {
            var page = PanelRows.Page(PanelPage.Widget, menu.Name, "", menu.Items);
            var open = PanelRows.All(page.Rows).Where(row => row.Kind == PanelRowKind.Expander).Select(row => row.Key).ToHashSet();
            var layout = new PanelLayout(words, Scale, new PanelInput(1, new SettingsContent([page]), PanelPage.Widget, open), size);
            var stops = layout.Stops().ToHashSet();
            for (var y = 0f; y < layout.FlyoutSize.Y; y += 6)
            {
                for (var x = 0f; x < layout.FlyoutSize.X; x += 6)
                {
                    var hit = layout.HitIn(new System.Numerics.Vector2(x, y));
                    // §592: a tab not chosen reached by the arrows from its strip's chosen one, the keys' stop.
                    if (hit.Kind is PanelHitKind.None or PanelHitKind.Page || stops.Contains(hit) || (hit.Kind == PanelHitKind.Tab && layout.TabsAlong(hit).Any(stops.Contains)))
                    {
                        continue;
                    }
                    unreached.Add($"[{menu.Name}] {hit.Kind} {hit.Key} {hit.Index}");
                }
            }
        }
        Assert.True(unreached.Count == 0, string.Join("\n", unreached.Distinct().Take(40)));
    }

    /// <summary>
    /// §575: a flyout hangs from its entry as every popup of the edit bar hangs from its control (PopupPlace): under it from a
    /// bar at the display's top, over it from a bar at its foot, inside the work area; §578 centred on it; §584 12 DIP off the
    /// bar's edge — not 4 off the entry, which stood it over the bar —, its beak out of the side facing the bar, its tip over
    /// the entry's middle, in the gap and short of the bar.
    /// </summary>
    [Fact]
    public void AFlyoutHangsFromItsEntry()
    {
        var display = new RectI(0, 0, 2880, 1620);
        // A bar at the bottom: the edit bar at the top; a bar at the top alone: the edit bar at the bottom.
        var top = new EditDeckRoom(display, 0, 72, Work: new RectI(0, 0, 2880, 1548));
        var bottom = new EditDeckRoom(display, 72, 0, Work: new RectI(0, 72, 2880, 1620));
        // §596: the folder's entry and the widget's hang theirs as a page's entry does, each its beak over itself.
        var content = Content(widget: true);
        var bar = new PanelLayout(new Words(), Scale, new PanelInput(0, content)).Size;
        foreach (var (room, foot, page) in new[] { (top, false), (bottom, true) }.SelectMany(each => new[] { PanelPage.Taskbar, PanelPage.Folder, PanelPage.Widget }.Select(page => (each.Item1, each.Item2, page))))
        {
            var spot = EditDeckPlan.Place(room, (int)bar.X, (int)bar.Y, Scale);
            Assert.Equal(foot ? Edge.Bottom : Edge.Top, spot.Edge);
            var shown = new FlyoutPlacement(spot.Anchor, new TerraFX.Interop.Windows.RECT { right = display.Right, bottom = display.Bottom }, spot.Edge, GapDip: EditDeckPlan.MarginDip, Align: FlyoutAlign.Center, Margin: 0).PanelAt((int)bar.X, (int)bar.Y, Scale);
            var work = new TerraFX.Interop.Windows.RECT { left = room.Inside.Left, top = room.Inside.Top, right = room.Inside.Right, bottom = room.Inside.Bottom };
            var layout = new PanelLayout(new Words(), Scale, new PanelInput(0, content, page), PanelLayout.SizeOf(Scale, room.Inside.Height));
            Assert.Equal(bar, layout.Size);
            var entry = layout.PartOf(new PanelHit(PanelHitKind.Nav, Page: page))!.Rect;
            var control = EditDeck.HungFrom(shown, entry);
            Assert.Equal((shown.top, shown.bottom, shown.left + (int)entry.X), (control.top, control.bottom, control.left));
            var hung = new FlyoutPlacement(new TerraFX.Interop.Windows.POINT { x = control.left, y = control.bottom }, work, spot.Edge, GapDip: Beak.GapDip, Control: control, Beak: true);
            var flyout = hung.PanelAt((int)layout.FlyoutSize.X, (int)layout.FlyoutSize.Y, Scale);
            Assert.InRange((flyout.left + flyout.right) - (control.left + control.right), -2, 2);
            Assert.Equal(Px(Beak.GapDip), foot ? shown.top - flyout.bottom : flyout.top - shown.bottom);
            Assert.True(flyout.top >= work.top && flyout.bottom <= work.bottom);
            // The beak in the gap, its tip over the entry's middle, 4 DIP short of the bar.
            var tip = Beak.TipAt(control, flyout, Px(8), Px(Beak.TallDip));
            Assert.Equal((control.left + control.right) / 2f, flyout.left + tip);
            var (_, peak, _) = Beak.Triangle(flyout.bottom - flyout.top, foot ? Edge.Bottom : Edge.Top, tip, Px(Beak.TallDip), 0);
            Assert.Equal(Px(Beak.GapDip - Beak.TallDip), foot ? shown.top - (flyout.top + peak.Y) : flyout.top + peak.Y - shown.bottom);
        }
    }

    // --- The add list -------------------------------------------------------------------------------

    /// <summary>§575: the name of the add tree's first widget that can be added: words its search finds.</summary>
    internal static string AddWord => AddList.Picks(AddList.Rows(WidgetType.Bar, turns: false, 1000).Items).First(item => item.Enabled).Text;

    /// <summary>
    /// §575: "+ Add"'s page as the dock builds it (Dock.AddOf): <paramref name="on"/>'s add tree (§590: the bar's or a
    /// folder's), its search its filter; §586 with a layout's counts.
    /// </summary>
    internal static PanelPageView AddPage(IReadOnlyDictionary<string, int>? placed = null, WidgetType on = WidgetType.Bar)
    {
        var (rows, _, _) = AddList.Rows(on, turns: false, 1000, placed);
        var at = PanelPage.Add.ToString();
        return new PanelPageView(PanelPage.Add, S.DockEditAdd, PanelRows.GlyphOf(PanelPage.Add), PanelRows.Picks(rows, at), []) { Filter = typed => PanelRows.Picks(rows, at, typed) };
    }

    /// <summary>
    /// §575 (the person, 2026-11-08: "the add menu should also be adjusted so its style aligns with the style of the other
    /// settings pages"): the add list a page as every other: each section of the add tree a heading, each of its folders an
    /// expander of its rows, each widget a card the whole of which a click adds (its row's command); its own search at the
    /// flyout's top, a stop of the keys, what is typed listing the widgets that match, flat, each saying where it is. Every
    /// widget of the tree is one of its cards.
    /// </summary>
    [Fact]
    public void TheAddListIsAPageOfItsOwn()
    {
        var page = AddPage();
        var (items, _, _) = AddList.Rows(WidgetType.Bar, turns: false, 1000);
        Assert.Equal(items.Where(item => item.Header).Select(item => item.Text), page.Rows.Where(row => row.Kind == PanelRowKind.Heading).Select(row => row.Text));
        Assert.All(PanelRows.All(page.Rows), row => Assert.Contains(row.Kind, new[] { PanelRowKind.Heading, PanelRowKind.Expander, PanelRowKind.Pick }));
        Assert.Equal(AddList.Picks(items).Select(item => item.Id).Order(), PanelRows.All(page.Rows).Where(row => row.Kind == PanelRowKind.Pick).Select(row => row.Item.Id).Order());
        Assert.Equal(PanelRows.All(page.Rows).Count(), PanelRows.All(page.Rows).Select(row => row.Key).Distinct().Count());
        var open = PanelRows.All(page.Rows).Where(row => row.Kind == PanelRowKind.Expander).Select(row => row.Key).ToHashSet();
        var content = new SettingsContent([page]);
        var layout = new PanelLayout(new Words(), Scale, new PanelInput(0, content, PanelPage.Add, open), PanelLayout.SizeOf(Scale, 1548));
        // §593: as wide as the bar, whatever opened it.
        Assert.Equal(layout.Size.X, layout.FlyoutSize.X);
        Assert.Equal(new RectF(Px(16), Px(16), layout.Size.X - Px(32), Px(32)), layout.Filter);
        Assert.Equal(layout.Filter.Bottom, layout.Viewport.Y);
        // Add's entry marked open, §593 whatever opened the page (§576 to §592 not where a "+" did, hung from it).
        Assert.True(layout.Opened(new PanelHit(PanelHitKind.Add)));
        Assert.Equal(new PanelHit(PanelHitKind.Filter), layout.HitIn(layout.Filter.Center));
        Assert.Equal(PanelHitKind.Filter, layout.Stops().First(stop => !PanelLayout.OnBar(stop.Kind)).Kind);
        // A widget's card: the whole of it its click, its row's command.
        var clock = layout.Blocks.SelectMany(block => block.Lines).First(line => line.Row.Kind == PanelRowKind.Pick && !line.Row.Greyed);
        var (hit, rect) = Assert.Single(layout.Controls(clock));
        Assert.Equal((PanelHitKind.Act, clock.Row.Item.Id, clock.Rect), (hit.Kind, hit.Id, rect));
        // Words typed: the widgets that match, flat, where each is under its name.
        var typed = new PanelLayout(new Words(), Scale, new PanelInput(0, content, PanelPage.Add, Filter: AddWord), PanelLayout.SizeOf(Scale, 1548));
        var found = typed.Blocks.Select(block => block.Row).ToList();
        Assert.NotEmpty(found);
        Assert.All(found, row => Assert.Equal((PanelRowKind.Pick, true), (row.Kind, row.Detail is not null)));
        Assert.Equal(AddList.Results(items, AddWord).Select(item => item.Id), found.Select(row => row.Item.Id));
    }

    /// <summary>
    /// §578 (the person, 2026-11-09: "add widgets flyout is structured to sparsely, try to make it more dense, maybe also place
    /// items beside each other"): the add page's widgets tiles, side by side, 4 apart either way, 40 DIP tall — each its own
    /// card the whole of which a click adds, a stop of the keys in reading order —; a folder's widgets tiles in its card, 8 in
    /// from its sides, on its rows' ground; the words typed list them as tiles too, 52 tall for where each is under its name.
    /// §593 as many as 200 DIP each fit across the page, as wide as the edit bar (§578 to §592 three across a flyout's 548 DIP);
    /// a narrower one fewer.
    /// </summary>
    [Fact]
    public void TheAddPagesWidgetsAreTilesSideBySide()
    {
        var page = AddPage();
        var open = PanelRows.All(page.Rows).Where(row => row.Kind == PanelRowKind.Expander).Select(row => row.Key).ToHashSet();
        var content = new SettingsContent([.. Entries().Pages, page]);
        var layout = new PanelLayout(new Words(), Scale, new PanelInput(0, content, PanelPage.Add, open), PanelLayout.SizeOf(Scale, 1548));
        Assert.Equal(layout.Size.X, layout.FlyoutSize.X);
        var w = layout.Size.X - (2 * Px(16));
        var across = (int)MathF.Floor((w + Px(4)) / (Px(200) + Px(4)));
        Assert.True(across >= 4, $"{across} across {w} px");
        var each = MathF.Floor((w - ((across - 1) * Px(4))) / across);
        // The page's own (Popular): each a card of its own, as many to a row as fit.
        var popular = layout.Blocks.Where(block => block.Lines is [{ Tile: true, Inner: false }]).Select(block => block.Lines[0]).ToList();
        Assert.True(popular.Count > across);
        Assert.All(popular, tile => Assert.Equal((each, Px(40)), (tile.Rect.Width, tile.Rect.Height)));
        Assert.Equal(Enumerable.Range(0, across).Select(at => Px(16) + (at * (each + Px(4)))), popular.Take(across).Select(tile => tile.Rect.X));
        Assert.All(popular.Take(across), tile => Assert.Equal(popular[0].Rect.Y, tile.Rect.Y));
        Assert.Equal((Px(16), popular[0].Rect.Bottom + Px(4)), (popular[across].Rect.X, popular[across].Rect.Y));
        // A folder's (Browse's): tiles in its card, 8 in, on its rows' ground.
        var folder = layout.Blocks.First(block => block.Lines.Count > 1 && block.Lines.Skip(1).All(line => line.Tile));
        Assert.Equal(Px(40), folder.Lines[0].Rect.Height);
        Assert.All(layout.Blocks.Where(block => block.Row.Kind == PanelRowKind.Expander), block => Assert.Equal(Px(40), block.Lines[0].Rect.Height));
        var inside = folder.Lines.Skip(1).ToList();
        Assert.All(inside, tile => Assert.True(tile.Inner));
        Assert.Equal((folder.Rect.X + Px(8), folder.Lines[0].Rect.Bottom + Px(8)), (inside[0].Rect.X, inside[0].Rect.Y));
        Assert.Equal(folder.Rect.Bottom - Px(8), inside.Max(tile => tile.Rect.Bottom));
        Assert.All(inside, tile => Assert.True(tile.Rect.Right <= folder.Rect.Right - Px(8)));
        // Each the whole of it its click, a stop of the keys in reading order; its icon 12 in, its name 12 after it.
        var stops = layout.Stops().Where(stop => stop.Kind == PanelHitKind.Act).Select(stop => stop.Key).ToList();
        Assert.Equal(layout.Blocks.SelectMany(block => block.Lines).Where(line => line.Tile).Select(line => line.Row.Key), stops);
        var first = popular[0];
        Assert.Equal(new PanelHit(PanelHitKind.Act, first.Row.Item.Id, Key: first.Row.Key), layout.HitIn(layout.OnPanel(first.Rect).Center));
        Assert.Equal((first.Rect.X + Px(12) + (Px(16) / 2), first.Rect.X + Px(40)), (layout.IconX(first), layout.NameX(first)));
        // Typed: the matches tiles, two lines tall.
        var typed = new PanelLayout(new Words(), Scale, new PanelInput(0, content, PanelPage.Add, Filter: AddWord), PanelLayout.SizeOf(Scale, 1548));
        Assert.All(typed.Blocks.SelectMany(block => block.Lines), tile => Assert.Equal((true, Px(52)), (tile.Tile, tile.Rect.Height)));
        // Narrower: as many as 200 DIP each fit.
        var rows = page.Rows.Where(row => row.Kind == PanelRowKind.Pick).ToList();
        Assert.Equal(2, layout.Tiles(rows, 0, Px(420), 0).Select(tile => tile.Rect.Y).TakeWhile(y => y == 0).Count());
        Assert.Single(layout.Tiles(rows, 0, Px(400), 0).Select(tile => tile.Rect.Y).TakeWhile(y => y == 0));
    }

    /// <summary>
    /// §578 (the person, 2026-11-09: "reset to default and add an entry should not get the dark big background buttons"): a row
    /// that only acts — a button with nothing to say before it — is its button alone, no card: Add a layout under the layouts,
    /// 8 off them, as wide as its glyph and words; §585 in an expander (Add an entry and Reset to default in the taskbar
    /// menu's) each in its line; §599 each at the end of its line, where the value controls end; a way to add one asks for its
    /// list, a reset acts.
    /// </summary>
    [Fact]
    public void ARowThatOnlyActsIsItsButtonAlone()
    {
        var content = Content();
        // On a page: Add a layout under the layouts, 8 off them, as wide as its glyph and words.
        var page = new PanelLayout(new Words(), Scale, new PanelInput(0, content, PanelPage.Layouts), PanelLayout.SizeOf(Scale, 1548));
        var lines = page.Blocks.SelectMany(block => block.Lines).ToList();
        var layoutAdd = lines.Single(line => line.Row.Text == S.SettingsLayoutsAdd);
        Assert.True(layoutAdd.Row.Foot);
        // §599 at the end of the page's lines, where a card's controls end.
        var cards = page.Blocks.Where(block => !block.Row.Foot).Max(block => block.Rect.Right);
        Assert.Equal((cards - Px(16), Px(32), page.FootWide(layoutAdd.Row)), (layoutAdd.Rect.Right, layoutAdd.Rect.Height, layoutAdd.Rect.Width));
        Assert.Equal(lines[lines.IndexOf(layoutAdd) - 1].Rect.Bottom + Px(8), layoutAdd.Rect.Y);
        // §628: it leads to its page, a link (§578 to §627 its list under it).
        Assert.Equal([(PanelHitKind.Link, layoutAdd.Rect)], page.Controls(layoutAdd).Select(each => (each.Hit.Kind, each.Rect)));
        // Each block its button alone.
        Assert.Single(page.Blocks, block => block.Lines is [var only] && only == layoutAdd);
        // §585: in an expander (the taskbar menu's, Behavior's last row) each its button alone in its line: Add an entry, then
        // Reset to default under it.
        var layout = new PanelLayout(new Words(), Scale, new PanelInput(0, content, PanelPage.Taskbar, BarMenuOpen), PanelLayout.SizeOf(Scale, 1548));
        lines = [.. layout.Blocks.SelectMany(block => block.Lines)];
        var add = lines.Single(line => line.Row.Text == S.SettingsBarMenuAdd);
        var reset = lines.Single(line => line.Row.Text == S.SettingsBarMenuReset);
        Assert.True(add.Row.Foot && reset.Row.Foot && add.Inner && reset.Inner);
        var (addButton, resetButton) = (layout.FootAt(add), layout.FootAt(reset));
        Assert.Equal((Px(32), layout.FootWide(add.Row)), (addButton.Height, addButton.Width));
        // §599 each at its line's end, where the expander's controls end.
        Assert.Equal((layout.ControlEnd(add), layout.ControlEnd(reset), add.Rect.Bottom), (addButton.Right, resetButton.Right, reset.Rect.Y));
        Assert.Equal([(PanelHitKind.Pop, addButton)], layout.Controls(add).Select(each => (each.Hit.Kind, each.Rect)));
        Assert.Equal([(PanelHitKind.Act, resetButton)], layout.Controls(reset).Select(each => (each.Hit.Kind, each.Rect)));
        Assert.Equal(PanelGlyphs.Add.Length > 0, PanelLayout.FootGlyph(add.Row).Length > 0);
    }

    /// <summary>
    /// §578 (the person, 2026-11-09: "the online dropdown selectors should all receive icons if possible"): every dropdown whose
    /// choices wear icons wears the one of the choice in force in its box, in one place (PanelRow.ValueIcon) — none of several
    /// widgets apart, none of a way to add one —; the dropdowns that wore none now do: Animations, Combine, Show; §597 the map,
    /// the weather site and the finance site their sites' own logos, the one in force in the box.
    /// </summary>
    [Fact]
    public void EveryDropdownWearsTheIconOfItsChoice()
    {
        var content = Content(widget: true);
        // §672: a choice of one an inline picker on every page, its head wearing it as a dropdown's box did.
        var dropdowns = content.Pages.SelectMany(page => PanelRows.All(page.Rows)).Where(row => row.Kind is PanelRowKind.Choice or PanelRowKind.Picker && !PanelPickers.Colours(row.Item)).ToList();
        foreach (var row in dropdowns)
        {
            var chosen = row.Item.Children?.FirstOrDefault(kid => kid.Checked == true && kid.Control is null);
            var want = row.Adds || row.Mixed ? null : row.Item.Value?.Icon ?? chosen?.Icon;
            Assert.Equal(want, row.ValueIcon);
        }
        foreach (var name in new[] { S.SettingsGeneralAnimationsHeader, S.SettingsGeneralCombineHeader, S.SettingsLayoutsShowHeader })
        {
            var row = dropdowns.FirstOrDefault(each => each.Text == name);
            Assert.True(row is not null, $"{name}: no dropdown ({string.Join(", ", content.Pages.SelectMany(page => PanelRows.All(page.Rows)).Where(each => each.Text == name).Select(each => each.Kind))})");
            Assert.All(row.Item.Children!.Where(kid => kid.Checked is not null), kid => Assert.NotNull(kid.Icon));
            Assert.NotNull(row.ValueIcon);
        }
        var animations = dropdowns.First(each => each.Text == S.SettingsGeneralAnimationsHeader);
        Assert.Equal(animations.Item.Children!.Count(kid => kid.Checked is not null), animations.Item.Children!.Where(kid => kid.Checked is not null).Select(kid => kid.Icon).Distinct().Count());
        // §597: Online's sites, each its own logo; the box the logo of the site in force (a config that never chose: each kind's default).
        foreach (var (name, site) in new[] { (S.SettingsMapProviderHeader, LinkProviders.DefaultMap.Name()), (S.SettingsLinksWeatherHeader, LinkProviders.DefaultWeather.Name()), (S.SettingsLinksMarketsHeader, LinkProviders.DefaultMarkets.Name()) })
        {
            var row = dropdowns.Single(each => each.Text == name);
            var sites = row.Item.Children!.Where(kid => kid.Checked is not null).ToList();
            Assert.All(sites, kid => Assert.IsType<FlyoutIcon.Picture>(kid.Icon));
            Assert.Equal(sites.Count, sites.Select(kid => kid.Icon).Distinct().Count());
            Assert.Equal(sites.Single(kid => kid.Text == site).Icon, row.ValueIcon);
        }
    }

    /// <summary>§597: a site picked, the box wears that site's logo: Online's map set to Apple Maps, the finance site to TradingView.</summary>
    [Fact]
    public void TheBoxWearsTheChosenSitesLogo()
    {
        var content = Content(edit: config => (config.MapProvider, config.MarketsProvider, config.WeatherProvider) = (MapProvider.Apple, MarketsProvider.TradingView, WeatherProvider.Yr));
        // §672: each its picker inline, its head wearing the logo.
        var rows = PanelRows.All(content.Of(PanelPage.Karpo)!.Rows).Where(row => row.Kind == PanelRowKind.Picker).ToList();
        foreach (var (name, file) in new[] { (S.SettingsMapProviderHeader, SiteLogo.File(MapProvider.Apple, 32)), (S.SettingsLinksMarketsHeader, SiteLogo.File(MarketsProvider.TradingView, 32)), (S.SettingsLinksWeatherHeader, SiteLogo.File(WeatherProvider.Yr, 32)) })
        {
            var row = rows.Single(each => each.Text == name);
            Assert.Equal(MenuPictures.Of(file!), row.ValueIcon);
            Assert.NotEqual(MenuPictures.Of(SiteLogo.File(MapProvider.OpenStreetMap, 32)!), row.ValueIcon);
        }
    }

    // --- Escape -------------------------------------------------------------------------------------

    /// <summary>§520 decision 4: Escape from the inside out: carried, ants, the search's words, §575 the flyout, a folder, edit mode.</summary>
    [Fact]
    public void EscapeGoesFromTheInsideOut()
    {
        Assert.Equal(EditEscape.Cancel, EditKeys.Escape(carrying: true, copied: true, folderOpen: true, searching: true, flyoutOpen: true));
        Assert.Equal(EditEscape.Uncopy, EditKeys.Escape(carrying: false, copied: true, folderOpen: true, searching: true, flyoutOpen: true));
        Assert.Equal(EditEscape.ClearSearch, EditKeys.Escape(carrying: false, copied: false, folderOpen: true, searching: true, flyoutOpen: true));
        Assert.Equal(EditEscape.CloseFlyout, EditKeys.Escape(carrying: false, copied: false, folderOpen: true, searching: false, flyoutOpen: true));
        Assert.Equal(EditEscape.CloseFolder, EditKeys.Escape(carrying: false, copied: false, folderOpen: true, searching: false, flyoutOpen: false));
        Assert.Equal(EditEscape.Leave, EditKeys.Escape(carrying: false));
    }
}
