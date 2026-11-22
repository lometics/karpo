// How the taskbar list interface (ITaskbarList to ITaskbarList4) reaches the taskbar, and Karpo's own
// band, which takes it in, keeps what the bars draw, and passes every call on to Explorer's.
//
// The interface is a COM object in each app's own process (explorerframe.dll). HrInit looks up the
// first Shell_TrayWnd — Karpo's tray window while it runs (Tray.cs) — and its TaskbandHWND property,
// and keeps that window for the object's life: every call after that is a message to it. Measured
// on a desktop of its own with nothing of Explorer's on it (a fake Shell_TrayWnd whose TaskbandHWND
// was a logging window, and a second process calling every method), this is what arrives, wParam
// first:
//
//   WM_USER+50  ActivateTab, SetActiveAlt  sent   0, the window
//   WM_USER+60  MarkFullscreenWindow       sent   entering (1) or leaving (0), the window
//   WM_USER+64  SetProgressValue           posted the window, the share done scaled to 0-0xFFFE
//   WM_USER+65  SetProgressState           posted the window, the TBPF flags
//   WM_USER+67  RegisterTab                posted the tab's window, the window it belongs to
//   WM_USER+68  UnregisterTab              posted the tab's window, 0
//   WM_USER+71  SetTabOrder                posted the tab's window, the tab it goes before
//   WM_USER+72  SetTabActive               posted the window the tabs belong to, 0
//   WM_USER+76  ThumbBarAddButtons         sent   the window, a data atom: a count and THUMBBUTTONs
//   WM_USER+77  ThumbBarUpdateButtons      sent   the same
//   WM_USER+78  ThumbBarSetImageList       sent   the window, a data atom: the image list as ImageList_Write
//                                                 wrote it (not measured: see below and ThumbBar.cs)
//   WM_USER+79  SetOverlayIcon             sent   the window, a copy of the icon (0 for none)
//   WM_USER+80  SetThumbnailTooltip        sent   the window, a data atom: the text (0, notified, for none)
//   WM_USER+81  SetThumbnailClip           sent   the window, a data atom: the RECT
//   WM_USER+85  SetOverlayIcon's text      sent   the window, a data atom: the text (0, notified, for none)
//   WM_USER+87  SetTabProperties           posted the tab's window, the STPF flags
//   SHELLHOOK   AddTab, DeleteTab, ActivateTab: the registered shell hook message itself, sent to
//               the band rather than to the hook windows, HSHELL_WINDOWCREATED (1), _DESTROYED (2)
//               or _ACTIVATED (4) and the window
//
// ThumbBarSetImageList sent nothing there: it failed inside the app's own process (E_FAIL) before a
// message left it, with every image list tried, so what it sends to Explorer could not be seen. What
// it must send is an image list written out (ExplorerFrame imports ImageList_Write, Taskbar.dll
// ImageList_Read): the band reads WM_USER+78's data as that, and any other sent message's whose data
// starts with an image list's "IL" header, for the window, and passes it on as it came. SetThumbnailClip(NULL), the documented way to take a
// clip away, fails the same way (RPC_X_NULL_REF_POINTER) and sends nothing either.
//
// A "data atom" is what SHAllocShared hands back on this Windows: not the section's handle itself
// but a global atom whose 32-character name holds 24 bytes, six bits a character from the lowest up,
// 0 written as '`' and anything else as 0x20 plus the digit — the signature "MARC", four bytes of 0,
// then the section's handle and the id of the process it is valid in, eight bytes each. The app
// makes it for the band window's process, so the handle is the band's; the section is a 16-byte
// header — the data's size, the signature 0xBAFF1AFF, the process that made it and the one it is
// for — and the data. Once the call returns the app frees it (SHFreeShared): the atom, and the handle
// in the band's process, which is closed by then; the band must not close it itself.
//
// Passed on as it came, such a message would reach Explorer naming memory made for Karpo, in Karpo's
// process — which is what an appbar message did (AppbarRelay.cs), and Explorer acted on nothing. So
// the band makes it again for Explorer: the data copied into memory SHAllocShared makes for
// Explorer's process, whatever that hands back passed as it is, and freed with SHFreeShared once
// Explorer has answered — as the app does for the band. (Wrapped in a data atom of Karpo's own, it
// named an atom where Explorer looked for a handle: the round trip test caught that.) Everything else
// is passed on as it came, the way it came: posted, sent, or sent without waiting. That keeps
// Explorer's own taskbar right while Karpo runs and after it quits.
//
// The window procedure never touches the dock, as the tray's doesn't: it keeps what the message says
// (AppSignals) and posts WM_KARPO_SIGNALS for the dock to read it in its own time.

