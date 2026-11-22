// menus: what a folder's right click on the bar works, face by face (Dock.FaceMenus.cs; the person,
// 2026-10-11: "most of the right click context menus of the taskbar widgets are empty ... they
// should show options related to the widget face, if there are multiple faces it should show all of
// them in order").
//
// Each face's rows work the widget it shows (WidgetOf): the line a timer, a drive or a news face
// reads, what is playing, else the folder itself, which then does what the face's own rows ask —
// switch the Wi-Fi or the Bluetooth radio, mute the output, set the power mode. Nothing is read
// again from a face's menu: the line's own menu has "Refresh" (§451). The radios are followed from the
// moment the face is worn, so their
// rows say "on" or "off" from the first right click; energy saver and the power modes only once a
// menu asks, as the battery line's are (BatteryIsland.Saver): nobody right-clicks most folders.

using Karpo.Core;
using Karpo.Dock.QuickSettings.Services;

namespace Karpo.Dock;

internal sealed partial class FolderIsland
{
    private IQuickSwitch? wifiRadio;
    private IQuickSwitch? bluetoothRadio;
    private IQuickSwitch? faceSaver;
    private IBatterySource? powerModes;

    /// <summary>
    /// menus: the widget the rows of <paramref name="face"/> work: the line a timer's, focus' or
    /// stopwatch's face shows, the drive line a drive face shows, the feed line a news face shows,
    /// what is playing, the folder's first contacts line; the folder itself for every other face,
    /// whose rows it answers (<see cref="Radio"/>, <see cref="ToggleMute"/>).
    /// </summary>
    internal IIsland? WidgetOf(FolderFace face)
    {
        var info = face.Info;
        return info == FolderFaces.NowPlaying ? NowPlaying
            : info == FolderFaces.News ? Feed
            : info == FolderFaces.Contacts ? tiles.Select(tile => tile.Island).OfType<Pim.ContactsLine>().FirstOrDefault()
            : FolderFaces.DriveOf(info) is { } drive ? DriveLineOf(drive)
            : FolderFaces.LineOf(info) is not null ? FaceLine(options, face) is { } line ? widgets.GetValueOrDefault($"{Key}/{line.Id}") : null
            : this;
    }

    /// <summary>
    /// menus: the Wi-Fi or the Bluetooth radio (<see cref="QuickSwitch.WifiRadio"/>,
    /// <see cref="QuickSwitch.BluetoothRadio"/>), followed while the folder wears its face
    /// (<see cref="FollowRadios"/>), else made and started now; null for any other switch.
    /// </summary>
    internal IQuickSwitch? Radio(QuickSwitch which)
    {
        switch (which)
        {
            case QuickSwitch.WifiRadio:
                return wifiRadio ??= Started(which);
            case QuickSwitch.BluetoothRadio:
                return bluetoothRadio ??= Started(which);
            default:
                return null;
        }
    }

    /// <summary>
    /// menus: the radios the folder's faces stand for followed from the moment it wears them, so the
    /// first right click already says whether the radio is on; let go when the face comes off.
    /// </summary>
    private void FollowRadios()
    {
        if (options.Wears(FolderFaces.Wifi))
        {
            wifiRadio ??= Started(QuickSwitch.WifiRadio);
        }
        else
        {
            wifiRadio?.Dispose();
            wifiRadio = null;
        }
        if (options.Wears(FolderFaces.Bluetooth))
        {
            bluetoothRadio ??= Started(QuickSwitch.BluetoothRadio);
        }
        else
        {
            bluetoothRadio?.Dispose();
            bluetoothRadio = null;
        }
    }

    /// <summary>The switch <paramref name="which"/>, started: what it asks Windows goes to the pool, and its news paints the face again.</summary>
    private IQuickSwitch Started(QuickSwitch which)
    {
        var made = QuickSwitches.For(which);
        made.Start(Post);
        return made;
    }

    /// <summary>menus: energy saver, for the battery face's menu, as the battery line's (BatteryIsland.Saver).</summary>
    internal IQuickSwitch Saver => faceSaver ??= Started(QuickSwitch.EnergySaver);

    /// <summary>
    /// menus: the power modes, for the battery face's menu: the quick sources' (a test's, the samples
    /// or Windows'), made and started the first time a menu asks.
    /// </summary>
    internal IBatterySource PowerModes
    {
        get
        {
            if (powerModes is null)
            {
                powerModes = QuickPageTile.SourcesNow.Battery();
                powerModes.Start(new Changes(_ => Post()));
            }
            return powerModes;
        }
    }

    /// <summary>menus: whether the sound face's output is muted; null where there is no output device or it wears no sound face.</summary>
    internal bool? OutputMuted => Output() is { } output ? output.Muted : null;

    /// <summary>menus: the sound face's output muted, or not, as a middle click on it does.</summary>
    internal void ToggleMute() => _ = MiddleClick(0);

    /// <summary>menus: what the face's menu made, let go with the folder.</summary>
    private void DropMenuSources()
    {
        wifiRadio?.Dispose();
        wifiRadio = null;
        bluetoothRadio?.Dispose();
        bluetoothRadio = null;
        faceSaver?.Dispose();
        faceSaver = null;
        powerModes?.Dispose();
        powerModes = null;
    }
}
