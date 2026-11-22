// keys: every kind a list holds walked by the keyboard in the real folder (docs\widgets.md, "One keyboard";
// the person, 2026-11-01: "keyboard navigation should be implemented for all the widgets"). Held on a
// hidden desktop, every kind set up on the stand-ins the gutter sheets draw on (ListGutterTests.StoodIn), in a
// folder of its own, at two scales:
//
//  - **Every target is a stop.** What a click on the line reaches — every target the folder's own hit test
//    answers on it, asked every few pixels (FolderFlyout.PartAt) — is one of the line's stops (LineFocus).
//  - **Every stop is reached by the arrows** from the first: the stops' graph under FocusNav.Toward, the walk
//    the folder makes, reaches them all.
//  - **The keys walk them for real**: the folder opened by the keys (TakeKeys) is on the line's first stop, the
//    ring round it; Down and Right go on; where the ring stands, a click is the same target the keys are on —
//    Enter there is what a click there does (both are Activate, Click(target)).
//  - **Escape closes the folder.**
//
// What a kind still breaks is Allowed (KeyboardSweepAllowed.cs), one line each; a line that passes now fails the
// test, so the list only shrinks. No line's click is run here: a graph's opens Task Manager, a switch flips this PC's.
// Needs Direct2D and a desktop: KARPO_UI_TESTS=1.

using System.Numerics;
using Karpo.Core;
using Karpo.Dock;
using Karpo.Dock.QuickSettings.Services;
using Karpo.Tests.Edit;
using Karpo.Tests.Forecasts;
using Karpo.Tests.Islands;
using Karpo.Tests.Perf;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using Xunit.Abstractions;

namespace Karpo.Tests.Folders;

[Collection(SamplerState.Collection)]
public sealed partial class KeyboardSweepTests(ITestOutputHelper output)
{
    /// <summary>How far apart the points asked of a line are, in px: finer than the walk's own (LineFocus).</summary>
    private const int Step = 3;

