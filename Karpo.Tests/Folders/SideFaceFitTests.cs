// A face down a bar on its side fits its cell at every Size the bar can be set to (docs\widgets.md §598;
// the person, 2026-11-09: "the screen time face does not show nicely when the taskbar is shown on the
// side and not so wide. it should not overflow like it does right now"; "same with the datetime face.
// at some point the month and year does not show anymore which should not be").
//
// What is held, at every Size from the thinnest a side bar is to the widest, at 100 and 150 %, at 100 and
// 150 % text, in English, German and Russian and in the regions' own date formats:
// - every face draws inside its cell, as wide as the bar, and no line of it is cut ("13 h 4…");
// - the date — the time and date's, the calendar's — keeps its day, its month and its year (where the face
//   writes one): a narrow bar puts them on more rows and sets them smaller, never leaves one out;
// - the one rule every face of words follows there (VerticalFit.Rows): the form it takes has every row
//   inside the room a line has.
// The pure parts — how a reading breaks into rows, the date's forms in a region — need no Direct2D.

using Karpo.Core;
using Karpo.Dock;
using Karpo.Dock.Panels;
using Karpo.Tests.Islands;
using Karpo.Tests.Localization;
using static Karpo.Tests.Sizing.OneRowTests;

namespace Karpo.Tests.Folders;

[Collection(InLanguage.Collection)]
public sealed unsafe class SideFaceFitTests
{
    /// <summary>Every Size a side bar is set to, four DIP apart: from the thinnest the icons allow to the widest.</summary>
    private static readonly int[] Sizes = [.. Enumerable.Range(0, ((Config.MaxHeight - 32) / 4) + 1).Select(step => 32 + (4 * step))];

    private static readonly (uint Dpi, float Text)[] Screens = [(96, 1f), (144, 1f), (96, 1.5f), (144, 1.5f)];

    /// <summary>Late in December, two figures in the day and the month, a long day behind it.</summary>
    private static readonly DateTime Evening = new(2026, 12, 28, 22, 58, 59);

    private static IslandStyle Side(int size, uint dpi, float text)
    {
        var config = new Config { Height = size };
        return new IslandStyle(dpi / 96f, Vertical: true, Layout.CellPx(dpi, config), Layout.IconPx(dpi, config), LookFor(dark: true, text));
    }

    /// <summary>Paints <paramref name="island"/>'s cell: what of it is past its cell, cut, or not as wide as the bar.</summary>
    private static List<string> Check(FolderIsland island, IslandStyle style, string where)
    {
        var failures = new List<string>();
        island.Forget();
        var (width, height) = island.CellSize(0, style);
        if (MathF.Abs(width - style.Cell) > 0.5f)
        {
            failures.Add($"{where}: {width:0.#} px across a {style.Cell:0.#} px bar");
        }
        const float margin = 24f;
        (float Left, float Top, float Right, float Bottom) ink = default;
        int cut;
        PaintWatch.Watching = (_, _, color) => color;
        PaintWatch.Trimmed = 0;
        try
        {
            Offscreen.Bitmap(
                width + (2 * margin),
                height + (2 * margin),
                dc => Offscreen.Shifted(dc, margin, margin, () => island.PaintCell(dc, 0, style, width, height)),
                bitmap => ink = Offscreen.InkOf(bitmap, (uint)MathF.Ceiling(width + (2 * margin)), (uint)MathF.Ceiling(height + (2 * margin))));
            cut = PaintWatch.Trimmed;
        }
        finally
        {
            PaintWatch.Watching = null;
        }
        var (left, top, right, bottom) = (ink.Left - margin, ink.Top - margin, ink.Right - margin, ink.Bottom - margin);
        if (right > left && (left < -0.5f || top < -0.5f || right > width + 0.5f || bottom > height + 0.5f))
        {
            failures.Add($"{where}: draws {left:0.#},{top:0.#}-{right:0.#},{bottom:0.#} outside its {width:0.#} by {height:0.#} cell");
        }
        if (cut > 0)
        {
            failures.Add($"{where}: {cut} line(s) cut short in a {width:0.#} px cell");
        }
        return failures;
    }

    /// <summary>Runs <paramref name="body"/> at <see cref="Evening"/>, 13 h 42 min of screen time behind it.</summary>
    private static void AtEvening(Action body)
    {
        Clock.Now = () => Evening;
        var log = new ScreenTimeLog();
        log.Ensure(DateOnly.FromDateTime(Evening)).Total = TimeSpan.FromMinutes((13 * 60) + 42);
        ScreenTimeFaceDrawing.Stand = () => new ScreenTimeSnapshot(DateOnly.FromDateTime(Evening), log);
        try
        {
            body();
        }
        finally
        {
            Clock.Now = Clock.Real;
            ScreenTimeFaceDrawing.Stand = null;
        }
    }

