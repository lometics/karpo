// The notification list as one ordinary widget (docs\widgets.md §9, §100): a line of a folder's
// list, and since the bell went (2026-10-10) the whole of what its flyout did — Windows 11's
// notification center — drawn on the kit's blocks as every list is (§401; the person, 2026-10-30:
// "they should be redesigned with the edge to edge style as well. they should fit in with the other
// widgets in terms of style").
//
// **The kit's list, as mail's and the feed's.** Its Header: the bell in the leading column,
// "Notifications" strong at the text edge, do not disturb's toggle — "zZ" on the accent while it is
// on — and "Clear all" against the trailing gutter; it does not say how many there are (2026-10-31,
// §454). Every button of the line is "Clear all"'s size (LineButton, §353). Under it the
// notifications grouped by app as Windows groups them (NotificationText.Groups): an app's heading,
// a compact row as the agenda's day — its icon in the leading column, its name at the text edge, under the pointer the × that clears its notifications —
// over every one of its notifications, newest first: no "See more (2)", the line scrolls (§461). A notification
// is the kit's item (LineItem, §428), no card: its band of the line, edge to edge, touching the next,
// lit under the pointer; its own picture in the leading column as a headline's; its title strong at
// the text edge with when it came on its line against the trailing gutter, its body in the secondary
// ink under it on two lines; its buttons the line's standard buttons under its words. Under the
// pointer its chevron, where there is more to it, and its × stand where the time was, each its own
// target over the band. The chevron expands it: all of its words, its pictures, its boxes, selections
// and every button in WinUI's shapes. A click on it brings its app forward with the toast's own launch
// arguments; a button with its own arguments and what was typed and chosen; Enter in a box with the
// button the box belongs to (ToastActivation.cs). Each clears the notification, as Windows clears a
// toast once it is acted on. What brings an app forward is the folder's click that acts
// (CellAct.Action): the folder goes and the dock hands the activation on while the right to the
// foreground is still Karpo's (Dock.Folders.cs, FolderForward); a background one (a "Like", a reply
// sent) is handed on here and the folder stays. Its footer is the preset's premade actions.
//
// **A line of every height.** Nothing to list: the kit's Status, one row. On one row: the header, the
// bell, "Notifications", the toggle and "Clear all". Taller, the whole list, from the top, its last band on
// its foot (§436). Held shorter than it is, the list scrolls under the header (ListScroll.cs). Its
// height follows its notifications (IListContent), at once, up to the most the line is set to. Its
// items are drawn in the item style chosen (ItemForm): their size, and their bodies, pictures and times.
//
// **What it keeps.** What is expanded, typed and chosen (NotificationView) is the showing's,
// as Windows' own forgets it: gone when the folder closes. The notifications are the store's, the
// one store the bell face reads too, so "cleared" means the same everywhere.
//
// **Read-only while arranging.** While the folder is arranged it tells a widget the pointer is
// nowhere (FolderFlyout.HoverOver), and every control here acts only on what the pointer is over.

using System.Numerics;
using Karpo.Core;
using Karpo.Dock.Native;
using Karpo.Dock.QuickSettings.Controls;
using Karpo.Dock.QuickSettings.Services;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

/// <summary>
/// notifications: what a right click on a notification list works — the line's own in an open folder,
/// and the bell face's on the bar (FolderIsland), on the face's store: "Clear all" and do not disturb.
/// </summary>
internal interface INotificationActs
{
    /// <summary>Whether "Clear all" has anything to clear.</summary>
    bool HasAny { get; }

    /// <summary>"Clear all": here and in Windows' own list.</summary>
    void ClearAll();

    /// <summary>Do not disturb, the service its rows switch (Dock.WidgetMenus.cs, DoNotDisturbRows).</summary>
    DoNotDisturb Dnd { get; }
}

internal sealed unsafe partial class NotificationPartTile : CellIsland, IListContent, IListScroll, ILinePieces, INotificationActs, ILineTargets
{
    // Segoe Fluent Icons: Ringer, the bell; Cancel, the notification center's own × (its XAML's
    // DismissButton); ChevronDown and ChevronUp, an expander's and a selection's.
    internal static readonly string BellGlyph = char.ConvertFromUtf32(0xEA8F);
    internal static readonly string DismissGlyph = char.ConvertFromUtf32(0xE711);
    internal static readonly string ExpandGlyph = char.ConvertFromUtf32(0xE70D);
    internal static readonly string FoldGlyph = char.ConvertFromUtf32(0xE70E);

    /// <summary>How many lines a notification's title takes until it is expanded: one, as a message's sender in mail.</summary>
    internal const int TitleLines = 1;

    /// <summary>How many lines a notification's body takes until it is expanded: two, cut with an ellipsis.</summary>
    internal const int BodyLines = 2;

    /// <summary>How many lines an expanded body may take: all of any toast's.</summary>
    internal const int MostLines = 60;

    /// <summary>
    /// tests: the store a line the registry makes reads, and the bell face, in this thread's place of
    /// <see cref="NotificationStore.Shared"/>; null for the shared one, which is what the dock uses.
    /// </summary>
    [ThreadStatic]
    private static NotificationStore? stand;

    internal static NotificationStore? Stand
    {
        get => stand;
        set => stand = value;
    }

    private readonly NotificationStore store;
    private readonly DoNotDisturb quiet;
    private readonly Dictionary<string, Pixels?> images = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>What is under the pointer, and the number the folder was given for it (<see cref="CardTarget"/>).</summary>
    private Target hot;

    private int hotId;

    /// <summary>The target a press is down on: a notification lights pressed (RowPress).</summary>
    private RowPress press;

    /// <summary>The ids the last hit test gave out, by what they stand for: the folder's click comes back with one.</summary>
    private readonly List<Target> given = [];

    private bool followingQuiet;

    /// <summary>Whether do not disturb was handed in (a test's), rather than the dock's own.</summary>
    private readonly bool ownQuiet;

    /// <summary>A click that brings an app forward, for the dock to hand on once the folder has gone (<see cref="ActivatePending"/>).</summary>
    private ToastActivation? pending;

    // Where the line was last drawn, for the keys of the box that has them.
    private D2D_RECT_F shownBox;
    private TileStyle shownStyle;
    private bool drawn;

    public NotificationPartTile(string key, WidgetItem island, Gfx gfx, Config config)
        : this(key, island, gfx, config, Stand ?? NotificationStore.Shared)
    {
    }

    /// <summary>A line on <paramref name="store"/> and <paramref name="quiet"/>: the dock's are the shared ones; tests bring their own.</summary>
    internal NotificationPartTile(string key, WidgetItem island, Gfx gfx, Config config, NotificationStore store, DoNotDisturb? quiet = null)
        : base(key, island, gfx)
    {
        this.store = store;
        this.quiet = quiet ?? DoNotDisturb.Shared;
        ownQuiet = quiet is not null;
        Configure(island, config);
        store.Use(this);
    }

    /// <summary>
    /// What the line shows, or null for the store's own — which is what the dock always uses. A
    /// picture or a test hands one in rather than arranging a notification database.
    /// </summary>
    internal IReadOnlyList<NotificationEntry>? Shown { get; set; }

    /// <summary>The notifications this line is about: the ones handed to it, else the store's.</summary>
    private IReadOnlyList<NotificationEntry> Entries => Shown ?? store.Items;

    /// <summary>What this showing has opened, expanded, typed and chosen.</summary>
    internal NotificationView View { get; } = new();

    /// <summary>The store it reads and clears.</summary>
    internal NotificationStore Store => store;

    /// <summary>The caret blinks while a box has the keys; nothing else ticks: the store wakes it.</summary>
    public override int IntervalMs => CardTyping ? 500 : 0;

