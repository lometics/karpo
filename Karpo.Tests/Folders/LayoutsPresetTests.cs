// The layout switcher is a preset (docs\widgets.md §127): what a click on it gave — the taskbar
// layouts, the one this bar shows marked, a click switching this display to another — is a folder
// holding the layouts' line (LayoutsLine), under a face of the layout's glyph and name the switcher
// was. A file's switcher becomes that folder in its place (FlyoutFoldersTests).
//
// The layouts are a test's own config: nothing here reads or writes the person's.
//
// Pictured: layouts-preset-<dark|light>.png at 150 %, and the face on the bar,
// layouts-face-<dark|light>.png.

using System.Numerics;
using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using static Karpo.Tests.Folders.ListFormPictures;

namespace Karpo.Tests.Folders;

public sealed unsafe class LayoutsPresetTests
{
    private static readonly PresetMachine Machine = new(HasBattery: false, FixedDrives: []);

    /// <summary>The layouts' preset folder, as the add list makes it.</summary>
    private static BarItem Holder() => Presets.Folder(Presets.Find(Presets.LayoutsId)!, Machine);

    /// <summary>A config of <paramref name="names"/>, the first the default (its id "default", the bar the tests' folders stand on).</summary>
    private static Config Layouts(params string[] names) => new()
    {
        Layouts = [.. names.Select((name, at) => new TaskbarLayout
        {
            Id = at == 0 ? TaskbarLayouts.DefaultId : $"L{at}",
            Name = name,
            Icon = at == 1 ? "" : TaskbarLayouts.LayoutGlyph,
        })],
        DefaultLayoutId = TaskbarLayouts.DefaultId,
    };

    /// <summary>The preset's folder on the default layout's bar of <paramref name="config"/>.</summary>
    private static FolderIsland Made(Gfx gfx, Config config, string? key = null)
    {
        var holder = Holder();
        return new FolderIsland(key ?? $"{Owner}/{holder.Id}", holder, gfx, config);
    }

    /// <summary>The line, alone, on the bar of <paramref name="owner"/>, shown as an opening folder shows it.</summary>
    private static LayoutsLine Line(Gfx gfx, Config config, string owner = Owner)
    {
        var cell = ListKind.Layouts.New();
        var line = new LayoutsLine(Config.IslandKey(owner, cell.Id), cell, gfx, config);
        line.StandIn(ListWidgets.Of(ListKind.Layouts));
        line.Visible(true);
        return line;
    }

    private static TileStyle StyleFor(float scale, Look look) => FolderFlyout.StyleFor(scale, look, FlyoutTheme.For(look));

    /// <summary>The line as tall as it asks to be, at the list's own width.</summary>
    private static D2D_RECT_F Whole(LayoutsLine line, in TileStyle style)
    {
        var width = FolderFlyout.ListWidth(style.Scale);
        return PopupPaint.Rect(0, 0, width, line.ListHeight(width, style));
    }

    private static IReadOnlyList<LinePiece> Pieces(LayoutsLine line, in TileStyle style) =>
        ((ILinePieces)line).LinePieces(Whole(line, style), style);

    private static Vector2 Middle(D2D_RECT_F box) => new((box.left + box.right) / 2, (box.top + box.bottom) / 2);

    /// <summary>A click on <paramref name="piece"/>, as the folder gives it: the pointer onto it, the press (the folder's), then its number.</summary>
    private static IslandAction Click(LayoutsLine line, in TileStyle style, string piece)
    {
        var box = Whole(line, style);
        var at = Middle(Assert.Single(Pieces(line, style), each => each.Name == piece).Box);
        line.CardHover(at, box, style, WidgetSurface.List);
        Assert.False(line.PressOn(at, box, style, WidgetSurface.List), "a press on a row is the folder's");
        var id = line.CardTarget(at, box, style, WidgetSurface.List);
        Assert.NotEqual(0, id);
        return line.Click(id);
    }

    // --- The preset ------------------------------------------------------------------------------------

