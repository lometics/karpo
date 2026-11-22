// What Karpo's tray does to an appbar message it passes on to Explorer (Karpo.Dock\AppbarRelay.cs):
// the shared memory the answer travels in, made again for Explorer. Against shared memory faked in
// this process and an Explorer that is a function here: nothing reaches Explorer, the shell or any
// other process. The last test maps the shell's own shared memory, made for this process alone.
//
// The regression: while Karpo's tray was the first Shell_TrayWnd, every ABM_SETPOS and ABM_QUERYPOS
// went to Explorer with memory made for Karpo, and Explorer answered TRUE and did nothing — on the
// user's PC both displays' first strips after the takeover, dropped.

using System.Buffers.Binary;
using System.Runtime.InteropServices;
using Karpo.Dock;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Docking;

public sealed unsafe class AppbarRelayTests
{
    private const uint Karpo = 4_100;
    private const uint Explorer = 11_064;
    private const uint Someone = 7_777;
    private const uint SetPos = 3;

    private static readonly RECT Asked = new() { left = 0, top = 2107, right = 3840, bottom = 2160 };
    private static readonly RECT Answered = new() { left = 0, top = 2035, right = 3840, bottom = 2088 };

    /// <summary>
    /// Shared memory as SHAllocShared makes it: blocks by handle, each made for one process and mapped
    /// only by its handle together with that process, every view counted and every block let go.
    /// </summary>
    private sealed class FakeMemory : ISharedMemory, IDisposable
    {
        private readonly Dictionary<nint, (uint Process, nint Block)> blocks = [];
        private nint next = 0xC000;

        /// <summary>Views mapped and not yet unmapped.</summary>
        public int Views;

        /// <summary>The blocks let go, and for which process.</summary>
        public readonly List<(nint Handle, uint Process)> Freed = [];

        /// <summary>SHAllocShared fails.</summary>
        public bool RefuseAlloc;

        /// <summary>The blocks made, and for which process.</summary>
        public readonly List<(nint Handle, uint Process)> Made = [];

        public nint Add(ReadOnlySpan<byte> data, uint process)
        {
            var block = Marshal.AllocHGlobal(data.Length);
            data.CopyTo(new Span<byte>((void*)block, data.Length));
            next += 4;
            blocks[next] = (process, block);
            return next;
        }

        public nint Alloc(ReadOnlySpan<byte> data, uint process)
        {
            if (RefuseAlloc)
            {
                return 0;
            }
            var handle = Add(data, process);
            Made.Add((handle, process));
            return handle;
        }

        public byte* Lock(nint handle, uint process)
        {
            if (!blocks.TryGetValue(handle, out var block) || block.Process != process)
            {
                return null;
            }
            Views++;
            return (byte*)block.Block;
        }

        public void Unlock(byte* view) => Views--;

        public void Free(nint handle, uint process)
        {
            Freed.Add((handle, process));
            if (blocks.Remove(handle, out var block))
            {
                Marshal.FreeHGlobal(block.Block);
            }
        }

        public bool Holds(nint handle) => blocks.ContainsKey(handle);

        public byte[] Read(nint handle) => new ReadOnlySpan<byte>((void*)blocks[handle].Block, AppbarRelay.DataSize).ToArray();

        public void Dispose()
        {
            foreach (var (_, block) in blocks.Values)
            {
                Marshal.FreeHGlobal(block);
            }
            blocks.Clear();
        }
    }

    /// <summary>The APPBARDATA of 32-bit handles, as SHAppBarMessage puts it in the memory and the message.</summary>
    private static byte[] Block(RECT rc)
    {
        var block = new byte[AppbarRelay.DataSize];
        BinaryPrimitives.WriteUInt32LittleEndian(block, AppbarRelay.DataSize);
        BinaryPrimitives.WriteUInt32LittleEndian(block.AsSpan(4), 0x1234);
        BinaryPrimitives.WriteUInt32LittleEndian(block.AsSpan(8), 0x8001);
        BinaryPrimitives.WriteUInt32LittleEndian(block.AsSpan(12), 3);
        Write(block, rc);
        return block;
    }

    private static void Write(Span<byte> block, RECT rc)
    {
        BinaryPrimitives.WriteInt32LittleEndian(block[16..], rc.left);
        BinaryPrimitives.WriteInt32LittleEndian(block[20..], rc.top);
        BinaryPrimitives.WriteInt32LittleEndian(block[24..], rc.right);
        BinaryPrimitives.WriteInt32LittleEndian(block[28..], rc.bottom);
    }

    private static RECT Rect(ReadOnlySpan<byte> block) => new()
    {
        left = BinaryPrimitives.ReadInt32LittleEndian(block[16..]),
        top = BinaryPrimitives.ReadInt32LittleEndian(block[20..]),
        right = BinaryPrimitives.ReadInt32LittleEndian(block[24..]),
        bottom = BinaryPrimitives.ReadInt32LittleEndian(block[28..]),
    };

