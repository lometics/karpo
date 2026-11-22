// menus: a settings menu's levels open in place of its rows (2026-10-20, the person: "i want to try to swap
// the settings in place so that for the settings it does not open a new submenu for each level. i mean the
// "inline" settings"; docs\widgets.md §268, tried on the markets widget's menu alone; 2026-10-22: "port all
// the widgets to the inline menu style now", §271). Every menu that Configures — a widget's, a pin's, a
// folder's line's, a folder's with its faces, on the bar or over a folder (Dock.ShowFlyout) — does; a menu
// of commands Windows cascades too keeps its submenus beside it (the add list's subjects, as Windows'
// "New ›"; Start's "Shut down or sign out ›", as Win+X's).
//
//  - **A level in place.** A row with a submenu — a click on it, Right or Enter on it, Narrator's Invoke —
//    opens no window beside it: the menu's own rows give way to the level's, as Windows 11's quick
//    settings open a page ("< Sound output") or a phone's settings a screen. The level's first row is its
//    head, the back arrow (BackHead.Glyph, Windows' pages' own) and the level's name in the
//    semibold, a rule under it, "Back" its tooltip and what Narrator calls it; the rows the submenu would
//    have hold under that, the same rows in every way, a level of their own as deep as they go.
//  - **Back.** A click on the head, Escape, Backspace or Left (Right in a mirrored menu, Mirror.cs), or
//    the mouse's back button, shows the level under it again, scrolled where it was: by the keys its row
//    lit, by the pointer what the pointer is on now (§409, as Windows' own pages: no row lit the pointer
//    is not on). Escape goes back rather than closing: on the menu's own rows it closes the menu. Whatever
//    has the keys hears them first — a field its Backspace and its arrows, a stepper or segments their
//    Left — and Escape in a field puts its words back, then lets the keys go, then is the level's. A line of text's level shuts
//    by Enter or Escape (CloseLevel); a search's stays for its results.
//  - **A long list is searched** (the person, 2026-10-22: "long select lists that have more than 10 items
//    or so should be searcheable"): a level of more than ListFilterFrom rows to pick and no field of its
//    own — a choice of many, the drives, the status icons, a face gallery's group — has a search field
//    under its head (Searchable), which has the keys as the level opens and leaves the rows whose words
//    hold what is typed, in the language Karpo speaks or in English (FilterWords), as an icon picker's
//    filter leaves its tiles (Flyout.Fields.cs). Ten or fewer stay a plain list. The menu's rule, not a
//    row's: whatever builds a level gets it.
//  - **Standing still.** The head, and on a search's or an icon picker's level its field and what stands
//    over it (a place's device switch), stay at the level's top while the rest scroll under them (pinned):
//    every result or tile is there, a scroll away.
//  - **At its foot** (§276, the person: "height should always be aligned at the bottom and should also
//    not scroll"): the rows that say so (FlyoutItem.Foot: a folder line's "Height ›", LineHeightRows.cs;
//    a widget's "Appearance ›", §311) and the one rule over them (OptionMenu.Foot, §314: "height and
//    appearance should not have a divider between them in the menus") stand still at the foot of the menu,
//    the rest scrolling over them, so they are at the menu's foot on every widget's menu however long its
//    rows. A folder's "Add a face ›" stood there too from §280 to §313; it is its faces' last row now.
//  - **A configuration menu one height** (§276; §288, the person: "the ones that are opened by left click
//    and show the widget configuration or show the folder configuration or the taskbar widget configuration
//    should have a fixed height. basically only the context menus in view mode should have a variable /
//    automatic height"). Edit mode's menu of a widget (Configuration, Dock.WidgetMenu) is SettingsHeightDip
//    tall on every level, a field's too, and every widget's the same (Flyout.Width.cs), to the most the room
//    on its side of the screen holds: a shorter level's rows stand at its top, its foot at its foot, and a
//    longer one scrolls. Going in and back, a Refresh, typing: nothing moves.
//  - **Any other as tall as its rows** (§286, the person: "the right click context menus on the folders
//    should not have a fixed height, these should change with their contents"): a widget's menu at rest,
//    and every level it shows, are their rows' height, to the most the room on its side of the screen
//    holds, and a longer one scrolls; a Refresh that brings more rows or fewer makes
//    it taller or shorter, from the edge kept. A level a field searches or filters (a zone's, a place's, an
//    icon picker, a long list) is what stands over its rows and room for ListFilterFrom rows, as many as a
//    list shows before it is searched, the rest a scroll away, whatever is typed and found (§271: "they
//    should show all the items with the fixed height so that the list is just scrollable in the end"), so
//    nothing moves as it is typed in.
//  - **Where it stands.** The window stays: the level is shown again where the menu stood, its width the
//    widget menu's one width (§230), its height the configuration menu's one or its own, kept from the
//    edge at what it opened from — the foot of a menu over a bar at the screen's bottom, so it grows up from the bar and never over it, the
//    top of one under a bar at the top — inside the work area as a Refresh keeps it (§226). Never placed
//    afresh (MenuSides, Roomiest), so it never moves to the widget's other side.
//  - **The slide.** The level's rows come in from the way they go (in from the end, back from the start):
//    the one motion's Swap (§489, MotionEase.SwapIn, 12 DIP, 167 ms at Normal, decelerating); only their
//    offset moves, never their opacity, so where DWM has stopped animations (FlyoutHost.Open) they are where
//    they belong, or a few px off it, and never unseen.
//  - **What it says.** Whoever showed the menu hears what it would of any of its rows — a choice, a
//    control's Adjust, a field's — and shows it again with the tree as it is now (Refresh), which finds the
//    open levels again in it by their rows (MenuKeeps.Again) and shows the deepest in place, its lit row
//    and scroll kept; a level whose row is gone falls back to the deepest found. A row is found by its id
//    where its words changed (§365, the person: "when entering a new name for the layout and clicking enter
//    i am not brought back to the previous level but i am brought back to the root level"): what is set in
//    a level — a name, an icon, a label — never shuts the levels it stands in.
//  - **One choice goes back** (§392, the person: "when i select someting in a single select menu, like a
//    country i should be brought back after selecting and not just stay in the same menu"). A level whose
//    row says it is one choice (FlyoutItem.PickOne: a choice's, a colour's, an icon's, a search's, a choice
//    of the person's things) goes back to the level under it once a choice is picked on it — by the pointer,
//    the keys or a screen reader, in a long list searched first, a search's result — as the menu is shown
//    again for the pick (Refresh), so the row it opened from says the choice at its end — lit where the
//    keys picked it, as Back lights it (§409) — and the rows slide back as Back slides them. Never before:
//    until the dock has set it the row would say the old value. A level of switches, of rows or of
//    steppers stays, as before.

