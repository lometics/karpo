// config.json on disk: the one way both apps read and write it.
//
// The dock and the settings app each save the whole file, often, and a person
// may edit it by hand at the same time. So that none of them loses what another
// wrote, and a crash never leaves half a file:
//
// - Every write holds a named mutex for the config folder for the read, merge
//   and write alone: milliseconds, never while anyone waits for input.
// - It reads the file first. When someone else wrote it since this config was
//   loaded or saved, the two versions are merged three ways against the one both
//   started from (ConfigMerge): both sets of changes stay, and only a value both
//   changed differently takes this one's.
// - The text goes to a temporary file in the same folder, through to the disk,
//   and File.Replace swaps it in, keeping the version before as config.json.bak.
//   A sharing violation (a reader without delete sharing, a virus scanner) is
//   retried. Both the read and the write are DurableFile's, as accounts.json's
//   and the caches' are.
// - Loading never loses the config. A config.json that doesn't read (a typo in a
//   hand edit, a file another program cut short) is kept as
//   config.json.broken-<time> and the last good version takes its place; a save
//   cut off between its two renames is finished; temporary files a crash left
//   behind are cleaned up.
// - A config remembers the file as it last saw it (Clone keeps that), so a file
//   watcher tells its own saves from someone else's (Reload), and a save knows
//   what to merge against.
// - A save that can't be made without losing something writes nothing and throws:
//   a file a newer Karpo wrote (FromNewerKarpo, NewerConfigException), defaults
//   standing in for a file another program held at the start (Unread), a lock
//   another process holds past its patience. The caller keeps the change and tries
//   again (the dock: Karpo.Dock\ConfigSaves.cs). A load writes the file back only for
//   ids it had to make up (MadeUpIds), never for what it only dropped.

using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Karpo.Core;

/// <summary>What <see cref="Config.Reload"/> found.</summary>
public enum ConfigReload
{
    /// <summary>The file says what this config last read or wrote: its own save coming back, or another file in the folder changed.</summary>
    Unchanged,

    /// <summary>Someone else changed the file: the fresh config has it, with this config's unsaved edits on top.</summary>
    Changed,

    /// <summary>The file doesn't read, probably a hand edit in progress: keep what you have. The next save keeps the text aside.</summary>
    Broken,

    /// <summary>Another program holds the file: try again later.</summary>
    Busy,
}

/// <summary>
/// What <see cref="Config.Save"/> throws for a file a newer Karpo wrote (<see cref="Config.FromNewerKarpo"/>): this
/// build's save would drop everything it doesn't know of it, so nothing is written, and trying again won't
/// help until Karpo is updated or the file changes.
/// </summary>
public sealed class NewerConfigException(int shape)
    : IOException($"{Config.FilePath} was written by a newer Karpo (shape {shape}, this one knows {Config.Shape}). Update Karpo to change settings.")
{
    /// <summary>The shape the file says.</summary>
    public int FileShape { get; } = shape;
}

public sealed partial class Config
{
    /// <summary>How long a save waits for another process's save to finish before it refuses (FileLock.TimedOut).</summary>
    private const int LockMilliseconds = 5000;

    /// <summary>A temporary file older than this belongs to no save in progress.</summary>
    private static readonly TimeSpan Stale = TimeSpan.FromMinutes(1);

    /// <summary>What <see cref="Load"/> had to do to read the file, for the settings app to say; null when it read as it was.</summary>
    [JsonIgnore]
    public string? Recovery { get; private set; }

    /// <summary>
    /// This config stands in for a file that wasn't read as the person left it: defaults for a
    /// file that was locked, unreadable or deleted while running, or whatever <see cref="Load"/>
    /// had to recover (<see cref="Recovery"/>). Kept by <see cref="Clone"/>; a config read from
    /// the file again (<see cref="Reload"/>) is not one. Comparing such a config with the file is
    /// no edit of anybody's, so nothing is taken from it for one (Windows' pins, WindowsPins.cs).
    /// </summary>
    [JsonIgnore]
    public bool StandsIn { get; private set; }

