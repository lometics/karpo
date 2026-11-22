// Virtual desktops through the shell's own, undocumented interface,
// IVirtualDesktopManagerInternal, reached through the ImmersiveShell's service
// provider: switching straight to a desktop, making one without going there,
// moving one, removing one and renaming one, none of which the documented
// IVirtualDesktopManager can do.
//
// The interface changes shape between Windows releases, and not always with a
// new IID: 24H2 inserted SwitchDesktopAndMoveForegroundView at slot 10 and kept
// the IID 23H2 (22631.3085) already had, 53F5CA0B. Calling a slot of another
// build's vtable passes the wrong arguments to the wrong method, so this runs
// only on the builds the table below was checked against, 26100 (24H2), 26200
// (25H2), their Insider twins 26120 and 26220, and the Dev build 26300 (its
// registered proxy, ActXPrxy.dll, has the 25 methods below; GetCount,
// GetCurrentDesktop and FindDesktop answer at their slots, read-only, §384),
// and only when QueryService hands out exactly this IID and a found desktop
// answers to 24H2's IVirtualDesktop IID. Anywhere else nothing is called, the
// caller falls back (the keys, for a switch) and the reason is logged once.
//
// SwitchDesktop switches without Explorer's slide. The keys' switch slides, and
// Explorer draws the slide in a window of a band above every app's, Karpo's
// bars included, which are out of sight until it ends (§384).
//
// Sources, both MIT and both matching slot for slot: Ciantic/VirtualDesktopAccessor
// (src/interfaces.rs, rust branch; "tested with 25H2 OS Build 26200.8117") and
// MScholtes/VirtualDesktop (VirtualDesktop11-24H2.cs, version 1.21, 2025-08-11).
//
// IVirtualDesktopManagerInternal, after IUnknown's three:
//    3 GetCount                       13 RemoveDesktop(desktop, fallback)
//    4 MoveViewToDesktop              14 FindDesktop(GUID*, out desktop)
//    5 CanViewMoveDesktops            15 GetDesktopSwitchIncludeExcludeViews
//    6 GetCurrentDesktop              16 SetDesktopName(desktop, HSTRING)
//    7 GetDesktops                    17 SetDesktopWallpaper
//    8 GetAdjacentDesktop             18 UpdateWallpaperPathForAllDesktops
//    9 SwitchDesktop(desktop)         19 CopyDesktopState
//   10 SwitchDesktopAndMoveForegroundView (new in 24H2)
//   11 CreateDesktop                  22 SwitchDesktopWithAnimation
//   12 MoveDesktop(desktop, index)
//
// Compiled into both Karpo and KarpoSettings (renaming a desktop), so it uses
// nothing but TerraFX and the BCL.

using System.Runtime.InteropServices;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

internal static unsafe class DesktopControl
{
    private static readonly Guid ImmersiveShell = new("C2F03A33-21F5-47FA-B4BB-156362A2F239");
    private static readonly Guid ServiceProvider = new("6D5140C1-7436-11CE-8034-00AA006009FA");
    private static readonly Guid ManagerService = new("C5E0CDCA-7B6E-41B2-9FC4-D93975CC467B");
    private static readonly Guid ManagerInterface = new("53F5CA0B-158F-4124-900C-057158060B27");
    private static readonly Guid DesktopInterface = new("3F07F4BE-B107-441A-AF0F-39D82529072C");

    /// <summary>The builds whose vtable is the one above.</summary>
    private static readonly int[] KnownBuilds = [26100, 26120, 26200, 26220, 26300];

    private const int SlotSwitch = 9;
    private const int SlotCreate = 11;
    private const int SlotMove = 12;
    private const int SlotRemove = 13;
    private const int SlotFind = 14;
    private const int SlotRename = 16;

    // What was said about the interface, so the log has it once, not per click.
    private static string? told;

