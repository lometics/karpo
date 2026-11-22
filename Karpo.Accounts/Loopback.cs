// The redirect a desktop sign-in comes back to: a socket on the loopback
// interface, on a port Windows picks, listening for exactly one request
// (RFC 8252 §7.3). Ported from hearth's Google provider.
//
// Microsoft registers the redirect as http://localhost, and a browser may
// resolve localhost to ::1 before 127.0.0.1, so the port is opened on both.
// A raw socket rather than HttpListener, because reserving an HTTP prefix on
// Windows wants an administrator.

using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Karpo.Accounts;

internal sealed class Loopback : IDisposable
{
    private readonly TcpListener four;
    private readonly TcpListener? six;

    internal Loopback()
    {
        four = new TcpListener(IPAddress.Loopback, 0);
        four.Start();
        Port = ((IPEndPoint)four.LocalEndpoint).Port;
        try
        {
            six = new TcpListener(IPAddress.IPv6Loopback, Port);
            six.Start();
        }
        catch (SocketException)
        {
            // No IPv6 loopback, or the port is taken there: the browser falls back to 127.0.0.1.
            six = null;
        }
    }

    internal int Port { get; }

    /// <summary>What the authorization request names as its redirect.</summary>
    internal string RedirectUri => $"http://localhost:{Port.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>
    /// Waits for the browser to come back and answers it with a page saying it can be closed. What
    /// comes back is the query, which carries the authorization code or the refusal.
    /// </summary>
    internal async Task<IReadOnlyDictionary<string, string>> WaitAsync(CancellationToken cancellation)
    {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        var waits = new List<Task<TcpClient>> { four.AcceptTcpClientAsync(stop.Token).AsTask() };
        if (six is not null)
        {
            waits.Add(six.AcceptTcpClientAsync(stop.Token).AsTask());
        }

        while (true)
        {
            var done = await Task.WhenAny(waits).ConfigureAwait(false);
            waits.Remove(done);
            using var client = await done.ConfigureAwait(false);
            var query = await AnswerAsync(client, cancellation).ConfigureAwait(false);
            // A browser's own request for /favicon.ico carries nothing: keep listening.
            if (query.Count > 0)
            {
                stop.Cancel();
                return query;
            }
            waits.Add(client.Client.LocalEndPoint is IPEndPoint { AddressFamily: AddressFamily.InterNetworkV6 } && six is not null
                ? six.AcceptTcpClientAsync(stop.Token).AsTask()
                : four.AcceptTcpClientAsync(stop.Token).AsTask());
        }
    }

    public void Dispose()
    {
        four.Stop();
        six?.Stop();
    }

    private static async Task<IReadOnlyDictionary<string, string>> AnswerAsync(TcpClient client, CancellationToken cancellation)
    {
        using var stream = client.GetStream();
        // The request line and no further: everything the redirect carries is on it.
        var line = new StringBuilder();
        var one = new byte[1];
        while (line.Length < 8192)
        {
            var read = await stream.ReadAsync(one, cancellation).ConfigureAwait(false);
            if (read == 0 || one[0] == (byte)'\n')
            {
                break;
            }
            if (one[0] != (byte)'\r')
            {
                line.Append((char)one[0]);
            }
        }
        var query = Query(line.ToString());
        var page = query.Count > 0 ? Page : "HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"; // not-ui: HTTP
        await stream.WriteAsync(Encoding.UTF8.GetBytes(page), cancellation).ConfigureAwait(false);
        await stream.FlushAsync(cancellation).ConfigureAwait(false);
        return query;
    }

    /// <summary>The query of a <c>GET /?a=b&amp;c=d HTTP/1.1</c> request line.</summary>
    internal static IReadOnlyDictionary<string, string> Query(string requestLine)
    {
        var query = new Dictionary<string, string>(StringComparer.Ordinal);
        var parts = requestLine.Split(' ');
        if (parts.Length < 2)
        {
            return query;
        }
        var mark = parts[1].IndexOf('?', StringComparison.Ordinal);
        if (mark < 0)
        {
            return query;
        }
        foreach (var pair in parts[1][(mark + 1)..].Split('&'))
        {
            var equals = pair.IndexOf('=', StringComparison.Ordinal);
            if (equals > 0)
            {
                query[Uri.UnescapeDataString(pair[..equals])] = Uri.UnescapeDataString(pair[(equals + 1)..].Replace('+', ' '));
            }
        }
        return query;
    }

    /// <summary>What the browser is left showing: no script, nothing fetched. In the language Karpo speaks.</summary>
    private static string Page =>
        "HTTP/1.1 200 OK\r\n" // not-ui: HTTP
        + "Content-Type: text/html; charset=utf-8\r\n" // not-ui: HTTP
        + "Connection: close\r\n" // not-ui: HTTP
        + "\r\n"
        + $"<!doctype html><html lang=\"{Karpo.Core.Languages.Tag(Karpo.Core.Strings.Language)}\"><head><meta charset=\"utf-8\">"
        + "<title>Karpo</title></head><body style=\"font-family:system-ui;"
        + $"margin:4rem auto;max-width:28rem\"><h1>{WebUtility.HtmlEncode(S.CoreAccountsPageSignedIn)}</h1>"
        + $"<p>{WebUtility.HtmlEncode(S.CoreAccountsPageClose)}</p></body></html>";
}
