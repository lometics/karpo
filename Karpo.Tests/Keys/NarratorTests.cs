// What Karpo's windows tell a screen reader (Karpo.Dock\Accessibility): a bar's entries along it
// from its plan, a panel's controls, and the UI Automation providers answering through their own
// tables as a client would call them. No screen reader is needed and nothing is pressed: the bars
// are plans (Layout.ForTests), the panel is controls, and the providers read trees built here.

using System.Runtime.InteropServices;
using Karpo.Dock;
using Karpo.Dock.QuickSettings;
using Karpo.Dock.QuickSettings.Controls;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Tests.Keys;

public sealed unsafe class NarratorTests
{
    private static Config Bar() => new() { Height = 48 };

    private static PartSpec Spec(string id, BarKind kind) => new(Config.IslandKey(null, id), kind);

    /// <summary>A bar of every kind of entry: Start, search, three app buttons and the weather, the tray with two icons and its chevron, the clock, show desktop at the right.</summary>
    private static Layout Plan(Edge edge = Edge.Bottom, int length = 1600)
    {
        var config = Bar();
        var area = edge.IsVertical()
            ? new RECT { left = 0, top = 0, right = 1600, bottom = length }
            : new RECT { left = 0, top = 0, right = length, bottom = 1600 };
        List<PartSpec> specs =
        [
            Spec("start", BarKind.Start), Spec("search", BarKind.Search), Spec("apps", BarKind.Apps),
            Spec("weather", BarKind.FolderList),
            Spec("tray", BarKind.Tray) with { Section = BarSection.End }, Spec("clock", BarKind.NightLight) with { Section = BarSection.End },
            Spec("desktop", BarKind.ShowDesktop) with { Section = BarSection.End },
        ];
        return Layout.ForTests(96, area, edge, config, specs, Layout.CellPx(96, config), tray: 2, trayOverflow: true, tiles: 3);
    }

    private static float Along(Layout layout, Hit hit) => layout.Vertical ? layout.ItemRect(hit).Y : layout.ItemRect(hit).X;

    // --- A bar's tree -----------------------------------------------------------------------------

    /// <summary>
    /// Every entry of the bar is an element, in its order along the bar — across the bottom and down
    /// the side alike.
    /// </summary>
    [Theory]
    [InlineData(Edge.Bottom)]
    [InlineData(Edge.Right)]
    public void EveryEntryAlongTheBarIsAnElement(Edge edge)
    {
        var plan = Plan(edge);

        var entries = BarUia.Entries(plan, tiles: 3);

        Assert.Contains(entries, hit => hit.Kind == HitKind.Start);
        Assert.Contains(entries, hit => hit.Kind == HitKind.Search);
        Assert.Equal(3, entries.Count(hit => hit.Kind == HitKind.Tile));
        Assert.Contains(entries, hit => hit.Kind == HitKind.Island && hit.Part == 3);
        Assert.Equal(2, entries.Count(hit => hit.Kind == HitKind.Tray));
        Assert.Contains(Hit.TrayOverflow, entries);
        Assert.Contains(entries, hit => hit.Kind == HitKind.Island && hit.Part == 5);
        Assert.Contains(entries, hit => hit.Kind == HitKind.ShowDesktop);
        Assert.All(entries.Zip(entries.Skip(1)), pair => Assert.True(Along(plan, pair.First) <= Along(plan, pair.Second)));
    }

    /// <summary>
    /// In edit mode the bar's elements are its entries, in their order along it, and no "+": the
    /// adds are each display's add button's (Dock.EditDeck.cs), and a section's edit bar holds only
    /// what it cannot show (EditRailPlan).
    /// </summary>
    [Fact]
    public void InEditModeTheBarsElementsAreItsEntries()
    {
        var config = Bar();
        List<PartSpec> specs =
        [
            Spec("start", BarKind.Start), Spec("weather", BarKind.FolderList) with { Section = BarSection.End }, Spec("clock", BarKind.NightLight) with { Section = BarSection.End },
        ];
        var area = new RECT { right = 1600, bottom = 1600 };
        var editing = Layout.ForTests(96, area, Edge.Bottom, config, specs, Layout.CellPx(96, config), editing: true);

        var entries = BarUia.Entries(editing, tiles: 0);

        Assert.NotEmpty(entries);
        Assert.All(entries.Zip(entries.Skip(1)), pair => Assert.True(Along(editing, pair.First) <= Along(editing, pair.Second)));
        Assert.Empty(EditRailPlan.Parts());
    }

