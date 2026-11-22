// Files an earlier Karpo wrote that nothing reads any more: let go of at the start, so a PC that ran
// Karpo for a year does not keep what its features left behind. A file is named here when the code that
// wrote it goes; one already gone is no error. Only names Karpo itself gave — never a copy the person
// made, never a backup of their settings.

namespace Karpo.Core;

public static class RetiredFiles
{
    /// <summary>
    /// In the local folder (%LOCALAPPDATA%\Karpo): the stocks' cache before the markets took over from it;
    /// the taskbar's state from before Karpo, which nothing keeps since Windows' taskbar is shown again
    /// as it is, not as it was (§465).
    /// </summary>
    internal static readonly string[] Local = ["stocks.json", "stocks.json.bak", "taskbar-state"];

    /// <summary>In the config's folder (%APPDATA%\Karpo): the log of the settings app, which is gone (§340).</summary>
    internal static readonly string[] Roaming = ["settings-errors.log"];

    /// <summary>Deletes every retired file there is; what can't be deleted now is tried at the next start.</summary>
    public static void Sweep()
    {
        Sweep(Config.LocalFolder, Local);
        Sweep(Config.Folder, Roaming);
    }

    internal static void Sweep(string folder, IEnumerable<string> names)
    {
        foreach (var name in names)
        {
            try
            {
                File.Delete(Path.Combine(folder, name));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
            }
        }
    }
}
