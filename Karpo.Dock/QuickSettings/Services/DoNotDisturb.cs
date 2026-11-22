// Windows' own do not disturb, as one service the widgets share: the bell on the
// bar wears its "Zzz", the notification list's header switches it, the one-button
// widget switches it from the bar, a card or a line, and whatever comes later — a
// focus session that turns it on, an indicator that shows it — reads it and sets
// it here rather than finding its own way to Windows.
//
// WHAT IT IS. The setting Windows 11's notification center toggles: with it on,
// Windows shows no banners but for priority notifications and alarms, and the
// notifications still go to the list. It is not Karpo's. Nothing here keeps a
// copy of it in Karpo's files, and turning it on in Karpo turns it on in Windows,
// for every toast of every app.
//
// HOW WINDOWS SETS IT, FOUND BY LOOKING RATHER THAN GUESSED. Do not disturb is the
// quiet-hours machinery that Focus assist was in Windows 10, renamed. It lives in
// the Windows Push Notifications user service (WpnUserService, QuietHours.dll),
// and every piece of Windows that switches it goes through one COM object there:
// CLSID_QuietHoursSettings {F53321FA-34F8-4B7F-B9A3-361877CB94CF}, interface
// IQuietHoursSettings {6BFF4732-81EC-4FFB-AE67-B6C1BC29631F}. On build 26200 that
// class and that interface are referenced by the notification center itself
// (ShellExperienceHost\Windows.UI.ActionCenter.dll, which also carries the three
// profile names below), by the Settings page (SettingsHandlers_Notifications.dll),
// by the shell in explorer (twinui.pcshell.dll) and by Windows Security's agent;
// the AppID says LocalService=WpnUserService, and its proxy is registered, so a
// process outside the service is expected to call it. What it holds is a
// "profile": Microsoft.QuietHoursProfile.Unrestricted (off), .PriorityOnly (on:
// priority notifications and alarms) and .AlarmsOnly (on: alarms). The person's
// own choice is UserSelectedProfile; an automatic rule — the schedule, a game, a
// duplicated display, a focus session — lays its own over it while it holds
// (ActiveQuietMomentProfile); and what is in force is ActiveProfile.
//
// No public API sets it. ToastNotificationManagerForUser.NotificationMode is the
// documented read (Windows 11; E_NOINTERFACE on Windows 10) and has no setter;
// Windows.UI.Shell.FocusSessionManager is a limited-access feature; and writing the
// CloudStore blob behind it only takes effect once WpnUserService is restarted.
// So the choice was between writing Windows' storage behind its back and calling
// the interface Windows' own toggle calls, and the interface is the one that keeps
// Windows the owner of its own setting: the service does what it does for the
// notification center, and everything that follows it hears.
//
// The layout is Rafael Rivera's reverse-engineered IDL (Windows 10 1903), and it is
// MEASURED on 26200 rather than trusted: read-only calls to slots 3, 7, 8, 9, 10
// and 12 answered Unrestricted, the three profiles' names ("Off", "Priority only",
// "Alarms only"), a quiet-moments manager, Unrestricted, "" and Unrestricted, which
// is this PC's state as the documented NotificationMode reported it at the same
// moment. Slot 4, the setter, sits between them; it was not called, because
// nothing Karpo runs on the person's PC outside the dock may change it.
//
//   IQuietHoursSettings  3 get_UserSelectedProfile  4 put_UserSelectedProfile
//                        8 get_QuietMomentsManager  9 get_OffProfileId
//                        10 get_ActiveQuietMomentProfile  12 get_ActiveProfile
//   IQuietMomentsManager 5 TurnOffCurrentlyActiveQuietMoment
//
// Every string comes back from CoTaskMemAlloc and goes back to it.
//
// HOW IT IS HEARD. ToastNotificationManagerForUser.NotificationModeChanged, the
// documented event for the mode in force, which answered on 26200 from this
// unpackaged process; each time it fires, the state is read again. Nothing polls.
// Where it is missing (Windows 10) the state is read when it is first looked at
// and after every change Karpo makes, and not otherwise.
//
// Turning it on sets the person's choice to PriorityOnly, which is what Windows'
// own toggle does. Turning it off sets it to the off profile and, where a rule is
// what holds it on, turns that rule off for now (TurnOffCurrentlyActiveQuietMoment),
// as Windows' toggle does: the schedule itself is left alone and comes round again.
// The schedule and the rules are Windows' Settings' to show (ms-settings:
// quietmomentsscheduled, the Notifications page on Windows 11), and Karpo links to
// it rather than keeping a second one.
//
// NOTHING IS ASKED OF WINDOWS ON THE DOCK'S THREAD. Every call above is a call into
// another process, so Start, a read and a switch all go to the pool, and the dock
// hears of them through Changed on its own thread. Until the first answer, On is
// null: blank, never wrong (docs\widgets.md §11). And a run that may not reach the
// person's world — every test, every smoke pass — is given a source that reaches
// nothing (Source), with WindowsDoNotDisturb.Set refusing one as well, so the
// guarantee is in the thing being reached and not in each caller remembering it.

