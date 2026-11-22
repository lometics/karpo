// The drive and the network drive are presets (docs\widgets.md §99, split §119): what a click on
// either entry gave — its flyout: the drive's picture, its name and kind, its file system and
// BitLocker state, the capacity bar with what is used and free, and under it the drive's top level,
// folders first and then files, each opened with a click — is a folder holding the drive's line, its
// information alone, and under it a folder pin to the drive's root showing its contents (2026-10-11:
// "the drive widget should get split in two"), under the drive face: the picture, the name and the
// capacity bar the entry was. The drives are stand-ins (DriveWatch.Source), and so are the folders
// and the volumes (DriveIsland's stands, FolderSource.Current, FakeFolders): nothing here reads a
// disk, a share, or opens anything.
//
// Pictured: drive-preset-<dark|light>.png and drive-preset-share-dark.png at 150 %.

using System.Numerics;
using Karpo.Dock;
using Karpo.Dock.Drives;
using Karpo.Tests.Islands;
using Karpo.Tests.Storage;
using TerraFX.Interop.DirectX;
using static Karpo.Tests.Folders.ListFormPictures;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Folders;

[Collection(AttachedDrivesCollection.Collection)]
public sealed unsafe class DrivePresetTests
{
    private const string SystemVolume = @"\\?\Volume{11111111-1111-1111-1111-111111111111}\";
    private const string StickVolume = @"\\?\Volume{55555555-5555-5555-5555-555555555555}\";
    private const string Share = @"\\server\work";

    private static ulong Gb(double gigabytes) => (ulong)(gigabytes * 1024 * 1024 * 1024);

    private static Drive Disk(int letter, string label, double total, double free, DriveKind kind = DriveKind.Fixed, string volume = SystemVolume) =>
        new() { Letter = letter, Kind = kind, Volume = volume, Label = label, Total = Gb(total), Free = Gb(free) };

    private static Drive SystemDisk(double free = 419) => Disk(2, "Local Disk", 931, free);

    private static Drive Connected() => Disk(25, @"work (\\server)", 200, 50, DriveKind.Remote, Share);

    private static DriveTargetOptions Target(string volume, string letter, string label) => new() { Volume = volume, Letter = letter, Label = label };

    private static DriveTargetOptions SystemTarget() => Target(SystemVolume, "C:", "Local Disk");

    /// <summary>A share as the settings' picker points a line at it: its UNC name, no letter.</summary>
    private static DriveTargetOptions ShareTarget() => Target(Share, "", @"work (\\server)");

