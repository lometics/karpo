// The two widget families the quick settings brought to the bar itself, measured
// without a window: the volume slider's plan (which way it runs, how long it is,
// where a level sits and what a point on it means) and the one-button widgets
// (what their tooltip and their line say, and that a state Windows tells no app
// is left out rather than made up).

using System.Numerics;
using Karpo.Core;
using Karpo.Dock;
using Karpo.Dock.QuickSettings;
using Karpo.Dock.QuickSettings.Services;
using System.Linq;
using TerraFX.Interop.DirectX;
using Color = Windows.UI.Color;

namespace Karpo.Tests.QuickSettings;

public sealed class VolumePlanTests
{
    // The taskbar's own bar at 100 %: a 48 DIP cell, so the numbers below are DIPs too.
    private const float Thick = 48;

    private static Look Plain => new(
        Glass: false,
        Dark: true,
        Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4));

    private static IslandStyle Style(bool vertical, float thickness = Thick, float scale = 1) =>
        new(scale, vertical, thickness * scale, 24 * scale, Plain);

    /// <summary>The numbers of the card the plans are made for: the edge, the speaker's size and the gap are shares of it.</summary>
    private static Fit Card() => new(Style(false));

    private static D2D_RECT_F Cell(in VolumePlan plan) => plan.BarVertical
        ? new D2D_RECT_F(0, 0, plan.Across, plan.Along)
        : new D2D_RECT_F(0, 0, plan.Along, plan.Across);

    private static VolumePlan Plan(VolumeOptions options, bool vertical, float thickness = Thick, float scale = 1)
    {
        var style = Style(vertical, thickness, scale);
        return VolumeIsland.Plan(options, style, style.Thickness);
    }

    [Fact]
    public void ItRunsAlongTheBarUnlessItIsTold()
    {
        // On a bar at the bottom: lying down, the speaker first, then the slider it was given.
        var flat = Plan(new VolumeOptions(), vertical: false);
        Assert.False(flat.Upright);
        Assert.True(flat.AlongBar);
        Assert.Equal(120, flat.Length);
        // The edge at each end, the speaker, the gap, then the slider.
        Assert.Equal(Card().Edge + Card().Glyph() + Card().Gap + 120 + Card().Edge, flat.Along);

        // On a bar at a side: standing up, and just as long.
        var side = Plan(new VolumeOptions(), vertical: true);
        Assert.True(side.Upright);
        Assert.True(side.AlongBar);
        Assert.Equal(flat.Along, side.Along);
    }

    [Fact]
    public void AcrossTheBarItIsOnlyAsLongAsTheBarIsThick()
    {
        // Standing up on a bar at the bottom: the slider crosses the bar, so 120 can't fit.
        var upright = Plan(new VolumeOptions { Orientation = SliderOrientation.Vertical }, vertical: false);
        Assert.True(upright.Upright);
        Assert.False(upright.AlongBar);
        Assert.Equal(Thick - (2 * Card().Side), upright.Length);
        // Along the bar it then takes only the speaker and the thumb.
        Assert.Equal(Card().Edge + Card().Glyph() + Card().Gap + upright.Thumb + Card().Edge, upright.Along);

        // Lying down on a bar at a side: the same, the other way round.
        var flat = Plan(new VolumeOptions { Orientation = SliderOrientation.Horizontal }, vertical: true);
        Assert.False(flat.Upright);
        Assert.False(flat.AlongBar);
        Assert.Equal(Thick - (2 * Card().Side), flat.Length);
        Assert.Equal(upright.Along, flat.Along);
    }

    [Fact]
    public void TheLengthIsTheOneThatWasAskedFor()
    {
        foreach (var length in new[] { VolumeOptions.ShortestDip, 120, 200, VolumeOptions.LongestDip })
        {
            var plan = Plan(new VolumeOptions { Length = length }, vertical: false);
            Assert.Equal(length, plan.Length);
            Assert.Equal(Card().Edge + Card().Glyph() + Card().Gap + length + Card().Edge, plan.Along);
        }
        // A file with a length outside the range is brought back into it when it is read.
        var island = new BarItem { Kind = BarKind.Volume, Volume = new VolumeOptions { Length = 9000 } };
        island.Normalize();
        Assert.Equal(VolumeOptions.LongestDip, island.Volume!.Length);
        island.Volume.Length = 1;
        island.Normalize();
        Assert.Equal(VolumeOptions.ShortestDip, island.Volume.Length);
    }

    [Fact]
    public void WithoutTheSpeakerOnlyTheSliderIsLeft()
    {
        var plan = Plan(new VolumeOptions { ShowSpeaker = false }, vertical: false);
        Assert.Equal(0, plan.Glyph);
        Assert.Equal(Card().Edge + 120 + Card().Edge, plan.Along);
        var box = Cell(plan);
        var empty = plan.GlyphBox(box);
        Assert.Equal(empty.left, empty.right);
        Assert.False(plan.OnGlyph(box, new Vector2(plan.Along / 2, plan.Across / 2)));
        // The track starts at the cell's edge, where the speaker would have been.
        Assert.Equal(Card().Edge, plan.TrackBox(box).left);
    }

    [Fact]
    public void ThePlaceAndTheLevelAgree()
    {
        foreach (var vertical in new[] { false, true })
        {
            foreach (var orientation in new[] { SliderOrientation.Auto, SliderOrientation.Horizontal, SliderOrientation.Vertical })
            {
                var plan = Plan(new VolumeOptions { Orientation = orientation }, vertical);
                var box = Cell(plan);
                for (var level = 0; level <= 100; level += 5)
                {
                    var at = plan.ThumbCentre(box, level);
                    Assert.Equal(level, plan.LevelAt(box, at));
                }
                // Past either end it stops there: 0 is at the left, or at the bottom when it stands up.
                var quiet = plan.Upright ? new Vector2(0, 2000) : new Vector2(-2000, 0);
                var loud = plan.Upright ? new Vector2(0, -2000) : new Vector2(2000, 0);
                Assert.Equal(0, plan.LevelAt(box, quiet));
                Assert.Equal(100, plan.LevelAt(box, loud));
            }
        }
    }

    [Fact]
    public void ItFillsFromTheLeftAndFromTheBottom()
    {
        var flat = Plan(new VolumeOptions(), vertical: false);
        var flatBox = Cell(flat);
        // Lying down: 0 at the left, 100 at the right.
        Assert.True(flat.ThumbCentre(flatBox, 0).X < flat.ThumbCentre(flatBox, 100).X);
        Assert.Equal(flat.TrackBox(flatBox).left, flat.Filled(flatBox, 50).left);
        Assert.True(flat.Filled(flatBox, 20).right < flat.Filled(flatBox, 80).right);

        var side = Plan(new VolumeOptions(), vertical: true);
        var sideBox = Cell(side);
        // Standing up: 0 at the bottom, as every volume slider Windows draws.
        Assert.True(side.ThumbCentre(sideBox, 0).Y > side.ThumbCentre(sideBox, 100).Y);
        Assert.Equal(side.TrackBox(sideBox).bottom, side.Filled(sideBox, 50).bottom);
        Assert.True(side.Filled(sideBox, 20).top > side.Filled(sideBox, 80).top);
    }

    [Fact]
    public void TheSpeakerAndTheSliderKeepToThemselves()
    {
        foreach (var vertical in new[] { false, true })
        {
            var plan = Plan(new VolumeOptions(), vertical);
            var box = Cell(plan);
            var glyph = plan.GlyphBox(box);
            var speaker = new Vector2((glyph.left + glyph.right) / 2, (glyph.top + glyph.bottom) / 2);
            Assert.True(plan.OnGlyph(box, speaker));
            Assert.False(plan.OnSlider(box, speaker));
            var thumb = plan.ThumbCentre(box, 50);
            Assert.True(plan.OnSlider(box, thumb));
            Assert.False(plan.OnGlyph(box, thumb));
        }
    }

    /// <summary>sizing: the grid rounds a widget up to whole cells, and the slider fills what it got.</summary>
    [Fact]
    public void ItFillsTheCellsItWasGiven()
    {
        var style = Style(vertical: false);
        var wanted = VolumeIsland.Plan(new VolumeOptions(), style, style.Thickness);
        // Content 150 long lands on four whole 48-point cells: the track takes the rest.
        var given = 4 * Thick;
        Assert.True(given > wanted.Along);
        var plan = VolumeIsland.Plan(new VolumeOptions(), style, style.Thickness, given);
        Assert.Equal(given, plan.Along);
        Assert.Equal(given - (2 * Card().Edge) - Card().Glyph() - Card().Gap, plan.Length);
        Assert.True(plan.Length > wanted.Length);
        var box = new D2D_RECT_F(0, 0, given, Thick);
        Assert.Equal(given - Card().Edge, plan.TrackBox(box).right);
        // A cell no larger than the content is left as it is.
        var tight = VolumeIsland.Plan(new VolumeOptions(), style, style.Thickness, wanted.Along);
        Assert.Equal(wanted, tight);
    }

    [Fact]
    public void ASliderAloneInALineStaysInsideIt()
    {
        // The plan a line builds for a slider standing alone (VolumePlan.Sideways, the brightness
        // line's): no speaker of its own — the line draws that beside it — so the track fills the
        // box rather than starting past it.
        var box = new D2D_RECT_F(0, 0, 120, 24);
        var plan = VolumePlan.Sideways(120, 16, 4, 24);
        var track = plan.TrackBox(box);
        Assert.Equal(box.left, track.left);
        Assert.Equal(box.right, track.right);
        foreach (var level in new[] { 0, 50, 100 })
        {
            var at = plan.ThumbCentre(box, level);
            Assert.InRange(at.X, box.left + plan.Thumb / 2, box.right - plan.Thumb / 2);
        }
    }

    [Fact]
    public void EveryBarThicknessKeepsTheThumbInside()
    {
        foreach (var thickness in new float[] { 28, 40, 48, 72, 115, 160 })
        {
            foreach (var scale in new[] { 1f, 1.5f, 2f })
            {
                foreach (var vertical in new[] { false, true })
                {
                    foreach (var orientation in new[] { SliderOrientation.Auto, SliderOrientation.Horizontal, SliderOrientation.Vertical })
                    {
                        var plan = Plan(new VolumeOptions { Orientation = orientation }, vertical, thickness, scale);
                        var box = Cell(plan);
                        var what = $"{orientation} at {thickness} x{scale}{(vertical ? " upright" : "")}";
                        Assert.True(plan.Thumb <= plan.Across, $"thumb {plan.Thumb} over {plan.Across} at {thickness}");
                        if (!plan.Slider)
                        {
                            // The short form: a track with less travel than the thumb's own width
                            // cannot be aimed, so there is none, and the speaker is the widget.
                            // A slider set across a thin bar is where this really happens.
                            Assert.Equal(0, plan.Length);
                            Assert.True(plan.Glyph > 0, $"{what} left a blank cell");
                            continue;
                        }
                        Assert.True(plan.Length >= 2 * plan.Thumb, $"{what} kept a track with no travel");
                        foreach (var level in new[] { 0, 50, 100 })
                        {
                            var at = plan.ThumbCentre(box, level);
                            var half = plan.Thumb / 2;
                            Assert.InRange(at.X, box.left + half - 0.5f, box.right - half + 0.5f);
                            Assert.InRange(at.Y, box.top + half - 0.5f, box.bottom - half + 0.5f);
                        }
                    }
                }
            }
        }
    }
}

