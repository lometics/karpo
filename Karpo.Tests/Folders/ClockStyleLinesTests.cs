// A clock's styles as lines (docs\widgets.md §443; Karpo.Core\ClockStyles.cs). The person, 2026-10-30: "the clock should
// also have the mode where it just shows the time without the date. add some more clock styles as well. (digital, two
// numbers etc...)". Each look in each size, at 100 and 150 % and 100 and 225 % text, this PC's clock and one of another
// zone with a label: its parts inside its line, what the look shows and nothing it does not (Time only no date, Analog
// no digits, Stacked the hours over the minutes), and its form filling its whole cells (§442). Sheets (KARPO_PNG_DIR):
// clock-styles-<dark|light>.png, every look Compact and Full at 150 %, and clock-styles-225.png at the largest text.
// Needs Direct2D: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Tests.Islands;
using Karpo.Tests.Perf;
using Karpo.Tests.Forecasts;
using Xunit.Abstractions;
using static Karpo.Tests.Folders.ListFormPictures;
using static Karpo.Tests.Islands.Offscreen;

namespace Karpo.Tests.Folders;

[Collection(SamplerState.Collection)]
public sealed unsafe class ClockStyleLinesTests(ITestOutputHelper output)
{
    private static readonly LineSize[] Sizes = [LineSize.Compact, LineSize.Full];

    /// <summary>A clock line in <paramref name="style"/> and <paramref name="size"/>: this PC's, or Tokyo's with its label.</summary>
    private static CellItem Made(ClockStyle style, LineSize size, bool zone, bool seconds = false) => Line(ListKind.Clock, line =>
    {
        line.Size = size;
        line.Clock!.Style = style;
        line.Clock.Seconds = seconds;
        if (zone)
        {
            line.Clock.TimeZone = "Tokyo Standard Time";
            line.Clock.Label = "Tokyo";
        }
    });

    /// <summary>Every look in every size, this PC's and Tokyo's, with and without seconds.</summary>
    private static IEnumerable<(ClockStyle Style, LineSize Size, bool Zone, bool Seconds)> Cases =>
        from style in Enum.GetValues<ClockStyle>()
        from size in Sizes
        from zone in new[] { false, true }
        from seconds in new[] { false, true }
        select (style, size, zone, seconds);

    private static string Of((ClockStyle Style, LineSize Size, bool Zone, bool Seconds) each) =>
        $"{each.Style} {each.Size}{(each.Zone ? " Tokyo" : "")}{(each.Seconds ? " with seconds" : "")}";

