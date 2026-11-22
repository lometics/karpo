// folder contents: a pin and a folder's contents, two widgets (§380; the person, 2026-10-28: "lets split
// folder and pin again. so: a pin always only shows the link to a webpage, application, folder, etc. and
// opens the location on click. a folder always shows the contents of the folder and inherits the
// navigation properties etc. from the pin widget"). §32 had made them one: a pin to a folder set to show
// its contents.
//
//   - A pin (WidgetInfos.Pin) is its target's icon and name, and a click opens the target: an app, a
//     file, a web address, a folder in File Explorer. Nothing else, whatever its target is.
//   - A folder's contents (WidgetInfos.FolderContents) point at their folder with the pin's record
//     (PinOptions: the folder, a name, an icon) and show what it holds, a line of a list. Their
//     row of buttons on the taskbar, whose subfolders opened a list from the bar, went (§492, the
//     person, 2026-11-03: "a remnant that is actually out of date"). Their navigation
//     (PinFolderNavigation) is what a click on a subfolder does: open it in File Explorer, or show its
//     contents in place of its parent's, as deep as the folders go, with a way back (2026-10-29, the
//     person: "the folder widget should also change the contents inline on navigation and not actually
//     open the submenus on the left or the right"). A file always just opens.
//
// Everything here is a rule on plain values, so the dock, the menus and a test ask the one question.

using System.Text.Json.Serialization;

namespace Karpo.Core;

/// <summary>
/// folder contents: what a click on a subfolder does (<see cref="PinOptions.Navigation"/>). A word this
/// build does not know reads as a value outside the enum, which is taken as <see cref="Explorer"/>.
/// </summary>
[JsonConverter(typeof(TolerantEnumConverter<PinFolderNavigation>))]
public enum PinFolderNavigation
{
    /// <summary>Opens the folder in File Explorer: what every pin did before the setting existed.</summary>
    [JsonStringEnumMemberName("explorer")] Explorer,

    /// <summary>What the folder holds, shown in place of its parent's items, as deep as the folders go, with a way back.</summary>
    [JsonStringEnumMemberName("contents")] Contents,
}

/// <summary>
/// folder contents (§498): how a folder's line lays its items out, as File Explorer's View does (the person, 2026-11-03:
/// "the folder contents widget should also get different styles like grid, details etc. (similar to the ones in the file
/// explorer)"). A word this build does not know reads as a value outside the enum, which is taken as <see cref="List"/>.
/// </summary>
[JsonConverter(typeof(TolerantEnumConverter<PinFolderView>))]
public enum PinFolderView
{
    /// <summary>A row an item: its icon, its name and, where the line has room, its date: what every line showed before the setting existed.</summary>
    [JsonStringEnumMemberName("list")] List,

    /// <summary>A row an item under a row of column names: its name, when it last changed, its type and its size, as many as the width holds.</summary>
    [JsonStringEnumMemberName("details")] Details,

    /// <summary>A larger icon beside its name and, under it, its type and its size; as many tiles across as the width holds.</summary>
    [JsonStringEnumMemberName("tiles")] Tiles,

    /// <summary>A grid of icons, each with its name under it.</summary>
    [JsonStringEnumMemberName("mediumIcons")] MediumIcons,

    /// <summary>A grid of large icons, a picture's own thumbnail where the shell has one, each with its name under it.</summary>
    [JsonStringEnumMemberName("largeIcons")] LargeIcons,
}

/// <summary>folder contents: which widget lists a folder, what its settings mean, and what the menus offer.</summary>
public static class PinFolders
{
    /// <summary>The navigations, in the order the menus offer them.</summary>
    public static readonly PinFolderNavigation[] Navigations = [PinFolderNavigation.Explorer, PinFolderNavigation.Contents];

    /// <summary>§498: the views, in the order File Explorer's View menu offers them.</summary>
    public static readonly PinFolderView[] Views = [PinFolderView.LargeIcons, PinFolderView.MediumIcons, PinFolderView.List, PinFolderView.Details, PinFolderView.Tiles];

    /// <summary><paramref name="chosen"/>, or <see cref="PinFolderView.List"/> for a value this build does not know.</summary>
    public static PinFolderView Known(PinFolderView chosen) => Enum.IsDefined(chosen) ? chosen : PinFolderView.List;

