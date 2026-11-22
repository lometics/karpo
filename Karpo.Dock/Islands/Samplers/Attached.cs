// perf: the graphics adapters and the disks this PC has, for a graph's choice of one: the settings
// app's card (compiled into it, as the samplers are) and the graph's own menu on the bar (OptionRows,
// docs\widgets.md §214) list them alike.
//
// Each asks the bar's own sampler, so the list offered is the list the bar matches a choice against:
// same enumeration, same filter, same order, same names. Opening the graphics sampler enumerates the
// adapters and takes one reading, a few milliseconds at worst; the disks are a probe of sixteen device
// paths, about a millisecond. Neither throws: a PC whose drivers will not answer is an empty list, and
// the choice offers only the main card, or every disk together.

using Karpo.Core;

namespace Karpo.Dock;

internal static class AttachedDevices
{
    /// <summary>
    /// Every graphics adapter, named as the settings file names them, and which of them is the main
    /// card a widget naming none follows (<see cref="GpuSampler.MainOf"/>, the bar's own rule); "" where there is none.
    /// </summary>
    public static (IReadOnlyList<string> Names, string Main) Adapters()
    {
        try
        {
            var sampler = new GpuSampler();
            sampler.Open();
            if (sampler.Failed)
            {
                return ([], "");
            }
            var names = new List<string>(sampler.Adapters.Count);
            for (var index = 0; index < sampler.Adapters.Count; index++)
            {
                names.Add(PerformanceOptions.AdapterName(index, sampler.Adapters[index].Name));
            }
            var main = sampler.MainIndex;
            return (names, main >= 0 ? names[main] : "");
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            // A choice is not worth a crash: the person can still choose the main card.
            return ([], "");
        }
    }

    /// <summary>Every disk, labelled as Task Manager labels it: "Disk 0 (C:)".</summary>
    public static IReadOnlyList<string> Disks()
    {
        try
        {
            using var sampler = new DiskSampler();
            sampler.Sample(System.Diagnostics.Stopwatch.GetTimestamp());
            return [.. sampler.Named.Select(disk => PerformanceOptions.DiskLabel(disk.Number, disk.Letters))];
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            // A choice is not worth a crash: the person can still follow every disk together.
            return [];
        }
    }
}
