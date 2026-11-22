// Where an app button's signals go and what colour they are (Karpo.Dock\AppSignals.cs, SignalShape
// and SignalInk): the progress on the running indicator's line, inside the highlight on every edge;
// the part done from the start, or from the bottom up a vertical bar; the overlay and the badge on
// the icon's corner; and Windows 11's colours for each state and look.

using System.Numerics;
using Karpo.Dock;
using Color = Windows.UI.Color;

namespace Karpo.Tests.Signals;

public sealed class SignalShapeTests
{
    /// <summary>A 48-DIP bar's app button at 100 %: a 40-px highlight, a 3-px indicator, a hairline, a 4-px radius.</summary>
    private static readonly RectF Highlight = new(102, 4, 40, 40);

    private const float Pill = 3;
    private const float Stroke = 1;
    private const float Radius = 4;

    [Fact]
    public void OnABottomBarTheRunIsWhereTheIndicatorIs()
    {
        var run = SignalShape.Run(Highlight, Edge.Bottom, Pill, Stroke, Radius);

        // Scene.PillCentre puts the indicator's centre a hairline plus half its thickness in.
        Assert.Equal(new RectF(106, 40, 32, 3), run);
        Assert.Equal(Highlight.Bottom - Stroke - Pill / 2f, run.Y + run.Height / 2f);
    }

    [Theory]
    [InlineData(Edge.Top)]
    [InlineData(Edge.Bottom)]
    [InlineData(Edge.Left)]
    [InlineData(Edge.Right)]
    public void TheRunStaysInsideTheRoundedHighlightOnEveryEdge(Edge edge)
    {
        var run = SignalShape.Run(Highlight, edge, Pill, Stroke, Radius);
        var vertical = edge is Edge.Left or Edge.Right;

        Assert.False(run.IsEmpty);
        Assert.True(run.X >= Highlight.X && run.Right <= Highlight.Right);
        Assert.True(run.Y >= Highlight.Y && run.Bottom <= Highlight.Bottom);
        // Along: the highlight less its corners. Across: the indicator's thickness, at its edge.
        Assert.Equal(Highlight.Width - 2 * Radius, vertical ? run.Height : run.Width);
        Assert.Equal(Pill, vertical ? run.Width : run.Height);
        var gap = edge switch
        {
            Edge.Top => run.Y - Highlight.Y,
            Edge.Left => run.X - Highlight.X,
            Edge.Right => Highlight.Right - run.Right,
            _ => Highlight.Bottom - run.Bottom,
        };
        Assert.Equal(Stroke, gap);
    }

    [Fact]
    public void NoHighlightNoRun()
    {
        Assert.True(SignalShape.Run(default, Edge.Bottom, Pill, Stroke, Radius).IsEmpty);
        Assert.True(SignalShape.Done(default, false, 0.5f).IsEmpty);
        Assert.True(SignalShape.Track(default, false, 1).IsEmpty);
        Assert.True(SignalShape.Corner(default, new Vector2(16, 16)).IsEmpty);
    }

