// The icon gallery's search (Karpo.Core\IconSearch.cs): what the picker
// shows while a name is typed, and that every glyph in the gallery is a real one.


namespace Karpo.Tests.Core;

public sealed class IconSearchTests
{
    [Fact]
    public void NothingTypedShowsEveryGlyph()
    {
        Assert.Equal(IconSearch.All.Count, IconSearch.Search("").Count);
        Assert.Equal(IconSearch.All.Count, IconSearch.Search("   ").Count);
        Assert.Equal(IconSearch.All.Count, IconSearch.Search(null).Count);
    }

    [Theory]
    [InlineData("folder", "Folder")]
    [InlineData("FOLDER", "Folder")]
    [InlineData("folder open", "Open folder")]
    [InlineData("open folder", "Open folder")]
    [InlineData("  clock ", "Recent")]
    [InlineData("house", "Home")]
    public void EveryWordHasToBeInItsWords(string query, string expected)
    {
        var found = IconSearch.Search(query);

        Assert.Contains(found, choice => choice.Name == expected);
        Assert.All(found, choice => Assert.All(
            query.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries),
            word => Assert.Contains(choice.Words, each => each.Contains(word, StringComparison.CurrentCultureIgnoreCase))));
    }

    [Fact]
    public void ANameNothingHasFindsNothing()
    {
        Assert.Empty(IconSearch.Search("zzzz"));
        Assert.Empty(IconSearch.Search("folder zzzz"));
    }

    /// <summary>Every choice is one Segoe Fluent Icons glyph in the private-use area, with words of its own to find it by.</summary>
    [Fact]
    public void TheGalleryIsGlyphsWithNames()
    {
        Assert.NotEmpty(IconSearch.All);
        Assert.All(IconSearch.All, choice =>
        {
            Assert.Equal(1, choice.Glyph.Length);
            Assert.InRange(choice.Glyph[0], (char)0xE000, (char)0xF8FF);
            Assert.NotNull(Strings.Lookup(choice.Key));
            Assert.False(string.IsNullOrWhiteSpace(choice.Name));
        });
        Assert.Equal(IconSearch.All.Count, IconSearch.All.Select(choice => choice.Id).Distinct().Count());
        // And every key of the gallery's words is one of its glyphs': none is left over.
        Assert.Equal(
            Strings.Keys.Where(key => key.StartsWith("settings.iconPicker.icon.", StringComparison.Ordinal)).Order(),
            IconSearch.All.Select(choice => choice.Key).Order());
    }
}