    /// <summary>
    /// This config stands in for a file that is there but was not read: another program held
    /// config.json all the while <see cref="Load"/> tried (a backup tool, a virus scanner), so these
    /// are defaults, not the person's settings. Such a config is never saved (<see cref="Save"/>
    /// refuses it): whatever it would write is defaults merged into the person's file, and where it
    /// and their file both set a value — the display it stands on, which layout that shows — the
    /// defaults' would win. <see cref="Reload"/> takes the file as it is the moment it reads, without
    /// anything changed here on top; until then the caller reads again now and then (the dock, on a
    /// timer). Kept by <see cref="Clone"/>; a config read from the file is never one.
    /// </summary>
    [JsonIgnore]
    public bool Unread { get; private set; }

    /// <summary>
    /// Set on the config <see cref="Reload"/> hands out when every change between the file the
    /// config before it knew and the file now was a save of Karpo's own (the dock or the settings
    /// app, <see cref="Save"/>) — an edit the person made through Karpo. Not for a hand edit, an
    /// import, a file deleted or put back, a recovery, or a config that stood in (<see cref="StandsIn"/>).
    /// </summary>
    [JsonIgnore]
    public bool KarposOwnEdit { get; private set; }

    // The file as this config last knew it, normalized (read, repaired and written
    // out again): the base of a three-way merge. Null for a config made in code,
    // whose save replaces the file.
    private string? known;

    // config.json's exact text when this config last read or wrote it: while the
    // file still says this, nobody else wrote it. After a save that merged the
    // other app's changes in, this config's own text instead, which the file then
    // doesn't say, so the next save or reload merges again. Null when there was no
    // file (or none that read).
    private string? seen;

    /// <summary>The file config.json becomes when a save replaces it: the version before, which always read.</summary>
    public static string BackupPath => DurableFile.BackupOf(FilePath);

    /// <summary>
    /// Reads config.json. Missing means defaults. A file that doesn't read is kept aside as
    /// config.json.broken-&lt;time&gt; and the last good version is put back (<see cref="Recovery"/>
    /// says so). A file <see cref="Repair"/> had to make ids up for is written back once, so every
    /// process reading it uses the same ids (<see cref="MadeUpIds"/>). A file another program holds
    /// all the while is defaults for now (<see cref="Unread"/>).
    /// </summary>
    public static Config Load()
    {
        using var guard = FileLock.Take();
        return LoadLocked(mayWrite: !guard.TimedOut);
    }

    /// <summary>
    /// Writes this config to config.json, with whatever someone else saved since this config was
    /// loaded or saved merged in (see <see cref="ConfigMerge"/>), and returns the config as saved: this
    /// one, or, when changes of the other app came in, a new one holding both. Keeping this one
    /// instead loses nothing: its next save, or <see cref="Reload"/>, merges in what it lacks. A
    /// config made in code (not loaded, nor cloned from one that was) replaces the file. Throws
    /// <see cref="IOException"/> when the file can't be written, and writes nothing then: for a file of
    /// a newer Karpo's (<see cref="FromNewerKarpo"/>), for a config that stands in for a file it could
    /// not read (<see cref="Unread"/>), and while another process holds the lock past its patience.
    /// The caller keeps the change and tries again (the dock: Karpo.Dock\ConfigSaves.cs).
    /// </summary>
    public Config Save()
    {
        if (FromNewerKarpo)
        {
            // Writing would drop everything this build doesn't know about.
            throw new NewerConfigException(Version);
        }
        if (Unread)
        {
            // Defaults standing in: merged into the person's file they would win wherever both set a value.
            throw new IOException($"{FilePath} hasn't been read yet: another program had it open as Karpo started.");
        }
        var ours = ToJson();
        using var guard = FileLock.Take();
        if (guard.TimedOut)
        {
            // Another process is in the middle of its read, merge and write, and has been for seconds: a
            // write now would be lost under its own (it read the file before this one's), or it under this.
            throw new IOException($"{FilePath} is being saved by another process that hasn't finished.");
        }
        FinishCutOffSave();
        CleanUp();
        if (ReadFile(FilePath, out var disk) == FileRead.Locked)
        {
            throw new IOException($"{FilePath} is in use by another program.");
        }
        // The file there now becomes the backup only if it reads.
        var keepBackup = disk is not null;
        Config? theirs = null;
        if (disk is not null && disk != seen)
        {
            theirs = TryParse(disk);
            if (theirs is null)
            {
                // Someone left a file that doesn't read, such as a hand edit with a mistake.
                KeepBroken();
                keepBackup = false;
                disk = null;
            }
            else if (theirs.FromNewerKarpo)
            {
                // A newer Karpo wrote the file since this config read it: merged through this build's
                // model, whatever this build doesn't know of it would be dropped, as for a save of its own.
                throw new NewerConfigException(theirs.Version);
            }
            else
            {
                theirs.Repair();
            }
        }

        var final = ours;
        var result = this;
        // Whether what someone else wrote is written over rather than merged with: a config made in
        // code, or a merge that failed.
        var over = theirs is not null;
        if (theirs is not null && known is not null)
        {
            try
            {
                var merged = Parse(ConfigMerge.Merge(known, ours, theirs.ToJson()));
                merged.Repair();
                final = merged.ToJson();
                over = false;
                if (final != ours)
                {
                    result = merged;
                }
            }
            catch (Exception error) when (error is JsonException or InvalidOperationException)
            {
                // Not expected: the merge takes whole values from files that read. This one's then.
                Console.Error.WriteLine($"Karpo: config.json not merged: {error.Message}");
            }
        }
        if (disk != final)
        {
            WriteLocked(final, keepBackup);
            // An edit of Karpo's own, from a file that read: from what this config knew of it, or
            // merged into what someone else wrote. Not a config made in code, which replaced it.
            if (disk is not null && !over)
            {
                NoteEdit(disk, final);
            }
        }
        // All of this config is in the file now, whatever came in around it: its own
        // content is the base of its next save or reload, which then merge in what
        // it lacks. (Keeping the base it had would make its own earlier additions look
        // like someone else's, and a later removal of one of them would be undone.)
        known = ours;
        seen = ours;
        if (!ReferenceEquals(result, this))
        {
            result.known = final;
            result.seen = final;
            result.StandsIn = StandsIn;
        }
        return result;
    }