using System.Runtime.InteropServices;
using Karpo.Core;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
using Notifications = Windows.UI.Notifications;

namespace Karpo.Dock.QuickSettings.Services;

/// <summary>What Windows says about do not disturb.</summary>
/// <param name="On">
/// Whether it is in force — by the person's choice or by an automatic rule — or null while nobody
/// has asked, or Windows would not say.
/// </param>
/// <param name="AlarmsOnly">On with only alarms let through, rather than priority notifications as well.</param>
/// <param name="Automatic">On because a rule turned it on (the schedule, a game, a focus session), not the person.</param>
/// <param name="CanSwitch">
/// Whether Karpo can switch it: false where Windows' own interface did not answer, and then a
/// click opens Windows' settings for it instead.
/// </param>
internal sealed record DndState(bool? On, bool AlarmsOnly = false, bool Automatic = false, bool CanSwitch = true)
{
    /// <summary>Nothing asked yet: what every source starts as.</summary>
    public static DndState Unknown { get; } = new(On: null);
}

internal interface IDoNotDisturbSource : IDisposable
{
    /// <summary>
    /// Starts following Windows' state; <paramref name="changed"/> comes from any thread. Called at
    /// the first look (<see cref="DoNotDisturb.Look"/>), once, and it must not block.
    /// </summary>
    void Start(Action changed);

    /// <summary>What Windows said last. A plain record: nothing here is a COM object.</summary>
    DndState Read();

    /// <summary>Asks Windows to turn it on or off, off the calling thread; <c>changed</c> follows.</summary>
    void Set(bool on);
}

/// <summary>
/// Do not disturb as the widgets see it: one per thread, like the notification store — the dock's
/// one thread has one, and so does each offscreen test's — read by anything, switched by anything,
/// and saying so through <see cref="Changed"/> on the thread that uses it.
/// </summary>
internal sealed class DoNotDisturb
{
    /// <summary>Where Windows keeps the schedule and the rules: the Notifications page on Windows 11.</summary>
    internal const string Schedule = "ms-settings:quietmomentsscheduled";

    /// <summary>Windows 11's own glyph for it (Segoe Fluent Icons): the bell with "zZ" its taskbar shows.</summary>
    internal const string Glyph = "\uF285";

    [ThreadStatic]
    private static DoNotDisturb? shared;

    [ThreadStatic]
    private static Func<IDoNotDisturbSource>? made;

    private readonly IDoNotDisturbSource source;

    /// <summary>The thread that looked first, and its dispatcher queue: where news from Windows is raised.</summary>
    private int owner;

    private Windows.System.DispatcherQueue? queue;
    private bool started;

    /// <summary>
    /// A switch asked for and not yet answered: shown at once, so a click lands under the pointer
    /// rather than a round trip to another process later, and dropped when Windows answers — with
    /// what it did, which is what stands.
    /// </summary>
    private bool? wished;

