// No widget has an alignment (2026-10-29: "the alignment settings for the widgets (e.g. the clock) should
// be removed"): a line's words stand where the kit's anatomy puts them (§401), and a face is what it shows
// and the padding (§277). Only a folder has one, where it stands along its bar (§385, FolderAlignTests).

namespace Karpo.Tests.Core;

public sealed class AlignmentTests
{
    /// <summary>
    /// No face has an alignment (§277): a face is what it shows and the padding (§275), so its words
    /// have nowhere to stand; the kind of option is gone with it.
    /// </summary>
    [Fact]
    public void NoFaceHasAnAlignment()
    {
        foreach (var face in FolderFaces.All)
        {
            Assert.DoesNotContain(face.Options, option => option.Key == "align");
        }
        Assert.DoesNotContain("Align", Enum.GetNames<FaceOptionKind>());
    }

    /// <summary>
    /// No line has one either: a widget carries no alignment, so a file's "align" on a line is read as
    /// nothing, the rest of the line as it was, and not written again.
    /// </summary>
    [Fact]
    public void NoLineHasAnAlignment()
    {
        Assert.Null(typeof(WidgetItem).GetProperty("Align"));
        var config = Configs.Repaired("""
            { "layouts": [ { "id": "default", "name": "Default", "islands": [
              { "id": "list", "kind": "folderList", "folder": { "name": "List", "lists": [ [
                { "island": { "id": "head", "kind": "heading", "heading": { "text": "Now" }, "align": "center" } },
                { "island": { "id": "time", "kind": "timeHeader", "align": "end" } } ] ] } } ] } ] }
            """);
        var lines = Configs.Islands(config)[0].Folder!.Cells.Select(cell => cell.Island!).ToList();
        Assert.Equal(["head", "time"], lines.Select(line => line.Id));
        Assert.Equal("Now", lines[0].Heading!.Text);
        Assert.DoesNotContain("\"align\"", config.ToJson(), StringComparison.Ordinal);
    }

    /// <summary>What a folder's alignment says is in every language; a line's start and end went with it.</summary>
    [Fact]
    public void OnlyTheFoldersAlignmentHasWords()
    {
        foreach (var language in Strings.All)
        {
            Assert.NotNull(Strings.Lookup("core.align.title", language));
            Assert.NotNull(Strings.Lookup("core.align.center", language));
            Assert.Null(Strings.Lookup("core.align.start", language));
            Assert.Null(Strings.Lookup("core.align.end", language));
        }
    }
}
