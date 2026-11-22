// edit, §675 (the person, 2026-11-12: "make sure that you not just add the text but actually also add an image that tries to
// explain / show the effect of the change too", "the different styles should get a preview like the patterns for every style
// in the dropdown"): a settings page's choices that change how a thing is drawn each pictured by what it draws — Karpo's own
// drawing of it with that choice made (LookShown.Zoomed, LookPreviews), never a drawing made by hand:
//
//  - **A widget's or a folder's** Style, Look or Placement (OptionSlot): the widget alone on a piece of its bar, set so.
//  - **The Appearance's** Palette and Mode: a piece of a bar of stand-ins (LookPreview.BarStandIns) wearing it.
//  - §679 **How a display's bar stands and behaves** — Show, Position, its layout, Combine buttons, Show windows from, Open
//    flyouts —: a piece of the bar close up where the choice shows (LookShown.Sketch, BarSketch), never the whole display.
//
// What a choice does (Behaviour), what it is of (Subject, Scope) and its words keep their icon: a picture of the same widget
// would say nothing of them. A colour's swatches, a pattern's tiles and a font's face are pictures already. Each choice's
// config is a copy with that choice set (its row's own Set, as a pick sets it); worked out again only when what it is drawn
// from has changed (the widget's own settings, the Appearance, the bar's kind and size), so a page made again at every step
// of a drag clones nothing.

using Karpo.Core;

namespace Karpo.Dock;

public sealed unsafe partial class Dock
{
    /// <summary>§675: the most choices a picker pictures; one of more is a list.</summary>
    internal const int PicturedMost = 8;

    /// <summary>§675: the pictures of a row's choices as last worked out, by what they were drawn from.</summary>
    private readonly Dictionary<string, LookShown[]> choicePictures = new(StringComparer.Ordinal);

    /// <summary>
    /// §675: <paramref name="items"/> (a page of the menu <paramref name="what"/>), each choice that changes how its thing is
    /// drawn given the picture of what it draws (FlyoutItem.Shows), in every level; the rest as they are.
    /// </summary>
    private List<FlyoutItem> Pictured(List<FlyoutItem> items, Pending what)
    {
        if (what.Keys.Count > 1)
        {
            return items;
        }
        List<FlyoutItem> made = new(items.Count);
        foreach (var row in items)
        {
            // §672: a row of segments its choices, as the panel reads it.
            var item = row.Control is FlyoutControl.Segments segments ? row with { Control = null, Children = PanelRows.ChoicesOf(segments), PickOne = true } : row;
            var kids = item.Children is { Count: > 0 } children ? Pictured([.. children], what) : null;
            var next = kids is null ? item : item with { Children = kids };
            if (next.PickOne && kids is not null && PicturesOf(next, kids, what) is { } pictures)
            {
                var at = 0;
                next = next with { Children = [.. kids.Select(kid => PanelPickers.Choice(kid) ? kid with { Shows = pictures[at++] } : kid)] };
            }
            made.Add(next);
        }
        return made;
    }

    /// <summary>
    /// §675: the picture of each choice of <paramref name="item"/> (its level <paramref name="kids"/>), in their order: what it
    /// draws with that choice set; null where its choices are not pictured (see the top of the file).
    /// </summary>
    private LookShown[]? PicturesOf(FlyoutItem item, List<FlyoutItem> kids, Pending what)
    {
        var choices = kids.Where(PanelPickers.Choice).ToList();
        if (choices.Count < 2 || PanelPickers.Colours(item)
            || OptionMenu.PickOf(what.Options, choices[0].Id) is not { Kind: OptionPickKind.Set } first
            || RowOfPick(what, first) is not { } rowIn
            || rowIn(config) is not { } row
            || !Pictures(row.Key, choices.Count, choices.Any(kid => kid.Icon is FlyoutTile)))
        {
            return null;
        }
        var picks = choices.Select(kid => OptionMenu.PickOf(what.Options, kid.Id)).ToList();
        if (picks.Any(pick => pick is not { Kind: OptionPickKind.Set } || pick.Key != first.Key))
        {
            return null;
        }
        // What it is drawn on: the widget alone, or the Appearance's stand-ins; none for a setting not of how it looks.
        Func<Config, LookShown?>? drawn = null;
        string from;
        if (what.Looks is { } bar && row.Key is LookRows.PaletteRow or LookRows.ModeRow && config.ActiveLayout(bar.Display) is { } shown)
        {
            drawn = of => StandIns(of, shown);
            from = Config.AppearanceSaid(config) + "|" + shown.Type + "|" + shown.Size;
        }
        else if (what.Target.Length > 0 && row.Slot is OptionSlot.Style or OptionSlot.Look or OptionSlot.Placement
            && Islands.OwnerOf(what.Target) is var owner && config.LayoutOf(owner) is { } layout
            && config.FindIsland(owner, Islands.IdOf(what.Target)) is BarItem island)
        {
            var id = island.Id;
            drawn = of => of.LayoutOf(owner) is { } its && of.FindIsland(owner, id) is BarItem alone ? Alone(of, its, alone) : null;
            from = Config.Said(island) + "|" + Config.AppearanceSaid(config) + "|" + layout.Type + "|" + layout.Size;
        }
        // §679: how a display's bar stands and behaves: a piece of it close up, where the choice shows.
        else if (what.Looks is { } spot && SketchOf(row.Key, spot) is var (sketch, display))
        {
            drawn = of => Sketched(of, sketch, display);
            from = Config.AppearanceSaid(config) + "|" + Config.LayoutsSaid(config) + "|" + Config.ScreenSaid(config, display);
        }
        else
        {
            return null;
        }
        var key = what.Target + "|" + row.Key + "|" + string.Join(',', choices.Select(kid => kid.Text)) + "|" + from;
        if (choicePictures.TryGetValue(key, out var kept))
        {
            return kept;
        }
        var pictures = new LookShown[picks.Count];
        for (var at = 0; at < picks.Count; at++)
        {
            var copy = config.Clone();
            if (rowIn(copy) is not { Set: { } set } || !Sets(() => set(picks[at]!.Value)) || drawn(copy) is not { } shows)
            {
                return null;
            }
            pictures[at] = shows;
        }
        // Only the latest of a row kept: an older is what the page drew before.
        foreach (var stale in choicePictures.Keys.Where(each => each.StartsWith(what.Target + "|" + row.Key + "|", StringComparison.Ordinal)).ToList())
        {
            choicePictures.Remove(stale);
        }
        choicePictures[key] = pictures;
        return pictures;
    }

