// menus: a widget's settings and a folder's faces in its menu (2026-10-19, the person: "the settings for
// the processor graph, inline them into the context menu, all of them" and "in edit mode for the
// taskbar, you should inline the settings for the folder faces into the left click menu ... adding and
// removing and reordering a folder face should all be possible"; Karpo.Core\OptionRows.cs,
// Karpo.Dock\OptionMenu.cs, docs\widgets.md §214). Held here:
//
//  - **Each kind's row.** A toggle a switch at the row's end; a choice a submenu, its choice ticked and
//    named beside the chevron; a number a stepper; an action a row. No "More settings…" (§228).
//  - **The same write as Settings.** A pick in the menu leaves the widget or the folder exactly as the
//    page's control does: the same file, byte for byte.
//  - **The faces.** Added from the gallery's groups, a face the button cannot wear greyed; taken off,
//    never the last; moved earlier and later as the page's list moves them; a pick for a face that has
//    changed under the menu does nothing.
//  - **Nothing left out.** A performance graph's menu has every card its page has, read from the page's
//    markup, so the two cannot drift; a face's has every row its section has.
//  - **On a real menu** (KARPO_UI_TESTS=1): a toggle in a face's submenu acts from the keys and the
//    menu, the submenu with it, stays open through a Refresh. Pictures in KARPO_PNG_DIR:
//    option-menu-graph-<look>.png, option-menu-faces-<look>.png, option-menu-add-<look>.png and
//    option-menu-row-<look>.png, at 150 %.

using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using Karpo.Tests.Localization;
using TerraFX.Interop.Windows;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Menus;

[Collection(InLanguage.Collection)]
public sealed class OptionMenuTests
{
    private static readonly OptionDevices Devices = new(
        ["GPU 0: Intel UHD Graphics 770", "GPU 1: NVIDIA GeForce RTX 4060 Ti"],
        ["Disk 0 (C:)", "Disk 1 (E: F:)"]);

    /// <summary>A folder wearing <paramref name="faces"/>, side by side.</summary>
    private static BarItem Folder(params string[] faces)
    {
        var island = BarKind.FolderList.New();
        island.Id = "box";
        island.Folder!.Faces = [.. faces.Select(FolderFace.Of)];
        return island;
    }

    private static string Json(WidgetItem item) => item switch
    {
        BarItem bar => JsonSerializer.Serialize(bar, ConfigJson.Default.BarItem),
        CellItem cell => JsonSerializer.Serialize(cell, ConfigJson.Default.CellItem),
        _ => "",
    };

    private static T Copy<T>(T item)
        where T : WidgetItem => item switch
        {
            BarItem bar => (T)(WidgetItem)JsonSerializer.Deserialize(Json(bar), ConfigJson.Default.BarItem)!,
            CellItem cell => (T)(WidgetItem)JsonSerializer.Deserialize(Json(cell), ConfigJson.Default.CellItem)!,
            _ => item,
        };

    /// <summary>The pick of <paramref name="picks"/> that does <paramref name="kind"/> to <paramref name="key"/> of the face at <paramref name="face"/> (-1 the widget), with <paramref name="value"/>.</summary>
    private static OptionPick Pick(IReadOnlyList<OptionPick> picks, OptionPickKind kind, int face = -1, string key = "", int value = 0) =>
        picks.Single(pick => pick.Kind == kind && pick.Face == face && pick.Key == key && pick.Value == value);

    private static List<OptionPick> FacePicks(BarItem folder)
    {
        List<OptionPick> picks = [];
        _ = OptionMenu.FaceSection(folder.Folder!, picks, Devices);
        return picks;
    }

    // --- Each kind's row ---------------------------------------------------------------------------

    /// <summary>
    /// A toggle is a switch, a choice a submenu ticked and named, a number a stepper, an action a row
    /// of its own; no "More settings…" (§228: every setting is the menu's).
    /// </summary>
    [Fact]
    public void EachKindOfSettingHasItsRow()
    {
        OptionRow[] options =
        [
            new("on", OptionRowKind.Toggle, "Seconds") { On = true },
            new("pick", OptionRowKind.Choice, "Clock") { Choices = ["Region", "12-hour", "24-hour"], Chosen = 2 },
            new("count", OptionRowKind.Number, "Years") { Value = 3, Least = 1, Most = 3 },
            new("delete", OptionRowKind.Action, "Delete this list?") { Set = _ => true, Ask = new DeleteQuestion("Delete this list?", "", "Delete", "Cancel", 0) },
        ];
        List<OptionPick> picks = [];
        var rows = OptionMenu.Rows(options, picks);

        Assert.Equal(["Seconds", "Clock", "Years", "Delete this list?"], rows.Select(row => row.Text));
        Assert.All(rows, row => Assert.True(OptionMenu.Holds(row.Id)));
        Assert.Equal(new FlyoutControl.Toggle(true), rows[0].Control);
        Assert.Equal(OptionPickKind.Flip, OptionMenu.PickOf(picks, rows[0].Id)!.Kind);

        Assert.Null(rows[1].Accelerator);
        Assert.Equal(["Region", "12-hour", "24-hour"], rows[1].Children!.Select(child => child.Text));
        Assert.Equal([false, false, true], rows[1].Children!.Select(child => child.Checked == true));
        Assert.Equal(OptionPickKind.None, OptionMenu.PickOf(picks, rows[1].Id)!.Kind);
        Assert.Equal(new OptionPick(rows[1].Children![1].Id, OptionPickKind.Set, Key: "pick", Value: 1), OptionMenu.PickOf(picks, rows[1].Children![1].Id));

        var stepper = Assert.IsType<FlyoutControl.Stepper>(rows[2].Control);
        Assert.Equal(("3", true, false), (stepper.Value, stepper.CanFewer, stepper.CanMore));
        Assert.Equal(-1, OptionMenu.PickOf(picks, stepper.Fewer)!.Value);
        Assert.Equal(1, OptionMenu.PickOf(picks, stepper.More)!.Value);

        Assert.Equal(new OptionPick(rows[3].Id, OptionPickKind.Act, Key: "delete"), OptionMenu.PickOf(picks, rows[3].Id));
        Assert.DoesNotContain(rows, row => row.Text == S.DockEditMoreSettings);
        // Toggles, steps and choices keep the menu open (§219); an action, asked first, closes it.
        Assert.True(OptionMenu.KeepsOpen(OptionPickKind.Flip) && OptionMenu.KeepsOpen(OptionPickKind.Step) && OptionMenu.KeepsOpen(OptionPickKind.Set));
        Assert.True(rows[1].Children!.All(choice => choice.Stays));
        Assert.False(OptionMenu.KeepsOpen(OptionPickKind.Act) || rows[3].Stays);
        // None of them hands the screen away from a folder (FolderAfterMenu): the action's question is the dock's own.
        Assert.Equal([false, false, false, false], rows.Select(row => TheDock.PickHandsAway([], row.Id, picks)));
        Assert.Null(OptionMenu.PickOf(picks, 1));
        Assert.Null(OptionMenu.PickOf(picks, OptionMenu.FirstId + (uint)picks.Count));
    }

