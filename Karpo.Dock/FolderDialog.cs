// Windows' own dialog for choosing a folder (FOS_PICKFOLDERS): where nearby sharing saves what it
// receives, as Settings' "Change" asks it. TargetDialog is the one for a file or a folder; this one
// shows folders alone, its Open the dialog's "Select Folder". Shown modally on the caller's thread,
// which must be an STA; nearby sharing's runs it on a thread of its own, owned by nothing, since the
// folder it was asked from goes as the dialog takes the focus.

using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

internal static unsafe class FolderDialog
{
    /// <summary>
    /// Shows the dialog over <paramref name="owner"/>, in <paramref name="start"/> where it is a
    /// folder; the folder chosen, or null where it was cancelled, could not be shown, or is no folder
    /// on disk (This PC, a library).
    /// </summary>
    public static string? Pick(HWND owner, string? start, string title)
    {
        var classId = new Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7");
        IFileOpenDialog* dialog = null;
        IShellItem* chosen = null;
        try
        {
            if (CoCreateInstance(&classId, null, (uint)CLSCTX.CLSCTX_INPROC_SERVER, __uuidof<IFileOpenDialog>(), (void**)&dialog).FAILED || dialog is null)
            {
                return null;
            }
            uint options = 0;
            _ = dialog->GetOptions(&options);
            options |= (uint)(FILEOPENDIALOGOPTIONS.FOS_PICKFOLDERS | FILEOPENDIALOGOPTIONS.FOS_FORCEFILESYSTEM | FILEOPENDIALOGOPTIONS.FOS_PATHMUSTEXIST);
            _ = dialog->SetOptions(options);
            fixed (char* text = title)
            {
                _ = dialog->SetTitle(text);
            }
            if (start is { Length: > 0 } folder && Directory.Exists(folder))
            {
                IShellItem* at = null;
                fixed (char* path = folder)
                {
                    if (SHCreateItemFromParsingName(path, null, __uuidof<IShellItem>(), (void**)&at).SUCCEEDED && at is not null)
                    {
                        _ = dialog->SetFolder(at);
                        at->Release();
                    }
                }
            }
            // What the frame asked of Karpo's windows goes as the dialog's own loop begins (Surfaces).
            Surfaces.Present();
            if (dialog->Show(owner).FAILED || dialog->GetResult(&chosen).FAILED || chosen is null)
            {
                return null;
            }
            char* name = null;
            if (chosen->GetDisplayName(SIGDN.SIGDN_FILESYSPATH, &name).FAILED || name is null)
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
        finally
        {
            if (chosen is not null)
            {
                chosen->Release();
            }
            if (dialog is not null)
            {
                dialog->Release();
            }
        }
    }
}