/// <summary>
/// quick: the power widget's rows. What a PC offers decides them, and nothing else: no row is shown
/// that would fail, and the order is Windows' own power menu's, with Lock at the end.
/// </summary>
public sealed class PowerLinesTests
{
    private static QuickLinkMachine Machine(bool sleep, bool hibernate) =>
        new(Battery: true, WindowsTerminal: true, sleep, hibernate);

    private static string[] Names(QuickLinkMachine machine) =>
        [.. QuickLinks.PowerLines(machine).Select(line => line.Text)];

    [Fact]
    public void APcThatSleepsAndHibernatesOffersEverything()
    {
        Assert.Equal(
            ["Sleep", "Hibernate", "Shut down", "Restart", "Sign out", "Lock"],
            Names(Machine(sleep: true, hibernate: true)));
    }

    [Fact]
    public void WhatWindowsHidesIsNotOffered()
    {
        // Hibernate off (no hiberfil, or hidden in the power options).
        Assert.Equal(["Sleep", "Shut down", "Restart", "Sign out", "Lock"], Names(Machine(sleep: true, hibernate: false)));
        // Sleep hidden in Windows' own power menu.
        Assert.Equal(["Hibernate", "Shut down", "Restart", "Sign out", "Lock"], Names(Machine(sleep: false, hibernate: true)));
        // A PC that does neither still shuts down, restarts, signs out and locks.
        Assert.Equal(["Shut down", "Restart", "Sign out", "Lock"], Names(Machine(sleep: false, hibernate: false)));
    }