using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;
using Karpo.Core;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

/// <summary>The messages the taskbar list sends its band (see the top of Taskband.cs).</summary>
internal static class BandMessage
{
    public const uint ActivateTab = WM.WM_USER + 50;
    public const uint FullscreenWindow = WM.WM_USER + 60;
    public const uint ProgressValue = WM.WM_USER + 64;
    public const uint ProgressState = WM.WM_USER + 65;
    public const uint RegisterTab = WM.WM_USER + 67;
    public const uint UnregisterTab = WM.WM_USER + 68;
    public const uint TabOrder = WM.WM_USER + 71;
    public const uint TabActive = WM.WM_USER + 72;
    public const uint ThumbBarAdd = WM.WM_USER + 76;
    public const uint ThumbBarUpdate = WM.WM_USER + 77;
    public const uint ThumbBarImages = WM.WM_USER + 78;
    public const uint OverlayIcon = WM.WM_USER + 79;
    public const uint ThumbnailTooltip = WM.WM_USER + 80;
    public const uint ThumbnailClip = WM.WM_USER + 81;
    public const uint OverlayText = WM.WM_USER + 85;
    public const uint TabProperties = WM.WM_USER + 87;
}

/// <summary>
/// The name of a data atom: a section's handle and the process it is valid in, packed into 32
/// characters (see the top of Taskband.cs).
/// </summary>
internal static class DataAtom
{
    /// <summary>"MARC", as the first four bytes read.</summary>
    public const uint Magic = 0x4352_414D;

    /// <summary>Characters in a name: 24 bytes at six bits each.</summary>
    public const int Length = 32;

    private const int Bytes = 24;

    /// <summary>The name for <paramref name="handle"/>, valid in <paramref name="process"/>.</summary>
    public static string Encode(nint handle, uint process)
    {
        Span<byte> raw = stackalloc byte[Bytes];
        raw.Clear();
        BinaryPrimitives.WriteUInt32LittleEndian(raw, Magic);
        BinaryPrimitives.WriteInt64LittleEndian(raw[8..], handle);
        BinaryPrimitives.WriteUInt64LittleEndian(raw[16..], process);
        Span<char> name = stackalloc char[Length];
        for (var index = 0; index < Length; index++)
        {
            var digit = 0;
            for (var bit = 0; bit < 6; bit++)
            {
                var at = index * 6 + bit;
                if ((raw[at / 8] & (1 << (at % 8))) != 0)
                {
                    digit |= 1 << bit;
                }
            }
            name[index] = digit == 0 ? '`' : (char)(0x20 + digit);
        }
        return new string(name);
    }

    /// <summary>The handle and process a name holds; false for anything that is not one.</summary>
    public static bool TryDecode(ReadOnlySpan<char> name, out nint handle, out uint process)
    {
        handle = 0;
        process = 0;
        if (name.Length != Length)
        {
            return false;
        }
        Span<byte> raw = stackalloc byte[Bytes];
        raw.Clear();
        for (var index = 0; index < Length; index++)
        {
            var c = name[index];
            int digit;
            if (c == '`')
            {
                digit = 0;
            }
            else if (c is > ' ' and <= '_')
            {
                digit = c - 0x20;
            }
            else
            {
                return false;
            }
            for (var bit = 0; bit < 6; bit++)
            {
                if ((digit & (1 << bit)) != 0)
                {
                    var at = index * 6 + bit;
                    raw[at / 8] |= (byte)(1 << (at % 8));
                }
            }
        }
        var id = BinaryPrimitives.ReadUInt64LittleEndian(raw[16..]);
        if (BinaryPrimitives.ReadUInt32LittleEndian(raw) != Magic || id > uint.MaxValue)
        {
            return false;
        }
        handle = (nint)BinaryPrimitives.ReadInt64LittleEndian(raw[8..]);
        process = (uint)id;
        return handle != 0;
    }
}

