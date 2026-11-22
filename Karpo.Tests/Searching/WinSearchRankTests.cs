// §642: Windows search's results: what a name answers (whole, start, a word's start, initials, held, letters in order),
// which group an item of the index is in, the best match and the groups, each target once, Start's own shortcuts left
// to the apps, and what was opened before first.

namespace Karpo.Tests.Searching;

public sealed class WinSearchRankTests
{
    private static readonly DateTime Now = new(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);

    private static IReadOnlyList<string> W(string text) => WinSearchQuery.Words(text);

    [Theory]
    [InlineData("Paint", "paint", 100)]
    [InlineData("Paint 3D", "paint", 90)]
    [InlineData("Sticky Notes", "notes", 70)]
    [InlineData("Visual Studio Code", "vsc", 60)]
    [InlineData("Visual Studio Code", "visual code", 75)]
    [InlineData("PowerToys", "toys", 70)]
    [InlineData("Notepad", "pad", 40)]
    [InlineData("Notepad", "ntpd", 20)]
    [InlineData("Microsoft Edge", "edge", 70)]
    [InlineData("Café Menü.docx", "cafe menu", 90)]
    [InlineData("Straße", "STRASSE", 100)]
    [InlineData("Notepad", "zz", 0)]
    [InlineData("Notepad", "note zz", 0)]
    [InlineData("Notepad", "nt", 0)]
    [InlineData("Background settings", "code", 0)]
    [InlineData("Notepad", "tpd", 0)]
    public void ANameAnswersByHowItHoldsTheWords(string name, string typed, int score) =>
        Assert.Equal(score, WinSearchRank.NameScore(name, W(typed)));

    [Theory]
    [InlineData("C:\\Users\\me\\Pictures\\beach.JPG", ".JPG", "file:C:/Users/me/Pictures/beach.JPG", WinSearchCategory.Photos)]
    [InlineData("C:\\Users\\me\\Videos\\trip.mp4", ".mp4", "file:C:/x", WinSearchCategory.Videos)]
    [InlineData("C:\\Music\\song.flac", ".flac", "file:C:/x", WinSearchCategory.Music)]
    [InlineData("C:\\Users\\me\\Documents\\Taxes", "Directory", "file:C:/x", WinSearchCategory.Folders)]
    [InlineData("C:\\Users\\me\\Documents\\tax.docx", ".docx", "file:C:/x", WinSearchCategory.Documents)]
    [InlineData("C:\\tools\\run.exe", "", "file:C:/x", WinSearchCategory.Documents)]
    [InlineData("Inbox\\Invoice", "", "mapi://{S-1-5}/Inbox/Invoice", WinSearchCategory.Email)]
    public void AnItemOfTheIndexIsInItsGroup(string path, string type, string url, WinSearchCategory category) =>
        Assert.Equal(category, WinSearchRank.CategoryOf(new WinSearchRow(Path.GetFileName(path), path, url, type)));

    [Fact]
    public void StartsOwnShortcutsAreTheAppsNotFiles()
    {
        Assert.False(WinSearchRank.Lists(new WinSearchRow("Word.lnk", @"C:\ProgramData\Microsoft\Windows\Start Menu\Programs\Word.lnk", "file:x", ".lnk")));
        Assert.True(WinSearchRank.Lists(new WinSearchRow("Word.lnk", @"C:\Users\me\Desktop\Word.lnk", "file:x", ".lnk")));
        Assert.False(WinSearchRank.Lists(new WinSearchRow("", @"C:\x", "file:x", "")));
    }

    [Fact]
    public void AProgramRunsAsAdministratorAFileDoesNot()
    {
        Assert.True(new WinSearchHit(WinSearchCategory.Apps, "Terminal", @"shell:AppsFolder\Microsoft.WindowsTerminal").Elevates);
        Assert.True(new WinSearchHit(WinSearchCategory.Documents, "setup.exe", @"C:\x\setup.exe").Elevates);
        Assert.False(new WinSearchHit(WinSearchCategory.Documents, "notes.txt", @"C:\x\notes.txt").Elevates);
        Assert.True(new WinSearchHit(WinSearchCategory.Folders, "x", @"C:\x").OnDisk);
        Assert.False(new WinSearchHit(WinSearchCategory.Settings, "Display settings", "ms-settings:display").OnDisk);
    }