using System.Numerics;
using TerraFX.Interop.Windows;
using Windows.UI.Composition;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

public sealed unsafe partial class Flyout
{
    /// <summary>§268: the id of a level's head, its back arrow: past every command's and every search result's (Flyout.Fields.cs).</summary>
    internal const uint InlineBackId = 0xFFFF_FFF0;

    /// <summary>A long list's search field's id on the first level, one less a level deeper, so no level's words are another's: under the head's.</summary>
    internal const uint InlineFilterId = 0xFFFF_FFE0;

    /// <summary>
    /// How many rows to pick a level lists before it has a search field (<see cref="Searchable"/>): more than
    /// ten; and the room in rows under the field of a level a field searches or filters (§286).
    /// </summary>
    internal const int ListFilterFrom = 10;

    // §268: the levels open in place, the first opened from the menu's own rows: the row each opened from,
    // as the tree last had it, and how far the level under it was scrolled.
    private readonly List<(FlyoutItem Holder, float Scroll)> levels = [];

    // §268: the showing is a level swapped in place, the menu's own rows again or a Refresh: placed where
    // the menu stands, never afresh.
    private bool swapping;

    // §268: the placement the menu's first showing took (MenuSides, Roomiest), which every level keeps.
    private FlyoutPlacement? placedAt;

