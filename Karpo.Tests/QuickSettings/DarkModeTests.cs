// Dark mode (QuickSettings\Services\DarkMode.cs): Windows' own mode as a toggle. It is on while the
// apps are dark (AppsUseLightTheme 0; absent is light, as Windows reads it); a click sets the apps
// and Windows (SystemUsesLightTheme) together, dark or light, and tells every window
// ("ImmersiveColorSet"); a change made in Windows' Settings is heard through the dock's own
// WM_SETTINGCHANGE. One switch for the bar's button, a folder's line and a toggles row. Made up (a
// store and a broadcast of the test's): nothing reads or changes this PC's mode. Pictured to
// KARPO_PNG_DIR as dark-mode-bar-<dark|light>.png (the button off, on, with its name) and
// dark-mode-lines-<dark|light>.png (its line off and on, and a toggles row holding it); the pictures
// need KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Dock.QuickSettings.Services;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using static Karpo.Tests.Folders.ListFormPictures;

namespace Karpo.Tests.QuickSettings;

public sealed class DarkModeTests
{
    // --- Where it is ---------------------------------------------------------------------------

    /// <summary>
    /// A toggle ("Dark mode on/off" in the add list) on both pages: the bar's button, a folder's line of
    /// one row, a toggles row's toggle — one switch behind the three — in the add list's Quick settings
    /// beside night light and at home with the screen's; the sun half dark, its page Windows' Colors.
    /// </summary>
    [Fact]
    public void TheCatalogueTheAddListAndTheTogglesHaveIt()
    {
        var info = WidgetInfos.DarkMode;
        Assert.Same(info, WidgetInfos.For(QuickSwitch.DarkMode));
        Assert.Equal("darkMode", info.Id);
        Assert.True(info.Flips);
        Assert.Equal("", info.Glyph);
        Assert.Equal("ms-settings:colors", info.Settings?.Uri);
        Assert.Equal("Dark mode", info.Title);
        Assert.Equal("Dark mode on/off", info.AddTitle);
        Assert.Contains(Catalogue.On(WidgetType.Bar), entry => entry.Id == info.Id);
        Assert.Contains(Catalogue.On(WidgetType.List), entry => entry.Id == info.Id);
        Assert.Equal(WidgetSource.Switch, BarEntries.Of(BarKind.DarkMode).Drawing);
        Assert.Equal(BarClick.Action, BarEntries.Of(BarKind.DarkMode).Click);
        var line = ListWidgets.Of(ListKind.DarkMode);
        Assert.Equal(WidgetSource.Switch, line.Drawing);
        Assert.Equal(LineHeight.Row, line.Cells);

        var quick = AddTree.QuickSettings.Rows.OfType<AddLeaf>().Select(leaf => leaf.Id).ToList();
        Assert.Equal(quick.IndexOf("nightLight") + 1, quick.IndexOf(info.Id));
        Assert.Contains(AddTree.QuickSettings.Rows, row => row is AddLeaf { Id: "darkMode", Again: true });
        var screen = AddTree.Browse.Rows.OfType<AddShelf>().Single(shelf => shelf.Id == "screen");
        Assert.Contains(screen.Rows, row => row is AddLeaf { Id: "darkMode", Again: false });

        // The toggles row's is the widget's: one definition, its glyph and name, its switch.
        var toggle = Toggles.Find(Toggle.DarkMode);
        Assert.NotNull(toggle);
        Assert.Equal(QuickSwitch.DarkMode, toggle.Switch);
        Assert.Equal(info.Title, toggle.Name);
        Assert.Equal(info.Glyph, toggle.Glyph);
        Assert.Equal(SwitchData.KeyOf(QuickSwitch.DarkMode), SwitchData.KeyOf(Toggle.DarkMode));
        // Offered where the person picks the row's toggles, and not shown on a new row.
        Assert.Contains(Toggles.All, offered => offered.Toggle == Toggle.DarkMode);
        Assert.DoesNotContain(Toggle.DarkMode, Toggles.Default);

        using var real = QuickSwitches.Windows(QuickSwitch.DarkMode);
        Assert.IsType<DarkModeSwitch>(real);
        Assert.Equal("Dark mode", real.Name);
    }

