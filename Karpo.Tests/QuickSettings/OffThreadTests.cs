// The network page's calls into Windows run on a thread of their own (OffThread.cs): the person,
// 2026-10-10, "when i click connect on a network the mouse freezes for a second or so". Held with a
// stand-in Wi-Fi whose connect blocks until the test lets it go, so nothing here touches this PC's
// Wi-Fi: the dock's thread is back at once, the page shows the attempt at once, and the outcome
// comes back through the page's Changes once the stand-in has answered.

using System.Diagnostics;
using System.Text;
using Karpo.Dock.QuickSettings.Services;

namespace Karpo.Tests.QuickSettings;

public sealed class OffThreadTests
{
    private static readonly TimeSpan Long = TimeSpan.FromSeconds(10);

    private static WifiNetwork Network(string ssid, WifiSecurity security, string? profile) =>
        new(ssid, Encoding.UTF8.GetBytes(ssid), 3, security, 4, false, profile);

    /// <summary>A Wi-Fi that answers a connect only once the test lets it, as the WLAN service does mid-join.</summary>
    private sealed class SlowWifi : IWifiSource
    {
        private Changes? changes;
        private volatile WifiAttempt? attempt;

        public ManualResetEventSlim Answer { get; } = new(false);

        public ManualResetEventSlim Asked { get; } = new(false);

        public int ConnectThread { get; private set; }

        public int StartThread { get; private set; }

        public bool Disposed { get; private set; }

        public void Start(Changes news)
        {
            StartThread = Environment.CurrentManagedThreadId;
            changes = news;
        }

        public WifiState Read() => new(true, true, WifiAccess.Allowed, [Network("Rabbit Hole", WifiSecurity.Wpa2Personal, "Rabbit Hole")], false, attempt, null);

        public void Scan()
        {
        }

        public void SetRadio(bool on)
        {
        }

        public void Connect(WifiNetwork network, string? key, bool automatic, WifiSignIn? signIn = null)
        {
            ConnectThread = Environment.CurrentManagedThreadId;
            Asked.Set();
            Assert.True(Answer.Wait(Long));
            attempt = new WifiAttempt(network.Ssid, WifiOutcome.Connected);
            changes?.Add(WifiNews.Connection);
        }

        public bool ShowProperties(WifiNetwork network) => false;

        public void Disconnect()
        {
        }

        public void Forget() => attempt = null;

        public void Dispose() => Disposed = true;
    }

    private static (OffThreadWifi Wifi, SlowWifi Slow, Func<int> Heard) Started()
    {
        var slow = new SlowWifi();
        var wifi = new OffThreadWifi(slow);
        var changes = new Changes(_ => { });
        wifi.Start(changes);
        Assert.True(wifi.Work.Idle(Long));
        _ = changes.Take();
        return (wifi, slow, changes.Take);
    }

    /// <summary>
    /// Connect is back at once while Windows takes its time, the page shows "Connecting…" at once,
    /// and the outcome follows through the page's Changes when Windows has answered.
    /// </summary>
    [Fact]
    public void ConnectIsMadeOffTheDocksThreadAndItsOutcomeComesBack()
    {
        var (wifi, slow, heard) = Started();
        try
        {
            Assert.NotEqual(Environment.CurrentManagedThreadId, slow.StartThread);
            var network = wifi.Read().Networks[0] with { Connected = false };
            var clock = Stopwatch.StartNew();
            wifi.Connect(network, null, automatic: true);
            var shown = wifi.Read();
            clock.Stop();
            Assert.True(clock.ElapsedMilliseconds < 500, $"Connect held the dock's thread for {clock.ElapsedMilliseconds} ms");
            Assert.Equal(WifiOutcome.Connecting, shown.Attempt?.Outcome);

            // Windows is still at it: the page keeps showing the attempt, and hears nothing yet.
            Assert.True(slow.Asked.Wait(Long));
            Assert.Equal(WifiOutcome.Connecting, wifi.Read().Attempt?.Outcome);

            slow.Answer.Set();
            Assert.True(wifi.Work.Idle(Long));
            Assert.Equal(WifiOutcome.Connected, wifi.Read().Attempt?.Outcome);
            Assert.True((heard() & WifiNews.Connection) != 0, "the page was not told the attempt ended");
            Assert.NotEqual(Environment.CurrentManagedThreadId, slow.ConnectThread);
            Assert.Equal(wifi.Work.WorkerThread, slow.ConnectThread);
        }
        finally
        {
            slow.Answer.Set();
            wifi.Dispose();
        }
    }

