// The pen menu and the virtual touchpad show where Windows would show them — on a PC with a pen, on
// a touch screen — or always, where the person chose that, as Windows' "System tray icons" settings
// let them (InputButtons). The touch keyboard has no such option and always shows (2026-10-08). All
// of it is held on made-up PCs, so nothing here depends on the machine the tests run on.
//
// And Windows' buttons after Start, search and task view — the widgets board and these
// three — as the registry and the library have them: taskbar entries alone, where Windows keeps
// them, on the Taskbar page in the groups a person looks in.

namespace Karpo.Tests.Core;

public sealed class InputButtonsTests
{
    /// <summary>A desktop: no pen, no touch screen, a keyboard.</summary>
    private static readonly InputHardware Desktop = new(Pen: false, Touch: false, Slate: false);

    /// <summary>A touch laptop with its keyboard on.</summary>
    private static readonly InputHardware TouchLaptop = new(Pen: false, Touch: true, Slate: false);

    /// <summary>A pen tablet with its keyboard taken off.</summary>
    private static readonly InputHardware PenTablet = new(Pen: true, Touch: true, Slate: true);

    // --- When each shows ------------------------------------------------------------------------

    [Theory]
    // The pen menu: where there is a pen, whatever else the PC has.
    [InlineData(BarKind.PenMenu, false, false, false, false)]
    [InlineData(BarKind.PenMenu, false, true, true, false)]
    [InlineData(BarKind.PenMenu, true, false, false, true)]
    [InlineData(BarKind.PenMenu, true, true, true, true)]
    // The touch keyboard: on every PC, a keyboard attached or not, touch screen or not.
    [InlineData(BarKind.TouchKeyboard, false, false, false, true)]
    [InlineData(BarKind.TouchKeyboard, false, false, true, true)]
    [InlineData(BarKind.TouchKeyboard, false, true, false, true)]
    [InlineData(BarKind.TouchKeyboard, false, true, true, true)]
    // The virtual touchpad: on a touch screen, keyboard or not.
    [InlineData(BarKind.VirtualTouchpad, false, false, false, false)]
    [InlineData(BarKind.VirtualTouchpad, true, false, true, false)]
    [InlineData(BarKind.VirtualTouchpad, false, true, false, true)]
    [InlineData(BarKind.VirtualTouchpad, false, true, true, true)]
    public void AutomaticShowsWhereWindowsWould(BarKind kind, bool pen, bool touch, bool slate, bool shows) =>
        Assert.Equal(shows, InputButtons.Shows(kind, InputButtonShow.Automatic, new InputHardware(pen, touch, slate)));

    /// <summary>Always is the person's choice over the rule: every PC shows it, whatever it has.</summary>
    [Fact]
    public void AlwaysShowsOnEveryPC()
    {
        foreach (var kind in InputButtons.Kinds)
        {
            foreach (var here in new[] { Desktop, TouchLaptop, PenTablet })
            {
                Assert.True(InputButtons.Shows(kind, InputButtonShow.Always, here), $"{kind} on {here}");
            }
        }
    }

    /// <summary>The rules are the three buttons' alone: everything else shows on a PC with nothing.</summary>
    [Theory]
    [InlineData(BarKind.Search)]
    [InlineData(BarKind.WidgetsBoard)]
    [InlineData(BarKind.NightLight)]
    public void EveryOtherKindShowsWhateverThePCHas(BarKind kind)
    {
        Assert.False(InputButtons.Is(kind));
        Assert.True(InputButtons.Shows(kind, InputButtonShow.Automatic, Desktop));
    }

