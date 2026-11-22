// §643: what a ghost of a widget that has not been set up yet is set up with (the person, 2026-11-09: "also widgets like the
// drive one show no drive choosen yet, in preview it should just choose one best effort or even make one up so that the user can
// see how the widget looks configured. this should be done for all the widgets where this is currently an issue.").
//
// **The one table** (Dress): the add page's ghost is shown from a copy of its own copy of the config, its widget's settings filled
// in where they are empty (AddList.Into: AddGhost.Shown). This PC's own where it has an obvious one, else made up:
//
//   drive              this PC's system drive (C:), read as the placed widget reads it
//   network drive      a made-up share, \\nas\media, drawn from here: no share is ever asked (DriveOf)
//   folder contents    the Documents folder; in a drive's ready-made group, the drive's root, as its one question would
//                      give it (DriveTargets.SpreadContents); the made-up share's, a made-up listing (Folders)
//   pin                File Explorer
//   camera             a made-up camera, its picture drawn here (Camera): no stream is opened
//
// and, read while the widget is made showing the samples (CellIsland.Sampled), where this PC's own is empty:
//
//   note               the notebook's note, else a made-up one (Note)
//   to-do              the notebook's list, else a made-up one (Tasks)
//
// Nothing is written: the copy is the ghost's alone; the click puts in the ghost's copy without them (AddGhost.Config), the
// widget placed made again from it (Samples.Outlived), wanting setting up as a new one does, its settings opened at the field.

using Karpo.Core;
using Karpo.Dock.Drives;

namespace Karpo.Dock;

internal static partial class Samples
{
    // --- The table --------------------------------------------------------------------------------------

    /// <summary>
    /// <paramref name="item"/> set up where it is not, and every widget inside it (a ready-made group's lines): what a ghost shows
    /// (see the top of the file). Only what is empty is filled; a setting already there stays.
    /// </summary>
    public static void Dress(WidgetItem? item)
    {
        if (item is BarItem { Folder: { } folder })
        {
            var cells = folder.Cells.Select(cell => cell.Island).ToList();
            // The drives first: a drive's group's folder contents show the drive's root (§119's one question, answered).
            foreach (var cell in cells.Where(cell => cell.Info?.Options == WidgetOptionsKind.Drive))
            {
                Dress(cell);
            }
            if (cells.Select(cell => cell.Drive).FirstOrDefault(drive => drive is { IsSet: true }) is { } chosen)
            {
                _ = DriveTargets.SpreadContents(folder, chosen);
            }
            foreach (var cell in cells)
            {
                Dress(cell);
            }
        }
        if (item?.Info is not { } info)
        {
            return;
        }
        switch (WidgetSetup.Needs(item))
        {
            case SetupField.Volume when info.Drives == DriveFilter.Network:
                item.Drive = new DriveTargetOptions { Volume = Share, Label = ShareLabel };
                break;
            case SetupField.Volume:
                item.Drive = new DriveTargetOptions { Letter = DriveTargets.Letter(Path.GetPathRoot(Environment.SystemDirectory)) };
                break;
            case SetupField.Target:
                (item.Pin ??= new PinOptions()).Target = PinFolders.Lists(item) ? Documents : Explorer;
                break;
            case SetupField.Stream:
                item.Camera = new CameraOptions { Url = CameraUrl, Name = CameraName };
                break;
        }
    }

    /// <summary>A pin's: File Explorer, which every Windows has, by its ID in the Apps folder, as Start names it.</summary>
    private const string Explorer = @"shell:AppsFolder\Microsoft.Windows.Explorer";

