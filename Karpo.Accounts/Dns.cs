// SRV and TXT lookups for discovery (RFC 6186 for mail, RFC 6764 for DAV).
//
// .NET resolves names and nothing else, so the packet is built and read here,
// over UDP to the resolvers Windows has configured (ported from hearth's two
// copies, made one). Nothing here retries and nothing here fails a sign-in: a
// resolver that says nothing is a discovery step that found nothing, which is
// what the next step is for. Nothing security-carrying is decided from an
// answer either — it names a server, and the sign-in that follows still has
// to succeed against it.

using System.Buffers.Binary;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace Karpo.Accounts;

/// <summary>One <c>SRV</c> record: where a service lives and how much it is preferred.</summary>
public sealed record ServiceRecord(string Host, int Port, int Priority, int Weight);

/// <summary>The lookups discovery makes; behind an interface so discovery is tried in tests without a resolver.</summary>
public interface IDnsLookup
{
    /// <summary>The <c>SRV</c> records of a name such as <c>_caldavs._tcp.example.com</c>, most preferred first.</summary>
    Task<IReadOnlyList<ServiceRecord>> ServicesAsync(string name, CancellationToken cancellation);

    /// <summary>The <c>TXT</c> strings of a name, which for DAV carry <c>path=/dav/</c>.</summary>
    Task<IReadOnlyList<string>> TextAsync(string name, CancellationToken cancellation);
}

public sealed class SystemDnsLookup : IDnsLookup
{
    /// <summary>How long one resolver is given. Discovery has more steps behind it.</summary>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(2);

    public async Task<IReadOnlyList<ServiceRecord>> ServicesAsync(string name, CancellationToken cancellation) =>
        await AskAsync(name, DnsMessage.Srv, cancellation).ConfigureAwait(false) is { } answer ? DnsMessage.Services(answer) : [];

    public async Task<IReadOnlyList<string>> TextAsync(string name, CancellationToken cancellation) =>
        await AskAsync(name, DnsMessage.Txt, cancellation).ConfigureAwait(false) is { } answer ? DnsMessage.Text(answer) : [];

    private static async Task<byte[]?> AskAsync(string name, int type, CancellationToken cancellation)
    {
        foreach (var resolver in Resolvers())
        {
            try
            {
                using var client = new UdpClient(resolver.AddressFamily);
                using var patience = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
                patience.CancelAfter(Patience);
                await client.SendAsync(DnsMessage.Query(name, type), new IPEndPoint(resolver, 53), patience.Token).ConfigureAwait(false);
                var answer = await client.ReceiveAsync(patience.Token).ConfigureAwait(false);
                return answer.Buffer;
            }
            catch (Exception error) when (error is SocketException or OperationCanceledException or ArgumentException)
            {
                // That resolver did not answer in time. The next one may.
                cancellation.ThrowIfCancellationRequested();
            }
        }
        return null;
    }

    private static IEnumerable<IPAddress> Resolvers() => NetworkInterface
        .GetAllNetworkInterfaces()
        .Where(card => card.OperationalStatus == OperationalStatus.Up)
        .SelectMany(card => card.GetIPProperties().DnsAddresses)
        .Where(address => address.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6)
        .Distinct()
        .Take(3);
}

/// <summary>A DNS query, and the <c>SRV</c> and <c>TXT</c> records in an answer.</summary>
/// <remarks>
/// <b>Name compression is the whole difficulty.</b> A name in an answer may end in a pointer back
/// into the message, and a target read without following one comes out truncated. So every name is
/// read through <see cref="Name"/>, and a pointer that goes forward or in a circle ends the read.
/// </remarks>
internal static class DnsMessage
{
    internal const int Srv = 33;
    internal const int Txt = 16;
    private const int Header = 12;
    private const int In = 1;

    /// <summary>A query for one name, recursion desired, with a fixed id (the socket asks once and is thrown away).</summary>
    internal static byte[] Query(string name, int type)
    {
        List<byte> message = [0x48, 0x42, 0x01, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00];
        foreach (var label in name.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            var bytes = Encoding.ASCII.GetBytes(label);
            if (bytes.Length is 0 or > 63)
            {
                throw new ArgumentException($"{name} is not a name DNS can carry.", nameof(name));
            }
            message.Add((byte)bytes.Length);
            message.AddRange(bytes);
        }
        message.Add(0);
        message.AddRange([(byte)(type >> 8), (byte)type, 0x00, In]);
        return [.. message];
    }