/// <summary>
/// What a data atom's section holds (SHAllocShared's layout, see the top of Taskband.cs), and the
/// shapes the taskbar list writes into it.
/// </summary>
internal static class BandData
{
    /// <summary>SHAllocShared's header: the data's size, the signature, and the two processes.</summary>
    public const int HeaderSize = 16;

    public const uint Signature = 0xBAFF_1AFF;

    /// <summary>
    /// The data of a section mapped at <paramref name="mapping"/> (the whole mapping, header first);
    /// null when it is not SHAllocShared's or claims more than the mapping holds.
    /// </summary>
    public static byte[]? Data(ReadOnlySpan<byte> mapping)
    {
        if (mapping.Length < HeaderSize || BinaryPrimitives.ReadUInt32LittleEndian(mapping[4..]) != Signature)
        {
            return null;
        }
        var size = BinaryPrimitives.ReadUInt32LittleEndian(mapping);
        return size > (uint)(mapping.Length - HeaderSize) ? null : mapping.Slice(HeaderSize, (int)size).ToArray();
    }

    /// <summary>
    /// A string the taskbar list sent (an overlay's description, a thumbnail's tooltip): UTF-16 up
    /// to its terminating zero, or to the end of the data.
    /// </summary>
    public static string Text(ReadOnlySpan<byte> data)
    {
        var chars = MemoryMarshal.Cast<byte, char>(data[..(data.Length & ~1)]);
        var end = chars.IndexOf('\0');
        return new string(end < 0 ? chars : chars[..end]);
    }
}

/// <summary>
/// What the band needs of Windows, so passing a message on can be tested without it: data atoms, the
/// shell's shared memory, and the message calls to Explorer's band.
/// </summary>
internal interface IBandSystem
{
    /// <summary>The data a data atom (<paramref name="atom"/>) holds when it names memory in this process; null otherwise.</summary>
    byte[]? Read(nint atom);

    /// <summary>
    /// <paramref name="data"/> in memory made for <paramref name="process"/> (SHAllocShared): what to
    /// pass in its place, or 0 when it can't be made.
    /// </summary>
    nint Make(ReadOnlySpan<byte> data, uint process);

    /// <summary>Lets go of what <see cref="Make"/> made for <paramref name="process"/> (SHFreeShared).</summary>
    void Unmake(nint made, uint process);

    /// <summary>Sends and waits, bounded; null when it never got there.</summary>
    nint? Send(nint to, uint msg, nuint wParam, nint lParam);

    /// <summary>Posts.</summary>
    void Post(nint to, uint msg, nuint wParam, nint lParam);

    /// <summary>Sends without waiting (SendNotifyMessage).</summary>
    void Notify(nint to, uint msg, nuint wParam, nint lParam);
}

/// <summary>How a message reached the band (InSendMessageEx), which is how it is passed on.</summary>
internal enum BandCall
{
    Posted,
    Sent,

    /// <summary>SendNotifyMessage: sent, and nobody waits.</summary>
    Notified,
}

/// <summary>
/// Passes a message the band took in on to Explorer's band (see the top of Taskband.cs): a sent one
/// whose lParam is a data atom with its data made again for Explorer, anything else as it came.
/// </summary>
internal static class BandRelay
{
    /// <summary>
    /// Passes <paramref name="msg"/> on to <paramref name="explorer"/>'s band window
    /// <paramref name="band"/> the way it came; <paramref name="data"/> is what its data atom held
    /// (null when it had none). What Explorer answered, or 0 where nothing waits or it never got there.
    /// </summary>
    public static nint Pass(IBandSystem system, nint band, uint explorer, uint msg, nuint wParam, nint lParam, BandCall call, byte[]? data)
    {
        if (band == 0)
        {
            return 0;
        }
        switch (call)
        {
            case BandCall.Posted:
                system.Post(band, msg, wParam, lParam);
                return 0;
            case BandCall.Notified:
                system.Notify(band, msg, wParam, lParam);
                return 0;
        }
        if (data is null || explorer == 0)
        {
            return system.Send(band, msg, wParam, lParam) ?? 0;
        }
        var made = system.Make(data, explorer);
        if (made == 0)
        {
            // Not made for Explorer: as it came, which is still more than nothing.
            return system.Send(band, msg, wParam, lParam) ?? 0;
        }
        try
        {
            return system.Send(band, msg, wParam, made) ?? 0;
        }
        finally
        {
            system.Unmake(made, explorer);
        }
    }
}

