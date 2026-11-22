// The volume slider's list form: the sound panel's volume row as a line of a list folder
// (ListKind.SoundVolume, the volume's partner in a list). It is a slider at every height a line can be
// given — one row of the list to three, at every scale and at the largest text — dragged and turned
// with the wheel anywhere on it, and it keeps the list's one gutter; and the pictures of it at one to
// three rows, dark and light.
//
// A made-up machine stands behind every line (SoundParts.Made): nothing here reads or sets this PC's
// volume or its mute.

using System.Numerics;
using Karpo.Core;
using Karpo.Dock;
using Karpo.Dock.QuickSettings.Controls;
using Karpo.Dock.QuickSettings.Services;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;

namespace Karpo.Tests.Folders;

public sealed unsafe class VolumeLineTests
{
    /// <summary>The scales a person's display runs at.</summary>
    private static readonly float[] Scales = [1f, 1.5f, 2f];

    /// <summary>The text sizes at Windows' two ends.</summary>
    private static readonly float[] TextScales = [1f, 2.25f];

    /// <summary>
    /// How tall a line of <paramref name="rows"/> rows is, as a list folder gives it (FolderFlyout's list
    /// case): one row is a line of the list, which grows with the text, and each row past it adds a cell
    /// and the gap before it.
    /// </summary>
    private static float RowsPx(int rows, float scale, float textScale)
    {
        return rows * LineHeight.Row * FolderFlyout.CellPx(scale, textScale);
    }

    /// <summary>
    /// At every height from one row to three: the slider lies inside the line, a drag takes the level to
    /// either end, and the wheel steps it — over the slider, and over the mute and the level beside it,
    /// because the line is one control, as the volume slider is anywhere on the bar.
    /// </summary>
    [UiFact]
    public void ItIsASliderAtEveryHeightFromOneRow()
    {
        Offscreen.OnStaThread(gfx =>
        {
            foreach (var scale in Scales)
            {
                foreach (var textScale in TextScales)
                {
                    for (var rows = 1; rows <= 3; rows++)
                    {
                        var machine = new Machine { Level = 0.5f };
                        using var line = Line(gfx, machine);
                        var height = RowsPx(rows, scale, textScale);
                        var box = new D2D_RECT_F(0, 0, MathF.Round(360 * scale), height);
                        var style = Style(scale, textScale);
                        var at = $"{rows} row(s) at {scale:0.##}x, text {textScale:0.##}";
                        line.LayOut(box, style);
                        var slider = Assert.Single(line.Placed.OfType<Slider>());
                        Assert.True(slider.Bounds.top >= 0 && slider.Bounds.bottom <= height, $"{at}: the slider leaves the line");
                        var middle = (slider.Bounds.top + slider.Bounds.bottom) / 2;

                        var start = new Vector2(slider.Bounds.left + 2, middle);
                        Assert.True(line.CardPress(start, box, style, WidgetSurface.List), $"{at}: the slider did not take the press");
                        line.CardDrag(start with { X = slider.Bounds.right - 1 }, box, style, WidgetSurface.List);
                        Assert.True(machine.Level > 0.95f, $"{at}: dragged to its end, the level is {machine.Level}");
                        line.CardDrag(start with { X = slider.Bounds.left }, box, style, WidgetSurface.List);
                        Assert.True(machine.Level < 0.05f, $"{at}: dragged to its start, the level is {machine.Level}");
                        line.CardRelease();

                        var mute = Assert.Single(line.Placed.OfType<Button>());
                        // The large level of the tall form (three rows at a large text, §442) is drawn, not a field.
                        var number = line.Placed.OfType<Label>().SingleOrDefault(label => int.TryParse(label.Text, out _));
                        foreach (var (name, over) in new[] { ("the slider", Centre(slider.Bounds)), ("the mute", Centre(mute.Bounds)), ("the level", number is null ? Centre(slider.Bounds) : Centre(number.Bounds)) })
                        {
                            var before = machine.Level;
                            Assert.True(line.CardWheel(over, box, style, WidgetSurface.List, 1), $"{at}: the wheel over {name} was not taken");
                            Assert.True(machine.Level > before, $"{at}: the wheel over {name} left the level at {machine.Level}");
                        }

                        // And a click on its speaker still mutes, as it does in the flyout.
                        _ = line.Click(line.CardTarget(Centre(mute.Bounds), box, style, WidgetSurface.List));
                        Assert.True(machine.Muted, $"{at}: the speaker did not mute");
                    }
                }
            }
        });
    }