    /// <summary>What SHAppBarMessage sends on 64-bit Windows: the block, the message at 40, the memory's handle at 48 and its process at 56.</summary>
    private static byte[] Message(ReadOnlySpan<byte> block, uint abm, nint handle, uint process)
    {
        var message = new byte[AppbarRelay.MessageSize];
        block.CopyTo(message);
        BinaryPrimitives.WriteUInt32LittleEndian(message.AsSpan(40), abm);
        BinaryPrimitives.WriteInt64LittleEndian(message.AsSpan(48), handle);
        BinaryPrimitives.WriteUInt32LittleEndian(message.AsSpan(56), process);
        return message;
    }

    /// <summary>
    /// Explorer, as the read-only probe of the real one showed it: it maps the memory a message names
    /// only where it was made for Explorer, and writes its answer there; memory made for any other
    /// process it leaves alone, and answers TRUE all the same.
    /// </summary>
    private static nint? ExplorerAnswers(COPYDATASTRUCT* passed, FakeMemory memory, List<byte[]> seen)
    {
        var message = new ReadOnlySpan<byte>(passed->lpData, (int)passed->cbData).ToArray();
        seen.Add(message);
        if (message.Length == AppbarRelay.MessageSize && BinaryPrimitives.ReadUInt32LittleEndian(message.AsSpan(56)) == Explorer)
        {
            var view = memory.Lock((nint)BinaryPrimitives.ReadInt64LittleEndian(message.AsSpan(48)), Explorer);
            if (view != null)
            {
                Write(new Span<byte>(view, AppbarRelay.DataSize), Answered);
                memory.Unlock(view);
            }
        }
        return 1;
    }

    /// <summary>
    /// A position asked for through Karpo's tray reaches Explorer with its memory made for Explorer and
    /// Explorer named; the rest of the message is as it came. Explorer's answer is copied into the
    /// caller's own memory, where SHAppBarMessage reads it, and the memory made for Explorer is let go.
    /// </summary>
    [Fact]
    public void APositionAskedForThroughKarposTrayReachesExplorerAndItsAnswerComesBack()
    {
        using var memory = new FakeMemory();
        var block = Block(Asked);
        var mine = memory.Add(block, Karpo);
        var message = Message(block, SetPos, mine, Karpo);
        var sent = message.ToArray();
        var seen = new List<byte[]>();
        nint? answer;
        fixed (byte* bytes = message)
        {
            var data = new COPYDATASTRUCT { dwData = 0, cbData = AppbarRelay.MessageSize, lpData = bytes };
            answer = AppbarRelay.Pass(&data, Karpo, Explorer, memory, passed => ExplorerAnswers(passed, memory, seen));
        }

        Assert.Equal((nint)1, answer);
        var (theirs, madeFor) = Assert.Single(memory.Made);
        Assert.Equal(Explorer, madeFor);
        var passedOn = Assert.Single(seen);
        Assert.Equal(sent.AsSpan(0, 48).ToArray(), passedOn.AsSpan(0, 48).ToArray());
        Assert.Equal((long)theirs, BinaryPrimitives.ReadInt64LittleEndian(passedOn.AsSpan(48)));
        Assert.Equal(Explorer, BinaryPrimitives.ReadUInt32LittleEndian(passedOn.AsSpan(56)));
        Assert.Equal(sent.AsSpan(60).ToArray(), passedOn.AsSpan(60).ToArray());

        // The answer, in the caller's memory, and nothing else of it changed.
        var back = memory.Read(mine);
        Assert.Equal(Answered, Rect(back));
        Assert.Equal(block.AsSpan(0, 16).ToArray(), back.AsSpan(0, 16).ToArray());
        Assert.Equal(block.AsSpan(32).ToArray(), back.AsSpan(32).ToArray());

        // The caller's message as it came, Explorer's memory let go, every view unmapped.
        Assert.Equal(sent, message);
        Assert.Equal(new[] { (theirs, Explorer) }, memory.Freed);
        Assert.False(memory.Holds(theirs));
        Assert.Equal(0, memory.Views);
    }

    /// <summary>
    /// The failure itself, for the record: the same message passed on as it came leaves the caller's
    /// memory as it was — the position never set, the query never answered — while Explorer answers TRUE.
    /// </summary>
    [Fact]
    public void PassedOnAsItCameThePositionNeverReachesExplorer()
    {
        using var memory = new FakeMemory();
        var block = Block(Asked);
        var mine = memory.Add(block, Karpo);
        var message = Message(block, SetPos, mine, Karpo);
        var seen = new List<byte[]>();
        nint? answer;
        fixed (byte* bytes = message)
        {
            var data = new COPYDATASTRUCT { dwData = 0, cbData = AppbarRelay.MessageSize, lpData = bytes };
            answer = ExplorerAnswers(&data, memory, seen);
        }

        Assert.Equal((nint)1, answer);
        Assert.Equal(Asked, Rect(memory.Read(mine)));
    }