    /// <summary>
    /// For a file watcher: reads config.json again when someone else changed it since this config
    /// read or wrote it. <see cref="ConfigReload.Changed"/> hands out the new config in
    /// <paramref name="fresh"/>, with what was changed here and not saved yet on top; otherwise
    /// <paramref name="fresh"/> is this config. A file Repair had to make ids up for (a hand edit
    /// without them) is written back once (<see cref="MadeUpIds"/>). A config that stood in for a file
    /// it could not read (<see cref="Unread"/>) takes the file as it is, nothing of its own on top.
    /// </summary>
    public ConfigReload Reload(out Config fresh)
    {
        fresh = this;
        // Most notifications are this config's own save coming back, or another
        // file in the folder: one read tells, without waiting for anything.
        var first = ReadFile(FilePath, out var disk, attempts: 1);
        if (first == FileRead.Locked)
        {
            return ConfigReload.Busy;
        }
        if (known is not null && disk == seen && !Unread)
        {
            return ConfigReload.Unchanged;
        }

        using var guard = FileLock.Take();
        FinishCutOffSave();
        if (ReadFile(FilePath, out disk) == FileRead.Locked)
        {
            return ConfigReload.Busy;
        }
        if (known is not null && disk == seen && !Unread)
        {
            return ConfigReload.Unchanged;
        }
        Config theirs;
        var theirsSeen = disk;
        // Whether every change from the file this config knew to this one was a save of Karpo's.
        var karposOwn = !StandsIn && disk is not null && seen is not null && EditedByKarpo(seen, disk);
        if (disk is null)
        {
            // Deleted: what a start without the file gets, standing in for it.
            theirs = Defaults();
        }
        else if (TryParse(disk) is { } parsed)
        {
            theirs = parsed;
            // Written back once when Repair made ids up — never a newer Karpo's file, nor past a lock
            // another process still holds, as Load.
            if (theirs.Repair() && !theirs.FromNewerKarpo && !guard.TimedOut && MadeUpIds(disk, theirs.ToJson()))
            {
                var repaired = theirs.ToJson();
                try
                {
                    WriteLocked(repaired, keepBackup: true);
                    theirsSeen = repaired;
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    Console.Error.WriteLine($"Karpo: {FilePath} not written: {error.Message}");
                }
            }
        }
        else
        {
            return ConfigReload.Broken;
        }

        var theirsJson = theirs.ToJson();
        var mine = ToJson();
        var next = theirs;
        var nextJson = theirsJson;
        // Edits made here and not saved yet stay, on top of the file as it is now — not those of
        // defaults that stood in for the file (Unread): the file is the person's, and what changed of
        // the defaults meanwhile (the display they stood on, put in again by the dock) is not.
        if (known is not null && mine != known && !Unread)
        {
            try
            {
                next = Parse(ConfigMerge.Merge(known, mine, theirsJson));
                next.Repair();
                nextJson = next.ToJson();
            }
            catch (Exception error) when (error is JsonException or InvalidOperationException)
            {
                Console.Error.WriteLine($"Karpo: unsaved changes not merged: {error.Message}");
                next = theirs;
                nextJson = theirsJson;
            }
        }
        if (nextJson == mine)
        {
            known = theirsJson;
            seen = theirsSeen;
            if (Unread)
            {
                // The file read at last, and says what stood in for it: this config is the file's now.
                Unread = false;
                StandsIn = disk is null;
            }
            return ConfigReload.Unchanged;
        }
        next.known = theirsJson;
        next.seen = theirsSeen;
        next.StandsIn = disk is null;
        next.KarposOwnEdit = karposOwn;
        fresh = next;
        return ConfigReload.Changed;
    }