    /// <summary>
    /// Every level alike (§226, the person taking §220's inline choices back): a choice, a search, a
    /// colour and an icon are each a submenu of their own, however deep the rows stand — in a face's
    /// submenu, in a group's — a line of text too (§246), a number's typed value too (§521, its stepper in its row); toggles and a time's steppers stay rows.
    /// </summary>
    [Fact]
    public void AChoiceOpensALevelOfItsOwnOnEveryLevel()
    {
        OptionRow[] options =
        [
            new("on", OptionRowKind.Toggle, "Seconds") { On = true },
            new("pick", OptionRowKind.Choice, "Clock") { Choices = ["Region", "12-hour", "24-hour"], Chosen = 2 },
            new("zone", OptionRowKind.Search, "Time zone"),
            new("label", OptionRowKind.Text, "Label"),
            new("count", OptionRowKind.Number, "Years") { Value = 3, Least = 1, Most = 3 },
            new("align", OptionRowKind.Choice, "Alignment") { Choices = ["Left", "Center", "Right"], Chosen = 0 },
        ];
        List<OptionPick> picks = [];
        var rows = OptionMenu.Rows(options, picks);

        Assert.Equal(["Seconds", "Clock", "Time zone", "Label", "Years", "Alignment"], rows.Select(row => row.Text));
        Assert.DoesNotContain(rows, row => row.Separator || row.Header);
        Assert.Equal([false, true, true, true, true, true], rows.Select(row => row.Children is not null));
        Assert.Equal([false, false, true], rows[1].Children!.Select(row => row.Checked == true));
        Assert.All(rows[1].Children!, row => Assert.True(row.Stays));
        Assert.Equal(new OptionPick(rows[1].Children![1].Id, OptionPickKind.Set, Key: "pick", Value: 1), OptionMenu.PickOf(picks, rows[1].Children![1].Id));
        // A search's submenu: its field, whose results come under it (Flyout.Fields.cs).
        var zone = Assert.Single(rows[2].Children!);
        Assert.IsType<FlyoutField>(zone.Control);
        Assert.Equal(OptionPickKind.Field, OptionMenu.PickOf(picks, zone.Id)!.Kind);
        Assert.Equal(OptionPickKind.None, OptionMenu.PickOf(picks, rows[2].Id)!.Kind);
        // A line of text a level of its own too, its field alone (§246).
        Assert.Null(rows[3].Control);
        Assert.IsType<FlyoutField>(Assert.Single(rows[3].Children!).Control);
        Assert.Equal(new FlyoutControl.Toggle(true), rows[0].Control);
        // §521: a number keeps its stepper in its row, its value typed in its field a level of its own.
        Assert.IsType<FlyoutControl.Stepper>(rows[4].Control);
        Assert.IsType<FlyoutField>(Assert.Single(rows[4].Children!).Control);
        Assert.Equal(OptionPickKind.Field, OptionMenu.PickOf(picks, rows[4].Children![0].Id)!.Kind);

        // In a face's submenu the same: a third level (FaceSection).
        var folder = Folder(FolderFaces.ClockId, FolderFaces.WeatherId);
        var face = OptionMenu.FaceSection(folder.Folder!, [], Devices).First(row => row.Reorder != 0);
        var own = OptionRows.OfFace(folder.Folder!, 0);
        var levels = face.Children!.Where(row => row.Children is not null).Select(row => row.Text).ToList();
        // An alignment is its buttons in its row (§253).
        Assert.Equal(own.Where(row => !row.Segmented && row.Kind is OptionRowKind.Choice or OptionRowKind.Search or OptionRowKind.Text or OptionRowKind.Number).Select(row => row.Title), levels);
        // Under the face's name, its level's head (§230, §271), and no heading of its own.
        Assert.Equal(TheDock.TitleOf(folder.Folder!.Faces[0]), face.Text);
        Assert.DoesNotContain(face.Children!, row => row.Header);
    }

    /// <summary>
    /// A toggles row's toggles, an ordered list (§228): "Toggles shown" over them, a row for each it
    /// shows in its order — a grip at its start, dragged among the others as a folder's faces are, and
    /// a button at its end taking it out — then "Add a toggle", a submenu of the rest, each staying.
    /// A drag says where it lands (Moved), and the row moves there; a click on the row moves nothing.
    /// </summary>
    [Fact]
    public void AToggleRowsTogglesAreAListDraggedIntoOrder()
    {
        var toggles = Enum.GetValues<ListKind>().Select(kind => kind.New()).First(item => item.Info?.Options == WidgetOptionsKind.Toggles);
        toggles.Toggles = new TogglesOptions { Shown = [Toggles.All[1].Toggle, Toggles.All[0].Toggle] };
        List<OptionPick> picks = [];
        var rows = OptionMenu.Rows(OptionRows.Of(toggles), picks);

        // §411 (R10): no heading over the list.
        Assert.DoesNotContain(rows, row => row.Header);
        var listed = rows.Where(row => row.Reorder != 0).ToList();
        Assert.Equal([Toggles.All[1].Name, Toggles.All[0].Name], listed.Select(row => row.Text));
        foreach (var row in listed)
        {
            Assert.Equal(OptionMenu.ListGroup, row.Reorder);
            Assert.True(row.Stays);
            Assert.Equal(OptionPickKind.Move, OptionMenu.PickOf(picks, row.Id)!.Kind);
            var tools = Assert.IsType<FlyoutControl.Tools>(row.Control).Buttons;
            Assert.True(tools[0].Grip && tools[0].Enabled);
            Assert.Equal((S.SettingsWidgetTogglesRemove, OptionPickKind.Flip), (tools[1].Name, OptionMenu.PickOf(picks, tools[1].Id)!.Kind));
            Assert.True(Flyout.Leads(row));
        }
        var add = rows[^1];
        Assert.Equal(S.SettingsWidgetTogglesAdd, add.Text);
        Assert.Equal(Toggles.All.Skip(2).Select(toggle => toggle.Name), add.Children!.Select(row => row.Text));
        Assert.All(add.Children!, row => Assert.True(row.Stays && OptionMenu.PickOf(picks, row.Id)!.Kind == OptionPickKind.Flip));
        // §411 (R5): a pick goes back to the list, the new row lit there.
        Assert.True(add.PickOne);
        Assert.Equal(listed[0].Id, OptionMenu.ListRowOf(picks, OptionMenu.PickOf(picks, listed[0].Id)!.Key));

        // A click on a row is no move; dragged to the end, it goes there; its button takes it out.
        Assert.False(OptionMenu.Apply(toggles, OptionMenu.PickOf(picks, listed[0].Id)!));
        Assert.True(OptionMenu.Apply(toggles, OptionMenu.Moved(OptionMenu.PickOf(picks, listed[0].Id), 1)!));
        Assert.Equal([Toggles.All[0].Toggle, Toggles.All[1].Toggle], toggles.Toggles.Shown);
        var remove = Assert.IsType<FlyoutControl.Tools>(listed[1].Control).Buttons[1];
        Assert.True(OptionMenu.Apply(toggles, OptionMenu.PickOf(picks, remove.Id)!));
        Assert.Equal([Toggles.All[1].Toggle], toggles.Toggles.Shown);
        // Added from the submenu: at the end.
        Assert.True(OptionMenu.Apply(toggles, OptionMenu.PickOf(picks, add.Children![0].Id)!));
        Assert.Equal([Toggles.All[1].Toggle, Toggles.All[2].Toggle], toggles.Toggles.Shown);

        // None shown: the list says so, and every toggle is to add.
        toggles.Toggles.Shown = [];
        var empty = OptionMenu.Rows(OptionRows.Of(toggles), []);
        Assert.Equal([S.SettingsWidgetTogglesEmpty, S.SettingsWidgetTogglesAdd], empty.Select(row => row.Text));
        Assert.Equal(Toggles.All.Count(), empty[^1].Children!.Count);
    }

    /// <summary>
    /// A group's rows (§225): one row opening a submenu under the group's menu title, on every level,
    /// a choice in it a submenu again (§226). The alarm's days and the status icons are such groups;
    /// a toggles row's toggles are an ordered list (§228).
    /// </summary>
    [Fact]
    public void AGroupsRowsStandTogetherUnderItsName()
    {
        OptionRow[] options =
        [
            new("on", OptionRowKind.Toggle, "Alarm") { On = true },
            new("day.1", OptionRowKind.Toggle, "Monday") { On = true, Group = "Repeat", GroupTitle = "Repeat on" },
            new("day.2", OptionRowKind.Toggle, "Tuesday") { Group = "Repeat", GroupTitle = "Repeat on" },
            new("icon.a", OptionRowKind.Choice, "Camera") { Choices = ["Taskbar", "Hidden"], Chosen = 1, Group = "Status icons" },
            new("count", OptionRowKind.Number, "Years") { Value = 3, Least = 1, Most = 3 },
        ];
        List<OptionPick> picks = [];
        var rows = OptionMenu.Rows(options, picks);
        Assert.Equal(["Alarm", "Repeat on", "Status icons", "Years"], rows.Select(row => row.Text));
        Assert.Equal(["Monday", "Tuesday"], rows[1].Children!.Select(row => row.Text));
        Assert.Equal(new FlyoutControl.Toggle(true), rows[1].Children![0].Control);
        Assert.Equal(new OptionPick(rows[1].Children![1].Id, OptionPickKind.Flip, Key: "day.2"), OptionMenu.PickOf(picks, rows[1].Children![1].Id));
        // A choice in the group's submenu opens a level of its own: a third.
        var camera = Assert.Single(rows[2].Children!);
        Assert.Equal("Camera", camera.Text);
        Assert.Equal(["Taskbar", "Hidden"], camera.Children!.Select(row => row.Text));
        Assert.Equal([false, true], camera.Children!.Select(row => row.Checked == true));
        Assert.Equal(OptionPickKind.None, OptionMenu.PickOf(picks, rows[1].Id)!.Kind);

        // The widgets' own: the toggles shown, the days an alarm repeats on, the status icons.
        WidgetItem Of(WidgetOptionsKind kind) =>
            Enum.GetValues<BarKind>().Select(each => (WidgetItem)each.New()).Concat(Enum.GetValues<ListKind>().Select(each => each.New()))
                .First(item => item.Info?.Options == kind);
        var alarm = OptionMenu.Rows(OptionRows.Of(Of(WidgetOptionsKind.Alarm)), []);
        Assert.Equal(7, alarm.Single(row => row.Text == S.DockEditRepeatOn).Children!.Count);
        var icons = OptionMenu.Rows(OptionRows.Of(Of(WidgetOptionsKind.StatusIcons)), []);
        var each = icons.Single(row => row.Text == S.SettingsStatusIconsHeader).Children!;
        Assert.Equal(StatusIcons.All.Count, each.Count);
        Assert.All(each, row => Assert.NotNull(row.Children));
    }

