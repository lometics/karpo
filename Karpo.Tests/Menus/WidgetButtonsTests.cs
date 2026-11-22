// A widget's menu's icon buttons — "Cut", "Copy", "Remove", a row as Windows 11's context menus
// have Cut, Copy and Delete (the person, 2026-10-11) — and the copy "Copy" makes: where the menu puts
// the row (Flyout.WithPrimaryRow: always on top, "the edit row should be on top of the context menu
// action"), Remove last, and the copy (Dock.DuplicateIn): new ids
// throughout, the options copied, a folder's widgets copied with it, the copy right after the
// original. Pure: no window. The menu's own drawing of them is MenuModePictures'.

using Karpo.Dock;
using CoreFolders = Karpo.Core.Folders;
using DockIslands = Karpo.Dock.Islands;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Menus;

public sealed class WidgetButtonsTests
{
    private static readonly FlyoutItem Rule = new(0, "", Separator: true);

    private static string[] Words(IEnumerable<FlyoutItem> rows) => [.. rows.Select(row => row.Separator ? "—" : row.Text)];

    // --- Where the menu puts them ---------------------------------------------------------------

    /// <summary>
    /// On top of the menu, whichever way it opens, ruled off from the rows under it: Cut, Copy, then
    /// Remove last (§220), all three together at the row's far end (§350) — nothing of the bar's, no
    /// "Add a widget" and no "Done editing". Edit mode's alone (§253). No "Settings" (§228: a widget's settings are its menu's own rows),
    /// the tray's neither: its icons are its menu's rows too (§261).
    /// </summary>
    [Fact]
    public void TheRowIsCutCopyThenRemove()
    {
        var shown = Flyout.WithPrimaryRow([.. TheDock.WidgetButtons(), new(7, "Own"), new(8, "Other")]);

        Assert.Equal(["Cut", "Copy", "Remove", "—", "Own", "Other"], Words(shown));
        Assert.Equal(["Cut (Ctrl+X)", "Copy (Ctrl+C)", "Remove (Del)"], shown.Take(3).Select(Flyout.TipText));

        foreach (var island in Enum.GetValues<BarKind>().Select(kind => kind.New()))
        {
            var rows = TheDock.WidgetRowsFor(island);
            Assert.Equal(["Cut", "Copy", "Remove"], Words(rows));
            Assert.DoesNotContain(rows, row => row.Id is TheDock.CMD_TASKBAR_SETTINGS);
        }
        Assert.DoesNotContain(TheDock.WidgetRowsFor(ListKind.Clock.New()), row => row.Text == S.DockMenuSettings);
        Assert.DoesNotContain(TheDock.WidgetRowsFor(BarKind.Tray.New()), row => row.Text == S.DockMenuSettings);
    }

    /// <summary>The buttons' going leaves no rule at an end of the rest, nor two together; alone, no rule at all.</summary>
    [Fact]
    public void TheRowLeavesNoStrayRules()
    {
        FlyoutItem button = new(1, "B", Primary: true);
        FlyoutItem[] items = [Rule, new(7, "Own"), button, Rule, Rule, new(8, "Other"), Rule];

        Assert.Equal(["B", "—", "Own", "—", "Other"], Words(Flyout.WithPrimaryRow(items)));
        Assert.Equal(["B"], Words(Flyout.WithPrimaryRow([Rule, button, Rule])));
        // A menu without buttons is as it was, rules and all.
        FlyoutItem[] plain = [new(7, "Own"), Rule, new(8, "Other")];
        Assert.Same(plain, Flyout.WithPrimaryRow(plain));
    }

    // --- Cut -------------------------------------------------------------------------------------

