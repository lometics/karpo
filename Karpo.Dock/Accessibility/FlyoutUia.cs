// narrator: what Karpo's flyouts tell a screen reader (UiaTree.cs, Uia.cs).
//
//   A menu (Flyout: a right click's menu, a jump list, "…"'s list, the tray's hidden icons) is a
//   menu of items: each row its text, its detail as its status, a check mark as a toggle, pressed
//   as chosen; headers and separators say nothing of their own. The row the keys or the pointer
//   are on is the focused one.
//
//   A panel of controls (QuickPanel: the quick settings, the weather, a share, the drives, the
//   mail, the contacts, search) is a pane of its controls in their order: a button or a list row
//   pressed as clicked, a check box and a switch toggled, a slider set, a text field and a label
//   read. The control with the keyboard is the focused one.
//
// The calendar, a countdown's panel, the notification list, the desktops' flyout and a folder draw
// their own content and have no tree yet.

using System.Numerics;
using Karpo.Dock.QuickSettings;
using Karpo.Dock.QuickSettings.Controls;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

/// <summary>A panel of controls' tree, and what a screen reader asks of its controls, done as the keys would.</summary>
internal sealed class PanelAccess(QuickPanel panel) : IUiaSource
{
    private readonly Dictionary<Widget, int> ids = new(ReferenceEqualityComparer.Instance);
    private Dictionary<int, Widget> widgets = [];

    // The last id given: each control keeps its own as long as the panel shows it, and a new one is never an old one's.
    private int lastId;

    public UiaTree Build()
    {
        var on = panel.Host.PanelOnScreen;
        var tree = Tree(panel.Content, widget => panel.PanelRect(widget), (on.left, on.top), panel.Focused, IdOf, out widgets, on);
        // A control the panel no longer shows lets go of its id: a panel a screen reader reads all day
        // would otherwise hold every control it ever showed (its pages are made anew as they open).
        if (ids.Count > widgets.Count)
        {
            var shown = widgets.Values.ToHashSet(ReferenceEqualityComparer.Instance);
            foreach (var gone in ids.Keys.Where(widget => !shown.Contains(widget)).ToList())
            {
                ids.Remove(gone);
            }
        }
        return tree;
    }

    /// <summary>
    /// The tree of what <paramref name="content"/> places, each control where <paramref name="rectOf"/>
    /// puts it in the panel, moved to the screen by <paramref name="origin"/>. A test builds it from a
    /// page it laid out. <paramref name="panelOnScreen"/>: where the panel stands, the root's bounds (§355:
    /// its window spans the display); none for the window's own.
    /// </summary>
    internal static UiaTree Tree(
        PanelContent content,
        Func<Widget, TerraFX.Interop.DirectX.D2D_RECT_F> rectOf,
        (int X, int Y) origin,
        Widget? focused,
        Func<Widget, int> idOf,
        out Dictionary<int, Widget> byId,
        RECT panelOnScreen = default)
    {
        byId = [];
        var children = new List<UiaNode>();
        var focusedId = -1;
        string? title = null;
        foreach (var widget in content.Header.Concat(content.Body).Concat(content.Footer).Concat(content.Above))
        {
            if (!widget.Visible || Describe(widget) is not { } node)
            {
                continue;
            }
            title ??= widget is Label && content.Header.Contains(widget) ? node.Name : null;
            var rect = rectOf(widget);
            var id = idOf(widget);
            byId[id] = widget;
            children.Add(node with
            {
                Id = id,
                Bounds = new RECT
                {
                    left = origin.X + (int)MathF.Floor(rect.left),
                    top = origin.Y + (int)MathF.Floor(rect.top),
                    right = origin.X + (int)MathF.Ceiling(rect.right),
                    bottom = origin.Y + (int)MathF.Ceiling(rect.bottom),
                },
            });
            if (ReferenceEquals(widget, focused))
            {
                focusedId = id;
            }
        }
        return new UiaTree(new UiaNode(0, title ?? "Karpo", UiaRole.Pane, panelOnScreen) { Children = children, AutomationId = "Karpo.Panel" }, focusedId);
    }

