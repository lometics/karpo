// §532: "Taskbar menu" (the person, 2026-11-07: "the right click context menu of the taskbar should be editable in the
// settings again. it should be scoped on a global level"): the taskbar's own right-click menu, one for Karpo as a whole
// (Config.BarMenu, BarMenus.cs). §543 a page of its own; §553 (the person, 2026-11-07: "taskbar menu should be folded into
// taskbar configuration page."): the Taskbar page's last section, after Bar and Behavior, under its heading "Taskbar menu",
// whose caption says it is every layout's (the page is the layout's, the menu Karpo's). §585 (the person, 2026-11-09:
// "taskbar menu should only get an expansion section with the configuration inside there but not a full own section"): no
// section, one expander, Behavior's last row ("Taskbar menu", "The same on every layout" under it: BarMenu); its rows in it:
//
//   each entry, in the menu's order   a row: its name and icon (a shortcut's as Windows names its target, a heading's
//                                     words), §551 dragged into order by its handle (no ↑ ↓; Taskbar settings too), at its end Remove —
//                                     Taskbar settings none, why under its name (BarMenus.Required); a shortcut's and a heading's an
//                                     expander of their own rows —
//                                     a shortcut's Target (a pin's, its kinds: OptionFields.OfTarget), Name, Arguments;
//                                     a heading's Name
//   Add an entry                      a button, its list the built-in rows the menu has not (as Windows names them),
//                                     then Separator, Heading and Shortcut; what is picked lands at the menu's end
//   Reset to default                  a button, asked first; greyed while the menu is the default
//
// Every change goes through BarMenus.Change: on a copy of the menu, repaired, and out of the file where it is the
// default again. It is a setting as any other of the page: saved at once, one step of edit mode's undo (Dock.Apply).
// §551: at its top, over them all, the menu itself as the bar draws it now (the expander's Shows: the settings panel's
// preview; §551 to §584 under the section's heading).

namespace Karpo.Core;

public sealed partial record KarpoServices
{
    /// <summary>
    /// §532: what a shortcut of the taskbar's menu is offered to open and how a target is named, as a pin's Target offers it
    /// (the dock asks this PC's shell, TargetLooks.Choices); asked only for a menu with a shortcut. Null for nothing offered.
    /// </summary>
    public Func<TargetChoices>? Targets { get; init; }
}

public static partial class KarpoMenu
{
    // §532: the expander's keys; an entry's rows are keyed by its id (BarMenuKey).
    public const string BarMenuRow = "BarMenu";
    public const string BarMenuAddRow = "BarMenuAdd";
    public const string BarMenuResetRow = "BarMenuReset";

    /// <summary>§532: the key of <paramref name="part"/> of the menu's entry <paramref name="id"/>: "BarMenu.edit.Remove".</summary>
    public static string BarMenuKey(string id, string part) => $"{BarMenuRow}.{id}.{part}";

    /// <summary>
    /// §532: "Taskbar menu": the menu's entries as the person's own things, Add an entry, Reset to default (see the top of the
    /// file). §585: one expander, the Taskbar page's Behavior's last row — "The same on every layout" under its name (§553 the
    /// caption of a section of its own), §551 showing the menu as <paramref name="layout"/>'s bar draws it
    /// (<see cref="OptionRow.Shows"/>: the settings panel's preview, at its top).
    /// </summary>
    public static OptionRow BarMenu(Config config, KarpoServices services, TaskbarLayout layout)
    {
        var entries = config.BarMenuEntries;
        TargetChoices? asked = null;
        TargetChoices Targets() => asked ??= services.Targets?.Invoke() ?? new TargetChoices();
        var addable = BarMenus.Addable(entries);
        List<string> choices = [.. addable.Select(info => info.Name), S.CoreWidgetDividerTitle, S.CoreWidgetHeadingTitle, S.SettingsBarMenuShortcut];
        BarMenuEntry Made(int at) =>
            at < addable.Count ? BarMenuEntry.Of(addable[at].Action)
            : at == addable.Count ? BarMenuEntry.Divider()
            : at == addable.Count + 1 ? BarMenuEntry.Heading(S.CoreWidgetHeadingTitle)
            : BarMenuEntry.Shortcut("");
        var add = new OptionRow(BarMenuAddRow, OptionRowKind.Choice, S.SettingsBarMenuAdd)
        {
            Glyph = OptionGlyphs.New,
            Adds = true,
            // As many as the menu has not: a list however few, never side by side (§521).
            Grows = true,
            Enabled = entries.Count < BarMenus.MostEntries,
            Choices = choices,
            ChoiceGlyphs = [.. addable.Select(info => info.Glyph), WidgetInfos.Divider.Glyph, WidgetInfos.Heading.Glyph, BarMenus.ShortcutGlyph],
            Set = at => at >= 0 && at < choices.Count && BarMenus.Change(config, list => BarMenus.Add(list, Made(at))),
        };
        var reset = new OptionRow(BarMenuResetRow, OptionRowKind.Action, S.SettingsBarMenuReset)
        {
            Glyph = OptionGlyphs.Reset,
            Enabled = config.BarMenu is not null,
            Ask = new DeleteQuestion(S.SettingsBarMenuResetTitle, S.SettingsBarMenuResetConfirm, S.SettingsBarMenuResetButton, S.DockDeleteCancel, 0),
            Set = _ =>
            {
                if (config.BarMenu is null)
                {
                    return false;
                }
                config.BarMenu = null;
                return true;
            },
        };
        return new OptionRow(BarMenuRow, OptionRowKind.Things, S.SettingsBarMenuHeader)
        {
            Glyph = OptionGlyphs.Menu,
            Detail = S.SettingsBarMenuEveryLayout,
            // §684 (the person, 2026-11-12: "taskbar menu should just show how many entries there are in it and not which ones").
            Holds = S.SettingsBarMenuEntries(entries.Count),
            Shows = new LookShown(config, layout) { Menu = entries },
            Owned = new OptionOwned(add, [.. entries.Select(entry => Thing(config, entry, Targets))]) { After = [reset] },
        };
    }

