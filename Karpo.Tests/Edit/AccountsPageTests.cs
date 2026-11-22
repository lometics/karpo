// edit, §535: the accounts a page of their own in the settings panel, and every colour a colour button (docs\widgets.md
// §535; the person, 2026-11-07: "accounts should get their complete own section." and "right now it is just a color
// button, but the button should show the color"). Held here:
//
//  - **The page**: Accounts a side item right after Karpo, its glyph the accounts'; on it every account (one with parts
//    an expander, their switches and colours in it; Sign in again and Remove at its head's end), the ways to add one, the
//    forms. No row of the accounts lost moving there (their rows' keys, as a pick finds its row), none left on the Karpo
//    page; the search finds them there; "Manage accounts" lands there.
//  - **Colour buttons**: a colour row's control and a thing's colour (an account's calendar's, a holiday calendar's) a
//    colour button showing the colour in force — what it paints is the row's colour —, no "Color" words; an account's
//    calendar's switch at its row's end, a click on it its switch's pick.

using Karpo.Dock;
using Karpo.Tests.Menus;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Edit;

public sealed class AccountsPageTests
{
    private const float Scale = 1.5f;

    private static float Px(float dip) => MathF.Round(dip * Scale, MidpointRounding.AwayFromZero);

    /// <summary>The Accounts page as the dock builds it (Dock.PanelOf), its commands into <paramref name="picks"/>.</summary>
    private static PanelPageView Page(List<OptionPick> picks) =>
        PanelRows.Page(PanelPage.Accounts, S.SettingsNavAccounts, PanelRows.GlyphOf(PanelPage.Accounts), TheDock.SettingsItems(MenuCorpus.Fresh(), MenuCorpus.Services(), picks, page: KarpoPage.Accounts));

    /// <summary>The keys of the picks a page's controls send: its rows', their buttons', a popup's choices (at any depth).</summary>
    internal static HashSet<string> KeysSent(PanelPageView page, List<OptionPick> picks)
    {
        HashSet<string> keys = new(StringComparer.Ordinal);
        void Item(FlyoutItem item)
        {
            if (OptionMenu.PickOf(picks, item.Id) is { Kind: not OptionPickKind.None } pick)
            {
                _ = keys.Add(pick.Key);
            }
            foreach (var tool in (item.Control as FlyoutControl.Tools)?.Buttons ?? [])
            {
                if (OptionMenu.PickOf(picks, tool.Id) is { Kind: not OptionPickKind.None } sent)
                {
                    _ = keys.Add(sent.Key);
                }
                foreach (var each in tool.Level ?? [])
                {
                    Item(each);
                }
            }
            foreach (var kid in item.Children ?? [])
            {
                Item(kid);
            }
        }
        foreach (var row in PanelRows.All(page.Rows))
        {
            Item(row.Item);
            // §551: a row put in order sends its move as it is dragged (OptionMenu.Moved: Dock.PanelMoved).
            if (row.Drags && OptionMenu.Moved(OptionMenu.PickOf(picks, row.Item.Id), 0) is { } move)
            {
                _ = keys.Add(move.Key);
            }
        }
        return keys;
    }

    /// <summary>Accounts is a side item of its own, right after Karpo, the accounts' glyph; "Manage accounts" lands on it.</summary>
    [Fact]
    public void AccountsIsASideItemOfItsOwn()
    {
        var content = SettingsPanelTests.Content();
        Assert.Equal([PanelPage.Karpo, PanelPage.Accounts], content.Shown.Take(2));
        var page = content.Of(PanelPage.Accounts)!;
        Assert.Equal((S.SettingsNavAccounts, S.SettingsNavAccounts, OptionGlyphs.Accounts), (page.Side, page.Title, page.Glyph));
        Assert.Equal(new EditLanding(PanelPage.Accounts), EditEntries.Landing(EditEntries.Of(OptionLink.Accounts)));
        // A widget's "Manage accounts" link says where it leads: the page itself.
        Assert.Equal(S.SettingsNavAccounts, PanelRows.LeadOf(OptionMenu.LinkItem(OptionLinks.Row(OptionLink.Accounts, OptionSlot.Subject), 1)));
    }

