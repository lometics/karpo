// §655: Windows search's home sections on this PC (Karpo.Core\WinSearchHome.cs): what the line shows before anything is
// typed, each read from what Karpo already reads for another widget — nothing read twice:
//
//   * Top apps and Pinned: Start (beta)'s source (IStartSource: StartStore on the dock, the samples in a ghost) — the apps as
//     the shell lists them, how often each was opened from Karpo's Start or the line (StartUses, and the line's history),
//     and the pinned apps of the config's Start (beta) pinned line, else this PC's usual ones (StartPins);
//   * Recent files and Recent folders: the same source's, Windows' Recent folder read once (RecentFiles.Items);
//   * Coming up, Unread mail, Recent notes: the calendars, the mail and the notebook Karpo keeps (CalendarStore, MailStore,
//     Notebook), each row as the line's search finds it (KarpoSources.cs).
//
// A section of Start's source is told when it has read again (IStartSource.Changed) while the line shows, and no more once
// it has gone (Forget): the line is never kept by the store.

using Karpo.Accounts;
using Karpo.Core;
using Karpo.Dock.Pim;
using Karpo.Dock.StartWidgets;

namespace Karpo.Dock.WinSearch;

/// <summary>§655: the home's sections over what the dock (or a ghost) reads (see the top of the file).</summary>
internal static class WinSearchHomes
{
    /// <summary>
    /// The home's sections: the apps and the files of <paramref name="start"/>, the pins of <paramref name="config"/>'s Start
    /// (beta), the events of <paramref name="events"/> (each opening at <paramref name="eventTarget"/>) from the wall-clock
    /// <paramref name="localNow"/>, the unread <paramref name="mail"/>, the <paramref name="notes"/>.
    /// </summary>
    public static IWinSearchHomeSource[] Of(
        IStartSource start,
        Func<Config> config,
        Func<IReadOnlyList<MailItem>> mail,
        Func<DateTime, DateTime, IReadOnlyList<CalendarEvent>> events,
        Func<CalendarEvent, DateOnly, string?> eventTarget,
        Func<IReadOnlyList<Note>> notes,
        Func<DateTime> localNow)
    {
        var watch = new StartWatch(start);
        static WinSearchApp Of(StartApp app) => new(app.Name, app.Target);
        IEnumerable<WinSearchApp> Pinned() => StartPins.Apps(StartPins.Of(StartPins.OfConfig(config()), start.Apps), start.Apps).Select(Of);
        return
        [
            // §658: before anything was opened from Karpo, the pinned apps stand in for the apps opened most.
            new WinSearchHomeSource(WinSearchHomeInfos.Apps,
                ask => WinSearchHomeHits.TopApps(start.Apps.Select(Of), target => start.Uses.Of(target) is { } use ? (use.Count, use.LastUtc) : (0, default), ask.Opened, ask.Most, Pinned()),
                watch.Warm, watch.Forget),
            new WinSearchHomeSource(WinSearchHomeInfos.Pinned,
                ask => [.. Pinned().Take(ask.Most).Select(WinSearchHomeHits.OfApp)],
                watch.Warm, watch.Forget),
            new WinSearchHomeSource(WinSearchHomeInfos.Files,
                ask => [.. start.Files.Take(ask.Most).Select(file => WinSearchHomeHits.OfPath(file.Target, file.WhenUtc, folder: false))],
                watch.Warm, watch.Forget),
            new WinSearchHomeSource(WinSearchHomeInfos.Folders,
                ask => [.. start.Folders.Take(ask.Most).Select(folder => WinSearchHomeHits.OfPath(folder.Target, folder.WhenUtc, folder: true))],
                watch.Warm, watch.Forget),
            new WinSearchHomeSource(WinSearchHomeInfos.Events, ask => WinSearchCalendarSource.Coming(events, eventTarget, localNow(), ask.Most)),
            new WinSearchHomeSource(WinSearchHomeInfos.Mail, ask => WinSearchMailSource.Unread(mail(), ask.Most)),
            new WinSearchHomeSource(WinSearchHomeInfos.Notes, ask => WinSearchNotesSource.Recent(notes(), ask.Most)),
        ];
    }

    /// <summary>
    /// Start's source read again where it is stale as the line shows, the line told whenever it has read (once, however many
    /// sections share it), and no more once the line has gone.
    /// </summary>
    private sealed class StartWatch(IStartSource start)
    {
        private readonly Lock gate = new();
        private Action? told;

        public void Warm(Action came)
        {
            lock (gate)
            {
                if (told is null)
                {
                    told = came;
                    start.Changed += came;
                }
            }
            start.Refresh();
        }

        public void Forget()
        {
            lock (gate)
            {
                if (told is { } was)
                {
                    start.Changed -= was;
                    told = null;
                }
            }
        }
    }
}
