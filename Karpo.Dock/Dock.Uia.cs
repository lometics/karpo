// narrator: what a bar tells a screen reader (Accessibility\UiaTree.cs, Accessibility\Uia.cs): every
// entry along it, by the name Windows' taskbar would give it, and what a press, a switch or a
// slider does there, done as the pointer would do it.
//
//   Start, Search, Task View, Windows' buttons   buttons, pressed as clicked
//   an app button                                its app or its window's title; how many windows,
//                                                and what the app shows on it (TileSignal.Spoken)
//   "…", the chevron, a tray icon                buttons; a tray icon hears what the keyboard sends
//   a widget's cell                              its Tooltip's first line, or why this PC hasn't
//                                                got it (the rest is its help; the bar shows neither);
//                                                a switch a toggle, the volume a slider
//   the clock, show desktop                      buttons
//
// Its menu (UI Automation's ShowContextMenu, Narrator's Shift+F10) is the right click's. The
// keyboard's place (Win+T, Win+B; Dock.Keys.cs) is the focused element.

using Karpo.Core;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

public sealed unsafe partial class Dock
{
    /// <summary>narrator: what <paramref name="hit"/> of <paramref name="window"/> says about itself; null for nothing to say.</summary>
    private UiaEntry? Describe(DockWindow window, Hit hit)
    {
        switch (hit.Kind)
        {
            case HitKind.Start:
                return new UiaEntry(S.DockUiaStart);
            case HitKind.Search:
                return new UiaEntry(S.DockUiaSearch);
            case HitKind.TaskView:
                return new UiaEntry(S.DockMenuTaskView);
            case HitKind.WindowsButton:
                return new UiaEntry(BarEntries.Find((BarKind)hit.Index)?.Info.Title ?? ((BarKind)hit.Index).ToString());
            case HitKind.Tile when window.TileAt(hit.Index) is { } tile:
                return new UiaEntry(tile.Label ?? tile.Item.Name) { Status = TileStatus(tile), Help = tile.Broken };
            case HitKind.AppOverflow:
                return new UiaEntry(S.DockUiaMoreApps);
            case HitKind.TrayOverflow:
                return new UiaEntry(S.DockUiaHiddenIcons);
            case HitKind.Tray when TrayIconAt(window, hit) is { } icon:
            {
                var (first, rest) = Lines(icon.Tip);
                return new UiaEntry(first.Length > 0 ? first : S.DockUiaNotificationIcon) { Help = rest };
            }
            case HitKind.ShowDesktop:
                return new UiaEntry(S.DockUiaShowDesktop);
            case HitKind.Island:
                return DescribeCell(window, hit);
            // edit: a section's "+", named for its section as the sections' edit bars named theirs.
            case HitKind.Add when hit.Index >= 0 && hit.Index < window.Layout.Adds().Count:
                return new UiaEntry(AddList.HereIn(window.Layout.Adds()[hit.Index].Section, window.Layout.Vertical));
        }
        return null;
    }

    /// <summary>narrator: a widget's cell: its tooltip's first line, and what it is — a switch, a slider or a button.</summary>
    private UiaEntry? DescribeCell(DockWindow window, Hit hit)
    {
        var specs = window.Layout.Specs;
        if (hit.Part < 0 || hit.Part >= specs.Count)
        {
            return null;
        }
        var kind = specs[hit.Part].Kind;
        var title = BarEntries.Find(kind)?.Info.Title ?? kind.ToString();
        islands.TryGetValue(specs[hit.Part].Key, out var island);
        // One whose thing this PC hasn't got says why (§247), which the bar shows no tooltip for (§367).
        var (first, rest) = Lines((island as CellIsland)?.UnavailableOn(window.Layout.Screen) ?? island?.Tooltip(hit.Index));
        var name = first.Length > 0 ? first : title;
        return island switch
        {
            SwitchIsland toggle => new UiaEntry(title, UiaRole.Button, UiaPattern.Toggle) { On = toggle.On, Enabled = toggle.Available, Help = first.Length > 0 ? string.Join("\n", new[] { first, rest }.Where(line => line is { Length: > 0 })) : null },
            VolumeIsland volume => new UiaEntry(title, UiaRole.Slider, UiaPattern.Range)
            {
                Range = new UiaRange(volume.Level, 0, 100, 2, 10),
                Status = volume.IsMuted ? S.DockUiaMuted : null,
            },
            // The network's face with a VPN connected says so in its name, as Windows' own icon does.
            FolderIsland { Vpn: { } vpn } => new UiaEntry(S.WidgetSystemControlVpnSpoken(name: name, vpn: vpn)) { Help = rest },
            _ => new UiaEntry(name) { Help = rest },
        };
    }

    /// <summary>What an app button is doing, as a screen reader adds it to its name: its windows, and what the app shows on it.</summary>
    private static string? TileStatus(Tile tile)
    {
        var parts = new List<string>(4);
        var windows = tile.Window != HWND.NULL ? 1 : tile.Open.Count;
        if (windows == 0)
        {
            parts.Add(tile.Pinned ? S.DockUiaPinned : "");
        }
        else if (tile.Window == HWND.NULL)
        {
            parts.Add(S.DockUiaRunningWindows(windows));
        }
        if (tile.Mark.Active)
        {
            parts.Add(S.DockUiaActive);
        }
        if (tile.Mark.Attention)
        {
            parts.Add(S.DockUiaAttention);
        }
        if (tile.Mark.Signal.Spoken is { Length: > 0 } spoken)
        {
            parts.Add(spoken);
        }
        var said = Listed(parts.Where(part => part.Length > 0));
        return said.Length > 0 ? said : null;
    }

