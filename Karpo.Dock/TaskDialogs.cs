// Windows' own question before a delete loses widgets (FolderDeletes): a task dialog, comctl32 v6's
// (the dock's manifest asks for it), in Windows' own look — the warning icon, the question, what is
// lost, "Delete N widgets" and "Cancel", Cancel the default as Windows asks of a destructive
// question, Escape and the close button Cancel too.
//
// It is modal to the window it is asked over (a bar, or an open folder): that window takes no input
// until it is answered. Owned by a topmost window it is topmost itself, and it is put at the top of
// that band and given the foreground as it shows, so it stands above the bar, the edit dim and an
// open folder. Edit mode's Escape lets it have the key (Dock.EscapeIsTheirs: it is the dock's own
// window with the keys).
//
// TaskDialogIndirect is looked up in the comctl32 the activation context gives (v6 only has it);
// where there is none, a plain warning message box asks the same, Cancel the default.
//
// Behind IDeleteAsk, so a test answers instead of a dialog showing. §339: the same dialog asks before an
// import (Confirm, the information icon, what changes and the details behind "See details") and says what
// went wrong with an export or an import (Tell), for ⚙ › Back up and reset (IBackupDialogs).

using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

/// <summary>Asks whether a delete that loses widgets goes ahead.</summary>
internal interface IDeleteAsk
{
    /// <summary>True where <paramref name="question"/> was answered with its delete button.</summary>
    bool Ask(HWND owner, Karpo.Core.DeleteQuestion question);
}

/// <summary>The task dialog (TaskDialogs.cs).</summary>
internal sealed unsafe class TaskDialogs : IDeleteAsk
{
    /// <summary>The delete button's id; Cancel's is IDCANCEL, which Escape and the close button give too.</summary>
    internal const int DeleteId = 100;

    /// <summary>TD_WARNING_ICON: MAKEINTRESOURCEW(-1).</summary>
    private static char* WarningIcon => (char*)(nuint)0xFFFF;

    public bool Ask(HWND owner, Karpo.Core.DeleteQuestion question)
    {
        fixed (char* caption = "Karpo")
        fixed (char* title = question.Title)
        fixed (char* content = question.Content)
        fixed (char* delete = question.Delete)
        fixed (char* cancel = question.Cancel)
        {
            var buttons = stackalloc TASKDIALOG_BUTTON[2];
            buttons[0] = new TASKDIALOG_BUTTON { nButtonID = DeleteId, pszButtonText = delete };
            buttons[1] = new TASKDIALOG_BUTTON { nButtonID = IDCANCEL, pszButtonText = cancel };
            var config = new TASKDIALOGCONFIG
            {
                cbSize = (uint)sizeof(TASKDIALOGCONFIG),
                hwndParent = IsWindow(owner) ? owner : HWND.NULL,
                dwFlags = (int)(TASKDIALOG_FLAGS.TDF_ALLOW_DIALOG_CANCELLATION | TASKDIALOG_FLAGS.TDF_SIZE_TO_CONTENT),
                pszWindowTitle = caption,
                pszMainInstruction = title,
                pszContent = content,
                cButtons = 2,
                pButtons = buttons,
                nDefaultButton = IDCANCEL,
                pfCallback = &Callback,
            };
            config.pszMainIcon = WarningIcon;
            int pressed = 0;
            // What the frame asked of Karpo's windows goes as the dialog's own loop begins (Surfaces).
            Surfaces.Present();
            if (Resolve() is var show && show is not null)
            {
                return show(&config, &pressed, null, null).SUCCEEDED && pressed == DeleteId;
            }
            // No comctl32 v6 (a host without the dock's manifest): Windows' plain message box, Cancel the default.
            fixed (char* text = question.Title + "\n\n" + question.Content)
            {
                return MessageBoxW(config.hwndParent, text, caption, MB.MB_OKCANCEL | MB.MB_ICONWARNING | MB.MB_DEFBUTTON2 | MB.MB_TOPMOST) == IDOK;
            }
        }
    }

    /// <summary>TD_INFORMATION_ICON: MAKEINTRESOURCEW(-3).</summary>
    private static char* InformationIcon => (char*)(nuint)0xFFFD;

    /// <summary>TD_ERROR_ICON: MAKEINTRESOURCEW(-2).</summary>
    private static char* ErrorIcon => (char*)(nuint)0xFFFE;

