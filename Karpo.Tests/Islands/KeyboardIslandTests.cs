// The input indicator (Karpo.Dock\Islands\KeyboardIsland.cs): which layout it
// shows after the dock hears of a change, how a layout reads on the
// bar ("ENG" over "CH"), and that placed — the keyboard preset's face since
// 2026-10-11 — it always shows. Its layouts are the keyboard's line (KeyboardPresetTests).

using Karpo.Dock;
using TerraFX.Interop.Windows;
using static Karpo.Tests.Islands.Offscreen;

namespace Karpo.Tests.Islands;

public sealed class KeyboardIslandTests
{
    private static readonly HKL English = Layout(0x04090409);
    private static readonly HKL EnglishSwiss = Layout(0x08070409);
    private static readonly HKL SwissGerman = Layout(0x08070807);

    /// <summary>An HKL as a 64-bit process gets it: the 32-bit value sign-extended.</summary>
    private static HKL Layout(uint value) => (HKL)unchecked((nint)(int)value);

    [Fact]
    public void AnotherAppsWindowShowsItsOwnLayout()
    {
        Assert.Equal(EnglishSwiss, KeyboardIsland.Pick(English, EnglishSwiss, ours: false, own: null));
        // What Karpo's own thread switched to is not what that window types with.
        Assert.Equal(EnglishSwiss, KeyboardIsland.Pick(English, EnglishSwiss, ours: false, own: SwissGerman));
    }

    [Fact]
    public void AWindowOfKarposKeepsTheLayoutShown()
    {
        // A flyout took the keys: Karpo's thread has a layout of its own, not the one in use.
        Assert.Equal(EnglishSwiss, KeyboardIsland.Pick(EnglishSwiss, English, ours: true, own: null));
    }

    [Fact]
    public void KarposThreadSwitchingWhileItHasTheKeysIsTheLayoutInUse()
    {
        Assert.Equal(SwissGerman, KeyboardIsland.Pick(EnglishSwiss, English, ours: true, own: SwissGerman));
        Assert.Equal(EnglishSwiss, KeyboardIsland.Pick(EnglishSwiss, English, ours: true, own: HKL.NULL));
    }

    [Fact]
    public void AtTheStartAnythingBeatsNothing()
    {
        Assert.Equal(English, KeyboardIsland.Pick(HKL.NULL, English, ours: true, own: null));
        Assert.Equal(HKL.NULL, KeyboardIsland.Pick(HKL.NULL, HKL.NULL, ours: true, own: null));
    }

    [Fact]
    public void ALayoutThatCantBeReadChangesNothing()
    {
        // The window's thread ended between the two calls.
        Assert.Equal(EnglishSwiss, KeyboardIsland.Pick(EnglishSwiss, HKL.NULL, ours: false, own: null));
    }

    [Theory]
    [InlineData(0x04090409u, "ENG", "US")]
    // English with the Swiss German keyboard: the language, and the keyboard's country.
    [InlineData(0x08070409u, "ENG", "CH")]
    [InlineData(0x08070807u, "DEU", "CH")]
    // A variant's high word is a handle (US-International): the language's own country.
    [InlineData(0xF0010409u, "ENG", "US")]
    [InlineData(0x00000409u, "ENG", "US")]
    public void TheLabelIsTheLanguageAndTheKeyboardsCountry(uint layout, string language, string region)
    {
        Assert.Equal((language, region), KeyboardIsland.Label(Layout(layout)));
    }

    /// <summary>
    /// Placed, the indicator always has its cell and its room on the bar, with one keyboard layout
    /// installed as with several: "keyboard layout widget does not show when i placed it on the
    /// taskbar" (2026-10-08). It is the keyboard preset's face now (2026-10-11), and it still counts
    /// no layouts, so whichever layout in use it reads — and the label it is shown, ENG alone or
    /// DEU CH — the folder wearing it has its cell, as wide each time.
    /// </summary>
    [UiFact]
    public void APlacedIndicatorAlwaysShows()
    {
        OnStaThread(gfx =>
        {
            var config = new Config { Height = 48 };
            var item = Presets.Folder(Presets.Find(Presets.KeyboardId)!, new PresetMachine(HasBattery: false, FixedDrives: []));
            var key = Config.IslandKey(null, item.Id);
            using var island = new FolderIsland(key, item, gfx, config);
            var area = new RECT { left = 0, top = 0, right = 1600, bottom = 960 };
            var specs = new List<PartSpec> { PartSpec.For(key, item, new InputHardware(Pen: false, Touch: false, Slate: false)) };
            var widths = new List<float>();
            foreach (var shown in new[] { ("ENG", "US"), ("ENG", ""), ("DEU", "CH") })
            {
                island.Keyboard!.Show(shown);
                _ = island.Refresh(gfx);
                Assert.Equal(1, island.CellCount);
                var content = new Content { Islands = new IslandCollection { [key] = island } };
                var layout = new Layout(gfx, 96, area, area, Edge.Bottom, config, Karpo.Tests.Edit.AddListPictures.LookFor(true), specs, content);
                var part = layout.Parts.Single(part => part.Kind == BarKind.FolderList);
                Assert.True(part.Width > 0f, $"the indicator showing {shown} takes no room");
                widths.Add(part.Width);
            }
            Assert.Single(widths.Distinct());
        });
    }
}