    /// <summary>
    /// The line keeps the list's one gutter, as every line of a list does (ListForm.Inset): its mute
    /// stands in the icon column the gutter starts and its level ends on the gutter, at every scale and text size.
    /// </summary>
    [UiFact]
    public void ItKeepsTheListsOneGutter()
    {
        Offscreen.OnStaThread(gfx =>
        {
            foreach (var scale in Scales)
            {
                foreach (var textScale in TextScales)
                {
                    using var line = Line(gfx, new Machine());
                    var style = Style(scale, textScale);
                    var form = ListForm.Of(gfx, style);
                    var width = MathF.Round(360 * scale);
                    line.LayOut(new D2D_RECT_F(0, 0, width, RowsPx(1, scale, textScale)), style);
                    var mute = Assert.Single(line.Placed.OfType<Button>());
                    var number = line.Placed.OfType<Label>().Single(label => int.TryParse(label.Text, out _));
                    // Its button centred on the icon column the gutter starts, where every switch's glyph is (§177).
                    var column = form.IconBox(0, 0, form.Line);
                    Assert.True(MathF.Abs(((column.left + column.right) / 2) - ((mute.Bounds.left + mute.Bounds.right) / 2)) <= 1, $"the mute is not in the icon column at {scale}x, text {textScale}");
                    Assert.Equal(width - form.Inset, number.Bounds.right, 1);
                }
            }
        });
    }

