// An app's open windows with live previews, beside its tile: the taskbar's
// thumbnail flyout, drawn as a Windows 11 flyout. DWM draws the previews
// straight into this window, so nothing is copied and nothing here redraws
// while a video plays in one.
//
// Each window is a cell, and the cells meet: a hovered one lights up out to
// the popup's edges, which round its outer corners off. Resting on a cell
// peeks its window (see LivePeek).
//
// Under a window's preview stands its thumbnail toolbar, as the app made it
// (ITaskbarList3::ThumbBarAddButtons, ThumbBar.cs): its buttons with their
// pictures and flags, and a click sent back to the window the way the taskbar
// sends it. A button's tooltip is kept as its name but never shown: the person
// asked for none over the buttons (2026-10-09: Spotify's media buttons), for
// every app's toolbar alike. Only a window whose app made no toolbar gets Karpo's own
// three buttons for its media session, which follow the session while the
// popup is open: the app's own buttons are what Windows shows, and the two
// together would be two rows of the same controls. The app's thumbnail tooltip
// is the card's title where it set one, and its clip is all of the window the
// preview shows.

using System.Diagnostics;
using System.Numerics;
using Karpo.Dock.Native;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using Windows.UI.Composition;
using Windows.UI.Composition.Desktop;
using static TerraFX.Interop.Windows.Windows;
using Color = Windows.UI.Color;
using Edge = Karpo.Core.Edge;
using ICompositorDesktopInterop = TerraFX.Interop.WinRT.ICompositorDesktopInterop;
using IDesktopWindowTarget = TerraFX.Interop.WinRT.IDesktopWindowTarget;

namespace Karpo.Dock;

public enum PreviewEventKind
{
    Nothing,
    Entered,
    Left,
    Activate,
    Close,

    /// <summary>A thumbnail toolbar's button that dismisses the list was clicked: the list closes.</summary>
    Dismiss,

    /// <summary>A card was right-clicked: its window's own menu opens (the window menu, as Shift+right click on its button).</summary>
    Menu,
}

/// <summary>What a message to the popup means for the dock.</summary>
public readonly record struct PreviewEvent(PreviewEventKind Kind, HWND Window = default)
{
    public static PreviewEvent Nothing { get; } = new(PreviewEventKind.Nothing);
    public static PreviewEvent Entered { get; } = new(PreviewEventKind.Entered);
    public static PreviewEvent Left { get; } = new(PreviewEventKind.Left);

    public static PreviewEvent Activate(HWND w) => new(PreviewEventKind.Activate, w);

    public static PreviewEvent Close(HWND w) => new(PreviewEventKind.Close, w);

    public static PreviewEvent Dismiss { get; } = new(PreviewEventKind.Dismiss);

    public static PreviewEvent Menu(HWND w) => new(PreviewEventKind.Menu, w);
}

public sealed unsafe class Preview : IDisposable
{
    /// <summary>The popup's window class. The dock registers it with its own window procedure.</summary>
    public const string ClassName = "karpo.preview";

    /// <summary>The popup's timers, which the dock hands to <see cref="Timer"/>: the peek's delay.</summary>
    public const nuint PeekTimer = 1;

    /// <summary>The popup's timers: a media session changed, to be shown.</summary>
    public const nuint MediaTimer = 2;

    /// <summary>Segoe Fluent Icons' ChromeClose, the caption bar's close cross.</summary>
    private static readonly string CloseGlyph = char.ConvertFromUtf32(0xE8BB);

    // Segoe Fluent Icons: Previous, Play, Pause and Next, as the taskbar's
    // thumbnail toolbar shows them.
    private static readonly string[] MediaGlyphs =
    [
        char.ConvertFromUtf32(0xE892),
        char.ConvertFromUtf32(0xE768),
        char.ConvertFromUtf32(0xE769),
        char.ConvertFromUtf32(0xE893),
    ];

    // The hover marks fade like the dock's highlights.

    public HWND Hwnd { get; }

    /// <summary>peek: DWM took the list as one of the windows a peek leaves in sight (LivePeek.StaysThrough).</summary>
    public bool StaysThroughPeek { get; }

    /// <summary>The tile whose windows are listed, while open.</summary>
    public int? Tile { get; private set; }

    /// <summary>The windows listed, as they were given, while open; none while hidden.</summary>
    public IReadOnlyList<(HWND Hwnd, string Title)> Windows => shown?.Windows ?? [];

    /// <summary>
    /// The pointer is over the list: its cards, or the gap between them and the button it opened
    /// from. Not the room around them the shadow is drawn in, which is the popup's window too but
    /// shows nothing: a pointer resting there has left the list, as it has on the taskbar
    /// (<see cref="ListArea"/>, <see cref="Bridge"/>).
    /// </summary>
    public bool Inside { get; private set; }

    /// <summary>
    /// Where the list is, in the popup's own coordinates: the panel. The window reaches the
    /// shadow's room further on the three sides away from the bar, and that room is not the list.
    /// </summary>
    internal RECT ListArea { get; private set; }

    /// <summary>
    /// The way from the button up to its list, in the popup's own coordinates: the gap between the
    /// panel and the window's edge on the bar's side, as wide as the button the list opened from,
    /// so the pointer going from the button to its list never leaves it. Only that wide: the
    /// window's edge there reaches over the bar a few px, and beside the button that strip is the
    /// bar's, so a pointer resting on it has left the list.
    /// </summary>
    internal RECT Bridge { get; private set; }

    private readonly DesktopWindowTarget target;
    private readonly List<Card> cards = [];
    private readonly List<Visual> highlights = [];

    // The red backing and the cross, apart: the cross shows on a hovered
    // card, the backing only under the pointer.
    private readonly List<(Visual Backing, SpriteVisual Glyph)> closers = [];

    // Per card: its media buttons, or null for a window without a session.
    private readonly List<MediaRow?> mediaRows = [];

    // Per card: its app's thumbnail toolbar as drawn, or null for a window without one; a card has
    // this or a media row, never both.
    private readonly List<ThumbRow?> thumbRows = [];

    // Where a window's toolbar, tooltip and clip come from, and where a click on a toolbar button goes.
    private Func<HWND, ThumbInfo?>? thumbs;
    private IThumbSender sender = ThumbSender.Instance;

    // The cards' marks: highlights, close buttons, row buttons (tests count what shows).
    private ContainerVisual? marks;

    private Spot? hovered;

    // The cross in the text colour, and white on the red backing. Two
    // surfaces shared by every card, swapped per card: redrawing one in
    // place would recolour the cross on every card showing it, and would
    // draw again on each hover.
    private CompositionBrush? crossBrush;
    private CompositionBrush? crossHotBrush;

    // The media glyphs by (glyph, enabled), shared by every row of a showing.
    private readonly Dictionary<(int Glyph, bool Enabled), CompositionBrush> mediaBrushes = [];

    // Where a window's session comes from, and whether the popup follows the
    // sessions' changes.
    private Func<HWND, MediaControls?>? media;
    private bool watching;

    // What the showing was made from: a card that gains or loses its media
    // row lays the popup out again.
    private Shown? shown;
    private Color accent;

    // preview: the dock window whose tile opened this list; it stays on top through a peek.
    private HWND bar;

    // Every composition object of the tree on screen, root included. Rust
    // frees the old tree the moment the new root replaces it; a projected
    // object only lets go when the GC finalizes it, and a dock allocates so
    // little that the GC may not run for hours, so each preview opened would
    // leave its surfaces behind. They are closed by hand instead.
    private List<IDisposable> made = [];

    /// <summary>Counts hides, so a hide's late letting go of its tree knows a showing came since.</summary>
    private uint hides;
    private bool disposed;

    /// <summary>One window's card: its preview, where it is, its close button, and the tooltip and clip it was shown with.</summary>
    private readonly record struct Card(HWND Source, RECT Bounds, RECT Close, string? Tooltip = null, ThumbClip? Clip = null);

    /// <summary>What the pointer is on: a card, its close button, or one of its media buttons (0 to 2; -1 for none).</summary>
    private readonly record struct Spot(int Index, bool OnClose, int Button);

