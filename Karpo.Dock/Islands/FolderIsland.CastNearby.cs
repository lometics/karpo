// The cast and nearby sharing faces' sources (docs\widgets.md §114, §115): each a source of the
// face's own while the folder wears it, made from the quick sources in force (QuickPageTile.
// SourcesNow: a test's, the samples, or Windows') when the face is put on and let go when it is
// taken off, as the hotspot face's is. What one hears reaches the dock's thread as a widget's news
// does (Post), and the face is painted again in place.
//
// The cast face's asks no radio anything: it only reads which display this PC shows on, on its own
// tick, since Windows says nothing when a Miracast session starts or ends that a desktop app hears.
// The nearby face's watches the key Windows keeps the policy under, so it ticks not at all.

using Karpo.Core;
using Karpo.Dock.QuickSettings.Services;

namespace Karpo.Dock;

internal sealed partial class FolderIsland
{
    /// <summary>quick: how often a cast face reads again which display this PC shows on.</summary>
    private const int CastEvery = 10_000;

    private ICastSource? castLink;
    private Changes? castNews;
    private INearbySource? nearbyLink;
    private Changes? nearbyNews;

    /// <summary>quick: whether it wears the cast face.</summary>
    internal bool ShowsCast => options.Wears(FolderFaces.Cast);

    /// <summary>quick: whether it wears the nearby sharing face.</summary>
    internal bool ShowsNearby => options.Wears(FolderFaces.Nearby);

    /// <summary>quick: what the cast face shows: which display this PC is cast to; null for another face.</summary>
    internal CastState? Cast => castLink?.Read();

    /// <summary>quick: what the nearby sharing face shows; null for another face.</summary>
    internal NearbyState? Nearby => nearbyLink?.Read();

    /// <summary>The face's own tick, or none: <see cref="IntervalMs"/> asks it after the faces before.</summary>
    private int CastNearbyEvery => castLink is not null ? CastEvery : 0;

    /// <summary>The two faces' sources, made or let go as the face the folder wears says.</summary>
    private void FollowCastNearby()
    {
        if (ShowsCast != castLink is not null)
        {
            if (castLink is { } gone)
            {
                (castLink, castNews) = (null, null);
                gone.Dispose();
            }
            else
            {
                var news = new Changes(_ => Post());
                var made = QuickPageTile.SourcesNow.Cast(false);
                (castLink, castNews) = (made, news);
                made.Start(news);
            }
        }
        if (ShowsNearby != nearbyLink is not null)
        {
            if (nearbyLink is { } gone)
            {
                (nearbyLink, nearbyNews) = (null, null);
                gone.Dispose();
            }
            else
            {
                var news = new Changes(_ => Post());
                var made = QuickPageTile.SourcesNow.Nearby();
                (nearbyLink, nearbyNews) = (made, news);
                made.Start(news);
            }
        }
    }

    /// <summary>What the sources heard is taken (the face reads them live); the cast face's tick reads again.</summary>
    private void SampleCastNearby(bool everything)
    {
        _ = nearbyNews?.Take();
        if (castLink is { } link)
        {
            _ = castNews?.Take();
            if (!everything && PaceDue("cast", CastEvery))
            {
                link.Poll();
            }
        }
    }

    private void DropCastNearby()
    {
        castLink?.Dispose();
        nearbyLink?.Dispose();
        (castLink, castNews, nearbyLink, nearbyNews) = (null, null, null, null);
    }
}
