// Which source a widget's data comes from (DataSources): one answer per kind of data, asked of
// what the widget is (its WidgetInfo) and how it is set (its options), never of where it stands —
// a graph on a bar, its card in a folder and its line in a list are one source when they read one
// thing, at any pace (a graph's pace is how it draws the one source: PerformanceData).

using Karpo.Core;

namespace Karpo.Dock;

internal static class SourceKinds
{
    /// <summary>
    /// The source <paramref name="widget"/>'s data comes from, or null for a widget that shows
    /// nothing it has to gather — the Start button, a pin, a separator, the clock.
    /// </summary>
    public static SourceKey? KeyOf(WidgetItem widget)
    {
        if (widget.Info is not { } info)
        {
            return null;
        }
        if (info.Metric is { } metric)
        {
            return PerformanceData.KeyOf(metric, widget.Performance ?? new PerformanceOptions());
        }
        // The weather's widget, card and list line: the place's forecast.
        if (widget.Weather is { } weather)
        {
            return WeatherData.KeyOf(weather);
        }
        if (widget.Market is { } market)
        {
            return MarketData.KeyOf(market);
        }
        // rss: the feed's headlines, one source per address however many widgets follow it.
        if (widget.Rss is { } rss)
        {
            return Rss.RssData.KeyOf(rss);
        }
        // The calendar's lines, the mail and the contacts have no source: the accounts are synced
        // from the dock's start whatever the config names (Pim\PimSync.cs).
        if (info == WidgetInfos.NotificationsList)
        {
            return NotificationData.KeyOf();
        }
        // The sound panel's regions — the output device's card and line among them — read one store.
        if (info == WidgetInfos.SoundOutput || info == WidgetInfos.SoundInput || info == WidgetInfos.SoundMixer || info == WidgetInfos.SoundVolume)
        {
            return SoundData.KeyOf();
        }
        if (info == WidgetInfos.Media)
        {
            return MediaData.KeyOf();
        }
        if (info == WidgetInfos.Security)
        {
            return SecurityData.KeyOf();
        }
        if (info.Switch is { } which)
        {
            return SwitchData.KeyOf(which);
        }
        if (info == WidgetInfos.Volume)
        {
            return VolumeData.KeyOf();
        }
        // The brightness's mode button (§605): Windows light or dark, the dark mode widget's one switch.
        if (info == WidgetInfos.Brightness)
        {
            return SwitchData.KeyOf(QuickSwitch.DarkMode);
        }
        return null;
    }

    /// <summary>
    /// Every source <paramref name="widget"/>'s data comes from: its one (<see cref="KeyOf"/>), or a
    /// toggles row's one per toggle it shows.
    /// </summary>
    public static IEnumerable<SourceKey> KeysOf(WidgetItem widget)
    {
        if (widget.Toggles is { } toggles)
        {
            return toggles.Shown.Where(toggle => Toggles.Find(toggle) is not null).Select(SwitchData.KeyOf);
        }
        return KeyOf(widget) is { } key ? [key] : [];
    }

    /// <summary>
    /// Every source <paramref name="widget"/> needs, with the widget each is made as: its own
    /// (<see cref="KeysOf"/>), and for a folder wearing a performance reading's face the reading the
    /// face shows, as a line of it set as the face is (<see cref="FaceLine"/>), so the face's history
    /// is gathered from the dock's start as a line's is.
    /// </summary>
    public static IEnumerable<(SourceKey Key, WidgetItem Widget)> Wanted(WidgetItem widget)
    {
        foreach (var key in KeysOf(widget))
        {
            yield return (key, widget);
        }
        if (widget is BarItem { Folder: { } folder })
        {
            // Each of its faces that is a reading's: a CPU face beside a memory face wants both.
            foreach (var face in folder.Faces)
            {
                if (FaceLine(face) is { } line && KeyOf(line) is { } faced)
                {
                    yield return (faced, line);
                }
            }
        }
    }

    /// <summary>
    /// What a performance reading's face reads, as a line of that reading set as the face is — its
    /// pace, its adapter or disk — which the face's source is made as; null for any other face.
    /// </summary>
    public static CellItem? FaceLine(FolderFace face)
    {
        if (PerformanceFace.MetricOf(face.Info) is not { } metric)
        {
            return null;
        }
        var line = ListWidgets.All.First(entry => entry.Info.Metric == metric).Kind.New();
        line.Performance = PerformanceFace.Options(face);
        return line;
    }

    /// <summary>
    /// The sources the dock runs from its start whether or not a widget names them: one for each
    /// performance reading, following the main adapter and every disk together. A face or a line
    /// made later draws their history, at whatever pace; only one that follows one named adapter or
    /// disk adds a source of its own to these.
    /// </summary>
    public static IReadOnlyList<(SourceKey Key, WidgetItem Widget)> Always() =>
    [
        .. Enum.GetValues<Metric>().Select(metric =>
        {
            var widget = ListWidgets.All.First(entry => entry.Info.Metric == metric).Kind.New();
            return (KeyOf(widget)!.Value, (WidgetItem)widget);
        }),
    ];

    /// <summary>A new source of <paramref name="key"/>, set as <paramref name="widget"/> is; null where the kind has none.</summary>
    public static DataSource? Make(SourceKey key, WidgetItem widget)
    {
        if (widget.Info?.Metric is { } metric)
        {
            var options = widget.Performance ?? new PerformanceOptions();
            // stage: a recording's graphs draw made-up readings, full from the start (Stage.cs).
            return Stage.On
                ? Stage.Performance(metric, PerformanceData.DeviceOf(metric, options))
                : new PerformanceData(metric, PerformanceData.DeviceOf(metric, options));
        }
        if (widget.Weather is { IsSet: true } weather)
        {
            return new WeatherData(weather);
        }
        if (widget.Market is { } market && MarketData.KeyOf(market) is not null)
        {
            return new MarketData(market);
        }
        if (widget.Rss is { IsSet: true } rss)
        {
            return new Rss.RssData(rss);
        }
        if (widget.Info?.Switch is { } which)
        {
            return new SwitchData(which);
        }
        // A toggles row's toggle: "switch:" and its one-button widget's switch ("switch:WifiRadio"),
        // or the toggle's own word for one that has none.
        if (key.Kind == "switch" && Enum.TryParse<QuickSwitch>(key.Of, out var switched) && Enum.IsDefined(switched))
        {
            return new SwitchData(switched);
        }
        if (key.Kind == "switch" && Enum.TryParse<Toggle>(key.Of, out var toggle) && Toggles.Find(toggle) is not null)
        {
            return new SwitchData(toggle);
        }
        return key.Kind switch
        {
            "security" => new SecurityData(),
            "volume" => new VolumeData(),
            "notifications" => new NotificationData(),
            "sound" => new SoundData(),
            "media" => new MediaData(),
            _ => null,
        };
    }
}