    /// <summary>§498: how a folder's line lays out its items: the view a folder's contents chose; the list for anything else.</summary>
    public static PinFolderView ViewOf(WidgetItem? item) =>
        Lists(item) && item!.Pin is { } pin ? Known(pin.View) : PinFolderView.List;

    /// <summary>What the settings call a view: File Explorer's own words for it.</summary>
    public static string Title(this PinFolderView view) => view switch
    {
        PinFolderView.Details => S.SettingsPinViewDetails,
        PinFolderView.Tiles => S.SettingsPinViewTiles,
        PinFolderView.MediumIcons => S.SettingsPinViewMediumIcons,
        PinFolderView.LargeIcons => S.SettingsPinViewLargeIcons,
        _ => S.SettingsPinViewList,
    };

    /// <summary><paramref name="chosen"/>, or <see cref="PinFolderNavigation.Explorer"/> for a value this build does not know.</summary>
    public static PinFolderNavigation Known(PinFolderNavigation chosen) => Enum.IsDefined(chosen) ? chosen : PinFolderNavigation.Explorer;

    /// <summary>Whether <paramref name="item"/> lists what its folder holds: a folder's contents, and never a pin.</summary>
    public static bool Lists(WidgetItem? item) => item?.Info == WidgetInfos.FolderContents;

    /// <summary>
    /// What a click on a subfolder <paramref name="item"/> shows does: the navigation a folder's contents
    /// chose; File Explorer for anything else — a pin's folder always opens there.
    /// </summary>
    public static PinFolderNavigation NavigationOf(WidgetItem? item) =>
        Lists(item) && item!.Pin is { } pin ? Known(pin.Navigation) : PinFolderNavigation.Explorer;

    /// <summary>Whether a click on a folder in <paramref name="navigation"/> shows its contents rather than File Explorer.</summary>
    public static bool ShowsContents(this PinFolderNavigation navigation) => navigation == PinFolderNavigation.Contents;

    /// <summary>What the settings call a navigation, in the words of the choice it is.</summary>
    public static string Title(this PinFolderNavigation navigation) => navigation switch
    {
        PinFolderNavigation.Contents => S.CorePinFolderClickContents,
        _ => S.CorePinFolderClickExplorer,
    };

    /// <summary>
    /// The folder a pin's <paramref name="target"/> names, as a path the file system answers to: its
    /// environment variables expanded and its quotes taken off; empty for a web address, an app or
    /// anything else that is not a path. Whether a folder is there is asked of
    /// <paramref name="exists"/>, so a test needs no disk.
    /// </summary>
    public static string FolderOf(string? target, Func<string, bool> exists) =>
        PathOf(target) is { Length: > 0 } path && exists(path) ? path : "";

    /// <summary>
    /// The path a pin's <paramref name="target"/> names, whatever is there: its environment variables
    /// expanded, its quotes taken off and a bare drive ("D:") its root; empty for a web address or
    /// nothing at all.
    /// </summary>
    public static string PathOf(string? target)
    {
        var path = (target ?? "").Trim().Trim('"');
        if (path.Length == 0 || path.Contains("://", StringComparison.Ordinal))
        {
            return "";
        }
        path = Environment.ExpandEnvironmentVariables(path);
        // A drive's root ("D:") names the drive's current folder to the file system; its root is meant.
        if (path.Length == 2 && path[1] == ':' && char.IsAsciiLetter(path[0]))
        {
            path += "\\";
        }
        return path;
    }

    /// <summary>
    /// Whether a pin's <paramref name="target"/> is a place on a network: a share's path
    /// ("\\server\share\…"), or a path on a drive letter mapped to one, which
    /// <paramref name="remoteDrive"/> is asked of the drive's root ("X:\") — a question the
    /// redirector answers at once. Such a place may make anything that touches it wait for the
    /// network, so the dock never asks it anything on its own thread.
    /// </summary>
    public static bool OnNetwork(string? target, Func<string, bool> remoteDrive)
    {
        var path = PathOf(target);
        if (path.StartsWith(@"\\", StringComparison.Ordinal))
        {
            // "\\?\" and "\\.\" name this PC's own volumes and devices.
            return !path.StartsWith(@"\\?\", StringComparison.Ordinal) && !path.StartsWith(@"\\.\", StringComparison.Ordinal);
        }
        return path.Length >= 3 && path[1] == ':' && path[2] == '\\' && char.IsAsciiLetter(path[0]) && remoteDrive(path[..3]);
    }
}
