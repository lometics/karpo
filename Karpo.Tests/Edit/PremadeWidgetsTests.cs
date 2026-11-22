// Premade widgets (Karpo.Core\PremadeWidgets.cs; 2026-10-10: "you should premade widgets (which are
// actually just configurations of the action widget), like bluetooth actions, wifi actions, clock
// action, calendar actions"): the add list offers each right under its widget, a pick adds an
// ordinary line of that widget set up as it says, and the presets' footers are the same records.

using Karpo.Dock;
using Karpo.Dock.QuickSettings;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Edit;

public sealed class PremadeWidgetsTests
{
    private const uint First = TheDock.CMD_ADD_WIDGET;

    /// <summary>A bar of one list folder ("list").</summary>
    private static List<BarItem> Bar()
    {
        var list = BarKind.FolderList.New();
        list.Id = "list";
        return [list];
    }

    /// <summary>The footers Windows' flyouts end with, as the person named them, and the two more that cost a line each.</summary>
    [Fact]
    public void ThePremadeActionsAreWindowsFooters()
    {
        Assert.Equal(
            [
                ("clockActions", "dateTime", "focus"),
                ("calendarActions", "dateTime", SettingsActions.CalendarId),
                ("wifiActions", "wifi", "network"),
                ("bluetoothActions", "bluetooth", SettingsActions.AddDeviceId),
                ("soundActions", "sound", "volumeMixer"),
                ("hotspotActions", "mobileHotspot", "network"),
                ("castActions", "display", SettingsActions.AddDeviceId),
                ("nearbyActions", null, SettingsActions.NearbySharingId),
                ("powerActions", "power", "energySaver"),
                ("notificationActions", "notifications", "focus"),
                ("taskManagerActions", null, SettingsActions.TaskManagerId),
                ("clipboardActions", null, "clipboard"),
                ("desktopActions", "multitasking", SettingsActions.TaskViewId),
                ("keyboardActions", null, "language"),
                ("vpnActions", "vpn", "network"),
                ("projectActions", null, "display"),
                ("accessibilityActions", null, "accessibility"),
                ("quickActions", SettingsActions.EditId, SettingsActions.AllSettingsId),
            ],
            PremadeWidgets.All.Select(premade => (premade.Id, premade.Actions!.Value.Left, premade.Actions.Value.Right)));
        Assert.All(PremadeWidgets.All, premade =>
        {
            Assert.Equal(ListKind.Actions, premade.Kind);
            Assert.Same(premade, PremadeWidgets.Find(premade.Id));
            // Each slot a page this build knows: none would read back empty once normalized.
            Assert.NotNull(SettingsActions.Find(premade.Actions!.Value.Right));
            // The left empty only where the flyout had one link, on the right where the person put
            // it (2026-10-11): Task Manager's, the clipboard's and nearby sharing's.
            if (premade != PremadeWidgets.TaskManagerActions && premade != PremadeWidgets.ClipboardActions && premade != PremadeWidgets.NearbyActions && premade != PremadeWidgets.KeyboardActions && premade != PremadeWidgets.ProjectActions && premade != PremadeWidgets.AccessibilityActions)
            {
                Assert.NotNull(SettingsActions.Find(premade.Actions.Value.Left));
            }
            Assert.Single(premade.Glyph);
        });
        Assert.Equal(PremadeWidgets.All.Length, PremadeWidgets.All.Select(premade => premade.Id).Distinct().Count());
    }

    /// <summary>"Add device" opens Settings' Bluetooth page, whose own "Add device" heads it (not the Cast panel).</summary>
    [Fact]
    public void AddDeviceIsTheBluetoothPage() =>
        Assert.Equal(SettingsActions.Find("bluetooth")!.Uri, SettingsActions.Find(SettingsActions.AddDeviceId)!.Uri);

    [Fact]
    public void EveryPremadeWidgetIsNamedInEveryLanguage()
    {
        foreach (var language in Localization.StringFiles.Codes)
        {
            var words = Localization.StringFiles.Read(language).ToDictionary(pair => pair.Key, pair => pair.Value);
            foreach (var premade in PremadeWidgets.All)
            {
                Assert.True(words.TryGetValue($"core.premade.{premade.Id}.title", out var name) && name.Length > 0, $"{language}.json has no name for {premade.Id}");
            }
        }
    }

    /// <summary>
    /// A list folder's add list has each premade widget in its subject of the add tree
    /// (Karpo.Core\AddTree.cs), beside what its actions are about — Bluetooth actions in Connections ›
    /// Bluetooth, Clock actions in Time and calendar — its own name and icon, once; the plain Actions
    /// line in Files and tools. The bar's has none, as the bar has no actions line.
    /// </summary>
    [Fact]
    public void TheAddListOffersThemInTheirSubject()
    {
        var (items, entries, _) = AddList.Rows(WidgetType.List, turns: true, First);
        var premade = entries.Where(entry => entry.Premade is not null).ToList();
        Assert.Equal(PremadeWidgets.All.Select(each => each.Id).Order(), premade.Select(entry => entry.Premade!.Id).Order());
        foreach (var entry in premade)
        {
            Assert.Equal((WidgetType.List, ListKind.Actions, "actions"), (entry.Type, entry.List, entry.Id));
            var row = Assert.Single(AddList.Picks(items), item => item.Id == First + (uint)entries.IndexOf(entry));
            Assert.Equal(entry.Premade!.Title, row.Text);
            Assert.Equal(entry.Premade.Glyph, Assert.IsType<FlyoutIcon.Glyph>(row.Icon).Code);
            Assert.True(row.Enabled);
        }
        Assert.Contains(items.Single(item => item.Text == "Connections").Children!, item => item.Text == "Bluetooth actions");
        Assert.Contains(items.Single(item => item.Text == "Time and calendar").Children!, item => item.Text == "Clock actions");
        Assert.Contains(items.Single(item => item.Text == "Files and tools").Children!, item => item.Text == WidgetInfos.Actions.Title);
        // Named apart from its widget's row across the library.
        Assert.Equal(entries.Count, entries.Select(entry => entry.Key).Distinct().Count());
        Assert.DoesNotContain(AddList.Rows(WidgetType.Bar, turns: true, First).Entries, entry => entry.Premade is not null);
    }

