// What dropped data pins (DropFormats): a web link, dragged or as text;
// a shell item by the name the shell opens it with again, an app out of Start
// included; and the Shell IDList Array those items come in, read safely.

using Karpo.Dock;

// Not Karpo.Tests.Desktops: the core tests name BarKind.Desktops as "Desktops" (using static),
// which a namespace of that name would shadow.
namespace Karpo.Tests.DesktopsArea;

public sealed class DropFormatsTests
{
    [Theory]
    [InlineData("https://example.com/a?b=c", "https://example.com/a?b=c")]
    [InlineData("  <https://example.com>  ", "https://example.com")]
    [InlineData("\"http://example.com\"", "http://example.com")]
    [InlineData("https://example.com\r\nExample Domain", "https://example.com")]
    public void AWebAddressIsALink(string text, string expected) => Assert.Equal(expected, DropFormats.Link(text));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("just some words")]
    [InlineData("https://example.com and more")]
    [InlineData("ftp://example.com")]
    [InlineData(@"C:\file.txt")]
    public void AnythingElseIsNot(string? text) => Assert.Null(DropFormats.Link(text));

    [Fact]
    public void AnAppFromStartIsPinnedByTheAppsFolder()
    {
        // The shell names an Applications-folder item by its app ID alone,
        // which it doesn't parse back; shell:AppsFolder\<ID> it does.
        Assert.Equal(
            @"shell:AppsFolder\Microsoft.WindowsCalculator_8wekyb3d8bbwe!App",
            DropFormats.ShellTarget("Microsoft.WindowsCalculator_8wekyb3d8bbwe!App", DropFormats.AppsFolder));
        Assert.Equal(
            @"shell:AppsFolder\Microsoft.Windows.Explorer",
            DropFormats.ShellTarget(DropFormats.AppsFolder + @"\Microsoft.Windows.Explorer", null));
        Assert.Equal(
            @"shell:AppsFolder\Microsoft.Windows.Explorer",
            DropFormats.ShellTarget(DropFormats.AppsFolder.ToLowerInvariant() + @"\Microsoft.Windows.Explorer", DropFormats.AppsFolder.ToLowerInvariant()));
    }

    [Fact]
    public void AVirtualFolderIsPinnedAsAShellPathAndAFileByItsPath()
    {
        Assert.Equal("shell:::{20D04FE0-3AEA-1069-A2D8-08002B30309D}", DropFormats.ShellTarget("::{20D04FE0-3AEA-1069-A2D8-08002B30309D}", null));
        Assert.Equal(@"C:\Windows\notepad.exe", DropFormats.ShellTarget(@"C:\Windows\notepad.exe", @"C:\Windows"));
        Assert.Null(DropFormats.ShellTarget(" ", null));
        Assert.Null(DropFormats.ShellTarget("", DropFormats.AppsFolder));
    }

    /// <summary>
    /// A CIDA with two items: the parent's list (one 4-byte item), an item's
    /// list (one 6-byte item) and an empty list.
    /// </summary>
    private static byte[] Cida()
    {
        var block = new List<byte>();
        void Uint(uint value) => block.AddRange(BitConverter.GetBytes(value));
        void Ushort(ushort value) => block.AddRange(BitConverter.GetBytes(value));
        Uint(2);
        Uint(16);
        Uint(22);
        Uint(30);
        Ushort(4);
        Ushort(0xABCD);
        Ushort(0);
        Ushort(6);
        Uint(0x01020304);
        Ushort(0);
        Ushort(0);
        return [.. block];
    }

    [Fact]
    public void AShellIdListArrayIsRead()
    {
        var offsets = DropFormats.IdListOffsets(Cida());

        Assert.NotNull(offsets);
        Assert.Equal(16, offsets.Value.Parent);
        Assert.Equal(new[] { 22, 30 }, offsets.Value.Items);
    }

    [Fact]
    public void ABrokenShellIdListArrayIsNot()
    {
        var cida = Cida();

        // Cut short: the last list no longer ends inside the block.
        Assert.Null(DropFormats.IdListOffsets(cida.AsSpan(0, cida.Length - 1)));
        // No items, or more than the block could hold.
        Assert.Null(DropFormats.IdListOffsets(Patched(cida, 0, 0)));
        Assert.Null(DropFormats.IdListOffsets(Patched(cida, 0, 1_000_000)));
        // An offset into the header, or past the end.
        Assert.Null(DropFormats.IdListOffsets(Patched(cida, 8, 4)));
        Assert.Null(DropFormats.IdListOffsets(Patched(cida, 12, 400)));
        // An item that claims a size of 1, which can't hold its own size.
        var small = Cida();
        small[22] = 1;
        small[23] = 0;
        Assert.Null(DropFormats.IdListOffsets(small));
        Assert.Null(DropFormats.IdListOffsets([]));
    }

    private static byte[] Patched(byte[] block, int at, uint value)
    {
        var copy = (byte[])block.Clone();
        BitConverter.GetBytes(value).CopyTo(copy, at);
        return copy;
    }
}