    /// <summary>
    /// Whether the interface can be used here: a known build, and the shell
    /// hands it out. Asks the shell, so not for every frame.
    /// </summary>
    public static bool Available()
    {
        var manager = Open();
        if (manager == null)
        {
            return false;
        }
        Release(manager);
        return true;
    }

    /// <summary>Straight to the desktop, without passing through the ones between.</summary>
    public static bool Switch(Guid id) => Run("SwitchDesktop", id, null, static (manager, desktop, _) =>
        ((delegate* unmanaged[Stdcall]<void*, void*, int>)Table((void*)manager)[SlotSwitch])((void*)manager, (void*)desktop));

    /// <summary>
    /// desktops: a new desktop at the end of Windows' order, which Windows does not switch to, as
    /// Task View's "New desktop" does not; false (and why, in the log) where the call can't be made.
    /// </summary>
    public static bool Create()
    {
        var manager = Open();
        if (manager == null)
        {
            return false;
        }
        void* desktop = null;
        try
        {
            var create = (delegate* unmanaged[Stdcall]<void*, void**, int>)Table(manager)[SlotCreate];
            var result = create(manager, &desktop);
            if (result < 0)
            {
                Log($"CreateDesktop failed 0x{result:X8}"); // not-ui: a log
                return false;
            }
            return true;
        }
        finally
        {
            Release(desktop);
            Release(manager);
        }
    }

    /// <summary>Moves the desktop to <paramref name="index"/> in Windows' order.</summary>
    public static bool Move(Guid id, int index) => index >= 0 && Run("MoveDesktop", id, null, (manager, desktop, _) =>
        ((delegate* unmanaged[Stdcall]<void*, void*, uint, int>)Table((void*)manager)[SlotMove])((void*)manager, (void*)desktop, (uint)index));

    /// <summary>Removes the desktop; its windows go to <paramref name="fallback"/>, as Windows' own close does.</summary>
    public static bool Remove(Guid id, Guid fallback) => id != fallback && Run("RemoveDesktop", id, fallback, static (manager, desktop, other) =>
        ((delegate* unmanaged[Stdcall]<void*, void*, void*, int>)Table((void*)manager)[SlotRemove])((void*)manager, (void*)desktop, (void*)other));

    /// <summary>Gives the desktop a name, which Windows keeps in the registry and shows in Task View.</summary>
    public static bool Rename(Guid id, string name)
    {
        name = name.Trim();
        if (name.Length == 0)
        {
            return false;
        }
        nint text = 0;
        try
        {
            fixed (char* chars = name)
            {
                var made = WindowsCreateString(chars, (uint)name.Length, &text);
                if (made < 0)
                {
                    Log($"WindowsCreateString failed 0x{made:X8}"); // not-ui: a log
                    return false;
                }
            }
            var hstring = text;
            return Run("SetDesktopName", id, null, (manager, desktop, _) =>
                ((delegate* unmanaged[Stdcall]<void*, void*, nint, int>)Table((void*)manager)[SlotRename])((void*)manager, (void*)desktop, hstring));
        }
        finally
        {
            if (text != 0)
            {
                _ = WindowsDeleteString(text);
            }
        }
    }

    /// <summary>Whether the table above may be used on <paramref name="build"/>.</summary>
    internal static bool Known(int build) => Array.IndexOf(KnownBuilds, build) >= 0;

    /// <summary>
    /// Finds the desktop (and the fallback one) and makes the call; false, and
    /// a line in the log, when any step fails.
    /// </summary>
    private static bool Run(string what, Guid id, Guid? fallback, Func<nint, nint, nint, int> call)
    {
        var manager = Open();
        if (manager == null)
        {
            return false;
        }
        void* desktop = null;
        void* other = null;
        try
        {
            desktop = Find(manager, id);
            if (desktop == null)
            {
                Log($"{what}: desktop {id:B} not found");
                return false;
            }
            if (fallback is { } fallbackId)
            {
                other = Find(manager, fallbackId);
                if (other == null)
                {
                    Log($"{what}: fallback desktop {fallbackId:B} not found");
                    return false;
                }
            }
            var result = call((nint)manager, (nint)desktop, (nint)other);
            if (result < 0)
            {
                Log($"{what} failed 0x{result:X8}");
                return false;
            }
            return true;
        }
        finally
        {
            Release(other);
            Release(desktop);
            Release(manager);
        }
    }

