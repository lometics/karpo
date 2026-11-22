// One CalDAV calendar brought up to date against what the dock holds of it
// (docs\widgets.md §616). The dock hands over each object's href and etag, and
// the token and tag of its last whole sync; this answers what to put in and
// what to take out.
//
// - The gate: the calendar's ctag (else its sync-token property), read in the
//   home's PROPFIND, is what the last sync found: nothing changed, nothing is
//   asked. A quiet calendar costs that one listing of the home.
// - sync-collection (RFC 6578), Depth 0, getetag only: from the token, what
//   changed and what went (404); with no token, the whole listing, compared by
//   etag. A server that answers in pages says so with a 507 for the calendar
//   itself: the next page is asked with the token it gave, at most MostRounds
//   times and never again with the same one. A token the server no longer
//   takes (403, 409, 410, or valid-sync-token) is let go once: the whole
//   listing again, only what differs downloaded.
// - A server without sync-collection (not in supported-report-set, or a 400,
//   403, 405 or 501 to it) is listed by PROPFIND Depth 1, each object's etag.
// - Each object that is new or has another etag is fetched in multigets of a
//   hundred, two at a time, a multiget cut short asked again in halves.
// - Taken out: what the server says went, and from a whole listing what it no
//   longer lists, but only from a listing it gave whole. Once a day the whole
//   listing is compared, for a server that loses deletions.
// - The token and tag are kept only when every change came: else the calendar
//   is incomplete, and the next sync asks again from the token before.
//
// A first download hands what it has to the dock every Checkpoint objects, so
// the events show in steps; what was handed over is not in its answer.

using System.Text;

namespace Karpo.Accounts.Dav;

internal sealed class CalendarSync(DavClient client, DavAnswer listed, RemoteCalendar calendar, CalendarState? state, DateTime utcNow, Func<CalendarChanges, Task>? checkpoint)
{
    /// <summary>Objects asked for in one multiget.</summary>
    internal const int Batch = 100;

    /// <summary>Multigets of one calendar at a time.</summary>
    internal const int Together = 2;

    /// <summary>Objects downloaded between two hand-overs to the dock.</summary>
    internal const int Checkpoint = 1000;

    /// <summary>Pages of a sync-collection asked for at most.</summary>
    internal const int MostRounds = 1000;

    /// <summary>Objects kept of one calendar at most: past it, the calendar shows what it holds and says not all is.</summary>
    internal const int MostObjects = 50_000;

    /// <summary>How often a calendar's whole listing is compared with what is held.</summary>
    internal static readonly TimeSpan Reconcile = TimeSpan.FromDays(1);

    private const int Truncated = 507;

    private readonly Uri url = listed.Href;

    private IReadOnlyDictionary<string, string> Held => state?.Etags ?? new Dictionary<string, string>(StringComparer.Ordinal);

    internal async Task<CalendarChanges> RunAsync(CancellationToken cancellation)
    {
        var tag = listed.Value(Dav.GetCtag) ?? listed.Value(Dav.SyncToken);
        var due = state is not { Complete: true, ReconciledUtc: { } at } || utcNow - at >= Reconcile || at > utcNow;
        if (!due && tag is { Length: > 0 } && tag == state!.Tag)
        {
            return new CalendarChanges(calendar) { Unchanged = true };
        }

        var noReport = state?.NoSyncReport == true || !Offers(listed, "sync-collection");
        Listing? listing = null;
        if (!noReport)
        {
            listing = await SyncCollectionAsync(due ? null : state!.Token, cancellation).ConfigureAwait(false);
            noReport = listing is null;
        }
        listing ??= await PropfindAsync(cancellation).ConfigureAwait(false);

        // What to fetch: new, or of another etag; past MostObjects nothing new.
        var held = Held;
        var removed = new HashSet<string>(listing.Gone, StringComparer.Ordinal);
        if (listing.Whole && !listing.Cut)
        {
            removed.UnionWith(held.Keys.Where(href => !listing.Present.ContainsKey(href)));
        }
        removed.IntersectWith(held.Keys);
        var room = MostObjects - (held.Count - removed.Count);
        var capped = false;
        var fetch = new List<string>();
        foreach (var (href, etag) in listing.Present)
        {
            if (held.TryGetValue(href, out var had))
            {
                if (etag is null || etag != had)
                {
                    fetch.Add(href);
                }
            }
            else if (room-- > 0)
            {
                fetch.Add(href);
            }
            else
            {
                capped = true;
            }
        }

        var (changed, gone, missed) = await FetchAsync(fetch, listing, cancellation).ConfigureAwait(false);
        removed.UnionWith(gone.Where(held.ContainsKey));
        var whole = !listing.Cut && !missed && !capped;
        return new CalendarChanges(calendar)
        {
            Changed = changed,
            Removed = [.. removed],
            Token = whole ? listing.Token : state?.Token,
            Tag = whole ? tag : null,
            NoSyncReport = noReport,
            Incomplete = !whole,
            ReconciledUtc = listing.Whole && !listing.Cut ? utcNow : state?.ReconciledUtc,
        };
    }

