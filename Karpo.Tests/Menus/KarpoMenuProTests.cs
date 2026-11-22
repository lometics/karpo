// edit: ⚙ Settings' Karpo Pro, Back up and reset and About Karpo (docs\widgets.md §339; Karpo.Core\
// KarpoMenu.Pro.cs, .Backup.cs, .About.cs). Held here, through the sections' services, never the Store or
// a dialog:
//
//  - **Karpo Pro** is the License page: what this copy is and why, each layout's widgets and the layouts
//    of the free version's where its limits apply, what counts, Get Karpo Pro (with the price, greyed with
//    why where it can't be bought, how the purchase ended under it) and Restore a purchase; a developer
//    build its state alone, Pro no Get Karpo Pro.
//  - **Back up and reset** is the About page's file and Reset: export, import, the file's place, the two
//    resets asked first with the page's words; how each ended under its row and at the level's end.
//  - **About Karpo** is the page's name card and credits: Karpo's logo, the version and the runtime, a
//    note; Lometics and the data's sources opening their sites.
//  - **In the dock**: notes are words, never greyed, nothing to pick; the logo a picture; an act that asks
//    closes the menu, the others leave it standing.

using Karpo.Dock;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Menus;

public sealed class KarpoMenuProTests
{
    private static readonly LicenseStatus Free = new(LicenseTier.Free, LicenseBasis.StoreFree);
    private static readonly LicenseStatus Pro = new(LicenseTier.Pro, LicenseBasis.StorePro);

    /// <summary>Two layouts, the first with two volume widgets (two that count) and a pin (one that doesn't).</summary>
    private static Config TwoLayouts() => new()
    {
        Layouts =
        [
            new TaskbarLayout { Id = "home", Name = "Home", Islands = [new BarItem { Id = "a", Kind = BarKind.Volume }, new BarItem { Id = "b", Kind = BarKind.Volume }, new BarItem { Id = "s", Kind = BarKind.Pin }] },
            new TaskbarLayout { Id = "work", Name = "Work", Islands = [] },
        ],
    };

    private static KarpoServices With(KarpoLicence? licence = null, KarpoBackup? backup = null, KarpoAbout? about = null, Action<string>? open = null) =>
        new(null, () => { }) { Licence = licence, Backup = backup, About = about, Open = open ?? (_ => { }) };

    private static bool Act(OptionRow row) => OptionMenu.ApplyRow(row, new OptionPick(OptionMenu.FirstId, OptionPickKind.Act, Key: row.Key));

    /// <summary>
    /// The free version: its state and why, each layout's widgets "n of 20" as the licence counts them, the
    /// layouts "2 of 2", what counts, Get Karpo Pro with the Store's price, Restore a purchase — one level
    /// under "Karpo Pro", which says "Free version" at its end. Get Karpo Pro buys and Restore asks the Store
    /// again through the licence given, and neither changes the config.
    /// </summary>
    [Fact]
    public void TheFreeVersionSaysWhatItIsWhatIsUsedAndSellsPro()
    {
        var (bought, asked) = (0, 0);
        var config = TwoLayouts();
        var licence = new KarpoLicence(Free, HasStore: true, CanPurchase: true, () => bought++, () => asked++) { Price = "CHF 9.00" };
        var rows = KarpoMenu.Pro(config, With(licence));

        Assert.Equal(
            [KarpoMenu.ProStatusRow, KarpoMenu.ProLayoutRow + "home", KarpoMenu.ProLayoutRow + "work", KarpoMenu.ProLayoutsRow, KarpoMenu.ProCountsRow, KarpoMenu.GetProRow, KarpoMenu.RestoreRow],
            rows.Select(row => row.Key));
        // §517 (R4): the licence at its row's end.
        Assert.All(rows, row => Assert.Equal((S.SettingsLicenseProTitle, (string?)S.SettingsLicenseFreeTitle), (row.Group, row.GroupSummary)));
        Assert.Equal((OptionRowKind.Note, S.SettingsLicenseFreeTitle), (rows[0].Kind, rows[0].Title));
        Assert.Equal(LicenseView.Detail(Free, _ => ""), rows[0].Why);
        Assert.Equal(["Home", "Work"], rows.Skip(1).Take(2).Select(row => row.Title));
        Assert.Equal(
            [LicenseView.Of(2, LicenseRules.FreeWidgets), LicenseView.Of(0, LicenseRules.FreeWidgets), LicenseView.Of(2, LicenseRules.FreeLayouts)],
            rows.Skip(1).Take(3).Select(row => row.Summary));
        Assert.Contains(S.SettingsLicenseCountsFree, rows[4].Why, StringComparison.Ordinal);
        Assert.All(rows.Take(5), row => Assert.Equal(OptionRowKind.Note, row.Kind));

        var (getPro, restore) = (rows[5], rows[6]);
        Assert.Equal((S.SettingsLicenseBuyPrice("CHF 9.00"), true, S.SettingsLicensePitch), (getPro.Title, getPro.Enabled, getPro.Why));
        Assert.Equal((S.SettingsLicenseRestoreHeader, true, S.SettingsLicenseRestoreDescription), (restore.Title, restore.Enabled, restore.Why));
        Assert.Null(getPro.Ask);
        Assert.False(Act(getPro));
        Assert.False(Act(restore));
        Assert.Equal((1, 1), (bought, asked));
    }

