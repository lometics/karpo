// A window's thumbnail toolbar (ITaskbarList3::ThumbBarAddButtons and ThumbBarUpdateButtons): up to
// seven buttons under its preview, which Karpo's window list (Preview.cs) draws and whose clicks go
// back to the window the way the taskbar sends them — WM_COMMAND, THBN_CLICKED in the high word of
// wParam and the button's id in the low one.
//
// What arrives is the taskbar list's copy of the app's THUMBBUTTONs (Taskband.cs): a count, then
// 540 bytes a button — its mask, id, image index, icon (a 32-bit handle), a 260-character tooltip and
// its flags. The mask says which of the others count; an update changes only those. The icon is
// drawn the moment it arrives, as an overlay's is: a HICON is the window manager's, so the band
// draws the app's own handle from its own process.
//
// A button's picture is either that icon (THB_ICON) or a picture of the image list the app set with
// ThumbBarSetImageList, by index (THB_BITMAP), which is how Spotify draws previous, play and next:
// its image list, never an icon. An HIMAGELIST is memory in the app's process, so the taskbar list
// does not send it: it writes the list out (ImageList_Write, which ExplorerFrame imports) and the
// taskbar reads it back (ImageList_Read, which Taskbar.dll imports) - a data atom like the buttons',
// holding the stream, which starts with the image list's "IL" header. The band reads it the same
// way, with comctl32 6 as Explorer does (the dock's manifest asks for it, since version 5 cannot
// read what 6 writes), and draws each picture of it at once. Where a button has both, its icon is
// drawn, as the taskbar does; a picture the list does not have leaves the letter tile of the
// button's tooltip, as before.
//
// The flags are Windows': a hidden button is not there at all and takes no room; a disabled one is
// drawn faint and does nothing; a non-interactive one is drawn as it is and does nothing, being a
// picture rather than a button; one without a background shows its picture alone, and no hover
// under it; one that dismisses closes the list once clicked.

using System.Buffers.Binary;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

/// <summary>THUMBBUTTONFLAGS, as a thumbnail toolbar's button carries them.</summary>
[Flags]
public enum ThumbFlags : uint
{
    None = 0,
    Disabled = 0x1,
    DismissOnClick = 0x2,
    NoBackground = 0x4,
    Hidden = 0x8,
    NonInteractive = 0x10,
}

/// <summary>A button as the app sent it: which fields count (THUMBBUTTONMASK), and the fields.</summary>
/// <param name="Icon">The icon's handle as sent: 32 bits, sign-extended where a 64-bit handle is wanted.</param>
internal readonly record struct ThumbButtonData(uint Mask, uint Id, uint Bitmap, uint Icon, string Tip, uint Flags)
{
    public const uint BitmapMask = 0x1;
    public const uint IconMask = 0x2;
    public const uint TipMask = 0x4;
    public const uint FlagsMask = 0x8;

    /// <summary>The icon's handle, where the mask says it counts; null otherwise.</summary>
    public HICON? IconHandle => (Mask & IconMask) != 0 && Icon != 0 ? (HICON)(nint)(int)Icon : null;
}

/// <summary>
/// A button as Karpo keeps it and draws it: its id, flags, tooltip, the picture drawn from its icon
/// (null for none, or for a picture that is an image list's), and its image list index (-1 for none).
/// The tooltip is the button's name — its letter tile where it has no picture — and is never shown
/// over the button (Preview.cs).
/// </summary>
public sealed record ThumbButton(uint Id, ThumbFlags Flags, string Tip, Pixels? Picture, int Bitmap)
{
    /// <summary>Not hidden: it is drawn and takes its room.</summary>
    public bool Shown => (Flags & ThumbFlags.Hidden) == 0;

    /// <summary>A click on it goes to the window.</summary>
    public bool Clickable => (Flags & (ThumbFlags.Hidden | ThumbFlags.Disabled | ThumbFlags.NonInteractive)) == 0;

    /// <summary>It lights up under the pointer: a button that can be clicked, and has a background.</summary>
    public bool Lights => Clickable && (Flags & ThumbFlags.NoBackground) == 0;

    /// <summary>Drawn faint, as a disabled button is.</summary>
    public bool Faint => (Flags & ThumbFlags.Disabled) != 0;
}

/// <summary>SetThumbnailClip's rectangle: a part of the window's client area, from its top left.</summary>
public readonly record struct ThumbClip(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;

    public int Height => Bottom - Top;

    public bool IsEmpty => Width <= 0 || Height <= 0;
}

