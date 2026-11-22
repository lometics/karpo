// What the taskbar list sends its band, decoded (Karpo.Dock\Taskband.cs): the data atoms' names, the
// sections they name, and how the band passes each kind of message on to Explorer's.
//
// The fixtures are what was measured: the atom names and the section's bytes below are copied from
// the probe's log, a second process calling ITaskbarList3 on a desktop of its own while a logging
// window stood in for the band (see the top of Taskband.cs). Passing on is tested through a fake
// of Windows (IBandSystem), so nothing here sends anything anywhere.

using System.Buffers.Binary;
using System.Text;
using Karpo.Dock;

namespace Karpo.Tests.Signals;

public sealed class TaskbandMessagesTests
{
    // --- Data atoms ------------------------------------------------------------------------

    /// <summary>Names the probe logged, with the handle and the band's process id they held.</summary>
    public static TheoryData<string, long, uint> Measured => new()
    {
        // SetOverlayIcon's description, then ThumbBarAddButtons and SetThumbnailClip, band pid 36396.
        { "-%D4#!`````1#````````PBC````````", 0x344, 36396 },
        { "-%D4#!`````%#````````PBC````````", 0x314, 36396 },
        { "-%D4#!`````)#````````PBC````````", 0x324, 36396 },
        // Another run, band pid 13468.
        { "-%D4#!`````-#````````P)-````````", 0x334, 13468 },
    };

    [Theory]
    [MemberData(nameof(Measured))]
    public void AMeasuredNameGivesItsHandleAndProcess(string name, long handle, uint process)
    {
        Assert.True(DataAtom.TryDecode(name, out var decodedHandle, out var decodedProcess));
        Assert.Equal((nint)handle, decodedHandle);
        Assert.Equal(process, decodedProcess);
    }

    [Theory]
    [MemberData(nameof(Measured))]
    public void EncodingGivesTheMeasuredNameBack(string name, long handle, uint process)
    {
        Assert.Equal(name, DataAtom.Encode((nint)handle, process));
    }

    [Theory]
    [InlineData(0x4, 1u)]
    [InlineData(0x7FFF_FFFC, 4_294_967_295u)]
    [InlineData(0x1234_5678_9ABC, 42u)]
    public void AnyHandleAndProcessComeBackAsTheyWent(long handle, uint process)
    {
        var name = DataAtom.Encode((nint)handle, process);

        Assert.Equal(DataAtom.Length, name.Length);
        // Every character is one a global atom's name can hold, and never a space: '`' stands for 0.
        Assert.All(name, c => Assert.InRange(c, '!', '`'));
        Assert.True(DataAtom.TryDecode(name, out var back, out var backProcess));
        Assert.Equal((nint)handle, back);
        Assert.Equal(process, backProcess);
    }

    [Theory]
    [InlineData("")]
    [InlineData("-%D4#!`````1#````````PBC```````")]
    [InlineData("-%D4#!`````1#````````PBC`````````")]
    // One character changed in the signature.
    [InlineData(".%D4#!`````1#````````PBC````````")]
    // A space is not a digit: 0 is written '`'.
    [InlineData("-%D4#! ````1#````````PBC````````")]
    // No handle at all.
    [InlineData("-%D4#!```````````````PBC````````")]
    public void AnythingElseIsNotADataAtom(string name)
    {
        Assert.False(DataAtom.TryDecode(name, out _, out _));
    }

    [Fact]
    public void AProcessIdPastThirtyTwoBitsIsNotOne()
    {
        // The name of handle 0x344 in "process" 2^32: the high word of the id set.
        var name = DataAtom.Encode(0x344, 0);
        var raw = Decode(name);
        raw[20] = 1;

        Assert.False(DataAtom.TryDecode(Encode(raw), out _, out _));
    }

    // --- The sections they name --------------------------------------------------------------

