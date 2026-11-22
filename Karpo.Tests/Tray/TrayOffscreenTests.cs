// The notification area drawn offscreen, so the shape of it can be looked at:
// every icon in a square cell of its own, the bar's, the cells side by side on
// the tray's one card, the chevron as one more such cell, and a widget either
// side; and its icons the pins' size beside them.
//
// Every rectangle comes from a real Layout; only the painting is done here,
// because the bar's own icons are composition sprites rather than D2D calls.
// The icons are real ones taken from shell32.dll at the size the bar asks for,
// the way the dock captures an app's icon (Tray.Render).
//
// Like the other offscreen tests these need Direct2D, so they only run with
// KARPO_UI_TESTS=1; the pictures go to KARPO_PNG_DIR. Nothing is ever shown on
// the person's desktop and no tray is touched.

using Karpo.Dock;
using Karpo.Dock.Native;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.DirectX.DirectX;
using static TerraFX.Interop.Windows.Windows;
using static TerraFX.Interop.Windows.S;
using Color = Windows.UI.Color;
using IDispatcherQueueController = TerraFX.Interop.WinRT.IDispatcherQueueController;

namespace Karpo.Tests.TrayIcons;

public sealed unsafe class TrayOffscreenTests
{
    /// <summary>Icons in shell32.dll that look like an app's: a network, a disc, a shield, a plug…</summary>
    private static readonly int[] ShellIcons = [13, 15, 18, 20, 23, 41, 47, 77, 78, 106, 130, 167];

    private static readonly int[] Heights = [28, 48, 72];

    private static readonly int[] Counts = [2, 4, 9];

    private static string Pictures
    {
        get
        {
            var folder = Environment.GetEnvironmentVariable("KARPO_PNG_DIR") is { Length: > 0 } named
                ? named
                : Path.Combine(Path.GetTempPath(), "karpo-tests", "pictures");
            Directory.CreateDirectory(folder);
            return folder;
        }
    }