    /// <summary>A face's options by the kinds its descriptor gives them, as the folder page draws them.</summary>
    [Fact]
    public void AFacesOptionsAreRowsByTheirKinds()
    {
        var clock = OptionRows.OfFace(Folder(FolderFaces.ClockId).Folder!, 0);
        // In the menus' one order (§292): its zone, its look, its label.
        Assert.Equal(
            [("zone", OptionRowKind.Search), ("seconds", OptionRowKind.Toggle), ("hours", OptionRowKind.Choice), ("timer", OptionRowKind.Toggle),
             ("label", OptionRowKind.Text)],
            clock.Select(row => (row.Key, row.Kind)));

        // A reading's pace from a second to a minute, its adapter what this PC has; not listed, no row.
        // Its style between them, the figure or its graph (§295).
        var gpu = OptionRows.OfFace(Folder(FolderFaces.GpuId).Folder!, 0, Devices);
        Assert.Equal([("adapter", OptionRowKind.Choice), ("style", OptionRowKind.Choice), ("pace", OptionRowKind.Choice)], gpu.Select(row => (row.Key, row.Kind)));
        Assert.Equal([PerformanceChoices.Main, .. Devices.Adapters!], gpu[0].Choices);
        Assert.Equal(PerformanceChoices.Paces.Select(PerformanceChoices.Pace), gpu[2].Choices);
        Assert.DoesNotContain(OptionRows.OfFace(Folder(FolderFaces.GpuId).Folder!, 0), row => row.Key == "adapter");

        // The markets face's search first, then the currency it's shown in (§494), then its range, its style, its chart and its name — no holding,
        // which a face never says (§246, §258, §292); its years a number while the range is so many years.
        var folder = Folder(FolderFaces.MarketsId);
        var stock = OptionRows.OfFace(folder.Folder!, 0);
        Assert.Equal(
            [(MarketRows.Symbol, OptionRowKind.Search), (MarketRows.Currency, OptionRowKind.Search), (MarketRows.Range, OptionRowKind.Choice), (MarketRows.Style, OptionRowKind.Choice),
             (MarketRows.Chart, OptionRowKind.Choice), (MarketRows.Label, OptionRowKind.Text)],
            stock.Select(row => (row.Key, row.Kind)));
        MarketFaces.Kept(folder.Folder!.Faces[0]).Range = MarketRange.Years;
        var years = OptionRows.OfFace(folder.Folder!, 0).Single(row => row.Key == MarketRows.Years);
        Assert.Equal((OptionRowKind.Number, 3, MarketOptions.LeastYears, MarketOptions.MostYears), (years.Kind, years.Value, years.Least, years.Most));

        // §364: the icon face's rows are its icon alone (FolderFacesTests.AFolderHasNoName).
        Assert.Equal([OptionPickers.FolderIcon], OptionRows.OfFace(Folder(FolderFaces.IconId).Folder!, 0).Select(row => row.Key));
        // An option shown only while another has a value: the calendar's form while it is words. Its
        // zone always (§230), as the time-and-date face's.
        var calendar = Folder(FolderFaces.CalendarId);
        // §351: its links (the accounts, the holidays) aside.
        Assert.Equal([CalendarFace.Zone, CalendarFace.Look, CalendarFace.Form], OptionRows.OfFace(calendar.Folder!, 0).Where(row => row.Kind != OptionRowKind.Link).Select(row => row.Key));
        calendar.Folder!.Faces[0].Set(FolderFaces.Calendar.Option(CalendarFace.Look)!, CalendarFace.Page);
        Assert.Equal([CalendarFace.Zone, CalendarFace.Look], OptionRows.OfFace(calendar.Folder!, 0).Where(row => row.Kind != OptionRowKind.Link).Select(row => row.Key));
        Assert.Contains(OptionRows.OfFace(Folder(FolderFaces.DateTimeId).Folder!, 0), row => row.Key == DateTimeFace.Zone && row.Kind == OptionRowKind.Search);
    }

    // --- The same write as Settings ------------------------------------------------------------------

    /// <summary>
    /// Every face option a menu sets, set from the menu and from the page's control (FolderPage's
    /// OptionControl and FaceRowCard, through FolderRows): the same folder, byte for byte.
    /// </summary>
    [Fact]
    public void AFacesSettingFromTheMenuWritesWhatTheSettingsPageWrites()
    {
        void Same(string[] faces, OptionPickKind kind, int face, string key, int value, Action<FolderOptions> page)
        {
            var menu = Folder(faces);
            var settings = Copy(menu);
            Assert.True(OptionMenu.Apply(menu, Pick(FacePicks(menu), kind, face, key, value), Devices), $"{key} changed nothing");
            page(settings.Folder!);
            Assert.Equal(Json(settings), Json(menu));
        }
        var clock = FolderFaces.Clock;
        Same([FolderFaces.ClockId], OptionPickKind.Flip, 0, ClockFace.Seconds, 0, folder => folder.Faces[0].Set(clock.Option(ClockFace.Seconds)!, true));
        Same([FolderFaces.ClockId], OptionPickKind.Set, 0, ClockFace.Hours, 2, folder => folder.Faces[0].Set(clock.Option(ClockFace.Hours)!, clock.Option(ClockFace.Hours)!.Choices[2]));
        Same([FolderFaces.WeatherId], OptionPickKind.Flip, 0, "device", 0, folder => FaceRows.UseDevice(folder.Faces[0], !FaceRows.UsesDevice(folder.Faces[0])));
        Same([FolderFaces.CpuId], OptionPickKind.Set, 0, PerformanceFace.Pace, 4, folder => folder.Faces[0].Set(FolderFaces.Cpu.Option(PerformanceFace.Pace)!, PerformanceChoices.FacePace(4)));
        Same([FolderFaces.GpuId], OptionPickKind.Set, 0, PerformanceFace.Adapter, 2, folder =>
        {
            var option = FolderFaces.Gpu.Option(PerformanceFace.Adapter)!;
            var choices = PerformanceChoices.FaceDevices(option.Kind, Devices.Adapters!, folder.Faces[0].Text(option));
            folder.Faces[0].Set(option, PerformanceChoices.FaceDevicePicked(option.Kind, choices, 2));
        });
        Same([FolderFaces.DiskId], OptionPickKind.Set, 0, PerformanceFace.Disk, 2, folder =>
        {
            var option = FolderFaces.Disk.Option(PerformanceFace.Disk)!;
            var choices = PerformanceChoices.FaceDevices(option.Kind, Devices.Disks!, folder.Faces[0].Text(option));
            folder.Faces[0].Set(option, PerformanceChoices.FaceDevicePicked(option.Kind, choices, 2));
        });
        Same([FolderFaces.MarketsId], OptionPickKind.Set, 0, MarketRows.Range, 1, folder => MarketFaces.Kept(folder.Faces[0]).Range = MarketRanges.All[1]);
        // §364, §385: where it stands, the folder's own row over its faces (no face's).
        var aligned = Folder(FolderFaces.IconId);
        var paged = Copy(aligned);
        paged.Folder!.Align = FolderAligns.All[2];
        List<OptionPick> own = [];
        _ = OptionMenu.Section(OptionRows.Of(aligned), own);
        Assert.True(OptionMenu.Apply(aligned, Pick(own, OptionPickKind.Set, -1, OptionRows.FolderAlign, 2)));
        Assert.Equal(Json(paged), Json(aligned));

        // The markets face's years, stepped as a number is.
        var stepped = Folder(FolderFaces.MarketsId);
        MarketFaces.Kept(stepped.Folder!.Faces[0]).Range = MarketRange.Years;
        var page = Copy(stepped);
        Assert.True(OptionMenu.Apply(stepped, Pick(FacePicks(stepped), OptionPickKind.Step, 0, MarketRows.Years, 1)));
        MarketFaces.Kept(page.Folder!.Faces[0]).RangeYears += 1;
        Assert.Equal(Json(page), Json(stepped));
    }

