// Tooltips kept (docs\widgets.md §359): a popup's tooltip — the edit bar's, a rail's, a menu's icon
// buttons', a folder's line's — is one TipView an owner keeps, not a host shown afresh per tip; and a
// bar's is drawn in a window of its own (BarTip), as the bar's window shows nothing past its region.
// Filmed as the dock's handler runs them (FrameFilm), animations off: shown, moved to another button,
// hidden — every frame the before or the end; and moved to words it said before, nothing drawn.

using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using Edge = Karpo.Core.Edge;

namespace Karpo.Tests.Present;

public sealed class TipFilmTests
{
    private static readonly Look Still = AddListPictures.LookFor(dark: true) with { Animations = false };

    private static readonly RECT Display = new() { right = 1280, bottom = 720 };

    /// <summary>Round the two buttons' tips, so a tip is a share of the film it can be told by.</summary>
    private static readonly RECT Area = new() { left = 300, top = 590, right = 860, bottom = 680 };

    [UiFact]
    public void ATooltipShownMovedAndHiddenIsTheBeforeOrTheEndInEveryFrame()
    {
        FlickerFilmTests.OnHiddenDesktop("tip", (gfx, instance) =>
        {
            using var tip = new TipView(gfx, instance, 0);
            var first = new POINT { x = 400, y = 660 };
            var second = new POINT { x = 760, y = 660 };
            var show = FrameFilm.Shoot("tip-show", gfx, Area, () => [tip.Hwnd], () => tip.Show(gfx, Still, 1f, "Undo (Ctrl+Z)", first, Display, Edge.Bottom));
            show.Holds(everyFrame: true);
            var (drawn, showings) = (tip.Drawn, tip.Showings);
            var move = FrameFilm.Shoot("tip-move", gfx, Area, () => [tip.Hwnd], () => tip.Show(gfx, Still, 1f, "Redo (Ctrl+Y)", second, Display, Edge.Bottom));
            move.Holds(everyFrame: true);
            // Moved: its words drawn, nothing else made — the window, the chrome, the showing kept.
            Assert.Equal((drawn + 1, showings), (tip.Drawn, tip.Showings));
            // Back to words it said before: nothing drawn at all.
            tip.Show(gfx, Still, 1f, "Undo (Ctrl+Z)", first, Display, Edge.Bottom);
            Assert.Equal((drawn + 1, showings), (tip.Drawn, tip.Showings));
            Assert.Equal("Undo (Ctrl+Z)", tip.Text);
            var hide = FrameFilm.Shoot("tip-hide", gfx, Area, () => [tip.Hwnd], tip.Hide);
            hide.Holds(everyFrame: true);
            Assert.Null(tip.Text);
            // Over everything a popup shows: the tooltips' stratum.
            Assert.Equal(Stratum.Tip, Surfaces.StratumOf(tip.Hwnd));
        });
    }

    /// <summary>
    /// The popups' tooltips are kept, each owner's one: none of them makes a host per tip any more (a source
    /// rule: no tooltip's FlyoutHost made where a tip is shown).
    /// </summary>
    [Fact]
    public void ThePopupsTooltipsAreKept()
    {
        var repo = RepoSource.Root();
        if (repo.Length == 0)
        {
            return;
        }
        foreach (var file in new[] { "Flyout.Primary.cs", "FolderFlyout.Tips.cs", "EditRail.cs" })
        {
            var text = File.ReadAllText(Path.Combine(repo, "Karpo.Dock", file));
            Assert.Contains("TipView", text);
            Assert.DoesNotContain("tip.Keep(", text);
            Assert.DoesNotContain("noteTip ??= new FlyoutHost", text);
            Assert.DoesNotContain("tip ??= new FlyoutHost", text);
        }
    }
}
