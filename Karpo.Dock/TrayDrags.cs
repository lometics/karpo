// What a tray icon dragged with the pointer would do, worked out apart from any
// window so it can be tested: where along the bar's tray it would land, and
// when it would go behind the chevron instead.
//
// The rules follow the Windows 11 taskbar. An icon moved along the tray takes
// the slot its middle is over, the others closing up around it; one coming from
// the overflow takes the place between two icons the pointer is nearest to, so
// it can be put before the first or after the last — and as it brings a cell of
// its own, the cell after the last icon takes it too, as does the cell after the
// chevron on a bar that shows no icon yet. Over the chevron — and anywhere in
// the open overflow — the icon goes into the hidden icon menu, at its end
// unless the overflow itself says where. A drop anywhere else is no drop at
// all: the icon stays as it was.

using Karpo.Core;
using TerraFX.Interop.Windows;

namespace Karpo.Dock;

public static class TrayDrags
{
    /// <summary>
    /// The place a drop at <paramref name="along"/> (px along the bar) takes among
    /// <paramref name="count"/> icons that start at <paramref name="first"/>, one every
    /// <paramref name="step"/> px (a cell and the gap after it): 0 before the first,
    /// <paramref name="count"/> after the last. Half a step is the turning point, the middle of a
    /// cell now the cells touch; a drop on it exactly goes before that icon, as one let go on an
    /// icon's middle means "here", where that icon stands.
    /// </summary>
    public static int IndexAt(float along, float first, float step, int count, int current = -1)
    {
        if (step <= 0 || count <= 0)
        {
            return 0;
        }
        var over = (along - first) / step - 0.5f;
        // The place it already has holds until the pointer is a fifth of a step past its edge, so a
        // pointer resting on a boundary doesn't flick the gap between the two places.
        if (current >= 0 && current <= count && over > current - 1 - Hysteresis && over <= current + Hysteresis)
        {
            return current;
        }
        return Math.Clamp((int)MathF.Ceiling(over), 0, count);
    }

    /// <summary>How far past a boundary, in steps, the pointer goes before the gap moves on from the place it has.</summary>
    public const float Hysteresis = 0.2f;

    /// <summary>
    /// The slot an icon held by its middle at <paramref name="along"/> takes when it is moved among
    /// <paramref name="count"/> icons, itself one of them: the slot its middle is in, counting a cell
    /// and the gap after it as one, so 0 to <paramref name="count"/> - 1. The others close up around
    /// it (Scene.TrayDrag), and the slot is its place among them. <see cref="IndexAt"/>'s nearer
    /// boundary put it one place too far whenever the pointer was past a cell's middle: dragged right
    /// onto the third of four it went last, dragged left onto the second it went third.
    /// </summary>
    public static int SlotAt(float along, float first, float step, int count, int current = -1)
    {
        if (step <= 0 || count <= 1)
        {
            return 0;
        }
        var over = (along - first) / step;
        if (current >= 0 && current < count && over >= current - Hysteresis && over < current + 1 + Hysteresis)
        {
            return current;
        }
        return Math.Clamp((int)MathF.Floor(over), 0, count - 1);
    }

