// edit: every way into Karpo's settings from outside the edit bar (docs\widgets.md §340; Karpo.Dock\
// Dock.EditEntries.cs). The settings app is gone, so nothing may start it; each way opens edit mode with
// the edit bar's settings panel on the page that holds what it asked for, at its row (§520). Held here:
//
//  - nothing in the dock, Core or the accounts starts a settings app, or names its way in;
//  - each way lands on a row the page it opens really has, an expander of its own;
//  - each menu's and face's row, the add list's Get Karpo Pro and a second start go that way;
//  - a second start asks the running dock for ⚙, an automatic one (Windows' Run entry, the logon
//    task, the guard's restart) and a test's nothing.

using System.Text.RegularExpressions;
using Karpo.Dock;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Edit;

public sealed class EditEntriesTests
{
    private const string Display = "\\\\.\\DISPLAY1";

    /// <summary>
    /// What a start of the settings app was made of, or how the dock and the app talked: the class that
    /// started it, its way to the dock, the folder it stood in, the variable that found it, its deep links,
    /// the edit mode handshake, Identify's message and the "app missing" flyout.
    /// </summary>
    private static readonly Regex Starts = new(
        @"\bSettingsApp\.|\bKarpoProcess\b|\bDockHandOff\b|KARPO_SETTINGS\b|""Karpo\.Settings""|""--page""|""--island""|""--setup""|karpo\.editing|karpo\.identify|WM_KARPO_SETTINGS|ShowSettingsMissing|OpenDisplaySettings|\bOpenSettings\(|SettingsHandOff",
        RegexOptions.CultureInvariant);

    /// <summary>
    /// §340: after P6 nothing of Karpo's starts the settings app — no launcher, no hand-off, no deep link, no
    /// handshake — in the dock, Core or the accounts; a comment may still tell what was.
    /// </summary>
    [Fact]
    public void NothingStartsTheSettingsApp()
    {
        Assert.NotEqual("", RepoSource.Root());
        var found = new List<string>();
        foreach (var project in new[] { "Karpo.Dock", "Karpo.Core", "Karpo.Accounts" })
        {
            foreach (var path in RepoSource.CSharp(project))
            {
                var lines = File.ReadAllLines(path);
                for (var at = 0; at < lines.Length; at++)
                {
                    if (!lines[at].TrimStart().StartsWith("//", StringComparison.Ordinal) && Starts.IsMatch(lines[at]))
                    {
                        found.Add($"{Path.GetFileName(path)}:{at + 1}: {lines[at].Trim()}");
                    }
                }
            }
        }
        Assert.Empty(found);
        Assert.False(File.Exists(Path.Combine(RepoSource.Root(), "Karpo.Dock", "Dock.EditSettings.cs")));
    }

    /// <summary>
    /// §341: the settings app is gone — its project, its place in the solution, the publish step that put it
    /// in a settings folder beside the bar, and the tests' copy of its logic.
    /// </summary>
    [Fact]
    public void TheSettingsAppIsGone()
    {
        Assert.False(Directory.Exists(Path.Combine(RepoSource.Root(), "Karpo.Settings")));
        Assert.DoesNotContain("Karpo.Settings", RepoSource.Read("Karpo.slnx"), StringComparison.Ordinal);
        Assert.DoesNotContain("Karpo.Settings", RepoSource.Read("Karpo.Tests", "Karpo.Tests.csproj"), StringComparison.Ordinal);
        var publish = RepoSource.Read("scripts", "publish.ps1");
        Assert.DoesNotContain("Karpo.Settings", publish, StringComparison.Ordinal);
        Assert.DoesNotContain("'settings'", publish, StringComparison.Ordinal);
    }

    /// <summary>
    /// Where each way lands (§352): "Taskbar settings" Layout ▾ itself, its Taskbar first; the Layouts
    /// face's Display ▾ itself, the layouts its own rows (§361); "Manage accounts" §535 the Accounts page, "Get Karpo
    /// Pro" ⚙ › Karpo Pro, a second start ⚙ itself; §351 the links: "Public holidays" ⚙ › Public holidays, a
    /// divider's Layout ▾ › its Folders' separators; the bar's right
    /// click's "Display" Display ▾ (§411), the folder room's "Layout" Layout ▾ (§429); §429 "Styles" the styles' own menu.
    /// </summary>
    [Fact]
    public void EachWayInLandsOnItsMenu()
    {
        Assert.Equal(
            [
                new EditLanding(PanelPage.Taskbar),
                // §521: the Layouts face's "Layout settings" the Layouts page, §585 (§624) Layouts.
                new EditLanding(PanelPage.Layouts),
                // §535: "Manage accounts" the Accounts page itself.
                new EditLanding(PanelPage.Accounts),
                new EditLanding(PanelPage.Karpo, KarpoMenu.ProStatusRow),
                new EditLanding(PanelPage.Karpo),
                new EditLanding(PanelPage.Karpo, OptionHolidays.Calendars),
                // §485: the layouts line's "Add", §628 Add a layout's page of the presets.
                new EditLanding(PanelPage.AddLayout),
                // §517: the folder room's "Look", the Appearance page.
                new EditLanding(PanelPage.Appearance),
                // §646: an account's "Mail folders", its page under the Accounts entry.
                new EditLanding(PanelPage.MailFolders),
            ],
            Enum.GetValues<EditEntry>().Select(EditEntries.Landing));
        Assert.Equal(
            [EditEntry.ManageAccounts, EditEntry.Holidays, EditEntry.AppearanceSettings, EditEntry.AddLayout, EditEntry.MailFolders],
            Enum.GetValues<OptionLink>().Select(EditEntries.Of));
    }

