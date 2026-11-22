// §335: "Quit Karpo" (§545 until §703 General's last row on the settings panel's Karpo page; §703 on no settings page: the taskbar
// menu's Quit Karpo, §532, where somebody adds it, is this row asked and acted as it was): asked first in Windows' dialog
// ("Closes the dock and gives the taskbar back", the About page's own words), then Karpo's one way out: the dock posts
// its host's marked close (DockHost.QuitBy.Menu), which the host answers as it answers the settings
// app's Quit Karpo and Karpo.exe --quit (Closing.Answer, §283): a line in karpo.log, the exit code the
// guard leaves down, the taskbar given back.

namespace Karpo.Core;

public static partial class KarpoMenu
{
    /// <summary>"Quit Karpo"'s row.</summary>
    public const string QuitRow = "Quit";

    /// <summary>"Quit Karpo": asked first, then <paramref name="services"/>' way out. Nothing of the config changes.</summary>
    public static IReadOnlyList<OptionRow> Quit(Config config, KarpoServices services) =>
    [
        new OptionRow(QuitRow, OptionRowKind.Action, S.SettingsAboutQuitHeader)
        {
            Glyph = OptionGlyphs.Power,
            Why = S.SettingsAboutQuitDescription,
            Ask = new DeleteQuestion(S.SettingsAboutQuitHeader, S.SettingsAboutQuitDescription, S.SettingsAboutQuitButton, S.DockDeleteCancel, 0),
            Set = _ =>
            {
                services.Quit();
                return false;
            },
        },
    ];
}