    /// <summary>
    /// §339: a question that changes something but loses nothing — an import, the settings there were kept
    /// beside it —, in the same dialog: the information icon, <paramref name="title"/>, <paramref name="content"/>,
    /// the rest in <paramref name="details"/> behind Windows' own "See details" (none where empty),
    /// <paramref name="confirm"/> and <paramref name="cancel"/>, Cancel the default. True where confirmed.
    /// </summary>
    public static bool Confirm(HWND owner, string title, string content, string details, string confirm, string cancel)
    {
        fixed (char* caption = "Karpo")
        fixed (char* heading = title)
        fixed (char* text = content)
        fixed (char* more = details)
        fixed (char* yes = confirm)
        fixed (char* no = cancel)
        {
            var buttons = stackalloc TASKDIALOG_BUTTON[2];
            buttons[0] = new TASKDIALOG_BUTTON { nButtonID = DeleteId, pszButtonText = yes };
            buttons[1] = new TASKDIALOG_BUTTON { nButtonID = IDCANCEL, pszButtonText = no };
            var config = new TASKDIALOGCONFIG
            {
                cbSize = (uint)sizeof(TASKDIALOGCONFIG),
                hwndParent = IsWindow(owner) ? owner : HWND.NULL,
                dwFlags = (int)(TASKDIALOG_FLAGS.TDF_ALLOW_DIALOG_CANCELLATION | TASKDIALOG_FLAGS.TDF_SIZE_TO_CONTENT),
                pszWindowTitle = caption,
                pszMainInstruction = heading,
                pszContent = text,
                pszExpandedInformation = details.Length > 0 ? more : null,
                cButtons = 2,
                pButtons = buttons,
                nDefaultButton = IDCANCEL,
                pfCallback = &Callback,
            };
            config.pszMainIcon = InformationIcon;
            int pressed = 0;
            // What the frame asked of Karpo's windows goes as the dialog's own loop begins (Surfaces).
            Surfaces.Present();
            if (Resolve() is var show && show is not null)
            {
                return show(&config, &pressed, null, null).SUCCEEDED && pressed == DeleteId;
            }
            fixed (char* plain = string.Join("\n\n", new[] { title, content, details }.Where(part => part.Length > 0)))
            {
                return MessageBoxW(config.hwndParent, plain, caption, MB.MB_OKCANCEL | MB.MB_ICONINFORMATION | MB.MB_DEFBUTTON2 | MB.MB_TOPMOST) == IDOK;
            }
        }
    }

    /// <summary>§339: what went wrong — an export or an import that could not be done —, said in the same dialog with the error icon and OK.</summary>
    public static void Tell(HWND owner, string title, string message)
    {
        fixed (char* caption = "Karpo")
        fixed (char* heading = title)
        fixed (char* text = message)
        {
            var config = new TASKDIALOGCONFIG
            {
                cbSize = (uint)sizeof(TASKDIALOGCONFIG),
                hwndParent = IsWindow(owner) ? owner : HWND.NULL,
                dwFlags = (int)(TASKDIALOG_FLAGS.TDF_ALLOW_DIALOG_CANCELLATION | TASKDIALOG_FLAGS.TDF_SIZE_TO_CONTENT),
                dwCommonButtons = (int)TASKDIALOG_COMMON_BUTTON_FLAGS.TDCBF_OK_BUTTON,
                pszWindowTitle = caption,
                pszMainInstruction = heading,
                pszContent = text,
                pfCallback = &Callback,
            };
            config.pszMainIcon = ErrorIcon;
            int pressed = 0;
            // What the frame asked of Karpo's windows goes as the dialog's own loop begins (Surfaces).
            Surfaces.Present();
            if (Resolve() is var show && show is not null)
            {
                _ = show(&config, &pressed, null, null);
                return;
            }
            fixed (char* plain = title + "\n\n" + message)
            {
                _ = MessageBoxW(config.hwndParent, plain, caption, MB.MB_OK | MB.MB_ICONERROR | MB.MB_TOPMOST);
            }
        }
    }

    /// <summary>
    /// TaskDialogIndirect of the comctl32 the activation context gives — v6 under the dock's manifest
    /// — looked up rather than bound, so the first binding never pins comctl32 v5. Null where it has none.
    /// </summary>
    private static delegate* unmanaged<TASKDIALOGCONFIG*, int*, int*, BOOL*, HRESULT> Resolve()
    {
        HMODULE module;
        fixed (char* name = "comctl32.dll")
        {
            module = LoadLibraryW(name);
        }
        if (module == HMODULE.NULL)
        {
            return null;
        }
        fixed (byte* entry = "TaskDialogIndirect"u8)
        {
            return (delegate* unmanaged<TASKDIALOGCONFIG*, int*, int*, BOOL*, HRESULT>)GetProcAddress(module, (sbyte*)entry);
        }
    }

    /// <summary>As it shows: at the top of the topmost band, with the foreground, over the bar that owns it.</summary>
    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    private static HRESULT Callback(HWND hwnd, uint notification, WPARAM wParam, LPARAM lParam, nint data)
    {
        if (notification == (uint)TASKDIALOG_NOTIFICATIONS.TDN_CREATED)
        {
            Surfaces.Topmost(hwnd, activate: true);
            _ = SetForegroundWindow(hwnd);
        }
        return TerraFX.Interop.Windows.S.S_OK;
    }
}
