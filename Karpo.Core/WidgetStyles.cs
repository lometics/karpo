// One widget, several styles (2026-10-17, docs\widgets.md §209). The person: "it should not be
// different widgets anymore, but one type of widget, calendar in this case, with different styles,
// agenda, day view, week view, etc... change this for other widgets where it applies too."
//
// So a calendar is one widget whose style is its agenda, a day's events, a day, a week, a month or a
// year; the hourly forecast one whose style is today's curve or the next hours in a row, the daily one whose style is
// the days in a list or on a graph (§590);
// the volume one whose style is the one volume or the mixer, each app's; the performance one whose
// reading is what it measures and whose style is how it shows it (below); the clock one whose style
// is the clock, the date or the time and date. The add list offers each once (the performance one
// once a reading, §283: StyledWidget.Offered), and its page in
// Settings and its menu on the bar choose the style, which keeps everything else the widget has: its
// id, where it stands, its height and its options — an option only one style reads is
// kept, and ignored by the others.
//
// **A style shows the same thing** (2026-10-20, §270). The person: "a style should not be input or
// output device, these dont show the same type of data ... only same data shown (or similar data,
// sometimes more verbose or less verbose) should be combined in the style property." So the styles of
// a widget are its one subject drawn more or less fully — a calendar's events as a list or a grid, the
// weather of one place for today or ahead, how loud it plays as one level or app by app, now in a
// zone as a clock or a date — and what it is *of* is never a style: a performance widget's reading
// is a choice of its own (above), and the output devices and the microphones are widgets of their own.
//
// **What a style is in code.** Each style was a widget of its own and keeps its record in ListWidgets
// (its form): how tall it starts, what draws it, what a click does, the channels it follows. A placed
// widget's Kind is that form's word in memory, so everything that draws, measures and acts on a line
// goes on asking the form, unchanged, and a style changed is a form changed, which the folder remakes
// as it remakes any line whose record changed. What changed is the file and what the person sees: the
// file writes the widget's word and its style ("kind": "calendar", "style": "week"; the first style is the word alone) — CellItem's
// FileKind and FileStyle — and the add list, the page and the menu speak of the widget and its style.
//
// **A reading and its style** (2026-10-19, §228). The person: "right now the styles are the different
// part of the system like the gpu or the cpu, but the styles should really just be the different
// styles that the cpu widget can be displayed." So the performance widget has two choices that do
// not touch each other: its reading — the processor, the graphics, the memory, the disk or the
// network, what it measures — and its style — its graph or its figure alone (its details too until
// §318, a widget of their own now, below), how it shows it. A form is a reading in a style
// (ListKind.Gpu is the graphics' graph), so each style of such a widget names its reading
// (WidgetStyle.Reading), and the file writes both ("kind": "performance", "reading": "gpu", "style":
// "graph"). The figure alone is
// the graph's form drawing no graph (PerformanceOptions.ShowGraph, the "Show a graph" switch until
// §228): no form of its own, an entry of the style choice (Looks, below).
//
// **The words.** A widget's word with no style, or a style this build does not know, is its first
// style: "calendar" alone is the agenda, and "clock" the clock.
//
// **Different data, different widgets** (2026-10-23, §318). The person: "the volume slider and volume
// mixer are (to the user at least) the same widget just with different styles. they show something
// different / have different behaviour. widgets like these should therefore not be grouped together".
// So the sound widget is gone: the volume ("soundVolume") and the mixer ("soundMixer") are widgets of
// one style each. Today's date ("dateHeader"), whose click sets the folder's day, is no clock's style.
// And a reading's details are the performance details widget ("performanceDetails"), a reading each as
// the performance widget is, but nothing else of how it shows. A widget whose line has no style to choose (Chooses) is its form to the person:
// no Style row, no "style" in the file, and its add list's rows the forms' names and icons.

namespace Karpo.Core;

/// <summary>One style of a widget: its word in the file, and the form that draws it (<see cref="ListWidgets"/>).</summary>
public sealed record WidgetStyle
{
    /// <summary>What config.json's "style" says ("week"); never changes once released.</summary>
    public required string Word { get; init; }

