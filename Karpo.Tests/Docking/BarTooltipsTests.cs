// Which parts of a bar show a hover tooltip (Karpo.Dock\BarTooltips.cs, which explains why): none
// over a widget or a folder (§367; the person, 2026-10-26: "remove the tooltips when hovering over a
// folder face or widget face") but over Windows' status icons (§382: the network, the sound,
// Bluetooth, the battery, the bell, each over its own icon), and the ones Windows' taskbar has — an
// app's button, Start, search, task view, show desktop, Windows' other buttons, a tray icon — stay.

using System.Numerics;
using Karpo.Dock;
using Karpo.Tests.Folders;
using Karpo.Tests.Islands;

namespace Karpo.Tests.Docking;

public sealed class BarTooltipsTests
{
    /// <summary>
    /// The bar's one answer (Dock.TooltipFor): over a widget or a folder only what the status icon
    /// under the pointer says (<c>status</c>, FolderIsland.StatusTipAt), and nothing where there is
    /// none — the app's buttons, Windows' parts and the tray keep theirs.
    /// </summary>
    [Fact]
    public void NoWidgetOrFolderHasATooltipWhatIsNotAWidgetKeepsIts()
    {
        var asked = new List<string>();
        string? Tile(int index)
        {
            asked.Add($"tile {index}");
            return $"App {index}";
        }
        string? Tray(Hit hit)
        {
            asked.Add($"tray {hit.Index}");
            return "Wi-Fi app: connected";
        }
        string? Status(Hit hit)
        {
            asked.Add($"status {hit.Part}");
            return hit.Part == 3 ? "Home\nInternet access" : null;
        }

        // A widget's cell, a folder's, a cell past the first of a widget of many: none of their own.
        foreach (var cell in new[] { Hit.Cell(0, 0), Hit.Cell(5, 2) })
        {
            Assert.Null(BarTooltips.For(cell, Tile, Tray, Status));
        }
        // A status icon's: what it says, asked of the cell under the pointer and nothing else.
        Assert.Equal("Home\nInternet access", BarTooltips.For(Hit.Cell(3, 0), Tile, Tray, Status));
        Assert.Equal(["status 0", "status 5", "status 3"], asked);
        asked.Clear();

        Assert.Equal("App 1", BarTooltips.For(Hit.Tile(1), Tile, Tray, Status));
        Assert.Equal("Wi-Fi app: connected", BarTooltips.For(Hit.Tray(0), Tile, Tray, Status));
        Assert.Equal("Start", BarTooltips.For(Hit.Start, Tile, Tray, Status));
        Assert.Equal("Search", BarTooltips.For(Hit.Search, Tile, Tray, Status));
        Assert.Equal("Task View", BarTooltips.For(Hit.TaskView, Tile, Tray, Status));
        Assert.Equal("Show desktop", BarTooltips.For(Hit.ShowDesktop, Tile, Tray, Status));
        foreach (var kind in new[] { BarKind.WidgetsBoard, BarKind.WindowsSettings, BarKind.PenMenu, BarKind.TouchKeyboard, BarKind.VirtualTouchpad })
        {
            Assert.Equal(kind.Title(), BarTooltips.For(Hit.WindowsButton(kind), Tile, Tray, Status));
        }
        Assert.Equal(["tile 1", "tray 0"], asked);

        // None where the bar has nothing to name, or a click opens one of Karpo's lists.
        foreach (var none in new Hit?[] { null, Hit.AppOverflow, Hit.TrayOverflow, Hit.Panel, Hit.ResizeGrip, Hit.Add(0) })
        {
            Assert.Null(BarTooltips.For(none, Tile, Tray, Status));
        }
    }

    /// <summary>
    /// The user, 2026-10-29: the status icons get their tooltips back — the network, the sound, the
    /// battery, Bluetooth and the bell — as Windows' taskbar shows them; every other face keeps none
    /// (§367): the clock, the date, the weather, the mail, an icon and the rest.
    /// </summary>
    [Fact]
    public void OnlyTheStatusIconsAreStatusIcons()
    {
        string[] status = [FolderFaces.WifiId, FolderFaces.SoundId, FolderFaces.BluetoothId, FolderFaces.BatteryId, FolderFaces.NotificationsId];
        Assert.Equal(status.Order(), FolderFaces.All.Where(BarTooltips.IsStatusIcon).Select(face => face.Id).Order());
    }

    /// <summary>
    /// What each says, in Windows 11's words: the network's name and whether it reaches the
    /// internet (and a VPN), the output and its level or Muted, the battery's status, Bluetooth on or
    /// off, and how many notifications are new.
    /// </summary>
    [Fact]
    public void AStatusIconSaysWhatWindowsSays()
    {
        var wifi = SystemStatus.Compose(new SystemStatus.Connection(SystemStatus.Medium.WiFi, true, "Home", 3), new SystemStatus.Audio(true, "Speakers", 45, false), bluetooth: 1);
        Assert.Equal("Home\nInternet access", StatusFaceDrawing.Tip(FolderFaces.WifiId, wifi));
        Assert.Equal("Speakers: 45%", StatusFaceDrawing.Tip(FolderFaces.SoundId, wifi));
        Assert.Equal("Bluetooth on", StatusFaceDrawing.Tip(FolderFaces.BluetoothId, wifi));

        var offline = SystemStatus.Compose(new SystemStatus.Connection(SystemStatus.Medium.None, false, null, 0), new SystemStatus.Audio(true, "Headphones", 30, true), bluetooth: 0);
        Assert.Equal("Not connected", StatusFaceDrawing.Tip(FolderFaces.WifiId, offline));
        Assert.Equal("Headphones: Muted", StatusFaceDrawing.Tip(FolderFaces.SoundId, offline));
        Assert.Equal("Bluetooth off", StatusFaceDrawing.Tip(FolderFaces.BluetoothId, offline));

        Assert.Equal("Battery status: 87% remaining", SystemStatus.Describe(new SystemStatus.Battery(87, false, false)).Tip);
        Assert.Equal("Battery status: 87% available (plugged in)", SystemStatus.Describe(new SystemStatus.Battery(87, true, false)).Tip);
        Assert.Equal("Battery status: Fully charged (100%)", SystemStatus.Describe(new SystemStatus.Battery(100, true, false)).Tip);
    }