    /// <summary>
    /// Each look draws its parts inside its line and shows what it is: Time only a time and never a date; Analog a face
    /// and no time in figures; Stacked the hours over the minutes; Analog and digital the time, the date and the face in
    /// Full. A clock of another zone says where it stands. Compact is one row at the list's text.
    /// </summary>
    [UiFact]
    public void EachLooksPartsAreInsideItsLineAndItShowsWhatItIs()
    {
        var failures = new List<string>();
        ListGutterTests.StoodIn<Gfx>(SavedWeather.Fixed, OnStaThread, gfx =>
        {
            foreach (var each in Cases)
            {
                using var tile = Karpo.Dock.Islands.CreateLine(Config.IslandKey(Owner, "c"), Made(each.Style, each.Size, each.Zone, each.Seconds), ListKind.Clock, gfx, new Config())!;
                foreach (var scale in (float[])[1f, 1.5f])
                {
                    foreach (var textScale in TextScales)
                    {
                        var look = LookFor(dark: true, textScale);
                        var style = FolderFlyout.StyleFor(scale, look, FlyoutTheme.For(look));
                        var width = FolderFlyout.ListWidth(scale);
                        var at = $"{Of(each)} at {scale * 100:0} %, {textScale * 100:0} % text";
                        var height = ((IListContent)tile).ListHeight(width, style);
                        if (each.Size == LineSize.Compact && MathF.Abs(height - LineRows.Px(1, style)) > 0.5f)
                        {
                            failures.Add($"{at}: Compact is {height:0} px, not one row ({LineRows.Px(1, style):0})");
                        }
                        var pieces = ((ILinePieces)tile).LinePieces(PopupPaint.Rect(0, 0, width, height), style)
                            .Where(piece => piece.Box.right > piece.Box.left && piece.Box.bottom > piece.Box.top).ToList();
                        var names = pieces.Select(piece => piece.Name).ToHashSet();
                        output.WriteLine($"{at}: {height:0} px, {string.Join(", ", pieces.Select(piece => $"{piece.Name} {piece.Box.left:0},{piece.Box.top:0}..{piece.Box.right:0},{piece.Box.bottom:0}"))}");
                        foreach (var piece in pieces.Where(piece => piece.Box.left < -0.5f || piece.Box.top < -0.5f || piece.Box.right > width + 0.5f || piece.Box.bottom > height + 0.5f))
                        {
                            failures.Add($"{at}: its {piece.Name} ({piece.Box.left:0},{piece.Box.top:0}..{piece.Box.right:0},{piece.Box.bottom:0}) leaves its {width:0} by {height:0} line");
                        }
                        void Needs(string name)
                        {
                            if (!names.Contains(name))
                            {
                                failures.Add($"{at}: no {name}");
                            }
                        }
                        void Never(string name)
                        {
                            if (names.Contains(name))
                            {
                                failures.Add($"{at}: a {name}, which it does not show");
                            }
                        }
                        switch (each.Style)
                        {
                            case ClockStyle.TimeOnly:
                                Needs("time");
                                Never("date");
                                Never("dial");
                                break;
                            case ClockStyle.Analog:
                                Needs("dial");
                                Never("time");
                                Never("hours");
                                break;
                            case ClockStyle.Stacked:
                                Needs("hours");
                                Needs("minutes");
                                Never("time");
                                if (pieces.FirstOrDefault(piece => piece.Name == "hours") is { Name: not null } hours
                                    && pieces.FirstOrDefault(piece => piece.Name == "minutes") is { Name: not null } minutes
                                    && (minutes.Box.top + minutes.Box.bottom) / 2 <= (hours.Box.top + hours.Box.bottom) / 2 + 1)
                                {
                                    failures.Add($"{at}: its minutes are not under its hours");
                                }
                                break;
                            case ClockStyle.AnalogAndDigital:
                                Needs("time");
                                if (each.Size == LineSize.Full)
                                {
                                    Needs("date");
                                    // At the largest text its time and date take the line's width, and the face gives way (§401).
                                    if (textScale == 1)
                                    {
                                        Needs("dial");
                                    }
                                }
                                break;
                        }
                        // Another zone's clock says where it stands, in every look and size; on one row at the largest text
                        // the time may leave no room for its words.
                        if (each.Zone && !names.Contains("place") && (textScale == 1 || each.Size == LineSize.Full))
                        {
                            failures.Add($"{at}: Tokyo's clock does not say Tokyo");
                        }
                    }
                }
            }
        });
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    /// <summary>
    /// In a folder each look is given the whole cells that hold it and fills them (§442): drawn inside them, its first
    /// ink in its first row, and no more air under its form than over it, give or take 4 DIP. The sheets show each
    /// look Compact and Full, this PC's clock over Tokyo's.
    /// </summary>
    [UiFact]
    public void EachLookFillsItsCells()
    {
        var failures = new List<string>();
        ListGutterTests.StoodIn<Gfx>(SavedWeather.Fixed, OnStaThread, gfx =>
        {
            var folders = new List<(LineSize Size, FolderIsland Folder)>();
            try
            {
                foreach (var size in Sizes)
                {
                    var cases = Cases.Where(each => each.Size == size && !each.Seconds).ToList();
                    var folder = Folder(gfx, ListGutterTests.Before, [.. cases.Select(each => Made(each.Style, each.Size, each.Zone))]);
                    folders.Add((size, folder));
                    foreach (var scale in (float[])[1f, 1.5f])
                    {
                        foreach (var textScale in TextScales)
                        {
                            var look = LookFor(dark: true, textScale);
                            var (natural, plan) = Plan(folder, look, scale);
                            var cell = FolderFlyout.CellPx(scale, textScale);
                            for (var index = 0; index < cases.Count; index++)
                            {
                                var at = $"{Of(cases[index])} at {scale * 100:0} %, {textScale * 100:0} % text";
                                var given = Given(plan, index);
                                var cells = LineHeight.CellsFor(natural[index], cell);
                                if (MathF.Abs(given - (cells * cell)) > 0.5f)
                                {
                                    failures.Add($"{at}: given {given:0} px, not the {cells} whole cells that hold its {natural[index]:0}");
                                }
                                var ink = Ink(folder, plan, index, look, scale);
                                if (ink.Right <= ink.Left)
                                {
                                    failures.Add($"{at}: draws nothing");
                                    continue;
                                }
                                if (ink.Top > (LineHeight.Row * cell) + 0.5f || ink.Bottom > given + 0.5f)
                                {
                                    failures.Add($"{at}: its ink {ink.Top:0}..{ink.Bottom:0} px, outside its {given:0}");
                                }
                                var under = given - ink.Bottom;
                                if (under > ink.Top + PopupPaint.Round(4 * scale * textScale))
                                {
                                    failures.Add($"{at}: {under:0} px of air under its form, {ink.Top:0} over it: it does not fill its {cells} cells");
                                }
                            }
                        }
                    }
                }
                foreach (var dark in new[] { true, false })
                {
                    var look = LookFor(dark);
                    Sheet(gfx, $"clock-styles-{(dark ? "dark" : "light")}.png", 1.5f,
                        [.. folders.Select(each => ($"{each.Size}: 150 %", look, each.Folder, Plan(each.Folder, look, 1.5f).Plan))]);
                }
                var large = LookFor(dark: true, 2.25f);
                Sheet(gfx, "clock-styles-225.png", 1f,
                    [.. folders.Select(each => ($"{each.Size}: 100 %, 225 % text", large, each.Folder, Plan(each.Folder, large, 1f).Plan))]);
            }
            finally
            {
                foreach (var (_, folder) in folders)
                {
                    folder.Closed();
                    folder.Dispose();
                }
            }
        });
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    /// <summary>The time split into what Stacked stands one over the other, in the region's own format: 24 and 12 hours, the mark before or after.</summary>
    [Theory]
    [InlineData("14:20", "14", "20", "", "")]
    [InlineData("14:20:07", "14", "20", "07", "")]
    [InlineData("2:20 PM", "2", "20", "", "PM")]
    [InlineData("午後 2:20", "2", "20", "", "午後")]
    [InlineData("14.20", "14", "20", "", "")]
    public void StackedSplitsTheTime(string time, string hours, string minutes, string seconds, string mark)
    {
        var split = ClockTile.Split(time);
        Assert.Equal((hours, minutes, seconds, mark), (split.Hours, split.Minutes, split.Seconds, split.Mark));
    }
}
