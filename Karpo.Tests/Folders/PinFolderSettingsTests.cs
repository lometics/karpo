// folder contents (§380; the person, 2026-10-28: "lets split folder and pin again. so: a pin always only
// shows the link to a webpage, application, folder, etc. and opens the location on click. a folder always
// shows the contents of the folder and inherits the navigation properties etc. from the pin widget"): a pin
// opens what it points at and nothing more, a folder's contents list it and navigate — as the model has
// them, as the file writes them, wherever either stands; and the way into a subfolder and back that the
// navigation in place walks (2026-10-29) — rules on plain values (Karpo.Core\PinFolders.cs,
// Karpo.Dock\Drives\FolderTrail.cs), so tested without a window. §32 had been one widget for both: a pin to
// a folder set to show its contents.

using System.Text.Json;
using Karpo.Dock;
using Karpo.Dock.Drives;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Folders;

public sealed class PinFolderSettingsTests
{
    private const string Documents = @"C:\Users\Ann\Documents";

    private static bool Here(string path) => string.Equals(path.TrimEnd('\\'), Documents, StringComparison.OrdinalIgnoreCase)
        || string.Equals(path, @"D:\", StringComparison.OrdinalIgnoreCase);

    // --- The model and its defaults --------------------------------------------------------------

    /// <summary>
    /// A pin never lists what a folder holds and never navigates: whatever its file said, its folder
    /// opens in File Explorer, on the taskbar and in a list, and nothing of a folder's is kept on it.
    /// </summary>
    [Fact]
    public void APinNeverListsWhatAFolderHolds()
    {
        foreach (var pin in new WidgetItem[] { BarKind.Pin.New(), ListKind.Pin.New() })
        {
            pin.Pin!.Target = Documents;
            pin.Pin.Navigation = PinFolderNavigation.Contents;
            pin.Normalize();
            Assert.False(PinFolders.Lists(pin));
            Assert.Equal(PinFolderNavigation.Explorer, PinFolders.NavigationOf(pin));
            Assert.Equal(PinFolderNavigation.Explorer, pin.Pin.Navigation);
        }
    }

    /// <summary>
    /// A folder's contents are a widget of their own, a line of a list (§492: no longer the taskbar's):
    /// they list what their folder holds and keep the navigation the pin had; a new one opens a
    /// subfolder in File Explorer.
    /// </summary>
    [Fact]
    public void AFolderListsWhatItHoldsAndNavigates()
    {
        var folder = ListKind.FolderContents.New();
        Assert.Same(WidgetInfos.FolderContents, folder.Info);
        Assert.True(PinFolders.Lists(folder));
        Assert.Equal(PinFolderNavigation.Explorer, PinFolders.NavigationOf(folder));
        folder.Pin!.Target = Documents;
        folder.Pin.Navigation = PinFolderNavigation.Contents;
        folder.Normalize();
        Assert.Equal(PinFolderNavigation.Contents, PinFolders.NavigationOf(folder));
        Assert.False(PinFolders.Lists(null));
        Assert.Equal(PinFolderNavigation.Explorer, PinFolders.NavigationOf(null));
        // Two widgets to add, each where the other was one.
        Assert.NotEqual(WidgetInfos.Pin.Id, WidgetInfos.FolderContents.Id);
    }

    /// <summary>
    /// What the settings offer, in their order and words: two navigations (2026-10-29): File Explorer, or the
    /// contents in place, as deep as the folders go — one level and all levels were one and the same once a
    /// subfolder opens where its parent was.
    /// </summary>
    [Fact]
    public void TheNavigationsAreTwo()
    {
        Assert.Equal([PinFolderNavigation.Explorer, PinFolderNavigation.Contents], PinFolders.Navigations);
        Assert.Equal("Open in File Explorer", PinFolderNavigation.Explorer.Title());
        Assert.Equal("Show its contents", PinFolderNavigation.Contents.Title());
        Assert.False(PinFolderNavigation.Explorer.ShowsContents());
        Assert.True(PinFolderNavigation.Contents.ShowsContents());
    }

    /// <summary>
    /// The folder a target names, as the file system is asked about it: quotes taken off, a variable
    /// expanded, a drive's letter its root; nothing for an address or a path that is not there.
    /// </summary>
    [Fact]
    public void TheFolderIsThePathTheTargetNames()
    {
        Assert.Equal(Documents, PinFolders.FolderOf($"\"{Documents}\"", Here));
        Assert.Equal(Documents + "\\", PinFolders.FolderOf(Documents + "\\", Here));
        Assert.Equal(@"D:\", PinFolders.FolderOf("D:", Here));
        var asked = "";
        _ = PinFolders.FolderOf(@"%USERPROFILE%\Documents", path =>
        {
            asked = path;
            return false;
        });
        Assert.Equal(Environment.ExpandEnvironmentVariables(@"%USERPROFILE%\Documents"), asked);
        Assert.Equal("", PinFolders.FolderOf("http://example.com", _ => true));
        Assert.Equal("", PinFolders.FolderOf(null, _ => true));
    }

    // --- As the file writes them -----------------------------------------------------------------

    /// <summary>
    /// The file: a folder's contents are their own kind, pointing at their folder as a pin points at
    /// its target, and say only what was chosen; a pin says nothing of a folder's; a word this build
    /// does not know is read as File Explorer.
    /// </summary>
    [Fact]
    public void TheFileWritesOnlyWhatWasChosen()
    {
        var folder = ListKind.FolderContents.New();
        folder.Pin!.Target = Documents;
        var plain = JsonSerializer.Serialize(folder, ConfigJson.Default.CellItem);
        Assert.Contains("\"kind\": \"folderContents\"", plain);
        Assert.DoesNotContain("navigation", plain);
        Assert.DoesNotContain("\"show\"", plain);

        folder.Pin.Navigation = PinFolderNavigation.Contents;
        var written = JsonSerializer.Serialize(folder, ConfigJson.Default.CellItem);
        Assert.Contains("\"navigation\": \"contents\"", written);
        var read = JsonSerializer.Deserialize(written, ConfigJson.Default.CellItem)!;
        read.Normalize();
        Assert.Equal(("folderContents", PinFolderNavigation.Contents), (read.Kind, read.Pin!.Navigation));

        var newer = JsonSerializer.Deserialize(written.Replace("\"contents\"", "\"tree\""), ConfigJson.Default.CellItem)!;
        newer.Normalize();
        Assert.Equal(PinFolderNavigation.Explorer, newer.Pin!.Navigation);

        // A pin given a folder's words drops them.
        var pin = JsonSerializer.Deserialize(written.Replace("\"folderContents\"", "\"pin\""), ConfigJson.Default.CellItem)!;
        pin.Normalize();
        Assert.DoesNotContain("navigation", JsonSerializer.Serialize(pin, ConfigJson.Default.CellItem));
    }

    /// <summary>
    /// §498: a folder's views are File Explorer's, in its View menu's order and words; the file says one only where it
    /// is not the list every line was before ("view": "details"), reads it back, takes a word this build does not know
    /// as the list, and a pin given one drops it. The row offers the five, each its own glyph, and writes the view.
    /// </summary>
    [Fact]
    public void TheViewsAreExplorersAndTheFileSaysOneOnlyWhereChosen()
    {
        Assert.Equal([PinFolderView.LargeIcons, PinFolderView.MediumIcons, PinFolderView.List, PinFolderView.Details, PinFolderView.Tiles], PinFolders.Views);
        Assert.Equal(["Large icons", "Medium icons", "List", "Details", "Tiles"], PinFolders.Views.Select(view => view.Title()));
        Assert.Equal(PinFolders.Views.Length, OptionGlyphs.FolderViews.Distinct().Count());

        var folder = ListKind.FolderContents.New();
        folder.Pin!.Target = Documents;
        Assert.Equal(PinFolderView.List, PinFolders.ViewOf(folder));
        Assert.DoesNotContain("\"view\"", JsonSerializer.Serialize(folder, ConfigJson.Default.CellItem));

        var row = OptionRows.Of(folder).Single(each => each.Key == OptionRows.PinView);
        Assert.Equal(PinFolders.Views.Select(view => view.Title()), row.Choices);
        Assert.Equal(Array.IndexOf(PinFolders.Views, PinFolderView.List), row.Chosen);
        Assert.Equal(OptionGlyphs.FolderViews, row.ChoiceGlyphs);
        Assert.True(row.Set!(Array.IndexOf(PinFolders.Views, PinFolderView.Details)));
        var written = JsonSerializer.Serialize(folder, ConfigJson.Default.CellItem);
        Assert.Contains("\"view\": \"details\"", written);
        var read = JsonSerializer.Deserialize(written, ConfigJson.Default.CellItem)!;
        read.Normalize();
        Assert.Equal(PinFolderView.Details, PinFolders.ViewOf(read));

        var newer = JsonSerializer.Deserialize(written.Replace("\"details\"", "\"filmstrip\""), ConfigJson.Default.CellItem)!;
        newer.Normalize();
        Assert.Equal(PinFolderView.List, PinFolders.ViewOf(newer));
        Assert.DoesNotContain("\"view\"", JsonSerializer.Serialize(newer, ConfigJson.Default.CellItem));

        var pin = JsonSerializer.Deserialize(written.Replace("\"folderContents\"", "\"pin\""), ConfigJson.Default.CellItem)!;
        pin.Normalize();
        Assert.Equal(PinFolderView.List, PinFolders.ViewOf(pin));
        Assert.DoesNotContain("\"view\"", JsonSerializer.Serialize(pin, ConfigJson.Default.CellItem));
    }

    // --- In place and back ----------------------------------------------------------------------

    /// <summary>
    /// 2026-10-29, the person: "the folder widget should also change the contents inline on navigation and not actually
    /// open the submenus on the left or the right". A subfolder gone into is shown in its parent's place, as deep as the
    /// folders go; Back shows the parent as it was left — its scroll and what it showed — and names the subfolder left,
    /// whose row the keys go back to; on the folder it started from there is no way back. Starting again forgets the way.
    /// </summary>
    [Fact]
    public void TheTrailGoesInAndBackAsItWasLeft()
    {
        var trail = new FolderTrail();
        trail.Start(@"C:\P");
        Assert.Equal((@"C:\P", 0), (trail.Current, trail.Depth));
        Assert.Null(trail.Back());

        var projects = new FolderContents { State = FolderListState.Listed, Path = @"C:\P" };
        trail.Into(@"C:\P\Karpo", 120, projects);
        trail.Into(@"C:\P\Karpo\docs", 36);
        Assert.Equal((@"C:\P\Karpo\docs", 2), (trail.Current, trail.Depth));
        Assert.Equal(@"C:\P", trail.Root);

        Assert.Equal(("docs", 36f, (FolderContents?)null), trail.Back());
        Assert.Equal(@"C:\P\Karpo", trail.Current);
        Assert.Equal(("Karpo", 120f, projects), trail.Back());
        Assert.Equal((@"C:\P", 0), (trail.Current, trail.Depth));
        Assert.Null(trail.Back());

        // As deep as the folders go.
        var path = @"C:\P";
        for (var depth = 1; depth <= 40; depth++)
        {
            path += $"\\{depth}";
            trail.Into(path);
        }
        Assert.Equal((path, 40), (trail.Current, trail.Depth));

        // Home from however deep (its folder closed, 2026-10-30): the root, as it was left; a copy taken back as it stood.
        var kept = trail.Copy();
        trail.Into(path + @"\more");
        Assert.Null(trail.Home());
        Assert.Equal((@"C:\P", 0), (trail.Current, trail.Depth));
        Assert.Null(trail.Home());
        trail.Into(@"C:\P\Karpo", 0, projects);
        Assert.Same(projects, trail.Home());
        trail.Take(kept);
        Assert.Equal((path, 40), (trail.Current, trail.Depth));
        trail.Start(@"D:\");
        Assert.Equal((@"D:\", 0), (trail.Current, trail.Depth));
    }

    /// <summary>
    /// §468, the person: "it should not change its height dependent on level / contents except for the root level of
    /// course". Every level is sized by the root's items as the root was left: on the root by what it shows, however
    /// deep under it by the root's, back out by what the root shows again.
    /// </summary>
    [Fact]
    public void EveryLevelIsSizedByTheRoot()
    {
        var trail = new FolderTrail();
        trail.Start(@"C:\P");
        var root = new FolderContents { State = FolderListState.Listed, Path = @"C:\P" };
        var karpo = new FolderContents { State = FolderListState.Listed, Path = @"C:\P\Karpo" };
        var docs = new FolderContents { State = FolderListState.Listed, Path = @"C:\P\Karpo\docs" };
        Assert.Same(root, trail.Sizing(root));
        trail.Into(@"C:\P\Karpo", 0, root);
        Assert.Same(root, trail.Sizing(karpo));
        trail.Into(@"C:\P\Karpo\docs", 0, karpo);
        Assert.Same(root, trail.Sizing(docs));
        _ = trail.Back();
        Assert.Same(root, trail.Sizing(karpo));
        _ = trail.Back();
        var changed = root with { Unlisted = 3 };
        Assert.Same(changed, trail.Sizing(changed));
    }

    // --- Beside its row ------------------------------------------------------------------------

    private static readonly RECT Screen = new() { left = 0, top = 0, right = 1920, bottom = 1040 };

    private static RECT Item(int left, int right) => new() { left = left, top = 400, right = right, bottom = 436 };

    /// <summary>
    /// A list opens to the right of its row where it fits and to the left where it does not, as a
    /// submenu does; once the cascade has gone left the next list goes left too while it fits there,
    /// rather than back over the lists it opened; where neither side fits, the side asked for, kept on
    /// the screen.
    /// </summary>
    [Fact]
    public void AListStandsBesideItsRowAndTurnsLeftAtTheEdge()
    {
        const int width = 320, keep = 12, overlap = 4;

        // Room on the right: the right, overlapping the row's padding.
        Assert.Equal((996, false), FlyoutHost.BesideX(Item(600, 1000), width, Screen, keep, overlap, leftward: false));
        // None on the right: the left.
        Assert.Equal((1284, true), FlyoutHost.BesideX(Item(1600, 1900), width, Screen, keep, overlap, leftward: false));
        // Gone left before: left again while it fits, though the right has room.
        Assert.Equal((284, true), FlyoutHost.BesideX(Item(600, 1000), width, Screen, keep, overlap, leftward: true));
        // Gone left, and no room on the left any more: the right.
        Assert.Equal((396, false), FlyoutHost.BesideX(Item(100, 400), width, Screen, keep, overlap, leftward: true));
        // Room on neither side: the side asked for, inside the screen.
        var narrow = new RECT { left = 0, top = 0, right = 700, bottom = 1040 };
        var (x, left) = FlyoutHost.BesideX(Item(100, 600), width, narrow, keep, overlap, leftward: false);
        Assert.True(left);
        Assert.InRange(x, narrow.left + keep, narrow.right - keep - width);
    }
}