    /// <summary>The list widget that is this style: its record, its drawing, its heights.</summary>
    public required ListKind Form { get; init; }

    /// <summary>The widget this is a style of; set by <see cref="StyledWidget"/>.</summary>
    public string Of { get; init; } = "";

    /// <summary>
    /// What the form measures, for a widget whose forms each read one thing ("gpu" of the performance
    /// widget, <see cref="StyledWidget.Readings"/>); null for any other. config.json's "reading".
    /// </summary>
    public string? Reading { get; init; }

    /// <summary>§249: its icon in the style choice (OptionGlyphs): "Week" a week's calendar.</summary>
    public string Glyph { get; init; } = "";

    /// <summary>§249: for a widget with readings, the icon of what it measures: the processor's chip.</summary>
    public string ReadingGlyph { get; init; } = "";

    /// <summary>What the style is called in the add list's choice, the page's and the menu's: "Week".</summary>
    public string Title => Strings.Text($"core.widgetStyle.{Of}.{Word}");
}

/// <summary>A widget of several styles: its word, its words for people, and its styles, the first where a new one starts.</summary>
public sealed record StyledWidget
{
    /// <summary>The word config.json stores as the widget's "kind" ("calendar").</summary>
    public required string Id { get; init; }

    /// <summary>A Segoe Fluent Icons code point standing for the widget as a whole.</summary>
    public required string Glyph { get; init; }

    /// <summary>
    /// Its styles, in the order offered; the first is a new widget's and a file's that names none. A
    /// widget with readings has each style once per reading, a form each (<see cref="WidgetStyle.Reading"/>).
    /// </summary>
    public required IReadOnlyList<WidgetStyle> Styles { get; init; }

    /// <summary>
    /// §318: whose names of the readings it speaks (<c>core.widgetReading.&lt;words&gt;.&lt;reading&gt;</c>):
    /// its own, or the performance widget's for the performance details, which measure the same.
    /// </summary>
    public string ReadingWords
    {
        get => readingWords ?? Id;
        init => readingWords = value;
    }

    private readonly string? readingWords;

    /// <summary>What the add list, the page and the menu call it: "Calendar".</summary>
    public string Title => Strings.Text($"core.styled.{Id}.title");

    /// <summary>One line saying what it is, under its name in the add list.</summary>
    public string Blurb => Strings.Text($"core.styled.{Id}.blurb");

    /// <summary>Its English name and line, where Karpo speaks another language; nothing in English.</summary>
    public IEnumerable<string> EnglishWords => Strings.AlsoEnglish($"core.styled.{Id}.title", $"core.styled.{Id}.blurb");

    /// <summary>
    /// §283: what the add list offers of it, a row each: its first style, and for a widget with
    /// readings each reading's first. What a widget measures is what it is of, never a style (§270),
    /// so somebody adding one says which: "Memory" adds the performance widget measuring the memory,
    /// its reading still chosen on it afterwards.
    /// </summary>
    public IReadOnlyList<WidgetStyle> Offered => Readings.Count == 0 ? [Default] : [.. Readings.Select(reading => StyleOf(null, reading))];

    /// <summary>
    /// What the add list calls the row of <paramref name="offered"/>: the widget's name, or the
    /// reading's ("Graphics"); where nothing is left to choose but the reading, its form's
    /// ("Graphics details", <see cref="WidgetStyles.Chooses"/>).
    /// </summary>
    public string TitleOf(WidgetStyle offered) =>
        offered.Reading is not { } reading ? Title : WidgetStyles.Chooses(this, offered) ? ReadingTitle(reading) : ListWidgets.Of(offered.Form).Info.Title;

    /// <summary>The icon of the row of <paramref name="offered"/>: the widget's, or the reading's, as the reading choice shows it, or its form's (as <see cref="TitleOf"/>).</summary>
    public string GlyphOf(WidgetStyle offered) =>
        offered.Reading is null ? Glyph : WidgetStyles.Chooses(this, offered) ? offered.ReadingGlyph : ListWidgets.Of(offered.Form).Info.Glyph;