    /// <summary>
    /// The search finds one by its name, by what it opens, and by its widget's name, as one widget,
    /// and Enter adds it where it is the one that matches.
    /// </summary>
    [Fact]
    public void TheSearchFindsThemByWhatTheyOpen()
    {
        var (items, entries, _) = AddList.Rows(WidgetType.List, turns: true, First);
        var bluetooth = First + (uint)entries.FindIndex(entry => entry.Premade == PremadeWidgets.BluetoothActions);

        var found = AddList.Results(items, "bluetooth actions");
        Assert.Equal(bluetooth, AddList.Single(found));
        // Add device ends Bluetooth's page and Windows' Cast panel ("Find other devices"): both.
        Assert.Equal(["Bluetooth actions", "Cast actions"], AddList.Results(items, "add device").Select(result => result.Text));
        var actions = AddList.Results(items, "actions");
        Assert.Equal(1 + PremadeWidgets.All.Length, actions.Count);
        Assert.Equal(WidgetInfos.Actions.Title, actions[0].Text);
        Assert.Null(AddList.Single(actions));
        Assert.Contains(AddList.Results(items, "volume mixer"), result => result.Text == "Sound actions");
    }

    /// <summary>
    /// A pick adds an ordinary actions line set as the premade widget says: the same as a new line
    /// of Actions whose slots somebody set by hand, nothing marking where it came from, and its
    /// slots change as any line's.
    /// </summary>
    [Fact]
    public void APickAddsAnOrdinaryActionsLine()
    {
        var bar = Bar();
        var (_, entries, _) = AddList.Rows(WidgetType.List, turns: true, First);
        var place = new AddSpot("default", default, "list");

        var plain = AddList.Add(bar, place, entries.Single(entry => entry.List == ListKind.Actions && entry.Premade is null));
        var path = AddList.Add(bar, place, entries.Single(entry => entry.Premade == PremadeWidgets.BluetoothActions));

        Assert.NotNull(plain);
        Assert.NotNull(path);
        var folder = Karpo.Core.Folders.FolderIn(bar, "list")!;
        var lines = Karpo.Core.Folders.ReadingOrder(folder).Select(cell => cell.Island).ToList();
        Assert.Equal(2, lines.Count);
        var (made, added) = (lines[0], lines[1]);
        Assert.Equal(Karpo.Core.Folders.Path("list", added.Id), path);
        Assert.NotEqual(made.Id, added.Id);
        Assert.Equal((ListKind.Actions, "bluetooth", SettingsActions.AddDeviceId), (added.List, added.Actions!.Left, added.Actions.Right));
        // Set by hand, the plain line is the premade one.
        (made.Actions!.Left, made.Actions.Right) = ("bluetooth", SettingsActions.AddDeviceId);
        Assert.Equal((made.Kind, made.View, made.Height), (added.Kind, added.View, added.Height));
        // And the premade one changes as any line does.
        added.Actions.Right = "network";
        added.Normalize();
        Assert.Equal("network", added.Actions.Right);
        Assert.Equal(("bluetooth", SettingsActions.AddDeviceId), PremadeWidgets.BluetoothActions.Actions);
    }

    /// <summary>A premade widget made twice is two widgets, sharing no options.</summary>
    [Fact]
    public void EachMadeIsItsOwn()
    {
        var one = PremadeWidgets.WifiActions.New();
        var two = PremadeWidgets.WifiActions.New();

        Assert.NotEqual(one.Id, two.Id);
        Assert.NotSame(one.Actions, two.Actions);
        Assert.Equal(("wifi", "network"), (one.Actions!.Left, one.Actions.Right));
    }

    /// <summary>The clock's, the calendar's, the network's and Bluetooth's footers are the premade widgets, so they cannot drift.</summary>
    [Fact]
    public void ThePresetsFootersAreThePremadeWidgets()
    {
        var machine = new PresetMachine(HasBattery: false, FixedDrives: []);
        foreach (var (id, premade) in new[] { (Presets.ClockId, PremadeWidgets.ClockActions), (Presets.CalendarId, PremadeWidgets.CalendarActions),
            (Presets.WifiId, PremadeWidgets.WifiActions), (Presets.BluetoothId, PremadeWidgets.BluetoothActions) })
        {
            var preset = Presets.Find(id)!;
            Assert.Same(premade, Assert.Single(preset.Items, item => item.Form == "actions").Premade);
            var line = Presets.Folder(preset, machine).Folder!.Cells.Single(cell => cell.Island.List == ListKind.Actions).Island;
            Assert.Equal(premade.Actions, (line.Actions!.Left, line.Actions.Right));
        }
    }
}
