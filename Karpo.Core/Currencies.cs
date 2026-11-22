// The currencies of the markets widget (docs\widgets.md §246): which ones a rate is offered between,
// which pair a new rate starts at, the order the lists put them in, how finely an amount or a rate is
// written, and how an amount typed is read.
//
//  - **The pair where nobody chose one**: the euro in the region's currency, the dollar in euros in the
//    euro area, read from Windows' region (LOCALE_SINTLSYMBOL), not .NET's — the dock runs with
//    InvariantGlobalization, where .NET's region is nobody's.
//  - **How finely**: a rate by its size — none from ten thousand, two from fifty, four below, and under a
//    hundredth as many as its first three figures need (0.000637 francs to the won); an amount of money
//    in its currency's minor unit (ISO 4217: the yen none, the dinar three), and four places under one.
//    Every figure is written as the markets widget writes its prices, "1,234.50".
//  - **Amounts typed in the person's own marks**: "1 234,5" in France, "1’234.50" in Switzerland,
//    "1.234,5" in Germany. The region's decimal sign decides an amount that could be either ("1,500" is
//    fifteen hundred in the US and one and a half in Germany); a sign followed by anything but groups of
//    three is the decimal point whatever the region says. Nothing negative, nothing that is not a number.

using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace Karpo.Core;

/// <summary>The decimal sign and the grouping mark a region writes numbers with.</summary>
public readonly record struct NumberMarks(string Decimal, string Group)
{
    /// <summary>"1,234.5", as the markets widget writes its figures.</summary>
    public static NumberMarks Invariant { get; } = new(".", ",");

    /// <summary>The region Windows is set to now (LOCALE_SDECIMAL, LOCALE_STHOUSAND); <see cref="Invariant"/> where it says none.</summary>
    public static NumberMarks Region()
    {
        var marks = new NumberMarks(Currencies.Locale(LocaleDecimal) ?? ".", Currencies.Locale(LocaleGroup) ?? ",");
        return marks.Decimal.Length == 0 ? Invariant : marks;
    }

    private const uint LocaleDecimal = 0x0000000E;
    private const uint LocaleGroup = 0x0000000F;
}

/// <summary>The currencies a rate is offered between, the region's own, and how an amount in one is written and read.</summary>
public static unsafe partial class Currencies
{
    /// <summary>
    /// The currencies offered, by ISO 4217 code, in the order of their codes: every one the rate sources
    /// quote against the dollar (checked 2026-10-19). The Bulgarian lev is not among them: Bulgaria took
    /// the euro on 1 January 2026, and nobody quotes it since.
    /// </summary>
    public static readonly IReadOnlyList<string> Codes =
    [
        "AED", "ARS", "AUD", "BRL", "CAD", "CHF", "CLP", "CNY", "COP", "CZK", "DKK", "EGP", "EUR",
        "GBP", "HKD", "HUF", "IDR", "ILS", "INR", "ISK", "JPY", "KRW", "KWD", "MAD", "MXN", "MYR", "NGN",
        "NOK", "NZD", "PEN", "PHP", "PKR", "PLN", "QAR", "RON", "SAR", "SEK", "SGD", "THB", "TRY", "TWD",
        "UAH", "USD", "VND", "ZAR",
    ];

    /// <summary>The world's most traded, which the lists put first after the region's own.</summary>
    private static readonly string[] Common = ["USD", "EUR", "JPY", "GBP", "CHF", "CNY", "CAD", "AUD"];

    /// <summary>The most an amount may be: a trillion, past which a figure is a typo.</summary>
    public const double MostAmount = 1e12;

    /// <summary>The longest an amount typed may be, in characters.</summary>
    public const int Longest = 24;

    /// <summary>Whether <paramref name="code"/> is written as a currency code is: three letters A to Z.</summary>
    public static bool IsCode(string? code) => code is { Length: 3 } && code.All(char.IsAsciiLetterUpper);

