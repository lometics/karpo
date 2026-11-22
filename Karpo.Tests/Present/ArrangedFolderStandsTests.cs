// An arranged folder stands where edit mode's z-order puts it, whatever is done to it (docs\widgets.md §361;
// §359's ZPlan): over its dim, under the bars, under the edit bar — through every interaction, not just as it
// opens. The person, on the build before §359: "in edit mode the folder is darker as well after some
// interactions. (it clips behind)": the folder had gone under the dim.
//
// Asked of the real dock, Karpo.exe --stage on a desktop of its own (InputReachesTheBarsTests.StageDock), a
// config of two folders of lines that only read. A thread on that desktop drives it as a person would — edit
// mode, a folder opened, a click on a line and on the folder's room, a line's menu opened and closed by Escape,
// each of the edit bar's menus opened and closed by its button, the other folder opened from the bar, a line
// dragged, a palette picked on the Appearance page and the type on the Taskbar page by their keys (the bars made again), the file
// changed from outside — and
// after each holds the screen to the plan: the plan the dock last applied (stage-z.json, each window's stratum)
// standing the dims under the arranged folder under the bars under the edit bars, and Windows' own z-order of
// the dock's windows (GetWindow) the plan's, window for window.
//
// §366 (the person, 2026-10-26: "when clicking away on the taskbar it should close the current folder if one
// is open"), on the same dock: a press on the bar's room closes the folder open, at rest and in edit mode —
// edit mode staying —, and a press on another folder's face opens that one.
//
// §386 (2026-10-29): a click on a folder's face opens the folder and its settings from Folder ▾ on the edit bar
// (stage-edit.json's "Menu": whose it is and where it stands) — §481 to §526 a double-click opened it; §527 a click
// selects it and opens it again, its face closing it, so the probes open folders by clicks (stage-edit.json's
// "Selected": what is selected). §387: a press
// held on a folder's face hides none of the bar's "+" (stage-edit.json's "BarAdd"); §397: the folder open, its
// bar shows none of them, the folder its own ("FolderAdd"), and closed they are back — filmed, without a blink.

using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using Xunit.Abstractions;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Tests.Present;

public sealed unsafe class ArrangedFolderStandsTests(ITestOutputHelper output)
{
    /// <summary>The two folders' ids on the bar.</summary>
    private static readonly string[] Folders = ["stands-one", "stands-two"];

    [UiFact]
    public void AnArrangedFolderStandsOverItsDimUnderTheBarsWhateverIsDone() => Drive(probe => probe.Run());

    /// <summary>
    /// §366: a press on the bar away from the folder open — its room, between and round the widgets — closes
    /// the folder: at rest, as any popup goes for a press elsewhere; in edit mode, where the bars are locked
    /// under the arranged folder (§212) and that press did nothing, the folder alone, edit mode staying (the
    /// person's rule: closing one closes only that one). A press on another folder's face opens that folder,
    /// in edit mode as at rest.
    /// </summary>
    [UiFact]
    public void APressOnTheBarClosesTheFolderOpen() => Drive(probe => probe.RunClosing());

    /// <summary>
    /// §520 decision 3 (§407 to §519 a click opened its settings beside it): in edit mode a click on a widget of the bar, on
    /// a folder's face and on a line of the folder open selects it, and nothing pops up; the settings panel open follows the
    /// selection — the widget's page, the folder's as it opens, the line's —; a right click on a line keeps its menu beside it.
    /// </summary>
    [UiFact]
    public void ALeftClickSelectsAndThePanelFollows() => Drive(probe => probe.RunSettings());

    /// <summary>
    /// §527, the person (2026-11-07): "when i click on a folder in edit mode it still does not open it, it only opens the
    /// settings, but it should toggle open the folder too". In edit mode, the settings panel open: a click on a folder's face
    /// selects it and opens it, the panel on its Folder page; clicked again it closes, still selected, the panel open; another
    /// folder's face opens that one in the one click; Enter on the face does what the click does; a drag opens nothing.
    /// </summary>
    [UiFact]
    public void AClickOnAFolderSelectsItAndTogglesItOpen() => Drive(probe => probe.RunToggle());

    /// <summary>
    /// §387, the person: "when i open a folder in edit mode the blue plus buttons flicker"; §397, the person:
    /// "dont show the add buttons on the taskbar anymore if a folder is open". A press on a folder's face, held:
    /// the bar's "+" stand the whole time — the press is no carry until the pointer leaves the drag threshold, so
    /// nothing is lifted and no "+" hidden for the length of a click. Let go: the folder opens and the bar's "+"
    /// go, its own standing; the face clicked again: it closes and they come back. Filmed, each "+" goes once and
    /// comes once, never a frame back between. A click where a "+" stood aside is on the bar's room: the folder
    /// closes, no add list opens.
    /// </summary>
    [UiFact]
    public void AFolderOpenTakesItsBarsPlusesAsideWithoutABlink() => Drive(probe => probe.RunPlusesAside());

    /// <summary>
    /// The person (2026-10-29): "the top edit bar on other screens gets hidden as soon as i open a folder". On
    /// two screens in edit mode, a folder opened on one leaves the other's edit bar standing, in sight and on top,
    /// as it was (the folder's crumb on the folder's screen alone); the same with the folder closed and with one
    /// opened on the other screen. "Add" on the screen without the folder opens that bar's add list, the folder
    /// staying open (§344).
    /// </summary>
    [UiFact]
    public void AFolderOpenLeavesTheOtherScreensEditBar() => Drive(probe => probe.RunTwoScreens(), displays: TwoScreens);

    /// <summary>
    /// §487, the person (2026-11-02): "when clicking done in edit mode it should not close the current folder that you are
    /// in". A folder opened arranged by a click (§527), its panel's menu hanging from Folder ▾: the edit bar's Done ends
    /// edit mode and the folder stays open, at rest, its panel where it stood; the same with no menu open, with a line's
    /// menu open, and with Done on the other screen's edit bar — on two screens, the folder flying in (the person's). The
    /// stage's desktop never has the input, so what the keys do as edit mode ends is OneFolderTests' (KeysBack).
    /// </summary>
    [UiFact]
    public void DoneLeavesTheFolderOpenAtRest() => Drive(probe => probe.RunDone(), displays: TwoScreens);

    /// <summary>
    /// §519, the person (2026-11-06): "dragging and dropping widgets across displays does not work but this should
    /// obviously work in all the places and for all the widget types". Two screens at two scales, each showing its own
    /// layout: a folder's face dragged from the first screen's bar onto the second's leaves the first layout for the
    /// second, and the second screen's bar shows it.
    /// </summary>
    [UiFact]
    public void AFolderDraggedOntoTheOtherScreensBarMovesToItsLayout() => Drive(probe => probe.RunAcross(), config =>
    {
        config.Layouts.Add(new TaskbarLayout
        {
            Id = AcrossLayout,
            Name = "Across",
            Islands = [new BarItem { Id = AcrossWidget, Kind = BarKind.NightLight, Section = BarSection.Middle }],
        });
        config.Screens =
        [
            new Screen { Id = "stage", Name = "Stage", ActiveLayoutId = config.DefaultLayout.Id },
            new Screen { Id = "stage-2", Name = "Stage 2", ActiveLayoutId = AcrossLayout },
        ];
    }, displays: MixedScreens);

    /// <summary>§519: the second screen's layout, and the one widget on it.</summary>
    private const string AcrossLayout = "across";

    private const string AcrossWidget = "across-night";

    /// <summary>§519: two screens at two scales: 2560 by 1440 at 150 % left of the main one, 1920 by 1080 at 100 %.</summary>
    private const string MixedScreens = "-2560,0,2560,1440,144;0,0,1920,1080,96";

    /// <summary><see cref="MixedScreens"/>, in screen px.</summary>
    private static readonly RECT[] Mixed = [new() { left = -2560, right = 0, bottom = 1440 }, new() { right = 1920, bottom = 1080 }];

    /// <summary>The person's two screens: 2560 by 1440 at 150 % each, the second left of the main one.</summary>
    private const string TwoScreens = "-2560,0,2560,1440,144;0,0,2560,1440,144";

    /// <summary><see cref="TwoScreens"/>, in screen px.</summary>
    private static readonly RECT[] Screens = [new() { left = -2560, right = 0, bottom = 1440 }, new() { right = 2560, bottom = 1440 }];

    /// §630, the person (2026-11-09): "the topbar should not be movable anymore. remove that feature". A drag from the edit
    /// bar's start moves nothing; an entry's flyout hangs from its entry; a folder opened and closed, edit mode left and begun
    /// again, it stands at its own spot (§395 to §629 dragged by its grip; §402 back at its own spot in a new edit mode).
    /// </summary>
    [UiFact]
    public void TheEditBarStandsAtItsOwnSpot() => Drive(probe => probe.RunStill());

    /// <summary>
    /// §537, the person (2026-11-07): "when opening edit mode the settings should be open as a default". On two screens, the
    /// bar's own menu's "Edit" on the screen that is not the main one: edit mode with its settings panel open, on that
    /// screen, on the Karpo page the first time; Settings folds it and edit mode stays; Escape leaves. "Edit" again: the panel
    /// open again, on the page shown last (Appearance); Escape folds it, edit mode staying, and the next Escape leaves.
    /// </summary>
    [UiFact]
    public void EditFromTheBarsMenuOpensTheSettingsPanel() => Drive(probe => probe.RunFromTheBarsMenu(), displays: TwoScreens);

    /// <summary>
    /// §394, the person: "show the errors that happen in the main edit topbar and make them clickable so that they
    /// can be shown". A line of a folder that cannot read its feed (an address nobody answers at): its error on the
    /// edit bar — the indicator before "Add", its list under it with the line's row —, and its row picked opens the
    /// line's folder arranged, the line selected and its settings beside it, as a click on it opens them (§407).
    /// </summary>
    [UiFact]
    public void AWidgetsErrorIsOnTheEditBarAndItsRowOpensIt() => Drive(probe => probe.RunErrors(), config =>
    {
        var feed = ListKind.Rss.New();
        feed.Id = Folders[0] + "-feed";
        feed.Rss = new RssOptions { Address = UnreadFeed, Title = "Nobody's news" };
        ((BarItem)config.DefaultLayout.Islands.First(island => island.Id == Folders[0])).Folder!.Put(feed);
    });

    /// <summary>
    /// §523, the person (2026-11-07): "the custom color should be integrated into the normal color picker". On the settings
    /// panel's Appearance page (§567), Taskbar widgets › Background's button (§613 of the mode shown) opens its picker under it — its spectrum, its hue, its
    /// opacity and its hex box in the popup itself —; a drag in the spectrum lands a colour in the file as it is let go, and a
    /// colour typed in the hex box and Enter is the file's value, as typed, alpha and all. A config copy of the stage's own.
    /// </summary>
    [UiFact]
    public void AColourIsMixedInTheSettingsPanel() => Drive(probe => probe.RunColorPicker());

    /// <summary>
    /// §627, the person (2026-11-09): "the blurr factor cannot be changed". On the settings panel's Appearance page, the Bar
    /// tab's Background blur (the page's one number), over a test wallpaper the stage shows (stage-wallpaper.bmp, never the
    /// person's): its + a step more in the file and under the bar (stage-map.json's "Wallpaper": the strip blurred again at
    /// that blur's sigma), again with its reset standing beside it now, its − a step less, a number typed in its box and Enter,
    /// its reset the palette's own again — each the file's and the bars'. Karpo's own palette as Windows is; a see-through
    /// palette of its own blur pinned Light; another pinned Dark on two displays at two scales.
    /// </summary>
    [UiTheory]
    [InlineData(LookPalettes.KarpoId, null, KarpoLook.Blur, false)]
    [InlineData("acrylic", TaskbarMode.Light, 80, false)]
    [InlineData("glacier", TaskbarMode.Dark, 60, true)]
    public void TheBarsBlurIsSetInTheSettingsPanel(string palette, TaskbarMode? mode, int start, bool two) => Drive(probe => probe.RunBlur(palette, start), more: config =>
    {
        _ = LookPalettes.Wear(config, LookPalettes.All.Single(each => each.Id == palette));
        if (mode is { } pinned)
        {
            config.Appearance ??= new Appearance();
            config.Appearance.Mode = pinned;
        }
    }, displays: two ? MixedScreens : null, wallpaper: true);

