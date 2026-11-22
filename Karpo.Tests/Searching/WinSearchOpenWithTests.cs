// §655: Windows search's "Open with" (the person, 2026-11-09: "open with does not work in the search widget."). It handed the
// file to the shell with the "openas" verb, which modern Windows answers with nothing for most files. It now asks Windows'
// own Open with dialog (SHOpenWithDialog) for the file, with the "Always use this app" box offered and the file opened in
// the app picked (OAIF_ALLOW_REGISTRATION | OAIF_EXEC), through the one launcher (§491) once the folder has gone. Windows is
// stood in for (LaunchStand): no dialog opens.

using Karpo.Dock;
using Karpo.Dock.WinSearch;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Searching;

public sealed class WinSearchOpenWithTests
{
    private const string Plan = @"C:\Users\me\Documents\plan.docx";

    [Fact]
    public void OpenWithAsksWindowsOpenWithDialogForTheFileOnceWhatIsOpenHasClosed()
    {
        using var stand = Launch.Stand();
        WinSearchLine.Launches(WinSearchAct.OpenWith, new WinSearchHit(WinSearchCategory.Documents, "plan.docx", Plan));
        Assert.Equal(2, stand.Said.Count);
        Assert.Equal("leave", stand.Said[0]);
        var launched = Assert.IsType<Launched>(stand.Said[1]);
        Assert.Equal(LaunchKind.OpenWith, launched.Kind);
        Assert.Equal(Plan, launched.Target);
        // The box "Always use this app" offered, and the file opened in the app picked: never the old "openas" verb.
        Assert.Equal((uint)(tagOPEN_AS_INFO_FLAGS.OAIF_ALLOW_REGISTRATION | tagOPEN_AS_INFO_FLAGS.OAIF_EXEC), launched.Flags);
        Assert.Null(launched.Verb);
    }

    [Fact]
    public void TheOtherActsGoWhereTheyWent()
    {
        using var stand = Launch.Stand();
        var hit = new WinSearchHit(WinSearchCategory.Documents, "plan.docx", Plan);
        WinSearchLine.Launches(WinSearchAct.Open, hit);
        WinSearchLine.Launches(WinSearchAct.Location, hit);
        WinSearchLine.Launches(WinSearchAct.Admin, hit);
        Assert.Equal(
            [LaunchKind.Open, LaunchKind.Select, LaunchKind.Elevated],
            stand.Said.OfType<Launched>().Select(launched => launched.Kind));
    }
}
