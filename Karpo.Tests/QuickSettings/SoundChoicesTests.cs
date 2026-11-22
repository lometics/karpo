// The sound page's Output and Input dropdowns (2026-10-16, the person: "the sound submenu of the quick
// settings should show the input and output device selectable from a dropdown"; SoundPartTile,
// Dock.FolderChoices). An output or input line on a page is a field with the device in use and a
// chevron; a click, Enter or Space opens the devices as a menu under it, the one in use ticked; a pick
// makes it Windows' default through the sound source, here a made-up one that records what it is
// asked; a PC with no microphone says so in a greyed field that opens nothing. Off a page the same
// lines are the lists they were.
//
// Pictured: sound-choices-<dark|light>-<100|150>.png — the quick settings folder on its sound page in
// a real window on a hidden desktop, the Output dropdown clicked and its list open under it.

using System.Numerics;
using Karpo.Dock;
using Karpo.Dock.QuickSettings;
using Karpo.Dock.QuickSettings.Controls;
using Karpo.Dock.QuickSettings.Services;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using static Karpo.Tests.Folders.ListFormPictures;
using static TerraFX.Interop.Windows.Windows;
using Edge = Karpo.Core.Edge;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.QuickSettings;

public sealed unsafe class SoundChoicesTests
{
    private static readonly PresetMachine Laptop = new(HasBattery: true, FixedDrives: []);

    /// <summary>The sound page holds the two dropdowns at its top, the rest as it was.</summary>
    [Fact]
    public void TheSoundPageStartsWithTheOutputAndTheInput() =>
        Assert.Equal(
            ["heading", "soundOutput", "soundInput", "spatialSound", "divider", "soundMixer", "actions"],
            FolderPages.Cells(FolderPages.Of(ListKind.SoundVolume)!).Select(cell => cell.Island.Kind));

    /// <summary>
    /// The devices become the menu's rows in the order given, by name, the one in use ticked and the
    /// rest unticked, each a choice of its own device.
    /// </summary>
    [Fact]
    public void TheDevicesAreTheMenusRowsTheOneInUseTicked()
    {
        var (items, rows) = TheDock.DeviceChoices([("a", "Speakers", false), ("b", "Headphones", true), ("c", "Display", false)]);
        Assert.Equal(["Speakers", "Headphones", "Display"], items.Select(item => item.Text));
        Assert.Equal([false, true, false], items.Select(item => item.Checked == true));
        Assert.All(items, item => Assert.NotNull(item.Checked));
        Assert.All(rows, row => Assert.Equal(WidgetAct.ChooseDevice, row.Act));
        Assert.Equal(["a", "b", "c"], rows.Select(row => row.On));
        Assert.Equal(items.Select(item => item.Id), rows.Select(row => row.Id));
        var (none, noRows) = TheDock.DeviceChoices([]);
        Assert.Empty(none);
        Assert.Empty(noRows);
    }

    /// <summary>The list hangs off the field's middle by half the field and 4 DIP, as wide as the field, centred on it.</summary>
    [Fact]
    public void TheListIsTheFieldsWidthAndClearsIt()
    {
        var field = new RECT { left = 100, top = 200, right = 580, bottom = 248 };
        var (anchor, gap, width) = TheDock.ChoicesPlacement(field, 1.5f);
        Assert.Equal((340, 224), (anchor.x, anchor.y));
        Assert.Equal(480, width);
        // 24 px is 16 DIP at 150 %: half the field, and 4 of air.
        Assert.Equal(20, gap, 3);
    }

