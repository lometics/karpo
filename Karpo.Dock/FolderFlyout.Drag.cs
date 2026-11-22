// edit: a line carried in the arranged folder, moved as the bar moves a block it carries (the
// person, 2026-10-12: "the editing of the list views should be made as smooth as the taskbar, with
// that i mean the reordering specifically"; Scene.Apply, Dock.EditCarry.cs).
//
//  - Past the drag threshold the line is lifted: taken out of its list onto a raised plate of the
//    panel's own colour with a shadow, over everything, and it follows the pointer from then on —
//    every pixel, by its offset alone, nothing drawn again.
//  - The other lines give way: each glides to where it would stand were the line let go where it
//    is now (FolderDrag: the folder planned again with the move made), in the bar's time and with
//    its easing, in its own list and in another, the cards and panels gliding with them. Where it
//    would land a ghost of its size stands, as on the bar.
//  - Let go, it settles: the lines glide from where they showed to where the folder lays them out
//    now, the carried one on its plate, which sinks into the list as it lands. Any other change of
//    the arranged folder's layout glides the same way: a line moved by the keys or a menu, one
//    removed or added.
//
// The carried line's visuals are moved, not drawn again: its card and its content go onto the
// plate, and back into their list when it is down.

using System.Numerics;
using Karpo.Core;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using Windows.UI.Composition;
using static TerraFX.Interop.Windows.Windows;
using Color = Windows.UI.Color;

namespace Karpo.Dock;

public sealed unsafe partial class FolderFlyout
{
    /// <summary>edit: a settle has run its course: the lines it lifted go back into their lists (<see cref="SettleDone"/>).</summary>
    internal const nuint SettleTimer = 17;

    // edit: where the carried line was taken hold of, from its top left corner (panel px), and where
    // the pointer carrying it last was.
    private Vector2 grab;
    private Vector2? dragPointer;

    // edit: where the carried line would land now, as the pointer has it — its own place too — or
    // null where it would land nowhere among the lines (away, or a new list); and the plan the lines
    // stand by meanwhile (FolderDrag), made for that place.
    private FolderPlace? aimed;
    private FolderPlan? arranged;
    private string arrangedFor = "";
    private IReadOnlyList<float>? dragNatural;

    // edit: how far each line stands from its place while the lines give way (panel px); null at rest.
    private Vector2[]? shifts;

    // edit: the cards of a list are one card while lines glide across them: each card's clip is its
    // list's, so a line crossing a divider is not cut by the card it leaves.
    private bool unruled;

    // edit: the lines lifted over everything — the carried one, and while a settle runs those it
    // glides into place from another list — and plates made before, to use again.
    private readonly List<Raised> raised = [];
    private readonly List<Raised> spareRaised = [];

    // edit: where the lines showed when the carried one was let go, for the layout the move brings (FolderChanged).
    private Snapshot? dropped;

    // edit: the ghost where the carried line would land, and what everything glides with.
    private ShapeVisual? ghostVisual;
    private CompositionRoundedRectangleGeometry? ghostBox;

    // §621: the add page's ghost's more rectangles, after ghostBox's, in ghostVisual: one a line of a ghost of several.
    private readonly List<(CompositionSpriteShape Shape, CompositionRoundedRectangleGeometry Box)> ghostMore = [];
    private Vector3KeyFrameAnimation? slide3;
    private Vector2KeyFrameAnimation? slide2;
    private ScalarKeyFrameAnimation? sink;
    private CompositionEasingFunction? decelerate;

    /// <summary>A line lifted over the folder: what goes with the pointer, its shadow, and its plate under the line's own visuals.</summary>
    private sealed class Raised
    {
        public required ContainerVisual Carrier;
        public required SpriteVisual Caster;
        public required ShapeVisual ShadowSource;
        public required CompositionRoundedRectangleGeometry ShadowBox;
        public required CompositionVisualSurface ShadowSurface;
        public required ContainerVisual Body;
        public required CompositionRoundedRectangleGeometry BodyClip;
        public required ShapeVisual Plate;
        public required CompositionRoundedRectangleGeometry PlateBox;
        public required CompositionSpriteShape PlateShape;
        public int Index = NoTarget;
        public ContainerVisual? Home;
        // The card's own view, where its content runs under the rim in another (CellIsland.UnderRim).
        public ContainerVisual? CardHome;
        public Vector2 At;

        // §502: a spacer's stripes (§524), carried on the carrier where they stand on its card, and where they go back to.
        public ShapeVisual? Mark;
        public ContainerVisual? MarkHome;

        /// <summary>§357: what was made for it, the folder's: kept for the next line lifted, closed as the look changes or the folder goes.</summary>
        public readonly List<IDisposable> Owned = [];
    }

    /// <summary>
    /// Where every line showed at one moment, on the screen (top left corner, px), by its widget's
    /// key, with its list; which was carried; and the frame they stood in — the panels (panel px),
    /// each list's clip, and where the panel stood on the screen.
    /// </summary>
    private sealed record Snapshot(
        Dictionary<string, (Vector2 At, int List)> Lines,
        IReadOnlySet<string> Carried,
        IReadOnlyList<D2D_RECT_F>? Pieces,
        IReadOnlyList<(Vector2 Offset, Vector2 Size)> Clips,
        IReadOnlyDictionary<string, Vector3> Adds,
        Vector2 Origin);

    /// <summary>Tests only: whether a line is lifted over the folder now, and where its top left corner stands (panel px).</summary>
    internal (bool Lifted, Vector2 At) LiftedLine => Carried() is { } lifted ? (true, lifted.At) : (false, default);

