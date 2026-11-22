// A text box drawn inside a folder's line (docs\widgets.md §87): a to-do's "Add a task" and a task
// being renamed (one line), a note being edited (every line). The quick settings' TextField is a
// control of their own panel; a line of a folder is a card drawn into a surface, so this is the same
// thing — TextEditing's rules, DirectWrite's hit testing, WinUI's look (the faint fill, the rim, the
// accent along the bottom while it has the keys) — drawn into a box the line gives it.
//
// The keys arrive as the folder hands them on (FolderFlyout: WM_KEYDOWN, then WM_CHAR, so any
// keyboard layout types what it types); the arrows, Home, End, Page Up and Down, Backspace, Delete
// and Ctrl+A/C/X/V are its own, and Up and Down move between lines where they wrap. Enter is a new
// line in a multi-line box and the caller's in a one-line one, Escape always the caller's. A one-line
// box scrolls sideways to keep the caret in view, a multi-line one down.

using System.Numerics;
using Karpo.Dock.Native;
using Karpo.Dock.QuickSettings.Controls;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

internal sealed unsafe class InlineEditor
{
    private readonly TextEditing editing;
    private Vector2 scroll;
    private bool caretOn = true;
    private char pendingHigh;
    private float? goal;

    // The caret moved since the box was last drawn, so the drawing brings it into view.
    private bool following = true;

    public InlineEditor(bool multiLine, int maxLength)
    {
        editing = new TextEditing(maxLength, multiLine: multiLine);
    }

    public TextEditing Editing => editing;

    public string Text => editing.Text;

    public bool MultiLine => editing.MultiLine;

    /// <summary>What the empty box says: "Add a task".</summary>
    public string Placeholder { get; set; } = "";

    /// <summary>Starts with <paramref name="text"/>, all of it selected or the caret at its end.</summary>
    public void Begin(string text, bool selectAll)
    {
        editing.SetText(text);
        if (selectAll)
        {
            editing.SelectAll();
        }
        scroll = default;
        caretOn = true;
        following = true;
        pendingHigh = '\0';
        goal = null;
    }

    /// <summary>The caret blinks: true when it changed.</summary>
    public bool Blink()
    {
        caretOn = !caretOn;
        return true;
    }

    // --- The box --------------------------------------------------------------------------------------

    /// <summary>Between the box's rim and its words, across and down.</summary>
    private static (float Across, float Down) Padding(in TileStyle style) => (style.Px(10), style.Px(5));

    /// <summary>How tall a box is that shows all of its text <paramref name="width"/> px wide: one line for a one-line box.</summary>
    public float Height(Gfx gfx, float px, float width, in TileStyle style)
    {
        var (across, down) = Padding(style);
        var line = TilePaint.LineHeight(gfx, px);
        if (!MultiLine)
        {
            return MathF.Max(PopupPaint.Round(style.Text(32)), line + (2 * down));
        }
        var layout = Layout(gfx, px, MathF.Max(1, width - (2 * across)));
        try
        {
            DWRITE_TEXT_METRICS metrics;
            ComUtil.Check(layout->GetMetrics(&metrics));
            return MathF.Ceiling(MathF.Max(line, metrics.height)) + (2 * down) + style.Px(2);
        }
        finally
        {
            layout->Release();
        }
    }

    /// <summary>Where the words stand in <paramref name="box"/>.</summary>
    private static D2D_RECT_F Area(D2D_RECT_F box, in TileStyle style)
    {
        var (across, down) = Padding(style);
        return new D2D_RECT_F(box.left + across, box.top + down, MathF.Max(box.left + across + 1, box.right - across), MathF.Max(box.top + down + 1, box.bottom - down - style.Px(1)));
    }

    private IDWriteTextLayout* Layout(Gfx gfx, float px, float width)
    {
        // A space laid out for no words keeps a line's height and a caret's place.
        var layout = gfx.TextLayout(editing.Text, px, Weight.Regular, null);
        if (MultiLine)
        {
            _ = layout->SetWordWrapping(DWRITE_WORD_WRAPPING.DWRITE_WORD_WRAPPING_EMERGENCY_BREAK);
            _ = layout->SetMaxWidth(MathF.Max(1, width));
        }
        return layout;
    }