    // --- What it says --------------------------------------------------------------------------

    /// <summary>A value of Windows' mode: 0 dark, any other DWORD light, absent light (as Windows reads it), else not known.</summary>
    [Fact]
    public void AValueSaysDarkOrLight()
    {
        Assert.Equal(false, DarkMode.Dark(null));
        Assert.Equal(true, DarkMode.Dark(0));
        Assert.Equal(false, DarkMode.Dark(1));
        Assert.Null(DarkMode.Dark("0"));
        Assert.Null(DarkMode.Dark(new byte[] { 0 }));
    }

    /// <summary>
    /// It is on while the apps are dark, whatever Windows' own mode (Settings' "Custom"): the apps are
    /// most of the screen, and what "dark mode" means to most people. Until read, it says nothing.
    /// </summary>
    [Fact]
    public void OnIsTheAppsMode()
    {
        Assert.Equal(true, Started(new Store { Apps = 0, System = 0 }, out _, out _).On);
        Assert.Equal(true, Started(new Store { Apps = 0, System = 1 }, out _, out _).On);
        Assert.Equal(false, Started(new Store { Apps = 1, System = 0 }, out _, out _).On);
        Assert.Equal(false, Started(new Store(), out _, out _).On);
        using var unread = new DarkModeSwitch("Dark mode", new Store(), () => true);
        Assert.Null(unread.On);
        Assert.False(unread.Switches);
        Assert.True(unread.Available);
    }

    // --- A click -------------------------------------------------------------------------------

    /// <summary>
    /// A click sets the apps and Windows together — both dark, or both light — and only then tells every
    /// window, so Explorer, the apps and Karpo itself change at once. From "Custom" (Windows dark, the
    /// apps light) it is off, and a click makes everything dark.
    /// </summary>
    [Fact]
    public void AClickSetsBothAndTellsEveryWindow()
    {
        var store = new Store { Apps = 1, System = 0 };
        using var mode = Started(store, out var broadcasts, out var changes);
        Assert.True(mode.Switches);
        Assert.Equal(S.DockQuickDarkModeNote, mode.Note);

        Acted(mode, changes);
        Assert.Equal(["AppsUseLightTheme=0", "SystemUsesLightTheme=0"], store.Written);
        Assert.Equal(1, broadcasts.Count);
        Assert.Equal(true, mode.On);

        Acted(mode, changes);
        Assert.Equal(["AppsUseLightTheme=0", "SystemUsesLightTheme=0", "AppsUseLightTheme=1", "SystemUsesLightTheme=1"], store.Written);
        Assert.Equal(2, broadcasts.Count);
        Assert.Equal(false, mode.On);
        Assert.Equal(0, mode.Refusals);
    }

    /// <summary>Windows refusing the write: nothing is told, the tooltip says so, and a pending click ends at once.</summary>
    [Fact]
    public void ARefusalTellsNobodyAndSaysSo()
    {
        var store = new Store { Apps = 1, System = 1, Refuses = true };
        using var mode = Started(store, out var broadcasts, out var changes);
        Acted(mode, changes);
        Assert.Equal(0, broadcasts.Count);
        Assert.Equal(false, mode.On);
        Assert.Equal(1, mode.Refusals);
        Assert.Equal(S.DockQuickDarkModeRefused, mode.Note);
    }

    /// <summary>Before Windows' mode has been read there is nothing to flip: a click opens its page, and writes nothing.</summary>
    [Fact]
    public void BeforeItHasReadAClickOpensItsPage()
    {
        var opened = new List<string>();
        var store = new Store();
        using var mode = new DarkModeSwitch("Dark mode", store, () => true, page => opened.Add(page));
        mode.Act();
        Assert.Equal(["ms-settings:colors"], opened);
        Assert.Empty(store.Written);
    }

