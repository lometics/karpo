// §641: Start's apps as a line (docs\widgets.md §641): the pinned apps (startPins) and every app (startApps), one class,
// for both draw apps the one way — a tile, the app's icon over its name, as Start's grid; a row, its icon beside its name,
// as Start's list.
//
// **Pinned.** "Pinned" over a grid of tiles, as many across as the line is wide (Start's six in its width), one line of
// them at Compact, three at Full, more scrolling under the heading. Its pins are the line's (StartOptions.Pins), this PC's
// usual ones until somebody pins, unpins or moves one (StartPins). A tile opens its app once the folder has gone; its right
// click opens, runs as administrator, shows its file, unpins, moves to the front, uninstalls; a tile dragged onto another
// place moves there (the accent's bar where it goes), Ctrl+Shift and an arrow too.
//
// **All apps.** "All apps" and its view's button (Category, Grid, List: a menu, the one in force ticked, as Start's View
// dropdown) over every app: in categories, a heading and their tiles each; a grid or a list A to Z under their letters.
// "Recently added" first while an app is new (StartUses). Its right click pins to the pinned apps beside it.

using System.Numerics;
using Karpo.Core;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;

namespace Karpo.Dock.StartWidgets;

internal sealed unsafe class StartAppsLine : StartLine, IListContent, IListScroll
{
    /// <summary>All apps' view button's spot.</summary>
    private const int ViewSpot = 1;

    /// <summary>The first app's spot: after the view button's.</summary>
    private const int FirstApp = 2;

    private readonly bool pins;
    private IReadOnlyList<string> pinned = [];
    private bool pinsBeside;

    // A tile carried (Pinned): what, from where, to where, whether it has moved yet.
    private StartApp? carried;
    private Vector2 carriedFrom;
    private bool carrying;
    private int carriedTo = -1;

    public StartAppsLine(string key, WidgetItem island, Gfx gfx, Config config)
        : base(key, island, gfx, config)
    {
        pins = Info == WidgetInfos.StartPins;
        Configure(island, config);
    }

    protected override void Configured(Config config)
    {
        // Whether a pinned apps line stands in this folder, which All apps' "Pin to Start" pins to.
        var owner = Islands.OwnerOf(Key);
        var id = Islands.IdOf(Key);
        pinsBeside = pins || StartEdits.PinsOf(config, owner, id) is not null;
        pinned = (pins ? Settings : StartEdits.PinsOf(config, owner, id))?.Start?.Pins ?? (IReadOnlyList<string>)[];
        pinnedDefault = (pins ? Settings : StartEdits.PinsOf(config, owner, id))?.Start?.Pins is null;
    }

    private bool pinnedDefault = true;

    /// <summary>The pins as they stand: the line's, or this PC's usual ones.</summary>
    internal IReadOnlyList<string> Pins => pinnedDefault ? StartPins.Defaults(Source.Apps) : pinned;

    /// <summary>All apps' view.</summary>
    internal StartView View => Settings.Start?.ViewShown ?? StartView.Category;

    /// <summary>The size it shows (Pinned: one line of tiles or three).</summary>
    private LineSize Size => LineSizes.Of(Settings);

    // --- What it shows --------------------------------------------------------------------------------

    /// <summary>What it lists now, run by run: a heading (none for none), then its apps as tiles or as rows.</summary>
    internal List<(string? Head, List<StartApp> Apps, bool Tiles)> Runs()
    {
        var apps = Source.Apps;
        if (pins)
        {
            return [(null, StartPins.Apps(Pins, apps), true)];
        }
        List<(string?, List<StartApp>, bool)> runs = [];
        var now = NowUtc;
        if (apps.Where(app => Source.Uses.IsNew(app.Target, now)).OrderByDescending(app => Source.Uses.Of(app.Target)?.SeenUtc).Take(3).ToList() is { Count: > 0 } added)
        {
            runs.Add((S.DockStartRecentlyAdded, added, View != StartView.List));
        }
        switch (View)
        {
            case StartView.Category:
                runs.AddRange(StartCategories.Group(apps).Select(group => ((string?)CategoryName(group.Category), group.Apps, true)));
                break;
            default:
                runs.AddRange(StartLetters.Group(apps).Select(group => ((string?)group.Head, group.Apps, View == StartView.Grid)));
                break;
        }
        return runs;
    }

