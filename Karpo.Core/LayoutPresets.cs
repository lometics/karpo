// §472: the layouts a new one starts from (the person, 2026-11-01: "when creating a new layout at the moment we only have
// one default one that we create. i want you to make some more presets that can be selected on creation. one should be one
// that mimics windows as close as possible, with the same widgets and also the windows native menus behind them, and then
// there should also be the one that we currently have and maybe a few others that are high quality layouts that can be
// selected. those just give the initial state and nothing more.").
//
// A layout preset is a recipe, as a ready-made folder is (Presets.cs): its name, a line saying what it is for, its icon,
// its type, its bar's size, the style it wears, and what stands where — Windows' buttons, the person's pins, and the
// ready-made folders (Presets.Folder, the very folders the add list makes). §628 "Add a layout" opens a page of them, each a
// tile picturing its bar as it makes it (BarLooks.AddLayoutRows; §472 to §627 a dropdown's list), and one pick makes an
// ordinary layout of it (Make): nothing remembers which it was — no key in the file, no link — so it is changed, copied and
// deleted as any other. The first layout of a new config is Karpo's (Config.FirstLayout), as it was.
//
//  - Blank (§628, the person, 2026-11-09: "there should also be one that starts just blank"): a taskbar with nothing on it,
//    to fill from the add page.
//  - Windows: Windows 11's taskbar as it comes, centred — Start, the search box, Task view, the pins and the open apps in
//    the middle, the "^" and its icons, the network, the sound and the battery on one button, the time over the date
//    beside the bell, and show desktop at the right —, each opening what Windows' opens: Start, Search and Task view are
//    Windows' own buttons, and so are the network's button and the clock — Windows' own widgets wearing Windows' faces
//    (§474: windowsQuickSettings, windowsNotifications), opening quick settings and the notifications and calendar. No
//    folder of Karpo's stands for a menu of Windows'. No Widgets button, the weather (§485, the person: "no"), as a new
//    bar has none (2026-10-14, WindowsTaskbar.cs). Follow Windows, 48 DIPs.
//  - Karpo: what a new layout was until now (Sections.DefaultLayout): Windows' buttons at the left, Karpo's quick
//    settings, keyboard and clock at the right.
//  - Focus: a floating dock, nothing but what is in use — Start, the pins and the open apps in the middle; a focus
//    session at the left; the tray, quick settings and the clock at the right. Graphite dark, calm (§509).
//  - Planner: the day — the next meeting, the inbox (the mail beside the notifications), the to-do list beside a note and a
//    focus session in the middle; today (the time over the date and the weather: the day's events and the forecast) at the
//    right. Paper light.
//  - Monitor: one taskbar that says how the PC is doing — Start, the search, Task view and the desktops at the left, the
//    pins and the open apps in the middle, and at the right the processor, the graphics, the memory, the disk and the
//    network each its figure, then the clipboard beside the tray, quick settings and the notifications. Harbor dark. (§488: it
//    was a thin bar along the top over Karpo's taskbar, a Top and bottom, until that type went.)
//
// What this PC has decides what Windows would show (LayoutMachine): the pins Windows' taskbar has, the pen and touch
// buttons, the battery.

namespace Karpo.Core;

/// <summary>§472: what of this PC a layout preset is made for: its taskbar's pins, its pen and touch screen, and Windows' taskbar.</summary>
/// <param name="Pins">The apps pinned to Windows' taskbar, in its order (<see cref="Sections.TaskbarPins"/>).</param>
/// <param name="Inputs">What it has to type and point with (<see cref="InputHardware.Read"/>).</param>
/// <param name="Taskbar">What Windows' taskbar shows (<see cref="WindowsTaskbar.Read"/>): the battery, the seconds.</param>
public sealed record LayoutMachine(IReadOnlyList<string> Pins, InputHardware Inputs, WindowsTaskbar Taskbar)
{
    /// <summary>This PC, as a new layout is made for it.</summary>
    public static LayoutMachine Read() => new(Sections.TaskbarPins(), InputHardware.Read(), WindowsTaskbar.Read());

    /// <summary>The ready-made folders' view of it: the battery and a screen that turns.</summary>
    public PresetMachine Folders => new(HasBattery: Taskbar.Battery, FixedDrives: [], Rotates: Taskbar.Rotates);
}

/// <summary>§472: a layout a new one starts from (see the top of the file). One record per preset, in <see cref="LayoutPresets.All"/>.</summary>
public sealed record LayoutPreset
{
    /// <summary>Its name in code and in a test; its words are keyed by it (<c>core.layoutPreset.&lt;id&gt;.title</c>, <c>.blurb</c>).</summary>
    public required string Id { get; init; }

