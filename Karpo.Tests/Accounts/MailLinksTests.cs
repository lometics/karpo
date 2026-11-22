// Where a message opens (Karpo.Accounts\Mail\MailLinks.cs): the account's web mail, the message itself
// where it can be named, the inbox of a web mail known by its server else, and nothing for a server
// Karpo knows no web mail for. Only https is ever handed on.

using Karpo.Accounts;

namespace Karpo.Tests.Accounts;

public sealed class MailLinksTests
{
    [Fact]
    public void OnlyAnHttpsAddressIsALink()
    {
        Assert.Equal("https://outlook.office.com/mail/id/AAQk", MailLinks.Web(" https://outlook.office.com/mail/id/AAQk "));
        Assert.Null(MailLinks.Web("http://outlook.office.com/mail/"));
        Assert.Null(MailLinks.Web("file:///C:/Windows/notepad.exe"));
        Assert.Null(MailLinks.Web("ms-settings:display"));
        Assert.Null(MailLinks.Web("outlook.office.com/mail"));
        Assert.Null(MailLinks.Web(""));
        Assert.Null(MailLinks.Web(null));
    }

    [Fact]
    public void GmailFindsTheMessageByItsMessageId()
    {
        Assert.Equal(
            "https://mail.google.com/mail/u/me%40gmail.com/#search/rfc822msgid%3ACAF%2Babc%40mail.gmail.com",
            MailLinks.Imap("imap.gmail.com", "me@gmail.com", "<CAF+abc@mail.gmail.com>"));
        Assert.Equal(
            "https://mail.google.com/mail/u/me%40googlemail.com/#inbox",
            MailLinks.Imap("IMAP.googlemail.com.", "me@googlemail.com", null));
        // §687: a Google account's message, the same search as Gmail's IMAP server's.
        Assert.Equal(MailLinks.Imap("imap.gmail.com", "me@gmail.com", "<CAF+abc@mail.gmail.com>"), MailLinks.Google("me@gmail.com", "<CAF+abc@mail.gmail.com>"));
    }

    [Fact]
    public void AnotherKnownServerOpensItsInboxAndAnUnknownOneNothing()
    {
        Assert.Equal("https://outlook.live.com/mail/", MailLinks.Imap("imap-mail.outlook.com", "me@outlook.com", "<id@x>"));
        Assert.Equal("https://outlook.office.com/mail/", MailLinks.Imap("outlook.office365.com", "me@contoso.com", null));
        Assert.Equal("https://www.icloud.com/mail/", MailLinks.Imap("imap.mail.me.com", "me@icloud.com", null));
        Assert.Null(MailLinks.Imap("mail.example.ch", "me@example.ch", "<id@example.ch>"));
        Assert.Null(MailLinks.Imap(null, "me@example.ch", null));
        Assert.All(new[] { "imap.gmail.com", "imap-mail.outlook.com", "imap.mail.yahoo.com", "imap.aol.com" },
            host => Assert.NotNull(MailLinks.Web(MailLinks.Imap(host, "me@example.com", null))));
    }
}
