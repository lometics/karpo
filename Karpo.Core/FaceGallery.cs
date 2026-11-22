// The gallery a folder's face is picked from: every face in
// a group of its kind, found by its name, what it shows and the words of the widget it shows, and
// whether this folder may wear it where it is asked for. The person, 2026-10-11: "there are just to
// many faces to just put them into a dropdown list". The bar's own "Add a face" submenu offers the
// faces in the same groups (Karpo.Dock\OptionMenu.cs, docs\widgets.md §214), so it is the core's.
//
// Kept here because it is a decision rather than a control: a face in no group, or one offered that
// the folder cannot wear, is a bug nobody sees until they look for it.

namespace Karpo.Core;

/// <summary>A group of the face gallery, in the order it shows them.</summary>
public enum FaceGroup
{
    /// <summary>Its icon: the folder itself.</summary>
    Folder,

    /// <summary>Clocks, the date, the countdowns and the stopwatch.</summary>
    Time,

    /// <summary>Wi-Fi, Bluetooth, the hotspot, casting, sharing and VPN.</summary>
    Connections,

    /// <summary>The speaker and what is playing.</summary>
    Media,

    /// <summary>The readings, the battery and power.</summary>
    System,

    /// <summary>Drives, network drives and removable devices.</summary>
    Drives,

    /// <summary>Weather, stocks, news, mail, notifications and contacts.</summary>
    Info,

    /// <summary>What Windows itself has: the clipboard, desktops, the keyboard layout, the taskbar's layout.</summary>
    Windows,

    /// <summary>A face nobody put in a group yet; shown only where there is one.</summary>
    Other,
}

/// <summary>A face as the gallery shows it: whether it can be picked, why not, and whether it is the one in that place now.</summary>
/// <param name="Face">The face.</param>
/// <param name="Available">Whether picking it does anything.</param>
/// <param name="Reason">Why it cannot be picked; empty where it can.</param>
/// <param name="Current">Whether it is the face in the place being changed.</param>
public readonly record struct FaceTile(FaceInfo Face, bool Available, string Reason, bool Current);