    /// <summary>
    /// The page: under "Your accounts" each account — one with parts an expander of them, its Sign in again and Remove at its
    /// head's end; one without a row with its Remove —; under "Add an account" a Microsoft and (§687) a Google account's buttons and the mail
    /// and CalDAV forms, expanders of their text boxes.
    /// </summary>
    [Fact]
    public void ThePageHoldsEveryAccountAndTheWaysToAddOne()
    {
        var page = Page([]);
        Assert.Equal(
            [
                (PanelRowKind.Heading, S.SettingsAccountsYours), (PanelRowKind.Expander, "me@outlook.com"), (PanelRowKind.Item, "me@example.com"),
                (PanelRowKind.Heading, S.SettingsAccountsAddHeader), (PanelRowKind.Action, S.SettingsAccountsAddMicrosoft), (PanelRowKind.Action, S.SettingsAccountsAddGoogle),
                (PanelRowKind.Expander, S.SettingsAccountsAddMail), (PanelRowKind.Expander, S.SettingsAccountsAddDav),
            ],
            page.Rows.Select(row => (row.Kind, row.Text)));
        var ms = page.Rows[1];
        Assert.Equal([S.DockMenuAccountsSignInAgain, S.SettingsCommonRemove], ms.Tools.Select(tool => tool.Name));
        // §646: its mail one link, Mail folders, its inbox and folders on that page.
        Assert.Equal([S.SettingsAccountsGivesCalendars, "Work", "Home", S.SettingsAccountsGivesMail, S.SettingsAccountsMailFolders], ms.Rows.Select(row => row.Text));
        Assert.Equal([S.SettingsCommonRemove], page.Rows[2].Tools.Select(tool => tool.Name));
        Assert.Contains(page.Rows[6].Rows, row => row.Kind == PanelRowKind.Text && row.Text == S.SettingsAccountsPasswordHeader);
    }

    /// <summary>
    /// §638 (the person, 2026-11-09: "the accounts when collapsed should show the amount of items totally synced"): an
    /// account's head, shut, says everything synced of it at its end before its buttons, 12 off them, in Caption — its name
    /// and its address never under it —; open, nothing (its rows say it); a screen reader hears it as the head's value. One
    /// with nothing counted (the mail account, a row of its buttons) says nothing.
    /// </summary>
    [Fact]
    public void AnAccountShutSaysEverythingSyncedOfItAtItsHeadsEnd()
    {
        var content = SettingsPanelTests.Content();
        PanelLayout Laid(bool open) => new(new SettingsPanelTests.Words(), Scale,
            new PanelInput(0, content, PanelPage.Accounts, open ? new HashSet<string> { PanelPage.Accounts + "/" + S.SettingsAccountsYours + "/me@outlook.com" } : new HashSet<string>()),
            PanelLayout.SizeOf(Scale, 4000));
        var shut = Laid(open: false);
        var head = shut.Blocks.SelectMany(block => block.Lines).Single(line => line.Row.Text == "me@outlook.com");
        Assert.Equal((PanelRowKind.Expander, MenuCorpus.MsHolds), (head.Row.Kind, head.Row.Value));
        Assert.Equal("56 events · " + RegionFormats.Count(1234) + " messages", MenuCorpus.MsHolds);
        Assert.False(shut.StandsOpen(head.Row));
        var tools = shut.Controls(head).Where(each => each.Hit.Kind == PanelHitKind.Tool).Min(each => each.Rect.X);
        var said = MathF.Ceiling(new SettingsPanelTests.Words().Width(MenuCorpus.MsHolds, Px(PanelMetrics.Caption)));
        Assert.Equal(tools - Px(PanelMetrics.Label) - said, shut.ControlLeft(head), 3);
        Assert.True(shut.NameX(head) + shut.NameRoom(head) + Px(PanelMetrics.Gutter) <= tools - Px(PanelMetrics.Label) - said + 0.5f);
        var heard = DeckUia.Tree(shut, new TerraFX.Interop.Windows.RECT { right = 900, bottom = 900 }, PanelHit.None, bar: false);
        Assert.Contains(Flat(heard.Root), node => node.Name == "me@outlook.com" && node.Status == MenuCorpus.MsHolds);
        var mail = shut.Blocks.SelectMany(block => block.Lines).Single(line => line.Row.Text == "me@example.com");
        Assert.Null(mail.Row.Value);

        var open = Laid(open: true);
        var opened = open.Blocks.SelectMany(block => block.Lines).Single(line => line.Row.Text == "me@outlook.com");
        Assert.True(open.StandsOpen(opened.Row));
        Assert.Equal(open.Controls(opened).Where(each => each.Hit.Kind == PanelHitKind.Tool).Min(each => each.Rect.X), open.ControlLeft(opened), 3);

        static IEnumerable<UiaNode> Flat(UiaNode node) => [node, .. node.Children.SelectMany(Flat)];
    }

