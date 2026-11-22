// A folder widget at each of its heights, drawn (docs\widgets.md §76): Automatic as it starts,
// Automatic held to a small most, and Fixed at one, two, four and eight rows — for the kinds whose
// content is a grid (the toggles, the month), a list that grows (the agenda, the apps of screen time),
// a graph (screen time's total) and a fixed piece of chrome (what is playing).
//
// Each line is asserted in pixels — the height the plan gives it is what its LineHeight says for the
// content it measured — and drawn twice: on the sheet, inside the folder's clip, as the open folder
// draws it; and alone with room round it, where it must keep inside its box. A line too short for
// its content shortens itself — whole rows of toggles, whole weeks of the month, fewer events and
// "N other events", a smaller tier — rather than being cut mid-piece by the folder's clip (the
// toggles row did that at one row, before it learned to drop or squeeze its rows).
//
// Sheets: line-height-<kind>-<dark|light>.png in KARPO_PNG_DIR.

using Karpo.Dock;
using Karpo.Dock.QuickSettings.Services;
using Karpo.Tests.Folders;
using Karpo.Tests.Islands;
using Xunit.Abstractions;
using static Karpo.Tests.Folders.ListFormPictures;
using static Karpo.Tests.Islands.Offscreen;

namespace Karpo.Tests.Sizing;

public sealed unsafe class LineHeightPicturesTests(ITestOutputHelper output)
{
    /// <summary>The heights every kind is shown at, as the card would set them.</summary>
    private static readonly (string Name, LineHeight? Height)[] Heights =
    [
        ("Automatic", null),
        ("Automatic, up to 2", LineHeight.Automatic(4)),
        ("Fixed, 1", LineHeight.Exactly(2)),
        ("Fixed, 2", LineHeight.Exactly(4)),
        ("Fixed, 4", LineHeight.Exactly(8)),
        ("Fixed, 8", LineHeight.Exactly(16)),
    ];

    private static readonly DateOnly Today = new(2026, 10, 9);

    /// <summary>One kind: its line, and what it needs set on the drawing thread first.</summary>
    private sealed record Kind(string Name, ListKind List, Action Fakes, Action<FolderIsland>? Before = null, DateTime? At = null);

    public static TheoryData<string> Kinds => [.. All.Select(kind => kind.Name)];

    private static readonly Kind[] All =
    [
        new("toggles", ListKind.Toggles, () => ToggleSwitches.Source = toggle =>
            new SampleSwitch(Toggles.Find(toggle)!.Name, on: toggle is Toggle.Wifi or Toggle.Bluetooth, switches: true)),
        new("month", ListKind.Month, () => { }),
        new("agenda", ListKind.Calendar, () => CalendarIsland.Made = ListFormCases.Store(ListFormCases.Busy), At: ListFormCases.Frozen),
        new("media", ListKind.Media, () => MediaIsland.Stand = new MediaStand()),
        new("screentime", ListKind.ScreenTime, () => { }, ScreenTime),
        new("screentime-apps", ListKind.ScreenTimeApps, () => { }, ScreenTime),
        new("screentime-chart", ListKind.ScreenTimeChart, () => { }, ScreenTime),
    ];

    private static void ScreenTime(FolderIsland folder)
    {
        var days = Karpo.Tests.Folders.ScreenTimeLinesTests.Fortnight();
        foreach (var tile in folder.Tiles)
        {
            Karpo.Tests.Folders.ScreenTimeLinesTests.Feed(tile.Island, days);
        }
    }

    /// <summary>
    /// Each height of each kind: the plan gives the line what its height says for its content, a
    /// Fixed line exactly its rows whatever it holds, and the line draws something at every height.
    /// </summary>
    [UiTheory]
    [MemberData(nameof(Kinds))]
    public void EveryKindAtEveryHeight(string name)
    {
        var kind = All.Single(each => each.Name == name);
        OnStaThread(gfx =>
        {
            var was = Clock.Now;
            var (switches, calendar, media) = (ToggleSwitches.Source, CalendarIsland.Made, MediaIsland.Stand);
            var at = kind.At ?? Today.ToDateTime(new TimeOnly(10, 30));
            Clock.Now = () => at;
            try
            {
                foreach (var dark in new[] { true, false })
                {
                    var look = LookFor(dark);
                    var panels = new List<(string, Look, FolderIsland, FolderPlan)>();
                    try
                    {
                        foreach (var (caption, height) in Heights)
                        {
                            kind.Fakes();
                            var line = Line(kind.List, island =>
                            {
                                if (height is { } set)
                                {
                                    island.Height = set;
                                }
                            });
                            var folder = ListFormPictures.Folder(gfx, made => kind.Before?.Invoke(made), line);
                            var (natural, plan) = Plan(folder, look, 1f);
                            var given = Given(plan, 0);
                            var wanted = LineHeight.For(folder.Tiles[0].Cell.Island);
                            Assert.Equal(wanted.Given(natural[0], FolderFlyout.CellPx(1f, 1f)), given, 1);
                            if (wanted.Fixed)
                            {
                                Assert.Equal(ListFormPictures.Cells(wanted.Cells, 1f, 1f), given, 1);
                            }
                            var (top, bottom, drawn) = Spill(folder, plan, look);
                            Assert.True(drawn, $"{name} at {caption}: nothing drawn");
                            Assert.True(top >= -0.5f && bottom <= given + 0.5f, $"{name} at {caption}: ink {top:0}..{bottom:0} outside a line {given:0} px tall");
                            var said = $"{caption}: {natural[0]:0} px of content, given {given:0}";
                            output.WriteLine($"{name} {(dark ? "dark" : "light")} {said} (ink {top:0}..{bottom:0})");
                            panels.Add((said, look, folder, plan));
                        }
                        Sheet(gfx, $"line-height-{name}-{(dark ? "dark" : "light")}.png", 1f, panels);
                    }
                    finally
                    {
                        foreach (var (_, _, folder, _) in panels)
                        {
                            folder.Closed();
                            folder.Dispose();
                        }
                    }
                }
            }
            finally
            {
                Clock.Now = was;
                (ToggleSwitches.Source, CalendarIsland.Made, MediaIsland.Stand) = (switches, calendar, media);
            }
        });
    }

    /// <summary>
    /// Where the first line puts its ink drawn alone, unclipped, into a box of the height it was
    /// given with room above and below it: its top and bottom against the box, and whether it drew.
    /// </summary>
    private static (float Top, float Bottom, bool Drawn) Spill(FolderIsland folder, FolderPlan plan, Look look)
    {
        const float margin = 400;
        var card = plan.Cards[0];
        var width = card.right - card.left;
        var height = card.bottom - card.top;
        var style = FolderFlyout.StyleFor(1f, look, FlyoutTheme.For(look));
        var line = (CellIsland)folder.Tiles[0].Island;
        (float Left, float Top, float Right, float Bottom) ink = default;
        Bitmap(width, height + (2 * margin),
            dc => Shifted(dc, 0, margin, () => line.PaintOn(dc, PopupPaint.Rect(0, 0, width, height), style, WidgetSurface.List)),
            bitmap => ink = InkOf(bitmap, (uint)MathF.Ceiling(width), (uint)MathF.Ceiling(height + (2 * margin))));
        return (ink.Top - margin, ink.Bottom - margin, ink.Right > ink.Left);
    }
}
