// Thumbnail toolbars (Karpo.Dock\ThumbBar.cs): what ThumbBarAddButtons and ThumbBarUpdateButtons
// send, byte for byte as the probe logged it; what is kept after an add and after an update; what
// each flag means; where the buttons stand; and where a click goes — through a fake of the sender,
// so nothing is posted anywhere.

using Karpo.Dock;

namespace Karpo.Tests.Signals;

public sealed class ThumbBarTests
{
    /// <summary>A section's data rebuilt from the non-zero bytes the probe logged, and its length.</summary>
    private static byte[] Logged(int length, params (int At, string Hex)[] runs)
    {
        var data = new byte[length];
        foreach (var (at, hex) in runs)
        {
            Convert.FromHexString(hex).CopyTo(data, at);
        }
        return data;
    }

    /// <summary>
    /// ThumbBarAddButtons of four buttons, as the band got it: "Previous" (an image, a tooltip, the
    /// flags), "Play" (an icon, dismisses), "Next (disabled)" (image 2, disabled and no background),
    /// and a fourth with image 1, hidden and not interactive.
    /// </summary>
    private static byte[] Added => Logged(
        2164,
        (0x0, "04"), (0x4, "0D"), (0x8, "65"), (0x14, "50"), (0x16, "72"), (0x18, "65"), (0x1A, "76"), (0x1C, "69"), (0x1E, "6F"),
        (0x20, "75"), (0x22, "73"), (0x220, "0E"), (0x224, "66"), (0x22C, "BB0B3C0150"), (0x232, "6C"), (0x234, "61"), (0x236, "79"),
        (0x438, "02"), (0x43C, "0D"), (0x440, "67"), (0x444, "02"), (0x44C, "4E"), (0x44E, "65"), (0x450, "78"), (0x452, "74"),
        (0x454, "20"), (0x456, "28"), (0x458, "64"), (0x45A, "69"), (0x45C, "73"), (0x45E, "61"), (0x460, "62"), (0x462, "6C"),
        (0x464, "65"), (0x466, "64"), (0x468, "29"), (0x654, "05"), (0x658, "09"), (0x65C, "68"), (0x660, "01"), (0x870, "18"));

    /// <summary>ThumbBarUpdateButtons of two: "Play" becomes "Pause" and enabled; the fourth becomes not hidden, still not interactive.</summary>
    private static byte[] Updated => Logged(
        1084,
        (0x0, "02"), (0x4, "0C"), (0x8, "66"), (0x14, "50"), (0x16, "61"), (0x18, "75"), (0x1A, "73"), (0x1C, "65"), (0x220, "08"),
        (0x224, "68"), (0x438, "10"));

    private static Pixels Picture(byte shade) => new() { Data = [shade, shade, shade, 255], Width = 1, Height = 1 };

    [Fact]
    public void AnAddReadsAsTheAppSentIt()
    {
        var buttons = ThumbBar.Read(Added);

        Assert.Equal(
            [
                new ThumbButtonData(0xD, 101, 0, 0, "Previous", 0),
                new ThumbButtonData(0xE, 102, 0, 0x013C0BBB, "Play", 0x2),
                new ThumbButtonData(0xD, 103, 2, 0, "Next (disabled)", 0x5),
                new ThumbButtonData(0x9, 104, 1, 0, "", 0x18),
            ],
            buttons);
        // Only the second sent an icon, and it counts.
        Assert.Null(buttons[0].IconHandle);
        Assert.NotNull(buttons[1].IconHandle);
    }

    [Fact]
    public void AnUpdateReadsAsTheAppSentIt()
    {
        Assert.Equal(
            [new ThumbButtonData(0xC, 102, 0, 0, "Pause", 0), new ThumbButtonData(0x8, 104, 0, 0, "", 0x10)],
            ThumbBar.Read(Updated));
    }

