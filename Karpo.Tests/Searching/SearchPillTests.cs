// Windows 11's search box as Karpo draws it (Karpo.Dock\Islands\SearchPill.cs): the numbers read
// from the taskbar's own resources (SearchBoxButton and its "_SearchBox_Wave3" states, 25H2) and the
// WinUI colours they name, state by state, light and dark, and where the box stands on the 48-DIP
// bar. All of it pure: nothing is drawn here (SceneTreeTests.SearchBoxSheets draws it).

using Karpo.Dock;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using Color = Windows.UI.Color;

namespace Karpo.Tests.Searching;

public sealed class SearchPillTests
{
    private static Look LookFor(bool dark) => new(Glass: true, Dark: dark, Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4));

    /// <summary>An ink as #AARRGGBB, for comparing with Windows' resources.</summary>
    private static uint Argb(DXGI_RGBA ink) =>
        ((uint)MathF.Round(ink.a * 255) << 24) | ((uint)MathF.Round(ink.r * 255) << 16) | ((uint)MathF.Round(ink.g * 255) << 8) | (uint)MathF.Round(ink.b * 255);

    /// <summary>
    /// Windows' box in light: ControlFillColorDefault, Secondary under the pointer, InputActive pressed;
    /// ControlElevationBorderBrush (#0F000000, its bottom #29000000) but pressed, when it is the plain
    /// stroke; the words TextFillColorSecondary, Tertiary pressed; the magnifier TextFillColorPrimary.
    /// </summary>
    [Theory]
    [InlineData(SearchBoxState.Rest, 0xB3FFFFFFu, 0x0F000000u, 0x29000000u, 0x9E000000u)]
    [InlineData(SearchBoxState.Hover, 0x80F9F9F9u, 0x0F000000u, 0x29000000u, 0x9E000000u)]
    [InlineData(SearchBoxState.Pressed, 0xFFFFFFFFu, 0x0F000000u, 0x0F000000u, 0x72000000u)]
    public void TheLightBoxIsWindowsOwn(SearchBoxState state, uint fill, uint edge, uint bottom, uint words)
    {
        var inks = SearchPill.Inks(LookFor(dark: false), state);

        Assert.Equal(fill, Argb(inks.Fill));
        Assert.Equal(edge, Argb(inks.Stroke));
        Assert.Equal(bottom, Argb(inks.StrokeBottom));
        Assert.Equal(words, Argb(inks.Text));
        Assert.Equal(0xE4000000u, Argb(inks.Glyph));
    }

    /// <summary>The same in dark: #0FFFFFFF, #15FFFFFF, #B31E1E1E; edge #12FFFFFF with #18FFFFFF at its bottom; words #C5FFFFFF, #87FFFFFF.</summary>
    [Theory]
    [InlineData(SearchBoxState.Rest, 0x0FFFFFFFu, 0x12FFFFFFu, 0x18FFFFFFu, 0xC5FFFFFFu)]
    [InlineData(SearchBoxState.Hover, 0x15FFFFFFu, 0x12FFFFFFu, 0x18FFFFFFu, 0xC5FFFFFFu)]
    [InlineData(SearchBoxState.Pressed, 0xB31E1E1Eu, 0x12FFFFFFu, 0x12FFFFFFu, 0x87FFFFFFu)]
    public void TheDarkBoxIsWindowsOwn(SearchBoxState state, uint fill, uint edge, uint bottom, uint words)
    {
        var inks = SearchPill.Inks(LookFor(dark: true), state);

        Assert.Equal(fill, Argb(inks.Fill));
        Assert.Equal(edge, Argb(inks.Stroke));
        Assert.Equal(bottom, Argb(inks.StrokeBottom));
        Assert.Equal(words, Argb(inks.Text));
        Assert.Equal(0xFFFFFFFFu, Argb(inks.Glyph));
    }

    /// <summary>
    /// Regression: the box was the taskbar button's ShellTaskbarItemFillColorPrimary, edged with
    /// StrokeColorTertiary — #15FFFFFF and #15FFFFFF in dark, #07000000 at the light edge — which the
    /// taskbar's buttons wear; Windows' search box is a text box at rest.
    /// </summary>
    [Fact]
    public void TheBoxIsATextBoxNotATaskbarButton()
    {
        Assert.NotEqual(0x15FFFFFFu, Argb(SearchPill.Inks(LookFor(dark: true)).Fill));
        Assert.NotEqual(0x07000000u, Argb(SearchPill.Inks(LookFor(dark: false)).Stroke));
    }

    /// <summary>In high contrast the box is the window's colour edged in its text's, and pressed, the highlight's.</summary>
    [Fact]
    public void HighContrastUsesTheSystemColours()
    {
        var look = LookFor(dark: true) with
        {
            HighContrast = true,
            HcWindow = Color.FromArgb(255, 0, 0, 0),
            HcText = Color.FromArgb(255, 255, 255, 255),
            HcHighlight = Color.FromArgb(255, 0x1A, 0xEB, 0xFF),
            HcHighlightText = Color.FromArgb(255, 0, 0, 0),
        };
        var rest = SearchPill.Inks(look);
        Assert.Equal(0xFF000000u, Argb(rest.Fill));
        Assert.Equal(0xFFFFFFFFu, Argb(rest.Stroke));
        Assert.Equal(0xFFFFFFFFu, Argb(rest.Text));
        var pressed = SearchPill.Inks(look, SearchBoxState.Pressed);
        Assert.Equal(0xFF1AEBFFu, Argb(pressed.Fill));
        Assert.Equal(0xFF000000u, Argb(pressed.Text));
    }

    /// <summary>Windows' numbers, in DIPs on the 48-DIP bar (SearchBoxButton, 25H2).</summary>
    [Fact]
    public void TheNumbersAreWindows()
    {
        Assert.Equal(220, SearchPill.LengthDip); // SearchBoxButtonExtent
        Assert.Equal(2, SearchPill.MarginDip); // SearchBoxButtonMargin
        Assert.Equal(32, SearchPill.HeightDip); // 48 less SearchBoxButtonNormalPadding's 8 + 8
        Assert.Equal(11, SearchPill.EndDip); // SearchBoxIconMargin, and after the words too (§510)
        Assert.Equal(37, SearchPill.TextLeftDip); // SearchBoxTextMargin
        Assert.Equal(14, SearchPill.TextDip); // BodyTextBlockStyle
        Assert.Equal(3, SearchPill.ElevationDip); // ControlElevationBorderBrush
    }

    /// <summary>
    /// On the 48-DIP bar's 38.4-DIP card the box is 32 tall and centred across it — 8 DIPs from the
    /// bar's top, as Windows' padding puts it — at 100 % and 150 %; 2 DIPs in from each end.
    /// </summary>
    [Theory]
    [InlineData(1f)]
    [InlineData(1.5f)]
    public void OnThe48DipBarTheBoxIs32TallAnd8Down(float scale)
    {
        var bar = 48 * scale;
        var cell = SearchPill.BaseCellDip * scale;
        // The cell centred across the bar, as the widget grid stands on it.
        var room = new D2D_RECT_F(0, (bar - cell) / 2, SearchPill.Length(cell, float.MaxValue), (bar + cell) / 2);

        var box = SearchPill.In(room, cell);

        Assert.Equal(MathF.Round(32 * scale), box.bottom - box.top, 0.01f);
        Assert.InRange(box.top - MathF.Round(8 * scale), -1f, 1f);
        Assert.Equal(MathF.Round(2 * scale), box.left, 0.01f);
        Assert.Equal(SearchPill.Length(cell, float.MaxValue) - MathF.Round(2 * scale), box.right, 0.01f);
    }

    /// <summary>A box drawn off the bar takes the cell its height stands for: 32 px is the 38.4-px cell, 48 px the 57.6-px one.</summary>
    [Fact]
    public void ABoxOffTheBarTakesTheCellItsHeightStandsFor()
    {
        Assert.Equal(38.4f, SearchPill.CellFor(32), 0.001f);
        Assert.Equal(57.6f, SearchPill.CellFor(48), 0.001f);
        Assert.Equal(32f, SearchPill.Height(SearchPill.CellFor(32)), 0.001f);
        Assert.Equal(3f, SearchPill.ElevationPx(38.4f), 0.001f);
    }

    /// <summary>
    /// The person: "the search boxes should show up with less rounded corners, they should basically
    /// have the same roundness as the other parts in the design language". The box's corners are a
    /// control's 4 DIPs (WinUI's ControlCornerRadius, the bar's highlights' own), on the 48-DIP bar's
    /// cell at 100 % and 150 %, in proportion on the 40- and 64-DIP bars, and never the pill's half-height.
    /// </summary>
    [Theory]
    [InlineData(48, 1f)]
    [InlineData(48, 1.5f)]
    [InlineData(40, 1f)]
    [InlineData(64, 1.5f)]
    public void TheBoxHasAControlsCornersNotAPills(int barDip, float scale)
    {
        Assert.Equal(4, PopupPaint.ControlRadiusDip);
        var cell = Layout.CellPx((uint)(96 * scale), new Config { Height = barDip });
        var radius = SearchPill.Radius(cell);
        Assert.Equal(MathF.Round(PopupPaint.ControlRadiusDip * cell / SearchPill.BaseCellDip), radius);
        if (barDip == 48)
        {
            Assert.Equal(MathF.Round(4 * scale), radius);
        }
        Assert.True(radius < SearchPill.Height(cell) / 4, "the box is not a pill");
    }
}
