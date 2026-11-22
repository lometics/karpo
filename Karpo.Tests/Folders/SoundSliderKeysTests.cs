// keys: the sound lines' sliders are stops the keys step (§471; the person approved, 2026-11-01): the volume line's
// slider and every app's in the mixer, walked onto by the folder's arrows and stepped as the brightness line's is —
// Right a level up, Left down, Page Up ten up, End and Home to its ends (CellIsland.CardStep). Up and Down still walk.
//
// Nothing here reaches this PC's audio: a made-up machine stands behind the lines (SoundParts.Made), checked before
// any key, and the volume's tick is silent (Feedback.Play). Needs Direct2D and a desktop: KARPO_UI_TESTS=1. A picture
// of the ring round the volume's slider goes to KARPO_PNG_DIR as keys-sound-slider-dark-100.png.

using Karpo.Core;
using Karpo.Dock;
using Karpo.Dock.QuickSettings.Controls;
using Karpo.Dock.QuickSettings.Services;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Folders;

public sealed class SoundSliderKeysTests
{
    /// <summary>The volume line's slider and the mixer's first app's, stepped by the folder's keys on a made-up machine.</summary>
    [UiFact]
    public void TheSoundSlidersStepByTheKeys()
    {
        var machine = new Machine();
        var volumes = new List<int>();
        var apps = new List<int>();
        WindowShots.OnHiddenDesktop("keyboard-sound-slider", (gfx, instance) =>
        {
            Feedback.Forget();
            Feedback.Play = _ => { };
            SoundParts.Made = () => machine;
            FolderIsland folder;
            try
            {
                folder = ListFormPictures.Folder(gfx, ListFormPictures.Line(ListKind.SoundVolume), ListFormPictures.Line(ListKind.SoundMixer));
            }
            finally
            {
                SoundParts.Made = null;
            }
            using var flyout = new FolderFlyout(gfx, instance, 0);
            try
            {
                flyout.Show(gfx, ListFormPictures.LookFor(true) with { Animations = false }, 1f, new POINT { x = 1700, y = 1500 }, new RECT { right = 2400, bottom = 1500 }, Edge.Bottom, folder, FlyoutAlign.End);
                var volume = (SoundPartTile)folder.Tiles[0].Island;
                var mixer = (SoundPartTile)folder.Tiles[1].Island;
                // The lines read the stand-in, not this PC: its level and its apps.
                Assert.Equal(50, (int)MathF.Round(volume.State.Volume * 100));
                Assert.Equal(["notepad", "paint"], mixer.State.Apps.Select(app => app.Key));

                flyout.TakeKeys();
                Assert.Equal(0, flyout.KeyedOn.Line);
                var slider = SliderTarget(flyout, volume, 0, 0);
                // The ring round the slider the keys came onto (the line draws a step again when the dock wakes it).
                WindowShots.Settle(300);
                Assert.True(WindowShots.Save(flyout.Hwnd, "keys-sound-slider-dark-100.png"), "the folder came back blank");
                Press(flyout, VK.VK_RIGHT, slider, 0, () => (int)MathF.Round(machine.Level * 100), volumes);
                foreach (var key in new[] { VK.VK_LEFT, VK.VK_LEFT, VK.VK_PRIOR, VK.VK_END, VK.VK_HOME })
                {
                    Press(flyout, key, slider, 0, () => (int)MathF.Round(machine.Level * 100), volumes);
                }

                // Down walks on to the mixer, and onto its first app's slider.
                _ = flyout.Message(WM.WM_KEYDOWN, (WPARAM)(nuint)VK.VK_DOWN, 0);
                Assert.Equal(1, flyout.KeyedOn.Line);
                var app = SliderTarget(flyout, mixer, 1, 0);
                Press(flyout, VK.VK_RIGHT, app, 1, () => (int)MathF.Round(machine.Apps["notepad"] * 100), apps);
                Press(flyout, VK.VK_PRIOR, app, 1, () => (int)MathF.Round(machine.Apps["notepad"] * 100), apps);
                Assert.Equal(30, (int)MathF.Round(machine.Apps["paint"] * 100));
                Assert.False(machine.Muted, "a key on the slider must never mute");
            }
            finally
            {
                flyout.Hide();
                folder.Dispose();
                Feedback.Play = null;
            }
        });
        Assert.Equal([51, 50, 49, 59, 100, 0], volumes);
        Assert.Equal([51, 61], apps);
    }

    /// <summary>
    /// The keys walked right along line <paramref name="index"/> onto its <paramref name="nth"/> slider; its target.
    /// Right walks from a stop that is no slider and steps one that is, so the walk ends on the first slider it meets.
    /// </summary>
    private static int SliderTarget(FolderFlyout flyout, SoundPartTile line, int index, int nth)
    {
        var sliders = line.LinePlaced.Select((control, at) => (control, at)).Where(each => each.control is Slider).Select(each => each.at + 1).ToList();
        Assert.True(sliders.Count > nth, "the line placed no slider");
        var target = sliders[nth];
        Assert.Contains(target, flyout.StopsOfLine(index).Select(stop => stop.Target));
        for (var press = 0; press < 8 && flyout.KeyedOn != (index, target); press++)
        {
            _ = flyout.Message(WM.WM_KEYDOWN, (WPARAM)(nuint)VK.VK_RIGHT, 0);
        }
        Assert.Equal((index, target), flyout.KeyedOn);
        return target;
    }

    /// <summary><paramref name="key"/> on the slider: the keys stay on it, and what it set is read back.</summary>
    private static void Press(FolderFlyout flyout, int key, int slider, int index, Func<int> level, List<int> levels)
    {
        _ = flyout.Message(WM.WM_KEYDOWN, (WPARAM)(nuint)key, 0);
        Assert.Equal((index, slider), flyout.KeyedOn);
        levels.Add(level());
    }

    /// <summary>A made-up PC: one output at half, two apps, nothing of this PC's.</summary>
    private sealed class Machine : ISoundSource
    {
        private Changes? news;

        public float Level { get; private set; } = 0.5f;

        public bool Muted { get; private set; }

        public Dictionary<string, float> Apps { get; } = new() { ["notepad"] = 0.5f, ["paint"] = 0.3f };

        public void Start(Changes changes) => news = changes;

        public SoundState Read(int changes) => new(
            true,
            Level,
            Muted,
            [new AudioOutput("speakers", "Speakers", WindowsSound.GlyphFor(1), true)], // not-ui: a stand-in device's name
            [.. Apps.Select(app => new AudioApp(app.Key, app.Key, app.Value, false, false, null, null))]);

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
            Apps[key] = value;
            news?.Add(SoundNews.SessionChanged);
        }

        public void SetAppMuted(string key, bool muted)
        {
        }

        public void Dispose()
        {
        }
    }
}
