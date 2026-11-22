// preview: when the window list opens and closes by pointing (Karpo.Dock\PreviewHover.cs), against a
// clock of the test's own instead of the dock's window timers.
//
// "when set windows previews on hover they have a little delay when i hover over them until they
// appear. this delay should be made as short as possible." The list waited 420 ms, about Windows'
// hover time, before anything showed. It waits only a guard now, long enough that a pointer crossing
// the bar opens nothing; with a list up, the next button's comes at once; and the list still stays
// while the pointer crosses from its button up into it.
//
// "sometimes the window preview pops up and does not go away again although i am not there with my
// mouse anymore": a leave no message told of (no WM_MOUSELEAVE, a bar still counting the pointer on
// the button) kept it up. While a list is up a watch looks at where the pointer really is.
//
// Pure: no windows, no compositor, no real time.

using Karpo.Dock;

namespace Karpo.Tests.Signals;

public sealed class PreviewHoverTests
{
    /// <summary>The timers, going off when the test's clock says, as the dock's SetTimer ones would.</summary>
    private sealed class Clock : IPreviewTimers
    {
        private readonly Dictionary<PreviewTimer, long> due = [];

        public long Now { get; private set; }

        public void Arm(PreviewTimer timer, uint ms) => due[timer] = Now + ms;

        public void Kill(PreviewTimer timer) => due.Remove(timer);

        public bool Armed(PreviewTimer timer) => due.ContainsKey(timer);

        /// <summary>One ms on: the timers due by then, each once, the earliest first.</summary>
        public List<PreviewTimer> Tick()
        {
            Now++;
            var fired = due.Where(pair => pair.Value <= Now).OrderBy(pair => pair.Value).Select(pair => pair.Key).ToList();
            foreach (var timer in fired)
            {
                due.Remove(timer);
            }
            return fired;
        }
    }

    /// <summary>
    /// A bar as the dock drives the rules: where the pointer is, and the list it opens when the open
    /// timer goes off and closes when the grace runs out with the pointer neither in the list nor on
    /// its button (Dock.DockWindow's HoverChanged, Leave, PreviewEntered, PreviewLeft and Timer).
    /// </summary>
    private sealed class Bar
    {
        public Bar() => Rules = new PreviewHover(Clock);

        public Clock Clock { get; } = new();

        public PreviewHover Rules { get; }

        /// <summary>The tile whose list is up, null when none is.</summary>
        public int? Open { get; private set; }

        /// <summary>How many times a list was shown, a swap included.</summary>
        public int Shown { get; private set; }

        private int? onButton;
        private bool inList;

        // The pointer went where no message told of: the cursor is away from the list and the button.
        private bool gone;

        /// <summary>Where the cursor really is, as the dock's watch reads it (DockWindow.PointerHome).</summary>
        private bool Home => !gone && (inList || (onButton is { } on && on == Open));

        /// <summary>The pointer onto an app's button with windows open, or (null) onto anything else on the bar.</summary>
        public void Point(int? tile)
        {
            onButton = tile;
            inList = false;
            gone = false;
            if (tile is not { } over)
            {
                Rules.Off(Open is not null);
            }
            else if (Rules.OnButton(over, Open))
            {
                Show(over);
            }
        }

        /// <summary>The pointer off the bar and into the list, across the gap between them.</summary>
        public void IntoList()
        {
            Point(null);
            inList = true;
            gone = false;
            Rules.Entered();
        }

        /// <summary>The pointer out of the list, somewhere that is neither the list nor the bar.</summary>
        public void OutOfList()
        {
            inList = false;
            Rules.LeftList();
        }

        /// <summary>
        /// The pointer away from the list and its button with no message saying so: no
        /// WM_MOUSELEAVE, and the bar or the list still counting it where it was.
        /// </summary>
        public void Vanish() => gone = true;

        /// <summary>The open list laid out again (its windows renamed, its app's signals changed), as the dock does.</summary>
        public void ShowAgain()
        {
            if (Open is { } open)
            {
                Show(open);
            }
        }