    /// <summary>A category's name, Start's own.</summary>
    internal static string CategoryName(StartCategory category) => category switch
    {
        StartCategory.Productivity => S.DockStartCategoryProductivity,
        StartCategory.Creativity => S.DockStartCategoryCreativity,
        StartCategory.Social => S.DockStartCategorySocial,
        StartCategory.Entertainment => S.DockStartCategoryEntertainment,
        StartCategory.Games => S.DockStartCategoryGames,
        StartCategory.Utilities => S.DockStartCategoryUtilities,
        _ => S.WidgetScreenTimeOther,
    };

    /// <summary>A view's name.</summary>
    internal static string ViewName(StartView view) => view switch
    {
        StartView.Grid => S.SettingsAppearanceStylesPatternGrid,
        StartView.List => S.SettingsPinViewList,
        _ => S.DockStartViewCategory,
    };

    // --- Sizes ----------------------------------------------------------------------------------------

    private readonly record struct Sizes(float Head, float Inset, int Across, float TileTall, float Icon, float RowTall, float RowIcon, float Sub, float Pad);

    private Sizes SizesOf(float width, in TileStyle style)
    {
        var form = ListForm.Of(Gfx, style);
        var inner = MathF.Max(1, width - (2 * form.Inset));
        var across = Math.Clamp((int)(inner / style.Px(72)), 3, 8);
        var name = TilePaint.LineHeight(Gfx, style.Text(12));
        return new Sizes(
            Head: PopupPaint.Round(style.Text(40)),
            Inset: form.Inset,
            Across: across,
            TileTall: PopupPaint.Round(style.Px(12) + style.Px(32) + style.Px(8) + name + style.Px(10)),
            Icon: style.Px(32),
            RowTall: PopupPaint.Round(MathF.Max(style.Px(40), TilePaint.LineHeight(Gfx, style.Text(14)) + style.Px(16))),
            RowIcon: style.Px(24),
            Sub: PopupPaint.Round(style.Text(32)),
            Pad: style.Px(8));
    }

    /// <summary>How tall all of it is at <paramref name="width"/>: its heading and every run.</summary>
    private float Content(float width, in TileStyle style)
    {
        var sizes = SizesOf(width, style);
        var height = sizes.Head;
        var runs = Runs();
        if (runs.All(run => run.Apps.Count == 0))
        {
            return height + sizes.RowTall + sizes.Pad;
        }
        foreach (var (head, apps, tiles) in runs)
        {
            height += head is null ? 0 : sizes.Sub;
            height += tiles ? (int)Math.Ceiling(apps.Count / (double)sizes.Across) * sizes.TileTall : apps.Count * sizes.RowTall;
        }
        return height + sizes.Pad;
    }

    /// <summary>Pinned: its form — the heading and one line of tiles or three; All apps: all of it.</summary>
    public float ListHeight(float width, in TileStyle style)
    {
        if (!pins)
        {
            return Content(width, style);
        }
        var sizes = SizesOf(width, style);
        return sizes.Head + ((Size == LineSize.Compact ? 1 : 3) * sizes.TileTall) + sizes.Pad;
    }

    public float ScrollHeight(float width, float height, in TileStyle style) => MathF.Max(height, Content(width, style));

    public float ScrollHead(float width, float content, in TileStyle style) => SizesOf(width, style).Head;

    // --- Laid out --------------------------------------------------------------------------------------

    public override string MainPiece => "heading";

