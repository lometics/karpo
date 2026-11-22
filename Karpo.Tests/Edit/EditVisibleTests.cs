// edit: what the person reported on 2026-10-07: "the parking is not always showing and the end edit
// mode is not showing too. additionally remove that you go out of edit mode if you click into the
// middle." And round 6 (the same day): the "Parked" tray gone, the edit bar a compact command bar in
// each display's top right corner, the settings app not opened by edit mode, and Escape leaving it
// wherever the keys are. Held here:
//
//  - **Every display keeps its edit bar in front of its dim** (ZPlan, §359). The dim (EditShade) is
//    a topmost window over the whole display; from the second edit mode on, the builds showed the
//    edit bars before the dims came up, so the dims stood over them, and showing them again moved
//    nothing. Now where each stands is its stratum's, whichever came up last. Two 4K displays at
//    150 %, as the person has them, through two edit modes: every edit bar is open, at its display's
//    top (EditDeckPlan.Place), with its crumbs and buttons, and in front of its dim.
//  - **A click never ends edit mode** (EditClicks.OnFrame): a click on the dim clears the selection
//    or keeps it, a drag selects, and the click-away hook is gone.
//  - **Escape leaves edit mode wherever the keys are** (EditFollowHook, FollowGesture): one Escape
//    calls a widget following off, the next leaves; a popup of the dock's own keeps its Escape.
//  - **The pictures**: two 4K displays at 150 % side by side in edit mode, as
//    edit-displays-4k-<dark|light>-150.png.
//
// The windows need Direct2D and a window station: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Tests.Islands;
using Karpo.Tests.Present;
using Karpo.Tests.Signals;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
using Vector2 = System.Numerics.Vector2;

namespace Karpo.Tests.Edit;

public sealed class EditStaysOnTests
{
    /// <summary>
    /// A click on the frame's empty room — the small screen's middle among it — clears the selection,
    /// or keeps it with Ctrl or Shift held, and a drag selects; none of the three ends edit mode, and
    /// there is no fourth.
    /// </summary>
    [Theory]
    [InlineData(false, false, FrameClick.Unselect)]
    [InlineData(false, true, FrameClick.Nothing)]
    [InlineData(true, false, FrameClick.Selected)]
    [InlineData(true, true, FrameClick.Selected)]
    public void AClickOnTheFrameLeavesEditModeOn(bool dragged, bool adding, FrameClick does)
    {
        Assert.Equal(does, EditClicks.OnFrame(dragged, adding));
        Assert.Equal([FrameClick.Nothing, FrameClick.Selected, FrameClick.Unselect], Enum.GetValues<FrameClick>());
    }

    /// <summary>
    /// The dim over every display in edit mode is half black (§297: "even a little darker"; it was
    /// 40 %), in every theme, the desktop still showing through.
    /// </summary>
    [Fact]
    public void TheDimIsHalfBlack()
    {
        Assert.Equal(0x80, EditShade.Alpha);
        Assert.Equal(Windows.UI.Color.FromArgb(0x80, 0, 0, 0), EditShade.Ink);
    }

    /// <summary>
    /// The rule that ended edit mode on a click away is gone, its hook with it: nothing in the dock
    /// listens for a click outside Karpo to end edit mode. The ways out are the edit bar's "Done" and
    /// Escape (§352).
    /// </summary>
    [Fact]
    public void NoClickAwayEndsEditMode()
    {
        var types = typeof(EditClicks).Assembly.GetTypes().Select(type => type.Name).ToList();
        Assert.DoesNotContain("EditDismiss", types);
        Assert.DoesNotContain("EditDismissHook", types);
        Assert.DoesNotContain("AwayGesture", types);
        Assert.Equal(
            [EditDeckAction.None, EditDeckAction.Open, EditDeckAction.Close, EditDeckAction.Done, EditDeckAction.Act, EditDeckAction.Pop, EditDeckAction.Link, EditDeckAction.Type, EditDeckAction.Mix, EditDeckAction.Reorder, EditDeckAction.Hover],
            Enum.GetValues<EditDeckAction>());
    }

