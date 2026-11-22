// A part takes the pointer where it is drawn (docs\widgets.md §370): every pixel a part of the bar draws
// is that part's to the pointer — a press there is its click, the pointer there lights it.
//
// The person (2026-10-26): "the click area on the weather widget is way off. its only like half of the
// widget. fix this in other places too". Asked of the bar as it stands: its parts' drawings taken out of
// a picture of the bar a third at a time (Scene.Hide), what the picture loses is what they draw, and the
// layout asked what is under each of those pixels (Layout.HitTest) — every part the bar can hold (Windows'
// buttons, the app buttons, the tray, Show desktop, every widget the bar draws, and a folder wearing every
// face, alone and several at once), on every edge, 32 DIP at 100 % and 150 %, 48 DIP and a dock at 150 %,
// and again after relayouts in flight. The grip band along the inner edge is the bar's by design (Takes).

using System.Runtime.InteropServices;
using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Folders;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using Xunit.Abstractions;
using static TerraFX.Interop.Windows.Windows;
using Edge = Karpo.Core.Edge;

namespace Karpo.Tests.Present;

public sealed unsafe class BarHitsWhereItDrawsTests(ITestOutputHelper output)
{
    /// <summary>How far a pixel's own colour may move with the part's drawing gone before it counts as the part's (anti-aliasing's last shades excepted).</summary>
    private const int Ink = 24;

    /// <summary>The parts are taken out of the picture a third at a time (<see cref="Check"/>).</summary>
    private const int Rounds = 3;

    [UiFact]
    public void EveryPartTakesThePointerWhereItDraws()
    {
        var wrong = new List<string>();
        var checkedParts = 0;
        RetainedBarTests.OnHiddenDesktop("hits-drawn", (gfx, instance) =>
        {
            // A thin bar (the person's: 32 DIP) on every edge at 100 % and 150 %, flush (spaced 0, §533) at 100 % and spaced 6
            // at 150 %; a tall one and a dock at 150 %.
            var cases = new List<(uint Dpi, Edge Edge, Config Config)>();
            foreach (var dpi in new uint[] { 96, 144 })
            {
                foreach (var edge in new[] { Edge.Bottom, Edge.Top, Edge.Left, Edge.Right })
                {
                    var spacing = dpi == 96 ? 0 : Config.DefaultSpacing;
                    cases.Add((dpi, edge, new Config { Height = 32, Style = BarStyle.Islands, Layouts = [new TaskbarLayout { Spacing = spacing }] }));
                }
            }
            foreach (var edge in new[] { Edge.Bottom, Edge.Left })
            {
                cases.Add((144, edge, new Config { Height = 48, Style = BarStyle.Islands }));
            }
            foreach (var edge in new[] { Edge.Bottom, Edge.Right })
            {
                cases.Add((144, edge, new Config { Height = 48, Style = BarStyle.Floating }));
            }
            foreach (var (dpi, edge, config) in cases)
            {
                foreach (var batch in Batches())
                {
                    checkedParts += Check(gfx, instance, dpi, edge, config, batch, wrong);
                }
            }
        });
        output.WriteLine($"{checkedParts} parts checked");
        Assert.True(wrong.Count == 0, $"{wrong.Count} parts drawn where they do not take the pointer:\n{string.Join("\n", wrong.Take(60))}");
    }

    [UiFact]
    public void AfterMovesInFlight()
    {
        var wrong = new List<string>();
        RetainedBarTests.OnHiddenDesktop("hits-moved", (gfx, instance) =>
        {
            var look = RetainedBarTests.Still with { Animations = true };
            foreach (var style in new[] { BarStyle.Islands, BarStyle.Floating })
            {
                Config Tall(int height) => new() { Height = height, Style = style };
                foreach (var batch in Batches().Take(2))
                {
                    // A widget gone and back, the bar another height in between, each before the last one's glide ended.
                    _ = Check(gfx, instance, 144, Edge.Bottom, Tall(48), batch, wrong, look, (bar, specs, content) =>
                    {
                        bar.Lay(specs, content, look, Tall(48));
                        WindowShots.Settle(400);
                        bar.Lay([.. specs.Where((_, at) => at != 5)], content, look, Tall(48));
                        WindowShots.Settle(40);
                        bar.Lay(specs, content, look, Tall(56));
                        WindowShots.Settle(40);
                        bar.Lay([.. specs.Where((_, at) => at != 6)], content, look, Tall(48));
                        WindowShots.Settle(40);
                    });
                }
            }
        });
        Assert.True(wrong.Count == 0, $"{wrong.Count} parts drawn where they do not take the pointer:\n{string.Join("\n", wrong.Take(60))}");
    }