/// <summary>The face gallery's groups, search and availability.</summary>
public static class FaceGallery
{
    // Each face's group, and the widget whose search words find it too: the face shows what that
    // widget shows ("ssid" finds the Wi-Fi face as it finds the Wi-Fi line).
    static readonly Dictionary<FaceInfo, (FaceGroup Group, WidgetInfo? Widget)> Kinds = new()
    {
        [FolderFaces.Icon] = (FaceGroup.Folder, null),
        [FolderFaces.Clock] = (FaceGroup.Time, WidgetInfos.Clock),
        [FolderFaces.Calendar] = (FaceGroup.Time, WidgetInfos.Calendar),
        [FolderFaces.Meeting] = (FaceGroup.Time, WidgetInfos.Meeting),
        [FolderFaces.DateTime] = (FaceGroup.Time, WidgetInfos.Clock),
        [FolderFaces.Timer] = (FaceGroup.Time, WidgetInfos.Timer),
        [FolderFaces.Focus] = (FaceGroup.Time, WidgetInfos.Focus),
        [FolderFaces.Stopwatch] = (FaceGroup.Time, WidgetInfos.Stopwatch),
        [FolderFaces.Wifi] = (FaceGroup.Connections, WidgetInfos.Wifi),
        [FolderFaces.Bluetooth] = (FaceGroup.Connections, WidgetInfos.Bluetooth),
        [FolderFaces.Hotspot] = (FaceGroup.Connections, WidgetInfos.Hotspot),
        [FolderFaces.Cast] = (FaceGroup.Connections, WidgetInfos.WirelessDisplays),
        [FolderFaces.Nearby] = (FaceGroup.Connections, WidgetInfos.NearbySharing),
        [FolderFaces.Vpn] = (FaceGroup.Connections, WidgetInfos.Vpn),
        [FolderFaces.Sound] = (FaceGroup.Media, WidgetInfos.Volume),
        [FolderFaces.NowPlaying] = (FaceGroup.Media, WidgetInfos.Media),
        [FolderFaces.Cpu] = (FaceGroup.System, WidgetInfos.Cpu),
        [FolderFaces.Gpu] = (FaceGroup.System, WidgetInfos.Gpu),
        [FolderFaces.Memory] = (FaceGroup.System, WidgetInfos.Memory),
        [FolderFaces.Disk] = (FaceGroup.System, WidgetInfos.Disk),
        [FolderFaces.NetworkActivity] = (FaceGroup.System, WidgetInfos.Network),
        [FolderFaces.ScreenTime] = (FaceGroup.System, WidgetInfos.ScreenTime),
        [FolderFaces.Battery] = (FaceGroup.System, WidgetInfos.Battery),
        [FolderFaces.Power] = (FaceGroup.System, WidgetInfos.ShutDown),
        [FolderFaces.Drive] = (FaceGroup.Drives, WidgetInfos.Drive),
        [FolderFaces.NetworkDrive] = (FaceGroup.Drives, WidgetInfos.NetworkDrive),
        [FolderFaces.Removable] = (FaceGroup.Drives, WidgetInfos.RemovableDevices),
        [FolderFaces.Weather] = (FaceGroup.Info, WidgetInfos.WeatherNow),
        [FolderFaces.AirQuality] = (FaceGroup.Info, WidgetInfos.AirQuality),
        [FolderFaces.Tides] = (FaceGroup.Info, WidgetInfos.Tides),
        [FolderFaces.Quakes] = (FaceGroup.Info, WidgetInfos.Quakes),
        [FolderFaces.Sun] = (FaceGroup.Info, WidgetInfos.SunArc),
        [FolderFaces.Markets] = (FaceGroup.Info, WidgetInfos.Markets),
        [FolderFaces.News] = (FaceGroup.Info, WidgetInfos.Rss),
        [FolderFaces.Sports] = (FaceGroup.Info, WidgetInfos.Sports),
        [FolderFaces.Mail] = (FaceGroup.Info, WidgetInfos.Mail),
        [FolderFaces.Notifications] = (FaceGroup.Info, WidgetInfos.NotificationsList),
        [FolderFaces.Contacts] = (FaceGroup.Info, WidgetInfos.Contacts),
        [FolderFaces.WinSearch] = (FaceGroup.Windows, WidgetInfos.WinSearch),
        [FolderFaces.Clipboard] = (FaceGroup.Windows, WidgetInfos.Clipboard),
        [FolderFaces.Desktops] = (FaceGroup.Windows, WidgetInfos.Desktops),
        [FolderFaces.Keyboard] = (FaceGroup.Windows, WidgetInfos.Keyboard),
        [FolderFaces.Layouts] = (FaceGroup.Windows, WidgetInfos.Layouts),
    };

    /// <summary>The group <paramref name="face"/> stands in; Other for one nobody put in a group.</summary>
    public static FaceGroup GroupOf(FaceInfo face) => Kinds.TryGetValue(face, out var kind) ? kind.Group : FaceGroup.Other;

    /// <summary>What a group's heading says.</summary>
    public static string Title(FaceGroup group) => group switch
    {
        FaceGroup.Folder => S.SettingsFolderFaceGroupFolder,
        FaceGroup.Time => S.SettingsFolderFaceGroupTime,
        FaceGroup.Connections => S.SettingsFolderFaceGroupConnections,
        FaceGroup.Media => S.SettingsFolderFaceGroupMedia,
        FaceGroup.System => S.SettingsFolderFaceGroupSystem,
        FaceGroup.Drives => S.SettingsFolderFaceGroupDrives,
        FaceGroup.Info => S.SettingsFolderFaceGroupInfo,
        FaceGroup.Windows => S.SettingsFolderFaceGroupWindows,
        _ => S.SettingsFolderFaceGroupOther,
    };

