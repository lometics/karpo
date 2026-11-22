// desktops: the virtual desktops as a line of a folder's list (docs\widgets.md §116). It was the
// desktop switcher's flyout until the switcher became the desktops preset (2026-10-11: "the desktop
// switcher should be made into a folder with face and widgets inside"): a folder wearing the name of
// the desktop you are on, holding this line, which does what the flyout did.
//
// **A row per desktop**, in Windows' order: its gripper in the list's leading column, its name — the
// one you are on marked as WinUI marks a list's chosen item, the subtle fill, the accent pill at its
// start and its name semibold — and the apps open on it at its end, then "+N" for the rest (the
// line's own setting, DesktopOptions.ShowApps). Under the pointer or the keys a row lights and shows
// its pencil and its cross where its apps stood; what a row cannot do is dimmed and says why.
// "New desktop" is the foot.
//
// **Reordering is a drag** (2026-10-11: "reordering desktops should be with drag and drop, not with
// the up and down buttons, there should be proper drag handles"). The gripper — six dots, two by
// three, as Windows' own reorderable lists draw it — is the handle: the pointer over it is the move
// cursor, and a press past the drag threshold lifts the row, which follows the pointer on a raised
// plate while the others slide out of its way (each row eased to its place, Ms); let go, the desktop
// is moved there through the shell's own call (IDesktops.Move) and the row settles into its slot.
// Escape puts it back where it was. From the keys, Alt+Up and Alt+Down move the row they are on a
// place, as Windows' Preferred languages list does. Until Windows has written the new order down, the
// line shows the one it asked for (pending), so nothing jumps back for a moment.
//
// **A click switches.** A row's click (or Enter on it) is the folder's act (CellAct.Action,
// IslandView.Desktops): the folder closes without handing the focus back — the window that had it is
// on the desktop being left, and activated as the switch goes it would switch Windows straight back —
// and the dock switches (TakeSwitch). The pencil (or F2) renames in place in a text box on the row,
// Enter or a press elsewhere keeping the name and Escape not; the cross (or Delete) closes the
// desktop, its windows going to its neighbour; "New desktop" makes one at the end without leaving
// this one, where the shell's call can.
//
// **What it reads.** The dock's desktops (DesktopState.Service), or a test's (Stand); the line follows
// their news while it shows (IDesktops.Changed). Its height follows how many there are (IListContent);
// held shorter, they scroll (ListScroll.cs).

using System.Numerics;
using Karpo.Core;
using Karpo.Dock.QuickSettings.Controls;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

internal sealed unsafe class DesktopsLine : CellIsland, IListContent, IListScroll, ILinePieces
{
    /// <summary>Segoe Fluent Icons: Edit, Cancel and Add.</summary>
    internal const string RenameGlyph = "";
    internal const string DeleteGlyph = "";
    internal const string AddGlyph = "";

    /// <summary>§489: how long a row takes to slide into its place, in ms: a move's (Motion.cs), as the look it was last laid out in has it; 0 with motion off.</summary>
    internal int Ms => (int)motion.Ms(MotionKind.Move);

    // §489: the motion of the look the line was last laid out in (Lay).
    private Motion motion = Motion.Normal;

    /// <summary>How long the line shows an order it asked for before believing Windows' again, in ms.</summary>
    internal const int PendingMs = 2000;

    /// <summary>The apps' icons, in DIPs: the 16 the rows' glyphs use.</summary>
    private const float IconDip = 16;

    [ThreadStatic]
    private static IDesktops? stand;

    /// <summary>tests: the desktops a line made on this thread reads; null for the dock's (<see cref="DesktopState.Service"/>).</summary>
    internal static IDesktops? Stand
    {
        get => stand;
        set => stand = value;
    }

    [ThreadStatic]
    private static Func<Item, int, Pixels?>? icons;

    /// <summary>tests: an app's icon at a size in px, in the shell's place.</summary>
    internal static Func<Item, int, Pixels?>? Icons
    {
        get => icons;
        set => icons = value;
    }

    private readonly IDesktops? desktops;
    private readonly Func<Item, int, Pixels?> iconOf;
    private readonly InlineEditor editor = new(multiLine: false, maxLength: 100);
    private bool listening;

    // An app's icon is the shell's answer and costs about a millisecond: asked once per app and size
    // while the folder shows.
    private readonly Dictionary<string, Pixels?> iconCache = new(StringComparer.OrdinalIgnoreCase);
    private int iconSide;

    // What is under the pointer and the number the folder was given for it; the ids given out.
    private Target hot;
    private int hotId;
    private readonly List<Target> given = [];
    private RowPress press;

    // The row the keys are on (a desktop's key, or AddKey); null for none.
    private string? keyed;

    // The desktop a click chose, for the dock to switch to once the folder has gone.
    private int? switchTo;

    // The desktop being renamed, by its key; null while none is.
    private string? renaming;
    private bool selecting;

    // A press on a gripper: the desktop, where in its row it was taken, where the press went down;
    // whether it has lifted (past the drag threshold), where its top is now (line px), and whether
    // Escape put it back while the button is still down.
    private string? grabbed;
    private float grabAt;
    private float pressedY;
    private bool lifted;
    private float liftTop;
    private bool cancelled;

    // Each row's slide: from where to where (px off its slot) since when.
    private readonly Dictionary<string, Tween> tweens = new(StringComparer.Ordinal);
    private bool animating;

    // What was last read of the desktops, and whether the shell's calls can be made: read once and
    // kept until the desktops say they changed, the line acts on them, or the folder opens again —
    // the line is laid out many times a frame, and asking the shell costs a call into Explorer.
    private DesktopStrip? read;
    private bool? shell;

    // An order asked of Windows and not yet read back, and until when it is believed.
    private List<string>? pending;
    private long pendingUntil;

    // The last plan drawn: what the keys and the release read.
    private Plan? shown;
    private D2D_RECT_F shownBox;
    private TileStyle shownStyle;

    public DesktopsLine(string key, WidgetItem island, Gfx gfx, Config config)
        : this(key, island, gfx, config, Stand ?? DesktopState.Service)
    {
    }

