// Where an address's IMAP server is (after hearth's Discovery). Four steps,
// in order, and every answer is tried rather than the first winning on paper:
// only a server that takes the password has really been found.
//
// 1. autoconfig, the file Thunderbird looks for: the domain's own, its
//    .well-known, then Mozilla's database.
// 2. SRV (RFC 6186): _imaps._tcp, then _imap._tcp.
// 3. The usual names: imap. and mail. of the domain, on 993 with TLS.
// 4. What the person typed, last: a person types a server when the three
//    before it have nothing to say.
//
// Only the domain leaves this PC, to the domain itself, the resolver and
// Mozilla's database; the address goes to the domain's own autoconfig,
// because that is what the file's query string is for.

using System.Xml.Linq;

namespace Karpo.Accounts.Mail;

/// <summary>One IMAP server to try, and which step offered it.</summary>
public sealed record MailCandidate(string Step, string Host, int Port, MailSecurity Security, string User);

public sealed class MailDiscovery(HttpClient http, IDnsLookup dns)
{
    public const string Autoconfig = "autoconfig";
    public const string Srv = "SRV";
    public const string Usual = "the usual server names";
    public const string Typed = "what you typed";

    /// <summary>How long one autoconfig file is waited for.</summary>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(6);

    public MailDiscovery()
        : this(Web.Following, new SystemDnsLookup())
    {
    }

    /// <summary>
    /// Signs in to the first candidate that takes the password and says where it was.
    /// </summary>
    /// <param name="typedHost">A server the person typed; empty for none.</param>
    /// <param name="security">§338: how the typed server is spoken to, as the person set it (the menu's form); null for its port's way.</param>
    /// <param name="port">§338: the typed server's port, as the person set it; null for the one typed with it, else its security's usual one.</param>
    /// <exception cref="SignInRequiredException">A server that answered refused the password, and no other took it.</exception>
    /// <exception cref="AccountException">No server answered; the message says what was tried.</exception>
    public async Task<MailCandidate> SignInAsync(
        string address, string? user, string password, string typedHost, CancellationToken cancellation, MailSecurity? security = null, int? port = null)
    {
        var tried = new List<string>();
        SignInRequiredException? refused = null;
        foreach (var candidate in await FindAsync(address, typedHost, cancellation, security, port).ConfigureAwait(false))
        {
            var signing = string.IsNullOrWhiteSpace(user) ? candidate : candidate with { User = user.Trim() };
            try
            {
                using var client = await Imap.ConnectAsync(
                    signing.Host, signing.Port, signing.Security, signing.User, password, cancellation).ConfigureAwait(false);
                await client.DisconnectAsync(true, cancellation).ConfigureAwait(false);
                return signing;
            }
            catch (SignInRequiredException no)
            {
                refused ??= no;
                tried.Add($"{signing.Host}: refused the password");
            }
            catch (Exception error) when (error is IOException or System.Net.Sockets.SocketException
                or MailKit.ProtocolException or MailKit.Security.SslHandshakeException
                || (error is OperationCanceledException && !cancellation.IsCancellationRequested))
            {
                tried.Add($"{signing.Host} ({candidate.Step}): {error.Message}");
            }
        }
        if (refused is not null)
        {
            throw refused;
        }
        throw new AccountException(S.CoreAccountsNoMailServer(address: address, tried: string.Join("; ", tried)));
    }

    /// <summary>
    /// The places to try, in order, the typed one last — first where its security or port was set too
    /// (§338: the person's own server, taken at its word). Never empty for an address.
    /// </summary>
    public async Task<IReadOnlyList<MailCandidate>> FindAsync(string address, string typedHost, CancellationToken cancellation, MailSecurity? security = null, int? port = null)
    {
        var domain = Domain(address);
        var candidates = new List<MailCandidate>();
        MailCandidate? own = null;
        if (typedHost.Trim() is { Length: > 0 } typed)
        {
            var (host, written) = HostAndPort(typed);
            var way = security ?? ((port ?? written) == 143 ? MailSecurity.StartTls : MailSecurity.Implicit);
            own = new MailCandidate(Typed, host, port ?? written ?? (way == MailSecurity.StartTls ? 143 : 993), way, address);
        }
        var first = security is not null || port is not null;
        if (first && own is not null)
        {
            candidates.Add(own);
        }
        if (await AutoconfigAsync(address, domain, cancellation).ConfigureAwait(false) is { } configured)
        {
            candidates.Add(configured);
        }
        if (await SrvAsync(address, domain, cancellation).ConfigureAwait(false) is { } announced)
        {
            candidates.Add(announced);
        }
        candidates.Add(new MailCandidate(Usual, $"imap.{domain}", 993, MailSecurity.Implicit, address));
        candidates.Add(new MailCandidate(Usual, $"mail.{domain}", 993, MailSecurity.Implicit, address));
        if (!first && own is not null)
        {
            candidates.Add(own);
        }
        return [.. candidates.DistinctBy(candidate => (candidate.Host.ToLowerInvariant(), candidate.Port, candidate.User))];
    }

