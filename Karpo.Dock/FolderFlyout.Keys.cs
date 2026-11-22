// keys: the open folder at rest (docs\widgets.md, "One keyboard"). The keyboard is on a line, and on one
// of its stops where it has them (LineFocus: the targets its click reaches — an item, a button on it, a
// stepper); the ring goes round that stop.
//
//   arrows               the nearest stop that way in the line (FocusNav.Toward); past its edge, the line
//                        beside, on its stop nearest that way. A line that walks its own keys takes
//                        them first (CellIsland.KeyOn), and lets the folder have the ones it leaves.
//   Tab, Shift+Tab       the next line, the one before, round, on its first stop
//   Home, End            the line's first, its last stop; again, the folder's first, its last line
//   Page Up, Page Down   a window of a scrolling line's items further, up or down
//   Enter, Space         what a click on the stop does (Activate, Click(target))
//   Menu key, Shift+F10  the line's menu, as a right click on it opens
//
// A stop out of sight in a scrolling line is scrolled into it (LineFocus.Reveal). While the folder is
// arranged the keys are edit mode's (EditKeys), and the lines are walked whole.
//
// §471: a line that walks its own keys has them while the keys are on it whole or on its own stop
// (target 0, beside the stops of the folder's it may have: a calendar's arrows), never on those. One ring
// shows: where such a line rings what its keys are on, the folder draws none (FocusRing); the keys gone
// from its own, it is told (CellIsland.OwnKeys) and puts its ring out.

using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

public sealed unsafe partial class FolderFlyout
{
    // keys: the stop the keys are on in the line they are on, by that line's key so no rebuild
    // carries it to another line; none while they are on the line whole.
    private (string Line, FocusPart Part)? keyPart;

    /// <summary>keys: the stop of the focused line the keys are on; null where they are on the line whole.</summary>
    private FocusPart? KeyPart =>
        keyPart is { } held && focused >= 0 && focused < tiles.Count && string.Equals(tiles[focused].Island.Key, held.Line, StringComparison.OrdinalIgnoreCase)
            ? held.Part
            : null;

    /// <summary>Tests: the line and the target the keys are on (0: the line whole); (-1, 0) where they are on nothing.</summary>
    internal (int Line, int Target) KeyedOn => (focused >= 0 && focused < tiles.Count ? focused : NoTarget, KeyPart?.Target ?? 0);

    /// <summary>Tests: where the ring stands round the stop the keys are on, panel px; null where they are on a line whole.</summary>
    internal D2D_RECT_F? KeyRingBox => KeyRing()?.Box;

    /// <summary>Tests: line <paramref name="index"/>'s stops, as the keys walk them.</summary>
    internal List<FocusPart> StopsOfLine(int index) => StopsOf(index);

    /// <summary>
    /// keys: the folder takes the keys from the keyboard that opened it (the bar's Enter, Dock.Keys.cs): on
    /// its first stop, the ring showing.
    /// </summary>
    internal void TakeKeys()
    {
        if (!IsOpen || plan is null)
        {
            return;
        }
        var order = Order();
        if (order.Count == 0)
        {
            return;
        }
        KeysOnto(order[0], first: true);
        keyboard = true;
        ShowStates();
        UiaFocusMoved();
    }

    /// <summary>keys: the space line <paramref name="index"/>'s stops stand in; null for a line that has none (arranged, a list's row, not a card).</summary>
    private (CellIsland Line, D2D_RECT_F Box, TileStyle Style)? StopSpace(int index)
    {
        if (editing || plan is not { } shown || index < 0 || index >= tiles.Count || index >= shown.Cards.Count
            || !DrawsOwnCard(index) || tiles[index].Island is not CellIsland cells)
        {
            return null;
        }
        var (box, _, style) = CardSpace(shown, index, default);
        return (cells, box, style);
    }

