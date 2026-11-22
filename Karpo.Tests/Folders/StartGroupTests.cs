// §641: the Start group (docs\widgets.md §641) — a folder looking and behaving like Windows 11's Start, made of four Beta
// widgets: the pinned apps, All apps, Recommended, the account and power (§653: no search box). Every line here reads the samples
// (StartSamples through StartSources.Stand): no test reads the PC's apps, files or account. What a click or a menu's row
// opens goes to the launcher's stand-in (Launch.Stand), a power command to the power buttons' (PowerButtonTile.Runner), a
// change of the config to the editor's (StartLine.Editor): no test opens an app, signs out, locks, sleeps or shuts down.
//
// Pictured (KARPO_UI_TESTS=1, KARPO_PNG_DIR): start-group-<dark|light>.png (the folder as the add page's pick makes it),
// start-views-<dark|light>.png (All apps by category, as a grid and a list).

using System.Numerics;
using Karpo.Dock;
using Karpo.Dock.StartWidgets;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using static Karpo.Tests.Folders.ListFormPictures;
using static Karpo.Tests.Islands.Offscreen;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Folders;

public sealed unsafe class StartGroupTests
{
    private static readonly PresetMachine Machine = new(HasBattery: false, FixedDrives: []);

    private static Preset Start => Presets.Find(Presets.StartId)!;

    // --- The group and the add page ----------------------------------------------------------------------

    /// <summary>
    /// Start as it stands: the pinned apps (three lines) and Recommended (three lines) over its foot, All apps beside them as
    /// tall, wide and medium; Windows' glyph its face; down a side bar one list, All apps under the rest.
    /// </summary>
    [Fact]
    public void TheGroupIsStartsPinsRecommendedAndFootBesideAllApps()
    {
        var lists = Presets.Lists(Start, Machine);
        Assert.Equal(
            [[ListKind.StartPins, ListKind.StartRecommended, ListKind.StartProfile], [ListKind.StartApps]],
            lists.Select(list => list.Select(cell => cell.Island.List!.Value).ToList()).ToList());
        Assert.Equal(LineSize.Full, lists[0][0].Island.Size);
        Assert.Equal(LineSize.Full, lists[0][1].Island.Size);
        // All apps as tall as the three beside it, cell for cell.
        var left = lists[0].Sum(cell => LineSizes.CellsOf(cell.Island));
        Assert.Equal(left, lists[1][0].Island.Height!.Value.Cells);
        Assert.Equal(Presets.StartAppsCells, left);
        Assert.Equal([FolderListWidth.Wide, FolderListWidth.Medium], Start.Widths);
        var folder = Presets.Folder(Start, Machine);
        Assert.Equal(OptionGlyphs.Windows, folder.Folder!.Faces[0].Glyph);
        Assert.True(Start.Beta);
        // §642: Windows search's group is the other one Beta.
        Assert.All(Presets.All.Where(preset => preset != Start && preset.Id != Presets.WinSearchId), preset => Assert.False(preset.Beta, preset.Id));

        var side = Presets.Lists(Start, Machine, Edge.Left);
        Assert.Single(side);
        Assert.Equal(ListKind.StartApps, side[0][^1].Island.List);
        Assert.Equal(Presets.StartAppsSideCells, side[0][^1].Island.Height!.Value.Cells);
    }

    /// <summary>
    /// §653 (the person, 2026-11-09: "start search folder widget does not seem to do anything. remove it."): Start's search
    /// box is gone whole — not in the group, not a widget, not a line, not on the add page, and the channel it fed with it.
    /// A file that still has one drops it as any kind this build does not know (RemovedKindsTests).
    /// </summary>
    [Fact]
    public void StartsSearchBoxIsGoneWhole()
    {
        Assert.DoesNotContain(Start.Items, item => item.Kind == "startSearch");
        Assert.All(Presets.All, preset => Assert.DoesNotContain(preset.Items, item => item.Kind == "startSearch"));
        Assert.Null(WidgetInfos.Find("startSearch"));
        Assert.Null(ListWidgets.Find("startSearch"));
        Assert.DoesNotContain("StartSearch", Enum.GetNames<ListKind>());
        Assert.DoesNotContain("StartSearch", Enum.GetNames<WidgetSource>());
        Assert.DoesNotContain(ListWidgets.All, line => line.Info.Id == "startSearch");
        Assert.DoesNotContain(WidgetInfos.All, info => info.Id == "startSearch");
        Assert.Null(Channels.Find("search"));
        var taskbar = AddTree.Browse.Rows.OfType<AddShelf>().Single(shelf => shelf.Id == "taskbar");
        Assert.DoesNotContain(taskbar.Rows.OfType<AddLeaf>(), leaf => leaf.Id == "startSearch");
        Assert.Null(S.Lookup("core.widget.startSearch.title"));
    }

