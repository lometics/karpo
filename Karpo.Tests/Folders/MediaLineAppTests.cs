// The app playing on the now-playing line's left (the person, 2026-10-30: "now playing widget should
// show the app that is playing on the left side"): where the track plays (NowPlayingSource) — the
// session's app's icon, or the favicon of the site a browser plays it on — in the list's leading
// column at the kit's Row glyph size (§401), where other lines stand their glyph, at every height
// the line is given; the note where no icon was found, and with nothing playing. The icon names
// where it plays under the pointer. It was a badge in the cover's corner before; the cover keeps
// the trailing gutter and wears no badge now.
//
// Every track here is a stand-in (MediaIsland.Stand) with made-up icons of one flat colour each, so
// the drawing tells where an icon went from its pixels. Pictures: media-line-app-<dark|light>.png in
// KARPO_PNG_DIR. Needs Direct2D, so it runs only with KARPO_UI_TESTS=1.

using System.Numerics;
using Karpo.Dock;
using Karpo.Dock.Native;
using Karpo.Tests.Islands;
using Karpo.Tests.QuickSettings;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using static Karpo.Tests.Folders.ListFormPictures;

namespace Karpo.Tests.Folders;

public sealed unsafe class MediaLineAppTests
{
    /// <summary>An app's made-up icon: a square of one opaque colour (blue, green, red).</summary>
    private static Pixels Flat(byte b, byte g, byte r)
    {
        const int side = NowPlayingSource.IconPx;
        var data = new byte[side * side * 4];
        for (var at = 0; at < data.Length; at += 4)
        {
            (data[at], data[at + 1], data[at + 2], data[at + 3]) = (b, g, r, 255);
        }
        return new Pixels { Data = data, Width = side, Height = side };
    }

    private static readonly (byte B, byte G, byte R) SpotifyInk = (84, 215, 30);
    private static readonly (byte B, byte G, byte R) PlayerInk = (0, 120, 255);

    /// <summary>Two apps, each with its own icon: what the line shows on its left.</summary>
    private static readonly (string App, MediaStand Stand, (byte B, byte G, byte R) Ink)[] Apps =
    [
        ("Spotify", new MediaStand(Title: "Hey", Artist: "Pixies", AppName: "Spotify", Art: QuickFlyoutOffscreenTests.Cover(),
            SourceIcon: Flat(SpotifyInk.B, SpotifyInk.G, SpotifyInk.R), Position: TimeSpan.FromSeconds(62), Duration: TimeSpan.FromSeconds(214)), SpotifyInk),
        ("Media Player", new MediaStand(AppName: "Media Player", SourceIcon: Flat(PlayerInk.B, PlayerInk.G, PlayerInk.R),
            Position: TimeSpan.FromSeconds(30), Duration: TimeSpan.FromSeconds(300)), PlayerInk),
    ];

    /// <summary>A now-playing line on its own over <paramref name="stand"/>.</summary>
    private static MediaIsland Line(Gfx gfx, MediaStand stand)
    {
        var item = ListKind.Media.New();
        item.Id = "media";
        MediaIsland.Stand = stand;
        try
        {
            return new MediaIsland(Karpo.Core.Config.IslandKey(null, item.Id), item, gfx, new Karpo.Core.Config());
        }
        finally
        {
            MediaIsland.Stand = null;
        }
    }

    private static TileStyle Style(bool dark, float text)
    {
        var look = LookFor(dark, text);
        return FolderFlyout.StyleFor(1.5f, look, FlyoutTheme.For(look));
    }

    private static D2D_RECT_F LineBox(int rows, in TileStyle style) => PopupPaint.Rect(0, 0, FolderFlyout.ListWidth(1.5f), LineRows.Px(rows, style));

    /// <summary>
    /// <paramref name="line"/> drawn in <paramref name="box"/>: the glyphs it drew, and the box of the
    /// pixels in <paramref name="ink"/> (all zero where none are).
    /// </summary>
    private static (List<PaintedStyle> Glyphs, D2D_RECT_F Ink) Drawn(MediaIsland line, D2D_RECT_F box, in TileStyle style, (byte B, byte G, byte R) ink)
    {
        var glyphs = new List<PaintedStyle>();
        var found = default(D2D_RECT_F);
        var drawnStyle = style;
        Offscreen.Bitmap(box.right, box.bottom, dc =>
        {
            PaintWatch.Styled = piece =>
            {
                if (piece.What == PaintedKind.Glyph)
                {
                    glyphs.Add(piece);
                }
            };
            try
            {
                line.PaintOn(dc, box, drawnStyle, WidgetSurface.List);
            }
            finally
            {
                PaintWatch.Styled = null;
            }
        }, bitmap => found = InkBox(bitmap, (uint)MathF.Ceiling(box.right), (uint)MathF.Ceiling(box.bottom), ink));
        return (glyphs, found);
    }

