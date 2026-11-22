// The sound is a preset (docs\widgets.md §90): what a click on the sound button gave — the sound
// flyout — is a folder of the sound panel's regions as lines (SoundPartTile) in one list, as the
// person rearranged it (2026-10-11), and the premade sound actions at its foot, under the sound face: the speaker the button was,
// its level and its mute, painted again in place as they change. Made-up outputs and levels
// throughout (SampleSound, SystemStatus.Compose): nothing reaches this PC's audio.
//
// Pictured beside the flyout it replaces (rendered before it went: quick-sound-*.png):
// sound-preset-<dark|light>.png, at 150 % and at 225 % text.

using Karpo.Dock;
using Karpo.Dock.QuickSettings.Services;
using Karpo.Tests.Islands;
using static Karpo.Tests.Folders.ListFormPictures;
using static Karpo.Tests.Islands.Offscreen;

namespace Karpo.Tests.Folders;

public sealed unsafe class SoundPresetTests
{
    private static readonly PresetMachine Machine = new(HasBattery: false, FixedDrives: []);

    /// <summary>The sound preset's folder, as the add list and a new bar make it.</summary>
    private static BarItem Holder() => Presets.Folder(Presets.Find(Presets.SoundId)!, Machine);

    /// <summary>
    /// The flyout's regions in one list, as the person rearranged it on their main bar (2026-10-11,
    /// "i made it a one column design"): the output over the microphone, a divider, the mixer, the
    /// volume and the premade sound actions (not a copy), at the heights they gave. It wears the
    /// sound face.
    /// </summary>
    [Fact]
    public void ThePresetIsTheSoundFlyoutInItsOrder()
    {
        var preset = Presets.Find(Presets.SoundId)!;
        Assert.Equal(FolderFaces.SoundId, preset.Face!.Kind);
        Assert.Equal(["soundOutput", "soundInput", "divider", "soundMixer", "soundVolume", "actions"], preset.Items.Select(item => item.Form));
        Assert.Equal(PresetItem.Of(PremadeWidgets.SoundActions).Premade, preset.Items[^1].Premade);

        var folder = Holder().Folder!;
        Assert.Same(FolderFaces.Sound, folder.Face.Info);
        Assert.Empty(folder.Face.Info.Options);
        Assert.Equal(
            [["soundOutput", "soundInput", "divider", "soundMixer", "soundVolume", "actions"]],
            folder.Lists.Select(list => list.Select(cell => cell.Island.Kind).ToArray()).ToArray());
        Assert.Equal(
            [LineHeight.Automatic(24), LineHeight.Automatic(24), LineHeight.Automatic(24), LineHeight.Exactly(2)],
            folder.Cells.Where(cell => cell.Island.Kind.StartsWith("sound", StringComparison.Ordinal)).Select(cell => LineHeight.For(cell.Island)));
        var footer = folder.Cells[^1].Island;
        Assert.Equal(ListKind.Actions, footer.List);
        Assert.Equal(("sound", "volumeMixer"), (footer.Actions!.Left, footer.Actions.Right));
    }

    /// <summary>The folder opened: the four regions, each the line of its own part, and the footer.</summary>
    [UiFact]
    public void TheFolderHoldsTheFourRegionsAndTheFooter() => OnStaThread(gfx =>
    {
        using var folder = Open(gfx, new SampleSound());
        var parts = folder.Tiles.Select(tile => tile.Island).OfType<SoundPartTile>().Select(tile => tile.Region);
        Assert.Equal([SoundPart.Output, SoundPart.Input, SoundPart.Mixer, SoundPart.Volume], parts);
        folder.Closed();
    });

    /// <summary>
    /// The face is the volume icon's glyph for the level — a wave for each third, none at nothing,
    /// crossed out while muted or where there is no output — and its hover the icon's: the output and
    /// its level. Before anything is read it is the speaker, and the hover the folder's own words.
    /// </summary>
    [Fact]
    public void TheSoundFaceShowsTheLevelAndTheMute()
    {
        Assert.Equal((FolderFaces.Sound.Glyph, (string?)null, false, (string?)null), StatusFaceDrawing.Glyphs(FolderFaces.SoundId, null));
        Assert.Null(StatusFaceDrawing.Tip(FolderFaces.SoundId, null));
        string[] levels = ["", "", "", ""];
        foreach (var (percent, glyph) in new[] { (0, levels[0]), (20, levels[1]), (50, levels[2]), (90, levels[3]) })
        {
            var state = Level(percent);
            Assert.Equal((glyph, (string?)null, false, (string?)null), StatusFaceDrawing.Glyphs(FolderFaces.SoundId, state));
            Assert.Equal(S.DockStatusVolume("Speakers", percent), StatusFaceDrawing.Tip(FolderFaces.SoundId, state));
        }
        var muted = Level(40, muted: true);
        Assert.Equal("", StatusFaceDrawing.Glyphs(FolderFaces.SoundId, muted).Glyph);
        Assert.Equal(S.DockStatusMuted("Speakers"), StatusFaceDrawing.Tip(FolderFaces.SoundId, muted));
        var none = SystemStatus.Compose(default, new SystemStatus.Audio(false, null, 0, false));
        Assert.Equal("", StatusFaceDrawing.Glyphs(FolderFaces.SoundId, none).Glyph);
        Assert.Equal(S.DockStatusNoAudio, StatusFaceDrawing.Tip(FolderFaces.SoundId, none));
    }

