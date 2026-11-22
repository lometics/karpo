// COM callback objects for the quick-settings services: the audio device list
// (IMMNotificationClient), a device's volume (IAudioEndpointVolumeCallback), new
// audio sessions (IAudioSessionNotification) and a session's own changes
// (IAudioSessionEvents). Built by hand as SystemStatus's are, a table of
// unmanaged function pointers over native memory, so native AOT needs no COM
// interop. Every method only says "something changed" to the Changes it was made
// for, found by number: a callback that comes after its service has gone finds
// nothing and does nothing.

using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
using static TerraFX.Interop.Windows.S;

namespace Karpo.Dock.QuickSettings.Services;

/// <summary>
/// What a service has heard since its page last looked, as bits; the first bit set posts one
/// message to the page, so a burst (a slider dragged in another app) is read once.
/// </summary>
internal sealed class Changes
{
    private readonly Action<int> post;
    private int pending;

    public Changes(Action<int> post)
    {
        this.post = post;
    }

    /// <summary>From any thread.</summary>
    public void Add(int what)
    {
        if (Interlocked.Or(ref pending, what) == 0)
        {
            post(what);
        }
    }

    /// <summary>On the dock's thread: everything heard, and a clean slate.</summary>
    public int Take() => Interlocked.Exchange(ref pending, 0);
}

internal unsafe struct QuickSink
{
    // The Changes each sink reports to, by the number it carries.
    private static readonly ConcurrentDictionary<int, (Changes Changes, int What)> Targets = new();
    private static int nextNumber;

    public static readonly void** DeviceTable = Build(new void*[]
    {
        (void*)(delegate* unmanaged<QuickSink*, Guid*, void**, int>)&QueryInterface,
        (void*)(delegate* unmanaged<QuickSink*, uint>)&AddRef,
        (void*)(delegate* unmanaged<QuickSink*, uint>)&Release,
        (void*)(delegate* unmanaged<QuickSink*, char*, uint, int>)&DeviceStateChanged,
        (void*)(delegate* unmanaged<QuickSink*, char*, int>)&DeviceAdded,
        (void*)(delegate* unmanaged<QuickSink*, char*, int>)&DeviceRemoved,
        (void*)(delegate* unmanaged<QuickSink*, EDataFlow, ERole, char*, int>)&DefaultDeviceChanged,
        (void*)(delegate* unmanaged<QuickSink*, char*, PROPERTYKEY, int>)&PropertyValueChanged,
    });

    public static readonly void** VolumeTable = Build(new void*[]
    {
        (void*)(delegate* unmanaged<QuickSink*, Guid*, void**, int>)&QueryInterface,
        (void*)(delegate* unmanaged<QuickSink*, uint>)&AddRef,
        (void*)(delegate* unmanaged<QuickSink*, uint>)&Release,
        (void*)(delegate* unmanaged<QuickSink*, void*, int>)&NotifiedPointer,
    });

    public static readonly void** SessionCreatedTable = Build(new void*[]
    {
        (void*)(delegate* unmanaged<QuickSink*, Guid*, void**, int>)&QueryInterface,
        (void*)(delegate* unmanaged<QuickSink*, uint>)&AddRef,
        (void*)(delegate* unmanaged<QuickSink*, uint>)&Release,
        (void*)(delegate* unmanaged<QuickSink*, void*, int>)&NotifiedPointer,
    });

    public static readonly void** SessionEventsTable = Build(new void*[]
    {
        (void*)(delegate* unmanaged<QuickSink*, Guid*, void**, int>)&QueryInterface,
        (void*)(delegate* unmanaged<QuickSink*, uint>)&AddRef,
        (void*)(delegate* unmanaged<QuickSink*, uint>)&Release,
        (void*)(delegate* unmanaged<QuickSink*, char*, Guid*, int>)&TextChanged,
        (void*)(delegate* unmanaged<QuickSink*, char*, Guid*, int>)&TextChanged,
        (void*)(delegate* unmanaged<QuickSink*, float, BOOL, Guid*, int>)&SimpleVolumeChanged,
        (void*)(delegate* unmanaged<QuickSink*, uint, float*, uint, Guid*, int>)&ChannelVolumeChanged,
        (void*)(delegate* unmanaged<QuickSink*, Guid*, Guid*, int>)&GroupingChanged,
        (void*)(delegate* unmanaged<QuickSink*, int, int>)&NotifiedValue,
        (void*)(delegate* unmanaged<QuickSink*, int, int>)&NotifiedValue,
    });

    public void** Table;
    public int References;
    public int Number;
    public Guid Interface;

    /// <summary>A new sink for <paramref name="iid"/>, holding the caller's reference, reporting <paramref name="what"/> to <paramref name="changes"/>.</summary>
    public static QuickSink* Create(void** table, Guid iid, Changes changes, int what)
    {
        var number = Interlocked.Increment(ref nextNumber);
        Targets[number] = (changes, what);
        var sink = (QuickSink*)NativeMemory.AllocZeroed((nuint)sizeof(QuickSink));
        sink->Table = table;
        sink->References = 1;
        sink->Number = number;
        sink->Interface = iid;
        return sink;
    }

