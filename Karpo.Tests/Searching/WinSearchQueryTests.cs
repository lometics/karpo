// §642: Windows search's question — a typed prefix's filter, the words, and the SQL Windows Search is asked: each word
// a prefix of the name (and of the contents where asked), escaped twice (CONTAINS' double quotes, SQL's single ones),
// a filter its System.Kind, files only but for mail, a line's own places as SCOPEs.

using Karpo.Tests.Localization;

namespace Karpo.Tests.Searching;

public sealed class WinSearchQueryTests
{
    [Theory]
    [InlineData("budget", null, "budget")]
    [InlineData("  budget 2024 ", null, "budget 2024")]
    [InlineData("documents: budget", WinSearchCategory.Documents, "budget")]
    [InlineData("Docs:budget", WinSearchCategory.Documents, "budget")]
    [InlineData("PHOTOS:  beach", WinSearchCategory.Photos, "beach")]
    [InlineData("pictures: beach", WinSearchCategory.Photos, "beach")]
    [InlineData("apps: code", WinSearchCategory.Apps, "code")]
    [InlineData("settings: display", WinSearchCategory.Settings, "display")]
    [InlineData("folders:", WinSearchCategory.Folders, "")]
    [InlineData("mail: invoice", WinSearchCategory.Email, "invoice")]
    [InlineData("web: weather", WinSearchCategory.Web, "weather")]
    [InlineData(@"c:\users", null, @"c:\users")]
    [InlineData("todo: to self", null, "todo: to self")]
    // §652: Karpo's own groups by their words.
    [InlineData("note: to self", WinSearchCategory.Notes, "to self")]
    [InlineData("calendar: offsite", WinSearchCategory.Events, "offsite")]
    [InlineData("people: anna", WinSearchCategory.People, "anna")]
    [InlineData("karpo: clock", WinSearchCategory.Karpo, "clock")]
    public void APrefixIsItsGroupsFilterAnythingElseIsTyped(string typed, WinSearchCategory? filter, string text)
    {
        var ask = WinSearchQuery.Parse(typed);
        Assert.Equal(filter, ask.Filter);
        Assert.Equal(text, ask.Text);
    }

    [Fact]
    public void AChipIsTheFilterUnlessAPrefixSaysAnother()
    {
        Assert.Equal(WinSearchCategory.Music, WinSearchQuery.Parse("queen", WinSearchCategory.Music).Filter);
        Assert.Equal(WinSearchCategory.Videos, WinSearchQuery.Parse("videos: queen", WinSearchCategory.Music).Filter);
        Assert.True(WinSearchQuery.Parse("   ").Empty);
    }

    [Fact]
    public void TheWordsLeaveOutControlCharactersAndWildcardsAndAreHeldShort()
    {
        Assert.Equal(["budget", "2024"], WinSearchQuery.Words("budget\t2024"));
        Assert.Equal(["bud", "get"], WinSearchQuery.Words("bud* *get\u0007"));
        Assert.Empty(WinSearchQuery.Words(" * ** "));
        Assert.Equal(WinSearchQuery.MostWords, WinSearchQuery.Words(string.Join(' ', Enumerable.Range(0, 20))).Count);
        Assert.Equal(WinSearchQuery.MostWordLength, WinSearchQuery.Words(new string('a', 500))[0].Length);
    }

    [Fact]
    public void AWordIsEscapedForContainsThenForSql()
    {
        Assert.Equal("'\"budget*\"'", WinSearchQuery.Term("budget"));
        Assert.Equal("'\"O''Brien*\"'", WinSearchQuery.Term("O'Brien"));
        Assert.Equal("'\"say\"\"hi*\"'", WinSearchQuery.Term("say\"hi"));
        Assert.Equal("'\"''\"\"''*\"'", WinSearchQuery.Term("'\"'"));
        Assert.Equal("'it''s'", WinSearchQuery.Literal("it's"));
    }