    /// <summary>
    /// §532: <paramref name="entry"/> as a thing of "Taskbar menu": named and pictured as the menu draws it, a shortcut and a
    /// heading with their own rows; its Remove (greyed on "Taskbar settings", saying why) and its move among the entries.
    /// </summary>
    private static OptionThing Thing(Config config, BarMenuEntry entry, Func<TargetChoices> targets)
    {
        var id = entry.Id;
        bool Edit(Func<BarMenuEntry, bool> change) => BarMenus.Change(config, list => list.Find(each => each.Id == id) is { } found && change(found));
        bool Words(string typed, Func<BarMenuEntry, string?> get, Action<BarMenuEntry, string?> set, int longest) => Edit(found =>
        {
            var words = typed.Trim() is { Length: > 0 } trimmed ? trimmed[..Math.Min(trimmed.Length, longest)] : null;
            if (get(found) == words)
            {
                return false;
            }
            set(found, words);
            return true;
        });
        OptionRow Text(string part, string title, string text, string placeholder, int longest, Func<string, bool> set) => new(BarMenuKey(id, part), OptionRowKind.Text, title)
        {
            Glyph = OptionGlyphs.Name,
            Field = OptionField.Text,
            Text = text,
            Placeholder = placeholder,
            Longest = longest,
            SetText = set,
        };
        string title;
        string glyph;
        string? summary = null;
        IReadOnlyList<OptionRow> rows = [];
        switch (entry.Kind)
        {
            case BarMenuKind.Action:
                var info = BarMenus.Info(entry.Action);
                (title, glyph) = (info?.Name ?? "", info?.Glyph ?? BarMenus.ShortcutGlyph);
                break;
            case BarMenuKind.Divider:
                (title, glyph) = (S.CoreWidgetDividerTitle, WidgetInfos.Divider.Glyph);
                break;
            case BarMenuKind.Heading:
                (title, glyph, summary) = (entry.Name ?? S.CoreWidgetHeadingTitle, WidgetInfos.Heading.Glyph, S.CoreWidgetHeadingTitle);
                rows =
                [
                    Text("Name", S.SettingsPinNameHeader, entry.Name ?? "", S.SettingsWidgetHeadingTextPlaceholder, BarMenus.MostHeading,
                        typed => Words(typed, found => found.Name, (found, words) => found.Name = words, BarMenus.MostHeading)),
                ];
                break;
            default:
            {
                var target = entry.Target ?? "";
                var named = target.Length > 0 ? targets().NameOf(target) : null;
                (title, glyph, summary) = (entry.Name ?? named ?? S.SettingsBarMenuShortcut, entry.IconIsGlyph ? entry.Icon! : BarMenus.ShortcutGlyph, S.SettingsBarMenuShortcut);
                rows =
                [
                    OptionFields.OfTarget(target, picked => Edit(found =>
                    {
                        var now = PinTargets.Normalize(picked ?? "");
                        if ((found.Target ?? "") == now)
                        {
                            return false;
                        }
                        found.Target = now;
                        return true;
                    }), targets(), BarMenuKey(id, "")),
                    Text("Name", S.SettingsPinNameHeader, entry.Name ?? "", named ?? "", OptionFields.PinNameLength,
                        typed => Words(typed, found => found.Name, (found, words) => found.Name = words, OptionFields.PinNameLength)),
                    Text("Arguments", S.SettingsBarMenuArguments, entry.Arguments ?? "", S.SettingsBarMenuArgumentsPlaceholder, ArgumentsLongest,
                        typed => Words(typed, found => found.Arguments, (found, words) => found.Arguments = words, ArgumentsLongest)),
                ];
                break;
            }
        }
        // "Taskbar settings" has no Remove: under its name why it stays (BarMenus.Required).
        var why = BarMenus.WhyNotRemove(entry);
        return new OptionThing(title, glyph)
        {
            Summary = why ?? summary,
            Rows = rows,
            Delete = why is not null ? null : new(BarMenuKey(id, "Remove"), OptionRowKind.Action, S.DockMenuRemove)
            {
                Glyph = OptionGlyphs.Delete,
                Set = _ => BarMenus.Change(config, list => BarMenus.Remove(list, list.FindIndex(each => each.Id == id))),
            },
            Move = new(BarMenuKey(id, "Move"), OptionRowKind.Action, S.DockEditFaceDrag)
            {
                Move = to => BarMenus.Change(config, list => BarMenus.Move(list, list.FindIndex(each => each.Id == id), to)),
            },
        };
    }

    /// <summary>§532: a command's arguments, at most: a command line's, not a script.</summary>
    private const int ArgumentsLongest = 1024;
}