    /// <summary><paramref name="code"/> as a currency code, "EUR" from " eur "; null for anything that is none.</summary>
    public static string? Code(string? code)
    {
        var upper = (code ?? "").Trim().ToUpperInvariant();
        return IsCode(upper) ? upper : null;
    }

    /// <summary>Whether <paramref name="code"/> is one Karpo offers (<see cref="Codes"/>).</summary>
    public static bool Offers(string? code) => code is not null && Codes.Contains(code, StringComparer.Ordinal);

    /// <summary>
    /// The pair a new rate starts at: the euro in the region's currency (<paramref name="region"/>, "CHF"
    /// in Switzerland) — the dollar in euros where that is the euro, the euro in dollars where the region
    /// has none Karpo can read.
    /// </summary>
    public static (string From, string To) Default(string? region)
    {
        var home = Code(region) ?? "USD";
        return home == "EUR" ? ("USD", "EUR") : ("EUR", home);
    }

    /// <summary>
    /// The codes Karpo offers, the common ones first: the region's own, then the world's most traded, then
    /// the rest in the order of their codes — the order a name matching several finds them in (MarketSearch).
    /// </summary>
    public static IReadOnlyList<string> Ordered(string? region)
    {
        var home = Code(region);
        List<string> first = home is not null && Offers(home) ? [home] : [];
        first.AddRange(Common.Where(code => code != home));
        return [.. first, .. Codes.Where(code => !first.Contains(code))];
    }

    /// <summary>The currency of the region Windows is set to (LOCALE_SINTLSYMBOL), "CHF"; empty where it says none.</summary>
    public static string Region() => Code(Locale(LocaleCurrency)) ?? "";

    /// <summary>An amount as a rate is said for it: 1 for none, nothing, a negative or a NaN; held to <see cref="MostAmount"/>.</summary>
    public static double Amount(double amount) =>
        !double.IsFinite(amount) || amount <= 0 ? 1 : Math.Min(Math.Round(amount, MarketHoldings.MostDecimals, MidpointRounding.AwayFromZero), MostAmount);

    // --- Decimals -------------------------------------------------------------------------------------

    // ISO 4217's minor units, where they are not two: of the codes offered, and the few others a region may have.
    private static readonly Dictionary<string, int> Minor = new(StringComparer.Ordinal)
    {
        ["BIF"] = 0, ["CLP"] = 0, ["DJF"] = 0, ["GNF"] = 0, ["ISK"] = 0, ["JPY"] = 0, ["KMF"] = 0, ["KRW"] = 0, ["PYG"] = 0,
        ["RWF"] = 0, ["UGX"] = 0, ["UYI"] = 0, ["VND"] = 0, ["VUV"] = 0, ["XAF"] = 0, ["XOF"] = 0, ["XPF"] = 0,
        ["BHD"] = 3, ["IQD"] = 3, ["JOD"] = 3, ["KWD"] = 3, ["LYD"] = 3, ["OMR"] = 3, ["TND"] = 3,
    };

    /// <summary>How many decimals <paramref name="code"/> is counted in: ISO 4217's minor unit, two where it does not say.</summary>
    public static int MinorUnits(string code) => Minor.GetValueOrDefault(code, 2);

    /// <summary>
    /// How many decimals a sum of money of <paramref name="value"/> in <paramref name="code"/> is written
    /// to: the currency's minor unit, and four under one, so a small amount still says something.
    /// </summary>
    public static int MoneyPlaces(string code, double value)
    {
        var minor = MinorUnits(code);
        return value != 0 && Math.Abs(value) < 1 ? Math.Max(minor, 4) : minor;
    }