    private sealed class MediaRow
    {
        public required MediaControls Controls;
        public required SpriteVisual[] Glyphs;
        public required Visual[] Fills;
        public required RECT[] Bounds;
    }

    /// <summary>A card's thumbnail toolbar as drawn: its shown buttons, and per button its picture, hover fill and place.</summary>
    private sealed class ThumbRow
    {
        public required IReadOnlyList<ThumbButton> Buttons;
        public required SpriteVisual[] Pictures;
        public required Visual[] Fills;
        public required RECT[] Bounds;
    }

    private sealed record Shown(Gfx Gfx, Look Look, float Scale, IReadOnlyList<(HWND Hwnd, string Title)> Windows, POINT Anchor, RECT Work, Edge Edge, HWND Bar, int ButtonSpan);

    /// <param name="dock">Goes into the popup's GWLP_USERDATA, so the dock's window
    /// procedure finds the dock from the popup's messages too.</param>
    /// <param name="dwm">Where previews are asked for: DWM, unless a test stands in for it.</param>
    public Preview(Gfx gfx, HINSTANCE instance, nint dock, IDwmThumbnails? dwm = null)
    {
        this.dwm = dwm ?? DwmThumbnails.Instance;
        fixed (char* className = ClassName)
        fixed (char* title = "Karpo windows") // not-ui: a window's title
        {
            Hwnd = CreateWindowExW(
                WS.WS_EX_TOOLWINDOW | WS.WS_EX_TOPMOST | WS.WS_EX_NOACTIVATE | WS.WS_EX_NOREDIRECTIONBITMAP,
                className,
                title,
                WS.WS_POPUP,
                0,
                0,
                1,
                1,
                HWND.NULL,
                HMENU.NULL,
                instance,
                null);
        }
        if (Hwnd == HWND.NULL)
        {
            throw Failed(nameof(CreateWindowExW));
        }
        _ = SetWindowLongPtrW(Hwnd, GWLP.GWLP_USERDATA, dock);
        // Without it the host backdrop brush has nothing to show.
        BOOL on = BOOL.TRUE;
        _ = DwmSetWindowAttribute(
            Hwnd,
            (uint)DWMWINDOWATTRIBUTE.DWMWA_USE_HOSTBACKDROPBRUSH,
            &on,
            (uint)sizeof(BOOL));
        // preview: the list is on screen the moment it is shown. DWM gives a popup a window
        // transition of its own unless told otherwise, which ran on top of the wait before it; the
        // list fades itself in instead, briefly, and only where animations are on (Show).
        _ = DwmSetWindowAttribute(
            Hwnd,
            (uint)DWMWINDOWATTRIBUTE.DWMWA_TRANSITIONS_FORCEDISABLED,
            &on,
            (uint)sizeof(BOOL));
        // peek: the list stays up while its card's window is peeked, as Windows' own does.
        StaysThroughPeek = LivePeek.StaysThrough(Hwnd);
        try
        {
            target = CreateTarget(gfx.Compositor, Hwnd);
        }
        catch
        {
            // The Rust leaves the window behind here. Unhooked from the dock
            // first, so its destruction is not routed to a dock that never
            // got a preview.
            _ = SetWindowLongPtrW(Hwnd, GWLP.GWLP_USERDATA, 0);
            Surfaces.Destroy(Hwnd);
            throw;
        }
    }

    public bool IsOpen => Tile.HasValue;

    /// <summary>
    /// How long the last showing's previews have spent in DWM's own calls so far, in ms: registering
    /// each window's thumbnail, asking its size and placing it, off the dock's thread
    /// (<see cref="ThumbnailBatch"/>). Registering waits for DWM to present its next frame, so this
    /// is the compositor's time rather than Karpo's, and it grows with how busy the desktop is. The
    /// smoke says it apart from the opening (Smoke.Opening), which no longer includes it.
    /// </summary>
    internal double DwmMs => batch?.DwmMs ?? 0;

    /// <summary>How many of the last showing's previews DWM registered so far.</summary>
    internal int PreviewsShown => batch?.Registered ?? 0;

    /// <summary>Finishes once the last showing's previews were all asked of DWM (or let go).</summary>
    internal Task Previews => batch?.Done ?? Task.CompletedTask;

    /// <summary>
    /// tests: where card <paramref name="card"/>'s toolbar button <paramref name="slot"/> stands in
    /// the popup's window, and the button it is; null where the card has no such button (a media
    /// row's buttons included, which are not the app's).
    /// </summary>
    internal (RECT Bounds, ThumbButton Button)? ToolbarButton(int card, int slot) =>
        card >= 0 && card < thumbRows.Count && thumbRows[card] is { } row && slot >= 0 && slot < row.Bounds.Length
            ? (row.Bounds[slot], row.Buttons[slot])
            : null;

    /// <summary>tests: where card <paramref name="card"/> stands in the popup; null past the last.</summary>
    internal RECT? CardBounds(int card) => card >= 0 && card < cards.Count ? cards[card].Bounds : null;

    /// <summary>tests: whether card <paramref name="card"/> shows Karpo's own media buttons.</summary>
    internal bool MediaRowAt(int card) => card >= 0 && card < mediaRows.Count && mediaRows[card] is not null;

    /// <summary>
    /// tests: the marks over the cards that are visible — a tooltip would be one more under the
    /// pointer; a hover fill only changes its opacity.
    /// </summary>
    internal int VisibleMarks => marks is { } container ? container.Children.Count(visual => visual.IsVisible) : 0;

