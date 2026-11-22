// sun: where a sun widget is (docs\widgets.md §368). Its place is a weather widget's (WeatherOptions:
// a place picked from the weather's own search, coordinates typed into it, or this PC's location), and
// nothing is fetched for it: a place picked or typed is its coordinates. This PC's location is asked of
// Windows the way the weather asks it (WeatherStore.Where, which also names the position, §253) — once,
// not for each widget, and not again for a quarter of an hour where Windows gave none — and taken from
// what the weather already knows where a weather widget asked first (WeatherStore.DeviceAt). The lines
// and folders waiting for it are woken when it comes (StoreSignal), not on a clock.

using Karpo.Core;

namespace Karpo.Dock;

/// <summary>Where a sun widget's place is: its coordinates, this PC's position, or not known yet.</summary>
internal static class SunPlaces
{
    /// <summary>How soon this PC's position is asked again after Windows gave none.</summary>
    internal static readonly TimeSpan Retry = TimeSpan.FromMinutes(15);

    private static readonly Lock gate = new();
    private static readonly StoreSignal signal = new(() => { });
    private static Task? asking;
    private static DateTime failedUtc = DateTime.MinValue;
    private static Tuple<double, double>? located;

    /// <summary>
    /// Tests only, set before anything asks: where this PC's position comes from instead of Windows
    /// (WeatherStore.Where), so no test asks Windows where it is.
    /// </summary>
    internal static Func<CancellationToken, Task<(double Latitude, double Longitude)?>>? SharedLocate { get; set; }

    /// <summary>
    /// Where <paramref name="options"/> is: its coordinates; for this PC's location the position
    /// Windows last gave, asked for now where none is known (and <paramref name="waiting"/> woken when
    /// it comes); else the place kept beside the switch while Windows gives none; null where nothing is known.
    /// </summary>
    public static (double Latitude, double Longitude)? Where(WeatherOptions? options, CellIsland? waiting = null)
    {
        if (options is null || !options.IsSet)
        {
            return null;
        }
        if (!options.UseDeviceLocation)
        {
            return (options.Latitude!.Value, options.Longitude!.Value);
        }
        if (Device is { } here)
        {
            return here;
        }
        Ask(waiting);
        return Unlocated && options is { Latitude: { } latitude, Longitude: { } longitude } ? (latitude, longitude) : null;
    }

    /// <summary>Whether this PC's location is wanted and Windows gave none, with no place kept beside the switch.</summary>
    public static bool Off(WeatherOptions? options) =>
        options is { UseDeviceLocation: true } && Device is null && Unlocated && options is not { Latitude: not null, Longitude: not null };

    /// <summary>Whether Windows gave no position the last time the weather or the sun asked (WeatherStore.Where says so to PlaceNames).</summary>
    private static bool Unlocated
    {
        get
        {
            lock (gate)
            {
                return PlaceNames.DeviceOff || failedUtc != DateTime.MinValue;
            }
        }
    }

    /// <summary>This PC's position as the weather last had it from Windows, else as the sun last did; null before either asked.</summary>
    private static (double Latitude, double Longitude)? Device =>
        WeatherStore.DeviceAt ?? (Volatile.Read(ref located) is { } at ? (at.Item1, at.Item2) : null);

    /// <summary>Tests: nothing known or asked about this PC's position.</summary>
    internal static void Forget()
    {
        lock (gate)
        {
            Volatile.Write(ref located, null);
            failedUtc = DateTime.MinValue;
            asking = null;
        }
    }

    /// <summary><paramref name="island"/> no longer waits for this PC's position.</summary>
    public static void Release(CellIsland island) => signal.Remove(island);

    /// <summary>Asks Windows for this PC's position, once at a time and not again soon after it gave none.</summary>
    private static void Ask(CellIsland? waiting)
    {
        if (waiting is not null)
        {
            signal.Add(waiting);
        }
        lock (gate)
        {
            if (asking is { IsCompleted: false } || Clock.RealUtc() - failedUtc < Retry)
            {
                return;
            }
            asking = Task.Run(Locate);
        }
    }

    private static async Task Locate()
    {
        (double, double)? found = null;
        try
        {
            using var stop = new CancellationTokenSource(TimeSpan.FromMinutes(1));
            found = SharedLocate is { } locate
                ? await locate(stop.Token).ConfigureAwait(false)
                : await WeatherStore.Where(new WeatherStore.Request(null, null, true), stop.Token).ConfigureAwait(false);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Console.Error.WriteLine($"Karpo: the sun's place not found: {error.GetType().Name}");
        }
        if (found is (var latitude, var longitude))
        {
            Volatile.Write(ref located, Tuple.Create(latitude, longitude));
        }
        else
        {
            lock (gate)
            {
                failedUtc = Clock.RealUtc();
            }
        }
        signal.Notify();
    }
}
