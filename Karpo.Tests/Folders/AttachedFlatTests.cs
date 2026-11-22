// An attached folder (§647) flat on its ground (§649; the person, 2026-11-09, after hearing §647's plan: "the widgets inside
// should not keep their own cards.", "the background should be configurable in the settings.", "dividers become an outline
// of the folder"). Held here:
//
//  - **The setting** (pure): the Appearance's Flyouts › "Attached folder background" (Tone.Ground), a colour pair as every
//    colour, last of the Flyouts'; unset in Karpo's own and every palette's design, which is the bar's colour, solid — the row
//    shows that; no None; swatches the backgrounds; written in the half of the mode shown, read back, reset; tried on as
//    every choice of the Appearance (§622). A file of shape 84 has none: the bar's colour, as it stood.
//  - **The look** (pure): on its ground a folder's lines have no card and no pattern; their text reads on the ground; their
//    highlight laid on it; the menus opened from it as ever. The ground the colour said, as said, or the bar's.
//  - **A divider** (pure): a line of the folder's outline, its weight, across its room's whole width, in its middle; the theme's
//    divider where the outline is None.
//  - **The open folder** (KARPO_UI_TESTS=1): attached, every line's fill clear at rest, no blur, no rim, no rounding; the
//    pixels of a line's corner and edge the ground's; the divider a line of the outline across its column; a line under the
//    pointer lit from its left edge to its right one, the ground beside it as it was. As Cards, every line on its card, every
//    list's rim, its corners round, no divider line. The ground's colour tried on recolours the open folder, its lines with it,
//    and taken off puts it back.

using System.Numerics;
using Karpo.Dock;
using Karpo.Tests.Core;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using Karpo.Tests.Menus;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using CardFill = Karpo.Dock.FolderFlyout.CardFill;
using Color = Windows.UI.Color;
using Edge = Karpo.Core.Edge;

namespace Karpo.Tests.Folders;

public sealed class AttachedFlatTests
{
    private const string Owner = "layout:default";

    private static readonly string Key = LookRows.ColourKey(LookRows.SectionKey(LookPart.Flyouts), Tone.Ground);

    /// <summary>A dark blue a capture can tell from anything Karpo draws of its own.</summary>
    // §690: a preset every picker has (Windows' blue).
    private static readonly Argb Blue = ColorPalette.Presets[5];

    private static OptionRow Row(Config config, bool dark) => OptionRows.Find(LookRows.Rows(config, config.DefaultLayout, dark), Key)!;

    // --- The setting -----------------------------------------------------------------------------