    /// <summary>An app button behind "…" is no element — it is nowhere on screen —, and "…" is.</summary>
    [Fact]
    public void AnAppBehindMoreIsNoElementAndMoreIs()
    {
        var config = Bar();
        var plan = Layout.ForTests(96, new RECT { right = 500, bottom = 1600 }, Edge.Bottom, config,
            [Spec("apps", BarKind.Apps), Spec("clock", BarKind.NightLight)], Layout.CellPx(96, config), tiles: 30);

        var entries = BarUia.Entries(plan, tiles: 30);

        Assert.NotEmpty(plan.Overflowed);
        Assert.Contains(Hit.AppOverflow, entries);
        Assert.DoesNotContain(entries, hit => hit.Kind == HitKind.Tile && plan.Overflowed.Contains(hit.Index));
    }

    /// <summary>
    /// The tree: a tool bar with an element for every entry that says something, by what it says —
    /// its name, its role, what it does, where it stands —, where the plan puts it, moved onto the
    /// screen; the entry the keyboard is on is the focused one, and the app buttons and the tray are
    /// where the keyboard can stand.
    /// </summary>
    [Fact]
    public void TheTreeSaysWhatEachEntryIsAndWhereItStands()
    {
        var plan = Plan();
        var entries = BarUia.Entries(plan, 3);
        var ids = new Dictionary<Hit, int>();
        UiaEntry? Describe(Hit hit) => hit.Kind switch
        {
            HitKind.Start => new UiaEntry("Start"),
            HitKind.Tile => new UiaEntry($"App {hit.Index}") { Status = hit.Index == 1 ? "2 running windows" : null },
            HitKind.Island => new UiaEntry("Airplane mode", UiaRole.Button, UiaPattern.Toggle) { On = true },
            _ => null,
        };
        int IdOf(Hit hit) => ids.TryGetValue(hit, out var id) ? id : ids[hit] = ids.Count + 1;
        var window = new RECT { left = 100, top = 1000, right = 1700, bottom = 1048 };

        var tree = BarUia.Tree(plan, "Taskbar", window, entries, Describe, IdOf, Hit.Tile(1), (100, 1000));

        Assert.Equal("Taskbar", tree.Root.Name);
        Assert.Equal(UiaRole.ToolBar, tree.Root.Role);
        Assert.Equal(["Start", "App 0", "App 1", "App 2", "Airplane mode", "Airplane mode"], tree.Root.Children.Select(node => node.Name));
        var app = tree.Root.Children[2];
        Assert.Equal("2 running windows", app.Status);
        Assert.Equal(UiaPattern.Invoke, app.Pattern);
        Assert.True(app.Focusable);
        Assert.Equal(app.Id, tree.Focused);
        var rect = plan.ItemRect(Hit.Tile(1));
        Assert.Equal(100 + (int)MathF.Floor(rect.X), app.Bounds.left);
        Assert.Equal(1000 + (int)MathF.Floor(rect.Y), app.Bounds.top);
        var toggle = tree.Root.Children[4];
        Assert.Equal((UiaPattern.Toggle, true), (toggle.Pattern, toggle.On));
        Assert.False(toggle.Focusable);
        Assert.Equal("App1", app.AutomationId);
    }

