// §635: what a widget shows where it is a picture of itself rather than the person's own: the add page's ghost (§593), the
// Appearance's preview and a layout's tile (LookStand). The person, 2026-11-09: "for the tides widget there is "no tidal data
// here". i dont want the widgets to take the current place for just the ghost preview, for the ghost preview they should take
// a place that certainly has the data to show available."
//
// **One set of samples, in the exe.** Every widget that shows something from far away — the weather and the air of a place,
// its tides, its sun, the earthquakes round it, a team's matches, a share's price, a feed's headlines — or from the person's
// accounts — the calendar, the inbox, the contacts — has a sample here: a place that certainly has it (Zürich's weather, air
// and pollen; Brest's sea; a league with a match on), made up and full. Built in, so a ghost is full the moment it stands: no
// network, no account, no location of this PC's is asked, and nothing can fail. The tests' saved answers are these same ones
// (Karpo.Tests\Forecasts\Saved*.cs ask here).
//
// **Which widgets show them.** A widget made while samples are shown (Show, On) reads them instead of its store, from its
// making on: each kind's own stand (a face's Stand, a line's Source, a store of its own) is the sample. The dock shows them
// for the add page's ghost: its key (Ghost) and every widget inside it (a ready-made group's lines) are made under Show by the
// one factory (Islands.Make). A widget made so keeps them (CellIsland.Sampled); the ghost put in, it is made again from the
// config (Dock.SyncIslands, FolderIsland.Configure), so the widget placed reads the person's own place and accounts at once.
//
// Thread: per thread, as the dock's widgets are made on its own thread; a test's on its own.

namespace Karpo.Dock;

/// <summary>§635: the samples a ghost and a preview show instead of the person's data (see the top of the file).</summary>
internal static partial class Samples
{
    [ThreadStatic]
    private static int showing;

    [ThreadStatic]
    private static string? ghost;

    /// <summary>Whether a widget made now on this thread shows the samples.</summary>
    public static bool On => showing > 0;

    /// <summary>
    /// The add page's ghost on this thread: the key of its widget (Config.IslandKey), whose widgets — it and everything inside
    /// it — are made showing the samples; null while none stands.
    /// </summary>
    public static string? Ghost
    {
        get => ghost;
        set => ghost = value;
    }

    /// <summary>Whether the widget of <paramref name="key"/> is the ghost's or inside it.</summary>
    public static bool Of(string key) =>
        ghost is { } of && key.StartsWith(of, StringComparison.OrdinalIgnoreCase)
        && (key.Length == of.Length || key[of.Length] == '/');

    /// <summary>Whether a widget of <paramref name="key"/> made now shows the samples: shown already, or the ghost's.</summary>
    public static bool For(string key) => On || Of(key);

    /// <summary>
    /// Whether <paramref name="island"/> of <paramref name="key"/> was made showing the samples and is no ghost's now — the ghost
    /// put in —, so it is made again, reading the person's own.
    /// </summary>
    public static bool Outlived(IIsland island, string key) => island is CellIsland { Sampled: true } && !For(key);

    /// <summary>The samples shown for whatever is made until the scope ends; scopes nest.</summary>
    public static Scope Show() => new(true);

    /// <summary>The samples shown while the widget of <paramref name="key"/> is made, where it is the ghost's.</summary>
    public static Scope Show(string key) => new(Of(key));

    /// <summary>While it lasts, widgets made on this thread show the samples.</summary>
    public readonly struct Scope : IDisposable
    {
        private readonly bool shown;

        internal Scope(bool shown)
        {
            this.shown = shown;
            if (shown)
            {
                showing++;
            }
        }

        public void Dispose()
        {
            if (shown)
            {
                showing--;
            }
        }
    }
}
