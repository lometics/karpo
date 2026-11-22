// §534: "Lock the taskbar" from the bar's right click, and a bar not locked dragged by its empty room to another edge
// of its display (Karpo.Core\BarMoves.cs says what and why).
//
// A press on the bar's own room (HitKind.Panel) at rest, unlocked, holds the mouse; short of the system's drag threshold
// it is a press like any other, its release the click it always was. Past it the bar is carried: the strip it would
// stand in on the edge nearest the pointer is shown in the accent as the selection rectangle is drawn (EditBand, the one
// pointer window), nothing where that is the edge it stands on; the release writes the edge as the Display page's
// Position does (BarLooks.WriteEdge, Apply), and the bar is made again there. Escape, a right press or the mouse lost
// leave it where it was.

using System.Numerics;
using Karpo.Core;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

public sealed unsafe partial class Dock
{
    /// <summary>§534: "Lock the taskbar" in the bar's right click.</summary>
    internal const uint CMD_LOCK_TASKBAR = 10;

    /// <summary>§534: where a bar carried would land, shown while it is carried; made the first time one is.</summary>
    private EditBand? landing;

    /// <summary>§534: its window's title, never shown: what a test finds it by.</summary>
    internal const string LandingTitle = "Karpo bar landing"; // not-ui: a window's title

    /// <summary>§534: the bars locked, or unlocked, as the right click's row was chosen: saved as every change is.</summary>
    private void ToggleLock()
    {
        var next = config.Clone();
        if (BarMoves.WriteLock(next, !next.LockTaskbar))
        {
            Apply(next);
        }
    }

    /// <summary>§534: whether a press on <paramref name="window"/>'s empty room may carry it to another edge: unlocked, at rest.</summary>
    private bool Movable(DockWindow window) =>
        BarMoves.Carries(config.LockTaskbar, editor is not null) && !options.Demo.HasValue && !window.IsAnchor;

    /// <summary>
    /// §534: where <paramref name="window"/>'s bar, carried, would land: its display's strip along <paramref name="edge"/>,
    /// the bar's thickness, in the accent of the look it wears; nothing for none, or for the edge it stands on.
    /// </summary>
    private void ShowLanding(DockWindow window, Edge? edge)
    {
        if (edge is not { } to || to == config.EdgeOfScreen(window.Id))
        {
            landing?.Hide();
            return;
        }
        var bounds = window.ScreenBounds;
        var strip = BarMoves.Landing(new RectI(bounds.left, bounds.top, bounds.right, bounds.bottom), to, (int)MathF.Ceiling(window.Layout.Thickness));
        landing ??= new EditBand(gfx, instance, LandingTitle);
        landing.Show(
            new RECT { left = strip.Left, top = strip.Top, right = strip.Right, bottom = strip.Bottom },
            FlyoutTheme.For(window.Look).Accent,
            window.UiScale);
    }

    /// <summary>§534: <paramref name="window"/>'s bar let go over <paramref name="edge"/>: stands there from now on, as the Display page's Position puts it.</summary>
    private void MoveBar(DockWindow window, Edge edge)
    {
        landing?.Hide();
        var next = config.Clone();
        if (windows.Contains(window) && BarLooks.WriteEdge(next, SpotOf(window), edge))
        {
            Apply(next);
        }
    }

    private sealed partial class DockWindow
    {
        /// <summary>§534: a press on the bar's empty room, unlocked, which past the drag threshold carries the bar.</summary>
        private BarMove? barMove;

        /// <summary>§534: whether the bar is being carried to another edge.</summary>
        public bool BarMoving => barMove is { Moving: true };

        /// <summary>
        /// §534: whether a drag arranges the bar — the grip along its inner edge drags its height, §663 a widget pressed on
        /// it is carried (into edit mode): in edit mode, or not locked. The one gate (BarMoves.Arranges).
        /// </summary>
        private bool Arranges => BarMoves.Arranges(dock.config.LockTaskbar, Editing);

        /// <summary>§534: a press at rest: on the bar's own room, unlocked, it may become the bar carried; the mouse held for it.</summary>
        private void BarMovePressed(Hit? hit, float x, float y)
        {
            barMove = null;
            if (hit is not { Kind: HitKind.Panel } || !dock.Movable(this))
            {
                return;
            }
            var at = OnScreen(new Vector2(x, y));
            barMove = new BarMove(new RectI(monitor.Bounds.Left, monitor.Bounds.Top, monitor.Bounds.Right, monitor.Bounds.Bottom), dock.config.EdgeOfScreen(Id), at.x, at.y);
            _ = SetCapture(Hwnd);
        }

        /// <summary>
        /// §534: the pointer moved with the bar's room pressed. Past the threshold the bar is carried: the pressed look,
        /// the tooltip and what was open go, and where it would land shows. True while it is carried.
        /// </summary>
        private bool BarMoveMoved()
        {
            if (barMove is not { } move)
            {
                return false;
            }
            var was = move.Moving;
            var at = OnScreen(pointer);
            var changed = move.Step(at.x, at.y, GetSystemMetrics(SM.SM_CXDRAG), GetSystemMetrics(SM.SM_CYDRAG));
            if (!move.Moving)
            {
                return false;
            }
            if (!was)
            {
                dock.ClosePreview(this);
                dock.CloseFlyout();
                scene.Tooltip(dock.gfx, null, null);
                scene.Highlight(null, false);
                if (pressed is not null)
                {
                    scene.Press(Lit(pressed), false);
                    pressed = null;
                }
                // Escape, which the bar may not have the keys to hear.
                Arm(DRAG, DRAG_MS);
            }
            SetCursor(LoadCursorW(HINSTANCE.NULL, IDC.IDC_SIZEALL));
            if (changed)
            {
                dock.ShowLanding(this, move.Target);
            }
            return true;
        }

        /// <summary>§534: the button came up: true where the bar was carried, which lands it, so no click follows.</summary>
        private bool BarMoveReleased(float x, float y)
        {
            if (barMove is not { } move)
            {
                return false;
            }
            barMove = null;
            if (GetCapture() == Hwnd)
            {
                _ = ReleaseCapture();
            }
            if (!move.Moving)
            {
                return false;
            }
            var at = OnScreen(new Vector2(x, y));
            _ = move.Step(at.x, at.y, GetSystemMetrics(SM.SM_CXDRAG), GetSystemMetrics(SM.SM_CYDRAG));
            if (move.Target == move.From)
            {
                dock.ShowLanding(this, null);
                RecheckPointer();
                return true;
            }
            // Made again on that edge: this window goes with the arrange.
            dock.MoveBar(this, move.Target);
            return true;
        }

        /// <summary>§534: the bar's carry called off (Escape, a right press, the mouse lost): it stays where it stood.</summary>
        public void CancelBarMove()
        {
            if (barMove is null)
            {
                return;
            }
            barMove = null;
            dock.ShowLanding(this, null);
            if (GetCapture() == Hwnd)
            {
                _ = ReleaseCapture();
            }
        }
    }
}
