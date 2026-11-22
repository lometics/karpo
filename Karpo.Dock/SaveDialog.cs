// Windows' own Save and Open dialogs for a file of one type (§339): ⚙ › Back up and reset's Export
// settings saves "Karpo settings 2026-10-23.json" where the person says, and Import settings opens one,
// as the settings app's FileSavePicker and FileOpenPicker did — the Documents folder to start in, the
// type's name and its extension in the type box, the extension added where the name has none, Save
// asking before it writes over a file. FolderDialog is the one for a folder, TargetDialog for what a pin
// opens. Shown modally over the owner on the caller's thread, which must be an STA (the dock's is).

using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

internal static unsafe class SaveDialog
{
    private static readonly Guid SaveClass = new("C0B4E2F3-BA21-4773-8DBA-335EC946EB8B");
    private static readonly Guid OpenClass = new("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7");

    /// <summary>
    /// Windows' Save dialog over <paramref name="owner"/>, <paramref name="name"/> in its name box, files of
    /// <paramref name="type"/> (<paramref name="extension"/>, ".json") offered; the path chosen, or null where
    /// it was cancelled or could not be shown.
    /// </summary>
    public static string? Save(HWND owner, string name, string type, string extension)
    {
        IFileSaveDialog* dialog = null;
        var classId = SaveClass;
        if (CoCreateInstance(&classId, null, (uint)CLSCTX.CLSCTX_INPROC_SERVER, __uuidof<IFileSaveDialog>(), (void**)&dialog).FAILED || dialog is null)
        {
            return null;
        }
        try
        {
            fixed (char* text = name)
            {
                _ = dialog->SetFileName(text);
            }
            return Show((IFileDialog*)dialog, owner, type, extension, FILEOPENDIALOGOPTIONS.FOS_OVERWRITEPROMPT);
        }
        finally
        {
            dialog->Release();
        }
    }

    /// <summary>Windows' Open dialog over <paramref name="owner"/> for a file of <paramref name="type"/> (<paramref name="extension"/>); its path, or null.</summary>
    public static string? Open(HWND owner, string type, string extension)
    {
        IFileOpenDialog* dialog = null;
        var classId = OpenClass;
        if (CoCreateInstance(&classId, null, (uint)CLSCTX.CLSCTX_INPROC_SERVER, __uuidof<IFileOpenDialog>(), (void**)&dialog).FAILED || dialog is null)
        {
            return null;
        }
        try
        {
            return Show((IFileDialog*)dialog, owner, type, extension, FILEOPENDIALOGOPTIONS.FOS_FILEMUSTEXIST);
        }
        finally
        {
            dialog->Release();
        }
    }

    /// <summary>Either dialog, set up and shown: its type, the Documents folder to start in, the file system's paths alone.</summary>
    private static string? Show(IFileDialog* dialog, HWND owner, string type, string extension, FILEOPENDIALOGOPTIONS more)
    {
        uint options = 0;
        _ = dialog->GetOptions(&options);
        _ = dialog->SetOptions(options | (uint)(FILEOPENDIALOGOPTIONS.FOS_FORCEFILESYSTEM | FILEOPENDIALOGOPTIONS.FOS_PATHMUSTEXIST | more));
        fixed (char* typeName = type)
        fixed (char* pattern = "*" + extension)
        fixed (char* bare = extension.TrimStart('.'))
        {
            var filter = new COMDLG_FILTERSPEC { pszName = typeName, pszSpec = pattern };
            _ = dialog->SetFileTypes(1, &filter);
            _ = dialog->SetDefaultExtension(bare);
        }
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (documents.Length > 0 && Directory.Exists(documents))
        {
            IShellItem* start = null;
            fixed (char* path = documents)
            {
                if (SHCreateItemFromParsingName(path, null, __uuidof<IShellItem>(), (void**)&start).SUCCEEDED && start is not null)
                {
                    // The folder it opens in the first time; afterwards Windows remembers the last one.
                    _ = dialog->SetDefaultFolder(start);
                    start->Release();
                }
            }
        }
        IShellItem* chosen = null;
        // What the frame asked of Karpo's windows goes as the dialog's own loop begins (Surfaces).
        Surfaces.Present();
        if (dialog->Show(owner).FAILED || dialog->GetResult(&chosen).FAILED || chosen is null)
        {
            return null;
        }
        try
        {
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
            chosen->Release();
        }
    }
}