    /// <summary>What a control says about itself: its name, its role and what it does; null for one with nothing to say.</summary>
    internal static UiaNode? Describe(Widget widget)
    {
        var name = widget.Name;
        UiaNode Node(string text, UiaRole role, UiaPattern pattern) =>
            new(0, text, role, default) { Pattern = pattern, Enabled = widget.Enabled, Focusable = widget.Focusable, AutomationId = widget.Name };
        switch (widget)
        {
            case Button button:
                return Node(button.Text.Length > 0 ? button.Text : name, UiaRole.Button, UiaPattern.Invoke);
            // A head that is the way back says where it goes: "Back, Wi-Fi".
            case BackHead head:
                return Node(head.Said, UiaRole.Button, UiaPattern.Invoke);
            case CheckBox box:
                return Node(box.Text.Length > 0 ? box.Text : name, UiaRole.CheckBox, UiaPattern.Toggle) with { On = box.Checked };
            case ToggleSwitch toggle:
                return Node(name, UiaRole.Button, UiaPattern.Toggle) with { On = toggle.On };
            case Slider slider:
                return Node(name, UiaRole.Slider, UiaPattern.Range) with
                {
                    Range = new UiaRange(slider.Value, slider.Minimum, slider.Maximum, slider.SmallChange, slider.LargeChange),
                };
            case ListRow row:
                return Node(row.Title.Length > 0 ? row.Title : name, UiaRole.ListItem, widget.Clicked is null ? UiaPattern.None : UiaPattern.Invoke) with
                {
                    Status = string.Join(", ", new[] { row.Detail, row.Trailing }.Where(part => !string.IsNullOrWhiteSpace(part))) is { Length: > 0 } said ? said : null,
                };
            case TextField field:
                return Node(name, UiaRole.Edit, UiaPattern.None);
            case Label label:
                return label.Text.Trim().Length > 0 ? Node(label.Text.Trim(), UiaRole.Text, UiaPattern.None) : null;
        }
        // Anything else that is pressed is a button by its name; the rest is drawing.
        return name.Length > 0 && widget.Clicked is not null ? Node(name, UiaRole.Button, UiaPattern.Invoke) : null;
    }

    public void Act(UiaAct act, int id, double value)
    {
        if (!widgets.TryGetValue(id, out var widget) || !widget.Enabled || !widget.Visible)
        {
            return;
        }
        Do(widget, act, value, panel);
        panel.Flush();
    }

    /// <summary>What a screen reader asked of <paramref name="widget"/>, done as the keys would (a test calls it without a panel).</summary>
    internal static void Do(Widget widget, UiaAct act, double value, QuickPanel? panel)
    {
        switch (act)
        {
            case UiaAct.Invoke or UiaAct.Toggle:
                // What Enter and Space do (QuickPanel.Navigate): a check box's click ticks it.
                widget.Clicked?.Invoke();
                break;
            case UiaAct.SetValue when widget is Slider slider:
            {
                var level = (int)Math.Round(value);
                slider.Value = level;
                slider.Changed?.Invoke(slider.Value);
                slider.Settled?.Invoke(slider.Value);
                break;
            }
            case UiaAct.Focus:
                panel?.Focus(widget, visible: true);
                break;
            case UiaAct.Menu when widget.ContextClicked is { } menu && panel is not null:
            {
                var box = panel.PanelRect(widget);
                menu(new Vector2((box.left + box.right) / 2, (box.top + box.bottom) / 2));
                break;
            }
        }
    }

    private int IdOf(Widget widget)
    {
        if (!ids.TryGetValue(widget, out var id))
        {
            id = ++lastId;
            ids[widget] = id;
        }
        return id;
    }
}
