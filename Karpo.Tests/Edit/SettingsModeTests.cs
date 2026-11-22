// edit, §694 (the person, 2026-11-12: "the taskbar again does not care about the background value. it just shows as white"):
// the settings edit the Appearance in its own mode — its Mode, else Windows' —, never the mode the bar's ground reads in.

using Karpo.Dock;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Edit;

public sealed class SettingsModeTests
{
    /// <summary>
    /// Midnight's taskbar white in dark mode: the bar reads light (its text dark on it), yet the page edits the dark mode's
    /// values — those the bar wears — so a colour picked there changes the bar. A Mode set is the Mode set.
    /// </summary>
    [Fact]
    public void AWhiteBarInDarkModeIsStillEditedInDarkMode()
    {
        var config = new Config();
        Assert.True(LookPalettes.Wear(config, LookPalettes.Of(LookPalettes.MidnightId)!));
        Assert.True(Looks.WritePaint(Looks.Section(config, LookPart.Taskbar), Tone.Background, dark: true, Karpo.Core.Paint.Parse("#FFFFFF")));
        var windows = new Look(Glass: false, Dark: true, Accent: default);
        // The trap: the bar's ground reads light.
        Assert.False(windows.Wearing(config).Dark);
        Assert.True(TheDock.ModeOf(config.Appearance, windows));
        Assert.True(Looks.WriteMode(config, TaskbarMode.Light));
        Assert.False(TheDock.ModeOf(config.Appearance, windows));
        Assert.True(Looks.WriteMode(config, TaskbarMode.Dark));
        Assert.True(TheDock.ModeOf(config.Appearance, new Look(Glass: false, Dark: false, Accent: default)));
    }
}
