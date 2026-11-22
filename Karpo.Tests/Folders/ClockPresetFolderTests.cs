// The clock preset opened (Karpo.Core\Presets.cs, docs\widgets.md §73): the person's arrangement
// (2026-10-11) — the time over its date, the timer, the stopwatch, the focus session and the footer,
// one card — each line its own tile. Pictured as it opens, dark and light, at 100 % and 225 % text:
// clock-preset-<dark|light>.png in KARPO_PNG_DIR. Needs Direct2D: KARPO_UI_TESTS=1. The month
// picking the agenda's day is the calendar preset's now (CalendarWeatherFolderTests).

using Karpo.Dock;
using Karpo.Dock.Panels;
using Karpo.Tests.Islands;
using static Karpo.Tests.Folders.ListFormPictures;
using static Karpo.Tests.Islands.Offscreen;

namespace Karpo.Tests.Folders;

public sealed class ClockPresetFolderTests
{
    /// <summary>The clock preset's folder, as the library adds it, opened.</summary>
    private static FolderIsland Opened(Gfx gfx)
    {
        var holder = Presets.Folder(Presets.Find(Presets.ClockId)!, new PresetMachine(HasBattery: false, FixedDrives: []));
        var folder = new FolderIsland($"{Owner}/{holder.Id}", holder, gfx, new Config());
        folder.Opened(gfx);
        return folder;
    }

    /// <summary>
    /// Opened, the clock is its five lines as their own tiles, in order: the time, two countdowns
    /// (the timer and the focus session) around the stopwatch, and the footer — and one card, no
    /// divider between them.
    /// </summary>
    [UiFact]
    public void TheClockOpensAsItsFiveLines() =>
        OnStaThread(gfx =>
        {
            Clock.Now = () => ListFormCases.Frozen;
            var folder = Opened(gfx);
            try
            {
                Assert.Collection(
                    folder.Tiles.Select(tile => tile.Island),
                    island => Assert.IsType<TimeHeaderTile>(island),
                    island => Assert.IsType<CountdownTile>(island),
                    island => Assert.IsType<StopwatchTile>(island),
                    island => Assert.IsType<CountdownTile>(island),
                    island => Assert.IsType<ActionsTile>(island));
                var (_, plan) = Plan(folder, LookFor(dark: true, 1f), 1);
                Assert.Equal(folder.Tiles.Count, plan.Cards.Count);
            }
            finally
            {
                folder.Closed();
                folder.Dispose();
            }
        });

    /// <summary>The clock preset as it opens, every line whole, at 100 % and 225 % text.</summary>
    [UiFact]
    public void ClockPresetPictures()
    {
        foreach (var dark in new[] { true, false })
        {
            OnStaThread(gfx =>
            {
                Clock.Now = () => ListFormCases.Frozen;
                var panels = new List<(string, Look, FolderIsland, FolderPlan)>();
                try
                {
                    foreach (var text in new[] { 1f, 2.25f })
                    {
                        var look = LookFor(dark, text);
                        var folder = Opened(gfx);
                        var (_, plan) = Plan(folder, look, 1);
                        // Every line stands in the folder: none is left out.
                        Assert.Equal(folder.Tiles.Count, plan.Cards.Count);
                        panels.Add(($"As it opens: {text * 100:0} % text", look, folder, plan));
                    }
                    Sheet(gfx, $"clock-preset-{(dark ? "dark" : "light")}.png", 1, panels);
                }
                finally
                {
                    foreach (var (_, _, folder, _) in panels)
                    {
                        folder.Closed();
                        folder.Dispose();
                    }
                }
            });
        }
    }
}
