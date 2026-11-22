// The project, cast and mobile hotspot flyouts' models: Win+P's modes and the
// display configuration's numbers for them, the wireless displays' rows, what
// the hotspot page says for each state, which page each bar button asks for —
// and that a set of sources a test makes can never reach the real screens,
// displays or hotspot. Nothing here needs a window.

using Karpo.Core;
using Karpo.Dock;
using Karpo.Dock.QuickSettings;
using Karpo.Dock.QuickSettings.Services;
using Operators = Windows.Networking.NetworkOperators;

namespace Karpo.Tests.QuickSettings;

public sealed class DisplayFlyoutsTests
{
    // --- Project --------------------------------------------------------------------------------

    [Fact]
    public void TheFourModesAreWinPsInItsOrder()
    {
        Assert.Equal([ProjectMode.PcScreenOnly, ProjectMode.Duplicate, ProjectMode.Extend, ProjectMode.SecondScreenOnly], ProjectModes.All);
        Assert.Equal(["PC screen only", "Duplicate", "Extend", "Second screen only"], ProjectModes.All.Select(ProjectModes.Title));
        Assert.Equal(4, ProjectModes.All.Select(ProjectModes.Glyph).Distinct().Count());
    }

    /// <summary>
    /// The topology ids the display configuration reports and the SetDisplayConfig flags of the same
    /// names: each mode asks for SDC_APPLY and its own topology and nothing else, and reads back as
    /// itself. An id that is none of the four (0, or one from a newer Windows) marks nothing.
    /// </summary>
    [Theory]
    [InlineData((int)ProjectMode.PcScreenOnly, 0x1u)]
    [InlineData((int)ProjectMode.Duplicate, 0x2u)]
    [InlineData((int)ProjectMode.Extend, 0x4u)]
    [InlineData((int)ProjectMode.SecondScreenOnly, 0x8u)]
    public void EachModeAsksForItsOwnTopology(int which, uint topology)
    {
        var mode = (ProjectMode)which;
        Assert.Equal(0x80u | topology, ProjectModes.Flags(mode));
        Assert.Equal(mode, ProjectModes.FromTopology(topology));
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(0x10u)]
    [InlineData(0x3u)]
    public void ATopologyThatIsNoneOfTheFourMarksNothing(uint topology) => Assert.Null(ProjectModes.FromTopology(topology));

    /// <summary>A screen counts once however many sources could drive it, and only while one is plugged in.</summary>
    [Fact]
    public void TheScreensAttachedAreTheTargetsWithSomethingPluggedIn()
    {
        Assert.Equal(0, ProjectModes.Attached([]));
        Assert.Equal(2, ProjectModes.Attached(
        [
            (1, 10, true), (1, 10, true), (1, 10, true),
            (1, 11, true),
            (1, 12, false),
            (2, 10, false),
        ]));
        // The same target number on another adapter is another screen.
        Assert.Equal(2, ProjectModes.Attached([(1, 10, true), (2, 10, true)]));
    }

    // --- The bar and the sources ----------------------------------------------------------------

    /// <summary>
    /// A set of sources a test makes reaches no screen, display or hotspot unless it says so: the
    /// three default to samples, and only Windows' own set holds Windows' own. Making one of those
    /// asks Windows nothing — the asking is in Start.
    /// </summary>
    [Fact]
    public void OnlyWindowsOwnSourcesReachTheRealOnes()
    {
        var made = new QuickSources
        {
            Wifi = static () => new SampleWifi(),
            Bluetooth = static () => new SampleBluetooth(),
            Battery = static () => new SampleBattery(),
            Desktops = static () => new SampleDesktopNames(),
        };
        using (var project = made.Project())
        using (var hotspot = made.Hotspot())
        using (var cast = made.Cast(true))
        using (var nearby = made.Nearby())
        {
            Assert.IsType<SampleProjection>(project);
            Assert.IsType<SampleHotspot>(hotspot);
            Assert.IsType<SampleCast>(cast);
            Assert.IsType<SampleNearby>(nearby);
        }
        using (var project = QuickSources.Samples.Project())
        using (var hotspot = QuickSources.Samples.Hotspot())
        using (var cast = QuickSources.Samples.Cast(false))
        using (var nearby = QuickSources.Samples.Nearby())
        {
            Assert.IsType<SampleProjection>(project);
            Assert.IsType<SampleHotspot>(hotspot);
            Assert.IsType<SampleCast>(cast);
            Assert.IsType<SampleNearby>(nearby);
        }
        using (var project = QuickSources.Windows.Project())
        using (var hotspot = QuickSources.Windows.Hotspot())
        using (var cast = QuickSources.Windows.Cast(false))
        using (var nearby = QuickSources.Windows.Nearby())
        {
            Assert.IsType<WindowsProjection>(project);
            Assert.IsType<WindowsHotspot>(hotspot);
            Assert.IsType<WindowsCast>(cast);
            Assert.IsType<WindowsNearby>(nearby);
        }
    }

    /// <summary>The made-up screens record what they were asked to apply, and change to it, and nothing else.</summary>
    [Fact]
    public void TheSampleScreensRecordWhatTheyWereAsked()
    {
        using var screens = new SampleProjection(ProjectMode.PcScreenOnly);
        screens.Apply(ProjectMode.SecondScreenOnly);
        Assert.Equal([ProjectMode.SecondScreenOnly], screens.Applied);
        Assert.Equal(ProjectMode.SecondScreenOnly, screens.Read().Current);
    }
}
