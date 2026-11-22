// edit: the buttons over each list of an open folder — its "+", its width and its remove — as a
// group centred on the list (2026-10-11: "right now the add button is always in the center but the
// group should be centered"). One list, two and three of every width, at 100, 150 and 225 %, a red
// hairline at each list's middle: folder-add-group-<100|150|225>.png in KARPO_PNG_DIR.

using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using static Karpo.Tests.Islands.Offscreen;

namespace Karpo.Tests.Folders;

public sealed unsafe class FolderAddGroupPictures
{
    private static FolderIsland Folder(Gfx gfx, FolderListWidth[] widths)
    {
        ListKind[][] lines = [[ListKind.EnergySaver, ListKind.NightLight], [ListKind.Cpu], [ListKind.Memory, ListKind.AirplaneMode]];
        var holder = BarKind.FolderList.New();
        for (var list = 0; list < widths.Length; list++)
        {
            foreach (var kind in lines[list])
            {
                holder.Folder!.Put(kind.New(), list);
            }
        }
        for (var list = 0; list < widths.Length; list++)
        {
            Karpo.Core.Folders.SetWidth(holder.Folder!, list, widths[list]);
        }
        var folder = new FolderIsland($"{ListFormPictures.Owner}/{holder.Id}", holder, gfx, new Config());
        folder.Opened(gfx);
        return folder;
    }

    [UiFact]
    public void TheGroupsOverTheLists()
    {
        FolderListWidth[][] shapes =
        [
            [FolderListWidth.Narrow],
            [FolderListWidth.Wide],
            [FolderListWidth.Wide, FolderListWidth.Narrow],
            [FolderListWidth.Narrow, FolderListWidth.Medium, FolderListWidth.Wide],
        ];
        foreach (var scale in new[] { 1f, 1.5f, 2.25f })
        {
            OnStaThread(gfx =>
            {
                var look = ListFormPictures.LookFor(dark: true);
                var theme = FlyoutTheme.For(look);
                var folders = shapes.Select(shape => Folder(gfx, shape)).ToList();
                try
                {
                    var plans = folders.Select(folder => ListFormPictures.Plan(folder, look, scale, editing: true).Plan).ToList();
                    var room = PopupPaint.Round(FolderFlyout.OutsideRoomDip * scale) + 16;
                    var width = room + plans.Sum(plan => plan.Width + (2 * room));
                    var height = (2 * room) + plans.Max(plan => plan.Height);
                    Bitmap(width, height, dc =>
                    {
                        var backdrop = new DXGI_RGBA(0.20f, 0.24f, 0.30f, 1);
                        dc->Clear(&backdrop);
                        var left = (float)room;
                        for (var index = 0; index < plans.Count; index++)
                        {
                            var (folder, plan) = (folders[index], plans[index]);
                            var context = dc;
                            Shifted(context, left, room, () =>
                            {
                                foreach (var piece in plan.Pieces)
                                {
                                    PopupPaint.Fill(context, piece, plan.Radius, PopupPaint.Ink(theme.Base));
                                }
                                ListFormPictures.Panel(context, gfx, folder, plan, look, theme, scale);
                                foreach (var add in plan.Adds)
                                {
                                    FolderFlyout.PaintAdd(context, gfx, add.Box, theme, FolderFlyout.AddLook.Rest, add.Kind);
                                }
                                foreach (var list in plan.Lists)
                                {
                                    var middle = list.Left + (list.Width / 2);
                                    PopupPaint.Fill(context, PopupPaint.Rect(middle - 0.5f, -room, 1, room + list.Box.top), 0, new DXGI_RGBA(1, 0.2f, 0.2f, 1));
                                }
                            });
                            left += plan.Width + (2 * room);
                        }
                    }, bitmap => Save(bitmap, Path.Combine(Pictures, $"folder-add-group-{scale * 100:0}.png")));
                    // Each group centred on its list: as much room left of its first button as right of its last.
                    foreach (var plan in plans)
                    {
                        for (var list = 0; list < plan.Lists.Count; list++)
                        {
                            var (first, last) = (plan.Adds.First(add => add.List == list).Box, plan.Adds.Last(add => add.List == list).Box);
                            var middle = plan.Lists[list].Left + (plan.Lists[list].Width / 2);
                            Assert.InRange(middle - first.left - (last.right - middle), -1, 1);
                        }
                    }
                }
                finally
                {
                    foreach (var folder in folders)
                    {
                        folder.Closed();
                        folder.Dispose();
                    }
                }
            });
        }
    }
}