    /// <summary>The layouts' line alone, under the layouts face, in the add list's groups and on no new bar.</summary>
    [Fact]
    public void ThePresetIsTheLineUnderTheLayoutsName()
    {
        var preset = Presets.Find(Presets.LayoutsId)!;
        Assert.Equal(FolderFaces.LayoutsId, preset.Face!.Kind);
        Assert.Equal(["layout"], preset.Items.Select(item => item.Form));
        var folder = Holder().Folder!;
        Assert.Equal(FolderFaces.Layouts, folder.Face.Info);
        Assert.Equal([ListKind.Layouts], folder.Cells.Select(cell => cell!.Island!.List));
        Assert.Contains(FolderFaces.Layouts, FolderFaces.All);
        Assert.DoesNotContain(Sections.DefaultLayout(), island => island.Folder?.Face.Kind == FolderFaces.LayoutsId);
    }

    /// <summary>
    /// The entry is gone from the bar; its word is the line's, whose click is the folder's act, the
    /// layout switched to once the folder has gone (IslandView.Layouts).
    /// </summary>
    [Fact]
    public void TheSwitcherIsGoneAndTheLineSwitches()
    {
        Assert.Null(BarEntries.Find("layout"));
        var line = ListWidgets.Of(ListKind.Layouts);
        Assert.Same(WidgetInfos.Layouts, line.Info);
        Assert.Equal("layout", line.Info.Id);
        Assert.Equal(CellAct.Action, line.Act);
        Assert.Equal(WidgetSource.Layouts, line.Drawing);
        Assert.True(FolderFlyout.Forwards(new IslandAction(View: IslandView.Layouts), CellAct.Action));
        Assert.False(FolderFlyout.Forwards(new IslandAction(View: IslandView.Layouts), CellAct.Nothing));
        Assert.DoesNotContain("Layouts", Enum.GetNames<Karpo.Dock.QuickSettings.QuickKind>());
    }

    /// <summary>What the face wears: the layout the bar shows, its own glyph, and whether there is another.</summary>
    [Fact]
    public void TheFaceWearsTheLayoutTheBarShows()
    {
        var config = Layouts("Default", "Work", "Games");
        var work = LayoutShown.Of(config, "layout:L1");
        Assert.Equal(("Work", "", 3, true), (work.Name, work.Glyph, work.Count, work.Choosable));
        var lone = LayoutShown.Of(Layouts("Mine"), Owner);
        Assert.Equal(("Mine", TaskbarLayouts.LayoutGlyph, false), (lone.Name, lone.Glyph, lone.Choosable));
        // A bar that belongs to no layout still says something.
        Assert.Equal(TaskbarLayouts.DefaultName, LayoutShown.Of(config, null).Name);
    }

    // --- The line --------------------------------------------------------------------------------------

