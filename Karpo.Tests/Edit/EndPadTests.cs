// The bar keeps no pad at its two ends beyond the room its cards keep off the bar (Layout.CardInsetPx,
// docs 56): the first widget starts that far from the display's edge and the last ends that far
// from the other, with only the gap between neighbours in between. (It was "the icons on the left
// and right should go all the way to the outside without any padding" until the person asked for
// a little space between the widgets and the taskbar, 2026-10-09.)
//
// The rule this has to keep is exactly one sentence: taking the pads out shifts every widget by
// the pad and changes no gap between neighbours. It is held over the bar a start gives everybody
// (the defaults, which take an older config.json's place since config shape 17), at every
// height, scale and edge below.

using Karpo.Dock;
using Karpo.Dock.Native;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
using IDispatcherQueueController = TerraFX.Interop.WinRT.IDispatcherQueueController;

namespace Karpo.Tests.Edit;

public sealed unsafe class EndPadTests
{
    /// <summary>What the bar used to keep at each end, in DIPs (Layout.EdgePadDip, now gone).</summary>
    private const float WasPadDip = 12;

    private static readonly int[] Heights = [28, 41, 48, 72, 115, 160];

    private static readonly uint[] Dpis = [96, 120, 144];

    private static readonly Edge[] Edges = [Edge.Bottom, Edge.Top, Edge.Left, Edge.Right];

    /// <summary>
    /// The whole of the change, on the bar everybody starts with: every widget moved towards the start by
    /// the pad that is gone, and no gap between two neighbours changed. The old placement is
    /// worked out here rather than kept alive in the layout — the pads were a constant added at
    /// the start and taken off the end, so the old answer is the new one shifted, and the last
    /// widget's end is what says the end pad has gone too.
    /// </summary>
    [Karpo.Tests.Islands.UiFact]
    public void TakingThePadsOutShiftsEveryWidgetAndChangesNoGap()
    {
        var config = new Config();
        _ = config.Repair();
        OnStaThread(gfx =>
        {
            foreach (var layout in config.Layouts)
            {
                foreach (var height in Heights)
                {
                    foreach (var dpi in Dpis)
                    {
                        foreach (var edge in Edges)
                        {
                            Flush(gfx, height, dpi, edge, layout.Islands);
                        }
                    }
                }
            }
        });
    }

    private static void Flush(Gfx gfx, int height, uint dpi, Edge edge, IReadOnlyList<BarItem> islands)
    {
        var length = edge.IsVertical() ? 1080f : 1920f;
        var inset = Layout.CardInsetPx(dpi, new Config { Height = height });
        var places = Place(gfx, height, dpi, edge, islands);
        var shown = islands.Where(island => places[island.Id].Width > 0).ToList();
        var where = $"{height} DIP, {dpi} DPI, {edge}";
        // At the thickest bars here, and down the side of a 1080-pixel display, three of the
        // defaults' fourteen parts still stand: two gaps to hold, and a first widget to find at
        // the edge.
        Assert.True(shown.Count >= 3, $"only {shown.Count} widgets measured anything at {where}");

        // The start section's first widget is flush with the display's edge — not within a pad of
        // it, on it. Where the start section shows nothing (its weather gave way on a crowded
        // bar), the first widget is the middle's, which stands where centring and its neighbours
        // put it, and has nothing to say about a pad.
        var first = places[shown[0].Id];
        if (shown[0].Section == BarSection.Start)
        {
            Assert.Equal(inset, first.Left);
        }

        var last = places[shown[^1].Id];
        if (last.Left + last.Width > length - inset)
        {
            // No room for everything: the list runs from the start and the tail falls off, so
            // there is nothing to say about the far end.
            return;
        }
        // And the end section's last widget ends on the display's other edge.
        if (shown[^1].Section == BarSection.End)
        {
            Assert.Equal(length - inset, last.Left + last.Width, 1f);
        }

        // And no gap between neighbours of one section changed: the island gap, or a separator's
        // length where the desktops ask for one, and never the pad that went. Two neighbours in
        // different sections stand as far apart as their sections put them.
        var pad = MathF.Round(WasPadDip * dpi / 96f);
        var run = islands.Where(island => places[island.Id].Width > 0).ToList();
        for (var at = 1; at < run.Count; at++)
        {
            if (run[at - 1].Section != run[at].Section)
            {
                continue;
            }
            var before = places[run[at - 1].Id];
            var after = places[run[at].Id];
            var gap = after.Left - (before.Left + before.Width);
            Assert.True(gap >= 0f, $"{run[at - 1].Kind} runs into {run[at].Kind} at {where}");
            Assert.True(
                gap < pad,
                $"{run[at - 1].Kind} to {run[at].Kind} is {gap} px apart at {where}, which is the pad's length");
        }
    }

    /// <summary>
    /// A widget at the start and one at the end, laid out at every scale: with no pads the first
    /// starts on the display's edge and the last ends on the other.
    /// </summary>
    [Karpo.Tests.Islands.UiFact]
    public void TheStartAndTheEndRunToTheDisplaysEdges()
    {
        OnStaThread(gfx =>
        {
            foreach (var dpi in Dpis)
            {
                var config = new Config { Height = 48 };
                var area = new RECT { left = 0, top = 0, right = 1920, bottom = 1080 };
                List<PartSpec> specs =
                [
                    new(Config.IslandKey(null, "w"), BarKind.WidgetsBoard),
                    new(Config.IslandKey(null, "b"), BarKind.EnergySaver) { Section = BarSection.End },
                ];
                var made = Layout.ForTests(dpi, area, Edge.Bottom, config, specs, Layout.CellPx(dpi, config));

                Assert.Equal(Layout.CardInsetPx(dpi, config), made.PartAt(0).Left);
                Assert.Equal(1920f - Layout.CardInsetPx(dpi, config), made.PartAt(1).Left + made.PartAt(1).Width, 1f);
            }
        });
    }

    private static Dictionary<string, (float Left, float Width)> Place(
        Gfx gfx, int height, uint dpi, Edge edge, IReadOnlyList<BarItem> islands)
    {
        var config = new Config { Height = height };
        var look = new Look(Glass: false, Dark: true, Accent: Windows.UI.Color.FromArgb(255, 0, 0x78, 0xD4));
        var area = new RECT { left = 0, top = 0, right = 1920, bottom = 1080 };
        var specs = islands
            .Select(island => new PartSpec(Config.IslandKey(null, island.Id), island.Kind) { Section = island.Section })
            .ToList();
        var built = new IslandCollection();
        foreach (var island in islands)
        {
            var key = Config.IslandKey(null, island.Id);
            if (Karpo.Dock.Islands.Create(key, island, gfx, config) is not { } made)
            {
                continue;
            }
            _ = made.Refresh(gfx);
            built[key] = made;
        }
        var layout = new Layout(gfx, dpi, area, area, edge, config, look, specs, new Content { Islands = built }, editing: false);
        var places = new Dictionary<string, (float, float)>(StringComparer.Ordinal);
        for (var index = 0; index < specs.Count; index++)
        {
            places[islands[index].Id] = (layout.Parts[index].Left, layout.Parts[index].Width);
        }
        return places;
    }

    private static void OnStaThread(Action<Gfx> body)
    {
        Exception? failed = null;
        var thread = new Thread(() =>
        {
            try
            {
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
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failed is not null)
        {
            throw new Xunit.Sdk.XunitException("On the offscreen thread: " + failed);
        }
    }
}
