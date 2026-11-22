// What a contact's row offers and what Windows is handed for it (ContactActs, docs\widgets.md §102):
// the details a line each, Email and Call only where Windows can open them, the mailto: and tel:
// addresses, and what each copy puts on the clipboard. Also the scrolling that brings an opened row
// into sight (ListScrolled.Revealing).

using Karpo.Accounts;
using Karpo.Dock;
using Karpo.Dock.Pim;
using TerraFX.Interop.DirectX;

namespace Karpo.Tests.Pim;

public sealed class ContactActsTests
{
    private static Person Contact(string? organization = null, string[]? emails = null, string[]? phones = null, string name = "Adrian Hager") =>
        new("a", name, organization, emails ?? [], phones ?? []);

    [Fact]
    public void TheDetailsAreEachAddressEachNumberThenTheOrganization()
    {
        var person = Contact("Contoso", ["a@contoso.com", " ", "A@Contoso.com", "b@example.com"], ["+41 79 123 45 67", ""]);
        Assert.Equal(
            [
                new ContactDetail(DetailKind.Email, "a@contoso.com"),
                new ContactDetail(DetailKind.Email, "b@example.com"),
                new ContactDetail(DetailKind.Phone, "+41 79 123 45 67"),
                new ContactDetail(DetailKind.Organization, "Contoso"),
            ],
            ContactActs.Details(person));
        Assert.Empty(ContactActs.Details(Contact(" ")));
    }

    /// <summary>What a row offers follows what the person has: an address to write to, a number to call, Ctrl+C's first of either.</summary>
    [Fact]
    public void TheActsFollowWhatThePersonHas()
    {
        var both = Contact("Contoso", ["adrian@contoso.com"], ["+41 79 123 45 67"]);
        Assert.Equal("adrian@contoso.com", ContactActs.FirstEmail(both));
        Assert.Equal("+41 79 123 45 67", ContactActs.FirstPhone(both));
        Assert.Equal("adrian@contoso.com", ContactActs.Primary(both));

        var phone = Contact(phones: ["079 123 45 67"]);
        Assert.Null(ContactActs.FirstEmail(phone));
        Assert.Equal("079 123 45 67", ContactActs.Primary(phone));

        // An organization alone: nothing to write to, call or copy with Ctrl+C.
        var organization = Contact("Fabrikam");
        Assert.Null(ContactActs.FirstEmail(organization));
        Assert.Null(ContactActs.FirstPhone(organization));
        Assert.Null(ContactActs.Primary(organization));

        // An address Windows cannot open is copied, never written to; the next one is.
        var broken = Contact(emails: ["not an address", "b@example.com"], phones: ["n/a", "+1 555 0100"]);
        Assert.Equal("b@example.com", ContactActs.FirstEmail(broken));
        Assert.Equal("+1 555 0100", ContactActs.FirstPhone(broken));
        Assert.Equal("not an address", ContactActs.Primary(broken));
        Assert.Null(ContactActs.Details(broken)[0].Uri);
    }

    [Theory]
    [InlineData("adrian@contoso.com", "mailto:adrian@contoso.com")]
    [InlineData("  first.last+tag@example.co.uk ", "mailto:first.last+tag@example.co.uk")]
    [InlineData("mailto:a@b.ch", "mailto:a@b.ch")]
    [InlineData("<a@b.ch>", "mailto:a@b.ch")]
    // Nothing in an address can start a subject or a body, or end the address.
    [InlineData("a?subject=hi@b.ch", "mailto:a%3Fsubject%3Dhi@b.ch")]
    [InlineData("a&b#c%d@e.ch", "mailto:a%26b%23c%25d@e.ch")]
    [InlineData("a/b@c.ch", "mailto:a%2Fb@c.ch")]
    [InlineData("jürg@bücher.ch", "mailto:j%C3%BCrg@b%C3%BCcher.ch")]
    [InlineData("😀@b.ch", "mailto:%F0%9F%98%80@b.ch")]
    [InlineData("", null)]
    [InlineData("adrian", null)]
    [InlineData("@contoso.com", null)]
    [InlineData("adrian@", null)]
    [InlineData("adrian hager@contoso.com", null)]
    [InlineData("a@b.ch\r\nBcc: x@y.z", null)]
    [InlineData(null, null)]
    public void MailUriEscapesAnAddress(string? address, string? uri) =>
        Assert.Equal(uri, ContactActs.MailUri(address));

    [Theory]
    [InlineData("+41 79 123 45 67", "tel:+41791234567")]
    [InlineData("079 123 45 67", "tel:0791234567")]
    [InlineData("(555) 123-4567", "tel:5551234567")]
    [InlineData("+1 (555) 123.4567", "tel:+15551234567")]
    [InlineData("044/555 01 02", "tel:0445550102")]
    [InlineData("tel:+41791234567", "tel:+41791234567")]
    [InlineData("+41 44 555 01 02 ext. 12", "tel:+41445550102;ext=12")]
    [InlineData("+41 44 555 01 02 x12", "tel:+41445550102;ext=12")]
    [InlineData("555 0100 #7", "tel:5550100;ext=7")]
    [InlineData("+1 555 0100;ext=22", "tel:+15550100;ext=22")]
    [InlineData("Box12", "tel:12")]
    [InlineData("12 + 34", "tel:1234")]
    [InlineData("n/a", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void TelUriKeepsTheDigits(string? number, string? uri) =>
        Assert.Equal(uri, ContactActs.TelUri(number));

    /// <summary>"Copy contact details": the name, the organization, every address, every number, a line each.</summary>
    [Fact]
    public void AllTextIsTheWholePersonALineEach()
    {
        Assert.Equal(
            "Adrian Hager\r\nContoso\r\nadrian@contoso.com\r\na@hager.ch\r\n+41 79 123 45 67",
            ContactActs.AllText(Contact("Contoso", ["adrian@contoso.com", "a@hager.ch"], ["+41 79 123 45 67"])));
        Assert.Equal("Adrian Hager", ContactActs.AllText(Contact()));
        Assert.Equal("a@b.ch", ContactActs.AllText(Contact(emails: ["a@b.ch"], name: " ")));
    }

    /// <summary>A band is brought into sight under the head as little as it takes; its top wins where it is taller than the window.</summary>
    [Theory]
    // In sight: nothing moves.
    [InlineData(100f, 150f, 50f, 50f)]
    // Below the window: its foot to the window's.
    [InlineData(300f, 340f, 0f, 140f)]
    // Above it, under the head: its top to the head's foot.
    [InlineData(60f, 90f, 100f, 20f)]
    // Taller than the window: its top.
    [InlineData(300f, 700f, 0f, 260f)]
    // Never past what can scroll.
    [InlineData(950f, 1000f, 0f, 800f)]
    [InlineData(0f, 30f, 500f, 0f)]
    public void ABandIsRevealedAsLittleAsItTakes(float top, float bottom, float offset, float revealed)
    {
        var laid = new ListScrolled(new D2D_RECT_F(0, 0, 300, 200), 1000, 40, offset);
        Assert.Equal(revealed, laid.Revealing(top, bottom));
    }
}
