// What a performance reading's flyout said, as words: the reading's name, the figure large, and
// the facts behind it in a grid, Windows Task Manager's way. The flyout went on 2026-10-10
// (docs\widgets.md §97); the reading's line at its tallest says this now (PerformanceIsland). It
// is a function of the source (the reading and its history, Sources\PerformanceData.cs) and of
// the few figures the machine gives besides (PerformanceFigures), so a test hands in both and gets
// the same words every time, and the line draws what this says and decides nothing of its own.
//
// Only what Windows answers cheaply and the same way every time. Left out on purpose: the
// processor's current speed (the API reports the base clock for it), the graphics adapters'
// dedicated memory (a driver query that fails on some machines), and a network adapter's link
// speed (it is the adapter's, not the traffic's).

using Karpo.Core;
using System.Globalization;

namespace Karpo.Dock.Performance;

/// <summary>One figure with its name: "Processes", "412".</summary>
internal readonly record struct PerformanceFact(string Label, string Value);

/// <summary>
/// The figures the machine gives besides a source's reading: what the system holds, how long it has
/// run, the processor's clock, and the names of the adapters and the disk. Read once a gather of
/// the source at most, while a line shows them (<see cref="Read"/>); a test makes its own.
/// </summary>
internal sealed record PerformanceFigures
{
    public SystemReading System { get; init; }

    /// <summary>How long the system has run.</summary>
    public TimeSpan Uptime { get; init; }

    /// <summary>The processor's base clock in MHz; 0 where not known.</summary>
    public int BaseMhz { get; init; }

    /// <summary>The logical processors.</summary>
    public int Logical { get; init; }

    /// <summary>The one graphics adapter the reading follows (GpuSampler.Followed); null where it is not attached.</summary>
    public string? Graphics { get; init; }

    /// <summary>The network adapter carrying the most; null where none is up.</summary>
    public string? Adapter { get; init; }

    /// <summary>A disk's name as Task Manager gives it ("Disk 0 (C: D:)"); null for every disk together.</summary>
    public string? DiskTitle { get; init; }

    /// <summary>The figures of the machine now, for the reading <paramref name="source"/> stands for.</summary>
    public static PerformanceFigures Read(PerformanceData source)
    {
        switch (source.Metric)
        {
            case Metric.Cpu:
                return new PerformanceFigures
                {
                    System = SystemReading.Read(),
                    Uptime = TimeSpan.FromMilliseconds(Environment.TickCount64),
                    BaseMhz = ProcessorInfo.BaseMhz,
                    Logical = ProcessorInfo.Logical,
                };
            case Metric.Memory:
                return new PerformanceFigures { System = SystemReading.Read() };
            case Metric.Gpu:
            {
                var gpu = SharedSamplers.GpuState;
                return new PerformanceFigures
                {
                    Graphics = !SharedSamplers.GpuFailed && gpu.Followed(source.Adapter, PerformanceIsland.AdapterName) is { } index
                        ? gpu.Adapters[index].Name
                        : null,
                };
            }
            case Metric.Disk:
            {
                var disk = SharedSamplers.DiskState;
                return new PerformanceFigures
                {
                    DiskTitle = source.Disk is { } number ? PerformanceOptions.DiskLabel(number, disk.LettersOf(number)) : null,
                };
            }
            default:
                return new PerformanceFigures { Adapter = SharedSamplers.NetworkState.AdapterName() };
        }
    }
}