    /// <summary>
    /// One bar: Windows' buttons, three open apps, <paramref name="widgets"/>, the tray and Show
    /// desktop, laid out and shown; each part's drawing taken out in turn and every pixel it took with it
    /// asked of the layout. Returns how many parts were asked.
    /// </summary>
    private static int Check(Gfx gfx, HINSTANCE instance, uint dpi, Edge edge, Config config, IReadOnlyList<BarItem> widgets, List<string> wrong, Look? look = null, Action<RetainedBarTests.Bar, List<PartSpec>, Content>? before = null)
    {
        var display = new RECT { right = (int)(1920 * dpi / 96), bottom = (int)(1080 * dpi / 96) };
        var made = new IslandCollection();
        var specs = new List<PartSpec>
        {
            Spec("start", BarKind.Start),
            Spec("search", BarKind.Search),
            Spec("task", BarKind.TaskView),
            Spec("apps", BarKind.Apps),
        };
        try
        {
            foreach (var item in widgets)
            {
                var key = Config.IslandKey(null, item.Id);
                var island = item.Kind == BarKind.FolderList ? FolderFaceTests.Make(gfx, item) : Karpo.Dock.Islands.Create(key, item, gfx, new Config());
                if (island is null)
                {
                    continue;
                }
                _ = island.Refresh(gfx);
                made[key] = island;
                specs.Add(PartSpec.For(key, item));
            }
            specs.Add(Spec("tray", BarKind.Tray, BarSection.End));
            specs.Add(Spec("desk", BarKind.ShowDesktop, BarSection.End));
            var things = RetainedBarTests.Things(3);
            var content = new Content { Tiles = things.Tiles, Trays = things.Trays, Islands = made };
            using var bar = new RetainedBarTests.Bar(gfx, instance, edge, dpi: dpi, on: display);
            before?.Invoke(bar, specs, content);
            var layout = bar.Lay(specs, content, look ?? RetainedBarTests.Still, config);
            var where = $"{edge}, {config.Height} DIP at {dpi} DPI, {config.Style}";
            Assert.True(layout.NotShown.Count == 0, $"{where}: no room for {string.Join(", ", layout.NotShown.Select(spec => spec.Kind))}");
            FrameFilm.Settle();
            WindowShots.Settle(look?.Animations == true ? 600 : 0);
            var (whole, width, height) = Grab(bar.Hwnd);
            var (dx, dy) = (layout.X - bar.Frame.left, layout.Y - bar.Frame.top);
            var asked = Enumerable.Range(0, layout.Parts.Count)
                .Where(layout.Placed)
                .OrderBy(part => layout.Vertical ? layout.BlockRect(part).Y : layout.BlockRect(part).X)
                .ToList();
            // A third of the parts out of the picture at a time, no two of them neighbours: a pixel that
            // goes with them is one of theirs, and the pointer there must be on one of them — never on a
            // neighbour, the panel, the grip or nothing.
            for (var round = 0; round < Rounds; round++)
            {
                var hidden = asked.Where((_, rank) => rank % Rounds == round).ToHashSet();
                foreach (var part in hidden)
                {
                    bar.Scene.Hide(part, true);
                }
                FrameFilm.Settle();
                var (without, _, _) = Grab(bar.Hwnd);
                foreach (var part in hidden)
                {
                    bar.Scene.Hide(part, false);
                }
                var off = new Dictionary<int, (int Count, string First)>();
                for (var y = 0; y < height; y++)
                {
                    for (var x = 0; x < width; x++)
                    {
                        var at = ((y * width) + x) * 4;
                        if (Math.Abs(whole[at] - without[at]) <= Ink && Math.Abs(whole[at + 1] - without[at + 1]) <= Ink && Math.Abs(whole[at + 2] - without[at + 2]) <= Ink)
                        {
                            continue;
                        }
                        // Within a pixel: the layout's own px, the pixel's middle, and its neighbours for an edge's shading.
                        var (lx, ly) = (x - dx + 0.5f, y - dy + 0.5f);
                        if (Takes(layout, hidden, lx, ly))
                        {
                            continue;
                        }
                        var whose = hidden.MinBy(part => Away(layout.BlockRect(part), lx, ly));
                        var (count, first) = off.GetValueOrDefault(whose);
                        off[whose] = (count + 1, count == 0 ? $"({lx - 0.5f:0},{ly - 0.5f:0}): {Describe(layout, layout.HitTest(lx, ly))}" : first);
                    }
                }
                foreach (var (part, (count, first)) in off)
                {
                    var block = layout.BlockRect(part);
                    wrong.Add($"{where}: {Name(layout, part)} (block {block.X:0},{block.Y:0} {block.Width:0}x{block.Height:0}): {count} px it draws are not its own, first at {first}");
                }
            }
            return asked.Count;
        }
        finally
        {
            foreach (var island in made.Values)
            {
                island.Dispose();
            }
        }
    }

