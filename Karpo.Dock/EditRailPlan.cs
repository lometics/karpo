// What a section's edit bar (the edit rail) holds and where its button lands, apart from any
// window, so it can be tested.
//
// Since 2026-10-07 (the person: the "+" and the grid, list and separator buttons go from the
// sections' edit bars — the add button at the bottom of every display and the add list's band
// have them — and "any remaining per-section strip [is] one row or one column, never 2×2, and
// never overlapping widgets") the edit bar holds one thing: what its section has no room to show,
// and only while there is any. A section showing everything has no edit bar at all.
//
// It is a plan in the sense VolumePlan is: one answer the measuring, the drawing, the hit test and
// the keyboard all read, so what is drawn and what a click is mapped by cannot disagree. Its buttons
// stand on one line along the bar: a row beside a horizontal bar, a column beside a vertical one.
// Everything below is in px at the scale it is given; nothing here reads the display, the config or
// the clock, so two calls with the same arguments give the same plan.

using System.Numerics;
using Karpo.Core;
using TerraFX.Interop.Windows;

namespace Karpo.Dock;

/// <param name="Rect">In panel px.</param>
public sealed record RailButton(EditRailAction Action, string Glyph, string Title, RectF Rect);

public sealed record EditRailPlan(IReadOnlyList<RailButton> Buttons, Vector2 Panel)
{
    public const float ButtonDip = 32;
    public const float MarginDip = 8;
    public const float SpaceDip = 4;
    public const float GlyphDip = 16;

    /// <summary>
    /// Lays the edit bar's buttons on one line along the bar's axis at <paramref name="scale"/>: a
    /// row, or a column beside a vertical bar (<paramref name="vertical"/>) — never wrapped into a
    /// square. <paramref name="notShown"/> says whether its section is holding widgets it has no room
    /// for, which is the only thing the edit bar says: with nothing hidden it holds nothing.
    /// </summary>
    public static EditRailPlan For(float scale, bool vertical, bool notShown = false)
    {
        float Px(float dip) => MathF.Round(dip * scale, MidpointRounding.AwayFromZero);
        var margin = Px(MarginDip);
        var button = Px(ButtonDip);
        var space = Px(SpaceDip);
        var parts = Parts(notShown);
        var placed = new List<RailButton>(parts.Count);
        var along = margin;
        for (var index = 0; index < parts.Count; index++)
        {
            along += index == 0 ? 0f : space;
            placed.Add(parts[index] with { Rect = vertical ? new RectF(margin, along, button, button) : new RectF(along, margin, button, button) });
            along += button;
        }
        var length = parts.Count == 0 ? 0f : along + margin;
        var thick = parts.Count == 0 ? 0f : margin + button + margin;
        return new EditRailPlan(placed, vertical ? new Vector2(thick, length) : new Vector2(length, thick));
    }

    /// <summary>
    /// The edit bar's buttons, in the order the keyboard walks them: what the section could not show,
    /// while there is any, and nothing else. Its title names what it is; what it says now (which
    /// widget, or how many) is the rail's, because that changes with the bar and the plan does not.
    /// </summary>
    public static IReadOnlyList<RailButton> Parts(bool notShown = false) =>
        notShown ? [new(EditRailAction.NotShown, Glyphs.NotShown, S.DockEditRailNotShown, default)] : [];

    /// <summary>The button at <paramref name="point"/> (panel px), or -1.</summary>
    public int At(Vector2 point)
    {
        for (var index = 0; index < Buttons.Count; index++)
        {
            if (Buttons[index].Rect.Contains(point.X, point.Y))
            {
                return index;
            }
        }
        return -1;
    }

    /// <summary>Segoe Fluent Icons for the buttons the registry has no kind for.</summary>
    internal static class Glyphs
    {
        /// <summary>Warning: several widgets the bar has no room for. One is drawn as its own kind (EditRail).</summary>
        public static readonly string NotShown = char.ConvertFromUtf32(0xE7BA);
    }
}

/// <summary>
/// edit: the sections' edit bars kept apart (2026-10-07: one beside every section of the display,
/// towards the middle of the screen). Two meet where two places meet at a corner of the display — a
/// place at the start of the top and one at the start of the left side — so the later one gives way:
/// it moves along its own section, away from the one it met, until it stands clear.
/// </summary>
public static class EditRails
{
    /// <summary>
    /// How far along its section a rail must move off its middle, in px, to stand clear of every one
    /// of <paramref name="taken"/> by <paramref name="gap"/>: 0 where it already does. <paramref name="at"/>
    /// is where it stands now, moved <paramref name="shift"/> off its middle; <paramref name="vertical"/>
    /// says it runs down the screen (beside a side). It moves away from the one it meets, and where
    /// the two are centred on each other, <paramref name="toward"/>: the middle of its edge (+1 right
    /// or down, -1 left or up).
    /// </summary>
    public static int Clear(RECT at, int shift, IReadOnlyList<RECT> taken, bool vertical, int toward, int gap)
    {
        var home = vertical ? at with { top = at.top - shift, bottom = at.bottom - shift } : at with { left = at.left - shift, right = at.right - shift };
        var moved = 0;
        // Each step clears one rail; past as many steps as there are rails, there is nothing left to clear.
        for (var step = 0; step <= taken.Count; step++)
        {
            var now = vertical ? home with { top = home.top + moved, bottom = home.bottom + moved } : home with { left = home.left + moved, right = home.right + moved };
            var hit = taken.Where(other => Overlap(now, other, gap)).Cast<RECT?>().FirstOrDefault();
            if (hit is not { } other)
            {
                return moved;
            }
            var away = vertical ? (now.top + now.bottom) - (other.top + other.bottom) : (now.left + now.right) - (other.left + other.right);
            var down = away > 0 || (away == 0 && toward >= 0);
            moved += vertical
                ? (down ? other.bottom + gap - now.top : -(now.bottom + gap - other.top))
                : (down ? other.right + gap - now.left : -(now.right + gap - other.left));
        }
        return moved;
    }

    /// <summary>Whether <paramref name="a"/> and <paramref name="b"/> stand closer than <paramref name="gap"/> to each other.</summary>
    private static bool Overlap(RECT a, RECT b, int gap) =>
        a.left < b.right + gap && b.left < a.right + gap && a.top < b.bottom + gap && b.top < a.bottom + gap;
}