    /// <summary>
    /// A performance graph's settings from its menu, and from its page's controls (IslandOptions:
    /// ReadingIndex, StyleIndex, PerformancePace, PerformanceAdapterIndex, PerformanceDiskIndex):
    /// the same line, byte for byte.
    /// </summary>
    [Fact]
    public void AGraphsSettingFromTheMenuWritesWhatTheSettingsPageWrites()
    {
        void Same(ListKind form, OptionPickKind kind, string key, int value, Action<CellItem> page)
        {
            var menu = form.New();
            var settings = Copy(menu);
            List<OptionPick> picks = [];
            _ = OptionMenu.Section(OptionRows.Of(menu, Devices), picks);
            Assert.True(OptionMenu.Apply(menu, Pick(picks, kind, key: key, value: value), Devices), $"{key} changed nothing");
            page(settings);
            Assert.Equal(Json(settings), Json(menu));
        }
        // The style: the graph, the figure alone (the graph drawing none) (§228).
        Same(ListKind.Cpu, OptionPickKind.Set, OptionRows.Style, 1, line => line.Performance!.ShowGraph = false);
        Same(ListKind.Cpu, OptionPickKind.Set, OptionRows.PerformancePace, 4, line => line.Performance!.Seconds = PerformanceChoices.Paces[4]);
        // The reading, the widget kept: the graphics' details become the disk's (§318).
        Same(ListKind.GpuDetails, OptionPickKind.Set, OptionRows.Reading, 3, line => WidgetStyles.Restyle(line, WidgetStyles.PerformanceDetails.StyleOf(WidgetStyles.DetailsWord, "disk")));
        Same(ListKind.Gpu, OptionPickKind.Set, OptionRows.PerformanceAdapter, 2,
            line => line.Performance!.GpuAdapter = PerformanceChoices.Picked(PerformanceChoices.AdapterChoices(Devices.Adapters!, line.Performance.GpuAdapter), 2));
        Same(ListKind.Disk, OptionPickKind.Set, OptionRows.PerformanceDisk, 1,
            line => line.Performance!.Disk = PerformanceChoices.PickedDisk(PerformanceChoices.DiskChoices(Devices.Disks!, line.Performance.Disk), 1));

        // The pace it has: nothing; the figure alone and the graph again: back as it was.
        var cpu = ListKind.Cpu.New();
        var before = Json(cpu);
        List<OptionPick> again = [];
        _ = OptionMenu.Section(OptionRows.Of(cpu), again);
        Assert.False(OptionMenu.Apply(cpu, Pick(again, OptionPickKind.Set, key: OptionRows.PerformancePace, value: 0)));
        Assert.True(OptionMenu.Apply(cpu, Pick(again, OptionPickKind.Set, key: OptionRows.Style, value: 1)));
        Assert.False(cpu.Performance!.ShowGraph);
        Assert.True(OptionMenu.Apply(cpu, Pick(again, OptionPickKind.Set, key: OptionRows.Style, value: 0)));
        Assert.Equal(before, Json(cpu));
    }

    // --- The faces -----------------------------------------------------------------------------------

    /// <summary>
    /// A face added from the gallery's groups, after the others; one the button wears already, of a kind
    /// that does not repeat, greyed; all of them greyed, and "Add a face" too, once the button is full.
    /// </summary>
    [Fact]
    public void AFaceIsAddedFromTheGallerysGroups()
    {
        var folder = Folder(FolderFaces.ClockId, FolderFaces.WeatherId);
        List<OptionPick> picks = [];
        var rows = OptionMenu.FaceSection(folder.Folder!, picks, Devices);
        // No heading of their own (§288), "Add a face" right under them, no rule between (§314).
        Assert.Equal(["Clock", "Weather", S.SettingsFolderFacesAddHeader], rows.Select(row => row.Text));

        // The list's last row, not the menu's foot (§314; §280 to §313 it was).
        var add = rows[^1];
        Assert.False(add.Foot);
        Assert.True(add.Enabled);
        // §411: a Find field over every face, the groups ruled off; a pick goes back to the faces.
        Assert.True(add.PickOne);
        Assert.Equal(new FlyoutField("", S.SettingsFolderFacesSearch, Filters: true, NoMatch: S.DockMenuNoMatch), add.Children![0].Control);
        Assert.Equal(FaceGallery.Search(folder.Folder!, null, null).Count() - 1, add.Children!.Count(item => item.Separator));
        var faces = add.Children!.Skip(1).Where(item => !item.Separator).ToList();
        Assert.Equal(FolderFaces.All.Count, faces.Count);
        // The weather does not repeat and is worn: greyed. A second clock may be added.
        Assert.False(faces.Single(face => face.Text == FolderFaces.Weather.Title).Enabled);
        var second = faces.Single(face => face.Text == FolderFaces.Clock.Title);
        Assert.True(second.Enabled);
        var pick = OptionMenu.PickOf(picks, second.Id)!;
        Assert.Equal((OptionPickKind.AddFace, FolderFaces.ClockId), (pick.Kind, pick.FaceKind));

        var page = Copy(folder);
        Assert.True(OptionMenu.Apply(folder, pick));
        Assert.True(FaceRows.AddFace(page.Folder!, FolderFaces.Clock));
        Assert.Equal(Json(page), Json(folder));
        Assert.Equal([FolderFaces.ClockId, FolderFaces.WeatherId, FolderFaces.ClockId], folder.Folder!.Faces.Select(face => face.Kind));
        Assert.False(OptionMenu.Apply(folder, OptionMenu.PickOf(picks, faces.Single(face => face.Text == FolderFaces.Weather.Title).Id)!));

        // No most (§259): a button of twelve faces, three of them icons, is offered every face still
        // that it may wear again, the icon among them.
        var many = Folder([.. Enumerable.Repeat(FolderFaces.ClockId, 9), FolderFaces.IconId, FolderFaces.IconId, FolderFaces.IconId]);
        var manyAdd = OptionMenu.FaceSection(many.Folder!, [], Devices)[^1];
        Assert.True(manyAdd.Enabled);
        Assert.True(manyAdd.Children!.Single(face => face.Text == FolderFaces.Icon.Title).Enabled);
    }

    /// <summary>
    /// A face's row on the menu's first level: dragged into order among the faces' rows (one
    /// <see cref="FlyoutItem.Reorder"/> group), its grip at its start (§226), moved there as the
    /// page's list moved it (FaceRows.MoveFace); its bin at its end before the chevron (§240, the
    /// person: "move the delete button into the root view again"), Delete its shortcut, takes it off as
    /// the page's Remove does, never the last; no "Remove" in its submenu. No "Move earlier" or "Move
    /// later" anywhere.
    /// </summary>
    [Fact]
    public void AFaceIsMovedAndTakenOffAsThePagesListDoes()
    {
        string[] three = [FolderFaces.ClockId, FolderFaces.WeatherId, FolderFaces.BatteryId];
        var folder = Folder(three);
        List<OptionPick> picks = [];
        var rows = OptionMenu.FaceSection(folder.Folder!, picks, Devices);
        var faces = rows.Where(row => row.Reorder != 0).ToList();
        Assert.Equal(["Clock", "Weather", FolderFaces.Battery.Title], faces.Select(row => row.Text));
        Assert.All(faces, row => Assert.Equal(OptionMenu.FaceGroup, row.Reorder));
        foreach (var (row, at) in faces.Select((row, at) => (row, at)))
        {
            var tools = Assert.IsType<FlyoutControl.Tools>(row.Control).Buttons;
            Assert.Equal(2, tools.Count);
            var (grip, remove) = (tools[0], tools[1]);
            Assert.True(grip.Grip && grip.Enabled);
            Assert.Equal(S.DockEditFaceDrag, grip.Name);
            Assert.True(Flyout.Leads(row));
            Assert.True(Flyout.AtEnd(row.Control!));
            Assert.Equal((S.DockMenuRemove, MenuChord.Delete, true, false), (remove.Name, remove.Keys, remove.Enabled, remove.Grip));
            // The battery has nothing to set: no submenu at all.
            Assert.DoesNotContain(row.Children ?? [], child => child.Text == S.DockMenuRemove || child.Separator);
            Assert.Equal(new OptionPick(remove.Id, OptionPickKind.RemoveFace, at, three[at]), OptionMenu.PickOf(picks, remove.Id));
            Assert.Equal(new OptionPick(row.Id, OptionPickKind.MoveFace, at, three[at]), OptionMenu.PickOf(picks, row.Id));
            Assert.Equal(row.Id, OptionMenu.FaceRowOf(picks, at));
            Assert.DoesNotContain(row.Children ?? [], child => child.Text is "Move earlier" or "Move later");
        }

        // Dragged from 1 to 0.
        var page = Copy(folder);
        Assert.True(OptionMenu.Apply(folder, OptionMenu.Moved(OptionMenu.PickOf(picks, faces[1].Id), 0)!));
        Assert.True(FaceRows.MoveFace(page.Folder!, 1, 0));
        Assert.Equal(Json(page), Json(folder));
        Assert.Equal([FolderFaces.WeatherId, FolderFaces.ClockId, FolderFaces.BatteryId], folder.Folder!.Faces.Select(face => face.Kind));

        // The menu as it was shown: the face at 1 is the clock now, so its picks do nothing.
        Assert.False(OptionMenu.Apply(folder, OptionMenu.Moved(OptionMenu.PickOf(picks, faces[1].Id), 2)!));
        Assert.False(OptionMenu.Apply(folder, Pick(picks, OptionPickKind.RemoveFace, 1)));
        // Only a face's row moves; nowhere is no move.
        Assert.Null(OptionMenu.Moved(OptionMenu.PickOf(picks, rows[^1].Id), 0));
        Assert.Null(OptionMenu.Moved(null, 0));

        // Dragged from 0 to the end.
        var moved = Folder(three);
        var movedPage = Copy(moved);
        var movedPicks = FacePicks(moved);
        Assert.True(OptionMenu.Apply(moved, OptionMenu.Moved(OptionMenu.PickOf(movedPicks, OptionMenu.FaceRowOf(movedPicks, 0)!.Value), 2)!));
        Assert.True(FaceRows.MoveFace(movedPage.Folder!, 0, 2));
        Assert.Equal(Json(movedPage), Json(moved));
        Assert.False(OptionMenu.Apply(moved, OptionMenu.Moved(OptionMenu.PickOf(FacePicks(moved), OptionMenu.FaceRowOf(FacePicks(moved), 2)!.Value), 3)!));

        var removed = Folder(three);
        var removedPage = Copy(removed);
        Assert.True(OptionMenu.Apply(removed, Pick(FacePicks(removed), OptionPickKind.RemoveFace, 2)));
        Assert.True(FaceRows.RemoveFace(removedPage.Folder!, 2));
        Assert.Equal(Json(removedPage), Json(removed));

        // One face: nothing to drag it among, and never taken off: its grip and its bin greyed.
        var one = Folder(FolderFaces.ClockId);
        var only = OptionMenu.FaceSection(one.Folder!, [], Devices).Single(row => row.Text == "Clock");
        Assert.All(Assert.IsType<FlyoutControl.Tools>(only.Control).Buttons, tool => Assert.False(tool.Enabled));
        Assert.False(OptionMenu.Apply(one, Pick(FacePicks(one), OptionPickKind.RemoveFace, 0)));
    }

