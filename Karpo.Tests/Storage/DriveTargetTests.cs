// Which drive a Drive or Network drive widget points at, and what happens when
// that drive isn't there. All of it pure: no disk is read and no drive of the
// PC these tests run on decides anything (Karpo.Core\DriveTargets.cs and the
// dock's DriveWatch.Matches).

using Karpo.Core;
using Karpo.Dock;

namespace Karpo.Tests.Storage;

public sealed class DriveTargetTests
{
    private const string SystemVolume = @"\\?\Volume{11111111-2222-3333-4444-555555555555}\";
    private const string StickVolume = @"\\?\Volume{aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee}\";

    private static DriveVolume Local(string volume, string letter, string label, DriveKind kind = DriveKind.Fixed) =>
        new(volume, letter, label, kind, 1024UL * 1024 * 1024 * 931, 1024UL * 1024 * 1024 * 419);

    private static DriveVolume Share(string unc, string letter) =>
        new(unc, letter, "work (\\\\server)", DriveKind.Remote, 1024UL * 1024 * 1024 * 200, 1024UL * 1024 * 1024 * 50);

    private static DriveTargetOptions Target(string volume = "", string letter = "", string label = "") =>
        new() { Volume = volume, Letter = letter, Label = label };

    // --- The letter is normalised, and junk names no drive ---------------------

    [Theory]
    [InlineData("c", "C:")]
    [InlineData("C:", "C:")]
    [InlineData("c:\\", "C:")]
    [InlineData(" e:/ ", "E:")]
    [InlineData("", "")]
    [InlineData("::", "")]
    [InlineData("C:\\Users", "")]
    [InlineData("1:", "")]
    public void ADriveLetterIsOneLetterAndAColon(string given, string kept)
    {
        Assert.Equal(kept, DriveTargets.Letter(given));
    }

    [Fact]
    public void ALetterKnowsItsPlaceInTheAlphabetAndItsRoot()
    {
        Assert.Equal(0, DriveTargets.IndexOf("a:"));
        Assert.Equal(25, DriveTargets.IndexOf("Z:\\"));
        Assert.Equal(-1, DriveTargets.IndexOf("?"));
        Assert.Equal("C:\\", DriveTargets.Root("c"));
        Assert.Equal("", DriveTargets.Root(""));
    }

    [Fact]
    public void ADriveIsNamedAsExplorerNamesIt()
    {
        Assert.Equal("Local Disk (C:)", DriveTargets.Title("Local Disk", "c:"));
        Assert.Equal("C:", DriveTargets.Title("", "C:"));
        Assert.Equal("Backup", DriveTargets.Title("Backup", ""));
        Assert.Equal("Drive", DriveTargets.Title("", ""));
    }

    // --- The durable name decides, the letter only stands in ------------------

    [Fact]
    public void TheWidgetFollowsItsVolumeToAnotherLetter()
    {
        // The stick was E: when it was chosen and is F: now; nothing else changed.
        var target = Target(StickVolume, "E:", "SANDISK");
        var attached = new[] { Local(SystemVolume, "C:", "Local Disk"), Local(StickVolume, "F:", "SANDISK", DriveKind.Removable) };

        var found = DriveTargets.Find(DriveFilter.Local, target, attached);

        Assert.NotNull(found);
        Assert.Equal("F:", found!.Value.Letter);
        Assert.Equal(StickVolume, found.Value.Volume);
    }

    [Fact]
    public void AVolumeThatIsNotThereIsMissingRatherThanWhateverTookItsLetter()
    {
        // Another drive has taken E:. Following the letter would show the wrong drive's
        // free space under the name of one that isn't plugged in.
        var target = Target(StickVolume, "E:", "SANDISK");
        var attached = new[] { Local(SystemVolume, "C:", "Local Disk"), Local("\\\\?\\Volume{99999999-9999-9999-9999-999999999999}\\", "E:", "Photos", DriveKind.Removable) };

        Assert.Null(DriveTargets.Find(DriveFilter.Local, target, attached));
    }

    [Fact]
    public void AFileWithNoDurableNameFallsBackToItsLetter()
    {
        var target = Target(letter: "D:", label: "Data");
        var attached = new[] { Local(SystemVolume, "C:", "Local Disk"), Local(StickVolume, "D:", "Data") };

        Assert.Equal(StickVolume, DriveTargets.Find(DriveFilter.Local, target, attached)!.Value.Volume);
    }

    [Fact]
    public void ALetterOnlyMatchesAKindTheWidgetMayShow()
    {
        var target = Target(letter: "Z:");
        var attached = new[] { Share("\\\\server\\work", "Z:") };

        // A Drive widget never shows a network drive, and the other way round.
        Assert.Null(DriveTargets.Find(DriveFilter.Local, target, attached));
        Assert.NotNull(DriveTargets.Find(DriveFilter.Network, target, attached));
    }