    /// <summary>How far (<paramref name="x"/>, <paramref name="y"/>) lies outside <paramref name="block"/>; 0 inside it.</summary>
    private static float Away(RectF block, float x, float y) =>
        MathF.Max(MathF.Max(block.X - x, x - block.Right), 0f) + MathF.Max(MathF.Max(block.Y - y, y - block.Bottom), 0f);

    /// <summary>Whether the pointer at (<paramref name="x"/>, <paramref name="y"/>), or a pixel beside it, is one of <paramref name="parts"/>'.</summary>
    private static bool Takes(Layout layout, HashSet<int> parts, float x, float y)
    {
        for (var oy = -1; oy <= 1; oy++)
        {
            for (var ox = -1; ox <= 1; ox++)
            {
                // The grip band along the inner edge is the bar's over a widget too, by design: on a bar with no
                // room left between its widgets it is where the bar's own menu and its height are (docs\widgets.md,
                // 2026-10-04, "the grip band ... is the bar on every bar"; BarMenuTests.TheGripBandIsTheBarEvenOverAWidget).
                if (layout.HitTest(x + ox, y + oy) is { } hit && (parts.Contains(layout.PartOf(hit)) || hit.Kind == HitKind.ResizeGrip))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static string Name(Layout layout, int part) =>
        layout.Specs[part].Kind == BarKind.FolderList && layout.Specs[part].Key is var key ? $"folder {key[(key.LastIndexOf('/') + 1)..]}" : $"{layout.Specs[part].Kind}";

    private static string Describe(Layout layout, Hit? hit) =>
        hit is not { } on ? "off the bar" : layout.PartOf(on) is var part and >= 0 ? $"{on.Kind} of {Name(layout, part)}" : $"{on.Kind}";

    /// <summary>
    /// What the bars hold beside Windows' buttons, the apps and the tray, a few at a time so each bar has
    /// room for all of them: a folder wearing each face, one wearing several, and every other widget the bar draws.
    /// </summary>
    private static IEnumerable<IReadOnlyList<BarItem>> Batches()
    {
        var items = new List<BarItem>();
        foreach (var face in FolderFaces.All)
        {
            var folder = FolderFaceTests.Folder(face.Id);
            folder.Id = $"face-{face.Id}";
            items.Add(folder);
        }
        var several = FolderFaceTests.Folder(FolderFaces.WeatherId);
        several.Id = "faces";
        several.Folder!.Faces = [FolderFace.Of(FolderFaces.WeatherId), FolderFace.Of(FolderFaces.ClockId), FolderFace.Of(FolderFaces.IconId), FolderFace.Of(FolderFaces.AirQualityId)];
        items.Add(several);
        foreach (var kind in Enum.GetValues<BarKind>().Where(kind => kind != BarKind.FolderList && (kind.IsIsland() || kind.IsWindowsButton())))
        {
            var item = kind.New();
            item.Id = $"kind-{kind}";
            items.Add(item);
        }
        foreach (var item in items)
        {
            item.Section = BarSection.Start;
        }
        return items.Chunk(5);
    }

    private static PartSpec Spec(string id, BarKind kind, BarSection section = BarSection.Start) => new(Config.IslandKey(null, id), kind) { Section = section };

    /// <summary><paramref name="window"/> as it shows now, all of it, BGRA.</summary>
    private static (byte[] Pixels, int Width, int Height) Grab(HWND window)
    {
        RECT box;
        _ = GetWindowRect(window, &box);
        var (width, height) = (box.right - box.left, box.bottom - box.top);
        var screen = GetDC(HWND.NULL);
        var memory = CreateCompatibleDC(screen);
        var info = new BITMAPINFO();
        info.bmiHeader.biSize = (uint)sizeof(BITMAPINFOHEADER);
        info.bmiHeader.biWidth = width;
        info.bmiHeader.biHeight = -height;
        info.bmiHeader.biPlanes = 1;
        info.bmiHeader.biBitCount = 32;
        info.bmiHeader.biCompression = BI.BI_RGB;
        void* bits = null;
        var dib = CreateDIBSection(memory, &info, DIB_RGB_COLORS, &bits, HANDLE.NULL, 0);
        var old = SelectObject(memory, dib);
        try
        {
            _ = PrintWindow(window, memory, PW_RENDERFULLCONTENT);
            _ = GdiFlush();
            var pixels = new byte[width * height * 4];
            Marshal.Copy((nint)bits, pixels, 0, pixels.Length);
            return (pixels, width, height);
        }
        finally
        {
            _ = SelectObject(memory, old);
            _ = DeleteObject(dib);
            _ = DeleteDC(memory);
            _ = ReleaseDC(HWND.NULL, screen);
        }
    }
}
