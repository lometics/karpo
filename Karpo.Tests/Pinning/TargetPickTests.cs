// One "Browse…" for a pin's or a shortcut's target, a file and a folder alike: what Windows' open
// dialog hands back stands for which target (Karpo.Core\TargetPicks.cs), and where the dialog
// starts. Rules on plain values, so tested without a dialog; whether a path is a folder is a fake.

namespace Karpo.Tests.Pinning;

public sealed class TargetPickTests
{
    private const string Documents = @"C:\Users\Ann\Documents";
    private const string Reports = @"C:\Users\Ann\Documents\Reports";
    private const string Letter = @"C:\Users\Ann\Documents\letter.docx";

    private static bool Folders(string path) =>
        path.TrimEnd('\\') is var trimmed
        && (string.Equals(trimmed, Documents, StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, Reports, StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, @"C:\Users\Ann", StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, @"C:\Users", StringComparison.OrdinalIgnoreCase)
            || string.Equals(path, @"C:\", StringComparison.OrdinalIgnoreCase));

    // --- What the dialog picked ------------------------------------------------------------------

    /// <summary>Open on a file: the file, whatever the folder button would have taken.</summary>
    [Fact]
    public void OpenPicksTheFile()
    {
        Assert.Equal(Letter, TargetPicks.Picked(TargetPickEnd.Opened, Letter, Reports, Documents, Folders));
        Assert.Equal(Letter, TargetPicks.Picked(TargetPickEnd.Opened, "  " + Letter + " ", null, null, Folders));
    }

    /// <summary>"Select folder" with a folder selected in the list: that folder, not the one the dialog is in.</summary>
    [Fact]
    public void TheFolderButtonPicksTheSelectedFolder()
    {
        Assert.Equal(Reports, TargetPicks.Picked(TargetPickEnd.Folder, null, Reports, Documents, Folders));
    }

    /// <summary>"Select folder" with nothing selected, or a file selected: the folder the dialog is in.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(Letter)]
    public void TheFolderButtonPicksTheCurrentFolderOtherwise(string? selected)
    {
        Assert.Equal(Documents, TargetPicks.Picked(TargetPickEnd.Folder, null, selected, Documents, Folders));
    }

    /// <summary>A drive's root is a folder like any other.</summary>
    [Fact]
    public void TheFolderButtonPicksADrive()
    {
        Assert.Equal(@"C:\", TargetPicks.Picked(TargetPickEnd.Folder, null, null, @"C:\", Folders));
    }

    /// <summary>
    /// This PC, Quick access or a library have no path, and a file isn't a folder: the button has
    /// nothing to take (it stays grey), and pressing it picks nothing.
    /// </summary>
    [Theory]
    [InlineData(null, null)]
    [InlineData(Letter, null)]
    [InlineData(null, " ")]
    [InlineData(@"C:\gone", @"D:\gone too")]
    public void TheFolderButtonHasNothingWithoutAFolder(string? selected, string? current)
    {
        Assert.Null(TargetPicks.Folder(selected, current, Folders));
        Assert.Null(TargetPicks.Picked(TargetPickEnd.Folder, null, selected, current, Folders));
    }

    /// <summary>A cancel picks nothing, whatever was selected, and an Open with no path neither.</summary>
    [Fact]
    public void CancelPicksNothing()
    {
        Assert.Null(TargetPicks.Picked(TargetPickEnd.Cancelled, Letter, Reports, Documents, Folders));
        Assert.Null(TargetPicks.Picked(TargetPickEnd.Cancelled, null, null, null, Folders));
        Assert.Null(TargetPicks.Picked(TargetPickEnd.Opened, null, Reports, Documents, Folders));
        Assert.Null(TargetPicks.Picked(TargetPickEnd.Opened, "   ", Reports, Documents, Folders));
    }

    // --- Where the dialog starts -----------------------------------------------------------------

    /// <summary>A file starts the dialog in its folder; a folder in the folder above it, beside its siblings.</summary>
    [Theory]
    [InlineData(Letter, Documents)]
    [InlineData(Reports, Documents)]
    [InlineData(Reports + @"\", Documents)]
    [InlineData("\"" + Letter + "\"", Documents)]
    [InlineData(Documents, @"C:\Users\Ann")]
    public void StartsBesideTheTarget(string near, string start)
    {
        Assert.Equal(start, TargetPicks.StartIn(near, Folders));
    }

    /// <summary>"Locate…" on a moved file: the nearest folder above it this PC still has.</summary>
    [Fact]
    public void StartsInTheNearestFolderStillThere()
    {
        Assert.Equal(Documents, TargetPicks.StartIn(@"C:\Users\Ann\Documents\Old\2019\tax.pdf", Folders));
    }

    /// <summary>
    /// A web address, an app, a relative name, a drive's root, an empty box or a drive that isn't
    /// there: no start of its own, so the dialog starts where it last was.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("https://example.com/page")]
    [InlineData(@"shell:AppsFolder\Microsoft.WindowsCalculator_8wekyb3d8bbwe!App")]
    [InlineData("notepad.exe")]
    [InlineData(@"C:\")]
    [InlineData(@"Q:\Archive\file.txt")]
    public void StartsNowhereInParticularOtherwise(string? near)
    {
        Assert.Null(TargetPicks.StartIn(near, Folders));
    }
}
