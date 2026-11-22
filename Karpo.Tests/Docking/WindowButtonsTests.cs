// §667: where a window's button is, for Windows' minimize and restore animations (WindowButtons), and the answer to
// HSHELL_GETMINRECT written into what Windows hands over (MinRect.Answer). Rules only; MinRectUiTests has the windows.
//
// The user's two displays, as they stand (3840x2160 each at 150 %): the main one with Karpo's bar at its right, the second
// left of it with its bar at its left.

using Karpo.Dock;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Docking;

public sealed unsafe class WindowButtonsTests
{
    private static RECT R(int left, int top, int right, int bottom) => new() { left = left, top = top, right = right, bottom = bottom };

    private static readonly RECT MainDisplay = R(0, 0, 3840, 2160);

    private static readonly RECT SecondDisplay = R(-3840, 0, 0, 2160);

    /// <summary>The main display's bar, at its right: app buttons down it, a folder, "…".</summary>
    private static BarButtons MainBar(params ButtonPlace[] places) => new(MainDisplay, true, R(3777, 0, 3840, 2160), places);

    /// <summary>The second display's bar, at its left.</summary>
    private static BarButtons SecondBar(params ButtonPlace[] places) => new(SecondDisplay, false, R(-3840, 0, -3777, 2160), places);

    private static ButtonPlace Place(RECT rect, params nint[] windows) => new(rect, windows);

    private static readonly RECT MainButton = R(3783, 66, 3834, 117);

    private static readonly RECT SecondButton = R(-3834, 66, -3783, 117);

    private static string Show(RECT? rect) => rect is { } r ? $"{r.left},{r.top}..{r.right},{r.bottom}" : "none";

    /// <summary>The user's report: a bar at the side, and the window goes into its button there — not to the bottom.</summary>
    [Fact]
    public void AWindowGoesToItsButtonOnABarAtTheSide()
    {
        var bars = new[] { MainBar(Place(R(3783, 6, 3834, 57), 1), Place(MainButton, 2)) };
        Assert.Equal(Show(MainButton), Show(WindowButtons.Of(bars, 2, MainDisplay)));
        Assert.Equal("3783,6..3834,57", Show(WindowButtons.Of(bars, 1, MainDisplay)));
    }

    /// <summary>Every window of a button of several (an app's, combined) goes to that one button.</summary>
    [Fact]
    public void EveryWindowOfAButtonOfSeveralGoesToIt()
    {
        var bars = new[] { MainBar(Place(MainButton, 1, 2, 3)) };
        foreach (var window in (nint[])[1, 2, 3])
        {
            Assert.Equal(Show(MainButton), Show(WindowButtons.Of(bars, window, MainDisplay)));
        }
    }

    /// <summary>Two displays, every window on both bars: a window goes to the button on its own display's bar.</summary>
    [Fact]
    public void AWindowGoesToTheButtonOnItsOwnDisplay()
    {
        var bars = new[] { MainBar(Place(MainButton, 1)), SecondBar(Place(SecondButton, 1)) };
        Assert.Equal(Show(MainButton), Show(WindowButtons.Of(bars, 1, MainDisplay)));
        Assert.Equal(Show(SecondButton), Show(WindowButtons.Of(bars, 1, SecondDisplay)));
        // Published the other way round, the same.
        var reversed = bars.Reverse().ToArray();
        Assert.Equal(Show(MainButton), Show(WindowButtons.Of(reversed, 1, MainDisplay)));
        Assert.Equal(Show(SecondButton), Show(WindowButtons.Of(reversed, 1, SecondDisplay)));
    }

    /// <summary>Its own display's bar shows no button for it (only pins there): the button on another bar, the main one's first.</summary>
    [Fact]
    public void WhereItsOwnBarHasNoButtonItGoesToAnotherBarsButton()
    {
        var third = new BarButtons(R(3840, 0, 5760, 1080), false, R(3840, 1032, 5760, 1080), [Place(R(4000, 1035, 4045, 1077), 1)]);
        var bars = new[] { SecondBar(), third, MainBar(Place(MainButton, 1)) };
        Assert.Equal(Show(MainButton), Show(WindowButtons.Of(bars, 1, SecondDisplay)));
        // Not on the main bar either: the third's.
        Assert.Equal("4000,1035..4045,1077", Show(WindowButtons.Of([SecondBar(), third, MainBar()], 1, SecondDisplay)));
    }

    /// <summary>A folder holding the pin of the window's app: the folder's button; "…" for a button the bar had no room for.</summary>
    [Fact]
    public void AFolderOrTheOverflowStandsForWhatItHolds()
    {
        var folder = R(3783, 400, 3834, 451);
        var more = R(3783, 2090, 3834, 2141);
        var bars = new[] { MainBar(Place(MainButton, 1), Place(folder, 2, 3), Place(more, 4)) };
        Assert.Equal(Show(folder), Show(WindowButtons.Of(bars, 2, MainDisplay)));
        Assert.Equal(Show(folder), Show(WindowButtons.Of(bars, 3, MainDisplay)));
        Assert.Equal(Show(more), Show(WindowButtons.Of(bars, 4, MainDisplay)));
    }

