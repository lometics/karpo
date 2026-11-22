// A line's page: what Windows' ">" on a quick settings tile opens (2026-10-14, the person: "can you
// functionality do the same stuff as you are able to do with the original win11 design? i dont think
// so. (switching wifi etc). fix that"). Windows' Wi-Fi tile switches the radio and its ">" swaps the
// panel for the networks, with a back arrow over them; Bluetooth's for the devices, the volume's for
// the output devices and the mixer, accessibility's for its switches. A folder opens no popup
// (docs\widgets.md §15), and this is none: the folder's column becomes the page where it stands, as
// Windows' panel does, and goes back to its lines on the back arrow, on Escape, when the folder is
// arranged and when it closes. Nothing of a page is in the config: its lines are made when it opens,
// a list each line of which is a line any folder may hold, and are gone when it closes.
//
// Which lines have a page, and what each page holds, is this table: the switch's own list (the wifi
// line under the Wi-Fi switch) and the footer Windows' page ends with. A page whose list draws no
// title of its own starts with a heading, its title beside the back arrow (PageHead).

namespace Karpo.Core;

/// <summary>A line's page: its title where its first line has none, and its lines.</summary>
/// <param name="Head">The title over a page whose lines say none of their own; null where the first line is the page's title (a quick settings page: Wi-Fi, Bluetooth).</param>
/// <param name="Lines">What it holds, top to bottom.</param>
public sealed record FolderPage(string? Head, IReadOnlyList<PresetItem> Lines);

/// <summary>Every line that opens a page, and the page it opens.</summary>
public static class FolderPages
{
    /// <summary>
    /// The rows the Wi-Fi networks and the Bluetooth devices are, fixed (2026-10-16, the person: "the
    /// wifi and bluetooth flyouts that pop up should have a fixed height, same for accessibility"):
    /// the page does not grow and shrink as networks come into range and devices connect — its rows
    /// scroll under its title, as Windows' page does. Eight rows hold the title and five networks or
    /// devices before they scroll, and with its foot the page is within a row of the accessibility
    /// page, whose six switches are the same six whatever happens. In cells, two a row (§442).
    /// </summary>
    public const int PageCells = 16;

    /// <summary>The page <paramref name="kind"/>'s line opens, or null for a line that has none.</summary>
    public static FolderPage? Of(ListKind kind) => kind switch
    {
        // Windows' Wi-Fi page: the networks, "More Wi-Fi settings".
        ListKind.WifiSwitch => new(null, [new() { Kind = "wifi", Height = LineHeight.Exactly(PageCells) }, PresetItem.Of(PremadeWidgets.WifiActions)]),
        // Bluetooth's: the paired devices, Bluetooth's settings and Add device.
        ListKind.BluetoothSwitch => new(null, [new() { Kind = "bluetooth", Height = LineHeight.Exactly(PageCells) }, PresetItem.Of(PremadeWidgets.BluetoothActions)]),
        // The volume's: the output and the input device, each a dropdown (2026-10-16, the person: "the
        // sound submenu of the quick settings should show the input and output device selectable from a
        // dropdown"; SoundPartTile), spatial sound, and the volume mixer Windows opens from the page's
        // foot, under them here, with the sound's settings and the mixer's. The mixer is every app
        // (2026-10-16, the person: "the quick settings volume bar flyout should show the volume mixer
        // the full height, it should grow with its contents up to a generous max height"): as tall as
        // its apps, and where the page would be past 80 % of the work area or the folder's room it
        // gives way and scrolls them, the title, the devices and the foot staying (§200).
        ListKind.SoundVolume => new(S.CorePageSound,
        [
            new() { Kind = WidgetInfos.SoundOutput.Id, Height = LineHeight.Automatic(4) },
            new() { Kind = WidgetInfos.SoundInput.Id, Height = LineHeight.Automatic(4) },
            new() { Kind = "spatialSound" },
            PresetItem.Divider,
            new() { Kind = WidgetInfos.SoundMixer.Id, Height = LineHeight.Automatic(LineHeight.Most) with { GivesWay = true } },
            PresetItem.Of(PremadeWidgets.SoundActions),
        ]),
        // Accessibility's: its switches in Windows' order, and its settings.
        ListKind.Accessibility => new(WidgetInfos.Accessibility.Title,
        [
            new() { Kind = "magnifier" },
            new() { Kind = "colorFilters" },
            new() { Kind = "narrator" },
            new() { Kind = "monoAudio" },
            new() { Kind = "liveCaptions" },
            new() { Kind = "stickyKeys" },
            PresetItem.Of(PremadeWidgets.AccessibilityActions),
        ]),
        // The VPN's: its connections, each connected or disconnected from its row.
        ListKind.Vpn => new(null, [new() { Kind = "vpnConnections", Height = LineHeight.Automatic(10) }, PresetItem.Of(PremadeWidgets.VpnActions)]),
        // Nearby sharing's: off, my devices only, everyone nearby.
        ListKind.NearbyShare => new(null, [new() { Kind = "nearbySharing" }, PresetItem.Of(PremadeWidgets.NearbyActions)]),
        // The mobile hotspot's: its network, its password, the devices on it.
        ListKind.MobileHotspot => new(null, [new() { Kind = "hotspot", Height = LineHeight.Automatic(16) }, PresetItem.Of(PremadeWidgets.HotspotActions)]),
        // Cast's: the wireless displays in range, as the Wi-Fi's networks a fixed height that scrolls,
        // Display and Add device under them, as Windows' Cast page ends.
        ListKind.Cast => new(null, [new() { Kind = "wirelessDisplays", Height = LineHeight.Exactly(PageCells) }, PresetItem.Of(PremadeWidgets.CastActions)]),
        // Project's: Win+P's four modes, the one in force marked, and the display's settings.
        ListKind.Project => new(null, [new() { Kind = "projection" }, PresetItem.Of(PremadeWidgets.ProjectActions)]),
        _ => null,
    };

    /// <summary>Whether <paramref name="kind"/>'s line opens a page.</summary>
    public static bool Has(ListKind kind) => Of(kind) is not null;

    /// <summary>
    /// The page's lines, made: a heading first where the page has a title of its own, then each line
    /// set up as its item says, with ids of the page's own ("page-0", …) so the same page is the same
    /// lines however often it opens. Nothing of it is written anywhere.
    /// </summary>
    public static List<FolderCell> Cells(FolderPage page)
    {
        var cells = new List<FolderCell>();
        if (page.Head is { } head)
        {
            var heading = ListKind.Heading.New();
            heading.Heading = new HeadingOptions { Text = head };
            cells.Add(new FolderCell { Island = heading });
        }
        foreach (var item in page.Lines)
        {
            var line = ListWidgets.Find(item.Form)?.Kind.New()
                ?? throw new InvalidOperationException($"a page holds a \"{item.Form}\", which a list folder has no line of.");
            item.Premade?.Apply(line);
            if (item.Height is { } height)
            {
                line.Height = height;
            }
            cells.Add(new FolderCell { Island = line });
        }
        for (var index = 0; index < cells.Count; index++)
        {
            cells[index].Island.Id = $"page-{index}";
        }
        return cells;
    }
}
