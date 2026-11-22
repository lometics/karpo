// The panels converted into list folders, as the tests picture and measure them
// (docs\widgets.md §9). They were the catalogue's presets until presets went; the arrangement each
// panel was drawn in is still what these tests want, so it is written here once rather than in
// every test that needs one.

namespace Karpo.Tests.Folders;

internal static class PanelFolders
{
    /// <summary>The notifications: the one list, with "Clear all" on the band above it.</summary>
    public static BarItem Notifications() => List("Notifications", ListKind.NotificationsList);

    /// <summary>
    /// The weather: the conditions now. Today's curve and the week are no lines of a list since
    /// the person took them out as widgets (2026-09-26); the panel on the bar keeps both.
    /// </summary>
    public static BarItem Weather() => List("Weather", ListKind.WeatherNow);

    /// <summary>A new list folder of <paramref name="kinds"/>, one line each, top to bottom, each with ids of its own.</summary>
    private static BarItem List(string name, params ListKind[] kinds)
    {
        var island = BarKind.FolderList.New();
        var folder = island.Folder!;
        for (var row = 0; row < kinds.Length; row++)
        {
            folder.Put(kinds[row].New());
        }
        island.Normalize();
        return island;
    }
}
