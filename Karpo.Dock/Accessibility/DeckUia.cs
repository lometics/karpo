// narrator, §520, §575: the edit bar and its flyout as a screen reader is given them (docs\widgets.md §520 decision 11, §575).
// The bar a tool bar named "Edit": its things in the order they stand — the grip, each page's entry a button that expands
// and collapses (its flyout open or not), the search box, the errors, the selection's entries, Add (that expands too),
// Done. The flyout, a window of its own, a pane named for its page: its title's buttons, each section a group under its
// heading's name, each of its controls by its row's name — a toggle switch that toggles, a number box a range that steps, a
// dropdown and an expander that expand and collapse, a segment that is selected, §592 a tab strip a tab list of its tabs, each
// selected or not, the rest buttons —; the add list's search
// an edit; or the search's finds a list. A greyed row is in the tree, said to be off (the menus left greyed rows out).
//
// An element's id is its place among what the keys and the pointer reach in its window (DeckUia.Things), so what it does is
// the click's on that thing (EditDeck.Act).

using TerraFX.Interop.Windows;

namespace Karpo.Dock;

/// <summary>§520: the bar's and the panel's UI Automation tree (see the top of the file).</summary>
internal static class DeckUia
{
    /// <summary>
    /// Every thing a screen reader may act on in the bar's window (<paramref name="bar"/>) or §575 the flyout's, in reading
    /// order, with its name, role and what it is: the bar's entries and buttons; the flyout's add list's search, the page's title
    /// buttons, then every control of the page in its order.
    /// </summary>
    /// <summary>§670: the choice or the mixer of an inline picker's body <paramref name="row"/> that <paramref name="hit"/> is.</summary>
    private static FlyoutItem? Cell(PanelRow row, PanelHit hit) => row.Item.Children?.FirstOrDefault(kid => kid.Id == hit.Id);

    public static List<(PanelHit Hit, string Name, UiaRole Role, UiaPattern Pattern, bool? On, bool Enabled, PanelLine? Line)> Things(PanelLayout layout, bool bar)
    {
        List<(PanelHit, string, UiaRole, UiaPattern, bool?, bool, PanelLine?)> things = [];
        if (bar)
        {
            foreach (var part in layout.Header)
            {
                var hit = PanelLayout.HitOf(part);
                // §575: an entry, and Add, a button that expands and collapses: its flyout open or not.
                var expands = part.Kind is PanelHitKind.Nav or PanelHitKind.Add;
                things.Add((hit, part.Words, UiaRole.Button, expands ? UiaPattern.Expand : UiaPattern.Invoke, expands ? layout.Opened(hit) : null,
                    part.Kind != PanelHitKind.Add || layout.Input.CanAdd, null));
            }
            return things;
        }
        if (!layout.Filter.IsEmpty)
        {
            things.Add((new PanelHit(PanelHitKind.Filter), S.DockSearchWidgets, UiaRole.Edit, UiaPattern.None, null, true, null));
        }
        // §628: a page under another's entry its way back, a button saying where it goes back from ("Back, Add a layout"), as
        // BackHead says it.
        if (!layout.BackHead.IsEmpty)
        {
            things.Add((new PanelHit(PanelHitKind.Back), S.DockPageBack + ", " + (layout.Shown?.Title ?? ""), UiaRole.Button, UiaPattern.Invoke, null, true, null));
        }
        foreach (var (item, _) in layout.Actions)
        {
            things.Add((new PanelHit(PanelHitKind.TitleAct, item.Id, Key: item.Text), item.Text, UiaRole.Button, UiaPattern.Invoke, null, item.Enabled, null));
        }
        foreach (var line in layout.Blocks.SelectMany(block => block.Lines))
        {
            var row = line.Row;
            // §536: a look's preview an image, named for what it previews: nothing to press, never the keys'.
            if (row.Shows is { } shows)
            {
                things.Add((new PanelHit(PanelHitKind.Preview, Key: row.Key), LookPreview.NameOf(shows), UiaRole.Image, UiaPattern.None, null, true, line));
                continue;
            }
            foreach (var (hit, _) in layout.Controls(line))
            {
                // §610: the row the control is — its line's pair's own (a holiday calendar's region, the palette's reset, §613 a
                // value's reset) —, greyed as it is. §554, §609: a thing's one choice (a calendar's region), in its thing's line or
                // under it, named for the thing as its buttons are ("Region · Switzerland"): several calendars' never read alike.
                var of = row.Own(hit.Key);
                var whose = layout.HolderOf(hit.Key) is { Kind: PanelRowKind.Item } thing ? " · " + thing.Text : "";
                // A number box is one range, stepped by setting its value (its − is the + 's other way, EditDeck.Act).
                // §521: its value typed in place is that range's too.
                if (hit.Kind is PanelHitKind.Fewer or PanelHitKind.Number)
                {
                    continue;
                }
                var (name, role, pattern, on) = hit.Kind switch
                {
                    // §528: several widgets mixed: Indeterminate, as a check box's third state.
                    PanelHitKind.Toggle => (row.Text, UiaRole.Button, UiaPattern.Toggle, row.Mixed ? null : (bool?)row.On),
                    PanelHitKind.More => (row.Text, UiaRole.Spinner, UiaPattern.Range, null),
                    // §592: a tab, selected or not, in its strip (Tree: a tab list round them).
                    PanelHitKind.Tab => (row.Rows[hit.Index].Text, UiaRole.TabItem, UiaPattern.Select, hit.Index == layout.Chosen(row)),
                    PanelHitKind.Tool => (row.Tools[hit.Index].Name + " · " + row.Text, UiaRole.Button, UiaPattern.Invoke, null),
                    PanelHitKind.Pop => (of.Text, UiaRole.ComboBox, UiaPattern.Expand, false),
                    // §521: a text box, its words its status (a password's bullets).
                    PanelHitKind.Text => (row.Text, UiaRole.Edit, UiaPattern.None, null),
                    // §552: a face's section with nothing to set folds nothing.
                    PanelHitKind.Head when row is { Kind: PanelRowKind.Group, Folds: false } => (row.Text, UiaRole.Group, UiaPattern.None, null),
                    PanelHitKind.Head => (row.Text, UiaRole.Group, UiaPattern.Expand, layout.StandsOpen(row)),
                    // §529: a mixed row's widgets' own values, shown or not.
                    PanelHitKind.Each => (S.DockPanelEach + " · " + row.Text, UiaRole.Button, UiaPattern.Expand, layout.Input.Open?.Contains(PanelGroups.EachOf(row.Key)) == true),
                    // §551: a row put in order's handle: how it is moved, its row's name; where it stands its status ("3 of 7").
                    PanelHitKind.Handle => (S.DockEditFaceDrag + " · " + row.Text, UiaRole.Button, UiaPattern.None, null),
                    // §610: a button beside a control its whole name ("Reset Graphite to default", §613 "Reset Background to
                    // default"), as its tooltip.
                    // §670: an inline picker's choice an item of its list, selected where it is in force; its colour's spectrum
                    // and tracks sliders, named for their picker ("Hue · Background").
                    PanelHitKind.Act when row.Kind == PanelRowKind.PickerBody && Cell(row, hit) is { } choice => (choice.Text, UiaRole.ListItem, UiaPattern.Select, choice.Checked == true),
                    PanelHitKind.Mix => ((Cell(row, hit)?.Text ?? "") + " · " + row.Text, UiaRole.Slider, UiaPattern.None, null),
                    PanelHitKind.Act when !ReferenceEquals(of, row) => (of.Detail ?? of.Text, UiaRole.Button, UiaPattern.Invoke, null),
                    _ => (row.Text, UiaRole.Button, UiaPattern.Invoke, null),
                };
                things.Add((hit, hit.Kind is PanelHitKind.Tool or PanelHitKind.Each ? name : name + whose, role, pattern, on, !of.Greyed, line));
            }
        }
        return things;
    }