    /// <summary>
    /// A window no bar shows (a tool window, an app on a bar without the open apps): the middle of its own display's bar,
    /// as thick as the bar — at the side, a square down the middle of its height; never Explorer's hidden bottom.
    /// </summary>
    [Fact]
    public void AWindowNoBarShowsGoesToTheMiddleOfItsDisplaysBar()
    {
        var bars = new[] { MainBar(Place(MainButton, 1)), SecondBar(Place(SecondButton, 1)) };
        Assert.Equal("3777,1048..3840,1111", Show(WindowButtons.Of(bars, 9, MainDisplay)));
        Assert.Equal("-3840,1048..-3777,1111", Show(WindowButtons.Of(bars, 9, SecondDisplay)));
        // A bar along the bottom: the middle of its length.
        Assert.Equal("1884,2088..1956,2160", Show(WindowButtons.Middle(R(0, 2088, 3840, 2160))));
        // A display with no bar of Karpo's: the main display's.
        Assert.Equal("3777,1048..3840,1111", Show(WindowButtons.Of([MainBar()], 9, R(3840, 0, 5760, 1080))));
        // The display not known: the main one first.
        Assert.Equal(Show(MainButton), Show(WindowButtons.Of(bars, 1, null)));
        Assert.Equal("3777,1048..3840,1111", Show(WindowButtons.Of(bars, 9, null)));
        // No main bar either: the first bar's.
        Assert.Equal("-3840,1048..-3777,1111", Show(WindowButtons.Of([SecondBar()], 9, R(3840, 0, 5760, 1080))));
    }

    /// <summary>No bar of Karpo's at all: no answer, and Windows keeps what it had.</summary>
    [Fact]
    public void WithoutABarThereIsNoAnswer()
    {
        Assert.Null(WindowButtons.Of([], 1, MainDisplay));
        Assert.False(WindowButtons.Holds([], 1));
        Assert.True(WindowButtons.Holds([MainBar(Place(MainButton, 1))], 1));
        Assert.False(WindowButtons.Holds([MainBar(Place(MainButton, 1))], 2));
    }

    /// <summary>Published, replaced and taken away per bar; what was read stays as it was read.</summary>
    [Fact]
    public void EachBarPublishesItsOwn()
    {
        var buttons = new WindowButtons();
        buttons.Publish(10, MainBar(Place(MainButton, 1)));
        buttons.Publish(20, SecondBar(Place(SecondButton, 1)));
        var read = buttons.Bars;
        Assert.Equal(2, read.Count);
        buttons.Publish(10, MainBar(Place(MainButton, 2)));
        Assert.Equal(2, buttons.Bars.Count);
        Assert.True(WindowButtons.Holds(buttons.Bars, 2));
        Assert.False(WindowButtons.Holds(buttons.Bars.Where(bar => bar.Main).ToList(), 1));
        // What was read before is the bars as they were then.
        Assert.True(WindowButtons.Holds(read.Where(bar => bar.Main).ToList(), 1));
        buttons.Publish(10, null);
        Assert.Single(buttons.Bars);
        buttons.Publish(20, null);
        Assert.Empty(buttons.Bars);
    }

    // --- The answer, as Windows hands the question over ------------------------------------------------------------

    /// <summary>SHELLHOOKINFO as it comes: the window, then its rectangle as four SHORTs.</summary>
    private struct Info
    {
        public nint Window;
        public short Left;
        public short Top;
        public short Right;
        public short Bottom;
    }

    /// <summary>The rectangle written in, as SHORTs after the window, and TRUE: Windows takes it.</summary>
    [Fact]
    public void TheAnswerIsWrittenAsWindowsReadsIt()
    {
        var info = new Info { Window = 0x5C0C6C, Left = 479, Top = 2158, Right = 480, Bottom = 2230 };
        nint asked = 0;
        Assert.True(MinRect.Answer(&info, window => { asked = window; return MainButton; }));
        Assert.Equal(0x5C0C6C, asked);
        Assert.Equal(0x5C0C6C, info.Window);
        Assert.Equal((3783, 66, 3834, 117), ((int)info.Left, (int)info.Top, (int)info.Right, (int)info.Bottom));
        // A display left of the main one: below zero.
        Assert.True(MinRect.Answer(&info, _ => SecondButton));
        Assert.Equal((-3834, 66, -3783, 117), ((int)info.Left, (int)info.Top, (int)info.Right, (int)info.Bottom));
    }

    /// <summary>No answer: FALSE, and the rectangle left as it came (Explorer's, or another shell's).</summary>
    [Fact]
    public void NoAnswerLeavesTheRectangleAsItCame()
    {
        var info = new Info { Window = 7, Left = 479, Top = 2158, Right = 480, Bottom = 2230 };
        Assert.False(MinRect.Answer(&info, _ => null));
        Assert.Equal((479, 2158, 480, 2230), ((int)info.Left, (int)info.Top, (int)info.Right, (int)info.Bottom));
        Assert.False(MinRect.Answer(null, _ => MainButton));
    }

    /// <summary>A place past what a SHORT holds is held at its end, never wrapped round to the other side.</summary>
    [Fact]
    public void APlacePastAShortIsHeldAtItsEnd()
    {
        var info = new Info { Window = 7 };
        Assert.True(MinRect.Answer(&info, _ => R(-40_000, 100, 40_000, 70_000)));
        Assert.Equal((short.MinValue, 100, short.MaxValue, short.MaxValue), ((int)info.Left, (int)info.Top, (int)info.Right, (int)info.Bottom));
    }
}