    /// <summary>The line under the row of <paramref name="offered"/>: the widget's, or what its form says it shows.</summary>
    public string BlurbOf(WidgetStyle offered) => offered.Reading is not null ? ListWidgets.Of(offered.Form).Info.Blurb : Blurb;

    /// <summary>
    /// The words the row of <paramref name="offered"/> is found by besides its name and its line: the
    /// widget's own and its name, then the name and words of each style it may be shown in — a
    /// reading's row its reading's styles alone, so "ram" finds Memory and not Processor.
    /// </summary>
    public string[] KeywordsOf(WidgetStyle offered) =>
        [.. Strings.List($"core.styled.{Id}.keywords"), .. offered.Reading is null ? Readings.Select(ReadingTitle) : [Title],
            .. StylesBeside(offered).SelectMany(style => (string[])[style.Title, ListWidgets.Of(style.Form).Info.Title, .. ListWidgets.Of(style.Form).Info.Keywords])
            .Distinct(StringComparer.CurrentCultureIgnoreCase)];

    /// <summary>The English words of the row of <paramref name="offered"/>, where Karpo speaks another language; nothing in English.</summary>
    public IEnumerable<string> EnglishWordsOf(WidgetStyle offered) => offered.Reading is { } reading
        ? [.. Strings.AlsoEnglish($"core.widgetReading.{ReadingWords}.{reading}", $"core.styled.{Id}.title"), .. ListWidgets.Of(offered.Form).Info.EnglishWords]
        : EnglishWords;

    /// <summary>The style a new one starts in.</summary>
    public WidgetStyle Default => Styles[0];

    /// <summary>
    /// What it may measure, in the order offered ("cpu", "gpu" … of the performance widget); empty for
    /// a widget whose styles measure nothing of their own.
    /// </summary>
    public IReadOnlyList<string> Readings => [.. Styles.Select(style => style.Reading).OfType<string>().Distinct(StringComparer.Ordinal)];

    /// <summary>What a reading is called in the page's choice and the menu's: "Graphics".</summary>
    public string ReadingTitle(string reading) => Strings.Text($"core.widgetReading.{ReadingWords}.{reading}");

    /// <summary>
    /// The style whose word is <paramref name="word"/>, of the reading <paramref name="reading"/> for a
    /// widget with readings: a word this build does not know is that reading's first style, and a
    /// reading it does not know (or none) the widget's first reading.
    /// </summary>
    public WidgetStyle StyleOf(string? word, string? reading = null)
    {
        var of = reading is not null && Readings.Contains(reading, StringComparer.Ordinal) ? reading : Default.Reading;
        var styles = Styles.Where(style => style.Reading == of).ToList();
        return styles.FirstOrDefault(style => string.Equals(style.Word, word, StringComparison.Ordinal)) ?? styles.FirstOrDefault() ?? Default;
    }

    /// <summary>The styles of <paramref name="now"/>'s reading, in the order offered: what a line in <paramref name="now"/> may be shown as.</summary>
    public IReadOnlyList<WidgetStyle> StylesBeside(WidgetStyle now) => [.. Styles.Where(style => style.Reading == now.Reading)];
}

/// <summary>Every widget of several styles (see the top of the file).</summary>
public static class WidgetStyles
{
    /// <summary>
    /// calendar: the agenda, the day's events, the day's hours, the week's days, the month and the
    /// year. The agenda first: "calendar" alone was the agenda before there were styles.
    /// </summary>
    public static readonly StyledWidget Calendar = Widget("calendar", WidgetInfos.Calendar.Glyph,
        ("agenda", ListKind.Calendar, OptionGlyphs.Agenda), ("events", ListKind.Events, OptionGlyphs.Events), ("day", ListKind.DayView, OptionGlyphs.Day),
        ("week", ListKind.WeekView, OptionGlyphs.Week), ("month", ListKind.Month, OptionGlyphs.Month), ("year", ListKind.YearView, OptionGlyphs.Year));

    /// <summary>
    /// weather, §590: one place's hours, as today's curve with the hours' glyphs (the Graph, first: the hourly view the
    /// weather always had) or the next five hours across, a column each (the List). The current weather is a widget of
    /// its own, of no style: the conditions now are other data.
    /// </summary>
    public static readonly StyledWidget Hourly = Widget("hourlyForecast", WidgetInfos.WeatherHours.Glyph,
        ("graph", ListKind.WeatherHours, OptionGlyphs.Graph), ("list", ListKind.WeatherHoursList, OptionGlyphs.Time));

