// The quick-settings flyouts shown for real on a hidden desktop, with made-up
// networks, devices and levels (nothing reaches this PC's Wi-Fi, Bluetooth,
// audio or power settings): pictures of every page in dark and light (the same
// painting the flyout's surfaces get), and runs that click, type, scroll and
// press keys the way a person would, checking what the samples were asked to do.
//
// The keys a test holds are the ones it says: none, on every thread here
// (KeyState.Held), so a key held on the person's own keyboard while the suite
// runs is never one of the panel's.
//
// Like the other offscreen tests these need a window station and Direct2D, so
// they only run with KARPO_UI_TESTS=1; the pictures go to KARPO_PNG_DIR.

using System.Numerics;
using System.Runtime.InteropServices;
using Karpo.Dock;
using Karpo.Dock.Native;
using Karpo.Dock.QuickSettings;
using Karpo.Dock.QuickSettings.Controls;
using Karpo.Dock.QuickSettings.Services;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.DirectX.DirectX;
using static TerraFX.Interop.Windows.Windows;
using Color = Windows.UI.Color;
using IDispatcherQueueController = TerraFX.Interop.WinRT.IDispatcherQueueController;

namespace Karpo.Tests.QuickSettings;

public sealed unsafe partial class QuickFlyoutOffscreenTests
{
    // A class of the tests' own, so another test's window procedure can't have the flyout's messages.
    private const string ClassName = "karpo.quick.test";

    private static readonly RECT Work = new() { left = 0, top = 0, right = 1920, bottom = 1040 };
    private static readonly POINT Anchor = new() { x = 1500, y = 1040 };

    private static string Pictures
    {
        get
        {
            var folder = Environment.GetEnvironmentVariable("KARPO_PNG_DIR") is { Length: > 0 } named
                ? named
                : Path.Combine(Path.GetTempPath(), "karpo-tests", "pictures");
            Directory.CreateDirectory(folder);
            return folder;
        }
    }

