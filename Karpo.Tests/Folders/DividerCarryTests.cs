// §502: a folder's divider carried — pasted into the folder standing open, added, dragged — is still what it
// is: it was lifted as its plate alone, its rule left behind out of sight; now edit mode's mark (§524 its
// stripes) goes with it on its carrier (FolderFlyout.Raise), standing still while it is carried. §505: a divider
// is empty space, so its ghost is the room it will take where it would land (its card, FolderFlyout.SeparatorRoom
// tall), with nothing drawn on it.
//
// The real folder on a desktop of its own, arranged at 150 %: a divider pasted in follows the pointer over
// the clock; a picture to KARPO_PNG_DIR as divider-carry.png. Needs KARPO_UI_TESTS=1.

using System.Reflection;
using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using Windows.UI.Composition;
using static Karpo.Tests.Folders.FolderEditClickTests;
using static TerraFX.Interop.Windows.Windows;
using Color = Windows.UI.Color;

namespace Karpo.Tests.Folders;

public sealed unsafe class DividerCarryTests
{
    private const string Owner = "layout:default";

    private static readonly Look Dark = new(Glass: false, Dark: true, Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4));
    private static readonly POINT Anchor = new() { x = 960, y = 1040 };
    private static readonly RECT Work = new() { right = 1920, bottom = 1040 };

    [UiFact]
    public void ADividerCarriedShowsItsRuleOnItAndItsRoomWhereItWouldLand()
    {
        var failures = new List<string>();
        WindowShots.OnHiddenDesktop("divider-carry", (gfx, instance) =>
        {
            {
                const string name = "divider";
                var island = BarKind.FolderList.New();
                island.Id = "box";
                foreach (var (line, id) in new[] { (ListKind.Clock.New(), "clock"), (ListKind.Battery.New(), "battery"), (ListKind.NightLight.New(), "night") })
                {
                    line.Id = id;
                    line.View = Karpo.Core.Folders.View(island.Kind);
                    island.Folder!.Put(line);
                }
                var config = new Config();
                config.ListFor(Owner).Add(island);
                var folder = new FolderIsland($"{Owner}/box", island, gfx, config);
                using var flyout = new FolderFlyout(gfx, instance, 0);
                try
                {
                    flyout.Show(gfx, Dark, 1.5f, Anchor, Work, Edge.Bottom, folder, editing: true);
                    // As a paste does (Dock.PasteLine): the copy into the folder where it adds, then carried.
                    var path = Karpo.Core.Folders.AddNew(config.ListFor(Owner), island, ListKind.Divider.New(), flyout.AddPlace());
                    var key = Config.IslandKey(Owner, path!);
                    folder.Configure(island, config);
                    var clock = flyout.Plan!.Cards[1];
                    var on = flyout.PanelOnScreen;
                    var over = new POINT { x = on.left + (int)MathF.Round((clock.left + clock.right) / 2), y = on.top + (int)MathF.Round(clock.top + 6) };
                    _ = SetCursorPos(over.x, over.y);
                    Assert.True(flyout.Carry(key), "the divider pasted is not carried");
                    _ = flyout.Message(WM.WM_MOUSEMOVE, 0, Client(flyout.Hwnd, over));
                    WindowShots.Settle(300);
                    Assert.True(WindowShots.Save(flyout.Hwnd, "divider-carry.png"));

                    var plan = flyout.Plan!;
                    var divider = Enumerable.Range(0, folder.Tiles.Count).Single(index => FolderFlyout.Divides(folder.Tiles[index].Cell));
                    if (divider >= plan.Bands.Count || plan.Bands[divider].IsEmpty)
                    {
                        failures.Add($"{name}: arranged, the folder stripes no room for its divider");
                    }
                    // Its ghost is the room it will take, and nothing on it.
                    var ghost = (ShapeVisual)typeof(FolderFlyout).GetField("ghostVisual", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(flyout)!;
                    if (!ghost.IsVisible || MathF.Abs(ghost.Size.Y - FolderFlyout.SeparatorRoom(1.5f)) > 0.5f)
                    {
                        failures.Add($"{name}: the ghost is {(ghost.IsVisible ? "shown" : "hidden")}, {ghost.Size.Y} px tall, not the divider's room {FolderFlyout.SeparatorRoom(1.5f)}");
                    }
                    if (ghost.Shapes.Count != 1)
                    {
                        failures.Add($"{name}: the ghost has {ghost.Shapes.Count} shapes, not its outline alone");
                    }
                    // Lifted, and its stripes go with it, on the carrier, not left out of sight; carried, they stand still (§524).
                    var band = flyout.Stripes[divider];
                    if (!flyout.LiftedLine.Lifted)
                    {
                        failures.Add($"{name}: the divider pasted is not lifted: the folder shows its landing bar alone");
                    }
                    else if (!band.Visual.IsVisible)
                    {
                        failures.Add($"{name}: the divider carried is an empty plate, its stripes hidden");
                    }
                    if (band.Marching)
                    {
                        failures.Add($"{name}: the divider carried has its stripes moving");
                    }

                }
                finally
                {
                    flyout.EndCarry();
                    flyout.Hide();
                    folder.Dispose();
                }
            }
        });
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }
}
