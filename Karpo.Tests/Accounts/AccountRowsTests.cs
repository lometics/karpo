// How the Accounts page writes an account in its list (Karpo.Accounts\AccountRows.cs): the
// line under the address says what Karpo shows of it, "Calendars, contacts, mail", and where it
// comes from.

using Karpo.Accounts;

namespace Karpo.Tests.Accounts;

public sealed class AccountRowsTests
{
    [Theory]
    [InlineData(AccountData.Calendars | AccountData.Contacts | AccountData.Mail, "Calendars, contacts, mail")]
    [InlineData(AccountData.Calendars | AccountData.Contacts, "Calendars, contacts")]
    [InlineData(AccountData.Calendars | AccountData.Mail, "Calendars, mail")]
    [InlineData(AccountData.Contacts | AccountData.Mail, "Contacts, mail")]
    [InlineData(AccountData.Calendars, "Calendars")]
    [InlineData(AccountData.Contacts, "Contacts")]
    [InlineData(AccountData.Mail, "Mail")]
    [InlineData(AccountData.None, "Nothing Karpo can show")]
    public void WhatAnAccountGivesReadsAsAList(AccountData data, string said) => Assert.Equal(said, AccountRows.Gives(data));

    [Fact]
    public void AMicrosoftAccountGivesEverythingFromMicrosoft()
    {
        var account = new Account { Id = "a", Kind = AccountKind.Microsoft, Address = "ann@outlook.com" };

        Assert.Equal("Calendars, contacts, mail · Microsoft account", AccountRows.Description(account));
    }

    [Fact]
    public void AGoogleAccountGivesEverythingFromGoogle()
    {
        var account = new Account { Id = "a", Kind = AccountKind.Google, Address = "ann@gmail.com" };

        Assert.Equal("Calendars, contacts, mail · Google account", AccountRows.Description(account));
        Assert.Equal(Karpo.Core.OptionAccountKind.Google, AccountRows.Option(AccountKind.Google));
        Assert.All(Enum.GetValues<AccountKind>(), kind => Assert.Equal(kind, AccountRows.Kind(AccountRows.Option(kind))));
    }

    [Fact]
    public void AMailServerGivesMailFromItsHost()
    {
        var account = new Account { Id = "b", Kind = AccountKind.Imap, Address = "ann@example.com", Host = "imap.example.com" };

        Assert.Equal("Mail · imap.example.com", AccountRows.Description(account));
    }

    /// <summary>A DAV account names the host of its calendar home, else of its address book's.</summary>
    [Fact]
    public void ADavAccountGivesWhatItsServerHasFromItsHost()
    {
        var both = new Account { Id = "c", Kind = AccountKind.Dav, Address = "ann@example.com", CalendarHome = "https://cloud.example.com/dav/calendars/ann/", BookHome = "https://books.example.com/ann/" };
        var books = new Account { Id = "d", Kind = AccountKind.Dav, Address = "ann@example.com", BookHome = "https://books.example.com/ann/" };

        Assert.Equal("Calendars, contacts · cloud.example.com", AccountRows.Description(both));
        Assert.Equal("Contacts · books.example.com", AccountRows.Description(books));
    }

    /// <summary>Where nothing says where it comes from, the line is what it gives alone, with no dangling dot.</summary>
    [Fact]
    public void NoHostLeavesWhatItGivesAlone()
    {
        var account = new Account { Id = "e", Kind = AccountKind.Imap, Address = "ann@example.com" };

        Assert.Equal("Mail", AccountRows.Description(account));
    }
}
