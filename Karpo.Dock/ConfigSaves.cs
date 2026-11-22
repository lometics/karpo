// config.json as the dock writes it: one way for every change the person makes, and nothing lost when the
// file won't take it (the person, 2026-10-31: "i dont want people complaining ... that a settings file get
// overwritten").
//
// Every save of the dock's goes through here (Dock.KeepConfig): an edit applied (Apply), a height dragged
// (KeepHeight), a display seen for the first time, a countdown's length. Config.Save throws, and writes
// nothing, when the file can't be written: another program holds it longer than a save waits, another
// Karpo's save is still under way past its patience, the disk refuses, the file is a newer Karpo's, or the
// config stands in for a file not read yet (Config.Unread). Before, the dock wrote a line to a console it
// does not have and went on: the bars showed the change, and after a restart it was gone. Now:
//
//   * the change is remembered as not saved (Unsaved), whatever is changed after it: the next try saves the
//     dock's config as it is then, which holds it — a reload of someone else's change merges what is not
//     saved yet on top (Config.Reload);
//   * it is tried again on a timer, sooner first and then less often (Delay: 1 s, 2 s, 4 s … a minute), and
//     at once after the file changed and was read again, and as Karpo ends — quit, log-off, shutdown;
//   * it is an error of the settings' at the first failure, on the edit bar (§394), and once it has failed
//     Patience times in a row it is said outside edit mode as well: the bar's own menu leads with it, and a
//     click there tries again (Dock.KeepConfig);
//   * a file from a newer Karpo is never written over (Config.FromNewerKarpo): it is said at once, plainly, and
//     not tried again until the file changes;
//   * a config that stands in for a file not read yet is no failure: it waits for the read, which replaces it
//     with the file as it is (Dock.ReadConfigAgain), and then saves nothing.
//
// The logic is here, apart from the dock, so the tests run it on files of their own (ConfigSavesTests).

using Karpo.Core;

namespace Karpo.Dock;

/// <summary>Whether the dock's changes are in config.json, and when to try again (see the top of the file).</summary>
internal sealed class ConfigSaves
{
    /// <summary>The first try again after a failed save, in ms; each later one waits twice as long, up to <see cref="LongestMs"/>.</summary>
    internal const uint FirstMs = 1000;

    /// <summary>The longest wait between tries.</summary>
    internal const uint LongestMs = 60_000;

    /// <summary>Failures in a row after which the person is told outside edit mode too.</summary>
    internal const int Patience = 3;

    /// <summary>Whether a change of the dock's is not in the file yet.</summary>
    public bool Unsaved { get; private set; }

    /// <summary>How many tries in a row have failed, since the last save that went in.</summary>
    public int Failures { get; private set; }

    /// <summary>Why the last try failed, as Config.Save threw it; null when it did not.</summary>
    public Exception? Error { get; private set; }

    /// <summary>Why the last try failed, in the words Config.Save gave; null when it did not.</summary>
    public string? Problem => Error?.Message;

    /// <summary>The file is a newer Karpo's: nothing is written over it, and nothing tried again until it changes.</summary>
    public bool Newer { get; private set; }

    /// <summary>The change waits for the file to be read at all (<see cref="Config.Unread"/>): not a failure.</summary>
    public bool Waiting { get; private set; }

    /// <summary>
    /// Whether the person is to be told outside edit mode: the change is not saved, and either the file
    /// is a newer Karpo's or <see cref="Patience"/> tries in a row have failed.
    /// </summary>
    public bool Telling => Unsaved && !Waiting && (Newer || Failures >= Patience);

    /// <summary>
    /// How long until the next try, while a change waits and trying again may help; null when nothing
    /// waits, for a newer Karpo's file (until it changes), and while the file is not read yet.
    /// </summary>
    public uint? NextTryMs => Unsaved && !Newer && !Waiting && Failures > 0 ? Delay(Failures) : null;

    /// <summary>The wait after <paramref name="failures"/> failed tries in a row: <see cref="FirstMs"/>, doubled each time, at most <see cref="LongestMs"/>.</summary>
    internal static uint Delay(int failures) =>
        (uint)Math.Min(LongestMs, (ulong)FirstMs << Math.Clamp(failures - 1, 0, 16));

    /// <summary>The first read again of a file that was held (<see cref="ConfigReload.Busy"/>, <see cref="Config.Unread"/>), in ms; then twice as long each time, up to <see cref="ReadLongestMs"/>.</summary>
    internal const uint ReadFirstMs = 500;

    /// <summary>The longest wait between reads of a file that is held.</summary>
    internal const uint ReadLongestMs = 8000;

    /// <summary>
    /// Whether config.json is to be read again soon, after a reload that found <paramref name="result"/>
    /// and left the dock <paramref name="config"/>: when another program held the file, so a change in it
    /// was not read, and while the dock's config stands in for a file it could not read at all — the
    /// person's own layout comes back as soon as it reads, not when the file next changes.
    /// </summary>
    internal static bool ReadAgain(ConfigReload result, Config config) => result == ConfigReload.Busy || config.Unread;

    /// <summary>The wait before the <paramref name="tries"/>th read again: <see cref="ReadFirstMs"/>, doubled each time, at most <see cref="ReadLongestMs"/>.</summary>
    internal static uint ReadDelay(int tries) =>
        (uint)Math.Min(ReadLongestMs, (ulong)ReadFirstMs << Math.Clamp(tries - 1, 0, 16));

    /// <summary>
    /// A change of the person's: <paramref name="config"/>, the dock's config with it, saved now. True
    /// when the file has it; false when it is kept to be tried again (<see cref="Again"/>).
    /// </summary>
    public bool Save(Config config)
    {
        Unsaved = true;
        return Again(config);
    }

    /// <summary>
    /// Tries again to save what is not saved, from <paramref name="config"/>, the dock's config as it is
    /// now (which holds every change not saved yet). True when nothing waits any more.
    /// </summary>
    public bool Again(Config config)
    {
        if (!Unsaved)
        {
            return true;
        }
        Waiting = config.Unread;
        if (Waiting)
        {
            // Not a failure: the read replaces this config with the file, and then there is nothing to save.
            return false;
        }
        try
        {
            config.Save();
        }
        catch (Exception error)
        {
            Failures++;
            Error = error;
            // The dock's own config from a newer Karpo's file, or a newer Karpo's write met in the file.
            Newer = config.FromNewerKarpo || error is NewerConfigException;
            return false;
        }
        Unsaved = false;
        Failures = 0;
        Error = null;
        Newer = false;
        return true;
    }
}
