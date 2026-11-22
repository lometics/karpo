// Every widget each registry names gets the island its drawing names, from that registry's own
// factory: the taskbar's entries and a list's lines.
//
// This is the general form of a bug that was live: Islands.Create switched on Section and had an
// arm for the grid folder (since gone) only, so a list folder — offered by the catalogue — fell
// through to null and had nothing on the bar at all. Nothing failed: a kind falling out of a switch is silent by
// construction, which is why the switch is gone (each registry says which drawing a widget is, and
// one table in Karpo.Dock\Islands\Island.cs says which class draws a drawing) and why these tests
// enumerate the registries instead of naming a few.
//
// Making a real island needs Direct2D and a dispatcher queue, so these run only with
// KARPO_UI_TESTS=1. Nothing is shown on the person's desktop and no window is created; the islands
// are made, checked and disposed.

using Karpo.Dock;
using Karpo.Dock.Native;
using Karpo.Dock.QuickSettings.Services;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
using IDispatcherQueueController = TerraFX.Interop.WinRT.IDispatcherQueueController;

namespace Karpo.Tests.Islands;

public sealed unsafe class IslandCreationTests
{
    /// <summary>A widget of <paramref name="kind"/> with its kind's own default options.</summary>
    private static BarItem Widget(BarKind kind) => kind.New();

    /// <summary>
    /// Every taskbar entry that names a drawing gets an island from the taskbar's factory, and one
    /// the scene draws itself gets none. Both halves matter: a missing island is a blank cell, a
    /// surplus one is drawn twice. What it made knows which widget it is and stands where its entry
    /// says: the entry's drawing and pointer answers, and no card to be confined to. A kind the
    /// taskbar does not know — one a later build wrote to the file — gets nothing from it.
    /// </summary>
    [UiFact]
    public void EveryTaskbarEntryGetsTheIslandItsEntryNames()
    {
        OnStaThread(gfx =>
        {
            var config = new Config();
            var unknown = new BarItem { Id = "unknown", Kind = TolerantEnumConverter<BarKind>.Unknown };
            using (var none = Karpo.Dock.Islands.Create(Config.IslandKey(null, unknown.Id), unknown, gfx, config))
            {
                Assert.True(none is null, $"a kind this build does not know was made a {none?.GetType().Name}");
            }
            foreach (var entry in BarEntries.All)
            {
                var bar = entry.Kind;
                var widget = Widget(bar);
                using var island = Karpo.Dock.Islands.Create(Config.IslandKey(null, widget.Id), widget, gfx, config);
                var wanted = entry.Drawing != WidgetSource.None;
                Assert.True(
                    (island is not null) == wanted,
                    $"{bar}: Islands.Create gave {(island is null ? "nothing" : island.GetType().Name)}, and its entry says it {(wanted ? $"is drawn by {entry.Drawing}" : "is drawn by the scene")}");
                if (island is CellIsland made)
                {
                    Assert.Equal(entry.Info, made.Info);
                    Assert.Equal(entry.Drawing, made.Drawing);
                    Assert.False(made.CardOnly, $"{bar} is confined to a card on the taskbar");
                    Assert.Equal(entry.Interaction, made.Interaction);
                }
            }
        });
    }

    /// <summary>
    /// Every line a list folder holds gets the island its line names, from the list's factory, and
    /// stands as that line: a line drawn as the widget's card is all that widget has there, and a
    /// row of a glyph and a name is not; the drawing and the pointer answers are the line's.
    /// </summary>
    [UiFact]
    public void EveryListLineGetsTheIslandItsLineNames()
    {
        OnStaThread(gfx =>
        {
            var config = new Config();
            foreach (var line in ListWidgets.All)
            {
                var widget = line.Kind.New();
                using var island = Karpo.Dock.Islands.CreateLine(Config.IslandKey(null, widget.Id), widget, line.Kind, gfx, config);
                var made = Assert.IsAssignableFrom<CellIsland>(island);
                Assert.Equal(line.Info, made.Info);
                Assert.Equal(line.Drawing, made.Drawing);
                Assert.Equal(line.Line == ListLine.Card, made.CardOnly);
                Assert.Equal(line.Interaction, made.Interaction);
            }
        });
    }