    /// <summary>
    /// Replaces config.json with <paramref name="imported"/>, settings brought from another PC (see
    /// <see cref="ConfigTransfer"/>): nothing merged, the import is meant to take over. The file it
    /// replaces is kept beside it as config.json.before-import-&lt;time&gt; (<paramref name="kept"/>, null
    /// when there was none), and as the backup. Returns the config as written, which knows the file,
    /// so the next save merges as any other. Throws <see cref="IOException"/> when it can't be written.
    /// </summary>
    public static Config Replace(Config imported, out string? kept) => Replace(imported, "before-import", out kept);

    /// <summary>
    /// <see cref="Replace(Config, out string?)"/>, the file it replaces kept as
    /// config.json.<paramref name="keptAs"/>-&lt;time&gt;: "before-import" for an import,
    /// "before-reset" for a reset (<see cref="ConfigReset"/>). Never over a copy kept before: a
    /// second one in the same second is -&lt;time&gt;-2.
    /// </summary>
    internal static Config Replace(Config imported, string keptAs, out string? kept)
    {
        var json = imported.ToJson();
        using var guard = FileLock.Take();
        if (guard.TimedOut)
        {
            // As Save: another process's save still under way would come down on top of this one.
            throw new IOException($"{FilePath} is being saved by another process that hasn't finished.");
        }
        MarkReplaced();
        FinishCutOffSave();
        CleanUp();
        kept = null;
        var keepBackup = false;
        if (ReadFile(FilePath, out var disk) == FileRead.Ok && disk is not null)
        {
            var name = $"{FilePath}.{keptAs}-{DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}";
            kept = name;
            for (var number = 2; File.Exists(kept); number++)
            {
                kept = $"{name}-{number.ToString(CultureInfo.InvariantCulture)}";
            }
            File.Copy(FilePath, kept);
            keepBackup = TryParse(disk) is not null;
        }
        WriteLocked(json, keepBackup);
        var written = Parse(json);
        written.Repair();
        written.known = json;
        written.seen = json;
        return written;
    }

    /// <summary>The mark <see cref="Replace"/> leaves, in the local folder: its time is when the file was last replaced whole.</summary>
    private static string ReplacedMark => Path.Combine(LocalFolder, "config-replaced");

    /// <summary>
    /// When config.json was last replaced whole (<see cref="Replace"/>, an import), as far as either
    /// app has said; <see cref="DateTime.MinValue"/> when never. The dock reads it to tell an
    /// import from an edit: an import's missing pins are another PC's, not pins somebody took off
    /// (Karpo.Dock\Dock.WindowsPins.cs).
    /// </summary>
    public static DateTime ReplacedAt()
    {
        try
        {
            return File.Exists(ReplacedMark) ? File.GetLastWriteTimeUtc(ReplacedMark) : DateTime.MinValue;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return DateTime.MinValue;
        }
    }