    /// <summary>
    /// weather, §590: one place's next seven days, a row a day with its range on the week's scale (the List, first: the
    /// days the weather always listed), or a column a day over two curves, the highs' and the lows' (the Graph).
    /// </summary>
    public static readonly StyledWidget Daily = Widget("dailyForecast", WidgetInfos.WeatherDays.Glyph,
        ("list", ListKind.WeatherDays, OptionGlyphs.Month), ("graph", ListKind.WeatherDaysGraph, OptionGlyphs.Graph));

    /// <summary>
    /// perf: what it measures — the processor, the graphics, the memory, the disk or the network — and
    /// how it shows it: its graph, or the figure alone, the graph drawing none (Looks; §228). The
    /// processor's graph first: what "performance" alone was. Its details are a widget of their own
    /// since §318 (<see cref="PerformanceDetails"/>): the facts behind the figure are other data.
    /// </summary>
    public static readonly StyledWidget Performance = Readings("performance", OptionGlyphs.Reading, GraphWord, OptionGlyphs.Graph,
        ("cpu", ListKind.Cpu, OptionGlyphs.Processor), ("gpu", ListKind.Gpu, OptionGlyphs.Graphics), ("memory", ListKind.Memory, OptionGlyphs.Memory),
        ("disk", ListKind.Disk, OptionGlyphs.Disk), ("network", ListKind.Network, OptionGlyphs.Network));

    /// <summary>
    /// perf: the facts behind a reading's figure — the processor's speed, processes and up time, the
    /// memory in use and cached, what a disk reads and writes, what the network sends — a reading
    /// each, named as the performance widget names them, in one style (§318).
    /// </summary>
    public static readonly StyledWidget PerformanceDetails = Readings("performanceDetails", OptionGlyphs.Details, DetailsWord, OptionGlyphs.Details,
        ("cpu", ListKind.CpuDetails, OptionGlyphs.Processor), ("gpu", ListKind.GpuDetails, OptionGlyphs.Graphics), ("memory", ListKind.MemoryDetails, OptionGlyphs.Memory),
        ("disk", ListKind.DiskDetails, OptionGlyphs.Disk), ("network", ListKind.NetworkDetails, OptionGlyphs.Network)) with { ReadingWords = "performance" };

    /// <summary>perf: the performance widget's graph, its first style.</summary>
    public const string GraphWord = "graph";

    /// <summary>perf: the performance details' one style.</summary>
    public const string DetailsWord = "details";

    /// <summary>perf: the figure alone — the graph's form drawing no graph (<see cref="PerformanceOptions.ShowGraph"/>), an entry of the style choice and no form.</summary>
    public const string FigureWord = "figure";

    /// <summary>
    /// clock: the clock, and the time with its seconds over the date. The clock first: "clock" alone
    /// is the clock. Today's date is a widget of its own (§318): a click on it sets the folder's day. The clock's
    /// form comes in looks, entries of its style choice (§443, <see cref="ClockStyles"/>, <see cref="LooksOf"/>).
    /// </summary>
    public static readonly StyledWidget Clock = Widget("clock", WidgetInfos.Clock.Glyph,
        ("clock", ListKind.Clock, OptionGlyphs.Clock), ("time", ListKind.TimeHeader, OptionGlyphs.DateAndTime));

    /// <summary>
    /// sun (§368): one place's sun, as Themia's Sun has it — its path across the sky (the arc), the
    /// day as a bar with its daylight lit. The arc first: "sun" alone is the arc. Its times are widgets of their own (§404).
    /// </summary>
    public static readonly StyledWidget Sun = Widget("sun", OptionGlyphs.Sunrise,
        ("arc", ListKind.SunArc, OptionGlyphs.Sunrise), ("dayBar", ListKind.SunDayBar, OptionGlyphs.Ruler));

    /// <summary>Every widget of several styles, in the order the add list's search finds them.</summary>
    public static readonly StyledWidget[] All = [Calendar, Hourly, Daily, Performance, PerformanceDetails, Clock, Sun];

