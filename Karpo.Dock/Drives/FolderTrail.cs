// folder contents: the way down from a folder's contents' own folder to the subfolder shown in its place
// (2026-10-29, the person: "the folder widget should also change the contents inline on navigation and not
// actually open the submenus on the left or the right"). A subfolder clicked shows its items where its
// parent's were, as a settings menu's level opens in place of its rows (Flyout.Inline.cs); Back shows the
// parent again, as it was left. The line of a list (FolderContentsView) walks it.
//
// Plain data: what was left at each level — how far it was scrolled and what it showed — so going back shows
// it at once, never "Reading…" first, and a test checks the walk without a window. And what every level is
// sized by: the root's items (Sizing, §468), so the line does not change height as it goes in.

namespace Karpo.Dock.Drives;

/// <summary>The folders gone into from a root, the deepest shown; what each level under it showed as it was left.</summary>
internal sealed class FolderTrail
{
    private readonly List<(string Path, float Scroll, FolderContents? Shown)> levels = [];

    /// <summary>The folder the contents start from: a folder's contents' own.</summary>
    public string Root { get; private set; } = "";

    /// <summary>The folder shown now: the deepest gone into, else the root.</summary>
    public string Current => levels.Count > 0 ? levels[^1].Path : Root;

    /// <summary>How many folders deep from the root; 0 on the root itself.</summary>
    public int Depth => levels.Count;

    /// <summary>
    /// §468: what the level shown is sized by — the root's items as the root was left, at every level under it;
    /// <paramref name="shown"/> on the root itself (2026-11-01, the person: "it should not change its height dependent
    /// on level / contents except for the root level of course"). A walk is as tall in and back as its root: a
    /// subfolder of fewer items leaves room under them, one of more scrolls them. The root is followed only while it
    /// is shown, so what changes in it while a subfolder is shows, and sizes the walk, once it is shown again.
    /// </summary>
    public FolderContents Sizing(FolderContents shown) => levels.Count > 0 && levels[0].Shown is { } root ? root : shown;

    /// <summary>Back at <paramref name="root"/>, nothing gone into.</summary>
    public void Start(string root)
    {
        Root = root;
        levels.Clear();
    }

    /// <summary>Back at the root from however deep: what it was left showing; null on the root itself, which shows what it shows.</summary>
    public FolderContents? Home()
    {
        var shown = levels.Count > 0 ? levels[0].Shown : null;
        levels.Clear();
        return shown;
    }

    /// <summary>This walk as it stands, to take again (<see cref="Take"/>).</summary>
    public FolderTrail Copy()
    {
        var copy = new FolderTrail { Root = Root };
        copy.levels.AddRange(levels);
        return copy;
    }

    /// <summary>The walk <paramref name="other"/> as it stood when copied.</summary>
    public void Take(FolderTrail other)
    {
        Root = other.Root;
        levels.Clear();
        levels.AddRange(other.levels);
    }

    /// <summary>
    /// Into <paramref name="path"/>, a subfolder of the folder shown, which is left <paramref name="scroll"/> px
    /// scrolled showing <paramref name="shown"/>.
    /// </summary>
    public void Into(string path, float scroll = 0, FolderContents? shown = null) => levels.Add((path, scroll, shown));

    /// <summary>
    /// Back to the folder the one shown was gone into from: the name of the subfolder left (its row, to light or put
    /// the keys on), and how the parent was left — its scroll and what it showed. Null on the root, which has none.
    /// </summary>
    public (string Left, float Scroll, FolderContents? Shown)? Back()
    {
        if (levels.Count == 0)
        {
            return null;
        }
        var (path, scroll, shown) = levels[^1];
        levels.RemoveAt(levels.Count - 1);
        return (FolderReader.NameOf(path), scroll, shown);
    }
}