    /// <summary>
    /// Lists <paramref name="windows"/> (handle and title) beside <paramref name="anchor"/>, the
    /// middle of the tile's inner side on screen, on the side away from <paramref name="edge"/>:
    /// above it for a dock at the bottom, below for the top, right of it for the left, left of it
    /// for the right. Centred on the anchor along the edge, and kept inside <paramref name="work"/>.
    /// <paramref name="media"/> gives a window's media session, if it has one; the popup then
    /// follows <see cref="MediaSessions.Changed"/> while it is open. <paramref name="thumbs"/> gives
    /// what a window's app said of its preview — its toolbar, tooltip and clip — and
    /// <paramref name="sender"/> is where a click on a toolbar button goes (the taskbar's way unless
    /// a test says otherwise).
    /// </summary>
    public void Show(
        Gfx gfx,
        Look look,
        float scale,
        int tile,
        IReadOnlyList<(HWND Hwnd, string Title)> windows,
        POINT anchor,
        RECT work,
        Edge edge = Edge.Bottom,
        Func<HWND, MediaControls?>? media = null,
        HWND bar = default,
        Func<HWND, ThumbInfo?>? thumbs = null,
        IThumbSender? sender = null,
        int buttonSpan = 0)
    {
        // preview: a list coming up from nothing fades in; one swapped for the next button's, or
        // laid out again as its windows change, is simply there.
        var appearing = !IsOpen;
        Release();
        this.media = media;
        this.thumbs = thumbs;
        this.sender = sender ?? ThumbSender.Instance;
        // preview: the bar this list belongs to, which a peek must leave standing (LivePeek.Show).
        this.bar = bar;
        if (windows.Count == 0)
        {
            HideNow();
            return;
        }
        var palette = Palette.For(look);
        float Px(float dip) => Round(dip * scale);
        var (thumbW, thumbH) = (Px(216), Px(124));
        float inner = Px(8);
        float close = Px(32);
        float cardW = thumbW + 2 * inner;
        float radius = Px(8);
        float buttonRadius = Px(4);
        float button = Px(32);
        float buttonGap = Px(4);
        // Room for the shadow around the panel, as the dock leaves.
        float room = Px(28);
        // Between the tile and the panel, as between an item and its tooltip.
        float away = Px(8);
        float margin = Px(8);
        float blur = Px(20);
        float drop = Px(6);
        float textScale = look.TextScale > 0 ? look.TextScale : 1;
        float titlePx = 12 * textScale * scale;
        float titleRoom = cardW - inner - close - Px(4);
        bool vertical = edge is Edge.Left or Edge.Right;

        // Asked once per showing; a session that comes or goes later lays
        // the popup out again (MediaChanged). A window whose app made a
        // toolbar shows that instead of Karpo's media buttons.
        var infos = new ThumbInfo?[windows.Count];
        var sessions = new MediaControls?[windows.Count];
        var anyRow = false;
        for (var index = 0; index < windows.Count; index++)
        {
            infos[index] = AskThumbs(thumbs, windows[index].Hwnd);
            sessions[index] = infos[index] is { Shown.Count: > 0 } ? null : Ask(media, windows[index].Hwnd);
            anyRow |= sessions[index] is not null || infos[index] is { Shown.Count: > 0 };
        }

        var made = new List<IDisposable>();
        var previews = new List<ThumbnailBatch.Request>(windows.Count);
        T Keep<T>(T value)
            where T : IDisposable
        {
            made.Add(value);
            return value;
        }

        try
        {
            var c = gfx.Compositor;

            // The titles first: the tallest one decides the title row, which a
            // larger text size makes taller than the close button.
            var labels = new List<(CompositionDrawingSurface Surface, Vector2 Size)>(windows.Count);
            float lineH = 0;
            for (var index = 0; index < windows.Count; index++)
            {
                // The app's thumbnail tooltip, where it set one, says what the card is.
                var title = infos[index]?.Tooltip ?? windows[index].Title;
                var label = gfx.Text(title, titlePx, Weight.Regular, palette.Text, titleRoom);
                Keep(label.Surface);
                labels.Add(label);
                lineH = MathF.Max(lineH, label.Size.Y);
            }
            float titleH = MathF.Max(close, MathF.Ceiling(lineH) + Px(8));
            // Every card is as tall as the tallest, so the cells line up.
            float mediaH = anyRow ? button + inner : 0;
            float cardH = titleH + thumbH + inner + mediaH;

            // Cards run along the edge, as the taskbar lists them on a
            // horizontal taskbar and stacks them on a vertical one, and wrap
            // away from the edge when the screen runs out. They meet: no
            // padding around them and no gap between them.
            float along = vertical ? cardH : cardW;
            float length = vertical ? work.bottom - work.top : work.right - work.left;
            float fits = MathF.Floor((length - 2 * margin) / along);
            // Written so NaN lands on one card, as Rust's f32::max(1.0) does.
            int perLane = fits >= 1 ? (int)fits : 1;
            perLane = Math.Min(perLane, windows.Count);
            int lanes = (windows.Count + perLane - 1) / perLane;
            int columns = vertical ? lanes : perLane;
            int rows = vertical ? perLane : lanes;
            float width = columns * cardW;
            float height = rows * cardH;

            int w = (int)width;
            int h = (int)height;
            int apart = (int)away;
            int keep = (int)margin;
            int x;
            int y;
            switch (edge)
            {
                case Edge.Top:
                    x = AlongEdge(anchor.x - w / 2, work.left, work.right, w, keep);
                    y = Math.Min(Math.Max(anchor.y + apart, work.top), work.bottom - h);
                    break;
                case Edge.Left:
                    x = Math.Min(anchor.x + apart, work.right - w);
                    y = AlongEdge(anchor.y - h / 2, work.top, work.bottom, h, keep);
                    break;
                case Edge.Right:
                    x = Math.Max(anchor.x - apart - w, work.left);
                    y = AlongEdge(anchor.y - h / 2, work.top, work.bottom, h, keep);
                    break;
                default:
                    x = AlongEdge(anchor.x - w / 2, work.left, work.right, w, keep);
                    y = Math.Max(Math.Min(anchor.y - apart, work.bottom) - h, work.top);
                    break;
            }

            // The window reaches only as far as the tile on the edge's side:
            // it is topmost, and anything of it over the dock would take the
            // dock's mouse. The other three sides leave the shadow its room.
            var (roomLeft, roomTop, roomRight, roomBottom) = edge switch
            {
                Edge.Top => (room, away, room, room),
                Edge.Left => (away, room, room, room),
                Edge.Right => (room, room, away, room),
                _ => (room, room, room, away),
            };
            // §580: never past the room it was placed in, as a flyout's (FlyoutHost.RoomInside): the window takes the
            // pointer, and a list pushed against an end of the work area held a bar beside it under its shadow's room.
            (roomLeft, roomTop, roomRight, roomBottom) = FlyoutHost.RoomInside((roomLeft, roomTop, roomRight, roomBottom), x, y, w, h, work);
            var root = Keep(c.CreateContainerVisual());
            root.Size = new Vector2(width + roomLeft + roomRight, height + roomTop + roomBottom);

            if (palette.ShadowOpacity > 0)
            {
                // The window edge on the tile's side is close to the panel, and
                // a shadow cut there shows as a hard line wherever the dock is
                // not below it. The shape casting it stops short on that side,
                // so what spills past the panel has faded out by the edge.
                float spill = edge switch
                {
                    Edge.Top => blur - drop,
                    Edge.Left or Edge.Right => blur,
                    _ => blur + drop,
                };
                float inset = MathF.Max(0, spill - away);
                var (castLeft, castTop, castW, castH) = edge switch
                {
                    Edge.Top => (roomLeft, roomTop + inset, width, height - inset),
                    Edge.Left => (roomLeft + inset, roomTop, width - inset, height),
                    Edge.Right => (roomLeft, roomTop, width - inset, height),
                    _ => (roomLeft, roomTop, width, height - inset),
                };
                if (castW > 0 && castH > 0)
                {
                    // A rounded shape to cast from: the default policy casts
                    // from the visual's square bounds. Drawn in the panel's own
                    // colour, under the panel, in case it ever shows.
                    var outline = Shape(c, made, 0, 0, castW, castH, radius, Keep(c.CreateColorBrush(palette.Base)), 0);
                    var picture = Keep(c.CreateVisualSurface());
                    picture.SourceVisual = outline;
                    picture.SourceSize = new Vector2(castW, castH);
                    var shadow = Keep(c.CreateDropShadow());
                    shadow.SourcePolicy = CompositionDropShadowSourcePolicy.InheritFromVisualContent;
                    shadow.BlurRadius = blur;
                    shadow.Offset = new Vector3(0, drop, 0);
                    shadow.Color = Rgba(0, 0, 0, 1);
                    shadow.Opacity = palette.ShadowOpacity;
                    var caster = Keep(c.CreateSpriteVisual());
                    caster.Size = new Vector2(castW, castH);
                    caster.Offset = new Vector3(castLeft, castTop, 0);
                    caster.Brush = Keep(c.CreateSurfaceBrush(picture));
                    caster.Shadow = shadow;
                    root.Children.InsertAtTop(caster);
                }
            }

            // The flyout material: the dock's panel.
            var panel = Keep(c.CreateSpriteVisual());
            panel.Size = new Vector2(width, height);
            panel.Offset = new Vector3(roomLeft, roomTop, 0);
            var corners = Keep(c.CreateRoundedRectangleGeometry());
            corners.Size = new Vector2(width, height);
            corners.CornerRadius = new Vector2(radius, radius);
            panel.Clip = Keep(c.CreateGeometricClip(corners));
            if (palette.Glass)
            {
                panel.Brush = Keep(c.CreateHostBackdropBrush());
                var tint = Keep(c.CreateSpriteVisual());
                tint.RelativeSizeAdjustment = new Vector2(1, 1);
                tint.Brush = Keep(c.CreateColorBrush(palette.Tint));
                panel.Children.InsertAtTop(tint);
            }
            else
            {
                panel.Brush = Keep(c.CreateColorBrush(palette.Base));
            }
            root.Children.InsertAtTop(panel);

            // The cells' marks, in panel coordinates and clipped to the
            // panel's rounded shape: a highlight reaching an edge of the popup
            // rounds off with it.
            var cells = Keep(c.CreateContainerVisual());
            cells.Size = new Vector2(width, height);
            cells.Offset = new Vector3(roomLeft, roomTop, 0);
            var cellShape = Keep(c.CreateRoundedRectangleGeometry());
            cellShape.Size = new Vector2(width, height);
            cellShape.CornerRadius = new Vector2(radius, radius);
            cells.Clip = Keep(c.CreateGeometricClip(cellShape));
            root.Children.InsertAtTop(cells);
            marks = cells;
            cardsOrigin = new Vector2(roomLeft, roomTop);

            var rim = Shape(
                c,
                made,
                roomLeft,
                roomTop,
                width,
                height,
                radius,
                Keep(c.CreateColorBrush(palette.Stroke)),
                MathF.Max(Px(palette.StrokeDip), 1));
            root.Children.InsertAtTop(rim);

            ImplicitAnimationCollection? fade = null;
            if (look.Animations)
            {
                var linear = Keep(MotionEase.Of(c, MotionKind.Hover));
                var toward = Keep(c.CreateScalarKeyFrameAnimation());
                toward.Target = "Opacity";
                toward.InsertExpressionKeyFrame(1, "this.FinalValue", linear);
                toward.Duration = look.Motion.Time(MotionKind.Hover);
                fade = Keep(c.CreateImplicitAnimationCollection());
                fade["Opacity"] = toward;
            }
            void Fading(Visual visual)
            {
                visual.Opacity = 0;
                if (fade is not null)
                {
                    visual.ImplicitAnimations = fade;
                }
            }

            var hover = Keep(c.CreateColorBrush(palette.Hover));
            float outlined = palette.HoverOutlined ? MathF.Max(Px(2), 1) : 0;
            var danger = Keep(c.CreateColorBrush(palette.CloseHot));
            var cross = Keep(c.CreateSurfaceBrush(Keep(gfx.Glyph(CloseGlyph, 10 * scale, palette.Text, close))));
            var crossHot = Keep(c.CreateSurfaceBrush(Keep(gfx.Glyph(CloseGlyph, 10 * scale, palette.CloseGlyphHot, close))));
            mediaBrushes.Clear();
            CompositionBrush MediaBrush(int glyph, bool enabled)
            {
                if (!mediaBrushes.TryGetValue((glyph, enabled), out var brush))
                {
                    var surface = Keep(gfx.Glyph(MediaGlyphs[glyph], 16 * scale, enabled ? palette.Text : palette.Disabled, button));
                    brush = Keep(c.CreateSurfaceBrush(surface));
                    mediaBrushes[(glyph, enabled)] = brush;
                }
                return brush;
            }

            int windowW = (int)(width + roomLeft + roomRight);
            int windowH = (int)(height + roomTop + roomBottom);
            ListArea = new RECT
            {
                left = (int)roomLeft,
                top = (int)roomTop,
                right = (int)(roomLeft + width),
                bottom = (int)(roomTop + height),
            };
            Bridge = BridgeOf(ListArea, windowW, windowH, edge, anchor.x - (x - (int)roomLeft), anchor.y - (y - (int)roomTop), buttonSpan);
            // Placed now; shown once the cards are on the screen (Surfaces, §354), a popup over the bars (ZPlan, §359).
            if (!Surfaces.Place(Hwnd, x - (int)roomLeft, y - (int)roomTop, windowW, windowH))
            {
                throw Failed("SetWindowPos");
            }
            Surfaces.Enlist(Hwnd, Stratum.Popup);
            Surfaces.Show(Hwnd);

            for (int index = 0; index < windows.Count; index++)
            {
                var source = windows[index].Hwnd;
                int lane = index / perLane;
                int step = index % perLane;
                int column = vertical ? lane : step;
                int row = vertical ? step : lane;
                // In the panel; the window's own coordinates add the room.
                float left = column * cardW;
                float top = row * cardH;
                var bounds = Box(roomLeft + left, roomTop + top, cardW, cardH);

                var highlight = Shape(c, made, left, top, cardW, cardH, 0, hover, outlined);
                Fading(highlight);
                cells.Children.InsertAtTop(highlight);

                // The caption bar's close button: flush with the card's corner.
                float closeLeft = left + cardW - close;
                float closeTop = top + Round((titleH - close) / 2);
                var backing = Shape(c, made, closeLeft, closeTop, close, close, buttonRadius, danger, 0);
                Fading(backing);
                cells.Children.InsertAtTop(backing);
                var glyph = Keep(c.CreateSpriteVisual());
                glyph.Size = new Vector2(close, close);
                glyph.Offset = new Vector3(closeLeft, closeTop, 0);
                glyph.Brush = cross;
                Fading(glyph);
                cells.Children.InsertAtTop(glyph);

                var (label, size) = labels[index];
                var caption = Keep(c.CreateSpriteVisual());
                caption.Size = size;
                caption.Brush = Keep(c.CreateSurfaceBrush(label));
                caption.Offset = new Vector3(left + inner, top + Round((titleH - size.Y) / 2), 0);
                cells.Children.InsertAtTop(caption);

                // DWM draws the preview; this only says where, and asks off the dock's thread once
                // every card stands (ThumbnailBatch): DWM may take its time answering.
                {
                    float boxLeft = roomLeft + left + inner;
                    float boxTop = roomTop + top + titleH;
                    var clip = infos[index]?.Clip;
                    previews.Add(new ThumbnailBatch.Request(source, sourceSize =>
                    {
                        // SetThumbnailClip: a part of the client area is all the preview shows.
                        float sw = Math.Max(clip is { } part ? part.Width : sourceSize.cx, 1);
                        float sh = Math.Max(clip is { } high ? high.Height : sourceSize.cy, 1);
                        // A window standing in for a tab has no size of its own: its app's picture of the
                        // tab fills the box.
                        var (tw, th) = clip is null && (sourceSize.cx < 2 || sourceSize.cy < 2) ? (thumbW, thumbH) : Fitted(sw, sh, thumbW, thumbH);
                        var properties = new DWM_THUMBNAIL_PROPERTIES
                        {
                            dwFlags = DWM.DWM_TNP_RECTDESTINATION
                                | DWM.DWM_TNP_VISIBLE
                                | DWM.DWM_TNP_OPACITY
                                | DWM.DWM_TNP_SOURCECLIENTAREAONLY,
                            rcDestination = Box(
                                boxLeft + Round((thumbW - tw) / 2),
                                boxTop + Round((thumbH - th) / 2),
                                tw,
                                th),
                            opacity = 255,
                            fVisible = BOOL.TRUE,
                            fSourceClientAreaOnly = clip is null ? BOOL.FALSE : BOOL.TRUE,
                        };
                        if (clip is { } cut)
                        {
                            properties.dwFlags |= DWM.DWM_TNP_RECTSOURCE;
                            properties.rcSource = new RECT { left = cut.Left, top = cut.Top, right = cut.Right, bottom = cut.Bottom };
                        }
                        return properties;
                    }));
                }

                ThumbRow? thumbRow = null;
                if (infos[index] is { Shown.Count: > 0 } info)
                {
                    // The app's own toolbar: its shown buttons, 32-DIP squares under the preview,
                    // centred on it as Karpo's media buttons are, closer where seven need it.
                    var shownButtons = info.Shown;
                    float rowTop = top + titleH + thumbH + inner;
                    var places = ThumbBar.Place(shownButtons.Count, left + inner, rowTop, thumbW, button, buttonGap);
                    var pictures = new SpriteVisual[shownButtons.Count];
                    var fills = new Visual[shownButtons.Count];
                    var boxes = new RECT[shownButtons.Count];
                    float picturePx = Px(16);
                    for (var slot = 0; slot < shownButtons.Count; slot++)
                    {
                        var place = places[slot];
                        var fill = Shape(c, made, place.X, place.Y, place.Width, place.Height, buttonRadius, hover, outlined);
                        Fading(fill);
                        cells.Children.InsertAtTop(fill);
                        var sprite = Keep(c.CreateSpriteVisual());
                        sprite.Size = new Vector2(place.Width, place.Height);
                        sprite.Offset = new Vector3(place.X, place.Y, 0);
                        sprite.Brush = Keep(Centred(c, Keep(ThumbPicture(gfx, shownButtons[slot], picturePx))));
                        sprite.Opacity = shownButtons[slot].Faint ? DisabledOpacity : 1;
                        cells.Children.InsertAtTop(sprite);
                        pictures[slot] = sprite;
                        fills[slot] = fill;
                        boxes[slot] = Box(roomLeft + place.X, roomTop + place.Y, place.Width, place.Height);
                    }
                    thumbRow = new ThumbRow { Buttons = shownButtons, Pictures = pictures, Fills = fills, Bounds = boxes };
                }

                MediaRow? mediaRow = null;
                if (sessions[index] is { } session)
                {
                    // The thumbnail toolbar's row: three 32-DIP buttons under
                    // the preview, centred on it.
                    float rowW = 3 * button + 2 * buttonGap;
                    float rowLeft = left + Round((cardW - rowW) / 2);
                    float rowTop = top + titleH + thumbH + inner;
                    var glyphs = new SpriteVisual[3];
                    var fills = new Visual[3];
                    var boxes = new RECT[3];
                    for (var slot = 0; slot < 3; slot++)
                    {
                        float buttonLeft = rowLeft + slot * (button + buttonGap);
                        var fill = Shape(c, made, buttonLeft, rowTop, button, button, buttonRadius, hover, outlined);
                        Fading(fill);
                        cells.Children.InsertAtTop(fill);
                        var sprite = Keep(c.CreateSpriteVisual());
                        sprite.Size = new Vector2(button, button);
                        sprite.Offset = new Vector3(buttonLeft, rowTop, 0);
                        var (code, enabled) = Glyph(session, slot);
                        sprite.Brush = MediaBrush(code, enabled);
                        cells.Children.InsertAtTop(sprite);
                        glyphs[slot] = sprite;
                        fills[slot] = fill;
                        boxes[slot] = Box(roomLeft + buttonLeft, roomTop + rowTop, button, button);
                    }
                    mediaRow = new MediaRow { Controls = session, Glyphs = glyphs, Fills = fills, Bounds = boxes };
                }
                cards.Add(new Card(source, bounds, Box(roomLeft + closeLeft, roomTop + closeTop, close, close), infos[index]?.Tooltip, infos[index]?.Clip));
                highlights.Add(highlight);
                closers.Add((backing, glyph));
                mediaRows.Add(mediaRow);
                thumbRows.Add(thumbRow);
            }
            target.Root = root;
            // preview: the open's only animation, a popup's fade in (§489: the one motion's Open), and none at all with
            // motion off. The cards and titles are drawn from the first frame; DWM's previews come in over it as each
            // registers.
            if (appearing && look.Animations)
            {
                var appear = Keep(c.CreateScalarKeyFrameAnimation());
                appear.InsertKeyFrame(0, 0);
                appear.InsertKeyFrame(1, 1, Keep(MotionEase.Of(c, MotionKind.Open)));
                appear.Duration = look.Motion.Time(MotionKind.Open);
                root.StartAnimation("Opacity", appear);
            }
            // Every card stands: now the previews, off the dock's thread.
            batch = new ThumbnailBatch(dwm, Hwnd, previews);
            crossBrush = cross;
            crossHotBrush = crossHot;
            // Swapped in when a session changes while the popup is open.
            this.mediaFactory = MediaBrush;
        }
        catch
        {
            // A tree that never made it onto the target; the one on screen stays.
            CloseAll(made);
            mediaBrushes.Clear();
            throw;
        }
        var previous = this.made;
        this.made = made;
        CloseAll(previous);
        Tile = tile;
        hovered = null;
        shown = new Shown(gfx, look, scale, windows, anchor, work, edge, bar, buttonSpan);
        accent = look.Accent;
        Watch(media is not null);
    }

