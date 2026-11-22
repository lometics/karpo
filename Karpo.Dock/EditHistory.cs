// edit: undo and redo while arranging (docs\widgets.md §352; the person, 2026-10-25, on the edit bar as a
// breadcrumb: "sounds good try that"). Every change the person makes to the config in edit mode — a widget
// moved, added, cut, removed or pasted, a setting of any menu, a layout made, shown or deleted — is
// committed in one place, Dock.Apply, which hands the config as it was before it to Record (and a bar's
// height dragged, which a drag writes in place, Dock.KeepHeight). Undo and Redo give a config of an earlier
// or a later step back, which goes through Apply again as any change does (Config.Restored: saved as the
// file it is now would save), the bars made again from it.
//
// One history an edit mode: it starts empty and goes as edit mode ends (Dock.BeginEdit, EndEdit), and a
// change someone else wrote into the file meanwhile ends it too (Dock.ConfigChanged), so an undo never
// takes that back. At most Most steps, the oldest going first. A new change after an undo drops what
// could have been redone, as everywhere.
//
// Plain code without windows, so the tests can reach it.

using Karpo.Core;

namespace Karpo.Dock;

/// <summary>Edit mode's undo and redo, of snapshots of <typeparamref name="T"/> (the config); see the top of the file.</summary>
internal sealed class EditHistory<T> where T : class
{
    /// <summary>The most steps kept to undo.</summary>
    public const int Most = 50;

    private readonly List<T> undo = [];
    private readonly List<T> redo = [];

    public bool CanUndo => undo.Count > 0;

    public bool CanRedo => redo.Count > 0;

    /// <summary>A change was committed; <paramref name="before"/> is what it was before, never changed afterwards. What could be redone goes.</summary>
    public void Record(T before)
    {
        redo.Clear();
        undo.Add(before);
        if (undo.Count > Most)
        {
            undo.RemoveAt(0);
        }
    }

    /// <summary>The step before <paramref name="now"/>, which is kept to redo; null with nothing to undo.</summary>
    public T? Undo(T now) => Step(undo, redo, now);

    /// <summary>The step undone last, <paramref name="now"/> kept to undo again; null with nothing to redo.</summary>
    public T? Redo(T now) => Step(redo, undo, now);

    /// <summary>Nothing to undo or redo: edit mode began or ended, or the file changed under it.</summary>
    public void Clear()
    {
        undo.Clear();
        redo.Clear();
    }

    private static T? Step(List<T> from, List<T> to, T now)
    {
        if (from.Count == 0)
        {
            return null;
        }
        var back = from[^1];
        from.RemoveAt(from.Count - 1);
        to.Add(now);
        return back;
    }
}

public sealed unsafe partial class Dock
{
    /// <summary>edit: this edit mode's undo and redo (EditHistory.cs, §352).</summary>
    private readonly EditHistory<Config> history = new();

    /// <summary>edit: an undo or a redo is being applied, which is no step of its own.</summary>
    private bool stepping;

    /// <summary>edit: the edit bar's Undo and Ctrl+Z: the config of the step before, made the config again (<see cref="StepTo"/>).</summary>
    private void EditUndo()
    {
        if (CanStep)
        {
            StepTo(history.Undo(config));
        }
    }

    /// <summary>edit: the edit bar's Redo, Ctrl+Y and Ctrl+Shift+Z: the step undone last, made again (<see cref="StepTo"/>).</summary>
    private void EditRedo()
    {
        if (CanStep)
        {
            StepTo(history.Redo(config));
        }
    }

    /// <summary>edit: in edit mode, and no widget following the pointer, which is no step yet.</summary>
    private bool CanStep => editor is not null && follow is null;

    /// <summary>
    /// edit: <paramref name="to"/>, an earlier or a later step's config, made the config, saved and shown
    /// as every change is (<see cref="Apply"/>, <see cref="Config.Restored"/>); edit mode and a folder
    /// open stay, the selection keeps what is still there.
    /// </summary>
    private void StepTo(Config? to)
    {
        if (to is null)
        {
            return;
        }
        stepping = true;
        try
        {
            Apply(config.Restored(to));
        }
        finally
        {
            stepping = false;
        }
        editSelection.Keep(key => config.FindIsland(Islands.OwnerOf(key), Islands.IdOf(key)) is not null);
        ShowSelection();
    }
}
