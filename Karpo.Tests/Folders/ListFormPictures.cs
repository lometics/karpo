// What the list forms' tests share: a list folder of the lines being shown, the plan the open folder
// would make of it (its lines measured through IListContent), the ink a line puts down, and a sheet
// of such panels side by side — at 100 % and at the largest text Windows offers — drawn by the
// folder's own painters, the same calls the open folder makes.

using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using static Karpo.Tests.Islands.Offscreen;

namespace Karpo.Tests.Folders;

internal static unsafe class ListFormPictures
{
    public const string Owner = "layout:default";

    /// <summary>The two text sizes every list form is shown and measured at.</summary>
    public static readonly float[] TextScales = [1f, 2.25f];

    public static Look LookFor(bool dark, float textScale = 1f) => new(
        Glass: false,
        Dark: dark,
        Accent: Windows.UI.Color.FromArgb(255, 0, 0x78, 0xD4))
    { TextScale = textScale };

    /// <summary>A widget of <paramref name="kind"/> made for a list's line, at the line's own measurement.</summary>
    public static CellItem Line(ListKind kind, Action<CellItem>? set = null)
    {
        var island = kind.New();
        set?.Invoke(island);
        island.Normalize();
        return island;
    }

    /// <summary>A list folder holding <paramref name="lines"/>, in that order, opened.</summary>
    public static FolderIsland Folder(Gfx gfx, params CellItem[] lines) => Folder(gfx, _ => { }, lines);

    /// <summary>
    /// A list folder holding <paramref name="lines"/>, in that order, opened — with
    /// <paramref name="before"/> run on it first, for a stand-in a line must have before its first
    /// look: opening a folder is a look (docs\widgets.md §11), and a weather line looked at without
    /// its own source goes and asks the shared store for its place.
    /// </summary>
    public static FolderIsland Folder(Gfx gfx, Action<FolderIsland> before, params CellItem[] lines) => Folder(gfx, new Config(), before, lines);

    /// <summary>The same, configured by <paramref name="config"/>: its dividers' look among other things (§478: its layout's style's Folder separators).</summary>
    public static FolderIsland Folder(Gfx gfx, Config config, Action<FolderIsland> before, params CellItem[] lines)
    {
        var holder = BarKind.FolderList.New();
        foreach (var line in lines)
        {
            Assert.NotNull(Karpo.Core.Folders.Add(BarKind.FolderList, holder.Folder!, line));
        }
        var folder = new FolderIsland($"{Owner}/{holder.Id}", holder, gfx, config);
        before(folder);
        folder.Opened(gfx);
        return folder;
    }

    /// <summary>What the open folder would make of <paramref name="folder"/>: each line's content, and the plan.</summary>
    public static (float[] Natural, FolderPlan Plan) Plan(FolderIsland folder, Look look, float scale, bool editing = false)
    {
        var natural = FolderFlyout.Natural(folder.Tiles, folder.Laid, scale, look);
        // A work area tall enough for every line: a sheet shows the whole folder, none of it left out;
        // its Fill lines fill to a 1440p screen's room over a 48-DIP bar (§173).
        var room = FlyoutPlacement.Room(1440 - PopupPaint.Round(48 * scale), scale);
        var plan = FolderFlyout.Build(look, scale, folder.View, folder.Laid, [.. folder.Tiles.Select(tile => tile.Cell)], 100_000, editing: editing, natural: natural, room: room);
        return (natural, plan);
    }

    /// <summary>The height line <paramref name="index"/> of <paramref name="plan"/> was given.</summary>
    public static float Given(FolderPlan plan, int index) => plan.Cards[index].bottom - plan.Cards[index].top;

    /// <summary>n rows of the list in px, as the plan gives a line a height: two cells each (§442).</summary>
    public static float Rows(int rows, float scale, float textScale) => Cells(rows * LineHeight.Row, scale, textScale);

    /// <summary>n of the folder's cells in px, as the plan gives a line a height: n times a cell, exactly (§442).</summary>
    public static float Cells(int cells, float scale, float textScale) => cells * FolderFlyout.CellPx(scale, textScale);

    /// <summary>
    /// Where the line at <paramref name="index"/> puts its ink when drawn alone into a box of the
    /// height it was given: the box around every pixel it inked, in its own px.
    /// </summary>
    public static (float Left, float Top, float Right, float Bottom) Ink(FolderIsland folder, FolderPlan plan, int index, Look look, float scale)
    {
        var card = plan.Cards[index];
        var width = card.right - card.left;
        var height = card.bottom - card.top;
        var style = FolderFlyout.StyleFor(scale, look, FlyoutTheme.For(look));
        var line = (CellIsland)folder.Tiles[index].Island;
        (float, float, float, float) ink = default;
        Bitmap(width, height,
            dc => line.PaintOn(dc, PopupPaint.Rect(0, 0, width, height), style, WidgetSurface.List),
            bitmap => ink = InkOf(bitmap, (uint)MathF.Ceiling(width), (uint)MathF.Ceiling(height)));
        return ink;
    }