    /// <summary>The domain of an address.</summary>
    /// <exception cref="ArgumentException">It is not an address.</exception>
    public static string Domain(string address)
    {
        var at = address.LastIndexOf('@');
        return at > 0 && at < address.Length - 1
            ? address[(at + 1)..].Trim().ToLowerInvariant()
            : throw new ArgumentException($"{address} is not an email address.", nameof(address));
    }

    /// <summary>An autoconfig file's first IMAP server, or null where it names none.</summary>
    internal static MailCandidate? Parse(string xml, string address)
    {
        XDocument document;
        try
        {
            document = XDocument.Parse(xml);
        }
        catch (System.Xml.XmlException)
        {
            return null;
        }
        var imap = document.Descendants("incomingServer").FirstOrDefault(server => (string?)server.Attribute("type") == "imap");
        if (imap is null)
        {
            return null;
        }
        var host = Fill((string?)imap.Element("hostname") ?? "", address);
        if (host.Length == 0)
        {
            return null;
        }
        var security = ((string?)imap.Element("socketType"))?.ToUpperInvariant() switch
        {
            "STARTTLS" => MailSecurity.StartTls,
            "PLAIN" => MailSecurity.None,
            _ => MailSecurity.Implicit,
        };
        var port = int.TryParse((string?)imap.Element("port"), out var number) && number > 0 ? number : 993;
        var user = Fill((string?)imap.Element("username") ?? "%EMAILADDRESS%", address);
        return new MailCandidate(Autoconfig, host, port, security, user.Length > 0 ? user : address);
    }

    private async Task<MailCandidate?> AutoconfigAsync(string address, string domain, CancellationToken cancellation)
    {
        var escaped = Uri.EscapeDataString(address);
        string[] sources =
        [
            $"https://autoconfig.{domain}/mail/config-v1.1.xml?emailaddress={escaped}",
            $"https://{domain}/.well-known/autoconfig/mail/config-v1.1.xml",
            $"https://autoconfig.thunderbird.net/v1.1/{domain}",
        ];
        foreach (var source in sources)
        {
            try
            {
                using var patience = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
                patience.CancelAfter(Patience);
                var xml = await http.GetStringAsync(source, patience.Token).ConfigureAwait(false);
                if (Parse(xml, address) is { } found)
                {
                    return found;
                }
            }
            catch (Exception error) when (error is HttpRequestException
                || (error is OperationCanceledException && !cancellation.IsCancellationRequested))
            {
                // Not there, or not answering. The next source may be.
            }
        }
        return null;
    }

    private async Task<MailCandidate?> SrvAsync(string address, string domain, CancellationToken cancellation)
    {
        foreach (var (name, security) in new[] { ($"_imaps._tcp.{domain}", MailSecurity.Implicit), ($"_imap._tcp.{domain}", MailSecurity.StartTls) })
        {
            var records = await dns.ServicesAsync(name, cancellation).ConfigureAwait(false);
            if (records.Count > 0)
            {
                return new MailCandidate(Srv, records[0].Host.TrimEnd('.'), records[0].Port, security, address);
            }
        }
        return null;
    }

    /// <summary>"host" or "host:port".</summary>
    internal static (string Host, int? Port) HostAndPort(string typed)
    {
        var colon = typed.LastIndexOf(':');
        return colon > 0 && int.TryParse(typed[(colon + 1)..], out var port) && port is > 0 and < 65536
            ? (typed[..colon], port)
            : (typed, null);
    }

    private static string Fill(string template, string address)
    {
        var at = address.LastIndexOf('@');
        return template
            .Replace("%EMAILADDRESS%", address, StringComparison.Ordinal)
            .Replace("%EMAILLOCALPART%", at > 0 ? address[..at] : address, StringComparison.Ordinal)
            .Replace("%EMAILDOMAIN%", at > 0 ? address[(at + 1)..] : "", StringComparison.Ordinal)
            .Trim();
    }
}
