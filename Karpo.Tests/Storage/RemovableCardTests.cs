// The removable devices in a folder (Karpo.Dock\Islands\RemovableIsland.Cards.cs): a line of a list,
// drawn for real with made-up devices.
//
// Nothing here reaches a real volume. What is plugged in is DriveWatch.Source's, the devices'
// pictures are DriveIcons.Source's (none, so the USB glyph stands in), and an eject or an open is
// handed to DriveWatch.Verb, which only writes it down — the one way a line and the flyout have to
// the shell's verbs.
//
// What is held:
//   * an eject goes to the device whose button was pressed, only where the pointer is, and never to
//     one that has taken that place since; a row opens its device;
//   * a line as tall as it asks shows a row for each device plugged in;
//   * nothing plugged in says so, at every height, with nothing to press;
//   * no line ever cuts a word or draws past its box, at every height, every scale a display runs at
//     and at the largest text, with nothing, one or three devices;
//   * a line of one row up can always eject.
//
// The sheets — the hovered row and button in each theme, and lines one to four rows tall with
// nothing, one and three devices — go to KARPO_PNG_DIR. Needs Direct2D, so it runs only with
// KARPO_UI_TESTS=1.

using System.Numerics;
using Karpo.Core;
using Karpo.Dock;
using Karpo.Dock.Drives;
using Karpo.Tests.Folders;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using Xunit.Abstractions;

namespace Karpo.Tests.Storage;

[Collection(AttachedDrivesCollection.Collection)]
public sealed unsafe class RemovableCardTests(ITestOutputHelper output)
{
    private const string Key = "layout:default/folder/removable";

    /// <summary>The scales a person's display runs at.</summary>
    private static readonly float[] Scales = [1f, 1.25f, 1.5f, 2f];

    /// <summary>The heights a line is held at, in the folder's rows.</summary>
    private static readonly int[] Heights = [1, 2, 3, 4];

    private static ulong Gb(double gigabytes) => (ulong)(gigabytes * 1024 * 1024 * 1024);

    private static Drive Device(int letter, string label, double total, double free, DriveKind kind = DriveKind.Removable) =>
        new()
        {
            Letter = letter,
            Kind = kind,
            Volume = $@"\\?\Volume{{7e57{letter:D4}-0000-0000-0000-000000000000}}\",
            Label = label,
            Total = Gb(total),
            Free = Gb(free),
        };

    /// <summary>A stick, a card and a larger stick, on E:, F: and G:.</summary>
    private static readonly Drive[] Three =
    [
        Device(4, "SANDISK", 28.6, 11.9),
        Device(5, "SD Card", 59.5, 51.2),
        Device(6, "Backup Stick", 238.4, 201.7),
    ];

    private static readonly Drive[] One = [Three[0]];

    private static readonly (string Name, Drive[] Devices)[] Pluggings = [("none", []), ("one", One), ("three", Three)];

    /// <summary>Stands in for what is plugged in, its pictures and the shell's verbs while it lives, and writes down what was asked.</summary>
    private sealed class Plugged : IDisposable
    {
        private readonly Func<bool, bool, IReadOnlyList<Drive>>? drives = DriveWatch.Source;
        private readonly Action<string, string?>? verb = DriveWatch.Verb;
        private readonly Func<string, int, Pixels?>? icons = DriveIcons.Source;
        private Drive[] devices;

        public Plugged(params Drive[] devices)
        {
            this.devices = devices;
            DriveWatch.Source = (_, remote) => [.. this.devices.Where(device => remote || device.Kind != DriveKind.Remote)];
            DriveWatch.Verb = (root, said) => Asked.Add((root, said));
            DriveIcons.Source = (_, _) => null;
        }

        /// <summary>What the widgets asked the shell for: a drive's root and the verb (null to open it).</summary>
        public List<(string Root, string? Verb)> Asked { get; } = [];

        /// <summary>Something else is plugged in from now on.</summary>
        public void Now(params Drive[] next) => devices = next;

        public void Dispose()
        {
            DriveWatch.Source = drives;
            DriveWatch.Verb = verb;
            DriveIcons.Source = icons;
        }
    }

