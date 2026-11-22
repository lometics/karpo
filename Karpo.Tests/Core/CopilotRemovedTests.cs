// Copilot, taken out (2026-10-09): "remove the copilot widget."
//
// What is held here: no registry, no library page and no shared record has the widget any more, and
// a file that still holds one reads without it — dropped, as an entry from a newer Karpo is, with
// its neighbours where they were — so the bar never shows a broken widget in its place.

namespace Karpo.Tests.Core;

public sealed class CopilotRemovedTests
{
    /// <summary>No widget of any view, no record, no page of the library and no language has it.</summary>
    [Fact]
    public void NoRegistryOrLibraryHasIt()
    {
        Assert.Null(BarEntries.Find("copilot"));
        Assert.Null(ListWidgets.Find("copilot"));
        Assert.DoesNotContain(WidgetInfos.All, info => info.Id == "copilot");
        Assert.DoesNotContain(Enum.GetNames<BarKind>(), name => name == "Copilot");
        foreach (var page in Catalogue.Pages)
        {
            Assert.DoesNotContain(Catalogue.On(page), entry => entry.Id == "copilot");
        }
        Assert.DoesNotContain(new[] { "en", "de", "es", "fr", "zh-Hans" }, language =>
            Strings.Lookup("core.widget.copilot.title", language) is not null);
    }

    /// <summary>
    /// A bar a build with Copilot wrote reads without it: the entries either side stay in order, the
    /// file says it changed so it is written back once, and today's words read the same after.
    /// </summary>
    [Fact]
    public void ABarsCopilotIsDroppedAsTheFileIsRead()
    {
        var config = Configs.Repaired("""
            { "layouts": [ { "id": "default", "name": "Default", "islands": [
              { "id": "start", "kind": "start" },
              { "id": "taskView", "kind": "taskView" },
              { "id": "copilot", "kind": "copilot" },
              { "id": "timer", "kind": "touchKeyboard" } ] } ] }
            """, out var changed);

        Assert.True(changed, "a file with Copilot in it would not be written back");
        Assert.Equal([BarKind.Start, BarKind.TaskView, BarKind.TouchKeyboard], Configs.Kinds(Configs.Islands(config)));
        Assert.DoesNotContain("copilot", config.ToJson(), StringComparison.OrdinalIgnoreCase);
        var again = Configs.RoundTrip(config, out var twice);
        Assert.False(twice, "today's words would change again");
        Assert.Equal([BarKind.Start, BarKind.TaskView, BarKind.TouchKeyboard], Configs.Kinds(Configs.Islands(again)));
    }
}
