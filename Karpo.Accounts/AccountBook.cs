// accounts.json on disk, which both apps change.
//
// The settings app adds and removes accounts; the dock writes a Microsoft
// account's refresh token back each time Microsoft hands out a new one. So
// every change is a patch — "add this account", "remove that id", "this id's
// secret is now this" — applied to the file as it is at that moment, under a
// named mutex, and written the durable way (DurableFile): a temporary file,
// through to the disk, swapped in with the version before kept as
// accounts.json.bak. Two apps changing different accounts, or the same one in
// turn, never undo each other, which a whole-list save would.
//
// The accounts are only what the file says when it read. A file another
// program holds (a virus scanner, a backup, the indexer: the read is tried
// again for half a second first) is busy, not empty: the dock keeps the
// accounts it had, and a change is not written — it would put that one patch
// in place of every account — but fails, for the caller to try again. A file
// that is empty or cut short (a write the disk never finished) is the version
// before it, accounts.json.bak, where that reads. A file that does not read and
// has no good version before it is no accounts for whoever asks to draw them;
// the dock keeps the accounts it had, and the file is not overwritten until
// someone changes an account, and then it is kept aside first.
//
// Reads take the mutex too, so none lands between the two renames of a
// change's swap, when there is for a moment no accounts.json at all.

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Karpo.Core;

namespace Karpo.Accounts;

/// <summary>What reading accounts.json found.</summary>
public enum AccountsRead
{
    /// <summary>These are the accounts: the file's, or, where it was empty or cut short, the version before it's (accounts.json.bak).</summary>
    Ok,

    /// <summary>There is no accounts.json: there are no accounts.</summary>
    Missing,

    /// <summary>Another program held the file every time it was tried: what accounts there are is not known now.</summary>
    Busy,

    /// <summary>accounts.json does not read, and neither does a version before it: what accounts there are is not known.</summary>
    Broken,
}

public static class AccountBook
{
    /// <summary>How long a change waits for the other app's change to finish.</summary>
    private const int LockMilliseconds = 5000;

    /// <summary>Where the accounts are kept: the local folder, because the secrets in it only read on this PC.</summary>
    public static string FilePath => Path.Combine(Config.LocalFolder, "accounts.json");

    /// <summary>The accounts, in the order they were added; empty when there is no file or it does not read now.</summary>
    public static IReadOnlyList<Account> Load() => Read(FilePath, out _).Accounts;

    /// <summary>
    /// The accounts, in the order they were added, and whether that is what the file says: only on
    /// <see cref="AccountsRead.Ok"/> and <see cref="AccountsRead.Missing"/> is it. On
    /// <see cref="AccountsRead.Busy"/> and <see cref="AccountsRead.Broken"/> they are empty, and
    /// nobody may take that for the accounts having gone.
    /// </summary>
    public static AccountsRead Load(out IReadOnlyList<Account> accounts) => Load(FilePath, out accounts);

    internal static AccountsRead Load(string path, out IReadOnlyList<Account> accounts)
    {
        accounts = Read(path, out var read).Accounts;
        return read;
    }

    /// <summary>The account with this id, as the file has it now.</summary>
    public static Account? Find(string id) => Load().FirstOrDefault(account => account.Id == id);

    /// <summary>Keeps <paramref name="account"/>, with <paramref name="secret"/> protected on it.</summary>
    public static Account Add(Account account, string? secret)
    {
        var kept = account with { Secret = secret is null ? null : Secrets.Protect(secret) };
        Change(file =>
        {
            file.Accounts.RemoveAll(one => one.Id == kept.Id);
            file.Accounts.Add(kept);
        });
        return kept;
    }

    /// <summary>Forgets the account, secret and all.</summary>
    public static void Remove(string id) => Change(file => file.Accounts.RemoveAll(one => one.Id == id));

    /// <summary>The account's secret is now <paramref name="secret"/>; nothing happens when it is gone.</summary>
    public static void SetSecret(string id, string secret) => SetSecret(FilePath, id, secret);

    internal static void SetSecret(string path, string id, string secret)
    {
        var protectedSecret = Secrets.Protect(secret);
        Change(path, file =>
        {
            var at = file.Accounts.FindIndex(one => one.Id == id);
            if (at >= 0)
            {
                file.Accounts[at] = file.Accounts[at] with { Secret = protectedSecret };
            }
        });
    }

