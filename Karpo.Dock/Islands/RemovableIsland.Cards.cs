// The removable devices in a folder: a line of a list (docs\widgets.md §9).
//
// The user: "removable devices should be shown in grid and list view" (the grid went on
// 2026-10-09). The widget left folders on
// 2026-09-25 because its face on the bar was only a way into its flyout, and nothing in a folder opens
// a popup (§15). So in a folder it is not that button: it shows what the flyout showed — the devices
// themselves — and acts through controls of its own, which are not a click on the widget and so
// never reach the folder's rule. Since 2026-10-10 the line is all there is: the button and its
// flyout became the removable-devices preset, a folder holding this line (§91).
//
// On a list, a row per device, the kit's row with its caption (§401): the picture in the leading column,
// the name where every name of the list begins, what it holds under it ("11.9 GB free of 28.6 GB"), and
// "Eject", a standard button, on the trailing gutter, as the flyout said it. A device is a row of 52 DIP;
// a line shorter than its devices shares its height down to a folder's cell each and then scrolls through
// them (ListScroll.cs), each with its button.
//
// The eject button says "Eject" (in Karpo's language), as the flyout did and File Explorer does.
//
// Nothing plugged in is the kit's one Status row (LineStatus): the USB glyph, quiet, and "Nothing plugged
// in" at the line's top.
//
// Its height is a list line's (§76): Automatic grows a row per device up to its most and then
// scrolls; Fixed is that many rows, the devices sharing them, and scrolling past them.
//
// A press on a device's row opens it in File Explorer, an app opening, which is what a folder allows;
// a press on its eject button asks the shell to eject it, and Windows says how that went, as it does
// for Explorer's own Eject. Both go through DriveWatch, where a test hands in its own
// (DriveWatch.Verb), and what a line draws is read through DriveWatch.Source and DriveIcons.Source:
// nothing a test or an offscreen picture does reaches a real volume.
//
// One layout (DeviceBoard) is what a line is drawn from and what a press is tested against, so the
// two cannot disagree, and it is a function of the room, the style and the devices alone.

using System.Globalization;
using System.Numerics;
using Karpo.Core;
using Karpo.Dock.Drives;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock;

internal sealed unsafe partial class RemovableIsland : IListContent, IListScroll
{
    /// <summary>What a folder's line says with nothing plugged in, where it fits whole.</summary>
    internal static string NothingText => S.WidgetRemovableNothing;

    /// <summary>The same, where only a word fits.</summary>
    internal static string NoneText => S.WidgetRemovableNone;

    /// <summary>What the Status says with nothing plugged in on a line <paramref name="width"/> px wide: the sentence where it fits whole, else the word.</summary>
    private string Nothing(in TileStyle style, float width)
    {
        var form = ListForm.Of(Gfx, style);
        return Fits([NothingText, NoneText], form.TextPx, Weight.Regular, width - form.TextLeft - form.Inset)?.Text ?? NoneText;
    }

    /// <summary>Nothing of the line's own under the pointer: the widget as a whole.</summary>
    private const int NoTarget = 0;

    /// <summary>A device's row, which opens it: this plus the device's place in <see cref="Devices"/>.</summary>
    internal const int OpenTarget = 1_000;

    /// <summary>A device's eject button: this plus the device's place in <see cref="Devices"/>.</summary>
    internal const int EjectTarget = 2_000;

    // What the pointer is over, and the root of the device it was over: a press acts only where the
    // pointer is (a folder being arranged tells a widget the pointer is nowhere), and only on the
    // device it was over — never on whichever device has taken that place since.
    private int hot = NoTarget;

    // The row or button a press is down on, lit pressed (RowPress).
    private RowPress press;
    private string hotRoot = "";

    /// <summary>
    /// A list's line: a row per device, or the one row saying nothing is plugged in — a device's row each
    /// (the kit's row with its caption), with nothing summarised.
    /// </summary>
    public float ListHeight(float width, in TileStyle style) =>
        devices.Count == 0 ? LineRows.Px(1, style) : DeviceRowPx(style) * devices.Count;

    /// <summary>A row of a device each, where the line has less than the least a row is each: they scroll (ListScroll.cs).</summary>
    public float ScrollHeight(float width, float height, in TileStyle style) =>
        devices.Count > 1 && height + 0.5f < devices.Count * LeastRow(style) ? ListHeight(width, style) : 0;

    /// <summary>A device's row: the kit's row with its caption, 52 DIP (§401).</summary>
    private static float DeviceRowPx(in TileStyle style) => LineRow.Height(style, caption: true);

    /// <summary>The least a device's row shares a line down to: a folder's cell, 4 DIP under the list's row.</summary>
    private static float LeastRow(in TileStyle style) => PopupPaint.Round(36 * style.Scale * style.TextScale);

    // --- The drawing --------------------------------------------------------------------------

    protected internal override void PaintCard(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, WidgetSurface surface) =>
        Paint(dc, box, style);

