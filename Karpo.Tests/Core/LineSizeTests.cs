// Which folder lines have one form, a few, or content that grows (docs\widgets.md §432; Karpo.Core\LineSize.cs).
//
// The person, 2026-10-30: "a lot of widgets just have one height like the now playing widget or also the alarm
// one. (or maybe just a second height where they actually fix their contents properly). for these widgets i
// would not like the user to be able to change their height manually." The height survey sorted every kind;
// the approved rule: one form, no height setting at all; a few forms, a size to choose; content that grows,
// the Height it had — a chart that stretches stopping where its information is whole.

using System.Text.Json;
using Karpo.Core;

namespace Karpo.Tests.Core;

public sealed class LineSizeTests
{
    private const LineSize C = LineSize.Compact;
    private const LineSize M = LineSize.Medium;
    private const LineSize F = LineSize.Full;

    /// <summary>The kinds of one form: no height to set at all.</summary>
    internal static readonly ListKind[] OneForm =
    [
        ListKind.Timer, ListKind.Focus, ListKind.DateHeader, ListKind.Toggles, ListKind.Projection, ListKind.NearbySharing,
        ListKind.Dawn, ListKind.Dusk,
        // §641: Start's foot.
        ListKind.StartProfile,
    ];

    /// <summary>The switches: a row, or the name over what a click does.</summary>
    internal static readonly ListKind[] Switches =
    [
        ListKind.WifiSwitch, ListKind.BluetoothSwitch, ListKind.SoundSwitch, ListKind.MicrophoneSwitch, ListKind.GameMode,
        ListKind.ColorFilters, ListKind.MonoAudio, ListKind.AirplaneMode, ListKind.MobileHotspot, ListKind.Vpn,
        ListKind.EnergySaver, ListKind.NightLight, ListKind.RotationLock, ListKind.LiveCaptions, ListKind.NearbyShare,
        ListKind.Accessibility, ListKind.DoNotDisturb, ListKind.Magnifier, ListKind.Narrator, ListKind.StickyKeys,
        ListKind.Cast, ListKind.Project, ListKind.KeepAwake, ListKind.DarkMode, ListKind.SpatialSound,
    ];

    /// <summary>The kinds of a few forms, the sizes each offers and where a new line starts.</summary>
    internal static readonly Dictionary<ListKind, (LineSize[] Sizes, LineSize Starts)> FewForms = new()
    {
        [ListKind.Media] = ([C, M, F], M),
        [ListKind.Alarm] = ([C, F], F),
        [ListKind.Sunrise] = ([C, F], F),
        [ListKind.Sunset] = ([C, F], F),
        [ListKind.Daylight] = ([C, F], F),
        [ListKind.SolarNoon] = ([C, F], F),
        [ListKind.Drive] = ([C, F], F),
        [ListKind.NetworkDrive] = ([C, F], F),
        [ListKind.Pin] = ([C, F], C),
        [ListKind.Battery] = ([C, M, F], M),
        [ListKind.Security] = ([C, M, F], M),
        [ListKind.Clock] = ([C, F], F),
        [ListKind.TimeHeader] = ([C, M, F], F),
        [ListKind.SoundVolume] = ([C, M, F], C),
        [ListKind.Brightness] = ([C, M, F], C),
        [ListKind.CpuDetails] = ([C, F], F),
        [ListKind.GpuDetails] = ([C, F], F),
        [ListKind.MemoryDetails] = ([C, F], F),
        [ListKind.DiskDetails] = ([C, F], F),
        [ListKind.NetworkDetails] = ([C, F], F),
        [ListKind.PowerMode] = ([C, F], F),
        [ListKind.Markets] = ([C, F], F),
        [ListKind.SunDayBar] = ([C, F], F),
        [ListKind.ScreenTimeChart] = ([C, F], F),
        // §590: the weather's and the air's split widgets, each the same content at both sizes.
        [ListKind.WeatherNow] = ([C, F], F),
        [ListKind.WeatherHours] = ([C, F], C),
        [ListKind.WeatherHoursList] = ([C, F], F),
        [ListKind.WeatherDays] = ([C, F], F),
        [ListKind.WeatherDaysGraph] = ([C, F], F),
        [ListKind.AirQuality] = ([C, F], F),
        [ListKind.AirPollutants] = ([C, F], F),
        [ListKind.AirPollen] = ([C, F], F),
        [ListKind.AirForecast] = ([C, F], F),
        // §603: the widgets that still grew by tiers, split into kinds of fixed content.
        [ListKind.Cpu] = ([C, F], C),
        [ListKind.Gpu] = ([C, F], C),
        [ListKind.Memory] = ([C, F], C),
        [ListKind.Disk] = ([C, F], C),
        [ListKind.Network] = ([C, F], C),
        [ListKind.ScreenTime] = ([C, F], F),
        [ListKind.ScreenTimeHistory] = ([C, F], F),
        [ListKind.Tides] = ([C, F], F),
        [ListKind.TideGraph] = ([C, F], F),
        [ListKind.SunArc] = ([C, F], F),
        [ListKind.Sports] = ([C, F], F),
        // §641: Start's pinned apps and Recommended, a line of them or three.
        [ListKind.StartPins] = ([C, F], F),
        [ListKind.StartRecommended] = ([C, F], F),
    };