    /// <summary>
    /// Every drawing is named by a taskbar entry or a line, so every drawing class is made
    /// by a factory. One nobody names is a class nothing can make, kept compiling for no one — but
    /// for the shell (ShellIsland), the drawing a kind has before its own is built: every kind that
    /// was one is built now, and the next kind added ahead of its drawing is one again
    /// (ShellTests draws it).
    /// </summary>
    [Fact]
    public void EveryDrawingIsNamedBySomeWidget()
    {
        var named = Everywhere().Select(widget => widget.Drawing).Append(WidgetSource.Shell).ToHashSet();
        Assert.All(Enum.GetValues<WidgetSource>(), drawing => Assert.Contains(drawing, named));
    }

    /// <summary>
    /// The bug itself: the list folder gets a folder island on the bar, opening as a list. Before,
    /// only the grid folder did.
    /// </summary>
    [UiFact]
    public void TheListFolderGetsAFolderIslandOpeningAList()
    {
        OnStaThread(gfx =>
        {
            var config = new Config();
            var widget = Widget(BarKind.FolderList);
            using var island = Karpo.Dock.Islands.Create(Config.IslandKey(null, widget.Id), widget, gfx, config);

            var folder = Assert.IsType<FolderIsland>(island);
            Assert.Equal(BarEntries.Of(BarKind.FolderList).Info, folder.Info);
            Assert.Equal(FolderView.List, folder.View);
        });
    }

    /// <summary>
    /// Making a one-button widget asks Windows nothing; the first look at it does.
    /// <para>
    /// This is a measured regression, not a tidiness rule. SwitchIsland's constructor called
    /// Start, and the mobile hotspot's Start is 8-11 ms of live network-stack queries (60 ms the
    /// first time in a process) — so a bar paid it while drawing, and a hotspot sitting in a
    /// folder nobody had opened paid it while invisible. Every switch kind is checked, because
    /// the fix is the rule for all of them rather than a special case for the expensive one.
    /// </para>
    /// </summary>
    [UiFact]
    public void MakingASwitchWidgetAsksWindowsNothingUntilSomebodyLooks()
    {
        OnStaThread(gfx =>
        {
            var config = new Config();
            var switches = BarEntries.All.Where(entry => entry.Drawing == WidgetSource.Switch).Select(entry => entry.Kind).ToList();
            Assert.NotEmpty(switches);
            foreach (var kind in switches)
            {
                var counted = new Counted();
                QuickSwitches.Source = _ => counted;
                try
                {
                    var widget = Widget(kind);
                    using var island = Karpo.Dock.Islands.Create(Config.IslandKey(null, widget.Id), widget, gfx, config);
                    Assert.NotNull(island);
                    Assert.Equal(0, counted.Starts);
                    // Somebody looks at it: hovering for the tooltip is the cheapest of the three
                    // ways (the other two are being drawn on the bar and being drawn as a line).
                    island.Tooltip(0);
                    Assert.Equal(1, counted.Starts);
                    // And it is started once, not once per look.
                    island.Tooltip(0);
                    island.Tooltip(0);
                    Assert.Equal(1, counted.Starts);
                }
                finally
                {
                    QuickSwitches.Source = null;
                }
            }
        });
    }

    /// <summary>A switch that counts how often it was asked to start following anything.</summary>
    private sealed class Counted : IQuickSwitch
    {
        public int Starts { get; private set; }

        public string Name => "Counted";

        public bool Available => true;

        public bool? On => null;

        public string? Note => null;

        public void Start(Action changed) => Starts++;

        public void Act()
        {
        }

        public void Dispose()
        {
        }
    }