    // §226, §268: where the next showing's panel stands, its corner kept (FlyoutHost.Held), null for its
    // own place; and the edge of it kept on the screen: its foot (screen px) where it stands nearer the
    // work area's bottom, null for its top.
    private POINT? holdAt;
    private int? holdFoot;

    // §268: which way the next showing's rows slide in: +1 a level in, -1 back out, 0 not at all.
    private int slideWay;

    // §392: a choice was picked on a level of one choice (FlyoutItem.PickOne): the Refresh for it goes back.
    private bool pickedOne;

    // §409: whether the keys (or a screen reader) drove the menu last rather than the pointer, and where the
    // pointer last was over it (screen px), null since it left: back from a level by the pointer, what lights
    // is what the pointer is on now; by the keys, the row the level opened from.
    private bool byKeys;
    private POINT? pointerScreen;

    // §271: the rows that stay at the level's top while the rest scroll — its head, a search's or an icon
    // picker's field (see the top of the file) — each with where it stands unscrolled, and the band they
    // take, in px; none on a menu's own rows.
    private readonly List<(Row Row, float Home)> pinned = [];
    private float pinnedH;

    // §276: the band the rows at the menu's foot take (FootFrom), in px, which the rest scroll over; they are
    // among the pinned, each with where it stands unscrolled.
    private float footH;

    /// <summary>§268: whether a level of this menu stands in place now: its rows are a submenu's.</summary>
    private bool OnInlineLevel => levels.Count > 0;

    /// <summary>Tests only: how many levels deep the menu stands in place (§268); 0 on its own rows.</summary>
    internal int InlineDepth => levels.Count;

    /// <summary>Tests only: the name of the level shown in place (its head's words), null on the menu's own rows.</summary>
    internal string? InlineTitle => OnInlineLevel ? levels[^1].Holder.Text : null;

    /// <summary>Tests only: the row at <paramref name="at"/> (window px), as the pointer there finds it; null for none.</summary>
    internal uint? IdAt(POINT at) => RowAt(at.x, at.y) is var index and >= 0 ? rows[index].Item.Id : null;

    /// <summary>Tests only: the rows this menu shows now that can be lit, a level's head among them, in their order.</summary>
    internal IReadOnlyList<FlyoutItem> RowsShown => rows.ConvertAll(row => row.Item);

    /// <summary>Tests only: where the menu's panel stands on the screen.</summary>
    internal RECT PanelOnScreen => host.PanelOnScreen;

    /// <summary>Tests only: the materials its window made, and how many times the window itself was placed (§355).</summary>
    internal (int Materials, int Frames) HostMade => (host.MaterialsMade, host.FramesPlaced);

    /// <summary>
    /// §268: row <paramref name="index"/>'s submenu shown in place of the menu's rows, its head on top;
    /// <paramref name="keys"/>: opened by the keys, which go to its first row (a field's level gives its
    /// field them however it opened, §246).
    /// </summary>
    private void OpenInPlace(int index, bool keys)
    {
        if (index < 0 || index >= rows.Count || rows[index].Item.Children is null || gfx is null || shownWith is null)
        {
            return;
        }
        // Words typed in a field here are kept as it lets the keys go.
        if (fieldKeys != 0)
        {
            BlurField(closeSearch: true);
        }
        levels.Add((rows[index].Item, scroll));
        ShowInPlace(+1, null, 0, keys);
    }

    /// <summary>
    /// §339: the level of the menu's row <paramref name="id"/> shown in place, as a click on it shows it:
    /// ⚙ shown again on Back up and reset after an export, an import or a reset, saying how it went; §400: a
    /// row on a level of the menu's, those over it opened first (a way in to Layout ▾ › Appearance › Taskbar
    /// separators). Nothing where the menu stands on a level already or has no such row.
    /// </summary>
    internal void OpenLevel(uint id)
    {
        if (!OnInlineLevel)
        {
            ShowLevel(id);
        }
    }

