// One sound source for every sound component that is looking (docs\widgets.md §9, the fourth
// conversion), as one notification store serves every notification card.
//
// Each of the sound panel's four regions used to start a source of its own. Measured in a
// published build on 25 September 2026: one Windows source costs 3.7 ms to start and read, and four
// cost 15 — linear, all of it on the dock's thread at the moment the regions are first drawn,
// where the flyout that holds all four paid 3.7. And it went on costing: four sets of Core Audio
// callbacks for as long as the regions existed, and every change read back four times. So the
// regions share this: the first to look starts the one source, each change is read once however
// many regions are drawn from it, and the last to go stops it.
//
// The guarantee that a test never reaches the person's audio stays where it was put, in
// SoundParts.Source — this is made from it, not instead of it.

namespace Karpo.Dock.QuickSettings.Services;

internal sealed class SoundStore
{
    private readonly Func<ISoundSource> make;
    private readonly List<Action> watching = [];
    private ISoundSource? source;
    private Changes? changes;
    private SoundState state = new(true, 0, false, [], []);

    /// <param name="make">Where the source comes from when the first region looks.</param>
    public SoundStore(Func<ISoundSource> make) => this.make = make;

    /// <summary>
    /// The one every sound region in the dock reads: Windows' own source, or the
    /// samples on an isolated run (SoundParts.Source). A test hands a region a machine of its own
    /// instead, and that region gets a store of its own with it.
    /// </summary>
    internal static SoundStore Shared { get; } = new(SoundParts.Source);

    /// <summary>What the source last said; <see cref="Take"/> brings it up to date.</summary>
    public SoundState State => state;

    /// <summary>The source, for what a region's controls set. Only while something is watching.</summary>
    public ISoundSource Source => source ?? throw new InvalidOperationException("Nothing is watching the sound store.");

    /// <summary>The news the source posts and the controls add to, for what a region's controls set.</summary>
    public Changes Changes => changes ?? throw new InvalidOperationException("Nothing is watching the sound store.");

    /// <summary>How many regions are watching, for a test.</summary>
    internal int Watching => watching.Count;

    /// <summary>
    /// A region looks for the first time: it is woken with <paramref name="wake"/> when anything
    /// changes. The first to look starts the source and reads it once.
    /// </summary>
    public void Watch(Action wake)
    {
        lock (watching)
        {
            watching.Add(wake);
        }
        if (source is not null)
        {
            return;
        }
        source = make();
        // Posted from whichever thread the source hears Windows on; every region asks for its own
        // redraw, and the first to be drawn reads what changed for all of them (Take).
        changes = new Changes(_ =>
        {
            Action[] all;
            lock (watching)
            {
                all = [.. watching];
            }
            foreach (var each in all)
            {
                each();
            }
        });
        source.Start(changes);
        state = source.Read(0);
    }

    /// <summary>A region goes. The last to go stops the source, so nothing listens for nobody.</summary>
    public void Unwatch(Action wake)
    {
        lock (watching)
        {
            watching.Remove(wake);
            if (watching.Count > 0)
            {
                return;
            }
        }
        source?.Dispose();
        source = null;
        changes = null;
    }

    /// <summary>
    /// Reads what changed, once: the first region drawn after a change takes the news, and the rest
    /// find none and read nothing.
    /// </summary>
    public void Take()
    {
        if (source is not null && changes?.Take() is > 0 and var news)
        {
            state = source.Read(news);
        }
    }
}