    protected override List<Spot> Lay(D2D_RECT_F box, in TileStyle style)
    {
        var sizes = SizesOf(box.right - box.left, style);
        var left = box.left + sizes.Inset;
        var right = box.right - sizes.Inset;
        var head = new D2D_RECT_F(left, box.top, right, box.top + sizes.Head);
        List<Spot> spots = [new(0, head, "heading", pins ? S.DockUiaPinned : S.SettingsIconPickerIconAllApps, Header: true)];
        if (!pins)
        {
            // The view's button at the heading's end: its name and a chevron, Start's "View" dropdown.
            var words = ViewName(View);
            var wide = PopupPaint.Measure(Gfx, words, style.Text(12), Weight.Regular).X + style.Px(12 + 8 + 12 + 12);
            var tall = style.Px(28);
            var mid = head.top + ((head.bottom - head.top) / 2);
            spots.Add(new(ViewSpot, new D2D_RECT_F(right - wide, PopupPaint.Round(mid - (tall / 2)), right, PopupPaint.Round(mid + (tall / 2))), "view", S.DockStartViewNamed(words)));
        }
        var y = box.top + sizes.Head;
        var id = FirstApp;
        var runs = Runs();
        if (runs.All(run => run.Apps.Count == 0))
        {
            spots.Add(new(0, new D2D_RECT_F(left, y, right, y + sizes.RowTall), "nothing", pins ? S.DockStartNoPins : ""));
            return spots;
        }
        foreach (var (title, apps, tiles) in runs)
        {
            if (title is not null)
            {
                spots.Add(new(0, new D2D_RECT_F(left, y, right, y + sizes.Sub), "section", title, Header: true));
                y += sizes.Sub;
            }
            // §661: the kit's items, edge to edge: tiles a grid of the line's whole width (LineItem.Grid), rows its bands.
            var cells = tiles ? LineItem.Grid(box, y, apps.Count, sizes.Across, sizes.TileTall) : LineItem.Down(box with { top = y, bottom = y + (apps.Count * sizes.RowTall) }, apps.Count);
            for (var index = 0; index < apps.Count; index++)
            {
                var app = apps[index];
                spots.Add(new(id++, cells[index], (tiles ? "tile " : "row ") + app.Name, app.Name, app, app.Target));
            }
            y += tiles ? (int)Math.Ceiling(apps.Count / (double)sizes.Across) * sizes.TileTall : apps.Count * sizes.RowTall;
        }
        return spots;
    }

    // --- Clicks and menus -------------------------------------------------------------------------------

    /// <summary>An app opens once the folder has gone; the view's button opens its menu.</summary>
    public override IslandAction Click(int index)
    {
        _ = press.Up();
        if (index == ViewSpot && !pins)
        {
            return MenuOf(ViewSpot);
        }
        return SpotOf(index) is { App: { } app } ? Leave(new StartAct(StartActKind.Open, app.Target)) : default;
    }

    internal override IReadOnlyList<StartMenuRow> MenuRows()
    {
        if (!pins && (menuSpot == ViewSpot || (menuSpot == 0 && hot == ViewSpot)))
        {
            return [.. Enum.GetValues<StartView>().Select(view => new StartMenuRow(ViewName(view), view switch
            {
                StartView.Grid => Glyphs.Grid,
                StartView.List => Glyphs.List,
                _ => Glyphs.Category,
            }, new StartEdit(StartEdits.View, view.ToString()).Value, Edit: true, Ticked: view == View))];
        }
        var spot = MenuSpot ?? SpotOf(KeyedPart);
        if (spot is not { App: { } app })
        {
            return [];
        }
        var isPinned = Pins.Contains(app.Target, StringComparer.OrdinalIgnoreCase);
        return AppRows(app, isPinned && pinsBeside, pinsBeside, moves: pins);
    }

    // --- A tile carried into another place (Pinned) ----------------------------------------------------

    /// <summary>A press on a pinned tile is the line's: a drag carries the tile, a click without one opens it.</summary>
    protected internal override bool CardPress(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        CardDown(point, box, style, surface);
        if (!pins || Laid(box, style).FirstOrDefault(spot => spot.Id > 0 && LineRows.Inside(spot.Box, point)) is not { App: { } app })
        {
            return false;
        }
        (carried, carriedFrom, carrying, carriedTo) = (app, point, false, -1);
        return true;
    }

    protected internal override void CardDrag(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        if (carried is null)
        {
            return;
        }
        carrying |= Vector2.Distance(point, carriedFrom) >= style.Px(6);
        if (carrying)
        {
            carriedTo = DropIndex(point, box, style);
            _ = Wake();
        }
    }

