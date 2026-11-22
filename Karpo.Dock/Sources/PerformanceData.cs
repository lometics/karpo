// One performance reading — the processor's, a graphics adapter's, the memory's, the busiest
// disk's or the network's — read every second, and its history (DataSources).
//
// The history used to be each graph's own: a graph began with its first tick, so one in a folder
// began when the folder opened and one in a layout nobody showed never began at all, and two graphs
// of the processor drew two histories. It is the source's now, gathered from the dock's start
// whether or not anything draws it.
//
// **One source per reading, whatever the pace.** The person, 2026-10-09: "there should be one
// service in the background that always gets the data for the graph and then all the widgets that
// show this data should pull the data from this one service". So the source reads at one second, Task
// Manager's pace, and nothing else: a graph set to five seconds draws the same history five samples
// to a step (Series), and its first step is there the moment it is made. The pace is the graph's,
// not the source's. What does make another source is another device: a graph that follows one
// named graphics adapter, or one disk ("Disk 1"), reads another machine's figure, with a history of
// that device's own, as Task Manager draws Disk 0 and Disk 1 apart; the disks and the adapters are
// still read once a pass for all of them (DiskSampler.Pick, SharedSamplers).
//
// The counters themselves stay SharedSamplers': one read of each counter per pass however many
// sources ask.

using System.Diagnostics;
using Karpo.Core;

namespace Karpo.Dock;

internal sealed class PerformanceData : DataSource
{
    /// <summary>The most steps a graph draws: more than the widest graph a 160-DIP bar with large text shows.</summary>
    public const int Samples = 512;

    /// <summary>
    /// The slowest pace a graph may be set to, in seconds a step: a minute (PerformanceChoices.Paces,
    /// §228), which makes the history some thirty thousand samples, about 120 KB a source.
    /// </summary>
    public const int SlowestPace = PerformanceFace.SlowestPace;

    /// <summary>What the history keeps, in one-second samples: the widest graph's steps at the slowest pace, and the step still filling.</summary>
    public const int Kept = (Samples + 1) * SlowestPace;

    private readonly bool counting;

    /// <summary>
    /// The source of <paramref name="metric"/>, following <paramref name="device"/>: the graphics
    /// adapter for a graphics graph ("" for the main one), the disk for a disk graph ("Disk 1"; "" for
    /// every disk together), nothing for the others (<see cref="DeviceOf"/>).
    /// <paramref name="counting"/> is whether it reads the machine: false for a stand-in whose
    /// readings are handed to it (<see cref="Feed"/>), which holds no counter.
    /// </summary>
    public PerformanceData(Metric metric, string device, bool counting = true)
        : base(KeyOf(metric, device))
    {
        Metric = metric;
        Adapter = metric == Metric.Gpu ? device : "";
        Disk = metric == Metric.Disk ? PerformanceOptions.DiskNumber(device) : null;
        this.counting = counting;
    }

    public Metric Metric { get; }

    /// <summary>The graphics adapter it follows, as the settings name it; "" for the main one.</summary>
    public string Adapter { get; }

    /// <summary>The disk a disk graph follows, by number; null for every disk together, and for the other kinds.</summary>
    public int? Disk { get; }

    /// <summary>The samples, one a second, newest last: what every graph of this reading draws (<see cref="Series"/>).</summary>
    public History History { get; } = new(Kept);

    /// <summary>The newest share, 0 to 1; negative while not known.</summary>
    public float Share { get; private set; } = -1;

    /// <summary>What the memory holds, for a memory graph's second line; null while not known.</summary>
    public MemoryReading? Memory { get; private set; }

    /// <summary>Bytes a second down, for the network; negative while not known.</summary>
    public double Down { get; private set; } = -1;

    /// <summary>Bytes a second up, for the network; negative while not known.</summary>
    public double Up { get; private set; } = -1;

    /// <summary>Bytes a second read, for the disk; negative while not known.</summary>
    public double DiskRead { get; private set; } = -1;

    /// <summary>Bytes a second written, for the disk; negative while not known.</summary>
    public double DiskWritten { get; private set; } = -1;

    /// <summary>
    /// A test's readings in place of the machine's: each gather asks it for the share (or, for the
    /// network, bytes a second), null for a sample that could not be taken.
    /// </summary>
    internal Func<float?>? Reading { get; init; }

    /// <summary>The history a step every <paramref name="seconds"/>, at most <see cref="Samples"/> steps: what a graph at that pace draws.</summary>
    public Series Paced(int seconds) => new(History, Math.Clamp(seconds, 1, SlowestPace), Samples);

    /// <summary>The key of <paramref name="metric"/>'s source for a graph set as <paramref name="options"/>: its device, never its pace.</summary>
    public static SourceKey KeyOf(Metric metric, PerformanceOptions options) =>
        KeyOf(metric, DeviceOf(metric, options));

    /// <summary>
    /// What a graph of <paramref name="metric"/> set as <paramref name="options"/> follows: its
    /// graphics adapter, its disk ("Disk 1"), or "" — the main adapter, every disk together, or
    /// a reading of the one machine.
    /// </summary>
    public static string DeviceOf(Metric metric, PerformanceOptions options) => metric switch
    {
        Metric.Gpu => options.GpuAdapter ?? "",
        Metric.Disk => options.Disk ?? "",
        _ => "",
    };

