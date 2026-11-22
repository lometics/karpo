// The window list's previews, asked of DWM off the dock's thread.
//
// DwmRegisterThumbnail waits for DWM to present its next frame, and so, now and then, does asking a
// thumbnail's size. On a desktop DWM is showing that is a frame, 16 ms; where DWM is slow to get
// round to it — a desktop it is not showing, as the smoke's is, or a compositor with a lot on — it
// was seen to be seconds, and once three minutes, for the one window the smoke lists, and the same
// before the thumbnail toolbar came as after. The window list registered every window's preview in
// turn, on the dock's thread, before Show returned: the list waited on DWM once per window, and the
// whole bar with it.
//
// So a showing lays its cards out and shows at once, and hands its previews to a batch, which asks
// DWM for them one after the other on a thread of its own and places each as its size is known. A
// preview arrives a frame after its card, where DWM keeps up. Letting the showing go lets the batch
// go: what it has registered is unregistered, also off the dock's thread, and whatever it registers
// after that it unregisters itself, so a list shown again and again leaves nothing registered behind.

using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

/// <summary>DWM's thumbnail calls, behind an interface so a test can stand in for a slow compositor.</summary>
public interface IDwmThumbnails
{
    /// <summary>DwmRegisterThumbnail: the thumbnail's handle, or 0 where DWM refused.</summary>
    nint Register(HWND destination, HWND source);

    /// <summary>DwmQueryThumbnailSourceSize; empty where DWM could not say.</summary>
    SIZE SourceSize(nint thumbnail);

    /// <summary>DwmUpdateThumbnailProperties.</summary>
    void Update(nint thumbnail, DWM_THUMBNAIL_PROPERTIES properties);

    /// <summary>DwmUnregisterThumbnail.</summary>
    void Unregister(nint thumbnail);
}

/// <summary>DWM itself.</summary>
internal sealed unsafe class DwmThumbnails : IDwmThumbnails
{
    public static readonly DwmThumbnails Instance = new();

    private DwmThumbnails()
    {
    }

    public nint Register(HWND destination, HWND source)
    {
        HTHUMBNAIL thumbnail;
        return DwmRegisterThumbnail(destination, source, &thumbnail).SUCCEEDED ? (nint)thumbnail : 0;
    }

    public SIZE SourceSize(nint thumbnail)
    {
        SIZE size = default;
        return DwmQueryThumbnailSourceSize((HTHUMBNAIL)thumbnail, &size).SUCCEEDED ? size : default;
    }

    public void Update(nint thumbnail, DWM_THUMBNAIL_PROPERTIES properties) =>
        _ = DwmUpdateThumbnailProperties((HTHUMBNAIL)thumbnail, &properties);

    public void Unregister(nint thumbnail) => _ = DwmUnregisterThumbnail((HTHUMBNAIL)thumbnail);
}

/// <summary>
/// One showing's previews (see the top of the file): registered in order off the dock's thread,
/// each placed by <see cref="Request.Place"/> once its source's size is known, and all let go by
/// <see cref="Release"/>.
/// </summary>
internal sealed class ThumbnailBatch
{
    /// <summary>A window's preview: its source, and where it goes for a source of a given size.</summary>
    /// <param name="Place">Called off the dock's thread with the source's size: must touch nothing of the dock's.</param>
    public readonly record struct Request(HWND Source, Func<SIZE, DWM_THUMBNAIL_PROPERTIES> Place);

    private readonly Lock gate = new();
    private readonly IDwmThumbnails dwm;
    private readonly List<nint> made = [];
    private bool released;
    private long dwmTicks;
    private int registered;

    public ThumbnailBatch(IDwmThumbnails dwm, HWND destination, IReadOnlyList<Request> requests)
    {
        this.dwm = dwm;
        Done = requests.Count == 0 ? Task.CompletedTask : Task.Run(() => Run(destination, requests));
    }

    /// <summary>Finishes once every preview was asked for, or the batch was let go.</summary>
    public Task Done { get; }

    /// <summary>How long the batch has spent in DWM's calls so far, in ms.</summary>
    public double DwmMs => Interlocked.Read(ref dwmTicks) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;

    /// <summary>How many previews DWM registered, of the ones asked for.</summary>
    public int Registered => Volatile.Read(ref registered);

    /// <summary>The previews go: what was registered is unregistered off the dock's thread, and the rest never will be.</summary>
    public void Release()
    {
        nint[] registered;
        lock (gate)
        {
            if (released)
            {
                return;
            }
            released = true;
            registered = [.. made];
            made.Clear();
        }
        if (registered.Length > 0)
        {
            _ = Task.Run(() =>
            {
                foreach (var thumbnail in registered)
                {
                    Guarded(() => dwm.Unregister(thumbnail));
                }
            });
        }
    }

    private void Run(HWND destination, IReadOnlyList<Request> requests)
    {
        foreach (var request in requests)
        {
            if (Volatile.Read(ref released))
            {
                return;
            }
            var started = System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                var thumbnail = dwm.Register(destination, request.Source);
                if (thumbnail == 0)
                {
                    continue;
                }
                lock (gate)
                {
                    if (released)
                    {
                        // Let go while DWM was answering: nobody else will unregister it.
                        Guarded(() => dwm.Unregister(thumbnail));
                        return;
                    }
                    made.Add(thumbnail);
                }
                Interlocked.Increment(ref registered);
                var properties = request.Place(dwm.SourceSize(thumbnail));
                dwm.Update(thumbnail, properties);
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                // One preview that failed costs that preview, not the others.
                Console.Error.WriteLine($"Karpo: a window's preview not shown: {error.GetType().Name}");
            }
            finally
            {
                _ = Interlocked.Add(ref dwmTicks, System.Diagnostics.Stopwatch.GetTimestamp() - started);
            }
        }
    }

    private static void Guarded(Action call)
    {
        try
        {
            call();
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Console.Error.WriteLine($"Karpo: a preview not let go: {error.GetType().Name}");
        }
    }
}