    /// <summary>
    /// tray: an icon's element stands where its square cell is — the bar's square, 44 by 44 DIP on the
    /// 48-DIP bar, as an app button's —, each beside the next, so Narrator's rectangle is the cell the
    /// icon is drawn in and pressed in. The chevron's the same.
    /// </summary>
    [Fact]
    public void ATrayIconsElementIsItsSquareCell()
    {
        var plan = Plan();
        var entries = BarUia.Entries(plan, 3);
        var ids = new Dictionary<Hit, int>();
        UiaEntry? Describe(Hit hit) => hit.Kind switch
        {
            HitKind.Tray => new UiaEntry($"Icon {hit.Index}"),
            HitKind.TrayOverflow => new UiaEntry("Show hidden icons"),
            _ => null,
        };
        int IdOf(Hit hit) => ids.TryGetValue(hit, out var id) ? id : ids[hit] = ids.Count + 1;
        var window = new RECT { left = 100, top = 1000, right = 1700, bottom = 1048 };

        var tree = BarUia.Tree(plan, "Taskbar", window, entries, Describe, IdOf, null, (100, 1000));

        Assert.Equal(["Show hidden icons", "Icon 0", "Icon 1"], tree.Root.Children.Select(node => node.Name));
        Assert.Equal(plan.CellSide, plan.TrayCell);
        foreach (var node in tree.Root.Children)
        {
            Assert.Equal((int)plan.TrayStep, node.Bounds.right - node.Bounds.left);
            Assert.Equal((int)plan.TrayCell, node.Bounds.bottom - node.Bounds.top);
        }
        var first = plan.ItemRect(Hit.Tray(0));
        Assert.Equal(100 + (int)MathF.Floor(first.X), tree.Root.Children[1].Bounds.left);
        // The next where its own cell is: flush on a dock, a card's gap on a bar of islands.
        Assert.Equal(100 + (int)MathF.Floor(plan.ItemRect(Hit.Tray(1)).X), tree.Root.Children[2].Bounds.left);
    }

    /// <summary>The tree walks as UI Automation walks it: up to the parent, along the siblings, down to the element under a point.</summary>
    [Fact]
    public void TheTreeWalksAsUiAutomationWalksIt()
    {
        var tree = new UiaTree(new UiaNode(0, "Bar", UiaRole.ToolBar, new RECT { right = 300, bottom = 40 })
        {
            Children =
            [
                new UiaNode(1, "One", UiaRole.Button, new RECT { left = 0, right = 100, bottom = 40 }),
                new UiaNode(2, "Two", UiaRole.Button, new RECT { left = 100, right = 200, bottom = 40 }),
                new UiaNode(3, "Three", UiaRole.Button, new RECT { left = 200, right = 300, bottom = 40 }),
            ],
        }, focused: 2);

        Assert.Equal("Bar", tree.Parent(2)?.Name);
        Assert.Equal("Three", tree.Sibling(2, 1)?.Name);
        Assert.Equal("One", tree.Sibling(2, -1)?.Name);
        Assert.Null(tree.Sibling(3, 1));
        Assert.Equal("Two", tree.At(150, 20)?.Name);
        Assert.Null(tree.At(500, 20));
        Assert.Equal(4, tree.All().Count());
    }

    // --- A panel's controls -------------------------------------------------------------------------

    /// <summary>
    /// A panel's controls say what they are: a button pressed, a check box and a switch toggled and
    /// where they stand, a slider's value and range, a list row's title and detail, a label read;
    /// a control with nothing to say is not there.
    /// </summary>
    [Fact]
    public void APanelsControlsSayWhatTheyAre()
    {
        var button = new Button("Connect");
        var box = new CheckBox("Metered connection", isChecked: true);
        var toggle = new ToggleSwitch { Name = "Bluetooth", On = false };
        var slider = new Slider { Name = "Volume", Minimum = 0, Maximum = 100, SmallChange = 2, LargeChange = 10, Value = 35 };
        var row = new ListRow { Title = "Headphones", Detail = "Connected" };
        var heading = new Label("Sound", LabelStyle.Strong);
        var content = new PanelContent(360, [heading], 40, [button, box, toggle, slider, row, new Label("")], 300, [], 0, 600);
        var ids = new Dictionary<Widget, int>(ReferenceEqualityComparer.Instance);

        var tree = PanelAccess.Tree(content, _ => new D2D_RECT_F(0, 0, 10, 10), (50, 60), toggle, widget => ids.TryGetValue(widget, out var id) ? id : ids[widget] = ids.Count + 1, out var byId);

        Assert.Equal("Sound", tree.Root.Name);
        var nodes = tree.Root.Children;
        Assert.Equal(["Sound", "Connect", "Metered connection", "Bluetooth", "Volume", "Headphones"], nodes.Select(node => node.Name));
        Assert.Equal((UiaRole.Button, UiaPattern.Invoke), (nodes[1].Role, nodes[1].Pattern));
        Assert.Equal((UiaRole.CheckBox, UiaPattern.Toggle, (bool?)true), (nodes[2].Role, nodes[2].Pattern, nodes[2].On));
        Assert.Equal((UiaPattern.Toggle, (bool?)false), (nodes[3].Pattern, nodes[3].On));
        Assert.Equal(new UiaRange(35, 0, 100, 2, 10), nodes[4].Range);
        Assert.Equal((UiaRole.ListItem, "Connected"), (nodes[5].Role, nodes[5].Status));
        Assert.Equal(UiaRole.Text, nodes[0].Role);
        Assert.Equal(nodes[3].Id, tree.Focused);
        Assert.Equal(nodes[3].Id, Uia.FocusTarget(tree));
        Assert.Equal(50, nodes[1].Bounds.left);
        Assert.Same(slider, byId[nodes[4].Id]);
    }