    /// <summary>§590: the weather's and the air's split widgets, the same content at both of their sizes.</summary>
    internal static readonly ListKind[] Split =
    [
        ListKind.WeatherNow, ListKind.WeatherHours, ListKind.WeatherHoursList, ListKind.WeatherDays, ListKind.WeatherDaysGraph,
        ListKind.AirQuality, ListKind.AirPollutants, ListKind.AirPollen, ListKind.AirForecast,
    ];

    /// <summary>§603: the widgets that grew by tiers, the same content at both of their sizes.</summary>
    internal static readonly ListKind[] TierSplit =
    [
        ListKind.Cpu, ListKind.Gpu, ListKind.Memory, ListKind.Disk, ListKind.Network,
        ListKind.ScreenTime, ListKind.ScreenTimeHistory, ListKind.Tides, ListKind.TideGraph, ListKind.SunArc, ListKind.Sports,
    ];

    /// <summary>Every sized kind: the one-form kinds, the switches and the rest of a few forms.</summary>
    internal static IEnumerable<ListKind> Sized => [.. OneForm, .. Switches, .. FewForms.Keys];

    /// <summary>Each card is one of the three, as the survey sorted them; a kind added says which.</summary>
    [Fact]
    public void EveryKindIsOneFormAFewOrGrows()
    {
        foreach (var kind in OneForm)
        {
            Assert.Equal([F], kind.Offered());
            Assert.False(kind.Chooses(), $"{kind} is one form");
        }
        foreach (var kind in Switches)
        {
            Assert.Equal([C, F], kind.Offered());
            Assert.Equal(C, ListWidgets.Of(kind).Starts);
        }
        foreach (var (kind, (sizes, starts)) in FewForms)
        {
            Assert.Equal(sizes, kind.Offered());
            Assert.Equal(starts, ListWidgets.Of(kind).Starts);
            Assert.True(kind.Chooses(), $"{kind} offers a size");
        }
        var sized = Sized.ToHashSet();
        foreach (var line in ListWidgets.All)
        {
            Assert.Equal(sized.Contains(line.Kind), line.Kind.Sized());
            // A sized kind has no Height; a card that grows has one, as it had.
            if (line.Kind.Sized())
            {
                Assert.False(line.Kind.HasHeight(), $"{line.Kind} is sized and has a Height");
            }
        }
        // The month's weeks grow with the height: it keeps it. The weather's and the air's split widgets have none (§590).
        Assert.True(ListKind.Month.HasHeight());
        Assert.All([ListKind.WeatherNow, ListKind.WeatherDays, ListKind.AirQuality], kind => Assert.False(kind.HasHeight()));
    }

