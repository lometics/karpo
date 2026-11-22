// The agenda, what's playing, the drives and the removable devices on the kit's blocks (docs\widgets.md §401):
// each line of the proposal's "time, media and drives" mockup drawn as the open folder draws it, at the
// height a new line of its kind gets, on the gutter tests' stand-ins, at 100 % and 150 %, its painting watched
// (PaintWatch.Styled), and held to the blocks' anatomy:
//
//   * the agenda: a header — the calendar's glyph in the leading column, its name in the Body strong where
//     every name begins — over its events, each its calendar's colour bar in the leading column, its title at the
//     text edge and when it is on the title's line on the trailing gutter (§414), every day, today too, a heading on
//     the gutter over its events, all alike (§419), an empty today's over the Status row "No events";
//     as tall as the header's 40 DIP, the kit's compact row of 32 an event (one line, §413) and one a heading (§418);
//   * what's playing: the kit's row with its caption, three cells (60 DIP, §442) — the title at the text edge, the art's plate on
//     the trailing gutter;
//   * a drive and a share: the row with its caption and the meter in its block, three cells — the name at the text edge,
//     how much is free ending on the trailing gutter, no other line of words;
//   * the removable devices: a device the row with its caption, three cells — its name at the text edge, its eject
//     button a standard button's plate on the trailing gutter.
// Needs Direct2D, so it runs only with KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Tests.Forecasts;
using Karpo.Tests.Islands;
using Karpo.Tests.Perf;
using TerraFX.Interop.DirectX;
using static Karpo.Tests.Islands.Offscreen;

namespace Karpo.Tests.Folders;

[Collection(SamplerState.Collection)]
public sealed unsafe class AgendaMediaDriveAnatomyTests
{
    private static readonly ListKind[] Kinds = [ListKind.Calendar, ListKind.Media, ListKind.Drive, ListKind.NetworkDrive, ListKind.RemovableDevices];

