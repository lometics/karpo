// clipboard: what was copied lately, as Karpo keeps it itself (the backlog, item 13).
//
// Windows keeps a history of its own (Win+V), and where it is on Karpo shows that one
// (ClipboardSystem.ReadHistoryAsync). Where it is off, Karpo keeps its own, here: what lands on the
// clipboard while the dock runs, heard through AddClipboardFormatListener, in memory only — nothing
// is written to disk, and it is gone when the dock ends.
//
// **What is never kept.** A program that puts a secret on the clipboard — a password manager, a
// banking app — marks it with the formats Windows reads for exactly this
// (learn.microsoft.com, "Clipboard formats", Cloud clipboard and clipboard history formats):
//
//   * ExcludeClipboardContentFromMonitorProcessing: present at all, and nothing that watches the
//     clipboard should look at the content;
//   * CanIncludeInClipboardHistory: a DWORD, 0 to keep it out of the history;
//   * CanUploadToCloudClipboard: a DWORD, 0 to keep it off the cloud. Windows still keeps such an
//     item in its local history; Karpo, at the person's word, keeps it nowhere;
//   * "Clipboard Viewer Ignore", the older convention of the same (KeePass and others set it).
//
// Content carrying any of them is not read at all, so it is neither stored nor shown
// (ClipboardRules.Excluded). A marker whose value cannot be read counts as a no.
//
// **What is kept**: text up to a million characters, and pictures up to 40 MB each — a 4K
// screenshot is about 33 MB as the DIB the clipboard holds, a 1080p one about 8 MB — the newest
// first, 25 at most, as Win+V's own. Pictures are kept whole, so a chosen one goes back on the
// clipboard as it was copied; all of them together stay under a budget of 160 MB, past which the
// oldest unpinned pictures are let go. The same content copied again moves to the top rather than
// standing twice. A pinned item stays when the history is cleared and when the oldest are let go.

using System.Buffers.Binary;

namespace Karpo.Dock;

/// <summary>What an item of the history is: text or a picture.</summary>
internal enum ClipKind
{
    Text,
    Image,
}

/// <summary>
/// The markers a program puts beside what it copies to keep it out of a clipboard history, as read
/// from the clipboard (<see cref="ClipboardRules"/>): which are there, and the two DWORDs' values —
/// null where the format is there but its value could not be read.
/// </summary>
internal sealed record ClipMarkers
{
    /// <summary>ExcludeClipboardContentFromMonitorProcessing is on the clipboard.</summary>
    public bool ExcludeFromMonitor { get; init; }

    /// <summary>"Clipboard Viewer Ignore" is on the clipboard.</summary>
    public bool ViewerIgnore { get; init; }

    /// <summary>CanIncludeInClipboardHistory is on the clipboard.</summary>
    public bool HasCanInclude { get; init; }

    /// <summary>Its value, null where it could not be read.</summary>
    public uint? CanInclude { get; init; }

    /// <summary>CanUploadToCloudClipboard is on the clipboard.</summary>
    public bool HasCanUpload { get; init; }

    /// <summary>Its value, null where it could not be read.</summary>
    public uint? CanUpload { get; init; }

    /// <summary>None of them: an ordinary copy.</summary>
    public static readonly ClipMarkers None = new();
}

/// <summary>
/// What was on the clipboard at one change: its markers, and — where they let it be read — its text
/// and its picture as a packed DIB (CF_DIB: a BITMAPINFOHEADER, its colour masks, its pixels).
/// </summary>
internal sealed record ClipSnapshot(ClipMarkers Markers, string? Text, byte[]? Dib);

/// <summary>Which formats keep a copy out of a clipboard history (see the file's header).</summary>
internal static class ClipboardRules
{
    public const string ExcludeFromMonitor = "ExcludeClipboardContentFromMonitorProcessing";
    public const string CanIncludeInHistory = "CanIncludeInClipboardHistory";
    public const string CanUploadToCloud = "CanUploadToCloudClipboard";
    public const string ViewerIgnore = "Clipboard Viewer Ignore"; // not-ui

    /// <summary>The largest picture Karpo's history keeps: a 4K screenshot as a 32-bit DIB, and some.</summary>
    public const int MostPictureBytes = 40 * 1024 * 1024;

    /// <summary>What all the pictures Karpo's history keeps may hold together.</summary>
    public const long PictureBudget = 160L * 1024 * 1024;

    /// <summary>
    /// The longest text Karpo's history keeps, in characters: a longer one is not kept at all, as
    /// one cut short would paste something other than what was copied.
    /// </summary>
    public const int MostTextChars = 1024 * 1024;