    /// <summary>
    /// What a drop at <paramref name="along"/> over a bar's tray means: hidden when it is on the
    /// chevron, else a place among the <paramref name="shown"/> icons. Null when the pointer is not
    /// over the tray at all, where a drop does nothing. <paramref name="left"/> and
    /// <paramref name="width"/> are the tray block's, <paramref name="chevronSlot"/> the room the
    /// chevron takes before the first icon — its cell and the gap after it, 0 when the bar shows
    /// none — <paramref name="step"/> one icon's cell to the next, and <paramref name="hidden"/> how
    /// many icons wait behind the chevron. <paramref name="held"/> when the dragged icon is one of
    /// the <paramref name="shown"/> ones (moved along the bar): its place is then the slot it is over
    /// among them (<see cref="SlotAt"/>); an icon coming from the overflow takes the boundary
    /// nearest the pointer (<see cref="IndexAt"/>). Either way the place counts the icons without the
    /// dragged one (<see cref="TrayDrop"/>).
    /// <para>
    /// An icon coming from the overflow brings a cell the tray doesn't have yet: the one after the
    /// last icon takes it as well, one <paramref name="step"/> past the block. The block ends on its
    /// last cell, so that cell's right half was all there was of "after the last", and a bar showing
    /// no icon at all had nothing but the chevron — every drop there hid the icon again. The cell
    /// after the chevron is then the bar's first place.
    /// </para>
    /// </summary>
    public static TrayDrop? OnBar(float along, float left, float width, float chevronSlot, float step, int shown, int hidden, bool held = false, int current = -1)
    {
        var end = left + width + (held ? 0f : MathF.Max(0f, step));
        if (width <= 0 || along < left || along >= end)
        {
            return null;
        }
        if (chevronSlot > 0 && along < left + chevronSlot)
        {
            // Onto the chevron: behind it, after the icons already there.
            return new TrayDrop(TrayArea.Overflow, Math.Max(0, hidden));
        }
        if (shown <= 0)
        {
            // No icon on the bar yet: the first place is the cell after the chevron.
            return new TrayDrop(TrayArea.Shown, 0);
        }
        var first = left + chevronSlot;
        return new TrayDrop(TrayArea.Shown, held ? SlotAt(along, first, step, shown, current) : IndexAt(along, first, step, shown, current));
    }

    /// <summary>
    /// What a drop at <paramref name="screen"/> (screen px) means over the bar <paramref name="layout"/>
    /// lays out: <see cref="OnBar(float, float, float, float, float, int, int, bool)"/> along its tray,
    /// where the point lies on the bar itself (<see cref="Layout.AlongOnBar"/>); null off the bar or on
    /// a bar with no tray. <paramref name="shown"/> and <paramref name="hidden"/> are the icons on the
    /// bar and behind the chevron — the bar draws as many of the shown as its tray holds — and
    /// <paramref name="heldAt"/> the dragged icon's place among the shown ones when it was taken from
    /// the bar, -1 when it came from the overflow. <paramref name="current"/> is the place the last
    /// move gave it (-1 for none), which holds a little past its edge (<see cref="Hysteresis"/>).
    /// </summary>
    public static TrayDrop? OnLayout(Layout layout, POINT screen, int shown, int hidden, int heldAt = -1, int current = -1, int part = -1)
    {
        part = part >= 0 ? part : layout.PartOf(Hit.TrayOverflow);
        if (part < 0 || part >= layout.Specs.Count || layout.Specs[part].Kind != BarKind.Tray
            || !layout.Placed(part) || layout.AlongOnBar(screen) is not { } along)
        {
            return null;
        }
        var block = layout.PartAt(part);
        // The icons this bar draws, which a tray with a set room may hold fewer of than there are to
        // show; and whether the dragged one is among them, moved along the bar.
        var drawn = Math.Min(Math.Max(0, shown), block.Count);
        return OnBar(
            along,
            block.Left,
            // The block as long as its icons, not with the cell the drag gave it or took away
            // (Layout.TrayMoreOf), so the tray reads the pointer as it did before it grew or shrank.
            block.Width - layout.TrayMoreOf(part) * layout.TrayStep,
            // The chevron's cell and the gap after it, then one cell to the next.
            layout.TrayChevronOf(part) > 0f ? layout.TrayStep : 0f,
            layout.TrayStep,
            drawn,
            hidden,
            heldAt >= 0 && heldAt < drawn,
            current);
    }