    [Fact]
    public void EveryRowIsACommandTheDockAlreadyRuns()
    {
        foreach (var line in QuickLinks.PowerLines(Machine(sleep: true, hibernate: true)))
        {
            var link = QuickLinks.Of(line.Id);
            Assert.NotNull(link);
            // The dock's power path runs it, and it carries the quick-link menu's own glyph.
            Assert.True(QuickLinks.IsPower(link!.Value), $"{line.Text} isn't a power line");
            var glyph = Assert.IsType<FlyoutIcon.Glyph>(line.Icon);
            Assert.False(string.IsNullOrEmpty(glyph.Code));
            Assert.Null(QuickLinks.Target(link.Value, Machine(sleep: true, hibernate: true)));
        }
        // Lock is new here and is not one of Windows' own Win+X lines.
        Assert.DoesNotContain(QuickLinks.Items(Machine(sleep: true, hibernate: true)),
            item => item.Text == "Lock");
    }

    /// <summary>Whether a folder has a form of <paramref name="kind"/>: a line of its list.</summary>
    internal static bool InAFolder(BarKind kind) => Karpo.Core.Folders.CanAdd(BarKind.FolderList, BarEntries.Of(kind).Info.Id);
}

public sealed class SwitchWidgetTests
{
    /// <summary>The taskbar's one-button widgets: the entries a switch draws.</summary>
    private static BarKind[] Kinds => [.. BarEntries.All.Where(entry => entry.Drawing == WidgetSource.Switch).Select(entry => entry.Kind)];

