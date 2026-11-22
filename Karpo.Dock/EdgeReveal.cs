// The hover places along one edge of a display come and go together: the pointer reaching any
// of their strips brings in every filled place on that edge, they stay while it is over any of
// them or a flyout or menu of one holds it, and they go together once it has left them all.
// Each window keeps its own reveal and settle; this only widens them to the window's edge, apart
// from any window, so it can be tested.

using Karpo.Core;

namespace Karpo.Dock;

/// <summary>A window as the reveal of its edge sees it.</summary>
internal interface IEdgeWindow
{
    /// <summary>The display the window is on.</summary>
    string Display { get; }

    /// <summary>The place along an edge it shows; null for a display's own bar or its anchor, which go alone.</summary>
    EdgePlace? Place { get; }

    bool Shown { get; }

    /// <summary>Something keeps the window up: the pointer, a flyout or menu of its own, a drag, edit mode.</summary>
    bool Holds { get; }

    /// <summary>Comes in: in sight, all of it, and taking the pointer over its bar, not only its strip.</summary>
    void Show();

    /// <summary>Goes back out of sight.</summary>
    void Hide();
}

internal static class EdgeReveal
{
    /// <summary>
    /// The other windows on <paramref name="window"/>'s edge of its display; none for a window that
    /// is not a place.
    /// </summary>
    public static IEnumerable<T> Siblings<T>(T window, IEnumerable<T> all) where T : class, IEdgeWindow =>
        window.Place is { } place
            ? all.Where(other => !ReferenceEquals(other, window)
                && other.Place is { } at && at.Edge == place.Edge
                && string.Equals(other.Display, window.Display, StringComparison.Ordinal))
            : [];

    /// <summary>Brings in <paramref name="window"/> and every place on its edge not in sight yet.</summary>
    public static void Reveal<T>(T window, IEnumerable<T> all) where T : class, IEdgeWindow
    {
        if (!window.Shown)
        {
            window.Show();
        }
        foreach (var other in Siblings(window, all).ToList())
        {
            if (!other.Shown)
            {
                other.Show();
            }
        }
    }

    /// <summary>
    /// <paramref name="window"/>'s time to go has come: it goes, and the rest of its edge with it,
    /// unless anything on the edge still holds it up. Returns whether they went.
    /// </summary>
    public static bool Settle<T>(T window, IEnumerable<T> all) where T : class, IEdgeWindow
    {
        var siblings = Siblings(window, all).ToList();
        if (window.Holds || siblings.Exists(other => other.Holds))
        {
            return false;
        }
        window.Hide();
        foreach (var other in siblings)
        {
            if (other.Shown)
            {
                other.Hide();
            }
        }
        return true;
    }
}
