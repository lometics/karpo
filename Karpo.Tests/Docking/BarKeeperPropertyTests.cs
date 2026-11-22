// The one reconcile (Karpo.Dock\BarKeeper.cs, §272) against any sequence of what can happen: random
// runs on the made-up desktop (BarWorld), each from a seed so a failure can be run again alone. Two
// things hold for every run:
//   - on the way, no rule is ever broken (BarWorld.Broken): nothing sent to the shell for a display
//     with a full screen app, no work area set there, no bar shown over one outside edit mode, no
//     message loop that never ends, no settle timer that keeps firing;
//   - at the end, once Explorer behaves again and the person does something (a window to the front),
//     everything is as it should be (BarWorld.Wrong): every strip held exactly, every bar shown, and
//     Explorer's taskbar hidden or as the person had it, as the setting says.

using Karpo.Core;
using Karpo.Dock;
using TerraFX.Interop.Windows;
using static Karpo.Tests.Docking.BarWorld;

namespace Karpo.Tests.Docking;

public sealed class BarKeeperPropertyTests
{
    public static TheoryData<int> Seeds()
    {
        var seeds = new TheoryData<int>();
        for (var seed = 1; seed <= 1000; seed++)
        {
            seeds.Add(seed);
        }
        return seeds;
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void AnySequenceEndsAsItShouldAndBreaksNoRuleOnTheWay(int seed)
    {
        var random = new Random(seed);
        var world = new BarWorld();
        var steps = new List<string>();
        world.Start();
        var games = new List<Window>();
        var twoPlugged = true;
        for (var step = 0; step < 60; step++)
        {
            var display = random.Next(2) == 0 || !twoPlugged ? One : Two;
            var bounds = display == One ? Rect(0, 0, 3840, 2160) : Rect(-3840, 0, 0, 2160);
            switch (random.Next(19))
            {
                case 0:
                    steps.Add($"a window to the front on {display}");
                    world.Activate(world.Open("Notepad", display, Rect(bounds.left + 100, 100, bounds.left + 900, 700)));
                    break;
                case 1:
                {
                    steps.Add($"a borderless full screen game on {display}");
                    var game = world.Open("UnityWndClass", display, world.Of(display).Bounds, captioned: false);
                    games.Add(game);
                    world.Activate(game);
                    break;
                }
                case 2:
                {
                    steps.Add($"an exclusive full screen game switching {display} to 640x480");
                    var mode = Rect(world.Of(display).Bounds.left, 0, world.Of(display).Bounds.left + 640, 480);
                    var game = world.Open("Anno1800", display, Rect(mode.left, 0, mode.left + 1920, 1080), captioned: false);
                    games.Add(game);
                    world.Activate(game);
                    game.Rect = mode;
                    world.Direct3DFullScreen = random.Next(2) == 0;
                    world.Mode(display, mode);
                    break;
                }
                case 3 when games.Count > 0:
                {
                    var game = games[random.Next(games.Count)];
                    switch (random.Next(3))
                    {
                        case 0:
                            steps.Add("a game windowed");
                            game.Rect = Rect(game.Rect.left + 100, 100, game.Rect.left + 900, 700);
                            game.Captioned = true;
                            world.Changed(game);
                            break;
                        case 1:
                            steps.Add("a game minimized");
                            game.Minimized = true;
                            world.Changed(game);
                            break;
                        default:
                            steps.Add("a game closed");
                            games.Remove(game);
                            world.Close(game);
                            break;
                    }
                    break;
                }
                case 4:
                    steps.Add($"{display} back to its own mode");
                    world.Direct3DFullScreen = false;
                    world.Mode(display, bounds);
                    break;
                case 5:
                    if (twoPlugged)
                    {
                        steps.Add("the second display unplugged");
                        foreach (var game in games.Where(game => game.Display == Two).ToList())
                        {
                            games.Remove(game);
                            world.Close(game);
                        }
                        world.Unplug(Two);
                    }
                    else
                    {
                        steps.Add("the second display back");
                        world.Plug(Two, 102, Rect(-3840, 0, 0, 2160));
                    }
                    twoPlugged = !twoPlugged;
                    break;
                case 6:
                    steps.Add("Explorer restarts");
                    world.ExplorerRestarts();
                    break;
                case 7:
                {
                    var stall = random.Next(0, 300_000);
                    steps.Add($"a dark screen, Explorer stalling {stall} ms");
                    world.DarkScreen(stall);
                    break;
                }
                case 8:
                    steps.Add($"{display}'s work area put back whole without a word");
                    world.ResetsWorkArea(display, says: false);
                    break;
                case 9:
                    steps.Add($"{display}'s work area put back whole, said");
                    world.ResetsWorkArea(display, says: true);
                    break;
                case 10:
                    world.HideTaskbar = !world.HideTaskbar;
                    steps.Add(world.HideTaskbar ? "the sign-out cancelled" : "the session ending");
                    world.Stirred(Stir.Settings);
                    break;
                case 11:
                {
                    var bar = world.Bars[random.Next(world.Bars.Count)];
                    bar.Thickness = new[] { 0, Band, 72 }[random.Next(3)];
                    steps.Add($"{bar.Display}'s bar {bar.Thickness} px thick");
                    world.Stirred(Stir.Scale);
                    break;
                }
                case 12:
                {
                    var bar = world.Bars[random.Next(world.Bars.Count)];
                    bar.Edge = bar.Edge == Edge.Bottom ? Edge.Top : Edge.Bottom;
                    steps.Add($"{bar.Display}'s bar to the {bar.Edge}");
                    world.Stirred(Stir.Settings);
                    break;
                }
                case 13:
                    if (world.Editing)
                    {
                        steps.Add("edit mode off");
                        world.EndEdit();
                    }
                    else
                    {
                        steps.Add("edit mode on");
                        world.BeginEdit();
                    }
                    break;
                case 14:
                {
                    var stir = new[] { Stir.Unlocked, Stir.Resumed, Stir.DisplayLit }[random.Next(3)];
                    steps.Add(stir.Says());
                    world.Stirred(stir);
                    break;
                }
                case 15:
                {
                    var ms = random.Next(0, 5_000);
                    steps.Add($"{ms} ms pass");
                    world.Wait(ms);
                    break;
                }
                case 16:
                    world.ApplyDelay = world.ApplyDelay == 0 ? 1_500 : 0;
                    steps.Add($"Explorer applies {world.ApplyDelay} ms late");
                    break;
                case 17:
                    world.DropsEverything = !world.DropsEverything;
                    steps.Add($"Explorer drops every position: {world.DropsEverything}");
                    break;
                default:
                    world.RefuseNew = random.Next(0, 4);
                    world.FightsDirectSets = random.Next(2) == 0;
                    steps.Add($"Explorer refuses the next {world.RefuseNew} registrations, fights direct sets: {world.FightsDirectSets}");
                    break;
            }
            Assert.True(world.Broken.Count == 0, $"seed {seed}: {string.Join("; ", world.Broken)} after: {string.Join(" > ", steps)}");
        }

        // Explorer behaves again; the games end; the person goes on working.
        world.DropsEverything = false;
        world.FightsDirectSets = false;
        world.RefuseNew = 0;
        world.ApplyDelay = 0;
        world.Direct3DFullScreen = false;
        foreach (var game in games.ToList())
        {
            world.Close(game);
        }
        if (world.Editing)
        {
            world.EndEdit();
        }
        foreach (var display in world.Displays.ToList())
        {
            if (display.Bounds.right - display.Bounds.left == 640)
            {
                world.Mode(display.Device, display.Device == One ? Rect(0, 0, 3840, 2160) : Rect(-3840, 0, 0, 2160));
            }
        }
        world.Settle();
        var settles = world.Settles;
        world.Wait(AppbarGuard.RepairWindowMs + 1);
        world.Activate(world.Open("Notepad", One, Rect(100, 100, 900, 700)));
        world.Settle();
        var wrong = world.Wrong();
        Assert.True(wrong.Count == 0, $"seed {seed}: {string.Join("; ", wrong)} after: {string.Join(" > ", steps)}");
        // And quiet: nothing more runs.
        var calls = world.Calls.Count;
        settles = world.Settles;
        world.Wait(600_000);
        Assert.Equal(calls, world.Calls.Count);
        Assert.Equal(settles, world.Settles);
    }
}