    /// <summary>What config.json's "style" is called: the key a widget's style is written under.</summary>
    public const string StyleKey = "style";

    /// <summary>What config.json's "reading" is called: the key a widget's reading is written under.</summary>
    public const string ReadingKey = "reading";

    private static readonly Dictionary<ListKind, (StyledWidget Widget, WidgetStyle Style)> byForm =
        All.SelectMany(widget => widget.Styles.Select(style => (style.Form, (widget, style)))).ToDictionary(pair => pair.Form, pair => pair.Item2);

    private static StyledWidget Widget(string id, string glyph, params (string Word, ListKind Form, string Glyph)[] styles) => new()
    {
        Id = id,
        Glyph = glyph,
        Styles = [.. styles.Select(style => new WidgetStyle { Word = style.Word, Form = style.Form, Of = id, Glyph = style.Glyph })],
    };

    /// <summary>A widget of readings, each one form in the style <paramref name="word"/>, reading by reading.</summary>
    private static StyledWidget Readings(string id, string glyph, string word, string styleGlyph, params (string Reading, ListKind Form, string Glyph)[] readings) => new()
    {
        Id = id,
        Glyph = glyph,
        Styles = [.. readings.Select(reading =>
            new WidgetStyle { Word = word, Form = reading.Form, Of = id, Reading = reading.Reading, Glyph = styleGlyph, ReadingGlyph = reading.Glyph })],
    };

    /// <summary>The widget whose word is <paramref name="id"/> ("calendar"), or null.</summary>
    public static StyledWidget? Find(string? id) => All.FirstOrDefault(widget => string.Equals(widget.Id, id, StringComparison.Ordinal));

    /// <summary>The widget and the style <paramref name="form"/> is, or null for a list widget of one style.</summary>
    public static (StyledWidget Widget, WidgetStyle Style)? Of(ListKind form) =>
        byForm.TryGetValue(form, out var found) ? found : null;

    /// <summary>The widget and the style the form whose word is <paramref name="word"/> is ("weekView"), or null.</summary>
    public static (StyledWidget Widget, WidgetStyle Style)? Of(string? word) =>
        ListWidgets.Find(word) is { } line ? Of(line.Kind) : null;

    /// <summary>The widget and the style <paramref name="item"/> is, or null for a widget of one style or a taskbar entry.</summary>
    public static (StyledWidget Widget, WidgetStyle Style)? Of(WidgetItem? item) =>
        item is CellItem cell ? Of(cell.Kind) : null;

    /// <summary>Whether <paramref name="item"/> is a widget with styles to choose from.</summary>
    public static bool Has(WidgetItem? item) => Of(item) is not null;

    /// <summary>Whether <paramref name="item"/> is a widget with readings to choose from (the performance widget).</summary>
    public static bool HasReadings(WidgetItem? item) => Of(item) is { Style.Reading: not null };

    /// <summary>
    /// The form a file's words name: a widget's word, its style and its reading ("calendar", "week" →
    /// "weekView"; "performance", "graph", "gpu" → "gpu"); any other word as it is, a widget of one style.
    /// </summary>
    public static string FormWord(string? kind, string? style, string? reading = null) =>
        Find(kind) is { } widget ? ListWidgets.Of(widget.StyleOf(style, reading).Form).Info.Id : kind ?? "";

    /// <summary>The word the file writes as "kind" for the form <paramref name="form"/>: its widget's, else its own.</summary>
    public static string FileKind(string form) => Of(form) is { } styled ? styled.Widget.Id : form;

    /// <summary>
    /// The word the file writes as "style" for the form <paramref name="form"/>: null for a widget of one
    /// style, and for a widget's first style, which its word alone says ("calendar" is the agenda), so
    /// a file that never had styles needs no word more for the lines it has. A widget with readings
    /// says its style always, beside its reading: "performance" alone was the processor's graph, and
    /// is read so, but a line says both its choices (§228) — where it has a style to choose (§318:
    /// the performance details say their reading alone).
    /// </summary>
    public static string? FileStyle(string form) =>
        Of(form) is { } styled && Chooses(styled.Widget, styled.Style) && (styled.Style.Reading is not null || styled.Style != styled.Widget.Default) ? styled.Style.Word : null;

