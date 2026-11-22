// The colours Windows 11's quick settings draw their controls with, beyond the
// popup's own (FlyoutTheme): WinUI's tokens for toggle switches, sliders, check
// boxes, text boxes and buttons, as solid colours for Direct2D. High contrast
// maps each to one of the system colours, as WinUI's high-contrast dictionary does.

using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
using Color = Windows.UI.Color;

namespace Karpo.Dock.QuickSettings.Controls;

internal sealed record QuickTheme
{
    public required FlyoutTheme Popup { get; init; }

    public bool Dark => Popup.Dark;

    public bool HighContrast => Popup.HighContrast;

    public DXGI_RGBA Text => Popup.Text;
    public DXGI_RGBA TextSecondary => Popup.TextSecondary;
    public DXGI_RGBA TextTertiary => Popup.TextTertiary;
    public DXGI_RGBA TextDisabled => Popup.TextDisabled;
    public DXGI_RGBA TextOnAccent => Popup.TextOnAccent;

    /// <summary>TextOnAccentFillColorSecondary: a pressed accent button's text.</summary>
    public required DXGI_RGBA TextOnAccentSecondary { get; init; }

    /// <summary>AccentTextFillColorPrimary: links ("Network and Internet settings").</summary>
    public required DXGI_RGBA Link { get; init; }

    /// <summary>A link under the pointer (AccentTextFillColorSecondary).</summary>
    public required DXGI_RGBA LinkHover { get; init; }

    /// <summary>SystemFillColorCritical: what went wrong.</summary>
    public required DXGI_RGBA Critical { get; init; }

    public required DXGI_RGBA Accent { get; init; }
    public required DXGI_RGBA AccentHover { get; init; }
    public required DXGI_RGBA AccentPressed { get; init; }

    /// <summary>AccentFillColorDisabled.</summary>
    public required DXGI_RGBA AccentDisabled { get; init; }

    public required DXGI_RGBA SubtleHover { get; init; }
    public required DXGI_RGBA SubtlePressed { get; init; }

    /// <summary>SubtleFillColorSecondary as a resting fill: the chosen output, an opened row.</summary>
    public required DXGI_RGBA SubtleSelected { get; init; }

    public required DXGI_RGBA ControlFill { get; init; }
    public required DXGI_RGBA ControlFillHover { get; init; }
    public required DXGI_RGBA ControlFillPressed { get; init; }

    /// <summary>ControlFillColorDisabled.</summary>
    public required DXGI_RGBA ControlFillDisabled { get; init; }

    /// <summary>ControlFillColorInputActive: a text box with the focus.</summary>
    public required DXGI_RGBA InputActive { get; init; }

    /// <summary>ControlAltFillColorSecondary: an unticked box, a switch that is off.</summary>
    public required DXGI_RGBA ControlAltFill { get; init; }

    /// <summary>ControlAltFillColorTertiary: the same under the pointer.</summary>
    public required DXGI_RGBA ControlAltFillHover { get; init; }

    /// <summary>ControlSolidFillColorDefault: a slider's thumb.</summary>
    public required DXGI_RGBA ControlSolid { get; init; }

    /// <summary>ControlStrokeColorDefault: a button's or a text box's outline.</summary>
    public required DXGI_RGBA ControlStroke { get; init; }

    /// <summary>ControlStrokeColorSecondary: the darker bottom edge of a control's outline.</summary>
    public required DXGI_RGBA ControlStrokeBottom { get; init; }

    /// <summary>ControlStrongStrokeColorDefault: a switch's rim, a box's outline, a text box's bottom line.</summary>
    public required DXGI_RGBA StrongStroke { get; init; }

    /// <summary>ControlStrongStrokeColorDisabled.</summary>
    public required DXGI_RGBA StrongStrokeDisabled { get; init; }

    /// <summary>ControlStrongFillColorDefault: a slider's track, a switch's knob when off.</summary>
    public required DXGI_RGBA StrongFill { get; init; }

    public required DXGI_RGBA Divider { get; init; }

    /// <summary>
    /// The footer band, as Windows' quick settings end (docs\widgets.md §9, "Flyout surface"): a
    /// shade over the body, measured dark #1C1C1C on #242424; light, unmeasured, Windows' secondary
    /// solid background (#EEEEEE) on #F9F9F9. A shade, so it darkens a chosen colour or the glass alike.
    /// </summary>
    public required DXGI_RGBA Footer { get; init; }

    /// <summary>The hairline over the footer band: measured dark #191919 on #242424; light #E5E5E5 on #F9F9F9.</summary>
    public required DXGI_RGBA FooterLine { get; init; }

    /// <summary>CardBackgroundFillColorDefault.</summary>
    public required DXGI_RGBA Card { get; init; }

    public required DXGI_RGBA CardStroke { get; init; }

    public required DXGI_RGBA FocusOuter { get; init; }
    public required DXGI_RGBA FocusInner { get; init; }

    /// <summary>Text over a lit row: the selection's text colour in high contrast.</summary>
    public DXGI_RGBA TextOnHover => Popup.TextOnHover;

