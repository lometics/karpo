// The month's scroll (docs\widgets.md §424, Karpo.Dock\Calendar\MonthScroll.cs): the weeks scroll as Windows 11's
// calendar flyout scrolls them — a wheel's notch glides to the next month, eased; a touchpad's deltas move by what
// they say and settle on the nearest month; the heading is the month most of whose days are in view (WinUI's
// CalendarViewGeneratorHost.UpdateScope); the arrows glide as a notch does.

using Karpo.Dock;

namespace Karpo.Tests.Calendars;

public sealed class MonthScrollTests
{
    // 1 September 2026 is a Tuesday: with Monday first, its first week starts on 31 August.
    private static readonly DateOnly September = new(2026, 9, 1);

    private static MonthScroll At(DateOnly month, DayOfWeek first = DayOfWeek.Monday) => new(month, first);

    [Fact]
    public void AMonthIsAlignedWithItsFirstWeekAtTheTop()
    {
        var scroll = At(September);
        Assert.Equal(new DateOnly(2026, 8, 31), scroll.Origin);
        Assert.Equal(0, scroll.Aligned(September));
        // 1 October is a Thursday: its week starts on 28 September, four weeks on.
        Assert.Equal(4, scroll.Aligned(new DateOnly(2026, 10, 1)));
        // 1 August is a Saturday: its week starts on 27 July, five weeks back.
        Assert.Equal(-5, scroll.Aligned(new DateOnly(2026, 8, 1)));
        // With Sunday first, 1 September's week starts on 30 August.
        Assert.Equal(new DateOnly(2026, 8, 30), At(September, DayOfWeek.Sunday).Origin);
    }

    [Fact]
    public void AnAlignedMonthIsItsOwnHeading()
    {
        foreach (var first in new[] { DayOfWeek.Monday, DayOfWeek.Sunday, DayOfWeek.Saturday })
        {
            var scroll = At(September, first);
            for (var month = new DateOnly(2025, 1, 1); month < new DateOnly(2028, 1, 1); month = month.AddMonths(1))
            {
                Assert.Equal(month, scroll.HeadingAt(scroll.Aligned(month)));
            }
        }
    }

    /// <summary>
    /// Windows' rule: of the two months in view, the one with the greater share of its own days shows wins; a tie is
    /// the earlier one's. A week down from September (7 Sep to 18 Oct): 24 of September's 30 against 18 of October's 31;
    /// a week and three quarters (from 12 Sep): 18¾ of 30 against 23¼ of 31.
    /// </summary>
    [Fact]
    public void TheHeadingIsTheMonthMostOfWhoseDaysShow()
    {
        var scroll = At(September);
        Assert.Equal(September, scroll.HeadingAt(0.5f));
        Assert.Equal(September, scroll.HeadingAt(1));
        Assert.Equal(new DateOnly(2026, 10, 1), scroll.HeadingAt(1.75f));
        Assert.Equal(new DateOnly(2026, 10, 1), scroll.HeadingAt(2.5f));
        Assert.Equal(new DateOnly(2026, 8, 1), scroll.HeadingAt(-3));
    }

    [Fact]
    public void AWheelNotchGlidesToTheNextMonthEased()
    {
        var scroll = At(September);
        var glide = Assert.NotNull(scroll.Step(1, now: 1000, animate: true));
        Assert.Equal(0, glide.From);
        Assert.Equal(4, glide.To);
        Assert.Equal(scroll.GlideMs, glide.Ms);
        // On the way: past the start at once, short of the end, never back, the end on time.
        var last = 0f;
        for (var ms = 1000; ms <= 1000 + scroll.GlideMs; ms += 10)
        {
            var at = scroll.At(ms);
            Assert.InRange(at, last, 4);
            last = at;
        }
        Assert.InRange(scroll.At(1000 + (scroll.GlideMs / 2)), 2.5f, 3.99f);
        Assert.Equal(4, scroll.At(1000 + scroll.GlideMs));
        Assert.True(scroll.Moving);
        Assert.Null(scroll.Tick(1000 + scroll.GlideMs));
        Assert.False(scroll.Moving);
        Assert.Equal(new DateOnly(2026, 10, 1), scroll.Heading(5000));
    }

    /// <summary>A second notch before the first has landed goes on from where the first was going, not from where it is.</summary>
    [Fact]
    public void NotchesAddUpFromWhereTheGlideIsGoing()
    {
        var scroll = At(September);
        _ = scroll.Step(1, 1000, true);
        var glide = Assert.NotNull(scroll.Step(1, 1050, true));
        Assert.Equal(scroll.Aligned(new DateOnly(2026, 11, 1)), glide.To);
        Assert.Equal(scroll.At(1050), glide.From);
        Assert.Equal(new DateOnly(2026, 11, 1), scroll.Target);
        // And back: up is the month before.
        glide = Assert.NotNull(scroll.Step(-3, 1100, true));
        Assert.Equal(-5, glide.To);
    }