    /// <summary>
    /// Whether what is on the clipboard with <paramref name="markers"/> must be left alone: not read,
    /// not kept, not shown. Any marker that says no, and any whose answer cannot be read.
    /// </summary>
    public static bool Excluded(ClipMarkers markers) =>
        markers.ExcludeFromMonitor
        || markers.ViewerIgnore
        || (markers.HasCanInclude && markers.CanInclude is not > 0)
        || (markers.HasCanUpload && markers.CanUpload is not > 0);
}

/// <summary>One item of Karpo's own history: what it is, what it holds, whether it is pinned.</summary>
internal sealed record ClipEntry(long Id, ClipKind Kind, string Text, byte[]? Dib, bool Pinned)
{
    /// <summary>Whether <paramref name="other"/> holds the same content: text by its characters, a picture by its bytes.</summary>
    public bool Same(ClipEntry other) =>
        Kind == other.Kind
        && (Kind == ClipKind.Text ? string.Equals(Text, other.Text, StringComparison.Ordinal) : Dib.AsSpan().SequenceEqual(other.Dib));
}

/// <summary>
/// Karpo's own clipboard history, in memory: the newest first, 25 at most, pinned items kept. Not
/// thread-safe: the store calls it under its own lock.
/// </summary>
internal sealed class ClipboardRing
{
    /// <summary>How many items it keeps, as Windows' own history.</summary>
    public const int Capacity = 25;

    private readonly List<ClipEntry> items = [];
    private readonly long pictureBudget;
    private long next = 1;

    /// <param name="pictureBudget">What its pictures may hold together; a test makes it small.</param>
    public ClipboardRing(long pictureBudget = ClipboardRules.PictureBudget)
    {
        this.pictureBudget = pictureBudget;
    }

    /// <summary>The items, the newest first.</summary>
    public IReadOnlyList<ClipEntry> Items => items;

    /// <summary>
    /// What was copied, kept at the top: false where nothing is kept — content a program marked to
    /// stay out (<see cref="ClipboardRules.Excluded"/>), nothing that is text or a picture, text past
    /// <see cref="ClipboardRules.MostTextChars"/>, a picture past
    /// <see cref="ClipboardRules.MostPictureBytes"/>. The same content again moves to the top, pinned
    /// as it was; past <see cref="Capacity"/> the oldest unpinned item goes, and past
    /// <see cref="ClipboardRules.PictureBudget"/> the oldest unpinned pictures.
    /// </summary>
    public bool Add(ClipSnapshot snapshot)
    {
        if (ClipboardRules.Excluded(snapshot.Markers))
        {
            return false;
        }
        ClipEntry entry;
        if (snapshot.Text is { } text && text.Trim().Length > 0)
        {
            if (text.Length > ClipboardRules.MostTextChars)
            {
                return false;
            }
            entry = new ClipEntry(0, ClipKind.Text, text, null, false);
        }
        else if (snapshot.Dib is { Length: > 0 and <= ClipboardRules.MostPictureBytes } dib)
        {
            entry = new ClipEntry(0, ClipKind.Image, "", dib, false);
        }
        else
        {
            return false;
        }
        var same = items.FindIndex(entry.Same);
        if (same >= 0)
        {
            var was = items[same];
            items.RemoveAt(same);
            items.Insert(0, was);
            return true;
        }
        items.Insert(0, entry with { Id = next++ });
        while (items.Count > Capacity && items.FindLastIndex(item => !item.Pinned) is var oldest and >= 0)
        {
            items.RemoveAt(oldest);
        }
        // The newest picture is always kept; older ones go, oldest first, until all fit the budget.
        while (PictureBytes > pictureBudget
            && items.FindLastIndex(item => !item.Pinned && item.Kind == ClipKind.Image) is var picture and > 0)
        {
            items.RemoveAt(picture);
        }
        return true;
    }

    /// <summary>What the pictures kept hold together.</summary>
    public long PictureBytes => items.Sum(item => (long)(item.Dib?.Length ?? 0));

    /// <summary>The item of <paramref name="id"/>, or null where it has gone.</summary>
    public ClipEntry? Find(long id) => items.Find(item => item.Id == id);

    /// <summary>Pins it, or unpins it; false where it has gone.</summary>
    public bool Pin(long id, bool pinned)
    {
        var at = items.FindIndex(item => item.Id == id);
        if (at < 0)
        {
            return false;
        }
        items[at] = items[at] with { Pinned = pinned };
        return true;
    }

    /// <summary>Takes it out; false where it had gone already.</summary>
    public bool Delete(long id) => items.RemoveAll(item => item.Id == id) > 0;

