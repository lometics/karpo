// A widget whose thing this PC hasn't got (docs\widgets.md §247): no battery, no display whose
// brightness can be set, no radio, a screen that does not turn. The person, 2026-10-19: "if there is
// no brightness to set it should still show the slider just greyed out. this should generally be the
// case for configuration options that are not available, you should still show its contents just
// greyed out".
//
// So such a widget draws what it draws where the thing is there — its own layout, with a stand-in
// for the reading (the slider at half, a full battery, a switch that is off) — and all of it in
// Windows' disabled ink: the widget says what it would be, and that it is not, as WinUI greys a
// control that can't be used rather than taking it away. One way for every widget, not one message
// each: the widget answers CellIsland.Unavailable (a face IFaceDrawing.Unavailable) with why, and
// the bar and the folder do the rest — the drawing greyed here, no press, drag, wheel or click
// reaching it, and the why as its tooltip.
//
// Greyed, not recoloured by each widget: the drawing is recorded as it is (a command list on a
// context of the same device) and drawn through one colour matrix that keeps of each pixel only
// how far it stands out from the backdrop — its light on a dark look, its dark on a light one — in
// the disabled ink (IslandInk.Disabled). The text's ink comes out as exactly the disabled text's;
// what was quieter (a track) is quieter still, the accent a grey between, and nothing keeps a hue.

using Karpo.Core;
using Karpo.Dock.Native;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock;

internal static unsafe class Unavailable
{
    // CLSID_D2D1ColorMatrix.
    private static readonly Guid ColorMatrix = new(0x921F03D6, 0x641C, 0x47DF, 0x85, 0x2D, 0xB4, 0xBB, 0x61, 0x53, 0xAE, 0x11);

    // Rec. 709's weights: how light a colour reads.
    private const float R = 0.2126f;
    private const float G = 0.7152f;
    private const float B = 0.0722f;

    /// <summary>
    /// Draws what <paramref name="paint"/> draws on <paramref name="dc"/>, where it would draw it, in
    /// <paramref name="look"/>'s disabled ink. Where the device can't record (it always can), plainly.
    /// </summary>
    public static void Paint(ID2D1DeviceContext* dc, in Look look, Paint paint)
    {
        ID2D1Device* device = null;
        ID2D1DeviceContext* recorder = null;
        ID2D1CommandList* list = null;
        ID2D1Effect* effect = null;
        try
        {
            dc->GetDevice(&device);
            if (device is null
                || device->CreateDeviceContext(D2D1_DEVICE_CONTEXT_OPTIONS.D2D1_DEVICE_CONTEXT_OPTIONS_NONE, &recorder).FAILED
                || recorder->CreateCommandList(&list).FAILED)
            {
                paint(dc);
                return;
            }
            float dpiX, dpiY;
            dc->GetDpi(&dpiX, &dpiY);
            recorder->SetDpi(dpiX, dpiY);
            recorder->SetTarget((ID2D1Image*)list);
            recorder->SetTextAntialiasMode(D2D1_TEXT_ANTIALIAS_MODE.D2D1_TEXT_ANTIALIAS_MODE_GRAYSCALE);
            // Recorded where it is drawn, the context's own transform left for the playback.
            recorder->BeginDraw();
            try
            {
                paint(recorder);
            }
            finally
            {
                _ = recorder->EndDraw(null, null);
                _ = list->Close();
            }
            var guid = ColorMatrix;
            ComUtil.Check(dc->CreateEffect(&guid, &effect));
            effect->SetInput(0, (ID2D1Image*)list, 1);
            var matrix = Matrix(look);
            var straight = D2D1_COLORMATRIX_ALPHA_MODE.D2D1_COLORMATRIX_ALPHA_MODE_STRAIGHT;
            var clamp = 1;
            ComUtil.Check(effect->SetValue((uint)D2D1_COLORMATRIX_PROP.D2D1_COLORMATRIX_PROP_COLOR_MATRIX, D2D1_PROPERTY_TYPE.D2D1_PROPERTY_TYPE_MATRIX_5X4, (byte*)&matrix, (uint)sizeof(D2D_MATRIX_5X4_F)));
            ComUtil.Check(effect->SetValue((uint)D2D1_COLORMATRIX_PROP.D2D1_COLORMATRIX_PROP_ALPHA_MODE, D2D1_PROPERTY_TYPE.D2D1_PROPERTY_TYPE_ENUM, (byte*)&straight, sizeof(uint)));
            ComUtil.Check(effect->SetValue((uint)D2D1_COLORMATRIX_PROP.D2D1_COLORMATRIX_PROP_CLAMP_OUTPUT, D2D1_PROPERTY_TYPE.D2D1_PROPERTY_TYPE_BOOL, (byte*)&clamp, sizeof(int)));
            dc->DrawImage(effect, null, null, D2D1_INTERPOLATION_MODE.D2D1_INTERPOLATION_MODE_NEAREST_NEIGHBOR, D2D1_COMPOSITE_MODE.D2D1_COMPOSITE_MODE_SOURCE_OVER);
        }
        finally
        {
            ComUtil.Release(ref effect);
            ComUtil.Release(ref list);
            ComUtil.Release(ref recorder);
            ComUtil.Release(ref device);
        }
    }

