// The markets face (Karpo.Core\FolderFaces.cs, docs\widgets.md §246): what a markets widget shows on the bar,
// for what it tracks of its own (FolderFace.Market), never a line's — a folder may hold any markets lines
// beside it.
//
//  - The ticker (the full style), as the stock entry on the bar drew itself (§98): "MSFT 412.34 +1.2%" on
//    one row across a bar, the name quiet and heavier, the price in the text's ink, the change in the up or
//    down colour, the chart faded behind them over the whole button; the name over the price down a bar on
//    its side. The compact style is the name and the price alone: no change, no chart.
//  - The converter (a currency's converter style), as the currency face did (§242): "100 EUR = 94.12 CHF",
//    the amount and its currency quiet, the worth in the text's ink; down a bar on its side, the currency
//    over the worth.
//
// It is as wide as what it says now (§245), its figures tabular, so a price moving through the day keeps
// its width and only a figure more or fewer moves the bar. A price three refreshes past due, or left standing by a failure, is in
// the quiet ink. A share with no symbol is the kind's glyph and "Set up" (SetupFace.OnBar), the glyph alone
// down a bar on its side. The store's news paints it again in place (FolderIsland.MarketReading).

using Karpo.Core;
using Karpo.Dock.Graphs;
using Karpo.Dock.Markets;
using Karpo.Dock.Native;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock;

internal sealed unsafe class MarketFaceDrawing : IFaceDrawing
{
    public string Id => FolderFaces.MarketsId;

    public bool Seconds(FolderFace face) => false;

    /// <summary>The widest change, a row of its own down a bar on its side: two figures, two decimals, the sign and the percent.</summary>
    private const string ChangeTemplate = "+88.88%";

    /// <summary>The price a bar on its side fits its size to: an ordinary share's to the cent, so every price is one size.</summary>
    private const string PriceTemplate = "888.88";

    /// <summary>What the face tracks, as kept: its own record.</summary>
    private static MarketOptions Options(in FaceContext context) => context.Face.Market ?? MarketOptions.New();

    /// <summary>What it reads now; nothing come yet where it is only measured (no folder).</summary>
    private static MarketReading Read(in FaceContext context) =>
        context.Folder?.MarketReading() ?? MarketReading.Of(null, Options(context), Clock.RealUtc(), null);

    public FaceSize Measure(in FaceContext context)
    {
        var options = Options(context);
        if (!options.IsSet)
        {
            return new FaceSize(UnsetLength(context), Square: false);
        }
        return new FaceSize(options.StyleOf == MarketStyle.Converter ? ConverterLength(context) : TickerLength(context, options), Square: false);
    }

    /// <summary>Where it is only measured (no folder), what it tracks with no price yet, as its picture in a gallery.</summary>
    public void Paint(ID2D1DeviceContext* dc, in FaceContext context, D2D_RECT_F box)
    {
        // The chart is the button's ground: the face's room and the ends the button keeps round it, along the
        // bar — its left and right across one, its top and bottom down one.
        var edge = context.Fit.Edge;
        var cell = context.Vertical
            ? new D2D_RECT_F(box.left, box.top - edge, box.right, box.bottom + edge)
            : new D2D_RECT_F(box.left - edge, box.top, box.right + edge, box.bottom);
        var options = Options(context);
        if (!options.IsSet)
        {
            PaintUnset(dc, context, cell);
            return;
        }
        if (options.StyleOf == MarketStyle.Converter)
        {
            PaintConverter(dc, context, box);
            return;
        }
        PaintTicker(dc, context, box, cell, options);
    }

    /// <summary>What it shows now: a new price, its change, its colour, a price going stale or another style paints it again.</summary>
    public string Reading(in FaceContext context)
    {
        var reading = Read(context);
        return $"{Options(context).StyleOf}|{reading.Chart}|{reading.State}|{reading.Symbol}|{reading.Price}|{reading.Currency}|{reading.Percent}|{reading.Direction}|{reading.Stale}|{reading.Day.Count}|{reading.From}{reading.To}{reading.Amount}";
    }

