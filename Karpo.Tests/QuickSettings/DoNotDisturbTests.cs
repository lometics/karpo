// Do not disturb as a service (Karpo.Dock\QuickSettings\Services\DoNotDisturb.cs), always on a
// made-up source and never on Windows' own: what it shows before Windows has answered, a switch
// shown at once and then replaced by what Windows did, news from another thread raised on the
// thread that uses the service, the one-button widget that stands for it, and that a test process
// is given a source that reaches nothing at all.

using System.Collections.Concurrent;
using Karpo.Dock.Native;
using Karpo.Dock.QuickSettings.Services;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
using IDispatcherQueueController = TerraFX.Interop.WinRT.IDispatcherQueueController;

namespace Karpo.Tests.QuickSettings;

/// <summary>
/// A do not disturb whose answers the test gives: it records what it was asked to switch and says
/// nothing until <see cref="Answer"/>, as Windows says nothing until its service has.
/// </summary>
internal sealed class FakeQuiet : IDoNotDisturbSource
{
    private Action? changed;

    public DndState State { get; private set; } = DndState.Unknown;

    public int Starts { get; private set; }

    public List<bool> Asked { get; } = [];

    public void Start(Action onChanged)
    {
        Starts++;
        changed = onChanged;
    }

    public DndState Read() => State;

    public void Set(bool on) => Asked.Add(on);

    /// <summary>Windows says <paramref name="state"/>, on whatever thread calls this.</summary>
    public void Answer(DndState state)
    {
        State = state;
        changed?.Invoke();
    }

    public void Dispose()
    {
    }
}

public sealed class DoNotDisturbTests
{
    private static readonly TimeSpan Patience = Karpo.Tests.Patience.Ceiling;

    /// <summary>
    /// Blank, never wrong (docs\widgets.md §11): nothing is known until Windows answers, and making
    /// the service asks it nothing — the first look does, once.
    /// </summary>
    [Fact]
    public void NothingIsShownUntilWindowsAnswers()
    {
        var windows = new FakeQuiet();
        var quiet = new DoNotDisturb(windows);
        var raised = 0;
        quiet.Changed += () => raised++;

        Assert.Equal(0, windows.Starts);
        Assert.Null(quiet.On);

        quiet.Look();
        quiet.Look();

        Assert.Equal(1, windows.Starts);
        Assert.Null(quiet.On);
        Assert.Equal(0, raised);

        windows.Answer(new DndState(On: false));

        Assert.False(quiet.On);
        Assert.Equal(1, raised);
    }

    /// <summary>
    /// A click shows its answer under the pointer, before the round trip to Windows' service is
    /// over; and what Windows then says is what stands — including a refusal, which leaves it where
    /// it was and says a click will open Windows' settings instead.
    /// </summary>
    [Fact]
    public void ASwitchShowsAtOnceAndWindowsHasTheLastWord()
    {
        var windows = new FakeQuiet();
        var quiet = new DoNotDisturb(windows);
        var raised = 0;
        quiet.Changed += () => raised++;
        quiet.Look();
        windows.Answer(new DndState(On: false));

        quiet.Set(true);

        Assert.Equal([true], windows.Asked);
        Assert.True(quiet.On);
        Assert.Equal(2, raised);

        windows.Answer(new DndState(On: true));
        Assert.True(quiet.On);

        quiet.Set(false);
        Assert.False(quiet.On);
        Assert.Equal([true, false], windows.Asked);

        // Windows would not: it stays on, and the widget will open Windows' settings instead.
        windows.Answer(new DndState(On: true, CanSwitch: false));
        Assert.True(quiet.On);
        Assert.False(quiet.State.CanSwitch);
    }

    /// <summary>
    /// A switch in flight is the person's own choice, so it is not shown as a rule's while it waits,
    /// and a rule's "on" is what Windows says it is.
    /// </summary>
    [Fact]
    public void AWishIsNeverShownAsARules()
    {
        var windows = new FakeQuiet();
        var quiet = new DoNotDisturb(windows);
        quiet.Look();
        windows.Answer(new DndState(On: true, Automatic: true));
        Assert.True(quiet.State.Automatic);

        quiet.Set(true);

        Assert.True(quiet.On);
        Assert.False(quiet.State.Automatic);
    }