    /// <summary>Where a dragged row lands for a drop before a place among its own, its own place counted (Flyout.Reorder.cs).</summary>
    [Fact]
    public void ARowDraggedLandsWhereTheGapIs()
    {
        Assert.Equal(0, Flyout.ReorderPlace(held: 2, target: 0));
        Assert.Equal(2, Flyout.ReorderPlace(held: 0, target: 3));
        Assert.Equal(1, Flyout.ReorderPlace(held: 1, target: 1));
        Assert.Equal(1, Flyout.ReorderPlace(held: 1, target: 2));
        Assert.Equal(1, Flyout.ReorderPlace(held: 1, target: -1));
    }

    /// <summary>The menu shown again after a change: these rows as they are now, the rows around them as they were.</summary>
    [Fact]
    public void TheMenuIsShownAgainWithTheRowsAsTheyAreNow()
    {
        var folder = Folder(FolderFaces.ClockId);
        List<OptionPick> picks = [];
        List<FlyoutItem> items = [new(1, "Copy", Primary: true), new(2, "Open folder"), .. OptionMenu.FaceSection(folder.Folder!, picks)];
        Assert.True(OptionMenu.Apply(folder, Pick(picks, OptionPickKind.Flip, 0, ClockFace.Seconds)));
        var now = OptionMenu.Updated(items, OptionMenu.FaceSection(folder.Folder!, []));
        Assert.Equal(items.Select(item => (item.Id, item.Text)), now.Select(item => (item.Id, item.Text)));
        var seconds = now.Single(row => row.Text == "Clock").Children!.First(child => child.Control is FlyoutControl.Toggle);
        Assert.Equal(new FlyoutControl.Toggle(true), seconds.Control);
        Assert.Equal(items, OptionMenu.Updated(items.Take(2).ToList(), []).Concat(items.Skip(2)));
    }

    // --- Nothing left out --------------------------------------------------------------------------

    /// <summary>
    /// A performance graph's menu has every card its page in Settings had (gone since §228: the reading
    /// and style cards, and the performance cards' adapter, disk and pace), and no other: each style's rows
    /// are the cards that page showed for it. Its size is the height rows'.
    /// </summary>
    [Fact]
    public void AGraphsMenuHasEveryCardItsPageHas()
    {
        string[] cards = [OptionRows.Reading, OptionRows.Style, OptionRows.PerformanceAdapter, OptionRows.PerformanceDisk, OptionRows.PerformancePace];

        var forms = WidgetStyles.Performance.Styles.Concat(WidgetStyles.PerformanceDetails.Styles).Select(style => style.Form).ToList();
        var offered = forms.SelectMany(form => OptionRows.Of(form.New(), Devices).Select(row => row.Key)).ToHashSet();
        Assert.Equal(cards.Order(), offered.Order());

        foreach (var form in forms)
        {
            var line = form.New();
            var info = line.Info!;
            var keys = OptionRows.Of(line, Devices).Select(row => row.Key).ToList();
            // What it measures, its adapter or disk, its style (§292's order).
            List<string> expected = [OptionRows.Reading];
            if (info.Metric == Metric.Gpu)
            {
                expected.Add(OptionRows.PerformanceAdapter);
            }
            if (info.Metric == Metric.Disk)
            {
                expected.Add(OptionRows.PerformanceDisk);
            }
            // The details have their reading and no style (§318).
            if (info.Facts != true)
            {
                expected.Add(OptionRows.Style);
            }
            // The pace in every style (§228).
            expected.Add(OptionRows.PerformancePace);
            if (info.Facts != true)
            {
                // A graph's size, Compact or Full (§603), and no height rows.
                Assert.True(form.Chooses());
                Assert.Null(LineHeightRows.For(line));
            }
            Assert.Equal(expected, keys);
            // Every one of them a row of the menu, none of them sent to the page while the devices are listed.
            List<OptionPick> picks = [];
            var rows = OptionMenu.Section(OptionRows.Of(line, Devices), picks);
            Assert.Equal(keys.Count, rows.Count - 1);
        }
    }

    /// <summary>Every face's rows in its menu are its section's rows on the folder page, alone on its button and among others.</summary>
    [Fact]
    public void AFacesMenuHasEveryRowItsSectionHas()
    {
        foreach (var face in FolderFaces.All)
        {
            foreach (var faces in new[] { new[] { face.Id }, [face.Id, FolderFaces.WifiId] })
            {
                var folder = Folder(faces);
                var worn = folder.Folder!.Faces[0];
                // The markets face's own record is its type and that type's rows, several for its one (§246).
                var section = FaceRows.Rows(face).Where(row => FaceRows.Shows(row, worn) && row.Kind != FaceRowKind.Market).ToList();
                // §351: its links (the accounts, the holidays) no rows of its section.
                var rows = OptionRows.OfFace(folder.Folder!, 0, Devices).Where(row => !row.Key.StartsWith("Market", StringComparison.Ordinal) && row.Kind != OptionRowKind.Link).ToList();
                // Each once; their order is the menus' one (§292, MenuConventionTests).
                Assert.Equal(
                    section.Select(row => row.Option?.Key ?? char.ToLowerInvariant(row.Kind.ToString()[0]) + row.Kind.ToString()[1..]).Order(StringComparer.Ordinal),
                    rows.Select(row => row.Key).Order(StringComparer.Ordinal));
            }
        }
    }

    // --- On a real menu ------------------------------------------------------------------------------

    /// <summary>The processor graph's menu in edit mode, as the dock builds it: its buttons, its settings and its height (§253).</summary>
    private static List<FlyoutItem> GraphMenu(CellItem line, List<OptionPick> picks)
    {
        List<FlyoutItem> rows = [.. TheDock.WidgetRowsFor(line), .. OptionMenu.Section(OptionRows.Of(line), picks)];
        return LineHeightRows.For(line) is { } height ? [.. rows, .. OptionMenu.Foot([.. LineHeightRows.Rows(height)])] : rows;
    }

    /// <summary>A folder's menu on the arranged bar, as the dock builds it: its buttons (no "Settings") and its faces.</summary>
    private static List<FlyoutItem> FolderMenu(BarItem folder, List<OptionPick> picks) =>
        [.. TheDock.WidgetRowsFor(folder), .. OptionMenu.FaceSection(folder.Folder!, picks, Devices)];

    /// <summary>A point of the menu's window as a mouse message's lParam.</summary>
    private static LPARAM At(POINT point) => (LPARAM)(nint)((point.y << 16) | (point.x & 0xFFFF));

    /// <summary>A click (down and up) at <paramref name="point"/>, after the pointer came there; what the release said.</summary>
    private static FlyoutEvent Click(Flyout menu, POINT point)
    {
        _ = menu.Message(WM.WM_MOUSEMOVE, 0, At(point));
        _ = menu.Message(WM.WM_LBUTTONDOWN, 1, At(point));
        return menu.Message(WM.WM_LBUTTONUP, 0, At(point));
    }

