// A list line whose items scroll inside it (the person, 2026-10-11: "if content is cut off by the
// fixed height the widgets should always be scrollable in list view. dont just show x more items,
// let the user scroll down to see all of the other items too.").
//
// It replaces "N more" (WidgetFill's summary row): a line of items given less than its items take is
// laid out at the height that shows every one of them, and shows a window of that, moved by the
// wheel or the touchpad. One mechanism for every such line, kept in CellIsland, so the drawing, the
// hit testing and the pointer read the same offset: a widget lays itself out at its full height as
// it would in a line that tall and never learns it is scrolled.
//
//   * **What scrolls** is the widget's answer (IListScroll.ScrollHeight): its whole content at the
//     line's width, nothing summarised, where at that height it lists its items at all — a line of
//     one row keeps its one-row form, which is a reading and not a list.
//   * **The head stays** (IListScroll.ScrollHead): the count over the mail, the clipboard's search
//     box, the notifications' "Clear all" are drawn where they are and the items pass under them,
//     as a Windows 11 list scrolls under its header.
//   * **The foot stays** (IListScroll.ScrollFoot), as the head does, at the line's bottom: the to-do
//     list's "Add a task", the news' "Open news.example.com" (2026-10-11: "the open xyz in the rss
//     feed list should always be shown and not scroll with contents"). It is the end of the content,
//     drawn where the content ends when it is scrolled all the way, so scrolled to the end nothing
//     moves; before that the items pass under it.
//   * **The side stays** (IListScroll.ScrollAside), §655: a band at the line's trailing side under the head — Windows
//     search's pane of the result chosen, beside its list — stays where it is while the items beside it scroll, as
//     Windows 11's search keeps its preview while its results scroll. The items show through the window left of it.
//   * **Nested scrolling** is Windows' own (WinUI's scroll chaining): the wheel over a line scrolls
//     its items until they reach their end, and the next turn the same way scrolls the folder.
//   * **Smooth**, like a Windows 11 list: a notch moves the items by a few lines, eased over a short
//     run of frames by the folder (FolderFlyout's scroll timer); a touchpad's fine steps move them by
//     what it says.
//   * **The overlay scrollbar** is Windows 11's thin one: a 2-DIP rounded bar at the line's trailing
//     edge, over the content, shown while the pointer is over the line, its length the share shown.

using TerraFX.Interop.DirectX;

namespace Karpo.Dock;

/// <summary>A list line whose items scroll inside it where it is given less than they take (see the top of the file).</summary>
internal interface IListScroll
{
    /// <summary>
    /// The height a line <paramref name="width"/> px wide lays its content out at when it is given
    /// <paramref name="height"/> px: every item, nothing summarised, its own padding included, where
    /// at that height it shows its items; <paramref name="height"/> or less where it does not scroll
    /// there — its one-row form, or content that fits.
    /// </summary>
    float ScrollHeight(float width, float height, in TileStyle style);

    /// <summary>
    /// The band at the line's top that stays while the rest scrolls, in px, for a line laid out
    /// <paramref name="content"/> px tall: its head. 0 where all of it scrolls.
    /// </summary>
    float ScrollHead(float width, float content, in TileStyle style) => 0;

    /// <summary>
    /// The band at the end of the content that stays at the line's bottom while the rest scrolls, in
    /// px, for a line laid out <paramref name="content"/> px tall: its foot. 0 where all of it scrolls.
    /// </summary>
    float ScrollFoot(float width, float content, in TileStyle style) => 0;

    /// <summary>
    /// §655: the band at the line's trailing side, under the head, that stays while the rest scrolls, in px wide, for a line
    /// <paramref name="width"/> px wide: its side pane. 0 where all of it scrolls.
    /// </summary>
    float ScrollAside(float width, in TileStyle style) => 0;

    /// <summary>
    /// A band to bring into sight, in px from the content's top — a row just opened, or chosen by the
    /// keys — answered once and then null until there is another; null by default.
    /// </summary>
    (float Top, float Bottom)? ScrollReveal(float width, in TileStyle style) => null;
}

