// A switch on its way (docs\widgets.md §182): from the click until the switch reads where it was sent,
// Windows refuses it or 30 s pass, its buttons show where it is going, quietened, with a ring on the
// toggles row's button and "Turning on…" on a line — after 200 ms, so a switch that answers at once
// never flickers — and a click meanwhile is ignored. The state machine is pure; the row and the line
// are checked with a switch that takes its time (Slow), on a clock of the test's own, and pictured
// dark and light (switch-pending-<dark|light>.png, KARPO_PNG_DIR). The UI parts need KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Dock.QuickSettings.Services;
using Karpo.Tests.Folders;
using Karpo.Tests.Islands;
using static Karpo.Tests.Folders.ListFormPictures;
using static Karpo.Tests.Islands.Offscreen;

namespace Karpo.Tests.QuickSettings;

public sealed class SwitchPendingTests
{
    // --- The state machine -----------------------------------------------------------------------

    [Fact]
    public void AClickPendsUntilTheSwitchReadsWhereItWasSent()
    {
        var pending = new SwitchPending();
        Assert.True(pending.Begin(on: false, refused: 0, now: 1000));
        Assert.True(pending.Busy);
        Assert.Equal(PendingStep.Waiting, pending.Settle(on: false, refused: 0, now: 1500));
        // The hotspot reads nothing while it is in transition: still waiting.
        Assert.Equal(PendingStep.Waiting, pending.Settle(on: null, refused: 0, now: 2000));
        Assert.Equal(PendingStep.Confirmed, pending.Settle(on: true, refused: 0, now: 2500));
        Assert.False(pending.Busy);
        Assert.Equal(PendingStep.None, pending.Settle(on: true, refused: 0, now: 2600));
    }

    [Fact]
    public void ItShowsOnlyAfterTheDelaySoAFastSwitchNeverFlickers()
    {
        var pending = new SwitchPending();
        pending.Begin(on: true, refused: 0, now: 0);
        Assert.False(pending.Shows(SwitchPending.ShowAfterMs - 1));
        Assert.Equal(true, pending.Shown(true, SwitchPending.ShowAfterMs - 1));
        Assert.True(pending.Shows(SwitchPending.ShowAfterMs));
        Assert.Equal(false, pending.Shown(true, SwitchPending.ShowAfterMs));
        // The clock wakes the button when it starts to show, then when it times out.
        Assert.Equal(SwitchPending.ShowAfterMs, pending.Due(0));
        Assert.Equal(SwitchPending.TimeoutMs, pending.Due(SwitchPending.ShowAfterMs));
    }

    [Fact]
    public void ARefusalEndsItAndTheRealStateShows()
    {
        var pending = new SwitchPending();
        pending.Begin(on: false, refused: 3, now: 0);
        Assert.Equal(PendingStep.Refused, pending.Settle(on: false, refused: 4, now: 900));
        Assert.Equal(false, pending.Shown(false, 900));
    }

    [Fact]
    public void ItTimesOutAndTheRealStateIsReadAgain()
    {
        var pending = new SwitchPending();
        pending.Begin(on: false, refused: 0, now: 0);
        Assert.Equal(PendingStep.Waiting, pending.Settle(on: false, refused: 0, now: SwitchPending.TimeoutMs - 1));
        Assert.Equal(PendingStep.TimedOut, pending.Settle(on: false, refused: 0, now: SwitchPending.TimeoutMs));
        Assert.Null(pending.Due(SwitchPending.TimeoutMs));
    }

    [Fact]
    public void AClickWhileOneIsOnItsWayIsIgnored()
    {
        var pending = new SwitchPending();
        Assert.True(pending.Begin(on: false, refused: 0, now: 0));
        Assert.False(pending.Begin(on: false, refused: 0, now: 50));
        Assert.Equal(true, pending.Target);
        pending.Settle(on: true, refused: 0, now: 100);
        Assert.True(pending.Begin(on: true, refused: 0, now: 200));
        Assert.Equal(false, pending.Target);
    }

