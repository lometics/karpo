// §528 (the person, 2026-11-07: "when i select multiple widgets it still does not show me the combination of the settings ...
// also for taskbar based widgets?"): several widgets selected in edit mode show the settings they have in common, one page
// of the one row model (OptionRows.Common, OptionMenu.Settings over targets), and a setting set there is set on them all.
// Held here, without a window:
//
//  - two of one kind share their whole page; two kinds the rows they share, nothing of one kind's own (§567: no look of
//    their own to share);
//  - a value they do not agree on is mixed: a toggle neither on nor off, a choice of none, each saying "Mixed";
//  - a pick and a typed number are set on every one, written into one config: one step of the undo;
//  - every widget kind there is — the taskbar's own (Start, Search, Task view, the apps, the tray, Show desktop …), every
//    line of a folder, a folder — paired with another: every row in common may be set.

using Karpo.Dock;
using Karpo.Tests.Menus;

namespace Karpo.Tests.Edit;

public sealed class CommonSettingsTests
{
    /// <summary><paramref name="items"/> on the default layout of a fresh config, as targets of a page.</summary>
    private static (Config Config, List<OptionTarget> Targets) Selected(params WidgetItem[] items)
    {
        var config = MenuCorpus.Fresh();
        var place = new OptionPlace(config, Dark: true) { Layout = config.DefaultLayout };
        return (config, [.. items.Select(item => new OptionTarget(item, MenuCorpus.Sources, place))]);
    }

    /// <summary>A row's keys and its level's, flattened.</summary>
    private static List<string> Keys(IEnumerable<OptionRow> rows) => [.. OptionRows.Flat(rows).Select(row => row.Key)];

    private static OptionPick PickOf(List<OptionPick> picks, OptionPickKind kind, string key, int value = 0) =>
        picks.First(pick => pick.Kind == kind && pick.Key == key && pick.Value == value);

    [Fact]
    public void TwoOfOneKindShareTheirWholePage()
    {
        var (_, targets) = Selected(ListKind.Clock.New(), ListKind.Clock.New());
        var place = targets[0].Place!;
        var one = OptionRows.Of(targets[0].Item, targets[0].Devices, place);
        var both = OptionRows.Common([one, OptionRows.Of(targets[1].Item, targets[1].Devices, place)]);
        // Every row but the person's own things and a list put in order, which are no setting two widgets share.
        Assert.Equal(Keys(one.Where(row => row.Owned is null && !row.Ordered && row.Kind is not (OptionRowKind.Things or OptionRowKind.Note))), Keys(both));
        Assert.DoesNotContain(OptionRows.Flat(both), row => row.Mixed);
    }

    [Fact]
    public void TwoKindsShareNoneOfOnesOwn()
    {
        var (_, targets) = Selected(ListKind.Clock.New(), ListKind.WeatherNow.New());
        var place = targets[0].Place!;
        var own = OptionRows.Common([OptionRows.Of(targets[0].Item, targets[0].Devices, place), OptionRows.Of(targets[1].Item, targets[1].Devices, place)]);
        // A style is a style of other choices under the same key: another setting.
        Assert.DoesNotContain(own, row => row.Key == OptionRows.Style);
        var clock = Keys(OptionRows.Of(targets[0].Item, targets[0].Devices, place));
        var weather = Keys(OptionRows.Of(targets[1].Item, targets[1].Devices, place));
        Assert.All(Keys(own), key => Assert.True(clock.Contains(key) && weather.Contains(key), key));
        // §567: no look of their own in common.
        var items = OptionMenu.Settings(targets, [], FolderSide.Face);
        Assert.DoesNotContain(items, item => item.Text == S.SettingsAppearanceHeader);
    }