    /// <summary>A line's devices, laid out as <see cref="Board"/> lays them: each the kit's row, its picture in the leading column.</summary>
    private void Paint(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style)
    {
        var theme = style.Theme;
        var sketch = new LineSketch(dc, Gfx);
        if (devices.Count == 0)
        {
            LineStatus.Draw(sketch, box, style, UsbGlyph, Nothing(style, TilePaint.Width(box)), []);
            return;
        }
        foreach (var row in Board(box, style).Rows)
        {
            var device = devices[row.Index];
            // The pointer's band, the row edge to edge (LineItem); its Eject lights on its own over it.
            if (hot == OpenTarget + row.Index)
            {
                LineItem.Light(dc, row.Box, theme, press.On(hot));
            }
            var lit = hot == EjectTarget + row.Index;
            LineRow.Draw(sketch, row.Box.left, row.Box.right, row.Box.top, style, row.Spec with
            {
                Controls = [row.Spec.Controls[0] with { Hot = lit, Pressed = lit && press.On(hot) }],
            }, row.Box.bottom - row.Box.top);
            // The shell's own picture of the device, and the USB glyph while there is none.
            if (DriveIcons.For(device.Volume, device.Root, (int)MathF.Ceiling(TilePaint.Width(row.Icon)), Announce) is { } picture)
            {
                PopupPaint.Picture(dc, picture, row.Icon, 0);
            }
            else
            {
                PopupPaint.Glyph(dc, Gfx, UsbGlyph, row.GlyphPx, theme.Text, row.Icon);
            }
        }
    }

    /// <summary>The word the list's button says: File Explorer's, as the flyout said it.</summary>
    private static string EjectName => S.DockDrivesEject;

    // --- The layout ---------------------------------------------------------------------------

    /// <summary>
    /// The devices laid out in <paramref name="box"/>: a row per device on the kit's row (§401) — the
    /// picture in the leading column, the name where every name begins, what it holds under it, "Eject",
    /// a standard button, on the trailing gutter — as many as the line holds from its top, sharing its
    /// height down to the least a row is; where it scrolls, it is as tall as all of them (ListScroll.cs).
    /// Nothing plugged in is the kit's one Status row.
    /// </summary>
    internal DeviceBoard Board(D2D_RECT_F box, in TileStyle style)
    {
        var form = ListForm.Of(Gfx, style);
        var width = TilePaint.Width(box);
        var height = TilePaint.Height(box);
        var board = new DeviceBoard();
        if (devices.Count == 0)
        {
            var nothing = Nothing(style, width);
            var laid = LineStatus.Lay(Gfx, box, style, glyph: true, nothing, []);
            board.Empty = new BoardEmpty(
                laid.Glyph ?? default,
                form.Glyph,
                laid.Words is { } said ? [new BoardWords(nothing, form.TextPx, said.left, said.top)] : []);
            return board;
        }
        var natural = DeviceRowPx(style);
        var shown = Math.Clamp((int)MathF.Floor((height + 0.5f) / LeastRow(style)), 1, devices.Count);
        var row = MathF.Min(natural, height / shown);
        var captioned = row + 0.5f >= LineRow.Height(style, caption: true);
        var buttons = LineButton.Of(Gfx, style);
        var eject = RowControl.Button(Gfx, buttons, "eject", EjectName, null, QuickSettings.Controls.ButtonStyle.Standard);
        var room = MathF.Max(1, width - form.TextLeft - form.Inset - eject.Width - style.Px(LineSpace.GapDip));
        var captionPx = style.Text(LineType.CaptionDip);
        for (var index = 0; index < shown; index++)
        {
            var device = devices[index];
            var y = box.top + MathF.Round(index * row);
            var h = box.top + MathF.Round((index + 1) * row) - y;
            var names = NameForms(device).ToList();
            var spec = new RowSpec(Fits(names, form.TextPx, Weight.Regular, room)?.Text ?? names[^1])
            {
                Caption = captioned ? Fits(DetailForms(device), captionPx, Weight.Regular, room)?.Text : null,
                Controls = [eject],
            };
            var laid = LineRow.Draw(new LineSketch(null, Gfx), box.left, box.right, y, style, spec, h);
            board.Rows.Add(new DeviceRow(
                index,
                LineItem.Band(box, y, y + h),
                form.IconBox(box.left, y, h),
                form.Glyph,
                spec,
                laid.Controls[0]));
        }
        return board;
    }

    /// <summary>A device's name, fullest first: "SANDISK (E:)", "SANDISK", "E:".</summary>
    private static IEnumerable<string> NameForms(Drive device)
    {
        yield return device.Title;
        if (device.Label.Length > 0 && device.Label != device.Title)
        {
            yield return device.Label;
        }
        if (device.Name.Length > 0)
        {
            yield return device.Name;
        }
    }

