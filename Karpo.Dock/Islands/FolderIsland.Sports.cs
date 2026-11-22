// sports: the sports face's answers (docs\widgets.md §239, §369), read as the air-quality face's is: the
// folder registers what each of its sports lines follows with the sports store, through a user of its
// own for each (§369: the face shows all of them, not the first line's alone), asks for each on its tick
// where it is due, and takes what came when the store wakes it — so the face is fresh with the folder
// shut, and the face and the lines are one fetch for each team or league.
//
// Which one the face shows is the most pressing (SportsPick.Lead): a game on now, then a result just in,
// then the soonest next game, then the last played; among equals the first line's.

using Karpo.Core;
using Karpo.Dock.Sports;

namespace Karpo.Dock;

internal sealed partial class FolderIsland
{
    // sports: what the face reads, while it wears the sports face and holds sports lines with something
    // followed: each followed once, its user with the store, and what the store holds for it.
    private List<(SportsOptions Team, object User)> sportsTeams = [];
    private SportsHeld[] sports = [];

    /// <summary>
    /// Tests only, on the thread that makes the folder: what a sports face shows, instead of whatever the
    /// store has, so a picture of it is the same on every machine and nothing is fetched.
    /// </summary>
    [ThreadStatic]
    internal static SportsHeld? SportsStand;

    private readonly SportsHeld? sportsStand = SportsStand ?? (Samples.On ? Samples.Sports() : null);

    /// <summary>sports: what the face follows first: its own (§477), else the folder's first sports line's; null for none.</summary>
    internal static SportsOptions? SportsOf(FolderOptions folder) => SportsAll(folder).FirstOrDefault();

    /// <summary>
    /// §369: what the folder's sports lines follow, in their order, each once; none where none follows anything. §477: what
    /// its sports face follows of its own instead, where it does.
    /// </summary>
    internal static IReadOnlyList<SportsOptions> SportsAll(FolderOptions folder) =>
        folder.FaceOf(FolderFaces.SportsId) is { Sports: { IsSet: true } own } ? [own] :
        [.. folder.Cells.Select(cell => cell?.Island).Where(inside => FaceSources.Of(inside, WidgetInfos.Sports))
            .Select(inside => inside!.Sports).OfType<SportsOptions>().Where(team => team.IsSet)
            .DistinctBy(team => team.Key, StringComparer.Ordinal)];

    /// <summary>
    /// sports: what the sports face shows now — the most pressing of its lines' readings (§369); null
    /// where it wears none or the folder follows nothing.
    /// </summary>
    internal SportsReading? SportsNow()
    {
        if (!options.Wears(FolderFaces.Sports) || Followed() is not { Count: > 0 } teams)
        {
            return null;
        }
        var utcNow = Clock.Now().ToUniversalTime();
        var readings = new List<SportsReading>(teams.Count);
        for (var at = 0; at < teams.Count; at++)
        {
            var held = sportsStand ?? (at < sports.Length && at < sportsTeams.Count && sportsTeams[at].Team.Key == teams[at].Key ? sports[at] : default);
            readings.Add(Sports.SportsReading.From(teams[at], held, utcNow, SportsStore.Shared.Interval));
        }
        var lead = SportsPick.Lead([.. readings.Select(reading => reading.View)]);
        return readings[Math.Max(0, lead)];
    }

    /// <summary>sports: the page the face's "Open website" opens: the shown match on ESPN, else the team's or the league's.</summary>
    internal string? SportsSite => SportsNow() is { } reading ? SportsTile.SiteOf(reading) : null;

    /// <summary>What the face follows (<see cref="SportsAll"/>); §635 a ghost's that follows nothing yet the samples' team.</summary>
    private IReadOnlyList<SportsOptions> Followed()
    {
        var all = SportsAll(options);
        return all.Count == 0 && Sampled ? [Samples.SportsTeam()] : all;
    }

    /// <summary>Whether the folder reads anything for a face, so it ticks at the sports' pace.</summary>
    private bool ShowsSports => sportsTeams.Count > 0 && sportsStand is null;

    /// <summary>sports: the face follows its lines' teams and leagues — each registered with the store, asked for where due, and read — or nothing.</summary>
    private void FollowSports()
    {
        var next = options.Wears(FolderFaces.Sports) ? Followed() : [];
        if (next.Select(team => team.Key).SequenceEqual(sportsTeams.Select(each => each.Team.Key), StringComparer.Ordinal))
        {
            return;
        }
        if (sportsStand is not null)
        {
            sportsTeams = [.. next.Select(team => (team, new object()))];
            return;
        }
        foreach (var (_, user) in sportsTeams)
        {
            SportsStore.Shared.Release(user);
        }
        sportsTeams = [.. next.Select(team => (team, new object()))];
        sports = new SportsHeld[sportsTeams.Count];
        for (var at = 0; at < sportsTeams.Count; at++)
        {
            var (team, user) = sportsTeams[at];
            SportsStore.Shared.Use(user, team, this);
            SportsStore.Shared.Poll(user);
            sports[at] = SportsStore.Shared.Read(user);
        }
    }

    /// <summary>sports: the store woke the folder, or its tick came: news raised, a fetch that is due started, and the answers taken.</summary>
    private void SampleSports(bool everything)
    {
        if (!ShowsSports)
        {
            return;
        }
        for (var at = 0; at < sportsTeams.Count; at++)
        {
            var user = sportsTeams[at].User;
            if (everything)
            {
                SportsStore.Shared.Deliver(user);
            }
            SportsStore.Shared.Poll(user);
            sports[at] = SportsStore.Shared.Read(user);
        }
    }

    /// <summary>sports: the folder goes, and with it its places among the store's users.</summary>
    private void ReleaseSports()
    {
        foreach (var (_, user) in sportsTeams)
        {
            SportsStore.Shared.Release(user);
        }
    }
}
