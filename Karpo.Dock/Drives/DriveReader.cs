// Reading what a drive's line says of the volume beyond its size: its file system and its
// BitLocker state (docs\widgets.md §99, the drive flyout's header words). None of it happens on the
// dock's thread — a network share that has gone away makes every one of these calls wait for the
// network to time out, tens of seconds, and the bar has only the one thread — so the line starts a
// read on a thread of its own (Start) and is told when it is done. What a drive's root holds is
// read the way a folder pin's folder is (FolderReader, on the same kind of thread).

using Karpo.Core;
using Karpo.Dock.Native;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock.Drives;

/// <summary>What a volume says of itself beyond its size: "NTFS", and Windows' word for its BitLocker state; empty where it says nothing.</summary>
internal readonly record struct DriveFacts(string FileSystem, string BitLocker);

internal static unsafe class DriveReader
{
    /// <summary>The shell's name for whether BitLocker guards a volume; Explorer shows the same word.</summary>
    private const string BitLockerProperty = "System.Volume.BitLockerProtection";

    /// <summary>
    /// Runs <paramref name="work"/> on a thread of its own with COM up, so the shell can be asked
    /// there. A read that waits on a dead network share holds that thread and nothing else; the
    /// pool's own threads are left for everyone else, which is why this is not <c>Task.Run</c>.
    /// </summary>
    public static Task Start(Action work) => Task.Factory.StartNew(
        () =>
        {
            var hr = CoInitializeEx(null, (uint)COINIT.COINIT_MULTITHREADED);
            try
            {
                work();
            }
            catch (Exception error)
            {
                // Nobody waits on the task: what the read threw is reported here or nowhere.
                Troubles.Report("Drives: a read on a thread of its own", error);
            }
            finally
            {
                // S_FALSE: already up on this thread, and someone else's to take down.
                if (hr.Value == 0)
                {
                    CoUninitialize();
                }
            }
        },
        CancellationToken.None,
        TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach,
        TaskScheduler.Default);

    /// <summary>
    /// The file system and the BitLocker state of the volume at <paramref name="root"/> ("C:\"),
    /// as the drive flyout's header said them. Waits on a drive that does not answer, so only ever
    /// on a thread of its own; never throws.
    /// </summary>
    public static DriveFacts Facts(string root)
    {
        uint mode;
        // No "insert a disk" box for an empty card reader; a failed read is enough.
        _ = SetThreadErrorMode(SEM_FAILCRITICALERRORS | SEM_NOOPENFILEERRORBOX, &mode);
        try
        {
            var volume = Volume(root);
            return volume.Ok ? new DriveFacts(volume.FileSystem, BitLockerState(root)) : default;
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            return default;
        }
        finally
        {
            _ = SetThreadErrorMode(mode, null);
        }
    }

    /// <summary>The volume's label and file system; <c>Ok</c> false when the volume did not answer.</summary>
    private static (bool Ok, string Label, string FileSystem) Volume(string root)
    {
        var name = stackalloc char[MAX.MAX_PATH + 1];
        var system = stackalloc char[MAX.MAX_PATH + 1];
        fixed (char* path = root)
        {
            if (!GetVolumeInformationW(path, name, MAX.MAX_PATH + 1, null, null, null, system, MAX.MAX_PATH + 1))
            {
                return (false, "", "");
            }
        }
        return (true, name[0] == '\0' ? "" : new string(name), system[0] == '\0' ? "" : new string(system));
    }

    /// <summary>
    /// Windows' own word for the volume's BitLocker state ("On", "Off", "Suspended"…), asked for
    /// through the shell property Explorer reads and formatted by Windows, so nothing here has to
    /// guess what a number means. Empty for a volume that says nothing about it.
    /// </summary>
    private static string BitLockerState(string root)
    {
        PROPERTYKEY key;
        fixed (char* name = BitLockerProperty)
        {
            if (PSGetPropertyKeyFromName(name, &key).FAILED)
            {
                return "";
            }
        }
        var shell = Items.ShellItem(root);
        if (shell == null)
        {
            return "";
        }
        IShellItem2* rich = null;
        PROPVARIANT value = default;
        char* text = null;
        try
        {
            if (shell->QueryInterface(__uuidof<IShellItem2>(), (void**)&rich).FAILED
                || rich->GetProperty(&key, &value).FAILED
                || value.Anonymous.Anonymous.vt == (ushort)VARENUM.VT_EMPTY
                || PSFormatForDisplayAlloc(&key, &value, PROPDESC_FORMAT_FLAGS.PDFF_DEFAULT, &text).FAILED
                || text == null)
            {
                return "";
            }
            var word = new string(text);
            return word.Length == 0 ? "" : $"BitLocker {word}"; // not-ui: a name, and Windows' own word for its state
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            return "";
        }
        finally
        {
            if (text != null)
            {
                CoTaskMemFree(text);
            }
            _ = PropVariantClear(&value);
            ComUtil.Release(ref rich);
            shell->Release();
        }
    }
}