    /// <summary>
    /// A level turned or a mute is painted again in place, never a rebuild (the face is one glyph's
    /// square whatever it shows), and the same reading again paints nothing.
    /// </summary>
    [UiFact]
    public void TheSoundFacePaintsAgainInPlaceWhenTheLevelTurns() => OnStaThread(gfx =>
    {
        var holder = Holder();
        using var island = new FolderIsland(Config.IslandKey(null, holder.Id), holder, gfx, new Config());
        Assert.True(island.ShowsStatus);
        var config = new Config { Height = 48 };
        var style = new IslandStyle(1f, false, Layout.CellPx(96, config), Layout.IconPx(96, config), LookFor(dark: true));
        Assert.False(island.SetStatus(Level(20), gfx));
        var size = island.CellSize(0, style);
        _ = island.Measure(style);
        _ = island.Surface(0, style);
        var painted = 0;
        PaintWatch.Watching = (_, _, color) =>
        {
            painted++;
            return color;
        };
        try
        {
            Assert.False(island.SetStatus(Level(20), gfx));
            Assert.Equal(0, painted);
            Assert.False(island.SetStatus(Level(90), gfx));
            Assert.True(painted > 0, "a new level was not painted");
            Assert.Contains("", island.FaceShown, StringComparison.Ordinal);
            painted = 0;
            Assert.False(island.SetStatus(Level(90, muted: true), gfx));
            Assert.True(painted > 0, "the mute was not painted");
            Assert.Contains("", island.FaceShown, StringComparison.Ordinal);
            Assert.Equal(size, island.CellSize(0, style));
            Assert.Equal(S.DockStatusMuted("Speakers"), island.Tooltip(0));
        }
        finally
        {
            PaintWatch.Watching = null;
        }
    });

    // --- Pictures --------------------------------------------------------------------------------------

    /// <summary>
    /// The sound preset as it opens, with one output and no microphone, muted with many apps playing,
    /// and at 225 % text — at 150 %, to stand beside quick-sound-*.png.
    /// </summary>
    [UiFact]
    public void SoundPresetPictures()
    {
        foreach (var dark in new[] { true, false })
        {
            OnStaThread(gfx =>
            {
                var folders = new List<FolderIsland>();
                try
                {
                    var panels = new List<(string, Look, FolderIsland, FolderPlan)>();
                    void Add(string caption, SampleSound sound, float text = 1f)
                    {
                        var look = LookFor(dark, text);
                        var folder = Open(gfx, sound);
                        folders.Add(folder);
                        panels.Add((caption, look, folder, Plan(folder, look, 1.5f).Plan));
                    }

                    Add("As it opens: 100 % text", new SampleSound());
                    Add("One output: no microphone", new SampleSound { OneOutput = true, Microphones = false });
                    var muted = new SampleSound { Apps = 7 };
                    muted.SetMuted(true);
                    Add("Muted: seven apps playing", muted);
                    Add("As it opens: 225 % text", new SampleSound(), 2.25f);
                    Sheet(gfx, $"sound-preset-{(dark ? "dark" : "light")}.png", 1.5f, panels);
                }
                finally
                {
                    foreach (var folder in folders)
                    {
                        folder.Closed();
                        folder.Dispose();
                    }
                }
            });
        }
    }

    /// <summary>The volume at <paramref name="percent"/> on made-up speakers, as SystemStatus reads it.</summary>
    private static SystemState Level(int percent, bool muted = false) =>
        SystemStatus.Compose(default, new SystemStatus.Audio(true, "Speakers", percent, muted));

    /// <summary>The preset's folder opened over <paramref name="sound"/>: its regions read the made-up machine and nothing of this PC's.</summary>
    private static FolderIsland Open(Gfx gfx, SampleSound sound)
    {
        var holder = Holder();
        SoundParts.Made = () => sound;
        try
        {
            var folder = new FolderIsland($"{Owner}/{holder.Id}", holder, gfx, new Config());
            folder.Opened(gfx);
            return folder;
        }
        finally
        {
            SoundParts.Made = null;
        }
    }
}
