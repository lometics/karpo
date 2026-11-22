// clipboard: the dock's part of the clipboard history — an item chosen in the clipboard's line
// (Clipboard\ClipboardLine.cs) is pasted where the person was, once the folder has gone and the
// window it gave the focus back to has settled (ClipboardPaste, PasteWait, on the host's PASTE
// timer: read every few milliseconds, never waited on). The line was the clipboard history entry's
// flyout until that became the clipboard preset (docs\widgets.md §101).

using TerraFX.Interop.Windows;

namespace Karpo.Dock;

public sealed unsafe partial class Dock
{
    /// <summary>The host's timer that reads the window a chosen item goes to again (<see cref="PasteWait"/>).</summary>
    private const uint PASTE = 45;

    /// <summary>The window a chosen item is to be pasted into, and the wait for its focus to settle; null when none is.</summary>
    private (HWND Back, PasteWait Wait)? pasting;

    /// <summary>
    /// A chosen item, pasted into <paramref name="back"/> once its focus has settled and where it is
    /// safe (ClipboardPaste): read now and again every few milliseconds, never for long.
    /// </summary>
    private void Paste(HWND back)
    {
        Kill(PASTE);
        pasting = null;
        if (!ClipboardPaste.Begin(back))
        {
            return;
        }
        pasting = (back, new PasteWait());
        Arm(PASTE, PasteWait.StepMs);
    }

    /// <summary>The paste's timer: the window read again, and pasted into, waited for, or left.</summary>
    private void PasteDue()
    {
        if (pasting is not { } now)
        {
            Kill(PASTE);
            return;
        }
        var step = now.Wait.Next(ClipboardPaste.Read(now.Back));
        if (step == PasteStep.Wait)
        {
            return;
        }
        Kill(PASTE);
        pasting = null;
        if (step == PasteStep.Paste)
        {
            ClipboardPaste.Send();
        }
    }
}