    /// <summary>
    /// The Flyouts' last colour, none of another section's; unset in Karpo's own and every palette, the row showing the bar's
    /// colour solid; no None; the backgrounds; a pick written in the half of the mode shown, read back unchanged, its reset
    /// beside it; reset, the bar's again and nothing left in the file.
    /// </summary>
    [Fact]
    public void TheAttachedGroundIsAFlyoutsColourThatFollowsTheBarByDefault()
    {
        Assert.Equal([Tone.Outline, Tone.Backdrop, Tone.Ground], LookParts.TonesOf(LookPart.Flyouts));
        foreach (var part in new[] { LookPart.Taskbar, LookPart.Widgets, LookPart.FolderWidgets })
        {
            Assert.DoesNotContain(Tone.Ground, LookParts.TonesOf(part));
        }
        Assert.False(ToneNames.MayBeNone(Tone.Ground));
        Assert.Equal("ground", ToneNames.JsonName(Tone.Ground));
        Assert.Equal(S.SettingsLookGround, ToneNames.Name(Tone.Ground));
        foreach (var palette in LookPalettes.Choices)
        {
            foreach (var dark in new[] { true, false })
            {
                Assert.True(palette.Drawn(dark).Flyouts.Ground.IsWindows, $"{palette.Id} says a ground");
            }
        }

        var config = MenuCorpus.Fresh();
        var flyouts = OptionRows.Find(LookRows.Rows(config, config.DefaultLayout, true), LookRows.SectionKey(LookPart.Flyouts) + LookRows.Tab)!;
        Assert.Equal(Key, flyouts.Level[^1].Key);
        foreach (var dark in new[] { true, false })
        {
            var row = Row(config, dark);
            Assert.Equal((OptionRowKind.Color, S.SettingsLookGround), (row.Kind, row.Title));
            // Unset: the bar's colour, solid — Karpo's own bar its #202020 (dark) or #F3F3F3 (light), whatever its opacity.
            var bar = Looks.Of(config, dark).Taskbar.Background.Color!.Value;
            Assert.Equal(bar with { A = 255 }, row.Color);
            Assert.Equal(Looks.Ground(Looks.Of(config, dark), dark), row.Color);
            Assert.DoesNotContain(row.Tiles, tile => tile.Plain);
            // §690: the presets every picker has.
            Assert.Equal(ColorPalette.Presets, row.Tiles.Select(tile => tile.Color!.Value));
            Assert.Null(OptionRows.Find(LookRows.Rows(config, config.DefaultLayout, dark), Key + LookRows.ResetEnd));
        }

        // The dark blue picked in the dark mode: there alone, opaque, as the bar's opacity is the bar's.
        var at = Row(config, dark: true).Tiles.ToList().FindIndex(tile => tile.Color == Blue);
        Assert.True(Row(config, dark: true).Set!(at));
        Assert.Equal(Blue, Looks.Of(config, dark: true).Flyouts.Ground.Color);
        Assert.Equal(Blue, Looks.Ground(Looks.Of(config, dark: true), dark: true));
        Assert.True(Looks.Of(config, dark: false).Flyouts.Ground.IsWindows);
        Assert.Equal(Blue, Row(config, dark: true).Color);
        Assert.Contains(S.SettingsLookResetValue(S.SettingsLookGround), OptionRows.Find(LookRows.Rows(config, config.DefaultLayout, true), Key + LookRows.ResetEnd)!.Detail!, StringComparison.Ordinal);

        var json = config.ToJson();
        Assert.Contains("\"ground\": {", json, StringComparison.Ordinal);
        Assert.Contains("\"dark\": \"#0078D4\"", json, StringComparison.Ordinal);
        var read = Configs.RoundTrip(config, out var changed);
        Assert.False(changed);
        Assert.Equal(new ColorPair { Dark = "#0078D4" }, read.Appearance![LookPalettes.KarpoId]!.Flyouts!.Ground);

        // Its reset: unset, the bar's again, nothing left.
        Assert.True(OptionRows.Find(LookRows.Rows(read, read.DefaultLayout, true), Key + LookRows.ResetEnd)!.Set!(0));
        Assert.Null(read.Appearance);
        Assert.True(Looks.Of(read, dark: true).Flyouts.Ground.IsWindows);

        // Said anywhere but the Flyouts it is not kept; a half that does not read is dropped.
        var odd = Configs.Repaired("""
            { "version": 85, "appearance": { "palettes": { "karpo": {
              "taskbar": { "ground": { "dark": "#123456" } },
              "flyouts": { "ground": { "dark": "#80102030", "light": "dusk" } } } } } }
            """);
        Assert.Null(odd.Appearance![LookPalettes.KarpoId]!.Taskbar);
        Assert.Equal(new ColorPair { Dark = "#80102030" }, odd.Appearance[LookPalettes.KarpoId]!.Flyouts!.Ground);
        // Its opacity its own.
        Assert.Equal(new Argb(0x80, 0x10, 0x20, 0x30), Looks.Of(odd, dark: true).Flyouts.Ground.Color);
    }

    /// <summary>The file's shape 85 or later; a file of 84 has no ground, which is the bar's colour — what every attached folder stood on.</summary>
    [Fact]
    public void AFileOfShape84ReadsWithTheBarsColour()
    {
        Assert.InRange(Config.Shape, 85, int.MaxValue);
        var older = Configs.Repaired("""{ "version": 84, "appearance": { "palette": "graphite" } }""");
        Assert.Equal(LookPalettes.GraphiteId, older.Appearance!.Palette);
        foreach (var dark in new[] { true, false })
        {
            Assert.True(Looks.Of(older, dark).Flyouts.Ground.IsWindows);
            var bar = AddListPictures.LookFor(dark).Wearing(older);
            Assert.Equal(FolderFlyout.AttachedGround(bar with { Worn = bar.Worn with { Flyouts = default } }), FolderFlyout.AttachedGround(bar));
        }
    }