    /// <summary>A window's media session, as the dock finds it; a failure only costs the buttons.</summary>
    private static MediaControls? Ask(Func<HWND, MediaControls?>? media, HWND window)
    {
        if (media is null)
        {
            return null;
        }
        try
        {
            return media(window);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Console.Error.WriteLine($"Karpo: media session of a window not read: {error.Message}");
            return null;
        }
    }

    /// <summary>What a window's app said of its preview; a failure only costs the toolbar.</summary>
    private static ThumbInfo? AskThumbs(Func<HWND, ThumbInfo?>? thumbs, HWND window)
    {
        if (thumbs is null)
        {
            return null;
        }
        try
        {
            return thumbs(window);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Console.Error.WriteLine($"Karpo: a window's thumbnail toolbar not read: {error.Message}");
            return null;
        }
    }

    /// <summary>How faint a disabled toolbar button's picture is, as a disabled glyph's ink is.</summary>
    private const float DisabledOpacity = 0.36f;

    /// <summary>
    /// A toolbar button's picture, <paramref name="px"/> square: its icon, or a letter tile of its
    /// tooltip where its picture is an image list's, which never reaches the taskbar where Karpo
    /// could see it (ThumbBar.cs). Drawn 1:1 in the middle of its button (<see cref="Centred"/>).
    /// </summary>
    private static CompositionDrawingSurface ThumbPicture(Gfx gfx, ThumbButton button, float px) =>
        button.Picture is { } picture ? gfx.Icon(picture, px) : gfx.Tile(button.Tip.Trim() is { Length: > 0 } text ? text : "?", px);