    /// <summary>
    /// What it says to a screen reader (none over the bar, §367): the ticker, "MSFT 412.34
    /// +1.2%" — §494: shown in another currency, then the figure in both and the rate —; the converter's
    /// sentence with its rate and its age.
    /// </summary>
    public string? Tooltip(in FaceContext context)
    {
        if (context.Folder is null || Read(context) is not { Ready: true } reading)
        {
            return null;
        }
        return Options(context).StyleOf == MarketStyle.Converter
            ? reading.Tooltip()
            : string.Join('\n', new[] { string.Join(' ', new[] { reading.Symbol, reading.Price, reading.Percent }.Where(part => part.Length > 0)), reading.ExchangeTip() }.OfType<string>());
    }

    // --- Before a symbol is set ----------------------------------------------------------------------

    /// <summary>The face's length before a symbol is set: the kind's glyph and "Set up" across a bar, the glyph alone down one.</summary>
    private static float UnsetLength(in FaceContext context)
    {
        var fit = context.Fit;
        var glyph = fit.Glyph();
        return context.Vertical
            ? glyph
            : FaceDrawings.EmptyWidth(context.Gfx, fit);
    }

    /// <summary>"Set up in Settings" as an entry on the bar draws it (<see cref="SetupFace.OnBar"/>): the kind's glyph and "Set up", centred in <paramref name="cell"/>.</summary>
    private static void PaintUnset(ID2D1DeviceContext* dc, in FaceContext context, D2D_RECT_F cell)
    {
        D2D_MATRIX_3X2_F before;
        dc->GetTransform(&before);
        // Drawn at the cell's own origin, as an entry's cell is.
        var moved = before with
        {
            _31 = before._31 + (cell.left * before._11) + (cell.top * before._21),
            _32 = before._32 + (cell.left * before._12) + (cell.top * before._22),
        };
        dc->SetTransform(&moved);
        try
        {
            SetupFace.OnBar(dc, context.Gfx, context.Style, cell.right - cell.left, cell.bottom - cell.top, context.Face.Info.Glyph);
        }
        finally
        {
            dc->SetTransform(&before);
        }
    }

    // --- The ticker ----------------------------------------------------------------------------------

    /// <summary>
    /// The ticker's length along the bar, without the button's ends: across a bar the name, the price and —
    /// in the full style — the change as they read now, in tabular figures; down a bar on its side its rows.
    /// </summary>
    private static float TickerLength(in FaceContext context, MarketOptions options)
    {
        var parts = Parts(context, options, Read(context));
        if (context.Vertical)
        {
            return parts.SymbolHeight + parts.PriceHeight + (parts.ShowsChange ? parts.ChangeHeight : 0);
        }
        var fit = context.Fit;
        return parts.SymbolWidth + fit.Gap + parts.PriceWidth + (parts.Change.Length > 0 ? fit.Gap + parts.ChangeWidth : 0);
    }

