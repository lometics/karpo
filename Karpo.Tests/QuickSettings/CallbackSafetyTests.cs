// Nothing the switches do off the dock's thread may end the process. An exception thrown on a
// pool thread's wait, a file watcher's thread, a WinRT event's thread or a thread of Karpo's own is
// unhandled, and an unhandled exception takes the whole dock down — so every such callback catches
// what it calls, notes it, and carries on. These make each one throw, on purpose, and a test run
// that ended in a crashed host instead of a result would be this failing.
//
// And what the dock reads while it draws (a switch's state) answers nothing, rather than throwing,
// where what it asks throws.

using Karpo.Dock.QuickSettings.Services;
using Microsoft.Win32;

namespace Karpo.Tests.QuickSettings;

public sealed class CallbackSafetyTests
{
    /// <summary>An airplane source that throws from everything it is asked.</summary>
    private sealed class Throwing : IAirplaneSource
    {
        public bool Available => throw new InvalidOperationException("A test's source throws on purpose (CallbackSafetyTests)");

        public bool? On => throw new InvalidOperationException("A test's source throws on purpose (CallbackSafetyTests)");

        public string Note => "throws";

        public void Start(Action changed) => throw new InvalidOperationException("A test's source throws on purpose (CallbackSafetyTests)");

        public bool Set(bool on) => throw new InvalidOperationException("A test's source throws on purpose (CallbackSafetyTests)");

        public void Dispose()
        {
        }
    }

    /// <summary>
    /// Airplane mode over a source that throws from its start, its state and its switch: the widget
    /// shows no state, a click says Windows refused, and nothing escapes any thread.
    /// </summary>
    [Fact]
    public void AnAirplaneSourceThatThrowsTakesNothingDown()
    {
        using var action = new AirplaneSwitch(() => new Throwing(), () => new Throwing());
        var told = new SemaphoreSlim(0);
        action.Start(() => told.Release());
        Assert.True(told.Wait(Patience.Ceiling), "the switch never answered");

        Assert.Null(action.On);
        Assert.False(action.Available);

        action.Act();
        Assert.True(told.Wait(Patience.Ceiling), "the click never answered");
        Assert.Equal("Windows didn't let Karpo switch airplane mode.", action.Note);
    }

    /// <summary>
    /// A registry watch whose news throws is caught where the pool hands it over, and the watch is
    /// armed again. The key watched is read, never written.
    /// </summary>
    [Fact]
    public void ARegistryWatchWhoseNewsThrowsIsCaught()
    {
        var heard = 0;
        using var watch = RegistryWatch.Open(RegistryHive.CurrentUser, "Software", subtree: false, () =>
        {
            Interlocked.Increment(ref heard);
            throw new InvalidOperationException("A test's watch throws on purpose (CallbackSafetyTests)");
        });
        Assert.NotNull(watch);

        watch.Heard();
        watch.Heard();

        Assert.Equal(2, Volatile.Read(ref heard));
    }

    /// <summary>The Settings handlers' thread goes on after a job that throws.</summary>
    [Fact]
    public void TheHandlersThreadOutlivesAJobThatThrows()
    {
        HandlerThread.Post(static () => throw new InvalidOperationException("A test's job throws on purpose (CallbackSafetyTests)"));

        Assert.Equal(42, HandlerThread.Run(static () => 42, 0, (int)Patience.Ceiling.TotalMilliseconds));
    }

    /// <summary>A VPN source that throws from its start.</summary>
    private sealed class ThrowingVpn : IVpnSource
    {
        public bool Looked => true;

        public IReadOnlyList<VpnConnection> Connections => [];

        public void Start(Action changed) => throw new InvalidOperationException("A test's source throws on purpose (CallbackSafetyTests)");

        public VpnOutcome Connect(VpnConnection connection) => throw new InvalidOperationException("A test's source throws on purpose (CallbackSafetyTests)");

        public bool Disconnect(VpnConnection connection) => throw new InvalidOperationException("A test's source throws on purpose (CallbackSafetyTests)");

        public void Dispose()
        {
        }
    }

    /// <summary>A VPN source whose start throws leaves the widget with no connections, not the dock gone.</summary>
    [Fact]
    public void AVpnSourceThatThrowsTakesNothingDown()
    {
        using var action = new VpnSwitch(() => new ThrowingVpn());
        var told = new SemaphoreSlim(0);
        action.Start(() => told.Release());
        Assert.True(told.Wait(Patience.Ceiling), "the switch never answered");

        Assert.False(action.On);
        Assert.Equal(0, action.Count);
    }
}