    /// <summary>A brush that draws its surface 1:1 in the middle of whatever it fills.</summary>
    private static CompositionSurfaceBrush Centred(Compositor c, CompositionDrawingSurface surface)
    {
        var brush = c.CreateSurfaceBrush(surface);
        brush.Stretch = CompositionStretch.None;
        brush.HorizontalAlignmentRatio = 0.5f;
        brush.VerticalAlignmentRatio = 0.5f;
        return brush;
    }

    /// <summary>A source <paramref name="width"/> × <paramref name="height"/> fitted into the preview's box, never enlarged.</summary>
    internal static (float Width, float Height) Fitted(float width, float height, float boxWidth, float boxHeight)
    {
        float fit = MathF.Min(MathF.Min(boxWidth / width, boxHeight / height), 1);
        return (Round(width * fit), Round(height * fit));
    }

    /// <summary>Button <paramref name="slot"/>'s glyph (an index into MediaGlyphs) and whether the session allows it.</summary>
    private static (int Glyph, bool Enabled) Glyph(MediaControls session, int slot) => slot switch
    {
        0 => (0, session.CanPrevious),
        1 => (session.Playing ? 2 : 1, session.CanPlayPause),
        _ => (3, session.CanNext),
    };

    // Makes (or finds) a media glyph's brush for the showing on screen.
    private Func<int, bool, CompositionBrush>? mediaFactory;

    // Where the cards' container starts in the window: the room left for the shadow.
    private Vector2 cardsOrigin;

    // DWM, and the showing's previews being asked of it (ThumbnailBatch).
    private readonly IDwmThumbnails dwm;
    private ThumbnailBatch? batch;

    public void Hide() => HideNow();

    private void HideNow()
    {
        Release();
        Watch(false);
        Tile = null;
        Inside = false;
        hovered = null;
        shown = null;
        // Hidden is most of the popup's life, and the shadow alone holds a
        // panel-sized picture: nothing of the tree is kept for next time — let go of once the
        // window is out of sight (Surfaces), unless a showing since has put its own on it.
        var old = made;
        made = [];
        var showing = ++hides;
        Surfaces.Hide(Hwnd, () =>
        {
            if (showing == hides && Tile is null)
            {
                try
                {
                    target.Root = null;
                }
                catch (Exception)
                {
                }
            }
            CloseAll(old);
        });
        crossBrush = null;
        crossHotBrush = null;
        mediaFactory = null;
        mediaBrushes.Clear();
        marks = null;
    }

    private void Release()
    {
        _ = KillTimer(Hwnd, PeekTimer);
        _ = KillTimer(Hwnd, MediaTimer);
        LivePeek.End();
        // The previews go with the showing, off the dock's thread (ThumbnailBatch).
        batch?.Release();
        batch = null;
        cards.Clear();
        highlights.Clear();
        closers.Clear();
        mediaRows.Clear();
        thumbRows.Clear();
    }

    /// <summary>Follows the media sessions while open with media, and not otherwise.</summary>
    private void Watch(bool on)
    {
        if (on == watching)
        {
            return;
        }
        watching = on;
        if (on)
        {
            MediaSessions.Shared.Changed += MediaSessionsChanged;
        }
        else
        {
            MediaSessions.Shared.Changed -= MediaSessionsChanged;
        }
    }

