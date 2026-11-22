// clipboard: what the clipboard history asks of Windows, behind one seam (IClipboardSystem), so a
// test brings a fake and nothing in a test reads or writes the person's real clipboard.
//
// Two ways in, one per source of the history (ClipboardStore says which is used when):
//
//   * Windows' own history (Win+V), through Windows.ApplicationModel.DataTransfer.Clipboard. It
//     answers an unpackaged program: GetHistoryItemsAsync returned Success with the person's items
//     to a plain console program on Windows 11 24H2 (2026-10-03), and IsHistoryEnabled says whether
//     the person has it on. An item's text and picture are read from its content, a picture decoded
//     to a thumbnail at once (BitmapDecoder), so the line only ever draws what is in memory. An
//     item is put back with SetHistoryItemAsContent and taken out with DeleteItemFromHistory, and
//     "Clear all" is ClearHistory, which keeps the pinned ones as Win+V does. Windows offers a
//     program no way to pin, so Karpo's line has no pin for Windows' items.
//   * Karpo's own, where Windows' is off: the clipboard heard through AddClipboardFormatListener on
//     a message-only window of its own, read when it changes — its markers first, and its text and
//     picture only where no marker keeps it out (ClipboardRules) — and put back with
//     SetClipboardData.
//
// Nothing managed crosses to WinRT here (docs\widgets.md §10): every call takes a WinRT object or
// nothing, and the one list, the history's items, came from WinRT.

using System.Runtime.InteropServices;
using Karpo.Core;
using TerraFX.Interop.Windows;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using static TerraFX.Interop.Windows.Windows;
using WinClipboard = Windows.ApplicationModel.DataTransfer.Clipboard;

namespace Karpo.Dock;

/// <summary>One item as the line shows it, from either source.</summary>
/// <param name="Id">What the store knows it by: Windows' item id, or Karpo's own number.</param>
/// <param name="Text">Its text, for <see cref="ClipKind.Text"/>.</param>
/// <param name="Picture">Its thumbnail, for <see cref="ClipKind.Image"/>; null where it could not be decoded.</param>
internal sealed record ClipView(string Id, ClipKind Kind, string Text, Pixels? Picture, bool Pinned);

/// <summary>What the clipboard history asks of Windows. The dock's is <see cref="ClipboardSystem"/>; a test brings a fake.</summary>
internal interface IClipboardSystem
{
    /// <summary>Whether Windows' own clipboard history is on (Settings, System, Clipboard).</summary>
    bool HistoryEnabled { get; }

    /// <summary>
    /// Windows' history now, the newest first, each picture made at most <paramref name="mostWidth"/>
    /// by <paramref name="mostHeight"/> px; null where Windows would not give it.
    /// </summary>
    Task<IReadOnlyList<ClipView>?> ReadHistoryAsync(int mostWidth, int mostHeight);

    /// <summary>That item of Windows' history on the clipboard again; false where it could not be.</summary>
    bool Restore(string id);

    /// <summary>That item taken out of Windows' history.</summary>
    bool Delete(string id);

    /// <summary>Windows' history cleared, but for the pinned items.</summary>
    bool ClearHistory();

    /// <summary>
    /// What is on the clipboard now: its markers, and its text and picture where no marker keeps
    /// them out; null where the clipboard could not be opened. Off the dock's thread.
    /// </summary>
    ClipSnapshot? Read();

    /// <summary>Text or a picture of Karpo's own history on the clipboard again.</summary>
    bool Put(ClipEntry entry);

    /// <summary>
    /// Hears the clipboard change from now on, calling <paramref name="changed"/> on the dock's
    /// thread each time; null stops hearing it.
    /// </summary>
    void Listen(Action? changed);
}

/// <summary>The real clipboard and Windows' real history.</summary>
internal sealed class ClipboardSystem : IClipboardSystem
{
    private const uint CF_UNICODETEXT = 13;
    private const uint CF_DIB = 8;
    private const uint WM_CLIPBOARDUPDATE = 0x031D;

    /// <summary>How many times, and how far apart, a busy clipboard is asked again: another program may hold it a moment.</summary>
    private const int Tries = 6;
    private const int TryMs = 15;

