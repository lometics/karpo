// §547: the bar's Background blur (the person, 2026-11-07: "the taskbar itself should be a little transparent and should
// show the desktop background image a little blurred behind it. (if possible) how blurry the background is should be
// changeable."): the Appearance's Taskbar says how blurred the wallpaper under its see-through background is, 0 to 100 %,
// Karpo's own 30 % where it says nothing; its row stands right after the Background on the Taskbar's look.

namespace Karpo.Tests.Core;

public sealed class BarBlurSettingTests
{
    private static OptionRow Row(Config config, string key) =>
        OptionRows.Find(LookRows.Rows(config, config.DefaultLayout, windows: true), key)!;

    private static string BlurKey => LookRows.SectionKey(LookPart.Taskbar) + ".blur";

    /// <summary>Right after the Background, a percentage 0 to 100 at Karpo's 30, in force on Karpo's see-through bar.</summary>
    [Fact]
    public void TheTaskbarsBlurStandsAfterItsBackground()
    {
        var config = new Config();
        // §592: the Bar tab's level.
        var level = LookRows.Rows(config, config.DefaultLayout, windows: true).Single(row => row.Key == LookRows.SectionKey(LookPart.Taskbar) + LookRows.Tab).Level;

        var at = level.ToList().FindIndex(row => row.Key == BlurKey);
        Assert.Equal(LookRows.ColourKey(LookRows.SectionKey(LookPart.Taskbar), Tone.Background), level[at - 1].Key);
        var blur = level[at];
        Assert.Equal((OptionRowKind.Number, S.SettingsLookBlur, OptionUnit.Percent), (blur.Kind, blur.Title, blur.Unit));
        Assert.Equal((30, 0, 100), (blur.Value, blur.Least, blur.Most));
        Assert.True(blur.Enabled);
        Assert.Null(blur.Why);
        Assert.Equal(30, Looks.Of(config, dark: true).Taskbar.Blur);
    }

    /// <summary>
    /// Set, it is the file's ("blur" in the Taskbar's look) and read back so; held to 0 to 100; Karpo's 30 set again is
    /// written as any value (§574: no default to fall back to), Karpo's palette worn again.
    /// </summary>
    [Fact]
    public void ItIsWrittenReadBackAndHeld()
    {
        var config = new Config();
        Assert.True(Row(config, BlurKey).Set!(60));
        Assert.Contains("\"blur\": 60", config.ToJson(), StringComparison.Ordinal);
        var read = Configs.RoundTrip(config, out _);
        Assert.Equal(60, read.WornValues?.Taskbar?.Blur);
        Assert.Equal(60, Looks.Of(read, dark: true).Taskbar.Blur);
        Assert.Equal(60, Row(read, BlurKey).Value);

        Assert.True(Row(config, BlurKey).Set!(250));
        Assert.Equal(100, Looks.Of(config, dark: true).Taskbar.Blur);
        Assert.True(Row(config, BlurKey).Set!(0));
        Assert.Equal(0, Looks.Of(config, dark: true).Taskbar.Blur);

        Assert.True(Row(config, BlurKey).Set!(KarpoLook.Blur));
        Assert.Equal(KarpoLook.Blur, config.WornValues?.Taskbar?.Blur);
        Assert.Same(LookPalettes.All[0], LookPalettes.Worn(config));
    }

    /// <summary>A file's blur out of range is held to it; one on another section is no setting and goes.</summary>
    [Fact]
    public void AFilesBlurIsHeldToTheBar()
    {
        var bar = new LookValues { Blur = -20 };
        bar.Normalize(LookPart.Taskbar);
        Assert.Equal(0, bar.Blur);
        foreach (var part in new[] { LookPart.Widgets, LookPart.Flyouts, LookPart.FolderWidgets })
        {
            var other = new LookValues { Blur = 40 };
            other.Normalize(part);
            Assert.True(other.IsEmpty);
            Assert.Null(Looks.Over(new LookValues { Blur = 40 }, default, part, dark: true).Blur);
        }
        Assert.Equal(40, Looks.Over(new LookValues { Blur = 40 }, KarpoLook.Paints(true).Taskbar, LookPart.Taskbar, dark: true).Blur);
    }

    /// <summary>Greyed, saying why, where the bar is not see-through: a solid background, or None; in force at any opacity between.</summary>
    [Theory]
    [InlineData("#202020", false)]
    [InlineData("none", false)]
    [InlineData("#80FF8C00", true)]
    [InlineData("#10202020", true)]
    public void ItCountsOnlyWhereTheBarIsSeeThrough(string background, bool shown)
    {
        var config = new Config();
        Assert.True(LookShorthand.Both(Looks.Section(config, LookPart.Taskbar), Tone.Background, Karpo.Core.Paint.Parse(background)));
        var blur = Row(config, BlurKey);
        Assert.Equal(shown, blur.Enabled);
        Assert.Equal(shown ? null : S.SettingsLookBlurWhy, blur.Why);
    }

    /// <summary>No section but the Taskbar has it; a palette worn leaves it Karpo's own.</summary>
    [Fact]
    public void OnlyTheBarHasIt()
    {
        var config = new Config();
        Assert.Single(OptionRows.Flat(LookRows.Rows(config, config.DefaultLayout, windows: true)), row => row.Key.EndsWith(".blur", StringComparison.Ordinal));
        Assert.True(LookPalettes.Wear(config, LookPalettes.All[1]));
        Assert.Equal(KarpoLook.Blur, Looks.Of(config, dark: true).Taskbar.Blur);
    }

    /// <summary>
    /// The setting's percent is the Gaussian's radius in DIPs, its standard deviation a third of it, in px at the display's
    /// scale: none at 0, 10 px at Karpo's 30 % at 100 %, 15 px at 150 %, a third of 100 DIPs at the most.
    /// </summary>
    [Theory]
    [InlineData(0, 1f, 0f)]
    [InlineData(30, 1f, 10f)]
    [InlineData(30, 1.5f, 15f)]
    [InlineData(100, 1f, 33.333f)]
    [InlineData(150, 1f, 33.333f)]
    [InlineData(-5, 2f, 0f)]
    public void ItsSigmaIsAThirdOfItsRadius(int blur, float scale, float sigma) =>
        Assert.Equal(sigma, KarpoLook.Sigma(blur, scale), 2);
}