    /// <summary>
    /// The faces' rows on a real menu (§220, §226): a grip at a row's start, its tooltip how to drag;
    /// a click on the row, the grip too, opens its settings, "Remove" their last row, which acts and the
    /// menu stays; a row dragged by the mouse past another lands there, the menu staying; Escape puts a
    /// drag back; Ctrl+Up and Ctrl+Down do it from the keys, Delete in its submenu, and the menu shown
    /// again lights the face where it went. Pictures in KARPO_PNG_DIR: option-menu-drag-&lt;look&gt;.png
    /// (a row being dragged) and option-menu-cpu-&lt;look&gt;.png (the processor face's settings), at 150 %.
    /// </summary>
    [UiFact]
    public void TheFacesAreMovedAndTakenOffOnTheMenusFirstLevel()
    {
        WindowShots.OnHiddenDesktop("option-menu-rows", (gfx, instance) =>
        {
            const float scale = 1.5f;
            try
            {
                foreach (var dark in new[] { true, false })
                {
                    var tone = dark ? "dark" : "light";
                    var look = AddListPictures.LookFor(dark);
                    var work = new RECT { right = (int)(1920 * scale), bottom = (int)(1040 * scale) };
                    var anchor = new POINT { x = work.right / 3, y = work.bottom };
                    using var menu = new Flyout(gfx, instance, 0) { Configures = true };
                    var folder = Folder(FolderFaces.ClockId, FolderFaces.WeatherId, FolderFaces.CpuId);
                    List<OptionPick> picks = [];
                    var items = FolderMenu(folder, picks);
                    menu.Show(gfx, look, scale, items, FlyoutLayout.List, anchor, work, Edge.Bottom, null);
                    WindowShots.Settle();
                    var faces = items.Where(item => item.Reorder != 0).ToList();
                    Assert.Equal(3, faces.Count);

                    // The row on top: Cut, Copy and Remove together against its far end (§350).
                    var panel = menu.OpenPanels[0].Panel;
                    var buttons = items.Where(item => item.Primary).Select(item => menu.ItemOnScreen(item.Id)!.Value).ToList();
                    Assert.Equal(3, buttons.Count);
                    Assert.True(panel.right - buttons[2].right <= (int)MathF.Ceiling(8 * scale), $"{buttons[2].right} {panel.right}");
                    Assert.Equal(buttons[1].left - buttons[0].right, buttons[2].left - buttons[1].right);

                    // The weather's grip at its row's start, before its icon and its words (§226); its
                    // tooltip says how to drag; it is no button: a click on it is the row's, its level in place.
                    var look0 = menu.Looks.Single(row => row.Id == faces[1].Id);
                    var gripAt = menu.ToolMiddle(faces[1].Id, 0)!.Value;
                    var middle = menu.RowMiddle(faces[1].Id)!.Value;
                    var gripX = gripAt.x - (middle.x - (look0.Plate.X + (look0.Plate.W / 2)));
                    Assert.InRange(gripX, look0.Plate.X, look0.Icon.X - 1);
                    // Its bin at its end, past its words and before the chevron (§240).
                    var binAt = menu.ToolMiddle(faces[1].Id, 1)!.Value;
                    var binX = binAt.x - (middle.x - (look0.Plate.X + (look0.Plate.W / 2)));
                    Assert.InRange(binX, look0.Words.X + look0.Words.W, look0.Plate.X + look0.Plate.W - (16 * scale));
                    Assert.Null(menu.ToolMiddle(faces[1].Id, 2));
                    // Its icon and its words a column further in than another row's.
                    var adding = menu.Looks.Single(row => row.Id == items[^1].Id);
                    Assert.True(look0.Icon.X > adding.Icon.X && look0.Words.X > adding.Words.X);
                    _ = menu.Message(WM.WM_MOUSEMOVE, 0, At(gripAt));
                    menu.TipDue();
                    Assert.Equal(S.DockEditFaceDrag, menu.TipShown);
                    _ = Click(menu, gripAt);
                    Assert.Equal(1, menu.InlineDepth);
                    _ = menu.Message(WM.WM_KEYDOWN, VK.VK_ESCAPE, 0);
                    Assert.Equal(0, menu.InlineDepth);
                    // Its bin takes it off: the menu stays, as for every change.
                    var remove = ((FlyoutControl.Tools)faces[1].Control!).Buttons[1];
                    Assert.Equal(new FlyoutEvent(FlyoutEventKind.Adjust, remove.Id), Click(menu, binAt));
                    Assert.True(menu.IsOpen);

                    // The CPU's row elsewhere: its settings, every choice a level of its own.
                    Assert.Equal(default, Click(menu, menu.RowMiddle(faces[2].Id)!.Value));
                    WindowShots.Settle();
                    Assert.Equal(1, menu.InlineDepth);
                    Assert.True(Save(menu, $"option-menu-cpu-{tone}.png", dark));
                    // Its first choice, a level of its own (§226): a third, in place (§271).
                    var choice = faces[2].Children!.First(row => row.Children is not null);
                    Assert.Equal(default, menu.Click(choice.Id));
                    WindowShots.Settle();
                    Assert.Equal(2, menu.InlineDepth);
                    Assert.True(Save(menu, $"option-menu-cpu-choice-{tone}.png", dark));
                    _ = menu.Click(Flyout.InlineBackId);
                    Assert.Equal(1, menu.InlineDepth);
                    _ = menu.Click(Flyout.InlineBackId);
                    Assert.Equal(0, menu.InlineDepth);

                    // The clock's row dragged down past the CPU's: it lands last; the menu stays.
                    var start = menu.RowMiddle(faces[0].Id)!.Value;
                    var past = menu.RowMiddle(faces[2].Id)!.Value;
                    _ = menu.Message(WM.WM_MOUSEMOVE, 0, At(start));
                    _ = menu.Message(WM.WM_LBUTTONDOWN, 1, At(start));
                    _ = menu.Message(WM.WM_MOUSEMOVE, 0, At(start with { y = start.y + 12 }));
                    _ = menu.Message(WM.WM_MOUSEMOVE, 0, At(past with { y = past.y + 4 }));
                    WindowShots.Settle();
                    Assert.Equal(0, menu.InlineDepth);
                    Assert.True(Save(menu, $"option-menu-drag-{tone}.png", dark));
                    Assert.Equal(new FlyoutEvent(FlyoutEventKind.Reorder, faces[0].Id, To: 2), menu.Message(WM.WM_LBUTTONUP, 0, At(past with { y = past.y + 4 })));
                    Assert.True(menu.IsOpen);

                    // Escape puts a drag back: nothing moves.
                    _ = menu.Message(WM.WM_LBUTTONDOWN, 1, At(start));
                    _ = menu.Message(WM.WM_MOUSEMOVE, 0, At(past));
                    Assert.Equal(default, menu.Message(WM.WM_KEYDOWN, VK.VK_ESCAPE, 0));
                    Assert.True(menu.IsOpen);
                    Assert.NotEqual(FlyoutEventKind.Reorder, menu.Message(WM.WM_LBUTTONUP, 0, At(past)).Kind);

                    // The keys: Ctrl+Up and Ctrl+Down on the weather's row, Delete; Ctrl+Up on the first is nothing.
                    menu.Hide();
                    menu.Show(gfx, look, scale, items, FlyoutLayout.List, anchor, work, Edge.Bottom, null);
                    WindowShots.Settle();
                    KeysTo(menu, faces[1].Id);
                    KeyState.Held = key => key == VK.VK_CONTROL;
                    Assert.Equal(new FlyoutEvent(FlyoutEventKind.Reorder, faces[1].Id, To: 0), menu.Message(WM.WM_KEYDOWN, VK.VK_UP, 0));
                    Assert.Equal(new FlyoutEvent(FlyoutEventKind.Reorder, faces[1].Id, To: 2), menu.Message(WM.WM_KEYDOWN, VK.VK_DOWN, 0));
                    KeyState.Held = _ => false;
                    // Delete on its row is its bin's (§240).
                    Assert.Equal(new FlyoutEvent(FlyoutEventKind.Adjust, remove.Id), menu.Message(WM.WM_KEYDOWN, VK.VK_DELETE, 0));
                    Assert.True(menu.IsOpen);

                    // Moved up as the dock moves it: shown again, the weather's row lit where it went.
                    Assert.True(OptionMenu.Apply(folder, OptionMenu.Moved(OptionMenu.PickOf(picks, faces[1].Id), 0)!));
                    List<OptionPick> again = [];
                    menu.Refresh(OptionMenu.Updated(menu.ShownItems, OptionMenu.FaceSection(folder.Folder!, again, Devices)), OptionMenu.FaceRowOf(again, 0));
                    Assert.Equal(OptionMenu.FaceRowOf(again, 0), menu.KeysOn);
                    Assert.Equal("Weather", menu.ShownItems.Single(item => item.Id == menu.KeysOn).Text);
                    KeyState.Held = key => key == VK.VK_CONTROL;
                    Assert.Equal(new FlyoutEvent(FlyoutEventKind.Nothing), menu.Message(WM.WM_KEYDOWN, VK.VK_UP, 0));
                    KeyState.Held = _ => false;
                    menu.Hide();

                    // A face with nothing to set: no submenu, no chevron (§253); a click or Enter on
                    // its row does nothing, the menu staying.
                    var plain = Folder(FolderFaces.ClockId, FolderFaces.WifiId);
                    var plainItems = FolderMenu(plain, []);
                    var content = plainItems.Where(item => item.Reorder != 0).Last();
                    Assert.Null(content.Children);
                    menu.Show(gfx, look, scale, plainItems, FlyoutLayout.List, anchor, work, Edge.Bottom, null);
                    WindowShots.Settle();
                    // Its bin in line with the clock's, the chevron's slot kept empty.
                    var clock = plainItems.First(item => item.Reorder != 0);
                    Assert.Equal(menu.ToolMiddle(clock.Id, 1)!.Value.x, menu.ToolMiddle(content.Id, 1)!.Value.x);
                    Assert.Equal(FlyoutEventKind.Nothing, Click(menu, menu.RowMiddle(content.Id)!.Value).Kind);
                    KeysTo(menu, content.Id);
                    Assert.Equal(FlyoutEventKind.Nothing, menu.Message(WM.WM_KEYDOWN, VK.VK_RETURN, 0).Kind);
                    WindowShots.Settle();
                    Assert.True(menu.IsOpen);
                    Assert.Equal(0, menu.InlineDepth);
                    Assert.True(Save(menu, $"option-menu-empty-{tone}.png", dark));
                    menu.Hide();
                }
            }
            finally
            {
                KeyState.Held = null;
            }
        });
    }