    /// <summary>
    /// Whether the config a reload that found <paramref name="result"/> is taking replaced the file
    /// whole (<see cref="Replace"/>) since the mark was last used up, <paramref name="seen"/> being
    /// when that was. Only a reload that takes a config (<see cref="ConfigReload.Changed"/>) uses
    /// the mark up: an import first copies the old file aside in the same folder, and that change,
    /// or one met Busy or Broken, takes nothing, so the mark waits for the reload that does.
    /// </summary>
    public static bool ReplacedFor(ConfigReload result, ref DateTime seen)
    {
        if (result != ConfigReload.Changed)
        {
            return false;
        }
        var replaced = ReplacedAt();
        if (replaced <= seen)
        {
            return false;
        }
        seen = replaced;
        return true;
    }

    /// <summary>Leaves the mark <see cref="ReplacedAt"/> reads; a mark that can't be left is not a reason to refuse the import.</summary>
    private static void MarkReplaced()
    {
        try
        {
            Directory.CreateDirectory(LocalFolder);
            File.WriteAllText(ReplacedMark, DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            File.SetLastWriteTimeUtc(ReplacedMark, DateTime.UtcNow);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Karpo: {ReplacedMark} not written: {error.Message}");
        }
    }

    /// <summary>
    /// The log <see cref="Save"/> keeps, in the local folder, of the edits Karpo's own saves made:
    /// one line each, the file's text before and after as hashes, oldest first, the last
    /// <see cref="EditsKept"/> of them. A hand edit, an import, a file deleted, put back or
    /// recovered writes no line, so a reload can tell an edit made through Karpo from any of those
    /// (<see cref="KarposOwnEdit"/>). A log that is missing or doesn't read only means no edit is
    /// taken for Karpo's, which is the safe side.
    /// </summary>
    private static string EditsLog => Path.Combine(LocalFolder, "config-edits");

    private const int EditsKept = 32;

    /// <summary>Notes, under the lock, that a save of Karpo's took the file from <paramref name="before"/> to <paramref name="after"/>.</summary>
    private static void NoteEdit(string before, string after)
    {
        try
        {
            Directory.CreateDirectory(LocalFolder);
            var lines = File.Exists(EditsLog) ? File.ReadAllLines(EditsLog).ToList() : [];
            lines.Add($"{Hash(before)} {Hash(after)}");
            File.WriteAllLines(EditsLog, lines.Skip(Math.Max(0, lines.Count - EditsKept)));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Karpo: {EditsLog} not written: {error.Message}");
        }
    }

    /// <summary>
    /// Whether saves of Karpo's alone (<see cref="EditsLog"/>) lead from the file's text
    /// <paramref name="from"/> to <paramref name="to"/>, one after another.
    /// </summary>
    private static bool EditedByKarpo(string from, string to)
    {
        List<(string Before, string After)> edits;
        try
        {
            if (!File.Exists(EditsLog))
            {
                return false;
            }
            edits = [.. File.ReadAllLines(EditsLog)
                .Select(line => line.Split(' '))
                .Where(parts => parts.Length == 2)
                .Select(parts => (parts[0], parts[1]))];
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return false;
        }
        var start = Hash(from);
        var end = Hash(to);
        // The file now must be what Karpo's last save left. A text Karpo wrote before and wrote over
        // since (an undo leads back to it) is a hand restore or a recovery of config.json.bak when it
        // comes back, not an edit of Karpo's.
        if (edits.Count == 0 || edits[^1].After != end)
        {
            return false;
        }
        // From each edit that started at the file this config knew, the ones after it in turn.
        for (var first = 0; first < edits.Count; first++)
        {
            if (edits[first].Before != start)
            {
                continue;
            }
            var at = start;
            for (var next = first; next < edits.Count; next++)
            {
                if (edits[next].Before == at)
                {
                    at = edits[next].After;
                    if (at == end)
                    {
                        return true;
                    }
                }
            }
        }
        return false;
    }

    private static string Hash(string text) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    /// <summary><paramref name="copy"/>, a copy of this config, saving as this one would: merged against the same file.</summary>
    /// <summary>
    /// §352: <paramref name="earlier"/>'s settings, a config of an earlier step, as a config that saves as
    /// this one does: merged against the file this one knows, so an edit undone writes those settings —
    /// not a merge that keeps what came after them as someone else's change.
    /// </summary>
    public Config Restored(Config earlier) => SameFile(earlier.Clone());

    private Config SameFile(Config copy)
    {
        copy.known = known;
        copy.seen = seen;
        copy.StandsIn = StandsIn;
        copy.Unread = Unread;
        return copy;
    }

    // --- Reading ------------------------------------------------------------------

    /// <summary>
    /// <see cref="Load"/> under the lock; <paramref name="mayWrite"/> false when the lock was not had
    /// (another process held it past its patience): read, and nothing written back.
    /// </summary>
    private static Config LoadLocked(bool mayWrite)
    {
        var path = FilePath;
        string? recovery = mayWrite && FinishCutOffSave() ? S.CoreConfigSaveFinished : null;
        // Longer than a save waits (about three seconds): at start there is nothing
        // to show until the file is read, and a scanner or backup tool lets go soon.
        if (ReadFile(path, out var text, attempts: 25) == FileRead.Locked)
        {
            // Defaults for now, knowing the file wasn't read: never saved, and replaced by the
            // file as it is once a reload reads it (Unread).
            var waiting = Defaults();
            waiting.Recovery = S.CoreConfigInUse;
            waiting.StandsIn = true;
            waiting.Unread = true;
            return waiting;
        }

        Config? config = null;
        string? onDisk = null;
        var write = false;
        var keepBackup = true;
        if (text is not null)
        {
            config = TryParse(text);
            if (config is not null)
            {
                onDisk = text;
            }
            else
            {
                var kept = KeepBroken();
                // A file that doesn't read never becomes the backup.
                keepBackup = false;
                if (Newest() is { } good)
                {
                    config = good.Config;
                    write = true;
                    recovery = S.CoreConfigRestoredGood(good.Name);
                }
                else
                {
                    recovery = S.CoreConfigStartedWithDefaults;
                }
                recovery += kept is null ? "" : " " + S.CoreConfigKeptIn(Path.GetFileName(kept));
            }
        }

        if (config is null)
        {
            // Nothing written: defaults are what a missing file means anyway.
            config = Defaults();
            config.Recovery = recovery;
            config.StandsIn = recovery is not null;
            CleanUp();
            return config;
        }
        // Written at once when Repair made up an id (MadeUpIds), so every process reading this
        // file from now on finds the same ids instead of each making up its own — two different
        // ids for one widget would merge as two widgets. Nothing else Repair does is a reason to
        // write: what it drops (a widget of a kind this build doesn't know, a newer build's word),
        // clamps or fills in, every process does alike as it reads, and writing it would take from
        // the file what a newer build put there. Never a file from a newer Karpo: writing it would
        // drop everything this build doesn't know, which is why Save refuses it too
        // (FromNewerKarpo). Not without the lock either, unless the last good version is being put
        // back: the file that didn't read is out of the way already, and config.json must not stay missing.
        var repaired = config.Repair();
        var json = config.ToJson();
        write |= repaired && mayWrite && MadeUpIds(onDisk, json);
        write &= !config.FromNewerKarpo;
        config.known = json;
        config.seen = onDisk;
        config.Recovery = recovery;
        config.StandsIn = recovery is not null;
        if (write)
        {
            try
            {
                WriteLocked(json, keepBackup);
                config.seen = json;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                Console.Error.WriteLine($"Karpo: {path} not written: {error.Message}");
            }
        }
        CleanUp();
        return config;
    }

    /// <summary>What a config.json that isn't there means, a new install's: repaired defaults, knowing there was no file.</summary>
    internal static Config Defaults()
    {
        var config = new Config();
        // The app buttons as the person's own taskbar has them (TaskbarApps).
        TaskbarApps.FromWindows(config);
        config.Repair();
        config.known = config.ToJson();
        return config;
    }

    /// <summary>
    /// Whether <paramref name="after"/>, a file's text as <see cref="Repair"/> left it, has an id that
    /// <paramref name="before"/>, the text it was read from, lacks: one Repair made up — for a layout, a
    /// widget or a folder's line that had none, or for the second of two that shared one — which
    /// another process reading the same file would make up differently. An id is any "id" property's
    /// text, wherever it stands. True when there is no text before, or it doesn't read.
    /// </summary>
    internal static bool MadeUpIds(string? before, string after)
    {
        if (before is null || Ids(before) is not { } had)
        {
            return true;
        }
        return Ids(after) is not { } now || !now.IsSubsetOf(had);
    }

    private static readonly JsonDocumentOptions Lenient = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };

    /// <summary>Every "id" property's text in <paramref name="text"/>, or null when it doesn't read.</summary>
    private static HashSet<string>? Ids(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text.TrimStart('﻿'), Lenient);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            Collect(document.RootElement);
            return ids;

            void Collect(JsonElement element)
            {
                switch (element.ValueKind)
                {
                    case JsonValueKind.Object:
                        foreach (var property in element.EnumerateObject())
                        {
                            if (property.NameEquals("id") && property.Value.ValueKind == JsonValueKind.String)
                            {
                                ids.Add(property.Value.GetString()!);
                            }
                            Collect(property.Value);
                        }
                        break;
                    case JsonValueKind.Array:
                        foreach (var item in element.EnumerateArray())
                        {
                            Collect(item);
                        }
                        break;
                }
            }
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The text as a config, or null when it doesn't read as one (not a JSON object, cut short, a typo).</summary>
    private static Config? TryParse(string text)
    {
        // Empty, whitespace, a run of NULs from a write the disk never finished, or JSON that isn't an object.
        if (!text.AsSpan().TrimStart("\uFEFF \t\r\n").StartsWith("{"))
        {
            return null;
        }
        try
        {
            return Parse(text);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// The file's text, read with every share mode (so a save by the other process can replace the
    /// file while this read is open); a sharing violation is retried <paramref name="attempts"/>
    /// times before it counts as locked (<see cref="DurableFile"/>).
    /// </summary>
    private static FileRead ReadFile(string path, out string? text, int attempts = DurableFile.Attempts) =>
        DurableFile.ReadText(path, out text, attempts);

    /// <summary>
    /// The newest earlier version that reads, for a config.json that doesn't: config.json.bak, or a
    /// temporary file a save that never got to swap it in left behind, when that is newer.
    /// </summary>
    private static (Config Config, string Name)? Newest()
    {
        var candidates = Temporaries();
        if (File.Exists(BackupPath))
        {
            candidates.Add(BackupPath);
        }
        foreach (var file in candidates.OrderByDescending(LastWrite))
        {
            if (ReadFile(file, out var text) == FileRead.Ok && text is not null && TryParse(text) is { } config)
            {
                return (config, Path.GetFileName(file));
            }
        }
        return null;
    }

    /// <summary>
    /// A save cut off between ReplaceFile's two renames (the process died, the power went) leaves
    /// no config.json, the version before as config.json.bak, and the new one whole in its temporary
    /// file (it went to the disk before the renames began): that one is moved in. True when that
    /// happened. The caller holds the lock, so no temporary file belongs to a save in progress.
    /// </summary>
    private static bool FinishCutOffSave()
    {
        if (File.Exists(FilePath))
        {
            return false;
        }
        foreach (var file in Temporaries().OrderByDescending(LastWrite))
        {
            if (ReadFile(file, out var text) != FileRead.Ok || text is null || TryParse(text) is null)
            {
                continue;
            }
            try
            {
                File.Move(file, FilePath);
                return true;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                Console.Error.WriteLine($"Karpo: {file} not moved in: {error.Message}");
                return false;
            }
        }
        return false;
    }

    private static DateTime LastWrite(string file)
    {
        try
        {
            return File.GetLastWriteTimeUtc(file);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return DateTime.MinValue;
        }
    }

    private static List<string> Temporaries()
    {
        try
        {
            return Directory.EnumerateFiles(Folder, "config.json.*.tmp")
                .Where(file => Path.GetFileName(file).StartsWith("config.json.", StringComparison.OrdinalIgnoreCase)
                    && file.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    // --- Writing ------------------------------------------------------------------

    /// <summary>
    /// Moves a config.json that doesn't read out of the way as config.json.broken-&lt;time&gt;, so
    /// nothing it said is lost; copies it when it can't be moved. Returns where it went.
    /// </summary>
    private static string? KeepBroken() => DurableFile.KeepBroken(FilePath);

    /// <summary>
    /// Writes <paramref name="text"/> as config.json: into a temporary file, through to the disk,
    /// then swapped in, the old file becoming config.json.bak when <paramref name="keepBackup"/>
    /// (<see cref="DurableFile.Write(string, string, bool)"/>). The caller holds the lock.
    /// </summary>
    private static void WriteLocked(string text, bool keepBackup) => DurableFile.Write(FilePath, text, keepBackup);

    /// <summary>
    /// Temporary files a crash left behind (a save in progress holds the lock, and
    /// is never this old), and all but the newest few of the copies kept beside config.json.
    /// </summary>
    private static void CleanUp()
    {
        try
        {
            var old = DateTime.UtcNow - Stale;
            foreach (var file in Temporaries().Where(file => LastWrite(file) < old))
            {
                File.Delete(file);
            }
            // And the copies kept beside it, the newest few of each kind: the files that did not read,
            // the ones an import or a reset replaced (DurableFile.Tidy, as for every file of Karpo's).
            DurableFile.Tidy(FilePath);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
        }
    }

    // --- The lock -------------------------------------------------------------------

    /// <summary>Tests: the lock's name for this config folder, to hold it as another process would.</summary>
    internal static string LockName => FileLock.Name();

    /// <summary>Tests: how long a save waits for another's lock before it refuses.</summary>
    internal static int LockPatience
    {
        get => FileLock.Patience;
        set => FileLock.Patience = value;
    }

    /// <summary>
    /// The named mutex that makes read-merge-write one step for every process using this config folder
    /// (so tests on their own folder never wait for the running Karpo). A holder that died leaves it
    /// abandoned, which counts as free: every write is whole or not there. When it isn't free within
    /// <see cref="LockMilliseconds"/> (<see cref="TimedOut"/>), another process is in the middle of
    /// its read, merge and write — hung, or held up — and a write now would be lost under its own, or
    /// lose it: <see cref="Save"/> and <see cref="Replace"/> refuse (the caller keeps the change and
    /// tries again), <see cref="Load"/> and <see cref="Reload"/> read and write nothing back. A
    /// mutex this process may not open at all (one an elevated Karpo made) is not that: the work goes
    /// ahead without it, the merge against the file read just before the write still keeping what
    /// the other wrote.
    /// </summary>
    private sealed class FileLock : IDisposable
    {
        private readonly Mutex? mutex;

        private FileLock(Mutex? mutex, bool timedOut = false)
        {
            this.mutex = mutex;
            TimedOut = timedOut;
        }

        /// <summary>Another process held the lock past <see cref="LockMilliseconds"/>: this one is not held.</summary>
        public bool TimedOut { get; }

        /// <summary>Tests: how long a save waits for the lock, <see cref="LockMilliseconds"/> unless set.</summary>
        internal static int Patience { get; set; } = LockMilliseconds;

        public static FileLock Take()
        {
            Mutex? mutex = null;
            try
            {
                mutex = new Mutex(false, Name());
                bool owned;
                try
                {
                    owned = mutex.WaitOne(Patience);
                }
                catch (AbandonedMutexException)
                {
                    owned = true;
                }
                if (owned)
                {
                    return new FileLock(mutex);
                }
                Console.Error.WriteLine("Karpo: config.json is still being saved by another process; nothing written now");
                mutex.Dispose();
                return new FileLock(null, timedOut: true);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or WaitHandleCannotBeOpenedException)
            {
                // Such as a mutex an elevated Karpo made, which this one may not open.
                Console.Error.WriteLine($"Karpo: config.json lock not available: {error.Message}");
            }
            mutex?.Dispose();
            return new FileLock(null);
        }

        public void Dispose()
        {
            if (mutex is null)
            {
                return;
            }
            mutex.ReleaseMutex();
            mutex.Dispose();
        }

        /// <summary>Local\karpo.config.&lt;hash of the folder&gt;: the same name in every process for the same folder.</summary>
        internal static string Name()
        {
            var folder = Path.GetFullPath(Folder).TrimEnd('\\', '/').ToUpperInvariant();
            // FNV-1a: string.GetHashCode differs from process to process.
            var hash = 14695981039346656037UL;
            foreach (var character in folder)
            {
                hash = (hash ^ character) * 1099511628211UL;
            }
            return $@"Local\karpo.config.{hash:x16}";
        }
    }
}