    public enum Unlike
    {
        AnotherSize,
        NoMemory,
        MemoryOfAnotherProcess,
        NoExplorer,
        ExplorerIsKarpo,
        MemoryThatWillNotMap,
        NoMemoryForExplorer,
    }

    /// <summary>
    /// Anything but an appbar message whose memory was made for Karpo is passed on as it came, as every
    /// message always was: the same message, no memory made, every view unmapped.
    /// </summary>
    [Theory]
    [InlineData(Unlike.AnotherSize)]
    [InlineData(Unlike.NoMemory)]
    [InlineData(Unlike.MemoryOfAnotherProcess)]
    [InlineData(Unlike.NoExplorer)]
    [InlineData(Unlike.ExplorerIsKarpo)]
    [InlineData(Unlike.MemoryThatWillNotMap)]
    [InlineData(Unlike.NoMemoryForExplorer)]
    public void AnythingElseIsPassedOnAsItCame(Unlike how)
    {
        using var memory = new FakeMemory();
        var block = Block(Asked);
        var mine = memory.Add(block, how == Unlike.MemoryOfAnotherProcess ? Someone : Karpo);
        var handle = how switch
        {
            Unlike.NoMemory => 0,
            Unlike.MemoryThatWillNotMap => mine + 1,
            _ => mine,
        };
        var message = Message(block, SetPos, handle, how == Unlike.MemoryOfAnotherProcess ? Someone : Karpo);
        memory.RefuseAlloc = how == Unlike.NoMemoryForExplorer;
        var explorer = how switch
        {
            Unlike.NoExplorer => 0u,
            Unlike.ExplorerIsKarpo => Karpo,
            _ => Explorer,
        };
        var sent = message.ToArray();
        var passedOn = new List<nint>();
        nint? answer;
        fixed (byte* bytes = message)
        {
            var data = new COPYDATASTRUCT
            {
                dwData = 0,
                cbData = (uint)(how == Unlike.AnotherSize ? AppbarRelay.MessageSize - 4 : AppbarRelay.MessageSize),
                lpData = bytes,
            };
            var original = (nint)(&data);
            answer = AppbarRelay.Pass(&data, Karpo, explorer, memory, passed =>
            {
                passedOn.Add((nint)passed);
                return 7;
            });
            Assert.Equal(new[] { original }, passedOn);
        }

        Assert.Equal((nint)7, answer);
        Assert.Equal(sent, message);
        Assert.Empty(memory.Made);
        Assert.Empty(memory.Freed);
        Assert.Equal(0, memory.Views);
    }

    /// <summary>
    /// A message that never reached Explorer answers nothing, so the tray answers it as the failure it
    /// is (Tray.Unreached); the memory made for Explorer is let go all the same, and the caller's is as it was.
    /// </summary>
    [Fact]
    public void AMessageThatNeverReachedExplorerAnswersNothingAndLetsItsMemoryGo()
    {
        using var memory = new FakeMemory();
        var block = Block(Asked);
        var mine = memory.Add(block, Karpo);
        var message = Message(block, SetPos, mine, Karpo);
        nint? answer;
        fixed (byte* bytes = message)
        {
            var data = new COPYDATASTRUCT { dwData = 0, cbData = AppbarRelay.MessageSize, lpData = bytes };
            answer = AppbarRelay.Pass(&data, Karpo, Explorer, memory, _ => null);
        }

        Assert.Null(answer);
        var (theirs, _) = Assert.Single(memory.Made);
        Assert.Equal(new[] { (theirs, Explorer) }, memory.Freed);
        Assert.Equal(block, memory.Read(mine));
        Assert.Equal(0, memory.Views);
    }

    /// <summary>
    /// The shell's own shared memory, made for this process alone and never handed to another: what it
    /// was made with is what maps, a write through one view is seen through the next, and it lets go.
    /// </summary>
    [Fact]
    public void TheShellsSharedMemoryMapsWhatItWasMadeWith()
    {
        var memory = ShellSharedMemory.Instance;
        var own = (uint)Environment.ProcessId;
        var block = Block(Asked);
        var handle = memory.Alloc(block, own);
        Assert.NotEqual(0, handle);
        try
        {
            var view = memory.Lock(handle, own);
            Assert.True(view != null);
            try
            {
                Assert.Equal(block, new ReadOnlySpan<byte>(view, AppbarRelay.DataSize).ToArray());
                Write(new Span<byte>(view, AppbarRelay.DataSize), Answered);
            }
            finally
            {
                memory.Unlock(view);
            }
            var again = memory.Lock(handle, own);
            Assert.True(again != null);
            try
            {
                Assert.Equal(Answered, Rect(new ReadOnlySpan<byte>(again, AppbarRelay.DataSize)));
            }
            finally
            {
                memory.Unlock(again);
            }
        }
        finally
        {
            memory.Free(handle, own);
        }
    }
}
