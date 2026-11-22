// sports: the teams' and the leagues' logos (docs\widgets.md §239, §369), for the sports lines, the
// face's folder and the search's rows in a menu alike: fetched once from ESPN's picture server alone
// (a.espncdn.com), small, through its resizer, and kept — in memory as pixels, and on disk as the
// picture ESPN gave, in sports-logos\ in the local folder, so a restart draws them at once and a logo is
// fetched again only once a month. A logo changes about never.
//
// Nothing here blocks the thread that asks: a read is a lookup under a lock; the disk and the network
// are read on the thread pool, and whoever asked is told once the picture is in (its island's store
// signal, a menu's post to its window). Until then, and where one never comes, the asker draws its own
// stand-in: a monogram in the side's colour on a line, its glyph on a menu's row. A logo that could not be
// had is asked again after a few minutes, not on every draw.

using System.Security.Cryptography;
using System.Text;
using Karpo.Core;
using Karpo.Dock.Favicons;

namespace Karpo.Dock.Sports;

internal sealed class SportsLogos
{
    /// <summary>The side a logo is asked for and decoded at, in px: a 28 DIP circle at 200 %.</summary>
    internal const int Side = 64;

    /// <summary>The logos kept in memory, at most: a league's every team and a search's twenty, twice over.</summary>
    private const int KeptInMemory = 256;

    /// <summary>The most a logo may weigh.</summary>
    private const int MostBytes = 1 << 20;

    /// <summary>How old a logo on disk may be before it is fetched again: a month.</summary>
    internal static readonly TimeSpan Fresh = TimeSpan.FromDays(30);

    /// <summary>
    /// How long a logo on disk is kept while nothing draws it: three months. One drawn is fetched again
    /// and written anew every month (Fresh), so only a team or a league nobody shows any more is let go.
    /// </summary>
    internal static readonly TimeSpan Unused = TimeSpan.FromDays(90);

    /// <summary>How long a logo that could not be had is left before it is asked again.</summary>
    internal static readonly TimeSpan Retry = TimeSpan.FromMinutes(5);

    private readonly Lock gate = new();
    private readonly Dictionary<string, (Pixels? Pixels, DateTime At)> kept = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<Action>> waiting = new(StringComparer.Ordinal);
    private readonly Func<string, CancellationToken, Task<byte[]?>> fetch;
    private readonly Func<byte[], Pixels?> decode;
    private readonly string? folder;
    private readonly Func<DateTime> now;

    // The folder looked over this run (Sweep): once is enough.
    private int swept;

    /// <summary>
    /// Logos fetched through <paramref name="fetch"/> (an address in, its bytes or null out), decoded by
    /// <paramref name="decode"/>, kept on disk in <paramref name="folder"/> (none: memory alone).
    /// </summary>
    internal SportsLogos(Func<string, CancellationToken, Task<byte[]?>> fetch, string? folder, Func<byte[], Pixels?>? decode = null, Func<DateTime>? now = null)
    {
        this.fetch = fetch;
        this.folder = folder;
        this.decode = decode ?? (bytes => FaviconImage.Decode(bytes, Side));
        this.now = now ?? (() => Clock.RealUtc());
    }

    private static SportsLogos? shared;

    /// <summary>The dock's: ESPN's picture server, kept in the local folder.</summary>
    public static SportsLogos Shared =>
        LazyInitializer.EnsureInitialized(ref shared, () => new SportsLogos(Download, Path.Combine(Config.LocalFolder, "sports-logos")));

    /// <summary>How many fetches it has started, for tests.</summary>
    internal int Fetched { get; private set; }

