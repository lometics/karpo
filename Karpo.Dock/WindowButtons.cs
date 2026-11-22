// §667: where a window's button is on Karpo's bars, for Windows' minimize and restore animations (MinRect.cs).
//
// Each bar publishes what it laid out as it is laid out again and as its buttons' windows change (Dock.DockWindow.PublishButtons):
// every app button with the windows it stands for, a folder with the windows its pins have open, "…" with the windows of
// the buttons it holds, and its display and panel. All in screen px, which are the physical px Windows' own rectangle is
// in. Replaced whole, never changed in place: MinRect's thread reads it without the dock, which may be busy for longer than
// Windows waits for an answer.
//
// A window's place is the button on the bar of its own display that stands for it; else one on another bar, the main
// display's first; where no bar shows it at all, the middle of its own display's bar, else of the main one's — never
// Explorer's hidden taskbar at the bottom.

using Karpo.Core;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

/// <summary>§667: something on a bar that stands for windows — an app button, a folder, "…" — on the screen, in px.</summary>
internal sealed record ButtonPlace(RECT Rect, IReadOnlyList<nint> Windows);

/// <summary>
/// §667: one bar's places as it last laid them out: its display (<paramref name="Display"/>), whether that is the main one,
/// its panel and what on it stands for windows, all in screen px.
/// </summary>
internal sealed record BarButtons(RECT Display, bool Main, RECT Panel, IReadOnlyList<ButtonPlace> Places);

/// <summary>§667: every bar's places, as the dock published them (see the top of WindowButtons.cs).</summary>
internal sealed unsafe class WindowButtons
{
    /// <summary>The dock's.</summary>
    public static readonly WindowButtons Shared = new();

    private readonly Lock gate = new();
    private Dictionary<nint, BarButtons> bars = [];
    private IReadOnlyList<BarButtons> all = [];

    /// <summary>Every bar's places now.</summary>
    public IReadOnlyList<BarButtons> Bars => Volatile.Read(ref all);

    /// <summary>Bar <paramref name="bar"/>'s places now; null for a bar gone or showing none.</summary>
    public void Publish(nint bar, BarButtons? buttons)
    {
        lock (gate)
        {
            var next = new Dictionary<nint, BarButtons>(bars);
            if (buttons is null)
            {
                _ = next.Remove(bar);
            }
            else
            {
                next[bar] = buttons;
            }
            bars = next;
            Volatile.Write(ref all, [.. next.Values]);
        }
    }

    /// <summary>
    /// Read off Windows: where <paramref name="window"/>'s button is — its own, else that of the first of its owners that has
    /// one (a dialog minimized with its app, WindowOwners) —, on its display as Windows has it; null where Karpo shows no bar.
    /// </summary>
    public RECT? For(HWND window)
    {
        var now = Bars;
        if (now.Count == 0)
        {
            return null;
        }
        var button = WindowOwners.ButtonOf(window, at => Holds(now, (nint)at));
        return Of(now, (nint)button, DisplayOf(button));
    }

    /// <summary>
    /// A bar's places as <paramref name="layout"/> lays them out: each app button (<paramref name="tiles"/>, the windows of
    /// each, in the layout's order) where it stands, a button the bar had no room for at "…", and each folder holding pins
    /// with windows open (<paramref name="folders"/>, by the folder's key) at its button. Its display
    /// (<paramref name="display"/>, the main one where <paramref name="main"/>) and panel with them.
    /// </summary>
    internal static BarButtons Laid(Layout layout, IReadOnlyList<IReadOnlyList<nint>> tiles, IReadOnlyDictionary<string, IReadOnlyList<nint>> folders, RECT display, bool main)
    {
        var places = new List<ButtonPlace>();
        var overflowed = new List<nint>();
        for (var index = 0; index < tiles.Count; index++)
        {
            if (tiles[index].Count == 0)
            {
                continue;
            }
            var button = layout.ItemRect(Hit.Tile(index));
            if (button.IsEmpty)
            {
                overflowed.AddRange(tiles[index]);
            }
            else
            {
                places.Add(new ButtonPlace(layout.OnScreen(button), [.. tiles[index]]));
            }
        }
        for (var part = 0; part < layout.Specs.Count; part++)
        {
            if (layout.Specs[part].Kind.IsFolder()
                && folders.TryGetValue(layout.Specs[part].Key, out var open) && open.Count > 0
                && layout.BlockRect(part) is { IsEmpty: false } folder)
            {
                places.Add(new ButtonPlace(layout.OnScreen(folder), [.. open]));
            }
        }
        if (overflowed.Count > 0 && layout.ItemRect(Hit.AppOverflow) is { IsEmpty: false } more)
        {
            places.Add(new ButtonPlace(layout.OnScreen(more), overflowed));
        }
        return new BarButtons(display, main, layout.OnScreen(layout.PanelRect), places);
    }

    /// <summary>Whether a place on one of <paramref name="bars"/> stands for <paramref name="window"/>.</summary>
    internal static bool Holds(IReadOnlyList<BarButtons> bars, nint window) =>
        bars.Any(bar => bar.Places.Any(place => place.Windows.Contains(window)));

    /// <summary>
    /// Where <paramref name="window"/>'s button is among <paramref name="bars"/>, its display being <paramref name="display"/>
    /// (null where not known): the place standing for it on a bar of that display, else on another bar, the main display's
    /// first; else the middle of that display's bar, else of the main one's, else of any (<see cref="Middle"/>). Null for no bars.
    /// </summary>
    internal static RECT? Of(IReadOnlyList<BarButtons> bars, nint window, RECT? display)
    {
        // Stable: bars of one rank keep the order they were published in.
        var ranked = bars.OrderBy(bar => display is { } on && Inside(Centre(bar.Display), on) ? 0 : bar.Main ? 1 : 2).ToList();
        foreach (var bar in ranked)
        {
            foreach (var place in bar.Places)
            {
                if (place.Windows.Contains(window))
                {
                    return place.Rect;
                }
            }
        }
        return ranked.Count > 0 ? Middle(ranked[0].Panel) : null;
    }

    /// <summary>The middle of a bar's panel: a square as thick as the bar, centred on it.</summary>
    internal static RECT Middle(RECT panel)
    {
        var side = Math.Min(panel.right - panel.left, panel.bottom - panel.top);
        var x = panel.left + ((panel.right - panel.left - side) / 2);
        var y = panel.top + ((panel.bottom - panel.top - side) / 2);
        return new RECT { left = x, top = y, right = x + side, bottom = y + side };
    }

    private static POINT Centre(RECT rect) => new() { x = rect.left + ((rect.right - rect.left) / 2), y = rect.top + ((rect.bottom - rect.top) / 2) };

    private static bool Inside(POINT point, RECT rect) => point.x >= rect.left && point.x < rect.right && point.y >= rect.top && point.y < rect.bottom;

    /// <summary>The display <paramref name="window"/> is on, or nearest — where it will be restored, while it is minimized —; null where Windows can't say.</summary>
    private static RECT? DisplayOf(HWND window)
    {
        var monitor = MonitorFromWindow(window, (uint)MONITOR.MONITOR_DEFAULTTONEAREST);
        var info = new MONITORINFO { cbSize = (uint)sizeof(MONITORINFO) };
        return monitor != HMONITOR.NULL && GetMonitorInfoW(monitor, &info) ? info.rcMonitor : null;
    }
}