    /// <summary>The items of the settings panel's <paramref name="page"/>, as the dock builds it (Dock.PanelOf), their commands into <paramref name="picks"/>.</summary>
    private static List<FlyoutItem> MenuItems(PanelPage page, Config config, KarpoServices services, List<OptionPick> picks) =>
        page == PanelPage.Karpo ? TheDock.SettingsItems(config, services, picks)
            : page == PanelPage.Accounts ? TheDock.SettingsItems(config, services, picks, page: KarpoPage.Accounts)
            : TheDock.LooksItems(config, new BarSpot(Display, "One"), dark: true, picks, ScopeOf(page), services: services);

    /// <summary>The looks a page other than Karpo shows (Dock.PanelOf's).</summary>
    private static LooksScope ScopeOf(PanelPage page) => page switch
    {
        PanelPage.Layouts => LooksScope.Layouts,
        PanelPage.AddLayout => LooksScope.AddLayout,
        PanelPage.Appearance => LooksScope.Appearance,
        _ => LooksScope.Taskbar,
    };

    /// <summary>§400: <paramref name="items"/> and the rows of every level under them, as Flyout.OpenLevel finds a row.</summary>
    private static IEnumerable<FlyoutItem> Deep(IEnumerable<FlyoutItem> items) =>
        items.SelectMany(item => (IEnumerable<FlyoutItem>)[item, .. Deep(item.Children ?? [])]);

    /// <summary>
    /// §351: each link lands where what it stands for is set: the accounts' "Add an account", the holidays'
    /// "Add a country", each kind of separators' Line, Space and None (§405).
    /// </summary>
    [Fact]
    public void EachLinkLandsWhereItsSettingIs()
    {
        var config = new Config();
        config.EditScreen(Display, "One");
        _ = BarLooks.Add(config, "Work");
        var forms = new AccountForms((form, add) => Task.FromResult(new AccountTry("imap.example.com")), work => work(), () => { });
        var services = new KarpoServices(new OptionAccounts([], _ => { }, _ => { }, _ => { }, forms), () => { });
        (OptionLink Link, string[] There)[] links =
        [
            (OptionLink.Accounts, [S.SettingsAccountsAddMail]),
            (OptionLink.Holidays, [S.CoreHolidaysAdd]),
            // §628: Add a layout's page, a tile for each preset.
            (OptionLink.AddLayout, [.. LayoutPresets.All.Select(preset => preset.Title)]),
        ];
        foreach (var (link, there) in links)
        {
            var landing = EditEntries.Landing(EditEntries.Of(link));
            List<OptionPick> picks = [];
            var items = MenuItems(landing.Page, config, services, picks);
            var shown = landing.Level is { } level ? Deep(items).Single(item => item.Id == picks.Find(each => each.Kind == OptionPickKind.None && each.Key == level)!.Id).Children! : items;
            Assert.All(there, words => Assert.Contains(shown, item => item.Text == words));
        }
    }

    /// <summary>
    /// Each level a way lands on is a row of the menu as the dock builds it whose level opens in place: the
    /// first row picked by its key (as Dock.OpenShownLevel finds it) has rows under it — ⚙'s Public holidays and Karpo
    /// Pro on its first level, Layout ▾'s two kinds of separators on its Appearance and
    /// Folders pages (§400: Flyout.OpenLevel opens those first). Layout ▾'s first row is the layout's type,
    /// where "Taskbar settings" lands; the layouts stand in Display ▾ itself, where "Layout settings" lands (§361).
    /// </summary>
    [Fact]
    public void EachLevelIsARowOfItsMenuThatOpens()
    {
        var config = new Config();
        config.EditScreen(Display, "One");
        var forms = new AccountForms((form, add) => Task.FromResult(new AccountTry("imap.example.com")), work => work(), () => { });
        var services = new KarpoServices(new OptionAccounts([], _ => { }, _ => { }, _ => { }, forms), () => { })
        {
            Licence = new KarpoLicence(new LicenseStatus(LicenseTier.Free, LicenseBasis.StoreFree), HasStore: true, CanPurchase: true, () => { }, () => { }),
        };
        foreach (var entry in Enum.GetValues<EditEntry>())
        {
            if (EditEntries.Landing(entry) is not { Level: { } level } landing)
            {
                continue;
            }
            List<OptionPick> picks = [];
            var items = MenuItems(landing.Page, config, services, picks);
            var pick = picks.Find(each => each.Kind == OptionPickKind.None && each.Key == level);
            Assert.True(pick is not null, $"{entry}: no row keyed {level}");
            var row = Deep(items).SingleOrDefault(item => item.Id == pick.Id);
            Assert.True(row is { Children.Count: > 0 }, $"{entry}: {level} is no row of the menu with a level of its own");
        }
        var first = TheDock.LooksItems(config, new BarSpot(Display, "One"), dark: true, [], ScopeOf(EditEntries.Landing(EditEntry.TaskbarSettings).Page))[0];
        Assert.Equal((true, S.SettingsSectionsBar), (first.Header, first.Text));
        var display = TheDock.LooksItems(config, new BarSpot(Display, "One"), dark: true, [], ScopeOf(EditEntries.Landing(EditEntry.LayoutSettings).Page));
        Assert.Contains(display, item => item.Text == config.Layouts[0].Name && item.Children is { Count: > 0 });
        Assert.Contains(display, item => item.Text == S.SettingsLayoutsAdd);
    }

