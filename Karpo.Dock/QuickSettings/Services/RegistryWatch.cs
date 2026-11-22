// A registry key watched for changes, for what Windows keeps there and announces nowhere
// else a desktop app can hear: the radio management service's system radio state, night
// light's state, and the like. RegNotifyChangeKeyValue signals an event, the pool waits on
// it, and the watch is armed again before the change is passed on, so a change made while
// the callback runs is not lost.
//
// Thread-agnostic (REG_NOTIFY_THREAD_AGNOSTIC): the pool thread that arms it may be gone by
// the time the key changes, which without the flag cancels the notification. The same shape
// as the tray's promotions (Tray.Promotions), made once for the switches.
//
// Nothing here reads a value. The callback comes on a pool thread and only says that
// something under the key changed; the owner reads what it needs, where it needs it.

using Microsoft.Win32;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock.QuickSettings.Services;

internal sealed unsafe class RegistryWatch : IDisposable
{
    private readonly RegistryKey key;
    private readonly bool subtree;
    private readonly Action changed;
    private readonly AutoResetEvent signal = new(false);
    private readonly object gate = new();
    private RegisteredWaitHandle? waiting;
    private bool disposed;

    private RegistryWatch(RegistryKey key, bool subtree, Action changed)
    {
        this.key = key;
        this.subtree = subtree;
        this.changed = changed;
    }

    /// <summary>
    /// Starts watching <paramref name="path"/> under <paramref name="hive"/> (the 64-bit view), and
    /// <paramref name="subtree"/> everything under it; <paramref name="changed"/> comes on a pool
    /// thread after each change. Null where the key is not there or cannot be watched: the owner
    /// then follows whatever else it has, and reads again when it is looked at.
    /// </summary>
    public static RegistryWatch? Open(RegistryHive hive, string path, bool subtree, Action changed)
    {
        RegistryKey? key = null;
        try
        {
            using var root = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
            // KEY_READ carries KEY_NOTIFY, which is all a watch needs.
            key = root.OpenSubKey(path, writable: false);
            if (key is null)
            {
                return null;
            }
            var watch = new RegistryWatch(key, subtree, changed);
            if (!watch.Arm())
            {
                watch.Dispose();
                return null;
            }
            return watch;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            key?.Dispose();
            return null;
        }
    }

    /// <summary>Asks for the next change; false where Windows refused the watch.</summary>
    private bool Arm()
    {
        lock (gate)
        {
            if (disposed)
            {
                return false;
            }
            waiting?.Unregister(null);
            waiting = null;
            const uint filter = (uint)(REG.REG_NOTIFY_CHANGE_NAME | REG.REG_NOTIFY_CHANGE_LAST_SET | REG.REG_NOTIFY_THREAD_AGNOSTIC);
            var handle = (HKEY)key.Handle.DangerousGetHandle();
            var wake = (HANDLE)signal.SafeWaitHandle.DangerousGetHandle();
            if (RegNotifyChangeKeyValue(handle, subtree ? BOOL.TRUE : BOOL.FALSE, filter, wake, BOOL.TRUE) != 0)
            {
                return false;
            }
            waiting = ThreadPool.RegisterWaitForSingleObject(
                signal,
                static (state, _) => ((RegistryWatch)state!).Heard(),
                this,
                Timeout.Infinite,
                executeOnlyOnce: true);
            return true;
        }
    }

    /// <summary>On a pool thread: armed again first, then told. Internal for the test that throws from it.</summary>
    internal void Heard()
    {
        // Nothing may escape into the pool's wait: an exception thrown on a pool thread ends the
        // process, and the dock with it. Arming again is inside too.
        try
        {
            if (!Arm())
            {
                return;
            }
            changed();
        }
        catch (Exception error)
        {
            Karpo.Core.Troubles.Note("A registry watch", error);
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed)
            {
                return;
            }
            disposed = true;
            waiting?.Unregister(null);
            waiting = null;
        }
        // Closing the key signals the event, which nothing waits on any more.
        key.Dispose();
        signal.Dispose();
    }
}