    /// <summary>
    /// Cut (§220) takes what Copy takes of what it acts on — the bar's widgets where there are any
    /// among them, else the lines — out of the file in one change; Ctrl+V puts them back where it is
    /// pressed, the same kinds and options with new ids.
    /// </summary>
    [Fact]
    public void CutTakesWhatCopyTakesAndPastePutsItBack()
    {
        var clock = new CellItem { Id = "c1", Kind = ListKind.Clock.Id(), Clock = new ClockOptions { Label = "Home" } };
        var folder = new BarItem { Id = "f", Kind = BarKind.FolderList, Folder = new FolderOptions { Lists = CellItems.OneList(clock) } };
        var light = new BarItem { Id = "a", Kind = BarKind.NightLight };
        var config = Bar(out var owner, light, folder, new BarItem { Id = "b", Kind = BarKind.ShowDesktop });
        var list = config.ListFor(owner);
        var a = Config.IslandKey(owner, "a");
        var line = Config.IslandKey(owner, CoreFolders.Path("f", "c1"));

        Assert.Equal([a], TheDock.Clipped(config, [line, a]));
        Assert.Equal([line], TheDock.Clipped(config, [line, Config.IslandKey(owner, "gone")]));
        Assert.Empty(TheDock.Clipped(config, [Config.IslandKey(owner, "gone")]));

        // What the clipboard keeps (Clip: a clone as it is now), then the cut.
        var place = Sections.Of(light);
        List<BarItem> clipboard = [light.Clone()];
        Assert.Equal([a], TheDock.RemoveFrom(config, TheDock.Clipped(config, [a])));
        Assert.Equal(["f", "b"], list.Select(entry => entry.Id));

        var pasted = IslandEdits.Paste(list, clipboard, new BarSlot(place, 0));
        var back = Assert.Single(pasted);
        Assert.Equal(BarKind.NightLight, back.Kind);
        Assert.True(back.SameOptions(clipboard[0]));
        Assert.Equal([back.Id, "f", "b"], list.Select(entry => entry.Id));

        // A line alone: out of its folder, its folder still there.
        Assert.Equal([line], TheDock.RemoveFrom(config, TheDock.Clipped(config, [line])));
        Assert.Empty(folder.Folder!.Cells);
        Assert.Contains(list, entry => entry.Id == "f");
    }

    // --- Duplicate --------------------------------------------------------------------------------

    /// <summary>A bar's entry: a copy with a new id and the same options, straight after the original.</summary>
    [Fact]
    public void ABarsEntryIsCopiedRightAfterItWithANewId()
    {
        var config = Bar(out var owner, new BarItem { Id = "a", Kind = BarKind.NightLight }, new BarItem { Id = "b", Kind = BarKind.ShowDesktop });
        var list = config.ListFor(owner);

        var key = TheDock.DuplicateIn(config, Config.IslandKey(owner, "a"));

        Assert.NotNull(key);
        Assert.Equal(["a", DockIslands.IdOf(key!), "b"], list.Select(entry => entry.Id));
        Assert.NotEqual("a", list[1].Id);
        Assert.True(list[1].SameOptions(list[0]));
    }

    /// <summary>
    /// A folder: copied with its widgets, every one of them a copy with a new id and its own options,
    /// the original's widgets untouched; the copy straight after the folder.
    /// </summary>
    [Fact]
    public void AFolderIsCopiedWithItsWidgetsAllWithNewIds()
    {
        var clock = new CellItem { Id = "c1", Kind = ListKind.Clock.Id(), Clock = new ClockOptions { TimeZone = "Tokyo Standard Time", Label = "Tokyo" } };
        var cpu = new CellItem { Id = "c2", Kind = ListKind.Cpu.Id() };
        var folder = new BarItem { Id = "f", Kind = BarKind.FolderList, Folder = new FolderOptions { Lists = CellItems.OneList(clock, cpu) } };
        var config = Bar(out var owner, folder, new BarItem { Id = "b", Kind = BarKind.ShowDesktop });
        var list = config.ListFor(owner);

        var key = TheDock.DuplicateIn(config, Config.IslandKey(owner, "f"));

        Assert.NotNull(key);
        var copy = list[1];
        Assert.Equal(["f", copy.Id, "b"], list.Select(entry => entry.Id));
        Assert.Equal(BarKind.FolderList, copy.Kind);
        var cells = copy.Folder!.Cells.Select(cell => cell.Island).ToList();
        Assert.Equal([ListKind.Clock.Id(), ListKind.Cpu.Id()], cells.Select(cell => cell.Kind));
        // New ids throughout, none the same as any other in the list.
        var ids = list.Select(entry => entry.Id).Concat(list.SelectMany(entry => entry.Folder?.Cells ?? []).Select(cell => cell.Island.Id)).ToList();
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(["c1", "c2"], folder.Folder!.Cells.Select(cell => cell.Island.Id));
        // The options copied, and a copy of their own: changing the copy's leaves the original's.
        Assert.Equal("Tokyo Standard Time", cells[0].Clock!.TimeZone);
        Assert.Equal("Tokyo", cells[0].Clock!.Label);
        cells[0].Clock!.Label = "Elsewhere";
        Assert.Equal("Tokyo", clock.Clock!.Label);
    }