        /// <summary><paramref name="ms"/> go by, with the timers going off as they come due.</summary>
        public void Wait(long ms)
        {
            for (var step = 0; step < ms; step++)
            {
                foreach (var timer in Clock.Tick())
                {
                    if (timer == PreviewTimer.Open && onButton is { } rested)
                    {
                        Show(rested);
                    }
                    else if (timer == PreviewTimer.Close && Open is { } open && !PreviewHover.Stays(inList, onButton == open))
                    {
                        Close();
                    }
                    else if (timer == PreviewTimer.Watch && Rules.Watched(Open is not null, Open is not null && Home))
                    {
                        Close();
                    }
                }
            }
        }

        private void Show(int tile)
        {
            Open = tile;
            Shown++;
            Rules.Shown();
        }

        /// <summary>The list goes, and its timers with it (Dock.ClosePreview).</summary>
        private void Close()
        {
            Open = null;
            Rules.Stop();
        }
    }

    [Fact]
    public void The_wait_before_a_list_opens_is_a_short_guard_not_Windows_hover_time()
    {
        // Long enough to outlast a pointer passing over a button, short enough to read as instant;
        // never the 400 ms of Windows' hover time again, nor the 420 it was.
        Assert.InRange(PreviewHover.OpenMs, 50u, 100u);
        Assert.True(PreviewHover.OpenMs < WidgetHover.OpenMs);

        var bar = new Bar();
        bar.Point(3);
        bar.Wait(PreviewHover.OpenMs - 1);
        Assert.Null(bar.Open);
        bar.Wait(1);
        Assert.Equal(3, bar.Open);
        Assert.Equal(1, bar.Shown);
    }

    [Fact]
    public void Moving_to_the_next_button_while_a_list_is_up_swaps_it_at_once()
    {
        var bar = new Bar();
        bar.Point(1);
        bar.Wait(PreviewHover.OpenMs);
        Assert.Equal(1, bar.Open);

        // No wait at all: the next button's list is there the moment the pointer is.
        bar.Point(2);
        Assert.Equal(2, bar.Open);
        Assert.False(bar.Clock.Armed(PreviewTimer.Open));
        Assert.False(bar.Clock.Armed(PreviewTimer.Close));

        // Across a gap between two buttons as well: the list waits out the gap, not the guard.
        bar.Point(null);
        Assert.True(bar.Clock.Armed(PreviewTimer.Close));
        bar.Wait(20);
        bar.Point(3);
        Assert.Equal(3, bar.Open);
        Assert.False(bar.Clock.Armed(PreviewTimer.Close));

        // Back on the button whose list is up: nothing shows again.
        var shown = bar.Shown;
        bar.Point(3);
        Assert.Equal(shown, bar.Shown);
    }

    [Fact]
    public void A_pointer_passing_over_the_buttons_opens_nothing()
    {
        var bar = new Bar();
        // Across three buttons on the way to the tray, each crossed in less than the guard.
        var crossing = PreviewHover.OpenMs / 2;
        bar.Point(1);
        bar.Wait(crossing);
        bar.Point(2);
        bar.Wait(crossing);
        bar.Point(3);
        bar.Wait(crossing);
        bar.Point(null);
        bar.Wait(1000);
        Assert.Null(bar.Open);
        Assert.Equal(0, bar.Shown);
        Assert.False(bar.Clock.Armed(PreviewTimer.Open));
    }

    [Fact]
    public void The_list_waits_out_the_grace_after_the_pointer_leaves()
    {
        var bar = new Bar();
        bar.Point(1);
        bar.Wait(PreviewHover.OpenMs);
        Assert.Equal(1, bar.Open);

        // Off the button and up into the list: the gap is crossed inside the grace, and it stays,
        // however long the pointer rests in it.
        bar.Point(null);
        bar.Wait(PreviewHover.CloseMs - 1);
        Assert.Equal(1, bar.Open);
        bar.IntoList();
        bar.Wait(5000);
        Assert.Equal(1, bar.Open);

        // Out of the list and away: it goes once the grace is up, not before.
        bar.OutOfList();
        bar.Wait(PreviewHover.CloseMs - 1);
        Assert.Equal(1, bar.Open);
        bar.Wait(1);
        Assert.Null(bar.Open);
    }