    [Fact]
    public void OnlyWholeButtonsAndNoMoreThanSevenAreRead()
    {
        // Four claimed, a button and a half there.
        Assert.Single(ThumbBar.Read(Added.AsSpan(0, 4 + ThumbBar.ButtonSize + 100)));
        Assert.Empty(ThumbBar.Read([1, 0]));
        // Nine claimed, nine there: seven kept, as the taskbar keeps no more.
        var nine = new byte[4 + 9 * ThumbBar.ButtonSize];
        nine[0] = 9;
        Assert.Equal(ThumbBar.Most, ThumbBar.Read(nine).Count);
    }

    [Fact]
    public void AddedButtonsTakeWhatTheirMasksSayAndDefaultsForTheRest()
    {
        var play = Picture(9);
        var kept = ThumbBar.Merge(null, ThumbBar.Read(Added), [null, play, null, null], update: false);

        Assert.Equal(
            [
                new ThumbButton(101, ThumbFlags.None, "Previous", null, 0),
                new ThumbButton(102, ThumbFlags.DismissOnClick, "Play", play, -1),
                new ThumbButton(103, ThumbFlags.Disabled | ThumbFlags.NoBackground, "Next (disabled)", null, 2),
                new ThumbButton(104, ThumbFlags.Hidden | ThumbFlags.NonInteractive, "", null, 1),
            ],
            kept);
    }

    [Fact]
    public void AnUpdateChangesOnlyWhatItsMasksSay()
    {
        var play = Picture(9);
        var kept = ThumbBar.Merge(null, ThumbBar.Read(Added), [null, play, null, null], update: false);

        var updated = ThumbBar.Merge(kept, ThumbBar.Read(Updated), [null, null], update: true);

        // "Play" took the tooltip and flags it was sent and kept its picture; the fourth took its flags only.
        Assert.Equal(new ThumbButton(102, ThumbFlags.None, "Pause", play, -1), updated[1]);
        Assert.Equal(new ThumbButton(104, ThumbFlags.NonInteractive, "", null, 1), updated[3]);
        Assert.Equal(kept[0], updated[0]);
        Assert.Equal(kept[2], updated[2]);
    }

    [Fact]
    public void AnUpdateForAButtonTheToolbarHasNotIsLeftOut()
    {
        var kept = ThumbBar.Merge(null, ThumbBar.Read(Added), [], update: false);

        var updated = ThumbBar.Merge(kept, [new ThumbButtonData(0x4, 999, 0, 0, "Nobody", 0)], [], update: true);

        Assert.Equal(kept, updated);
    }

    [Fact]
    public void AnIconSentAgainReplacesThePicture()
    {
        var (first, second) = (Picture(1), Picture(2));
        var kept = ThumbBar.Merge(null, [new ThumbButtonData(0x2, 7, 0, 0x1234, "", 0)], [first], update: false);

        var updated = ThumbBar.Merge(kept, [new ThumbButtonData(0x2, 7, 0, 0x5678, "", 0)], [second], update: true);

        Assert.Same(second, updated[0].Picture);
    }

    [Theory]
    [InlineData(ThumbFlags.None, true, true, true, false)]
    [InlineData(ThumbFlags.Disabled, true, false, false, true)]
    [InlineData(ThumbFlags.DismissOnClick, true, true, true, false)]
    [InlineData(ThumbFlags.NoBackground, true, true, false, false)]
    [InlineData(ThumbFlags.Hidden, false, false, false, false)]
    [InlineData(ThumbFlags.NonInteractive, true, false, false, false)]
    public void EachFlagMeansWhatWindowsSays(ThumbFlags flags, bool shown, bool clickable, bool lights, bool faint)
    {
        var button = new ThumbButton(1, flags, "Tip", null, -1);

        Assert.Equal(shown, button.Shown);
        Assert.Equal(clickable, button.Clickable);
        Assert.Equal(lights, button.Lights);
        Assert.Equal(faint, button.Faint);
    }

    [Fact]
    public void HiddenButtonsAreNotShownAndTakeNoRoom()
    {
        var info = new ThumbInfo(ThumbBar.Merge(null, ThumbBar.Read(Added), [], update: false), null, null);

        Assert.Equal([101u, 102u, 103u], info.Shown.Select(button => button.Id));
    }