    /// <summary>
    /// §528, the person (2026-11-07): "when i select multiple widgets it still does not show me the combination of the
    /// settings ... also for taskbar based widgets?". In edit mode two widgets of the bar of two kinds — two switches, Wi-Fi
    /// showing its name and Night light not — selected by a drag over them: the settings panel opens on the page of both
    /// ("2 widgets"), the setting they have in common on it, mixed (§567: no look of their own to share any more); turned on
    /// there, it is on in the file for both.
    /// </summary>
    [UiFact]
    public void TwoWidgetsSelectedShowTheirSettingsInCommon() => Drive(probe => probe.RunCommon(), config =>
    {
        var named = BarKind.WifiSwitch.New();
        named.Id = CommonIds[0];
        named.Section = BarSection.Middle;
        named.Switch = new SwitchOptions { ShowLabel = true };
        var plain = BarKind.NightLight.New();
        plain.Id = CommonIds[1];
        plain.Section = BarSection.Middle;
        config.DefaultLayout.Islands.Add(named);
        config.DefaultLayout.Islands.Add(plain);
    });

    /// <summary>
    /// §573, the person (2026-11-08): "height of the weather widget cannot be changed. it does not update when i click it." A
    /// feed line of the first folder (no address, so nothing is fetched; the current weather has no Height since §590),
    /// selected in the folder open arranged: on its page of the settings panel its Height's Fixed pressed is fixed in the
    /// file, a row more on its Rows' stepper a cell more there too, Fill pressed fill — the panel's one handler
    /// (Dock.Chosen) acts a line's height rows as its menu's does.
    /// </summary>
    [UiFact]
    public void ALinesHeightIsSetInTheSettingsPanel() => Drive(probe => probe.RunHeight(), config =>
    {
        var feed = ListKind.Rss.New();
        feed.Id = HeightLine;
        feed.Rss = new RssOptions();
        config.DefaultLayout.Islands.First(island => island.Id == Folders[0]).Folder!.Put(feed);
    });

    /// <summary>§573: the feed line whose height is set in the panel.</summary>
    private const string HeightLine = "stands-one-feed";

    /// <summary>§528: the two switches selected together, Wi-Fi's and Night light's.</summary>
    private static readonly string[] CommonIds = ["common-wifi", "common-night"];

    /// <summary>§394: a feed's address nobody answers at, on this PC: its read fails at once.</summary>
    private const string UnreadFeed = "http://127.0.0.1:9/karpo-feed.xml";

