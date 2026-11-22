// The Snipping Tool and Settings (the backlog, items 10 and 18): "shortcut widgets, the same
// one-icon design as Task view and Search, in all three views" — the bar and a list since grid
// folders went (2026-10-09) — and clipboard history (item 13), the taskbar's alone. Phone Link was a third shortcut (item 23) until the person asked for it gone; a
// file that still holds one has a pin to the app in its place (PhoneLinkRemovedTests).
//
//   * where they stand: on every page of the library (clipboard history on the Taskbar page only),
//     found by what people call them;
//   * what a click opens, through the answer that says it without doing it (WindowsButtons.Target)
//     and the seam that does it (WindowsButtons.Press), never pressing or starting anything here:
//     a snip (ms-screenclip:), in a folder once the folder has gone, and Settings (ms-settings:) —
//     and, where the Snipping Tool is not installed, a way to get it;
//   * their menus: the Snipping Tool's app, its delays and its recorder — greyed where the Snipping
//     Tool installed cannot — and "Get Snipping Tool" only while it is missing;
//   * and pictures of each in each of its views, light and dark, 100 and 150 % — the bar's buttons
//     (bar-shortcuts-*.png) and the lines (list-shortcuts-*.png).
//
// The seams are static, and a test here sets the installed apps (WindowsButtons.VersionOf), so this
// class runs alone. Needs Direct2D for the pictures and the clicks: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Folders;

[CollectionDefinition(Collection, DisableParallelization = true)]
public sealed class WindowsButtonSeams
{
    public const string Collection = "the Windows buttons' seams";
}

[Collection(WindowsButtonSeams.Collection)]
public sealed unsafe class ShortcutWidgetTests
{
    private static readonly string[] Shortcuts = ["snippingTool", "windowsSettings"];

    /// <summary>The Snipping Tool on this made-up PC, as a full package name would say it.</summary>
    private static readonly Version Snipping = new(11, 2607, 23, 0);

    /// <summary>
    /// Runs <paramref name="body"/> with the apps installed being <paramref name="installed"/>, by
    /// family, and puts the PC's own answer back after.
    /// </summary>
    private static void With(Dictionary<string, Version> installed, Action body)
    {
        var before = WindowsButtons.VersionOf;
        WindowsButtons.VersionOf = family => installed.GetValueOrDefault(family);
        try
        {
            body();
        }
        finally
        {
            WindowsButtons.VersionOf = before;
        }
    }

    private static Dictionary<string, Version> Everything() => new()
    {
        [WindowsButtons.SnippingFamily] = Snipping,
    };

    // --- Where they stand ------------------------------------------------------------------------

    /// <summary>The two shortcuts are on every page of the library.</summary>
    [Fact]
    public void TheShortcutsAreOnEveryPage()
    {
        foreach (var page in Catalogue.Pages)
        {
            foreach (var id in Shortcuts)
            {
                Assert.Single(Catalogue.On(page), entry => entry.Id == id);
            }
        }
        Assert.Equal(["Snipping Tool", "Settings"],
            new[] { WidgetInfos.SnippingTool, WidgetInfos.WindowsSettings }.Select(info => info.Title));
        // The shortcuts open apps.
        Assert.All([BarKind.SnippingTool, BarKind.WindowsSettings], kind =>
        {
            Assert.Equal(BarClick.Action, BarEntries.Of(kind).Click);
            Assert.True(kind.IsWindowsButton());
        });
    }

    // --- What a click opens ---------------------------------------------------------------------

    /// <summary>
    /// The Snipping Tool snips at once, through the address it answers (ms-screenclip:), where it is
    /// installed. Where it is not, the button says so, as a pin to an app that is not here does, with its page in the
    /// Store — Win+Shift+S is its own overlay, so pressing that instead did nothing (regression).
    /// </summary>
    [Fact]
    public void TheSnippingToolSnipsAtOnceOrSaysItIsNotInstalled()
    {
        var asked = new List<string>();
        Assert.Equal(new ShellTarget(OpenBy.Address, "ms-screenclip:"),
            WindowsButtons.Target(BarKind.SnippingTool, family => { asked.Add(family); return true; }));
        Assert.Equal(["Microsoft.ScreenSketch_8wekyb3d8bbwe"], asked);
        Assert.Null(WindowsButtons.Missing(BarKind.SnippingTool, _ => true));
        Assert.Equal(new ShellTarget(OpenBy.NotInstalled, "ms-windows-store://pdp/?PFN=Microsoft.ScreenSketch_8wekyb3d8bbwe"),
            WindowsButtons.Target(BarKind.SnippingTool, _ => false));
        Assert.Equal("This app isn't installed on this PC", WindowsButtons.Missing(BarKind.SnippingTool, _ => false));
    }

