// §605: the volume's and the brightness's lines as the person asked for them on 2026-11-09.
//
// * The volume's line beside its controls is its page's, as the Bluetooth line beside its switch is: a click on the
//   line's room opens the sound page, one on the speaker's button or the slider is theirs; under the pointer there the
//   whole line lights, on a control only the control.
// * What stands before the volume's ">" keeps the air a switch's line's words keep from it, and the ">" and the level
//   stand on the list's gutters.
// * The brightness's leading button is Windows' light or dark mode: a sun or a moon, named and told as the dark mode
//   switch is, a click switching it — on the bar a press and a release on it. A made-up switch stands behind every one
//   of them (QuickSwitches.Source): nothing here turns this PC dark or light.
//
// A made-up output and display stand behind the lines (SoundParts.Made, Brightness.Stand).

using System.Numerics;
using Karpo.Core;
using Karpo.Dock;
using Karpo.Dock.QuickSettings.Controls;
using Karpo.Dock.QuickSettings.Services;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;

namespace Karpo.Tests.Folders;

public sealed class QuickSliderLineTests
{
    /// <summary>A made-up display the brightness's line stands on.</summary>
    private const nint Screen = 0x10;

    /// <summary>A made-up output, one level, read and set nowhere.</summary>
    private sealed class Output : ISoundSource
    {
        public void Start(Changes changes)
        {
        }

        public SoundState Read(int changes) =>
            new(true, 0.45f, false, [new AudioOutput("speakers", "Speakers (Realtek(R) Audio)", WindowsSound.GlyphFor(1), true)], []);

        public void SetVolume(float value)
        {
        }

