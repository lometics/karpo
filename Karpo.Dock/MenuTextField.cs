// menus: a text field as a row of a menu (2026-10-19, the person: "inline every setting of every bar
// widget and folder widget into its edit menu"; docs\widgets.md §219). A clock's label, a heading's
// words, a share's name — and the field a search row types into (MenuSearch.cs) — are set where the
// menu stands, without Settings.
//
// It is Windows 11's text box, as the add list's search field and the quick settings' fields are
// (Flyout.Search.cs, QuickSettings\Controls\TextField.cs): 32 DIP with radius 4, a faint fill and a
// thin outline whose bottom edge turns into 2 px of accent while it has the keys, its placeholder
// greyed while it is empty; a clear button (✕) at its end while it has the keys and words, and a
// search field's magnifier after it, as WinUI's AutoSuggestBox has them. In a mirrored menu
// (Arabic, Urdu) its words stand at its right and its buttons at its left (Mirror.cs).
//
// What it holds, the caret and the selection, and what each key does to them is TextEditing's — the
// one model every field of Karpo's edits with — and this file's Key and Char say what the menu does
// next: Enter keeps what was typed (Commit); Escape first puts back what the setting says (Revert),
// and only a field with nothing typed lets the keys go (Blur), so a first Escape never closes the
// menu; Up and Down (Tab, Shift+Tab) go on to the rows over and under it. Every other key is the
// field's while it has the keys: the menu's arrows, its shortcuts (Delete, Ctrl+C) and its
// accelerators hear none of them (Flyout.Fields.cs).
//
// §338: a password's field (Masked) is Windows' PasswordBox: a bullet for each letter, nothing to the
// clipboard, a double click selecting all of it, and what it holds wiped as it goes (Wipe).

using System.Numerics;
using Karpo.Dock.Native;
using Karpo.Dock.QuickSettings.Controls;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

/// <summary>What a key did to a menu's text field, and so what the menu does next (see the top of the file).</summary>
internal enum FieldKey
{
    /// <summary>The key was the field's, and changed nothing that shows: the menu does nothing with it.</summary>
    None,

    /// <summary>The caret or the selection moved.</summary>
    Moved,

    /// <summary>What it holds changed.</summary>
    Edited,

    /// <summary>Enter: what it holds is kept.</summary>
    Commit,

    /// <summary>Escape with something typed: what the setting says is back in it, and it keeps the keys.</summary>
    Revert,

    /// <summary>Escape with nothing typed: it lets the keys go, and the menu has them again.</summary>
    Blur,

    /// <summary>Up, or Shift+Tab: the keys go to the row over it.</summary>
    Up,

    /// <summary>Down, or Tab: the keys go to the row under it (a search's first result).</summary>
    Down,
}

/// <summary>How a menu's text field is drawn: its size and the menu's scale, words and direction.</summary>
internal readonly record struct FieldLook(Vector2 Size, float Scale, float TextPx, string Placeholder, bool Mirrored, bool Search);

/// <summary>A menu's text field: what it holds, what the keys do to it, and how it is drawn; see the top of the file.</summary>
internal sealed unsafe class MenuTextField
{
    // Segoe Fluent Icons: WinUI's delete button and AutoSuggestBox's query icon.
    private static readonly string ClearGlyph = char.ConvertFromUtf32(0xE894);
    private static readonly string SearchGlyph = char.ConvertFromUtf32(0xE721);

    /// <summary>A button inside the box (✕, the magnifier), in DIP across, and its glyph, as WinUI's.</summary>
    internal const float ButtonDip = 28;

    /// <summary>Where its words begin inside the box, in DIPs, as WinUI's TextBox's.</summary>
    internal const float PadDip = 11;

    private const float ButtonGlyphPx = 12;

    private char high;

    /// <param name="shown">What the setting says now, which the field holds to begin with.</param>
    /// <param name="longest">The most it may hold, as the page's box allows.</param>
    /// <param name="masked">§338: a password's: see <see cref="Masked"/>.</param>
    public MenuTextField(string shown, int longest = int.MaxValue, bool masked = false)
    {
        Editing = new TextEditing(longest);
        Editing.SetText(shown);
        Shown = Editing.Text;
        Masked = masked;
    }

    public TextEditing Editing { get; }

    public string Text => Editing.Text;