    /// <summary>
    /// No row of the accounts lost moving to their page: every key the accounts' rows have (each account's Sign in again and
    /// Remove, each part's switch and colour, a Microsoft account added, every field of the forms) is sent by a control of
    /// the page; and none by the Karpo page any more.
    /// </summary>
    [Fact]
    public void NoAccountsRowIsLostAndNoneIsLeftOnTheKarpoPage()
    {
        List<OptionPick> picks = [];
        var sent = KeysSent(Page(picks), picks);
        string[] wanted =
        [
            OptionAccountRows.Again + "ms", OptionAccountRows.Remove + "ms", OptionAccountRows.Remove + "imap",
            OptionAccountRows.Part + "ms|calendar:work", OptionAccountRows.Part + "ms|calendar:home",
            OptionAccountRows.Colour + "ms|calendar:work", OptionAccountRows.Colour + "ms|calendar:home",
            // §646: the mail's parts are its Mail folders page's (MailFoldersPageTests), its link here.
            OptionAccountRows.Folders + "ms",
            OptionAccountRows.AddMicrosoft, OptionAccountRows.AddGoogle,
            OptionAccountRows.Form + "Mail.Address", OptionAccountRows.Form + "Mail.Password", OptionAccountRows.Form + "Mail.Own", OptionAccountRows.Form + "Mail.Test", OptionAccountRows.Form + "Mail.Add",
            OptionAccountRows.Form + "Dav.Address", OptionAccountRows.Form + "Dav.Password", OptionAccountRows.Form + "Dav.Own", OptionAccountRows.Form + "Dav.Test", OptionAccountRows.Form + "Dav.Add",
        ];
        Assert.All(wanted, key => Assert.Contains(key, sent));
        // Every key the page's builder made a pick for, a control of the page sends.
        Assert.Equal(picks.Where(pick => pick.Kind != OptionPickKind.None).Select(pick => pick.Key).ToHashSet(), sent);

        List<OptionPick> karpo = [];
        _ = TheDock.SettingsItems(MenuCorpus.Fresh(), MenuCorpus.Services(), karpo);
        Assert.DoesNotContain(karpo, pick => pick.Key.StartsWith(OptionAccountRows.Accounts, StringComparison.Ordinal));
        var page = SettingsPanelTests.Content().Of(PanelPage.Karpo)!;
        Assert.DoesNotContain(PanelRows.All(page.Rows), row => row.Text == S.SettingsNavAccounts ||row.Text.Contains("me@outlook.com", StringComparison.Ordinal));
    }

