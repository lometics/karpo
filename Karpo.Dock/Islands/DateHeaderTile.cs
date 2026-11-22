// Today's date as a line of a list (docs\widgets.md §80): the calendar flyout's header — today in
// the longest form that fits ("Tuesday, 22 September", else "Tue, 22/09") in semibold on the list's one row (§432: it was a 48-DIP band, a row and a bit,
// which a folder of whole rows made two), on the list's gutter. A click on it makes today the folder's day (Channels.Day), as a click
// on the flyout's header went back to today: a month, a view and the day's events beside it come
// back to today together. It is one thing to press, so the whole line is, and the folder lights it whole
// (FolderFlyout.LightsWhole; §379: it lit and took a box round the date alone, the rest of its band dead).
//
// It is not the clock flyout's header (TimeHeaderTile), the time large over the long date: this is
// the date alone, and a way back.

using System.Numerics;
using Karpo.Core;
using Karpo.Dock.Panels;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;

namespace Karpo.Dock;

internal sealed unsafe class DateHeaderTile : CellIsland, IListContent, ILinePieces
{
    public DateHeaderTile(string key, WidgetItem island, Gfx gfx, Config config)
        : base(key, island, gfx)
    {
        Configure(island, config);
    }

    /// <summary>The folder's tick at a new minute is enough: today changes at midnight.</summary>
    public override int IntervalMs => 0;

    protected override int Count => 1;

    public override void Configure(WidgetItem island, Config config)
    {
        Settings = island;
        Forget();
    }

    public override string? Tooltip(int index) => null;

    public override bool MinuteChanged(Gfx gfx) => Redraw(gfx);

    protected override bool Sample(bool everything) => false;

    internal override void PaintCell(ID2D1DeviceContext* dc, int index, in IslandStyle style, float width, float height) =>
        throw new NotSupportedException($"{Info.Id} is a list's line; on the bar the date is a folder's face.");

    /// <summary>Today in the clock's zone, the clock widget's date style's (§230): this PC's unless set.</summary>
    private DateOnly Today => DateOnly.FromDateTime(Clock.NowIn(Settings.Clock?.TimeZone));

    /// <summary>The line, anywhere on it: today is the folder's day again.</summary>
    public override IslandAction Click(int index)
    {
        Publish(Channels.Day, Today);
        return default;
    }

    /// <summary>Enter or Space on the line goes back to today, as a click does.</summary>
    protected internal override bool CardKey(int key, WidgetSurface surface)
    {
        if (key is not (VK.VK_RETURN or VK.VK_SPACE))
        {
            return false;
        }
        Publish(Channels.Day, Today);
        return true;
    }

    /// <summary>The band and the date, in the line's px.</summary>
    private readonly record struct HeaderPlan(string Date, float Px, D2D_RECT_F Band, D2D_RECT_F Text);

    /// <summary>The flyout's header at <paramref name="box"/>'s top: the list's one row (§432), the date on the gutter.</summary>
    private HeaderPlan Lay(D2D_RECT_F box, in TileStyle style)
    {
        var gutter = ListForm.Gutter(style);
        var room = MathF.Max(1, box.right - box.left - (2 * gutter));
        var px = style.Text(14);
        var today = Today;
        string[] forms = [Nls.LongDateWithoutYear(today), Nls.DayAndShortDate(today, year: false)];
        var date = forms.FirstOrDefault(form => PopupPaint.Measure(Gfx, form, px, Weight.Semibold).X <= room) ?? forms[^1];
        var band = PopupPaint.Rect(box.left, box.top, box.right - box.left, LineRows.Px(1, style));
        var size = PopupPaint.Measure(Gfx, date, px, Weight.Semibold, room);
        var text = PopupPaint.Rect(box.left + gutter, band.top + PopupPaint.Round((band.bottom - band.top - size.Y) / 2), size.X, size.Y);
        return new HeaderPlan(date, px, band, text);
    }

    public float ListHeight(float width, in TileStyle style) => LineRows.Px(1, style);

    protected internal override void PaintCard(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var plan = Lay(box, style);
        PopupPaint.Text(dc, Gfx, plan.Date, plan.Px, Weight.Semibold, style.Theme.Text, plan.Text.left, plan.Text.top, plan.Text.right - plan.Text.left + 1);
    }

    public string MainPiece => "date";

    public IReadOnlyList<LinePiece> LinePieces(D2D_RECT_F box, in TileStyle style)
    {
        var plan = Lay(box, style);
        return [new("date", plan.Text, plan.Date)];
    }
}
