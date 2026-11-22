// A folder's lines, lists and buttons kept, and a folder kept with them (docs\widgets.md §357, the flicker
// design's phase 3): laid out again with nothing changed it makes nothing and draws no button again; a
// warning come beside a list makes that one button and keeps every line and every other button; edit mode
// makes its buttons alone; a folder switched back to shows what it kept, one of the two in sight in every
// frame; opened where it was left it is scrolled, stepped and on its day as it was. Tier 3 (what is made,
// counted), tier 1 and 2 for the switch, tier 4 (a folder hands its host nothing per showing), and the rule
// of where a folder opens (FolderMemories) and of a menu following the line it stands beside.

using Karpo.Dock;
using Karpo.Dock.Panels;
using Karpo.Tests.Edit;
using Karpo.Tests.Folders;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using Edge = Karpo.Core.Edge;

namespace Karpo.Tests.Present;

public sealed class RetainedFolderTests
{
    private const string Owner = "layout:default";

    private static readonly Look Still = AddListPictures.LookFor(dark: true) with { Animations = false };

    private static readonly RECT Display = new() { right = 1280, bottom = 720 };

    // --- The buttons, by what each is ---------------------------------------------------------------

    /// <summary>
    /// A column's button is known by what it is: a warning come beside the first list moves the second list's
    /// buttons along the plan's, and they are the same buttons — the keys the folder keeps them by.
    /// </summary>
    [Fact]
    public void AButtonIsKnownByWhatItIsWhereverItStandsAmongThem()
    {
        var few = Plan(Lists(4, 2));
        var many = Plan(Lists(30, 2));
        Assert.Empty(few.Lists[0].Hidden);
        Assert.NotEmpty(many.Lists[0].Hidden);
        Assert.Equal(few.Adds.Count + 1, many.Adds.Count);
        // Every button its own key.
        Assert.Equal(many.Adds.Count, many.Adds.Select(FolderFlyout.AddKeyOf).Distinct().Count());
        // The second list's, at other places among them, the same keys.
        var second = few.Adds.Where(add => add.List == 1).Select(FolderFlyout.AddKeyOf).ToList();
        Assert.Equal(second, many.Adds.Where(add => add.List == 1).Select(FolderFlyout.AddKeyOf).ToList());
        Assert.NotEqual(few.Adds.ToList().FindIndex(add => add.List == 1), many.Adds.ToList().FindIndex(add => add.List == 1));
        // A slot's by its side and how far out, whatever the lists are now.
        Assert.Equal(FolderFlyout.AddKeyOf(new FolderAdd(default, FolderPlace.Slot(2, 1), FolderAddKind.Add, -1)),
            FolderFlyout.AddKeyOf(new FolderAdd(default, FolderPlace.Slot(3, 1), FolderAddKind.Add, -1)));
    }

    // --- Kept through a layout --------------------------------------------------------------------

    /// <summary>A folder laid out again with nothing changed, arranged or at rest: nothing made, no surface drawn, no button drawn again.</summary>
    [UiFact]
    public void ALayoutWithNothingChangedMakesNothing()
    {
        WindowShots.OnHiddenDesktop("retained-folder-same", (gfx, instance) =>
        {
            foreach (var arranged in new[] { false, true })
            {
                var island = Folder("same", Lists(3, 2));
                using var folder = new FolderIsland($"{Owner}/{island.Id}", island, gfx, new Config());
                using var flyout = Shown(gfx, instance, folder, arranged);
                var made = flyout.Made;
                var drawn = flyout.ButtonsDrawn;
                var host = flyout.HostMade;
                flyout.LayOutAgain();
                flyout.LayOutAgain();
                Assert.Equal(made, flyout.Made);
                Assert.Equal(drawn, flyout.ButtonsDrawn);
                Assert.Equal(host, flyout.HostMade);
            }
        });
    }

