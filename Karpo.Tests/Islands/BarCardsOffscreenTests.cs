// The whole bar drawn offscreen in the islands look and the connected one, so that the cards can be
// seen to be uniformly there in the one and uniformly gone in the other (the cards are the look's
// since the "Island backgrounds" switch went). Every part is here: the taskbar's own glyph buttons,
// the app row, three pins, a folder, two switches, the virtual
// desktops, the notification area and the show-desktop sliver.
//
// Every rectangle comes from a real Layout — only the painting is done here, because the bar's own
// icons are composition sprites rather than D2D calls. The card's fill and stroke are Scene's own
// Fluent values (CardBackgroundFillColorDefault, CardStrokeColorDefault) written out, so a pin's
// card is the same ink as the weather's beside it.
//
// Like the other offscreen tests these need Direct2D, so they only run with KARPO_UI_TESTS=1; the
// pictures go to KARPO_PNG_DIR. Nothing is ever shown on the person's desktop.

using Karpo.Dock;
using Karpo.Dock.Native;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.DirectX.DirectX;
using static TerraFX.Interop.Windows.Windows;
using static TerraFX.Interop.Windows.S;
using Color = Windows.UI.Color;
using IDispatcherQueueController = TerraFX.Interop.WinRT.IDispatcherQueueController;

namespace Karpo.Tests.Islands;

public sealed unsafe class BarCardsOffscreenTests
{
    /// <summary>Icons in shell32.dll that look like an app's, for the pins and the app row.</summary>
    private static readonly int[] ShellIcons = [13, 15, 18, 20, 23, 41, 47, 77, 78, 106, 130, 167];

    private static readonly int[] Heights = [28, 48, 72];

    /// <summary>The app buttons all told: three pins, then three open windows in the app row.</summary>
    private const int Tiles = 6;

    private static readonly BarKind[] Bar =
    [
        BarKind.Start, BarKind.Search, BarKind.TaskView, BarKind.Apps,
        BarKind.Pin, BarKind.Pin, BarKind.Pin,
        BarKind.WidgetsBoard, BarKind.FolderList, BarKind.TouchKeyboard, BarKind.Volume, BarKind.EnergySaver,
        BarKind.WidgetsBoard, BarKind.Tray, BarKind.NightLight, BarKind.ShowDesktop,
    ];

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

    /// <summary>Scene's CardBackgroundFillColorDefault and CardStrokeColorDefault, as the bar uses them.</summary>
    private static (DXGI_RGBA Fill, DXGI_RGBA Stroke) CardInk(bool dark) => dark
        ? (new DXGI_RGBA(1f, 1f, 1f, 0.051f), new DXGI_RGBA(0f, 0f, 0f, 0.098f))
        : (new DXGI_RGBA(1f, 1f, 1f, 0.702f), new DXGI_RGBA(0f, 0f, 0f, 0.0588f));

    /// <summary>A shell32.dll icon at <paramref name="size"/> px, for a pin, an app or a tray icon in a picture.</summary>
    internal static Pixels? Icon(int which, int size)
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

    private static Layout Measured(uint dpi, int height, BarStyle style)
    {
        // A card per part is the islands look; a connected bar wears none.
        var config = new Config { Height = height, Style = style };
        List<PartSpec> specs = [.. Bar.Select((kind, i) => new PartSpec(Config.IslandKey(null, $"p{i}"), kind))];
        return Layout.ForTests(
            dpi,
            new RECT { right = 3400, bottom = 1200 },
            Edge.Bottom,
            config,
            specs,
            Layout.CellPx(dpi, config),
            tray: 4,
            trayOverflow: true,
            tiles: Tiles);
    }

    [UiFact]
    public void BarPictures()
    {
        OnStaThread(gfx =>
        {
            foreach (var dark in new[] { true, false })
            {
                foreach (var style in new[] { BarStyle.Islands, BarStyle.Floating })
                {
                    Sheet(gfx, LookFor(dark), style, $"bar-cards-{(dark ? "dark" : "light")}-{(style == BarStyle.Islands ? "islands" : "floating")}.png");
                }
            }
        });
    }