    protected override int Count => 1;

    public override void Configure(WidgetItem island, Config config)
    {
        Settings = island;
        Forget();
    }

    /// <summary>Nothing of its own: a component is drawn, and what it is about is its folder's.</summary>
    public override string? Tooltip(int index) => null;

    /// <summary>folders: whether its menu's "Clear all" has anything to clear.</summary>
    public bool HasAny => Entries.Count > 0;

    /// <summary>folders: its menu's "Clear all" — what the list's own button does.</summary>
    public void ClearAll()
    {
        store.DismissAll();
        Redrawn();
    }

    /// <summary>The folder was opened to look at it: everything in it counts as seen, and the bell's count goes.</summary>
    internal void Seen() => store.MarkSeen();

    /// <summary>folders: do not disturb, for the list's menu in a folder (Dock.WidgetMenus.cs): the one the header switches.</summary>
    DoNotDisturb INotificationActs.Dnd => quiet;

    /// <summary>Do not disturb's switch, the line's header's: Windows' setting.</summary>
    public IQuickSwitch Quiet
    {
        get
        {
            if (quietSwitch is null)
            {
                // The do not disturb widget's own switch (QuickSwitches), on the same setting the
                // header switches; on the one handed in where a test brought its own.
                quietSwitch = ownQuiet ? new DoNotDisturbSwitch(WidgetInfos.DoNotDisturb.Title, quiet) : QuickSwitches.For(QuickSwitch.DoNotDisturb);
                quietSwitch.Start(Post);
            }
            return quietSwitch;
        }
    }

    private IQuickSwitch? quietSwitch;

    /// <summary>Do not disturb, followed from the first time the line is drawn (docs\widgets.md §11).</summary>
    private void FollowQuiet()
    {
        if (followingQuiet)
        {
            return;
        }
        followingQuiet = true;
        quiet.Changed += QuietChanged;
        quiet.Look();
    }

    private void QuietChanged() => Post();

    /// <summary>What do not disturb is now, for the switch and an empty list's words.</summary>
    private DndState QuietState => followingQuiet ? quiet.State : DndState.Unknown;

    // --- Clicks ------------------------------------------------------------------------------------

    /// <summary>
    /// What the click the hit test found does. Each arm asks the pointer as well (<see cref="hot"/>),
    /// because the pointer is what a folder being arranged takes away: a press that picked a line up
    /// and was let go without moving still reaches here.
    /// </summary>
    public override IslandAction Click(int index)
    {
        if (press.Up())
        {
            _ = Wake();
        }
        if (index == 0 || index != hotId)
        {
            return default;
        }
        var target = hot;
        switch (target.Spot)
        {
            case Spot.Clear:
                ClearAll();
                return default;
            case Spot.Quiet:
                // Switched in place, as Windows' own is; where Windows would not let Karpo, its settings.
                Quiet.Act();
                Redrawn();
                return default;
            case Spot.GroupClose:
                store.DismissApp(target.AppId);
                Rest();
                return default;
        }
        if (Find(target.Item) is not { } item)
        {
            return default;
        }
        switch (target.Spot)
        {
            case Spot.Close:
                store.Dismiss(item.Id);
                Rest();
                return default;
            case Spot.Chevron:
                if (!View.Expanded.Remove(item.Id))
                {
                    View.Expanded.Add(item.Id);
                }
                else if (View.Focus?.Id == item.Id)
                {
                    View.Focus = null;
                }
                Redrawn();
                return default;
            case Spot.Part when PartsOf(item) is { } parts && target.Part >= 0 && target.Part < parts.Count:
                return Chosen(item, parts[target.Part]);
            case Spot.Body:
                // The card itself: its app, with the toast's own launch arguments, as Windows' does.
                return Activated(item, ToastActivation.Launch(item));
            default:
                return default;
        }
    }

    /// <summary>One of an expanded card's controls, clicked: a button pressed, a box given the keys, a selection stepped on.</summary>
    private IslandAction Chosen(NotificationEntry item, (PartKind Kind, ToastButton? Button, ToastInput? Input) part)
    {
        switch (part.Kind)
        {
            case PartKind.Button when part.Button is { } button:
                return Pressed(item, button);
            case PartKind.Box when part.Input is { } input:
                View.Box(item.Id, input);
                View.Focus = (item.Id, input.Id);
                Redrawn();
                return default;
            case PartKind.Choice when part.Input is { Choices.Count: > 0 } input:
            {
                var at = input.Choices.ToList().FindIndex(choice => choice.Id == View.Choice(item.Id, input));
                View.Chosen[(item.Id, input.Id)] = input.Choices[(at + 1) % input.Choices.Count].Id;
                Redrawn();
                return default;
            }
            default:
                return default;
        }
    }

    /// <summary>A toast's button: Windows' own Dismiss clears the notification; any other activates its app with the button's arguments and every input's value.</summary>
    private IslandAction Pressed(NotificationEntry item, ToastButton button)
    {
        if (button.Dismisses)
        {
            store.Dismiss(item.Id);
            Rest();
            return default;
        }
        return Activated(item, ToastActivation.Of(item, button, View.Values(item)));
    }

    /// <summary>
    /// An activation of <paramref name="item"/>'s app, which clears the notification. What brings the
    /// app forward goes to the dock through the folder's click (an app opening), which hands it on
    /// once the folder has gone; a background one is handed on here, and the folder stays.
    /// </summary>
    private IslandAction Activated(NotificationEntry item, ToastActivation activation)
    {
        store.Dismiss(item.Id);
        Rest();
        if (activation.Closes)
        {
            pending = activation;
            return new IslandAction(View: IslandView.App);
        }
        // §491: a background one shows nothing, and closes nothing.
        Launch.Toast(activation);
        return default;
    }

    /// <summary>
    /// The dock, once the folder has gone: the activation a click asked for is handed on now, while
    /// the right to the foreground from the click is still Karpo's to pass on (ToastActivation.cs).
    /// </summary>
    internal void ActivatePending()
    {
        if (pending is { } activation)
        {
            pending = null;
            Launch.Toast(activation);
        }
    }

    /// <summary>tests: the activation waiting for the folder to go.</summary>
    internal ToastActivation? Pending => pending;

    /// <summary>Something went from under the pointer: nothing is lit until it moves, and the line is laid out again.</summary>
    private void Rest()
    {
        hot = default;
        hotId = 0;
        Redrawn();
    }

    /// <summary>The notification <paramref name="id"/> names, where it is still there.</summary>
    private NotificationEntry? Find(long id) => Entries.FirstOrDefault(entry => entry.Id == id);

    /// <summary>An expanded card's controls, in the order they are laid out: its inputs, then its buttons.</summary>
    private static List<(PartKind Kind, ToastButton? Button, ToastInput? Input)> PartsOf(NotificationEntry item)
    {
        var offer = item.Offer ?? ToastOffer.None;
        var parts = new List<(PartKind, ToastButton?, ToastInput?)>();
        foreach (var input in offer.Inputs)
        {
            parts.Add((input.Kind == ToastInputKind.Selection ? PartKind.Choice : PartKind.Box, null, input));
        }
        foreach (var button in offer.Buttons)
        {
            parts.Add((PartKind.Button, button, null));
        }
        return parts;
    }

    // --- The keys of a box ------------------------------------------------------------------------

    /// <summary>notes: a toast's box has the keys — on a card that is still there and still expanded.</summary>
    protected internal override bool CardTyping =>
        View.Focus is { } focus && View.Expanded.Contains(focus.Id) && Find(focus.Id) is not null;

