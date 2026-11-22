// Helpers the newer islands share: one HTTP client, their JSON files, dates
// in the user's own format, the widgets key, and time zones by IANA name.

using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Serialization;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

/// <summary>
/// The one HTTP client the islands use, made the first time one needs it, so
/// a dock without weather or calendar never loads the network stack.
/// </summary>
internal static class IslandWeb
{
    private static readonly Lazy<HttpClient> Shared = new(() =>
    {
        var handler = new SocketsHttpHandler
        {
            // DNS changes (a VPN, another network) are picked up by new connections.
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            PooledConnectionIdleTimeout = TimeSpan.FromSeconds(30),
            AutomaticDecompression = DecompressionMethods.All,
            // stocks: no cookies, ever. None of what Karpo fetches needs one, and a client that
            // keeps them is a client that can be walked through a consent gate without noticing.
            UseCookies = false,
        };
        var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Karpo/1.0");
        return client;
    });

    public static HttpClient Client => Shared.Value;

    /// <summary>Reads at most <paramref name="limit"/> bytes of a response as text; null when it is longer.</summary>
    public static async Task<string?> Text(HttpResponseMessage response, int limit, CancellationToken cancel)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancel).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancel).ConfigureAwait(false)) > 0)
        {
            if (buffer.Length + read > limit)
            {
                return null;
            }
            buffer.Write(chunk, 0, read);
        }
        return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }
}

/// <summary>
/// How a shared store tells the dock thread it has news. Each attached island
/// that uses the store posts WM_KARPO_ISLAND, so the dock refreshes every one
/// of them, and the first of those Refresh calls raises the store's Changed.
/// With no island attached, the dispatcher queue of the thread that <b>looks</b>
/// at the store raises it.
/// <para>
/// Which thread that is was decided here once, in a field initialiser — the
/// thread that <i>made</i> the store. A store is usually made by whichever thread
/// first touches a Lazy, and that is not necessarily the one using it; in the
/// test process it was another test's, so its news, and a notification store's
/// work with it, went to a thread that had never looked (docs\widgets.md §11:
/// making a thing asks Windows nothing, and the first look starts the work).
/// So the queue is taken at the first <see cref="Look"/>, and nothing reads it
/// before then.
/// </para>
/// <para>
/// Adding an island and delivering are looks, because both are on the dock's
/// thread by contract. A store whose listener does neither — a flyout that only
/// subscribes to Changed — looks at its own door: CalendarStore.Open, a
/// notification store's first Use. The favicon store is made by the dock on its
/// own thread on purpose, and nothing ever attaches to it or delivers it, so its
/// making is its look and it says so.
/// </para>
/// </summary>
internal sealed class StoreSignal
{
    private readonly Lock gate = new();
    private readonly List<CellIsland> islands = [];
    private readonly Action raise;

    // The queue of the thread that looked first — the dock's, which pumps it — and whether any
    // thread has looked. Both under gate: Notify reads them from whatever thread has news.
    private Windows.System.DispatcherQueue? queue;
    private bool looked;
    private int news;
    private int told;

    public StoreSignal(Action raise) => this.raise = raise;

    /// <summary>
    /// On the thread that looks at the store — the dock's. The first call takes that thread's
    /// dispatcher queue, which is where news is raised when no island is there to take it; later
    /// calls, from any thread, change nothing.
    /// <para>
    /// The first look wins even on a thread with no queue at all. A later look from another thread
    /// would be a second thread using the store, and raising its news there would be raising it
    /// for somebody else. News that came before the first look is kept, not lost: the look raises
    /// it — through the queue, so not in the middle of whatever the looker is doing.
    /// </para>
    /// </summary>
    public void Look()
    {
        Windows.System.DispatcherQueue? taken;
        lock (gate)
        {
            if (looked)
            {
                return;
            }
            looked = true;
            taken = queue = Windows.System.DispatcherQueue.GetForCurrentThread();
        }
        // On the looking thread, which is the only one that writes `told`.
        if (Volatile.Read(ref news) != told)
        {
            Later(taken);
        }
    }

    /// <summary>An island that shows the store: woken when there is news. On the dock's thread, so a look.</summary>
    public void Add(CellIsland island)
    {
        Look();
        lock (gate)
        {
            if (!islands.Contains(island))
            {
                islands.Add(island);
            }
        }
    }

    public void Remove(CellIsland island)
    {
        lock (gate)
        {
            islands.Remove(island);
        }
    }

    /// <summary>Whether any island shows the store now: a store that reads on a clock stops when none does.</summary>
    public bool Watched
    {
        get
        {
            lock (gate)
            {
                return islands.Count > 0;
            }
        }
    }