    /// <summary>SetOverlayIcon's description as its section held it: SHAllocShared's header, then UTF-16.</summary>
    private static byte[] OverlayTextSection()
    {
        var text = Encoding.Unicode.GetBytes("3 unread messages\0");
        var mapping = new byte[4096];
        BinaryPrimitives.WriteUInt32LittleEndian(mapping, (uint)text.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(mapping.AsSpan(4), BandData.Signature);
        BinaryPrimitives.WriteUInt32LittleEndian(mapping.AsSpan(8), 11508);
        BinaryPrimitives.WriteUInt32LittleEndian(mapping.AsSpan(12), 36396);
        text.CopyTo(mapping, BandData.HeaderSize);
        return mapping;
    }

    [Fact]
    public void ASectionGivesItsDataAfterTheHeader()
    {
        var mapping = OverlayTextSection();

        var data = BandData.Data(mapping);

        Assert.NotNull(data);
        Assert.Equal(0x24, data!.Length);
        Assert.Equal("3 unread messages", BandData.Text(data));
    }

    [Fact]
    public void TheMeasuredHeaderReadsAsMeasured()
    {
        // The first sixteen bytes the probe dumped for that call, byte for byte.
        var header = Convert.FromHexString("24000000FF1AFFBAF42C00002C8E0000");

        Assert.Equal(header, OverlayTextSection()[..16]);
    }

    [Fact]
    public void ASectionWithoutTheSignatureIsNotRead()
    {
        var mapping = OverlayTextSection();
        mapping[4] = 0;

        Assert.Null(BandData.Data(mapping));
    }

    [Fact]
    public void ASectionClaimingMoreThanItHoldsIsNotRead()
    {
        var mapping = OverlayTextSection();
        BinaryPrimitives.WriteUInt32LittleEndian(mapping, 4096);

        Assert.Null(BandData.Data(mapping));
        Assert.Null(BandData.Data(mapping.AsSpan(0, 8)));
    }

    [Fact]
    public void TextEndsAtItsZeroOrAtTheEnd()
    {
        Assert.Equal("Tooltip text", BandData.Text(Encoding.Unicode.GetBytes("Tooltip text\0junk")));
        Assert.Equal("no end", BandData.Text(Encoding.Unicode.GetBytes("no end")));
        // An odd byte at the end is not half a character.
        Assert.Equal("ab", BandData.Text([.. Encoding.Unicode.GetBytes("ab"), 0x41]));
        Assert.Equal("", BandData.Text([]));
    }

    // --- Passing them on -----------------------------------------------------------------------

    private const nint Band = 0x5150;
    private const uint Explorer = 4242;
    private const nint App = 0x1A0D56;

    [Fact]
    public void APostedMessageIsPostedOnAsItCame()
    {
        var system = new FakeSystem();

        var answer = BandRelay.Pass(system, Band, Explorer, BandMessage.ProgressValue, (nuint)App, 0x4CCC, BandCall.Posted, null);

        Assert.Equal(0, answer);
        Assert.Equal([("post", Band, BandMessage.ProgressValue, (nuint)App, (nint)0x4CCC)], system.Calls);
    }

    [Fact]
    public void ANotifiedMessageIsNotifiedOnAsItCame()
    {
        var system = new FakeSystem();

        BandRelay.Pass(system, Band, Explorer, BandMessage.OverlayText, (nuint)App, 0, BandCall.Notified, null);

        Assert.Equal([("notify", Band, BandMessage.OverlayText, (nuint)App, (nint)0)], system.Calls);
    }

    [Fact]
    public void ASentMessageWithoutDataIsSentOnAndExplorersAnswerComesBack()
    {
        var system = new FakeSystem { Answer = 7 };

        var answer = BandRelay.Pass(system, Band, Explorer, BandMessage.OverlayIcon, (nuint)App, 0x360E2B, BandCall.Sent, null);

        Assert.Equal(7, answer);
        Assert.Equal([("send", Band, BandMessage.OverlayIcon, (nuint)App, (nint)0x360E2B)], system.Calls);
    }

    [Fact]
    public void ASentMessagesDataIsMadeAgainForExplorerAndLetGoOnceItAnswered()
    {
        var system = new FakeSystem { Answer = 1 };
        byte[] data = [1, 2, 3, 4];

        var answer = BandRelay.Pass(system, Band, Explorer, BandMessage.ThumbnailClip, (nuint)App, 0xC231, BandCall.Sent, data);

        Assert.Equal(1, answer);
        // Made for Explorer's process, sent with what that gave in place of the app's, then let go.
        Assert.Equal(
            [
                ("make", 0, 0u, (nuint)Explorer, FakeSystem.Made),
                ("send", Band, BandMessage.ThumbnailClip, (nuint)App, FakeSystem.Made),
                ("unmake", 0, 0u, (nuint)Explorer, FakeSystem.Made),
            ],
            system.Calls);
        Assert.Equal(data, system.MadeData);
    }

    [Fact]
    public void DataIsLetGoEvenWhenExplorerNeverAnswered()
    {
        var system = new FakeSystem { Answer = null };

        var answer = BandRelay.Pass(system, Band, Explorer, BandMessage.ThumbBarAdd, (nuint)App, 0xC0D9, BandCall.Sent, [9]);

        Assert.Equal(0, answer);
        Assert.Equal("unmake", system.Calls[^1].What);
    }

    [Fact]
    public void DataThatCannotBeMadeForExplorerGoesAsItCame()
    {
        var system = new FakeSystem { CanMake = false, Answer = 3 };

        var answer = BandRelay.Pass(system, Band, Explorer, BandMessage.ThumbnailTooltip, (nuint)App, 0xC12E, BandCall.Sent, [1, 0]);

        Assert.Equal(3, answer);
        Assert.Equal(("send", Band, BandMessage.ThumbnailTooltip, (nuint)App, (nint)0xC12E), system.Calls[^1]);
        Assert.DoesNotContain(system.Calls, call => call.What == "unmake");
    }

    [Fact]
    public void WithoutExplorersBandNothingIsPassedOn()
    {
        var system = new FakeSystem();

        foreach (var call in new[] { BandCall.Posted, BandCall.Sent, BandCall.Notified })
        {
            Assert.Equal(0, BandRelay.Pass(system, 0, 0, BandMessage.ProgressState, (nuint)App, 2, call, [1]));
        }
        Assert.Empty(system.Calls);
    }

    // --- Helpers ---------------------------------------------------------------------------------

    /// <summary>The 24 bytes a name holds, without judging them.</summary>
    private static byte[] Decode(string name)
    {
        var raw = new byte[24];
        for (var index = 0; index < name.Length; index++)
        {
            var digit = name[index] == '`' ? 0 : name[index] - 0x20;
            for (var bit = 0; bit < 6; bit++)
            {
                if ((digit & (1 << bit)) != 0)
                {
                    raw[(index * 6 + bit) / 8] |= (byte)(1 << ((index * 6 + bit) % 8));
                }
            }
        }
        return raw;
    }

    private static string Encode(byte[] raw)
    {
        var name = new char[32];
        for (var index = 0; index < 32; index++)
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

    /// <summary>Windows as the relay sees it, writing down every call.</summary>
    private sealed class FakeSystem : IBandSystem
    {
        public const nint Made = 0xC777;

        public List<(string What, nint To, uint Msg, nuint WParam, nint LParam)> Calls { get; } = [];

        public nint? Answer { get; init; }

        public bool CanMake { get; init; } = true;

        public byte[]? MadeData { get; private set; }

        public byte[]? Read(nint atom) => null;

        public nint Make(ReadOnlySpan<byte> data, uint process)
        {
            MadeData = data.ToArray();
            Calls.Add(("make", 0, 0, process, CanMake ? Made : 0));
            return CanMake ? Made : 0;
        }

        public void Unmake(nint made, uint process) => Calls.Add(("unmake", 0, 0, process, made));

        public nint? Send(nint to, uint msg, nuint wParam, nint lParam)
        {
            Calls.Add(("send", to, msg, wParam, lParam));
            return Answer;
        }

        public void Post(nint to, uint msg, nuint wParam, nint lParam) => Calls.Add(("post", to, msg, wParam, lParam));

        public void Notify(nint to, uint msg, nuint wParam, nint lParam) => Calls.Add(("notify", to, msg, wParam, lParam));
    }
}