    protected internal override void CardStopTyping()
    {
        if (View.Focus is not null)
        {
            View.Focus = null;
            Redrawn();
        }
    }

    protected internal override bool CardChar(char c, WidgetSurface surface)
    {
        if (!CardTyping || Focused() is not { } editor)
        {
            return false;
        }
        if (editor.Char(c))
        {
            Redrawn();
        }
        return true;
    }

    /// <summary>
    /// A key while a box has them: Escape lets them go (the folder's Escape comes after, and closes it),
    /// Tab too and moves on, Enter sends it with the button it belongs to, or the toast's one button;
    /// the rest are the box's own (InlineEditor).
    /// </summary>
    protected internal override bool CardKey(int key, WidgetSurface surface)
    {
        if (!CardTyping || View.Focus is not { } focus || Find(focus.Id) is not { } item || Focused() is not { } editor)
        {
            return false;
        }
        switch (key)
        {
            case VK.VK_ESCAPE:
                CardStopTyping();
                return true;
            case VK.VK_TAB:
                CardStopTyping();
                return false;
            case VK.VK_RETURN:
            {
                var offer = item.Offer ?? ToastOffer.None;
                var buttons = offer.Buttons.Where(button => !button.Dismisses).ToList();
                var button = offer.Buttons.FirstOrDefault(candidate => candidate.InputId == focus.Input)
                    ?? (buttons.Count == 1 ? buttons[0] : null);
                if (button is not null)
                {
                    View.Focus = null;
                    // From the keys there is no click for the folder to forward: the app is reached here,
                    // and the folder goes as it comes forward.
                    var action = Pressed(item, button);
                    if (action.View == IslandView.App)
                    {
                        ActivatePending();
                    }
                }
                return true;
            }
        }
        var form = ListForm.Of(Gfx, shownStyle);
        _ = editor.Key(key, KeyState.Down(VK.VK_SHIFT), KeyState.Down(VK.VK_CONTROL), Gfx, form.TextPx, FocusedBox(), shownStyle, out _);
        Redrawn();
        return true;
    }

    /// <summary>A press on a box: it takes the keys, and the caret goes where the pointer is.</summary>
    protected internal override bool CardPress(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var target = LineTarget(point, box, style, out var rect);
        if (target.Spot != Spot.Part || Find(target.Item) is not { } item || PartsOf(item) is not { } parts
            || target.Part < 0 || target.Part >= parts.Count || parts[target.Part] is not { Kind: PartKind.Box, Input: { } input })
        {
            // Anything else stays the folder's press, which makes it the click; a notification lights pressed meanwhile.
            if (press.Down(IdOf(target)))
            {
                _ = Wake();
            }
            return false;
        }
        _ = press.Up();
        var editor = View.Box(item.Id, input);
        View.Focus = (item.Id, input.Id);
        editor.Press(point, 1, KeyState.Down(VK.VK_SHIFT), Gfx, ListForm.Of(Gfx, style).TextPx, rect, style);
        Redrawn();
        return true;
    }

    protected internal override void CardDrag(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        if (Focused() is { } editor)
        {
            editor.Drag(point, Gfx, ListForm.Of(Gfx, style).TextPx, FocusedBox(), style);
            Redrawn();
        }
    }

    /// <summary>The box that has the keys, where there is one.</summary>
    private InlineEditor? Focused() =>
        View.Focus is { } focus && View.Boxes.TryGetValue(focus, out var editor) ? editor : null;

    /// <summary>Where the box that has the keys was last drawn.</summary>
    private D2D_RECT_F FocusedBox()
    {
        if (!drawn || View.Focus is not { } focus)
        {
            return default;
        }
        var targets = new List<(Target Target, D2D_RECT_F Box)>();
        Lay(new LineSketch(null, Gfx), shownBox, shownStyle, targets);
        foreach (var (target, rect) in targets)
        {
            if (target is { Spot: Spot.Part } && target.Item == focus.Id && Find(focus.Id) is { } item
                && PartsOf(item) is var parts && target.Part < parts.Count && parts[target.Part].Input?.Id == focus.Input)
            {
                return rect;
            }
        }
        return default;
    }

    // --- The showing -------------------------------------------------------------------------------

    /// <summary>
    /// The folder opened or closed: what was expanded and typed goes with the showing. The
    /// activation a click asked for does not: the folder closes before the dock hands it on
    /// (FolderFlyout hides, then forwards), so it outlives the showing until <see cref="ActivatePending"/>
    /// takes it. Dropped here, every click on a notification did nothing (docs\widgets.md §294).
    /// </summary>
    public override void Visible(bool shown)
    {
        base.Visible(shown);
        if (!shown)
        {
            View.Clear();
            hot = default;
            hotId = 0;
            _ = press.Up();
        }
    }

    /// <summary>§357: the notifications expanded and the choices made, kept as the folder closes in edit mode.</summary>
    private sealed record Kept(long[] Expanded, KeyValuePair<(long Id, string Input), string>[] Chosen);

    internal override object? ViewMemory() => new Kept([.. View.Expanded], [.. View.Chosen]);

    internal override void RecallView(object memory)
    {
        if (memory is not Kept kept)
        {
            return;
        }
        View.Expanded.UnionWith(kept.Expanded);
        foreach (var (key, value) in kept.Chosen)
        {
            View.Chosen[key] = value;
        }
    }

    public override void Dispose()
    {
        store.Release(this);
        quietSwitch?.Dispose();
        quietSwitch = null;
        if (followingQuiet)
        {
            followingQuiet = false;
            quiet.Changed -= QuietChanged;
        }
        base.Dispose();
    }

    /// <summary>The times are written to the minute ("2m"), so the line follows the minute.</summary>
    public override bool MinuteChanged(Gfx gfx) => Redraw(gfx);

    /// <summary>The store posted: it takes what it read, and the folder redraws the line; a tick blinks the caret.</summary>
    protected override bool Sample(bool everything)
    {
        if (everything)
        {
            store.Deliver();
        }
        else if (CardTyping)
        {
            Focused()?.Blink();
        }
        return false;
    }

    /// <summary>Never drawn on a bar: it is a line of a list and nothing else.</summary>
    internal override void PaintCell(ID2D1DeviceContext* dc, int index, in IslandStyle style, float width, float height) =>
        throw new NotSupportedException($"{Info.Id} is a line of a folder's list, not a widget of a bar.");

    protected internal override void PaintCard(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        FollowQuiet();
        shownBox = box;
        shownStyle = style;
        drawn = true;
        Lay(new LineSketch(dc, Gfx), box, style, null);
    }

    /// <summary>Only a notification, a heading's ×, a fold and the buttons light under the pointer, never the line around them.</summary>
    protected internal override bool LightsItsRows => true;

    /// <summary>Where the pointer rests: the line is drawn again through the folder, which owns its surface.</summary>
    protected internal override void CardHover(Vector2? point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var target = point is { } at ? LineTarget(at, box, style, out _) : default;
        var let = press.Over(IdOf(target));
        if (target == hot && !let)
        {
            return;
        }
        hot = target;
        hotId = IdOf(target);
        _ = Wake();
    }

    // The wheel is not this widget's: nothing inside a folder scrolls, so it leaves it to the panel.

    protected internal override int CardTarget(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface) =>
        IdOf(LineTarget(point, box, style, out _));

    /// <summary>keys: every target as laid out, a notification's body after everything on it, as <see cref="LineTarget"/> reads them (ILineTargets).</summary>
    public IReadOnlyList<(int Target, D2D_RECT_F Box)> LineTargets(D2D_RECT_F box, in TileStyle style)
    {
        var targets = new List<(Target Target, D2D_RECT_F Box)>();
        Lay(new LineSketch(null, Gfx), box, style, targets);
        return [.. targets.Where(each => each.Target.Spot is not Spot.Body).Concat(targets.Where(each => each.Target.Spot is Spot.Body))
            .Select(each => (IdOf(each.Target), each.Box)).Where(each => each.Item1 != 0)];
    }