    // keys: each line's stops as last found, by its key and the size it was found at; forgotten as the folder
    // changes (FolderChanged) and closes, so a key pressed again and again asks the line once.
    private readonly Dictionary<string, (float Width, float Height, List<FocusPart> Stops)> stopsKept = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>keys: line <paramref name="index"/>'s stops (LineFocus.Parts); none where the line is one stop.</summary>
    private List<FocusPart> StopsOf(int index)
    {
        if (StopSpace(index) is not { } at)
        {
            return [];
        }
        var key = at.Line.Key;
        var (width, height) = (at.Box.right - at.Box.left, at.Box.bottom - at.Box.top);
        if (stopsKept.TryGetValue(key, out var kept) && kept.Width == width && kept.Height == height)
        {
            return kept.Stops;
        }
        try
        {
            var stops = LineFocus.Parts(at.Line, at.Box, at.Style, WidgetSurface.List);
            stopsKept[key] = (width, height, stops);
            return stops;
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Console.Error.WriteLine($"Karpo: a folder's {at.Line.Info.Id} stops failed: {error.Message}");
            return [];
        }
    }

    /// <summary>keys: where <paramref name="part"/> of line <paramref name="index"/> shows, panel px, mirrored as the line is; empty where it does not.</summary>
    private D2D_RECT_F PartOnPanel(int index, FocusPart part)
    {
        if (StopSpace(index) is not { } at || plan is not { } shown)
        {
            return default;
        }
        var card = shown.Cards[index];
        var box = LineFocus.Shown(at.Line, part, at.Box, at.Style, WidgetSurface.List);
        var width = card.right - card.left;
        var (left, right) = Mirror.On ? (width - box.right, width - box.left) : (box.left, box.right);
        return new D2D_RECT_F(
            MathF.Max(card.left, card.left + left), MathF.Max(card.top, card.top + box.top),
            MathF.Min(card.right, card.left + right), MathF.Min(card.bottom, card.top + box.bottom));
    }

    /// <summary>
    /// keys: the keys on line <paramref name="index"/>, on <paramref name="part"/> of it (null: the line whole),
    /// that stop brought into sight in its line.
    /// </summary>
    private void KeysTo(int index, FocusPart? part)
    {
        var ownWas = OwnKeyed();
        focused = index;
        keyPart = part is { } on && index >= 0 && index < tiles.Count ? (tiles[index].Island.Key, on) : null;
        // The line told which of its targets the keys are on, the others that they are on none; one that
        // lights what the keys are on drawn again.
        for (var line = 0; line < tiles.Count; line++)
        {
            if (tiles[line].Island is CellIsland cells && (line == index ? part?.Target ?? 0 : 0) is var keyed && cells.KeyedPart != keyed)
            {
                cells.KeyedPart = keyed;
                if (cells.LightsKeyedPart && plan is { } drawn && line < drawn.Cards.Count)
                {
                    DrawCard(drawn, line);
                }
            }
        }
        // §471: a line whose own keys the keys left puts its ring out; one they came onto rings what they are on.
        if (OwnKeyed() is var ownNow && !ReferenceEquals(ownWas, ownNow))
        {
            ownWas?.OwnKeys(false);
            ownNow?.OwnKeys(true);
        }
        if (part is { } shown && StopSpace(index) is { } at && plan is { } laid)
        {
            var was = at.Line.ListScroll;
            LineFocus.Reveal(at.Line, shown, at.Box, at.Style, WidgetSurface.List);
            if (MathF.Abs(at.Line.ListScroll - was) > 0.01f)
            {
                StopGlide();
                DrawCard(laid, index);
            }
        }
    }

    /// <summary>
    /// keys: the line whose own keys the keys are on — a line that walks them, the keys on it whole or on its own
    /// stop (target 0) rather than on one beside them; null for none (at rest only).
    /// </summary>
    private CellIsland? OwnKeyed() =>
        !editing && focused >= 0 && focused < tiles.Count && tiles[focused].Island is CellIsland { WalksItsOwnKeys: true } cells
            && KeyPart is not { Target: not 0 }
            ? cells
            : null;

