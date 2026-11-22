// pins: what a pin opens (2026-10-22, the person: "path and what it opens should be combined"; docs\widgets.md
// §292). §380 (2026-10-28: "what it opens, for the pins should show the pin icons and the proper names, not the
// weird ass names"; "what it opens should also show a list of apps if possible"): every target by the name
// Windows gives it and with its picture. §425 (2026-10-30: "for the pin widget it should be more clear to add an
// app, folder, website, etc. right now the user does not see that he can add a website for example. put them
// under separate menus, its okey if it is more than two levels deep in the end"): Target's level is a choice of
// kind first — App, Folder, File, Website, Settings page —, each a level of its own, the kind in force ticked
// and saying the target; a pick goes back to the kinds. A folder's contents keep one level, folders alone.
// Rows of Karpo.Core\OptionFields.cs (OfPinTarget, the finders) and the menu's (Karpo.Dock\OptionMenu.cs),
// tested without a window; one real menu (KARPO_UI_TESTS=1); the folder listing on a folder of the test's own.

using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using Karpo.Tests.Localization;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Tests.Folders;

[Collection(InLanguage.Collection)]
public sealed class PinTargetMenuTests
{
    private const string ExplorerLink = @"C:\Users\Ann\AppData\Roaming\Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar\File Explorer.lnk";
    private const string SpotifyApp = @"shell:AppsFolder\SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify";
    private const string CalculatorApp = @"shell:AppsFolder\Microsoft.WindowsCalculator_8wekyb3d8bbwe!App";
    private const string ThisPc = "shell:MyComputerFolder";
    private const string Report = @"C:\Users\Ann\Documents\Report.docx";

    private static readonly OptionFound Documents = TargetChoices.Row("Documents", @"C:\Users\Ann\Documents", OptionGlyphs.Document);
    private static readonly OptionFound Downloads = TargetChoices.Row("Downloads", @"C:\Users\Ann\Downloads", OptionGlyphs.Download);
    private static readonly OptionFound Calculator = TargetChoices.App("Calculator", CalculatorApp);
    private static readonly OptionFound Spotify = TargetChoices.App("Spotify", SpotifyApp);

    /// <summary>The names Windows gives the targets, as the dock asks the shell.</summary>
    private static readonly Dictionary<string, string> Names = new(StringComparer.OrdinalIgnoreCase)
    {
        [ExplorerLink] = "File Explorer",
        [SpotifyApp] = "Spotify",
        [ThisPc] = "This PC",
        [@"C:\Work"] = "Work",
    };

    private static readonly OptionDevices Sources = new()
    {
        Targets = new TargetChoices
        {
            Folders = [Documents, Downloads],
            Places = [TargetChoices.Row("This PC", ThisPc, OptionGlyphs.Monitor)],
            Files = [TargetChoices.File("Report.docx", Report)],
            Sites = [TargetChoices.Site("example.org", "https://example.org")],
            Apps = [Calculator, Spotify],
            Name = target => Names.GetValueOrDefault(target.Trim()) ?? "",
            IsFolder = path => path is @"C:\Work" or @"C:\Users\Ann\Documents" or @"C:\Users\Ann\Downloads",
        },
    };

    /// <summary>The kinds, in their order, by their words.</summary>
    private static readonly string[] Kinds = [S.SettingsPinKindApp, S.SettingsPinKindFolder, S.SettingsPinKindFile, S.SettingsPinKindWebsite, S.SettingsPinKindSettings];

    private static (WidgetItem Pin, OptionPlace Place) FolderPin(string target = @"C:\Work", WidgetItem? item = null)
    {
        var pin = item ?? BarKind.Pin.New();
        pin.Pin!.Target = target;
        return (pin, new OptionPlace(new Config(), Dark: true, Exists: path => path == @"C:\Work"));
    }

    private static OptionRow Target(WidgetItem pin, OptionPlace place) => OptionRows.Of(pin, Sources, place).Single(row => row.Key == OptionPickers.PinTarget);

    /// <summary>The kind <paramref name="key"/>'s row on Target's level.</summary>
    private static OptionRow Kind(WidgetItem pin, OptionPlace place, string key) => Target(pin, place).Level.Single(row => row.Key == key);