    /// <summary>
    /// On a page, an output and an input line are dropdowns: a field with the device in use, a click
    /// on it or Enter or Space on the line asks for the choices, and a pick sets the default through
    /// the source — the output's for the output, the input's for the input. A device changing in
    /// Windows shows in the field at once.
    /// </summary>
    [UiFact]
    public void AFieldShowsTheDeviceInUseAndAPickSetsTheDefault()
    {
        Offscreen.OnStaThread(gfx =>
        {
            foreach (var kind in new[] { ListKind.SoundOutput, ListKind.SoundInput })
            {
                var machine = new Machine
                {
                    Outputs = [Device("speakers", "Speakers (Realtek(R) Audio)"), Device("headphones", "Headphones", inUse: true)],
                    Inputs = [Device("array", "Microphone Array", inUse: true), Device("headset", "Headset")],
                };
                using var tile = Tile(gfx, kind, machine, onPage: true);
                Assert.True(tile.Picker);
                var style = Style();
                var box = PopupPaint.Rect(0, 0, 360, tile.ListHeight(360, style));
                tile.LayOut(box, style);
                var field = Assert.Single(tile.Placed.OfType<ComboField>());
                Assert.True(field.Enabled);
                Assert.Equal(kind == ListKind.SoundOutput ? "Headphones" : "Microphone Array", field.Text);
                Assert.Contains(tile.Placed.OfType<Label>(), label => label.Text == SoundPartTile.PickerHeadOf(tile.Region));
                Assert.Equal(field.Bounds, tile.ChoicesField);
                // Windows' order, whichever is in use.
                Assert.Equal(kind == ListKind.SoundOutput ? ["speakers", "headphones"] : ["array", "headset"], tile.Choices.Select(choice => choice.Id));
                Assert.Equal(0, tile.ScrollHeight(360, box.bottom, style));

                var target = tile.CardTarget(Centre(field), box, style, WidgetSurface.List);
                Assert.True(target > 0, "the field is no target");
                Assert.Equal(IslandView.Choices, tile.Click(target).View);
                // The keys: Enter or Space on the line.
                Assert.Equal(IslandView.Choices, tile.Click(0).View);
                // The header is no target.
                Assert.Equal(0, tile.CardTarget(new Vector2(20, 8), box, style, WidgetSurface.List));

                // The pick: the other device, Windows' default through the source.
                var (_, rows) = TheDock.DeviceChoices(tile.Choices);
                var other = rows.Single(row => row.On is "speakers" or "headset");
                TheDock.RunAct(other, tile, (_, _) => { });
                if (kind == ListKind.SoundOutput)
                {
                    Assert.Equal("speakers", machine.Chosen);
                    Assert.Null(machine.ChosenInput);
                }
                else
                {
                    Assert.Equal("headset", machine.ChosenInput);
                    Assert.Null(machine.Chosen);
                }

                // The device in use changing in Windows: the field says so on its next look.
                machine.Outputs = [Device("speakers", "Speakers (Realtek(R) Audio)", inUse: true), Device("headphones", "Headphones")];
                machine.Inputs = [Device("array", "Microphone Array"), Device("headset", "Headset", inUse: true)];
                machine.Tell();
                tile.LayOut(box, style);
                Assert.Equal(kind == ListKind.SoundOutput ? "Speakers (Realtek(R) Audio)" : "Headset", Assert.Single(tile.Placed.OfType<ComboField>()).Text);
            }
        });
    }

