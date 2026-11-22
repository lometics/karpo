// quick: the pointer on an island that takes it itself — the volume slider on
// the bar (ISliderIsland). A press on its track grabs the thumb, the bar keeps
// the pointer until the button goes up (so a drag that leaves the bar still
// moves the slider), and the click that would follow is swallowed; a wheel turn
// over the cell steps the level. Never in edit mode, where a press only picks a
// block up.

using System.Numerics;
using Karpo.Core;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

public sealed unsafe partial class Dock
{
    private sealed partial class DockWindow
    {
        /// <summary>The island holding the pointer, and the cell it holds it for.</summary>
        private (ISliderIsland Island, Hit Hit)? slider;

        /// <summary>Whether a slider is being dragged: its click must not also open or launch anything.</summary>
        internal bool SliderDragging => slider is not null;

        /// <summary>
        /// A press at (<paramref name="x"/>, <paramref name="y"/>): true when an island took the pointer,
        /// and the press does nothing else.
        /// </summary>
        private bool SliderPressed(Hit? hit, float x, float y)
        {
            // pins: the entry says whether a drag inside it may be its own (BarEntries.TakesDrags), and
            // the widget says where. It read `is ISliderIsland` before, which is an answer a kind
            // gave by omission: a slider added later that forgot the interface was never asked, and
            // nothing said so. A kind that declares the drag and whose widget cannot take one falls
            // through to the move, and the registry's table test is what makes that loud.
            if (Editing || hit is not { Kind: HitKind.Island } cell
                || !KindOf(cell).TakesDrags()
                || IslandAt(cell) is not ISliderIsland island)
            {
                return false;
            }
            var (point, size) = CellPoint(cell, x, y);
            if (size.X <= 0 || size.Y <= 0)
            {
                return false;
            }
            if (!island.Press(cell.Index, point, size, IslandLook()))
            {
                return false;
            }
            slider = (island, cell);
            SetCapture(Hwnd);
            return true;
        }

        /// <summary>The pointer moved while an island holds it; true when it was the island's.</summary>
        private bool SliderDragMoved()
        {
            if (slider is not { } held)
            {
                return false;
            }
            var (point, size) = CellPoint(held.Hit, pointer.X, pointer.Y);
            if (size.X > 0 && size.Y > 0)
            {
                held.Island.Drag(held.Hit.Index, point, size, IslandLook());
            }
            return true;
        }

        /// <summary>The button went up: the island lets the pointer go, and the click is the slider's.</summary>
        private bool SliderReleased()
        {
            if (slider is not { } held)
            {
                return false;
            }
            slider = null;
            held.Island.Release();
            if (GetCapture() == Hwnd)
            {
                ReleaseCapture();
            }
            return true;
        }

        /// <summary>The capture went elsewhere (a menu, another window): the drag ends where it was.</summary>
        private void SliderCaptureLost()
        {
            if (slider is { } held)
            {
                slider = null;
                held.Island.Release();
            }
        }

        /// <summary>
        /// A wheel turn over the bar: the island under the pointer takes it (the volume slider steps
        /// the level); true when it did. <paramref name="lParam"/> carries the pointer in screen px.
        /// </summary>
        internal bool SliderWheel(WPARAM wParam, LPARAM lParam)
        {
            var at = Local(new POINT { x = (short)((nint)lParam & 0xFFFF), y = (short)(((nint)lParam >> 16) & 0xFFFF) });
            if (Editing)
            {
                return false;
            }
            var hit = HitAt(at.X, at.Y);
            // A widget with no slider whose wheel is its own, where its entry says so: the sound
            // face steps the volume, as Windows 11's icon does (another face takes none).
            if (hit is { Kind: HitKind.Island } turned && KindOf(turned).TakesWheel() && IslandAt(turned) is IWheelIsland wheeled)
            {
                return wheeled.Wheel((short)(((nuint)wParam >> 16) & 0xFFFF));
            }
            // pins: as the press, the kind's own declaration rather than an interface sniff.
            if (hit is not { Kind: HitKind.Island } cell
                || !KindOf(cell).TakesWheel()
                || IslandAt(cell) is not ISliderIsland island)
            {
                return false;
            }
            return island.Wheel((short)(((nuint)wParam >> 16) & 0xFFFF), IslandLook());
        }

        /// <summary>The kind of the part <paramref name="hit"/> is in, or a kind no entry has for none.</summary>
        private BarKind KindOf(Hit hit)
        {
            var index = layout.PartOf(hit);
            // A kind no entry has takes neither a drag nor a wheel, so it is the harmless answer for "no part".
            return index >= 0 && index < layout.Specs.Count ? layout.Specs[index].Kind : TolerantEnumConverter<BarKind>.Unknown;
        }

        /// <summary>A point in a cell's own px, and the cell's size; a size of zero when the cell has gone.</summary>
        private (Vector2 Point, Vector2 Size) CellPoint(Hit hit, float x, float y)
        {
            var rect = layout.ItemRect(hit);
            return rect.IsEmpty
                ? (Vector2.Zero, Vector2.Zero)
                : (new Vector2(x - rect.X, y - rect.Y), new Vector2(rect.Width, rect.Height));
        }

        /// <summary>The style the islands are drawn with on this bar.</summary>
        /// <summary>
        /// A click released at (<paramref name="x"/>, <paramref name="y"/>) on <paramref name="hit"/>: true
        /// where a button inside the widget's cell took it — the next meeting's Join on its face — and the
        /// widget's own click (its folder opening) does not follow.
        /// </summary>
        private bool PointClicked(Hit hit, float x, float y)
        {
            if (Editing || hit is not { Kind: HitKind.Island } cell || IslandAt(cell) is not IPointClickIsland island)
            {
                return false;
            }
            var (point, size) = CellPoint(cell, x, y);
            return size.X > 0 && size.Y > 0 && island.ClickAt(cell.Index, point, size, IslandLook());
        }

        private IslandStyle IslandLook() =>
            new(layout.Scale, layout.Vertical, layout.IslandThickness, layout.Icon, Look) { Screen = layout.Screen };
    }
}
