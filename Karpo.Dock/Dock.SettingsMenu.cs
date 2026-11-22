// edit: the settings panel's Karpo page (§520; ⚙ Karpo ▾, the edit bar's gear's menu, until then): what belongs to Karpo
// as a whole rather than to a bar or a folder (docs\widgets.md §335; the settings-in-the-dock plan's P2;
// Karpo.Core\KarpoMenu.cs, one builder per section). Its sections under their headings, its levels inline expanders, a
// pick set and saved at once (Apply) and the page shown again as the config is now.
// Every pick finds its row again in KarpoMenu's rows read from a copy of the config, as a widget's does
// (OptionRows.Find): the builders are the one place a row is made and written, so a row a later phase
// adds to a section is on the page with nothing to wire here.
//
// It is on a display that shows None too, owned by the bar holding edit mode: it is no bar's. A section with no rows is
// not in it (KarpoMenu.Rows). §703 Quit Karpo is not on it (the taskbar menu's, §532, where somebody adds it).
//
// §337, §597: a site's choice wears its logo (MenuPictures). §356: another language picked reads the page's
// rows again in the new words where it stands.

using Karpo.Core;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

/// <summary>edit: the settings panel's Karpo page (§335, §520) or §535 its Accounts page, and the display whose panel it is on.</summary>
internal sealed record SettingsMenu(string Display, KarpoPage Page);

public sealed unsafe partial class Dock
{
    /// <summary>
    /// edit: ⚙ Karpo's items as <paramref name="of"/> has them (KarpoMenu.Rows, §517: its sections under their headings,
    /// §703 no Quit Karpo), or §535 the Accounts page's (KarpoMenu.Accounts) for <paramref name="page"/>, their commands
    /// into <paramref name="picks"/>. A row's or a choice's own picture by what it names (<see cref="MenuPictures"/>):
    /// Karpo's logo and Lometics' mark (§339), a site's logo (§337, §597).
    /// </summary>
    internal static List<FlyoutItem> SettingsItems(Config of, KarpoServices services, List<OptionPick> picks, KarpoPage page = KarpoPage.Karpo) =>
        OptionMenu.Rows(KarpoMenu.Of(page, of, services), picks, pictures: MenuPictures.Of);

    /// <summary>edit: the row of page <paramref name="page"/> <paramref name="pick"/> was shown for, read from <paramref name="of"/>; null for none.</summary>
    private OptionRow? SettingsRow(Config of, KarpoPage page, OptionPick pick) => OptionRows.Find(KarpoMenu.Of(page, of, SettingsServices(page)), pick.Key);

    /// <summary>
    /// edit: what ⚙ Settings' sections are given (§335): the accounts as accounts.json has them now and
    /// what a menu does to them (MenuAccounts, §324), and the way out (<see cref="QuitFromMenu"/>); §337:
    /// an address of Windows' opened as a click on the bar opens what it launches (Windows' region settings,
    /// About Karpo's sites). Windows' side of "Start with Windows" and its languages are the real ones
    /// (KarpoServices' own). §339: the licence (MenuLicence), Back up and reset (MenuBackup) and Karpo
    /// itself (MenuAbout). §535: the accounts only for the Accounts page, the one that lists them (<paramref name="page"/>).
    /// </summary>
    private KarpoServices SettingsServices(KarpoPage page) => new(page == KarpoPage.Accounts ? MenuAccounts() : null, QuitFromMenu)
    {
        Open = uri => LaunchLater(uri),
        // §710: the package's startup task for the Store's copy, the Run entry for any other.
        Startup = PackagedStartup.ForThisProcess,
        Licence = MenuLicence(),
        Backup = MenuBackup(),
        About = MenuAbout(),
    };

    /// <summary>
    /// edit: ⚙ Settings' Quit Karpo, asked already: the host's marked close, posted, so the dock quits by
    /// its one way out once the menu has gone (Closing.Answer, §283: the line in karpo.log, the exit code).
    /// </summary>
    private void QuitFromMenu() => _ = PostMessageW(host, WM.WM_CLOSE, (uint)DockHost.QuitBy.Menu, 0);

    /// <summary>
    /// edit: a row of ⚙ Settings acted: set as its section writes it and saved as every change of the
    /// dock's is (§523: a colour of one's own mixed in its picker, MenuMixed); an action that asks — an
    /// account or a holiday calendar removed, Quit Karpo — once Windows' own dialog asked. Where the menu
    /// stays open (<paramref name="list"/>) it shows the rows as they are now.
    /// </summary>
    private void SettingsPicked(Pending what, OptionPick pick, Flyout? list)
    {
        var next = config.Clone();
        var row = SettingsRow(next, what.SettingsMenu!.Page, pick);
        if (!GoesAhead(what, row, pick))
        {
            return;
        }
        if (row is not { Enabled: false } && OptionMenu.ApplyRow(row, pick))
        {
            Apply(next);
        }
        // §337, §356: another language: ⚙ stays where it stands, its rows read again in the new words
        // (Dock.Apply), the edit bar's words new too.
        if (list is { IsOpen: true })
        {
            MenuAgain(what);
        }
    }

    /// <summary>edit: a field of ⚙ Settings kept <paramref name="kept"/>: set through its row, saved where anything changed.</summary>
    private void SettingsKept(KarpoPage page, OptionPick pick, FieldCommit kept)
    {
        var next = config.Clone();
        if (FieldPicks.KeepRow(SettingsRow(next, page, pick), kept))
        {
            Apply(next);
        }
    }
}