    /// <summary>
    /// The panels side by side, each under its caption, on a backdrop and in the rounded plates the
    /// real panel has — each list's cards and the title's row (<see cref="FolderPlan.Pieces"/>), each
    /// line clipped to its card — each drawn at its own look, so one sheet shows a line at 100 % and
    /// at 225 % text beside each other.
    /// </summary>
    /// <param name="under">A line lit as the open folder lights the one under the pointer: its index and the fill, drawn under it.</param>
    public static void Sheet(Gfx gfx, string file, float scale, IReadOnlyList<(string Caption, Look Look, FolderIsland Folder, FolderPlan Plan)> panels, (int Index, DXGI_RGBA Ink)? under = null)
    {
        const float margin = 24;
        var caption = PopupPaint.Round(14 * scale);
        var captionBand = caption * 3;
        var width = margin + panels.Sum(panel => panel.Plan.Width + margin);
        var height = margin + captionBand + panels.Max(panel => panel.Plan.Height) + margin;
        var dark = panels[0].Look.Dark;
        Bitmap(width, height, dc =>
        {
            var backdrop = dark ? new DXGI_RGBA(0.20f, 0.24f, 0.30f, 1) : new DXGI_RGBA(0.72f, 0.78f, 0.86f, 1);
            dc->Clear(&backdrop);
            var ink = dark ? new DXGI_RGBA(1, 1, 1, 0.9f) : new DXGI_RGBA(0, 0, 0, 0.9f);
            var left = margin;
            foreach (var (words, look, folder, plan) in panels)
            {
                var theme = FlyoutTheme.For(look);
                var colon = words.IndexOf(": ", StringComparison.Ordinal);
                var (first, second) = colon < 0 ? (words, "") : (words[..colon], words[(colon + 2)..]);
                PopupPaint.Text(dc, gfx, first, caption, Weight.Semibold, ink, left, margin, plan.Width);
                PopupPaint.Text(dc, gfx, second, caption, Weight.Regular, ink, left, margin + (caption * 1.4f), plan.Width);
                var box = new D2D_RECT_F(left, margin + captionBand, left + plan.Width, margin + captionBand + plan.Height);
                foreach (var piece in plan.Pieces)
                {
                    var plate = new D2D_RECT_F(box.left + piece.left, box.top + piece.top, box.left + piece.right, box.top + piece.bottom);
                    PopupPaint.Fill(dc, plate, plan.Radius, PopupPaint.Ink(theme.Base));
                    PopupPaint.Stroke(dc, plate, plan.Radius, MathF.Max(1, MathF.Round(scale)), PopupPaint.Ink(theme.Stroke));
                }
                var context = dc;
                var at = (box.left, box.top);
                Shifted(context, at.left, at.top, () => Panel(context, gfx, folder, plan, look, theme, scale, under));
                left += plan.Width + margin;
            }
        }, bitmap => Save(bitmap, Path.Combine(Pictures, file)));
    }

    internal static void Panel(ID2D1DeviceContext* dc, Gfx gfx, FolderIsland folder, FolderPlan plan, Look look, FlyoutTheme theme, float scale, (int Index, DXGI_RGBA Ink)? under = null)
    {
        FolderFlyout.PaintEmpty(dc, gfx, plan, theme);
        if (under is { } lit && lit.Index >= 0 && lit.Index < plan.Cards.Count)
        {
            PopupPaint.Fill(dc, plan.Cards[lit.Index], plan.Radius, lit.Ink);
        }
        for (var index = 0; index < plan.Cards.Count && index < folder.Tiles.Count; index++)
        {
            if (!plan.Shows(index))
            {
                continue;
            }
            var card = plan.Cards[index];
            var box = PopupPaint.Rect(0, 0, card.right - card.left, card.bottom - card.top);
            var style = FolderFlyout.StyleFor(scale, look, theme);
            var tile = folder.Tiles[index];
            var context = dc;
            // Clipped to the card it stands on, as the open folder clips it (a divider stands on none).
            var list = plan.Lists[plan.CardList[index]];
            var run = index < plan.CardRun.Count ? plan.CardRun[index] : 0;
            var on = run >= 0 && run < list.Runs.Count ? list.Runs[run] : list.Box;
            PopupPaint.Clipped(context, on, plan.Radius, () => Shifted(context, card.left, card.top, () =>
            {
                if (tile.Cell.Island.List?.CardInList() != true)
                {
                    FolderFlyout.PaintListLine(context, gfx, tile.Island, tile.Cell.Island, box, plan, theme, scale);
                }
                else
                {
                    FolderFlyout.PaintContent(context, tile.Island, box, style);
                }
            }));
        }
        // §524: an arranged spacer's stripes are the compositor's (SpacerStripes), pictured in a window
        // (SpacerStripesTests); offscreen a spacer is its empty room.
    }
}