    /// <summary>
    /// The ways in, as the dock makes them: Win+Alt+Enter on a bar's button, and §581 the bar's menu's "Taskbar settings"
    /// again (gone from §366 to §580), each edit mode on that bar with its Taskbar page open; the faces' "Manage
    /// accounts" and "Layout settings", Get Karpo Pro (once its menu has gone), a second start; and the way
    /// out of a second start, Karpo.exe asking the dock that runs.
    /// </summary>
    [Fact]
    public void TheDockGoesEachWay()
    {
        var dock = RepoSource.Read("Karpo.Dock", "Dock.cs");
        Assert.Contains("OpenEditMenuLater(window, EditEntry.TaskbarSettings);", RepoSource.Read("Karpo.Dock", "Dock.Keys.cs"));
        // §581: the bar's menu's row (Dock.BarRows: BarMenuAction.TaskbarSettings, CMD_TASKBAR_SETTINGS), on the bar it was opened on.
        var chosen = dock.IndexOf("if (id == CMD_TASKBAR_SETTINGS && what.Owner is { } edited)", StringComparison.Ordinal);
        Assert.True(chosen >= 0, "no case for the bar menu's Taskbar settings");
        Assert.StartsWith("if (id == CMD_TASKBAR_SETTINGS && what.Owner is { } edited)\n        {\n            OpenEditMenuLater(edited, EditEntry.TaskbarSettings);",
            dock[chosen..].ReplaceLineEndings("\n"), StringComparison.Ordinal);
        Assert.Contains("new FlyoutItem(CMD_TASKBAR_SETTINGS, line.Text", RepoSource.Read("Karpo.Dock", "Dock.BarMenu.cs"));
        Assert.Contains("Report(() => OpenEditMenu(null, EditEntry.SecondStart));", dock);
        Assert.Contains("if (LicenseChosen(id, what.Owner))", dock);
        var faces = RepoSource.Read("Karpo.Dock", "Dock.FaceMenus.cs");
        Assert.Contains("OpenEditMenuLater(window, EditEntry.ManageAccounts);", faces);
        Assert.Contains("OpenEditMenuLater(window, EditEntry.LayoutSettings);", faces);
        Assert.Contains("OpenEditMenuLater(from, EditEntry.GetPro);", RepoSource.Read("Karpo.Dock", "Dock.License.cs"));
        // §485: the layouts line's "Add", once its folder has gone.
        Assert.Contains("OpenEditMenuLater(window, EditEntry.AddLayout);", RepoSource.Read("Karpo.Dock", "Dock.Folders.cs"));
        var program = RepoSource.Read("Karpo.Dock", "Program.cs");
        Assert.Contains("if (SecondStart.Asks(args, Config.IsIsolated))", program);
        Assert.Contains("SecondStart.Ask();", program);
        // §520: the settings panel opens on its page, at its row.
        Assert.Contains("OpenPanel(display, landing.Page, landing.Level);", RepoSource.Read("Karpo.Dock", "Dock.EditEntries.cs"));
    }

    /// <summary>
    /// A second start asks for ⚙ where a person started it (the Start menu, Explorer, a pin, with or without
    /// the drawing's switches); never where Windows or Karpo did — the Run entry, the logon task, the guard's
    /// restart —, nor on a config of its own, which the running dock is not.
    /// </summary>
    [Theory]
    [InlineData(new string[0], false, true)]
    [InlineData(new[] { "--gpu" }, false, true)]
    [InlineData(new string[0], true, false)]
    [InlineData(new[] { "--startup" }, false, false)]
    [InlineData(new[] { "--startup", "--logon-task" }, false, false)]
    [InlineData(new[] { "--restarts", "1@1700000000" }, false, false)]
    [InlineData(new[] { "--GUARD", "12" }, false, false)]
    public void ASecondStartAsksWhereAPersonStartedIt(string[] args, bool isolated, bool asks) =>
        Assert.Equal(asks, SecondStart.Asks(args, isolated));
}