    [Fact]
    public void SettingsOpensWindowsSettings()
    {
        Assert.Equal(new ShellTarget(OpenBy.Address, "ms-settings:"), WindowsButtons.Target(BarKind.WindowsSettings, _ => false));
        Assert.Null(WindowsButtons.Missing(BarKind.WindowsSettings, _ => false));
    }

    /// <summary>
    /// An app's version is read from its packages' full names: the highest of its family, a resource
    /// package counting as its own, another family's never; none where none is installed.
    /// </summary>
    [Fact]
    public void AnAppsVersionIsItsPackagesHighest()
    {
        string[] packages =
        [
            "Microsoft.ScreenSketch_11.2607.23.0_x64__8wekyb3d8bbwe",
            "Microsoft.ScreenSketch_11.2510.31.0_x64__8wekyb3d8bbwe",
            "Microsoft.YourPhone_1.26042.95.0_neutral_split.scale-400_8wekyb3d8bbwe",
            "Microsoft.ScreenSketchPreview_12.0.0.0_x64__8wekyb3d8bbwe",
        ];
        Assert.Equal(new Version(11, 2607, 23, 0), WindowsButtons.Version(WindowsButtons.SnippingFamily, packages));
        Assert.Equal(new Version(1, 26042, 95, 0), WindowsButtons.Version("Microsoft.YourPhone_8wekyb3d8bbwe", packages));
        Assert.Null(WindowsButtons.Version("Microsoft.WindowsNotepad_8wekyb3d8bbwe", packages));
        Assert.Null(WindowsButtons.Version(WindowsButtons.SnippingFamily, []));
    }

    /// <summary>The Snipping Tool records through its address since 11.2307, as Microsoft's own documentation of it said.</summary>
    [Fact]
    public void TheSnippingToolRecordsSince11_2307()
    {
        Assert.False(WindowsButtons.Records(null));
        Assert.False(WindowsButtons.Records(new Version(11, 2306, 99, 0)));
        Assert.True(WindowsButtons.Records(new Version(11, 2307)));
        Assert.True(WindowsButtons.Records(Snipping));
    }