    // --- Keys -----------------------------------------------------------------------------------------

    /// <summary>
    /// A key in the box <paramref name="box"/>: true when it took it. <paramref name="changed"/> says
    /// whether the words changed (rather than only the caret).
    /// </summary>
    public bool Key(int key, bool shift, bool control, Gfx gfx, float px, D2D_RECT_F box, in TileStyle style, out bool changed)
    {
        changed = false;
        var keepGoal = false;
        switch (key)
        {
            case VK.VK_LEFT:
                editing.Left(shift, control);
                break;
            case VK.VK_RIGHT:
                editing.Right(shift, control);
                break;
            case VK.VK_HOME when control || !MultiLine:
                editing.Home(shift);
                break;
            case VK.VK_END when control || !MultiLine:
                editing.End(shift);
                break;
            case VK.VK_HOME or VK.VK_END:
                editing.MoveTo(LineEdge(gfx, px, box, style, key == VK.VK_END), shift);
                break;
            case VK.VK_UP or VK.VK_DOWN or VK.VK_PRIOR or VK.VK_NEXT when MultiLine:
                editing.MoveTo(Vertical(gfx, px, box, style, key), shift);
                keepGoal = true;
                break;
            case VK.VK_BACK:
                changed = editing.Backspace(control);
                break;
            case VK.VK_DELETE when shift && !control:
                changed = Cut();
                break;
            case VK.VK_DELETE:
                changed = editing.Delete(control);
                break;
            case VK.VK_INSERT when shift:
                changed = Paste();
                break;
            case VK.VK_INSERT when control:
                Copy();
                break;
            case 'A' when control:
                editing.SelectAll();
                break;
            case 'C' when control:
                Copy();
                break;
            case 'X' when control:
                changed = Cut();
                break;
            case 'V' when control:
                changed = Paste();
                break;
            case VK.VK_RETURN when MultiLine:
                changed = editing.Insert("\n");
                break;
            case VK.VK_SPACE:
                // The space itself comes as a character.
                return true;
            default:
                // A key that types comes as a character; anything else is not the box's.
                return !control && KeyTypes(key);
        }
        if (!keepGoal)
        {
            goal = null;
        }
        caretOn = true;
        following = true;
        return true;
    }

    /// <summary>Whether <paramref name="key"/> types something (a WM_CHAR follows it), so it is the box's.</summary>
    private static bool KeyTypes(int key) =>
        key is >= '0' and <= '9' or >= 'A' and <= 'Z' or >= VK.VK_NUMPAD0 and <= VK.VK_DIVIDE or >= VK.VK_OEM_1 and <= VK.VK_OEM_3 or >= VK.VK_OEM_4 and <= VK.VK_OEM_8 or VK.VK_OEM_102;

    /// <summary>A character typed: true when the words changed.</summary>
    public bool Char(char c)
    {
        if (char.IsHighSurrogate(c))
        {
            pendingHigh = c;
            return false;
        }
        string typed;
        if (char.IsLowSurrogate(c))
        {
            if (pendingHigh == 0)
            {
                return false;
            }
            typed = new string([pendingHigh, c]);
        }
        else
        {
            typed = c.ToString();
        }
        pendingHigh = '\0';
        // Control characters (Tab, Enter, Escape, Ctrl+letter) were keys, not text.
        if (char.IsControl(c))
        {
            return false;
        }
        goal = null;
        caretOn = true;
        following = true;
        return editing.Insert(typed);
    }

    private static HWND Owner => GetFocus();

    private void Copy()
    {
        if (editing.Copy() is { } text)
        {
            QuickSettings.Controls.Clipboard.SetText(Owner, text);
        }
    }