/// <summary>
/// Karpo's band: the window the taskbar list talks to while Karpo's tray window is the first
/// Shell_TrayWnd (see the top of Taskband.cs). Made by the tray, which points its TaskbandHWND here.
/// </summary>
internal sealed unsafe class Taskband : IDisposable
{
    /// <summary>Posted to the dock when a window's signals changed; the dock reads <see cref="AppSignals.Shared"/>.</summary>
    public const uint WM_KARPO_SIGNALS = WM.WM_APP + 0x24;

    private const string ClassName = "Karpo.Taskband";
    private const string BandProperty = "TaskbandHWND";

    // InSendMessageEx's answers.
    private const uint ISMEX_SEND = 0x1;
    private const uint ISMEX_NOTIFY = 0x2;

    // What the window procedure works with. Static, not per instance: a window procedure gets no
    // context, and there is one band at a time.
    private static HWND target;
    private static uint shellHook;
    private static HWND own;
    private static AppSignals signals = AppSignals.Shared;

    private readonly HWND band;
    private bool disposed;

    private Taskband(HWND band) => this.band = band;

    /// <summary>The band's window, for the tray's TaskbandHWND.</summary>
    public HWND Hwnd => band;

    /// <summary>
    /// The band, keeping what it hears in <paramref name="into"/> (the shared signals, which the bars
    /// read, unless a test gives its own) and telling <paramref name="dock"/> when a window's signals
    /// change; null when its window can't be made, and then the tray lends Explorer's own band instead.
    /// </summary>
    public static Taskband? Start(HWND dock, HINSTANCE instance, AppSignals? into = null)
    {
        fixed (char* className = ClassName)
        {
            var type = new WNDCLASSW { lpfnWndProc = &Procedure, hInstance = instance, lpszClassName = className };
            // Fails when an earlier band registered it, which is fine: same procedure.
            RegisterClassW(&type);
            // Never shown, like the tray's window: a plain hidden window is what the taskbar list
            // was seen talking to.
            var window = CreateWindowExW((uint)WS.WS_EX_TOOLWINDOW, className, null, WS.WS_POPUP, 0, 0, 0, 0, HWND.NULL, HMENU.NULL, instance, null);
            if (window == HWND.NULL)
            {
                return null;
            }
            fixed (char* hook = "SHELLHOOK")
            {
                shellHook = RegisterWindowMessageW(hook);
            }
            target = dock;
            own = window;
            signals = into ?? AppSignals.Shared;
            return new Taskband(window);
        }
    }