    /// <summary>
    /// Where Pro can't be bought here Get Karpo Pro stands greyed with why (not on sale yet, no Store); how
    /// a purchase and an ask ended stand under their rows; Pro has no Get Karpo Pro and no usage; a developer
    /// build its state alone; no licence given, no Karpo Pro at all.
    /// </summary>
    [Fact]
    public void ProTheStoreAndADeveloperBuildEachShowTheirOwn()
    {
        var config = TwoLayouts();
        KarpoLicence Licence(LicenseStatus status, bool store = true, bool sale = true) => new(status, store, sale, () => { }, () => { });

        var notOnSale = KarpoMenu.Pro(config, With(Licence(Free, sale: false))).Single(row => row.Key == KarpoMenu.GetProRow);
        Assert.Equal((S.CoreLicenseGetPro, false, S.SettingsLicenseNotOnSale), (notOnSale.Title, notOnSale.Enabled, notOnSale.Why));
        var told = KarpoMenu.Pro(config, With(Licence(Free) with { Purchased = S.SettingsLicenseCancelled, Asked = S.SettingsLicenseFreeTitle }));
        Assert.Equal(S.SettingsLicenseCancelled, told.Single(row => row.Key == KarpoMenu.GetProRow).Why);
        Assert.Equal(S.SettingsLicenseFreeTitle, told.Single(row => row.Key == KarpoMenu.RestoreRow).Why);

        Assert.Equal([KarpoMenu.ProStatusRow, KarpoMenu.RestoreRow], KarpoMenu.Pro(config, With(Licence(Pro))).Select(row => row.Key));
        Assert.Equal(LicenseView.Title(Pro), KarpoMenu.Pro(config, With(Licence(Pro)))[0].GroupSummary);
        var developer = Assert.Single(KarpoMenu.Pro(config, With(Licence(LicenseStatus.Developer, store: false, sale: false))));
        Assert.Equal((S.SettingsLicenseDeveloperTitle, S.SettingsLicenseDeveloperDetail), (developer.Title, developer.Why));
        Assert.Empty(KarpoMenu.Pro(config, With()));
    }