    /// <summary>
    /// stage 3: a widget whose drawing has a shape is really sized by it, wherever it stands. The
    /// layout's answer has to be the shape's cells rounded to the grid — if the island were still
    /// sizing itself, the two would drift and nothing else would say so.
    /// </summary>
    [UiFact]
    public void AConvertedDrawingIsSizedByItsShapeAndNotByItsIsland()
    {
        OnStaThread(gfx =>
        {
            var config = new Config { Height = 48 };
            var style = new IslandStyle(1f, false, Layout.CellPx(96, config), Layout.IconPx(96, config), new Look(Glass: false, Dark: true, Accent: Windows.UI.Color.FromArgb(255, 0, 0x78, 0xD4)));
            foreach (var (name, drawing, fresh, make) in Everywhere().Where(widget => WidgetShapes.Has(widget.Drawing)))
            {
                var widget = fresh();
                using var island = make(widget, gfx, config);

                Assert.True(island is not null, $"{name} has a shape and no island");
                // A component of a kind the taskbar has no entry for — the notification list's
                // shell card — has no length along a bar to be sized by at all
                // (EveryWidgetAnswersTheSizeWhereItStandsNeeds says so of it).
                if (island is CellIsland { CardOnly: true } && BarEntries.Find(island.Info.Id) is null)
                {
                    continue;
                }
                var shape = WidgetShapes.For(drawing)!(new ShapeContext(widget, island.Info, style, gfx));
                var (wanted, _) = CellIsland.Share([.. shape.Cells], shape.Exact, style.Room, style.Thickness);
                var measured = island.Measure(style);

                Assert.Equal(shape.Count, measured.Count);
                for (var index = 0; index < shape.Count; index++)
                {
                    var got = measured.Lengths is { } lengths ? lengths[index] : measured.CellLength;
                    Assert.Equal(wanted[index], got, 3);
                }
            }
        });
    }

    /// <summary>
    /// Several threads measuring text at once do not break the template cache or the tabular
    /// figures they share. The offscreen tests run a class per thread with a device each, and a
    /// widget's shape measures templates, so this really happens.
    /// <para>
    /// <b>What this test is and is not.</b> It cannot prove a race is gone: a data race shows
    /// itself only on some interleavings, so a run that passes says nothing about a broken
    /// implementation. What it does is exercise the thing concurrently and insist the answers
    /// agree — it would have had a fair chance of catching the corrupt dictionary, and on the
    /// fixed code it never fails, which is the only honest thing to write here. The real evidence
    /// that the race is gone is the code: a ConcurrentDictionary and a gate on the one-off.
    /// </para>
    /// </summary>
    [UiFact]
    public void MeasuringTextFromSeveralThreadsAtOnceAgrees()
    {
        const int threads = 6;
        string[] templates = ["88:88", "100%", "WWW", "88h 88m", "-88°", "Cloudy", "Wed 28"];
        var answers = new (float Width, float Height)[threads][];
        var failures = new Exception?[threads];

        var running = new Thread[threads];
        for (var index = 0; index < threads; index++)
        {
            var mine = index;
            running[mine] = new Thread(() =>
            {
                try
                {
                    // Each thread its own device, as the offscreen test classes have.
                    OnStaThread(gfx =>
                    {
                        var got = new (float, float)[templates.Length];
                        // Round and round, so the threads overlap on the same keys rather than
                        // each filling the cache once and never touching it again.
                        for (var pass = 0; pass < 40; pass++)
                        {
                            for (var which = 0; which < templates.Length; which++)
                            {
                                got[which] = TextLine.Template(gfx, templates[which], 14 + (which % 3), Weight.Regular);
                            }
                        }
                        answers[mine] = got;
                    });
                }
                catch (Exception error)
                {
                    failures[mine] = error;
                }
            });
            running[mine].Start();
        }
        foreach (var thread in running)
        {
            thread.Join();
        }

        Assert.All(failures, failure => Assert.Null(failure));
        Assert.All(answers, answer => Assert.Equal(answers[0], answer));
    }

