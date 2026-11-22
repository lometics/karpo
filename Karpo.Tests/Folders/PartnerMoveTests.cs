// What the dock no longer carries anywhere (Karpo.Core\Partners.cs, 2026-10-09): nothing moves
// between the taskbar and a folder — the edits that did (into a folder, out of one, two widgets
// folded into a new folder, "Ungroup") are gone, and so is the drop target that a folder's button on
// the bar was — and since one folder stands open at a time (2026-10-09, "only one folder can be open
// at a time") there is no second open folder to carry a line into, so the move between two folders
// is gone too. A line moves inside its own folder: between its lists, or into a new one
// (FolderModelTests, Folders.Move).

using System.Reflection;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Folders;

public sealed class PartnerMoveTests
{
    /// <summary>
    /// The edits that carried a widget between the taskbar and a folder, or between two open
    /// folders, are gone from the dock, not only unused.
    /// </summary>
    [Theory]
    [InlineData("IntoFolder")]
    [InlineData("MoveIntoFolder")]
    [InlineData("OutOfFolder")]
    [InlineData("MoveOutOfFolder")]
    [InlineData("FolderOfTwo")]
    [InlineData("MakeFolder")]
    [InlineData("Ungrouped")]
    [InlineData("Ungroup")]
    [InlineData("BetweenFolders")]
    [InlineData("MoveBetweenFolders")]
    [InlineData("SettleFolders")]
    public void TheDockHasNoEditBetweenTheTaskbarAndAFolderOrTwoFolders(string name) =>
        Assert.Null(typeof(TheDock).GetMethod(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic));
}