    /// <summary>What "Add a layout" calls it, and the layout it makes, in the language Karpo speaks now.</summary>
    public string Title => Strings.Text($"core.layoutPreset.{Id}.title");

    /// <summary>What it is for, one line under its name.</summary>
    public string Blurb => Strings.Text($"core.layoutPreset.{Id}.blurb");

    /// <summary>A Segoe Fluent Icons code point: its row's icon, and the layout's.</summary>
    public required string Glyph { get; init; }

    /// <summary>How its bar looks.</summary>
    public LayoutType Type { get; init; } = LayoutType.Taskbar;

    /// <summary>Its bar's thickness in DIPs.</summary>
    public int Size { get; init; } = Config.DefaultHeight;

    /// <summary>What stands on it on <see cref="LayoutMachine"/>, each in its section.</summary>
    public required Func<LayoutMachine, List<BarItem>> Islands { get; init; }
}

/// <summary>§472: the layout presets, and the one act that makes a layout of one.</summary>
public static class LayoutPresets
{
    public const string BlankId = "blank";
    public const string WindowsId = "windows";
    public const string KarpoId = "karpo";
    public const string FocusId = "focus";
    public const string PlannerId = "planner";
    public const string MonitorId = "monitor";

    /// <summary>§628: a taskbar with nothing on it, of the default size.</summary>
    public static readonly LayoutPreset Blank = new()
    {
        Id = BlankId,
        Glyph = OptionGlyphs.Taskbar,
        Islands = _ => [],
    };

    /// <summary>Windows 11's own taskbar, its menus Windows' own.</summary>
    public static readonly LayoutPreset Windows = new()
    {
        Id = WindowsId,
        Glyph = OptionGlyphs.Windows,
        Islands = machine =>
        {
            var bar = new Bar(machine);
            // §485: no Widgets button, the weather, at the far left: the person asked it out.
            bar.WindowsButtons(BarSection.Middle);
            bar.Corner();
            // The network, the sound and the battery on one button, opening Windows' quick settings; the time over the
            // date beside the bell, opening Windows' notifications and calendar; then the sliver. Windows' own, all three.
            bar.Add(BarKind.WindowsQuickSettings.New(), BarSection.End);
            bar.Add(BarKind.WindowsNotifications.New(), BarSection.End);
            bar.Add(BarKind.ShowDesktop.New(), BarSection.End);
            return bar.Done();
        },
    };

    /// <summary>Karpo's: what a new layout and a new config were until §472 (<see cref="Sections.DefaultLayout"/>).</summary>
    public static readonly LayoutPreset Karpo = new()
    {
        Id = KarpoId,
        Glyph = TaskbarLayouts.LayoutGlyph,
        Islands = machine => Sections.DefaultLayout(machine.Pins, machine.Inputs, machine.Taskbar),
    };

    /// <summary>A floating dock of what is in use, and a focus session.</summary>
    public static readonly LayoutPreset Focus = new()
    {
        Id = FocusId,
        Glyph = WidgetInfos.Focus.Glyph,
        Type = LayoutType.Dock,
        Islands = machine =>
        {
            var bar = new Bar(machine);
            bar.Folder(Presets.FocusId, BarSection.Start);
            bar.Add(BarKind.Start.New(), BarSection.Middle);
            bar.Apps(BarSection.Middle);
            bar.Add(BarKind.Tray.New(), BarSection.End);
            bar.Folder(Presets.QuickPanelId, BarSection.End);
            bar.Folder(Presets.ClockCalendarId, BarSection.End);
            return bar.Done();
        },
    };

    /// <summary>The day: meetings, mail, to-dos and focus in the middle, today at the right.</summary>
    public static readonly LayoutPreset Planner = new()
    {
        Id = PlannerId,
        Glyph = OptionGlyphs.Agenda,
        Islands = machine =>
        {
            var bar = new Bar(machine);
            bar.Add(BarKind.Start.New(), BarSection.Start);
            bar.Add(BarKind.Search.New(), BarSection.Start);
            bar.Apps(BarSection.Start);
            bar.Folder(Presets.MeetingId, BarSection.Middle);
            bar.Folder(Presets.InboxId, BarSection.Middle);
            bar.Folder(Presets.NotebookId, BarSection.Middle);
            bar.Folder(Presets.FocusId, BarSection.Middle);
            bar.Corner();
            bar.Folder(Presets.QuickPanelId, BarSection.End);
            bar.Folder(Presets.TodayId, BarSection.End);
            bar.Add(BarKind.ShowDesktop.New(), BarSection.End);
            return bar.Done();
        },
    };