    /// <summary>
    /// Back up and reset: export, import, the file's place in File Explorer (its path under it), the two resets — each asked
    /// first in Windows' dialog with the About page's title, what goes and what stays, and its button; the
    /// rest ask nothing. Each acts through the backup given, a reset with its scope, and changes no config;
    /// how each ended stands under it.
    /// </summary>
    [Fact]
    public void BackUpAndResetAreTheAboutPagesFileAndReset()
    {
        List<string> done = [];
        var backup = new KarpoBackup(() => done.Add("export"), () => done.Add("import"), () => done.Add("file"), scope => done.Add($"reset {scope}"))
        {
            FilePath = @"C:\Users\me\AppData\Roaming\Karpo\config.json",
            Said = new Dictionary<string, string> { [KarpoMenu.ExportRow] = S.SettingsAboutExportSavedAs(@"D:\karpo.json") },
        };
        var rows = KarpoMenu.Backup(new Config(), With(backup: backup));

        Assert.Equal([KarpoMenu.ExportRow, KarpoMenu.ImportRow, KarpoMenu.ConfigFileRow, KarpoMenu.ResetLayoutsRow, KarpoMenu.ResetAllRow], rows.Select(row => row.Key));
        Assert.All(rows, row => Assert.Equal((OptionRowKind.Action, S.DockSettingsBackup), (row.Kind, row.Group)));
        Assert.Equal(
            [S.SettingsAboutExportHeader, S.SettingsAboutImportHeader, S.DockWidgetMenuOpenFileLocation, S.SettingsAboutResetLayoutsHeader, S.SettingsAboutResetAllHeader],
            rows.Select(row => row.Title));
        Assert.Equal(
            [S.SettingsAboutExportSavedAs(@"D:\karpo.json"), S.SettingsAboutImportDescription, backup.FilePath, S.SettingsAboutResetLayoutsDescription, S.SettingsAboutResetAllDescription],
            rows.Select(row => row.Why));
        Assert.Equal([null, null, null], rows.Take(3).Select(row => row.Ask));
        Assert.Equal(
            new DeleteQuestion(S.SettingsAboutResetLayoutsTitle, S.SettingsAboutResetLayoutsMessage + "\n\n" + S.SettingsAboutResetKept, S.SettingsAboutResetLayoutsConfirm, S.SettingsCommonCancel, 0),
            rows[3].Ask);
        Assert.Equal((S.SettingsAboutResetAllTitle, S.SettingsAboutResetAllConfirm), (rows[4].Ask!.Title, rows[4].Ask!.Delete));
        Assert.Contains(S.SettingsAboutImportStartupStays(S.SettingsGeneralStartupHeader), rows[4].Ask!.Content, StringComparison.Ordinal);

        Assert.All(rows, row => Assert.False(Act(row)));
        Assert.Equal(["export", "import", "file", "reset Layouts", "reset All"], done);
        Assert.Empty(KarpoMenu.Backup(new Config(), With()));
    }

    /// <summary>
    /// About Karpo: Karpo, its logo and the version and runtime under it, a note; Lometics GmbH with its mark
    /// and its one line, opening its site; §630 nothing else — no data source (Open-Meteo's credit is under a weather
    /// widget's Place). Under "About Karpo".
    /// </summary>
    [Fact]
    public void AboutKarpoShowsTheVersionAndOpensTheCredits()
    {
        List<string> opened = [];
        var rows = KarpoMenu.About(new Config(), With(about: new KarpoAbout("1.4.2", ".NET 10.0.1"), open: opened.Add));

        Assert.Equal([KarpoMenu.AboutRow, KarpoMenu.MakerRow, KarpoMenu.MakerSiteRow], rows.Select(row => row.Key));
        // §579: a section's rows, no level.
        Assert.All(rows, row => Assert.Null(row.Group));
        var karpo = rows[0];
        Assert.Equal((OptionRowKind.Note, "Karpo", KarpoMenu.LogoPicture), (karpo.Kind, karpo.Title, karpo.Picture));
        // §579: one line under its name, as a card's second line is.
        Assert.Equal(S.SettingsAboutVersion("1.4.2") + " · .NET 10.0.1", karpo.Why);
        // §638: Lometics a note as Karpo is, its name over its line — no town ("based in Switzerland") —, its site a button
        // beside it.
        Assert.Equal((OptionRowKind.Note, "Lometics GmbH", KarpoMenu.PublisherPicture, S.SettingsAboutMakerDescription), (rows[1].Kind, rows[1].Title, rows[1].Picture, rows[1].Why));
        Assert.Equal("The company behind Karpo, based in Switzerland", Strings.EnglishOf(S.SettingsAboutMakerDescription) ?? S.SettingsAboutMakerDescription);
        Assert.Equal((OptionRowKind.Action, KarpoMenu.MakerSiteName, true), (rows[2].Kind, rows[2].Title, rows[2].Beside));

        Assert.All(rows.Skip(2), row => Assert.False(Act(row)));
        Assert.Equal([KarpoMenu.MakerSite], opened);
        Assert.Empty(KarpoMenu.About(new Config(), With()));
    }

