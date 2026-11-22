// A folder panel's hairline (SeparatorInk): whole device pixels at every scale. §511: the bar's separator, which this
// painted too, is gone; §524: a folder divider's rule and its colour too, its room striped arranged (SpacerStripesTests).

using Karpo.Dock;

namespace Karpo.Tests.Sizing;

public sealed class SeparatorInkTests
{
    [Theory]
    [InlineData(1f, 1f)]
    [InlineData(1.25f, 1f)]
    [InlineData(1.5f, 2f)]
    [InlineData(2f, 2f)]
    public void AHairlineIsWholePixels(float scale, float hairline) => Assert.Equal(hairline, SeparatorInk.Hairline(scale));
}
