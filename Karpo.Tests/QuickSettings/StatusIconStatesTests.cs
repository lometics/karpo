// The status icons' three places (Karpo.Core\StatusIcons.cs, Indicators.Split, IndicatorsIsland): the
// person, 2026-10-08: "for the status icons you should be able to set which status icons are visible
// and which should be hidden / hidden behind an extend arrow, (basically the same style as it is done
// with the tray icons.) as default all should show".
//
// Held here: every icon on the bar where nobody chose, and in a file from before this; one set behind
// the arrow is there and not on the bar; one hidden completely shows nowhere; the arrow stands only
// while an icon behind it is so; the microphone and the location are one icon only in one place; the
// arrow's flyout opens each icon's page and its right click each icon's rows; and the option as the
// file keeps it: written only where it is not the bar, read back the same, dropped from other kinds,
// and merged key by key. Every state is made up; nothing is opened.

using System.Text.Json;
using System.Text.Json.Nodes;
using Karpo.Dock;
using Karpo.Dock.QuickSettings.Services;
using Karpo.Tests.Core;
using Karpo.Tests.Islands;

namespace Karpo.Tests.QuickSettings;

public sealed class StatusIconStatesTests
{
    /// <summary>Everything Windows' taskbar can show, all at once: each status icon so.</summary>
    private static IndicatorState Everything => new()
    {
        Microphone = ["Teams"],
        Camera = ["Teams"],
        Location = ["Maps"],
        Presence = ["Windows Hello"],
        AirplaneMode = true,
        Hotspot = true,
        Metered = true,
        EnergySaver = true,
        DoNotDisturb = true,
        Update = UpdateState.Due,
    };

    /// <summary>
    /// Everything but the microphone and the location together, so each icon is its own: the
    /// location's own icon keeps the location and lets the microphone go, every other the microphone.
    /// </summary>
    private static IndicatorState Alone(StatusIcon icon) =>
        icon == StatusIcon.Location ? Everything with { Microphone = [] } : Everything with { Location = [] };

    private static StatusIconOptions Options(params (StatusIcon Icon, TrayState State)[] states)
    {
        StatusIconOptions? options = null;
        foreach (var (icon, state) in states)
        {
            options = StatusIcons.With(options, icon, state);
        }
        return options ?? new StatusIconOptions();
    }

    private static IndicatorKind[] Kinds(IReadOnlyList<Indicator> icons) => [.. icons.Select(icon => icon.Kind)];

    // --- The states ---------------------------------------------------------------------------

    /// <summary>Nobody chose: every icon on the bar, as before, and nothing behind an arrow.</summary>
    [Fact]
    public void EveryIconIsOnTheBarWhereNobodyChose()
    {
        var (bar, behind) = Indicators.Split(Everything, null);
        Assert.Equal(Kinds(Indicators.Of(Everything)), Kinds(bar));
        Assert.Empty(behind);
        foreach (var icon in StatusIcons.All)
        {
            Assert.Equal(TrayState.Taskbar, StatusIcons.State(null, icon));
            Assert.Equal(TrayState.Taskbar, StatusIcons.State(new StatusIconOptions(), icon));
        }
    }

    /// <summary>Each icon set behind the arrow is there, in Windows' order, and no longer on the bar.</summary>
    [Theory]
    [MemberData(nameof(EveryIcon))]
    public void AnIconBehindTheArrowIsThereAndNotOnTheBar(StatusIcon icon)
    {
        var kind = KindOf(icon);
        var (bar, behind) = Indicators.Split(Alone(icon), Options((icon, TrayState.Overflow)));
        Assert.Equal([kind], Kinds(behind));
        Assert.DoesNotContain(kind, Kinds(bar));
        Assert.Equal(Indicators.Of(Alone(icon)).Count - 1, bar.Count);
    }