    /// <summary>A line on <paramref name="desktops"/>: the dock's, or a test's; none reads as no desktops.</summary>
    internal DesktopsLine(string key, WidgetItem island, Gfx gfx, Config config, IDesktops? desktops)
        : base(key, island, gfx)
    {
        this.desktops = desktops;
        iconOf = Icons ?? (static (item, size) => item.Icon(size));
        Configure(island, config);
    }

    /// <summary>tests: the clock the slides are timed by, in ms.</summary>
    internal Func<long> Ticks { get; set; } = static () => Environment.TickCount64;

    /// <summary>The caret blinks while a name is typed; nothing ticks otherwise.</summary>
    public override int IntervalMs => renaming is not null ? 500 : 0;

    protected override int Count => 1;

    public override void Configure(WidgetItem island, Config config)
    {
        Settings = island;
        Forget();
    }

    /// <summary>Whether the rows show the apps open on each desktop (the line's own setting).</summary>
    private bool ShowApps => Settings.Desktops?.ShowApps != false;

    public override string? Tooltip(int index) => null;

    internal override void PaintCell(ID2D1DeviceContext* dc, int index, in IslandStyle style, float width, float height) =>
        throw new NotSupportedException($"{Info.Id} is a line of a folder's list; on the bar it is the desktops preset's face.");

    protected override bool Sample(bool everything)
    {
        if (!everything && renaming is not null)
        {
            editor.Blink();
        }
        return false;
    }

    /// <summary>Rows light themselves; the line does not light as a whole.</summary>
    protected internal override bool LightsItsRows => true;

    /// <summary>
    /// Whether a row is sliding, or the last frame of a slide has still to be drawn: the folder draws
    /// the line on its quick tick meanwhile (FolderFlyout.ArmQuick).
    /// </summary>
    internal override bool Animating => animating;

    /// <summary>A desktop held by its gripper: the pointer is the move cursor wherever it goes.</summary>
    internal override bool Gripping => grabbed is not null && !cancelled;

    public override void Visible(bool shown)
    {
        base.Visible(shown);
        if (shown)
        {
            Reread();
            if (!listening && desktops is not null)
            {
                listening = true;
                desktops.Changed += Heard;
            }
            return;
        }
        if (listening && desktops is not null)
        {
            listening = false;
            desktops.Changed -= Heard;
        }
        CardStopTyping();
        hot = default;
        hotId = 0;
        keyed = null;
        _ = press.Up();
        grabbed = null;
        lifted = cancelled = selecting = false;
        tweens.Clear();
        animating = false;
        iconCache.Clear();
    }

    public override void Dispose()
    {
        if (listening && desktops is not null)
        {
            listening = false;
            desktops.Changed -= Heard;
        }
        base.Dispose();
    }

    /// <summary>The desktops changed (switched, renamed, made, moved, closed, or their apps): laid out again.</summary>
    private void Heard()
    {
        Reread();
        Redrawn();
    }

    /// <summary>What was read is read again the next time it is asked for.</summary>
    private void Reread()
    {
        read = null;
        shell = null;
    }

    /// <summary>The desktops as last read.</summary>
    private DesktopStrip Read() => read ??= desktops?.Strip() ?? new DesktopStrip([], 0);

    /// <summary>Whether the shell's own calls can be made, as last asked.</summary>
    private bool Shell => shell ??= desktops?.Shell ?? false;

    // --- What it shows ------------------------------------------------------------------------------

    /// <summary>A desktop as the line shows it: its row's words and apps, and its place in Windows' order.</summary>
    internal readonly record struct Desk(SwitchRow Row, int Index)
    {
        public string Key => Row.Key;
    }

    /// <summary>The key "New desktop" is kept by among the rows.</summary>
    internal const string AddKey = "+";

    /// <summary>
    /// The desktops in the order the line shows them: Windows', or the one it asked for and has not
    /// read back yet (<see cref="PendingMs"/>); keyed by their ids.
    /// </summary>
    internal IReadOnlyList<Desk> Desks()
    {
        var strip = Read();
        var rows = Switchers.Desktops(strip, ShowApps, Info.Glyph);
        var desks = rows.Select((row, index) => new Desk(row with { Key = row.Key is { Length: > 0 } key ? key : $"#{index}" }, index)).ToList();
        if (pending is { } asked)
        {
            var keys = desks.Select(desk => desk.Key).ToList();
            if (keys.SequenceEqual(asked) || Ticks() > pendingUntil || keys.Count != asked.Count || !keys.All(asked.Contains))
            {
                pending = null;
            }
            else
            {
                desks = [.. asked.Select(key => desks.First(desk => desk.Key == key))];
            }
        }
        return desks;
    }

    /// <summary>What a desktop's row may have done to it now.</summary>
    private Switchers.DesktopActions ActionsOf(Desk desk, int count) =>
        Switchers.ActionsFor(desk.Index, Read().Current, count, Shell);

    /// <summary>One row of the line, at its slot (line px from the box's top).</summary>
    internal readonly record struct Row(Desk? Desk, float Top);

    /// <summary>The rows, their height, and how tall all of them are.</summary>
    internal sealed record Plan(IReadOnlyList<Row> Rows, float RowHeight, float Natural, IReadOnlyList<Desk> Desks, bool CanMove);

    /// <summary>A row: 32, 40 or 48 DIP at the item style's size, as every list's row.</summary>
    private float RowHeight(in TileStyle style) =>
        PopupPaint.Round(ItemStyles.Of(Settings).Size switch { ItemSize.Compact => 32, ItemSize.Large => 48, _ => 40 } * style.Scale * style.TextScale);

    /// <summary>The rows from the line's top, "New desktop" its foot: no air over the first or under the last, whose bands are the line's edges (LineItem).</summary>
    internal Plan Lay(in TileStyle style)
    {
        motion = style.Look.Motion;
        var height = RowHeight(style);
        var desks = Desks();
        var rows = new List<Row>(desks.Count + 1);
        var y = 0f;
        foreach (var desk in desks)
        {
            rows.Add(new Row(desk, y));
            y += height;
        }
        rows.Add(new Row(null, y));
        y += height;
        var canMove = Shell && desks.Count > 1;
        return new Plan(rows, height, y, desks, canMove);
    }