    // edit: edit mode's dim (Dock.EditDeck.cs, EditShade) is a window over the whole of every
    // display, above the rest, and Windows' automatic rule "When using an app in full-screen mode"
    // takes it for an app in full screen: do not disturb comes on by itself for as long as edit mode
    // lasts, and the status icons, the bell and the switch all said so (the person, 2026-10-08: "in
    // edit mode the status icons widget always shows the do not disturb icon even when do not
    // disturb is off"). That is Karpo's own doing, not the person's, so while the dim stands, and
    // until Windows has spoken once after it has gone, a rule's "on" that the dim found off is not
    // shown (Undimmed). One the person chooses, and one that was on before the dim, are.
    private bool dimmed;
    private bool lifting;
    private bool offUnderDim;

    // notifications: a timed one's end (TurnOnUntil): Karpo's own clock, for Windows has no "for an
    // hour" of its own — its schedule is a daily one, the person's, and not for a one-off. Kept here
    // and not in a file: a Karpo that exits before it leaves it on, as Windows' own toggle would.
    private readonly TimeProvider time;
    private ITimer? ender;
    private DateTimeOffset? until;

    internal DoNotDisturb(IDoNotDisturbSource source, TimeProvider? time = null)
    {
        this.source = source;
        this.time = time ?? TimeProvider.System;
    }

    /// <summary>
    /// This thread's: the dock's, in the dock. Windows' own source, and on a run that may not reach
    /// the person's world one that reaches nothing (<see cref="Source"/>).
    /// </summary>
    public static DoNotDisturb Shared => shared ??= new DoNotDisturb(Source());

    /// <summary>Where the source comes from, for a test that wants one of its own. Per thread.</summary>
    internal static Func<IDoNotDisturbSource>? Made
    {
        get => made;
        set
        {
            made = value;
            // A test that hands one over wants the service built on it, not one made before.
            shared = null;
        }
    }

    /// <summary>
    /// The state Windows reports — or, while a switch is on its way, what was asked for.
    /// </summary>
    public DndState State
    {
        get
        {
            var said = source.Read();
            return wished is { } on ? said with { On = on, Automatic = false, AlarmsOnly = on && said.AlarmsOnly }
                : (dimmed || lifting) && offUnderDim ? Undimmed(said)
                : said;
        }
    }

    /// <summary>
    /// What <paramref name="said"/> is with edit mode's dim taken out of it: a rule's "on" is off,
    /// for the rule that holds it is Windows taking the dim for an app in full screen (see
    /// <see cref="Dimmed"/>). The person's own "on" stands.
    /// </summary>
    internal static DndState Undimmed(DndState said) =>
        said is { On: true, Automatic: true } ? said with { On = false, AlarmsOnly = false, Automatic = false } : said;

    /// <summary>
    /// edit: edit mode's dim comes up over the displays (<paramref name="up"/>) or goes. While it
    /// stands, a do not disturb that a rule turns on, where it was off as the dim came, is shown off
    /// (<see cref="Undimmed"/>), and so it is after the dim has gone until Windows next says what it
    /// is, as its rule lets go a moment later. Neither changes what is shown there and then: the
    /// rule comes, and goes, with Windows' word, which <see cref="Changed"/> brings.
    /// </summary>
    public void Dimmed(bool up)
    {
        if (up == dimmed)
        {
            return;
        }
        if (up)
        {
            offUnderDim = State.On != true;
            lifting = false;
        }
        else
        {
            lifting = offUnderDim;
        }
        dimmed = up;
    }

    /// <summary>Whether it is on; null until Windows has answered.</summary>
    public bool? On => State.On;

    /// <summary>Whether this is Windows' own and not a made-up one: never, in a test (Source).</summary>
    internal bool ReachesWindows => source is WindowsDoNotDisturb;

    /// <summary>It changed, or Windows answered. On the thread that looked first.</summary>
    public event Action? Changed;

