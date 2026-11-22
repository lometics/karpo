// Named layouts (the person, 2026-10-08: "named layouts come back"). A layout is an arrangement with
// a name and an icon, and a type that says how its one bar looks:
//
//  - Taskbar: one bar with three sections (start, centre, end), along any edge of the screen;
//  - Dock: three floating docks along any edge, its start at the left end (the top on a side), its
//    middle centred, its end at the right end (the bottom), each as long as what it holds (2026-10-09:
//    "dock mode should get the left and right slot as well").
//
// A layout's widgets live in its bar's three sections (BarSection), one list in their order. Changing
// the type moves nothing (TaskbarLayout.Become). The looks are fixed: a dock floats, a bar wears
// islands. (A third type, Top and bottom, with a second bar and a second row of three sections, went
// on 2026-11-02, the person: "the top and bottom layout style should also be removed".)
//
// Where the bar stands is the display's, not the layout's (Screen.Edge: any of the four; the person,
// 2026-10-12: "i want to be able to show it on the left and right as well"), so one layout can stand at
// the bottom of one display and down the left of another.
//
// The bar shows one of three ways (EdgeMode): all the time holding its space, all the time over the
// windows, or only when the pointer comes near, at its size. All of it is set in the edit bar's Layout
// menu, for the layout shown.
//
// §345: so is how its bars behave, each layout its own (Look: flyouts on hover; Combine; Previews). §346: and
// so do the folders on its bars (Folders: opening on hover). How everything on them looks is no layout's: §567 Karpo's one
// Appearance (Config.Appearance).
//
// Every display shows one layout (Screen.ActiveLayoutId), or none at all (Screen.Enabled false: Karpo
// is hidden there); a display Karpo has not seen shows the default layout (DefaultLayoutId). Two
// displays showing one layout show the same widgets.

using System.Text.Json.Serialization;

namespace Karpo.Core;

/// <summary>
/// How a layout's bar looks (see the top of the file). A word this build does not know reads as a value
/// outside the enum, which <see cref="Config.Repair"/> makes <see cref="Taskbar"/>.
/// </summary>
[JsonConverter(typeof(TolerantEnumConverter<LayoutType>))]
public enum LayoutType
{
    /// <summary>One bar along the display's edge, its start, middle and end sections, each widget an island.</summary>
    [JsonStringEnumMemberName("taskbar")] Taskbar,

    /// <summary>Three floating docks along the display's edge, its start, middle and end sections, each a dock of its own.</summary>
    [JsonStringEnumMemberName("dock")] Dock,
}

/// <summary>A named layout: its name, its icon, its type and bars, and its widgets.</summary>
public sealed class TaskbarLayout
{
    private string? id;

    /// <summary>Made up on first use when the file had none, like <see cref="WidgetItem.Id"/>. Never contains '/'.</summary>
    [JsonPropertyOrder(0)]
    public string Id
    {
        get => id ??= Guid.NewGuid().ToString("N");
        set => id = value;
    }

    /// <summary>Whether an id was given rather than made up on first use.</summary>
    internal bool HasId => !string.IsNullOrWhiteSpace(id);

    [JsonPropertyOrder(0)]
    public string Name { get; set; } = S.CoreLayoutNewName;

    /// <summary>A Segoe Fluent Icons code point.</summary>
    [JsonPropertyOrder(0)]
    public string Icon { get; set; } = TaskbarLayouts.LayoutGlyph;

    /// <summary>How its bar looks (<see cref="LayoutType"/>). Change it with <see cref="Become"/>, which moves nothing.</summary>
    [JsonPropertyOrder(1)]
    public LayoutType Type { get; set; }

    /// <summary>The thickness of its bar, in DIPs as stored; <see cref="BarSize"/> is it clamped.</summary>
    [JsonPropertyOrder(1)]
    public int Size { get; set; } = Config.DefaultHeight;