    [Fact]
    public void ANetworkDriveIsNamedByItsShare()
    {
        var target = Target("\\\\server\\work", "Z:", "work (\\\\server)");
        // The share came back on another letter, as a remapped drive does.
        var attached = new[] { Share("\\\\SERVER\\Work", "Y:") };

        Assert.Equal("Y:", DriveTargets.Find(DriveFilter.Network, target, attached)!.Value.Letter);
    }

    [Fact]
    public void AWidgetWithNothingChosenFindsNothing()
    {
        Assert.Null(DriveTargets.Find(DriveFilter.Local, Target(), [Local(SystemVolume, "C:", "Local Disk")]));
        Assert.Null(DriveTargets.Find(DriveFilter.Local, null, [Local(SystemVolume, "C:", "Local Disk")]));
        Assert.False(Target().IsSet);
        Assert.True(Target(letter: "C:").IsSet);
    }

    [Fact]
    public void PointingAtADriveKeepsItsNameAndItsLetterWithIt()
    {
        var target = Target();

        DriveTargets.PointAt(target, Local(StickVolume, "e:", "SANDISK", DriveKind.Removable));

        Assert.Equal(StickVolume, target.Volume);
        Assert.Equal("E:", target.Letter);
        Assert.Equal("SANDISK", target.Label);
    }

    // --- What a missing drive says --------------------------------------------

    [Fact]
    public void AMissingDriveKeepsTheNameTheFileRemembers()
    {
        Assert.Equal("SANDISK (E:)", DriveTargets.MissingTitle(Target(StickVolume, "E:", "SANDISK")));
        Assert.Equal("No drive chosen", DriveTargets.MissingTitle(Target()));
        Assert.Equal("No drive chosen", DriveTargets.MissingTitle(null));

        Assert.Contains("isn't connected", DriveTargets.MissingLine(DriveFilter.Local, Target(letter: "E:")));
        Assert.Contains("network drive", DriveTargets.MissingLine(DriveFilter.Network, Target(letter: "Z:")));
        Assert.Contains("No drive chosen", DriveTargets.MissingLine(DriveFilter.Local, Target()));
    }

    // --- Which devices count as removable -------------------------------------

    [Theory]
    [InlineData(DriveKind.Removable, true)]
    [InlineData(DriveKind.Optical, true)]
    [InlineData(DriveKind.Fixed, false)]
    [InlineData(DriveKind.Remote, false)]
    [InlineData(DriveKind.RamDisk, false)]
    [InlineData(DriveKind.Unknown, false)]
    public void RemovableMeansMediaThatComesOut(DriveKind kind, bool removable)
    {
        Assert.Equal(removable, kind.IsRemovable());
        Assert.Equal(removable, DriveTargets.Fits(DriveFilter.Removable, kind));
    }

    [Theory]
    [InlineData(DriveKind.Fixed, true)]
    [InlineData(DriveKind.Removable, true)]
    [InlineData(DriveKind.Optical, true)]
    [InlineData(DriveKind.RamDisk, true)]
    [InlineData(DriveKind.Remote, false)]
    [InlineData(DriveKind.NoRootDirectory, false)]
    public void ADriveWidgetShowsAnythingButANetworkDrive(DriveKind kind, bool local)
    {
        Assert.Equal(local, kind.IsLocal());
        Assert.Equal(local, DriveTargets.Fits(DriveFilter.Local, kind));
    }

    // --- The dock's own matching, which the bar's cell uses --------------------

    [Fact]
    public void TheDockMatchesADriveTheSameWay()
    {
        var target = Target(StickVolume, "E:", "SANDISK");
        var moved = new Drive { Letter = 5, Kind = DriveKind.Removable, Volume = StickVolume, Label = "SANDISK" };
        var other = new Drive { Letter = 4, Kind = DriveKind.Removable, Volume = "\\\\?\\Volume{00000000-0000-0000-0000-000000000000}\\", Label = "Photos" };

        Assert.True(DriveWatch.Matches(DriveFilter.Local, target, moved));
        Assert.False(DriveWatch.Matches(DriveFilter.Local, target, other));
        // A drive that is only a remembered target matches nothing, ever.
        Assert.False(DriveWatch.Matches(DriveFilter.Local, target, Drive.Absent(target)));
    }

    [Fact]
    public void AnAbsentDriveKeepsWhatTheFileKnew()
    {
        var absent = Drive.Absent(Target(StickVolume, "E:", "SANDISK"));

        Assert.True(absent.Missing);
        Assert.Equal(4, absent.Letter);
        Assert.Equal("E:", absent.Name);
        Assert.Equal("E:\\", absent.Root);
        Assert.Equal("SANDISK (E:)", absent.Title);

        // A target with no letter at all still gives a drive, with no root to read.
        var nowhere = Drive.Absent(Target(StickVolume));
        Assert.Equal(-1, nowhere.Letter);
        Assert.Equal("", nowhere.Name);
        Assert.Equal("", nowhere.Root);
    }

    [Fact]
    public void AShareIsNamedAsExplorerNamesAMappedDrive()
    {
        Assert.Equal("work (\\\\server)", DriveWatch.ShareLabel("\\\\server\\work"));
        Assert.Equal("\\\\server", DriveWatch.ShareLabel("\\\\server"));
    }
}