    private bool Cut()
    {
        if (!editing.HasSelection)
        {
            return false;
        }
        QuickSettings.Controls.Clipboard.SetText(Owner, editing.SelectedText);
        return editing.Cut() is not null;
    }

    private bool Paste() => QuickSettings.Controls.Clipboard.GetText(Owner) is { Length: > 0 } text && editing.Paste(text);

    /// <summary>The start or the end of the line the caret is on, as the words wrap.</summary>
    private int LineEdge(Gfx gfx, float px, D2D_RECT_F box, in TileStyle style, bool end)
    {
        var area = Area(box, style);
        var layout = Layout(gfx, px, area.right - area.left);
        try
        {
            uint count = 0;
            _ = layout->GetLineMetrics(null, 0, &count);
            var lines = new DWRITE_LINE_METRICS[Math.Max(1, (int)count)];
            fixed (DWRITE_LINE_METRICS* first = lines)
            {
                _ = layout->GetLineMetrics(first, (uint)lines.Length, &count);
            }
            var start = 0;
            for (var index = 0; index < count; index++)
            {
                var line = lines[index];
                var stop = start + (int)line.length;
                var last = index == count - 1;
                if (editing.Caret < stop || last)
                {
                    if (!end)
                    {
                        return start;
                    }
                    // Before the line's break, or before the space it wrapped at.
                    return Math.Max(start, stop - (int)line.newlineLength - (last || line.newlineLength > 0 ? 0 : (int)line.trailingWhitespaceLength));
                }
                start = stop;
            }
            return end ? editing.Text.Length : 0;
        }
        finally
        {
            layout->Release();
        }
    }

    /// <summary>Where Up, Down, Page Up or Page Down take the caret: the same x on the line above or below.</summary>
    private int Vertical(Gfx gfx, float px, D2D_RECT_F box, in TileStyle style, int key)
    {
        var area = Area(box, style);
        var layout = Layout(gfx, px, area.right - area.left);
        try
        {
            float x;
            float y;
            DWRITE_HIT_TEST_METRICS hit;
            ComUtil.Check(layout->HitTestTextPosition((uint)editing.Caret, BOOL.FALSE, &x, &y, &hit));
            goal ??= x;
            var line = MathF.Max(1, hit.height);
            var page = MathF.Max(line, MathF.Floor((area.bottom - area.top) / line) * line);
            var to = key switch
            {
                VK.VK_UP => hit.top - (line / 2),
                VK.VK_DOWN => hit.top + line + (line / 2),
                VK.VK_PRIOR => hit.top - page,
                _ => hit.top + page,
            };
            DWRITE_TEXT_METRICS metrics;
            ComUtil.Check(layout->GetMetrics(&metrics));
            if (to < 0)
            {
                return 0;
            }
            if (to >= metrics.height)
            {
                return editing.Text.Length;
            }
            BOOL trailing;
            BOOL inside;
            ComUtil.Check(layout->HitTestPoint(goal.Value, to, &trailing, &inside, &hit));
            return (int)hit.textPosition + (trailing ? (int)hit.length : 0);
        }
        finally
        {
            layout->Release();
        }
    }

    // --- The pointer ------------------------------------------------------------------------------------

    /// <summary>A press at <paramref name="point"/> in <paramref name="box"/>: the caret there, a word on a double click, all on a triple.</summary>
    public void Press(Vector2 point, int clicks, bool shift, Gfx gfx, float px, D2D_RECT_F box, in TileStyle style)
    {
        var index = IndexAt(point, gfx, px, box, style);
        switch (clicks)
        {
            case 2:
                editing.SelectWordAt(index);
                break;
            case >= 3:
                editing.SelectAll();
                break;
            default:
                editing.MoveTo(index, shift);
                break;
        }
        goal = null;
        caretOn = true;
        following = true;
    }

    /// <summary>The pointer dragged to <paramref name="point"/> while pressed: the selection follows it.</summary>
    public void Drag(Vector2 point, Gfx gfx, float px, D2D_RECT_F box, in TileStyle style)
    {
        editing.MoveTo(IndexAt(point, gfx, px, box, style), extend: true);
        caretOn = true;
        following = true;
    }