    /// <summary>One hidden completely is on neither the bar nor behind the arrow, whatever is so.</summary>
    [Theory]
    [MemberData(nameof(EveryIcon))]
    public void AnIconHiddenCompletelyNeverShows(StatusIcon icon)
    {
        var kind = KindOf(icon);
        foreach (var state in new[] { Everything, Alone(icon) })
        {
            var (bar, behind) = Indicators.Split(state, Options((icon, TrayState.Hidden)));
            Assert.DoesNotContain(kind, Kinds(bar));
            Assert.DoesNotContain(kind, Kinds(behind));
        }
        // Not even the microphone and the location together, where one of them is hidden.
        if (icon is StatusIcon.Microphone or StatusIcon.Location)
        {
            var (bar, behind) = Indicators.Split(Everything, Options((icon, TrayState.Hidden)));
            Assert.DoesNotContain(IndicatorKind.MicrophoneAndLocation, Kinds(bar).Concat(Kinds(behind)));
        }
    }

    /// <summary>Every icon hidden: nothing on the bar and nothing behind an arrow.</summary>
    [Fact]
    public void EveryIconHiddenShowsNothing()
    {
        var all = Options([.. StatusIcons.All.Select(icon => (icon, TrayState.Hidden))]);
        var (bar, behind) = Indicators.Split(Everything, all);
        Assert.Empty(bar);
        Assert.Empty(behind);
    }

    /// <summary>
    /// The microphone and the location in use are Windows' one icon only where both stand in one
    /// place; set apart, each is its own in its own place.
    /// </summary>
    [Fact]
    public void TheMicrophoneAndTheLocationAreOneIconOnlyInOnePlace()
    {
        var state = new IndicatorState { Microphone = ["Teams"], Location = ["Maps"] };
        Assert.Equal([IndicatorKind.MicrophoneAndLocation], Kinds(Indicators.Split(state, null).OnBar));

        var together = Indicators.Split(state, Options((StatusIcon.Microphone, TrayState.Overflow), (StatusIcon.Location, TrayState.Overflow)));
        Assert.Empty(together.OnBar);
        Assert.Equal([IndicatorKind.MicrophoneAndLocation], Kinds(together.Behind));

        var apart = Indicators.Split(state, Options((StatusIcon.Location, TrayState.Overflow)));
        Assert.Equal([IndicatorKind.Microphone], Kinds(apart.OnBar));
        Assert.Equal([IndicatorKind.Location], Kinds(apart.Behind));
    }

    /// <summary>Windows Update hidden takes Windows' own update icon with it.</summary>
    [Fact]
    public void WindowsUpdateHiddenHidesWindowsOwnIconToo()
    {
        var state = new IndicatorState { WindowsIcon = new WindowsUpdateIcon("Restart required", UpdateBadge.Important) };
        Assert.Single(Indicators.Split(state, null).OnBar);
        var (bar, behind) = Indicators.Split(state, Options((StatusIcon.WindowsUpdate, TrayState.Hidden)));
        Assert.Empty(bar);
        Assert.Empty(behind);
    }

    /// <summary>Each setting stands for one of the bar's icons, and its page's glyph is the bar's.</summary>
    [Fact]
    public void EachSettingIsOneIconWithTheBarsGlyph()
    {
        var mapped = Enum.GetValues<IndicatorKind>().Where(kind => Indicators.Setting(kind) is not null).ToList();
        Assert.Equal(StatusIcons.All.Count, mapped.Count);
        Assert.Equal(StatusIcons.All, mapped.Select(kind => Indicators.Setting(kind)!.Value));
        Assert.Null(Indicators.Setting(IndicatorKind.MicrophoneAndLocation));
        foreach (var kind in mapped)
        {
            Assert.Equal(Indicators.Glyph(kind), StatusIcons.Glyph(Indicators.Setting(kind)!.Value));
            Assert.False(string.IsNullOrWhiteSpace(StatusIcons.Title(Indicators.Setting(kind)!.Value)));
        }
    }