    /// <summary>
    /// §467: the level of the menu's row <paramref name="id"/> shown in place from wherever the menu stands: the levels shown
    /// that it is not under give way, those over it open, each as a click opens it — a thing just made, its own level (§467 to
    /// §506 a style just added under ⚙ › Styles). Nothing where the menu has no such row, or shows its level already.
    /// </summary>
    internal void ShowLevel(uint id)
    {
        if (PathTo(ShownItems, id) is not { } path)
        {
            return;
        }
        var kept = 0;
        while (kept < levels.Count && kept < path.Count && levels[kept].Holder.Id == path[kept])
        {
            kept++;
        }
        if (kept == path.Count && kept == levels.Count)
        {
            return;
        }
        if (kept < levels.Count)
        {
            if (fieldKeys != 0)
            {
                BlurField(closeSearch: true);
            }
            var under = levels[kept].Scroll;
            levels.RemoveRange(kept, levels.Count - kept);
            ShowInPlace(0, null, under, keys: false);
        }
        foreach (var step in path.Skip(kept))
        {
            OpenInPlace(rows.FindIndex(row => row.Item.Id == step), keys: false);
        }
    }

    /// <summary>§400: the rows with a level from <paramref name="items"/> down to row <paramref name="id"/>, its own last; null where there is none.</summary>
    private static List<uint>? PathTo(IReadOnlyList<FlyoutItem> items, uint id)
    {
        foreach (var item in items)
        {
            if (item.Children is not { } children)
            {
                continue;
            }
            if (item.Id == id)
            {
                return [id];
            }
            if (PathTo(children, id) is { } below)
            {
                below.Insert(0, item.Id);
                return below;
            }
        }
        return null;
    }

    /// <summary>
    /// §317: <paramref name="tool"/>'s level (<see cref="FlyoutTool.Level"/>) shown in place, under the
    /// tool's name, as a row's submenu is (<see cref="OpenInPlace"/>): a note's name field, opened by its
    /// rename button or F2. Going back lights the row the tool stands on.
    /// </summary>
    private void OpenToolLevel(FlyoutTool tool, bool keys)
    {
        if (tool.Level is null || gfx is null || shownWith is null || parent is not null)
        {
            return;
        }
        if (fieldKeys != 0)
        {
            BlurField(closeSearch: true);
        }
        levels.Add((ToolHolder(tool), scroll));
        ShowInPlace(+1, null, 0, keys);
    }

    /// <summary>§317: the row a tool's level stands for, as a level's holder: the tool's id and name, its level the row's submenu.</summary>
    private static FlyoutItem ToolHolder(FlyoutTool tool) => new(tool.Id, tool.Name, Children: tool.Level);

    /// <summary>
    /// §365: the levels open in place found again in <paramref name="items"/>, the menu's rows as they are
    /// now, each by the row it opened from (<see cref="HolderIn"/>), as the tree has it now; whether all were.
    /// A level whose row is gone, or opens no level any more, is shut with those over it: the deepest found
    /// stands. A Refresh's and a menu's opened where it was left (§356): one way for both.
    /// </summary>
    private bool LevelsAgain(IReadOnlyList<FlyoutItem> items)
    {
        var level = items;
        for (var depth = 0; depth < levels.Count; depth++)
        {
            if (HolderIn(level, levels[depth].Holder) is not { Children: { } children } holder)
            {
                levels.RemoveRange(depth, levels.Count - depth);
                return false;
            }
            levels[depth] = (holder, levels[depth].Scroll);
            level = children;
        }
        return true;
    }