    /// <summary>
    /// stage 5: a kind that says a click opens one of Karpo's flyouts has one, and a flyout is
    /// only ever written for a kind that says so. The descriptor carries the question and the dock
    /// owns the answer (Dock.Widgets.cs), because a flyout is a dock type and the registry is
    /// shared with the settings app — so this is what holds the two halves together.
    /// </summary>
    public static TheoryData<BarKind> Placeable()
    {
        var data = new TheoryData<BarKind>();
        foreach (var entry in BarEntries.All)
        {
            data.Add(entry.Kind);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(Placeable))]
    public void AKindThatOpensAFlyoutHasOne(BarKind kind)
    {
        var entry = BarEntries.Of(kind);
        var has = Karpo.Dock.Dock.HasFlyout(kind);

        if (has)
        {
            Assert.True(
                entry.Click == BarClick.Flyout,
                $"{kind} has a flyout in Dock.Widgets.cs but its entry says a click {entry.Click}");
            return;
        }
        // The other way round is not an equivalence: an entry whose click opens the generic list of
        // items (the keyboard's layouts, the tray's own menus) says Flyout and has no row of its
        // own, because the dock shows those from the action itself.
        Assert.True(
            entry.Click != BarClick.Flyout || Generic.Contains(kind),
            $"{kind}'s entry says a click opens a flyout, and Dock.Widgets.cs names none");
    }

    /// <summary>The entries whose flyout is the dock's generic list of items, built from the click itself.</summary>
    private static readonly BarKind[] Generic = [BarKind.Tray];

    /// <summary>
    /// Every widget that can be drawn as the bar's cells answers <b>one of the two sizing routes</b>:
    /// a <see cref="WidgetShapes"/> entry for its drawing, or its own <c>Length</c>. That is every
    /// taskbar entry with an island. One that answers neither falls between them and has no size at all
    /// — it draws nowhere, and until now it said so only when somebody enumerated the kinds by
    /// hand.
    /// <para>
    /// This is the pure half, so it fails on the day a widget is added without a shape rather than
    /// on the day a device is there to measure it.
    /// <see cref="EveryWidgetAnswersTheSizeWhereItStandsNeeds"/> is the other half: that the route
    /// really answers, and that a component's refusal names what it is.
    /// </para>
    /// </summary>
    [Fact]
    public void EveryWidgetDrawnAsCellsHasOneOfTheTwoSizingRoutes()
    {
        var missing = BarEntries.All.Where(entry => entry.Drawing != WidgetSource.None).Select(entry => (Name: $"taskbar {entry.Kind}", entry.Drawing))
            .Where(widget => !WidgetShapes.Has(widget.Drawing) && !SizesItself.Contains(widget.Drawing))
            .Select(widget => widget.Name)
            .ToArray();

        Assert.True(
            missing.Length == 0,
            $"{string.Join(", ", missing)} can be drawn as the bar's cells and answer neither WidgetShapes nor a Length of their own. "
                + "Add a shape in WidgetShapes.Converted, or name the drawing in SizesItself if its island really measures itself.");
    }

    /// <summary>
    /// The drawings whose island still measures itself, rather than having a shape. Naming them is
    /// the point: the list is meant to shrink, and a drawing joining it is a decision somebody made
    /// on purpose instead of a gap nobody noticed (docs\widgets.md §4, stage 3).
    /// </summary>
    private static readonly WidgetSource[] SizesItself =
    [
        WidgetSource.Markets,
        // The status icons: a glyph square a cell, and as many cells as there are icons that are so — a
        // count that is live by design, as a widget with nothing to show takes itself off the bar.
        // A shape is one list of cells whatever is shown, so it cannot say this; every cell is the same
        // square, which is what its Length gives (IndicatorsIsland).
        WidgetSource.Indicators,
    ];