    /// <summary>
    /// How many decimals a rate is written to, by its size: none from ten thousand (16,401 rupiah), two
    /// from fifty (158.52 yen), four below (0.9269 francs), and below a hundredth as many as its first
    /// three figures need (0.000637 francs to the won). A rate keeps its decimals as it moves; it would
    /// cross a step only in a currency's collapse.
    /// </summary>
    public static int RatePlaces(double rate)
    {
        var size = Math.Abs(rate);
        if (!double.IsFinite(size) || size == 0)
        {
            return 4;
        }
        return size >= 10_000 ? 0
            : size >= 50 ? 2
            : size >= 0.01 ? 4
            : Math.Min(8, (int)Math.Ceiling(-Math.Log10(size)) + 2);
    }

    /// <summary><paramref name="amount"/> in the other currency at <paramref name="rate"/> (one of the first in the second); the other way with <paramref name="back"/>.</summary>
    public static double Convert(double amount, double rate, bool back = false) =>
        back ? (rate == 0 ? 0 : amount / rate) : amount * rate;

    // --- Written --------------------------------------------------------------------------------------

    /// <summary><paramref name="value"/> to <paramref name="places"/> decimals, grouped by threes: "1,234.50".</summary>
    public static string Format(double value, int places) =>
        value.ToString("N" + Math.Clamp(places, 0, 12).ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

    /// <summary>A sum of <paramref name="code"/> as written: "94.12", "17,184", "0.0059".</summary>
    public static string Money(double value, string code) => Format(value, MoneyPlaces(code, value));

    /// <summary>A rate as written, to its <see cref="RatePlaces"/>: "0.9269", "158.52", "16,401"; "–" for none.</summary>
    public static string Rate(double? rate) =>
        rate is { } value && double.IsFinite(value) ? Format(value, RatePlaces(value)) : "–";

    /// <summary>
    /// An amount somebody gave, as they gave it: grouped, no decimals it does not have, up to eight it
    /// does — "100", "2.5", "1,234.5678".
    /// </summary>
    public static string Plain(double value)
    {
        var text = Format(Math.Round(value, 8), 8);
        var point = text.LastIndexOf('.');
        if (point < 0)
        {
            return text;
        }
        var end = text.Length;
        while (end > point + 1 && text[end - 1] == '0')
        {
            end--;
        }
        return end == point + 1 ? text[..point] : text[..end];
    }

    /// <summary>
    /// The same as a field shows it to be typed over: ungrouped, its decimal sign <paramref name="marks"/>' —
    /// "1250", "0.0125"; "0,125" in Germany — so it reads back as itself (§332: "0.125" read there was 125).
    /// </summary>
    public static string Typed(double value, NumberMarks marks)
    {
        var text = Plain(value).Replace(",", "", StringComparison.Ordinal);
        return marks.Decimal is { Length: 1 } point && point != "." ? text.Replace(".", point, StringComparison.Ordinal) : text;
    }

    /// <summary>One of the first in the second, "1 EUR = 0.9412 CHF".</summary>
    public static string RateText(string from, string to, double rate) => $"1 {from} = {Rate(rate)} {to}";

    /// <summary>What <paramref name="amount"/> is worth in <paramref name="to"/> at <paramref name="rate"/>, written; a dash without one.</summary>
    public static string Worth(double amount, string to, double? rate) =>
        rate is { } known && double.IsFinite(known) ? Money(Convert(amount, known), to) : "–";

    /// <summary>An amount and what it is worth, "100 EUR = 94.12 CHF"; "100 EUR = – CHF" without a rate.</summary>
    public static string Sentence(double amount, string from, string to, double? rate) =>
        $"{Plain(amount)} {from} = {Worth(amount, to, rate)} {to}";

    // --- Read -----------------------------------------------------------------------------------------

    /// <summary>
    /// An amount typed in <paramref name="marks"/> (see the top of the file); null for none — empty,
    /// negative, not a number, or more than <see cref="MostAmount"/>.
    /// </summary>
    public static double? Parse(string? text, NumberMarks marks)
    {
        var said = (text ?? "").Trim();
        if (said.Length == 0 || said.Length > Longest * 2)
        {
            return null;
        }
        var decimalMark = marks.Decimal.Length == 1 ? marks.Decimal[0] : '.';
        var groupMark = marks.Group.Length == 1 ? marks.Group[0] : ',';
        // The region's decimal point among the two marks: a sign that is neither is read as the point.
        var regionPoint = decimalMark == ',' ? ',' : '.';
        // The digits, and each '.' or ',' where it stands; spaces and apostrophes group, and go.
        var kept = new StringBuilder(said.Length);
        for (var index = 0; index < said.Length; index++)
        {
            var c = said[index];
            if (char.IsDigit(c))
            {
                kept.Append((char)('0' + (int)char.GetNumericValue(c)));
            }
            else if (c is '.' or ',')
            {
                kept.Append(c);
            }
            else if (c == decimalMark)
            {
                // A region's own decimal sign that is neither ("٫" in Arabic).
                kept.Append('.');
            }
            else if (char.IsWhiteSpace(c) || c is '\'' or '’' || c == groupMark)
            {
                continue;
            }
            else if (c == '+' && index == 0)
            {
                continue;
            }
            else
            {
                return null;
            }
        }
        var digits = kept.ToString();
        if (!digits.Any(char.IsAsciiDigit))
        {
            return null;
        }
        var dots = digits.Count(c => c == '.');
        var commas = digits.Count(c => c == ',');
        char? point = null;
        if (dots > 0 && commas > 0)
        {
            // Both: the last is the decimal point, once, and the other groups.
            var last = digits[digits.LastIndexOfAny(['.', ','])];
            if ((last == '.' ? dots : commas) != 1)
            {
                return null;
            }
            point = last;
            if (!Grouped(digits[..digits.IndexOf(last)], last == '.' ? ',' : '.'))
            {
                return null;
            }
        }
        else if (dots + commas > 0)
        {
            var mark = dots > 0 ? '.' : ',';
            var count = dots + commas;
            var grouped = Grouped(digits, mark);
            // The region's decimal sign once is its decimal point; another mark is a grouping where
            // it groups by threes, else a decimal point where it stands once.
            if (mark == regionPoint && count == 1)
            {
                point = mark;
            }
            else if (!grouped)
            {
                if (count != 1)
                {
                    return null;
                }
                point = mark;
            }
        }
        var plain = new StringBuilder(digits.Length);
        foreach (var c in digits)
        {
            if (char.IsAsciiDigit(c))
            {
                plain.Append(c);
            }
            else if (c == point)
            {
                plain.Append('.');
            }
        }
        if (!double.TryParse(plain.ToString(), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value)
            || !double.IsFinite(value) || value < 0 || value > MostAmount)
        {
            return null;
        }
        return value;

        // Whether every run after a mark is three digits and the run before it one to three.
        static bool Grouped(string text, char mark)
        {
            var runs = text.Split(mark);
            return runs.Length > 1 && runs[0].Length is >= 1 and <= 3 && runs[0].All(char.IsAsciiDigit)
                && runs.Skip(1).All(run => run.Length == 3 && run.All(char.IsAsciiDigit));
        }
    }

    // --- Windows' region ------------------------------------------------------------------------------

    private const uint LocaleCurrency = 0x00000015;

    /// <summary>What Windows' region format says for <paramref name="type"/> (GetLocaleInfoEx); null where it says nothing.</summary>
    internal static string? Locale(uint type)
    {
        try
        {
            var buffer = stackalloc char[16];
            var length = GetLocaleInfoEx(null, type, buffer, 16);
            return length > 1 ? new string(buffer, 0, length - 1) : null;
        }
        catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException)
        {
            return null;
        }
    }

    [DllImport("kernel32", ExactSpelling = true)]
    private static extern int GetLocaleInfoEx(char* lpLocaleName, uint LCType, char* lpLCData, int cchData);
}
