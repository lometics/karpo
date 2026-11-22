// Windows' buttons after Start, search and task view — the widgets board, the pen menu, the
// touch keyboard and the virtual touchpad (WindowsButtons, BarEntries.IsWindowsButton):
//
//   * what a click on each opens, through the seam that says it without doing it
//     (WindowsButtons.Target): Win+W, Windows' own addresses
//     for the pen menu and the touchpad, the touch keyboard's own switch — nothing is pressed or
//     started here;
//   * where the bar puts each: one button, as wide as search's, whose press hits that button; and an
//     input button this PC would not show takes no room, only edit mode's placeholder;
//   * and what they look like beside Start, search and task view: each its glyph at the size the
//     bar draws a button's (Scene.GlyphButton), to windows-buttons.png, dark over light.

using Karpo.Dock;
using TerraFX.Interop.Windows;
using static Karpo.Tests.Islands.Offscreen;

namespace Karpo.Tests.Islands;

public sealed unsafe class WindowsButtonsTests
{
    /// <summary>A desktop: no pen, no touch screen, a keyboard.</summary>
    private static readonly InputHardware Desktop = new(Pen: false, Touch: false, Slate: false);

    /// <summary>A pen tablet with its keyboard taken off: every input button shows.</summary>
    private static readonly InputHardware PenTablet = new(Pen: true, Touch: true, Slate: true);

    // --- What a click opens ---------------------------------------------------------------------

    [Fact]
    public void TheWidgetsBoardIsWinW() =>
        Assert.Equal(new ShellTarget(OpenBy.Shortcut, Key: 'W'), WindowsButtons.Target(BarKind.WidgetsBoard, _ => true));

    /// <summary>The pen menu and the virtual touchpad: the addresses Windows registers for them.</summary>
    [Fact]
    public void ThePenMenuAndTheTouchpadOpenWindowsOwnAddresses()
    {
        Assert.Equal(new ShellTarget(OpenBy.Address, "ms-penworkspace:"), WindowsButtons.Target(BarKind.PenMenu, _ => false));
        Assert.Equal(new ShellTarget(OpenBy.Address, "ms-virtualtouchpad:"), WindowsButtons.Target(BarKind.VirtualTouchpad, _ => false));
    }

    /// <summary>The touch keyboard is its own switch, which shows it and hides it again.</summary>
    [Fact]
    public void TheTouchKeyboardIsItsOwnSwitch() =>
        Assert.Equal(new ShellTarget(OpenBy.TouchKeyboard), WindowsButtons.Target(BarKind.TouchKeyboard, _ => false));

    /// <summary>
    /// A click hands exactly its target over, once; a kind that is not one of these buttons hands
    /// over nothing and says so.
    /// </summary>
    [Fact]
    public void OpenHandsItsTargetOverAndNothingElseIsAButton()
    {
        foreach (var kind in BarEntries.All.Select(entry => entry.Kind))
        {
            var opened = new List<ShellTarget>();
            var asked = WindowsButtons.Open(kind, _ => false, opened.Add);
            Assert.Equal(kind.IsWindowsOwn(), asked);
            if (asked)
            {
                Assert.Equal([WindowsButtons.Target(kind, _ => false)!.Value], opened);
            }
            else
            {
                Assert.Empty(opened);
            }
        }
    }

    /// <summary>
    /// A package family is installed where a package of its name and its publisher is, whatever
    /// its version and architecture; another name or publisher, or a name that only begins the same,
    /// is not.
    /// </summary>
    [Fact]
    public void APackageIsFoundByItsNameAndItsPublisher()
    {
        const string family = "Microsoft.ScreenSketch_8wekyb3d8bbwe";
        Assert.True(WindowsButtons.Installed(family, ["Microsoft.WindowsTerminal_1.24.1.0_x64__8wekyb3d8bbwe", "Microsoft.ScreenSketch_11.2307.0.0_x64__8wekyb3d8bbwe"]));
        Assert.True(WindowsButtons.Installed(family, ["microsoft.screensketch_2.0.0.0_arm64__8WEKYB3D8BBWE"]));
        Assert.False(WindowsButtons.Installed(family, ["Microsoft.ScreenSketchPreview_1.0.0.0_x64__8wekyb3d8bbwe"]));
        Assert.False(WindowsButtons.Installed(family, ["Microsoft.ScreenSketch_1.0.0.0_x64__cw5n1h2txyewy"]));
        Assert.False(WindowsButtons.Installed(family, []));
        Assert.False(WindowsButtons.Installed("nopublisher", ["nopublisher_1.0.0.0_x64__"]));
    }

    // --- On the bar -------------------------------------------------------------------------------

    private static readonly Config Bar = new() { Height = 48 };

    private static Layout Lay(IReadOnlyList<PartSpec> specs, bool editing = false) =>
        Layout.ForTests(
            96,
            new RECT { left = 0, top = 0, right = 4000, bottom = 1080 },
            Edge.Bottom,
            Bar,
            specs,
            Layout.CellPx(96, Bar),
            editing: editing);

