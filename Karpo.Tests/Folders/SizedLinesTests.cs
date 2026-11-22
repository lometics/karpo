// A sized folder line is its form, measured, in whole cells, and its form fills them (docs\widgets.md §432, §442;
// Karpo.Core\LineSize.cs).
//
// The person, 2026-10-30: "try to design on the cell height as well, so that it is possible to actually make
// designs where the widgets combined give the same folder height." And of two such columns: "there are a lot of
// spaces in between the individual widgets. i want them to be smaller / more densly together" — "make the grid
// finer but also optimize the layouts of the widgets so that they then fill up the remaining space properly". So
// every size of every kind of one form or a few is a whole number of the folder's cells, half a row each, at every
// scale and text size — the fewest that hold what the line measures of its form — and the form fills them: drawn
// inside its line, from its first row, with no more air under it than over it (a cell's padding, never a band).
// Two lists of such lines that add up to the same cells are the same height, whatever the lines are.
//
// Every kind is drawn on the gutter tests' stand-ins (ListGutterTests.StoodIn): a frozen clock, the saved
// forecast, a sample battery, Security, sound, drives, a track, a share. Sheets (KARPO_PNG_DIR):
// sized-<compact|medium|full>.png, every sized kind at each size; sized-sizes-<group>.png, each kind's
// sizes side by side; sized-stacks-<dark|light>.png and sized-mixed-<dark|light>.png, two lists of the same cells.
// Needs Direct2D: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Tests.Core;
using Karpo.Tests.Forecasts;
using Karpo.Tests.Islands;
using Karpo.Tests.Perf;
using Xunit.Abstractions;
using static Karpo.Tests.Folders.ListFormPictures;
using static Karpo.Tests.Islands.Offscreen;

namespace Karpo.Tests.Folders;

[Collection(SamplerState.Collection)]
public sealed unsafe class SizedLinesTests(ITestOutputHelper output)
{
    private static readonly LineSize[] Sizes = [LineSize.Compact, LineSize.Medium, LineSize.Full];

    /// <summary>Every sized kind in each of its sizes.</summary>
    private static IEnumerable<(ListKind Kind, LineSize Size)> Cases =>
        LineSizeTests.Sized.SelectMany(kind => kind.Offered().Select(size => (kind, size)));

    /// <summary>A line of <paramref name="kind"/> with what it shows set (ListGutterTests.Made), in <paramref name="size"/>.</summary>
    private static CellItem Made(ListKind kind, LineSize size)
    {
        var line = ListGutterTests.Made(kind);
        line.Size = size;
        line.Normalize();
        return line;
    }

    /// <summary>
    /// Kinds whose fullest form says as much as the stand-in has, so its rows are not fixed in advance: a switch's
    /// description wraps to the rows it needs; a reading's facts are as many as this PC's samplers give.
    /// </summary>
    private static bool Varies(ListKind kind, LineSize size) =>
        size != LineSize.Compact && (LineSizeTests.Switches.Contains(kind)
            || kind is ListKind.CpuDetails or ListKind.GpuDetails or ListKind.MemoryDetails or ListKind.DiskDetails or ListKind.NetworkDetails);

