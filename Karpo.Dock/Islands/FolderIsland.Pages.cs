// A line's page in the open folder (Karpo.Core\FolderPages.cs, docs\widgets.md §170): Windows' ">"
// on a quick settings tile. The line's column becomes the page where it stands — its other columns
// stay — and goes back to its lines on the page's back arrow, on Escape, when the folder is arranged
// and when it closes. The page's lines are widgets of the folder's like any other (their sources,
// their ticks, their news), keyed under "page/", made as it opens and dropped as it goes; nothing
// of them is in the config. What the open folder lays out is Laid and Tiles: the page's while one
// stands, the config's otherwise; everything that edits the folder edits Options, which a page never
// touches — and a folder with a page open is never arranged.

using Karpo.Core;

namespace Karpo.Dock;

internal sealed partial class FolderIsland
{
    /// <summary>The open page: the line it is of, its options as the open folder lays them out, and its tiles.</summary>
    private sealed record OpenPage(string Line, ListKind Kind, FolderOptions Laid, List<FolderTile> Tiles);

    private OpenPage? page;

    // The page that went, kept until the folder's next news: its back arrow is pressed inside its own
    // drawing, and a widget is not disposed under the press that let it go.
    private readonly List<IIsland> leaving = [];

    // A back arrow asked from inside a page's widget, done on the folder's next news (Post).
    private bool backAsked;

    private Config? lastConfig;

    /// <summary>What the open folder lays out: the page's columns while one stands, else the config's.</summary>
    internal FolderOptions Laid => page?.Laid ?? options;

    /// <summary>The key of the line whose page stands open, or null.</summary>
    internal string? PageOf => page?.Line;

    /// <summary>Whether a page stands open.</summary>
    internal bool Paged => page is not null;

    /// <summary>
    /// Opens the page of the line keyed <paramref name="line"/> (<see cref="FolderPages"/>) in its
    /// column: true where it did. Nothing for a line without one, or one not this folder's.
    /// </summary>
    internal bool OpenPageOf(string line)
    {
        if (tiles.FindIndex(tile => string.Equals(tile.Island.Key, line, StringComparison.OrdinalIgnoreCase)) is not (>= 0 and var at)
            || ListWidgets.Find(tiles[at].Cell.Island.Kind)?.Kind is not { } kind
            || FolderPages.Of(kind) is not { } shown
            || Folders.PlaceOf(options, tiles[at].Cell) is not { } place)
        {
            return false;
        }
        ClosePage(tell: false);
        var cells = FolderPages.Cells(shown);
        // The folder's columns with the line's one the page: its width, the others as they are.
        var laid = new FolderOptions
        {
            Lists = [.. options.Lists.Select((list, index) => index == place.List ? cells : list)],
            Widths = options.Widths is { } widths ? [.. widths] : null,
        };
        var made = new List<FolderTile>();
        foreach (var cell in laid.Lists.SelectMany(list => list))
        {
            if (!cells.Contains(cell))
            {
                if (tiles.Find(tile => ReferenceEquals(tile.Cell, cell)) is { } kept)
                {
                    made.Add(kept);
                }
                continue;
            }
            var key = $"{Key}/page/{cell.Island.Id}";
            if (MakePageLine(key, cell.Island) is not { } widget)
            {
                continue;
            }
            widgets[key] = widget;
            made.Add(new FolderTile(widget, cell));
        }
        page = new OpenPage(line, kind, laid, made);
        Changed?.Invoke();
        return true;
    }