    /// <summary>§249: a group's icon, on its row of a menu's "Add a face".</summary>
    public static string Glyph(FaceGroup group) => group switch
    {
        FaceGroup.Folder => OptionGlyphs.Folder,
        FaceGroup.Time => OptionGlyphs.Clock,
        FaceGroup.Connections => "\uE701",
        FaceGroup.Media => "\uE8D6",
        FaceGroup.System => OptionGlyphs.Reading,
        FaceGroup.Drives => OptionGlyphs.Disk,
        FaceGroup.Info => OptionGlyphs.Feed,
        FaceGroup.Windows => OptionGlyphs.Windows,
        _ => OptionGlyphs.Tiles,
    };

    /// <summary>§249: <paramref name="face"/>'s icon where a menu lists it: its own, the tiles for one that has none.</summary>
    public static string GlyphOf(FaceInfo face) => face.Glyph.Length > 0 ? face.Glyph : OptionGlyphs.Tiles;

    /// <summary>
    /// Whether <paramref name="query"/> finds <paramref name="face"/>: every word of it in the face's
    /// name, what it shows, its group's name, or the search words of the widget it shows — and in
    /// another language their English too (<see cref="CatalogueSearch.Matches"/>). An empty query finds every face.
    /// </summary>
    public static bool Finds(string? query, FaceInfo face)
    {
        var widget = Kinds.TryGetValue(face, out var kind) ? kind.Widget : null;
        IEnumerable<string> words =
        [
            face.Blurb, Title(GroupOf(face)),
            .. Strings.AlsoEnglish($"core.face.{face.Id}.title", $"core.face.{face.Id}.blurb"),
            .. widget?.Keywords ?? [],
        ];
        return CatalogueSearch.Matches(query, face.Title, words);
    }

    /// <summary>
    /// The faces <paramref name="query"/> finds, in their groups in <see cref="FaceGroup"/>'s order and
    /// each group's in <see cref="FolderFaces.All"/>'s; a group none of whose faces is found is left
    /// out. Each says whether <paramref name="folder"/> may wear it: added after its faces where
    /// <paramref name="replacing"/> is null (<see cref="FaceRows.CanAdd"/>), else put in the place of
    /// its face at <paramref name="replacing"/> (<see cref="FolderFaces.CanWear"/>), that face itself
    /// marked as the one there.
    /// </summary>
    public static IReadOnlyList<(FaceGroup Group, IReadOnlyList<FaceTile> Tiles)> Search(FolderOptions folder, int? replacing, string? query)
    {
        var tiles = FolderFaces.All.Where(face => Finds(query, face)).Select(face => Tile(folder, replacing, face)).ToList();
        return
        [
            .. Enum.GetValues<FaceGroup>()
                .Select(group => (group, (IReadOnlyList<FaceTile>)[.. tiles.Where(tile => GroupOf(tile.Face) == group)]))
                .Where(found => found.Item2.Count > 0),
        ];
    }

    /// <summary>How <paramref name="face"/> stands for <paramref name="folder"/> (<see cref="Search"/>).</summary>
    public static FaceTile Tile(FolderOptions folder, int? replacing, FaceInfo face)
    {
        if (replacing is { } at && at >= 0 && at < folder.Faces.Count)
        {
            var current = folder.Faces[at].Info == face;
            return FolderFaces.CanWear(folder, face, at)
                ? new FaceTile(face, true, "", current)
                : new FaceTile(face, false, S.SettingsFolderFacesWorn, false);
        }
        return FolderFaces.CanWear(folder, face)
            ? new FaceTile(face, true, "", false)
            : new FaceTile(face, false, S.SettingsFolderFacesWorn, false);
    }

    /// <summary>The one face <paramref name="query"/> finds that can be picked, where it finds exactly one: what Enter in the search box picks.</summary>
    public static FaceInfo? Only(FolderOptions folder, int? replacing, string? query) =>
        Search(folder, replacing, query).SelectMany(group => group.Tiles).Where(tile => tile.Available).ToList() is [var only] ? only.Face : null;
}
