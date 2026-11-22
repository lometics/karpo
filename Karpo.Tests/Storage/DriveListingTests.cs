// The pure parts of a drive's line (Karpo.Dock\Drives\DriveContents.cs), the
// drive flyout's until it went: the figures the header is written from and where
// the alarm colour starts, the order and the cap the list uses, and which icon a
// row asks for. Nothing here touches a disk.

using Karpo.Core;
using Karpo.Dock.Drives;

namespace Karpo.Tests.Storage;

public sealed class DriveListingTests
{
    private static ulong Gb(double gigabytes) => (ulong)(gigabytes * 1024 * 1024 * 1024);

    // GetDriveTypeW's answers, as Karpo.Dock uses them.
    private const DriveKind Fixed = DriveKind.Fixed;
    private const DriveKind Removable = DriveKind.Removable;
    private const DriveKind Remote = DriveKind.Remote;
    private const DriveKind CdRom = DriveKind.Optical;

    [Fact]
    public void FiguresReadAsExplorerWritesThem()
    {
        var total = Gb(931);
        var free = Gb(419);
        Assert.Equal("512 of 931 GB used", DriveListing.UsedOfTotal(total, free));
        Assert.Equal("419 GB free", DriveListing.FreeSpace(total, free));
        Assert.Equal("55%", DriveListing.PercentUsed(total, free));
        // The unit follows the whole: a small stick counts in megabytes.
        Assert.Equal("12.0 of 64.0 MB used", DriveListing.UsedOfTotal(64 * 1024 * 1024, 52 * 1024 * 1024));
        Assert.Equal("1.8 TB free", DriveListing.FreeSpace(Gb(2048), Gb(1843)));
    }

    [Fact]
    public void FreeSpaceNeverReadsAsMoreThanTheDrive()
    {
        // A quota can leave more "available" than the volume's own free bytes.
        Assert.Equal("100 GB free", DriveListing.FreeSpace(Gb(100), Gb(220)));
        Assert.Equal(0f, DriveListing.Fraction(Gb(100), Gb(220)));
    }

    [Fact]
    public void NoSizeMeansNoFraction()
    {
        Assert.Equal(0f, DriveListing.Fraction(0, 0));
        Assert.False(DriveListing.NearlyFull(0, 0));
        Assert.Equal("0%", DriveListing.PercentUsed(0, 0));
    }

    [Fact]
    public void TheAlarmStartsPastNineTenthsUsed()
    {
        // Exactly a tenth left is not yet the alarm, as the bar on the dock has it.
        Assert.False(DriveListing.NearlyFull(1000, 100));
        Assert.True(DriveListing.NearlyFull(1000, 99));
        Assert.True(DriveListing.NearlyFull(1000, 0));
        Assert.False(DriveListing.NearlyFull(1000, 900));
        Assert.Equal(0.9, DriveListing.Fraction(1000, 100), 5);
        Assert.Equal(0.9f, DriveListing.FullAt);
    }

    [Fact]
    public void FoldersComeFirstAndNamesReadAsPeopleReadThem()
    {
        var entries = new List<DriveEntry>
        {
            new("b.txt", false, 10, default),
            new("Zeta", true, 0, default),
            new("A.txt", false, 10, default),
            new("alpha", true, 0, default),
            new("File 10.log", false, 10, default),
            new("File 2.log", false, 10, default),
        };
        Assert.Equal(
            new[] { "alpha", "Zeta", "A.txt", "b.txt", "File 2.log", "File 10.log" },
            DriveListing.Sort(entries).Select(entry => entry.Name));
    }

    [Fact]
    public void NumbersInNamesCountAsNumbers()
    {
        Assert.True(DriveListing.NaturalCompare("img2", "img10") < 0);
        Assert.True(DriveListing.NaturalCompare("img10", "img2") > 0);
        Assert.Equal(0, DriveListing.NaturalCompare("Photo", "photo"));
        // The same number written with more zeros sorts after the plainer one.
        Assert.True(DriveListing.NaturalCompare("v1", "v01") < 0);
        Assert.True(DriveListing.NaturalCompare("a", "ab") < 0);
        Assert.True(DriveListing.NaturalCompare("1.txt", "2.txt") < 0);
    }