    public static TheoryData<StatusIcon> EveryIcon() => [.. StatusIcons.All];

    private static IndicatorKind KindOf(StatusIcon icon) =>
        Enum.GetValues<IndicatorKind>().Single(kind => Indicators.Setting(kind) == icon);

    // --- The config ---------------------------------------------------------------------------

    /// <summary>Only what is not the bar is kept: an icon put back on the bar goes, and with none left the options do.</summary>
    [Fact]
    public void OnlyWhatIsNotTheBarIsKept()
    {
        var options = StatusIcons.With(null, StatusIcon.Camera, TrayState.Overflow);
        Assert.NotNull(options);
        Assert.Equal(TrayState.Overflow, StatusIcons.State(options, StatusIcon.Camera));
        options = StatusIcons.With(options, StatusIcon.Hotspot, TrayState.Hidden);
        Assert.Equal(2, options!.States.Count);

        var back = StatusIcons.With(options, StatusIcon.Camera, TrayState.Taskbar);
        Assert.Equal(["hotspot"], back!.States.Keys);
        // What it was made from is left as it was.
        Assert.Equal(2, options.States.Count);
        Assert.Null(StatusIcons.With(back, StatusIcon.Hotspot, TrayState.Taskbar));
    }

    /// <summary>A key in another case is the same icon; an unknown state, an empty key and "Taskbar" go; an unknown key stays.</summary>
    [Fact]
    public void TheMapIsReadTolerantly()
    {
        var options = new StatusIconOptions
        {
            States = new Dictionary<string, TrayState>
            {
                ["Camera"] = TrayState.Overflow,
                ["airplaneMode"] = TrayState.Taskbar,
                [""] = TrayState.Hidden,
                ["metered"] = (TrayState)42,
                ["somethingNewer"] = TrayState.Hidden,
            },
        }.Normalized();
        Assert.NotNull(options);
        Assert.Equal(TrayState.Overflow, StatusIcons.State(options, StatusIcon.Camera));
        Assert.Equal(TrayState.Taskbar, StatusIcons.State(options, StatusIcon.Metered));
        Assert.Equal(["Camera", "somethingNewer"], options!.States.Keys.Order(StringComparer.Ordinal));
        Assert.Null(new StatusIconOptions { States = new() { ["camera"] = TrayState.Taskbar } }.Normalized());
    }

    /// <summary>The entry's map goes into the file and comes back the same; an entry nobody set writes nothing.</summary>
    [Fact]
    public void TheMapRoundTripsAndANewEntryWritesNothing()
    {
        var plain = BarKind.Indicators.New();
        plain.Normalize();
        Assert.Null(plain.StatusIcons);
        Assert.DoesNotContain("statusIcons", JsonSerializer.Serialize(plain, ConfigJson.Default.BarItem), StringComparison.Ordinal);

        var set = BarKind.Indicators.New();
        set.StatusIcons = Options((StatusIcon.Camera, TrayState.Overflow), (StatusIcon.DoNotDisturb, TrayState.Hidden), (StatusIcon.Metered, TrayState.Taskbar));
        set.Normalize();
        var json = JsonSerializer.Serialize(set, ConfigJson.Default.BarItem);
        var written = JsonNode.Parse(json)!["statusIcons"]!["states"]!.AsObject();
        Assert.Equal("Overflow", (string?)written["camera"]);
        Assert.Equal("Hidden", (string?)written["doNotDisturb"]);
        Assert.Equal(2, written.Count);

        var read = JsonSerializer.Deserialize(json, ConfigJson.Default.BarItem)!;
        read.Normalize();
        foreach (var icon in StatusIcons.All)
        {
            Assert.Equal(StatusIcons.State(set.StatusIcons, icon), StatusIcons.State(read.StatusIcons, icon));
        }
        Assert.True(read.SameOptions(set));
    }

