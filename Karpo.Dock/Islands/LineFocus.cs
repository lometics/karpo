// keys: what the keyboard stops at inside a line of a folder (docs\widgets.md, "One keyboard"). A line's
// stops are its hit targets: every part a click on it reaches — an item, a button on an item, a stepper,
// a transport button, a day — found where the line's own hit test (CellIsland.CardTarget) answers, over
// the whole of its laid-out content, a scrolled line's items out of sight included. Nothing to declare
// and nothing to keep in step: what the pointer reaches, the keys reach, and pressed by the keys a stop
// does what a click on it does (the folder's Activate, Click(target)).
//
// A line that walks its own interior — a calendar's days, a list with its own editor, a page of
// controls, a text field — says so (CellIsland.WalksItsOwnKeys) and is one stop, its own keys first.
// Where such a line lays its targets out as one list and some stand beside what its own keys reach (a
// calendar's arrows, a Join: CellIsland.OwnKeysReach), those are stops of their own and its own are
// one more, target 0, where they stand together (§471).
//
// The stops stand in the line's content: px from its box's top-left, unscrolled. Where they show is
// Shown; a stop out of sight is brought into it by Reveal, as a row chosen by the keys always was
// (IListScroll.ScrollReveal).

using System.Numerics;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock;

/// <summary>One stop of a line: target <paramref name="Target"/> of its click, and where it stands in its content (px).</summary>
internal readonly record struct FocusPart(int Target, D2D_RECT_F Box);

/// <summary>
/// A line that lays its targets out as one list as it draws (a headline's band, a message's): its hit test is
/// the first of them a point is in (<see cref="LineFocus.At"/>), and its stops are found in one layout rather
/// than asked point by point.
/// </summary>
internal interface ILineTargets
{
    /// <summary>The line's targets laid out in <paramref name="box"/>, the first a point is in its target; none but 0.</summary>
    IReadOnlyList<(int Target, D2D_RECT_F Box)> LineTargets(D2D_RECT_F box, in TileStyle style);
}

/// <summary>A line's stops, where they show, and bringing one into sight (see the top of the file).</summary>
internal static class LineFocus
{
    /// <summary>How finely the line is asked across, DIP: under half the narrowest button (a line button's reach is 32).</summary>
    public const float AcrossDip = 8;

    /// <summary>How finely the line is asked down, DIP: under half the shortest item (a compact row is 32).</summary>
    public const float DownDip = 4;

    /// <summary>
    /// The stops of <paramref name="line"/> drawn in <paramref name="box"/> on <paramref name="surface"/>,
    /// in reading order: every target but 0 its hit test answers, each the band it answers over. None for a
    /// line not set up, not on this PC, too small for its card, or that walks its own keys.
    /// </summary>
    public static List<FocusPart> Parts(CellIsland line, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        // A line that walks its own keys is one stop, unless it lays its targets out as one list and some of them
        // stand beside its own (a calendar's arrows beside its days).
        if ((line.WalksItsOwnKeys && line is not ILineTargets) || !line.DrawsCard(box, style))
        {
            return [];
        }
        var laid = line.Scrolled(box, style, surface);
        // The content whole: the scrolled line's from its top as if not scrolled, the head and the foot where
        // they stand at its ends; the line's own box where nothing scrolls.
        var content = laid.Scrolls ? laid.Still : laid.Box;
        if (line is ILineTargets listed)
        {
            // Each target once, its own box, the band a point in it is answered by.
            var each = listed.LineTargets(content, style).Where(target => target.Target != 0).DistinctBy(target => target.Target).ToList();
            if (line.WalksItsOwnKeys)
            {
                // §471: the targets its own keys reach are one stop, target 0, where they stand together; the
                // others are stops of their own beside it. None of its own: the line is one stop.
                var own = each.Where(target => line.OwnKeysReach(target.Target)).ToList();
                if (own.Count == each.Count)
                {
                    return [];
                }
                each = [.. each.Where(target => !line.OwnKeysReach(target.Target))];
                if (own.Count > 0)
                {
                    each.Add((0, own.Select(target => target.Box).Aggregate(Union)));
                }
            }
            return [.. FocusNav.ReadingOrder([.. each.Select(target => Rect(target.Box))], style.Px(DownDip)).Select(index => new FocusPart(each[index].Target, each[index].Box))];
        }
        var across = MathF.Max(1, style.Px(AcrossDip));
        var down = MathF.Max(1, style.Px(DownDip));
        var found = new Dictionary<int, (float Left, float Top, float Right, float Bottom)>();
        for (var y = content.top + (down / 2); y < content.bottom; y += down)
        {
            for (var x = content.left + (across / 2); x < content.right; x += across)
            {
                var target = line.CardTarget(new Vector2(x, y), content, style, surface);
                if (target == 0)
                {
                    continue;
                }
                found[target] = found.TryGetValue(target, out var was)
                    ? (MathF.Min(was.Left, x), MathF.Min(was.Top, y), MathF.Max(was.Right, x), MathF.Max(was.Bottom, y))
                    : (x, y, x, y);
            }
        }
        // Each sample stands for the cell around it.
        var styled = style;
        var parts = found.Select(entry => new FocusPart(entry.Key, new D2D_RECT_F(
            MathF.Max(content.left, entry.Value.Left - (across / 2)), MathF.Max(content.top, entry.Value.Top - (down / 2)),
            MathF.Min(content.right, entry.Value.Right + (across / 2)), MathF.Min(content.bottom, entry.Value.Bottom + (down / 2)))))
            // A target answered over more than its control (the volume's line's room, its ">"'s: §605) is ringed round the control.
            .Select(part => line.StopBox(part.Target, content, styled) is { } own ? part with { Box = own } : part).ToList();
        var order = FocusNav.ReadingOrder([.. parts.Select(part => Rect(part.Box))], down);
        return [.. order.Select(index => parts[index])];
    }

