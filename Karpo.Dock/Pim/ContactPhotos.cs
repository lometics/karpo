// The people's pictures, for the contacts line (docs\widgets.md, "The contacts wear their pictures").
//
// A person's picture is fetched once per version (Person.Photo: a vCard PHOTO's hash, a Graph
// contact's changeKey) and kept on disk, small: decoded and cut down to Side px — a row's
// picture at twice its Standard size — as the premultiplied pixels a row draws. One file a
// person, contact-photos\<key>.bin in the local folder (Config.LocalFolder), the key a hash of
// the person's id and their picture's version, so a new picture is a new file and nothing of the
// person is in a name. Encrypted for this Windows user, as the PIM caches are (PimCache.Protect).
// An empty file is "they have none": a Graph contact without a picture (404) is asked once per
// version too, not at every read.
//
// The store hands over everyone its accounts gave after each read (ContactStore, Want), and the
// work runs on the pool, one pass at a time: what is in memory stays, what is on disk is taken in,
// the rest is fetched four at a time (Graph's own limit of requests at once for a mailbox), each
// told as it comes so the rows fill in. An account that fails a picture is asked no more in that
// pass, and nothing it failed is written: the next read tries again. Once every account was read,
// a file of nobody listed any more is deleted. A line only ever reads memory (Of) — never the disk,
// never the network — so drawing never waits on a picture.
//
// Tests and isolated runs keep the pictures in memory alone (no folder).

using System.Security.Cryptography;
using System.Text;
using Karpo.Accounts;
using Karpo.Core;
using Karpo.Dock.Favicons;

namespace Karpo.Dock.Pim;

internal sealed class ContactPhotos(string? folder, Action changed)
{
    /// <summary>The most a kept picture is across, in px: a row's (24 DIP) at Large and 200 %.</summary>
    internal const int Side = 64;

    /// <summary>Pictures asked for at once: Graph serves a mailbox four requests at a time.</summary>
    internal const int AtOnce = 4;

    /// <summary>The most bytes a picture is taken at; anything larger is no picture.</summary>
    internal const int MostBytes = 8 << 20;

    /// <summary>The kept pictures' folder's name in the local folder.</summary>
    internal const string FolderName = "contact-photos";

    private readonly Lock gate = new();

    /// <summary>Under <see cref="gate"/>: each picture known, by its key; null for "they have none".</summary>
    private readonly Dictionary<string, Pixels?> known = new(StringComparer.Ordinal);

    private Task running = Task.CompletedTask;
    private (IReadOnlyList<(IAccountReader Reader, Person Person)> Wanted, bool Prune)? next;

    /// <summary>The dock's folder; none on an isolated run, which keeps nothing.</summary>
    internal static string? DockFolder() => Config.IsIsolated ? null : Path.Combine(Config.LocalFolder, FolderName);

    /// <summary>Where they are kept; null for memory alone.</summary>
    internal string? Folder { get; } = folder;