    private static Look LookFor(bool dark) => new(
        Glass: false,
        Dark: dark,
        Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4),
        AccentLight2: Color.FromArgb(255, 0x99, 0xEB, 0xFF),
        AccentDark1: Color.FromArgb(255, 0x00, 0x5F, 0xB8));

    private static Look HighContrast() => new(
        Glass: false,
        Dark: true,
        Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4),
        HighContrast: true,
        HcWindow: Color.FromArgb(255, 0, 0, 0),
        HcText: Color.FromArgb(255, 255, 255, 255),
        HcHighlight: Color.FromArgb(255, 0x1A, 0xEB, 0xFF),
        HcHighlightText: Color.FromArgb(255, 0, 0, 0));

    /// <summary>The samples of one test, kept so the test can see what they were asked to do.</summary>
    private sealed class Samples
    {
        public SampleWifi Wifi { get; set; } = new();

        /// <summary>The cables: none unless a test hands some, so the Wi-Fi tests see Wi-Fi alone.</summary>
        public SampleWired Wired { get; set; } = new();
        public SampleBluetooth Bluetooth { get; set; } = new();
        public SampleBattery Battery { get; set; } = new();
        public SampleDesktopNames Desktops { get; } = new();

        /// <summary>What the made-up PC offers the power page.</summary>
        public QuickLinkMachine Machine { get; set; } = new(Battery: true, WindowsTerminal: true, Sleep: true, Hibernate: true);

        public QuickSources Sources => new()
        {
            Wifi = () => Wifi,
            Wired = () => Wired,
            Bluetooth = () => Bluetooth,
            Battery = () => Battery,
            Machine = () => Machine,
            Desktops = () => Desktops,
        };
    }

    /// <summary>
    /// A Wi-Fi and Bluetooth state of our own, so a picture of the bar shows the same thing on
    /// every machine. Without it the sheet followed whatever network this PC happened to be on.
    /// </summary>
    private static readonly SystemState Radios = new(
        char.ConvertFromUtf32(0xE873),
        char.ConvertFromUtf32(0xE994),
        NetworkBackGlyph: char.ConvertFromUtf32(0xE701),
        BluetoothGlyph: char.ConvertFromUtf32(0xE702),
        BluetoothOn: true);

    [UiFact]
    public void PagePictures()
    {
        OnHiddenDesktop((gfx, instance) =>
        {
            var samples = new Samples();
            using var flyout = new QuickFlyout(gfx, instance, 0, samples.Sources, ClassName);
            Target = message => flyout.Message(message.Msg, message.WParam, message.LParam);
            foreach (var dark in new[] { true, false })
            {
                var name = dark ? "dark" : "light";
                var look = LookFor(dark);
                foreach (var kind in new[] { QuickKind.RenameDesktop })
                {
                    Show(flyout, gfx, look, 1.5f, kind);
                    Save(flyout, look, 1.5f, $"quick-{kind.ToString().ToLowerInvariant()}-{name}.png");
                }
                flyout.Hide();
            }
            flyout.Hide();
            Target = null;
        });
    }

    /// <summary>
    /// §363: the rename dialog keeps its panel — its layers, and the surfaces of what stays — through its showings,
    /// shown again at its top with nothing lit; another look, a panel of its own.
    /// </summary>
    [UiFact]
    public void TheRenameDialogKeepsItsPanelThroughItsShowings()
    {
        OnHiddenDesktop((gfx, instance) =>
        {
            var samples = new Samples();
            using var flyout = new QuickFlyout(gfx, instance, 0, samples.Sources, ClassName);
            Target = message => flyout.Message(message.Msg, message.WParam, message.LParam);
            Show(flyout, gfx, LookFor(true), 1f, QuickKind.RenameDesktop);
            var panel = flyout.Panel;
            Assert.NotNull(panel);
            flyout.Hide();
            Show(flyout, gfx, LookFor(true), 1f, QuickKind.RenameDesktop);
            Assert.Same(panel, flyout.Panel);
            Assert.Equal(0, flyout.Panel!.Scroll);
            Assert.True(Assert.IsType<TextField>(flyout.Panel.Find("Desktop name")).Focused);
            Show(flyout, gfx, LookFor(false), 1f, QuickKind.RenameDesktop);
            Assert.NotSame(panel, flyout.Panel);
            flyout.Hide();
            Target = null;
            Assert.Empty(Failures);
        });
    }

    [UiFact]
    public void RenameTypesAndSaves()
    {
        OnHiddenDesktop((gfx, instance) =>
        {
            var samples = new Samples();
            using var flyout = new QuickFlyout(gfx, instance, 0, samples.Sources, ClassName);
            var outcome = default(QuickFlyoutEvent);
            Target = message =>
            {
                var happened = flyout.Message(message.Msg, message.WParam, message.LParam);
                if (happened.Action != QuickFlyoutAction.None)
                {
                    outcome = happened;
                }
            };
            Show(flyout, gfx, LookFor(true), 1f, QuickKind.RenameDesktop);
            var field = Assert.IsType<TextField>(flyout.Panel!.Find("Desktop name"));
            Assert.True(field.Focused);
            Assert.Equal("Work", field.Text);
            // The name opens selected: typing replaces it.
            Type(flyout, "Home office");
            Assert.Equal("Home office", field.Text);
            Key(flyout, VK.VK_BACK);
            Key(flyout, VK.VK_LEFT);
            Type(flyout, "X");
            Assert.Equal("Home offiXc", field.Text);
            Key(flyout, VK.VK_HOME);
            Key(flyout, VK.VK_DELETE);
            Assert.Equal("ome offiXc", field.Text);
            Key(flyout, VK.VK_END);
            Key(flyout, VK.VK_BACK);
            Key(flyout, VK.VK_BACK);
            Type(flyout, "ce");
            Assert.Equal("ome office", field.Text);
            // Nothing typed: it says so and stays.
            field.Text = "   ";
            Key(flyout, VK.VK_RETURN);
            Assert.True(flyout.IsOpen);
            Assert.NotNull(flyout.Panel!.Find("Save"));
            field.Text = "Home";
            Key(flyout, VK.VK_RETURN);
            Assert.False(flyout.IsOpen);
            Assert.Equal(QuickFlyoutAction.Done, outcome.Action);
            Assert.Equal("Home", samples.Desktops.Name);
            Target = null;
            Assert.Empty(Failures);
        });
    }

    /// <summary>
    /// The flake behind the test above: it once typed "Home offiX". Shift held on the person's keyboard
    /// reached the panel's keys, the test's Left became Shift+Left, and the "X" replaced the selected
    /// "c". Shift is held here as Windows reports it to this thread — its own key table, which no
    /// other thread sees — with this desktop standing for the one with the input, so the keyboard's
    /// answer is Shift down; the test holds no key, and the name comes out as it was typed.
    /// </summary>
    [UiFact]
    public void ShiftOnThePersonsKeyboardIsNotTheRenamesShift()
    {
        OnHiddenDesktop((gfx, instance) =>
        {
            var samples = new Samples();
            using var flyout = new QuickFlyout(gfx, instance, 0, samples.Sources, ClassName);
            Target = message => flyout.Message(message.Msg, message.WParam, message.LParam);
            Show(flyout, gfx, LookFor(true), 1f, QuickKind.RenameDesktop);
            var field = Assert.IsType<TextField>(flyout.Panel!.Find("Desktop name"));
            var held = KeyState.Held;
            // Only while the keys below are sent to the window, which pumps nothing: a desktop that
            // stands for the one with the input would have the flyout's mouse hook take the
            // person's clicks for its own, too.
            InputDesktop.Holds = () => true;
            ThisThreadsShift(down: true);
            try
            {
                KeyState.Held = null;
                Assert.True(KeyState.Down(VK.VK_SHIFT), "the keyboard's answer is not Shift down");
                KeyState.Held = held;
                Type(flyout, "Home office");
                Key(flyout, VK.VK_BACK);
                Key(flyout, VK.VK_LEFT);
                Type(flyout, "X");
                Assert.Equal("Home offiXc", field.Text);
            }
            finally
            {
                ThisThreadsShift(down: false);
                InputDesktop.Holds = InputDesktop.Here;
                KeyState.Held = held;
            }
            flyout.Hide();
            Target = null;
            Assert.Empty(Failures);
        });
    }

    /// <summary>Shift down, or up, in this thread's own key table (SetKeyboardState): what the keyboard reports to this thread and no other.</summary>
    private static void ThisThreadsShift(bool down)
    {
        var state = new byte[256];
        fixed (byte* keys = state)
        {
            Assert.True(GetKeyboardState(keys));
            keys[VK.VK_SHIFT] = keys[VK.VK_LSHIFT] = (byte)(down ? 0x80 : 0);
            Assert.True(SetKeyboardState(keys));
        }
    }

    /// <summary>
    /// A click on the flyout's empty space ends the editing, as a click on nothing in particular does
    /// in Windows 11 (backlog 35): the key box has the keyboard no more, so it shows no caret and no
    /// selection and what is typed goes nowhere; a click on the box gives it back. Before, only
    /// another control or leaving the page took it.
    /// </summary>
    [UiFact]
    public void AClickOnEmptySpaceEndsTheEditing()
    {
        OnHiddenDesktop((gfx, instance) =>
        {
            var samples = new Samples();
            using var flyout = new QuickFlyout(gfx, instance, 0, samples.Sources, ClassName);
            Target = message => flyout.Message(message.Msg, message.WParam, message.LParam);
            // The rename box opens with its name selected: a click away leaves nothing selected on show.
            Show(flyout, gfx, LookFor(true), 1f, QuickKind.RenameDesktop);
            var field = Assert.IsType<TextField>(flyout.Panel!.Find("Desktop name"));
            Assert.True(field.Focused && field.Editing.HasSelection);
            ClickAt(flyout, EmptySpot(flyout));
            Assert.False(field.Focused);
            Assert.Equal("Work", field.Text);
            flyout.Hide();
            Target = null;
            Assert.Empty(Failures);
        });
    }

    /// <summary>A point of the panel no widget answers, in the window's client px.</summary>
    private static POINT EmptySpot(QuickFlyout flyout)
    {
        var panel = flyout.Panel!;
        var size = flyout.PanelSize;
        for (var y = 4f; y < size.Y - 4; y += 4)
        {
            for (var x = 4f; x < size.X - 4; x += 4)
            {
                if (panel.WidgetAt(new System.Numerics.Vector2(x, y)) is null)
                {
                    return new POINT { x = (int)(flyout.PanelOrigin.X + x), y = (int)(flyout.PanelOrigin.Y + y) };
                }
            }
        }
        throw new Xunit.Sdk.XunitException("The panel has no empty space");
    }

    private static void ClickAt(QuickFlyout flyout, POINT at)
    {
        Send(flyout.Hwnd, WM.WM_MOUSEMOVE, 0, Point(at.x, at.y));
        Send(flyout.Hwnd, WM.WM_LBUTTONDOWN, 1, Point(at.x, at.y));
        Send(flyout.Hwnd, WM.WM_LBUTTONUP, 0, Point(at.x, at.y));
    }

    /// <summary>Every word the panel shows: labels, rows and the footer.</summary>
    private static List<string> Words(QuickPanel panel)
    {
        var content = panel.Content;
        var said = new List<string>();
        foreach (var widget in content.Header.Concat(content.Body).Concat(content.Footer))
        {
            switch (widget)
            {
                case ListRow row:
                    said.Add(row.Title);
                    if (row.Detail is { Length: > 0 } detail)
                    {
                        said.Add(detail);
                    }
                    break;
                case Label label:
                    said.Add(label.Text);
                    break;
                case Button button:
                    said.Add(button.Text);
                    break;
            }
        }
        return said;
    }

    /// <summary>
    /// A made-up app icon, at the size the source line asks the shell for: a rounded square in the
    /// app's colour (green unless given) with a white disc. Never this PC's own icons, which the
    /// shell may not hand over while the suite runs, and which differ from one PC to the next.
    /// </summary>
    internal static Pixels AppIcon(byte b = 0x54, byte g = 0xB9, byte r = 0x1D)
    {
        const int side = NowPlayingSource.IconPx;
        var data = new byte[side * side * 4];
        for (var y = 0; y < side; y++)
        {
            for (var x = 0; x < side; x++)
            {
                var at = (y * side + x) * 4;
                var inside = x >= 2 && x <= 29 && y >= 2 && y <= 29
                    && !((x < 5 || x > 26) && (y < 5 || y > 26));
                var disc = (x - 15.5) * (x - 15.5) + (y - 15.5) * (y - 15.5) <= 49;
                var (blue, green, red, alpha) = !inside ? ((byte)0, (byte)0, (byte)0, (byte)0) : disc ? ((byte)255, (byte)255, (byte)255, (byte)255) : (b, g, r, (byte)255);
                data[at] = blue;
                data[at + 1] = green;
                data[at + 2] = red;
                data[at + 3] = alpha;
            }
        }
        return new Pixels { Data = data, Width = side, Height = side };
    }

    /// <summary>A made-up favicon: a red rounded square with a white play mark, 32 px.</summary>
    internal static Pixels Favicon()
    {
        const int side = 32;
        var data = new byte[side * side * 4];
        for (var y = 0; y < side; y++)
        {
            for (var x = 0; x < side; x++)
            {
                var at = (y * side + x) * 4;
                var inside = x >= 1 && x <= 30 && y >= 5 && y <= 26
                    && !((x < 4 || x > 27) && (y < 8 || y > 23));
                var mark = x >= 12 && x <= 22 && Math.Abs(y - 15.5) <= (22 - x) * 0.55;
                var (b, g, r, a) = !inside ? (0, 0, 0, 0) : mark ? (255, 255, 255, 255) : (0, 0, 255, 255);
                data[at] = (byte)b;
                data[at + 1] = (byte)g;
                data[at + 2] = (byte)r;
                data[at + 3] = (byte)a;
            }
        }
        return new Pixels { Data = data, Width = side, Height = side };
    }

    /// <summary>A made-up cover: a diagonal wash, so the pictures show art rather than the note.</summary>
    internal static Pixels Cover()
    {
        const int side = 96;
        var data = new byte[side * side * 4];
        for (var y = 0; y < side; y++)
        {
            for (var x = 0; x < side; x++)
            {
                var at = (y * side + x) * 4;
                data[at] = (byte)(60 + x * 2 % 180);
                data[at + 1] = (byte)(40 + (x + y));
                data[at + 2] = (byte)(120 + y % 120);
                data[at + 3] = 255;
            }
        }
        return new Pixels { Data = data, Width = side, Height = side };
    }

    /// <summary>
    /// quick: the widgets this agent added, drawn by the bar's own code at a 40-DIP bar, which is
    /// what these sheets have always used. (They used to stand beside the settings app's own
    /// drawing of the same widget; those previews are gone — docs\widgets.md §7a — so the sheet
    /// is now just the widget, which is the only drawing there is.)
    [UiFact]
    public void WidgetsAsTheBarDrawsThem()
    {
        OnHiddenDesktop((gfx, instance) =>
        {
            foreach (var dark in new[] { true, false })
            {
                var look = LookFor(dark);
                var style = new IslandStyle(1, Vertical: false, Thickness: 40, Icon: 16, look);
                var config = new Karpo.Core.Config();
                var kinds = new (string Name, Karpo.Core.BarKind Kind)[]
                {
                    ("volume", Karpo.Core.BarKind.Volume),
                    ("airplane mode", Karpo.Core.BarKind.AirplaneMode),
                    ("energy saver", Karpo.Core.BarKind.EnergySaver),
                };
                var made = new List<(string Name, CellIsland Island, IslandCells Cells)>();
                foreach (var (name, kind) in kinds)
                {
                    var island = kind.New();
                    if (Karpo.Dock.Islands.Create(Karpo.Core.Config.IslandKey(null, island.Id), island, gfx, config) is not CellIsland cells)
                    {
                        continue;
                    }
                    _ = cells.Refresh(gfx);
                    made.Add((name, cells, cells.Measure(style)));
                }
                try
                {
                    SaveBar(gfx, made, style, look, $"widgets-bar-{(dark ? "dark" : "light")}.png");
                }
                finally
                {
                    foreach (var (_, island, _) in made)
                    {
                        island.Dispose();
                    }
                }
            }
            Assert.Empty(Failures);
        });
    }

    /// <summary>A strip of bar with each widget on it, named under it, as the bar itself draws them.</summary>
    private static void SaveBar(Gfx gfx, List<(string Name, CellIsland Island, IslandCells Cells)> made, IslandStyle style, Look look, string file)
    {
        const float margin = 16;
        const float gap = 24;
        const float caption = 22;
        var theme = FlyoutTheme.For(look);
        var widths = made.Select(made => made.Cells.Lengths is { } lengths ? lengths.Sum() : made.Cells.CellLength * made.Cells.Count).ToArray();
        var w = (uint)(2 * margin + widths.Sum() + gap * MathF.Max(0, made.Count - 1));
        var h = (uint)(2 * margin + style.Thickness + caption);
        ID2D1Factory1* factory = null;
        IWICImagingFactory* wic = null;
        IWICBitmap* bitmap = null;
        ID2D1RenderTarget* target = null;
        ID2D1DeviceContext* dc = null;
        try
        {
            ComUtil.Check(D2D1CreateFactory(D2D1_FACTORY_TYPE.D2D1_FACTORY_TYPE_SINGLE_THREADED, __uuidof<ID2D1Factory1>(), (void**)&factory));
            var clsid = CLSID.CLSID_WICImagingFactory2;
            ComUtil.Check(CoCreateInstance(&clsid, null, (uint)CLSCTX.CLSCTX_INPROC_SERVER, __uuidof<IWICImagingFactory>(), (void**)&wic));
            var format = GUID.GUID_WICPixelFormat32bppPBGRA;
            ComUtil.Check(wic->CreateBitmap(w, h, &format, WICBitmapCreateCacheOption.WICBitmapCacheOnLoad, &bitmap));
            var properties = new D2D1_RENDER_TARGET_PROPERTIES
            {
                type = D2D1_RENDER_TARGET_TYPE.D2D1_RENDER_TARGET_TYPE_SOFTWARE,
                pixelFormat = new D2D1_PIXEL_FORMAT { format = DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM, alphaMode = D2D1_ALPHA_MODE.D2D1_ALPHA_MODE_PREMULTIPLIED },
                dpiX = 96,
                dpiY = 96,
            };
            ComUtil.Check(((ID2D1Factory*)factory)->CreateWicBitmapRenderTarget(bitmap, &properties, &target));
            ComUtil.Check(target->QueryInterface(__uuidof<ID2D1DeviceContext>(), (void**)&dc));
            dc->BeginDraw();
            // The taskbar behind them, as Previews draws its own strip.
            var bar = look.Dark ? new DXGI_RGBA(0.11f, 0.11f, 0.11f, 1) : new DXGI_RGBA(0.93f, 0.93f, 0.93f, 1);
            dc->Clear(&bar);
            var ink = look.Dark ? new DXGI_RGBA(1, 1, 1, 0.55f) : new DXGI_RGBA(0, 0, 0, 0.55f);
            var x = margin;
            for (var index = 0; index < made.Count; index++)
            {
                var (name, island, cells) = made[index];
                var cellWidth = cells.CellLength;
                var at = x;
                for (var cell = 0; cell < cells.Count; cell++)
                {
                    var width = cells.Lengths is { } lengths ? lengths[cell] : cellWidth;
                    var context = dc;
                    var left = at;
                    QuickPanel.Shift(context, left, margin, () => island.PaintCell(context, cell, style, width, style.Thickness));
                    at += width;
                }
                PopupPaint.Text(dc, gfx, name, 11, Weight.Regular, ink, x, margin + style.Thickness + 4, widths[index] + gap);
                x += widths[index] + gap;
            }
            ComUtil.Check(dc->EndDraw(null, null));
            SavePng(wic, bitmap, Path.Combine(Pictures, file));
        }
        finally
        {
            ComUtil.Release(ref dc);
            ComUtil.Release(ref target);
            ComUtil.Release(ref bitmap);
            ComUtil.Release(ref wic);
            ComUtil.Release(ref factory);
        }
    }

    /// <summary>
    /// quick: every page the flyout can be asked for builds itself. The switch used to have a
    /// fall-back arm — the rename box — so a kind added without an arm showed somebody else's page.
    /// The system buttons that opened pages (the network, Bluetooth, the sound) are folders' faces now.
    /// </summary>
    [UiFact]
    public void EveryQuickKindBuildsItsOwnPage()
    {
        OnHiddenDesktop((gfx, instance) =>
        {
            var samples = new Samples();
            using var flyout = new QuickFlyout(gfx, instance, 0, samples.Sources, ClassName);
            Target = message => flyout.Message(message.Msg, message.WParam, message.LParam);
            // The network, Bluetooth, mobile hotspot, power commands and power mode pages are lines
            // of a folder (QuickPageTile), never flyouts, and so are cast's, Win+P's as the cast
            // preset's line, and nearby sharing's.
            foreach (var kind in Enum.GetValues<QuickKind>().Where(kind => kind is not (QuickKind.Wifi or QuickKind.Bluetooth or QuickKind.Hotspot or QuickKind.PowerMode
                or QuickKind.Cast or QuickKind.Projection or QuickKind.Nearby or QuickKind.Keyboard or QuickKind.Vpn)))
            {
                Show(flyout, gfx, LookFor(true), 1.5f, kind);
                Assert.Equal(kind, flyout.Showing);
                Assert.NotNull(flyout.Panel);
                flyout.Hide();
            }

            Target = null;
            Assert.Empty(Failures);
        });
    }

    /// <summary>
    /// quick: every page is as tall as what it shows and no taller — the panel is exactly the page,
    /// and a picture of it has nothing at its foot but the page's own padding — up to the most every
    /// flyout has; on a low work area each page stops at the most, and what does not fit scrolls.
    /// </summary>
    [UiFact]
    public void EveryPageIsAsTallAsWhatItShows()
    {
        OnHiddenDesktop((gfx, instance) =>
        {
            var samples = new Samples();
            using var flyout = new QuickFlyout(gfx, instance, 0, samples.Sources, ClassName);
            Target = message => flyout.Message(message.Msg, message.WParam, message.LParam);
            QuickKind[] pages = [QuickKind.RenameDesktop];
            var most = new FlyoutPlacement(Anchor, Work, Karpo.Core.Edge.Bottom).MostHeight(1.5f);
            foreach (var kind in pages)
            {
                flyout.Show(gfx, LookFor(true), 1.5f, Anchor, Work, Karpo.Core.Edge.Bottom, kind);
                Pump();
                var panel = flyout.Panel!;
                Assert.Equal(panel.Size, flyout.PanelSize);
                Assert.True(flyout.PanelSize.Y <= most, $"{kind} is {flyout.PanelSize.Y} px of the most's {most}");
                // Every row shown, or — a page longer than the most — the most, with the rest a scroll away.
                if (panel.Content.BodyHeight > panel.Viewport + 0.5f)
                {
                    Assert.Equal(most, flyout.PanelSize.Y);
                }
                else
                {
                    Assert.Equal(panel.Content.BodyHeight, panel.Viewport, 1);
                }
                var foot = Fitting.EmptyFoot(flyout.PanelSize.X, flyout.PanelSize.Y, dc => panel.PaintAll(dc));
                Assert.True(foot <= panel.Context.D(24), $"{kind} has {foot} px with nothing on them at its foot");
                flyout.Hide();
                Pump();
            }

            var low = new RECT { left = 0, top = 0, right = 1920, bottom = 420 };
            var anchor = new POINT { x = 900, y = 420 };
            var lowMost = new FlyoutPlacement(anchor, low, Karpo.Core.Edge.Bottom).MostHeight(1.5f);
            var scrolled = 0;
            foreach (var kind in pages)
            {
                flyout.Show(gfx, LookFor(true), 1.5f, anchor, low, Karpo.Core.Edge.Bottom, kind);
                Pump();
                var panel = flyout.Panel!;
                Assert.True(flyout.PanelSize.Y <= lowMost, $"{kind} is {flyout.PanelSize.Y} px of the most's {lowMost}");
                if (panel.Content.BodyHeight > panel.Viewport + 0.5f)
                {
                    // Past the most: the panel is the most, and the body scrolls.
                    Assert.Equal(lowMost, flyout.PanelSize.Y);
                    scrolled++;
                }
                else
                {
                    Assert.Equal(panel.Size, flyout.PanelSize);
                }
                flyout.Hide();
                Pump();
            }
            Target = null;
            Assert.Empty(Failures);
        });
    }

    /// <summary>The page's own widgets and where they sit — what a dropdown must not move.</summary>
    private static List<string> Page(QuickFlyout flyout)
    {
        var content = flyout.Panel!.Content;
        return [.. content.Header.Concat(content.Body).Concat(content.Footer).Select(widget =>
        {
            var at = widget.Bounds;
            return $"{widget.GetType().Name}|{widget.Name}|{at.left},{at.top},{at.right},{at.bottom}";
        })];
    }

    /// <summary>Where a named widget sits in the panel, for "this did not move".</summary>
    private static string Where(QuickFlyout flyout, string name)
    {
        var at = flyout.Panel!.PanelRect(flyout.Panel.Find(name) ?? throw new Xunit.Sdk.XunitException($"No widget named {name}"));
        return $"{at.left},{at.top},{at.right},{at.bottom}";
    }

    /// <summary>Every widget of the flyout, in order, with where it sits: what a scan must not move.</summary>
    private static List<string> Layout(QuickFlyout flyout)
    {
        var content = flyout.Panel!.Content;
        return [.. content.Header.Concat(content.Body).Concat(content.Footer).Select(widget =>
        {
            var at = widget.Bounds;
            return $"{widget.GetType().Name}|{widget.Name}|{widget.Visible}|{at.left},{at.top},{at.right},{at.bottom}";
        })];
    }

    /// <summary>Lets the window's own timers run for <paramref name="milliseconds"/>, pumping what they post.</summary>
    private static void Rest(int milliseconds)
    {
        var until = Environment.TickCount64 + milliseconds;
        while (Environment.TickCount64 < until)
        {
            Thread.Sleep(5);
            Pump();
        }
    }

    /// <summary>
    /// quick: a flyout is centred on the widget that opened it, wherever that widget stands, and
    /// only slides inward where it would leave the work area — never flush to the screen's end.
    /// </summary>
    [UiFact]
    public void AFlyoutIsCentredOnItsWidget()
    {
        OnHiddenDesktop((gfx, instance) =>
        {
            var samples = new Samples();
            using var flyout = new QuickFlyout(gfx, instance, 0, samples.Sources, ClassName);
            Target = message => flyout.Message(message.Msg, message.WParam, message.LParam);
            var look = LookFor(true);
            const float scale = 1.5f;
            // 12 DIP from the work area's end, as QuickFlyout.Show asks for.
            var keep = (int)MathF.Round(12 * scale);
            var second = new RECT { left = 1920, top = 0, right = 3840, bottom = 1200 };

            // A widget in the middle of a bar at the bottom.
            var panel = Open(flyout, gfx, look, scale, new POINT { x = 960, y = 1040 }, Work, Karpo.Core.Edge.Bottom);
            Map(gfx, look, "place-bottom-middle.png", Work, new POINT { x = 960, y = 1040 }, panel, Karpo.Core.Edge.Bottom);
            Assert.InRange(Middle(panel), 959, 961);

            // One at the right end: it slides in only as far as it must, and keeps its margin.
            var right = new POINT { x = 1895, y = 1040 };
            panel = Open(flyout, gfx, look, scale, right, Work, Karpo.Core.Edge.Bottom);
            Map(gfx, look, "place-bottom-right.png", Work, right, panel, Karpo.Core.Edge.Bottom);
            Assert.Equal(Work.right - keep, panel.right);
            Assert.True(panel.left > Work.left, "it left the work area");
            // Still under its widget, not pushed past it: the anchor is inside the panel.
            Assert.InRange(right.x, panel.left, panel.right);

            // One at the left end.
            var left = new POINT { x = 24, y = 1040 };
            panel = Open(flyout, gfx, look, scale, left, Work, Karpo.Core.Edge.Bottom);
            Map(gfx, look, "place-bottom-left.png", Work, left, panel, Karpo.Core.Edge.Bottom);
            Assert.Equal(Work.left + keep, panel.left);
            Assert.InRange(left.x, panel.left, panel.right);

            // A bar standing at the right: the panel is beside the widget, centred on it.
            var side = new POINT { x = 1872, y = 520 };
            panel = Open(flyout, gfx, look, scale, side, Work, Karpo.Core.Edge.Right);
            Map(gfx, look, "place-right-bar.png", Work, side, panel, Karpo.Core.Edge.Right);
            Assert.InRange((panel.top + panel.bottom) / 2, 519, 521);
            Assert.True(panel.right <= side.x, "the panel is over the bar");

            // The same widget high up a side bar: it slides down, keeping its margin.
            var high = new POINT { x = 1872, y = 40 };
            panel = Open(flyout, gfx, look, scale, high, Work, Karpo.Core.Edge.Right);
            Map(gfx, look, "place-right-bar-top.png", Work, high, panel, Karpo.Core.Edge.Right);
            Assert.Equal(Work.top + keep, panel.top);

            // A second display, whose work area starts where the first one ends.
            var far = new POINT { x = 2400, y = 1200 };
            panel = Open(flyout, gfx, look, scale, far, second, Karpo.Core.Edge.Bottom);
            Map(gfx, look, "place-second-middle.png", second, far, panel, Karpo.Core.Edge.Bottom);
            Assert.InRange(Middle(panel), 2399, 2401);
            var edge = new POINT { x = 3820, y = 1200 };
            panel = Open(flyout, gfx, look, scale, edge, second, Karpo.Core.Edge.Bottom);
            Map(gfx, look, "place-second-right.png", second, edge, panel, Karpo.Core.Edge.Bottom);
            Assert.Equal(second.right - keep, panel.right);
            Assert.True(panel.left >= second.left + keep, "it reached onto the first display");

            flyout.Hide();
            Target = null;
            Assert.Empty(Failures);
        });
    }

    private static int Middle(RECT rect) => (rect.left + rect.right) / 2;

    /// <summary>Opens the rename page at <paramref name="anchor"/> and gives back where its panel landed on the screen.</summary>
    private static RECT Open(QuickFlyout flyout, Gfx gfx, Look look, float scale, POINT anchor, RECT work, Karpo.Core.Edge edge)
    {
        flyout.Show(gfx, look, scale, anchor, work, edge, QuickKind.RenameDesktop);
        Pump();
        return flyout.PanelOnScreen;
    }

    /// <summary>
    /// A map of one placement: the work area with the bar on its edge, the widget's spot on it and
    /// the panel that opened, so the pictures show at a glance what is centred on what.
    /// </summary>
    private static void Map(Gfx gfx, Look look, string file, RECT work, POINT anchor, RECT panel, Karpo.Core.Edge edge)
    {
        const float shrink = 0.25f;
        const float margin = 24;
        var theme = FlyoutTheme.For(look);
        var w = (uint)((work.right - work.left) * shrink + 2 * margin);
        var h = (uint)((work.bottom - work.top) * shrink + 2 * margin + 20);
        ID2D1Factory1* factory = null;
        IWICImagingFactory* wic = null;
        IWICBitmap* bitmap = null;
        ID2D1RenderTarget* target = null;
        ID2D1DeviceContext* dc = null;
        try
        {
            ComUtil.Check(D2D1CreateFactory(D2D1_FACTORY_TYPE.D2D1_FACTORY_TYPE_SINGLE_THREADED, __uuidof<ID2D1Factory1>(), (void**)&factory));
            var clsid = CLSID.CLSID_WICImagingFactory2;
            ComUtil.Check(CoCreateInstance(&clsid, null, (uint)CLSCTX.CLSCTX_INPROC_SERVER, __uuidof<IWICImagingFactory>(), (void**)&wic));
            var format = GUID.GUID_WICPixelFormat32bppPBGRA;
            ComUtil.Check(wic->CreateBitmap(w, h, &format, WICBitmapCreateCacheOption.WICBitmapCacheOnLoad, &bitmap));
            var properties = new D2D1_RENDER_TARGET_PROPERTIES
            {
                type = D2D1_RENDER_TARGET_TYPE.D2D1_RENDER_TARGET_TYPE_SOFTWARE,
                pixelFormat = new D2D1_PIXEL_FORMAT { format = DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM, alphaMode = D2D1_ALPHA_MODE.D2D1_ALPHA_MODE_PREMULTIPLIED },
                dpiX = 96,
                dpiY = 96,
            };
            ComUtil.Check(((ID2D1Factory*)factory)->CreateWicBitmapRenderTarget(bitmap, &properties, &target));
            ComUtil.Check(target->QueryInterface(__uuidof<ID2D1DeviceContext>(), (void**)&dc));
            dc->BeginDraw();
            var backdrop = new DXGI_RGBA(0.10f, 0.12f, 0.16f, 1);
            dc->Clear(&backdrop);
            D2D_RECT_F On(RECT rect) => new(
                margin + (rect.left - work.left) * shrink,
                margin + 20 + (rect.top - work.top) * shrink,
                margin + (rect.right - work.left) * shrink,
                margin + 20 + (rect.bottom - work.top) * shrink);
            // The display's work area, then the bar along its edge.
            PopupPaint.Fill(dc, On(work), 2, new DXGI_RGBA(0.16f, 0.19f, 0.24f, 1));
            var bar = 48;
            var barRect = edge switch
            {
                Karpo.Core.Edge.Top => new RECT { left = work.left, top = work.top, right = work.right, bottom = work.top + bar },
                Karpo.Core.Edge.Left => new RECT { left = work.left, top = work.top, right = work.left + bar, bottom = work.bottom },
                Karpo.Core.Edge.Right => new RECT { left = work.right - bar, top = work.top, right = work.right, bottom = work.bottom },
                _ => new RECT { left = work.left, top = work.bottom - bar, right = work.right, bottom = work.bottom },
            };
            PopupPaint.Fill(dc, On(barRect), 2, new DXGI_RGBA(0.24f, 0.28f, 0.34f, 1));
            // The widget the flyout belongs to: a cell of the bar around the anchor.
            var half = bar / 2;
            var widget = edge is Karpo.Core.Edge.Left or Karpo.Core.Edge.Right
                ? new RECT { left = barRect.left, top = anchor.y - half, right = barRect.right, bottom = anchor.y + half }
                : new RECT { left = anchor.x - half, top = barRect.top, right = anchor.x + half, bottom = barRect.bottom };
            PopupPaint.Fill(dc, On(widget), 2, PopupPaint.Ink(theme.Accent));
            // The panel, and the line through the widget's middle: the two line up when it is centred.
            PopupPaint.Fill(dc, On(panel), 3, new DXGI_RGBA(0.86f, 0.88f, 0.92f, 1));
            var through = edge is Karpo.Core.Edge.Left or Karpo.Core.Edge.Right
                ? new RECT { left = work.left, top = anchor.y - 1, right = work.right, bottom = anchor.y + 1 }
                : new RECT { left = anchor.x - 1, top = work.top, right = anchor.x + 1, bottom = work.bottom };
            PopupPaint.Fill(dc, On(through), 0, new DXGI_RGBA(1f, 0.55f, 0.20f, 0.9f));
            PopupPaint.Text(dc, gfx, Path.GetFileNameWithoutExtension(file), 13, Weight.Regular,
                new DXGI_RGBA(0.86f, 0.88f, 0.92f, 1), margin, 6, w - 2 * margin);
            ComUtil.Check(dc->EndDraw(null, null));
            SavePng(wic, bitmap, Path.Combine(Pictures, file));
        }
        finally
        {
            ComUtil.Release(ref dc);
            ComUtil.Release(ref target);
            ComUtil.Release(ref bitmap);
            ComUtil.Release(ref wic);
            ComUtil.Release(ref factory);
        }
    }
    [UiFact]
    public void EveryPageTakesThePointerAndTheKeys()
    {
        OnHiddenDesktop((gfx, instance) =>
        {
            var samples = new Samples();
            using var flyout = new QuickFlyout(gfx, instance, 0, samples.Sources, ClassName);
            Target = message => flyout.Message(message.Msg, message.WParam, message.LParam);
            foreach (var kind in new[] { QuickKind.RenameDesktop })
            {
                Show(flyout, gfx, LookFor(true), 1.5f, kind);
                var window = flyout.Hwnd;
                // Over its box (§355: the window spans the display), in client px.
                var extent = Karpo.Tests.Edit.WindowShots.Extent(window);
                var corner = Karpo.Tests.Edit.WindowShots.Client(window, 0, 0);
                for (var y = corner.y; y < corner.y + extent.bottom - extent.top && flyout.IsOpen; y += 17)
                {
                    for (var x = corner.x; x < corner.x + extent.right - extent.left; x += 29)
                    {
                        Send(window, WM.WM_MOUSEMOVE, 0, Point(x, y));
                    }
                }
                Send(window, WM.WM_MOUSELEAVE, 0, 0);
                foreach (var key in new[] { VK.VK_TAB, VK.VK_DOWN, VK.VK_DOWN, VK.VK_UP, VK.VK_TAB, VK.VK_HOME, VK.VK_END, VK.VK_PRIOR, VK.VK_NEXT, VK.VK_LEFT, VK.VK_RIGHT })
                {
                    Key(flyout, key);
                }
                Pump();
                if (flyout.IsOpen)
                {
                    flyout.Hide();
                }
            }
            Target = null;
            Assert.Empty(Failures);
        });
    }

    private static int Percent(float level) => (int)MathF.Round(level * 100, MidpointRounding.AwayFromZero);

    // --- Driving the flyout --------------------------------------------------------

    private static void Show(QuickFlyout flyout, Gfx gfx, Look look, float scale, QuickKind kind)
    {
        flyout.Show(gfx, look, scale, Anchor, Work, Karpo.Core.Edge.Bottom, kind, FlyoutAlign.End, Guid.NewGuid());
        Assert.True(flyout.IsOpen);
        Assert.Equal(kind, flyout.Showing);
        Pump();
    }

    /// <summary>Where the widget named <paramref name="name"/> is, in the window's client px.</summary>
    private static POINT Centre(QuickFlyout flyout, string name)
    {
        var widget = flyout.Panel!.Find(name) ?? throw new Xunit.Sdk.XunitException($"No widget named {name}");
        var rect = flyout.Panel.PanelRect(widget);
        var origin = flyout.PanelOrigin;
        return new POINT
        {
            x = (int)(origin.X + (rect.left + rect.right) / 2),
            y = (int)(origin.Y + (rect.top + rect.bottom) / 2),
        };
    }

    private static void Click(QuickFlyout flyout, string name)
    {
        var at = Centre(flyout, name);
        var window = flyout.Hwnd;
        Send(window, WM.WM_MOUSEMOVE, 0, Point(at.x, at.y));
        Send(window, WM.WM_LBUTTONDOWN, 1, Point(at.x, at.y));
        Send(window, WM.WM_LBUTTONUP, 0, Point(at.x, at.y));
    }

    private static void Wheel(QuickFlyout flyout, string? name, int delta)
    {
        var at = name is null
            ? new POINT { x = (int)flyout.PanelOrigin.X + 40, y = (int)flyout.PanelOrigin.Y + (int)flyout.PanelSize.Y / 2 }
            : Centre(flyout, name);
        // The wheel's point is on the screen.
        _ = ClientToScreen(flyout.Hwnd, &at);
        Send(flyout.Hwnd, WM.WM_MOUSEWHEEL, (nuint)(uint)(delta << 16), Point(at.x, at.y));
    }

    private static void Key(QuickFlyout flyout, int key) => Send(flyout.Hwnd, WM.WM_KEYDOWN, (nuint)key, 0);

    private static void Type(QuickFlyout flyout, string text)
    {
        foreach (var c in text)
        {
            Send(flyout.Hwnd, WM.WM_CHAR, c, 0);
        }
    }

    // --- Pictures ------------------------------------------------------------------

    /// <summary>
    /// The flyout as it shows: the panel's chrome (solid base, stroke, radius 8) on a neutral
    /// backdrop, then every widget — at the size the host gave the panel (FlyoutHost.Fit).
    /// </summary>
    private static void Save(QuickFlyout flyout, Look look, float scale, string file)
    {
        var panel = flyout.Panel!;
        var size = flyout.PanelSize;
        var theme = FlyoutTheme.For(look);
        const int margin = 16;
        var w = (uint)MathF.Ceiling(size.X) + 2 * margin;
        var h = (uint)MathF.Ceiling(size.Y) + 2 * margin;
        ID2D1Factory1* factory = null;
        IWICImagingFactory* wic = null;
        IWICBitmap* bitmap = null;
        ID2D1RenderTarget* target = null;
        ID2D1DeviceContext* dc = null;
        try
        {
            ComUtil.Check(D2D1CreateFactory(D2D1_FACTORY_TYPE.D2D1_FACTORY_TYPE_SINGLE_THREADED, __uuidof<ID2D1Factory1>(), (void**)&factory));
            var clsid = CLSID.CLSID_WICImagingFactory2;
            ComUtil.Check(CoCreateInstance(&clsid, null, (uint)CLSCTX.CLSCTX_INPROC_SERVER, __uuidof<IWICImagingFactory>(), (void**)&wic));
            var format = GUID.GUID_WICPixelFormat32bppPBGRA;
            ComUtil.Check(wic->CreateBitmap(w, h, &format, WICBitmapCreateCacheOption.WICBitmapCacheOnLoad, &bitmap));
            var properties = new D2D1_RENDER_TARGET_PROPERTIES
            {
                type = D2D1_RENDER_TARGET_TYPE.D2D1_RENDER_TARGET_TYPE_SOFTWARE,
                pixelFormat = new D2D1_PIXEL_FORMAT { format = DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM, alphaMode = D2D1_ALPHA_MODE.D2D1_ALPHA_MODE_PREMULTIPLIED },
                dpiX = 96,
                dpiY = 96,
            };
            ComUtil.Check(((ID2D1Factory*)factory)->CreateWicBitmapRenderTarget(bitmap, &properties, &target));
            ComUtil.Check(target->QueryInterface(__uuidof<ID2D1DeviceContext>(), (void**)&dc));
            dc->BeginDraw();
            var backdrop = theme.Dark ? new DXGI_RGBA(0.20f, 0.24f, 0.30f, 1) : new DXGI_RGBA(0.72f, 0.78f, 0.86f, 1);
            dc->Clear(&backdrop);
            var rect = new D2D_RECT_F(margin, margin, margin + size.X, margin + size.Y);
            PopupPaint.Fill(dc, rect, 8 * scale, PopupPaint.Ink(theme.Base));
            var context = dc;
            QuickPanel.Shift(context, margin, margin, () => panel.PaintAll(context));
            PopupPaint.Stroke(dc, rect, 8 * scale, MathF.Max(1, MathF.Round(scale)), PopupPaint.Ink(theme.Stroke));
            ComUtil.Check(dc->EndDraw(null, null));
            SavePng(wic, bitmap, Path.Combine(Pictures, file));
        }
        finally
        {
            ComUtil.Release(ref dc);
            ComUtil.Release(ref target);
            ComUtil.Release(ref bitmap);
            ComUtil.Release(ref wic);
            ComUtil.Release(ref factory);
        }
    }

    private static void SavePng(IWICImagingFactory* wic, IWICBitmap* bitmap, string path)
    {
        IWICStream* stream = null;
        IWICBitmapEncoder* encoder = null;
        IWICBitmapFrameEncode* frame = null;
        try
        {
            ComUtil.Check(wic->CreateStream(&stream));
            fixed (char* name = path)
            {
                ComUtil.Check(stream->InitializeFromFilename(name, GENERIC_WRITE));
            }
            var png = GUID.GUID_ContainerFormatPng;
            ComUtil.Check(wic->CreateEncoder(&png, null, &encoder));
            ComUtil.Check(encoder->Initialize((IStream*)stream, WICBitmapEncoderCacheOption.WICBitmapEncoderNoCache));
            ComUtil.Check(encoder->CreateNewFrame(&frame, null));
            ComUtil.Check(frame->Initialize(null));
            uint width;
            uint height;
            ComUtil.Check(bitmap->GetSize(&width, &height));
            ComUtil.Check(frame->SetSize(width, height));
            var format = GUID.GUID_WICPixelFormat32bppPBGRA;
            ComUtil.Check(frame->SetPixelFormat(&format));
            ComUtil.Check(frame->WriteSource((IWICBitmapSource*)bitmap, null));
            ComUtil.Check(frame->Commit());
            ComUtil.Check(encoder->Commit());
        }
        finally
        {
            ComUtil.Release(ref frame);
            ComUtil.Release(ref encoder);
            ComUtil.Release(ref stream);
        }
    }

    // --- The hidden desktop ------------------------------------------------------------

    private readonly record struct Sent(uint Msg, WPARAM WParam, LPARAM LParam);

    [ThreadStatic]
    private static Action<Sent>? Target;

    [ThreadStatic]
    private static List<Exception>? failures;

    private static List<Exception> Failures => failures ??= [];

    private static void Register(HINSTANCE instance)
    {
        fixed (char* name = ClassName)
        {
            var type = new WNDCLASSW { lpfnWndProc = &Procedure, hInstance = instance, lpszClassName = name, hCursor = LoadCursorW(HINSTANCE.NULL, IDC.IDC_ARROW) };
            _ = RegisterClassW(&type);
        }
    }

    [UnmanagedCallersOnly]
    private static LRESULT Procedure(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        try
        {
            Target?.Invoke(new Sent(msg, wParam, lParam));
        }
        catch (Exception error)
        {
            Failures.Add(error);
        }
        return DefWindowProcW(hwnd, msg, wParam, lParam);
    }

    private static void Send(HWND window, uint msg, nuint wParam, nint lParam)
    {
        if (IsWindow(window))
        {
            _ = SendMessageW(window, msg, (WPARAM)wParam, (LPARAM)lParam);
        }
    }

    private static nint Point(int x, int y) => (nint)(((y & 0xFFFF) << 16) | (x & 0xFFFF));

    /// <summary>Delivers what was posted: the samples' news, the host's own posts.</summary>
    private static void Pump()
    {
        MSG message;
        for (var round = 0; round < 8; round++)
        {
            var any = false;
            while (PeekMessageW(&message, HWND.NULL, 0, 0, PM.PM_REMOVE))
            {
                any = true;
                _ = TranslateMessage(&message);
                _ = DispatchMessageW(&message);
            }
            if (!any)
            {
                return;
            }
        }
    }

    /// <summary>Runs <paramref name="body"/> on a thread of its own on a desktop nobody sees, an STA with a dispatcher queue and a Gfx.</summary>
    private static void OnHiddenDesktop(Action<Gfx, HINSTANCE> body)
    {
        Exception? failed = null;
        HDESK hidden = default;
        var thread = new Thread(() =>
        {
            try
            {
                fixed (char* name = "karpo-quick-test-" + Guid.NewGuid().ToString("N"))
                {
                    hidden = CreateDesktopW(name, null, null, 0, 0x10000000u /* GENERIC_ALL */, null);
                }
                Assert.True(hidden != HDESK.NULL, "no hidden desktop");
                // Before the thread owns a window: its windows are made there.
                Assert.True(SetThreadDesktop(hidden));
                var options = new DispatcherQueueOptions
                {
                    dwSize = (uint)sizeof(DispatcherQueueOptions),
                    threadType = DISPATCHERQUEUE_THREAD_TYPE.DQTYPE_THREAD_CURRENT,
                    apartmentType = DISPATCHERQUEUE_THREAD_APARTMENTTYPE.DQTAT_COM_STA,
                };
                IDispatcherQueueController* controller = null;
                ComUtil.Check(CreateDispatcherQueueController(options, &controller));
                try
                {
                    using var gfx = new Gfx(gpu: false);
                    var instance = (HINSTANCE)(void*)GetModuleHandleW(null);
                    Register(instance);
                    // The mixer's icons load on this thread, so every sheet is drawn with them
                    // and the same on every run (AppLooks.Inline says why that took a seam).
                    AppLooks.Inline = true;
                    // No key held unless a test says so: the person's keyboard is not the panel's.
                    KeyState.Held = static _ => false;
                    body(gfx, instance);
                }
                finally
                {
                    controller->Release();
                }
            }
            catch (Exception error)
            {
                failed = error;
            }
        });
        thread.Start();
        thread.Join();
        HiddenDesktop.Close(hidden);
        if (failed is not null)
        {
            throw new Xunit.Sdk.XunitException("On the hidden desktop: " + failed);
        }
    }
}