    private static WinSearchHit Hit(WinSearchCategory category, string name, string target, string typed, DateTime? modified = null, WinSearchHistory? opened = null)
    {
        var hit = new WinSearchHit(category, name, target) { Modified = modified };
        return hit with { Score = WinSearchRank.Score(hit, W(typed), Now, opened) };
    }

    [Fact]
    public void AnAppComesBeforeAPageAndAFileNamedAlike()
    {
        var typed = "paint";
        var results = WinSearchRank.Group(WinSearchQuery.Parse(typed),
        [
            Hit(WinSearchCategory.Documents, "paint.txt", @"C:\paint.txt", typed),
            Hit(WinSearchCategory.Settings, "Paint settings", "ms-settings:paint", typed),
            Hit(WinSearchCategory.Apps, "Paint", @"shell:AppsFolder\Paint", typed),
            Hit(WinSearchCategory.Folders, "Paintings", @"C:\Paintings", typed),
        ]);
        Assert.Equal("Paint", results.Best!.Name);
        Assert.Equal([WinSearchCategory.Settings, WinSearchCategory.Documents, WinSearchCategory.Folders], results.Groups.Select(group => group.Category));
        Assert.DoesNotContain(results.Groups, group => group.Hits.Contains(results.Best));
        Assert.Equal(1, results.Counts[WinSearchCategory.Apps]);
        Assert.Equal(4, results.All.Count());
    }

    [Fact]
    public void TheAnswerIsAlwaysTheBestMatchAndTheWebNeverWhileAnythingElseAnswers()
    {
        var ask = WinSearchQuery.Parse("12*3");
        var answer = new WinSearchHit(WinSearchCategory.Answer, "= 36", "36") { Score = 100_000 };
        var web = WinSearchRank.Web(ask, "https://example.com/?q=12*3")!;
        var app = new WinSearchHit(WinSearchCategory.Apps, "Calculator", "calc") { Score = 500 };
        Assert.Equal(answer, WinSearchRank.Group(ask, [web, answer, app]).Best);
        Assert.Equal(WinSearchCategory.Apps, WinSearchRank.Group(ask, [web, app]).Best!.Category);
        Assert.Equal(web, WinSearchRank.Group(ask, [web]).Best);
        Assert.Null(WinSearchRank.Web(ask, null));
        Assert.Null(WinSearchRank.Web(WinSearchQuery.Parse("apps: x"), "https://example.com"));
    }

    [Fact]
    public void EachTargetOnceAndNothingThatScoresNothing()
    {
        var typed = "report";
        var results = WinSearchRank.Group(WinSearchQuery.Parse(typed),
        [
            Hit(WinSearchCategory.Documents, "report.docx", @"C:\a\report.docx", typed),
            Hit(WinSearchCategory.Documents, "report.docx", @"c:\A\REPORT.docx", typed, Now),
            Hit(WinSearchCategory.Apps, "Calculator", "calc", typed),
        ]);
        Assert.Single(results.All);
        Assert.Equal(Now, results.Best!.Modified);
    }

    [Fact]
    public void EveryGroupIsHeldShortUnlessItIsTheOneFiltered()
    {
        var typed = "plan";
        var many = Enumerable.Range(0, 30).Select(i => Hit(WinSearchCategory.Documents, $"plan {i:00}.txt", $@"C:\plan{i}.txt", typed)).ToList();
        var all = WinSearchRank.Group(WinSearchQuery.Parse(typed), many);
        var documents = Assert.Single(all.Groups);
        Assert.Equal(WinSearchRank.MostPerGroup, documents.Hits.Count);
        Assert.Equal(30, documents.Found);
        Assert.Equal(30, all.Counts[WinSearchCategory.Documents]);
        var filtered = WinSearchRank.Group(WinSearchQuery.Parse("documents: plan"), many);
        Assert.Equal(29, Assert.Single(filtered.Groups).Hits.Count);
        // Another group's hits leave a filtered list.
        Assert.Empty(WinSearchRank.Group(WinSearchQuery.Parse("photos: plan"), many).All);
    }

