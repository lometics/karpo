// air: the air-quality face's answer (docs\widgets.md §210), read as the weather face's forecast is:
// the folder registers the face's own place with the air's store as a user of its own, asks for it
// on its tick where it is due, and takes what came when the store wakes it.

using Karpo.Core;

namespace Karpo.Dock;

internal sealed partial class FolderIsland
{
    // air: the place the face shows the air for, while it wears the air-quality face with one set;
    // and the answer the store holds for it.
    private WeatherOptions? airPlace;
    private (AirCache? Shown, string? Problem) air;

    /// <summary>
    /// Tests only, on the thread that makes the folder: the answer an air-quality face shows, instead
    /// of whatever the store has, so a picture of it is the same on every machine and nothing is fetched.
    /// </summary>
    [ThreadStatic]
    internal static AirCache? AirStand;

    private readonly AirCache? airStand = AirStand ?? (Samples.On ? Samples.Air() : null);

    /// <summary>air: the answer the air-quality face shows, and why a newer one is not there; null for none.</summary>
    internal (AirCache? Shown, string? Problem)? Air =>
        airStand is { } stand ? (stand, null) : airPlace is null ? null : air;

    /// <summary>Whether the folder reads the air for a face, so it ticks at the online interval.</summary>
    private bool ShowsAir => airPlace is not null && airStand is null;

    /// <summary>air: the face follows its own place — registered with the store, asked for where due, and read — or nothing.</summary>
    private void FollowAir()
    {
        var place = options.Wears(FolderFaces.AirQuality) ? AirIndexes.PlaceOfFace(options) : null;
        var next = place is null ? null : WeatherPlaces.Settled(place);
        if (next is not null && airPlace is not null && WeatherStore.KeyOf(next) == WeatherStore.KeyOf(airPlace))
        {
            return;
        }
        airPlace = next;
        if (airStand is not null)
        {
            return;
        }
        if (next is null)
        {
            AirQualityStore.Shared.Release(this);
            air = default;
            return;
        }
        AirQualityStore.Shared.Use(this, next);
        AirQualityStore.Shared.Poll(this);
        var (shown, problem, _) = AirQualityStore.Shared.Read(this);
        air = (shown, problem);
    }

    /// <summary>air: the store woke the folder, or its tick came: news raised, a fetch that is due started, and the answer taken.</summary>
    private void SampleAir(bool everything)
    {
        if (!ShowsAir)
        {
            return;
        }
        if (everything)
        {
            AirQualityStore.Shared.Deliver(this);
        }
        AirQualityStore.Shared.Poll(this);
        var (shown, problem, _) = AirQualityStore.Shared.Read(this);
        air = (shown, problem);
    }
}
