// Pins drawn offscreen: a folder of pins as a list with every name, with a
// running indicator on one pin and a target this PC doesn't have on another,
// which draws faint with its warning badge. Nothing ever shows on the person's desktop, and nothing is
// launched: the pins only draw.
//
// Like the other offscreen tests these need Direct2D, so they only run with
// KARPO_UI_TESTS=1; the pictures go to KARPO_PNG_DIR.

using Karpo.Dock;
using Karpo.Dock.Native;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.DirectX.DirectX;
using static TerraFX.Interop.Windows.Windows;
using Color = Windows.UI.Color;
using IDispatcherQueueController = TerraFX.Interop.WinRT.IDispatcherQueueController;

namespace Karpo.Tests.Pinning;

public sealed unsafe class PinOffscreenTests
{
    private const string Owner = "layout:default";

    /// <summary>A target no PC has: the pin for it is drawn broken.</summary>
    private const string Gone = @"C:\Karpo\no-such-folder\gone.exe";

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

    private static string System32(string exe) =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), exe);

    private static BarItem Pin(string id, string target, string name = "", string icon = "")
    {
        var island = BarKind.Pin.New();
        island.Id = id;
        island.Pin = new PinOptions { Target = target, Name = name, Icon = icon };
        return island;
    }

    /// <summary>A folder of four pins: a running one, a glyph, a link and one that is gone.</summary>
    private static BarItem Folder()
    {
        var kind = BarKind.FolderList;
        var folder = new FolderOptions();
        _ = Karpo.Core.Folders.Add(kind, folder, Pin("notepad", System32("notepad.exe")).AsCell());
        _ = Karpo.Core.Folders.Add(kind, folder, Pin("calc", System32("calc.exe")).AsCell());
        _ = Karpo.Core.Folders.Add(kind, folder, Pin("site", "https://example.com", "Example", "\uE774").AsCell());
        _ = Karpo.Core.Folders.Add(kind, folder, Pin("gone", Gone).AsCell());
        var island = kind.New();
        island.Id = "tools";
        island.Folder = folder;
        return island;
    }

    [UiFact]
    public void PinPictures()
    {
        OnStaThread(gfx =>
        {
            var config = new Config();
            foreach (var (dark, file) in new[] { (true, "pins-list-dark.png"), (false, "pins-list-light.png") })
            {
                var island = Folder();
                using var folder = new FolderIsland($"{Owner}/{island.Id}", island, gfx, config);
                folder.Opened(gfx);
                // One pin has two windows open, one of them in front: the indicator under it.
                folder.SetMarks(item => item.Target.EndsWith("notepad.exe", StringComparison.OrdinalIgnoreCase)
                    ? new Mark(2, Active: true, Attention: false)
                    : default);
                var look = LookFor(dark);
                var theme = FlyoutTheme.For(look);
                var scale = 1.5f;
                var plan = FolderFlyout.Build(
                    look,
                    scale,
                    folder.View,
                    folder.Options,
                    folder.Tiles.Select(tile => tile.Cell).ToList(),
                    1040);
                Assert.Equal(folder.Tiles.Count, plan.Cards.Count);
                // The pin with windows open carries the indicator the bar draws under its own pins.
                var running = folder.Tiles.Select(tile => tile.Island).OfType<PinTile>()
                    .Single(pin => pin.Item.Target.EndsWith("notepad.exe", StringComparison.OrdinalIgnoreCase));
                Assert.Equal(new Mark(2, Active: true, Attention: false), running.Mark);
                // The broken pin knows why, and says so in its tooltip.
                var broken = folder.Tiles.Select(tile => tile.Island).OfType<PinTile>().Single(pin => pin.Item.Target == Gone);
                Assert.NotNull(broken.Item.Broken);
                Assert.Contains(broken.Item.Broken!, broken.Tooltip(0));
                Picture(plan.Width, plan.Height, scale, theme, file, dc =>
                {
                    FolderFlyout.PaintEmpty(dc, gfx, plan, theme);
                    for (var index = 0; index < plan.Cards.Count; index++)
                    {
                        var card = plan.Cards[index];
                        var tile = folder.Tiles[index];
                        PopupPaint.Fill(dc, card, plan.Radius, PopupPaint.Ink(theme.CardBackground));
                        PopupPaint.Stroke(dc, card, plan.Radius, plan.Hairline, PopupPaint.Ink(theme.CardStroke));
                        var box = PopupPaint.Rect(0, 0, card.right - card.left, card.bottom - card.top);
                        Shifted(dc, card.left, card.top, () => FolderFlyout.PaintListLine(dc, gfx, tile.Island, tile.Cell.Island, box, plan, theme, scale));
                    }
                });
                folder.Closed();
            }
        });
    }

    /// <summary>
    /// The regression (§372): a pin in a folder carried its running indicator and nothing its app said
    /// on its button, so WhatsApp pinned in a folder never showed a count. Now a pin's line shows its
    /// app's badge on its icon's corner as the bar's pins do — with no window open too — and the
    /// folder's button the badges of its pins together, so the count is seen with the folder shut; the
    /// count going away takes both away.
    /// </summary>
    [UiFact]
    public void APinInAFolderShowsItsAppsBadgeAndTheFolderButtonAllOfThem()
    {
        OnStaThread(gfx =>
        {
            var config = new Config();
            var island = Folder();
            using var folder = new FolderIsland($"{Owner}/{island.Id}", island, gfx, config);
            var look = LookFor(true);
            var style = new IslandStyle(1.5f, false, Layout.CellPx(144, new Config { Height = 48 }), Layout.IconPx(144, new Config { Height = 48 }), look);
            var (width, height) = folder.CellSize(0, style);
            (float Left, float Top, float Right, float Bottom) Ink()
            {
                (float, float, float, float) ink = default;
                Offscreen.Bitmap(width, height, dc => folder.PaintCell(dc, 0, style, width, height), bitmap =>
                    ink = Offscreen.InkOf(bitmap, (uint)MathF.Ceiling(width), (uint)MathF.Ceiling(height)));
                return ink;
            }
            var plain = Ink();

            // Calculator has no window and a count of 3; Notepad one window and a count of 2.
            Mark MarkOf(Item item) =>
                item.Target.EndsWith("calc.exe", StringComparison.OrdinalIgnoreCase) ? new Mark(0, false, false, new TileSignal(Badge: Badge.Of("3")))
                : item.Target.EndsWith("notepad.exe", StringComparison.OrdinalIgnoreCase) ? new Mark(1, false, false, new TileSignal(Badge: Badge.Of("2")))
                : default;
            folder.SetMarks(MarkOf);
            var calc = folder.Tiles.Select(tile => tile.Island).OfType<PinTile>().Single(pin => pin.Item.Target.EndsWith("calc.exe", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(Badge.Of("3"), calc.Mark.Signal.Badge);
            Assert.Equal(Badge.Of("5"), folder.PinBadge);
            Assert.True(folder.SetRunning(new Mark(0, false, false, new TileSignal(Badge: folder.PinBadge))));
            var badged = Ink();
            Assert.True(badged.Right > plain.Right || badged.Bottom > plain.Bottom, $"no badge on the folder's button: {badged} against {plain}");
            Assert.True(badged.Right <= width + 0.5f && badged.Bottom <= height + 0.5f, "the badge runs out of the cell");

            // Both pictures, for a look: the folder's button and its list, each pin's count on its icon.
            Offscreen.Bitmap(width, height, dc =>
            {
                // On the dark bar it stands on.
                PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, width, height), 0, new DXGI_RGBA(0.13f, 0.13f, 0.13f, 1));
                folder.PaintCell(dc, 0, style, width, height);
            }, bitmap =>
                Offscreen.Save(bitmap, Path.Combine(Pictures, "pins-folder-badge.png")));
            folder.Opened(gfx);
            var theme = FlyoutTheme.For(look);
            var plan = FolderFlyout.Build(look, 1.5f, folder.View, folder.Options, folder.Tiles.Select(tile => tile.Cell).ToList(), 1040);
            Picture(plan.Width, plan.Height, 1.5f, theme, "pins-list-badges.png", dc =>
            {
                FolderFlyout.PaintEmpty(dc, gfx, plan, theme);
                for (var index = 0; index < plan.Cards.Count; index++)
                {
                    var card = plan.Cards[index];
                    var tile = folder.Tiles[index];
                    PopupPaint.Fill(dc, card, plan.Radius, PopupPaint.Ink(theme.CardBackground));
                    var box = PopupPaint.Rect(0, 0, card.right - card.left, card.bottom - card.top);
                    Shifted(dc, card.left, card.top, () => FolderFlyout.PaintListLine(dc, gfx, tile.Island, tile.Cell.Island, box, plan, theme, 1.5f));
                }
            });
            folder.Closed();

            // Read: the counts go, and with them both badges.
            folder.SetMarks(_ => default);
            Assert.Null(folder.PinBadge);
            Assert.True(folder.SetRunning(new Mark(0, false, false, new TileSignal(Badge: folder.PinBadge))));
            Assert.Equal(plain, Ink());
        });
    }

    // --- Pictures ------------------------------------------------------------------

    private delegate void Painter(ID2D1DeviceContext* dc);

    /// <summary>The panel on a neutral backdrop, with its chrome (solid base, stroke, radius 8), then the content.</summary>
    private static void Picture(float width, float height, float scale, FlyoutTheme theme, string file, Painter content)
    {
        const int margin = 16;
        var w = (uint)MathF.Ceiling(width) + 2 * margin;
        var h = (uint)MathF.Ceiling(height) + 2 * margin;
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
            var backdrop = theme.Dark ? new DXGI_RGBA(0.20f, 0.24f, 0.30f, 1) : new DXGI_RGBA(0.72f, 0.78f, 0.86f, 1);
            dc->Clear(&backdrop);
            var panel = new D2D_RECT_F(margin, margin, margin + width, margin + height);
            PopupPaint.Fill(dc, panel, 8 * scale, PopupPaint.Ink(theme.Base));
            PopupPaint.Stroke(dc, panel, 8 * scale, MathF.Max(1, MathF.Round(scale)), PopupPaint.Ink(theme.Stroke));
            var context = dc;
            Shifted(context, margin, margin, () => content(context));
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
            throw new Xunit.Sdk.XunitException("On the offscreen thread: " + failed);
        }
    }
}