    /// <summary>Tests only: where line <paramref name="index"/> stands now, top left corner (panel px): given way, lifted, or at its place.</summary>
    internal Vector2 LineAt(int index)
    {
        if (plan is not { } shown || index < 0 || index >= shown.Cards.Count)
        {
            return default;
        }
        if (RaisedOf(index) is { } lifted)
        {
            return lifted.At;
        }
        var rest = shown.Cards[index];
        return new Vector2(rest.left, rest.top) + ShiftOf(index);
    }

    /// <summary>Tests only: the plan the lines stand by while one is carried (<see cref="arranged"/>), in its own px; null at rest.</summary>
    internal FolderPlan? ArrangedPlan => shifts is not null ? arranged : null;

    /// <summary>Tests only: where a carried line would land as the lines show it (its own place too); null where nowhere.</summary>
    internal FolderPlace? Aimed => aimed;

    /// <summary>Tests only: whether lines glide or are lifted after a drop (a settle runs).</summary>
    internal bool Settling => raised.Count > 0 && moving < 0 || unruled && shifts is null;

    /// <summary>edit: where the pointer took hold of line <paramref name="index"/> at <paramref name="point"/> (panel px), from its top left corner.</summary>
    private void TakeHold(int index, Vector2 point)
    {
        if (plan is { } shown && index >= 0 && index < shown.Cards.Count)
        {
            var at = shown.Cards[index];
            grab = point - new Vector2(at.left, at.top);
        }
    }

    /// <summary>edit: line <paramref name="index"/> is taken hold of in its middle — one carried with no button held (<see cref="Carry"/>).</summary>
    private void TakeHoldInMiddle(int index)
    {
        if (plan is { } shown && index >= 0 && index < shown.Cards.Count)
        {
            var card = shown.Cards[index];
            grab = new Vector2((card.right - card.left) / 2, (card.bottom - card.top) / 2);
        }
    }

    /// <summary>
    /// edit: the carried line follows the pointer at <paramref name="point"/> (panel px): lifted the
    /// first time, then only moved. Kept where the window shows it: off the folder it waits at the edge.
    /// </summary>
    private void Follow(Vector2 point)
    {
        if (plan is not { } shown || moving < 0 || moving >= tiles.Count || moving >= cards.Count || !editing)
        {
            return;
        }
        var lifted = Carried();
        if (lifted is null)
        {
            // A settle still under way ends where it is going: this drag starts from the lines at rest.
            Settled();
            dropped = null;
            dragNatural = folder is { } island ? Natural(island) : null;
            shifts = new Vector2[tiles.Count];
            unruled = true;
            lifted = Raise(moving, Grabbed(shown, point), plate: true);
            if (lifted is null)
            {
                shifts = null;
                unruled = false;
                return;
            }
            // Lines carried with it, each on its plate, stacked under and over it (FolderFlyout.Pending.cs).
            RaiseGroup(shown, Grabbed(shown, point));
        }
        dragPointer = point;
        var card = shown.Cards[moving];
        var size = new Vector2(card.right - card.left, card.bottom - card.top);
        // Lines carried together are kept in as one stack.
        var (_, over, under) = StackOf(shown);
        var room = Visible();
        var at = Grabbed(shown, point);
        at = new Vector2(
            Math.Clamp(at.X, room.left, MathF.Max(room.left, room.right - size.X)),
            Math.Clamp(at.Y, room.top - over, MathF.Max(room.top - over, room.bottom - under)));
        lifted.At = at;
        Try(() =>
        {
            lifted.Carrier.StopAnimation("Offset");
            lifted.Carrier.Offset = new Vector3(at, 0);
            // A little see-through, as a block of the bar carried; out of the folder fainter, where
            // nothing would take it (EditLook.cs).
            lifted.Carrier.Opacity = dragging >= 0 ? EditLook.CarriedOutOpacity : EditLook.LiftOpacity;
            FollowGroup(shown, at, lifted.Carrier.Opacity);
        });
    }

    /// <summary>Where the carried line's top left corner goes for the pointer at <paramref name="point"/> (panel px).</summary>
    private Vector2 Grabbed(FolderPlan shown, Vector2 point) => point - grab;