    private static RemovableIsland LineOf(Gfx gfx) =>
        (RemovableIsland)Karpo.Dock.Islands.CreateLine(Key, ListKind.RemovableDevices.New(), ListKind.RemovableDevices, gfx, new Config())!;

    /// <summary>A line's box: the list panel's width, <paramref name="rows"/> of the folder's rows tall.</summary>
    private static D2D_RECT_F LineBox(int rows, float scale, float textScale) =>
        PopupPaint.Rect(0, 0, PopupPaint.Round(Flyout.PanelWidthDip * scale), ListFormPictures.Rows(rows, scale, textScale));

    private static TileStyle LineStyle(float scale, float textScale, bool dark = true) =>
        new(scale, TestLooks.LookFor(dark, textScale), FlyoutTheme.For(TestLooks.LookFor(dark, textScale)), 1, 1);

    private static Vector2 Centre(D2D_RECT_F box) => new((box.left + box.right) / 2, (box.top + box.bottom) / 2);

    // --- Its own controls ---------------------------------------------------------------------

    /// <summary>
    /// A press on a device's eject button ejects that device, through the shell's own verb, and a
    /// press on its row opens it, where the pointer is and nowhere else.
    /// </summary>
    [UiFact]
    public void AnEjectGoesToTheDevicePressedAndNowhereElse()
    {
        using var plugged = new Plugged(Three);
        Offscreen.OnStaThread(gfx =>
        {
            using var line = LineOf(gfx);
            var box = LineBox(3, 1.5f, 1f);
            var style = LineStyle(1.5f, 1f);
            var board = line.Board(box, style);
            Assert.True(board.Rows.Count >= 2, $"a line shows {board.Rows.Count} rows");
            var second = board.Rows[1];
            var eject = Centre(second.Eject);
            Assert.Equal(RemovableIsland.EjectTarget + 1, line.TargetOn(eject, box, style, WidgetSurface.List));

            // A press the pointer is not over does nothing: that is what a folder being arranged
            // tells its widgets, and a line being picked up must not eject anything.
            line.Click(RemovableIsland.EjectTarget + 1);
            Assert.Empty(plugged.Asked);

            line.HoverOn(eject, box, style, WidgetSurface.List);
            var action = line.Click(line.TargetOn(eject, box, style, WidgetSurface.List));
            Assert.Equal(IslandView.None, action.View);
            Assert.Equal(new[] { ("F:\\", (string?)"eject") }, plugged.Asked);

            // The row, less its button, opens the device in File Explorer.
            plugged.Asked.Clear();
            var row = new Vector2(second.Icon.right + 2, Centre(second.Box).Y);
            line.HoverOn(row, box, style, WidgetSurface.List);
            Assert.Equal(RemovableIsland.OpenTarget + 1, line.TargetOn(row, box, style, WidgetSurface.List));
            line.Click(line.TargetOn(row, box, style, WidgetSurface.List));
            Assert.Equal(new[] { ("F:\\", (string?)null) }, plugged.Asked);

            // Anywhere else is the widget as a whole: a click that does nothing, and nothing is asked
            // of the shell (the flyout it once asked for is the removable-devices preset now).
            plugged.Asked.Clear();
            Assert.Equal(default, line.Click(0));
            Assert.Empty(plugged.Asked);
        });
    }

    /// <summary>
    /// The pointer rested on the second device's button, and the devices changed before the press: a
    /// stick pulled out moved the next one into that place. The press ejects nothing — never the
    /// device that took the place of the one the person was pointing at.
    /// </summary>
    [UiFact]
    public void APressAfterTheDevicesChangedEjectsNothing()
    {
        using var plugged = new Plugged(Three);
        Offscreen.OnStaThread(gfx =>
        {
            using var line = LineOf(gfx);
            var box = LineBox(3, 1.5f, 1f);
            var style = LineStyle(1.5f, 1f);
            var eject = Centre(line.Board(box, style).Rows[1].Eject);
            line.HoverOn(eject, box, style, WidgetSurface.List);

            // Windows says a device went; the dock forgets the list and refreshes its widgets.
            plugged.Now(Three[0], Three[2]);
            DriveWatch.Forget();
            line.Refresh(gfx);
            Assert.Equal(2, line.Devices.Count);
            line.Click(RemovableIsland.EjectTarget + 1);
            Assert.Empty(plugged.Asked);
        });
    }