    /// <summary>
    /// §425: Target is one row, the target's name at its end and its picture with it; its level the kinds, each a
    /// level of its own — App, Folder, File, Website, Settings page —, the one in force ticked and saying the target,
    /// the rest saying nothing; each a level of one choice, so a pick goes back. A folder's contents keep their one
    /// level: "Browse…" over their field, folders alone.
    /// </summary>
    [Fact]
    public void TheTargetLevelOffersEachKindItsOwnLevel()
    {
        var (pin, place) = FolderPin();
        var rows = OptionRows.Of(pin, Sources, place);
        List<OptionPick> picks = [];
        var picture = new FlyoutIcon.Glyph("P");
        var menu = OptionMenu.Rows(rows, picks, pictures: address => address == PinTargets.PictureOf(@"C:\Work") ? picture : null);
        Assert.Equal([S.SettingsPinTargetHeader, S.SettingsPinNameHeader, S.SettingsFolderIconHeader], menu.Select(row => row.Text));
        var target = menu[0];
        Assert.Equal(("Work", picture), (target.Value!.Text, target.Value.Icon));
        Assert.Equal(Kinds, target.Children!.Select(row => row.Text));
        Assert.All(target.Children!, kind => Assert.True(kind.Children is { Count: > 0 } && kind.PickOne, kind.Text));
        Assert.Equal([false, true, false, false, false], target.Children!.Select(kind => kind.Checked == true));
        var folder = target.Children![1];
        Assert.Equal("Work", folder.Value!.Text);
        Assert.All(target.Children!.Where(kind => kind != folder), kind => Assert.Null(kind.Value));
        // Each kind wears its own icon.
        Assert.Equal(5, target.Children!.Select(kind => ((FlyoutIcon.Glyph)kind.Icon!).Code).Distinct().Count());

        // Nothing yet: it says so, greyed, and no kind is ticked.
        var (empty, at) = FolderPin("");
        var none = OptionMenu.Rows(OptionRows.Of(empty, Sources, at), [])[0];
        Assert.Equal((S.SettingsPinTargetNone, true), (none.Value!.Text, none.Value.Implied));
        Assert.All(none.Children!, kind => Assert.Equal(false, kind.Checked));

        // A folder's contents: their folder, by its own words, "Browse…" over the field of folders, and how a subfolder opens.
        var (contents, there) = FolderPin(item: ListKind.FolderContents.New());
        List<OptionPick> listedPicks = [];
        var listed = OptionMenu.Rows(OptionRows.Of(contents, Sources, there), listedPicks);
        Assert.Equal(S.SettingsFolderContentsTargetHeader, listed[0].Text);
        Assert.Equal((S.SettingsPinTargetBrowse, OptionPickKind.Browse), (listed[0].Children![0].Text, OptionMenu.PickOf(listedPicks, listed[0].Children![0].Id)!.Kind));
        Assert.IsType<FlyoutField>(listed[0].Children![1].Control);
        Assert.Contains(S.SettingsPinNavigationHeader, listed.Select(row => row.Text));
    }