    [Fact]
    public void AStateNotKnownHasNothingToPend()
    {
        var pending = new SwitchPending();
        Assert.True(pending.Begin(on: null, refused: 0, now: 0));
        Assert.False(pending.Busy);
    }

    [Fact]
    public void ASwitchThatAnswersAsItIsAskedIsNeverPending()
    {
        var quick = new SampleSwitch("Airplane mode", on: false, switches: true);
        Assert.True(Pendings.Act(quick, quick.Act));
        Assert.Equal(true, quick.On);
        Assert.False(Pendings.Of(quick).Busy);
        // And so a second click goes through at once.
        Assert.True(Pendings.Act(quick, quick.Act));
        Assert.Equal(false, quick.On);
    }

    [Fact]
    public void OneSwitchHasOnePendingHoweverManyButtons()
    {
        var slow = new Slow("Mobile hotspot", on: false);
        Assert.True(Pendings.Act(slow, slow.Act));
        Assert.False(Pendings.Act(slow, slow.Act));
        Assert.Equal(1, slow.Acts);
        Assert.Same(Pendings.Of(slow), Pendings.Of(slow));
    }

    // --- The row and the line --------------------------------------------------------------------

    /// <summary>
    /// The toggles row: a click on a slow toggle shows nothing for 200 ms, then where it is going; a
    /// second click is ignored; the switch landing ends it. A refusal shows the real state again.
    /// </summary>
    [UiFact]
    public void TheRowsToggleIsBusyUntilTheSwitchLands() =>
        OnStaThread(gfx =>
        {
            long now = 10_000;
            SwitchPending.Clock = () => now;
            var hotspot = new Slow("Mobile hotspot", on: false);
            var airplane = new Slow("Airplane mode", on: true);
            ToggleSwitches.Source = toggle => toggle == Toggle.MobileHotspot ? hotspot : airplane;
            var folder = Folder(gfx, Line(ListKind.Toggles, line => line.Toggles!.Shown = [Toggle.MobileHotspot, Toggle.AirplaneMode]));
            var row = (TogglesTile)folder.Tiles[0].Island;
            try
            {
                row.Flip(0);
                now += 100;
                Assert.False(row.PendingAt(0), "a switch on its way for 100 ms does not look busy yet");
                now += 150;
                Assert.True(row.PendingAt(0));
                row.Flip(0);
                Assert.Equal(1, hotspot.Acts);
                hotspot.Land();
                Assert.False(row.PendingAt(0));
                Assert.True(row.OnAt(0));

                row.Flip(1);
                now += 300;
                Assert.True(row.PendingAt(1));
                airplane.Refuse();
                Assert.False(row.PendingAt(1));
                Assert.True(row.OnAt(1), "refused, it stays where it was");
            }
            finally
            {
                folder.Closed();
                folder.Dispose();
            }
        });

    /// <summary>A switch's line: greyed switch where it is going, "Turning on…" beside the name, the tooltip saying it; timed out, the real state.</summary>
    [UiFact]
    public void TheLineSaysTurningOnUntilItLandsOrTimesOut() =>
        OnStaThread(gfx =>
        {
            long now = 10_000;
            SwitchPending.Clock = () => now;
            var slow = new Slow("Airplane mode", on: false);
            QuickSwitches.Source = _ => slow;
            var folder = Folder(gfx, Line(ListKind.AirplaneMode));
            var line = (SwitchIsland)folder.Tiles[0].Island;
            try
            {
                var look = LookFor(dark: true);
                var style = FolderFlyout.StyleFor(1, look, FlyoutTheme.For(look));
                var box = PopupPaint.Rect(0, 0, 360, 40);
                line.Click(0);
                now += SwitchPending.ShowAfterMs;
                Assert.True(line.Pending);
                Assert.Equal(S.WidgetSwitchTurningOn, line.LineOf(box, style).SubText);
                Assert.StartsWith(S.WidgetSwitchTurningOnTip("Airplane mode"), line.Tooltip(0));
                line.Click(0);
                Assert.Equal(1, slow.Acts);
                now += SwitchPending.TimeoutMs;
                Assert.False(line.Pending);
                Assert.Null(line.LineOf(box, style).SubText);
                Assert.Equal(false, line.On);
            }
            finally
            {
                folder.Closed();
                folder.Dispose();
            }
        });