    [Fact]
    public void AValueTheyDoNotAgreeOnIsMixed()
    {
        var (_, targets) = Selected(ListKind.Clock.New(), ListKind.Clock.New());
        (targets[0].Item!.Clock ??= new ClockOptions()).Seconds = true;
        var place = targets[0].Place!;
        var seconds = OptionRows.Find(OptionRows.Common([OptionRows.Of(targets[0].Item, targets[0].Devices, place), OptionRows.Of(targets[1].Item, targets[1].Devices, place)]), OptionRows.ClockSeconds)!;
        Assert.True(seconds.Mixed);
        Assert.False(seconds.On);
        // On the panel: a switch neither on nor off.
        var page = PanelRows.Page(PanelPage.Widget, "", "", OptionMenu.Settings(targets, [], FolderSide.Face));
        var row = PanelRows.All(page.Rows).First(each => each.Item.Setting == OptionRows.ClockSeconds);
        Assert.True(row.Mixed);
    }

    [Fact]
    public void AToggleTheyDoNotAgreeOnIsNeitherAndTurnsThemAllOn()
    {
        var a = OptionRows.Common([
            new OptionRow("k", OptionRowKind.Toggle, "Seconds") { On = true },
            new OptionRow("k", OptionRowKind.Toggle, "Seconds") { On = false },
        ])!;
        Assert.True(a.Mixed);
        Assert.False(a.On);
        var on = new[] { true, false };
        var row = OptionRows.Common([
            new OptionRow("k", OptionRowKind.Toggle, "Seconds") { On = on[0], Set = value => (on[0] = value != 0) | true },
            new OptionRow("k", OptionRowKind.Toggle, "Seconds") { On = on[1], Set = value => (on[1] = value != 0) | true },
        ])!;
        Assert.True(OptionMenu.ApplyRow(row, new OptionPick(OptionMenu.FirstId, OptionPickKind.Flip, Key: "k")));
        Assert.Equal([true, true], on);
        // A toggle's control: neither, for the panel to draw and a screen reader to hear as Indeterminate.
        var item = OptionMenu.Rows([a], []).Single();
        Assert.True(((FlyoutControl.Toggle)item.Control!).Mixed);
        Assert.True(PanelRows.One(item, "Widget")!.Mixed);
    }

    [Fact]
    public void ANumberTheyDoNotAgreeOnSaysMixedAndIsTypedForAll()
    {
        var values = new[] { 4, 8 };
        OptionRow Number(int at) => new("n", OptionRowKind.Number, "Gap") { Value = values[at], Least = at == 0 ? 0 : 2, Most = at == 0 ? 20 : 16, Set = value => (values[at] = value) == value };
        var row = OptionRows.Common([Number(0), Number(1)])!;
        Assert.True(row.Mixed);
        Assert.Equal(S.SettingsValueMixed, row.Shown);
        // Within the range both take.
        Assert.Equal((2, 16), (row.Least, row.Most));
        var item = OptionMenu.Rows([row], []).Single();
        var field = (FlyoutField)item.Children!.Single().Control!;
        Assert.Equal(("", S.SettingsValueMixed), (field.Text, field.Placeholder));
        // The first's own value typed sets the other too.
        Assert.True(FieldPicks.KeepRow(row, new FieldCommit(OptionMenu.FirstId, "4", null)));
        Assert.Equal([4, 4], values);
        // Choices of other words are another setting.
        Assert.Null(OptionRows.Common([
            new OptionRow(OptionRows.Style, OptionRowKind.Choice, "Style") { Choices = ["Digital", "Analog"] },
            new OptionRow(OptionRows.Style, OptionRowKind.Choice, "Style") { Choices = ["Simple", "Detailed"] },
        ]));
        // Each one's own "Default (…)" is the same choice: unset.
        var font = OptionRows.Common([
            new OptionRow("f", OptionRowKind.Choice, "Font") { Choices = [S.SettingsValueDefault("Segoe UI"), "Sitka"], Chosen = 0 },
            new OptionRow("f", OptionRowKind.Choice, "Font") { Choices = [S.SettingsValueDefault("Bahnschrift"), "Sitka"], Chosen = 0 },
        ])!;
        Assert.Equal([S.SettingsValueDefault(S.SettingsValueMixed), "Sitka"], font.Choices);
        Assert.False(font.Mixed);
    }

