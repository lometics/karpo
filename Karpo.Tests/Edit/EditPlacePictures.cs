// Edit mode where the bars stand (the person, 2026-10-08: "edit mode doesn't shrink the screen any
// more"): a display's layout's bars edited where they stand at rest, over the display dimmed (EditShade:
// 40 % black over a made-up desktop), each bar and each place a window of its own as the dock lays
// them out (Dock.StandingEdges, HoverPlaces.BarArea, HoverPlaces.For with editing, Config.WithEdge,
// Layout's Only): a bar shown all the time along its edge, and every place of a bar that comes in on
// hover brought in — the filled ones with their widgets, the empty ones as an empty block of three
// cells (Layout.EmptyRooms) —; a dock's three sections, each a dock of its own. By the layout's first
// bar its edit bar, a compact command bar — "Add a widget", "Folder",
// "Open settings", "Close edit mode" (EditDeck, the real window) — 8 DIPs off the bar,
// at a taskbar's end or a dock's centre (EditDeckPlan.Place, 2026-10-08); beside a section
// with widgets it cannot show, its edit bar of one button (EditRailPlan). And mid-drag: a widget
// carried from one surface to another, the surface it left closed up behind it, the one it goes to
// opening the room for it with a blue ghost of its size there, and the widget itself at the pointer
// in a window of its own, over everything (the carry window, drawn here from the same visual surface
// of the lifted block).
//
// A display 1280 DIPs wide with another against its right edge, at 150 %, dark, showing a layout of
// each type: a Taskbar along the bottom holding its strip, and a Dock along the top that comes in when
// the pointer comes near. To KARPO_PNG_DIR as edit-inplace-<taskbar-bottom|dock-top>-dark-150.png; the
// taskbar with a widget carried from its bar to its end, as edit-inplace-taskbar-bottom-drag-<dark|light>-150.png.
//
// Several selected (Dock.EditSelect.cs): on a Taskbar along the bottom that comes in on hover, its three
// places brought in, three widgets of two places ringed, the selection rectangle drawn round the
// first's, as edit-select-<dark|light>-<150|100>.png; and the three carried together into the middle's
// place, empty before: the places they left closed up, one landing round all three there, each at the
// pointer side by side in the carry window, each on its bar's ground (§625: without it a light bar's
// widgets came back black, the dim under them), as edit-select-group-<dark|light>-<150|100>.png. A rectangle begun on the
// start place's own room, in the gap before Start and drawn into Task view, the two ringed:
// edit-select-inside-<look>-<scale>.png. (§488: these stood on a Top and bottom until that type went.)
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Tests.Islands;
using TheDock = Karpo.Dock.Dock;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using Windows.UI.Composition;
using Windows.UI.Composition.Desktop;
using static TerraFX.Interop.Windows.Windows;
using Color = Windows.UI.Color;
using Vector2 = System.Numerics.Vector2;
using Vector3 = System.Numerics.Vector3;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    /// <summary>
    /// A widget carried in a picture: from the surface along <c>From</c> holding it — a place's section,
    /// or with null the bar shown all the time there — to a place or a bar's section along <c>To</c>.
    /// </summary>
    private sealed record Carry(Edge From, BarSection? FromPlace, BarKind Kind, Edge To, BarSection ToPlace);

    /// <summary>
    /// Several widgets selected in a picture, the first the one last clicked: each by the edge and the
    /// place (null for the bar's) it stands in and its kind; with <c>Band</c> the selection rectangle
    /// drawn round the first one's place, with <c>Carry</c> all of them carried together into the
    /// empty middle place; with <c>Inside</c> the rectangle begun on the bar's own room, round them alone.
    /// </summary>
    private sealed record Selection(IReadOnlyList<(Edge Edge, BarSection? Place, BarKind Kind)> Widgets, bool Band = false, bool Carry = false, bool Inside = false);

    /// <summary>One surface of a display in edit mode: a bar shown all the time, or a place.</summary>
    private sealed record Surface(RectI Area, Edge Edge, List<BarItem> Items, Config Config, Look Look, BarSection? Only, bool Standing);

    /// <summary>A layout a picture shows, on a display whose bar stands on <paramref name="Screen"/> (the position is the display's).</summary>
    private sealed record Picture(TaskbarLayout Layout, Edge Screen)
    {
        public LayoutType Type => Layout.Type;

        public List<BarItem> Islands => Layout.Islands;

        public IReadOnlyList<Edge> Bars => [Screen];

        public ScreenEdges Edges() => Layout.Edges(Screen);
    }

    /// <summary>The end place's show desktop, and the start place's Start and Task view: what the selection pictures select.</summary>
    private static readonly (Edge, BarSection?, BarKind)[] Picked =
    [
        (Edge.Bottom, BarSection.End, BarKind.ShowDesktop),
        (Edge.Bottom, BarSection.Start, BarKind.Start),
        (Edge.Bottom, BarSection.Start, BarKind.TaskView),
    ];

    /// <summary>
    /// The layout a picture shows, of <paramref name="type"/> along <paramref name="edge"/>: a Taskbar
    /// with Windows 11's taskbar on it; a Dock with Start at its start, search, Task view and the open
    /// apps in its middle, a graph and the clock at its end.
    /// </summary>
    /// <summary>The bar's clock: the clock preset's folder, as a new bar has it.</summary>
    private static BarItem ClockFolder() => Presets.Folder(Presets.Find(Presets.ClockId)!, new PresetMachine(HasBattery: false, FixedDrives: []));

    private static Picture PictureLayout(LayoutType type, Edge edge, EdgeMode show = EdgeMode.Reserve)
    {
        List<BarItem> islands;
        if (type == LayoutType.Dock)
        {
            islands = [BarKind.Start.New(), BarKind.Search.New(), BarKind.TaskView.New(), BarKind.Apps.New(), BarKind.WidgetsBoard.New(), ClockFolder()];
            foreach (var island in islands)
            {
                island.Section = (island.Kind switch
                {
                    BarKind.Start => BarSection.Start,
                    BarKind.WidgetsBoard or BarKind.NightLight => BarSection.End,
                    _ => BarSection.Middle,
                });
            }
        }
        else
        {
            islands = Sections.DefaultLayout(pins: [], inputs: NoPenNoTouch);
        }
        Sections.Order(islands);
        return new Picture(new TaskbarLayout { Id = "pictures", Name = "Pictures", Type = type, Show = show, Islands = islands }, edge);
    }

    [UiFact]
    public void EditInPlaceSheets()
    {
        WindowShots.OnHiddenDesktop("edit-inplace", (gfx, instance) =>
        {
            const uint dpi = 144;
            var look = AddListPictures.LookFor(dark: true) with { Animations = false };
            var config = new Config();
            var taskbar = PictureLayout(LayoutType.Taskbar, Edge.Bottom);
            var dock = PictureLayout(LayoutType.Dock, Edge.Top, EdgeMode.Hover);
            EditedDisplay(gfx, instance, dpi, config, look, taskbar, "edit-inplace-taskbar-bottom-dark-150.png");
            EditedDisplay(gfx, instance, dpi, config, look, dock, "edit-inplace-dock-top-dark-150.png");
            // §625: carried in either mode, the widget at the pointer on its bar's own ground.
            foreach (var dark in new[] { true, false })
            {
                var worn = AddListPictures.LookFor(dark) with { Animations = false };
                EditedDisplay(gfx, instance, dpi, config, worn, taskbar, $"edit-inplace-taskbar-bottom-drag-{(dark ? "dark" : "light")}-150.png",
                    new Carry(Edge.Bottom, null, BarKind.FolderList, Edge.Bottom, BarSection.End));
            }
        });
    }

    [UiFact]
    public void EditSelectSheets()
    {
        WindowShots.OnHiddenDesktop("edit-select", (gfx, instance) =>
        {
            var config = new Config();
            var both = PictureLayout(LayoutType.Taskbar, Edge.Bottom, EdgeMode.Hover);
            foreach (var (dark, dpi) in new[] { (true, 144u), (false, 144u), (true, 96u) })
            {
                var look = AddListPictures.LookFor(dark) with { Animations = false };
                var name = $"{(dark ? "dark" : "light")}-{dpi * 100 / 96}";
                EditedDisplay(gfx, instance, dpi, config, look, both, $"edit-select-{name}.png", selection: new Selection(Picked, Band: true));
                EditedDisplay(gfx, instance, dpi, config, look, both, $"edit-select-inside-{name}.png", selection: new Selection(Picked[1..], Band: true, Inside: true));
                EditedDisplay(gfx, instance, dpi, config, look, both, $"edit-select-group-{name}.png", selection: new Selection(Picked, Carry: true));
            }
        });
    }

    /// <summary>
    /// Every bar stands where it stands at rest in edit mode, on every edge and for every type: a bar
    /// shown all the time along its edge, the places of one on hover in their stretches of it; a dock's
    /// three sections each a panel of its own, on one bar or three places; and the edit bar at the top
    /// of the display, centred, 8 DIPs under what stands there, its window clear of every bar and place
    /// (EditDeckPlan.Place).
    /// </summary>
    [UiTheory]
    [InlineData(LayoutType.Taskbar, Edge.Bottom, EdgeMode.Reserve, 144u)]
    [InlineData(LayoutType.Taskbar, Edge.Top, EdgeMode.Reserve, 144u)]
    [InlineData(LayoutType.Taskbar, Edge.Top, EdgeMode.Hover, 96u)]
    [InlineData(LayoutType.Dock, Edge.Top, EdgeMode.Hover, 144u)]
    [InlineData(LayoutType.Dock, Edge.Bottom, EdgeMode.Reserve, 120u)]
    [InlineData(LayoutType.Taskbar, Edge.Left, EdgeMode.Reserve, 144u)]
    [InlineData(LayoutType.Taskbar, Edge.Right, EdgeMode.Reserve, 96u)]
    [InlineData(LayoutType.Taskbar, Edge.Left, EdgeMode.Hover, 96u)]
    [InlineData(LayoutType.Dock, Edge.Left, EdgeMode.Float, 96u)]
    [InlineData(LayoutType.Dock, Edge.Right, EdgeMode.Hover, 144u)]
    public void EveryBarIsEditedWhereItStands(LayoutType type, Edge edge, EdgeMode show, uint dpi)
    {
        WindowShots.OnHiddenDesktop("edit-inplace-rules", (gfx, instance) =>
        {
            var look = AddListPictures.LookFor(dark: true) with { Animations = false };
            var config = new Config();
            var layout = PictureLayout(type, edge, show);
            var bounds = new RectI(0, 0, (int)(1920 * dpi / 144f), (int)(1080 * dpi / 144f));
            var surfaces = SurfacesOf(gfx, dpi, bounds, config, look, layout, []);
            var edges = layout.Edges();
            Assert.All(surfaces, surface => Assert.True(edges[surface.Edge].Shown, $"a surface on the {surface.Edge}, which has no bar"));
            // Every surface on the display itself, against its own edge: nothing shrunk, nothing moved.
            foreach (var surface in surfaces)
            {
                Assert.True(surface.Area.Left >= bounds.Left && surface.Area.Top >= bounds.Top && surface.Area.Right <= bounds.Right && surface.Area.Bottom <= bounds.Bottom);
                var against = surface.Edge switch
                {
                    Edge.Top => surface.Area.Top == bounds.Top,
                    Edge.Left => surface.Area.Left == bounds.Left,
                    Edge.Right => surface.Area.Right == bounds.Right,
                    _ => surface.Area.Bottom == bounds.Bottom,
                };
                Assert.True(against, $"the {surface.Edge} {surface.Only} surface stands off its edge");
                if (type == LayoutType.Dock)
                {
                    Assert.Equal(surface.Standing, surface.Only is null);
                }
            }
            var screen = new RECT { left = bounds.Left, top = bounds.Top, right = bounds.Right, bottom = bounds.Bottom };
            var laid = new List<(Edge Edge, RectI At)>();
            foreach (var surface in surfaces)
            {
                var (specs, islands) = Made(gfx, surface.Config, surface.Items);
                try
                {
                    var area = new RECT { left = surface.Area.Left, top = surface.Area.Top, right = surface.Area.Right, bottom = surface.Area.Bottom };
                    var made = new Layout(gfx, dpi, screen, area, surface.Edge, surface.Config, surface.Look, specs, PlaceContent(surface.Items, islands), editing: true, only: surface.Only);
                    // A dock's sections each a panel of its own: three on its bar, one on a place.
                    if (type == LayoutType.Dock)
                    {
                        Assert.Empty(made.EmptyRooms());
                        Assert.Equal(surface.Standing ? 3 : 1, made.SectionPanels().Count(panel => !panel.IsEmpty));
                    }
                    var strip = Strip(made, surface.Standing ? bounds : surface.Area);
                    laid.Add((surface.Edge, new RectI(strip.left, strip.top, strip.right, strip.bottom)));
                }
                finally
                {
                    foreach (var island in islands.Values)
                    {
                        island.Dispose();
                    }
                }
            }
            // The edit bar, the real window, across from the bars (§296): 8 DIPs off the deepest window
            // of its side, inside the display, and neither its panel nor its window over any bar or place.
            var deck = new EditDeck(gfx, instance, 0);
            try
            {
                var room = EditDeckPlanTests.Room(bounds, laid);
                deck.Show(gfx, look, dpi / 96f, room);
                var panel = Assert.IsType<RECT>(deck.Panel);
                var at = new RectI(panel.left, panel.top, panel.right, panel.bottom);
                Assert.Equal(deck.Spot.Panel, at);
                var window = EditDeckClickTests.WindowOf(deck.Hwnd);
                foreach (var bar in laid)
                {
                    Assert.False(Overlaps(at, bar.At), $"the edit bar runs into the {bar.Edge} bar");
                    Assert.False(Overlaps(new RectI(window.left, window.top, window.right, window.bottom), bar.At), $"the edit bar's window runs into the {bar.Edge} bar");
                }
                var margin = (int)MathF.Round(EditDeckPlan.MarginDip * dpi / 96f, MidpointRounding.AwayFromZero);
                if (edge is Edge.Top or Edge.Bottom)
                {
                    Assert.Equal(edge == Edge.Top ? Edge.Bottom : Edge.Top, room.Side);
                }
                Assert.Equal(0, EditDeckPlanTests.Off(room, at, margin));
                // Centred, or pushed along off a bar at the side by its window's room (§343).
                var clear = (int)MathF.Round(FlyoutHost.RoomDip * dpi / 96f, MidpointRounding.AwayFromZero);
                Assert.True(
                    Math.Abs(at.Left - bounds.Left - ((bounds.Width - at.Width) / 2)) <= 1
                    || (room.LeftDepth > 0 && at.Left == bounds.Left + room.LeftDepth + clear)
                    || (room.RightDepth > 0 && at.Right == bounds.Right - room.RightDepth - clear),
                    $"the edit bar {at} is neither centred nor off a side bar");
                Assert.True(at.Left >= bounds.Left + margin && at.Right <= bounds.Right - margin && at.Top >= bounds.Top + margin && at.Bottom <= bounds.Bottom - margin, $"the edit bar {at} leaves the display");
            }
            finally
            {
                deck.Dispose();
            }
        });
    }

    /// <summary>
    /// The surfaces of a display at <paramref name="bounds"/> and <paramref name="dpi"/> showing
    /// <paramref name="layout"/> in edit mode, as the dock makes its windows (Dock.Arrange): a bar for
    /// every edge shown all the time, along it (HoverPlaces.BarArea), and every place of an edge on
    /// hover the layout has, in its stretch of the edge as the places share it by what each wants
    /// (HoverPlaces.For with editing), none on an edge another display touches (<paramref name="others"/>).
    /// </summary>
    private static List<Surface> SurfacesOf(Gfx gfx, uint dpi, RectI bounds, Config config, Look look, Picture layout, RectI[] others, bool editing = true)
    {
        var edges = layout.Edges();
        var list = layout.Islands;
        Config Sized(Edge edge) => config.WithEdge(edges[edge]);
        List<BarItem> ItemsOf(Edge edge, BarSection section) => edge == layout.Screen ? Sections.Along(list).FindAll(island => Sections.Of(island) == section) : [];
        bool Filled(Edge edge, BarSection section) => ItemsOf(edge, section).Count > 0;
        var screen = new RECT { left = bounds.Left, top = bounds.Top, right = bounds.Right, bottom = bounds.Bottom };
        var surfaces = new List<Surface>();
        // A bar for every edge shown all the time, along the whole edge, the reserving first (Dock.StandingEdges).
        foreach (var edge in TheDock.StandingEdges(edges).OfType<Edge>())
        {
            // A bar over the windows is never clear (HoverPlaces.Dressed).
            var wearing = edges[edge].Mode == EdgeMode.Reserve ? look : HoverPlaces.Dressed(look);
            surfaces.Add(new Surface(bounds, edge, TheDock.Shows(layout.Layout, layout.Screen, null, edge), Sized(edge), wearing, null, Standing: true));
        }
        // The places by thirds first, then shared by what each wants (Dock.WantsOf, HoverPlaces.Share).
        var thirds = HoverPlaces.For(bounds, edges.Standing, Filled, others)
            .Where(place => edges.Has(place.Edge, place.Section))
            .ToList();
        var wants = new Dictionary<Edge, EdgeWants>();
        foreach (var group in thirds.GroupBy(place => place.Edge))
        {
            var known = PlaceWants(gfx, dpi, screen, Sized(group.Key), HoverPlaces.Dressed(look), editing, PlaceContent,
                [.. group.Select(place => (place.Edge, place.Section, ItemsOf(place.Edge, place.Section), HoverPlaces.Area(bounds, place)))])[group.Key];
            // A place the layout does not have wants nothing (Dock.WantsOf: no window, nothing in it).
            wants[group.Key] = new EdgeWants(
                edges.Has(group.Key, BarSection.Start) ? known.Start : 0,
                known.Middle,
                edges.Has(group.Key, BarSection.End) ? known.End : 0,
                known.Gap);
        }
        var places = HoverPlaces.For(bounds, edges.Standing, Filled, others, edge => wants.TryGetValue(edge, out var want) ? want : default)
            .Where(place => edges.Has(place.Edge, place.Section));
        foreach (var place in places)
        {
            // At rest only a place with something in it comes in, as the pointer brings it.
            if (!editing && !Filled(place.Edge, place.Section))
            {
                continue;
            }
            Assert.True(HoverPlaces.Wanted(place, Filled(place.Edge, place.Section), editing));
            surfaces.Add(new Surface(HoverPlaces.Area(bounds, place), place.Edge, ItemsOf(place.Edge, place.Section), Sized(place.Edge), HoverPlaces.Dressed(look), place.Section, Standing: false));
        }
        return surfaces;
    }

    /// <summary>What a bar or a place of the edit sheets shows: its widgets, the bar's clock, and three open apps where it holds them.</summary>
    private static Content PlaceContent(List<BarItem> items, IslandCollection islands) =>
        new() { Islands = islands, Tiles = items.Exists(island => island.Kind == BarKind.Apps) ? [.. Enumerable.Range(0, 3).Select(BusyApp)] : [] };

    /// <summary>
    /// A display 1280 DIPs by 720 (<paramref name="dips"/> where given) at <paramref name="dpi"/>, with
    /// another display against its right edge (its left with <paramref name="neighbourLeft"/>),
    /// showing <paramref name="layout"/> in edit mode: dimmed, its bars where they stand, those on
    /// hover brought in, its edit bar by its first bar, all over one desktop with the edit bars
    /// of the sections that cannot show everything, <paramref name="carry"/> under way where one is
    /// given, saved to <paramref name="file"/> — or, with <paramref name="keep"/>, handed back to be
    /// put beside another.
    /// </summary>
    /// <summary>
    /// What the edit bar shows in a picture (§352): "Display 1" and a new install's layout, no layout on a
    /// display that shows None (§326); the folder <paramref name="folder"/> open, by its name.
    /// </summary>
    internal static EditDeckState PictureState(bool none = false, string? folder = null) =>
        new(
            new EditDeckSubject(OptionGlyphs.Monitor, S.CoreMonitorDisplay("1")),
            none ? null : new EditDeckSubject(TaskbarLayouts.LayoutGlyph, Config.FirstLayout().Name),
            folder is null ? null : new EditDeckSubject(OptionGlyphs.FolderOpen, folder));

    private static (byte[] Canvas, int Width, int Height)? EditedDisplay(Gfx gfx, HINSTANCE instance, uint dpi, Config config, Look look, Picture layout, string file, Carry? carry = null, bool neighbourLeft = false, bool keep = false, Selection? selection = null, Vector2? dips = null, bool editing = true, bool popup = false, bool folder = false, bool besideMenu = false, bool lineMenu = false, bool none = false, string? folderFace = null, Action<EditDeck>? panel = null, Func<EditDeck, Flyout>? panelPopup = null, Action<EditDeck, RECT>? fromPlus = null, Func<Layout, EditView?>? marks = null, Action<FolderFlyout, EditDeck?>? inFolder = null, bool folderGhost = false, bool preview = false)
    {
        var scale = dpi / 96f;
        var size = (dips ?? new Vector2(1280f, 720f)) * scale;
        var bounds = new RectI(0, 0, (int)size.X, (int)size.Y);
        var screen = new RECT { left = 0, top = 0, right = bounds.Right, bottom = bounds.Bottom };
        RectI[] others = [neighbourLeft ? new RectI(-bounds.Width, 0, 0, bounds.Bottom) : new RectI(bounds.Right, 0, bounds.Right + bounds.Width, bounds.Bottom)];
        // A display that shows None (§326): no bar, the edit bar alone over the dim.
        var shown = none ? [] : SurfacesOf(gfx, dpi, bounds, config, look, layout, others, editing);
        var wall = MakeWindow();
        var made = new List<(HWND Window, DesktopWindowTarget Target, Scene Scene, ContainerVisual Desktop, IslandCollection Islands, Layout Layout, Surface Surface)>();
        var (wallTarget, wallDesktop) = (default(DesktopWindowTarget), default(ContainerVisual));
        EditDeck? add = null;
        Flyout? menu = null;
        FolderFlyout? opened = null;
        // edit: the bar whose face opened the folder, and the part (§251), drawn as itself (§267).
        (Scene Scene, Layout Layout, int Part)? face = null;
        (HWND Window, DesktopWindowTarget Target, RECT Rect)? carried = null;
        try
        {
            _ = SetWindowPos(wall, HWND.NULL, 0, 0, bounds.Right, bounds.Bottom, SWP.SWP_NOZORDER | SWP.SWP_NOACTIVATE);
            wallTarget = Target(gfx, wall);
            // The display dimmed, as the dim window over it shows it (EditShade).
            wallDesktop = editing ? Dimmed(gfx, look, size, Vector2.Zero) : Wallpaper(gfx, look, size, Vector2.Zero);
            wallTarget.Root = wallDesktop;
            _ = ShowWindow(wall, SW.SW_SHOWNOACTIVATE);
            var parts = new List<(HWND Window, RECT Part)> { (wall, screen) };
            foreach (var surface in shown)
            {
                var (specs, islands) = Made(gfx, surface.Config, surface.Items);
                var content = PlaceContent(surface.Items, islands);
                var area = new RECT { left = surface.Area.Left, top = surface.Area.Top, right = surface.Area.Right, bottom = surface.Area.Bottom };
                var laid = new Layout(gfx, dpi, screen, area, surface.Edge, surface.Config, surface.Look, specs, content, editing, only: surface.Only);
                // No "+" on any bar: an empty place is its empty room alone; a dock has none at its ends.
                if (editing && surface.Items.Count == 0 && surface.Only is not null)
                {
                    Assert.Single(laid.EmptyRooms());
                }
                var window = MakeWindow();
                _ = SetWindowPos(window, HWND.NULL, laid.X, laid.Y, laid.Width, laid.Height, SWP.SWP_NOZORDER | SWP.SWP_NOACTIVATE);
                var target = Target(gfx, window);
                var scene = Scenes.Build(gfx, target, laid, content, surface.Look, new Icons(), shown: true, edit: editing ? new EditView() : null);
                // Behind the bar what stands there, the dim over the desktop, for its material to see through.
                var desktop = editing ? Dimmed(gfx, look, size, new Vector2(-laid.X, -laid.Y)) : Wallpaper(gfx, look, size, new Vector2(-laid.X, -laid.Y));
                var behind = gfx.Compositor.CreateContainerVisual();
                behind.Children.InsertAtTop(desktop);
                var root = target.Root;
                target.Root = behind;
                behind.Children.InsertAtTop(root);
                made.Add((window, target, scene, behind, islands, laid, surface));
                _ = ShowWindow(window, SW.SW_SHOWNOACTIVATE);
                // Of each window its bar and the room off its edge, as the dock's region takes. Several
                // carried together into a place grow its panel past where it stands at rest: its whole
                // room is shown, as the dock's region takes it while it is edited.
                parts.Add((window, selection is { Carry: true } && !surface.Standing ? area : Strip(laid, surface.Standing ? bounds : surface.Area)));
            }
            // §593: what a picture marks on a bar (the add page's place, its ghost), as the dock shows it.
            foreach (var entry in made)
            {
                if (marks?.Invoke(entry.Layout) is { } view)
                {
                    entry.Scene.Edit(view);
                }
            }
            // The edit bar centred across from the bars (Dock.ShowDecks, EditDeckPlan.Place, §296): 8 DIPs
            // off what stands along its side.
            var taken = made.ConvertAll(entry => Strip(entry.Layout, entry.Surface.Standing ? bounds : entry.Surface.Area));
            var bars = taken.Select((strip, at) => (made[at].Layout.Edge, new RectI(strip.left, strip.top, strip.right, strip.bottom))).ToList();
            RECT? deck = null;
            // §612: the Appearance's preview (<paramref name="preview"/>): edit mode on, its edit bar and flyout there, the bars and the
            // desktop drawn as out of it (<paramref name="editing"/> false).
            if (editing || preview)
            {
                add = new EditDeck(gfx, instance, 0);
                // Its crumbs the display and the layout shown, or the display alone with "Add a widget" greyed (§326), as Dock.ShowDecks says.
                add.Show(gfx, look, scale, EditDeckPlanTests.Room(bounds, bars), PictureState(none), SettingsPanelTests.Entries());
                Assert.True(add.IsOpen);
                var top = Assert.IsType<RECT>(add.Panel);
                Assert.Equal(add.Spot.Panel, new RectI(top.left, top.top, top.right, top.bottom));
                // §575: one row 48 DIP tall, as wide as its entries and buttons, no flyout open.
                Assert.Equal((int)MathF.Round(48 * scale), top.bottom - top.top);
                Assert.Equal([.. Enumerable.Repeat(PanelHitKind.Nav, 5), PanelHitKind.Add, PanelHitKind.Done], add.Layout!.Header.Select(part => part.Kind));
                Assert.False(add.FlyoutOpen);
                // §576: the add page hung from the first bar's first "+", that "+" pressed.
                if (fromPlus is not null && made.FirstOrDefault(entry => entry.Layout.Adds().Count > 0) is { Layout: { } plusOn } bar)
                {
                    var first = plusOn.Adds()[0];
                    bar.Scene.Edit(new EditView(AddPressed: 0));
                    fromPlus(add, new RECT { left = plusOn.X + (int)first.Rect.X, top = plusOn.Y + (int)first.Rect.Y, right = plusOn.X + (int)MathF.Ceiling(first.Rect.Right), bottom = plusOn.Y + (int)MathF.Ceiling(first.Rect.Bottom) });
                    top = add.PanelOnScreen;
                }
                // §575: a flyout open from it, as the picture asks.
                else if (panel is not null)
                {
                    panel(add);
                    top = add.PanelOnScreen;
                    // §554: a popup of the panel open over it (a dropdown's list), as the dock hangs it from its control.
                    menu = panelPopup?.Invoke(add);
                }
                taken.Add(top);
                deck = top;
            }
            // The bars, the places and the edit bar, each clear of the others.
            for (var i = 0; i < taken.Count; i++)
            {
                for (var j = i + 1; j < taken.Count; j++)
                {
                    Assert.False(Overlaps(taken[i], taken[j]), $"{file}: {i} and {j} overlap");
                }
            }
            foreach (var (_, _, _, _, _, laid, surface) in made.OrderBy(entry => entry.Surface.Standing ? 0 : 1))
            {
                if (editing)
                {
                    RailsBeside(gfx, wallDesktop, laid, surface.Only, scale, bounds, taken);
                }
            }
            if (popup)
            {
                menu = PoppedFromTheTop(gfx, instance, look, scale, made.ConvertAll(entry => (entry.Layout, entry.Surface.Standing)), bounds, deck, taken, file);
            }
            if (folder)
            {
                opened = FolderFrom(gfx, instance, look, scale, made.ConvertAll(entry => (entry.Layout, entry.Islands, entry.Surface.Standing)), bounds, deck, taken, file, editing, folderFace);
                // edit: a folder open arranged has edit mode to itself, every bar locked under it (§212)
                // and shown as it is, not dimmed (§267): the folder's face selected, as the click that
                // opened it left it (§253), and drawn as itself — no accent, no × on it.
                if (editing)
                {
                    var key = opened.Folder!.Key;
                    foreach (var entry in made)
                    {
                        var part = entry.Layout.PartIndex(key);
                        // §606: the add page's ghost of a group: its face the ghost, nothing selected.
                        entry.Scene.Edit(folderGhost ? new EditView(Ghost: part >= 0 ? part : null) : new EditView(Selected: part >= 0 ? part : null));
                        if (part < 0)
                        {
                            continue;
                        }
                        Assert.Null(face);
                        face = (entry.Scene, entry.Layout, part);
                    }
                    Assert.NotNull(face);
                    // Its guide lines down the display to the bars' edges, none on a bar's strip — its
                    // panels, the room between them, the room off the screen's edge (§287) — and no
                    // close button of its own (§287).
                    var lines = opened.GuidesOnScreen;
                    Assert.NotEmpty(lines);
                    foreach (var strip in made.Select(entry => Strip(entry.Layout, entry.Surface.Standing ? bounds : entry.Surface.Area)))
                    {
                        foreach (var line in lines)
                        {
                            Assert.False(Overlaps(line, strip), $"{file}: a guide line {line.left},{line.top}..{line.right},{line.bottom} runs onto the bar at {strip.left},{strip.top}..{strip.right},{strip.bottom}");
                        }
                    }
                    var edge = face!.Value.Layout.Edge;
                    var own = Strip(face.Value.Layout, bounds);
                    Assert.Contains(lines, line => edge switch
                    {
                        Edge.Top => line.top == own.bottom,
                        Edge.Left => line.left >= own.right,
                        Edge.Right => line.right <= own.left,
                        _ => line.bottom == own.top,
                    });
                }
            }
            // edit: a click's menu beside the widget clicked, on the bar or on a line of the folder (§212).
            if (besideMenu)
            {
                menu = MenuBesideABlock(gfx, instance, look, scale, made.ConvertAll(entry => (entry.Layout, entry.Surface.Standing)), bounds, file);
            }
            if (lineMenu && opened is not null)
            {
                menu = MenuBesideALine(gfx, instance, look, scale, opened, made.ConvertAll(entry => (entry.Layout, entry.Surface.Standing)), bounds, file);
            }
            // edit: a folder open arranged, the edit bar shows the folder's crumb where it stands
            // (Dock.ShowDecks, §297, §352), keeping the bar's place, over the folder (§329).
            var folderButtons = folder && editing && add is not null && !folderGhost;
            // Where the arranged folder goes in the picture: over the dim, under the bars (§279), the
            // edit bars and the menus.
            var folderAt = 1;
            if (folderButtons)
            {
                var barAdd = add!.OnScreen(new PanelHit(PanelHitKind.Add));
                add.Show(gfx, look, scale, EditDeckPlanTests.Room(bounds, bars), PictureState(none, S.CoreWidgetFolderListTitle), SettingsPanelTests.Entries());
                Assert.True(add.IsOpen && add.Folder);
                Assert.Equal(deck, add.Panel);
                // Every button where it stood (§310, §352, §520): "Add" where it stood, saying the same (§366).
                Assert.Equal(deck, add.PanelOnScreen);
                Assert.Equal(barAdd, add.OnScreen(new PanelHit(PanelHitKind.Add)));
            }
            // §593: what the picture marks in the open folder (the add page's ghost among its lines), the edit bar there.
            if (opened is not null)
            {
                // §606: the whole folder the add page's ghost, as the dock shows it while its group is under the pointer.
                opened.Ghosted = folderGhost;
                inFolder?.Invoke(opened, add);
            }
            var surfaces = made.ConvertAll(entry => (entry.Layout, entry.Surface.Standing ? (BarSection?)null : entry.Surface.Only, entry.Scene));
            if (carry is not null)
            {
                carried = Carried(gfx, surfaces, carry, scale);
            }
            if (selection is not null)
            {
                // The selection rectangle is a window over everything (EditBand), drawn here into the
                // dim and into every window it runs over, as it would show over them.
                List<(ContainerVisual, Vector2)> drawnOver = [(wallDesktop, Vector2.Zero), .. made.Select(entry => (entry.Desktop, new Vector2(entry.Layout.X, entry.Layout.Y)))];
                carried = SelectedOn(gfx, surfaces, selection, look, scale, drawnOver);
            }
            List<HWND> flyoutsLaid = [];
            if (add is not null && deck is { } shownDeck)
            {
                // edit: the folder's in front of the folder's window (Dock.ShowDecks), by its own panel.
                parts.Add((add.Hwnd, folderButtons ? add.PanelOnScreen : shownDeck));
                // §575: its flyout, a window of its own over it; §584 with its beak, laid over what is under it by its alpha (its
                // rounded corners and the beak's sides on the dim, not on black).
                if (add.FlyoutOpen)
                {
                    var flyout = add.FlyoutOnScreen;
                    if (add.FlyoutBeak is { } beak)
                    {
                        var tall = (int)MathF.Ceiling(beak.Tall);
                        (flyout.top, flyout.bottom) = beak.Side == Edge.Top ? (flyout.top - tall, flyout.bottom) : (flyout.top, flyout.bottom + tall);
                    }
                    parts.Add((add.FlyoutHwnd, flyout));
                    flyoutsLaid.Add(add.FlyoutHwnd);
                }
            }
            if (menu is not null)
            {
                // A click's menu by its panel, whose room round it has nothing behind it to show here.
                parts.Add((menu.Hwnd, besideMenu || lineMenu || panelPopup is not null ? menu.PanelsOnScreen.Panel : EditDeckClickTests.WindowOf(menu.Hwnd)));
            }
            List<HWND> laidOver = [.. flyoutsLaid];
            if (opened is not null && editing)
            {
                // Arranged, its window is the display's (FlyoutHost.Spread), laid over the dim by its
                // alpha and under the bars, its guide lines ending at their edges (§279, §287); its "+"
                // and width buttons over each column.
                parts.Insert(folderAt, (opened.Hwnd, screen));
                laidOver.Add(opened.Hwnd);
            }
            else if (opened is not null)
            {
                parts.Add((opened.Hwnd, EditDeckClickTests.WindowOf(opened.Hwnd)));
            }
            if (carried is { } over)
            {
                parts.Add((over.Window, over.Rect));
            }
            WindowShots.Settle(300);
            (byte[] Canvas, int Width, int Height)? kept = null;
            if (keep)
            {
                var (canvas, every) = WindowShots.Together(parts, screen, file, (0, 0, 0), laidOver);
                Assert.True(every, $"{file} came back blank");
                kept = (canvas, screen.right - screen.left, screen.bottom - screen.top);
            }
            else
            {
                var frame = new RECT { left = parts.Min(part => part.Part.left), top = parts.Min(part => part.Part.top), right = parts.Max(part => part.Part.right), bottom = parts.Max(part => part.Part.bottom) };
                var (canvas, every) = WindowShots.Together(parts, frame, file, (0, 0, 0), laidOver);
                WindowShots.WritePng(canvas, frame.right - frame.left, frame.bottom - frame.top, Path.Combine(Offscreen.Pictures, file));
                Assert.True(every, $"{file} came back blank");
                (byte B, byte G, byte R) At(int x, int y)
                {
                    var spot = (((y - frame.top) * (frame.right - frame.left)) + x - frame.left) * 4;
                    return (canvas[spot], canvas[spot + 1], canvas[spot + 2]);
                }
                if (face is var (_, onBar, part))
                {
                    // The face drawn as itself (§267): inside its edit block, clear of its ring, no more
                    // than a few of its pixels the accent — the close button it was filled it whole.
                    var block = onBar.BlockRect(part);
                    var accent = FlyoutTheme.For(look).Accent;
                    var inset = 6 * scale;
                    var (all, accented) = (0, 0);
                    for (var y = (int)(block.Y + inset); y < (int)(block.Bottom - inset); y += 2)
                    {
                        for (var x = (int)(block.X + inset); x < (int)(block.Right - inset); x += 2)
                        {
                            var (b, g, r) = At(onBar.X + x, onBar.Y + y);
                            all++;
                            accented += Math.Abs(b - accent.B) + Math.Abs(g - accent.G) + Math.Abs(r - accent.R) <= 24 ? 1 : 0;
                        }
                    }
                    Assert.True(all > 0 && accented * 4 < all, $"{file}: {accented} of {all} of the face's pixels are the accent");
                }
            }
            // The folder closed, its crumb goes, every other button where it stood (§352).
            if (folderButtons)
            {
                add!.Show(gfx, look, scale, EditDeckPlanTests.Room(bounds, bars), PictureState(none), SettingsPanelTests.Entries());
                Assert.True(add.IsOpen);
                Assert.False(add.Folder);
                Assert.Equal(deck, add.Panel);
                Assert.Equal(deck, add.PanelOnScreen);
            }
            return kept;
        }
        finally
        {
            add?.Dispose();
            menu?.Dispose();
            opened?.Dispose();
            if (carried is { } gone)
            {
                gone.Target.Root = null;
                gone.Target.Dispose();
                _ = DestroyWindow(gone.Window);
            }
            foreach (var (window, target, scene, desktop, islands, _, _) in made)
            {
                scene.Dispose();
                target.Root = null;
                desktop.Dispose();
                foreach (var island in islands.Values)
                {
                    island.Dispose();
                }
                _ = DestroyWindow(window);
            }
            if (wallTarget is not null)
            {
                wallTarget.Root = null;
            }
            wallDesktop?.Dispose();
            _ = DestroyWindow(wall);
        }
    }

    /// <summary>
    /// A made-up desktop <paramref name="size"/> px, at <paramref name="offset"/>, under edit mode's dim:
    /// black, as the dim window lays it over the display (EditShade.Ink).
    /// </summary>
    private static ContainerVisual Dimmed(Gfx gfx, Look look, Vector2 size, Vector2 offset)
    {
        var desktop = Wallpaper(gfx, look, size, offset);
        var dim = gfx.Compositor.CreateSpriteVisual();
        dim.Size = size;
        dim.Offset = new Vector3(offset, 0f);
        dim.Brush = gfx.Compositor.CreateColorBrush(EditShade.Ink);
        desktop.Children.InsertAtTop(dim);
        return desktop;
    }

    private static bool Overlaps(RectI a, RectI b) =>
        Math.Min(a.Right, b.Right) > Math.Max(a.Left, b.Left) && Math.Min(a.Bottom, b.Bottom) > Math.Max(a.Top, b.Top);

    private static bool Overlaps(RECT a, RECT b) =>
        Math.Min(a.right, b.right) > Math.Max(a.left, b.left) && Math.Min(a.bottom, b.bottom) > Math.Max(a.top, b.top);

    /// <summary>
    /// <paramref name="carry"/> under way: the surface it left closed up behind it (EditView.Away), the
    /// surface it goes to opening the room for it with its ghost there (EditView.Offered, Layout.Landing
    /// with Layout.Carry) and the widget itself at the pointer, a little short of the ghost towards
    /// the middle of the screen, drawn from the lifted block in a window of its own over everything,
    /// as the carry window draws it. That window and where it stands, which the caller destroys. A
    /// surface is a place by its section, or with null the bar shown all the time along its edge.
    /// </summary>
    private static (HWND Window, DesktopWindowTarget Target, RECT Rect) Carried(Gfx gfx, List<(Layout Layout, BarSection? Only, Scene Scene)> surfaces, Carry carry, float scale)
    {
        var from = surfaces.Single(entry => entry.Layout.Edge == carry.From && entry.Only == carry.FromPlace);
        var part = from.Layout.Specs.ToList().FindIndex(spec => spec.Kind == carry.Kind);
        Assert.True(part >= 0, $"no {carry.Kind} to carry");
        from.Scene.Edit(new EditView(Dragging: part, DropIndex: from.Layout.DropSlot(0f, part), Away: true));
        var (source, block) = Assert.IsType<(Visual, RectF)>(from.Scene.Carried());
        // Into a place, or into a section of a bar.
        var to = surfaces.Find(entry => entry.Layout.Edge == carry.To && entry.Only == carry.ToPlace);
        if (to.Layout is null)
        {
            to = surfaces.Single(entry => entry.Layout.Edge == carry.To && entry.Only is null);
        }
        var carriedBlock = Assert.IsType<Layout.Carried>(from.Layout.Carry(from.Layout.Specs[part].Key));
        var room = to.Layout.SectionRect(carry.ToPlace);
        var along = to.Layout.Vertical ? room.Y + room.Height / 2f : room.X + room.Width / 2f;
        Landed landed;
        if (ReferenceEquals(to.Layout, from.Layout))
        {
            // Along its own bar: the gap opens where it goes (Layout.Arrange), its ghost there.
            var slot = to.Layout.DropSlot(along, part);
            from.Scene.Edit(new EditView(Dragging: part, DropIndex: slot));
            landed = to.Layout.Landing(slot, carriedBlock);
        }
        else
        {
            var slot = to.Layout.DropSlot(along, -1);
            landed = to.Layout.Landing(slot, carriedBlock);
            to.Scene.Edit(new EditView(Offered: landed));
            Assert.Equal(landed.Rect, to.Scene.GhostShown);
        }
        Assert.False(landed.Rect.IsEmpty, "the surface shows where the widget would land");
        // A little way off the ghost, towards the middle of the screen, as a pointer still on its way.
        var ghost = new Vector2(to.Layout.X + landed.Rect.X, to.Layout.Y + landed.Rect.Y);
        var away = MathF.Round(56f * scale);
        var inward = to.Layout.Edge switch
        {
            Edge.Top => new Vector2(away * 0.6f, away),
            Edge.Left => new Vector2(away, away * 0.6f),
            Edge.Right => new Vector2(-away, away * 0.6f),
            _ => new Vector2(away * 0.6f, -away),
        };
        var at = ghost + inward + (to.Layout.Edge == Edge.Bottom ? new Vector2(0f, -block.Height) : Vector2.Zero);
        // The widget at the pointer: drawn from where its own bar lifted it, as the carry window does.
        var c = gfx.Compositor;
        var surface = c.CreateVisualSurface();
        surface.SourceVisual = source;
        surface.SourceOffset = new Vector2(block.X, block.Y);
        surface.SourceSize = block.Size;
        var brush = c.CreateSurfaceBrush(surface);
        brush.Stretch = CompositionStretch.None;
        var picture = c.CreateSpriteVisual();
        picture.Brush = brush;
        picture.Size = block.Size;
        var left = (int)MathF.Round(at.X);
        var top = (int)MathF.Round(at.Y);
        var rect = new RECT { left = left, top = top, right = left + (int)MathF.Ceiling(block.Width), bottom = top + (int)MathF.Ceiling(block.Height) };
        var window = MakeWindow();
        _ = SetWindowPos(window, HWND.NULL, rect.left, rect.top, rect.right - rect.left, rect.bottom - rect.top, SWP.SWP_NOZORDER | SWP.SWP_NOACTIVATE);
        var target = Target(gfx, window);
        target.Root = picture;
        _ = ShowWindow(window, SW.SW_SHOWNOACTIVATE);
        return (window, target, rect);
    }

    /// <summary>
    /// <paramref name="selection"/> on the display's surfaces: every one of its widgets ringed as
    /// selected (EditView.Selected, AlsoSelected), the first the one last clicked, and with its
    /// <c>Band</c> the selection rectangle round the first one's place, over everything, as the dim
    /// draws it while it is dragged (EditBand). With its <c>Carry</c>, all of them carried together
    /// into the empty middle place (Dock.GroupOffer): each surface they came from closed up behind them
    /// (Layout.Without), the place opening the room for all three with one landing round them
    /// (Layout.Landing with a group), and each at the pointer, side by side, in a window over
    /// everything, as the carry window draws them (EditCarryPlan). That window and where it stands,
    /// which the caller destroys.
    /// </summary>
    private static (HWND Window, DesktopWindowTarget Target, RECT Rect)? SelectedOn(Gfx gfx, List<(Layout Layout, BarSection? Only, Scene Scene)> surfaces, Selection selection, Look look, float scale, List<(ContainerVisual Container, Vector2 Origin)> over)
    {
        var mine = surfaces.ConvertAll(_ => new List<int>());
        var keys = new List<string>();
        foreach (var (edge, place, kind) in selection.Widgets)
        {
            var at = surfaces.FindIndex(entry => entry.Layout.Edge == edge && entry.Only == place);
            Assert.True(at >= 0, $"no surface along {edge} {place}");
            var part = surfaces[at].Layout.Specs.ToList().FindIndex(spec => spec.Kind == kind);
            Assert.True(part >= 0, $"no {kind} along {edge} {place}");
            mine[at].Add(part);
            keys.Add(surfaces[at].Layout.Specs[part].Key);
        }
        var c = gfx.Compositor;
        var accent = FlyoutTheme.For(look).Accent;
        if (!selection.Carry)
        {
            for (var at = 0; at < surfaces.Count; at++)
            {
                var anchor = at == surfaces.FindIndex(entry => entry.Layout.Specs.Any(spec => spec.Key == keys[0])) ? mine[at].FirstOrDefault(part => surfaces[at].Layout.Specs[part].Key == keys[0], -1) : -1;
                surfaces[at].Scene.Edit(new EditView(Selected: anchor >= 0 ? anchor : null, AlsoSelected: [.. mine[at].Where(part => part != anchor)]));
            }
            // The rectangle a drag over the dim draws, round the first one's place and a little past it.
            var first = surfaces.Find(entry => entry.Layout.Specs.Any(spec => spec.Key == keys[0]));
            var panel = first.Layout.PanelRect;
            var grow = MathF.Round(14f * scale);
            var band = new RECT
            {
                left = first.Layout.X + (int)(panel.X - (2 * grow)),
                top = first.Layout.Y + (int)(panel.Y - grow),
                right = first.Layout.X + (int)(panel.Right + (3 * grow)),
                bottom = first.Layout.Y + (int)(panel.Bottom + (2 * grow)),
            };
            if (selection.Inside)
            {
                // Begun on the bar's own room, in the gap before the first, and drawn to a third into
                // the last, inside the bar (EditClicks.OnPress): what it runs over is what is ringed.
                var blocks = keys.Select(key => first.Layout.PartIndex(key)).Where(part => part >= 0).Select(first.Layout.BlockRect).OrderBy(block => block.X).ToList();
                Assert.Equal(keys.Count, blocks.Count);
                var (from, to) = (blocks[0], blocks[^1]);
                var inset = MathF.Round(4f * scale);
                // The middle of the gap before the first: half way from the block before it, if any.
                var before = Enumerable.Range(0, first.Layout.Specs.Count)
                    .Where(part => first.Layout.Placed(part) && first.Layout.BlockRect(part).Right <= from.X)
                    .Select(part => first.Layout.BlockRect(part).Right)
                    .DefaultIfEmpty(from.X - (12f * scale))
                    .Max();
                band = new RECT
                {
                    left = first.Layout.X + (int)MathF.Ceiling((before + from.X) / 2f),
                    top = first.Layout.Y + (int)(panel.Y + inset),
                    right = first.Layout.X + (int)(to.Right - (to.Width / 3f)),
                    bottom = first.Layout.Y + (int)(panel.Bottom - inset),
                };
                Assert.True(band.right > band.left && band.bottom > band.top, $"the rectangle {band.left},{band.top},{band.right},{band.bottom} is empty; panel {panel}, from {from}, to {to}");
                // Ringed is what it runs over, as the dock selects it (DockWindow.KeysIn): the two, and
                // what stands between them.
                var ran = new List<string>();
                foreach (var (layout, _, scene) in surfaces)
                {
                    var runOver = Enumerable.Range(0, layout.Specs.Count)
                        .Where(part => layout.Placed(part) && layout.BlockRect(part) is var block
                            && EditClicks.Overlaps(band, layout.X + block.X, layout.Y + block.Y, layout.X + block.Right, layout.Y + block.Bottom))
                        .ToList();
                    ran.AddRange(runOver.Select(part => layout.Specs[part].Key));
                    scene.Edit(new EditView(Selected: runOver.Count > 0 ? runOver[0] : null, AlsoSelected: [.. runOver.Skip(1)]));
                }
                Assert.All(keys, key => Assert.Contains(key, ran));
                Assert.True(ran.Count <= keys.Count + 1, "the rectangle inside the bar runs over more than the two and what is between them");
            }
            var size = new Vector2(band.right - band.left, band.bottom - band.top);
            foreach (var (container, origin) in over)
            {
                var root = c.CreateContainerVisual();
                root.Offset = new Vector3(band.left - origin.X, band.top - origin.Y, 0f);
                var fill = c.CreateSpriteVisual();
                fill.Size = size;
                fill.Brush = c.CreateColorBrush(Color.FromArgb(0x40, accent.R, accent.G, accent.B));
                root.Children.InsertAtTop(fill);
                var hair = MathF.Max(1f, MathF.Floor(scale));
                var line = c.CreateRoundedRectangleGeometry();
                line.Offset = new Vector2(hair / 2f, hair / 2f);
                line.Size = size - new Vector2(hair, hair);
                var stroke = c.CreateSpriteShape(line);
                stroke.StrokeBrush = c.CreateColorBrush(accent);
                stroke.StrokeThickness = hair;
                var edge = c.CreateShapeVisual();
                edge.Size = size;
                edge.Shapes.Add(stroke);
                root.Children.InsertAtTop(edge);
                container.Children.InsertAtTop(root);
            }
            return null;
        }
        // Carried together into the empty middle place: each surface as Dock.GroupOffer shows it.
        var carriedAll = new List<Layout.Carried>();
        for (var index = 0; index < keys.Count; index++)
        {
            var from = surfaces.Find(entry => entry.Layout.PartIndex(keys[index]) >= 0);
            carriedAll.Add(Assert.IsType<Layout.Carried>(from.Layout.Carry(keys[index])));
        }
        var target = surfaces.FindIndex(entry => entry.Only == BarSection.Middle);
        Assert.True(target >= 0, "no middle place to carry them into");
        RectF ghost = default;
        for (var at = 0; at < surfaces.Count; at++)
        {
            var (layout, _, scene) = surfaces[at];
            var without = mine[at].Count > 0 ? layout.Without(mine[at]) : layout;
            if (at == target)
            {
                // Into its empty room.
                var slot = without.DropSlot(0f, -1);
                var landed = without.Landing(slot, carriedAll);
                Assert.False(landed.Rect.IsEmpty, "the place shows where they would land");
                scene.Edit(new EditView(Group: mine[at], GroupLanding: landed with { Lefts = layout.Remaining(mine[at], landed.Lefts) }));
                Assert.Equal(landed.Rect, scene.GhostShown);
                ghost = new RectF(layout.X + landed.Rect.X, layout.Y + landed.Rect.Y, landed.Rect.Width, landed.Rect.Height);
            }
            else if (mine[at].Count > 0)
            {
                var closed = without.Landing(default, []);
                scene.Edit(new EditView(Group: mine[at], GroupLanding: closed with { Lefts = layout.Remaining(mine[at], closed.Lefts) }));
                Assert.Null(scene.GhostShown);
            }
        }
        // Each at the pointer, side by side, as the carry window holds them.
        var lifted = surfaces.SelectMany(entry => entry.Scene.CarriedGroup()).ToList();
        var pieces = keys.Select(key => lifted.Single(piece => piece.Key == key)).ToList();
        // One landing round all three: longer along its side than any one of them alone, as long as their cells there.
        var vertical = surfaces[target].Layout.Vertical;
        var along = vertical ? ghost.Height : ghost.Width;
        Assert.True(along >= pieces.Sum(piece => vertical ? piece.Block.Height : piece.Block.Width) - 1f, $"the landing {along} long holds all three");
        var (places, whole) = EditCarryPlan.Row([.. pieces.Select(piece => piece.Block.Size)], vertical: false, MathF.Round(4f * scale));
        var carryRoot = c.CreateContainerVisual();
        for (var index = 0; index < pieces.Count; index++)
        {
            var surface = c.CreateVisualSurface();
            surface.SourceVisual = pieces[index].Source;
            surface.SourceOffset = new Vector2(pieces[index].Block.X, pieces[index].Block.Y);
            surface.SourceSize = pieces[index].Block.Size;
            var brush = c.CreateSurfaceBrush(surface);
            brush.Stretch = CompositionStretch.None;
            var sprite = c.CreateSpriteVisual();
            sprite.Brush = brush;
            sprite.Size = pieces[index].Block.Size;
            sprite.Offset = new Vector3(places[index], 0f);
            carryRoot.Children.InsertAtTop(sprite);
        }
        // A little way under the landing, towards the middle of the screen, as a pointer still on its way.
        var away = MathF.Round(56f * scale);
        var left = (int)MathF.Round(ghost.X + (away * 0.6f));
        var topAt = (int)MathF.Round(ghost.Bottom + (away * 0.5f));
        var rect = new RECT { left = left, top = topAt, right = left + (int)MathF.Ceiling(whole.X), bottom = topAt + (int)MathF.Ceiling(whole.Y) };
        var window = MakeWindow();
        _ = SetWindowPos(window, HWND.NULL, rect.left, rect.top, rect.right - rect.left, rect.bottom - rect.top, SWP.SWP_NOZORDER | SWP.SWP_NOACTIVATE);
        var carryTarget = Target(gfx, window);
        carryTarget.Root = carryRoot;
        _ = ShowWindow(window, SW.SW_SHOWNOACTIVATE);
        return (window, carryTarget, rect);
    }

    /// <summary>
    /// Every section's edit bar beside it, towards the middle of the screen (EditRail): the plan's
    /// panel, buttons and glyphs, centred along the section, 16 DIPs off its inner side (the 8 the
    /// blocks' badges keep and the 8 of the gap), and inside the display 8 DIPs from its sides.
    /// </summary>
    private static void RailsBeside(Gfx gfx, ContainerVisual desktop, Layout layout, BarSection? only, float scale, RectI bounds, List<RECT> taken)
    {
        var c = gfx.Compositor;
        float Px(float dip) => MathF.Round(dip * scale, MidpointRounding.AwayFromZero);
        foreach (var section in (BarSection[])[BarSection.Start, BarSection.Middle, BarSection.End])
        {
            if (only is { } one && one != section)
            {
                continue;
            }
            var rect = layout.SectionRect(section);
            if (rect.IsEmpty)
            {
                continue;
            }
            var vertical = layout.Vertical;
            var plan = EditRailPlan.For(scale, vertical, layout.NotShown.Any(spec => spec.Section == section));
            if (plan.Buttons.Count == 0)
            {
                // A section showing everything has no edit bar.
                continue;
            }
            var on = new Vector2(layout.X + rect.X, layout.Y + rect.Y);
            var off = Px(16f);
            var at = layout.Edge switch
            {
                Edge.Top => new Vector2(on.X + (rect.Width - plan.Panel.X) / 2f, on.Y + rect.Height + off),
                Edge.Left => new Vector2(on.X + rect.Width + off, on.Y + (rect.Height - plan.Panel.Y) / 2f),
                Edge.Right => new Vector2(on.X - off - plan.Panel.X, on.Y + (rect.Height - plan.Panel.Y) / 2f),
                _ => new Vector2(on.X + (rect.Width - plan.Panel.X) / 2f, on.Y - off - plan.Panel.Y),
            };
            var margin = Px(8f);
            at = new Vector2(
                MathF.Round(Math.Clamp(at.X, bounds.Left + margin, bounds.Right - margin - plan.Panel.X)),
                MathF.Round(Math.Clamp(at.Y, bounds.Top + margin, bounds.Bottom - margin - plan.Panel.Y)));
            var placed = new RECT { left = (int)at.X, top = (int)at.Y, right = (int)(at.X + plan.Panel.X), bottom = (int)(at.Y + plan.Panel.Y) };
            var centre = vertical ? (placed.top + placed.bottom) / 2 : (placed.left + placed.right) / 2;
            var middle = vertical ? bounds.Height / 2 : bounds.Width / 2;
            var shift = EditRails.Clear(placed, 0, taken, vertical, middle >= centre ? 1 : -1, (int)Px(EditRailPlan.MarginDip));
            at += vertical ? new Vector2(0f, shift) : new Vector2(shift, 0f);
            taken.Add(vertical ? placed with { top = placed.top + shift, bottom = placed.bottom + shift } : placed with { left = placed.left + shift, right = placed.right + shift });
            var panel = c.CreateSpriteVisual();
            panel.Size = plan.Panel;
            panel.Offset = new Vector3(at, 0f);
            panel.Brush = c.CreateColorBrush(Color.FromArgb(255, 0x2C, 0x2C, 0x2C));
            desktop.Children.InsertAtTop(panel);
            var box = Px(EditRailPlan.GlyphDip);
            foreach (var button in plan.Buttons)
            {
                var plate = c.CreateSpriteVisual();
                plate.Size = new Vector2(button.Rect.Width, button.Rect.Height);
                plate.Offset = new Vector3(at + new Vector2(button.Rect.X, button.Rect.Y), 0f);
                // The warning in Windows' warning orange, as the rail draws it (EditRail.Warning).
                var warning = EditRail.Warning(FlyoutTheme.For(AddListPictures.LookFor(true)));
                plate.Brush = c.CreateColorBrush(warning.Fill);
                desktop.Children.InsertAtTop(plate);
                var glyph = c.CreateSpriteVisual();
                var brush = c.CreateSurfaceBrush(gfx.Glyph(button.Glyph, box, warning.Glyph, box));
                brush.Stretch = CompositionStretch.None;
                glyph.Brush = brush;
                glyph.Size = new Vector2(box, box);
                glyph.Offset = new Vector3(at + new Vector2(button.Rect.X + MathF.Floor((button.Rect.Width - box) / 2f), button.Rect.Y + MathF.Floor((button.Rect.Height - box) / 2f)), 0f);
                desktop.Children.InsertAtTop(glyph);
            }
        }
    }

    /// <summary>
    /// The part of a bar's window the dock shows: its panel strip and the room between it and the edge
    /// of <paramref name="bounds"/> (DockWindow.Takes).
    /// </summary>
    private static RECT Strip(Layout layout, RectI bounds)
    {
        var p = layout.PanelRect;
        var strip = new RECT
        {
            left = layout.X + (int)MathF.Floor(p.X),
            top = layout.Y + (int)MathF.Floor(p.Y),
            right = layout.X + (int)MathF.Ceiling(p.Right),
            bottom = layout.Y + (int)MathF.Ceiling(p.Bottom),
        };
        switch (layout.Edge)
        {
            case Edge.Top:
                strip.top = bounds.Top;
                break;
            case Edge.Left:
                strip.left = bounds.Left;
                break;
            case Edge.Right:
                strip.right = bounds.Right;
                break;
            default:
                strip.bottom = bounds.Bottom;
                break;
        }
        return strip;
    }
}
