// Which contacts a search shows, and in what order.
//
// Pure: a list in, a list out, no clock, no store, no drawing. The flyout calls
// it on every keystroke over the few hundred contacts already in memory, so it
// is a scan and nothing cleverer; if this ever runs over tens of thousands it
// should ask the accounts to filter instead.
//
// What someone types is matched against the name, the organization, the email
// addresses and the phone numbers. A phone is matched on its digits alone, so
// "079" finds "+41 79 123 45 67" — the separators people write are not the
// separators the contact was saved with.

using System.Text;
using Karpo.Accounts;

namespace Karpo.Dock.Pim;

internal static class ContactSearch
{
    /// <summary>
    /// How well a contact answers a search. The order matters: it is the order the rows come in,
    /// so a person whose name begins with what was typed stands above one whose email merely
    /// contains it.
    /// </summary>
    private enum Rank
    {
        /// <summary>The display name begins with it: "ad" finds "Adrian Hager".</summary>
        NameStarts,

        /// <summary>A later word of the name begins with it: "hag" finds "Adrian Hager".</summary>
        WordStarts,

        /// <summary>The name holds it somewhere: "dria" finds "Adrian Hager".</summary>
        NameHolds,

        /// <summary>An email address or the organization holds it.</summary>
        Detail,

        /// <summary>The digits of a phone number hold the digits typed.</summary>
        Phone,
    }

    /// <summary>
    /// The contacts that answer <paramref name="query"/>, best first and then by name. An empty
    /// query is every contact in the order they came, which is the store's sort by name.
    /// </summary>
    public static IReadOnlyList<Person> Match(
        IReadOnlyList<Person> contacts, string? query)
    {
        var wanted = Fold(query);
        if (wanted.Length == 0)
        {
            return contacts;
        }

        var digits = Digits(query);
        var found = new List<(Rank Rank, string Name, Person Contact)>();

        foreach (var contact in contacts)
        {
            if (RankOf(contact, wanted, digits) is { } rank)
            {
                found.Add((rank, Fold(contact.DisplayName), contact));
            }
        }

        found.Sort(static (left, right) =>
        {
            var byRank = left.Rank.CompareTo(right.Rank);
            return byRank != 0 ? byRank : string.CompareOrdinal(left.Name, right.Name);
        });

        return [.. found.Select(entry => entry.Contact)];
    }

    /// <summary>
    /// §652: how well <paramref name="contact"/> answers <paramref name="query"/> on Windows search's scale of a name
    /// (WinSearchRank.NameScore), by this search's own order: 90 the name starts so, 70 a later word of it does, 40 the name
    /// holds it, 35 an address or the organization does, 30 a number's digits; 0 for none.
    /// </summary>
    internal static int Score(Person contact, string? query)
    {
        var wanted = Fold(query);
        return wanted.Length == 0 ? 0 : RankOf(contact, wanted, Digits(query)) switch
        {
            Rank.NameStarts => 90,
            Rank.WordStarts => 70,
            Rank.NameHolds => 40,
            Rank.Detail => 35,
            Rank.Phone => 30,
            _ => 0,
        };
    }

    /// <summary>How well one contact answers, or null when it does not answer at all.</summary>
    private static Rank? RankOf(Person contact, string wanted, string digits)
    {
        var name = Fold(contact.DisplayName);
        if (name.StartsWith(wanted, StringComparison.Ordinal))
        {
            return Rank.NameStarts;
        }

        if (WordStarts(name, wanted))
        {
            return Rank.WordStarts;
        }

        if (name.Contains(wanted, StringComparison.Ordinal))
        {
            return Rank.NameHolds;
        }

        foreach (var email in contact.Emails)
        {
            if (Fold(email).Contains(wanted, StringComparison.Ordinal))
            {
                return Rank.Detail;
            }
        }

        if (contact.Organization is { } organization
            && Fold(organization).Contains(wanted, StringComparison.Ordinal))
        {
            return Rank.Detail;
        }

        // Only when something numeric was typed: otherwise every contact with a phone would
        // answer an empty digit string.
        if (digits.Length > 0)
        {
            foreach (var phone in contact.Phones)
            {
                if (Digits(phone).Contains(digits, StringComparison.Ordinal))
                {
                    return Rank.Phone;
                }
            }
        }

        return null;
    }

