// §347: one tray widget's icons as it shows them now — on the bar, and behind its chevron — split by its
// own states and order (BarItem.TrayIcons, TrayLayout.Split). Every placed tray has one (Dock.ReadTray),
// by its key, and everything that reads a tray's icons reads its own: the bar's cells and pictures
// (Content.Trays), a click, a hover and a tooltip on one of them, the keys and Narrator, the overflow its
// chevron opens, and a drag along it.

using Karpo.Core;

namespace Karpo.Dock;

/// <summary>A tray's icons on its bar and behind its chevron, in order (see the top of the file).</summary>
internal sealed record TraySplit(List<TrayIcon> Shown, List<TrayIcon> Hidden)
{
    /// <summary>A tray with no icons: one that isn't placed, or no tray running.</summary>
    public static TraySplit Empty { get; } = new([], []);

    /// <summary>
    /// <paramref name="icons"/> as the tray of <paramref name="options"/> shows them (<see cref="TrayLayout.Split"/>).
    /// Two icons of one app share an identity: each entry stands for one of them, in their order.
    /// </summary>
    public static TraySplit Of(TrayIconOptions? options, IReadOnlyList<TrayIcon> icons)
    {
        var byIdentity = new Dictionary<string, Queue<TrayIcon>>(StringComparer.OrdinalIgnoreCase);
        foreach (var icon in icons)
        {
            if (!byIdentity.TryGetValue(icon.Identity, out var same))
            {
                byIdentity[icon.Identity] = same = new Queue<TrayIcon>();
            }
            same.Enqueue(icon);
        }
        var (shown, overflow) = TrayLayout.Split(options, [.. icons.Select(icon => new TrayEntry(icon.Identity, icon.Hidden))]);
        List<TrayIcon> Take(List<TrayEntry> entries) =>
            [.. entries.Select(entry => byIdentity.TryGetValue(entry.Identity, out var same) && same.Count > 0 ? same.Dequeue() : null).OfType<TrayIcon>()];
        return new TraySplit(Take(shown), Take(overflow));
    }

    /// <summary>Whether <paramref name="other"/> holds the same icons in the same places and order.</summary>
    public bool Same(TraySplit other) => Shown.SequenceEqual(other.Shown) && Hidden.SequenceEqual(other.Hidden);
}