    [Fact]
    public void EveryWordIsAPrefixOfTheNameAndOfTheContentsWhereAsked()
    {
        var sql = WinSearchQuery.Sql(WinSearchQuery.Parse("tax ab"), contents: true, top: 50)!;
        Assert.Equal(
            "SELECT TOP 50 System.ItemNameDisplay, System.ItemPathDisplay, System.ItemUrl, System.ItemType, System.DateModified, System.Size, System.Search.Rank" +
            " FROM SystemIndex WHERE (CONTAINS(System.ItemNameDisplay, '\"tax*\"') OR CONTAINS(System.Search.Contents, '\"tax*\"'))" +
            " AND (CONTAINS(System.ItemNameDisplay, '\"ab*\"'))" +
            " ORDER BY System.Search.Rank DESC",
            sql);
        var names = WinSearchQuery.Sql(WinSearchQuery.Parse("taxes"), contents: false, top: 10)!;
        Assert.DoesNotContain("System.Search.Contents", names, StringComparison.Ordinal);
    }

    [Fact]
    public void AFilterIsItsKindAndEveryFilterButMailIsFilesOnly()
    {
        var photos = WinSearchQuery.Sql(WinSearchQuery.Parse("photos: beach"), contents: true, top: 40)!;
        Assert.Contains(" AND System.Kind = 'picture' AND SCOPE = 'file:' ORDER BY", photos, StringComparison.Ordinal);
        var folders = WinSearchQuery.Sql(WinSearchQuery.Parse("folders: work"), contents: true, top: 40)!;
        Assert.Contains("System.Kind = 'folder'", folders, StringComparison.Ordinal);
        var mail = WinSearchQuery.Sql(WinSearchQuery.Parse("email: invoice"), contents: true, top: 40)!;
        Assert.Contains("System.Kind = 'email'", mail, StringComparison.Ordinal);
        Assert.DoesNotContain("SCOPE", mail, StringComparison.Ordinal);
        // All: files and mail alike, no kind.
        var all = WinSearchQuery.Sql(WinSearchQuery.Parse("invoice"), contents: true, top: 40)!;
        Assert.DoesNotContain("System.Kind", all, StringComparison.Ordinal);
        Assert.DoesNotContain("SCOPE", all, StringComparison.Ordinal);
    }

    [Fact]
    public void TheIndexIsNotAskedForNothingNorForWhatItDoesNotHold()
    {
        Assert.Null(WinSearchQuery.Sql(WinSearchQuery.Parse(""), true, 50));
        Assert.Null(WinSearchQuery.Sql(WinSearchQuery.Parse("apps: code"), true, 50));
        Assert.Null(WinSearchQuery.Sql(WinSearchQuery.Parse("settings: display"), true, 50));
        Assert.Null(WinSearchQuery.Sql(WinSearchQuery.Parse("web: weather"), true, 50));
        Assert.Null(WinSearchQuery.Sql(WinSearchQuery.Parse("code"), true, 0));
    }

    [Fact]
    public void ALinesPlacesAreScopesAnyOfThem()
    {
        var sql = WinSearchQuery.Sql(WinSearchQuery.Parse("plan"), false, 20, [@"C:\Users\me\Documents\", "D:/Work", "file:E:/It's"])!;
        Assert.Contains(" AND (SCOPE = 'file:C:/Users/me/Documents' OR SCOPE = 'file:D:/Work' OR SCOPE = 'file:E:/It''s') ORDER BY", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryFilterHasAChipAndAName()
    {
        Assert.DoesNotContain(WinSearchCategory.Answer, WinSearchQuery.Filters);
        Assert.Equal(Enum.GetValues<WinSearchCategory>().Length, WinSearchQuery.Order.Length);
        Assert.All(WinSearchQuery.Order, category =>
        {
            Assert.False(string.IsNullOrWhiteSpace(WinSearchWords.Category(category)));
            Assert.False(string.IsNullOrWhiteSpace(WinSearchWords.Glyph(category)));
        });
        Assert.Equal(WinSearchQuery.Order.Length, WinSearchQuery.Order.Select(WinSearchWords.Category).Distinct().Count());
    }
}

/// <summary>§642: a prefix in the language Karpo speaks: one at a time, as every test that switches it.</summary>
[Collection(InLanguage.Collection)]
public sealed class WinSearchQueryLanguageTests
{
    [Fact]
    public void TheGroupsNameInTheLanguageKarpoSpeaksIsAPrefixToo()
    {
        using var german = InLanguage.Use("de");
        Assert.Equal(WinSearchCategory.Photos, WinSearchQuery.Parse("Fotos: Strand").Filter);
        Assert.Equal(WinSearchCategory.Documents, WinSearchQuery.Parse("dokumente: Steuer").Filter);
        Assert.Equal(WinSearchCategory.Documents, WinSearchQuery.Parse("documents: tax").Filter);
    }
}
