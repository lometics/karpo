// The alarm as a line of a list at its own height and at one row to six (Karpo.Dock\Islands\AlarmTile.cs),
// as the stopwatch's is tested (StopwatchFormsTests): its pieces stay between the list's gutters and
// off one another in every state, its switch and its ringing buttons take a click, and the sheets.
//
// Everything here draws or measures text, so it runs with KARPO_UI_TESTS=1; the sheets go to
// KARPO_PNG_DIR. Every line runs on a book of the test's own and a clock the test sets.

using System.Numerics;
using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using Color = Windows.UI.Color;

namespace Karpo.Tests.Calendars;

public sealed unsafe class AlarmFormsTests
{
    /// <summary>Monday 12 October 2026, 08:00 UTC: what every line is read at.</summary>
    private static readonly DateTime Now = new(2026, 10, 12, 8, 0, 0, DateTimeKind.Utc);

    private static Look LookFor(bool dark, float textScale = 1f) => new(
        Glass: false,
        Dark: dark,
        Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4),
        AccentLight2: Color.FromArgb(255, 0x99, 0xEB, 0xFF),
        AccentDark1: Color.FromArgb(255, 0x00, 0x5F, 0xB8),
        TextScale: textScale);

    /// <summary>The minute before <see cref="Now"/> on this PC's wall clock, as an alarm's time: one set to it rings at <see cref="Now"/>.</summary>
    private static string MinuteBefore => $"{TimeZoneInfo.ConvertTimeFromUtc(Now.AddMinutes(-1), TimeZoneInfo.Local):HH:mm}";

    /// <summary>A 12-hour clock, the wider of the two.</summary>
    private static string Twelve(DateTime at) => at.ToString("h:mm tt", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>What an alarm can be doing, each on a book of its own: its options, and what is done to it before it is read.</summary>
    private static (string What, Action<AlarmOptions> Set, Action<AlarmBook, AlarmOptions>? Then)[] States() =>
    [
        ("off", options => options.On = false, null),
        ("tomorrow", options => options.Time = "06:30", null),
        ("weekdays", options => options.Days = ["mon", "tue", "wed", "thu", "fri"], null),
        ("named, some days", options => { options.Name = "Gym before work"; options.Days = ["tue", "thu", "sat"]; }, null),
        ("ringing", options => options.Time = MinuteBefore, (book, _) => book.Tick(Now)),
        ("snoozed", options => options.Time = MinuteBefore, (book, _) => { book.Tick(Now); book.Snooze("probe", Now); }),
    ];

    /// <summary>An alarm line on a book of its own, at 07:00 and on unless <paramref name="set"/> says otherwise, set a minute before <see cref="Now"/>.</summary>
    private static AlarmTile Tile(Gfx gfx, Action<AlarmOptions>? set = null, Action<AlarmBook, AlarmOptions>? then = null, Countdowns? countdowns = null)
    {
        countdowns ??= new Countdowns();
        var item = ListKind.Alarm.New();
        item.Id = "probe";
        item.Normalize();
        item.Alarm!.On = true;
        set?.Invoke(item.Alarm);
        item.Alarm.Normalize();
        countdowns.Alarms.Sync([("probe", item.Alarm)], Now.AddMinutes(-2));
        then?.Invoke(countdowns.Alarms, item.Alarm);
        return new AlarmTile(Config.IslandKey(null, "probe"), item, gfx, new Config(), countdowns, () => Now, Twelve);
    }

    private static float RowsPx(int rows, float scale, float textScale) => Karpo.Tests.Folders.ListFormPictures.Rows(rows, scale, textScale);

    /// <summary>
    /// A line at its own height and at one row to six, at 100, 150 and 200 %, every text size, both
    /// list widths, every state: everything between the gutters and inside the line, nothing on
    /// anything else, the switch — or Snooze and Dismiss while it rings — reached by a click. At the
    /// list's text the time is whole; from a row and its caption it says what it will do (§401).
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
                    foreach (var listWidth in FolderListWidths.All)
                    {
                        var width = FolderFlyout.ListWidth(scale, listWidth);
                        for (var rows = 0; rows <= 6; rows++)
                        {
                            foreach (var (what, set, then) in States())
                            {
                                using var tile = Tile(gfx, set, then);
                                var height = rows == 0 ? tile.ListHeight(width, style) : RowsPx(rows, scale, textScale);
                                var box = PopupPaint.Rect(0, 0, width, height);
                                var inside = new D2D_RECT_F(form.Inset - 0.5f, -0.5f, width - form.Inset + 0.5f, height + 0.5f);
                                var line = tile.Draw(new LineSketch(null, gfx), box, style);
                                var where = $"{scale:0.##}x, text {textScale:0.##}, {listWidth} ({width} px), {(rows == 0 ? "its own height" : $"{rows} rows")}, {what}";
                                void Fail(string why) => failures.Add($"{where}: {why}");

                                var ringing = what == "ringing";
                                var pieces = tile.LinePieces(box, style);
                                foreach (var piece in pieces)
                                {
                                    if (!Inside(piece.Box, inside))
                                    {
                                        Fail($"its {piece.Name} {Place(piece.Box)} is outside the line {Place(inside)}");
                                    }
                                }
                                for (var one = 0; one < pieces.Count; one++)
                                {
                                    for (var other = one + 1; other < pieces.Count; other++)
                                    {
                                        if (Overlap(pieces[one].Box, pieces[other].Box))
                                        {
                                            Fail($"its {pieces[one].Name} and its {pieces[other].Name} overlap");
                                        }
                                    }
                                }
                                var targets = ringing
                                    ? new[] { (line.Snooze, AlarmTargetKind.Snooze), (line.Dismiss, AlarmTargetKind.Dismiss) }
                                    : what == "snoozed" ? [(line.Dismiss, AlarmTargetKind.Dismiss)]
                                    : [(line.Switch, AlarmTargetKind.Switch)];
                                foreach (var (part, kind) in targets)
                                {
                                    if (part.right <= part.left)
                                    {
                                        Fail($"it has no {kind}");
                                        continue;
                                    }
                                    var middle = new Vector2((part.left + part.right) / 2, (part.top + part.bottom) / 2);
                                    if (tile.CardTarget(middle, box, style, WidgetSurface.List) != (int)kind)
                                    {
                                        Fail($"a click on its {kind} does not reach it");
                                    }
                                }
                                if (tile.CardTarget(new Vector2(form.TextLeft, height / 2), box, style, WidgetSurface.List) != 0)
                                {
                                    Fail("a click on its time does something");
                                }
                                // The time is the line's reading: whole, in the kit's Value (§401), at the list's text.
                                if (textScale == 1 && pieces.FirstOrDefault(piece => piece.Name == "time") is { Name: "time" } time
                                    && time.Box.right - time.Box.left + 0.5f < PopupPaint.Measure(gfx, time.Text, style.Text(LineType.ValueDip), Weight.Semibold).X)
                                {
                                    Fail($"its time \"{time.Text}\" is cut");
                                }
                                // Its caption from a row and its caption up: what it is doing and when it rings.
                                var captioned = height + 0.5f >= LineRow.Height(style, caption: true);
                                if (textScale == 1 && captioned != pieces.Any(piece => piece.Name == "hint"))
                                {
                                    Fail(captioned ? "it does not say what it will do" : "a line of one row has a caption");
                                }
                                if (textScale == 1 && ringing && listWidth == FolderListWidth.Medium && !line.Worded)
                                {
                                    Fail("its Snooze and Dismiss are not worded on the usual width");
                                }
                                // The row at the line's top: nothing under it.
                                if (pieces.Any(piece => piece.Box.bottom > line.Row.Bottom + 0.5f))
                                {
                                    Fail("something stands under its row");
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
    /// Its switch on the line sets it and turns it off, in its options and the book; ringing, Snooze
    /// and Dismiss work it; a click beside them does nothing. It asks for no tick of its own.
    /// </summary>
    [UiFact]
    public void ItsSwitchAndButtonsWorkIt()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var countdowns = new Countdowns();
            using var tile = Tile(gfx, options => options.On = false, countdowns: countdowns);
            Assert.Equal(0, tile.IntervalMs);
            Assert.Equal(AlarmPhase.Off, tile.Status.Phase);
            _ = tile.Click((int)AlarmTargetKind.None);
            Assert.False(tile.Options.On);

            _ = tile.Click((int)AlarmTargetKind.Switch);
            Assert.True(tile.Options.On);
            Assert.Equal(AlarmPhase.Set, tile.Status.Phase);
            Assert.Contains(tile.Hint(tile.Status), new[] { S.DockAlarmToday, S.DockAlarmTomorrow });

            countdowns.Alarms.Tick(tile.Status.Next!.Value);
            Assert.True(countdowns.Alarms.Ringing);
            _ = tile.Click((int)AlarmTargetKind.Snooze);
            Assert.Equal(AlarmPhase.Snoozed, countdowns.Alarms.Of("probe", tile.Options, Now).Phase);
            _ = tile.Click((int)AlarmTargetKind.Dismiss);
            Assert.False(countdowns.Alarms.Ringing);
            // It rang once and does not repeat: it is off.
            Assert.False(tile.Options.On);
        });
    }

    // --- Sheets --------------------------------------------------------------------------

    /// <summary>A line of a list at its own height and at one row to six, in every state, dark and light, at 100 and 150 %, at large text, and in high contrast.</summary>
    [UiFact]
    public void AlarmSheets()
    {
        Offscreen.OnStaThread(gfx =>
        {
            foreach (var scale in new[] { 1f, 1.5f })
            {
                foreach (var dark in new[] { true, false })
                {
                    Lines(gfx, Path.Combine(Offscreen.Pictures, $"alarm-line-{(int)(scale * 100)}-{(dark ? "dark" : "light")}.png"), LookFor(dark), scale);
                }
            }
            Lines(gfx, Path.Combine(Offscreen.Pictures, "alarm-line-100-text225.png"), LookFor(true, 2.25f), 1f);
            var contrast = LookFor(true) with
            {
                HighContrast = true,
                HcWindow = Color.FromArgb(255, 0, 0, 0),
                HcText = Color.FromArgb(255, 255, 255, 255),
                HcHighlight = Color.FromArgb(255, 0x1A, 0xEB, 0xFF),
                HcHighlightText = Color.FromArgb(255, 0, 0, 0),
            };
            Lines(gfx, Path.Combine(Offscreen.Pictures, "alarm-line-150-contrast.png"), contrast, 1.5f);
        });
    }

    private static void Lines(Gfx gfx, string path, Look look, float scale)
    {
        var theme = FlyoutTheme.For(look);
        var pad = PopupPaint.Round(12 * scale);
        var label = PopupPaint.Round(11 * scale);
        var labelLine = TilePaint.LineHeight(gfx, label);
        var lineWidth = FolderFlyout.ListWidth(scale);
        var style = FolderFlyout.StyleFor(scale, look, theme);
        var states = States();
        float Own()
        {
            using var tile = Tile(gfx);
            return tile.ListHeight(lineWidth, style);
        }
        var heights = new List<(string Label, float Height)> { ("its own height", Own()) };
        heights.AddRange(Enumerable.Range(1, 6).Select(rows => ($"{rows} row{(rows == 1 ? "" : "s")}", RowsPx(rows, scale, style.TextScale))));
        var width = pad + (states.Length * (lineWidth + pad));
        var height = pad + heights.Sum(each => labelLine + each.Height + pad);
        Offscreen.Bitmap(width, height, dc =>
        {
            PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, width, height), 0, PopupPaint.Ink(theme.Base));
            var y = pad;
            foreach (var (caption, lineHeight) in heights)
            {
                var x = pad;
                for (var column = 0; column < states.Length; column++)
                {
                    using var tile = Tile(gfx, states[column].Set, states[column].Then);
                    PopupPaint.Text(dc, gfx, column == 0 ? $"{caption} · {states[column].What}" : states[column].What, label, Weight.Regular, theme.TextSecondary, x, y);
                    var at = PopupPaint.Rect(x, y + labelLine, lineWidth, lineHeight);
                    PopupPaint.Stroke(dc, at, PopupPaint.Round(8 * scale), MathF.Max(1, MathF.Round(scale)), PopupPaint.Ink(theme.Stroke));
                    var context = dc;
                    Offscreen.Shifted(context, at.left, at.top, () => tile.PaintCard(context, PopupPaint.Rect(0, 0, lineWidth, lineHeight), style, WidgetSurface.List));
                    x += lineWidth + pad;
                }
                y += labelLine + lineHeight + pad;
            }
        }, bitmap => Offscreen.Save(bitmap, path));
    }

    private static bool Inside(D2D_RECT_F rect, D2D_RECT_F room) =>
        rect.left >= room.left && rect.top >= room.top && rect.right <= room.right && rect.bottom <= room.bottom;

    private static bool Overlap(D2D_RECT_F a, D2D_RECT_F b) =>
        a.right > a.left && b.right > b.left && a.left < b.right - 0.5f && b.left < a.right - 0.5f && a.top < b.bottom - 0.5f && b.top < a.bottom - 0.5f;

    private static string Place(D2D_RECT_F rect) => $"{rect.left},{rect.top},{rect.right},{rect.bottom}";
}