    /// <summary>The manager, or null (and why, in the log) where it can't be had safely.</summary>
    private static void* Open()
    {
        var build = Environment.OSVersion.Version.Build;
        if (!Known(build))
        {
            Tell($"not used on build {build}: its vtable was only checked for builds {string.Join(", ", KnownBuilds)}; switching falls back to Ctrl+Win+arrows");
            return null;
        }
        void* provider = null;
        void* manager = null;
        var shell = ImmersiveShell;
        var providerId = ServiceProvider;
        var hr = CoCreateInstance(&shell, null, (uint)CLSCTX.CLSCTX_LOCAL_SERVER, &providerId, &provider);
        if (hr.FAILED || provider == null)
        {
            // Explorer is not running, or restarting.
            Tell($"ImmersiveShell unavailable (0x{hr.Value:X8})"); // not-ui: a log
            return null;
        }
        try
        {
            var service = ManagerService;
            var managerId = ManagerInterface;
            // IServiceProvider::QueryService, slot 3.
            var query = (delegate* unmanaged[Stdcall]<void*, Guid*, Guid*, void**, int>)(*(void***)provider)[3];
            var answer = query(provider, &service, &managerId, &manager);
            if (answer < 0 || manager == null)
            {
                // Another IID on this build: its vtable is unknown, so nothing is called.
                Tell($"IVirtualDesktopManagerInternal {ManagerInterface:B} refused on build {build} (0x{answer:X8})"); // not-ui: a log
                return null;
            }
            Tell($"using IVirtualDesktopManagerInternal {ManagerInterface:B} on build {build}");
            return manager;
        }
        finally
        {
            Release(provider);
        }
    }

    /// <summary>The desktop with <paramref name="id"/>, as an IVirtualDesktop of this build; null when there is none.</summary>
    private static void* Find(void* manager, Guid id)
    {
        void* desktop = null;
        var find = (delegate* unmanaged[Stdcall]<void*, Guid*, void**, int>)Table(manager)[SlotFind];
        if (find(manager, &id, &desktop) < 0 || desktop == null)
        {
            return null;
        }
        // What FindDesktop hands back must be this build's IVirtualDesktop, or
        // the table is not what it seems: then nothing more is called.
        void* checkedDesktop = null;
        var desktopId = DesktopInterface;
        var asked = ((IUnknown*)desktop)->QueryInterface(&desktopId, &checkedDesktop);
        Release(desktop);
        if (asked.FAILED || checkedDesktop == null)
        {
            Log($"a desktop does not answer to IVirtualDesktop {DesktopInterface:B} (0x{asked.Value:X8}); not used");
            return null;
        }
        return checkedDesktop;
    }

    private static void** Table(void* instance) => *(void***)instance;

    private static void Release(void* value)
    {
        if (value != null)
        {
            ((IUnknown*)value)->Release();
        }
    }

    private static void Log(string message) => Console.Error.WriteLine($"Karpo: virtual desktops: {message}");

    /// <summary>Logs a state of the interface once, until it changes.</summary>
    private static void Tell(string message)
    {
        if (told != message)
        {
            told = message;
            Log(message);
        }
    }

    [DllImport("combase.dll", ExactSpelling = true)]
    private static extern int WindowsCreateString(char* sourceString, uint length, nint* hstring);

    [DllImport("combase.dll", ExactSpelling = true)]
    private static extern int WindowsDeleteString(nint hstring);
}