    /// <summary>A folder's widget: a copy with a new id and its options, right under it in its own list.</summary>
    [Fact]
    public void AFoldersWidgetIsCopiedRightUnderIt()
    {
        var clock = new CellItem { Id = "c1", Kind = ListKind.Clock.Id(), Clock = new ClockOptions { Label = "Home" } };
        var cpu = new CellItem { Id = "c2", Kind = ListKind.Cpu.Id() };
        var other = new CellItem { Id = "c3", Kind = ListKind.Cpu.Id() };
        var holder = new FolderOptions { Lists = CellItems.OneList(clock, cpu) };
        holder.Put(other, list: 1);
        var config = Bar(out var owner, new BarItem { Id = "f", Kind = BarKind.FolderList, Folder = holder });
        CoreFolders.Project(holder, CoreFolders.View(BarKind.FolderList));

        Assert.True(TheDock.CanDuplicate(config, Config.IslandKey(owner, CoreFolders.Path("f", "c1"))));
        var key = TheDock.DuplicateIn(config, Config.IslandKey(owner, CoreFolders.Path("f", "c1")));

        Assert.NotNull(key);
        var first = holder.Lists[0].Select(cell => cell.Island).ToList();
        Assert.Equal(3, first.Count);
        Assert.Equal("c1", first[0].Id);
        Assert.Equal(CoreFolders.Path("f", first[1].Id), DockIslands.IdOf(key!));
        Assert.NotEqual("c1", first[1].Id);
        Assert.Equal(ListKind.Clock.Id(), first[1].Kind);
        Assert.Equal("Home", first[1].Clock!.Label);
        Assert.Equal("c2", first[2].Id);
        // The other list is as it was.
        Assert.Equal(["c3"], holder.Lists[1].Select(cell => cell.Island.Id));
    }

    /// <summary>A widget the file has lost has nothing to copy: greyed, and nothing changes.</summary>
    [Fact]
    public void AWidgetThatIsGoneCannotBeDuplicated()
    {
        var config = Bar(out var owner, new BarItem { Id = "a", Kind = BarKind.NightLight });

        Assert.False(TheDock.CanDuplicate(config, Config.IslandKey(owner, "gone")));
        Assert.Null(TheDock.DuplicateIn(config, Config.IslandKey(owner, "gone")));
        Assert.Single(config.ListFor(owner));
    }

    /// <summary>
    /// Ctrl+V of a folder's line copied (Dock.EditCopy.cs, PasteLineIn): a copy with new ids and the
    /// options copied, at the end of the open folder's first list — in this layout's folder or another's —
    /// as often as it is pasted; nothing into a folder that is gone.
    /// </summary>
    [Fact]
    public void ACopiedLineIsPastedIntoAnOpenFolderWithNewIds()
    {
        var clock = new CellItem { Id = "c1", Kind = ListKind.Clock.Id(), Clock = new ClockOptions { Label = "Home" } };
        var holder = new FolderOptions { Lists = CellItems.OneList(new CellItem { Id = "c2", Kind = ListKind.Cpu.Id() }) };
        var config = Bar(out var owner, new BarItem { Id = "f", Kind = BarKind.FolderList, Folder = holder });

        var first = TheDock.PasteLineIn(config, Config.IslandKey(owner, "f"), clock);
        var second = TheDock.PasteLineIn(config, Config.IslandKey(owner, "f"), clock);

        var lines = holder.Lists[0].Select(cell => cell.Island).ToList();
        Assert.Equal(3, lines.Count);
        Assert.Equal("c2", lines[0].Id);
        Assert.Equal([CoreFolders.Path("f", lines[1].Id), CoreFolders.Path("f", lines[2].Id)], [DockIslands.IdOf(first!), DockIslands.IdOf(second!)]);
        Assert.DoesNotContain(lines, line => line.Id == "c1");
        Assert.NotEqual(lines[1].Id, lines[2].Id);
        Assert.Equal("Home", lines[2].Clock!.Label);
        Assert.Null(TheDock.PasteLineIn(config, Config.IslandKey(owner, "gone"), clock));
    }

    /// <summary>A layout of its own with <paramref name="entries"/> on its bar; <paramref name="owner"/> its list's owner.</summary>
    private static Config Bar(out string owner, params BarItem[] entries)
    {
        var config = new Config();
        config.Layouts.Clear();
        config.Layouts.Add(new TaskbarLayout { Id = "only", Name = "Default", Islands = [.. entries] });
        owner = Config.LayoutOwner("only");
        return config;
    }
}
