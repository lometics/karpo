// menus: where a menu stood, to show it there again (docs\widgets.md §356, §374). A menu the dock closes for a
// dialog of Windows' — a colour mixed, a pin's target browsed — and shows again when the dialog is done stands
// where it was: its levels by their rows (Flyout.LevelsAgain, by id where the words changed), each scrolled
// where it was, the row lit, what was typed in a field and not kept yet, the field with the keys and its
// caret (Dock.Resume). A menu the person opens is at its top, as Windows' own are
// (the person, 2026-10-28: "the settings menus dont always open at the root level although they should").

namespace Karpo.Dock;

/// <summary>§356: a field of a menu left with words typed in it: its row, the words, and where its caret and selection stood.</summary>
internal sealed record FieldMemory(uint Id, string Text, int Caret, int Anchor);

/// <summary>
/// §356: where a menu was left (<see cref="Flyout.Remembered"/>): each level open in place by its row (id and
/// words) and how far the level under it was scrolled, how far the deepest is, the row lit (id and words), the
/// fields typed in, the field with the keys.
/// </summary>
internal sealed record MenuMemory(
    IReadOnlyList<(uint Id, string Text, float Scroll)> Path,
    float Scroll,
    (uint Id, string Text)? Lit,
    IReadOnlyList<FieldMemory> Fields,
    uint FieldKeys);

public sealed unsafe partial class Flyout
{
    /// <summary>§356: where the open menu stands now, to open it there again; null where it is not open or is a submenu.</summary>
    internal MenuMemory? Remembered()
    {
        if (!IsOpen || parent is not null)
        {
            return null;
        }
        var lit = hot >= 0 && hot < rows.Count ? rows[hot].Item : null;
        List<FieldMemory> fields = [];
        foreach (var (id, state) in fieldStates)
        {
            // §338: a password's never.
            if (!state.Field.Masked && state.Field.Dirty)
            {
                fields.Add(new FieldMemory(id, state.Field.Text, state.Field.Editing.Caret, state.Field.Editing.Anchor));
            }
        }
        return new MenuMemory(
            [.. levels.Select(level => (level.Holder.Id, level.Holder.Text, level.Scroll))],
            scroll,
            lit is null ? null : (lit.Id, lit.Text),
            fields,
            fieldKeys != 0 && fieldStates.TryGetValue(fieldKeys, out var keyed) && !keyed.Field.Masked ? fieldKeys : 0);
    }

    /// <summary>
    /// §356: the menu just shown opened where <paramref name="memory"/> says it was left: its levels found
    /// again in its rows as they are now (a level whose row is gone, and those under it, are not), shown in
    /// place, scrolled and lit as they were; the fields' words back, the keys where they were.
    /// </summary>
    internal void Recall(MenuMemory memory)
    {
        if (!IsOpen || parent is not null || shownWith is not { } shown || gfx is null)
        {
            return;
        }
        levels.Clear();
        levels.AddRange(memory.Path.Select(level => (new FlyoutItem(level.Id, level.Text), level.Scroll)));
        var whole = LevelsAgain(shown.Items);
        if (levels.Count == 0 && memory.Path.Count == 0 && memory.Scroll == 0 && memory.Lit is null && memory.Fields.Count == 0)
        {
            return;
        }
        ShowInPlace(0, whole && memory.Lit is { } lit ? new FlyoutItem(lit.Id, lit.Text) : null, whole ? memory.Scroll : 0, keys: false);
        if (OnInlineLevel)
        {
            spokenTitle = levels[^1].Holder.Text;
        }
        foreach (var field in memory.Fields)
        {
            if (fieldStates.TryGetValue(field.Id, out var state) && !state.Field.Masked)
            {
                state.Field.Editing.SetText(field.Text);
                state.Field.Editing.MoveTo(field.Anchor, extend: false);
                state.Field.Editing.MoveTo(field.Caret, extend: true);
                RedrawField(state);
            }
        }
        if (memory.FieldKeys != 0 && fieldStates.TryGetValue(memory.FieldKeys, out var keyed) && rows.FindIndex(row => row.Item.Id == memory.FieldKeys) is var row and >= 0)
        {
            var (caret, anchor) = (keyed.Field.Editing.Caret, keyed.Field.Editing.Anchor);
            SetHot(row);
            FocusField(memory.FieldKeys, selectAll: false);
            keyed.Field.Editing.MoveTo(anchor, extend: false);
            keyed.Field.Editing.MoveTo(caret, extend: true);
            CaretMoved(keyed);
        }
    }
}