    /// <summary>A number box's range: its value the first number it says; a step of 1, one less or more wherever it can step.</summary>
    public static UiaRange Range(FlyoutControl.Stepper stepper)
    {
        var digits = new string([.. stepper.Value.SkipWhile(c => !char.IsDigit(c)).TakeWhile(c => char.IsDigit(c) || c is '.' or ',')]);
        var value = double.TryParse(digits.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var number) ? number : 0;
        return new UiaRange(value, stepper.CanFewer ? value - 1 : value, stepper.CanMore ? value + 1 : value, 1, 1);
    }

    /// <summary>What element <paramref name="id"/> of the bar's tree (<paramref name="bar"/>) or the flyout's is (its place among <see cref="Things"/>, from 1); none for another.</summary>
    public static PanelHit HitOf(PanelLayout layout, int id, bool bar)
    {
        var things = Things(layout, bar);
        return id >= 1 && id <= things.Count ? things[id - 1].Hit : PanelHit.None;
    }

    /// <summary>The tree of <paramref name="layout"/>'s bar (<paramref name="bar"/>), or its flyout, standing at <paramref name="panel"/> (screen px).</summary>
    public static UiaTree Tree(PanelLayout? layout, RECT panel, PanelHit focus, bool bar = true)
    {
        if (layout is null || panel.right <= panel.left)
        {
            return new UiaTree(new UiaNode(0, bar ? S.CoreBarMenuEdit : S.DockEditSettings, bar ? UiaRole.ToolBar : UiaRole.Pane, panel));
        }
        RECT Screen(RectF r) => new() { left = panel.left + (int)r.X, top = panel.top + (int)r.Y, right = panel.left + (int)MathF.Ceiling(r.Right), bottom = panel.top + (int)MathF.Ceiling(r.Bottom) };
        var things = Things(layout, bar);
        UiaNode Node(int index)
        {
            var (hit, name, role, pattern, on, enabled, line) = things[index];
            var rect = layout.RectOf(hit);
            if (hit.Kind == PanelHitKind.More && line?.Row.Stepper is { } stepper)
            {
                // The number box as a whole: its value the number it says, a step either way where it can step.
                rect = layout.OnPanel(layout.Controls(line).Select(each => each.Rect).Aggregate((a, b) => new RectF(MathF.Min(a.X, b.X), a.Y, MathF.Max(a.Right, b.Right) - MathF.Min(a.X, b.X), a.Height)));
            }
            var range = line?.Row.Stepper is { } steps && hit.Kind == PanelHitKind.More ? Range(steps) : (UiaRange?)null;
            // §568, §609, §610: a pair's control says its own row's value (a holiday calendar's region's, in the calendar's line).
            var row = layout.HolderOf(hit.Key) is not null ? layout.Row(hit.Key) : line?.Row;
            return new UiaNode(index + 1, name, role, Screen(rect))
            {
                Pattern = pattern,
                On = on,
                Range = range,
                Enabled = enabled,
                Focusable = hit.Kind is not PanelHitKind.Preview,
                // §583: a dropdown's choice its mark too ("Graphite, Changed"); §586 an add page's tile or folder what its
                // number is, "3 in this layout"; §595 an expander's head its badge, "Default".
                Status = hit.Kind == PanelHitKind.Handle && layout.PlaceOf(hit.Key) is { } place ? PanelReorder.Position(place.At, place.Count)
                    // §641: an add page's tile marked Beta says so, after how many the layout has.
                    : row is { Kind: PanelRowKind.Pick, Badge: { } beta } ? string.Join(", ", new[] { row.Item.Placed > 0 ? S.DockAddPlaced(row.Item.Placed) : null, beta }.OfType<string>())
                    : row?.Item.Placed is > 0 and var placed ? S.DockAddPlaced(placed)
                    : row is { Value: { } value, ValueMark: { } mark } ? value + ", " + mark
                    : hit.Kind == PanelHitKind.Head && row?.Badge is { } badge ? string.Join(", ", new[] { row.Value, badge }.OfType<string>())
                    : row?.Value,
                // §529: a group's sentence whole, cut short as it may be drawn. §610 a button beside a dropdown (the palette's
                // reset) nothing more: its whole name is its name.
                Help = row is { Kind: PanelRowKind.Action } && !ReferenceEquals(row, line?.Row) ? null : row?.Sentence ?? row?.Detail,
                AutomationId = $"Panel.{hit.Kind}.{(hit.Kind == PanelHitKind.Nav ? hit.Page.ToString() : hit.Key)}.{hit.Index}",
                // §552: a group's head (a widget's What it shows, a folder's face) a heading under the page's.
                Heading = hit.Kind == PanelHitKind.Head && line?.Row.Kind == PanelRowKind.Group ? 2 : 0,
            };
        }
        // The thing with the keys: its element's id; none (-1) while they are nowhere in it.
        int Focused() => things.FindIndex(thing => thing.Hit == focus) is var at and >= 0 ? at + 1 : -1;
        if (bar)
        {
            return new UiaTree(new UiaNode(0, S.CoreBarMenuEdit, UiaRole.ToolBar, panel) { Children = [.. Enumerable.Range(0, things.Count).Select(Node)] }, Focused());
        }
        var head = Enumerable.Range(0, things.Count).Where(at => things[at].Hit.Kind is PanelHitKind.TitleAct or PanelHitKind.Filter or PanelHitKind.Back).Select(Node).ToList();
        // The page's controls, in groups under their sections' headings (a group of the page's own before the first).
        List<UiaNode> groups = [];
        var section = layout.Shown?.Side ?? "";
        List<UiaNode> members = [];
        var groupId = 100_000;
        var tabsId = 200_000;
        void Close()
        {
            if (members.Count > 0)
            {
                groups.Add(new UiaNode(groupId++, section, UiaRole.Group, Screen(layout.Viewport)) { Children = [.. members] });
                members.Clear();
            }
        }
        foreach (var block in layout.Blocks)
        {
            if (block.Row.Kind == PanelRowKind.Heading)
            {
                Close();
                section = block.Row.Text;
                continue;
            }
            foreach (var line in block.Lines)
            {
                List<UiaNode> nodes = [];
                for (var at = 0; at < things.Count; at++)
                {
                    if (ReferenceEquals(things[at].Line, line))
                    {
                        nodes.Add(Node(at));
                    }
                }
                // §592: a tab strip a tab list named for its page, its tabs in it; §673 its button (the palette's reset) after it.
                if (line.Row.Kind == PanelRowKind.Tabs)
                {
                    members.Add(new UiaNode(tabsId++, layout.Shown?.Side ?? "", UiaRole.Tab, Screen(layout.OnPanel(line.Rect))) { Children = [.. nodes.Where(node => node.Role == UiaRole.TabItem)] });
                    members.AddRange(nodes.Where(node => node.Role != UiaRole.TabItem));
                    continue;
                }
                members.AddRange(nodes);
            }
        }
        Close();
        return new UiaTree(new UiaNode(0, layout.Shown?.Title ?? "", UiaRole.Pane, panel) { Children = [.. head, .. groups] }, Focused());
    }
}
