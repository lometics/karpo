// How busy the graphics adapters are, read the way Task Manager reads it.
//
// The kernel keeps a running-time counter for every engine ("node") of every
// adapter. The share of wall time an engine spent running is its utilisation,
// and an adapter is as busy as its busiest engine. The D3DKMT calls are gdi32
// exports that go straight to the kernel: nothing of the driver is loaded
// into this process for them, unlike for a Direct3D device (see Gfx), and no
// performance-counter query is set up, which would cost megabytes.
//
// Reading one engine is not cheap: 150 µs to 850 µs each, measured on an
// Intel UHD 770 and an RTX 4060 Ti, 5.5 ms for all 22 of them. So only the 3D
// engines and the engines that were busy at their last reading are read on
// every sample; the idle rest is looked at every fourth sample, which is how
// late a video decoder that starts is noticed.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.DirectX.DirectX;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

internal sealed unsafe class GpuSampler
{
    /// <summary>Every how many samples the idle engines are read too.</summary>
    private const int FullEvery = 4;

    /// <summary>
    /// Below this share an engine counts as idle: a trickle of work cannot be
    /// the busiest engine's, and reading it every time would cost more than it tells.
    /// </summary>
    private const float BusyShare = 0.005f;

    public struct Engine
    {
        /// <summary>DXGK_ENGINE_TYPE; -1 when the driver did not say.</summary>
        public int Type;

        /// <summary>Read on every sample, busy or not.</summary>
        public bool Always;

        /// <summary>More than a trickle of work at its last reading, so read on the next sample too.</summary>
        public bool Busy;

        /// <summary>Running time at the last reading, in 100 ns.</summary>
        public long Running;

        /// <summary>When it was last read (a Stopwatch timestamp); 0 = never.</summary>
        public long Stamp;

        /// <summary>0 to 1 over the span between its last two readings; -1 before that.</summary>
        public float Usage;
    }

    public sealed class Adapter
    {
        public required LUID Luid { get; init; }
        public required string Name { get; init; }
        public required Engine[] Engines { get; init; }

        /// <summary>Its own video memory, in bytes, as DXGI says; 0 where DXGI did not answer.</summary>
        public ulong Memory { get; init; }

        /// <summary>Whether a display is attached to it, as DXGI lists its outputs.</summary>
        public bool Displays { get; init; }

        /// <summary>Its busiest engine, 0 to 1; -1 while not known.</summary>
        public float Usage { get; set; } = -1;
    }

    private int untilFull;

    /// <summary>Only real graphics adapters: no software renderer, no compute-only NPU.</summary>
    public List<Adapter> Adapters { get; } = [];

    /// <summary>The busiest engine of any adapter, 0 to 1; -1 while not known.</summary>
    public float Usage { get; private set; } = -1;

    /// <summary>The adapter a graphics widget follows where nobody named one (<see cref="MainOf"/>); -1 with none.</summary>
    public int MainIndex => MainOf([.. Adapters.Select(adapter => (adapter.Memory, adapter.Displays))]);

    /// <summary>
    /// Which of <paramref name="adapters"/> (in the sampler's order, which is DXGI's: the first drives
    /// the main display) is the PC's main graphics card, the one a graphics widget follows where
    /// nobody named one: the one with the most video memory of its own, which is the discrete card
    /// beside an integrated one (an integrated adapter has a token amount); between equals the one a
    /// display is attached to; then the first. -1 for none. Software adapters (Microsoft Basic
    /// Render Driver) and compute-only ones are never in the list (<see cref="Adapters"/>).
    /// Separated from the reading so that it can be tested.
    /// </summary>
    internal static int MainOf(IReadOnlyList<(ulong Memory, bool Displays)> adapters)
    {
        var best = -1;
        for (var index = 0; index < adapters.Count; index++)
        {
            if (best < 0
                || adapters[index].Memory > adapters[best].Memory
                || (adapters[index].Memory == adapters[best].Memory && adapters[index].Displays && !adapters[best].Displays))
            {
                best = index;
            }
        }
        return best;
    }