/// <summary>What a window's preview shows beyond the window: its toolbar, its tooltip and its clip.</summary>
public sealed record ThumbInfo(IReadOnlyList<ThumbButton> Buttons, string? Tooltip, ThumbClip? Clip)
{
    /// <summary>The buttons that are drawn, in their order.</summary>
    public IReadOnlyList<ThumbButton> Shown => [.. Buttons.Where(button => button.Shown)];
}

/// <summary>Where a toolbar's pictures come from: the app's icons, and the image list it set. Windows' in the band, a fake in the tests.</summary>
internal interface IThumbPictures
{
    /// <summary>The icon <paramref name="icon"/>, drawn; null where it can't be.</summary>
    Pixels? Icon(nint icon);

    /// <summary>
    /// Each picture of the image list written to <paramref name="stream"/> (ImageList_Write's
    /// stream, from its "IL" header on), drawn, in its order; empty where it can't be read.
    /// </summary>
    IReadOnlyList<Pixels?> ImageList(ReadOnlySpan<byte> stream);
}

/// <summary>Thumbnail toolbars: what arrives, what is kept, where it goes and where a click goes.</summary>
internal static class ThumbBar
{
    /// <summary>ILHEAD's usMagic, "IL": where an image list written to a stream starts.</summary>
    public const ushort ImageListMagic = 0x4C49;

    /// <summary>ILHEAD's size: magic, version, count, most, grow, cx, cy, background, flags, four overlays.</summary>
    public const int ImageListHeader = 28;

    /// <summary>How far into the data its image list may start: right at it, or after a few words of the taskbar list's own.</summary>
    private const int ImageListSearch = 32;

    /// <summary>The icons <paramref name="sent"/> carry, drawn through <paramref name="pictures"/>, in step with them (null where a button sent none).</summary>
    public static List<Pixels?> Icons(IReadOnlyList<ThumbButtonData> sent, IThumbPictures pictures) =>
        [.. sent.Select(button => button.IconHandle is { } icon ? pictures.Icon((nint)icon) : null)];

    /// <summary>
    /// Where the image list ThumbBarSetImageList sent starts in <paramref name="data"/>: the first
    /// "IL" header, two-byte aligned, near the start, whose pictures have a size an image list's can
    /// have; -1 where there is none.
    /// </summary>
    public static int ImageListStart(ReadOnlySpan<byte> data)
    {
        for (var at = 0; at <= ImageListSearch && at + ImageListHeader <= data.Length; at += 2)
        {
            if (BinaryPrimitives.ReadUInt16LittleEndian(data[at..]) != ImageListMagic)
            {
                continue;
            }
            var count = BinaryPrimitives.ReadUInt16LittleEndian(data[(at + 4)..]);
            var cx = BinaryPrimitives.ReadUInt16LittleEndian(data[(at + 10)..]);
            var cy = BinaryPrimitives.ReadUInt16LittleEndian(data[(at + 12)..]);
            if (cx is >= 1 and <= 256 && cy is >= 1 and <= 256 && count <= 4096)
            {
                return at;
            }
        }
        return -1;
    }

    /// <summary>The pictures of the image list in <paramref name="data"/>, drawn through <paramref name="pictures"/>; none where it holds none.</summary>
    public static List<Pixels?> Images(ReadOnlySpan<byte> data, IThumbPictures pictures)
    {
        var start = ImageListStart(data);
        return start < 0 ? [] : [.. pictures.ImageList(data[start..])];
    }

    /// <summary>
    /// <paramref name="buttons"/> as they are drawn: each without an icon of its own takes the picture
    /// at its image index in <paramref name="images"/> (the image list its window set), where that has
    /// one. An icon is drawn over an image list's picture, as the taskbar does.
    /// </summary>
    public static IReadOnlyList<ThumbButton> WithImages(IReadOnlyList<ThumbButton> buttons, IReadOnlyList<Pixels?>? images)
    {
        if (images is not { Count: > 0 })
        {
            return buttons;
        }
        return [.. buttons.Select(button => button.Picture is null && button.Bitmap >= 0 && button.Bitmap < images.Count && images[button.Bitmap] is { } picture
            ? button with { Picture = picture }
            : button)];
    }

    /// <summary>THBN_CLICKED: the high word of WM_COMMAND's wParam for a thumbnail button's click.</summary>
    public const uint Clicked = 0x1800;

    /// <summary>The most buttons a toolbar has (ThumbBarAddButtons' documentation).</summary>
    public const int Most = 7;

    /// <summary>One THUMBBUTTON as the taskbar list sends it: four DWORDs, 260 characters, one DWORD.</summary>
    public const int ButtonSize = 16 + 520 + 4;

