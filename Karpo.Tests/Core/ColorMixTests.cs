// §523: a colour of one's own, mixed in a colour row's picker (Karpo.Core\ColorMix.cs; docs\widgets.md §523). Held here:
//
//  - **Hex**: "#RRGGBB", "#RRGGBBAA" where the value takes an alpha and the colour is see-through; typed with or without the
//    '#', either case, spaces round it; a value without alpha keeps none typed; anything else is no colour.
//  - **Hue, saturation and value** to a colour and back: every one of Windows' colours, the greys, the corners.
//  - **The spectrum and the sliders**: a point to what it stands for and back, held at the edges; the keys' steps held at the ends.

using Karpo.Core;

namespace Karpo.Tests.Core;

public sealed class ColorMixTests
{
    [Theory]
    [InlineData(0xFF, 0x1E, 0x3A, 0x5F, false, "#1E3A5F")]
    [InlineData(0xFF, 0x1E, 0x3A, 0x5F, true, "#1E3A5F")]
    [InlineData(0x80, 0x1E, 0x3A, 0x5F, true, "#1E3A5F80")]
    [InlineData(0x80, 0x1E, 0x3A, 0x5F, false, "#1E3A5F")]
    [InlineData(0x00, 0x00, 0x00, 0x00, true, "#00000000")]
    [InlineData(0xFF, 0xFF, 0xFF, 0xFF, false, "#FFFFFF")]
    public void AColourIsWrittenAsItsHexBoxSaysIt(byte a, byte r, byte g, byte b, bool alpha, string expected) =>
        Assert.Equal(expected, ColorMix.Hex(new Argb(a, r, g, b), alpha));