    /// <summary>
    /// A line opens what the bar's button opens, through the one seam, and hands the
    /// folder nothing to carry out: the app takes the screen and the folder may close. The Snipping
    /// Tool missing opens its page in the Store, which is all a folder can offer. The
    /// snip alone waits (regression: the folder was in the picture): its click closes the folder,
    /// and the snip is started after (ShortcutTile.Act, which the dock calls once the folder has gone).
    /// </summary>
    [UiFact]
    public void ALineOpensWhatTheBarsButtonOpens()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var pressed = new List<ShellTarget>();
            var before = WindowsButtons.Press;
            WindowsButtons.Press = pressed.Add;
            try
            {
                foreach (var installed in new Dictionary<string, Version>[] { Everything(), [] })
                {
                    With(installed, () =>
                    {
                        foreach (var id in Shortcuts)
                        {
                            var holder = BarKind.FolderList.New();
                            holder.Id = "holder";
                            holder.Folder = new FolderOptions();
                            CellItem cell = ListWidgets.Find(id)!.Kind.New();
                            cell.Id = id;
                            holder.Folder.Put(cell);
                            using var folder = new FolderIsland(Config.IslandKey(null, "holder"), holder, gfx, new Config());
                            var tile = Assert.Single(folder.Tiles);
                            Assert.IsType<ShortcutTile>(tile.Island);
                            pressed.Clear();

                            var action = tile.Island.Click(0);

                            var act = FolderFlyout.ActOf(tile.Cell);
                            Assert.Equal(CellAct.Action, act);
                            if (id == "snippingTool" && installed.Count > 0)
                            {
                                // Nothing yet: the folder is asked to close, and the snip comes after.
                                Assert.Empty(pressed);
                                Assert.True(FolderFlyout.Forwards(action, act), "the folder is not closed before the snip");
                                ((ShortcutTile)tile.Island).Act();
                                Assert.Equal([new ShellTarget(OpenBy.Address, "ms-screenclip:")], pressed);
                                continue;
                            }
                            var expected = id == "snippingTool"
                                ? new ShellTarget(OpenBy.NotInstalled, WindowsButtons.SnippingStore)
                                : new ShellTarget(OpenBy.Address, "ms-settings:");
                            Assert.Equal([expected], pressed);
                            Assert.False(FolderFlyout.Forwards(action, act), $"{id} hands the folder something to carry out");
                        }
                    });
                }
            }
            finally
            {
                WindowsButtons.Press = before;
            }
        });
    }

    // --- The right click -------------------------------------------------------------------------

    private static (List<FlyoutItem> Items, List<WidgetActRow> Rows) Rows(IReadOnlyList<WidgetAct> acts) => TheDock.ActRows(acts, widget: null);

    /// <summary>
    /// The acts that picture the screen — a snip in 3 and in 10 seconds, "Record screen" — wait for
    /// every open folder to be off the screen, as the snip a click starts does; the rest of the menu,
    /// the Snipping Tool's own window included, needs nothing gone.
    /// </summary>
    [Fact]
    public void ASnipFromTheMenuClosesTheFoldersFirst()
    {
        Assert.True(TheDock.PicturesTheScreen(WidgetAct.SnipIn3));
        Assert.True(TheDock.PicturesTheScreen(WidgetAct.SnipIn10));
        Assert.True(TheDock.PicturesTheScreen(WidgetAct.RecordScreen));
        Assert.All(Enum.GetValues<WidgetAct>().Except([WidgetAct.SnipIn3, WidgetAct.SnipIn10, WidgetAct.RecordScreen]),
            act => Assert.False(TheDock.PicturesTheScreen(act)));
    }

    /// <summary>
    /// The Snipping Tool's menu, on the bar and as a line: "Open Snipping Tool", a snip in
    /// 3 and in 10 seconds and "Record screen", each opening the Snipping Tool's own address and each
    /// handing the screen away; greyed where the Snipping Tool installed cannot — the recorder before
    /// 11.2307, everything where there is none, and then "Get Snipping Tool from Microsoft Store" first.
    /// </summary>
    [Fact]
    public void TheSnippingToolsMenuIsTheRestOfWhatItDoes()
    {
        WidgetAct[] acts = [WidgetAct.GetSnippingTool, WidgetAct.OpenSnippingTool, WidgetAct.SnipIn3, WidgetAct.SnipIn10, WidgetAct.RecordScreen];
        // On the bar, the print screen key's page in Windows after them (Dock.FaceMenus.cs).
        Assert.Equal([.. acts, WidgetAct.WindowsSettings], TheDock.ActsFor(BarKind.SnippingTool));
        Assert.Equal(acts, TheDock.ActsFor(ListKind.SnippingTool));
        // What the Snipping Tool installed cannot do is left out (§308): an older one records nothing
        // through its address, and none at all has only the way to get it.
        foreach (var (version, texts) in new (Version?, string[])[]
        {
            (Snipping, ["Open Snipping Tool", "Snip in 3 seconds", "Snip in 10 seconds", "Record screen"]),
            (new Version(11, 2200), ["Open Snipping Tool", "Snip in 3 seconds", "Snip in 10 seconds"]),
            (null, ["Get Snipping Tool from Microsoft Store"]),
        })
        {
            var installed = version is null ? [] : new Dictionary<string, Version> { [WindowsButtons.SnippingFamily] = version };
            With(installed, () =>
            {
                var (items, rows) = Rows(acts);
                Assert.Equal(texts, items.Select(item => item.Text));
                Assert.All(items, item => Assert.True(item.Enabled, item.Text));
                var opened = new List<(string Target, string? Verb)>();
                foreach (var row in rows)
                {
                    TheDock.RunAct(row, widget: null, (target, verb) => opened.Add((target, verb)));
                    Assert.True(TheDock.HandsAway(row.Act));
                }
                (string, string?)[] targets =
                [
                    (@"shell:AppsFolder\Microsoft.ScreenSketch_8wekyb3d8bbwe!App", (string?)null),
                    ("ms-screenclip:?delayInSeconds=3", null),
                    ("ms-screenclip:?delayInSeconds=10", null),
                    ("ms-screenclip:?type=recording", null),
                ];
                (string, string?)[] expected = version is null ? [(WindowsButtons.SnippingStore, null)] : [.. targets.Take(texts.Length)];
                Assert.Equal(expected, opened);
            });
        }
    }

    /// <summary>
    /// The bar's Snipping Tool shows its own rows at rest, then Windows' print screen key page; the
    /// Settings button has Windows Update (Dock.FaceMenus.cs): neither is the buttons alone.
    /// </summary>
    [Fact]
    public void TheBarsMenusOfTheShortcuts()
    {
        static string[] Texts(IEnumerable<FlyoutItem> rows) => [.. rows.Select(row => row.Separator ? "—" : row.Text)];
        With(Everything(), () =>
        {
            var (items, acts) = TheDock.BarMenuRows(BarKind.SnippingTool.New());
            Assert.Equal(["Open Snipping Tool", "Snip in 3 seconds", "Snip in 10 seconds", "Record screen", WidgetInfos.SnippingTool.Settings!.Value.Title], Texts(items));
            Assert.Equal(5, acts.Count);
            Assert.Equal(["Windows Update settings"], Texts(TheDock.BarMenuRows(BarKind.WindowsSettings.New()).Items));
            Assert.Equal(["Windows Update settings"], Texts(TheDock.RestMenuRows(BarKind.WindowsSettings.New(), inFolder: false).Items));
        });
    }

    // --- The pictures ----------------------------------------------------------------------------

    private static readonly (float Scale, bool Dark)[] Looks = [(1f, true), (1f, false), (1.5f, true), (1.5f, false)];

    private static Look LookFor(bool dark) => TestLooks.LookFor(dark);

    /// <summary>
    /// The bar's buttons as the bar draws them, beside Search and Task view: the Snipping Tool,
    /// Settings, and the Snipping Tool not installed — faint with the warning badge, as a broken
    /// pin. Each in a real
    /// layout's box at a 48 DIP bar, 100 and 150 %, dark and light: bar-shortcuts-{scale}-{theme}.png.
    /// </summary>
    [UiFact]
    public void BarButtonsPicture()
    {
        BarKind[] kinds = [BarKind.Search, BarKind.TaskView, BarKind.SnippingTool, BarKind.WindowsSettings, BarKind.SnippingTool];
        Offscreen.OnStaThread(gfx =>
        {
            foreach (var (scale, dark) in Looks)
            {
                var dpi = (uint)MathF.Round(96 * scale);
                var config = new Config { Height = 48 };
                var specs = kinds.Select((kind, index) => PartSpec.For(Config.IslandKey(null, $"p{index}"), kind.New())).ToList();
                var layout = Layout.ForTests(dpi, new RECT { right = 2000, bottom = 1080 }, Edge.Bottom, config, specs, Layout.CellPx(dpi, config));
                var blocks = Enumerable.Range(0, kinds.Length).Select(layout.BlockRect).ToList();
                var left = blocks.Min(block => block.X);
                var top = blocks.Min(block => block.Y);
                var width = blocks.Max(block => block.Right) - left;
                var pad = MathF.Round(16 * scale);
                var look = LookFor(dark);
                var theme = FlyoutTheme.For(look);
                Offscreen.Bitmap(width + (2 * pad), layout.Thickness + (2 * pad), dc =>
                {
                    PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, width + (2 * pad), layout.Thickness + (2 * pad)), 0, PopupPaint.Ink(theme.Base));
                    for (var part = 0; part < kinds.Length; part++)
                    {
                        var kind = kinds[part];
                        var hit = kind switch
                        {
                            BarKind.Search => Hit.Search,
                            BarKind.TaskView => Hit.TaskView,
                            _ => Hit.WindowsButton(kind),
                        };
                        var box = layout.IconBox(hit.In(part), layout.UnitPx(24));
                        var at = PopupPaint.Rect(box.X - left + pad, box.Y - top + pad, box.Width, box.Height);
                        // The second Snipping Tool is the one whose app is not installed, as Scene draws it.
                        var missing = part is 4;
                        PopupPaint.Glyph(dc, gfx, kind.Glyph(), layout.UnitPx(20), missing ? IslandInk.Faded(theme.Text, ShortcutTile.MissingOpacity) : theme.Text, at);
                        if (missing)
                        {
                            var side = MathF.Max(MathF.Round(10 * scale), MathF.Round(box.Height * 0.5f));
                            var attention = dark ? new DXGI_RGBA(0xFC / 255f, 0xE1 / 255f, 0, 1) : new DXGI_RGBA(0x9D / 255f, 0x5D / 255f, 0, 1);
                            PopupPaint.Glyph(dc, gfx, ShortcutTile.WarningGlyph, side, attention,
                                PopupPaint.Rect(at.right - (side * 0.75f), at.bottom - (side * 0.75f), side, side));
                        }
                    }
                }, bitmap => Offscreen.Save(bitmap, Path.Combine(Offscreen.Pictures, $"bar-shortcuts-{(int)MathF.Round(scale * 100)}-{(dark ? "dark" : "light")}.png")));
            }
        });
    }

    /// <summary>
    /// The Snipping Tool's line where the app is not installed, as a broken pin's: it says why, in
    /// its tooltip too. How it looks is in the lines' picture (list-shortcuts-*-missing.png).
    /// </summary>
    [UiFact]
    public void TheSnippingToolsLineSaysItIsNotInstalled()
    {
        Offscreen.OnStaThread(gfx =>
        {
            With([], () =>
            {
                using var folder = ListFormPictures.Folder(gfx, ListFormPictures.Line(ListKind.SnippingTool));
                var island = Assert.IsType<ShortcutTile>(Assert.Single(folder.Tiles).Island);
                Assert.Equal("This app isn't installed on this PC", island.Missing);
                Assert.Equal("Snipping Tool\nThis app isn't installed on this PC", island.Tooltip(0));
            });
        });
    }

    /// <summary>
    /// The two as lines of a list — the list's own row of a glyph and a name, as Task view's — and
    /// the Snipping Tool's line where it is not installed, at 100 and 150 %, dark and light, each one row
    /// tall: list-shortcuts-{scale}-{theme}.png.
    /// </summary>
    [UiFact]
    public void TheLinesPicture()
    {
        Offscreen.OnStaThread(gfx =>
        {
            foreach (var (scale, dark) in Looks)
            {
                var look = ListFormPictures.LookFor(dark);
                var panels = new List<(string, Look, FolderIsland, FolderPlan)>();
                try
                {
                    foreach (var missing in new[] { false, true })
                    {
                        With(missing ? [] : Everything(), () =>
                        {
                            var folder = ListFormPictures.Folder(gfx, ListFormPictures.Line(ListKind.SnippingTool), ListFormPictures.Line(ListKind.WindowsSettings));
                            var (_, plan) = ListFormPictures.Plan(folder, look, scale);
                            Assert.All(Enumerable.Range(0, plan.Cards.Count), index =>
                                Assert.InRange(ListFormPictures.Given(plan, index), ListFormPictures.Rows(1, scale, 1) - 0.5f, ListFormPictures.Rows(1, scale, 1) + 0.5f));
                            panels.Add((missing ? "Not installed" : "Shortcuts: installed", look, folder, plan));
                        });
                    }
                    // Drawn with the Snipping Tool missing for the second panel only: the state is asked as it is drawn.
                    With(Everything(), () => ListFormPictures.Sheet(gfx, $"list-shortcuts-{(int)MathF.Round(scale * 100)}-{(dark ? "dark" : "light")}-installed.png", scale, [panels[0]]));
                    With([], () => ListFormPictures.Sheet(gfx, $"list-shortcuts-{(int)MathF.Round(scale * 100)}-{(dark ? "dark" : "light")}-missing.png", scale, [panels[1]]));
                }
                finally
                {
                    foreach (var (_, _, folder, _) in panels)
                    {
                        folder.Dispose();
                    }
                }
            }
        });
    }
}
