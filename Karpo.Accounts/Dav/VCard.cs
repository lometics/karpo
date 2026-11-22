// Just enough vCard (RFC 6350, and the 3.0 and 2.1 that servers still hold)
// for the contacts panel: FN, N, ORG, EMAIL, TEL and PHOTO. Everything else is
// skipped.
//
// PHOTO comes three ways: 4.0's data: URI ("data:image/jpeg;base64,..."), 3.0's
// and 2.1's base64 under ENCODING=b, BASE64 or a bare BASE64 parameter (folded
// over many lines, which the unfolding joins), or a URL to fetch. A read of the
// book keeps only a hash of it (Person.Photo); the picture itself is read when
// the dock has none of that version (DavReader.PhotoAsync, Photo).
//
// A content line is unfolded (a line starting with a space or tab continues
// the one before), its name is read up to the first unquoted ';' or ':', and
// its value unescaped. 2.1's QUOTED-PRINTABLE is decoded, because old address
// books exported from phones are full of it.

using System.Globalization;
using System.Text;

namespace Karpo.Accounts.Dav;

internal static class VCard
{
    /// <summary>The person in one vCard, or null where it holds no name, email or phone at all.</summary>
    /// <param name="id">What the person is known by: the card's href.</param>
    internal static Person? Parse(string text, string id)
    {
        string? full = null;
        string? structured = null;
        string? organization = null;
        var emails = new List<string>();
        var phones = new List<string>();
        string? photo = null;
        var inside = false;

        foreach (var (name, parameters, raw) in Lines(text))
        {
            switch (name)
            {
                case "BEGIN" when raw.Equals("VCARD", StringComparison.OrdinalIgnoreCase):
                    inside = true;
                    continue;
                case "END" when raw.Equals("VCARD", StringComparison.OrdinalIgnoreCase):
                    inside = false;
                    continue;
            }
            if (!inside)
            {
                continue;
            }
            var value = Decode(raw, parameters);
            switch (name)
            {
                case "FN":
                    full ??= Unescape(value).Trim();
                    break;
                case "N":
                    structured ??= Named(value);
                    break;
                case "ORG":
                    organization ??= Unescape(value.Split(';')[0]).Trim();
                    break;
                case "EMAIL":
                    if (Unescape(value).Trim() is { Length: > 0 } email)
                    {
                        emails.Add(email);
                    }
                    break;
                case "TEL":
                    if (Unescape(value).Trim() is { Length: > 0 } phone)
                    {
                        // 4.0 allows a tel: URI; the panel shows the number.
                        phones.Add(phone.StartsWith("tel:", StringComparison.OrdinalIgnoreCase) ? phone[4..] : phone);
                    }
                    break;
                case "PHOTO" when photo is null && raw.Trim().Length > 0:
                    photo = Tag(parameters, raw);
                    break;
            }
        }

        var display = !string.IsNullOrWhiteSpace(full) ? full
            : !string.IsNullOrWhiteSpace(structured) ? structured
            : !string.IsNullOrWhiteSpace(organization) ? organization
            : emails.FirstOrDefault() ?? phones.FirstOrDefault();
        return display is null
            ? null
            : new Person(id, display, string.IsNullOrWhiteSpace(organization) ? null : organization, emails, phones) { Photo = photo };
    }