    // --- The clip -------------------------------------------------------------------------------

    [Fact]
    public void TheClipReadsAsSent()
    {
        var logged = Convert.FromHexString("0A000000140000006E000000DC000000");

        Assert.Equal(new ThumbClip(10, 20, 110, 220), ThumbBar.Clip(logged));
        Assert.Equal(100, ThumbBar.Clip(logged)!.Value.Width);
    }

    [Fact]
    public void AnEmptyOrShortClipIsNone()
    {
        Assert.Null(ThumbBar.Clip(new byte[16]));
        Assert.Null(ThumbBar.Clip(new byte[8]));
    }

    // --- Where they stand -------------------------------------------------------------------------

    [Fact]
    public void ThreeButtonsStandAtTheirSizeCentredUnderThePreview()
    {
        var places = ThumbBar.Place(3, 8, 100, 216, 32, 4);

        Assert.Equal([new RectF(64, 100, 32, 32), new RectF(100, 100, 32, 32), new RectF(136, 100, 32, 32)], places);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void EveryToolbarFitsTheBox(int count)
    {
        var places = ThumbBar.Place(count, 8, 100, 216, 32, 4);

        Assert.Equal(count, places.Count);
        Assert.True(places[0].X >= 8 && places[^1].Right <= 8 + 216, $"{count} buttons run from {places[0].X} to {places[^1].Right}");
        for (var index = 1; index < count; index++)
        {
            Assert.True(places[index].X >= places[index - 1].Right, "buttons overlap");
            Assert.Equal(places[0].Width, places[index].Width);
        }
        // Squares, and as large as the room lets them be.
        Assert.Equal(places[0].Width, places[0].Height);
        Assert.Equal(count <= 6 ? 32 : 30, places[0].Width);
    }

    [Fact]
    public void NoButtonsStandNowhere()
    {
        Assert.Empty(ThumbBar.Place(0, 0, 0, 216, 32, 4));
        Assert.Empty(ThumbBar.Place(3, 0, 0, 0, 32, 4));
    }

    // --- Where a click goes -------------------------------------------------------------------

    [Fact]
    public void AClickIsTheTaskbarsWmCommand()
    {
        // THBN_CLICKED over the id.
        Assert.Equal((nuint)0x1800_0066, ThumbBar.Command(102));
        Assert.Equal((nuint)0x1800_FFFF, ThumbBar.Command(0x1_FFFF));
    }

    [Fact]
    public void AClickOnAButtonGoesToItsWindow()
    {
        var sender = new FakeSender();

        var (sent, dismiss) = ThumbBar.Click(new ThumbButton(102, ThumbFlags.None, "Pause", null, -1), 0x1A0D56, sender);

        Assert.True(sent);
        Assert.False(dismiss);
        Assert.Equal([(0x1A0D56, 102u)], sender.Clicks);
    }

    [Fact]
    public void AButtonThatDismissesClosesTheList()
    {
        var sender = new FakeSender();

        var (sent, dismiss) = ThumbBar.Click(new ThumbButton(7, ThumbFlags.DismissOnClick, "", null, -1), 0x42, sender);

        Assert.True(sent);
        Assert.True(dismiss);
    }

    [Theory]
    [InlineData(ThumbFlags.Disabled)]
    [InlineData(ThumbFlags.NonInteractive)]
    [InlineData(ThumbFlags.Hidden)]
    [InlineData(ThumbFlags.Disabled | ThumbFlags.DismissOnClick)]
    public void AButtonThatTakesNoClicksSendsNothing(ThumbFlags flags)
    {
        var sender = new FakeSender();

        var (sent, dismiss) = ThumbBar.Click(new ThumbButton(7, flags, "", null, -1), 0x42, sender);

        Assert.False(sent);
        Assert.False(dismiss);
        Assert.Empty(sender.Clicks);
    }

    private sealed class FakeSender : IThumbSender
    {
        public List<(nint Window, uint Id)> Clicks { get; } = [];

        public void Clicked(nint window, uint id) => Clicks.Add((window, id));
    }
}
