// menus: what breaks a menu rule and is still to be mended (MenuRulesTests), one line each: the rule, then the rows'
// titles from the menu's first level; a line that is kept now fails the test, so it only shrinks (§411: 184 lines when
// it was written, none once the phases landed). And the exceptions the rules make on purpose, each with why.

namespace Karpo.Tests.Menus;

public sealed partial class MenuRulesTests
{
    /// <summary>The rules' own exceptions, each with why: not to be mended.</summary>
    internal static readonly Dictionary<string, string> Exceptions = new(StringComparer.Ordinal)
    {
        ["R8 Battery and power › Power & battery"] = "Windows' own Settings page, by its own name",
        ["R8 Connections › Bluetooth & devices"] = "Windows' own Settings page, by its own name",
        ["R8 Connections › Network & internet"] = "Windows' own Settings page, by its own name",
        ["R13 Karpo Pro"] = "each layout's line wears the layout's own icon, two the default one",
        ["R13 Tray icons"] = "each tray icon wears its own picture in the dock (the test lists no pictures)",
        // §425: a pin's Target is a choice of kind first, each kind its own level (the person: "put them under separate
        // menus, its okey if it is more than two levels deep"). The kind in force is ticked and says the target; the
        // others say nothing, where a "None" on four rows of five would read as four settings left empty.
        ["R4 Target › App"] = "a pin's kind not in force: the tick on the one in force says it (§425)",
        ["R4 Target › Folder"] = "a pin's kind not in force: the tick on the one in force says it (§425)",
        ["R4 Target › File"] = "a pin's kind not in force: the tick on the one in force says it (§425)",
        ["R4 Target › Website"] = "a pin's kind not in force: the tick on the one in force says it (§425)",
        ["R4 Target › Settings page"] = "a pin's kind not in force: the tick on the one in force says it (§425)",
        // §717: Windows search's home is the person's own list, seven sections until they order it (§655: "no expander
        // hiding one setting"), each a row of the menu's first level as a toggles row's toggles are: 12 DIP of it scroll.
        ["R2 [line WinSearch]"] = "its home's sections are a list of the person's own, seven by default (§655)",
    };

    internal static readonly HashSet<string> Allowed = new(StringComparer.Ordinal)
    {
    };
}
