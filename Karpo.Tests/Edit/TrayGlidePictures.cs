// tray: every icon an update moves slides on from where it is (the person, 2026-10-16: "not all of
// the tray icons get animated on their move, some just jump"). A tray at the bar's end grown by a cell
// for an icon coming from the overflow (Content.TrayMore) moves all its icons a cell towards the bar's
// start; the drag's next move then opens the gap after the first, sending the others back. They used to
// jump to their new places and slide back from there — only an icon a drag had moved had a glide to go
// on from (Scene.CarryTray) —, so the first slid and the others jumped twice. Now each goes on from
// where it is: the first slides into its place and the others stand still, as they should. The real
// scene, animations on, at 100 % and 150 %, and a sheet of the frames as the compositor draws them, to
// KARPO_PNG_DIR as tray-glide-frames-<scale>.png, one frame under the other every 25 ms.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using Windows.UI.Composition.Desktop;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    [UiFact]
    public void TrayIconsMovedByAnUpdateGoOnFromWhereTheyAre()
    {
        WindowShots.OnHiddenDesktop("tray-glide", (gfx, instance) =>
        {
            foreach (var dpi in new uint[] { 96, 144 })
            {
                var look = AddListPictures.LookFor(dark: true) with { Animations = true };
                var config = new Config { Height = 48, Style = BarStyle.Islands };
                var area = new RECT { left = 0, top = 0, right = (int)(480 * dpi / 96), bottom = (int)(dpi * 10) };
                var px = (int)MathF.Ceiling(Layout.TrayIconPx(dpi, config));
                var icons = DraggedTrayIcons(3, px);
                var specs = TrayBar();
                var trayKey = specs[0].Key;
                var rest = new Content { Tray = icons, TrayOverflow = true };
                var grown = new Content { Tray = icons, TrayOverflow = true, TrayMore = new Dictionary<string, int> { [trayKey] = 1 } };
                var before = new Layout(gfx, dpi, area, area, Edge.Bottom, config, look, specs, rest);
                var after = new Layout(gfx, dpi, area, area, Edge.Bottom, config, look, specs, grown);
                var cell = after.TrayStep;
                // At the bar's end the tray grows towards its start: every icon a cell that way.
                for (var index = 0; index < icons.Count; index++)
                {
                    Assert.Equal(before.TrayIconRect(index).X - cell, after.TrayIconRect(index).X, 0.5f);
                }

                var window = MakeWindow();
                DesktopWindowTarget? target = null;
                Scene? second = null;
                try
                {
                    _ = SetWindowPos(window, HWND.NULL, 0, 0, before.Width, before.Height, SWP.SWP_NOZORDER | SWP.SWP_NOACTIVATE);
                    target = Target(gfx, window);
                    second = Scenes.Build(gfx, target, before, rest, look, new Icons(), shown: true);
                    _ = ShowWindow(window, SW.SW_SHOWNOACTIVATE);
                    WindowShots.Settle(250);

                    // Grown: every icon taken up a cell from its new place, sliding into it.
                    // Read by what each glide holds, not by the clock at one moment: one tick of the
                    // clock (15.6 ms) between the scene starting the glides and the test's look, on a
                    // busy machine, had them near half way on already.
                    second.Update(after, grown, look, new Icons(), shown: true);
                    var glides = icons.Select(icon => Assert.NotNull(second.TrayGlideOf(0, icon.Key))).ToList();
                    foreach (var glide in glides)
                    {
                        Assert.Equal(cell, glide.From, 1f);
                        Assert.Equal(0f, glide.Target);
                    }
                    // The drag's next move: the gap after the first. The others go back where they were
                    // — which is where they are: they stand still, from where they are, not from their
                    // places. Where they are is where their glides had them when the move came.
                    var asked = Environment.TickCount64;
                    second.TrayDrag(new TrayDragView(null, 1, More: 1));
                    var moved = Environment.TickCount64;
                    foreach (var index in new[] { 1, 2 })
                    {
                        var back = Assert.NotNull(second.TrayGlideOf(0, icons[index].Key));
                        Assert.Equal(cell, back.Target);
                        Assert.InRange(back.From, glides[index].At(moved) - 0.01f, glides[index].At(asked) + 0.01f);
                    }
                    // The first goes on into its place.
                    var lead = Assert.NotNull(second.TrayGlideOf(0, icons[0].Key));
                    Assert.InRange(lead.At(moved), 0f, cell + 0.5f);
                    Assert.Equal(0f, lead.At(moved + (long)Motion.NormalMs(MotionKind.Move)));

                    // The frames, as the compositor draws them.
                    var panel = after.PanelRect;
                    var part = new RECT { left = (int)panel.X, top = (int)panel.Y, right = (int)panel.Right, bottom = (int)panel.Bottom };
                    var frames = new List<byte[]>();
                    var wide = 0;
                    var name = $"tray-glide-frames-{dpi * 100 / 96}.png";
                    for (var frame = 0; frame < 9; frame++)
                    {
                        Assert.True(WindowShots.Save(window, name, part, (pixels, width) => (frames, wide) = ([.. frames, pixels], width)), $"{name} came back blank");
                        WindowShots.Settle(25);
                    }
                    var tall = frames[0].Length / 4 / wide;
                    var sheet = new byte[frames.Count * frames[0].Length];
                    for (var index = 0; index < frames.Count; index++)
                    {
                        Buffer.BlockCopy(frames[index], 0, sheet, index * frames[0].Length, frames[0].Length);
                    }
                    WindowShots.WritePng(sheet, wide, tall * frames.Count, Path.Combine(Offscreen.Pictures, name));
                }
                finally
                {
                    second?.Dispose();
                    _ = DestroyWindow(window);
                }
            }
        });
    }
}