    /// <summary>
    /// §365: the row of <paramref name="rows"/> — the rows of a level as they are now — a level open in place
    /// opened from, <paramref name="was"/> as the menu last had it: found again as every row is
    /// (<see cref="MenuKeeps.Again"/>: its id and words, its words, its id), or a tool's whose level it was
    /// (§317: a note's rename); null for none. How a Refresh and a menu opened where it was left (§356) find
    /// each level again, so a level stays open whatever is done in it — its row renamed, drawn anew, moved.
    /// </summary>
    private static FlyoutItem? HolderIn(IReadOnlyList<FlyoutItem> rows, FlyoutItem was)
    {
        if (MenuKeeps.Again(rows, was) is var at and >= 0)
        {
            return rows[at];
        }
        List<FlyoutItem> tools = [.. rows.SelectMany(row => row.Control is FlyoutControl.Tools each ? each.Buttons.Where(tool => tool.Level is not null) : []).Select(ToolHolder)];
        return MenuKeeps.Again(tools, was) is var found and >= 0 ? tools[found] : null;
    }

    /// <summary>
    /// §268: the level shown in place gives way to the one under it, its row lit and scrolled where it was;
    /// whether there was one.
    /// </summary>
    private bool BackInPlace()
    {
        if (!OnInlineLevel || parent is not null || !IsOpen)
        {
            return false;
        }
        if (fieldKeys != 0)
        {
            BlurField(closeSearch: true);
        }
        var (holder, under) = levels[^1];
        levels.RemoveAt(levels.Count - 1);
        ShowInPlace(-1, holder, under, keys: false);
        return true;
    }

    /// <summary>
    /// §392: row <paramref name="item"/>, which stays, picked on the level shown now (see the top of the file):
    /// the Adjust the dock hears; a choice's (<see cref="FlyoutItem.Checked"/>) on a level of one choice goes back
    /// as the menu is shown again for it.
    /// </summary>
    private FlyoutEvent Picked(FlyoutItem item)
    {
        PickedOne(PickedAsOne(item));
        return new FlyoutEvent(FlyoutEventKind.Adjust, item.Id);
    }

    /// <summary>
    /// §392: whether <paramref name="item"/>, picked, is the one choice of its level: a choice's, ticked; §411 (R5): or a
    /// row with no control on a level with no ticks at all, an Add's (a toggle, a face), which goes back to its list too.
    /// A level of the person's things with a tick (a line's notes) stays for "New note".
    /// </summary>
    private bool PickedAsOne(FlyoutItem item) => OneChoice(item, rows.Select(row => row.Item));

    /// <summary>§392, §411 (R5): whether <paramref name="item"/>, picked, is the one choice of <paramref name="level"/> (<see cref="PickedAsOne"/>).</summary>
    internal static bool OneChoice(FlyoutItem item, IEnumerable<FlyoutItem> level) =>
        item.Checked is not null || (item.Control is null && level.All(row => row.Checked is null));

    /// <summary>§392: a choice (<paramref name="choice"/>) picked where the menu stands now; on a level of one choice, the next Refresh goes back.</summary>
    private void PickedOne(bool choice = true)
    {
        choseOne = choice;
        pickedOne = choice && OnInlineLevel && parent is null && levels[^1].Holder.PickOne;
    }

    // §589: the pick heard last was the one choice of its level (PickedOne), on whichever level, the menu's own rows too: a
    // popup of the settings panel closes on it (Dock.PanelDropped), as a ComboBox's list closes. Taken once.
    private bool choseOne;

    /// <summary>§589: whether the pick heard last was the one choice of its level — a choice, an Add's row, a search's find; taken once.</summary>
    internal bool TakeChoseOne()
    {
        var chose = choseOne;
        choseOne = false;
        return chose;
    }

    /// <summary>§268: the rows of the level open in place now — its head, a rule and its submenu's rows —, or the menu's own.</summary>
    private IReadOnlyList<FlyoutItem> InPlaceRows()
    {
        if (!OnInlineLevel)
        {
            return shownWith?.Items ?? [];
        }
        var holder = levels[^1].Holder;
        var back = new FlyoutItem(InlineBackId, holder.Text, new FlyoutIcon.Glyph(Mirror.On ? QuickSettings.Controls.BackHead.ForwardGlyph : QuickSettings.Controls.BackHead.Glyph));
        var rows = holder.Children ?? [];
        List<FlyoutItem> head = [back, new FlyoutItem(0, "", Separator: true)];
        if (Searchable(rows))
        {
            head.Add(new FlyoutItem(InlineFilterId - (uint)levels.Count, S.DockMenuSearch, Control: new FlyoutField("", S.DockMenuSearch, Filters: true, NoMatch: S.DockMenuNoMatch)));
        }
        return [.. head, .. rows];
    }