/// <summary>What a reading's flyout showed, and its line at its tallest shows: see <see cref="For"/>.</summary>
internal sealed record PerformanceModel(
    Metric Metric,
    string Title,
    string Value,
    string Top,
    bool Known,
    IReadOnlyList<PerformanceFact> Facts)
{
    /// <summary>The longest stretch the graph shows, in seconds: Task Manager's own.</summary>
    public const int WindowSeconds = 60;

    /// <summary>What the figure says where there is no reading yet.</summary>
    public const string Unknown = "–";

    /// <summary>
    /// How many steps the graph spans: the window's worth and the one at its start — Task Manager's
    /// minute at one second a step, the line's default pace; a line set slower spans as many of its
    /// steps, a longer stretch, and says so under the graph.
    /// </summary>
    public const int Samples = WindowSeconds + 1;

    /// <summary>The words under the graph's left end: how far back it reaches.</summary>
    public static string Span => SpanAt(1);

    /// <summary>
    /// How far back a graph stepping every <paramref name="pace"/> seconds reaches: Task Manager's "60
    /// seconds" at a second a step, and minutes from two on ("30 minutes" at thirty seconds, "60
    /// minutes" at a minute, §228), never thousands of seconds.
    /// </summary>
    public static string SpanAt(int pace)
    {
        var seconds = WindowSeconds * Math.Max(1, pace);
        return seconds <= WindowSeconds ? S.WidgetPerformanceSeconds(seconds: seconds) : S.WidgetPerformanceMinutes(minutes: seconds / 60);
    }

    /// <summary>"3:04:12:45": days, hours, minutes and seconds, as Task Manager writes the time the system has run.</summary>
    public static string Uptime(TimeSpan time)
    {
        var total = (long)Math.Max(time.TotalSeconds, 0);
        return string.Create(CultureInfo.InvariantCulture, $"{total / 86400}:{total / 3600 % 24:00}:{total / 60 % 60:00}:{total % 60:00}");
    }

    /// <summary>"3.40 GHz": a clock in MHz as Task Manager writes it.</summary>
    public static string Clock(int mhz) =>
        string.Create(CultureInfo.InvariantCulture, $"{mhz / 1000.0:0.00} GHz");

    /// <summary>The flyout of the reading <paramref name="source"/> holds, with <paramref name="figures"/> beside it.</summary>
    public static PerformanceModel For(PerformanceData source, PerformanceFigures figures)
    {
        var metric = source.Metric;
        var network = metric == Metric.Network;
        var known = network ? source.Down >= 0 : source.Share >= 0;
        var share = known ? Units.Percent(source.Share) : Unknown;
        var title = metric switch
        {
            Metric.Disk when figures.DiskTitle is { Length: > 0 } disk => disk,
            Metric.Disk => S.WidgetPerformanceDisk,
            Metric.Cpu => S.WidgetPerformanceProcessor,
            Metric.Gpu => S.WidgetPerformanceGraphics,
            Metric.Memory => S.WidgetPerformanceMemory,
            _ => S.WidgetPerformanceNetwork,
        };
        var facts = new List<PerformanceFact>();
        switch (metric)
        {
            case Metric.Cpu:
                facts.Add(new(S.WidgetPerformanceUtilization, share));
                if (figures.BaseMhz > 0)
                {
                    facts.Add(new(S.WidgetPerformanceBaseSpeed, Clock(figures.BaseMhz)));
                }
                if (figures.Logical > 0)
                {
                    facts.Add(new(S.WidgetPerformanceLogicalProcessors, figures.Logical.ToString(CultureInfo.InvariantCulture)));
                }
                if (figures.System.Known)
                {
                    facts.Add(new(S.WidgetPerformanceProcesses, figures.System.Processes.ToString(CultureInfo.InvariantCulture)));
                    facts.Add(new(S.WidgetPerformanceThreads, figures.System.Threads.ToString(CultureInfo.InvariantCulture)));
                    facts.Add(new(S.WidgetPerformanceHandles, figures.System.Handles.ToString(CultureInfo.InvariantCulture)));
                }
                if (figures.Uptime > TimeSpan.Zero)
                {
                    facts.Add(new(S.WidgetPerformanceUpTime, Uptime(figures.Uptime)));
                }
                break;
            case Metric.Memory:
                if (source.Memory is { } memory)
                {
                    facts.Add(new(S.WidgetPerformanceInUse, Units.Of(memory.Used, memory.Total)));
                    facts.Add(new(S.WidgetPerformanceAvailable, Units.Size(memory.Available)));
                }
                if (figures.System.Known && figures.System.CommitLimit > 0)
                {
                    facts.Add(new(S.WidgetPerformanceCommitted, Units.Of(figures.System.Committed, figures.System.CommitLimit)));
                    facts.Add(new(S.WidgetPerformanceCached, Units.Size(figures.System.Cached)));
                }
                break;
            case Metric.Gpu:
                facts.Add(new(S.WidgetPerformanceUtilization, share));
                if (figures.Graphics is { Length: > 0 } graphics)
                {
                    facts.Add(new(S.WidgetPerformanceAdapter, graphics));
                }
                break;
            case Metric.Disk:
                facts.Add(new(S.WidgetPerformanceActiveTime, share));
                facts.Add(new(S.WidgetPerformanceReadSpeed, source.DiskRead < 0 ? Unknown : Units.Rate(source.DiskRead)));
                facts.Add(new(S.WidgetPerformanceWriteSpeed, source.DiskWritten < 0 ? Unknown : Units.Rate(source.DiskWritten)));
                break;
            default:
                facts.Add(new(S.WidgetPerformanceSend, known ? Units.Rate(source.Up) : Unknown));
                facts.Add(new(S.WidgetPerformanceReceive, known ? Units.Rate(source.Down) : Unknown));
                if (figures.Adapter is { Length: > 0 } adapter)
                {
                    facts.Add(new(S.WidgetPerformanceAdapter, adapter));
                }
                break;
        }
        // The big figure: the load for most, and everything moving (down and up together, which is
        // what the graph plots) for the network.
        var value = !known ? Unknown : network ? Units.Rate(source.Down + source.Up) : share;
        // What the graph's top stands for: a hundred per cent, or for the network the busiest moment
        // of the minute it draws rounded up, which it is scaled to (PerformanceIsland.History).
        var top = network ? Units.Rate(Graphs.Sparkline.Round(Graphs.Sparkline.Ceiling(source.Paced(1), Samples))) : Units.Percent(1);
        return new PerformanceModel(metric, title, value, top, known, facts);
    }
}