    /// <summary>The number the folder carries for <paramref name="target"/> to <see cref="Click"/>: 0 for nothing.</summary>
    private int IdOf(Target target)
    {
        if (target.Spot == Spot.None)
        {
            return 0;
        }
        var at = given.IndexOf(target);
        if (at < 0)
        {
            given.Add(target);
            at = given.Count - 1;
        }
        return at + 1;
    }

    /// <summary>tests: what a number <see cref="CardTarget"/> gave stands for.</summary>
    internal Target TargetOf(int id) => id > 0 && id <= given.Count ? given[id - 1] : default;

    /// <summary>What is at <paramref name="point"/> on a line, and where it is: a control before the row it stands on.</summary>
    private Target LineTarget(Vector2 point, D2D_RECT_F box, in TileStyle style, out D2D_RECT_F rect)
    {
        var targets = new List<(Target Target, D2D_RECT_F Box)>();
        Lay(new LineSketch(null, Gfx), box, style, targets);
        foreach (var precise in new[] { true, false })
        {
            foreach (var (target, at) in targets)
            {
                if ((target.Spot is Spot.Body) != precise && LineRows.Inside(at, point))
                {
                    rect = at;
                    return target;
                }
            }
        }
        rect = default;
        return default;
    }

    // --- As a line of every height ------------------------------------------------------------------

    /// <summary>What a line of one row shows first: "Notifications", the header's name.</summary>
    public string MainPiece => "head";

    public IReadOnlyList<LinePiece> LinePieces(D2D_RECT_F box, in TileStyle style)
    {
        var sketch = new LineSketch(null, Gfx);
        Lay(sketch, box, style, null);
        return sketch.Pieces;
    }

    /// <summary>The air over and under a notification's words, DIP at the item style's size: mail's and the feed's (§401).</summary>
    private const float ItemAir = 8;

    /// <summary>The sizes every form of the line shares, at a style, in px: the kit's (§401, §428).</summary>
    /// <param name="HeadLine">The Header: the kit's row, 40 DIP.</param>
    /// <param name="Button">Every button of the line — "Clear all", do not disturb's toggle, the ×, the chevron, a toast's
    /// own: the lines' one (LineButton).</param>
    /// <param name="Heading">An app's heading: the kit's compact row, 32 DIP.</param>
    /// <param name="Air">Over and under a notification's words: the band's own, half of it each neighbour's.</param>
    /// <param name="Gap">Between a notification's words and its buttons, pictures and boxes.</param>
    /// <param name="Field">An expanded notification's box or selection: WinUI's 32 DIP.</param>
    /// <param name="Logo">A toast's own picture in the leading column: the kit's Feature size, 24 DIP, as the feed's.</param>
    internal readonly record struct Sizes(ListForm Form, ItemForm Items, float HeadLine, LineButton Button, float Heading, float Air, float Gap,
        float Field, float Logo);

    internal Sizes SizesOf(in TileStyle style)
    {
        var form = ListForm.Of(Gfx, style);
        var items = ItemForm.For(Gfx, style, Settings);
        return new Sizes(form, items, LineRow.Height(style, caption: false), LineButton.Of(Gfx, style), LineRow.Compact(style),
            Air: items.Space(style, ItemAir), Gap: style.Px(LineSpace.GapDip), Field: MathF.Max(style.Px(32), PopupPaint.Round(form.Line + style.Px(12))),
            Logo: items.Side(LineGlyph.Feature(style)));
    }

    /// <summary>
    /// The line, at whatever height it was given (docs\widgets.md §9, §401), drawn where the sketch draws, on the kit's blocks:
    /// <list type="bullet">
    /// <item>Nothing to list: the kit's Status — the bell, "No new notifications" (do not disturb's words while it is on,
    /// why where they cannot be read) — one row, do not disturb's toggle at its end.</item>
    /// <item>One row: the Header alone — the bell in the list's leading column, "Notifications", do not disturb's toggle
    /// and "Clear all" against the trailing gutter.</item>
    /// <item>Taller, from where an app's heading and its newest notification's first line fit under it: the Header — the
    /// bell, "Notifications" strong, the toggle and "Clear all", never how many there are — over each app's heading and its notifications
    /// as the kit's items (<see cref="Grouped"/>); held shorter, it scrolls (ListScroll.cs).</item>
    /// </list>
    /// With <paramref name="targets"/> it writes down where each click lands.
    /// </summary>
    private void Lay(LineSketch sketch, D2D_RECT_F box, in TileStyle style, List<(Target Target, D2D_RECT_F Box)>? targets)
    {
        var sizes = SizesOf(style);
        var form = sizes.Form;
        var theme = style.Theme;
        var height = box.bottom - box.top;
        var right = box.right - form.Inset;
        var entries = NotificationText.Newest(Entries);
        var available = Shown is not null || store.Available;
        var state = QuietState;
        var buttons = sizes.Button;
        var hotNow = hot;
        var pressed = press.On(hotId);
        var gap = sizes.Gap;
        // Do not disturb's toggle: WinUI's ToggleButton, the standard button, the accent's while on.
        var toggle = new RowControl("quiet", buttons.Height, buttons.Height, "", DoNotDisturb.Glyph,
            state.On == true ? ButtonStyle.Accent : ButtonStyle.Standard, hotNow.Spot == Spot.Quiet, pressed, state.On is not null);
        if (entries.Count == 0 || !available)
        {
            var none = !available ? S.WidgetNotificationsUnreadable : state.On == true ? S.DockNotificationsQuietTitle : S.WidgetNotificationsNone;
            LineStatus.Draw(sketch, box, style, BellGlyph, none, [], toggle.Width + gap);
            var square = buttons.Square(right - toggle.Width, box.top + PopupPaint.Round(MathF.Min(height, LineRows.Px(1, style)) / 2));
            sketch.Button(toggle.Name, buttons, theme, square, "", toggle.Glyph, toggle.Style!.Value, toggle.Hot, toggle.Pressed, toggle.Enabled);
            targets?.Add((new Target(Spot.Quiet), buttons.Hit(square, box.top, box.bottom)));
            return;
        }
        var one = height + 0.5f < Lists(sizes);
        var name = S.WidgetNotificationsName;
        var clear = RowControl.Button(Gfx, buttons, "clear all", S.WidgetNotificationsClearAll, null, ButtonStyle.Standard, hotNow.Spot == Spot.Clear, pressed);
        // The name wins: where it and both controls do not fit on one row (a large text size) the toggle gives way first
        // (it is on the line's menu too), then "Clear all" (the menu's, and each app's ×).
        var controls = new List<RowControl> { toggle, clear };
        var named = PopupPaint.Measure(Gfx, name, form.TextPx, Weight.Semibold).X;
        var room = right - (box.left + form.TextLeft);
        float Taken() => controls.Sum(control => control.Width + gap);
        while (controls.Count > 0 && named > room - Taken())
        {
            controls.RemoveAt(0);
        }
        var header = LineRow.Draw(sketch, box.left, box.right, box.top, style, new RowSpec(name)
        {
            Glyph = BellGlyph,
            GlyphInk = TilePaint.Accent(style),
            Strong = true,
            Controls = controls,
            NamePiece = "head",
        }, one ? height : sizes.HeadLine);
        for (var index = 0; index < controls.Count; index++)
        {
            targets?.Add((new Target(controls[index].Name == toggle.Name ? Spot.Quiet : Spot.Clear), buttons.Hit(header.Controls[index], box.top, header.Bottom)));
        }
        if (!one)
        {
            Grouped(sketch, box, style, sizes, entries, targets, header.Bottom);
        }
    }