    /// <summary>
    /// The pictures: the toggles row with the hotspot turning on and airplane mode turning off beside
    /// toggles at rest, and switch lines turning on and off, dark and light, at 100 % and 225 % text.
    /// </summary>
    [UiFact]
    public void ThePendingLookIsPictured()
    {
        foreach (var dark in new[] { true, false })
        {
            OnStaThread(gfx =>
            {
                long now = 10_000;
                SwitchPending.Clock = () => now;
                var panels = new List<(string, Look, FolderIsland, FolderPlan)>();
                try
                {
                    foreach (var textScale in TextScales)
                    {
                        var switches = new Dictionary<Toggle, IQuickSwitch>
                        {
                            [Toggle.Wifi] = new SampleSwitch("Wi-Fi", on: true, switches: true),
                            [Toggle.MobileHotspot] = new Slow("Mobile hotspot", on: false),
                            [Toggle.AirplaneMode] = new Slow("Airplane mode", on: true),
                            [Toggle.NightLight] = new SampleSwitch("Night light", on: false, switches: true),
                        };
                        ToggleSwitches.Source = toggle => switches.GetValueOrDefault(toggle) ?? new SampleSwitch(toggle.ToString(), on: false, switches: true);
                        var lines = new Dictionary<QuickSwitch, IQuickSwitch>
                        {
                            [QuickSwitch.MobileHotspot] = new Slow("Mobile hotspot", on: false),
                            [QuickSwitch.AirplaneMode] = new Slow("Airplane mode", on: true),
                            [QuickSwitch.NightLight] = new SampleSwitch("Night light", on: true, switches: true),
                        };
                        QuickSwitches.Source = which => lines.GetValueOrDefault(which);
                        var folder = Folder(gfx,
                            Line(ListKind.Toggles, line => line.Toggles!.Shown = [Toggle.Wifi, Toggle.MobileHotspot, Toggle.AirplaneMode, Toggle.NightLight]),
                            Line(ListKind.MobileHotspot),
                            Line(ListKind.AirplaneMode),
                            Line(ListKind.NightLight));
                        ((TogglesTile)folder.Tiles[0].Island).Flip(1);
                        ((TogglesTile)folder.Tiles[0].Island).Flip(2);
                        ((SwitchIsland)folder.Tiles[1].Island).Click(0);
                        ((SwitchIsland)folder.Tiles[2].Island).Click(0);
                        var look = LookFor(dark, textScale);
                        var (_, plan) = Plan(folder, look, 1);
                        panels.Add(($"On its way: {textScale * 100:0} % text", look, folder, plan));
                    }
                    // Past the delay: the busy look shows.
                    now += SwitchPending.ShowAfterMs + 50;
                    Sheet(gfx, $"switch-pending-{(dark ? "dark" : "light")}.png", 1, panels);
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

    /// <summary>A switch that takes its time, as the hotspot does: a click asks, and it lands when the test says.</summary>
    private sealed class Slow(string name, bool on) : IQuickSwitch
    {
        private Action? changed;

        public string Name { get; } = name;

        public bool Available => true;

        public bool? On { get; private set; } = on;

        public string? Note => null;

        public bool Switches => true;

        public int Refusals { get; private set; }

        /// <summary>How many clicks reached it.</summary>
        public int Acts { get; private set; }

        public void Start(Action onChanged) => changed = onChanged;

        public void Act() => Acts++;

        /// <summary>Windows has done it.</summary>
        public void Land()
        {
            On = !On;
            changed?.Invoke();
        }

        /// <summary>Windows said no.</summary>
        public void Refuse()
        {
            Refusals++;
            changed?.Invoke();
        }

        public void Dispose()
        {
        }
    }
}
