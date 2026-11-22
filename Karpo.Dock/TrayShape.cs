// When a change of the tray icons needs the bars built again, and when it
// doesn't: the dock asks on every change an app sends, and most are a new
// picture or tooltip of an icon already there. Pure, for the dock and its tests.

namespace Karpo.Dock;

internal static class TrayShape
{
    /// <summary>
    /// Whether a bar built for the tray <paramref name="shown"/> / <paramref name="hidden"/> lays out,
    /// hit-tests and publishes its icons' places exactly as one built for
    /// <paramref name="nextShown"/> / <paramref name="nextHidden"/> would: the same icons drawn (the
    /// shown ones, which is what Scene draws) in the same order with a picture where there was one,
    /// and the overflow chevron there or not alike. Then only pictures and tooltips changed,
    /// and <see cref="Scene.SetTray"/> paints the new pictures in place.
    /// </summary>
    public static bool Same(
        IReadOnlyList<TrayIcon> shown,
        IReadOnlyList<TrayIcon> hidden,
        IReadOnlyList<TrayIcon> nextShown,
        IReadOnlyList<TrayIcon> nextHidden)
    {
        // tray: every shown icon is drawn, and the chevron stands for what is behind it. Both used
        // to ask TrayIcon.Hidden — Windows' own choice for the icon — again, although the split
        // had already decided with it: an icon the person kept on the bar against that choice was
        // then left out of the comparison, and the bar was never built again for it.
        if (hidden.Count > 0 != nextHidden.Count > 0 || shown.Count != nextShown.Count)
        {
            return false;
        }
        for (var index = 0; index < shown.Count; index++)
        {
            var (was, now) = (shown[index], nextShown[index]);
            if (was.Key != now.Key || (was.Icon is null) != (now.Icon is null))
            {
                return false;
            }
        }
        return true;
    }
}
