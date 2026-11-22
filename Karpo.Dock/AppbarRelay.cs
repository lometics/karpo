// What Karpo's tray does to an appbar message it passes on to Explorer (Tray.ForwardAppbar).
//
// SHAppBarMessage sends its message to the first Shell_TrayWnd it finds, and while Karpo's tray is that
// window, every app's appbar messages — the bars' own included — reach Explorer through it. For the
// three that answer with a rectangle (ABM_QUERYPOS, ABM_SETPOS, ABM_GETTASKBARPOS) the answer travels
// in shared memory, and SHAppBarMessage makes that memory for the process of the window it found,
// naming that process in the message. Passed on as it came, the message reached Explorer with memory
// made for Karpo and Karpo named; Explorer answered TRUE and did nothing — the position was never
// set, the query never answered. Asked of the real taskbar with a message made that way, read-only
// (ABM_GETTASKBARPOS): the rectangle came back untouched, where the same message with its memory made
// for Explorer came back with the taskbar's. That is the start on the user's PC where the strips were
// asked for through Karpo's tray and neither display took its own until the bar asked again, directly;
// starts where Explorer seemed to act on nothing for seconds may have been this too.
//
// So the tray passes such a message on with its memory made again for Explorer: what the caller put
// in its memory copied into memory made for Explorer's process, the message naming that, and what
// Explorer writes there copied back into the caller's own memory, where SHAppBarMessage reads it.
//
// The message's layout is what SHAppBarMessage sends on 64-bit Windows, seen on a desktop of its own
// with nothing of Explorer's on it: 64 bytes — the APPBARDATA of 32-bit handles (40 bytes: size, window,
// callback message, edge, rectangle, lParam), the message at 40, the memory's handle as 64 bits at 48
// and the process it was made for at 56. Anything else — another size, no memory, memory made for any
// process but this one, memory that will not map — is passed on as it came, as it always was.

using System.Buffers.Binary;
using System.Runtime.InteropServices;
using TerraFX.Interop.Windows;

namespace Karpo.Dock;

/// <summary>The shell's shared memory, behind an interface so the relay can be tested without it.</summary>
internal interface ISharedMemory
{
    /// <summary>SHAllocShared: <paramref name="data"/> in memory made for <paramref name="process"/>; the handle there, or 0.</summary>
    nint Alloc(ReadOnlySpan<byte> data, uint process);

    /// <summary>SHLockShared: memory made for <paramref name="process"/>, mapped into this one; null when it will not map.</summary>
    unsafe byte* Lock(nint handle, uint process);

    /// <summary>SHUnlockShared.</summary>
    unsafe void Unlock(byte* view);

    /// <summary>SHFreeShared: the memory made for <paramref name="process"/> is let go.</summary>
    void Free(nint handle, uint process);
}

/// <summary>Sends an appbar message on to Explorer's taskbar: what it answers, or null where it never got there.</summary>
internal unsafe delegate nint? AppbarSend(COPYDATASTRUCT* message);

/// <summary>The appbar messages Karpo's tray passes on, with their shared memory made for Explorer (see the top of the file).</summary>
internal static unsafe class AppbarRelay
{
    /// <summary>What SHAppBarMessage sends on 64-bit Windows.</summary>
    public const int MessageSize = 64;

    /// <summary>The APPBARDATA of 32-bit handles at its start, and in the shared memory.</summary>
    public const int DataSize = 40;

    private const int HandleAt = 48;
    private const int ProcessAt = 56;

    /// <summary>
    /// Passes <paramref name="data"/>, an appbar message (WM_COPYDATA 0) that reached this process's
    /// window (<paramref name="own"/>), on with <paramref name="send"/> — its memory made again for
    /// <paramref name="explorer"/>'s process where it was made for this one, and Explorer's answer
    /// copied back. What <paramref name="send"/> answers; null where it never reached Explorer.
    /// </summary>
    public static nint? Pass(COPYDATASTRUCT* data, uint own, uint explorer, ISharedMemory memory, AppbarSend send)
    {
        if (data == null || data->lpData == null || data->cbData != MessageSize || explorer == 0 || explorer == own)
        {
            return send(data);
        }
        var message = new ReadOnlySpan<byte>(data->lpData, MessageSize);
        var handle = (nint)BinaryPrimitives.ReadInt64LittleEndian(message[HandleAt..]);
        if (handle == 0 || BinaryPrimitives.ReadUInt32LittleEndian(message[ProcessAt..]) != own)
        {
            return send(data);
        }
        var block = stackalloc byte[DataSize];
        var theirs = (nint)0;
        var view = memory.Lock(handle, own);
        if (view == null)
        {
            return send(data);
        }
        try
        {
            new ReadOnlySpan<byte>(view, DataSize).CopyTo(new Span<byte>(block, DataSize));
        }
        finally
        {
            memory.Unlock(view);
        }
        theirs = memory.Alloc(new ReadOnlySpan<byte>(block, DataSize), explorer);
        if (theirs == 0)
        {
            return send(data);
        }
        try
        {
            var copy = stackalloc byte[MessageSize];
            message.CopyTo(new Span<byte>(copy, MessageSize));
            BinaryPrimitives.WriteInt64LittleEndian(new Span<byte>(copy + HandleAt, 8), theirs);
            BinaryPrimitives.WriteUInt32LittleEndian(new Span<byte>(copy + ProcessAt, 4), explorer);
            var passed = new COPYDATASTRUCT { dwData = data->dwData, cbData = MessageSize, lpData = copy };
            var answer = send(&passed);
            // What Explorer wrote, into the caller's own memory, where SHAppBarMessage reads it.
            var back = memory.Lock(theirs, explorer);
            if (back != null)
            {
                try
                {
                    var mine = memory.Lock(handle, own);
                    if (mine != null)
                    {
                        new ReadOnlySpan<byte>(back, DataSize).CopyTo(new Span<byte>(mine, DataSize));
                        memory.Unlock(mine);
                    }
                }
                finally
                {
                    memory.Unlock(back);
                }
            }
            return answer;
        }
        finally
        {
            memory.Free(theirs, explorer);
        }
    }
}

/// <summary>The shell's own shared memory (shlwapi).</summary>
internal sealed unsafe class ShellSharedMemory : ISharedMemory
{
    public static readonly ShellSharedMemory Instance = new();

    private ShellSharedMemory()
    {
    }

    public nint Alloc(ReadOnlySpan<byte> data, uint process)
    {
        fixed (byte* bytes = data)
        {
            return SHAllocShared(bytes, (uint)data.Length, process);
        }
    }

    public byte* Lock(nint handle, uint process) => (byte*)SHLockShared(handle, process);

    public void Unlock(byte* view) => _ = SHUnlockShared(view);

    public void Free(nint handle, uint process) => _ = SHFreeShared(handle, process);

    [DllImport("shlwapi.dll", ExactSpelling = true)]
    private static extern nint SHAllocShared(void* data, uint size, uint process);

    [DllImport("shlwapi.dll", ExactSpelling = true)]
    private static extern void* SHLockShared(nint handle, uint process);

    [DllImport("shlwapi.dll", ExactSpelling = true)]
    private static extern int SHUnlockShared(void* view);

    [DllImport("shlwapi.dll", ExactSpelling = true)]
    private static extern int SHFreeShared(nint handle, uint process);
}
