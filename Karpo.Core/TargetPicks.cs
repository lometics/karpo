// What Windows' open dialog hands back as a pin's or a shortcut's target, file and folder alike.
//
// The person: "for the pins, there should not be a difference between picking a file or a folder.
// there should not be multiple options if that is possible to do." So there is one "Browse…", and
// its dialog (TargetDialog) picks either: Open takes the file that is chosen, as it always does,
// and a "Select folder" button beside it takes a folder — the one selected in the list, or, with
// none selected, the folder the dialog is in, as Windows' own folder dialog does. Open on a folder
// still goes into it: the dialog can't tell that Open from a double click, which must go in.
//
// Everything here is a rule on plain values: whether a path is a folder is handed in, never looked
// up here, so the dialog, the dock and a test ask the same question.

namespace Karpo.Core;

/// <summary>How the open dialog was closed.</summary>
public enum TargetPickEnd
{
    /// <summary>Cancel, Escape, the close box, or a dialog that couldn't be shown.</summary>
    Cancelled,

    /// <summary>Open, on a file (or a file name typed in its box).</summary>
    Opened,

    /// <summary>"Select folder": the folder selected in the list, or the one the dialog is in.</summary>
    Folder,
}

/// <summary>The target an open dialog's answer stands for, and where the dialog starts.</summary>
public static class TargetPicks
{
    /// <summary>What the dialog's own button says: Windows' folder dialog calls its button that too.</summary>
    public static string FolderButton => S.CorePickSelectFolder;

    /// <summary>
    /// The folder "Select folder" takes: <paramref name="selected"/> when it is a folder, else the
    /// folder the dialog is in (<paramref name="current"/>); null when neither is a folder on disk
    /// (This PC, Quick access, a library), when the button has nothing to take and stays grey.
    /// Each is a file-system path or null; whether it is a folder is asked of <paramref name="isFolder"/>.
    /// </summary>
    public static string? Folder(string? selected, string? current, Func<string, bool> isFolder)
    {
        if (Given(selected) is { } chosen && isFolder(chosen))
        {
            return chosen;
        }
        return Given(current) is { } here && isFolder(here) ? here : null;
    }

    /// <summary>
    /// The target the dialog picked, or null when nothing was: <paramref name="opened"/> for Open,
    /// <see cref="Folder"/> for "Select folder", and nothing for a cancel.
    /// </summary>
    public static string? Picked(TargetPickEnd end, string? opened, string? selected, string? current, Func<string, bool> isFolder) => end switch
    {
        TargetPickEnd.Opened => Given(opened),
        TargetPickEnd.Folder => Folder(selected, current, isFolder),
        _ => null,
    };

    /// <summary>
    /// The folder the dialog opens in for a target that is <paramref name="near"/> (what the pin
    /// opens now, or what was typed): the folder it stands in, or the nearest one above it this PC
    /// still has, so "Locate…" on a moved file starts as close to it as can be. Null for a web
    /// address, an app, an empty box or a drive that isn't there, where the dialog starts where it
    /// last was. Whether a folder is there is asked of <paramref name="exists"/>.
    /// </summary>
    public static string? StartIn(string? near, Func<string, bool> exists)
    {
        var path = (near ?? "").Trim().Trim('"');
        if (path.Length == 0 || path.Contains("://", StringComparison.Ordinal) || path.StartsWith("shell:", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        path = Environment.ExpandEnvironmentVariables(path);
        if (!Path.IsPathFullyQualified(path))
        {
            return null;
        }
        // "C:\Folder\" is the folder, not something in it: the slash goes before asking for its parent.
        path = Trimmed(path);
        while (Path.GetDirectoryName(path) is { Length: > 0 } parent)
        {
            if (exists(parent))
            {
                return parent;
            }
            path = parent;
        }
        return null;
    }

    /// <summary>A path with its blanks taken off; null for none.</summary>
    static string? Given(string? path) => path?.Trim() is { Length: > 0 } trimmed ? trimmed : null;

    /// <summary><paramref name="path"/> without trailing slashes, a drive's root ("C:\") and a share's ("\\server\share\") aside.</summary>
    static string Trimmed(string path)
    {
        var root = Path.GetPathRoot(path) ?? "";
        while (path.Length > root.Length && path[^1] is '\\' or '/')
        {
            path = path[..^1];
        }
        return path;
    }
}
