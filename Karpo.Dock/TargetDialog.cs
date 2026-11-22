// Windows' own open dialog, for what a pin or a shortcut opens: a file or a folder, in the one
// dialog (TargetPicks has the rules). Open takes the file chosen; a "Select folder" button beside
// it, added through IFileDialogCustomize, takes the folder selected in the list or, with none
// selected, the folder the dialog is in. The button is grey where there is no folder on disk to
// take (This PC, Quick access, a library).
//
// Why a button and not Open itself: FOS_PICKFOLDERS hides the files, and the dialog never asks
// about Open on a folder (OnFileOk is for files; OnFolderChanging can't tell Open from the double
// click that must go into the folder). The button is what many Windows apps add for the same.
//
// The events object is built by hand, as DropTarget is, so it needs no marshalling and stays
// AOT-safe: a native block holding two vtable pointers, IFileDialogEvents first and
// IFileDialogControlEvents second (the dialog asks the first for the second), and a handle to the
// managed state. Shown modally on the caller's thread, which must be an STA (the dock's and the
// settings app's UI threads are).
//
// Compiled into both Karpo and KarpoSettings, so it uses nothing but TerraFX, the BCL and Karpo.Core.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Karpo.Core;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
using static TerraFX.Interop.Windows.S;

namespace Karpo.Dock;

public static unsafe class TargetDialog
{
    /// <summary>The id of the "Select folder" button among the dialog's own controls.</summary>
    private const uint FolderButtonId = 1;

    /// <summary>
    /// Shows the dialog over <paramref name="owner"/> (a window handle), starting near <paramref name="near"/> (see
    /// <see cref="TargetPicks.StartIn"/>); the path of the file or folder picked, or null when it
    /// was cancelled or couldn't be shown. §425: <paramref name="browses"/> a file alone (no "Select folder"), or a
    /// folder alone (Windows' own folder dialog, FOS_PICKFOLDERS), for a pin's File and Folder.
    /// </summary>
    public static string? Pick(nint owner, string? near, string? title = null, TargetBrowse browses = TargetBrowse.Any) => Pick((HWND)owner, near, title, browses);

    /// <inheritdoc cref="Pick(nint, string?, string, TargetBrowse)"/>
    public static string? Pick(HWND owner, string? near, string? title = null, TargetBrowse browses = TargetBrowse.Any)
    {
        var classId = new Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7");
        IFileOpenDialog* dialog = null;
        IFileDialogCustomize* custom = null;
        Events* events = null;
        IShellItem* chosen = null;
        var state = new State();
        var handle = GCHandle.Alloc(state);
        uint cookie = 0;
        try
        {
            if (CoCreateInstance(&classId, null, (uint)CLSCTX.CLSCTX_INPROC_SERVER, __uuidof<IFileOpenDialog>(), (void**)&dialog).FAILED || dialog is null)
            {
                return null;
            }
            uint options = 0;
            _ = dialog->GetOptions(&options);
            // A shortcut chosen is pinned as the shortcut, its arguments and all, not as what it points at.
            options |= (uint)(FILEOPENDIALOGOPTIONS.FOS_FORCEFILESYSTEM | FILEOPENDIALOGOPTIONS.FOS_PATHMUSTEXIST | FILEOPENDIALOGOPTIONS.FOS_NODEREFERENCELINKS
                | (browses == TargetBrowse.Folder ? FILEOPENDIALOGOPTIONS.FOS_PICKFOLDERS : FILEOPENDIALOGOPTIONS.FOS_FILEMUSTEXIST));
            _ = dialog->SetOptions(options);
            fixed (char* text = title ?? S.DockPinChoose)
            {
                _ = dialog->SetTitle(text);
            }
            if (TargetPicks.StartIn(near, Directory.Exists) is { } start)
            {
                IShellItem* at = null;
                fixed (char* path = start)
                {
                    if (SHCreateItemFromParsingName(path, null, __uuidof<IShellItem>(), (void**)&at).SUCCEEDED && at is not null)
                    {
                        _ = dialog->SetFolder(at);
                        at->Release();
                    }
                }
            }
            // The folder button; without it (a dialog that won't be customised) Open still picks files. Only where
            // either may be picked: a folder's own dialog picks folders by Open, a file's none.
            if (browses == TargetBrowse.Any && dialog->QueryInterface(__uuidof<IFileDialogCustomize>(), (void**)&custom).SUCCEEDED && custom is not null)
            {
                fixed (char* label = TargetPicks.FolderButton)
                {
                    _ = custom->AddPushButton(FolderButtonId, label);
                }
            }
            events = Events.Create(GCHandle.ToIntPtr(handle), (IFileDialog*)dialog);
            _ = dialog->Advise((IFileDialogEvents*)events, &cookie);
            // What the frame asked of Karpo's windows goes as the dialog's own loop begins (Surfaces).
            Surfaces.Present();
            var shown = dialog->Show(owner);
            if (state.Folder is { } folder)
            {
                return TargetPicks.Picked(TargetPickEnd.Folder, null, folder, null, Directory.Exists);
            }
            if (shown.FAILED || dialog->GetResult(&chosen).FAILED || chosen is null)
            {
                return TargetPicks.Picked(TargetPickEnd.Cancelled, null, null, null, Directory.Exists);
            }
            return TargetPicks.Picked(TargetPickEnd.Opened, PathOf(chosen), null, null, Directory.Exists);
        }
        finally
        {
            if (cookie != 0)
            {
                _ = dialog->Unadvise(cookie);
            }
            if (events is not null)
            {
                events->Owner = null;
                ((IUnknown*)events)->Release();
            }
            if (chosen is not null)
            {
                chosen->Release();
            }
            if (custom is not null)
            {
                custom->Release();
            }
            if (dialog is not null)
            {
                dialog->Release();
            }
            handle.Free();
        }
    }

