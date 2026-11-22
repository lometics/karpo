// menus: every setting at one level, and a link where a lower level needs it (docs\widgets.md §351; the
// person, 2026-10-25: "some settings appear multiple times like account, then i can also change account
// settings in a widget for example. i would rather not have that. i think in that case it would be better
// to link into the account settings instead of duplicating them, just so that it is easier for the person
// to understand what level of settings the person is editing").
//
// A setting belongs to one of a widget, a folder, a layout, a display or Karpo as a whole, and only that
// level's menu sets it. Where a lower level's menu needs what a higher one owns, it has a link row
// (OptionRowKind.Link): named for what it leads to, the arrow out at its end; picked, the menu closes and the
// owner's menu opens at its level (the dock's EditEntries, edit mode first where the bar was at rest):
//
//   a widget or face showing the accounts' data   "Manage accounts"        §535 the Accounts page (§529: a line of what it
//                                                                          draws on — its accounts, a calendar's public
//                                                                          holidays —, its link at its end, under Show from;
//                                                                          §351 to §528 a calendar had "Public holidays" a
//                                                                          link of its own, to ⚙ › Public holidays)
//   an account's rows on the Accounts page        "Mail folders"           §646 a page under the Accounts entry: its inbox and every
//                                                                          folder a switch (the link's own level), the way back at its
//                                                                          top (§639 to §645 the switches in the account's expander)
//   an arranged folder room's right click         "Appearance"             §567 the Appearance page (§517 to §566 "Look";
//                                                                          §423 to §513 "Layout", the menu's foot); §505: a
//                                                                          divider has no link
//
// §507: no styles, so no link to them (§485 to §506 "Styles" under every choice of a style, to ⚙ › Styles). §567: one look,
// Karpo's Appearance; no widget's, folder's or layout's own.
//
// (§517: the edit bar's Display ▾, Taskbar ▾ and Look ▾ side by side, §352's Layout ▾ split. §361: a layout's "Show on this
// display" is no link since the layouts are managed in Display ▾, which chooses it.)
//
// §585: no "Display" link (§411's from the bar's right click, which opens the Taskbar page since §520; the displays are the
// Layouts page's).
//
// What is the widget's own stays the widget's: which list a to-do line shows, its look (§507), where each tray icon shows.

namespace Karpo.Core;

/// <summary>§351: the link rows; see the top of the file.</summary>
public static class OptionLinks
{
    /// <summary>A link's key: this, then its <see cref="OptionLink"/> ("Link.Accounts").</summary>
    public const string Key = "Link.";

    /// <summary>The link to <paramref name="to"/>, in its menu's part <paramref name="slot"/>.</summary>
    public static OptionRow Row(OptionLink to, OptionSlot slot) => new(Key + to, OptionRowKind.Link, Title(to))
    {
        Glyph = Glyph(to),
        Slot = slot,
        Link = to,
    };

    /// <summary>What a link to <paramref name="to"/> is called: what the person goes there for.</summary>
    public static string Title(OptionLink to) => to switch
    {
        // §411 (R15): named for its home, as the home names it.
        OptionLink.Accounts => S.SettingsNavAccounts,
        OptionLink.Holidays => S.CoreHolidaysName,
        OptionLink.AddLayout => S.SettingsLayoutsAdd,
        OptionLink.MailFolders => S.SettingsAccountsMailFolders,
        _ => S.SettingsAppearanceHeader,
    };

    /// <summary>A link's icon: its owner's row's.</summary>
    public static string Glyph(OptionLink to) => to switch
    {
        OptionLink.Accounts => OptionGlyphs.Accounts,
        OptionLink.Holidays => OptionGlyphs.Flag,
        OptionLink.AddLayout => OptionGlyphs.New,
        OptionLink.MailFolders => OptionGlyphs.Folder,
        _ => OptionGlyphs.Palette,
    };

    /// <summary>What a screen reader says a link does: "Opens Accounts in Accounts", "Opens Appearance in Appearance".</summary>
    public static string Opens(OptionLink to) => to switch
    {
        // §535: the accounts' own page.
        OptionLink.Accounts => S.DockMenuLinkOpens(S.SettingsNavAccounts, S.SettingsNavAccounts),
        // §614: the General page's (was "Karpo").
        OptionLink.Holidays => S.DockMenuLinkOpens(S.CoreHolidaysName, S.SettingsGeneralHeader),
        // §628: its own page, under the Layouts page's entry.
        OptionLink.AddLayout => S.DockMenuLinkOpens(S.SettingsLayoutsAdd, S.CoreBarMenuLayouts),
        // §646: its own page, under the Accounts page's entry.
        OptionLink.MailFolders => S.DockMenuLinkOpens(S.SettingsAccountsMailFolders, S.SettingsNavAccounts),
        _ => S.DockMenuLinkOpens(S.SettingsAppearanceHeader, S.SettingsAppearanceHeader),
    };

    /// <summary>
    /// §646: whether a link to <paramref name="to"/> leads to a page under the page it stands on (§628 Add a layout's, §646 an
    /// account's Mail folders), one level down in the same flyout: it says what it holds at its end, as an expander says it
    /// (its <see cref="OptionRow.Summary"/>), where a link elsewhere says what it draws on as a line of words (§529).
    /// </summary>
    public static bool Under(OptionLink to) => to is OptionLink.AddLayout or OptionLink.MailFolders;
}