    /// <summary>
    /// What its switch is called: the name a row of toggles calls it ("Wi-Fi"), which its tooltip
    /// says ("Wi-Fi: on"), or its title where no row holds it.
    /// </summary>
    private static string NameOf(BarKind kind) =>
        Toggles.All.FirstOrDefault(toggle => toggle.Switch == BarEntries.Of(kind).Info.Switch)?.Name ?? kind.Title();

    [Fact]
    public void TheOneButtonKindsAreTheOnesTheUserAskedFor()
    {
        Assert.Equal(
            [
                BarKind.SpatialSound,
                BarKind.WifiSwitch, BarKind.BluetoothSwitch, BarKind.SoundSwitch, BarKind.MicrophoneSwitch,
                BarKind.GameMode, BarKind.ColorFilters, BarKind.MonoAudio,
                BarKind.AirplaneMode, BarKind.EnergySaver, BarKind.NightLight, BarKind.RotationLock,
                BarKind.LiveCaptions, BarKind.Accessibility,
                BarKind.Magnifier, BarKind.Narrator, BarKind.StickyKeys, BarKind.KeepAwake, BarKind.DarkMode,
                BarKind.DoNotDisturb,
            ],
            Kinds);
        foreach (var kind in Kinds)
        {
            Assert.True(kind.IsIsland());
            // Every one acts, and goes into a folder.
            Assert.True(PowerLinesTests.InAFolder(kind));
            Assert.NotEmpty(kind.Title());
            Assert.NotEmpty(BarEntries.Of(kind).Info.Blurb);
            Assert.NotEmpty(kind.Glyph());
            // A new one carries its own options and nothing else's.
            var island = kind.New();
            Assert.NotNull(island.Switch);
            Assert.Null(island.Volume);
        }
    }

    [Fact]
    public void EachKindHasASwitchThatSaysWhatItCanDo()
    {
        foreach (var kind in Kinds)
        {
            using var action = QuickSwitches.For(BarEntries.Of(kind).Info.Switch!.Value);
            Assert.NotEmpty(action.Name);
            Assert.NotNull(action.Note);
            Assert.NotEmpty(action.Note);
        }
    }

    [Fact]
    public void TheOnesWindowsOnlyOpensShowNoState()
    {
        // Live captions and accessibility: Windows tells no app whether they are on, so
        // the widget says nothing rather than making it up. Night light left this list when it
        // became a switch of its own (NightLightTests), and nearby sharing when it did (§115).
        foreach (var kind in new[]
        {
            BarKind.LiveCaptions, BarKind.Accessibility,
        })
        {
            using var action = QuickSwitches.For(BarEntries.Of(kind).Info.Switch!.Value);
            Assert.True(action.Available);
            Assert.Null(action.On);
            Assert.Equal(kind.Title(), action.Name);
        }
    }

