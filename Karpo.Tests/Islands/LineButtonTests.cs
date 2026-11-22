// The lines' one button (Karpo.Dock\Islands\LineButton.cs, docs\widgets.md §353): the person, of a
// notifications line and a focus line, "i would like all of them to have the size of the clear
// button. in general." The standard is "Clear all"'s own sizes, and every line's buttons measure it:
// the focus band's steppers and Focus, a timer's and a stopwatch's buttons, a month's arrows.
//
// Everything here measures text, so it runs with KARPO_UI_TESTS=1.

using Karpo.Core;
using Karpo.Dock;
using Karpo.Dock.Panels;
using TerraFX.Interop.DirectX;
using Color = Windows.UI.Color;

namespace Karpo.Tests.Islands;

public sealed unsafe class LineButtonTests
{
    private static readonly float[] Scales = [1f, 1.25f, 1.5f, 2f];

    private static readonly float[] TextScales = [1f, 1.5f, 2.25f];

    private static TileStyle StyleFor(float scale, float textScale)
    {
        var look = new Look(
            Glass: false,
            Dark: true,
            Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4),
            AccentLight2: Color.FromArgb(255, 0x99, 0xEB, 0xFF),
            AccentDark1: Color.FromArgb(255, 0x00, 0x5F, 0xB8),
            TextScale: textScale);
        return FolderFlyout.StyleFor(scale, look, FlyoutTheme.For(look));
    }

    private static float Tall(D2D_RECT_F box) => box.bottom - box.top;

    private static float Wide(D2D_RECT_F box) => box.right - box.left;

    /// <summary>
    /// The standard is the notifications' "Clear all" (PlatedButtonStyle): 24 DIP tall, or the small
    /// text's line and 3 DIP over and under it where that is taller; 8 DIP either side of its words; a
    /// glyph alone a square of its height; the corners 4; 8 between two buttons.
    /// </summary>
    [UiFact]
    public void TheStandardIsClearAllsButton() =>
        Offscreen.OnStaThread(gfx =>
        {
            foreach (var scale in Scales)
            {
                foreach (var textScale in TextScales)
                {
                    var style = StyleFor(scale, textScale);
                    var at = $"{scale * 100:0} %, {textScale * 100:0} % text";
                    var buttons = LineButton.Of(gfx, style);
                    var small = TilePaint.LineHeight(gfx, style.Text(12));
                    Assert.True(Math.Abs(MathF.Max(style.Px(24), PopupPaint.Round(small + style.Px(6))) - buttons.Height) < 0.01f, at);
                    Assert.Equal((style.Px(8), style.Px(4), style.Px(8), style.Text(12)), (buttons.Pad, buttons.Radius, buttons.Gap, buttons.TextPx));
                    Assert.Equal(buttons.Height, buttons.Width(gfx, ""));
                    var words = PopupPaint.Measure(gfx, "Clear all", buttons.TextPx, Weight.Regular).X;
                    Assert.Equal(MathF.Ceiling(words + (2 * buttons.Pad)), buttons.Width(gfx, "Clear all"), 0.5f);
                    Assert.True(buttons.Width(gfx, "Focus", glyph: true) > buttons.Width(gfx, "Focus"), at);
                }
            }
        });

    /// <summary>
    /// A button under WinUI's 32 DIP keeps a hit area of 32 round it, evenly; two glyph buttons a
    /// button's gap apart have hit areas that meet and do not overlap, and a line's edges hold them.
    /// </summary>
    [UiFact]
    public void ItsHitAreaIsWinUIsAndNeighboursDoNotOverlap() =>
        Offscreen.OnStaThread(gfx =>
        {
            foreach (var scale in Scales)
            {
                var buttons = LineButton.Of(gfx, StyleFor(scale, 1));
                var one = buttons.Square(100, 50);
                var two = buttons.Square(one.right + buttons.Gap, 50);
                var hitOne = buttons.Hit(one);
                var hitTwo = buttons.Hit(two);
                Assert.Equal(buttons.Reach, Wide(hitOne), 0.5f);
                Assert.Equal(buttons.Reach, Tall(hitOne), 0.5f);
                Assert.True(hitOne.right <= hitTwo.left + 0.5f, $"{scale * 100:0} %: the hit areas overlap");
                var held = buttons.Hit(one, one.top, one.bottom);
                Assert.Equal((one.top, one.bottom), (held.top, held.bottom));
                var wide = buttons.At(0, 50, buttons.Reach * 3);
                Assert.Equal(Wide(wide), Wide(buttons.Hit(wide)), 0.5f);
            }
        });

    /// <summary>Every button of the clock's lines and a month's arrows is the standard's height, a glyph alone its square.</summary>
    [UiFact]
    public void EveryLinesButtonsAreTheStandardSize() =>
        Offscreen.OnStaThread(gfx =>
        {
            foreach (var scale in Scales)
            {
                foreach (var textScale in TextScales)
                {
                    var style = StyleFor(scale, textScale);
                    var at = $"{scale * 100:0} %, {textScale * 100:0} % text";
                    var buttons = LineButton.Of(gfx, style);
                    var form = ListForm.Of(gfx, style);
                    var width = FolderFlyout.ListWidth(scale);
                    void Square(string what, D2D_RECT_F box)
                    {
                        Assert.True(Math.Abs(Tall(box) - buttons.Height) < 0.5f, $"{at}: {what} is {Tall(box):0} px tall, not {buttons.Height:0}");
                        Assert.True(Math.Abs(Wide(box) - buttons.Height) < 0.5f, $"{at}: {what} is {Wide(box):0} px wide, not square");
                    }
                    void Standard(string what, D2D_RECT_F box) =>
                        Assert.True(Math.Abs(Tall(box) - buttons.Height) < 0.5f, $"{at}: {what} is {Tall(box):0} px tall, not {buttons.Height:0}");

                    var line = PopupPaint.Rect(0, 0, width, CountdownLine.Height(style));
                    foreach (var kind in new[] { CountdownKind.Focus, CountdownKind.Timer })
                    {
                        var at25 = new DateTime(2026, 9, 22, 14, 0, 0);
                        var idle = CountdownLine.Draw(new LineSketch(null, gfx), line, style, new CountdownInputs(kind, Countdown.None, TimeSpan.FromMinutes(25), at25));
                        if (idle.Less.right > idle.Less.left)
                        {
                            Square($"the {kind}'s minus", idle.Less);
                            Square($"the {kind}'s plus", idle.More);
                        }
                        Square($"the {kind}'s start", idle.Action);
                        var running = CountdownLine.Draw(new LineSketch(null, gfx), line, style,
                            new CountdownInputs(kind, Countdown.Start(kind, TimeSpan.FromMinutes(25), at25.AddMinutes(-3)), TimeSpan.FromMinutes(25), at25));
                        if (running.Clear.right > running.Clear.left)
                        {
                            Square($"the {kind}'s stop", running.Clear);
                        }
                        Square($"the {kind}'s pause", running.Action);
                    }

                    var stopwatch = StopwatchCard.Draw(new LineSketch(null, gfx), line, style, new StopwatchInputs(StopwatchState.None, TimeSpan.Zero)).Targets;
                    if (stopwatch.Count == 3)
                    {
                        Square("the stopwatch's lap", stopwatch[0].Box);
                        Square("the stopwatch's reset", stopwatch[1].Box);
                        Assert.Equal(buttons.Gap, stopwatch[1].Box.left - stopwatch[0].Box.right, 0.5f);
                    }
                    Square("the stopwatch's start", stopwatch[^1].Box);

                    var month = MonthTile.Lay(gfx, width, style);
                    if (Wide(month.Next) >= buttons.Height - 0.5f)
                    {
                        Square("the month's previous", month.Previous);
                        Square("the month's next", month.Next);
                    }
                }
            }
        });

    /// <summary>
    /// The clock beside the calendar (the timer, the stopwatch and the focus session over the month),
    /// as it opens at 150 %, dark and light: buttons-clock-&lt;dark|light&gt;-150.png in KARPO_PNG_DIR.
    /// </summary>
    [UiFact]
    public void ClockButtonsPictures()
    {
        foreach (var dark in new[] { true, false })
        {
            Offscreen.OnStaThread(gfx =>
            {
                Clock.Now = () => Folders.ListFormCases.Frozen;
                var panels = new List<(string, Look, FolderIsland, FolderPlan)>();
                try
                {
                    foreach (var text in new[] { 1f, 1.5f })
                    {
                        var look = Folders.ListFormPictures.LookFor(dark, text);
                        var holder = Presets.Folder(Presets.Find(Presets.ClockCalendarId)!, new PresetMachine(HasBattery: false, FixedDrives: []));
                        var folder = new FolderIsland($"{Folders.ListFormPictures.Owner}/{holder.Id}", holder, gfx, new Config());
                        folder.Opened(gfx);
                        var (_, plan) = Folders.ListFormPictures.Plan(folder, look, 1.5f);
                        panels.Add(($"{text * 100:0} % text", look, folder, plan));
                    }
                    Folders.ListFormPictures.Sheet(gfx, $"buttons-clock-{(dark ? "dark" : "light")}-150.png", 1.5f, panels);
                }
                finally
                {
                    foreach (var (_, _, folder, _) in panels)
                    {
                        folder.Closed();
                        folder.Dispose();
                    }
                }
            });
        }
    }
}