    /// <summary>
    /// §425: each kind's level offers its own, before anything is typed (§411, R6): the apps as Start lists them;
    /// "Browse…" for a folder and the folders — This PC first, then the person's and Windows' own —; "Browse…" for
    /// a file and the files used lately; a website's address typed; the pages of Windows' Settings. Each a Find
    /// field but the website's, a line of text that opens with the address in force.
    /// </summary>
    [Fact]
    public async Task EachKindOffersItsOwn()
    {
        var (pin, place) = FolderPin(@"C:\Users\Ann\Documents\");
        List<OptionPick> picks = [];
        var kinds = OptionMenu.Rows(OptionRows.Of(pin, Sources, place), picks)[0].Children!;
        FlyoutField Field(int at) => (FlyoutField)kinds[at].Children!.Single(row => row.Control is FlyoutField).Control!;
        async Task<IReadOnlyList<OptionFound>> Offered(int at) => await Field(at).Finder!.FindAsync("", CancellationToken.None);

        // App: the apps, by their names alone.
        Assert.Equal((true, "", S.SettingsPinAppPlaceholder), (Field(0).Finds, Field(0).Text, Field(0).Placeholder));
        Assert.Single(kinds[0].Children!);
        Assert.Equal(["Calculator", "Spotify"], (await Offered(0)).Select(row => row.Title));
        Assert.All(await Offered(0), row => Assert.Equal(("", PinTargets.PictureOf((string)row.Value!)), (row.Detail, row.Picture)));
        // Words find an app by its name; a path is no app.
        Assert.Equal(["Spotify"], (await Field(0).Finder!.FindAsync("spot", CancellationToken.None)).Select(row => row.Title));
        Assert.Empty(await Field(0).Finder!.FindAsync(@"C:\", CancellationToken.None));

        // Folder: "Browse…" for a folder, then This PC and the folders, the one in force ticked.
        Assert.Equal((S.SettingsPinTargetBrowse, OptionPickKind.Browse), (kinds[1].Children![0].Text, OptionMenu.PickOf(picks, kinds[1].Children![0].Id)!.Kind));
        Assert.Equal(TargetBrowse.Folder, OptionRows.Find(OptionRows.Of(pin, Sources, place), OptionPickers.PinFolderBrowse)!.Browses);
        Assert.Equal((true, S.SettingsFolderContentsTargetPlaceholder), (Field(1).Finds, Field(1).Placeholder));
        Assert.Equal([("This PC", false), ("Documents", true), ("Downloads", false)], (await Offered(1)).Select(row => (row.Title, row.Chosen)));

        // File: "Browse…" for a file, then the files used lately.
        Assert.Equal(TargetBrowse.File, OptionRows.Find(OptionRows.Of(pin, Sources, place), OptionPickers.PinFileBrowse)!.Browses);
        Assert.Equal(S.SettingsPinTargetBrowse, kinds[2].Children![0].Text);
        Assert.Equal((true, S.SettingsPinFilePlaceholder), (Field(2).Finds, Field(2).Placeholder));
        Assert.Equal(["Report.docx"], (await Offered(2)).Select(row => row.Title));

        // Website: a line of text, no magnifier, the address typed; the sites the person pins offered.
        Assert.Equal((false, "", S.SettingsPinWebsitePlaceholder), (Field(3).Finds, Field(3).Text, Field(3).Placeholder));
        Assert.Equal(["example.org"], (await Offered(3)).Select(row => row.Title));

        // Settings page: Windows' Settings' pages, by their names.
        Assert.Equal((true, S.SettingsPinSettingsPlaceholder), (Field(4).Finds, Field(4).Placeholder));
        var pages = await Offered(4);
        Assert.True(pages.Count > 10, $"only {pages.Count} pages");
        Assert.All(pages, page => Assert.StartsWith("ms-settings:", (string)page.Value!, StringComparison.Ordinal));
        Assert.Equal(pages.Count, pages.Select(page => page.Value).Distinct().Count());
        var display = Assert.Single(await Field(4).Finder!.FindAsync(SettingsActions.Find("display")!.Name, CancellationToken.None), page => (string)page.Value! == "ms-settings:display");
        Assert.True(OptionMenu.Row(pin, OptionMenu.PickOf(picks, kinds[4].Children!.Single(row => row.Control is FlyoutField).Id)!, Sources, place)!.Pick!(display));
        Assert.Equal("ms-settings:display", pin.Pin!.Target);
        Assert.Equal(SettingsActions.Find("display")!.Name, Target(pin, place).Summary);
        Assert.True(Kind(pin, place, OptionPickers.PinSetting).Ticked);
    }

    /// <summary>
    /// §425: a website typed and entered is what the pin opens: a bare domain an https address, the row it is offered
    /// as its host with its site's picture; the pin then says the host, wears the site's picture, and Website is ticked,
    /// its field opening with the address. Words that are no address say so and keep nothing.
    /// </summary>
    [Fact]
    public async Task AWebsiteTypedAndEnteredBecomesTheTarget()
    {
        var (pin, place) = FolderPin();
        var site = Kind(pin, place, OptionPickers.PinSite);
        Assert.Equal((OptionRowKind.Search, (bool?)false, ""), (site.Kind, site.Ticked, site.Text));
        var typed = site.Finder!.Typed(" example.com/news ");
        var found = Assert.IsType<OptionFound>(typed?.Found);
        Assert.Equal(("example.com", "https://example.com/news", PinTargets.PictureOf("https://example.com/news")), (found.Title, found.Value, found.Picture));
        // As it is typed, the address is offered first, with its picture.
        Assert.Equal("example.com", (await site.Finder.FindAsync("example.com/news", CancellationToken.None))[0].Title);
        Assert.True(site.Pick!(found));
        Assert.Equal("https://example.com/news", pin.Pin!.Target);

        var target = Target(pin, place);
        Assert.Equal(("example.com", PinTargets.PictureOf("https://example.com/news")), (target.Summary, target.ValuePicture));
        var now = Kind(pin, place, OptionPickers.PinSite);
        Assert.Equal((true, "https://example.com/news", "example.com"), (now.Ticked, now.Text, now.Summary));
        Assert.Equal(false, Kind(pin, place, OptionPickers.PinFolder).Ticked);
        Assert.Equal("example.com", OptionRows.Of(pin, Sources, place).Single(row => row.Key == OptionFields.PinName).Placeholder);

        foreach (var words in new[] { "not a site", "ftp://example.com", "example", "https://" })
        {
            Assert.Equal((null, S.SettingsPinWebsiteInvalid), (now.Finder!.Typed(words)?.Found, now.Finder.Typed(words)?.Said));
        }
        Assert.Null(now.Finder!.Typed("  "));
        Assert.Equal("http://localhost:8080/", PinTargets.Site("http://localhost:8080/"));
        Assert.Equal("https://www.example.com", PinTargets.Site("www.example.com"));
    }

    /// <summary>
    /// §425: what kind a target is, the one its level ticks: an app by its ID in the Apps folder, a folder (Windows'
    /// own places too), a file, a web address, a page of Settings; none for nothing, and for a link of another kind.
    /// </summary>
    [Fact]
    public void ATargetIsOfOneKind()
    {
        static PinKind Of(string target) => PinTargets.KindOf(target, path => path is @"C:\Work" or @"\\server\share");
        Assert.Equal(PinKind.None, Of(""));
        Assert.Equal(PinKind.App, Of(SpotifyApp));
        Assert.Equal(PinKind.Folder, Of(@"C:\Work"));
        Assert.Equal(PinKind.Folder, Of(@"D:\Tools\"));
        Assert.Equal(PinKind.Folder, Of(ThisPc));
        Assert.Equal(PinKind.Folder, Of(@"\\server\share"));
        Assert.Equal(PinKind.File, Of(Report));
        Assert.Equal(PinKind.File, Of(ExplorerLink));
        Assert.Equal(PinKind.File, Of(@"C:\Windows\notepad.exe"));
        Assert.Equal(PinKind.Website, Of("https://example.com"));
        Assert.Equal(PinKind.Website, Of("http://example.com/a"));
        Assert.Equal(PinKind.SettingsPage, Of("ms-settings:display"));
        Assert.Equal(PinKind.None, Of("mailto:ann@example.com"));
        // A path with no disk to ask: a folder where it has no extension.
        Assert.Equal(PinKind.Folder, PinTargets.KindOf(@"E:\Photos"));
        Assert.Equal(PinKind.File, PinTargets.KindOf(@"E:\Photos\cat.jpg"));
    }

    /// <summary>
    /// §380: Target says the name Windows gives it — a shortcut's without ".lnk", an app's as Start names it, never
    /// its ID — at the row's end and in its name's field. Where Windows has not said (no dock asked), the name is
    /// still a person's: the shortcut's file without its extension, an app by its package's name, a web address by
    /// its host, a page of Settings by its name.
    /// </summary>
    [Fact]
    public void TargetSaysTheNameWindowsGivesIt()
    {
        foreach (var (target, name) in new[] { (ExplorerLink, "File Explorer"), (SpotifyApp, "Spotify"), (ThisPc, "This PC") })
        {
            var (pin, place) = FolderPin(target);
            Assert.Equal(name, Target(pin, place).Summary);
            Assert.Equal(name, OptionRows.Of(pin, Sources, place).Single(row => row.Key == OptionFields.PinName).Placeholder);
        }
        Assert.Equal("File Explorer", PinTargets.NameOf(ExplorerLink));
        Assert.Equal("SpotifyMusic", PinTargets.NameOf(SpotifyApp));
        Assert.Equal("example.com", PinTargets.NameOf("https://example.com/news"));
        Assert.Equal("Tools", PinTargets.NameOf(@"D:\Tools\"));
        Assert.Equal(SettingsActions.Find("bluetooth")!.Name, PinTargets.NameOf("ms-settings:bluetooth"));
        // Nothing the shell knows: the name before it is asked.
        var (other, at) = FolderPin(@"D:\Projects\notes.txt");
        Assert.Equal("notes.txt", Target(other, at).Summary);
        Assert.True(Kind(other, at, OptionPickers.PinFile).Ticked);
    }

    /// <summary>
    /// An app picked keeps its target; the App kind ticked then, saying it. A folder's contents are offered folders
    /// alone, and no place of Windows' that has no folder on disk to list.
    /// </summary>
    [Fact]
    public async Task PickingAnAppOrAFolderSetsIt()
    {
        var (pin, place) = FolderPin(@"C:\Users\Ann\Documents\");
        var app = Kind(pin, place, OptionPickers.PinApp);
        var found = await app.Finder!.FindAsync("", CancellationToken.None);
        Assert.True(app.Pick!(found[1]));
        Assert.Equal(SpotifyApp, pin.Pin!.Target);
        Assert.False(Kind(pin, place, OptionPickers.PinApp).Pick!(found[1]));
        Assert.Equal((true, "Spotify"), (Kind(pin, place, OptionPickers.PinApp).Ticked, Kind(pin, place, OptionPickers.PinApp).Summary));
        Assert.Equal(false, Kind(pin, place, OptionPickers.PinFolder).Ticked);

        var folder = Kind(pin, place, OptionPickers.PinFolder);
        Assert.True(folder.Pick!((await folder.Finder!.FindAsync("", CancellationToken.None))[0]));
        Assert.Equal(ThisPc, pin.Pin.Target);
        Assert.True(Kind(pin, place, OptionPickers.PinFolder).Ticked);

        var (contents, there) = FolderPin(item: ListKind.FolderContents.New());
        var folders = await Target(contents, there).Finder!.FindAsync("", CancellationToken.None);
        Assert.Equal(["Documents", "Downloads"], folders.Select(row => row.Title));
    }

    /// <summary>
    /// What the person's pins and folders open that is a folder, each once, by the name Windows gives it and with its
    /// picture: what a picker offers first; what they open that is a website, the website's. An app, a file or a web
    /// address is not a folder.
    /// </summary>
    [Fact]
    public void ThePinnedFoldersAndSitesAreOfferedFirst()
    {
        BarItem Pinned(string target)
        {
            var each = BarKind.Pin.New();
            each.Pin!.Target = target;
            return each;
        }
        // A folder's contents, a line of a Karpo folder, show their folder as a pin does.
        var photos = BarKind.FolderList.New();
        photos.Folder = new FolderOptions { Lists = [[new FolderCell { Island = new CellItem { Kind = "folderContents", Pin = new PinOptions { Target = @"E:\Photos" } } }]] };
        var config = new Config
        {
            Layouts = [new TaskbarLayout { Islands = [Pinned(@"D:\Tools"), Pinned("https://example.com"), Pinned(@"d:\tools"), Pinned(@"C:\Work\notes.txt"), Pinned(SpotifyApp), photos, Pinned("HTTPS://example.com")] }],
        };
        var found = OptionFields.PinsInUse(config, path => !Path.HasExtension(path) && path.Contains(':') && !path.StartsWith("shell:", StringComparison.Ordinal) && !path.Contains("//"), target => target.ToUpperInvariant());
        Assert.Equal([(@"D:\TOOLS", @"D:\Tools"), (@"E:\PHOTOS", @"E:\Photos")], found.Select(row => (row.Title, row.Detail)));
        Assert.All(found, row => Assert.Equal(PinTargets.PictureOf((string)row.Value!), row.Picture));
        var sites = OptionFields.SitesInUse(config);
        Assert.Equal([("example.com", "https://example.com")], sites.Select(row => (row.Title, (string)row.Value!)));
        Assert.Equal(PinTargets.PictureOf("https://example.com"), sites[0].Picture);
    }

    /// <summary>
    /// A folder's or a file's field, typed: words find a folder by its name; a path is a row of its own first, which
    /// Enter keeps; a path lists the folders under it as far as it is typed, none hidden; a share on the network is
    /// never listed.
    /// </summary>
    [Fact]
    public async Task TypingFindsAFolderOrKeepsThePathTyped()
    {
        var (pin, place) = FolderPin();
        var finder = Kind(pin, place, OptionPickers.PinFolder).Finder!;
        Assert.Equal(["Documents"], (await finder.FindAsync("docu", CancellationToken.None)).Select(row => row.Title));
        Assert.Equal(S.DockMenuNoMatch, finder.NoneFound("zzz"));
        Assert.Equal(@"D:\Data", (string)finder.Typed(@"D:\Data")!.Value.Found!.Value!);
        var file = Kind(pin, place, OptionPickers.PinFile);
        Assert.Equal(["Report.docx"], (await file.Finder!.FindAsync("rep", CancellationToken.None)).Select(row => row.Title));
        Assert.True(file.Pick!(file.Finder.Typed(@"C:\Windows\notepad.exe")!.Value.Found!));
        Assert.Equal(@"C:\Windows\notepad.exe", pin.Pin!.Target);

        var root = Directory.CreateTempSubdirectory("karpo-pin-target-").FullName;
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "Alpha"));
            Directory.CreateDirectory(Path.Combine(root, "Albums"));
            Directory.CreateDirectory(Path.Combine(root, "Beta"));
            new DirectoryInfo(Directory.CreateDirectory(Path.Combine(root, "Hidden")).FullName).Attributes |= FileAttributes.Hidden;
            File.WriteAllText(Path.Combine(root, "Alps.txt"), "");
            var under = await finder.FindAsync(root + @"\", CancellationToken.None);
            Assert.Equal(["Albums", "Alpha", "Beta"], under.Skip(1).Select(row => row.Title).Order());
            var al = await finder.FindAsync(Path.Combine(root, "Al"), CancellationToken.None);
            Assert.Equal(("Al", Path.Combine(root, "Al")), (al[0].Title, al[0].Value));
            Assert.Equal(["Albums", "Alpha"], al.Skip(1).Select(row => row.Title).Order());
            Assert.True(Kind(pin, place, OptionPickers.PinFolder).Pick!(al.Single(row => row.Title == "Alpha")));
            Assert.Equal(Path.Combine(root, "Alpha"), pin.Pin!.Target);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
        Assert.Empty(OptionFields.TargetFinder.Under(@"\\server\share\"));
        Assert.Empty(OptionFields.TargetFinder.Under("documents"));
    }

    /// <summary>
    /// §425: a pin that opens nothing yet has its menu open on Target's kinds, where what it needs is chosen
    /// (WidgetSetup.RowOf): the one field each kind of widget needs is a row of its menu.
    /// </summary>
    [Fact]
    public void WhatAPinNeedsIsTarget()
    {
        Assert.Equal(SetupField.Target, WidgetSetup.Needs(BarKind.Pin.New()));
        Assert.Equal(OptionPickers.PinTarget, WidgetSetup.RowOf(SetupField.Target));
        Assert.Equal(OptionPickers.PinTarget, WidgetSetup.RowOf(WidgetSetup.Needs(ListKind.FolderContents.New())!.Value));
    }

    // --- On a real menu -------------------------------------------------------------------------

    private const float Scale = 1.5f;

    private static readonly RECT Work = new() { right = (int)(1920 * Scale), bottom = (int)(1040 * Scale) };

    private static readonly POINT Anchor = new() { x = Work.right / 3, y = Work.bottom };

    private static FlyoutEvent Key(Flyout menu, int key) => menu.Message(WM.WM_KEYDOWN, (WPARAM)(nuint)key, 0);

    /// <summary>The menu's own posted messages and timers handed to it for <paramref name="ms"/>: what it said.</summary>
    private static unsafe List<FlyoutEvent> Pump(Flyout menu, int ms = 300)
    {
        List<FlyoutEvent> said = [];
        var until = Environment.TickCount64 + ms;
        MSG msg;
        while (Environment.TickCount64 < until)
        {
            for (var left = 64; left > 0 && PeekMessageW(&msg, HWND.NULL, 0, 0, PM.PM_REMOVE); left--)
            {
                if (msg.hwnd == menu.Hwnd && (msg.message is >= WM.WM_USER and <= WM.WM_USER + 0xFF || msg.message == WM.WM_TIMER))
                {
                    said.Add(menu.Message(msg.message, msg.wParam, msg.lParam));
                    continue;
                }
                _ = TranslateMessage(&msg);
                _ = DispatchMessageW(&msg);
            }
            Thread.Sleep(10);
        }
        return [.. said.Where(happened => happened.Kind != FlyoutEventKind.Nothing)];
    }

    /// <summary>
    /// §425, §392: Target opened, then App, "spot" typed, Spotify picked by Down and Enter, kept and the menu shown
    /// again for it: back on the kinds, App lit, ticked and saying Spotify. Then Website: "example.com" typed and
    /// Enter: kept as https://example.com, back on the kinds, Website ticked and saying example.com.
    /// </summary>
    [UiFact]
    public void APickGoesBackToTheKindsSayingIt()
    {
        WindowShots.OnHiddenDesktop("pin-target-back", (gfx, instance) =>
        {
            var (pin, place) = FolderPin(@"C:\Work");
            List<OptionPick> picks = [];
            List<FlyoutItem> Rows()
            {
                picks.Clear();
                return OptionMenu.Rows(OptionRows.Of(pin, Sources, place), picks);
            }
            var items = Rows();
            using var menu = new Flyout(gfx, instance, 0) { Configures = true };
            menu.Show(gfx, AddListPictures.LookFor(true), Scale, items, FlyoutLayout.List, Anchor, Work, Edge.Bottom, null);
            WindowShots.Settle();
            FlyoutItem KindRow(string title) => menu.RowsShown.Single(item => item.Text == title);
            var target = items[0];
            _ = menu.Click(target.Id);
            Assert.Equal(1, menu.InlineDepth);
            var app = KindRow(S.SettingsPinKindApp);
            var field = app.Children!.Single(item => item.Control is FlyoutField);
            _ = menu.Click(app.Id);
            Assert.Equal((2, field.Id), (menu.InlineDepth, menu.FieldKeys));
            foreach (var c in "spot")
            {
                _ = menu.Message(WM.WM_CHAR, (WPARAM)(nuint)c, 0);
            }
            _ = Pump(menu);
            _ = Key(menu, VK.VK_DOWN);
            _ = Key(menu, VK.VK_RETURN);
            Assert.Equal(new FlyoutEvent(FlyoutEventKind.Adjust, field.Id), Assert.Single(Pump(menu)));
            Assert.True(FieldPicks.Keep(pin, OptionMenu.PickOf(picks, field.Id)!, menu.TakeFieldCommit(field.Id)!, Sources, place));
            Assert.Equal(SpotifyApp, pin.Pin!.Target);
            menu.Refresh(Rows());
            WindowShots.Settle();
            var now = KindRow(S.SettingsPinKindApp);
            Assert.Equal((1, now.Id), (menu.InlineDepth, menu.HotId!.Value));
            Assert.Equal((true, "Spotify"), (now.Checked, now.Value?.Text));

            var site = KindRow(S.SettingsPinKindWebsite);
            var address = site.Children!.Single(item => item.Control is FlyoutField);
            _ = menu.Click(site.Id);
            Assert.Equal((2, address.Id), (menu.InlineDepth, menu.FieldKeys));
            foreach (var c in "example.com")
            {
                _ = menu.Message(WM.WM_CHAR, (WPARAM)(nuint)c, 0);
            }
            _ = Pump(menu);
            _ = Key(menu, VK.VK_RETURN);
            _ = Pump(menu);
            Assert.True(FieldPicks.Keep(pin, OptionMenu.PickOf(picks, address.Id)!, menu.TakeFieldCommit(address.Id)!, Sources, place));
            Assert.Equal("https://example.com", pin.Pin.Target);
            menu.Refresh(Rows());
            WindowShots.Settle();
            Assert.Equal(1, menu.InlineDepth);
            Assert.Equal((true, "example.com"), (KindRow(S.SettingsPinKindWebsite).Checked, KindRow(S.SettingsPinKindWebsite).Value?.Text));
            Assert.Equal(false, KindRow(S.SettingsPinKindApp).Checked);
            menu.Hide();
        });
    }
}
