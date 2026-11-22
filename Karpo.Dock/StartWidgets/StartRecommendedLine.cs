// §641: Start's Recommended as a line (docs\widgets.md §641): "Recommended" over the files opened lately (Windows' Recent
// folder) and the apps added lately (first seen by Karpo within a week), newest first, two across as Start has them, a
// row each — the icon, the name, and under it when it was opened (the time today, the day this week, else the date, in
// the culture's own words) or "Recently added". Compact one line of two, Full three. What it lists is set on its page
// (Recent files, Recently added apps). A click opens it once the folder has gone; its right click opens it, shows it in
// File Explorer, pins an app beside.

using Karpo.Core;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock.StartWidgets;

internal sealed unsafe class StartRecommendedLine : StartLine, IListContent, IListScroll
{
    private const int First = 1;

    private bool pinsBeside;
    private IReadOnlyList<string>? pinned;

    public StartRecommendedLine(string key, WidgetItem island, Gfx gfx, Config config)
        : base(key, island, gfx, config) => Configure(island, config);

    protected override void Configured(Config config)
    {
        var line = StartEdits.PinsOf(config, Islands.OwnerOf(Key), Islands.IdOf(Key));
        pinsBeside = line is not null;
        pinned = line?.Start?.Pins;
    }

    /// <summary>What it lists now, newest first.</summary>
    internal List<StartRecommendation> Items()
    {
        var now = NowUtc;
        var added = Source.Apps
            .Where(app => Source.Uses.IsNew(app.Target, now))
            .Select(app => new StartRecommendation(app.Name, app.Target, Source.Uses.Of(app.Target)!.Value.SeenUtc, App: true));
        return StartRecommendations.Of(Settings.Start, Source.Files, added);
    }

    private LineSize Size => LineSizes.Of(Settings);

    private readonly record struct Sizes(float Head, float Inset, float Row, float Icon, float Pad);

    private Sizes SizesOf(in TileStyle style)
    {
        var form = ListForm.Of(Gfx, style);
        var two = TilePaint.LineHeight(Gfx, style.Text(14)) + TilePaint.LineHeight(Gfx, style.Text(12));
        return new Sizes(PopupPaint.Round(style.Text(40)), form.Inset, PopupPaint.Round(MathF.Max(style.Px(50), two + style.Px(14))), style.Px(32), style.Px(6));
    }

    public float ListHeight(float width, in TileStyle style)
    {
        var sizes = SizesOf(style);
        return sizes.Head + ((Size == LineSize.Compact ? 1 : 3) * sizes.Row) + sizes.Pad;
    }

    public float ScrollHeight(float width, float height, in TileStyle style)
    {
        var sizes = SizesOf(style);
        return MathF.Max(height, sizes.Head + (Math.Max(1, (Items().Count + 1) / 2) * sizes.Row) + sizes.Pad);
    }

    public float ScrollHead(float width, float content, in TileStyle style) => SizesOf(style).Head;

    public override string MainPiece => "heading";

    protected override List<Spot> Lay(D2D_RECT_F box, in TileStyle style)
    {
        var sizes = SizesOf(style);
        var left = box.left + sizes.Inset;
        var right = box.right - sizes.Inset;
        List<Spot> spots = [new(0, new D2D_RECT_F(left, box.top, right, box.top + sizes.Head), "heading", S.DockStartRecommended, Header: true)];
        var items = Items();
        var top = box.top + sizes.Head;
        if (items.Count == 0)
        {
            spots.Add(new(0, new D2D_RECT_F(left, top, right, top + sizes.Row), "nothing", S.DockStartNoRecommended));
            return spots;
        }
        // §661: two across, the kit's items edge to edge: the line's whole width in two cells a row, the rows touching.
        var cells = LineItem.Grid(box, top, items.Count, 2, sizes.Row);
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            spots.Add(new(First + index, cells[index], "item " + item.Name, item.Name, Target: item.Target));
        }
        return spots;
    }

    /// <summary>The item, by its spot.</summary>
    private StartRecommendation? ItemOf(int id) => id >= First && id - First < Items().Count ? Items()[id - First] : null;

    public override IslandAction Click(int index)
    {
        _ = press.Up();
        return ItemOf(index) is { } item ? Leave(new StartAct(StartActKind.Open, item.Target)) : default;
    }

    internal override IReadOnlyList<StartMenuRow> MenuRows()
    {
        if (ItemOf(menuSpot != 0 ? menuSpot : hot != 0 ? hot : KeyedPart) is not { } item)
        {
            return [];
        }
        if (item.App)
        {
            var pins = pinned ?? StartPins.Defaults(Source.Apps);
            return AppRows(new StartApp(item.Name, item.Target), pins.Contains(item.Target, StringComparer.OrdinalIgnoreCase) && pinsBeside, pinsBeside, moves: false);
        }
        return
        [
            new(S.DockWidgetMenuOpen, Glyphs.Open, new StartAct(StartActKind.Open, item.Target).Value),
            new(S.DockWidgetMenuOpenFileLocation, Glyphs.Location, new StartAct(StartActKind.Location, item.Target).Value),
        ];
    }

    /// <summary>When an item came: "Recently added" for an app, when it was opened for a file.</summary>
    internal string WhenOf(StartRecommendation item) =>
        item.App ? S.DockStartRecentlyAdded : StartRecommendations.When(item.WhenUtc, NowUtc, Panels.Nls.Time, Panels.Nls.DayName, Panels.Nls.ShortDate);

    protected internal override void PaintCard(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var spots = Laid(box, style);
        var sizes = SizesOf(style);
        var items = Items();
        var theme = style.Theme;
        foreach (var spot in spots)
        {
            if (spot.Name == "heading")
            {
                Heading(dc, spot.Said, spot.Box, style, spot.Box.right - spot.Box.left);
                continue;
            }
            if (spot.Name == "nothing")
            {
                Nothing(dc, spot.Said, spot.Box, style);
                continue;
            }
            if (spot.Id - First is var at && (at < 0 || at >= items.Count))
            {
                continue;
            }
            var item = items[spot.Id - First];
            Plate(dc, spot, style);
            var tall = spot.Box.bottom - spot.Box.top;
            // Its icon on the gutter of its cell, its words after it, ending inside the line's gutter.
            var icon = PopupPaint.Rect(spot.Box.left + sizes.Inset, PopupPaint.Round(spot.Box.top + ((tall - sizes.Icon) / 2)), sizes.Icon, sizes.Icon);
            Icon(dc, item.Target, item.App ? Glyphs.App : Glyphs.File, icon, style);
            var left = icon.right + style.Px(12);
            var room = MathF.Max(1, LineItem.Words(spot.Box, box.left, box.right, sizes.Inset, style.Px(8)).Right - left);
            var name = style.Text(14);
            var when = style.Text(12);
            var words = TilePaint.LineHeight(Gfx, name) + TilePaint.LineHeight(Gfx, when);
            var y = PopupPaint.Round(spot.Box.top + ((tall - words) / 2));
            y += TilePaint.Line(dc, Gfx, item.Name, name, Weight.Regular, theme.Text, left, y, room);
            _ = TilePaint.Line(dc, Gfx, WhenOf(item), when, Weight.Regular, theme.TextSecondary, left, y, room);
        }
    }
}