    /// <summary>
    /// Whether a level of <paramref name="rows"/> is a long list, which a search field heads (see the top
    /// of the file): more than <see cref="ListFilterFrom"/> rows to pick (<see cref="Listed"/>) and no field of its own. §429: a
    /// link is no row to pick but a way out, so it does not count.
    /// </summary>
    internal static bool Searchable(IReadOnlyList<FlyoutItem> rows) =>
        !rows.Any(row => row.Control is FlyoutField) && rows.Count(row => Listed(row) && row.Opens is null) > ListFilterFrom;

    /// <summary>Whether <paramref name="item"/> is a row of a list a search field filters: one with words of its own, neither a heading, a rule, a tile, a field nor a head.</summary>
    internal static bool Listed(FlyoutItem item) =>
        !item.Header && !item.Separator && !item.Primary && !item.Foot && item.Text.Length > 0
        && item.Control is not (FlyoutField or FlyoutMix) && !IsTile(item) && item.Id != InlineBackId;

    /// <summary>
    /// The words a filter finds <paramref name="item"/> by: its own (an icon's or a widget's, <see cref="FlyoutItem.Words"/>),
    /// else its words and their English, its second line and what it says at its end.
    /// </summary>
    internal static IReadOnlyList<string> FilterWords(FlyoutItem item) =>
        item.Words ?? [.. new[] { item.Text, Karpo.Core.Strings.EnglishOf(item.Text), item.Detail, item.Value?.Text }.OfType<string>().Where(word => word.Length > 0)];

    /// <summary>§268: whether <paramref name="items"/> are a level's in place: its head first.</summary>
    private static bool IsLevel(IReadOnlyList<FlyoutItem> items) => items.Count > 0 && items[0].Id == InlineBackId;

    /// <summary>
    /// §268: the level open in place now shown where the menu stands, its rows sliding in <paramref name="way"/>;
    /// <paramref name="lit"/> lit again (the row a level went back to, the row lit before a Refresh) and
    /// scrolled to <paramref name="scrollTo"/>, or, a level just opened, its field given the keys, or its
    /// first row where <paramref name="keys"/>.
    /// </summary>
    private void ShowInPlace(int way, FlyoutItem? lit, float scrollTo, bool keys)
    {
        if (gfx is not { } g || shownWith is not { } shown)
        {
            return;
        }
        KeepPlace();
        // §392: another level shown in place: what was picked was not this one's.
        if (way != 0)
        {
            pickedOne = false;
        }
        slideWay = look.Animations ? way : 0;
        swapping = true;
        try
        {
            Open(g, look, scale, InPlaceRows(), shown.Layout, OnInlineLevel ? null : shown.Title, shown.Placement, null, 0);
        }
        finally
        {
            swapping = false;
        }
        if (OnInlineLevel)
        {
            spokenTitle = levels[^1].Holder.Text;
        }
        ScrollQuietly(scrollTo);
        // §409: back by the pointer, the row under it, if any: the row the level opened from only where it is.
        if (way < 0 && !byKeys)
        {
            SetHot(RowUnderPointer());
            return;
        }
        if (lit is not null && MenuKeeps.Again(rows.ConvertAll(row => row.Item), lit) is var again and >= 0)
        {
            SetHot(again);
            return;
        }
        // §317: back from a tool's level, the row the tool stands on.
        if (lit is not null && rows.FindIndex(row => row.Item.Control is FlyoutControl.Tools tools && tools.Buttons.Any(tool => tool.Level is not null && tool.Id == lit.Id)) is var holding and >= 0)
        {
            SetHot(holding);
            return;
        }
        if (way <= 0)
        {
            return;
        }
        // A field's level gives its field the keys as it opens, however it opened (§230, §246); an icon
        // picker opens on the icon in force (§249).
        if (!FocusFieldLevel() && keys)
        {
            Focus(rows.Count > 1 && rows[0].Item.Id == InlineBackId ? 1 : 0);
        }
        ChosenTileInView();
    }

