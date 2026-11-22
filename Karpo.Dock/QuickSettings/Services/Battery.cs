// The battery for the battery's line and the power mode's (the battery preset's,
// the power preset's): the charge, whether it is charging and
// the time left (GetSystemPowerStatus; the time to full from the aggregate
// battery's charge rate), energy saver, and the power mode of Windows' "Power &
// battery" page. The power mode is Windows' power overlay (powrprof's
// PowerGetEffectiveOverlayScheme and PowerSetActiveOverlayScheme, which Windows
// Settings itself uses; exported since Windows 10 1709, not in the SDK headers).
// A PC whose power plan takes no overlay doesn't offer the choice. Setting it is
// asked of Windows on the pool, never on the dock's thread (SetMode).
//
// While the folder is open it follows the charge, the power source and energy
// saver (power setting notifications) and the power mode (effective power mode
// notifications), each by a callback on a system thread that only posts.

using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
using Troubles = Karpo.Core.Troubles;

namespace Karpo.Dock.QuickSettings.Services;

internal enum PowerMode
{
    Efficiency,
    Balanced,
    Performance,
}

/// <param name="Present">A system battery exists.</param>
/// <param name="Remaining">On battery: how long it lasts, when Windows knows.</param>
/// <param name="UntilFull">Charging: how long until full, from the charge rate, when the battery reports one.</param>
/// <param name="Mode">The power mode; null when this PC's plan offers none.</param>
/// <param name="SaverKnown">
/// Windows tells apps where energy saver stands on this PC: a battery has always had it, and since
/// Windows 11 24H2 a PC on the wall has it too (the OS knows GUID_ENERGY_SAVER_STATUS). False on an
/// older Windows without a battery, where the feature isn't there at all and no row is shown.
/// </param>
internal sealed record BatteryState(bool Present, int Percent, bool PluggedIn, bool Charging, TimeSpan? Remaining, TimeSpan? UntilFull, bool Saver, PowerMode? Mode, bool SaverKnown = false);

internal interface IBatterySource : IDisposable
{
    void Start(Changes changes);

    BatteryState Read();

    /// <summary>
    /// Asks Windows for the power mode, never holding the caller: the answer comes as news —
    /// <see cref="BatteryNews.Power"/> once it is in force, <see cref="BatteryNews.Refused"/> where
    /// Windows kept the one it had.
    /// </summary>
    void SetMode(PowerMode mode);
}

internal static class BatteryNews
{
    public const int Power = 1;

    /// <summary>Windows refused the power mode asked for.</summary>
    public const int Refused = 2;
}

internal sealed unsafe class WindowsBattery : IBatterySource
{
    // The overlays Windows 11's power modes stand for; Balanced is none (all zeros).
    internal static readonly Guid BestEfficiency = new("961CC777-2547-4F9D-8174-7D86181B8A7A");
    internal static readonly Guid BestPerformance = new("DED574B5-45A0-4F42-8737-46345C09C238");

    private const uint DeviceNotifyCallback = 2;
    private const uint EffectivePowerModeV2 = 2;

    // The Changes each registration reports to, by the number its context carries.
    private static readonly ConcurrentDictionary<nint, Changes> Targets = new();
    private static long nextNumber;

    private readonly List<nint> registrations = [];
    private nint modeRegistration;
    private bool saverKnown;
    private Subscription* subscription;
    private nint number;
    private bool disposed;