    /// <summary>How its bar shows.</summary>
    [JsonPropertyOrder(1)]
    [JsonConverter(typeof(TolerantEnumConverter<EdgeMode>))]
    public EdgeMode Show { get; set; } = EdgeMode.Reserve;

    /// <summary>
    /// §533, §589: the room its bars keep between two widgets, in DIPs as stored — as <see cref="Size"/> is, the same room at
    /// every display scale — at every bar size (<c>Layout.WidgetGap</c>); 0 its widgets touch, an islands bar flush (§123's
    /// look). From <see cref="Config.LeastSpacing"/> to <see cref="Config.MostSpacing"/>, <see cref="Config.DefaultSpacing"/>
    /// where the file says none. (§533 to §588 one of three words, "none", "small" and "large": 0, 6 and 12 DIP.)
    /// </summary>
    [JsonPropertyOrder(1)]
    public int Spacing { get; set; } = Config.DefaultSpacing;

    /// <summary>
    /// §514 (the person, 2026-11-05: "open widget flyouts and open folders should be the same setting and not treated
    /// differently"): whether what a widget on its bars opens — a folder, a widget's flyout — opens as the pointer rests on
    /// it (Q7, Karpo.Dock\WidgetHover.cs), else on a click. Each layout its own; off for a new one, a copy takes its source's.
    /// The file's "openOnHover" (§345 to §515 two: the taskbar's "look" and the folders' "folders", each { "openOnHover" }).
    /// §567: how they look is Karpo's one <see cref="Config.Appearance"/>.
    /// </summary>
    [JsonPropertyOrder(1)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool OpenOnHover { get; set; }

    /// <summary>
    /// §345: Windows' "Combine taskbar buttons and hide labels", on its bars, on the main display and
    /// the others alike (<see cref="TaskbarApps"/>). Left out of the file while it is Windows' own default.
    /// </summary>
    [JsonPropertyOrder(1)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public ButtonCombine Combine { get; set; }

    /// <summary>§345: hovering an open app on its bars lists its windows with live previews.</summary>
    [JsonPropertyOrder(1)]
    public bool Previews { get; set; } = true;

    /// <summary>
    /// §362: Windows' "On the taskbar, show all the open windows": on, its bars show the windows of every
    /// virtual desktop, and a click on one of another desktop switches there; off (the default, left out
    /// of the file), those of the desktop in use only.
    /// </summary>
    [JsonPropertyOrder(1)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool AllDesktops { get; set; }

    /// <summary>
    /// Its widgets, each in one of its bar's three sections (<see cref="BarItem.Section"/>), in their
    /// sections' order (<see cref="Sections.Order"/>).
    /// </summary>
    [JsonPropertyOrder(2)]
    public List<BarItem> Islands { get; set; } = [];

    /// <summary>The thickness of its bar, clamped to <see cref="Config.MinHeight"/> to <see cref="Config.MaxHeight"/>.</summary>
    [JsonIgnore]
    public int BarSize => Math.Clamp(Size, Config.MinHeight, Config.MaxHeight);

    /// <summary>
    /// The four edges of a display whose edge is <paramref name="screen"/> showing this layout, as the
    /// dock lays them out: the display's edge has its bar, shown as it shows, at its size, a dock
    /// floating and a bar in islands; every other edge has nothing.
    /// </summary>
    public ScreenEdges Edges(Edge screen)
    {
        var edges = ScreenEdges.Empty();
        var bar = edges[Sections.Known(screen)];
        bar.Layout = Type == LayoutType.Dock ? EdgeLayout.Dock : EdgeLayout.Taskbar;
        bar.Mode = Enum.IsDefined(Show) ? Show : EdgeMode.Reserve;
        bar.Size = BarSize;
        bar.Of = this;
        return edges;
    }

    /// <summary>
    /// Makes it <paramref name="type"/>. Only the type changes: every widget keeps its section.
    /// True when the type changed.
    /// </summary>
    public bool Become(LayoutType type)
    {
        var known = Enum.IsDefined(type) ? type : LayoutType.Taskbar;
        if (Type == known)
        {
            return false;
        }
        Type = known;
        return true;
    }

    /// <summary>
    /// A deep copy under a new id, named <paramref name="name"/>: its look and behaviour (§345, §514), its
    /// widgets with their options, each a copy by the one copy rule
    /// (<see cref="BarItem.Copy"/>) — new ids, a folder's widgets' too — since an id is unique across
    /// every layout: the copy's timers, stopwatches and alarms are its own, not the original's.
    /// </summary>
    public TaskbarLayout CopyAs(string name) => new()
    {
        Name = name,
        Icon = Icon,
        Type = Type,
        Size = Size,
        Show = Show,
        Spacing = Spacing,
        OpenOnHover = OpenOnHover,
        Combine = Combine,
        Previews = Previews,
        AllDesktops = AllDesktops,
        Islands = [.. Islands.Where(island => island is not null).Select(island => island.Copy())],
    };
}

/// <summary>The types of layout.</summary>
public static class LayoutTypes
{
    /// <summary>The types in the order they are offered: Taskbar, Dock.</summary>
    public static readonly LayoutType[] All = [LayoutType.Taskbar, LayoutType.Dock];
}

/// <summary>The glyph and the id a new config's layout starts with.</summary>
public static class TaskbarLayouts
{
    /// <summary>Segoe Fluent Icons: ViewAll, the glyph of a layout that has none of its own.</summary>
    public const string LayoutGlyph = "";

    /// <summary>The name of the layout a new config starts with.</summary>
    public static string DefaultName => S.CoreLayoutDefaultName;

    /// <summary>Its id: the same in both apps, whichever writes a new config first.</summary>
    public const string DefaultId = "default";
}

public sealed partial class Config
{
    /// <summary>The named layouts, at least one once repaired (see the top of the file).</summary>
    public List<TaskbarLayout> Layouts { get; set; } = [];

    /// <summary>
    /// The layout a display gets when Karpo first sees it (<see cref="AddDisplay"/>), and what a
    /// display without an entry shows. §636: there is always one — names an existing layout once repaired
    /// (none, or one that is gone, reads as the first), moves only to another layout, never to none.
    /// </summary>
    public string? DefaultLayoutId { get; set; }

    /// <summary>
    /// The layout <see cref="DefaultLayoutId"/> names, else the first. A config without any layout (a
    /// new one, or one reset) gets <see cref="FirstLayout"/> here: Windows 11's taskbar.
    /// </summary>
    [JsonIgnore]
    public TaskbarLayout DefaultLayout
    {
        get
        {
            Layouts ??= [];
            if ((Layouts.Find(layout => layout is not null && layout.Id == DefaultLayoutId) ?? Layouts.Find(layout => layout is not null)) is { } found)
            {
                return found;
            }
            var made = FirstLayout();
            Layouts.Add(made);
            DefaultLayoutId = made.Id;
            return made;
        }
    }

    /// <summary>
    /// §533: the room between the widgets of the bar this config is laid out as, in DIPs: its layout's (<see cref="Bar"/>), else
    /// the default layout's, read without making one (<see cref="DefaultLayout"/> would); <see cref="DefaultSpacing"/> where
    /// there is none. §589: held to <see cref="LeastSpacing"/> to <see cref="MostSpacing"/>.
    /// </summary>
    [JsonIgnore]
    public int BarSpacing =>
        Math.Clamp((Bar ?? Layouts?.Find(layout => layout is not null && layout.Id == DefaultLayoutId) ?? Layouts?.Find(layout => layout is not null))?.Spacing
        ?? DefaultSpacing, LeastSpacing, MostSpacing);

    /// <summary>
    /// What a new install, or a reset, starts with: one layout, a Taskbar along the bottom holding its
    /// space at 48 DIPs, with Windows 11's taskbar on it (<see cref="Sections.DefaultLayout"/>): §472 Karpo's
    /// layout preset (<see cref="LayoutPresets.Karpo"/>), as "Add a layout" makes it.
    /// </summary>
    public static TaskbarLayout FirstLayout()
    {
        var layout = LayoutPresets.Make(LayoutPresets.Karpo, LayoutMachine.Read(), TaskbarLayouts.DefaultName);
        layout.Id = TaskbarLayouts.DefaultId;
        return layout;
    }

    /// <summary>The layout <paramref name="layoutId"/>, or null.</summary>
    public TaskbarLayout? Layout(string? layoutId) =>
        layoutId is null ? null : Layouts.Find(layout => layout is not null && layout.Id == layoutId);

    /// <summary>
    /// The layout the display shows, or would show were Karpo on there (<see cref="Screen.Enabled"/>).
    /// A display without an entry, or whose layout is gone, shows <see cref="DefaultLayout"/>.
    /// </summary>
    public TaskbarLayout ActiveLayout(string screenId) =>
        Layout(Entry(screenId)?.ActiveLayoutId) ?? DefaultLayout;

    /// <summary>The edge the display's bar stands on (<see cref="Screen.Edge"/>); the bottom for a display with no entry.</summary>
    public Edge EdgeOfScreen(string screenId) => Sections.Known(Entry(screenId)?.Edge ?? Edge.Bottom);

    /// <summary>How each edge of the display shows: as its layout has it at the display's own edge (<see cref="TaskbarLayout.Edges"/>).</summary>
    public ScreenEdges EdgesOf(string screenId) => ActiveLayout(screenId).Edges(EdgeOfScreen(screenId));

    /// <summary>
    /// Shows the layout <paramref name="layoutId"/> on the display, and Karpo there if it was hidden.
    /// True when anything changed: the caller saves.
    /// </summary>
    public bool SelectLayout(string screenId, string layoutId, string name = "")
    {
        if (Layout(layoutId) is null || !IsDisplayId(screenId))
        {
            return false;
        }
        var screen = EditScreen(screenId, name);
        var changed = screen.ActiveLayoutId != layoutId || !screen.Enabled;
        screen.ActiveLayoutId = layoutId;
        screen.Enabled = true;
        return changed;
    }

    /// <summary>
    /// Whether Karpo may be hidden on the display <paramref name="screenId"/> ("None"): only while
    /// another of the displays attached now, <paramref name="attached"/>, still shows it. At least one
    /// display always shows Karpo.
    /// </summary>
    public bool CanHide(string screenId, IEnumerable<string> attached) =>
        attached.Any(other => !string.Equals(other, screenId, StringComparison.OrdinalIgnoreCase) && ScreenFor(other).Enabled);

    /// <summary>
    /// A new layout <paramref name="name"/> of <paramref name="type"/>, holding
    /// <paramref name="islands"/> (each where it stands), at the end of
    /// <see cref="Layouts"/>; shown by no display until one is switched to it.
    /// </summary>
    public TaskbarLayout AddLayout(string name, IEnumerable<BarItem> islands, LayoutType type = LayoutType.Taskbar, string icon = TaskbarLayouts.LayoutGlyph)
    {
        var layout = new TaskbarLayout { Name = name, Icon = icon, Type = type, Islands = [.. islands] };
        Sections.Order(layout.Islands);
        Layouts.Add(layout);
        return layout;
    }

    /// <summary>
    /// Deletes the layout <paramref name="layoutId"/>. Refused (false) for the last layout: there is
    /// always one. Displays showing it show the layout new displays get; when it was that layout, the
    /// first one left becomes it.
    /// </summary>
    public bool DeleteLayout(string layoutId)
    {
        var index = Layouts.FindIndex(layout => layout?.Id == layoutId);
        if (index < 0 || Layouts.Count <= 1)
        {
            return false;
        }
        Layouts.RemoveAt(index);
        if (DefaultLayoutId == layoutId || Layout(DefaultLayoutId) is null)
        {
            DefaultLayoutId = Layouts[0].Id;
        }
        foreach (var screen in Screens.Where(screen => screen?.ActiveLayoutId == layoutId))
        {
            screen.ActiveLayoutId = DefaultLayoutId;
        }
        return true;
    }

    /// <summary>
    /// The layouts, whatever the file said: unique ids without '/', widgets' ids unique across all of
    /// them, names and icons, a type,
    /// sizes and shows this build knows, each list repaired and every widget in a row and section
    /// this build knows, in their order, shown or not; at least one layout, a default that exists, and every display showing
    /// an existing layout. True when it changed more than names, icons, options and clamps (see
    /// <see cref="Repair"/>).
    /// </summary>
    private bool RepairLayouts()
    {
        var changed = false;
        Layouts ??= [];
        changed |= Layouts.RemoveAll(layout => layout is null) > 0;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var layout in Layouts)
        {
            // An id with '/' would break the owners ("layout:<id>") and the island keys.
            if (!layout.HasId || layout.Id.Contains('/') || !ids.Add(layout.Id))
            {
                layout.Id = Guid.NewGuid().ToString("N");
                ids.Add(layout.Id);
                changed = true;
            }
        }
        if (Layouts.Count == 0)
        {
            // A new config, a reset, or a file whose layouts were all taken out.
            ids.Add(DefaultLayout.Id);
            changed = true;
        }
        if (DefaultLayoutId is null || !ids.Contains(DefaultLayoutId))
        {
            DefaultLayoutId = Layouts[0].Id;
            changed = true;
        }
        // A widget's id is unique across every layout, not only its own: the dock keeps a widget's
        // own state by its id alone — a timer's countdown, a stopwatch, an alarm's ring — so two of
        // one id in two layouts ran one timer, rang one alarm and heard only the first one's edits.
        // Where the file has two, the first keeps its id, in the layouts' order (a layout duplicated
        // comes after the one it was copied from), and the later one is given a new one.
        var widgets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var layout in Layouts)
        {
            layout.Name = string.IsNullOrWhiteSpace(layout.Name) ? S.CoreLayoutNewName : layout.Name.Trim();
            layout.Icon = string.IsNullOrWhiteSpace(layout.Icon) ? TaskbarLayouts.LayoutGlyph : layout.Icon;
            if (!Enum.IsDefined(layout.Type))
            {
                layout.Type = LayoutType.Taskbar;
                changed = true;
            }
            if (!Enum.IsDefined(layout.Show))
            {
                layout.Show = EdgeMode.Reserve;
                changed = true;
            }
            // §345: its app buttons' setting from another build reads as Windows' own default.
            if (!Enum.IsDefined(layout.Combine))
            {
                layout.Combine = ButtonCombine.Always;
                changed = true;
            }
            layout.Size = Math.Clamp(layout.Size == 0 ? DefaultHeight : layout.Size, MinHeight, MaxHeight);
            // §589: a spacing typed by hand past what is offered, held to it, as a size is.
            layout.Spacing = Math.Clamp(layout.Spacing, LeastSpacing, MostSpacing);
            layout.Islands ??= [];
            changed |= RepairList(layout.Islands, widgets);
            // Every entry in a section this build knows; one whose file says none reads as the start's.
            foreach (var island in layout.Islands)
            {
                if (!Enum.IsDefined(island.Section))
                {
                    island.Section = BarSection.Start;
                    changed = true;
                }
            }
            changed |= Sections.Order(layout.Islands);
        }
        foreach (var screen in Screens)
        {
            if (screen.ActiveLayoutId is null || !ids.Contains(screen.ActiveLayoutId))
            {
                // A display always has a layout, Karpo on there or not: the one new displays get.
                screen.ActiveLayoutId = DefaultLayoutId;
                changed = true;
            }
        }
        return changed;
    }
}