    /// <summary>From any thread: something changed. Never a look — it is the one call that is not on the dock's thread.</summary>
    public void Notify()
    {
        Interlocked.Increment(ref news);
        CellIsland[] targets;
        Windows.System.DispatcherQueue? queue;
        lock (gate)
        {
            targets = [.. islands];
            // Null until the first look: then there is no thread yet that it would be right to
            // raise this on, and the news waits, counted, for the look.
            queue = this.queue;
        }
        var posted = false;
        foreach (var island in targets)
        {
            posted |= island.Wake();
        }
        if (!posted)
        {
            Later(queue);
        }
    }

    /// <summary>A <see cref="Deliver"/> on <paramref name="queue"/>'s thread, when it next pumps; nothing without one.</summary>
    private void Later(Windows.System.DispatcherQueue? queue) =>
        _ = queue?.TryEnqueue(() =>
        {
            try
            {
                Deliver();
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                Console.Error.WriteLine($"Karpo: a store's Changed handler failed: {error.Message}");
            }
        });

    /// <summary>On the dock thread: raises Changed once for all the news since the last time. A look.</summary>
    public void Deliver()
    {
        Look();
        var now = Volatile.Read(ref news);
        if (now == told)
        {
            return;
        }
        told = now;
        raise();
    }
}

/// <summary>
/// The files islands keep in <see cref="Karpo.Core.Config.LocalFolder"/>, read and written the durable
/// way (<see cref="Karpo.Core.DurableFile"/>): through to the disk and swapped in, the version before
/// kept as name.bak. A file that is empty or a run of NULs (a write the disk never finished) is the
/// version before it. A file another program held (a virus scanner, a backup) every time its read was
/// tried reads as nothing for the store, which starts empty — and is not written over until it has
/// read again, so what it holds is not lost to a store that never saw it.
/// </summary>
internal static class IslandFiles
{
    private static readonly Lock Writing = new();

    /// <summary>Under <see cref="Writing"/>: the files held by another program at their last read, which nothing writes over.</summary>
    private static readonly HashSet<string> Held = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Under <see cref="Writing"/>: the files that were empty at their last read, of which the next write keeps no backup.</summary>
    private static readonly HashSet<string> Blank = new(StringComparer.OrdinalIgnoreCase);

    public static string PathOf(string name) => Path.Combine(Karpo.Core.Config.LocalFolder, name);

    /// <summary>What the file says; null where there is none, nothing in it or in the version before it, or another program holds it.</summary>
    public static string? Read(string name)
    {
        var path = PathOf(name);
        var read = Karpo.Core.DurableFile.ReadText(path, out var text);
        lock (Writing)
        {
            if (read == Karpo.Core.FileRead.Locked)
            {
                Held.Add(name);
                Console.Error.WriteLine($"Karpo: {name} held by another program; not read, and not written over");
                return null;
            }
            Held.Remove(name);
            if (text is null || !Empty(text))
            {
                Blank.Remove(name);
                return text;
            }
            Blank.Add(name);
        }
        return Karpo.Core.DurableFile.ReadText(Karpo.Core.DurableFile.BackupOf(path), out var before) == Karpo.Core.FileRead.Ok && !Empty(before!)
            ? before
            : null;
    }

    /// <summary>
    /// The file says <paramref name="text"/> from now on; nothing happens to one held by another
    /// program at its last read. One write at a time: stores write from their own threads.
    /// </summary>
    public static void Write(string name, string text)
    {
        lock (Writing)
        {
            if (Held.Contains(name))
            {
                return;
            }
            try
            {
                // An empty file never becomes the backup: the good version before it stays.
                Karpo.Core.DurableFile.Write(PathOf(name), text, keepBackup: !Blank.Remove(name));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                Console.Error.WriteLine($"Karpo: {name} not saved: {error.Message}");
            }
        }
    }

    /// <summary>Nothing but white space, a byte order mark or NULs.</summary>
    private static bool Empty(string text) => text.AsSpan().Trim("﻿ \t\r\n\0").IsEmpty;
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    NumberHandling = JsonNumberHandling.AllowReadingFromString)]
[JsonSerializable(typeof(Forecast))]
[JsonSerializable(typeof(WeatherCache))]
[JsonSerializable(typeof(WeatherFile))]
[JsonSerializable(typeof(AirAnswer))]
[JsonSerializable(typeof(AirFile))]
[JsonSerializable(typeof(TideAnswer))]
[JsonSerializable(typeof(TideFile))]
[JsonSerializable(typeof(Sports.SportsFile))]
[JsonSerializable(typeof(QuakeFeedAnswer))]
[JsonSerializable(typeof(QuakeFile))]
[JsonSerializable(typeof(NotificationsFile))]
internal sealed partial class IslandJson : JsonSerializerContext;

