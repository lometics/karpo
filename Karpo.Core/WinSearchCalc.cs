// §642: Windows search's answers, worked out on the PC as it is typed — where Windows 11's search sends a sum or a unit
// to Bing and shows what comes back, Karpo's never leaves the PC: arithmetic ("12*(3+4)", "2^10", "15% of 80", "sqrt 2",
// "sin 30" in degrees, as Windows' Calculator starts), and a quantity in another unit ("5 km in mi", "100 f to c",
// "3 GB in MB", "1.5 h in min"). The answer is the best match; Enter copies it.

using System.Globalization;

namespace Karpo.Core;

/// <summary>§642: an answer: what it says (<see cref="Text"/>), the value it copies (<see cref="Value"/>), and what it answered.</summary>
/// <param name="Text">"= 42", "3.107 mi".</param>
/// <param name="Value">What Enter copies: the figure alone, in the language's own figures ("42", "3.107").</param>
/// <param name="Question">What was asked, tidied ("12 × (3 + 4)", "5 km → mi").</param>
public sealed record WinSearchAnswer(string Text, string Value, string Question);

/// <summary>§642: the calculator and the units (see the top of the file).</summary>
public static class WinSearchCalc
{
    /// <summary>A Segoe Fluent Icons code point: Calculator's.</summary>
    public const string Glyph = "";

    /// <summary>The longest question worked out: a search box's, not a program's.</summary>
    private const int MostLength = 200;

    /// <summary>
    /// The answer to <paramref name="typed"/>, in <paramref name="marks"/>' figures (the region's decimal sign read and
    /// written too: the dock runs with InvariantGlobalization, so the region's marks are read from Windows, NumberMarks.Region), or null where it asks no sum and no unit: a bare number answers nothing, nor does a word.
    /// </summary>
    public static WinSearchAnswer? Answer(string? typed, NumberMarks marks)
    {
        var text = (typed ?? "").Trim();
        if (text.Length == 0 || text.Length > MostLength)
        {
            return null;
        }
        if (Units.Convert(text, marks) is { } converted)
        {
            return converted;
        }
        // "1+1=": the sign that asks for the answer is no part of the sum.
        var parser = new Parser(text.TrimEnd('=', ' '), marks);
        if (parser.Parse() is not { } value || !double.IsFinite(value) || !parser.Calculated)
        {
            return null;
        }
        var shown = Figure(value, marks);
        return new WinSearchAnswer("= " + shown, shown, text);
    }

    /// <summary>A value as the answer writes it: at most ten significant figures, no exponent below a trillion, in the region's marks.</summary>
    public static string Figure(double value, NumberMarks marks)
    {
        if (Math.Abs(value) < 1e-12)
        {
            value = 0;
        }
        var written = Math.Abs(value) is >= 1e15 or (> 0 and < 1e-6)
            ? value.ToString("G10", CultureInfo.InvariantCulture)
            : Math.Round(value, Math.Max(0, Math.Min(15, 9 - (int)Math.Floor(Math.Log10(Math.Max(Math.Abs(value), 1e-300)))))).ToString("#,0.##########", CultureInfo.InvariantCulture);
        if (written == "-0")
        {
            return "0";
        }
        // The invariant marks swapped for the region's, through a mark neither is.
        return written.Replace(",", "\u0001", StringComparison.Ordinal).Replace(".", marks.Decimal, StringComparison.Ordinal).Replace("\u0001", marks.Group, StringComparison.Ordinal);
    }