    /// <summary>
    /// Where <paramref name="part"/> of <paramref name="line"/> shows in <paramref name="box"/> as the line is
    /// scrolled now: a head's and a foot's where they stay, an item's moved with the items.
    /// </summary>
    public static D2D_RECT_F Shown(CellIsland line, FocusPart part, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var laid = line.Scrolled(box, style, surface);
        if (!laid.Scrolls)
        {
            return part.Box;
        }
        var top = part.Box.top - box.top;
        var shift = top < laid.Head ? 0
            : laid.Foot > 0 && top >= laid.Content - laid.Foot - 0.5f ? box.bottom - (box.top + laid.Content)
            : -laid.Offset;
        return part.Box with { top = part.Box.top + shift, bottom = part.Box.bottom + shift };
    }

    /// <summary>
    /// <paramref name="part"/> brought into sight in <paramref name="box"/>, scrolling the line's items as little
    /// as it takes (<see cref="ListScrolled.Revealing"/>); nothing for a head's or a foot's, which always show.
    /// </summary>
    public static void Reveal(CellIsland line, FocusPart part, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var laid = line.Scrolled(box, style, surface);
        if (!laid.Scrolls)
        {
            return;
        }
        var top = part.Box.top - box.top;
        var bottom = part.Box.bottom - box.top;
        if (top < laid.Head || (laid.Foot > 0 && top >= laid.Content - laid.Foot - 0.5f))
        {
            return;
        }
        line.ListScroll = laid.Revealing(top, bottom);
    }

    /// <summary>The page down (<paramref name="down"/>) or up from <paramref name="at"/> of <paramref name="parts"/>: the stop a line's window further that way, the last or the first at the most.</summary>
    public static int Page(IReadOnlyList<FocusPart> parts, int at, float window, bool down)
    {
        if (parts.Count == 0)
        {
            return -1;
        }
        if (at < 0 || at >= parts.Count)
        {
            return down ? parts.Count - 1 : 0;
        }
        var goal = parts[at].Box.top + (down ? window : -window);
        var best = down ? parts.Count - 1 : 0;
        for (var index = 0; index < parts.Count; index++)
        {
            var top = parts[index].Box.top;
            if (down ? top >= goal - 0.5f && index > at : top <= goal + 0.5f && index < at)
            {
                best = down ? Math.Min(best, index) : Math.Max(best, index);
            }
        }
        return best;
    }

    /// <summary>The target of <paramref name="targets"/> <paramref name="point"/> is in, the first that holds it; 0 for none (<see cref="ILineTargets"/>).</summary>
    public static int At(IReadOnlyList<(int Target, D2D_RECT_F Box)> targets, Vector2 point)
    {
        foreach (var (target, box) in targets)
        {
            if (LineRows.Inside(box, point))
            {
                return target;
            }
        }
        return 0;
    }

    /// <summary>The box round both <paramref name="one"/> and <paramref name="other"/>.</summary>
    private static D2D_RECT_F Union(D2D_RECT_F one, D2D_RECT_F other) =>
        new(MathF.Min(one.left, other.left), MathF.Min(one.top, other.top), MathF.Max(one.right, other.right), MathF.Max(one.bottom, other.bottom));

    /// <summary>A part's box as the navigation's rectangle.</summary>
    public static RectF Rect(D2D_RECT_F box) => new(box.left, box.top, box.right - box.left, box.bottom - box.top);
}