    public float ListHeight(float width, in TileStyle style) => Lay(style).Natural;

    /// <summary>All of them, where the line has room for a row: the rest scrolls (ListScroll.cs).</summary>
    public float ScrollHeight(float width, float height, in TileStyle style) =>
        height + 0.5f < RowHeight(style) ? 0 : Lay(style).Natural;

    // --- Where things are ---------------------------------------------------------------------------

    /// <summary>What a press lands on.</summary>
    internal enum Spot
    {
        None,
        Row,
        Grip,
        Rename,
        Delete,
        Add,
    }

    /// <summary>A thing a click acts on: what it is, and for a desktop's row and its parts the desktop, by its key — never its place.</summary>
    internal readonly record struct Target(Spot Spot, string Key = "");

    /// <summary>The sizes a row is laid out with: its buttons the lines' one (LineButton).</summary>
    private readonly record struct Sizes(ListForm Form, ItemForm Items, LineButton Buttons, float Icon, float IconGap)
    {
        public float Button => Buttons.Height;
    }

    private Sizes SizesOf(in TileStyle style) =>
        new(ListForm.Of(Gfx, style), ItemForm.For(Gfx, style, Settings), LineButton.Of(Gfx, style), style.Px(IconDip), style.Px(4));

    private static D2D_RECT_F Moved(D2D_RECT_F box, float top, float height) =>
        new(box.left, box.top + top, box.right, box.top + top + height);

    /// <summary>
    /// A row lifted off the list, and the keys' ring: its row a little in from the line's edges. What lights under the
    /// pointer, and the chosen one's fill, is the row itself, edge to edge (LineItem).
    /// </summary>
    private static D2D_RECT_F PlateOf(D2D_RECT_F row, in TileStyle style) =>
        new(row.left + style.Px(4), row.top + style.Px(2), row.right - style.Px(4), row.bottom - style.Px(2));

    /// <summary>A row's gripper, the list's leading column.</summary>
    private static D2D_RECT_F GripOf(D2D_RECT_F row, in Sizes sizes) =>
        sizes.Form.IconBox(row.left, row.top, row.bottom - row.top);

    /// <summary>A row's cross, at its end, and its pencil before it.</summary>
    private static (D2D_RECT_F Rename, D2D_RECT_F Delete) ButtonsOf(D2D_RECT_F row, in Sizes sizes, in TileStyle style)
    {
        var right = row.right - sizes.Form.Inset;
        var top = row.top + PopupPaint.Round((row.bottom - row.top - sizes.Button) / 2);
        var delete = new D2D_RECT_F(right - sizes.Button, top, right, top + sizes.Button);
        var rename = PopupPaint.Rect(delete.left - sizes.Buttons.Gap - sizes.Button, top, sizes.Button, sizes.Button);
        return (rename, delete);
    }

    /// <summary>
    /// The room a row keeps at its end: its apps; lit, its two buttons where they are wider. Kept for
    /// the buttons on every row, at the largest text they would cut a name to its first letters.
    /// </summary>
    private float EndRoom(Desk desk, in Sizes sizes, in TileStyle style, bool lit) =>
        lit ? MathF.Max((2 * sizes.Button) + style.Px(2), AppsWidth(desk, sizes)) : AppsWidth(desk, sizes);

    private float AppsWidth(Desk desk, in Sizes sizes)
    {
        var count = desk.Row.Apps.Count;
        var width = count > 0 ? (count * sizes.Icon) + ((count - 1) * sizes.IconGap) : 0;
        if (desk.Row.Plus is { Length: > 0 } plus)
        {
            width += (count > 0 ? sizes.IconGap : 0) + PopupPaint.Measure(Gfx, plus, sizes.Form.SmallPx, Weight.Regular).X;
        }
        return width;
    }

    /// <summary>Where a desktop's name is typed while it is renamed: from where the words begin to its end room.</summary>
    private static D2D_RECT_F EditorOf(D2D_RECT_F row, in Sizes sizes, in TileStyle style)
    {
        var height = PopupPaint.Round(style.Text(32));
        var top = row.top + PopupPaint.Round((row.bottom - row.top - height) / 2);
        return new D2D_RECT_F(row.left + sizes.Form.TextLeft - style.Px(10), top, row.right - sizes.Form.Inset, top + height);
    }

    /// <summary>Every target of the plan in <paramref name="box"/> and where it is: the parts before the row they stand on.</summary>
    private List<(Target Target, D2D_RECT_F Box)> Targets(Plan plan, D2D_RECT_F box, in TileStyle style)
    {
        var sizes = SizesOf(style);
        var targets = new List<(Target, D2D_RECT_F)>();
        foreach (var row in plan.Rows)
        {
            var at = Moved(box, row.Top, plan.RowHeight);
            if (row.Desk is not { } desk)
            {
                targets.Add((new Target(Spot.Add), at));
                continue;
            }
            if (renaming != desk.Key)
            {
                var (rename, delete) = ButtonsOf(at, sizes, style);
                targets.Add((new Target(Spot.Delete, desk.Key), delete));
                targets.Add((new Target(Spot.Rename, desk.Key), rename));
            }
            var grip = GripOf(at, sizes);
            targets.Add((new Target(Spot.Grip, desk.Key), new D2D_RECT_F(at.left, at.top, grip.right + style.Px(4), at.bottom)));
            targets.Add((new Target(Spot.Row, desk.Key), at));
        }
        return targets;
    }