    /// <summary>
    /// Explorer's own band, where everything is passed on to: the TaskbandHWND of Explorer's
    /// Shell_TrayWnd, never one of Karpo's own windows.
    /// </summary>
    public static HWND ExplorerBand(out uint process)
    {
        process = 0;
        var tray = Taskbar.ExplorerTray();
        if (tray == HWND.NULL)
        {
            return HWND.NULL;
        }
        HWND band;
        fixed (char* name = BandProperty)
        {
            band = (HWND)GetPropW(tray, name).Value;
        }
        if (band == HWND.NULL || band == own)
        {
            return HWND.NULL;
        }
        uint found = 0;
        _ = GetWindowThreadProcessId(band, &found);
        if (found == 0 || found == GetCurrentProcessId())
        {
            return HWND.NULL;
        }
        process = found;
        return band;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }
        disposed = true;
        Surfaces.Gone(band);
        if (own == band)
        {
            own = HWND.NULL;
            target = HWND.NULL;
        }
    }

    [UnmanagedCallersOnly]
    private static LRESULT Procedure(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        if ((msg is >= WM.WM_USER and < WM.WM_APP) || (shellHook != 0 && msg == shellHook))
        {
            try
            {
                return Take(msg, wParam, lParam);
            }
            catch (Exception error)
            {
                // Nothing may unwind into user32, and a message that broke here must not keep the
                // app waiting: it is still passed on, as it came. Every app's taskbar calls come
                // here, so this runs as apps start; passing it on is guarded too, for the same reason.
                Troubles.Report($"Taskbar list: message 0x{msg:X} not read", error);
                try
                {
                    return Relay(msg, wParam, lParam, CallOf(), null);
                }
                catch (Exception again)
                {
                    Troubles.Report($"Taskbar list: message 0x{msg:X} not passed on", again);
                    return 0;
                }
            }
        }
        return Closing.Default(hwnd, msg, wParam, lParam);
    }

    /// <summary>Keeps what the message says, tells the dock when that changed, and passes it on.</summary>
    private static LRESULT Take(uint msg, WPARAM wParam, LPARAM lParam)
    {
        // Asked first: how the message came is how it is passed on.
        var call = CallOf();
        var window = (nint)(nuint)wParam;
        // Only a sent message carries a data atom: the app frees it once the call returns, so a
        // posted one never could. A posted lParam that happens to lie in the atoms' range (a window,
        // a flag) is not looked up.
        var data = call == BandCall.Sent ? Data(lParam) : null;
        var changed = false;
        switch (msg)
        {
            // app clicks: ActivateTab and SetActiveAlt send the window as lParam: its button shows active.
            case BandMessage.ActivateTab:
                changed = signals.ActiveTab((nint)lParam);
                break;
            case BandMessage.ProgressState:
                changed = signals.ProgressState(window, (uint)(nint)lParam);
                break;
            case BandMessage.ProgressValue:
                changed = signals.ProgressValue(window, lParam);
                break;
            case BandMessage.OverlayIcon:
                // Drawn now: the icon is the taskbar list's copy, destroyed once the call returns.
                var icon = (HICON)(nint)lParam;
                changed = signals.Overlay(window, icon == HICON.NULL ? null : Tray.Render(icon, IconSide(icon)));
                break;
            case BandMessage.OverlayText:
                changed = signals.OverlayText(window, data is null ? "" : BandData.Text(data));
                break;
            case BandMessage.ThumbBarAdd:
            case BandMessage.ThumbBarUpdate:
                if (data is not null)
                {
                    var sent = ThumbBar.Read(data);
                    // Drawn now, as the overlay is: the icons are the app's, and may go with the call.
                    changed = signals.ThumbButtons(window, sent, ThumbBar.Icons(sent, ThumbPictures.Instance), update: msg == BandMessage.ThumbBarUpdate);
                }
                break;
            case BandMessage.ThumbBarImages:
                // The image list THB_BITMAP buttons take their pictures from, drawn now: the stream
                // goes with the call. None sent (a list taken away) is none kept.
                changed = signals.ThumbImages(window, data is null ? [] : ThumbBar.Images(data, ThumbPictures.Instance));
                break;
            case BandMessage.ThumbnailTooltip:
                changed = signals.ThumbnailTooltip(window, data is null ? null : BandData.Text(data));
                break;
            case BandMessage.ThumbnailClip:
                changed = signals.ThumbnailClip(window, data is null ? null : ThumbBar.Clip(data));
                break;
            case BandMessage.RegisterTab:
                changed = signals.RegisterTab(window, (nint)lParam);
                break;
            case BandMessage.UnregisterTab:
                changed = signals.UnregisterTab(window);
                break;
            case BandMessage.TabOrder:
                changed = signals.TabOrder(window, (nint)lParam);
                break;
            default:
                // An image list under another number than the one counted on: its stream says what it is.
                if (data is not null && msg is > BandMessage.ThumbBarAdd and < BandMessage.TabProperties && ThumbBar.ImageListStart(data) >= 0)
                {
                    changed = signals.ThumbImages(window, ThumbBar.Images(data, ThumbPictures.Instance));
                }
                break;
        }
        if (changed && target != HWND.NULL)
        {
            PostMessageW(target, WM_KARPO_SIGNALS, default, default);
        }
        return Relay(msg, wParam, lParam, call, data);
    }

    /// <summary>How the message being handled came (InSendMessageEx).</summary>
    private static BandCall CallOf()
    {
        var how = InSendMessageEx(null);
        return (how & ISMEX_NOTIFY) != 0 ? BandCall.Notified : (how & ISMEX_SEND) != 0 ? BandCall.Sent : BandCall.Posted;
    }

    /// <summary>Passes the message on to Explorer's band (BandRelay), the way it came.</summary>
    private static LRESULT Relay(uint msg, WPARAM wParam, LPARAM lParam, BandCall call, byte[]? data)
    {
        var explorer = ExplorerBand(out var process);
        return BandRelay.Pass(BandSystem.Instance, (nint)explorer, process, msg, (nuint)wParam, (nint)lParam, call, data);
    }

    /// <summary>What a data atom in <paramref name="lParam"/> holds, when it is one naming memory in this process.</summary>
    private static byte[]? Data(LPARAM lParam) => BandSystem.Instance.Read((nint)lParam);

    /// <summary>The side an icon was drawn at, so its picture is taken without scaling: 16 to 64 px.</summary>
    internal static int IconSide(HICON icon)
    {
        ICONINFO info;
        if (!GetIconInfo(icon, &info))
        {
            return 16;
        }
        try
        {
            BITMAP bitmap;
            var picture = info.hbmColor != HBITMAP.NULL ? info.hbmColor : info.hbmMask;
            if (picture == HBITMAP.NULL || GetObjectW((HGDIOBJ)picture.Value, sizeof(BITMAP), &bitmap) == 0)
            {
                return 16;
            }
            return Math.Clamp(bitmap.bmWidth, 16, 64);
        }
        finally
        {
            if (info.hbmColor != HBITMAP.NULL)
            {
                DeleteObject((HGDIOBJ)info.hbmColor.Value);
            }
            if (info.hbmMask != HBITMAP.NULL)
            {
                DeleteObject((HGDIOBJ)info.hbmMask.Value);
            }
        }
    }
}

