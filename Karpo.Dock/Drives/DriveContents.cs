// What a drive's line says, apart from how it is drawn: the volume's own
// figures (how much of it is used, its file system, whether BitLocker guards
// it), and the order and the words of a folder's or a drive's top level,
// folders first and then files, which a drive's contents and a folder pin
// show (FolderContents, FolderContentsView). The drive's flyout said all of it until
// it went (docs\widgets.md §99).
//
// Everything in this file is plain data and pure functions — the reading
// itself is DriveReader's and FolderReader's — so the order, the bound and the
// units can be checked without a window, a disk or a drive that is nearly full.

using Karpo.Core;
using Karpo.Dock.Panels;

namespace Karpo.Dock.Drives;

/// <summary>
/// One thing in a drive's root. <paramref name="Size"/> is 0 for a folder (the
/// dock never adds up what a folder holds: that would walk the whole disk); the
/// list does not show it — the enumeration hands it over anyway, and the column
/// it used to fill only took room from the names.
/// </summary>
/// <param name="Modified">When it last changed, in local time; <c>default</c> when it could not be read.</param>
internal readonly record struct DriveEntry(string Name, bool Folder, ulong Size, DateTime Modified);

/// <summary>
/// The order, the bound and the icon keys of a drive's top level, and the words
/// the header and the list's messages are written in. Pure: the same inputs
/// always give the same text, whatever disk this is.
/// </summary>
internal static class DriveListing
{
    /// <summary>
    /// From this much of a drive used, its bar turns to the alarm colour, as the bar on the dock
    /// does and as Explorer does at a tenth left.
    /// </summary>
    public const float FullAt = 0.9f;

    /// <summary>The icon key of a folder: one icon stands for every plain folder in the root.</summary>
    public const string FolderIcon = "\\";

    /// <summary>The icon key of a file with nothing to tell one kind from another.</summary>
    public const string PlainIcon = "?";

    /// <summary>
    /// Files whose icon is their own rather than their kind's: programs, shortcuts and icon files
    /// carry a picture inside them, so each is asked for separately.
    /// </summary>
    private static readonly string[] OwnIcon = [".exe", ".lnk", ".ico", ".cur", ".ani", ".url", ".msc", ".cpl", ".scr", ".appref-ms"];

    /// <summary>How full the drive is, 0 to 1; 0 when its size is not known.</summary>
    public static float Fraction(ulong total, ulong free) =>
        total == 0 ? 0 : (float)((double)(total - Math.Min(free, total)) / total);

    /// <summary>Whether the drive is nearly full: its bar goes red and what is free is called out.</summary>
    public static bool NearlyFull(ulong total, ulong free) => Fraction(total, free) > FullAt;

    /// <summary>What is used of the whole, in the whole's unit: "512 of 931 GB used".</summary>
    public static string UsedOfTotal(ulong total, ulong free) =>
        S.DockDrivesUsed(Units.Of(total - Math.Min(free, total), total));

    /// <summary>What is left: "419 GB free".</summary>
    public static string FreeSpace(ulong total, ulong free) => S.DockDrivesFree(Units.Size(Math.Min(free, total)));

    /// <summary>"55%" of the drive used, for the bar's own label.</summary>
    public static string PercentUsed(ulong total, ulong free) => Units.Percent(Fraction(total, free));

    /// <summary>What Windows calls this kind of drive.</summary>
    public static string TypeName(DriveKind kind) => DriveTargets.TypeName(kind);

    /// <summary>The header's second line: the kind of drive, its file system and its BitLocker state, those that are known.</summary>
    public static string Subtitle(DriveKind kind, string fileSystem, string bitLocker)
    {
        var parts = new List<string>(3) { TypeName(kind) };
        if (fileSystem.Length > 0)
        {
            parts.Add(fileSystem);
        }
        if (bitLocker.Length > 0)
        {
            parts.Add(bitLocker);
        }
        return string.Join(" · ", parts);
    }

    /// <summary>"Local Disk (C:)", as Explorer names a drive.</summary>
    public static string Title(string label, string letter) => DriveTargets.Title(label, letter);

    /// <summary>Instead of the figures, why a drive has none: no disc in it, or a share out of reach.</summary>
    public static string NotReadyLine(DriveKind kind) => kind switch
    {
        DriveKind.Remote => S.DockDrivesUnreachable,
        DriveKind.Optical => S.DockDrivesNoDisc,
        DriveKind.Removable => S.DockDrivesNoMedia,
        _ => S.DockDrivesNotReady,
    };