    [Fact]
    public void Back_on_its_button_within_the_grace_the_list_stays()
    {
        var bar = new Bar();
        bar.Point(1);
        bar.Wait(PreviewHover.OpenMs);
        bar.Point(null);
        bar.Wait(PreviewHover.CloseMs / 2);
        bar.Point(1);
        bar.Wait(5000);
        Assert.Equal(1, bar.Open);
        Assert.Equal(1, bar.Shown);
    }

    [Fact]
    public void It_stays_only_in_the_list_or_on_its_own_button()
    {
        Assert.True(PreviewHover.Stays(insideList: true, onItsButton: false));
        Assert.True(PreviewHover.Stays(insideList: false, onItsButton: true));
        Assert.False(PreviewHover.Stays(insideList: false, onItsButton: false));
        // The grace outlasts the guard: what took a moment to open never goes quicker than it came.
        Assert.True(PreviewHover.CloseMs > PreviewHover.OpenMs);
    }

    [Fact]
    public void Stopping_leaves_nothing_waiting()
    {
        var bar = new Bar();
        bar.Point(1);
        bar.Rules.Stop();
        Assert.False(bar.Clock.Armed(PreviewTimer.Open));
        bar.Wait(1000);
        Assert.Null(bar.Open);
    }

    [Fact]
    public void A_leave_no_message_told_of_still_closes_the_list()
    {
        var bar = new Bar();
        bar.Point(1);
        bar.Wait(PreviewHover.OpenMs);
        Assert.Equal(1, bar.Open);
        Assert.True(bar.Clock.Armed(PreviewTimer.Watch));

        // The pointer goes straight off to another window: no WM_MOUSELEAVE, and the bar still
        // counts it on the button, so the grace is never armed and would keep it if it were.
        bar.Wait(37);
        bar.Vanish();
        Assert.False(bar.Clock.Armed(PreviewTimer.Close));

        // Never sooner than the grace, at most a look later.
        bar.Wait(PreviewHover.CloseMs - 1);
        Assert.Equal(1, bar.Open);
        bar.Wait(PreviewHover.WatchMs + 1);
        Assert.Null(bar.Open);
        Assert.False(bar.Clock.Armed(PreviewTimer.Watch));
    }

    [Fact]
    public void A_list_counted_as_holding_the_pointer_still_closes_once_it_is_gone()
    {
        var bar = new Bar();
        bar.Point(1);
        bar.Wait(PreviewHover.OpenMs);
        bar.IntoList();
        bar.Wait(2000);
        Assert.Equal(1, bar.Open);

        // Out of the list with no leave heard: the list still thinks the pointer is in it.
        bar.Vanish();
        bar.Wait(PreviewHover.CloseMs + 2 * PreviewHover.WatchMs);
        Assert.Null(bar.Open);
    }

    [Fact]
    public void The_watch_keeps_a_list_the_pointer_is_in_however_long()
    {
        var bar = new Bar();
        bar.Point(2);
        bar.Wait(PreviewHover.OpenMs);
        bar.Wait(5000);
        Assert.Equal(2, bar.Open);
        bar.IntoList();
        bar.Wait(5000);
        Assert.Equal(2, bar.Open);
        Assert.True(bar.Clock.Armed(PreviewTimer.Watch));
    }

    [Fact]
    public void A_list_laid_out_again_and_again_does_not_keep_itself_up()
    {
        var bar = new Bar();
        bar.Point(1);
        bar.Wait(PreviewHover.OpenMs);
        bar.Vanish();
        // An app renaming its window every 50 ms lays the list out again each time.
        for (var step = 0; step < 20 && bar.Open is not null; step++)
        {
            bar.ShowAgain();
            bar.Wait(50);
        }
        Assert.Null(bar.Open);
    }

    [Fact]
    public void The_watch_stops_with_the_list()
    {
        var bar = new Bar();
        bar.Point(1);
        bar.Wait(PreviewHover.OpenMs);
        bar.Point(null);
        bar.Wait(PreviewHover.CloseMs);
        Assert.Null(bar.Open);
        Assert.False(bar.Clock.Armed(PreviewTimer.Watch));

        // A list gone another way: the next look finds it closed and looks no more.
        var other = new Bar();
        other.Point(1);
        other.Wait(PreviewHover.OpenMs);
        Assert.False(other.Rules.Watched(listOpen: false, home: false));
        Assert.False(other.Clock.Armed(PreviewTimer.Watch));
    }
}