    /// <summary>Everything but the pinned items, as Windows' "Clear all".</summary>
    public void Clear() => items.RemoveAll(item => !item.Pinned);
}

/// <summary>
/// A packed DIB (CF_DIB) made small enough to show: the thumbnail of a copied picture in Karpo's own
/// history. Uncompressed 24- and 32-bit pictures, top-down or bottom-up — what screenshots and
/// browsers put on the clipboard; anything else has no thumbnail and shows as a picture glyph.
/// </summary>
internal static class DibPictures
{
    private const int BI_RGB = 0;
    private const int BI_BITFIELDS = 3;

    /// <summary>
    /// The picture in <paramref name="dib"/> fitted inside <paramref name="most"/> px across and
    /// down — never larger than it is — as premultiplied BGRA, or null where it cannot be read.
    /// </summary>
    public static Pixels? Thumbnail(ReadOnlySpan<byte> dib, int mostWidth, int mostHeight)
    {
        if (dib.Length < 40 || mostWidth < 1 || mostHeight < 1)
        {
            return null;
        }
        var header = BinaryPrimitives.ReadInt32LittleEndian(dib);
        var width = BinaryPrimitives.ReadInt32LittleEndian(dib[4..]);
        var height = BinaryPrimitives.ReadInt32LittleEndian(dib[8..]);
        var bits = BinaryPrimitives.ReadUInt16LittleEndian(dib[14..]);
        var compression = BinaryPrimitives.ReadInt32LittleEndian(dib[16..]);
        var colours = BinaryPrimitives.ReadInt32LittleEndian(dib[32..]);
        if (header < 40 || width <= 0 || height == 0 || bits is not (24 or 32)
            || compression is not (BI_RGB or BI_BITFIELDS) || (long)width * Math.Abs((long)height) > 64L * 1024 * 1024)
        {
            return null;
        }
        // The masks of BI_BITFIELDS follow a 40-byte header; a larger header holds them itself.
        var offset = header + (compression == BI_BITFIELDS && header == 40 ? 12 : 0) + (colours * 4);
        var bottomUp = height > 0;
        height = Math.Abs(height);
        var stride = ((width * bits) + 31) / 32 * 4;
        if (offset < 0 || offset + ((long)stride * height) > dib.Length)
        {
            return null;
        }
        var fit = Math.Min(1.0, Math.Min((double)mostWidth / width, (double)mostHeight / height));
        var outWidth = Math.Max(1, (int)Math.Round(width * fit));
        var outHeight = Math.Max(1, (int)Math.Round(height * fit));
        var bytes = bits / 8;
        // A 32-bit picture whose alpha is zero everywhere has none: it is opaque, as Windows draws it.
        var alpha = bits == 32 && HasAlpha(dib[offset..], stride, width, height);
        var data = new byte[outWidth * outHeight * 4];
        for (var y = 0; y < outHeight; y++)
        {
            // Each pixel of the thumbnail is the average of the block of the picture it stands for.
            var top = (int)((long)y * height / outHeight);
            var bottom = Math.Max(top + 1, (int)((long)(y + 1) * height / outHeight));
            for (var x = 0; x < outWidth; x++)
            {
                var left = (int)((long)x * width / outWidth);
                var right = Math.Max(left + 1, (int)((long)(x + 1) * width / outWidth));
                long b = 0, g = 0, r = 0, a = 0, n = 0;
                for (var row = top; row < bottom; row++)
                {
                    var line = offset + ((bottomUp ? height - 1 - row : row) * stride);
                    for (var column = left; column < right; column++)
                    {
                        var at = line + (column * bytes);
                        var opacity = alpha ? dib[at + 3] : 255;
                        // Premultiplied as it is summed, so a transparent pixel adds no colour.
                        b += dib[at] * opacity;
                        g += dib[at + 1] * opacity;
                        r += dib[at + 2] * opacity;
                        a += opacity;
                        n++;
                    }
                }
                var to = ((y * outWidth) + x) * 4;
                data[to] = (byte)(b / 255 / n);
                data[to + 1] = (byte)(g / 255 / n);
                data[to + 2] = (byte)(r / 255 / n);
                data[to + 3] = (byte)(a / n);
            }
        }
        return new Pixels { Data = data, Width = outWidth, Height = outHeight };
    }

    /// <summary>Whether any pixel of a 32-bit picture has an alpha other than zero.</summary>
    private static bool HasAlpha(ReadOnlySpan<byte> pixels, int stride, int width, int height)
    {
        for (var row = 0; row < height; row++)
        {
            for (var column = 0; column < width; column++)
            {
                if (pixels[(row * stride) + (column * 4) + 3] != 0)
                {
                    return true;
                }
            }
        }
        return false;
    }
}