    /// <summary>
    /// The key <paramref name="person"/>'s picture is kept under: a hash of their id and their picture's
    /// version, so another picture is another key; null where they have none.
    /// </summary>
    internal static string? Key(Person person) =>
        person.Photo is { } version
            ? Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(person.Id + "\n" + version)))[..32]
            : null;

    /// <summary>The file <paramref name="key"/>'s picture is kept in.</summary>
    internal string? FileOf(string key) => Folder is null ? null : Path.Combine(Folder, key + ".bin");

    /// <summary>§661: what an icon's address of a person's picture starts with (<see cref="Address"/>): Windows search's people wear theirs.</summary>
    internal const string Scheme = "contact-photo:";

    /// <summary>§661: the address <paramref name="person"/>'s picture is asked by, the one kept (<see cref="Key"/>); null where they have none.</summary>
    internal static string? Address(Person person) => Key(person) is { } key ? Scheme + key : null;

    /// <summary>§661: the key of a picture's <paramref name="address"/>; null for an address that is no person's picture.</summary>
    internal static string? KeyOf(string address) => address.StartsWith(Scheme, StringComparison.Ordinal) ? address[Scheme.Length..] : null;

    /// <summary><paramref name="person"/>'s picture where one is in memory; null where they have none or it has not come yet.</summary>
    public Pixels? Of(Person person) => Key(person) is { } key ? Of(key) : null;

    /// <summary>§661: the picture kept under <paramref name="key"/> where it is in memory; null where it is none or has not come yet.</summary>
    public Pixels? Of(string key)
    {
        lock (gate)
        {
            return known.GetValueOrDefault(key);
        }
    }

    /// <summary>For tests: <paramref name="person"/>'s picture is <paramref name="pixels"/>, as if fetched.</summary>
    internal void Put(Person person, Pixels? pixels)
    {
        if (Key(person) is { } key)
        {
            lock (gate)
            {
                known[key] = pixels;
            }
        }
    }

    /// <summary>
    /// Everyone the accounts gave now, each with the account that gave them: their pictures are made
    /// ready, in this order. With <paramref name="prune"/> (every account was read), what nobody of
    /// them wears any more goes from the disk too. A pass running takes this in when it ends.
    /// </summary>
    public void Want(IReadOnlyList<(IAccountReader Reader, Person Person)> wanted, bool prune)
    {
        lock (gate)
        {
            next = (wanted, prune);
            if (running.IsCompleted)
            {
                running = Task.Run(() => Troubles.Guard("Contacts: their pictures", PassesAsync));
            }
        }
    }

    /// <summary>The passes in flight, for tests.</summary>
    internal Task Idle()
    {
        lock (gate)
        {
            return running;
        }
    }

    private async Task PassesAsync()
    {
        while (true)
        {
            (IReadOnlyList<(IAccountReader Reader, Person Person)> Wanted, bool Prune) pass;
            lock (gate)
            {
                if (next is not { } taken)
                {
                    return;
                }
                pass = taken;
                next = null;
            }
            await PassAsync(pass.Wanted, pass.Prune).ConfigureAwait(false);
        }
    }

    private async Task PassAsync(IReadOnlyList<(IAccountReader Reader, Person Person)> wanted, bool prune)
    {
        var keyed = wanted
            .Select(each => (each.Reader, each.Person, Key: Key(each.Person)))
            .Where(each => each.Key is not null)
            .DistinctBy(each => each.Key, StringComparer.Ordinal)
            .ToList();
        var keys = keyed.Select(each => each.Key!).ToHashSet(StringComparer.Ordinal);
        lock (gate)
        {
            // Nobody listed wears these any more.
            foreach (var gone in known.Keys.Where(key => !keys.Contains(key)).ToList())
            {
                known.Remove(gone);
            }
        }

        // What is kept on disk, at once; the rest is fetched.
        var missing = new List<(IAccountReader Reader, Person Person, string Key)>();
        var took = false;
        foreach (var (reader, person, key) in keyed)
        {
            bool have;
            lock (gate)
            {
                have = known.ContainsKey(key!);
            }
            if (have)
            {
                continue;
            }
            if (Load(key!) is { } kept)
            {
                lock (gate)
                {
                    known[key!] = kept.Pixels;
                }
                took |= kept.Pixels is not null;
            }
            else
            {
                missing.Add((reader, person, key!));
            }
        }
        if (took)
        {
            changed();
        }

        var failed = new HashSet<string>(StringComparer.Ordinal);
        using var slots = new SemaphoreSlim(AtOnce);
        await Task.WhenAll(missing.Select(async job =>
        {
            await slots.WaitAsync().ConfigureAwait(false);
            try
            {
                lock (failed)
                {
                    if (failed.Contains(job.Reader.Account.Id))
                    {
                        return;
                    }
                }
                byte[]? bytes;
                try
                {
                    bytes = await job.Reader.PhotoAsync(job.Person, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception error) when (error is not OutOfMemoryException)
                {
                    // Offline, signed out, refused: this account's pictures wait for the next read.
                    lock (failed)
                    {
                        failed.Add(job.Reader.Account.Id);
                    }
                    return;
                }
                var pixels = bytes is { Length: > 0 and <= MostBytes } ? FaviconImage.Decode(bytes, Side) : null;
                Save(job.Key, pixels);
                lock (gate)
                {
                    known[job.Key] = pixels;
                }
                if (pixels is not null)
                {
                    changed();
                }
            }
            finally
            {
                slots.Release();
            }
        })).ConfigureAwait(false);

        if (prune)
        {
            Prune(keys);
        }
    }

    /// <summary>A picture kept on disk: its pixels, none for "they have none".</summary>
    internal readonly record struct Kept(Pixels? Pixels);

    /// <summary>A kept picture: its pixels, or none for "they have none"; null where none is kept or it does not read.</summary>
    internal Kept? Load(string key)
    {
        if (FileOf(key) is not { } path || !File.Exists(path))
        {
            return null;
        }
        try
        {
            var bytes = File.ReadAllBytes(path);
            return bytes.Length == 0 ? new Kept(null) : Unpacked(PimCache.Unprotect(bytes)) is { } pixels ? new Kept(pixels) : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or CryptographicException)
        {
            return null;
        }
    }

    /// <summary>Keeps <paramref name="pixels"/> as <paramref name="key"/>'s picture; null as "they have none".</summary>
    private void Save(string key, Pixels? pixels)
    {
        if (FileOf(key) is not { } path)
        {
            return;
        }
        try
        {
            Directory.CreateDirectory(Folder!);
            var temporary = path + ".tmp";
            File.WriteAllBytes(temporary, pixels is null ? [] : PimCache.Protect(Packed(pixels)));
            File.Move(temporary, path, overwrite: true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or CryptographicException)
        {
            Console.Error.WriteLine($"Karpo: a contact's picture not saved ({error.GetType().Name}: {error.HResult:X8})");
        }
    }

    /// <summary>Deletes every kept picture but <paramref name="keys"/>'.</summary>
    private void Prune(HashSet<string> keys)
    {
        if (Folder is null || !Directory.Exists(Folder))
        {
            return;
        }
        try
        {
            foreach (var path in Directory.EnumerateFiles(Folder))
            {
                if (!keys.Contains(Path.GetFileNameWithoutExtension(path)) || !path.EndsWith(".bin", StringComparison.Ordinal))
                {
                    File.Delete(path);
                }
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Karpo: old contact pictures not removed ({error.GetType().Name}: {error.HResult:X8})");
        }
    }

    /// <summary>A picture as a file holds it before it is encrypted: its width, its height, its pixels.</summary>
    internal static byte[] Packed(Pixels pixels)
    {
        var bytes = new byte[8 + pixels.Data.Length];
        BitConverter.TryWriteBytes(bytes.AsSpan(0, 4), pixels.Width);
        BitConverter.TryWriteBytes(bytes.AsSpan(4, 4), pixels.Height);
        pixels.Data.CopyTo(bytes, 8);
        return bytes;
    }

    /// <summary>What <see cref="Packed"/> wrote; null for anything else.</summary>
    internal static Pixels? Unpacked(byte[] bytes)
    {
        if (bytes.Length < 8)
        {
            return null;
        }
        var width = BitConverter.ToInt32(bytes, 0);
        var height = BitConverter.ToInt32(bytes, 4);
        if (width is <= 0 or > Side || height is <= 0 or > Side || bytes.Length != 8 + (width * height * 4))
        {
            return null;
        }
        return new Pixels { Data = bytes[8..], Width = width, Height = height };
    }
}