    /// <summary>
    /// The first look starts following Windows (docs\widgets.md §11): a widget's first draw, the
    /// notification list opening, a switch. Later calls change nothing.
    /// </summary>
    public void Look()
    {
        Own();
        if (started)
        {
            return;
        }
        started = true;
        Volatile.Write(ref queue, Windows.System.DispatcherQueue.GetForCurrentThread());
        source.Start(Heard);
    }

    /// <summary>
    /// Turns it on or off in Windows, until turned off again: a time it was on for is over. The change
    /// shows at once; Windows' answer follows.
    /// </summary>
    public void Set(bool on)
    {
        Ends(null);
        Switch(on);
    }

    /// <summary>
    /// notifications: on from now until <paramref name="end"/> (a menu's "for 1 hour", "until
    /// tomorrow"), and then off again, unless it was turned off, or on for another time, before.
    /// </summary>
    public void TurnOnUntil(DateTimeOffset end)
    {
        Switch(true);
        Ends(end);
    }

    /// <summary>When a timed one ends; null while it is off, and while on until turned off.</summary>
    public DateTimeOffset? Until => until;

    /// <summary>notifications: the start of the day after <paramref name="now"/>'s, in <paramref name="zone"/>: where "until tomorrow" ends.</summary>
    internal static DateTimeOffset Tomorrow(DateTimeOffset now, TimeZoneInfo zone)
    {
        var day = TimeZoneInfo.ConvertTime(now, zone).Date.AddDays(1);
        // A day that starts in a clock change's gap starts at the gap's end.
        while (zone.IsInvalidTime(day))
        {
            day = day.AddMinutes(30);
        }
        return new DateTimeOffset(day, zone.GetUtcOffset(day));
    }

    /// <summary>The start of tomorrow on this clock, here.</summary>
    internal DateTimeOffset Tomorrow() => Tomorrow(time.GetUtcNow(), time.LocalTimeZone);

    /// <summary>An hour from now on this clock.</summary>
    internal DateTimeOffset InAnHour() => time.GetUtcNow().AddHours(1);

    private void Switch(bool on)
    {
        Look();
        wished = on;
        Changed?.Invoke();
        source.Set(on);
    }