    /// <summary>
    /// notifications: on for a time (a menu's "for 1 hour"): on at once, and off again when the time is
    /// up — once, and only that switch; Karpo's own clock, for Windows has no "for an hour" of its own.
    /// </summary>
    [Fact]
    public void OnForATimeTurnsItselfOffAtItsEnd()
    {
        var windows = new FakeQuiet();
        var clock = new TestClock();
        var quiet = new DoNotDisturb(windows, clock);
        quiet.Look();
        windows.Answer(new DndState(On: false));

        quiet.TurnOnUntil(quiet.InAnHour());
        windows.Answer(new DndState(On: true));
        Assert.True(quiet.On);
        Assert.Equal(clock.Now.AddHours(1), quiet.Until);

        clock.Advance(TimeSpan.FromMinutes(59));
        Assert.Equal([true], windows.Asked);
        clock.Advance(TimeSpan.FromMinutes(2));
        Assert.Equal([true, false], windows.Asked);
        Assert.Null(quiet.Until);
        clock.Advance(TimeSpan.FromHours(5));
        Assert.Equal([true, false], windows.Asked);
    }

    /// <summary>
    /// A time it was on for ends with any other say: switched by hand in Karpo, turned off in Windows'
    /// own notification center, or held by a rule of Windows' at its end — then the rule's on stands.
    /// </summary>
    [Fact]
    public void ATimeEndsWithAnyOtherSay()
    {
        var windows = new FakeQuiet();
        var clock = new TestClock();
        var quiet = new DoNotDisturb(windows, clock);
        quiet.Look();
        windows.Answer(new DndState(On: false));

        quiet.TurnOnUntil(quiet.InAnHour());
        quiet.Set(true);
        Assert.Null(quiet.Until);
        clock.Advance(TimeSpan.FromHours(2));
        Assert.Equal([true, true], windows.Asked);

        quiet.TurnOnUntil(quiet.InAnHour());
        windows.Answer(new DndState(On: false));
        Assert.Null(quiet.Until);
        clock.Advance(TimeSpan.FromHours(2));
        Assert.Equal([true, true, true], windows.Asked);

        quiet.TurnOnUntil(quiet.InAnHour());
        windows.Answer(new DndState(On: true, Automatic: true));
        clock.Advance(TimeSpan.FromHours(2));
        Assert.Equal([true, true, true, true], windows.Asked);
        Assert.Null(quiet.Until);
        Assert.True(quiet.On);
    }

