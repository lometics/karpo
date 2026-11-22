// What the bar holds but cannot show.
//
// A bar too short for its widgets drops them from the end rather than squeezing
// everything past being readable, and it does that silently — a badge on a bar
// already too crowded to hold its widgets is the last thing that bar needs. The
// telling belongs to the edit rail, where the person is already doing the thing
// that would fix it, so the bar's job is only to record what it dropped and be
// asked (Layout.NotShown).
//
// What this guards is that the record is the truth and not a second opinion:
// exactly the widgets that were given no room, in the list's order, and empty
// when everything fitted. A count the rail cannot trust is worse than no count.
//
// It lays a real bar out, so it needs Direct2D and a dispatcher queue and runs
// only with KARPO_UI_TESTS=1. Nothing is ever shown on the person's desktop.

using Karpo.Dock;
using Karpo.Dock.Native;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
using Color = Windows.UI.Color;
using IDispatcherQueueController = TerraFX.Interop.WinRT.IDispatcherQueueController;

namespace Karpo.Tests.Sizing;

public sealed unsafe class NotShownTests
{
    /// <summary>Enough widgets, each wide enough, that a short bar must give some up.</summary>
    private static readonly (string Id, BarKind Kind, int Width)[] Widgets =
    [
        ("weather", BarKind.FolderList, 3),
        ("timer", BarKind.NightLight, 3),
        ("battery", BarKind.EnergySaver, 2),
        ("quiet", BarKind.DoNotDisturb, 2),
        ("folder", BarKind.FolderList, 2),
    ];

    private static Look LookFor() => new(
        Glass: false,
        Dark: true,
        Accent: Color.FromArgb(255, 0, 0x78, 0xD4));

    /// <summary>
    /// What the record means, said without reaching inside the bar: a widget is
    /// left off when it would have been shown had there been room and was not.
    /// So the same widgets are laid out twice, once with room to spare and once
    /// without, and the difference between the two bars must be exactly what the
    /// short one recorded — no more, which would name a widget that was never
    /// going to be drawn, and no fewer, which is the half that fails in silence.
    /// <para>
    /// The difference is the right question and "has no width" is not: a widget
    /// with nothing to show has no width either, and it is not hidden, it is
    /// empty. A count that confuses the two would tell someone to make room for a
    /// widget that would still show them nothing.
    /// </para>
    /// </summary>
    [Karpo.Tests.Islands.UiFact]
    public void TheBarRecordsTheWidgetsItCouldNotShow()
    {
        OnStaThread(gfx =>
        {
            foreach (var edge in (Edge[])[Edge.Bottom, Edge.Left])
            {
                var roomy = Bar(gfx, edge, along: 4000);
                var cramped = Bar(gfx, edge, along: 300);

                // With room to spare nothing gives way, whatever else is empty.
                Assert.Empty(roomy.NotShown);

                var shown = Shown(roomy);
                var left = Shown(cramped);
                var gaveWay = shown.Where(key => !left.Contains(key)).ToList();
                Assert.Equal(gaveWay, cramped.NotShown.Select(spec => spec.Key).ToList());

                // And the short bar really was short, and really did keep some of
                // its widgets: without both, everything above holds of a bar that
                // dropped nothing, or of one that dropped the lot.
                Assert.NotEmpty(gaveWay);
                Assert.True(left.Count > 0, $"the {edge} bar gave up every one of its {shown.Count} widgets");

                foreach (var spec in cramped.NotShown)
                {
                    Assert.True(spec.Kind.IsIsland(), $"{spec.Kind} is not a widget and cannot be left off");
                }
            }
        });
    }

    /// <summary>The widgets <paramref name="layout"/> drew, in the order the person arranged them.</summary>
    private static List<string> Shown(Layout layout)
    {
        var widths = layout.Parts.ToDictionary(part => part.Key, part => part.Width);
        return [.. layout.Specs
            .Where(spec => spec.Kind.IsIsland() && widths[spec.Key] > 0f)
            .Select(spec => spec.Key)];
    }

    private static Layout Bar(Gfx gfx, Edge edge, int along)
    {
        var config = new Config { Height = 48 };
        var specs = new List<PartSpec>();
        var islands = new IslandCollection();
        try
        {
            foreach (var (id, kind, width) in Widgets)
            {
                var key = Config.IslandKey(null, id);
                var settings = kind == BarKind.FolderList
                    ? new BarItem { Id = id, Kind = kind, Folder = new FolderOptions() }
                    : new BarItem { Id = id, Kind = kind };
                if (Karpo.Dock.Islands.Create(key, settings, gfx, config) is not { } island)
                {
                    continue;
                }
                _ = island.Refresh(gfx);
                islands[key] = island;
                specs.Add(new PartSpec(key, kind, width));
            }
            var vertical = edge.IsVertical();
            var area = vertical
                ? new RECT { left = 0, top = 0, right = 1200, bottom = along }
                : new RECT { left = 0, top = 0, right = along, bottom = 1200 };
            var content = new Content { Islands = islands };
            return new Layout(gfx, 96, area, area, edge, config, LookFor(), specs, content);
        }
        finally
        {
            foreach (var island in islands.Values)
            {
                island.Dispose();
            }
        }
    }

    /// <summary>Runs <paramref name="body"/> on a thread with a dispatcher queue (the compositor needs one) and a Gfx.</summary>
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
        thread.Start();
        thread.Join();
        if (failed is not null)
        {
            throw new Xunit.Sdk.XunitException("On the offscreen thread: " + failed);
        }
    }
}