    /// <summary>
    /// Every size of every sized kind, at 100 % and 150 % and at 100 % and 225 % text: measured (never "cannot
    /// say"); given the fewest whole cells that hold what it measured; Compact one row at the list's text; at 100 % the
    /// cells its kind declares where nothing measures; drawn inside its line from its first row; and filling its cells:
    /// no more air under its form than over it, give or take 4 DIP (a band of air at the foot is what made a column of
    /// them airy, §442).
    /// </summary>
    [UiFact]
    public void EverySizeIsWholeCellsAndItsFormFillsThem()
    {
        var failures = new List<string>();
        Drawn((gfx, size, folder) =>
        {
            foreach (var scale in (float[])[1f, 1.5f])
            {
                foreach (var textScale in TextScales)
                {
                    var look = LookFor(dark: true, textScale);
                    var (natural, plan) = Plan(folder, look, scale);
                    var cell = FolderFlyout.CellPx(scale, textScale);
                    for (var index = 0; index < folder.Tiles.Count; index++)
                    {
                        var kind = folder.Tiles[index].Cell.Island.List!.Value;
                        var at = $"{kind} {size} at {scale * 100:0} % scale, {textScale * 100:0} % text";
                        var measured = natural[index];
                        var given = Given(plan, index);
                        if (measured <= 0)
                        {
                            failures.Add($"{at}: measures nothing");
                            continue;
                        }
                        var cells = LineHeight.CellsFor(measured, cell);
                        if (MathF.Abs(given - (cells * cell)) > 0.5f)
                        {
                            failures.Add($"{at}: given {given:0} px, not {cells} whole cells ({cells * cell:0} px), the fewest that hold its {measured:0}");
                        }
                        // §590, §603: the split widgets say the same at both sizes; their Compact is the smaller
                        // arrangement of it, never one row.
                        if (size == LineSize.Compact && textScale == 1 && !LineSizeTests.Split.Contains(kind) && !LineSizeTests.TierSplit.Contains(kind) && MathF.Abs(given - (LineHeight.Row * cell)) > 0.5f)
                        {
                            failures.Add($"{at}: Compact is {given:0} px, not one row ({LineHeight.Row * cell:0})");
                        }
                        var declared = LineSizes.CellsOf(ListWidgets.Of(kind), size);
                        if (scale == 1 && textScale == 1 && !Varies(kind, size) && cells != declared)
                        {
                            failures.Add($"{at}: measures {measured:0} px, {cells} cells, where its kind declares {declared}");
                        }
                        var ink = Ink(folder, plan, index, look, scale);
                        output.WriteLine($"{at}: measures {measured:0}, given {given:0} ({cells} cells), ink {ink.Top:0}..{ink.Bottom:0}");
                        if (ink.Right <= ink.Left)
                        {
                            failures.Add($"{at}: draws nothing");
                            continue;
                        }
                        if (ink.Top > (LineHeight.Row * cell) + 0.5f)
                        {
                            failures.Add($"{at}: its first ink {ink.Top:0} px down, past its first row");
                        }
                        if (ink.Bottom > given + 0.5f)
                        {
                            failures.Add($"{at}: draws to {ink.Bottom:0} px, past its {given:0}");
                        }
                        // The ring's legend keeps a row for every piece it can have (the top apps and the rest), so
                        // its line does not change height as the day pointed at changes; on the stand-in's empty day
                        // that is air. The day bar keeps the line for how soon its next time is, which a large text
                        // leaves out where its words are wider than their room under the time (as the ring, so the
                        // line does not change height as the words do). §603: screen time today keeps the line for its
                        // average, and its history the band of its bars, both its size's whatever was counted; on the
                        // stand-in's empty log today has no average and the history says "No screen time" on its top row; the score,
                        // fetching nothing here, says why on its top row (SportsSplitTests draws its forms with an answer).
                        var under = given - ink.Bottom;
                        var slack = PopupPaint.Round(4 * scale * textScale);
                        var keeps = kind is ListKind.ScreenTimeChart or ListKind.ScreenTime or ListKind.ScreenTimeHistory or ListKind.Sports || (kind == ListKind.SunDayBar && textScale > 1);
                        if (under > ink.Top + slack && !keeps)
                        {
                            failures.Add($"{at}: {under:0} px of air under its form, {ink.Top:0} over it: it does not fill its {cells} cells");
                        }
                    }
                }
            }
            Sheet(gfx, $"sized-{size.ToString().ToLowerInvariant()}.png", 1f,
                [.. TextScales.Select(textScale => ($"{size}: 100 %, {textScale * 100:0} % text", LookFor(true, textScale), folder, Plan(folder, LookFor(true, textScale), 1f).Plan))]);
        });
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    /// <summary>
    /// A kind of a few forms says more the larger its size: what it measures grows from Compact to Full, Medium
    /// between where it has one (dawn and dusk, which have no note, are one form).
    /// </summary>
    [UiFact]
    public void ALargerSizeIsMore()
    {
        var measured = new Dictionary<(ListKind, LineSize), float>();
        Drawn((_, size, folder) =>
        {
            var (natural, _) = Plan(folder, LookFor(dark: true), 1f);
            for (var index = 0; index < folder.Tiles.Count; index++)
            {
                measured[(folder.Tiles[index].Cell.Island.List!.Value, size)] = natural[index];
            }
        });
        var failures = new List<string>();
        foreach (var kind in LineSizeTests.Sized.Where(kind => kind.Chooses()))
        {
            var said = kind.Offered().Select(size => (Size: size, Px: measured[(kind, size)])).ToList();
            output.WriteLine($"{kind}: {string.Join(", ", said.Select(each => $"{each.Size} {each.Px:0}"))}");
            for (var at = 1; at < said.Count; at++)
            {
                if (said[at].Px <= said[at - 1].Px + 0.5f)
                {
                    failures.Add($"{kind}: {said[at].Size} measures {said[at].Px:0}, not more than {said[at - 1].Size}'s {said[at - 1].Px:0}");
                }
            }
        }
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    /// <summary>
    /// Two lists of sized lines that add up to the same cells end level, in any order and whatever the lines are, at
    /// 100 %, 125 % and 150 % — lines stand flush and n cells are n times a cell (§442). The stacks: now playing, the
    /// battery, a timer, a clock and an alarm (3, 3, 2, 5, 3) beside a pin, Security's row, the volume, a drive and the
    /// time (2, 2, 4, 3, 5), 16 cells each; mixed: now playing whole, a battery's row, Security whole and a timer (6, 2,
    /// 7, 2) beside a clock, the volume large, an alarm and a pin (5, 7, 3, 2), 17 each. The sheets show them side by
    /// side. (At 225 % text a form may say less to fit its width — now playing keeps play alone — and the two need not
    /// add up then.)
    /// </summary>
    [UiFact]
    public void ListsOfTheSameCellsEndLevel()
    {
        (string Name, (ListKind Kind, LineSize Size)[][] Lists)[] sheets =
        [
            ("stacks", [
                [(ListKind.Media, LineSize.Medium), (ListKind.Battery, LineSize.Medium), (ListKind.Timer, LineSize.Full), (ListKind.Clock, LineSize.Full), (ListKind.Alarm, LineSize.Full)],
                [(ListKind.Pin, LineSize.Compact), (ListKind.Security, LineSize.Compact), (ListKind.SoundVolume, LineSize.Medium), (ListKind.Drive, LineSize.Full), (ListKind.TimeHeader, LineSize.Full)],
            ]),
            ("mixed", [
                [(ListKind.Media, LineSize.Full), (ListKind.Battery, LineSize.Compact), (ListKind.Security, LineSize.Full), (ListKind.Timer, LineSize.Full)],
                [(ListKind.Clock, LineSize.Full), (ListKind.SoundVolume, LineSize.Full), (ListKind.Alarm, LineSize.Full), (ListKind.Pin, LineSize.Compact)],
            ]),
        ];
        var failures = new List<string>();
        ListGutterTests.StoodIn<Gfx>(SavedWeather.Fixed, OnStaThread, gfx =>
        {
            foreach (var (name, lists) in sheets)
            {
                var holder = BarKind.FolderList.New();
                for (var list = 0; list < lists.Length; list++)
                {
                    if (list > 0)
                    {
                        holder.Folder!.Lists.Add([]);
                    }
                    foreach (var (kind, size) in lists[list])
                    {
                        Assert.NotNull(Karpo.Core.Folders.Add(BarKind.FolderList, holder.Folder!, Made(kind, size)));
                    }
                }
                var folder = new FolderIsland($"{Owner}/{holder.Id}", holder, gfx, new Config());
                ListGutterTests.Before(folder);
                folder.Opened(gfx);
                ListGutterTests.Ready(folder);
                try
                {
                    foreach (var (scale, textScale) in new[] { (1f, 1f), (1.25f, 1f), (1.5f, 1f), (1f, 2.25f) })
                    {
                        var (_, plan) = Plan(folder, LookFor(dark: true, textScale), scale);
                        var tall = plan.Lists.Select(list => list.Box.bottom - list.Box.top).ToList();
                        var cell = FolderFlyout.CellPx(scale, textScale);
                        output.WriteLine($"{name} at {scale * 100:0} %, {textScale * 100:0} % text: lists {string.Join(", ", tall.Select(each => $"{each:0} ({each / cell:0.##} cells)"))}");
                        if (textScale == 1 && MathF.Abs(tall[0] - tall[1]) > 0.5f)
                        {
                            failures.Add($"{name} at {scale * 100:0} %, {textScale * 100:0} % text: the lists are {tall[0]:0} and {tall[1]:0} px, though their lines are the same cells");
                        }
                    }
                    foreach (var dark in new[] { true, false })
                    {
                        var cells = Plan(folder, LookFor(dark), 1.5f).Plan.Lists[0] is var first ? (first.Box.bottom - first.Box.top) / FolderFlyout.CellPx(1.5f, 1f) : 0;
                        Sheet(gfx, $"sized-{name}-{(dark ? "dark" : "light")}.png", 1.5f,
                            [($"{cells:0} cells beside {cells:0} cells: 150 %", LookFor(dark), folder, Plan(folder, LookFor(dark), 1.5f).Plan)]);
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

    /// <summary>Each kind of a few forms in its sizes side by side, a sheet a group, dark at 150 %.</summary>
    [UiFact]
    public void EachKindsSizesSideBySide()
    {
        (string Name, ListKind[] Kinds)[] groups =
        [
            ("media-time", [ListKind.Media, ListKind.Alarm, ListKind.Clock, ListKind.TimeHeader, ListKind.ScreenTimeChart]),
            ("system", [ListKind.Battery, ListKind.Security, ListKind.Drive, ListKind.NetworkDrive, ListKind.PowerMode, ListKind.SoundVolume, ListKind.Brightness]),
            ("readings", [ListKind.CpuDetails, ListKind.GpuDetails, ListKind.MemoryDetails, ListKind.DiskDetails, ListKind.NetworkDetails, ListKind.Markets]),
            ("sun-pin", [ListKind.Sunrise, ListKind.Sunset, ListKind.Daylight, ListKind.SolarNoon, ListKind.Dawn, ListKind.Dusk, ListKind.SunDayBar, ListKind.Pin]),
            ("switches", [ListKind.WifiSwitch, ListKind.BluetoothSwitch, ListKind.GameMode, ListKind.ColorFilters, ListKind.AirplaneMode, ListKind.EnergySaver, ListKind.DarkMode, ListKind.SpatialSound, ListKind.NightLight, ListKind.KeepAwake]),
            ("one-form", [.. LineSizeTests.OneForm]),
        ];
        ListGutterTests.StoodIn<Gfx>(SavedWeather.Fixed, OnStaThread, gfx =>
        {
            foreach (var (name, kinds) in groups)
            {
                var folders = new List<(LineSize Size, FolderIsland Folder)>();
                try
                {
                    foreach (var size in Sizes.Where(size => kinds.Any(kind => kind.Offered().Contains(size))))
                    {
                        var folder = ListFormPictures.Folder(gfx, ListGutterTests.Before, [.. kinds.Where(kind => kind.Offered().Contains(size)).Select(kind => Made(kind, size))]);
                        ListGutterTests.Ready(folder);
                        folders.Add((size, folder));
                    }
                    var look = LookFor(dark: true);
                    Sheet(gfx, $"sized-sizes-{name}.png", 1.5f, [.. folders.Select(each => ($"{each.Size}: 150 %", look, each.Folder, Plan(each.Folder, look, 1.5f).Plan))]);
                }
                finally
                {
                    foreach (var (_, folder) in folders)
                    {
                        folder.Closed();
                        folder.Dispose();
                    }
                }
            }
        });
    }

    /// <summary>A folder of every sized kind in <c>size</c> — a kind without it is left out — made and handed to <paramref name="use"/>, size by size.</summary>
    private static void Drawn(Action<Gfx, LineSize, FolderIsland> use) =>
        ListGutterTests.StoodIn<Gfx>(SavedWeather.Fixed, OnStaThread, gfx =>
        {
            foreach (var size in Sizes)
            {
                var folder = ListFormPictures.Folder(gfx, ListGutterTests.Before, [.. Cases.Where(each => each.Size == size).Select(each => Made(each.Kind, each.Size))]);
                ListGutterTests.Ready(folder);
                try
                {
                    use(gfx, size, folder);
                }
                finally
                {
                    folder.Closed();
                    folder.Dispose();
                }
            }
        });
}
