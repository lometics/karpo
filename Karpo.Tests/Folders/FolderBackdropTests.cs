// folders, §623 (the person, 2026-11-09: "it should be possible to actually set a background for the whole screen when a
// folder is open. that way you can darken everything else when a folder is open for example. this background should be
// applied to all the screens when the folder is open."):
//
//  - **The setting.** The Appearance's Flyouts › "Background while a folder is open" (Tone.Backdrop): a colour pair as every
//    colour of the Appearance, none by default (Karpo's own and every palette's design), picked from none at half its
//    opacity; written, read back, reset; the cards and the bar never have one.
//  - **When it stands** (FolderBackdrop.Of): a folder open, out of edit mode, out of high contrast, a colour with something
//    of it to see.
//  - **The real dock** (Karpo.exe --stage on a desktop of its own, two displays at two scales): a folder opened at rest lays
//    it over both displays, in its colour, under the bars and the folder; closed, it goes; a click on it closes the folder,
//    the press the backdrop's alone; edit mode has its dim, not the backdrop; by the motion, at once with animations off.

using Karpo.Dock;
using Karpo.Tests.Core;
using Karpo.Tests.Islands;
using Karpo.Tests.Menus;
using Karpo.Tests.Present;
using TerraFX.Interop.Windows;
using Xunit.Abstractions;
using static TerraFX.Interop.Windows.Windows;
using Color = Windows.UI.Color;
using Edge = Karpo.Core.Edge;
using Spot = Karpo.Tests.Present.PanelPopupPlaceTests.Spot;

namespace Karpo.Tests.Folders;

public sealed unsafe class FolderBackdropTests(ITestOutputHelper output)
{
    private static readonly string Key = LookRows.ColourKey(LookRows.SectionKey(LookPart.Flyouts), Tone.Backdrop);

    private static OptionRow Row(Config config, bool dark) => OptionRows.Find(LookRows.Rows(config, config.DefaultLayout, dark), Key)!;

