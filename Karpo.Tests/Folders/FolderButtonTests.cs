// folders: the folder button's size on the bar.
//
// The button's cells come from its settings — the bar's height, its faces —
// and never from how many widgets are inside it, nor a name (§427). A folder
// holding one widget is exactly as wide as one holding nine: its icon is one
// square, chosen or the folder's own (FolderOptions.Face).

using Karpo.Core;
using Karpo.Dock;
using Karpo.Dock.Native;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
using IDispatcherQueueController = TerraFX.Interop.WinRT.IDispatcherQueueController;

namespace Karpo.Tests.Folders;

public sealed unsafe class FolderButtonTests
{
    private const uint Dpi = 96;

    private static Look LookFor(bool dark) =>
        new(Glass: false, Dark: dark, Accent: Windows.UI.Color.FromArgb(255, 0, 0x78, 0xD4));

    internal static BarItem Folder(int count, string glyph = "", string face = FolderFaces.IconId)
    {
        var island = BarKind.FolderList.New();
        island.Id = "face";
        var folder = island.Folder!;
        folder.Face = FolderFace.Of(face);
        folder.Face.Glyph = glyph;
        folder.Lists = [[]];
        BarKind[] kinds = [BarKind.WidgetsBoard, BarKind.EnergySaver, BarKind.TouchKeyboard, BarKind.WidgetsBoard, BarKind.NightLight, BarKind.Volume];
        for (var index = 0; index < count; index++)
        {
            folder.Put(kinds[index % kinds.Length].New().AsCell());
        }
        return island;
    }

    /// <summary>How wide that folder's button comes out on a bar of <paramref name="height"/>.</summary>
    private static float Width(Gfx gfx, BarItem island, int height, Edge edge)
    {
        var config = new Config { Height = height };
        var vertical = edge.IsVertical();
        var area = vertical
            ? new RECT { left = 0, top = 0, right = 1200, bottom = 2600 }
            : new RECT { left = 0, top = 0, right = 2600, bottom = 1200 };
        var made = new IslandCollection();
        var key = Config.IslandKey(null, island.Id);
        var built = Karpo.Dock.Islands.Create(key, island, gfx, config);
        Assert.NotNull(built);
        _ = built.Refresh(gfx);
        made[key] = built;
        var specs = new List<PartSpec> { new(key, island.Kind) };
        var layout = new Layout(gfx, Dpi, area, area, edge, config, LookFor(true), specs, new Content { Islands = made });
        try
        {
            return layout.Parts[0].Width;
        }
        finally
        {
            built.Dispose();
        }
    }

    [UiFact]
    public void TheButtonIsTheSameWidthWhateverIsInside()
    {
        OnStaThread(gfx =>
        {
            foreach (var edge in (Edge[])[Edge.Bottom, Edge.Left])
            {
                foreach (var height in (int[])[28, 48, 72])
                {
                    var empty = Width(gfx, Folder(0), height, edge);
                    Assert.True(empty > 0);
                    foreach (var count in (int[])[1, 2, 3, 4, 9])
                    {
                        Assert.Equal(empty, Width(gfx, Folder(count), height, edge), 3);
                    }

                    // The icon is the same square, chosen or the folder's own.
                    Assert.Equal(empty, Width(gfx, Folder(4, face: FolderFaces.IconId, glyph: ""), height, edge), 3);
                    Assert.Equal(empty, Width(gfx, Folder(0, face: FolderFaces.IconId, glyph: ""), height, edge), 3);
                    Assert.Equal(empty, Width(gfx, Folder(4, face: FolderFaces.IconId), height, edge), 3);
                }
            }
        });
    }

    /// <summary>
    /// §427: a folder has no name, so none stands beside its button: a file that still says one and to
    /// show it draws its icon's square, or its clock, exactly as wide as a folder that never had one —
    /// along the bottom and on end alike.
    /// </summary>
    [UiFact]
    public void NoNameStandsBesideTheButton()
    {
        OnStaThread(gfx =>
        {
            foreach (var face in (string[])[FolderFaces.IconId, FolderFaces.ClockId])
            {
                foreach (var edge in (Edge[])[Edge.Bottom, Edge.Left])
                {
                    Assert.Equal(Width(gfx, Folder(0, face: face), 48, edge), Width(gfx, Named(face), 48, edge), 3);
                }
            }
        });

        static BarItem Named(string face) => Karpo.Tests.Core.Configs.Islands(Karpo.Tests.Core.Configs.Repaired($$"""
            { "layouts": [ { "id": "L", "islands": [
              { "id": "face", "kind": "folderList", "folder": { "name": "Glance", "showName": true,
                "face": { "kind": "{{face}}", "options": {} }, "lists": [ [] ] } } ] } ] }
            """)).Single();
    }

    internal static void OnStaThread(Action<Gfx> body)
    {
        Exception? failed = null;
        var thread = new Thread(() =>
        {
            try
            {
                // The islands are made on a thread with a dispatcher queue, as the dock's own is.
                var options = new DispatcherQueueOptions
                {
                    dwSize = (uint)sizeof(DispatcherQueueOptions),
                    threadType = DISPATCHERQUEUE_THREAD_TYPE.DQTYPE_THREAD_CURRENT,
                    apartmentType = DISPATCHERQUEUE_THREAD_APARTMENTTYPE.DQTAT_COM_STA,
                };
                IDispatcherQueueController* controller = null;
                ComUtil.Check(CreateDispatcherQueueController(options, &controller));
                try
                {
                    using var gfx = new Gfx(gpu: false);
                    body(gfx);
                }
                finally
                {
                    controller->Release();
                }
            }
            catch (Exception error)
            {
                failed = error;
            }
        });
        thread.Start();
        thread.Join();
        if (failed is not null)
        {
            throw failed;
        }
    }
}