    /// <summary>
    /// Which adapter a widget set to <paramref name="named"/> follows: the one of that name
    /// (<c>name</c> makes it, "GPU 0: …"), or the main one (<see cref="MainIndex"/>) for "". Null where
    /// the one named is not attached now, or there is none.
    /// </summary>
    public int? Followed(string named, Func<int, string, string> name)
    {
        if (named.Length == 0)
        {
            return MainIndex is >= 0 and var main ? main : null;
        }
        for (var index = 0; index < Adapters.Count; index++)
        {
            if (string.Equals(name(index, Adapters[index].Name), named, StringComparison.Ordinal))
            {
                return index;
            }
        }
        return null;
    }

    /// <summary>Nothing can be read: the calls failed, or there is no adapter.</summary>
    public bool Failed { get; private set; } = true;

    /// <summary>
    /// Finds the adapters and takes the first reading, which the next
    /// <see cref="Sample"/> measures against.
    /// </summary>
    public void Open()
    {
        Adapters.Clear();
        Usage = -1;
        Failed = D3dkmt.LayoutProblem() is not null || !Enumerate() || Adapters.Count == 0;
        untilFull = 0;
        if (!Failed)
        {
            Sample(Stopwatch.GetTimestamp());
        }
    }

    public void Sample(long now)
    {
        if (Failed)
        {
            Usage = -1;
            return;
        }
        bool full = untilFull <= 0;
        untilFull = full ? FullEvery - 1 : untilFull - 1;
        D3dkmt.D3DKMT_QUERYSTATISTICS query = default;
        float busiest = -1;
        foreach (var adapter in Adapters)
        {
            float adapterBusiest = -1;
            var engines = adapter.Engines;
            for (int node = 0; node < engines.Length; node++)
            {
                ref var engine = ref engines[node];
                if (full || engine.Always || engine.Busy)
                {
                    query.Type = D3dkmt.D3DKMT_QUERYSTATISTICS_NODE;
                    query.AdapterLuid = adapter.Luid;
                    query.Query.NodeId = (uint)node;
                    if (D3dkmt.D3DKMTQueryStatistics(&query) != D3dkmt.STATUS_SUCCESS)
                    {
                        // The adapter went away (driver update, eGPU unplugged).
                        // Refresh opens the new set.
                        Failed = true;
                        Usage = -1;
                        return;
                    }
                    var running = query.QueryResult.NodeInformation.GlobalInformation.RunningTime;
                    if (engine.Stamp != 0 && now > engine.Stamp)
                    {
                        // 100 ns, the unit of the running times.
                        var wall = (now - engine.Stamp) * 10_000_000.0 / Stopwatch.Frequency;
                        engine.Usage = Share(engine.Running, running, wall);
                        engine.Busy = engine.Usage >= BusyShare;
                    }
                    engine.Running = running;
                    engine.Stamp = now;
                }
                // An engine skipped this time was idle at its last reading,
                // so its last usage (a trickle at most) still stands.
                adapterBusiest = MathF.Max(adapterBusiest, engine.Usage);
            }
            adapter.Usage = adapterBusiest;
            busiest = MathF.Max(busiest, adapterBusiest);
        }
        Usage = busiest;
    }

    /// <summary>
    /// An engine's share of the wall time between two readings, 0 to 1, and <b>-1 where there is
    /// no baseline</b> — the first reading of an engine can only set one. <paramref name="span"/>
    /// is in 100 ns, the unit the running times are kept in.
    /// <para>
    /// Clamped both ways. At the top because an engine with several queues can report more running
    /// time than wall time, and because the counter and the clock are read at slightly different
    /// moments; at the bottom because a driver restart resets the counter, and a running time that
    /// went backwards is a spike to the ceiling if it is drawn as unsigned arithmetic.
    /// </para>
    /// <para>
    /// Separated from the reading so that it can be tested: the reading needs an adapter, and this
    /// needs three numbers.
    /// </para>
    /// </summary>
    internal static float Share(long runningBefore, long runningNow, double span) =>
        span <= 0 ? -1 : (float)Math.Clamp((runningNow - runningBefore) / span, 0, 1);

