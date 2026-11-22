// Where a widget may be carried (Karpo.Core\Partners.cs, 2026-10-09): within its own type only. A
// taskbar entry moves along a bar and from bar to bar, a list's line within its list and from one
// list folder to another, and nothing crosses between the taskbar and a folder — the person: "even
// though it would technically be possible ... it does not work for every widget so i would rather
// disable it completely". Every refusal names the widget and says the move isn't supported.

using Karpo.Tests.Localization;

namespace Karpo.Tests.Core;

public sealed class PartnersTests
{
    /// <summary>Two types are left: the taskbar's entries and a list folder's lines. The grid went.</summary>
    [Fact]
    public void TheTypesAreTheBarAndTheList() =>
        Assert.Equal([WidgetType.Bar, WidgetType.List], Enum.GetValues<WidgetType>());

    /// <summary>A widget's type is the place it stands in: a folder's widget is a line, anything else an entry.</summary>
    [Fact]
    public void AWidgetIsOfTheTypeOfItsPlace()
    {
        Assert.Equal(WidgetType.Bar, Partners.TypeOf(BarKind.NightLight.New()));
        Assert.Equal(WidgetType.List, Partners.TypeOf(ListKind.Clock.New()));
        Assert.Equal(WidgetType.List, Partners.TypeOf(FolderView.List));
    }

    /// <summary>
    /// Both ways are refused, for every kind of either registry: the subject has a form on the
    /// other side or not, it does not cross.
    /// </summary>
    [Fact]
    public void NothingCrossesBetweenTheTaskbarAndAFolder()
    {
        foreach (var entry in BarEntries.All)
        {
            var island = entry.Kind.New();
            Assert.True(Partners.CanBe(island, WidgetType.Bar), entry.Kind.ToString());
            Assert.False(Partners.CanBe(island, WidgetType.List), entry.Kind.ToString());
            Assert.Null(Partners.Move(island, WidgetType.Bar, WidgetType.List));
        }
        foreach (var line in ListWidgets.All)
        {
            var island = line.Kind.New();
            Assert.True(Partners.CanBe(island, WidgetType.List), line.Kind.ToString());
            Assert.False(Partners.CanBe(island, WidgetType.Bar), line.Kind.ToString());
            Assert.Null(Partners.Move(island, WidgetType.List, WidgetType.Bar));
        }
    }

    /// <summary>
    /// Within one type a move is the widget itself, as it is: a line keeps the height somebody gave
    /// it, an entry its options.
    /// </summary>
    [Fact]
    public void AMoveWithinOneTypeIsTheWidgetItself()
    {
        var line = ListKind.Calendar.New();
        line.Height = LineHeight.Exactly(6);
        var entry = BarKind.NightLight.New();

        Assert.Same(line, Partners.Move(line, WidgetType.List, WidgetType.List));
        Assert.Equal(LineHeight.Exactly(6), line.Height);
        Assert.Same(entry, Partners.Move(entry, WidgetType.Bar, WidgetType.Bar));
    }

    /// <summary>
    /// Every refusal names the widget and says that moving between the taskbar and folders isn't
    /// supported; where the widget may go there is nothing to say.
    /// </summary>
    [Fact]
    public void EveryRefusalNamesTheWidgetAndSaysItIsNotSupported()
    {
        var entry = BarKind.NightLight.New();
        var line = ListKind.Clock.New();

        var intoFolder = Partners.WhyNot(entry, WidgetType.List);
        var ontoBar = Partners.WhyNot(line, WidgetType.Bar);

        Assert.StartsWith(entry.Info!.Title, intoFolder, StringComparison.Ordinal);
        Assert.Contains("from the taskbar into a folder", intoFolder, StringComparison.Ordinal);
        Assert.Contains("isn't supported", intoFolder, StringComparison.Ordinal);
        Assert.StartsWith(line.Info!.Title, ontoBar, StringComparison.Ordinal);
        Assert.Contains("from a folder onto the taskbar", ontoBar, StringComparison.Ordinal);
        Assert.Contains("isn't supported", ontoBar, StringComparison.Ordinal);
        Assert.Equal("", Partners.WhyNot(entry, WidgetType.Bar));
        Assert.Equal("", Partners.WhyNot(line, WidgetType.List));
    }

}

/// <summary>Both refusals in every language Karpo speaks (InLanguage: one at a time, after the rest).</summary>
[Collection(InLanguage.Collection)]
public sealed class PartnersLanguageTests
{
    /// <summary>Every language says both refusals, each with the widget's name in it, and they differ.</summary>
    [Theory]
    [InlineData("en")]
    [InlineData("de")]
    [InlineData("es")]
    [InlineData("fr")]
    [InlineData("zh-Hans")]
    public void EveryLanguageSaysBothRefusals(string language)
    {
        using var speaking = InLanguage.Use(language);
        var entry = BarKind.NightLight.New();
        var line = ListKind.Clock.New();

        var intoFolder = Partners.WhyNot(entry, WidgetType.List);
        var ontoBar = Partners.WhyNot(line, WidgetType.Bar);

        Assert.Contains(entry.Info!.Title, intoFolder, StringComparison.Ordinal);
        Assert.Contains(line.Info!.Title, ontoBar, StringComparison.Ordinal);
        Assert.NotEqual(intoFolder.Replace(entry.Info!.Title, "", StringComparison.Ordinal), ontoBar.Replace(line.Info!.Title, "", StringComparison.Ordinal));
    }
}
