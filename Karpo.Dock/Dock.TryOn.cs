// §622 (the person, 2026-11-09: "when hovering over an entry in the palette it should apply it / preview it with the whole
// taskbar and also in the preview itself. the hovering should also work in other selectors like the font selector or color
// selectors"): a config worn a moment, never the person's — one state for everything that shows a change before it is made:
//
//  - **A choice under the pointer or the keys** (TryOn): a menu whose rows hold a choice of the Appearance (LookRows.TriedOn:
//    the palette, a font, a pattern, a colour's swatch, "Default (…)") tells the row they rest on, a moment after
//    (Flyout.TellsHover, HoverMs); that choice is set on a copy of the config and the copy worn — every bar, every folder
//    open, the Appearance page's values and its preview, as a pick would show it. Off every row, out of the menu, on a row
//    that changes nothing, the config the person has is worn again.
//  - **A colour being mixed** (§523, Dock.MenuPickers.cs): each colour of the drag worn the same way.
//
// Worn is Apply without a save (no file written, no step of the undo, the open menu's rows not made again under the pointer):
// the one path every look change takes, which builds again only what changed. While a copy is worn, `config` is it and
// `wornOver` the person's config under it; whatever else the menu or the panel does — a pick, a close, a step — takes the copy
// off first (Settle), so a pick is one change of the person's config, one step of the undo; the bars are shown as that config
// has them again where nothing else was applied (ShowSettled). What saves without a pick (a countdown, a display seen)
// saves the person's config, never the copy (Persons).

using Karpo.Core;

namespace Karpo.Dock;

public sealed unsafe partial class Dock
{
    /// <summary>§622: the person's config while a copy of it is worn (<see cref="Wear"/>); null while none is.</summary>
    private Config? wornOver;

    /// <summary>§622: a copy is being put on: the open menu is not shown again for it, standing as it is under the pointer.</summary>
    private bool wearing;

    /// <summary>§622: the bars show a config other than <see cref="config"/>: a copy taken off and nothing applied since.</summary>
    private bool shownStale;

    /// <summary>§622: the row of the open menu whose choice is tried on (<see cref="TryOn"/>), the last one told; null for none.</summary>
    private (Pending What, uint Id)? tried;

    /// <summary>§622: the config as the person has it, whatever is worn a moment: what is saved and what a change is made from.</summary>
    private Config Persons => wornOver ?? config;

    /// <summary>Tests only: whether a copy of the config is worn a moment.</summary>
    internal bool Wearing => wornOver is not null;

    /// <summary>
    /// §622: <paramref name="next"/> — a copy of the person's config with a change on it — worn: shown everywhere as a change
    /// is, nothing saved, no step of the undo, the menu open left standing.
    /// </summary>
    private void Wear(Config next)
    {
        wornOver ??= config;
        wearing = true;
        try
        {
            Apply(next, save: false);
        }
        finally
        {
            wearing = false;
        }
    }

    /// <summary>
    /// §622: the copy worn taken off: <see cref="config"/> the person's again, the bars left as they stand for what comes
    /// next to show (<see cref="shownStale"/>); whether one was.
    /// </summary>
    private bool Settle()
    {
        tried = null;
        if (wornOver is not { } persons)
        {
            return false;
        }
        wornOver = null;
        wornPages.Clear();
        config = persons;
        shownStale = true;
        return true;
    }

    /// <summary>§622: the bars shown as the config is again, where a copy was taken off and nothing applied since.</summary>
    private void ShowSettled()
    {
        if (shownStale && !disposed)
        {
            Apply(config, save: false);
        }
    }

    /// <summary>§622: the copy worn taken off and the person's config shown again; nothing where none is worn.</summary>
    private void TakeOff()
    {
        Settle();
        ShowSettled();
    }

    /// <summary>
    /// §622: the open menu <paramref name="what"/> told the row <paramref name="id"/> the pointer or the keys rest on (0: none):
    /// its choice of the Appearance (<see cref="LookRows.TriedOn"/>) set on a copy of the person's config and worn, as a pick
    /// would show it; anything else — no row, another kind of row, a choice that changes nothing — the person's config worn
    /// again. Nothing while a colour is being mixed: the mix wears its own.
    /// </summary>
    private void TryOn(Pending what, uint id)
    {
        if (mixing is not null || disposed || !ReferenceEquals(pending, what))
        {
            return;
        }
        if (tried is { } was && ReferenceEquals(was.What, what) && was.Id == id)
        {
            return;
        }
        if (Tried(what, id) is { } next)
        {
            tried = (what, id);
            Wear(next);
            return;
        }
        TakeOff();
    }

    /// <summary>
    /// §622: the person's config with the choice of <paramref name="what"/>'s row <paramref name="id"/> on it, a copy; null where
    /// that row is no choice of the Appearance, or its choice changes nothing.
    /// </summary>
    private Config? Tried(Pending what, uint id)
    {
        if (id == 0 || OptionMenu.PickOf(what.Options, id) is not { Kind: OptionPickKind.Set } pick
            || RowOfPick(what, pick) is not { } rowIn)
        {
            return null;
        }
        var next = Persons.Clone();
        return rowIn(next) is { Enabled: true } row && TriesOn(row) && OptionMenu.ApplyRow(row, pick) && !Same(next, Persons) ? next : null;
    }

    /// <summary>
    /// §622, §684 (the person, 2026-11-12: "this should be done for other settings too, this should be a general design rule,
    /// not just for the font"): whether a choice of <paramref name="row"/> is tried on while it is pointed at — every value of
    /// the Appearance, and every choice of how a thing is drawn (a widget's or a folder's style, look, placement), the bar's
    /// kind and how its app buttons combine; not one that moves the bar from under the pointer (its edge, how it shows), nor
    /// one that changes no look (a language, a behaviour).
    /// </summary>
    internal static bool TriesOn(OptionRow row) =>
        LookRows.TriedOn(row.Key)
        || (row.Slot is OptionSlot.Style or OptionSlot.Look or OptionSlot.Placement && row.Key != KarpoMenu.AnimationsRow)
        || row.Key is BarLooks.TypeRow or BarLooks.CombineRow;

    /// <summary>
    /// §684: the choice <paramref name="id"/> of page <paramref name="page"/> of display <paramref name="displayId"/>'s panel
    /// the pointer rests on (0: none), tried on as a popup's row is (<see cref="TryOn"/>); off every choice, the person's config
    /// worn again.
    /// </summary>
    private void PanelHovered(string displayId, PanelPage page, uint id)
    {
        if (mixing is not null || disposed)
        {
            return;
        }
        if (!panelPages.TryGetValue((displayId, page), out var built) || Tried(built.What, id) is not { } next)
        {
            TakeOff();
            return;
        }
        if (tried is { } was && ReferenceEquals(was.What, built.What) && was.Id == id)
        {
            return;
        }
        tried = (built.What, id);
        Wear(next);
    }

    /// <summary>
    /// §622: whether <paramref name="kind"/>, said by the open menu, is something done in it — a pick, a close, a step, a key
    /// passed on —, which a choice tried on comes off for first; not the pointer's or the keys' rest (Hover), nor a colour
    /// mixed (its own, Dock.MenuPickers.cs).
    /// </summary>
    internal static bool Settles(FlyoutEventKind kind) =>
        kind is FlyoutEventKind.Invoke or FlyoutEventKind.Dismissed or FlyoutEventKind.Context or FlyoutEventKind.Adjust
            or FlyoutEventKind.PassedKey or FlyoutEventKind.Reorder;
}
