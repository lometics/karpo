// One style for every widget's line (docs\widgets.md Â§388): the ratchet that holds every list kind's
// drawing to the kit (Karpo.Dock\Islands\LineKit.cs).
//
// Every kind a list holds is drawn on the gutter tests' stand-ins (ListGutterTests: a frozen clock, the
// saved forecast, sample drives and sounds and graphs) at its own height and at one, two and four rows,
// at 100 % and in the dark, with the painting watched (PaintWatch.Styled): every line of text, glyph and
// fill it draws, with its size, its weight, its ink and its corner. What it drew is held to:
//
//   * text sizes in the ramp: 12, 14, 20, 28, 40;
//   * glyphs in 12, 16, 24;
//   * corners 0, 4, 8 or a pill;
//   * the words that lead a row start on the gutter or on the text edge (44);
//   * no words in the accent (the accent is for on, selected, the one main action and a chart);
//   * at most one accent-filled control;
//   * the blocks' anatomy (§401): a row's glyph in the list's leading column, whatever ends a row in its
//     trailing half on the trailing gutter.
//
// What breaks a rule is an offence, "<kind>: <rule> <value>". The allowlist (Allowed) holds the
// offences a kind is still let off; a new offence fails the test, and so does an allowed one no kind
// commits any more, so the list only shrinks. The few left at the end are the documented exceptions.
// Needs Direct2D, so it runs only with KARPO_UI_TESTS=1; what each kind drew is written to the test's
// output.

using Karpo.Dock;
using Karpo.Tests.Forecasts;
using Karpo.Tests.Islands;
using Karpo.Tests.Perf;
using TerraFX.Interop.DirectX;
using Xunit.Abstractions;
using static Karpo.Tests.Islands.Offscreen;

namespace Karpo.Tests.Folders;

[Collection(SamplerState.Collection)]
public sealed unsafe class LineStyleTests(ITestOutputHelper output)
{
    /// <summary>The offences still let off (see the top of the file). Shrinks; never grows.</summary>
    internal static readonly HashSet<string> Allowed =
    [
        "Bluetooth: ends off the gutter",
        "Hibernate: glyph 8",
        "Note: glyph off the column",
        "Note: text 6.7",
        "SoundMixer: glyph 9",
        // Documented (§401): a chart's labels stand at the points they name — the sun's strip under its arc's
        // and its day bar's ends, the tide graph's high and low times on its curve (§603) — not on the rows' edges.
        "SunArc: ends off the gutter",
        "SunArc: glyph off the column",
        "SunArc: starts at 49",
        "SunDayBar: ends off the gutter",
        "SunDayBar: glyph off the column",
        "SunDayBar: starts at 109",
        "TideGraph: ends off the gutter",
        "TideGraph: starts at 80",
        "Wifi: glyph 9",
    ];

    /// <summary>
    /// The kinds that are a grid (a Band): a month's, a day's, a week's and a year's views, the weather's
    /// hours, the screen-time ring's legend. Their labels stand in the grid's columns, not on the edges.
    /// </summary>
    private static readonly HashSet<ListKind> Bands =
        [ListKind.Month, ListKind.DayView, ListKind.WeekView, ListKind.YearView, ListKind.WeatherHours, ListKind.ScreenTimeChart,
            // §590: the next hours' columns, the week's graph and the air's day: grids of a column an hour or a day.
            ListKind.WeatherHoursList, ListKind.WeatherDaysGraph, ListKind.AirForecast,
            // The readings' facts, label over figure in two columns (§401), and a footer's buttons, a band of controls.
            ListKind.CpuDetails, ListKind.GpuDetails, ListKind.MemoryDetails, ListKind.DiskDetails, ListKind.NetworkDetails, ListKind.Actions];

    /// <summary>The rows each kind is drawn at besides its own height.</summary>
    private static readonly int[] RowsDrawn = [1, 2, 4];

