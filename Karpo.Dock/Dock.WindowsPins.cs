// pins: Windows' own taskbar pins kept in step with Karpo's (backlog 2). The rules are
// Karpo.Core\WindowsPins.cs (WindowsPinSync); this is the dock's part:
//
// - It listens: Explorer's Taskband key, where its pins are kept, and the folder of their
//   shortcuts, both watched off this thread; either posts WM_KARPO_PINS, and the pins are read
//   once things have been quiet for WINDOWS_PINS_MS, since one pin is several writes.
// - A pin Windows gained goes into the layouts the bars show now, after the last pin on each one's
//   main bar (Apply, as any edit: one save, one rebuild). Not while the config stands in for a
//   file that couldn't be read (Config.StandsIn): that waits for the person's own config.
// - An edit the person made through Karpo — on the bar, or in the settings app — lets go of the
//   pins no layout holds any more (Apply → WindowsPinSync.Release). Nothing else does: not a hand
//   edit of the file, not the file deleted, put back or recovered, not defaults run on while it
//   couldn't be read (Config.StandsIn), and not a config that replaced the file whole (an import,
//   ConfigFile.Replace), which is somebody else's PC's pins. A settings app's edit is told from
//   the rest by the log Karpo's own saves keep (Config.KarposOwnEdit).
// - What Windows had is remembered across restarts (windows-pins.txt in the local folder,
//   WindowsPinMemory), so a pin added while Karpo wasn't running is found at the next start.
//
// None of it runs on an isolated config (the tests, --smoke): they never read or touch the
// person's pins.

using Karpo.Core;
using Karpo.Dock.QuickSettings.Services;
using Microsoft.Win32;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

public sealed unsafe partial class Dock
{
    /// <summary>pins: Explorer's pins or their folder changed (posted from a pool thread).</summary>
    public const uint WM_KARPO_PINS = WM.WM_APP + 0x29;

    /// <summary>pins: the host's timer that reads Windows' pins once their writes have settled.</summary>
    private const uint WINDOWS_PINS = 44;

    private const uint WINDOWS_PINS_MS = 600;

    /// <summary>pins: the first look after the start, once the bars stand, for pins added while Karpo was not running.</summary>
    private const uint WINDOWS_PINS_FIRST_MS = 3000;

    private WindowsPinSync? windowsPins;
    private RegistryWatch? windowsPinsKey;
    private FileSystemWatcher? windowsPinsFolder;

    /// <summary>When the file was last replaced whole as far as this dock knows (<see cref="Config.ReplacedAt"/>).</summary>
    private DateTime replacedSeen = Config.ReplacedAt();

    /// <summary>Set while a config another app saved is taken: it releases pins only when it is an edit made through Karpo, and didn't replace the file whole.</summary>
    private bool takingTheirs;

    /// <summary>Whether the config <see cref="takingTheirs"/> is taking may release pins.</summary>
    private bool theirsReleases;

    private static string RememberedPinsPath => Path.Combine(Config.LocalFolder, "windows-pins.txt");

    /// <summary>pins: starts following Windows' pins, unless this Karpo runs on a config of its own.</summary>
    private void WatchWindowsPins()
    {
        if (Config.IsIsolated || windowsPins is not null)
        {
            return;
        }
        windowsPins = new WindowsPinSync(WindowsTaskbarPins.This, WindowsPinShell.This, PinKeysOf, WindowsPinMemory.Read(RememberedPinsPath));
        SaveRememberedPins();
        var target = host;
        void Heard() => _ = PostMessageW(target, WM_KARPO_PINS, 0, 0);
        windowsPinsKey = RegistryWatch.Open(RegistryHive.CurrentUser, WindowsTaskbarPins.TaskbandKey, subtree: false, Heard);
        try
        {
            if (Directory.Exists(WindowsTaskbarPins.Folder))
            {
                windowsPinsFolder = new FileSystemWatcher(WindowsTaskbarPins.Folder, "*.lnk")
                {
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                };
                windowsPinsFolder.Created += (_, _) => Heard();
                windowsPinsFolder.Deleted += (_, _) => Heard();
                windowsPinsFolder.Renamed += (_, _) => Heard();
                windowsPinsFolder.EnableRaisingEvents = true;
            }
        }
        catch (Exception error) when (error is IOException or ArgumentException or UnauthorizedAccessException)
        {
            // The key alone still says when a pin comes or goes.
            windowsPinsFolder?.Dispose();
            windowsPinsFolder = null;
        }
        Arm(WINDOWS_PINS, WINDOWS_PINS_FIRST_MS);
    }