    /// <summary>
    /// The "Parked" tray is gone (round 6): no part of the edit bar is it, no type of the dock is a
    /// parked widget's chip, and no string names it.
    /// </summary>
    [Fact]
    public void TheParkedTrayIsGone()
    {
        var types = typeof(EditClicks).Assembly.GetTypes().Select(type => type.Name).ToList();
        Assert.DoesNotContain("EditDeckPart", types);
        Assert.DoesNotContain("DeckChip", types);
        Assert.Null(typeof(BarItem).GetProperty("Parked"));
        foreach (var language in new[] { "en", "de", "es", "fr", "zh-Hans" })
        {
            Assert.Null(Strings.Lookup("dock.edit.parked", language));
            Assert.Null(Strings.Lookup("dock.edit.parkedEmpty", language));
            Assert.Null(Strings.Lookup("settings.widget.where.parked", language));
        }
    }

    /// <summary>
    /// Escape in edit mode with nothing following leaves it, wherever the keys are: taken from the
    /// window that has them, its release too, and its repeats held down mean nothing more.
    /// </summary>
    [Fact]
    public void EscapeLeavesEditMode()
    {
        var gesture = new FollowGesture { Editing = true };

        Assert.Equal((true, FollowInput.Leave), gesture.Key(WM.WM_KEYDOWN, VK.VK_ESCAPE));
        Assert.Equal((true, FollowInput.None), gesture.Key(WM.WM_KEYDOWN, VK.VK_ESCAPE));
        Assert.True(gesture.OwesKey);
        Assert.Equal((true, FollowInput.None), gesture.Key(WM.WM_KEYUP, VK.VK_ESCAPE));
        Assert.False(gesture.Owes);
        // Any other key goes where it went.
        Assert.Equal((false, FollowInput.None), gesture.Key(WM.WM_KEYDOWN, VK.VK_TAB));
        // Out of edit mode Escape is nobody's but the window's with the keys.
        gesture.Editing = false;
        Assert.Equal((false, FollowInput.None), gesture.Key(WM.WM_KEYDOWN, VK.VK_ESCAPE));
        Assert.Equal((false, FollowInput.None), gesture.Key(WM.WM_KEYUP, VK.VK_ESCAPE));
    }

    /// <summary>
    /// While a widget follows (added or pasted), Escape calls it off first; only the next Escape
    /// leaves edit mode.
    /// </summary>
    [Fact]
    public void TheFirstEscapeCallsAFollowingWidgetOffTheNextLeaves()
    {
        var gesture = new FollowGesture { Editing = true, Following = true };

        Assert.Equal((true, FollowInput.Cancel), gesture.Key(WM.WM_KEYDOWN, VK.VK_ESCAPE));
        // The widget is called off (Dock.DropFollow): nothing follows any more.
        gesture.Following = false;
        // Still held down: nothing more.
        Assert.Equal((true, FollowInput.None), gesture.Key(WM.WM_KEYDOWN, VK.VK_ESCAPE));
        Assert.Equal((true, FollowInput.None), gesture.Key(WM.WM_KEYUP, VK.VK_ESCAPE));
        // The next press leaves edit mode.
        Assert.Equal((true, FollowInput.Leave), gesture.Key(WM.WM_KEYDOWN, VK.VK_ESCAPE));
    }

    /// <summary>
    /// A popup of the dock's own with the keys (a menu, the add list, an open folder) closes on Escape
    /// itself: edit mode lets it have it, and stays on. A widget following takes it all the same.
    /// </summary>
    [Fact]
    public void APopupWithTheKeysKeepsItsEscape()
    {
        var gesture = new FollowGesture { Editing = true };

        Assert.Equal((false, FollowInput.None), gesture.Key(WM.WM_KEYDOWN, VK.VK_ESCAPE, () => true));
        Assert.False(gesture.Owes);
        Assert.Equal((false, FollowInput.None), gesture.Key(WM.WM_KEYUP, VK.VK_ESCAPE, () => true));
        Assert.Equal((true, FollowInput.Leave), gesture.Key(WM.WM_KEYDOWN, VK.VK_ESCAPE, () => false));
        Assert.Equal((true, FollowInput.None), gesture.Key(WM.WM_KEYUP, VK.VK_ESCAPE, () => false));
        gesture.Following = true;
        Assert.Equal((true, FollowInput.Cancel), gesture.Key(WM.WM_KEYDOWN, VK.VK_ESCAPE, () => true));
    }

}