    /// <summary>What is at <paramref name="point"/> on the line.</summary>
    internal Target LineTarget(Vector2 point, D2D_RECT_F box, in TileStyle style)
    {
        foreach (var (target, at) in Targets(Lay(style), box, style))
        {
            if (LineRows.Inside(at, point))
            {
                return target;
            }
        }
        return default;
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

    /// <summary>keys: the desktop's row (or New desktop) its keys are on, by its target.</summary>
    protected internal override int? OwnKeyedTarget => keyed switch
    {
        null => 0,
        AddKey => IdOf(new Target(Spot.Add)),
        var key => IdOf(new Target(Spot.Row, key)),
    };

    /// <summary>keys: the row or "New desktop" its keys are on is ringed, but while a name is typed (one ring, §471).</summary>
    protected internal override bool RingsOwnKeys => keyed is not null && renaming is null;

    /// <summary>keys: gone to another line, the row they were on is ringed no more.</summary>
    protected internal override void OwnKeys(bool on)
    {
        if (!on && keyed is not null && renaming is null)
        {
            keyed = null;
            Redrawn();
        }
    }

    protected internal override int CardTarget(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface) =>
        IdOf(LineTarget(point, box, style));

    // --- Clicks -------------------------------------------------------------------------------------

    /// <summary>
    /// What the click the hit test found does; with no part (the keys: Enter or Space) the row the
    /// keys are on. A desktop's row switches to it once the folder has gone; its pencil renames it in
    /// place, its cross closes it, and "New desktop" makes one, the folder staying.
    /// </summary>
    public override IslandAction Click(int index)
    {
        _ = press.Up();
        Target target;
        if (index == 0)
        {
            target = keyed switch
            {
                null => default,
                AddKey => new Target(Spot.Add),
                var key => new Target(Spot.Row, key),
            };
        }
        else
        {
            target = TargetOf(index);
        }
        var desks = Desks();
        var desk = desks.FirstOrDefault(each => each.Key == target.Key);
        switch (target.Spot)
        {
            case Spot.Add:
                if (desktops?.Add() == true)
                {
                    Reread();
                    Rest();
                }
                return default;
            case Spot.Row when desk.Row.Key is { Length: > 0 }:
                switchTo = desk.Index;
                return new IslandAction(View: IslandView.Desktops);
            case Spot.Rename when desk.Row.Key is { Length: > 0 } && ActionsOf(desk, desks.Count).CanRename:
                Begin(desk);
                return default;
            case Spot.Delete when desk.Row.Key is { Length: > 0 } && ActionsOf(desk, desks.Count).CanDelete:
                if (desktops?.Remove(desk.Index) == true)
                {
                    Reread();
                    keyed = keyed == desk.Key ? null : keyed;
                }
                Rest();
                return default;
            default:
                Redrawn();
                return default;
        }
    }

    /// <summary>
    /// The dock, once the folder has gone: the desktop a click chose, where one waits to be switched
    /// to — asked once, and forgotten as it is told.
    /// </summary>
    internal int? TakeSwitch()
    {
        var to = switchTo;
        switchTo = null;
        return to;
    }

    /// <summary>Something went from under the pointer: nothing is lit until it moves, and the line is laid out again.</summary>
    private void Rest()
    {
        hot = default;
        hotId = 0;
        Redrawn();
    }

    // --- Renaming -----------------------------------------------------------------------------------

    /// <summary>Starts typing <paramref name="desk"/>'s name, all of it chosen, as Task View's rename does.</summary>
    private void Begin(Desk desk)
    {
        renaming = desk.Key;
        editor.Placeholder = Switchers.Numbered(desk.Index);
        editor.Begin(desk.Row.Name, selectAll: true);
        keyed = desk.Key;
        Redrawn();
    }

    /// <summary>tests: the desktop being renamed, by its key.</summary>
    internal string? Renaming => renaming;

    internal InlineEditor Editor => editor;

    protected internal override bool CardTyping => renaming is not null;

    /// <summary>Stops typing; <paramref name="keep"/>: the name typed is given to the desktop where it is one and new.</summary>
    private void Stop(bool keep)
    {
        if (renaming is not { } key)
        {
            return;
        }
        renaming = null;
        selecting = false;
        var typed = editor.Text.Trim();
        if (keep && typed.Length > 0 && Desks().FirstOrDefault(desk => desk.Key == key) is { Row.Key.Length: > 0 } desk && typed != desk.Row.Name)
        {
            if (desktops?.Rename(desk.Index, typed) == true)
            {
                Reread();
            }
        }
        Redrawn();
    }

    /// <summary>A press elsewhere, or the folder closing: what was typed is kept, as a click away from Task View's box keeps it.</summary>
    protected internal override void CardStopTyping() => Stop(keep: true);

    protected internal override bool CardChar(char c, WidgetSurface surface)
    {
        if (renaming is null)
        {
            return false;
        }
        if (editor.Char(c))
        {
            Redrawn();
        }
        return true;
    }

    // --- The keys -----------------------------------------------------------------------------------

    /// <summary>keys: the line walks its desktops and their editor itself (CellIsland.WalksItsOwnKeys).</summary>
    protected internal override bool WalksItsOwnKeys => true;

    /// <summary>
    /// Up and Down walk the rows, "New desktop" last — past either end the keys are the folder's
    /// again; Alt+Up and Alt+Down move the desktop the keys are on a place; F2 renames it and Delete
    /// closes it; Enter and Space are the folder's, which click the row (<see cref="Click"/>). While a
    /// name is typed every key is the box's: Enter keeps it, Escape does not.
    /// </summary>
    protected internal override bool CardKey(int key, WidgetSurface surface)
    {
        var shift = KeyState.Down(VK.VK_SHIFT);
        var control = KeyState.Down(VK.VK_CONTROL);
        if (renaming is { } typing)
        {
            switch (key)
            {
                case VK.VK_ESCAPE:
                    Stop(keep: false);
                    return true;
                case VK.VK_RETURN:
                    Stop(keep: true);
                    return true;
                case VK.VK_TAB:
                    Stop(keep: true);
                    return false;
            }
            var sizes = SizesOf(shownStyle);
            var row = shown?.Rows.FirstOrDefault(each => each.Desk?.Key == typing);
            var box = row is { } typed && shown is { } plan ? EditorOf(Moved(shownBox, typed.Top, plan.RowHeight), sizes, shownStyle) : default;
            editor.Key(key, shift, control, Gfx, sizes.Items.TextPx, box, shownStyle, out _);
            Redrawn();
            return true;
        }
        var desks = Desks();
        var keys = desks.Select(desk => desk.Key).Append(AddKey).ToList();
        var at = keyed is { } on ? keys.IndexOf(on) : -1;
        switch (key)
        {
            case VK.VK_UP or VK.VK_DOWN when KeyState.Down(VK.VK_MENU):
                if (at < 0 || at >= desks.Count)
                {
                    return false;
                }
                var to = at + (key == VK.VK_UP ? -1 : 1);
                if (to >= 0 && to < desks.Count && ActionsOf(desks[at], desks.Count).CanMove)
                {
                    Reorder(desks, at, to, lifted: null);
                }
                return true;
            case VK.VK_UP or VK.VK_DOWN:
            {
                var next = at < 0 ? (key == VK.VK_DOWN ? 0 : keys.Count - 1) : at + (key == VK.VK_DOWN ? 1 : -1);
                if (next < 0 || next >= keys.Count)
                {
                    keyed = null;
                    Redrawn();
                    return false;
                }
                keyed = keys[next];
                Redrawn();
                return true;
            }
            case VK.VK_F2 when at >= 0 && at < desks.Count:
                if (ActionsOf(desks[at], desks.Count).CanRename)
                {
                    Begin(desks[at]);
                }
                return true;
            case VK.VK_DELETE when at >= 0 && at < desks.Count:
                _ = Click(IdOf(new Target(Spot.Delete, desks[at].Key)));
                return true;
            default:
                return false;
        }
    }

    /// <summary>tests: the row the keys are on.</summary>
    internal string? Keyed => keyed;

    // --- The pointer --------------------------------------------------------------------------------

    protected internal override void CardHover(Vector2? point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var target = point is { } at ? LineTarget(at, box, style) : default;
        if (point is not null && target.Spot != Spot.None)
        {
            // The pointer and the keys light one row at a time.
            keyed = renaming;
        }
        var let = press.Over(IdOf(target));
        if (target == hot && !let)
        {
            return;
        }
        hot = target;
        hotId = IdOf(target);
        _ = Wake();
    }

    /// <summary>Over a gripper that can drag its desktop, the pointer is the move cursor.</summary>
    protected internal override bool CardGrip(Vector2 point, D2D_RECT_F box, in TileStyle style) =>
        LineTarget(point, box, style) is { Spot: Spot.Grip } target && Lay(style) is { CanMove: true } && !CardTyping && target.Key.Length > 0;

    /// <summary>
    /// A press: in the name being typed, the caret goes there (a drag selects); on a gripper whose
    /// desktop can move, the row is the line's to lift; anywhere else it stays the folder's, which
    /// makes it the click (lit pressed meanwhile).
    /// </summary>
    protected internal override bool CardPress(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var plan = Lay(style);
        Keep(plan, box, style);
        var target = LineTarget(point, box, style);
        if (renaming is { } typing)
        {
            var row = plan.Rows.FirstOrDefault(each => each.Desk?.Key == typing);
            var sizes = SizesOf(style);
            var editing = row.Desk is not null ? EditorOf(Moved(box, row.Top, plan.RowHeight), sizes, style) : default;
            if (row.Desk is not null && LineRows.Inside(editing, point))
            {
                editor.Press(point, 1, KeyState.Down(VK.VK_SHIFT), Gfx, sizes.Items.TextPx, editing, style);
                selecting = true;
                Redrawn();
                return true;
            }
            Stop(keep: true);
        }
        if (target.Spot == Spot.Grip && plan.CanMove && plan.Rows.FirstOrDefault(each => each.Desk?.Key == target.Key) is { Desk: not null } grip)
        {
            grabbed = target.Key;
            grabAt = point.Y - (box.top + grip.Top);
            pressedY = point.Y;
            liftTop = grip.Top;
            lifted = cancelled = false;
            keyed = null;
            return true;
        }
        if (press.Down(IdOf(target)))
        {
            _ = Wake();
        }
        return false;
    }

    protected internal override void CardDrag(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        if (selecting && renaming is { } typing && shown is { } typed)
        {
            var row = typed.Rows.FirstOrDefault(each => each.Desk?.Key == typing);
            var sizes = SizesOf(style);
            editor.Drag(point, Gfx, sizes.Items.TextPx, EditorOf(Moved(box, row.Top, typed.RowHeight), sizes, style), style);
            Redrawn();
            return;
        }
        if (grabbed is not { } key || cancelled)
        {
            return;
        }
        var plan = Lay(style);
        Keep(plan, box, style);
        if (!lifted && MathF.Abs(point.Y - pressedY) <= GetSystemMetrics(SM.SM_CYDRAG))
        {
            return;
        }
        var from = IndexIn(plan.Desks, key);
        if (from < 0)
        {
            Cancel();
            return;
        }
        lifted = true;
        var first = plan.Rows[0].Top;
        var last = plan.Rows[plan.Desks.Count - 1].Top;
        liftTop = Math.Clamp(point.Y - box.top - grabAt, first, last);
        var to = DropIndex(liftTop, first, plan.RowHeight, plan.Desks.Count);
        var now = Ticks();
        for (var index = 0; index < plan.Desks.Count; index++)
        {
            if (index != from)
            {
                Slide(plan.Desks[index].Key, Shift(index, from, to, plan.RowHeight), now);
            }
        }
        Redrawn();
    }

    /// <summary>
    /// The pointer lets go: a desktop lifted is moved where it was let go, through the shell, and its
    /// row settles into that slot; one that was not, or that Escape put back, stays where it is.
    /// </summary>
    protected internal override void CardRelease()
    {
        selecting = false;
        if (grabbed is not { } key)
        {
            return;
        }
        var wasLifted = lifted && !cancelled;
        grabbed = null;
        lifted = cancelled = false;
        if (!wasLifted || shown is not { } plan)
        {
            Redrawn();
            return;
        }
        var from = IndexIn(plan.Desks, key);
        var to = DropIndex(liftTop, plan.Rows[0].Top, plan.RowHeight, plan.Desks.Count);
        if (from < 0 || to == from)
        {
            Settle(plan, key);
            return;
        }
        Reorder(plan.Desks, from, to, lifted: liftTop);
    }

    /// <summary>Escape while a desktop is lifted: it goes back where it was, the others with it, and the drag is over until the button is let go.</summary>
    protected internal override bool CardEscape()
    {
        if (grabbed is null || !lifted || cancelled || shown is not { } plan)
        {
            return false;
        }
        cancelled = true;
        Settle(plan, grabbed);
        return true;
    }

    /// <summary>The drag is over without a move: the lifted row slides from where it is back to its slot, and the others to theirs.</summary>
    private void Settle(Plan plan, string key)
    {
        var now = Ticks();
        var at = plan.Rows.FirstOrDefault(row => row.Desk?.Key == key);
        foreach (var desk in plan.Desks)
        {
            if (desk.Key == key)
            {
                tweens[key] = new Tween(liftTop - at.Top, 0, now, Ms);
            }
            else
            {
                Slide(desk.Key, 0, now);
            }
        }
        animating = true;
        Redrawn();
    }

    private void Cancel()
    {
        grabbed = null;
        lifted = cancelled = false;
        tweens.Clear();
        Redrawn();
    }

    /// <summary>
    /// Moves desktop <paramref name="from"/> of <paramref name="desks"/> (the order shown) to
    /// <paramref name="to"/>: asked of the shell, and shown at once in the new order, each row
    /// sliding there from where it was drawn — the lifted one from <paramref name="lifted"/>.
    /// </summary>
    private void Reorder(IReadOnlyList<Desk> desks, int from, int to, float? lifted)
    {
        if (desktops is null || !desktops.Move(desks[from].Index, to))
        {
            if (lifted is not null && shown is { } plan)
            {
                Settle(plan, desks[from].Key);
            }
            return;
        }
        Reread();
        var now = Ticks();
        var height = shown?.RowHeight ?? RowHeight(shownStyle);
        // Where each row is drawn now, off the slot it is leaving.
        var before = new Dictionary<string, float>(StringComparer.Ordinal);
        for (var index = 0; index < desks.Count; index++)
        {
            before[desks[index].Key] = (index * height) + Offset(desks[index].Key, now);
        }
        if (lifted is { } top && shown is { } laid)
        {
            before[desks[from].Key] = top - laid.Rows[0].Top;
        }
        var order = Reordered(desks.Select(desk => desk.Key).ToList(), from, to);
        pending = order;
        pendingUntil = now + PendingMs;
        for (var index = 0; index < order.Count; index++)
        {
            tweens[order[index]] = new Tween(before[order[index]] - (index * height), 0, now, Ms);
        }
        animating = true;
        Redrawn();
    }

    // --- Slides -------------------------------------------------------------------------------------

    /// <summary>A row's slide: <see cref="From"/> px off its slot to <see cref="To"/>, from <see cref="Start"/> (ms) for <see cref="Length"/> ms.</summary>
    internal readonly record struct Tween(float From, float To, long Start, int Length)
    {
        /// <summary>Where it is at <paramref name="now"/>: on the one motion's decelerate curve (§489).</summary>
        public float At(long now) =>
            Length <= 0 ? To : From + ((To - From) * Motion.Decelerate.At(Math.Clamp((now - Start) / (float)Length, 0, 1)));

        public bool Done(long now) => now - Start >= Length;
    }

    /// <summary>Row <paramref name="key"/>'s offset off its slot at <paramref name="now"/>.</summary>
    internal float Offset(string key, long now) => tweens.TryGetValue(key, out var tween) ? tween.At(now) : 0;

    /// <summary>Row <paramref name="key"/> slides to <paramref name="to"/> px off its slot, from wherever it is now.</summary>
    private void Slide(string key, float to, long now)
    {
        var from = Offset(key, now);
        if (tweens.TryGetValue(key, out var going) ? MathF.Abs(going.To - to) < 0.5f : MathF.Abs(to) < 0.5f)
        {
            return;
        }
        tweens[key] = new Tween(from, to, now, Ms);
        animating = true;
    }

    /// <summary>Where a row lifted to <paramref name="top"/> lands among <paramref name="count"/> rows of <paramref name="height"/> from <paramref name="first"/>: the nearest slot.</summary>
    internal static int DropIndex(float top, float first, float height, int count) =>
        count <= 0 ? 0 : Math.Clamp((int)MathF.Round((top - first) / MathF.Max(1, height)), 0, count - 1);

    /// <summary>
    /// How far row <paramref name="index"/> stands off its slot while row <paramref name="from"/> is
    /// held over slot <paramref name="to"/>: a row's height up or down for the rows between, which
    /// make room; nothing for the rest.
    /// </summary>
    internal static float Shift(int index, int from, int to, float height) =>
        from < to && index > from && index <= to ? -height
        : to < from && index >= to && index < from ? height
        : 0;

    /// <summary><paramref name="order"/> with the one at <paramref name="from"/> taken out and put in at <paramref name="to"/>.</summary>
    internal static List<string> Reordered(IReadOnlyList<string> order, int from, int to)
    {
        var moved = order.ToList();
        var key = moved[from];
        moved.RemoveAt(from);
        moved.Insert(Math.Clamp(to, 0, moved.Count), key);
        return moved;
    }

    private static int IndexIn(IReadOnlyList<Desk> desks, string key)
    {
        for (var index = 0; index < desks.Count; index++)
        {
            if (desks[index].Key == key)
            {
                return index;
            }
        }
        return -1;
    }

    /// <summary>The plan as it is now, for the keys and the release.</summary>
    private void Keep(Plan plan, D2D_RECT_F box, in TileStyle style)
    {
        shown = plan;
        shownBox = box;
        shownStyle = style;
    }

    // --- Drawn --------------------------------------------------------------------------------------

    protected internal override void PaintCard(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var plan = Lay(style);
        Keep(plan, box, style);
        var now = Ticks();
        var theme = QuickTheme.For(style.Theme);
        var sizes = SizesOf(style);
        var count = plan.Desks.Count;
        var held = lifted && !cancelled ? grabbed : null;
        var side = (int)sizes.Icon;
        if (side != iconSide)
        {
            iconCache.Clear();
            iconSide = side;
        }
        foreach (var row in plan.Rows)
        {
            if (row.Desk is not { } desk)
            {
                PaintAdd(dc, Moved(box, row.Top, plan.RowHeight), sizes, style, theme);
                continue;
            }
            if (desk.Key == held)
            {
                continue;
            }
            var top = row.Top + Offset(desk.Key, now);
            PaintDesk(dc, Moved(box, top, plan.RowHeight), desk, count, plan.CanMove, sizes, style, theme, lifted: false);
        }
        if (held is not null && plan.Rows.FirstOrDefault(row => row.Desk?.Key == held) is { Desk: { } carried })
        {
            PaintDesk(dc, Moved(box, liftTop, plan.RowHeight), carried, count, plan.CanMove, sizes, style, theme, lifted: true);
        }
        // Still sliding: drawn again on the folder's quick tick; the frame that ends a slide is drawn
        // before the line says it has stopped.
        animating = tweens.Values.Any(tween => !tween.Done(now));
        if (!animating)
        {
            tweens.Clear();
        }
    }

    /// <summary>
    /// A desktop's row in <paramref name="at"/>: its plate (the chosen one's fill and pill, the lit
    /// one's), its gripper, its name, and its apps — or, lit, its pencil and its cross. Lifted, it
    /// stands on a raised plate of its own with a shadow under it, over the rows making room.
    /// </summary>
    private void PaintDesk(ID2D1DeviceContext* dc, D2D_RECT_F at, Desk desk, int count, bool canMove, in Sizes sizes, in TileStyle style, QuickTheme theme, bool lifted)
    {
        var plate = PlateOf(at, style);
        var radius = style.Px(4);
        var form = sizes.Form;
        var actions = ActionsOf(desk, count);
        var lit = !lifted && grabbed is null && renaming != desk.Key
            && ((hot.Key == desk.Key && hot.Spot is Spot.Row or Spot.Grip or Spot.Rename or Spot.Delete) || keyed == desk.Key);
        if (lifted)
        {
            // Raised: a soft shadow a little under it, the solid control fill and its rim, so the row
            // reads as picked up off the list rather than as another row's light.
            for (var ring = 4; ring >= 1; ring--)
            {
                var spread = style.Px(ring);
                var shadow = new D2D_RECT_F(plate.left - spread, plate.top - spread + style.Px(2), plate.right + spread, plate.bottom + spread + style.Px(2));
                PopupPaint.Fill(dc, shadow, radius + spread, new DXGI_RGBA(0, 0, 0, 0.045f));
            }
            PopupPaint.Fill(dc, plate, radius, theme.ControlSolid);
            if (desk.Row.Current)
            {
                PopupPaint.Fill(dc, plate, radius, theme.SubtleSelected);
            }
            PopupPaint.Stroke(dc, plate, radius, MathF.Max(1, style.Px(1)), theme.ControlStroke);
        }
        else if (desk.Row.Current)
        {
            LineItem.Chosen(dc, at, theme, lit);
        }
        else if (lit)
        {
            LineItem.Light(dc, at, theme, press.On(hotId) && hot.Key == desk.Key && hot.Spot == Spot.Row);
        }
        if (desk.Row.Current)
        {
            // WinUI's selection pill, at the row's start; lifted, at its plate's.
            if (lifted)
            {
                LineItem.Pill(dc, plate, 0, style.Scale, 1, theme.Accent);
            }
            else
            {
                LineItem.Pill(dc, at, style, theme.Accent);
            }
        }
        if (!lifted && keyed == desk.Key && renaming is null && sizes.Form.Inset > 0)
        {
            TogglesTile.FocusRing(dc, plate, radius, style, theme);
        }
        var gripHot = lifted || (hot == new Target(Spot.Grip, desk.Key) && grabbed is null);
        Gripper(dc, GripOf(at, sizes), style, !canMove ? theme.TextDisabled : gripHot ? theme.Text : theme.TextSecondary);
        var height = at.bottom - at.top;
        var left = at.left + form.TextLeft;
        if (renaming == desk.Key)
        {
            editor.Paint(dc, Gfx, EditorOf(at, sizes, style), sizes.Items.TextPx, theme, style);
            return;
        }
        var end = at.right - form.Inset - EndRoom(desk, sizes, style, lit) - style.Px(8);
        var px = sizes.Items.TextPx;
        var weight = desk.Row.Current ? Weight.Semibold : Weight.Regular;
        TilePaint.Line(dc, Gfx, desk.Row.Name, px, weight, theme.Text, left, at.top + PopupPaint.Round((height - TilePaint.LineHeight(Gfx, px, weight)) / 2), MathF.Max(1, end - left));
        if (lit)
        {
            var (rename, delete) = ButtonsOf(at, sizes, style);
            Button(dc, sizes.Buttons, rename, RenameGlyph, actions.CanRename, hot == new Target(Spot.Rename, desk.Key), style);
            Button(dc, sizes.Buttons, delete, DeleteGlyph, actions.CanDelete, hot == new Target(Spot.Delete, desk.Key), style);
            return;
        }
        Apps(dc, desk, at, sizes, style, theme);
    }

    /// <summary>A row's button, subtle: its glyph, lit under the pointer; greyed where the row cannot do it.</summary>
    private void Button(ID2D1DeviceContext* dc, in LineButton sizes, D2D_RECT_F box, string glyph, bool enabled, bool hovered, in TileStyle style) =>
        sizes.Paint(dc, Gfx, style.Theme, box, "", glyph, ButtonStyle.Subtle, enabled && hovered, press.On(hotId), enabled: enabled);


    /// <summary>The apps open on the desktop at the row's end, then "+N" for the rest.</summary>
    private void Apps(ID2D1DeviceContext* dc, Desk desk, D2D_RECT_F at, in Sizes sizes, in TileStyle style, QuickTheme theme)
    {
        var right = at.right - sizes.Form.Inset;
        var middle = (at.top + at.bottom) / 2;
        if (desk.Row.Plus is { Length: > 0 } plus)
        {
            var px = sizes.Form.SmallPx;
            var width = TilePaint.LineRight(dc, Gfx, plus, px, Weight.Regular, theme.TextSecondary, right, middle - (TilePaint.LineHeight(Gfx, px) / 2), style.Px(40));
            right -= width + sizes.IconGap;
        }
        var side = sizes.Icon;
        for (var index = desk.Row.Apps.Count - 1; index >= 0; index--)
        {
            if (Icon(desk.Row.Apps[index], (int)side) is { } picture)
            {
                Gfx.DrawPixels(dc, picture, right - side, PopupPaint.Round(middle - (side / 2)), side);
            }
            right -= side + sizes.IconGap;
        }
    }

    /// <summary>An app's icon at <paramref name="side"/> px, asked of the shell once per app.</summary>
    private Pixels? Icon(Item app, int side)
    {
        if (iconCache.TryGetValue(app.Target, out var kept))
        {
            return kept;
        }
        Pixels? picture = null;
        try
        {
            picture = iconOf(app, side);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Console.Error.WriteLine($"Karpo: desktops: an app's icon: {error.Message}");
        }
        iconCache[app.Target] = picture;
        return picture;
    }

    /// <summary>The gripper (PopupPaint.Gripper), centred in <paramref name="box"/> at the line's scale.</summary>
    internal static void Gripper(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, DXGI_RGBA ink) => PopupPaint.Gripper(dc, box, style.Scale, ink);

    /// <summary>"New desktop" at the foot: the accent's plus and the words in the link's ink, lit under the pointer or the keys.</summary>
    private void PaintAdd(ID2D1DeviceContext* dc, D2D_RECT_F at, in Sizes sizes, in TileStyle style, QuickTheme theme)
    {
        var plate = PlateOf(at, style);
        var radius = style.Px(4);
        var lit = grabbed is null && (hot.Spot == Spot.Add || keyed == AddKey);
        if (lit)
        {
            LineItem.Light(dc, at, theme, press.On(hotId) && hot.Spot == Spot.Add);
        }
        if (keyed == AddKey)
        {
            TogglesTile.FocusRing(dc, plate, radius, style, theme);
        }
        var form = sizes.Form;
        PopupPaint.Glyph(dc, Gfx, AddGlyph, form.Glyph, theme.TextSecondary, form.IconBox(at.left, at.top, at.bottom - at.top));
        var px = sizes.Items.TextPx;
        var left = at.left + form.TextLeft;
        TilePaint.Line(dc, Gfx, S.DockMenuNewDesktop, px, Weight.Regular, theme.TextSecondary, left,
            at.top + PopupPaint.Round((at.bottom - at.top - TilePaint.LineHeight(Gfx, px)) / 2), MathF.Max(1, at.right - form.Inset - left));
    }

    // --- Words for the pointer and the screen reader ------------------------------------------------

    /// <summary>The gripper and the buttons name themselves, being drawings alone; where one cannot act, why; a cut name whole.</summary>
    protected internal override (string Text, D2D_RECT_F Part)? CardTip(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var plan = Lay(style);
        foreach (var (target, at) in Targets(plan, box, style))
        {
            if (!LineRows.Inside(at, point))
            {
                continue;
            }
            if (plan.Desks.FirstOrDefault(desk => desk.Key == target.Key) is not { Row.Key.Length: > 0 } desk)
            {
                return null;
            }
            var actions = ActionsOf(desk, plan.Desks.Count);
            switch (target.Spot)
            {
                case Spot.Grip:
                    return (actions.WhyNotMove ?? Switchers.Reorder, at);
                case Spot.Rename when hot.Key == desk.Key || keyed == desk.Key:
                    return (actions.WhyNotRename ?? S.DockSwitcherRename, at);
                case Spot.Delete when hot.Key == desk.Key || keyed == desk.Key:
                    return (actions.WhyNotDelete ?? S.DockSwitcherDelete, at);
            }
            var sizes = SizesOf(style);
            var weight = desk.Row.Current ? Weight.Semibold : Weight.Regular;
            var room = at.right - sizes.Form.Inset - EndRoom(desk, sizes, style, lit: hot.Key == desk.Key) - style.Px(8) - (at.left + sizes.Form.TextLeft);
            return PopupPaint.Measure(Gfx, desk.Row.Name, sizes.Items.TextPx, weight).X > room ? (desk.Row.Name, at) : null;
        }
        return null;
    }

    /// <summary>What a line of one row shows first: the first desktop's name.</summary>
    public string MainPiece => "desktop 0";

    public IReadOnlyList<LinePiece> LinePieces(D2D_RECT_F box, in TileStyle style)
    {
        var plan = Lay(style);
        var sizes = SizesOf(style);
        var pieces = new List<LinePiece>();
        var index = 0;
        foreach (var row in plan.Rows)
        {
            var at = Moved(box, row.Top, plan.RowHeight);
            if (row.Desk is not { } desk)
            {
                pieces.Add(new LinePiece("add", at, S.DockMenuNewDesktop));
                continue;
            }
            pieces.Add(new LinePiece($"desktop {index}", at, desk.Row.Name));
            pieces.Add(new LinePiece($"grip {index}", GripOf(at, sizes), Switchers.Reorder));
            var (rename, delete) = ButtonsOf(at, sizes, style);
            pieces.Add(new LinePiece($"rename {index}", rename, S.DockSwitcherRename));
            pieces.Add(new LinePiece($"delete {index}", delete, S.DockSwitcherDelete));
            if (desk.Row.Current)
            {
                pieces.Add(new LinePiece("current", at, desk.Row.Name));
            }
            index++;
        }
        return pieces;
    }
}