    /// <summary>
    /// The time it is on for set to <paramref name="end"/>, or none. Its clock looks at least once a
    /// minute, so a PC that slept through the end turns it off within a minute of waking.
    /// </summary>
    private void Ends(DateTimeOffset? end)
    {
        until = end;
        if (end is null)
        {
            ender?.Dispose();
            ender = null;
            return;
        }
        var left = end.Value - time.GetUtcNow();
        var due = left <= TimeSpan.Zero ? TimeSpan.Zero : left < TimeSpan.FromMinutes(1) ? left : TimeSpan.FromMinutes(1);
        ender ??= time.CreateTimer(static state => ((DoNotDisturb)state!).Ticked(), this, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        ender.Change(due, Timeout.InfiniteTimeSpan);
    }

    /// <summary>From the clock, on any thread: looked at on the owner's, as Windows' news is.</summary>
    private void Ticked()
    {
        if (Environment.CurrentManagedThreadId == Volatile.Read(ref owner))
        {
            Due();
            return;
        }
        _ = Volatile.Read(ref queue)?.TryEnqueue(() => Troubles.Guard("Do not disturb: its time", Due));
    }

    /// <summary>The time it was on for is over: off, where it is the person's on and not a rule's; else a minute on.</summary>
    private void Due()
    {
        if (until is not { } end)
        {
            return;
        }
        if (time.GetUtcNow() < end)
        {
            Ends(end);
            return;
        }
        Ends(null);
        if (State is { On: true, Automatic: false })
        {
            Switch(false);
        }
        else
        {
            Changed?.Invoke();
        }
    }

    /// <summary>Off where it is on, on where it is off or nobody knows yet.</summary>
    public void Toggle() => Set(On != true);

    /// <summary>
    /// From the source, on any thread: raised here when it is the owner's, and through the owner's
    /// dispatcher queue when it is not.
    /// </summary>
    private void Heard()
    {
        if (Environment.CurrentManagedThreadId == Volatile.Read(ref owner))
        {
            Arrived();
            return;
        }
        _ = Volatile.Read(ref queue)?.TryEnqueue(() => Troubles.Guard("Do not disturb: hearing Windows", Arrived));
    }

    private void Arrived()
    {
        wished = null;
        // edit: Windows has spoken since the dim went: what it says stands again.
        lifting = false;
        // notifications: turned off elsewhere (Windows' own center) before its time was up: the time is over too.
        if (until is not null && source.Read().On == false)
        {
            Ends(null);
        }
        Changed?.Invoke();
    }

    /// <summary>
    /// Claims the service for this thread on the first touch, and refuses any other after that, for
    /// the reason NotificationStore does: a handler raised on the wrong thread is a failing line
    /// three calls later, somewhere else.
    /// </summary>
    private void Own()
    {
        var me = Environment.CurrentManagedThreadId;
        var first = Interlocked.CompareExchange(ref owner, me, 0);
        if (first != 0 && first != me)
        {
            throw new InvalidOperationException(
                $"Do not disturb belongs to thread {first}, the first to use it, and was reached from thread {me}. "
                + "Each thread takes its own (DoNotDisturb.Shared is per thread).");
        }
    }

    /// <summary>
    /// Windows' own, except on a run that may not reach the person's world: every test and every
    /// smoke pass gets one that reaches nothing, which is where the guarantee lives
    /// (docs\widgets.md §11).
    /// </summary>
    private static IDoNotDisturbSource Source() =>
        Made?.Invoke() ?? (Config.IsIsolated || QuickSources.UseSamples ? new SampleDoNotDisturb() : new WindowsDoNotDisturb());

    /// <summary>
    /// The line a tooltip and a card say under the name: what it lets through, or why a click will
    /// open Windows' settings. Pure: the tests check the words.
    /// </summary>
    internal static string Note(DndState state) => state switch
    {
        { CanSwitch: false } => S.DockQuickDndRefused,
        { On: true, Automatic: true } => S.DockQuickDndAutomatic,
        { On: true, AlarmsOnly: true } => S.DockQuickDndAlarmsOnly,
        _ => S.DockQuickDndPriority,
    };
}

/// <summary>
/// Do not disturb as a one-button widget, in night light's family (SwitchIsland): on the bar, a card
/// and a line. It keeps nothing of its own — the state is the service's, so the widget, the bell
/// and the notification list's header never disagree — and says what Windows' own button says.
/// </summary>
internal sealed class DoNotDisturbSwitch : IQuickSwitch
{
    private readonly DoNotDisturb service;
    private Action? changed;
    private bool following;

    public DoNotDisturbSwitch(string name, DoNotDisturb service)
    {
        Name = name;
        this.service = service;
    }

    public string Name { get; }

    public bool Available => true;

    public bool? On => service.On;

    /// <summary>Whether a click switches it (<see cref="Act"/>): while Windows lets Karpo.</summary>
    public bool Switches => service.State.CanSwitch;

    public string? Note => DoNotDisturb.Note(service.State);

    /// <summary>The service starts following Windows here, at the widget's first look, if nothing has yet.</summary>
    public void Start(Action onChanged)
    {
        changed = onChanged;
        if (!following)
        {
            following = true;
            service.Changed += Heard;
        }
        service.Look();
    }

    /// <summary>Switches it; where Windows would not let Karpo, opens Windows' own settings for it.</summary>
    public void Act()
    {
        if (!service.State.CanSwitch)
        {
            Launch.Open(DoNotDisturb.Schedule);
            return;
        }
        service.Toggle();
    }

    public void Dispose()
    {
        if (following)
        {
            following = false;
            service.Changed -= Heard;
        }
    }