    /// <summary>The system drive's top level, as a fresh Windows has it, and a share's.</summary>
    private static FakeFolders Roots() => new FakeFolders()
        .With(@"C:\", "PerfLogs\\", "Program Files\\", "Program Files (x86)\\", "Users\\", "Windows\\", "A very long file name that will not fit beside its date.txt", "bootmgr", "DumpStack.log.tmp", "hiberfil.sys")
        .With(@"E:\", "Photos\\", "Backup 2026.zip")
        .With(Share, "Shared\\", "Budget.xlsx", "Minutes.docx");

    /// <summary>Stands in for the drives this PC has, the folders on them and what their volumes say, while it lives.</summary>
    private sealed class Stands : IDisposable
    {
        private readonly Func<bool, bool, IReadOnlyList<Drive>>? before = DriveWatch.Source;
        private readonly Func<string, int, Pixels?>? pictures = DriveIcons.Source;
        private readonly FolderSource folderSource = FolderSource.Current;

        public Stands(FakeFolders folders, params Drive[] drives)
        {
            Folders = folders;
            Drives = [.. drives];
            DriveWatch.Source = (_, remote) => Drives.Where(drive => remote || drive.Kind != DriveKind.Remote).ToList();
            DriveWatch.Forget();
            // No drive's picture is asked of the shell: a letter here may be a real share there.
            DriveIcons.Source = (_, _) => null;
            DriveIcons.Forget();
            DriveIsland.FolderStand = folders.Source;
            // The folder pins read the folders' source every pin reads.
            FolderSource.Current = folders.Source;
            DriveIsland.FactsStand = root =>
            {
                Interlocked.Increment(ref asked);
                return root.StartsWith('Z') ? new DriveFacts("NTFS", "") : new DriveFacts("NTFS", "BitLocker On");
            };
        }

        public FakeFolders Folders { get; }

        /// <summary>The drives this PC has from now on; the next read finds them.</summary>
        public List<Drive> Drives { get; set; }

        /// <summary>How many times a volume was asked what it is.</summary>
        public int Asked => Volatile.Read(ref asked);

        private int asked;

        public void Dispose()
        {
            DriveWatch.Source = before;
            DriveWatch.Forget();
            DriveIsland.FolderStand = null;
            DriveIsland.FactsStand = null;
            FolderSource.Current = folderSource;
            DriveIcons.Source = pictures;
            DriveIcons.Forget();
        }
    }

    /// <summary>
    /// The preset's folder of <paramref name="kind"/>, as the add list makes it, its drive line
    /// pointed at <paramref name="target"/> and its folder pin at the drive's root, as choosing the
    /// drive in Settings leaves them; where not <paramref name="spread"/>, the pin with no folder yet.
    /// </summary>
    private static BarItem Holder(ListKind kind, DriveTargetOptions? target, bool name = true, bool spread = true)
    {
        var holder = DriveWidgetOffscreenTests.Holder(kind == ListKind.NetworkDrive ? "share" : "drive", kind, target, spread);
        holder.Folder!.Face.Set(holder.Folder.Face.Info.Option(DriveFace.Name)!, name);
        return holder;
    }

    private static FolderIsland Made(Gfx gfx, ListKind kind, DriveTargetOptions? target, string? key = null, bool name = true, bool spread = true)
    {
        var holder = Holder(kind, target, name, spread);
        var folder = new FolderIsland(key ?? $"{Owner}/{holder.Id}", holder, gfx, new Config());
        _ = ((IIsland)folder).Refresh(gfx);
        return folder;
    }

    private static IslandStyle Bar(int height = 48, uint dpi = 96, bool vertical = false, float text = 1f, bool dark = true)
    {
        var config = new Config { Height = height };
        return new IslandStyle(dpi / 96f, vertical, Layout.CellPx(dpi, config), Layout.IconPx(dpi, config), LookFor(dark, text));
    }

    private static TileStyle LineStyle(float scale = 1.5f, float text = 1f) =>
        new(scale, LookFor(true, text), FlyoutTheme.For(LookFor(true, text)), 1, 1);

    /// <summary>A point of <paramref name="box"/> whose target is <paramref name="target"/>, or null.</summary>
    private static Vector2? Target(CellIsland line, D2D_RECT_F box, in TileStyle style, int target)
    {
        for (var y = box.top + 1; y < box.bottom; y += 3)
        {
            for (var x = box.left + 1; x < box.right; x += 3)
            {
                if (line.TargetOn(new Vector2(x, y), box, style, WidgetSurface.List) == target)
                {
                    return new Vector2(x, y);
                }
            }
        }
        return null;
    }

    // --- The presets ------------------------------------------------------------------------------------

    /// <summary>
    /// Each preset is the drive's line, its information alone, and under it a folder pin showing its
    /// contents, under the face of its kind; the two as tall as the person's one line was, ten rows
    /// for a drive and twelve for a share (2026-10-11), the drive's line its own height. It starts
    /// with no drive, as the entry did: the line asks for one, and the pin has no folder yet.
    /// </summary>
    [Theory]
    [InlineData(ListKind.Drive)]
    [InlineData(ListKind.NetworkDrive)]
    public void ThePresetIsTheDrivesLineOverAFolderPinUnderTheDriveFace(ListKind kind)
    {
        var network = kind == ListKind.NetworkDrive;
        var preset = Presets.Find(network ? Presets.NetworkDriveId : Presets.DriveId)!;
        var face = network ? FolderFaces.NetworkDrive : FolderFaces.Drive;
        Assert.Equal(face.Id, preset.Face!.Kind);
        Assert.Equal([kind.Id(), ListKind.FolderContents.Id()], preset.Items.Select(item => item.Form));
        Assert.Null(preset.Items[0].Height);
        Assert.True(preset.Items[1].DriveContents);
        Assert.Equal(LineHeight.Automatic((network ? FolderOptions.MostCells : 20) - Presets.DriveCells), preset.Items[1].Height);
        Assert.Equal((network ? WidgetInfos.NetworkDrive : WidgetInfos.Drive).Glyph, preset.Glyph);

        var folder = Presets.Folder(preset, new PresetMachine(HasBattery: false, FixedDrives: [])).Folder!;
        Assert.Same(face, folder.Face.Info);
        Assert.Equal([DriveFace.Name], face.Options.Select(option => option.Key));
        Assert.True(DriveFace.ShowsName(folder.Face));
        var cells = folder.Cells.Select(cell => cell.Island).ToList();
        Assert.Equal([kind, ListKind.FolderContents], cells.Select(line => line.List));
        var line = cells[0];
        Assert.False(line.Drive!.IsSet);
        Assert.Equal(SetupField.Volume, WidgetSetup.Needs(line));
        // The pin shows its folder's contents, and has no folder until the drive is chosen.
        Assert.True(PinFolders.Lists(cells[1]));
        Assert.Equal("", cells[1].Pin!.Target);
        Assert.Same(network ? WidgetInfos.NetworkDrive : WidgetInfos.Drive, FolderFaces.DriveOf(face));
        // What the settings ask first is the drive: the pin's folder follows from it.
        Assert.Same(line, Karpo.Core.Folders.ReadingOrder(folder).First(cell => WidgetSetup.Needs(cell.Island) is not null).Island);
    }

    /// <summary>The rows the presets keep for a drive line hold an unmeasured drive line's Full form (§432).</summary>
    [Fact]
    public void TheDrivesRowsAreAnUnmeasuredDriveLines()
    {
        Assert.InRange(LineSizes.CellsOf(ListWidgets.Of(ListKind.Drive), LineSize.Full), 1, Presets.DriveCells);
        Assert.InRange(LineSizes.CellsOf(ListWidgets.Of(ListKind.NetworkDrive), LineSize.Full), 1, Presets.DriveCells);
    }

    /// <summary>
    /// The drive chosen for the preset's line, once, is given to its folder pin with no folder yet:
    /// the drive's root by its letter, a share by its name; never to a pin that has one, nor a pin
    /// that shows just its folder.
    /// </summary>
    [Fact]
    public void TheDriveChosenIsGivenToTheFolderPinOnce()
    {
        var folder = Presets.Folder(Presets.Find(Presets.DriveId)!, new PresetMachine(false, [])).Folder!;
        var pin = folder.Cells.Last().Island.Pin!;
        Assert.Equal(1, DriveTargets.SpreadContents(folder, SystemTarget()));
        Assert.Equal(@"C:\", pin.Target);
        // Chosen again: the pin keeps the folder it was given.
        Assert.Equal(0, DriveTargets.SpreadContents(folder, Target(StickVolume, "E:", "Stick")));
        Assert.Equal(@"C:\", pin.Target);

        var share = Presets.Folder(Presets.Find(Presets.NetworkDriveId)!, new PresetMachine(false, [])).Folder!;
        Assert.Equal(1, DriveTargets.SpreadContents(share, ShareTarget()));
        Assert.Equal(Share, share.Cells.Last().Island.Pin!.Target);

        var plain = new FolderOptions { Lists = [[new FolderCell { Island = new CellItem { Kind = "pin", Pin = new PinOptions() } }]] };
        Assert.Equal(0, DriveTargets.SpreadContents(plain, SystemTarget()));
        Assert.Equal(0, DriveTargets.SpreadContents(folder, new DriveTargetOptions()));
        Assert.Equal("", DriveTargets.ContentsFolder(null));
        Assert.Equal(@"D:\", DriveTargets.ContentsFolder(Target(StickVolume, "d", "")));
        Assert.Equal(StickVolume, DriveTargets.ContentsFolder(Target(StickVolume, "", "")));
    }

    /// <summary>The entries are gone from the bar, and with them the name switch only the bar read; the lines act; the contents' switch is never written.</summary>
    [Fact]
    public void TheEntriesAreGone()
    {
        Assert.Null(BarEntries.Find("drive"));
        Assert.Null(BarEntries.Find("networkDrive"));
        Assert.DoesNotContain("drive", Enum.GetValues<BarKind>().Select(kind => kind.Id()));
        Assert.DoesNotContain("networkDrive", Enum.GetValues<BarKind>().Select(kind => kind.Id()));
        Assert.Null(typeof(DriveTargetOptions).GetProperty("ShowName"));
        Assert.Equal(CellAct.Action, ListWidgets.Find("drive")!.Act);
        Assert.Equal(CellAct.Action, ListWidgets.Find("networkDrive")!.Act);
        Assert.Null(Type.GetType("Karpo.Dock.Drives.DrivesFlyout, Karpo"));
        var line = ListKind.Drive.New();
        line.Drive = SystemTarget();
        Assert.DoesNotContain("contents", System.Text.Json.JsonSerializer.Serialize(line, ConfigJson.Default.CellItem), StringComparison.Ordinal);
    }

    // --- The lines --------------------------------------------------------------------------------------

    /// <summary>
    /// The drive's line is its information alone: the header, saying the file system and BitLocker as
    /// the flyout did, read once the open folder shows it; it lists nothing, whatever an earlier file
    /// said, and lights as one line. The folder pin under it lists the drive's top level in the
    /// flyout's order — folders first, names as a person reads them — watched, so a file saved into
    /// it shows a moment later.
    /// </summary>
    [UiFact]
    public void TheDriveLineIsItsInformationAndThePinListsItsTopLevel()
    {
        Offscreen.OnStaThread(gfx =>
        {
            using var stands = new Stands(Roots(), SystemDisk());
            using var folder = Made(gfx, ListKind.Drive, SystemTarget());
            var line = Assert.IsType<DriveIsland>(folder.Tiles[0].Island);
            var pin = Assert.IsType<FolderContentsLine>(folder.Tiles[1].Island);
            Assert.Equal(0, stands.Asked);

            folder.Opened(gfx);
            Assert.Equal(1, stands.Asked);
            Assert.Equal(new DriveFacts("NTFS", "BitLocker On"), line.Facts);
            Assert.False(line.LightsItsRows);
            var style = LineStyle(1f);
            Assert.True(line.ListHeight(300, style) > 0);
            Assert.Equal(line.ListHeight(300, style), line.ListHeight(600, style));

            var read = pin.Contents!.Contents;
            Assert.Equal(FolderListState.Listed, read.State);
            Assert.Equal(["PerfLogs", "Program Files", "Program Files (x86)", "Users", "Windows"], read.Entries.Where(entry => entry.Folder).Select(entry => entry.Name));
            Assert.Equal(@"C:\", pin.Contents.FolderPath);
            Assert.Contains(@"C:\", stands.Folders.Watched);

            // Looked at again, the volume is not asked again.
            folder.Closed();
            folder.Opened(gfx);
            Assert.Equal(1, stands.Asked);
        });
    }

    /// <summary>
    /// A click on the drive's line opens the drive in File Explorer once the folder has gone, as the
    /// flyout's header and footer did; a drive that is not there opens nothing.
    /// </summary>
    [UiFact]
    public void TheDriveLineOpensTheDrive()
    {
        Offscreen.OnStaThread(gfx =>
        {
            using var stands = new Stands(Roots(), SystemDisk());
            using var folder = Made(gfx, ListKind.Drive, SystemTarget());
            folder.Opened(gfx);
            var line = (DriveIsland)folder.Tiles[0].Island;
            Assert.Equal(IslandView.App, line.Click(0).View);
            Assert.Empty(stands.Folders.Opened);
            line.Open();
            Assert.Equal([(@"C:\", true)], stands.Folders.Opened);
            line.Open();
            Assert.Single(stands.Folders.Opened);

            stands.Drives = [];
            DriveWatch.Forget();
            _ = ((IIsland)line).Refresh(gfx);
            Assert.Equal(default, line.Click(0));
        });
    }

    /// <summary>
    /// A folder pin to a share is never asked on the dock's thread whether it is there, and never
    /// watched — a watcher's start waits on a share that has gone — but read again on the minute
    /// while it shows; one to a share that is gone says it is missing.
    /// </summary>
    [UiFact]
    public void AShareIsNeverWatched()
    {
        Offscreen.OnStaThread(gfx =>
        {
            using var stands = new Stands(Roots(), Connected());
            using var shared = Made(gfx, ListKind.NetworkDrive, ShareTarget(), key: $"{Owner}/share");
            var pin = (FolderContentsLine)shared.Tiles[1].Island;
            Assert.Equal(Share, pin.Folder);
            // Shut, it reads nothing, on its tick or any other.
            _ = ((IIsland)shared).Tick(gfx);
            _ = ((IIsland)pin).Tick(gfx);
            Assert.Equal(0, stands.Folders.Reads);
            shared.Opened(gfx);
            Assert.Equal(FolderListState.Listed, pin.Contents!.Contents.State);
            Assert.DoesNotContain(Share, stands.Folders.Watched);
            var reads = stands.Folders.Reads;
            _ = ((IIsland)pin).Tick(gfx);
            Assert.True(stands.Folders.Reads > reads, "a share's contents are read again on the tick");
            shared.Closed();
            reads = stands.Folders.Reads;
            _ = ((IIsland)pin).Tick(gfx);
            Assert.Equal(reads, stands.Folders.Reads);

            var gone = new DriveTargetOptions { Volume = @"\\server\gone", Label = "gone" };
            using var missing = Made(gfx, ListKind.NetworkDrive, gone, key: $"{Owner}/gone");
            var nothing = (FolderContentsLine)missing.Tiles[1].Island;
            Assert.Equal(@"\\server\gone", nothing.Folder);
            missing.Opened(gfx);
            Assert.Equal(FolderListState.Missing, nothing.Contents!.Contents.State);
        });
    }

    /// <summary>Whether a pin's target is on a network: a share's path, or a path on a mapped drive; never this PC's own volumes.</summary>
    [Fact]
    public void APlaceOnANetworkIsKnownWithoutAskingIt()
    {
        static bool Mapped(string root) => root == @"Z:\";

        Assert.True(PinFolders.OnNetwork(@"\\server\work", Mapped));
        Assert.True(PinFolders.OnNetwork(@"""\\server\work\Shared""", Mapped));
        Assert.True(PinFolders.OnNetwork(@"Z:\Shared", Mapped));
        Assert.True(PinFolders.OnNetwork("Z:", Mapped));
        Assert.False(PinFolders.OnNetwork(@"C:\Users", Mapped));
        Assert.False(PinFolders.OnNetwork(StickVolume, Mapped));
        Assert.False(PinFolders.OnNetwork(@"\\.\PhysicalDrive0", Mapped));
        Assert.False(PinFolders.OnNetwork("https://example.com", Mapped));
        Assert.False(PinFolders.OnNetwork("", Mapped));
    }

    // --- The face ---------------------------------------------------------------------------------------

    /// <summary>
    /// The face shows the folder's first drive line of its kind — the same line, so the face and the
    /// line are one drive, set in one place — says it to a screen reader as the entry did, and has the
    /// line's right-click rows; the bar shows a folder no tooltip.
    /// </summary>
    [UiFact]
    public void TheFaceReadsTheFoldersLine()
    {
        Offscreen.OnStaThread(gfx =>
        {
            using var stands = new Stands(Roots(), SystemDisk(), Connected());
            using var folder = Made(gfx, ListKind.Drive, SystemTarget());
            var line = Assert.IsType<DriveIsland>(folder.Tiles[0].Island);
            Assert.Same(line, folder.DriveLine);
            Assert.Same(line, folder.FaceWidget);
            Assert.Equal(line.Tooltip(0), folder.Tooltip(0));
            Assert.StartsWith("Local Disk (C:)", folder.Tooltip(0), StringComparison.Ordinal);
            Paint(folder, Bar());
            Assert.Equal(line.Look.Reading, folder.FaceShown);
            Assert.Equal([WidgetAct.OpenDrive, WidgetAct.Eject, WidgetAct.DriveProperties, WidgetAct.StorageSettings], TheDock.ActsFor(FolderFaces.Drive));
            Assert.Equal([WidgetAct.OpenDrive, WidgetAct.Disconnect, WidgetAct.DriveProperties, WidgetAct.FileExplorer], TheDock.ActsFor(FolderFaces.NetworkDrive));
            Assert.Equal(60_000, folder.IntervalMs);

            // A network drive line in a drive folder is not the drive face's.
            var share = ListKind.NetworkDrive.New();
            share.Drive = ShareTarget();
            using var other = Folder(gfx, share);
            Assert.Null(other.DriveLine);
        });
    }

    /// <summary>
    /// The drive filling up, going and coming back paints the face again in place, never another size;
    /// the face reads the drive on its own tick with the folder shut, never what is on it (the pin
    /// here has no folder: what a folder pin reads is its own, FolderPinContentsTests).
    /// </summary>
    [UiFact]
    public void TheFacePaintsAgainInPlaceAsTheDriveChanges()
    {
        Offscreen.OnStaThread(gfx =>
        {
            using var stands = new Stands(Roots(), SystemDisk());
            using var folder = Made(gfx, ListKind.Drive, SystemTarget(), spread: false);
            var style = Bar();
            var size = folder.CellSize(0, style);
            _ = folder.Measure(style);
            _ = folder.Surface(0, style);
            var before = folder.FaceShown;

            stands.Drives = [SystemDisk(free: 9)];
            DriveWatch.Forget();
            _ = ((IIsland)folder).Tick(gfx);
            Assert.NotEqual(before, folder.FaceShown);
            Assert.Equal(size, folder.CellSize(0, style));

            stands.Drives = [];
            DriveWatch.Forget();
            Assert.False(((IIsland)folder).Refresh(gfx));
            Assert.Contains("True", folder.FaceShown, StringComparison.Ordinal);
            Assert.Equal(size, folder.CellSize(0, style));
            Assert.Equal(0, stands.Folders.Reads);
        });
    }

    private static void Paint(FolderIsland folder, in IslandStyle style)
    {
        var (width, height) = folder.CellSize(0, style);
        var copy = style;
        Offscreen.Bitmap(width, height, dc => folder.PaintCell(dc, 0, copy, width, height), _ => { });
    }

    // --- Pictures ---------------------------------------------------------------------------------------

    /// <summary>
    /// The presets as they open, at 150 %: the system drive and its top level, one nearly full, a
    /// stick that is empty, a disconnected share, no drive chosen yet, and the system drive at 225 %
    /// text.
    /// </summary>
    [UiFact]
    public void DrivePresetPictures()
    {
        foreach (var dark in new[] { true, false })
        {
            Offscreen.OnStaThread(gfx =>
            {
                var folders = new List<FolderIsland>();
                var panels = new List<(string, Look, FolderIsland, FolderPlan)>();
                using var stands = new Stands(Roots().With(@"F:\").With(@"D:\", "Archive\\", "Games\\", "Videos\\", "disk image.iso"), SystemDisk(), Disk(3, "Data", 500, 9, volume: StickVolume), Disk(5, "Stick", 58, 57, DriveKind.Removable, @"\\?\Volume{66666666-6666-6666-6666-666666666666}\"));
                try
                {
                    void Add(string caption, ListKind kind, DriveTargetOptions? target, float text = 1f)
                    {
                        var look = LookFor(dark, text);
                        var folder = Made(gfx, kind, target, key: $"{Owner}/drives-{panels.Count}");
                        folders.Add(folder);
                        folder.Opened(gfx);
                        panels.Add((caption, look, folder, Plan(folder, look, 1.5f).Plan));
                    }

                    Add("Local Disk (C:)", ListKind.Drive, SystemTarget());
                    Add("Nearly full", ListKind.Drive, Target(StickVolume, "D:", "Data"));
                    Add("An empty stick", ListKind.Drive, Target(@"\\?\Volume{66666666-6666-6666-6666-666666666666}\", "F:", "Stick"));
                    Add("Disconnected share", ListKind.NetworkDrive, Target(@"\\server\gone", "", @"gone (\\server)"));
                    Add("No drive yet", ListKind.Drive, null);
                    Add("225 % text", ListKind.Drive, SystemTarget(), 2.25f);
                    Sheet(gfx, $"drive-preset-{(dark ? "dark" : "light")}.png", 1.5f, panels);
                }
                finally
                {
                    foreach (var folder in folders)
                    {
                        folder.Closed();
                        folder.Dispose();
                    }
                }
            });
        }
    }

    /// <summary>The share connected, at 150 %, its header's words and the pin's rows, and a row lit: drive-preset-share-dark.png.</summary>
    [UiFact]
    public void SharePresetPicture()
    {
        Offscreen.OnStaThread(gfx =>
        {
            using var stands = new Stands(Roots(), Connected());
            var look = LookFor(true);
            using var folder = Made(gfx, ListKind.NetworkDrive, ShareTarget());
            folder.Opened(gfx);
            using var lit = Made(gfx, ListKind.NetworkDrive, ShareTarget(), key: $"{Owner}/lit");
            lit.Opened(gfx);
            var pin = (FolderContentsLine)lit.Tiles[1].Island;
            var style = FolderFlyout.StyleFor(1.5f, look, FlyoutTheme.For(look));
            var width = PopupPaint.Round(Flyout.PanelWidthDip * 1.5f);
            var box = PopupPaint.Rect(0, 0, width, pin.ListHeight(width, style));
            pin.HoverOn(Target(pin, box, style, FolderContentsView.OpenTarget + 1), box, style, WidgetSurface.List);
            Sheet(gfx, "drive-preset-share-dark.png", 1.5f,
            [
                ("work (\\\\server)", look, folder, Plan(folder, look, 1.5f).Plan),
                ("A row lit", look, lit, Plan(lit, look, 1.5f).Plan),
            ]);
        });
    }
}
