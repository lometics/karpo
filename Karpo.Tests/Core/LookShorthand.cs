// Tests' shorthand for the Appearance (§507, §567): a test that only needs the bar's opacity to be something writes it into
// its Taskbar section, as Windows' own light colour at that opacity; a test that needs a bar in one of the designed looks
// applies its palette; one that needs the cards coloured writes their sections.

namespace Karpo.Tests;

internal static class LookShorthand
{
    /// <summary>Windows' own (null) and the palettes, light and dark, as the pictures go through them.</summary>
    public static readonly string?[] Palettes = [null, .. LookPalettes.All.Select(palette => palette.Id)];

    /// <summary>A config in palette <paramref name="palette"/> (Karpo's own for none), its bar at <paramref name="taskbarOpacity"/> where one is said.</summary>
    public static Config Wearing(string? palette = null, int? taskbarOpacity = null)
    {
        var config = new Config();
        if (LookPalettes.Of(palette) is { } designed)
        {
            _ = LookPalettes.Wear(config, designed);
        }
        if (taskbarOpacity is { } bar)
        {
            config.TaskbarOpacity = bar;
        }
        return config;
    }

    /// <summary>§568: <paramref name="tone"/> at <paramref name="slot"/> set to <paramref name="paint"/> in both of Windows' modes; false where neither changed.</summary>
    public static bool Both(LookSlot slot, Tone tone, Paint? paint) => Looks.WritePaint(slot, tone, false, paint) | Looks.WritePaint(slot, tone, true, paint);

    /// <summary>§568: a colour pair saying <paramref name="both"/> in both modes.</summary>
    public static ColorPair Pair(string? both) => new() { Dark = both, Light = both };

    extension(Config config)
    {
        /// <summary>§577: the values the palette worn keeps over its design, as the file has them; null for none.</summary>
        public PaletteValues? WornValues => config.Appearance?[LookPalettes.Worn(config).Id];

        /// <summary>
        /// The bar's opacity; set, the Appearance's Taskbar section's background at it — the colour set there, else Windows' own
        /// of the light mode.
        /// </summary>
        public int TaskbarOpacity
        {
            get => SurfaceStyle.OpacityOf(Looks.Of(config, dark: true).Taskbar.Background);
            set
            {
                var now = Looks.Of(config, dark: true).Taskbar.Background.Color ?? ToneRules.WindowsBackground(false);
                _ = Both(Looks.Section(config, LookPart.Taskbar), Tone.Background, new Paint(now.At(value / 100.0)));
            }
        }

        /// <summary>
        /// The Appearance's sections with cards — the bar's widgets, the folders' lines and the menus — in
        /// <paramref name="paints"/>' colours.
        /// </summary>
        public void Colour(PartPaints paints)
        {
            foreach (var part in new[] { LookPart.Widgets, LookPart.FolderWidgets })
            {
                var slot = Looks.Section(config, part);
                foreach (var tone in ToneNames.All)
                {
                    _ = Both(slot, tone, paints[tone].IsWindows ? null : paints[tone]);
                }
            }
        }
    }
}