/// <summary>Where a scrolled line stands: the box its content is laid out in, and the band that stays.</summary>
/// <param name="Box">The line's box (px), as it was given.</param>
/// <param name="Content">The content's full height, in px; at most the box's where it does not scroll.</param>
/// <param name="Head">The band at the top that stays, in px.</param>
/// <param name="Offset">How far the content is scrolled, in px, held to <see cref="Most"/>.</param>
/// <param name="Foot">The band at the bottom that stays, in px: the content's last.</param>
/// <param name="Aside">§655: the band at the trailing side under the head that stays, in px wide: a side pane.</param>
internal readonly record struct ListScrolled(D2D_RECT_F Box, float Content, float Head, float Offset, float Foot = 0, float Aside = 0)
{
    /// <summary>Whether there is anything to scroll.</summary>
    public bool Scrolls => Most > 0;

    /// <summary>How far the content can scroll, in px.</summary>
    public float Most => MathF.Max(0, Content - (Box.bottom - Box.top));

    /// <summary>The content's box where it is not scrolled: what the head is drawn in.</summary>
    public D2D_RECT_F Still => Box with { bottom = Box.top + MathF.Max(Content, Box.bottom - Box.top) };

    /// <summary>The content's box moved up by the offset: what the items are drawn in.</summary>
    public D2D_RECT_F Moved => Scrolls ? new D2D_RECT_F(Box.left, Box.top - Offset, Box.right, Box.top - Offset + Content) : Box;

    /// <summary>The content's box scrolled all the way, its end on the line's bottom: what the foot is drawn in.</summary>
    public D2D_RECT_F End => Scrolls ? new D2D_RECT_F(Box.left, Box.bottom - Content, Box.right, Box.bottom) : Box;

    /// <summary>The part of the line the items show through: under the head, over the foot, and §655 short of the side that stays.</summary>
    public D2D_RECT_F Window
    {
        get
        {
            var top = MathF.Min(Box.bottom, Box.top + Head);
            return Box with { top = top, bottom = MathF.Max(top, Box.bottom - Foot), right = MathF.Max(Box.left, Box.right - Aside) };
        }
    }

    /// <summary>§655: the band at the trailing side under the head that stays (a side pane); empty where it has none.</summary>
    public D2D_RECT_F AsideBand => Aside > 0 ? Box with { left = Window.right, top = Window.top } : Box with { left = Box.right };

    /// <summary>The band at the line's bottom the foot stays in; empty where it has none.</summary>
    public D2D_RECT_F FootBand => Box with { top = Window.bottom };

    /// <summary>
    /// The box a point at <paramref name="point"/> (px, in the line's space) is read in: the still one over the head and §655
    /// in the side that stays, the end under the foot, the moved one between.
    /// </summary>
    public D2D_RECT_F At(System.Numerics.Vector2 point) =>
        !Scrolls ? Box
        : point.Y < Box.top + Head || (Aside > 0 && point.X >= Window.right) ? Still
        : Foot > 0 && point.Y >= Window.bottom ? End
        : Moved;

    /// <summary>
    /// The offset that shows the band from <paramref name="top"/> to <paramref name="bottom"/> (px from
    /// the content's top) under the head, moving as little as it can: its foot brought up where it
    /// is below the window, its top brought down where it is above it — its top winning where it is
    /// taller than the window. Held to what there is to scroll.
    /// </summary>
    public float Revealing(float top, float bottom)
    {
        var height = Box.bottom - Box.top - Foot;
        var offset = Offset;
        if (bottom > offset + height)
        {
            offset = bottom - height;
        }
        if (top < offset + Head)
        {
            offset = top - Head;
        }
        return Math.Clamp(offset, 0, Most);
    }


    /// <summary>
    /// The overlay scrollbar's thumb (px): at the line's trailing edge, inside the window the items
    /// show through, as long as the share of the content shown — never under 24 DIP — and as far down
    /// as the offset is through what can scroll. Empty where nothing scrolls.
    /// </summary>
    public D2D_RECT_F Thumb(in TileStyle style)
    {
        if (!Scrolls)
        {
            return default;
        }
        var window = Window;
        var inset = style.Px(3);
        var thick = MathF.Max(2, style.Px(2));
        var track = MathF.Max(0, (window.bottom - window.top) - (2 * inset));
        var height = Box.bottom - Box.top;
        var length = MathF.Min(track, MathF.Max(style.Px(24), track * height / MathF.Max(1, Content)));
        var top = window.top + inset + ((track - length) * (Offset / Most));
        var right = window.right - inset;
        return new D2D_RECT_F(right - thick, top, right, top + length);
    }

    /// <summary>How far one notch of the wheel moves the items, in px: three lines of the list's text, never over most of what shows.</summary>
    public static float Notch(in TileStyle style, float shown) =>
        MathF.Max(1, MathF.Min(style.Px(48) * style.TextScale, shown * 0.8f));
}