    /// <summary>
    /// The ticker in <paramref name="box"/> (its length along the bar, the cell across): across a bar the
    /// name quiet, the price and the change in the up or down colour on one row; down a bar the name over the
    /// price; the chart behind the words, faded under them, over the button's whole <paramref name="cell"/>,
    /// in the full style.
    /// </summary>
    private static void PaintTicker(ID2D1DeviceContext* dc, in FaceContext context, D2D_RECT_F box, D2D_RECT_F cell, MarketOptions options)
    {
        var fit = context.Fit;
        var gfx = context.Gfx;
        var ink = new IslandInk(context.Style.Look);
        var reading = Read(context);
        var parts = Parts(context, options, reading);
        var full = options.StyleOf == MarketStyle.Full;
        var width = box.right - box.left;
        var height = box.bottom - box.top;
        dc->SetTextAntialiasMode(D2D1_TEXT_ANTIALIAS_MODE.D2D1_TEXT_ANTIALIAS_MODE_GRAYSCALE);
        ID2D1SolidColorBrush* brush = null;
        TextLine symbol = default;
        TextLine price = default;
        TextLine moved = default;
        try
        {
            brush = Gfx.Brush(dc, ink.Text);
            var room = MathF.Max(1, width);
            symbol = TextLine.Lay(gfx, parts.Symbol, parts.SymbolPx, Weight.Semibold, room);
            price = TextLine.Lay(gfx, parts.Price, parts.PricePx, Weight.Regular, room);
            if (parts.Change.Length > 0)
            {
                moved = TextLine.Lay(gfx, parts.Change, parts.ChangePx, Weight.Regular, room);
            }
            // A price three refreshes past due goes quiet: the figure in the secondary ink and no up or down
            // colour on the change.
            var colour = reading.Stale ? ink.Secondary : MarketChart.Moved(reading.Direction, context.Style.Look, ink);
            var figure = reading.Stale ? ink.Secondary : ink.Text;
            var chart = full && reading.Ready && reading.Day.Count >= 2;
            var radius = GraphBackdrop.BarRadius(context.Style);
            if (context.Vertical)
            {
                var block = parts.SymbolHeight + parts.PriceHeight + (parts.ShowsChange ? parts.ChangeHeight : 0);
                var top = TextLine.Round(box.top + ((height - block) / 2));
                if (chart)
                {
                    MarketChart.Behind(dc, cell, reading, colour, MarketChart.Coloured(reading, context.Style.Look), down: true, [(top, top + block)], 2 * fit.Gap, radius);
                }
                symbol.Draw(dc, brush, ink.Secondary, TextLine.Round(box.left + ((width - symbol.Width) / 2)), top);
                price.Draw(dc, brush, figure, TextLine.Round(box.left + ((width - price.Width) / 2)), top + parts.SymbolHeight);
                if (!moved.IsEmpty)
                {
                    moved.Draw(dc, brush, colour, TextLine.Round(box.left + ((width - moved.Width) / 2)), top + parts.SymbolHeight + parts.PriceHeight);
                }
                return;
            }
            if (chart)
            {
                // Across a bar the ticker's words are the whole button, so the chart is faded across all of it.
                MarketChart.Behind(dc, cell, reading, colour, MarketChart.Coloured(reading, context.Style.Look), down: false, [(cell.left, cell.right)], 2 * fit.Gap, radius);
            }
            var row = symbol.Width + fit.Gap + price.Width + (moved.IsEmpty ? 0 : fit.Gap + moved.Width);
            var x = box.left + TextLine.Round(MathF.Max(0, (width - row) / 2));
            var line = TextLine.Round(box.top + ((height - parts.PriceHeight) / 2));
            symbol.Draw(dc, brush, ink.Secondary, x, TextLine.Round(box.top + ((height - parts.SymbolHeight) / 2)));
            x += symbol.Width + fit.Gap;
            price.Draw(dc, brush, figure, x, line);
            x += price.Width + fit.Gap;
            moved.Draw(dc, brush, colour, x, line);
        }
        finally
        {
            moved.Release();
            price.Release();
            symbol.Release();
            ComUtil.Release(ref brush);
        }
    }

