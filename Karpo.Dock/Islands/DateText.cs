// How the date is written on a bar (the calendar face's text, FaceDrawings.cs): its widest forms
// over a year, so the face keeps its width as the date rolls over, and the stacked pair down a
// vertical bar — the day's name over the date — at sizes that fit. It was the calendar entry's
// drawing (DateIsland) until the calendar became a preset folder wearing the calendar face
// (2026-10-09); what it measured, the face measures with.
//
// The day's name and the date stand on one line, in the order the region writes them ("Thu,
// 17.09.", or the date first where the region puts the name last: Panels\Nls.cs works that out from
// the long date's own pattern). Down a vertical bar, where a line has no room, the name sits over
// the date, and the date stands on as many rows as the bar's width asks (SideForms, §598): its day,
// month and year are never left out to make it fit.

using Karpo.Core;
using Karpo.Dock.Panels;

namespace Karpo.Dock;

internal static class DateText
{
    /// <summary>
    /// The forms a date takes down a side bar, fewest rows first (§598): the region's short date on
    /// one row ("28.12.2026"); without its year over the year ("28.12." over "2026", the year first
    /// where the region writes it first); each of its fields a row ("28" over "12" over "2026"); and,
    /// where the region names the month ("28. Dez. 2026"), the fields with the month's number. Without
    /// <paramref name="year"/> the date as the region writes it without one, then its fields. Every
    /// form says the day and the month, and the year where it is asked for: a narrower bar puts
    /// them on more rows and sets them smaller, never leaves one out.
    /// </summary>
    internal static string[][] SideForms(DateOnly day, bool year)
    {
        var rest = Nls.ShortDateWithoutYear(day);
        var forms = new List<string[]> { year ? [Nls.ShortDate(day)] : [rest] };
        var fields = Nls.ShortDateFields(day, year);
        if (year && Array.FindIndex(fields, field => field.Field == 'y') is var at and >= 0)
        {
            forms.Add(at == 0 ? [fields[at].Text, rest] : [rest, fields[at].Text]);
        }
        forms.Add([.. fields.Select(field => field.Text)]);
        if (Nls.ShortDateHasNames())
        {
            forms.Add([.. Nls.ShortDateFields(day, year, numbers: true).Select(field => field.Text)]);
        }
        return [.. forms];
    }

    /// <summary>
    /// How the date stands down a side bar (<see cref="VerticalFit.Rows(Gfx, in Fit, int, Func{int, IEnumerable{string[]}}, Weight, float)"/>):
    /// the first of <see cref="SideForms"/> whose rows fit at a size worth reading, no larger than
    /// <paramref name="most"/>. Measured on the 28th of every month — two-figure days, every month's
    /// name where the region writes one — never on today, so neither the form nor the size moves
    /// with the date.
    /// </summary>
    internal static VerticalFit.SideRows SideDate(Gfx gfx, in Fit fit, bool year, float most = float.MaxValue)
    {
        // Figures are measured as eights (TextLine.Template), so one two-figure date stands for all
        // where nothing in the date is a name.
        int[] months = Nls.ShortDateHasNames() ? [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12] : [12];
        var days = months.Select(month => SideForms(new DateOnly(2026, month, 28), year)).ToArray();
        return VerticalFit.Rows(gfx, fit, days[0].Length, form => days.Select(forms => forms[form]), Weight.Regular, most);
    }

    /// <summary>
    /// How the day's name stands over the date down a side bar: its short name ("Mon") where the
    /// widest of the week's fits at a size worth reading, else its shortest ("Mo"); form 1 is the
    /// shortest (<see cref="SideName"/>).
    /// </summary>
    internal static VerticalFit.SideRows SideNames(Gfx gfx, in Fit fit)
    {
        var week = Enumerable.Range(0, 7).Select(day => new DateOnly(2026, 12, 21).AddDays(day).DayOfWeek).ToArray();
        return VerticalFit.Rows(gfx, fit, 2, form => week.Select(day => new[] { SideName(day, form) }));
    }

    /// <summary>The day's name in the form <see cref="SideNames"/> chose.</summary>
    internal static string SideName(DayOfWeek day, int form) => form == 0 ? Nls.ShortDayName(day) : Nls.ShortestDayName(day);

    /// <summary>
    /// The widest the text can be across a bar: the longest of the week's seven day names, with a date
    /// whose day, month and (with <paramref name="year"/>) year all take their full room ("Wed,
    /// 28.12.2026", else "Wed, 28.12.").
    /// </summary>
    internal static float Widest(Gfx gfx, float px, bool year = true)
    {
        var widest = 0f;
        // A week of two-digit days in a two-digit month, so every name is formatted as the region
        // writes it and the numbers are as wide as they get.
        var week = new DateOnly(2026, 12, 21);
        for (var day = 0; day < 7; day++)
        {
            var date = week.AddDays(day);
            var text = Nls.DayAndShortDate(date, year);
            var (width, _) = TextLine.Template(gfx, text, px, Weight.Regular);
            widest = MathF.Max(widest, width);
        }
        return widest;
    }
}