    /// <summary>
    /// A colour of the Flyouts' tab after their outline, none by default in Karpo's own and every palette; picked from none at
    /// half its opacity, written in the half of the mode shown alone, read back from the file, its reset beside it; reset, none
    /// again. The bar and the cards have none: a backdrop said there is not kept.
    /// </summary>
    [Fact]
    public void TheBackdropIsAFlyoutsColourNoneByDefault()
    {
        // §649: the attached folders' ground after it.
        Assert.Equal([Tone.Outline, Tone.Backdrop, Tone.Ground], LookParts.TonesOf(LookPart.Flyouts));
        Assert.DoesNotContain(Tone.Backdrop, LookParts.TonesOf(LookPart.Taskbar));
        Assert.DoesNotContain(Tone.Backdrop, LookParts.TonesOf(LookPart.Widgets));
        Assert.DoesNotContain(Tone.Backdrop, LookParts.TonesOf(LookPart.FolderWidgets));
        Assert.True(ToneNames.MayBeNone(Tone.Backdrop));
        Assert.Equal("backdrop", ToneNames.JsonName(Tone.Backdrop));
        foreach (var palette in LookPalettes.Choices)
        {
            foreach (var dark in new[] { true, false })
            {
                Assert.Null(palette.Drawn(dark).Flyouts.BackdropColor);
            }
        }

        var config = MenuCorpus.Fresh();
        var flyouts = OptionRows.Find(LookRows.Rows(config, config.DefaultLayout, true), LookRows.SectionKey(LookPart.Flyouts) + LookRows.Tab)!;
        Assert.Equal(
            [LookRows.ColourKey(LookRows.SectionKey(LookPart.Flyouts), Tone.Outline), Key, LookRows.ColourKey(LookRows.SectionKey(LookPart.Flyouts), Tone.Ground)],
            flyouts.Level.Select(row => row.Key));
        var row = Row(config, dark: true);
        Assert.Equal((OptionRowKind.Color, S.SettingsLookBackdrop), (row.Kind, row.Title));
        Assert.Equal((0, S.SettingsAppearanceSeparatorStyleNone), (row.Chosen, row.Tiles[0].Name));
        Assert.Equal(new Argb(0, 0, 0, 0), row.Color);
        // §690: the presets every picker has; the dark taskbar's ground among them, picked at the backdrop's half.
        var ground = row.Tiles.ToList().FindIndex(tile => tile.Color == new Argb(255, 0x20, 0x20, 0x20));
        Assert.True(ground > 0);
        Assert.True(row.Set!(ground));
        Assert.Equal(new Argb(ToneNames.BackdropAlpha, 0x20, 0x20, 0x20), Looks.Of(config, dark: true).Flyouts.BackdropColor);
        Assert.Null(Looks.Of(config, dark: false).Flyouts.BackdropColor);
        Assert.Contains(OptionRows.Find(LookRows.Rows(config, config.DefaultLayout, true), Key + LookRows.ResetEnd)!.Detail!, S.SettingsLookResetValue(S.SettingsLookBackdrop), StringComparison.Ordinal);

        // A colour over one in force keeps its opacity; the file has it in its half, under the palette worn's Flyouts.
        Assert.True(Row(config, dark: true).Set!(Row(config, dark: true).Tiles.ToList().FindIndex(tile => tile.Color == new Argb(255, 0x76, 0x76, 0x76))));
        Assert.Equal(new Argb(0x80, 0x76, 0x76, 0x76), Looks.Of(config, dark: true).Flyouts.BackdropColor);
        var json = config.ToJson();
        Assert.Contains("\"backdrop\": {", json, StringComparison.Ordinal);
        Assert.Contains("\"dark\": \"#80767676\"", json, StringComparison.Ordinal);
        var read = Configs.RoundTrip(config, out var changed);
        Assert.False(changed);
        Assert.Equal("#80767676", read.Appearance![LookPalettes.KarpoId]!.Flyouts!.Backdrop!.Dark);
        Assert.Null(read.Appearance[LookPalettes.KarpoId]!.Flyouts!.Backdrop!.Light);
        Assert.Equal(new Argb(0x80, 0x76, 0x76, 0x76), Looks.Of(read, dark: true).Flyouts.BackdropColor);

        // None picked: none drawn, the word kept.
        Assert.True(Row(read, dark: true).Set!(0));
        Assert.Null(Looks.Of(read, dark: true).Flyouts.BackdropColor);
        Assert.Equal("none", read.Appearance![LookPalettes.KarpoId]!.Flyouts!.Backdrop!.Dark);

        // Its reset: unset, Karpo's own (none) again, nothing left in the file.
        Assert.True(OptionRows.Find(LookRows.Rows(read, read.DefaultLayout, true), Key + LookRows.ResetEnd)!.Set!(0));
        Assert.Null(read.Appearance);
        Assert.Null(OptionRows.Find(LookRows.Rows(read, read.DefaultLayout, true), Key + LookRows.ResetEnd));

        // The bar and the cards keep none.
        foreach (var part in new[] { LookPart.Taskbar, LookPart.Widgets, LookPart.FolderWidgets })
        {
            Assert.False(LookShorthand.Both(Looks.Section(read, part), Tone.Backdrop, new Karpo.Core.Paint(new Argb(0x80, 0, 0, 0))));
        }
        var odd = Configs.Repaired("""
            { "version": 82, "appearance": { "palettes": { "karpo": {
              "widgets": { "backdrop": { "dark": "#80000000" } },
              "flyouts": { "backdrop": { "dark": "#40102030", "light": "dusk" } } } } } }
            """);
        Assert.Null(odd.Appearance![LookPalettes.KarpoId]!.Widgets);
        Assert.Equal(new ColorPair { Dark = "#40102030" }, odd.Appearance[LookPalettes.KarpoId]!.Flyouts!.Backdrop);
    }

    /// <summary>
    /// The file's shape 82 or later (§647: 83); a file of 81, which has no backdrop, reads as none — what it meant —, the rest of it as it was.
    /// </summary>
    [Fact]
    public void AFileOfShape81ReadsWithNoBackdrop()
    {
        Assert.InRange(Config.Shape, 82, int.MaxValue);
        var older = Configs.Repaired("""{ "version": 81, "appearance": { "palette": "graphite" } }""");
        Assert.Equal(LookPalettes.GraphiteId, older.Appearance!.Palette);
        Assert.Null(Looks.Of(older, dark: true).Flyouts.BackdropColor);
        Assert.Null(Looks.Of(older, dark: false).Flyouts.BackdropColor);
    }