    /// <summary>
    /// edit: the lines give way for the carried ones where they would land now (<see cref="aimed"/>),
    /// or close up behind them where they would land nowhere among them; gliding there
    /// (<paramref name="animate"/>) or put there at once. The folder they stand by is the one layout
    /// with what is pending in it (<see cref="Layout"/>, <see cref="FolderPending"/>): the move made, or
    /// over no place — away, or a new list's slot — the carried lines in no list, their lists closed up
    /// on the bar's side (§230). Nothing until they are lifted.
    /// </summary>
    private void ShowArranged(bool animate)
    {
        if (plan is not { } shown || folder is not { } island || moving < 0 || moving >= tiles.Count || shifts is null || Carried() is null)
        {
            return;
        }
        var key = aimed is { } at ? $"{at.List}:{at.Index}" : "closed";
        if (arranged is null || key != arrangedFor)
        {
            arranged = Layout(island, Pending(aimed), dragNatural);
            arrangedFor = key;
        }
        var laid = arranged;
        var down = FolderDrag.Down(shown, laid, shownEdge);
        Try(() =>
        {
            for (var index = 0; index < tiles.Count && index < shown.Cards.Count && index < laid.Cards.Count && index < shifts.Length; index++)
            {
                if (index == moving || RaisedOf(index) is not null)
                {
                    continue;
                }
                var rest = shown.Cards[index];
                var to = FolderDrag.At(shown, laid, index, shownEdge);
                var restAt = new Vector2(rest.left, rest.top);
                Shift(index, to - restAt, animate);
            }
            // The ghost where the carried line would land: in the gap the others open, its own size;
            // in an empty slot, on the bar's side of it, a medium list's width (SlotGhost).
            if (ghostVisual is not null && ghostBox is not null)
            {
                var slotted = SlotGhost(shown);
                var show = (aimed is not null || slotted is not null) && moving < laid.Cards.Count;
                ghostVisual.IsVisible = show;
                ShowMoreGhosts(0);
                if (show)
                {
                    // Lines carried together: one ghost round the room they all take, as each is drawn arranged.
                    var (first, taken) = GroupRoom(laid);
                    var card = slotted ?? taken;
                    var firstCard = first >= 0 && first < laid.Cards.Count ? laid.Cards[first] : taken;
                    var to = slotted is { } inSlot ? new Vector2(inSlot.left, inSlot.top)
                        : FolderDrag.At(shown, laid, first, shownEdge) + new Vector2(taken.left - firstCard.left, taken.top - firstCard.top);
                    // The bar's ghost's edge: the ring's thickness, inside it (EditLook.cs).
                    var edge = EditLook.Ring(EditLook.Hair(scale));
                    ghostVisual.Size = new Vector2(card.right - card.left, card.bottom - card.top);
                    var (offset, size, corner) = EditLook.Stroke(ghostVisual.Size, MathF.Min(shown.Radius, (card.bottom - card.top) / 2), edge);
                    ghostBox.Offset = offset;
                    ghostBox.Size = size;
                    ghostBox.CornerRadius = new Vector2(corner, corner);
                    ((CompositionSpriteShape)ghostVisual.Shapes[0]).StrokeThickness = edge;
                    var was = ghostVisual.Offset;
                    Glide(ghostVisual, ghostShown ? was : new Vector3(to, 0), new Vector3(to, 0), animate && ghostShown);
                }
                ghostShown = show;
            }
            // An empty list saying so would speak under a line coming into it.
            if (empty is not null)
            {
                empty.IsVisible = laid.Empty.Count == shown.Empty.Count;
            }
            ShowFrame(laid, down, animate);
            PlaceAdds(animate);
        });
    }

    /// <summary>
    /// edit: where the carried line's ghost stands in the empty slot it would become a list in
    /// (<see cref="landing"/>, <see cref="FolderSlot"/>): the slot's width — a new list is medium —
    /// the line's height, on the bar's side where the lists line up. Null where it lands in no slot.
    /// </summary>
    private D2D_RECT_F? SlotGhost(FolderPlan shown)
    {
        if (aimed is not null || landing is not { New: true } place || moving < 0 || moving >= shown.Cards.Count)
        {
            return null;
        }
        foreach (var slot in shown.Slots)
        {
            if (slot.Place == place)
            {
                var (_, over, under) = StackOf(shown);
                var tall = under - over;
                var top = PopupPaint.Round((shown.Height - tall) * Over(shownEdge));
                return PopupPaint.Rect(slot.Box.left, top, slot.Box.right - slot.Box.left, tall);
            }
        }
        return null;
    }

    /// <summary>
    /// edit: where "+" <paramref name="index"/> shows, <paramref name="rest"/> being its place at
    /// rest (panel px): while lines give way a list's own buttons stand where its arranged end has
    /// them. A slot's buttons stay put: the slot is a place to carry a line to, and one gliding away
    /// from under the pointer would take the place it offered with it.
    /// </summary>
    private Vector3 AddAt(int index, D2D_RECT_F rest)
    {
        // §357: the same button in the folder the lines give way in, by what it is: a list's warning come or
        // gone there leaves every other where it is.
        if (arranged is { } laid && shifts is not null && plan is { } shown && index < shown.Adds.Count
            && AddIndexOf(laid, AddKeyOf(shown.Adds[index])) is var same and >= 0 && laid.Adds[same].List >= 0)
        {
            // Up or down with its list's end; across where it stands at rest, which keeps an outer
            // empty list's button on what shows of it (WithSlots) — the columns do not move sideways.
            var by = laid.Adds[same].Box.top + FolderDrag.Down(shown, laid, shownEdge) - shown.Adds[index].Box.top;
            var box = KeepIn(Down(shown.Adds[index].Box, by), Visible());
            return new Vector3(rest.left, box.top, 0);
        }
        return new Vector3(rest.left, rest.top, 0);
    }

    /// <summary>edit: every "+" where it shows now (<see cref="AddAt"/>), gliding there where <paramref name="animate"/>.</summary>
    private void PlaceAdds(bool animate)
    {
        if (plan is not { } shown)
        {
            return;
        }
        for (var index = 0; index < addVisuals.Count && index < shown.Adds.Count; index++)
        {
            var sprite = addVisuals[index].Sprite;
            Glide(sprite, sprite.Offset, AddAt(index, AddBox(shown, index)), animate);
        }
    }

    // edit: whether the ghost stood somewhere already, so it glides from there rather than appearing.
    private bool ghostShown;

