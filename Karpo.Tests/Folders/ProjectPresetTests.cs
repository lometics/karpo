// Project is a preset (docs\widgets.md §126): what a click on the project entry gave — Win+P's four
// modes, the one in force marked — is a folder holding the projection line the cast preset has too
// (CastNearbyPresetTests holds the line: it marks the mode and puts another in force) and the premade
// project actions under it, under the folder's icon, the second screen. Plain code: no window.

using Karpo.Dock.QuickSettings;

namespace Karpo.Tests.Folders;

public sealed class ProjectPresetTests
{
    private static BarItem Holder() => Presets.Folder(Presets.Find(Presets.ProjectId)!, new PresetMachine(HasBattery: false, FixedDrives: []));

    /// <summary>
    /// The projection line, then in the same card Display — the flyout's "More display settings" —
    /// under the folder's icon, the entry's glyph: Windows tells no app the mode in force until asked,
    /// so the entry had no state to wear, and the icon face says as much as it did.
    /// </summary>
    [Fact]
    public void ThePresetIsTheModesAndTheirFooterUnderTheIcon()
    {
        var preset = Presets.Find(Presets.ProjectId)!;
        Assert.Null(preset.Face);
        Assert.Equal(["projection", "actions"], preset.Items.Select(item => item.Form));
        Assert.Equal((null, "display"), PremadeWidgets.ProjectActions.Actions);
        var folder = Holder().Folder!;
        Assert.Equal(FolderFaces.IconId, folder.Face.Kind);
        Assert.Equal(WidgetInfos.Projection.Glyph, folder.Face.GlyphOrDefault);
        Assert.Equal([ListKind.Projection, ListKind.Actions], folder.Cells.Select(cell => cell!.Island!.List));
    }

    /// <summary>
    /// The entry is gone from the bar; the quick settings tile is back (5c4e84df) with its switch, and
    /// the page is the projection line's.
    /// </summary>
    [Fact]
    public void TheEntryIsGoneAndThePageIsALine()
    {
        Assert.Null(BarEntries.Find("project"));
        // A quick settings tile again since 5c4e84df: the line whose whole is Win+P's page.
        Assert.NotNull(WidgetInfos.Find("project"));
        Assert.Contains("Project", Enum.GetNames<QuickSwitch>());
        Assert.DoesNotContain("Project", Enum.GetNames<QuickKind>());
        Assert.Equal(WidgetSource.QuickPage, ListWidgets.Find("projection")!.Drawing);
    }
}