    /// <summary>
    /// §318: whether a line of <paramref name="widget"/> in <paramref name="style"/> has a style to
    /// choose, two looks or more (the performance widget's graph or its figure); where it has not —
    /// the performance details, a reading each in one style — its menu has no Style row, the file no
    /// "style", and the add list's rows are its forms.
    /// </summary>
    public static bool Chooses(StyledWidget widget, WidgetStyle style) => LooksOf(widget, style).Count > 1;

    /// <summary>The word the file writes as "reading" for the form <paramref name="form"/> ("gpu"); null for a form that measures nothing of its own.</summary>
    public static string? FileReading(string form) => Of(form)?.Style.Reading;

    /// <summary>
    /// <paramref name="item"/> in <paramref name="style"/>, a style of its own widget: its form changed and
    /// everything else kept — its id, its height (as the style allows it, LineHeight.Within) and its
    /// options, each one even where the new style does not read it. False where it
    /// is no widget with styles, the style is another widget's, or it is in that style already.
    /// </summary>
    public static bool Restyle(CellItem item, WidgetStyle style)
    {
        if (Of(item) is not { } now || now.Widget.Id != style.Of || now.Style.Form == style.Form)
        {
            return false;
        }
        item.Kind = ListWidgets.Of(style.Form).Info.Id;
        item.Normalize();
        return true;
    }

    // --- The style choice and the reading choice --------------------------------------------------
    //
    // A line's style choice is its widget's styles of its reading, and beside a reading's graph its
    // figure alone: the graph's form with its graph turned off (PerformanceOptions.ShowGraph). An entry
    // of the choice is a look: the style it is, and for the graph and the figure whether a graph is
    // drawn. The reading choice is the widget's readings, a pick keeping the style. The menu's rows and
    // the page in Settings are both these (OptionRows.Style, OptionRows.Reading).

    /// <summary>
    /// One entry of a line's style choice: its name, its style, its icon, and for an entry that is its style's form drawn
    /// one way of several (a reading's graph or its figure, a clock's looks) whether a line is drawn so and how one is
    /// made so.
    /// </summary>
    private readonly record struct Look(string Title, WidgetStyle Style, string Glyph, Func<WidgetItem, bool>? Is = null, Action<CellItem>? Make = null);

    private static List<Look> LooksOf(StyledWidget widget, WidgetStyle now)
    {
        var looks = new List<Look>();
        foreach (var style in widget.StylesBeside(now))
        {
            if (style.Reading is not null && style.Word == GraphWord)
            {
                looks.Add(Graph(style.Title, style, style.Glyph, graph: true));
                looks.Add(Graph(Strings.Text($"core.widgetStyle.{widget.Id}.{FigureWord}"), style, OptionGlyphs.Figure, graph: false));
            }
            else if (style.Form == ListKind.Clock)
            {
                // §443: the clock's form in each of its looks.
                looks.AddRange(Enum.GetValues<ClockStyle>().Select(look => new Look(ClockStyles.Title(look), style, ClockStyles.Glyph(look),
                    item => ClockStyles.Of(item) == look, item => (item.Clock ??= new ClockOptions()).Style = look)));
            }
            else
            {
                looks.Add(new(style.Title, style, style.Glyph));
            }
        }
        return looks;
    }

    /// <summary>A reading's graph, or its figure alone: the graph's form drawing a graph or none (<see cref="PerformanceOptions.ShowGraph"/>).</summary>
    private static Look Graph(string title, WidgetStyle style, string glyph, bool graph) =>
        new(title, style, glyph, item => DrawsGraph(item) == graph, item => (item.Performance ??= new PerformanceOptions()).ShowGraph = graph);

    private static bool DrawsGraph(WidgetItem item) => item.Performance?.ShowGraph ?? true;

    /// <summary>What <paramref name="item"/>'s style choice offers, by name (see above); empty for a widget of one style.</summary>
    public static IReadOnlyList<string> LookNames(WidgetItem? item) =>
        Of(item) is { } styled ? [.. LooksOf(styled.Widget, styled.Style).Select(look => look.Title)] : [];