    /// <summary>Folder contents': the Documents folder, else the person's own.</summary>
    private static string Documents =>
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) is { Length: > 0 } documents ? documents
        : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    // --- The network drive ------------------------------------------------------------------------------

    /// <summary>The made-up share a network drive's ghost shows: never asked, its space and what it holds drawn from here.</summary>
    public const string Share = @"\\nas\media";

    /// <summary>What Explorer would call <see cref="Share"/> mapped.</summary>
    private const string ShareLabel = @"media (\\nas)";

    /// <summary>Whether <paramref name="path"/> is the made-up share or in it.</summary>
    public static bool OnShare(string? path) =>
        path is { } text && text.StartsWith(Share, StringComparison.OrdinalIgnoreCase)
        && (text.Length == Share.Length || text[Share.Length] is '\\' or '/');

    /// <summary>
    /// The drive a ghost's drive line shows where this PC gives none: the made-up share's (4 TB, 60 % full), or a local disk of
    /// 512 GB for a drive that is not there. No letter: nothing is ever read at it.
    /// </summary>
    public static Drive DriveOf(DriveTargetOptions target) => OnShare(target.Volume)
        ? new Drive { Letter = -1, Kind = DriveKind.Remote, Volume = Share, Label = ShareLabel, Total = 4_000_787_030_016, Free = 1_612_316_786_688 }
        : new Drive { Letter = -1, Kind = DriveKind.Fixed, Volume = target.Volume, Label = target.Label.Length > 0 ? target.Label : DriveTargets.PlainLabel(DriveKind.Fixed), Total = 512_110_190_592, Free = 214_748_364_800 };

    /// <summary>The made-up share as a folder's contents name it: its name, the folder's glyph.</summary>
    public static Item ShareItem(string target) => new() { Target = target, Name = "media", Kind = ItemKind.Shell, Keys = [] };

    /// <summary>
    /// What a ghost's folder contents read: <paramref name="real"/>, but for the made-up share, whose listing is made up here
    /// (folders and files of a media share, no icon of the shell's: their glyphs) and never watched.
    /// </summary>
    public static FolderSource Folders(FolderSource real) => new()
    {
        Enumerate = path => OnShare(path) ? ShareListing : real.Enumerate(path),
        Exists = path => OnShare(path) || real.Exists(path),
        Icon = (path, px) => OnShare(path) ? null : real.Icon(path, px),
        Watch = (path, told) => OnShare(path) ? null : real.Watch(path, told),
        Open = real.Open,
        Run = real.Run,
        TypeName = real.TypeName,
        Thumbnail = real.Thumbnail is { } thumbnail ? (path, px) => OnShare(path) ? null : thumbnail(path, px) : null,
        Holds = real.Holds,
    };

    /// <summary>The made-up share's: a media server's folders and a few files, changed lately.</summary>
    private static IEnumerable<FolderItem> ShareListing
    {
        get
        {
            var now = Clock.Real();
            return
            [
                new("Films", true, now.AddDays(-2)),
                new("Music", true, now.AddDays(-5)),
                new("Photos 2026", true, now.AddHours(-20)),
                new("Series", true, now.AddDays(-12)),
                new("Backups", true, now.AddDays(-1)),
                new("Holiday in Ticino.mp4", false, now.AddDays(-30), Size: 2_147_483_648),
                new("Playlist.m3u", false, now.AddDays(-3), Size: 4_096),
                new("Read me.txt", false, now.AddDays(-90), Size: 1_024),
            ];
        }
    }

    // --- The camera -------------------------------------------------------------------------------------

    /// <summary>The made-up camera's address: the reserved example domain, never asked (CameraLine shows <see cref="Camera"/>).</summary>
    private const string CameraUrl = "https://camera.example.com/lake.jpg";

    /// <summary>The made-up camera's name.</summary>
    private const string CameraName = "Lake Zürich";

    private static Pixels? lake;

    /// <summary>
    /// What a ghost's camera shows: a lake under mountains at dusk, drawn here, as a snapshot taken a minute ago — no stream
    /// opened, nothing asked of anybody.
    /// </summary>
    public static CameraShot Camera() =>
        new(lake ??= Lake(), 1, CameraState.Live, null, CameraProtocol.Snapshot, Clock.RealUtc().AddMinutes(-1));

    /// <summary>The picture: sky, a low sun, two ranges of mountains and the lake that mirrors them, 640 × 360, opaque.</summary>
    private static Pixels Lake()
    {
        const int Width = 640, Height = 360, Shore = 232;
        var data = new byte[Width * Height * 4];
        static float Ridge(float x, float a, float b, float c) =>
            (MathF.Sin(x * a) * 0.5f) + (MathF.Sin((x * b) + 1.3f) * 0.3f) + (MathF.Sin((x * c) + 0.4f) * 0.2f);
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var fx = x / (float)Width;
                // Above the shore the scene; under it, the scene mirrored, darker and bluer.
                var mirrored = y >= Shore;
                var sy = mirrored ? (2 * Shore) - y : y;
                var t = sy / (float)Shore;
                // Sky: deep blue at the top to a warm glow at the horizon, a sun low on the right.
                var (r, g, b) = (Lerp(38, 250, t * t), Lerp(64, 176, t * t), Lerp(128, 120, t));
                var sun = MathF.Sqrt(MathF.Pow((fx - 0.72f) * Width, 2) + MathF.Pow(sy - 178, 2));
                if (sun < 22)
                {
                    (r, g, b) = (255, 236, 190);
                }
                else if (sun < 70)
                {
                    var glow = 1 - ((sun - 22) / 48);
                    (r, g, b) = (Lerp(r, 255, glow * 0.6f), Lerp(g, 214, glow * 0.6f), Lerp(b, 160, glow * 0.6f));
                }
                // The far range, blue-grey, then the near one, dark.
                if (sy > 150 + (Ridge(fx * 9, 1.0f, 2.3f, 5.1f) * 34))
                {
                    (r, g, b) = (Lerp(92, 70, t), Lerp(100, 78, t), Lerp(134, 110, t));
                }
                if (sy > 190 + (Ridge((fx * 7) + 3, 1.2f, 2.9f, 6.3f) * 26))
                {
                    (r, g, b) = (34, 44, 52);
                }
                if (mirrored)
                {
                    var deep = (y - Shore) / (float)(Height - Shore);
                    var ripple = 0.92f + (0.08f * MathF.Sin((y * 0.9f) + (MathF.Sin(x * 0.05f) * 2)));
                    (r, g, b) = (Lerp(r * 0.72f, 18, deep) * ripple, Lerp(g * 0.78f, 34, deep) * ripple, Lerp(b * 0.9f, 58, deep) * ripple);
                }
                var at = ((y * Width) + x) * 4;
                data[at] = Byte(b);
                data[at + 1] = Byte(g);
                data[at + 2] = Byte(r);
                data[at + 3] = 255;
            }
        }
        return new Pixels { Data = data, Width = Width, Height = Height };

        static float Lerp(float from, float to, float t) => from + ((to - from) * Math.Clamp(t, 0, 1));
        static byte Byte(float value) => (byte)Math.Clamp(MathF.Round(value), 0, 255);
    }

    // --- The notebook -----------------------------------------------------------------------------------

    /// <summary>What a ghost's note shows where the notebook's is empty: a short note in Markdown.</summary>
    public static Note Note() => new()
    {
        Id = "sample",
        Title = "Weekend",
        Body = "Train to **Lucerne** at 9:12, back Sunday evening.\n\n- [x] Book the hotel\n- [ ] Pick up the parcel\n- [ ] Call Anna about dinner",
        Modified = Clock.RealOffset().AddHours(-3),
    };

    /// <summary>What a ghost's to-do list shows where the notebook's is empty: four tasks, one due today, one starred, one done.</summary>
    public static TodoList Tasks()
    {
        var now = Clock.RealOffset();
        return new TodoList
        {
            Id = "sample",
            Name = "Tasks",
            Items =
            [
                new() { Id = "sample-1", Text = "Send the slides to Anna", Created = now.AddDays(-1), Due = DateOnly.FromDateTime(now.Date), Important = true },
                new() { Id = "sample-2", Text = "Renew the parking permit", Created = now.AddDays(-3) },
                new() { Id = "sample-3", Text = "Buy a birthday present for Leo", Created = now.AddDays(-2), Due = DateOnly.FromDateTime(now.Date.AddDays(3)) },
                new() { Id = "sample-4", Text = "Water the plants", Created = now.AddDays(-4), Done = true, Finished = now.AddHours(-5) },
            ],
        };
    }
}