        public void SetMuted(bool muted)
        {
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

    private static Vector2 Centre(D2D_RECT_F box) => new((box.left + box.right) / 2, (box.top + box.bottom) / 2);

    /// <summary>A list folder holding the volume's line of <paramref name="size"/>, opened, on a made-up output.</summary>
    private static FolderIsland VolumeFolder(Gfx gfx, LineSize size = LineSize.Compact)
    {
        var item = ListKind.SoundVolume.New();
        item.Size = size;
        item.Normalize();
        SoundParts.Made = () => new Output();
        try
        {
            return ListFormPictures.Folder(gfx, item);
        }
        finally
        {
            SoundParts.Made = null;
        }
    }

    /// <summary>
    /// Beside its controls the volume's line is its page's (the person: "when i click anywhere it should go into the sound
    /// settings, except for the areas that are with other buttons"): its corners, the level and the ">" answer the page's
    /// target, whose click opens the sound page; the speaker's button and the slider answer their own. Under the pointer
    /// beside them the whole line lights; on the slider, not. On a page itself the line has no ">" and its room does nothing.
    /// </summary>
    [UiFact]
    public void BesideItsControlsTheVolumesLineOpensItsPage()
    {
        Offscreen.OnStaThread(gfx =>
        {
            foreach (var size in ListKind.SoundVolume.Offered())
            {
                var folder = VolumeFolder(gfx, size);
                try
                {
                    var line = Assert.IsType<SoundPartTile>(folder.Tiles[0].Island);
                    Assert.True(line.PageShown);
                    var look = ListFormPictures.LookFor(dark: true);
                    var style = FolderFlyout.StyleFor(1.5f, look, FlyoutTheme.For(look));
                    var (natural, plan) = ListFormPictures.Plan(folder, look, 1.5f);
                    var box = new D2D_RECT_F(0, 0, plan.Cards[0].right - plan.Cards[0].left, natural[0]);
                    line.LayOut(box, style);
                    var page = line.Placed.OfType<Button>().Single(button => button.Name == S.DockPageOutputs);
                    var mute = line.Placed.OfType<Button>().Single(button => button.Name == S.WidgetSoundMute);
                    var slider = Assert.Single(line.Placed.OfType<Slider>());
                    var pageTarget = line.Placed.ToList().IndexOf(page) + 1;
                    var at = $"{size}";

                    // The line's room: its corners, its middle's left edge, the level's field and the ">".
                    var beside = new List<Vector2>
                    {
                        new(2, 2), new(box.right - 2, 2), new(2, box.bottom - 2), new(box.right - 2, box.bottom - 2), Centre(page.Bounds),
                    };
                    if (line.Placed.OfType<Label>().FirstOrDefault(label => label.Trailing) is { } level)
                    {
                        beside.Add(Centre(level.Bounds));
                    }
                    foreach (var point in beside)
                    {
                        Assert.True(pageTarget == line.CardTarget(point, box, style, WidgetSurface.List), $"{at}: {point} is not the page's");
                    }
                    Assert.Equal(line.Placed.ToList().IndexOf(mute) + 1, line.CardTarget(Centre(mute.Bounds), box, style, WidgetSurface.List));
                    Assert.Equal(line.Placed.ToList().IndexOf(slider) + 1, line.CardTarget(Centre(slider.Bounds), box, style, WidgetSurface.List));

                    // Lit whole beside them, not on them.
                    line.CardHover(new Vector2(2, 2), box, style, WidgetSurface.List);
                    Assert.True(line.PageLit, $"{at}: the line is not lit beside its controls");
                    Assert.False(page.Hot, $"{at}: the \">\" lights beside the line");
                    line.CardHover(Centre(slider.Bounds), box, style, WidgetSurface.List);
                    Assert.False(line.PageLit, $"{at}: the line lights with the slider under the pointer");
                    Assert.True(slider.Hot);
                    line.CardHover(Centre(mute.Bounds), box, style, WidgetSurface.List);
                    Assert.False(line.PageLit);
                    Assert.True(mute.Hot);
                    line.CardHover(null, box, style, WidgetSurface.List);
                    Assert.False(line.PageLit);

                    // A press beside them is no drag; its click is the page.
                    Assert.False(line.CardPress(new Vector2(2, 2), box, style, WidgetSurface.List));
                    Assert.Equal(IslandView.Page, line.Click(pageTarget).View);
                    Assert.True(folder.OpenPageOf(line.Key));
                    // On the page its own volume has no ">", and its room does nothing.
                    var own = folder.Tiles.Select(tile => tile.Island).OfType<SoundPartTile>().FirstOrDefault(tile => tile.Region == SoundPart.Volume);
                    if (own is not null)
                    {
                        Assert.False(own.PageShown);
                        Assert.Equal(0, own.CardTarget(new Vector2(2, 2), box, style, WidgetSurface.List));
                    }
                }
                finally
                {
                    folder.Closed();
                    folder.Dispose();
                }
            }
        });
    }

    /// <summary>
    /// The volume's ">" and what stands before it (the person: "make sure that the buttons are properly spaced and have
    /// the proper spacing on the side as well"): in every size, at 100 and 150 % and 100 and 225 % text, the ">"'s glyph
    /// ends on the trailing gutter, the speaker's button stands on the icon column — the gutter's air on both sides —, and
    /// the level and the output's name end the switches' lines' air (14 DIP) before the ">"'s glyph, as a switch's
    /// line's words do. The brightness's level, with no ">", ends on the trailing gutter.
    /// </summary>
    [UiFact]
    public void WhatStandsBeforeTheChevronKeepsTheSwitchesAir()
    {
        var failures = new List<string>();
        Offscreen.OnStaThread(gfx =>
        {
            foreach (var size in ListKind.SoundVolume.Offered())
            {
                var folder = VolumeFolder(gfx, size);
                try
                {
                    var line = Assert.IsType<SoundPartTile>(folder.Tiles[0].Island);
                    foreach (var scale in new[] { 1f, 1.5f })
                    {
                        foreach (var textScale in new[] { 1f, 2.25f })
                        {
                            var look = ListFormPictures.LookFor(dark: true, textScale);
                            var style = FolderFlyout.StyleFor(scale, look, FlyoutTheme.For(look));
                            var (natural, plan) = ListFormPictures.Plan(folder, look, scale);
                            var box = new D2D_RECT_F(0, 0, plan.Cards[0].right - plan.Cards[0].left, natural[0]);
                            var at = $"{size} at {scale * 100:0} %, {textScale * 100:0} % text";
                            line.LayOut(box, style);
                            var gutter = ListForm.Gutter(style);
                            var glyph = SwitchIsland.ChevronGlyphBox(box, style, switched: false);
                            var air = style.Px(SwitchIsland.ChevronAirDip);
                            var end = glyph.left - air;
                            if (MathF.Abs(glyph.right - (box.right - gutter)) > 0.5f)
                            {
                                failures.Add($"{at}: the \">\" ends at {glyph.right:0.#}, not on the trailing gutter ({box.right - gutter:0.#})");
                            }
                            var page = line.Placed.OfType<Button>().Single(button => button.Name == S.DockPageOutputs).Bounds;
                            if (MathF.Abs(Centre(page).X - ((glyph.left + glyph.right) / 2)) > 1)
                            {
                                failures.Add($"{at}: the \">\"'s button is not round its glyph");
                            }
                            var mute = line.Placed.OfType<Button>().Single(button => button.Name == S.WidgetSoundMute).Bounds;
                            var icon = ListForm.Of(gfx, style);
                            if (MathF.Abs(Centre(mute).X - (gutter + (icon.Icon / 2))) > 1)
                            {
                                failures.Add($"{at}: the speaker stands at {Centre(mute).X:0.#}, not on the icon column ({gutter + (icon.Icon / 2):0.#})");
                            }
                            // Where the button round the ">" grows past the air (a large text), short of the button.
                            end = MathF.Min(end, line.Placed.OfType<Button>().Single(button => button.Name == S.DockPageOutputs).Bounds.left);
                            foreach (var label in line.Placed.OfType<Label>().Where(label => label.Trailing))
                            {
                                if (MathF.Abs(label.Bounds.right - end) > 0.5f)
                                {
                                    failures.Add($"{at}: the level ends at {label.Bounds.right:0.#}, not {air / scale:0} DIP before the \">\" ({end:0.#})");
                                }
                            }
                            foreach (var words in line.Words.Where(drawn => !drawn.Glyph && drawn.Box.top < page.bottom && drawn.Box.bottom > page.top))
                            {
                                if (words.Box.right > end + 0.5f)
                                {
                                    failures.Add($"{at}: \"{words.Text}\" ends at {words.Box.right:0.#}, past {end:0.#}");
                                }
                            }

                            using var brightness = Brightness(gfx, size);
                            brightness.LayOut(box, style with { Screen = Screen });
                            foreach (var label in brightness.LinePlaced.OfType<Label>().Where(label => label.Trailing))
                            {
                                if (MathF.Abs(label.Bounds.right - (box.right - gutter)) > 0.5f)
                                {
                                    failures.Add($"{at}: the brightness's level ends at {label.Bounds.right:0.#}, not on the gutter");
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

    /// <summary>A brightness line of <paramref name="size"/> over one made-up display at 62, made as a list folder makes it.</summary>
    private static BrightnessIsland Brightness(Gfx gfx, LineSize size = LineSize.Compact)
    {
        var cell = ListKind.Brightness.New();
        cell.Size = size;
        cell.Normalize();
        Karpo.Dock.QuickSettings.Services.Brightness.Stand = new SampleScreens().Add(Screen, @"\\.\DISPLAY1", "SAMSUNG", 62);
        try
        {
            var made = Karpo.Dock.Islands.CreateLine(Config.IslandKey(null, cell.Id), cell, ListKind.Brightness, gfx, new Config());
            return Assert.IsType<BrightnessIsland>(made);
        }
        finally
        {
            Karpo.Dock.QuickSettings.Services.Brightness.Stand = null;
        }
    }

    /// <summary>
    /// The brightness's leading button is Windows' mode (the person: "the screen brigtness selector should have the toggle
    /// brigth and dark mode on the side instead of the brightness icon"): a sun while light, a moon while dark, named
    /// "Dark mode" and told as its switch ("Dark mode: off"); its click — the line's mode target — switches it, the slider's
    /// target still the slider's and the line's room nothing. A made-up switch: this PC's mode is never touched.
    /// </summary>
    [UiFact]
    public void TheBrightnessesButtonSwitchesWindowsLightAndDark()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var mode = new SampleSwitch(WidgetInfos.DarkMode.Title, on: false, switches: true);
            QuickSwitches.Source = which => which == QuickSwitch.DarkMode ? mode : null;
            try
            {
                using var line = Brightness(gfx);
                var look = ListFormPictures.LookFor(dark: true);
                var style = FolderFlyout.StyleFor(1.5f, look, FlyoutTheme.For(look)) with { Screen = Screen };
                var box = new D2D_RECT_F(0, 0, 540, 60);
                line.LayOut(box, style);
                var button = line.LinePlaced.OfType<Button>().Single();
                Assert.Equal(WidgetInfos.DarkMode.Title, button.Name);
                Assert.Equal(BrightnessIsland.Sun, button.Glyph);
                var tip = line.CardTip(Centre(button.Bounds), box, style, WidgetSurface.List);
                Assert.NotNull(tip);
                Assert.StartsWith(S.WidgetSwitchOffTip(WidgetInfos.DarkMode.Title), tip.Value.Text);

                var target = line.CardTarget(Centre(button.Bounds), box, style, WidgetSurface.List);
                Assert.Equal(BrightnessIsland.ModeTarget, target);
                Assert.False(line.CardPress(Centre(button.Bounds), box, style, WidgetSurface.List));
                Assert.Equal(IslandView.None, line.Click(target).View);
                Assert.True(mode.On);
                line.LayOut(box, style);
                Assert.Equal(BrightnessIsland.Moon, button.Glyph);
                Assert.StartsWith(S.WidgetSwitchOnTip(WidgetInfos.DarkMode.Title), line.CardTip(Centre(button.Bounds), box, style, WidgetSurface.List)!.Value.Text);

                // The slider is still the slider's; the line's room does nothing.
                var slider = line.LinePlaced.OfType<Slider>().Single();
                Assert.Equal(BrightnessIsland.SetLevelTarget, line.CardTarget(Centre(slider.Bounds), box, style, WidgetSurface.List));
                Assert.Equal(0, line.CardTarget(new Vector2(2, 2), box, style, WidgetSurface.List));
                _ = line.Click(0);
                Assert.True(mode.On);

                // Under the pointer the button lights, and the line's stops are the button and the slider.
                line.CardHover(Centre(button.Bounds), box, style, WidgetSurface.List);
                Assert.True(button.Hot);
                line.CardHover(null, box, style, WidgetSurface.List);
                Assert.False(button.Hot);
                Assert.Equal([BrightnessIsland.ModeTarget, BrightnessIsland.SetLevelTarget], LineFocus.Parts(line, box, style, WidgetSurface.List).Select(part => part.Target));
            }
            finally
            {
                QuickSwitches.Source = null;
            }
        });
    }

    /// <summary>
    /// On the bar the mode is a button of its own beside the slider: a press on it is taken, and switches the mode where
    /// the release comes on it — not where the pointer left it first —; a press on the track is the slider's; in the short
    /// form the button is the widget. A made-up switch.
    /// </summary>
    [UiFact]
    public void OnTheBarTheModesButtonSwitchesOnItsRelease()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var mode = new SampleSwitch(WidgetInfos.DarkMode.Title, on: false, switches: true);
            QuickSwitches.Source = which => which == QuickSwitch.DarkMode ? mode : null;
            Karpo.Dock.QuickSettings.Services.Brightness.Stand = new SampleScreens().Add(Screen, @"\\.\DISPLAY1", "SAMSUNG", 40);
            try
            {
                var item = BarKind.Brightness.New();
                using var bar = Assert.IsType<BrightnessIsland>(Karpo.Dock.Islands.Create(Config.IslandKey(null, item.Id), item, gfx, new Config()));
                ISliderIsland slider = bar;
                var look = ListFormPictures.LookFor(dark: true);
                var style = new IslandStyle(1.5f, false, 72, 36, look) { Screen = Screen };
                var along = BrightnessIsland.Plan(style, style.Thickness).Along;
                var plan = BrightnessIsland.Plan(style, style.Thickness, along);
                Assert.True(plan.Slider);
                var size = new Vector2(along, style.Thickness);
                var cell = new D2D_RECT_F(0, 0, size.X, size.Y);
                var glyph = Centre(plan.GlyphBox(cell));
                var track = plan.ThumbCentre(cell, 80);
                Assert.True(BrightnessIsland.OnMode(plan, cell, glyph));
                Assert.False(BrightnessIsland.OnMode(plan, cell, track));
                Assert.Equal(BrightnessIsland.Sun, bar.ModeGlyph);

                Assert.True(slider.Press(0, glyph, size, style));
                slider.Release();
                Assert.True(mode.On);
                Assert.Equal(BrightnessIsland.Moon, bar.ModeGlyph);

                // Pressed on it, let go off it: nothing.
                Assert.True(slider.Press(0, glyph, size, style));
                slider.Drag(0, track, size, style);
                slider.Release();
                Assert.True(mode.On);

                // The track is the slider's, and switches nothing.
                Assert.True(slider.Press(0, track, size, style));
                Assert.Equal(Screen, bar.Holding);
                slider.Release();
                Assert.True(mode.On);
                Assert.Equal(default, bar.Click(0));
                Assert.True(mode.On);
            }
            finally
            {
                QuickSwitches.Source = null;
                Karpo.Dock.QuickSettings.Services.Brightness.Stand = null;
            }
        });
    }

    /// <summary>
    /// qs-sliders-&lt;dark|light&gt;.png (KARPO_PNG_DIR): the volume's and the brightness's lines in a folder in each size,
    /// at 150 %, Windows dark on the dark sheet (the moon) and light on the light one (the sun); then the volume's line
    /// under the pointer beside its controls, lit whole, and the brightness's mode button under the pointer.
    /// </summary>
    [UiFact]
    public void QuickSliderSheets()
    {
        Offscreen.OnStaThread(gfx =>
        {
            foreach (var dark in new[] { true, false })
            {
                var mode = new SampleSwitch(WidgetInfos.DarkMode.Title, on: dark, switches: true);
                QuickSwitches.Source = which => which == QuickSwitch.DarkMode ? mode : null;
                var folders = new List<(string Caption, FolderIsland Folder)>();
                try
                {
                    foreach (var size in ListKind.SoundVolume.Offered())
                    {
                        folders.Add(($"{size}: at rest", SliderFolder(gfx, size)));
                    }
                    folders.Add(("Compact: the volume's room and the mode under the pointer", SliderFolder(gfx, LineSize.Compact)));
                    var look = ListFormPictures.LookFor(dark);
                    var panels = folders.Select(each => (each.Caption, look, each.Folder, ListFormPictures.Plan(each.Folder, look, 1.5f).Plan)).ToList();
                    var (_, folder, plan) = (panels[^1].Caption, panels[^1].Folder, panels[^1].Item4);
                    var style = FolderFlyout.StyleFor(1.5f, look, FlyoutTheme.For(look)) with { Screen = Screen };
                    var volume = Assert.IsType<SoundPartTile>(folder.Tiles[0].Island);
                    var volumeBox = new D2D_RECT_F(0, 0, plan.Cards[0].right - plan.Cards[0].left, plan.Cards[0].bottom - plan.Cards[0].top);
                    volume.CardHover(new Vector2(volumeBox.right * 0.8f, 3), volumeBox, style, WidgetSurface.List);
                    Assert.True(volume.PageLit);
                    var brightness = Assert.IsType<BrightnessIsland>(folder.Tiles[1].Island);
                    var brightnessBox = new D2D_RECT_F(0, 0, plan.Cards[1].right - plan.Cards[1].left, plan.Cards[1].bottom - plan.Cards[1].top);
                    brightness.LayOut(brightnessBox, style);
                    var button = brightness.LinePlaced.OfType<Button>().Single();
                    brightness.CardHover(Centre(button.Bounds), brightnessBox, style, WidgetSurface.List);
                    ListFormPictures.Sheet(gfx, $"qs-sliders-{(dark ? "dark" : "light")}.png", 1.5f, panels);
                }
                finally
                {
                    foreach (var (_, folder) in folders)
                    {
                        folder.Closed();
                        folder.Dispose();
                    }
                    QuickSwitches.Source = null;
                }
            }
        });
    }

    /// <summary>A folder of the volume's line and the brightness's in <paramref name="size"/>, on a made-up output and display.</summary>
    private static FolderIsland SliderFolder(Gfx gfx, LineSize size)
    {
        SoundParts.Made = () => new Output();
        Karpo.Dock.QuickSettings.Services.Brightness.Stand = new SampleScreens().Add(Screen, @"\\.\DISPLAY1", "SAMSUNG", 62);
        try
        {
            var lines = new[] { ListKind.SoundVolume, ListKind.Brightness }.Select(kind => ListFormPictures.Line(kind, line => line.Size = size)).ToArray();
            return ListFormPictures.Folder(gfx, lines);
        }
        finally
        {
            SoundParts.Made = null;
            Karpo.Dock.QuickSettings.Services.Brightness.Stand = null;
        }
    }
}
