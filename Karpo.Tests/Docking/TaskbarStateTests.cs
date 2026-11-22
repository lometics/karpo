// Hiding Explorer's taskbar and showing it again (Karpo.Dock\Taskbar.cs: TakeOver, PutBack, Rehide),
// against a fake Explorer. §465, 2026-11-01: "hiding / showing the windows taskbar does not work /
// does not change anything" — Explorer saves its live auto-hide into StuckRects3, Karpo read Karpo's own
// back as the person's, and every put-back left the taskbar auto-hidden. Now nothing is kept:
// shown is auto-hide off and its windows shown, whatever Explorer said before.

using Karpo.Core;
using Karpo.Dock;

namespace Karpo.Tests.Docking;

public sealed class TaskbarStateTests
{
    private const uint AutoHide = 1;
    private const uint OnTop = 2;

    /// <summary>Explorer's taskbar: its state, and whether it takes a state asked of it.</summary>
    private sealed class FakeExplorer : ITaskbarShell
    {
        public bool Found { get; set; } = true;
        public uint Now { get; set; } = OnTop;
        public bool Takes { get; set; } = true;
        public int Shown { get; private set; }
        public List<uint> Asked { get; } = [];

        public uint State() => Now;

        public void SetState(uint state)
        {
            Asked.Add(state);
            Steps.Add((state & AutoHide) != 0 ? "auto-hide on" : "auto-hide off");
            if (Takes)
            {
                Now = state;
            }
        }

        public void ShowBars()
        {
            Shown++;
            Steps.Add("show");
        }

        /// <summary>§591: the edge it stands on; null, a Windows with no edge but the bottom.</summary>
        public Edge? At { get; set; }

        /// <summary>§591: what was asked of it, in order: "auto-hide on/off", "move left", "show".</summary>
        public List<string> Steps { get; } = [];

        public Edge? Edge => At;

        public TaskbarPosition.Moved MoveTo(Edge edge)
        {
            if (At is not { } now)
            {
                return TaskbarPosition.Moved.NotOffered;
            }
            if (now == edge)
            {
                return TaskbarPosition.Moved.Already;
            }
            Steps.Add("move " + edge.ToString().ToLowerInvariant());
            At = edge;
            return TaskbarPosition.Moved.Done;
        }
    }

    /// <summary>
    /// §591: Windows' taskbar on a side or the top (the person's own choice in Windows' settings, or an earlier Karpo's
    /// quit before §600): the takeover moves it to the bottom first, the one edge it auto-hides on, then auto-hides it —
    /// on a side it would hold its strip, hidden or not, and every bar on that edge would stand beside an empty strip.
    /// </summary>
    [Theory]
    [InlineData(Edge.Left)]
    [InlineData(Edge.Top)]
    [InlineData(Edge.Right)]
    public void TheTakeoverMovesWindowsTaskbarToTheBottomBeforeAutoHidingIt(Edge side)
    {
        var explorer = new FakeExplorer { At = side };
        Assert.Equal(TaskbarPosition.Moved.Done, Taskbar.TakeOver(explorer));
        Assert.Equal(["move bottom", "auto-hide on"], explorer.Steps);
        Assert.Equal(Edge.Bottom, explorer.At);
        Assert.True(Taskbar.AutoHides(explorer.Now));
    }

    /// <summary>§591: at the bottom already, or a Windows with no other edge: nothing moved.</summary>
    [Fact]
    public void ATaskbarAtTheBottomOrWithNoOtherEdgeIsNotMoved()
    {
        var bottom = new FakeExplorer { At = Edge.Bottom };
        Assert.Equal(TaskbarPosition.Moved.Already, Taskbar.TakeOver(bottom));
        var older = new FakeExplorer();
        Assert.Equal(TaskbarPosition.Moved.NotOffered, Taskbar.TakeOver(older));
        Assert.Equal(["auto-hide on"], bottom.Steps);
        Assert.Equal(["auto-hide on"], older.Steps);
    }

    /// <summary>
    /// §600, the person: "dont do this then. because it moves it to the right on all screens." — Windows has one edge
    /// for every display. However Karpo ends (a quit, the session ending, a crash, the guard: all one PutBack), Windows'
    /// taskbar is only shown again, on the edge it stands on: auto-hide off, then shown, never moved.
    /// </summary>
    [Theory]
    [InlineData(Edge.Bottom)]
    [InlineData(Edge.Left)]
    [InlineData(Edge.Right)]
    public void NoWayOfEndingMovesWindowsTaskbar(Edge at)
    {
        var explorer = new FakeExplorer { At = Edge.Bottom };
        Taskbar.TakeOver(explorer);
        explorer.At = at;
        explorer.Steps.Clear();

        var back = Taskbar.PutBack(explorer);
        Assert.Equal(Taskbar.Back.Done, back);
        Assert.Equal(["auto-hide off", "show"], explorer.Steps);
        Assert.Equal(at, explorer.At);
        Assert.Equal("Explorer's taskbar shown again, auto-hide off", Taskbar.Says(back));
    }

