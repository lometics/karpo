// What a placed widget is in the config (the split, S5 step 6a).
//
// A widget stands on a taskbar or in a folder, and the file says which by where it is written: a
// display's "islands" hold the taskbar's entries (BarItem), a folder's "lists" its lines
// (CellItem). Both are a WidgetItem: an id, the size a person gave it, and the options records a
// widget carries wherever it stands. What only a taskbar's entry has — a folder's list, its section,
// the web-search box — is on BarItem
// alone, so a widget of a folder cannot carry a folder.
//
// Each names its kind in its own registry's words. A taskbar's entry is a BarKind. A folder's
// widget keeps the word the file wrote ("clock"), and the folder it stands in reads it as its line.
//
// The file does not change. The two classes write the keys the one class always wrote, in the same
// order (JsonPropertyOrder), and a folder's widget is still "island" with a "kind". Which view a
// folder's widget stands in is not in the file: the folder fills it in where the file
// is read (Folders.Project, run by Config.Repair), and so does whatever makes a widget for a folder
// (ListKind.New).

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Karpo.Core;

/// <summary>
/// One placed widget: an entry of a taskbar (<see cref="BarItem"/>) or a widget of a folder
/// (<see cref="CellItem"/>). <see cref="Id"/> tells it apart from the others of its list; exactly
/// the options of what it is (<see cref="Info"/>) are set, the others are null and stay out of the file.
/// </summary>
public abstract class WidgetItem
{
    private string? id;

    /// <summary>
    /// 8 hex digits, unique within the island's list. Made up on first use when the
    /// file had none; <see cref="Config.Repair"/> notices that and has the file saved,
    /// so the dock and the settings app don't each make up a different one.
    /// </summary>
    [JsonPropertyOrder(0)]
    public string Id
    {
        get => id ??= NewId();
        set => id = value;
    }

    /// <summary>Whether an id was given (by the file or in code) rather than made up on first use.</summary>
    internal bool HasId => !string.IsNullOrWhiteSpace(id);

    /// <summary>What it is, wherever it stands (<see cref="WidgetInfo"/>); null for a kind this build does not know.</summary>
    [JsonIgnore]
    public abstract WidgetInfo? Info { get; }

    /// <summary>
    /// Whether what it is has a height a person sets: a folder's widget whose line is its own card
    /// (<see cref="Folders.HasHeight(ListKind)"/>). Never a taskbar entry's: an entry is as wide as it
    /// needs (docs\widgets.md §12.3), so it has no size to set.
    /// </summary>
    [JsonIgnore]
    internal abstract bool HasHeight { get; }

    /// <summary>Where a new widget of what it is starts (<see cref="LineHeight.StartFor"/>), where it has a height.</summary>
    [JsonIgnore]
    internal abstract LineHeight StartsAt { get; }

    /// <summary>
    /// list: how tall it is down its folder's list (<see cref="LineHeight"/>): automatic up to a
    /// count of rows, or fixed at one. <b>Always written</b> for a widget with a height
    /// (<see cref="HasHeight"/>) — where its kind starts if nobody set one — and never for anything
    /// else, a taskbar entry least of all. Read through <see cref="LineHeight.For"/>.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyOrder(2)]
    public LineHeight? Height { get; set; }

