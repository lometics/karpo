// §480 (the person, 2026-11-02: "sometimes when i open a folder it does not open properly and might have a transparent
// background or is stuck on a part of the animation"): a folder that went by its animation and is shown again at once — a
// switch to it skips the animation — stood with only its outlines, its lines left at the face where the close had taken
// them (§458's Fly in, gone with §489). The one motion moves the panel alone, and a showing puts it where it ends: held
// here for it, landed and cut short.

using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using Edge = Karpo.Core.Edge;

namespace Karpo.Tests.Present;

public sealed unsafe partial class FolderMotionFilmTests
{
    /// <summary>Opened by its animation, gone by it (landed first, or cut short), then shown at once: exactly the folder shown still.</summary>
    [UiTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void AFolderShownAtOnceAfterItsAnimationStandsWhole(bool landed)
    {
        FlickerFilmTests.OnHiddenDesktop("folder-again", (gfx, instance) =>
        {
            var island = FlickerFilmTests.Readings("again");
            using var folder = new FolderIsland($"{Owner}/{island.Id}", island, gfx, new Config());
            using var flyout = Folder(gfx, instance);
            void Open(PopupMotion? how) => flyout.Show(gfx, Moving, 1f, Anchor, Display, Edge.Bottom, folder, motion: how);
            Open(null);
            FrameFilm.Settle();
            var panel = flyout.PanelOnScreen;
            var area = new RECT { left = Math.Max(0, panel.left - 24), top = Math.Max(0, panel.top - 24), right = Math.Min(Display.right, panel.right + 24), bottom = Display.bottom };
            var still = FrameFilm.Snap(area, [flyout.Hwnd], 0, "still");
            Shut(flyout);
            Open(Slide);
            if (landed)
            {
                // The opening's timer puts it where it ends whether or not the compositor ran it.
                Thread.Sleep((int)Slide.Enter.TotalMilliseconds + 400);
                FrameFilm.Settle();
            }
            else
            {
                Thread.Sleep(40);
                FrameFilm.Pump();
            }
            // Gone by its animation, back the way it came.
            Shut(flyout);
            Open(null);
            FrameFilm.Settle();
            var off = Differs(area, flyout, still);
            Assert.True(off < 0.004, $"{(landed ? "landed" : "cut short")}: shown again at once {off:P2} off the folder shown still");
        });
    }
}
