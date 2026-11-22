// What can be done with one person of the contacts' line (docs\widgets.md §102, "A person's details"):
// each of their details on a line of its own, what a line offers (copied always; an address written
// to, a number called), and the addresses Windows is handed for that — mailto: for the default mail
// app, tel: for the default calling app (Phone Link, Teams, Skype, whichever the person chose).
//
// Pure: a person in, lines and strings out. No clipboard, no shell, no drawing.

using System.Text;
using Karpo.Accounts;

namespace Karpo.Dock.Pim;

/// <summary>What a detail of a person is, which says what its line offers beside copying.</summary>
internal enum DetailKind
{
    Email,
    Phone,
    Organization,
}

/// <summary>One of a person's details, on a line of its own when their row is open.</summary>
/// <param name="Value">What it says, as the account holds it: what a copy puts on the clipboard.</param>
internal readonly record struct ContactDetail(DetailKind Kind, string Value)
{
    /// <summary>Segoe Fluent Icons: Mail, Phone, Work — the detail's kind in the leading column.</summary>
    public string Glyph => Kind switch
    {
        DetailKind.Email => ContactActs.MailGlyph,
        DetailKind.Phone => ContactActs.PhoneGlyph,
        _ => ContactActs.WorkGlyph,
    };

    /// <summary>The address Windows opens for it: mailto: for an email, tel: for a phone; null where it opens nothing.</summary>
    public string? Uri => Kind switch
    {
        DetailKind.Email => ContactActs.MailUri(Value),
        DetailKind.Phone => ContactActs.TelUri(Value),
        _ => null,
    };
}

internal static class ContactActs
{
    /// <summary>Segoe Fluent Icons: Mail, Phone, Work, Copy.</summary>
    internal const string MailGlyph = "";
    internal const string PhoneGlyph = "";
    internal const string WorkGlyph = "";
    internal const string CopyGlyph = "";

    /// <summary>
    /// Every detail of <paramref name="person"/> a line shows, in the order a contact card lists them:
    /// each address, each number, the organization. Blank ones and repeats are left out.
    /// </summary>
    public static IReadOnlyList<ContactDetail> Details(Person person)
    {
        var details = new List<ContactDetail>();
        void Add(DetailKind kind, string? value)
        {
            if (value?.Trim() is { Length: > 0 } kept && !details.Exists(one => one.Kind == kind && string.Equals(one.Value, kept, StringComparison.OrdinalIgnoreCase)))
            {
                details.Add(new ContactDetail(kind, kept));
            }
        }
        foreach (var email in person.Emails)
        {
            Add(DetailKind.Email, email);
        }
        foreach (var phone in person.Phones)
        {
            Add(DetailKind.Phone, phone);
        }
        Add(DetailKind.Organization, person.Organization);
        return details;
    }

    /// <summary>The first address Windows can write to, which the row's own Email button and the menu use; null for none.</summary>
    public static string? FirstEmail(Person person) =>
        Details(person).FirstOrDefault(one => one.Kind == DetailKind.Email && one.Uri is not null) is { Value: { } value } ? value : null;

    /// <summary>The first number Windows can call; null for none.</summary>
    public static string? FirstPhone(Person person) =>
        Details(person).FirstOrDefault(one => one.Kind == DetailKind.Phone && one.Uri is not null) is { Value: { } value } ? value : null;

    /// <summary>What Ctrl+C on a row copies: the first address, else the first number; null where there is neither.</summary>
    public static string? Primary(Person person) =>
        Details(person).FirstOrDefault(one => one.Kind != DetailKind.Organization) is { Value: { } value } ? value : null;

    /// <summary>
    /// The whole person as text, for "Copy contact details": the name, the organization, every address
    /// and every number, a line each — what pasted into a mail or a note reads as a contact.
    /// </summary>
    public static string AllText(Person person)
    {
        var lines = new List<string>();
        if (person.DisplayName.Trim() is { Length: > 0 } name)
        {
            lines.Add(name);
        }
        var details = Details(person);
        lines.AddRange(details.Where(one => one.Kind == DetailKind.Organization).Select(one => one.Value));
        lines.AddRange(details.Where(one => one.Kind != DetailKind.Organization).Select(one => one.Value));
        return string.Join("\r\n", lines);
    }