    /// <summary>
    /// A widget answers the size where it stands needs, and nothing else.
    /// <list type="bullet">
    /// <item>On the taskbar, <c>Measure</c> gives a finite, positive length for every cell.</item>
    /// <item>A component — a line of a widget the taskbar does not have — has no length along a
    /// bar at all, and asking says <b>that</b>, rather than sending somebody to look for a missing
    /// row in the shape table. It declares its size in the folder's rows instead
    /// (<see cref="ListWidget.Rows"/>), which is the only size it has.</item>
    /// </list>
    /// <para>
    /// The hole this closes: <c>ScreenTimeDays</c> claimed neither route, so asking for its size
    /// threw "sized by WidgetShapes, not by its island" — an answer that was false twice over.
    /// Every card-only component was in the same position: the weather's three, the notification
    /// list, the timer and the focus session.
    /// </para>
    /// </summary>
    [UiFact]
    public void EveryWidgetAnswersTheSizeWhereItStandsNeeds()
    {
        OnStaThread(gfx =>
        {
            var config = new Config { Height = 48 };
            var style = new IslandStyle(1f, false, Layout.CellPx(96, config), Layout.IconPx(96, config), new Look(Glass: false, Dark: true, Accent: Windows.UI.Color.FromArgb(255, 0, 0x78, 0xD4)));
            foreach (var entry in BarEntries.All.Where(entry => entry.Drawing != WidgetSource.None))
            {
                var widget = Widget(entry.Kind);
                using var island = Karpo.Dock.Islands.Create(Config.IslandKey(null, widget.Id), widget, gfx, config)!;
                var cells = island.Measure(style);
                Assert.True(cells.Count >= 0, $"{entry.Kind} says it has {cells.Count} cells");
                for (var index = 0; index < cells.Count; index++)
                {
                    var length = cells.Lengths is { } lengths ? lengths[index] : cells.CellLength;
                    Assert.True(
                        float.IsFinite(length) && length > 0,
                        $"{entry.Kind}'s cell {index} measures {length}, which is not a length");
                }
            }

            // The clock has no entry since it became a folder's face, but its line still draws its
            // small form (ClockTile): not a component.
            foreach (var line in ListWidgets.All.Where(line => BarEntries.Find(line.Info.Id) is null && line.Kind != ListKind.Clock))
            {
                Assert.True(line.Cells >= 1, $"{line.Kind} is a component and declares a line of {line.Cells} rows");
                var widget = line.Kind.New();
                using var island = Karpo.Dock.Islands.CreateLine(Config.IslandKey(null, widget.Id), widget, line.Kind, gfx, config)!;
                RefusesABarLength(island, style, $"line {line.Kind}");
            }
        });
    }

    /// <summary>
    /// A component asked for a bar length refuses in those words, or there is nothing to refuse
    /// because the widget has no cells at all just now (a timer nobody set). What it may never do
    /// is claim a shape table has the answer, which is what sent somebody looking for a row that
    /// was never going to be there.
    /// </summary>
    private static void RefusesABarLength(IIsland island, IslandStyle style, string name)
    {
        var refused = Record.Exception(() => island.Measure(style));
        if (refused is not null)
        {
            Assert.Contains("component", refused.Message, StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            Assert.True(island.Measure(style).Count == 0, $"{name} is a component and answered a bar length");
        }
    }

    /// <summary>
    /// Every widget each registry names, with the drawing it names, a new widget of it and the
    /// factory that makes its island there: the taskbar's entries and a list's lines.
    /// </summary>
    private static IEnumerable<(string Name, WidgetSource Drawing, Func<WidgetItem> New, Func<WidgetItem, Gfx, Config, IIsland?> Make)> Everywhere() =>
        BarEntries.All
            .Select(entry => (
                $"taskbar {entry.Kind}",
                entry.Drawing,
                (Func<WidgetItem>)(() => entry.Kind.New()),
                (Func<WidgetItem, Gfx, Config, IIsland?>)((widget, gfx, config) =>
                    Karpo.Dock.Islands.Create(Config.IslandKey(null, widget.Id), (BarItem)widget, gfx, config))))
            .Concat(ListWidgets.All.Select(line => (
                $"line {line.Kind}",
                line.Drawing,
                (Func<WidgetItem>)(() => line.Kind.New()),
                (Func<WidgetItem, Gfx, Config, IIsland?>)((widget, gfx, config) =>
                    Karpo.Dock.Islands.CreateLine(Config.IslandKey(null, widget.Id), (CellItem)widget, line.Kind, gfx, config)))));

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
