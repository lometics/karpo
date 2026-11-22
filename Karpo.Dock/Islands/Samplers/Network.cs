// How fast data moves through the network adapters.
//
// Only interfaces that are up and backed by hardware count. Everything else
// carries the same bytes a second time: the filter drivers stacked on a
// Wi-Fi adapter each report its traffic as their own, a VPN or a Hyper-V
// switch passes it on to a physical adapter, and loopback never leaves the
// machine.
//
// The whole interface table is expensive to read, about a millisecond with
// the fifty-odd filter and miniport entries a desktop has, and the
// statistics are most of that. So the table is read without statistics once
// a minute to find the hardware interfaces, and each sample asks only those,
// at a few dozen microseconds each.

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Karpo.Dock;

internal sealed unsafe class NetworkSampler
{
    private const uint NO_ERROR = 0;
    private const uint IF_TYPE_SOFTWARE_LOOPBACK = 24;
    private const int IfOperStatusUp = 1;

    // MIB_IF_TABLE_LEVEL
    private const int MibIfTableNormal = 0;
    private const int MibIfTableNormalWithoutStatistics = 2;

    // InterfaceAndOperStatusFlags bits.
    private const byte HardwareInterface = 1 << 0;
    private const byte FilterInterface = 1 << 1;

    /// <summary>A USB adapter or a tethered phone is a new interface; this is how late it is noticed.</summary>
    private const long RescanSeconds = 60;

    // The hardware interfaces, up or not: an Ethernet port that is down now
    // comes up under the same LUID when a cable goes in. With each, its octet
    // counters at the last sample, matched by LUID, so an interface that
    // comes or goes between two samples adds nothing rather than its whole
    // history as one burst.
    private ulong[] luids = new ulong[4];
    private ulong[] received = new ulong[4];
    private ulong[] sent = new ulong[4];
    private bool[] counted = new bool[4];
    private int count;
    private long scanned;
    private long stamp;

    // The followed interface that carried the most at the last sample, by LUID; 0 before any did.
    // Kept as a number so a sample allocates nothing; its name is asked only when somebody looks
    // (AdapterName).
    private ulong busiest;

    /// <summary>Bytes per second at the last sample; -1 while not known.</summary>
    public double Down { get; private set; } = -1;

    /// <inheritdoc cref="Down"/>
    public double Up { get; private set; } = -1;

    /// <summary>Forgets everything, as after a resume, when adapters and counters may have started over.</summary>
    public void Reset()
    {
        count = 0;
        scanned = 0;
        stamp = 0;
        busiest = 0;
        Down = -1;
        Up = -1;
    }

    /// <summary>
    /// The description of the adapter carrying the most now ("Intel(R) Wi-Fi 6 AX201 160MHz"), the
    /// first one that is up where none moves; null where there is none. One call for one row, made
    /// when a flyout asks, never by a sample.
    /// </summary>
    public string? AdapterName()
    {
        if (busiest == 0)
        {
            return null;
        }
        MIB_IF_ROW2 row = default;
        row.InterfaceLuid = busiest;
        return GetIfEntry2(&row) == NO_ERROR && row.Description[0] != '\0' ? new string(row.Description) : null;
    }

    public void Sample(long now)
    {
        if (scanned == 0 || now - scanned > RescanSeconds * Stopwatch.Frequency)
        {
            Scan();
            scanned = now;
        }
        ulong inDelta = 0;
        ulong outDelta = 0;
        ulong most = 0;
        ulong pick = 0;
        for (int i = 0; i < count; i++)
        {
            MIB_IF_ROW2 row = default;
            row.InterfaceLuid = luids[i];
            if (GetIfEntry2(&row) != NO_ERROR)
            {
                // Gone: look for the new set on the next sample.
                counted[i] = false;
                scanned = 0;
                continue;
            }
            if (row.OperStatus != IfOperStatusUp)
            {
                counted[i] = false;
                continue;
            }
            var rowIn = Since(counted[i], row.InOctets, received[i]);
            var rowOut = Since(counted[i], row.OutOctets, sent[i]);
            inDelta += rowIn;
            outDelta += rowOut;
            if (pick == 0 || rowIn + rowOut > most)
            {
                (pick, most) = (luids[i], rowIn + rowOut);
            }
            received[i] = row.InOctets;
            sent[i] = row.OutOctets;
            counted[i] = true;
        }
        busiest = pick;
        if (stamp != 0 && now > stamp)
        {
            var seconds = (double)(now - stamp) / Stopwatch.Frequency;
            Down = inDelta / seconds;
            Up = outDelta / seconds;
        }
        stamp = now;
    }

