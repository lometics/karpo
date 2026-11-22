// The input indicator is a preset (docs\widgets.md §124): what a click on the keyboard entry gave —
// the layouts installed, the one in use ticked, a pick switching the window the person types in — is
// a folder holding the keyboard's line (QuickPageTile hosting KeyboardPage) and the premade keyboard
// actions under it, under the keyboard face: the entry's own "ENG" over "US", measured and drawn as
// the entry was (KeyboardSquareTests holds its size). Made-up layouts throughout (SampleKeyboard,
// through QuickPageTile.Sources): no window's layout is switched for real.
//
// Pictured: keyboard-preset-<dark|light>.png at 150 % and 225 % text, and the face on the bar,
// keyboard-face-<dark|light>.png.

using System.Numerics;
using Karpo.Dock;
using Karpo.Dock.QuickSettings;
using Karpo.Dock.QuickSettings.Controls;
using Karpo.Dock.QuickSettings.Services;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using static Karpo.Tests.Folders.ListFormPictures;

namespace Karpo.Tests.Folders;

public sealed unsafe class KeyboardPresetTests
{
    private static readonly PresetMachine Machine = new(HasBattery: false, FixedDrives: []);

    /// <summary>The keyboard's preset folder, as the add list makes it.</summary>
    private static BarItem Holder() => Presets.Folder(Presets.Find(Presets.KeyboardId)!, Machine);

    /// <summary>The quick sources with <paramref name="keyboard"/>'s made-up layouts.</summary>
    private static QuickSources Sources(SampleKeyboard keyboard) => new()
    {
        Wifi = static () => new SampleWifi(),
        Bluetooth = static () => new SampleBluetooth(),
        Battery = static () => new SampleBattery(),
        Desktops = static () => new SampleDesktopNames(),
        Keyboard = () => keyboard,
    };

    /// <summary>The preset's folder, its line reading <paramref name="keyboard"/>; opened where asked.</summary>
    private static FolderIsland Made(Gfx gfx, SampleKeyboard keyboard, bool open = true)
    {
        QuickPageTile.Sources = Sources(keyboard);
        var holder = Holder();
        var folder = new FolderIsland($"{Owner}/{holder.Id}", holder, gfx, new Config());
        if (open)
        {
            folder.Opened(gfx);
        }
        return folder;
    }

    private static void Done(FolderIsland folder)
    {
        folder.Closed();
        folder.Dispose();
        QuickPageTile.Sources = null;
    }

    private static TileStyle Style(Look look, float scale = 1.5f) => FolderFlyout.StyleFor(scale, look, FlyoutTheme.For(look));

    /// <summary>The line laid out at the list's width: its rows, in the card's own px.</summary>
    private static (QuickPageTile Tile, D2D_RECT_F Box, IReadOnlyList<ListRow> Rows) Laid(FolderIsland folder, TileStyle style)
    {
        var tile = folder.Tiles.Select(each => each.Island).OfType<QuickPageTile>().Single();
        tile.Pump();
        var width = FolderFlyout.ListWidth(1.5f);
        var box = PopupPaint.Rect(0, 0, width, tile.ListHeight(width, style));
        tile.LayOut(box, style);
        return (tile, box, [.. tile.Placed.OfType<ListRow>()]);
    }

    /// <summary>A press and a release in the middle of row <paramref name="index"/>.</summary>
    private static void Click(FolderIsland folder, TileStyle style, int index)
    {
        var (tile, box, rows) = Laid(folder, style);
        var at = rows[index].Bounds;
        if (tile.CardPress(new Vector2((at.left + at.right) / 2, (at.top + at.bottom) / 2), box, style, WidgetSurface.List))
        {
            tile.CardRelease();
        }
        tile.Pump();
    }

    // --- The preset ------------------------------------------------------------------------------------