    /// <summary>
    /// The buttons ThumbBarAddButtons or ThumbBarUpdateButtons sent (see the top of the file): at most
    /// as many as the data holds whole, and never more than <see cref="Most"/>.
    /// </summary>
    public static List<ThumbButtonData> Read(ReadOnlySpan<byte> data)
    {
        var buttons = new List<ThumbButtonData>();
        if (data.Length < 4)
        {
            return buttons;
        }
        var count = (int)Math.Min(BinaryPrimitives.ReadUInt32LittleEndian(data), (uint)Most);
        for (var index = 0; index < count; index++)
        {
            var at = 4 + index * ButtonSize;
            if (at + ButtonSize > data.Length)
            {
                break;
            }
            var button = data.Slice(at, ButtonSize);
            buttons.Add(new ThumbButtonData(
                BinaryPrimitives.ReadUInt32LittleEndian(button),
                BinaryPrimitives.ReadUInt32LittleEndian(button[4..]),
                BinaryPrimitives.ReadUInt32LittleEndian(button[8..]),
                BinaryPrimitives.ReadUInt32LittleEndian(button[12..]),
                BandData.Text(button.Slice(16, 520)),
                BinaryPrimitives.ReadUInt32LittleEndian(button[536..])));
        }
        return buttons;
    }

    /// <summary>
    /// The toolbar after <paramref name="sent"/>: added, it is those buttons with what their masks
    /// give (the rest at its defaults: enabled, no tooltip, no picture); updated, each button of
    /// <paramref name="kept"/> with an id among them takes what its mask gives and keeps the rest, and
    /// ids the toolbar has none of are left out. <paramref name="pictures"/> are the sent buttons'
    /// icons, drawn, in step with them.
    /// </summary>
    public static List<ThumbButton> Merge(IReadOnlyList<ThumbButton>? kept, IReadOnlyList<ThumbButtonData> sent, IReadOnlyList<Pixels?> pictures, bool update)
    {
        if (!update)
        {
            var made = new List<ThumbButton>(sent.Count);
            for (var index = 0; index < sent.Count && made.Count < Most; index++)
            {
                made.Add(Apply(new ThumbButton(sent[index].Id, ThumbFlags.None, "", null, -1), sent[index], Picture(pictures, index)));
            }
            return made;
        }
        var buttons = new List<ThumbButton>(kept ?? []);
        for (var index = 0; index < sent.Count; index++)
        {
            var at = buttons.FindIndex(button => button.Id == sent[index].Id);
            if (at >= 0)
            {
                buttons[at] = Apply(buttons[at], sent[index], Picture(pictures, index));
            }
        }
        return buttons;
    }

    private static Pixels? Picture(IReadOnlyList<Pixels?> pictures, int index) => index < pictures.Count ? pictures[index] : null;

    private static ThumbButton Apply(ThumbButton button, ThumbButtonData sent, Pixels? picture)
    {
        if ((sent.Mask & ThumbButtonData.FlagsMask) != 0)
        {
            button = button with { Flags = (ThumbFlags)(sent.Flags & 0x1F) };
        }
        if ((sent.Mask & ThumbButtonData.TipMask) != 0)
        {
            button = button with { Tip = sent.Tip };
        }
        if ((sent.Mask & ThumbButtonData.IconMask) != 0)
        {
            button = button with { Picture = picture };
        }
        if ((sent.Mask & ThumbButtonData.BitmapMask) != 0)
        {
            button = button with { Bitmap = (int)sent.Bitmap };
        }
        return button;
    }

    /// <summary>The RECT SetThumbnailClip sent; null for anything that is not a clip.</summary>
    public static ThumbClip? Clip(ReadOnlySpan<byte> data)
    {
        if (data.Length < 16)
        {
            return null;
        }
        var clip = new ThumbClip(
            BinaryPrimitives.ReadInt32LittleEndian(data),
            BinaryPrimitives.ReadInt32LittleEndian(data[4..]),
            BinaryPrimitives.ReadInt32LittleEndian(data[8..]),
            BinaryPrimitives.ReadInt32LittleEndian(data[12..]));
        return clip.IsEmpty ? null : clip;
    }

    /// <summary>WM_COMMAND's wParam for a click on button <paramref name="id"/>: THBN_CLICKED over the id.</summary>
    public static nuint Command(uint id) => ((nuint)Clicked << 16) | (id & 0xFFFF);