    public void Start(Changes changes)
    {
        number = (nint)Interlocked.Increment(ref nextNumber);
        Targets[number] = changes;
        Guid[] settings =
        [
            GUID.GUID_ACDC_POWER_SOURCE,
            GUID.GUID_BATTERY_PERCENTAGE_REMAINING,
            GUID.GUID_POWER_SAVING_STATUS,
            GUID.GUID_ENERGY_SAVER_STATUS,
        ];
        // Kept until the registrations are undone: nothing says Windows copies it.
        subscription = (Subscription*)NativeMemory.Alloc((nuint)sizeof(Subscription));
        subscription->Callback = &SettingChanged;
        subscription->Context = (void*)number;
        foreach (var setting in settings)
        {
            var id = setting;
            void* handle = null;
            // The newer settings don't exist on older Windows 11: refused, and fine.
            if (PowerSettingRegisterNotification(&id, DeviceNotifyCallback, (HANDLE)subscription, &handle) == 0 && handle != null)
            {
                registrations.Add((nint)handle);
                if (setting == GUID.GUID_ENERGY_SAVER_STATUS)
                {
                    // Windows knows this setting exactly where energy saver does — since 24H2 that
                    // includes a PC on the wall, where the row is worth showing.
                    saverKnown = true;
                }
            }
        }
        try
        {
            void* mode = null;
            if (PowerRegisterForEffectivePowerModeNotifications(EffectivePowerModeV2, &ModeChanged, (void*)number, &mode).SUCCEEDED)
            {
                modeRegistration = (nint)mode;
            }
        }
        catch (EntryPointNotFoundException)
        {
            // Before Windows 10 1809: the mode shows as it was when the flyout opened.
        }
    }

    public BatteryState Read()
    {
        SYSTEM_POWER_STATUS status = default;
        var read = GetSystemPowerStatus(&status);
        // BatteryFlag 128: no system battery; 255: can't tell, which has that bit too.
        var present = read && (status.BatteryFlag & 128) == 0 && status.BatteryLifePercent <= 100;
        var plugged = status.ACLineStatus == 1;
        var charging = present && (status.BatteryFlag & 8) != 0;
        TimeSpan? remaining = present && !plugged && status.BatteryLifeTime != uint.MaxValue ? TimeSpan.FromSeconds(status.BatteryLifeTime) : null;
        return new BatteryState(present, present ? status.BatteryLifePercent : 0, plugged, charging, remaining,
            charging ? UntilFull() : null, EnergySaver.Saving(status.SystemStatusFlag == 1, EnergySaver.Shared.On), Mode(), present || saverKnown || SaverSupported());
    }

    /// <summary>
    /// What sets the overlay: Windows' own (<see cref="Apply"/>) unless a test hands in a stand-in,
    /// which is how no test ever changes the power mode of the PC it runs on.
    /// </summary>
    private readonly Func<Guid, bool> apply;

    public WindowsBattery()
        : this(Apply)
    {
    }

    internal WindowsBattery(Func<Guid, bool> apply) => this.apply = apply;

    /// <summary>
    /// Asks Windows for <paramref name="mode"/> on the pool, in turn, never on the dock's thread:
    /// powrprof answers once the power manager has applied the overlay, and the folder (and, with a
    /// menu's hook in, the pointer) stood still for it, as it did for a Wi-Fi connect (OffThread.cs)
    /// and a default audio device (Sound.cs). The answer comes back as news: the mode in force, or
    /// Windows' refusal.
    /// </summary>
    public void SetMode(PowerMode mode)
    {
        var overlay = OverlayOf(mode);
        var number = this.number;
        lock (SettingGate)
        {
            setting = setting.ContinueWith(
                _ => Troubles.Guard("Battery: setting the power mode", () =>
                {
                    var taken = apply(overlay);
                    if (Targets.TryGetValue(number, out var changes))
                    {
                        changes.Add(taken ? BatteryNews.Power : BatteryNews.Refused);
                    }
                }),
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default);
        }
    }

    // The modes asked for, one after another in the order asked.
    private static readonly object SettingGate = new();
    private static Task setting = Task.CompletedTask;

    /// <summary>The last mode asked for (for a test).</summary>
    internal static Task Setting
    {
        get
        {
            lock (SettingGate)
            {
                return setting;
            }
        }
    }

    /// <summary>The overlay Windows 11 sets for <paramref name="mode"/>; Balanced is none.</summary>
    internal static Guid OverlayOf(PowerMode mode) => mode switch
    {
        PowerMode.Efficiency => BestEfficiency,
        PowerMode.Performance => BestPerformance,
        _ => Guid.Empty,
    };