    /// <summary>How long Windows' clipboard service is given to hand over its history.</summary>
    private static readonly TimeSpan HistoryWait = TimeSpan.FromSeconds(5);

    public static readonly ClipboardSystem Shared = new();

    // Windows' items by id, from the last read: what Restore and Delete act on.
    private readonly Lock gate = new();
    private Dictionary<string, ClipboardHistoryItem> windowsItems = [];

    // The listening window, and what it calls; on the dock's thread.
    private static HWND listener;
    private static Action? onChanged;

    private ClipboardSystem()
    {
    }

    public bool HistoryEnabled
    {
        get
        {
            try
            {
                return WinClipboard.IsHistoryEnabled();
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                Troubles.Note("Clipboard history: IsHistoryEnabled", error);
                return false;
            }
        }
    }

    public async Task<IReadOnlyList<ClipView>?> ReadHistoryAsync(int mostWidth, int mostHeight)
    {
        try
        {
            // Windows' clipboard service may not answer at all (it is restarting, or hung): the
            // line then shows Karpo's own rather than "Reading…" for ever (ClipboardStore too).
            var result = await WinClipboard.GetHistoryItemsAsync().AsTask().WaitAsync(HistoryWait).ConfigureAwait(false);
            if (result.Status != ClipboardHistoryItemsResultStatus.Success)
            {
                Troubles.Write($"Clipboard history: GetHistoryItemsAsync said {result.Status}");
                return null;
            }
            var views = new List<ClipView>();
            var kept = new Dictionary<string, ClipboardHistoryItem>(StringComparer.Ordinal);
            foreach (var item in result.Items)
            {
                var content = item.Content;
                ClipView? view = null;
                try
                {
                    if (content.Contains(StandardDataFormats.Text))
                    {
                        // Only what a card can show is held: the item itself stays Windows', put back by its id.
                        view = new ClipView(item.Id, ClipKind.Text, ClipText.Held(await content.GetTextAsync()), null, false);
                    }
                    else if (content.Contains(StandardDataFormats.Bitmap))
                    {
                        view = new ClipView(item.Id, ClipKind.Image, "", await PictureAsync(content, mostWidth, mostHeight), false);
                    }
                }
                catch (Exception error) when (error is not OutOfMemoryException)
                {
                    // One item that cannot be read leaves the others; the person still has it in Win+V.
                    Troubles.Note("Clipboard history: an item", error);
                }
                if (view is not null)
                {
                    views.Add(view);
                    kept[item.Id] = item;
                }
            }
            lock (gate)
            {
                windowsItems = kept;
            }
            return views;
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Troubles.Note("Clipboard history: GetHistoryItemsAsync", error);
            return null;
        }
    }

    /// <summary>A history item's picture, decoded and made small enough to show.</summary>
    private static async Task<Pixels?> PictureAsync(DataPackageView content, int mostWidth, int mostHeight)
    {
        var reference = await content.GetBitmapAsync();
        using var stream = await reference.OpenReadAsync();
        var decoder = await BitmapDecoder.CreateAsync(stream);
        var (width, height) = ((int)decoder.PixelWidth, (int)decoder.PixelHeight);
        if (width <= 0 || height <= 0)
        {
            return null;
        }
        var fit = Math.Min(1.0, Math.Min((double)mostWidth / width, (double)mostHeight / height));
        var (outWidth, outHeight) = (Math.Max(1, (int)Math.Round(width * fit)), Math.Max(1, (int)Math.Round(height * fit)));
        var transform = new BitmapTransform
        {
            ScaledWidth = (uint)outWidth,
            ScaledHeight = (uint)outHeight,
            InterpolationMode = BitmapInterpolationMode.Fant,
        };
        var pixels = await decoder.GetPixelDataAsync(
            BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, transform,
            ExifOrientationMode.IgnoreExifOrientation, ColorManagementMode.ColorManageToSRgb);
        var data = pixels.DetachPixelData();
        return data.Length == outWidth * outHeight * 4 ? new Pixels { Data = data, Width = outWidth, Height = outHeight } : null;
    }

