// Energy saver as Windows 11 24H2 and later say it (Karpo.Dock\QuickSettings\Services\EnergySaver.cs).
//
// Turned on by hand, energy saver is in its standard mode, which the power status's SystemStatusFlag
// does not show: every view of energy saver said "off" while Windows' taskbar showed it on, and none
// was told to look again. Energy saver's own status (GUID_ENERGY_SAVER_STATUS) is what they read now,
// heard by the dock's window; these drive it with a fake status, never the PC's own energy saver.

using System.Runtime.InteropServices;
using Karpo.Dock.QuickSettings.Services;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Tests.QuickSettings;

public sealed unsafe class EnergySaverTests
{
    [Fact]
    public void EnergySaversOwnStatusSaysWhetherItIsOn()
    {
        var saver = new EnergySaver();
        Assert.Null(saver.On);
        // Standard is what turning it on by hand gives: on.
        Assert.True(saver.Heard(EnergySaver.Standard));
        Assert.True(saver.On);
        // High savings is on too, and no change from standard.
        Assert.False(saver.Heard(EnergySaver.HighSavings));
        Assert.True(saver.On);
        Assert.True(saver.Heard(EnergySaver.Off));
        Assert.False(saver.On);
        Assert.False(saver.Heard(EnergySaver.Off));
        // A value past the ones Windows has is still "on", not a crash.
        Assert.True(saver.Heard(7));
        Assert.True(saver.On);
    }

    [Theory]
    [InlineData(false, null, false)]
    [InlineData(true, null, true)]
    // The bug: on by hand, the flag says 0 and energy saver's status says standard.
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void ItsStatusWinsOverThePowerStatusFlag(bool flag, bool? status, bool saving) =>
        Assert.Equal(saving, EnergySaver.Saving(flag, status));

    [Fact]
    public void APowerSettingNotificationIsReadOnlyForEnergySaver()
    {
        Assert.Null(EnergySaver.Read(null));
        Assert.Equal(1u, EnergySaver.Read(Setting(GUID.GUID_ENERGY_SAVER_STATUS, 1)));
        Assert.Equal(2u, EnergySaver.Read(Setting(GUID.GUID_ENERGY_SAVER_STATUS, 2)));
        // Battery saver's old notification, and the power source: not energy saver's.
        Assert.Null(EnergySaver.Read(Setting(GUID.GUID_POWER_SAVING_STATUS, 1)));
        Assert.Null(EnergySaver.Read(Setting(GUID.GUID_ACDC_POWER_SOURCE, 1)));
        // Data too short to hold a status.
        Assert.Null(EnergySaver.Read(Setting(GUID.GUID_ENERGY_SAVER_STATUS, 1, length: 2)));

        var saver = new EnergySaver();
        Assert.True(saver.Heard(Setting(GUID.GUID_ENERGY_SAVER_STATUS, 1)));
        Assert.False(saver.Heard(Setting(GUID.GUID_POWER_SAVING_STATUS, 0)));
        Assert.True(saver.On);
    }

    /// <summary>
    /// The switch (bar, card, list line and the battery's menu) over a fake energy saver: off while it
    /// is off, on once it says standard — with the power status's flag still 0, as Windows leaves it.
    /// </summary>
    [Fact]
    public void TheSwitchFollowsEnergySaversStatus()
    {
        var saver = new EnergySaver();
        using var action = new EnergySaverSwitch(() => null, () => new PowerNow(true, true, EnergySaver.Saving(false, saver.On)), () => true);
        Assert.False(action.On);
        saver.Heard(EnergySaver.Standard);
        Assert.True(action.On);
        saver.Heard(EnergySaver.Off);
        Assert.False(action.On);
    }

