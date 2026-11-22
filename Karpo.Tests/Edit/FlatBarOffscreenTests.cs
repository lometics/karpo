// The bar a start gives everybody (the defaults, which take an older config.json's place since
// config shape 17), drawn offscreen so the shape of it can be looked at: laid out by the real
// Layout, at rest and in edit mode, on the edge the person's bar is on (the top).
//
// What the sheet is for: the room between the three sections draws no edge against the widgets
// beside it (a connected bar wears no cards: its widgets stand on the bar itself), and each section
// stands where it will really stand, the middle centred. It is about where the blocks land, not how
// they are dressed.
//
// Every rectangle comes from a real Layout and every branch is the one the scene takes
// (layout.Cards); only the painting is done here. Like the
// other offscreen tests these need Direct2D, so they only run with KARPO_UI_TESTS=1; the pictures
// go to KARPO_PNG_DIR.
//
// GAP, so nobody reads these sheets as coverage they do not have: FlatBarPictures and
// SectionNeighbourPictures are TOP EDGE ONLY (SeparatorPictures, gone with the separators' line in
// §505, drew bars down the left edge too). The first time a bar was drawn down the side
// here, the band this file paints turned out to cover only the first widget — the horizontal
// case was the only one it had ever been asked. Looking is what catches that, so a vertical mode for the other two is worth
// writing.

using Karpo.Dock;
using Karpo.Dock.Native;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.DirectX.DirectX;
using static TerraFX.Interop.Windows.Windows;
using Color = Windows.UI.Color;
using IDispatcherQueueController = TerraFX.Interop.WinRT.IDispatcherQueueController;

namespace Karpo.Tests.Edit;

public sealed unsafe class FlatBarOffscreenTests
{
    private static readonly int[] Heights = [28, 41, 72];

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

    [Karpo.Tests.Islands.UiFact]
    public void FlatBarPictures()
    {
        var config = new Config();
        _ = config.Repair();
        OnStaThread(gfx =>
        {
            foreach (var dark in new[] { true, false })
            {
                Sheet(gfx, config, LookFor(dark), $"flat-bar-{(dark ? "dark" : "light")}.png");
            }
        });
    }

    /// <summary>
    /// The cases at the sections' edges, on a bar nobody has but somebody will build: a widget at
    /// the end of the start section (next to the room between two sections), a middle of one widget,
    /// and an end section of one.
    /// </summary>
    [Karpo.Tests.Islands.UiFact]
    public void SectionNeighbourPictures()
    {
        OnStaThread(gfx =>
        {
            foreach (var dark in new[] { true, false })
            {
                var look = LookFor(dark);
                var theme = FlyoutTheme.For(look);
                const float pad = 16;
                const float labelRoom = 190;
                List<PartSpec> specs =
                [
                    // Two widgets with nothing between them, first: without a plain adjacent pair
                    // this sheet could not show what the connected look does between two widgets —
                    // which is nothing: they meet flush, with no line.
                    new(Config.IslandKey(null, "d1"), BarKind.EnergySaver),
                    new(Config.IslandKey(null, "d2"), BarKind.NightLight),
                    new(Config.IslandKey(null, "w"), BarKind.WidgetsBoard),
                    new(Config.IslandKey(null, "c"), BarKind.NightLight) { Section = BarSection.Middle },
                    new(Config.IslandKey(null, "b"), BarKind.EnergySaver) { Section = BarSection.End },
                ];
                var rows = new List<(string Label, Layout Layout, bool Editing)>();
                foreach (var style in new[] { BarStyle.Islands, BarStyle.Floating })
                {
                    foreach (var height in Heights)
                    {
                        foreach (var editing in new[] { false, true })
                        {
                            // The look is a choice now, so every height is drawn in both: the sheet
                            // is the evidence that neither one changes with the height any more.
                            var config = new Config { Height = height, Style = style };
                            rows.Add((
                                $"{style} · {height} DIP · {(editing ? "edit" : "at rest")}",
                                Layout.ForTests(144, new RECT { right = 1920, bottom = 1080 }, Edge.Top, config, specs, Layout.CellPx(144, config), editing),
                                editing));
                        }
                    }
                }
                var tall = rows.Sum(row => row.Layout.Thickness + pad) + pad;
                Picture(pad * 2 + labelRoom + 1920, tall, theme, $"section-neighbours-{(dark ? "dark" : "light")}.png", dc =>
                {
                    var at = pad;
                    foreach (var (label, layout, editing) in rows)
                    {
                        var left = pad + labelRoom;
                        PopupPaint.Text(dc, gfx, label, 13, Weight.Regular, theme.Text, pad, at + (layout.Thickness - 13) / 2f, labelRoom);
                        PopupPaint.Fill(dc, PopupPaint.Rect(left, at, 1920, layout.Thickness), 0, PopupPaint.Ink(theme.Base));
                        Shifted(dc, left, at - Depth(layout), () => Row(dc, gfx, layout, look, theme, editing));
                        at += layout.Thickness + pad;
                    }
                });
            }
        });
    }