    /// <summary>
    /// "Beta" on the add page's tile of each of the four, on a folder's page, and of the group on the bar's — a badge after
    /// its name, and nothing on any other; kept by the search's results; Start and taskbar's run, after Windows' three.
    /// </summary>
    [Fact]
    public void TheAddPageMarksTheStartWidgetsAndTheirGroupBeta()
    {
        string[] ours = ["startPins", "startApps", "startRecommended", "startProfile"];
        var (items, entries, _) = AddList.Rows(WidgetType.List, turns: false, 1000);
        foreach (var pick in AddList.Picks(items))
        {
            var entry = entries[(int)(pick.Id - 1000)];
            // §642: Windows search wears the same Beta.
            Assert.Equal((ours.Contains(entry.Id) || entry.Id == WidgetInfos.WinSearch.Id) && entry.Premade is null ? S.DockAddBeta : null, pick.Badge);
        }
        Assert.All(ours, id => Assert.Contains(AddList.Picks(items), pick => pick.Badge is not null && entries[(int)(pick.Id - 1000)].Id == id));
        Assert.Equal(S.DockAddBeta, Assert.Single(AddList.Results(items, "all apps"), result => result.Text == WidgetInfos.StartApps.Title).Badge);

        var (bar, barEntries, presets) = AddList.Rows(WidgetType.Bar, turns: false, 1000);
        var start = (uint)(1000 + barEntries.Count + presets.IndexOf(Start));
        var winSearch = (uint)(1000 + barEntries.Count + presets.FindIndex(preset => preset.Id == Presets.WinSearchId));
        Assert.All(AddList.Picks(bar), pick => Assert.Equal(pick.Id == start || pick.Id == winSearch ? S.DockAddBeta : null, pick.Badge));
        Assert.Equal(S.DockAddBeta, AddList.Picks(bar).First(pick => pick.Id == start).Badge);

        // A run of Start and taskbar's own, its group first.
        var taskbar = AddTree.Browse.Rows.OfType<AddShelf>().Single(shelf => shelf.Id == "taskbar");
        var leaves = taskbar.Rows.OfType<AddLeaf>().Select(leaf => leaf.Id).ToList();
        Assert.Equal([Presets.StartId, .. ours], leaves.Skip(3).Take(5));
    }

    // --- The lines, on the samples -----------------------------------------------------------------------

    /// <summary>The Start group's folder, opened, every line reading <paramref name="samples"/>.</summary>
    private static FolderIsland Opened(Gfx gfx, StartSamples samples, BarItem? holder = null)
    {
        holder ??= Presets.Folder(Start, Machine);
        StartSources.Stand = samples;
        try
        {
            // On its bar in the config, as the dock has it: All apps finds the pinned apps beside it there.
            var config = new Config { Layouts = [new TaskbarLayout { Id = Owner["layout:".Length..], Islands = [holder] }] };
            var folder = new FolderIsland($"{Owner}/{holder.Id}", holder, gfx, config);
            folder.Opened(gfx);
            return folder;
        }
        finally
        {
            StartSources.Stand = null;
        }
    }

    private static T LineOf<T>(FolderIsland folder)
        where T : StartLine => folder.Tiles.Select(tile => tile.Island).OfType<T>().First();

    private static StartAppsLine Lines(FolderIsland folder, ListKind kind) =>
        folder.Tiles.Where(tile => tile.Cell.Island.List == kind).Select(tile => (StartAppsLine)tile.Island).Single();

    private static TileStyle StyleOf(bool dark = true) => FolderFlyout.StyleFor(1.5f, LookFor(dark), FlyoutTheme.For(LookFor(dark)));

    private static D2D_RECT_F Box(CellIsland line, in TileStyle style, FolderListWidth width = FolderListWidth.Wide)
    {
        var wide = FolderFlyout.ListWidth(style.Scale, width);
        var tall = line is IListContent content ? content.ListHeight(wide, style) : style.Px(60);
        if (line is IListScroll scroll)
        {
            tall = scroll.ScrollHeight(wide, tall, style);
        }
        return PopupPaint.Rect(0, 0, wide, tall);
    }