    /// <summary>
    /// Every one-button kind names its own switch, and nothing else has one. The default arm here
    /// used to hand an unnamed kind the accessibility shortcut, which decided what the button did.
    /// </summary>
    [Fact]
    public void EveryOneButtonKindNamesItsOwnSwitch()
    {
        foreach (var kind in Kinds)
        {
            using var action = QuickSwitches.For(BarEntries.Of(kind).Info.Switch!.Value);
            Assert.Equal(NameOf(kind), action.Name);
        }
        // A widget that is not a one-button widget has no switch to ask for, in any registry. The
        // mobile hotspot's, nearby sharing's and VPN's are ones still, folders' lines alone since their
        // buttons became presets; cast's and project's, quick settings' lines that open their pages
        // (5c4e84df), press Win+K and Win+P outside a folder.
        var switches = Kinds.Select(kind => BarEntries.Of(kind).Info).Append(WidgetInfos.MobileHotspot).Append(WidgetInfos.NearbyShare).Append(WidgetInfos.Vpn).Append(WidgetInfos.Cast).Append(WidgetInfos.Project).ToHashSet();
        foreach (var info in WidgetInfos.All.Where(info => !switches.Contains(info)))
        {
            Assert.Null(info.Switch);
        }
    }

    [Fact]
    public void TheTooltipSaysWhereItStands()
    {
        Assert.Equal("Airplane mode: on\nTurns the radios off.", SwitchIsland.Say("Airplane mode", true, true, "Turns the radios off."));
        Assert.Equal("Airplane mode: off", SwitchIsland.Say("Airplane mode", true, false, null));
        // Nothing invented where Windows tells no app: the name alone, and the line under it.
        Assert.Equal("Night light\nOpens its settings.", SwitchIsland.Say("Night light", true, null, "Opens its settings."));
        Assert.Equal("Energy saver isn't available on this PC\nThis PC has no battery.",
            SwitchIsland.Say("Energy saver", false, null, "This PC has no battery."));
    }

    /// <summary>
    /// A run that may not reach the person's world gets no switch that would.
    /// <para>
    /// The guarantee is in QuickSwitches.For, not in the things that ask it for a switch, because
    /// a switch starts following what it stands for on its widget's first draw and a drawn widget
    /// is what every offscreen picture and the smoke pass produce. Asking each of those paths to
    /// remember is how this was already wrong: KARPO_QUICK_SAMPLES reaches the flyout's pages and
    /// never reached these, so a smoke pass that says it reads no radio was reading them.
    /// </para>
    /// </summary>
    [Fact]
    public void AnIsolatedRunGetsNoSwitchThatReachesWindows()
    {
        Assert.True(Config.IsIsolated, "The tests are meant to run on a config of their own.");
        foreach (var kind in Kinds)
        {
            using var action = QuickSwitches.For(BarEntries.Of(kind).Info.Switch!.Value);
            Assert.IsType<SampleSwitch>(action);
            // Still its own switch's words, so this cannot quietly hand every kind the same one.
            Assert.Equal(NameOf(kind), action.Name);
            Assert.NotEmpty(action.Note!);
        }
        // Windows' own are still there for the smoke pass, which wants them exercised natively.
        using var real = QuickSwitches.Windows(QuickSwitch.MobileHotspot);
        Assert.IsType<HotspotSwitch>(real);
    }

    /// <summary>
    /// Before the look has come back the hotspot says nothing rather than something wrong. Its
    /// state is null, which the widget draws as no state at all, and its line is the plain one:
    /// not the line about Windows refusing, which is a thing to say once that has been found out.
    /// A button that is briefly blank; never one that is briefly wrong.
    /// </summary>
    [Fact]
    public void TheHotspotSaysNothingBeforeItHasLooked()
    {
        using var hotspot = new HotspotSwitch();
        Assert.Null(hotspot.On);
        Assert.True(hotspot.Available);
        Assert.Equal("Shares this PC's connection over Wi-Fi.", hotspot.Note);
    }

    [Fact]
    public void ALineSaysTheSame()
    {
        Assert.Equal("On", SwitchIsland.State(true));
        Assert.Equal("Off", SwitchIsland.State(false));
        // Where Windows tells no app, nothing is made up: the line's state is left out.
        Assert.Equal("", SwitchIsland.State(null));
        // Where this PC hasn't got it, the line is drawn off and greyed rather than saying so (§247).
    }
}
