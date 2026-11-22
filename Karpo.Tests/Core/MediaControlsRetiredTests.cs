// The now-playing widget's "Show playback controls" setting is gone: the card and the list line always
// have their buttons, and nothing offers it. Its other two settings went with the entry (2026-10-10):
// the art and the hiding are the now-playing face's options now (FolderFaces.NowPlaying), set where a
// folder's face is. A widget's settings are its menu's rows (OptionRows, §228).

namespace Karpo.Tests.Core;

public sealed class MediaControlsRetiredTests
{
    [Fact]
    public void NothingOffersAControlsSwitch()
    {
        var rows = Enum.GetValues<ListKind>().Select(kind => (WidgetItem)kind.New())
            .Concat(Enum.GetValues<BarKind>().Select(kind => (WidgetItem)kind.New()))
            .SelectMany(item => OptionRows.Of(item));
        Assert.DoesNotContain(rows, row => row.Key.StartsWith("Media", StringComparison.Ordinal));

        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Karpo.slnx")))
        {
            root = root.Parent;
        }
        Assert.NotNull(root);
        var en = File.ReadAllText(Path.Combine(root!.FullName, "Karpo.Core", "Strings", "en.json"));
        Assert.DoesNotContain("settings.widget.media.", en, StringComparison.Ordinal);
    }
}