    /// <summary>
    /// edit: each list's clip, each card's and the panels where <paramref name="laid"/> has them,
    /// moved down by <paramref name="down"/> (<see cref="FolderDrag.Down"/>); the cards one card a
    /// list while <see cref="unruled"/>.
    /// </summary>
    private void ShowFrame(FolderPlan laid, float down, bool animate)
    {
        for (var list = 0; list < listShapes.Count && list < laid.Lists.Count; list++)
        {
            var box = laid.Lists[list].Box;
            Reshape(listShapes[list], new Vector2(box.left, box.top + down), new Vector2(box.right - box.left, box.bottom - box.top), animate);
        }
        foreach (var (_, shape, list, run, rim) in runViews)
        {
            if (list >= laid.Lists.Count)
            {
                continue;
            }
            var box = unruled || run >= laid.Lists[list].Runs.Count ? laid.Lists[list].Box : laid.Lists[list].Runs[run];
            var (offset, size) = CardClip(box, down, rim);
            Reshape(shape, offset, size, animate);
        }
        var pieces = laid.Pieces.Select(piece => Down(piece, down)).ToList();
        host.SetPieces(pieces, glide: animate && look.Animations);
    }

    /// <summary>edit: line <paramref name="index"/> stands <paramref name="by"/> px from its place: its card, its content and a spacer's stripes.</summary>
    private void Shift(int index, Vector2 by, bool animate)
    {
        if (shifts is null || index >= shifts.Length || plan is not { } shown || RaisedOf(index) is not null)
        {
            return;
        }
        var was = shifts[index];
        if (was == by)
        {
            return;
        }
        shifts[index] = by;
        var rest = shown.Cards[index];
        var home = new Vector2(rest.left, rest.top);
        if (index < cards.Count)
        {
            Glide(cards[index], new Vector3(home + was, 0), new Vector3(home + by, 0), animate);
        }
        if (index < contents.Count)
        {
            Glide(contents[index].Sprite, new Vector3(home + was, 0), new Vector3(home + by, 0), animate);
        }
        if (index < bands.Count && bands[index].Visual.IsVisible)
        {
            var mark = bands[index].Visual;
            var markHome = mark.Offset - new Vector3(was, 0);
            Glide(mark, markHome + new Vector3(was, 0), markHome + new Vector3(by, 0), animate);
        }
    }

    /// <summary>How far line <paramref name="index"/> stands from its place now.</summary>
    private Vector2 ShiftOf(int index) => shifts is { } shifted && index >= 0 && index < shifted.Length ? shifted[index] : default;

    /// <summary>
    /// Where line <paramref name="index"/>'s visuals go in its list's view (panel px): its place
    /// <paramref name="at"/> moved by how far it gave way, or the plate's corner where it is lifted.
    /// What a line drawn again is put at (DrawCard), so a draw never snaps a line back mid-drag.
    /// </summary>
    private Vector3 LineOffset(int index, D2D_RECT_F at) =>
        RaisedOf(index) is not null ? Vector3.Zero : new Vector3(new Vector2(at.left, at.top) + ShiftOf(index), 0);

    /// <summary>
    /// After the lines were put at their places (PlaceLines): those giving way stand where they
    /// give way, the lifted ones on their plates, and the lists' frames as the lines have them.
    /// </summary>
    private void AfterPlaced()
    {
        if (plan is not { } shown || (shifts is null && raised.Count == 0 && !unruled))
        {
            return;
        }
        Try(() =>
        {
            for (var index = 0; index < cards.Count && index < shown.Cards.Count; index++)
            {
                var offset = LineOffset(index, shown.Cards[index]);
                cards[index].StopAnimation("Offset");
                cards[index].Offset = offset;
                if (index < contents.Count)
                {
                    contents[index].Sprite.StopAnimation("Offset");
                    contents[index].Sprite.Offset = offset;
                }
                // §502: a lifted spacer's stripes are on its carrier (Raise).
                if (index < bands.Count && RaisedOf(index) is null && bands[index].Visual.IsVisible && ShiftOf(index) is var by && by != default)
                {
                    bands[index].Visual.StopAnimation("Offset");
                    bands[index].Visual.Offset += new Vector3(by, 0);
                }
            }
            if (arranged is { } laid && shifts is not null)
            {
                ShowArranged(animate: false);
            }
            else if (unruled)
            {
                ShowFrame(shown, 0, animate: false);
            }
        });
    }

    /// <summary>
    /// edit: the carried line is let go: where every line shows is kept for the layout a move brings
    /// (<see cref="dropped"/>), the lines go back to their places, and they glide there from where
    /// they showed — the carried one on its plate, sinking. Where the folder is laid out again for
    /// the move, its new places are where they glide to (<see cref="FolderChanged"/>).
    /// </summary>
    private void Drop()
    {
        if (Carried() is null)
        {
            return;
        }
        var before = Take();
        dropped = before;
        Unlift();
        Settle(before);
    }

    /// <summary>§575: a line taken hold of, which Escape puts back first (<see cref="CancelMove"/>).</summary>
    internal bool Moving => editing && moving >= 0 && moving < tiles.Count;

    /// <summary>
    /// edit: the line taken hold of goes back where it was — Escape, a right press —, as a block of
    /// the bar does (DockWindow.CancelDrag, EditKeys.Escape): the lines it moved aside glide back, it
    /// settles into its own place, and nothing changes. False where no line was held.
    /// </summary>
    private bool CancelMove()
    {
        if (!editing || moving < 0 || moving >= tiles.Count)
        {
            return false;
        }
        landing = null;
        aimed = null;
        Drop();
        moving = NoTarget;
        pressed = NoTarget;
        dragging = NoTarget;
        travelled = false;
        // And the lines carried with it (FolderFlyout.Pending.cs).
        Group([]);
        if (GetCapture() == Hwnd)
        {
            _ = ReleaseCapture();
        }
        ShowArranged(animate: true);
        SetHot(NoTarget);
        ShowStates();
        return true;
    }