    /// <summary>
    /// The real dock on a desktop of its own, two folders of lines on its bar (and what <paramref name="more"/> adds),
    /// driven by <paramref name="run"/> on a thread on that desktop; on <paramref name="displays"/> where given (Stage.cs),
    /// else the one made-up display.
    /// </summary>
    private void Drive(Action<Probe> run, Action<Config>? more = null, string? displays = null, bool wallpaper = false)
    {
        var folder = Path.Combine(Path.GetTempPath(), "karpo-tests", "stands-" + Guid.NewGuid().ToString("N")[..12]);
        Directory.CreateDirectory(folder);
        // §627: the stage's desktops a test picture (Stage.Wallpaper), where asked.
        if (wallpaper)
        {
            File.Copy(Karpo.Tests.Edit.SceneTreeTests.TestWallpaper(), Path.Combine(folder, "stage-wallpaper.bmp"));
        }
        var config = new Config();
        foreach (var id in Folders)
        {
            config.DefaultLayout.Islands.Add(Readings(id));
        }
        more?.Invoke(config);
        Sections.Order(config.DefaultLayout.Islands);
        File.WriteAllText(Path.Combine(folder, "config.json"), config.ToJson());
        var name = "karpo-stands-" + Guid.NewGuid().ToString("N");
        HDESK desktop;
        fixed (char* chars = name)
        {
            desktop = CreateDesktopW(chars, null, null, 0, 0x10000000u /* GENERIC_ALL */, null);
        }
        Assert.True(desktop != HDESK.NULL, $"CreateDesktopW failed: {Marshal.GetLastSystemError()}");
        var dock = InputReachesTheBarsTests.StageDock.Start(name, folder, displays);
        var said = new StringBuilder();
        Exception? failed = null;
        try
        {
            var thread = new Thread(() =>
            {
                try
                {
                    Assert.True(SetThreadDesktop(desktop), $"SetThreadDesktop failed: {Marshal.GetLastSystemError()}");
                    _ = SetThreadDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
                    run(new Probe(dock, said));
                }
                catch (Exception error)
                {
                    failed = error;
                }
            });
            thread.Start();
            thread.Join();
        }
        finally
        {
            dock.Dispose();
            HiddenDesktop.Close(desktop);
            output.WriteLine(said.ToString());
            if (File.Exists(Path.Combine(folder, "local", "karpo.log")))
            {
                output.WriteLine(File.ReadAllText(Path.Combine(folder, "local", "karpo.log")));
            }
        }
        if (failed is not null)
        {
            throw new Xunit.Sdk.XunitException("On the dock's desktop: " + failed);
        }
        try
        {
            Directory.Delete(folder, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    /// <summary>A list folder <paramref name="id"/> in the bar's middle, of four lines that only read: two clocks, a heading, the battery.</summary>
    private static BarItem Readings(string id)
    {
        var options = new FolderOptions();
        var island = BarKind.FolderList.New();
        island.Id = id;
        island.Folder = options;
        island.Section = BarSection.Middle;
        foreach (var (line, kind) in new[] { ("clock", ListKind.Clock), ("battery", ListKind.Battery), ("later", ListKind.Clock) })
        {
            var made = kind.New();
            made.Id = $"{id}-{line}";
            options.Put(made);
        }
        var title = ListKind.Heading.New();
        title.Id = $"{id}-heading";
        title.Heading = new HeadingOptions { Text = "Notes" };
        options.Put(title);
        return island;
    }

    /// <summary>A thing to press on (stage-map.json, stage-edit.json): what it is and where, in screen px.</summary>
    private sealed record Spot(string Hit, int Index, string Key, RECT Rect, string Kind = "")
    {
        public POINT Center => new() { x = (Rect.left + Rect.right) / 2, y = (Rect.top + Rect.bottom) / 2 };
    }

    /// <summary>The drive and the checks, on a thread on the dock's desktop.</summary>
    private sealed class Probe(InputReachesTheBarsTests.StageDock dock, StringBuilder said)
    {
        private readonly List<string> wrong = [];

        private int checks;

        public void Run()
        {
            var bar = Read("stage-map.json", wait: true);
            Spot FolderOnBar(string id) => bar.First(spot => spot.Key.EndsWith("/" + id, StringComparison.Ordinal));
            Pump(1500);

            // Edit mode, as a second start asks for it (its settings panel open), the panel folded by Escape.
            EditMode();
            Check("edit mode", folder: false);

            // The first folder opened from the bar: arranged in place.
            Click(FolderOnBar(Folders[0]).Center);
            Check("the first folder opened", folder: true);
            var lines = Lines();
            Assert.True(lines.Count >= 3, $"the folder shows {lines.Count} lines");

            // A click on a line (it is selected; its menu where a click opens one), and on the folder's room.
            Click(lines[0].Center);
            Check("a click on a line", folder: true);
            EscapeMenus();
            Check("Escape after the click on a line", folder: true);
            var panel = Read("stage-edit.json").First(spot => spot.Hit == "Folder").Rect;
            said.AppendLine($"the folder's panel {Text(panel)}, its lines {string.Join("; ", lines.Select(line => $"{line.Key} {Text(line.Rect)}"))}");
            // Beside the panel on the folder's window spread over the display (between two lines there is no room
            // since they stand edge to edge, §410).
            Click(new POINT { x = panel.right + 60, y = (panel.top + panel.bottom) / 2 });
            Check("a click beside the folder", folder: true);

            // A line's menu by a right click, closed by Escape: the focus handed back to the folder.
            RightClick(lines[1].Center);
            Assert.True(Plan().Any(entry => entry.Stratum == Stratum.Menu), "no menu opened on the line");
            Check("a line's menu open", folder: true);
            EscapeMenus();
            Check("a line's menu closed by Escape", folder: true);

            // §575: the line selected last (its right click selected it) its entry's flyout, closed by its entry and opened by it
            // again, the folder staying under it.
            CloseFlyout();
            Click(DeckKey("Nav:Widget").Center);
            Assert.True(Waited(() => Page() == "Widget"), $"the selected line's entry did not open its flyout: {Page()}");
            Check("the line's flyout open", folder: true);
            CloseFlyout();
            Check("the line's flyout closed", folder: true);

            // The other folder opened from the bar, the first closing.
            Click(FolderOnBar(Folders[1]).Center);
            Check("the other folder opened", folder: true);
            Assert.Equal(Folders[1], OpenFolderId(Folders[1]));

            // A line dragged down the list.
            lines = Lines();
            Drag(lines[0].Center, lines[2].Center);
            Assert.NotEqual(lines.Select(line => line.Key), Lines().Select(line => line.Key));
            Check("a line dragged", folder: true);
            EscapeMenus();

            // §520: a setting picked in a page's dropdown, its list over the folder: on the Appearance page a palette (its list's
            // second choice — §567 the Appearance written, every bar drawn again), then on the Taskbar page the type (its
            // second, a Dock: the bars made again); §575 its flyout closed by its entry.
            var looksBefore = Appearance()?.ToJsonString();
            var typeBefore = (string?)Layout()["type"];
            CloseFlyout();
            Click(DeckKey("Nav:" + nameof(PanelPage.Appearance)).Center);
            Click(DeckKey("Pop:" + nameof(PanelPage.Appearance) + "/" + S.SettingsLookPalette).Center);
            Assert.True(Waited(() => Plan().Any(entry => entry.Stratum == Stratum.Menu)), "the palette's list did not open");
            Keys(VK.VK_HOME, VK.VK_DOWN, VK.VK_RETURN);
            Check("a palette picked on the Appearance page", folder: null);
            Assert.True(Waited(() => Appearance()?.ToJsonString() != looksBefore), "the palette picked on the Appearance page changed nothing in the file");
            Assert.True(Waited(() => Plan().All(entry => entry.Stratum != Stratum.Menu)), "the palette's list stayed open after its pick");
            Check("the palette's list closed", folder: null);
            if (OpenFolderKey() is null)
            {
                Click(FolderOnBar(Folders[1]).Center);
            }
            CloseFlyout();
            Click(DeckKey("Nav:Taskbar").Center);
            // The type a segmented control (Taskbar, Dock): its second segment.
            Click(DeckKey("Segment:Taskbar/" + S.SettingsSectionsBar + "/" + S.SettingsLayoutsTypeHeader + "#1").Center);
            Assert.True(Waited(() => (string?)Layout()["type"] != typeBefore), "the type picked on the Taskbar page changed nothing in the file");
            Check("the type picked: the bars made again", folder: null);
            CloseFlyout();

            // The file changed from outside, as another copy of Karpo or the person's editor would: the size.
            bar = Read("stage-map.json", wait: true);
            if (OpenFolderKey() is null && bar.FirstOrDefault(spot => spot.Key.EndsWith("/" + Folders[0], StringComparison.Ordinal)) is { } again)
            {
                Click(again.Center);
            }
            var file = Path.Combine(dock.Folder, "config.json");
            var mapped = File.ReadAllText(Path.Combine(dock.Folder, "stage-map.json"));
            var json = JsonNode.Parse(File.ReadAllText(file))!;
            json["layouts"]![0]!["size"] = 64;
            File.WriteAllText(file, json.ToJsonString());
            Pump(2000);
            Assert.True(Waited(() => File.ReadAllText(Path.Combine(dock.Folder, "stage-map.json")) != mapped), "the file changed from outside, the bar was not made again");
            Check("the file changed from outside", folder: null);

            // Escape: the folder closes, then edit mode ends.
            Escape();
            Check("Escape: the folder closed", folder: false);
            Escape();
            Pump(800);
            Assert.DoesNotContain(Plan(), entry => entry.Stratum is Stratum.Dim or Stratum.ArrangedFolder);

            Assert.True(dock.Running, "the dock ended");
            Assert.True(checks >= 15, $"only {checks} checks made");
            Assert.True(wrong.Count == 0, string.Join(Environment.NewLine, wrong));
        }

        /// <summary>§366: presses on the bar's room with a folder open, at rest and in edit mode.</summary>
        public void RunClosing()
        {
            var bar = Read("stage-map.json", wait: true);
            Spot FolderOnBar(string id) => bar.First(spot => spot.Key.EndsWith("/" + id, StringComparison.Ordinal));
            Pump(1500);

            // At rest: a folder opened by a click is a popup; a click on the bar's room closes it.
            Click(FolderOnBar(Folders[0]).Center);
            Assert.True(Shows(Stratum.Popup), "the folder did not open at rest");
            Click(Room(bar));
            Assert.True(Shows(Stratum.Popup, false), "a click on the bar's room left the folder open at rest");
            said.AppendLine("at rest: the room's click closed the folder");
            // Another folder's face opens that one.
            Click(FolderOnBar(Folders[0]).Center);
            Assert.True(Shows(Stratum.Popup), "the folder did not open again at rest");
            Click(FolderOnBar(Folders[1]).Center);
            Assert.True(Shows(Stratum.Popup), "the other folder's face opened no folder at rest");
            Click(Room(bar));
            Assert.True(Shows(Stratum.Popup, false), "a click on the bar's room left a folder open at rest");

            // Edit mode, as a second start asks for it (its settings panel open), the panel folded by Escape.
            EditMode();
            bar = Read("stage-map.json", wait: true);
            Click(FolderOnBar(Folders[0]).Center);
            Assert.Equal(Folders[0], OpenFolderId(Folders[0]));
            Assert.True(Shows(Stratum.ArrangedFolder), "the folder did not open arranged");

            // The bar's room, locked under the folder: the folder closes, edit mode stays.
            bar = Read("stage-map.json", wait: true);
            Click(Room(bar));
            Assert.Null(OpenFolderId(null));
            Assert.True(Shows(Stratum.ArrangedFolder, false), "a click on the bar's room left the arranged folder open");
            Assert.True(Shows(Stratum.Dim) && Shows(Stratum.EditBar), "a click on the bar's room ended edit mode");
            said.AppendLine("edit mode: the room's click closed the folder, edit mode stayed");

            // Another folder's face, the first open: that one opens arranged.
            Click(FolderOnBar(Folders[0]).Center);
            Assert.Equal(Folders[0], OpenFolderId(Folders[0]));
            Click(FolderOnBar(Folders[1]).Center);
            Assert.Equal(Folders[1], OpenFolderId(Folders[1]));
            Assert.True(Shows(Stratum.Dim), "edit mode ended");
            Assert.True(dock.Running, "the dock ended");
        }

        /// <summary>§520 decision 3, §575: clicks select, nothing pops up; the selection's entry opens its flyout.</summary>
        public void RunSettings()
        {
            var bar = Read("stage-map.json", wait: true);
            Pump(1500);
            EditMode();
            bar = Read("stage-map.json", wait: true);
            var general = Page();

            // A widget of the bar: selected, no menu; its entry's flyout open.
            var widget = bar.First(spot => spot.Kind == "TaskView");
            Click(widget.Center);
            Assert.True(Menu() is null, "a click on Task view opened a menu");
            Assert.True(Waited(() => Page() == "Widget"), $"the panel did not follow the selection to Task view's page: {Page()}");
            // §576: clicked again with its flyout open, the flyout stays; closed, clicked again, it opens again.
            Click(widget.Center);
            Assert.True(Waited(() => Page() == "Widget"), $"a click on the widget selected closed its flyout: {Page()}");
            CloseFlyout();
            Click(widget.Center);
            Assert.True(Waited(() => Page() == "Widget"), $"a click on the widget selected did not open its flyout again: {Page()}");

            // §527: a folder's face: selected and open, the panel on the folder's page.
            var face = bar.First(spot => spot.Key.EndsWith("/" + Folders[0], StringComparison.Ordinal));
            Click(face.Center);
            Assert.True(Menu() is null, "a click on a folder's face opened a menu");
            Assert.Equal(Folders[0], OpenFolderId(Folders[0]));
            Assert.True(Waited(() => Page() == "Folder"), $"the panel did not follow the folder opened: {Page()}");

            // A line of it: selected, its entry's flyout; the folder staying open. A line the flyout stands over is clicked with
            // the flyout closed, and its own opens.
            var lines = Lines();
            var panel = Read("stage-edit.json").First(spot => spot.Hit == "Page").Rect;
            if (lines.FirstOrDefault(line => !Inside(panel, line.Center)) is { } clear)
            {
                Click(clear.Center);
            }
            else
            {
                CloseFlyout();
                Click(lines[0].Center);
            }
            Assert.True(Menu() is null, "a click on a line opened a menu");
            Assert.True(Waited(() => Page() == "Widget"), $"the panel did not follow the line: {Page()}");
            Assert.Equal(Folders[0], OpenFolderId(Folders[0]));

            // A right click on another line: its menu beside it, as before.
            RightClick(lines[1].Center);
            StandsBeside("a right click on a line", lines[1].Key, lines[1].Rect);
            EscapeMenus();

            // §575: Escape closes the flyout first (Escape's own first step), then the folder: no flyout open as before.
            Escape();
            Assert.Null(OpenFolderId(null));
            Assert.True(Waited(() => Page() == general), $"the folder closed left a flyout open: {Page()}");
            Assert.True(dock.Running, "the dock ended");
        }

        /// <summary>§528: two widgets of the bar of two kinds selected by a drag over them; their page in common; a mixed switch turned on for both.</summary>
        public void RunCommon()
        {
            _ = Read("stage-map.json", wait: true);
            Pump(1500);
            EditMode();
            var bar = Read("stage-map.json", wait: true);
            // The two switches, neighbours on the bar, of two kinds.
            var both = bar.Where(spot => CommonIds.Contains(spot.Key.Split('/')[^1])).OrderBy(spot => spot.Rect.left).ToList();
            Assert.True(both.Count == 2, $"the bar shows {string.Join(", ", bar.Select(spot => spot.Key))}");
            var (a, b) = (both[0], both[1]);
            said.AppendLine($"selecting {a.Kind} {Text(a.Rect)} and {b.Kind} {Text(b.Rect)}");
            // A drag from the dim beside the bar over both: the selection rectangle (Dock.EditSelect.cs), as the mouse would.
            var from = new POINT { x = a.Center.x, y = a.Rect.top > 120 ? a.Rect.top - 60 : a.Rect.bottom + 60 };
            Drag(from, b.Center);
            Assert.True(Waited(() => Page() == "Widget"), $"the two widgets selected did not open their entry's flyout: {Page()}");
            // Show name, the one setting they share: mixed (one on, one off), each one's own under it; pressed, on for both.
            bool? Named(string id) => (bool?)Layout()["islands"]!.AsArray().FirstOrDefault(island => (string?)island!["id"] == id)?["switch"]?["showLabel"];
            Assert.True(Named(CommonIds[0]) == true && Named(CommonIds[1]) != true, $"the switches do not start mixed: {Named(CommonIds[0])}, {Named(CommonIds[1])}");
            Click(DeckKey("Toggle:Widget/" + S.SettingsWidgetSwitchLabelHeader).Center);
            Assert.True(Waited(() => Named(CommonIds[0]) == true && Named(CommonIds[1]) == true), $"the mixed switch pressed is not on in the file for both: {Named(CommonIds[0])}, {Named(CommonIds[1])}");
            said.AppendLine("both show their names");
            Assert.True(dock.Running, "the dock ended");
        }

        /// <summary>§573: a folder line's height set on its page of the settings panel: Fixed, a row more, Fill, each in the file.</summary>
        public void RunHeight()
        {
            _ = Read("stage-map.json", wait: true);
            Pump(1500);
            EditMode();
            var bar = Read("stage-map.json", wait: true);
            Click(bar.First(spot => spot.Key.EndsWith("/" + Folders[0], StringComparison.Ordinal)).Center);
            Assert.Equal(Folders[0], OpenFolderId(Folders[0]));
            Spot? feed = null;
            Assert.True(Waited(() => (feed = Lines().FirstOrDefault(line => line.Key.EndsWith("/" + HeightLine, StringComparison.Ordinal))) is not null),
                $"the folder shows no feed line: {string.Join(", ", Lines().Select(line => line.Key))}");
            // Selected with the folder's flyout closed (it may stand over the line): its own entry's flyout opens.
            CloseFlyout();
            Click(feed!.Center);
            Assert.True(Waited(() => Page() == "Widget"), $"the feed line selected did not open its entry's flyout: {Page()}");
            JsonNode? Height() => LineOf(HeightLine)?["height"];
            var segment = "Segment:Widget/" + S.DockEditHeightTitle + "#";
            Click(WheelToDeck(segment + "1").Center);
            Assert.True(Waited(() => (bool?)Height()?["fixed"] == true), $"Fixed pressed in the panel is not in the file: {Height()?.ToJsonString()}");
            var cells = (int)Height()!["cells"]!;
            Click(WheelToDeck("More:Widget/" + S.DockEditHeightRows).Center);
            Assert.True(Waited(() => (int?)Height()?["cells"] == cells + 1), $"a row more in the panel is not in the file: {Height()?.ToJsonString()}");
            Click(WheelToDeck(segment + "2").Center);
            Assert.True(Waited(() => (bool?)Height()?["fill"] == true), $"Fill pressed in the panel is not in the file: {Height()?.ToJsonString()}");
            said.AppendLine("the feed line's height set in the panel: " + Height()?.ToJsonString());
            Assert.True(dock.Running, "the dock ended");
        }

        /// <summary>§573: the folder line <paramref name="id"/> as the file has it now, wherever it stands; null for none.</summary>
        private JsonNode? LineOf(string id)
        {
            static JsonNode? Find(JsonNode? node, string id) => node switch
            {
                JsonObject one when (string?)(one["id"] as JsonValue) == id => one,
                JsonObject one => one.Select(pair => Find(pair.Value, id)).FirstOrDefault(found => found is not null),
                JsonArray many => many.Select(each => Find(each, id)).FirstOrDefault(found => found is not null),
                _ => null,
            };
            return Find(Layout(), id);
        }

        /// <summary>§527: a click on a folder's face selects it and toggles it open; Enter the same; a drag opens nothing.</summary>
        public void RunToggle()
        {
            _ = Read("stage-map.json", wait: true);
            Pump(1500);
            EditMode();
            var bar = Read("stage-map.json", wait: true);
            Spot Face(string id) => bar.First(spot => spot.Key.EndsWith("/" + id, StringComparison.Ordinal));
            bool Selected(string id) => Read("stage-edit.json").Any(spot => spot.Hit == "Selected" && spot.Key.EndsWith("/" + id, StringComparison.Ordinal));

            // A click: the folder selected and open, its page on the panel, nothing popped up.
            Click(Face(Folders[0]).Center);
            Assert.Equal(Folders[0], OpenFolderId(Folders[0]));
            Assert.True(Waited(() => Selected(Folders[0])), "a click on a folder's face did not select it");
            Assert.True(Waited(() => Page() == "Folder"), $"the panel did not follow the folder opened: {Page()}");
            Assert.True(Menu() is null, "a click on a folder's face opened a menu");

            // Clicked again: closed, still selected, §575 its entry on the bar still.
            Click(Face(Folders[0]).Center);
            Assert.Null(OpenFolderId(null));
            Assert.True(Selected(Folders[0]), "the folder closed by its face is no longer selected");
            Assert.True(Waited(() => Read("stage-edit.json").Any(spot => spot.Hit == "Deck" && spot.Key == "Nav:Folder")), "the folder closed, still selected, took its entry off the bar");

            // Once more: open again; the other folder's face: that one, in the one click.
            Click(Face(Folders[0]).Center);
            Assert.Equal(Folders[0], OpenFolderId(Folders[0]));
            Click(Face(Folders[1]).Center);
            Assert.Equal(Folders[1], OpenFolderId(Folders[1]));
            Assert.True(Waited(() => Selected(Folders[1])), "the other folder opened is not selected");
            Assert.True(Waited(() => Page() == "Folder"), $"the panel did not follow the other folder: {Page()}");
            Click(Face(Folders[1]).Center);
            Assert.Null(OpenFolderId(null));

            // Enter on the face with the keys: the click's toggle open; Escape closes it.
            _ = PostMessageW(Under(Face(Folders[1]).Center), WM.WM_KEYDOWN, VK.VK_RETURN, 0);
            Pump(1000);
            Assert.Equal(Folders[1], OpenFolderId(Folders[1]));
            Escape();
            Assert.Null(OpenFolderId(null));

            // A drag of the face: carried, nothing opened.
            var from = Face(Folders[0]).Center;
            Drag(from, new POINT { x = from.x + 40, y = from.y });
            Assert.Null(OpenFolderId(null));
            Assert.True(dock.Running, "the dock ended");
        }

        /// <summary>§523: Appearance › Taskbar widgets › Background's picker (§613 of the mode shown, 400 DIP wide): a drag in its spectrum, a colour typed in its hex box.</summary>
        public void RunColorPicker()
        {
            _ = Read("stage-map.json", wait: true);
            Pump(1500);
            EditMode();
            Click(DeckKey("Nav:" + nameof(PanelPage.Appearance)).Center);
            Assert.True(Waited(() => Page() == "Appearance"), $"the Appearance entry opened no flyout: {Page()}");
            // §592: the Taskbar widgets tab chosen, its Background under the preview and the tabs: wheeled into sight.
            Click(DeckKey("Tab:Appearance/" + PanelTabs.Mark + LookParts.Name(LookPart.Widgets) + "#1").Center);
            Pump(400);
            var background = "Pop:Appearance/" + PanelTabs.Mark + LookParts.Name(LookPart.Widgets) + "/" + S.SettingsAppearanceCustomBackgroundHeader;

            var button = WheelToDeck(background);
            Click(button.Center);
            Assert.True(Waited(() => Mix().Count == 4), $"the colour's picker did not open: {Mix().Count} of its parts mapped");
            // §559: its picker hangs from its button, under it or over it, never over the button itself (PopupPlace).
            var picker = Read("stage-edit.json").First(spot => spot.Hit == "Menu").Rect;
            Assert.True(picker.top >= button.Rect.bottom || picker.bottom <= button.Rect.top, $"the picker {Text(picker)} covers its button {Text(button.Rect)}");
            // In sight in its popup: scrolled to by the wheel where the popup is shorter than its rows.
            var menu = Plan().First(entry => entry.Stratum == Stratum.Menu).Window;
            RECT shown;
            _ = GetWindowRect(menu, &shown);
            for (var turn = 0; turn < 12 && Mix().Single(spot => spot.Key == "Hex").Rect.bottom > Read("stage-edit.json").First(spot => spot.Hit == "Menu").Rect.bottom; turn++)
            {
                var middle = Read("stage-edit.json").First(spot => spot.Hit == "Menu").Center;
                _ = PostMessageW(menu, WM.WM_MOUSEWHEEL, (WPARAM)(nuint)unchecked((uint)(-120 << 16)), (LPARAM)(nint)((middle.y << 16) | (middle.x & 0xFFFF)));
                Pump(300);
            }
            var spectrum = Mix().Single(spot => spot.Key == nameof(MixPart.Spectrum)).Rect;
            said.AppendLine($"the picker's spectrum at {Text(spectrum)}, its popup's window at {Text(shown)}");

            // A drag in the spectrum: the colour where it was let go, in the file.
            var before = Appearance()?.ToJsonString();
            var (w, h) = (spectrum.right - spectrum.left, spectrum.bottom - spectrum.top);
            Drag(new POINT { x = spectrum.left + (w / 4), y = spectrum.top + (h / 4) }, new POINT { x = spectrum.left + (3 * w / 4), y = spectrum.top + (h / 3) });
            Assert.True(Waited(() => Appearance()?.ToJsonString() != before), "a drag in the spectrum changed nothing in the file");
            Assert.True(Plan().Any(entry => entry.Stratum == Stratum.Menu), "the picker closed as its colour was let go");
            said.AppendLine($"after the drag: {Appearance()?.ToJsonString()}");

            // A colour typed in the hex box over its words, and Enter: the file's, as typed.
            Click(Mix().Single(spot => spot.Key == "Hex").Center);
            Keys(VK.VK_END, VK.VK_BACK, VK.VK_BACK, VK.VK_BACK, VK.VK_BACK, VK.VK_BACK, VK.VK_BACK, VK.VK_BACK, VK.VK_BACK, VK.VK_BACK, VK.VK_BACK);
            foreach (var c in "#12345680")
            {
                _ = PostMessageW(menu, WM.WM_CHAR, c, 0);
            }
            Pump(300);
            Keys(VK.VK_RETURN);
            Assert.True(Waited(() => Appearance()?.ToJsonString().Contains("\"#80123456\"", StringComparison.Ordinal) == true), $"the colour typed is not the file's: {Appearance()?.ToJsonString()}");
            Assert.True(Plan().Any(entry => entry.Stratum == Stratum.Menu), "the picker closed as a colour was typed");

            // §613: the picker the one popup wider than a value's box, 400 DIP; Escape closes it.
            var dpi = Read("stage-edit.json").First(spot => spot.Hit == "Work").Index;
            var hung = Read("stage-edit.json").First(spot => spot.Hit == "Menu").Rect;
            Assert.True(Math.Abs(hung.right - hung.left - (int)MathF.Round(PanelMetrics.PickerWide * dpi / 96f, MidpointRounding.AwayFromZero)) <= 1, $"the picker {Text(hung)} is not {PanelMetrics.PickerWide} DIP wide at {dpi} dpi");
            Keys(VK.VK_ESCAPE);
            Assert.True(Waited(() => Mix().Count == 0), "the picker did not close");
            Assert.True(dock.Running, "the dock ended");
        }

        /// <summary>
        /// §627: Appearance › Bar › Background blur of <paramref name="palette"/> (its design's <paramref name="start"/>) stepped,
        /// typed and reset in the panel, each the file's and every bar's: the wallpaper under it blurred again at its sigma.
        /// </summary>
        public void RunBlur(string palette, int start)
        {
            _ = Read("stage-map.json", wait: true);
            Pump(1500);
            EditMode();
            Click(DeckKey("Nav:" + nameof(PanelPage.Appearance)).Center);
            Assert.True(Waited(() => Page() == "Appearance"), $"the Appearance entry opened no flyout: {Page()}");
            var tab = PanelTabs.Mark + LookParts.Name(LookPart.Taskbar);
            Click(DeckKey("Tab:Appearance/" + tab + "#0").Center);
            Pump(400);
            var row = "Appearance/" + tab + "/" + S.SettingsLookBlur;
            int? Blur() => (int?)Appearance()?["palettes"]?[palette]?["taskbar"]?["blur"];
            string Said() => Appearance()?.ToJsonString() ?? "(none)";
            // Every bar shows the wallpaper blurred at the blur's sigma at its display's scale.
            var scales = Read("stage-edit.json").Where(spot => spot.Hit == "Work").Select(spot => spot.Index / 96f).ToList();
            List<int> Shown() => [.. Read("stage-map.json").Where(spot => spot.Hit == "Wallpaper").Select(spot => spot.Index).Order()];
            List<int> Wanted(int blur) => [.. scales.Select(scale => (int)MathF.Round(KarpoLook.Sigma(blur, scale) * 100)).Order()];
            void Holds(int? file, int drawn, string when)
            {
                Assert.True(Waited(() => Blur() == file), $"{when}: the file says {Said()}, not {file}");
                Assert.True(Waited(() => Shown().SequenceEqual(Wanted(drawn))), $"{when}: the bars show the wallpaper at sigmas {string.Join(", ", Shown())}, not {string.Join(", ", Wanted(drawn))}");
                said.AppendLine($"{when}: {Said()}, the bars at sigmas {string.Join(", ", Shown())}");
            }
            Holds(null, start, "at first");

            Click(WheelToDeck("More:" + row).Center);
            Holds(start + 5, start + 5, "+ pressed");
            // Its reset beside it now: + again, − back.
            Click(WheelToDeck("More:" + row).Center);
            Holds(start + 10, start + 10, "+ pressed again, its reset beside it");
            Click(WheelToDeck("Fewer:" + row).Center);
            Holds(start + 5, start + 5, "− pressed");

            // A number typed over its box (its words all chosen as it takes the keys), and Enter.
            var box = WheelToDeck("Number:" + row);
            Click(box.Center);
            var panel = Under(box.Center);
            foreach (var c in "15")
            {
                _ = PostMessageW(panel, WM.WM_CHAR, c, 0);
            }
            _ = PostMessageW(panel, WM.WM_KEYDOWN, VK.VK_RETURN, 0);
            Holds(15, 15, "15 typed");
            // And the most, typed the same.
            Click(WheelToDeck("Number:" + row).Center);
            foreach (var c in "100")
            {
                _ = PostMessageW(panel, WM.WM_CHAR, c, 0);
            }
            _ = PostMessageW(panel, WM.WM_KEYDOWN, VK.VK_RETURN, 0);
            Holds(100, 100, "100 typed");

            // Its reset (the tab's one, keyed by its words): the palette's own again, nothing in the file.
            Click(WheelToDeck("Act:Appearance/" + tab + "/" + S.SettingsLookReset).Center);
            Holds(null, start, "reset");
            Assert.True(dock.Running, "the dock ended");
        }

        /// <summary>§523: the parts of the colour's picker in the menu open, each by its part ("Spectrum", "Hue", "Opacity", "Hex").</summary>
        private List<Spot> Mix() => [.. Read("stage-edit.json").Where(spot => spot.Hit == "Mix")];

        /// <summary>
        /// What must hold after <paramref name="when"/>: a menu open, of the widget keyed <paramref name="key"/>,
        /// standing beside its <paramref name="block"/> (a bar's block, a folder's line), from no crumb of the edit bar.
        /// </summary>
        private void StandsBeside(string when, string key, RECT block)
        {
            var menu = Menu();
            Assert.True(menu is not null, $"{when}: no menu opened");
            Assert.True(string.Equals(menu!.Key, key, StringComparison.OrdinalIgnoreCase), $"{when}: the menu open is {menu.Key}'s, not {key}'s");
            Assert.True(Beside(menu.Rect, block), $"{when}: the menu stands at {Text(menu.Rect)}, not beside the widget at {Text(block)}");
            said.AppendLine($"{when}: the menu at {Text(menu.Rect)}, beside the widget at {Text(block)}");
        }

        /// <summary>
        /// Whether a menu at <paramref name="menu"/> stands beside <paramref name="block"/>: off it, along one of its
        /// four sides, at most a small gap away (MenuSides).
        /// </summary>
        private static bool Beside(RECT menu, RECT block)
        {
            const int Gap = 40;
            var across = Math.Max(block.left - menu.right, menu.left - block.right);
            var along = Math.Max(block.top - menu.bottom, menu.top - block.bottom);
            return across is >= -2 and <= Gap && along < 0 || along is >= -2 and <= Gap && across < 0;
        }

        /// <summary>The menu open, by the widget it is of, and where it stands; null for none.</summary>
        private Spot? Menu() => Read("stage-edit.json").FirstOrDefault(spot => spot.Hit == "Menu");

        /// <summary>
        /// §387, §397: a press on a folder's face held, let go, and the face clicked again — filmed over the bar
        /// (KARPO_PNG_DIR, film-pluses-open.png, film-pluses-close.png).
        /// </summary>
        public void RunPlusesAside()
        {
            var bar = Read("stage-map.json", wait: true);
            Pump(1500);
            EditMode();
            bar = Read("stage-map.json", wait: true);
            var standing = BarAdds();
            Assert.True(standing.Count > 0, "the bar shows no \"+\" in edit mode");
            var face = bar.First(spot => spot.Key.EndsWith("/" + Folders[0], StringComparison.Ordinal)).Center;
            var window = Under(face);
            // Each "+" on the screen, and the film's area: the bar round them all.
            var pluses = Read("stage-edit.json").Where(spot => spot.Hit == "BarAdd").Select(spot => spot.Rect).ToList();
            var area = bar.Select(spot => spot.Rect).Concat(pluses).Aggregate((a, b) => new RECT { left = Math.Min(a.left, b.left), top = Math.Min(a.top, b.top), right = Math.Max(a.right, b.right), bottom = Math.Max(a.bottom, b.bottom) });
            area = new RECT { left = area.left - 8, top = area.top - 8, right = area.right + 8, bottom = area.bottom + 8 };
            // The bar's window alone, over the arranged folder (§361), so the frames come as fast as they can.
            HWND[] filmed = [window];

            // §387: held down on the face nothing is carried, the bar's "+" stand. §397: let go, the folder opens
            // and takes them aside, its own standing.
            var opening = new FrameFilm.Film("pluses-open", area) { Before = FrameFilm.Snap(area, filmed, 0, "before") };
            var clock = System.Diagnostics.Stopwatch.StartNew();
            Post(window, WM.WM_MOUSEMOVE, face, 0);
            Post(window, WM.WM_LBUTTONDOWN, face, 0x0001);
            Watch(opening, clock, 600, "held", filmed);
            var held = BarAdds();
            Post(window, WM.WM_LBUTTONUP, face, 0);
            Watch(opening, clock, 1600, "let go", filmed);
            opening.End = FrameFilm.Snap(area, filmed, clock.Elapsed.TotalMilliseconds, "open");
            opening.Save();
            Once(opening, pluses, "the folder opening");
            Assert.True(standing.SequenceEqual(held), $"held down on the folder's face, the bar shows {held.Count} of its {standing.Count} \"+\": {string.Join("; ", held)}");
            Assert.Equal(Folders[0], OpenFolderId(Folders[0]));
            var open = BarAdds();
            Assert.True(open.Count == 0, $"with the folder open on it, the bar shows {open.Count} of its {standing.Count} \"+\": {string.Join("; ", open)}");
            Assert.True(Read("stage-edit.json").Any(spot => spot.Hit == "FolderAdd"), "the folder open shows none of its own \"+\"");

            // The face clicked again: the folder closes, the bar's "+" back where they stood.
            var closing = new FrameFilm.Film("pluses-close", area) { Before = opening.End };
            clock.Restart();
            Post(window, WM.WM_MOUSEMOVE, face, 0);
            Post(window, WM.WM_LBUTTONDOWN, face, 0x0001);
            Post(window, WM.WM_LBUTTONUP, face, 0);
            Watch(closing, clock, 1000, "closing", filmed);
            closing.End = FrameFilm.Snap(area, filmed, clock.Elapsed.TotalMilliseconds, "closed");
            closing.Save();
            Assert.Null(OpenFolderId(null));
            var back = BarAdds();
            Assert.True(standing.SequenceEqual(back), $"the folder closed, the bar shows {back.Count} of its {standing.Count} \"+\": {string.Join("; ", back)}");
            Once(closing, pluses, "the folder closing");

            // Opened again, a click where a "+" stood aside is on the bar's room: the folder closes (§366), no add list opens.
            Click(face);
            Assert.Equal(Folders[0], OpenFolderId(Folders[0]));
            EscapeMenus();
            Click(new POINT { x = (pluses[0].left + pluses[0].right) / 2, y = (pluses[0].top + pluses[0].bottom) / 2 });
            Assert.Null(OpenFolderId(null));
            Assert.True(Menu() is null && !Plan().Any(entry => entry.Stratum == Stratum.Menu), "a click where a \"+\" stood aside opened a menu");
            Assert.True(standing.SequenceEqual(BarAdds()), "the folder closed by a click on the bar, its \"+\" are not back");
            Assert.True(dock.Running, "the dock ended");
        }

        /// <summary>§487: Done with a folder open arranged, a menu open or none: edit mode ends, the folder stays open at rest.</summary>
        public void RunDone()
        {
            Read("stage-map.json", wait: true);
            Pump(1500);
            // The folder on the second screen; Done on its screen's edit bar, with its menu open and not, and on the other's.
            foreach (var (menu, doneOn) in new[] { ("folder", 1), ("", 1), ("line", 1), ("folder", 0) })
            {
                var withPanel = menu == "folder";
                EditMode();
                var bar = Read("stage-map.json", wait: true);
                Click(bar.First(spot => spot.Key.EndsWith("/" + Folders[0], StringComparison.Ordinal) && Inside(Screens[1], spot.Center)).Center);
                Assert.Equal(Folders[0], OpenFolderId(Folders[0]));
                var when = $"Done on screen {doneOn}{(menu == "folder" ? ", a flyout open" : menu == "line" ? ", a line selected" : "")}";
                Assert.True(Menu() is null, "the click opened a menu");
                // §575: with a flyout open on Done's screen (Taskbar's), or a line selected.
                if (withPanel)
                {
                    Click(DeckKeyOn(doneOn, "Nav:" + nameof(PanelPage.Taskbar)).Center);
                    Assert.True(Waited(() => Page() == "Taskbar"), $"the Taskbar entry opened no flyout: {Page()}");
                }
                if (menu == "line")
                {
                    Click(Lines()[0].Center);
                }
                var panel = Read("stage-edit.json").First(spot => spot.Hit == "Folder").Rect;

                HookedClick(DeckOn(doneOn, PanelHitKind.Done).Center);
                Assert.True(Shows(Stratum.EditBar, false) && Shows(Stratum.Dim, false), $"{when}: edit mode did not end");
                Pump(1500);
                var popups = Plan().Where(entry => entry.Stratum == Stratum.Popup && IsWindowVisible(entry.Window)).Select(entry => entry.Window).ToList();
                said.AppendLine($"{when}: {popups.Count} popups in sight: {string.Join(", ", popups.Select(Hex))}");
                Assert.True(popups.Count == 1, $"{when}: the folder closed with edit mode ({popups.Count} popups in sight)");
                Assert.True(Plan().All(entry => entry.Stratum != Stratum.ArrangedFolder || !IsWindowVisible(entry.Window)), $"{when}: the folder is still arranged");
                // Where its panel stood: the folder's window holds its middle.
                var middle = new POINT { x = (panel.left + panel.right) / 2, y = (panel.top + panel.bottom) / 2 };
                Assert.True(Under(middle) == popups[0], $"{when}: the folder's window is not where its panel stood ({Text(panel)})");

                // At rest it goes as any popup does: a press on the bar's room.
                bar = Read("stage-map.json", wait: true);
                Click(Room(bar));
                Assert.True(Shows(Stratum.Popup, false), $"{when}: a click on the bar's room left the folder open at rest");
            }
            Assert.True(dock.Running, "the dock ended");
        }

        /// <summary>Two screens: a folder opened on one, closed, opened on the other; "Add" on the screen without it.</summary>
        public void RunTwoScreens()
        {
            Read("stage-map.json", wait: true);
            Pump(1500);
            EditMode();
            var bar = Read("stage-map.json", wait: true);
            Spot FaceOn(int screen) => bar.FirstOrDefault(spot => spot.Key.EndsWith("/" + Folders[0], StringComparison.Ordinal) && Inside(Screens[screen], spot.Center))
                ?? throw new Xunit.Sdk.XunitException($"no folder on screen {screen}'s bar: {string.Join("; ", bar.Select(spot => $"{spot.Key} {Text(spot.Rect)}"))}");
            EditBarsStand("edit mode", folderOn: -1);
            // The pictures (KARPO_PNG_DIR, film-two-screens.png): both screens' tops, before and with the folder open on the first.
            var film = new FrameFilm.Film("two-screens", new RECT { left = Screens[0].left, top = 0, right = Screens[1].right, bottom = 200 });
            film.Before = FrameFilm.Snap(film.Area, Screen(), 0, "before");

            Click(FaceOn(0).Center);
            Assert.Equal(Folders[0], OpenFolderId(Folders[0]));
            EditBarsStand("a folder opened on the first screen", folderOn: 0);
            film.End = FrameFilm.Snap(film.Area, Screen(), 1000, "open");
            film.Save();

            Escape();
            Assert.Null(OpenFolderId(null));
            EditBarsStand("the folder closed", folderOn: -1);

            bar = Read("stage-map.json", wait: true);
            Click(FaceOn(1).Center);
            Assert.Equal(Folders[0], OpenFolderId(Folders[0]));
            EscapeMenus();
            EditBarsStand("a folder opened on the second screen", folderOn: 1);

            // "Add" on the first screen's edit bar, the folder open on the second: §575 the add list's flyout opens on the first,
            // the folder stays (§344); Add again closes it.
            Click(DeckOn(0, PanelHitKind.Add).Center);
            Assert.True(Waited(() => Page() == "Add"), $"\"Add\" on the other screen opened no add list: {Page()}");
            Assert.True(Inside(Screens[0], Read("stage-edit.json").First(spot => spot.Hit == "Page").Center), "the add list opened off Add's screen");
            Assert.Equal(Folders[0], OpenFolderId(Folders[0]));
            Click(DeckOn(0, PanelHitKind.Add).Center);
            Assert.True(Waited(() => Page() is null), "Add clicked again left its list open");
            EditBarsStand("the add list closed", folderOn: 1);
            Assert.True(dock.Running, "the dock ended");
        }

        /// <summary>§519: a folder's face dragged from the first screen's bar onto the second's, which shows another layout at another scale.</summary>
        public void RunAcross()
        {
            Read("stage-map.json", wait: true);
            Pump(1500);
            EditMode();
            var bar = Read("stage-map.json", wait: true);
            Spot On(string id, RECT screen) => bar.FirstOrDefault(spot => spot.Key.EndsWith("/" + id, StringComparison.Ordinal) && Inside(screen, spot.Center))
                ?? throw new Xunit.Sdk.XunitException($"no {id} on that screen's bar: {string.Join("; ", bar.Select(spot => $"{spot.Key} {Text(spot.Rect)}"))}");
            var face = On(Folders[0], Mixed[0]);
            var there = On(AcrossWidget, Mixed[1]);

            Drag(face.Center, there.Center);

            Assert.True(Waited(() => IdsOf(AcrossLayout).Contains(Folders[0])), $"the face let go on the other screen's bar is not in its layout: {string.Join(" ", IdsOf(AcrossLayout))}");
            Assert.DoesNotContain(Folders[0], IdsOf(TaskbarLayouts.DefaultId));
            said.AppendLine($"across: {string.Join(" ", IdsOf(AcrossLayout))}");
            Assert.True(
                Waited(() => Read("stage-map.json").Exists(spot => spot.Key == Config.IslandKey(Config.DisplayOwner("stage-2"), Folders[0]) && Inside(Mixed[1], spot.Center))),
                "the second screen's bar does not show the face");
            Assert.True(dock.Running, "the dock ended");
        }

        /// <summary>§519: the ids of the widgets of layout <paramref name="id"/>, as the file has them now.</summary>
        private List<string> IdsOf(string id) =>
            [.. JsonNode.Parse(File.ReadAllText(Path.Combine(dock.Folder, "config.json")))!["layouts"]!.AsArray()
                .First(layout => (string?)layout!["id"] == id)!["islands"]!.AsArray()
                .Select(island => (string?)island!["id"] ?? "")];

        /// <summary>
        /// What must hold after <paramref name="when"/>, waited for a moment: each screen's edit bar in sight, its
        /// "Add" and "Done" there, the window under its "Done" the edit bar's (nothing over it), the folder's crumb on
        /// screen <paramref name="folderOn"/> alone (-1: none).
        /// </summary>
        private void EditBarsStand(string when, int folderOn)
        {
            string? why = null;
            var until = Environment.TickCount64 + 3000;
            do
            {
                Pump(100);
                why = null;
                var bars = Plan().Where(entry => entry.Stratum == Stratum.EditBar).Select(entry => entry.Window).ToHashSet();
                for (var screen = 0; screen < Screens.Length && why is null; screen++)
                {
                    var parts = Read("stage-edit.json").Where(spot => spot.Hit == "Deck" && Inside(Screens[screen], spot.Center)).ToList();
                    var done = parts.FirstOrDefault(spot => spot.Index == (int)PanelHitKind.Done);
                    if (done is null || !parts.Exists(spot => spot.Index == (int)PanelHitKind.Add))
                    {
                        why = $"screen {screen}'s edit bar shows {parts.Count} parts, no \"Add\" or \"Done\"";
                    }
                    else if (Under(done.Center) is var under && (!bars.Contains(under) || !IsWindowVisible(under)))
                    {
                        why = $"on screen {screen}, {Hex(under)} stands where its edit bar's \"Done\" is ({Text(done.Rect)}), not the edit bar";
                    }
                }
            }
            while (why is not null && Environment.TickCount64 < until);
            said.AppendLine($"{when}: {why ?? "both edit bars stand"}");
            Assert.True(why is null, $"{when}: {why}");
        }

        /// <summary>Button <paramref name="part"/> of screen <paramref name="screen"/>'s edit bar.</summary>
        private Spot DeckOn(int screen, PanelHitKind part) =>
            Read("stage-edit.json").FirstOrDefault(spot => spot.Hit == "Deck" && spot.Index == (int)part && Inside(Screens[screen], spot.Center))
            ?? throw new Xunit.Sdk.XunitException($"screen {screen}'s edit bar shows no {part}");

        /// <summary>§575: the thing of screen <paramref name="screen"/>'s edit bar whose stage key is <paramref name="key"/> ("Nav:Taskbar").</summary>
        private Spot DeckKeyOn(int screen, string key) =>
            Read("stage-edit.json").FirstOrDefault(spot => spot.Hit == "Deck" && spot.Key == key && Inside(Screens[screen], spot.Center))
            ?? throw new Xunit.Sdk.XunitException($"screen {screen}'s edit bar shows no {key}");

        /// <summary>
        /// §630: the edit bar dragged from its start, §575 an entry's flyout hung from it, a folder opened and closed, edit mode
        /// left and begun again: it never moves.
        /// </summary>
        public void RunStill()
        {
            Read("stage-map.json", wait: true);
            Pump(1500);
            EditMode();
            RECT Bar() => Read("stage-edit.json").First(spot => spot.Hit == "EditBar").Rect;
            var own = Bar();
            var parts = DeckArea();
            said.AppendLine($"the edit bar at {Text(own)}, its parts at {Text(parts)}");
            // Its start, where the grip stood: between the bar's edge and its first entry.
            var start = new POINT { x = (own.left + parts.left) / 2, y = (parts.top + parts.bottom) / 2 };
            Assert.True(start.x > own.left && start.x < parts.left, $"no room at the edit bar's start: {Text(own)}, {Text(parts)}");
            Drag(start, new POINT { x = start.x - 300, y = start.y + 250 });
            Assert.Equal(own, Bar());
            Assert.Equal(parts, DeckArea());

            // §575: Taskbar's flyout hangs from its entry: under it, §584 12 DIP off it, §578 centred on it.
            var entry = DeckKey("Nav:" + nameof(PanelPage.Taskbar));
            Click(entry.Center);
            Assert.True(Waited(() => Page() == "Taskbar"), $"the Taskbar entry opened no flyout: {Page()}");
            var page = Read("stage-edit.json").First(spot => spot.Hit == "Page").Rect;
            Assert.True(Math.Abs((page.left + page.right) - (entry.Rect.left + entry.Rect.right)) <= 2 && page.top > entry.Rect.bottom, $"the flyout at {Text(page)} does not hang from its entry at {Text(entry.Rect)}");
            CloseFlyout();

            // A folder opened and closed in the same edit mode: the bar where it stood.
            Click(Read("stage-map.json", wait: true).First(spot => spot.Key.EndsWith("/" + Folders[0], StringComparison.Ordinal)).Center);
            Assert.Equal(Folders[0], OpenFolderId(Folders[0]));
            Assert.Equal(own, Bar());
            Escape();
            Assert.Null(OpenFolderId(null));
            Assert.Equal(own, Bar());

            // Edit mode left and begun again: at its own spot.
            Escape();
            Assert.True(Shows(Stratum.EditBar, false), "Escape left the edit bar in sight");
            EditMode();
            Assert.Equal(own, Bar());
            Assert.True(dock.Running, "the dock ended");
        }

        /// <summary>
        /// §575: "Edit" from the bar's own menu on the first screen (not the main one), twice: edit mode with no flyout open each
        /// time (§537 to §574 its settings panel open); an entry of that screen's bar opens its flyout there; Escape closes it,
        /// edit mode staying; the next leaves.
        /// </summary>
        public void RunFromTheBarsMenu()
        {
            Read("stage-map.json", wait: true);
            Pump(1500);
            foreach (var (time, page) in new[] { (1, "Karpo"), (2, "Appearance") })
            {
                var when = time == 1 ? "edit mode from the bar's menu" : "edit mode from the bar's menu again";
                var bar = Read("stage-map.json", wait: true).Where(spot => Inside(Screens[0], spot.Center)).ToList();
                var room = Room(bar);
                RightClick(room);
                // A dock still settling may let a click go (InputReachesTheBarsTests): asked once more.
                if (!Waited(() => Plan().Any(entry => entry.Stratum == Stratum.Menu)))
                {
                    RightClick(room);
                }
                Assert.True(Waited(() => Plan().Any(entry => entry.Stratum == Stratum.Menu)), $"{when}: the bar's right click opened no menu");
                // "Edit", the bar's menu's first row.
                Keys(VK.VK_HOME, VK.VK_RETURN);
                Assert.True(Shows(Stratum.EditBar) && Shows(Stratum.Dim), $"{when}: no edit mode");
                Assert.True(Waited(() => Read("stage-edit.json").Any(spot => spot.Hit == "Deck" && spot.Key == "Nav:" + page && Inside(Screens[0], spot.Center))), $"{when}: the first screen's edit bar shows no {page} entry");
                Assert.True(Page() is null, $"{when}: a flyout opened with edit mode: {Page()}");
                Click(DeckKeyOn(0, "Nav:" + page).Center);
                Assert.True(Waited(() => Page() == page), $"{when}: the {page} entry opened no flyout: {Page() ?? "none"}");
                var open = Read("stage-edit.json").First(spot => spot.Hit == "Page");
                Assert.True(Inside(Screens[0], open.Center), $"{when}: the flyout opened at {Text(open.Rect)}, not on the screen whose entry was clicked");
                Assert.True(Read("stage-edit.json").Count(spot => spot.Hit == "Page") == 1, $"{when}: more than one flyout open");
                said.AppendLine($"{when}: the flyout open on {Page()} at {Text(open.Rect)}");
                // Escape closes the flyout first, edit mode staying; the next leaves.
                EscapeOn(0);
                Assert.True(Waited(() => Page() is null), "Escape left the flyout open");
                Assert.True(Shows(Stratum.EditBar) && Shows(Stratum.Dim), "the first Escape ended edit mode, a flyout open");
                EscapeOn(0);
                Assert.True(Shows(Stratum.EditBar, false) && Shows(Stratum.Dim, false), $"{when}: Escape, the flyout closed, did not end edit mode");
            }
            Assert.True(dock.Running, "the dock ended");
        }

        /// <summary>§394: a line's failed read on the edit bar, its row opening the line's settings.</summary>
        public void RunErrors()
        {
            var bar = Read("stage-map.json", wait: true);
            Pump(1500);
            EditMode();
            // The folder opened, so its feed is read; its failure the edit bar's error.
            bar = Read("stage-map.json", wait: true);
            Click(bar.First(spot => spot.Key.EndsWith("/" + Folders[0], StringComparison.Ordinal)).Center);
            var feed = $"/{Folders[0]}/{Folders[0]}-feed";
            var until = Environment.TickCount64 + 30_000;
            while (!Read("stage-edit.json").Any(spot => spot.Hit == "Fault" && spot.Key.EndsWith(feed, StringComparison.Ordinal)))
            {
                Assert.True(Environment.TickCount64 < until, "the feed's failed read never came to the edit bar");
                Pump(250);
            }
            var key = Read("stage-edit.json").First(spot => spot.Hit == "Fault").Key;
            said.AppendLine($"the error of {key}");
            Escape();
            Assert.Null(OpenFolderId(null));

            // The indicator, its list under it.
            var errors = Deck(PanelHitKind.Errors);
            Click(errors.Center);
            var list = Menu();
            Assert.True(list is not null, "the errors' indicator opened no list");
            Assert.True(list!.Rect.top >= errors.Rect.bottom - 2 && list.Rect.top <= errors.Rect.bottom + 40, $"the errors' list stands at {Text(list.Rect)}, not from the indicator at {Text(errors.Rect)}");
            // The pictures (KARPO_PNG_DIR, film-editbar-errors.png): the edit bar with its error, its list open.
            var deck = DeckArea();
            var film = new FrameFilm.Film("editbar-errors", new RECT { left = deck.left - 40, top = deck.top - 40, right = deck.right + 40, bottom = Math.Max(deck.bottom, list.Rect.bottom) + 40 });
            film.Before = FrameFilm.Snap(film.Area, Screen(), 0, "the list open");
            film.End = film.Before;
            film.Save();
            // Its row picked: the line's folder open, the line selected, §520 the settings panel open on its page.
            Keys(VK.VK_HOME, VK.VK_RETURN);
            Pump(1000);
            Assert.Equal(Folders[0], OpenFolderId(Folders[0]));
            Assert.True(Waited(() => Page() == "Widget"), $"the error's row did not open the line's page: {Page()}");
            Assert.True(dock.Running, "the dock ended");
        }

        /// <summary>§394: where the edit bar's buttons stand, all of them together.</summary>
        private RECT DeckArea() => Read("stage-edit.json").Where(spot => spot.Hit == "Deck").Select(spot => spot.Rect)
            .Aggregate((a, b) => new RECT { left = Math.Min(a.left, b.left), top = Math.Min(a.top, b.top), right = Math.Max(a.right, b.right), bottom = Math.Max(a.bottom, b.bottom) });


        /// <summary>
        /// <paramref name="film"/>'s frames of <paramref name="windows"/> until <paramref name="until"/> ms of
        /// <paramref name="clock"/>, as fast as they are taken, each <paramref name="note"/>.
        /// </summary>
        private static void Watch(FrameFilm.Film film, System.Diagnostics.Stopwatch clock, int until, string note, HWND[] windows)
        {
            while (clock.ElapsedMilliseconds < until)
            {
                Pump(1);
                film.Frames.Add(FrameFilm.Snap(film.Area, windows, clock.Elapsed.TotalMilliseconds, note));
            }
        }

        /// <summary>
        /// §397: each "+" of <paramref name="pluses"/> (screen px) changes once over <paramref name="film"/>: the
        /// before until a frame, the end from it on, never back between — no blink; and the before in every frame
        /// of a press held (§387).
        /// </summary>
        private void Once(FrameFilm.Film film, List<RECT> pluses, string when)
        {
            foreach (var (plus, index) in pluses.Select((plus, index) => (plus, index)))
            {
                Assert.True(Off(film, film.Before, film.End, plus) > 0.2, $"{when}: the \"+\" {index} at {Text(plus)} looks the same before and after");
                var states = film.Frames.Select(shot => Off(film, shot, film.Before, plus) <= Off(film, shot, film.End, plus) ? 'b' : 'e').ToList();
                var seen = new string([.. states]);
                var changes = Enumerable.Range(1, Math.Max(0, states.Count - 1)).Count(at => states[at] != states[at - 1]) + (states.FirstOrDefault() == 'e' ? 1 : 0);
                said.AppendLine($"{when}: the \"+\" {index} over {states.Count} frames (b before, e after): {seen}");
                Assert.True(changes <= 1 && states.LastOrDefault() == 'e', $"{when}: the \"+\" {index} blinks over the frames (b before, e after): {seen}");
                Assert.True(film.Frames.Select((shot, at) => (shot.Note, at)).Where(frame => frame.Note == "held").All(frame => states[frame.at] == 'b'), $"{when}: held down, the \"+\" {index} goes: {seen}");
            }
        }

        /// <summary>The share of <paramref name="plus"/> (screen px) that differs between <paramref name="a"/> and <paramref name="b"/> of <paramref name="film"/>.</summary>
        private static double Off(FrameFilm.Film film, FrameFilm.Shot a, FrameFilm.Shot b, RECT plus)
        {
            var (differ, all) = (0, 0);
            for (var y = Math.Max(plus.top, film.Area.top); y < Math.Min(plus.bottom, film.Area.bottom); y++)
            {
                for (var x = Math.Max(plus.left, film.Area.left); x < Math.Min(plus.right, film.Area.right); x++)
                {
                    var at = (((y - film.Area.top) * film.Width) + x - film.Area.left) * 4;
                    all++;
                    differ += Math.Abs(a.Pixels[at] - b.Pixels[at]) > 24 || Math.Abs(a.Pixels[at + 1] - b.Pixels[at + 1]) > 24 || Math.Abs(a.Pixels[at + 2] - b.Pixels[at + 2]) > 24 ? 1 : 0;
                }
            }
            return all == 0 ? 0 : differ / (double)all;
        }

        /// <summary>§387: each bar's "+" in sight, by its bar and where it stands on the screen.</summary>
        private List<string> BarAdds() => [.. Read("stage-edit.json").Where(spot => spot.Hit == "BarAdd").Select(spot => $"{spot.Key} {spot.Index} {Text(spot.Rect)}")];

        /// <summary>
        /// The middle of the widest room between two entries along the main bar (stage-map.json): none of its
        /// widgets, nor a section's "+", but the bar's own window there.
        /// </summary>
        private static POINT Room(List<Spot> bar)
        {
            var row = bar.GroupBy(spot => spot.Rect.top / 8).OrderByDescending(group => group.Count()).First().OrderBy(spot => spot.Rect.left).ToList();
            var (gap, at) = Enumerable.Range(1, row.Count - 1).Select(index => (row[index].Rect.left - row[index - 1].Rect.right, index)).Max();
            Assert.True(gap > 40, $"no room on the bar between its entries (the widest {gap} px)");
            var point = new POINT { x = (row[at - 1].Rect.right + row[at].Rect.left) / 2, y = (row[at].Rect.top + row[at].Rect.bottom) / 2 };
            Assert.All(bar, spot => Assert.False(Inside(spot.Rect, point), $"{point.x},{point.y} is on {spot.Key}"));
            return point;
        }

        /// <summary>
        /// Whether the plan the dock last applied comes to have a window of <paramref name="stratum"/> in sight
        /// (<paramref name="want"/>) or none: waited for, as the plan lands at the commit's fence; false where it
        /// never does.
        /// </summary>
        private bool Shows(Stratum stratum, bool want = true)
        {
            var until = Environment.TickCount64 + 3000;
            do
            {
                Pump(100);
                if (Plan().Any(entry => entry.Stratum == stratum && IsWindowVisible(entry.Window)) == want)
                {
                    return true;
                }
            }
            while (Environment.TickCount64 < until);
            return false;
        }

        /// <summary>
        /// What must hold after <paramref name="when"/>: the plan stands every dim under the arranged folder
        /// (<paramref name="folder"/>: one is open; false: none; null: either), the folder under every bar and
        /// edit bar; and Windows stands the dock's windows in the plan's order. Waited for a moment, as the
        /// plan lands at the commit's fence; what still does not hold then is wrong.
        /// </summary>
        private void Check(string when, bool? folder)
        {
            checks++;
            string? why = null;
            var until = Environment.TickCount64 + 3000;
            do
            {
                Pump(100);
                var plan = Plan().Where(entry => IsWindowVisible(entry.Window)).ToList();
                var stratum = plan.ToDictionary(entry => entry.Window, entry => entry.Stratum);
                var screen = Screen().Where(stratum.ContainsKey).ToList();
                var arranged = plan.Where(entry => entry.Stratum == Stratum.ArrangedFolder).Select(entry => entry.Window).ToList();
                why = null;
                if (!plan.Any(entry => entry.Stratum == Stratum.Dim) || !plan.Any(entry => entry.Stratum == Stratum.Bar) || !plan.Any(entry => entry.Stratum == Stratum.EditBar))
                {
                    why = "the plan has no dim, bar or edit bar in sight";
                }
                else if (folder is { } open && open != (arranged.Count == 1))
                {
                    why = open ? $"{arranged.Count} arranged folders in sight, not one" : "an arranged folder in sight";
                }
                else if (Enumerable.Range(1, plan.Count - 1).FirstOrDefault(at => plan[at].Stratum > plan[at - 1].Stratum) is var badPlan and > 0)
                {
                    why = $"the plan stands {plan[badPlan].Stratum} over {plan[badPlan - 1].Stratum}";
                }
                else if (Enumerable.Range(1, Math.Max(0, screen.Count - 1)).FirstOrDefault(at => stratum[screen[at]] > stratum[screen[at - 1]]) is var badScreen and > 0)
                {
                    why = $"Windows stands {stratum[screen[badScreen]]} {Hex(screen[badScreen])} over {stratum[screen[badScreen - 1]]} {Hex(screen[badScreen - 1])}";
                }
                else if (!screen.SequenceEqual(plan.Select(entry => entry.Window)))
                {
                    why = "Windows' order is not the plan's";
                }
                if (why is null)
                {
                    break;
                }
            }
            while (Environment.TickCount64 < until);
            var line = new StringBuilder($"{when}: {why ?? "holds"}\n  plan: ");
            line.AppendJoin(", ", Plan().Where(entry => IsWindowVisible(entry.Window)).Select(entry => $"{entry.Stratum} {Hex(entry.Window)}"));
            line.Append("\n  screen: ").AppendJoin(", ", Screen().Select(Hex));
            said.AppendLine(line.ToString());
            if (why is not null)
            {
                wrong.Add(line.ToString());
            }
        }

        // --- Doing --------------------------------------------------------------------------------------

        /// <summary>A press and its release at <paramref name="at"/>, posted to the dock's window there, as the mouse would; then a moment.</summary>
        private static void Click(POINT at)
        {
            var window = Under(at);
            Post(window, WM.WM_MOUSEMOVE, at, 0);
            Post(window, WM.WM_LBUTTONDOWN, at, 0x0001);
            Post(window, WM.WM_LBUTTONUP, at, 0);
            Pump(1000);
        }

        /// <summary>
        /// A click at <paramref name="at"/> as the mouse makes it (MenuToggleTests.Press): the popups' hook hears the press
        /// first — sent to the dock's sink as the hook posts it (PressHook.StagedBit) —, then the window under the pointer
        /// its press and release; then a moment.
        /// </summary>
        private void HookedClick(POINT at)
        {
            var window = Under(at);
            if (Sink() is { } sink)
            {
                nuint result;
                _ = SendMessageTimeoutW(sink, PressHook.WM_KARPO_PRESS, (WPARAM)(nuint)(WM.WM_LBUTTONDOWN | PressHook.StagedBit), EditFollowHook.Pack(at), 0 /* SMTO_NORMAL */, 10_000, &result);
            }
            Post(window, WM.WM_MOUSEMOVE, at, 0);
            Post(window, WM.WM_LBUTTONDOWN, at, 0x0001);
            Post(window, WM.WM_MOUSEMOVE, at, 0x0001);
            Post(window, WM.WM_LBUTTONUP, at, 0);
            Pump(1000);
        }

        /// <summary>The dock's sink for the hook's presses (PressHook), once a popup made it; null before.</summary>
        private HWND? Sink()
        {
            var at = HWND.NULL;
            fixed (char* name = "karpo.presses")
            {
                while ((at = FindWindowExW(HWND.HWND_MESSAGE, at, name, null)) != HWND.NULL)
                {
                    uint pid = 0;
                    _ = GetWindowThreadProcessId(at, &pid);
                    if (pid == dock.Pid)
                    {
                        return at;
                    }
                }
            }
            return null;
        }

        private static void RightClick(POINT at)
        {
            var window = Under(at);
            Post(window, WM.WM_MOUSEMOVE, at, 0);
            Post(window, WM.WM_RBUTTONDOWN, at, 0x0002);
            Post(window, WM.WM_RBUTTONUP, at, 0);
            Pump(1000);
        }

        /// <summary>A press at <paramref name="from"/>, the pointer moved to <paramref name="to"/> in steps, the button held, and let go there.</summary>
        private static void Drag(POINT from, POINT to)
        {
            var window = Under(from);
            Post(window, WM.WM_MOUSEMOVE, from, 0);
            Post(window, WM.WM_LBUTTONDOWN, from, 0x0001);
            Pump(50);
            for (var step = 1; step <= 10; step++)
            {
                Post(window, WM.WM_MOUSEMOVE, new POINT { x = from.x + ((to.x - from.x) * step / 10), y = from.y + ((to.y - from.y) * step / 10) }, 0x0001);
                Pump(30);
            }
            Post(window, WM.WM_LBUTTONUP, to, 0);
            Pump(1000);
        }

        /// <summary>Keys pressed on the topmost menu, one by one.</summary>
        private void Keys(params int[] keys)
        {
            foreach (var key in keys)
            {
                if (Plan().FirstOrDefault(entry => entry.Stratum == Stratum.Menu) is { Window: var menu } && menu != HWND.NULL)
                {
                    _ = PostMessageW(menu, WM.WM_KEYDOWN, (WPARAM)(nuint)key, 0);
                }
                Pump(250);
            }
            Pump(800);
        }

        /// <summary>Escape on every menu open.</summary>
        private void EscapeMenus()
        {
            foreach (var (menu, _) in Plan().Where(entry => entry.Stratum == Stratum.Menu))
            {
                _ = PostMessageW(menu, WM.WM_KEYDOWN, VK.VK_ESCAPE, 0);
            }
            Pump(800);
        }

        /// <summary>
        /// Edit mode, as a second start asks for it (§575: the Karpo entry's flyout open), the flyout closed by its entry — each
        /// waited for: on a machine the run starves.
        /// </summary>
        private void EditMode()
        {
            Assert.True(Host() is { } host && PostMessageW(host, SecondStart.Message, 0, 0), "no dock host");
            Assert.True(Shows(Stratum.EditBar), "a second start opened no edit mode");
            Assert.True(Waited(() => Page() == "Karpo"), $"a second start opened no flyout of the Karpo entry: {Page()}");
            CloseFlyout();
        }

        /// <summary>§575: the flyout open closed by its entry clicked again (Add's by Add); nothing where none is open.</summary>
        private void CloseFlyout()
        {
            if (Page() is not { } open)
            {
                return;
            }
            Click(DeckKey(open == nameof(PanelPage.Add) ? "Add:" : "Nav:" + open).Center);
            Assert.True(Waited(() => Page() is null), $"its entry left the {open} flyout open");
        }

        /// <summary>
        /// Escape where edit mode has the keys: the arranged folder, else the bars. §575: a flyout open (a folder opened opens
        /// its own) takes the first Escape, from the inside out: it is closed by one first, the Escape asked for the next.
        /// </summary>
        private void Escape()
        {
            if (Page() is not null)
            {
                var keys = Plan().FirstOrDefault(entry => entry.Stratum is Stratum.ArrangedFolder).Window is var folder && folder != HWND.NULL ? folder
                    : Plan().First(entry => entry.Stratum == Stratum.Bar).Window;
                _ = PostMessageW(keys, WM.WM_KEYDOWN, VK.VK_ESCAPE, 0);
                Assert.True(Waited(() => Page() is null), $"Escape left the {Page()} flyout open");
            }
            var to = Plan().Where(entry => entry.Stratum is Stratum.ArrangedFolder).Select(entry => entry.Window).ToList();
            foreach (var window in to.Count > 0 ? to : [.. Plan().Where(entry => entry.Stratum == Stratum.Bar).Select(entry => entry.Window)])
            {
                _ = PostMessageW(window, WM.WM_KEYDOWN, VK.VK_ESCAPE, 0);
            }
            Pump(1000);
        }

        /// <summary>§537: one Escape, on screen <paramref name="screen"/>'s bar alone (<see cref="Escape"/> gives one to every bar).</summary>
        private void EscapeOn(int screen)
        {
            foreach (var window in Plan().Where(entry => entry.Stratum == Stratum.Bar).Select(entry => entry.Window))
            {
                RECT rect;
                _ = GetWindowRect(window, &rect);
                if (Inside(Screens[screen], new POINT { x = (rect.left + rect.right) / 2, y = (rect.top + rect.bottom) / 2 }))
                {
                    _ = PostMessageW(window, WM.WM_KEYDOWN, VK.VK_ESCAPE, 0);
                    break;
                }
            }
            Pump(1000);
        }

        private static void Post(HWND window, uint message, POINT screen, uint keys)
        {
            RECT rect;
            _ = GetWindowRect(window, &rect);
            var (x, y) = (screen.x - rect.left, screen.y - rect.top);
            _ = PostMessageW(window, message, keys, (LPARAM)(nint)((y << 16) | (x & 0xFFFF)));
        }

        // --- Reading ------------------------------------------------------------------------------------

        /// <summary>The window at <paramref name="at"/>, as Windows finds it for the pointer.</summary>
        private static HWND Under(POINT at) => GetAncestor(WindowFromPoint(at), GA.GA_ROOT);

        /// <summary>The arranged folder's lines, in their order.</summary>
        private List<Spot> Lines() => [.. Read("stage-edit.json").Where(spot => spot.Hit == "Line").OrderBy(spot => spot.Index)];

        /// <summary>§520: the page the settings panel shows; null while it is folded.</summary>
        private string? Page() => Read("stage-edit.json").FirstOrDefault(spot => spot.Hit == "Page")?.Key;

        /// <summary>§520: the edit bar's or its panel's thing whose stage key is <paramref name="key"/> ("Nav:Appearance", "Pop:Appearance/Palette").</summary>
        private Spot DeckKey(string key)
        {
            var until = Environment.TickCount64 + 10_000;
            while (true)
            {
                if (Read("stage-edit.json").FirstOrDefault(spot => spot.Hit == "Deck" && spot.Key == key) is { } found)
                {
                    return found;
                }
                Assert.True(Environment.TickCount64 < until, $"the settings panel shows no {key}");
                Pump(250);
            }
        }

        /// <summary>
        /// §549: the flyout's thing whose stage key is <paramref name="key"/>, the page wheeled down a notch at a time till it
        /// stands in the flyout, a card's height off either end, as PanelPopupPlaceTests.WheelTo.
        /// </summary>
        private Spot WheelToDeck(string key)
        {
            _ = DeckKey(key);
            for (var notch = 0; notch < 20; notch++)
            {
                var spots = Read("stage-edit.json");
                var page = spots.First(spot => spot.Hit == "Page").Rect;
                var (top, bottom) = (page.top + 8, page.bottom - 8);
                if (spots.FirstOrDefault(spot => spot.Hit == "Deck" && spot.Key == key && spot.Rect.top >= top && spot.Rect.bottom <= bottom) is { } shown)
                {
                    return shown;
                }
                var at = new POINT { x = (page.left + page.right) / 2, y = (page.top + page.bottom) / 2 };
                var flyout = Under(at);
                _ = PostMessageW(flyout, WM.WM_MOUSEWHEEL, (WPARAM)(nuint)unchecked((uint)(-120 << 16)), (LPARAM)(nint)((at.y << 16) | (at.x & 0xFFFF)));
                Pump(800);
            }
            throw new Xunit.Sdk.XunitException($"the settings panel never brought {key} into sight");
        }

        /// <summary>The edit bar's button <paramref name="part"/>.</summary>
        private Spot Deck(PanelHitKind part)
        {
            // A loaded machine may write the map a while after the step that changed it.
            var until = Environment.TickCount64 + 10_000;
            while (true)
            {
                if (Read("stage-edit.json").FirstOrDefault(spot => spot.Hit == "Deck" && spot.Index == (int)part) is { } found)
                {
                    return found;
                }
                Assert.True(Environment.TickCount64 < until, $"the edit bar shows no part {part}");
                Pump(250);
            }
        }

        /// <summary>The key of the folder open arranged, or null.</summary>
        private string? OpenFolderKey() => Read("stage-edit.json").FirstOrDefault(spot => spot.Hit == "Folder")?.Key;

        /// <summary>
        /// The id of the folder open arranged (null for none) once it is <paramref name="expected"/>, waited for as
        /// <see cref="Shows"/> waits (<see cref="Waited"/>): a click's moment (Pump) is not always the dock's — on a machine
        /// the run starves, the stage's files come a while after the clicks (a double-click asserted open read none, the
        /// frames filmed four a second). What it is after 3 s otherwise.
        /// </summary>
        private string? OpenFolderId(string? expected)
        {
            string? open = null;
            _ = Waited(() => (open = OpenFolderKey()?.Split('/')[^1]) == expected);
            return open;
        }

        /// <summary>Whether <paramref name="done"/> comes to hold within 3 s, pumped meanwhile; a file read as it is written tried again.</summary>
        private static bool Waited(Func<bool> done)
        {
            var until = Environment.TickCount64 + 3000;
            while (true)
            {
                try
                {
                    if (done())
                    {
                        return true;
                    }
                }
                catch (Exception error) when (error is IOException or JsonException)
                {
                }
                if (Environment.TickCount64 >= until)
                {
                    return false;
                }
                Pump(100);
            }
        }

        /// <summary>The first layout as the file has it now.</summary>
        private JsonNode Layout() => JsonNode.Parse(File.ReadAllText(Path.Combine(dock.Folder, "config.json")))!["layouts"]![0]!;

        /// <summary>§567: Karpo's one Appearance as the file has it now; null while nothing of it is set.</summary>
        private JsonNode? Appearance() => JsonNode.Parse(File.ReadAllText(Path.Combine(dock.Folder, "config.json")))!["appearance"];

        /// <summary>The plan the dock last applied, top first, each window with its stratum.</summary>
        private List<(HWND Window, Stratum Stratum)> Plan()
        {
            for (var tries = 0; ; tries++)
            {
                try
                {
                    using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(dock.Folder, "stage-z.json")));
                    return [.. json.RootElement.EnumerateArray().Select(entry => ((HWND)(nint)entry.GetProperty("hwnd").GetInt64(), Enum.Parse<Stratum>(entry.GetProperty("stratum").GetString()!)))];
                }
                catch (Exception error) when (error is IOException or JsonException && tries < 20)
                {
                    Pump(50);
                }
            }
        }

        /// <summary>The dock's windows in sight as Windows stands them, top first.</summary>
        private List<HWND> Screen()
        {
            var found = new List<HWND>();
            for (var at = GetTopWindow(HWND.NULL); at != HWND.NULL; at = GetWindow(at, GW_HWNDNEXT))
            {
                uint pid = 0;
                _ = GetWindowThreadProcessId(at, &pid);
                if (pid == dock.Pid && IsWindowVisible(at))
                {
                    found.Add(at);
                }
            }
            return found;
        }

        /// <summary>The spots of <paramref name="name"/> in the dock's folder; <paramref name="wait"/>: until it has some.</summary>
        private List<Spot> Read(string name, bool wait = false)
        {
            var until = Environment.TickCount64 + 60_000;
            while (true)
            {
                Assert.True(dock.Running, "the dock ended");
                try
                {
                    var path = Path.Combine(dock.Folder, name);
                    if (File.Exists(path))
                    {
                        using var json = JsonDocument.Parse(File.ReadAllText(path));
                        var spots = json.RootElement.EnumerateArray().Select(entry => new Spot(
                            entry.GetProperty("hit").GetString()!,
                            entry.GetProperty("index").GetInt32(),
                            entry.GetProperty("key").GetString()!,
                            new RECT
                            {
                                left = entry.GetProperty("x").GetInt32(),
                                top = entry.GetProperty("y").GetInt32(),
                                right = entry.GetProperty("x").GetInt32() + entry.GetProperty("w").GetInt32(),
                                bottom = entry.GetProperty("y").GetInt32() + entry.GetProperty("h").GetInt32(),
                            },
                            entry.TryGetProperty("kind", out var kind) ? kind.GetString() ?? "" : "")).ToList();
                        if (spots.Count > 0 || !wait)
                        {
                            return spots;
                        }
                    }
                    else if (!wait)
                    {
                        return [];
                    }
                }
                catch (Exception error) when (error is IOException or JsonException)
                {
                    // Being written.
                }
                Assert.True(Environment.TickCount64 < until, $"the dock never wrote {name}");
                Pump(100);
            }
        }

        private HWND? Host()
        {
            var at = HWND.NULL;
            fixed (char* name = DockHost.ClassName)
            {
                while ((at = FindWindowExW(HWND.NULL, at, name, null)) != HWND.NULL)
                {
                    uint pid = 0;
                    _ = GetWindowThreadProcessId(at, &pid);
                    if (pid == dock.Pid)
                    {
                        return at;
                    }
                }
            }
            return null;
        }

        private static bool Inside(RECT rect, POINT at) => at.x >= rect.left && at.x < rect.right && at.y >= rect.top && at.y < rect.bottom;

        private static string Hex(HWND window) => $"{(nint)window:X}";

        private static string Text(RECT r) => $"{r.left},{r.top}..{r.right},{r.bottom}";

        /// <summary>Waits <paramref name="ms"/>, this thread's messages handled meanwhile.</summary>
        private static void Pump(int ms)
        {
            var until = Environment.TickCount64 + ms;
            while (Environment.TickCount64 < until)
            {
                _ = MsgWaitForMultipleObjects(0, null, BOOL.FALSE, (uint)Math.Max(1, until - Environment.TickCount64), QS.QS_ALLINPUT);
                MSG msg;
                while (PeekMessageW(&msg, HWND.NULL, 0, 0, PM.PM_REMOVE))
                {
                    TranslateMessage(&msg);
                    DispatchMessageW(&msg);
                }
            }
        }
    }
}