    /// <summary>
    /// §591: the person moves Windows' taskbar to a side in Windows' settings while Karpo runs, auto-hide still on: it
    /// would hold its strip there, so the reconcile moves it back to the bottom; there, nothing is asked.
    /// </summary>
    [Fact]
    public void ATaskbarMovedOffTheBottomWhileKarpoRunsGoesBack()
    {
        var explorer = new FakeExplorer { At = Edge.Bottom };
        Taskbar.TakeOver(explorer);
        Assert.False(Taskbar.Rehide(explorer));

        explorer.At = Edge.Top;
        explorer.Steps.Clear();
        Assert.True(Taskbar.Rehide(explorer));
        Assert.Equal(["move bottom", "auto-hide on"], explorer.Steps);
        Assert.False(Taskbar.Rehide(explorer));
    }

    [Fact]
    public void HiddenIsAutoHideAndShownIsAutoHideOff()
    {
        var explorer = new FakeExplorer();

        Taskbar.TakeOver(explorer);
        Assert.Equal(AutoHide | OnTop, explorer.Now);

        Assert.Equal(Taskbar.Back.Done, Taskbar.PutBack(explorer));
        Assert.Equal(OnTop, explorer.Now);
        Assert.Equal(1, explorer.Shown);
    }

    /// <summary>
    /// The user's PC (2026-11-01): Explorer auto-hidden before Karpo ever ran — Karpo's own auto-hide,
    /// kept by Windows from an earlier takeover. Shown again, it is not auto-hidden.
    /// </summary>
    [Fact]
    public void AnAutoHideFoundBeforeKarpoIsNotPutBack()
    {
        var explorer = new FakeExplorer { Now = AutoHide | OnTop };

        Taskbar.TakeOver(explorer);
        Assert.Equal(Taskbar.Back.Done, Taskbar.PutBack(explorer));
        Assert.False(Taskbar.AutoHides(explorer.Now));
    }

    /// <summary>The guard after the dock showed the taskbar: shown already, it stays so.</summary>
    [Fact]
    public void ASecondPutBackChangesNothing()
    {
        var explorer = new FakeExplorer { Now = AutoHide | OnTop };
        Taskbar.PutBack(explorer);

        Assert.Equal(Taskbar.Back.Done, Taskbar.PutBack(explorer));
        Assert.Equal(OnTop, explorer.Now);
        Assert.Equal(2, explorer.Shown);
    }

    /// <summary>No Explorer as the taskbar is to be shown (it is restarting): nothing asked.</summary>
    [Fact]
    public void WithNoExplorerNothingIsAsked()
    {
        var explorer = new FakeExplorer { Found = false };
        Assert.Equal(Taskbar.Back.NoTaskbar, Taskbar.PutBack(explorer));
        Assert.Empty(explorer.Asked);
        Assert.Equal(0, explorer.Shown);
    }

    /// <summary>An Explorer that does not take the state says so, its windows shown all the same.</summary>
    [Fact]
    public void AStateNotTakenIsSaid()
    {
        var explorer = new FakeExplorer { Now = AutoHide | OnTop, Takes = false };
        var back = Taskbar.PutBack(explorer);
        Assert.Equal(Taskbar.Back.NotTaken, back);
        Assert.Equal(1, explorer.Shown);
        Assert.Contains("still auto-hides", Taskbar.Says(back), StringComparison.Ordinal);
    }

    /// <summary>
    /// An Explorer restart (§272), or the person's own switch in Windows' settings, while Karpo runs:
    /// the taskbar not auto-hidden is auto-hidden again; already so, nothing is asked.
    /// </summary>
    [Fact]
    public void ATaskbarThatLostAutoHideIsAutoHiddenAgain()
    {
        var explorer = new FakeExplorer();
        Taskbar.TakeOver(explorer);
        Assert.False(Taskbar.Rehide(explorer));

        explorer.Now = OnTop;
        Assert.True(Taskbar.Rehide(explorer));
        Assert.Equal(AutoHide | OnTop, explorer.Now);
        var asked = explorer.Asked.Count;
        Assert.False(Taskbar.Rehide(explorer));
        Assert.Equal(asked, explorer.Asked.Count);

        Assert.Equal(Taskbar.Back.Done, Taskbar.PutBack(explorer));
        Assert.False(Taskbar.AutoHides(explorer.Now));
    }

    /// <summary>No Explorer to ask: nothing set.</summary>
    [Fact]
    public void WithNoExplorerNothingIsRehidden()
    {
        var explorer = new FakeExplorer { Found = false };
        Assert.False(Taskbar.Rehide(explorer));
        Assert.Empty(explorer.Asked);
    }

    /// <summary>
    /// §542: the reconcile covers the taskbar again only where something is off — and a window without its
    /// blank region (Explorer made a display's taskbar anew, on a thread already watched) is off too, or it
    /// would draw over the bar each time Explorer shows it. Nothing off asks for nothing, so no reconcile loops.
    /// </summary>
    [Fact]
    public void ATaskbarWindowWithoutItsBlankRegionIsCoveredAgain()
    {
        Assert.Null(Taskbar.Off(lost: false, shown: false, moved: false, bare: false));
        Assert.Contains("blanked", Taskbar.Off(lost: false, shown: false, moved: false, bare: true), StringComparison.Ordinal);
        Assert.Contains("showed itself", Taskbar.Off(lost: false, shown: true, moved: false, bare: false), StringComparison.Ordinal);
        Assert.Contains("new windows", Taskbar.Off(lost: false, shown: false, moved: true, bare: true), StringComparison.Ordinal);
        // The first that holds is what the trace says: a lost auto-hide says so whatever else is off.
        Assert.Contains("not auto-hidden", Taskbar.Off(lost: true, shown: true, moved: true, bare: true), StringComparison.Ordinal);
    }
}
