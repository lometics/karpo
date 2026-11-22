// WinUI's text box and password box: a 32 px box with radius 4, a faint fill
// and a thin outline whose bottom edge turns into a 2 px accent line while it
// has the keyboard. Typing arrives as WM_CHAR, so any keyboard layout (dead
// keys, AltGr, a surrogate pair in two messages) types what it types; the keys
// that edit (arrows, Home, End, Backspace, Delete, Ctrl+A/C/X/V, Enter) arrive
// as WM_KEYDOWN. What the text and the caret do is TextEditing's; this draws it,
// scrolls it sideways to keep the caret in view, and maps the pointer to places
// in it with DirectWrite's hit testing.
//
// A password shows a dot per character, and while the eye button at its right
// is held (WinUI's "peek"), the text itself.
//
// A search box (SearchBox, Glyph) is Windows 11's instead: the magnifier before the words and no
// line along its bottom, its corners a text box's 4 DIPs as every box Karpo draws, not a pill.
// The contacts' search box is this one.

using System.Numerics;
using Karpo.Dock.Native;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock.QuickSettings.Controls;

internal sealed unsafe class TextField : Widget
{
    // Segoe Fluent Icons: RedEye, the reveal button's eye.
    private static readonly string EyeGlyph = char.ConvertFromUtf32(0xE7B3);

    // PasswordBox's default PasswordChar.
    private const char Dot = '●';

    private readonly TextEditing editing;
    private float scroll;
    private bool caretOn = true;
    private bool selecting;
    private bool revealHot;
    private bool revealing;
    private char pendingHigh;
    private string placeholder = "";

    public TextField(bool password = false, int maxLength = 256)
    {
        editing = new TextEditing(maxLength, password);
    }

    public bool Password => editing.Password;

    /// <summary>Whether some of the text is selected: Ctrl+C is the box's then.</summary>
    public bool HasSelection => editing.HasSelection;

    public string Text
    {
        get => editing.Text;
        set
        {
            if (editing.Text != value)
            {
                editing.SetText(value);
                scroll = 0;
                Changed();
            }
        }
    }

    /// <summary>What the empty box says: "Enter the network security key".</summary>
    public string Placeholder
    {
        get => placeholder;
        set
        {
            if (placeholder != value)
            {
                placeholder = value;
                Invalidate();
            }
        }
    }

    /// <summary>The editing state, for tests and for the page to select all on opening.</summary>
    public TextEditing Editing => editing;

    /// <summary>
    /// search: a glyph before the text — the magnifier of Windows 11's search box, in the
    /// contacts' search. Null for none, which every other field is.
    /// </summary>
    public string? Glyph { get; init; }

    /// <summary>
    /// search: Windows 11's search box rather than a text box: no line along its bottom, the focus
    /// shows in its fill. Its corners are a text box's (PopupPaint.ControlRadiusDip), not Windows' pill.
    /// </summary>
    public bool SearchBox { get; init; }

    /// <summary>The text changed by the person.</summary>
    public Action? TextChanged { get; set; }

    /// <summary>Enter: the page's default action (Connect, Save).</summary>
    public Action? Submitted { get; set; }

    public override bool Focusable => Enabled;

    public override bool HitTestVisible => true;

    /// <summary>Selects everything, as a field that opens with text in it does.</summary>
    public void SelectAll()
    {
        editing.SelectAll();
        Invalidate();
    }

    // --- Keys and characters ---------------------------------------------------

    public override bool Key(int key, Mods mods)
    {
        var shift = (mods & Mods.Shift) != 0;
        var control = (mods & Mods.Control) != 0;
        var changed = false;
        switch (key)
        {
            case VK.VK_LEFT:
                editing.Left(shift, control);
                break;
            case VK.VK_RIGHT:
                editing.Right(shift, control);
                break;
            case VK.VK_HOME:
                editing.Home(shift);
                break;
            case VK.VK_END:
                editing.End(shift);
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
            case VK.VK_RETURN:
                Submitted?.Invoke();
                return true;
            case VK.VK_SPACE:
                // The space itself comes as a character.
                return true;
            default:
                return false;
        }
        if (changed)
        {
            Changed();
        }
        else
        {
            Moved();
        }
        return true;
    }

