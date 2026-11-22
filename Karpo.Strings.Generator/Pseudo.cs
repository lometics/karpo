// The pseudo-locale, qps-ploc: English made to look foreign and longer, from en.json alone.
//
// Nobody reads Karpo in it. It is for pictures and smoke runs: a word still in plain English is a
// string somebody wrote in the code rather than in en.json, a word cut off is a width measured
// from English, and the brackets show where a text was cut or run together with another.

using System.Collections.Generic;
using System.Text;

namespace Karpo.Strings.Generator;

internal static class Pseudo
{
    /// <summary>The code it goes by, as Windows names its own pseudo-locale.</summary>
    public const string Code = "qps-ploc";

    private const string Plain = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string Accented = "áƀçđéƒĝĥíĵķĺɱñóþǫŕšţúṽŵẋýžÁƁÇĐÉƑĜĤÍĴĶĹṀÑÓÞǪŔŠŢÚṼŴẊÝŽ";

    /// <summary>
    /// <paramref name="pieces"/> in pseudo: every letter accented, the places left as they are, the
    /// whole about 40 % longer and in brackets. Written back as a template, braces doubled.
    /// </summary>
    public static string Of(List<Piece> pieces)
    {
        var text = new StringBuilder("[");
        var length = 0;
        foreach (var piece in pieces)
        {
            if (piece.IsPlace)
            {
                text.Append('{').Append(piece.Name);
                if (piece.Type is not null)
                {
                    text.Append(':').Append(piece.Type);
                }
                text.Append('}');
                // A value is about as long as its place, braces and all, for the padding's sake.
                length += piece.Name!.Length + 2;
                continue;
            }
            foreach (var c in piece.Words!)
            {
                var at = Plain.IndexOf(c);
                text.Append(c switch
                {
                    '{' => "{{",
                    '}' => "}}",
                    _ when at >= 0 => Accented[at].ToString(),
                    _ => c.ToString(),
                });
                length++;
            }
        }
        // Longer by 40 %, counting the brackets: German runs about a third longer than English.
        var pad = System.Math.Max(1, ((length * 2) + 4) / 5 - 2);
        text.Append('·', pad).Append(']');
        return text.ToString();
    }
}
