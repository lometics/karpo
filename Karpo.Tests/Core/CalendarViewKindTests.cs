namespace Karpo.Tests.Core;

/// <summary>
/// calendar: a day, week or year view placed in a folder is that view after every way the config is
/// read again (docs\widgets.md §205): the file loaded, loaded again, saved and loaded, and the copy
/// every edit of the dock's starts from (Config.Clone), which stood no folder's widget in its view.
/// </summary>
[Collection(ConfigFile.Collection)]
public sealed class CalendarViewKindTests : IDisposable
{
    public CalendarViewKindTests() => ConfigFile.Delete();

    public void Dispose() => ConfigFile.Delete();

    private static string File(string kind) => $$"""
        {
          "version": {{Config.Shape}},
          "layouts": [ { "id": "default", "name": "Default", "islands": [
            { "id": "c", "kind": "folderList", "folder": { "name": "Calendar", "lists": [ [ { "island": { "id": "d", "kind": "{{kind}}" } } ] ] } },
            { "id": "t", "kind": "tray" } ] } ]
        }
        """;

    private static CellItem Line(Config config) => Configs.Islands(config)[0].Folder!.Cells.Single().Island;

    /// <remarks>
    /// The file writes the view as the calendar in that style since §209 (WidgetStyles): a file in the
    /// words from before reads as that view, and is written back as the calendar and its style.
    /// </remarks>
    [Theory]
    [InlineData("dayView", "day", ListKind.DayView)]
    [InlineData("weekView", "week", ListKind.WeekView)]
    [InlineData("yearView", "year", ListKind.YearView)]
    public void AViewStaysItsViewThroughLoadsCopiesAndSaves(string kind, string style, ListKind expected)
    {
        var loaded = ConfigFile.Load(File(kind));
        Assert.Equal(expected, Line(loaded).List);
        Assert.Equal(expected, Line(Config.Load()).List);

        var edited = loaded.Clone();
        Assert.Equal(expected, Line(edited).List);
        edited.Height = 40;
        edited.Save();
        Assert.Contains($"\"style\": \"{style}\"", ConfigFile.Text(), StringComparison.Ordinal);
        Assert.DoesNotContain($"\"{kind}\"", ConfigFile.Text(), StringComparison.Ordinal);
        Assert.Equal(expected, Line(Config.Load()).List);
    }
}
