// A popup's tooltip, kept (docs\widgets.md §359; the flicker design's TipView): the edit bar's, a
// section's edit bar's, a menu's icon buttons' and tools', a folder's line's parts' and its note. It was
// a Tool host shown afresh per tip — its content made again, its window raised again — so going from one
// button to the next made everything again. Now one host is kept per owner, its window in the tooltips'
// stratum (ZPlan), taking no pointer: a tip moved to another button is its panel moved in composition
// (FlyoutHost.Show(keep:), §355), and its words a surface drawn once per what they say, kept, so a
// tip shown again for a word it said before draws nothing.
//
// Windows 11's tooltip: 12 px words on 9 by 6 of padding, at most 320 wide, 6 off what it is for.

using System.Collections.Concurrent;
using System.Numerics;
using System.Runtime.InteropServices;
using TerraFX.Interop.Windows;
using Windows.UI.Composition;
using static TerraFX.Interop.Windows.Windows;
using Edge = Karpo.Core.Edge;

namespace Karpo.Dock;

internal sealed unsafe class TipView : IDisposable
{
    internal const float TextDip = 12;
    internal const float PadXDip = 9;
    internal const float PadYDip = 6;
    internal const float GapDip = 6;
    internal const float MaxDip = 320;

    /// <summary>How many words drawn are kept: a toolbar's worth.</summary>
    private const int Kept = 16;

    private readonly FlyoutHost host;

    /// <summary>The words drawn, by what they say and how (§526: in which font too): drawn once each.</summary>
    private readonly Dictionary<(string Text, Karpo.Core.TextStyle Font, float Px, float Wide, float R, float G, float B, float A), (CompositionDrawingSurface Surface, Vector2 Size)> words = [];

    /// <summary>What shows the words in the showing: made with it, kept through every tip it moves to.</summary>
    private SpriteVisual? sprite;
    private CompositionSurfaceBrush? brush;

    /// <param name="userData">Goes into its window's GWLP_USERDATA, as its owner's windows have it.</param>
    /// <param name="className">Its window's class: its owner's, whose procedure its messages go to.</param>
    public TipView(Gfx gfx, HINSTANCE instance, nint userData, string? className = null)
    {
        // §489: a tooltip fades in and out where it stands (PopupEntrance.Tip).
        host = new FlyoutHost(gfx, instance, userData, FlyoutMode.Tool, className ?? Flyout.ClassName) { Stratum = Stratum.Tip, Entrance = PopupEntrance.Tip };
        Transparent();
    }

    public HWND Hwnd => host.Hwnd;

    public bool IsOpen => host.IsOpen;

    /// <summary>Where its panel stands on the screen.</summary>
    public RECT PanelOnScreen => host.PanelOnScreen;

    /// <summary>What it says now; null while it is out of sight.</summary>
    public string? Text { get; private set; }

    /// <summary>Tests only: the words drawn over its life (§359: a tip moved to words it said before draws nothing).</summary>
    internal int Drawn { get; private set; }

    /// <summary>Tests only: the showings it made, each a sprite (§359: moving from one tip to the next makes none).</summary>
    internal int Showings { get; private set; }

    /// <summary>The panel a tip saying <paramref name="text"/> has at <paramref name="scale"/> in <paramref name="look"/>, px.</summary>
    public static Vector2 Panel(Gfx gfx, Look look, float scale, string text)
    {
        float Px(float dip) => MathF.Round(dip * scale, MidpointRounding.AwayFromZero);
        // §526: in its look's font, a menu's (Look.MenuTypeface): the settings' Windows' own, a menu's its layout's.
        using var typeface = gfx.Wearing(look.MenuTypeface);
        var size = gfx.Measure(text, TextPx(look, scale), Weight.Regular);
        var wide = MathF.Min(size.X, Px(MaxDip));
        return new Vector2(MathF.Ceiling(wide + (2f * Px(PadXDip))), MathF.Ceiling(size.Y + (2f * Px(PadYDip))));
    }

    private static float TextPx(Look look, float scale) => TextDip * (look.TextScale > 0f ? look.TextScale : 1f) * scale;