    /// <summary>The place among the pins shown a tile let go at <paramref name="point"/> goes to: before the tile it is on, or after where it is on its right half.</summary>
    private int DropIndex(Vector2 point, D2D_RECT_F box, in TileStyle style)
    {
        var tiles = Laid(box, style).Where(spot => spot.App is not null).ToList();
        var best = tiles.Count;
        var nearest = float.MaxValue;
        for (var index = 0; index < tiles.Count; index++)
        {
            var at = tiles[index].Box;
            var centre = new Vector2((at.left + at.right) / 2, (at.top + at.bottom) / 2);
            var distance = Vector2.Distance(centre, point);
            if (distance < nearest)
            {
                nearest = distance;
                best = point.X > centre.X ? index + 1 : index;
            }
        }
        return best;
    }

    protected internal override void CardRelease()
    {
        var app = carried;
        var moved = carrying;
        var to = carriedTo;
        (carried, carrying, carriedTo) = (null, false, -1);
        _ = press.Up();
        _ = Wake();
        if (app is null)
        {
            return;
        }
        if (!moved)
        {
            // A click: the app opens, everything of Karpo's closing first (Launch).
            Run(new StartAct(StartActKind.Open, app.Target));
            return;
        }
        var from = Pins.ToList().FindIndex(pin => string.Equals(pin, app.Target, StringComparison.OrdinalIgnoreCase));
        // The place counted without the tile carried.
        var index = from >= 0 && to > from ? to - 1 : to;
        if (index >= 0 && index != from)
        {
            Edit(new StartEdit(StartEdits.Move, app.Target, index));
        }
    }

    /// <summary>Escape while a tile is carried puts it back.</summary>
    protected internal override bool CardEscape()
    {
        if (carried is null)
        {
            return false;
        }
        (carried, carrying, carriedTo) = (null, false, -1);
        _ = Wake();
        return true;
    }

    internal override bool Gripping => carrying;

    /// <summary>Ctrl+Shift and Left or Right moves the pinned tile the keys are on a place along, as Start's Alt+Shift does.</summary>
    protected internal override bool CardKey(int key, WidgetSurface surface)
    {
        if (!pins || key is not (VK.VK_LEFT or VK.VK_RIGHT) || !KeyState.Down(VK.VK_CONTROL) || !KeyState.Down(VK.VK_SHIFT)
            || SpotOf(KeyedPart) is not { App: { } app })
        {
            return false;
        }
        var list = Pins.ToList();
        var from = list.FindIndex(pin => string.Equals(pin, app.Target, StringComparison.OrdinalIgnoreCase));
        var to = from + (key == VK.VK_RIGHT ^ Mirror.On ? 1 : -1);
        if (from < 0 || to < 0 || to >= list.Count)
        {
            return false;
        }
        Edit(new StartEdit(StartEdits.Move, app.Target, to));
        return true;
    }

    // --- Drawn ------------------------------------------------------------------------------------------

    protected internal override void PaintCard(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var spots = Laid(box, style);
        var sizes = SizesOf(box.right - box.left, style);
        var theme = style.Theme;
        foreach (var spot in spots)
        {
            switch (spot)
            {
                case { Name: "heading" }:
                    Heading(dc, spot.Said, spot.Box, style, spot.Box.right - spot.Box.left - (pins ? 0 : style.Px(120)));
                    break;
                case { Id: ViewSpot } when !pins:
                {
                    // A subtle button: its fill under the pointer, its name and its chevron.
                    var lit = hot == ViewSpot;
                    PopupPaint.Fill(dc, spot.Box, style.Px(4), PopupPaint.Ink(lit ? (press.On(ViewSpot) ? theme.SubtlePressed : theme.SubtleHover) : theme.ControlFill));
                    var px = style.Text(12);
                    var tall = TilePaint.LineHeight(Gfx, px);
                    var words = ViewName(View);
                    _ = TilePaint.Line(dc, Gfx, words, px, Weight.Regular, theme.Text, spot.Box.left + style.Px(12), PopupPaint.Round(spot.Box.top + ((spot.Box.bottom - spot.Box.top - tall) / 2)), spot.Box.right - spot.Box.left);
                    var chevron = style.Px(12);
                    PopupPaint.Glyph(dc, Gfx, Glyphs.Chevron, style.Text(10), theme.TextSecondary,
                        PopupPaint.Rect(spot.Box.right - style.Px(12) - chevron, spot.Box.top, chevron, spot.Box.bottom - spot.Box.top));
                    break;
                }
                case { Name: "nothing" }:
                    Nothing(dc, spot.Said, spot.Box, style);
                    break;
                case { Name: "section" }:
                {
                    var px = style.Text(12);
                    var tall = TilePaint.LineHeight(Gfx, px, Weight.Semibold);
                    _ = TilePaint.Line(dc, Gfx, spot.Said, px, Weight.Semibold, theme.TextSecondary, spot.Box.left, PopupPaint.Round(spot.Box.top + ((spot.Box.bottom - spot.Box.top - tall) / 2)), spot.Box.right - spot.Box.left);
                    break;
                }
                case { App: { } app }:
                    if (carrying && carried == app)
                    {
                        // The tile carried stands faint where it was.
                        LineItem.Light(dc, spot.Box, theme, pressed: false);
                        break;
                    }
                    Plate(dc, spot, style);
                    if (spot.Name.StartsWith("tile ", StringComparison.Ordinal))
                    {
                        Tile(dc, app, spot.Box, box, sizes, style);
                    }
                    else
                    {
                        Row(dc, app, spot.Box, sizes, style);
                    }
                    break;
            }
        }
        if (carrying && carriedTo >= 0)
        {
            Bar(dc, spots.Where(spot => spot.App is not null).Select(spot => spot.Box).ToList(), style);
        }
    }

