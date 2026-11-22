// The folder rule (docs\widgets.md §15), in the user's words: "generally a widget cannot be clicked
// in any folder view. with this i mean that no flyout should open when the widget is clicked."
//
// The rule is in the types, and what these hold is that nothing gets round them:
//
//   * a folder holds list lines, and what a click on one may do is a CellAct — nothing, or an
//     act — so none has a popup to declare;
//   * the folder hands the dock one thing, an app opening from a widget whose click acts, and
//     decides that before it closes itself, so a click that does nothing leaves it open;
//   * the click table is keyed by taskbar entry, and the two buttons that open one of Windows'
//     popups inside their own click — second screen and cast — are taskbar entries only;
//   * the folder itself is on the bar, and its widgets know it holds them.
//
// No test here clicks a widget that acts on the machine. A graph's click opens Task Manager and the
// security widget's opens Windows Security, both through a ShellExecute that no isolation stops, so
// the per-kind checks read each widget's own declaration of what its click does instead.

using Karpo.Core;
using Karpo.Dock;
using Karpo.Dock.QuickSettings.Services;
using Karpo.Tests.Islands;

// Karpo.Dock is the namespace as well as the class, and the namespace wins from in here.
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Folders;

public sealed class FolderClickRuleTests
{
    /// <summary>
    /// In a folder only an app opens, and only from a widget whose click acts: every other thing a
    /// click can carry — one of Karpo's flyouts, a list of items — is a popup, and is not forwarded
    /// whatever the widget says. A click that carries nothing forwards nothing either: that is a
    /// widget that did its own thing (a switch flipped, a slider moved). The other things carried
    /// out are a desktop chosen in the desktops' line (§116) and a layout in the layouts' line
    /// (§127), which the dock switches to once the folder has gone, and which open nothing.
    /// </summary>
    [Fact]
    public void InAFolderOnlyAnAppOpens()
    {
        foreach (var view in Enum.GetValues<IslandView>())
        {
            var action = new IslandAction(View: view);
            Assert.Equal(view is IslandView.App or IslandView.Desktops or IslandView.Layouts, FolderFlyout.Forwards(action, CellAct.Action));
            Assert.False(FolderFlyout.Forwards(action, CellAct.Nothing), $"{view} is carried out of a folder by a widget whose click does nothing");
        }

        var items = new IslandAction(Items: [new FlyoutItem(1, "a")]);
        foreach (var act in Enum.GetValues<CellAct>())
        {
            Assert.False(FolderFlyout.Forwards(items, act), $"a list of items is carried out of a folder by a widget that says {act}");
            Assert.False(FolderFlyout.Forwards(default, act));
        }
    }

    /// <summary>
    /// What a click in a folder may do is asked of the line the folder shows, and a widget a list
    /// cannot hold does nothing there. A pin's line acts, which is what keeps an app folder an app
    /// folder.
    /// </summary>
    [Fact]
    public void AFolderAsksTheLineItShows()
    {
        foreach (var line in ListWidgets.All)
        {
            Assert.Equal(line.Act, FolderFlyout.ActOf(CellOf(line.Kind)));
        }
        foreach (var id in WidgetInfos.All.Select(info => info.Id))
        {
            if (ListWidgets.Find(id) is null)
            {
                Assert.Equal(CellAct.Nothing, FolderFlyout.ActOf(CellOf(id)));
            }
        }

        var pinLines = ListWidgets.All.Where(line => line.Drawing == WidgetSource.PinTile).ToList();
        Assert.NotEmpty(pinLines);
        Assert.All(pinLines, line => Assert.Equal(CellAct.Action, line.Act));
    }

    /// <summary>
    /// "Opens a popup" is two of the four answers a taskbar entry can give, and exactly those two:
    /// one of Karpo's flyouts or one of Windows' panels. The eight that open Windows' own are
    /// declared so, and none of them is in Karpo's table — those popups are Windows', not Karpo's.
    /// Seven have no line, so no folder can hold one. Task view has one, at the person's asking
    /// (2026-09-27), and there its click is an act, Win+Tab, as the bar's is.
    /// Project left this list when it got a page of Karpo's own (Win+P's modes); cast left it for the
    /// wireless displays' page, came back when that went, and left the bar for the cast preset
    /// (2026-10-11).
    /// </summary>
    [Fact]
    public void TheWindowsPopupsAreDeclaredAsPanels()
    {
        BarKind[] panels =
        [
            BarKind.WidgetsBoard, BarKind.Start, BarKind.Search, BarKind.TaskView,
            BarKind.PenMenu, BarKind.TouchKeyboard, BarKind.VirtualTouchpad,
            BarKind.QuickLink, BarKind.WindowsNotifications, BarKind.WindowsQuickSettings, BarKind.WindowsSound,
            BarKind.WindowsNetworks, BarKind.WindowsCast, BarKind.WindowsClipboard, BarKind.EmojiPanel,
        ];
        foreach (var kind in panels)
        {
            Assert.Equal(BarClick.Panel, BarEntries.Of(kind).Click);
            Assert.False(TheDock.HasFlyout(kind), $"{kind} opens one of Windows' popups and has one of Karpo's too");
            if (kind == BarKind.TaskView)
            {
                Assert.Equal(CellAct.Action, ListWidgets.Of(ListKind.TaskView).Act);
                continue;
            }
            Assert.Null(ListWidgets.Find(kind.Id()));
        }
        Assert.Equal(panels.Order(), BarEntries.All.Where(entry => entry.Click == BarClick.Panel).Select(entry => entry.Kind).Order());
    }

    /// <summary>
    /// A folder's widgets are in the folder, and the folder itself is on the bar: a click on its
    /// button opens it, which the rule leaves alone. The folder hands its widgets itself as their
    /// container, and the folder has none.
    /// </summary>
    [UiFact]
    public void AFoldersWidgetsAreInItAndTheFolderIsOnTheBar()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var config = new Config();
            var options = new FolderOptions();
            var clock = ListKind.Clock.New();
            clock.Id = "clock";
            var battery = BarKind.EnergySaver.New();
            battery.Id = "battery";
            options.Put(clock);
            options.Put(battery.AsCell());
            var island = BarKind.FolderList.New();
            island.Id = "folder";
            island.Folder = options;

            using var folder = new FolderIsland(Config.IslandKey(null, island.Id), island, gfx, config);

            Assert.Equal(2, folder.Tiles.Count);
            Assert.All(folder.Tiles, tile => Assert.Same(folder, ((CellIsland)tile.Island).Container));
            Assert.Null(folder.Container);
        });
    }

    /// <summary>A folder cell holding a fresh widget of <paramref name="kind"/>.</summary>
    private static FolderCell CellOf(ListKind kind) => new() { Island = kind.New() };

    /// <summary>A cell holding a widget of the word <paramref name="id"/>, as a folder read from a file would.</summary>
    private static FolderCell CellOf(string id) => new() { Island = new CellItem { Id = "probe", Kind = id } };

    /// <summary>A switch that counts its clicks and does nothing with them.</summary>
    private sealed class Recorded : IQuickSwitch
    {
        public int Acts { get; private set; }

        public string Name => "Recorded";

        public bool Available => true;

        public bool? On => null;

        public string? Note => null;

        public void Start(Action changed)
        {
        }

        public void Act() => Acts++;

        public void Dispose()
        {
        }
    }
}