    /// <summary>Its swatches are choices of the Appearance: tried on under the pointer (§622), as every colour's are.</summary>
    [Fact]
    public void TheGroundsSwatchesAreTriedOn()
    {
        var config = MenuCorpus.Fresh();
        List<OptionPick> picks = [];
        _ = OptionMenu.Rows(LookRows.Rows(config, config.DefaultLayout, windows: true), picks);
        var swatches = picks.Where(pick => pick.Key == Key && pick.Kind == OptionPickKind.Set).ToList();
        Assert.Equal(Row(config, dark: true).Tiles.Count, swatches.Count);
        Assert.True(LookRows.TriedOn(Key));
    }

    // --- The look --------------------------------------------------------------------------------

    /// <summary>
    /// The ground the colour said, as said (its opacity too), the bar's where none is, never in high contrast; on it the lines'
    /// card is the ground — drawn by nothing of theirs —, no pattern, their text 4.5:1 on it; the menus opened from the
    /// folder as from any.
    /// </summary>
    [Fact]
    public void OnItsGroundAFoldersLinesReadOnTheGround()
    {
        foreach (var dark in new[] { true, false })
        {
            var config = new Config();
            var plain = AddListPictures.LookFor(dark).Wearing(config);
            // A pattern on the lines, which the ground has none of.
            Assert.True(Looks.WritePattern(Looks.Section(config, LookPart.FolderWidgets), Pattern.Dots));
            Assert.True(Looks.WritePaint(Looks.Section(config, LookPart.Flyouts), Tone.Ground, dark, new Karpo.Core.Paint(new Argb(255, 0xF2, 0xC2, 0xC2))));
            var bar = AddListPictures.LookFor(dark).Wearing(config);
            var ground = FolderFlyout.AttachedGround(bar);
            Assert.Equal(Color.FromArgb(255, 0xF2, 0xC2, 0xC2), ground);
            Assert.NotEqual(FolderFlyout.AttachedGround(plain), ground);
            // High contrast: Windows' system colours alone, whatever is said.
            Assert.Equal(FolderFlyout.AttachedGround(plain with { HighContrast = true }), FolderFlyout.AttachedGround(bar with { HighContrast = true }));

            var worn = FolderFlyout.Wearing(bar, ground);
            Assert.Equal(new Argb(255, 0xF2, 0xC2, 0xC2), worn.Colors.Card);
            Assert.Null(worn.Colors.CardPattern);
            Assert.NotNull(FolderFlyout.Wearing(bar, null).Colors.CardPattern);
            var text = Assert.IsType<Argb>(worn.Ink) with { A = 255 };
            Assert.True(ToneRules.Contrast(text, new Argb(255, 0xF2, 0xC2, 0xC2)) >= ToneRules.TextContrast, $"text #{text.R:X2}{text.G:X2}{text.B:X2} on the pink ground");
            // The menus opened from it: the same as from a folder of cards.
            Assert.Equal(FolderFlyout.Wearing(bar, null).Colors.Flyout, worn.Colors.Flyout);
            Assert.Equal(FolderFlyout.Wearing(bar, null).Colors.PanelText, worn.Colors.PanelText);

            // A line on it: clear at rest, no blur, its highlight laid on the ground under the pointer.
            var theme = FlyoutTheme.Folder(worn);
            Assert.Equal(0, FolderFlyout.InkOf(theme, CardFill.None, worn.Colors, carded: false).A);
            Assert.Equal(default, FolderFlyout.LineFill(theme, worn.Colors, carded: false));
            var lit = FolderFlyout.InkOf(theme, CardFill.LineHover, worn.Colors, carded: false);
            var highlight = worn.Colors.Highlight!.Value;
            Assert.Equal(Color.FromArgb(highlight.A, highlight.R, highlight.G, highlight.B), lit);
        }
    }

    // --- A divider -------------------------------------------------------------------------------