    /// <summary>
    /// The least a line lists its notifications at: the header, and under it the first app's heading and its newest
    /// notification's first line; under it, the line of one row.
    /// </summary>
    private static float Lists(in Sizes sizes) => sizes.HeadLine + sizes.Heading + sizes.Air + sizes.Items.Line;

    /// <summary>When <paramref name="item"/> came, in the list's words ("2h").</summary>
    private static string When(NotificationEntry item, DateTime now) =>
        NotificationText.Relative(item.Arrived, now, Panels.Nls.MonthDay, Panels.Nls.ShortDate);

    // --- The whole list, grouped -------------------------------------------------------------------

    /// <summary>A row of the grouped list.</summary>
    private enum Row
    {
        /// <summary>An app's heading: its icon, its name, and under the pointer the × that clears its notifications.</summary>
        Heading,

        /// <summary>A notification: the kit's item.</summary>
        Item,
    }

    /// <summary>One row of the grouped list, laid out from the list's top: its kind, what it is about, how tall.</summary>
    private sealed record Block(Row Kind, NotificationGroup Group, NotificationEntry? Item, float Height, Laid? Laid = null);

    /// <summary>
    /// A notification as laid out, every box from its band's top and the line's left: its title and body and their room,
    /// how tall the leading column's picture stands beside, its own picture, its pictures and its controls under its words,
    /// whether it has the chevron and is expanded.
    /// </summary>
    internal sealed record Laid(float Height, float TitleTop, string Title, float TitleRoom, float ToolMiddle, float TimeTop, float BodyTop, string Body, float Lead, Pixels? Logo,
        List<(D2D_RECT_F Box, Pixels Picture)> Pictures, bool Chevron, bool Expanded,
        List<(PartKind Kind, D2D_RECT_F Box, string Text, string Label, ToastInput? Input, int Part)> Parts);

    /// <summary>The grouped rows of <paramref name="entries"/> at <paramref name="width"/>, in order: each app's heading over every one of its notifications.</summary>
    private List<Block> Blocks(IReadOnlyList<NotificationEntry> entries, in TileStyle style, in Sizes sizes, float width)
    {
        var now = Clock.Now();
        var blocks = new List<Block>();
        foreach (var group in NotificationText.Groups(entries))
        {
            blocks.Add(new Block(Row.Heading, group, null, sizes.Heading));
            foreach (var item in group.Items)
            {
                var laid = ItemOf(item, style, sizes, width, now);
                blocks.Add(new Block(Row.Item, group, item, laid.Height, laid));
            }
        }
        return blocks;
    }

    /// <summary>
    /// A notification on a line <paramref name="width"/> px wide as the kit's item, as mail lays out a message: its title
    /// strong on one line at the text edge, when it came on that line against the trailing gutter, its body in the
    /// secondary ink on two lines under it, cut with an ellipsis (<see cref="TitleLines"/>, <see cref="BodyLines"/>); its
    /// own picture in the leading column; its buttons — Windows' own, all but a box's — the line's standard buttons under
    /// its words from the text edge, side by side, the next row where one does not fit. Expanded (the chevron, where there
    /// is more to it: more words, a box, a picture), all of its words, its pictures, its boxes and selections and every
    /// button. The air over and under it is its band's, so each is as tall as what it shows and no taller.
    /// </summary>
    internal Laid ItemOf(NotificationEntry item, in TileStyle style, in Sizes sizes, float width, DateTime now)
    {
        var form = sizes.Form;
        var items = sizes.Items;
        var buttons = sizes.Button;
        var offer = item.Offer ?? ToastOffer.None;
        var text = form.TextLeft;
        var end = width - form.Inset;
        var room = MathF.Max(1, end - text);
        // The title's line ends short of when it came, and of the chevron and the × that stand there under the pointer,
        // whichever is wider, so the words do not move as the pointer comes. Where that leaves the title less than half
        // the room (a narrow line, a large text) they stand on a row of their own over it, and the title has all of it.
        var tools = (2 * buttons.Height) + style.Px(2);
        var time = items.Time ? PopupPaint.Measure(Gfx, When(item, now), items.SmallPx, Weight.Regular).X : 0;
        var titleRoom = room - MathF.Max(tools, time) - sizes.Gap;
        var over = titleRoom < room / 2;
        titleRoom = over ? room : titleRoom;
        var titleTop = sizes.Air + (over ? buttons.Height : 0);
        // The chevron and the × end on the title's foot, a button being taller than a line: up into the air over it, clear of the body.
        var toolMiddle = sizes.Air + (over ? buttons.Height : items.Line) - (buttons.Height / 2);
        var (title, titleHeight) = PopupPaint.Fit(Gfx, item.Title, items.TextPx, Weight.Semibold, titleRoom, TitleLines);
        var preview = items.Preview && item.Body.Length > 0;
        var (body, bodyHeight) = preview ? PopupPaint.Fit(Gfx, item.Body, items.SmallPx, Weight.Regular, room, BodyLines) : ("", 0f);
        var hero = items.Pictures ? item.HeroPath : null;
        var inline = items.Pictures ? item.InlinePath : null;
        var cut = !string.Equals(title, item.Title, StringComparison.Ordinal) || (preview && !string.Equals(body, item.Body, StringComparison.Ordinal));
        var chevron = cut || offer.Inputs.Count > 0 || offer.Buttons.Any(button => button.InputId is not null) || hero is not null || inline is not null;
        var expanded = chevron && View.Expanded.Contains(item.Id);
        if (expanded)
        {
            (title, titleHeight) = PopupPaint.Fit(Gfx, item.Title, items.TextPx, Weight.Semibold, titleRoom, MostLines);
            if (preview)
            {
                (body, bodyHeight) = PopupPaint.Fit(Gfx, item.Body, items.SmallPx, Weight.Regular, room, MostLines);
            }
        }
        var bodyTop = titleTop + MathF.Max(titleHeight, items.Line);
        var bottom = bodyTop + bodyHeight;
        var logo = items.Pictures && item.ImagePath is { } path ? Image(path, sizes.Logo) : null;
        var pictures = new List<(D2D_RECT_F, Pixels)>();
        if (expanded)
        {
            foreach (var picture in new[] { hero, inline })
            {
                if (picture is not null && Image(picture, MathF.Min(2048, 2 * room)) is { Width: > 0, Height: > 0 } pixels)
                {
                    var fit = MathF.Min(room / pixels.Width, style.Px(180) / pixels.Height);
                    var at = PopupPaint.Rect(text, bottom + sizes.Gap, MathF.Max(1, PopupPaint.Round(pixels.Width * fit)), MathF.Max(1, PopupPaint.Round(pixels.Height * fit)));
                    pictures.Add((at, pixels));
                    bottom = at.bottom;
                }
            }
        }
        var parts = new List<(PartKind, D2D_RECT_F, string, string, ToastInput?, int)>();
        var all = PartsOf(item);
        var y = bottom + sizes.Gap;
        if (expanded)
        {
            for (var index = 0; index < all.Count; index++)
            {
                if (all[index] is not { Kind: not PartKind.Button, Input: { } input })
                {
                    continue;
                }
                if (input.Title.Length > 0)
                {
                    y += form.SmallLine + style.Px(4);
                }
                var field = PopupPaint.Rect(text, y, room, sizes.Field);
                var said = input.Kind == ToastInputKind.Selection
                    ? input.Choices.FirstOrDefault(choice => choice.Id == View.Choice(item.Id, input))?.Content ?? View.Choice(item.Id, input)
                    : "";
                parts.Add((all[index].Kind, field, said, input.Title, input, index));
                y += sizes.Field + sizes.Gap;
            }
        }
        var x = text;
        var first = true;
        for (var index = 0; index < all.Count; index++)
        {
            if (all[index] is not { Kind: PartKind.Button, Button: { } button } || !(expanded || button.InputId is null))
            {
                continue;
            }
            var wide = MathF.Min(buttons.Width(Gfx, button.Content), room);
            if (!first && x + wide > end + 0.5f)
            {
                x = text;
                y += buttons.Height + buttons.Gap;
            }
            parts.Add((PartKind.Button, PopupPaint.Rect(x, y, wide, buttons.Height), button.Content, "", null, index));
            x += wide + buttons.Gap;
            first = false;
        }
        if (parts.Count > 0)
        {
            bottom = parts.Max(part => part.Item2.bottom);
        }
        return new Laid(PopupPaint.Round(bottom + sizes.Air), titleTop, title, titleRoom, toolMiddle,
            over ? toolMiddle - (items.SmallLine / 2) : titleTop + ((items.Line - items.SmallLine) / 2), bodyTop, body, items.Line + (bodyHeight > 0 ? items.SmallLine : 0), logo,
            pictures, chevron, expanded, parts);
    }