    private bool Enumerate()
    {
        D3dkmt.D3DKMT_ENUMADAPTERS2 request = default;
        if (D3dkmt.D3DKMTEnumAdapters2(&request) != D3dkmt.STATUS_SUCCESS)
        {
            return false;
        }
        // Adapters can arrive between the call that counts them and the call
        // that lists them; a few spare entries and a retry cover that.
        for (int attempt = 0; attempt < 3; attempt++)
        {
            var infos = new D3dkmt.D3DKMT_ADAPTERINFO[request.NumAdapters + 4];
            int status;
            fixed (D3dkmt.D3DKMT_ADAPTERINFO* list = infos)
            {
                request.NumAdapters = (uint)infos.Length;
                request.pAdapters = list;
                status = D3dkmt.D3DKMTEnumAdapters2(&request);
                request.pAdapters = null;
            }
            if (status == D3dkmt.STATUS_BUFFER_TOO_SMALL)
            {
                continue;
            }
            if (status != D3dkmt.STATUS_SUCCESS)
            {
                return false;
            }
            var count = (int)Math.Min(request.NumAdapters, (uint)infos.Length);
            try
            {
                var named = DxgiAdapters();
                for (int i = 0; i < count; i++)
                {
                    var luid = infos[i].AdapterLuid;
                    string name = "Graphics";
                    ulong memory = 0;
                    var displays = false;
                    if (named is not null)
                    {
                        // DXGI lists what renders: no NPU, and it marks the
                        // Microsoft Basic Render Driver as software.
                        var found = named.FindIndex(entry => Same(entry.Luid, luid));
                        if (found < 0 || named[found].Software)
                        {
                            continue;
                        }
                        (name, memory, displays) = (named[found].Name, named[found].Memory, named[found].Displays);
                    }
                    var nodes = NodeCount(luid);
                    if (nodes > 0)
                    {
                        Adapters.Add(new Adapter { Luid = luid, Name = name, Engines = Engines(infos[i].hAdapter, nodes), Memory = memory, Displays = displays });
                    }
                }
            }
            finally
            {
                // The statistics are asked for by LUID; the handles the
                // enumeration opened are only needed for the engine types.
                for (int i = 0; i < count; i++)
                {
                    var close = new D3dkmt.D3DKMT_CLOSEADAPTER { hAdapter = infos[i].hAdapter };
                    _ = D3dkmt.D3DKMTCloseAdapter(&close);
                }
            }
            return true;
        }
        return false;
    }

    private static int NodeCount(LUID luid)
    {
        var query = new D3dkmt.D3DKMT_QUERYSTATISTICS
        {
            Type = D3dkmt.D3DKMT_QUERYSTATISTICS_ADAPTER,
            AdapterLuid = luid,
        };
        if (D3dkmt.D3DKMTQueryStatistics(&query) != D3dkmt.STATUS_SUCCESS)
        {
            return 0;
        }
        // An adapter has a few dozen engines at most; anything bigger is a
        // misread, and would size an array from it.
        var nodes = query.QueryResult.AdapterInformation.NodeCount;
        return nodes <= 256 ? (int)nodes : 0;
    }

    /// <summary>
    /// The engines of an adapter, with their types where the driver reports them.
    /// Without any, the first engine, which is the 3D engine on every driver
    /// seen, is the one read every time.
    /// </summary>
    private static Engine[] Engines(uint handle, int nodes)
    {
        var engines = new Engine[nodes];
        bool typed = false;
        for (int node = 0; node < nodes; node++)
        {
            var metadata = new D3dkmt.D3DKMT_NODEMETADATA { NodeOrdinalAndAdapterIndex = (uint)node };
            var request = new D3dkmt.D3DKMT_QUERYADAPTERINFO
            {
                hAdapter = handle,
                Type = D3dkmt.KMTQAITYPE_NODEMETADATA,
                pPrivateDriverData = &metadata,
                PrivateDriverDataSize = (uint)sizeof(D3dkmt.D3DKMT_NODEMETADATA),
            };
            var known = D3dkmt.D3DKMTQueryAdapterInfo(&request) == D3dkmt.STATUS_SUCCESS;
            engines[node] = new Engine
            {
                Type = known ? metadata.NodeData.EngineType : -1,
                Always = known && metadata.NodeData.EngineType == D3dkmt.DXGK_ENGINE_TYPE_3D,
                Usage = -1,
            };
            typed |= engines[node].Always;
        }
        if (!typed)
        {
            engines[0].Always = true;
        }
        return engines;
    }