    /// <summary>edit: where every line shows now, on the screen (<see cref="Snapshot"/>).</summary>
    private Snapshot? Take()
    {
        if (plan is not { } shown)
        {
            return null;
        }
        var on = host.PanelOnScreen;
        var origin = new Vector2(on.left, on.top);
        var lines = new Dictionary<string, (Vector2 At, int List)>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < tiles.Count && index < shown.Cards.Count; index++)
        {
            lines[tiles[index].Island.Key] = (origin + LineAt(index), index < shown.CardList.Count ? shown.CardList[index] : 0);
        }
        // The carried line, and the lines carried with it (FolderFlyout.Pending.cs).
        var carried = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (Carried() is not null)
        {
            foreach (var index in GroupIndices())
            {
                carried.Add(tiles[index].Island.Key);
            }
        }
        // §357: the buttons by what each is, so they glide from where they showed whatever came or went among them.
        var adds = new Dictionary<string, Vector3>(StringComparer.Ordinal);
        foreach (var button in addVisuals)
        {
            adds[button.Key] = button.Sprite.Offset;
        }
        return new Snapshot(lines, carried, host.Pieces is { } pieces ? [.. pieces] : null, [.. listShapes.Select(shape => (shape.Offset, shape.Size))], adds, origin);
    }

    /// <summary>
    /// edit: the frame <paramref name="before"/> saw — the panels and each list's clip — put back at
    /// once where the panel stands now, to glide from (<see cref="Settle"/>); nothing where the
    /// folder has other panels or lists now.
    /// </summary>
    private void Reframe(Snapshot before, Vector2 origin)
    {
        var by = before.Origin - origin;
        if (before.Pieces is { } pieces && host.Pieces is { } now && pieces.Count == now.Count)
        {
            host.SetPieces([.. pieces.Select(piece => new D2D_RECT_F { left = piece.left + by.X, top = piece.top + by.Y, right = piece.right + by.X, bottom = piece.bottom + by.Y })]);
        }
        if (before.Clips.Count == listShapes.Count)
        {
            for (var list = 0; list < listShapes.Count; list++)
            {
                Reshape(listShapes[list], before.Clips[list].Offset + by, before.Clips[list].Size, animate: false);
            }
        }
    }

    /// <summary>
    /// edit: every line glides from where <paramref name="before"/> saw it to where it stands now —
    /// the carried one, and one come from another list, lifted over the folder while it does, so no
    /// list's edge cuts it — in a move's time; the lists' frames with them. Nothing where nothing moved.
    /// </summary>
    private void Settle(Snapshot? before)
    {
        if (before is null || plan is not { } shown || !IsOpen)
        {
            return;
        }
        var on = host.PanelOnScreen;
        var origin = new Vector2(on.left, on.top);
        var animate = look.Animations;
        var moved = false;
        Try(() =>
        {
            for (var index = 0; index < tiles.Count && index < shown.Cards.Count && index < cards.Count && index < contents.Count; index++)
            {
                var key = tiles[index].Island.Key;
                if (!before.Lines.TryGetValue(key, out var was))
                {
                    // A line come in — added, pasted, carried in — fades in where it stands, as a block
                    // added to the bar does (Scene.FadeIn).
                    if (animate)
                    {
                        Appear(cards[index]);
                        Appear(contents[index].Sprite);
                    }
                    continue;
                }
                var rest = shown.Cards[index];
                var to = new Vector2(rest.left, rest.top);
                var from = was.At - origin;
                var carried = before.Carried.Contains(key);
                if (from == to && !carried)
                {
                    continue;
                }
                moved = true;
                var list = index < shown.CardList.Count ? shown.CardList[index] : 0;
                if (animate && (carried || list != was.List) && Raise(index, from, plate: carried) is { } lifted)
                {
                    lifted.At = to;
                    Glide(lifted.Carrier, new Vector3(from, 0), new Vector3(to, 0), animate: true);
                    if (carried)
                    {
                        // The plate sinks into the list as the line lands.
                        Sink(lifted.Plate);
                        Sink(lifted.Caster);
                    }
                    continue;
                }
                Glide(cards[index], new Vector3(from, 0), new Vector3(to, 0), animate);
                Glide(contents[index].Sprite, new Vector3(from, 0), new Vector3(to, 0), animate);
                if (index < bands.Count && bands[index].Visual.IsVisible)
                {
                    var mark = bands[index].Visual;
                    Glide(mark, mark.Offset + new Vector3(from - to, 0), mark.Offset, animate);
                }
            }
        });
        if (!moved || !animate)
        {
            Settled();
            return;
        }
        // Until it is done the cards are one card a list, so no card's edge cuts a line gliding over
        // it; the panels and the lists' clips glide from where they showed, with the lines.
        unruled = true;
        Try(() =>
        {
            Reframe(before, origin);
            ShowFrame(shown, 0, animate: true);
            // The lists' buttons with them, each from where it showed (§357: by what it is; one come in
            // stands where it stands, as a line come in does).
            var by = new Vector3(before.Origin - origin, 0);
            for (var index = 0; index < addVisuals.Count && index < shown.Adds.Count; index++)
            {
                if (before.Adds.TryGetValue(addVisuals[index].Key, out var was))
                {
                    var sprite = addVisuals[index].Sprite;
                    Glide(sprite, was + by, AddAt(index, AddBox(shown, index)), animate: true);
                }
            }
        });
        if (Hwnd != HWND.NULL)
        {
            _ = SetTimer(Hwnd, SettleTimer, (uint)look.Motion.Ms(MotionKind.Move) + 50, null);
        }
    }

    /// <summary>edit: a settle has run its course: the lines it lifted go back into their lists, and the cards are each their own again.</summary>
    private void SettleDone()
    {
        if (Hwnd != HWND.NULL)
        {
            _ = KillTimer(Hwnd, SettleTimer);
        }
        // What a drop left for a layout is stale now.
        dropped = null;
        if (moving >= 0 && Carried() is not null)
        {
            // A new drag began meanwhile: it is that drag's.
            return;
        }
        Settled();
    }

    /// <summary>edit: nothing glides or is lifted any more: every line at its place, every card its own, at once.</summary>
    private void Settled()
    {
        if (Hwnd != HWND.NULL)
        {
            _ = KillTimer(Hwnd, SettleTimer);
        }
        if (raised.Count == 0 && !unruled && shifts is null)
        {
            return;
        }
        Unlift();
        if (IsOpen && plan is not null)
        {
            PlaceLines();
            // The rings and the marching ants (FolderFlyout.Ants.cs) as they are with
            // nothing lifted.
            ShowStates();
        }
    }

    /// <summary>
    /// edit: every lifted line back into its list and every line at its place, the frames at rest,
    /// at once: what a drop or a new layout starts from.
    /// </summary>
    private void Unlift()
    {
        foreach (var lifted in raised.ToList())
        {
            Lower(lifted);
        }
        shifts = null;
        unruled = false;
        arranged = null;
        arrangedFor = "";
        aimed = null;
        dragPointer = null;
        dragNatural = null;
        ghostShown = false;
        closedPlan = null;
        Try(() =>
        {
            if (ghostVisual is not null)
            {
                ghostVisual.IsVisible = false;
            }
            if (empty is not null)
            {
                empty.IsVisible = true;
            }
            PlaceAdds(animate: false);
            if (plan is { } shown)
            {
                for (var list = 0; list < listShapes.Count && list < shown.Lists.Count; list++)
                {
                    var box = shown.Lists[list].Box;
                    Reshape(listShapes[list], new Vector2(box.left, box.top), new Vector2(box.right - box.left, box.bottom - box.top), animate: false);
                }
            }
        });
        if (IsOpen && plan is not null)
        {
            PlaceLines();
        }
    }

    /// <summary>
    /// edit: line <paramref name="index"/> lifted over the folder with its top left corner at
    /// <paramref name="at"/> (panel px): its card and its content onto a carrier over everything,
    /// on the panel's own colour with a shadow where <paramref name="plate"/>. Null where it has no visuals.
    /// </summary>
    private Raised? Raise(int index, Vector2 at, bool plate)
    {
        if (plan is not { } shown || index < 0 || index >= cards.Count || index >= contents.Count || index >= shown.Cards.Count)
        {
            return null;
        }
        if (RaisedOf(index) is { } already)
        {
            return already;
        }
        Raised? made = null;
        Try(() =>
        {
            var lifted = spareRaised.Count > 0 ? spareRaised[^1] : MakeRaised();
            if (spareRaised.Count > 0)
            {
                spareRaised.RemoveAt(spareRaised.Count - 1);
            }
            var (card, sprite) = (cards[index], contents[index].Sprite);
            var home = sprite.Parent;
            var cardHome = card.Parent;
            cardHome?.Children.Remove(card);
            home?.Children.Remove(sprite);
            card.StopAnimation("Offset");
            sprite.StopAnimation("Offset");
            card.Offset = Vector3.Zero;
            sprite.Offset = Vector3.Zero;
            lifted.Body.Children.InsertAtTop(card);
            lifted.Body.Children.InsertAtTop(sprite);
            var box = shown.Cards[index];
            // §502: a spacer's stripes go with it, on its carrier where they stand on its card (FolderPlan.Bands): lifted —
            // dragged, added, pasted — a spacer is still what it is, not an empty plate. They stand still while it is (MoveBands).
            if (index < bands.Count)
            {
                var band = bands[index];
                var mark = band.Visual;
                var room = index < shown.Bands.Count ? shown.Bands[index] : default;
                mark.IsVisible = !room.IsEmpty;
                if (!room.IsEmpty)
                {
                    lifted.MarkHome = mark.Parent;
                    mark.Parent?.Children.Remove(mark);
                    mark.StopAnimation("Offset");
                    mark.Offset = new Vector3(room.X - box.left, room.Y - box.top, 0);
                    band.Lay(room.Size, scale);
                    lifted.Carrier.Children.InsertAtTop(mark);
                    lifted.Mark = mark;
                }
            }
            var size = new Vector2(MathF.Max(1, box.right - box.left), MathF.Max(1, box.bottom - box.top));
            var radius = MathF.Min(shown.Radius, size.Y / 2);
            var hair = shown.Hairline;
            lifted.BodyClip.Size = size;
            lifted.BodyClip.CornerRadius = new Vector2(radius, radius);
            lifted.Body.Size = size;
            lifted.Plate.Size = size;
            lifted.PlateBox.Offset = new Vector2(hair / 2, hair / 2);
            // §502: never less than nothing: a size below zero threw, Try swallowed it, and the line was never lifted.
            lifted.PlateBox.Size = Vector2.Max(size - new Vector2(hair, hair), Vector2.Zero);
            lifted.PlateBox.CornerRadius = new Vector2(MathF.Max(0, radius - (hair / 2)), MathF.Max(0, radius - (hair / 2)));
            lifted.PlateShape.StrokeThickness = hair;
            lifted.ShadowBox.Size = size;
            lifted.ShadowBox.CornerRadius = new Vector2(radius, radius);
            lifted.ShadowSource.Size = size;
            lifted.ShadowSurface.SourceSize = size;
            lifted.Caster.Size = size;
            foreach (var part in new Visual[] { lifted.Plate, lifted.Caster })
            {
                part.StopAnimation("Opacity");
                part.Opacity = 1;
            }
            lifted.Plate.IsVisible = plate;
            lifted.Caster.IsVisible = plate && theme.ShadowOpacity > 0;
            lifted.Carrier.StopAnimation("Offset");
            lifted.Carrier.Offset = new Vector3(at, 0);
            lifted.Carrier.Opacity = 1;
            carriersLayer!.Children.InsertAtTop(lifted.Carrier);
            lifted.Index = index;
            lifted.Home = home;
            lifted.CardHome = cardHome;
            lifted.At = at;
            raised.Add(lifted);
            made = lifted;
        });
        return made;
    }

    /// <summary>edit: a lifted line back into its list, at its place, and its carrier put by for the next.</summary>
    private void Lower(Raised lifted)
    {
        raised.Remove(lifted);
        Try(() =>
        {
            lifted.Carrier.Parent?.Children.Remove(lifted.Carrier);
            var index = lifted.Index;
            if (index >= 0 && index < cards.Count && index < contents.Count)
            {
                var (card, sprite) = (cards[index], contents[index].Sprite);
                lifted.Body.Children.Remove(card);
                lifted.Body.Children.Remove(sprite);
                lifted.CardHome?.Children.InsertAtTop(card);
                lifted.Home?.Children.InsertAtTop(sprite);
            }
            // §502: its stripes back where it stands, put in their place as the lines are placed again.
            if (lifted.Mark is { } mark)
            {
                lifted.Carrier.Children.Remove(mark);
                lifted.MarkHome?.Children.InsertAtTop(mark);
            }
        });
        lifted.Index = NoTarget;
        lifted.Home = null;
        lifted.CardHome = null;
        lifted.Mark = null;
        lifted.MarkHome = null;
        spareRaised.Add(lifted);
    }

    /// <summary>edit: a carrier for a lifted line, its shadow's shape as the bar's lifted block's (Scene.Shadow); §357: the folder's, kept for the next.</summary>
    private Raised MakeRaised()
    {
        var c = host.Compositor;
        var owned = new List<IDisposable>();
        T Keep<T>(T value)
            where T : IDisposable
        {
            objectsMade++;
            owned.Add(value);
            return value;
        }
        var carrier = Keep(c.CreateContainerVisual());
        var shadowBox = Keep(c.CreateRoundedRectangleGeometry());
        var shadowShape = Keep(c.CreateSpriteShape(shadowBox));
        shadowShape.FillBrush = Keep(c.CreateColorBrush(Color.FromArgb(255, 0, 0, 0)));
        var shadowSource = Keep(c.CreateShapeVisual());
        shadowSource.Shapes.Add(shadowShape);
        var shadowSurface = Keep(c.CreateVisualSurface());
        shadowSurface.SourceVisual = shadowSource;
        var shadow = Keep(c.CreateDropShadow());
        shadow.Mask = Keep(c.CreateSurfaceBrush(shadowSurface));
        // The bar's lifted block's (EditLook.cs).
        shadow.BlurRadius = PopupPaint.Round(EditLook.LiftShadowBlurDip * scale);
        shadow.Offset = new Vector3(0, PopupPaint.Round(EditLook.LiftShadowOffsetDip * scale), 0);
        shadow.Color = Color.FromArgb(255, 0, 0, 0);
        shadow.Opacity = EditLook.LiftShadow(theme.ShadowOpacity);
        var caster = Keep(c.CreateSpriteVisual());
        caster.Shadow = shadow;
        carrier.Children.InsertAtTop(caster);
        var body = Keep(c.CreateContainerVisual());
        var bodyClip = Keep(c.CreateRoundedRectangleGeometry());
        body.Clip = Keep(c.CreateGeometricClip(bodyClip));
        carrier.Children.InsertAtTop(body);
        var plate = Keep(c.CreateShapeVisual());
        var plateBox = Keep(c.CreateRoundedRectangleGeometry());
        var plateShape = Keep(c.CreateSpriteShape(plateBox));
        plateShape.FillBrush = Keep(c.CreateColorBrush(theme.Base));
        plateShape.StrokeBrush = Keep(c.CreateColorBrush(theme.Stroke));
        plate.Shapes.Add(plateShape);
        body.Children.InsertAtBottom(plate);
        var made = new Raised
        {
            Carrier = carrier,
            Caster = caster,
            ShadowSource = shadowSource,
            ShadowBox = shadowBox,
            ShadowSurface = shadowSurface,
            Body = body,
            BodyClip = bodyClip,
            Plate = plate,
            PlateBox = plateBox,
            PlateShape = plateShape,
        };
        made.Owned.AddRange(owned);
        return made;
    }

    /// <summary>The carried line's carrier, while it is lifted.</summary>
    private Raised? Carried() => moving >= 0 && shifts is not null ? RaisedOf(moving) : null;

    private Raised? RaisedOf(int index)
    {
        foreach (var lifted in raised)
        {
            if (lifted.Index == index)
            {
                return lifted;
            }
        }
        return null;
    }

    /// <summary>
    /// edit: what the folder kept of a drag and a settle goes as it closes (Closed): the next drag starts
    /// from nothing. §357: the lifted lines are back in their lists first (Unlift); their plates, the ghost
    /// (the accent's wash in an accent outline as the bar's, Scene.ShowGhost, made with the folder's frame)
    /// and the animations are the folder's, kept for the next drag.
    /// </summary>
    private void ForgetDrag()
    {
        if (Hwnd != HWND.NULL)
        {
            _ = KillTimer(Hwnd, SettleTimer);
        }
        shifts = null;
        unruled = false;
        arranged = null;
        arrangedFor = "";
        aimed = null;
        closedPlan = null;
        dragPointer = null;
        dragNatural = null;
        ghostShown = false;
    }

    /// <summary>
    /// edit: <paramref name="visual"/> to <paramref name="to"/>, gliding there from
    /// <paramref name="from"/> in a move's time with the Windows easing, or at once — the bar's
    /// Slide: explicit key frames on one animation set again for every use (StartAnimation copies it).
    /// </summary>
    private void Glide(Visual visual, Vector3 from, Vector3 to, bool animate)
    {
        visual.StopAnimation("Offset");
        visual.Offset = to;
        if (!animate || !look.Animations || from == to)
        {
            return;
        }
        var slide = slide3 ??= Own(host.Compositor.CreateVector3KeyFrameAnimation());
        slide.InsertKeyFrame(0, from);
        slide.InsertKeyFrame(1, to, Ease());
        slide.Duration = look.Motion.Time(MotionKind.Move);
        visual.StartAnimation("Offset", slide);
    }

    /// <summary>edit: a clip's shape to <paramref name="offset"/> and <paramref name="size"/>, gliding as the lines do (the bar's Reshape).</summary>
    private void Reshape(CompositionRoundedRectangleGeometry shape, Vector2 offset, Vector2 size, bool animate)
    {
        var (fromOffset, fromSize) = (shape.Offset, shape.Size);
        if (fromOffset == offset && fromSize == size)
        {
            return;
        }
        shape.StopAnimation("Offset");
        shape.StopAnimation("Size");
        shape.Offset = offset;
        shape.Size = size;
        if (!animate || !look.Animations)
        {
            return;
        }
        var slide = slide2 ??= Own(host.Compositor.CreateVector2KeyFrameAnimation());
        slide.Duration = look.Motion.Time(MotionKind.Move);
        slide.InsertKeyFrame(0, fromOffset);
        slide.InsertKeyFrame(1, offset, Ease());
        shape.StartAnimation("Offset", slide);
        slide.InsertKeyFrame(0, fromSize);
        slide.InsertKeyFrame(1, size, Ease());
        shape.StartAnimation("Size", slide);
    }

    /// <summary>edit: <paramref name="visual"/> fades out in a move's time, as a lifted line's plate sinks into its list.</summary>
    private void Sink(Visual visual)
    {
        visual.StopAnimation("Opacity");
        visual.Opacity = 0;
        if (!look.Animations)
        {
            return;
        }
        var fade = sink ??= Own(host.Compositor.CreateScalarKeyFrameAnimation());
        fade.InsertKeyFrame(0, 1);
        fade.InsertKeyFrame(1, 0, Ease());
        fade.Duration = look.Motion.Time(MotionKind.Move);
        visual.StartAnimation("Opacity", fade);
    }

    /// <summary>edit: <paramref name="visual"/> fades in, in a swap's time, as a block added to the bar (Scene.FadeIn).</summary>
    private void Appear(Visual visual)
    {
        visual.StopAnimation("Opacity");
        visual.Opacity = 1;
        if (!look.Animations)
        {
            return;
        }
        var fade = appear ??= Own(host.Compositor.CreateScalarKeyFrameAnimation());
        fade.InsertKeyFrame(0, 0);
        fade.InsertKeyFrame(1, 1, Ease());
        fade.Duration = look.Motion.Time(MotionKind.Swap);
        visual.StartAnimation("Opacity", fade);
    }

    private ScalarKeyFrameAnimation? appear;

    private CompositionEasingFunction Ease() =>
        decelerate ??= Own(MotionEase.Of(host.Compositor, MotionKind.Move));

    /// <summary>
    /// edit: Ctrl+arrow on line <paramref name="index"/>: it moves a place up or down its list, or
    /// into the list beside it, level with where it stands (<see cref="FolderDrag.KeyPlace"/>), and
    /// the dock moves it there (<see cref="FolderFlyoutAction.Place"/>); it glides to its new place
    /// as a carried line settles. Nothing at a list's end, or beside the outer lists.
    /// </summary>
    private FolderFlyoutEvent KeyMove(int index, int key)
    {
        keyboard = true;
        // Left and right as the columns stand: the first on the right of a right bar (FirstOnRight).
        if (FirstOnRight(shownEdge) && key is VK.VK_LEFT or VK.VK_RIGHT)
        {
            key = key == VK.VK_LEFT ? VK.VK_RIGHT : VK.VK_LEFT;
        }
        if (folder is not { } island || index < 0 || index >= tiles.Count || PlaceOfCard(index) is not { } from
            || FolderDrag.KeyPlace(island.Options, from, key) is not { } to)
        {
            ShowStates();
            return default;
        }
        return new FolderFlyoutEvent(FolderFlyoutAction.Place, tiles[index].Island, index, Place: to);
    }

    /// <summary>edit: the folder's own timers of a drag; true where <paramref name="id"/> was one of them.</summary>
    private bool DragTimer(nuint id)
    {
        switch (id)
        {
            case SettleTimer:
                SettleDone();
                return true;
            default:
                return false;
        }
    }

}