    /// <summary>How tall the grouped list is, all of it.</summary>
    private static float Natural(List<Block> blocks) => blocks.Sum(block => block.Height);

    /// <summary>
    /// The whole list under the header, from <paramref name="top"/>: each app's heading — its icon in the leading column, its
    /// name at the text edge, under the pointer its × — over its notifications, the kit's items, each its band of the line
    /// edge to edge and touching the next (§428), every one of a group's, newest first.
    /// The last ends on the line's foot at the height it asks for (§436). Laid out as tall as all of it where it scrolls
    /// (ListScroll.cs); in a box shorter than that what does not fit is left out whole from the end, and a heading is never
    /// left alone at the foot.
    /// </summary>
    private void Grouped(LineSketch sketch, D2D_RECT_F box, in TileStyle style, in Sizes sizes, List<NotificationEntry> entries,
        List<(Target Target, D2D_RECT_F Box)>? targets, float top)
    {
        var blocks = Blocks(entries, style, sizes, TilePaint.Width(box));
        var room = MathF.Max(0, box.bottom - top);
        var kept = blocks.Count;
        if (Natural(blocks) > room + 0.5f)
        {
            var y = 0f;
            kept = 0;
            foreach (var block in blocks)
            {
                if (y + block.Height > room + 0.5f)
                {
                    break;
                }
                y += block.Height;
                kept++;
            }
            while (kept > 0 && blocks[kept - 1].Kind == Row.Heading)
            {
                kept--;
            }
        }
        var now = Clock.Now();
        var at = top;
        var number = 0;
        for (var place = 0; place < kept; place++)
        {
            var block = blocks[place];
            // Out of sight in a scrolled line: not drawn (CellIsland.Hidden). A notification still counts.
            if (!(sketch.Draws && Hidden(at, at + block.Height)))
            {
                switch (block.Kind)
                {
                    case Row.Heading:
                        Heading(sketch, box, style, sizes, block.Group, at, targets);
                        break;
                    default:
                        Item(sketch, box, style, sizes, block.Item!, block.Laid!, $"{number}", at, now, targets);
                        break;
                }
            }
            number += block.Kind == Row.Item ? 1 : 0;
            at += block.Height;
        }
    }

    /// <summary>A subtle glyph button: lit under the pointer, pressed while a press is down on it.</summary>
    private void Tool(LineSketch sketch, in TileStyle style, string name, string mark, D2D_RECT_F square, Target target) =>
        sketch.Button(name, LineButton.Of(Gfx, style), style.Theme, square, "", mark, ButtonStyle.Subtle, hot == target, press.On(hotId));

    /// <summary>
    /// An app's heading at <paramref name="top"/>, as the agenda's day (§412): the kit's compact row, a gap lower than its
    /// middle so it stands with its own notifications; the app's icon in the leading column, its name strong in the
    /// secondary ink at the text edge; under the pointer the × that clears its notifications on the trailing gutter. No
    /// item: it lights nothing.
    /// </summary>
    private void Heading(LineSketch sketch, D2D_RECT_F box, in TileStyle style, in Sizes sizes, NotificationGroup group, float top,
        List<(Target Target, D2D_RECT_F Box)>? targets)
    {
        var form = sizes.Form;
        var theme = style.Theme;
        // Where the pointer shows its ×: between the gutters, so the line's edge down the list is its notifications' alone.
        var row = PopupPaint.Rect(box.left + form.Inset, top, TilePaint.Width(box) - (2 * form.Inset), sizes.Heading);
        var rowTop = top + sizes.Gap;
        var middle = rowTop + PopupPaint.Round((sizes.Heading - sizes.Gap) / 2);
        var icon = form.IconBox(box.left, rowTop, sizes.Heading - sizes.Gap);
        if (group.AppIcon is { } pixels)
        {
            // A white glyph of Windows' own in the heading's colour (NotificationApps).
            var drawn = NotificationApps.Drawn(pixels, theme.Text);
            sketch.Picture($"app {group.AppId}", icon, (dc, rect) => PopupPaint.Picture(dc, drawn, rect, 0));
        }
        else
        {
            sketch.Glyph($"app {group.AppId}", BellGlyph, form.Glyph, theme.TextSecondary, icon);
        }
        var side = sizes.Button.Height;
        var cross = sizes.Button.Square(box.right - form.Inset - side, middle);
        var close = new Target(Spot.GroupClose, group.AppId);
        var lit = hot.AppId == group.AppId && hot.Spot is Spot.Heading or Spot.GroupClose;
        var text = box.left + form.TextLeft;
        var line = TilePaint.LineHeight(Gfx, form.TextPx, Weight.Semibold);
        sketch.Text($"heading {group.AppId}", group.AppName, form.TextPx, Weight.Semibold, theme.TextSecondary, text, PopupPaint.Round(middle - (line / 2)),
            MathF.Max(1, (lit ? cross.left - sizes.Gap : box.right - form.Inset) - text));
        targets?.Add((close, cross));
        // The heading itself is no button; it is written down so the pointer on it shows its ×.
        targets?.Add((new Target(Spot.Heading, group.AppId), row));
        if (lit)
        {
            Tool(sketch, style, $"heading close {group.AppId}", DismissGlyph, cross, close);
        }
    }

