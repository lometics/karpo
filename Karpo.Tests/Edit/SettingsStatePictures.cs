// edit, §544: the settings panel drawn offscreen by its own painter (PanelPainter) in Windows' tokens (FlyoutTheme), the
// pointer's marks set, so the colours of every state can be seen and read back: a page of switches in each state (on,
// off, disabled), §575 the bar with its entry open and another under the pointer, a switch under the pointer and pressed.
// To KARPO_PNG_DIR as settings-states-<state>-<dark|light>.png (SettingsColourTests samples the same renders).
//
// Needs Direct2D: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Dock.Native;
using Karpo.Tests.Folders;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Edit;

/// <summary>§544: the panel painted offscreen at 150 %, read back as BGRA pixels.</summary>
internal static unsafe class SettingsPanelRender
{
    internal const float Scale = 1.5f;

    /// <summary>The states page's switches by name.</summary>
    internal const string On = "On";
    internal const string Off = "Off";
    internal const string OnGreyed = "On, disabled";
    internal const string OffGreyed = "Off, disabled";

    /// <summary>The settings' look, Windows' own, as the dock reads it (Dock.SettingsLookOf).</summary>
    internal static Look LookFor(bool dark) => TheDock.SettingsLookOf(TestLooks.LookFor(dark)) with { Animations = false };

    /// <summary>The dock's pages (SettingsPanelTests.Content) and a page of switches, each state, as the Widget page.</summary>
    internal static SettingsContent States(bool dark)
    {
        var content = SettingsPanelTests.Content(dark: dark);
        var glyph = new FlyoutIcon.Glyph("");
        FlyoutItem[] items =
        [
            new(1, "Switches", Header: true),
            new(2, On, glyph, Control: new FlyoutControl.Toggle(true)),
            new(3, Off, glyph, Control: new FlyoutControl.Toggle(false)),
            new(4, OnGreyed, glyph, Enabled: false, Control: new FlyoutControl.Toggle(true)),
            new(5, OffGreyed, glyph, Enabled: false, Control: new FlyoutControl.Toggle(false)),
        ];
        return content with { Pages = [.. content.Pages, PanelRows.Page(PanelPage.Widget, "States", "", items)] };
    }

    internal static PanelLayout Layout(Gfx gfx, SettingsContent content, PanelPage page) =>
        new(new GfxPanelText(gfx, LookFor(dark: true)), Scale, new PanelInput(0, content, page, new HashSet<string>()), PanelLayout.SizeOf(Scale, 1548));

    /// <summary>The block of the row named <paramref name="name"/>, on the panel.</summary>
    internal static (PanelLine Line, RectF Rect) Row(PanelLayout layout, string name)
    {
        var block = layout.Blocks.First(each => each.Lines.Count > 0 && each.Lines[0].Row.Text == name);
        return (block.Lines[0], layout.OnPanel(block.Rect));
    }

    /// <summary>The switch of the row named <paramref name="name"/>: its hit and where its track stands, as PanelPainter puts it.</summary>
    internal static (PanelHit Hit, RectF Track) Switch(PanelLayout layout, string name)
    {
        var (line, at) = Row(layout, name);
        var right = at.Right - layout.P(PanelMetrics.Gutter);
        var w = layout.P(PanelMetrics.SwitchWide);
        var h = layout.P(PanelMetrics.SwitchTall);
        var mid = at.Y + (at.Height / 2);
        return (layout.Controls(line)[0].Hit, new RectF(right - w, mid - (h / 2), w, h));
    }

    /// <summary>
    /// <paramref name="layout"/>'s flyout — §575 its bar where <paramref name="bar"/> — painted in <paramref name="theme"/> with
    /// <paramref name="marks"/>: its pixels, BGRA premultiplied, a row <see cref="Width"/> × 4 bytes; saved to
    /// <paramref name="save"/> in KARPO_PNG_DIR where named.
    /// </summary>
    internal static byte[] Render(Gfx gfx, FlyoutTheme theme, PanelLayout layout, PanelMarks marks, string? save = null, bool bar = false)
    {
        var painter = new PanelPainter(gfx, theme, layout, marks, null);
        byte[] pixels = [];
        var size = bar ? layout.Size : layout.FlyoutSize;
        Offscreen.Bitmap(size.X, size.Y, dc =>
        {
            if (bar)
            {
                painter.PaintBar(dc);
            }
            else
            {
                painter.PaintFlyout(dc);
            }
        }, bitmap =>
        {
            pixels = Read(bitmap, (uint)Width(layout, bar), (uint)Height(layout, bar));
            if (save is not null)
            {
                Offscreen.Save(bitmap, Path.Combine(Offscreen.Pictures, save));
            }
        });
        return pixels;
    }