    private int IndexAt(Vector2 point, Gfx gfx, float px, D2D_RECT_F box, in TileStyle style)
    {
        if (editing.Text.Length == 0)
        {
            return 0;
        }
        var area = Area(box, style);
        var layout = Layout(gfx, px, area.right - area.left);
        try
        {
            var top = MultiLine ? area.top : Centred(layout, area);
            BOOL trailing;
            BOOL inside;
            DWRITE_HIT_TEST_METRICS hit;
            if (layout->HitTestPoint(point.X - area.left + scroll.X, point.Y - top + scroll.Y, &trailing, &inside, &hit).FAILED)
            {
                return editing.Caret;
            }
            return (int)hit.textPosition + (trailing ? (int)hit.length : 0);
        }
        finally
        {
            layout->Release();
        }
    }

    private static float Centred(IDWriteTextLayout* layout, D2D_RECT_F area)
    {
        DWRITE_TEXT_METRICS metrics;
        return layout->GetMetrics(&metrics).SUCCEEDED ? area.top + PopupPaint.Round((area.bottom - area.top - metrics.height) / 2) : area.top;
    }

    // --- Drawn ----------------------------------------------------------------------------------------

    /// <summary>
    /// The box in <paramref name="box"/>: WinUI's text box with the keys — its fill, its rim and the
    /// accent along its bottom — the words scrolled to keep the caret in view, the selection, the caret.
    /// </summary>
    public void Paint(ID2D1DeviceContext* dc, Gfx gfx, D2D_RECT_F box, float px, QuickTheme theme, in TileStyle style)
    {
        // In a mirrored line the box stands where the mirror puts it and is drawn as it is: its
        // caret and selection are DirectWrite's, from the left (Mirror.cs).
        var kept = Mirror.Keep(dc, box);
        try
        {
            PaintUpright(dc, gfx, box, px, theme, style);
        }
        finally
        {
            Mirror.Resume(dc, kept);
        }
    }

    private void PaintUpright(ID2D1DeviceContext* dc, Gfx gfx, D2D_RECT_F box, float px, QuickTheme theme, in TileStyle style)
    {
        var radius = style.Px(4);
        var hair = MathF.Max(1, style.Px(1));
        PopupPaint.Fill(dc, box, radius, theme.InputActive);
        PopupPaint.Stroke(dc, box, radius, hair, theme.ControlStroke);
        var edge = style.Px(2);
        var strip = new D2D_RECT_F(box.left, box.bottom - edge, box.right, box.bottom);
        dc->PushAxisAlignedClip(&strip, D2D1_ANTIALIAS_MODE.D2D1_ANTIALIAS_MODE_ALIASED);
        PopupPaint.Fill(dc, box, radius, theme.Accent);
        dc->PopAxisAlignedClip();

        var area = Area(box, style);
        var layout = Layout(gfx, px, area.right - area.left);
        dc->PushAxisAlignedClip(&area, D2D1_ANTIALIAS_MODE.D2D1_ANTIALIAS_MODE_ALIASED);
        try
        {
            var top = MultiLine ? area.top : Centred(layout, area);
            float caretX;
            float caretY;
            DWRITE_HIT_TEST_METRICS caret;
            ComUtil.Check(layout->HitTestTextPosition((uint)editing.Caret, BOOL.FALSE, &caretX, &caretY, &caret));
            var lineHeight = caret.height > 0 ? caret.height : TilePaint.LineHeight(gfx, px);
            Keep(caretX, caret.top, lineHeight, area, layout);
            var origin = new D2D_POINT_2F(PopupPaint.Round(area.left - scroll.X), PopupPaint.Round(top - scroll.Y));
            if (editing.Text.Length == 0 && Placeholder.Length > 0)
            {
                PopupPaint.Text(dc, gfx, Placeholder, px, Weight.Regular, theme.TextSecondary, area.left, top, area.right - area.left);
            }
            if (editing.HasSelection)
            {
                uint count = 0;
                _ = layout->HitTestTextRange((uint)editing.SelectionStart, (uint)editing.SelectionLength, origin.x, origin.y, null, 0, &count);
                if (count > 0)
                {
                    var hits = new DWRITE_HIT_TEST_METRICS[count];
                    fixed (DWRITE_HIT_TEST_METRICS* first = hits)
                    {
                        _ = layout->HitTestTextRange((uint)editing.SelectionStart, (uint)editing.SelectionLength, origin.x, origin.y, first, count, &count);
                    }
                    foreach (var hit in hits.AsSpan(0, (int)count))
                    {
                        // A selected line break shows as a sliver, as Windows' edit boxes show it.
                        var width = MathF.Max(hit.width, style.Px(4));
                        PopupPaint.Fill(dc, new D2D_RECT_F(hit.left, hit.top, hit.left + width, hit.top + hit.height), 0, theme.Selection);
                    }
                }
            }
            var brush = Gfx.Brush(dc, PaintWatch.Seen(layout, origin, theme.Text));
            try
            {
                dc->DrawTextLayout(origin, layout, (ID2D1Brush*)brush, D2D1_DRAW_TEXT_OPTIONS.D2D1_DRAW_TEXT_OPTIONS_ENABLE_COLOR_FONT);
            }
            finally
            {
                brush->Release();
            }
            if (!editing.HasSelection && caretOn)
            {
                var x = PopupPaint.Round(origin.x + caretX);
                var y = origin.y + caret.top;
                PopupPaint.Fill(dc, new D2D_RECT_F(x, y, x + hair, y + lineHeight), 0, theme.Text);
            }
        }
        finally
        {
            dc->PopAxisAlignedClip();
            layout->Release();
        }
    }

