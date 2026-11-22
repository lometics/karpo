// The power mode's line at every height (docs\widgets.md §248; Karpo.Dock\QuickSettings\Pages\PowerModePage.cs):
// from one row up it fits itself to the height it is given — one row, the strip, the modes listed,
// the whole page — with everything between the list's gutters and inside the line, nothing on
// anything else, all three modes a click away, and never a scroll. At one row its segments set the
// mode by the pointer and by the keys, and name themselves.
//
// Everything here draws or measures text, so it runs with KARPO_UI_TESTS=1; the sheets go to
// KARPO_PNG_DIR. Made-up batteries throughout (SampleBattery): the PC's own power mode is never touched.

using System.Numerics;
using Karpo.Dock;
using Karpo.Dock.QuickSettings;
using Karpo.Dock.QuickSettings.Controls;
using Karpo.Dock.QuickSettings.Services;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using static Karpo.Tests.Folders.ListFormPictures;

namespace Karpo.Tests.Folders;

public sealed unsafe class PowerModeFormsTests
{
    /// <summary>What the line can be saying: a mode in force, a mode Windows refused, a plan with none.</summary>
    private static readonly (string What, Func<SampleBattery> Battery, bool Refuse)[] States =
    [
        ("balanced", static () => new SampleBattery(), false),
        ("refused", static () => new SampleBattery { Refuses = true }, true),
        ("no modes", static () => new SampleBattery { Present = false, Offers = null }, false),
    ];

    /// <summary>A power mode line reading <paramref name="battery"/>, Efficiency asked for and refused where <paramref name="refuse"/>.</summary>
    private static QuickPageTile Tile(Gfx gfx, SampleBattery battery, bool refuse = false)
    {
        QuickPageTile.Sources = new QuickSources
        {
            Wifi = static () => new SampleWifi(),
            Bluetooth = static () => new SampleBluetooth(),
            Battery = () => battery,
            Desktops = static () => new SampleDesktopNames(),
        };
        var tile = new QuickPageTile(Config.IslandKey(null, "probe"), ListKind.PowerMode.New(), gfx, new Config());
        if (refuse)
        {
            // Asked through the page as a click would, so Windows' refusal comes back as news.
            var style = FolderFlyout.StyleFor(1, LookFor(true), FlyoutTheme.For(LookFor(true)));
            var width = FolderFlyout.ListWidth(1);
            tile.LayOut(PopupPaint.Rect(0, 0, width, tile.ListHeight(width, style)), style);
            tile.Placed.OfType<ListRow>().First(row => row.Title == S.DockQuickPowerEfficiency).Clicked!();
            tile.Pump();
        }
        return tile;
    }

    private static void Done(QuickPageTile tile)
    {
        tile.Dispose();
        QuickPageTile.Sources = null;
    }

    private static PowerModeForm FormOf(QuickPageTile tile) => ((PowerModePage)tile.Current).Form;