    [UiFact]
    public void EveryLineDrawsOnTheKit()
    {
        var offences = Offences(output);
        var fresh = offences.Except(Allowed).Order().ToList();
        var gone = Allowed.Except(offences).Order().ToList();
        Assert.True(fresh.Count == 0 && gone.Count == 0,
            (fresh.Count > 0 ? $"New offences:{Environment.NewLine}{string.Join(Environment.NewLine, fresh.Select(o => $"        \"{o}\","))}{Environment.NewLine}" : "")
            + (gone.Count > 0 ? $"Allowed but no longer committed (take them off the list):{Environment.NewLine}{string.Join(Environment.NewLine, gone.Select(o => $"        \"{o}\","))}" : ""));
    }

    /// <summary>
    /// The kit's row heights (§401, §418): the Row 40 DIP, 52 with a caption under its name, and the compact row 32 for a
    /// dense list (an agenda's events and day headings) — never under a button's reach, so a pointer hits it as it hits
    /// any button, and holding a Body line with the kit's tight air over and under it, at 100 and 150 % and 100 and 225 % text.
    /// </summary>
    [UiFact]
    public void TheKitsRowsAreThreeHeights()
    {
        Assert.Equal(32, LineRow.CompactDip);
        Assert.True(LineRow.CompactDip >= LineButton.ReachDip, $"the compact row {LineRow.CompactDip} DIP, under a button's reach {LineButton.ReachDip}");
        OnStaThread(gfx =>
        {
            foreach (var scale in new[] { 1f, 1.5f })
            {
                foreach (var textScale in new[] { 1f, 2.25f })
                {
                    var look = ListFormPictures.LookFor(dark: true, textScale);
                    var style = FolderFlyout.StyleFor(scale, look, FlyoutTheme.For(look));
                    var at = $"{scale * 100:0} %, text {textScale * 100:0} %";
                    float Px(float dip) => PopupPaint.Round(dip * scale * textScale);
                    Assert.True(LineRow.Height(style, caption: false) >= Px(40) - 0.5f, $"{at}: the Row {LineRow.Height(style, caption: false)} px");
                    // §442: a row with its caption is three of the folder's cells.
                    Assert.Equal(3 * FolderFlyout.CellPx(scale, textScale), LineRow.Height(style, caption: true), 0.5f);
                    Assert.Equal(Px(LineRow.CompactDip), LineRow.Compact(style), 0.5f);
                    var body = TilePaint.LineHeight(gfx, style.Text(LineType.BodyDip), Weight.Semibold) + (2 * style.Px(LineSpace.TightDip));
                    Assert.True(LineRow.Compact(style) >= body, $"{at}: the compact row {LineRow.Compact(style)} px holds no Body line and its air, {body:0.#}");
                    Assert.True(LineRow.Compact(style) >= LineButton.Of(gfx, style).Reach, $"{at}: the compact row under a button's reach");
                }
            }
        });
    }

    /// <summary>Every offence every kind commits, at 100 %, dark, at its own height and at one, two and four rows.</summary>
    internal static HashSet<string> Offences(ITestOutputHelper? output = null)
    {
        var offences = new HashSet<string>();
        ListGutterTests.StoodIn<Gfx>(SavedWeather.Fixed, OnStaThread, gfx =>
        {
            const float scale = 1f;
            var look = ListFormPictures.LookFor(dark: true);
            var theme = FlyoutTheme.For(look);
            var style = FolderFlyout.StyleFor(scale, look, theme);
            var accent = TilePaint.Accent(style);
            foreach (var (_, kinds) in ListGutterTests.Groups)
            {
                var folder = ListFormPictures.Folder(gfx, ListGutterTests.Before, [.. kinds.Select(ListGutterTests.Made)]);
                try
                {
                    ListGutterTests.Ready(folder);
                    var (_, plan) = ListFormPictures.Plan(folder, look, scale);
                    var gutter = FolderFlyout.RowInset(plan.TextPx);
                    var textEdge = FolderFlyout.RowText(plan.TextPx);
                    for (var index = 0; index < folder.Tiles.Count; index++)
                    {
                        var kind = folder.Tiles[index].Cell.Island.List!.Value;
                        var width = plan.Cards[index].right - plan.Cards[index].left;
                        var heights = new[] { ListFormPictures.Given(plan, index) }.Concat(RowsDrawn.Select(rows => LineRows.Px(rows, style))).Distinct();
                        var said = new SortedSet<string>();
                        foreach (var height in heights)
                        {
                            var drawn = Draw(gfx, folder, plan, index, width, height, look, scale);
                            foreach (var offence in Judge(kind, drawn, width, gutter, textEdge, accent, scale, style.TextScale))
                            {
                                offences.Add(offence);
                            }
                            foreach (var piece in drawn.Where(piece => piece.What is PaintedKind.Text or PaintedKind.Glyph))
                            {
                                said.Add($"{piece.What.ToString().ToLowerInvariant()} {piece.Px / scale:0.#}{(piece.Weight >= 600 ? " semibold" : "")}");
                            }
                        }
                        output?.WriteLine($"{kind}: {string.Join(", ", said)}");
                    }
                }
                finally
                {
                    folder.Closed();
                    folder.Dispose();
                }
            }
        });
        return offences;
    }

    /// <summary>The offences in one drawing of <paramref name="kind"/>'s line (see the top of the file).</summary>
    private static IEnumerable<string> Judge(ListKind kind, List<PaintedStyle> drawn, float width, float gutter, float textEdge, DXGI_RGBA accent, float scale, float textScale)
    {
        static bool In(float value, float[] steps) => steps.Any(step => MathF.Abs(value - step) < 0.05f);
        static bool Same(DXGI_RGBA one, DXGI_RGBA two) =>
            MathF.Abs(one.r - two.r) < 0.01f && MathF.Abs(one.g - two.g) < 0.01f && MathF.Abs(one.b - two.b) < 0.01f && one.a > 0.5f;
        // What a line scrolls out of its box is not drawn there.
        drawn = [.. drawn.Where(piece => piece.Box.right > 0 && piece.Box.left < width && piece.Box.bottom > 0)];
        var plates = drawn.Where(piece => piece.What == PaintedKind.Fill && piece.Radius > 0).Select(piece => piece.Box).ToList();
        bool OnPlate(D2D_RECT_F box) => plates.Any(plate =>
            box.left >= plate.left - 0.5f && box.right <= plate.right + 0.5f && box.top >= plate.top - 0.5f && box.bottom <= plate.bottom + 0.5f);
        foreach (var piece in drawn)
        {
            var dip = piece.Px / scale / textScale;
            switch (piece.What)
            {
                case PaintedKind.Text when !In(dip, LineType.Dips):
                    yield return $"{kind}: text {dip:0.#}";
                    break;
                case PaintedKind.Glyph when !In(dip, LineGlyph.Dips):
                    yield return $"{kind}: glyph {dip:0.#}";
                    break;
                case PaintedKind.Fill or PaintedKind.Outline when piece.Radius > 0:
                    var across = MathF.Min(piece.Box.right - piece.Box.left, piece.Box.bottom - piece.Box.top);
                    var corner = piece.Radius / scale;
                    if (!In(corner, [LineCorner.ControlDip, LineCorner.CardDip]) && piece.Radius + 0.75f < across / 2)
                    {
                        yield return $"{kind}: corner {corner:0.#}";
                    }
                    break;
            }
            if (piece.What == PaintedKind.Text && Same(piece.Ink, accent))
            {
                yield return $"{kind}: accent words";
            }
        }

        // The words that lead each row: on the gutter or the text edge, unless they stand on a plate
        // (a button's word) or past the line's first third (a reading). A grid's labels stand in its
        // columns (Bands).
        if (Bands.Contains(kind))
        {
            yield break;
        }
        // A row stands on the line, not on a card of its own (§401, §428): no words on a rounded fill across the line taller
        // than a row (a field, a button, a meter are lower).
        var cards = drawn.Where(piece => piece.What == PaintedKind.Fill && piece.Radius > 0 && piece.Box.right - piece.Box.left >= width * 2 / 3
            && piece.Box.bottom - piece.Box.top > (40 * scale * textScale) + 0.5f).ToList();
        if (drawn.Any(piece => piece.What == PaintedKind.Text && cards.Any(card => piece.Box.left >= card.Box.left - 0.5f && piece.Box.right <= card.Box.right + 0.5f
            && piece.Box.top >= card.Box.top - 0.5f && piece.Box.bottom <= card.Box.bottom + 0.5f)))
        {
            yield return $"{kind}: words on a card";
        }
        var words = drawn.Where(piece => piece.What == PaintedKind.Text && !OnPlate(piece.Box)).ToList();
        foreach (var lead in words.Where(piece => !words.Any(other => other.Box.left < piece.Box.left - 0.5f && Overlaps(other.Box, piece.Box))))
        {
            var left = lead.Box.left;
            if (left < width / 3 && MathF.Abs(left - gutter) > 1.5f && MathF.Abs(left - textEdge) > 1.5f)
            {
                yield return $"{kind}: starts at {MathF.Round(left / scale):0}";
            }
        }

        // The blocks' anatomy (§401): a row's glyph stands in the list's leading column on the gutter (a Feature
        // glyph beside a Hero grown into the room before the text edge), and whatever ends a row in the line's
        // trailing half — a reading, a button's plate, a switch, an icon button — ends on the trailing gutter. Not a
        // glyph or a word on a plate (a button's own), nor a plate as wide as the line (a chosen row's).
        var icon = textEdge - gutter - (8 * scale * textScale);
        foreach (var glyph in drawn.Where(piece => piece.What == PaintedKind.Glyph && !OnPlate(piece.Box)))
        {
            var middle = (glyph.Box.left + glyph.Box.right) / 2;
            if (middle >= width / 3)
            {
                continue;
            }
            var feature = glyph.Px / scale / textScale > LineGlyph.RowDip + 0.05f;
            var (least, most) = feature ? (gutter, textEdge) : (gutter, gutter + icon);
            if (middle < least - 1.5f || middle > most + 1.5f)
            {
                yield return $"{kind}: glyph off the column";
            }
        }
        var ends = drawn.Where(piece => piece.What switch
        {
            PaintedKind.Text or PaintedKind.Glyph => !OnPlate(piece.Box),
            // A plate or a switch: as wide as tall or wider, and not round. A bar standing up and a circle are a
            // picture's — a clock face's hands, ticks, ring and hub.
            PaintedKind.Fill or PaintedKind.Outline => piece.Radius > 0 && piece.Box.right - piece.Box.left < width * 2 / 3
                && piece.Box.right - piece.Box.left >= piece.Box.bottom - piece.Box.top
                && !(MathF.Abs((piece.Box.right - piece.Box.left) - (piece.Box.bottom - piece.Box.top)) < 1 && piece.Radius + 0.75f >= (piece.Box.right - piece.Box.left) / 2),
            _ => false,
        }).ToList();
        var trailing = width - gutter;
        foreach (var end in ends.Where(piece => !ends.Any(other => other.Box.right > piece.Box.right + 0.5f && Overlaps(other.Box, piece.Box))))
        {
            var right = end.Box.right;
            // What ends a row starts in its trailing half: a reading, a control. Words that run on from the text edge end where they end.
            if (end.Box.left < width / 2 || right <= width * 2 / 3 || right > width - 0.5f)
            {
                continue;
            }
            // A word keeps a little air of its own inside its advance, a plate ends on its edge, and a glyph ends a
            // row as an icon button's: in the middle of its box, a control up to 40 DIP across, standing on the gutter.
            var air = end.What switch
            {
                PaintedKind.Text => MathF.Max(2, 0.12f * end.Px),
                PaintedKind.Glyph => MathF.Max(2, ((40 * scale * textScale) - (end.Box.right - end.Box.left)) / 2),
                _ => 2,
            };
            if (right < trailing - air - 1.5f || right > trailing + 1.5f)
            {
                yield return $"{kind}: ends off the gutter";
            }
        }

        // The controls filled with the accent: corners of a control, no larger than a row.
        var filled = drawn.Count(piece => piece.What == PaintedKind.Fill && Same(piece.Ink, accent)
            && MathF.Abs((piece.Radius / scale) - LineCorner.ControlDip) < 0.05f && piece.Box.bottom - piece.Box.top <= 40 * scale * textScale);
        if (filled > 1)
        {
            yield return $"{kind}: accent controls {filled}";
        }
    }

    /// <summary>Whether two boxes share a band of height: the same row of words.</summary>
    private static bool Overlaps(D2D_RECT_F one, D2D_RECT_F two) =>
        MathF.Min(one.bottom, two.bottom) - MathF.Max(one.top, two.top) > MathF.Min(one.bottom - one.top, two.bottom - two.top) / 2;

    /// <summary>Line <paramref name="index"/> drawn alone into a box <paramref name="height"/> px tall, its painting watched, in its own px.</summary>
    private static List<PaintedStyle> Draw(Gfx gfx, FolderIsland folder, FolderPlan plan, int index, float width, float height, Look look, float scale)
    {
        var theme = FlyoutTheme.For(look);
        var style = FolderFlyout.StyleFor(scale, look, theme);
        var tile = folder.Tiles[index];
        var box = PopupPaint.Rect(0, 0, width, height);
        var drawn = new List<PaintedStyle>();
        try
        {
            Bitmap(width, height, dc =>
            {
                var device = (nint)dc;
                PaintWatch.Styled = piece =>
                {
                    D2D_MATRIX_3X2_F at;
                    ((ID2D1DeviceContext*)device)->GetTransform(&at);
                    // A piece drawn turned is a picture's (a clock face's hands), not a row's.
                    if (MathF.Abs(at._12) > 0.001f || MathF.Abs(at._21) > 0.001f || at._11 < 0 || at._22 < 0)
                    {
                        return;
                    }
                    var placed = new D2D_RECT_F(
                        (piece.Box.left * at._11) + (piece.Box.top * at._21) + at._31,
                        (piece.Box.left * at._12) + (piece.Box.top * at._22) + at._32,
                        (piece.Box.right * at._11) + (piece.Box.bottom * at._21) + at._31,
                        (piece.Box.right * at._12) + (piece.Box.bottom * at._22) + at._32);
                    var grown = MathF.Abs(at._22) > 0 ? MathF.Abs(at._22) : 1;
                    drawn.Add(piece with { Box = placed, Px = piece.Px * grown, Radius = piece.Radius * grown });
                };
                if (tile.Cell.Island.List?.CardInList() != true)
                {
                    FolderFlyout.PaintListLine(dc, gfx, tile.Island, tile.Cell.Island, box, plan, theme, scale);
                }
                else
                {
                    FolderFlyout.PaintContent(dc, tile.Island, box, style);
                }
            }, _ => { });
        }
        finally
        {
            PaintWatch.Styled = null;
        }
        return drawn;
    }
}
