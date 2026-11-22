// menus: what a text or a search field of a widget's menu kept, set as Settings sets it (docs\widgets.md
// §219; Flyout.Fields.cs). The menu says FlyoutEventKind.Adjust for the field's row and holds what it
// kept — the words typed, or a search's pick, proved — until it is taken here: set on the widget keyed
// by the menu, or on its folder's face, through the very row the menu showed (OptionRows, OptionFields:
// the settings app's own writes), saved as every change of the dock's is, and the menu shown again
// with the setting as it is now (OptionPicked).

using Karpo.Core;

namespace Karpo.Dock;

public sealed unsafe partial class Dock
{
    /// <summary>
    /// menus: the field of <paramref name="pick"/> in the widget's menu (<paramref name="what"/>) kept
    /// <paramref name="kept"/>: set where the field's row was read from, and saved where anything changed.
    /// </summary>
    private void FieldKept(Pending what, OptionPick pick, FieldCommit kept)
    {
        // A bar's looks' menu: a layout named (§309, §333).
        if (what.Looks is { } bar)
        {
            _ = LooksKept(bar, what.LooksScope, pick, kept);
            return;
        }
        // The edit bar's ⚙ Settings (§335): a holiday country added, and what later phases type there.
        if (what.SettingsMenu is { } settings)
        {
            SettingsKept(settings.Page, pick, kept);
            return;
        }
        // §528: on every widget of a page of several selected, in one copy of the file, saved once.
        var next = config.Clone();
        if (what.Keys.Count > 0 && FieldPicks.Keep(TargetsOf(next, what.Keys), pick, kept))
        {
            Apply(next);
        }
    }

    /// <summary>
    /// menus: the menu (<paramref name="what"/>) closed with words typed in a text field and left there,
    /// which the page's box would have kept as it lost the focus: kept.
    /// </summary>
    private void FieldsLeft(Pending what, Flyout list)
    {
        foreach (var kept in list.TakeFieldsLeft())
        {
            if (OptionMenu.PickOf(what.Options, kept.Id) is { Kind: OptionPickKind.Field } pick)
            {
                FieldKept(what, pick, kept);
            }
        }
    }
}

/// <summary>menus: what a text or a search field kept, set where its row was read from (see Dock.MenuFields.cs).</summary>
internal static class FieldPicks
{
    /// <summary>
    /// <paramref name="kept"/> set on <paramref name="item"/> — the widget as the file has it, in a copy
    /// saved where this says it changed — through the row <paramref name="pick"/> named: its own
    /// setting, or its folder's face's while the face in that place is still of the kind it was shown
    /// for. A search's pick is kept by its row, an icon's search's too (OptionPickers); what a line's
    /// list is made, renamed or deleted in is <paramref name="devices"/>' notebook.
    /// </summary>
    /// <param name="place">The config the widget is of, which a row of the person's own writes to (the holidays', §237).</param>
    public static bool Keep(WidgetItem? item, OptionPick pick, FieldCommit kept, OptionDevices? devices = null, OptionPlace? place = null) =>
        Keep([new OptionTarget(item, devices, place)], pick, kept);

    /// <summary>
    /// §528: <paramref name="kept"/> set on every one of <paramref name="targets"/> (the widgets selected) through the row they
    /// have in common (OptionMenu.Row): its own setting's, its face's, or (§523: a colour's hex box) its look's.
    /// </summary>
    public static bool Keep(IReadOnlyList<OptionTarget> targets, OptionPick pick, FieldCommit kept) =>
        targets.All(target => target.Item is not null) && pick.Kind == OptionPickKind.Field && KeepRow(OptionMenu.Row(targets, pick), kept);

    /// <summary>
    /// <paramref name="kept"/> set through <paramref name="row"/>, the field's row as it is now, whichever menu it
    /// is of (a widget's, a bar's looks, ⚙); false where it has none, may not be used now, or nothing changed.
    /// </summary>
    public static bool KeepRow(OptionRow? row, FieldCommit kept) => row switch
    {
        // A line of text keeps the words typed, or what it offered that was picked (§304); a number typed (§333:
        // a bar's size) the same.
        { Kind: OptionRowKind.Text or OptionRowKind.Number, Enabled: true, SetText: { } set } => set(kept.Found?.Value as string ?? kept.Text),
        // §521: a number typed in its box in place, held to its range and its steps (OptionUnits.Typed).
        { Kind: OptionRowKind.Number, Enabled: true, Set: { } set } => OptionUnits.Typed(row, kept.Text) is { } number && set(number),
        // §425: a search whose words are its value (a pin's website, a camera's address: OptionFinder.Suggests) keeps
        // them, Enter or left, where they are a thing of their own (OptionFinder.Typed); words that are none nothing.
        { Pick: { } keep } => (kept.Found ?? row.Finder?.Typed(kept.Text)?.Found) is { } found && keep(found),
        // §523: a colour's hex box: "#RRGGBB", or "#RRGGBBAA" where it takes an alpha, kept as a colour of one's own; else nothing.
        { Kind: OptionRowKind.Color, SetColor: { } mix } => ColorMix.ParseHex(kept.Text, row.Alpha) is { } typed && mix(typed, true),
        _ => false,
    };
}