    /// <summary>When an entry last changed, in the region's short date; empty when it is not known.</summary>
    public static string ModifiedOf(in DriveEntry entry) => entry.Modified == default
        ? ""
        : Nls.ShortDate(DateOnly.FromDateTime(entry.Modified));

    /// <summary>
    /// Which icon a row draws: one for every plain folder, the file's own path for the kinds that
    /// carry a picture, else its extension, so one question to the shell serves every file of a kind.
    /// </summary>
    public static string IconKey(in DriveEntry entry)
    {
        if (entry.Folder)
        {
            return FolderIcon;
        }
        var dot = entry.Name.LastIndexOf('.');
        // A name that is nothing but an extension (".gitignore") has none, as Explorer reads it.
        var extension = dot > 0 ? entry.Name[dot..].ToLowerInvariant() : "";
        if (extension.Length == 0)
        {
            return PlainIcon;
        }
        return Array.IndexOf(OwnIcon, extension) >= 0 ? entry.Name.ToLowerInvariant() : extension;
    }

    /// <summary>
    /// Explorer's order: folders before files, and within each, names as a person reads them —
    /// case is ignored and a run of digits counts as the number it is, so "File 2" comes before
    /// "File 10". Names that differ only in case keep a fixed order so sorting is stable.
    /// </summary>
    public static int Compare(in DriveEntry left, in DriveEntry right)
    {
        if (left.Folder != right.Folder)
        {
            return left.Folder ? -1 : 1;
        }
        var order = NaturalCompare(left.Name, right.Name);
        return order != 0 ? order : string.CompareOrdinal(left.Name, right.Name);
    }

    /// <summary><paramref name="entries"/> in the order the flyout shows them (<see cref="Compare"/>).</summary>
    public static List<DriveEntry> Sort(IEnumerable<DriveEntry> entries)
    {
        var sorted = new List<DriveEntry>(entries);
        sorted.Sort(static (left, right) => Compare(left, right));
        return sorted;
    }

    /// <summary>
    /// The first <paramref name="most"/> of <paramref name="entries"/>, and how many are past them, counted and
    /// never said. <paramref name="beyond"/> counts entries the reader itself stopped short of listing.
    /// </summary>
    public static (List<DriveEntry> Shown, int Unlisted) Cap(IReadOnlyList<DriveEntry> entries, int most, int beyond = 0)
    {
        most = Math.Max(0, most);
        var shown = new List<DriveEntry>(Math.Min(entries.Count, most));
        for (var i = 0; i < entries.Count && i < most; i++)
        {
            shown.Add(entries[i]);
        }
        return (shown, entries.Count - shown.Count + Math.Max(0, beyond));
    }

    /// <summary>
    /// Two names as a person would order them: digits by their value, everything else by its letter
    /// with case ignored. Zero when they read the same.
    /// </summary>
    internal static int NaturalCompare(string left, string right)
    {
        int a = 0;
        int b = 0;
        while (a < left.Length && b < right.Length)
        {
            if (char.IsAsciiDigit(left[a]) && char.IsAsciiDigit(right[b]))
            {
                // Leading zeros count for nothing until everything else is equal.
                var startA = a;
                var startB = b;
                while (a < left.Length && left[a] == '0')
                {
                    a++;
                }
                while (b < right.Length && right[b] == '0')
                {
                    b++;
                }
                var digitsA = a;
                var digitsB = b;
                while (a < left.Length && char.IsAsciiDigit(left[a]))
                {
                    a++;
                }
                while (b < right.Length && char.IsAsciiDigit(right[b]))
                {
                    b++;
                }
                var lengthA = a - digitsA;
                var lengthB = b - digitsB;
                if (lengthA != lengthB)
                {
                    return lengthA < lengthB ? -1 : 1;
                }
                var order = string.CompareOrdinal(left, digitsA, right, digitsB, lengthA);
                if (order != 0)
                {
                    return order < 0 ? -1 : 1;
                }
                // Equal numbers: the one written with fewer zeros first.
                var zerosA = digitsA - startA;
                var zerosB = digitsB - startB;
                if (zerosA != zerosB)
                {
                    return zerosA < zerosB ? -1 : 1;
                }
                continue;
            }
            var one = char.ToUpperInvariant(left[a]);
            var other = char.ToUpperInvariant(right[b]);
            if (one != other)
            {
                return one < other ? -1 : 1;
            }
            a++;
            b++;
        }
        var restA = left.Length - a;
        var restB = right.Length - b;
        return restA == restB ? 0 : restA < restB ? -1 : 1;
    }
}
