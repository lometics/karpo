// perf: what a performance widget's choices are — its pace, its adapter, its disk — and what picking
// one of them stores. The widget's menu on the bar (OptionRows, docs\widgets.md §214) offers them for a
// line and a face alike, so they are the core's.
//
// The adapter and the disk lists keep a name that is not attached now rather than drop it: an eGPU
// unplugged, a dock left at the office, an external disk — the device comes back, and a list that had
// quietly re-pointed the widget at whatever was left would have thrown the choice away.

namespace Karpo.Core;

/// <summary>The performance widgets' choices and what a pick stores; see the top of the file.</summary>
public static class PerformanceChoices
{
    /// <summary>
    /// Every pace a reading may step at, in seconds (2026-10-19, §228; the person: "how often it
    /// updates should get better times, right now it is just seconds from 1 to 10"): a ladder, each a
    /// clear step from the last, from Task Manager's second to a minute. Not under a second: the dock
    /// reads each reading once a second for every widget at once (PerformanceData), and a faster
    /// widget would be drawing that second again. Not over a minute: a graph steps once a pace, so
    /// one of five minutes would wait that long to move, and the source keeps the widest graph's
    /// history at the slowest pace (PerformanceData.Kept) — at a minute some eight hours.
    /// </summary>
    public static int[] Paces { get; } = [1, 2, 5, 10, 30, 60];

    /// <summary>The pace of <see cref="Paces"/> nearest <paramref name="seconds"/>, the faster of two as near: a file's 3 from when every second up to ten was offered is 2, its 8 is 10.</summary>
    public static int Nearest(int seconds) =>
        Paces.OrderBy(pace => Math.Abs((long)pace - seconds)).ThenBy(pace => pace).First();

    /// <summary>
    /// A pace as the list writes it. One second is Task Manager's own and is said so, because a
    /// person choosing it is choosing the familiar thing rather than an extreme.
    /// </summary>
    public static string Pace(int seconds) => seconds switch
    {
        1 => S.SettingsWidgetPerformancePaceSecond,
        60 => S.SettingsWidgetPerformancePaceMinute,
        _ => S.SettingsWidgetPerformancePaceSeconds(seconds),
    };

    /// <summary>Which entry of <see cref="Paces"/> a performance face's pace (<see cref="FaceOptionKind.Pace"/>, its seconds as a word) is, the nearest for any other number; the first for anything else.</summary>
    public static int FacePaceIndex(string word) =>
        int.TryParse(word, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var seconds)
            ? Math.Max(0, Array.IndexOf(Paces, Nearest(seconds)))
            : 0;

    /// <summary>What picking entry <paramref name="index"/> of <see cref="Paces"/> stores in a performance face: its seconds, as a word.</summary>
    public static string FacePace(int index) =>
        Paces[Math.Clamp(index, 0, Paces.Length - 1)].ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// A performance face's adapter or disk list (<see cref="FaceOptionKind.Adapter"/>,
    /// <see cref="FaceOptionKind.Disk"/>): the very list the reading's line offers, for what the face has chosen.
    /// </summary>
    public static List<string> FaceDevices(FaceOptionKind kind, IReadOnlyList<string> attached, string chosen) =>
        kind == FaceOptionKind.Adapter ? AdapterChoices(attached, chosen) : DiskChoices(attached, chosen);

    /// <summary>Which entry of <see cref="FaceDevices"/> the face has chosen; never outside the list.</summary>
    public static int FaceDeviceIndex(FaceOptionKind kind, IReadOnlyList<string> choices, string chosen) =>
        kind == FaceOptionKind.Adapter ? ChosenIndex(choices, chosen) : ChosenDiskIndex(choices, chosen);

    /// <summary>What picking entry <paramref name="index"/> stores in the face: "" for the main card or every disk, else the adapter's name or "Disk 1".</summary>
    public static string FaceDevicePicked(FaceOptionKind kind, IReadOnlyList<string> choices, int index) =>
        kind == FaceOptionKind.Adapter ? Picked(choices, index) : PickedDisk(choices, index) ?? "";

    /// <summary>
    /// What the first entry of the adapter list says: no adapter named, follow the main card — the
    /// one with the most video memory of its own (GpuSampler.MainOf). One card, never all of them.
    /// </summary>
    public static string Main => S.SettingsWidgetPerformanceMainAdapter;

    /// <summary>The adapter list: the main card first, then what this PC has, then a named one that is not attached now, kept.</summary>
    public static List<string> AdapterChoices(IReadOnlyList<string> attached, string? chosen)
    {
        var list = new List<string> { Main };
        foreach (var name in attached)
        {
            if (name.Length > 0 && !list.Contains(name, StringComparer.Ordinal))
            {
                list.Add(name);
            }
        }
        var named = (chosen ?? "").Trim();
        if (named.Length > 0 && !list.Contains(named, StringComparer.Ordinal))
        {
            list.Add(named);
        }
        return list;
    }

    /// <summary>Which entry of <see cref="AdapterChoices"/> is the one in use; never outside the list.</summary>
    public static int ChosenIndex(IReadOnlyList<string> choices, string? chosen)
    {
        var named = (chosen ?? "").Trim();
        if (named.Length == 0)
        {
            return 0;
        }
        var at = choices.ToList().IndexOf(named);
        return at < 0 ? 0 : at;
    }

    /// <summary>
    /// What picking entry <paramref name="index"/> stores: the empty string for the main card, which
    /// is what the widget reads as "no adapter named", and the name itself otherwise.
    /// </summary>
    public static string Picked(IReadOnlyList<string> choices, int index) =>
        index <= 0 || index >= choices.Count ? "" : choices[index];

    /// <summary>What the first entry of the disk list says: no disk named, follow them all.</summary>
    public static string AllDisks => S.SettingsWidgetPerformanceAllDisks;

    /// <summary>
    /// The disk list: every disk together first, then what this PC has as Task Manager labels them
    /// ("Disk 0 (C:)"), then a disk the widget names that is not attached now ("Disk 2"), kept.
    /// </summary>
    public static List<string> DiskChoices(IReadOnlyList<string> attached, string? chosen)
    {
        var list = new List<string> { AllDisks };
        var numbers = new HashSet<int>();
        foreach (var label in attached)
        {
            if (PerformanceOptions.DiskNumber(label) is { } number && numbers.Add(number))
            {
                list.Add(label);
            }
        }
        if (PerformanceOptions.DiskNumber(chosen) is { } named && !numbers.Contains(named))
        {
            list.Add(PerformanceOptions.DiskName(named));
        }
        return list;
    }

    /// <summary>Which entry of <see cref="DiskChoices"/> is the disk in use, matched by number; never outside the list.</summary>
    public static int ChosenDiskIndex(IReadOnlyList<string> choices, string? chosen)
    {
        if (PerformanceOptions.DiskNumber(chosen) is not { } named)
        {
            return 0;
        }
        for (var index = 1; index < choices.Count; index++)
        {
            if (PerformanceOptions.DiskNumber(choices[index]) == named)
            {
                return index;
            }
        }
        return 0;
    }

    /// <summary>
    /// What picking entry <paramref name="index"/> stores: null for every disk together, and the
    /// disk's number as the settings file writes it ("Disk 0") otherwise — never the letters,
    /// which move as volumes come and go.
    /// </summary>
    public static string? PickedDisk(IReadOnlyList<string> choices, int index) =>
        index <= 0 || index >= choices.Count || PerformanceOptions.DiskNumber(choices[index]) is not { } number
            ? null
            : PerformanceOptions.DiskName(number);
}