    /// <summary>Sets <paramref name="overlay"/> as Windows Settings does; false where Windows refused.</summary>
    private static bool Apply(Guid overlay)
    {
        try
        {
            return PowerSetActiveOverlayScheme(&overlay) == 0;
        }
        catch (Exception error) when (error is EntryPointNotFoundException or DllNotFoundException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }
        disposed = true;
        Targets.TryRemove(number, out _);
        foreach (var handle in registrations)
        {
            _ = PowerSettingUnregisterNotification((void*)handle);
        }
        registrations.Clear();
        if (modeRegistration != 0)
        {
            _ = PowerUnregisterFromEffectivePowerModeNotifications((void*)modeRegistration);
            modeRegistration = 0;
        }
        if (subscription != null)
        {
            NativeMemory.Free(subscription);
            subscription = null;
        }
    }

    /// <summary>The power mode for an overlay: anything but the two named ones is Balanced, as Settings shows it.</summary>
    internal static PowerMode ModeOf(Guid overlay) =>
        overlay == BestEfficiency ? PowerMode.Efficiency : overlay == BestPerformance ? PowerMode.Performance : PowerMode.Balanced;

    private static PowerMode? Mode()
    {
        try
        {
            Guid overlay;
            return PowerGetEffectiveOverlayScheme(&overlay) == 0 ? ModeOf(overlay) : null;
        }
        catch (Exception error) when (error is EntryPointNotFoundException or DllNotFoundException)
        {
            return null;
        }
    }