    /// <summary>A PC with no microphone: the Input field says so, greyed, and opens nothing, by the pointer or the keys.</summary>
    [UiFact]
    public void NoInputDeviceIsAGreyedFieldThatOpensNothing()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var machine = new Machine { Outputs = [Device("speakers", "Speakers", inUse: true)] };
            using var tile = Tile(gfx, ListKind.SoundInput, machine, onPage: true);
            var style = Style();
            var box = PopupPaint.Rect(0, 0, 360, tile.ListHeight(360, style));
            tile.LayOut(box, style);
            var field = Assert.Single(tile.Placed.OfType<ComboField>());
            Assert.False(field.Enabled);
            Assert.Equal("No input devices", field.Text);
            Assert.Empty(tile.Choices);
            Assert.Equal(0, tile.CardTarget(Centre(field), box, style, WidgetSurface.List));
            Assert.Equal(IslandView.None, tile.Click(0).View);
            Assert.Null(machine.ChosenInput);
        });
    }

    /// <summary>Off a page the output and input lines are the lists they were: rows, no field.</summary>
    [UiFact]
    public void OffAPageTheLinesStayLists()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var machine = new Machine { Outputs = [Device("speakers", "Speakers", inUse: true), Device("headphones", "Headphones")] };
            using var tile = Tile(gfx, ListKind.SoundOutput, machine, onPage: false);
            Assert.False(tile.Picker);
            tile.LayOut(PopupPaint.Rect(0, 0, 360, 200), Style());
            Assert.Empty(tile.Placed.OfType<ComboField>());
            Assert.Equal(2, tile.Placed.OfType<ListRow>().Count());
            Assert.Null(tile.ChoicesField);
            Assert.Empty(tile.Choices);
        });
    }

    /// <summary>
    /// In a real folder window: the volume's ">" opens the sound page, a click on the Output field asks
    /// for its choices under the field where it stands on the screen, and the folder stays open; the
    /// list the dock opens there is pictured with it, sound-choices-&lt;dark|light&gt;-&lt;100|150&gt;.png.
    /// </summary>
    [UiFact]
    public void TheFieldOpensItsListUnderItInTheOpenFolder()
    {
        WindowShots.OnHiddenDesktop("sound-choices", (gfx, instance) =>
        {
            QuickSwitches.Source = which => new SampleSwitch(which.ToString(), on: which is QuickSwitch.WifiRadio or QuickSwitch.BluetoothRadio, switches: true);
            try
            {
                foreach (var scale in new[] { 1f, 1.5f })
                {
                    foreach (var dark in new[] { true, false })
                    {
                        var look = LookFor(dark);
                        var holder = Presets.Folder(Presets.Find(Presets.QuickPanelId)!, Laptop);
                        using var folder = new FolderIsland($"{Owner}/{holder.Id}-{scale}-{dark}", holder, gfx, new Config());
                        using var flyout = new FolderFlyout(gfx, instance, 0);
                        var work = new RECT { left = 0, top = 0, right = (int)(1920 * scale), bottom = (int)(1040 * scale) };
                        flyout.Show(gfx, look, scale, new POINT { x = work.right - (int)(200 * scale), y = work.bottom }, work, Edge.Bottom, folder, FlyoutAlign.End);
                        WindowShots.Settle();
                        LPARAM At(int x, int y)
                        {
                            var point = new POINT { x = x, y = y };
                            Assert.True(ScreenToClient(flyout.Hwnd, &point));
                            return (LPARAM)(nint)((point.y << 16) | (point.x & 0xFFFF));
                        }
                        FolderFlyoutEvent Click(int x, int y)
                        {
                            var at = At(x, y);
                            _ = flyout.Message(WM.WM_MOUSEMOVE, 0, at);
                            _ = flyout.Message(WM.WM_LBUTTONDOWN, 1, at);
                            return flyout.Message(WM.WM_LBUTTONUP, 0, at);
                        }
                        var volume = flyout.LineOnScreen(folder.Tiles.Select(tile => tile.Cell.Island.Kind).ToList().IndexOf("soundVolume"));
                        // The ">" on the trailing gutter: the volume draws no switch to stand before.
                        var lineStyle = FolderFlyout.StyleFor(scale, look, FlyoutTheme.For(look));
                        var sign = SwitchIsland.ChevronGlyphBox(PopupPaint.Rect(0, 0, volume.right - volume.left, volume.bottom - volume.top), lineStyle, switched: false);
                        _ = Click(volume.left + (int)((sign.left + sign.right) / 2), (volume.top + volume.bottom) / 2);
                        Assert.True(folder.Paged, "the volume's > opened no page");
                        WindowShots.Settle();

                        var output = Assert.IsType<SoundPartTile>(folder.Tiles[1].Island);
                        Assert.True(output.Picker);
                        Assert.IsType<SoundPartTile>(folder.Tiles[2].Island);
                        var field = Assert.NotNull(output.ChoicesField);
                        var line = flyout.LineOnScreen(1);
                        var expected = new RECT
                        {
                            left = line.left + (int)MathF.Round(field.left),
                            top = line.top + (int)MathF.Round(field.top),
                            right = line.left + (int)MathF.Round(field.right),
                            bottom = line.top + (int)MathF.Round(field.bottom),
                        };
                        var happened = Click((expected.left + expected.right) / 2, (expected.top + expected.bottom) / 2);
                        Assert.Equal(FolderFlyoutAction.Choices, happened.Kind);
                        Assert.Same(output, happened.Widget);
                        Assert.InRange(Math.Abs(happened.Field.left - expected.left), 0, 1);
                        Assert.InRange(Math.Abs(happened.Field.top - expected.top), 0, 1);
                        Assert.InRange(Math.Abs(happened.Field.right - expected.right), 0, 1);
                        Assert.True(flyout.IsOpen);

                        // The list as the dock opens it there (Dock.FolderChoices).
                        var (rows, _) = TheDock.DeviceChoices(output.Choices);
                        var (anchor, gap, width) = TheDock.ChoicesPlacement(happened.Field, scale);
                        using var menu = new Flyout(gfx, instance, 0) { ListWidth = width, SpokenName = SoundPartTile.PickerHeadOf(output.Region) };
                        menu.Show(gfx, look, scale, rows, FlyoutLayout.List, anchor, work, Edge.Top, gapDip: gap);
                        WindowShots.Settle();
                        var (list, _) = menu.PanelsOnScreen;
                        Assert.InRange(Math.Abs((list.right - list.left) - width), 0, 1);
                        // Clear of the field: under it where there is room, else over it.
                        Assert.True(list.top >= happened.Field.bottom || list.bottom <= happened.Field.top, "the list stands over its field");
                        var file = $"sound-choices-{(dark ? "dark" : "light")}-{scale * 100:0}.png";
                        Assert.True(
                            WindowShots.SaveTogether([(flyout.Hwnd, flyout.PanelOnScreen), (menu.Hwnd, list)], file, dark ? ((byte)32, (byte)32, (byte)32) : ((byte)230, (byte)230, (byte)230)),
                            $"{file} came back blank");
                        menu.Hide();
                        flyout.MenuClosed();
                        folder.Closed();
                    }
                }
            }
            finally
            {
                QuickSwitches.Source = null;
            }
        });
    }

    // --- The made-up machine -----------------------------------------------------------------

    private sealed class Machine : ISoundSource
    {
        public List<AudioOutput> Outputs { get; set; } = [];
        public List<AudioOutput> Inputs { get; set; } = [];
        public string? Chosen { get; private set; }
        public string? ChosenInput { get; private set; }
        private Changes? news;

        public void Start(Changes changes) => news = changes;

        public void Tell() => news?.Add(SoundNews.Devices);

        public SoundState Read(int changes) => new(true, 0.5f, false, [.. Outputs], [], [.. Inputs], false);

        public void SetVolume(float value)
        {
        }

        public void SetMuted(bool muted)
        {
        }

        public void SetDefault(string id) => Chosen = id;

        public void SetDefaultInput(string id) => ChosenInput = id;

        public void SetInputMuted(bool muted)
        {
        }

        public void SetAppVolume(string key, float value)
        {
        }

        public void SetAppMuted(string key, bool muted)
        {
        }

        public void Dispose()
        {
        }
    }

    private static AudioOutput Device(string id, string name, bool inUse = false) => new(id, name, char.ConvertFromUtf32(0xE7F5), inUse);

    private static SoundPartTile Tile(Gfx gfx, ListKind kind, Machine machine, bool onPage)
    {
        var widget = kind.New();
        SoundParts.Made = () => machine;
        try
        {
            var key = onPage ? $"test/quick/page/{widget.Id}" : Config.IslandKey(null, widget.Id);
            return new SoundPartTile(key, widget, gfx, new Config());
        }
        finally
        {
            SoundParts.Made = null;
        }
    }

    private static TileStyle Style(bool dark = true) => FolderFlyout.StyleFor(1f, LookFor(dark), FlyoutTheme.For(LookFor(dark)));

    private static Vector2 Centre(Widget control) =>
        new((control.Bounds.left + control.Bounds.right) / 2, (control.Bounds.top + control.Bounds.bottom) / 2);
}