    /// <summary>
    /// §338: a password's field, as Windows' PasswordBox: it draws a bullet for each letter (<see cref="Drawn"/>),
    /// gives nothing to the clipboard (copy and cut do nothing; paste does), and a double click selects all
    /// of it rather than a word, which would say where its words end.
    /// </summary>
    public bool Masked { get; }

    /// <summary>What it draws and measures: its words, or a bullet for each of their letters where <see cref="Masked"/>, so the caret stands where it does.</summary>
    internal string Drawn => Masked ? new string(Bullet, Editing.Text.Length) : Editing.Text;

    /// <summary>A masked field's bullet, as the row says a password it holds (OptionAccountRows.Bullets).</summary>
    internal const char Bullet = Karpo.Core.OptionAccountRows.Bullet;

    /// <summary>§338: everything it holds gone, and what the setting said with it: a password's field, its menu closed.</summary>
    public void Wipe()
    {
        Editing.SetText("");
        Shown = "";
        Scroll = 0;
    }

    /// <summary>What the setting says now: what the field holds at rest, and what Escape puts back.</summary>
    public string Shown { get; private set; }

    /// <summary>Whether something other than the setting was typed.</summary>
    public bool Dirty => Editing.Text != Shown;

    /// <summary>Whether it has the keys: the caret shows, and the bottom edge is the accent.</summary>
    public bool Focused { get; private set; }

    /// <summary>Whether the pointer is over it: a lighter fill.</summary>
    public bool Hot { get; set; }

    /// <summary>Whether the pointer is over its clear button.</summary>
    public bool ClearHot { get; set; }

    /// <summary>Whether the blinking caret is lit now.</summary>
    public bool CaretOn { get; set; } = true;

    /// <summary>How far its words are scrolled sideways, in px, to keep the caret in view.</summary>
    public float Scroll { get; set; }

    /// <summary>Whether the clear button shows: while it has the keys and words.</summary>
    public bool Clears => Focused && Editing.Text.Length > 0;

    /// <summary>
    /// The setting says <paramref name="shown"/> now (the menu shown again after a change): the field
    /// shows it, unless somebody is typing something else in it — or <paramref name="browsing"/> the
    /// results of what they typed, the keys on one of them (an answer shows the menu again).
    /// </summary>
    public void Show(string shown, bool browsing = false)
    {
        if ((Focused || browsing) && Dirty)
        {
            Shown = shown;
            return;
        }
        var changed = Editing.Text != shown;
        Shown = shown;
        if (changed)
        {
            Editing.SetText(shown);
            Scroll = 0;
        }
    }

    /// <summary>
    /// It takes the keys: what it holds selected, so typing replaces the setting's words, as the
    /// page's search boxes select theirs (<paramref name="selectAll"/>); else the caret stays.
    /// </summary>
    public void Focus(bool selectAll)
    {
        if (!Focused && selectAll)
        {
            Editing.SelectAll();
        }
        Focused = true;
        CaretOn = true;
    }

    /// <summary>It lets the keys go: no caret, no selection; what was typed stays until it is kept or put back.</summary>
    public void Blur()
    {
        Focused = false;
        high = '\0';
        Editing.MoveTo(Editing.Caret, extend: false);
    }

    /// <summary>What it holds was kept: it is what the setting says, until the menu is shown again with it (<see cref="Show"/>).</summary>
    public void Kept() => Shown = Editing.Text;

    /// <summary>What the setting says back in it, the caret at its end.</summary>
    public void Revert()
    {
        Editing.SetText(Shown);
        Scroll = 0;
    }

    /// <summary>The clear button: everything it holds taken out. Whether anything was.</summary>
    public bool Clear()
    {
        if (Editing.Text.Length == 0)
        {
            return false;
        }
        Editing.SetText("");
        Scroll = 0;
        return true;
    }

    /// <summary>
    /// A character typed (WM_CHAR): into the field, a surrogate pair's two halves as one. Control
    /// characters (Enter, Escape, Backspace, Ctrl+letters) were keys, not text.
    /// </summary>
    public FieldKey Char(char c)
    {
        if (char.IsHighSurrogate(c))
        {
            high = c;
            return FieldKey.None;
        }
        string typed;
        if (char.IsLowSurrogate(c))
        {
            if (high == '\0')
            {
                return FieldKey.None;
            }
            typed = new string([high, c]);
        }
        else
        {
            typed = c.ToString();
        }
        high = '\0';
        if (char.IsControl(c))
        {
            return FieldKey.None;
        }
        return Editing.Insert(typed) ? FieldKey.Edited : FieldKey.None;
    }