    /// <summary>Stops the sink's reports and gives up the caller's reference; COM may hold it a moment longer.</summary>
    public static void Drop(ref QuickSink* sink)
    {
        if (sink == null)
        {
            return;
        }
        Targets.TryRemove(sink->Number, out _);
        Decrement(sink);
        sink = null;
    }

    /// <summary>Another reference, for work that uses the sink on another thread.</summary>
    public static void Hold(QuickSink* sink) => Interlocked.Increment(ref sink->References);

    /// <summary>Gives up a reference taken with <see cref="Hold"/>.</summary>
    public static void Let(QuickSink* sink) => Decrement(sink);

    private static void** Build(void*[] entries)
    {
        var table = (void**)NativeMemory.Alloc((nuint)(entries.Length * sizeof(void*)));
        for (var i = 0; i < entries.Length; i++)
        {
            table[i] = entries[i];
        }
        return table;
    }

    private static void Report(QuickSink* self)
    {
        if (Targets.TryGetValue(self->Number, out var target))
        {
            target.Changes.Add(target.What);
        }
    }

    private static uint Decrement(QuickSink* self)
    {
        var left = Interlocked.Decrement(ref self->References);
        if (left == 0)
        {
            NativeMemory.Free(self);
        }
        return (uint)left;
    }

    [UnmanagedCallersOnly]
    private static int QueryInterface(QuickSink* self, Guid* iid, void** result)
    {
        if (result == null)
        {
            return E.E_POINTER;
        }
        if (iid != null && (*iid == IID.IID_IUnknown || *iid == self->Interface))
        {
            Interlocked.Increment(ref self->References);
            *result = self;
            return S_OK;
        }
        *result = null;
        return E.E_NOINTERFACE;
    }

    [UnmanagedCallersOnly]
    private static uint AddRef(QuickSink* self) => (uint)Interlocked.Increment(ref self->References);

    [UnmanagedCallersOnly]
    private static uint Release(QuickSink* self) => Decrement(self);

    [UnmanagedCallersOnly]
    private static int DeviceStateChanged(QuickSink* self, char* id, uint state)
    {
        try
        {
            Report(self);
            return S_OK;
        }
        catch (Exception error)
        {
            Karpo.Core.Troubles.Report("Quick settings: Windows telling of a change", error);
            return S_OK;
        }
    }

    [UnmanagedCallersOnly]
    private static int DeviceAdded(QuickSink* self, char* id)
    {
        try
        {
            Report(self);
            return S_OK;
        }
        catch (Exception error)
        {
            Karpo.Core.Troubles.Report("Quick settings: Windows telling of a change", error);
            return S_OK;
        }
    }

    [UnmanagedCallersOnly]
    private static int DeviceRemoved(QuickSink* self, char* id)
    {
        try
        {
            Report(self);
            return S_OK;
        }
        catch (Exception error)
        {
            Karpo.Core.Troubles.Report("Quick settings: Windows telling of a change", error);
            return S_OK;
        }
    }

    [UnmanagedCallersOnly]
    private static int DefaultDeviceChanged(QuickSink* self, EDataFlow flow, ERole role, char* id)
    {
        try
        {
            if (flow == EDataFlow.eRender)
            {
                Report(self);
            }
            return S_OK;
        }
        catch (Exception error)
        {
            Karpo.Core.Troubles.Report("Quick settings: Windows telling of a change", error);
            return S_OK;
        }
    }

    // Every property of every device reports here, many times a second at times; a name
    // change shows the next time the list is read anyway.
    [UnmanagedCallersOnly]
    private static int PropertyValueChanged(QuickSink* self, char* id, PROPERTYKEY key) => S_OK;

    [UnmanagedCallersOnly]
    private static int NotifiedPointer(QuickSink* self, void* data)
    {
        try
        {
            Report(self);
            return S_OK;
        }
        catch (Exception error)
        {
            Karpo.Core.Troubles.Report("Quick settings: Windows telling of a change", error);
            return S_OK;
        }
    }

    [UnmanagedCallersOnly]
    private static int NotifiedValue(QuickSink* self, int value)
    {
        try
        {
            Report(self);
            return S_OK;
        }
        catch (Exception error)
        {
            Karpo.Core.Troubles.Report("Quick settings: Windows telling of a change", error);
            return S_OK;
        }
    }

    [UnmanagedCallersOnly]
    private static int TextChanged(QuickSink* self, char* text, Guid* context)
    {
        try
        {
            Report(self);
            return S_OK;
        }
        catch (Exception error)
        {
            Karpo.Core.Troubles.Report("Quick settings: Windows telling of a change", error);
            return S_OK;
        }
    }

    [UnmanagedCallersOnly]
    private static int SimpleVolumeChanged(QuickSink* self, float volume, BOOL muted, Guid* context)
    {
        try
        {
            Report(self);
            return S_OK;
        }
        catch (Exception error)
        {
            Karpo.Core.Troubles.Report("Quick settings: Windows telling of a change", error);
            return S_OK;
        }
    }

    [UnmanagedCallersOnly]
    private static int ChannelVolumeChanged(QuickSink* self, uint count, float* volumes, uint changed, Guid* context) => S_OK;

    [UnmanagedCallersOnly]
    private static int GroupingChanged(QuickSink* self, Guid* grouping, Guid* context) => S_OK;
}
