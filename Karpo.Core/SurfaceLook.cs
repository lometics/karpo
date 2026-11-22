// How Karpo looks, once for all of it (the person, 2026-10-29: "the styles are still way to complicated"; docs\widgets.md
// §405; 2026-10-31: the style system of §459).
//
// §507: no styles (§505 a separator is empty space); §567 one look, Karpo's Appearance (Config.Appearance, Looks.cs; §507
// to §566 each layout's and each widget's own). How a surface is made follows from its
// background's opacity (SurfaceStyle.Made): 100 % is solid, 0 % or None nothing at all — the desktop —, anything between
// the blur of the desktop under the colour at that opacity, as Windows' own acrylic — and, with Windows' transparency
// effects off, the colour at that opacity straight over the desktop (§546; §405 to §545 solid, which made a colour's
// opacity do nothing on a PC with them off), Windows' own solid as Windows draws its own then. Each surface is so on its
// own, over the desktop, never over another.
//
// §514: how a layout's surfaces behave is no look of theirs: whether what a widget opens opens on hover is the layout's
// one switch (TaskbarLayout.OpenOnHover; §334 to §515 a SurfaceLook each, the taskbar's and the folders').

namespace Karpo.Core;

/// <summary>
/// §405: what a surface is made of, settled from its opacity (<see cref="SurfaceStyle.Made"/>): the blur under the
/// colour, nothing, or the colour solid. Never in the file.
/// </summary>
public enum Material
{
    /// <summary>The blurred desktop behind the colour at the surface's opacity. Needs Windows' transparency effects.</summary>
    Acrylic,

    /// <summary>No plate at all: the widgets stand on the desktop.</summary>
    Clear,

    /// <summary>The colour solid.</summary>
    Opaque,

    /// <summary>
    /// §546: the colour at its opacity straight over the desktop, no blur: a see-through colour chosen while Windows'
    /// transparency effects are off.
    /// </summary>
    Translucent,
}

/// <summary>§405, §459: how a surface is made, by its opacity: what Windows' own is, and what an opacity makes of it (<see cref="Made"/>).</summary>
public static class SurfaceStyle
{
    /// <summary>The bar's opacity of Windows' own: 70 %, as it always was.</summary>
    public const int DefaultOpacity = 70;

    /// <summary>§459: how opaque a bar of background <paramref name="paint"/> is, 0 to 100: Windows' own its default, None none, a colour its alpha.</summary>
    public static int OpacityOf(Paint paint) =>
        paint.None ? 0 : paint.Color is { } color ? (int)Math.Round(color.A * 100 / 255.0, MidpointRounding.AwayFromZero) : DefaultOpacity;

    /// <summary>
    /// §405, §546: what a surface of background <paramref name="paint"/> is made of, by its opacity (<see cref="OpacityOf"/>):
    /// solid at 100, no plate at 0, the blur under the colour between where Windows' transparency effects are on
    /// (<paramref name="glass"/>). Without them a colour chosen is still at its opacity, straight over the desktop — a
    /// person's opacity is theirs —, and Windows' own is solid, as Windows draws its own surfaces then. Always solid in high
    /// contrast (<paramref name="high"/>).
    /// </summary>
    public static Material Made(Paint paint, bool glass, bool high) => OpacityOf(paint) switch
    {
        _ when high => Material.Opaque,
        >= 100 => Material.Opaque,
        <= 0 => Material.Clear,
        _ when glass => Material.Acrylic,
        _ => paint.Color is null ? Material.Opaque : Material.Translucent,
    };
}

/// <summary>
/// §547: Karpo's own look of the taskbar, under every value the person sets — the bar, its widgets, its folders and every
/// flyout, menu and tooltip opened from them. §568 (the person, 2026-11-08: "the dark and light mode setting of the operating
/// system should be respected on the taskbar again"): one for each of Windows' modes, the taskbar in the one Windows is in
/// (<see cref="Paints"/>) — its dark one §547's (<see cref="Ground"/>, <see cref="Card"/>), its light one Windows 11's light
/// equivalents (<see cref="LightGround"/>, <see cref="LightCard"/>) —; in both the bar a little see-through over the wallpaper
/// blurred (<see cref="Blur"/>), each widget on a solid card, Windows 11's own blue (<see cref="Accent"/>), never Windows'
/// accent, "Show accent color on Start and taskbar" nor its transparency effects. Windows' high contrast still wins over all
/// of it (accessibility), and Windows' text size and its animations are still followed.
/// </summary>
public static class KarpoLook
{
    /// <summary>The bar's background in the dark mode: Windows 11's dark taskbar, #202020, at 85 %, over the wallpaper blurred.</summary>
    public static readonly Argb Ground = new(0xD9, 0x20, 0x20, 0x20);