    /// <summary>
    /// Raised on the dock thread, from the dock's queue rather than one of its
    /// window procedures, so possibly while the dock is in the middle of
    /// something: only a timer is set, and the popup's WM_TIMER comes through
    /// the dock like every other message. A timer the dock turned away while
    /// busy fires again.
    /// </summary>
    private void MediaSessionsChanged()
    {
        if (IsOpen && !disposed)
        {
            _ = SetTimer(Hwnd, MediaTimer, 0, null);
        }
    }

    /// <summary>A timer of the popup went off (the dock passes its WM_TIMER here).</summary>
    public void Timer(nuint id)
    {
        _ = KillTimer(Hwnd, id);
        if (id == PeekTimer)
        {
            PeekNow();
        }
        else if (id == MediaTimer)
        {
            MediaChanged();
        }
    }

    private void PeekNow()
    {
        if (Inside && hovered is { OnClose: false, Button: < 0 } spot && spot.Index < cards.Count)
        {
            LivePeek.Show(cards[spot.Index].Source, Hwnd, bar, accent);
        }
    }

    /// <summary>
    /// Shows the sessions as they are now: glyphs and enabled states in place,
    /// or the whole popup again when a window gained or lost its session.
    /// </summary>
    private void MediaChanged()
    {
        if (!IsOpen || media is null || shown is not { } last || Tile is not int tile)
        {
            return;
        }
        var fresh = new MediaControls?[cards.Count];
        var relayout = false;
        for (var index = 0; index < cards.Count; index++)
        {
            // A card with the app's own toolbar keeps it, whatever its session does.
            fresh[index] = index < thumbRows.Count && thumbRows[index] is not null ? null : Ask(media, cards[index].Source);
            relayout |= (fresh[index] is null) != (index >= mediaRows.Count || mediaRows[index] is null);
        }
        if (relayout)
        {
            ShowAgain(last, tile);
            return;
        }
        if (mediaFactory is not { } brushFor)
        {
            return;
        }
        for (var index = 0; index < cards.Count; index++)
        {
            if (mediaRows[index] is not { } row || fresh[index] is not { } now || row.Controls.Equals(now))
            {
                // Same values, perhaps a new session object behind them.
                if (mediaRows[index] is { } same && fresh[index] is { } again)
                {
                    same.Controls = again;
                }
                continue;
            }
            row.Controls = now;
            for (var slot = 0; slot < 3; slot++)
            {
                var (code, enabled) = Glyph(now, slot);
                try
                {
                    row.Glyphs[slot].Brush = brushFor(code, enabled);
                }
                catch (Exception)
                {
                    // A popup mid-teardown: the next showing draws afresh.
                }
                if (!enabled && hovered is { Button: >= 0 } spot && spot.Index == index && spot.Button == slot)
                {
                    SetOpacity(row.Fills[slot], 0);
                }
            }
        }
    }

    /// <summary>
    /// What apps said of their previews changed (the band's WM_KARPO_SIGNALS): a toolbar button's
    /// picture or flags change in place; a toolbar that came, went or has other buttons, a
    /// new thumbnail tooltip or clip, lays the popup out again.
    /// </summary>
    public void ThumbsChanged()
    {
        if (!IsOpen || shown is not { } last || Tile is not int tile)
        {
            return;
        }
        var relayout = false;
        var fresh = new ThumbInfo?[cards.Count];
        for (var index = 0; index < cards.Count && !relayout; index++)
        {
            fresh[index] = AskThumbs(thumbs, cards[index].Source);
            var row = index < thumbRows.Count ? thumbRows[index] : null;
            var buttons = fresh[index]?.Shown ?? [];
            relayout = (row is null) != (buttons.Count == 0)
                || (row is not null && !row.Buttons.Select(button => button.Id).SequenceEqual(buttons.Select(button => button.Id)))
                || fresh[index]?.Tooltip != cards[index].Tooltip
                || fresh[index]?.Clip != cards[index].Clip;
        }
        if (relayout)
        {
            ShowAgain(last, tile);
            return;
        }
        var scale = last.Scale;
        for (var index = 0; index < cards.Count; index++)
        {
            if (thumbRows[index] is not { } row || fresh[index] is not { } info)
            {
                continue;
            }
            var buttons = info.Shown;
            for (var slot = 0; slot < buttons.Count; slot++)
            {
                if (Equals(buttons[slot], row.Buttons[slot]))
                {
                    continue;
                }
                try
                {
                    var surface = ThumbPicture(last.Gfx, buttons[slot], Round(16 * scale));
                    made.Add(surface);
                    var brush = Centred(last.Gfx.Compositor, surface);
                    made.Add(brush);
                    row.Pictures[slot].Brush = brush;
                    row.Pictures[slot].Opacity = buttons[slot].Faint ? DisabledOpacity : 1;
                }
                catch (Exception)
                {
                    // A popup mid-teardown: the next showing draws afresh.
                }
                if (hovered is { Button: >= 0 } spot && spot.Index == index && spot.Button == slot)
                {
                    SetOpacity(row.Fills[slot], buttons[slot].Lights ? 1 : 0);
                }
            }
            thumbRows[index] = new ThumbRow { Buttons = buttons, Pictures = row.Pictures, Fills = row.Fills, Bounds = row.Bounds };
        }
    }

    /// <summary>The popup laid out again as it was shown, the pointer still counted inside.</summary>
    private void ShowAgain(Shown last, int tile)
    {
        var wasInside = Inside;
        Show(last.Gfx, last.Look, last.Scale, tile, last.Windows, last.Anchor, last.Work, last.Edge, media, last.Bar, thumbs, sender, last.ButtonSpan);
        Inside = wasInside;
    }

    /// <summary>
    /// Whether the screen point <paramref name="screen"/> is in the list (<see cref="ListArea"/>
    /// or <see cref="Bridge"/>), as <see cref="Moved"/> counts it, while it is up. Asked of where the
    /// cursor is, not of the last message (the dock's watch, PreviewHover.Watched).
    /// </summary>
    public bool Holds(POINT screen)
    {
        if (!IsOpen || !ScreenToClient(Hwnd, &screen))
        {
            return false;
        }
        return Within(ListArea, screen.x, screen.y) || Within(Bridge, screen.x, screen.y);
    }

    public PreviewEvent Moved(int x, int y)
    {
        // Out into the shadow's room is out of the list, though still in its window: the list is
        // left, and closes as it would had the pointer left the window. WM_MOUSELEAVE never comes
        // for a pointer that stops there, and the list stayed open under nothing.
        if (!Within(ListArea, x, y) && !Within(Bridge, x, y))
        {
            if (!Inside)
            {
                return PreviewEvent.Nothing;
            }
            // The leave asked for on the way in is not wanted any more: it would come when the
            // pointer leaves the window, and say the list was left a second time.
            var cancel = new TRACKMOUSEEVENT
            {
                cbSize = (uint)sizeof(TRACKMOUSEEVENT),
                dwFlags = TME.TME_CANCEL | TME.TME_LEAVE,
                hwndTrack = Hwnd,
            };
            _ = TrackMouseEvent(&cancel);
            return Left();
        }
        bool entered = !Inside;
        if (entered)
        {
            Inside = true;
            var track = new TRACKMOUSEEVENT
            {
                cbSize = (uint)sizeof(TRACKMOUSEEVENT),
                dwFlags = TME.TME_LEAVE,
                hwndTrack = Hwnd,
                dwHoverTime = 0,
            };
            _ = TrackMouseEvent(&track);
        }
        var hit = HitTest(x, y);
        if (hit != hovered)
        {
            // Resting on a card peeks its window; on its close or media
            // buttons, or between cards, nothing is peeked.
            _ = KillTimer(Hwnd, PeekTimer);
            LivePeek.End();
            if (hit is { OnClose: false, Button: < 0 })
            {
                _ = SetTimer(Hwnd, PeekTimer, Math.Max(LivePeek.DelayMs, 1), null);
            }
            // Between a card and its own buttons only the buttons change;
            // the card's highlight stays rather than fading out and in.
            if (hovered is { } old && hit?.Index != old.Index)
            {
                Unhover(old.Index);
            }
            if (hovered is { Button: >= 0 } oldButton && (hit?.Index != oldButton.Index || hit?.Button != oldButton.Button))
            {
                SetButton(oldButton.Index, oldButton.Button, false);
            }
            if (hit is { } on)
            {
                Hover(on.Index, on.OnClose);
                if (on.Button >= 0)
                {
                    SetButton(on.Index, on.Button, true);
                }
            }
            hovered = hit;
        }
        return entered ? PreviewEvent.Entered : PreviewEvent.Nothing;
    }