    /// <summary>
    /// A line of a few forms keeps its size and no height, where a new one starts at its kind's; one of one form
    /// keeps neither; a line whose content grows keeps its height and no size. A size its kind has not reads as
    /// the one it starts at.
    /// </summary>
    [Fact]
    public void ASizedLineKeepsItsSizeAndNoHeight()
    {
        foreach (var kind in Sized)
        {
            var line = kind.New();
            line.Height = LineHeight.Exactly(8);
            line.Normalize();
            Assert.Null(line.Height);
            Assert.Equal(kind.Chooses() ? ListWidgets.Of(kind).Starts : null, line.Size);
            if (kind.Chooses())
            {
                foreach (var size in kind.Offered())
                {
                    line.Size = size;
                    line.Normalize();
                    Assert.Equal(size, line.Size);
                    Assert.Equal(size, LineSizes.Of(line));
                }
                if (!kind.Offered().Contains(M))
                {
                    line.Size = M;
                    line.Normalize();
                    Assert.Equal(ListWidgets.Of(kind).Starts, line.Size);
                }
            }
        }
        var mail = ListKind.Mail.New();
        mail.Size = C;
        mail.Normalize();
        Assert.Null(mail.Size);
        Assert.NotNull(mail.Height);
    }

    /// <summary>In the file a sized line says its size, <c>"size": "compact"</c>, and reads back as it was; one of one form says nothing.</summary>
    [Fact]
    public void TheFileKeepsASizeAndNoHeight()
    {
        var media = ListKind.Media.New();
        media.Size = C;
        media.Normalize();
        var json = JsonSerializer.Serialize(media, ConfigJson.Default.CellItem);
        Assert.Contains("\"size\": \"compact\"", json.Replace("\":\"", "\": \""), StringComparison.Ordinal);
        Assert.DoesNotContain("\"height\"", json, StringComparison.Ordinal);
        var read = JsonSerializer.Deserialize(json, ConfigJson.Default.CellItem)!;
        read.View = FolderView.List;
        read.Normalize();
        Assert.Equal(C, read.Size);

        var timer = ListKind.Timer.New();
        timer.Normalize();
        var timerJson = JsonSerializer.Serialize(timer, ConfigJson.Default.CellItem);
        Assert.DoesNotContain("\"size\"", timerJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\"height\"", timerJson, StringComparison.Ordinal);

        // A size this build does not know reads as where its kind starts.
        var unknown = JsonSerializer.Deserialize(json.Replace("compact", "tiny", StringComparison.Ordinal), ConfigJson.Default.CellItem)!;
        unknown.View = FolderView.List;
        unknown.Normalize();
        Assert.Equal(M, unknown.Size);
    }

    /// <summary>
    /// The fewest whole cells a measured form takes, a cell 20 px: 40 a row (two), 52 three (60), 64 four; never under a
    /// row; half a px over a cell is still the cell (a measurement on a fraction).
    /// </summary>
    [Fact]
    public void AFormIsTheFewestWholeCellsThatHoldIt()
    {
        Assert.Equal(2, LineHeight.CellsFor(0, 20));
        Assert.Equal(2, LineHeight.CellsFor(40, 20));
        Assert.Equal(3, LineHeight.CellsFor(40.6f, 20));
        Assert.Equal(3, LineHeight.CellsFor(52, 20));
        Assert.Equal(3, LineHeight.CellsFor(60.4f, 20));
        Assert.Equal(4, LineHeight.CellsFor(64, 20));
        Assert.Equal(6, LineHeight.CellsFor(118, 20));
        Assert.Equal(7, LineHeight.CellsFor(132, 30 / 1.5f));
        Assert.Equal(5, LineHeight.CellsFor(132, 30));
    }

    /// <summary>A ready-made folder gives a sized line a size, never a height.</summary>
    [Fact]
    public void APresetGivesASizedLineASizeNotAHeight()
    {
        foreach (var preset in Presets.All)
        {
            foreach (var item in preset.Items)
            {
                if (ListWidgets.Find(item.Form) is { } line && line.Kind.Sized())
                {
                    Assert.True(item.Height is null, $"{preset.Id}: its {item.Form} is sized and given {item.Height}");
                }
            }
        }
    }
}