    /// <summary>
    /// A key (WM_KEYDOWN) while it has the keys, with Shift and Ctrl as they are: what it did (see
    /// the top of the file). <paramref name="paste"/> reads the clipboard, <paramref name="copy"/>
    /// writes it.
    /// </summary>
    public FieldKey Key(int key, bool shift, bool control, Func<string?> paste, Action<string> copy)
    {
        var editing = Editing;
        switch (key)
        {
            // §338: a password gives nothing to the clipboard: copy and cut do nothing at all.
            case VK.VK_DELETE when shift && !control && Masked:
            case 'X' when control && Masked:
            case 'C' when control && Masked:
            case VK.VK_INSERT when control && Masked:
                return FieldKey.None;
            case VK.VK_RETURN:
                return FieldKey.Commit;
            case VK.VK_ESCAPE:
                if (Dirty)
                {
                    Revert();
                    return FieldKey.Revert;
                }
                return FieldKey.Blur;
            case VK.VK_UP:
            case VK.VK_TAB when shift:
                return FieldKey.Up;
            case VK.VK_DOWN:
            case VK.VK_TAB:
                return FieldKey.Down;
            case VK.VK_LEFT:
                editing.Left(shift, control);
                return FieldKey.Moved;
            case VK.VK_RIGHT:
                editing.Right(shift, control);
                return FieldKey.Moved;
            case VK.VK_HOME:
                editing.Home(shift);
                return FieldKey.Moved;
            case VK.VK_END:
                editing.End(shift);
                return FieldKey.Moved;
            case VK.VK_BACK:
                return editing.Backspace(control) ? FieldKey.Edited : FieldKey.None;
            case VK.VK_DELETE when shift && !control:
            case 'X' when control:
                return Cut(copy) ? FieldKey.Edited : FieldKey.None;
            case VK.VK_DELETE:
                return editing.Delete(control) ? FieldKey.Edited : FieldKey.None;
            case VK.VK_INSERT when shift:
            case 'V' when control:
                return paste() is { Length: > 0 } text && editing.Paste(text) ? FieldKey.Edited : FieldKey.None;
            case VK.VK_INSERT when control:
            case 'C' when control:
                if (editing.Copy() is { } copied)
                {
                    copy(copied);
                }
                return FieldKey.None;
            case 'A' when control:
                editing.SelectAll();
                return FieldKey.Moved;
            default:
                // A letter comes as a character (Char); every other key is the field's and does nothing.
                return FieldKey.None;
        }
    }

    private bool Cut(Action<string> copy)
    {
        if (!Editing.HasSelection)
        {
            return false;
        }
        copy(Editing.SelectedText);
        return Editing.Cut() is not null;
    }

    // --- Where things stand ---------------------------------------------------------------------------

    private static float Px(float dip, float scale) => MathF.Round(dip * scale, MidpointRounding.AwayFromZero);

    /// <summary>The buttons inside the box, in its px: the clear button (empty while it does not show) and a search's magnifier (empty for a text field).</summary>
    public (D2D_RECT_F Clear, D2D_RECT_F Search) Buttons(in FieldLook look)
    {
        var side = Px(ButtonDip, look.Scale);
        var inset = Px(2, look.Scale);
        var top = inset;
        var bottom = look.Size.Y - inset;
        var width = look.Size.X;
        var mirrored = look.Mirrored;
        D2D_RECT_F At(int place) => mirrored
            ? new D2D_RECT_F(inset + (place * side), top, inset + ((place + 1) * side), bottom)
            : new D2D_RECT_F(width - inset - ((place + 1) * side), top, width - inset - (place * side), bottom);
        var search = look.Search ? At(0) : default;
        var clear = Clears ? At(look.Search ? 1 : 0) : default;
        return (clear, search);
    }

    /// <summary>Where its words go, in its px: inside WinUI's padding, clear of the buttons.</summary>
    public (float Left, float Right) TextArea(in FieldLook look)
    {
        var pad = Px(PadDip, look.Scale);
        var buttons = (look.Search ? 1 : 0) + (Clears ? 1 : 0);
        var room = buttons > 0 ? Px(2, look.Scale) + (buttons * Px(ButtonDip, look.Scale)) + Px(2, look.Scale) : pad;
        return look.Mirrored
            ? (room, MathF.Max(room + 1, look.Size.X - pad))
            : (pad, MathF.Max(pad + 1, look.Size.X - room));
    }