    /// <summary>How long a charging battery takes to fill, from what the aggregate battery reports now.</summary>
    private static TimeSpan? UntilFull()
    {
        try
        {
            var report = global::Windows.Devices.Power.Battery.AggregateBattery.GetReport();
            if (report.ChargeRateInMilliwatts is not { } rate || rate <= 0
                || report.FullChargeCapacityInMilliwattHours is not { } full
                || report.RemainingCapacityInMilliwattHours is not { } now)
            {
                return null;
            }
            return EstimateUntilFull(now, full, rate);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>The time to charge from <paramref name="now"/> to <paramref name="full"/> mWh at <paramref name="rate"/> mW; null when it makes no sense.</summary>
    internal static TimeSpan? EstimateUntilFull(int now, int full, int rate)
    {
        if (rate <= 0 || full <= 0 || now >= full)
        {
            return null;
        }
        var hours = (double)(full - now) / rate;
        return hours is > 0 and < 48 ? TimeSpan.FromHours(hours) : null;
    }

    // Asked once: whether this Windows knows energy saver at all.
    private static bool? saverSupported;

    /// <summary>
    /// Whether energy saver exists on this PC, whether or not it has a battery: Windows 11 24H2
    /// applies it on the wall too. Asked of Windows rather than guessed from a build number — the
    /// power setting can only be subscribed to where the feature is — and asked once.
    /// </summary>
    internal static bool SaverSupported()
    {
        if (saverSupported is { } known)
        {
            return known;
        }
        var answer = false;
        var probe = (Subscription*)NativeMemory.Alloc((nuint)sizeof(Subscription));
        try
        {
            probe->Callback = &SettingChanged;
            // A context no Changes is registered under: a notification arriving first does nothing.
            probe->Context = null;
            var id = GUID.GUID_ENERGY_SAVER_STATUS;
            void* handle = null;
            if (PowerSettingRegisterNotification(&id, DeviceNotifyCallback, (HANDLE)probe, &handle) == 0 && handle != null)
            {
                answer = true;
                // Unregistering waits for any callback to finish, so the block is free to go after.
                _ = PowerSettingUnregisterNotification(handle);
            }
        }
        catch (Exception error) when (error is EntryPointNotFoundException or DllNotFoundException)
        {
            answer = false;
        }
        finally
        {
            NativeMemory.Free(probe);
        }
        saverSupported = answer;
        return answer;
    }

    [UnmanagedCallersOnly]
    private static uint SettingChanged(void* context, uint type, void* setting)
    {
        try
        {
            // Energy saver's own status, kept for every view of it, as the dock's broadcast keeps it.
            if (type == EnergySaver.PbtPowerSettingChange)
            {
                _ = EnergySaver.Shared.Heard((POWERBROADCAST_SETTING*)setting);
            }
            if (Targets.TryGetValue((nint)context, out var changes))
            {
                changes.Add(BatteryNews.Power);
            }
            return 0;
        }
        catch (Exception error)
        {
            Karpo.Core.Troubles.Report("Battery: a power setting changed", error);
            return 0;
        }
    }

    [UnmanagedCallersOnly]
    private static void ModeChanged(int mode, void* context)
    {
        try
        {
            if (Targets.TryGetValue((nint)context, out var changes))
            {
                changes.Add(BatteryNews.Power);
            }
        }
        catch (Exception error)
        {
            Karpo.Core.Troubles.Report("Battery: the power mode changed", error);
        }
    }

    /// <summary>powrprof.h's DEVICE_NOTIFY_SUBSCRIBE_PARAMETERS.</summary>
    private struct Subscription
    {
        public delegate* unmanaged<void*, uint, void*, uint> Callback;
        public void* Context;
    }

    [DllImport("powrprof.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint PowerSettingRegisterNotification(Guid* setting, uint flags, HANDLE recipient, void** registration);

    [DllImport("powrprof.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint PowerSettingUnregisterNotification(void* registration);

    [DllImport("powrprof.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern HRESULT PowerRegisterForEffectivePowerModeNotifications(uint version, delegate* unmanaged<int, void*, void> callback, void* context, void** registration);

    [DllImport("powrprof.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern HRESULT PowerUnregisterFromEffectivePowerModeNotifications(void* registration);

    [DllImport("powrprof.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint PowerGetEffectiveOverlayScheme(Guid* overlay);

    [DllImport("powrprof.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint PowerSetActiveOverlayScheme(Guid* overlay);
}

/// <summary>A made-up battery for pictures and tests; the power mode changes nothing.</summary>
internal sealed class SampleBattery : IBatterySource
{
    private PowerMode mode = PowerMode.Balanced;
    private Changes? changes;

    /// <summary>This PC has a battery; false makes the samples a desktop on the wall.</summary>
    public bool Present { get; init; } = true;

    /// <summary>Whether Windows knows energy saver here (a battery, or 24H2 and later without one).</summary>
    public bool SaverKnown { get; init; } = true;

    public bool Saver { get; init; }

    /// <summary>The power mode this PC's plan offers; null for a plan with none.</summary>
    public PowerMode? Offers { get; init; } = PowerMode.Balanced;

    public void Start(Changes news) => changes = news;

    public BatteryState Read() => Present
        ? new(true, 76, false, false, TimeSpan.FromMinutes(197), null, Saver, Offers is null ? null : mode, SaverKnown)
        : new(false, 0, true, false, null, null, Saver, Offers is null ? null : mode, SaverKnown);

    /// <summary>Windows refuses every mode asked for here, as a plan that will not take one does.</summary>
    public bool Refuses { get; init; }

    /// <summary>Every mode asked for, in turn: what a test reads instead of the PC's own.</summary>
    public List<PowerMode> Asked { get; } = [];

    public void SetMode(PowerMode value)
    {
        Asked.Add(value);
        if (Refuses)
        {
            changes?.Add(BatteryNews.Refused);
            return;
        }
        mode = value;
        changes?.Add(BatteryNews.Power);
    }

    /// <summary>The mode moved to <paramref name="value"/> elsewhere — Windows' own page, a plan — and the source says so, as Windows' notification does.</summary>
    public void Turn(PowerMode value)
    {
        mode = value;
        changes?.Add(BatteryNews.Power);
    }

    /// <summary>Whether anything is following it (<see cref="Start"/>).</summary>
    public bool Followed => changes is not null;

    public void Dispose()
    {
    }
}