    /// <summary>
    /// Each is one button, as wide as search's, whose middle hits it — its own kind of hit, naming
    /// the button and the part it is in — with its box where the button's glyph goes.
    /// </summary>
    [Fact]
    public void EachIsOneButtonAsWideAsSearchsAndItsPressHitsIt()
    {
        BarKind[] kinds = [BarKind.Search, BarKind.SnippingTool, BarKind.PenMenu, BarKind.TouchKeyboard, BarKind.VirtualTouchpad];
        var specs = kinds.Select((kind, index) => PartSpec.For(Config.IslandKey(null, $"p{index}"), kind.New(), PenTablet)).ToList();
        var layout = Lay(specs);
        Assert.Empty(layout.NotShown);
        var search = layout.PartAt(0);
        for (var part = 1; part < kinds.Length; part++)
        {
            var kind = kinds[part];
            var placed = layout.PartAt(part);
            Assert.Equal(search.Width, placed.Width);
            Assert.False(layout.Empty(part));
            var hit = Hit.WindowsButton(kind).In(part);
            var rect = layout.ItemRect(hit);
            Assert.False(rect.IsEmpty, $"{kind} has no button");
            Assert.Equal(hit, layout.HitTest(rect.X + (rect.Width / 2f), rect.Y + (rect.Height / 2f)));
            Assert.Equal(part, layout.PartOf(Hit.WindowsButton(kind)));
            Assert.Equal((int)kind, hit.Index);
        }
    }

    /// <summary>
    /// An input button this PC would not show is a part with nothing to show: no room on the bar,
    /// nothing to press — and in edit mode the placeholder every empty part has, so it can still be
    /// found, moved and set to Always. Set to Always, it is a button on any PC.
    /// </summary>
    [Fact]
    public void AnInputButtonThisPCWouldNotShowTakesNoRoom()
    {
        var pen = BarKind.PenMenu.New();
        var start = PartSpec.For(Config.IslandKey(null, "start"), BarKind.Start.New(), Desktop);
        var hidden = PartSpec.For(Config.IslandKey(null, "pen"), pen, Desktop);
        Assert.True(hidden.Absent);

        var layout = Lay([start, hidden]);
        Assert.Equal(0f, layout.PartAt(1).Width);
        Assert.False(layout.Placed(1));

        var editing = Lay([start, hidden], editing: true);
        Assert.True(editing.Empty(1));
        Assert.True(editing.PartAt(1).Width > 0f);

        pen.InputButton = new InputButtonOptions { Show = InputButtonShow.Always };
        var always = PartSpec.For(Config.IslandKey(null, "pen"), pen, Desktop);
        Assert.False(always.Absent);
        var shown = Lay([start, always]);
        Assert.True(shown.Placed(1));
        Assert.False(shown.Empty(1));

        // On a PC with a pen, as Windows decides, it is there with no option at all.
        Assert.False(PartSpec.For(Config.IslandKey(null, "pen"), BarKind.PenMenu.New(), PenTablet).Absent);
        // And nothing but the three is ever absent.
        Assert.False(PartSpec.For(Config.IslandKey(null, "board"), BarKind.WidgetsBoard.New(), Desktop).Absent);
    }

    // --- The picture ------------------------------------------------------------------------------

    /// <summary>
    /// Windows' buttons as the bar draws them, beside Start, search and task view — the Snipping Tool
    /// after them (§474, §490: the Widgets button and Windows' menus wear faces now), the three input buttons at the end — on a 48 DIP bar at 150 %,
    /// dark over light, each glyph at a button's size in its box (Scene: 20 DIP in 24; Windows' glyph buttons a face's icon, §497). Start is drawn
    /// here by its glyph; the bar draws Windows' logo there.
    /// </summary>
    [UiFact]
    public void WindowsButtonsPicture()
    {
        const uint dpi = 144;
        BarKind[] kinds =
        [
            BarKind.Start, BarKind.Search, BarKind.TaskView, BarKind.SnippingTool,
            BarKind.PenMenu, BarKind.TouchKeyboard, BarKind.VirtualTouchpad,
        ];
        OnStaThread(gfx =>
        {
            var config = new Config { Height = 48 };
            var specs = kinds.Select((kind, index) => PartSpec.For(Config.IslandKey(null, $"p{index}"), kind.New(), PenTablet)).ToList();
            var layout = Layout.ForTests(dpi, new RECT { right = 2000, bottom = 1080 }, Edge.Bottom, config, specs, Layout.CellPx(dpi, config));
            var blocks = Enumerable.Range(0, kinds.Length).Select(layout.BlockRect).ToList();
            var left = blocks.Min(block => block.X);
            var top = blocks.Min(block => block.Y);
            var width = blocks.Max(block => block.Right) - left;
            const float pad = 24;
            var band = layout.Thickness + (2 * pad);
            Bitmap(width + (2 * pad), 2 * band, dc =>
            {
                var at = 0f;
                foreach (var dark in new[] { true, false })
                {
                    var theme = FlyoutTheme.For(new Look(Glass: false, Dark: dark, Accent: Windows.UI.Color.FromArgb(255, 0, 0x78, 0xD4)));
                    PopupPaint.Fill(dc, PopupPaint.Rect(0, at, width + (2 * pad), band), 0, PopupPaint.Ink(theme.Base));
                    for (var part = 0; part < kinds.Length; part++)
                    {
                        var kind = kinds[part];
                        var hit = kind switch
                        {
                            BarKind.Start => Hit.Start,
                            BarKind.Search => Hit.Search,
                            BarKind.TaskView => Hit.TaskView,
                            _ => Hit.WindowsButton(kind),
                        };
                        // §497: Windows' glyph buttons at a face's icon, in a box their own size; Start, search and Task view 20 DIP in 24.
                        var glyph = kind.IsWindowsButton() ? Scene.FaceGlyph(layout) : layout.UnitPx(20);
                        var box = layout.IconBox(hit.In(part), kind.IsWindowsButton() ? glyph : layout.UnitPx(24));
                        PopupPaint.Glyph(dc, gfx, kind.Glyph(), glyph, theme.Text,
                            PopupPaint.Rect(box.X - left + pad, box.Y - top + at + pad, box.Width, box.Height));
                    }
                    at += band;
                }
            }, bitmap => Save(bitmap, Path.Combine(Pictures, "windows-buttons.png")));
        });
    }
}