    /// <summary>
    /// The part <paramref name="key"/> (<see cref="SyncKeys"/>) of the account is synced, or not; nothing
    /// happens when the account is gone. The dock hears the file change and reads, or drops, it.
    /// </summary>
    public static void SetSynced(string id, string key, bool on) => SetSynced(FilePath, id, key, on);

    internal static void SetSynced(string path, string id, string key, bool on) => Change(path, file =>
    {
        var at = file.Accounts.FindIndex(one => one.Id == id);
        if (at >= 0)
        {
            file.Accounts[at] = file.Accounts[at].Synced(key, on);
        }
    });

    /// <summary>
    /// The part <paramref name="key"/> (<see cref="SyncKeys"/>) of the account wears
    /// <paramref name="colour"/> (<c>#RRGGBB</c>), or its own again (null); nothing happens when the
    /// account is gone. The dock hears the file change and draws it in that colour, reading nothing.
    /// </summary>
    public static void SetColour(string id, string key, string? colour) => SetColour(FilePath, id, key, colour);

    internal static void SetColour(string path, string id, string key, string? colour) => Change(path, file =>
    {
        var at = file.Accounts.FindIndex(one => one.Id == id);
        if (at >= 0)
        {
            file.Accounts[at] = file.Accounts[at].Coloured(key, colour);
        }
    });

    /// <summary>The secret an account holds, or null where it has none this user can read.</summary>
    public static string? Secret(Account account) => Secrets.Unprotect(account.Secret);

    /// <summary>The file as text, for the watcher's comparison; <see cref="FileRead.Locked"/> where another program holds it.</summary>
    internal static FileRead Text(string path, out string? text) => DurableFile.ReadText(path, out text);

    /// <summary>The file at <paramref name="path"/>, and what reading it found (<paramref name="read"/>).</summary>
    internal static AccountFile Read(string path, out AccountsRead read)
    {
        var reading = Locked(path, () => ReadLocked(path));
        read = reading.Read;
        return reading.File;
    }

    private static void Change(Action<AccountFile> patch) => Change(FilePath, patch);

    /// <summary>
    /// Reads the file, patches it and writes it back, all under the lock. Throws IOException, and
    /// writes nothing, where the file is held by another program (<see cref="AccountsRead.Busy"/>)
    /// or could not be written: the file is then as it was, every account in it.
    /// </summary>
    internal static void Change(string path, Action<AccountFile> patch) => Locked(path, () =>
    {
        var reading = ReadLocked(path);
        if (reading.Read == AccountsRead.Busy)
        {
            // Not known what it says: written, this one patch would take the place of every account.
            throw new IOException($"{Path.GetFileName(path)} is held by another program; nothing changed");
        }
        var mended = reading.Read == AccountsRead.Broken || reading.FromBackup;
        if (mended)
        {
            // Kept aside, never lost: the person may have been editing it.
            DurableFile.KeepBroken(path);
        }
        patch(reading.File);
        // A file that did not read never becomes the backup: the good version before it stays there.
        DurableFile.Write(path, JsonSerializer.Serialize(reading.File, AccountJson.Default.AccountFile), keepBackup: !mended);
        return true;
    });

    private static Reading ReadLocked(string path)
    {
        switch (DurableFile.ReadText(path, out var text))
        {
            case FileRead.Missing:
                // A change cut short between File.Replace's two renames leaves no accounts.json and the
                // version before it as accounts.json.bak: that is the person's accounts, not none.
                if (DurableFile.ReadText(DurableFile.BackupOf(path), out var kept) == FileRead.Ok && Parse(kept!) is { } saved)
                {
                    Console.Error.WriteLine($"Karpo: {Path.GetFileName(path)} is gone; the version before it is there");
                    return new Reading(saved, AccountsRead.Ok, FromBackup: true);
                }
                return new Reading(new AccountFile(), AccountsRead.Missing);
            case FileRead.Locked:
                Console.Error.WriteLine($"Karpo: {Path.GetFileName(path)} is held by another program");
                return new Reading(new AccountFile(), AccountsRead.Busy);
        }
        if (Parse(text!) is { } file)
        {
            return new Reading(file, AccountsRead.Ok);
        }
        // Empty, cut short, a run of NULs from a write the disk never finished, a hand edit's typo:
        // the version before it, which read when a change swapped it out.
        if (DurableFile.ReadText(DurableFile.BackupOf(path), out var before) == FileRead.Ok && Parse(before!) is { } backup)
        {
            Console.Error.WriteLine($"Karpo: {Path.GetFileName(path)} does not read; the version before it does");
            return new Reading(backup, AccountsRead.Ok, FromBackup: true);
        }
        return new Reading(new AccountFile(), AccountsRead.Broken);
    }

