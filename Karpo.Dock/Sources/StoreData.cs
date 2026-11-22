// The data sources whose data a store already keeps once for everybody (DataSources).
//
// The weather of a place, a share's price, the notifications, the sound devices and what is
// playing are each one store's already: every
// widget that shows them reads the one entry, and a second widget of a place or a symbol costs no
// second fetch. What the stores did not do was start without a widget. Each began with the first
// widget that looked, and went on only while one was drawn — so a weather card in a folder nobody
// opened was fetched once when the dock started and never again, a list row of it not even once,
// and a widget in a layout no display shows never at all.
//
// A source here is the store's holder for the config: made at the dock's start for every place,
// symbol or store the config's widgets name, on any bar, in any folder, in any layout, and it
// keeps that store reading at its own cadence whether or not a widget of it is drawn. The widgets
// keep their own hold on the store, as before — they are what is woken when news comes — and the
// source draws nothing and wakes nobody: the next widget drawn reads what the store already has.
//
// The cadence is the store's, unchanged. A place's forecast and a symbol's price are asked for
// once a minute and fetched when the store says they are due — at the one interval every online
// widget follows (OnlineUpdates), held to each provider's floor.
//
// The accounts' calendars, mail, contacts and tasks have no source here: the dock syncs every
// account from its start whatever the config names (Pim\PimSync.cs), so a widget of them needs
// nothing to keep its store reading.

using Karpo.Core;
using Karpo.Dock.QuickSettings.Services;
using Karpo.Dock.Markets;

namespace Karpo.Dock;

/// <summary>A place's forecast, kept fetched at its interval from the dock's start (WeatherStore).</summary>
internal sealed class WeatherData : DataSource
{
    private readonly WeatherOptions options;
    private readonly WeatherStore store;

    public WeatherData(WeatherOptions options, WeatherStore? store = null)
        : base(KeyOf(options) ?? throw new ArgumentException("No place is set.", nameof(options)))
    {
        this.options = new WeatherOptions
        {
            Place = options.Place,
            Latitude = options.Latitude,
            Longitude = options.Longitude,
            UseDeviceLocation = options.UseDeviceLocation,
        };
        this.store = store ?? WeatherStore.Shared;
    }

    /// <summary>
    /// The source of a widget set to <paramref name="options"/>: the place, one source however many
    /// widgets show it. Null where no place is set, which fetches nothing.
    /// </summary>
    public static SourceKey? KeyOf(WeatherOptions? options) =>
        options is { IsSet: true } set ? new SourceKey("weather", WeatherStore.KeyOf(set)) : null;

    /// <summary>A minute: the store decides whether the forecast is due.</summary>
    public override int IntervalMs => 60_000;

    protected internal override void Start()
    {
        store.Use(this, options);
        store.Poll(this);
    }

    protected override bool Gather(long now)
    {
        store.Poll(this);
        return false;
    }

    public override void Dispose() => store.Release(this);
}

/// <summary>A markets widget's price — a share's, a rate's or a coin's — kept fetched at its interval from the dock's start (MarketStore).</summary>
internal sealed class MarketData : DataSource
{
    private readonly MarketAsk ask;
    private readonly MarketStore store;

    public MarketData(MarketOptions options, MarketStore? store = null)
        : base(KeyOf(options) ?? throw new ArgumentException("Nothing is asked.", nameof(options)))
    {
        ask = MarketAsk.Of(options)!.Value;
        this.store = store ?? MarketStore.Shared;
    }

    /// <summary>The source of a widget set to <paramref name="options"/>: its question; null where it asks nothing.</summary>
    public static SourceKey? KeyOf(MarketOptions? options) =>
        options is not null && MarketAsk.Of(options) is { } asked ? new SourceKey("market", asked.Key) : null;

    /// <summary>A minute: the store decides whether the price is due.</summary>
    public override int IntervalMs => 60_000;

    protected internal override void Start()
    {
        store.Use(this, ask);
        store.Poll(this);
    }

    protected override bool Gather(long now)
    {
        store.Poll(this);
        return false;
    }

    public override void Dispose() => store.Release(this);
}

/// <summary>
/// Windows' notifications, read and watched from the dock's start (NotificationStore). Not on an
/// isolated run — a test, <c>--smoke</c> — which reads none of the person's notifications: the
/// widgets there are handed stores of their own.
/// </summary>
internal sealed class NotificationData() : DataSource(KeyOf())
{
    public static SourceKey KeyOf() => new("notifications");

    // The store is the dock thread's own; the one this source started is the one it lets go.
    private NotificationStore? store;

    public override int IntervalMs => 0;

    protected internal override void Start()
    {
        if (Config.IsIsolated)
        {
            return;
        }
        store = NotificationStore.Shared;
        store.Use(this);
    }

    protected override bool Gather(long now) => false;

    public override void Dispose() => store?.Release(this);
}

/// <summary>
/// The sound devices, the apps' levels and the volume, followed from the dock's start (SoundStore):
/// the store starts with its first watcher and reads once, and stops with its last.
/// </summary>
internal sealed class SoundData() : DataSource(KeyOf())
{
    public static SourceKey KeyOf() => new("sound");

    // The source draws nothing: a change wakes the widgets that watch as well, and they read it.
    private readonly Action wake = () => { };

    public override int IntervalMs => 0;

    protected internal override void Start() => SoundStore.Shared.Watch(wake);

    protected override bool Gather(long now) => false;

    public override void Dispose() => SoundStore.Shared.Unwatch(wake);
}

/// <summary>
/// What is playing, followed from the dock's start: Windows' media sessions (MediaSessions), whose
/// manager is asked for once and then only listens. It follows for the life of the dock, as it did
/// once a media widget had been made, because the quick settings' media row reads it too.
/// </summary>
internal sealed class MediaData() : DataSource(KeyOf())
{
    public static SourceKey KeyOf() => new("media");

    public override int IntervalMs => 0;

    // On the dock's thread: the sessions raise their news on the thread that made them.
    protected internal override void Start() => _ = MediaSessions.Shared;

    protected override bool Gather(long now) => false;
}