    [Fact]
    public void NamesThatDifferOnlyInCaseKeepAFixedOrder()
    {
        var one = DriveListing.Sort(new DriveEntry[] { new("Readme", false, 0, default), new("readme", false, 0, default) });
        var other = DriveListing.Sort(new DriveEntry[] { new("readme", false, 0, default), new("Readme", false, 0, default) });
        Assert.Equal(one.Select(entry => entry.Name), other.Select(entry => entry.Name));
    }

    [Fact]
    public void PastTheBoundTheRestAreCounted()
    {
        var entries = Enumerable.Range(0, 250).Select(i => new DriveEntry($"file{i}", false, 0, default)).ToList();
        var (shown, unlisted) = DriveListing.Cap(entries, 100);
        Assert.Equal(100, shown.Count);
        Assert.Equal(150, unlisted);
        // What the reader itself stopped short of listing counts too.
        Assert.Equal(1150, DriveListing.Cap(entries, 100, beyond: 1000).Unlisted);
    }

    [Fact]
    public void AShortListIsNotCapped()
    {
        var entries = new List<DriveEntry> { new("one", true, 0, default), new("two", false, 0, default) };
        var (shown, unlisted) = DriveListing.Cap(entries, 100);
        Assert.Equal(2, shown.Count);
        Assert.Equal(0, unlisted);
    }

    [Fact]
    public void EveryFileOfAKindSharesOneIcon()
    {
        Assert.Equal(DriveListing.FolderIcon, DriveListing.IconKey(new DriveEntry("Windows", true, 0, default)));
        Assert.Equal(".txt", DriveListing.IconKey(new DriveEntry("notes.TXT", false, 0, default)));
        Assert.Equal(".txt", DriveListing.IconKey(new DriveEntry("other.txt", false, 0, default)));
        // Programs, shortcuts and icons carry their own picture, so each is asked for on its own.
        Assert.Equal("setup.exe", DriveListing.IconKey(new DriveEntry("Setup.exe", false, 0, default)));
        Assert.Equal("karpo.lnk", DriveListing.IconKey(new DriveEntry("Karpo.lnk", false, 0, default)));
        // A name that is nothing but an extension has none, as Explorer reads it.
        Assert.Equal(DriveListing.PlainIcon, DriveListing.IconKey(new DriveEntry(".gitignore", false, 0, default)));
        Assert.Equal(DriveListing.PlainIcon, DriveListing.IconKey(new DriveEntry("README", false, 0, default)));
    }

    [Fact]
    public void RowsShowADateWhenThereIsOneAndNeverASize()
    {
        Assert.Equal("", DriveListing.ModifiedOf(new DriveEntry("no date", false, 0, default)));
        Assert.NotEqual("", DriveListing.ModifiedOf(new DriveEntry("dated", false, 0, new DateTime(2026, 9, 22, 18, 44, 0, DateTimeKind.Local))));
    }

    [Fact]
    public void EachKindOfDriveHasItsOwnWords()
    {
        Assert.Equal("Local disk", DriveListing.TypeName(Fixed));
        Assert.Equal("Removable drive", DriveListing.TypeName(Removable));
        Assert.Equal("Network drive", DriveListing.TypeName(Remote));
        Assert.Equal("Optical drive", DriveListing.TypeName(CdRom));
        Assert.Equal("This network drive can't be reached", DriveListing.NotReadyLine(Remote));
        Assert.Equal("No disc in the drive", DriveListing.NotReadyLine(CdRom));
        Assert.Equal("No media in the drive", DriveListing.NotReadyLine(Removable));
        Assert.Equal("The drive isn't ready", DriveListing.NotReadyLine(Fixed));
    }

    [Fact]
    public void TheHeaderNamesTheDriveAndSaysWhatItIs()
    {
        Assert.Equal("Local Disk (C:)", DriveListing.Title("Local Disk", "C:"));
        Assert.Equal("D:", DriveListing.Title("", "D:"));
        Assert.Equal("Local disk · NTFS · BitLocker On", DriveListing.Subtitle(Fixed, "NTFS", "BitLocker On"));
        // Only what is known is named.
        Assert.Equal("Local disk · NTFS", DriveListing.Subtitle(Fixed, "NTFS", ""));
        Assert.Equal("Network drive", DriveListing.Subtitle(Remote, "", ""));
    }
}
