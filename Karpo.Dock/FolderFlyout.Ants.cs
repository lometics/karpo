// edit: the lines of the folder on edit mode's clipboard wear Excel's marching ants (MarchingAnts.cs,
// Dock.EditCopy.cs), while the folder is arranged: round each line where its selected ring would
// stand — just outside it, over everything but the "+" buttons and the focus rectangle — and in
// the ring's place where it is selected too. Not while lines are carried, give way or glide: the ants
// come back round them once they rest. §524: a spacer's stripes (SpacerStripes.cs) move on the same
// terms, on the ants' loop (MoveBands).

using System.Numerics;
using Karpo.Core;

namespace Karpo.Dock;

public sealed unsafe partial class FolderFlyout
{
    // edit: the keys of the widgets on the clipboard (the dock's), and the ants made for as many lines as wanted them.
    private IReadOnlyList<string> copied = [];
    private readonly List<MarchingAnts> ants = [];

    // §357: what each of the ants was made of, the folder's: kept for the next showing, closed as the look changes.
    private readonly List<List<IDisposable>> antsOwned = [];

    /// <summary>edit: the keys of the widgets on edit mode's clipboard; those of this folder's lines wear the marching ants while it is arranged.</summary>
    internal IReadOnlyList<string> Copied
    {
        get => copied;
        set
        {
            if (copied.SequenceEqual(value, StringComparer.OrdinalIgnoreCase))
            {
                return;
            }
            copied = [.. value];
            ShowStates();
        }
    }

    /// <summary>Tests only: the lines wearing the marching ants now, by index.</summary>
    internal IReadOnlyList<int> CopiedShown => [.. CopiedLines().Where((_, at) => at < ants.Count && ants[at].Visual.IsVisible)];

    /// <summary>Tests only: whether every line wearing the ants has them marching.</summary>
    internal bool AntsMarch => ants.Where(each => each.Visual.IsVisible).All(each => each.Marching);

    /// <summary>edit: whether line <paramref name="index"/> is on the clipboard.</summary>
    private bool IsCopied(int index) =>
        index >= 0 && index < tiles.Count && copied.Contains(tiles[index].Island.Key, StringComparer.OrdinalIgnoreCase);

    /// <summary>edit: the lines on the clipboard, in the folder's order.</summary>
    private List<int> CopiedLines() => [.. Enumerable.Range(0, tiles.Count).Where(IsCopied)];

    /// <summary>
    /// edit: the marching ants round every line on the clipboard that shows (see the top of the file),
    /// made as many as are wanted; hidden for the rest. Part of <see cref="ShowStates"/>.
    /// </summary>
    private void ShowAnts(FolderPlan shown)
    {
        // Not while lines give way, are lifted or glide: the ants stand still, and come back when they rest.
        var lines = shown.Editing && Resting ? CopiedLines() : [];
        while (ants.Count < lines.Count && ringsLayer is not null && pickedVisual is not null)
        {
            var owned = new List<IDisposable>();
            var made = new MarchingAnts(host.Compositor, theme.Accent, look.Animations, value =>
            {
                objectsMade++;
                owned.Add(value);
                return value;
            });
            ringsLayer.Children.InsertAbove(made.Visual, pickedVisual);
            ants.Add(made);
            antsOwned.Add(owned);
        }
        var thick = EditLook.Ring(EditLook.Hair(scale));
        for (var each = 0; each < ants.Count; each++)
        {
            var line = each < lines.Count ? lines[each] : NoTarget;
            var rect = line >= 0 && line < shown.Cards.Count ? Shown(shown, shown.CardList[line], shown.Grab(line)) : default;
            var show = line >= 0 && line != dragging && shown.Holds(line) && rect.bottom > rect.top;
            if (!show)
            {
                ants[each].Show(false, default, default, 0, 0);
                continue;
            }
            // Where the selected ring stands (EditLook.cs): on the line's edge, inside it, on whole pixels.
            var (left, top) = (MathF.Round(rect.left), MathF.Round(rect.top));
            var (right, bottom) = (MathF.Round(rect.right), MathF.Round(rect.bottom));
            ants[each].Show(true, new Vector2(left, top), new Vector2(right - left, bottom - top), shown.Radius, thick);
        }
    }

    /// <summary>edit: no line gives way, is lifted or glides: what moves while the folder is arranged (the ants, a spacer's stripes) may move.</summary>
    private bool Resting => shifts is null && !unruled && raised.Count == 0;

    /// <summary>Tests only: each line's spacer stripes (<see cref="FolderPlan.Bands"/>), in the folder's order.</summary>
    internal IReadOnlyList<SpacerStripes> Stripes => bands;

    /// <summary>Tests only: whether every spacer's stripes that show are moving (true), or every one stands still (false); null for some of each or none shown.</summary>
    internal bool? BandsMove =>
        bands.Where(band => band.Visual.IsVisible).Select(band => band.Marching).Distinct().ToList() is [var all] ? all : null;

    /// <summary>
    /// §524: a spacer's stripes move on the ants' loop (MarchingAnts.Loop) while the folder is arranged and shows them, as the
    /// ants do: not while lines give way, are lifted or glide, nor with Karpo's Animations off; at rest they are hidden
    /// (FolderPlan.Bands empty), and closed the folder stops them (StillKept). Part of <see cref="ShowStates"/>.
    /// </summary>
    private void MoveBands(FolderPlan shown)
    {
        var moving = look.Animations && shown.Editing && Resting;
        for (var index = 0; index < bands.Count; index++)
        {
            var striped = index < shown.Bands.Count && !shown.Bands[index].IsEmpty && index != dragging;
            bands[index].Move(bandLoop, moving && striped);
        }
    }
}
