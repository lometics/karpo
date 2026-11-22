// folders: where a folder was left (docs\widgets.md §357; the flicker design's UI session store, §5.4). A
// folder closed — another opened in its place, its own button, a menu that took it, an arrange that made its
// bar again — forgot everything one looked at in it: how far each line's items were scrolled, the day picked,
// the month or the week stepped to, a notification expanded, the page a line opened, the line with the keys.
// Its memory is kept by the folder's key, and its folder opens there again:
//
//  - **In edit mode**, for the rest of the edit session (the person, 2026-10-25, of menus: "yes go ahead,
//    reopen where i left it"): a folder switched away from and opened again, or closed and opened again, is
//    as it was left. Edit mode's start and end forget them all.
//  - **At rest** a folder opens at its start, as Windows' flyouts do and as it always has: today, every line
//    at its top. Only a folder the dock itself closes and opens again — handed on to its bar made again
//    (Dock.FolderFollows) — comes back as it was.

namespace Karpo.Dock;

/// <summary>§357: where a folder was left: each line's scroll and its own view of what it shows, by its widget's key; what its widgets agreed on (the day picked); the line whose page stood open; the line with the keys.</summary>
internal sealed record FolderMemory(
    IReadOnlyDictionary<string, LineMemory> Lines,
    IReadOnlyDictionary<string, object?> Channels,
    string? Page,
    string? Focused,
    bool Keyboard);

/// <summary>§357: where a line was left: how far its items were scrolled, and its own view of what it shows (<see cref="CellIsland.ViewMemory"/>).</summary>
internal readonly record struct LineMemory(float Scroll, object? View);

/// <summary>
/// §357: where each folder was left in this edit session, by its key; forgotten as the session ends. The rule
/// (see the top of the file): a folder opened in edit mode opens where it was left in the session; at rest at
/// its start.
/// </summary>
internal sealed class FolderMemories
{
    private readonly Dictionary<string, FolderMemory> kept = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>How many folders are remembered, for tests.</summary>
    public int Count => kept.Count;

    /// <summary>The folder <paramref name="key"/> was left at <paramref name="memory"/>: kept while <paramref name="editing"/>.</summary>
    public void Left(string key, FolderMemory? memory, bool editing)
    {
        if (editing && memory is not null)
        {
            kept[key] = memory;
        }
    }

    /// <summary>Where the folder <paramref name="key"/> opens: where it was left in this session while <paramref name="editing"/>; null for its start.</summary>
    public FolderMemory? Recall(string key, bool editing) => editing ? kept.GetValueOrDefault(key) : null;

    /// <summary>The edit session is over (or one begins): every folder opens at its start again.</summary>
    public void Clear() => kept.Clear();
}