    /// <summary>
    /// The octets since the last sample, and <b>nothing</b> where there is no baseline (an
    /// interface first seen this sample) or the counter went backwards (a reset). An interface
    /// that appears must add nothing rather than its whole history as one burst, which is the
    /// spike-to-the-ceiling a tethered phone used to draw the moment it was plugged in.
    /// </summary>
    internal static ulong Since(bool counted, ulong now, ulong before) =>
        counted && now >= before ? now - before : 0;

    /// <summary>
    /// The interfaces this sampler is following and what each carried at the last sample, for the
    /// tests: the carry-over across a rescan is the part with a rule in it (<see cref="Adopt"/>).
    /// </summary>
    internal IEnumerable<(ulong Luid, ulong Received, ulong Sent, bool Counted)> Tracked
    {
        get
        {
            for (var i = 0; i < count; i++)
            {
                yield return (luids[i], received[i], sent[i], counted[i]);
            }
        }
    }

    /// <summary>
    /// Takes up <paramref name="found"/> as the interfaces to follow, keeping what each of them
    /// carried before. An interface that is still there keeps its counters and its baseline, so
    /// the rescan costs it nothing; one that has gone is forgotten entirely; one that is new has
    /// no baseline, so its first window adds nothing (<see cref="Since"/>).
    /// <para>
    /// Separated from the reading of the table so that it can be tested: an adapter coming or
    /// going is the case this whole matching exists for, and it cannot be arranged on a machine.
    /// </para>
    /// </summary>
    internal void Adopt(IReadOnlyList<ulong> found)
    {
        var nextLuids = new ulong[Math.Max(found.Count, 1)];
        var nextReceived = new ulong[nextLuids.Length];
        var nextSent = new ulong[nextLuids.Length];
        var nextCounted = new bool[nextLuids.Length];
        for (int i = 0; i < found.Count; i++)
        {
            nextLuids[i] = found[i];
            var before = Array.IndexOf(luids, found[i], 0, count);
            if (before >= 0)
            {
                nextReceived[i] = received[before];
                nextSent[i] = sent[before];
                nextCounted[i] = counted[before];
            }
        }
        (luids, received, sent, counted, count) = (nextLuids, nextReceived, nextSent, nextCounted, found.Count);
    }

