// Screen time's bars by app (docs\widgets.md §69): what the total's graph draws from three rows
// where nobody chose the area. The person (2026-10-10): "screen time widget should show a bar chart
// with the different apps as well" — as Screen Time and Digital Wellbeing draw a week: a bar a day,
// each split by the apps it went to.
//
//   * Which apps: the period's top apps (MostApps), by their time over the days shown, each its own
//     colour; the rest of a day — smaller apps, and time counted for no app — is "Other", in a
//     neutral tone. A day from before the apps were counted (an older Karpo's "earlier" totals) has
//     no split at all: one neutral bar.
//   * Which colours: a palette made for charts, one for dark text and one for light, each colour
//     apart from the others in hue and readable on Windows' flyout grey, on acrylic and on a colour
//     of one's own (the palette goes by the text's lightness, which the theme keeps readable on it).
//     An app takes the palette's colour nearest its logo's (its icon's most common saturated hue),
//     where an app before it in the ranking has not taken that one already; a grey logo takes the
//     first free colour from its key's own place on the wheel. So an app keeps its colour on every
//     day of the chart, and from one look to the next while the top apps stay the same; and no two
//     apps of a chart share a colour. Logos read their colour themselves: an icon's pixels are on the
//     CPU already (Pixels), and 48 × 48 of them are counted in microseconds.
//   * High contrast has no colours to tell apart: every app is the text's colour, the shown day the
//     highlight's, and the segments are told apart by the gap between them (ScreenTimeLine).
//
// Pure: no window and no clock, so the ranking, the split and the colours are tested as they are.

using Karpo.Core;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock;

/// <summary>One day's bar: its total, each top app's part of it (by the chart's order), and the rest.</summary>
/// <param name="Split">Whether the day was counted by app at all; a day that was not is one neutral bar.</param>
internal sealed record ScreenTimeBar(DateOnly Day, TimeSpan Total, TimeSpan[] Parts, TimeSpan Other, bool Split);

/// <summary>The bars of a period, and the apps that have colours of their own, most used first.</summary>
internal sealed record ScreenTimeChart(IReadOnlyList<string> Top, IReadOnlyList<ScreenTimeBar> Bars)
{
    /// <summary>How many apps have colours of their own; every other is "Other".</summary>
    public const int MostApps = 4;

    /// <summary>
    /// The chart of <paramref name="days"/> in <paramref name="log"/>: the apps with the most time over
    /// them (at least a minute, as an app's row asks, <see cref="ScreenTimeDay.LeastShown"/>), equal
    /// times by key, up to <paramref name="most"/>; and each day split by them, the rest as other.
    /// </summary>
    public static ScreenTimeChart Of(ScreenTimeLog log, IReadOnlyList<DateOnly> days, int most = MostApps)
    {
        var sums = new Dictionary<string, TimeSpan>(StringComparer.OrdinalIgnoreCase);
        foreach (var date in days)
        {
            foreach (var (app, time) in log.Day(date)?.Apps ?? [])
            {
                sums[app] = sums.GetValueOrDefault(app) + time;
            }
        }
        string[] top = [.. sums.Where(pair => pair.Value >= ScreenTimeDay.LeastShown)
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Max(0, most))
            .Select(pair => pair.Key)];
        var bars = new List<ScreenTimeBar>(days.Count);
        foreach (var date in days)
        {
            var day = log.Day(date);
            var parts = new TimeSpan[top.Length];
            var named = TimeSpan.Zero;
            for (var index = 0; index < top.Length; index++)
            {
                parts[index] = day?.Apps.GetValueOrDefault(top[index]) ?? TimeSpan.Zero;
                named += parts[index];
            }
            // Apps' times add up to the total or less (time for no app is the difference); a total
            // that is less than its apps, which only a hand-edited file has, is their sum.
            var total = day is null ? TimeSpan.Zero : day.Total > named ? day.Total : named;
            bars.Add(new ScreenTimeBar(date, total, parts, total - named, day is { Apps.Count: > 0 }));
        }
        return new ScreenTimeChart(top, bars);
    }

    /// <summary>The hours the chart's top stands for: its longest day, up to a whole hour, one at least.</summary>
    public float FullHours => MathF.Max(1, MathF.Ceiling(Bars.Count == 0 ? 0 : Bars.Max(bar => (float)bar.Total.TotalHours)));
}

/// <summary>The colours of a chart's apps (see the note at the top).</summary>
internal static class ScreenTimeColours
{
    /// <summary>The palette's hues, in degrees, in the order of <see cref="Dark"/> and <see cref="Light"/>.</summary>
    internal static readonly float[] Hues = [212, 172, 140, 42, 22, 352, 322, 262];

    /// <summary>For light text, over a dark flyout: Tailwind's 400s, bright enough on #2C2C2C and on acrylic.</summary>
    private static readonly uint[] Dark = [0x60A5FA, 0x2DD4BF, 0x4ADE80, 0xFACC15, 0xFB923C, 0xF87171, 0xF472B6, 0xA78BFA];

