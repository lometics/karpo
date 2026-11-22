// quakes: the earthquake face's answer (docs\widgets.md §240), read as the air-quality face's is: the
// folder registers with the earthquakes' store as a user of its own while it wears the face, asks for
// the feed on its tick where it is due, and takes what came when the store wakes it.

using Karpo.Core;

namespace Karpo.Dock;

internal sealed partial class FolderIsland
{
    // quakes: whether the folder wears the earthquake face looking somewhere, and what the store holds.
    private bool followsQuakes;
    private QuakeHeld quakes = QuakeHeld.None;

    /// <summary>
    /// Tests only, on the thread that makes the folder: what an earthquake face shows, instead of
    /// whatever the store has, so a picture of it is the same on every machine and nothing is fetched.
    /// </summary>
    [ThreadStatic]
    internal static QuakeHeld? QuakeStand;

    private readonly QuakeHeld? quakeStand = QuakeStand ?? (Samples.On ? Samples.Quakes() : null);

    /// <summary>quakes: what the earthquake face shows from; null where it wears none.</summary>
    internal QuakeHeld? Quakes => quakeStand ?? (followsQuakes ? quakes : null);

    /// <summary>Whether the folder reads the earthquakes for a face, so it ticks at their interval.</summary>
    private bool ShowsQuakes => followsQuakes && quakeStand is null;

    /// <summary>quakes: the face follows the store — registered, asked for where due, and read — or nothing.</summary>
    private void FollowQuakes()
    {
        var face = options.FaceOf(FolderFaces.QuakesId);
        followsQuakes = face is not null && QuakeFaceDrawing.Set(face);
        if (quakeStand is not null)
        {
            return;
        }
        if (!followsQuakes)
        {
            QuakeStore.Shared.Release(this);
            quakes = QuakeHeld.None;
            return;
        }
        QuakeStore.Shared.Use(this, face!.Weather?.UseDeviceLocation == true);
        QuakeStore.Shared.Poll();
        quakes = QuakeStore.Shared.Read();
    }

    /// <summary>quakes: the store woke the folder, or its tick came: news raised, a fetch that is due started, and the answer taken.</summary>
    private void SampleQuakes(bool everything)
    {
        if (!ShowsQuakes)
        {
            return;
        }
        if (everything)
        {
            QuakeStore.Shared.Deliver();
        }
        QuakeStore.Shared.Poll();
        quakes = QuakeStore.Shared.Read();
    }
}
