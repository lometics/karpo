// The timer and the focus session as a line of a list (CountdownTile, CountdownCard). The user:
// "timer and focus should get implementations for all three" — an entry of a taskbar, a line, and
// the panel the entry opened; the entry and its panel became the timer and focus presets
// (docs\widgets.md §92), whose faces are TimerPresetTests'.
//
// Everything here draws or measures text, so it runs with KARPO_UI_TESTS=1; the sheets go to
// KARPO_PNG_DIR. Every widget here runs on a store of countdowns of the test's own (Countdowns), as a
// widget of the dock's runs on the dock's: there is no store the process shares.

using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using Color = Windows.UI.Color;

namespace Karpo.Tests.Calendars;

public sealed unsafe class CountdownFormsTests
{
    private static readonly DateTime Frozen = new(2026, 9, 22, 14, 40, 7, DateTimeKind.Local);

    private static Look LookFor(bool dark, float textScale = 1f) => new(
        Glass: false,
        Dark: dark,
        Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4),
        AccentLight2: Color.FromArgb(255, 0x99, 0xEB, 0xFF),
        AccentDark1: Color.FromArgb(255, 0x00, 0x5F, 0xB8),
        TextScale: textScale);

    /// <summary>A name a countdown of <paramref name="kind"/> might be given.</summary>
    private static string NameOf(CountdownKind kind) => kind == CountdownKind.Focus ? "Deep work" : "Tea";

    /// <summary>
    /// What a countdown of <paramref name="kind"/> can be doing — set, running, paused, over, and a run
    /// of an hour and a half — and, with a name it was given, running and over.
    /// </summary>
    private static (string What, Countdown State, TimeSpan Length, string Name)[] States(CountdownKind kind)
    {
        var length = TimeSpan.FromMinutes(kind == CountdownKind.Focus ? 25 : 10);
        var running = Countdown.Start(kind, length, Frozen.AddMinutes(-2).AddSeconds(-13));
        var over = Countdown.Start(kind, length, Frozen - length).At(Frozen);
        return
        [
            ("set", Countdown.None, length, ""),
            ("running", running, length, ""),
            ("paused", running.Pause(Frozen.AddSeconds(-30)), length, ""),
            ("over", over, length, ""),
            ("an hour", Countdown.Start(kind, TimeSpan.FromMinutes(90), Frozen.AddMinutes(-7)), TimeSpan.FromMinutes(90), ""),
            ("named, running", running, length, NameOf(kind)),
            ("named, over", over, length, NameOf(kind)),
        ];
    }

    /// <summary>
    /// A countdown widget of <paramref name="kind"/> on a store of its own, set to <paramref name="length"/>,
    /// called <paramref name="name"/>, and showing <paramref name="state"/>.
    /// </summary>
    private static CountdownTile Tile(Gfx gfx, CountdownKind kind, Countdown state, TimeSpan length, string name = "")
    {
        var countdowns = new Countdowns();
        countdowns.Set("probe", state);
        var item = (kind == CountdownKind.Focus ? ListKind.Focus : ListKind.Timer).New();
        item.Id = "probe";
        item.Countdown = new CountdownOptions { Minutes = (int)length.TotalMinutes, Name = name };
        return new CountdownTile(Config.IslandKey(null, "probe"), item, gfx, new Config(), countdowns, () => Frozen);
    }

    // --- What they are -----------------------------------------------------------------

    /// <summary>
    /// The timer and the focus time are a widget of a list, no longer shells: the list's line is
    /// drawn by it, one row to six, and takes a press on a stepper and the wheel; the factory makes
    /// the one class. Neither is an entry of the taskbar any more: that is its preset, a folder.
    /// </summary>
    [UiFact]
    public void TheyAreOneWidgetInAList()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var config = new Config();
            foreach (var list in new[] { ListKind.Timer, ListKind.Focus })
            {
                Assert.Null(BarEntries.Find(list.Id()));
                var line = ListWidgets.Of(list);
                Assert.Equal(WidgetSource.Countdown, line.Drawing);
                Assert.Equal(ListLine.Card, line.Line);
                Assert.Equal(CellAct.Nothing, line.Act);
                Assert.True(line.Interaction.HasFlag(Interaction.Drag) && line.Interaction.HasFlag(Interaction.Wheel), $"{list} does not claim its steppers' press and the wheel");
                Assert.Equal((2, 12), (line.Range.Least, line.Range.Most));

                var inList = list.New();
                inList.Id = "list";
                using var lineIsland = Assert.IsType<CountdownTile>(Karpo.Dock.Islands.CreateLine(Config.IslandKey(null, inList.Id), inList, list, gfx, config));
                // A line of a list is the kit's one row (CountdownLine, §401), the timer's and the focus session's alike.
                var tile = new TileStyle(1f, LookFor(true), FlyoutTheme.For(LookFor(true)), 1, 1);
                Assert.Equal(CountdownLine.Height(tile), lineIsland.ListHeight(360, tile));
                Assert.Equal(default, lineIsland.Click(0));
            }
        });
    }

    // --- A line of a list ------------------------------------------------------------

    /// <summary>How tall a line of <paramref name="rows"/> of the list's rows is, in px: as the list folder gives it (FolderFlyout.Build).</summary>
    private static float RowsPx(int rows, float scale, float textScale) => Karpo.Tests.Folders.ListFormPictures.Rows(rows, scale, textScale);

    /// <summary>
    /// A line of a list is the kit's row (§401), worked at every height from one row to six and at the height it
    /// asks for, at 100, 150 and 200 %, at every text size, on a list of every width, in every state, named and
    /// not, timer and focus: its row at the line's top and nothing under it but the meter of what is left; its
    /// start button on the trailing gutter, the line's one accent control, and a click there reaches it; its time
    /// shown; its glyph in the leading column and its name where every name begins, the given name or none;
    /// nothing outside the gutters, nothing on anything else; and the buttons where they were in every state
    /// at the same height — nothing moves as it runs.
    /// </summary>
    [UiFact]
    public void TheLineIsWorkedAtEveryHeight()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var failures = new List<string>();
            foreach (var scale in new[] { 1f, 1.5f, 2f })
            {
                foreach (var textScale in new[] { 1f, 1.5f, 2.25f })
                {
                    var look = LookFor(true, textScale);
                    var style = new TileStyle(scale, look, FlyoutTheme.For(look), 1, 1);
                    var form = ListForm.Of(gfx, style);
                    var row = CountdownLine.Height(style);
                    foreach (var listWidth in FolderListWidths.All)
                    {
                        var width = FolderFlyout.ListWidth(scale, listWidth);
                        foreach (var kind in new[] { CountdownKind.Timer, CountdownKind.Focus })
                        {
                            for (var rows = 0; rows <= 6; rows++)
                            {
                                // Nought is the height the line asks for; then one row to six.
                                var height = rows == 0 ? row : RowsPx(rows, scale, textScale);
                                var box = PopupPaint.Rect(0, 0, width, height);
                                D2D_RECT_F? start = null;
                                foreach (var (what, state, length, name) in States(kind))
                                {
                                    using var tile = Tile(gfx, kind, state, length, name);
                                    var plan = tile.Plan(box, style);
                                    var pieces = ((ILinePieces)tile).LinePieces(box, style);
                                    var where = $"{kind} at {scale:0.##}x, text {textScale:0.##}, {listWidth} ({width} px) wide, {(rows == 0 ? "its own height" : $"{rows} rows")} ({height} px), {what}";
                                    void Fail(string why) => failures.Add($"{where}: {why}");
                                    var inside = new D2D_RECT_F(form.Inset, 0, width - form.Inset, MathF.Min(height, row));
                                    if (!Shown(plan.Action) || MathF.Abs(plan.Action.right - (width - form.Inset)) > 1 || !Inside(plan.Action, inside))
                                    {
                                        Fail($"its start button {Place(plan.Action)} is not on the trailing gutter of its row");
                                        continue;
                                    }
                                    var middle = new System.Numerics.Vector2((plan.Action.left + plan.Action.right) / 2, (plan.Action.top + plan.Action.bottom) / 2);
                                    if (tile.CardTarget(middle, box, style, WidgetSurface.List) != (int)CountdownTargetKind.Action)
                                    {
                                        Fail("a click on its start button does not reach it");
                                    }
                                    if (start is { } before && (MathF.Abs(before.left - plan.Action.left) > 0.5f || MathF.Abs(before.top - plan.Action.top) > 0.5f))
                                    {
                                        Fail($"its start button moved from {Place(before)} to {Place(plan.Action)}");
                                    }
                                    start ??= plan.Action;
                                    if (!pieces.Any(piece => piece.Name == "time" && piece.Text.Length > 0))
                                    {
                                        Fail("it shows no time");
                                    }
                                    if (pieces.Where(piece => piece.Name == "name").Select(piece => piece.Text).FirstOrDefault() is { } said && name.Length > 0 && said != name)
                                    {
                                        Fail($"it says \"{said}\", not the name it was given");
                                    }
                                    var drawn = pieces.Where(piece => piece.Name is not "meter").ToList();
                                    foreach (var piece in drawn)
                                    {
                                        if (!Inside(piece.Box, inside))
                                        {
                                            Fail($"its {piece.Name} {Place(piece.Box)} is outside its row between the gutters {Place(inside)}");
                                        }
                                    }
                                    for (var one = 0; one < drawn.Count; one++)
                                    {
                                        for (var other = one + 1; other < drawn.Count; other++)
                                        {
                                            if (Overlap(drawn[one].Box, drawn[other].Box))
                                            {
                                                Fail($"its {drawn[one].Name} and its {drawn[other].Name} overlap");
                                            }
                                        }
                                    }
                                    if (pieces.Where(piece => piece.Name == "glyph").Select(piece => (D2D_RECT_F?)piece.Box).FirstOrDefault() is not { } glyph
                                        || (glyph.left + glyph.right) / 2 < form.Inset || (glyph.left + glyph.right) / 2 > form.Inset + form.Icon)
                                    {
                                        Fail("its glyph is not in the list's leading column");
                                    }
                                }
                            }
                        }
                    }
                }
            }
            Assert.True(failures.Count == 0, $"{failures.Count} failures, the first of them:\n{string.Join("\n", failures.Take(40))}");
        });
    }

    /// <summary>
    /// Its line says what it laid out, as every line of a list does (ILinePieces): the focus session's row at
    /// every height, its time, its steppers and its start button, every piece inside the line.
    /// </summary>
    [UiFact]
    public void ItsLineSaysWhatItLaidOut()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var look = LookFor(true);
            var style = FolderFlyout.StyleFor(1.5f, look, FlyoutTheme.For(look));
            var width = FolderFlyout.ListWidth(1.5f);
            using var tile = Tile(gfx, CountdownKind.Focus, Countdown.None, TimeSpan.FromMinutes(25), "Deep work");
            var line = (ILinePieces)tile;
            Assert.Equal("time", line.MainPiece);
            foreach (var rows in new[] { 1, 2, 6 })
            {
                var height = LineRows.Px(rows, style);
                var pieces = line.LinePieces(PopupPaint.Rect(0, 0, width, height), style);
                var names = pieces.Select(piece => piece.Name).ToList();
                Assert.Contains("time", names);
                Assert.Contains("start", names);
                Assert.True(names.Contains("minus") && names.Contains("plus"), $"no steppers at {rows} rows");
                Assert.Equal("25 min", pieces.Single(piece => piece.Name == "time").Text);
                Assert.All(pieces, piece => Assert.True(Inside(piece.Box, PopupPaint.Rect(0, 0, width, height)), $"{piece.Name} at {rows} rows runs out of its line"));
            }
        });
    }

    /// <summary>
    /// The time is the kit's Value, 20 DIP, at every height (§388, §401: a taller line shows more, never bigger
    /// type), at every scale: one line of the Value, set and running alike.
    /// </summary>
    [UiFact]
    public void TheTimeIsTheValueAtEveryHeight()
    {
        Offscreen.OnStaThread(gfx =>
        {
            foreach (var scale in new[] { 1f, 1.5f, 2f })
            {
                var look = LookFor(true);
                var style = new TileStyle(scale, look, FlyoutTheme.For(look), 1, 1);
                var width = FolderFlyout.ListWidth(scale);
                var value = TilePaint.LineHeight(gfx, style.Text(LineType.ValueDip), Weight.Semibold);
                foreach (var (what, state, length, _) in States(CountdownKind.Timer))
                {
                    using var tile = Tile(gfx, CountdownKind.Timer, state, length);
                    foreach (var rows in new[] { 1, 3, 6 })
                    {
                        var time = ((ILinePieces)tile).LinePieces(PopupPaint.Rect(0, 0, width, RowsPx(rows, scale, 1)), style).Single(piece => piece.Name == "time");
                        Assert.True(MathF.Abs(time.Box.bottom - time.Box.top - value) < 1, $"{what} at {rows} rows, {scale}x: its time is {time.Box.bottom - time.Box.top} tall, not the Value's {value}");
                    }
                }
            }
        });
    }

    /// <summary>
    /// A given name that is too long gives way as every line's name does: cut with an ellipsis where its first
    /// four letters still show, and left out where they do not — never swapped for its kind's, which its glyph
    /// already says, and never over the time. Where it would not keep four letters beside the steppers, the
    /// steppers fold into its menu first.
    /// </summary>
    [UiFact]
    public void AOneRowLinesNameIsCutWhereItStillReads()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var look = LookFor(true);
            var style = new TileStyle(1f, look, FlyoutTheme.For(look), 1, 1);
            var form = ListForm.Of(gfx, style);
            var height = CountdownLine.Height(style);
            const string name = "Sourdough, second rise";
            using var tile = Tile(gfx, CountdownKind.Timer, Countdown.None, TimeSpan.FromMinutes(45), name);
            IReadOnlyList<LinePiece> At(float width) => ((ILinePieces)tile).LinePieces(PopupPaint.Rect(0, 0, width, height), style);

            var wide = At(720);
            Assert.Equal(name, wide.Single(piece => piece.Name == "name").Text);
            Assert.Contains(wide, piece => piece.Name == "minus");

            var narrow = At(FolderFlyout.ListWidth(1f, FolderListWidth.Narrow));
            var cut = narrow.Single(piece => piece.Name == "name");
            var four = PopupPaint.Measure(gfx, "Sour…", form.TextPx, Weight.Regular).X;
            Assert.Equal(name, cut.Text);
            Assert.True(cut.Box.right - cut.Box.left < PopupPaint.Measure(gfx, name, form.TextPx, Weight.Regular).X, "it is not cut");
            Assert.True(cut.Box.right - cut.Box.left + 0.5f >= four, "it is cut to fewer than four letters");
            Assert.True(cut.Box.right <= narrow.Single(piece => piece.Name == "time").Box.left, "its name runs onto its time");

            // Where not even four letters fit beside its start button alone, no name at all: the glyph says what
            // it is, and the time keeps its room.
            var least = At(form.TextLeft + form.Inset + LineButton.Of(gfx, style).Height + (2 * style.Px(8))
                + PopupPaint.Measure(gfx, "88 min", style.Text(LineType.ValueDip), Weight.Semibold).X + four - 4);
            Assert.DoesNotContain(least, piece => piece.Name == "name");
            Assert.DoesNotContain(least, piece => piece.Name == "minus");
            Assert.Contains(least, piece => piece.Name == "time");
        });
    }

    /// <summary>
    /// What a line has no room for is its menu's (a folder's right-click): the steppers — a minute longer or
    /// shorter, doing nothing while it runs, and shown enabled only where they would change something — the
    /// start button, and the button that clears it, each on the widget's own countdown.
    /// </summary>
    [UiFact]
    public void ALineTooShortForItsButtonsHasThemInItsMenu()
    {
        Offscreen.OnStaThread(gfx =>
        {
            using var tile = Tile(gfx, CountdownKind.Timer, Countdown.None, TimeSpan.FromMinutes(2));
            Assert.Equal((true, true), tile.Steps);
            Assert.True(tile.Longer());
            Assert.Equal(3, tile.Settings.Countdown!.Minutes);
            Assert.True(tile.Shorter());
            Assert.True(tile.Shorter());
            Assert.Equal(1, tile.Settings.Countdown!.Minutes);
            // The least a length is: its minus changes nothing, and says so.
            Assert.Equal((false, true), tile.Steps);
            Assert.False(tile.Shorter());
            Assert.Equal(1, tile.Settings.Countdown!.Minutes);

            tile.Act();
            Assert.Equal(CountdownPhase.Running, tile.Phase);
            // Running, the length is the next run's and the steppers are not there: nothing.
            Assert.Equal((false, false), tile.Steps);
            Assert.False(tile.Longer());
            Assert.Equal(CountdownPhase.Running, tile.Phase);
            tile.Stop();
            Assert.Equal(CountdownPhase.Idle, tile.Phase);
            Assert.Equal(1, tile.Settings.Countdown!.Minutes);
        });
    }

    /// <summary>
    /// The list lines' sheets, at 150 %, dark: a sheet for each kind — list-timer.png and list-focus.png, beside
    /// the readings' list-&lt;kind&gt;.png in KARPO_LISTS_DIR, else %TEMP% — its line at the height it asks for
    /// and at one row to three down it, and a column for each state, named and not, each on the list's own
    /// ground as the open folder draws it.
    /// </summary>
    [UiFact]
    public void ListLineSheets()
    {
        Offscreen.OnStaThread(gfx =>
        {
            const float scale = 1.5f;
            var look = LookFor(true);
            var theme = FlyoutTheme.For(look);
            var pad = PopupPaint.Round(16 * scale);
            var label = PopupPaint.Round(12 * scale);
            var labelLine = TilePaint.LineHeight(gfx, label);
            var lineWidth = FolderFlyout.ListWidth(scale);
            var folder = Environment.GetEnvironmentVariable("KARPO_LISTS_DIR") is { Length: > 0 } named ? named : Path.GetTempPath();
            Directory.CreateDirectory(folder);
            var lineStyle = FolderFlyout.StyleFor(scale, look, theme);
            foreach (var kind in new[] { CountdownKind.Timer, CountdownKind.Focus })
            {
                var states = States(kind).Where(each => each.What != "an hour").ToList();
                var heights = new List<(string Label, float Height)> { ("its own height", CountdownLine.Height(lineStyle)) };
                heights.AddRange(Enumerable.Range(1, 3).Select(rows => ($"{rows} row{(rows == 1 ? "" : "s")}", RowsPx(rows, scale, 1))));
                var width = pad + (states.Count * (lineWidth + pad));
                var height = pad + heights.Sum(each => labelLine + each.Height + pad);
                var line = ListWidgets.Of(kind == CountdownKind.Focus ? ListKind.Focus : ListKind.Timer);
                var tiles = states.Select(each => Tile(gfx, kind, each.State, each.Length, each.Name)).ToList();
                tiles.ForEach(tile => tile.StandIn(line));
                try
                {
                    Offscreen.Bitmap(width, height, dc =>
                    {
                        PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, width, height), 0, PopupPaint.Ink(Color.FromArgb(255, 0x1C, 0x1C, 0x1C)));
                        var y = pad;
                        foreach (var (caption, lineHeight) in heights)
                        {
                            var x = pad;
                            for (var column = 0; column < tiles.Count; column++)
                            {
                                PopupPaint.Text(dc, gfx, column == 0 ? $"{caption} · {states[column].What}" : states[column].What, label, Weight.Regular, theme.TextSecondary, x, y);
                                var at = PopupPaint.Rect(x, y + labelLine, lineWidth, lineHeight);
                                PopupPaint.Fill(dc, at, PopupPaint.Round(8 * scale), PopupPaint.Ink(theme.Base));
                                PopupPaint.Stroke(dc, at, PopupPaint.Round(8 * scale), MathF.Max(1, MathF.Round(scale)), PopupPaint.Ink(theme.Stroke));
                                var tile = tiles[column];
                                var context = dc;
                                PopupPaint.Clipped(context, at, PopupPaint.Round(8 * scale), () => Offscreen.Shifted(context, at.left, at.top, () =>
                                    FolderFlyout.PaintContent(context, tile, PopupPaint.Rect(0, 0, lineWidth, lineHeight), lineStyle)));
                                x += lineWidth + pad;
                            }
                            y += labelLine + lineHeight + pad;
                        }
                    }, bitmap => Offscreen.Save(bitmap, Path.Combine(folder, $"list-{(kind == CountdownKind.Focus ? "focus" : "timer")}.png")));
                }
                finally
                {
                    foreach (var tile in tiles)
                    {
                        tile.Dispose();
                    }
                }
            }
        });
    }

    private static bool Shown(D2D_RECT_F rect) => rect.right > rect.left && rect.bottom > rect.top;

    private static bool Inside(D2D_RECT_F rect, D2D_RECT_F room) =>
        rect.left >= room.left - 0.5f && rect.top >= room.top - 0.5f && rect.right <= room.right + 0.5f && rect.bottom <= room.bottom + 0.5f;

    private static bool Overlap(D2D_RECT_F a, D2D_RECT_F b) =>
        Shown(a) && Shown(b) && a.left < b.right - 0.5f && b.left < a.right - 0.5f && a.top < b.bottom - 0.5f && b.top < a.bottom - 0.5f;

    private static string Place(D2D_RECT_F rect) => $"{rect.left},{rect.top},{rect.right},{rect.bottom}";
}