    /// <summary>
    /// At one row to eight and its own height, at 100, 150 and 200 %, every text size and every list
    /// width, in every state: everything it places is between the gutters and inside the line, off
    /// everything else (the segments stand on their track), with every mode a control; a line of
    /// one row is the one-row form, a taller line never a smaller form, and none of it scrolls. At
    /// the list's text on the usual width, one row says the mode whole.
    /// </summary>
    [UiFact]
    public void TheLineIsWorkedAtEveryHeight() => Offscreen.OnStaThread(gfx =>
    {
        var failures = new List<string>();
        foreach (var scale in new[] { 1f, 1.5f, 2f })
        {
            foreach (var textScale in new[] { 1f, 1.5f, 2.25f })
            {
                var look = LookFor(true, textScale);
                var style = FolderFlyout.StyleFor(scale, look, FlyoutTheme.For(look));
                var form = ListForm.Of(gfx, style);
                foreach (var listWidth in FolderListWidths.All)
                {
                    var width = FolderFlyout.ListWidth(scale, listWidth);
                    foreach (var (what, battery, refuse) in States)
                    {
                        var tile = Tile(gfx, battery(), refuse);
                        try
                        {
                            var last = PowerModeForm.One;
                            for (var rows = 1; rows <= 9; rows++)
                            {
                                var height = rows == 9 ? tile.ListHeight(width, style) : Rows(rows, scale, textScale);
                                var where = $"{scale:0.##}x, text {textScale:0.##}, {listWidth} ({width} px), {(rows == 9 ? "its own height" : $"{rows} rows")}, {what}";
                                void Fail(string why) => failures.Add($"{where}: {why}");

                                var box = PopupPaint.Rect(0, 0, width, height);
                                if (tile.ScrollHeight(width, height, style) > height + 0.5f)
                                {
                                    Fail("it scrolls");
                                }
                                tile.LayOut(box, style);
                                var shape = FormOf(tile);
                                if (rows == 1 && shape != PowerModeForm.One)
                                {
                                    Fail($"a line of one row is {shape}");
                                }
                                if (shape > last)
                                {
                                    Fail($"it is {shape}, smaller than the {last} of a shorter line");
                                }
                                last = shape;
                                if (rows == 9 && shape != PowerModeForm.Whole)
                                {
                                    Fail($"at its own height it is {shape}");
                                }

                                var inside = new D2D_RECT_F(form.Inset - 0.5f, -0.5f, width - form.Inset + 0.5f, height + 0.5f);
                                var placed = tile.Placed.Where(widget => widget.Visible && widget.Width > 0).ToList();
                                foreach (var widget in placed)
                                {
                                    // A list's row is an item, its band the line's whole width (§428).
                                    var pad = widget is Karpo.Dock.QuickSettings.Controls.ListRow ? form.Inset : 0;
                                    if (!Inside(widget.Bounds, new D2D_RECT_F(inside.left - pad, inside.top, inside.right + pad, inside.bottom)))
                                    {
                                        Fail($"its {Named(widget)} {Place(widget.Bounds)} is outside the line {Place(inside)}");
                                    }
                                    if (widget is Label { Lines: 1 } label && label.Text.Length > 0
                                        && widget.Height + 0.5f < Label.Measure(LineContext(gfx, style), label.Text, label.Style, 1e6f))
                                    {
                                        Fail($"its {Named(widget)} is cut through its words ({widget.Height} px)");
                                    }
                                }
                                for (var one = 0; one < placed.Count; one++)
                                {
                                    for (var other = one + 1; other < placed.Count; other++)
                                    {
                                        if ((placed[one] is SegmentedTrack && placed[other] is SegmentedItem) || (placed[one] is SegmentedItem && placed[other] is SegmentedTrack))
                                        {
                                            continue;
                                        }
                                        if (Overlap(placed[one].Bounds, placed[other].Bounds))
                                        {
                                            Fail($"its {Named(placed[one])} and its {Named(placed[other])} overlap");
                                        }
                                    }
                                }
                                var modes = placed.Where(widget => widget is ListRow or SegmentedItem && widget.Clicked is not null).Select(widget => widget.Name).ToList();
                                var offered = what != "no modes";
                                if (offered && !modes.ToHashSet().SetEquals(PowerModePage.Modes.Select(mode => mode.Name)))
                                {
                                    Fail($"its modes are [{string.Join(", ", modes)}]");
                                }
                                if (!offered && modes.Count > 0)
                                {
                                    Fail("a plan with no modes offers some");
                                }
                                if (rows == 1 && textScale == 1 && listWidth == FolderListWidth.Medium && what == "balanced")
                                {
                                    var said = placed.OfType<Label>().FirstOrDefault(label => label.Text.Length > 0);
                                    var whole = said is null ? 0 : PopupPaint.Measure(gfx, said.Text, form.TextPx, Weight.Regular).X;
                                    if (said is null || said.Width + 0.5f < whole)
                                    {
                                        Fail($"one row does not say {(said is null ? "anything" : $"\"{said.Text}\" whole")}");
                                    }
                                }
                            }
                        }
                        finally
                        {
                            Done(tile);
                        }
                    }
                }
            }
        }
        Assert.True(failures.Count == 0, $"{failures.Count} failures, the first of them:\n{string.Join("\n", failures.Take(40))}");
    });

    /// <summary>
    /// One row: a click on a segment asks for its mode, marked at once and said after the gauge; the
    /// one in force asks nothing; a segment of its gauge alone names itself; Down reaches the segments
    /// and Enter sets the one ringed.
    /// </summary>
    [UiFact]
    public void OneRowSetsTheMode() => Offscreen.OnStaThread(gfx =>
    {
        var battery = new SampleBattery();
        var tile = Tile(gfx, battery);
        try
        {
            var style = FolderFlyout.StyleFor(1.5f, LookFor(true), FlyoutTheme.For(LookFor(true)));
            var width = FolderFlyout.ListWidth(1.5f);
            var box = PopupPaint.Rect(0, 0, width, Rows(1, 1.5f, 1));
            tile.LayOut(box, style);
            Assert.Equal(PowerModeForm.One, FormOf(tile));
            SegmentedItem Segment(string name) => tile.Placed.OfType<SegmentedItem>().Single(each => each.Name == name);
            Vector2 Middle(Widget widget) => new((widget.Bounds.left + widget.Bounds.right) / 2, (widget.Bounds.top + widget.Bounds.bottom) / 2);
            void Click(string name)
            {
                if (tile.CardPress(Middle(Segment(name)), box, style, WidgetSurface.List))
                {
                    tile.CardRelease();
                }
                tile.Pump();
                tile.LayOut(box, style);
            }

            Assert.True(Segment(S.DockQuickPowerBalanced).Selected);
            Assert.Equal(S.DockQuickPowerBalanced, tile.CardTip(Middle(Segment(S.DockQuickPowerBalanced)), box, style, WidgetSurface.List)?.Text);

            Click(S.DockQuickPowerPerformance);
            Assert.Equal([PowerMode.Performance], battery.Asked);
            Assert.True(Segment(S.DockQuickPowerPerformance).Selected);
            Assert.Single(tile.Placed.OfType<SegmentedItem>(), each => each.Selected);
            Assert.Contains(tile.Placed.OfType<Label>(), label => label.Text == S.DockQuickPowerPerformance);
            Click(S.DockQuickPowerPerformance);
            Assert.Equal([PowerMode.Performance], battery.Asked);

            // The keys: the click left them on Performance; Up rings Balanced, Enter sets it.
            Assert.True(tile.CardKey(VK.VK_UP, WidgetSurface.List));
            Assert.True(tile.CardKey(VK.VK_RETURN, WidgetSurface.List));
            tile.Pump();
            Assert.Equal([PowerMode.Performance, PowerMode.Balanced], battery.Asked);
            tile.LayOut(box, style);
            Assert.True(Segment(S.DockQuickPowerBalanced).Selected);
            Assert.Equal(PowerModeForm.One, FormOf(tile));
        }
        finally
        {
            Done(tile);
        }
    });

    // --- Sheets --------------------------------------------------------------------------------

    /// <summary>The line at its own height and at one row to six, in each state and on a narrow list, dark and light at 150 %, and at 225 % text.</summary>
    [UiFact]
    public void PowerModeSheets() => Offscreen.OnStaThread(gfx =>
    {
        foreach (var dark in new[] { true, false })
        {
            Sheet(gfx, Path.Combine(Offscreen.Pictures, $"power-mode-line-150-{(dark ? "dark" : "light")}.png"), LookFor(dark), 1.5f);
        }
        Sheet(gfx, Path.Combine(Offscreen.Pictures, "power-mode-line-150-text225.png"), LookFor(true, 2.25f), 1.5f);
    });

    private static void Sheet(Gfx gfx, string path, Look look, float scale)
    {
        var theme = FlyoutTheme.For(look);
        var style = FolderFlyout.StyleFor(scale, look, theme);
        var pad = PopupPaint.Round(12 * scale);
        var label = PopupPaint.Round(11 * scale);
        var labelLine = TilePaint.LineHeight(gfx, label);
        (string What, Func<SampleBattery> Battery, bool Refuse, float Width)[] columns =
        [
            .. States.Select(state => (state.What, state.Battery, state.Refuse, FolderFlyout.ListWidth(scale))),
            ("narrow", static () => new SampleBattery(), false, FolderFlyout.ListWidth(scale, FolderListWidth.Narrow)),
        ];
        float Own()
        {
            var tile = Tile(gfx, new SampleBattery());
            try
            {
                return tile.ListHeight(FolderFlyout.ListWidth(scale), style);
            }
            finally
            {
                Done(tile);
            }
        }
        var heights = new List<(string Label, float Height)> { ("its own height", Own()) };
        heights.AddRange(Enumerable.Range(1, 6).Select(rows => ($"{rows} row{(rows == 1 ? "" : "s")}", Rows(rows, scale, style.TextScale))));
        var width = pad + columns.Sum(column => column.Width + pad);
        var height = pad + heights.Sum(each => labelLine + each.Height + pad);
        Offscreen.Bitmap(width, height, dc =>
        {
            PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, width, height), 0, PopupPaint.Ink(theme.Base));
            var y = pad;
            foreach (var (caption, lineHeight) in heights)
            {
                var x = pad;
                foreach (var (what, battery, refuse, lineWidth) in columns)
                {
                    var tile = Tile(gfx, battery(), refuse);
                    try
                    {
                        PopupPaint.Text(dc, gfx, x == pad ? $"{caption} · {what}" : what, label, Weight.Regular, theme.TextSecondary, x, y);
                        var at = PopupPaint.Rect(x, y + labelLine, lineWidth, lineHeight);
                        PopupPaint.Stroke(dc, at, PopupPaint.Round(8 * scale), MathF.Max(1, MathF.Round(scale)), PopupPaint.Ink(theme.Stroke));
                        var context = dc;
                        Offscreen.Shifted(context, at.left, at.top, () => tile.PaintCard(context, PopupPaint.Rect(0, 0, lineWidth, lineHeight), style, WidgetSurface.List));
                    }
                    finally
                    {
                        Done(tile);
                    }
                    x += lineWidth + pad;
                }
                y += labelLine + lineHeight + pad;
            }
        }, bitmap => Offscreen.Save(bitmap, path));
    }

    /// <summary>The context the line's controls measure their words with at <paramref name="style"/>.</summary>
    private static QuickContext LineContext(Gfx gfx, in TileStyle style) =>
        new() { Gfx = gfx, Theme = QuickTheme.For(style.Theme), Scale = style.Scale, TextScale = style.TextScale };

    private static string Named(Widget widget) => widget.Name.Length > 0 ? $"{widget.GetType().Name} \"{widget.Name}\"" : widget is Label label ? $"label \"{label.Text}\"" : widget.GetType().Name;

    private static bool Inside(D2D_RECT_F rect, D2D_RECT_F room) =>
        rect.left >= room.left && rect.top >= room.top && rect.right <= room.right && rect.bottom <= room.bottom;

    private static bool Overlap(D2D_RECT_F a, D2D_RECT_F b) =>
        a.right > a.left && b.right > b.left && a.left < b.right - 0.5f && b.left < a.right - 0.5f && a.top < b.bottom - 0.5f && b.top < a.bottom - 0.5f;

    private static string Place(D2D_RECT_F rect) => $"{rect.left},{rect.top},{rect.right},{rect.bottom}";
}
