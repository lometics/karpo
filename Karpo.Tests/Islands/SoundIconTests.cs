// quick: the bar's sound icon does what Windows 11's does besides its click — the wheel over it steps
// the volume two a notch, held between 0 and 100, and a middle click mutes and unmutes — while the
// Wi-Fi and Bluetooth faces beside it, drawn the same way, take neither. The icon is the sound
// preset's face since 2026-10-10 (docs\widgets.md §90), as the sound button it was did.
//
// Nothing here touches the PC's audio: VolumeWatch.Stand is a make-believe output for this thread,
// and Feedback.Play counts instead of beeping.
//
// Needs Direct2D, so it runs only with KARPO_UI_TESTS=1.

using Karpo.Core;
using Karpo.Dock;
using Karpo.Dock.QuickSettings.Services;

namespace Karpo.Tests.Islands;

public sealed class SoundIconTests
{
    /// <summary>
    /// A notch up is two up and a notch down two down, a turn of several notches as many steps, and
    /// the level stops at the ends however far it is turned. A run of notches that moves the level
    /// ticks once, when the wheel rests (backlog 16); one that cannot move it is still the icon's,
    /// and does not tick.
    /// </summary>
    [UiFact]
    public void TheWheelStepsTheVolumeTwoANotchBetweenTheEnds()
    {
        OnOutput(new SampleVolume { Level = 50 }, (output, sound, ticks) =>
        {
            Assert.True(sound.Wheel(120));
            Assert.Equal(52, output.Level);
            Assert.True(sound.Wheel(-3 * 120));
            Assert.Equal(46, output.Level);
            // A touchpad's small turns are a notch each, never nothing.
            Assert.True(sound.Wheel(30));
            Assert.Equal(48, output.Level);
            // The notches are one run: nothing while it turns, one tick once it rests.
            Assert.Equal(0, ticks());
            Rest();
            Assert.Equal(1, ticks());

            output.Level = 99;
            Assert.True(sound.Wheel(120));
            Assert.Equal(100, output.Level);
            Rest();
            var heard = ticks();
            Assert.True(sound.Wheel(120));
            Assert.Equal(100, output.Level);
            Assert.False(Feedback.Waiting, "a notch that cannot move the level waits for no tick");
            Rest();
            Assert.Equal(heard, ticks());

            output.Level = 3;
            Assert.True(sound.Wheel(-40 * 120));
            Assert.Equal(0, output.Level);
            Assert.False(output.Muted, "the wheel sets the level and leaves the mute alone");
        });
    }

    /// <summary>
    /// The tick for a run of notches plays after the output has the run's last level, and at that
    /// level: what the output says at the moment it plays, and what it was played at, are the same.
    /// </summary>
    [UiFact]
    public void TheWheelsTickPlaysAtTheLevelItLeft()
    {
        var output = new SampleVolume { Level = 30 };
        var heard = new List<(int Played, int Output)>();
        Offscreen.OnStaThread(gfx =>
        {
            VolumeWatch.Stand = output;
            Feedback.Forget();
            Feedback.Play = level => heard.Add((level, output.Level));
            Feedback.Arm = _ => { };
            try
            {
                using var sound = Made(gfx, Presets.SoundId);
                Assert.True(sound.Wheel(3 * 120));
                Assert.True(sound.Wheel(2 * 120));
                Assert.Empty(heard);
                Rest();
            }
            finally
            {
                Feedback.Arm = null;
                Feedback.Play = null;
                VolumeWatch.Stand = null;
            }
        });
        Assert.Equal([(40, 40)], heard);
    }

    /// <summary>A middle click mutes, the next unmutes, and the level stays where it was.</summary>
    [UiFact]
    public void AMiddleClickMutesAndUnmutes()
    {
        OnOutput(new SampleVolume { Level = 37 }, (output, sound, _) =>
        {
            Assert.True(((IMiddleClickIsland)sound).MiddleClick(0));
            Assert.True(output.Muted);
            Assert.True(((IMiddleClickIsland)sound).MiddleClick(0));
            Assert.False(output.Muted);
            Assert.Equal(37, output.Level);
            // The click is still the folder's: it opens the sound preset.
            Assert.Equal(IslandView.Folder, sound.Click(0).View);
        });
    }

    /// <summary>
    /// With no output device there is nothing to step or mute: the wheel is not the icon's, and a
    /// middle click is the click it would have been — the folder, whose lines say there is no device.
    /// </summary>
    [UiFact]
    public void WithNoOutputNothingIsTaken()
    {
        OnOutput(new SampleVolume { Present = false, Level = 50 }, (output, sound, _) =>
        {
            Assert.False(sound.Wheel(120));
            Assert.False(((IMiddleClickIsland)sound).MiddleClick(0));
            Assert.Equal(50, output.Level);
            Assert.False(output.Muted);
        });
    }

    /// <summary>
    /// Only the sound face takes the wheel and the middle click: a folder's entry says its face may
    /// take the wheel, and the Wi-Fi and Bluetooth faces, drawn the same way, and every other folder
    /// leave both to the bar.
    /// </summary>
    [UiFact]
    public void OnlyTheSoundFaceTakesTheWheelAndTheMiddleClick()
    {
        Assert.True(BarKind.FolderList.TakesWheel());
        var output = new SampleVolume { Level = 50 };
        Offscreen.OnStaThread(gfx =>
        {
            VolumeWatch.Stand = output;
            using var sound = Made(gfx, Presets.SoundId);
            Assert.True(sound.Sounds);
            foreach (var other in new[] { Presets.WifiId, Presets.BluetoothId, Presets.ClockId })
            {
                using var folder = Made(gfx, other);
                Assert.False(folder.Sounds);
                Assert.False(folder.Wheel(120));
                Assert.False(((IMiddleClickIsland)folder).MiddleClick(0));
            }
            VolumeWatch.Stand = null;
        });
        Assert.Equal(50, output.Level);
        Assert.False(output.Muted);
    }

    /// <summary>The wheel has rested: the run of notches settles, as the dock's timer would settle it.</summary>
    private static void Rest() => Feedback.Settle(Environment.TickCount64 + Feedback.QuietMs);

    /// <summary>The preset <paramref name="preset"/>'s folder, as the bar makes it.</summary>
    private static FolderIsland Made(Gfx gfx, string preset)
    {
        var holder = Presets.Folder(Presets.Find(preset)!, new PresetMachine(HasBattery: false, FixedDrives: []));
        return Assert.IsType<FolderIsland>(Karpo.Dock.Islands.Create(Config.IslandKey(null, holder.Id), holder, gfx, new Config()));
    }

    /// <summary>
    /// Runs <paramref name="body"/> with the sound face over <paramref name="output"/>, and a count
    /// of the ticks it played. The stand-in is this thread's for as long as the icon may reach it.
    /// </summary>
    private static void OnOutput(SampleVolume output, Action<SampleVolume, FolderIsland, Func<int>> body) =>
        Offscreen.OnStaThread(gfx =>
        {
            var ticks = 0;
            VolumeWatch.Stand = output;
            Feedback.Forget();
            Feedback.Play = _ => ticks++;
            // The test settles a run of notches itself (Rest), on its own clock.
            Feedback.Arm = _ => { };
            try
            {
                using var sound = Made(gfx, Presets.SoundId);
                body(output, sound, () => ticks);
            }
            finally
            {
                Feedback.Arm = null;
                Feedback.Play = null;
                VolumeWatch.Stand = null;
            }
        });
}