    /// <summary>
    /// A notification at <paramref name="top"/>, laid out as <paramref name="laid"/> (<see cref="ItemOf"/>): its band lit under
    /// the pointer and pressed, opening its app; its own picture in the leading column; when it came on its title's line
    /// against the trailing gutter, and under the pointer in its place the chevron and the ×, each its own target over the
    /// band; its title and body at the text edge; its pictures, boxes and buttons under them.
    /// </summary>
    private void Item(LineSketch sketch, D2D_RECT_F box, in TileStyle style, in Sizes sizes, NotificationEntry item, Laid laid, string name, float top,
        DateTime now, List<(Target Target, D2D_RECT_F Box)>? targets)
    {
        var form = sizes.Form;
        var items = sizes.Items;
        var theme = style.Theme;
        var buttons = sizes.Button;
        var hotNow = hot;
        var pressed = press.On(hotId);
        var band = LineItem.Band(box, top, top + laid.Height);
        var body = new Target(Spot.Body, item.AppId, item.Id);
        targets?.Add((body, band));
        // Lit while the pointer is anywhere on it, its controls too, as a WinUI list item is; pressed only for a press on it.
        var mine = hotNow.Item == item.Id && hotNow.Spot != Spot.None;
        if (mine)
        {
            LineItem.Light(sketch, band, theme, pressed && hotNow == body);
        }
        D2D_RECT_F Moved(D2D_RECT_F rect) => new(box.left + rect.left, top + rect.top, box.left + rect.right, top + rect.bottom);
        var text = box.left + form.TextLeft;
        var end = box.right - form.Inset;
        var titleTop = top + laid.TitleTop;
        if (laid.Logo is { } logo)
        {
            // As the feed's pictures: the kit's Feature size grown into the room before the text edge, on its words' middle.
            var side = sizes.Logo;
            var lead = ListTiers.GlyphBox(form, box.left, titleTop, laid.Lead, side);
            var square = PopupPaint.Rect(PopupPaint.Round(((lead.left + lead.right) / 2) - (side / 2)), PopupPaint.Round(titleTop + ((laid.Lead - side) / 2)), side, side);
            var corner = style.Px(LineCorner.ControlDip);
            sketch.Picture($"picture {name}", square, (dc, rect) => PopupPaint.Covered(dc, logo, rect, corner));
        }
        // The title's line's end — or the row over it where the title needs the line's whole room (ItemOf) — the × on the
        // trailing gutter, the chevron before it, never out of the band.
        var middle = MathF.Max(top + laid.ToolMiddle, band.top + (buttons.Height / 2));
        var cross = buttons.Square(end - buttons.Height, middle);
        var chevron = buttons.Square(cross.left - style.Px(2) - buttons.Height, middle);
        var close = new Target(Spot.Close, item.AppId, item.Id);
        var fold = new Target(Spot.Chevron, item.AppId, item.Id);
        targets?.Add((close, cross));
        if (laid.Chevron)
        {
            targets?.Add((fold, chevron));
        }
        if (mine)
        {
            if (laid.Chevron)
            {
                Tool(sketch, style, $"chevron {name}", laid.Expanded ? FoldGlyph : ExpandGlyph, chevron, fold);
            }
            Tool(sketch, style, $"close {name}", DismissGlyph, cross, close);
        }
        else if (items.Time)
        {
            sketch.TextRight($"time {name}", When(item, now), items.SmallPx, Weight.Regular, theme.TextSecondary, end,
                PopupPaint.Round(top + laid.TimeTop), end - text);
        }
        sketch.Wrapped($"title {name}", laid.Title, items.TextPx, Weight.Semibold, theme.Text, text, titleTop, laid.TitleRoom, laid.Expanded ? MostLines : TitleLines);
        if (laid.Body.Length > 0)
        {
            sketch.Wrapped($"body {name}", laid.Body, items.SmallPx, Weight.Regular, theme.TextSecondary, text, top + laid.BodyTop, end - text,
                laid.Expanded ? MostLines : BodyLines);
        }
        for (var index = 0; index < laid.Pictures.Count; index++)
        {
            var (at, picture) = laid.Pictures[index];
            var corner = style.Px(LineCorner.ControlDip);
            sketch.Picture($"inline {name} {index}", Moved(at), (dc, rect) => Fitted(dc, picture, rect, corner));
        }
        var editorTheme = sketch.Draws ? QuickTheme.For(theme) : null;
        var look = style;
        foreach (var (kind, partBox, said, label, input, part) in laid.Parts)
        {
            var rect = Moved(partBox);
            var target = new Target(Spot.Part, item.AppId, item.Id, part);
            targets?.Add((target, rect));
            var lit = hotNow == target;
            if (kind == PartKind.Button)
            {
                sketch.Button($"part {name} {part}", buttons, theme, rect, said, null, ButtonStyle.Standard, lit, lit && pressed);
                continue;
            }
            if (label.Length > 0)
            {
                sketch.Text($"label {name} {part}", label, form.SmallPx, Weight.Regular, theme.TextSecondary, rect.left,
                    rect.top - form.SmallLine - style.Px(4), rect.right - rect.left);
            }
            var focused = kind == PartKind.Box && input is not null && View.Focus == (item.Id, input.Id);
            var editor = kind == PartKind.Box && input is not null ? View.Peek(item.Id, input) : null;
            sketch.Picture($"part {name} {part}", rect, (dc, drawn) =>
            {
                if (focused && editor is not null)
                {
                    editor.Paint(dc, Gfx, drawn, form.TextPx, editorTheme!, look);
                    return;
                }
                PaintPart(dc, kind, drawn, kind == PartKind.Box ? (editor?.Text is { Length: > 0 } typed ? typed : input?.Placeholder ?? "") : said,
                    kind == PartKind.Box && editor?.Text is not { Length: > 0 }, lit, look, form);
            });
        }
    }

    /// <summary><paramref name="pixels"/> filling <paramref name="box"/>, which has its shape, its corners rounded: a hero or an inline picture.</summary>
    private static void Fitted(ID2D1DeviceContext* dc, Pixels pixels, D2D_RECT_F box, float radius)
    {
        var width = box.right - box.left;
        var height = box.bottom - box.top;
        var side = MathF.Max(width, height);
        var device = dc;
        PopupPaint.Clipped(dc, box, radius, () => Gfx.DrawPixels(device, pixels, box.left - ((side - width) / 2), box.top - ((side - height) / 2), side));
    }

    /// <summary>
    /// An expanded notification's box or selection at rest, in WinUI's shapes: a text box with its text or its quieter
    /// placeholder, a selection showing its choice with a chevron at its end. A box with the keys is the editor's own
    /// (InlineEditor.Paint).
    /// </summary>
    private void PaintPart(ID2D1DeviceContext* dc, PartKind kind, D2D_RECT_F box, string text, bool placeholder, bool lit, in TileStyle style, in ListForm form)
    {
        var theme = style.Theme;
        var radius = style.Px(4);
        PopupPaint.Fill(dc, box, radius, PopupPaint.Ink(lit && kind != PartKind.Box ? theme.ControlFillHover : theme.ControlFill));
        PopupPaint.Stroke(dc, box, radius, MathF.Max(1, style.Px(1)), PopupPaint.Ink(theme.ControlStroke));
        var pad = style.Px(11);
        if (kind == PartKind.Choice)
        {
            var side = box.bottom - box.top;
            var chevron = PopupPaint.Rect(box.right - side, box.top, side, side);
            PopupPaint.Glyph(dc, Gfx, ExpandGlyph, LineGlyph.Inline(style), theme.TextSecondary, chevron);
            var room = MathF.Max(1, chevron.left - box.left - pad);
            PopupPaint.Text(dc, Gfx, text, form.TextPx, Weight.Regular, theme.Text, box.left + pad,
                box.top + PopupPaint.Round((box.bottom - box.top - form.Line) / 2), room);
            return;
        }
        var inner = new D2D_RECT_F(box.left + style.Px(10), box.top, MathF.Max(box.left + style.Px(11), box.right - style.Px(10)), box.bottom);
        var px = form.TextPx;
        var top = box.top + PopupPaint.Round((box.bottom - box.top - form.Line) / 2);
        var ink = placeholder ? theme.TextSecondary : theme.Text;
        var device = dc;
        PopupPaint.Clipped(dc, inner, 0, () => PopupPaint.Text(device, Gfx, text, px, Weight.Regular, ink, inner.left, top));
    }

