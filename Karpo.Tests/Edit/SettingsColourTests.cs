// edit, §544: the settings' colours are Windows' own, exactly (the person, 2026-11-07: "with the styles i mean the colors dont
// match what windows has"). The tokens the panel and its popups are drawn in (FlyoutTheme, Dock.SettingsLook) are WinUI 3's
// (microsoft-ui-xaml, Common_themeresources_any.xaml), light and dark, the accent its system shade for each mode; the ground
// is the Settings window's where Mica falls back (SolidBackgroundFillColorBase), never a tint picked off a wallpaper; and
// what the panel's painter puts on the screen at known spots is those tokens laid as WinUI lays them. The swatch sheet
// (settings-swatches-<mode>.png) shows each token's Windows value beside the pixel Karpo drew.

using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using Color = Windows.UI.Color;

namespace Karpo.Tests.Edit;

public sealed class SettingsColourTests
{
    private static Color C(uint argb) => Color.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);

    private static uint U(Color color) => ((uint)color.A << 24) | ((uint)color.R << 16) | ((uint)color.G << 8) | color.B;

    private static uint U(DXGI_RGBA ink) =>
        ((uint)MathF.Round(ink.a * 255) << 24) | ((uint)MathF.Round(ink.r * 255) << 16) | ((uint)MathF.Round(ink.g * 255) << 8) | (uint)MathF.Round(ink.b * 255);

    /// <summary>WinUI 3's values (Common_themeresources_any.xaml, generic.xaml), dark then light, ARGB: docs\widgets.md §544's table.</summary>
    private static readonly (string Token, Func<FlyoutTheme, uint> Of, uint Dark, uint Light)[] Tokens =
    [
        ("SolidBackgroundFillColorBase", t => U(t.Ground), 0xFF202020, 0xFFF3F3F3),
        ("AcrylicInAppFillColorDefault fallback", t => U(t.Base), 0xFF2C2C2C, 0xFFF9F9F9),
        ("SurfaceStrokeColorFlyout", t => U(t.Stroke), 0x33000000, 0x0F000000),
        ("TextFillColorPrimary", t => U(t.Text), 0xFFFFFFFF, 0xE4000000),
        ("TextFillColorSecondary", t => U(t.TextSecondary), 0xC5FFFFFF, 0x9E000000),
        ("TextFillColorTertiary", t => U(t.TextTertiary), 0x87FFFFFF, 0x72000000),
        ("TextFillColorDisabled", t => U(t.TextDisabled), 0x5DFFFFFF, 0x5C000000),
        ("TextFillColorInverse", t => U(t.TextInverse), 0xE4000000, 0xFFFFFFFF),
        ("TextOnAccentFillColorPrimary", t => U(t.TextOnAccent), 0xFF000000, 0xFFFFFFFF),
        ("TextOnAccentFillColorSecondary", t => U(t.TextOnAccentSecondary), 0x80000000, 0xB3FFFFFF),
        ("TextOnAccentFillColorDisabled", t => U(t.TextOnAccentDisabled), 0x87FFFFFF, 0xFFFFFFFF),
        ("SubtleFillColorSecondary", t => U(t.SubtleHover), 0x0FFFFFFF, 0x09000000),
        ("SubtleFillColorTertiary", t => U(t.SubtlePressed), 0x0AFFFFFF, 0x06000000),
        ("DividerStrokeColorDefault", t => U(t.Divider), 0x15FFFFFF, 0x0F000000),
        ("CardBackgroundFillColorDefault", t => U(t.CardBackground), 0x0DFFFFFF, 0xB3FFFFFF),
        ("CardBackgroundFillColorSecondary", t => U(t.CardBackgroundSecondary), 0x08FFFFFF, 0x80F6F6F6),
        ("CardStrokeColorDefault", t => U(t.CardStroke), 0x19000000, 0x0F000000),
        ("LayerFillColorAlt", t => U(t.LayerAlt), 0x0DFFFFFF, 0xFFFFFFFF),
        ("ControlFillColorDefault", t => U(t.ControlFill), 0x0FFFFFFF, 0xB3FFFFFF),
        ("ControlFillColorSecondary", t => U(t.ControlFillHover), 0x15FFFFFF, 0x80F9F9F9),
        ("ControlFillColorTertiary", t => U(t.ControlFillPressed), 0x08FFFFFF, 0x4DF9F9F9),
        ("ControlFillColorDisabled", t => U(t.ControlFillDisabled), 0x0BFFFFFF, 0x4DF9F9F9),
        ("ControlFillColorInputActive", t => U(t.ControlFillInputActive), 0xB31E1E1E, 0xFFFFFFFF),
        ("ControlStrokeColorDefault", t => U(t.ControlStroke), 0x12FFFFFF, 0x0F000000),
        ("ControlStrokeColorSecondary", t => U(t.ControlStrokeSecondary), 0x18FFFFFF, 0x29000000),
        ("ControlStrokeColorOnAccentDefault", t => U(t.AccentStroke), 0x14FFFFFF, 0x14FFFFFF),
        ("ControlStrokeColorOnAccentSecondary", t => U(t.AccentStrokeBottom), 0x23000000, 0x66000000),
        ("ControlStrongStrokeColorDefault", t => U(t.ControlStrongStroke), 0x8BFFFFFF, 0x72000000),
        ("ControlStrongStrokeColorDisabled", t => U(t.ControlStrongStrokeDisabled), 0x28FFFFFF, 0x37000000),
        ("ControlStrongFillColorDefault", t => U(t.ScrollIndicator), 0x8BFFFFFF, 0x72000000),
        ("ControlAltFillColorSecondary", t => U(t.ControlAltFill), 0x19000000, 0x06000000),
        ("ControlAltFillColorTertiary", t => U(t.ControlAltFillHover), 0x0BFFFFFF, 0x0F000000),
        ("ControlAltFillColorQuarternary", t => U(t.ControlAltFillPressed), 0x12FFFFFF, 0x18000000),
        ("AccentFillColorDisabled", t => U(t.AccentDisabled), 0x28FFFFFF, 0x37000000),
        ("FocusStrokeColorOuter", t => U(t.FocusOuter), 0xFFFFFFFF, 0xE4000000),
        ("FocusStrokeColorInner", t => U(t.FocusInner), 0xB3000000, 0xB3FFFFFF),
        ("SystemFillColorCritical", t => U(t.Critical), 0xFFFF99A4, 0xFFC42B1C),
        ("SystemFillColorCaution", t => U(t.Caution), 0xFFFCE100, 0xFF9D5D00),
        ("ToolTipBackground (SystemChromeMediumLowColor)", t => U(t.ToolTip), 0xFF2B2B2B, 0xFFF2F2F2),
    ];

    /// <summary>
    /// Every token of the settings, light and dark, is WinUI's; the accent fills are the system's shade WinUI takes for the
    /// mode (AccentFillColorDefault: SystemAccentColorLight2 dark, SystemAccentColorDark1 light; Secondary and Tertiary it at
    /// 90 % and 80 %); the ground is the solid base, not the blue tint that stood for Mica (#1D212A, #ECF0F6).
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TheSettingsTokensAreWinUIs(bool dark)
    {
        var look = SettingsPanelRender.LookFor(dark);
        var theme = FlyoutTheme.For(look);
        foreach (var (token, of, inDark, inLight) in Tokens)
        {
            Assert.True((dark ? inDark : inLight) == of(theme), $"{token} ({(dark ? "dark" : "light")}): Windows {(dark ? inDark : inLight):X8}, Karpo {of(theme):X8}");
        }
        var shade = dark ? look.AccentLight2 : look.AccentDark1;
        Assert.Equal(FlyoutTheme.Opaque(shade), theme.Accent);
        Assert.Equal(FlyoutTheme.WithAlpha(FlyoutTheme.Opaque(shade), 0.9f), theme.AccentHover);
        Assert.Equal(FlyoutTheme.WithAlpha(FlyoutTheme.Opaque(shade), 0.8f), theme.AccentPressed);
        Assert.Equal(dark, theme.ElevationAtTop);
        Assert.NotEqual(dark ? 0xFF1D212Au : 0xFFECF0F6u, U(theme.Ground));
        // A tooltip's plate is WinUI's ToolTip's, solid.
        var tip = FlyoutTheme.Tip(look);
        Assert.Equal(theme.ToolTip, tip.Base);
        Assert.False(tip.Glass);
    }

    /// <summary>High contrast keeps the system's colours for the new tokens too: the ground the window's, the strokes its text.</summary>
    [Fact]
    public void TheSettingsGroundInHighContrastIsTheWindowsColour()
    {
        var look = SettingsPanelRender.LookFor(dark: true) with
        {
            HighContrast = true,
            HcWindow = C(0xFF000000),
            HcText = C(0xFFFFFFFF),
            HcHighlight = C(0xFF1AEBFF),
            HcHighlightText = C(0xFF000000),
        };
        var theme = FlyoutTheme.For(look);
        Assert.Equal(look.HcWindow, theme.Ground);
        Assert.Equal(look.HcText, theme.ControlStrongStroke);
        Assert.Equal(look.HcText, theme.ControlStrokeSecondary);
        Assert.Equal(look.HcWindow, theme.CardBackgroundSecondary);
        Assert.False(theme.ElevationAtTop);
    }

    /// <summary><paramref name="top"/> laid over the opaque <paramref name="under"/> as Direct2D blends it.</summary>
    private static (byte R, byte G, byte B) Over(Color top, (byte R, byte G, byte B) under)
    {
        var a = top.A / 255f;
        byte One(byte t, byte u) => (byte)MathF.Round((t * a) + (u * (1 - a)));
        return (One(top.R, under.R), One(top.G, under.G), One(top.B, under.B));
    }

    private static (byte R, byte G, byte B) Rgb(Color color) => (color.R, color.G, color.B);

    /// <summary>
    /// The states page painted (SettingsPanelRender): at known spots its pixels are the tokens laid as WinUI lays them — the
    /// ground SolidBackgroundFillColorBase; a card CardBackgroundFillColorDefault over it, a disabled one
    /// ControlFillColorDisabled; §575 the entry open on the bar SubtleFillColorSecondary and its pill AccentFillColorDefault,
    /// another under the pointer SubtleFillColorSecondary, the shown one under the pointer Tertiary; a switch on the accent,
    /// off ControlAltFillColorSecondary on its card (Tertiary under the pointer, Quarternary pressed), on and disabled
    /// AccentFillColorDisabled on a disabled card; each within one step of rounding. The sheet shows each beside Windows'.
    /// </summary>
    [UiFact]
    public void ARenderedPanelDrawsWindowsColoursAtKnownSpots() => Offscreen.OnStaThread(gfx =>
    {
        foreach (var dark in new[] { true, false })
        {
            var theme = FlyoutTheme.For(SettingsPanelRender.LookFor(dark));
            var layout = SettingsPanelRender.Layout(gfx, SettingsPanelRender.States(dark), PanelPage.Widget);
            var width = SettingsPanelRender.Width(layout);
            var accounts = new PanelHit(PanelHitKind.Nav, Page: PanelPage.Accounts);
            var shown = new PanelHit(PanelHitKind.Nav, Page: PanelPage.Widget);
            var (offHit, offTrack) = SettingsPanelRender.Switch(layout, SettingsPanelRender.Off);
            var (_, onTrack) = SettingsPanelRender.Switch(layout, SettingsPanelRender.On);
            var (_, onGreyed) = SettingsPanelRender.Switch(layout, SettingsPanelRender.OnGreyed);
            PanelMarks Marks(PanelHit hot, PanelHit pressed = default) => new(hot, pressed, PanelHit.None, null, false);
            var rest = SettingsPanelRender.Render(gfx, theme, layout, Marks(accounts));
            var hotOff = SettingsPanelRender.Render(gfx, theme, layout, Marks(offHit));
            var downOff = SettingsPanelRender.Render(gfx, theme, layout, Marks(offHit, offHit));
            var hotShown = SettingsPanelRender.Render(gfx, theme, layout, Marks(shown), bar: true);
            // §575: the bar, Widget's entry open, Accounts' under the pointer.
            var barRest = SettingsPanelRender.Render(gfx, theme, layout, Marks(accounts), bar: true);
            var barWidth = SettingsPanelRender.Width(layout, bar: true);

            var ground = Rgb(theme.Ground);
            var card = Over(theme.CardBackground, ground);
            var disabledCard = Over(theme.ControlFillDisabled, ground);
            var entryOf = layout.Header.Where(part => part.Kind == PanelHitKind.Nav).ToDictionary(part => part.Page, part => part.Rect);
            var (_, firstCard) = SettingsPanelRender.Row(layout, SettingsPanelRender.On);
            var (_, greyedCard) = SettingsPanelRender.Row(layout, SettingsPanelRender.OffGreyed);
            var p = layout.P;
            var opened = entryOf[PanelPage.Widget];
            List<(string Name, (byte R, byte G, byte B) Windows, byte[] Pixels, int Width, float X, float Y)> spots =
            [
                ("Ground: SolidBackgroundFillColorBase", ground, rest, width, firstCard.X / 2, firstCard.Y + (firstCard.Height / 2)),
                ("Card: CardBackgroundFillColorDefault", card, rest, width, firstCard.X + (firstCard.Width * 0.55f), firstCard.Y + p(4)),
                ("Disabled card: ControlFillColorDisabled", disabledCard, rest, width, greyedCard.X + (greyedCard.Width * 0.55f), greyedCard.Y + p(4)),
                ("Entry, open: SubtleFillColorSecondary", Over(theme.SubtleHover, ground), barRest, barWidth, opened.Right - p(6), opened.Y + p(3)),
                ("Entry, open, pointer: SubtleFillColorTertiary", Over(theme.SubtlePressed, ground), hotShown, barWidth, opened.Right - p(6), opened.Y + p(3)),
                ("Entry pill: AccentFillColorDefault", Rgb(theme.Accent), barRest, barWidth, opened.X + (opened.Width / 2), opened.Bottom - p(1)),
                ("Entry, pointer: SubtleFillColorSecondary", Over(theme.SubtleHover, ground), barRest, barWidth, entryOf[PanelPage.Accounts].Right - p(6), entryOf[PanelPage.Accounts].Y + p(3)),
                ("Switch on: AccentFillColorDefault", Rgb(theme.Accent), rest, width, onTrack.X + p(10), onTrack.Y + (onTrack.Height / 2)),
                ("Switch off: ControlAltFillColorSecondary", Over(theme.ControlAltFill, card), rest, width, offTrack.Right - p(10), offTrack.Y + (offTrack.Height / 2)),
                ("Switch off, pointer: ControlAltFillColorTertiary", Over(theme.ControlAltFillHover, card), hotOff, width, offTrack.Right - p(10), offTrack.Y + (offTrack.Height / 2)),
                ("Switch off, pressed: ControlAltFillColorQuarternary", Over(theme.ControlAltFillPressed, card), downOff, width, offTrack.Right - p(10), offTrack.Y + (offTrack.Height / 2)),
                ("Switch on, disabled: AccentFillColorDisabled", Over(theme.AccentDisabled, disabledCard), rest, width, onGreyed.X + p(10), onGreyed.Y + (onGreyed.Height / 2)),
            ];
            List<(string Name, (byte R, byte G, byte B) Windows, (byte R, byte G, byte B) Karpo)> read = [];
            foreach (var (name, windows, pixels, wide, x, y) in spots)
            {
                var (a, r, g, b) = SettingsPanelRender.At(pixels, wide, x, y);
                read.Add((name, windows, (r, g, b)));
                Assert.True(a == 255 && Near(r, windows.R) && Near(g, windows.G) && Near(b, windows.B),
                    $"{name} ({(dark ? "dark" : "light")}) at {x},{y}: Windows #{windows.R:X2}{windows.G:X2}{windows.B:X2}, Karpo #{r:X2}{g:X2}{b:X2} (alpha {a})");
            }
            Sheet(gfx, read, $"settings-swatches-{(dark ? "dark" : "light")}.png", theme);
        }

        static bool Near(byte a, byte b) => Math.Abs(a - b) <= 1;
    });

    /// <summary>The swatch sheet: each spot's Windows value and the pixel Karpo drew, side by side, named, with their hex.</summary>
    private static unsafe void Sheet(Gfx gfx, List<(string Name, (byte R, byte G, byte B) Windows, (byte R, byte G, byte B) Karpo)> rows, string file, FlyoutTheme theme)
    {
        const float row = 44;
        const float swatch = 64;
        Offscreen.Bitmap(900, (rows.Count * row) + 40, dc =>
        {
            PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, 900, (rows.Count * row) + 40), 0, PopupPaint.Ink(theme.Ground));
            PopupPaint.Text(dc, gfx, "Windows        Karpo", 13, Weight.Semibold, theme.Text, 16, 10);
            for (var i = 0; i < rows.Count; i++)
            {
                var (name, windows, karpo) = rows[i];
                var y = 36 + (i * row);
                PopupPaint.Fill(dc, PopupPaint.Rect(16, y, swatch, row - 8), 4, Gfx.Ink(windows.R / 255f, windows.G / 255f, windows.B / 255f, 1));
                PopupPaint.Fill(dc, PopupPaint.Rect(16 + swatch + 8, y, swatch, row - 8), 4, Gfx.Ink(karpo.R / 255f, karpo.G / 255f, karpo.B / 255f, 1));
                // Rimmed, so a swatch of the ground's own colour shows.
                PopupPaint.Stroke(dc, PopupPaint.Rect(16, y, swatch, row - 8), 4, 1, theme.TextTertiary);
                PopupPaint.Stroke(dc, PopupPaint.Rect(16 + swatch + 8, y, swatch, row - 8), 4, 1, theme.TextTertiary);
                var said = $"{name}   #{windows.R:X2}{windows.G:X2}{windows.B:X2} / #{karpo.R:X2}{karpo.G:X2}{karpo.B:X2}";
                PopupPaint.Text(dc, gfx, said, 13, Weight.Regular, theme.Text, 16 + (2 * swatch) + 24, y + 9);
            }
        }, bitmap => Offscreen.Save(bitmap, Path.Combine(Offscreen.Pictures, file)));
    }
}