    /// <summary>Across its room's whole width, the outline's weight, in its middle on whole px; in the outline's colour, the theme's divider where that is None.</summary>
    [Fact]
    public void ADividerIsALineOfTheOutlineAcrossItsRoom()
    {
        var room = new D2D_RECT_F { left = 18, top = 120, right = 438, bottom = 129 };
        Assert.Equal(new D2D_RECT_F { left = 18, top = 124, right = 438, bottom = 126 }, FolderFlyout.RuleOf(room, 2));
        Assert.Equal(new D2D_RECT_F { left = 18, top = 124, right = 438, bottom = 125 }, FolderFlyout.RuleOf(room, 1));
        // A divider that grows: in the middle of all it grows to.
        var grown = FolderFlyout.RuleOf(room with { bottom = 420 }, 2);
        Assert.Equal((269f, 271f), (grown.top, grown.bottom));

        var look = AddListPictures.LookFor(dark: true).Wearing(new Config()).Flyout();
        var theme = FlyoutTheme.Folder(look);
        Assert.True(theme.Stroke.A > 0);
        Assert.Equal(theme.Stroke, FolderFlyout.RuleInk(theme));
        var none = theme with { Stroke = Color.FromArgb(0, 0, 0, 0) };
        Assert.Equal(theme.Divider, FolderFlyout.RuleInk(none));
    }

    // --- The open folder -------------------------------------------------------------------------

    private static readonly RECT Display = new() { right = 1920, bottom = 1080 };

    /// <summary>"a - b | c": a column of Task view's lines, "-" a divider, "|" the next column.</summary>
    internal static BarItem Shaped(string shape, FolderStyle style)
    {
        var island = BarKind.FolderList.New();
        island.Id = "flat";
        island.Folder!.Style = style;
        island.Folder.Lists.Clear();
        island.Folder.Widths?.Clear();
        foreach (var part in shape.Split('|'))
        {
            island.Folder.Lists.Add([.. part.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select((id, at) =>
            {
                var line = id == "-" ? ListKind.Divider.New() : ListKind.TaskView.New();
                line.Id = id == "-" ? $"divider{at}" : id;
                return new FolderCell { Island = line };
            })]);
        }
        return island;
    }

    /// <summary>The colour of the pixel at <paramref name="x"/>, <paramref name="y"/> of a capture (BGRA).</summary>
    private static (int R, int G, int B) At((byte[] Pixels, int Width) shot, int x, int y)
    {
        var at = ((y * shot.Width) + x) * 4;
        return (shot.Pixels[at + 2], shot.Pixels[at + 1], shot.Pixels[at]);
    }

    private static bool Near((int R, int G, int B) one, (int R, int G, int B) other, int by = 2) =>
        Math.Abs(one.R - other.R) <= by && Math.Abs(one.G - other.G) <= by && Math.Abs(one.B - other.B) <= by;

    private static (int R, int G, int B) Rgb(Color color) => (color.R, color.G, color.B);

    /// <summary><paramref name="over"/> laid on the opaque <paramref name="under"/>.</summary>
    private static (int R, int G, int B) Over(Color over, Color under)
    {
        var mixed = ToneRules.Blend(new Argb(over.A, over.R, over.G, over.B), new Argb(255, under.R, under.G, under.B));
        return (mixed.R, mixed.G, mixed.B);
    }

