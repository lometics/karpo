// The look the tests draw widgets and pictures in: the dock's accents, either theme. One place, so
// pictures from different kinds are drawn alike and can be laid side by side.

using Karpo.Dock;
using Color = Windows.UI.Color;

namespace Karpo.Tests.Folders;

internal static class TestLooks
{
    /// <summary>The look the pictures are drawn in: the dock's accents, either theme.</summary>
    public static Look LookFor(bool dark, float textScale = 1f) => new(
        Glass: false,
        Dark: dark,
        Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4),
        AccentLight2: Color.FromArgb(255, 0x99, 0xEB, 0xFF),
        AccentDark1: Color.FromArgb(255, 0x00, 0x5F, 0xB8),
        TextScale: textScale);
}
