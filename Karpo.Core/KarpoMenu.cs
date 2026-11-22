// The edit bar's ⚙ Settings menu: everything that belongs to Karpo as a whole rather than to a bar or a
// folder (docs\widgets.md §335; the person, 2026-10-23: "put the remaining settings pages, licence and
// about on the edit bar"; the settings-in-the-dock plan's P2). The gear opens it, in bar mode and with
// the folder's buttons alike.
//
// One builder per section, each in a file of its own (KarpoMenu.<Section>.cs), so each later phase
// fills its own file and no other. §517 (the person, 2026-11-05: "1. sections"): one page under three headings in a fixed
// order, every row icon · name · what it is set to · chevron (a level says what it holds at its end where it holds
// anything set):
//
//   General    Start with Windows        KarpoMenu.Startup.cs    a switch, Windows' side its Why (§337)
//              Animations, speed         KarpoMenu.General.cs    §489: a switch, a choice of three
//              Language                  KarpoMenu.Language.cs   every language, Windows default first (§337)
//              Formats ›                 KarpoMenu.Formats.cs    the Localization page's formats (§337); "Follow Windows" or "Custom"
//   Services   Public holidays ›         KarpoMenu.Holidays.cs   §324's level; its first calendar at its end
//              Online ›                  KarpoMenu.Online.cs     the update interval (at its end), the search engine and
//                                                                its address (§337), the links' sites (§322)
//   Karpo      Karpo Pro ›               KarpoMenu.Pro.cs        the licence (at its end), its use, Get Pro, ask again (§339)
//              Back up and reset ›       KarpoMenu.Backup.cs     export, import, the file, the resets (§339)
//   About      Karpo, Lometics GmbH      KarpoMenu.About.cs      §579 a section of its own: the logo, version, maker
//   Karpo                                                        (§339 to §578 a level of the Karpo section, "About Karpo ›")
//
// §535 (the person, 2026-11-07: "accounts should get their complete own section."): the accounts are not on this page; they
// are a page of their own, KarpoPage.Accounts (KarpoMenu.Accounts.cs), the settings panel's Accounts. Neither is §532's
// Taskbar menu: §553 a section of the settings panel's Taskbar page (KarpoMenu.BarMenu.cs).
//
// §545 (the person, 2026-11-07: "quit karpo should go into the general settings") put Quit Karpo as General's last row (§409
// to §544 it stood alone at the foot, the settings panel's side-list footer); §703 (the owner, 2026-11-15: "dont show quit
// karpo") took it off the page. Karpo quits from the taskbar's own menu where somebody adds it there (§532, KarpoMenu.Quit),
// and Karpo.exe --quit. §485's General › page opened onto the page (§517).
//
// Each section is rows (OptionRow), read from and written to a config as a widget's are: a row of its
// own (a toggle, an action), a level of its own (OptionRow.Group, or a row with a level such as the
// accounts'), nothing at all. A section with no rows is not in the menu, and a heading with none neither:
// nothing stands empty or greyed for what is not there yet.
//
// What a section needs that Core does not have — the accounts (Karpo.Accounts', the dock lists them),
// the way out (the dock's host) — comes in KarpoServices; a phase that needs more adds it there, in its
// section's file (KarpoServices is partial: Windows' side, the languages and Open are §337's; the licence, the
// backup and Karpo's own §339's).

namespace Karpo.Core;

/// <summary>
/// §335: what the ⚙ menu's sections need that Core does not have: the accounts and what adds, signs in to
/// and removes one (<paramref name="Accounts"/>, none where they are not listed), and Karpo's one way out
/// (<paramref name="Quit"/>: the dock posts its host's marked close, DockHost.QuitBy.Menu; §703 the taskbar menu's Quit Karpo's).
/// </summary>
public sealed partial record KarpoServices(OptionAccounts? Accounts, Action Quit);

/// <summary>§535: a page of Karpo's own, app-wide settings: Karpo's (General, Services, Karpo), the accounts'.</summary>
public enum KarpoPage
{
    Karpo,
    Accounts,
}

/// <summary>§335: the edit bar's ⚙ Settings menu, one builder per section; see the top of the file.</summary>
public static partial class KarpoMenu
{
    /// <summary>§535: the rows of <paramref name="page"/>: Karpo's (<see cref="Rows"/>), the accounts' (<see cref="Accounts"/>).</summary>
    public static IReadOnlyList<OptionRow> Of(KarpoPage page, Config config, KarpoServices services) =>
        page == KarpoPage.Accounts ? Accounts(config, services) : Rows(config, services);

    // §517: the headings' keys.
    public const string GeneralHeading = "Heading.General";
    public const string ServicesHeading = "Heading.Services";
    public const string KarpoHeading = "Heading.Karpo";

    /// <summary>
    /// §517: the menu's rows, every section's in the menu's order under its heading (a heading with no rows under it left
    /// out); §703 no Quit Karpo. A pick finds its row here again (OptionRows.Find).
    /// </summary>
    public static IReadOnlyList<OptionRow> Rows(Config config, KarpoServices services)
    {
        IEnumerable<OptionRow> Section(string key, string title, params IReadOnlyList<OptionRow>[] sections) =>
            sections.SelectMany(rows => rows).ToList() is { Count: > 0 } rows ? [new OptionRow(key, OptionRowKind.Heading, title), .. rows] : [];
        return
        [
            .. Section(GeneralHeading, S.SettingsGeneralHeader, General(config, services), Language(config, services), Formats(config, services)),
            .. Section(ServicesHeading, S.SettingsSectionsServices, Holidays(config, services), Online(config, services)),
            .. Section(KarpoHeading, S.SettingsSectionsKarpo, Pro(config, services), Backup(config, services)),
            // §579: About Karpo a section of its own, its rows cards wearing their logos.
            .. Section(AboutHeading, S.DockSettingsAbout, About(config, services)),
        ];
    }
}
