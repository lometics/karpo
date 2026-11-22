// folders: a widget carried out of a folder and let go on the bar.
//
// A folder's line never goes onto the taskbar (Partners, 2026-10-09): nothing
// moves between the taskbar and a folder. The line has nothing to draw there
// either — CellIsland.Length throws for exactly that case — so a drop that
// does not refuse is a crash rather than a cosmetic bug.
//
// The catalogue, CanAdd and Folders.Add refuse as well. The drag out of an open
// folder is what these hold down.

using System.Text.RegularExpressions;
using Karpo.Core;

namespace Karpo.Tests.Folders;

public sealed class FolderOutOfBarTests
{
    /// <summary>
    /// Every widget a person can place, of every registry, says whether a bar can draw it, and
    /// where a bar can't, the sentence names it.
    /// </summary>
    [Fact]
    public void TheRuleIsTheFlagAndTheSentenceNamesTheKind()
    {
        WidgetItem[] widgets =
        [
            .. BarEntries.All.Select(entry => entry.Kind.New()),
            .. ListWidgets.All.Select(widget => widget.Kind.New()),
        ];
        foreach (var widget in widgets)
        {
            // "Can it go on a bar?" is one question with one answer, and the sentence is empty
            // exactly when the answer is yes.
            var onBar = Partners.CanBe(widget, WidgetType.Bar);
            var why = Partners.WhyNot(widget, WidgetType.Bar);
            Assert.Equal(onBar, why.Length == 0);
            if (!onBar)
            {
                // A refusal has to say which widget it is about: the person is holding it.
                Assert.Contains(widget.Info!.Title, why, StringComparison.Ordinal);
            }
        }
    }

    /// <summary>
    /// Regression: the drop path for a widget carried out of a folder refuses it on the bar, with
    /// the folder saying why (Partners.WhyNot), while it is carried there and when it is let go. It
    /// is in a window procedure that these tests can't drive, so what is checked is that the refusal
    /// is said at all — a drop path that forgets it is the bug, and it is invisible until something
    /// crashes on a real bar. The rule itself is PartnerMoveTests'.
    /// </summary>
    [Fact]
    public void TheDragOutOfAFolderIsRefusedOnTheBar()
    {
        if (Source() is not { Length: > 0 } repo)
        {
            return;
        }
        var code = File.ReadAllText(Path.Combine(repo, "Karpo.Dock", "Dock.Pins.cs"));
        var start = code.IndexOf("private void FolderDragged", StringComparison.Ordinal);
        Assert.True(start > 0, "FolderDragged is where a widget leaves a folder");
        var body = code[start..code.IndexOf("\n    /// <summary>", start, StringComparison.Ordinal)];
        // A refusal, not a silent snap-back: the folder says why on its own line, while the widget
        // is carried over the bar and when it is let go there.
        Assert.Matches(new Regex(@"Say\(\s*Partners\.WhyNot\(\s*\w+\.Settings,\s*WidgetType\.Bar\s*\)\s*\)"), body);
        Assert.True(Regex.Matches(body, @"Partners\.WhyNot\(\s*\w+\.Settings,\s*WidgetType\.Bar\s*\)").Count >= 2, "the drop says why as well as the carry");
    }

    /// <summary>The repository, when these tests run from inside it; empty otherwise.</summary>
    private static string Source()
    {
        var at = AppContext.BaseDirectory;
        for (var up = new DirectoryInfo(at); up is not null; up = up.Parent)
        {
            if (File.Exists(Path.Combine(up.FullName, "Karpo.slnx")))
            {
                return up.FullName;
            }
        }
        return "";
    }
}