    /// <summary>
    /// A page's line: its head the title beside the back arrow (PageHeadTile), a quick settings page
    /// its own title with the back arrow before it (QuickPageTile.Back), anything else the line it is.
    /// </summary>
    private IIsland? MakePageLine(string key, CellItem cell)
    {
        IIsland? made;
        try
        {
            made = string.Equals(cell.Kind, WidgetInfos.Heading.Id, StringComparison.Ordinal)
                ? new PageHeadTile(key, cell, Gfx, lastConfig ?? new Config()) { Back = AskBack }
                : Islands.CreateLine(key, cell, ListWidgets.Find(cell.Kind)!.Kind, Gfx, lastConfig ?? new Config(), countdowns);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Console.Error.WriteLine($"Karpo: a page's {cell.Kind} not made: {error.Message}");
            return null;
        }
        if (made is null)
        {
            return null;
        }
        if (made is QuickPageTile quick)
        {
            quick.Back = AskBack;
        }
        if (made is CellIsland held)
        {
            held.Container = this;
        }
        if (attached)
        {
            made.Attach(dock, token);
        }
        if (Showing)
        {
            Guard(made, () =>
            {
                made.Visible(true);
                made.Refresh(Gfx);
                made.Tick(Gfx);
            });
        }
        return made;
    }

    /// <summary>
    /// Back to the folder's lines: the page's widgets go (once the folder's next news has come,
    /// <see cref="leaving"/>), and the open folder lays out its lines again where <paramref name="tell"/>.
    /// True where a page stood.
    /// </summary>
    internal bool ClosePage(bool tell = true)
    {
        backAsked = false;
        if (page is not { } open)
        {
            return false;
        }
        page = null;
        foreach (var tile in open.Tiles)
        {
            var key = tile.Island.Key;
            if (key.StartsWith($"{Key}/page/", StringComparison.OrdinalIgnoreCase) && widgets.Remove(key))
            {
                ticked.Remove(tile.Island);
                if (tile.Island is CellIsland held)
                {
                    held.Container = null;
                }
                Guard(tile.Island, () => tile.Island.Visible(false));
                leaving.Add(tile.Island);
            }
        }
        if (tell)
        {
            Changed?.Invoke();
        }
        return true;
    }

    /// <summary>A back arrow pressed inside a page's widget: done on the folder's next news, out of the widget's own press.</summary>
    private void AskBack()
    {
        backAsked = true;
        Post();
    }

    /// <summary>On the folder's news: a back arrow asked is done, and what went with the last page is let go.</summary>
    private void PageNews()
    {
        if (backAsked)
        {
            ClosePage();
        }
        DropLeaving();
    }

    private void DropLeaving()
    {
        foreach (var gone in leaving)
        {
            Guard(gone, gone.Dispose);
        }
        leaving.Clear();
    }

    /// <summary>
    /// The config changed under an open page: the page stays where its line still is, with the other
    /// columns as they are now, and goes where the line went.
    /// </summary>
    private void PageConfigured()
    {
        if (page is not { } open)
        {
            return;
        }
        var line = tiles.Find(tile => string.Equals(tile.Island.Key, open.Line, StringComparison.OrdinalIgnoreCase));
        if (line is null || Folders.PlaceOf(options, line.Cell) is not { } place || ListWidgets.Find(line.Cell.Island.Kind)?.Kind != open.Kind)
        {
            ClosePage(tell: false);
            return;
        }
        var cells = open.Tiles.Where(tile => tile.Island.Key.StartsWith($"{Key}/page/", StringComparison.OrdinalIgnoreCase)).Select(tile => tile.Cell).ToList();
        var laid = new FolderOptions
        {
            Lists = [.. options.Lists.Select((list, index) => index == place.List ? cells : list)],
            Widths = options.Widths is { } widths ? [.. widths] : null,
        };
        var made = new List<FolderTile>();
        foreach (var cell in laid.Lists.SelectMany(list => list))
        {
            if (open.Tiles.Find(tile => ReferenceEquals(tile.Cell, cell)) is { } own)
            {
                made.Add(own);
            }
            else if (tiles.Find(tile => ReferenceEquals(tile.Cell, cell)) is { } kept)
            {
                made.Add(kept);
            }
        }
        page = open with { Laid = laid, Tiles = made };
    }
}
