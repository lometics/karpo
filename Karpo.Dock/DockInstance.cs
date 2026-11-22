// The one dock of a session: the mutex the running dock holds (Taskbar.Instance), which is how a second
// start and the guard learn that one is up. Program.Main takes it as the dock starts; the guard asks
// whether anyone holds it once its own dock has ended (DockGuard.NewerDock).
//
// A stage (Stage.cs: a recording's, a real-dock test's) takes none (§393). It took it, only not minding
// that it was held, so a stage running as the person's dock quit kept the mutex alive: the dock's guard
// read that as "a newer dock has taken over" and left Windows' taskbar hidden to nobody, and the logon
// task's start seconds later found it held and ended without a word, as an automatic second start does
// (2026-10-29, 11:13). Only a dock that is the person's holds it.

using Karpo.Dock.Native;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
using ERROR = TerraFX.Interop.Windows.ERROR;

namespace Karpo.Dock;

/// <summary>What a start found: the instance its own (<paramref name="Mine"/>), its handle held for as long as the dock runs; else another dock's.</summary>
internal readonly record struct InstanceTaken(bool Mine, HANDLE Handle);

internal static unsafe class DockInstance
{
    /// <summary>
    /// The instance <paramref name="name"/> taken for a dock starting: its own where nobody holds it, the
    /// handle open for as long as the process runs; another dock's where one does. A <paramref name="stage"/>
    /// runs beside whatever dock there is and takes nothing.
    /// </summary>
    public static InstanceTaken Take(bool stage, string name = Taskbar.Instance)
    {
        if (stage)
        {
            return new(true, HANDLE.NULL);
        }
        HANDLE instance;
        fixed (char* named = name)
        {
            instance = CreateMutexW(null, BOOL.FALSE, named);
        }
        var created = GetLastError();
        if (instance == HANDLE.NULL)
        {
            throw new NativeException($"CreateMutexW failed: error {created}", HRESULT_FROM_WIN32(created).Value);
        }
        if (created == ERROR.ERROR_ALREADY_EXISTS)
        {
            CloseHandle(instance);
            return new(false, HANDLE.NULL);
        }
        return new(true, instance);
    }

    /// <summary>Whether a dock holds the instance <paramref name="name"/>.</summary>
    public static bool Held(string name = Taskbar.Instance)
    {
        fixed (char* named = name)
        {
            var held = OpenMutexW((uint)SYNCHRONIZE, BOOL.FALSE, named);
            if (held == HANDLE.NULL)
            {
                return false;
            }
            CloseHandle(held);
            return true;
        }
    }
}