public sealed unsafe partial class SceneTreeTests
{
    /// <summary>The person's displays: 4K, at 150 %, so 2560 by 1440 DIPs.</summary>
    private static readonly Vector2 FourK = new(2560f, 1440f);

    /// <summary>
    /// Both displays get their edit bar — "Add a widget", "Grid folder", "Folder", "Separator",
    /// "Open settings", "Close edit mode" — open, over the right end of the display's bar along the
    /// bottom (none drawn here: over the display's bottom) and in front of the display's dim: on the first edit mode,
    /// and on the second, when the edit bars already made are shown by the first builds before the
    /// dims come up over them and are shown again unchanged.
    /// </summary>
    [UiFact]
    public void EveryDisplayKeepsItsEditBarInFrontOfItsDim()
    {
        WindowShots.OnHiddenDesktop("edit-visible", (gfx, instance) =>
        {
            PreviewToolbarTests.Register(Preview.ClassName, instance);
            const uint dpi = 144;
            const float scale = dpi / 96f;
            var look = AddListPictures.LookFor(dark: true) with { Animations = false };
            var size = FourK * scale;
            RectI[] displays =
            [
                new(0, 0, (int)size.X, (int)size.Y),
                new((int)size.X, 0, 2 * (int)size.X, (int)size.Y),
            ];
            var shown = new List<(EditShade Shade, EditDeck Add, RectI Bounds)>();
            try
            {
                foreach (var bounds in displays)
                {
                    shown.Add((new EditShade(gfx, instance, 0), new EditDeck(gfx, instance, 0), bounds));
                }
                void ShowRows()
                {
                    foreach (var (shade, add, bounds) in shown)
                    {
                        // As the chrome shows them (EditChrome): no bar or place at the top in sight.
                        add.Show(gfx, look, scale, new EditDeckRoom(bounds, 0));
                    }
                }
                void ShowDims()
                {
                    foreach (var (shade, _, bounds) in shown)
                    {
                        shade.Show(bounds, dimmed: true, Motion.Still);
                    }
                }

                // The first edit mode: the dims come up, then the rows.
                ShowDims();
                ShowRows();
                AllInFront("the first edit mode");

                // Edit mode ends: everything goes (EditChrome.Update).
                foreach (var (shade, add, _) in shown)
                {
                    add.Hide();
                    shade.Hide(Motion.Still);
                }
                WindowShots.Settle(50);

                // The second: the rows shown while the dims are still out of sight, the dims coming up
                // after them: nothing to lift, each stands where its stratum puts it (§359).
                ShowRows();
                ShowDims();
                AllInFront("the second edit mode, the dims last");
                ShowRows();
                AllInFront("the second edit mode");
            }
            finally
            {
                foreach (var (shade, add, _) in shown)
                {
                    add.Dispose();
                    shade.Dispose();
                }
            }

            void AllInFront(string when)
            {
                foreach (var (shade, add, bounds) in shown)
                {
                    Assert.True(IsWindowVisible(shade.Hwnd), $"{when}: the dim is not up");
                    Assert.True(add.IsOpen, $"{when}: the edit bar of {bounds} is not open");
                    Assert.True(IsWindowVisible(add.Hwnd), $"{when}: the edit bar of {bounds} is not in sight");
                    Assert.True(Z.Above(add.Hwnd, shade.Hwnd), $"{when}: the edit bar of {bounds} stands behind its dim");
                    var panel = Assert.IsType<RECT>(add.Panel);
                    // At the top, centred, its margin kept under what stands there (nothing here).
                    var margin = (int)MathF.Round(EditDeckPlan.MarginDip * scale);
                    Assert.Equal(bounds.Top + margin, panel.top);
                    Assert.InRange(panel.left - bounds.Left, (bounds.Width - (panel.right - panel.left)) / 2 - 1, (bounds.Width - (panel.right - panel.left)) / 2 + 1);
                    // §575: its things in one row, 48 DIP tall: no entries shown with no pages, Add, Done.
                    var row = add.Layout!;
                    Assert.Equal([PanelHitKind.Add, PanelHitKind.Done], row.Header.Select(part => part.Kind));
                    Assert.True(row.Header[^1].Rect.Right <= row.Size.X, "Done runs past its row");
                    Assert.Equal(MathF.Round(48 * scale), row.Size.Y);
                }
            }
        });
    }

