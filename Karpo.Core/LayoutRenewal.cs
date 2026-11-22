// A layout made new again (2026-10-11: Karpo.exe --renew-layout <layoutId>): what a first install
// would give it (Config.FirstLayout, Windows 11's taskbar with the presets' folders), read on this
// PC as a new install reads it, under the layout's own id, name and icon, so every display that
// shows it still does. Its pins stay the person's: the pins it holds, as they are and in their
// order, where a new bar's pins go (before the open apps), not Windows' taskbar's read again.
// Nothing else in config.json changes. The file as it was is copied to
// config.before-renew-<id>.json beside it first; then it goes through the same load and save as
// both apps (ConfigFile.cs), so a running dock merges it in on its next reload.

namespace Karpo.Core;

/// <summary>A layout made what a first install makes (see the top of the file).</summary>
public static class LayoutRenewal
{
    /// <summary>What a renewal did, for the command line to say.</summary>
    public sealed record Renewed(TaskbarLayout Layout, int ItemsBefore, int PinsKept, string Backup);

    /// <summary>
    /// Puts <paramref name="first"/> — a first install's layout, <see cref="Config.FirstLayout"/>
    /// when null — in the place of the layout <paramref name="layoutId"/>, under its id, name and
    /// icon, holding its pins instead of the new one's. Returns the new layout, or null when
    /// <paramref name="config"/> has no such layout (and then changes nothing).
    /// </summary>
    public static TaskbarLayout? Renew(Config config, string layoutId, TaskbarLayout? first = null)
    {
        config.Layouts ??= [];
        var index = config.Layouts.FindIndex(layout => layout is not null && layout.Id == layoutId);
        if (index < 0)
        {
            return null;
        }
        var old = config.Layouts[index];
        var layout = first ?? Config.FirstLayout();
        layout.Id = old.Id;
        layout.Name = old.Name;
        layout.Icon = old.Icon;
        var list = layout.Islands;
        // The pins a first install would read from Windows' taskbar give way to the layout's own.
        var at = list.FindIndex(island => island.Kind is BarKind.Pin or BarKind.Apps);
        list.RemoveAll(island => island.Kind == BarKind.Pin);
        var pins = old.Islands.Where(island => island is not null && island.Kind == BarKind.Pin).Select(island => island.Clone()).ToList();
        // Where a new bar has them: before the open apps, in the open apps' section.
        var apps = list.Find(island => island.Kind == BarKind.Apps);
        foreach (var pin in pins)
        {
            pin.Section = apps?.Section ?? BarSection.Middle;
        }
        list.InsertRange(at < 0 ? list.Count : Math.Min(at, list.Count), pins);
        Sections.Order(list);
        config.Layouts[index] = layout;
        return layout;
    }

    /// <summary>
    /// The file config.json is copied to before the layout <paramref name="layoutId"/> is renewed:
    /// config.before-renew-&lt;id&gt;.json, or, where a renewal before kept one, the same with the
    /// time after the id, so the first copy (the layout as the person made it) is never written over.
    /// </summary>
    public static string BackupPath(string layoutId, DateTime now)
    {
        var name = string.Concat(layoutId.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        var path = Path.Combine(Config.Folder, $"config.before-renew-{name}.json");
        return File.Exists(path) ? Path.Combine(Config.Folder, $"config.before-renew-{name}-{now:yyyyMMdd-HHmmss}.json") : path;
    }

    /// <summary>
    /// Karpo.exe --renew-layout: loads config.json as both apps do, and when it has the layout
    /// <paramref name="layoutId"/>, copies the file to <see cref="BackupPath"/>, renews the layout (<see cref="Renew"/>) and saves, merged with whatever
    /// else wrote the file. Throws <see cref="KeyNotFoundException"/> for a layout the file doesn't
    /// have, with nothing written, and <see cref="IOException"/> when the file can't be written.
    /// </summary>
    public static Renewed RenewInFile(string layoutId)
    {
        if (!File.Exists(Config.FilePath))
        {
            throw new FileNotFoundException($"there is no {Config.FilePath}");
        }
        var config = Config.Load();
        // The folders' names in the language the person chose, as the dock would write them.
        Languages.Apply(config.Language);
        var old = config.Layout(layoutId);
        if (old is null)
        {
            var known = string.Join(", ", config.Layouts.Where(layout => layout is not null).Select(layout => $"{layout.Id} (\"{layout.Name}\")"));
            throw new KeyNotFoundException($"config.json has no layout \"{layoutId}\"; it has {known}");
        }
        var before = old.Islands.Count(island => island is not null);
        var backup = BackupPath(layoutId, DateTime.Now);
        File.Copy(Config.FilePath, backup, overwrite: true);
        var layout = Renew(config, layoutId)!;
        config.Save();
        return new Renewed(layout, before, layout.Islands.Count(island => island.Kind == BarKind.Pin), backup);
    }
}