    /// <summary>
    /// An account's calendar: its colour a colour button showing the colour it wears (Work its Automatic, Home the one
    /// picked), 68 × 32, no words; its switch at the row's end, a click on it its switch's pick, a click on the button its
    /// colours' popup.
    /// </summary>
    [Fact]
    public void AnAccountsCalendarShowsItsColourAndItsSwitch()
    {
        List<OptionPick> picks = [];
        var page = Page(picks);
        var ms = page.Rows[1];
        PanelRow Part(string name) => ms.Rows.Single(row => row.Text == name);
        Assert.Equal(
            [Argb.Parse("#038387"), Argb.Parse("#E3008C")],
            new[] { "Work", "Home" }.Select(name => PanelSwatch.Of(Assert.Single(Part(name).Tools))!.Color));
        Assert.Equal([true, false], new[] { "Work", "Home" }.Select(name => Part(name).On));
        Assert.All(new[] { "Work", "Home" }, name => Assert.True(Part(name).Switches && Part(name).Kind == PanelRowKind.Item));

        var content = new SettingsContent([page]);
        var layout = new PanelLayout(new SettingsPanelTests.Words(), Scale, new PanelInput(0, content, PanelPage.Accounts, new HashSet<string> { ms.Key }), PanelLayout.SizeOf(Scale, 1548));
        var line = layout.LineOf(Part("Home").Key)!;
        var controls = layout.Controls(line);
        Assert.Equal([PanelHitKind.Tool, PanelHitKind.Toggle], controls.Select(each => each.Hit.Kind));
        var (button, rect) = controls[0];
        Assert.Equal((Px(PanelMetrics.ColourWide), Px(32)), (rect.Width, rect.Height));
        Assert.True(rect.Right < controls[1].Rect.X);
        Assert.Equal((OptionPickKind.Flip, OptionAccountRows.Part + "ms|calendar:home"), (OptionMenu.PickOf(picks, controls[1].Hit.Id)!.Kind, OptionMenu.PickOf(picks, controls[1].Hit.Id)!.Key));
        Assert.Equal(OptionAccountRows.Colour + "ms|calendar:home", OptionMenu.PickOf(picks, button.Id)!.Key);
        Assert.NotNull(Part("Home").Tools[0].Level);
        // Its name has its room up to a gutter before the button (§548).
        Assert.Equal(rect.X, layout.ControlLeft(line));
        Assert.Equal(rect.X - Px(PanelMetrics.Gutter), layout.NameX(line) + layout.NameRoom(line));
    }

    /// <summary>
    /// Every colour of the panel is a colour button: a colour row's (the Appearance page's) shows the colour in force, what it is set
    /// to said before it; a holiday calendar's (the Karpo page's) its colour, no words. No "Color" button of words anywhere.
    /// </summary>
    [Fact]
    public void EveryColourIsAColourButton()
    {
        var content = SettingsPanelTests.Content(widget: true);
        var rows = content.Pages.SelectMany(page => PanelRows.All(page.Rows)).ToList();
        // §670: a colour row its picker inline, its swatch in its head.
        var colours = rows.Where(row => row.Kind == PanelRowKind.Picker && PanelPickers.Colours(row.Item)).ToList();
        Assert.NotEmpty(colours);
        Assert.All(colours, row => Assert.NotNull(row.Swatch));
        // Each tool that is a colour (FlyoutTool.Swatch) is drawn as one; the holidays' and the accounts' among them.
        var tools = rows.SelectMany(row => row.Tools).Where(tool => tool.Swatch is not null).ToList();
        // Two calendars' and a holiday calendar's; §646 the inbox's is on its Mail folders page (MailFoldersPageTests).
        Assert.True(tools.Count >= 3, $"{tools.Count} colour buttons");
        Assert.All(tools, tool => Assert.Equal(tool.Swatch, PanelSwatch.Of(tool)!.Color));
        Assert.All(rows.SelectMany(row => row.Tools).Where(tool => tool.Name == S.SettingsAccountsColorHeader), tool => Assert.NotNull(PanelSwatch.Of(tool)));
        var holidays = PanelRows.All(content.Of(PanelPage.Karpo)!.Rows).First(row => row.Tools.Any(tool => tool.Swatch is not null));
        Assert.Equal(HolidayPlaces.Name("CH"), holidays.Text);
    }
}