    /// <summary>The text as the file's accounts, or null where it does not read as them.</summary>
    private static AccountFile? Parse(string text)
    {
        try
        {
            if (JsonSerializer.Deserialize(text, AccountJson.Default.AccountFile) is not { Accounts: not null } file)
            {
                return null;
            }
            // A hand edit may leave an account without its required parts; it is dropped, not guessed at.
            file.Accounts.RemoveAll(one => string.IsNullOrEmpty(one?.Id) || string.IsNullOrEmpty(one.Address));
            return file;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary><paramref name="work"/> under the file's mutex; without it, where the other app holds it longer than <see cref="LockMilliseconds"/>.</summary>
    private static T Locked<T>(string path, Func<T> work)
    {
        using var mutex = new Mutex(false, MutexName(path));
        var held = false;
        try
        {
            try
            {
                held = mutex.WaitOne(LockMilliseconds);
            }
            catch (AbandonedMutexException)
            {
                // The other app died holding it; the file is whole, because writes replace it.
                held = true;
            }
            return work();
        }
        finally
        {
            if (held)
            {
                mutex.ReleaseMutex();
            }
        }
    }

    /// <summary>One mutex per file, so an isolated run's file and the person's never wait on each other.</summary>
    private static string MutexName(string path)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(path).ToUpperInvariant()));
        return @"Local\Karpo.Accounts." + Convert.ToHexString(hash, 0, 8);
    }

    /// <summary>What a read under the lock found; <paramref name="FromBackup"/> where the file itself did not read and accounts.json.bak did.</summary>
    private readonly record struct Reading(AccountFile File, AccountsRead Read, bool FromBackup = false);
}

/// <summary>
/// Tells when accounts.json changed on disk — an account added or removed in the settings app, or
/// a token written back — so the dock reads the accounts again.
/// </summary>
public sealed class AccountWatch : IDisposable
{
    /// <summary>A burst of writes (the temporary file, the move) is one change.</summary>
    private static readonly TimeSpan Settle = TimeSpan.FromMilliseconds(300);

    /// <summary>A file another program holds is looked at again this much later.</summary>
    private static readonly TimeSpan Again = TimeSpan.FromSeconds(2);

    private readonly FileSystemWatcher? watcher;
    private readonly Timer timer;
    private string? last;

    public AccountWatch()
    {
        timer = new Timer(_ => Troubles.Guard("Accounts: looking at accounts.json", Check));
        AccountBook.Text(AccountBook.FilePath, out last);
        try
        {
            Directory.CreateDirectory(Config.LocalFolder);
            watcher = new FileSystemWatcher(Config.LocalFolder, "accounts.json")
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
            };
            watcher.Changed += (_, _) => Troubles.Guard("Accounts: hearing accounts.json change", Heard);
            watcher.Created += (_, _) => Troubles.Guard("Accounts: hearing accounts.json change", Heard);
            watcher.Deleted += (_, _) => Troubles.Guard("Accounts: hearing accounts.json change", Heard);
            watcher.Renamed += (_, _) => Troubles.Guard("Accounts: hearing accounts.json change", Heard);
            watcher.EnableRaisingEvents = true;
        }
        catch (Exception error) when (error is IOException or ArgumentException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Karpo: accounts not watched: {error.Message}");
        }
    }

    /// <summary>The file says something else than it did. Raised on a pool thread.</summary>
    public event Action? Changed;

    public void Dispose()
    {
        watcher?.Dispose();
        timer.Dispose();
    }

    /// <summary>
    /// A change heard: read once the burst has settled. On the watcher's thread, so a timer
    /// disposed under it (the watch ended meanwhile) is reported rather than thrown.
    /// </summary>
    private void Heard() => timer.Change(Settle, Timeout.InfiniteTimeSpan);

    private void Check()
    {
        if (AccountBook.Text(AccountBook.FilePath, out var now) == FileRead.Locked)
        {
            // Held by another program: what it says now is not known, so it is looked at again.
            timer.Change(Again, Timeout.InfiniteTimeSpan);
            return;
        }
        if (now == last)
        {
            return;
        }
        last = now;
        Changed?.Invoke();
    }
}