    /// <summary>Whether a word of <paramref name="name"/> after the first begins with <paramref name="wanted"/>.</summary>
    private static bool WordStarts(string name, string wanted)
    {
        var at = name.IndexOf(' ', StringComparison.Ordinal);
        while (at >= 0 && at + 1 < name.Length)
        {
            if (string.CompareOrdinal(name, at + 1, wanted, 0, wanted.Length) == 0)
            {
                return true;
            }
            at = name.IndexOf(' ', at + 1);
        }
        return false;
    }

    /// <summary>
    /// Lower case, trimmed, and with the accents taken off, so "Schlapfer" finds "Schläpfer" and
    /// the other way round. Ordinal comparisons everywhere after this, because the folding is what
    /// decides what counts as the same letter.
    /// </summary>
    /// <remarks>
    /// The letters are mapped one at a time rather than through
    /// <see cref="string.Normalize(NormalizationForm)"/>: Karpo builds with
    /// <c>InvariantGlobalization</c>, where normalizing is a no-op that leaves "ä" whole, so the
    /// folding would silently do nothing and only accented names would go missing. A table is
    /// also what makes "ß" into "ss", which no amount of decomposing would.
    /// </remarks>
    internal static string Fold(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "";
        }

        var lowered = text.Trim().ToLowerInvariant();
        var kept = new StringBuilder(lowered.Length);
        foreach (var letter in lowered)
        {
            switch (letter)
            {
                case 'ä' or 'à' or 'á' or 'â' or 'ã' or 'å' or 'ā' or 'ă' or 'ą':
                    kept.Append('a');
                    break;
                case 'ë' or 'è' or 'é' or 'ê' or 'ē' or 'ĕ' or 'ė' or 'ę' or 'ě':
                    kept.Append('e');
                    break;
                case 'ï' or 'ì' or 'í' or 'î' or 'ī' or 'į' or 'ı':
                    kept.Append('i');
                    break;
                case 'ö' or 'ò' or 'ó' or 'ô' or 'õ' or 'ø' or 'ō' or 'ŏ' or 'ő':
                    kept.Append('o');
                    break;
                case 'ü' or 'ù' or 'ú' or 'û' or 'ū' or 'ŭ' or 'ů' or 'ű' or 'ų':
                    kept.Append('u');
                    break;
                case 'ç' or 'ć' or 'ĉ' or 'ċ' or 'č':
                    kept.Append('c');
                    break;
                case 'ñ' or 'ń' or 'ņ' or 'ň':
                    kept.Append('n');
                    break;
                case 'ý' or 'ÿ':
                    kept.Append('y');
                    break;
                case 'š' or 'ś' or 'ş':
                    kept.Append('s');
                    break;
                case 'ž' or 'ź' or 'ż':
                    kept.Append('z');
                    break;
                case 'ł':
                    kept.Append('l');
                    break;
                case 'đ' or 'ď':
                    kept.Append('d');
                    break;
                case 'ť' or 'ţ':
                    kept.Append('t');
                    break;
                case 'ř' or 'ŕ':
                    kept.Append('r');
                    break;
                case 'ğ':
                    kept.Append('g');
                    break;
                // The two-letter ones, which are why this is a table and not a stripping of marks.
                case 'ß':
                    kept.Append("ss");
                    break;
                case 'æ':
                    kept.Append("ae");
                    break;
                case 'œ':
                    kept.Append("oe");
                    break;
                default:
                    kept.Append(letter);
                    break;
            }
        }
        return kept.ToString();
    }

    /// <summary>The digits of a phone number, or of what was typed: everything else is separators.</summary>
    internal static string Digits(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "";
        }

        var kept = new StringBuilder(text.Length);
        foreach (var letter in text)
        {
            if (char.IsAsciiDigit(letter))
            {
                kept.Append(letter);
            }
        }
        return kept.ToString();
    }
}