    /// <summary>The <c>SRV</c> records of an answer, most preferred first; a malformed answer is none.</summary>
    internal static IReadOnlyList<ServiceRecord> Services(ReadOnlySpan<byte> message)
    {
        var found = new List<ServiceRecord>();
        foreach (var (type, at, length) in Records(message))
        {
            if (type != Srv || length < 7)
            {
                continue;
            }
            var target = Name(message, at + 6, out _);
            if (target is { Length: > 0 })
            {
                found.Add(new ServiceRecord(
                    target.TrimEnd('.'),
                    BinaryPrimitives.ReadUInt16BigEndian(message[(at + 4)..]),
                    BinaryPrimitives.ReadUInt16BigEndian(message[at..]),
                    BinaryPrimitives.ReadUInt16BigEndian(message[(at + 2)..])));
            }
        }
        // RFC 2782's "." target means "not offered here", which is nothing found rather than a host called nothing.
        return
        [
            .. found
                .Where(record => record.Host.Length > 0 && record.Port > 0)
                .OrderBy(record => record.Priority)
                .ThenByDescending(record => record.Weight)
                .ThenBy(record => record.Host, StringComparer.Ordinal),
        ];
    }

    /// <summary>The <c>TXT</c> strings of an answer, each character-string on its own.</summary>
    internal static IReadOnlyList<string> Text(ReadOnlySpan<byte> message)
    {
        var found = new List<string>();
        foreach (var (type, at, length) in Records(message))
        {
            if (type != Txt)
            {
                continue;
            }
            var end = at + length;
            var part = at;
            while (part < end && part < message.Length)
            {
                int size = message[part];
                if (part + 1 + size > end || part + 1 + size > message.Length)
                {
                    break;
                }
                found.Add(Encoding.ASCII.GetString(message.Slice(part + 1, size)));
                part += 1 + size;
            }
        }
        return found;
    }

    private static List<(int Type, int At, int Length)> Records(ReadOnlySpan<byte> message)
    {
        var records = new List<(int, int, int)>();
        if (message.Length < Header)
        {
            return records;
        }
        int questions = BinaryPrimitives.ReadUInt16BigEndian(message[4..]);
        int answers = BinaryPrimitives.ReadUInt16BigEndian(message[6..]);
        var at = Header;
        for (var i = 0; i < questions && at > 0; i++)
        {
            at = Skip(message, at);
            at = at > 0 && at + 4 <= message.Length ? at + 4 : -1;
        }
        for (var i = 0; i < answers && at > 0; i++)
        {
            at = Skip(message, at);
            if (at < 0 || at + 10 > message.Length)
            {
                break;
            }
            int type = BinaryPrimitives.ReadUInt16BigEndian(message[at..]);
            int length = BinaryPrimitives.ReadUInt16BigEndian(message[(at + 8)..]);
            var data = at + 10;
            at = data + length;
            if (at <= message.Length)
            {
                records.Add((type, data, length));
            }
        }
        return records;
    }

    /// <summary>The name at <paramref name="at"/>, following compression pointers; null where it runs out or loops.</summary>
    private static string? Name(ReadOnlySpan<byte> message, int at, out int end)
    {
        var name = new StringBuilder();
        var start = at;
        end = -1;
        while (at >= 0 && at < message.Length)
        {
            int length = message[at];
            if ((length & 0xC0) == 0xC0)
            {
                if (at + 1 >= message.Length)
                {
                    return null;
                }
                var pointer = ((length & 0x3F) << 8) | message[at + 1];
                if (pointer >= start)
                {
                    return null;
                }
                end = end < 0 ? at + 2 : end;
                start = pointer;
                at = pointer;
                continue;
            }
            if (length == 0)
            {
                end = end < 0 ? at + 1 : end;
                return name.ToString();
            }
            if (at + 1 + length > message.Length)
            {
                return null;
            }
            if (name.Length > 0)
            {
                name.Append('.');
            }
            name.Append(Encoding.ASCII.GetString(message.Slice(at + 1, length)));
            at += 1 + length;
        }
        return null;
    }

    private static int Skip(ReadOnlySpan<byte> message, int at) => Name(message, at, out var end) is null ? -1 : end;
}