    private static IReadOnlyList<LinePiece> Pieces(CellIsland line, in TileStyle style) => ((ILinePieces)line).LinePieces(Box(line, style), style);

    private static List<string> Tiles(CellIsland line, in TileStyle style, string kind = "tile ") =>
        [.. Pieces(line, style).Where(piece => piece.Name.StartsWith(kind, StringComparison.Ordinal)).Select(piece => piece.Text)];

    private static Vector2 Middle(D2D_RECT_F box) => new((box.left + box.right) / 2, (box.top + box.bottom) / 2);

    /// <summary>A click on the piece called <paramref name="name"/>, as the folder gives it.</summary>
    private static IslandAction Click(CellIsland line, in TileStyle style, string name)
    {
        var box = Box(line, style);
        var at = Middle(Pieces(line, style).First(piece => piece.Name == name).Box);
        line.CardHover(at, box, style, WidgetSurface.List);
        return line.Click(line.CardTarget(at, box, style, WidgetSurface.List));
    }

    /// <summary>The pinned apps are this PC's usual ones (Start's own order, those it has) in a grid as wide as the line.</summary>
    [Fact]
    public void ThePinsAreTheUsualOnesInAGrid()
    {
        OnStaThread(gfx =>
        {
            using var folder = Opened(gfx, new StartSamples());
            var style = StyleOf();
            var pins = Lines(folder, ListKind.StartPins);
            var usual = StartPins.Defaults(StartSamples.SampleApps).Select(target => StartSamples.SampleApps.First(app => app.Target == target).Name).ToList();
            Assert.Equal(usual, Tiles(pins, style));
            Assert.Equal(["Microsoft Edge", "Word", "Excel"], usual.Take(3));
            Assert.Equal(StartPins.MostUsual, usual.Count);
            // Six across in a wide list at 150 %, as Start's grid.
            var tiles = Pieces(pins, style).Where(piece => piece.Name.StartsWith("tile ", StringComparison.Ordinal)).ToList();
            Assert.Equal(6, tiles.Count(tile => tile.Box.top == tiles[0].Box.top));
        });
    }

    /// <summary>
    /// A tile's click, a recommended file's click: what they open is left for the dock, which opens it once the folder has
    /// gone through the one launcher, and counts it.
    /// </summary>
    [Fact]
    public void AClickOpensOnceTheFolderHasGoneThroughTheLauncher()
    {
        OnStaThread(gfx =>
        {
            var samples = new StartSamples();
            using var folder = Opened(gfx, samples);
            var style = StyleOf();
            using var launched = Launch.Stand();

            var pins = Lines(folder, ListKind.StartPins);
            Assert.Equal(IslandView.App, Click(pins, style, "tile Excel").View);
            Assert.Empty(launched.Said);
            pins.Act();
            var excel = StartSamples.SampleApps.First(app => app.Name == "Excel").Target;
            Assert.Contains(new Launched(LaunchKind.Open, excel, "open"), launched.Said.OfType<Launched>().Select(said => said with { Verb = said.Verb ?? "open" }));
            Assert.Equal([excel], samples.OpenedTargets);
            pins.Act();
            Assert.Single(samples.OpenedTargets);

            var recommended = LineOf<StartRecommendedLine>(folder);
            Assert.Equal(IslandView.App, Click(recommended, style, "item Quarterly report.docx").View);
            recommended.Act();
            Assert.Equal(@"C:\Users\Public\Documents\Quarterly report.docx", samples.OpenedTargets[^1]);
            // Clipchamp, first seen a day ago, is recently added; the newest first: the file twelve minutes old before it.
            Assert.Equal(["Quarterly report.docx", "Budget 2027.xlsx", "Clipchamp", "Holiday photos.zip"], recommended.Items().Take(4).Select(item => item.Name));
            Assert.Equal(S.DockStartRecentlyAdded, recommended.WhenOf(recommended.Items()[2]));
        });
    }