    public bool Restore(string id) => OnWindowsItem(id, item => WinClipboard.SetHistoryItemAsContent(item) == SetHistoryItemAsContentStatus.Success);

    public bool Delete(string id) => OnWindowsItem(id, WinClipboard.DeleteItemFromHistory);

    public bool ClearHistory()
    {
        try
        {
            return WinClipboard.ClearHistory();
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Troubles.Note("Clipboard history: ClearHistory", error);
            return false;
        }
    }

    /// <summary><paramref name="act"/> on Windows' item <paramref name="id"/> from the last read; false where it has gone.</summary>
    private bool OnWindowsItem(string id, Func<ClipboardHistoryItem, bool> act)
    {
        ClipboardHistoryItem? item;
        lock (gate)
        {
            item = windowsItems.GetValueOrDefault(id);
        }
        if (item is null)
        {
            return false;
        }
        try
        {
            return act(item);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Troubles.Note("Clipboard history: an item's act", error);
            return false;
        }
    }

    // --- Karpo's own: the clipboard itself -------------------------------------------------------

    public ClipSnapshot? Read()
    {
        if (!Open(HWND.NULL))
        {
            return null;
        }
        try
        {
            var markers = Markers();
            // A marker that keeps it out keeps it unread: its text and its picture are never touched.
            if (ClipboardRules.Excluded(markers))
            {
                return new ClipSnapshot(markers, null, null);
            }
            // At most one character past the most kept: enough for the history to see it is too long.
            var text = IsClipboardFormatAvailable(CF_UNICODETEXT) ? ReadText(ClipboardRules.MostTextChars + 1) : null;
            var dib = text is null && IsClipboardFormatAvailable(CF_DIB) ? ReadBytes(CF_DIB, ClipboardRules.MostPictureBytes) : null;
            return new ClipSnapshot(markers, text, dib);
        }
        finally
        {
            _ = CloseClipboard();
        }
    }

    /// <summary>The markers on the clipboard, which is open (see ClipboardRules).</summary>
    private static ClipMarkers Markers()
    {
        var exclude = Format(ClipboardRules.ExcludeFromMonitor);
        var ignore = Format(ClipboardRules.ViewerIgnore);
        var include = Format(ClipboardRules.CanIncludeInHistory);
        var upload = Format(ClipboardRules.CanUploadToCloud);
        var hasInclude = include != 0 && IsClipboardFormatAvailable(include);
        var hasUpload = upload != 0 && IsClipboardFormatAvailable(upload);
        return new ClipMarkers
        {
            ExcludeFromMonitor = exclude != 0 && IsClipboardFormatAvailable(exclude),
            ViewerIgnore = ignore != 0 && IsClipboardFormatAvailable(ignore),
            HasCanInclude = hasInclude,
            CanInclude = hasInclude ? Dword(include) : null,
            HasCanUpload = hasUpload,
            CanUpload = hasUpload ? Dword(upload) : null,
        };
    }

    /// <summary>A registered format's number, by its name; 0 where it could not be had.</summary>
    private static unsafe uint Format(string name)
    {
        fixed (char* chars = name)
        {
            return RegisterClipboardFormatW(chars);
        }
    }

    /// <summary>A DWORD format's value, or null where it holds none.</summary>
    private static uint? Dword(uint format) =>
        ReadBytes(format, 64) is { Length: >= 4 } bytes ? BitConverter.ToUInt32(bytes, 0) : null;

    /// <summary>The clipboard's text, to its first nul, and no more than <paramref name="most"/> characters of it.</summary>
    private static unsafe string? ReadText(int most)
    {
        var data = (HGLOBAL)GetClipboardData(CF_UNICODETEXT).Value;
        if (data == HGLOBAL.NULL)
        {
            return null;
        }
        var locked = GlobalLock(data);
        if (locked == null)
        {
            return null;
        }
        try
        {
            var chars = (char*)locked;
            most = (int)Math.Min(GlobalSize(data) / 2, (nuint)most);
            var length = 0;
            while (length < most && chars[length] != '\0')
            {
                length++;
            }
            return new string(chars, 0, length);
        }
        finally
        {
            _ = GlobalUnlock(data);
        }
    }

