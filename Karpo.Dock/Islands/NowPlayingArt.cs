// The art of what is playing, decoded once for every now-playing face.
//
// Each face used to decode the session's thumbnail for itself, and to remember the track it had
// decoded it for before it knew whether the decode found anything. A folder of faces — the review
// folder has one at every span from 1x1 to 4x4 — asked for the same thumbnail all at once, a read
// that crosses into the player's process, and a face whose read came back with nothing (the read
// failed, or the player had not handed its thumbnail over yet) kept nothing for the rest of the
// track: its cover was the grey note while the faces beside it showed the art. Nothing asked again
// until the track changed.
//
// Now the art is the track's, not a face's: one decode serves every face that asks while it runs,
// what it finds every face shows, and a decode that finds nothing is not the track's answer — the
// next face to read the session, on the next event, decodes again.

namespace Karpo.Dock;

internal sealed class NowPlayingArt
{
    private readonly object gate = new();

    // The track whose art is held or being decoded, its art once a decode found it, and the decode
    // under way for it.
    private string? track;
    private Pixels? art;
    private Task<Pixels?>? decoding;

    /// <summary>The one every now-playing face asks: what one face found, all of them show.</summary>
    internal static NowPlayingArt Shared { get; } = new();

    /// <summary>
    /// The art of the track <paramref name="key"/> names: the art already found for it, else what
    /// <paramref name="decode"/> finds — run once for every face that asks while it runs. Null where
    /// the session hands over no thumbnail (<paramref name="decode"/> is null), or where the decode
    /// found nothing or failed, and then the next ask decodes again.
    /// </summary>
    internal async Task<Pixels?> For(string key, Func<Task<Pixels?>>? decode)
    {
        Task<Pixels?> running;
        lock (gate)
        {
            if (track != key)
            {
                // Another track: what was held, or being decoded, was the last one's.
                (track, art, decoding) = (key, null, null);
            }
            if (art is not null)
            {
                return art;
            }
            if (decode is null)
            {
                return null;
            }
            running = decoding ??= decode();
        }
        Pixels? found = null;
        try
        {
            found = await running.ConfigureAwait(false);
            return found;
        }
        finally
        {
            lock (gate)
            {
                if (track == key && ReferenceEquals(decoding, running))
                {
                    // Done: what it found is the track's; nothing found leaves the next ask to try again.
                    (art, decoding) = (found, null);
                }
            }
        }
    }
}