    /// <summary>
    /// §240: "Use my device's location" is one control, the switch at the top of the place's level —
    /// no row of the widget's own, no result of the search —; on, the field greyed and the place's row
    /// saying "Device location"; flipped by its pick, it writes as the switch always did, and a place
    /// picked turns it off.
    /// </summary>
    [Fact]
    public void TheDevicesLocationIsTheSwitchOverThePlacesSearch()
    {
        foreach (var on in new[] { true, false })
        {
            var weather = ListKind.WeatherNow.New();
            weather.Weather = new WeatherOptions { UseDeviceLocation = on, Place = "Bern", Latitude = 46.95, Longitude = 7.45 };
            List<OptionPick> picks = [];
            var rows = OptionMenu.Rows(OptionRows.Of(weather), picks);
            Assert.DoesNotContain(rows, row => row.Text == S.SettingsWidgetWeatherDeviceHeader);
            var place = rows.Single(row => row.Text == S.SettingsWidgetWeatherPlaceHeader);
            Assert.Equal(on ? S.DockEditDeviceLocation : "Bern", place.Value!.Text);
            var (toggle, field) = (place.Children![0], place.Children[1]);
            Assert.Equal(2, place.Children.Count);
            Assert.Equal((S.SettingsWidgetWeatherDeviceHeader, new FlyoutControl.Toggle(on)), (toggle.Text, toggle.Control));
            // §411 (R6): the search stands whole while the device's location is on, a pick turning it off; a Find field
            // opens empty, the place it holds ticked under it.
            Assert.True(field.Enabled);
            Assert.Equal("", ((FlyoutField)field.Control!).Text);
            Assert.Empty(((FlyoutField)field.Control!).Finder!.Offered);
            Assert.True(OptionMenu.Apply(weather, OptionMenu.PickOf(picks, toggle.Id)!));
            Assert.Equal(!on, weather.Weather.UseDeviceLocation);
        }
        var device = ListKind.WeatherNow.New();
        device.Weather = new WeatherOptions { UseDeviceLocation = true };
        Assert.True(OptionTexts.SetPlace(device.Weather, new OptionFound("Bern", "", new Place("Bern", "", 46.95, 7.45))));
        Assert.False(device.Weather.UseDeviceLocation);
    }

    /// <summary>
    /// Every face with settings has its submenu (§226), its section's rows on the folder page, every choice, search,
    /// icon and colour a level of its own (the person: "the menus like style, or index, or location
    /// picker etc, should be shown as their own level again"), the one in force ticked, a time's or a
    /// length's parts a level too (§240); no "Remove" (its row's bin). Every face's under its name (§230);
    /// one with none has no submenu at all (§253: "if a face has no settings dont show the submenu. also
    /// dont draw the angled arrow then"), its bin still taking it off.
    /// </summary>
    [Fact]
    public void EveryFacesSubmenuHasItsChoicesAsLevelsOfTheirOwn()
    {
        var empty = 0;
        foreach (var face in FolderFaces.All)
        {
            var folder = Folder(face.Id, FolderFaces.WifiId);
            var options = OptionRows.OfFace(folder.Folder!, 0, Devices);
            List<OptionPick> facePicks = [];
            var row = OptionMenu.FaceSection(folder.Folder!, facePicks, Devices).First(item => item.Reorder != 0);
            // §253: one with nothing to set has no submenu, so no chevron (Flyout.FarSlot); a click on
            // it does nothing; its grip and its bin as every face's.
            if (options.Count == 0)
            {
                empty++;
                Assert.Null(row.Children);
                Assert.False(Flyout.FarSlot(row, anyCheck: false), face.Id);
                var tools = Assert.IsType<FlyoutControl.Tools>(row.Control).Buttons;
                Assert.Equal(2, tools.Count);
                Assert.True(tools[0].Grip);
                var bin = tools[1];
                Assert.Equal(S.DockMenuRemove, bin.Name);
                Assert.True(OptionMenu.Apply(folder, OptionMenu.PickOf(facePicks, bin.Id)!));
                Assert.Equal([FolderFaces.WifiId], folder.Folder!.Faces.Select(kept => kept.Kind));
                continue;
            }
            Assert.NotNull(row.Children);
            Assert.DoesNotContain(row.Children, child => child.Text == S.DockMenuRemove);
            // Every face's under its name, its level's head (§230: "always show the name of the face in
            // the folder settings on the second level"; §271), and no heading of its own.
            Assert.False(row.Children[0].Header, face.Id);
            Assert.Equal(TheDock.TitleOf(folder.Folder!.Faces[0]), row.Text);
            var shown = row.Children.ToList();
            // An alignment is its buttons in its row (§253).
            var levels = options.Where(option => option.Within is null && !option.Segmented && option.Kind is OptionRowKind.Choice or OptionRowKind.Search or OptionRowKind.Text or OptionRowKind.Glyph or OptionRowKind.Color or OptionRowKind.Time or OptionRowKind.Duration).ToList();
            Assert.Equal(levels.Select(option => option.Title), shown.Where(child => child.Children is not null).Select(child => child.Text));
            var picks = shown.Where(child => child.Children is not null).SelectMany(child => child.Children!).ToList();
            Assert.Equal(levels.Sum(option => option.Choices.Count + option.Tiles.Count), picks.Count(child => child.Checked is not null));
            Assert.Equal(levels.Count(option => option.Chosen >= 0), picks.Count(child => child.Checked == true));
            // No heading among the rows: every row one row (§240).
            Assert.DoesNotContain(shown, child => child.Header);
            // A switch heading a search's level stands on it, not here (§240).
            Assert.Equal(options.Count(option => option.Kind == OptionRowKind.Toggle && option.Within is null), shown.Count(child => child.Control is FlyoutControl.Toggle));
            Assert.Equal(options.Count(option => option.Kind == OptionRowKind.Number), shown.Count(child => child.Control is FlyoutControl.Stepper));
            // A line of text a level of its own, its field alone (§246), with what it offers where it offers anything (§304).
            Assert.Equal(options.Count(option => option.Kind == OptionRowKind.Text), shown.Count(child => child.Children is [{ Control: FlyoutField { Finder: null or SuggestFinder } }]));
            Assert.DoesNotContain(shown, child => child.Control is FlyoutField);
            Assert.DoesNotContain(shown, child => child.Text == S.DockEditMoreSettings);
            Assert.False(shown[0].Separator, face.Id);
        }
        Assert.True(empty > 0);
    }

    /// <summary>Down to the row <paramref name="id"/>, from the keys.</summary>
    private static void KeysTo(Flyout menu, uint id)
    {
        for (var step = 0; step < 40 && menu.KeysOn != id; step++)
        {
            _ = menu.Message(WM.WM_KEYDOWN, VK.VK_DOWN, 0);
        }
        Assert.Equal(id, menu.KeysOn);
    }

    private static bool Save(Flyout menu, string name, bool dark) =>
        WindowShots.SaveTogether([.. menu.OpenPanels], name, dark ? ((byte)0, (byte)0, (byte)0) : ((byte)243, (byte)243, (byte)243));

