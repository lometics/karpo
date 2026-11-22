// The picture a drive is drawn with: the one Explorer draws it with.
//
// The user, of the widget this replaces: "the layout of the disk icon looks off
// in general". It drew Segoe Fluent's HardDrive glyph, which is the same flat
// outlined pill as the battery glyphs a few cells along — so a disk, a stick
// and a share were one shape, and that shape read as a battery. The shell's own
// icon tells them apart at a glance, and it is the picture Windows itself uses
// everywhere else for the same drive.
//
// Asking the shell for it is not free and, for a network drive or a card reader
// with nothing in it, not quick either, so nothing here happens on the dock's
// thread: a miss draws the fallback glyph, starts one read on a thread of its
// own, and tells the widget when the picture is there. Answers are kept, keyed
// by the drive and the size it is drawn at, and the cache is bounded — an
// unbounded icon cache is what grew the dock's memory over a day.
//
// A network drive that is not connected is drawn with the picture Explorer gives
// one: the shell's stock "disconnected network drive", the drive with the red X
// (SIID_DRIVENETDISABLED). It is asked of the stock icons rather than of the
// drive, whose root would be a question to a share that is not there.

using Karpo.Core;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
using static TerraFX.Interop.Windows.S;

namespace Karpo.Dock.Drives;

internal static class DriveIcons
{
    /// <summary>What <see cref="Source"/> is asked for the disconnected network drive's picture, in place of a root.</summary>
    internal const string DisconnectedRoot = "stock:drivenetdisabled";

    /// <summary>
    /// The most pictures kept. A PC has a handful of drives and a bar a handful of sizes;
    /// past this the oldest go, so a day of plugging sticks in cannot grow the dock.
    /// </summary>
    private const int Most = 48;

    private static readonly Lock Gate = new();
    private static readonly Dictionary<string, Pixels?> Kept = new(StringComparer.OrdinalIgnoreCase);
    private static readonly List<string> Order = [];
    private static readonly HashSet<string> Asking = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Where a picture comes from: the shell, unless a test hands in its own answer for a root and
    /// a size (null draws the fallback glyph). A picture of made-up drives lives on letters the PC
    /// running it may really have, and asking the shell about one would read the real volume.
    /// </summary>
    internal static Func<string, int, Pixels?>? Source { get; set; }

    /// <summary>
    /// The shell's picture for the drive at <paramref name="root"/> ("C:\") at
    /// <paramref name="px"/> px, or null while it is not known yet. A miss starts one read off
    /// the dock's thread and calls <paramref name="ready"/> — from that thread — once there is
    /// an answer, so the widget can ask again and redraw.
    /// </summary>
    /// <param name="id">
    /// What the picture belongs to: the drive's durable name, so a stick keeps its icon when it
    /// comes back at another letter. Empty falls back to the root.
    /// </param>
    public static Pixels? For(string id, string root, int px, Action ready)
    {
        if (root.Length == 0 || px <= 0)
        {
            return null;
        }
        if (Source is { } stand)
        {
            return stand(root, px);
        }
        return Cached($"{(id.Length > 0 ? id : root)}|{px}", () => Items.Icon(root, px), ready);
    }

    /// <summary>
    /// Explorer's picture of a network drive that is not connected — the drive with the red X — at
    /// <paramref name="px"/> px, or null while it is not known yet (read off the dock's thread, as
    /// <see cref="For"/> reads a drive's).
    /// </summary>
    public static Pixels? Disconnected(int px, Action ready)
    {
        if (px <= 0)
        {
            return null;
        }
        if (Source is { } stand)
        {
            return stand(DisconnectedRoot, px);
        }
        return Cached($"{DisconnectedRoot}|{px}", () => Stock(SHSTOCKICONID.SIID_DRIVENETDISABLED, px), ready);
    }

    /// <summary>The shell's stock icon <paramref name="id"/> at <paramref name="px"/> px, from its own file; null where there is none.</summary>
    internal static unsafe Pixels? Stock(SHSTOCKICONID id, int px)
    {
        var stock = new SHSTOCKICONINFO { cbSize = (uint)sizeof(SHSTOCKICONINFO) };
        if (SHGetStockIconInfo(id, SHGSI.SHGSI_ICONLOCATION, &stock).FAILED)
        {
            return null;
        }
        HICON icon = HICON.NULL;
        // S_FALSE: the file has no icon at that index.
        if (SHDefExtractIconW(&stock.szPath[0], stock.iIcon, 0, &icon, null, (uint)px).Value != S_OK || icon == HICON.NULL)
        {
            return null;
        }
        try
        {
            return Tray.Render(icon, px);
        }
        finally
        {
            _ = DestroyIcon(icon);
        }
    }

    /// <summary>
    /// What is kept under <paramref name="key"/>; a miss starts one <paramref name="read"/> off the
    /// dock's thread and calls <paramref name="ready"/> from there once it is kept.
    /// </summary>
    private static Pixels? Cached(string key, Func<Pixels?> read, Action ready)
    {
        lock (Gate)
        {
            if (Kept.TryGetValue(key, out var kept))
            {
                return kept;
            }
            if (!Asking.Add(key))
            {
                return null;
            }
        }
        _ = DriveReader.Start(() =>
        {
            Pixels? picture = null;
            try
            {
                picture = read();
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                // A shell handler that misbehaves costs the drive its picture, not the bar.
            }
            Remember(key, picture);
            ready();
        });
        return null;
    }

    /// <summary>
    /// Drops what is kept for a drive that has gone or come back, so its picture is asked for
    /// again (a card reader's icon changes when a card goes in).
    /// </summary>
    public static void Forget()
    {
        lock (Gate)
        {
            Kept.Clear();
            Order.Clear();
        }
    }

    private static void Remember(string key, Pixels? picture)
    {
        lock (Gate)
        {
            Asking.Remove(key);
            if (Kept.TryAdd(key, picture))
            {
                Order.Add(key);
            }
            while (Order.Count > Most)
            {
                Kept.Remove(Order[0]);
                Order.RemoveAt(0);
            }
        }
    }
}