    // --- Followed ------------------------------------------------------------------------------

    /// <summary>
    /// Switched in Windows' Settings (or by another app), it says so: Windows announces it with
    /// WM_SETTINGCHANGE, which the dock hands to its widgets' data, and it reads the mode again.
    /// Nothing else it hears reads it.
    /// </summary>
    [Fact]
    public void AChangeMadeElsewhereIsHeard()
    {
        var store = new Store { Apps = 1, System = 1 };
        using var mode = Started(store, out _, out var changes);
        store.Apps = 0;
        var reads = store.Reads;
        mode.Heard(WM.WM_POWERBROADCAST, 0x000A);
        Assert.Equal(reads, store.Reads);
        Assert.Equal(false, mode.On);

        var changed = changes.Arm();
        mode.Heard(WM.WM_SETTINGCHANGE, 0);
        Assert.True(changed.Wait(TimeSpan.FromSeconds(10)));
        Assert.Equal(true, mode.On);
    }

    // --- Pictures ------------------------------------------------------------------------------

    /// <summary>The bar's button off, on, and on with its name; a folder's line off and on, and a toggles row with it on; dark and light.</summary>
    [UiFact]
    public void TheButtonTheLineAndTheRowArePictured()
    {
        foreach (var dark in new[] { true, false })
        {
            Offscreen.OnStaThread(gfx =>
            {
                var suffix = dark ? "dark" : "light";
                var panels = new List<(string, Look, FolderIsland, FolderPlan)>();
                try
                {
                    var on = new SampleSwitch("Dark mode", on: true, switches: true);
                    var off = new SampleSwitch("Dark mode", on: false, switches: true);
                    ToggleSwitches.Source = toggle => new SampleSwitch(toggle.ToString(), on: toggle is Toggle.DarkMode or Toggle.Wifi, switches: true);
                    Buttons(gfx, dark, $"dark-mode-bar-{suffix}.png", on, off);
                    foreach (var textScale in TextScales)
                    {
                        // The first line off, the second on.
                        var next = new Queue<IQuickSwitch>([off, on]);
                        QuickSwitches.Source = which => which == QuickSwitch.DarkMode && next.Count > 0 ? next.Dequeue() : null;
                        var folder = Folder(gfx, Line(ListKind.DarkMode), Line(ListKind.DarkMode),
                            Line(ListKind.Toggles, line => line.Toggles!.Shown = [Toggle.Wifi, Toggle.Bluetooth, Toggle.NightLight, Toggle.DarkMode, Toggle.EnergySaver, Toggle.DoNotDisturb]));
                        var look = LookFor(dark, textScale);
                        var (_, plan) = Plan(folder, look, 1.5f);
                        panels.Add(($"Dark mode: {textScale * 100:0} % text", look, folder, plan));
                    }
                    Sheet(gfx, $"dark-mode-lines-{suffix}.png", 1.5f, panels);
                }
                finally
                {
                    QuickSwitches.Source = null;
                    ToggleSwitches.Source = null;
                    foreach (var (_, _, folder, _) in panels)
                    {
                        folder.Closed();
                        folder.Dispose();
                    }
                }
            });
        }
    }