    /// <summary>
    /// A real folder of two columns, a divider in the first, on a left bar at 150 % and a bottom one at 100 %, the dark blue its
    /// ground: attached, every line's fill clear at rest and no blur, no rim, no corner rounded; a line's corner and its edge
    /// the ground's own pixels; the divider a line of the outline across its column, in its room's middle; the pointer on the
    /// last line lights it from its left edge to its right one and nothing beside it. As Cards, every line on its card, a rim
    /// round every list, round corners, no divider line.
    /// </summary>
    [UiTheory]
    [InlineData(Edge.Left, 1.5f)]
    [InlineData(Edge.Bottom, 1f)]
    public void AttachedLinesStandFlatOnTheGroundAndLightEdgeToEdge(Edge edge, float scale)
    {
        WindowShots.OnHiddenDesktop("folder-flat", (gfx, instance) =>
        {
            var config = new Config();
            Assert.True(Looks.WritePaint(Looks.Section(config, LookPart.Flyouts), Tone.Ground, dark: true, new Karpo.Core.Paint(Blue)));
            var look = AddListPictures.LookFor(dark: true).Wearing(config) with { Animations = false };
            var bar = (int)MathF.Round(48 * scale);
            var work = edge == Edge.Left ? Display with { left = bar } : Display with { bottom = Display.bottom - bar };
            var anchor = edge == Edge.Left ? new POINT { x = work.left, y = 500 } : new POINT { x = 900, y = work.bottom };
            var ground = Color.FromArgb(255, Blue.R, Blue.G, Blue.B);

            // As Cards: as every folder stood.
            using (var cards = new FolderIsland($"{Owner}/flat", Shaped("a - b | c", FolderStyle.Cards), gfx, config))
            using (var flyout = new FolderFlyout(gfx, instance, 0))
            {
                flyout.Show(gfx, look, scale, anchor, work, edge, cards, display: Display);
                WindowShots.Settle();
                Assert.Null(flyout.GroundShown);
                Assert.False(flyout.Flat);
                Assert.Equal(MathF.Round(8 * scale), flyout.Plan!.Radius);
                var (lines, rims) = flyout.DressShown;
                Assert.All(rims, rim => Assert.True(rim.A > 0, "a list of cards without its rim"));
                Assert.All(new[] { 0, 2, 3 }, index => Assert.True(lines[index].Fill.A > 0, $"line {index} without its card"));
                Assert.All(lines, line => Assert.Null(line.Rule));
                flyout.Hide();
            }

            using var folder = new FolderIsland($"{Owner}/flat", Shaped("a - b | c", FolderStyle.Attached), gfx, config);
            using var attached = new FolderFlyout(gfx, instance, 0);
            attached.Show(gfx, look, scale, anchor, work, edge, folder, display: Display);
            WindowShots.Settle(300);
            Assert.True(attached.Flat);
            Assert.Equal(ground, attached.GroundShown!.Value.Color);
            var plan = attached.Plan!;
            Assert.Equal(0, plan.Radius);
            var (flat, none) = attached.DressShown;
            Assert.All(none, rim => Assert.Equal(0, rim.A));
            Assert.All(flat, line => Assert.Equal((0, false), (line.Fill.A, line.Blur)));
            // The divider: across its column, the outline's weight, in its room's middle; no other line has one.
            var rule = Assert.IsType<D2D_RECT_F>(flat[1].Rule);
            var room = plan.Cards[1];
            var weight = MathF.Max(MathF.Round(scale), 1);
            Assert.Equal(FolderFlyout.RuleOf(room, rule.bottom - rule.top), rule);
            Assert.Equal((room.left, room.right), (rule.left, rule.right));
            Assert.InRange(rule.bottom - rule.top, 1, weight);
            Assert.All(new[] { 0, 2, 3 }, index => Assert.Null(flat[index].Rule));

            // The pixels: a line's corner and its edges the ground's; the divider's middle the outline over the ground.
            (byte[] Pixels, int Width) rest = default;
            Assert.True(WindowShots.Save(attached.Hwnd, $"folder-flat-{edge.ToString().ToLowerInvariant()}-{scale * 100:0}-rest.png", default, (pixels, width) => rest = (pixels, width)));
            var extent = WindowShots.Extent(attached.Hwnd);
            var a = attached.ScreenRectOfCard(0);
            var c = attached.ScreenRectOfCard(3);
            // §651: the lines flush with the ground's far side, where its line of the outline runs over them.
            var edgeWeight = (int)weight;
            var (inTop, inRight) = edge == Edge.Bottom ? (edgeWeight, 0) : (0, edgeWeight);
            foreach (var (x, y, what) in new[] { (a.left, a.top + inTop, "a's top left corner"), (a.right - 1, a.top + inTop, "a's top right corner"), (c.left, c.bottom - 1, "c's bottom left corner"), (c.right - 1 - inRight, c.top + 2, "c's right edge") })
            {
                var seen = At(rest, x - extent.left, y - extent.top);
                Assert.True(Near(seen, Rgb(ground)), $"{what}: {seen}, not the ground {Rgb(ground)}");
            }
            var far = edge == Edge.Bottom ? At(rest, a.left + 4 - extent.left, a.top - extent.top) : At(rest, c.right - 1 - extent.left, c.top + 2 - extent.top);
            Assert.True(Near(far, Over(attached.Theme.Stroke, ground), 3), $"the ground's far side: {far}, not its outline over the ground {Over(attached.Theme.Stroke, ground)}");
            var line = attached.ScreenRectOfCard(1);
            var middle = (line.top + line.bottom) / 2;
            var rimInk = FolderFlyout.RuleInk(FlyoutTheme.Folder(FolderFlyout.Wearing(look, ground)));
            var drawn = At(rest, ((line.left + line.right) / 2) - extent.left, (int)(attached.PanelOnScreen.top + rule.top) - extent.top);
            Assert.True(Near(drawn, Over(rimInk, ground), 3), $"the divider's line: {drawn}, not the outline over the ground {Over(rimInk, ground)}");

            // The pointer on b (its middle): lit from its left edge to its right one, the ground beside it as it was.
            var b = attached.ScreenRectOfCard(2);
            var on = WindowShots.Client(attached.Hwnd, ((b.left + b.right) / 2) - extent.left, ((b.top + b.bottom) / 2) - extent.top);
            attached.Message(WM.WM_MOUSEMOVE, 0, (nint)(((on.y & 0xFFFF) << 16) | (on.x & 0xFFFF)));
            WindowShots.Settle(300);
            Assert.Equal(2, attached.Lit.Hot);
            var lit = attached.DressShown.Lines[2].Fill;
            Assert.True(lit.A > 0, "b under the pointer does not light");
            (byte[] Pixels, int Width) hovered = default;
            Assert.True(WindowShots.Save(attached.Hwnd, $"folder-flat-{edge.ToString().ToLowerInvariant()}-{scale * 100:0}-hover.png", default, (pixels, width) => hovered = (pixels, width)));
            var top = b.top + 2 - extent.top;
            var (left, right) = (At(hovered, b.left - extent.left, top), At(hovered, b.right - 1 - extent.left, top));
            Assert.True(Near(left, Over(lit, ground)) && Near(right, Over(lit, ground)), $"b lit {left} at its left edge, {right} at its right one, not {Over(lit, ground)}");
            var beside = edge == Edge.Bottom ? At(hovered, b.left - 2 - extent.left, top) : At(hovered, a.left + 4 - extent.left, a.top - 3 - extent.top);
            Assert.True(Near(beside, Rgb(ground)), "the ground beside b lit");
            Assert.True(Near(At(hovered, a.left - extent.left, a.top + 2 - extent.top), Rgb(ground)), "a lit with b");
            attached.Hide();
        });
    }