    /// <summary>For dark text, over a light flyout: Tailwind's 600s, dark enough on #F9F9F9.</summary>
    private static readonly uint[] Light = [0x2563EB, 0x0D9488, 0x16A34A, 0xCA8A04, 0xEA580C, 0xDC2626, 0xDB2777, 0x7C3AED];

    /// <summary>How many colours the palette has.</summary>
    public static int Count => Hues.Length;

    /// <summary>
    /// Each app's place in the palette, in the order given (most used first): the free one nearest
    /// its logo's hue, or for a logo without one (null: grey, black, white, none), the first free
    /// one from its key's own place. Never two the same while there are colours left.
    /// </summary>
    public static int[] Slots(IReadOnlyList<string> apps, IReadOnlyList<float?> hues)
    {
        var taken = new bool[Count];
        var slots = new int[apps.Count];
        for (var index = 0; index < apps.Count; index++)
        {
            var hue = index < hues.Count ? hues[index] : null;
            var best = -1;
            if (hue is { } wanted)
            {
                var nearest = float.MaxValue;
                for (var slot = 0; slot < Count; slot++)
                {
                    var distance = Distance(wanted, Hues[slot]);
                    if (!taken[slot] && distance < nearest)
                    {
                        (best, nearest) = (slot, distance);
                    }
                }
            }
            else
            {
                var start = (int)(Hash(apps[index]) % (uint)Count);
                for (var step = 0; step < Count && best < 0; step++)
                {
                    var slot = (start + step) % Count;
                    best = taken[slot] ? -1 : slot;
                }
            }
            // Every colour taken (more apps than colours, which MostApps keeps from happening): the
            // nearest again, so an app still has one.
            slots[index] = best >= 0 ? best : index % Count;
            taken[slots[index]] = true;
        }
        return slots;
    }

    /// <summary>The palette's colour <paramref name="slot"/>, for a theme whose text is light (<paramref name="lightText"/>) or dark.</summary>
    public static DXGI_RGBA Colour(int slot, bool lightText)
    {
        var rgb = (lightText ? Dark : Light)[((slot % Count) + Count) % Count];
        return Gfx.Ink(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1);
    }

    /// <summary>Whether text in <paramref name="text"/> is the light kind, as on a dark flyout.</summary>
    public static bool LightText(DXGI_RGBA text) => (0.2126f * text.r) + (0.7152f * text.g) + (0.0722f * text.b) > 0.5f;

    /// <summary>
    /// The logo's colour as a hue in degrees: the most common one among its opaque, saturated and
    /// not too dark pixels (premultiplied BGRA, <see cref="Pixels"/>); null where too few are (a
    /// grey, black or white logo), or there is no logo.
    /// </summary>
    public static float? HueOf(Pixels? logo)
    {
        if (logo is null || logo.Width <= 0 || logo.Height <= 0 || logo.Data.Length < logo.Width * logo.Height * 4)
        {
            return null;
        }
        const int bins = 36;
        Span<float> weights = stackalloc float[bins];
        Span<float> sums = stackalloc float[bins];
        var opaque = 0;
        var coloured = 0f;
        var data = logo.Data;
        for (var at = 0; at + 3 < logo.Width * logo.Height * 4; at += 4)
        {
            var alpha = data[at + 3];
            if (alpha < 128)
            {
                continue;
            }
            opaque++;
            // Premultiplied: back to the colour itself.
            var b = data[at] / (float)alpha;
            var g = data[at + 1] / (float)alpha;
            var r = data[at + 2] / (float)alpha;
            var max = MathF.Max(r, MathF.Max(g, b));
            var min = MathF.Min(r, MathF.Min(g, b));
            var chroma = max - min;
            if (max < 0.2f || chroma < 0.18f)
            {
                continue;
            }
            float hue;
            if (max == r)
            {
                hue = 60 * (((g - b) / chroma) % 6);
            }
            else if (max == g)
            {
                hue = 60 * (((b - r) / chroma) + 2);
            }
            else
            {
                hue = 60 * (((r - g) / chroma) + 4);
            }
            hue = (hue + 360) % 360;
            var bin = Math.Min(bins - 1, (int)(hue / (360f / bins)));
            weights[bin] += chroma;
            sums[bin] += hue * chroma;
            coloured += chroma;
        }
        // A tenth of the logo, at full colour, before it is said to have one.
        if (opaque == 0 || coloured < opaque * 0.1f)
        {
            return null;
        }
        var top = 0;
        for (var bin = 1; bin < bins; bin++)
        {
            if (weights[bin] > weights[top])
            {
                top = bin;
            }
        }
        return sums[top] / weights[top];
    }

    /// <summary>How far apart two hues are on the wheel, 0 to 180.</summary>
    internal static float Distance(float one, float two)
    {
        var apart = MathF.Abs(one - two) % 360;
        return apart > 180 ? 360 - apart : apart;
    }

    /// <summary>FNV-1a of the key, lower-cased: the same on every run, as string.GetHashCode is not.</summary>
    internal static uint Hash(string key)
    {
        var hash = 2166136261u;
        foreach (var letter in key)
        {
            hash = (hash ^ char.ToLowerInvariant(letter)) * 16777619u;
        }
        return hash;
    }
}