    /// <summary>
    /// "touch keyboard when it shows option should not be configurable. it should always show"
    /// (2026-10-08): the touch keyboard has no option, shows on every PC as it is, and its page says
    /// nothing about when it shows; the pen menu and the touchpad keep theirs.
    /// </summary>
    [Fact]
    public void TheTouchKeyboardAlwaysShowsAndHasNoOption()
    {
        Assert.False(InputButtons.Decides(BarKind.TouchKeyboard));
        Assert.True(InputButtons.Decides(BarKind.PenMenu) && InputButtons.Decides(BarKind.VirtualTouchpad));
        Assert.Equal(WidgetOptionsKind.None, BarEntries.Of(BarKind.TouchKeyboard).Info.Options);
        Assert.Equal(WidgetOptionsKind.InputButton, BarEntries.Of(BarKind.PenMenu).Info.Options);
        Assert.Equal(WidgetOptionsKind.InputButton, BarEntries.Of(BarKind.VirtualTouchpad).Info.Options);

        var keyboard = BarKind.TouchKeyboard.New();
        foreach (var here in new[] { Desktop, TouchLaptop, PenTablet })
        {
            Assert.True(InputButtons.Shows(keyboard, here), $"the touch keyboard on {here}");
        }
        Assert.Equal("", InputButtons.Rule(BarKind.TouchKeyboard));
        Assert.Equal("", InputButtons.Says(BarKind.TouchKeyboard, InputButtonShow.Automatic, TouchLaptop));
    }