    /// <summary>
    /// The layouts as a line, then in the same card the premade keyboard actions — Language &amp;
    /// region, for the input switcher's "More keyboard settings", alone on the right — under the
    /// keyboard face, which has no name and nothing to set.
    /// </summary>
    [Fact]
    public void ThePresetIsTheLayoutsAndTheirFooterUnderTheKeyboardFace()
    {
        var preset = Presets.Find(Presets.KeyboardId)!;
        Assert.Equal(FolderFaces.KeyboardId, preset.Face!.Kind);
        Assert.Equal(["keyboard", "actions"], preset.Items.Select(item => item.Form));
        Assert.Same(PremadeWidgets.KeyboardActions, preset.Items[^1].Premade);
        Assert.Equal((null, "language"), PremadeWidgets.KeyboardActions.Actions);

        var folder = Holder().Folder!;
        Assert.Same(FolderFaces.Keyboard, folder.Face.Info);
        Assert.Empty(folder.Face.Info.Options);
        Assert.Equal([ListKind.Keyboard, ListKind.Actions], folder.Cells.Select(cell => cell!.Island!.List));
        Assert.Equal("ms-settings:regionlanguage", SettingsActions.Find("language")!.Uri);
    }

    /// <summary>
    /// The entry is gone from the bar and from the dock's flyouts; its word is the layouts' line now,
    /// drawn by a quick-settings page.
    /// </summary>
    [Fact]
    public void TheEntryIsGoneAndItsWordIsTheLine()
    {
        Assert.Null(BarEntries.Find("keyboard"));
        Assert.DoesNotContain(BarEntries.All, entry => entry.Info == WidgetInfos.Keyboard);
        Assert.Equal(WidgetSource.QuickPage, ListWidgets.Find("keyboard")!.Drawing);
        Assert.Equal(CellAct.Nothing, ListWidgets.Find("keyboard")!.Act);
        Assert.Contains(Presets.All, preset => preset.Id == Presets.KeyboardId);
    }

    // --- The line --------------------------------------------------------------------------------------

    /// <summary>
    /// The folder opened: a row per layout in Windows' order, its language over the indicator's codes
    /// and the layout's keyboard, the one in use marked and in semibold, the others not.
    /// </summary>
    [UiFact]
    public void TheLineListsTheLayoutsTheOneInUseMarked() => Offscreen.OnStaThread(gfx =>
    {
        var keyboard = new SampleKeyboard(SampleKeyboard.Three with { Current = 0x08070807 });
        var folder = Made(gfx, keyboard);
        try
        {
            var tile = folder.Tiles.Select(each => each.Island).OfType<QuickPageTile>().Single();
            Assert.Equal(QuickKind.Keyboard, tile.Page);
            var rows = Laid(folder, Style(LookFor(dark: true))).Rows;
            Assert.Equal(["English (United States)", "German (Switzerland)", "French (France)"], rows.Select(row => row.Title));
            Assert.Equal(S.WidgetKeyboardLayoutKeyboard("Swiss German"), rows[1].Detail!.Split(" · ")[1]);
            Assert.StartsWith("DEU CH", rows[1].Detail);
            Assert.Equal([false, true, false], rows.Select(row => row.Selected));
            Assert.Equal([false, true, false], rows.Select(row => row.Strong));
            Assert.Empty(keyboard.Chosen);
        }
        finally
        {
            Done(folder);
        }
    });

    /// <summary>
    /// A click on a row switches the window the person types in to that layout — the source is asked,
    /// never the folder's own thread — and the line marks it; a row is matched by its layout, so the
    /// third row is French whatever is marked.
    /// </summary>
    [UiFact]
    public void AClickSwitchesTheWindowToTheLayout() => Offscreen.OnStaThread(gfx =>
    {
        var keyboard = new SampleKeyboard();
        var folder = Made(gfx, keyboard);
        try
        {
            var style = Style(LookFor(dark: false));
            Click(folder, style, 2);
            Assert.Equal([(nint)0x040C040C], keyboard.Chosen);
            Assert.Equal([false, false, true], Laid(folder, style).Rows.Select(row => row.Selected));
            Click(folder, style, 0);
            Assert.Equal([(nint)0x040C040C, (nint)0x04090409], keyboard.Chosen);
        }
        finally
        {
            Done(folder);
        }
    });

