// What a screen reader is told about a window of Karpo's: a tree of elements, each with a name, a
// role (UI Automation's control type), where it is on screen, whether it can be used, and what it
// does — invoked like a button, toggled like a switch, set like a slider. Narrator and every other
// UI Automation client read it through the providers in Uia.cs.
//
// A tree is a snapshot, made on the window's own thread from what the window shows (a bar's layout
// and widgets, a flyout's rows or controls) and read on UI Automation's threads, which never touch
// the window's state: what an element does is posted back to the window's thread by its id.
//
// A bar's tree (BarUia) is every entry along the bar, in its order: the Start button, search, task
// view and Windows' other buttons, each app button, "…", each widget's cells, the tray's icons and
// its chevron, the clock, show desktop, in edit mode each section's "+".

using Karpo.Core;
using TerraFX.Interop.Windows;

namespace Karpo.Dock;

/// <summary>An element's role: UI Automation's control type ids, the ones Karpo's windows have.</summary>
public enum UiaRole
{
    Button = 50000,
    CheckBox = 50002,

    /// <summary>§520: a dropdown of the settings panel: it expands and collapses.</summary>
    ComboBox = 50003,
    Edit = 50004,

    /// <summary>§545: the settings panel's Get Karpo Pro, a link.</summary>
    Hyperlink = 50005,

    /// <summary>§536: a look's preview in the settings panel: a picture, named for what it shows.</summary>
    Image = 50006,
    ListItem = 50007,
    List = 50008,
    Menu = 50009,
    MenuItem = 50011,

    /// <summary>§545: a meter of the settings panel's side-list footer: how much of a limit is placed.</summary>
    ProgressBar = 50012,
    Slider = 50015,

    /// <summary>§520: a number box of the settings panel: a range stepped by its value.</summary>
    Spinner = 50016,

    /// <summary>§592: a tab strip of the settings panel (the Appearance's): its tabs in it, one selected.</summary>
    Tab = 50018,

    /// <summary>§592: one tab of a strip: selected among its strip's (SelectionItem).</summary>
    TabItem = 50019,
    Text = 50020,
    ToolBar = 50021,
    Group = 50026,
    Window = 50032,
    Pane = 50033,
}

/// <summary>What an element does beyond being there: UI Automation's control patterns, the ones Karpo's elements have.</summary>
public enum UiaPattern
{
    /// <summary>Nothing: text, or a group.</summary>
    None,

    /// <summary>Pressed like a button (InvokePattern).</summary>
    Invoke,

    /// <summary>Switched on and off (TogglePattern); <see cref="UiaNode.On"/> is where it stands.</summary>
    Toggle,

    /// <summary>Set to a value in a range (RangeValuePattern); <see cref="UiaNode.Range"/> is it.</summary>
    Range,

    /// <summary>§520: expands and collapses (UI Automation's ExpandCollapse): a dropdown, an expander, the panel itself; <see cref="UiaNode.On"/> open.</summary>
    Expand,

    /// <summary>§520: an item selected among its list's (SelectionItem): a side item, a segment; <see cref="UiaNode.On"/> selected.</summary>
    Select,

    /// <summary>§523: words that are its value, read and set (ValuePattern): a menu's line of text, a colour's hex box; <see cref="UiaNode.Value"/> they.</summary>
    Value,
}

/// <summary>A slider's value and range, as RangeValuePattern gives them.</summary>
public readonly record struct UiaRange(double Value, double Minimum, double Maximum, double SmallChange, double LargeChange);

/// <summary>
/// One element of a window's tree. <paramref name="Id"/> is the window's own for it, the same from
/// one snapshot to the next while it is the same thing, so the focus and a screen reader's place
/// stay on it; <paramref name="Bounds"/> are screen px.
/// </summary>
public sealed record UiaNode(int Id, string Name, UiaRole Role, RECT Bounds)
{
    public UiaPattern Pattern { get; init; }

    /// <summary>For a toggle: on, off, or null where it is not known (the indeterminate state).</summary>
    public bool? On { get; init; }

    /// <summary>For a slider.</summary>
    public UiaRange? Range { get; init; }

    /// <summary>§523: for words that are its value (<see cref="UiaPattern.Value"/>), they.</summary>
    public string? Value { get; init; }

    public bool Enabled { get; init; } = true;

    /// <summary>Whether the keyboard can stand on it.</summary>
    public bool Focusable { get; init; }

    /// <summary>What it is doing, beyond its name: "2 windows open", "3 unread messages".</summary>
    public string? Status { get; init; }