    /// <summary>
    /// The matrix the drawing goes through, on premultiplied pixels (its straight alpha mode, which
    /// leaves them so): of each pixel, how far it stands out — its lightness on a dark look, where the
    /// disabled ink is light, its darkness on a light one — scaled so the text's ink gives the
    /// disabled ink's strength, in the disabled ink's colour.
    /// </summary>
    internal static D2D_MATRIX_5X4_F Matrix(in Look look)
    {
        var ink = new IslandInk(look);
        var disabled = ink.Disabled;
        var light = Lightness(disabled.r, disabled.g, disabled.b) > 0.5f;
        // Stands out by s·lightness + t·alpha: the lightness on a dark look, alpha less it on a light one.
        var (s, t) = light ? (1f, 0f) : (-1f, 1f);
        var text = ink.Text;
        var textOut = (s * Lightness(text.r, text.g, text.b) * text.a) + (t * text.a);
        var k = textOut > 0.01f ? disabled.a / textOut : disabled.a;
        // Out = colour · k · (s·(R·r + G·g + B·b) + t·a), the colour's alpha 1.
        Span<float> colour = [disabled.r, disabled.g, disabled.b, 1];
        var m = default(D2D_MATRIX_5X4_F);
        var rows = new Span<float>(&m, 20);
        Span<float> weights = [s * R, s * G, s * B, t, 0];
        for (var row = 0; row < 5; row++)
        {
            for (var column = 0; column < 4; column++)
            {
                rows[(row * 4) + column] = colour[column] * k * weights[row];
            }
        }
        return m;
    }

    /// <summary>What a pixel of <paramref name="straight"/> (not premultiplied) comes out as, premultiplied: the matrix applied, clamped.</summary>
    internal static DXGI_RGBA Greyed(in Look look, DXGI_RGBA straight)
    {
        var m = Matrix(look);
        var rows = new Span<float>(&m, 20);
        Span<float> pixel = [straight.r * straight.a, straight.g * straight.a, straight.b * straight.a, straight.a, 1];
        Span<float> output = stackalloc float[4];
        for (var column = 0; column < 4; column++)
        {
            var sum = 0f;
            for (var row = 0; row < 5; row++)
            {
                sum += pixel[row] * rows[(row * 4) + column];
            }
            output[column] = Math.Clamp(sum, 0, 1);
        }
        return new DXGI_RGBA(output[0], output[1], output[2], output[3]);
    }

    private static float Lightness(float r, float g, float b) => (R * r) + (G * g) + (B * b);
}