    /// <summary>The keys reach the rows as in any page line: Down rings the first, Enter switches to it.</summary>
    [UiFact]
    public void TheKeysWalkTheRowsAndChoose() => Offscreen.OnStaThread(gfx =>
    {
        var keyboard = new SampleKeyboard();
        var folder = Made(gfx, keyboard);
        try
        {
            var (tile, _, _) = Laid(folder, Style(LookFor(dark: true)));
            Assert.True(tile.CardKey(TerraFX.Interop.Windows.VK.VK_DOWN, WidgetSurface.List));
            Assert.True(tile.CardKey(TerraFX.Interop.Windows.VK.VK_DOWN, WidgetSurface.List));
            Assert.True(tile.CardKey(TerraFX.Interop.Windows.VK.VK_RETURN, WidgetSurface.List));
            Assert.Equal([(nint)0x08070807], keyboard.Chosen);
        }
        finally
        {
            Done(folder);
        }
    });

    /// <summary>The line is as tall as its layouts: one more layout, one more row.</summary>
    [UiFact]
    public void TheLineIsAsTallAsItsLayouts() => Offscreen.OnStaThread(gfx =>
    {
        var three = new SampleKeyboard();
        var one = new SampleKeyboard(SampleKeyboard.Three with { Layouts = [.. SampleKeyboard.Three.Layouts.Take(1)] });
        var tall = Made(gfx, three);
        var low = Made(gfx, one);
        try
        {
            var style = Style(LookFor(dark: true));
            var width = FolderFlyout.ListWidth(1.5f);
            var (threeTile, _, threeRows) = Laid(tall, style);
            var (oneTile, _, oneRows) = Laid(low, style);
            Assert.Equal(3, threeRows.Count);
            Assert.Single(oneRows);
            // From one seam to the next: a row's band takes half the room to each neighbour (§428).
            var row = threeRows[2].Bounds.top - threeRows[1].Bounds.top;
            Assert.Equal(2 * row, threeTile.ListHeight(width, style) - oneTile.ListHeight(width, style), 0.5f);
        }
        finally
        {
            Done(low);
            Done(tall);
        }
    });

    // --- The face --------------------------------------------------------------------------------------

    /// <summary>
    /// The face is the folder's own indicator: it shows the label it reads, is painted again where
    /// the label changes and only then, and its hover is the indicator's — the language, and the
    /// layout's keyboard. Another face lets the indicator go.
    /// </summary>
    [UiFact]
    public void TheFaceIsTheIndicatorAndPaintsAgainAsTheLayoutChanges() => Offscreen.OnStaThread(gfx =>
    {
        var folder = Made(gfx, new SampleKeyboard(), open: false);
        try
        {
            var style = new IslandStyle(1f, false, Layout.CellPx(96, new Config()), Layout.IconPx(96, new Config()), LookFor(dark: true));
            var (w, h) = folder.CellSize(0, style);
            Offscreen.Bitmap(w, h, dc => folder.PaintCell(dc, 0, style, w, h), _ => { });
            Assert.False(folder.FaceTick(gfx));
            folder.Keyboard!.Show(("DEU", "CH"));
            Assert.Equal("DEU|CH", FaceDrawings.Of(folder.Options.Face).Reading(new FaceContext(gfx, style, folder.Options, folder)));
            Assert.NotEqual("DEU|CH", folder.FaceShown);
            Offscreen.Bitmap(w, h, dc => folder.PaintCell(dc, 0, style, w, h), _ => { });
            Assert.Equal("DEU|CH", folder.FaceShown);
            Assert.Equal(folder.Keyboard.Tooltip(0), FaceDrawings.Of(folder.Options.Face).Tooltip(new FaceContext(gfx, style, folder.Options, folder)));

            var other = Holder();
            other.Folder!.Face = FolderFace.Of(FolderFaces.IconId);
            folder.Configure(other, new Config());
            Assert.Null(folder.Keyboard);
        }
        finally
        {
            Done(folder);
        }
    });