    /// <summary>A format's bytes, or null where it has none or more than <paramref name="most"/>.</summary>
    private static unsafe byte[]? ReadBytes(uint format, int most)
    {
        var data = (HGLOBAL)GetClipboardData(format).Value;
        if (data == HGLOBAL.NULL)
        {
            return null;
        }
        var size = GlobalSize(data);
        if (size == 0 || size > (nuint)most)
        {
            return null;
        }
        var locked = GlobalLock(data);
        if (locked == null)
        {
            return null;
        }
        try
        {
            return new ReadOnlySpan<byte>(locked, (int)size).ToArray();
        }
        finally
        {
            _ = GlobalUnlock(data);
        }
    }

    public bool Put(ClipEntry entry)
    {
        if (!Open(listener))
        {
            return false;
        }
        try
        {
            _ = EmptyClipboard();
            return entry.Kind == ClipKind.Text
                ? Set(CF_UNICODETEXT, MemoryMarshal.AsBytes((entry.Text + "\0").AsSpan()))
                : entry.Dib is { } dib && Set(CF_DIB, dib);
        }
        finally
        {
            _ = CloseClipboard();
        }
    }

    /// <summary>Puts <paramref name="bytes"/> on the open clipboard as <paramref name="format"/>, which then owns them.</summary>
    private static unsafe bool Set(uint format, ReadOnlySpan<byte> bytes)
    {
        var data = GlobalAlloc(GMEM.GMEM_MOVEABLE, (nuint)bytes.Length);
        if (data == HGLOBAL.NULL)
        {
            return false;
        }
        var locked = GlobalLock(data);
        if (locked == null)
        {
            _ = GlobalFree(data);
            return false;
        }
        bytes.CopyTo(new Span<byte>(locked, bytes.Length));
        _ = GlobalUnlock(data);
        if (SetClipboardData(format, (HANDLE)data.Value) == HANDLE.NULL)
        {
            _ = GlobalFree(data);
            return false;
        }
        return true;
    }

    /// <summary>Opens the clipboard, asking again a few times while another program holds it.</summary>
    private static bool Open(HWND owner)
    {
        for (var attempt = 0; attempt < Tries; attempt++)
        {
            if (OpenClipboard(owner))
            {
                return true;
            }
            Thread.Sleep(TryMs);
        }
        return false;
    }

    // --- Hearing it change ------------------------------------------------------------------------

    public unsafe void Listen(Action? changed)
    {
        onChanged = changed;
        if (changed is not null && listener == HWND.NULL)
        {
            var instance = (HINSTANCE)(void*)GetModuleHandleW(null);
            fixed (char* name = "karpo.clipboard")
            {
                var type = new WNDCLASSW { lpfnWndProc = &ListenerProcedure, hInstance = instance, lpszClassName = name };
                // Fails when an earlier one registered it, which is fine: same procedure.
                RegisterClassW(&type);
                // Message-only: WM_CLIPBOARDUPDATE reaches it, and nothing on screen is made.
                listener = CreateWindowExW(0, name, null, 0, 0, 0, 0, 0, HWND.HWND_MESSAGE, HMENU.NULL, instance, null);
            }
            if (listener != HWND.NULL && !AddClipboardFormatListener(listener))
            {
                Surfaces.Gone(listener);
                listener = HWND.NULL;
            }
        }
        else if (changed is null && listener != HWND.NULL)
        {
            _ = RemoveClipboardFormatListener(listener);
            Surfaces.Gone(listener);
            listener = HWND.NULL;
        }
    }

    [UnmanagedCallersOnly]
    private static LRESULT ListenerProcedure(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        try
        {
            if (msg == WM_CLIPBOARDUPDATE)
            {
                onChanged?.Invoke();
                return 0;
            }
            return DefWindowProcW(hwnd, msg, wParam, lParam);
        }
        catch (Exception error)
        {
            // Nothing may leave a window procedure: it would end the dock (NativeBoundaryTests).
            Troubles.Note("Clipboard history: a change", error);
            return 0;
        }
    }
}
