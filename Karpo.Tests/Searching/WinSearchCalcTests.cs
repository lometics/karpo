// §642: Windows search's answers, worked out on the PC: sums with their precedence, percentages, powers, functions in
// degrees, a quantity in another unit, the region's marks; and nothing for what asks no sum.

namespace Karpo.Tests.Searching;

public sealed class WinSearchCalcTests
{
    private static readonly NumberMarks English = NumberMarks.Invariant;
    private static readonly NumberMarks Swiss = new(".", "’");
    private static readonly NumberMarks German = new(",", ".");

    [Theory]
    [InlineData("12*3", "36")]
    [InlineData("12 × (3 + 4)", "84")]
    [InlineData("1+2*3", "7")]
    [InlineData("(1+2)*3", "9")]
    [InlineData("2^10", "1,024")]
    [InlineData("2^3^2", "512")]
    [InlineData("-3^2", "-9")]
    [InlineData("2^-1", "0.5")]
    [InlineData("10/4", "2.5")]
    [InlineData("1/3", "0.3333333333")]
    [InlineData("1+1=", "2")]
    [InlineData("15% of 80", "12")]
    [InlineData("50%", "0.5")]
    [InlineData("sqrt 2", "1.414213562")]
    [InlineData("sqrt(16)+1", "5")]
    [InlineData("sin 30", "0.5")]
    [InlineData("2pi", "6.283185307")]
    [InlineData("5!", "120")]
    [InlineData("17 mod 5", "2")]
    [InlineData("1e3+1", "1,001")]
    [InlineData("0.1+0.2", "0.3")]
    public void ASumIsWorkedOut(string typed, string value)
    {
        var answer = WinSearchCalc.Answer(typed, English);
        Assert.NotNull(answer);
        Assert.Equal(value, answer.Value);
        Assert.Equal("= " + value, answer.Text);
    }

    [Theory]
    [InlineData("5 km in mi", "3.106855961 mi")]
    [InlineData("100 f to c", "37.77777778 °C")]
    [InlineData("0 °C in °F", "32 °F")]
    [InlineData("-40 c in f", "-40 °F")]
    [InlineData("3 GB in MB", "3,072 MB")]
    [InlineData("1.5 h in min", "90 min")]
    [InlineData("10 lb in kg", "4.5359237 kg")]
    [InlineData("1 gallon = liters", "3.785411784 l")]
    [InlineData("100 km/h in mph", "62.13711922 mph")]
    [InlineData("273.15 K in c", "0 °C")]
    public void AQuantityIsPutInAnotherUnit(string typed, string text) =>
        Assert.Equal(text, WinSearchCalc.Answer(typed, English)?.Text);

    [Theory]
    [InlineData("")]
    [InlineData("42")]
    [InlineData("paint")]
    [InlineData("windows 11")]
    [InlineData("5 km in kg")]
    [InlineData("1/0")]
    [InlineData("(1+2")]
    [InlineData("2 +")]
    [InlineData("report 2024")]
    [InlineData("171!")]
    public void WhatAsksNoSumHasNoAnswer(string typed) => Assert.Null(WinSearchCalc.Answer(typed, English));

    [Fact]
    public void TheRegionsMarksAreReadAndWritten()
    {
        Assert.Equal("2,5", WinSearchCalc.Answer("1,5+1", German)?.Value);
        Assert.Equal("2,5", WinSearchCalc.Answer("1.5+1", German)?.Value);
        Assert.Equal("1.024", WinSearchCalc.Answer("2^10", German)?.Value);
        Assert.Equal("1’024", WinSearchCalc.Answer("2^10", Swiss)?.Value);
        Assert.Equal("3,106855961 mi", WinSearchCalc.Answer("5 km in mi", German)?.Text);
        Assert.Equal("5 km → mi", WinSearchCalc.Answer("5 km in mi", German)?.Question);
    }
}
