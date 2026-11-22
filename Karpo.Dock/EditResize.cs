// edit: Shift+arrows resize the widget the keys are on in edit mode, the bar's and an open folder's (docs\widgets.md
// §471; the person, 2026-11-01: "sounds good. go ahead" to "Shift+Up/Down changes its height by one cell,
// Shift+Left/Right its width where widths are adjustable ... same undo as the menu change"). No size of its own:
// a step is what the widget's menu would set, through the menu's own rules, so the two cannot differ.
//
//   Shift+Down, Shift+Up      a folder line a cell taller, shorter: its menu's rows stepper ("+", "−";
//                             LineHeightRows.Act), in either mode, held to the kind's limits and the folder's
//                             room, nothing under Fill; a sized line (§432) the next size its kind offers, the
//                             one before (its "Size" row)
//   Shift+Right, Shift+Left   a bar widget wider, narrower: the width choice of each of its faces that has one
//                             (Compact, Standard, Wide; its menu's row, OptionRows.OfFace)
//
// Nothing else has a size to step: a bar widget no height, a folder's line no width (its list has one, its own
// menu's). The caller saves what changed as every change of edit mode's is (Dock.Apply), so it is one step of the
// undo, as the menu's change is.
//
// Plain code without windows, so the tests can reach it.

using Karpo.Core;
using TerraFX.Interop.Windows;

namespace Karpo.Dock;

/// <summary>The widget the keys are on a size larger or smaller by Shift+arrows; see the top of the file.</summary>
public static class EditResize
{
    /// <summary>
    /// <paramref name="item"/> a size larger or smaller by Shift and <paramref name="arrow"/>, its folder's room
    /// <paramref name="room"/> cells at the most; false where it has no such size or is at its end.
    /// </summary>
    public static bool Step(WidgetItem item, int arrow, int room = LineHeight.Most) => Way(arrow) switch
    {
        (var down, 0) when down != 0 => LineHeightRows.SizeOf(item) is not null
            ? NextSize(item, down) is { } size && LineHeightRows.Resize(item, size)
            : LineHeightRows.For(item, room) is { } choice
                && LineHeightRows.Act(choice, down < 0 ? LineHeightRows.FewerId : LineHeightRows.MoreId) is { } height
                && LineHeightRows.Resize(item, height),
        (0, var wider) when wider != 0 => Widen(item, wider),
        _ => false,
    };

    /// <summary>Each width choice of <paramref name="item"/>'s faces <paramref name="step"/> on, as its row sets it; false where none moved.</summary>
    private static bool Widen(WidgetItem item, int step)
    {
        var changed = false;
        foreach (var row in Widths(item).ToList())
        {
            if (row.Set is { } set && row.Chosen + step is var at && at >= 0 && at < row.Choices.Count)
            {
                changed |= set(at);
            }
        }
        return changed;
    }

    /// <summary>
    /// What <paramref name="item"/>'s size is now, as its menu says it, for Narrator after a step by
    /// <paramref name="arrow"/>: "5 rows", "Compact", "Wide"; null where it has none that way.
    /// </summary>
    public static string? Said(WidgetItem item, int arrow) => Way(arrow) switch
    {
        (var down, 0) when down != 0 => LineHeightRows.SizeOf(item) is { } size ? LineHeightRows.Said(size)
            : LineHeightRows.For(item) is { } choice ? LineHeightRows.Count(choice.Now.Cells)
            : null,
        (0, var wider) when wider != 0 => Widths(item).FirstOrDefault() is { } row && row.Chosen >= 0 && row.Chosen < row.Choices.Count ? row.Choices[row.Chosen] : null,
        _ => null,
    };

    /// <summary>The way an arrow sizes: (taller +1 or shorter −1, wider +1 or narrower −1).</summary>
    private static (int Down, int Wider) Way(int arrow) => arrow switch
    {
        VK.VK_UP => (-1, 0),
        VK.VK_DOWN => (1, 0),
        VK.VK_LEFT => (0, -1),
        VK.VK_RIGHT => (0, 1),
        _ => (0, 0),
    };

    /// <summary>§432: the size of <paramref name="item"/>'s kind beside the one it has, <paramref name="step"/> on; null past the ends.</summary>
    private static LineSize? NextSize(WidgetItem item, int step)
    {
        if (LineHeightRows.SizeOf(item) is not { } now || ListWidgets.Find(item.Info?.Id) is not { } line)
        {
            return null;
        }
        var offered = line.Kind.Offered();
        var at = offered.ToList().IndexOf(now) + step;
        return at >= 0 && at < offered.Count && offered[at] != now ? offered[at] : null;
    }

    /// <summary>The width choices of <paramref name="item"/>'s faces, their menu's rows (choices whose glyphs are the widths'); none for a line.</summary>
    private static IEnumerable<OptionRow> Widths(WidgetItem item) =>
        item is BarItem { Folder: { } folder }
            ? Enumerable.Range(0, folder.Faces.Count)
                .SelectMany(face => OptionRows.OfFace(folder, face))
                .Where(row => row.Kind == OptionRowKind.Choice && ReferenceEquals(row.ChoiceGlyphs, OptionGlyphs.Widths))
            : [];
}