    /// <summary>The bar's button off, on, and both with its name beside it, on a 48 DIP bar at 150 %.</summary>
    private static unsafe void Buttons(Gfx gfx, bool dark, string file, IQuickSwitch on, IQuickSwitch off)
    {
        var look = LookFor(dark);
        var theme = FlyoutTheme.For(look);
        const float scale = 1.5f;
        var dpi = (uint)(96 * scale);
        var style = new IslandStyle(scale, false, Layout.CellPx(dpi, new Config { Height = 48 }), Layout.IconPx(dpi, new Config { Height = 48 }), look);
        var islands = new List<(SwitchIsland Island, float X, float W, float H)>();
        const float gap = 12;
        var x = gap;
        var tallest = 0f;
        try
        {
            foreach (var label in new[] { false, true })
            {
                foreach (var state in new[] { off, on })
                {
                    QuickSwitches.Source = _ => state;
                    var entry = BarKind.DarkMode.New();
                    entry.Id = $"d{islands.Count}";
                    entry.Switch!.ShowLabel = label;
                    var island = new SwitchIsland(Config.IslandKey(null, entry.Id), entry, gfx, new Config());
                    var (w, h) = island.CellSize(0, style);
                    islands.Add((island, x, w, h));
                    x += w + gap;
                    tallest = MathF.Max(tallest, h);
                }
            }
            var height = tallest + 2 * gap;
            Offscreen.Bitmap(x, height, dc =>
            {
                PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, x, height), 0, PopupPaint.Ink(theme.Base));
                foreach (var (island, left, w, h) in islands)
                {
                    Offscreen.Shifted(dc, left, gap, () => island.PaintCell(dc, 0, style, w, h));
                }
            }, bitmap => Offscreen.Save(bitmap, Path.Combine(Offscreen.Pictures, file)));
        }
        finally
        {
            foreach (var (island, _, _, _) in islands)
            {
                island.Dispose();
            }
        }
    }

    // --- Made up -------------------------------------------------------------------------------

    /// <summary>A dark mode switch over <paramref name="store"/>, once it has read; <paramref name="broadcasts"/> counts what it told.</summary>
    private static DarkModeSwitch Started(Store store, out Counter broadcasts, out Changes changes)
    {
        var told = new Counter();
        broadcasts = told;
        var heard = new Changes();
        changes = heard;
        var mode = new DarkModeSwitch("Dark mode", store, () =>
        {
            told.Count++;
            return true;
        });
        var read = heard.Arm();
        mode.Start(heard.Fire);
        Assert.True(read.Wait(TimeSpan.FromSeconds(10)));
        return mode;
    }

    /// <summary>A click, and its answer from the pool.</summary>
    private static void Acted(DarkModeSwitch mode, Changes changes)
    {
        var changed = changes.Arm();
        mode.Act();
        Assert.True(changed.Wait(TimeSpan.FromSeconds(10)));
    }

    /// <summary>The switch's changed callback, waited for one at a time.</summary>
    private sealed class Changes
    {
        private readonly object gate = new();
        private TaskCompletionSource next = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>The next change from now.</summary>
        public Task Arm()
        {
            lock (gate)
            {
                next = new(TaskCreationOptions.RunContinuationsAsynchronously);
                return next.Task;
            }
        }

        public void Fire()
        {
            lock (gate)
            {
                next.TrySetResult();
            }
        }
    }

    private sealed class Counter
    {
        public int Count { get; set; }
    }

    /// <summary>Windows' Personalize key, made up: the two values, what was written in order, and how often it was read.</summary>
    private sealed class Store : ISettingStore
    {
        public int? Apps { get; set; }

        public int? System { get; set; }

        public bool Refuses { get; init; }

        public List<string> Written { get; } = [];

        public int Reads { get; private set; }

        public object? Read(string name)
        {
            Reads++;
            return name switch
            {
                DarkMode.AppsValue => Apps,
                DarkMode.SystemValue => System,
                _ => null,
            };
        }

        public bool Write(string name, int data)
        {
            if (Refuses)
            {
                return false;
            }
            Written.Add($"{name}={data}");
            if (name == DarkMode.AppsValue)
            {
                Apps = data;
            }
            else if (name == DarkMode.SystemValue)
            {
                System = data;
            }
            return true;
        }

        public IDisposable? Watch(Action changed) => throw new InvalidOperationException("Dark mode follows the dock's WM_SETTINGCHANGE, not a watch of its own.");
    }
}
