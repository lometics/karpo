// §526: Karpo's settings never wear the person's look (the person, 2026-11-07: "the settings should never take the style of
// the user, they should always take the style that we have for it (in our case the base windows 11 based style.)"). The edit
// bar and its panel, the sections' edit bars and every menu of edit mode are drawn in Dock.SettingsLook: Windows' own — its
// mode, its accent, high contrast, transparency effects —, none of a layout's colours, outlines, text colour or font.

using Karpo.Dock;
using Karpo.Dock.QuickSettings.Controls;
using Karpo.Tests.Folders;
using Color = Windows.UI.Color;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Edit;

public sealed class SettingsLookTests
{
    /// <summary>
    /// A default layout dressed in everything odd a look can say — its menus' (Widgets in folders') background, outline, text and
    /// font, its flyouts' outline, its widgets' accent and font, a bar whose ground reads in the other mode —: the look the
    /// dock reads (ReadLook: Windows', Configured with the default layout's menus' font) gives the settings Windows' own
    /// tokens all the same, in either mode, with transparency effects on or off, in Segoe UI Variable. The layout's own look
    /// does not: the odd values are in effect.
    /// </summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void TheSettingsWearWindowsOwnWhateverTheLayoutWears(bool dark, bool glass)
    {
        var windows = TestLooks.LookFor(dark) with { Glass = glass };
        var config = new Config();
        Dress(config, dark);
        var read = windows.Plain().Configured(config);
        var settings = TheDock.SettingsLookOf(read);

        // Windows' tokens, every one: a menu's, a taskbar flyout's, a folder's; the text boxes' too.
        Assert.Equal(FlyoutTheme.For(windows), FlyoutTheme.For(settings));
        Assert.Equal(FlyoutTheme.Taskbar(windows), FlyoutTheme.Taskbar(settings));
        Assert.Equal(FlyoutTheme.Folder(windows), FlyoutTheme.Folder(settings));
        Assert.Equal(QuickTheme.For(FlyoutTheme.For(windows)), QuickTheme.For(FlyoutTheme.For(settings)));
        // What Windows decides: its mode, its accent, transparency effects; nothing chosen; its default font.
        Assert.Equal(dark, settings.Dark);
        Assert.Equal(glass, settings.Glass);
        Assert.Equal(windows.Accent, settings.Accent);
        Assert.Equal(windows.AccentLight2, settings.AccentLight2);
        Assert.Equal(windows.AccentDark1, settings.AccentDark1);
        Assert.True(settings.Colors.IsAutomatic);
        Assert.Null(settings.Ink);
        Assert.Equal(default, settings.Typeface);
        Assert.Equal(default, settings.MenuTypeface);

        // The odd look is in effect where the person's look is worn: the dock's look as read (its chrome's font) and the layout's.
        Assert.NotEqual(default, read.MenuTypeface);
        var worn = FlyoutTheme.For(windows.Wearing(config));
        var plain = FlyoutTheme.For(windows);
        Assert.NotEqual(plain.Base, worn.Base);
        Assert.NotEqual(plain.Stroke, worn.Stroke);
        Assert.NotEqual(plain.Text, worn.Text);
        Assert.NotEqual(plain.SubtleHover, worn.SubtleHover);
        Assert.NotEqual(windows.Accent, windows.Wearing(config).Accent);
    }

    /// <summary>High contrast: the settings in Windows' system colours, as every app is, whatever the layout says.</summary>
    [Fact]
    public void TheSettingsInHighContrastAreWindowsSystemColours()
    {
        var windows = TestLooks.LookFor(dark: true) with
        {
            HighContrast = true,
            HcWindow = Color.FromArgb(255, 0, 0, 0),
            HcText = Color.FromArgb(255, 255, 255, 255),
            HcHighlight = Color.FromArgb(255, 0x1A, 0xEB, 0xFF),
            HcHighlightText = Color.FromArgb(255, 0, 0, 0),
        };
        var config = new Config();
        Dress(config, dark: true);
        var settings = TheDock.SettingsLookOf(windows.Plain().Configured(config));

        var theme = FlyoutTheme.For(settings);
        Assert.Equal(FlyoutTheme.For(windows), theme);
        Assert.True(theme.HighContrast);
        Assert.Equal(windows.HcWindow, theme.Base);
        Assert.Equal(windows.HcHighlight, theme.SubtleHover);
        Assert.Equal(default, settings.MenuTypeface);
    }

    /// <summary>
    /// <paramref name="config"/>'s default layout made to say something odd of every part a settings surface could take from
    /// it, its bar's ground of the other mode than <paramref name="dark"/> (SettingsWornTests too).
    /// </summary>
    internal static void Dress(Config config, bool dark)
    {
        var layout = config.DefaultLayout;
        Karpo.Core.Paint Of(string hex) => new(Argb.Parse(hex)!.Value);
        var menus = Looks.Section(config, LookPart.FolderWidgets);
        Assert.True(LookShorthand.Both(menus, Tone.Background, Of("#FF00FF")));
        Assert.True(LookShorthand.Both(menus, Tone.Text, Of("#FF8000")));
        Assert.True(Looks.WriteFont(menus, new TextStyle("Cascadia Mono", 600)));
        Assert.True(LookShorthand.Both(Looks.Section(config, LookPart.Flyouts), Tone.Outline, Of("#FFB900")));
        var widgets = Looks.Section(config, LookPart.Widgets);
        Assert.True(LookShorthand.Both(widgets, Tone.Accent, Of("#E81123")));
        Assert.True(Looks.WriteFont(widgets, new TextStyle("Georgia")));
        // A ground of the other mode: the bar reads light on a dark Windows, dark on a light one.
        Assert.True(LookShorthand.Both(Looks.Section(config, LookPart.Taskbar), Tone.Background, Of(dark ? "#F0F0F0" : "#101010")));
    }
}