    public PreviewEvent Left()
    {
        _ = KillTimer(Hwnd, PeekTimer);
        LivePeek.End();
        Inside = false;
        if (hovered is { } old)
        {
            Unhover(old.Index);
            if (old.Button >= 0)
            {
                SetButton(old.Index, old.Button, false);
            }
        }
        hovered = null;
        return PreviewEvent.Left;
    }

    private void Hover(int index, bool onClose)
    {
        if (index < highlights.Count && index < closers.Count)
        {
            var (backing, glyph) = closers[index];
            SetOpacity(highlights[index], 1);
            SetOpacity(glyph, 1);
            SetOpacity(backing, onClose ? 1 : 0);
            SetBrush(glyph, onClose ? crossHotBrush : crossBrush);
        }
    }

    private void Unhover(int index)
    {
        if (index < highlights.Count && index < closers.Count)
        {
            var (backing, glyph) = closers[index];
            SetOpacity(highlights[index], 0);
            SetOpacity(backing, 0);
            SetOpacity(glyph, 0);
            SetBrush(glyph, crossBrush);
        }
    }

    /// <summary>
    /// A row button's hover fill — none on a media button the session doesn't allow, or a toolbar
    /// button that takes no clicks or has no background. A toolbar button shows no tooltip.
    /// </summary>
    private void SetButton(int index, int slot, bool lit)
    {
        if (index < thumbRows.Count && thumbRows[index] is { } thumbRow && slot >= 0 && slot < thumbRow.Buttons.Count)
        {
            SetOpacity(thumbRow.Fills[slot], lit && thumbRow.Buttons[slot].Lights ? 1 : 0);
            return;
        }
        if (index < mediaRows.Count && mediaRows[index] is { } row && slot is >= 0 and < 3)
        {
            SetOpacity(row.Fills[slot], lit && Glyph(row.Controls, slot).Enabled ? 1 : 0);
        }
    }

    public PreviewEvent Clicked(int x, int y, bool middle)
    {
        _ = KillTimer(Hwnd, PeekTimer);
        LivePeek.End();
        if (HitTest(x, y) is not { } spot)
        {
            return PreviewEvent.Nothing;
        }
        if (spot.Button >= 0 && !middle && thumbRows[spot.Index] is { } thumbRow)
        {
            // Back to the window, as the taskbar sends it: WM_COMMAND with THBN_CLICKED and the id.
            var (_, dismiss) = ThumbBar.Click(thumbRow.Buttons[spot.Button], (nint)cards[spot.Index].Source, sender);
            return dismiss ? PreviewEvent.Dismiss : PreviewEvent.Nothing;
        }
        if (spot.Button >= 0 && !middle)
        {
            // The session says what happened through its events, and the
            // glyph follows.
            if (mediaRows[spot.Index] is { } row && Glyph(row.Controls, spot.Button).Enabled)
            {
                switch (spot.Button)
                {
                    case 0:
                        row.Controls.Previous();
                        break;
                    case 1:
                        row.Controls.PlayPause();
                        break;
                    default:
                        row.Controls.Next();
                        break;
                }
            }
            return PreviewEvent.Nothing;
        }
        return spot.OnClose || middle
            ? PreviewEvent.Close(cards[spot.Index].Source)
            : PreviewEvent.Activate(cards[spot.Index].Source);
    }

    /// <summary>
    /// app clicks: a right click at (<paramref name="x"/>, <paramref name="y"/>): on a card, its window's
    /// own menu, as Windows' taskbar opens it there (not on its toolbar's buttons or its close button).
    /// </summary>
    public PreviewEvent RightClicked(int x, int y)
    {
        _ = KillTimer(Hwnd, PeekTimer);
        LivePeek.End();
        return HitTest(x, y) is { OnClose: false, Button: < 0 } spot ? PreviewEvent.Menu(cards[spot.Index].Source) : PreviewEvent.Nothing;
    }

    /// <summary>How many cards the list shows.</summary>
    public int Cards => cards.Count;

    /// <summary>The window card <paramref name="card"/> shows (a tab's own, for a tab's card); null past the last.</summary>
    public HWND? CardWindow(int card) => card >= 0 && card < cards.Count ? cards[card].Source : null;

    /// <summary>app clicks: the card at (<paramref name="x"/>, <paramref name="y"/>) in the popup's own px, -1 for none: what files dragged there rest on.</summary>
    public int CardAt(int x, int y) => HitTest(x, y)?.Index ?? -1;

    /// <summary>
    /// app clicks: the keys on card <paramref name="card"/> (-1 for none): it lights up as the pointer
    /// lights a card, its close cross showing, as Windows' taskbar lights the thumbnail the arrows are on.
    /// </summary>
    public void Select(int card)
    {
        if (hovered is { } old && old.Index != card)
        {
            Unhover(old.Index);
            if (old.Button >= 0)
            {
                SetButton(old.Index, old.Button, false);
            }
        }
        if (card >= 0 && card < cards.Count)
        {
            Hover(card, false);
            hovered = new Spot(card, false, -1);
        }
        else
        {
            hovered = null;
        }
    }