    /// <summary>
    /// A click on nothing in particular in a flyout leaves nothing in it with the keyboard (backlog
    /// 35): the focus event a screen reader hears is then the panel's own, so it knows the control it
    /// was on has lost it, rather than hearing nothing and still reading the box.
    /// </summary>
    [Fact]
    public void WithNothingFocusedThePanelItselfHasTheFocus()
    {
        var field = new Button("Connect");
        var content = new PanelContent(360, [new Label("Wi-Fi", LabelStyle.Strong)], 40, [field], 300, [], 0, 600);
        var ids = new Dictionary<Widget, int>(ReferenceEqualityComparer.Instance);

        var tree = PanelAccess.Tree(content, _ => new D2D_RECT_F(0, 0, 10, 10), (0, 0), null, widget => ids.TryGetValue(widget, out var id) ? id : ids[widget] = ids.Count + 1, out _);

        Assert.Equal(-1, tree.Focused);
        Assert.Equal(tree.Root.Id, Uia.FocusTarget(tree));
    }

    /// <summary>What a screen reader asks of a control is what the keys do: pressed is clicked, a check box ticks, a slider is set and says so.</summary>
    [Fact]
    public void WhatAScreenReaderAsksIsWhatTheKeysDo()
    {
        var pressed = 0;
        var button = new Button("Connect") { Clicked = () => pressed++ };
        var box = new CheckBox("Metered connection", isChecked: false);
        var told = new List<int>();
        var slider = new Slider { Name = "Volume", Maximum = 100, Changed = told.Add };

        PanelAccess.Do(button, UiaAct.Invoke, 0, null);
        PanelAccess.Do(box, UiaAct.Toggle, 0, null);
        PanelAccess.Do(slider, UiaAct.SetValue, 42, null);

        Assert.Equal(1, pressed);
        Assert.True(box.Checked);
        Assert.Equal(42, slider.Value);
        Assert.Equal([42], told);
    }

    // --- The providers ---------------------------------------------------------------------------------

    private sealed class Source(UiaTree tree) : IUiaSource
    {
        public UiaTree Build() => tree;

        public void Act(UiaAct act, int id, double value)
        {
        }
    }

    private static readonly Guid IFragment = new("f7063da8-8359-439c-9297-bbc5299a7d87");
    private static readonly Guid IFragmentRoot = new("620ce2a5-ab8f-40a9-86cb-de3c75599b58");
    private static readonly Guid IToggle = new("56d00bd0-c4f4-433c-a836-1a52a57e0892");
    private static readonly Guid IRange = new("36dc7aef-33e6-4691-afe1-2be7274b3d33");

    private static void* Query(void* unknown, Guid iid)
    {
        void* result = null;
        var hr = ((delegate* unmanaged<void*, Guid*, void**, int>)(*(void***)unknown)[0])(unknown, &iid, &result);
        return hr == 0 ? result : null;
    }

    private static string? Name(void* simple)
    {
        var variant = stackalloc byte[24];
        Assert.Equal(0, ((delegate* unmanaged<void*, int, void*, int>)(*(void***)simple)[5])(simple, 30005, variant));
        if (*(ushort*)variant != 8)
        {
            return null;
        }
        var text = *(char**)(variant + 8);
        var name = new string(text);
        SysFreeString(text);
        return name;
    }