    [Fact]
    public void AFresherFileAndOneOpenedBeforeComeFirst()
    {
        var typed = "notes";
        var old = Hit(WinSearchCategory.Documents, "notes.txt", @"C:\old\notes.txt", typed, Now.AddYears(-1));
        var fresh = Hit(WinSearchCategory.Documents, "notes.txt", @"C:\new\notes.txt", typed, Now.AddHours(-2));
        Assert.True(fresh.Score > old.Score);

        var history = new WinSearchHistory();
        history.Open(old, "no", Now);
        var opened = Hit(WinSearchCategory.Documents, "notes.txt", @"C:\old\notes.txt", typed, Now.AddYears(-1), history);
        Assert.True(opened.Score > fresh.Score);
        Assert.Equal(WinSearchHistory.OpenedBoost * 2, history.Boost(@"C:\OLD\notes.txt", "n"));
        Assert.Equal(WinSearchHistory.OpenedBoost, history.Boost(@"C:\old\notes.txt", "x"));
        Assert.Equal(0, history.Boost(@"C:\new\notes.txt", "notes"));
    }

    [Fact]
    public void TheIndexsRowsAreHitsInTheirGroupsTheirFolderUnderTheirName()
    {
        var rows = new[]
        {
            new WinSearchRow("tax 2025.xlsx", @"C:\Users\me\Documents\tax 2025.xlsx", "file:C:/Users/me/Documents/tax 2025.xlsx", ".xlsx", Now.AddDays(-3), 2048, 900),
            new WinSearchRow("Invoice", @"Inbox\Invoice", "mapi://{S}/Inbox/Invoice", "", null, null, 100),
            new WinSearchRow("Excel.lnk", @"C:\ProgramData\Microsoft\Windows\Start Menu\Programs\Excel.lnk", "file:x", ".lnk"),
            new WinSearchRow("budget notes.txt", @"C:\notes\budget notes.txt", "file:C:/notes/budget notes.txt", ".txt"),
        };
        var hits = WinSearchRank.FromIndex(WinSearchQuery.Parse("tax"), rows, Now, null).ToList();
        Assert.Equal(3, hits.Count);
        var tax = hits[0];
        Assert.Equal((WinSearchCategory.Documents, @"C:\Users\me\Documents", 2048L), (tax.Category, tax.Detail, tax.Size!.Value));
        Assert.Equal((WinSearchCategory.Email, "mapi://{S}/Inbox/Invoice"), (hits[1].Category, hits[1].Target));
        // Found by what it holds alone: listed, after what its name answers.
        Assert.True(hits[2].Score > 0 && hits[2].Score < tax.Score);
    }

    /// <summary>
    /// §661: a file of the index is its address's path, which the shell parses — its icon, opening it, its location and the
    /// path copied —, never the display path the index also gives, which names a known folder as File Explorer shows it
    /// ("Public Documents", a phone's "Internal storage"; 220 of 2,444 items on the person's PC). Red before: the hit's
    /// target was the display path, the shell parsed nothing, the row wore its group's glyph.
    /// </summary>
    [Theory]
    [InlineData(@"C:\Users\Public\Public Documents\Autodesk\Reports.ini", "file:C:/Users/Public/Documents/Autodesk/Reports.ini", @"C:\Users\Public\Documents\Autodesk\Reports.ini")]
    [InlineData(@"C:\Users\me\CrossDevice\S24\Internal storage\Download\a.pdf", "file:C:/Users/me/CrossDevice/S24/storage/Download/a.pdf", @"C:\Users\me\CrossDevice\S24\storage\Download\a.pdf")]
    [InlineData(@"C:\Users\me\Documents\plan.docx", "file:C:/Users/me/Documents/plan.docx", @"C:\Users\me\Documents\plan.docx")]
    [InlineData(@"\\server\share\x.txt", "file://server/share/x.txt", @"\\server\share\x.txt")]
    [InlineData(@"C:\a b\c.txt", "file:///C:/a b/c.txt", @"C:\a b\c.txt")]
    [InlineData(@"C:\odd\x.txt", "file:", @"C:\odd\x.txt")]
    public void AFileOfTheIndexIsItsAddresssPathNotItsDisplayPath(string display, string url, string path)
    {
        var row = new WinSearchRow(Path.GetFileName(path), display, url, Path.GetExtension(path), Now, 10, 900);
        Assert.Equal(path, WinSearchRank.PathOf(row));
        var hit = Assert.Single(WinSearchRank.FromIndex(WinSearchQuery.Parse(Path.GetFileNameWithoutExtension(path)[..1]), [row], Now, null));
        Assert.Equal((path, path, $"\"{path}\"", WinSearchRank.FolderOf(path)), (hit.Target, hit.Icon, hit.Copies, hit.Detail));
    }