    /// <summary>
    /// The ground's colour tried on while an attached folder stands open (§622: a copy of the config worn, every folder open
    /// relooked) recolours its ground and its lines' look at once; taken off, both as they were.
    /// </summary>
    [UiFact]
    public void TheGroundsColourTriedOnRecoloursTheOpenFolder()
    {
        WindowShots.OnHiddenDesktop("folder-flat-try", (gfx, instance) =>
        {
            const float scale = 1.5f;
            var config = new Config();
            var look = AddListPictures.LookFor(dark: true).Wearing(config) with { Animations = false };
            var work = Display with { left = 72 };
            using var folder = new FolderIsland($"{Owner}/flat", Shaped("a - b", FolderStyle.Attached), gfx, config);
            using var flyout = new FolderFlyout(gfx, instance, 0);
            flyout.Show(gfx, look, scale, new POINT { x = 72, y = 500 }, work, Edge.Left, folder, display: Display);
            WindowShots.Settle();
            var bars = flyout.GroundShown!.Value.Color;
            Assert.Equal(FolderFlyout.AttachedGround(look), bars);

            // Tried on: a copy with the pink worn.
            var copy = Configs.RoundTrip(config, out _);
            Assert.True(Looks.WritePaint(Looks.Section(copy, LookPart.Flyouts), Tone.Ground, dark: true, new Karpo.Core.Paint(new Argb(255, 0xF2, 0xC2, 0xC2))));
            flyout.Relook(AddListPictures.LookFor(dark: true).Wearing(copy) with { Animations = false });
            WindowShots.Settle();
            Assert.Equal(Color.FromArgb(255, 0xF2, 0xC2, 0xC2), flyout.GroundShown!.Value.Color);
            Assert.All(flyout.DressShown.Lines, line => Assert.Equal(0, line.Fill.A));

            // Taken off: the bar's again.
            flyout.Relook(look);
            WindowShots.Settle();
            Assert.Equal(bars, flyout.GroundShown!.Value.Color);
            flyout.Hide();
        });
    }
}