    /// <summary>
    /// The right click of each, as Start's: a packaged pin's Open, Unpin, Move to front, Uninstall; a desktop app's in All apps
    /// Run as administrator and Open file location as well, and Pin to Start; a row of either hands the screen away or, a
    /// change of the pins, stays and asks the dock — Pin to Start pins to the pinned apps beside.
    /// </summary>
    [Fact]
    public void AnAppsMenuIsStartsAndItsPinsAreChangedThroughTheDock()
    {
        OnStaThread(gfx =>
        {
            using var folder = Opened(gfx, new StartSamples());
            var style = StyleOf();
            var pins = Lines(folder, ListKind.StartPins);
            var box = Box(pins, style);
            pins.CardHover(Middle(Pieces(pins, style).First(piece => piece.Name == "tile Excel").Box), box, style, WidgetSurface.List);
            Assert.Equal([S.DockWidgetMenuOpen, "", S.DockStartUnpin, S.DockStartToFront, "", S.DockStartUninstall],
                pins.MenuRows().Select(row => row.Text));

            var apps = Lines(folder, ListKind.StartApps);
            apps.Settings.Start = new StartOptions { View = StartView.List };
            var appsBox = Box(apps, style);
            apps.CardHover(Middle(Pieces(apps, style).First(piece => piece.Name == "row Spotify").Box), appsBox, style, WidgetSurface.List);
            var rows = apps.MenuRows();
            Assert.Equal([S.DockWidgetMenuOpen, "", S.DockStartPin, "", S.DockStartUninstall], rows.Select(row => row.Text));
            Assert.True(rows[2].Edit);

            // Through the dock's menu: the rows its act makes, the change one that stays.
            var (items, acts) = TheDock.FolderMenuRows(apps.Settings, apps);
            Assert.Equal([WidgetAct.Start, WidgetAct.StartEdit, WidgetAct.Start], acts.Select(act => act.Act));
            Assert.True(items.Count(item => item.Separator) == 2);
            Assert.False(TheDock.HandsAway(WidgetAct.StartEdit));
            List<(string Key, StartEdit Edit)> asked = [];
            StartLine.Editor = (key, edit) => asked.Add((key, edit));
            try
            {
                apps.MenuPicked(rows[2].Value);
            }
            finally
            {
                StartLine.Editor = null;
            }
            var spotify = StartSamples.SampleApps.First(app => app.Name == "Spotify").Target;
            Assert.Equal((apps.Key, new StartEdit(StartEdits.Pin, spotify)), Assert.Single(asked));

            // A desktop app's menu runs it as administrator and shows its program in File Explorer.
            apps.CardHover(Middle(Pieces(apps, style).First(piece => piece.Name == "row 7-Zip File Manager").Box), appsBox, style, WidgetSurface.List);
            using var launched = Launch.Stand();
            var zip = apps.MenuRows();
            apps.MenuPicked(zip.First(row => row.Text == S.DockWidgetMenuRunAsAdministrator).Value);
            apps.MenuPicked(zip.First(row => row.Text == S.DockWidgetMenuOpenFileLocation).Value);
            var program = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"7-Zip\7zFM.exe");
            Assert.Equal([LaunchKind.Elevated, LaunchKind.Select], launched.Said.OfType<Launched>().Select(said => said.Kind));
            Assert.All(launched.Said.OfType<Launched>(), said => Assert.Equal(program, said.Target));
        });
    }

    /// <summary>
    /// A pinned tile carried onto another place moves there, a click without a drag opens it; Ctrl+Shift and an arrow move the
    /// one the keys are on. Every tile is a stop of the folder's keys, in reading order.
    /// </summary>
    [Fact]
    public void APinIsDraggedOrKeyedIntoPlaceAndEveryTileIsAStop()
    {
        OnStaThread(gfx =>
        {
            using var folder = Opened(gfx, new StartSamples());
            var style = StyleOf();
            var pins = Lines(folder, ListKind.StartPins);
            var box = Box(pins, style);
            var tiles = Pieces(pins, style).Where(piece => piece.Name.StartsWith("tile ", StringComparison.Ordinal)).ToList();

            var stops = LineFocus.Parts(pins, box, style, WidgetSurface.List);
            Assert.Equal(tiles.Count, stops.Count);
            Assert.Equal(tiles.Select(tile => tile.Box.top - tiles[0].Box.top), stops.Select(stop => stop.Box.top - stops[0].Box.top));
            Assert.Equal(tiles.Select(tile => tile.Box.left - tiles[0].Box.left), stops.Select(stop => stop.Box.left - stops[0].Box.left));

            List<StartEdit> asked = [];
            StartLine.Editor = (_, edit) => asked.Add(edit);
            using var launched = Launch.Stand();
            try
            {
                // The first tile carried onto the right half of the third: after it, the third place.
                var third = tiles[2].Box;
                pins.Carry(Middle(tiles[0].Box), new Vector2(third.right - 2, Middle(third).Y), box, style);
                var edge = StartSamples.SampleApps.First(app => app.Name == "Microsoft Edge").Target;
                Assert.Equal(new StartEdit(StartEdits.Move, edge, 2), Assert.Single(asked));
                Assert.Empty(launched.Said);

                // Pressed and let go where it was: it opens.
                pins.Carry(Middle(tiles[1].Box), Middle(tiles[1].Box), box, style);
                Assert.Single(asked);
                Assert.Single(launched.Said.OfType<Launched>());
            }
            finally
            {
                StartLine.Editor = null;
            }
        });
    }

    /// <summary>
    /// All apps: by category by default, "Recently added" first while an app is new, a category of fewer than three in Other;
    /// as a grid or a list A to Z under their letters; its view's button opens the views' menu, the one in force ticked.
    /// </summary>
    [Fact]
    public void AllAppsIsByCategoryAsAGridOrAListAndItsViewIsAMenu()
    {
        OnStaThread(gfx =>
        {
            using var folder = Opened(gfx, new StartSamples());
            var style = StyleOf();
            var apps = Lines(folder, ListKind.StartApps);
            List<string> Heads() => [.. Pieces(apps, style).Where(piece => piece.Name == "section").Select(piece => piece.Text)];

            Assert.Equal(StartView.Category, apps.View);
            Assert.Equal(S.DockStartRecentlyAdded, Heads()[0]);
            Assert.Contains(S.DockStartCategoryProductivity, Heads());
            Assert.Contains(S.DockStartCategoryGames, Heads());
            Assert.Equal(S.WidgetScreenTimeOther, Heads()[^1]);
            Assert.NotEmpty(Tiles(apps, style));

            apps.Settings.Start = new StartOptions { View = StartView.List };
            Assert.Equal([S.DockStartRecentlyAdded, "#", "C", "D", "E", "F", "G", "M", "N", "O", "P", "S", "T", "V", "W", "X"], Heads());
            Assert.Empty(Tiles(apps, style));
            Assert.Equal(StartSamples.SampleApps.Length + 1, Tiles(apps, style, "row ").Count);

            apps.Settings.Start = new StartOptions { View = StartView.Grid };
            Assert.Equal(StartSamples.SampleApps.Length + 1, Tiles(apps, style).Count);

            Assert.Equal(IslandView.Menu, Click(apps, style, "view").View);
            var views = apps.MenuRows();
            Assert.Equal([S.DockStartViewCategory, S.SettingsAppearanceStylesPatternGrid, S.SettingsPinViewList], views.Select(row => row.Text));
            Assert.Equal([false, true, false], views.Select(row => row.Ticked == true));
            Assert.All(views, row => Assert.True(row.Edit));
            Assert.Equal(new StartEdit(StartEdits.View, nameof(StartView.List)), StartEdit.Of(views[2].Value));
        });
    }

    /// <summary>
    /// The foot: the account's name, the folders beside power, the power button. Its menus are Start's: Change account
    /// settings, Lock, Sign out, Switch user; Sleep and Hibernate where Windows offers them, "Update and …" first while a
    /// restart is due. A command goes to the power buttons' runner — never to Windows in a test.
    /// </summary>
    [Fact]
    public void TheFootsMenusAreStartsAndItsCommandsAreInjected()
    {
        OnStaThread(gfx =>
        {
            var samples = new StartSamples();
            using var folder = Opened(gfx, samples);
            var style = StyleOf();
            var foot = LineOf<StartProfileLine>(folder);
            Assert.Contains(Pieces(foot, style), piece => piece.Name == "account" && piece.Text == "Alex Morgan");
            Assert.Equal(["place Settings", "place FileExplorer"], Pieces(foot, style).Where(piece => piece.Name.StartsWith("place ", StringComparison.Ordinal)).Select(piece => piece.Name).Reverse());
            Assert.Equal("AM", samples.Account.Initials);

            Assert.Equal(IslandView.Menu, Click(foot, style, "account").View);
            Assert.Equal([S.DockStartAccountSettings, "", S.DockQuickLinkLock, S.DockQuickLinkSignOut, S.DockStartSwitchUser], foot.MenuRows().Select(row => row.Text));

            Assert.Equal(IslandView.Menu, Click(foot, style, "power").View);
            Assert.Equal([S.DockQuickLinkSleep, S.DockQuickLinkHibernate, S.DockStartUpdateShutDown, S.DockQuickLinkShutDown, S.DockStartUpdateRestart, S.DockQuickLinkRestart],
                foot.MenuRows().Select(row => row.Text));
            Assert.Equal([S.DockQuickLinkShutDown, S.DockQuickLinkRestart], StartProfileLine.PowerRows(new QuickLinkMachine(false, false, false, false), updateDue: false).Select(row => row.Text));

            List<QuickLink> ran = [];
            PowerButtonTile.Runner = ran.Add;
            try
            {
                foreach (var row in foot.MenuRows())
                {
                    foot.MenuPicked(row.Value);
                }
                Click(foot, style, "account");
                foreach (var row in foot.MenuRows().Where(row => !row.Separator))
                {
                    using var launched = Launch.Stand();
                    foot.MenuPicked(row.Value);
                }
            }
            finally
            {
                PowerButtonTile.Runner = null;
            }
            Assert.Equal([QuickLink.Sleep, QuickLink.Hibernate, QuickLink.UpdateShutDown, QuickLink.ShutDown, QuickLink.UpdateRestart, QuickLink.Restart,
                QuickLink.Lock, QuickLink.SignOut, QuickLink.SwitchUser], ran);
            Assert.All(ran, link => Assert.True(QuickLinks.IsPower(link)));

            // A folder beside power opens it once the folder has gone.
            using var opened = Launch.Stand();
            Assert.Equal(IslandView.App, Click(foot, style, "place FileExplorer").View);
            foot.Act();
            Assert.Equal("shell:MyComputerFolder", Assert.Single(opened.Said.OfType<Launched>()).Target);
        });
    }

    // --- Pictures ---------------------------------------------------------------------------------------------

    /// <summary>The PC's own icons of the samples' apps, and their files' kinds', read at once at the size drawn: a picture shows the icons Start would.</summary>
    private static StartSamples Pictured() => new((target, px, _) => target.StartsWith(StartApp.AppsFolder, StringComparison.Ordinal) ? Items.Icon(target, TargetPictures.Bucket(px)) : Items.KindIcon(Path.GetExtension(target), TargetPictures.Bucket(px)));

    [UiFact]
    public void PicturesOfTheStartGroup()
    {
        OnStaThread(gfx =>
        {
            foreach (var dark in new[] { true, false })
            {
                var look = LookFor(dark);
                var name = dark ? "dark" : "light";
                using (var folder = Opened(gfx, Pictured()))
                {
                    var (_, plan) = Plan(folder, look, 1.5f);
                    Sheet(gfx, $"start-group-{name}.png", 1.5f, [("Start (beta): the ready-made group", look, folder, plan)]);
                }
                // §661: an item of each line under the pointer, lit edge to edge: a pinned tile at the line's edge, a
                // recommended file, the account.
                using (var folder = Opened(gfx, Pictured()))
                {
                    var style = StyleOf(dark);
                    void Hover(CellIsland line, string piece) =>
                        line.CardHover(Middle(Pieces(line, style).First(each => each.Name.StartsWith(piece, StringComparison.Ordinal)).Box), Box(line, style), style, WidgetSurface.List);
                    Hover(Lines(folder, ListKind.StartPins), "tile ");
                    Hover(LineOf<StartRecommendedLine>(folder), "item ");
                    Hover(LineOf<StartProfileLine>(folder), "account");
                    var (_, plan) = Plan(folder, look, 1.5f);
                    Sheet(gfx, $"start-group-hover-{name}.png", 1.5f, [("Start (beta): an item of each line under the pointer", look, folder, plan)]);
                }
                var panels = new List<(string, Look, FolderIsland, FolderPlan)>();
                foreach (var view in new[] { StartView.Category, StartView.Grid, StartView.List })
                {
                    var apps = ListFormPictures.Line(ListKind.StartApps, line => line.Start = new StartOptions { View = view });
                    apps.Height = LineHeight.Exactly(24);
                    StartSources.Stand = Pictured();
                    var folder = ListFormPictures.Folder(gfx, apps);
                    StartSources.Stand = null;
                    panels.Add(($"All apps: {view}", look, folder, Plan(folder, look, 1.5f).Plan));
                }
                Sheet(gfx, $"start-views-{name}.png", 1.5f, panels);
                foreach (var (_, _, folder, _) in panels)
                {
                    folder.Dispose();
                }
            }
        });
    }
}