    /// <summary>The adapters DXGI knows, with their names as Task Manager shows them; null if DXGI failed.</summary>
    private static List<(LUID Luid, string Name, bool Software, ulong Memory, bool Displays)>? DxgiAdapters()
    {
        IDXGIFactory1* factory = null;
        if (CreateDXGIFactory1(__uuidof<IDXGIFactory1>(), (void**)&factory).FAILED)
        {
            return null;
        }
        var found = new List<(LUID, string, bool, ulong, bool)>();
        try
        {
            for (uint i = 0; ; i++)
            {
                IDXGIAdapter1* adapter = null;
                if (factory->EnumAdapters1(i, &adapter).FAILED)
                {
                    break;
                }
                DXGI_ADAPTER_DESC1 desc;
                var ok = adapter->GetDesc1(&desc).SUCCEEDED;
                // A display on it: its first output is enough to say so.
                IDXGIOutput* output = null;
                var displays = adapter->EnumOutputs(0, &output).SUCCEEDED;
                if (output is not null)
                {
                    output->Release();
                }
                adapter->Release();
                if (ok)
                {
                    var description = new ReadOnlySpan<char>((char*)&desc.Description, 128);
                    var end = description.IndexOf('\0');
                    var name = new string(end >= 0 ? description[..end] : description).Trim();
                    var software = (desc.Flags & (uint)DXGI_ADAPTER_FLAG.DXGI_ADAPTER_FLAG_SOFTWARE) != 0;
                    found.Add((desc.AdapterLuid, name.Length > 0 ? name : "Graphics", software, (ulong)desc.DedicatedVideoMemory, displays));
                }
            }
        }
        finally
        {
            factory->Release();
        }
        return found;
    }

    private static bool Same(LUID a, LUID b) => a.LowPart == b.LowPart && a.HighPart == b.HighPart;
}

/// <summary>
/// The D3DKMT declarations Karpo needs, from d3dkmthk.h (Windows SDK 10.0.26100,
/// shared\), laid out as on x64. TerraFX does not carry them. The result union
/// declares the two members Karpo reads; the header's static assertion fixes
/// the whole query at 0x328 bytes, and <see cref="LayoutProblem"/> checks it.
/// </summary>
internal static unsafe class D3dkmt
{
    public const int STATUS_SUCCESS = 0;
    public const int STATUS_BUFFER_TOO_SMALL = unchecked((int)0xC0000023);

    // D3DKMT_QUERYSTATISTICS_TYPE
    public const int D3DKMT_QUERYSTATISTICS_ADAPTER = 0;
    public const int D3DKMT_QUERYSTATISTICS_NODE = 5;

    // KMTQUERYADAPTERINFOTYPE
    public const int KMTQAITYPE_NODEMETADATA = 25;

    // DXGK_ENGINE_TYPE (d3dkmdt.h)
    public const int DXGK_ENGINE_TYPE_3D = 1;