    [Fact]
    public void APickIsSetOnThemAllInOneConfigOneStepOfTheUndo()
    {
        var config = MenuCorpus.Fresh();
        var folder = BarKind.FolderList.New();
        _ = folder.Folder!.Put(ListKind.Clock.New());
        _ = folder.Folder!.Put(ListKind.Clock.New());
        config.DefaultLayout.Islands.Add(folder);
        var ids = folder.Folder.Cells.Select(cell => cell.Island!.Id).ToList();
        var before = config.Clone();
        var history = new EditHistory<Config>();
        // As the dock does it (Dock.OptionPicked): one copy, every widget in it, applied and recorded once.
        var next = config.Clone();
        var place = new OptionPlace(next, Dark: true) { Layout = next.DefaultLayout };
        var owner = Config.LayoutOwner(next.DefaultLayout.Id);
        List<OptionTarget> targets = [.. ids.Select(id => new OptionTarget(next.FindIsland(owner, Karpo.Core.Folders.Path(folder.Id, id)), null, place))];
        List<OptionPick> picks = [];
        _ = OptionMenu.Settings(targets, picks, FolderSide.Face);
        Assert.True(OptionMenu.Apply(targets, PickOf(picks, OptionPickKind.Flip, OptionRows.ClockSeconds)));
        Assert.All(targets, target => Assert.True(target.Item!.Clock?.Seconds));
        history.Record(config);
        config = next;
        // Ctrl+Z: one step back, both as they were.
        var undone = history.Undo(config)!;
        Assert.False(history.CanUndo);
        Assert.Equal(before.ToJson(), undone.ToJson());
    }

    /// <summary>Every widget kind there is, each paired with another of its place (a bar's with Start, a folder's line with a clock), and a folder with a bar's widget.</summary>
    public static IEnumerable<object[]> Pairs()
    {
        foreach (var kind in Enum.GetValues<BarKind>())
        {
            yield return ["widget " + kind, kind, null!, BarKind.Start];
        }
        foreach (var kind in Enum.GetValues<ListKind>())
        {
            yield return ["line " + kind, null!, kind, null!];
        }
    }

    /// <summary>
    /// Every kind paired: every row the page has in common may be set on both (a choice, a switch, a step, a number or a text
    /// typed) without throwing; §567 no look among them.
    /// </summary>
    [Theory]
    [MemberData(nameof(Pairs))]
    public void EveryKindPairedSetsEveryRowInCommon(string name, BarKind? bar, ListKind? line, BarKind? partner)
    {
        WidgetItem first = bar is { } kind ? kind.New() : line!.Value.New();
        WidgetItem second = partner is { } other ? other.New() : ListKind.Clock.New();
        if (first.Pin is { } pin)
        {
            pin.Target = @"C:\Users\Ada\Documents";
        }
        var (_, targets) = Selected(first, second);
        List<OptionPick> picks = [];
        var items = OptionMenu.Settings(targets, picks, FolderSide.Face);
        Assert.DoesNotContain(items, item => item.Text == S.SettingsAppearanceHeader);
        // Every row set on both, a few of each, none throwing.
        foreach (var group in picks.Where(pick => pick.Kind is OptionPickKind.Set or OptionPickKind.Flip or OptionPickKind.Step or OptionPickKind.Act or OptionPickKind.Field).GroupBy(pick => (pick.Kind, pick.Key)))
        {
            foreach (var pick in group.Take(3))
            {
                var row = OptionMenu.Row(targets, pick);
                if (row is null || row.Ask is not null)
                {
                    continue;
                }
                if (pick.Kind == OptionPickKind.Field)
                {
                    if (row.Kind is OptionRowKind.Number or OptionRowKind.Text)
                    {
                        _ = FieldPicks.Keep(targets, pick, new FieldCommit(pick.Id, "7", null));
                    }
                    continue;
                }
                _ = OptionMenu.Apply(targets, pick);
            }
        }
        Assert.NotEmpty(name);
    }

    /// <summary>A folder and a bar's widget: the folder's faces none of what they have in common.</summary>
    [Fact]
    public void AFolderAndAWidgetShareNoFaces()
    {
        var (_, targets) = Selected(BarKind.FolderList.New(), BarKind.TaskView.New());
        var items = OptionMenu.Settings(targets, [], FolderSide.Face);
        Assert.DoesNotContain(items, item => item.Text == S.SettingsFolderFacesAddHeader || item.Text == S.SettingsAppearanceHeader);
    }
}