    /// <summary>
    /// When it stands: a folder open, out of edit mode (its own dim then), out of high contrast (Windows' colours alone), a
    /// colour said with something of it to see — none for unset, None, or a clear one.
    /// </summary>
    [Fact]
    public void TheBackdropStandsOnlyWhileAFolderIsOpenAtRest()
    {
        var dim = new PartPaints(Backdrop: new Karpo.Core.Paint(new Argb(0x80, 0, 0, 0)));
        Assert.Equal(new Argb(0x80, 0, 0, 0), FolderBackdrop.Of(folderOpen: true, editing: false, highContrast: false, dim));
        Assert.Null(FolderBackdrop.Of(folderOpen: false, editing: false, highContrast: false, dim));
        Assert.Null(FolderBackdrop.Of(folderOpen: true, editing: true, highContrast: false, dim));
        Assert.Null(FolderBackdrop.Of(folderOpen: true, editing: false, highContrast: true, dim));
        Assert.Null(FolderBackdrop.Of(folderOpen: true, editing: false, highContrast: false, default));
        Assert.Null(FolderBackdrop.Of(folderOpen: true, editing: false, highContrast: false, new PartPaints(Backdrop: Karpo.Core.Paint.Nothing)));
        Assert.Null(FolderBackdrop.Of(folderOpen: true, editing: false, highContrast: false, new PartPaints(Backdrop: new Karpo.Core.Paint(new Argb(0, 0, 0, 0)))));
    }

    /// <summary>
    /// The layer itself, as the dock lays it (EditShade, tinted): a folder opened at rest over a bar, the backdrop over the
    /// display in the same handler — under the bar, the bar under the folder; animations on, it fades in (frames between the
    /// before and the end) and out with the folder, its window out of sight once it has; animations off, at once, every frame
    /// the before or the end. Films (KARPO_PNG_DIR): film-backdrop-open-on/-close-on, film-backdrop-open-off/-close-off.
    /// </summary>
    [UiFact]
    public void TheBackdropFadesInAndOutWithTheFolderByTheMotion() => FlickerFilmTests.OnHiddenDesktop("folder-backdrop", (gfx, instance) =>
    {
        Surfaces.FenceHoldMs = 120;
        try
        {
            var display = new RECT { right = 1280, bottom = 720 };
            var bounds = new RectI(0, 0, 1280, 720);
            using var bar = new FlickerFilmTests.Plain(gfx, instance, new RECT { left = 0, top = 648, right = 1280, bottom = 720 }, Color.FromArgb(255, 0x20, 0x40, 0x80));
            Surfaces.Enlist(bar.Hwnd, Stratum.Bar);
            // A colour the films can see: a capture gives what is translucent flattened on black, a half black as black.
            using var backdrop = new EditShade(gfx, instance, 0, Color.FromArgb(0x80, 0xFF, 0x80, 0x00), EditShade.BackdropTitle);
            backdrop.Tint(Color.FromArgb(0x80, 0x00, 0x80, 0xFF));
            Assert.Equal(Color.FromArgb(0x80, 0x00, 0x80, 0xFF), backdrop.Tinted);
            var island = Readings("backdropped");
            using var folder = new FolderIsland($"layout:default/{island.Id}", island, gfx, new Config());
            using var flyout = new FolderFlyout(gfx, instance, 0);
            FrameFilm.Settle();
            HWND[] Windows() => [bar.Hwnd, backdrop.Hwnd, flyout.Hwnd];
            foreach (var moving in new[] { true, false })
            {
                var look = Edit.AddListPictures.LookFor(dark: true) with { Animations = moving };
                var name = moving ? "on" : "off";
                var open = FrameFilm.Shoot($"backdrop-open-{name}", gfx, display, Windows, () =>
                {
                    flyout.Show(gfx, look, 1f, new POINT { x = 640, y = 648 }, new RECT { right = 1280, bottom = 648 }, Edge.Bottom, folder, display: display);
                    backdrop.Show(bounds, dimmed: true, look.Motion);
                }, filmMs: 400);
                Assert.Equal((moving, 1f), backdrop.Faded);
                Assert.True(Z.Above(flyout.Hwnd, bar.Hwnd) && Z.Above(bar.Hwnd, backdrop.Hwnd), "the folder over the bar over the backdrop");
                Assert.True(open.Differs(open.Before, open.End) > 0.5, "the backdrop is not in the film");
                var close = FrameFilm.Shoot($"backdrop-close-{name}", gfx, display, Windows, () =>
                {
                    flyout.Hide();
                    backdrop.Hide(look.Motion);
                }, filmMs: 400);
                Assert.Equal((moving, 0f), backdrop.Faded);
                Assert.False(IsWindowVisible(backdrop.Hwnd), "the backdrop's window stayed once it had faded out");
                if (moving)
                {
                    Assert.NotEmpty(open.Between(everyFrame: true));
                    Assert.NotEmpty(close.Between(everyFrame: true));
                }
                else
                {
                    open.Holds(0.005, everyFrame: true);
                    close.Holds(0.005, everyFrame: true);
                }
                open.Save();
                close.Save();
            }
        }
        finally
        {
            Surfaces.FenceHoldMs = 0;
        }
    });