    /// <summary>
    /// The three in ⚙'s third band, in their order, each a level; in the dock their notes are words (never
    /// greyed, no command of their own to pick), Karpo's logo the dock's picture of it, an act that asks (a
    /// reset) closes the menu and the others leave it standing (Stays).
    /// </summary>
    [Fact]
    public void InTheDockTheyAreLevelsOfNotesPicturesAndActs()
    {
        var services = With(
            new KarpoLicence(Free, true, true, () => { }, () => { }),
            new KarpoBackup(() => { }, () => { }, () => { }, _ => { }),
            new KarpoAbout("1.4.2", ".NET 10.0.1"));
        Assert.Equal(
            [S.SettingsLicenseProTitle, S.DockSettingsBackup],
            KarpoMenu.Rows(TwoLayouts(), services).SkipWhile(row => row.Key != KarpoMenu.KarpoHeading).Skip(1).TakeWhile(row => row.Kind != OptionRowKind.Heading).Select(row => row.Group).Distinct());
        // §579: About Karpo a section of its own after it, its rows cards.
        Assert.Equal(
            [KarpoMenu.AboutRow, KarpoMenu.MakerRow, KarpoMenu.MakerSiteRow],
            KarpoMenu.Rows(TwoLayouts(), services).SkipWhile(row => row.Key != KarpoMenu.AboutHeading).Skip(1).Select(row => row.Key));

        List<OptionPick> picks = [];
        var items = TheDock.SettingsItems(TwoLayouts(), services, picks);
        FlyoutItem Level(string name) => items.Single(item => item.Text == name);
        var pro = Level(S.SettingsLicenseProTitle);
        Assert.Equal(S.SettingsLicenseFreeTitle, pro.Value?.Text);
        var status = pro.Children![0];
        Assert.True(status is { Note: true, Enabled: true });
        Assert.Equal(LicenseView.Detail(Free, _ => ""), status.Detail);
        Assert.Equal(LicenseView.Of(2, LicenseRules.FreeWidgets), pro.Children[1].Value?.Text);
        Assert.Equal(OptionPickKind.None, OptionMenu.PickOf(picks, status.Id)?.Kind);
        Assert.True(pro.Children.Single(item => item.Text == S.CoreLicenseGetPro).Stays);

        var backup = Level(S.DockSettingsBackup).Children!;
        Assert.Equal([true, true, true, false, false], backup.Select(item => item.Stays));
        Assert.All(backup, item => Assert.Equal(OptionPickKind.Act, OptionMenu.PickOf(picks, item.Id)?.Kind));

        var about = items.SkipWhile(item => !(item.Header && item.Text == S.DockSettingsAbout)).Skip(1).ToList();
        Assert.Equal(["Karpo", "Lometics GmbH", KarpoMenu.MakerSiteName], about.Select(item => item.Text));
        Assert.True(about[1].Note);
        Assert.True(about[2].Beside);
        Assert.IsType<FlyoutIcon.Picture>(about[0].Icon);
        Assert.IsType<FlyoutIcon.Picture>(about[1].Icon);
        Assert.True(about[0].Note);
        Assert.Contains("1.4.2", about[0].Detail, StringComparison.Ordinal);
    }

    /// <summary>
    /// The import's task dialog: what changes as its content, a line each, "and N more" past twelve; what
    /// this PC lacks, the displays not here and what is good to know behind "See details", each under its
    /// heading; that nothing changes where nothing does.
    /// </summary>
    [Fact]
    public void TheImportDialogSaysWhatChangesAndTheRestInItsDetails()
    {
        var changes = Enumerable.Range(1, 14).Select(at => $"Setting {at}").ToList();
        var (content, details) = TransferSummary.Describe(new ImportPreview(changes, [new MissingTarget(@"Q:\game.exe", "Home", "drive")], ["Office monitor"], ["Exported from DESKTOP-A."]));

        Assert.Equal([.. changes.Take(12).Select(change => "• " + change), S.SettingsAboutImportMore(2)], content.Split('\n'));
        var sections = details.Split("\n\n");
        Assert.Equal([S.SettingsAboutImportMissing, S.SettingsAboutImportAbsent, S.SettingsAboutImportNotes], sections.Select(section => section.Split('\n')[0]));
        Assert.Contains(S.SettingsAboutImportMissingKept, sections[0], StringComparison.Ordinal);
        Assert.Contains("• Office monitor", sections[1], StringComparison.Ordinal);
        Assert.EndsWith("• " + S.SettingsAboutImportBackup, sections[2], StringComparison.Ordinal);

        var (none, _) = TransferSummary.Describe(new ImportPreview([], [], [], []));
        Assert.Equal("• " + S.SettingsAboutImportNoChange, none);
    }
}