    /// <summary>
    /// On the bar: a folder wearing the network, the sound, Bluetooth and the battery beside the clock
    /// has a tooltip over each status icon, its own words, standing over that icon's room; none over
    /// the clock, nor past the faces. Across a bar and down one.
    /// </summary>
    [UiFact]
    public void EachStatusIconHasItsOwnTooltipAndTheClockNone()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var probe = BatteryIsland.Probe;
            BatteryIsland.Probe = () => new SystemStatus.Battery(87, false, false);
            FolderIsland folder;
            try
            {
                folder = FaceSlackTests.Wearing(gfx, FolderFaces.WifiId, FolderFaces.SoundId, FolderFaces.BluetoothId, FolderFaces.BatteryId, FolderFaces.ClockId);
            }
            finally
            {
                BatteryIsland.Probe = probe;
            }
            using var _ = folder;
            string?[] said = ["Home\nInternet access\nConnected to VPN: Work", "Speakers: 40%", "Bluetooth on", "Battery status: 87% remaining", null];
            foreach (var vertical in new[] { false, true })
            {
                var style = FaceCentringTests.Style(48, 144, vertical);
                var (width, height) = folder.CellSize(0, style);
                var size = new Vector2(width, height);
                var boxes = FolderIsland.FaceBoxes(gfx, folder.Options, style, width, height, folder.FacesOnBar, folder);
                Assert.Equal(said.Length, boxes.Length);
                for (var at = 0; at < boxes.Length; at++)
                {
                    var box = boxes[at];
                    var middle = new Vector2((box.left + box.right) / 2, (box.top + box.bottom) / 2);
                    var tip = folder.StatusTipAt(middle, size, style);
                    Assert.Equal(said[at], tip?.Text);
                    if (tip is { } shown)
                    {
                        // It stands over the icon's own room, not the button's middle.
                        Assert.Equal((box.left + box.right) / 2, (shown.Box.left + shown.Box.right) / 2, 0.5f);
                        Assert.Equal((box.top + box.bottom) / 2, (shown.Box.top + shown.Box.bottom) / 2, 0.5f);
                    }
                }
                // Past the faces, at the button's very start and end: the folder's, and no tooltip.
                Assert.Null(folder.StatusTipAt(Vector2.Zero, size, style));
                Assert.Null(folder.StatusTipAt(size - Vector2.One, size, style));
            }
        });
    }

    /// <summary>
    /// The parts the bar draws itself whose clicks open Karpo's lists have none: the apps' "…", which
    /// lists the apps that did not fit, and the tray's chevron, which opens the hidden icons.
    /// </summary>
    [Fact]
    public void WhatOpensAListOfKarposHasNoTooltip()
    {
        Assert.Null(BarTooltips.ForPart(HitKind.AppOverflow));
        Assert.Null(BarTooltips.ForPart(HitKind.TrayOverflow));
    }

    /// <summary>Windows' own Start, search, task view and show desktop are named, as on the taskbar.</summary>
    [Fact]
    public void WindowsOwnPartsAreNamed()
    {
        Assert.Equal("Start", BarTooltips.ForPart(HitKind.Start));
        Assert.Equal("Search", BarTooltips.ForPart(HitKind.Search));
        Assert.Equal("Task View", BarTooltips.ForPart(HitKind.TaskView));
        Assert.Equal("Show desktop", BarTooltips.ForPart(HitKind.ShowDesktop));
    }

    /// <summary>
    /// The Snipping Tool's button while the app is not installed: its click opens Karpo's menu
    /// saying so, so it has no tooltip. Installed, it has its title, as Windows' other buttons do.
    /// </summary>
    [Fact]
    public void AMissingSnippingToolHasNoTooltip()
    {
        Assert.Null(BarTooltips.ForWindowsButton(BarKind.SnippingTool, WindowsButtons.Missing(BarKind.SnippingTool, _ => false)));
        Assert.Equal(BarKind.SnippingTool.Title(), BarTooltips.ForWindowsButton(BarKind.SnippingTool, WindowsButtons.Missing(BarKind.SnippingTool, _ => true)));
        Assert.Equal(BarKind.SnippingTool.Title(), BarTooltips.ForWindowsButton(BarKind.SnippingTool, null));
    }

    /// <summary>
    /// An app's button: its window's title, else its name; and none for a pin this PC can't open,
    /// whose click opens Karpo's menu saying why.
    /// </summary>
    [Fact]
    public void ABrokenPinHasNoTooltip()
    {
        Assert.Null(BarTooltips.ForTile("Notepad", null, "The file this pin opens isn't on this PC"));
        Assert.Equal("Notepad", BarTooltips.ForTile("Notepad", null, null));
        Assert.Equal("notes.txt - Notepad", BarTooltips.ForTile("Notepad", "notes.txt - Notepad", null));
    }
}