    /// <summary>
    /// §675: whether the row <paramref name="key"/>'s <paramref name="count"/> choices are pictured by what they draw: at most
    /// <see cref="PicturedMost"/>, none already a tile of its own (<paramref name="tiled"/>: a pattern's, a colour's). §681 (the
    /// person, 2026-11-12: "the palettes are not shown with previews anymore. why?"): the Appearance's Palette and Mode
    /// always — a palette's one swatch says little of the bar it dresses, and there are more than eight of them; the gate let
    /// them through to no picture at all.
    /// </summary>
    internal static bool Pictures(string key, int count, bool tiled) =>
        key is LookRows.PaletteRow or LookRows.ModeRow || (count <= PicturedMost && !tiled);

    /// <summary>
    /// §679: how the row <paramref name="key"/> of a bar is sketched and the display it is of: Taskbar's Show and its
    /// Behavior's choices, the display's of <paramref name="spot"/>; a display's edge and layout (the Layouts page's
    /// displays), the display it is of; null for any other.
    /// </summary>
    internal static (BarSketch Sketch, string Display)? SketchOf(string key, BarSpot spot)
    {
        switch (key)
        {
            case BarLooks.TypeRow: return (BarSketch.Type, spot.Display);
            case BarLooks.ShowRow: return (BarSketch.Show, spot.Display);
            case BarLooks.CombineRow: return (BarSketch.Buttons, spot.Display);
            case BarLooks.DesktopsRow: return (BarSketch.Desktops, spot.Display);
            case BarLooks.HoverRow: return (BarSketch.Opens, spot.Display);
            case KarpoMenu.AnimationsRow: return (BarSketch.Motion, spot.Display);
        }
        // §700 (the person, 2026-11-13: "layouts should show a little preview for all the layouts vertically"; §682 had made
        // them their names alone): a display's layout each its own bar, a line each.
        foreach (var (row, sketch) in (ReadOnlySpan<(string, BarSketch)>)[(BarLooks.PositionRow, BarSketch.Edge), (BarLooks.ShownRow, BarSketch.Layout)])
        {
            if (key.StartsWith(row + ":", StringComparison.Ordinal))
            {
                return (sketch, key[(row.Length + 1)..]);
            }
        }
        return null;
    }

    /// <summary>
    /// §679: <paramref name="display"/>'s bar of <paramref name="of"/> sketched as <paramref name="sketch"/> says: its own
    /// widgets for its layout (none where the display shows none), the Apps widget alone for its app buttons, stand-ins else
    /// — its kind, size and behaviour its layout's.
    /// </summary>
    internal static LookShown Sketched(Config of, BarSketch sketch, string display)
    {
        var layout = of.ActiveLayout(display);
        var shown = sketch switch
        {
            BarSketch.Layout => of.ScreenFor(display).Enabled ? layout : Strip(layout, []),
            BarSketch.Type or BarSketch.Edge or BarSketch.Opens or BarSketch.Motion => Strip(layout, LookPreview.EdgeStandIns()),
            BarSketch.Buttons or BarSketch.Desktops => Strip(layout, [BarKind.Apps.New()]),
            _ => Strip(layout, LookPreview.BarStandIns()),
        };
        return new LookShown(of, shown) { Itself = true, Sketch = sketch, Display = display };
    }

    /// <summary>§675: whether <paramref name="set"/> ran: a choice its copy cannot take (whatever it is) left unpictured.</summary>
    private static bool Sets(Action set)
    {
        try
        {
            set();
            return true;
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Console.Error.WriteLine($"Karpo: a choice's picture not worked out: {error.Message}");
            return false;
        }
    }

    /// <summary>§675: <paramref name="item"/> of <paramref name="of"/> alone on a piece of its bar (<paramref name="layout"/>'s kind and size), zoomed.</summary>
    internal static LookShown Alone(Config of, TaskbarLayout layout, BarItem item) => new(of, Strip(layout, [item])) { Itself = true, Zoomed = true };

    /// <summary>§675: the Appearance of <paramref name="of"/> on a piece of a bar of stand-ins of <paramref name="layout"/>'s kind and size, zoomed.</summary>
    internal static LookShown StandIns(Config of, TaskbarLayout layout) => new(of, Strip(layout, LookPreview.BarStandIns())) { Itself = true, Zoomed = true };

    /// <summary>§675: a bar of <paramref name="layout"/>'s kind and size (§679 and behaviour) holding <paramref name="items"/> alone, in its middle.</summary>
    private static TaskbarLayout Strip(TaskbarLayout layout, List<BarItem> items)
    {
        foreach (var item in items)
        {
            item.Section = BarSection.Middle;
        }
        return new TaskbarLayout
        {
            Id = layout.Id,
            Type = layout.Type,
            Size = layout.Size,
            Show = layout.Show,
            Combine = layout.Combine,
            AllDesktops = layout.AllDesktops,
            OpenOnHover = layout.OpenOnHover,
            Islands = items,
        };
    }
}