    /// <summary>
    /// keys: <paramref name="line"/>'s own keys took a key: what they moved (a day picked, the hours scrolled) moves
    /// its stops beside them too (a Join), so they are found again, and the keys stay on its own stop.
    /// </summary>
    private void OwnKeysMoved(CellIsland line)
    {
        if (!line.WalksItsOwnKeys || line is not ILineTargets || focused < 0 || focused >= tiles.Count || !ReferenceEquals(tiles[focused].Island, line))
        {
            return;
        }
        stopsKept.Remove(line.Key);
        if (KeyPart is not null)
        {
            var own = StopsOf(focused).FindIndex(stop => stop.Target == 0);
            KeysTo(focused, own >= 0 ? StopsOf(focused)[own] : null);
        }
    }

    /// <summary>Tests: the keys onto stop <paramref name="target"/> of line <paramref name="index"/> (0: its own, or the line whole), in use.</summary>
    internal void KeysOnStop(int index, int target)
    {
        var stops = StopsOf(index);
        var at = stops.FindIndex(stop => stop.Target == target);
        KeysTo(index, at >= 0 ? stops[at] : null);
        keyboard = true;
        ShowStates();
    }

    /// <summary>keys: the keys onto line <paramref name="index"/>, its first stop (or its last, <paramref name="first"/> false).</summary>
    private void KeysOnto(int index, bool first)
    {
        var stops = StopsOf(index);
        KeysTo(index, stops.Count == 0 ? null : stops[first ? 0 : ^1]);
    }

    /// <summary>
    /// keys: onto line <paramref name="index"/> coming <paramref name="way"/> from <paramref name="from"/> (panel px):
    /// its stop nearest that way — down into its first, up into its last, across into the one beside.
    /// </summary>
    private void KeysArrive(int index, FocusWay way, D2D_RECT_F from)
    {
        var stops = StopsOf(index);
        if (stops.Count == 0)
        {
            KeysTo(index, null);
            return;
        }
        if (way is FocusWay.Down or FocusWay.Up)
        {
            KeysTo(index, stops[way == FocusWay.Down ? 0 : ^1]);
            return;
        }
        var rects = new List<RectF> { LineFocus.Rect(from) };
        rects.AddRange(stops.Select(stop => LineFocus.Rect(PartOnPanel(index, stop))));
        var nearest = FocusNav.Toward(rects, 0, way);
        KeysTo(index, stops[nearest > 0 ? nearest - 1 : 0]);
    }