    /// <summary>
    /// Every face there is, and the time and date, the calendar and the screen time in each of their
    /// forms, fit their cell at every Size, at 100 and 150 % and at 100 and 150 % text: inside it, as
    /// wide as the bar, nothing cut. The faces of words again in German and Russian, whose words are longer.
    /// </summary>
    [UiFact]
    public void EveryFaceFitsAtEverySideBarSize()
    {
        var failures = new List<string>();
        Offscreen.OnStaThread(gfx => AtEvening(() =>
        {
            foreach (var language in new[] { "en", "de", "ru" })
            {
                using var _ = InLanguage.Use(language);
                var faces = new List<(string Name, FolderIsland Island)>
                {
                    ("time and date", FolderFaceTests.Make(gfx, FolderFaceTests.Folder(FolderFaces.DateTimeId))),
                    ("time and date + s", FolderFaceTests.Make(gfx, FolderFaceTests.Folder(FolderFaces.DateTimeId, (DateTimeFace.Seconds, DateTimeFace.Shown)))),
                    ("calendar day + date", FolderFaceTests.Make(gfx, FolderFaceTests.Folder(FolderFaces.CalendarId, (CalendarFace.Form, CalendarFace.DayAndDate)))),
                };
                faces.AddRange(language == "en"
                    ? FaceDrawings.Ids.Select(id => (id, FolderFaceTests.Make(gfx, FolderFaceTests.Folder(id))))
                    : [(FolderFaces.ScreenTimeId, FolderFaceTests.Make(gfx, FolderFaceTests.Folder(FolderFaces.ScreenTimeId))),
                        (FolderFaces.CalendarId, FolderFaceTests.Make(gfx, FolderFaceTests.Folder(FolderFaces.CalendarId)))]);
                try
                {
                    foreach (var (name, island) in faces)
                    {
                        foreach (var (dpi, text) in Screens)
                        {
                            foreach (var size in Sizes)
                            {
                                failures.AddRange(Check(island, Side(size, dpi, text), $"{name} ({language}) at {size} DIP, {dpi} DPI, text ×{text}"));
                            }
                        }
                    }
                }
                finally
                {
                    foreach (var (_, island) in faces)
                    {
                        island.Dispose();
                    }
                }
            }
        }));
        Assert.True(failures.Count == 0, $"{failures.Count} faces do not fit a side bar:\n{string.Join("\n", failures.Take(60))}");
    }