    /// <summary>Whether <paramref name="x"/>, <paramref name="y"/> (its px) is on the clear button.</summary>
    public bool OnClear(in FieldLook look, float x, float y)
    {
        var (clear, _) = Buttons(look);
        return Clears && x >= clear.left && x < clear.right && y >= clear.top && y < clear.bottom;
    }

    /// <summary>The place in its words nearest <paramref name="x"/> (its px).</summary>
    public int IndexAt(Gfx gfx, in FieldLook look, float x)
    {
        var text = Drawn;
        if (text.Length == 0)
        {
            return 0;
        }
        var (left, _) = TextArea(look);
        var layout = gfx.TextLayout(text, look.TextPx, Weight.Regular, null);
        try
        {
            BOOL trailing;
            BOOL inside;
            DWRITE_HIT_TEST_METRICS hit;
            if (layout->HitTestPoint(x - left + Scroll, 1, &trailing, &inside, &hit).FAILED)
            {
                return Editing.Caret;
            }
            return (int)hit.textPosition + (trailing ? (int)hit.length : 0);
        }
        finally
        {
            layout->Release();
        }
    }

    /// <summary>
    /// Its words scroll sideways to keep the caret in it, never leaving room at their end; in a mirrored
    /// menu words that fit stand at its right. At rest it shows where its words begin — their left, or
    /// their right where they are written right to left —, as a text box without the focus does.
    /// </summary>
    public void KeepCaretInView(Gfx gfx, in FieldLook look)
    {
        var text = Drawn;
        var (left, right) = TextArea(look);
        var room = right - left;
        if (text.Length == 0)
        {
            Scroll = 0;
            return;
        }
        var layout = gfx.TextLayout(text, look.TextPx, Weight.Regular, null);
        try
        {
            var caret = TextX(layout, Focused ? Editing.Caret : 0, text.Length);
            DWRITE_TEXT_METRICS metrics;
            var total = layout->GetMetrics(&metrics).SUCCEEDED ? metrics.widthIncludingTrailingWhitespace : 0;
            var scroll = Scroll;
            if (caret - scroll > room)
            {
                scroll = caret - room;
            }
            else if (caret - scroll < 0)
            {
                scroll = caret;
            }
            scroll = Math.Clamp(scroll, 0, MathF.Max(0, total - room + MathF.Max(1, Px(1, look.Scale))));
            if (look.Mirrored && total <= room)
            {
                scroll = total - room;
            }
            Scroll = scroll;
        }
        finally
        {
            layout->Release();
        }
    }

    // --- Drawing ----------------------------------------------------------------------------------

