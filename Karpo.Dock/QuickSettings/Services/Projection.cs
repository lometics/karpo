// How the screens are used, for the project widget's flyout: the four modes of
// Windows' own Win+P panel — PC screen only, Duplicate, Extend, Second screen
// only — and which of them is in force now.
//
// Both halves are the display configuration's, the very calls Win+P makes. The
// mode in force is the topology the current database records
// (QueryDisplayConfig with QDC_DATABASE_CURRENT, whose last argument is the
// topology id); choosing one is SetDisplayConfig with nothing but SDC_APPLY and
// that topology's flag, which has Windows pick the paths for it as Win+P does.
// Neither is WinRT, so neither has anything to fall off in the AOT build.
//
// Reading costs a few milliseconds of driver queries and applying is a whole
// mode change, so both go to the pool; the page shows what the last read found.
// A test never reaches either: the page is handed a SampleProjection, which
// records what it was asked to apply and changes no screen.

using Karpo.Core;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock.QuickSettings.Services;

/// <summary>Win+P's four modes, in its order.</summary>
internal enum ProjectMode
{
    PcScreenOnly,
    Duplicate,
    Extend,
    SecondScreenOnly,
}

/// <param name="Current">The mode in force; null until it has been read, or where Windows names none.</param>
/// <param name="Displays">How many screens are attached, whether they show anything now or not; 0 until read.</param>
/// <param name="Problem">What went wrong the last time a mode was applied, in the language now.</param>
internal sealed record ProjectState(ProjectMode? Current, int Displays, string? Problem = null);

internal interface IProjectSource : IDisposable
{
    void Start(Changes changes);

    ProjectState Read();

    /// <summary>Puts <paramref name="mode"/> in force, as choosing it in Win+P does. Never on the dock's thread.</summary>
    void Apply(ProjectMode mode);
}

internal static class ProjectNews
{
    public const int State = 1;
}

/// <summary>What each mode is called, wears and asks the display configuration for. Pure: the tests check it.</summary>
internal static class ProjectModes
{
    public static readonly ProjectMode[] All = [ProjectMode.PcScreenOnly, ProjectMode.Duplicate, ProjectMode.Extend, ProjectMode.SecondScreenOnly];

    // DISPLAYCONFIG_TOPOLOGY_ID, and the SetDisplayConfig flags of the same names.
    internal const uint Internal = 0x1;
    internal const uint Clone = 0x2;
    internal const uint Extended = 0x4;
    internal const uint External = 0x8;

    /// <summary>SDC_APPLY: put the topology in force now.</summary>
    internal const uint ApplyNow = 0x80;

    /// <summary>The mode a topology id names; null for none of the four (0, or a value from a newer Windows).</summary>
    public static ProjectMode? FromTopology(uint topology) => topology switch
    {
        Internal => ProjectMode.PcScreenOnly,
        Clone => ProjectMode.Duplicate,
        Extended => ProjectMode.Extend,
        External => ProjectMode.SecondScreenOnly,
        _ => null,
    };

    /// <summary>What SetDisplayConfig is handed for <paramref name="mode"/>: SDC_APPLY and that topology, and nothing else, as Win+P asks.</summary>
    public static uint Flags(ProjectMode mode) => ApplyNow | mode switch
    {
        ProjectMode.PcScreenOnly => Internal,
        ProjectMode.Duplicate => Clone,
        ProjectMode.Extend => Extended,
        ProjectMode.SecondScreenOnly => External,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Win+P has no such mode."),
    };

    public static string Title(ProjectMode mode) => mode switch
    {
        ProjectMode.PcScreenOnly => S.DockQuickProjectPcScreenOnly,
        ProjectMode.Duplicate => S.DockQuickProjectDuplicate,
        ProjectMode.Extend => S.DockQuickProjectExtend,
        ProjectMode.SecondScreenOnly => S.DockQuickProjectSecondScreenOnly,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Win+P has no such mode."),
    };

    /// <summary>
    /// A Segoe Fluent Icons glyph for each, after Win+P's pictures: the laptop alone, two screens
    /// over each other, two side by side, the monitor alone.
    /// </summary>
    public static string Glyph(ProjectMode mode) => char.ConvertFromUtf32(mode switch
    {
        ProjectMode.PcScreenOnly => 0xE7F8,
        ProjectMode.Duplicate => 0xEBC6,
        ProjectMode.Extend => 0xE736,
        ProjectMode.SecondScreenOnly => 0xE7F4,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Win+P has no such mode."),
    });

    /// <summary>
    /// How many screens are attached, from every path the configuration knows: a target counts
    /// once however many sources could drive it, and only while something is plugged into it.
    /// </summary>
    public static int Attached(IEnumerable<(ulong Adapter, uint Target, bool Available)> targets) =>
        targets.Where(target => target.Available).Select(target => (target.Adapter, target.Target)).Distinct().Count();
}