    /// <summary>
    /// The cell a drag gives a tray's block or takes from it, so the block is as long as the icons it
    /// will have: one more where an icon not drawn in it (from the overflow) would land among its
    /// icons, <paramref name="aimedHere"/>; one fewer for a tray that draws the dragged icon
    /// (<paramref name="draws"/>) where it would go behind the chevron; none otherwise. The block kept
    /// its length, and the icons making room went past its ends. <paramref name="aim"/> is the last
    /// place the drag showed — kept while the pointer crosses the room between the tray and the
    /// overflow, so the tray doesn't grow and shrink back on the way —, null before it showed any.
    /// </summary>
    public static int MoreFor(TrayDrop? aim, bool aimedHere, bool draws) => aim switch
    {
        { Area: TrayArea.Shown } => aimedHere && !draws ? 1 : 0,
        { Area: TrayArea.Overflow } => draws ? -1 : 0,
        _ => 0,
    };

    /// <summary>
    /// What a drop in the open overflow means: hidden, at the place the flyout's row
    /// <paramref name="index"/> says (its own count when the pointer is past the last row) among
    /// <paramref name="count"/> rows. <paramref name="from"/> is the dragged icon's own row when it
    /// was taken from this overflow, -1 when it came from the bar: its own row is counted out, so a
    /// drop past it lands where it was let go and not one row further.
    /// <para>
    /// The first <paramref name="spilled"/> rows are icons the bar would show but has no room for (a
    /// tray of a set size, <see cref="Overflow{T}"/>): they are not hidden ones, so a place among
    /// the hidden icons is counted after them, and a drop before them goes first among the hidden.
    /// </para>
    /// </summary>
    public static TrayDrop InOverflow(int index, int count, int from = -1, int spilled = 0)
    {
        var place = Math.Clamp(index, 0, Math.Max(0, count));
        var mine = from >= 0 && from < count;
        if (mine)
        {
            place = Math.Clamp(place > from ? place - 1 : place, 0, count - 1);
        }
        // The rows before the hidden ones, the dragged one counted out where it is one of them.
        var before = Math.Clamp(spilled, 0, Math.Max(0, count)) - (mine && from < spilled ? 1 : 0);
        return new TrayDrop(TrayArea.Overflow, Math.Max(0, place - Math.Max(0, before)));
    }

    /// <summary>
    /// What the overflow a bar opens holds, in order: the icons the bar would show past the
    /// <paramref name="drawn"/> it has room for (a tray of a set size), then the hidden ones.
    /// Icons that didn't fit showed nowhere before, neither on the bar nor behind the chevron; on
    /// Windows 11 they go into the overflow, at its start.
    /// </summary>
    public static List<T> Overflow<T>(IReadOnlyList<T> shown, int drawn, IReadOnlyList<T> hidden) =>
        [.. shown.Skip(Math.Max(0, drawn)), .. hidden];
}

/// <summary>
/// The glide of one tray icon to where the drag wants it, retargetable: a new target takes up from
/// where the icon is by then, not from the old target, and the same target again changes nothing, so
/// a pointer moving on doesn't restart a slide that is under way (which snapped it, once per move).
/// The curve is the one motion's decelerate (<see cref="Motion.Decelerate"/>).
/// </summary>
public struct TrayGlide
{
    private float from;
    private float to;
    private long started;
    private int length;

    /// <summary>Where the glide set off from.</summary>
    public readonly float From => from;

    /// <summary>Where the glide is going.</summary>
    public readonly float Target => to;

    /// <summary>Where the icon is at <paramref name="now"/> (ms).</summary>
    public readonly float At(long now) =>
        length <= 0 || now - started >= length ? to : from + (to - from) * Motion.Decelerate.At((now - started) / (float)length);

    /// <summary>
    /// On to <paramref name="target"/> in <paramref name="ms"/> from where it is at <paramref name="now"/>.
    /// False, and nothing touched, when it is already going there.
    /// </summary>
    public bool To(float target, long now, int ms)
    {
        if (target == to)
        {
            return false;
        }
        from = At(now);
        to = target;
        started = now;
        length = ms;
        return true;
    }

