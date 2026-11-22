// PinTargets: which pinned files, folders, shortcuts and apps aren't on this PC,
// with a made-up PC (FakePc) standing in for the disk and the registry, and
// shortcuts built byte by byte as MS-SHLLINK lays them out.

using System.Buffers.Binary;
using System.Text;

namespace Karpo.Tests.Persistence;

public sealed class PinTargetsTests
{
    /// <summary>A PC with a C: drive, a network drive Z:, and what each test puts on it.</summary>
    private sealed class FakePc : ITargetSystem
    {
        public readonly HashSet<string> Files = new(StringComparer.OrdinalIgnoreCase);
        public readonly HashSet<string> Folders = new(StringComparer.OrdinalIgnoreCase) { @"C:\", @"C:\Windows" };
        public readonly Dictionary<string, byte[]> Shortcuts = new(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, DriveType> Drives = new(StringComparer.OrdinalIgnoreCase) { [@"C:\"] = DriveType.Fixed, [@"Z:\"] = DriveType.Network };
        public readonly List<string> Packages = ["Microsoft.WindowsCalculator_11.2607.0.0_x64__8wekyb3d8bbwe", "Microsoft.WindowsTerminal_1.24.11911.0_x64__8wekyb3d8bbwe"];

        public bool FileExists(string path) => Files.Contains(path) || Shortcuts.ContainsKey(path);

        public bool DirectoryExists(string path) => Folders.Contains(path);

        public DriveType? Drive(string root) => Drives.TryGetValue(root, out var type) ? type : null;

        public byte[]? ReadShortcut(string path) => Shortcuts.GetValueOrDefault(path);

        public IReadOnlyList<string> UserSubKeys(string path) => path == PinTargets.PackagesKey ? Packages : [];

        public string Expand(string text) => text.Replace("%WINDIR%", @"C:\Windows", StringComparison.OrdinalIgnoreCase);

        public string Folder(Environment.SpecialFolder folder) => folder switch
        {
            Environment.SpecialFolder.ProgramFiles => @"C:\Program Files",
            Environment.SpecialFolder.System => @"C:\Windows\System32",
            _ => "",
        };
    }

    private static TargetState State(string target, FakePc pc) => PinTargets.Check(target, pc).State;

    [Fact]
    public void FilesAndFoldersAreLookedFor()
    {
        var pc = new FakePc();
        pc.Files.Add(@"C:\Tools\app.exe");

        Assert.Equal(TargetState.Here, State(@"C:\Tools\app.exe", pc));
        Assert.Equal(TargetState.Here, State(@"C:\Windows", pc));
        Assert.Equal(TargetState.Here, State(@"%WINDIR%", pc));
        Assert.Equal(TargetState.Missing, State(@"C:\Tools\gone.exe", pc));
        Assert.Equal(TargetState.Missing, State(@"C:\Projects", pc));
    }

    [Fact]
    public void ADriveThisPcDoesntHaveSaysSo()
    {
        var check = PinTargets.Check(@"D:\Games\game.exe", new FakePc());

        Assert.True(check.Missing);
        Assert.Contains("D:", check.Reason);
    }

    /// <summary>Asking a share that isn't there can stall: network places are left alone.</summary>
    [Theory]
    [InlineData(@"\\server\share\tool.exe")]
    [InlineData(@"Z:\Shared\report.xlsx")]
    public void NetworkPlacesAreNotChecked(string target) => Assert.Equal(TargetState.Unchecked, State(target, new FakePc()));

    [Theory]
    [InlineData("https://example.com/")]
    [InlineData("http://intranet/")]
    [InlineData("mailto:someone@example.com")]
    [InlineData("ms-settings:display")]
    [InlineData("shell:Downloads")]
    [InlineData("shell:::{20D04FE0-3AEA-1069-A2D8-08002B30309D}")]
    [InlineData("::{645FF040-5081-101B-9F08-00AA002F954E}")]
    [InlineData("|")]
    [InlineData("group:abc123")]
    [InlineData("")]
    [InlineData("tools\\relative.exe")]
    public void WhatCantBeToldForSureIsNotChecked(string target) => Assert.Equal(TargetState.Unchecked, State(target, new FakePc()));

    [Fact]
    public void PackagedAppsAreLookedForByTheirFamily()
    {
        var pc = new FakePc();

        Assert.Equal(TargetState.Here, State(@"shell:AppsFolder\Microsoft.WindowsCalculator_8wekyb3d8bbwe!App", pc));
        Assert.Equal(TargetState.Here, State(@"shell:appsfolder\microsoft.windowsterminal_8wekyb3d8bbwe!App", pc));
        Assert.Equal(TargetState.Missing, State(@"shell:AppsFolder\SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify", pc));
    }

    [Fact]
    public void WithoutAListOfInstalledAppsNoAppIsCalledMissing()
    {
        var pc = new FakePc();
        pc.Packages.Clear();

        Assert.Equal(TargetState.Unchecked, State(@"shell:AppsFolder\SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify", pc));
    }

    [Fact]
    public void AppsWithoutAnIdOfTheirOwnAreLookedForByPath()
    {
        var pc = new FakePc();
        pc.Files.Add(@"C:\Program Files\Notepad++\notepad++.exe");

        Assert.Equal(TargetState.Here, State(@"shell:AppsFolder\{6D809377-6AF0-444B-8957-A3773F02200E}\Notepad++\notepad++.exe", pc));
        Assert.Equal(TargetState.Missing, State(@"shell:AppsFolder\{6D809377-6AF0-444B-8957-A3773F02200E}\Paint.NET\paintdotnet.exe", pc));
        Assert.Equal(TargetState.Missing, State(@"shell:AppsFolder\C:\Tools\gone.exe", pc));
        Assert.Equal(TargetState.Unchecked, State(@"shell:AppsFolder\Microsoft.Windows.Explorer", pc));
        Assert.Equal(TargetState.Unchecked, State(@"shell:AppsFolder\{3EB685DB-65F9-4CF6-A03A-E3EF65729F3D}\app.exe", pc));
    }

    [Fact]
    public void AShortcutIsMissingWhenWhatItPointsAtIs()
    {
        var pc = new FakePc();
        pc.Shortcuts[@"C:\Links\gone.lnk"] = Link(LocalInfo(@"C:\Tools\gone.exe"));
        pc.Shortcuts[@"C:\Links\here.lnk"] = Link(LocalInfo(@"C:\Windows"));
        pc.Shortcuts[@"C:\Links\app.lnk"] = Link(idList: [4, 0, 0, 0]);

        var gone = PinTargets.Check(@"C:\Links\gone.lnk", pc);
        Assert.True(gone.Missing);
        Assert.Contains(@"C:\Tools\gone.exe", gone.Reason);
        Assert.Equal(TargetState.Here, State(@"C:\Links\here.lnk", pc));
        // A shortcut to a packaged app has no path: it is there, and that is all that can be said.
        Assert.Equal(TargetState.Here, State(@"C:\Links\app.lnk", pc));
    }

    [Fact]
    public void EveryPinOfAConfigIsCheckedOnceAndSaysWhereItIs()
    {
        var pc = new FakePc();
        pc.Files.Add(@"C:\here.exe");
        var config = Config.Parse("""
            {
              "layouts": [ { "id": "work", "name": "Work", "islands": [
                { "id": "p1", "kind": "pin", "pin": { "target": "C:\\Projects\\notes.txt" } },
                { "id": "p2", "kind": "pin", "pin": { "target": "C:\\here.exe" } },
                { "id": "p3", "kind": "pin", "pin": { "target": "C:\\gone.exe" } },
                { "id": "p4", "kind": "pin", "pin": { "target": "C:\\here.exe" } },
                { "id": "p5", "kind": "pin", "pin": { "target": "https://example.com" } },
                { "id": "g1", "kind": "folderList", "folder": { "lists": [ [
                  { "island": { "id": "t1", "kind": "pin", "pin": { "target": "D:\\tool.exe" } } },
                  { "island": { "id": "t2", "kind": "pin", "pin": { "target": "C:\\gone.exe" } } } ] ] } } ] } ]
            }
            """);
        config.Repair();

        var missing = PinTargets.Missing(config, pc);

        // Each target once, in the order the file has them, and where it stands.
        Assert.Equal([@"C:\Projects\notes.txt", @"C:\gone.exe", @"D:\tool.exe"], missing.Select(item => item.Target));
        // §427: a folder has no name; a pin in one is in a folder of its layout.
        Assert.Equal(["A pin in “Work”", "A pin in “Work”", "A pin in a folder in “Work”"], missing.Select(item => item.Where));
        // Nothing is dropped from the config for it.
        Assert.Contains(Karpo.Core.Folders.All(config.Layouts[0].Islands), entry => entry.Island.Pin?.Target == @"C:\gone.exe");
    }

    // --- Shortcuts, byte by byte ----------------------------------------------------------------

    [Fact]
    public void AShortcutsLocalPathIsRead() =>
        Assert.Equal(@"C:\Tools\app.exe", PinTargets.ShortcutTarget(Link(LocalInfo(@"C:\Tools\", "app.exe"))));

    [Fact]
    public void TheUnicodePathIsTakenWhenThereIsOne() =>
        Assert.Equal(@"C:\Users\Zoë\app.exe", PinTargets.ShortcutTarget(Link(LocalInfo(@"C:\Users\Zoë\app.exe", unicode: true))));

    /// <summary>A path in a code page this can't know is not guessed at: a wrong guess would call it missing.</summary>
    [Fact]
    public void AnAnsiPathBeyondAsciiIsNotGuessed() =>
        Assert.Null(PinTargets.ShortcutTarget(Link(LocalInfo("C:\\Users\\Zo\u00EB\\app.exe", latin1: true))));

    [Fact]
    public void TheTargetWithItsVariablesComesFirst() =>
        Assert.Equal(@"%WINDIR%\notepad.exe", PinTargets.ShortcutTarget(Link(LocalInfo(@"C:\Windows\notepad.exe"), environment: @"%WINDIR%\notepad.exe")));

    [Fact]
    public void ARelativePathIsTakenFromTheShortcutsFolder() =>
        Assert.Equal(@"C:\Links\Tools\app.exe", PinTargets.ShortcutTarget(Link(relative: @"Tools\app.exe"), @"C:\Links"));

    [Fact]
    public void WhatIsntAShortcutHasNoTarget()
    {
        Assert.Null(PinTargets.ShortcutTarget([]));
        Assert.Null(PinTargets.ShortcutTarget(Encoding.UTF8.GetBytes("{ \"not\": \"a shortcut\" } and some more text to be long enough for a header")));
        var cut = Link(LocalInfo(@"C:\Tools\app.exe"));
        Assert.Null(PinTargets.ShortcutTarget(cut.AsSpan(0, 90)));
    }

    private static readonly byte[] Clsid = [0x01, 0x14, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0xC0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x46];

    /// <summary>A shell link: header, then the parts given, their flags set, then the terminal block.</summary>
    private static byte[] Link(byte[]? linkInfo = null, byte[]? idList = null, string? relative = null, string? environment = null)
    {
        uint flags = 0x80; // IsUnicode
        flags |= idList is null ? 0u : 0x1;
        flags |= linkInfo is null ? 0u : 0x2;
        flags |= relative is null ? 0u : 0x8;
        flags |= environment is null ? 0u : 0x200;
        var bytes = new List<byte>();
        var header = new byte[0x4C];
        BinaryPrimitives.WriteUInt32LittleEndian(header, 0x4C);
        Clsid.CopyTo(header, 4);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(20), flags);
        bytes.AddRange(header);
        if (idList is not null)
        {
            bytes.AddRange(BitConverter.GetBytes((ushort)idList.Length));
            bytes.AddRange(idList);
        }
        if (linkInfo is not null)
        {
            bytes.AddRange(linkInfo);
        }
        if (relative is not null)
        {
            bytes.AddRange(BitConverter.GetBytes((ushort)relative.Length));
            bytes.AddRange(Encoding.Unicode.GetBytes(relative));
        }
        if (environment is not null)
        {
            var block = new byte[0x314];
            BinaryPrimitives.WriteUInt32LittleEndian(block, 0x314);
            BinaryPrimitives.WriteUInt32LittleEndian(block.AsSpan(4), 0xA0000001);
            Encoding.ASCII.GetBytes(environment).CopyTo(block, 8);
            Encoding.Unicode.GetBytes(environment).CopyTo(block, 8 + 260);
            bytes.AddRange(block);
        }
        bytes.AddRange(new byte[4]);
        return [.. bytes];
    }

    /// <summary>LinkInfo for a local path: a volume id, the base path and the suffix, in ANSI and, with <paramref name="unicode"/>, UTF-16 too.</summary>
    private static byte[] LocalInfo(string basePath, string suffix = "", bool unicode = false, bool latin1 = false)
    {
        var headerSize = unicode ? 0x24 : 0x1C;
        var volume = new byte[0x11];
        BinaryPrimitives.WriteUInt32LittleEndian(volume, 0x11);
        BinaryPrimitives.WriteUInt32LittleEndian(volume.AsSpan(12), 0x10);
        var narrow = latin1 ? Encoding.Latin1 : Encoding.ASCII;
        var narrowBase = narrow.GetBytes(basePath + "\0");
        var narrowSuffix = narrow.GetBytes(suffix + "\0");
        var wideBase = Encoding.Unicode.GetBytes(basePath + "\0");
        var wideSuffix = Encoding.Unicode.GetBytes(suffix + "\0");
        var volumeAt = headerSize;
        var baseAt = volumeAt + volume.Length;
        var suffixAt = baseAt + narrowBase.Length;
        var wideBaseAt = suffixAt + narrowSuffix.Length;
        var wideSuffixAt = wideBaseAt + wideBase.Length;
        var size = unicode ? wideSuffixAt + wideSuffix.Length : wideBaseAt;
        var info = new byte[size];
        BinaryPrimitives.WriteUInt32LittleEndian(info, (uint)size);
        BinaryPrimitives.WriteUInt32LittleEndian(info.AsSpan(4), (uint)headerSize);
        BinaryPrimitives.WriteUInt32LittleEndian(info.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(info.AsSpan(12), (uint)volumeAt);
        BinaryPrimitives.WriteUInt32LittleEndian(info.AsSpan(16), (uint)baseAt);
        BinaryPrimitives.WriteUInt32LittleEndian(info.AsSpan(24), (uint)suffixAt);
        volume.CopyTo(info, volumeAt);
        narrowBase.CopyTo(info, baseAt);
        narrowSuffix.CopyTo(info, suffixAt);
        if (unicode)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(info.AsSpan(28), (uint)wideBaseAt);
            BinaryPrimitives.WriteUInt32LittleEndian(info.AsSpan(32), (uint)wideSuffixAt);
            wideBase.CopyTo(info, wideBaseAt);
            wideSuffix.CopyTo(info, wideSuffixAt);
        }
        return info;
    }
}