    [Fact]
    public void TheAppsAnswerByTheirNamesAndOnlyWhereAppsMayBeListed()
    {
        WinSearchApp[] apps = [new("Visual Studio Code", @"shell:AppsFolder\Code"), new("Calculator", @"shell:AppsFolder\Calc"), new("Clock", @"shell:AppsFolder\Clock")];
        Assert.Equal(["Visual Studio Code"], WinSearchRank.FromApps(WinSearchQuery.Parse("vsc"), apps, Now, null).Select(hit => hit.Name));
        // "c": the start of Calculator and Clock, and of a word of Visual Studio Code.
        Assert.Equal(["Calculator", "Clock", "Visual Studio Code"], WinSearchRank.FromApps(WinSearchQuery.Parse("c"), apps, Now, null).Select(hit => hit.Name).Order());
        Assert.Empty(WinSearchRank.FromApps(WinSearchQuery.Parse("documents: calc"), apps, Now, null));
        Assert.Empty(WinSearchRank.FromApps(WinSearchQuery.Parse(""), apps, Now, null));
        Assert.All(WinSearchRank.FromApps(WinSearchQuery.Parse("calc"), apps, Now, null), hit => Assert.Equal(WinSearchWords.AppDetail, hit.Detail));
    }

    [Fact]
    public void SettingsPagesAnswerByTheirNameAndTheirOwnWords()
    {
        WinSearchHit[] Find(string typed) => [.. WinSearchPages.Find(WinSearchQuery.Parse(typed), Now, null).OrderByDescending(hit => hit.Score)];
        Assert.Equal(WindowsPages.Display.Uri, Find("display")[0].Target);
        Assert.Equal(WindowsPages.Display.Uri, Find("resolution")[0].Target);
        Assert.Equal(WindowsPages.Background.Uri, Find("wallpaper")[0].Target);
        Assert.Equal(WindowsPages.InstalledApps.Uri, Find("uninstall")[0].Target);
        Assert.Equal(WindowsPages.SearchingWindows.Uri, Find("indexing")[0].Target);
        // By a word of its own, at most a word of the name's: "sound" by its name before "bluetooth" by its word "headphones".
        Assert.True(Find("display")[0].Score > Find("resolution")[0].Score);
        Assert.Empty(Find("xyzzy"));
        Assert.Empty(WinSearchPages.Find(WinSearchQuery.Parse("apps: display"), Now, null));
        Assert.All(Find("sound"), hit => Assert.Equal((WinSearchCategory.Settings, WinSearchPages.Glyph), (hit.Category, hit.Glyph)));
    }

    [Fact]
    public void ASizeReadsAsFileExplorerSaysIt()
    {
        Assert.Equal("512 B", WinSearchRank.SizeText(512));
        Assert.Equal("1.5 KB", WinSearchRank.SizeText(1536));
        Assert.Equal("12 MB", WinSearchRank.SizeText(12L * 1024 * 1024));
        Assert.Equal("2 GB", WinSearchRank.SizeText(2L * 1024 * 1024 * 1024));
        Assert.Equal(@"C:\a", WinSearchRank.FolderOf(@"C:\a\b.txt"));
        Assert.Null(WinSearchRank.FolderOf("b.txt"));
    }
}
