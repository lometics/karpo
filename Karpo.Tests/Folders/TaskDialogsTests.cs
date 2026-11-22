// Windows' own question before a delete loses widgets (Karpo.Dock\TaskDialogs.cs), shown for real
// on a hidden desktop over a topmost stand-in for the bar: owned by it, topmost above it, Cancel the
// default (its buttons are no windows to ask: the picture shows it ringed), the delete button
// answering true and Cancel false. Its picture is folder-delete-dialog.png.
//
// Needs a window station: KARPO_UI_TESTS=1.

using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Tests.Folders;

public sealed unsafe class TaskDialogsTests
{
    // What the timer does once the dialog shows, and what it saw; the dialog's thread's alone.
    [ThreadStatic] private static uint answer;
    [ThreadStatic] private static string? picture;
    [ThreadStatic] private static HWND seen;
    [ThreadStatic] private static HWND seenOwner;
    [ThreadStatic] private static bool seenTopmost;
    [ThreadStatic] private static bool pictured;

    [UiFact]
    public void TheDialogStandsOverTheBarAndAnswers()
    {
        WindowShots.OnHiddenDesktop("delete-dialog", (gfx, instance) =>
        {
            HWND bar;
            fixed (char* kind = "STATIC")
            {
                bar = CreateWindowExW(WS.WS_EX_TOPMOST | WS.WS_EX_TOOLWINDOW, kind, null, WS.WS_POPUP | WS.WS_VISIBLE, 0, 1032, 1920, 48, HWND.NULL, HMENU.NULL, instance, null);
            }
            Assert.True(bar != HWND.NULL);
            // The test host has no manifest: comctl32 v6 as the dock's own manifest asks for it.
            var manifest = Path.Combine(Path.GetTempPath(), $"karpo-comctl6-{Guid.NewGuid():N}.manifest");
            File.WriteAllText(manifest, """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <assembly xmlns="urn:schemas-microsoft-com:asm.v1" manifestVersion="1.0">
                  <dependency><dependentAssembly>
                    <assemblyIdentity type="win32" name="Microsoft.Windows.Common-Controls" version="6.0.0.0" processorArchitecture="*" publicKeyToken="6595b64144ccf1df" language="*" />
                  </dependentAssembly></dependency>
                </assembly>
                """);
            HANDLE context;
            fixed (char* source = manifest)
            {
                var create = new ACTCTXW { cbSize = (uint)sizeof(ACTCTXW), lpSource = source };
                context = CreateActCtxW(&create);
            }
            File.Delete(manifest);
            Assert.True(context != INVALID_HANDLE_VALUE, "no activation context");
            nuint cookie = 0;
            Assert.True(ActivateActCtx(context, &cookie));
            try
            {
                var question = FolderDeletes.ForRemoving([Sound()])!;

                answer = TaskDialogs.DeleteId;
                picture = "folder-delete-dialog.png";
                _ = SetTimer(HWND.NULL, 0, 300, &Tick);
                Assert.True(new TaskDialogs().Ask(bar, question));
                Assert.True(seen != HWND.NULL, "the dialog never showed");
                Assert.Equal(bar, seenOwner);
                Assert.True(seenTopmost, "the dialog is topmost, over the bar");
                Assert.True(pictured, "the dialog's picture came back blank");
                Assert.Equal(7, question.Widgets);

                answer = IDCANCEL;
                picture = null;
                seen = HWND.NULL;
                _ = SetTimer(HWND.NULL, 0, 300, &Tick);
                Assert.False(new TaskDialogs().Ask(bar, question));
                Assert.True(seen != HWND.NULL);
            }
            finally
            {
                _ = DeactivateActCtx(0, cookie);
                ReleaseActCtx(context);
                _ = DestroyWindow(bar);
            }
        });
    }

    private static BarItem Sound()
    {
        var island = BarKind.FolderList.New();
        island.Folder.Lists = [[]];
        foreach (var kind in new[] { ListKind.SoundVolume, ListKind.SoundOutput, ListKind.SoundInput, ListKind.Divider, ListKind.SoundMixer, ListKind.SpatialSound, ListKind.SoundSwitch, ListKind.MicrophoneSwitch })
        {
            island.Folder.Put(kind.New());
        }
        return island;
    }

    /// <summary>The dialog of this thread, once it shows: looked at, pictured, then answered.</summary>
    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    private static void Tick(HWND hwnd, uint message, nuint id, uint time)
    {
        HWND dialog = HWND.NULL;
        _ = EnumThreadWindows(GetCurrentThreadId(), &Find, (LPARAM)(nint)(&dialog));
        if (dialog == HWND.NULL || !IsWindowVisible(dialog))
        {
            return;
        }
        _ = KillTimer(HWND.NULL, id);
        seen = dialog;
        seenOwner = GetWindow(dialog, GW_OWNER);
        seenTopmost = (GetWindowLongW(dialog, GWL.GWL_EXSTYLE) & WS.WS_EX_TOPMOST) != 0;
        if (picture is { } file)
        {
            _ = DwmFlush();
            pictured = WindowShots.Save(dialog, file);
        }
        _ = PostMessageW(dialog, (uint)TASKDIALOG_MESSAGES.TDM_CLICK_BUTTON, (WPARAM)(nuint)answer, 0);
    }

    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    private static BOOL Find(HWND window, LPARAM found)
    {
        var name = stackalloc char[16];
        if (GetClassNameW(window, name, 16) > 0 && new string(name) == "#32770")
        {
            *(HWND*)(nint)found = window;
            return false;
        }
        return true;
    }
}
