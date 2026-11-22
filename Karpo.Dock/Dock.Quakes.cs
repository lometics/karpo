// quakes: the dock's part of the earthquake alerts (docs\widgets.md §240): it watches the earthquakes'
// store from its start, while any earthquake widget stands on a bar or in one of its folders, so a new
// earthquake is said whether or not a widget is looked at — a folder shut all day still hears of one.
//
// SAYING ONE. Karpo has no packaged identity, so it cannot raise a Windows toast (as the timer and the
// alarm cannot, Dock.Countdowns.cs): a new earthquake that an earthquake widget with its alert on
// shows — within its radius, over its least magnitude, never seen before or seen under it — rings
// once with the sound Windows gives notifications, and the folder holding the widget, or wearing it
// as its face, wears the accent's dot on its button until it is opened (FolderIsland.Due). The face
// itself shows the earthquake at once. Several in one answer ring once.

using Karpo.Core;

namespace Karpo.Dock;

public sealed unsafe partial class Dock
{
    /// <summary>The host's timer that asks the earthquakes' store whether a fetch is due.</summary>
    private const uint QUAKES = 83;

    /// <summary>How often the dock asks: a minute; the store decides what is due.</summary>
    private const uint QuakesLookMs = 60_000;

    private bool quakesHooked;

    /// <summary>Whether the dock is one of the store's users now, as some earthquake widget stands somewhere.</summary>
    private bool quakesWatched;

    /// <summary>Rings for the earthquakes one answer brought: once, and once for several.</summary>
    private readonly Ringer quakeRinger = new(ClockSound.Ring);

    /// <summary>The store is heard from the dock's start.</summary>
    private void HookQuakes()
    {
        if (quakesHooked)
        {
            return;
        }
        QuakeStore.Shared.Changed += QuakesChanged;
        quakesHooked = true;
        SyncQuakes();
    }

    private void DisposeQuakes()
    {
        if (quakesHooked)
        {
            QuakeStore.Shared.Changed -= QuakesChanged;
            quakesHooked = false;
        }
        Kill(QUAKES);
        if (quakesWatched)
        {
            QuakeStore.Shared.Release(this);
            quakesWatched = false;
        }
    }

    /// <summary>
    /// The config's earthquake widgets looked at: while any stands, the dock is the store's user — for
    /// this PC's position where one is for it — and asks it every minute; with none, it lets go.
    /// </summary>
    private void SyncQuakes()
    {
        var places = QuakesIn(config).Select(widget => widget.Place).ToList();
        if (places.Count == 0)
        {
            if (quakesWatched)
            {
                QuakeStore.Shared.Release(this);
                quakesWatched = false;
            }
            Kill(QUAKES);
            return;
        }
        QuakeStore.Shared.Watch();
        QuakeStore.Shared.Use(this, places.Any(place => place?.UseDeviceLocation == true));
        quakesWatched = true;
        QuakeStore.Shared.Poll();
        Arm(QUAKES, QuakesLookMs);
    }

    /// <summary>The minute has come: a fetch that is due is started.</summary>
    private void QuakesDue()
    {
        if (quakesWatched)
        {
            QuakeStore.Shared.Poll();
        }
    }

    /// <summary>Every earthquake widget of <paramref name="config"/>, a line or a face, with its place and settings.</summary>
    internal static IEnumerable<(WeatherOptions? Place, QuakeSettings Settings)> QuakesIn(Config config)
    {
        foreach (var layout in config.Layouts)
        {
            foreach (var (_, island) in Folders.All(layout.Islands))
            {
                if (island.Info == WidgetInfos.Quakes)
                {
                    yield return (island.Weather, QuakeChoices.Of(island.Weather));
                }
                if (island is BarItem { Folder: { } folder })
                {
                    foreach (var face in folder.Faces.Where(face => face?.Kind == FolderFaces.QuakesId))
                    {
                        yield return (face.Weather, QuakesFace.SettingsOf(face));
                    }
                }
            }
        }
    }

    /// <summary>
    /// Whether <paramref name="folder"/> says any of <paramref name="news"/>: a face of it or a line in
    /// it whose alert is on and which shows the earthquake as new (<see cref="QuakeNews.Says"/>).
    /// </summary>
    internal static bool SaysQuake(FolderOptions folder, IReadOnlyList<QuakeNew> news, QuakeHeld held)
    {
        bool Says(WeatherOptions? place, QuakeSettings settings) =>
            news.Any(each => QuakeNews.Says(each, settings, QuakeReading.OriginOf(place, held)));
        return folder.Faces.Any(face => face?.Kind == FolderFaces.QuakesId && Says(face.Weather, QuakesFace.SettingsOf(face)))
            || folder.Cells.Any(cell => cell?.Island is { } line && line.Info == WidgetInfos.Quakes && Says(line.Weather, QuakeChoices.Of(line.Weather)));
    }

    /// <summary>An answer came: the earthquakes it brought are said by the widgets that show them, once.</summary>
    private void QuakesChanged()
    {
        var news = QuakeStore.Shared.TakeNews();
        if (news.Count == 0)
        {
            return;
        }
        var held = QuakeStore.Shared.Read();
        var said = false;
        foreach (var island in islands.Values)
        {
            if (island is not FolderIsland folder || !SaysQuake(folder.Options, news, held))
            {
                continue;
            }
            said = true;
            if (!folder.Showing)
            {
                try
                {
                    _ = folder.SetDue(true);
                }
                catch (Exception error)
                {
                    Report(error);
                }
            }
        }
        if (said)
        {
            _ = quakeRinger.Ended(Environment.TickCount64);
        }
    }
}