    /// <summary>§409: who drives the menu, from <paramref name="msg"/>: the keys, or the pointer and where it is.</summary>
    private void Driven(uint msg, LPARAM lParam)
    {
        switch (msg)
        {
            case WM.WM_KEYDOWN or WM.WM_SYSKEYDOWN or WM.WM_CHAR:
                byKeys = true;
                break;
            case WM.WM_MOUSEMOVE or WM.WM_LBUTTONDOWN or WM.WM_LBUTTONUP or WM.WM_RBUTTONDOWN or WM.WM_RBUTTONUP or WM.WM_XBUTTONUP:
                byKeys = false;
                pointerScreen = Screen(new POINT(X(lParam), Y(lParam)));
                break;
            case WM.WM_MOUSELEAVE:
                pointerScreen = null;
                break;
        }
    }

    /// <summary>§409: the row the pointer is on, as the rows stand now; -1 where it is on none, or not on the menu.</summary>
    private int RowUnderPointer()
    {
        if (pointerScreen is not { } at)
        {
            return -1;
        }
        return ScreenToClient(Hwnd, &at) ? RowAt(at.x, at.y) : -1;
    }

    /// <summary>
    /// §268: the next showing stands where the panel does, kept from the edge of it at what it opened from —
    /// its foot over it, its top under it (§286: the room over a bar filled, both are near the screen's) —,
    /// beside it from the edge nearer the work area's (see the top of the file).
    /// </summary>
    private void KeepPlace()
    {
        // §357: a menu following its widget is placed beside it again (Follow).
        if (following)
        {
            return;
        }
        var panel = host.PanelOnScreen;
        holdAt = new POINT(panel.left, panel.top);
        holdFoot = placedAt?.Edge switch
        {
            Karpo.Core.Edge.Bottom => panel.bottom,
            Karpo.Core.Edge.Top => null,
            _ => work.bottom - panel.bottom < panel.top - work.top ? panel.bottom : null,
        };
    }

    /// <summary>
    /// The menu shown again where it stands for <paramref name="items"/>, what its rows stand for now — a
    /// control's value changed, a pick, a field's answer (Flyout.Controls.cs) —: the open levels found
    /// again in the tree by their rows and the deepest shown in place, its lit row and scroll kept — or row
    /// <paramref name="litId"/> lit, where the thing it stood for moved (Flyout.Reorder.cs). A level whose
    /// row is gone falls back to the deepest found, from its top. Nothing where it is not open, or is a
    /// submenu. §365: a level's row is found again whatever its words say now (<see cref="HolderIn"/>): a
    /// layout renamed in its own level, another language (§356). Only what changed is drawn again (Flyout.Retained.cs).
    /// §392: the showing for a choice picked on a level of one choice goes back to the level under it.
    /// §396: in <paramref name="look"/>, its window's look as it is now (the dock's MenuAgain), where one is given: a
    /// colour picked in the menu — its own flyout background among them — is what it is drawn in at once.
    /// </summary>
    public void Refresh(IReadOnlyList<FlyoutItem> items, uint? litId = null, Look? look = null) => Reshow(items, litId, picked: true, look);

