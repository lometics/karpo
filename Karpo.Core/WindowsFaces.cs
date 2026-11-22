// §474: Windows' own widgets wear Windows' own faces (the person, 2026-11-02: "it should not be implemented in the
// folder, these widgets should be their own widgets and the face and the contents (obviously) cannot be edited. only if
// they use ours, but ours is just a folder face with widgets as presets inside.").
//
// So there are two kinds of thing, never one folder with a switch. Karpo's own is a folder: a face with widgets inside,
// the ready-made ones presets (Presets.cs), every bit of it editable. Windows' own is a taskbar entry (§466) whose click
// opens Windows' menu, and three of them are the buttons Windows' own taskbar has for their menus, and look as those do:
//
//   * widgetsBoard: Windows' Widgets button — the weather's glyph, the temperature over the conditions
//     (WeatherFace.Conditions), where this PC is;
//   * windowsQuickSettings: quick settings' button — the network, the volume and, on a PC with one, the battery, its
//     percentage beside it only where Windows' own "Battery percentage" is on (§485, WindowsTaskbar.BatteryPercent);
//   * windowsNotifications: the clock — the time over the date as Windows writes them — and the bell beside it.
//
// §490 (the person, 2026-11-02: "the windows menu taskbar widgets dont look like the ones on the folder faces, the icons
// are a bit different etc. i want them to look exactly the same as the other taskbar widgets and adhere to the same
// styling rules but i want them to carry the content that the windows native taskbar widget would carry as well"): the
// rest of Windows' menus wore one glyph in a button's well, drawn bigger than a face's icon and some of another glyph.
// Now every one wears the faces of the Karpo folder that stands for its menu: the sound page the volume, the Wi-Fi page
// the network, cast the cast face, clipboard history the clipboard's; the quick link menu and the emoji panel, which no
// folder stands for, the icon face of their own glyph. Worn as a folder's face is (its style's Taskbar widgets, §499).
//
// The faces are Karpo's own (FolderFaces), drawn by the code that draws a folder's (FaceDrawings) from the same readings,
// so nothing is drawn twice. They are fixed here and kept in no file: nothing of them is set, and there is nothing inside.
// What stays Windows' is their content: the time and the date in Windows' formats, the battery's percentage as Windows'
// setting says, the weather's conditions on the Widgets button.

namespace Karpo.Core;

/// <summary>§474: the faces Windows' own widgets wear, fixed (see the top of the file).</summary>
public static class WindowsFaces
{
    /// <summary>Whether a taskbar entry of <paramref name="kind"/> wears faces (<see cref="Of"/>): the Widgets button and each of Windows' menus (§490).</summary>
    public static bool WearsWindowsFace(this BarKind kind) => kind == BarKind.WidgetsBoard || kind.IsWindowsMenu();

    /// <summary>
    /// The faces Windows' button of <paramref name="kind"/> shows, left to right, on a PC with a battery or without
    /// (<paramref name="battery"/>: Windows shows the battery only where there is one), the battery's percentage beside it
    /// or not as Windows' "Battery percentage" says (<paramref name="percent"/>, §485); null for every other kind. A new
    /// set each time: nothing shares it, and nothing writes it.
    /// </summary>
    public static FolderOptions? Of(BarKind kind, bool battery, bool percent) => kind switch
    {
        BarKind.WidgetsBoard => new FolderOptions { Faces = [Weather()] },
        BarKind.WindowsQuickSettings => new FolderOptions
        {
            Faces = [FolderFace.Of(FolderFaces.WifiId), FolderFace.Of(FolderFaces.SoundId), .. battery ? [new FolderFace { Kind = FolderFaces.BatteryId, NoPercent = !percent }] : Array.Empty<FolderFace>()],
        },
        BarKind.WindowsNotifications => new FolderOptions { Faces = [FolderFace.Of(FolderFaces.DateTimeId), FolderFace.Of(FolderFaces.NotificationsId)] },
        BarKind.WindowsSound => One(FolderFace.Of(FolderFaces.SoundId)),
        BarKind.WindowsNetworks => One(FolderFace.Of(FolderFaces.WifiId)),
        BarKind.WindowsCast => One(FolderFace.Of(FolderFaces.CastId)),
        BarKind.WindowsClipboard => One(FolderFace.Of(FolderFaces.ClipboardId)),
        BarKind.QuickLink or BarKind.EmojiPanel => One(new FolderFace { Kind = FolderFaces.IconId, Glyph = kind.Glyph() }),
        _ => null,
    };

    private static FolderOptions One(FolderFace face) => new() { Faces = [face] };

    /// <summary>Windows' Widgets button's weather: the glyph, the temperature over the conditions, where this PC is.</summary>
    private static FolderFace Weather()
    {
        var face = WeatherFace.New(WeatherFace.Conditions);
        face.Weather = WeatherOptions.New();
        return face;
    }
}