    /// <summary>
    /// What the ticker says and how large: one size across a bar, so its three parts share a baseline; down a
    /// bar on its side the largest size the price's shape fits, the name a step under it, and the change
    /// under them where it fits at 11 DIP, the smallest the bar's text goes to. Each part's room is what it
    /// says now, its figures tabular (§245). The price is never cut and the change never cut short
    /// (§262): down a bar a shorter form of either where the bar is too thin for it. Internal for the
    /// tests, which hold each part to the room it is drawn in.
    /// </summary>
    internal static (string Symbol, string Price, string Change, bool ShowsChange,
        float SymbolPx, float PricePx, float ChangePx,
        float SymbolWidth, float SymbolHeight, float PriceWidth, float PriceHeight, float ChangeWidth, float ChangeHeight) Parts(
        in FaceContext context, MarketOptions options, MarketReading reading)
    {
        var fit = context.Fit;
        var gfx = context.Gfx;
        var showChange = options.StyleOf == MarketStyle.Full;
        var symbol = MarketWords.Short(options, MarketWords.Title(options) ?? S.WidgetMarketsNoSymbolCell);
        var price = reading.Ready ? reading.Price : "–";
        var change = reading.Ready && showChange ? reading.Percent : "";
        var pricePx = fit.Primary;
        var symbolPx = fit.Primary;
        var changePx = fit.Primary;
        if (context.Vertical)
        {
            var across = VerticalFit.Room(fit);
            var ladder = VerticalFit.Ladder(fit);
            // The price is never cut (§262): as it reads where its shape fits the bar at the smallest
            // size, else the first of its shorter forms that does ("734,568", "734.6K"); the last where
            // none does, a bar too thin for any. Its shape, not the price now, so it keeps its form and
            // size as it ticks (§12.2).
            price = PriceForm(gfx, price, across, ladder[^1]);
            // One size for every ordinary price, a share's to the cent; a longer one (a coin's thousands) a
            // size it fits at (§245).
            pricePx = VerticalFit.AcrossAll(gfx, [PriceTemplate, Widest(price)], across, Weight.Regular, ladder).Px;
            var under = ladder.Where(size => size <= MathF.Min(fit.Secondary, pricePx)).ToArray();
            symbolPx = VerticalFit.Across(gfx, symbol, across, Weight.Semibold, under.Length > 0 ? under : [pricePx]).Px;
            // Say less before saying it smaller: the change at 11 DIP at the least — as small as an ordinary
            // price where that had to go under it — and no larger than an ordinary price; the first of its
            // forms that fits there ("+12.34%", "+12.3%", "+12%"), and no change row where none does (§262).
            // Sized on the ordinary price's template and the change's, never the figures now, so every face
            // of a bar has its change row or none, at one size (§245).
            var ordinary = VerticalFit.Across(gfx, PriceTemplate, across, Weight.Regular, ladder).Px;
            var least = MathF.Min(11 * fit.Scale * fit.TextScale, ordinary) - 0.01f;
            var smaller = ladder.Where(size => size <= ordinary && size >= least).ToArray();
            var (form, shapes) = ChangeForm(change.Length > 0 ? change : ChangeTemplate);
            showChange &= shapes is not null;
            change = showChange && change.Length > 0 ? form : "";
            changePx = shapes is { } fitted ? VerticalFit.AcrossAll(gfx, fitted, across, Weight.Regular, smaller).Px : pricePx;

            // The fullest of the change's forms that fits the bar at the smallest size the change goes to, and
            // the shapes it is sized on: its own and the ordinary day's of as many decimals ("+88.88%",
            // "+88.8%", "+88%"), so every ordinary change is one size (§12.2); none where no form fits.
            (string Form, string[]? Shapes) ChangeForm(string said)
            {
                foreach (var each in MarketText.ChangeForms(said))
                {
                    var decimals = each.Contains('.', StringComparison.Ordinal) ? each.Length - each.IndexOf('.', StringComparison.Ordinal) - 2 : 0;
                    string[] shaped = [MarketText.ChangeShape(each), decimals switch { 2 => ChangeTemplate, 1 => "+88.8%", _ => "+88%" }];
                    if (smaller.Length > 0 && shaped.All(shape => VerticalFit.Fits(gfx, shape, across, Weight.Regular, smaller[^1])))
                    {
                        return (each, shaped);
                    }
                }
                return (said, null);
            }
        }
        var (priceWidth, priceHeight) = TextLine.Template(gfx, Widest(price), pricePx, Weight.Regular);
        var (symbolWidth, symbolHeight) = TextLine.Template(gfx, symbol, symbolPx, Weight.Semibold);
        // Down a bar on its side the change keeps its row whenever the face shows one, even with nothing to put there.
        var (changeWidth, changeHeight) = change.Length > 0 ? TextLine.Template(gfx, change, changePx, Weight.Regular)
            : showChange && context.Vertical ? TextLine.Template(gfx, ChangeTemplate, changePx, Weight.Regular)
            : (0f, 0f);
        return (symbol, price, change, showChange, symbolPx, pricePx, changePx,
            symbolWidth, symbolHeight, priceWidth, priceHeight, changeWidth, changeHeight);
    }

    /// <summary>
    /// The form of <paramref name="price"/> a bar on its side shows (§262): the first of
    /// <see cref="MarketText.PriceForms"/> whose shape fits <paramref name="across"/> px at
    /// <paramref name="smallest"/>, the ladder's last; the shortest where none does.
    /// </summary>
    internal static string PriceForm(Gfx gfx, string price, float across, float smallest)
    {
        var forms = MarketText.PriceForms(price);
        return forms.FirstOrDefault(form => VerticalFit.Fits(gfx, Widest(form), across, Weight.Regular, smallest)) ?? forms[^1];
    }

    /// <summary>A price's shape in tabular figures: every figure an eight, the dash a figure's room.</summary>
    private static string Widest(string price) => price.Length <= 1 ? "8" : MarketIsland.Shape(price);

    // --- The converter -------------------------------------------------------------------------------

    /// <summary>Between "100 EUR =" and the worth: a word's space, as the sentence reads, not the face's wider gap.</summary>
    private static float Space(in Fit fit) => MathF.Round(fit.Gap / 2);

    /// <summary>The words before the worth, "100 EUR =", quiet.</summary>
    private static string Lead(MarketReading reading) => $"{Currencies.Plain(reading.Amount)} {reading.From} =";