    /// <summary>§249: each of <see cref="LookNames"/>' icon, in their order.</summary>
    public static IReadOnlyList<string> LookGlyphs(WidgetItem? item) =>
        Of(item) is { } styled ? [.. LooksOf(styled.Widget, styled.Style).Select(look => look.Glyph)] : [];

    /// <summary>Which of <see cref="LookNames"/> <paramref name="item"/> is in; -1 for a widget of one style.</summary>
    public static int LookIndex(WidgetItem? item) =>
        item is not null && Of(item) is { } styled
            ? LooksOf(styled.Widget, styled.Style).FindIndex(look => look.Style == styled.Style && (look.Is is not { } drawn || drawn(item)))
            : -1;

    /// <summary>
    /// <paramref name="item"/> in entry <paramref name="index"/> of its style choice: restyled
    /// (<see cref="Restyle"/>), and a reading's graph told whether to draw its graph. False where
    /// nothing changed.
    /// </summary>
    public static bool ChooseLook(CellItem item, int index)
    {
        if (Of(item) is not { } styled || LooksOf(styled.Widget, styled.Style) is not { } looks || index < 0 || index >= looks.Count)
        {
            return false;
        }
        var look = looks[index];
        var changed = Restyle(item, look.Style);
        if (look.Is is { } drawn && !drawn(item))
        {
            look.Make!(item);
            changed = true;
        }
        return changed;
    }

    /// <summary>What <paramref name="item"/> may measure, by name ("Processor", "Graphics" …); empty for a widget without readings.</summary>
    public static IReadOnlyList<string> ReadingNames(WidgetItem? item) =>
        Of(item) is { Style.Reading: not null } styled ? [.. styled.Widget.Readings.Select(styled.Widget.ReadingTitle)] : [];

    /// <summary>§249: each of <see cref="ReadingNames"/>' icon, in their order.</summary>
    public static IReadOnlyList<string> ReadingGlyphs(WidgetItem? item) =>
        Of(item) is { Style.Reading: not null } styled
            ? [.. styled.Widget.Readings.Select(reading => styled.Widget.Styles.First(style => style.Reading == reading).ReadingGlyph)]
            : [];

    /// <summary>Which of <see cref="ReadingNames"/> <paramref name="item"/> measures; -1 for a widget without readings.</summary>
    public static int ReadingIndex(WidgetItem? item) =>
        Of(item) is { Style.Reading: { } reading } styled ? styled.Widget.Readings.ToList().IndexOf(reading) : -1;

    /// <summary>
    /// <paramref name="item"/> measuring reading <paramref name="index"/> in the style it is in — a
    /// graph another reading's graph, details another's details — everything else kept
    /// (<see cref="Restyle"/>). False where it is no widget with readings, or measures that already.
    /// </summary>
    public static bool ChooseReading(CellItem item, int index)
    {
        if (Of(item) is not { Style.Reading: not null } styled || index < 0 || index >= styled.Widget.Readings.Count)
        {
            return false;
        }
        return Restyle(item, styled.Widget.StyleOf(styled.Style.Word, styled.Widget.Readings[index]));
    }

    /// <summary>
    /// Whether a widget of <paramref name="item"/>'s kind keeps an options record of
    /// <paramref name="options"/>: its own kind's, or — for a widget with styles — any of its styles',
    /// so a style changed and changed back finds its options where it left them.
    /// </summary>
    public static bool Keeps(WidgetItem item, WidgetOptionsKind options) =>
        item.Info?.Options == options
        || (Of(item) is { } styled && styled.Widget.Styles.Any(style => ListWidgets.Of(style.Form).Info.Options == options));

    /// <summary>The forms of <paramref name="item"/>'s widget: each of its styles' for a widget with styles, its own for any other.</summary>
    public static IEnumerable<ListKind> FormsOf(WidgetItem item) =>
        Of(item) is { } styled ? styled.Widget.Styles.Select(style => style.Form)
        : item is CellItem cell && ListWidgets.Find(cell.Kind) is { } line ? [line.Kind]
        : [];
}