    internal static int Width(PanelLayout layout, bool bar = false) => (int)MathF.Ceiling(bar ? layout.Size.X : layout.FlyoutSize.X);

    internal static int Height(PanelLayout layout, bool bar = false) => (int)MathF.Ceiling(bar ? layout.Size.Y : layout.FlyoutSize.Y);

    /// <summary>The pixel at (<paramref name="x"/>, <paramref name="y"/>) of a render <paramref name="width"/> wide, as (A, R, G, B).</summary>
    internal static (byte A, byte R, byte G, byte B) At(byte[] pixels, int width, float x, float y)
    {
        var i = (((int)y * width) + (int)x) * 4;
        return (pixels[i + 3], pixels[i + 2], pixels[i + 1], pixels[i]);
    }

    private static byte[] Read(IWICBitmap* bitmap, uint width, uint height)
    {
        IWICBitmapLock* locked = null;
        try
        {
            var all = new WICRect { X = 0, Y = 0, Width = (int)width, Height = (int)height };
            ComUtil.Check(bitmap->Lock(&all, (uint)WICBitmapLockFlags.WICBitmapLockRead, &locked));
            uint stride = 0;
            ComUtil.Check(locked->GetStride(&stride));
            uint size = 0;
            byte* data = null;
            ComUtil.Check(locked->GetDataPointer(&size, &data));
            var pixels = new byte[width * height * 4];
            for (var y = 0u; y < height; y++)
            {
                new ReadOnlySpan<byte>(data + (y * stride), (int)width * 4).CopyTo(pixels.AsSpan((int)(y * width * 4)));
            }
            return pixels;
        }
        finally
        {
            ComUtil.Release(ref locked);
        }
    }
}

public sealed class SettingsStatePictures
{
    /// <summary>
    /// The states page in each mode: at rest with Accounts under the pointer on the bar (§575 the bar too: its entry open, another lit),
    /// the switch that is off under the pointer and pressed, the switch that is on under the pointer and pressed; and the
    /// Karpo page at rest.
    /// </summary>
    [UiFact]
    public void SettingsStateSheets() => Offscreen.OnStaThread(gfx =>
    {
        foreach (var dark in new[] { true, false })
        {
            var mode = dark ? "dark" : "light";
            var theme = FlyoutTheme.For(SettingsPanelRender.LookFor(dark));
            var content = SettingsPanelRender.States(dark);
            var layout = SettingsPanelRender.Layout(gfx, content, PanelPage.Widget);
            var accounts = new PanelHit(PanelHitKind.Nav, Page: PanelPage.Accounts);
            var off = SettingsPanelRender.Switch(layout, SettingsPanelRender.Off).Hit;
            var on = SettingsPanelRender.Switch(layout, SettingsPanelRender.On).Hit;
            PanelMarks Marks(PanelHit hot, PanelHit pressed) => new(hot, pressed, PanelHit.None, null, false);
            _ = SettingsPanelRender.Render(gfx, theme, layout, Marks(accounts, PanelHit.None), $"settings-states-rest-{mode}.png");
            _ = SettingsPanelRender.Render(gfx, theme, layout, Marks(accounts, PanelHit.None), $"settings-states-bar-{mode}.png", bar: true);
            _ = SettingsPanelRender.Render(gfx, theme, layout, Marks(off, PanelHit.None), $"settings-states-hover-off-{mode}.png");
            _ = SettingsPanelRender.Render(gfx, theme, layout, Marks(off, off), $"settings-states-pressed-off-{mode}.png");
            _ = SettingsPanelRender.Render(gfx, theme, layout, Marks(on, PanelHit.None), $"settings-states-hover-on-{mode}.png");
            _ = SettingsPanelRender.Render(gfx, theme, layout, Marks(on, on), $"settings-states-pressed-on-{mode}.png");
            _ = SettingsPanelRender.Render(gfx, theme, SettingsPanelRender.Layout(gfx, content, PanelPage.Karpo), Marks(PanelHit.None, PanelHit.None), $"settings-states-karpo-{mode}.png");
        }
    });
}