    /// <summary>
    /// The providers answer through their own tables as a client calls them: the root is a fragment
    /// root, its first child is the first entry and walks on to the next, each says its name, a
    /// toggle where it stands and a slider its value, and the root gives the focused element —
    /// the tree the window built, read on the caller's thread.
    /// </summary>
    [Fact]
    public void TheProvidersAnswerAsAClientAsks()
    {
        var hwnd = CreateMessageWindow();
        try
        {
            var tree = new UiaTree(new UiaNode(0, "Taskbar", UiaRole.ToolBar, default)
            {
                Children =
                [
                    new UiaNode(1, "Start", UiaRole.Button, new RECT { right = 40, bottom = 40 }) { Pattern = UiaPattern.Invoke },
                    new UiaNode(2, "Airplane mode", UiaRole.Button, new RECT { left = 40, right = 80, bottom = 40 }) { Pattern = UiaPattern.Toggle, On = true },
                    new UiaNode(3, "Volume", UiaRole.Slider, new RECT { left = 80, right = 200, bottom = 40 }) { Pattern = UiaPattern.Range, Range = new UiaRange(35, 0, 100, 2, 10) },
                ],
            }, focused: 3);
            Uia.Register(hwnd, new Source(tree));
            Assert.NotNull(Uia.Current(hwnd));

            var root = Uia.Provider(hwnd, 0);
            Assert.Equal("Taskbar", Name(root));
            var fragmentRoot = Query(root, IFragmentRoot);
            Assert.True(fragmentRoot != null);
            var fragment = Query(root, IFragment);

            // FirstChild (3), then NextSibling (1), as Narrator walks the bar.
            void* first = null;
            Assert.Equal(0, ((delegate* unmanaged<void*, int, void**, int>)(*(void***)fragment)[3])(fragment, 3, &first));
            var firstSimple = Query(first, new Guid("d6dd68d1-86fd-4332-8666-9abedea2d24c"));
            Assert.Equal("Start", Name(firstSimple));
            Assert.True(Query(first, IFragmentRoot) == null);
            void* second = null;
            Assert.Equal(0, ((delegate* unmanaged<void*, int, void**, int>)(*(void***)first)[3])(first, 1, &second));

            // The toggle where it stands.
            var toggle = Query(second, IToggle);
            Assert.True(toggle != null);
            int state = -1;
            Assert.Equal(0, ((delegate* unmanaged<void*, int*, int>)(*(void***)toggle)[4])(toggle, &state));
            Assert.Equal(1, state);

            // The focused element, from the root: the slider, with its value.
            void* focused = null;
            Assert.Equal(0, ((delegate* unmanaged<void*, void**, int>)(*(void***)fragmentRoot)[4])(fragmentRoot, &focused));
            var range = Query(focused, IRange);
            double value = 0;
            Assert.Equal(0, ((delegate* unmanaged<void*, double*, int>)(*(void***)range)[4])(range, &value));
            Assert.Equal(35, value);

            // A button has no toggle to give.
            Assert.True(Query(first, IToggle) == null);

            foreach (var held in new[] { range, focused, toggle, second, firstSimple, first, fragment, fragmentRoot, root })
            {
                Uia.Unref(held);
            }
        }
        finally
        {
            Uia.Forget(hwnd);
            DestroyWindow(hwnd);
        }
        Assert.False(Uia.Registered(hwnd));
    }