    /// <summary>A list folder of widgets that only read — a clock, the battery, a heading — fetching nothing, launching nothing.</summary>
    private static BarItem Readings(string id)
    {
        var options = new FolderOptions();
        var island = BarKind.FolderList.New();
        island.Id = id;
        island.Folder = options;
        foreach (var (line, kind) in new[] { ("clock", ListKind.Clock), ("battery", ListKind.Battery) })
        {
            var made = kind.New();
            made.Id = $"{id}-{line}";
            options.Put(made);
        }
        var title = ListKind.Heading.New();
        title.Id = $"{id}-heading";
        title.Heading = new HeadingOptions { Text = "Notes" };
        options.Put(title);
        return island;
    }

    /// <summary>
    /// The real dock on two displays, a backdrop set in both modes: nothing over the displays at first; the folder opened at
    /// rest, the backdrop over each display, whole, in its colour, under the bars and the folder, faded in by the motion (at
    /// once with animations off); its face clicked again, the folder closes and the backdrops go; opened again and the
    /// backdrop clicked — on the display the folder is not on —, the press is the backdrop's and the folder closes; opened
    /// again and edit mode begun, the folder stays, arranged, edit mode's dim in place of the backdrop.
    /// </summary>
    [UiTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void TheBackdropCoversEveryDisplayWhileAFolderIsOpenAndAClickOnItClosesIt(bool animations) =>
        PanelPopupPlaceTests.Drive(output, Edge.Bottom, "-2560,0,2560,1440,144;0,0,1920,1080,96", probe =>
        {
            var faded = animations && WindowsAnimates();
            var bar = probe.Read("stage-map.json", wait: true);
            var face = bar.First(spot => spot.Key.EndsWith("/" + Folder, StringComparison.Ordinal));
            output.WriteLine($"face {face}; faded {faded}");
            Assert.Empty(Backdrops(probe).Where(spot => spot.Index > 0));

            // Opened at rest: over both displays, in its colour, the folder and the bars over it.
            probe.Click(face.Center);
            Assert.NotNull(Open(probe));
            var shown = Settled(probe, standing: true);
            Assert.Equal(2, shown.Count);
            Assert.Equal(["-2560,0,0,1440", "0,0,1920,1080"], shown.Select(spot => Text(spot.Rect)).Order(StringComparer.Ordinal));
            Assert.All(shown, spot => Assert.Equal("#80000000", Part(spot, 1)));
            Assert.All(shown, spot => Assert.Equal(faded ? "faded" : "at once", Part(spot, 2)));
            var folder = Open(probe)!.Rect;
            Assert.Equal(Stratum.Popup, probe.StratumAt(Middle(folder)));
            Assert.Equal(Stratum.Bar, probe.StratumAt(face.Center));
            var away = Away(shown, folder);
            Assert.Equal(Stratum.Dim, probe.StratumAt(away));

            // Its face again: closed, the backdrops gone.
            probe.Click(face.Center);
            Assert.Null(Open(probe));
            Assert.All(Settled(probe, standing: false), spot => Assert.Equal(faded ? "faded" : "at once", Part(spot, 2)));

            // Opened again, the backdrop on the other display clicked: the press the backdrop's, the folder closed.
            probe.Click(face.Center);
            Assert.NotNull(Open(probe));
            _ = Settled(probe, standing: true);
            Assert.Equal(Stratum.Dim, probe.StratumAt(away));
            probe.Click(away);
            Assert.Null(Open(probe));
            _ = Settled(probe, standing: false);
            Assert.Null(probe.StratumAt(away));

            // Opened again, then edit mode: the folder stays, arranged; edit mode's dim, no backdrop.
            probe.Click(face.Center);
            Assert.NotNull(Open(probe));
            _ = Settled(probe, standing: true);
            probe.Open();
            probe.Pump(800);
            Assert.NotNull(Open(probe));
            Assert.All(Settled(probe, standing: false), spot => Assert.Equal(0, spot.Index));
            Assert.All(probe.Read("stage-edit.json").Where(spot => spot.Hit == "Dim"), spot => Assert.Equal(1, spot.Index));
        }, config => Dress(config, animations));

    /// <summary>The folder on the bar the stage opens.</summary>
    private const string Folder = "backdrop-folder";

    /// <summary>The stage's config: a folder of three lines that only read in the bar's middle, a backdrop of half black in both modes.</summary>
    private static void Dress(Config config, bool animations)
    {
        var island = Readings(Folder);
        island.Section = BarSection.Middle;
        config.DefaultLayout.Islands.Add(island);
        Sections.Order(config.DefaultLayout.Islands);
        Assert.True(LookShorthand.Both(Looks.Section(config, LookPart.Flyouts), Tone.Backdrop, new Karpo.Core.Paint(new Argb(0x80, 0, 0, 0))));
        if (!animations)
        {
            config.Animation = Animation.Off;
        }
    }

    /// <summary>Whether Windows' own animation effects are on (Settings › Accessibility › Visual effects), which Karpo's motion follows.</summary>
    private static bool WindowsAnimates()
    {
        BOOL on = BOOL.TRUE;
        _ = SystemParametersInfoW(SPI.SPI_GETCLIENTAREAANIMATION, 0, &on, 0);
        return on != BOOL.FALSE;
    }

    /// <summary>The folder open (stage-edit.json's "FolderOpen"), where its panel stands; null for none.</summary>
    private static Spot? Open(PanelPopupPlaceTests.Probe probe) => probe.Read("stage-edit.json").FirstOrDefault(spot => spot.Hit == "FolderOpen");

    /// <summary>Each display's backdrop layer made (stage-edit.json's "Backdrop").</summary>
    private static List<Spot> Backdrops(PanelPopupPlaceTests.Probe probe) => [.. probe.Read("stage-edit.json").Where(spot => spot.Hit == "Backdrop")];

    /// <summary>
    /// The backdrops once they have come to rest, two seconds at most: <paramref name="standing"/>, each laid and its window in
    /// sight (100, "shown"); else none standing — each going (0) and its window out of sight once its fade has run.
    /// </summary>
    private static List<Spot> Settled(PanelPopupPlaceTests.Probe probe, bool standing)
    {
        var until = Environment.TickCount64 + 2000;
        while (true)
        {
            var spots = Backdrops(probe);
            var rest = standing
                ? spots.Count > 0 && spots.All(spot => spot.Index == 100 && Part(spot, 3) == "shown")
                : spots.All(spot => spot.Index == 0 && Part(spot, 3) == "hidden");
            if (rest || Environment.TickCount64 > until)
            {
                Assert.True(rest, $"the backdrops {(standing ? "never stood" : "stayed")}: {string.Join("; ", spots)}");
                return spots;
            }
            probe.Pump(50);
        }
    }

    /// <summary>A point on a backdrop clear of the folder and of the bars: the middle of the display the folder is not on.</summary>
    private static POINT Away(List<Spot> backdrops, RECT folder) =>
        Middle(backdrops.First(spot => !(Middle(folder).x >= spot.Rect.left && Middle(folder).x < spot.Rect.right)).Rect);

    private static POINT Middle(RECT rect) => new() { x = (rect.left + rect.right) / 2, y = (rect.top + rect.bottom) / 2 };

    /// <summary>The <paramref name="at"/>th of a backdrop's words: its display, its colour, how it faded, whether it is in sight, its window.</summary>
    private static string Part(Spot spot, int at) => spot.Key.Split('|')[at];

    private static string Text(RECT rect) => $"{rect.left},{rect.top},{rect.right},{rect.bottom}";
}