    /// <summary>
    /// The logo at <paramref name="address"/> — its drawing for a dark ground where
    /// <paramref name="dark"/> and ESPN has one —, null while it comes (<paramref name="arrived"/> is
    /// called, from the thread pool, once it is in) and for an address that is no logo of ESPN's.
    /// </summary>
    public Pixels? Get(string address, bool dark, Action? arrived)
    {
        if (!SportsAnswers.IsLogo(address))
        {
            return null;
        }
        var key = dark ? "dark|" + address : address;
        lock (gate)
        {
            if (kept.TryGetValue(key, out var known) && (known.Pixels is not null || now() - known.At < Retry))
            {
                return known.Pixels;
            }
            if (waiting.TryGetValue(key, out var told))
            {
                if (arrived is not null)
                {
                    told.Add(arrived);
                }
                return null;
            }
            waiting[key] = arrived is null ? [] : [arrived];
            Fetched++;
        }
        _ = Task.Run(() => LoadAsync(key, address, dark));
        return null;
    }

    private async Task LoadAsync(string key, string address, bool dark)
    {
        if (folder is not null && Interlocked.Exchange(ref swept, 1) == 0)
        {
            Sweep(folder);
        }
        Pixels? picture = null;
        try
        {
            var file = folder is null ? null : Path.Combine(folder, FileOf(key));
            var bytes = file is not null && IsFresh(file) ? Read(file) : null;
            if (bytes is null)
            {
                using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                bytes = await fetch(SportsAnswers.LogoUrl(address, Side, dark), stop.Token).ConfigureAwait(false);
                if (dark && bytes is not { Length: > 0 })
                {
                    bytes = await fetch(SportsAnswers.LogoUrl(address, Side), stop.Token).ConfigureAwait(false);
                }
                if (bytes is { Length: > 0 } && file is not null)
                {
                    Write(file, bytes);
                }
                // Offline: a month-old logo is still the team's.
                bytes ??= file is not null ? Read(file) : null;
            }
            if (bytes is { Length: > 0 })
            {
                picture = decode(bytes);
            }
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Console.Error.WriteLine($"Karpo: a sports logo was not read: {error.GetType().Name}");
        }
        List<Action> told;
        lock (gate)
        {
            if (kept.Count >= KeptInMemory)
            {
                kept.Clear();
            }
            kept[key] = (picture, now());
            told = waiting.Remove(key, out var list) ? list : [];
        }
        if (picture is not null)
        {
            foreach (var each in told)
            {
                each();
            }
        }
    }

    /// <summary>
    /// The logos no line, folder or search has drawn for <see cref="Unused"/>, and the half-written
    /// files a crash left: one file a logo ever shown would otherwise pile up in sports-logos for good.
    /// </summary>
    internal void Sweep(string from)
    {
        try
        {
            if (!Directory.Exists(from))
            {
                return;
            }
            foreach (var file in Directory.EnumerateFiles(from))
            {
                var age = now() - File.GetLastWriteTimeUtc(file);
                if ((file.EndsWith(".img", StringComparison.OrdinalIgnoreCase) && age > Unused) || (file.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase) && age > TimeSpan.FromHours(1)))
                {
                    File.Delete(file);
                }
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>The file a logo is kept in: its address's hash, so no address names a path.</summary>
    internal static string FileOf(string key) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key)).AsSpan(0, 12)) + ".img";

    private bool IsFresh(string file)
    {
        try
        {
            return File.Exists(file) && now() - File.GetLastWriteTimeUtc(file) < Fresh;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static byte[]? Read(string file)
    {
        try
        {
            return File.Exists(file) ? File.ReadAllBytes(file) : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static void Write(string file, byte[] bytes)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            var temporary = file + ".tmp";
            File.WriteAllBytes(temporary, bytes);
            File.Move(temporary, file, overwrite: true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Kept in memory for this run; fetched again on the next.
        }
    }

    private static async Task<byte[]?> Download(string address, CancellationToken cancel)
    {
        if (!SportsAnswers.IsLogo(address))
        {
            return null;
        }
        using var response = await IslandWeb.Client.GetAsync(address, HttpCompletionOption.ResponseHeadersRead, cancel).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength is > MostBytes)
        {
            return null;
        }
        var bytes = await response.Content.ReadAsByteArrayAsync(cancel).ConfigureAwait(false);
        return bytes.Length is > 0 and <= MostBytes ? bytes : null;
    }
}