    /// <summary>A tile: the icon in its cell's upper middle, the name centred under it, one line cut short, inside the gutters.</summary>
    private void Tile(ID2D1DeviceContext* dc, StartApp app, D2D_RECT_F at, D2D_RECT_F line, in Sizes sizes, in TileStyle style)
    {
        var mid = (at.left + at.right) / 2;
        var top = at.top + style.Px(12);
        Icon(dc, app.Target, Glyphs.App, PopupPaint.Rect(PopupPaint.Round(mid - (sizes.Icon / 2)), top, sizes.Icon, sizes.Icon), style);
        var (left, right) = LineItem.Words(at, line.left, line.right, sizes.Inset, style.Px(4));
        _ = TilePaint.Centred(dc, Gfx, app.Name, style.Text(12), Weight.Regular, style.Theme.Text, left, top + sizes.Icon + style.Px(8), MathF.Max(1, right - left));
    }

    /// <summary>A row: its band edge to edge, the icon on the gutter, the name after it.</summary>
    private void Row(ID2D1DeviceContext* dc, StartApp app, D2D_RECT_F at, in Sizes sizes, in TileStyle style)
    {
        var tall = at.bottom - at.top;
        var icon = PopupPaint.Rect(at.left + sizes.Inset, PopupPaint.Round(at.top + ((tall - sizes.RowIcon) / 2)), sizes.RowIcon, sizes.RowIcon);
        Icon(dc, app.Target, Glyphs.App, icon, style);
        var px = style.Text(14);
        var line = TilePaint.LineHeight(Gfx, px);
        var left = icon.right + style.Px(12);
        _ = TilePaint.Line(dc, Gfx, app.Name, px, Weight.Regular, style.Theme.Text, left, PopupPaint.Round(at.top + ((tall - line) / 2)), at.right - left - sizes.Inset);
    }

    /// <summary>Where a carried tile goes: the accent's bar before the tile at that place, or after the last.</summary>
    private void Bar(ID2D1DeviceContext* dc, List<D2D_RECT_F> tiles, in TileStyle style)
    {
        if (tiles.Count == 0)
        {
            return;
        }
        var at = carriedTo < tiles.Count ? tiles[carriedTo] : tiles[^1];
        var x = carriedTo < tiles.Count ? at.left : at.right;
        var thick = MathF.Max(2, style.Px(3));
        PopupPaint.Fill(dc, new D2D_RECT_F(x - (thick / 2), at.top + style.Px(8), x + (thick / 2), at.bottom - style.Px(8)), thick / 2, PopupPaint.Ink(style.Theme.Accent));
    }

    /// <summary>tests: a tile carried from <paramref name="from"/> and let go at <paramref name="to"/>, as the folder hands the pointer on.</summary>
    internal void Carry(Vector2 from, Vector2 to, D2D_RECT_F box, in TileStyle style)
    {
        if (CardPress(from, box, style, WidgetSurface.List))
        {
            CardDrag(to, box, style, WidgetSurface.List);
            CardRelease();
        }
    }
}
