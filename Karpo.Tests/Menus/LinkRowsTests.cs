// menus: every setting at one level, a link where a lower one needs it (docs\widgets.md §351; Karpo.Core\
// OptionLinks.cs). Held here: which menus link where, a link row as the menu shows it — the arrow out at its
// end, no chevron, the menu closing, what it opens said to a screen reader — and the dock's way there. Where
// each lands is EditEntriesTests'; that no setting is written from two levels ConfigCoverageTests'.
// Pictures (KARPO_UI_TESTS=1, KARPO_PNG_DIR), dark, 150 %: links-mail-dark.png, links-calendar-dark.png,
// links-separator-dark.png, links-divider-dark.png.

using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using Karpo.Tests.Localization;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Menus;

[Collection(InLanguage.Collection)]
public sealed class LinkRowsTests
{
    private const float Scale = 1.5f;

    private static readonly OptionPlace Place = new(new Config(), Dark: true);

    /// <summary>The links of <paramref name="rows"/>, flattened, by where they lead.</summary>
    private static OptionLink[] Links(IEnumerable<OptionRow> rows) =>
        [.. OptionRows.Flat(rows).Where(row => row.Kind == OptionRowKind.Link).Select(row => row.Link)];

    /// <summary>
    /// A widget that shows what the accounts hold links to the Accounts page (§535; §529: a calendar's holidays named in that line,
    /// ⚙ › Public holidays no link of its own), a separator and a divider to Layout ▾, whose Style draws them (§478), the faces alike.
    /// Nothing of what they lead to is in those menus any more. §361: the display's and the layout's menus
    /// link nowhere: a layout's "Show on this display" stands in Display ▾ with the layouts, an action.
    /// </summary>
    [Fact]
    public void EachMenuLinksToWhatAHigherLevelOwns()
    {
        Assert.Equal([OptionLink.Accounts], Links(OptionRows.Of(ListKind.Mail.New(), null, Place)));
        Assert.Equal([OptionLink.Accounts], Links(OptionRows.Of(ListKind.Todo.New(), null, Place)));
        // §529: a calendar's holidays are named in its accounts' line, no link of their own.
        Assert.Equal([OptionLink.Accounts], Links(OptionRows.Of(ListKind.Month.New(), null, Place)));
        // §505: a divider has no look, and so no way to one.
        Assert.Empty(Links(OptionRows.Of(ListKind.Divider.New(), null, Place)));
        Assert.Empty(Links(OptionRows.Of(ListKind.Heading.New(), null, Place)));
        var folder = BarKind.FolderList.New();
        folder.Folder!.Faces = [FolderFace.Of(FolderFaces.CalendarId), FolderFace.Of(FolderFaces.ContactsId), FolderFace.Of(FolderFaces.WifiId)];
        Assert.Equal([OptionLink.Accounts], Links(OptionRows.OfFace(folder.Folder, 0, null, Place)));
        Assert.Equal([OptionLink.Accounts], Links(OptionRows.OfFace(folder.Folder, 1, null, Place)));
        Assert.Empty(Links(OptionRows.OfFace(folder.Folder, 2, null, Place)));

        var config = new Config();
        config.EditScreen("\\\\.\\DISPLAY1", "One");
        // §423: the folder room's foot links to Look; §507: no styles to link to (§459 to §506 there were); §520 the bar's
        // right click opens the panel's Taskbar page, its pages linking to nothing of their own.
        // §628: the Layouts page's Add a layout to its own page.
        OptionLink[] Expected(LooksScope scope) => scope switch
        {
            LooksScope.Folders => [OptionLink.Appearance],
            LooksScope.Layouts => [OptionLink.AddLayout],
            _ => [],
        };
        Assert.All(Enum.GetValues<LooksScope>(), scope => Assert.Equal(Expected(scope), Links(BarLooks.Rows(config, new BarSpot("\\\\.\\DISPLAY1", "One"), dark: true, scope: scope))));

        // What they lead to is set there alone: no holiday calendars, no accounts, no separators' look here.
        var calendar = OptionRows.Flat(OptionRows.Of(ListKind.Month.New(), null, Place)).Select(row => row.Key).ToList();
        Assert.DoesNotContain(OptionHolidays.Calendars, calendar);
        Assert.DoesNotContain(OptionAccountRows.Accounts, calendar);
        Assert.DoesNotContain(OptionRows.Flat(OptionRows.Of(ListKind.Divider.New(), null, Place)), row => row.Key.StartsWith("StyleSeparators", StringComparison.Ordinal));
    }