    /// <summary>One sheet: every layout of the file at every height, stacked, labelled.</summary>
    private static void Sheet(Gfx gfx, Config config, Look look, string file)
    {
        const uint dpi = 144;
        const float pad = 16;
        const float labelPx = 13;
        const float labelRoom = 150;
        var theme = FlyoutTheme.For(look);
        var rows = new List<(string Label, TaskbarLayout Layout, int Height, bool Editing)>();
        foreach (var layout in config.Layouts.Take(1))
        {
            foreach (var height in Heights)
            {
                foreach (var editing in new[] { false, true })
                {
                    rows.Add(($"{height} DIP · {(editing ? "edit" : "at rest")}", layout, height, editing));
                }
            }
        }
        var area = new RECT { left = 0, top = 0, right = 1920, bottom = 1080 };
        var made = rows.Select(row => Measure(gfx, dpi, area, row.Layout, row.Height, look, row.Editing)).ToList();
        var tall = made.Sum(layout => layout.Thickness + pad) + pad;

        Picture(pad * 2 + labelRoom + 1920, tall, theme, file, dc =>
        {
            var at = pad;
            for (var index = 0; index < rows.Count; index++)
            {
                var layout = made[index];
                var left = pad + labelRoom;
                PopupPaint.Text(dc, gfx, rows[index].Label, labelPx, Weight.Regular, theme.Text, pad, at + (layout.Thickness - labelPx) / 2f, labelRoom);
                PopupPaint.Fill(dc, PopupPaint.Rect(left, at, 1920, layout.Thickness), 0, PopupPaint.Ink(theme.Base));
                Shifted(dc, left, at - Depth(layout), () => Row(dc, gfx, layout, look, theme, rows[index].Editing));
                at += layout.Thickness + pad;
            }
        });
    }

    /// <summary>Where the bar's blocks begin across it: a window keeps room for tooltips on the inner side.</summary>
    private static float Depth(Layout layout) =>
        Enumerable.Range(0, layout.Parts.Count)
            .Select(layout.BlockRect)
            .Where(rect => !rect.IsEmpty)
            .Select(rect => rect.Y)
            .DefaultIfEmpty(0f)
            .Min();

    private static Layout Measure(Gfx gfx, uint dpi, RECT area, TaskbarLayout named, int height, Look look, bool editing = false)
    {
        var config = new Config { Height = height };
        // The dock's own mapping (PartSpec.For), so the person's bar is laid out as the dock lays it
        // out: a hand-built part skipped every width range.
        var specs = named.Islands
            .Select(island => PartSpec.For(Config.IslandKey(null, island.Id), island))
            .ToList();
        var islands = new IslandCollection();
        foreach (var island in named.Islands)
        {
            var key = Config.IslandKey(null, island.Id);
            if (Karpo.Dock.Islands.Create(key, island, gfx, config) is not { } built)
            {
                continue;
            }
            _ = built.Refresh(gfx);
            islands[key] = built;
        }
        // The person's bar is on the top edge, which is where this is drawn.
        return new Layout(gfx, dpi, area, area, Edge.Top, config, look, specs, new Content { Islands = islands }, editing);
    }

    /// <summary>
    /// Every block where the layout puts it, drawn by the rules the scene draws it by: a
    /// connected bar wears no cards, and the islands look a card per part. In
    /// edit mode every block is outlined, so its real width can be seen.
    /// </summary>
    private static void Row(ID2D1DeviceContext* dc, Gfx gfx, Layout layout, Look look, FlyoutTheme theme, bool editing)
    {
        var across = Depth(layout);
        var fill = look.Dark ? new DXGI_RGBA(1f, 1f, 1f, 0.051f) : new DXGI_RGBA(1f, 1f, 1f, 0.702f);
        var stroke = look.Dark ? new DXGI_RGBA(0f, 0f, 0f, 0.098f) : new DXGI_RGBA(0f, 0f, 0f, 0.0588f);
        var outline = look.Dark ? new DXGI_RGBA(1f, 1f, 1f, 0.28f) : new DXGI_RGBA(0f, 0f, 0f, 0.24f);
        // No line between neighbours: the bar draws none. A picture has to draw what the bar draws, no
        // more and no less.
        for (var part = 0; part < layout.Parts.Count; part++)
        {
            var kind = layout.Parts[part].Kind;
            var block = layout.BlockRect(part);
            if (block.IsEmpty)
            {
                continue;
            }
            var rect = PopupPaint.Rect(block.X, block.Y, block.Width, block.Height);
            if (layout.Cards)
            {
                PopupPaint.Fill(dc, rect, layout.CardRadius, fill);
                PopupPaint.Stroke(dc, rect, layout.CardRadius, layout.Stroke, stroke);
            }
            if (editing)
            {
                // Every block in this sheet is outlined while the bar is edited: this sheet paints
                // its own rectangles and is about where the blocks are, not what they are dressed
                // in; SceneTreeTests holds that.
                PopupPaint.Stroke(dc, rect, layout.CardRadius, MathF.Max(1f, layout.Stroke), outline);
            }
            if (kind.Glyph() is { Length: > 0 } glyph)
            {
                PopupPaint.Glyph(dc, gfx, glyph, MathF.Round(layout.CellSide * 0.4f), theme.Text, rect);
            }
        }
        _ = across;
    }

    // --- Pictures ------------------------------------------------------------------

    private delegate void Painter(ID2D1DeviceContext* dc);

    /// <summary>
    /// Draws a sheet and saves it. <paramref name="inspect"/>, where given, is handed the finished
    /// pixels — width, height, and one 0xAARRGGBB value per pixel, premultiplied, row by row — so a
    /// sheet can be measured and not only looked at.
    /// </summary>
    private static void Picture(float width, float height, FlyoutTheme theme, string file, Painter content, Action<int, int, uint[]>? inspect = null)
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
            if (inspect is not null)
            {
                var pixels = new uint[w * h];
                fixed (uint* into = pixels)
                {
                    ComUtil.Check(bitmap->CopyPixels(null, w * 4, w * h * 4, (byte*)into));
                }
                inspect((int)w, (int)h, pixels);
            }
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
            throw new Xunit.Sdk.XunitException("On the offscreen thread: " + failed);
        }
    }
}