    /// <summary>A file from before this has every icon on the bar, and reads and writes as it did.</summary>
    [Fact]
    public void AnOldFileShowsEveryIcon()
    {
        var config = Configs.Repaired("""
            { "layouts": [ { "id": "default", "name": "Default", "islands": [
              { "id": "status", "kind": "indicators" },
              { "id": "time", "kind": "timer" } ] } ] }
            """);
        var status = Configs.Islands(config).Single(island => island.Kind == BarKind.Indicators);
        Assert.Null(status.StatusIcons);
        Assert.All(StatusIcons.All, icon => Assert.Equal(TrayState.Taskbar, StatusIcons.State(status.StatusIcons, icon)));
        Assert.DoesNotContain("statusIcons", config.ToJson(), StringComparison.Ordinal);
        Configs.RoundTrip(config, out var twice);
        Assert.False(twice);
    }

    /// <summary>A whole file keeps the entry's map through a save and a read.</summary>
    [Fact]
    public void AFileKeepsTheMap()
    {
        var config = Configs.Repaired("""
            { "layouts": [ { "id": "default", "name": "Default", "islands": [
              { "id": "status", "kind": "indicators", "statusIcons": { "states": { "camera": "Overflow", "windowsUpdate": "Hidden" } } } ] } ] }
            """);
        var status = Configs.Islands(config).Single();
        Assert.Equal(TrayState.Overflow, StatusIcons.State(status.StatusIcons, StatusIcon.Camera));
        Assert.Equal(TrayState.Hidden, StatusIcons.State(status.StatusIcons, StatusIcon.WindowsUpdate));
        Assert.Equal(TrayState.Taskbar, StatusIcons.State(status.StatusIcons, StatusIcon.Microphone));
        var again = Configs.RoundTrip(config, out var changed);
        Assert.False(changed);
        Assert.Equal(TrayState.Overflow, StatusIcons.State(Configs.Islands(again).Single().StatusIcons, StatusIcon.Camera));
    }

    /// <summary>Only the status icons carry the map: another kind given one drops it.</summary>
    [Fact]
    public void OtherKindsDropTheMap()
    {
        var clock = BarKind.NightLight.New();
        clock.StatusIcons = Options((StatusIcon.Camera, TrayState.Hidden));
        clock.Normalize();
        Assert.Null(clock.StatusIcons);
    }

    /// <summary>Two apps setting two icons of one entry at once both land, key by key, as the tray's states merge.</summary>
    [Fact]
    public void TwoSidesSettingTwoIconsBothLand()
    {
        static string Json(string text) => JsonNode.Parse(text.Replace('\'', '"'))!.ToJsonString();
        var merged = ConfigMerge.Merge(
            Json("{'islands':[{'id':'s','kind':'indicators'}]}"),
            Json("{'islands':[{'id':'s','kind':'indicators','statusIcons':{'states':{'camera':'Overflow'}}}]}"),
            Json("{'islands':[{'id':'s','kind':'indicators','statusIcons':{'states':{'hotspot':'Hidden'}}}]}"));
        Assert.Equal(
            Json("{'islands':[{'id':'s','kind':'indicators','statusIcons':{'states':{'camera':'Overflow','hotspot':'Hidden'}}}]}"),
            merged);
    }
}

/// <summary>The entry on a bar with its arrow: needs Direct2D (KARPO_UI_TESTS=1).</summary>
public sealed class StatusIconArrowTests
{
    private static readonly Look Dark = Karpo.Tests.Folders.TestLooks.LookFor(true);

    private static IslandStyle Style(float scale = 1f) =>
        new(scale, false, Layout.CellPx(96, new Config { Height = 48 }) * scale, Layout.IconPx(96, new Config { Height = 48 }) * scale, Dark);