    /// <summary>The box of the opaque pixels of <paramref name="ink"/>, a little either way; all zero where there are none.</summary>
    private static D2D_RECT_F InkBox(IWICBitmap* bitmap, uint width, uint height, (byte B, byte G, byte R) ink)
    {
        IWICBitmapLock* locked = null;
        try
        {
            var all = new WICRect { X = 0, Y = 0, Width = (int)width, Height = (int)height };
            ComUtil.Check(bitmap->Lock(&all, (uint)WICBitmapLockFlags.WICBitmapLockRead, &locked));
            uint stride = 0;
            ComUtil.Check(locked->GetStride(&stride));
            uint size = 0;
            byte* data = null;
            ComUtil.Check(locked->GetDataPointer(&size, &data));
            float left = width, top = height, right = 0, bottom = 0;
            for (var y = 0u; y < height; y++)
            {
                for (var x = 0u; x < width; x++)
                {
                    var at = data + (y * stride) + (x * 4);
                    if (at[3] > 240 && Math.Abs(at[0] - ink.B) < 16 && Math.Abs(at[1] - ink.G) < 16 && Math.Abs(at[2] - ink.R) < 16)
                    {
                        (left, top, right, bottom) = (MathF.Min(left, x), MathF.Min(top, y), MathF.Max(right, x + 1), MathF.Max(bottom, y + 1));
                    }
                }
            }
            return right > left ? new D2D_RECT_F(left, top, right, bottom) : default;
        }
        finally
        {
            ComUtil.Release(ref locked);
        }
    }

    private static float Middle(float one, float other) => (one + other) / 2;