    /// <summary>What the events learn while the dialog is up: the folder "Select folder" took.</summary>
    private sealed class State
    {
        public string? Folder;
    }

    /// <summary>An item's file-system path; null for one that has none (This PC, a library).</summary>
    private static string? PathOf(IShellItem* item)
    {
        if (item is null)
        {
            return null;
        }
        char* name = null;
        if (item->GetDisplayName(SIGDN.SIGDN_FILESYSPATH, &name).FAILED || name is null)
        {
            return null;
        }
        try
        {
            return new string(name);
        }
        finally
        {
            CoTaskMemFree(name);
        }
    }

    /// <summary>The folder the button would take now (<see cref="TargetPicks.Folder"/>); null when there is none.</summary>
    private static string? FolderNow(IFileDialog* dialog)
    {
        IShellItem* selected = null;
        IShellItem* current = null;
        try
        {
            _ = dialog->GetCurrentSelection(&selected);
            _ = dialog->GetFolder(&current);
            return TargetPicks.Folder(PathOf(selected), PathOf(current), Directory.Exists);
        }
        finally
        {
            if (selected is not null)
            {
                selected->Release();
            }
            if (current is not null)
            {
                current->Release();
            }
        }
    }

    /// <summary>The button grey where it has no folder to take, as the dialog's own buttons go grey.</summary>
    private static void ShowButton(IFileDialog* dialog)
    {
        IFileDialogCustomize* custom = null;
        if (dialog->QueryInterface(__uuidof<IFileDialogCustomize>(), (void**)&custom).FAILED || custom is null)
        {
            return;
        }
        var state = FolderNow(dialog) is not null ? CDCONTROLSTATEF.CDCS_ENABLEDVISIBLE : CDCONTROLSTATEF.CDCS_VISIBLE;
        _ = custom->SetControlState(FolderButtonId, state);
        custom->Release();
    }

    // --- The events object ---------------------------------------------------
    //
    // None of these may let an exception escape: thrown out of an UnmanagedCallersOnly method, it
    // ends the process instead of the dialog.

    [StructLayout(LayoutKind.Sequential)]
    private struct Events
    {
        public IFileDialogEvents.Vtbl<IFileDialogEvents>* Vtable;
        public IFileDialogControlEvents.Vtbl<IFileDialogControlEvents>* ControlVtable;
        public int Refs;
        public nint State;

        // The dialog, borrowed: it outlives these events, and Pick clears this before letting go.
        public IFileDialog* Owner;

        private static readonly IFileDialogEvents.Vtbl<IFileDialogEvents>* Table = BuildTable();
        private static readonly IFileDialogControlEvents.Vtbl<IFileDialogControlEvents>* ControlTable = BuildControlTable();

        public static Events* Create(nint state, IFileDialog* owner)
        {
            var events = (Events*)NativeMemory.Alloc((nuint)sizeof(Events));
            *events = new Events { Vtable = Table, ControlVtable = ControlTable, Refs = 1, State = state, Owner = owner };
            return events;
        }