    [UiFact]
    public void EachLineIsTheKitsBlocks()
    {
        var failures = new List<string>();
        ListGutterTests.StoodIn<Gfx>(SavedWeather.Fixed, OnStaThread, gfx =>
        {
            foreach (var scale in new[] { 1f, 1.5f })
            {
                var look = ListFormPictures.LookFor(dark: true);
                var theme = FlyoutTheme.For(look);
                var style = FolderFlyout.StyleFor(scale, look, theme);
                var folder = ListFormPictures.Folder(gfx, ListGutterTests.Before, [.. Kinds.Select(ListGutterTests.Made)]);
                try
                {
                    ListGutterTests.Ready(folder);
                    var (natural, plan) = ListFormPictures.Plan(folder, look, scale);
                    var gutter = FolderFlyout.RowInset(plan.TextPx);
                    var textEdge = FolderFlyout.RowText(plan.TextPx);
                    var column = (gutter + textEdge - (8 * scale)) / 2;
                    float Dip(float dip) => PopupPaint.Round(dip * scale);
                    for (var index = 0; index < folder.Tiles.Count; index++)
                    {
                        var kind = folder.Tiles[index].Cell.Island.List!.Value;
                        var width = plan.Cards[index].right - plan.Cards[index].left;
                        var height = ListFormPictures.Given(plan, index);
                        var drawn = Draw(gfx, folder, plan, index, width, height, look, scale);
                        var at = $"{kind} at {scale * 100:0} %";
                        var words = drawn.Where(piece => piece.What == PaintedKind.Text).ToList();
                        var plates = drawn.Where(piece => piece.What == PaintedKind.Fill && piece.Radius > 0).ToList();
                        void Near(string what, float value, float want, float slack = 1.5f)
                        {
                            if (MathF.Abs(value - want) > slack)
                            {
                                failures.Add($"{at}: {what} at {value:0.#}, not {want:0.#}");
                            }
                        }
                        bool AtTextEdge(PaintedStyle piece) => MathF.Abs(piece.Box.left - textEdge) <= 1.5f;
                        switch (kind)
                        {
                            case ListKind.Calendar:
                            {
                                var glyph = drawn.FirstOrDefault(piece => piece.What == PaintedKind.Glyph);
                                Near("its glyph's middle", (glyph.Box.left + glyph.Box.right) / 2, column, 4);
                                var name = words.FirstOrDefault(piece => piece.Weight >= 600);
                                if (name.Px == 0 || !AtTextEdge(name) || name.Box.top > Dip(40))
                                {
                                    failures.Add($"{at}: no name in the Body strong at the text edge on the first row");
                                }
                                var bars = plates.Where(piece => piece.Box.right - piece.Box.left <= Dip(4) && piece.Box.top >= Dip(40)).ToList();
                                if (bars.Count < 2)
                                {
                                    failures.Add($"{at}: {bars.Count} colour bars under the header");
                                }
                                // §414: each event's colour bar in the leading column, its title at the text edge in the Body, and
                                // when it is on the title's line, one line, ending on the trailing gutter. Nothing on the gutter but
                                // a later day's heading (semibold): no time left of the titles.
                                var headings = words.Where(piece => piece.Box.top >= Dip(40) && piece.Weight >= 600).ToList();
                                // Nothing today (the stand-ins' Wednesday): today's heading over the kit's Status row, "No events" in
                                // the Body, regular, in the headings' secondary ink, at the text edge on its compact row's middle, no
                                // colour bar on it, then tomorrow's heading (2026-10-30: "in the agenda today should also appear if no
                                // event is there and then it should just say no event").
                                var empty = (Top: Dip(40) + Dip(32), Bottom: Dip(40) + Dip(64));
                                var status = words.Where(piece => piece.Weight < 600 && MathF.Abs(piece.Px - (14 * scale)) < 0.5f && headings.Count > 0 && piece.Ink.Equals(headings[0].Ink) && piece.Box.top >= Dip(40)).ToList();
                                if (status.Count != 1)
                                {
                                    failures.Add($"{at}: {status.Count} rows of the Body in the headings' ink, not one under today's heading");
                                }
                                else
                                {
                                    var said = status[0];
                                    var over = headings.OrderBy(piece => piece.Box.top).ToList();
                                    if (MathF.Abs(((said.Box.top + said.Box.bottom) / 2) - ((empty.Top + empty.Bottom) / 2)) > 1.5f || !AtTextEdge(said))
                                    {
                                        failures.Add($"{at}: \"No events\" at {said.Box.left:0},{said.Box.top:0}..{said.Box.bottom:0}, not at the text edge {textEdge:0} on the middle of the row {empty.Top:0}..{empty.Bottom:0}");
                                    }
                                    if (over.Count < 2 || over[0].Box.bottom > empty.Top + 0.5f || over[1].Box.top < empty.Bottom - 0.5f)
                                    {
                                        failures.Add($"{at}: the row {empty.Top:0}..{empty.Bottom:0} not between today's heading and the next");
                                    }
                                }
                                words = [.. words.Except(headings).Except(status)];
                                foreach (var left in words.Where(piece => piece.Box.top >= Dip(40) && piece.Box.left < textEdge - 1.5f))
                                {
                                    failures.Add($"{at}: words of {left.Px:0} px at {left.Box.left:0}..{left.Box.right:0}, left of the titles at the text edge {textEdge:0}");
                                }
                                var titles = words.Where(piece => piece.Box.top >= Dip(40) && AtTextEdge(piece) && MathF.Abs(piece.Px - (14 * scale)) < 0.5f).ToList();
                                var times = words.Where(piece => piece.Box.top >= Dip(40) && !AtTextEdge(piece) && MathF.Abs(piece.Box.right - (width - gutter)) <= 1.5f).ToList();
                                if (titles.Count < 2 || times.Count != titles.Count)
                                {
                                    failures.Add($"{at}: {times.Count} times on the trailing gutter for {titles.Count} titles at the text edge");
                                }
                                foreach (var title in titles)
                                {
                                    var middle = (title.Box.top + title.Box.bottom) / 2;
                                    if (!times.Any(time => MathF.Abs(((time.Box.top + time.Box.bottom) / 2) - middle) <= 1.5f && time.Box.left > title.Box.right))
                                    {
                                        failures.Add($"{at}: the title at {title.Box.left:0},{title.Box.top:0} has no time after it on its line on the trailing gutter");
                                    }
                                }
                                foreach (var bar in bars)
                                {
                                    if (bar.Box.left < gutter - 0.5f || bar.Box.right > textEdge - 0.5f)
                                    {
                                        failures.Add($"{at}: a colour bar at {bar.Box.left:0}..{bar.Box.right:0}, not in the leading column ({gutter:0}..{textEdge:0})");
                                    }
                                }
                                if (bars.Any(bar => bar.Box.top < empty.Bottom - 0.5f))
                                {
                                    failures.Add($"{at}: a colour bar on today's empty row");
                                }
                                var events = bars.Count;
                                // Its items, rounded up to whole cells (§442).
                                Near("its height", height, LineRows.Whole(Dip(40) + ((events + headings.Count + status.Count) * Dip(32)), style), 2);
                                // One line of content an event: every title on its own row's middle, 32 DIP apart where no heading stands between (§418).
                                var middles = titles.Select(piece => (piece.Box.top + piece.Box.bottom) / 2).Order().ToList();
                                foreach (var (over, under) in middles.Zip(middles.Skip(1)))
                                {
                                    if (MathF.Abs(under - over - Dip(32)) > 1.5f && MathF.Abs(under - over - Dip(64)) > 1.5f)
                                    {
                                        failures.Add($"{at}: two titles {under - over:0} px apart, not a 32 DIP row (or a heading's and a row's)");
                                    }
                                }
                                foreach (var heading in headings)
                                {
                                    Near("a day's heading's start", heading.Box.left, gutter);
                                    // Today's heading as every other day's (§419): one size, weight and ink.
                                    if (heading.Px != headings[0].Px || heading.Weight != headings[0].Weight || !heading.Ink.Equals(headings[0].Ink))
                                    {
                                        failures.Add($"{at}: a day's heading at {heading.Box.top:0} unlike the first's: {heading.Px:0} px weight {heading.Weight}, not {headings[0].Px:0} px weight {headings[0].Weight} in its ink");
                                    }
                                }
                                // Today has a heading of its own over its first event, the header no date (§419).
                                if (headings.Count == 0 || titles.Count == 0 || headings.Min(piece => piece.Box.top) > titles.Min(piece => piece.Box.top))
                                {
                                    failures.Add($"{at}: the first event under the header stands under no day's heading");
                                }
                                break;
                            }
                            case ListKind.Media:
                            {
                                // §432, §442: its form, the kit's row with its caption, three cells.
                                Near("its form's height", natural[index], LineRow.Height(style, caption: true), 1);
                                var title = words.OrderBy(piece => piece.Box.top).FirstOrDefault();
                                if (!AtTextEdge(title))
                                {
                                    failures.Add($"{at}: its title at {title.Box.left:0}, not the text edge {textEdge:0}");
                                }
                                var art = plates.OrderByDescending(piece => piece.Box.right).FirstOrDefault();
                                Near("its art's end", art.Box.right, width - gutter);
                                Near("its art's side", art.Box.right - art.Box.left, Dip(40));
                                break;
                            }
                            case ListKind.Drive or ListKind.NetworkDrive:
                            {
                                Near("its form's height", natural[index], LineRow.Height(style, caption: true), 1);
                                var name = words.OrderBy(piece => piece.Box.top).ThenBy(piece => piece.Box.left).First();
                                if (!AtTextEdge(name))
                                {
                                    failures.Add($"{at}: its name at {name.Box.left:0}, not the text edge {textEdge:0}");
                                }
                                var free = words.OrderByDescending(piece => piece.Box.right).First();
                                Near("how much is free's end", free.Box.right, width - gutter, 3);
                                if (words.Count != 3)
                                {
                                    failures.Add($"{at}: {words.Count} lines of words, not its name, its caption and what is free");
                                }
                                break;
                            }
                            case ListKind.RemovableDevices:
                            {
                                Near("its height", height, LineRow.Height(style, caption: true), 1);
                                var name = words.OrderBy(piece => piece.Box.left).First();
                                if (!AtTextEdge(name))
                                {
                                    failures.Add($"{at}: its name at {name.Box.left:0}, not the text edge {textEdge:0}");
                                }
                                var eject = plates.OrderByDescending(piece => piece.Box.right).FirstOrDefault();
                                if (eject.Px != 0 || eject.Radius == 0)
                                {
                                    failures.Add($"{at}: its eject button has no plate");
                                }
                                Near("its eject button's end", eject.Box.right, width - gutter);
                                break;
                            }
                        }
                    }
                }
                finally
                {
                    folder.Closed();
                    folder.Dispose();
                }
            }
        });
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

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
                    drawn.Add(piece with
                    {
                        Box = new D2D_RECT_F(piece.Box.left + at._31, piece.Box.top + at._32, piece.Box.right + at._31, piece.Box.bottom + at._32),
                    });
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
        return [.. drawn.Where(piece => piece.Box.bottom > 0 && piece.Box.top < height)];
    }
}