    /// <summary>
    /// Lines come into the first list past its room, arranged: a warning comes beside its buttons. The lines
    /// come are made, the warning is made — and nothing else: every line there was and the second list's
    /// buttons are the very ones they were, though they stand at other places among the buttons now.
    /// </summary>
    [UiFact]
    public void AWarningComingMakesItsButtonAloneAndKeepsTheLines()
    {
        WindowShots.OnHiddenDesktop("retained-folder-warning", (gfx, instance) =>
        {
            var island = Folder("warn", Lists(4, 2));
            using var folder = new FolderIsland($"{Owner}/{island.Id}", island, gfx, new Config());
            using var flyout = Shown(gfx, instance, folder, arranged: true);
            Assert.Empty(flyout.Plan!.Lists[0].Hidden);
            var made = flyout.Made;
            var keys = folder.Tiles.Select(tile => tile.Island.Key).ToList();
            var before = keys.Select(key => flyout.ElementsOf(key, "Add/1")).ToList();
            var addsBefore = flyout.Plan!.Adds.Count;

            var more = Lists(30, 2);
            foreach (var (cell, index) in more.Lists[0].Skip(4).Select((cell, index) => (cell, index)))
            {
                cell.Island.Id = $"more{index}";
                island.Folder!.Lists[0].Add(cell);
            }
            folder.Configure(island, new Config());

            Assert.NotEmpty(flyout.Plan!.Lists[0].Hidden);
            Assert.Equal(addsBefore + 1, flyout.Plan!.Adds.Count);
            Assert.Equal(made.Lines + 26, flyout.Made.Lines);
            Assert.Equal(made.Buttons + 1, flyout.Made.Buttons);
            Assert.Equal(made.Views, flyout.Made.Views);
            for (var at = 0; at < keys.Count; at++)
            {
                var now = flyout.ElementsOf(keys[at], "Add/1");
                Assert.Same(before[at].Line, now.Line);
                Assert.Same(before[at].Button, now.Button);
            }
        });
    }

    // --- Kept through a close ------------------------------------------------------------------------

    /// <summary>
    /// A folder closed and opened again — switched away from and back — makes nothing: it shows what it kept,
    /// drawn again where it stands; and a switch between two kept folders has one of the two in sight in every
    /// frame (tier 1 with animations off, tier 2 with them on).
    /// </summary>
    [UiFact]
    public void AFolderSwitchedBackToShowsWhatItKept()
    {
        WindowShots.OnHiddenDesktop("film-retained-folder-switch", (gfx, instance) =>
        {
            foreach (var look in new[] { Still, Still with { Animations = true } })
            {
                var (one, two) = (Folder("first", Lists(3, 1)), Folder("second", Lists(2, 2)));
                using var first = new FolderIsland($"{Owner}/{one.Id}", one, gfx, new Config());
                using var second = new FolderIsland($"{Owner}/{two.Id}", two, gfx, new Config());
                using var left = new FolderFlyout(gfx, instance, 0);
                using var right = new FolderFlyout(gfx, instance, 0);
                // Each opened once and closed, as the dock keeps them (Dock.Folders.cs, folderViews).
                right.Show(gfx, look, 1f, new POINT { x = 900, y = 720 }, Display, Edge.Bottom, second);
                right.Hide();
                left.Show(gfx, look, 1f, new POINT { x = 380, y = 720 }, Display, Edge.Bottom, first);
                FrameFilm.Settle();
                var kept = right.Made with { Surfaces = 0 };
                var a = left.PanelOnScreen;
                var film = FrameFilm.Shoot(
                    look.Animations ? "folder-switch-kept-animated" : "folder-switch-kept",
                    gfx,
                    new RECT { left = 0, top = 180, right = 1280, bottom = 720 },
                    () => [left.Hwnd, right.Hwnd],
                    () =>
                    {
                        left.Hide();
                        right.Show(gfx, look, 1f, new POINT { x = 900, y = 720 }, Display, Edge.Bottom, second);
                    });
                var b = right.PanelOnScreen;
                Assert.Equal(kept, right.Made with { Surfaces = 0 });
                if (!look.Animations)
                {
                    film.Holds();
                }
                film.StaysInSight(0.6, Inset(a), Inset(b));
                right.Hide();
                FrameFilm.Settle();
            }
        });
    }