    /// <summary>A network that asks for its key asks at once: no "Connecting…" flashes up before it.</summary>
    [Fact]
    public void AKeyIsAskedForAtOnce()
    {
        var (wifi, slow, _) = Started();
        try
        {
            wifi.Connect(Network("FRITZ!Box 7590 KL", WifiSecurity.Wpa3Personal, profile: null), null, automatic: true);
            Assert.Equal(WifiOutcome.NeedsKey, wifi.Read().Attempt?.Outcome);
            wifi.Connect(Network("FRITZ!Box 7590 KL", WifiSecurity.Wpa3Personal, profile: null), "short", automatic: true);
            Assert.Equal(WifiOutcome.WrongKey, wifi.Read().Attempt?.Outcome);
        }
        finally
        {
            slow.Answer.Set();
            wifi.Dispose();
        }
    }

    /// <summary>Forgetting the attempt takes it off the page at once, and the source ends on its own thread.</summary>
    [Fact]
    public void ForgetAndDisposeAreTheWorkersToo()
    {
        var (wifi, slow, _) = Started();
        slow.Answer.Set();
        wifi.Connect(wifi.Read().Networks[0], null, automatic: true);
        Assert.True(wifi.Work.Idle(Long));
        Assert.NotNull(wifi.Read().Attempt);
        wifi.Forget();
        Assert.Null(wifi.Read().Attempt);
        wifi.Dispose();
        var deadline = Stopwatch.StartNew();
        while (!slow.Disposed && deadline.Elapsed < Long)
        {
            Thread.Sleep(10);
        }
        Assert.True(slow.Disposed);
        // Nothing asked after the end is done, and asking throws nothing.
        wifi.Scan();
        Assert.True(wifi.Work.Idle(Long));
    }

    /// <summary>What decides the attempt before Windows is asked, the same for the worker and for WindowsWifi.Connect.</summary>
    [Fact]
    public void WhatIsPlainBeforeWindowsIsAsked()
    {
        var secured = Network("Rabbit Hole", WifiSecurity.Wpa2Personal, null);
        Assert.Equal(WifiOutcome.NeedsKey, WifiProfiles.Foreseen(secured, null, null, hasProfile: false)?.Outcome);
        Assert.Null(WifiProfiles.Foreseen(secured, null, null, hasProfile: true));
        // A hidden network's profile is not known until Windows is asked by the name typed.
        Assert.Null(WifiProfiles.Foreseen(secured, null, null, hasProfile: null));
        Assert.Equal(WifiOutcome.WrongKey, WifiProfiles.Foreseen(secured, "short", null, hasProfile: false)?.Outcome);
        Assert.Null(WifiProfiles.Foreseen(secured, "carrots!", null, hasProfile: false));
        Assert.Null(WifiProfiles.Foreseen(Network("Café Central Guest", WifiSecurity.Open, null), null, null, hasProfile: false));

        var enterprise = Network("eduroam", WifiSecurity.Enterprise, null) with { Auth = 6 };
        Assert.Equal(WifiOutcome.NeedsSignIn, WifiProfiles.Foreseen(enterprise, null, null, hasProfile: false)?.Outcome);
        Assert.Null(WifiProfiles.Foreseen(enterprise, null, null, hasProfile: true));
        Assert.Equal(WifiOutcome.WrongSignIn, WifiProfiles.Foreseen(enterprise, null, new WifiSignIn("alice", ""), hasProfile: false)?.Outcome);
        Assert.Null(WifiProfiles.Foreseen(enterprise, null, new WifiSignIn("alice", "wonderland"), hasProfile: false));
        Assert.Equal(WifiOutcome.Failed, WifiProfiles.Foreseen(enterprise with { Auth = 99 }, null, null, hasProfile: true)?.Outcome);
    }

    /// <summary>Two reads of the same networks are the same, though each has its own bytes for the names; a bar more is not.</summary>
    [Fact]
    public void ReadsAreComparedByWhatTheyShow()
    {
        WifiState Read(int bars) => new(true, true, WifiAccess.Allowed, [Network("Rabbit Hole", WifiSecurity.Wpa2Personal, null) with { Bars = bars }], false, null, null);
        Assert.True(OffThreadWifi.Same(Read(3), Read(3)));
        Assert.False(OffThreadWifi.Same(Read(3), Read(4)));
        Assert.False(OffThreadWifi.Same(Read(3), Read(3) with { Scanning = true }));
    }
}