    [Fact]
    public void WithoutAnimationsAStepLandsAtOnce()
    {
        var scroll = At(September);
        Assert.Null(scroll.Step(1, 1000, animate: false));
        Assert.Equal(4, scroll.At(1000));
        Assert.False(scroll.Moving);
    }

    /// <summary>A touchpad moves the weeks by what it says, and a moment after it stops they settle on the nearest month.</summary>
    [Fact]
    public void ATouchpadPansAndSettlesOnTheNearestMonth()
    {
        var scroll = At(September);
        scroll.Pan(0.6f, 1000);
        scroll.Pan(1.15f, 1010);
        Assert.Equal(1.75f, scroll.At(1020), 3);
        Assert.True(scroll.Moving);
        // Still moving: no settle yet.
        Assert.Null(scroll.Tick(1010 + MonthScroll.SettleMs - 1));
        // Let go: back to September, 1¾ weeks away, rather than on to October, 2¼ away — though October heads it.
        Assert.Equal(new DateOnly(2026, 10, 1), scroll.Heading(1020));
        var settle = Assert.NotNull(scroll.Tick(1010 + MonthScroll.SettleMs));
        Assert.Equal(1.75f, settle.From, 3);
        Assert.Equal(0, settle.To);
        Assert.Null(scroll.Tick(1010 + MonthScroll.SettleMs + scroll.GlideMs));
        Assert.Equal(0, scroll.At(99_999));
        Assert.False(scroll.Moving);

        // Three weeks on, October is nearer.
        scroll.Pan(3, 100_000);
        Assert.Equal(4, Assert.NotNull(scroll.Tick(100_000 + MonthScroll.SettleMs)).To);
    }

    /// <summary>A pan takes over from a glide where the glide had got to; a notch after a pan goes from the month the pan shows.</summary>
    [Fact]
    public void APanStopsAGlideWhereItIs()
    {
        var scroll = At(September);
        _ = scroll.Step(1, 1000, true);
        var mid = scroll.At(1100);
        scroll.Pan(0.25f, 1100);
        Assert.Equal(mid + 0.25f, scroll.At(1100), 3);
        Assert.Equal(mid + 0.25f, scroll.At(1200), 3);
        Assert.Equal(scroll.Aligned(new DateOnly(2026, 11, 1)), Assert.NotNull(scroll.Step(1, 1110, true)).To);
    }

    /// <summary>A month far away (a day picked in another month, today again) glides there as a notch does.</summary>
    [Fact]
    public void AMonthGlidesThereAndJumpsWithoutAnimations()
    {
        var scroll = At(September);
        var glide = Assert.NotNull(scroll.To(new DateOnly(2027, 1, 1), 1000, true));
        Assert.Equal(scroll.Aligned(new DateOnly(2027, 1, 1)), glide.To);
        Assert.Null(scroll.To(new DateOnly(2027, 1, 1), 1010, true));
        Assert.Null(scroll.To(September, 1020, false));
        Assert.Equal(0, scroll.At(1020));
    }

    [Fact]
    public void TheEaseIsWindowsDecelerate()
    {
        Assert.Equal(0, MonthScroll.Eased(0));
        Assert.Equal(1, MonthScroll.Eased(1));
        // (0,0)-(0,1): x = s³, y = 3s² − 2s³ — at s = ½, x = ⅛ and y = ½.
        Assert.Equal(0.5f, MonthScroll.Eased(0.125f), 3);
        for (var x = 0.05f; x < 1; x += 0.05f)
        {
            Assert.True(MonthScroll.Eased(x) > MonthScroll.Eased(x - 0.05f));
        }
    }

    /// <summary>Never past the calendar's ends: no month before year 1's second or after 9999's eleventh, whatever is asked.</summary>
    [Fact]
    public void TheEndsHold()
    {
        var scroll = At(new DateOnly(1, 3, 1));
        _ = scroll.Step(-5, 1000, false);
        Assert.Equal(new DateOnly(1, 2, 1), scroll.Target);
        scroll = At(new DateOnly(9999, 10, 1));
        _ = scroll.Step(5, 1000, false);
        Assert.Equal(new DateOnly(9999, 11, 1), scroll.Target);
        scroll.Pan(50, 2000);
        Assert.Equal(scroll.Aligned(new DateOnly(9999, 11, 1)), scroll.At(2000));
    }
}