    /// <summary>Whether the calendar's supported-report-set offers <paramref name="report"/>; true where the server did not say.</summary>
    private static bool Offers(DavAnswer answer, string report) =>
        answer.Prop(Dav.SupportedReports) is not { } reports
        || !reports.Descendants(Dav.D + "report").Any()
        || reports.Descendants(Dav.D + "report").Any(one => one.Elements().Any(element => element.Name.LocalName == report));

    /// <summary>
    /// The calendar's changes since <paramref name="token"/> by sync-collection, or its whole listing for none; null where
    /// the server has no sync-collection for it. A token it no longer takes is let go once, for the whole listing.
    /// </summary>
    private async Task<Listing?> SyncCollectionAsync(string? token, CancellationToken cancellation)
    {
        var listing = new Listing(Whole: token is null);
        var asked = token;
        for (var round = 0; ; round++)
        {
            DavReport report;
            try
            {
                report = await client.ReportAsync(url, Body(asked), cancellation, depth: 0).ConfigureAwait(false);
            }
            catch (AccountException refused) when (round == 0 && token is not null
                && (refused.Status is 403 or 409 or 410 || refused.Precondition == "valid-sync-token"))
            {
                // The token is too old (Nextcloud prunes them): the whole listing, compared by etag.
                return await SyncCollectionAsync(null, cancellation).ConfigureAwait(false);
            }
            catch (AccountException refused) when (round == 0 && token is null && refused.Status is 400 or 403 or 405 or 501)
            {
                return null;
            }
            catch (AccountException refused) when (refused.Status == Truncated)
            {
                listing.Cut = true;
                return listing;
            }

            var more = false;
            foreach (var answer in report.Answers)
            {
                if (DavClient.Same(answer.Href, url))
                {
                    // RFC 6578 §3.6: the calendar itself with a 507 is "more to come, from the token given".
                    more |= answer.Status == Truncated;
                    continue;
                }
                var href = DavClient.Key(answer.Href);
                if (answer.Status == 404)
                {
                    listing.Present.Remove(href);
                    listing.Gone.Add(href);
                }
                else if (answer.Status is >= 200 and < 300)
                {
                    listing.Gone.Remove(href);
                    listing.Present[href] = answer.Value(Dav.GetEtag);
                }
            }
            listing.Token = report.SyncToken ?? listing.Token;
            if (!more)
            {
                return listing;
            }
            if (report.SyncToken is null || report.SyncToken == asked || round + 1 >= MostRounds)
            {
                // A server whose pages do not move on: what came is kept, the rest is asked again next time.
                listing.Cut = true;
                return listing;
            }
            asked = report.SyncToken;
        }
    }

    /// <summary>The whole listing by PROPFIND, each object's etag: what every CalDAV server answers.</summary>
    private async Task<Listing> PropfindAsync(CancellationToken cancellation)
    {
        var found = await client.PropfindAsync(url, "<getetag/><resourcetype/>", 1, cancellation).ConfigureAwait(false);
        var listing = new Listing(Whole: true);
        foreach (var answer in found.Answers)
        {
            if (answer.Status == Truncated)
            {
                listing.Cut = true;
                continue;
            }
            if (DavClient.Same(answer.Href, url) || DavClient.Same(answer.Href, found.At) || answer.Status is < 200 or >= 300
                || answer.Prop(Dav.ResourceType)?.Element(Dav.D + "collection") is not null)
            {
                continue;
            }
            listing.Present[DavClient.Key(answer.Href)] = answer.Value(Dav.GetEtag);
        }
        return listing;
    }

