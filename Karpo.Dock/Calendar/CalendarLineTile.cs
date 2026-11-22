// What the calendar flyout's pieces share as lines of a list (docs\widgets.md §80): the day's events
// (EventsTile) and the day, week and year views (CalendarViewTile). Each reads the events through one
// narrow seam (CalendarLines.Store) — the shared CalendarStore, or a test's own — asking only what the
// flyout asked: whether there are calendars, whether they are being read, why not, and the events in
// a range. Whatever comes to stand behind the store (a cache that keeps the events for when the
// network is not there) these lines read through it unchanged.
//
// As the calendar's own line (CalendarIsland): the store is open from the dock's start
// (Pim\PimSync.cs); the line asks it as well the first time it is looked at (a
// test's own store opens then), is woken when the events change, and never ticks: the minute the
// folder ticks moves the now-line and turns today over.

using Karpo.Core;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock;

/// <summary>Where every calendar line reads its events.</summary>
internal static class CalendarLines
{
    /// <summary>A test's own store (<see cref="CalendarIsland.Made"/>), §635 the samples' while they are shown, else the shared one.</summary>
    internal static CalendarStore Store => CalendarIsland.Made ?? (Samples.On ? Samples.Calendar() : CalendarStore.Shared);

    /// <summary>
    /// The events overlapping a range of wall-clock times of the calendars <paramref name="item"/> shows from (§455: every
    /// one where it picked none), or none where no calendar is there.
    /// </summary>
    internal static Func<DateTime, DateTime, IReadOnlyList<CalendarEvent>> EventsIn(CalendarStore store, WidgetItem? item) =>
        store.HasCalendars ? (from, to) => store.EventsIn(from, to, shown: SourceFilter.Of(item)) : static (_, _) => [];

    /// <summary>
    /// holidays: what <paramref name="day"/> is a public holiday as, a line each — a day's tooltip in a
    /// month, a year or a week's heading, whose dot or cell says no name (§240); null for none, or while off.
    /// </summary>
    internal static string? HolidayTip(CalendarStore store, DateOnly day) =>
        store.Holidays?.NamesOn(day) is { Count: > 0 } names ? string.Join('\n', names) : null;
}

/// <summary>A calendar line of a list: its store, looked at once, and woken when the events change.</summary>
internal abstract unsafe class CalendarLineTile : CellIsland, IRefreshable
{
    // Whether anybody has looked at this line yet (Look).
    private bool looked;

    protected CalendarLineTile(string key, WidgetItem island, Gfx gfx)
        : base(key, island, gfx)
    {
        Store = CalendarLines.Store;
    }

    /// <summary>Where its events come from: the shared store, or a test's own.</summary>
    protected CalendarStore Store { get; }

    /// <summary>Never ticks: the store reads on its own clock and wakes this line when the events change.</summary>
    public override int IntervalMs => 0;

    protected override int Count => 1;

    public override void Configure(WidgetItem island, Config config)
    {
        Settings = island;
        // Only to be woken: attaching reads nothing and opens nothing.
        Store.Attach(this);
        Forget();
    }

    public override string? Tooltip(int index) => null;

    /// <summary>Whether a calendar could not be read and what it gave before is shown (StaleMark.cs).</summary>
    protected bool Stale => Store.Freshness.Stale;

    /// <summary>"Last updated 14:05 — couldn't refresh" while <see cref="Stale"/>.</summary>
    internal override string? StaleTip => Store.Freshness.Tip(Clock.Now());

    /// <summary>
    /// The stale mark at the trailing gutter of <paramref name="box"/>, beside the row from
    /// <paramref name="top"/> that is <paramref name="height"/> tall, where the line is stale; the piece
    /// it is, for the tooltip over it, or null. Drawn where <paramref name="dc"/> is not null.
    /// </summary>
    protected LinePiece? StalePiece(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, float top, float height)
    {
        if (!Stale)
        {
            return null;
        }
        var sketch = new LineSketch(dc, Gfx);
        StaleMark.Sketch(sketch, box, style, ListForm.Of(Gfx, style), top, height);
        return sketch.Pieces.Count > 0 ? sketch.Pieces[^1] : null;
    }

    /// <summary>What the stale mark takes from the trailing edge of a row it stands beside: nothing while not stale.</summary>
    protected float StaleRoom(in TileStyle style) => StaleMark.Room(style, ListForm.Of(Gfx, style), Stale);

    /// <summary>Calendars to read again ("Refresh", IRefreshable): an account's, or one of the store's own.</summary>
    public bool CanRefresh => Store.CanReachAccounts || Store.HasCalendars;

    /// <summary>The accounts' calendars are being read.</summary>
    public bool Refreshing => Store.Reading;

    /// <summary>When the events shown were read.</summary>
    public DateTime? UpdatedUtc => Store.Freshness.UpdatedUtc;

    /// <summary>Why the events could not be read: an account's or a calendar's problem, or "Last updated 14:05 — couldn't refresh".</summary>
    string? IRefreshable.Problem => Store.Problem ?? Store.Freshness.Tip(Clock.Now());

    /// <summary>A new minute: the now-line moves, and today turns over at midnight.</summary>
    public override bool MinuteChanged(Gfx gfx) => Redraw(gfx);

    public override void Dispose()
    {
        Store.Detach(this);
        base.Dispose();
    }

    /// <summary>
    /// Asks the store for events, the first time anybody looks at this line. Once: the shared store
    /// has been open since the dock's start, and asking starts only what is due.
    /// </summary>
    protected void Look()
    {
        if (looked)
        {
            return;
        }
        looked = true;
        Store.Open();
    }

    /// <summary>"Refresh": every account's calendars read again now, whatever is due (<see cref="CalendarStore.Refresh"/>).</summary>
    public void RefreshNow()
    {
        looked = true;
        Store.Refresh();
    }

    /// <summary>The store posted: what arrived is taken, and the redraw that follows shows it.</summary>
    protected override bool Sample(bool everything)
    {
        if (everything)
        {
            Store.Deliver();
        }
        return false;
    }

    /// <summary>Never drawn on a bar: the calendar's pieces are lines of a list.</summary>
    internal override void PaintCell(ID2D1DeviceContext* dc, int index, in IslandStyle style, float width, float height) =>
        throw new NotSupportedException($"{Info.Id} is a line of a folder's list, not a widget of a bar.");

    /// <summary>Today, by the clock a test can freeze.</summary>
    protected static DateOnly Today => DateOnly.FromDateTime(Clock.Now());
}
