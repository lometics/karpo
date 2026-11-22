// folders: what a short list line leaves out is in the widget's right-click menu (docs\widgets.md §9,
// "A line keeps what it does at every height"; Karpo.Dock\Dock.WidgetMenus.cs).
//
// A line one row tall shows the output device in use, the microphone in use or one app's level
// alone, and what is playing without previous, next or its timeline — which only a line of four
// rows or more shows. So each of those is a row of the widget's menu: every device under its name,
// the one in use ticked, a pick making it Windows' default; every app's mute, ticked while it is
// muted; where the track stands, and a step back or on through it. Here each is read from a real
// line and done through its own controls, a made-up sound machine and a made-up track standing in
// for this PC's.
//
// Needs Direct2D: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Dock.QuickSettings.Services;
using Karpo.Tests.Islands;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Folders;

public sealed class ListMenuCoverageTests
{
    private static CellIsland Line(Gfx gfx, ListKind kind)
    {
        var widget = ListFormPictures.Line(kind);
        var made = Karpo.Dock.Islands.CreateLine(Config.IslandKey(null, widget.Id), widget, kind, gfx, new Config());
        return Assert.IsAssignableFrom<CellIsland>(made);
    }

    private static void Never(string target, string? verb) => Assert.Fail($"a row that acts in Karpo opened {target}");