    /// <summary>The worth and its currency as they read now, "94.12 CHF"; "– CHF" without a rate.</summary>
    private static string Worth(MarketReading reading) => $"{Currencies.Worth(reading.Amount, reading.To, reading.Rate)} {reading.To}";

    /// <summary>Down a bar on its side: the sizes the worth and its currency fit the bar's room at, and the two lines' heights.</summary>
    private static (float FigurePx, float FigureHeight, float CodePx, float CodeHeight) Stacked(in FaceContext context, MarketReading reading)
    {
        var fit = context.Fit;
        var room = VerticalFit.Room(fit);
        var ladder = VerticalFit.Ladder(fit);
        // One size whatever it is worth, as a ticker's price down a bar (§245): four figures to the cent, a
        // longer worth a size it fits at.
        var worth = MarketIsland.Shape(Currencies.Worth(reading.Amount, reading.To, reading.Rate));
        var (figurePx, figureHeight) = VerticalFit.AcrossAll(context.Gfx, ["8,888.88", worth], room, Weight.Regular, ladder);
        var under = ladder.Where(size => size <= MathF.Min(fit.Secondary, figurePx)).ToArray();
        var (codePx, _, codeHeight) = VerticalFit.Across(context.Gfx, reading.To, room, Weight.Semibold, under.Length > 0 ? under : [figurePx]);
        return (figurePx, figureHeight, codePx, codeHeight);
    }

    /// <summary>The converter's length: "100 EUR =" and the worth as it reads now, in tabular figures; down a bar its two rows.</summary>
    private static float ConverterLength(in FaceContext context)
    {
        var reading = Read(context);
        if (context.Vertical)
        {
            var (_, figureHeight, _, codeHeight) = Stacked(context, reading);
            return codeHeight + figureHeight;
        }
        var px = context.Fit.Primary;
        var worth = TextLine.Template(context.Gfx, Worth(reading), px, Weight.Regular).Width;
        var lead = TextLine.Template(context.Gfx, Lead(reading), px, Weight.Regular).Width;
        return MathF.Ceiling(lead + Space(context.Fit) + worth);
    }

    /// <summary>The amount and its worth: "100 EUR =" quiet, the worth in the text's ink — quiet without a rate or past due.</summary>
    private static void PaintConverter(ID2D1DeviceContext* dc, in FaceContext context, D2D_RECT_F box)
    {
        var fit = context.Fit;
        var ink = new IslandInk(context.Style.Look);
        var gfx = context.Gfx;
        var reading = Read(context);
        var width = box.right - box.left;
        var height = box.bottom - box.top;
        var worthInk = reading.Ready && !reading.Stale ? ink.Text : ink.Secondary;
        dc->SetTextAntialiasMode(D2D1_TEXT_ANTIALIAS_MODE.D2D1_TEXT_ANTIALIAS_MODE_GRAYSCALE);
        if (context.Vertical)
        {
            var (figurePx, figureHeight, codePx, codeHeight) = Stacked(context, reading);
            var top = TextLine.Round(box.top + ((height - (codeHeight + figureHeight)) / 2));
            FaceDrawings.Put(dc, gfx, reading.To, codePx, ink.Secondary, box, top);
            FaceDrawings.Put(dc, gfx, Currencies.Worth(reading.Amount, reading.To, reading.Rate), figurePx, worthInk, box, top + codeHeight);
            return;
        }
        var px = fit.Primary;
        TextLine lead = default;
        TextLine worth = default;
        ID2D1SolidColorBrush* brush = null;
        try
        {
            brush = Gfx.Brush(dc, ink.Text);
            worth = TextLine.Lay(gfx, Worth(reading), px, Weight.Regular, MathF.Max(1, width));
            lead = TextLine.Lay(gfx, Lead(reading), px, Weight.Regular, MathF.Max(1, width));
            var row = lead.Width + Space(fit) + worth.Width;
            var x = box.left + TextLine.Round(MathF.Max(0, (width - row) / 2));
            var y = TextLine.Round(box.top + ((height - worth.Height) / 2));
            lead.Draw(dc, brush, ink.Secondary, x, y);
            worth.Draw(dc, brush, worthInk, x + lead.Width + Space(fit), y);
        }
        finally
        {
            worth.Release();
            lead.Release();
            ComUtil.Release(ref brush);
        }
    }
}