    private Spot? HitTest(int x, int y)
    {
        for (int index = 0; index < cards.Count; index++)
        {
            if (!Within(cards[index].Bounds, x, y))
            {
                continue;
            }
            var button = -1;
            if (index < thumbRows.Count && thumbRows[index] is { } thumbRow)
            {
                for (var slot = 0; slot < thumbRow.Bounds.Length; slot++)
                {
                    if (Within(thumbRow.Bounds[slot], x, y))
                    {
                        button = slot;
                        break;
                    }
                }
            }
            else if (index < mediaRows.Count && mediaRows[index] is { } row)
            {
                for (var slot = 0; slot < 3; slot++)
                {
                    if (Within(row.Bounds[slot], x, y))
                    {
                        button = slot;
                        break;
                    }
                }
            }
            return new Spot(index, Within(cards[index].Close, x, y), button);
        }
        return null;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }
        disposed = true;
        Release();
        Watch(false);
        Tile = null;
        Inside = false;
        hovered = null;
        shown = null;
        crossBrush = null;
        crossHotBrush = null;
        mediaFactory = null;
        // The dock may already be tearing itself down; the popup's last
        // messages have nothing to tell it.
        _ = SetWindowLongPtrW(Hwnd, GWLP.GWLP_USERDATA, 0);
        var old = made;
        made = [];
        Surfaces.Destroy(Hwnd, () =>
        {
            CloseAll([target]);
            CloseAll(old);
        });
    }

    private static DesktopWindowTarget CreateTarget(Compositor compositor, HWND hwnd)
    {
        var desktop = ComUtil.Query<ICompositorDesktopInterop>(compositor);
        try
        {
            IDesktopWindowTarget* raw = null;
            ComUtil.Check(desktop->CreateDesktopWindowTarget(hwnd, BOOL.FALSE, &raw));
            return ComUtil.Adopt<DesktopWindowTarget>(raw);
        }
        finally
        {
            desktop->Release();
        }
    }

    // The Rust discards these results: a card that cannot change its look is
    // not worth failing a mouse move over.
    private static void SetOpacity(Visual visual, float opacity)
    {
        try
        {
            visual.Opacity = opacity;
        }
        catch (Exception)
        {
        }
    }

    private static void SetBrush(SpriteVisual visual, CompositionBrush? brush)
    {
        if (brush is null)
        {
            return;
        }
        try
        {
            visual.Brush = brush;
        }
        catch (Exception)
        {
        }
    }

    // Best effort: an object the compositor already let go of has nothing
    // left to free, and a failure here must not stop the rest being closed.
    private static void CloseAll(List<IDisposable> objects)
    {
        foreach (var value in objects)
        {
            try
            {
                value.Dispose();
            }
            catch (Exception)
            {
            }
        }
        objects.Clear();
    }

    /// <summary>
    /// Where a <paramref name="length"/> long popup centred at <paramref name="start"/> starts
    /// along the edge, <paramref name="keep"/> px inside <paramref name="low"/>..<paramref name="high"/>.
    /// </summary>
    /// <remarks>
    /// Rust's clamp panics when the popup is longer than the room; this keeps
    /// the start margin instead.
    /// </remarks>
    private static int AlongEdge(int start, int low, int high, int length, int keep) =>
        Math.Max(Math.Min(start, Math.Max(high - keep - length, low)), low + keep);

    /// <summary>
    /// The gap on the bar's side between <paramref name="list"/> and the window's edge, in a
    /// <paramref name="windowW"/> × <paramref name="windowH"/> window, as wide as the
    /// <paramref name="button"/> px button whose inner side's middle is at
    /// (<paramref name="anchorX"/>, <paramref name="anchorY"/>), as far as the window reaches. A
    /// button of 0 px is one the caller did not say: the gap the list's whole width. Pure.
    /// <para>
    /// Not cut to the list: a list pushed along the bar against the screen's edge is no longer
    /// centred on its button, which reaches past the list's end into the shadow's room — and the
    /// way up from that part of the button was off the list, so the list closed under a pointer
    /// on its way to it (V4). The room is the window's, so the way reaches over it.
    /// </para>
    /// </summary>
    internal static RECT BridgeOf(RECT list, int windowW, int windowH, Edge edge, int anchorX, int anchorY, int button)
    {
        var vertical = edge is Edge.Left or Edge.Right;
        var (from, to) = vertical ? (list.top, list.bottom) : (list.left, list.right);
        if (button > 0)
        {
            var middle = vertical ? anchorY : anchorX;
            from = Math.Max(0, middle - button / 2);
            to = Math.Max(from, Math.Min(vertical ? windowH : windowW, middle - button / 2 + button));
        }
        return edge switch
        {
            Edge.Top => new RECT { left = from, top = 0, right = to, bottom = list.top },
            Edge.Left => new RECT { left = 0, top = from, right = list.left, bottom = to },
            Edge.Right => new RECT { left = list.right, top = from, right = windowW, bottom = to },
            _ => new RECT { left = from, top = list.bottom, right = to, bottom = windowH },
        };
    }

    private static bool Within(in RECT r, int x, int y) =>
        x >= r.left && x < r.right && y >= r.top && y < r.bottom;

    // Rust's f32::round, which rounds halves away from zero; MathF.Round
    // alone rounds them to even and would move edges by a pixel.
    private static float Round(float value) => MathF.Round(value, MidpointRounding.AwayFromZero);

    private static RECT Box(float left, float top, float width, float height) =>
        new()
        {
            left = (int)left,
            top = (int)top,
            right = (int)(left + width),
            bottom = (int)(top + height),
        };

    /// <summary>
    /// A rounded rectangle, filled with <paramref name="brush"/> when <paramref name="stroke"/>
    /// is 0, else outlined that thick on its inside.
    /// </summary>
    private static ShapeVisual Shape(
        Compositor c,
        List<IDisposable> made,
        float left,
        float top,
        float width,
        float height,
        float radius,
        CompositionBrush brush,
        float stroke)
    {
        var visual = c.CreateShapeVisual();
        made.Add(visual);
        visual.Size = new Vector2(width, height);
        visual.Offset = new Vector3(left, top, 0);
        var geometry = c.CreateRoundedRectangleGeometry();
        made.Add(geometry);
        float half = stroke / 2;
        geometry.Offset = new Vector2(half, half);
        geometry.Size = new Vector2(width - stroke, height - stroke);
        geometry.CornerRadius = new Vector2(MathF.Max(radius - half, 0), MathF.Max(radius - half, 0));
        var shape = c.CreateSpriteShape(geometry);
        made.Add(shape);
        if (stroke > 0)
        {
            shape.StrokeBrush = brush;
            shape.StrokeThickness = stroke;
        }
        else
        {
            shape.FillBrush = brush;
        }
        visual.Shapes.Add(shape);
        return visual;
    }

    private static Color Rgba(byte r, byte g, byte b, float a) => Color.FromArgb(Alpha(a), r, g, b);

    private static Color WithAlpha(Color color, float a) => Color.FromArgb(Alpha(a), color.R, color.G, color.B);

    private static byte Alpha(float a) => (byte)MathF.Round(Math.Clamp(a, 0, 1) * 255, MidpointRounding.AwayFromZero);

    private static DXGI_RGBA Ink(Color color) => Gfx.Ink(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);

    private static NativeException Failed(string call)
    {
        var hr = HRESULT_FROM_WIN32(GetLastError());
        return new NativeException($"{call} failed: 0x{hr.Value:X8}", hr.Value);
    }

    /// <summary>
    /// The flyout's colours: Windows 11's flyout, stroke, shadow and hover
    /// tokens for the Windows mode, or the system colours in high contrast.
    /// </summary>
    private readonly record struct Palette(
        bool Glass,
        Color Base,
        Color Tint,
        Color Stroke,
        float StrokeDip,
        float ShadowOpacity,
        Color Hover,
        bool HoverOutlined,
        DXGI_RGBA Text,
        DXGI_RGBA Disabled,
        Color CloseHot,
        DXGI_RGBA CloseGlyphHot)
    {
        public static Palette For(Look look)
        {
            // System colours come from COLORREFs, which carry no alpha.
            if (look.HighContrast)
            {
                uint gray = GetSysColor(COLOR.COLOR_GRAYTEXT);
                return new Palette(
                    Glass: false,
                    Base: WithAlpha(look.HcWindow, 1),
                    Tint: default,
                    Stroke: WithAlpha(look.HcText, 1),
                    StrokeDip: 2,
                    ShadowOpacity: 0,
                    Hover: WithAlpha(look.HcHighlight, 1),
                    HoverOutlined: true,
                    Text: Ink(WithAlpha(look.HcText, 1)),
                    Disabled: Gfx.Ink((gray & 0xFF) / 255f, ((gray >> 8) & 0xFF) / 255f, ((gray >> 16) & 0xFF) / 255f, 1),
                    // High contrast has no red: the close button takes the
                    // selection colours, as a hovered button does there.
                    CloseHot: WithAlpha(look.HcHighlight, 1),
                    CloseGlyphHot: Ink(WithAlpha(look.HcHighlightText, 1)));
            }
            // Only ever set in dark mode: "Show accent colour on Start and taskbar".
            var surface = !look.Dark ? Rgba(0xF3, 0xF3, 0xF3, 1)
                : look.AccentOnSurface ? WithAlpha(look.AccentDark2, 1)
                : Rgba(0x20, 0x20, 0x20, 1);
            return new Palette(
                Glass: look.Glass,
                Base: surface,
                Tint: WithAlpha(surface, look.Dark ? 0.82f : 0.80f),
                Stroke: look.Dark ? Rgba(255, 255, 255, 0.09f) : Rgba(0, 0, 0, 0.07f),
                StrokeDip: 1,
                ShadowOpacity: look.Dark ? 0.30f : 0.14f,
                Hover: look.Dark ? Rgba(255, 255, 255, 0.06f) : Rgba(0, 0, 0, 0.04f),
                HoverOutlined: false,
                Text: look.Dark ? Gfx.Ink(1, 1, 1, 1) : Gfx.Ink(0, 0, 0, 0.90f),
                // TextFillColorDisabled.
                Disabled: look.Dark ? Gfx.Ink(1, 1, 1, 0x5D / 255f) : Gfx.Ink(0, 0, 0, 0x5C / 255f),
                CloseHot: Rgba(0xC4, 0x2B, 0x1C, 1),
                CloseGlyphHot: Gfx.Ink(1, 1, 1, 1));
        }
    }
}