    /// <summary>
    /// list, §432: which of its sizes a line of a kind with a few forms shows (<see cref="ListWidget.Forms"/>):
    /// <c>"size": "compact"</c>. Always written for such a line, never for anything else — a kind of one form,
    /// a line whose content grows (it has <see cref="Height"/>), a taskbar entry.
    /// </summary>
    [JsonPropertyName("size"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyOrder(2)]
    public LineSize? Size { get; set; }

    /// <summary>The width that means "as wide as the content needs".</summary>
    public const int AutomaticWidth = 0;

    public const int MinWidth = 1;

    /// <summary>
    /// Where most kinds' maximum starts (<see cref="WidgetSize.MaxCells"/>): eight cells is a wide
    /// widget on a 48-DIP bar (352 DIP). Only a starting point — a person may set up to
    /// <see cref="MostCells"/>.
    /// </summary>
    public const int MaxWidth = 8;

    /// <summary>
    /// The most cells a range on the bar reaches: 32. A number past it is a typo, and reads as the
    /// nearest one that is not; a range that reaches it holds nothing back (<see cref="WidgetBounds.Open"/>).
    /// A folder's widget has rows instead (<see cref="LineHeight.Most"/>).
    /// </summary>
    public const int MostCells = 32;

    /// <summary>A pin's target, name and icon.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyOrder(4)]
    public PinOptions? Pin { get; set; }

    /// <summary>drives: which drive a Drive or Network drive widget shows.</summary>
    [JsonPropertyName("drive"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyOrder(5)]
    public DriveTargetOptions? Drive { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyOrder(8)]
    public WeatherOptions? Weather { get; set; }

    /// <summary>markets: what a markets widget tracks and how it shows it (<see cref="MarketOptions"/>, docs\widgets.md §246).</summary>
    [JsonPropertyName("market"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyOrder(9)]
    public MarketOptions? Market { get; set; }

    /// <summary>
    /// perf: a performance graph's own settings: the pace of its graph. Which metric it shows is
    /// not among them, because the metric is the widget's kind.
    /// </summary>
    [JsonPropertyName("performance"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyOrder(10)]
    public PerformanceOptions? Performance { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyOrder(11)]
    public CalendarOptions? Calendar { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyOrder(12)]
    public ClockOptions? Clock { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyOrder(13)]
    public DesktopOptions? Desktops { get; set; }

    /// <summary>quick: the volume slider's direction and length.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyOrder(16)]
    public VolumeOptions? Volume { get; set; }

    /// <summary>quick: a one-button widget's options (<see cref="WidgetInfo.Switch"/>).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyOrder(19)]
    public SwitchOptions? Switch { get; set; }

    /// <summary>calendar: a timer's or a focus session's own length and name, wherever it stands.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyOrder(21)]
    public CountdownOptions? Countdown { get; set; }

    /// <summary>list: a heading's words.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyOrder(23)]
    public HeadingOptions? Heading { get; set; }

    /// <summary>
    /// quick: which displays the brightness's slider sets, where somebody chose the one it is on.
    /// Absent means every display, and it is never written for a widget nobody set.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyOrder(24)]
    public BrightnessOptions? Brightness { get; set; }

    /// <summary>rss: the feed this widget follows, on the bar and in a folder alike.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyOrder(25)]
    public RssOptions? Rss { get; set; }

    /// <summary>list: which toggles a toggles row shows, in its order (<see cref="TogglesOptions"/>).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyOrder(29)]
    public TogglesOptions? Toggles { get; set; }

    /// <summary>list: an actions line's two pages of Windows' Settings (<see cref="ActionsOptions"/>).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyOrder(30)]
    public ActionsOptions? Actions { get; set; }

    /// <summary>notes: the to-do list a to-do line shows (<see cref="TodoOptions"/>).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyOrder(40)]
    public TodoOptions? Todo { get; set; }

    /// <summary>notes: the note a note line shows (<see cref="NoteOptions"/>).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyOrder(41)]
    public NoteOptions? Note { get; set; }

    /// <summary>alarm: an alarm's time, days, switch and name (<see cref="AlarmOptions"/>). A file from before it has none.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyOrder(42)]
    public AlarmOptions? Alarm { get; set; }

    /// <summary>
    /// list: how a line of items draws them (<see cref="ItemStyleOptions"/>, ItemStyles.cs): their size
    /// and the parts shown. Only on a line of a kind that has items, and only where somebody chose
    /// something other than its defaults; a file from before it has none and draws as it did.
    /// </summary>
    [JsonPropertyName("items"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyOrder(43)]
    public ItemStyleOptions? Items { get; set; }

    /// <summary>
    /// sound: the devices an output or a microphone line does not list, by Windows' endpoint id
    /// (docs\widgets.md §159): hidden at rest, greyed in edit mode, where a click on one shows it
    /// again. Only on those two lines, and absent where nothing is hidden.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyOrder(44)]
    public string[]? HiddenDevices { get; set; }

    /// <summary>
    /// §455: which of the accounts' parts a calendar, a mail or a contacts widget shows (<see cref="WidgetSources"/>):
    /// each by its account's address and its part ("me@example.com|calendar:work", "me@example.com|mail:Archive").
    /// Absent, as on every widget nobody set, is every calendar and address book and every account's inbox.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyOrder(45)]
    public string[]? Sources { get; set; }

    /// <summary>sports: the team a sports line follows (<see cref="SportsOptions"/>, docs\widgets.md §239). A file from before it has none.</summary>
    [JsonPropertyName("sports"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyOrder(48)]
    public SportsOptions? Sports { get; set; }

    /// <summary>camera: the stream a camera line shows and how (<see cref="CameraOptions"/>, docs\widgets.md §331). A file from before it has none.</summary>
    [JsonPropertyName("camera"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyOrder(48)]
    public CameraOptions? Camera { get; set; }

    /// <summary>§641: a Start widget's pins, view, folders or what it recommends (<see cref="StartOptions"/>); none where nobody set them.</summary>
    [JsonPropertyName("start"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyOrder(49)]
    public StartOptions? Start { get; set; }

    /// <summary>§642, §652: Windows search's sources switched from their defaults (<see cref="WinSearchOptions"/>); none for none.</summary>
    [JsonPropertyName("winSearch"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyOrder(50)]
    public WinSearchOptions? WinSearch { get; set; }

    /// <summary>
    /// <paramref name="options"/>, the record of that kind it carries now, as <see cref="Normalize"/>
    /// keeps it: its own kind's, made where it has none; a record another style of its widget reads,
    /// kept where it has one (<see cref="WidgetStyles.Keeps"/>); any other, none.
    /// </summary>
    private T? Kept<T>(WidgetOptionsKind options, T? now)
        where T : class, new() =>
        Info?.Options == options ? now ?? new T() : Keeps(options) ? now : null;

    /// <summary>Whether it keeps a record of <paramref name="options"/> (<see cref="WidgetStyles.Keeps"/>).</summary>
    private bool Keeps(WidgetOptionsKind options) => WidgetStyles.Keeps(this, options);

    /// <summary>perf: whether it, or another style of its widget, shows <paramref name="metric"/>.</summary>
    private bool Reads(Metric metric) =>
        Info?.Metric == metric || WidgetStyles.FormsOf(this).Any(form => ListWidgets.Of(form).Info.Metric == metric);

    /// <summary>Whether a widget of <paramref name="id"/> lists devices a person may hide (<see cref="HiddenDevices"/>).</summary>
    public static bool HidesDevices(string? id) => id is "soundOutput" or "soundInput";

    /// <summary>
    /// sound: <paramref name="device"/> hidden or shown again on this line (<see cref="HiddenDevices"/>),
    /// the rest kept in their order; the field goes where nothing is left hidden.
    /// </summary>
    public void SetDeviceHidden(string device, bool hidden)
    {
        var rest = (HiddenDevices ?? []).Where(each => each != device);
        var next = hidden ? [.. rest, device] : rest.ToArray();
        HiddenDevices = next.Length == 0 ? null : next;
    }

    public static string NewId() => Guid.NewGuid().ToString("N")[..8];

    /// <summary>A deep copy with the same id, standing where this one does.</summary>
    public abstract WidgetItem Clone();

    /// <summary>
    /// Gives the kind its options (defaults when missing), drops the others, and clamps values: the
    /// ones every placed widget carries. A taskbar's entry adds its own (<see cref="BarItem.Normalize"/>).
    /// </summary>
    public virtual void Normalize()
    {
        if (string.IsNullOrWhiteSpace(Id))
        {
            Id = NewId();
        }
        // The height: always, for a widget that has one — what it carries, else where its kind
        // starts — its rows 1 to LineHeight.Most. Anything else has none, and nothing is written for it.
        if (HasHeight)
        {
            var height = Height is { Cells: > 0 } own ? own : StartsAt;
            // Held to what its kind allows (LineHeight.Within(ListKind)): its limits, and fixed where
            // its content does not vary — so a file from before the limits is set right in place.
            Height = ListWidgets.Find(Info?.Id) is { } line ? height.Within(line.Kind) : height.Within();
        }
        else
        {
            Height = null;
        }
        // §432: a line of a few forms keeps the size it shows, one of its kind's; nothing else keeps one.
        Size = this is CellItem && ListWidgets.Find(Info?.Id) is { Forms.Count: > 1 } sized ? LineSizes.Of(sized, Size) : null;
        // The options record is the one the widget declared (WidgetInfo.Options), wherever it
        // stands, and every other is cleared: a widget carries exactly its own, and a record the
        // widget does not read is not written into the file. A widget with styles keeps every record
        // one of its styles reads, so a style changed and changed back finds them, and makes only its
        // own where it has none (Kept, WidgetStyles.Keeps).
        var info = Info;
        Pin = Kept(WidgetOptionsKind.Pin, Pin);
        if (Pin is { } pin)
        {
            pin.Target = pin.Target?.Trim() ?? "";
            pin.Name = pin.Name?.Trim() ?? "";
            pin.Icon = pin.Icon?.Trim() ?? "";
            // §380: a folder's contents keep their navigation (one this build does not know is File
            // Explorer) and their view; a pin has neither, so it never carries them.
            var lists = PinFolders.Lists(this);
            pin.Navigation = lists ? PinFolders.Known(pin.Navigation) : PinFolderNavigation.Explorer;
            pin.View = lists ? PinFolders.Known(pin.View) : PinFolderView.List;
        }
        Drive = Kept(WidgetOptionsKind.Drive, Drive);
        if (Drive is { } target)
        {
            target.Volume = target.Volume?.Trim() ?? "";
            target.Letter = DriveTargets.Letter(target.Letter);
            target.Label = target.Label?.Trim() ?? "";
        }
        // weather: the widget and the three rows a list folder holds all carry a place of their
        // own (docs\widgets.md §9).
        Weather = Kept(WidgetOptionsKind.Weather, Weather);
        // markets: its symbol, its words and its amounts as they may be (§246, §258).
        Market = Kept(WidgetOptionsKind.Market, Market);
        Market?.Normalize();
        // rss: the address is kept only where it is one Karpo asks for — http or https — and the
        // feed's name, which the feed wrote, is plain words no longer than a card can show.
        Rss = Kept(WidgetOptionsKind.Rss, Rss);
        if (Rss is { } rss)
        {
            rss.Address = Feeds.Address(rss.Address) ?? "";
            rss.Title = Feeds.Plain(rss.Title, RssOptions.MostTitle);
            // An icon chosen is one glyph of the icon font's, as a pin's (§306); anything else is none.
            rss.Glyph = rss.Glyph is [>= '' and <= ''] ? rss.Glyph : null;
        }
        // perf: the five graphs share one options class, as the clock and the date do.
        Performance = Kept(WidgetOptionsKind.Performance, Performance);
        if (Performance is { } graph)
        {
            // A pace that is none of the list's reads as the nearest one that is (§228): a file's 3
            // from when the list was every second from one to ten is 2, and a 0 is not a bar that
            // samples a hundred times a second.
            graph.Seconds = PerformanceChoices.Nearest(graph.Seconds);
            // The adapter and the disk only where a style of the widget reads them: a graph in the
            // processor's style keeps the card it follows in the graphics' (WidgetStyles).
            graph.GpuAdapter = Reads(Metric.Gpu) ? graph.GpuAdapter?.Trim() ?? "" : "";
            // The disk a disk graph follows, written as Task Manager names it ("Disk 0"); anything
            // else, and any other kind's, is every disk together, which is not written at all.
            graph.Disk = Reads(Metric.Disk) && PerformanceOptions.DiskNumber(graph.Disk) is { } disk
                ? PerformanceOptions.DiskName(disk)
                : null;
        }
        Calendar = Kept(WidgetOptionsKind.Calendar, Calendar);
        // calendar: the clock and the date are two widgets with the same options.
        Clock = Kept(WidgetOptionsKind.Clock, Clock);
        if (Clock is { } clock)
        {
            // §443: a look this build does not know is the clock as it was, which is not written.
            clock.Style = ClockStyles.Known(clock.Style);
        }
        Desktops = Kept(WidgetOptionsKind.Desktops, Desktops);
        // quick: the one-button widgets — and the Security widget, which is not one of them (its
        // button switches nothing) but wears the same label option.
        Switch = Kept(WidgetOptionsKind.Switch, Switch);
        if (Switch is { } button)
        {
            // Spatial sound's format, a GUID as Windows writes it; anything else, or any other
            // switch's, is the one last used, which is not written at all.
            button.SpatialFormat = info?.Switch == QuickSwitch.SpatialSound && Guid.TryParse(button.SpatialFormat, out var format) && format != Guid.Empty
                ? format.ToString("B").ToUpperInvariant()
                : null;
            // Color filters' filter, one of Windows'; grayscale, which is not written, for any other switch.
            button.ColorFilter = info?.Switch == QuickSwitch.ColorFilters ? ColorFilterChoices.Known(button.ColorFilter) : ColorFilter.Grayscale;
            // Keep awake's how long, one it offers; nothing, which is not written, for any other switch.
            button.AwakeMinutes = info?.Switch == QuickSwitch.KeepAwake ? KeepAwakeLengths.Known(button.AwakeMinutes) : 0;
        }
        Volume = Kept(WidgetOptionsKind.Volume, Volume);
        if (Volume is { } slider)
        {
            slider.Length = Math.Clamp(slider.Length, VolumeOptions.ShortestDip, VolumeOptions.LongestDip);
        }
        // quick: the brightness's displays, where somebody chose its own; every display is none.
        Brightness = Keeps(WidgetOptionsKind.Brightness) ? Brightness?.Normalized() : null;
        // calendar: a timer's and a focus session's own length and name, where somebody set them. Not
        // "?? new()": its kind's length is the absence of a choice, as a size of nobody's is, and
        // a record for every timer would be written into files that never had one. A length that
        // makes no sense is its kind's.
        Countdown = Keeps(WidgetOptionsKind.Countdown) ? Countdown : null;
        if (Countdown is { } countdown)
        {
            countdown.Minutes = CountdownOptions.Clamp(countdown.Minutes,
                info == WidgetInfos.Focus ? CountdownOptions.FocusMinutes : CountdownOptions.TimerMinutes);
            countdown.Name = countdown.Name?.Trim() ?? "";
            // A timer has no do not disturb to turn on; a focus session's is on where it says nothing.
            countdown.DoNotDisturb = info == WidgetInfos.Focus && countdown.DoNotDisturb == false ? false : null;
        }
        // list: a heading's words, one line of them and no longer than a title.
        Heading = Kept(WidgetOptionsKind.Heading, Heading);
        if (Heading is { } heading)
        {
            var text = (heading.Text ?? "").ReplaceLineEndings(" ").Trim();
            heading.Text = text.Length <= HeadingOptions.MostLength ? text : text[..HeadingOptions.MostLength].TrimEnd();
        }

        // list: the toggles a row shows, those this build knows, each once, in the order chosen.
        Toggles = Kept(WidgetOptionsKind.Toggles, Toggles);
        if (Toggles is { } toggles)
        {
            toggles.Shown = Core.Toggles.Known(toggles.Shown);
        }
        // list: an actions line's two pages, each one this build knows or empty.
        Actions = Keeps(WidgetOptionsKind.Actions) ? Actions ?? ActionsOptions.New() : null;
        Actions?.Normalize();
        // notes: the list or the note a line shows, by id; none is the notebook's first.
        Todo = Kept(WidgetOptionsKind.Todo, Todo);
        Todo?.List = Todo.List?.Trim() ?? "";
        Todo?.Account = string.IsNullOrWhiteSpace(Todo.Account) ? null : Todo.Account.Trim();
        Todo?.TaskList = string.IsNullOrWhiteSpace(Todo.TaskList) || Todo.Account is null ? null : Todo.TaskList.Trim();
        Note = Kept(WidgetOptionsKind.Note, Note);
        Note?.Note = Note.Note?.Trim() ?? "";
        // alarm: its time, its days and its name as they may be; a new one is 07:00 and off.
        Alarm = Kept(WidgetOptionsKind.Alarm, Alarm);
        Alarm?.Normalize();
        // §455: the parts it shows only on a widget of the accounts' calendars, mail or contacts, each once; none is every one.
        Sources = WidgetSources.KindOf(this) != AccountShows.None ? WidgetSources.Normalized(Sources) : null;
        // sports: a team only where it is one of the table's leagues and an id ESPN could have given.
        Sports = Kept(WidgetOptionsKind.Sports, Sports);
        Sports?.Normalize();
        // camera: an address Karpo asks for or none, a pace one of its steps, and its defaults not written (§331).
        Camera = Kept(WidgetOptionsKind.Camera, Camera);
        Camera?.Normalize();
        // §641: a Start widget's own part of its record, and nothing written for what nobody set.
        Start = Info?.Options == WidgetOptionsKind.Start && ListWidgets.Find(Info.Id) is { } startLine ? Start?.Normalized(startLine.Kind) : null;
        // §642, §652: Windows search's sources, each at its default (every one on but the web's) not written; §655 its home's
        // sections and counts at their defaults neither; none, no record.
        WinSearch = Kept(WidgetOptionsKind.WinSearch, WinSearch);
        WinSearch?.Normalize();
        WinSearch = WinSearch is { Empty: false } ? WinSearch : null;
        // list: an item style only on a line of items, and only what differs from its kind's look.
        Items = ItemStyles.KindOf(this) is { } lineKind && ItemStyles.Has(lineKind) ? Items?.Normalized(lineKind)
            : WidgetStyles.FormsOf(this).Any(ItemStyles.Has) ? Items
            : null;
        // sound: hidden devices only on a device line, each once, and nothing written for none.
        HiddenDevices = (HidesDevices(info?.Id) || WidgetStyles.FormsOf(this).Any(form => HidesDevices(form.Id()))) && HiddenDevices?.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToArray() is { Length: > 0 } hidden
            ? hidden : null;

        if (Weather is not null)
        {
            Weather.Place ??= "";
            // air: an index only on an air-quality line (§210); a word a newer build wrote is kept,
            // and read as the region's (AirIndexes.Chosen).
            Weather.AirIndex = info == WidgetInfos.AirQuality && !string.IsNullOrWhiteSpace(Weather.AirIndex) ? Weather.AirIndex.Trim() : null;
            // quakes: a radius, a magnitude and an alert only on an earthquake line (§240), each the
            // nearest of its choices, and nothing written where all are their defaults.
            Weather.Quakes = info == WidgetInfos.Quakes ? Weather.Quakes?.Normalized() : null;
        }
        if (Calendar is not null)
        {
            // The retired feed address and its refresh are left exactly as the file has them:
            // nothing reads them, and clamping a dead value would only rewrite somebody's file.
            Calendar.FeedUrl ??= "";
            // §496: the agenda's days ahead held to its range, nothing written for a week.
            Calendar.DaysAhead = CalendarOptions.KeptDaysAhead(Calendar.DaysAhead);
        }
        if (Clock is not null)
        {
            Clock.TimeZone ??= "";
            Clock.Label ??= "";
        }
    }
}

/// <summary>
/// One entry placed on a taskbar: what every placed widget carries (<see cref="WidgetItem"/>), and
/// what only a taskbar's entry has — its section, a folder's list and the widgets in it, and
/// the web-search box.
/// </summary>
public sealed class BarItem : WidgetItem
{
    /// <summary>
    /// Which taskbar entry it is, as the file names it (<see cref="BarKind"/>). A word this build
    /// does not know reads as a value outside the enum and <see cref="Config.Repair"/> drops it.
    /// </summary>
    [JsonPropertyOrder(1)]
    public BarKind Kind { get; set; }

    /// <summary>
    /// Which of the bar's three sections it stands in (<see cref="BarSection"/>): its place along the
    /// bar is its place among that section's entries, in the list's order. Always written, so a file
    /// says where every entry stands; one that says nothing, or a word this build does not know,
    /// reads as the start (<see cref="Config.Repair"/>).
    /// </summary>
    [JsonPropertyOrder(1)]
    public BarSection Section { get; set; }

    /// <summary>What it is: its entry's record, or null for a kind this build does not know.</summary>
    [JsonIgnore]
    public override WidgetInfo? Info => BarEntries.Find(Kind)?.Info;

    /// <summary>
    /// Never: a taskbar entry takes the size it needs, which its kind's range and its own settings
    /// decide (<see cref="WidgetBounds.For(BarItem)"/>), and nobody sets a width for it. The
    /// person (2026-10-08): "remove the ability to resize widgets on the taskbar".
    /// </summary>
    internal override bool HasHeight => false;

    internal override LineHeight StartsAt => LineHeight.Exactly(2);

    /// <summary>A folder's list and the widgets in it.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyOrder(15)]
    public FolderOptions? Folder { get; set; }

    /// <summary>
    /// When the pen menu or the virtual touchpad shows, where somebody chose Always. A touch
    /// keyboard's, from an older file, is dropped: it always shows. Absent means where Windows would (<see cref="InputButtons"/>), and it is never written
    /// for a button nobody set.
    /// </summary>
    [JsonPropertyName("inputButton"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyOrder(21)]
    public InputButtonOptions? InputButton { get; set; }

    /// <summary>
    /// search: whether Windows' search button is the search box, where somebody chose it. Absent
    /// means the icon alone, and it is never written for a button nobody set.
    /// </summary>
    [JsonPropertyName("windowsSearch"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyOrder(22)]
    public WindowsSearchOptions? WindowsSearch { get; set; }

    /// <summary>
    /// The status icons: where each shows — on the bar, behind the entry's arrow, or nowhere — where
    /// somebody chose anything but the bar (<see cref="StatusIcons"/>). Absent means every icon on
    /// the bar, and it is never written for an entry nobody set.
    /// </summary>
    [JsonPropertyName("statusIcons"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyOrder(27)]
    public StatusIconOptions? StatusIcons { get; set; }

    /// <summary>
    /// §302: Show desktop as Windows 11's bare corner, where somebody chose it. Absent means its icon,
    /// and it is never written for a button nobody set.
    /// </summary>
    [JsonPropertyName("showDesktop"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyOrder(28)]
    public ShowDesktopOptions? ShowDesktop { get; set; }

    /// <summary>
    /// §347: a tray's own icons — where each shows, on the bar, behind its chevron or nowhere, and their
    /// order (<see cref="TrayIconOptions"/>, <see cref="TrayLayout"/>). Absent means every icon where
    /// Windows puts it, and it is never written for a tray nobody set.
    /// </summary>
    [JsonPropertyName("trayIcons"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyOrder(29)]
    public TrayIconOptions? TrayIcons { get; set; }

    /// <summary>
    /// A deep copy with the same id. A folder's widgets are stood in its view again
    /// (<see cref="Folders.Project"/>), because which view that is is not in the file.
    /// </summary>
    public override BarItem Clone()
    {
        var copy = JsonSerializer.Deserialize(JsonSerializer.Serialize(this, ConfigJson.Default.BarItem), ConfigJson.Default.BarItem)!;
        if (copy.Folder is { } folder)
        {
            Folders.Project(folder, Folders.View(copy.Kind));
        }
        return copy;
    }

    /// <summary>
    /// A deep copy with a new id, and a folder's widgets new ids too: the one copy rule, which every
    /// copy goes by — "Duplicate" and "Copy" in a widget's menu, Ctrl+V, a layout duplicated
    /// (<see cref="TaskbarLayout.CopyAs"/>). An id is what the dock keeps a widget's own state by
    /// across every layout — a timer's countdown, a stopwatch, an alarm's ring — so a copy that kept
    /// one would run the original's timer and ring its alarm (the person: "i dont want ... widgets
    /// behave weirdly because they got copied").
    /// </summary>
    public BarItem Copy()
    {
        var copy = Clone();
        copy.Id = NewId();
        foreach (var cell in copy.Folder?.Cells ?? [])
        {
            if (cell?.Island is { } inside)
            {
                inside.Id = NewId();
            }
        }
        return copy;
    }

    /// <summary>
    /// What a paste puts down of this widget from edit mode's clipboard. One that was
    /// <paramref name="cut"/> is moved, not copied: itself again, its id and its folder's widgets'
    /// kept, so what it was doing comes with it (a running timer, a stopwatch) — where none of
    /// <paramref name="taken"/> (every id of the config, <see cref="Folders.Ids"/>) is one of them, as after the
    /// cut took it out; pasted again it meets itself there, and is a copy. Anything else is a copy
    /// (<see cref="Copy"/>). Its ids are among <paramref name="taken"/> from then on.
    /// </summary>
    public BarItem Pasted(bool cut, HashSet<string> taken)
    {
        var moved = cut && Folders.All([this]).All(entry => !taken.Contains(entry.Island.Id));
        var pasted = moved ? Clone() : Copy();
        taken.UnionWith(Folders.Ids([pasted]));
        return pasted;
    }

    /// <summary>Same kind and same options, whatever the id and the section it stands in.</summary>
    public bool SameOptions(BarItem other)
    {
        if (other.Kind != Kind)
        {
            return false;
        }
        var mine = Clone();
        var theirs = other.Clone();
        mine.Id = theirs.Id = "";
        mine.Section = theirs.Section = BarSection.Start;
        return JsonSerializer.Serialize(mine, ConfigJson.Default.BarItem) == JsonSerializer.Serialize(theirs, ConfigJson.Default.BarItem);
    }

    /// <summary>
    /// What every placed widget's options come to (<see cref="WidgetItem.Normalize"/>), and what
    /// only a taskbar's entry has: a folder's list,
    /// when an input button shows and where each status icon does.
    /// </summary>
    public override void Normalize()
    {
        base.Normalize();
        // An entry has no size of its own (HasHeight): what a file still holds, from when the bar
        // had widths, is dropped without a word (WidgetItem.Normalize), and is not written back.
        var declared = Info?.Options ?? WidgetOptionsKind.None;
        Folder = declared == WidgetOptionsKind.Folder ? Folder ?? new() : null;
        Folder?.Normalize();
        // The same for the input buttons: Automatic is no options at all.
        InputButton = declared == WidgetOptionsKind.InputButton ? InputButton?.Normalized() : null;
        // search: and for Windows' search button, the icon.
        WindowsSearch = declared == WidgetOptionsKind.WindowsSearch ? WindowsSearch?.Normalized() : null;
        // The status icons: every icon on the bar is no options at all.
        StatusIcons = declared == WidgetOptionsKind.StatusIcons ? StatusIcons?.Normalized() : null;
        // §302: and for Show desktop, its icon.
        ShowDesktop = declared == WidgetOptionsKind.ShowDesktop ? ShowDesktop?.Normalized() : null;
        // §347: and for a tray, every icon where Windows puts it.
        TrayIcons = declared == WidgetOptionsKind.Tray ? TrayIcons?.Normalized() : null;
    }
}

/// <summary>
/// One widget of a folder: a line of a list folder (<see cref="View"/>).
/// </summary>
public sealed class CellItem : WidgetItem
{
    private string kind = "";
    private FolderView? view;
    private ListKind? list;

    // What the file said, where it was read from one: its "kind", its "style" and its "reading" as
    // written, so the form is the same whichever the file put first (WidgetStyles).
    private string? fileKind;
    private string? fileStyle;
    private string? fileReading;

    /// <summary>
    /// What it is: the word of its list widget ("clock", "weekView") — for a widget with styles, its
    /// style's form (<see cref="WidgetStyles"/>) — read in the view the widget stands in
    /// (<see cref="List"/>); a word no registry has reads as nothing, and the folder drops the widget
    /// where the file is read. Not the file's words: those are <see cref="FileKind"/> and <see cref="FileStyle"/>.
    /// </summary>
    [JsonIgnore]
    public string Kind
    {
        get => kind;
        set
        {
            kind = value ?? "";
            (fileKind, fileStyle, fileReading) = (null, null, null);
            Project();
        }
    }

    /// <summary>
    /// What the file says it is: the widget's word ("calendar") for a widget with styles, else its
    /// <see cref="Kind"/>. Read, it is a widget's word with its style.
    /// </summary>
    [JsonPropertyName("kind"), JsonPropertyOrder(1)]
    public string FileKind
    {
        get => WidgetStyles.FileKind(kind);
        set
        {
            fileKind = value ?? "";
            Read();
        }
    }

    /// <summary>The file's word for what it measures ("gpu"): only for a widget with readings, the performance one (<see cref="WidgetStyles.FileReading"/>).</summary>
    [JsonPropertyName(WidgetStyles.ReadingKey), JsonPropertyOrder(1), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FileReading
    {
        get => WidgetStyles.FileReading(kind);
        set
        {
            fileReading = value;
            Read();
        }
    }

    /// <summary>The file's word for its style ("week"): only for a widget with styles, and not for its first (<see cref="WidgetStyles.FileStyle"/>).</summary>
    [JsonPropertyName(WidgetStyles.StyleKey), JsonPropertyOrder(1), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FileStyle
    {
        get => WidgetStyles.FileStyle(kind);
        set
        {
            fileStyle = value;
            Read();
        }
    }

    private void Read()
    {
        kind = WidgetStyles.FormWord(fileKind, fileStyle, fileReading);
        Project();
    }

    /// <summary>
    /// The view it stands in — a line of a list — or null where nothing has said yet. It is not in
    /// the file: the folder that holds it fills it in where the file is read
    /// (<see cref="Folders.Project"/>), and so does whatever makes a widget for a folder
    /// (<see cref="Sections.New(ListKind)"/>).
    /// </summary>
    [JsonIgnore]
    public FolderView? View
    {
        get => view;
        set
        {
            view = value;
            Project();
        }
    }

    /// <summary>Its line, where it stands in a list folder; null anywhere else, and for a word no list has.</summary>
    [JsonIgnore]
    public ListKind? List => list;

    /// <summary>What it is: its line's record, or, standing nowhere yet, the one its word names.</summary>
    [JsonIgnore]
    public override WidgetInfo? Info =>
        list is { } line ? ListWidgets.Of(line).Info : WidgetInfos.Find(kind);

    // A folder's widget has a height where its line is its own card, but a divider's, which is its
    // hairline whatever it is given (Folders.HasHeight).
    internal override bool HasHeight => ListWidgets.Find(kind) is { } line && line.Kind.HasHeight();

    internal override LineHeight StartsAt =>
        ListWidgets.Find(kind) is { } line ? LineHeight.StartFor(line.Kind) : LineHeight.Exactly(2);

    private void Project()
    {
        list = view == FolderView.List ? ListWidgets.Find(kind)?.Kind : null;
    }

    /// <summary>
    /// list: false where a line that may leave its folder at rest while it has nothing to show
    /// (<see cref="ListWidget.IdleKey"/>, §327) is set to stay. Absent is on, as the now-playing
    /// face's is (§319): a file from before it hides such a line, and nothing is written for it.
    /// </summary>
    [JsonPropertyName("hideIdle"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyOrder(50)]
    public bool? HideIdle { get; set; }

    /// <summary>list: whether its line leaves the folder at rest while it has nothing to show (<see cref="HideIdle"/>).</summary>
    [JsonIgnore]
    public bool HidesIdle => IdleKey is not null && HideIdle != false;

    /// <summary>The strings key of its "hide while idle" switch; null for a line that is always there (<see cref="ListWidget.IdleKey"/>).</summary>
    [JsonIgnore]
    public string? IdleKey => ListWidgets.Find(kind)?.IdleKey;

    /// <summary>
    /// §435: a divider that grows (<see cref="DividerOptions"/>). Absent means the folders' look, and it is never written
    /// for a divider nobody set.
    /// </summary>
    [JsonPropertyName("divider"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyOrder(51)]
    public DividerOptions? Divider { get; set; }

    /// <summary>
    /// Its options as <see cref="WidgetItem.Normalize"/> keeps them, hiding while idle written only where it is set off on
    /// a line that hides, and a divider's Grow only on a divider.
    /// </summary>
    public override void Normalize()
    {
        base.Normalize();
        HideIdle = IdleKey is not null && HideIdle == false ? false : null;
        Divider = Info?.Options == WidgetOptionsKind.Divider ? Divider?.Normalized() : null;
    }

    /// <summary>A deep copy with the same id, standing where this one does.</summary>
    public override CellItem Clone()
    {
        var copy = JsonSerializer.Deserialize(JsonSerializer.Serialize(this, ConfigJson.Default.CellItem), ConfigJson.Default.CellItem)!;
        copy.View = View;
        return copy;
    }

    /// <summary>A deep copy with a new id: a folder's widget by the one copy rule (<see cref="BarItem.Copy"/>).</summary>
    public CellItem Copy()
    {
        var copy = Clone();
        copy.Id = NewId();
        return copy;
    }

    /// <summary>
    /// What a paste puts down of this line from edit mode's clipboard, as a bar's widget is
    /// (<see cref="BarItem.Pasted"/>): one <paramref name="cut"/> keeps its id where none of
    /// <paramref name="taken"/> has it, moved; anything else a copy. Its id is among <paramref name="taken"/> from then on.
    /// </summary>
    public CellItem Pasted(bool cut, HashSet<string> taken)
    {
        var pasted = cut && !taken.Contains(Id) ? Clone() : Copy();
        taken.Add(pasted.Id);
        return pasted;
    }
}