    /// <summary>
    /// §359: where edit mode's windows stand is their strata's, whichever comes into sight last and in
    /// whatever order: the dim under the edit bar, a widget carried over both — in one frame or one by
    /// one, the dim shown again or not — never a frame of the dim over the edit bar to be lifted off it.
    /// </summary>
    [UiFact]
    public void EditModesWindowsStandByTheirStrata()
    {
        WindowShots.OnHiddenDesktop("edit-stack", (gfx, _) =>
        {
            var frame = MakeWindow();
            var row = MakeWindow();
            var carried = MakeWindow();
            try
            {
                Surfaces.Enlist(frame, Stratum.Dim);
                Surfaces.Enlist(row, Stratum.EditBar);
                Surfaces.Enlist(carried, Stratum.Carry);
                // The row and the carried widget first, the dim last: the dim under both.
                Surfaces.Show(row);
                Surfaces.Show(carried);
                Surfaces.Show(frame);
                Assert.True(Z.Above(row, frame) && Z.Above(carried, row));
                // All in one frame, the dim asked last again: the same, in one batch.
                foreach (var window in new[] { frame, row, carried })
                {
                    Surfaces.Hide(window);
                }
                using (Surfaces.Begin(gfx.Compositor))
                {
                    Surfaces.Show(carried);
                    Surfaces.Show(row);
                    Surfaces.Show(frame);
                }
                Present.FrameFilm.Settle();
                Assert.True(Z.Above(row, frame) && Z.Above(carried, row));
                // The row brought to the front of its stratum: still under what is carried.
                Surfaces.Front(row);
                Assert.True(Z.Above(carried, row) && Z.Above(row, frame));
                Assert.False(Z.Above(row, row));
            }
            finally
            {
                foreach (var window in new[] { carried, row, frame })
                {
                    Surfaces.Destroy(window);
                }
            }
        });
    }

    /// <summary>
    /// Two 4K displays at 150 % side by side in edit mode, as the person has them: the left one showing
    /// a Taskbar along the bottom, the right one a Dock along the bottom. Both
    /// with their edit bar over the right end of their bottom bar, in dark and light, as
    /// edit-displays-4k-&lt;dark|light&gt;-150.png.
    /// </summary>
    [UiFact]
    public void EditDisplays4KSheet()
    {
        WindowShots.OnHiddenDesktop("edit-displays-4k", (gfx, instance) =>
        {
            const uint dpi = 144;
            var config = new Config();
            var first = PictureLayout(LayoutType.Taskbar, Edge.Bottom);
            var second = PictureLayout(LayoutType.Dock, Edge.Bottom);
            foreach (var dark in new[] { true, false })
            {
                var look = AddListPictures.LookFor(dark) with { Animations = false };
                var name = dark ? "dark" : "light";
                var one = EditedDisplay(gfx, instance, dpi, config, look, first, $"edit-displays-4k-1-{name}.png", keep: true, dips: FourK);
                var two = EditedDisplay(gfx, instance, dpi, config, look, second, $"edit-displays-4k-2-{name}.png", neighbourLeft: true, keep: true, dips: FourK);
                WindowShots.SaveSideBySide([one!.Value, two!.Value], $"edit-displays-4k-{name}-150.png", dark ? ((byte)0, (byte)0, (byte)0) : ((byte)0xFF, (byte)0xFF, (byte)0xFF));
            }
        });
    }
}