    /// <summary>pins: a change heard; read once it has settled.</summary>
    private void WindowsPinsHeard()
    {
        if (windowsPins is not null)
        {
            Arm(WINDOWS_PINS, WINDOWS_PINS_MS);
        }
    }

    /// <summary>pins: Windows' pins read again, and the ones it gained put on the layouts the bars show.</summary>
    private void WindowsPinsDue()
    {
        Kill(WINDOWS_PINS);
        if (windowsPins is not { } sync)
        {
            return;
        }
        // Every layout a display shows now, once: two displays showing one layout add it once (AddTo). One config, so one save and one rebuild for however
        // many pins and layouts.
        var shown = windows.Select(window => config.ActiveLayout(window.Screen.Id).Id).Distinct().ToList();
        var next = sync.Follow(config, shown);
        SaveRememberedPins();
        if (next is not null)
        {
            Apply(next);
        }
    }

    /// <summary>
    /// pins: after the config went from <paramref name="before"/> to the one in force, Windows lets
    /// go of the pins no layout holds any more — for an edit made here, or one the settings app
    /// made (<see cref="TakeTheirs"/>), and for nothing else.
    /// </summary>
    private void ReleaseWindowsPins(Config before)
    {
        if (windowsPins is null)
        {
            return;
        }
        // Defaults for now gave way to the person's own file: what Windows gained meanwhile was left
        // unread (WindowsPinSync.Follow), and is read now.
        if (before.StandsIn && !config.StandsIn)
        {
            WindowsPinsHeard();
        }
        if (windowsPins is not { } sync || takingTheirs && !theirsReleases)
        {
            return;
        }
        // Explorer writes its key once a pin is gone, which reads it again (WindowsPinsHeard).
        _ = sync.Release(before, config);
    }

    /// <summary>
    /// pins: takes <paramref name="fresh"/>, a config another app saved that a reload found
    /// (<see cref="ConfigReload.Changed"/>). It releases pins only when it is an edit made through
    /// Karpo (<see cref="Config.KarposOwnEdit"/>) and didn't replace the file whole since the dock
    /// last took one that did (<see cref="Config.ReplacedFor"/>: the mark is used up here, by the
    /// reload that takes a config, never by the copy an import keeps beside the file first, nor by
    /// a reload that met the file Busy or Broken).
    /// </summary>
    private void TakeTheirs(Config fresh)
    {
        var replaced = Config.ReplacedFor(ConfigReload.Changed, ref replacedSeen);
        takingTheirs = true;
        theirsReleases = fresh.KarposOwnEdit && !replaced;
        try
        {
            Apply(fresh, save: false);
        }
        finally
        {
            takingTheirs = false;
            theirsReleases = false;
        }
    }

    /// <summary>pins: the identities a pin target's app answers to, as the bar resolves it (PinItems).</summary>
    private static IReadOnlyCollection<string> PinKeysOf(string target) =>
        PinItems.For(new PinOptions { Target = target }).Keys;

    /// <summary>pins: what Windows has, as last seen whole, kept for the next start; nothing until it has been seen whole.</summary>
    private void SaveRememberedPins()
    {
        if (windowsPins is not { HasSeen: true } sync)
        {
            return;
        }
        try
        {
            WindowsPinMemory.Write(RememberedPinsPath, sync.Seen);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Report(error);
        }
    }

    private void StopWatchingWindowsPins()
    {
        windowsPinsKey?.Dispose();
        windowsPinsKey = null;
        windowsPinsFolder?.Dispose();
        windowsPinsFolder = null;
        windowsPins = null;
    }
}