    /// <summary>
    /// mailto: for <paramref name="address"/> (RFC 6068): the address alone, every character that is
    /// not plain in a mailto percent-encoded as UTF-8 — a '?', '&amp;', '#' or '%' in an address can
    /// never start a subject or a body. Null where it is no address: empty, no '@' with something on
    /// both sides, or white space or a control character in it.
    /// </summary>
    public static string? MailUri(string? address)
    {
        var value = address?.Trim() ?? "";
        if (value.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
        {
            value = value["mailto:".Length..].Trim();
        }
        if (value.Length > 2 && value[0] == '<' && value[^1] == '>')
        {
            value = value[1..^1].Trim();
        }
        var at = value.LastIndexOf('@');
        if (at <= 0 || at == value.Length - 1 || value.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)))
        {
            return null;
        }
        var uri = new StringBuilder("mailto:", value.Length + 16);
        Span<byte> bytes = stackalloc byte[4];
        for (var index = 0; index < value.Length; index++)
        {
            var c = value[index];
            if (c < 0x80 && (char.IsAsciiLetterOrDigit(c) || "-._~!$'()*+,;:@".Contains(c)))
            {
                _ = uri.Append(c);
                continue;
            }
            // A character outside ASCII, as its UTF-8 bytes; a surrogate pair together.
            var length = char.IsHighSurrogate(c) && index + 1 < value.Length && char.IsLowSurrogate(value[index + 1]) ? 2 : 1;
            var written = Encoding.UTF8.GetBytes(value.AsSpan(index, length), bytes);
            for (var b = 0; b < written; b++)
            {
                _ = uri.Append('%').Append(bytes[b].ToString("X2", System.Globalization.CultureInfo.InvariantCulture));
            }
            index += length - 1;
        }
        return uri.ToString();
    }

    /// <summary>
    /// tel: for <paramref name="number"/> (RFC 3966) as a calling app takes it: the digits alone, a
    /// leading '+' kept, the spaces, dashes, dots, slashes and brackets people write left out, and an
    /// extension ("ext. 12", "x12", "#12") as <c>;ext=12</c>. Null where there are no digits to call.
    /// </summary>
    public static string? TelUri(string? number)
    {
        var value = number?.Trim() ?? "";
        if (value.StartsWith("tel:", StringComparison.OrdinalIgnoreCase))
        {
            value = value["tel:".Length..].Trim();
        }
        var (main, extension) = Split(value);
        var digits = new StringBuilder(main.Length);
        foreach (var c in main)
        {
            if (char.IsAsciiDigit(c))
            {
                _ = digits.Append(c);
            }
        }
        if (digits.Length == 0)
        {
            return null;
        }
        var plus = main.TrimStart().StartsWith('+');
        return "tel:" + (plus ? "+" : "") + digits + (extension.Length > 0 ? ";ext=" + extension : "");
    }

    /// <summary>A number and its extension's digits: after "ext", "x", "#" or ";ext=" at its end.</summary>
    private static (string Main, string Extension) Split(string value)
    {
        var end = value.Length;
        while (end > 0 && char.IsAsciiDigit(value[end - 1]))
        {
            end--;
        }
        if (end == value.Length || end == 0)
        {
            return (value, "");
        }
        var before = value[..end].TrimEnd().TrimEnd('.', ':', '=').TrimEnd();
        foreach (var mark in (string[])[";ext", "extension", "ext", "x", "#"])
        {
            if (before.EndsWith(mark, StringComparison.OrdinalIgnoreCase))
            {
                var main = before[..^mark.Length];
                // "x" is a mark only after the number, never a letter of a word ("Box12").
                if (mark == "x" && main.Length > 0 && char.IsAsciiLetter(main[^1]))
                {
                    break;
                }
                return (main, value[end..]);
            }
        }
        return (value, "");
    }
}