    /// <summary>"Until tomorrow" ends where tomorrow starts, here: at midnight, a clock change's day too.</summary>
    [Fact]
    public void TomorrowStartsAtMidnightHere()
    {
        var zurich = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");
        Assert.Equal(new DateTimeOffset(2026, 10, 24, 0, 0, 0, TimeSpan.FromHours(2)), DoNotDisturb.Tomorrow(new DateTimeOffset(2026, 10, 23, 21, 30, 0, TimeSpan.FromHours(2)), zurich));
        // Late at night in UTC is the next day already in Zurich: the day after that.
        Assert.Equal(new DateTimeOffset(2026, 10, 25, 0, 0, 0, TimeSpan.FromHours(2)), DoNotDisturb.Tomorrow(new DateTimeOffset(2026, 10, 23, 23, 30, 0, TimeSpan.Zero), zurich));
        // The night the clocks go back: the next midnight in winter time.
        Assert.Equal(new DateTimeOffset(2026, 10, 26, 0, 0, 0, TimeSpan.FromHours(1)), DoNotDisturb.Tomorrow(new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.FromHours(1)), zurich));
    }

    /// <summary>
    /// The rows a notification menu has for it, by what Windows says: off (or not known yet), on now,
    /// for an hour, until tomorrow; on, off — saying till when where it is on for a time; none where
    /// Windows won't let Karpo switch it. Each switches the menu's service as it says.
    /// </summary>
    [Fact]
    public void DoNotDisturbRowsFollowItsState()
    {
        var windows = new FakeQuiet();
        var quiet = new DoNotDisturb(windows);
        var bell = new Bell(quiet);
        string[] Texts() => [.. Karpo.Dock.Dock.DoNotDisturbRows(quiet).Select(row => row.Text)];

        Assert.Equal(["Turn do not disturb on", "Turn on for 1 hour", "Turn on until tomorrow"], Texts());
        quiet.Look();
        windows.Answer(new DndState(On: false));
        Assert.Equal(["Turn do not disturb on", "Turn on for 1 hour", "Turn on until tomorrow"], Texts());

        var (items, acts) = Karpo.Dock.Dock.ActRows([Karpo.Dock.WidgetAct.DoNotDisturb], bell);
        Assert.All(items, item => Assert.False(item.Stays));
        var tomorrow = acts[items.FindIndex(item => item.Text == "Turn on until tomorrow")];
        Karpo.Dock.Dock.RunAct(tomorrow, bell, (_, _) => throw new InvalidOperationException("nothing is opened"));
        Assert.Equal(quiet.Tomorrow(), quiet.Until);
        windows.Answer(new DndState(On: true));

        var on = Karpo.Dock.Dock.DoNotDisturbRows(quiet);
        Assert.Equal(["Turn do not disturb off"], on.Select(row => row.Text));
        Assert.Equal(quiet.Until, on[0].Until);
        (items, acts) = Karpo.Dock.Dock.ActRows([Karpo.Dock.WidgetAct.DoNotDisturb], bell);
        Assert.StartsWith("Until ", Assert.Single(items).Detail);
        Karpo.Dock.Dock.RunAct(acts[0], bell, (_, _) => { });
        Assert.Equal([true, false], windows.Asked);
        Assert.Null(quiet.Until);

        // No widget to ask, as a menu at rest: no switch, as no other control there (§308).
        Assert.Empty(Karpo.Dock.Dock.ActRows([Karpo.Dock.WidgetAct.DoNotDisturb], widget: null).Items);

        windows.Answer(new DndState(On: false, CanSwitch: false));
        Assert.Empty(Texts());
    }

    /// <summary>A notification widget whose do not disturb is the test's: all a menu asks of it.</summary>
    private sealed class Bell(DoNotDisturb quiet) : Karpo.Dock.IIsland, Karpo.Dock.INotificationActs
    {
        public bool HasAny => false;

        public DoNotDisturb Dnd => quiet;

        public void ClearAll() { }

        public string Key => "bell";

        public Karpo.Core.WidgetInfo Info => throw new NotSupportedException();

        public int IntervalMs => 0;

        public void Attach(nint dock, nuint token) => throw new NotSupportedException();

        public void Configure(Karpo.Core.WidgetItem island, Karpo.Core.Config config) => throw new NotSupportedException();

        public Karpo.Dock.IslandCells Measure(Karpo.Dock.IslandStyle style) => throw new NotSupportedException();

        public Windows.UI.Composition.CompositionDrawingSurface Surface(int index, Karpo.Dock.IslandStyle style) => throw new NotSupportedException();

        public IReadOnlyList<Karpo.Dock.IslandLayer> Layers(int index, Karpo.Dock.IslandStyle style) => throw new NotSupportedException();

        public void Visible(bool shown) { }

        public bool Tick(Karpo.Dock.Gfx gfx) => false;

        public bool Refresh(Karpo.Dock.Gfx gfx) => false;

        public bool MinuteChanged(Karpo.Dock.Gfx gfx) => false;

        public void System(uint msg, WPARAM wParam, LPARAM lParam) { }

        public string? Tooltip(int index) => null;

        public Karpo.Dock.IslandAction Click(int index) => throw new NotSupportedException();

        public void Invoke(int index, uint id) { }

        public void Forget() { }

        public void Dispose() { }
    }

    /// <summary>A toggle turns off what is on, and on what is off or not known yet.</summary>
    [Fact]
    public void AToggleTurnsOnWhatNobodyKnowsYet()
    {
        var windows = new FakeQuiet();
        var quiet = new DoNotDisturb(windows);

        quiet.Toggle();
        Assert.Equal([true], windows.Asked);

        windows.Answer(new DndState(On: true));
        quiet.Toggle();
        Assert.Equal([true, false], windows.Asked);

        windows.Answer(new DndState(On: false));
        quiet.Toggle();
        Assert.Equal([true, false, true], windows.Asked);
    }

    // --- Edit mode's dim -----------------------------------------------------------------------
    //
    // Edit mode's dim is a window over the whole of every display, and Windows' rule "When using an
    // app in full-screen mode" takes it for one: do not disturb came on by itself for as long as
    // edit mode lasted, and the status icons showed it (the person, 2026-10-08: "in edit mode the
    // status icons widget always shows the do not disturb icon even when do not disturb is off").

    /// <summary>
    /// Off as the dim comes, a rule's "on" while it stands is shown off; the person's own "on" is
    /// shown, from the click on.
    /// </summary>
    [Fact]
    public void ARuleTheDimBringsIsNotShown()
    {
        var windows = new FakeQuiet();
        var quiet = new DoNotDisturb(windows);
        quiet.Look();
        windows.Answer(new DndState(On: false));

        quiet.Dimmed(true);
        windows.Answer(new DndState(On: true, Automatic: true));
        Assert.False(quiet.On);

        quiet.Set(true);
        Assert.True(quiet.On);
        windows.Answer(new DndState(On: true));
        Assert.True(quiet.On);
    }

    /// <summary>
    /// After the dim has gone, its rule is still not shown until Windows next speaks, as the rule
    /// lets go a moment later; what Windows says then stands, a rule's "on" included.
    /// </summary>
    [Fact]
    public void AfterTheDimWindowsHasTheWordAgain()
    {
        var windows = new FakeQuiet();
        var quiet = new DoNotDisturb(windows);
        quiet.Look();
        windows.Answer(new DndState(On: false));
        quiet.Dimmed(true);
        windows.Answer(new DndState(On: true, Automatic: true));

        quiet.Dimmed(false);
        Assert.False(quiet.On);

        windows.Answer(new DndState(On: false));
        Assert.False(quiet.On);
        // A rule after edit mode, the schedule's say, is shown.
        windows.Answer(new DndState(On: true, Automatic: true));
        Assert.True(quiet.On);
    }

    /// <summary>On before the dim came, by a rule or by the person, it is shown on all through it.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void OnBeforeTheDimStaysOn(bool automatic)
    {
        var windows = new FakeQuiet();
        var quiet = new DoNotDisturb(windows);
        quiet.Look();
        windows.Answer(new DndState(On: true, Automatic: automatic));
        var raised = 0;
        quiet.Changed += () => raised++;

        quiet.Dimmed(true);

        Assert.True(quiet.On);
        Assert.Equal(0, raised);
        quiet.Dimmed(false);
        Assert.True(quiet.On);
    }

    /// <summary>Only a rule's "on" is taken out; the person's, and every "off", are what Windows says.</summary>
    [Fact]
    public void UndimmedTakesOutOnlyARulesOn()
    {
        Assert.Equal(new DndState(On: false), DoNotDisturb.Undimmed(new DndState(On: true, AlarmsOnly: true, Automatic: true)));
        Assert.Equal(new DndState(On: true), DoNotDisturb.Undimmed(new DndState(On: true)));
        Assert.Equal(new DndState(On: false), DoNotDisturb.Undimmed(new DndState(On: false)));
        Assert.Equal(DndState.Unknown, DoNotDisturb.Undimmed(DndState.Unknown));
    }

    /// <summary>
    /// Windows answers on a thread of its own, and the service raises its news on the thread that
    /// uses it, through that thread's dispatcher queue: every handler — the bell, the list's header,
    /// the widget — runs where the dock draws.
    /// </summary>
    [Fact]
    public void NewsFromAnotherThreadIsRaisedOnTheOneThatUsesIt()
    {
        var raisedOn = new ConcurrentBag<int>();
        var user = 0;
        var worker = Sta(() =>
        {
            user = Environment.CurrentManagedThreadId;
            var windows = new FakeQuiet();
            var quiet = new DoNotDisturb(windows);
            quiet.Changed += () => raisedOn.Add(Environment.CurrentManagedThreadId);
            quiet.Look();
            var elsewhere = new Thread(() => windows.Answer(new DndState(On: true)));
            elsewhere.Start();
            elsewhere.Join();
            // Queued, not raised on Windows' thread: nothing has run here yet.
            Assert.Empty(raisedOn);
            Pump(() => !raisedOn.IsEmpty, Patience);
            Assert.True(quiet.On);
        });
        worker.Join();
        worker.Rethrow();

        Assert.Equal([user], raisedOn.ToArray());
    }

    /// <summary>
    /// A service belongs to the thread that first used it, and says so at the call from any other,
    /// rather than raising a handler on the wrong thread three calls later.
    /// </summary>
    [Fact]
    public void AnotherThreadIsRefused()
    {
        var quiet = new DoNotDisturb(new FakeQuiet());
        quiet.Look();
        Exception? refused = null;

        var other = new Thread(() => refused = Record.Exception(() => quiet.Set(true)));
        other.Start();
        other.Join();

        Assert.IsType<InvalidOperationException>(refused);
    }

    /// <summary>
    /// The guarantee is in the service and not in its callers: a test process is an isolated run,
    /// and the service it is given reaches nothing of Windows' — so no picture, no flyout run and no
    /// click in any test can turn on the person's do not disturb.
    /// </summary>
    [Fact]
    public void ATestProcessNeverReachesWindowsOwn()
    {
        Assert.True(Config.IsIsolated);
        Exception? failed = null;
        var thread = new Thread(() => failed = Record.Exception(() =>
        {
            var quiet = DoNotDisturb.Shared;
            Assert.False(quiet.ReachesWindows);
            quiet.Look();
            quiet.Toggle();
            Assert.True(quiet.On);
        }));
        thread.Start();
        thread.Join();
        Assert.Null(failed);
    }

    /// <summary>
    /// The one-button widget has no state of its own: it reads the service, switches the service,
    /// and follows it from its first look, so the widget, the bell and the list never disagree.
    /// </summary>
    [Fact]
    public void TheWidgetIsTheServicesSwitch()
    {
        var windows = new FakeQuiet();
        var quiet = new DoNotDisturb(windows);
        using var button = new DoNotDisturbSwitch("Do not disturb", quiet);
        var heard = 0;

        Assert.Equal(0, windows.Starts);
        Assert.Null(button.On);

        button.Start(() => heard++);
        Assert.Equal(1, windows.Starts);
        windows.Answer(new DndState(On: false));
        Assert.False(button.On);
        Assert.Equal(1, heard);

        button.Act();
        Assert.Equal([true], windows.Asked);
        Assert.True(button.On);

        // Switched anywhere else — the list's header, a focus session — the widget hears it.
        quiet.Set(false);
        Assert.False(button.On);
        Assert.True(heard >= 3);

        button.Dispose();
        var before = heard;
        windows.Answer(new DndState(On: true));
        Assert.Equal(before, heard);
    }

    /// <summary>What the tooltip and a card say under the name: Windows' own sense of it, never a state made up.</summary>
    [Fact]
    public void TheWordsSayWhatGetsThrough()
    {
        Assert.Equal("Only priority notifications and alarms show banners.", DoNotDisturb.Note(new DndState(On: false)));
        Assert.Equal("Only priority notifications and alarms show banners.", DoNotDisturb.Note(new DndState(On: true)));
        Assert.Equal("Only alarms show banners.", DoNotDisturb.Note(new DndState(On: true, AlarmsOnly: true)));
        Assert.Equal("Turned on by one of your rules; a click turns it off until the next time.", DoNotDisturb.Note(new DndState(On: true, Automatic: true)));
        Assert.Equal("Windows didn't let Karpo switch it; a click opens its settings.", DoNotDisturb.Note(new DndState(On: true, CanSwitch: false)));
        Assert.Equal("Do not disturb: on\nOnly priority notifications and alarms show banners.",
            Karpo.Dock.SwitchIsland.Say("Do not disturb", true, true, DoNotDisturb.Note(new DndState(On: true))));
        Assert.Equal("Do not disturb", Karpo.Dock.SwitchIsland.Say("Do not disturb", true, null, null));
    }

    // --- A thread with a dispatcher queue -------------------------------------------------------

    private static unsafe Worker Sta(Action body)
    {
        var worker = new Worker();
        worker.Thread = new Thread(() =>
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
                    body();
                }
                finally
                {
                    controller->Release();
                }
            }
            catch (Exception error)
            {
                worker.Failed = error;
            }
        });
        worker.Thread.SetApartmentState(ApartmentState.STA);
        worker.Thread.Start();
        return worker;
    }

    private static unsafe void Pump(Func<bool> done, TimeSpan patience)
    {
        var until = DateTime.UtcNow + patience;
        while (!done() && DateTime.UtcNow < until)
        {
            MSG message;
            while (PeekMessageW(&message, HWND.NULL, 0, 0, PM.PM_REMOVE))
            {
                _ = TranslateMessage(&message);
                _ = DispatchMessageW(&message);
            }
            Thread.Sleep(1);
        }
    }

    private sealed class Worker
    {
        public Thread Thread { get; set; } = null!;

        public Exception? Failed { get; set; }

        public void Join() => Thread.Join();

        public void Rethrow()
        {
            if (Failed is not null)
            {
                throw new Xunit.Sdk.XunitException("On the service's thread: " + Failed);
            }
        }
    }
}