    /// <summary>A row per layout in the config's order, the one this bar shows marked: the bar's menu's rows.</summary>
    [UiFact]
    public void ARowPerLayoutTheShownOneMarked()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var config = Layouts("Default", "Work", "Games");
            using var line = Line(gfx, config, "layout:L1");
            var style = StyleFor(1f, LookFor(true));
            var pieces = Pieces(line, style);
            Assert.Equal(["Default", "Work", "Games"], pieces.Where(piece => piece.Name.StartsWith("layout ", StringComparison.Ordinal)).Select(piece => piece.Text));
            Assert.Equal("Work", Assert.Single(pieces, piece => piece.Name == "current").Text);
            Assert.Equal(Dock.Dock.LayoutRows(config.Layouts, "L1").Select(row => row.Text), line.Rows().Select(row => row.Name));
        });
    }

    /// <summary>
    /// A click on another layout is the folder's act: the dock switches to it once the folder has gone
    /// (TakeChoice), by its place in the config's list, asked once. The one shown does nothing.
    /// </summary>
    [UiFact]
    public void AClickOnAnotherLayoutSwitchesOnceTheFolderHasGone()
    {
        Offscreen.OnStaThread(gfx =>
        {
            using var line = Line(gfx, Layouts("Default", "Work", "Games"));
            var style = StyleFor(1f, LookFor(true));

            Assert.Equal(default, Click(line, style, "layout 0"));
            Assert.Null(line.TakeChoice());

            var act = Click(line, style, "layout 2");
            Assert.Equal(IslandView.Layouts, act.View);
            Assert.Equal(2, line.TakeChoice());
            Assert.Null(line.TakeChoice());
        });
    }

    /// <summary>
    /// The keys walk the rows as the folder walks every line's targets (§470, LineFocus): each row a stop, in their
    /// order; Enter on one is its click, which switches to it.
    /// </summary>
    [UiFact]
    public void TheKeysWalkAndSwitch()
    {
        Offscreen.OnStaThread(gfx =>
        {
            using var line = Line(gfx, Layouts("Default", "Work"));
            var style = StyleFor(1f, LookFor(true));
            var stops = LineFocus.Parts(line, Whole(line, style), style, WidgetSurface.List);
            Assert.Equal([1, 2], stops.Select(stop => stop.Target));
            Assert.False(line.KeyOn(VK.VK_DOWN, WidgetSurface.List));
            Assert.Equal(IslandView.Layouts, line.Click(stops[1].Target).View);
            Assert.Equal(1, line.TakeChoice());
        });
    }

    /// <summary>
    /// One layout: no row to choose (§561: the bar's menu shows it, ticked), the kit's one Status saying so in its
    /// first sentence alone, and a standard "Add" against the trailing gutter (§404; the person, 2026-10-29: "if
    /// there is only one layout there should also be the add button, the text ... shorter, just the first
    /// sentence"). Its click is the folder's act: once the folder has gone the dock opens §628 Add a layout's page
    /// (TakeAdd, §485), asked once.
    /// </summary>
    [UiFact]
    public void OneLayoutSaysSoAndOffersToAddOne()
    {
        Offscreen.OnStaThread(gfx =>
        {
            using var line = Line(gfx, Layouts("Default"));
            var style = StyleFor(1f, LookFor(true));
            Assert.Empty(line.Rows());
            var pieces = Pieces(line, style);
            Assert.Equal(S.DockSwitcherOnlyLayout, Assert.Single(pieces, piece => piece.Name == "only").Text);
            Assert.DoesNotContain(";", S.DockSwitcherOnlyLayout);
            Assert.DoesNotContain("settings", S.DockSwitcherOnlyLayout, StringComparison.OrdinalIgnoreCase);
            var add = Assert.Single(pieces, piece => piece.Name == "add");
            Assert.Equal(S.SettingsCommonAdd, add.Text);
            var whole = Whole(line, style);
            var gutter = FolderFlyout.RowInset(style.Text(14));
            Assert.True(MathF.Abs(add.Box.right - (whole.right - gutter)) <= 1, $"Add ends at {add.Box.right:0}, the gutter at {whole.right - gutter:0}");
            Assert.Equal(LineRows.Px(1, style), whole.bottom - whole.top, 0.5f);

            Assert.False(line.TakeAdd());
            Assert.Equal(default, line.Click(0));
            var act = Click(line, style, "add");
            Assert.Equal(IslandView.Layouts, act.View);
            Assert.Null(line.TakeChoice());
            Assert.True(line.TakeAdd());
            Assert.False(line.TakeAdd());
        });
    }

    /// <summary>
    /// §485 (§472's open question): the line's "Add" offers the presets the Layouts page's "Add a layout" does, by opening that
    /// one picker — §628 its page (EditEntry.AddLayout, the link's own way there) —, never a picker of its own: there each
    /// preset is its name and its line, and a pick makes it under its name and shows it on the display the bar stands on (§404
    /// to §481 it made Karpo's as "New layout").
    /// </summary>
    [Fact]
    public void TheLinesAddOffersThePresetsOfDisplaysAddALayout()
    {
        Assert.Equal(new EditLanding(PanelPage.AddLayout), EditEntries.Landing(EditEntry.AddLayout));
        Assert.Equal(EditEntry.AddLayout, EditEntries.Of(OptionLink.AddLayout));
        var config = Layouts("Default");
        var bar = new BarSpot(@"\\.\DISPLAY1", "One");
        List<OptionPick> picks = [];
        var page = Karpo.Dock.Dock.LooksItems(config, bar, dark: true, picks, LooksScope.AddLayout);
        Assert.Equal(LayoutPresets.All.Select(preset => preset.Title), page.Select(item => item.Text));
        Assert.Equal(LayoutPresets.All.Select(preset => preset.Blurb), page.Select(item => item.Detail));
        var pick = OptionMenu.PickOf(picks, page[Array.IndexOf(LayoutPresets.All, LayoutPresets.Planner)].Id)!;
        Assert.True(OptionMenu.ApplyRow(OptionRows.Find(BarLooks.Rows(config, bar, dark: true, scope: LooksScope.AddLayout), pick.Key), pick));
        Assert.Equal((2, LayoutPresets.Planner.Title), (config.Layouts.Count, config.ActiveLayout(bar.Display).Name));
        Assert.Contains("OpenEditMenuLater(window, EditEntry.AddLayout);", RepoSource.Read("Karpo.Dock", "Dock.Folders.cs"));
        Assert.DoesNotContain("AddLayout(window)", RepoSource.Read("Karpo.Dock", "Dock.Switchers.cs"));
    }

    /// <summary>As tall as there are layouts: a row more is a row taller.</summary>
    [UiFact]
    public void TheLineIsAsTallAsThereAreLayouts()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var style = StyleFor(1f, LookFor(true));
            var width = FolderFlyout.ListWidth(1f);
            using var two = Line(gfx, Layouts("A", "B"));
            using var three = Line(gfx, Layouts("A", "B", "C"));
            var row = three.ListHeight(width, style) - two.ListHeight(width, style);
            Assert.True(row >= 32, $"a row is {row} px");
        });
    }

    // --- The face --------------------------------------------------------------------------------------

    /// <summary>
    /// The face is the switcher's button: the glyph and the layout's name across a bar, as long as the
    /// name up to the room of ten characters, past which it is cut (docs\widgets.md §244), and the glyph
    /// alone in a square down one on its side. Its tooltip the whole name and what a click does, or
    /// that there is no other.
    /// </summary>
    [UiFact]
    public void TheFaceIsAsLongAsTheNameUpToTheSwitchersRoom()
    {
        Offscreen.OnStaThread(gfx =>
        {
            foreach (var height in new[] { 32, 48 })
            {
                var config = Layouts("A layout with a very long name", "Work");
                config.Height = height;
                using var island = Made(gfx, config);
                var style = new IslandStyle(1.5f, false, Layout.CellPx(144, config), Layout.IconPx(144, config), LookFor(dark: true));
                var fit = new Fit(style);
                // The button's ends: a text widget's, never under 8 DIP (§506).
                var pad = 2 * MathF.Max(fit.Edge, fit.Px(8));
                var room = pad + fit.Glyph() + fit.Gap + TextLine.Template(gfx, Switchers.NameRoom, fit.Primary, Weight.Regular).Width;
                var (wide, _) = island.CellSize(0, style);
                var shortName = Layouts("X", "Work");
                shortName.Height = height;
                island.Configure(island.Settings, shortName);
                var x = pad + fit.Glyph() + fit.Gap + TextLine.Measure(gfx, "X", fit.Primary, Weight.Regular, null);
                Assert.Equal(MathF.Ceiling(room), wide, 1f);
                Assert.Equal(MathF.Ceiling(x), island.CellSize(0, style).Width, 1f);
                var down = style with { Vertical = true };
                var (_, along) = island.CellSize(0, down);
                Assert.Equal(FaceWidthTests.GlyphButton(down), along, 0.5f);
                Assert.Equal(Switchers.LayoutTooltip("X", 2), island.Tooltip(0));
            }
            using var lone = Made(gfx, Layouts("Mine"));
            Assert.Equal(Switchers.LayoutTooltip("Mine", 1), lone.Tooltip(0));
        });
    }

    // --- Pictures ----------------------------------------------------------------------------------------

    /// <summary>The preset as it opens at 150 %: at rest, a row under the pointer, the keys on a row, one layout, and at 225 % text.</summary>
    [UiFact]
    public void LayoutsPresetPictures()
    {
        foreach (var dark in new[] { true, false })
        {
            Offscreen.OnStaThread(gfx =>
            {
                var folders = new List<FolderIsland>();
                try
                {
                    var panels = new List<(string, Look, FolderIsland, FolderPlan)>();
                    void Add(string caption, Config config, Action<LayoutsLine, TileStyle>? set = null, float text = 1f)
                    {
                        var look = LookFor(dark, text);
                        var holder = Holder();
                        var island = new FolderIsland($"{Owner}/{holder.Id}-{panels.Count}", holder, gfx, config);
                        folders.Add(island);
                        island.Opened(gfx);
                        var line = (LayoutsLine)island.Tiles[0].Island;
                        var style = StyleFor(1.5f, look);
                        set?.Invoke(line, style);
                        panels.Add((caption, look, island, Plan(island, look, 1.5f).Plan));
                    }

                    var three = Layouts("Default", "Work", "A layout with a very long name indeed");
                    Add("At rest", three);
                    Add("A row under the pointer", three, (line, style) =>
                        line.CardHover(Middle(Assert.Single(Pieces(line, style), piece => piece.Name == "layout 1").Box), Whole(line, style), style, WidgetSurface.List));
                    Add("One layout", Layouts("Default"));
                    Add("225 % text", three, text: 2.25f);
                    Sheet(gfx, $"layouts-preset-{(dark ? "dark" : "light")}.png", 1.5f, panels);
                }
                finally
                {
                    foreach (var island in folders)
                    {
                        island.Closed();
                        island.Dispose();
                    }
                }
            });
        }
    }

    /// <summary>The face on bars of 40 and 48 DIP, at 150 %, down a bar on its side, and dimmed alone — layouts-face-&lt;dark|light&gt;.png.</summary>
    [UiFact]
    public void LayoutsFacePictures()
    {
        foreach (var dark in new[] { true, false })
        {
            Offscreen.OnStaThread(gfx =>
            {
                var look = LookFor(dark);
                var theme = FlyoutTheme.For(look);
                using var island = Made(gfx, Layouts("Default", "Work"), $"layout:L1/{Holder().Id}");
                using var lone = Made(gfx, Layouts("Default"));
                var styles = new[] { (40, 96u, false), (48, 96u, false), (48, 144u, false), (48, 144u, true) }
                    .Select(each => new IslandStyle(each.Item2 / 96f, each.Item3, Layout.CellPx(each.Item2, new Config { Height = each.Item1 }), Layout.IconPx(each.Item2, new Config { Height = each.Item1 }), look))
                    .ToList();
                var faces = styles.Select(style => (Folder: island, Style: style)).Append((Folder: lone, Style: styles[2])).ToList();
                const float gap = 12;
                var width = gap + faces.Sum(face => face.Folder.CellSize(0, face.Style).Width + gap);
                var height = gap + faces.Max(face => face.Folder.CellSize(0, face.Style).Height) + gap;
                Offscreen.Bitmap(width, height, dc =>
                {
                    PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, width, height), 0, PopupPaint.Ink(theme.Base));
                    var x = gap;
                    foreach (var (folder, style) in faces)
                    {
                        var (w, h) = folder.CellSize(0, style);
                        PopupPaint.Fill(dc, PopupPaint.Rect(x, gap, w, h), 4 * style.Scale, PopupPaint.Ink(theme.CardBackground));
                        Offscreen.Shifted(dc, x, gap, () => folder.PaintCell(dc, 0, style, w, h));
                        x += w + gap;
                    }
                }, bitmap => Offscreen.Save(bitmap, Path.Combine(Offscreen.Pictures, $"layouts-face-{(dark ? "dark" : "light")}.png")));
            });
        }
    }
}
