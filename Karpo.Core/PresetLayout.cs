// A layout holding every ready-made group (2026-10-11: "make a layout with all the premade groups,
// then i will rearrange them how i want them to look and then you can take that as the default
// again"). Karpo.exe --add-preset-layout writes it into the person's config.json, through the same
// load and save as both apps (ConfigFile.cs), so a running dock merges it in on its next reload.
//
// It is a new bar's (Config.FirstLayout): its type, size and show, and its own buttons — Start, the
// pins, the open apps, the system icons, the keyboard and show desktop — without the groups a new
// bar wears (quick settings, the clock …), which come again with the others. Then every
// group in Presets.All, each added as the add list adds it (Presets.AddToBar), so its face, its
// widgets and their options are exactly what a pick makes. Where each stands is the person's
// arrangement of it (2026-10-11: "i changed the layouts on the layout page already. so take them
// over to the presets"): in library order, the clock to the power button after the open apps, the
// mail to the network drive in the middle, the notifications to the notes at the end, then
// the system icons and the keyboard, now playing, show desktop; no search box, Task view or tray,
// which they took out. The battery's only on a PC with one, as a new bar's. It is shown by no display, and other layouts are left alone; made
// again, it takes the place of the one made before (its id, PresetLayout.Id), wherever it stands.

namespace Karpo.Core;

/// <summary>The layout of every ready-made group (see the top of the file).</summary>
public static class PresetLayout
{
    /// <summary>Its id: what a second run finds to replace.</summary>
    public const string Id = "allPresets";

    /// <summary>Its name, in the language Karpo speaks now.</summary>
    public static string Name => S.CoreLayoutPresetsName;

    /// <summary>The presets it holds on <paramref name="machine"/>, in library order: all of them, the battery's only where there is one.</summary>
    public static IReadOnlyList<Preset> On(PresetMachine machine) =>
        [.. Presets.All.Where(preset => machine.HasBattery || preset.Id != Presets.BatteryId)];

    /// <summary>
    /// Puts the layout into <paramref name="config"/>: in the place of the one made before (by its
    /// id, or by its name where an older one has another id), else at the end of the layouts. No
    /// display is switched to it. Returns it.
    /// </summary>
    public static TaskbarLayout Build(Config config, PresetMachine machine)
    {
        config.Layouts ??= [];
        var layout = Make(machine);
        var index = config.Layouts.FindIndex(old => old?.Id == Id);
        if (index < 0)
        {
            index = config.Layouts.FindIndex(old => old is not null && string.Equals(old.Name, layout.Name, StringComparison.Ordinal));
        }
        if (index < 0)
        {
            config.Layouts.Add(layout);
            return layout;
        }
        // Under the old one's id, so a display that shows it still does.
        layout.Id = config.Layouts[index].Id;
        config.Layouts[index] = layout;
        return layout;
    }

    /// <summary>
    /// The section <paramref name="preset"/> stands in, as the person arranged the layout
    /// (2026-10-11): the start's up to the mail, the middle's from the mail to the notifications,
    /// the end's from the notifications on. A preset added later falls where its place in the
    /// library puts it.
    /// </summary>
    public static BarSection SectionOf(Preset preset)
    {
        var at = Array.IndexOf(Presets.All, preset);
        return at < Array.FindIndex(Presets.All, one => one.Id == Presets.MailId) ? BarSection.Start
            : at < Array.FindIndex(Presets.All, one => one.Id == Presets.NotificationsId) ? BarSection.Middle
            : BarSection.End;
    }

    /// <summary>The layout alone, not yet in any config.</summary>
    public static TaskbarLayout Make(PresetMachine machine)
    {
        var layout = Config.FirstLayout();
        layout.Id = Id;
        layout.Name = Name;
        // A new bar's own buttons, but for those the person took out of theirs (2026-10-11): the
        // search box, Task view and the tray; its groups come again below, with every other.
        var list = layout.Islands;
        list.RemoveAll(island => island.Folder is not null
            || island.Kind is BarKind.Search or BarKind.TaskView or BarKind.Tray);
        // Start, the pins and the open apps in the start, where the person has them.
        foreach (var island in list.Where(island => island.Kind is BarKind.Start or BarKind.Pin or BarKind.Apps))
        {
            island.Section = BarSection.Start;
        }
        Sections.Order(list);
        var presets = On(machine);
        var end = 0;
        foreach (var preset in presets.Where(preset => preset.Id is not (Presets.MediaId or Presets.KeyboardId)))
        {
            var section = SectionOf(preset);
            var range = Sections.Range(list, section);
            // The start's and the middle's after what they hold; the end's before the system icons, in order.
            var index = section == BarSection.End ? range.From + end++ : range.To;
            Presets.AddToBar(list, new BarSlot(section, index), preset, machine);
        }
        if (presets.FirstOrDefault(preset => preset.Id == Presets.KeyboardId) is { } keyboard)
        {
            // The input indicator right after the system icons, where the person has it and Windows
            // keeps it.
            var range = Sections.Range(list, BarSection.End);
            var icons = list.FindIndex(range.From, range.To - range.From, island => island.Kind == BarKind.Indicators);
            Presets.AddToBar(list, new BarSlot(BarSection.End, icons < 0 ? range.To : icons + 1), keyboard, machine);
        }
        if (presets.FirstOrDefault(preset => preset.Id == Presets.MediaId) is { } media)
        {
            // What's playing beside the keyboard, last before show desktop, where the person put it.
            var range = Sections.Range(list, BarSection.End);
            var desktop = list.FindIndex(range.From, range.To - range.From, island => island.Kind == BarKind.ShowDesktop);
            Presets.AddToBar(list, new BarSlot(BarSection.End, desktop < 0 ? range.To : desktop), media, machine);
        }
        Sections.Order(list);
        return layout;
    }

    /// <summary>
    /// Karpo.exe --add-preset-layout: loads config.json as both apps do, puts the layout in
    /// (<see cref="Build"/>) and saves, merged with whatever else wrote the file. Returns it.
    /// Throws <see cref="IOException"/> when the file can't be written.
    /// </summary>
    public static TaskbarLayout AddToFile(PresetMachine machine)
    {
        var config = Config.Load();
        // Its name and its folders' in the language the person chose, as the dock would write them.
        Languages.Apply(config.Language);
        var layout = Build(config, machine);
        config.Save();
        return layout;
    }
}