    /// <summary>The scroll that keeps the caret, at <paramref name="x"/>, <paramref name="top"/>, in the area; no empty room past the words.</summary>
    private void Keep(float x, float top, float line, D2D_RECT_F area, IDWriteTextLayout* layout)
    {
        DWRITE_TEXT_METRICS metrics;
        ComUtil.Check(layout->GetMetrics(&metrics));
        var width = area.right - area.left;
        var height = area.bottom - area.top;
        // Only a caret that moved is brought into view: the wheel's scroll stays where it took the words.
        var follow = following;
        following = false;
        if (MultiLine)
        {
            if (follow && top - scroll.Y < 0)
            {
                scroll.Y = top;
            }
            else if (follow && top + line - scroll.Y > height)
            {
                scroll.Y = top + line - height;
            }
            scroll.Y = Math.Clamp(scroll.Y, 0, MathF.Max(0, metrics.height - height));
            scroll.X = 0;
            return;
        }
        if (x - scroll.X > width - 1)
        {
            scroll.X = x - width + 1;
        }
        else if (x - scroll.X < 0)
        {
            scroll.X = x;
        }
        scroll.X = Math.Clamp(scroll.X, 0, MathF.Max(0, metrics.widthIncludingTrailingWhitespace - width + 1));
        scroll.Y = 0;
    }

    /// <summary>The wheel over a multi-line box: its words scroll, a line a notch. True when they moved.</summary>
    public bool Wheel(int notches, Gfx gfx, float px, D2D_RECT_F box, in TileStyle style)
    {
        if (!MultiLine)
        {
            return false;
        }
        var before = scroll.Y;
        var area = Area(box, style);
        var layout = Layout(gfx, px, area.right - area.left);
        try
        {
            DWRITE_TEXT_METRICS metrics;
            ComUtil.Check(layout->GetMetrics(&metrics));
            scroll.Y = Math.Clamp(scroll.Y - (notches * 3 * TilePaint.LineHeight(gfx, px)), 0, MathF.Max(0, metrics.height - (area.bottom - area.top)));
        }
        finally
        {
            layout->Release();
        }
        return scroll.Y != before;
    }
}