    /// <summary>
    /// <paramref name="hrefs"/> fetched in multigets, two at a time, each cut to what is kept; what the server says is
    /// gone (404), and whether any did not come. Every <see cref="Checkpoint"/> objects what came is handed to the
    /// dock and left out of the answer.
    /// </summary>
    private async Task<(List<CalendarObject> Changed, List<string> Gone, bool Missed)> FetchAsync(
        List<string> hrefs, Listing listing, CancellationToken cancellation)
    {
        var changed = new List<CalendarObject>();
        var gone = new List<string>();
        var missed = false;
        using var turns = new SemaphoreSlim(Together);
        for (var start = 0; start < hrefs.Count; start += Checkpoint)
        {
            var group = hrefs.Skip(start).Take(Checkpoint).ToList();
            var batches = await Task.WhenAll(Enumerable.Range(0, (group.Count + Batch - 1) / Batch).Select(async number =>
            {
                await turns.WaitAsync(cancellation).ConfigureAwait(false);
                try
                {
                    var batch = group.Skip(number * Batch).Take(Batch).Select(href => new Uri(href)).ToList();
                    return await DavReader.MultigetAsync(client, url, batch, Multiget, cancellation).ConfigureAwait(false);
                }
                finally
                {
                    turns.Release();
                }
            })).ConfigureAwait(false);

            var came = new Dictionary<string, DavAnswer>(StringComparer.Ordinal);
            foreach (var (answers, _) in batches)
            {
                foreach (var answer in answers)
                {
                    came[DavClient.Key(answer.Href)] = answer;
                }
            }
            var some = new List<CalendarObject>();
            foreach (var href in group)
            {
                if (!came.TryGetValue(href, out var answer))
                {
                    missed = true;
                }
                else if (answer.Status == 404)
                {
                    gone.Add(href);
                }
                else if (answer.Status is >= 200 and < 300 && answer.Value(Dav.CalendarData) is { } text)
                {
                    var etag = answer.Value(Dav.GetEtag) ?? listing.Present.GetValueOrDefault(href) ?? "";
                    some.Add(new CalendarObject(href, etag, KeptICalendar.Of(text)));
                }
                else
                {
                    missed = true;
                }
            }
            if (checkpoint is not null && start + Checkpoint < hrefs.Count)
            {
                await checkpoint(new CalendarChanges(calendar)
                {
                    Changed = some,
                    Token = state?.Token,
                    NoSyncReport = state?.NoSyncReport ?? false,
                    Incomplete = true,
                    ReconciledUtc = state?.ReconciledUtc,
                }).ConfigureAwait(false);
            }
            else
            {
                changed.AddRange(some);
            }
        }
        return (changed, gone, missed);
    }

    /// <summary>A sync-collection from <paramref name="token"/> (none: the whole listing), each member's etag alone.</summary>
    internal static string Body(string? token) =>
        """<?xml version="1.0" encoding="utf-8"?>"""
        + """<d:sync-collection xmlns:d="DAV:">"""
        + (token is null ? "<d:sync-token/>" : $"<d:sync-token>{DavClient.Escaped(token)}</d:sync-token>")
        + "<d:sync-level>1</d:sync-level><d:prop><d:getetag/></d:prop></d:sync-collection>";

    /// <summary>A calendar-multiget of <paramref name="hrefs"/>: each one's etag and data.</summary>
    internal static string Multiget(IEnumerable<Uri> hrefs)
    {
        var body = new StringBuilder();
        body.Append("""<?xml version="1.0" encoding="utf-8"?>""")
            .Append($"""<c:calendar-multiget xmlns:d="DAV:" xmlns:c="{Dav.Cal}">""")
            .Append("<d:prop><d:getetag/><c:calendar-data/></d:prop>");
        foreach (var href in hrefs)
        {
            body.Append("<d:href>").Append(DavClient.Escaped(DavClient.Path(href))).Append("</d:href>");
        }
        return body.Append("</c:calendar-multiget>").ToString();
    }

    /// <summary>
    /// What a listing found: each object it lists by href with its etag (null where the server gave none), what it
    /// says went, the token to go on from, whether it is of the whole calendar, and whether it was cut short.
    /// </summary>
    private sealed record Listing(bool Whole)
    {
        public Dictionary<string, string?> Present { get; } = new(StringComparer.Ordinal);

        public HashSet<string> Gone { get; } = new(StringComparer.Ordinal);

        public string? Token { get; set; }

        public bool Cut { get; set; }
    }
}