    // --- Pictures --------------------------------------------------------------------------------------

    /// <summary>
    /// The preset as it opens, English in use; German in use; and at 225 % text — at 150 %.
    /// </summary>
    [UiFact]
    public void KeyboardPresetPictures()
    {
        foreach (var dark in new[] { true, false })
        {
            Offscreen.OnStaThread(gfx =>
            {
                var folders = new List<FolderIsland>();
                try
                {
                    var panels = new List<(string, Look, FolderIsland, FolderPlan)>();
                    void Add(string caption, SampleKeyboard keyboard, float text = 1f)
                    {
                        var look = LookFor(dark, text);
                        var folder = Made(gfx, keyboard);
                        folders.Add(folder);
                        Laid(folder, Style(look));
                        panels.Add((caption, look, folder, Plan(folder, look, 1.5f).Plan));
                    }

                    Add("Three layouts: English in use", new SampleKeyboard());
                    Add("German in use", new SampleKeyboard(SampleKeyboard.Three with { Current = 0x08070807 }));
                    Add("One layout", new SampleKeyboard(SampleKeyboard.Three with { Layouts = [.. SampleKeyboard.Three.Layouts.Take(1)] }));
                    Add("225 % text", new SampleKeyboard(), 2.25f);
                    Sheet(gfx, $"keyboard-preset-{(dark ? "dark" : "light")}.png", 1.5f, panels);
                }
                finally
                {
                    foreach (var folder in folders)
                    {
                        folder.Closed();
                        folder.Dispose();
                    }
                    QuickPageTile.Sources = null;
                }
            });
        }
    }

    /// <summary>
    /// The face on a bar of 28, 40 and 48 DIP at 100 % and of 48 at 150 %, showing ENG US, ENG alone
    /// and DEU CH — keyboard-face-&lt;dark|light&gt;.png.
    /// </summary>
    [UiFact]
    public void KeyboardFacePictures()
    {
        foreach (var dark in new[] { true, false })
        {
            Offscreen.OnStaThread(gfx =>
            {
                var look = LookFor(dark);
                var theme = FlyoutTheme.For(look);
                (string, string)[] labels = [("ENG", "US"), ("ENG", ""), ("DEU", "CH")];
                var styles = new[] { (28, 96u), (40, 96u), (48, 96u), (48, 144u) }
                    .Select(pair => new IslandStyle(pair.Item2 / 96f, false, Layout.CellPx(pair.Item2, new Config { Height = pair.Item1 }), Layout.IconPx(pair.Item2, new Config { Height = pair.Item1 }), look))
                    .ToList();
                var islands = labels.Select(label =>
                {
                    var folder = Made(gfx, new SampleKeyboard(), open: false);
                    folder.Keyboard!.Show(label);
                    return folder;
                }).ToList();
                try
                {
                    const float gap = 12;
                    var width = gap + styles.Sum(style => islands.Sum(island => island.CellSize(0, style).Width + gap) + gap);
                    var height = gap + styles.Max(style => islands[0].CellSize(0, style).Height) + gap;
                    Offscreen.Bitmap(width, height, dc =>
                    {
                        PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, width, height), 0, PopupPaint.Ink(theme.Base));
                        var x = gap;
                        foreach (var style in styles)
                        {
                            foreach (var island in islands)
                            {
                                var (w, h) = island.CellSize(0, style);
                                PopupPaint.Fill(dc, PopupPaint.Rect(x, gap, w, h), 4 * style.Scale, PopupPaint.Ink(theme.CardBackground));
                                Offscreen.Shifted(dc, x, gap, () => island.PaintCell(dc, 0, style, w, h));
                                x += w + gap;
                            }
                            x += gap;
                        }
                    }, bitmap => Offscreen.Save(bitmap, Path.Combine(Offscreen.Pictures, $"keyboard-face-{(dark ? "dark" : "light")}.png")));
                }
                finally
                {
                    foreach (var island in islands)
                    {
                        Done(island);
                    }
                }
            });
        }
    }
}