        /// <summary>The block behind its second interface, which points one field in.</summary>
        public static Events* FromControl(void* self) => (Events*)((byte*)self - sizeof(nint));

        private static IFileDialogEvents.Vtbl<IFileDialogEvents>* BuildTable()
        {
            var table = (IFileDialogEvents.Vtbl<IFileDialogEvents>*)NativeMemory.Alloc((nuint)sizeof(IFileDialogEvents.Vtbl<IFileDialogEvents>));
            table->QueryInterface = &QueryInterface;
            table->AddRef = &AddRef;
            table->Release = &Release;
            table->OnFileOk = &OnFileOk;
            table->OnFolderChanging = &OnFolderChanging;
            table->OnFolderChange = &OnFolderChange;
            table->OnSelectionChange = &OnSelectionChange;
            table->OnShareViolation = &OnShareViolation;
            table->OnTypeChange = &OnTypeChange;
            table->OnOverwrite = &OnOverwrite;
            return table;
        }

        private static IFileDialogControlEvents.Vtbl<IFileDialogControlEvents>* BuildControlTable()
        {
            var table = (IFileDialogControlEvents.Vtbl<IFileDialogControlEvents>*)NativeMemory.Alloc((nuint)sizeof(IFileDialogControlEvents.Vtbl<IFileDialogControlEvents>));
            table->QueryInterface = &ControlQueryInterface;
            table->AddRef = &ControlAddRef;
            table->Release = &ControlRelease;
            table->OnItemSelected = &OnItemSelected;
            table->OnButtonClicked = &OnButtonClicked;
            table->OnCheckButtonToggled = &OnCheckButtonToggled;
            table->OnControlActivating = &OnControlActivating;
            return table;
        }

        // --- IUnknown, for both interfaces ---------------------------------------

        private static int Query(Events* events, Guid* iid, void** result)
        {
            if (result == null)
            {
                return E.E_POINTER;
            }
            if (iid == null)
            {
                *result = null;
                return E.E_POINTER;
            }
            Guid* unknown = __uuidof<IUnknown>();
            Guid* first = __uuidof<IFileDialogEvents>();
            Guid* second = __uuidof<IFileDialogControlEvents>();
            if (*iid == *unknown || *iid == *first)
            {
                *result = &events->Vtable;
            }
            else if (*iid == *second)
            {
                *result = &events->ControlVtable;
            }
            else
            {
                *result = null;
                return E.E_NOINTERFACE;
            }
            Interlocked.Increment(ref events->Refs);
            return S_OK;
        }

        private static uint Drop(Events* events)
        {
            var refs = Interlocked.Decrement(ref events->Refs);
            if (refs == 0)
            {
                NativeMemory.Free(events);
            }
            return (uint)refs;
        }