    /// <summary>
    /// <see cref="Refresh"/>; <paramref name="picked"/>: whoever showed the menu shows it again for what was
    /// picked (§392), rather than the menu itself for its fields' rows (RelayoutFields) or beside its widget (Follow).
    /// </summary>
    private void Reshow(IReadOnlyList<FlyoutItem> items, uint? litId, bool picked, Look? look = null)
    {
        if (!IsOpen || parent is not null || shownWith is not { } shown || gfx is null)
        {
            return;
        }
        if (look is { } now)
        {
            this.look = now;
        }
        shownWith = shown with { Items = items };
        var lit = hot >= 0 && hot < rows.Count ? rows[hot].Item : null;
        var scrolled = scroll;
        var way = 0;
        if (!LevelsAgain(items))
        {
            (lit, scrolled) = (null, 0);
        }
        // §392: a choice picked on a level of one choice: back to the level under it, its row lit, saying it now.
        else if (picked && pickedOne && OnInlineLevel && levels[^1].Holder.PickOne)
        {
            if (fieldKeys != 0)
            {
                BlurField(closeSearch: true);
            }
            (lit, scrolled) = levels[^1];
            levels.RemoveAt(levels.Count - 1);
            way = -1;
        }
        // §356: and an open submenu with it (a menu that cascades).
        refreshing = true;
        try
        {
            ShowInPlace(way, lit, scrolled, keys: false);
        }
        finally
        {
            refreshing = false;
        }
        if (litId is { } named && rows.FindIndex(row => row.Item.Id == named) is var moved and >= 0)
        {
            SetHot(moved);
        }
    }

    /// <summary>
    /// §357: the open menu moved to stand beside its widget where that stands now — the folder it stood over
    /// handed on to its bar made again (Dock.FoldersFollow) — placed beside it as its first showing placed it
    /// (MenuSides), its level, rows, scroll and lit row kept, as a Refresh keeps them. Nothing for a menu not
    /// open, a submenu, or one that stands beside no widget.
    /// </summary>
    internal void Follow(MenuBeside beside)
    {
        if (!IsOpen || parent is not null || shownWith is not { } shown || BesideWidget is null)
        {
            return;
        }
        BesideWidget = beside;
        following = true;
        try
        {
            Reshow(shown.Items, null, picked: false);
        }
        finally
        {
            following = false;
        }
    }

    // §357: the showing under way places the menu beside its widget again (Follow).
    private bool following;

    /// <summary>
    /// §271: the last of <paramref name="items"/> that stands still at the level's top (see the top of the
    /// file): a search's or an icon picker's field, or a level's head's rule; -1 for none.
    /// </summary>
    private int PinnedThrough(IReadOnlyList<FlyoutItem> items)
    {
        var field = -1;
        for (var i = 0; i < items.Count; i++)
        {
            if (items[i].Control is FlyoutField { Filters: true } or FlyoutField { Finder.Finds: true })
            {
                field = i;
                break;
            }
        }
        if (field >= 0 && (IsLevel(items) || IsPicker(items)))
        {
            return field;
        }
        return IsLevel(items) && items.Count > 1 && items[1].Separator ? 1 : -1;
    }

    /// <summary>
    /// §276, §314: the first of <paramref name="items"/> that stands still at the menu's foot (see the top of
    /// the file): the one rule over the rows at its end that say so (<see cref="FlyoutItem.Foot"/>), or the
    /// first of them; the count of items for none.
    /// </summary>
    internal static int FootFrom(IReadOnlyList<FlyoutItem> items)
    {
        var from = items.Count;
        while (from > 0 && items[from - 1].Foot)
        {
            from--;
        }
        return from < items.Count && from > 0 && items[from - 1].Separator ? from - 1 : from;
    }

    /// <summary>§271, §276: whether <paramref name="row"/> stands still, at the level's top or the menu's foot.</summary>
    private bool IsPinned(Row row) => pinned.Exists(each => each.Row == row);

    /// <summary>
    /// §268: the rows of a level swapped in place slide into <paramref name="window"/>, the visual that
    /// holds them at <paramref name="top"/>, from the way they go — a swap (§489), the offset alone (see the top of the file).
    /// </summary>
    private void SlideIn(Compositor c, Func<IDisposable, IDisposable> keep, Visual window, float top, int way) =>
        MotionEase.SwapIn(c, window, new Vector3(0, top, 0), Vector3.UnitX, way * (mirrored ? -1 : 1), scale, look.Motion, made => keep(made));
}
