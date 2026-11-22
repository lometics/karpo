// A network drive that is not connected (2026-10-09: "the not connected state of a network drive
// widget should show up in the same style as win11 would show not connected"), held where it is
// arithmetic or a rule rather than a picture:
//
//   * what a share's answer is (DriveWatch.Reading): a connection Windows remembers but has not made
//     is not there, and its share is not asked anything — reading it would try to reconnect, and on
//     a share that is gone the read waits for the network;
//   * which drive a widget is handed (DriveWatch.Pick): a share whose first answer is still out is
//     pending, never disconnected, so a reachable share never flashes its red X at start;
//   * the cell without a capacity bar (DriveIsland.Rows): the name has the bar's room, and the
//     widget's size does not follow (DriveWidgetOffscreenTests holds that, and draws the pictures).

using Karpo.Core;
using Karpo.Dock;

namespace Karpo.Tests.Storage;

public sealed class DisconnectedDriveTests
{
    private const string Share = @"\\server\work";

    private static DriveTargetOptions Target() => new() { Volume = Share, Letter = "Z:", Label = @"work (\\server)" };

    // --- A share's answer -----------------------------------------------------------------------

    [Fact]
    public void ARememberedConnectionWindowsHasNotMadeIsNotThereAndItsShareIsNotAsked()
    {
        var asked = 0;
        var reading = DriveWatch.Reading(DriveWatch.ConnectionUnavailable, Share, () => { asked++; return (100, 50); }, () => "work");
        Assert.False(reading.Ok);
        Assert.Equal(Share, reading.Share);
        Assert.Equal(0, asked);
    }

    [Fact]
    public void AConnectedShareIsThereWhenItsSpaceIsRead()
    {
        var reading = DriveWatch.Reading(0, Share, () => (100, 40), () => @"work (\\server)");
        Assert.Equal(new DriveReading(true, 100, 40, @"work (\\server)", Share), reading);
    }

    [Fact]
    public void AConnectedShareWhoseSpaceCannotBeReadIsNotThere()
    {
        var labelled = 0;
        var reading = DriveWatch.Reading(0, Share, () => null, () => { labelled++; return "work"; });
        Assert.False(reading.Ok);
        Assert.Equal(0, labelled);
    }

    // --- Which drive a widget is handed ---------------------------------------------------------

    private static Drive Pending(int letter) => new() { Letter = letter, Kind = DriveKind.Remote, Pending = true };

    private static Drive Connected() => new() { Letter = 25, Kind = DriveKind.Remote, Volume = Share, Label = @"work (\\server)", Total = 100, Free = 40 };

    [Fact]
    public void AShareWhoseFirstAnswerIsOutIsPendingNotDisconnected()
    {
        var drive = DriveWatch.Pick(DriveFilter.Network, Target(), [Pending(25)]);
        Assert.True(drive.Missing);
        Assert.True(drive.Pending);
    }

    [Fact]
    public void AShareNotListedAtAllIsMissingAndNotPending()
    {
        var drive = DriveWatch.Pick(DriveFilter.Network, Target(), []);
        Assert.True(drive.Missing);
        Assert.False(drive.Pending);
    }

    [Fact]
    public void AConnectedShareIsFoundBesideOneStillPending()
    {
        var drive = DriveWatch.Pick(DriveFilter.Network, Target(), [Pending(24), Connected()]);
        Assert.False(drive.Missing);
        Assert.Equal(Share, drive.Volume);
    }

    [Fact]
    public void APlaceholderIsNeverTheDriveAWidgetNames()
    {
        // Even by its letter alone, where the file has no share.
        var byLetter = new DriveTargetOptions { Letter = "Z:" };
        Assert.False(DriveWatch.Matches(DriveFilter.Network, byLetter, Pending(25)));
        // And a widget with no drive chosen is not pending: it has nothing to wait for.
        Assert.False(DriveWatch.Pick(DriveFilter.Network, new DriveTargetOptions(), [Pending(25)]).Pending);
        // Nor is a local drive's widget, which never lists a share.
        Assert.False(DriveWatch.Pick(DriveFilter.Local, Target(), [Pending(25)]).Pending);
    }

    // --- The cell without a bar -----------------------------------------------------------------

    [Theory]
    [InlineData(28, false)]
    [InlineData(48, false)]
    [InlineData(72, false)]
    [InlineData(48, true)]
    [InlineData(72, true)]
    public void WithoutACapacityBarTheNameHasTheBarsRoom(int height, bool vertical)
    {
        var config = new Config { Height = height };
        var look = new Look(Glass: false, Dark: true, Accent: Windows.UI.Color.FromArgb(255, 0x00, 0x78, 0xD4));
        var style = new IslandStyle(1f, vertical, Layout.CellPx(96, config), Layout.IconPx(96, config), look);
        var fit = new Fit(style);
        var (width, tall) = vertical ? (style.Cell, 2 * style.Cell) : (2 * style.Cell, style.Cell);
        var with = DriveIsland.Rows(fit, width, tall, leastName: 10);
        var without = DriveIsland.Rows(fit, width, tall, leastName: 10, capacity: false);
        Assert.False(without.WithBar);
        if (!vertical)
        {
            // In a row the icon stays where it was: only what follows it changes.
            Assert.Equal(with.Icon, without.Icon);
            Assert.True(with.WithBar);
            Assert.True(without.WithName);
            Assert.Equal(without.Right, without.NameRight);
            Assert.True(without.NameRight > with.NameRight);
        }
        // Everything still inside the cell.
        Assert.True(without.Top >= 0 && without.Bottom <= tall, $"{without.Top}..{without.Bottom} of {tall}");
    }
}