    /// <summary>
    /// The date down a side bar keeps its day, its month and its year in every region — the region's own
    /// short date, and one written with the month's name, long in German, Russian and Finnish ("Dezember",
    /// "декабря", "joulukuuta") — at every Size, at 100 and 150 % and at 100 and 150 % text: every field is
    /// in its rows, every row inside the room a line has. The time and date's and the calendar's faces
    /// drawn with it, inside their cells, nothing cut.
    /// </summary>
    [UiFact]
    public void TheDateKeepsItsMonthAndYearAtEverySize()
    {
        var failures = new List<string>();
        Offscreen.OnStaThread(gfx => AtEvening(() =>
        {
            var today = DateOnly.FromDateTime(Evening);
            using var both = FolderFaceTests.Make(gfx, FolderFaceTests.Folder(FolderFaces.DateTimeId));
            using var calendar = FolderFaceTests.Make(gfx, FolderFaceTests.Folder(FolderFaces.CalendarId));
            foreach (var region in new[] { "en-US", "de-DE", "ru-RU", "fi-FI", "ja-JP" })
            {
                foreach (var pattern in new[] { "", "d. MMMM yyyy" })
                {
                    Nls.Region = region;
                    Nls.Formats = pattern.Length > 0 ? new LocalizationOptions { ShortDate = pattern } : null;
                    try
                    {
                        var named = Nls.ShortDateHasNames();
                        foreach (var (dpi, text) in Screens)
                        {
                            foreach (var size in Sizes)
                            {
                                var style = Side(size, dpi, text);
                                var fit = new Fit(style);
                                var room = VerticalFit.Room(fit);
                                var where = $"{region}{(pattern.Length > 0 ? $" \"{pattern}\"" : "")} at {size} DIP, {dpi} DPI, text ×{text}";
                                foreach (var year in new[] { true, false })
                                {
                                    var plan = DateText.SideDate(gfx, fit, year);
                                    var forms = DateText.SideForms(today, year);
                                    var rows = forms[plan.Form];
                                    var fields = Nls.ShortDateFields(today, year, numbers: named && plan.Form == forms.Length - 1);
                                    var said = string.Join(" ", rows);
                                    failures.AddRange(fields
                                        // A month alone is its name's nominative ("Декабрь"), in the date its genitive ("декабря").
                                        .Where(field => !said.Contains(field.Text.TrimEnd('ь'), StringComparison.OrdinalIgnoreCase))
                                        .Select(field => $"{where}: the {field.Field} \"{field.Text}\" is not in \"{said}\""));
                                    if (year && !fields.Any(field => field.Field == 'y'))
                                    {
                                        failures.Add($"{where}: no year in \"{said}\"");
                                    }
                                    failures.AddRange(rows
                                        .Where(row => TextLine.Template(gfx, row, plan.Px, Weight.Regular).Width > room)
                                        .Select(row => $"{where}: \"{row}\" is wider than the {room:0.#} px room at {plan.Px:0.#} px"));
                                }
                                failures.AddRange(Check(both, style, $"time and date, {where}"));
                                failures.AddRange(Check(calendar, style, $"calendar, {where}"));
                            }
                        }
                    }
                    finally
                    {
                        Nls.Region = null;
                        Nls.Formats = null;
                    }
                }
            }
        }));
        Assert.True(failures.Count == 0, $"{failures.Count} dates do not keep what they say:\n{string.Join("\n", failures.Take(60))}");
    }

    /// <summary>
    /// The person's own bar: about 63 px on a 150 % display, 42 DIP. The time and date there says the
    /// month and the year, and the screen time its hours and minutes, whole.
    /// </summary>
    [UiFact]
    public void ThePersonsBarShowsTheMonthYearAndTheWholeTotal()
    {
        Offscreen.OnStaThread(gfx => AtEvening(() =>
        {
            Nls.Region = "de-DE";
            try
            {
                var style = Side(42, 144, 1f);
                var plan = DateTimeFaceDrawing.Plan(gfx, style, FolderFace.Of(FolderFaces.DateTimeId));
                var rows = DateText.SideForms(DateOnly.FromDateTime(Evening), year: true)[plan.Date.Form];
                var said = string.Join(" ", rows);
                Assert.Contains("12", said, StringComparison.Ordinal);
                Assert.Contains("2026", said, StringComparison.Ordinal);
                using var screen = FolderFaceTests.Make(gfx, FolderFaceTests.Folder(FolderFaces.ScreenTimeId));
                using var both = FolderFaceTests.Make(gfx, FolderFaceTests.Folder(FolderFaces.DateTimeId));
                Assert.Empty(Check(screen, style, "screen time"));
                Assert.Empty(Check(both, style, "time and date"));
                var context = new FaceContext(gfx, style, screen.Options, screen);
                var total = ScreenTimeFaceDrawing.Stacked(context);
                Assert.True(VerticalFit.Phrase[total.Form](ScreenTimeFaceDrawing.Said(ScreenTimeFaceDrawing.Today())).All(row =>
                    TextLine.Template(gfx, row, total.Px, Weight.Regular).Width <= VerticalFit.Room(context.Fit)));
            }
            finally
            {
                Nls.Region = null;
            }
        }));
    }

    /// <summary>
    /// The one rule (VerticalFit.Rows) over what the faces of words read down a side bar — a day's total in
    /// three languages, a score, a time, a 12-hour time — at every Size, screen and text: the form taken has
    /// every row inside the room a line has, and a row is never larger than asked.
    /// </summary>
    [UiFact]
    public void TheRowsOfAReadingAlwaysFitTheRoom()
    {
        var failures = new List<string>();
        Offscreen.OnStaThread(gfx =>
        {
            foreach (var language in new[] { "en", "de", "ru" })
            {
                using var _ = InLanguage.Use(language);
                string[][] readings = [[S.WidgetScreenTimeHoursMinutes(hours: 88, minutes: 88), S.WidgetScreenTimeUnderMinute], ["88–88"], ["22∶58"], ["10:58 PM"]];
                foreach (var templates in readings)
                {
                    foreach (var (dpi, text) in Screens)
                    {
                        foreach (var size in Sizes)
                        {
                            var fit = new Fit(Side(size, dpi, text));
                            var room = VerticalFit.Room(fit);
                            var plan = VerticalFit.Rows(gfx, fit, VerticalFit.Phrase, templates);
                            failures.AddRange(templates
                                .SelectMany(VerticalFit.Phrase[plan.Form])
                                .Where(row => TextLine.Template(gfx, row, plan.Px, Weight.Regular).Width > room)
                                .Select(row => $"\"{row}\" ({language}) at {size} DIP, {dpi} DPI, text ×{text}: wider than {room:0.#} px at {plan.Px:0.#} px"));
                            var capped = VerticalFit.Rows(gfx, fit, VerticalFit.Phrase, templates, most: fit.Small);
                            if (capped.Px > fit.Small + 0.01f)
                            {
                                failures.Add($"{string.Join(", ", templates)} at {size} DIP: {capped.Px:0.#} px over the {fit.Small:0.#} px asked");
                            }
                        }
                    }
                }
            }
        });
        Assert.True(failures.Count == 0, $"{failures.Count} rows do not fit:\n{string.Join("\n", failures.Take(60))}");
    }

    /// <summary>A reading breaks before each figure that starts a new group, then at its words, then at its parts.</summary>
    [Theory]
    [InlineData("3 h 42 min", "3 h|42 min")]
    [InlineData("3 Std. 42 Min.", "3 Std.|42 Min.")]
    [InlineData("3 ч 42 мин", "3 ч|42 мин")]
    [InlineData("< 1 min", "< 1 min")]
    [InlineData("42 min", "42 min")]
    [InlineData("22∶58", "22∶58")]
    public void AReadingGroupsAtItsFigures(string text, string rows) =>
        Assert.Equal(rows.Split('|'), VerticalFit.Groups(text));

    [Theory]
    [InlineData("2–1", "2|1")]
    [InlineData("3小时42分钟", "3|小时|42|分钟")]
    public void AScoreAndAWordlessTotalBreakAtTheirParts(string text, string rows) =>
        Assert.Equal(rows.Split('|'), VerticalFit.Parts(text));

    /// <summary>
    /// The date's forms in a region, fewest rows first: the short date; without its year over the year (the
    /// year first where the region writes it first); a field a row; the month's number where it is named.
    /// </summary>
    [Theory]
    [InlineData("de-DE", "", "28.12.2026", "28.12|2026", "28|12|2026", null)]
    [InlineData("en-US", "", "12/28/2026", "12/28|2026", "12|28|2026", null)]
    [InlineData("ja-JP", "", "2026/12/28", "2026|12/28", "2026|12|28", null)]
    [InlineData("fi-FI", "", "28.12.2026", "28.12|2026", "28|12|2026", null)]
    [InlineData("de-DE", "d. MMMM yyyy", "28. Dezember 2026", "28. Dezember|2026", "28|Dezember|2026", "28|12|2026")]
    [InlineData("ru-RU", "d MMMM yyyy", "28 декабря 2026", "28 декабря|2026", "28|Декабрь|2026", "28|12|2026")]
    public void TheDatesFormsInARegion(string region, string pattern, string one, string two, string fields, string? numbers)
    {
        Nls.Region = region;
        Nls.Formats = pattern.Length > 0 ? new LocalizationOptions { ShortDate = pattern } : null;
        try
        {
            var forms = DateText.SideForms(new DateOnly(2026, 12, 28), year: true).Select(form => string.Join("|", form)).ToList();
            string[] expected = numbers is null ? [one, two, fields] : [one, two, fields, numbers];
            Assert.Equal(expected, forms);
            // Without its year: the date as the region writes it so, then its two fields.
            var yearless = DateText.SideForms(new DateOnly(2026, 12, 28), year: false);
            Assert.All(yearless, form => Assert.DoesNotContain(form, row => row.Contains("2026", StringComparison.Ordinal)));
            Assert.Equal(2, yearless[1].Length);
        }
        finally
        {
            Nls.Region = null;
            Nls.Formats = null;
        }
    }

    /// <summary>
    /// The ladder a side bar's rows step down: at Windows' text size to 9 DIP of it, and last to 9 DIP
    /// at 100 % text, so a large text size on a thin bar sets a row smaller rather than cut it.
    /// </summary>
    [Fact]
    public void TheLadderEndsAtNineDipOfPlainText()
    {
        var style = new IslandStyle(1.5f, Vertical: true, 38, 36, LookFor(dark: true, 1.5f));
        var ladder = VerticalFit.Ladder(new Fit(style));
        Assert.Equal(9 * 1.5f, ladder[^1], 2);
        Assert.Contains(9 * 1.5f * 1.5f, ladder);
        Assert.Equal(ladder.OrderDescending(), ladder);
    }
}