    /// <summary>
    /// What the dock's window hears when energy saver changes (WM_POWERBROADCAST, PBT_POWERSETTINGCHANGE)
    /// reaches the switch and has it drawn again; so does the old power-status change.
    /// </summary>
    [Fact]
    public void ThePowerSettingBroadcastIsHeard()
    {
        using var action = new EnergySaverSwitch(() => null, () => new PowerNow(true, true, false), () => true);
        var told = 0;
        action.Start(() => Interlocked.Increment(ref told));
        Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref told) > 0, TimeSpan.FromSeconds(10)));
        var before = Volatile.Read(ref told);
        action.Heard(WM.WM_POWERBROADCAST, EnergySaver.PbtPowerSettingChange);
        Assert.Equal(before + 1, Volatile.Read(ref told));
        action.Heard(WM.WM_POWERBROADCAST, 0x000A);
        Assert.Equal(before + 2, Volatile.Read(ref told));
    }

    /// <summary>
    /// Energy saver switched while a menu of Karpo's is open (the dock busy in its modal loop): the
    /// broadcast is kept for afterwards, as a setting change is, so the battery, its status icon and
    /// the switch still draw it; before, it went by and they said the old state until something else
    /// drew them. What it pointed at is gone by then, so nothing of its lParam is kept.
    /// </summary>
    [Fact]
    public void APowerBroadcastDuringAMenuIsKeptForAfterwards()
    {
        Assert.True(Karpo.Dock.Dock.Deferrable(WM.WM_POWERBROADCAST, EnergySaver.PbtPowerSettingChange));
        Assert.True(Karpo.Dock.Dock.Deferrable(WM.WM_POWERBROADCAST, 0x000A));
        Assert.Equal((LPARAM)0, Karpo.Dock.Dock.Kept(WM.WM_POWERBROADCAST, 0x1234));
        Assert.Equal((LPARAM)0x1234, Karpo.Dock.Dock.Kept(WM.WM_TIMER, 0x1234));
    }

    /// <summary>
    /// On this Windows, read-only: a window that follows energy saver is told its status at once, so
    /// the views read the right one from the dock's start, before it is ever switched. Skipped where
    /// Windows has no energy saver status to give (before 24H2).
    /// </summary>
    [Fact]
    public void AWindowThatFollowsIsToldTheStatusAtOnce()
    {
        uint? heard = null;
        var followed = true;
        var thread = new Thread(() =>
        {
            var instance = (HINSTANCE)(void*)GetModuleHandleW(null);
            fixed (char* name = "karpo.tests.energysaver")
            {
                var type = new WNDCLASSW { lpfnWndProc = &Procedure, hInstance = instance, lpszClassName = name };
                _ = RegisterClassW(&type);
                var window = CreateWindowExW(0, name, null, 0, 0, 0, 0, 0, HWND.HWND_MESSAGE, HMENU.NULL, instance, null);
                Assert.True(window != HWND.NULL);
                last = null;
                var registration = EnergySaver.Follow(window);
                if (registration is not { } following)
                {
                    followed = false;
                    DestroyWindow(window);
                    return;
                }
                var until = Environment.TickCount64 + 3000;
                MSG msg;
                while (last is null && Environment.TickCount64 < until)
                {
                    while (PeekMessageW(&msg, HWND.NULL, 0, 0, PM.PM_REMOVE))
                    {
                        DispatchMessageW(&msg);
                    }
                    Thread.Sleep(10);
                }
                heard = last;
                EnergySaver.Unfollow(following);
                DestroyWindow(window);
            }
        });
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)));
        if (!followed)
        {
            return;
        }
        Assert.NotNull(heard);
        Assert.True(heard <= EnergySaver.HighSavings);
    }

    [ThreadStatic]
    private static uint? last;

    [UnmanagedCallersOnly]
    private static LRESULT Procedure(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        if (msg == WM.WM_POWERBROADCAST && (uint)(nuint)wParam == EnergySaver.PbtPowerSettingChange)
        {
            last = EnergySaver.Read((POWERBROADCAST_SETTING*)(nint)lParam) ?? last;
            return 1;
        }
        return DefWindowProcW(hwnd, msg, wParam, lParam);
    }

    /// <summary>A power setting notification for <paramref name="id"/> carrying <paramref name="value"/>, kept for the test's life.</summary>
    private static POWERBROADCAST_SETTING* Setting(Guid id, uint value, uint length = sizeof(uint))
    {
        var setting = (POWERBROADCAST_SETTING*)NativeMemory.AllocZeroed((nuint)(sizeof(POWERBROADCAST_SETTING) + 8));
        setting->PowerSetting = id;
        setting->DataLength = length;
        *(uint*)&setting->Data = value;
        return setting;
    }
}