    /// <summary>The entry on a bar, on a source of the test's own, with <paramref name="options"/> for its icons; opens nothing.</summary>
    internal static IndicatorsIsland Make(Gfx gfx, SampleIndicators source, StatusIconOptions? options, List<string>? opened = null)
    {
        IndicatorSources.Made = () => source;
        try
        {
            var entry = BarKind.Indicators.New();
            entry.Id = "indicators";
            entry.StatusIcons = options;
            return new IndicatorsIsland(Config.IslandKey(null, entry.Id), entry, gfx, new Config()) { Opened = opened is null ? _ => { } : opened.Add };
        }
        finally
        {
            IndicatorSources.Made = null;
        }
    }

    private static StatusIconOptions? Options(params (StatusIcon Icon, TrayState State)[] states)
    {
        StatusIconOptions? options = null;
        foreach (var (icon, state) in states)
        {
            options = StatusIcons.With(options, icon, state);
        }
        return options;
    }

    /// <summary>
    /// The arrow is a cell after the icons on the bar only while an icon behind it is so: none with
    /// nothing set behind it, none while what is behind it is not so, and gone again when it stops.
    /// </summary>
    [UiFact]
    public void TheArrowStandsOnlyWhileSomethingIsBehindIt()
    {
        Offscreen.OnStaThread(gfx =>
        {
            // Nothing set behind it: no arrow, whatever is so.
            var source = new SampleIndicators(new IndicatorState { Microphone = ["Teams"], Hotspot = true });
            using (var plain = Make(gfx, source, null))
            {
                IIsland entry = plain;
                Assert.Equal(2, entry.Measure(Style()).Count);
                Assert.Equal(-1, plain.Chevron);
            }

            // The hotspot behind it, and not on: no arrow.
            source = new SampleIndicators(new IndicatorState { Microphone = ["Teams"] });
            using var island = Make(gfx, source, Options((StatusIcon.Hotspot, TrayState.Overflow)));
            IIsland bar = island;
            Assert.Equal(1, bar.Measure(Style()).Count);
            Assert.Equal(-1, island.Chevron);

            // The hotspot on: the microphone, then the arrow.
            source.Tell(new IndicatorState { Microphone = ["Teams"], Hotspot = true });
            Assert.True(bar.Refresh(gfx));
            Assert.Equal(2, bar.Measure(Style()).Count);
            Assert.Equal(1, island.Chevron);
            Assert.Equal([IndicatorKind.Microphone], island.Shown.Select(icon => icon.Kind));
            Assert.Equal([IndicatorKind.Hotspot], island.Behind.Select(icon => icon.Kind));
            // The arrow opens Karpo's own flyout, so it has no tooltip.
            Assert.Null(bar.Tooltip(1));

            // Off again: the arrow goes.
            source.Tell(new IndicatorState { Microphone = ["Teams"] });
            Assert.True(bar.Refresh(gfx));
            Assert.Equal(1, bar.Measure(Style()).Count);
            Assert.Equal(-1, island.Chevron);

            // Only icons behind it: the arrow alone. One cell before and one after, each a square, so
            // the layout stands and nothing asks for a rebuild: the cell is drawn again as the arrow.
            source.Tell(new IndicatorState { Hotspot = true });
            Assert.False(bar.Refresh(gfx));
            Assert.Equal(0, island.Chevron);
            Assert.Empty(island.Shown);
            Assert.Equal([IndicatorKind.Hotspot], island.Behind.Select(icon => icon.Kind));
            Assert.Equal(1, bar.Measure(Style()).Count);
        });
    }