    private static SourceKey KeyOf(Metric metric, string device)
    {
        // A disk is keyed by its number as the settings write it, so "disk 1" and "Disk 1" are
        // one source; a name that is no disk at all is every disk, as the widget reads it.
        var named = metric switch
        {
            Metric.Gpu => device,
            Metric.Disk => PerformanceOptions.DiskNumber(device) is { } number ? PerformanceOptions.DiskName(number) : "",
            _ => "",
        };
        return new("performance", named.Length > 0 ? $"{metric}/{named}" : $"{metric}");
    }

    public override int IntervalMs => 1000;

    protected internal override void Start()
    {
        if (counting && Reading is null)
        {
            SharedSamplers.Acquire();
        }
    }

    public override void Dispose()
    {
        if (counting && Reading is null)
        {
            SharedSamplers.Release();
        }
    }

    protected internal override bool Renew(long now)
    {
        if (!counting || Reading is not null)
        {
            return false;
        }
        // A device change or a resume: adapters and disks may have come or gone, and counters may
        // have started over. Measure from here rather than draw the whole gap as one spike.
        var at = Stopwatch.GetTimestamp();
        switch (Metric)
        {
            case Metric.Gpu:
                SharedSamplers.ReopenGpu(at);
                break;
            case Metric.Disk:
                SharedSamplers.ResetDisk(at);
                break;
            case Metric.Network:
                SharedSamplers.ResetNetwork(at);
                break;
        }
        return false;
    }

    protected override bool Gather(long now)
    {
        if (!counting)
        {
            return false;
        }
        float? value;
        if (Reading is { } reading)
        {
            value = reading();
            if (Metric == Metric.Network)
            {
                (Down, Up) = value is { } rate ? (rate, 0) : (-1, -1);
            }
            else
            {
                Share = value ?? -1;
            }
        }
        else
        {
            value = Read(Stopwatch.GetTimestamp());
        }
        if (value is { } sample)
        {
            History.Add(sample);
        }
        else if (History.Count > 0)
        {
            // A sample that could not be taken repeats the last rather than leaving a hole: the
            // graph's x axis is samples, so a missing one would silently stretch the time it covers.
            History.Add(History.Ago(0));
        }
        return true;
    }

    /// <summary>One reading of the machine: the value its graph plots, null where none could be taken.</summary>
    private float? Read(long now)
    {
        switch (Metric)
        {
            case Metric.Cpu:
                Share = SharedSamplers.Cpu(now);
                return Share >= 0 ? Share : null;
            case Metric.Gpu:
            {
                var gpu = SharedSamplers.Gpu(now);
                var chosen = Chosen(gpu);
                if (chosen >= 0)
                {
                    Share = chosen;
                    return chosen;
                }
                if (gpu.Failed || Adapter.Length > 0)
                {
                    // The adapter somebody named is not there: say nothing rather than quietly
                    // show another one's load under its name.
                    Share = -1;
                }
                return null;
            }
            case Metric.Memory:
                Memory = MemoryReading.Read();
                Share = Memory is { } memory ? memory.Load / 100f : -1;
                return Share >= 0 ? Share : null;
            case Metric.Disk:
            {
                // The disk's active time, as Task Manager's Disk graph plots it — never how full a
                // drive is, which is the Drive widget's. One disk's, or the busiest of them.
                var disk = SharedSamplers.Disk(now);
                (Share, DiskRead, DiskWritten) = DiskSampler.Pick(disk.Disks, Disk);
                return Share >= 0 ? Share : null;
            }
            default:
            {
                var network = SharedSamplers.Network(now);
                (Down, Up) = (network.Down, network.Up);
                // Down and up together, which is Task Manager's own summary figure: a cell has room
                // for one number, and the one people want is "how much is moving".
                return Down >= 0 ? (float)(Down + Up) : null;
            }
        }
    }

    /// <summary>
    /// The one adapter this source follows: the one named in its settings, or the main one
    /// (<see cref="GpuSampler.MainOf"/>). Never every adapter: the person, "the graphics widget should
    /// only show information / details about one graphics card not all installed once" (2026-10-11).
    /// </summary>
    private float Chosen(GpuSampler gpu) =>
        gpu.Followed(Adapter, PerformanceIsland.AdapterName) is { } index ? gpu.Adapters[index].Usage : -1;

    /// <summary>
    /// Readings handed over rather than read: what the offscreen pictures and the tests draw from.
    /// Only a stand-in (<c>counting: false</c>) is fed — a source the dock reads is never written by
    /// anybody else.
    /// </summary>
    /// <param name="reading">0 to 1; -1 for not known.</param>
    /// <param name="down">Bytes a second, for a network graph; -1 for not known.</param>
    /// <param name="up"><inheritdoc cref="down"/></param>
    /// <param name="memory">What the memory holds, for a memory graph's second line; kept when not given.</param>
    /// <param name="read">Bytes a second read, for a disk graph's second line.</param>
    /// <param name="written">Bytes a second written, for a disk graph's second line.</param>
    internal void Feed(float reading = -1, double down = -1, double up = -1, MemoryReading? memory = null, double read = -1, double written = -1)
    {
        Share = reading;
        (Down, Up) = (down, up);
        Memory = memory ?? Memory;
        (DiskRead, DiskWritten) = (read, written);
        if (Metric == Metric.Network)
        {
            if (down >= 0)
            {
                History.Add((float)(down + up));
            }
            return;
        }
        if (reading >= 0)
        {
            History.Add(reading);
        }
    }

    /// <summary>A stand-in's history, emptied.</summary>
    internal void Forget() => History.Clear();
}