    /// <summary>A longer description, read on request.</summary>
    public string? Help { get; init; }

    /// <summary>A name for tools and tests that does not change with the language or the state.</summary>
    public string AutomationId { get; init; } = "";

    /// <summary>§338: a password's field (IsPassword): a screen reader says "password" and never reads it out.</summary>
    public bool Password { get; init; }

    /// <summary>§552: a heading's level (HeadingLevel, 1 to 9: a screen reader's way from one section to the next); 0 for none.</summary>
    public int Heading { get; init; }

    public IReadOnlyList<UiaNode> Children { get; init; } = [];
}

/// <summary>A window's elements, and which one has the keyboard.</summary>
public sealed class UiaTree
{
    private readonly Dictionary<int, (UiaNode Node, UiaNode? Parent, int Index)> index = [];

    /// <param name="root">The window itself; its id is 0.</param>
    /// <param name="focused">The element with the keyboard; -1 for none.</param>
    public UiaTree(UiaNode root, int focused = -1)
    {
        Root = root;
        Focused = focused;
        Add(root, null, 0);
    }

    public UiaNode Root { get; }

    public int Focused { get; }

    /// <summary>A tree with nothing in it, for a window that has shown nothing yet.</summary>
    public static UiaTree Empty(string name, UiaRole role) => new(new UiaNode(0, name, role, default));

    public UiaNode? Find(int id) => index.TryGetValue(id, out var entry) ? entry.Node : null;

    public UiaNode? Parent(int id) => index.TryGetValue(id, out var entry) ? entry.Parent : null;

    /// <summary>The element <paramref name="step"/> places along from <paramref name="id"/> among its parent's children; null past either end.</summary>
    public UiaNode? Sibling(int id, int step)
    {
        if (!index.TryGetValue(id, out var entry) || entry.Parent is not { } parent)
        {
            return null;
        }
        var at = entry.Index + step;
        return at >= 0 && at < parent.Children.Count ? parent.Children[at] : null;
    }

    /// <summary>Every element, the root first and then in the order a screen reader walks them.</summary>
    public IEnumerable<UiaNode> All() => Walk(Root);

    /// <summary>The innermost element under the screen point, the root's children first; null where only the root is.</summary>
    public UiaNode? At(int x, int y)
    {
        UiaNode? found = null;
        var nodes = Root.Children;
        while (true)
        {
            var under = nodes.FirstOrDefault(node => x >= node.Bounds.left && x < node.Bounds.right && y >= node.Bounds.top && y < node.Bounds.bottom);
            if (under is null)
            {
                return found;
            }
            found = under;
            nodes = under.Children;
        }
    }

    private static IEnumerable<UiaNode> Walk(UiaNode node)
    {
        yield return node;
        foreach (var child in node.Children)
        {
            foreach (var inner in Walk(child))
            {
                yield return inner;
            }
        }
    }

    private void Add(UiaNode node, UiaNode? parent, int at)
    {
        index[node.Id] = (node, parent, at);
        for (var child = 0; child < node.Children.Count; child++)
        {
            Add(node.Children[child], node, child);
        }
    }
}

/// <summary>What a bar's entry says about itself to a screen reader: everything but where it is, which the plan knows.</summary>
public sealed record UiaEntry(string Name, UiaRole Role = UiaRole.Button, UiaPattern Pattern = UiaPattern.Invoke)
{
    public bool? On { get; init; }

    public UiaRange? Range { get; init; }

    public bool Enabled { get; init; } = true;

    public string? Status { get; init; }

    public string? Help { get; init; }
}