    /// <summary>
    /// Sets what one followed interface carried at the last sample, as a sample would, for the
    /// tests: <see cref="Adopt"/>'s rule is about carrying counters over, and it cannot be reached
    /// without counters to carry.
    /// </summary>
    internal void Remember(ulong luid, ulong received, ulong sent)
    {
        var at = Array.IndexOf(luids, luid, 0, count);
        if (at < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(luid), luid, "is not an interface this sampler follows.");
        }
        this.received[at] = received;
        this.sent[at] = sent;
        counted[at] = true;
    }

    private void Scan()
    {
        MIB_IF_TABLE2* table = null;
        // The level without statistics is Windows 10 1703 and later.
        if ((GetIfTable2Ex(MibIfTableNormalWithoutStatistics, &table) != NO_ERROR || table == null)
            && (GetIfTable2Ex(MibIfTableNormal, &table) != NO_ERROR || table == null))
        {
            count = 0;
            return;
        }
        try
        {
            var rows = &table->Table;
            var found = new List<ulong>();
            for (uint i = 0; i < table->NumEntries; i++)
            {
                var row = rows + i;
                if (row->Type != IF_TYPE_SOFTWARE_LOOPBACK
                    && (row->InterfaceAndOperStatusFlags & (HardwareInterface | FilterInterface)) == HardwareInterface)
                {
                    found.Add(row->InterfaceLuid);
                }
            }
            Adopt(found);
        }
        finally
        {
            FreeMibTable(table);
        }
    }

    [DllImport("iphlpapi.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint GetIfTable2Ex(int level, MIB_IF_TABLE2** table);

    [DllImport("iphlpapi.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint GetIfEntry2(MIB_IF_ROW2* row);

    [DllImport("iphlpapi.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern void FreeMibTable(void* memory);

    /// <summary>The size <see cref="MIB_IF_ROW2"/> has in netioapi.h on x64.</summary>
    public const int RowSize = 1352;

    /// <summary>Null when the declarations below match netioapi.h (Windows SDK 10.0.26100, shared\); otherwise what is wrong.</summary>
    public static string? LayoutProblem()
    {
        MIB_IF_TABLE2 table = default;
        var row = &table.Table;
        (string Name, long Actual, long Expected)[] checks =
        [
            ("MIB_IF_ROW2", sizeof(MIB_IF_ROW2), RowSize),
            ("MIB_IF_TABLE2.Table", (byte*)row - (byte*)&table, 8),
            ("MIB_IF_ROW2.Type", (byte*)&row->Type - (byte*)row, 1128),
            ("MIB_IF_ROW2.InterfaceAndOperStatusFlags", (byte*)&row->InterfaceAndOperStatusFlags - (byte*)row, 1152),
            ("MIB_IF_ROW2.OperStatus", (byte*)&row->OperStatus - (byte*)row, 1156),
            ("MIB_IF_ROW2.InOctets", (byte*)&row->InOctets - (byte*)row, 1208),
            ("MIB_IF_ROW2.OutOctets", (byte*)&row->OutOctets - (byte*)row, 1280),
        ];
        foreach (var (name, actual, expected) in checks)
        {
            if (actual != expected)
            {
                return $"{name} is {actual}, expected {expected}";
            }
        }
        return null;
    }

    /// <summary>
    /// netioapi.h's MIB_IF_ROW2, which TerraFX does not carry. The enum
    /// members are ints, as the C enums are.
    /// </summary>
    public struct MIB_IF_ROW2
    {
        /// <summary>NET_LUID: a union around one ULONG64.</summary>
        public ulong InterfaceLuid;

        public uint InterfaceIndex;
        public Guid InterfaceGuid;

        /// <summary>IF_MAX_STRING_SIZE + 1.</summary>
        public fixed char Alias[257];

        public fixed char Description[257];
        public uint PhysicalAddressLength;

        /// <summary>IF_MAX_PHYS_ADDRESS_LENGTH.</summary>
        public fixed byte PhysicalAddress[32];

        public fixed byte PermanentPhysicalAddress[32];
        public uint Mtu;

        /// <summary>IFTYPE.</summary>
        public uint Type;

        public int TunnelType;
        public int MediaType;
        public int PhysicalMediumType;
        public int AccessType;
        public int DirectionType;

        /// <summary>
        /// BOOLEAN bit fields, lowest first: HardwareInterface, FilterInterface,
        /// ConnectorPresent, NotAuthenticated, NotMediaConnected, Paused,
        /// LowPower, EndPointInterface.
        /// </summary>
        public byte InterfaceAndOperStatusFlags;

        /// <summary>IF_OPER_STATUS.</summary>
        public int OperStatus;

        public int AdminStatus;
        public int MediaConnectState;
        public Guid NetworkGuid;
        public int ConnectionType;
        public ulong TransmitLinkSpeed;
        public ulong ReceiveLinkSpeed;
        public ulong InOctets;
        public ulong InUcastPkts;
        public ulong InNUcastPkts;
        public ulong InDiscards;
        public ulong InErrors;
        public ulong InUnknownProtos;
        public ulong InUcastOctets;
        public ulong InMulticastOctets;
        public ulong InBroadcastOctets;
        public ulong OutOctets;
        public ulong OutUcastPkts;
        public ulong OutNUcastPkts;
        public ulong OutDiscards;
        public ulong OutErrors;
        public ulong OutUcastOctets;
        public ulong OutMulticastOctets;
        public ulong OutBroadcastOctets;
        public ulong OutQLen;
    }

    public struct MIB_IF_TABLE2
    {
        public uint NumEntries;

        /// <summary>The first of <see cref="NumEntries"/> rows (ANY_SIZE array).</summary>
        public MIB_IF_ROW2 Table;
    }
}