    /// <summary>
    /// Where a folder was left (FolderMemory): the day picked, the month stepped to and a line's scroll are as
    /// they were when it opens there; opened afresh it is at its start — today, every line at its top.
    /// </summary>
    [UiFact]
    public void AFolderOpenedWhereItWasLeftIsOnItsDayScrolledAsItWas()
    {
        WindowShots.OnHiddenDesktop("retained-folder-memory", (gfx, instance) =>
        {
            var frozen = new DateTime(2026, 9, 22, 14, 40, 7, DateTimeKind.Local);
            var thursday = new DateOnly(2026, 9, 24);
            Clock.Now = () => frozen;
            var store = new CalendarStore();
            foreach (var calendar in CalendarWeatherFolderTests.Sample.GroupBy(item => item.Colour))
            {
                store.Add(new ListCalendar($"test:{calendar.Key}", calendar.Key.ToString(), calendar.Key, [.. calendar]));
            }
            CalendarIsland.Made = store;
            Nls.Formats = new LocalizationOptions { FirstDayOfWeek = DayOfWeek.Monday };
            try
            {
                var holder = Presets.Folder(Presets.Find(Presets.CalendarId)!, new PresetMachine(HasBattery: false, FixedDrives: []));
                using var folder = new FolderIsland($"{Owner}/{holder.Id}", holder, gfx, new Config());
                using var flyout = new FolderFlyout(gfx, instance, 0);
                var work = new RECT { left = 0, top = 0, right = 1600, bottom = 1600 };
                void Open(FolderMemory? recall) => flyout.Show(gfx, Still, 1f, new POINT { x = 800, y = 1600 }, work, Edge.Bottom, folder, recall: recall);
                Open(null);
                var month = folder.Tiles.Select(tile => tile.Island).OfType<MonthTile>().Single();
                var events = folder.Tiles.Select(tile => tile.Island).OfType<EventsTile>().Single();
                month.Click(MonthTile.Target(thursday));
                month.Click(MonthTile.NextTarget);
                events.ListScroll = 24;
                var stepped = month.Shown;
                Assert.Equal(thursday, events.Day);
                Assert.NotEqual(new DateOnly(2026, 9, 1), stepped);
                flyout.Hide();
                var left = flyout.Left;
                Assert.NotNull(left);

                // Afresh: at its start.
                Open(null);
                Assert.Equal(DateOnly.FromDateTime(frozen), events.Day);
                Assert.Equal(new DateOnly(2026, 9, 1), month.Shown);
                Assert.Equal(0, events.ListScroll);
                flyout.Hide();

                // Where it was left.
                Open(left);
                Assert.Equal(thursday, events.Day);
                Assert.Equal(stepped, month.Shown);
                Assert.Equal(24, events.ListScroll);
                flyout.Hide();
            }
            finally
            {
                Nls.Formats = null;
                CalendarIsland.Made = null;
                Clock.Now = Clock.Real;
            }
        });
    }

    /// <summary>The rule: in edit mode a folder left opens where it was until the session ends; at rest at its start.</summary>
    [Fact]
    public void AnEditModeFolderOpensWhereItWasLeftAndOneAtRestAtItsStart()
    {
        var memories = new FolderMemories();
        var memory = new FolderMemory(new Dictionary<string, LineMemory>(), new Dictionary<string, object?>(), null, null, false);
        memories.Left("layout:default/a", memory, editing: false);
        Assert.Null(memories.Recall("layout:default/a", editing: true));
        memories.Left("layout:default/a", memory, editing: true);
        Assert.Same(memory, memories.Recall("LAYOUT:DEFAULT/A", editing: true));
        Assert.Null(memories.Recall("layout:default/a", editing: false));
        Assert.Null(memories.Recall("layout:default/b", editing: true));
        memories.Left("layout:default/b", null, editing: true);
        Assert.Equal(1, memories.Count);
        memories.Clear();
        Assert.Null(memories.Recall("layout:default/a", editing: true));
    }

    // --- A menu over a folder ------------------------------------------------------------------------

    /// <summary>
    /// A line's menu standing beside it follows it where the folder shows it now (Dock.FoldersFollow): beside
    /// the line's new place, on its level, nothing of it made again.
    /// </summary>
    [UiFact]
    public void AMenuBesideALineFollowsItsFolder()
    {
        WindowShots.OnHiddenDesktop("retained-folder-menu", (gfx, instance) =>
        {
            var line = new RECT { left = 300, top = 400, right = 600, bottom = 440 };
            using var menu = new Flyout(gfx, instance, 0) { Configures = true, Configuration = true, TypeTitle = "Clock" };
            menu.BesideWidget = new MenuBeside(line, MenuSides.Order(onBar: false, Edge.Bottom, false));
            List<FlyoutItem> rows =
            [
                new FlyoutItem(1, "Switch", Control: new FlyoutControl.Toggle(true), Stays: true),
                new FlyoutItem(40, "More", Children: [.. Enumerable.Range(41, 6).Select(id => new FlyoutItem((uint)id, $"Row {id}"))]),
            ];
            menu.Show(gfx, Still, 1f, rows, FlyoutLayout.List, new POINT { x = 450, y = 420 }, Display, Edge.Bottom, null);
            menu.OpenLevel(40);
            FrameFilm.Settle();
            var stood = menu.PanelOnScreen;
            var made = menu.Made;
            var moved = line with { left = line.left + 200, right = line.right + 200, top = line.top - 150, bottom = line.bottom - 150 };
            menu.Follow(new MenuBeside(moved, MenuSides.Order(onBar: false, Edge.Bottom, false)));
            var now = menu.PanelOnScreen;
            Assert.NotEqual(stood, now);
            // Beside it as before: on the same side, as far from it, clear of it.
            Assert.Equal(stood.left - line.left, now.left - moved.left);
            Assert.True(now.left >= moved.right || now.right <= moved.left, $"the menu {now.left}..{now.right} over the line {moved.left}..{moved.right}");
            Assert.Equal(1, menu.InlineDepth);
            Assert.Equal(made.Elements, menu.Made.Elements);
        });
    }

