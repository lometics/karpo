// The actions line (Karpo.Dock\Islands\ActionsTile.cs, docs\widgets.md §68): two text buttons in the
// text colour, the two names shared fairly where both do not fit, and one row of the list always.
//
// The person, 2026-10-10: "the actions widget should show both actions as text, additionally it should
// show it in normal color and not primary color. additionally it should not even have a resizing
// option as it should be one row height all the time."

using System.Text.Json;
using Karpo.Dock;
using Karpo.Dock.Native;
using Karpo.Dock.QuickSettings.Controls;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using static Karpo.Tests.Folders.ListFormPictures;
using static Karpo.Tests.Islands.Offscreen;

namespace Karpo.Tests.Folders;

public sealed unsafe class ActionsLineTests
{
    private static CellItem Actions(string? left, string? right) => Line(ListKind.Actions, line =>
    {
        line.Actions!.Left = left;
        line.Actions.Right = right;
    });

    // --- The two names' share ------------------------------------------------------------------------

    [Fact]
    public void BothNamesWholeWhereTheyFit() => Assert.Equal((40f, 60f), ActionsTile.Split(40, 60, 100));

    /// <summary>The shorter, under half, keeps every word; the other is cut to the rest.</summary>
    [Fact]
    public void TheShorterKeepsItsWordsAndTheOtherIsCut()
    {
        Assert.Equal((30f, 70f), ActionsTile.Split(30, 200, 100));
        Assert.Equal((70f, 30f), ActionsTile.Split(200, 30, 100));
    }

    [Fact]
    public void TwoLongNamesHaveHalfEach()
    {
        Assert.Equal((50f, 50f), ActionsTile.Split(80, 90, 100));
        Assert.Equal((0f, 0f), ActionsTile.Split(80, 90, -5));
    }

    // --- One row, nothing to set ---------------------------------------------------------------------