    private static Look LookFor(bool dark) => new(
        Glass: false,
        Dark: dark,
        Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4),
        AccentLight2: Color.FromArgb(255, 0x99, 0xEB, 0xFF),
        AccentDark1: Color.FromArgb(255, 0x00, 0x5F, 0xB8));

    /// <summary>One of Windows' own icons at <paramref name="size"/> px, drawn as the tray draws an app's.</summary>
    private static Pixels? Icon(int which, int size)
    {
        var icon = HICON.NULL;
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "shell32.dll");
        fixed (char* name = path)
        {
            if (SHDefExtractIconW(name, ShellIcons[which % ShellIcons.Length], 0, &icon, null, (uint)size).Value != S_OK
                || icon == HICON.NULL)
            {
                return null;
            }
        }
        try
        {
            return Tray.Render(icon, size);
        }
        finally
        {
            _ = DestroyIcon(icon);
        }
    }

    /// <summary>
    /// A bar's end group: two pins (the taskbar's own bare buttons), a Wi-Fi switch, the tray with
    /// <paramref name="icons"/> icons and the chevron, and the battery — so a tray cell is seen
    /// against a widget's card on one side and an app button on the other.
    /// </summary>
    private static Layout Bar(Gfx gfx, uint dpi, int height, Edge edge, Look look, int icons, BarStyle style = BarStyle.Islands)
    {
        // The tray's card is the islands look's; the other looks wear none.
        var config = new Config { Height = height, Style = style };
        List<PartSpec> specs =
        [
            new(Config.IslandKey(null, "pin1"), BarKind.Pin),
            new(Config.IslandKey(null, "pin2"), BarKind.Pin),
            new(Config.IslandKey(null, "wifi"), BarKind.TouchKeyboard),
            new(Config.IslandKey(null, "tray"), BarKind.Tray),
            new(Config.IslandKey(null, "battery"), BarKind.EnergySaver),
        ];
        var area = edge.IsVertical()
            ? new RECT { right = 1200, bottom = 2600 }
            : new RECT { right = 2600, bottom = 1200 };
        _ = gfx;
        return Layout.ForTests(dpi, area, edge, config, specs, Layout.CellPx(dpi, config), tray: icons, trayOverflow: true);
    }

    [UiFact]
    public void TrayPictures()
    {
        OnStaThread(gfx =>
        {
            foreach (var dark in new[] { true, false })
            {
                // The cards are the islands look's; the connected look wears none.
                foreach (var style in new[] { BarStyle.Islands, BarStyle.Floating })
                {
                    foreach (var edge in new[] { Edge.Bottom, Edge.Left })
                    {
                        var name = $"tray-cells-{(dark ? "dark" : "light")}-{(style == BarStyle.Islands ? "cards" : "nocards")}"
                            + (edge.IsVertical() ? "-vertical" : "") + ".png";
                        Sheet(gfx, LookFor(dark), edge, name, style: style);
                    }
                }
            }
        });
    }

    /// <summary>
    /// The tray's icons beside the pins and the network icon, as the person compares them (*"tray
    /// icons show in the same size as all the other icons on the bar again"*): the default connected
    /// bar at 100 and 150 %, dark and light, at 32, 48, 64 and 96 DIP. A tray icon is a pin's size, in
    /// the bar's square: <c>KARPO_PNG_DIR\tray-size-{dark|light}-{100|150}.png</c>.
    /// </summary>
    [UiFact]
    public void TraySizePictures()
    {
        OnStaThread(gfx =>
        {
            foreach (var dark in new[] { true, false })
            {
                foreach (var dpi in new uint[] { 96, 144 })
                {
                    var name = $"tray-size-{(dark ? "dark" : "light")}-{dpi * 100 / 96}.png";
                    Sheet(gfx, LookFor(dark), Edge.Bottom, name, dpi, [32, 48, 64, 96], [4], BarStyle.Floating);
                }
            }
        });
    }

    /// <summary>
    /// One sheet: the tray at each height with 2, 4 and 9 icons. A horizontal bar's rows are
    /// stacked with their labels beside them; a vertical bar's stand side by side, labelled above.
    /// </summary>
    private static void Sheet(
        Gfx gfx,
        Look look,
        Edge edge,
        string file,
        uint dpi = 96,
        int[]? heights = null,
        int[]? counts = null,
        BarStyle style = BarStyle.Islands)
    {
        var scale = dpi / 96f;
        var pad = 16 * scale;
        var labelPx = 13 * scale;
        var theme = FlyoutTheme.For(look);
        var vertical = edge.IsVertical();
        var rows = new List<(string Label, Layout Layout)>();
        foreach (var height in heights ?? Heights)
        {
            foreach (var count in counts ?? Counts)
            {
                rows.Add(($"{height} DIP · {count} icons", Bar(gfx, dpi, height, edge, look, count, style)));
            }
        }
        var labelRoom = vertical ? 0f : 130f * scale;
        var longest = rows.Max(row => Span(row.Layout, vertical));
        var stack = rows.Sum(row => row.Layout.Thickness + pad) + pad;
        var width = vertical ? stack : pad * 2 + labelRoom + longest;
        var tall = vertical ? pad * 3 + labelPx * 2 + longest : stack;

        Picture(width, tall, theme, file, dc =>
        {
            var ink = theme.Text;
            var at = pad;
            foreach (var (label, layout) in rows)
            {
                var span = Span(layout, vertical);
                var left = vertical ? at : pad + labelRoom;
                var top = vertical ? pad * 2 + labelPx * 2 : at;
                if (vertical)
                {
                    PopupPaint.Text(dc, gfx, label, labelPx, Weight.Regular, ink, left, pad, layout.Thickness + pad);
                }
                else
                {
                    PopupPaint.Text(dc, gfx, label, labelPx, Weight.Regular, ink, pad, at + (layout.Thickness - labelPx) / 2f, labelRoom);
                }
                // The bar itself, so a card is seen against it.
                var bar = vertical
                    ? PopupPaint.Rect(left, top, layout.Thickness, span)
                    : PopupPaint.Rect(left, top, span, layout.Thickness);
                PopupPaint.Fill(dc, bar, 0, PopupPaint.Ink(theme.Base));
                Shifted(
                    dc,
                    (vertical ? left : left) - (vertical ? Depth(layout, true) : Start(layout, false)),
                    (vertical ? top - Start(layout, true) : top - Depth(layout, false)),
                    () => Row(dc, gfx, layout, look, theme));
                at += layout.Thickness + pad;
            }
        });
    }

    private static List<int> Shown(Layout layout) =>
        [.. Enumerable.Range(0, layout.Parts.Count).Where(part => layout.Parts[part].Width > 0)];

    /// <summary>The whole end group's length along the bar, from the first widget to the last.</summary>
    private static float Span(Layout layout, bool vertical)
    {
        var shown = Shown(layout);
        var first = shown.Min(part => vertical ? layout.BlockRect(part).Y : layout.BlockRect(part).X);
        var last = shown.Max(part => vertical ? layout.BlockRect(part).Bottom : layout.BlockRect(part).Right);
        return last - first;
    }

    /// <summary>Where the group starts along the bar.</summary>
    private static float Start(Layout layout, bool vertical) =>
        Shown(layout).Min(part => vertical ? layout.BlockRect(part).Y : layout.BlockRect(part).X);

    /// <summary>Where the bar's widgets begin across it (a window keeps room for tooltips on the inner side).</summary>
    private static float Depth(Layout layout, bool vertical) =>
        Shown(layout).Min(part => vertical ? layout.BlockRect(part).X : layout.BlockRect(part).Y);

    /// <summary>The widgets and the tray of one bar, each card and each picture where the layout puts it.</summary>
    private static void Row(ID2D1DeviceContext* dc, Gfx gfx, Layout layout, Look look, FlyoutTheme theme)
    {
        var card = PopupPaint.Ink(theme.CardBackground);
        var edge = PopupPaint.Ink(theme.CardStroke);
        var ink = theme.Text;

        void Card(RectF box)
        {
            if (!layout.Cards || box.IsEmpty)
            {
                return;
            }
            var rect = PopupPaint.Rect(box.X, box.Y, box.Width, box.Height);
            PopupPaint.Fill(dc, rect, layout.CardRadius, card);
            PopupPaint.Stroke(dc, rect, layout.CardRadius, layout.Stroke, edge);
        }

        // The pins: an app button at the taskbar's slot, its icon and no card at all.
        var pins = 0;
        for (var part = 0; part < layout.Parts.Count; part++)
        {
            if (layout.Parts[part].Kind != BarKind.Pin || layout.Parts[part].Width <= 0)
            {
                continue;
            }
            var button = layout.IconBox(Hit.Tile(pins), layout.Icon);
            if (!button.IsEmpty && Icon(pins + 6, (int)MathF.Ceiling(layout.Icon)) is { } art)
            {
                PopupPaint.Picture(dc, art, PopupPaint.Rect(button.X, button.Y, button.Width, button.Height), 0);
            }
            pins++;
        }

        // The widgets either side: their card, and a glyph in it at the size a 1x1 widget uses.
        for (var part = 0; part < layout.Parts.Count; part++)
        {
            if (layout.Parts[part].Kind is BarKind.Tray or BarKind.Pin || layout.Parts[part].Width <= 0)
            {
                continue;
            }
            var box = layout.BlockRect(part);
            Card(box);
            var glyph = layout.Parts[part].Kind == BarKind.TouchKeyboard ? "" : "";
            // At the size the bar draws the network's and the battery's glyphs, which a tray icon is.
            PopupPaint.Glyph(dc, gfx, glyph, Fit.SystemIconFor(layout.CellSide, layout.Scale), ink, PopupPaint.Rect(box.X, box.Y, box.Width, box.Height));
        }

        // The tray: one card , the chevron's cell, then one cell per icon, touching.
        var chevron = layout.ItemRect(Hit.TrayOverflow);
        var trayPart = layout.PartOf(Hit.TrayOverflow);
        Card(layout.BlockRect(trayPart));
        PopupPaint.Glyph(dc, gfx, "", MathF.Round(layout.TrayIconSize * 0.75f), ink, PopupPaint.Rect(chevron.X, chevron.Y, chevron.Width, chevron.Height));
        var size = (int)MathF.Ceiling(layout.TrayIconSize);
        for (var index = 0; index < layout.Parts[trayPart].Count; index++)
        {
            var cell = layout.ItemRect(Hit.Tray(index));
            if (cell.IsEmpty)
            {
                continue;
            }
            if (Icon(index, size) is { } pixels)
            {
                var box = layout.TrayIconRect(index);
                PopupPaint.Picture(dc, pixels, PopupPaint.Rect(box.X, box.Y, box.Width, box.Height), 0);
            }
        }
    }

    // --- Pictures ------------------------------------------------------------------

    private delegate void Painter(ID2D1DeviceContext* dc);

    private static void Picture(float width, float height, FlyoutTheme theme, string file, Painter content)
    {
        var w = (uint)MathF.Ceiling(width);
        var h = (uint)MathF.Ceiling(height);
        ID2D1Factory1* factory = null;
        IWICImagingFactory* wic = null;
        IWICBitmap* bitmap = null;
        ID2D1RenderTarget* target = null;
        ID2D1DeviceContext* dc = null;
        try
        {
            ComUtil.Check(D2D1CreateFactory(D2D1_FACTORY_TYPE.D2D1_FACTORY_TYPE_SINGLE_THREADED, __uuidof<ID2D1Factory1>(), (void**)&factory));
            var clsid = CLSID.CLSID_WICImagingFactory2;
            ComUtil.Check(CoCreateInstance(&clsid, null, (uint)CLSCTX.CLSCTX_INPROC_SERVER, __uuidof<IWICImagingFactory>(), (void**)&wic));
            var format = GUID.GUID_WICPixelFormat32bppPBGRA;
            ComUtil.Check(wic->CreateBitmap(w, h, &format, WICBitmapCreateCacheOption.WICBitmapCacheOnLoad, &bitmap));
            var properties = new D2D1_RENDER_TARGET_PROPERTIES
            {
                type = D2D1_RENDER_TARGET_TYPE.D2D1_RENDER_TARGET_TYPE_SOFTWARE,
                pixelFormat = new D2D1_PIXEL_FORMAT { format = DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM, alphaMode = D2D1_ALPHA_MODE.D2D1_ALPHA_MODE_PREMULTIPLIED },
                dpiX = 96,
                dpiY = 96,
            };
            ComUtil.Check(((ID2D1Factory*)factory)->CreateWicBitmapRenderTarget(bitmap, &properties, &target));
            ComUtil.Check(target->QueryInterface(__uuidof<ID2D1DeviceContext>(), (void**)&dc));
            dc->BeginDraw();
            var backdrop = theme.Dark ? new DXGI_RGBA(0.10f, 0.12f, 0.16f, 1) : new DXGI_RGBA(0.80f, 0.84f, 0.90f, 1);
            dc->Clear(&backdrop);
            var context = dc;
            content(context);
            ComUtil.Check(dc->EndDraw(null, null));
            Save(wic, bitmap, Path.Combine(Pictures, file));
        }
        finally
        {
            ComUtil.Release(ref dc);
            ComUtil.Release(ref target);
            ComUtil.Release(ref bitmap);
            ComUtil.Release(ref wic);
            ComUtil.Release(ref factory);
        }
    }

    private static void Shifted(ID2D1DeviceContext* dc, float x, float y, Action paint)
    {
        D2D_MATRIX_3X2_F before;
        dc->GetTransform(&before);
        var moved = before;
        moved._31 = x * before._11 + y * before._21 + before._31;
        moved._32 = x * before._12 + y * before._22 + before._32;
        dc->SetTransform(&moved);
        try
        {
            paint();
        }
        finally
        {
            dc->SetTransform(&before);
        }
    }

    private static void Save(IWICImagingFactory* wic, IWICBitmap* bitmap, string path)
    {
        IWICStream* stream = null;
        IWICBitmapEncoder* encoder = null;
        IWICBitmapFrameEncode* frame = null;
        try
        {
            ComUtil.Check(wic->CreateStream(&stream));
            fixed (char* name = path)
            {
                ComUtil.Check(stream->InitializeFromFilename(name, GENERIC_WRITE));
            }
            var png = GUID.GUID_ContainerFormatPng;
            ComUtil.Check(wic->CreateEncoder(&png, null, &encoder));
            ComUtil.Check(encoder->Initialize((IStream*)stream, WICBitmapEncoderCacheOption.WICBitmapEncoderNoCache));
            ComUtil.Check(encoder->CreateNewFrame(&frame, null));
            ComUtil.Check(frame->Initialize(null));
            uint width;
            uint height;
            ComUtil.Check(bitmap->GetSize(&width, &height));
            ComUtil.Check(frame->SetSize(width, height));
            var format = GUID.GUID_WICPixelFormat32bppPBGRA;
            ComUtil.Check(frame->SetPixelFormat(&format));
            ComUtil.Check(frame->WriteSource((IWICBitmapSource*)bitmap, null));
            ComUtil.Check(frame->Commit());
            ComUtil.Check(encoder->Commit());
        }
        finally
        {
            ComUtil.Release(ref frame);
            ComUtil.Release(ref encoder);
            ComUtil.Release(ref stream);
        }
    }

    /// <summary>Runs <paramref name="body"/> on a new thread made an STA by its dispatcher queue, with a Gfx.</summary>
    private static void OnStaThread(Action<Gfx> body)
    {
        Exception? failed = null;
        var thread = new Thread(() =>
        {
            try
            {
                var options = new DispatcherQueueOptions
                {
                    dwSize = (uint)sizeof(DispatcherQueueOptions),
                    threadType = DISPATCHERQUEUE_THREAD_TYPE.DQTYPE_THREAD_CURRENT,
                    apartmentType = DISPATCHERQUEUE_THREAD_APARTMENTTYPE.DQTAT_COM_STA,
                };
                IDispatcherQueueController* controller = null;
                ComUtil.Check(CreateDispatcherQueueController(options, &controller));
                try
                {
                    using var gfx = new Gfx(gpu: false);
                    body(gfx);
                }
                finally
                {
                    controller->Release();
                }
            }
            catch (Exception error)
            {
                failed = error;
            }
        });
        thread.Start();
        thread.Join();
        if (failed is not null)
        {
            throw new InvalidOperationException(failed.Message, failed);
        }
    }
}