    /// <summary>
    /// A widget's card on the bar in the dark mode: #2B2B2B solid — Windows 11's dark card (CardBackgroundFillColorDefault,
    /// white at 5.12 %) over its dark taskbar #202020, made opaque.
    /// </summary>
    public static readonly Argb Card = new(0xFF, 0x2B, 0x2B, 0x2B);

    /// <summary>§568: the bar's background in the light mode: Windows 11's light taskbar, #F3F3F3, at 85 % as the dark one, over the wallpaper blurred.</summary>
    public static readonly Argb LightGround = new(0xD9, 0xF3, 0xF3, 0xF3);

    /// <summary>
    /// §568: a widget's card on the bar in the light mode: #FBFBFB solid — Windows 11's light card (CardBackgroundFillColorDefault,
    /// white at 70 %) over its light taskbar #F3F3F3, made opaque.
    /// </summary>
    public static readonly Argb LightCard = new(0xFF, 0xFB, 0xFB, 0xFB);

    /// <summary>The accent where none is set: Windows 11's default blue #0078D4, in both modes.</summary>
    public static readonly Argb Accent = new(0xFF, 0x00, 0x78, 0xD4);

    /// <summary>Its shades as Windows makes them of #0078D4: on a dark ground (AccentLight2 #60CDFF), on a light one (AccentDark1 #005FB8), and AccentDark2 #004C87.</summary>
    public static readonly Argb AccentLight2 = new(0xFF, 0x60, 0xCD, 0xFF), AccentDark1 = new(0xFF, 0x00, 0x5F, 0xB8), AccentDark2 = new(0xFF, 0x00, 0x4C, 0x87);

    /// <summary>§544's text shades of it: AccentLight3 #99EBFF (accent text on a dark ground), AccentDark3 #003E92 (on a light one).</summary>
    public static readonly Argb AccentLight3 = new(0xFF, 0x99, 0xEB, 0xFF), AccentDark3 = new(0xFF, 0x00, 0x3E, 0x92);

    /// <summary>
    /// §568: Karpo's defaults of the Appearance in the mode <paramref name="dark"/>, which its sections' values of that mode are
    /// laid over (<see cref="Looks.Of(Appearance?, bool)"/>): the Taskbar's background <see cref="Ground"/> or
    /// <see cref="LightGround"/> and its blur, the Taskbar widgets' <see cref="Card"/> or <see cref="LightCard"/>; every other
    /// value unset, drawn in that mode's own (the text, no outline, the flyouts and menus Windows 11's solid surfaces of it).
    /// </summary>
    public static LookPaints Paints(bool dark) => new(
        Taskbar: new PartPaints(new Paint(dark ? Ground : LightGround), Blur: Blur),
        Widgets: new PartPaints(new Paint(dark ? Card : LightCard)));

    /// <summary>
    /// The bar's blur where none is set (the Taskbar's Background blur): 30 % — a Gaussian of 30 DIP's radius, the wallpaper
    /// a little blurred under the bar's see-through colour.
    /// </summary>
    public const int Blur = 30;

    /// <summary>The most blur there is: 100 %, a radius of <see cref="MostBlurDip"/>.</summary>
    public const int MostBlur = 100;

    /// <summary>The radius of the most blur, in DIPs: each percent of the setting is a DIP of the Gaussian's radius.</summary>
    public const float MostBlurDip = 100f;

    /// <summary><paramref name="blur"/> held to what the setting may be, 0 to <see cref="MostBlur"/>.</summary>
    public static int Held(int blur) => Math.Clamp(blur, 0, MostBlur);

    /// <summary>
    /// The Gaussian's standard deviation in px a blur of <paramref name="blur"/> % is on a display at <paramref name="scale"/>
    /// (its DPI over 96): its radius (<paramref name="blur"/> % of <see cref="MostBlurDip"/>) a third of it, as a Gaussian
    /// reaches all but nothing at three. 0 for none: the wallpaper sharp.
    /// </summary>
    public static float Sigma(int blur, float scale) => Held(blur) / (float)MostBlur * MostBlurDip * scale / 3f;

    /// <summary>The accent's shade on a ground that reads dark (<paramref name="dark"/>), else on a light one.</summary>
    public static Argb AccentOn(bool dark) => dark ? AccentLight2 : AccentDark1;
}