    /// <summary>On its way from <paramref name="from"/> to its place (0), begun at <paramref name="now"/>: an icon an update takes up mid-glide.</summary>
    public static TrayGlide Settling(float from, long now, int ms) =>
        new() { from = from, to = 0f, started = now, length = ms };

    /// <summary>
    /// How an update takes up an icon that was at <paramref name="was"/> on the screen and stands at
    /// <paramref name="home"/> in it (px in the bar, along y on a <paramref name="vertical"/> one):
    /// settling from there over <paramref name="ms"/> from <paramref name="now"/>, as the scene slides
    /// it. Null where it hasn't moved, and where it moved across the bar — a line of its own, which a
    /// glide along the bar can't say —, either way nothing to go on from.
    /// </summary>
    public static TrayGlide? TakenUp(System.Numerics.Vector3 was, System.Numerics.Vector3 home, bool vertical, long now, int ms)
    {
        var across = vertical ? was.X - home.X : was.Y - home.Y;
        var along = vertical ? was.Y - home.Y : was.X - home.X;
        return MathF.Abs(across) > 0.5f || along == 0f ? null : Settling(along, now, ms);
    }

    /// <summary>There at once, as a dragged icon follows the pointer.</summary>
    public void Jump(float target)
    {
        from = to = target;
        length = 0;
    }
}

/// <summary>
/// tray: the overflow a dragged icon keeps open, as Windows 11's: a drag of an icon on the bar opens
/// it at once, past the drag's threshold (a click opens nothing), even with nothing in it — then it is
/// one empty cell, somewhere to let the icon go —, and it stands open for the whole drag, so the icon
/// can be carried up into it and let go at a place of its choosing. When the drag ends, let go or
/// called off, it closes again if it was not open before the drag; one that was (the drag began in
/// it) is left as it is. It used to open only for an icon rested on the chevron for Windows' hover
/// time, never with nothing behind it, and close again once the icon was away from it.
/// </summary>
public sealed class TrayDragOverflow(bool wasOpen)
{
    // Whether it was asked to open: once a drag, so one closed by something else stays closed.
    private bool tried;

    /// <summary>Whether the overflow stood open when the drag began.</summary>
    public bool WasOpen { get; } = wasOpen;

    /// <summary>Whether the overflow standing open was opened by this drag.</summary>
    public bool Opened { get; private set; }

    /// <summary>
    /// The drag moved, the overflow open or not (<paramref name="open"/>), the bar the icon was taken
    /// from showing its chevron to open it from or not yet (<paramref name="chevron"/>; the chevron
    /// comes with the drag where nothing is hidden, a layout later): whether to open it now.
    /// </summary>
    public bool Opens(bool open, bool chevron)
    {
        if (Opened && !open)
        {
            // Closed by something else (a key, another window): opened by this drag no longer.
            Opened = false;
        }
        if (open || WasOpen || tried || !chevron)
        {
            return false;
        }
        tried = Opened = true;
        return true;
    }

    /// <summary>The drag ended, let go anywhere or called off: whether the overflow it opened closes.</summary>
    public bool Ended()
    {
        var close = Opened;
        Opened = false;
        return close;
    }
}

/// <summary>
/// tray: where the window that carries a dragged icon stands (Dock.Tray.cs, TrayCarry): its icon's
/// middle under the pointer, wherever on the screen that is — across the bar and up into the
/// overflow alike, as Windows 11's tray icons follow the pointer. The icon used to move only along
/// the bar, inside its tray.
/// </summary>
public static class TrayCarrying
{
    /// <summary>The top left corner, in screen px, of a <paramref name="size"/> px square held by its middle at <paramref name="pointer"/>.</summary>
    public static POINT Corner(POINT pointer, int size) =>
        new() { x = pointer.x - Math.Max(0, size) / 2, y = pointer.y - Math.Max(0, size) / 2 };
}
