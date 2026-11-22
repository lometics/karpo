// edit, §536: the settings panel's look previews as Karpo draws them, on the panel the person picked (§520, SettingsPanelPictures):
// a display 1920 × 1080 DIP at 150 % in edit mode, the Appearance page's one preview over its tabs (§592), in a few odd
// values. To KARPO_PNG_DIR as preview-<scene>-<dark|light>.png:
//
//  - whole: the whole look at once — a purple bar with dots and a yellow outline, cards in Georgia on it, the clock folder
//    open over its face, a red flyout outline round blue lines in Bahnschrift; §582 in both modes, dark left of the slanted
//    line, light right of it, each mode its own colours.
//
// §562: a widget's, a line's and a folder's own Look has no preview (LookPreviewTests.AWidgetsOwnLookHasNoPreview).
//
// And a look changed while its preview shows draws it again, once, and nothing else does (a scroll, the pointer).
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using System.Numerics;
using Karpo.Dock;
using Karpo.Tests.Islands;
using Karpo.Tests.Menus;
using TerraFX.Interop.Windows;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    /// <summary>A preview's scene: its name, the odd values.</summary>
    private sealed record PreviewScene(string Name, Action<Config> Odd);

    /// <summary>§592: one preview, the whole look: every part's odd values on it at once.</summary>
    private static readonly PreviewScene[] PreviewScenes =
    [
        new("whole", config =>
        {
            OddTaskbar(config);
            OddFolders(config);
        }),
    ];


    /// <summary>§582: <paramref name="tone"/> of <paramref name="slot"/> <paramref name="dark"/> in Windows' dark mode, <paramref name="light"/> in its light one.</summary>
    private static void Paired(LookSlot slot, Tone tone, string dark, string light)
    {
        _ = Looks.WritePaint(slot, tone, true, Karpo.Core.Paint.Parse(dark));
        _ = Looks.WritePaint(slot, tone, false, Karpo.Core.Paint.Parse(light));
    }

    /// <summary>
    /// The bar purple with dots and a yellow outline, its cards in Georgia; §582 each mode its own: dark a deep purple bar,
    /// plum cards, cream words; light a lavender bar, cream cards, brown words.
    /// </summary>
    private static void OddTaskbar(Config config)
    {
        var bar = Looks.Section(config, LookPart.Taskbar);
        Paired(bar, Tone.Background, "#E05C2D91", "#E0D9C8F2");
        _ = LookShorthand.Both(bar, Tone.Outline, Karpo.Core.Paint.Parse("#FFB900"));
        _ = Looks.WritePattern(bar, Pattern.Dots);
        var widgets = Looks.Section(config, LookPart.Widgets);
        Paired(widgets, Tone.Background, "#3B2766", "#FFF4CE");
        _ = LookShorthand.Both(widgets, Tone.Outline, Karpo.Core.Paint.Parse("#C19C00"));
        Paired(widgets, Tone.Text, "#FFF4CE", "#3B2300");
        _ = Looks.WriteFont(widgets, new TextStyle("Georgia", 600));
    }

    /// <summary>The flyouts' outline red, the lines in folders in Bahnschrift; §582 dark deep blue with white words, light pale blue with navy ones.</summary>
    private static void OddFolders(Config config)
    {
        _ = LookShorthand.Both(Looks.Section(config, LookPart.Flyouts), Tone.Outline, Karpo.Core.Paint.Parse("#E74856"));
        var lines = Looks.Section(config, LookPart.FolderWidgets);
        Paired(lines, Tone.Background, "#CC0F6CBD", "#CCC7E0F4");
        Paired(lines, Tone.Text, "#FFFFFF", "#0B2E59");
        Paired(lines, Tone.Accent, "#FFB900", "#8A5A00");
        _ = Looks.WriteFont(lines, new TextStyle("Bahnschrift"));
    }

    /// <summary>
    /// The pages the previews show, as the dock builds them of <paramref name="config"/>: the Appearance page; the page shown, and
    /// §592 the key of its one preview, over its tabs.
    /// </summary>
    private static (SettingsContent Content, PanelPage Page, string Open) PreviewContent(Config config, bool dark)
    {
        var bar = new BarSpot(MenuCorpus.Display, "One") { Attached = [MenuCorpus.Display] };
        var shown = PanelRows.Page(PanelPage.Appearance, S.SettingsAppearanceHeader, PanelRows.GlyphOf(PanelPage.Appearance), TheDock.LooksItems(config, bar, dark, [], LooksScope.Appearance));
        return (new SettingsContent([shown]), PanelPage.Appearance, shown.Rows.Single(row => row.Kind == PanelRowKind.Preview).Key);
    }

    [UiFact]
    public void LookPreviewSheets()
    {
        WindowShots.OnHiddenDesktop("look-preview", (gfx, instance) =>
        {
            const uint dpi = 144;
            foreach (var dark in new[] { true, false })
            {
                var look = AddListPictures.LookFor(dark) with { Animations = false };
                foreach (var scene in PreviewScenes)
                {
                    var config = MenuCorpus.Fresh();
                    scene.Odd(config);
                    var file = $"preview-{scene.Name}-{(dark ? "dark" : "light")}.png";
                    var layout = PictureLayout(LayoutType.Taskbar, Edge.Bottom);
                    EditedDisplay(gfx, instance, dpi, new Config(), look, layout, file, dips: new Vector2(1920f, 1080f), panel: deck =>
                    {
                        var (content, page, open) = PreviewContent(config, dark);
                        deck.Memory = new PanelMemory();
                        deck.Memory.Show(page);
                        deck.Show(gfx, look, dpi / 96f, deck.Room!, deck.State ?? PictureState(), content);
                        deck.Land(page, open, light: false);
                        // §592: its one preview, its sample drawn once.
                        var line = deck.Layout!.Blocks.SelectMany(block => block.Lines).Single(each => each.Row.Kind == PanelRowKind.Preview);
                        Assert.False(deck.Layout.OnPanel(line.Rect) is var shown && (shown.Bottom > deck.Layout.Viewport.Bottom || shown.Y < deck.Layout.Viewport.Y), $"{file}: the preview is out of sight");
                        Assert.Equal(line.Row.Key, Assert.Single(deck.Previews!.Keys).Key);
                        Assert.Equal(1, deck.Previews.Drawn);
                        LinesWhole(deck, line, file);
                    });
                }
            }
        });
    }

    /// <summary>
    /// §571: the folder's lines the whole look's sample draws (§592) stand whole in its room, clear of its every edge: nothing of
    /// them cut. Its room, px.
    /// </summary>
    private static Vector2 LinesWhole(EditDeck deck, PanelLine line, string file)
    {
        var (room, lines) = deck.Previews!.Rooms[line.Row.Key];
        Assert.True(lines is { } at && at.X >= 0 && at.Y > 0 && at.Right <= room.X && at.Bottom < room.Y, $"{file}: the lines {lines} cut by their room {room}");
        return room;
    }

    /// <summary>
    /// §571: the Widgets in folders' and the Flyouts' samples hold the folder's lines whole at every text size: Windows' text
    /// at 150 % lays the lines out taller, and the room grows with them — never a guessed height that cuts them.
    /// </summary>
    [UiFact]
    public void AFoldersLinesStandWholeInTheirRoomAtEveryTextSize()
    {
        WindowShots.OnHiddenDesktop("look-preview-lines", (gfx, instance) =>
        {
            const uint dpi = 144;
            foreach (var scene in PreviewScenes)
            {
                List<float> rooms = [];
                foreach (var text in new[] { 1f, 1.5f })
                {
                    var look = AddListPictures.LookFor(dark: true) with { Animations = false, TextScale = text };
                    var config = MenuCorpus.Fresh();
                    var file = $"preview-{scene.Name}-text{text * 100:0}-dark.png";
                    EditedDisplay(gfx, instance, dpi, new Config(), look, PictureLayout(LayoutType.Taskbar, Edge.Bottom), file, dips: new Vector2(1920f, 2000f), panel: deck =>
                    {
                        var (content, page, open) = PreviewContent(config, dark: true);
                        deck.Memory = new PanelMemory();
                        deck.Memory.Show(page);
                        deck.Show(gfx, look, dpi / 96f, deck.Room!, deck.State ?? PictureState(), content);
                        deck.Land(page, open, light: false);
                        var line = deck.Layout!.Blocks.SelectMany(block => block.Lines).Single(each => each.Row.Kind == PanelRowKind.Preview);
                        rooms.Add(LinesWhole(deck, line, file).Y);
                    });
                }
                Assert.True(rooms[1] > rooms[0], $"{scene.Name}: the room {rooms[0]} px at 100 % text, {rooms[1]} px at 150 %");
            }
        });
    }

    /// <summary>§582: whether column <paramref name="x"/> of a picture <paramref name="wide"/> px across lies on one side, well clear of the slanted line: its outer 30 %.</summary>
    private static bool Side(int x, int wide, bool left) => left ? x < wide * 3 / 10 : x >= wide * 7 / 10;

    /// <summary>§582: how bright one side of <paramref name="pixels"/> (BGRA, <paramref name="wide"/> px across) is, on average (0 to 255).</summary>
    private static double Brightness(byte[] pixels, int wide, bool left)
    {
        var (sum, count) = (0d, 0);
        for (var at = 0; at < pixels.Length; at += 4)
        {
            if (Side(at / 4 % wide, wide, left))
            {
                sum += (pixels[at] + pixels[at + 1] + pixels[at + 2]) / 3d;
                count++;
            }
        }
        return count == 0 ? 0 : sum / count;
    }

    /// <summary>§582: how many px of one side differ between <paramref name="was"/> and <paramref name="now"/> (BGRA, <paramref name="wide"/> px across).</summary>
    private static int Changed(byte[] was, byte[] now, int wide, bool left)
    {
        Assert.Equal(was.Length, now.Length);
        var changed = 0;
        for (var at = 0; at < was.Length; at += 4)
        {
            if (Side(at / 4 % wide, wide, left) && Math.Abs(was[at] - now[at]) + Math.Abs(was[at + 1] - now[at + 1]) + Math.Abs(was[at + 2] - now[at + 2]) > 24)
            {
                changed++;
            }
        }
        return changed;
    }

    /// <summary>
    /// §536: a look's value changed while its preview stands open draws its sample again, once, and what it shows is
    /// another picture; a scroll and the pointer moving over it draw nothing again. §613 drawn in the taskbar's one mode: the
    /// other mode's half of a colour changed changes nothing seen; the Mode made Light, that half shows.
    /// </summary>
    [UiFact]
    public void ALookChangedDrawsItsPreviewAgainOnceAndNothingElseDoes()
    {
        WindowShots.OnHiddenDesktop("look-preview-redraw", (gfx, instance) =>
        {
            const uint dpi = 144;
            var look = AddListPictures.LookFor(dark: true) with { Animations = false };
            var config = MenuCorpus.Fresh();
            EditedDisplay(gfx, instance, dpi, new Config(), look, PictureLayout(LayoutType.Taskbar, Edge.Bottom), "preview-redraw-dark.png", dips: new Vector2(1920f, 1080f), panel: deck =>
            {
                var (content, page, open) = PreviewContent(config, dark: true);
                deck.Memory = new PanelMemory();
                deck.Memory.Show(page);
                deck.Show(gfx, look, dpi / 96f, deck.Room!, deck.State ?? PictureState(), content);
                deck.Land(page, open, light: false);
                var previews = deck.Previews!;
                Assert.Equal(1, previews.Drawn);
                // §613: drawn in the mode the taskbar is in: Windows' (dark) by default.
                Assert.True(Assert.Single(previews.Modes).Value);
                var wide = 0;
                byte[] Picture()
                {
                    WindowShots.Settle(200);
                    var hit = new PanelHit(PanelHitKind.Preview, Key: deck.Layout!.Blocks.SelectMany(block => block.Lines).Single(each => each.Row.Kind == PanelRowKind.Preview).Row.Key);
                    var at = deck.OnScreen(hit);
                    var extent = WindowShots.Extent(deck.FlyoutHwnd);
                    var part = new RECT { left = at.left - extent.left + 4, top = at.top - extent.top + 4, right = at.right - extent.left - 4, bottom = at.bottom - extent.top - 4 };
                    byte[] pixels = [];
                    Assert.True(WindowShots.Save(deck.FlyoutHwnd, "preview-redraw-sample.png", part, (seen, width) => (pixels, wide) = (seen, width)));
                    return pixels;
                }
                var before = Picture();
                // The pointer over it and the page scrolled a little and back: nothing drawn again.
                var middle = deck.Layout!.RectOf(new PanelHit(PanelHitKind.Preview, Key: Assert.Single(previews.Keys).Key));
                _ = deck.Message(deck.FlyoutHwnd, WM.WM_MOUSEMOVE, 0, EditDeckClickTests.At(deck, middle.Center, flyout: true));
                _ = deck.Message(deck.FlyoutHwnd, WM.WM_MOUSEWHEEL, (nuint)(uint)(120 << 16), 0);
                _ = deck.Message(deck.FlyoutHwnd, WM.WM_MOUSEWHEEL, unchecked((nuint)(uint)(-120 << 16)), 0);
                _ = deck.Message(deck.FlyoutHwnd, WM.WM_MOUSEWHEEL, unchecked((nuint)(uint)(-120 << 16)), 0);
                Assert.Equal(1, previews.Drawn);
                // Its cards' background another colour: its rows made again, its sample drawn again once, another picture.
                Assert.True(LookShorthand.Both(Looks.Section(config, LookPart.Widgets), Tone.Background, Karpo.Core.Paint.Parse("#E74856")));
                var (again, _, _) = PreviewContent(config, dark: true);
                deck.Show(gfx, look, dpi / 96f, deck.Room!, deck.State ?? PictureState(), again, page);
                Assert.Equal(2, previews.Drawn);
                var after = Picture();
                Assert.Equal(before.Length, after.Length);
                var differ = 0;
                for (var at = 0; at < before.Length; at += 4)
                {
                    if (Math.Abs(before[at] - after[at]) + Math.Abs(before[at + 1] - after[at + 1]) + Math.Abs(before[at + 2] - after[at + 2]) > 24)
                    {
                        differ++;
                    }
                }
                Assert.True(differ > before.Length / 4 / 50, $"only {differ} of {before.Length / 4} px changed");
                // Shown again as it is: nothing drawn.
                deck.Show(gfx, look, dpi / 96f, deck.Room!, deck.State ?? PictureState(), again, page);
                Assert.Equal(2, previews.Drawn);
                // §613: the light half of the bar's background alone changed, Windows dark: drawn again once, nothing of it seen.
                Assert.True(Looks.WritePaint(Looks.Section(config, LookPart.Taskbar), Tone.Background, false, Karpo.Core.Paint.Parse("#E74856")));
                var (light, _, _) = PreviewContent(config, dark: true);
                deck.Show(gfx, look, dpi / 96f, deck.Room!, deck.State ?? PictureState(), light, page);
                Assert.Equal(3, previews.Drawn);
                var same = Picture();
                Assert.Equal(0, Changed(after, same, wide, left: true) + Changed(after, same, wide, left: false));
                // The Mode made Light: the sample in the light mode, its bar red.
                Assert.True(Looks.WriteMode(config, TaskbarMode.Light));
                var (lit, _, _) = PreviewContent(config, dark: true);
                deck.Show(gfx, look, dpi / 96f, deck.Room!, deck.State ?? PictureState(), lit, page);
                Assert.Equal(4, previews.Drawn);
                Assert.False(Assert.Single(previews.Modes).Value);
                var red = Picture();
                Assert.True(Changed(after, red, wide, left: true) > 100 && Changed(after, red, wide, left: false) > 100,
                    $"{Changed(after, red, wide, left: true)} | {Changed(after, red, wide, left: false)} px changed");
            });
        });
    }
}