    private void Heard() => changed?.Invoke();
}

/// <summary>
/// Windows' own, through the interface its notification center switches it with. See the top of
/// this file for what each slot is and how that was found out.
/// </summary>
internal sealed unsafe class WindowsDoNotDisturb : IDoNotDisturbSource
{
    internal const string Unrestricted = "Microsoft.QuietHoursProfile.Unrestricted";
    internal const string PriorityOnly = "Microsoft.QuietHoursProfile.PriorityOnly";
    internal const string AlarmsOnly = "Microsoft.QuietHoursProfile.AlarmsOnly";

    private static readonly Guid SettingsClass = new("F53321FA-34F8-4B7F-B9A3-361877CB94CF");
    private static readonly Guid SettingsInterface = new("6BFF4732-81EC-4FFB-AE67-B6C1BC29631F");

    private volatile DndState state = DndState.Unknown;
    private Action? changed;
    private Notifications.ToastNotificationManagerForUser? manager;
    private Windows.Foundation.TypedEventHandler<Notifications.ToastNotificationManagerForUser, object>? modeChanged;
    private volatile bool disposed;

    public void Start(Action onChanged)
    {
        changed = onChanged;
        // Both on the pool: each is a call into WpnUserService, and the dock's thread is drawing.
        _ = Task.Run(() => Troubles.Guard("Do not disturb: following it", () =>
        {
            Follow();
            Look();
        }));
    }

    public DndState Read() => state;

    public void Set(bool on)
    {
        // The guarantee, in the thing being reached: no test and no smoke pass switches the
        // person's do not disturb, whatever a caller got wrong (docs\widgets.md §11). What was
        // asked for is answered with what stands, so nothing waits on a switch that never comes.
        if (Config.IsIsolated)
        {
            changed?.Invoke();
            return;
        }
        _ = Task.Run(() =>
        {
            try
            {
                Switch(on);
            }
            catch (Exception error)
            {
                Troubles.Note("Do not disturb: switching it", error);
                state = state with { CanSwitch = false };
            }
            Look();
        });
    }

    public void Dispose()
    {
        disposed = true;
        if (manager is { } told && modeChanged is { } handler)
        {
            try
            {
                told.NotificationModeChanged -= handler;
            }
            catch (Exception)
            {
            }
        }
        manager = null;
        modeChanged = null;
    }

    /// <summary>Asks Windows to say when the mode in force changes, so nothing here polls.</summary>
    private void Follow()
    {
        if (disposed)
        {
            return;
        }
        try
        {
            var told = Notifications.ToastNotificationManager.GetDefault();
            modeChanged = (_, _) => Look();
            told.NotificationModeChanged += modeChanged;
            manager = told;
        }
        catch (Exception error)
        {
            // Windows 10 has no such event (E_NOINTERFACE): the state is read when it is looked at
            // and after every change Karpo makes, and that is all.
            Troubles.Note("Do not disturb: following Windows' notification mode", error);
            modeChanged = null;
        }
    }

    /// <summary>Reads what is in force, and tells the dock when it moved.</summary>
    private void Look()
    {
        if (disposed)
        {
            return;
        }
        DndState found;
        try
        {
            found = Ask();
        }
        catch (Exception error)
        {
            Troubles.Note("Do not disturb: reading it", error);
            found = Documented();
        }
        if (found == state)
        {
            return;
        }
        state = found;
        changed?.Invoke();
    }

    /// <summary>The documented read, where the interface would not answer: on or off, and no way to switch it.</summary>
    private static DndState Documented()
    {
        try
        {
            var mode = Notifications.ToastNotificationManager.GetDefault().NotificationMode;
            return new DndState(
                mode != Notifications.ToastNotificationMode.Unrestricted,
                AlarmsOnly: mode == Notifications.ToastNotificationMode.AlarmsOnly,
                CanSwitch: false);
        }
        catch (Exception error)
        {
            Troubles.Note("Do not disturb: Windows' notification mode", error);
            return DndState.Unknown with { CanSwitch = false };
        }
    }