    /// <summary>An icon hidden completely takes no cell and opens nothing, and no arrow stands for it.</summary>
    [UiFact]
    public void AHiddenIconTakesNoCell()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var source = new SampleIndicators(new IndicatorState { Camera = ["Teams"], DoNotDisturb = true });
            using var island = Make(gfx, source, Options((StatusIcon.Camera, TrayState.Hidden)));
            IIsland entry = island;
            Assert.Equal(1, entry.Measure(Style()).Count);
            Assert.Equal([IndicatorKind.DoNotDisturb], island.Shown.Select(icon => icon.Kind));
            Assert.Empty(island.Behind);
            Assert.Equal(-1, island.Chevron);
        });
    }

    /// <summary>
    /// The arrow's click is a flyout of the icons behind it, shown as the tray's overflow is; a click
    /// on one opens its page, and a right click on one its own rows, as on the bar.
    /// </summary>
    [UiFact]
    public void TheArrowsFlyoutIsClickableAsTheBar()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var opened = new List<string>();
            var source = new SampleIndicators(new IndicatorState { Microphone = ["Teams"], AirplaneMode = true, Metered = true });
            using var island = Make(gfx, source, Options((StatusIcon.AirplaneMode, TrayState.Overflow), (StatusIcon.Metered, TrayState.Overflow)), opened);
            IIsland entry = island;
            Assert.Equal(2, entry.Measure(Style()).Count);
            var chevron = island.Chevron;
            Assert.Equal(1, chevron);

            var action = entry.Click(chevron);
            Assert.True(action.Icons);
            Assert.Equal(FlyoutLayout.Grid, action.Layout);
            var items = Assert.IsAssignableFrom<IReadOnlyList<FlyoutItem>>(action.Items);
            Assert.Equal([IndicatorsIsland.BehindItem, IndicatorsIsland.BehindItem + 1], items.Select(item => item.Id));
            Assert.Equal([Indicators.Glyph(IndicatorKind.AirplaneMode), Indicators.Glyph(IndicatorKind.Metered)],
                items.Select(item => Assert.IsType<FlyoutIcon.Glyph>(item.Icon).Code));
            Assert.Empty(opened);

            // A click on one: its page, as on the bar.
            entry.Invoke(chevron, items[1].Id);
            Assert.Equal(["ms-settings:network-status"], opened);

            // A right click on one: its own rows, as on the bar, and they act.
            var cell = island.MenuCellOf(chevron, items[0].Id);
            Assert.Equal(chevron + 1, cell);
            var rows = island.MenuRows(cell);
            Assert.Equal(["Turn airplane mode off", "Airplane mode settings"], rows.Select(row => row.Text));
            island.MenuChosen(cell, rows[0].Id);
            Assert.Equal([IndicatorAct.TurnOffAirplaneMode], source.Acts);

            // The arrow's own right click is the widget's menu, and nothing else stands for an icon.
            Assert.Empty(island.MenuRows(chevron));
            Assert.Equal(-1, island.MenuCellOf(0, items[0].Id));
            Assert.Equal(-1, island.MenuCellOf(chevron, IndicatorsIsland.BehindItem + 9));
            // The icon on the bar still opens its own page.
            Assert.Equal(default, entry.Click(0));
            Assert.Equal("ms-settings:privacy-microphone", opened[^1]);
        });
    }

    /// <summary>New options put the icons in their new places at once.</summary>
    [UiFact]
    public void NewOptionsMoveTheIcons()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var source = new SampleIndicators(new IndicatorState { Camera = ["Teams"], EnergySaver = true });
            using var island = Make(gfx, source, null);
            IIsland entry = island;
            Assert.Equal(2, entry.Measure(Style()).Count);

            var moved = BarKind.Indicators.New();
            moved.Id = "indicators";
            moved.StatusIcons = Options((StatusIcon.Camera, TrayState.Overflow));
            entry.Configure(moved, new Config());
            Assert.Equal([IndicatorKind.EnergySaver], island.Shown.Select(icon => icon.Kind));
            Assert.Equal([IndicatorKind.Camera], island.Behind.Select(icon => icon.Kind));
            Assert.Equal(2, entry.Measure(Style()).Count);

            moved.StatusIcons = Options((StatusIcon.Camera, TrayState.Hidden), (StatusIcon.EnergySaver, TrayState.Hidden));
            entry.Configure(moved, new Config());
            Assert.Equal(0, entry.Measure(Style()).Count);
        });
    }
}
