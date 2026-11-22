// Rotation lock, as Windows' own tile has it: whether the PC turns its screen at all, whether it
// may right now, whether it is locked, and switched the way Windows switches it.
//
// What the PC can do is GetAutoRotationState, documented, and read the way Windows' own handler
// for the tile reads it (SettingsHandlers_PCDisplay.dll, SystemSettings_Display_IsRotationLocked):
//
//   turns its screen   no AR_NOSENSOR and no AR_NOT_SUPPORTED            (state & 0x30) == 0
//   may be switched    and none of remote session, several screens,
//                      docked or used as a laptop                       (state & 0xCC) == 0
//   locked             AR_DISABLED, or any of those four                (state & 0xCD) != 0
//
// A PC whose screen does not turn has no tile in Windows (Offered); one that turns but may not be
// switched now — a convertible folded as a laptop, a second screen plugged in — shows the tile
// greyed. The widget is greyed on both, where somebody placed it (docs\widgets.md §247).
//
// The switch is SetAutoRotation, which user32 exports by ordinal only (2507): Windows' own handler
// and twinui import it that way, Wine's user32.spec lists it by that number, and it is a jump to
// win32u's NtUserSetAutoRotation. TRUE lets the screen turn, FALSE locks it. It is resolved with
// GetProcAddress by ordinal, and only where the ordinal is there.
//
// Windows keeps the state under HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\AutoRotation and reads
// it again when that key changes; so does this (RegistryWatch), and a user may watch the key though
// only the system may write it.

using Karpo.Core;
using Microsoft.Win32;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock.QuickSettings.Services;

/// <summary>What GetAutoRotationState's answer means, as Windows' own handler reads it (see the top of this file). Pure.</summary>
internal static class RotationState
{
    /// <summary>No sensor, or not supported: the screen does not turn, and Windows has no tile.</summary>
    private const uint Cannot = 0x30;

    /// <summary>A remote session, several screens, docked, or used as a laptop: it may not be switched now.</summary>
    private const uint NotNow = 0xCC;

    /// <summary>The person locked it.</summary>
    private const uint Disabled = 0x1;

    /// <summary>Whether the PC turns its screen at all: <paramref name="state"/> null is a PC that did not say.</summary>
    internal static bool Turns(uint? state) => state is { } now && (now & Cannot) == 0;

    /// <summary>Whether it may be switched now.</summary>
    internal static bool Switchable(uint? state) => Turns(state) && (state!.Value & NotNow) == 0;

    /// <summary>Whether the screen is kept from turning; null where it does not turn at all.</summary>
    internal static bool? Locked(uint? state) => Turns(state) ? (state!.Value & (NotNow | Disabled)) != 0 : null;
}

/// <summary>
/// Rotation lock (see the top of this file). Its state is read every time it is asked — one user32
/// call, as cheap as the power status energy saver reads — and redrawn when Windows' key changes.
/// </summary>
internal sealed unsafe class RotationSwitch : IQuickSwitch
{
    private const string Key = @"SOFTWARE\Microsoft\Windows\CurrentVersion\AutoRotation";

    private readonly Func<uint?> read;
    private readonly Func<bool, bool> set;
    private readonly Func<Action, IDisposable?> watch;
    private readonly object gate = new();
    private IDisposable? following;
    private Action? changed;
    private volatile string? note;
    private bool disposed;

    public RotationSwitch()
        : this(Read, Set, static heard => RegistryWatch.Open(RegistryHive.LocalMachine, Key, subtree: false, heard))
    {
    }

    /// <param name="read">GetAutoRotationState's answer, or null where it gave none.</param>
    /// <param name="set">SetAutoRotation: true lets the screen turn; false where it could not be asked.</param>
    /// <param name="watch">Follows Windows' key; called on the pool.</param>
    internal RotationSwitch(Func<uint?> read, Func<bool, bool> set, Func<Action, IDisposable?> watch)
    {
        this.read = read;
        this.set = set;
        this.watch = watch;
    }

    public string Name => S.DockQuickRotationName;

    /// <summary>Only where the screen turns at all: elsewhere Windows has no tile (the smoke pass says which).</summary>
    public bool Offered => RotationState.Turns(read());

    public bool Available => RotationState.Switchable(read());

    /// <summary>A click switches the lock wherever it may be switched now.</summary>
    public bool Switches => Available;

    public bool? On => RotationState.Locked(read());

    public string? Note
    {
        get
        {
            if (changed is null)
            {
                // Not looked at yet: the plain line, without asking Windows anything (IQuickSwitch.Start).
                return S.DockQuickRotationNote;
            }
            var state = read();
            return note ?? (!RotationState.Turns(state)
                ? S.DockQuickRotationNone
                : RotationState.Switchable(state)
                    ? S.DockQuickRotationNote
                    : S.DockQuickRotationTablet);
        }
    }

    public void Start(Action onChanged)
    {
        changed = onChanged;
        // On the pool: the watch is a registry key, and a first look asks nothing on the dock's
        // thread (IQuickSwitch.Start).
        _ = Task.Run(() => Troubles.Guard("Rotation lock: following it", () =>
        {
            var made = watch(() => changed?.Invoke());
            lock (gate)
            {
                if (disposed)
                {
                    made?.Dispose();
                    return;
                }
                following = made;
            }
        }));
    }

    public void Act()
    {
        if (!Available)
        {
            return;
        }
        // Locked, it is let turn; turning, it is locked.
        var done = set(On == true);
        note = done ? null : S.DockQuickRotationRefused;
        if (!done)
        {
            refusals++;
        }
        changed?.Invoke();
    }

    private int refusals;

    public int Refusals => refusals;

    public void Dispose()
    {
        lock (gate)
        {
            disposed = true;
            following?.Dispose();
            following = null;
        }
    }

    /// <summary>
    /// edit: whether this PC's screen turns at all, read now: the add list greys rotation lock where
    /// it does not, and says so in the row (Catalogue.WhyNotHere).
    /// </summary>
    internal static bool ScreenTurns => RotationState.Turns(Read());

    /// <summary>GetAutoRotationState, or null where it does not answer.</summary>
    private static uint? Read()
    {
        AR_STATE state = default;
        return GetAutoRotationState(&state) ? (uint)state : null;
    }

    /// <summary>SetAutoRotation by its ordinal; false where user32 has no such export.</summary>
    private static bool Set(bool turn)
    {
        HMODULE user32;
        fixed (char* name = "user32.dll")
        {
            user32 = GetModuleHandleW(name);
        }
        if (user32 == HMODULE.NULL)
        {
            return false;
        }
        var export = GetProcAddress(user32, (sbyte*)2507);
        if (export == null)
        {
            return false;
        }
        // Its answer is not a result: Windows' own handler ignores it too.
        _ = ((delegate* unmanaged[Stdcall]<int, int>)(void*)export)(turn ? 1 : 0);
        return true;
    }
}