    /// <summary>
    /// A toggle in a face's submenu flips from the keys and the menu stays open, the submenu with it,
    /// through the Refresh that shows the toggle on; the pictures of the three menus, dark and light.
    /// </summary>
    [UiFact]
    public void TheSettingsActInTheMenuAndThePicturesAreDrawn()
    {
        WindowShots.OnHiddenDesktop("option-menu", (gfx, instance) =>
        {
            const float scale = 1.5f;
            foreach (var dark in new[] { true, false })
            {
                var look = AddListPictures.LookFor(dark);
                var tone = dark ? "dark" : "light";
                var work = new RECT { right = (int)(1920 * scale), bottom = (int)(1040 * scale) };
                var anchor = new POINT { x = work.right / 3, y = work.bottom };

                // The processor graph: its settings, the pace's submenu open from the keys.
                using (var menu = new Flyout(gfx, instance, 0) { Configures = true })
                {
                    var line = ListKind.Cpu.New();
                    List<OptionPick> picks = [];
                    var items = GraphMenu(line, picks);
                    menu.Show(gfx, look, scale, items, FlyoutLayout.List, anchor, work, Edge.Bottom, null);
                    WindowShots.Settle();
                    Assert.True(Save(menu, $"option-menu-row-{tone}.png", dark));
                    var pace = items.Single(item => item.Text == S.SettingsOnlineUpdateHeader);
                    KeysTo(menu, pace.Id);
                    _ = menu.Message(WM.WM_KEYDOWN, VK.VK_RIGHT, 0);
                    WindowShots.Settle();
                    Assert.Equal(1, menu.InlineDepth);
                    Assert.True(Save(menu, $"option-menu-graph-{tone}.png", dark));
                    // A pace picked from the keys: set, and the menu stays, the submenu with it (§219).
                    _ = menu.Message(WM.WM_KEYDOWN, VK.VK_DOWN, 0);
                    var chosen = menu.Message(WM.WM_KEYDOWN, VK.VK_RETURN, 0);
                    Assert.Equal(FlyoutEventKind.Adjust, chosen.Kind);
                    Assert.Equal((OptionPickKind.Set, 1), (OptionMenu.PickOf(picks, chosen.Id)!.Kind, OptionMenu.PickOf(picks, chosen.Id)!.Value));
                    Assert.True(menu.IsOpen);
                    Assert.Equal(1, menu.InlineDepth);
                    menu.Hide();
                }

                // A folder's faces: the clock's submenu open, its seconds flipped from the keys.
                using (var menu = new Flyout(gfx, instance, 0) { Configures = true })
                {
                    var folder = Folder(FolderFaces.ClockId, FolderFaces.WeatherId, FolderFaces.CpuId);
                    List<OptionPick> picks = [];
                    var items = FolderMenu(folder, picks);
                    menu.Show(gfx, look, scale, items, FlyoutLayout.List, anchor, work, Edge.Bottom, null);
                    WindowShots.Settle();
                    var clock = items.Single(item => item.Text == "Clock");
                    KeysTo(menu, clock.Id);
                    _ = menu.Message(WM.WM_KEYDOWN, VK.VK_RIGHT, 0);
                    WindowShots.Settle();
                    Assert.True(Save(menu, $"option-menu-faces-{tone}.png", dark));

                    // Its zone first (§292), its seconds under it.
                    var seconds = clock.Children!.First(child => child.Control is FlyoutControl.Toggle);
                    KeysTo(menu, seconds.Id);
                    Assert.Equal(seconds.Id, menu.HotId);
                    var flipped = menu.Message(WM.WM_KEYDOWN, VK.VK_SPACE, 0);
                    Assert.Equal(new FlyoutEvent(FlyoutEventKind.Adjust, seconds.Id), flipped);
                    Assert.True(OptionMenu.Apply(folder, OptionMenu.PickOf(picks, seconds.Id)!));
                    List<OptionPick> again = [];
                    menu.Refresh(OptionMenu.Updated(menu.ShownItems, OptionMenu.FaceSection(folder.Folder!, again, Devices)));
                    Assert.True(menu.IsOpen);
                    Assert.Equal(1, menu.InlineDepth);
                    Assert.Equal(seconds.Id, menu.HotId);
                    Assert.Equal(new FlyoutControl.Toggle(true), menu.ShownItems.Single(item => item.Id == clock.Id).Children!.First(child => child.Control is FlyoutControl.Toggle).Control);
                    menu.Hide();
                }

                // "Add a face": its search over every face (§411).
                using (var menu = new Flyout(gfx, instance, 0) { Configures = true })
                {
                    var folder = Folder(FolderFaces.ClockId, FolderFaces.WeatherId);
                    var items = FolderMenu(folder, []);
                    menu.Show(gfx, look, scale, items, FlyoutLayout.List, anchor, work, Edge.Bottom, null);
                    WindowShots.Settle();
                    KeysTo(menu, items[^1].Id);
                    _ = menu.Message(WM.WM_KEYDOWN, VK.VK_RIGHT, 0);
                    WindowShots.Settle();
                    Assert.Equal(1, menu.InlineDepth);
                    Assert.True(Save(menu, $"option-menu-add-{tone}.png", dark));
                    menu.Hide();
                }
            }
        });
    }

    /// <summary>
    /// §265, §324: ⚙'s "Public holidays ▸" (§351: a calendar's menu links there) on a real menu: "Add a country" at its top, then a row
    /// for each country listed, a bin at its end; Delete on a lit row asks, then takes it out and the menu
    /// stays, the level open and shown again without it. Pictures at 150 %: option-menu-holidays-&lt;look&gt;.png (three
    /// countries) and option-menu-holidays-removed-&lt;look&gt;.png (the first taken out).
    /// </summary>
    [UiFact]
    public void TheHolidaysCountriesAreListedAndTakenOutOnARealMenu()
    {
        WindowShots.OnHiddenDesktop("option-menu-holidays", (gfx, instance) =>
        {
            const float scale = 1.5f;
            foreach (var dark in new[] { true, false })
            {
                var look = AddListPictures.LookFor(dark);
                var tone = dark ? "dark" : "light";
                var work = new RECT { right = (int)(1920 * scale), bottom = (int)(1040 * scale) };
                var anchor = new POINT { x = work.right / 3, y = work.bottom };
                var config = new Config
                {
                    Holidays = new HolidayOptions { Calendars = [new() { Country = "CH", Region = "CH-ZH" }, new() { Country = "GB" }, new() { Country = "FR" }] },
                };
                using var menu = new Flyout(gfx, instance, 0) { Configures = true };
                List<OptionPick> picks = [];
                var items = OptionMenu.Section(OptionHolidays.Rows(config), picks);
                menu.Show(gfx, look, scale, items, FlyoutLayout.List, anchor, work, Edge.Bottom, null);
                WindowShots.Settle();
                var group = items.Single(item => item.Text == S.CoreHolidaysName);
                KeysTo(menu, group.Id);
                _ = menu.Message(WM.WM_KEYDOWN, VK.VK_RIGHT, 0);
                WindowShots.Settle();
                Assert.Equal(1, menu.InlineDepth);
                // §411: the countries, then "Add a country" right under them.
                Assert.Equal(group.Children![0].Id, menu.HotId);
                Assert.Equal(S.CoreHolidaysAdd, group.Children[^1].Text);
                _ = menu.Message(WM.WM_KEYDOWN, VK.VK_DOWN, 0);
                Assert.Equal(group.Children[1].Id, menu.HotId);
                Assert.True(Save(menu, $"option-menu-holidays-{tone}.png", dark));

                // Delete on the lit row: its bin, asked first (the dock's DeleteAsk), and the menu stays.
                var removed = menu.Message(WM.WM_KEYDOWN, VK.VK_DELETE, 0);
                Assert.Equal(FlyoutEventKind.Adjust, removed.Kind);
                var pick = OptionMenu.PickOf(picks, removed.Id)!;
                Assert.Equal(OptionPickKind.Act, pick.Kind);
                var row = OptionRows.Find(OptionHolidays.Rows(config), pick.Key);
                Assert.NotNull(row!.Ask);
                Assert.True(OptionMenu.ApplyRow(row, pick));
                List<OptionPick> again = [];
                menu.Refresh(OptionMenu.Updated(menu.ShownItems, OptionMenu.Section(OptionHolidays.Rows(config), again)));
                WindowShots.Settle();
                Assert.True(menu.IsOpen);
                Assert.Equal(1, menu.InlineDepth);
                Assert.Equal(
                    [HolidayPlaces.Name("CH"), HolidayPlaces.Name("FR"), "", S.CoreHolidaysAdd],
                    menu.ShownItems.Single(item => item.Text == S.CoreHolidaysName).Children!.Select(child => child.Text));
                Assert.True(Save(menu, $"option-menu-holidays-removed-{tone}.png", dark));
                menu.Hide();
            }
        });
    }
}