    /// <summary>
    /// keys: a key at rest that no line took (see the top of the file); default for one that is not the
    /// folder's. The folder's lines in reading order are <paramref name="order"/>.
    /// </summary>
    private FolderFlyoutEvent KeyAtRest(int key, bool shift, List<int> order, FolderPlan shown)
    {
        var line = focused >= 0 && focused < shown.Cards.Count ? focused : NoTarget;
        var part = line >= 0 ? KeyPart : null;
        // A slider the keys are on steps: Left and Right a step (mirrored, the other way), Page Up and Down ten,
        // Home and End to its ends, as Windows' sliders do; Up and Down still walk.
        var steps = key switch
        {
            VK.VK_LEFT => Mirror.On ? 1 : -1,
            VK.VK_RIGHT => Mirror.On ? -1 : 1,
            VK.VK_PRIOR => 10,
            VK.VK_NEXT => -10,
            VK.VK_HOME => -1000,
            VK.VK_END => 1000,
            _ => 0,
        };
        if (steps != 0 && part is { } slider && StopSpace(line) is { } sliding && sliding.Line.CardStep(slider.Target, steps, Screen()))
        {
            keyboard = true;
            ShowStates();
            return default;
        }
        switch (key)
        {
            case VK.VK_TAB:
            {
                if (order.Count == 0)
                {
                    return default;
                }
                var at = order.IndexOf(focused);
                KeysOnto(order[FocusNav.Step(order.Count, at, shift ? -1 : 1)], first: true);
                break;
            }
            case VK.VK_LEFT or VK.VK_RIGHT or VK.VK_UP or VK.VK_DOWN:
            {
                // Mirrored, the line's stops stand the other way across (Mirror.cs).
                var way = FocusNav.WayOf(key)!.Value;
                var inLine = Mirror.On ? way switch { FocusWay.Left => FocusWay.Right, FocusWay.Right => FocusWay.Left, _ => way } : way;
                if (line < 0)
                {
                    if (order.Count > 0)
                    {
                        KeysOnto(order[0], first: true);
                    }
                    break;
                }
                var stops = StopsOf(line);
                if (part is null && stops.Count > 0)
                {
                    // On a line whole that has stops (the pointer left the keys there): into them.
                    KeysOnto(line, first: way is FocusWay.Down or FocusWay.Right);
                    break;
                }
                if (part is { } on && stops.FindIndex(stop => stop.Target == on.Target) is var at and >= 0
                    && FocusNav.Toward([.. stops.Select(stop => LineFocus.Rect(stop.Box))], at, inLine) is var next and >= 0)
                {
                    KeysTo(line, stops[next]);
                    break;
                }
                var beside = Nearest(line, key);
                if (beside != line)
                {
                    KeysArrive(beside, way, part is { } from ? PartOnPanel(line, from) : shown.Cards[line]);
                }
                break;
            }
            case VK.VK_HOME or VK.VK_END:
            {
                var home = key == VK.VK_HOME;
                var stops = line >= 0 ? StopsOf(line) : [];
                var end = stops.Count == 0 ? (FocusPart?)null : stops[home ? 0 : ^1];
                if (end is { } there && part?.Target != there.Target)
                {
                    KeysTo(line, there);
                }
                else if (order.Count > 0)
                {
                    KeysOnto(home ? order[0] : order[^1], first: home);
                }
                break;
            }
            case VK.VK_PRIOR or VK.VK_NEXT when line >= 0 && part is { } on && StopSpace(line) is { } space:
            {
                var stops = StopsOf(line);
                var laid = space.Line.Scrolled(space.Box, space.Style, WidgetSurface.List);
                var window = laid.Window.bottom - laid.Window.top;
                var at = stops.FindIndex(stop => stop.Target == on.Target);
                if (LineFocus.Page(stops, at, MathF.Max(1, window), down: key == VK.VK_NEXT) is var paged and >= 0)
                {
                    KeysTo(line, stops[paged]);
                }
                break;
            }
            case VK.VK_RETURN or VK.VK_SPACE:
                return line == NoTarget ? default : Activate(line, null, part?.Target);
            // The menu key and Shift+F10: the line's menu, as a right click on it opens it, at the stop.
            case VK.VK_APPS:
            case VK.VK_F10 when shift:
            {
                if (line < 0 || folder is { Paged: true })
                {
                    return default;
                }
                host.StaysOpen = true;
                var at = part is { } on ? PartOnPanel(line, on) : shown.Cards[line];
                return new FolderFlyoutEvent(FolderFlyoutAction.Context, tiles[line].Island, line, At: OnScreen(at));
            }
            default:
                return default;
        }
        keyboard = true;
        ShowStates();
        UiaFocusMoved();
        return default;
    }

    /// <summary>keys: the ring's box and corner for the stop the keys are on, panel px; null where they are on a line whole.</summary>
    private (D2D_RECT_F Box, float Radius)? KeyRing()
    {
        if (KeyPart is not { } part || PartOnPanel(focused, part) is var box && (box.right <= box.left || box.bottom <= box.top))
        {
            return null;
        }
        return (box, MathF.Min(scale * LineCorner.ControlDip, (box.bottom - box.top) / 2));
    }
}