    /// <summary>
    /// list: the line this widget needs to show every notification it has — the header over the grouped list, all of it,
    /// its last notification's band on its foot — or one row of the Status with none (<see cref="IListContent"/>).
    /// </summary>
    public float ListHeight(float width, in TileStyle style)
    {
        IReadOnlyList<NotificationEntry> entries = Shown is not null || store.Available ? Entries : [];
        if (entries.Count == 0)
        {
            return LineRows.Px(1, style);
        }
        var sizes = SizesOf(style);
        return sizes.HeadLine + Natural(Blocks(NotificationText.Newest(entries), style, sizes, width));
    }

    /// <summary>All of it, where the line lists its notifications: they scroll under the header (ListScroll.cs).</summary>
    public float ScrollHeight(float width, float height, in TileStyle style)
    {
        var count = Shown is not null || store.Available ? Entries.Count : 0;
        return count == 0 || height + 0.5f < Lists(SizesOf(style)) ? 0 : ListHeight(width, style);
    }

    /// <summary>The header — the bell, "Notifications", the toggle and "Clear all" — stays while the notifications scroll.</summary>
    public float ScrollHead(float width, float content, in TileStyle style) => SizesOf(style).HeadLine;

    /// <summary>A toast's own picture at most <paramref name="px"/> px across, read once while the line stands (and kept for it).</summary>
    private Pixels? Image(string path, float px)
    {
        var size = Math.Max(1, (int)MathF.Ceiling(px));
        var key = $"{size}|{path}";
        if (!images.TryGetValue(key, out var pixels))
        {
            if (images.Count >= 64)
            {
                images.Clear();
            }
            pixels = LoadImage(path, size);
            images[key] = pixels;
        }
        return pixels;
    }

    /// <summary>A picture file fitted into a <paramref name="size"/> px square, as premultiplied pixels; null when WIC can't read it.</summary>
    internal static Pixels? LoadImage(string path, int size)
    {
        var clsid = CLSID.CLSID_WICImagingFactory2;
        IWICImagingFactory* wic = null;
        IWICBitmapDecoder* decoder = null;
        IWICBitmapFrameDecode* frame = null;
        IWICBitmapScaler* scaler = null;
        IWICFormatConverter* converter = null;
        try
        {
            if (CoCreateInstance(&clsid, null, (uint)CLSCTX.CLSCTX_INPROC_SERVER, __uuidof<IWICImagingFactory>(), (void**)&wic).FAILED)
            {
                return null;
            }
            fixed (char* name = path)
            {
                if (wic->CreateDecoderFromFilename(name, null, GENERIC_READ, WICDecodeOptions.WICDecodeMetadataCacheOnDemand, &decoder).FAILED)
                {
                    return null;
                }
            }
            uint width;
            uint height;
            if (decoder->GetFrame(0, &frame).FAILED || frame->GetSize(&width, &height).FAILED || width == 0 || height == 0)
            {
                return null;
            }
            var fit = MathF.Min(1, size / (float)Math.Max(width, height));
            var scaledWidth = (uint)Math.Max(1, MathF.Round(width * fit));
            var scaledHeight = (uint)Math.Max(1, MathF.Round(height * fit));
            if (wic->CreateBitmapScaler(&scaler).FAILED
                || scaler->Initialize((IWICBitmapSource*)frame, scaledWidth, scaledHeight, WICBitmapInterpolationMode.WICBitmapInterpolationModeFant).FAILED
                || wic->CreateFormatConverter(&converter).FAILED)
            {
                return null;
            }
            var format = GUID.GUID_WICPixelFormat32bppPBGRA;
            if (converter->Initialize((IWICBitmapSource*)scaler, &format, WICBitmapDitherType.WICBitmapDitherTypeNone, null, 0, WICBitmapPaletteType.WICBitmapPaletteTypeCustom).FAILED)
            {
                return null;
            }
            var data = new byte[scaledWidth * scaledHeight * 4];
            var rect = new WICRect { X = 0, Y = 0, Width = (int)scaledWidth, Height = (int)scaledHeight };
            fixed (byte* pixels = data)
            {
                if (converter->CopyPixels(&rect, scaledWidth * 4, (uint)data.Length, pixels).FAILED)
                {
                    return null;
                }
            }
            return new Pixels { Data = data, Width = (int)scaledWidth, Height = (int)scaledHeight };
        }
        finally
        {
            ComUtil.Release(ref converter);
            ComUtil.Release(ref scaler);
            ComUtil.Release(ref frame);
            ComUtil.Release(ref decoder);
            ComUtil.Release(ref wic);
        }
    }

    /// <summary>What of the line is under the pointer.</summary>
    internal enum Spot
    {
        None,

        /// <summary>The header's "Clear all".</summary>
        Clear,

        /// <summary>The header's do not disturb switch.</summary>
        Quiet,

        /// <summary>An app's heading, which only shows its ×.</summary>
        Heading,

        /// <summary>An app's heading's ×, which clears the app's notifications.</summary>
        GroupClose,

        /// <summary>A notification, which activates its app.</summary>
        Body,

        /// <summary>A notification's ×.</summary>
        Close,

        /// <summary>A notification's chevron, which expands it and folds it again.</summary>
        Chevron,

        /// <summary>One of an expanded notification's controls.</summary>
        Part,
    }

    /// <summary>A thing a click lands on, by what it is rather than where: an app's, a notification's, a control of one.</summary>
    internal readonly record struct Target(Spot Spot, string AppId = "", long Item = 0, int Part = -1);
}

internal enum PartKind
{
    /// <summary>A toast's button.</summary>
    Button,

    /// <summary>A toast's text box: a reply.</summary>
    Box,

    /// <summary>A toast's selection, whose choices a click steps through.</summary>
    Choice,
}

/// <summary>
/// What the line shows of its list beyond what the store holds, kept while the folder stands and gone
/// when it closes, as Windows' own forgets it: the notifications expanded, what has been typed in their boxes and chosen in their selections,
/// and which box has the keys.
/// </summary>
internal sealed class NotificationView
{
    public HashSet<long> Expanded { get; } = [];

    public Dictionary<(long Id, string Input), InlineEditor> Boxes { get; } = [];

    public Dictionary<(long Id, string Input), string> Chosen { get; } = [];

    /// <summary>The box the keys go to: a notification's and its input's id; null for none.</summary>
    public (long Id, string Input)? Focus { get; set; }

    /// <summary>A box's text and caret, made with its default text the first time it is asked for.</summary>
    public InlineEditor Box(long id, ToastInput input)
    {
        if (!Boxes.TryGetValue((id, input.Id), out var editor))
        {
            editor = new InlineEditor(multiLine: false, maxLength: 4096) { Placeholder = input.Placeholder };
            editor.Begin(input.Default, selectAll: false);
            Boxes[(id, input.Id)] = editor;
        }
        return editor;
    }

    /// <summary>A box's editor where one has been made, without making one.</summary>
    public InlineEditor? Peek(long id, ToastInput input) => Boxes.GetValueOrDefault((id, input.Id));

    /// <summary>The choice a selection shows: the one chosen, else its default.</summary>
    public string Choice(long id, ToastInput input) =>
        Chosen.TryGetValue((id, input.Id), out var chosen) ? chosen : input.Default;

    /// <summary>Every input's value for <paramref name="entry"/>, as an activation hands them to the app.</summary>
    public Dictionary<string, string> Values(NotificationEntry entry)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var input in (entry.Offer ?? ToastOffer.None).Inputs)
        {
            values[input.Id] = input.Kind == ToastInputKind.Selection ? Choice(entry.Id, input)
                : Peek(entry.Id, input)?.Text ?? input.Default;
        }
        return values;
    }

    /// <summary>The showing ended: everything expanded, typed and chosen is forgotten.</summary>
    public void Clear()
    {
        Expanded.Clear();
        Boxes.Clear();
        Chosen.Clear();
        Focus = null;
    }
}
