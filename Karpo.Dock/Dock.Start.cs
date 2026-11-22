// §641: the dock's side of the Start widgets (docs\widgets.md §641): what a Start line asks to change of the config — a pin,
// an unpin, a pin moved to the front or dragged to another place, All apps' view — made as any edit is (Apply: saved, one
// step of edit mode's undo where it is on), the folder standing open meanwhile.

using Karpo.Core;
using Karpo.Dock.StartWidgets;

namespace Karpo.Dock;

public sealed unsafe partial class Dock
{
    /// <summary>The dock starts: the Start lines' changes are its to make.</summary>
    private void HookStart() => StartLine.Editor = StartEdited;

    /// <summary>The dock goes: nobody makes them.</summary>
    private static void UnhookStart() => StartLine.Editor = null;

    /// <summary>The Start line <paramref name="key"/> asks for <paramref name="edit"/>: made on a copy of the config, applied where it changed anything.</summary>
    private void StartEdited(string key, StartEdit edit)
    {
        var next = config.Clone();
        if (StartEdits.Apply(next, Islands.OwnerOf(key), Islands.IdOf(key), edit.Verb, edit.Target, StartSources.Now.Apps, edit.Index))
        {
            Apply(next);
        }
    }
}
