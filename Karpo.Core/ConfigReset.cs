// Settings made new again (2026-10-20, docs\widgets.md §266: "reset settings file should also be
// possible"): config.json as a new install of Karpo writes it on this PC, from the settings app's
// About page ("Reset") or Karpo.exe --reset-settings.
//
// A new install's config is what Load makes when there is no file (Config.Defaults): the layout
// Windows 11's own taskbar has, with the apps pinned to it now (Config.FirstLayout), and every
// other setting at its default. Nothing else is read for it, config.json.bak no more than the file.
//
// Two of them (ResetScope): the layouts alone — the bars, their folders and widgets, and which
// layout each display shows and at which edge — every other setting kept; or every setting. Both
// keep whether Karpo starts with Windows, this PC's own choice as an import keeps it, and nothing
// outside config.json is touched: the accounts and their sign-ins (%LOCALAPPDATA%\Karpo\accounts.json,
// docs\accounts.md), the licence, the caches and the tray icons the dock has seen stay.
//
// The file goes in whole, as an import does (Config.Replace): the one there is kept beside it as
// config.json.before-reset-<time>, never over a copy kept before, and the dock takes the new one
// as it does any file replaced whole — the pins a reset takes off are no unpin, so Windows'
// taskbar keeps them (WindowsPins.cs).

namespace Karpo.Core;

/// <summary>What a reset makes new (<see cref="ConfigReset"/>).</summary>
public enum ResetScope
{
    /// <summary>
    /// The layouts, their bars, folders and widgets, and which layout each display shows and at
    /// which edge (<see cref="Config.Layouts"/>, <see cref="Config.DefaultLayoutId"/>,
    /// <see cref="Config.Screens"/>): a new install's. Every other setting stays.
    /// </summary>
    Layouts,

    /// <summary>Every setting in config.json, but whether Karpo starts with Windows.</summary>
    All,
}

/// <summary>config.json made what a new install makes (see the top of the file).</summary>
public static class ConfigReset
{
    /// <summary>
    /// The config a new install of Karpo starts on, on this PC now: what config.json's absence
    /// means (Windows' taskbar and its pins read, the rest the defaults), in the language S speaks.
    /// </summary>
    public static Config FirstInstall() => Config.Defaults();

    /// <summary>
    /// What <paramref name="current"/> becomes when <paramref name="scope"/> is reset, as a new
    /// config (<paramref name="current"/> is left as it is): <paramref name="fresh"/>, a new
    /// install's (<see cref="FirstInstall"/> when null), with what the reset keeps of the current one.
    /// </summary>
    public static Config Reset(Config current, ResetScope scope, Config? fresh = null)
    {
        fresh ??= FirstInstall();
        if (scope == ResetScope.All)
        {
            fresh.StartWithWindows = current.StartWithWindows;
            return fresh;
        }
        var kept = current.Clone();
        kept.Layouts = fresh.Layouts;
        kept.DefaultLayoutId = fresh.DefaultLayoutId;
        kept.Screens = fresh.Screens;
        kept.Repair();
        return kept;
    }

    /// <summary>
    /// Resets <paramref name="scope"/> in config.json: loads it as both apps do and resets that
    /// (<see cref="ResetFile(Config, ResetScope, out string?)"/>). For a script, Karpo.exe
    /// --reset-settings.
    /// </summary>
    public static Config ResetFile(ResetScope scope, out string? backup) => ResetFile(Config.Load(), scope, out backup);

    /// <summary>
    /// Writes <paramref name="current"/>, the config as the file has it, reset (<see cref="Reset"/>)
    /// to config.json in its place. The new install's folders are named in the language the result
    /// speaks — the one chosen, or Windows' after a reset of all — whatever S speaks after: that is
    /// left as it was. The file there was is kept as config.json.before-reset-&lt;time&gt; beside it
    /// (<paramref name="backup"/>, its path; null when there was none). Returns the config as
    /// written. Throws <see cref="IOException"/> when the file can't be written, and for the
    /// layouts alone of a file a newer Karpo wrote, whose settings this build would drop.
    /// </summary>
    public static Config ResetFile(Config current, ResetScope scope, out string? backup)
    {
        if (scope == ResetScope.Layouts && current.FromNewerKarpo)
        {
            throw new IOException(
                $"{Config.FilePath} was written by a newer Karpo (shape {current.Version}, this one knows {Config.Shape}). Update Karpo to reset its layouts.");
        }
        var speaking = Strings.Language;
        Config fresh;
        try
        {
            Languages.Apply(scope == ResetScope.All ? null : current.Language);
            fresh = FirstInstall();
        }
        finally
        {
            Strings.Use(speaking);
        }
        return Config.Replace(Reset(current, scope, fresh), "before-reset", out backup);
    }
}
