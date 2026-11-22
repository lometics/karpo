// edit: ⚙ Settings' Back up and reset and About Karpo, the dock's side (docs\widgets.md §339; the rows are
// Karpo.Core\KarpoMenu.Backup.cs and KarpoMenu.About.cs). An export, an import and a reset run once the menu
// has gone (Later), owned by the bar the menu hung from: Windows' dialogs (SettingsBackup, IBackupDialogs),
// the config written taken as it is, and ⚙ shown again under the gear, open on Back up and reset, how it
// went under the act's row. The file's place opens in File Explorer, config.json selected beside the copies
// an import and a reset keep. About Karpo's addresses open in the browser.

using Karpo.Core;
using TerraFX.Interop.Windows;

namespace Karpo.Dock;

public sealed unsafe partial class Dock
{
    /// <summary>§339: the dialogs Back up and reset shows; a test answers instead.</summary>
    internal IBackupDialogs BackupDialogs { get; set; } = new WindowsBackupDialogs();

    /// <summary>§339: how each of Back up and reset's acts last ended, by its row, while ⚙ is shown again after it.</summary>
    private readonly Dictionary<string, string> backupSaid = [];

    /// <summary>§339: what the Karpo page's dialogs are owned by: the bar of the display whose settings panel is open (§520), else the bar holding edit mode.</summary>
    private HWND SettingsOwner => ((panelOn is { } display ? BarOf(display) : null) ?? editor)?.Hwnd ?? default;

    /// <summary>§339: what Back up and reset does, as ⚙ is built now.</summary>
    private KarpoBackup MenuBackup() => new(
        () => BackupLater(owner => SettingsBackup.Export(config, BackupDialogs, owner, Clock.RealOffset(), Environment.MachineName)),
        () => BackupLater(owner => SettingsBackup.Import(config, BackupDialogs, owner, [.. arranged.Select(display => (display.Id, display.Name))], PinTargets.System)),
        () => Later(() => Launch.Select(Config.FilePath)),
        scope => BackupLater(owner => SettingsBackup.Reset(config, scope, BackupDialogs, owner)))
    {
        FilePath = Config.FilePath,
        Said = new Dictionary<string, string>(backupSaid),
    };

    /// <summary>§339: Karpo itself for About Karpo: this build's version and the runtime (its sites open by KarpoServices.Open).</summary>
    private static KarpoAbout MenuAbout() => new(
        typeof(Dock).Assembly.GetName().Version?.ToString(3) ?? "",
        $".NET {Environment.Version.ToString(3)}");

    /// <summary>
    /// §339: <paramref name="act"/> once the menu has gone, owned by ⚙'s bar: what it wrote taken as the config
    /// is (written already, so not saved again), how it went kept for Back up and reset, and ⚙ shown again on
    /// that level, the person carrying on where they were (as a picker's dialog brings its menu back, §224).
    /// </summary>
    private void BackupLater(Func<HWND, SettingsBackup.Outcome?> act)
    {
        var display = panelOn;
        Later(() =>
        {
            CloseFlyout();
            if (act(SettingsOwner) is { } outcome)
            {
                backupSaid[outcome.Key] = outcome.Said;
                if (outcome.Written is { } written)
                {
                    Apply(written, save: false);
                }
            }
            if (display is null || editor is null)
            {
                return;
            }
            // §520: the panel shows what was said, at Back up and reset's row (KarpoMenu.Backup's first row's key).
            OpenPanel(display, PanelPage.Karpo, KarpoMenu.ExportRow);
        });
    }

    /// <summary>§339: ⚙ opened afresh from the gear: nothing said of an act before.</summary>
    private void ForgetBackupSaid() => backupSaid.Clear();
}

/// <summary>
/// §339: the pictures ⚙'s rows draw by name (OptionRow.Picture): Karpo's logo (assets\logo\karpo-64.png) and
/// Lometics' mark (assets\branding\lometics-128.png, the About page's SVG drawn at 128 px), inside the exe; §337, §597:
/// and the sites' logos a choice of Online names (OptionRow.ChoicePictures, SiteLogo's file, assets\site-logos).
/// </summary>
internal static class MenuPictures
{
    /// <summary>The size they are read at: sharp for a menu's 16 DIP icon up to 400 %.</summary>
    private const int Size = 64;

    private static readonly Lazy<Pixels?> Logo = new(() => Read("Karpo.Dock.Logo.png", Size));
    private static readonly Lazy<Pixels?> Publisher = new(() => Read("Karpo.Dock.Publisher.png", Size));

    // The sites' logos, decoded once each; one at a time (a lock): the tests build the pages on several threads at once, and
    // a logo decoded on two at once came out none.
    private static readonly Dictionary<string, Pixels?> sites = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The picture <paramref name="name"/> names (<see cref="KarpoMenu.LogoPicture"/>, <see cref="KarpoMenu.PublisherPicture"/>,
    /// a site's logo by its file); null for another's, or one that won't read, so the choice wears its glyph.
    /// </summary>
    public static FlyoutIcon? Of(string name) => (name switch
    {
        KarpoMenu.LogoPicture => Logo.Value,
        KarpoMenu.PublisherPicture => Publisher.Value,
        _ => Site(name),
    }) is { } pixels ? new FlyoutIcon.Picture(pixels) : null;

    /// <summary>The site's logo <paramref name="file"/> (SiteLogo's), decoded once at its biggest size.</summary>
    private static Pixels? Site(string file)
    {
        lock (sites)
        {
            if (!sites.TryGetValue(file, out var pixels))
            {
                pixels = Read(SiteLogo.Folder + "." + file, SiteLogo.Sizes[^1]);
                sites[file] = pixels;
            }
            return pixels;
        }
    }

    private static Pixels? Read(string resource, int size)
    {
        using var stream = typeof(MenuPictures).Assembly.GetManifestResourceStream(resource);
        if (stream is null)
        {
            return null;
        }
        using var bytes = new MemoryStream();
        stream.CopyTo(bytes);
        return Favicons.FaviconImage.Decode(bytes.ToArray(), size);
    }
}