/// <summary>The band's Windows (<see cref="IBandSystem"/>): global atoms, the shell's shared memory, and messages.</summary>
internal sealed unsafe class BandSystem : IBandSystem
{
    public static readonly BandSystem Instance = new();

    /// <summary>How long Explorer's band is given to answer, as the tray gives Explorer's taskbar.</summary>
    private const uint TimeoutMs = 4000;

    private BandSystem()
    {
    }

    public byte[]? Read(nint atom)
    {
        if (atom is < 0xC000 or > 0xFFFF)
        {
            return null;
        }
        var name = stackalloc char[DataAtom.Length + 2];
        var length = GlobalGetAtomNameW((ushort)atom, name, DataAtom.Length + 2);
        if (length != DataAtom.Length
            || !DataAtom.TryDecode(new ReadOnlySpan<char>(name, (int)length), out var handle, out var process)
            || process != GetCurrentProcessId())
        {
            return null;
        }
        // The app's handle, in this process: mapped to read, and never closed here (the app closes it).
        var view = MapViewOfFile((HANDLE)handle, FILE.FILE_MAP_READ, 0, 0, 0);
        if (view == null)
        {
            return null;
        }
        try
        {
            MEMORY_BASIC_INFORMATION region;
            if (VirtualQuery(view, &region, (nuint)sizeof(MEMORY_BASIC_INFORMATION)) == 0)
            {
                return null;
            }
            var size = (int)Math.Min((long)(nuint)region.RegionSize, 1 << 20);
            return BandData.Data(new ReadOnlySpan<byte>(view, size));
        }
        finally
        {
            UnmapViewOfFile(view);
        }
    }

    // SHAllocShared hands back the data atom itself, as the app's own call did, so what it gives is
    // what goes in the message; SHFreeShared takes the same.
    public nint Make(ReadOnlySpan<byte> data, uint process) => ShellSharedMemory.Instance.Alloc(data, process);

    public void Unmake(nint made, uint process) => ShellSharedMemory.Instance.Free(made, process);

    public nint? Send(nint to, uint msg, nuint wParam, nint lParam)
    {
        nuint result = 0;
        var sent = SendMessageTimeoutW((HWND)to, msg, wParam, lParam, (uint)(SMTO_ABORTIFHUNG | SMTO_NORMAL), TimeoutMs, &result);
        return sent.Value == 0 ? null : (nint)result;
    }

    public void Post(nint to, uint msg, nuint wParam, nint lParam) => PostMessageW((HWND)to, msg, wParam, lParam);

    public void Notify(nint to, uint msg, nuint wParam, nint lParam) => SendNotifyMessageW((HWND)to, msg, wParam, lParam);
}