    [DllImport("gdi32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    public static extern int D3DKMTEnumAdapters2(D3DKMT_ENUMADAPTERS2* adapters);

    [DllImport("gdi32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    public static extern int D3DKMTQueryAdapterInfo(D3DKMT_QUERYADAPTERINFO* info);

    [DllImport("gdi32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    public static extern int D3DKMTCloseAdapter(D3DKMT_CLOSEADAPTER* adapter);

    [DllImport("gdi32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    public static extern int D3DKMTQueryStatistics(D3DKMT_QUERYSTATISTICS* statistics);

    /// <summary>
    /// Null when these declarations have the sizes the header asserts or
    /// implies; otherwise what is wrong. A wrong size would let the kernel
    /// write past the end of the query, so sampling refuses to start.
    /// </summary>
    public static string? LayoutProblem()
    {
        D3DKMT_QUERYSTATISTICS probe = default;
        var result = (byte*)&probe.QueryResult - (byte*)&probe;
        var query = (byte*)&probe.Query - (byte*)&probe;
        (string Name, long Actual, long Expected)[] checks =
        [
            ("D3DKMT_ADAPTERINFO", sizeof(D3DKMT_ADAPTERINFO), 20),
            ("D3DKMT_ENUMADAPTERS2", sizeof(D3DKMT_ENUMADAPTERS2), 16),
            ("D3DKMT_QUERYADAPTERINFO", sizeof(D3DKMT_QUERYADAPTERINFO), 24),
            ("D3DKMT_NODEMETADATA", sizeof(D3DKMT_NODEMETADATA), 78),
            ("D3DKMT_QUERYSTATISTICS_ADAPTER_INFORMATION", sizeof(D3DKMT_QUERYSTATISTICS_ADAPTER_INFORMATION), 776),
            ("D3DKMT_QUERYSTATISTICS_PROCESS_NODE_INFORMATION", sizeof(D3DKMT_QUERYSTATISTICS_PROCESS_NODE_INFORMATION), 272),
            ("D3DKMT_QUERYSTATISTICS_NODE_INFORMATION", sizeof(D3DKMT_QUERYSTATISTICS_NODE_INFORMATION), 616),
            ("D3DKMT_QUERYSTATISTICS_RESULT", sizeof(D3DKMT_QUERYSTATISTICS_RESULT), 776),
            ("D3DKMT_QUERYSTATISTICS.QueryResult offset", result, 24), // not-ui: a layout check's name
            ("D3DKMT_QUERYSTATISTICS query union offset", query, 800), // not-ui: a layout check's name
            ("D3DKMT_QUERYSTATISTICS", sizeof(D3DKMT_QUERYSTATISTICS), 0x328),
        ];
        foreach (var (name, actual, expected) in checks)
        {
            if (actual != expected)
            {
                return $"{name} is {actual} bytes, expected {expected}";
            }
        }
        return null;
    }

    public struct D3DKMT_ADAPTERINFO
    {
        public uint hAdapter;
        public LUID AdapterLuid;
        public uint NumOfSources;
        public BOOL bPrecisePresentRegionsPreferred;
    }

    public struct D3DKMT_ENUMADAPTERS2
    {
        public uint NumAdapters;
        public D3DKMT_ADAPTERINFO* pAdapters;
    }

    public struct D3DKMT_CLOSEADAPTER
    {
        public uint hAdapter;
    }

    public struct D3DKMT_QUERYADAPTERINFO
    {
        public uint hAdapter;

        /// <summary>KMTQUERYADAPTERINFOTYPE.</summary>
        public int Type;

        public void* pPrivateDriverData;
        public uint PrivateDriverDataSize;
    }

    /// <summary>
    /// d3dkmdt.h, inside its <c>#pragma pack(push, 1)</c>: 78 bytes, and the
    /// kernel refuses any other size.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct D3DKMT_NODEMETADATA
    {
        /// <summary>WDDM 2: the high word is the physical adapter index, the low word the node ordinal.</summary>
        public uint NodeOrdinalAndAdapterIndex;

        public DXGK_NODEMETADATA NodeData;
    }

    /// <summary>d3dkmdt.h, packed like <see cref="D3DKMT_NODEMETADATA"/>.</summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct DXGK_NODEMETADATA
    {
        /// <summary>DXGK_ENGINE_TYPE.</summary>
        public int EngineType;

        /// <summary>DXGK_MAX_METADATA_NAME_LENGTH.</summary>
        public fixed char FriendlyName[32];

        /// <summary>DXGK_NODEMETADATA_FLAGS: a UINT32 of bit fields.</summary>
        public uint Flags;

        public byte GpuMmuSupported;
        public byte IoMmuSupported;
    }

    public struct D3DKMT_QUERYSTATISTICS
    {
        public int Type;
        public LUID AdapterLuid;
        public HANDLE hProcess;
        public D3DKMT_QUERYSTATISTICS_RESULT QueryResult;
        public D3DKMT_QUERYSTATISTICS_QUERY Query;
    }

    /// <summary>
    /// The anonymous union of the query inputs. Every member is one ULONG, or
    /// two UINT16 in the WDDM 3.1 variants.
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    public struct D3DKMT_QUERYSTATISTICS_QUERY
    {
        /// <summary>QuerySegment / QueryProcessSegment.SegmentId.</summary>
        [FieldOffset(0)] public uint SegmentId;

        /// <summary>QueryNode / QueryProcessNode.NodeId.</summary>
        [FieldOffset(0)] public uint NodeId;

        /// <summary>QueryVidPnSource / QueryProcessVidPnSource.VidPnSourceId.</summary>
        [FieldOffset(0)] public uint VidPnSourceId;

        /// <summary>QueryPhysAdapter.PhysicalAdapterIndex.</summary>
        [FieldOffset(0)] public uint PhysicalAdapterIndex;

        /// <summary>QueryNode2.PhysicalAdapterIndex (and the other WDDM 3.1 queries').</summary>
        [FieldOffset(0)] public ushort PhysicalAdapterIndex2;

        /// <summary>QueryNode2.NodeOrdinal, QuerySegment2.SegmentId.</summary>
        [FieldOffset(2)] public ushort NodeOrdinal2;
    }

    /// <summary>
    /// D3DKMT_QUERYSTATISTICS_RESULT. AdapterInformation (776 bytes) is the
    /// largest member of the header's union, so leaving out the members Karpo
    /// does not read leaves its size as it is.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 776)]
    public struct D3DKMT_QUERYSTATISTICS_RESULT
    {
        [FieldOffset(0)] public D3DKMT_QUERYSTATISTICS_ADAPTER_INFORMATION AdapterInformation;
        [FieldOffset(0)] public D3DKMT_QUERYSTATISTICS_NODE_INFORMATION NodeInformation;
    }

    public struct D3DKMT_QUERYSTATISTICS_COUNTER
    {
        public uint Count;
        public ulong Bytes;
    }

    public struct D3DKMT_QUERYSTATSTICS_REFERENCE_DMA_BUFFER
    {
        public uint NbCall;
        public uint NbAllocationsReferenced;
        public uint MaxNbAllocationsReferenced;
        public uint NbNULLReference;
        public uint NbWriteReference;
        public uint NbRenamedAllocationsReferenced;
        public uint NbIterationSearchingRenamedAllocation;
        public uint NbLockedAllocationReferenced;
        public uint NbAllocationWithValidPrepatchingInfoReferenced;
        public uint NbAllocationWithInvalidPrepatchingInfoReferenced;
        public uint NbDMABufferSuccessfullyPrePatched;
        public uint NbPrimariesReferencesOverflow;
        public uint NbAllocationWithNonPreferredResources;
        public uint NbAllocationInsertedInMigrationTable;
    }

    public struct D3DKMT_QUERYSTATSTICS_RENAMING
    {
        public uint NbAllocationsRenamed;
        public uint NbAllocationsShrinked;
        public uint NbRenamedBuffer;
        public uint MaxRenamingListLength;
        public uint NbFailuresDueToRenamingLimit;
        public uint NbFailuresDueToCreateAllocation;
        public uint NbFailuresDueToOpenAllocation;
        public uint NbFailuresDueToLowResource;
        public uint NbFailuresDueToNonRetiredLimit;
    }

    public struct D3DKMT_QUERYSTATSTICS_PREPRATION
    {
        public uint BroadcastStall;
        public uint NbDMAPrepared;
        public uint NbDMAPreparedLongPath;
        public uint ImmediateHighestPreparationPass;
        public D3DKMT_QUERYSTATISTICS_COUNTER AllocationsTrimmed;
    }

    public struct D3DKMT_QUERYSTATSTICS_PAGING_FAULT
    {
        public D3DKMT_QUERYSTATISTICS_COUNTER Faults;
        public D3DKMT_QUERYSTATISTICS_COUNTER FaultsFirstTimeAccess;
        public D3DKMT_QUERYSTATISTICS_COUNTER FaultsReclaimed;
        public D3DKMT_QUERYSTATISTICS_COUNTER FaultsMigration;
        public D3DKMT_QUERYSTATISTICS_COUNTER FaultsIncorrectResource;
        public D3DKMT_QUERYSTATISTICS_COUNTER FaultsLostContent;
        public D3DKMT_QUERYSTATISTICS_COUNTER FaultsEvicted;
        public D3DKMT_QUERYSTATISTICS_COUNTER AllocationsMEM_RESET;
        public D3DKMT_QUERYSTATISTICS_COUNTER AllocationsUnresetSuccess;
        public D3DKMT_QUERYSTATISTICS_COUNTER AllocationsUnresetFail;
        public uint AllocationsUnresetSuccessRead;
        public uint AllocationsUnresetFailRead;
        public D3DKMT_QUERYSTATISTICS_COUNTER Evictions;
        public D3DKMT_QUERYSTATISTICS_COUNTER EvictionsDueToPreparation;
        public D3DKMT_QUERYSTATISTICS_COUNTER EvictionsDueToLock;
        public D3DKMT_QUERYSTATISTICS_COUNTER EvictionsDueToClose;
        public D3DKMT_QUERYSTATISTICS_COUNTER EvictionsDueToPurge;
        public D3DKMT_QUERYSTATISTICS_COUNTER EvictionsDueToSuspendCPUAccess;
    }

    public struct D3DKMT_QUERYSTATSTICS_PAGING_TRANSFER
    {
        public ulong BytesFilled;
        public ulong BytesDiscarded;
        public ulong BytesMappedIntoAperture;
        public ulong BytesUnmappedFromAperture;
        public ulong BytesTransferredFromMdlToMemory;
        public ulong BytesTransferredFromMemoryToMdl;
        public ulong BytesTransferredFromApertureToMemory;
        public ulong BytesTransferredFromMemoryToAperture;
    }

    public struct D3DKMT_QUERYSTATSTICS_SWIZZLING_RANGE
    {
        public uint NbRangesAcquired;
        public uint NbRangesReleased;
    }

    public struct D3DKMT_QUERYSTATSTICS_LOCKS
    {
        public uint NbLocks;
        public uint NbLocksWaitFlag;
        public uint NbLocksDiscardFlag;
        public uint NbLocksNoOverwrite;
        public uint NbLocksNoReadSync;
        public uint NbLocksLinearization;
        public uint NbComplexLocks;
    }

    public struct D3DKMT_QUERYSTATSTICS_ALLOCATIONS
    {
        public D3DKMT_QUERYSTATISTICS_COUNTER Created;
        public D3DKMT_QUERYSTATISTICS_COUNTER Destroyed;
        public D3DKMT_QUERYSTATISTICS_COUNTER Opened;
        public D3DKMT_QUERYSTATISTICS_COUNTER Closed;
        public D3DKMT_QUERYSTATISTICS_COUNTER MigratedSuccess;
        public D3DKMT_QUERYSTATISTICS_COUNTER MigratedFail;
        public D3DKMT_QUERYSTATISTICS_COUNTER MigratedAbandoned;
    }

    public struct D3DKMT_QUERYSTATSTICS_TERMINATIONS
    {
        public D3DKMT_QUERYSTATISTICS_COUNTER TerminatedShared;
        public D3DKMT_QUERYSTATISTICS_COUNTER TerminatedNonShared;
        public D3DKMT_QUERYSTATISTICS_COUNTER DestroyedShared;
        public D3DKMT_QUERYSTATISTICS_COUNTER DestroyedNonShared;
    }

    public struct D3DKMT_QUERYSTATISTICS_ADAPTER_INFORMATION
    {
        public uint NbSegments;
        public uint NodeCount;
        public uint VidPnSourceCount;
        public uint VSyncEnabled;
        public uint TdrDetectedCount;
        public long ZeroLengthDmaBuffers;
        public ulong RestartedPeriod;
        public D3DKMT_QUERYSTATSTICS_REFERENCE_DMA_BUFFER ReferenceDmaBuffer;
        public D3DKMT_QUERYSTATSTICS_RENAMING Renaming;
        public D3DKMT_QUERYSTATSTICS_PREPRATION Preparation;
        public D3DKMT_QUERYSTATSTICS_PAGING_FAULT PagingFault;
        public D3DKMT_QUERYSTATSTICS_PAGING_TRANSFER PagingTransfer;
        public D3DKMT_QUERYSTATSTICS_SWIZZLING_RANGE SwizzlingRange;
        public D3DKMT_QUERYSTATSTICS_LOCKS Locks;
        public D3DKMT_QUERYSTATSTICS_ALLOCATIONS Allocations;
        public D3DKMT_QUERYSTATSTICS_TERMINATIONS Terminations;

        /// <summary>D3DKMT_QUERYSTATISTICS_ADAPTER_INFORMATION_FLAGS: a UINT64 of bit fields.</summary>
        public ulong Flags;

        public fixed ulong Reserved[7];
    }

    public struct D3DKMT_QUERYSTATISTICS_QUEUE_PACKET_TYPE_INFORMATION
    {
        public uint PacketSubmited;
        public uint PacketCompleted;
    }

    public struct D3DKMT_QUERYSTATISTICS_DMA_PACKET_TYPE_INFORMATION
    {
        public uint PacketSubmited;
        public uint PacketCompleted;
        public uint PacketPreempted;
        public uint PacketFaulted;
    }

    /// <summary>D3DKMT_QUERYSTATISTICS_QUEUE_PACKET_TYPE_MAX entries.</summary>
    [InlineArray(8)]
    public struct QueuePackets
    {
        private D3DKMT_QUERYSTATISTICS_QUEUE_PACKET_TYPE_INFORMATION first;
    }

    /// <summary>D3DKMT_QUERYSTATISTICS_DMA_PACKET_TYPE_MAX entries.</summary>
    [InlineArray(4)]
    public struct DmaPackets
    {
        private D3DKMT_QUERYSTATISTICS_DMA_PACKET_TYPE_INFORMATION first;
    }

    public struct D3DKMT_QUERYSTATISTICS_PACKET_INFORMATION
    {
        public QueuePackets QueuePacket;
        public DmaPackets DmaPacket;
    }

    public struct D3DKMT_QUERYSTATISTICS_PREEMPTION_INFORMATION
    {
        /// <summary>D3DKMT_QUERYRESULT_PREEMPTION_ATTEMPT_RESULT_MAX entries.</summary>
        public fixed uint PreemptionCounter[16];
    }

    public struct D3DKMT_QUERYSTATISTICS_PROCESS_NODE_INFORMATION
    {
        /// <summary>
        /// The header says microseconds; the kernel counts 100 ns, which is
        /// what Task Manager's numbers agree with.
        /// </summary>
        public long RunningTime;

        public uint ContextSwitch;
        public D3DKMT_QUERYSTATISTICS_PREEMPTION_INFORMATION PreemptionStatistics;
        public D3DKMT_QUERYSTATISTICS_PACKET_INFORMATION PacketStatistics;
        public fixed ulong Reserved[8];
    }

    public struct D3DKMT_NODE_PERFDATA
    {
        public uint NodeOrdinal;
        public uint PhysicalAdapterIndex;
        public ulong Frequency;
        public ulong MaxFrequency;
        public ulong MaxFrequencyOC;
        public uint Voltage;
        public uint VoltageMax;
        public uint VoltageMaxOC;
        public ulong MaxTransitionLatency;
    }

    public struct D3DKMT_QUERYSTATISTICS_NODE_INFORMATION
    {
        public D3DKMT_QUERYSTATISTICS_PROCESS_NODE_INFORMATION GlobalInformation;
        public D3DKMT_QUERYSTATISTICS_PROCESS_NODE_INFORMATION SystemInformation;
        public D3DKMT_NODE_PERFDATA NodePerfData;
        public fixed uint Reserved[3];
    }
}
