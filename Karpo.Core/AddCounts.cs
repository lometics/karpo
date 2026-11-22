// What the add page counts (docs\widgets.md §586). The person: "in the add widgets menu show how many widgets from which
// type are already placed and also sum this up over the sections itself. that way the user is able to see immediately
// what he has".
//
// Counted from the config, here alone: the widgets of the layout the page adds into — on its bar and in each of its
// folders — by their kind (a widget's id, the file's word, which a catalogue row's id is), whatever page they stand on: a
// clock on the bar and a clock line in a folder are two clocks. One layout, not the whole file: the page adds into one,
// what one display shows; a clock of a layout no display shows, or of another display's, is no clock the person sees here.
//
// A tile counts its kind, shown again (Popular, Quick settings) the same; a folder of the tree sums its kinds, each once
// however often it shows one. A premade widget's tile and a ready-made folder's count nothing: the file keeps no trace of
// what a widget or a folder was made from, and its widget's own tile counts it already.

namespace Karpo.Core;

public static class AddCounts
{
    /// <summary>How many of each kind <paramref name="islands"/> (a layout's) holds, on the bar and in its folders, by the kind's id.</summary>
    public static IReadOnlyDictionary<string, int> Placed(IEnumerable<BarItem> islands)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (_, island) in Folders.All(islands))
        {
            if (island.Info?.Id is { } id)
            {
                counts[id] = counts.GetValueOrDefault(id) + 1;
            }
        }
        return counts;
    }

    /// <summary>
    /// What <paramref name="row"/> of the add tree counts of <paramref name="placed"/>: a widget's tile its kind's; a folder the
    /// sum of the kinds in it, each once; a premade widget, a ready-made folder and a line nothing.
    /// </summary>
    public static int Of(AddRow row, IReadOnlyDictionary<string, int> placed) => row switch
    {
        AddPick pick => KindOf(pick) is { } id ? placed.GetValueOrDefault(id) : 0,
        AddFolder folder => AddTree.Picks(folder.Rows).Select(KindOf).OfType<string>().Distinct(StringComparer.Ordinal).Sum(id => placed.GetValueOrDefault(id)),
        _ => 0,
    };

    /// <summary>The kind a pick counts: a widget's own; none for a premade widget or a ready-made folder.</summary>
    private static string? KindOf(AddPick pick) => pick.Entry is { Premade: null } entry ? entry.Id : null;
}