/// <summary>
/// Dates and times in the user's own format and language, as the taskbar
/// clock shows them. Formatted by Windows: the dock runs with
/// InvariantGlobalization, and the user's settings live in the locale Windows reads.
/// </summary>
internal static unsafe class Dates
{
    // winnls.h, not part of TerraFX.
    private const uint TIME_NOSECONDS = 0x00000002;
    private const uint DATE_LONGDATE = 0x00000002;

    /// <summary>
    /// "10:30", or "10:30 AM": the time pattern someone chose in Karpo's localization settings,
    /// else the user's own. Asked of Nls rather than answered here — a second answer is how half an
    /// app ends up following a setting and half not.
    /// </summary>
    public static string Time(DateTime local) => Panels.Nls.TimeOfDay(local, seconds: false) ?? local.ToString("HH:mm");

    /// <summary>The long date, the same way: the chosen pattern where there is one, else the region's.</summary>
    public static string Long(DateTime local) => Panels.Nls.LongDate(DateOnly.FromDateTime(local));

    /// <summary>A date picture ("ddd", "d", "dddd d MMMM") in the user's language.</summary>
    public static string Format(DateTime local, string picture)
    {
        var at = Systemtime(local);
        var text = stackalloc char[128];
        int length;
        fixed (char* format = picture)
        {
            length = GetDateFormatEx(null, 0, &at, format, text, 128, null);
        }
        return length > 1 ? new string(text, 0, length - 1) : local.ToString(picture);
    }

    private static SYSTEMTIME Systemtime(DateTime local) => new()
    {
        wYear = (ushort)local.Year,
        wMonth = (ushort)local.Month,
        wDay = (ushort)local.Day,
        wDayOfWeek = (ushort)local.DayOfWeek,
        wHour = (ushort)local.Hour,
        wMinute = (ushort)local.Minute,
        wSecond = (ushort)local.Second,
    };

    [DllImport("kernel32", ExactSpelling = true)]
    private static extern int GetTimeFormatEx(char* lpLocaleName, uint dwFlags, SYSTEMTIME* lpTime, char* lpFormat, char* lpTimeStr, int cchTime);

    [DllImport("kernel32", ExactSpelling = true)]
    private static extern int GetDateFormatEx(char* lpLocaleName, uint dwFlags, SYSTEMTIME* lpDate, char* lpFormat, char* lpDateStr, int cchDate, char* lpCalendar);
}

/// <summary>
/// Time zones by the names calendars use. .NET maps IANA names ("Europe/Berlin")
/// through ICU, which InvariantGlobalization switches off; Windows' own ICU
/// (icu.dll, Windows 10 1903 and later) does the mapping here instead.
/// </summary>
internal static unsafe class Zones
{
    private static readonly Dictionary<string, TimeZoneInfo?> Known = new(StringComparer.OrdinalIgnoreCase);
    private static bool noIcu;

    /// <summary>The zone for a TZID, or null when neither Windows nor ICU knows it.</summary>
    public static TimeZoneInfo? Find(string id)
    {
        lock (Known)
        {
            if (Known.TryGetValue(id, out var cached))
            {
                return cached;
            }
            var zone = Lookup(id) ?? Lookup(FromIana(id));
            // Some producers prefix the IANA name ("/mozilla.org/20050126_1/Europe/Berlin").
            if (zone is null && id.Split('/', StringSplitOptions.RemoveEmptyEntries) is { Length: > 2 } parts)
            {
                zone = Lookup(FromIana($"{parts[^2]}/{parts[^1]}"));
            }
            Known[id] = zone;
            return zone;
        }
    }

    private static TimeZoneInfo? Lookup(string? id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception error) when (error is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return null;
        }
    }

    private static string? FromIana(string iana)
    {
        if (noIcu)
        {
            return null;
        }
        try
        {
            var buffer = stackalloc char[128];
            int status = 0;
            int length;
            fixed (char* name = iana)
            {
                length = ucal_getWindowsTimeZoneID(name, iana.Length, buffer, 128, &status);
            }
            return status <= 0 && length is > 0 and <= 128 ? new string(buffer, 0, length) : null;
        }
        catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException)
        {
            noIcu = true;
            return null;
        }
    }

    [DllImport("icu.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int ucal_getWindowsTimeZoneID(char* id, int length, char* windowsId, int capacity, int* status);
}
