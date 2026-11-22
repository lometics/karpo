// config.json kept as the person left it (the person, 2026-10-31: "i dont want people complaining ... that a
// settings file get overwritten"): the dock's one way to save it, and its reads again of a file another
// program held.
//
// Saving (ConfigSaves.cs): every change the dock makes goes through KeepConfig. One the file does not take is
// remembered and tried again on a timer (CONFIG_SAVE: 1 s, 2 s, 4 s … a minute), after the file changed and
// was read again, and as Karpo ends — quit, and the session ending (WM_QUERYENDSESSION, when Explorer is
// still there and the process may be ended before it hears the answer). It is an error of the settings'
// on the edit bar at once (§394), and once it keeps failing the bar's own menu leads with it, outside edit
// mode, where a click tries again. A file from a newer Karpo is never written over: said at once, plainly.
//
// Reading again: a config.json another program held as Karpo started is defaults for now (Config.Unread),
// and one held as it changed is a change not read (ConfigReload.Busy). Before, either stayed so until the
// file next changed — the person's layout gone for as long as they did not touch the file. Now the file is
// read again on a timer (CONFIG_READ: half a second, then twice as long each time, up to eight) until it
// reads, and the defaults that stood in are never saved: the file as it is takes their place.

using Karpo.Core;

namespace Karpo.Dock;

public sealed unsafe partial class Dock
{
    /// <summary>The host's timer that tries a save that failed again (<see cref="ConfigSaves.NextTryMs"/>).</summary>
    private const uint CONFIG_SAVE = 47;

    /// <summary>The host's timer that reads config.json again while another program holds it (<see cref="ConfigSaves.ReadAgain"/>).</summary>
    private const uint CONFIG_READ = 48;

    /// <summary>The bar's menu's row that says the settings are not saved, and tries again (<see cref="SavesRows"/>).</summary>
    internal const uint CMD_SAVE_AGAIN = 2950;

    /// <summary>Whether the dock's changes are in config.json (ConfigSaves.cs).</summary>
    private readonly ConfigSaves saves = new();

    /// <summary>How many times in a row config.json has been read again while held (<see cref="CONFIG_READ"/>).</summary>
    private int readTries;

    /// <summary>
    /// A change of the person's, in <paramref name="next"/> — the config the dock takes, or has taken —
    /// written to config.json; one the file does not take is kept and tried again (<see cref="ConfigSaves"/>).
    /// </summary>
    private void KeepConfig(Config next)
    {
        saves.Save(next);
        SavesSaid();
    }

    /// <summary>What is not saved yet, tried again from the config as it is now; nothing where all is saved.</summary>
    private void SaveConfigAgain()
    {
        if (!saves.Unsaved)
        {
            return;
        }
        saves.Again(Persons);
        SavesSaid();
    }

    /// <summary>
    /// Karpo is ending (quit, or the session: log-off, restart, shutdown): what is not saved yet gets
    /// its last try, with nothing shown — the windows are going. What still fails is in karpo.log.
    /// </summary>
    private void FlushConfig()
    {
        if (saves.Unsaved && !saves.Again(Persons) && saves.Error is { } error)
        {
            Troubles.Note("config.json not saved as Karpo ends", error); // not-ui: a trouble's name
        }
    }

    /// <summary>
    /// What the last try came to, said: an error of the settings' while a change is not saved — a
    /// newer Karpo's file in its own words —, over once it is; the next try armed where one may help.
    /// </summary>
    private void SavesSaid()
    {
        Kill(CONFIG_SAVE);
        if (!saves.Unsaved)
        {
            SettingsFine(FaultSources.NotSaved);
            // What reading the file at the start had to do is behind it once the file saves well.
            SettingsFine(FaultSources.Recovered);
            return;
        }
        if (saves.Error is { } error)
        {
            Troubles.Note("config.json not saved", error); // not-ui: a trouble's name
            SettingsFault(FaultSources.NotSaved, saves.Newer ? S.DockFaultNewer : S.DockFaultNotSaved(error.Message));
        }
        if (saves.NextTryMs is { } wait)
        {
            Arm(CONFIG_SAVE, wait);
        }
    }

    /// <summary>
    /// After a reload that found <paramref name="result"/>: config.json read again soon while another
    /// program holds it, or while the dock's config stands in for a file not read yet; the reads
    /// counted afresh once it reads.
    /// </summary>
    private void ReadConfigSoon(ConfigReload result)
    {
        if (!ConfigSaves.ReadAgain(result, Persons))
        {
            readTries = 0;
            Kill(CONFIG_READ);
            return;
        }
        Arm(CONFIG_READ, ConfigSaves.ReadDelay(++readTries));
    }

    /// <summary>
    /// The bar's menu's first rows while the person's changes keep not saving (<see cref="ConfigSaves.Telling"/>):
    /// "Settings not saved", why under it — a click tries again —, or that the file is a newer Karpo's,
    /// which no click mends; then a line. None while all is saved.
    /// </summary>
    internal static List<FlyoutItem> SavesRows(ConfigSaves saves)
    {
        if (!saves.Telling)
        {
            return [];
        }
        var why = saves.Newer ? S.DockFaultNewer : S.DockFaultNotSaved(saves.Problem ?? "");
        return
        [
            new FlyoutItem(CMD_SAVE_AGAIN, S.DockNotSavedTitle, Glyph(GlyphWarning), Detail: why, Enabled: !saves.Newer),
            new FlyoutItem(0, "", Separator: true),
        ];
    }
}