    /// <summary>
    /// WinUI's text box, as the add list's search field draws it: the fill and the outline, the
    /// bottom edge (the accent while it has the keys), the words or the placeholder, the selection
    /// or the caret, and the buttons.
    /// </summary>
    public void Paint(ID2D1DeviceContext* dc, Gfx gfx, QuickTheme theme, in FieldLook look)
    {
        if (!Focused)
        {
            KeepCaretInView(gfx, look);
        }
        var scale = look.Scale;
        var box = PopupPaint.Rect(0, 0, look.Size.X, look.Size.Y);
        var radius = Px(PopupPaint.ControlRadiusDip, scale);
        var hairline = MathF.Max(1, Px(1, scale));
        PopupPaint.Fill(dc, box, radius, Focused ? theme.InputActive : Hot ? theme.ControlFillHover : theme.ControlFill);
        PopupPaint.Stroke(dc, box, radius, hairline, theme.ControlStroke);
        var edge = Focused ? Px(2, scale) : hairline;
        var strip = new D2D_RECT_F(box.left, box.bottom - edge, box.right, box.bottom);
        dc->PushAxisAlignedClip(&strip, D2D1_ANTIALIAS_MODE.D2D1_ANTIALIAS_MODE_ALIASED);
        PopupPaint.Fill(dc, box, radius, Focused ? theme.Accent : theme.StrongStroke);
        dc->PopAxisAlignedClip();

        var (clear, search) = Buttons(look);
        if (Clears)
        {
            if (ClearHot)
            {
                PopupPaint.Fill(dc, clear, radius, theme.SubtleHover);
            }
            PopupPaint.Glyph(dc, gfx, ClearGlyph, ButtonGlyphPx * scale, theme.TextSecondary, clear);
        }
        if (look.Search)
        {
            PopupPaint.Glyph(dc, gfx, SearchGlyph, ButtonGlyphPx * scale, theme.TextSecondary, search);
        }

        var (left, right) = TextArea(look);
        var area = new D2D_RECT_F(left - MathF.Max(1, Px(1, scale)), 0, right, look.Size.Y);
        var text = Drawn;
        dc->PushAxisAlignedClip(&area, D2D1_ANTIALIAS_MODE.D2D1_ANTIALIAS_MODE_ALIASED);
        try
        {
            if (text.Length == 0)
            {
                if (look.Placeholder.Length > 0)
                {
                    var size = PopupPaint.Measure(gfx, look.Placeholder, look.TextPx, Weight.Regular, right - left);
                    var from = look.Mirrored ? right - size.X : left;
                    PopupPaint.Text(dc, gfx, look.Placeholder, look.TextPx, Weight.Regular, theme.TextSecondary, from, PopupPaint.Round((look.Size.Y - size.Y) / 2), right - left);
                }
                if (Focused && CaretOn)
                {
                    Caret(dc, theme, look, look.Mirrored ? right - MathF.Max(1, Px(1, scale)) : left, left);
                }
                return;
            }
            var layout = gfx.TextLayout(text, look.TextPx, Weight.Regular, null);
            try
            {
                DWRITE_TEXT_METRICS metrics;
                ComUtil.Check(layout->GetMetrics(&metrics));
                var top = PopupPaint.Round((look.Size.Y - metrics.height) / 2);
                var origin = new D2D_POINT_2F(PopupPaint.Round(left - Scroll), top);
                Draw(dc, layout, origin, theme.Text);
                if (Focused && Editing.HasSelection)
                {
                    var from = origin.x + TextX(layout, Editing.SelectionStart, text.Length);
                    var to = origin.x + TextX(layout, Editing.SelectionStart + Editing.SelectionLength, text.Length);
                    var selection = new D2D_RECT_F(MathF.Max(left, MathF.Min(from, to)), top, MathF.Min(right, MathF.Max(from, to)), top + metrics.height);
                    if (selection.right > selection.left)
                    {
                        PopupPaint.Fill(dc, selection, 0, theme.Selection);
                        dc->PushAxisAlignedClip(&selection, D2D1_ANTIALIAS_MODE.D2D1_ANTIALIAS_MODE_ALIASED);
                        Draw(dc, layout, origin, theme.SelectionText);
                        dc->PopAxisAlignedClip();
                    }
                }
                else if (Focused && CaretOn)
                {
                    Caret(dc, theme, look, origin.x + TextX(layout, Editing.Caret, text.Length), left);
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
    }

    /// <summary>The caret at the edge <paramref name="x"/>; at the words' start, on the pixels just before it, clear of the first letter's stem.</summary>
    private static void Caret(ID2D1DeviceContext* dc, QuickTheme theme, in FieldLook look, float x, float left)
    {
        var width = MathF.Max(1, Px(1, look.Scale));
        var inset = Px(7, look.Scale);
        var at = PopupPaint.Round(x);
        if (at <= PopupPaint.Round(left))
        {
            at -= width;
        }
        PopupPaint.Fill(dc, new D2D_RECT_F(at, inset, at + width, look.Size.Y - inset), 0, theme.Text);
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

    /// <summary>The x of the edge before character <paramref name="index"/> in the laid-out words; words laid right to left start at their right, and DirectWrite says where.</summary>
    internal static float TextX(IDWriteTextLayout* layout, int index, int length)
    {
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
        if (index >= length)
        {
            DWRITE_TEXT_METRICS metrics;
            return layout->GetMetrics(&metrics).SUCCEEDED ? metrics.widthIncludingTrailingWhitespace : 0;
        }
        float x;
        float y;
        DWRITE_HIT_TEST_METRICS hit;
        return layout->HitTestTextPosition((uint)index, BOOL.FALSE, &x, &y, &hit).SUCCEEDED ? x : 0;
    }

    /// <summary>The caret's edge in the field's px, for the IME's composition window: where the next letter goes.</summary>
    public float CaretX(Gfx gfx, in FieldLook look)
    {
        var (left, right) = TextArea(look);
        var text = Drawn;
        if (text.Length == 0)
        {
            return look.Mirrored ? right : left;
        }
        var layout = gfx.TextLayout(text, look.TextPx, Weight.Regular, null);
        try
        {
            return PopupPaint.Round(left - Scroll) + TextX(layout, Editing.Caret, text.Length);
        }
        finally
        {
            layout->Release();
        }
    }
}