    /// <summary>
    /// The app playing stands in the leading column, two apps each with its own icon: at the kit's Row
    /// glyph size, centred in the column on the first row's middle (where play stands), at one row,
    /// at the row with its caption and on the whole widget, dark and light, at 100 and 150 % text —
    /// and nowhere else: not as a badge on the cover. No note beside it.
    /// </summary>
    [UiFact]
    public void TheAppPlayingStandsInTheLeadingColumn()
    {
        var failures = new List<string>();
        Offscreen.OnStaThread(gfx =>
        {
            foreach (var (app, stand, ink) in Apps)
            {
                using var line = Line(gfx, stand);
                foreach (var dark in new[] { true, false })
                {
                    foreach (var text in new[] { 1f, 1.5f })
                    {
                        var style = Style(dark, text);
                        var form = ListForm.Of(gfx, style);
                        var column = Middle(form.Inset, form.Inset + form.Icon);
                        foreach (var rows in new[] { 1, 2, 4 })
                        {
                            var box = LineBox(rows, style);
                            var where = $"{app}, {rows} rows, {(dark ? "dark" : "light")}, {text:0.##}x text";
                            var laid = line.LineOf(line.Scrolled(box, style, WidgetSurface.List).Box, style);
                            var (glyphs, icon) = Drawn(line, box, style, ink);
                            if (icon.right <= icon.left)
                            {
                                failures.Add($"{where}: its icon is drawn nowhere");
                                continue;
                            }
                            var row = Middle(laid.Play.top, laid.Play.bottom);
                            if (MathF.Abs(Middle(icon.left, icon.right) - column) > 1.5f || MathF.Abs(Middle(icon.top, icon.bottom) - row) > 1.5f)
                            {
                                failures.Add($"{where}: its icon at {icon.left:0},{icon.top:0}..{icon.right:0},{icon.bottom:0}, not on the leading column's middle {column:0} and the row's {row:0}");
                            }
                            if (MathF.Abs(icon.right - icon.left - form.Glyph) > 1.5f || MathF.Abs(icon.bottom - icon.top - form.Glyph) > 1.5f)
                            {
                                failures.Add($"{where}: its icon {icon.right - icon.left:0} by {icon.bottom - icon.top:0} px, not the Row glyph's {form.Glyph:0}");
                            }
                            if (glyphs.Any(glyph => glyph.Box.right > form.Inset && glyph.Box.left < form.TextLeft && glyph.Box.top < row && glyph.Box.bottom > row))
                            {
                                failures.Add($"{where}: a glyph beside its icon in the leading column");
                            }
                            if (TilePaint.Width(laid.App) <= 0 || MathF.Abs(Middle(laid.App.left, laid.App.right) - column) > 1.5f)
                            {
                                failures.Add($"{where}: the layout's app at {laid.App.left:0}..{laid.App.right:0}, not the leading column");
                            }
                        }
                    }
                }
            }
        });
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    /// <summary>
    /// Where no icon was found — and with nothing playing — the leading column wears the note, the
    /// kit's Row glyph on the row's middle.
    /// </summary>
    [UiFact]
    public void WithoutItsIconTheLineWearsTheNote()
    {
        var failures = new List<string>();
        Offscreen.OnStaThread(gfx =>
        {
            foreach (var (name, stand) in new[] { ("no icon", new MediaStand(AppName: "Sample Player")), ("nothing playing", new MediaStand(Nothing: true)) })
            {
                using var line = Line(gfx, stand);
                var style = Style(dark: true, text: 1f);
                var form = ListForm.Of(gfx, style);
                var column = Middle(form.Inset, form.Inset + form.Icon);
                foreach (var rows in new[] { 1, 2, 4 })
                {
                    var box = LineBox(rows, style);
                    var laid = line.LineOf(line.Scrolled(box, style, WidgetSurface.List).Box, style);
                    var row = Middle(laid.Play.top, laid.Play.bottom);
                    var (glyphs, _) = Drawn(line, box, style, (0, 0, 0));
                    var note = glyphs.Where(glyph => glyph.Box.right > form.Inset && glyph.Box.left < form.TextLeft).ToList();
                    if (note.Count != 1)
                    {
                        failures.Add($"{name}, {rows} rows: {note.Count} glyphs in the leading column, not the note");
                        continue;
                    }
                    if (MathF.Abs(Middle(note[0].Box.left, note[0].Box.right) - column) > 1.5f || MathF.Abs(Middle(note[0].Box.top, note[0].Box.bottom) - row) > 1.5f)
                    {
                        failures.Add($"{name}, {rows} rows: the note at {note[0].Box.left:0},{note[0].Box.top:0}, not on the column's middle {column:0} and the row's {row:0}");
                    }
                    if (MathF.Abs(note[0].Px - form.Glyph) > 0.5f)
                    {
                        failures.Add($"{name}, {rows} rows: the note at {note[0].Px:0.#} px, not the Row glyph's {form.Glyph:0.#}");
                    }
                }
            }
        });
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    /// <summary>The icon names where the track plays under the pointer: the app, or the site and the browser; the words have no tip.</summary>
    [UiFact]
    public void TheAppsIconNamesWhereItPlays()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var site = new MediaStand(Title: "lofi hip hop radio", Artist: "www.youtube.com", AppName: "Brave", SourceIcon: QuickFlyoutOffscreenTests.Favicon(), Site: "www.youtube.com");
            foreach (var (stand, said) in new[] { (Apps[0].Stand, "Spotify"), (Apps[1].Stand, "Media Player"), (site, "youtube.com · Brave") })
            {
                using var line = Line(gfx, stand);
                var style = Style(dark: true, text: 1f);
                var box = LineBox(2, style);
                var laid = line.LineOf(line.Scrolled(box, style, WidgetSurface.List).Box, style);
                var tip = line.TipOn(new Vector2(Middle(laid.App.left, laid.App.right), Middle(laid.App.top, laid.App.bottom)), box, style, WidgetSurface.List);
                Assert.Equal(said, tip?.Text);
                Assert.Equal(laid.App, tip?.Part);
                var words = laid.Words[0].Box;
                Assert.Null(line.TipOn(new Vector2(words.left + 4, Middle(words.top, words.bottom)), box, style, WidgetSurface.List));
            }
            using var idle = Line(gfx, new MediaStand(Nothing: true));
            var idleStyle = Style(dark: true, text: 1f);
            var idleBox = LineBox(2, idleStyle);
            var none = idle.LineOf(idleBox, idleStyle).App;
            Assert.Null(idle.TipOn(new Vector2(Middle(none.left, none.right), Middle(none.top, none.bottom)), idleBox, idleStyle, WidgetSurface.List));
        });
    }

    /// <summary>
    /// Regression (seen in media-line-app-*.png): one row with the room for "Hey · Pixies" drew
    /// "Hey · Pixi…" — the artist's box was what the two measured less the title, each rounded up, a
    /// pixel short of what the artist measures alone. The words the row has room for are drawn whole.
    /// </summary>
    [UiFact]
    public void OneRowSaysTheTitleAndTheArtistWholeWhereTheyFit()
    {
        Offscreen.OnStaThread(gfx =>
        {
            using var line = Line(gfx, Apps[0].Stand);
            foreach (var text in new[] { 1f, 1.5f })
            {
                var style = Style(dark: true, text);
                var box = LineBox(1, style);
                Assert.Equal(["Hey", " · Pixies"], line.LineOf(box, style).Words.Select(words => words.Text));
                PaintWatch.Trimmed = 0;
                Offscreen.Bitmap(box.right, box.bottom, dc =>
                {
                    PaintWatch.Watching = (_, _, color) => color;
                    try
                    {
                        line.PaintOn(dc, box, style, WidgetSurface.List);
                    }
                    finally
                    {
                        PaintWatch.Watching = null;
                    }
                }, _ => { });
                Assert.True(PaintWatch.Trimmed == 0, $"{text:0.##}x text: {PaintWatch.Trimmed} words cut with an ellipsis");
            }
        });
    }

    /// <summary>
    /// The pictures: the two apps, a site in a browser and one without an icon, at one row, at the row
    /// with its caption and as the whole widget, at 150 % — media-line-app-&lt;dark|light&gt;.png.
    /// </summary>
    [UiFact]
    public void MediaLineAppPictures()
    {
        foreach (var dark in new[] { true, false })
        {
            Offscreen.OnStaThread(gfx =>
            {
                var style = Style(dark, 1f);
                var theme = style.Theme;
                var stands = new List<(string Caption, MediaStand Stand)>
                {
                    ("Spotify", Apps[0].Stand with { SourceIcon = QuickFlyoutOffscreenTests.AppIcon() }),
                    ("Media Player", Apps[1].Stand),
                    ("youtube.com in Brave", new MediaStand(Title: "lofi hip hop radio - beats to relax/study to", Artist: "www.youtube.com", AppName: "Brave",
                        Art: QuickFlyoutOffscreenTests.Cover(), SourceIcon: QuickFlyoutOffscreenTests.Favicon(), Site: "www.youtube.com")),
                    ("No icon found", new MediaStand(AppName: "Sample Player")),
                    ("Nothing playing", new MediaStand(Nothing: true)),
                };
                var lines = stands.Select(stand => Line(gfx, stand.Stand)).ToList();
                try
                {
                    int[] heights = [1, 2, 4];
                    const float pad = 18, label = 20;
                    var width = FolderFlyout.ListWidth(1.5f);
                    var tallest = LineRows.Px(4, style);
                    var sheetWidth = pad + (heights.Length * (width + pad));
                    var sheetHeight = pad + (stands.Count * (label + tallest + pad));
                    Offscreen.Bitmap(sheetWidth, sheetHeight, dc =>
                    {
                        PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, sheetWidth, sheetHeight), 0, PopupPaint.Ink(theme.Base));
                        var y = pad;
                        for (var index = 0; index < stands.Count; index++)
                        {
                            var x = pad;
                            foreach (var rows in heights)
                            {
                                var box = LineBox(rows, style);
                                PopupPaint.Text(dc, gfx, $"{stands[index].Caption} · {rows} {(rows == 1 ? "row" : "rows")}", 13f, Weight.Regular, theme.TextSecondary, x, y, width);
                                PopupPaint.Fill(dc, PopupPaint.Rect(x, y + label, width, box.bottom), 8, PopupPaint.Ink(theme.CardBackground));
                                var line = lines[index];
                                Offscreen.Shifted(dc, x, y + label, () => line.PaintOn(dc, box, style, WidgetSurface.List));
                                x += width + pad;
                            }
                            y += label + tallest + pad;
                        }
                    }, bitmap => Offscreen.Save(bitmap, Path.Combine(Offscreen.Pictures, $"media-line-app-{(dark ? "dark" : "light")}.png")));
                }
                finally
                {
                    foreach (var line in lines)
                    {
                        line.Dispose();
                    }
                }
            });
        }
    }
}
