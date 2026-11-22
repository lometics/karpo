// edit: edit mode is the dock's, every display's at once, never a window's (the person, 2026-10-23:
// "when changing position of the taskbar it ends the edit mode"; docs\widgets.md §296). Another edge,
// type or way of showing — from the bar's looks menu in edit mode (§290) or the settings app — makes the
// display's bar again in another window; the old one went, and edit mode ended with it (Forget). Now the
// arrange hands edit mode to the bar the build keeps or makes (Dock.EditHolder) before the old windows
// are forgotten, and it ends only with no bar left.

using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Edit;

public sealed class EditHolderTests
{
    private sealed record Bar(string Display, string Name);

    private static Bar? Holder(Bar held, params Bar[] bars) => TheDock.EditHolder(held, bars, bar => bar.Display);

    /// <summary>
    /// The bar holding edit mode keeps it while it is still a bar; made again — another edge, type or
    /// way of showing — its display's new bar holds it; its display gone from Karpo, another display's;
    /// no bar left, none (edit mode ends).
    /// </summary>
    [Fact]
    public void EditModePassesToTheBarMadeAgain()
    {
        var bottom = new Bar("A", "bottom");
        var top = new Bar("A", "top");
        var other = new Bar("B", "bottom");

        Assert.Same(bottom, Holder(bottom, other, bottom));
        Assert.Same(top, Holder(bottom, other, top));
        Assert.Same(other, Holder(bottom, other));
        Assert.Null(Holder(bottom));
    }

    /// <summary>
    /// The arrange hands edit mode on before it forgets the windows that went, so Forget, which ends edit
    /// mode with the window holding it, sees it held by the new bar; a change of the bar's looks is that
    /// arrange (LooksPicked → Apply → Arrange), as the settings app's is (ConfigChanged → Apply).
    /// </summary>
    [Fact]
    public void TheArrangeHandsEditModeOnBeforeTheOldBarGoes()
    {
        var repo = RepoSource.Root();
        var dock = File.ReadAllText(Path.Combine(repo, "Karpo.Dock", "Dock.cs")).ReplaceLineEndings("\n");
        var handed = dock.IndexOf("EditHolder(held, next.FindAll(window => window.HoldsAppbar), window => window.Id) is { } holder", StringComparison.Ordinal);
        // §637: a window that went is forgotten, then goes by the motion while its display is still there, else is
        // disposed — the Forget is the step edit mode must already have been handed on before.
        var forgotten = dock.IndexOf("            Forget(window, next);\n            if (monitors.Exists(", StringComparison.Ordinal);
        Assert.True(handed > 0 && forgotten > handed, "the arrange forgets the old bar before handing edit mode on");
        Assert.Contains("editor = holder;", dock[handed..forgotten]);

        var looks = File.ReadAllText(Path.Combine(repo, "Karpo.Dock", "Dock.BarLooks.cs"));
        Assert.Contains("Apply(next);", looks);
        Assert.DoesNotContain("EndEdit", looks);
    }
}