    /// <summary>One taskbar saying how the PC is doing: its readings beside the tray, the desktops beside Task view.</summary>
    public static readonly LayoutPreset Monitor = new()
    {
        Id = MonitorId,
        Glyph = WidgetInfos.Cpu.Glyph,
        Islands = machine =>
        {
            var bar = new Bar(machine);
            bar.Add(BarKind.Start.New(), BarSection.Start);
            bar.Add(BarKind.Search.New(), BarSection.Start);
            bar.Add(BarKind.TaskView.New(), BarSection.Start);
            bar.Folder(Presets.DesktopsId, BarSection.Start);
            bar.Apps(BarSection.Middle);
            foreach (var reading in (ReadOnlySpan<string>)[Presets.CpuId, Presets.GpuId, Presets.MemoryId, Presets.DiskId, Presets.NetworkActivityId])
            {
                bar.Folder(reading, BarSection.End);
            }
            bar.Folder(Presets.ClipboardId, BarSection.End);
            bar.Corner();
            bar.Folder(Presets.QuickPanelId, BarSection.End);
            bar.Folder(Presets.NotificationCenterId, BarSection.End);
            bar.Add(BarKind.ShowDesktop.New(), BarSection.End);
            return bar.Done();
        },
    };

    /// <summary>Every preset, in the order "Add a layout" lists them: §628 Blank first, then Windows' and Karpo's.</summary>
    public static readonly LayoutPreset[] All = [Blank, Windows, Karpo, Focus, Planner, Monitor];

    /// <summary>The preset <paramref name="id"/> names, or null.</summary>
    public static LayoutPreset? Find(string? id) => All.FirstOrDefault(preset => preset.Id == id);

    /// <summary>
    /// A new layout of <paramref name="preset"/> on <paramref name="machine"/>, called <paramref name="name"/>: its type,
    /// size, icon and widgets, each where it stands, in no config yet; nothing in it says which preset it was.
    /// </summary>
    public static TaskbarLayout Make(LayoutPreset preset, LayoutMachine machine, string name)
    {
        var layout = new TaskbarLayout
        {
            Name = name,
            Icon = preset.Glyph,
            Type = preset.Type,
            Size = preset.Size,
            Islands = preset.Islands(machine),
        };
        Sections.Order(layout.Islands);
        return layout;
    }

    /// <summary>A bar being put together: each widget into its section, in the order given.</summary>
    private sealed class Bar(LayoutMachine machine)
    {
        private readonly List<BarItem> list = [];

        /// <summary><paramref name="island"/> at the end of <paramref name="section"/>.</summary>
        public void Add(BarItem island, BarSection section)
        {
            island.Section = section;
            list.Add(island);
        }

        /// <summary>The ready-made folder <paramref name="id"/>, as the add list makes it (<see cref="Presets.Folder"/>).</summary>
        public void Folder(string id, BarSection section) =>
            Add(Presets.Folder(Presets.Find(id)!, machine.Folders), section);

        /// <summary>The pins of Windows' taskbar and the open apps, one run.</summary>
        public void Apps(BarSection section)
        {
            foreach (var target in machine.Pins)
            {
                var pin = BarKind.Pin.New();
                pin.Pin!.Target = target;
                Add(pin, section);
            }
            Add(BarKind.Apps.New(), section);
        }

        /// <summary>Windows' own buttons as Windows 11 has them: Start, the search box, Task view, the pins and the open apps.</summary>
        public void WindowsButtons(BarSection section)
        {
            Add(BarKind.Start.New(), section);
            var search = BarKind.Search.New();
            search.WindowsSearch = new WindowsSearchOptions { ShowBox = true };
            Add(search, section);
            Add(BarKind.TaskView.New(), section);
            Apps(section);
        }

        /// <summary>The system corner's start, as Windows': the "^" and the tray, the pen and touch buttons where Windows shows them, the status icons.</summary>
        public void Corner()
        {
            Add(BarKind.Tray.New(), BarSection.End);
            foreach (var input in InputButtons.ForNewBar(machine.Inputs))
            {
                Add(input, BarSection.End);
            }
            Add(BarKind.Indicators.New(), BarSection.End);
        }

        public List<BarItem> Done()
        {
            Sections.Order(list);
            return list;
        }
    }
}