    /// <summary>
    /// A file from before, which kept "when it shows" for its touch keyboard, reads without a word:
    /// the touch keyboard's choice is dropped and it shows, Always or Automatic; the pen menu's is kept.
    /// </summary>
    [Fact]
    public void AnOlderFilesTouchKeyboardChoiceIsDroppedQuietly()
    {
        var config = Configs.Repaired("""
            { "layouts": [ { "id": "default", "name": "Default", "islands": [
              { "id": "tk", "kind": "touchKeyboard", "section": "end", "inputButton": { "show": "automatic" } },
              { "id": "tk2", "kind": "touchKeyboard", "section": "end", "inputButton": { "show": "always" } },
              { "id": "pen", "kind": "penMenu", "section": "end", "inputButton": { "show": "always" } } ] } ] }
            """);

        var islands = Configs.Islands(config);
        var keyboards = islands.Where(island => island.Kind == BarKind.TouchKeyboard).ToList();
        Assert.Equal(2, keyboards.Count);
        Assert.All(keyboards, keyboard =>
        {
            Assert.Null(keyboard.InputButton);
            Assert.True(InputButtons.Shows(keyboard, Desktop));
        });
        Assert.Equal(InputButtonShow.Always, islands.Single(island => island.Kind == BarKind.PenMenu).InputButton?.Show);
        // Written back, only the pen menu's is in the file.
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(config.ToJson(), "\"inputButton\""));
    }

    /// <summary>An entry shows by its own option: none is Automatic, and Always is written where chosen.</summary>
    [Fact]
    public void AnEntryShowsByItsOwnOption()
    {
        var pen = BarKind.PenMenu.New();
        Assert.Null(pen.InputButton);
        Assert.False(InputButtons.Shows(pen, Desktop));
        Assert.True(InputButtons.Shows(pen, PenTablet));

        pen.InputButton = new InputButtonOptions { Show = InputButtonShow.Always };
        Assert.True(InputButtons.Shows(pen, Desktop));
    }

    // --- In the file ----------------------------------------------------------------------------

    /// <summary>
    /// Automatic is the absence of a choice, so it is never written; Always is, and reads back; and
    /// only the pen menu and the touchpad carry the option at all.
    /// </summary>
    [Fact]
    public void AutomaticIsNoOptionsAndAlwaysIsKept()
    {
        var automatic = BarKind.PenMenu.New();
        automatic.InputButton = new InputButtonOptions { Show = InputButtonShow.Automatic };
        automatic.Normalize();
        Assert.Null(automatic.InputButton);

        var always = BarKind.VirtualTouchpad.New();
        always.InputButton = new InputButtonOptions { Show = InputButtonShow.Always };
        always.Normalize();
        Assert.Equal(InputButtonShow.Always, always.InputButton?.Show);

        var config = new Config();
        config.Layouts.Add(new TaskbarLayout { Id = "a", Name = "A", Islands = [always, automatic] });
        var json = System.Text.Json.JsonSerializer.Serialize(config, ConfigJson.Default.Config);
        Assert.Contains("\"inputButton\"", json, StringComparison.Ordinal);
        Assert.Contains("\"always\"", json, StringComparison.Ordinal);
        var read = System.Text.Json.JsonSerializer.Deserialize(json, ConfigJson.Default.Config)!;
        var islands = read.Layouts.Single().Islands;
        Assert.Equal(InputButtonShow.Always, islands[0].InputButton?.Show);
        Assert.Null(islands[1].InputButton);

        // A kind that is not one of the three drops the option where the file is read.
        var search = BarKind.Search.New();
        search.InputButton = new InputButtonOptions { Show = InputButtonShow.Always };
        search.Normalize();
        Assert.Null(search.InputButton);
    }

    // --- A new bar ------------------------------------------------------------------------------

    /// <summary>
    /// A new bar has what Windows would show on the PC it is made on, beside the tray's icons and
    /// before the input indicator and Wi-Fi: nothing on a desktop, the touch keyboard on a touch laptop, the pen menu and the
    /// touch keyboard on a pen tablet — never the virtual touchpad, which Windows leaves off — and
    /// each as Windows decides, which is no options at all.
    /// </summary>
    [Fact]
    public void ANewBarHasWhatWindowsWouldShowBesideTheTray()
    {
        Assert.DoesNotContain(Sections.DefaultLayout([], Desktop), island => InputButtons.Is(island.Kind));

        Assert.Equal([BarKind.TouchKeyboard], Inputs(Sections.DefaultLayout([], TouchLaptop)));

        var tablet = Sections.DefaultLayout([], PenTablet);
        Assert.Equal([BarKind.PenMenu, BarKind.TouchKeyboard], Inputs(tablet));
        var tray = tablet.FindIndex(island => island.Kind == BarKind.Tray);
        // Right after the tray.
        Assert.Equal(BarKind.PenMenu, tablet[tray + 1].Kind);
        Assert.Equal(BarKind.TouchKeyboard, tablet[tray + 2].Kind);
        // Then the status icons and the keyboard, as Windows has them (2026-10-13).
        Assert.Equal(BarKind.Indicators, tablet[tray + 3].Kind);
        Assert.Equal(FolderFaces.KeyboardId, tablet[tray + 4].Folder?.Face.Kind);
        Assert.All(tablet.Where(island => InputButtons.Is(island.Kind)), island => Assert.Null(island.InputButton));

        static BarKind[] Inputs(List<BarItem> bar) => [.. bar.Select(island => island.Kind).Where(InputButtons.Is)];
    }

    /// <summary>The page says the rule and whether this PC meets it now; Always says so.</summary>
    [Fact]
    public void TheWordsSayTheRuleAndWhetherThisPCMeetsIt()
    {
        Assert.Contains("no pen", InputButtons.Says(BarKind.PenMenu, InputButtonShow.Automatic, Desktop), StringComparison.Ordinal);
        Assert.Contains("has a pen", InputButtons.Says(BarKind.PenMenu, InputButtonShow.Automatic, PenTablet), StringComparison.Ordinal);
        Assert.Contains("no touch screen", InputButtons.Says(BarKind.VirtualTouchpad, InputButtonShow.Automatic, Desktop), StringComparison.Ordinal);
        Assert.Equal("On every PC, whatever it has", InputButtons.Says(BarKind.VirtualTouchpad, InputButtonShow.Always, Desktop));
        Assert.Equal("", InputButtons.Says(BarKind.Search, InputButtonShow.Automatic, Desktop));
        Assert.All(InputButtons.Kinds.Where(InputButtons.Decides), kind => Assert.StartsWith(InputButtons.Rule(kind), InputButtons.Says(kind, InputButtonShow.Automatic, Desktop), StringComparison.Ordinal));
    }

    // --- The registry and the library -----------------------------------------------------------

    /// <summary>
    /// Windows' three input buttons after Start, search and task view: taskbar entries alone, one cell
    /// each, drawn by the bar, each opening what Windows' own does: its popup. (§474: the widgets board
    /// wears Windows' Widgets button's face, <see cref="WindowsFaces"/>.)
    /// </summary>
    [Theory]
    [InlineData(BarKind.PenMenu, "penMenu", "Pen menu", BarClick.Panel, WidgetGroup.End)]
    [InlineData(BarKind.TouchKeyboard, "touchKeyboard", "Touch keyboard", BarClick.Panel, WidgetGroup.End)]
    [InlineData(BarKind.VirtualTouchpad, "virtualTouchpad", "Virtual touchpad", BarClick.Panel, WidgetGroup.End)]
    public void EachIsAnEntryOfTheTaskbarsOwn(BarKind kind, string id, string title, BarClick click, WidgetGroup run)
    {
        var entry = BarEntries.Of(kind);
        Assert.Equal(id, entry.Info.Id);
        Assert.Equal(title, entry.Info.Title);
        Assert.Equal(click, entry.Click);
        Assert.Equal(run, entry.Run);
        Assert.Equal(WidgetSource.None, entry.Drawing);
        Assert.Null(entry.Opens);
        Assert.Equal((1, 1), (entry.Size.MinCells, entry.Size.MaxCells));
        Assert.True(kind.IsWindowsButton());
        Assert.False(kind.IsIsland());
        Assert.Equal(kind, Configs.Kind(id));
        Assert.Null(ListWidgets.Find(id));
        Assert.NotEmpty(entry.Info.Glyph);
    }

    /// <summary>Only these four are Windows' buttons of the kind the bar draws as one glyph; Start, search and task view keep their own.</summary>
    [Fact]
    public void TheWindowsButtonsAreTheseFour()
    {
        // And the two shortcuts to Windows' apps drawn as they are (the backlog, items 10 and 18).
        Assert.Equal(
            [
                BarKind.SnippingTool, BarKind.WindowsSettings,
                BarKind.PenMenu, BarKind.TouchKeyboard, BarKind.VirtualTouchpad,
                // §474, §490: not the Widgets button nor Windows' menus (§466), which wear faces.
            ],
            BarEntries.All.Select(entry => entry.Kind).Where(kind => kind.IsWindowsButton()));
        Assert.Equal(InputButtons.Kinds, BarEntries.All.Select(entry => entry.Kind).Where(InputButtons.Is));
    }

    /// <summary>
    /// They stand where Windows keeps them: the widgets board first, where Windows keeps its button
    /// (a new bar has neither it nor the weather); the three input buttons after the tray's icons and before the
    /// quick settings (the volume slider the first of them since the network, Bluetooth and the sound
    /// went to their presets' folders).
    /// </summary>
    [Fact]
    public void EachStandsWhereWindowsKeepsIt()
    {
        var bar = BarEntries.All.Select(entry => entry.Kind).ToList();
        Assert.Equal(0, bar.IndexOf(BarKind.WidgetsBoard));
        Assert.Equal(bar.IndexOf(BarKind.Tray) + 1, bar.IndexOf(BarKind.PenMenu));
        Assert.Equal(bar.IndexOf(BarKind.Tray) + 2, bar.IndexOf(BarKind.TouchKeyboard));
        Assert.Equal(bar.IndexOf(BarKind.Tray) + 3, bar.IndexOf(BarKind.VirtualTouchpad));
        Assert.Equal(bar.IndexOf(BarKind.Volume) - 1, bar.IndexOf(BarKind.VirtualTouchpad));
    }

    /// <summary>
    /// The library has them on its Taskbar page and nowhere else: the widgets board with
    /// Windows' other buttons under Taskbar, the pen menu, the touch keyboard and the touchpad under
    /// System, beside the keyboard.
    /// </summary>
    [Fact]
    public void TheLibraryHasThemOnItsTaskbarPage()
    {
        var everything = Catalogue.Everything();
        foreach (var (id, group) in new[]
        {
            ("widgetsBoard", WidgetCategory.WindowsMenus),
            ("penMenu", WidgetCategory.System),
            ("touchKeyboard", WidgetCategory.System),
            ("virtualTouchpad", WidgetCategory.System),
        })
        {
            var row = Assert.Single(everything, row => row.Id == id);
            Assert.Equal($"bar/{id}", row.Key);
            Assert.Equal(WidgetType.Bar, row.Type);
            Assert.Equal(group, row.Category);
        }
    }

    /// <summary>A new bar has neither Windows' widgets board nor Karpo's weather in its place (2026-10-14: "dont actually show the widgets button").</summary>
    [Fact]
    public void ANewBarHasNoWidgetsButton() =>
        Assert.DoesNotContain(Sections.DefaultLayout([], PenTablet), island => island.Kind is BarKind.WidgetsBoard || island.Folder?.Face.Kind == FolderFaces.WeatherId);
}
