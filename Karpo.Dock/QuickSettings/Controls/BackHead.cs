// The way back at the head of whatever is shown in place of what was there (2026-10-30, the person: "for the
// wifi submenu or bluetooth submenu or also the folder view there is the back button, but the back button is
// only on the topleft, i want the whole header area to be the back button"): a quick settings page open in a
// folder's column (PageLayout.Title), a page's own head (PageHeadTile), a subfolder gone into in a folder's
// contents line (FolderContentsView). One head, one look, one target.
//
// It is the whole head row: under the pointer the row lights in one plate, as a row of a list does, and a press
// anywhere on it goes back. Windows' back arrow stands at its start (the forward one where mirrored, as it
// points back there); the title is its host's words over it. What the head carries of its own — the Wi-Fi's
// switch and refresh, Bluetooth's switch — stands over it and keeps its own press, as a Windows settings card's
// switch does over the card: the host asks those first. Narrator hears where it goes: "Back, Wi-Fi".

using System.Numerics;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock.QuickSettings.Controls;

internal sealed unsafe class BackHead : Widget
{
    /// <summary>Segoe Fluent Icons' Back, the arrow Windows' pages start with.</summary>
    internal const string Glyph = "";

    /// <summary>Segoe Fluent Icons' Forward: the way back where the drawing is mirrored.</summary>
    internal const string ForwardGlyph = "";

    private string title = "";

    public BackHead(Action back)
    {
        Name = S.DockPageBack;
        Clicked = back;
    }

    /// <summary>What it goes back from: the title its host draws over it, for Narrator's sake.</summary>
    public string Title
    {
        get => title;
        set
        {
            if (title != value)
            {
                title = value;
                Invalidate();
            }
        }
    }

    /// <summary>What Narrator hears: "Back, Wi-Fi"; "Back" where there is no title.</summary>
    public string Said => title.Length > 0 ? $"{Name}, {title}" : Name;

    /// <summary>The plate the pointer lights, in its own px: its host's rows' across the head (a line's, all of it).</summary>
    public D2D_RECT_F Plate { get; private set; }

    /// <summary>The arrow's box, in its own px; its glyph centred in it.</summary>
    public D2D_RECT_F Arrow { get; private set; }

    /// <summary>The arrow's size in px, where it is not WinUI's 16 DIPs: a list's line glyph (FolderContentsView).</summary>
    public float? GlyphSize { get; set; }

    public override bool Focusable => Enabled;

    /// <summary>
    /// Puts it on the head <paramref name="row"/> (its host's px): the plate at <paramref name="plate"/> and the
    /// arrow at <paramref name="arrow"/>, both in the same px.
    /// </summary>
    public void Place(D2D_RECT_F row, D2D_RECT_F plate, D2D_RECT_F arrow)
    {
        Bounds = row;
        Plate = Within(plate, row);
        Arrow = Within(arrow, row);
    }

    /// <summary>Whether <paramref name="at"/>, in its own px, is on the arrow: where "Back" is said as a tooltip.</summary>
    public bool OnArrow(Vector2 at) => at.X >= Arrow.left && at.X < Arrow.right && at.Y >= Arrow.top && at.Y < Arrow.bottom;

    /// <summary>The arrow's box in its host's px.</summary>
    public D2D_RECT_F ArrowAt => new(Bounds.left + Arrow.left, Bounds.top + Arrow.top, Bounds.left + Arrow.right, Bounds.top + Arrow.bottom);

    private static D2D_RECT_F Within(D2D_RECT_F rect, D2D_RECT_F row) =>
        new(rect.left - row.left, rect.top - row.top, rect.right - row.left, rect.bottom - row.top);

    public override void Paint(ID2D1DeviceContext* dc, QuickContext context)
    {
        var theme = context.Theme;
        var down = Pressed && Hot;
        var plate = Shift(Plate);
        // A line's head is lit edge to edge, square, as its rows are (LineItem); a flyout's plate in from its sides, round.
        var radius = Plate.left <= 0 && Plate.right >= Width ? 0 : context.D(4);
        if (Enabled && (Hot || down))
        {
            PopupPaint.Fill(dc, plate, radius, down ? theme.SubtlePressed : theme.SubtleHover);
        }
        var ink = !Enabled ? theme.TextDisabled : Hot && theme.HighContrast ? theme.TextOnHover : theme.Text;
        PopupPaint.Glyph(dc, context.Gfx, Mirror.On ? ForwardGlyph : Glyph, GlyphSize ?? context.GlyphPx, ink, Shift(Arrow));
        if (FocusVisible)
        {
            FocusRing(dc, context, plate, radius, 0);
        }
    }

    /// <summary><paramref name="rect"/>, in its own px, where it paints: from (0, 0), or at its bounds (<see cref="Widget.AtBounds"/>).</summary>
    private D2D_RECT_F Shift(D2D_RECT_F rect) =>
        new(Local.left + rect.left, Local.top + rect.top, Local.left + rect.right, Local.top + rect.bottom);
}