    /// <summary>
    /// The tip saying <paramref name="text"/>, standing off <paramref name="anchor"/> (screen px) as a flyout
    /// stands off a bar on <paramref name="edge"/>, <paramref name="gapDip"/> apart, inside <paramref name="work"/>.
    /// Up already, it moves there in composition, its words swapped: nothing made but words it never said.
    /// </summary>
    public void Show(Gfx gfx, Look look, float scale, string text, POINT anchor, RECT work, Edge edge, float gapDip = GapDip)
    {
        float Px(float dip) => MathF.Round(dip * scale, MidpointRounding.AwayFromZero);
        using var typeface = gfx.Wearing(look.MenuTypeface);
        var textPx = TextPx(look, scale);
        var measured = gfx.Measure(text, textPx, Weight.Regular);
        var wide = MathF.Min(measured.X, Px(MaxDip));
        var panel = new Vector2(MathF.Ceiling(wide + (2f * Px(PadXDip))), MathF.Ceiling(measured.Y + (2f * Px(PadYDip))));
        var keep = host.IsOpen && sprite is not null;
        host.Show(gfx, look, scale, panel, new FlyoutPlacement(anchor, work, edge, GapDip: gapDip), keep: keep);
        // Said again: the host makes a new window where the last one was destroyed.
        Transparent();
        var c = host.Compositor;
        if (!keep || sprite is null || brush is null)
        {
            brush = host.Keep(c.CreateSurfaceBrush());
            brush.Stretch = CompositionStretch.None;
            brush.HorizontalAlignmentRatio = 0f;
            brush.VerticalAlignmentRatio = 0f;
            sprite = host.Keep(c.CreateSpriteVisual());
            sprite.Brush = brush;
            host.Content.Children.InsertAtTop(sprite);
            Showings++;
        }
        var ink = host.Theme.Text;
        var key = (text, look.MenuTypeface, textPx, wide, ink.r, ink.g, ink.b, ink.a);
        if (!words.TryGetValue(key, out var drawn))
        {
            if (words.Count >= Kept)
            {
                Forget(keep: brush.Surface);
            }
            drawn = gfx.Text(text, textPx, Weight.Regular, ink, wide);
            words[key] = drawn;
            Drawn++;
        }
        brush.Surface = drawn.Surface;
        sprite.Offset = new Vector3(Px(PadXDip), MathF.Floor((panel.Y - drawn.Size.Y) / 2f), 0f);
        sprite.Size = drawn.Size;
        Text = text;
    }

    /// <summary>Out of sight, fading as a popup closes; what it showed goes with the showing, its words kept.</summary>
    public void Hide()
    {
        Text = null;
        sprite = null;
        brush = null;
        host.Hide();
    }

    public void Dispose()
    {
        // Closed with the window, once it is out of sight.
        foreach (var (surface, _) in words.Values)
        {
            host.Keep(surface);
        }
        words.Clear();
        sprite = null;
        brush = null;
        host.Dispose();
    }

    /// <summary>The words drawn let go of, but those shown now.</summary>
    private void Forget(ICompositionSurface? keep)
    {
        foreach (var (key, (surface, _)) in words.ToList())
        {
            if (!ReferenceEquals(surface, keep))
            {
                // Closed once the window no longer shows them: a hide may still be fading.
                host.Keep(surface);
                words.Remove(key);
            }
        }
    }

    /// <summary>
    /// Its window takes no pointer: the pointer is on what it is for, and a tip that took it would flicker
    /// itself off and on. Windows asks it what is under the pointer and it answers HTTRANSPARENT, so a press
    /// goes to the menu, the folder or the bar under it — each on this thread, as HTTRANSPARENT needs.
    /// WS_EX_TRANSPARENT alone, which it counted on, lets nothing through a window that is not layered
    /// (§360): a tip standing over a menu's rows or a folder's lines took the first click there (§379).
    /// </summary>
    private void Transparent()
    {
        if (host.Hwnd == HWND.NULL)
        {
            return;
        }
        var style = GetWindowLongPtrW(host.Hwnd, GWL.GWL_EXSTYLE);
        if ((style & (nint)(uint)WS.WS_EX_TRANSPARENT) == 0)
        {
            _ = SetWindowLongPtrW(host.Hwnd, GWL.GWL_EXSTYLE, style | (nint)(uint)WS.WS_EX_TRANSPARENT);
        }
        PassThrough(host.Hwnd);
    }

    // §379: the window procedure each window passed through had before, which every other message still goes to.
    private static readonly ConcurrentDictionary<nint, nint> before = new();

    /// <summary>§379: <paramref name="window"/> answers every hit test with HTTRANSPARENT from now on (<see cref="Transparent"/>); once.</summary>
    internal static void PassThrough(HWND window)
    {
        var mine = (nint)(delegate* unmanaged<HWND, uint, WPARAM, LPARAM, LRESULT>)&Passing;
        if (window == HWND.NULL || GetWindowLongPtrW(window, GWLP.GWLP_WNDPROC) == mine)
        {
            return;
        }
        before[window] = SetWindowLongPtrW(window, GWLP.GWLP_WNDPROC, mine);
    }

    [UnmanagedCallersOnly]
    private static LRESULT Passing(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        if (msg == WM.WM_NCHITTEST)
        {
            return HTTRANSPARENT;
        }
        try
        {
            var next = msg == WM.WM_NCDESTROY ? (before.TryRemove(hwnd, out var gone) ? gone : 0) : before.GetValueOrDefault(hwnd);
            return next != 0
                ? CallWindowProcW((delegate* unmanaged<HWND, uint, WPARAM, LPARAM, LRESULT>)next, hwnd, msg, wParam, lParam)
                : DefWindowProcW(hwnd, msg, wParam, lParam);
        }
        catch (Exception)
        {
            // Nothing may escape into the window manager's callback.
            return DefWindowProcW(hwnd, msg, wParam, lParam);
        }
    }
}