    // --- The source rule -----------------------------------------------------------------------------

    /// <summary>
    /// Tier 4, a source rule: a folder hands its host nothing per showing — what it makes is its elements' and
    /// its frame's (FolderFlyout.Retained.cs), handed over only as the folder goes for good (ReleaseKept) — and
    /// empties nothing: no line, list or button is made again by emptying the content. Its tooltip's own host
    /// keeps its words per tooltip (FolderFlyout.Tips.cs), until tips are kept views too (phase 5).
    /// </summary>
    [Fact]
    public void AFolderHandsItsHostNothingPerShowing()
    {
        if (RepoSource.Root().Length == 0)
        {
            return;
        }
        var folder = Path.Combine(RepoSource.Root(), "Karpo.Dock");
        var keeps = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var emptied = new List<string>();
        foreach (var file in Directory.GetFiles(folder, "FolderFlyout*.cs"))
        {
            var tree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(File.ReadAllText(file));
            var calls = tree.GetRoot().DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax>().ToList();
            var kept = calls.Count(call => call.Expression is Microsoft.CodeAnalysis.CSharp.Syntax.MemberAccessExpressionSyntax
            {
                Expression: Microsoft.CodeAnalysis.CSharp.Syntax.IdentifierNameSyntax { Identifier.Text: "host" },
                Name.Identifier.Text: "Keep",
            });
            if (kept > 0)
            {
                keeps[Path.GetFileName(file)] = kept;
            }
            // A visual's children emptied: Children.RemoveAll().
            emptied.AddRange(calls.Where(call => call.Expression is Microsoft.CodeAnalysis.CSharp.Syntax.MemberAccessExpressionSyntax
                {
                    Name.Identifier.Text: "RemoveAll",
                    Expression: Microsoft.CodeAnalysis.CSharp.Syntax.MemberAccessExpressionSyntax { Name.Identifier.Text: "Children" },
                })
                .Select(call => $"{Path.GetFileName(file)}:{call.GetLocation().GetLineSpan().StartLinePosition.Line + 1}"));
        }
        Assert.Equal(new Dictionary<string, int> { ["FolderFlyout.Retained.cs"] = 6 }, keeps);
        Assert.Empty(emptied);
    }

    // --- Helpers ---------------------------------------------------------------------------------

    private static RECT Inset(RECT rect) => new() { left = rect.left + 6, top = rect.top + 6, right = rect.right - 6, bottom = rect.bottom - 6 };

    /// <summary>A folder of lists of battery lines, as many as <paramref name="counts"/> says a list, each its own id.</summary>
    private static FolderOptions Lists(params int[] counts)
    {
        var folder = new FolderOptions { Lists = [] };
        var next = 0;
        foreach (var count in counts)
        {
            folder.Lists.Add([.. Enumerable.Range(0, count).Select(_ => new FolderCell { Island = Line($"line{next++}") })]);
        }
        return folder;
    }

    private static CellItem Line(string id)
    {
        var island = ListKind.Battery.New();
        island.Id = id;
        island.View = FolderView.List;
        return island;
    }

    private static FolderPlan Plan(FolderOptions options) =>
        FolderFlyout.Build(Still, 1f, FolderView.List, options, options.Cells, 720, editing: true);

    private static BarItem Folder(string id, FolderOptions lists)
    {
        var island = BarKind.FolderList.New();
        island.Id = id;
        island.Folder = lists;
        return island;
    }

    /// <summary><paramref name="folder"/> open over a bottom bar, arranged or at rest, its commit landed.</summary>
    private static FolderFlyout Shown(Gfx gfx, HINSTANCE instance, FolderIsland folder, bool arranged)
    {
        var flyout = new FolderFlyout(gfx, instance, 0);
        flyout.Show(gfx, Still, 1f, new POINT { x = 640, y = 720 }, Display, Edge.Bottom, folder, editing: arranged, display: Display);
        FrameFilm.Settle();
        return flyout;
    }
}