    [Theory]
    [InlineData("#1E3A5F", true, 0xFF, 0x1E, 0x3A, 0x5F)]
    [InlineData("1e3a5f", true, 0xFF, 0x1E, 0x3A, 0x5F)]
    [InlineData("  #1e3A5F  ", false, 0xFF, 0x1E, 0x3A, 0x5F)]
    [InlineData("#1E3A5F80", true, 0x80, 0x1E, 0x3A, 0x5F)]
    [InlineData("1E3A5F00", true, 0x00, 0x1E, 0x3A, 0x5F)]
    // A value that takes no alpha keeps none of what is typed.
    [InlineData("#1E3A5F80", false, 0xFF, 0x1E, 0x3A, 0x5F)]
    public void AHexTypedIsTheColourItSays(string typed, bool alpha, byte a, byte r, byte g, byte b) =>
        Assert.Equal(new Argb(a, r, g, b), ColorMix.ParseHex(typed, alpha));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("#")]
    [InlineData("#12345")]
    [InlineData("#1234567")]
    [InlineData("#123456789")]
    [InlineData("#12G456")]
    [InlineData("##123456")]
    [InlineData("# 123456")]
    [InlineData("#+12345")]
    [InlineData("#-12345")]
    [InlineData("0x123456")]
    [InlineData("blue")]
    [InlineData("#１２３４５６")]
    public void AnythingElseTypedIsNoColour(string? typed)
    {
        Assert.Null(ColorMix.ParseHex(typed, alpha: true));
        Assert.Null(ColorMix.ParseHex(typed, alpha: false));
    }

    /// <summary>What the hex box says reads back as the colour, with its alpha or without.</summary>
    [Fact]
    public void WhatTheHexBoxSaysReadsBack()
    {
        foreach (var colour in Colours())
        {
            Assert.Equal(colour, ColorMix.ParseHex(ColorMix.Hex(colour, alpha: true), alpha: true));
            Assert.Equal(colour with { A = 255 }, ColorMix.ParseHex(ColorMix.Hex(colour, alpha: false), alpha: false));
        }
    }

    /// <summary>Every colour to its hue, saturation and value and back is itself: Windows' 48, the greys, the corners, a spread.</summary>
    [Fact]
    public void HueSaturationAndValueGoRoundToTheSameColour()
    {
        foreach (var colour in Colours())
        {
            var hsv = ColorMix.ToHsv(colour);
            Assert.InRange(hsv.H, 0, 360);
            Assert.InRange(hsv.S, 0, 1);
            Assert.InRange(hsv.V, 0, 1);
            Assert.Equal(colour, ColorMix.ToArgb(hsv, colour.A));
        }
    }

    [Fact]
    public void TheCornersAreWhatTheySay()
    {
        Assert.Equal(new Hsv(0, 1, 1), ColorMix.ToHsv(new Argb(255, 255, 0, 0)));
        Assert.Equal(new Hsv(120, 1, 1), ColorMix.ToHsv(new Argb(255, 0, 255, 0)));
        Assert.Equal(new Hsv(240, 1, 1), ColorMix.ToHsv(new Argb(255, 0, 0, 255)));
        // A grey has no hue of its own: 0, no saturation.
        Assert.Equal(new Hsv(0, 0, 128 / 255.0), ColorMix.ToHsv(new Argb(255, 128, 128, 128)));
        Assert.Equal(new Hsv(0, 0, 0), ColorMix.ToHsv(new Argb(255, 0, 0, 0)));
        // A hue all the way round is red again; the alpha is what is asked.
        Assert.Equal(new Argb(0x40, 255, 0, 0), ColorMix.ToArgb(new Hsv(360, 1, 1), 0x40));
        Assert.Equal(new Argb(255, 0, 255, 255), ColorMix.Pure(180));
        // Past its ends a saturation or a value is held at them.
        Assert.Equal(new Argb(255, 255, 255, 255), ColorMix.ToArgb(new Hsv(77, -1, 2)));
    }

    /// <summary>The spectrum: saturation across from its left, value up from its bottom; a point past an edge held at it; and back.</summary>
    [Fact]
    public void TheSpectrumsPointIsTheColourAndBack()
    {
        const double w = 300, h = 120;
        Assert.Equal(new Hsv(200, 0, 1), ColorMix.Spectrum(200, 0, 0, w, h));
        Assert.Equal(new Hsv(200, 1, 0), ColorMix.Spectrum(200, w, h, w, h));
        Assert.Equal(new Hsv(200, 0.5, 0.75), ColorMix.Spectrum(200, 150, 30, w, h));
        // Held at the edges, however far past them.
        Assert.Equal(new Hsv(200, 0, 1), ColorMix.Spectrum(200, -40, -900, w, h));
        Assert.Equal(new Hsv(200, 1, 0), ColorMix.Spectrum(200, 5000, 5000, w, h));
        Assert.Equal((150.0, 30.0), ColorMix.SpectrumPoint(new Hsv(200, 0.5, 0.75), w, h));
        Assert.Equal((0.0, h), ColorMix.SpectrumPoint(new Hsv(0, -3, -3), w, h));
        foreach (var colour in Colours())
        {
            var hsv = ColorMix.ToHsv(colour);
            var (x, y) = ColorMix.SpectrumPoint(hsv, w, h);
            var back = ColorMix.Spectrum(hsv.H, x, y, w, h);
            Assert.Equal(colour, ColorMix.ToArgb(back, colour.A));
        }
        // An empty square says what it can.
        Assert.Equal(new Hsv(10, 0, 1), ColorMix.Spectrum(10, 5, 5, 0, 0));
    }

    /// <summary>A slider: its start none, its end the most; held at either; its point back; the keys' steps held at the ends.</summary>
    [Fact]
    public void ASlidersPointIsItsValueAndBack()
    {
        Assert.Equal(0, ColorMix.Slider(-20, 200, ColorMix.MostHue));
        Assert.Equal(360, ColorMix.Slider(999, 200, ColorMix.MostHue));
        Assert.Equal(90, ColorMix.Slider(50, 200, ColorMix.MostHue));
        Assert.Equal(50, ColorMix.SliderPoint(90, 200, ColorMix.MostHue));
        Assert.Equal(200, ColorMix.SliderPoint(400, 200, ColorMix.MostHue));
        Assert.Equal(0, ColorMix.Slider(10, 0, ColorMix.MostOpacity));
        for (var at = 0.0; at <= 200; at += 12.5)
        {
            Assert.Equal(at, ColorMix.SliderPoint(ColorMix.Slider(at, 200, ColorMix.MostOpacity), 200, ColorMix.MostOpacity), 6);
        }
        Assert.Equal(100, ColorMix.Stepped(95, ColorMix.PageStep, ColorMix.MostOpacity));
        Assert.Equal(0, ColorMix.Stepped(0, -ColorMix.Step, ColorMix.MostOpacity));
        Assert.Equal(11, ColorMix.Stepped(10, ColorMix.Step, ColorMix.MostHue));
    }

    /// <summary>The opacity slider's percent and an alpha go round to the same alpha, every one of them.</summary>
    [Fact]
    public void AnOpacityIsItsAlphaAndBack()
    {
        for (var alpha = 0; alpha <= 255; alpha++)
        {
            Assert.Equal((byte)alpha, ColorMix.Alpha(ColorMix.Opacity(new Argb((byte)alpha, 1, 2, 3))));
        }
        Assert.Equal((0.0, 100.0), (ColorMix.Opacity(default), ColorMix.Opacity(new Argb(255, 0, 0, 0))));
        Assert.Equal(((byte)0, (byte)255), (ColorMix.Alpha(-5), ColorMix.Alpha(250)));
    }

    /// <summary>Windows' colours, the backgrounds, the greys, the corners, and a spread of the rest at several alphas.</summary>
    private static IEnumerable<Argb> Colours()
    {
        foreach (var colour in ColorPalette.Presets)
        {
            yield return colour;
        }
        for (var grey = 0; grey <= 255; grey += 15)
        {
            yield return new Argb(255, (byte)grey, (byte)grey, (byte)grey);
        }
        byte[] steps = [0, 1, 37, 64, 128, 200, 254, 255];
        foreach (var r in steps)
        {
            foreach (var g in steps)
            {
                foreach (var b in steps)
                {
                    yield return new Argb((byte)(r ^ g ^ 0xFF), r, g, b);
                }
            }
        }
    }
}
