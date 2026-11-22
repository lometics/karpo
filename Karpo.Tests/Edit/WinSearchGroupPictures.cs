// §652: Windows search's ready-made group open on a Taskbar along the bottom and the left edge of a display at 150 %, dark and
// light: its face where Windows' Search stands, its line at its one height (fixed, twelve rows) listing what "team" finds in
// the samples' sources — the best match, then the groups of several sources (apps, documents, folders, photos, mail, the
// calendar, notes, Karpo's own) —, each row with Windows' own icon (a made-up file's its kind's); §655 the group Extra wide,
// the best match's pane beside the list, after its home (the samples') is checked. To KARPO_PNG_DIR as
//
//   winsearch2-group-<bottom|left>-<dark|light>-150.png
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Dock.WinSearch;
using Karpo.Tests.Islands;
using Karpo.Tests.Searching;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    [UiFact]
    public void WinSearchGroupSheets()
    {
        WindowShots.OnHiddenDesktop("winsearch-group-sheets", (gfx, instance) =>
        {
            const uint dpi = 144;
            WinSearchLine.IconStand = WinSearchFakes.PictureIcons;
            WinSearchLine.HistoryStand = new WinSearchHistory();
            try
            {
                foreach (var dark in new[] { true, false })
                {
                    var look = AddListPictures.LookFor(dark) with { Animations = false };
                    foreach (var edge in new[] { Edge.Bottom, Edge.Left })
                    {
                        // The samples' sources answer at once (§635): the line shows what it finds as the folder opens.
                        using var samples = Samples.Show();
                        EditedDisplay(gfx, instance, dpi, new Config(), look, SearchGroup(edge),
                            $"winsearch2-group-{edge.ToString().ToLowerInvariant()}-{(dark ? "dark" : "light")}-150.png",
                            editing: false, folder: true, folderFace: FolderFaces.WinSearchId,
                            inFolder: (open, _) =>
                            {
                                var line = Assert.IsType<WinSearchLine>(Assert.Single(open.Lines).Island);
                                // §655: it opens on its home, the samples'; "team" typed, what the samples find.
                                Assert.Contains(line.Home.For(null), section => section.Info.Id == WinSearchHomeInfos.Apps);
                                line.Box.Text = WinSearchSamples.Typed;
                                Assert.Equal("Microsoft Teams", line.Results.Best?.Name);
                                Assert.True(line.Results.Groups.Count >= 5);
                            });
                    }
                }
            }
            finally
            {
                WinSearchLine.IconStand = null;
                WinSearchLine.HistoryStand = null;
            }
        });
    }

    /// <summary>A Taskbar along <paramref name="edge"/> with Windows search's group where Windows' Search stood.</summary>
    private static Picture SearchGroup(Edge edge)
    {
        var layout = PictureLayout(LayoutType.Taskbar, edge);
        var group = Presets.Folder(Presets.Find(Presets.WinSearchId)!, new PresetMachine(HasBattery: false, FixedDrives: []));
        var at = layout.Islands.FindIndex(island => island.Kind == BarKind.Search);
        group.Section = at >= 0 ? layout.Islands[at].Section : BarSection.Middle;
        if (at >= 0)
        {
            layout.Islands[at] = group;
        }
        else
        {
            layout.Islands.Insert(0, group);
        }
        Sections.Order(layout.Islands);
        return layout;
    }
}