    /// <summary>One sheet: the same bar at each height, stacked, with the height beside it.</summary>
    private static void Sheet(Gfx gfx, Look look, BarStyle style, string file)
    {
        const uint dpi = 96;
        const float pad = 16;
        const float labelPx = 13;
        const float labelRoom = 110;
        var theme = FlyoutTheme.For(look);
        var rows = Heights.Select(height => (Label: $"{height} DIP", Layout: Measured(dpi, height, style))).ToList();
        var longest = rows.Max(row => Span(row.Layout));
        var tall = rows.Sum(row => row.Layout.Thickness + pad) + pad;

        Picture(pad * 2 + labelRoom + longest, tall, theme, file, dc =>
        {
            var at = pad;
            foreach (var (label, layout) in rows)
            {
                var left = pad + labelRoom;
                PopupPaint.Text(dc, gfx, label, labelPx, Weight.Regular, theme.Text, pad, at + (layout.Thickness - labelPx) / 2f, labelRoom);
                var bar = PopupPaint.Rect(left, at, Span(layout), layout.Thickness);
                PopupPaint.Fill(dc, bar, 0, PopupPaint.Ink(theme.Base));
                Shifted(dc, left - Start(layout), at - Depth(layout), () => Row(dc, gfx, layout, look, theme));
                at += layout.Thickness + pad;
            }
        });
    }

    private static List<int> Shown(Layout layout) =>
        [.. Enumerable.Range(0, layout.Parts.Count).Where(part => !layout.BlockRect(part).IsEmpty)];

    private static float Span(Layout layout) =>
        Shown(layout).Max(part => layout.BlockRect(part).Right) - Shown(layout).Min(part => layout.BlockRect(part).X);

    private static float Start(Layout layout) => Shown(layout).Min(part => layout.BlockRect(part).X);

    private static float Depth(Layout layout) => Shown(layout).Min(part => layout.BlockRect(part).Y);

    /// <summary>One bar: every part's card where the layout puts it, and what stands on it.</summary>
    private static void Row(ID2D1DeviceContext* dc, Gfx gfx, Layout layout, Look look, FlyoutTheme theme)
    {
        var (fill, stroke) = CardInk(look.Dark);
        var ink = theme.Text;
        var iconPx = (int)MathF.Ceiling(layout.Icon);

        void Card(RectF box)
        {
            if (!layout.Cards || box.IsEmpty)
            {
                return;
            }
            var rect = PopupPaint.Rect(box.X, box.Y, box.Width, box.Height);
            PopupPaint.Fill(dc, rect, layout.CardRadius, fill);
            PopupPaint.Stroke(dc, rect, layout.CardRadius, layout.Stroke, stroke);
        }

        void Art(RectF box, int which)
        {
            if (!box.IsEmpty && Icon(which, iconPx) is { } art)
            {
                PopupPaint.Picture(dc, art, PopupPaint.Rect(box.X, box.Y, box.Width, box.Height), 0);
            }
        }

        for (var part = 0; part < layout.Parts.Count; part++)
        {
            var kind = layout.Parts[part].Kind;
            var block = layout.BlockRect(part);
            if (block.IsEmpty)
            {
                continue;
            }
            Card(block);
            switch (kind)
            {
                case BarKind.Apps:
                case BarKind.Pin:
                {
                    // Tiles of this part: a pin holds one, the app row holds what the pins left.
                    for (var tile = 0; tile < Tiles; tile++)
                    {
                        var box = layout.IconBox(Hit.Tile(tile), layout.Icon);
                        if (!box.IsEmpty && box.X >= block.X && box.Right <= block.Right)
                        {
                            Art(box, tile);
                        }
                    }
                    break;
                }
                case BarKind.Tray:
                {
                    // tray: the chevron's cell and its icons side by side on the one card.
                    var chevron = layout.ItemRect(Hit.TrayOverflow);
                    PopupPaint.Glyph(dc, gfx, "", MathF.Round(layout.TrayIconSize * 0.75f), ink, PopupPaint.Rect(chevron.X, chevron.Y, chevron.Width, chevron.Height));
                    var size = (int)MathF.Ceiling(layout.TrayIconSize);
                    for (var index = 0; index < layout.Parts[part].Count; index++)
                    {
                        if (layout.ItemRect(Hit.Tray(index)) is not { IsEmpty: false } cell)
                        {
                            continue;
                        }
                        if (Icon(index + 5, size) is { } pixels)
                        {
                            var box = layout.TrayIconRect(index);
                            PopupPaint.Picture(dc, pixels, PopupPaint.Rect(box.X, box.Y, box.Width, box.Height), 0);
                        }
                    }
                    break;
                }
                case BarKind.NightLight:
                {
                    PopupPaint.Text(dc, gfx, "14:05", MathF.Round(layout.SmallIcon * 0.8f), Weight.Regular, ink, block.X + 6, block.Y + (block.Height - layout.SmallIcon) / 2f, block.Width - 12);
                    break;
                }
                default:
                {
                    if (kind.Glyph() is { Length: > 0 } code)
                    {
                        PopupPaint.Glyph(dc, gfx, code, MathF.Round(layout.CellSide * 0.4f), ink, PopupPaint.Rect(block.X, block.Y, block.Width, block.Height));
                    }
                    break;
                }
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
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failed is not null)
        {
            throw failed;
        }
    }
}