    /// <summary>KARPO_KEYS_KINDS: only these kinds, comma-separated, while one is being mended; all where unset.</summary>
    private static readonly HashSet<string>? Only = Environment.GetEnvironmentVariable("KARPO_KEYS_KINDS") is { Length: > 0 } kinds
        ? [.. kinds.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)]
        : null;

    /// <summary>The scales swept.</summary>
    private static readonly float[] Scales = [1f, 1.5f];

    [UiFact]
    public void EveryTargetOfEveryLineIsReachedByTheKeys()
    {
        var offences = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var seen = new List<string>();
        ListGutterTests.StoodIn<(Gfx Gfx, HINSTANCE Instance)>(
            SavedWeather.Fixed,
            body => WindowShots.OnHiddenDesktop("keyboard-sweep", (gfx, instance) => body((gfx, instance))),
            context =>
            {
                var (gfx, instance) = context;
                var look = ListFormPictures.LookFor(true);
                // The brightness a slider stepped by the keys sets is a stand-in's, never this PC's display's.
                Karpo.Dock.QuickSettings.Services.Brightness.Stand = new SampleScreens().Add(Panel(), @"\\.\DISPLAY1", "Sample", 50);
                using var flyout = new FolderFlyout(gfx, instance, 0);
                foreach (var widget in ListWidgets.All.Where(widget => Only is null || Only.Contains(widget.Kind.ToString())))
                {
                    foreach (var scale in Scales)
                    {
                        var folder = LineClickTests.Folder(gfx, widget.Kind);
                        var clock = System.Diagnostics.Stopwatch.StartNew();
                        try
                        {
                            Sweep(gfx, flyout, folder, widget.Kind, scale, look, offences, seen);
                            seen.Add($"  {clock.ElapsedMilliseconds} ms");
                        }
                        catch (Exception error) when (error is not OutOfMemoryException)
                        {
                            offences.TryAdd(widget.Kind.ToString(), $"threw {error.GetType().Name}: {error.Message}");
                        }
                        finally
                        {
                            flyout.Hide();
                            folder.Dispose();
                        }
                    }
                }
                Karpo.Dock.QuickSettings.Services.Brightness.Stand = null;
            });
        output.WriteLine(string.Join(Environment.NewLine, seen));
        var broken = offences.Where(offence => !Allowed.ContainsKey(offence.Key)).Select(offence => $"{offence.Key}: {offence.Value}").ToList();
        var mended = Allowed.Keys.Where(kind => (Only is null || Only.Contains(kind)) && !offences.ContainsKey(kind)).ToList();
        Assert.True(broken.Count == 0, "not walked by the keys:\n" + string.Join("\n", broken));
        Assert.True(mended.Count == 0, "walked now; take them out of KeyboardSweepAllowed.cs:\n" + string.Join("\n", mended));
    }

    /// <summary>The display the folders open on (by their anchor): what the brightness stand-in stands for.</summary>
    private static nint Panel() =>
        (nint)TerraFX.Interop.Windows.Windows.MonitorFromPoint(new POINT { x = 1700, y = 1400 }, MONITOR.MONITOR_DEFAULTTONEAREST);

    /// <summary>
    /// A slider the keys are on steps (§470): Right a level up, Left down, Page Up ten up, End and Home to its ends —
    /// the brightness line's, on a stand-in display; Up and Down still walk.
    /// </summary>
    [UiFact]
    public void ASliderStepsByTheKeys()
    {
        var levels = new List<int>();
        WindowShots.OnHiddenDesktop("keyboard-slider", (gfx, instance) =>
        {
            var screen = Panel();
            Karpo.Dock.QuickSettings.Services.Brightness.Stand = new SampleScreens().Add(screen, @"\\.\DISPLAY1", "Sample", 50);
            try
            {
                using var flyout = new FolderFlyout(gfx, instance, 0);
                var folder = ListFormPictures.Folder(gfx, ListFormPictures.Line(ListKind.Brightness));
                try
                {
                    flyout.Show(gfx, ListFormPictures.LookFor(true), 1f, new POINT { x = 1700, y = 1500 }, new RECT { right = 2400, bottom = 1500 }, Edge.Bottom, folder, FlyoutAlign.End);
                    var line = (BrightnessIsland)folder.Tiles[0].Island;
                    flyout.TakeKeys();
                    // The mode's button first (§605), the slider beside it.
                    Assert.Equal((0, BrightnessIsland.ModeTarget), flyout.KeyedOn);
                    _ = flyout.Message(WM.WM_KEYDOWN, (WPARAM)(nuint)VK.VK_RIGHT, 0);
                    Assert.Equal((0, BrightnessIsland.SetLevelTarget), flyout.KeyedOn);
                    foreach (var key in new[] { VK.VK_RIGHT, VK.VK_LEFT, VK.VK_LEFT, VK.VK_PRIOR, VK.VK_END, VK.VK_HOME })
                    {
                        _ = flyout.Message(WM.WM_KEYDOWN, (WPARAM)(nuint)key, 0);
                        levels.Add(line.BarLevel(screen));
                    }
                    Assert.Equal((0, BrightnessIsland.SetLevelTarget), flyout.KeyedOn);
                }
                finally
                {
                    flyout.Hide();
                    folder.Dispose();
                }
            }
            finally
            {
                Karpo.Dock.QuickSettings.Services.Brightness.Stand = null;
            }
        });
        Assert.Equal([51, 50, 49, 59, 100, 0], levels);
    }

    /// <summary>
    /// One ring (§471): a line that rings what its own keys are on (the month's day) shows its ring alone, the folder's
    /// none round it; its arrows are stops of their own, ringed by the folder, Up from its top week goes onto them and
    /// Down from them back into its days; the keys gone to another line, its ring goes out.
    /// </summary>
    [UiFact]
    public void OneRingShowsAtATime()
    {
        WindowShots.OnHiddenDesktop("keyboard-one-ring", (gfx, instance) =>
        {
            using var flyout = new FolderFlyout(gfx, instance, 0);
            var folder = ListFormPictures.Folder(gfx, ListFormPictures.Line(ListKind.Month), ListFormPictures.Line(ListKind.Clock));
            try
            {
                flyout.Show(gfx, ListFormPictures.LookFor(true), 1f, new POINT { x = 1700, y = 1500 }, new RECT { right = 2400, bottom = 1500 }, Edge.Bottom, folder, FlyoutAlign.End);
                var month = (MonthTile)folder.Tiles[0].Island;
                var stops = flyout.StopsOfLine(0).Select(stop => stop.Target).ToList();
                Assert.Equal([MonthTile.PreviousTarget, MonthTile.NextTarget, 0], stops);

                // In on the arrow before: the folder's ring round it, the month's none.
                flyout.TakeKeys();
                Assert.Equal((0, MonthTile.PreviousTarget), flyout.KeyedOn);
                Assert.True(flyout.FocusRingShows);
                Assert.False(month.RingsOwnKeys);

                // Down: into the days, the picked day ringed by the month alone.
                Key(flyout, VK.VK_DOWN);
                Assert.Equal((0, 0), flyout.KeyedOn);
                Assert.True(month.RingsOwnKeys);
                Assert.False(flyout.FocusRingShows);
                var picked = month.Picked;
                Key(flyout, VK.VK_RIGHT);
                Assert.Equal(picked.AddDays(1), month.Picked);
                Assert.False(flyout.FocusRingShows);

                // Up week after week: at the top week shown, onto the arrows, the month's ring out.
                for (var press = 0; press < 8 && flyout.KeyedOn.Target == 0; press++)
                {
                    Key(flyout, VK.VK_UP);
                }
                Assert.Contains(flyout.KeyedOn.Target, new[] { MonthTile.PreviousTarget, MonthTile.NextTarget });
                Assert.False(month.RingsOwnKeys);
                Assert.True(flyout.FocusRingShows);

                // Back into the days, and Tab to the clock: the month's ring out, the folder's round the clock.
                Key(flyout, VK.VK_DOWN);
                Assert.True(month.RingsOwnKeys);
                Key(flyout, VK.VK_TAB);
                Assert.Equal(1, flyout.KeyedOn.Line);
                Assert.False(month.RingsOwnKeys);
                Assert.True(flyout.FocusRingShows);
            }
            finally
            {
                flyout.Hide();
                folder.Dispose();
            }
        });
    }

    private static void Key(FolderFlyout flyout, int key) => _ = flyout.Message(WM.WM_KEYDOWN, (WPARAM)(nuint)key, 0);

    /// <summary>
    /// Narrator (§471): the folder is a pane of its lines, a line with stops a group of buttons named by their tooltips
    /// (the month's arrows) or by its widget (its own keys' stop), a line stopped at whole a button; the focused element
    /// is the stop the keys are on, and moves with them.
    /// </summary>
    [UiFact]
    public void NarratorFollowsTheFolderKeys()
    {
        WindowShots.OnHiddenDesktop("keyboard-narrator", (gfx, instance) =>
        {
            using var flyout = new FolderFlyout(gfx, instance, 0);
            var folder = ListFormPictures.Folder(gfx, ListFormPictures.Line(ListKind.Month), ListFormPictures.Line(ListKind.Clock));
            try
            {
                flyout.Show(gfx, ListFormPictures.LookFor(true), 1f, new POINT { x = 1700, y = 1500 }, new RECT { right = 2400, bottom = 1500 }, Edge.Bottom, folder, FlyoutAlign.End);
                flyout.TakeKeys();
                var tree = flyout.UiaTreeNow();
                Assert.Equal(UiaRole.Pane, tree.Root.Role);
                var month = tree.Root.Children[0];
                Assert.Equal(UiaRole.Group, month.Role);
                Assert.Equal(
                    [S.WidgetMonthPrevious, S.WidgetMonthNext, folder.Tiles[0].Island.Info.Title],
                    month.Children.Select(stop => stop.Name).ToList());
                Assert.All(month.Children, stop => Assert.Equal(UiaRole.Button, stop.Role));
                Assert.Equal(UiaRole.Button, tree.Root.Children[1].Role);
                Assert.Equal(month.Children[0].Id, tree.Focused);

                Key(flyout, VK.VK_RIGHT);
                Assert.Equal(month.Children[1].Id, flyout.UiaTreeNow().Focused);
                Key(flyout, VK.VK_TAB);
                Assert.Equal(tree.Root.Children[1].Id, flyout.UiaTreeNow().Focused);
            }
            finally
            {
                flyout.Hide();
                folder.Dispose();
            }
        });
    }

    /// <summary>One kind at one scale (see the top of the file); what it breaks, the first thing, under its kind.</summary>
    private static void Sweep(Gfx gfx, FolderFlyout flyout, FolderIsland folder, ListKind kind, float scale, Look look, IDictionary<string, string> offences, List<string> seen)
    {
        var name = kind.ToString();
        void Offence(string what) => offences.TryAdd(name, $"{what} (at {scale * 100:0} %)");
        void Open() => flyout.Show(gfx, look, scale, new POINT { x = 1700, y = 1500 }, new RECT { right = 2400, bottom = 1500 }, Edge.Bottom, folder, FlyoutAlign.End);
        Open();
        if (flyout.Plan is not { Cards.Count: > 0 } plan)
        {
            Offence("no line shown");
            return;
        }
        var card = plan.Cards[0];
        var asked = System.Diagnostics.Stopwatch.StartNew();
        var stops = flyout.StopsOfLine(0);
        var took = asked.ElapsedMilliseconds;
        var targets = stops.Select(stop => stop.Target).ToHashSet();

        // Every target a click reaches where the line shows is a stop.
        var clicked = new SortedSet<int>();
        for (var y = card.top + 0.5f; y < card.bottom; y += Step)
        {
            for (var x = card.left + 0.5f; x < card.right; x += Step)
            {
                var (line, part) = flyout.PartAt(LineClickTests.Client(flyout, new Vector2(x, y)));
                if (line == 0 && part != 0)
                {
                    clicked.Add(part);
                }
            }
        }
        // A line that walks its own keys: its targets are the ones its own keys reach from its own stop
        // (CellIsland.OwnKeyedTarget), each way in the folder opened afresh (a row of hours walks by Right, the days
        // before today by Left); and as they go one ring shows, its own or the folder's (§471).
        if (folder.Tiles[0].Island is CellIsland { WalksItsOwnKeys: true } own && clicked.Count > 0)
        {
            if (own.OwnKeyedTarget is null)
            {
                Offence("walks its own keys and does not say where they are (OwnKeyedTarget)");
            }
            foreach (var walk in new[] { VK.VK_DOWN, VK.VK_RIGHT, VK.VK_UP, VK.VK_LEFT })
            {
                flyout.Hide();
                Open();
                flyout.TakeKeys();
                flyout.KeysOnStop(0, 0);
                for (var press = 0; press <= (2 * clicked.Count) + 6; press++)
                {
                    // Where they came onto first, then where each press took them.
                    if (press > 0)
                    {
                        _ = flyout.Message(WM.WM_KEYDOWN, (WPARAM)(nuint)walk, 0);
                    }
                    if (own.OwnKeyedTarget is > 0 and var on)
                    {
                        targets.Add(on);
                    }
                    if (own.RingsOwnKeys && flyout.FocusRingShows && flyout.KeyedOn.Target == 0)
                    {
                        Offence("two rings: its own round what its keys are on, and the folder's");
                    }
                    else if (!own.RingsOwnKeys && !flyout.FocusRingShows)
                    {
                        Offence($"no ring as its keys walk, on {flyout.KeyedOn}");
                    }
                }
            }
            flyout.Hide();
            Open();
        }
        var missed = clicked.Where(target => !targets.Contains(target)).ToList();
        seen.Add($"{name} {scale * 100:0} %: {stops.Count} stops in {took} ms, {clicked.Count} targets clicked{(missed.Count > 0 ? $", missed {string.Join(" ", missed)}" : "")}");
        if (missed.Count > 0)
        {
            Offence($"{missed.Count} of its {clicked.Count} targets are no stop: {string.Join(", ", missed.Take(6))}");
        }

        // Every stop reached by the arrows from the first.
        if (stops.Count > 1)
        {
            var rects = stops.Select(stop => LineFocus.Rect(stop.Box)).ToList();
            var reached = new HashSet<int> { 0 };
            var queue = new Queue<int>([0]);
            while (queue.Count > 0)
            {
                var at = queue.Dequeue();
                foreach (var way in Enum.GetValues<FocusWay>())
                {
                    if (FocusNav.Toward(rects, at, way) is var next and >= 0 && reached.Add(next))
                    {
                        queue.Enqueue(next);
                    }
                }
            }
            if (reached.Count < stops.Count)
            {
                var lost = Enumerable.Range(0, stops.Count).Where(index => !reached.Contains(index)).Select(index => stops[index].Target);
                Offence($"{stops.Count - reached.Count} of its {stops.Count} stops are reached by no arrow: {string.Join(", ", lost.Take(6))}");
            }
        }

        // The keys for real: in on the first stop, the ring round it, and where the ring stands a click is that stop.
        flyout.TakeKeys();
        if (flyout.KeyedOn != (0, stops.Count > 0 ? stops[0].Target : 0))
        {
            Offence($"the keys came in on {flyout.KeyedOn}, not its first stop");
        }
        if (!flyout.Lit.Keyboard)
        {
            Offence("no ring as the keys came in");
        }
        var walked = new HashSet<int>();
        for (var press = 0; press < (2 * stops.Count) + 2; press++)
        {
            var (line, target) = flyout.KeyedOn;
            if (line != 0)
            {
                break;
            }
            walked.Add(target);
            if (target != 0)
            {
                if (flyout.KeyRingBox is not { } ring)
                {
                    Offence($"no ring round stop {target}");
                }
                else if (flyout.PartAt(LineClickTests.Client(flyout, new Vector2((ring.left + ring.right) / 2, (ring.top + ring.bottom) / 2))) is var under
                    && under != (0, target))
                {
                    Offence($"the ring round stop {target} stands over {under.Part}: Enter and a click there would differ");
                }
            }
            else if (stops.Count > 0 && folder.Tiles[0].Island is CellIsland walker
                && flyout.KeyRingBox is { } around
                && flyout.PartAt(LineClickTests.Client(flyout, new Vector2((around.left + around.right) / 2, (around.top + around.bottom) / 2))) is var inside
                && (inside.Line != 0 || (inside.Part != 0 && !walker.OwnKeysReach(inside.Part))))
            {
                // Its own keys' stop (§471): where it stands is what they reach, nothing beside them.
                Offence($"the ring round its own keys stands over {inside.Part}, which they do not reach");
            }
            _ = flyout.Message(WM.WM_KEYDOWN, (WPARAM)(nuint)(press % 2 == 0 ? VK.VK_DOWN : VK.VK_RIGHT), 0);
        }
        if (stops.Count > 0 && walked.Count < 2 && stops.Count > 1)
        {
            Offence($"Down and Right moved the keys off nothing of its {stops.Count} stops");
        }

        // Escape: the folder closes (a line's own field lets go first).
        for (var press = 0; press < 3 && flyout.IsOpen; press++)
        {
            _ = flyout.Message(WM.WM_KEYDOWN, (WPARAM)(nuint)VK.VK_ESCAPE, 0);
        }
        if (flyout.IsOpen)
        {
            Offence("Escape did not close the folder");
        }
    }
}