    /// <summary>
    /// A line in a folder is refreshed whenever any widget of the folder has news — its own pointer
    /// included — and none of that is a device coming or going: it reads the drives again only when
    /// one did (the dock forgets the list when Windows says so), and on its minute's tick.
    /// </summary>
    [UiFact]
    public void ARefreshReadsTheDrivesOnlyWhenADeviceCameOrWent()
    {
        using var plugged = new Plugged(Three);
        var reads = 0;
        var devices = DriveWatch.Source!;
        DriveWatch.Source = (labels, remote) =>
        {
            reads++;
            return devices(labels, remote);
        };
        Offscreen.OnStaThread(gfx =>
        {
            using var line = LineOf(gfx);
            var made = reads;
            for (var news = 0; news < 5; news++)
            {
                line.Refresh(gfx);
            }
            Assert.Equal(made, reads);

            DriveWatch.Forget();
            line.Refresh(gfx);
            Assert.Equal(made + 1, reads);

            line.Tick(gfx);
            Assert.Equal(made + 2, reads);
        });
    }

    // --- What is plugged in and the empty state -------------------------------------------------

    /// <summary>A line as tall as it asks for (ListHeight) shows a row for each device plugged in, and none where nothing is.</summary>
    [UiFact]
    public void ALineShowsARowForEachDevicePluggedIn()
    {
        foreach (var (name, devices) in Pluggings)
        {
            using var plugged = new Plugged(devices);
            Offscreen.OnStaThread(gfx =>
            {
                using var line = LineOf(gfx);
                var style = LineStyle(1.5f, 1f);
                var width = TilePaint.Width(LineBox(1, 1.5f, 1f));
                var box = PopupPaint.Rect(0, 0, width, line.ListHeight(width, style));
                var board = line.Board(box, style);
                Assert.Equal(devices.Length, board.Rows.Count);
                Assert.Equal(devices.Length == 0, board.Empty is not null);
                output.WriteLine($"{name}: {board.Rows.Count} rows");
            });
        }
    }

    /// <summary>
    /// Nothing plugged in says so — "Nothing plugged in" — on a line of every height, beside a quiet
    /// glyph, with nothing to press.
    /// </summary>
    [UiFact]
    public void NothingPluggedInSaysSoEverywhere()
    {
        using var plugged = new Plugged();
        Offscreen.OnStaThread(gfx =>
        {
            using var line = LineOf(gfx);
            foreach (var rows in Heights)
            {
                var box = LineBox(rows, 1.5f, 1f);
                var style = LineStyle(1.5f, 1f);
                var board = line.Board(box, style);
                Assert.Equal(RemovableIsland.NothingText, board.Empty?.Words.Single().Text);
                Assert.Empty(board.Rows);
                Assert.True(Texts(gfx, line, box, style, WidgetSurface.List) > 0, $"a line of {rows} row(s) says nothing");
                Assert.Equal(0, line.TargetOn(Centre(box), box, style, WidgetSurface.List));
            }
        });
    }

    /// <summary>How many lines of words a line draws.</summary>
    private static int Texts(Gfx gfx, RemovableIsland widget, D2D_RECT_F box, TileStyle style, WidgetSurface surface)
    {
        var count = 0;
        PaintWatch.Watching = (_, what, color) =>
        {
            count += what == Painted.Text ? 1 : 0;
            return color;
        };
        try
        {
            Offscreen.Bitmap(TilePaint.Width(box), TilePaint.Height(box), dc => widget.PaintOn(dc, box, style, surface), _ => { });
        }
        finally
        {
            PaintWatch.Watching = null;
        }
        return count;
    }

    // --- Nothing cut, nothing past the edge -----------------------------------------------------