        // Each catches, though none of what it calls is expected to throw: an exception out of here
        // ends the process. A failed answer is E_UNEXPECTED, a failed count 1 (never "freed").

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
        private static int QueryInterface(IFileDialogEvents* self, Guid* iid, void** result)
        {
            try
            {
                return Query((Events*)self, iid, result);
            }
            catch (Exception error)
            {
                return Failed(error, E.E_UNEXPECTED);
            }
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
        private static uint AddRef(IFileDialogEvents* self)
        {
            try
            {
                return (uint)Interlocked.Increment(ref ((Events*)self)->Refs);
            }
            catch (Exception error)
            {
                return (uint)Failed(error, 1);
            }
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
        private static uint Release(IFileDialogEvents* self)
        {
            try
            {
                return Drop((Events*)self);
            }
            catch (Exception error)
            {
                return (uint)Failed(error, 1);
            }
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
        private static int ControlQueryInterface(IFileDialogControlEvents* self, Guid* iid, void** result)
        {
            try
            {
                return Query(FromControl(self), iid, result);
            }
            catch (Exception error)
            {
                return Failed(error, E.E_UNEXPECTED);
            }
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
        private static uint ControlAddRef(IFileDialogControlEvents* self)
        {
            try
            {
                return (uint)Interlocked.Increment(ref FromControl(self)->Refs);
            }
            catch (Exception error)
            {
                return (uint)Failed(error, 1);
            }
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
        private static uint ControlRelease(IFileDialogControlEvents* self)
        {
            try
            {
                return Drop(FromControl(self));
            }
            catch (Exception error)
            {
                return (uint)Failed(error, 1);
            }
        }

        /// <summary>Says what went wrong and hands back <paramref name="answer"/>.</summary>
        private static int Failed(Exception error, int answer)
        {
            try
            {
                Console.Error.WriteLine($"Karpo: open dialog: {error.Message}");
            }
            catch (Exception)
            {
                // Nowhere left to say it.
            }
            return answer;
        }

        // --- IFileDialogEvents ---------------------------------------------------
        //
        // E_NOTIMPL is the documented "no answer" for the ones that offer a choice: the dialog
        // then does what it would have done.

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
        private static int OnFileOk(IFileDialogEvents* self, IFileDialog* dialog)
        {
            try
            {
                return S_OK;
            }
            catch (Exception error)
            {
                return Failed(error, E.E_UNEXPECTED);
            }
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
        private static int OnFolderChanging(IFileDialogEvents* self, IFileDialog* dialog, IShellItem* folder)
        {
            try
            {
                return S_OK;
            }
            catch (Exception error)
            {
                return Failed(error, E.E_UNEXPECTED);
            }
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
        private static int OnFolderChange(IFileDialogEvents* self, IFileDialog* dialog)
        {
            try
            {
                return Refresh(dialog);
            }
            catch (Exception error)
            {
                return Failed(error, E.E_UNEXPECTED);
            }
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
        private static int OnSelectionChange(IFileDialogEvents* self, IFileDialog* dialog)
        {
            try
            {
                return Refresh(dialog);
            }
            catch (Exception error)
            {
                return Failed(error, E.E_UNEXPECTED);
            }
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
        private static int OnShareViolation(IFileDialogEvents* self, IFileDialog* dialog, IShellItem* item, FDE_SHAREVIOLATION_RESPONSE* response)
        {
            try
            {
                return E.E_NOTIMPL;
            }
            catch (Exception error)
            {
                return Failed(error, E.E_UNEXPECTED);
            }
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
        private static int OnTypeChange(IFileDialogEvents* self, IFileDialog* dialog)
        {
            try
            {
                return S_OK;
            }
            catch (Exception error)
            {
                return Failed(error, E.E_UNEXPECTED);
            }
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
        private static int OnOverwrite(IFileDialogEvents* self, IFileDialog* dialog, IShellItem* item, FDE_OVERWRITE_RESPONSE* response)
        {
            try
            {
                return E.E_NOTIMPL;
            }
            catch (Exception error)
            {
                return Failed(error, E.E_UNEXPECTED);
            }
        }

        private static int Refresh(IFileDialog* dialog)
        {
            try
            {
                if (dialog is not null)
                {
                    ShowButton(dialog);
                }
            }
            catch (Exception error)
            {
                Console.Error.WriteLine($"Karpo: open dialog: {error.Message}");
            }
            return S_OK;
        }

        // --- IFileDialogControlEvents --------------------------------------------

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
        private static int OnItemSelected(IFileDialogControlEvents* self, IFileDialogCustomize* custom, uint control, uint item)
        {
            try
            {
                return S_OK;
            }
            catch (Exception error)
            {
                return Failed(error, E.E_UNEXPECTED);
            }
        }

        /// <summary>"Select folder": the folder it takes, and the dialog closed on it; nothing when there is none.</summary>
        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
        private static int OnButtonClicked(IFileDialogControlEvents* self, IFileDialogCustomize* custom, uint control)
        {
            var events = FromControl(self);
            try
            {
                if (control != FolderButtonId || events->Owner is null || FolderNow(events->Owner) is not { } folder)
                {
                    return S_OK;
                }
                ((State)GCHandle.FromIntPtr(events->State).Target!).Folder = folder;
                // Show returns what Close is handed; Pick reads the folder before it looks.
                _ = events->Owner->Close(S_OK);
            }
            catch (Exception error)
            {
                Console.Error.WriteLine($"Karpo: open dialog: {error.Message}");
            }
            return S_OK;
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
        private static int OnCheckButtonToggled(IFileDialogControlEvents* self, IFileDialogCustomize* custom, uint control, BOOL on)
        {
            try
            {
                return S_OK;
            }
            catch (Exception error)
            {
                return Failed(error, E.E_UNEXPECTED);
            }
        }

        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvMemberFunction)])]
        private static int OnControlActivating(IFileDialogControlEvents* self, IFileDialogCustomize* custom, uint control)
        {
            try
            {
                return S_OK;
            }
            catch (Exception error)
            {
                return Failed(error, E.E_UNEXPECTED);
            }
        }
    }
}