    /// <summary>
    /// The output devices and the microphones: every one under its name, the one in use first and
    /// ticked, under the line's own heading; a pick makes it Windows' default through the line's own
    /// choice — the output for the output, the microphone for the microphones — and opens nothing.
    /// </summary>
    [UiFact]
    public void EveryDeviceIsInTheMenu()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var machine = new Machine();
            SoundParts.Made = () => machine;
            try
            {
                foreach (var (kind, heading, names) in new[]
                {
                    (ListKind.SoundOutput, "Output device", new[] { "Headphones", "Speakers", "Display" }),
                    (ListKind.SoundInput, "Input device", new[] { "Headset", "Array" }),
                })
                {
                    using var line = Line(gfx, kind);
                    var (items, rows) = TheDock.ActRows(TheDock.ActsFor(kind), line);
                    var chosen = items.SkipWhile(item => item.Text != heading).Skip(1).TakeWhile(item => !item.Separator).ToList();
                    Assert.Equal(names, chosen.Select(item => item.Text));
                    Assert.Equal([true, .. Enumerable.Repeat(false, names.Length - 1)], chosen.Select(item => item.Checked == true));

                    var other = rows.First(row => row.Act == WidgetAct.ChooseDevice && row.On != chosen[0].Text.ToLowerInvariant());
                    TheDock.RunAct(other, line, Never);
                    Assert.Equal(other.On, kind == ListKind.SoundOutput ? machine.Chosen : machine.ChosenInput);
                    Assert.False(TheDock.HandsAway(WidgetAct.ChooseDevice));
                }
            }
            finally
            {
                SoundParts.Made = null;
            }
        });
    }

    /// <summary>
    /// The mixer: every app's mute under its name, the person's own apps first and Windows' own
    /// sounds last, as the line lists them, ticked while muted; a pick mutes or unmutes that app
    /// through the mixer's own call and opens nothing.
    /// </summary>
    [UiFact]
    public void EveryAppsMuteIsInTheMenu()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var machine = new Machine();
            SoundParts.Made = () => machine;
            try
            {
                using var line = Line(gfx, ListKind.SoundMixer);
                var (items, rows) = TheDock.ActRows(TheDock.ActsFor(ListKind.SoundMixer), line);
                var apps = rows.Where(row => row.Act == WidgetAct.MuteApp).ToList();
                Assert.Equal(["music", "call", "system"], apps.Select(row => row.On));
                Assert.Equal(["Mute Music", "Mute Call", "Mute System sounds"], items.Take(3).Select(item => item.Text));
                Assert.Equal([false, true, false], items.Take(3).Select(item => item.Checked == true));

                TheDock.RunAct(apps[0], line, Never);
                Assert.Equal(("music", true), machine.AppMuted);
                TheDock.RunAct(apps[1], line, Never);
                Assert.Equal(("call", false), machine.AppMuted);
            }
            finally
            {
                SoundParts.Made = null;
            }
        });
    }

    /// <summary>
    /// What is playing: where the track stands of how long it is, over a step back and a step on,
    /// which act where the app lets its track be sought; with no timeline neither is offered and
    /// nothing says where the track stands.
    /// </summary>
    [UiFact]
    public void TheTimelineIsInTheMenu()
    {
        Offscreen.OnStaThread(gfx =>
        {
            try
            {
                MediaIsland.Stand = new MediaStand(Position: TimeSpan.FromSeconds(123), Duration: TimeSpan.FromSeconds(272), CanSeek: true);
                using var sought = Line(gfx, ListKind.Media);
                var (items, rows) = TheDock.ActRows(TheDock.ActsFor(ListKind.Media), sought);
                var at = items.FindIndex(item => item.Header);
                Assert.Equal("2:03 of 4:32", items[at].Text);
                Assert.Equal(["Back 10 seconds", "Forward 10 seconds"], items.Skip(at + 1).Take(2).Select(item => item.Text));
                Assert.All(items.Skip(at + 1).Take(2), item => Assert.True(item.Enabled, item.Text));
                // A stand-in has no app to seek: pressed, it asks nothing and opens nothing.
                TheDock.RunAct(rows.Single(row => row.Act == WidgetAct.SeekForward), sought, Never);
                Assert.False(TheDock.HandsAway(WidgetAct.SeekBack));

                MediaIsland.Stand = new MediaStand();
                using var stream = Line(gfx, ListKind.Media);
                var (none, _) = TheDock.ActRows(TheDock.ActsFor(ListKind.Media), stream);
                Assert.DoesNotContain(none, item => item.Header);
                Assert.All(none.Where(item => item.Text.Contains("seconds", StringComparison.Ordinal)), item => Assert.False(item.Enabled, item.Text));
            }
            finally
            {
                MediaIsland.Stand = null;
            }
        });
    }

    /// <summary>A made-up machine: three outputs, two microphones and three apps, one of them muted, recording what it is asked.</summary>
    private sealed class Machine : ISoundSource
    {
        private static readonly string Mic = char.ConvertFromUtf32(0xE720);
        private readonly Dictionary<string, bool> muted = new() { ["call"] = true };

        public string? Chosen { get; private set; }

        public string? ChosenInput { get; private set; }

        public (string Key, bool Muted)? AppMuted { get; private set; }

        public void Start(Changes changes)
        {
        }

        public SoundState Read(int news) => new(
            true,
            0.45f,
            false,
            [
                new("speakers", "Speakers", WindowsSound.GlyphFor(1), false),
                new("headphones", "Headphones", WindowsSound.GlyphFor(3), true),
                new("display", "Display", WindowsSound.GlyphFor(9), false),
            ],
            [
                new("system", "System sounds", 1f, muted.GetValueOrDefault("system"), true, null, null),
                new("music", "Music", 0.8f, muted.GetValueOrDefault("music"), false, null, null),
                new("call", "Call", 0.6f, muted.GetValueOrDefault("call"), false, null, null),
            ],
            [
                new("array", "Array", Mic, false),
                new("headset", "Headset", Mic, true),
            ],
            false);

        public void SetVolume(float value)
        {
        }

        public void SetMuted(bool muted)
        {
        }

        public void SetDefault(string id) => Chosen = id;

        public void SetDefaultInput(string id) => ChosenInput = id;

        public void SetInputMuted(bool muted)
        {
        }

        public void SetAppVolume(string key, float value)
        {
        }

        public void SetAppMuted(string key, bool muted)
        {
            this.muted[key] = muted;
            AppMuted = (key, muted);
        }

        public void Dispose()
        {
        }
    }
}