    public override bool Character(char c)
    {
        if (char.IsHighSurrogate(c))
        {
            pendingHigh = c;
            return true;
        }
        string typed;
        if (char.IsLowSurrogate(c))
        {
            if (pendingHigh == 0)
            {
                return true;
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
            return true;
        }
        if (editing.Insert(typed))
        {
            Changed();
        }
        return true;
    }

    private void Copy()
    {
        if (editing.Copy() is { } text)
        {
            Clipboard.SetText(Window, text);
        }
    }

    private bool Cut()
    {
        if (editing.Password || !editing.HasSelection)
        {
            return false;
        }
        Clipboard.SetText(Window, editing.SelectedText);
        return editing.Cut() is not null;
    }

    private bool Paste() => Clipboard.GetText(Window) is { Length: > 0 } text && editing.Paste(text);

    /// <summary>
    /// The window the clipboard is opened for: the panel's, or, for a field on a folder's card
    /// (QuickPageTile), the thread's active window, which is the folder taking the keys.
    /// </summary>
    private HWND Window => Panel is { } panel ? panel.Hwnd : GetActiveWindow();

    // --- The pointer -------------------------------------------------------------

    public override bool PointerPressed(Vector2 at, Mods mods, int clicks)
    {
        if (RevealShown && Inside(RevealBox, at))
        {
            revealing = true;
            Invalidate();
            return true;
        }
        var index = IndexAt(at.X);
        switch (clicks)
        {
            case 2:
                editing.SelectWordAt(index);
                break;
            case >= 3:
                editing.SelectAll();
                break;
            default:
                editing.MoveTo(index, (mods & Mods.Shift) != 0);
                break;
        }
        selecting = clicks == 1;
        Moved();
        return true;
    }

    public override void PointerMoved(Vector2 at)
    {
        var overReveal = RevealShown && Inside(RevealBox, at);
        if (overReveal != revealHot)
        {
            revealHot = overReveal;
            Invalidate();
        }
        if (Pressed && selecting)
        {
            editing.MoveTo(IndexAt(at.X), extend: true);
            Moved();
        }
    }

    public override void PointerReleased(Vector2 at, bool inside)
    {
        selecting = false;
        if (revealing)
        {
            revealing = false;
            Invalidate();
        }
    }

    public override void FocusChanged()
    {
        caretOn = true;
        pendingHigh = '\0';
        if (!Focused)
        {
            revealing = false;
        }
    }

    public override bool Blink()
    {
        if (!Focused)
        {
            return false;
        }
        caretOn = !caretOn;
        return true;
    }

    // --- Drawing ---------------------------------------------------------------

    private bool RevealShown => editing.Password && Focused && editing.Text.Length > 0;

    private D2D_RECT_F RevealBox
    {
        get
        {
            var context = ContextNow;
            var width = context?.D(30) ?? 30;
            var inset = context?.D(4) ?? 4;
            return new D2D_RECT_F(Width - inset - width, inset, Width - inset, Height - inset);
        }
    }

    /// <summary>Where the text goes: inside WinUI's padding, after the glyph where it has one, left of the eye button when it shows.</summary>
    private (float Left, float Right) TextArea(QuickContext context)
    {
        var left = Glyph is null ? context.D(11) : context.D(GlyphTextDip);
        var right = Width - (SearchBox ? context.D(SearchTextRightDip) : context.D(11));
        if (RevealShown)
        {
            right = RevealBox.left - context.D(2);
        }
        return (left, MathF.Max(left + 1, right));
    }

    private static bool Inside(D2D_RECT_F rect, Vector2 at) => at.X >= rect.left && at.X < rect.right && at.Y >= rect.top && at.Y < rect.bottom;

    private string Shown => editing.Password && !revealing ? new string(Dot, editing.Text.Length) : editing.Text;

    public override void Paint(ID2D1DeviceContext* dc, QuickContext context)
    {
        var theme = context.Theme;
        var radius = context.D(PopupPaint.ControlRadiusDip);
        var box = Local;
        var fill = !Enabled ? theme.ControlFillDisabled : Focused ? theme.InputActive : Hot ? theme.ControlFillHover : theme.ControlFill;
        PopupPaint.Fill(dc, box, radius, fill);
        if (SearchBox && Enabled && !Focused)
        {
            // search: Windows 11's search box at rest and under the pointer: ControlElevationBorderBrush,
            // its bottom 3 DIPs shading to the darker stroke; with the focus, the plain one (SearchPill.cs).
            PopupPaint.ElevationStroke(dc, box, radius, context.Hairline, theme.ControlStroke, theme.ControlStrokeBottom, context.D(SearchPill.ElevationDip));
        }
        else
        {
            PopupPaint.Stroke(dc, box, radius, context.Hairline, theme.ControlStroke);
        }
        if (!SearchBox)
        {
            // The bottom edge: the strong stroke at rest, 2 px of accent with the keyboard.
            var edge = Focused ? context.D(2) : context.Hairline;
            var strip = new D2D_RECT_F(box.left, box.bottom - edge, box.right, box.bottom);
            dc->PushAxisAlignedClip(&strip, D2D1_ANTIALIAS_MODE.D2D1_ANTIALIAS_MODE_ALIASED);
            PopupPaint.Fill(dc, box, radius, Focused ? theme.Accent : Enabled ? theme.StrongStroke : theme.StrongStrokeDisabled);
            dc->PopAxisAlignedClip();
        }
        if (Glyph is { } glyph)
        {
            // Where Windows 11's search box has its magnifier: 11 DIPs in, 16 DIPs across, centred.
            var side = context.D(16);
            PopupPaint.Glyph(dc, context.Gfx, glyph, context.GlyphPx, Enabled ? theme.Text : theme.TextDisabled,
                PopupPaint.Rect(context.D(GlyphLeftDip), PopupPaint.Round((Height - side) / 2), side, side));
        }

        var (left, right) = TextArea(context);
        var area = new D2D_RECT_F(left, 0, right, Height);
        var shown = Shown;
        dc->PushAxisAlignedClip(&area, D2D1_ANTIALIAS_MODE.D2D1_ANTIALIAS_MODE_ALIASED);
        try
        {
            if (shown.Length == 0)
            {
                if (placeholder.Length > 0)
                {
                    var size = PopupPaint.Measure(context.Gfx, placeholder, context.TextPx, Weight.Regular, right - left);
                    // search: a search box with the focus dims its placeholder, as Windows' open one
                    // does (TextControlPlaceholderForegroundFocused).
                    var hint = !Enabled ? theme.TextDisabled : SearchBox && Focused ? theme.TextTertiary : theme.TextSecondary;
                    PopupPaint.Text(dc, context.Gfx, placeholder, context.TextPx, Weight.Regular, hint, left, PopupPaint.Round((Height - size.Y) / 2), right - left);
                }
                if (Focused && caretOn)
                {
                    Caret(dc, context, left);
                }
                return;
            }
            var layout = context.Gfx.TextLayout(shown, context.TextPx, Weight.Regular, null);
            try
            {
                DWRITE_TEXT_METRICS metrics;
                ComUtil.Check(layout->GetMetrics(&metrics));
                var top = PopupPaint.Round((Height - metrics.height) / 2);
                var origin = new D2D_POINT_2F(PopupPaint.Round(left - scroll), top);
                var ink = Enabled ? theme.Text : theme.TextDisabled;
                Draw(dc, layout, origin, ink);
                if (Focused && editing.HasSelection)
                {
                    var from = origin.x + X(layout, editing.SelectionStart, shown.Length);
                    var to = origin.x + X(layout, editing.SelectionStart + editing.SelectionLength, shown.Length);
                    var selection = new D2D_RECT_F(MathF.Max(left, from), top, MathF.Min(right, to), top + metrics.height);
                    if (selection.right > selection.left)
                    {
                        PopupPaint.Fill(dc, selection, 0, theme.Selection);
                        dc->PushAxisAlignedClip(&selection, D2D1_ANTIALIAS_MODE.D2D1_ANTIALIAS_MODE_ALIASED);
                        Draw(dc, layout, origin, theme.SelectionText);
                        dc->PopAxisAlignedClip();
                    }
                }
                else if (Focused && caretOn)
                {
                    Caret(dc, context, origin.x + X(layout, editing.Caret, shown.Length));
                }
            }
            finally
            {
                layout->Release();
            }
        }
        finally
        {
            dc->PopAxisAlignedClip();
        }
        if (RevealShown)
        {
            var reveal = RevealBox;
            if (revealing || revealHot)
            {
                PopupPaint.Fill(dc, reveal, radius, revealing ? theme.SubtlePressed : theme.SubtleHover);
            }
            PopupPaint.Glyph(dc, context.Gfx, EyeGlyph, 12 * context.Scale, theme.TextSecondary, reveal);
        }
    }

    /// <summary>
    /// search: where the glyph starts, where the words start after it, and how far a search box's words
    /// keep from its far end: Windows' search box's (SearchBoxIconMargin, SearchBoxTextMargin).
    /// </summary>
    private const float GlyphLeftDip = 11;
    private const float GlyphTextDip = 37;
    private const float SearchTextRightDip = 16;

    private void Caret(ID2D1DeviceContext* dc, QuickContext context, float x)
    {
        var width = MathF.Max(1, context.D(1));
        var inset = context.D(7);
        PopupPaint.Fill(dc, new D2D_RECT_F(PopupPaint.Round(x), inset, PopupPaint.Round(x) + width, Height - inset), 0, context.Theme.Text);
    }

    private static void Draw(ID2D1DeviceContext* dc, IDWriteTextLayout* layout, D2D_POINT_2F origin, DXGI_RGBA color)
    {
        var brush = Gfx.Brush(dc, color);
        try
        {
            dc->DrawTextLayout(origin, layout, (ID2D1Brush*)brush, D2D1_DRAW_TEXT_OPTIONS.D2D1_DRAW_TEXT_OPTIONS_ENABLE_COLOR_FONT);
        }
        finally
        {
            brush->Release();
        }
    }

    /// <summary>The x of the edge before character <paramref name="index"/> in the laid-out text.</summary>
    private static float X(IDWriteTextLayout* layout, int index, int length)
    {
        // Words laid right to left (Arabic) start at their right and end at their left: DirectWrite says where.
        if (length > 0 && layout->GetReadingDirection() == DWRITE_READING_DIRECTION.DWRITE_READING_DIRECTION_RIGHT_TO_LEFT)
        {
            float edge;
            float down;
            DWRITE_HIT_TEST_METRICS at;
            return layout->HitTestTextPosition((uint)Math.Clamp(index, 0, length), BOOL.FALSE, &edge, &down, &at).SUCCEEDED ? edge : 0;
        }
        if (index <= 0)
        {
            return 0;
        }
        float x;
        float y;
        DWRITE_HIT_TEST_METRICS hit;
        if (index >= length)
        {
            DWRITE_TEXT_METRICS metrics;
            return layout->GetMetrics(&metrics).SUCCEEDED ? metrics.widthIncludingTrailingWhitespace : 0;
        }
        return layout->HitTestTextPosition((uint)index, BOOL.FALSE, &x, &y, &hit).SUCCEEDED ? x : 0;
    }

    /// <summary>The place in the text nearest <paramref name="x"/> (the field's own px).</summary>
    private int IndexAt(float x)
    {
        if (ContextNow is not { } context || editing.Text.Length == 0)
        {
            return 0;
        }
        var (left, _) = TextArea(context);
        var layout = context.Gfx.TextLayout(Shown, context.TextPx, Weight.Regular, null);
        try
        {
            BOOL trailing;
            BOOL inside;
            DWRITE_HIT_TEST_METRICS hit;
            if (layout->HitTestPoint(x - left + scroll, 1, &trailing, &inside, &hit).FAILED)
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

    /// <summary>The text changed: the page hears of it, and the caret stays in view.</summary>
    private void Changed()
    {
        TextChanged?.Invoke();
        Moved();
    }

    /// <summary>The caret or the selection moved: it shows (a blink restarts lit), in view.</summary>
    private void Moved()
    {
        caretOn = true;
        KeepCaretInView();
        Invalidate();
    }

    private void KeepCaretInView()
    {
        if (ContextNow is not { } context)
        {
            return;
        }
        var shown = Shown;
        var (left, right) = TextArea(context);
        var room = right - left;
        if (shown.Length == 0)
        {
            scroll = 0;
            return;
        }
        var layout = context.Gfx.TextLayout(shown, context.TextPx, Weight.Regular, null);
        try
        {
            var caret = X(layout, editing.Caret, shown.Length);
            var total = X(layout, shown.Length, shown.Length);
            if (caret - scroll > room)
            {
                scroll = caret - room;
            }
            else if (caret - scroll < 0)
            {
                scroll = caret;
            }
            // No empty room at the end once there is more text than room.
            scroll = Math.Clamp(scroll, 0, MathF.Max(0, total - room + MathF.Max(1, context.D(1))));
        }
        finally
        {
            layout->Release();
        }
    }
}

/// <summary>The clipboard's text, read and written the way an edit control does.</summary>
internal static unsafe class Clipboard
{
    private const uint UnicodeText = 13;

    public static string? GetText(HWND owner)
    {
        if (!IsClipboardFormatAvailable(UnicodeText) || !OpenClipboard(owner))
        {
            return null;
        }
        try
        {
            var data = (HGLOBAL)GetClipboardData(UnicodeText).Value;
            if (data == HGLOBAL.NULL)
            {
                return null;
            }
            var chars = (char*)GlobalLock(data);
            if (chars == null)
            {
                return null;
            }
            try
            {
                var bytes = (int)Math.Min(GlobalSize(data), int.MaxValue);
                var length = 0;
                while (length < bytes / 2 && chars[length] != '\0')
                {
                    length++;
                }
                return new string(chars, 0, length);
            }
            finally
            {
                _ = GlobalUnlock(data);
            }
        }
        finally
        {
            _ = CloseClipboard();
        }
    }

    public static void SetText(HWND owner, string text)
    {
        if (!OpenClipboard(owner))
        {
            return;
        }
        try
        {
            _ = EmptyClipboard();
            var bytes = (nuint)((text.Length + 1) * sizeof(char));
            var data = GlobalAlloc(GMEM.GMEM_MOVEABLE, bytes);
            if (data == HGLOBAL.NULL)
            {
                return;
            }
            var chars = (char*)GlobalLock(data);
            if (chars == null)
            {
                _ = GlobalFree(data);
                return;
            }
            fixed (char* source = text)
            {
                Buffer.MemoryCopy(source, chars, (long)bytes, text.Length * sizeof(char));
            }
            chars[text.Length] = '\0';
            _ = GlobalUnlock(data);
            // The clipboard owns the memory once this succeeds.
            if (SetClipboardData(UnicodeText, (HANDLE)data.Value) == HANDLE.NULL)
            {
                _ = GlobalFree(data);
            }
        }
        finally
        {
            _ = CloseClipboard();
        }
    }
}