    /// <summary>Reads a number at <paramref name="at"/> of <paramref name="text"/>: digits, the region's decimal sign or a point, an exponent.</summary>
    internal static bool Number(string text, ref int at, NumberMarks marks, out double value)
    {
        var start = at;
        var separator = marks.Decimal;
        var digits = new System.Text.StringBuilder();
        var dot = false;
        while (at < text.Length)
        {
            var c = text[at];
            if (char.IsAsciiDigit(c))
            {
                digits.Append(c);
                at++;
            }
            else if (!dot && (c == '.' || (separator.Length == 1 && c == separator[0])) && at + 1 < text.Length && char.IsAsciiDigit(text[at + 1]))
            {
                digits.Append('.');
                dot = true;
                at++;
            }
            else
            {
                break;
            }
        }
        if (digits.Length == 0)
        {
            at = start;
            value = 0;
            return false;
        }
        // An exponent: "1e6", "2.5E-3".
        if (at + 1 < text.Length && (text[at] is 'e' or 'E') && (char.IsAsciiDigit(text[at + 1]) || (text[at + 1] is '+' or '-' && at + 2 < text.Length && char.IsAsciiDigit(text[at + 2]))))
        {
            digits.Append('e');
            at++;
            if (text[at] is '+' or '-')
            {
                digits.Append(text[at++]);
            }
            while (at < text.Length && char.IsAsciiDigit(text[at]))
            {
                digits.Append(text[at++]);
            }
        }
        return double.TryParse(digits.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>A recursive descent over a sum: + − × ÷ ^, %, parentheses, a few functions and π and e.</summary>
    private sealed class Parser(string text, NumberMarks marks)
    {
        private int at;

        /// <summary>Whether anything was worked out: an operator or a function, not a number alone.</summary>
        public bool Calculated { get; private set; }

        public double? Parse()
        {
            try
            {
                var value = Sum();
                Space();
                return at == text.Length ? value : null;
            }
            catch (FormatException)
            {
                return null;
            }
        }

        private void Space()
        {
            while (at < text.Length && char.IsWhiteSpace(text[at]))
            {
                at++;
            }
        }

        private bool Take(params string[] marks)
        {
            Space();
            foreach (var mark in marks)
            {
                if (string.CompareOrdinal(text, at, mark, 0, mark.Length) == 0)
                {
                    at += mark.Length;
                    return true;
                }
            }
            return false;
        }

        private double Sum()
        {
            var value = Product();
            while (true)
            {
                if (Take("+"))
                {
                    value += Product();
                }
                else if (Take("-", "−"))
                {
                    value -= Product();
                }
                else
                {
                    return value;
                }
                Calculated = true;
            }
        }

        private double Product()
        {
            var value = Unary();
            while (true)
            {
                if (Take("*", "×", "·") || TakeWord("of"))
                {
                    value *= Unary();
                }
                else if (Take("/", "÷", ":"))
                {
                    value /= Unary();
                }
                else if (TakeWord("mod"))
                {
                    var by = Unary();
                    value -= by * Math.Floor(value / by);
                }
                else if (StartsFactor())
                {
                    // "2pi", "3(4+5)", "2 sqrt 9": side by side is times.
                    value *= Unary();
                }
                else
                {
                    return value;
                }
                Calculated = true;
            }
        }

        private double Unary()
        {
            // A sign binds looser than a power: -3^2 is -9.
            if (Take("-", "−"))
            {
                return -Unary();
            }
            if (Take("+"))
            {
                return Unary();
            }
            return Power();
        }

        private double Power()
        {
            var value = Postfix();
            if (Take("^", "**"))
            {
                Calculated = true;
                // Right to left, a sign after it its own: 2^3^2 is 2^9, 2^-1 a half.
                return Math.Pow(value, Unary());
            }
            return value;
        }

        private double Postfix()
        {
            var value = Primary();
            while (true)
            {
                if (Take("%"))
                {
                    value /= 100;
                    Calculated = true;
                }
                else if (Take("!"))
                {
                    value = Factorial(value);
                    Calculated = true;
                }
                else
                {
                    return value;
                }
            }
        }

        private bool StartsFactor()
        {
            Space();
            return at < text.Length && (text[at] == '(' || text[at] == 'π' || (char.IsAsciiLetter(text[at]) && !Ahead("mod") && !Ahead("of")));
        }

        private bool Ahead(string word) =>
            string.Compare(text, at, word, 0, word.Length, StringComparison.OrdinalIgnoreCase) == 0
            && (at + word.Length >= text.Length || !char.IsAsciiLetter(text[at + word.Length]));

        private bool TakeWord(string word)
        {
            Space();
            if (!Ahead(word))
            {
                return false;
            }
            at += word.Length;
            return true;
        }

        private double Primary()
        {
            Space();
            if (at >= text.Length)
            {
                throw new FormatException();
            }
            if (Take("("))
            {
                var inner = Sum();
                if (!Take(")"))
                {
                    throw new FormatException();
                }
                return inner;
            }
            if (Number(text, ref at, marks, out var number))
            {
                return number;
            }
            if (Take("π"))
            {
                return Math.PI;
            }
            var start = at;
            while (at < text.Length && char.IsAsciiLetter(text[at]))
            {
                at++;
            }
            var word = text[start..at].ToLowerInvariant();
            switch (word)
            {
                case "pi":
                    return Math.PI;
                case "e":
                    return Math.E;
            }
            Func<double, double>? function = word switch
            {
                "sqrt" => Math.Sqrt,
                "cbrt" => Math.Cbrt,
                "abs" => Math.Abs,
                "ln" => Math.Log,
                "log" => Math.Log10,
                "exp" => Math.Exp,
                "round" => Math.Round,
                "floor" => Math.Floor,
                "ceil" => Math.Ceiling,
                // Degrees, as Windows' Calculator starts.
                "sin" => x => Math.Sin(x * Math.PI / 180),
                "cos" => x => Math.Cos(x * Math.PI / 180),
                "tan" => x => Math.Tan(x * Math.PI / 180),
                "asin" => x => Math.Asin(x) * 180 / Math.PI,
                "acos" => x => Math.Acos(x) * 180 / Math.PI,
                "atan" => x => Math.Atan(x) * 180 / Math.PI,
                _ => null,
            };
            if (function is null)
            {
                throw new FormatException();
            }
            Calculated = true;
            return function(Unary());
        }

        private static double Factorial(double value)
        {
            if (value < 0 || value > 170 || value != Math.Floor(value))
            {
                throw new FormatException();
            }
            var product = 1.0;
            for (var n = 2; n <= value; n++)
            {
                product *= n;
            }
            return product;
        }
    }

    /// <summary>A quantity in another unit: "5 km in mi", "100 °F to °C", "3 GB = MB".</summary>
    internal static class Units
    {
        /// <summary>A unit: its dimension, how many of the dimension's base unit one is, and an offset (temperatures).</summary>
        private readonly record struct Unit(string Dimension, double Factor, string Symbol, double Offset = 0);

        private static readonly Dictionary<string, Unit> Known = Build();

        private static Dictionary<string, Unit> Build()
        {
            var known = new Dictionary<string, Unit>(StringComparer.OrdinalIgnoreCase);
            void Add(string dimension, double factor, string symbol, params string[] names)
            {
                var unit = new Unit(dimension, factor, symbol);
                known[symbol] = unit;
                foreach (var name in names)
                {
                    known[name] = unit;
                }
            }
            // Length, in metres.
            Add("length", 0.001, "mm", "millimeter", "millimeters", "millimetre", "millimetres");
            Add("length", 0.01, "cm", "centimeter", "centimeters", "centimetre", "centimetres");
            Add("length", 1, "m", "meter", "meters", "metre", "metres");
            Add("length", 1000, "km", "kilometer", "kilometers", "kilometre", "kilometres");
            Add("length", 0.0254, "in", "inch", "inches", "\"");
            Add("length", 0.3048, "ft", "foot", "feet", "'");
            Add("length", 0.9144, "yd", "yard", "yards");
            Add("length", 1609.344, "mi", "mile", "miles");
            Add("length", 1852, "nmi", "nautical mile", "nautical miles");
            // Mass, in kilograms.
            Add("mass", 1e-6, "mg", "milligram", "milligrams");
            Add("mass", 0.001, "g", "gram", "grams");
            Add("mass", 1, "kg", "kilogram", "kilograms", "kilo", "kilos");
            Add("mass", 1000, "t", "tonne", "tonnes", "ton", "tons");
            Add("mass", 0.028349523125, "oz", "ounce", "ounces");
            Add("mass", 0.45359237, "lb", "lbs", "pound", "pounds");
            Add("mass", 6.35029318, "st", "stone", "stones");
            // Volume, in litres.
            Add("volume", 0.001, "ml", "milliliter", "milliliters", "millilitre", "millilitres");
            Add("volume", 0.01, "cl", "centiliter", "centilitre");
            Add("volume", 0.1, "dl", "deciliter", "decilitre");
            Add("volume", 1, "l", "liter", "liters", "litre", "litres");
            Add("volume", 3.785411784, "gal", "gallon", "gallons");
            Add("volume", 0.946352946, "qt", "quart", "quarts");
            Add("volume", 0.473176473, "pt", "pint", "pints");
            Add("volume", 0.2365882365, "cup", "cups");
            Add("volume", 0.0295735295625, "fl oz", "floz", "fluid ounce", "fluid ounces");
            // Area, in square metres.
            Add("area", 1, "m²", "m2", "sqm", "square meter", "square meters", "square metre", "square metres");
            Add("area", 1e6, "km²", "km2", "square kilometer", "square kilometers", "square kilometre", "square kilometres");
            Add("area", 1e4, "ha", "hectare", "hectares");
            Add("area", 4046.8564224, "ac", "acre", "acres");
            Add("area", 0.09290304, "ft²", "ft2", "sqft", "square foot", "square feet");
            // Data, in bytes: Windows' own (a kilobyte 1,024 bytes, as File Explorer counts).
            Add("data", 0.125, "bit", "bits");
            Add("data", 1, "B", "byte", "bytes");
            Add("data", 1024, "KB", "kib", "kilobyte", "kilobytes");
            Add("data", 1024d * 1024, "MB", "mib", "megabyte", "megabytes");
            Add("data", 1024d * 1024 * 1024, "GB", "gib", "gigabyte", "gigabytes");
            Add("data", 1024d * 1024 * 1024 * 1024, "TB", "tib", "terabyte", "terabytes");
            // Time, in seconds.
            Add("time", 0.001, "ms", "millisecond", "milliseconds");
            Add("time", 1, "s", "sec", "secs", "second", "seconds");
            Add("time", 60, "min", "mins", "minute", "minutes");
            Add("time", 3600, "h", "hr", "hrs", "hour", "hours");
            Add("time", 86400, "d", "day", "days");
            Add("time", 604800, "wk", "week", "weeks");
            // Speed, in metres a second.
            Add("speed", 1, "m/s", "mps");
            Add("speed", 1000.0 / 3600, "km/h", "kmh", "kph");
            Add("speed", 1609.344 / 3600, "mph");
            Add("speed", 1852.0 / 3600, "kn", "knot", "knots");
            // Temperature, in kelvin, each with its offset.
            known["°C"] = known["c"] = known["celsius"] = known["degc"] = new Unit("temperature", 1, "°C", 273.15);
            known["°F"] = known["f"] = known["fahrenheit"] = known["degf"] = new Unit("temperature", 5.0 / 9, "°F", 459.67 * 5 / 9);
            known["K"] = known["kelvin"] = new Unit("temperature", 1, "K", 0);
            return known;
        }

        private static readonly string[] Joins = [" in ", " to ", " into ", " as ", " = ", "=", "→", "->"];

        public static WinSearchAnswer? Convert(string text, NumberMarks marks)
        {
            foreach (var join in Joins)
            {
                var cut = text.IndexOf(join, StringComparison.OrdinalIgnoreCase);
                if (cut <= 0)
                {
                    continue;
                }
                var left = text[..cut].Trim();
                var right = text[(cut + join.Length)..].Trim();
                var at = 0;
                var negative = left.StartsWith('-');
                if (negative)
                {
                    at = 1;
                }
                if (!Number(left, ref at, marks, out var amount) || !Find(left[at..].Trim(), out var from) || !Find(right, out var to) || from.Dimension != to.Dimension)
                {
                    continue;
                }
                amount = negative ? -amount : amount;
                var value = ((((amount * from.Factor) + from.Offset) - to.Offset) / to.Factor);
                var figure = Figure(value, marks);
                var given = Figure(amount, marks);
                return new WinSearchAnswer($"{figure} {to.Symbol}", figure, $"{given} {from.Symbol} → {to.Symbol}");
            }
            return null;
        }

        private static bool Find(string name, out Unit unit)
        {
            var plain = name.Trim().TrimEnd('.');
            if (plain.StartsWith('°'))
            {
                plain = "°" + plain[1..].Trim().ToUpperInvariant();
            }
            return Known.TryGetValue(plain, out unit);
        }
    }
}
