// The current weather as a line of a folder's list (docs\widgets.md §9, §401, §590), on the kit's blocks (LineKit.cs).
// It says the same at both of its sizes — the place, the conditions' glyph, the temperature, the conditions in words,
// what it feels like with the day's high and low — and nothing more at either (§590: the person, 2026-11-09, "these
// widgets should be broken down into smaller ones that always show the same amount of data"):
//
//   Compact  the kit's row with its caption: the glyph, the conditions over the place and what it feels like, the
//            temperature at the trailing gutter
//   Full     a Hero — the glyph, the temperature in the Hero, the conditions beside it, what it feels like under it —
//            and the place a caption under that, how fresh the reading is after it (§607: no header, PlaceLabel.Foot)
//
// The next hours and the days ahead, which a taller line showed under the hero until §590, are the hourly and the
// daily forecast, widgets of their own. With no forecast yet, or none to be had, it is the kit's one Status, at its
// size's height all the same.
//
// One thing to do (docs\widgets.md §131, §370, §379): the conditions now search the weather — the whole line, lit as
// the folder lights a line that is one thing to press (FolderFlyout.LightsWhole); it is read again from "Refresh" on
// its right click (IRefreshable, §451). The place, the store and the looking are every weather line's (WeatherLineTile).

using Karpo.Core;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock.Weather;

/// <summary>The current weather as a line of a list, at its size (see the top of the file).</summary>
internal sealed unsafe class WeatherPartTile : WeatherLineTile, IListContent, ILinePieces
{
    public WeatherPartTile(string key, WidgetItem island, Gfx gfx, Config config)
        : base(key, island, gfx)
    {
        Configure(island, config);
    }

    /// <summary>
    /// §379: the line is one thing to press, not made of parts that light themselves: the folder lights it
    /// whole and its click is the line's (FolderFlyout.LightsWhole).
    /// </summary>
    protected internal override bool LightsItsRows => false;

    /// <summary>A click anywhere on the line, or Enter on it: the weather at its place searched in the browser (<see cref="WeatherLineTile.SearchTheWeb"/>).</summary>
    public override IslandAction Click(int index)
    {
        var done = base.Click(index);
        if (index == 0 && Unset is null)
        {
            Take();
            SearchTheWeb();
        }
        return done;
    }

    /// <summary>No parts of its own: the line is the one target.</summary>
    protected override void Act(int target)
    {
    }

    /// <summary>None: the keys are on the line, whose Enter is its click.</summary>
    protected override IReadOnlyList<int> KeyOrder() => [];

    /// <summary>Across the line: Left and Right.</summary>
    protected override (int Back, int On) WalkKeys => (TerraFX.Interop.Windows.VK.VK_LEFT, TerraFX.Interop.Windows.VK.VK_RIGHT);

    protected internal override void PaintCard(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        Take();
        Line(new LineSketch(dc, Gfx), box, style);
    }

    /// <summary>What a line of one row shows first: the temperature.</summary>
    public string MainPiece => "temperature";

    /// <summary>What the line lays out in <paramref name="box"/>: every piece, where it stands, what it says.</summary>
    public IReadOnlyList<LinePiece> LinePieces(D2D_RECT_F box, in TileStyle style)
    {
        Take();
        var sketch = new LineSketch(null, Gfx);
        Line(sketch, box, style);
        return sketch.Pieces;
    }

    /// <summary>Lays the line out at <paramref name="width"/> × <paramref name="height"/> px exactly as a paint would, and says what it would draw.</summary>
    internal IReadOnlyList<LinePiece> LayOut(float width, float height, in TileStyle style) => LinePieces(PopupPaint.Rect(0, 0, width, height), style);

    /// <summary>
    /// list: its size's form, whatever the reading (§590): Compact the kit's row with its caption, Full the hero with
    /// two captions, what it feels like and the place (§607). Nothing in it grows or goes with the forecast.
    /// </summary>
    public float ListHeight(float width, in TileStyle style) => LineSizes.Of(Settings) == LineSize.Compact
        ? LineRow.Height(style, caption: true)
        : LineHero.Height(Gfx, style, 2);

    /// <summary>The glyph the line stands under: the place's condition, a cloud while it waits.</summary>
    private static string GlyphOf(WeatherReading reading) => reading.State == WeatherState.Ready ? reading.Glyph : WeatherCodes.Cloud;

    /// <summary>The line on <paramref name="sketch"/> (see the top of the file).</summary>
    private void Line(LineSketch sketch, D2D_RECT_F box, in TileStyle style)
    {
        var reading = Read();
        var theme = style.Theme;
        if (reading.State != WeatherState.Ready)
        {
            LineStatus.Draw(sketch, box, style, GlyphOf(reading), reading.Problem ?? WeatherReading.LoadingText, []);
            return;
        }
        var glyph = GlyphOf(reading);
        var glyphInk = WeatherCodes.Ink(glyph, theme);
        var stale = reading.Stale ? Karpo.Dock.QuickSettings.Controls.QuickTheme.For(theme).Critical : theme.TextSecondary;
        if (LineSizes.Of(Settings) == LineSize.Compact)
        {
            LineRow.Draw(sketch, box.left, box.right, box.top, style, new RowSpec(reading.Condition)
            {
                Glyph = glyph,
                GlyphInk = glyphInk,
                Caption = PlaceLabel.Caption(reading.Place, reading.Detail),
                Reading = reading.Temperature,
                ReadingInk = reading.Stale ? stale : null,
                NamePiece = "condition",
                CaptionPiece = PlaceLabel.Piece,
                ReadingPiece = "temperature",
            });
            return;
        }
        // §607: no header; the hero, then the place a caption under it, the age after it — or why a newer forecast is
        // not there — only where the place still reads whole beside it.
        var hero = LineHero.Draw(sketch, box.left, box.right, box.top, style, glyph, reading.Temperature, reading.Condition,
            reading.Detail.Length > 0 ? [reading.Detail] : [], glyphInk: glyphInk);
        PlaceLabel.Foot(sketch, box.left, box.right, hero.Bottom - style.Px(LineSpace.GapDip), style, reading.Place,
            reading.Problem ?? reading.Updated, stale, UpdatedName);
    }
}