    /// <summary>
    /// The kind says it is always its one row (FixedHeight): it has no height to choose, so Settings
    /// shows no height card, and a height a file holds is dropped as the line is read.
    /// </summary>
    [Fact]
    public void TheActionsLineHasNoHeightToSet()
    {
        Assert.True(ListWidgets.Of(ListKind.Actions).FixedHeight);
        Assert.Equal(LineHeight.Row, ListWidgets.Of(ListKind.Actions).Cells);
        Assert.False(ListKind.Actions.HasHeight());
        Assert.Null(LineHeightRows.For(ListKind.Actions.New()));
        Assert.Equal(LineHeight.Exactly(2), LineHeight.StartFor(ListKind.Actions));

        const string json = """{"id":"a1","kind":"actions","height":{"fixed":false,"cells":12},"actions":{"left":"dateTime","right":"wifi"}}""";
        var line = JsonSerializer.Deserialize(json, ConfigJson.Default.CellItem)!;
        line.View = FolderView.List;
        line.Normalize();
        Assert.Null(line.Height);
        Assert.Equal(LineHeight.Exactly(2), LineHeight.For(line));
        Assert.DoesNotContain("height", JsonSerializer.Serialize(line, ConfigJson.Default.CellItem), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("wifi", line.Actions!.Right);

        // A height that got onto it anyway is not read.
        line.Height = LineHeight.Exactly(10);
        Assert.Equal(LineHeight.Exactly(2), LineHeight.For(line));
    }

    /// <summary>A kind that does not opt in keeps its card: the flag is the kind's, not every line's.</summary>
    [Fact]
    public void OnlyAKindThatSaysSoIsFixed() =>
        Assert.All(ListWidgets.All.Where(widget => widget.FixedHeight), widget => Assert.False(widget.Kind.HasHeight()));

    /// <summary>The open folder gives the line exactly one row, at 100 % and 225 % text, whatever height it carries.</summary>
    [UiFact]
    public void TheLineIsOneRowWhateverItCarries() =>
        OnStaThread(gfx =>
        {
            foreach (var textScale in TextScales)
            {
                var line = Actions("dateTime", "notifications");
                line.Height = LineHeight.Exactly(12);
                var folder = Folder(gfx, line, Line(ListKind.Toggles));
                var (_, plan) = Plan(folder, LookFor(dark: true, textScale), 1);
                Assert.Equal(Rows(1, 1, textScale), Given(plan, 0), 0.5f);
                folder.Closed();
                folder.Dispose();
            }
        });

    // --- Inside the card ------------------------------------------------------------------------------

    /// <summary>
    /// The person, 2026-10-11: "the action buttons are not properly spaced from the widget borders, they
    /// directly touch it." Each button stands inside the card's padding — on the list's gutter at the
    /// sides, the card's 6 DIP above and below — and a button's gap from the other, at every width,
    /// display scale and text size, and with one slot or both. Each is the lines' one button
    /// (LineButton, §353): its glyph its padding from its start and the words its glyph's gap after it,
    /// its padding from its end; a line too narrow for the names has the glyphs alone.
    /// </summary>
    [UiFact]
    public void TheButtonsKeepTheCardsPadding() =>
        OnStaThread(gfx =>
        {
            foreach (var (one, other) in new[] { ("dateTime", "notifications"), ("notifications", "dateTime"), ("wifi", null), (null, "bluetooth"), ("taskView", "wifi") })
            {
                var folder = Folder(gfx, Actions(one, other));
                var tile = (ActionsTile)folder.Tiles[0].Island;
                foreach (var scale in new[] { 1f, 1.5f })
                {
                    foreach (var textScale in TextScales.Append(1.5f))
                    {
                        foreach (var dark in new[] { true, false })
                        {
                            var look = LookFor(dark, textScale);
                            var style = FolderFlyout.StyleFor(scale, look, FlyoutTheme.For(look));
                            var gutter = ListForm.Gutter(style);
                            var height = tile.ListHeight(0, style);
                            var air = style.Text(6) - 1;
                            foreach (var width in new[] { 120f, 200f, 280f, 360f, 520f })
                            {
                                var box = PopupPaint.Rect(0, 0, width * scale, height);
                                var laid = tile.Lay(box, style);
                                var at = $"{one}|{other} {width * scale:0} px wide at {scale * 100:0} %, {textScale * 100:0} % text";
                                var buttons = new[] { (one, laid.Left), (other, laid.Right) }.Where(slot => slot.Item1 is not null).Select(slot => slot.Item2).ToList();
                                foreach (var button in buttons)
                                {
                                    Assert.True(button.left >= gutter - 0.5f, $"{at}: a button {button.left:0} px from the card's left edge, inside the gutter of {gutter:0}");
                                    Assert.True(button.right <= box.right - gutter + 0.5f, $"{at}: a button {box.right - button.right:0} px from the card's right edge, inside the gutter of {gutter:0}");
                                    Assert.True(button.top >= air && box.bottom - button.bottom >= air, $"{at}: a button {button.top:0} px from the top and {box.bottom - button.bottom:0} from the bottom");
                                }
                                if (one is not null)
                                {
                                    Assert.Equal(gutter, laid.Left.left, 0.5f);
                                }
                                if (other is not null)
                                {
                                    Assert.Equal(box.right - gutter, laid.Right.right, 0.5f);
                                }
                                if (buttons.Count == 2)
                                {
                                    Assert.True(buttons[1].left - buttons[0].right >= LineButton.Of(gfx, style).Gap - 0.5f, $"{at}: the buttons {buttons[1].left - buttons[0].right:0} px apart");
                                }
                                // Each glyph inside its button, in the band's middle; the words after it.
                                foreach (var (slot, button, glyph) in new[] { (one, laid.Left, laid.LeftGlyph), (other, laid.Right, laid.RightGlyph) })
                                {
                                    if (slot is null)
                                    {
                                        continue;
                                    }
                                    if (!laid.Bare || button.right - button.left >= LineButton.Of(gfx, style).GlyphPx)
                                    {
                                        Assert.Equal(LineButton.Of(gfx, style).GlyphPx, glyph.right - glyph.left, 0.5f);
                                    }
                                    Assert.True(glyph.left >= button.left && glyph.right <= button.right && glyph.top >= button.top && glyph.bottom <= button.bottom, $"{at}: a glyph out of its button");
                                    Assert.Equal((button.top + button.bottom) / 2, (glyph.top + glyph.bottom) / 2, 1f);
                                    if (!laid.Bare)
                                    {
                                        Assert.Equal(LineButton.Of(gfx, style).Pad, glyph.left - button.left, 0.5f);
                                    }
                                }
                                if (laid.Bare)
                                {
                                    // No room for the names: each button its glyph alone, square where the line has room
                                    // for it, its name in its tooltip.
                                    Assert.True(laid.LeftCut == (one is not null) && laid.RightCut == (other is not null));
                                    Assert.All(buttons, button => Assert.True(button.right - button.left <= button.bottom - button.top + 0.5f));
                                    continue;
                                }
                                if (one is not null)
                                {
                                    Assert.Equal(laid.LeftGlyph.right + LineButton.Of(gfx, style).GlyphGap, laid.LeftText, 0.5f);
                                }
                                if (other is not null)
                                {
                                    Assert.Equal(LineButton.Of(gfx, style).Pad, laid.Right.right - laid.RightText, 0.5f);
                                }
                            }
                        }
                    }
                }
                folder.Closed();
                folder.Dispose();
            }
        });

    // --- Drawn ---------------------------------------------------------------------------------------

    /// <summary>Both names are drawn in the text colour, dark and light: neither is the accent's link colour.</summary>
    [UiFact]
    public void BothNamesAreInTheTextColour() =>
        OnStaThread(gfx =>
        {
            foreach (var dark in new[] { true, false })
            {
                var look = LookFor(dark);
                var folder = Folder(gfx, Actions("dateTime", "notifications"));
                var (_, plan) = Plan(folder, look, 1);
                var card = plan.Cards[0];
                var (width, height) = (card.right - card.left, card.bottom - card.top);
                var style = FolderFlyout.StyleFor(1, look, FlyoutTheme.For(look));
                var theme = QuickTheme.For(style.Theme);
                var tile = (ActionsTile)folder.Tiles[0].Island;
                var laid = tile.Lay(PopupPaint.Rect(0, 0, width, height), style);
                Bitmap(width, height,
                    dc => tile.PaintOn(dc, PopupPaint.Rect(0, 0, width, height), style, WidgetSurface.List),
                    bitmap =>
                    {
                        var left = Average(bitmap, (uint)MathF.Ceiling(width), (uint)MathF.Ceiling(height), laid.Left);
                        var right = Average(bitmap, (uint)MathF.Ceiling(width), (uint)MathF.Ceiling(height), laid.Right);
                        foreach (var (name, ink) in new[] { ("left", left), ("right", right) })
                        {
                            Assert.True(Distance(ink, theme.Text) < 24, $"{(dark ? "dark" : "light")}: the {name} name is {ink}, not the text colour");
                            Assert.True(Distance(ink, theme.Link) > 40, $"{(dark ? "dark" : "light")}: the {name} name is the link's colour");
                        }
                    });
                folder.Closed();
                folder.Dispose();
            }
        });

    /// <summary>
    /// The line is Windows' footer band (2026-10-16): its hairline across the top, a whole px or two,
    /// and the band's shade under it to the line's foot, each laid on the body once, side by side.
    /// </summary>
    [UiFact]
    public void TheLineIsAFooterBand() =>
        OnStaThread(gfx =>
        {
            foreach (var scale in new[] { 1f, 1.5f })
            {
                foreach (var dark in new[] { true, false })
                {
                    var look = LookFor(dark);
                    var style = FolderFlyout.StyleFor(scale, look, FlyoutTheme.Taskbar(look));
                    var theme = QuickTheme.For(style.Theme);
                    var folder = Folder(gfx, Actions("dateTime", "notifications"));
                    var tile = (ActionsTile)folder.Tiles[0].Island;
                    var (width, height) = (360 * scale, tile.ListHeight(0, style));
                    var hair = MathF.Max(1, PopupPaint.Round(scale));
                    Bitmap(width, height,
                        dc => tile.PaintOn(dc, PopupPaint.Rect(0, 0, width, height), style, WidgetSurface.List),
                        bitmap =>
                        {
                            var at = $"{(dark ? "dark" : "light")} at {scale * 100:0} %";
                            var row = (uint)MathF.Ceiling(width);
                            // Beside the buttons, in the gutter: nothing but the band.
                            Near(Alpha(theme.FooterLine), Pixel(bitmap, row, (uint)height, 2, 0), at);
                            Near(Alpha(theme.FooterLine), Pixel(bitmap, row, (uint)height, 2, (int)hair - 1), at);
                            Near(Alpha(theme.Footer), Pixel(bitmap, row, (uint)height, 2, (int)hair), at);
                            Near(Alpha(theme.Footer), Pixel(bitmap, row, (uint)height, 2, (int)height - 1), at);
                            Assert.True(theme.Footer.a > 0 && theme.FooterLine.a > theme.Footer.a, at);
                        });
                    folder.Closed();
                    folder.Dispose();
                }
            }
        });

    /// <summary>
    /// A name shorter than two letters' room ("Edit") is no reason for glyphs alone: quick settings'
    /// "Edit | All settings" keeps both names in a list of any width it has room in; only a line
    /// too narrow for any name has the glyphs alone, each name in its tooltip.
    /// </summary>
    [UiFact]
    public void OnlyANarrowLineHasTheGlyphsAlone() =>
        OnStaThread(gfx =>
        {
            var look = LookFor(dark: true);
            var style = FolderFlyout.StyleFor(1.5f, look, FlyoutTheme.Taskbar(look));
            var folder = Folder(gfx, Actions(Karpo.Core.SettingsActions.EditId, Karpo.Core.SettingsActions.AllSettingsId));
            var tile = (ActionsTile)folder.Tiles[0].Island;
            var height = tile.ListHeight(0, style);
            var wide = tile.Lay(PopupPaint.Rect(0, 0, 360 * 1.5f, height), style);
            Assert.False(wide.Bare);
            Assert.False(wide.LeftCut || wide.RightCut);
            var narrow = tile.Lay(PopupPaint.Rect(0, 0, 110 * 1.5f, height), style);
            Assert.True(narrow.Bare && narrow.LeftCut && narrow.RightCut);
            Assert.Equal(tile.Chosen.Left!.Name, tile.CardTip(ListFormCases.Centre(narrow.Left), PopupPaint.Rect(0, 0, 110 * 1.5f, height), style, WidgetSurface.List)?.Text);
            folder.Closed();
            folder.Dispose();
        });

    private static int Alpha(DXGI_RGBA colour) => (int)MathF.Round(colour.a * 255);

    /// <summary>A pixel's alpha, which is the shade laid over the body, within a level of <paramref name="alpha"/>.</summary>
    private static void Near(int alpha, uint pixel, string at) => Assert.InRange((int)(pixel >> 24), alpha - 1, alpha + 1);

    /// <summary>The premultiplied BGRA pixel at (<paramref name="x"/>, <paramref name="y"/>).</summary>
    private static uint Pixel(IWICBitmap* bitmap, uint width, uint height, int x, int y)
    {
        var pixels = new uint[width * height];
        fixed (uint* into = pixels)
        {
            ComUtil.Check(bitmap->CopyPixels(null, width * 4, width * height * 4, (byte*)into));
        }
        return pixels[(y * width) + x];
    }

    /// <summary>
    /// Two long names in a narrow line share it: neither runs into the other, both are cut, and each
    /// says itself whole in its tooltip.
    /// </summary>
    [UiFact]
    public void CutNamesSayThemselvesInTheirTooltips() =>
        OnStaThread(gfx =>
        {
            var look = LookFor(dark: true);
            var folder = Folder(gfx, Actions("notifications", "dateTime"));
            var style = FolderFlyout.StyleFor(1, look, FlyoutTheme.For(look));
            var tile = (ActionsTile)folder.Tiles[0].Island;
            var box = PopupPaint.Rect(0, 0, 150, 40);
            var laid = tile.Lay(box, style);
            Assert.True(laid.LeftCut && laid.RightCut);
            Assert.True(laid.Left.right <= laid.Right.left);
            Assert.Equal(laid.LeftRoom, laid.RightRoom, 0.5f);
            var (left, right) = tile.Chosen;
            Assert.Equal(left!.Name, tile.CardTip(ListFormCases.Centre(laid.Left), box, style, WidgetSurface.List)?.Text);
            Assert.Equal(right!.Name, tile.CardTip(ListFormCases.Centre(laid.Right), box, style, WidgetSurface.List)?.Text);
            folder.Closed();
            folder.Dispose();
        });

    /// <summary>The colour of the strongly inked pixels inside <paramref name="part"/>, unpremultiplied, 0 to 1.</summary>
    private static (float R, float G, float B) Average(IWICBitmap* bitmap, uint width, uint height, D2D_RECT_F part)
    {
        var pixels = new uint[width * height];
        fixed (uint* into = pixels)
        {
            ComUtil.Check(bitmap->CopyPixels(null, width * 4, width * height * 4, (byte*)into));
        }
        var (r, g, b, n) = (0f, 0f, 0f, 0);
        for (var y = (int)MathF.Max(0, part.top); y < (int)MathF.Min(height, part.bottom); y++)
        {
            for (var x = (int)MathF.Max(0, part.left); x < (int)MathF.Min(width, part.right); x++)
            {
                var pixel = pixels[(y * width) + x];
                var a = (int)(pixel >> 24);
                if (a < 160)
                {
                    continue;
                }
                r += ((pixel >> 16) & 0xFF) / (float)a;
                g += ((pixel >> 8) & 0xFF) / (float)a;
                b += (pixel & 0xFF) / (float)a;
                n++;
            }
        }
        Assert.True(n > 0, "nothing was drawn in the button");
        return (r / n, g / n, b / n);
    }

    /// <summary>How far apart two colours are, the largest of the three channels, 0 to 255.</summary>
    private static float Distance((float R, float G, float B) ink, DXGI_RGBA want) =>
        255 * MathF.Max(MathF.Abs(ink.R - want.r), MathF.Max(MathF.Abs(ink.G - want.g), MathF.Abs(ink.B - want.b)));
}