    /// <summary>
    /// Where <paramref name="count"/> buttons stand in a row <paramref name="room"/> px long from
    /// <paramref name="left"/>, centred in it: <paramref name="button"/> px squares
    /// <paramref name="gap"/> apart where that fits, closer where it does not, and smaller where even
    /// touching they would not fit — seven always do.
    /// </summary>
    public static List<RectF> Place(int count, float left, float top, float room, float button, float gap)
    {
        var rects = new List<RectF>(Math.Max(count, 0));
        if (count <= 0 || room <= 0)
        {
            return rects;
        }
        var side = MathF.Min(button, MathF.Floor(room / count));
        var between = count > 1 ? MathF.Max(0f, MathF.Min(gap, MathF.Floor((room - count * side) / (count - 1)))) : 0f;
        var width = count * side + (count - 1) * between;
        var start = left + MathF.Round((room - width) / 2f, MidpointRounding.AwayFromZero);
        for (var index = 0; index < count; index++)
        {
            rects.Add(new RectF(start + index * (side + between), top, side, side));
        }
        return rects;
    }

    /// <summary>
    /// A click on <paramref name="button"/> of <paramref name="window"/>'s toolbar, through
    /// <paramref name="sender"/>: sent when the button takes clicks. Whether the list then closes
    /// (THBF_DISMISSONCLICK), and whether anything was sent at all.
    /// </summary>
    public static (bool Sent, bool Dismiss) Click(ThumbButton button, nint window, IThumbSender sender)
    {
        if (!button.Clickable)
        {
            return (false, false);
        }
        sender.Clicked(window, button.Id);
        return (true, (button.Flags & ThumbFlags.DismissOnClick) != 0);
    }
}

/// <summary>
/// Windows' way to a toolbar's pictures (<see cref="IThumbPictures"/>): an icon drawn from its
/// handle, and an image list read back from its stream with ImageList_Read and drawn picture by
/// picture (see the top of the file).
/// </summary>
internal sealed unsafe class ThumbPictures : IThumbPictures
{
    public static readonly ThumbPictures Instance = new();

    /// <summary>The most pictures an image list is read for: a toolbar has seven buttons, and a list a few states of each.</summary>
    private const int Most = 64;

    private ThumbPictures()
    {
    }

    public Pixels? Icon(nint icon) => Tray.Render((HICON)icon, Taskband.IconSide((HICON)icon));

    public IReadOnlyList<Pixels?> ImageList(ReadOnlySpan<byte> stream)
    {
        if (stream.IsEmpty)
        {
            return [];
        }
        fixed (byte* bytes = stream)
        {
            var memory = SHCreateMemStream(bytes, (uint)stream.Length);
            if (memory == null)
            {
                return [];
            }
            try
            {
                var list = ImageList_Read(memory);
                if (list == HIMAGELIST.NULL)
                {
                    return [];
                }
                try
                {
                    int cx = 16, cy = 16;
                    _ = ImageList_GetIconSize(list, &cx, &cy);
                    var side = Math.Clamp(Math.Max(cx, cy), 16, 64);
                    var count = Math.Clamp(ImageList_GetImageCount(list), 0, Most);
                    var pictures = new List<Pixels?>(count);
                    for (var index = 0; index < count; index++)
                    {
                        var icon = ImageList_GetIcon(list, index, ILD.ILD_TRANSPARENT);
                        if (icon == HICON.NULL)
                        {
                            pictures.Add(null);
                            continue;
                        }
                        try
                        {
                            pictures.Add(Tray.Render(icon, side));
                        }
                        finally
                        {
                            DestroyIcon(icon);
                        }
                    }
                    return pictures;
                }
                finally
                {
                    _ = ImageList_Destroy(list);
                }
            }
            finally
            {
                _ = memory->Release();
            }
        }
    }
}

/// <summary>Where a thumbnail button's click goes; the taskbar's way in the dock, a fake in the tests.</summary>
public interface IThumbSender
{
    /// <summary>Button <paramref name="id"/> of <paramref name="window"/>'s toolbar was clicked.</summary>
    void Clicked(nint window, uint id);
}

/// <summary>
/// The taskbar's way: WM_COMMAND with THBN_CLICKED and the id, posted to the window, which may take
/// the foreground (a button that shows the window, a player's that opens its own window).
/// </summary>
internal sealed unsafe class ThumbSender : IThumbSender
{
    public static readonly ThumbSender Instance = new();

    private ThumbSender()
    {
    }

    public void Clicked(nint window, uint id)
    {
        uint process = 0;
        _ = GetWindowThreadProcessId((HWND)window, &process);
        if (process != 0)
        {
            _ = AllowSetForegroundWindow(process);
        }
        _ = PostMessageW((HWND)window, WM.WM_COMMAND, ThumbBar.Command(id), 0);
    }
}