    /// <summary>
    /// The line at one, two and three rows, at 150 % and at the largest text, dark and light, on the
    /// folder's panel with each line's box drawn faintly round it. To %TEMP%\karpo-cards, or
    /// KARPO_CARDS_DIR.
    /// </summary>
    [UiFact]
    public void PicturesAtOneToThreeRows()
    {
        Offscreen.OnStaThread(gfx =>
        {
            const float scale = 1.5f;
            foreach (var dark in new[] { true, false })
            {
                foreach (var textScale in TextScales)
                {
                    var style = Style(scale, textScale, dark);
                    var theme = style.Theme;
                    var width = MathF.Round(360 * scale);
                    var pad = MathF.Round(12 * scale);
                    var label = MathF.Round(10 * scale);
                    var labelLine = TilePaint.LineHeight(gfx, label);
                    var heights = new[] { 1, 2, 3 }.Select(rows => RowsPx(rows, scale, textScale)).ToArray();
                    var sheetWidth = width + (2 * pad);
                    var sheetHeight = pad + heights.Sum(height => labelLine + height + pad);
                    var path = Path.Combine(Offscreen.Pictures, $"soundVolume-lines{(textScale > 1 ? "-225" : "")}{(dark ? "" : "-light")}.png");
                    var lines = heights.Select(_ => Line(gfx, new Machine { Level = 0.62f })).ToList();
                    try
                    {
                        Offscreen.Bitmap(sheetWidth, sheetHeight, dc =>
                        {
                            PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, sheetWidth, sheetHeight), 0, PopupPaint.Ink(theme.Base));
                            var y = pad;
                            for (var index = 0; index < heights.Length; index++)
                            {
                                PopupPaint.Text(dc, gfx, index == 0 ? "1 row" : $"{index + 1} rows", label, Weight.Regular, theme.TextSecondary, pad, y);
                                y += labelLine;
                                PopupPaint.Stroke(dc, PopupPaint.Rect(pad, y, width, heights[index]), 0, 1, PopupPaint.Ink(theme.CardStroke));
                                var box = new D2D_RECT_F(0, 0, width, heights[index]);
                                var line = lines[index];
                                Offscreen.Shifted(dc, pad, y, () => line.PaintOn(dc, box, style, WidgetSurface.List));
                                y += heights[index] + pad;
                            }
                        }, bitmap => Offscreen.Save(bitmap, path));
                    }
                    finally
                    {
                        foreach (var line in lines)
                        {
                            line.Dispose();
                        }
                    }
                    Assert.True(File.Exists(path));
                }
            }
        });
    }

    /// <summary>
    /// The person, 2026-10-30: "the speakers widget layout seems off" — in a folder the ">" to the outputs' page stood
    /// alone at the trailing edge between the name's row and the slider's (centred on the whole line), and the slider
    /// began under the speaker's button and ended short of the ">". In every size, at 100 and 150 % and 100 and 225 %
    /// text: the ">" is the trailing control of the row the speaker stands on (their middles level), after the level and
    /// the output's name; and where the slider has a row of its own its track runs from where the words begin to the
    /// trailing gutter, under the ">".
    /// </summary>
    [UiFact]
    public void TheChevronIsOnTheSpeakersRowAndTheSliderUnderTheWords()
    {
        var failures = new List<string>();
        Offscreen.OnStaThread(gfx =>
        {
            foreach (var size in ListKind.SoundVolume.Offered())
            {
                var item = ListKind.SoundVolume.New();
                item.Size = size;
                item.Normalize();
                SoundParts.Made = () => new Machine { Level = 0.45f };
                FolderIsland folder;
                try
                {
                    folder = ListFormPictures.Folder(gfx, item);
                }
                finally
                {
                    SoundParts.Made = null;
                }
                try
                {
                    var line = Assert.IsType<SoundPartTile>(folder.Tiles[0].Island);
                    Assert.True(line.PageShown, "the volume in a folder has no page");
                    foreach (var scale in new[] { 1f, 1.5f })
                    {
                        foreach (var textScale in TextScales)
                        {
                            var look = ListFormPictures.LookFor(dark: true, textScale);
                            var style = FolderFlyout.StyleFor(scale, look, FlyoutTheme.For(look));
                            var (natural, plan) = ListFormPictures.Plan(folder, look, scale);
                            var width = plan.Cards[0].right - plan.Cards[0].left;
                            var height = natural[0];
                            var at = $"{size} at {scale * 100:0} %, {textScale * 100:0} % text";
                            line.LayOut(new D2D_RECT_F(0, 0, width, height), style);
                            var buttons = line.Placed.OfType<Button>().ToList();
                            var page = buttons.Single(button => button.Name == S.DockPageOutputs).Bounds;
                            var mute = buttons.Single(button => button.Name != S.DockPageOutputs).Bounds;
                            var slider = Assert.Single(line.Placed.OfType<Slider>()).Bounds;
                            if (MathF.Abs(Centre(page).Y - Centre(mute).Y) > 1)
                            {
                                failures.Add($"{at}: the \">\" stands at {Centre(page).Y:0}, the speaker's row at {Centre(mute).Y:0}");
                            }
                            if (page.top < -0.5f || page.bottom > height + 0.5f)
                            {
                                failures.Add($"{at}: the \">\" leaves the line");
                            }
                            foreach (var label in line.Placed.OfType<Label>().Where(label => label.Bounds.right > page.left + 0.5f))
                            {
                                failures.Add($"{at}: \"{label.Text}\" runs on under the \">\"");
                            }
                            var form = ListForm.Of(gfx, style);
                            if (slider.top >= mute.bottom - 0.5f)
                            {
                                // The slider's own row: from the text edge to the trailing gutter, its thumb at either end touching them.
                                if (MathF.Abs(slider.left - form.TextLeft) > 1)
                                {
                                    failures.Add($"{at}: the slider starts at {slider.left:0}, not where the words do ({form.TextLeft:0})");
                                }
                                if (MathF.Abs(slider.right - (width - form.Inset)) > 1)
                                {
                                    failures.Add($"{at}: the slider ends at {slider.right:0}, not on the trailing gutter ({width - form.Inset:0})");
                                }
                                if (page.bottom > slider.top + 0.5f && Centre(page).Y > mute.bottom)
                                {
                                    failures.Add($"{at}: the \">\" stands between the rows");
                                }
                            }
                        }
                    }
                }
                finally
                {
                    folder.Closed();
                    folder.Dispose();
                }
            }
        });
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    /// <summary>
    /// The volume's line (its ">" with it) and the brightness's in a folder in each of their sizes: slider-sizes-{dark,light}.png
    /// at 150 % and 100 %, and slider-sizes-225.png at the largest text, in the pictures folder (KARPO_PNG_DIR).
    /// </summary>
    [UiFact]
    public void PicturesOfEverySizeInAFolder()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var folders = new List<(LineSize Size, FolderIsland Folder)>();
            SoundParts.Made = () => new Machine { Level = 0.45f };
            Karpo.Dock.QuickSettings.Services.Brightness.Stand = new SampleScreens().Add(Screen, @"\\.\DISPLAY1", "SAMSUNG", 62);
            try
            {
                foreach (var size in ListKind.SoundVolume.Offered())
                {
                    var lines = new[] { ListKind.SoundVolume, ListKind.Brightness }.Select(kind => ListFormPictures.Line(kind, line => line.Size = size)).ToArray();
                    folders.Add((size, ListFormPictures.Folder(gfx, lines)));
                }
            }
            finally
            {
                SoundParts.Made = null;
                Karpo.Dock.QuickSettings.Services.Brightness.Stand = null;
            }
            try
            {
                foreach (var dark in new[] { true, false })
                {
                    var look = ListFormPictures.LookFor(dark);
                    foreach (var scale in new[] { 1.5f, 1f })
                    {
                        ListFormPictures.Sheet(gfx, $"slider-sizes-{(int)(scale * 100)}-{(dark ? "dark" : "light")}.png", scale,
                            [.. folders.Select(each => ($"{each.Size}: {scale * 100:0} %", look, each.Folder, ListFormPictures.Plan(each.Folder, look, scale).Plan))]);
                    }
                }
                var large = ListFormPictures.LookFor(dark: true, 2.25f);
                ListFormPictures.Sheet(gfx, "slider-sizes-225.png", 1f,
                    [.. folders.Select(each => ($"{each.Size}: 100 %, 225 % text", large, each.Folder, ListFormPictures.Plan(each.Folder, large, 1f).Plan))]);
            }
            finally
            {
                foreach (var (_, folder) in folders)
                {
                    folder.Closed();
                    folder.Dispose();
                }
            }
        });
    }

    // --- The brightness's line beside it ------------------------------------------------------------

    /// <summary>A made-up display the brightness's line stands on: nothing here reads or sets a real screen.</summary>
    private const nint Screen = 0x10;

    /// <summary>
    /// The volume's line and the brightness's are one layout (SliderLine; 2026-10-11, the person: "the
    /// volume and brigness slider look different in terms of layout, this should not be"): at every
    /// height from one row to four, every scale and both text sizes, the glyph's button, the slider and
    /// the level's field stand in the same place, and the level is said the same way.
    /// </summary>
    [UiFact]
    public void TheBrightnessLineIsTheVolumeLinesLayout()
    {
        Offscreen.OnStaThread(gfx =>
        {
            foreach (var scale in Scales)
            {
                foreach (var textScale in TextScales)
                {
                    for (var rows = 1; rows <= 4; rows++)
                    {
                        using var volume = Line(gfx, new Machine { Level = 0.62f });
                        using var brightness = Brightness(gfx, 62);
                        var box = new D2D_RECT_F(0, 0, MathF.Round(360 * scale), RowsPx(rows, scale, textScale));
                        var style = Style(scale, textScale) with { Screen = Screen };
                        var at = $"{rows} row(s) at {scale:0.##}x, text {textScale:0.##}";
                        volume.LayOut(box, style);
                        brightness.LayOut(box, style);
                        Assert.Equal(Boxes(volume.Placed), Boxes(brightness.LinePlaced));
                        Assert.Equal(
                            volume.Placed.OfType<Label>().Select(label => label.Text),
                            brightness.LinePlaced.OfType<Label>().Select(label => label.Text));
                        Assert.True(brightness.LinePlaced.OfType<Slider>().Any(), $"{at}: no slider");
                    }
                }
            }
        });

        static List<(string, D2D_RECT_F)> Boxes(IEnumerable<Widget> placed) =>
            [.. placed.Select(control => (control.GetType().Name, control.Bounds)).OrderBy(each => each.Item1, StringComparer.Ordinal)];
    }

    /// <summary>
    /// The volume's line and the brightness's side by side at one, two and three rows, at 100 and 150
    /// %, dark and light, on the folder's panel with each line's box drawn faintly round it:
    /// slider-lines-{100,150}{,-light}.png in the pictures folder (KARPO_PNG_DIR).
    /// </summary>
    [UiFact]
    public void PicturesSideBySide()
    {
        Offscreen.OnStaThread(gfx =>
        {
            foreach (var scale in new[] { 1f, 1.5f })
            {
                foreach (var dark in new[] { true, false })
                {
                    var style = Style(scale, 1, dark) with { Screen = Screen };
                    var theme = style.Theme;
                    var width = MathF.Round(360 * scale);
                    var pad = MathF.Round(12 * scale);
                    var label = MathF.Round(10 * scale);
                    var labelLine = TilePaint.LineHeight(gfx, label);
                    var heights = new[] { 1, 2, 3 }.Select(rows => RowsPx(rows, scale, 1)).ToArray();
                    var sheetWidth = pad + width + pad + width + pad;
                    var sheetHeight = pad + heights.Sum(height => labelLine + height + pad);
                    var path = Path.Combine(Offscreen.Pictures, $"slider-lines-{(int)MathF.Round(scale * 100)}{(dark ? "" : "-light")}.png");
                    var volumes = heights.Select(_ => Line(gfx, new Machine { Level = 0.62f })).ToList();
                    var brightnesses = heights.Select(_ => Brightness(gfx, 62)).ToList();
                    try
                    {
                        Offscreen.Bitmap(sheetWidth, sheetHeight, dc =>
                        {
                            PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, sheetWidth, sheetHeight), 0, PopupPaint.Ink(theme.Base));
                            var y = pad;
                            for (var index = 0; index < heights.Length; index++)
                            {
                                PopupPaint.Text(dc, gfx, index == 0 ? "1 row" : $"{index + 1} rows", label, Weight.Regular, theme.TextSecondary, pad, y);
                                y += labelLine;
                                var box = new D2D_RECT_F(0, 0, width, heights[index]);
                                foreach (var (x, line) in new (float, CellIsland)[] { (pad, volumes[index]), (pad + width + pad, brightnesses[index]) })
                                {
                                    PopupPaint.Stroke(dc, PopupPaint.Rect(x, y, width, heights[index]), 0, 1, PopupPaint.Ink(theme.CardStroke));
                                    Offscreen.Shifted(dc, x, y, () => line.PaintOn(dc, box, style, WidgetSurface.List));
                                }
                                y += heights[index] + pad;
                            }
                        }, bitmap => Offscreen.Save(bitmap, path));
                    }
                    finally
                    {
                        foreach (var line in volumes.Concat<CellIsland>(brightnesses))
                        {
                            line.Dispose();
                        }
                    }
                    Assert.True(File.Exists(path));
                }
            }
        });
    }

    /// <summary>A brightness line over one made-up display at <paramref name="level"/>, made the way a list folder makes it.</summary>
    private static BrightnessIsland Brightness(Gfx gfx, int level)
    {
        var cell = ListKind.Brightness.New();
        Karpo.Dock.QuickSettings.Services.Brightness.Stand = new SampleScreens().Add(Screen, @"\\.\DISPLAY1", "SAMSUNG", level);
        try
        {
            var made = Karpo.Dock.Islands.CreateLine(Karpo.Core.Config.IslandKey(null, cell.Id), cell, ListKind.Brightness, gfx, new Karpo.Core.Config());
            return Assert.IsType<BrightnessIsland>(made);
        }
        finally
        {
            Karpo.Dock.QuickSettings.Services.Brightness.Stand = null;
        }
    }

    // --- The machine, and the lines it stands behind --------------------------------------------------

    /// <summary>
    /// A made-up output with a level and a mute, which records what it is asked to set and says it
    /// changed, as Windows does, so the line reads the level back.
    /// </summary>
    private sealed class Machine : ISoundSource
    {
        private Changes? news;

        public float Level { get; set; } = 0.5f;

        public bool Muted { get; private set; }

        public void Start(Changes changes) => news = changes;

        public SoundState Read(int changes) =>
            new(true, Level, Muted, [new AudioOutput("speakers", "Speakers (Realtek(R) Audio)", WindowsSound.GlyphFor(1), true)], []);

        public void SetVolume(float value)
        {
            Level = value;
            news?.Add(SoundNews.Volume);
        }

        public void SetMuted(bool muted)
        {
            Muted = muted;
            news?.Add(SoundNews.Volume);
        }

        public void SetDefault(string id)
        {
        }

        public void SetDefaultInput(string id)
        {
        }

        public void SetInputMuted(bool muted)
        {
        }

        public void SetAppVolume(string key, float value)
        {
        }

        public void SetAppMuted(string key, bool muted)
        {
        }

        public void Dispose()
        {
        }
    }

    /// <summary>A volume line, made the way a list folder makes it, over <paramref name="machine"/>.</summary>
    private static SoundPartTile Line(Gfx gfx, Machine machine)
    {
        var widget = ListKind.SoundVolume.New();
        SoundParts.Made = () => machine;
        try
        {
            var made = Karpo.Dock.Islands.CreateLine(Karpo.Core.Config.IslandKey(null, widget.Id), widget, ListKind.SoundVolume, gfx, new Karpo.Core.Config());
            return Assert.IsType<SoundPartTile>(made);
        }
        finally
        {
            SoundParts.Made = null;
        }
    }

    private static TileStyle Style(float scale, float textScale, bool dark = true)
    {
        var look = TestLooks.LookFor(dark, textScale);
        return new TileStyle(scale, look, FlyoutTheme.For(look), 1, 1);
    }

    private static Vector2 Centre(D2D_RECT_F box) => new((box.left + box.right) / 2, (box.top + box.bottom) / 2);
}
