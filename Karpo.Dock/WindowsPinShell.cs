// Taking a pin off Windows' taskbar (backlog 2), the one way Windows supports: its own "Unpin
// from taskbar" (the canonical verb "taskbarunpin"), asked of the item's context menu as File
// Explorer and Start ask it. Deleting the shortcut in User Pinned\TaskBar does not tell Explorer,
// and there is no API for it.
//
// The verb is offered on the app as Start knows it — its entry in the Apps folder, by its app id
// — and, for a desktop app, on its program too; Windows does not offer it on the shortcut in the
// pins' folder itself. So each of those is asked in turn, and the first that offers the verb is
// told to run it. Nothing else of the menu is ever run: a menu that doesn't offer the verb is
// closed again untouched.
//
// Each of them must be the pin asked for and nothing wider. The program is asked only where the
// shortcut is the program's own app (no app id, or the one Windows gives the program itself,
// WindowsPinSync.IsDefaultId): a web app's shortcut starts its browser, and the browser's "Unpin"
// would take the browser off instead. Where none of the exact ones offers the verb, nothing is
// unpinned, and the log says so.
//
// Explorer's taskbar may be hidden while Karpo runs (BarKeeper); its pins are still its
// own, and the verb works on them hidden or not.
//
// Building a context menu loads shell extensions and can take a second, so it is never done on
// the dock's thread: unpins are queued to a thread of their own (an STA, as the shell wants),
// one after another.

using Karpo.Core;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

internal sealed unsafe class WindowsPinShell : IWindowsPinShell
{
    /// <summary>Windows' canonical name for "Unpin from taskbar".</summary>
    internal const string Verb = "taskbarunpin";

    private const string AppsFolder = @"shell:AppsFolder\";

    // shobjidl_core.h / shellapi.h: what QueryContextMenu, GetCommandString and InvokeCommand take.
    private const uint CMF_NORMAL = 0x0;
    private const uint GCS_VERBW = 0x4;
    private const uint CMIC_MASK_FLAG_NO_UI = 0x400;
    private const uint CMIC_MASK_UNICODE = 0x4000;
    private const uint FirstId = 1;
    private const uint LastId = 0x7FFF;

    public static WindowsPinShell This { get; } = new();

    private readonly System.Collections.Concurrent.BlockingCollection<string> queue = [];
    private Thread? worker;

    /// <summary>Queues <paramref name="target"/> to be taken off Windows' taskbar on the shell's thread; true once it is queued.</summary>
    public bool Unpin(string target)
    {
        lock (queue)
        {
            if (worker is null)
            {
                worker = new Thread(Work) { IsBackground = true, Name = "Karpo: Windows' pins" }; // not-ui: a thread's name
                worker.SetApartmentState(ApartmentState.STA);
                worker.Start();
            }
        }
        queue.Add(target);
        return true;
    }

    private void Work()
    {
        foreach (var target in queue.GetConsumingEnumerable())
        {
            try
            {
                if (!UnpinNow(target))
                {
                    Troubles.Note("Windows' taskbar pins", new InvalidOperationException($"Windows offers no \"Unpin from taskbar\" for {target}"));
                }
            }
            catch (Exception error)
            {
                Troubles.Note("Windows' taskbar pins", error);
            }
        }
    }

    /// <summary>Takes <paramref name="target"/> off Windows' taskbar now, on this thread; true when the shell ran the verb.</summary>
    internal static bool UnpinNow(string target)
    {
        foreach (var candidate in Candidates(target))
        {
            if (Offer(candidate, run: true))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Which of the items asked for <paramref name="target"/> offers "Unpin from taskbar", without
    /// running it; null when none does. For looking without touching anything.
    /// </summary>
    internal static string? Offering(string target) =>
        Candidates(target).FirstOrDefault(candidate => Offer(candidate, run: false));

    /// <summary>The items to ask for <paramref name="target"/>, as the shell resolves it (<see cref="Candidates(string, string?, string?)"/>).</summary>
    internal static IEnumerable<string> Candidates(string target) =>
        Candidates(target, PinItems.For(new PinOptions { Target = target }).Aumid,
            Items.IsShortcut(target) ? PinItems.Program(target) : null);

    /// <summary>
    /// The items to ask, in order: the app in the Apps folder by its app id <paramref name="aumid"/>;
    /// the pin's target as it is; and, for a shortcut, the <paramref name="program"/> it starts —
    /// only where the shortcut is that program's own app (it has no app id, or the program's own,
    /// <see cref="WindowsPinSync.IsDefaultId"/>), never a web app or another app id of its program.
    /// </summary>
    internal static IEnumerable<string> Candidates(string target, string? aumid, string? program)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (aumid is { Length: > 0 } id && seen.Add(AppsFolder + id))
        {
            yield return AppsFolder + id;
        }
        if (seen.Add(target))
        {
            yield return target;
        }
        if (program is { Length: > 0 }
            && (aumid is not { Length: > 0 } || WindowsPinSync.IsDefaultId(aumid, program))
            && seen.Add(program))
        {
            yield return program;
        }
    }

    /// <summary>
    /// Whether <paramref name="candidate"/>'s context menu offers <see cref="Verb"/>; with
    /// <paramref name="run"/>, whether it then ran.
    /// </summary>
    private static bool Offer(string candidate, bool run)
    {
        var item = Items.ShellItem(candidate);
        if (item == null)
        {
            return false;
        }
        IContextMenu* menu = null;
        var popup = HMENU.NULL;
        try
        {
            var handler = BHID.BHID_SFUIObject;
            if (item->BindToHandler(null, &handler, __uuidof<IContextMenu>(), (void**)&menu).FAILED || menu == null)
            {
                return false;
            }
            popup = CreatePopupMenu();
            if (popup == HMENU.NULL || menu->QueryContextMenu(popup, 0, FirstId, LastId, CMF_NORMAL).FAILED)
            {
                return false;
            }
            var count = GetMenuItemCount(popup);
            var name = stackalloc char[64];
            for (var index = 0; index < count; index++)
            {
                var id = GetMenuItemID(popup, index);
                // A submenu answers -1, and an id outside the range is not this menu's.
                if (id is < FirstId or > LastId)
                {
                    continue;
                }
                name[0] = '\0';
                if (menu->GetCommandString(id - FirstId, GCS_VERBW, null, (sbyte*)name, 64).FAILED
                    || !string.Equals(new string(name), Verb, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                if (!run)
                {
                    return true;
                }
                var info = new CMINVOKECOMMANDINFOEX
                {
                    cbSize = (uint)sizeof(CMINVOKECOMMANDINFOEX),
                    fMask = CMIC_MASK_UNICODE | CMIC_MASK_FLAG_NO_UI,
                    // The command by its offset, as MAKEINTRESOURCE does.
                    lpVerb = (sbyte*)(nuint)(id - FirstId),
                    lpVerbW = (char*)(nuint)(id - FirstId),
                    nShow = SW.SW_SHOWNORMAL,
                };
                return menu->InvokeCommand((CMINVOKECOMMANDINFO*)&info).SUCCEEDED;
            }
            return false;
        }
        finally
        {
            if (popup != HMENU.NULL)
            {
                _ = DestroyMenu(popup);
            }
            if (menu != null)
            {
                menu->Release();
            }
            item->Release();
        }
    }
}