    /// <summary>What a device holds, fullest first — "12 GB free of 29 GB", "12 GB free" — or why it has no figures.</summary>
    private static IEnumerable<string> DetailForms(Drive device)
    {
        if (device.Total == 0)
        {
            yield return DriveListing.NotReadyLine(device.Kind);
            yield break;
        }
        yield return Detail(device);
        yield return DriveListing.FreeSpace(device.Total, device.Free);
    }

    /// <summary>The first of <paramref name="forms"/> that fits <paramref name="room"/> px whole at <paramref name="px"/>, and its width.</summary>
    private (string Text, float Width)? Fits(IEnumerable<string> forms, float px, Weight weight, float room)
    {
        foreach (var form in forms)
        {
            if (form.Length == 0)
            {
                continue;
            }
            var width = PopupPaint.Measure(Gfx, form, px, weight).X;
            if (width <= room)
            {
                return (form, width);
            }
        }
        return null;
    }

    // --- The pointer --------------------------------------------------------------------------

    /// <summary>A device's row opens it, and its button ejects it; anywhere else is the widget as a whole.</summary>
    protected internal override int CardTarget(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface) =>
        Board(box, style).TargetAt(point);

    /// <summary>
    /// Only a device's row and its button light under the pointer, never the line around them (a folder pin's contents' rule, CellIsland.LightsItsRows): a press beside them does nothing.
    /// </summary>
    protected internal override bool LightsItsRows => true;

    /// <summary>
    /// The row or the button under the pointer lights, as a row and a button of the flyout did; the
    /// line asks for its redraw, which the folder makes.
    /// </summary>
    protected internal override void CardHover(Vector2? point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var target = point is { } at ? Board(box, style).TargetAt(at) : NoTarget;
        var root = Device(target)?.Root ?? "";
        var let = press.Over(target);
        if (target == hot && root == hotRoot && !let)
        {
            return;
        }
        (hot, hotRoot) = (target, root);
        _ = Wake();
    }

    /// <summary>A press on a device's row or its button lights it pressed; the press stays the folder's, which makes it the click (RowPress).</summary>
    protected internal override void CardDown(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        if (press.Down(Board(box, style).TargetAt(point)))
        {
            _ = Wake();
        }
    }

    /// <summary>
    /// A press on a device's row or its button: the device opens, or is ejected — where the pointer
    /// is, and only on the device it was over.
    /// </summary>
    private IslandAction Act(int index)
    {
        if (index != hot || Device(index) is not { } device || device.Root.Length == 0
            || !string.Equals(device.Root, hotRoot, StringComparison.OrdinalIgnoreCase))
        {
            return default;
        }
        if (index >= EjectTarget)
        {
            DriveWatch.Eject(device.Root);
        }
        else
        {
            DriveWatch.Open(device.Root);
        }
        return default;
    }

    /// <summary>The device a row's or a button's target stands for, or null.</summary>
    private Drive? Device(int target)
    {
        var at = target >= EjectTarget ? target - EjectTarget : target >= OpenTarget ? target - OpenTarget : -1;
        return (uint)at < (uint)devices.Count ? devices[at] : null;
    }

    private static bool Inside(D2D_RECT_F rect, Vector2 point) =>
        point.X >= rect.left && point.X < rect.right && point.Y >= rect.top && point.Y < rect.bottom;

    // --- The layout's pieces ------------------------------------------------------------------

    /// <summary>
    /// A line's devices, laid out (<see cref="Board"/>): what it is drawn from and what a press is
    /// tested against. Rows are in <see cref="Devices"/>' order, from the first.
    /// </summary>
    internal sealed class DeviceBoard
    {
        public List<DeviceRow> Rows { get; } = [];

        /// <summary>Nothing plugged in: the glyph, quiet, and the words saying so.</summary>
        public BoardEmpty? Empty { get; set; }

        /// <summary>What a press at <paramref name="point"/> is: a device's button, its row, or nothing of the line's own.</summary>
        public int TargetAt(Vector2 point)
        {
            foreach (var row in Rows)
            {
                if (Inside(row.Eject, point))
                {
                    return EjectTarget + row.Index;
                }
                if (Inside(row.Box, point))
                {
                    return OpenTarget + row.Index;
                }
            }
            return NoTarget;
        }
    }

    /// <summary>
    /// One device's row: its band (LineItem), what the pointer lights, the picture's box and the glyph's size in it,
    /// what its row shows (the kit's row, §401), and the eject button.
    /// </summary>
    internal readonly record struct DeviceRow(
        int Index,
        D2D_RECT_F Box,
        D2D_RECT_F Icon,
        float GlyphPx,
        RowSpec Spec,
        D2D_RECT_F Eject);

    /// <summary>A line of words, whole, with its line box's top-left.</summary>
    internal readonly record struct BoardWords(string Text, float Px, float Left, float Top, Weight Weight = Weight.Regular);

    internal readonly record struct BoardEmpty(D2D_RECT_F Glyph, float GlyphPx, IReadOnlyList<BoardWords> Words);
}