    [Fact]
    public void TheTrackIsAHairlineThroughTheRunsMiddle()
    {
        var run = new RectF(106, 40, 32, 3);

        Assert.Equal(new RectF(106, 41, 32, 1), SignalShape.Track(run, false, 1));
        Assert.Equal(new RectF(41, 106, 1, 32), SignalShape.Track(new RectF(40, 106, 3, 32), true, 1));
    }

    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(0.3f, 10f)]
    [InlineData(0.5f, 16f)]
    [InlineData(1f, 32f)]
    [InlineData(1.7f, 32f)]
    [InlineData(-1f, 0f)]
    [InlineData(float.NaN, 0f)]
    public void ThePartDoneGrowsFromTheStart(float value, float length)
    {
        var run = new RectF(106, 40, 32, 3);

        var done = SignalShape.Done(run, false, value);

        Assert.Equal(new RectF(106, 40, length, 3), done);
    }

    [Fact]
    public void UpAVerticalBarItGrowsFromTheBottom()
    {
        var run = new RectF(40, 106, 3, 32);

        var done = SignalShape.Done(run, true, 0.25f);

        Assert.Equal(new RectF(40, 130, 3, 8), done);
        Assert.Equal(run.Bottom, done.Bottom);
    }

    /// <summary>
    /// Spotify's pin: a progress with nothing done drew its bare track where the running indicator
    /// was. It shows only once a pixel of the run is done, on a bar of either direction; a run
    /// without an end always shows, and nothing shows without a run.
    /// </summary>
    [Theory]
    [InlineData(ProgressKind.Normal, 0f, false)]
    [InlineData(ProgressKind.Paused, 0f, false)]
    [InlineData(ProgressKind.Error, 0f, false)]
    // 0.01 of 32 px is a third of a pixel: an empty fill on screen.
    [InlineData(ProgressKind.Normal, 0.01f, false)]
    // 0.02 of 32 px rounds to the first pixel.
    [InlineData(ProgressKind.Normal, 0.02f, true)]
    [InlineData(ProgressKind.Paused, 0.7f, true)]
    [InlineData(ProgressKind.Indeterminate, 0f, true)]
    [InlineData(ProgressKind.None, 0.5f, false)]
    public void ProgressTakesTheIndicatorsPlaceOnlyOnceSomethingIsDone(ProgressKind kind, float value, bool shows)
    {
        var signal = new TileSignal(kind, value);

        Assert.Equal(shows, SignalShape.Shows(signal, new RectF(106, 40, 32, 3), false));
        Assert.Equal(shows, SignalShape.Shows(signal, new RectF(40, 106, 3, 32), true));
        Assert.False(SignalShape.Shows(signal, default, false));
    }

    [Fact]
    public void TheRunnerIsTwoFifthsOfTheRun()
    {
        Assert.Equal(13f, SignalShape.RunnerLength(new RectF(0, 0, 32, 3), false));
        Assert.Equal(13f, SignalShape.RunnerLength(new RectF(0, 0, 3, 32), true));
    }

    [Theory]
    [InlineData(24f, 16f)]
    [InlineData(36f, 24f)]
    [InlineData(48f, 32f)]
    [InlineData(6f, 8f)]
    public void TheCornerIsTwoThirdsOfTheIcon(float icon, float side)
    {
        Assert.Equal(side, SignalShape.CornerSide(icon));
    }

    [Fact]
    public void TheCornerReachesPastTheIconsLowerRight()
    {
        var icon = new RectF(110, 12, 24, 24);

        var corner = SignalShape.Corner(icon, new Vector2(16, 16));

        // An eighth of its height past the corner: 2 px of a 16-px overlay.
        Assert.Equal(new RectF(120, 22, 16, 16), corner);
        Assert.Equal(icon.Right + 2, corner.Right);
        Assert.Equal(icon.Bottom + 2, corner.Bottom);
    }

    [Fact]
    public void AWideBadgeGrowsToTheLeft()
    {
        var icon = new RectF(110, 12, 24, 24);

        var narrow = SignalShape.Corner(icon, SignalShape.BadgeSize(16, 6));
        var wide = SignalShape.Corner(icon, SignalShape.BadgeSize(16, 18));

        Assert.Equal(narrow.Right, wide.Right);
        Assert.True(wide.X < narrow.X);
    }

    [Theory]
    [InlineData(16f, 6f, 16f)]
    [InlineData(16f, 8f, 16f)]
    [InlineData(16f, 12f, 20f)]
    [InlineData(16f, 18f, 26f)]
    public void ABadgeIsACircleForOneFigureAndLongerForMore(float side, float text, float width)
    {
        Assert.Equal(new Vector2(width, side), SignalShape.BadgeSize(side, text));
    }

    // --- Ink ---------------------------------------------------------------------------------------

    private static readonly Color AccentLight2 = Color.FromArgb(255, 0x99, 0xEB, 0xFF);
    private static readonly Color AccentDark1 = Color.FromArgb(255, 0x00, 0x5F, 0xB8);

    private static Look Dark => new(Glass: false, Dark: true, Accent: Color.FromArgb(255, 0, 0x78, 0xD4), AccentLight2: AccentLight2, AccentDark1: AccentDark1);

    private static Look Light => Dark with { Dark = false };

    [Fact]
    public void RunningIsTheAccentNotGreen()
    {
        Assert.Equal(AccentLight2, SignalInk.Progress(ProgressKind.Normal, Dark));
        Assert.Equal(AccentDark1, SignalInk.Progress(ProgressKind.Normal, Light));
        Assert.Equal(AccentLight2, SignalInk.Progress(ProgressKind.Indeterminate, Dark));
    }

    [Fact]
    public void PausedIsCautionAndAnErrorIsCritical()
    {
        Assert.Equal(Color.FromArgb(255, 0xFC, 0xE1, 0x00), SignalInk.Progress(ProgressKind.Paused, Dark));
        Assert.Equal(Color.FromArgb(255, 0x9D, 0x5D, 0x00), SignalInk.Progress(ProgressKind.Paused, Light));
        Assert.Equal(Color.FromArgb(255, 0xFF, 0x99, 0xA4), SignalInk.Progress(ProgressKind.Error, Dark));
        Assert.Equal(Color.FromArgb(255, 0xC4, 0x2B, 0x1C), SignalInk.Progress(ProgressKind.Error, Light));
    }

    [Fact]
    public void TheTrackIsTheIndicatorsGrey()
    {
        Assert.Equal(Color.FromArgb(139, 255, 255, 255), SignalInk.Track(Dark));
        Assert.Equal(Color.FromArgb(114, 0, 0, 0), SignalInk.Track(Light));
    }

    [Fact]
    public void WithoutItsLighterAndDarkerShadesTheAccentItselfIsUsed()
    {
        var plain = new Look(Glass: false, Dark: true, Accent: Color.FromArgb(255, 1, 2, 3));

        Assert.Equal(Color.FromArgb(255, 1, 2, 3), SignalInk.Accent(plain));
        Assert.Equal(Color.FromArgb(255, 1, 2, 3), SignalInk.Accent(plain with { Dark = false }));
    }

    [Fact]
    public void TheBadgeIsTheAccentWithTextOnAccent()
    {
        Assert.Equal(AccentLight2, SignalInk.BadgeFill(Dark));
        Assert.Equal(Color.FromArgb(255, 0, 0, 0), SignalInk.BadgeText(Dark));
        Assert.Equal(AccentDark1, SignalInk.BadgeFill(Light));
        Assert.Equal(Color.FromArgb(255, 255, 255, 255), SignalInk.BadgeText(Light));
    }

    [Fact]
    public void HighContrastHasOneColourForEveryState()
    {
        var contrast = Dark with
        {
            HighContrast = true,
            HcHighlight = Color.FromArgb(255, 0x1A, 0xEB, 0xFF),
            HcText = Color.FromArgb(255, 255, 255, 255),
            HcHighlightText = Color.FromArgb(255, 0, 0, 0),
        };

        foreach (var kind in new[] { ProgressKind.Normal, ProgressKind.Paused, ProgressKind.Error, ProgressKind.Indeterminate })
        {
            Assert.Equal(contrast.HcHighlight, SignalInk.Progress(kind, contrast));
        }
        Assert.Equal(contrast.HcText, SignalInk.Track(contrast));
        Assert.Equal(contrast.HcHighlightText, SignalInk.BadgeText(contrast));
    }
}