    /// <summary>
    /// A link as the menu shows it: its owner's name and icon, the arrow out where a value stands, no
    /// chevron; picked, the menu closes (no stay) and the pick names where it goes; a screen reader is told
    /// what it opens. The dock goes there by the edit bar's ways in (Dock.EditEntries.cs).
    /// </summary>
    [Fact]
    public void ALinkIsARowWithTheArrowOutThatClosesTheMenu()
    {
        foreach (var link in Enum.GetValues<OptionLink>())
        {
            List<OptionPick> picks = [];
            var item = OptionMenu.Rows([OptionLinks.Row(link, OptionSlot.Subject)], picks).Single();
            Assert.Equal((OptionLinks.Title(link), OptionLinks.Opens(link)), (item.Text, item.Opens));
            Assert.Equal(new FlyoutIcon.Glyph(OptionLinks.Glyph(link)), item.Icon);
            Assert.Equal(new FlyoutIcon.Glyph(OptionMenu.GlyphLink), item.Value!.Icon);
            Assert.Null(item.Children);
            Assert.False(item.Stays);
            var pick = OptionMenu.PickOf(picks, item.Id)!;
            Assert.Equal((OptionPickKind.Link, (int)link), (pick.Kind, pick.Value));
            Assert.False(OptionMenu.KeepsOpen(pick.Kind));
            Assert.False(OptionMenu.ApplyRow(OptionLinks.Row(link, OptionSlot.Subject), pick));
        }
        Assert.Equal(S.DockMenuLinkOpens(S.SettingsNavAccounts, S.SettingsNavAccounts), OptionLinks.Opens(OptionLink.Accounts));
        Assert.Contains("OpenEditMenuLater(what.Owner, EditEntries.Of((OptionLink)pick.Value));", RepoSource.Read("Karpo.Dock", "Dock.Folders.cs"));
    }

    // --- Pictures -----------------------------------------------------------------------------------

    /// <summary>
    /// A mail line's and a calendar's menus (§505: a separator's and a divider's link no more), dark, 150 %, the pointer on
    /// their one link, "Manage accounts" (§529: a line of what they draw on, a calendar's public holidays named in it, no
    /// link of their own); a screen reader told what it opens.
    /// </summary>
    [UiFact]
    public void TheLinksArePictured()
    {
        WindowShots.OnHiddenDesktop("link-rows", (gfx, instance) =>
        {
            (WidgetItem Item, string File)[] menus =
            [
                (ListKind.Mail.New(), "links-mail-dark.png"),
                (ListKind.Month.New(), "links-calendar-dark.png"),
            ];
            foreach (var (item, file) in menus)
            {
                var items = OptionMenu.Settings(item, [], null, Place, FolderSide.Face, roomCells: null);
                var work = new RECT { right = (int)(1920 * Scale), bottom = (int)(1040 * Scale) };
                using var menu = new Flyout(gfx, instance, 0) { Configures = true, Configuration = true, TypeTitle = IslandEdits.Kind(item) };
                menu.Show(gfx, AddListPictures.LookFor(dark: true), Scale, items, FlyoutLayout.List, new POINT { x = work.right / 3, y = work.bottom }, work, Edge.Bottom, null);
                WindowShots.Settle();
                var link = items.Single(each => each.Opens is not null);
                Assert.Equal((S.DockWidgetMenuManageAccounts, OptionLinks.Opens(OptionLink.Accounts)), (link.Text, link.Opens));
                menu.PointTo(link.Id);
                WindowShots.Settle(300);
                Assert.Contains(menu.AccessTree.Root.Children, node => node.Name == link.Text && node.Help == link.Opens);
                Assert.True(WindowShots.SaveTogether([.. menu.OpenPanels], file, (0, 0, 0)));
            }
        });
    }
}