    /// <summary>
    /// No line from one row to four ever cuts a word with an ellipsis or draws a word, a glyph or a
    /// picture past its box — at every scale a display runs at, at the largest text, and with
    /// nothing, one or three devices plugged in. And every line that shows a device's row gives it
    /// its eject button inside the box.
    /// </summary>
    [UiFact]
    public void NoLineCutsAWordOrDrawsPastItsBox()
    {
        var failures = new List<string>();
        foreach (var (name, devices) in Pluggings)
        {
            using var plugged = new Plugged(devices);
            Offscreen.OnStaThread(gfx =>
            {
                using var line = LineOf(gfx);
                foreach (var scale in Scales)
                {
                    foreach (var textScale in new[] { 1f, 2.25f })
                    {
                        foreach (var rows in Heights)
                        {
                            var box = LineBox(rows, scale, textScale);
                            Watch(failures, $"{name}: a line of {rows} row(s) at {scale * 100:0} %, text {textScale * 100:0} %", gfx, line, box, LineStyle(scale, textScale), WidgetSurface.List);
                        }
                    }
                }
            });
        }
        Assert.True(failures.Count == 0, $"{failures.Count} wrong:\n{string.Join("\n", failures.Take(60))}");
    }

    /// <summary>
    /// One line drawn and watched: nothing trimmed, no ink past the box, every row's button inside
    /// it. Drawn with a margin all round and without the clip a folder puts on a line, so ink that
    /// reaches past the box is there to be seen; ink, not line boxes, because a line's box carries
    /// the font's ascent and descent and a large figure's box is taller than its line while its ink
    /// is not.
    /// </summary>
    private static void Watch(List<string> failures, string where, Gfx gfx, RemovableIsland widget, D2D_RECT_F box, TileStyle style, WidgetSurface surface)
    {
        const float margin = 24;
        var boxWidth = TilePaint.Width(box);
        var boxHeight = TilePaint.Height(box);
        var width = boxWidth + (2 * margin);
        var height = boxHeight + (2 * margin);
        (float Left, float Top, float Right, float Bottom) ink = default;
        PaintWatch.Trimmed = 0;
        PaintWatch.Watching = (_, _, color) => color;
        try
        {
            Offscreen.Bitmap(width, height,
                dc => Offscreen.Shifted(dc, margin, margin, () => widget.PaintCard(dc, box, style, surface)),
                bitmap => ink = Offscreen.InkOf(bitmap, (uint)MathF.Ceiling(width), (uint)MathF.Ceiling(height)));
        }
        finally
        {
            PaintWatch.Watching = null;
        }
        if (PaintWatch.Trimmed > 0)
        {
            failures.Add($"{where}: {PaintWatch.Trimmed} line(s) cut with an ellipsis");
        }
        if (ink.Right > ink.Left
            && (ink.Left < margin - 1 || ink.Top < margin - 1 || ink.Right > margin + boxWidth + 1 || ink.Bottom > margin + boxHeight + 1))
        {
            failures.Add($"{where}: ink at {ink.Left - margin:0},{ink.Top - margin:0}..{ink.Right - margin:0},{ink.Bottom - margin:0}, past its {boxWidth:0}x{boxHeight:0} box");
        }
        foreach (var row in widget.Board(box, style).Rows)
        {
            if (TilePaint.Width(row.Eject) < 1 || row.Eject.left < box.left || row.Eject.right > box.right || row.Eject.bottom > box.bottom + 1)
            {
                failures.Add($"{where}: row {row.Index}'s eject button is not inside the box");
            }
        }
    }

    /// <summary>
    /// Workable from the smallest: a line one row tall with three devices shows one device with its
    /// eject button, and scrolls to the others (ListScroll.cs) rather than saying how many it left out.
    /// </summary>
    [UiFact]
    public void TheSmallestLineCanStillEject()
    {
        using var plugged = new Plugged(Three);
        Offscreen.OnStaThread(gfx =>
        {
            using var line = LineOf(gfx);
            foreach (var scale in Scales)
            {
                var one = line.Board(LineBox(1, scale, 1f), LineStyle(scale, 1f));
                var row = Assert.Single(one.Rows);
                Assert.True(TilePaint.Width(row.Eject) > 0);
                var laid = line.Scrolled(LineBox(1, scale, 1f), LineStyle(scale, 1f), WidgetSurface.List);
                Assert.True(laid.Scrolls);
                Assert.Equal(3, line.Board(laid.Moved, LineStyle(scale, 1f)).Rows.Count);
                output.WriteLine($"{scale * 100:0} %: a one-row line scrolls {laid.Most:0} px");
            }
        });
    }