/// <summary>Windows' own: the display configuration, read and set the way Win+P does it.</summary>
internal sealed unsafe class WindowsProjection : IProjectSource
{
    private Changes? changes;
    private volatile ProjectState state = new(null, 0);
    private volatile bool disposed;

    public void Start(Changes news)
    {
        changes = news;
        Look();
    }

    public ProjectState Read() => state;

    public void Apply(ProjectMode mode)
    {
        var flags = ProjectModes.Flags(mode);
        // A refusal said before is not this one's.
        state = state with { Problem = null };
        _ = Task.Run(() => Troubles.Guard("Quick settings: applying a projection mode", () =>
        {
            var status = SetDisplayConfig(0, null, 0, null, flags);
            if (status != 0)
            {
                Troubles.Write($"Quick settings: SetDisplayConfig for {mode} answered {status}");
                state = state with { Problem = S.DockQuickProjectFailed(status) };
            }
            if (!disposed)
            {
                Look();
            }
        }));
    }

    public void Dispose() => disposed = true;

    private void Look() => _ = Task.Run(() => Troubles.Guard("Quick settings: reading the projection mode", () =>
    {
        var problem = state.Problem;
        state = new ProjectState(Current(), Attached(), problem);
        changes?.Add(ProjectNews.State);
    }));

    /// <summary>The topology the current database records: the mode Win+P would mark.</summary>
    private static ProjectMode? Current()
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            uint pathCount;
            uint modeCount;
            if (GetDisplayConfigBufferSizes(QDC_DATABASE_CURRENT, &pathCount, &modeCount) != 0)
            {
                return null;
            }
            var paths = new DISPLAYCONFIG_PATH_INFO[Math.Max(1, pathCount)];
            var modes = new DISPLAYCONFIG_MODE_INFO[Math.Max(1, modeCount)];
            DISPLAYCONFIG_TOPOLOGY_ID topology;
            int status;
            fixed (DISPLAYCONFIG_PATH_INFO* p = paths)
            fixed (DISPLAYCONFIG_MODE_INFO* m = modes)
            {
                status = QueryDisplayConfig(QDC_DATABASE_CURRENT, &pathCount, p, &modeCount, m, &topology);
            }
            if (status == 122 /* ERROR_INSUFFICIENT_BUFFER */)
            {
                continue;
            }
            return status == 0 ? ProjectModes.FromTopology((uint)topology) : null;
        }
        return null;
    }

    /// <summary>How many screens are plugged in, active or not (<see cref="ProjectModes.Attached"/>).</summary>
    private static int Attached()
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            uint pathCount;
            uint modeCount;
            if (GetDisplayConfigBufferSizes(QDC_ALL_PATHS, &pathCount, &modeCount) != 0)
            {
                return 0;
            }
            var paths = new DISPLAYCONFIG_PATH_INFO[Math.Max(1, pathCount)];
            var modes = new DISPLAYCONFIG_MODE_INFO[Math.Max(1, modeCount)];
            int status;
            fixed (DISPLAYCONFIG_PATH_INFO* p = paths)
            fixed (DISPLAYCONFIG_MODE_INFO* m = modes)
            {
                status = QueryDisplayConfig(QDC_ALL_PATHS, &pathCount, p, &modeCount, m, null);
            }
            if (status == 122 /* ERROR_INSUFFICIENT_BUFFER */)
            {
                continue;
            }
            if (status != 0)
            {
                return 0;
            }
            return ProjectModes.Attached(paths.Take((int)pathCount).Select(path =>
                (((ulong)(uint)path.targetInfo.adapterId.HighPart << 32) | path.targetInfo.adapterId.LowPart, path.targetInfo.id, path.targetInfo.targetAvailable != 0)));
        }
        return 0;
    }
}

/// <summary>A made-up PC with a laptop screen and a monitor: what the tests and the pictures use. It changes no screen.</summary>
internal sealed class SampleProjection : IProjectSource
{
    private Changes? changes;

    public SampleProjection(ProjectMode? current = ProjectMode.Extend, int displays = 2, string? problem = null)
    {
        Current = current;
        Displays = displays;
        Problem = problem;
    }

    public ProjectMode? Current { get; private set; }

    public int Displays { get; }

    public string? Problem { get; }

    /// <summary>Every mode it was asked to apply, in order.</summary>
    public List<ProjectMode> Applied { get; } = [];

    public void Start(Changes news) => changes = news;

    public ProjectState Read() => new(Current, Displays, Problem);

    public void Apply(ProjectMode mode)
    {
        Applied.Add(mode);
        Current = mode;
        changes?.Add(ProjectNews.State);
    }

    public void Dispose()
    {
    }
}