    /// <summary>
    /// §520: the settings panel's patterns through their own tables: an expander that says it is expanded and is collapsed
    /// by its click's act, one shut that a Collapse leaves alone; a side item selected, the other selected by its click's act,
    /// its list its container; a button gives neither.
    /// </summary>
    [Fact]
    public void ThePanelsExpandersAndSideItemsAnswerAsAClientAsks()
    {
        var hwnd = CreateMessageWindow();
        try
        {
            var tree = new UiaTree(new UiaNode(0, "Settings", UiaRole.Pane, default)
            {
                Children =
                [
                    new UiaNode(1, "Pages", UiaRole.List, default)
                    {
                        Children =
                        [
                            new UiaNode(2, "Karpo", UiaRole.ListItem, default) { Pattern = UiaPattern.Select, On = true },
                            new UiaNode(3, "Appearance", UiaRole.ListItem, default) { Pattern = UiaPattern.Select, On = false },
                        ],
                    },
                    new UiaNode(4, "Accounts", UiaRole.Group, default) { Pattern = UiaPattern.Expand, On = true },
                    new UiaNode(5, "Formats", UiaRole.Group, default) { Pattern = UiaPattern.Expand, On = false },
                    new UiaNode(6, "Done", UiaRole.Button, default) { Pattern = UiaPattern.Invoke },
                ],
            });
            var acted = new List<(UiaAct, int)>();
            Uia.Register(hwnd, new Acting(tree, acted));
            Assert.NotNull(Uia.Current(hwnd));
            var expand = new Guid("d847d3a5-cab0-4a98-8c32-ecb45c59ad24");
            var select = new Guid("2acad808-b2d4-452d-a407-91ff1ad167b2");

            var accounts = Uia.Provider(hwnd, 4);
            var expanded = Query(accounts, expand);
            Assert.True(expanded != null);
            int state = -1;
            Assert.Equal(0, ((delegate* unmanaged<void*, int*, int>)(*(void***)expanded)[5])(expanded, &state));
            Assert.Equal(1, state);
            // Collapse (4): its click's act, posted to the window.
            Assert.Equal(0, ((delegate* unmanaged<void*, int>)(*(void***)expanded)[4])(expanded));
            var formats = Uia.Provider(hwnd, 5);
            var shut = Query(formats, expand);
            Assert.Equal(0, ((delegate* unmanaged<void*, int>)(*(void***)shut)[4])(shut));

            var look = Uia.Provider(hwnd, 3);
            var item = Query(look, select);
            Assert.True(item != null);
            int selected = -1;
            Assert.Equal(0, ((delegate* unmanaged<void*, int*, int>)(*(void***)item)[6])(item, &selected));
            Assert.Equal(0, selected);
            // Select (3): its click's act.
            Assert.Equal(0, ((delegate* unmanaged<void*, int>)(*(void***)item)[3])(item));
            void* container = null;
            Assert.Equal(0, ((delegate* unmanaged<void*, void**, int>)(*(void***)item)[7])(item, &container));
            Assert.Equal("Pages", Name(container));

            var done = Uia.Provider(hwnd, 6);
            Assert.True(Query(done, expand) == null && Query(done, select) == null);
            foreach (var held in new[] { expanded, accounts, shut, formats, item, look, container, done })
            {
                Uia.Unref(held);
            }
            // The acts posted to the window's thread: two, Accounts' and Look's (Formats' Collapse none).
            PumpPosted(hwnd);
            Assert.Equal([(UiaAct.Invoke, 4), (UiaAct.Invoke, 3)], acted);
        }
        finally
        {
            Uia.Forget(hwnd);
            DestroyWindow(hwnd);
        }
    }

    private sealed class Acting(UiaTree tree, List<(UiaAct, int)> acted) : IUiaSource
    {
        public UiaTree Build() => tree;

        public void Act(UiaAct act, int id, double value) => acted.Add((act, id));
    }

    /// <summary>What was posted to <paramref name="hwnd"/>, handed to Uia as the window's procedure hands it.</summary>
    private static void PumpPosted(HWND hwnd)
    {
        MSG msg;
        while (PeekMessageW(&msg, hwnd, 0, 0, PM.PM_REMOVE))
        {
            if (msg.message == Uia.WM_KARPO_UIA_ACT)
            {
                Uia.Act(hwnd, msg.wParam, msg.lParam);
            }
        }
    }

    /// <summary>A message-only window of the system's own class, which answers none of Karpo's messages: the providers read the tree the test built.</summary>
    private static HWND CreateMessageWindow()
    {
        fixed (char* className = "STATIC")
        {
            var hwnd = CreateWindowExW(0, className, null, 0, 0, 0, 0, 0, HWND.HWND_MESSAGE, HMENU.NULL, HINSTANCE.NULL, null);
            Assert.True(hwnd != HWND.NULL);
            return hwnd;
        }
    }
}
