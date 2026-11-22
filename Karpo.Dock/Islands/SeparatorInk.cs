// A folder panel's hairline.
//
// §505 (the person, 2026-11-05: "the spacers should always just be empty space and not a line or dot or
// anything"): a folder's divider is empty space, nothing drawn at rest. §524 (2026-11-07: "the spacers between
// groups should be a moving stripe pattern and not the line"): arranged, its room is striped (SpacerStripes.cs),
// so the one painter of its rule (Ink) and the rule's colour (ColorOf, Strong) are gone; §511 took the bar's
// separator, which this painted on the bar as well. What stays is the panel's hairline (Hairline).

namespace Karpo.Dock;

internal static class SeparatorInk
{
    /// <summary>
    /// The hairline of a panel at <paramref name="scale"/> px per DIP — the line between two rows of a list folder
    /// (docs\widgets.md §9) — in whole device pixels: the <b>rounded</b> scale, which every other line a popup draws
    /// already is (a folder's <c>Hairline</c>, a flyout's dividers). A panel with two rules for the thickness of a line
    /// is a panel where the line between two widgets is a pixel off the line under its footer, at 150 % and nowhere else.
    /// </summary>
    public static float Hairline(float scale) => MathF.Max(1f, PopupPaint.Round(scale > 0f ? scale : 1f));
}
