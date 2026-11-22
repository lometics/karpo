namespace Karpo.Tests.Persistence;

/// <summary>The test run's config folder (see Isolation) for the save and recovery tests.</summary>
internal static class Files
{
    /// <summary>
    /// config.json and everything saving leaves beside it, gone: the backup, kept broken files,
    /// temporary files, and the files of older shapes a load kept aside.
    /// </summary>
    public static void Reset()
    {
        Directory.CreateDirectory(Config.Folder);
        foreach (var file in Directory.GetFiles(Config.Folder, "config.json*").Concat(Directory.GetFiles(Config.Folder, "config.shape*.json")))
        {
            File.Delete(file);
        }
    }

    /// <summary>A starting config.json, as a save of Karpo's own writes it.</summary>
    public static Config Start(Action<Config>? edit = null)
    {
        var config = Config.Parse("{}");
        config.Repair();
        edit?.Invoke(config);
        config.Save();
        return Config.Load();
    }

    /// <summary>The targets of the pins on the layout new displays get, in their order: what these tests add and take away.</summary>
    public static List<string> Pins(Config config) =>
        [.. config.DefaultLayout.Islands.Where(island => island.Kind == BarKind.Pin).Select(island => island.Pin!.Target)];

    /// <summary>
    /// §347: the tray icons' states of the layout new displays get, its tray's own: a dictionary each writer adds its own key to.
    /// </summary>
    public static Dictionary<string, TrayState> TrayStates(Config config) =>
        (config.DefaultLayout.Islands.First(island => island.Kind == BarKind.Tray).TrayIcons ??= new()).States;

    /// <summary>
    /// Pins <paramref name="target"/> on the layout new displays get: after everything on it, or before
    /// everything with <paramref name="first"/>. Its id is made from the target, as both apps would make it.
    /// </summary>
    public static void AddPin(Config config, string target, bool first = false)
    {
        var list = config.DefaultLayout.Islands;
        list.Insert(first ? 0 : list.Count, Pin(target));
    }

    /// <summary>Unpins <paramref name="target"/> from that layout.</summary>
    public static void RemovePin(Config config, string target) =>
        config.DefaultLayout.Islands.RemoveAll(island => island.Kind == BarKind.Pin && island.Pin!.Target == target);

    /// <summary>A folder <paramref name="id"/> holding a pin of <paramref name="member"/>, after everything on that layout.</summary>
    public static void AddFolder(Config config, string id, string member)
    {
        var folder = BarKind.FolderList.New();
        folder.Id = id;
        Karpo.Core.Folders.Add(folder.Kind, folder.Folder, Pin(member).AsCell());
        config.DefaultLayout.Islands.Add(folder);
    }

    /// <summary>The folder <paramref name="id"/> of that layout, or null.</summary>
    public static FolderOptions? Folder(Config config, string id) => Karpo.Core.Folders.FolderIn(config.DefaultLayout.Islands, id);

    private static BarItem Pin(string target)
    {
        var pin = BarKind.Pin.New();
        // FNV-1a over the target: string.GetHashCode is seeded per process.
        pin.Id = target.ToLowerInvariant().Aggregate(0x811c_9dc5u, (hash, c) => unchecked((hash ^ c) * 0x0100_0193u))
            .ToString("x8", System.Globalization.CultureInfo.InvariantCulture);
        pin.Pin!.Target = target;
        return pin;
    }

    public static string Text() => File.ReadAllText(Config.FilePath);

    public static string[] Broken() => Directory.GetFiles(Config.Folder, "config.json.broken-*");

    public static string[] Temporary() => Directory.GetFiles(Config.Folder, "config.json.*.tmp");
}
