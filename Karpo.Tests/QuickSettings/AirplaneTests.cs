// Airplane mode as Windows' own tile has it (QuickSettings\Services\RadioManager.cs): the Radio
// Management service's system-wide switch, and the radios one by one only where the service's
// interface is not there. Everything here runs against made-up sources: no test switches, or even
// asks after, this PC's radios.

using Karpo.Dock.QuickSettings.Services;

namespace Karpo.Tests.QuickSettings;

public sealed class AirplaneTests
{
    /// <summary>A made-up source: where it stands, and every switch asked of it.</summary>
    private sealed class Made(bool? on, bool allow = true, bool available = true, string note = "made up") : IAirplaneSource
    {
        public List<bool> Asked { get; } = [];

        public bool Started { get; private set; }

        public bool Disposed { get; private set; }

        public Action? Changed { get; private set; }

        public bool Available { get; } = available;

        public bool? On { get; set; } = on;

        public string Note { get; } = note;

        public void Start(Action changed)
        {
            Started = true;
            Changed = changed;
        }

        public bool Set(bool value)
        {
            Asked.Add(value);
            if (allow)
            {
                On = value;
            }
            return allow;
        }

        public void Dispose() => Disposed = true;
    }

    /// <summary>
    /// What the switch said, and when: it answers from the pool, so a test waits for each answer
    /// rather than guessing how long one takes. Never disposed — a late answer must find it there.
    /// </summary>
    private sealed class Heard
    {
        private readonly SemaphoreSlim told = new(0);
        private int count;

        public int Count => Volatile.Read(ref count);

        public void Tell()
        {
            Interlocked.Increment(ref count);
            told.Release();
        }

        public void Next() => Assert.True(told.Wait(Patience.Ceiling), "the switch never answered");
    }

    /// <summary>Starts <paramref name="action"/> and waits for its first answer: what it found, and where that stands.</summary>
    private static Heard Started(AirplaneSwitch action)
    {
        var heard = new Heard();
        action.Start(heard.Tell);
        heard.Next();
        return heard;
    }

    /// <summary>
    /// The service's "enabled" is the radios', so airplane mode is its opposite; and a service that
    /// did not answer is no state at all, never "off".
    /// </summary>
    [Theory]
    [InlineData(0, 1, 0)]
    [InlineData(0, 0, 1)]
    [InlineData(0, 5, 0)]
    [InlineData(unchecked((int)0x80004005), 0, -1)]
    [InlineData(unchecked((int)0x800706BA), 1, -1)]
    public void TheServicesAnswerIsAirplaneModesOpposite(int result, int radiosEnabled, int airplane) =>
        Assert.Equal(airplane, AirplaneState.From(result, radiosEnabled));

    [Fact]
    public void TheServiceIsWhatItReadsAndSwitchesWhereItIsThere()
    {
        var service = new Made(on: false);
        var radios = new Made(on: true);
        using var action = new AirplaneSwitch(() => service, () => radios);

        // Nothing known before it has looked: no state, and still offered.
        Assert.Null(action.On);
        Assert.True(action.Available);

        var heard = Started(action);
        Assert.Same(service, action.Source);
        Assert.True(service.Started);
        Assert.False(radios.Started);
        Assert.False(action.On);
        Assert.Equal("made up", action.Note);

        action.Act();
        heard.Next();
        Assert.Equal([true], service.Asked);
        Assert.True(action.On);
        Assert.Empty(radios.Asked);

        action.Act();
        heard.Next();
        Assert.Equal([true, false], service.Asked);
        Assert.False(action.On);
    }

    /// <summary>A change made anywhere else — Windows' own panel — reaches the widget through the source.</summary>
    [Fact]
    public void AChangeMadeElsewhereIsHeard()
    {
        var service = new Made(on: false);
        using var action = new AirplaneSwitch(() => service, () => new Made(null));
        var heard = Started(action);
        var before = heard.Count;

        service.On = true;
        service.Changed!();

        Assert.True(action.On);
        Assert.Equal(before + 1, heard.Count);
    }

    /// <summary>The radios one by one only where the service is not there, and then they are what a click switches.</summary>
    [Fact]
    public void WithoutTheServiceTheRadiosStandIn()
    {
        var radios = new Made(on: false, note: "radios");
        using var action = new AirplaneSwitch(() => null, () => radios);

        var heard = Started(action);
        Assert.Same(radios, action.Source);
        Assert.True(radios.Started);
        Assert.Equal("radios", action.Note);

        action.Act();
        heard.Next();
        Assert.Equal([true], radios.Asked);
        Assert.True(action.On);
    }

    /// <summary>A service that throws while it is made is the same as one that is not there.</summary>
    [Fact]
    public void AServiceThatFailsIsNotThere()
    {
        var radios = new Made(on: true);
        using var action = new AirplaneSwitch(() => throw new InvalidCastException("A test's made-up service that throws on purpose (AirplaneTests)"), () => radios);

        Started(action);
        Assert.Same(radios, action.Source);
        Assert.True(action.On);
    }

    /// <summary>A PC with neither the service nor a radio says there is nothing to switch, once it has looked.</summary>
    [Fact]
    public void NothingToSwitchIsNotAvailable()
    {
        using var action = new AirplaneSwitch(() => null, () => new Made(null, available: false));

        Assert.True(action.Available);
        Started(action);
        Assert.False(action.Available);
        Assert.Null(action.On);
    }

    /// <summary>Windows refusing is said in the tooltip's line, and the state is not made up.</summary>
    [Fact]
    public void ARefusalIsSaid()
    {
        var service = new Made(on: false, allow: false);
        using var action = new AirplaneSwitch(() => service, () => new Made(null));
        var heard = Started(action);

        action.Act();
        heard.Next();

        Assert.Equal([true], service.Asked);
        Assert.False(action.On);
        Assert.Equal("Windows didn't let Karpo switch airplane mode.", action.Note);
    }

    /// <summary>What it found is let go with the widget.</summary>
    [Fact]
    public void TheSourceGoesWithTheSwitch()
    {
        var service = new Made(on: false);
        var action = new AirplaneSwitch(() => service, () => new Made(null));
        Started(action);

        action.Dispose();

        Assert.True(service.Disposed);
        Assert.Null(action.Source);
    }
}
