// What a currency is called in the language Karpo speaks, for the markets widget's search (docs\widgets.md
// §246, §258): "Schweizer Franken" in German, "瑞士法郎" in Chinese, and "franken" finds the franc. Karpo's
// own strings would need some fifty names in thirty languages; Windows has them all already, in the
// ICU it ships as icu.dll (Windows 10 1903 and later), and ucurr_getName reads one. Where it cannot
// — no icu.dll, a code ICU does not know — the code alone is the name, which is still right.
//
// In the core: a markets widget's menu names them, and its line and its face say a pair's names.

using System.Runtime.InteropServices;

namespace Karpo.Core;

public static partial class CurrencyNames
{
    /// <summary>
    /// What <paramref name="code"/> is called in <paramref name="language"/> (a language of
    /// Karpo's, "de", "pt-BR", "zh-Hans"; the one Karpo speaks where none is given); the code itself
    /// where Windows does not know it.
    /// </summary>
    public static string Of(string code, string? language = null)
    {
        if (!Currencies.IsCode(code))
        {
            return code;
        }
        try
        {
            var error = 0;
            var name = ucurr_getName(code, (language ?? Strings.Language).Replace('-', '_'), LongName, out _, out var length, ref error);
            // ICU answers a code it does not know with the code and a warning (a negative error).
            if (error > 0 || name == 0 || length <= 0)
            {
                return code;
            }
            var said = Marshal.PtrToStringUni(name, length);
            return string.IsNullOrWhiteSpace(said) ? code : said;
        }
        catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException)
        {
            return code;
        }
    }

    /// <summary>What a pair is, in words: "Euro → Swiss Franc", for the line under the symbol box.</summary>
    public static string Pair(string from, string to, string? language = null) => $"{Of(from, language)} → {Of(to, language)}";

    const int LongName = 1;

    [LibraryImport("icu.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint ucurr_getName(
        string currency,
        [MarshalAs(UnmanagedType.LPStr)] string locale,
        int nameStyle,
        out byte isChoiceFormat,
        out int length,
        ref int errorCode);
}