    /// <summary>
    /// What a screen reader hears of several things at once: "Pinned, wants attention", each joined
    /// to the next as the language writes a list (Chinese with its own comma); "" for none.
    /// </summary>
    internal static string Listed(IEnumerable<string> parts) =>
        parts.Aggregate("", (said, part) => said.Length == 0 ? part : S.DockUiaJoin(first: said, next: part));

    /// <summary>A tooltip's first line, and the rest as one.</summary>
    private static (string First, string? More) Lines(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return ("", null);
        }
        var lines = text.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return lines.Length == 0 ? ("", null) : (lines[0], lines.Length > 1 ? string.Join(", ", lines[1..]) : null);
    }

    /// <summary>narrator: what UI Automation asked of <paramref name="hit"/> of <paramref name="window"/>, done as the pointer or the keyboard would.</summary>
    private void ActFor(DockWindow window, UiaAct act, Hit hit, double value)
    {
        // edit: on the bar being arranged a widget pressed or switched by UI Automation does what a
        // click on its block does — its settings, a folder opened and its settings (EditClicks.cs) — and never its
        // own act, as the pointer and Enter never reach it then. A "+" pressed opens its add list.
        if (hit.Kind == HitKind.Add)
        {
            if (Arranging(window) && act == Karpo.Dock.UiaAct.Invoke)
            {
                AddAtPlus(window, hit.Index);
            }
            return;
        }
        if (Arranging(window) && EditClicks.ByAutomation(act, hit.Kind))
        {
            if (window.Layout.PartOf(hit) is var part and >= 0)
            {
                EditActivate(window, part, null);
            }
            return;
        }
        switch (act)
        {
            case Karpo.Dock.UiaAct.Invoke or Karpo.Dock.UiaAct.Toggle:
                if (hit.Kind == HitKind.Tray)
                {
                    KeyAct(window, hit, menu: false);
                }
                else
                {
                    Click(window, hit, middle: false);
                }
                break;
            case Karpo.Dock.UiaAct.SetValue:
                if (hit.Kind == HitKind.Island && hit.Part >= 0 && hit.Part < window.Layout.Specs.Count
                    && islands.TryGetValue(window.Layout.Specs[hit.Part].Key, out var island) && island is VolumeIsland volume)
                {
                    volume.SetLevel((int)Math.Round(value));
                }
                break;
            // keys: any stop of the bar (KeyStops), as Tab and the arrows reach it.
            case Karpo.Dock.UiaAct.Focus:
                EnterKeysAt(window, hit);
                break;
            case Karpo.Dock.UiaAct.Menu:
                if (hit.Kind is HitKind.Tile or HitKind.Tray)
                {
                    KeyAct(window, hit, menu: true);
                }
                else
                {
                    var rect = window.Layout.ItemRect(hit);
                    Context(window, hit, rect.X + (rect.Width / 2f), rect.Y + (rect.Height / 2f));
                }
                break;
        }
    }

    private sealed partial class DockWindow
    {
        // narrator: the id each entry has had, by what it is (BarUia.AutomationIdOf), so an entry
        // keeps its element from one tree to the next; and the entry of each id in the last tree.
        private readonly Dictionary<string, int> uiaIds = new(StringComparer.Ordinal);
        private Dictionary<int, Hit> uiaHits = [];
        private bool uiaRegistered;

        /// <summary>narrator: this bar's elements are its own from its first build on.</summary>
        private void RegisterUia()
        {
            if (uiaRegistered)
            {
                return;
            }
            uiaRegistered = true;
            Uia.Register(Hwnd, new BarAccess(this));
        }

        /// <summary>narrator: this bar's tree, as it stands (BarUia.Tree).</summary>
        private UiaTree BuildUia()
        {
            var entries = BarUia.Entries(layout, Tiles.Count);
            // §358: the bar's own rectangle, in the window that stands still round it.
            var window = new RECT { left = layout.X, top = layout.Y, right = layout.X + layout.Width, bottom = layout.Y + layout.Height };
            var hits = new Dictionary<int, Hit>();
            int IdOf(Hit hit)
            {
                var key = BarUia.AutomationIdOf(hit);
                if (!uiaIds.TryGetValue(key, out var id))
                {
                    id = uiaIds.Count + 1;
                    uiaIds[key] = id;
                }
                hits[id] = hit;
                return id;
            }
            var tree = BarUia.Tree(
                layout,
                monitor.Primary ? S.DockUiaTaskbar : S.DockUiaTaskbarOn(Name),
                window,
                entries,
                hit => dock.Describe(this, hit),
                IdOf,
                keyHit,
                (window.left, window.top));
            uiaHits = hits;
            return tree;
        }

        private sealed class BarAccess(DockWindow bar) : IUiaSource
        {
            public UiaTree Build() => bar.BuildUia();

            public void Act(UiaAct act, int id, double value)
            {
                if (bar.uiaHits.TryGetValue(id, out var hit))
                {
                    bar.dock.ActFor(bar, act, hit, value);
                }
            }
        }
    }
}
