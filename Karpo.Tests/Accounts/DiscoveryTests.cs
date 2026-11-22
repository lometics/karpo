// Finding a mail server (Karpo.Accounts\Mail\MailDiscovery.cs): what an
// autoconfig file says, and the order the candidates are tried in — without a
// resolver or the network. And the loopback redirect's query, and an SRV
// answer with a compressed name.

using System.Net;
using Karpo.Accounts;
using Karpo.Accounts.Mail;

namespace Karpo.Tests.Accounts;

public sealed class DiscoveryTests
{
    private sealed class Nothing : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }

    private sealed class Dns(params ServiceRecord[] imaps) : IDnsLookup
    {
        public Task<IReadOnlyList<ServiceRecord>> ServicesAsync(string name, CancellationToken cancellation) =>
            Task.FromResult<IReadOnlyList<ServiceRecord>>(name.StartsWith("_imaps.", StringComparison.Ordinal) ? imaps : []);

        public Task<IReadOnlyList<string>> TextAsync(string name, CancellationToken cancellation) =>
            Task.FromResult<IReadOnlyList<string>>([]);
    }

    [Fact]
    public void AnAutoconfigFileNamesItsImapServer()
    {
        var found = MailDiscovery.Parse("""
            <clientConfig version="1.1">
              <emailProvider id="example.com">
                <incomingServer type="pop3"><hostname>pop.example.com</hostname></incomingServer>
                <incomingServer type="imap">
                  <hostname>imap.%EMAILDOMAIN%</hostname>
                  <port>143</port>
                  <socketType>STARTTLS</socketType>
                  <username>%EMAILLOCALPART%</username>
                </incomingServer>
              </emailProvider>
            </clientConfig>
            """, "anna@example.com")!;

        Assert.Equal("imap.example.com", found.Host);
        Assert.Equal(143, found.Port);
        Assert.Equal(MailSecurity.StartTls, found.Security);
        Assert.Equal("anna", found.User);
        Assert.Null(MailDiscovery.Parse("<clientConfig/>", "anna@example.com"));
        Assert.Null(MailDiscovery.Parse("not xml", "anna@example.com"));
    }

    [Fact]
    public async Task TheDomainsAnnouncementComesBeforeTheUsualNamesAndTheTypedServerLast()
    {
        var discovery = new MailDiscovery(new HttpClient(new Nothing()), new Dns(new ServiceRecord("mx.example.net.", 993, 0, 0)));

        var found = await discovery.FindAsync("anna@Example.com", "imap.example.com:143", CancellationToken.None);

        Assert.Equal(
            ["mx.example.net:993 SRV", "imap.example.com:993 the usual server names", "mail.example.com:993 the usual server names", "imap.example.com:143 what you typed"],
            found.Select(candidate => $"{candidate.Host}:{candidate.Port} {candidate.Step}"));
        Assert.Equal(MailSecurity.StartTls, found[^1].Security);
    }

    /// <summary>
    /// §338: a server set by hand with its security or port (the menu's form) is taken at its word, tried
    /// first: STARTTLS on 143 unless a port is set, SSL/TLS on 993; the rest are tried after it.
    /// </summary>
    [Fact]
    public async Task AServerSetWithItsSecurityIsTriedFirst()
    {
        var discovery = new MailDiscovery(new HttpClient(new Nothing()), new Dns());
        var found = await discovery.FindAsync("anna@example.com", "mail.example.org", CancellationToken.None, MailSecurity.StartTls);
        Assert.Equal(
            ["mail.example.org:143 what you typed", "imap.example.com:993 the usual server names", "mail.example.com:993 the usual server names"],
            found.Select(candidate => $"{candidate.Host}:{candidate.Port} {candidate.Step}"));
        Assert.Equal(MailSecurity.StartTls, found[0].Security);

        found = await discovery.FindAsync("anna@example.com", "mail.example.org", CancellationToken.None, MailSecurity.Implicit, 1993);
        Assert.Equal(("mail.example.org", 1993, MailSecurity.Implicit), (found[0].Host, found[0].Port, found[0].Security));
        Assert.Equal(3, found.Count);
    }

    [Fact]
    public async Task AServerFoundTwiceIsTriedOnce()
    {
        var discovery = new MailDiscovery(new HttpClient(new Nothing()), new Dns());
        var found = await discovery.FindAsync("anna@example.com", "imap.example.com", CancellationToken.None);
        Assert.Equal(2, found.Count);
    }

    [Fact]
    public void AnAddressWithoutADomainIsRefused() =>
        Assert.Throws<ArgumentException>(() => MailDiscovery.Domain("anna"));

    [Fact]
    public void TheRedirectsQueryCarriesTheCode()
    {
        var query = Loopback.Query("GET /?code=0.AX%2Bz&state=abc&session_state=x+y HTTP/1.1");
        Assert.Equal("0.AX+z", query["code"]);
        Assert.Equal("abc", query["state"]);
        Assert.Equal("x y", query["session_state"]);
        Assert.Empty(Loopback.Query("GET /favicon.ico HTTP/1.1"));
    }

    [Fact]
    public void AnSrvAnswerIsReadThroughItsCompressedNames()
    {
        // _imaps._tcp.example.com SRV 0 1 993 imap.example.com, the target's domain a pointer back
        // into the question.
        var question = DnsMessage.Query("_imaps._tcp.example.com", DnsMessage.Srv);
        var answer = new List<byte>(question);
        answer[2] = 0x81;
        answer[3] = 0x80;
        answer[7] = 1; // one answer
        answer.AddRange([0xC0, 0x0C, 0x00, 0x21, 0x00, 0x01, 0, 0, 0x0E, 0x10]);
        byte[] target = [4, (byte)'i', (byte)'m', (byte)'a', (byte)'p', 0xC0, 0x18];
        var data = new List<byte> { 0, 0, 0, 1, 0x03, 0xE1 };
        data.AddRange(target);
        answer.AddRange([0, (byte)data.Count]);
        answer.AddRange(data);

        var record = Assert.Single(DnsMessage.Services(answer.ToArray()));
        Assert.Equal("imap.example.com", record.Host);
        Assert.Equal(993, record.Port);
        Assert.Empty(DnsMessage.Services(answer.Take(20).ToArray()));
    }
}