/// <summary>A bar's tree, from its plan (see the top of the file).</summary>
internal static class BarUia
{
    /// <summary>
    /// The things of the bar <paramref name="layout"/> plans, along it: every entry it shows, with
    /// what it is — its <paramref name="tiles"/> app buttons, each tray's own icons, and the cells of
    /// every widget, and in edit mode its sections' "+" (<see cref="Layout.Adds"/>). What the bar has no
    /// room for is not there: "…" is.
    /// </summary>
    public static List<Hit> Entries(Layout layout, int tiles)
    {
        var hits = new List<Hit>();
        for (var part = 0; part < layout.Specs.Count; part++)
        {
            if (!layout.Placed(part))
            {
                continue;
            }
            var kind = layout.Specs[part].Kind;
            switch (kind)
            {
                case BarKind.Start:
                    hits.Add(Hit.Start with { Part = part });
                    break;
                case BarKind.Search:
                    hits.Add(Hit.Search with { Part = part });
                    break;
                case BarKind.TaskView:
                    hits.Add(Hit.TaskView with { Part = part });
                    break;
                case BarKind.ShowDesktop:
                    hits.Add(new Hit(HitKind.ShowDesktop, 0, part));
                    break;
                case BarKind.Apps or BarKind.Pin:
                    hits.AddRange(Enumerable.Range(0, tiles).Select(Hit.Tile).Where(tile => layout.PartOf(tile) == part));
                    if (layout.PartOf(Hit.AppOverflow) == part)
                    {
                        hits.Add(Hit.AppOverflow);
                    }
                    break;
                // tray: each of the bar's trays, the whole tray of its own icons (§347), where it holds two.
                case BarKind.Tray:
                    hits.Add(layout.InTray(Hit.TrayOverflow, part));
                    hits.AddRange(Enumerable.Range(0, layout.PartAt(part).Count).Select(icon => layout.InTray(Hit.Tray(icon), part)));
                    break;
                default:
                    if (BarEntries.IsWindowsButton(kind))
                    {
                        hits.Add(Hit.WindowsButton(kind) with { Part = part });
                    }
                    else
                    {
                        hits.AddRange(Enumerable.Range(0, layout.CellCount(part)).Select(cell => Hit.Cell(part, cell)));
                    }
                    break;
            }
        }
        hits.AddRange(Enumerable.Range(0, layout.Adds().Count).Select(Hit.Add));
        return [.. hits
            .Select(hit => (Hit: hit, Rect: layout.ItemRect(hit)))
            .Where(entry => !entry.Rect.IsEmpty)
            .OrderBy(entry => layout.Vertical ? entry.Rect.Y : entry.Rect.X)
            .Select(entry => entry.Hit)];
    }

    /// <summary>
    /// The bar's tree: a tool bar named <paramref name="name"/>, and under it an element for every
    /// entry <paramref name="describe"/> says something about, where the plan puts it — its window px
    /// moved to the screen by <paramref name="origin"/> —, numbered by <paramref name="idOf"/>. The
    /// entry the keyboard is on (<paramref name="focused"/>) is the focused element.
    /// </summary>
    public static UiaTree Tree(
        Layout layout,
        string name,
        RECT window,
        IReadOnlyList<Hit> entries,
        Func<Hit, UiaEntry?> describe,
        Func<Hit, int> idOf,
        Hit? focused,
        (float X, float Y) origin)
    {
        var children = new List<UiaNode>(entries.Count);
        var focusedId = -1;
        foreach (var hit in entries)
        {
            if (describe(hit) is not { } entry)
            {
                continue;
            }
            var rect = layout.ItemRect(hit);
            var id = idOf(hit);
            var stop = hit.Kind is HitKind.Tile or HitKind.AppOverflow or HitKind.Tray or HitKind.TrayOverflow;
            children.Add(new UiaNode(id, entry.Name, entry.Role, new RECT
            {
                left = (int)MathF.Floor(origin.X + rect.X),
                top = (int)MathF.Floor(origin.Y + rect.Y),
                right = (int)MathF.Ceiling(origin.X + rect.X + rect.Width),
                bottom = (int)MathF.Ceiling(origin.Y + rect.Y + rect.Height),
            })
            {
                Pattern = entry.Pattern,
                On = entry.On,
                Range = entry.Range,
                Enabled = entry.Enabled,
                Status = entry.Status,
                Help = entry.Help,
                Focusable = stop,
                AutomationId = AutomationIdOf(hit),
            });
            if (focused == hit)
            {
                focusedId = id;
            }
        }
        return new UiaTree(new UiaNode(0, name, UiaRole.ToolBar, window) { Children = children, AutomationId = "Karpo.Bar" }, focusedId);
    }

    /// <summary>What an entry is called for tools: its kind and place, never its words.</summary>
    public static string AutomationIdOf(Hit hit) => hit.Kind switch
    {
        HitKind.Tile => $"App{hit.Index}",
        HitKind.Tray => hit.Part >= 0 ? $"Tray{hit.Part}.{hit.Index}" : $"Tray{hit.Index}",
        HitKind.TrayOverflow when hit.Part >= 0 => $"TrayOverflow{hit.Part}",
        HitKind.Island => $"Widget{hit.Part}.{hit.Index}",
        HitKind.WindowsButton => ((BarKind)hit.Index).ToString(),
        HitKind.Add => $"Add{hit.Index}",
        _ => hit.Kind.ToString(),
    };
}