    /// <summary>The selection in a text box.</summary>
    public required DXGI_RGBA Selection { get; init; }

    public required DXGI_RGBA SelectionText { get; init; }

    public static QuickTheme For(Look look) => For(FlyoutTheme.For(look));

    public static QuickTheme For(FlyoutTheme popup)
    {
        static DXGI_RGBA Ink(Color color) => FlyoutTheme.Ink(color);
        static DXGI_RGBA Argb(uint argb) => new(((argb >> 16) & 0xFF) / 255f, ((argb >> 8) & 0xFF) / 255f, (argb & 0xFF) / 255f, (argb >> 24) / 255f);
        if (popup.HighContrast)
        {
            var text = popup.Text;
            var window = Ink(popup.Base);
            var highlight = Ink(popup.Accent);
            var gray = popup.TextDisabled;
            return new QuickTheme
            {
                Popup = popup,
                TextOnAccentSecondary = popup.TextOnAccent,
                Link = Ink(popup.AccentText),
                LinkHover = Ink(popup.AccentTextHover),
                Critical = text,
                Accent = highlight,
                AccentHover = highlight,
                AccentPressed = highlight,
                AccentDisabled = gray,
                SubtleHover = highlight,
                SubtlePressed = highlight,
                SubtleSelected = highlight,
                ControlFill = window,
                ControlFillHover = highlight,
                ControlFillPressed = highlight,
                ControlFillDisabled = window,
                InputActive = window,
                ControlAltFill = window,
                ControlAltFillHover = window,
                ControlSolid = window,
                ControlStroke = text,
                ControlStrokeBottom = text,
                StrongStroke = text,
                StrongStrokeDisabled = gray,
                StrongFill = text,
                Divider = text,
                Footer = window,
                FooterLine = text,
                Card = window,
                CardStroke = text,
                FocusOuter = text,
                FocusInner = window,
                Selection = highlight,
                SelectionText = popup.TextOnAccent,
            };
        }
        // §544: every token WinUI has for these from the popup's (FlyoutTheme), the one source; only quick settings' own here.
        var accent = Ink(popup.Accent);
        var dark = popup.Dark;
        return new QuickTheme
        {
            Popup = popup,
            TextOnAccentSecondary = popup.TextOnAccentSecondary,
            Link = Ink(popup.AccentText),
            LinkHover = Ink(popup.AccentTextHover),
            Critical = Ink(popup.Critical),
            Accent = accent,
            AccentHover = Ink(popup.AccentHover),
            AccentPressed = Ink(popup.AccentPressed),
            AccentDisabled = Ink(popup.AccentDisabled),
            SubtleHover = Ink(popup.SubtleHover),
            SubtlePressed = Ink(popup.SubtlePressed),
            // SubtleFillColorSecondary, Windows' own whatever lights the panel's rows.
            SubtleSelected = Argb(dark ? 0x0FFFFFFFu : 0x09000000u),
            ControlFill = Ink(popup.ControlFill),
            ControlFillHover = Ink(popup.ControlFillHover),
            ControlFillPressed = Ink(popup.ControlFillPressed),
            ControlFillDisabled = Ink(popup.ControlFillDisabled),
            InputActive = Ink(popup.ControlFillInputActive),
            ControlAltFill = Ink(popup.ControlAltFill),
            ControlAltFillHover = Ink(popup.ControlAltFillHover),
            // ControlSolidFillColorDefault.
            ControlSolid = Argb(dark ? 0xFF454545u : 0xFFFFFFFFu),
            ControlStroke = Ink(popup.ControlStroke),
            ControlStrokeBottom = Ink(popup.ControlStrokeSecondary),
            StrongStroke = Ink(popup.ControlStrongStroke),
            StrongStrokeDisabled = Ink(popup.ControlStrongStrokeDisabled),
            StrongFill = Ink(popup.ScrollIndicator),
            Divider = Ink(popup.Divider),
            Footer = Argb(dark ? FooterDark : FooterLight),
            FooterLine = Argb(dark ? FooterLineDark : FooterLineLight),
            Card = Ink(popup.CardBackground),
            CardStroke = Ink(popup.CardStroke),
            FocusOuter = Ink(popup.FocusOuter),
            FocusInner = Ink(popup.FocusInner),
            Selection = accent,
            SelectionText = popup.TextOnAccent,
        };
    }

    // The footer's shades, black over the body: 22 % and 31 % take #242424 to #1C1C1C and #191919;
    // 4 % and 8 % take #F9F9F9 to #EEEEEE and #E5E5E5.
    internal const uint FooterDark = 0x39000000;
    internal const uint FooterLineDark = 0x4E000000;
    internal const uint FooterLight = 0x0B000000;
    internal const uint FooterLineLight = 0x14000000;

    /// <summary><paramref name="color"/> at <paramref name="alpha"/> of its opacity.</summary>
    public static DXGI_RGBA WithAlpha(DXGI_RGBA color, float alpha) => new(color.r, color.g, color.b, color.a * alpha);
}