    // --- The sheets ------------------------------------------------------------------------------

    /// <summary>
    /// The pointer over a device's row, and over its eject button, on a line, in each theme:
    /// removable-hover-&lt;dark|light&gt;.png in KARPO_PNG_DIR. Each lights its own plate and nothing
    /// else.
    /// </summary>
    [UiFact]
    public void TheHoveredRowAndButtonArePictured()
    {
        using var plugged = new Plugged(Three);
        Offscreen.OnStaThread(gfx =>
        {
            using var line = LineOf(gfx);
            const float scale = 1.5f;
            foreach (var dark in new[] { true, false })
            {
                var theme = FlyoutTheme.For(TestLooks.LookFor(dark));
                var lineBox = LineBox(3, scale, 1f);
                var lineStyle = LineStyle(scale, 1f, dark);
                var lineRow = line.Board(lineBox, lineStyle).Rows[1];
                var pad = PopupPaint.Round(12 * scale);
                var width = pad + (2 * (TilePaint.Width(lineBox) + pad));
                var height = pad + TilePaint.Height(lineBox) + pad;
                var path = Path.Combine(Offscreen.Pictures, $"removable-hover-{(dark ? "dark" : "light")}.png");
                Offscreen.Bitmap(width, height, dc =>
                {
                    PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, width, height), 0, PopupPaint.Ink(theme.Base));
                    var x = pad;
                    foreach (var over in new[] { new Vector2(lineRow.Icon.right + 2, Centre(lineRow.Box).Y), Centre(lineRow.Eject) })
                    {
                        var at = PopupPaint.Rect(x, pad, TilePaint.Width(lineBox), TilePaint.Height(lineBox));
                        PopupPaint.Fill(dc, at, PopupPaint.Round(8 * scale), PopupPaint.Ink(theme.CardBackground));
                        line.HoverOn(over, lineBox, lineStyle, WidgetSurface.List);
                        Offscreen.Shifted(dc, x, pad, () => line.PaintOn(dc, lineBox, lineStyle, WidgetSurface.List));
                        x += TilePaint.Width(lineBox) + pad;
                    }
                }, bitmap => Offscreen.Save(bitmap, path));
                line.HoverOn(null, lineBox, lineStyle, WidgetSurface.List);
                output.WriteLine(path);
            }
        });
    }

    /// <summary>
    /// Lines one to four rows tall, in a list folder, with nothing, one and three devices plugged in:
    /// removable-lines-&lt;dark|light&gt;.png in KARPO_PNG_DIR, three panels side by side.
    /// </summary>
    [UiFact]
    public void EveryLineHeightIsPictured()
    {
        foreach (var dark in new[] { true, false })
        {
            foreach (var (name, devices) in Pluggings)
            {
                using var plugged = new Plugged(devices);
                Offscreen.OnStaThread(gfx =>
                {
                    var look = ListFormPictures.LookFor(dark);
                    var folder = ListFormPictures.Folder(gfx,
                    [
                        .. Heights.Select(rows => ListFormPictures.Line(ListKind.RemovableDevices, island => island.Height = LineHeight.Exactly(rows * LineHeight.Row))),
                    ]);
                    var (_, plan) = ListFormPictures.Plan(folder, look, 1.5f);
                    var file = $"removable-lines-{name}-{(dark ? "dark" : "light")}.png";
                    ListFormPictures.Sheet(gfx, file, 1.5f, [($"Removable devices: {name}, 1 to 4 rows", look, folder, plan)]);
                    output.WriteLine(Path.Combine(Offscreen.Pictures, file));
                    folder.Closed();
                    folder.Dispose();
                });
            }
        }
    }
}
