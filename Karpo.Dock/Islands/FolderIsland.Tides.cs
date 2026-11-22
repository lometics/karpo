// tides: the tides face's answer (docs\widgets.md §236), read as the air-quality face's is: the
// folder registers the face's own place with the tides' store as a user of its own, asks for it on
// its tick where it is due, and takes what came when the store wakes it. The face's words move on
// with the minute between fetches (FaceTick), worked out of the series it holds.

using Karpo.Core;

namespace Karpo.Dock;

internal sealed partial class FolderIsland
{
    // tides: the place the face shows the tides for, while it wears the tides face with one set;
    // and the answer the store holds for it.
    private WeatherOptions? tidesPlace;
    private (TideCache? Shown, string? Problem) tides;

    /// <summary>
    /// Tests only, on the thread that makes the folder: the answer a tides face shows, instead of
    /// whatever the store has, so a picture of it is the same on every machine and nothing is fetched.
    /// </summary>
    [ThreadStatic]
    internal static TideCache? TidesStand;

    private readonly TideCache? tidesStand = TidesStand ?? (Samples.On ? Samples.Tides() : null);

    /// <summary>tides: the answer the tides face shows, and why a newer one is not there; null for none.</summary>
    internal (TideCache? Shown, string? Problem)? Tides =>
        tidesStand is { } stand ? (stand, null) : tidesPlace is null ? null : tides;

    /// <summary>Whether the folder reads the tides for a face, so it ticks at the store's interval.</summary>
    private bool ShowsTides => tidesPlace is not null && tidesStand is null;

    /// <summary>tides: the face follows its own place — registered with the store, asked for where due, and read — or nothing.</summary>
    private void FollowTides()
    {
        var place = options.Wears(FolderFaces.Tides) ? TidesFace.PlaceOfFace(options) : null;
        var next = place is null ? null : WeatherPlaces.Settled(place);
        if (next is not null && tidesPlace is not null && WeatherStore.KeyOf(next) == WeatherStore.KeyOf(tidesPlace))
        {
            return;
        }
        tidesPlace = next;
        if (tidesStand is not null)
        {
            return;
        }
        if (next is null)
        {
            TideStore.Shared.Release(this);
            tides = default;
            return;
        }
        TideStore.Shared.Use(this, next);
        TideStore.Shared.Poll(this);
        var (shown, problem, _) = TideStore.Shared.Read(this);
        tides = (shown, problem);
    }

    /// <summary>tides: the store woke the folder, or its tick came: news raised, a fetch that is due started, and the answer taken.</summary>
    private void SampleTides(bool everything)
    {
        if (!ShowsTides)
        {
            return;
        }
        if (everything)
        {
            TideStore.Shared.Deliver(this);
        }
        TideStore.Shared.Poll(this);
        var (shown, problem, _) = TideStore.Shared.Read(this);
        tides = (shown, problem);
    }
}
