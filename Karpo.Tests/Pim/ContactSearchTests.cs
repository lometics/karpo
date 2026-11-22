// What the contacts flyout shows for what was typed (ContactSearch).
//
// The names here are the shapes the person's own address book actually holds:
// accents, an organization with no email, a phone-only contact from a phone.

using Karpo.Accounts;
using Karpo.Dock.Pim;

namespace Karpo.Tests.Pim;

public sealed class ContactSearchTests
{
    private static Person Contact(
        string name, string? organization = null, string[]? emails = null, string[]? phones = null) =>
        new(name, name, organization, emails ?? [], phones ?? []);

    private static readonly IReadOnlyList<Person> Book =
    [
        Contact("Adrian Hager", "Bühler AG", ["adrian.hager@buhlergroup.com"]),
        Contact("Adrian Peier"),
        Contact("Aaron Schläpfer", emails: ["aaron@example.ch"]),
        Contact("Air-Glacier", "Air-Glacier SA", phones: ["+41 27 329 14 15"]),
        Contact("Beat Hagenbuch", phones: ["079 123 45 67"]),
        Contact("411 & More"),
    ];

    [Fact]
    public void NothingTypedShowsEveryContactInTheOrderTheyCame()
    {
        // The store sorted them; an empty box must not re-sort or drop any.
        Assert.Same(Book, ContactSearch.Match(Book, ""));
        Assert.Same(Book, ContactSearch.Match(Book, "   "));
        Assert.Same(Book, ContactSearch.Match(Book, null));
    }

    [Fact]
    public void ANameThatBeginsWithWhatWasTypedComesFirst()
    {
        var found = ContactSearch.Match(Book, "adrian");

        Assert.Equal(["Adrian Hager", "Adrian Peier"], found.Select(contact => contact.DisplayName));
    }

    [Fact]
    public void AWordOfTheNameCountsAndRanksUnderTheFirstWord()
    {
        // "hag" begins Hager's surname and Hagenbuch's; neither begins a display name, so they
        // rank together and fall back to the name's own order.
        var found = ContactSearch.Match(Book, "hag");

        Assert.Equal(["Adrian Hager", "Beat Hagenbuch"], found.Select(contact => contact.DisplayName));
    }

    [Fact]
    public void AWordThatBeginsANameOutranksOneMerelyHeldInside()
    {
        var book = new[] { Contact("Sandra Gerber"), Contact("Gerry Ott") };

        var found = ContactSearch.Match(book, "ger");

        // "Gerry" begins its display name; "Gerber" only begins a later word.
        Assert.Equal(["Gerry Ott", "Sandra Gerber"], found.Select(contact => contact.DisplayName));
    }

    [Fact]
    public void AccentsAreNotSomethingAnyoneHasToType()
    {
        Assert.Equal(
            ["Aaron Schläpfer"],
            ContactSearch.Match(Book, "schlapfer").Select(contact => contact.DisplayName));

        // And typing the accent still finds it.
        Assert.Equal(
            ["Aaron Schläpfer"],
            ContactSearch.Match(Book, "schläpfer").Select(contact => contact.DisplayName));
    }

    [Fact]
    public void AnEmailOrAnOrganizationAnswersButRanksUnderEveryName()
    {
        var found = ContactSearch.Match(Book, "buhler");

        // Nothing is named Bühler; Adrian Hager answers through his organization.
        Assert.Equal(["Adrian Hager"], found.Select(contact => contact.DisplayName));

        Assert.Equal(
            ["Aaron Schläpfer"],
            ContactSearch.Match(Book, "example.ch").Select(contact => contact.DisplayName));
    }

    [Fact]
    public void APhoneIsFoundByItsDigitsHoweverEitherSideWroteThem()
    {
        // The contact holds "079 123 45 67"; the person types it without the spaces.
        Assert.Equal(
            ["Beat Hagenbuch"],
            ContactSearch.Match(Book, "0791234567").Select(contact => contact.DisplayName));

        // And the other way round: the contact holds "+41 27 329 14 15".
        Assert.Equal(
            ["Air-Glacier"],
            ContactSearch.Match(Book, "27 329").Select(contact => contact.DisplayName));
    }

    [Fact]
    public void LettersThatAreNotDigitsNeverMatchEveryContactWithAPhone()
    {
        // The defect this guards: folding "abc" to an empty digit string, which every phone
        // number contains, so every contact with a number would answer.
        Assert.Empty(ContactSearch.Match(Book, "zzz"));
    }

    [Fact]
    public void ANameOfDigitsIsStillANameAndOutranksAPhone()
    {
        var found = ContactSearch.Match(Book, "411");

        Assert.Equal("411 & More", found[0].DisplayName);
    }

    [Fact]
    public void TheDetailLineIsTheEmailThenThePhoneThenTheOrganization()
    {
        Assert.Equal("adrian.hager@buhlergroup.com", Book[0].Detail);
        Assert.Equal("+41 27 329 14 15", Book[3].Detail);
        Assert.Equal("Bühler AG", Contact("X", "Bühler AG").Detail);
        Assert.Null(Contact("X").Detail);
    }

    [Fact]
    public void FoldingLeavesNothingOfWhitespaceOrCase()
    {
        Assert.Equal("adrian hager", ContactSearch.Fold("  Adrian Hager "));
        Assert.Equal("", ContactSearch.Fold(null));
        Assert.Equal("", ContactSearch.Fold("   "));
    }

    [Fact]
    public void OnlyDigitsSurviveAPhoneNumber()
    {
        Assert.Equal("41273291415", ContactSearch.Digits("+41 27 329 14 15"));
        Assert.Equal("", ContactSearch.Digits("n/a"));
        Assert.Equal("", ContactSearch.Digits(null));
    }
}