    /// <summary>The profile in force, the person's own choice and a rule's, from Windows' own object.</summary>
    private static DndState Ask()
    {
        var settings = Make();
        try
        {
            var off = Text(settings, 9) ?? Unrestricted;
            var active = Text(settings, 12) ?? off;
            var chosen = Text(settings, 3) ?? off;
            var on = !Same(active, off);
            return new DndState(on, AlarmsOnly: Same(active, AlarmsOnly), Automatic: on && Same(chosen, off));
        }
        finally
        {
            _ = Marshal.Release(settings);
        }
    }

    /// <summary>
    /// On: the person's choice becomes priority only, as Windows' own toggle makes it. Off: their
    /// choice goes back to the off profile, and a rule that holds it on is turned off until it next
    /// comes round.
    /// </summary>
    private static void Switch(bool on)
    {
        var settings = Make();
        try
        {
            var off = Text(settings, 9) ?? Unrestricted;
            if (on)
            {
                Put(settings, PriorityOnly);
                return;
            }
            if (!Same(Text(settings, 3) ?? off, off))
            {
                Put(settings, off);
            }
            if (Text(settings, 10) is { Length: > 0 })
            {
                nint moments = 0;
                ComCheck(((delegate* unmanaged<nint, nint*, int>)Slot(settings, 8))(settings, &moments), "get_QuietMomentsManager");
                try
                {
                    ComCheck(((delegate* unmanaged<nint, int>)Slot(moments, 5))(moments), "TurnOffCurrentlyActiveQuietMoment");
                }
                finally
                {
                    _ = Marshal.Release(moments);
                }
            }
        }
        finally
        {
            _ = Marshal.Release(settings);
        }
    }

    /// <summary>Windows' own QuietHoursSettings, in WpnUserService. Throws where there is none.</summary>
    private static nint Make()
    {
        var classId = SettingsClass;
        var interfaceId = SettingsInterface;
        void* made = null;
        ComCheck(CoCreateInstance(&classId, null, (uint)CLSCTX.CLSCTX_LOCAL_SERVER, &interfaceId, &made).Value, "QuietHoursSettings");
        return (nint)made;
    }

    private static void* Slot(nint self, int index) => (*(void***)self)[index];

    /// <summary>A profile id from one of the getters; null where it said nothing.</summary>
    private static string? Text(nint settings, int slot)
    {
        char* text = null;
        ComCheck(((delegate* unmanaged<nint, char**, int>)Slot(settings, slot))(settings, &text), $"slot {slot}");
        try
        {
            return text is null ? null : new string(text);
        }
        finally
        {
            Marshal.FreeCoTaskMem((nint)text);
        }
    }

    private static void Put(nint settings, string profile)
    {
        fixed (char* id = profile)
        {
            ComCheck(((delegate* unmanaged<nint, char*, int>)Slot(settings, 4))(settings, id), "put_UserSelectedProfile");
        }
    }

    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static void ComCheck(int result, string what)
    {
        if (result < 0)
        {
            throw new COMException($"IQuietHoursSettings {what}", result);
        }
    }
}

/// <summary>
/// A made-up do not disturb for pictures and tests, and what any isolated run gets. It reaches
/// nothing: switching it switches this object and nothing else.
/// </summary>
internal sealed class SampleDoNotDisturb : IDoNotDisturbSource
{
    private DndState state;
    private Action? changed;

    /// <param name="on">Where it starts: off by default, as a PC mostly is.</param>
    public SampleDoNotDisturb(bool on = false, bool automatic = false, bool canSwitch = true) =>
        state = new DndState(on, Automatic: on && automatic, CanSwitch: canSwitch);

    public void Start(Action onChanged)
    {
        changed = onChanged;
        onChanged();
    }

    public DndState Read() => state;

    public void Set(bool on)
    {
        if (state.CanSwitch)
        {
            state = state with { On = on, Automatic = false };
        }
        changed?.Invoke();
    }

    public void Dispose()
    {
    }
}
