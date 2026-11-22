using System.Text.Json;

namespace Karpo.Tests;

/// <summary>
/// A folder's widgets, as the tests build them by hand. A folder holds <see cref="CellItem"/>s and a
/// bar <see cref="BarItem"/>s (S5 step 6a), and the file writes the two the same way, so a test that
/// makes a widget with its kind's <c>New()</c> can put it in a folder as it always did.
/// </summary>
internal static class CellItems
{
    /// <summary>
    /// <paramref name="island"/> as a folder's widget: the same id, kind and options, read back as
    /// the file would give them to a folder, and standing in no view until a folder stands it in
    /// one (<see cref="Folders.Project"/>, <see cref="Folders.Add"/>). A copy, as a folder's widget
    /// read from the file is a record of its own, and normalised as one: a taskbar entry carries no
    /// size (2026-10-08), and a folder's widget is given the one its kind starts at, as the file's
    /// own repair would give it.
    /// </summary>
    public static CellItem AsCell(this BarItem island)
    {
        var cell = JsonSerializer.Deserialize(JsonSerializer.Serialize(island, ConfigJson.Default.BarItem), ConfigJson.Default.CellItem)!;
        cell.Normalize();
        return cell;
    }

    /// <summary>
    /// <paramref name="island"/> put by hand at the end of <paramref name="folder"/>'s list
    /// <paramref name="list"/>, the lists before it made where the folder has none yet — as a file
    /// would have it, without the checks <see cref="Folders.Add"/> makes. The line it is.
    /// </summary>
    public static FolderCell Put(this FolderOptions folder, CellItem island, int list = 0)
    {
        folder.Lists ??= [];
        while (folder.Lists.Count <= list)
        {
            folder.Lists.Add([]);
        }
        var cell = new FolderCell { Island = island };
        folder.Lists[list].Add(cell);
        return cell;
    }

    /// <summary>A folder of one list holding <paramref name="islands"/> in their order, as a file would have it.</summary>
    public static List<List<FolderCell>> OneList(params IEnumerable<CellItem> islands) =>
        [[.. islands.Select(island => new FolderCell { Island = island })]];
}