    /// <summary>A PHOTO's version: a hash of all it says, short, so a card keeps no picture in memory.</summary>
    private static string Tag(string parameters, string value) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(parameters + ":" + value.Trim())))[..16];

    /// <summary>
    /// The first PHOTO of a vCard: its bytes where the card holds them (a data: URI, base64), or the
    /// http(s) address it names; neither where it has none, or one that does not read.
    /// </summary>
    internal static (byte[]? Data, Uri? Url) Photo(string text)
    {
        foreach (var (name, parameters, raw) in Lines(text))
        {
            if (name != "PHOTO" || raw.Trim() is not { Length: > 0 } value)
            {
                continue;
            }
            if (value.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                // data:[<mediatype>][;base64],<data>
                var comma = value.IndexOf(',');
                if (comma < 0)
                {
                    return (null, null);
                }
                var data = value[(comma + 1)..];
                return value[..comma].EndsWith(";base64", StringComparison.OrdinalIgnoreCase)
                    ? (Base64(data), null)
                    : (Encoding.Latin1.GetBytes(Uri.UnescapeDataString(data)), null);
            }
            var coded = parameters.Split(';').Any(parameter =>
                parameter.Trim().ToUpperInvariant() is "ENCODING=B" or "ENCODING=BASE64" or "BASE64");
            if (!coded && Uri.TryCreate(value, UriKind.Absolute, out var url))
            {
                return url.Scheme == Uri.UriSchemeHttps || url.Scheme == Uri.UriSchemeHttp ? (null, url) : (null, null);
            }
            // Base64 under its encoding, or (some exports) with none said.
            return (Base64(value), null);
        }
        return (null, null);
    }

    /// <summary>Base64, whatever white space it was folded with; null where it is not.</summary>
    private static byte[]? Base64(string text)
    {
        try
        {
            var bytes = Convert.FromBase64String(text);
            return bytes.Length > 0 ? bytes : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    /// <summary>N's parts (family; given; additional; prefix; suffix) as "given additional family".</summary>
    private static string? Named(string value)
    {
        var parts = value.Split(';').Select(part => Unescape(part).Trim()).ToArray();
        string Part(int index) => index < parts.Length ? parts[index] : "";
        var name = string.Join(' ', new[] { Part(3), Part(1), Part(2), Part(0), Part(4) }.Where(part => part.Length > 0));
        return name.Length > 0 ? name : null;
    }

    /// <summary>A value as its ENCODING and CHARSET parameters say (2.1's QUOTED-PRINTABLE).</summary>
    private static string Decode(string value, string parameters)
    {
        if (parameters.Contains("QUOTED-PRINTABLE", StringComparison.OrdinalIgnoreCase))
        {
            var bytes = new List<byte>();
            for (var i = 0; i < value.Length; i++)
            {
                if (value[i] == '=' && i + 2 < value.Length
                    && byte.TryParse(value.AsSpan(i + 1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var one))
                {
                    bytes.Add(one);
                    i += 2;
                }
                else if (value[i] != '=')
                {
                    bytes.AddRange(Encoding.UTF8.GetBytes(value[i].ToString()));
                }
            }
            return Encoding.UTF8.GetString([.. bytes]);
        }
        return value;
    }

    /// <summary>A text value with its backslash escapes undone.</summary>
    internal static string Unescape(string value)
    {
        if (!value.Contains('\\'))
        {
            return value;
        }
        var text = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '\\' && i + 1 < value.Length)
            {
                var next = value[++i];
                text.Append(next is 'n' or 'N' ? '\n' : next);
            }
            else
            {
                text.Append(value[i]);
            }
        }
        return text.ToString();
    }

    /// <summary>
    /// The content lines of a vCard, unfolded: the name (upper case, group prefix dropped), the raw
    /// parameters, and the value.
    /// </summary>
    internal static IEnumerable<(string Name, string Parameters, string Value)> Lines(string text)
    {
        var unfolded = new List<string>();
        var softBreak = false;
        foreach (var line in text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            if (unfolded.Count > 0 && (softBreak || line.StartsWith(' ') || line.StartsWith('\t')))
            {
                unfolded[^1] += softBreak ? line : line[1..];
            }
            else
            {
                unfolded.Add(line);
            }
            // A QUOTED-PRINTABLE line ending in '=' goes on in the next, unindented.
            softBreak = line.EndsWith('=') && unfolded[^1].Contains("QUOTED-PRINTABLE", StringComparison.OrdinalIgnoreCase);
            if (softBreak)
            {
                unfolded[^1] = unfolded[^1][..^1];
            }
        }

        foreach (var line in unfolded)
        {
            var colon = -1;
            var quoted = false;
            for (var i = 0; i < line.Length; i++)
            {
                if (line[i] == '"')
                {
                    quoted = !quoted;
                }
                else if (line[i] == ':' && !quoted)
                {
                    colon = i;
                    break;
                }
            }
            if (colon <= 0)
            {
                continue;
            }
            var head = line[..colon];
            var semicolon = head.IndexOf(';');
            var name = (semicolon < 0 ? head : head[..semicolon]).ToUpperInvariant();
            var dot = name.LastIndexOf('.');
            if (dot >= 0)
            {
                // "item1.EMAIL", Apple's grouping.
                name = name[(dot + 1)..];
            }
            yield return (name, semicolon < 0 ? "" : head[(semicolon + 1)..], line[(colon + 1)..]);
        }
    }
}
