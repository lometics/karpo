// §642, §652: where Windows search reads on this PC — the registry of a line's sources (IWinSearchSource): Windows' index
// (WindowsIndex), Start's apps (the shell's Apps folder, the one listing the pins' and the add page's Apps use,
// InstalledApps), Settings' pages, the calculator in the region's marks, the web's row with the app's engine, and Karpo's
// own (KarpoSources.cs) over the stores the dock keeps — the mail, the calendars, the contacts, the notes — and over the
// config; §655 the home's sections (WinSearchHomes) over Start (beta)'s source (StartStore: the apps, the pins, the files
// and folders used lately) and the same stores. Each slow read off the dock's thread on a thread of its own with COM up
// (DriveReader.Start). The engine is handed this; a test, and a ghost, hand their own (WinSearchSamples).

using Karpo.Core;
using Karpo.Dock.Drives;
using Karpo.Dock.Pim;
using Karpo.Dock.StartWidgets;

namespace Karpo.Dock.WinSearch;

/// <summary>§642: Start's apps, as the shell lists them (InstalledApps, read once a minute at most).</summary>
internal sealed class ShellApps : IWinSearchApps
{
    public static readonly ShellApps Shared = new();

    public IReadOnlyList<WinSearchApp> List() => [.. InstalledApps.List().Select(app => new WinSearchApp(app.Name, app.Target))];
}

/// <summary>§642, §652: this PC's sources (see the top of the file).</summary>
internal static class WinSearchSystem
{
    /// <summary>One index for every line: it holds nothing between questions.</summary>
    private static readonly WindowsIndex Index = new();

    /// <summary>
    /// The sources a line of the dock reads: Windows', then Karpo's own — the config's (<paramref name="config"/>, read as the
    /// line shows) and the web's engine (<paramref name="web"/>) as the line has them now —; §655 and its home's sections.
    /// </summary>
    public static WinSearchSources Sources(Func<Config> config, Func<string, string?> web) => WinSearchSources.Windows(
        Index,
        ShellApps.Shared,
        static work => _ = DriveReader.Start(work),
        WinSearchSources.Sleep,
        static () => Clock.RealUtc(),
        NumberMarks.Region(),
        web,
        Karpo(config),
        WinSearchHomes.Of(
            StartSources.Now,
            config,
            static () => MailStore.Shared.Messages,
            static (from, to) => CalendarStore.Shared.EventsIn(from, to),
            static (item, day) => EventLinks.Target(item, day, CalendarStore.Shared),
            static () => Notebook.Shared.Current.Notes,
            static () => Clock.Real()));

    /// <summary>§652: Karpo's own sources over the dock's stores and <paramref name="config"/>.</summary>
    private static IWinSearchSource[] Karpo(Func<Config> config) =>
    [
        new WinSearchMailSource(static () => MailStore.Shared.Messages, static () => Clock.RealUtc()),
        new WinSearchCalendarSource(
            static (from, to) => CalendarStore.Shared.EventsIn(from, to),
            static (item, day) => EventLinks.Target(item, day, CalendarStore.Shared),
            static () => Clock.Real()),
        new WinSearchContactsSource(static () => ContactStore.Shared.Contacts),
        new WinSearchNotesSource(static () => Notebook.Shared.Current.Notes),
        new WinSearchKarpoSource(() => WinSearchKarpoSource.Of(config())),
    ];
}
