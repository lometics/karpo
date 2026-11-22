// folder contents: what one folder on this PC holds, as a line of a list at every height (§380; backlog
// 14 and 28 before, docs\widgets.md §9). FolderContentsLine draws itself with this; its row of buttons on
// the taskbar went (§492). From §32 to §380 it was a pin's,
// set to show its folder's contents, and it draws as it did then.
//
// An item is drawn as a row of the drive flyout was: the shell's own icon
// for its kind, its name, and where a line has the room, when it last changed in a column the rows
// share; under the pointer its row lights as a row of the flyout does. The items come in the
// flyout's order — folders first, then files, names as a person reads them — and hidden and system
// ones are left out, as File Explorer leaves them out.
//
// On a list, on the list's own edges (ListForm, ListTiers): one row is the folder's picture in the
// icon column, its name where every name begins and the count against the trailing gutter; a taller
// line has the items under that, a row each at the list's own row height, with the date on the
// trailing gutter where the names keep their room. Held shorter than all of them, the items scroll
// under the folder's row (FolderContentsLine, ListScroll.cs): never "12 more items" (§461).
//
// Under the pointer only the item's row lights; the line around the rows never does
// (FolderContentsLine.LightsItsRows), so it never reads as one big button with the rows inside it.
//
// What a click on a row does is the line's (FolderContentsLine.Click): a file opens with its own program,
// a subfolder as the navigation says — File Explorer, or its items in place of its parent's — and a click
// anywhere else opens nothing (2026-10-30, the person: "the folder opens in file explorer when i click
// somewhere inside of it. this should not be"): the folder shown opens in File Explorer from the line's
// right click (Dock.WidgetMenus.cs, WidgetAct.OpenInExplorer).
//
// **In place** (2026-10-29, the person: "the folder widget should also change the contents inline on
// navigation and not actually open the submenus on the left or the right"): a subfolder gone into (Into)
// is shown where its parent was, the line the same line, as a settings menu's level opens in place of its
// rows (Flyout.Inline.cs) and Windows 11's quick settings open a page ("< Sound output"). Its whole head is
// the way back (BackHead; 2026-10-30, the person: "i want the whole header area to be the back button"): lit
// in one plate under the pointer, Windows' back arrow in the picture's column ("Back" its tooltip), the
// subfolder's name where the folder's was, its count after it. A subfolder's row has a chevron at its
// end where the navigation goes in (Navigates), as a menu's row with a level has. Back shows the parent
// as it was left — its rows at once, never "Reading…" first (FolderTrail) — the keys back on the
// subfolder left where they brought it back. The folder shown is the one read and followed. The line's height
// is its own folder's at every level (§468; FolderTrail.Sizing): an automatic line is as tall as its own items
// however deep it goes, a subfolder of fewer leaving room under them and one of more scrolling them.
//
// The keys, with the line's focus (FolderContentsLine.CardKey): Up and Down walk the rows, past either end
// the folder's; Enter goes into a subfolder, or opens what the row is (the folder's click: Keyed);
// Backspace and Left (Right where mirrored) go back.
//
// What the folder holds is read off the dock's thread through FolderSource, which a test replaces
// with made-up folders, and followed while it is shown (FolderWatch): a file saved into it shows a
// moment later. A folder that cannot be watched is read again on the minute's tick. §380 (the person:
// "when showing folder contents the reading text is there for around 10 seconds or so. just too
// long"): it is read as soon as the line is given its folder, not at the next tick of anything, and
// the names are shown the moment they are listed — the folder's picture and the items' icons, which
// the shell may be slow to draw, fill in after them.
//
// One layout (FolderBoard) is what a line is drawn from and what a press is tested against, so the
// two cannot disagree; it is a function of the room, the style, the view and what was read.
//
// **Views** (§498; the person, 2026-11-03: "the folder contents widget should also get different styles like grid,
// details etc. (similar to the ones in the file explorer)"), chosen in the line's settings' View (PinFolderView):
//
//   * List — the rows above, what every line was before the setting.
//   * Details — a row of column names under the folder's row, staying while the items scroll, and a compact row an
//     item: its 16-DIP icon, its name, and as many of Date modified, Type and Size as the width keeps beside a name
//     of at least 120 DIP (the date first, then the size, then the type), Explorer's order across.
//   * Tiles — a 48-DIP icon beside three lines: the name, the type, the size ("File folder" alone for a folder), as
//     many tiles across as 220 DIP each go into the width.
//   * Medium icons and Large icons — a grid of 48- and 96-DIP icons, each with its name under it in two lines at
//     most, as many across as 84 and 124 DIP each go into the width.
//
// A picture or a video in tiles and icons wears the shell's thumbnail of it, read off the dock's thread and kept
// (FolderThumbnails), its icon until it comes. One column (a list, details, a narrow line of tiles) lights its items
// as bands edge to edge (§425); more than one lights a cell as Explorer does, a rounded plate inside it, the cells
// touching so that every pixel of the grid is an item's. The keys walk a grid in two dimensions: Left and Right the
// next item (Right the previous where mirrored), Up and Down the one above and under (the last where the row under
// is shorter); Backspace goes back. A name cut short says itself whole as the tooltip.
//
// drives: a drive's contents are a folder's contents line at its root, under the drive's line
// (docs\widgets.md §119). A folder on a network — a share, or a mapped drive — is never watched: a
// watcher's start waits on a share that has gone, on the dock's thread; it is read again on the minute
// while it shows (FolderContentsLine).

using System.Globalization;
using System.Numerics;
using Karpo.Core;
using Karpo.Dock.Drives;
using Karpo.Dock.Native;
using Karpo.Dock.QuickSettings.Controls;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock;

internal sealed unsafe class FolderContentsView : IDisposable
{
    /// <summary>Segoe Fluent Icons: Folder and Document, for what the shell has no picture of yet.</summary>
    internal const string FolderGlyph = "";

    internal const string FileGlyph = "";

    /// <summary>Nothing of the line's own under the pointer: a click there does nothing.</summary>
    internal const int NoTarget = 0;

    /// <summary>The head of a subfolder gone into, all of it the way back (<see cref="Back"/>).</summary>
    internal const int BackTarget = 2;

    /// <summary>An item's row, which opens it: this plus the item's place in what was read.</summary>
    internal const int OpenTarget = 1_000;

    /// <summary>Segoe Fluent Icons: ChevronRight, the mark of a subfolder's row that goes in; ChevronLeft where mirrored.</summary>
    private const string ChevronGlyph = "\uE974";

    private const string ChevronBackGlyph = "\uE973";

    /// <summary>
    /// How large the shell is asked to draw the icons, in px: enough for a row's 20 DIP at 200 %, so a
    /// row at any scale is a picture scaled down rather than one blown up.
    /// </summary>
    internal const int IconPx = 40;

    /// <summary>The most an item's icon box is, in DIP: the drive flyout's row's.</summary>
    private const float ItemIconDip = 20;

    /// <summary>A minute: the watcher follows the folder as it changes, and the tick is the safety net for one that cannot be watched.</summary>
    internal const int IntervalMs = 60_000;

    private readonly Lock gate = new();
    private readonly Gfx gfx;
    private readonly FolderSource source;
    private readonly Action wake;
    private readonly FolderTrail trail = new();

    // The folder shown: its own, or the subfolder gone into (trail.Current).
    private string path = "";
    private bool watching = true;
    private string title = "";

    // The pin it is the contents of, whose own picture heads the line where it has one (§319).
    private Item? pin;
    private FolderContents contents = new() { State = FolderListState.Unset };
    private FolderWatch? watch;
    private int generation;
    private bool started;
    private volatile bool stale;

    // What the pointer is over, and the name of the item it was over: a press acts only where the
    // pointer is, and only on the item it was over — never on one that has taken its place since.
    private int hot = NoTarget;
    private string hotName = "";

    // The row the keys are on, by its target and its item's name, ringed; NoTarget for none. And how many rows
    // the line showed last, which the keys walk.
    private int keyed = NoTarget;
    private string keyedName = "";
    private int laidRows;

    // The row a press is down on, lit pressed (RowPress).
    private RowPress press;

    // The way back at the head of a subfolder gone into, the whole head (BackHead); placed where it is drawn.
    private readonly BackHead backHead = new(() => { }) { AtBounds = true };

    // §498: the pictures' and the videos' thumbnails, read off the dock's thread and kept.
    private readonly FolderThumbnails thumbnails;

    /// <param name="wake">Asks for the pin to be drawn again: something was read, or the pointer moved onto another row.</param>
    public FolderContentsView(Gfx gfx, FolderSource source, Action wake)
    {
        this.gfx = gfx;
        this.source = source;
        this.wake = wake;
        thumbnails = new FolderThumbnails(source, wake);
    }

    /// <summary>§498: how the items are laid out, as File Explorer's View (the line's to say, <see cref="Configure"/>).</summary>
    internal PinFolderView View { get; private set; }

    /// <summary>§498: the thumbnails it shows and has asked for, for tests.</summary>
    internal FolderThumbnails Thumbnails => thumbnails;

    /// <summary>
    /// §498: how large the shell is asked to draw a view's icons and thumbnails, in px: enough for its icon at 200 %, so
    /// an icon at any scale is a picture scaled down; the list's and the details' <see cref="IconPx"/>.
    /// </summary>
    internal static int IconPxOf(PinFolderView view) => view switch
    {
        PinFolderView.Tiles or PinFolderView.MediumIcons => 2 * (int)GridIconDip,
        PinFolderView.LargeIcons => 2 * (int)LargeIconDip,
        _ => IconPx,
    };

    /// <summary>§498: whether a view says what each kind of file is (its type names, <see cref="FolderRequest.Types"/>).</summary>
    private static bool SaysTypes(PinFolderView view) => view is PinFolderView.Details or PinFolderView.Tiles;

    private Gfx Gfx => gfx;

    /// <summary>What was read last, for tests and the menu.</summary>
    internal FolderContents Contents
    {
        get
        {
            lock (gate)
            {
                return contents;
            }
        }
    }

    /// <summary>The folder it shows: the pin's target, or the subfolder gone into.</summary>
    internal string FolderPath => path;

    /// <summary>How many folders deep from its own it shows; 0 for its own.</summary>
    internal int Depth => trail.Depth;

    /// <summary>Whether a subfolder's row goes in (the navigation shows the contents): it wears a chevron. The line's to say.</summary>
    internal bool Navigates { get; set; }

    /// <summary>What the folder shown is called: the pin's name for its own, else the folder's.</summary>
    internal string Title => trail.Depth == 0 && title.Length > 0 ? title : FolderReader.NameOf(path);

    /// <summary>
    /// Shows <paramref name="folder"/> as <paramref name="pinned"/> draws it — its name, and its
    /// picture over the folder's own (<see cref="Item.Icon(int, Func{string, int, Pixels?}, string?)"/>);
    /// a folder other than the one it showed, or another picture, is read afresh — at once, where
    /// <paramref name="read"/> (§380), else at the next look — and its watcher follows it from then on,
    /// where <paramref name="watched"/>, else the minute's tick reads it again.
    /// </summary>
    public void Configure(string folder, Item pinned, bool watched = true, bool read = true, PinFolderView view = PinFolderView.List)
    {
        title = pinned.Name;
        var same = pin?.Picture == pinned.Picture;
        pin = pinned;
        var own = string.Equals(folder, trail.Root, StringComparison.OrdinalIgnoreCase);
        // §498: a view that draws its icons larger, or says the kinds' types where the last did not, reads them afresh;
        // what the line shows stays shown meanwhile, the keys on the item they were on.
        var was = View;
        View = view;
        var redrawn = IconPxOf(was) != IconPxOf(view) || (SaysTypes(view) && !SaysTypes(was));
        var steady = own && watched == watching && same;
        if (steady && (started || !read) && !(redrawn && started))
        {
            if (was != view)
            {
                wake();
            }
            return;
        }
        if (steady && started && Contents is { State: FolderListState.Listed } kept)
        {
            Redraw(kept);
            return;
        }
        // Another folder starts at its own items; the same one stays where it was gone into.
        if (!own)
        {
            trail.Start(folder);
        }
        watching = watched;
        Show(trail.Current, null, read);
    }

    /// <summary>§498: the folder shown read again for another view, what it shows staying until the read comes back.</summary>
    private void Redraw(FolderContents kept)
    {
        lock (gate)
        {
            contents = kept;
        }
        Read();
        wake();
    }

    /// <summary>
    /// Shows <paramref name="folder"/>: what it was left showing (<paramref name="shown"/>) at once, else nothing read
    /// yet; read now where <paramref name="read"/>, and followed from then on in the place of the folder shown before.
    /// </summary>
    private void Show(string folder, FolderContents? shown, bool read = true)
    {
        path = folder;
        watch?.Dispose();
        watch = null;
        started = false;
        lock (gate)
        {
            generation++;
            contents = path.Length == 0 ? new FolderContents { State = FolderListState.Unset } : shown ?? FolderContents.Reading(path);
        }
        (hot, hotName, keyed, keyedName) = (NoTarget, "", NoTarget, "");
        _ = press.Up();
        if (read && path.Length > 0)
        {
            Read();
        }
    }

    /// <summary>
    /// Into the subfolder <paramref name="folder"/> of the folder shown, which was left <paramref name="scroll"/> px
    /// scrolled: its items in their place, the way back at the head; the keys on its first row where they brought it
    /// there (<paramref name="keys"/>).
    /// </summary>
    internal void Into(string folder, bool keys = false, float scroll = 0)
    {
        trail.Into(folder, scroll, Contents);
        Show(folder, null);
        if (keys)
        {
            Key(0);
        }
        wake();
    }

    /// <summary>
    /// Back to the folder the one shown was gone into from, as it was left; the keys on the subfolder left where they
    /// brought it back (<paramref name="keys"/>). False on its own folder, which has no way back.
    /// </summary>
    internal bool Back(bool keys = false) => Back(keys, out _);

    /// <summary><see cref="Back(bool)"/>, and how far the folder gone back to was scrolled as it was left (<paramref name="scroll"/>).</summary>
    internal bool Back(bool keys, out float scroll)
    {
        scroll = 0;
        if (trail.Back() is not var (left, was, shown))
        {
            return false;
        }
        scroll = was;
        Show(trail.Current, shown);
        if (keys)
        {
            var at = Contents.Entries.ToList().FindIndex(entry => string.Equals(entry.Name, left, StringComparison.OrdinalIgnoreCase));
            Key(Math.Max(0, at));
        }
        wake();
        return true;
    }

    /// <summary>
    /// Back on its own folder from however deep, as its folder closes (2026-10-30, the person: "it should show the root
    /// path again after this"): its items as they were left at once, read again as it is next looked at.
    /// </summary>
    internal void Home()
    {
        if (trail.Depth > 0)
        {
            Show(trail.Root, trail.Home(), read: false);
        }
    }

    /// <summary>§357: the folders gone into and what the one shown holds, for its folder to show it so again; null on its own.</summary>
    internal object? Kept() => trail.Depth > 0 ? new Left(trail.Copy(), Contents) : null;

    /// <summary>§357: the folders gone into as <see cref="Kept"/> kept them, the one shown with its items at once; nothing where its own folder is another now.</summary>
    internal void Recall(object kept)
    {
        if (kept is Left left && string.Equals(left.Trail.Root, trail.Root, StringComparison.OrdinalIgnoreCase))
        {
            trail.Take(left.Trail);
            Show(trail.Current, left.Shown);
        }
    }

    private sealed record Left(FolderTrail Trail, FolderContents Shown);

    /// <summary>The row the keys are on, for tests: its target, or <see cref="NoTarget"/>.</summary>
    internal int KeyedTarget => keyed;

    /// <summary>The item of the row the keys are on and its path, what Enter opens (FolderContentsLine.Click); null for none.</summary>
    internal (DriveEntry Entry, string Path)? Keyed() =>
        Entry(keyed) is { } entry && string.Equals(entry.Name, keyedName, StringComparison.Ordinal) ? (entry, FolderReader.PathOf(path, entry.Name)) : null;

    /// <summary>
    /// The keys walk the rows the line shows, down or up: from none the first going down and the last going up; past
    /// either end none, and false: the folder's to move on.
    /// </summary>
    internal bool Walk(bool down) => Walk(0, down ? 1 : -1);

    /// <summary>
    /// §498: the keys walk the items the line shows <paramref name="across"/> items on (Left and Right in a grid) or
    /// <paramref name="down"/> rows on — a row a column's worth of items, so in a grid to the one above or under, and to
    /// the last where the row under is shorter. From none the first going on and the last going back; past either end
    /// none, and false: the folder's to move on.
    /// </summary>
    internal bool Walk(int across, int down)
    {
        var count = Math.Min(laidRows, Contents.Entries.Count);
        var columns = Math.Max(1, laidColumns);
        var at = keyed >= OpenTarget ? keyed - OpenTarget : -1;
        int next;
        if (at < 0)
        {
            next = across + down > 0 ? 0 : count - 1;
        }
        else if (down != 0)
        {
            next = at + (down * columns);
            // Down into a shorter last row: its last item, as File Explorer goes.
            if (down > 0 && next >= count && (at / columns) < ((count - 1) / columns))
            {
                next = count - 1;
            }
        }
        else
        {
            next = at + across;
        }
        if (count == 0 || next < 0 || next >= count)
        {
            Key(-1);
            return false;
        }
        Key(next);
        return true;
    }

    /// <summary>§498: whether the line shows its items in more than one column, where Left and Right walk them rather than go back.</summary>
    internal bool Grid => laidColumns > 1;

    /// <summary>keys: the keys gone to another line, on no row (§471).</summary>
    internal void Unkey() => Key(-1);

    /// <summary>The keys on row <paramref name="index"/>; -1 for none. A row the keys come to is brought into sight (<see cref="Reveal"/>).</summary>
    private void Key(int index)
    {
        var (target, name) = index >= 0 && Entry(OpenTarget + index) is { } entry ? (OpenTarget + index, entry.Name) : (NoTarget, "");
        if ((target, name) == (keyed, keyedName))
        {
            return;
        }
        (keyed, keyedName) = (target, name);
        revealing = target != NoTarget ? index : null;
        wake();
    }

    // The row the keys just came to, to bring into sight once (Reveal); null for none.
    private int? revealing;

    /// <summary>
    /// The row the keys just came to, from the content's top — under the folder's row, a row each — once; then null until the
    /// keys move again: a scrolled line brings it into sight (IListScroll.ScrollReveal).
    /// </summary>
    internal (float Top, float Bottom)? Reveal(in TileStyle style) => Reveal(0, style);

    /// <summary><see cref="Reveal(in TileStyle)"/> for a line <paramref name="width"/> px wide: in a grid, the row of cells the item stands in.</summary>
    internal (float Top, float Bottom)? Reveal(float width, in TileStyle style)
    {
        if (revealing is not { } index)
        {
            return null;
        }
        revealing = null;
        var shape = Shape(View, width, style);
        var top = HeadRow(style) + shape.Over + ((index / shape.Columns) * shape.Row);
        return (top, top + shape.Row);
    }

    /// <summary>
    /// Whether a click on <paramref name="target"/> is the back arrow's: where the pointer is, on a subfolder gone into.
    /// The press is over either way.
    /// </summary>
    internal bool PressedBack(int target)
    {
        if (press.Up())
        {
            wake();
        }
        return target == BackTarget && hot == BackTarget && trail.Depth > 0;
    }

    /// <summary>The folder's name over its path, as a pin to a folder says it.</summary>
    public string Tooltip() => $"{Title}\n{path}";

    /// <summary>The back arrow's tooltip, "Back", as a settings menu's level's head has it, and where the arrow is; null elsewhere.</summary>
    internal (string Text, D2D_RECT_F Part)? Tip(Vector2 point, D2D_RECT_F box, in TileStyle style)
    {
        var shown = Contents;
        var board = LineBoard(box, style, shown, View == PinFolderView.List ? (_, _) => true : (top, bottom) => point.Y < top || point.Y >= bottom);
        if (board.Head is { Arrow: { } arrow } && Inside(arrow, point))
        {
            return (backHead.Name, arrow);
        }
        // §498: an item's name cut short in a view, whole (the list's rows keep theirs as they were).
        foreach (var row in board.Rows)
        {
            if (Inside(row.Box, point) && row.Name is { } name && (uint)row.Index < (uint)shown.Entries.Count
                && !string.Equals(name.Text, shown.Entries[row.Index].Name, StringComparison.Ordinal))
            {
                return (shown.Entries[row.Index].Name, row.Box);
            }
        }
        return null;
    }

    /// <summary>The item an item's target stands for, or null.</summary>
    internal DriveEntry? Entry(int target)
    {
        var at = target - OpenTarget;
        var shown = Contents;
        return target >= OpenTarget && (uint)at < (uint)shown.Entries.Count ? shown.Entries[at] : null;
    }

    /// <summary>
    /// The item a press on <paramref name="target"/> stands for — where the pointer is, and only the
    /// one it was over — and its path; null for anything else, which is the folder itself.
    /// </summary>
    internal (DriveEntry Entry, string Path)? Pressed(int target)
    {
        if (press.Up())
        {
            wake();
        }
        if (target < OpenTarget || target != hot || Entry(target) is not { } entry || !string.Equals(entry.Name, hotName, StringComparison.Ordinal))
        {
            return null;
        }
        return (entry, FolderReader.PathOf(path, entry.Name));
    }

    /// <summary>The item at <paramref name="target"/> and its path, whatever the pointer is on; null for none.</summary>
    internal (DriveEntry Entry, string Path)? ItemAt(int target) =>
        Entry(target) is { } entry ? (entry, FolderReader.PathOf(path, entry.Name)) : null;

    /// <summary>
    /// Reads the folder the first time it is looked at, again once its watcher says it changed, and on
    /// the minute's tick where it has no watcher.
    /// </summary>
    public void Sample(bool everything)
    {
        if (!started || stale || (!everything && watch is not { Watching: true }))
        {
            Read();
        }
    }

    /// <summary>
    /// Asks for what the folder holds, off the dock's thread (FolderSource.Run): first the listing, shown
    /// at once (§380: the names never wait on the shell), then the folder's picture and the items' icons
    /// (<see cref="FolderReader.Pictured"/>), shown as they arrive. What a read from before the folder
    /// changed hands over is dropped.
    /// </summary>
    private void Read()
    {
        started = true;
        stale = false;
        if (path.Length > 0 && watch is null && watching)
        {
            // Followed from the first look on: a change is a read once the folder has settled.
            watch = new FolderWatch(source, path, () =>
            {
                stale = true;
                wake();
            });
        }
        int mine;
        lock (gate)
        {
            mine = ++generation;
        }
        var request = new FolderRequest(path, FolderReader.MostListed, IconPxOf(View), SaysTypes(View));
        var reader = source;
        // A subfolder gone into wears the back arrow at its head, not a picture.
        var pinned = trail.Depth == 0 ? pin : null;
        reader.Run(() => FolderReader.Pictured(request, reader, read => Deliver(mine, read), () => Picture(reader, pinned, request.Path)));
    }

    /// <summary>
    /// The line's picture, by the pins' one precedence: the widget's own picture where it is a file's,
    /// else the folder's own, as the shell draws it; null where there is none, or its own is a glyph
    /// (<see cref="Paint"/> draws it).
    /// </summary>
    private static Pixels? Picture(FolderSource source, Item? pinned, string folder) =>
        pinned is { } item ? item.Icon(IconPx, source.Icon, own: folder) : source.Icon(folder, IconPx);

    /// <summary>A read came back, on whatever thread: it takes the place of what was shown, and the pin is drawn again.</summary>
    private void Deliver(int mine, FolderContents read)
    {
        lock (gate)
        {
            if (mine != generation)
            {
                return;
            }
            contents = read;
        }
        wake();
    }

    public void Dispose()
    {
        watch?.Dispose();
        watch = null;
        lock (gate)
        {
            generation++;
        }
        thumbnails.Dispose();
    }

    // --- How tall a line is ----------------------------------------------------------------------

    /// <summary>
    /// How tall the line is: its own folder's row and a row for each of its own folder's items, at every level (§468,
    /// FolderTrail.Sizing) — a subfolder gone into is shown in that room, scrolling where it holds more (<see cref="ContentHeight"/>).
    /// §498: a row of the view's — a list's or a details' row, a row of tiles or of icons — at <paramref name="width"/>.
    /// </summary>
    public float ListHeight(float width, in TileStyle style) => Tall(trail.Sizing(Contents), width, style);

    /// <summary>What the folder shown lays out at: its row, and a row for each item; held shorter, it scrolls (§461).</summary>
    internal float ContentHeight(in TileStyle style) => ContentHeight(0, style);

    /// <summary><see cref="ContentHeight(in TileStyle)"/> on a line <paramref name="width"/> px wide, as a grid's rows are counted (§498).</summary>
    internal float ContentHeight(float width, in TileStyle style) => Tall(Contents, width, style);

    private float Tall(FolderContents shown, float width, in TileStyle style)
    {
        if (shown.State != FolderListState.Listed)
        {
            return HeadRow(style) + (FolderReader.StateLine(shown).Length > 0 ? ItemRow(style) : 0);
        }
        var shape = Shape(View, width, style);
        var rows = (shown.Entries.Count + shape.Columns - 1) / shape.Columns;
        return HeadRow(style) + shape.Over + (rows * shape.Row) + (rows > 0 ? shape.Under : 0);
    }

    /// <summary>The folder's own row on a line: the list's own row, so a line one row tall is exactly a row of the list.</summary>
    internal static float HeadRow(in TileStyle style) => PopupPaint.Round(40 * style.Scale * style.TextScale);

    /// <summary>
    /// What stays while the items scroll: the folder's row, and §498 the details' column names under it where the folder
    /// shown lists its items (a grid's air scrolls with its cells).
    /// </summary>
    internal float Head(float width, in TileStyle style) =>
        HeadRow(style) + (View == PinFolderView.Details && Contents.State == FolderListState.Listed ? Shape(View, width, style).Over : 0);

    /// <summary>An item's row on a line: the drive flyout's 36 DIP, which follows the text as its rows do.</summary>
    internal static float ItemRow(in TileStyle style) => PopupPaint.Round(36 * style.Scale * style.TextScale);

    // --- §498: the views' measures ------------------------------------------------------------------

    /// <summary>The details' icon, DIP: Explorer's small icon, the kit's row glyph.</summary>
    internal const float DetailsIconDip = 16;

    /// <summary>A tile's and a medium icon's picture, DIP: Explorer's 48.</summary>
    internal const float GridIconDip = 48;

    /// <summary>A large icon's picture, DIP: Explorer's 96.</summary>
    internal const float LargeIconDip = 96;

    /// <summary>The least a tile is wide, DIP: a name and its type beside the icon; as many across as go into the width.</summary>
    internal const float TileDip = 220;

    /// <summary>The least a medium icon's cell is wide, DIP.</summary>
    internal const float MediumCellDip = 84;

    /// <summary>The least a large icon's cell is wide, DIP.</summary>
    internal const float LargeCellDip = 124;

    /// <summary>
    /// How a view stands its items: how many across, how tall a row of them is, and what stands over the first row and
    /// under the last — the details' column names, a grid's air (px).
    /// </summary>
    internal readonly record struct ViewShape(int Columns, float Row, float Over, float Under);

    /// <summary>§498: <paramref name="view"/>'s shape on a line <paramref name="width"/> px wide (any width a single column where it is not known).</summary>
    internal ViewShape Shape(PinFolderView view, float width, in TileStyle style)
    {
        var (inset, scale) = (GridInset(style), style.Scale);
        int Across(float least) => width <= 0 ? 1 : Math.Max(1, (int)MathF.Floor((width - (2 * inset) + 0.5f) / (least * scale)));
        switch (view)
        {
            case PinFolderView.Details:
                return new(1, LineRow.Compact(style), LineRow.Compact(style), 0);
            case PinFolderView.Tiles:
            {
                var across = Across(TileDip);
                var air = across > 1 ? style.Px(LineSpace.TightDip) : 0;
                return new(across, TileRow(style), air, air);
            }
            case PinFolderView.MediumIcons or PinFolderView.LargeIcons:
            {
                var large = view == PinFolderView.LargeIcons;
                return new(Across(large ? LargeCellDip : MediumCellDip), CellRow(style, large ? LargeIconDip : GridIconDip), style.Px(LineSpace.TightDip), style.Px(LineSpace.TightDip));
            }
            default:
                return new(1, ItemRow(style), 0, 0);
        }
    }

    /// <summary>The room a grid keeps at each side of its cells, px.</summary>
    private static float GridInset(in TileStyle style) => style.Px(LineSpace.TightDip);

    /// <summary>A tile's height: its picture with air over and under it, or its three lines with theirs, whichever is taller.</summary>
    private float TileRow(in TileStyle style)
    {
        var lines = TilePaint.LineHeight(Gfx, style.Text(LineType.BodyDip)) + (2 * TilePaint.LineHeight(Gfx, style.Text(LineType.CaptionDip)));
        return PopupPaint.Round(MathF.Max(style.Px(GridIconDip), lines) + (2 * style.Px(10)));
    }

    /// <summary>A grid's cell's height: air, the picture, a gap, two lines of its name, air.</summary>
    private float CellRow(in TileStyle style, float iconDip) =>
        PopupPaint.Round(style.Px(iconDip) + (2 * style.Px(LineSpace.GapDip)) + style.Px(LineSpace.TightDip)
            + (2 * TilePaint.LineHeight(Gfx, style.Text(LineType.CaptionDip))));

    // --- The drawing -----------------------------------------------------------------------------

    /// <summary>
    /// The line: the folder's row and its items, laid out (<see cref="Board"/>); the rows <paramref name="hidden"/> says are out
    /// of sight in a scrolled line (CellIsland.Hidden) are neither laid out nor drawn, so a folder of thousands draws the few that show.
    /// </summary>
    public void PaintCard(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, Func<float, float, bool>? hidden = null)
    {
        var shown = Contents;
        Paint(dc, LineBoard(box, style, shown, hidden), style, shown);
    }

    /// <summary>A line's items, as <see cref="Board"/> laid them out.</summary>
    private void Paint(ID2D1DeviceContext* dc, FolderBoard board, in TileStyle style, FolderContents shown)
    {
        var theme = style.Theme;
        var radius = style.Px(4);
        if (board.Head is { } head)
        {
            // A subfolder gone into: the whole head the way back, lit under the pointer and pressed as the rows under it
            // are, edge to edge (LineItem), Windows' back arrow in the picture's column (BackHead).
            if (head is { Back: { } back, Arrow: { } arrow })
            {
                backHead.Place(back, back, arrow);
                backHead.GlyphSize = LineGlyph.Row(style);
                backHead.Hot = hot == BackTarget;
                backHead.Pressed = press.On(BackTarget);
                backHead.Paint(dc, new QuickContext { Gfx = Gfx, Theme = QuickTheme.For(theme), Scale = style.Scale, TextScale = style.TextScale });
            }
            // The pin's glyph where it chose one, else its picture or the folder's (Picture).
            else if (pin is { PictureIsGlyph: true, Picture: { } glyph })
            {
                PopupPaint.Glyph(dc, Gfx, glyph, head.GlyphPx, theme.Text, head.Icon);
            }
            else if (shown.Picture is { } own)
            {
                PopupPaint.Picture(dc, own, head.Icon, 0);
            }
            else
            {
                PopupPaint.Glyph(dc, Gfx, FolderGlyph, head.GlyphPx, theme.TextSecondary, head.Icon);
            }
            Words(dc, head.Name, theme.Text);
            Words(dc, head.Count, theme.TextSecondary);
        }
        // §498: the details' column names, and the hairline under them.
        foreach (var column in board.Columns)
        {
            Words(dc, column, theme.TextSecondary);
        }
        if (board.Rule is { } rule)
        {
            PopupPaint.Fill(dc, rule, 0, PopupPaint.Ink(theme.Divider));
        }
        foreach (var row in board.Rows)
        {
            var entry = shown.Entries[row.Index];
            // The pointer's plate, as a row of the drive flyout lights; and the row the keys are on, in their ring.
            var over = hot == OpenTarget + row.Index && string.Equals(hotName, entry.Name, StringComparison.Ordinal);
            var ringed = keyed == OpenTarget + row.Index && string.Equals(keyedName, entry.Name, StringComparison.Ordinal);
            if (over || ringed)
            {
                // A band edge to edge in a column; a grid's cell its rounded plate, as File Explorer lights it.
                if (row.Plate is { } plate)
                {
                    PopupPaint.Fill(dc, plate, radius, PopupPaint.Ink(over && press.On(hot) ? theme.SubtlePressed : theme.SubtleHover));
                }
                else
                {
                    LineItem.Light(dc, row.Box, theme, over && press.On(hot));
                }
            }
            if (ringed)
            {
                var around = row.Plate ?? new D2D_RECT_F(row.Box.left + style.Px(4), row.Box.top + style.Px(2), row.Box.right - style.Px(4), row.Box.bottom - style.Px(2));
                TogglesTile.FocusRing(dc, around, radius, style, QuickTheme.For(theme));
            }
            // §498: a picture's or a video's own thumbnail where the view is large enough and the shell has one, else its kind's icon.
            if (row.Thumb && thumbnails.Of(FolderReader.PathOf(path, entry.Name), entry.Modified, IconPxOf(View)) is { } thumbnail)
            {
                PopupPaint.Picture(dc, thumbnail, row.Icon, 0);
            }
            else if (shown.Icons.TryGetValue(DriveListing.IconKey(entry), out var picture))
            {
                PopupPaint.Picture(dc, picture, row.Icon, 0);
            }
            else
            {
                PopupPaint.Glyph(dc, Gfx, entry.Folder ? FolderGlyph : FileGlyph, row.GlyphPx, theme.TextSecondary, row.Icon);
            }
            Words(dc, row.Name, theme.Text);
            Words(dc, row.Date, theme.TextSecondary);
            foreach (var more in row.More ?? [])
            {
                Words(dc, more, theme.TextSecondary);
            }
            if (row.Chevron is { } mark)
            {
                PopupPaint.Glyph(dc, Gfx, Mirror.On ? ChevronBackGlyph : ChevronGlyph, LineGlyph.Inline(style), theme.TextSecondary, mark);
            }
        }
        Words(dc, board.Message, theme.TextSecondary);
    }

    private void Words(ID2D1DeviceContext* dc, BoardWords? words, DXGI_RGBA color)
    {
        if (words is not { } said)
        {
            return;
        }
        if (said.Lines > 1 || said.Centred)
        {
            // §498: a grid's name, in its lines under the picture, each in the cell's middle; cut to them already (Fitted).
            _ = PopupPaint.Wrapped(dc, Gfx, said.Text, said.Px, said.Weight, color, said.Left, said.Top, said.Room, said.Lines, said.Centred);
            return;
        }
        PopupPaint.Text(dc, Gfx, said.Text, said.Px, said.Weight, color, said.Left, said.Top, said.Room);
    }

    // --- The layout ------------------------------------------------------------------------------

    /// <summary>The items laid out in <paramref name="box"/>: the line at its height, every row it holds.</summary>
    internal FolderBoard Board(D2D_RECT_F box, in TileStyle style) => LineBoard(box, style, Contents, null);

    /// <summary>The line laid out for a point: its head, and only the row at <paramref name="point"/>'s height.</summary>
    private FolderBoard BoardAt(Vector2 point, D2D_RECT_F box, in TileStyle style) =>
        LineBoard(box, style, Contents, (top, bottom) => point.Y < top || point.Y >= bottom);

    /// <summary>
    /// A list's line: the folder's row — its picture in the icon column, or the way back on a subfolder gone into,
    /// its name where every name begins and the count against the trailing gutter — and under it, where the line is
    /// taller, its items a row each at the list's own row height, or why there is nothing to list; no "N more items"
    /// (§461): laid out as tall as all of them, the line scrolls (FolderContentsLine, ListScroll.cs). The rows the keys
    /// walk are every one it holds; the rows <paramref name="hidden"/> says are out of sight are not laid out.
    /// </summary>
    private FolderBoard LineBoard(D2D_RECT_F box, in TileStyle style, FolderContents shown, Func<float, float, bool>? hidden)
    {
        var (board, listed) = Lay(box, style, shown, hidden);
        laidRows = listed;
        laidColumns = board.Across;
        return board;
    }

    // §498: how many items across the line showed last, which the keys walk in two dimensions where it is more than one.
    private int laidColumns = 1;

    // The date column's width for the rows of one read at one text size, kept: measured once a read rather than on
    // every hover, a folder of thousands holding few dates.
    private (FolderContents Of, int Listed, float Px, float Width) dates;

    private float DateWidth(FolderContents shown, int listed, float px)
    {
        if (!ReferenceEquals(dates.Of, shown) || dates.Listed != listed || dates.Px != px)
        {
            var width = 0f;
            foreach (var date in shown.Entries.Take(listed).Select(entry => DriveListing.ModifiedOf(entry)).Distinct())
            {
                width = MathF.Max(width, PopupPaint.Measure(Gfx, date, px, Weight.Regular).X);
            }
            dates = (shown, listed, px, width);
        }
        return dates.Width;
    }

    private (FolderBoard Board, int Listed) Lay(D2D_RECT_F box, in TileStyle style, FolderContents shown, Func<float, float, bool>? hidden)
    {
        var form = ListForm.Of(Gfx, style);
        var height = box.bottom - box.top;
        var head = MathF.Min(HeadRow(style), height);
        var board = new FolderBoard();
        var counted = shown.State is FolderListState.Listed or FolderListState.Empty ? FolderReader.CountOf(shown.Count) : FolderReader.StateWord(shown);
        var field = PopupPaint.Measure(Gfx, counted, form.SmallPx, Weight.Regular).X;
        var one = ListTiers.Row(Gfx, PopupPaint.Rect(box.left, box.top, box.right - box.left, head), style, [Title, FolderReader.NameOf(path)], Weight.Semibold, field);
        var small = TilePaint.LineHeight(Gfx, form.SmallPx);
        var headIcon = form.IconBox(box.left, box.top, head);
        D2D_RECT_F? back = null;
        D2D_RECT_F? arrow = null;
        if (trail.Depth > 0)
        {
            // The way back: the whole head, its arrow a square on the picture's column, as tall as the head leaves it.
            var side = MathF.Max(1, MathF.Min(head - style.Px(8), style.Px(32)));
            var middle = (headIcon.left + headIcon.right) / 2;
            back = PopupPaint.Rect(box.left, box.top, box.right - box.left, head);
            arrow = PopupPaint.Rect(MathF.Max(box.left, PopupPaint.Round(middle - (side / 2))), box.top + PopupPaint.Round((head - side) / 2), side, side);
        }
        board.Head = new BoardHead(
            headIcon,
            form.Glyph,
            one.Name is { } name ? new BoardWords(name, form.TextPx, one.NameLeft, one.TextTop, one.NameRoom, Weight.Semibold) : null,
            counted.Length > 0 && one.Field >= field - 0.5f
                ? new BoardWords(counted, form.SmallPx, one.FieldRight - field, PopupPaint.Round(one.Middle - (small / 2)), field)
                : null,
            back,
            arrow);
        var top = box.top + head;
        var room = box.bottom - top;
        var row = ItemRow(style);
        var textLeft = box.left + form.TextLeft;
        var right = box.right - form.Inset;
        if (shown.State != FolderListState.Listed)
        {
            var said = FolderReader.StateLine(shown);
            if (said.Length > 0 && room + 0.5f >= form.Line)
            {
                var slot = MathF.Min(row, room);
                board.Message = new BoardWords(said, form.TextPx, textLeft, top + MathF.Round((slot - form.Line) / 2), right - textLeft);
            }
            return (board, 0);
        }
        // §498: the views other than the list lay out their own items under the same head.
        switch (View)
        {
            case PinFolderView.Details:
                return (board, DetailsRows(board, box, top, style, shown, hidden, form));
            case PinFolderView.Tiles or PinFolderView.MediumIcons or PinFolderView.LargeIcons:
                return (board, GridRows(board, box, top, style, shown, hidden, form));
        }
        // The whole rows the room holds: every one where it is laid out as tall as all of them, as a scrolled line is.
        var listed = Math.Min(shown.Entries.Count, Math.Max(0, (int)MathF.Floor((room + 0.5f) / row)));
        // The rows share what is left of the line, up to the list's own row each, so a line set taller
        // than its rows has its air between them rather than all at its foot.
        if (listed > 0)
        {
            row = MathF.Max(row, MathF.Min(HeadRow(style), MathF.Floor(room / listed)));
        }
        // The date where the names keep their room, in a column every row shares so the dates line
        // up, as the drive flyout keeps it; left out whole where it would squeeze the names.
        var dateWidth = DateWidth(shown, listed, form.SmallPx);
        var gap = style.Px(10);
        // A subfolder that goes in has a chevron at the row's end, as a menu's row with a level; the dates stand before it.
        var mark = Navigates && shown.Entries.Take(listed).Any(entry => entry.Folder) ? LineGlyph.Row(style) : 0;
        var end = right - (mark > 0 ? mark + gap : 0);
        if (end - textLeft - (dateWidth > 0 ? dateWidth + gap : 0) < style.Text(120))
        {
            dateWidth = 0;
        }
        var nameRight = end - (dateWidth > 0 ? dateWidth + gap : 0);
        var icon = MathF.Min(form.Icon, style.Px(ItemIconDip));
        for (var index = 0; index < listed; index++)
        {
            var entry = shown.Entries[index];
            var y = top + (index * row);
            if (hidden is not null && hidden(y, y + row))
            {
                continue;
            }
            var named = ListTiers.Name(Gfx, [entry.Name], form.TextPx, Weight.Regular, nameRight - textLeft);
            var date = DriveListing.ModifiedOf(entry);
            var iconBox = form.IconBox(box.left, y, row);
            board.Rows.Add(new BoardRow(
                index,
                // The row edge to edge, each the next's edge: what lights and takes a press (LineItem).
                LineItem.Band(box, y, y + row),
                PopupPaint.Rect(iconBox.left + MathF.Round((form.Icon - icon) / 2), y + MathF.Round((row - icon) / 2), icon, icon),
                MathF.Round(icon * 0.8f),
                named is null ? null : new BoardWords(named, form.TextPx, textLeft, y + MathF.Round((row - form.Line) / 2), nameRight - textLeft),
                dateWidth > 0 && date.Length > 0
                    ? new BoardWords(date, form.SmallPx, end - PopupPaint.Measure(Gfx, date, form.SmallPx, Weight.Regular).X, y + MathF.Round((row - small) / 2), dateWidth)
                    : null,
                mark > 0 && entry.Folder ? PopupPaint.Rect(right - mark, y + MathF.Round((row - mark) / 2), mark, mark) : null));
        }
        return (board, listed);
    }

    // --- §498: the details ---------------------------------------------------------------------------

    /// <summary>When an entry last changed as the details' column says it: the region's short date and its time; empty where not known.</summary>
    internal static string ModifiedAt(in DriveEntry entry) => entry.Modified == default
        ? ""
        : $"{DriveListing.ModifiedOf(entry)} {Panels.Nls.Time(entry.Modified)}";

    /// <summary>How large a file is, as the details' and the tiles' Size says it ("12.5 KB"); nothing for a folder.</summary>
    internal static string SizeOf(in DriveEntry entry) => entry.Folder ? "" : Units.Size(entry.Size);

    // The details' columns' widths for the rows of one read at one text size, kept as the dates' are.
    private (FolderContents Of, int Listed, float Px, float Date, float Type, float Size) columns;

    private (float Date, float Type, float Size) ColumnWidths(FolderContents shown, int listed, float px, in TileStyle style)
    {
        if (!ReferenceEquals(columns.Of, shown) || columns.Listed != listed || columns.Px != px)
        {
            float Widest(string title, IEnumerable<string> said) =>
                said.Distinct().Prepend(title).Max(words => PopupPaint.Measure(Gfx, words, px, Weight.Regular).X);
            var items = shown.Entries.Take(listed).ToList();
            var date = Widest(S.DockFolderDetailsModified, items.Select(entry => ModifiedAt(entry)));
            // A type as long as Windows' longest names is cut rather than given the line.
            var type = MathF.Min(Widest(S.DockFolderDetailsType, items.Select(entry => FolderReader.TypeOf(entry, shown))), style.Text(160));
            var size = Widest(S.DockFolderDetailsSize, items.Select(entry => SizeOf(entry)));
            columns = (shown, listed, px, MathF.Ceiling(date), MathF.Ceiling(type), MathF.Ceiling(size));
        }
        return (columns.Date, columns.Type, columns.Size);
    }

    /// <summary>
    /// The details (§498): under the folder's row a row of the column names and a hairline, then a compact row an item —
    /// its 16-DIP icon in the list's icon column, its name where every name begins, and the columns the width keeps beside
    /// a name of at least 120 DIP: the date first, then the size, then the type, across in Explorer's order, the size
    /// against the trailing gutter. The rows <paramref name="hidden"/> says are out of sight are not laid out.
    /// </summary>
    private int DetailsRows(FolderBoard board, D2D_RECT_F box, float top, in TileStyle style, FolderContents shown, Func<float, float, bool>? hidden, ListForm form)
    {
        var shape = Shape(PinFolderView.Details, box.right - box.left, style);
        var room = box.bottom - top;
        if (room + 0.5f < shape.Over)
        {
            return 0;
        }
        var listed = Math.Min(shown.Entries.Count, Math.Max(0, (int)MathF.Floor((room - shape.Over + 0.5f) / shape.Row)));
        var textLeft = box.left + form.TextLeft;
        var right = box.right - form.Inset;
        var gap = style.Px(LineSpace.SectionDip);
        var (dateWidth, typeWidth, sizeWidth) = ColumnWidths(shown, listed, form.SmallPx, style);
        var least = style.Text(120);
        var used = 0f;
        bool Keeps(float width)
        {
            if (right - textLeft - used - width - gap < least)
            {
                return false;
            }
            used += width + gap;
            return true;
        }
        var date = Keeps(dateWidth);
        var size = Keeps(sizeWidth);
        var type = Keeps(typeWidth);
        // From the trailing gutter back: the size, the type, the date; the name has the rest.
        var x = right;
        var sizeLeft = size ? x - sizeWidth : x;
        x = size ? sizeLeft - gap : x;
        var typeLeft = type ? x - typeWidth : x;
        x = type ? typeLeft - gap : x;
        var dateLeft = date ? x - dateWidth : x;
        x = date ? dateLeft - gap : x;
        var nameRight = x;

        // The column names, in the caption's ink, and the hairline under them.
        var small = form.SmallLine;
        var titleTop = top + PopupPaint.Round((shape.Over - small) / 2);
        BoardWords Title(string words, float left, float width) => new(ListTiers.Name(Gfx, [words], form.SmallPx, Weight.Regular, width) ?? "", form.SmallPx, left, titleTop, width);
        board.Columns.Add(Title(S.DockFolderDetailsName, textLeft, nameRight - textLeft));
        if (date)
        {
            board.Columns.Add(Title(S.DockFolderDetailsModified, dateLeft, dateWidth));
        }
        if (type)
        {
            board.Columns.Add(Title(S.DockFolderDetailsType, typeLeft, typeWidth));
        }
        if (size)
        {
            var title = S.DockFolderDetailsSize;
            board.Columns.Add(new(title, form.SmallPx, right - PopupPaint.Measure(Gfx, title, form.SmallPx, Weight.Regular).X, titleTop, sizeWidth));
        }
        var hair = MathF.Max(1, PopupPaint.Round(style.Scale));
        board.Rule = PopupPaint.Rect(box.left + form.Inset, top + shape.Over - hair, box.right - box.left - (2 * form.Inset), hair);

        var icon = style.Px(DetailsIconDip);
        for (var index = 0; index < listed; index++)
        {
            var entry = shown.Entries[index];
            var y = top + shape.Over + (index * shape.Row);
            if (hidden is not null && hidden(y, y + shape.Row))
            {
                continue;
            }
            var middle = y + MathF.Round((shape.Row - small) / 2);
            BoardWords? Column(bool kept, string said, float left, float width, bool trailing = false) =>
                !kept || said.Length == 0 ? null
                : trailing ? new BoardWords(said, form.SmallPx, right - PopupPaint.Measure(Gfx, said, form.SmallPx, Weight.Regular).X, middle, width)
                : ListTiers.Name(Gfx, [said], form.SmallPx, Weight.Regular, width) is { } cut ? new BoardWords(cut, form.SmallPx, left, middle, width) : null;
            var named = ListTiers.Name(Gfx, [entry.Name], form.TextPx, Weight.Regular, nameRight - textLeft);
            var iconBox = form.IconBox(box.left, y, shape.Row);
            List<BoardWords> more = [];
            if (Column(type, FolderReader.TypeOf(entry, shown), typeLeft, typeWidth) is { } typed)
            {
                more.Add(typed);
            }
            if (Column(size, SizeOf(entry), sizeLeft, sizeWidth, trailing: true) is { } sized)
            {
                more.Add(sized);
            }
            board.Rows.Add(new BoardRow(
                index,
                LineItem.Band(box, y, y + shape.Row),
                PopupPaint.Rect(iconBox.left + MathF.Round((form.Icon - icon) / 2), y + MathF.Round((shape.Row - icon) / 2), icon, icon),
                MathF.Round(icon * 0.8f),
                named is null ? null : new BoardWords(named, form.TextPx, textLeft, y + MathF.Round((shape.Row - form.Line) / 2), nameRight - textLeft),
                Column(date, ModifiedAt(entry), dateLeft, dateWidth),
                More: more));
        }
        return listed;
    }

    // --- §498: tiles and icons -----------------------------------------------------------------------

    /// <summary>
    /// Tiles and icons (§498): the items in a grid of as many across as the view's least width goes into the line's, the
    /// cells touching, each row of them as tall as the view's. One across — a narrow line of tiles — is a list's band
    /// edge to edge, the picture on the gutter; more light a rounded plate inside the cell. A tile has its 48-DIP picture
    /// and its name, type and size beside it; an icon its picture over its name in two lines at most, in the cell's
    /// middle. A picture or a video wears its thumbnail (<see cref="BoardRow.Thumb"/>).
    /// </summary>
    private int GridRows(FolderBoard board, D2D_RECT_F box, float top, in TileStyle style, FolderContents shown, Func<float, float, bool>? hidden, ListForm form)
    {
        var width = box.right - box.left;
        var shape = Shape(View, width, style);
        board.Across = shape.Columns;
        var room = box.bottom - top;
        var rows = Math.Max(0, (int)MathF.Floor((room - shape.Over + 0.5f) / shape.Row));
        var listed = Math.Min(shown.Entries.Count, rows * shape.Columns);
        var single = shape.Columns == 1;
        var inset = single ? 0 : GridInset(style);
        var cellWidth = (width - (2 * inset)) / shape.Columns;
        var tiles = View == PinFolderView.Tiles;
        var icon = style.Px(View == PinFolderView.LargeIcons ? LargeIconDip : GridIconDip);
        var small = form.SmallLine;
        for (var index = 0; index < listed; index++)
        {
            var (across, down) = (index % shape.Columns, index / shape.Columns);
            var y = top + shape.Over + (down * shape.Row);
            if (hidden is not null && hidden(y, y + shape.Row))
            {
                continue;
            }
            var entry = shown.Entries[index];
            var left = PopupPaint.Round(box.left + inset + (across * cellWidth));
            var right = across == shape.Columns - 1 ? box.right - inset : PopupPaint.Round(box.left + inset + ((across + 1) * cellWidth));
            var cell = single ? LineItem.Band(box, y, y + shape.Row) : PopupPaint.Rect(left, PopupPaint.Round(y), right - left, PopupPaint.Round(shape.Row));
            var plate = single ? (D2D_RECT_F?)null : LineItem.Inside(cell, style.Px(LineSpace.HairDip));
            var thumb = FolderThumbnails.Shows(entry);
            if (tiles)
            {
                // The picture on the gutter (a cell's own air in a grid), the three lines beside it in the block's middle.
                var pictureLeft = single ? box.left + form.Inset : left + style.Px(LineSpace.GapDip + LineSpace.HairDip);
                var pictureBox = PopupPaint.Rect(pictureLeft, y + MathF.Round((shape.Row - icon) / 2), icon, icon);
                var textLeft = pictureBox.right + style.Px(12);
                var textRoom = MathF.Max(1, (single ? box.right - form.Inset : right - style.Px(LineSpace.GapDip)) - textLeft);
                string[] under = entry.Folder ? [FolderReader.TypeOf(entry, shown)] : [FolderReader.TypeOf(entry, shown), SizeOf(entry)];
                var block = form.Line + (under.Length * small);
                var lineTop = y + MathF.Round((shape.Row - block) / 2);
                var named = ListTiers.Name(Gfx, [entry.Name], form.TextPx, Weight.Regular, textRoom);
                List<BoardWords> more = [];
                for (var line = 0; line < under.Length; line++)
                {
                    if (under[line].Length > 0 && ListTiers.Name(Gfx, [under[line]], form.SmallPx, Weight.Regular, textRoom) is { } cut)
                    {
                        more.Add(new BoardWords(cut, form.SmallPx, textLeft, lineTop + form.Line + (line * small), textRoom));
                    }
                }
                board.Rows.Add(new BoardRow(index, cell, pictureBox, MathF.Round(icon * 0.6f),
                    named is null ? null : new BoardWords(named, form.TextPx, textLeft, lineTop, textRoom),
                    null, More: more, Plate: plate, Thumb: thumb));
                continue;
            }
            // An icon: its picture over its name, both in the cell's middle.
            var middle = (cell.left + cell.right) / 2;
            var iconBox = PopupPaint.Rect(PopupPaint.Round(middle - (icon / 2)), y + style.Px(LineSpace.GapDip), icon, icon);
            var nameRoom = MathF.Max(1, cell.right - cell.left - (2 * style.Px(LineSpace.TightDip + LineSpace.HairDip)));
            var (fitted, _) = PopupPaint.Fit(Gfx, entry.Name, form.SmallPx, Weight.Regular, nameRoom, 2);
            board.Rows.Add(new BoardRow(index, cell, iconBox, MathF.Round(icon * 0.6f),
                new BoardWords(fitted, form.SmallPx, PopupPaint.Round(middle - (nameRoom / 2)), iconBox.bottom + style.Px(LineSpace.TightDip), nameRoom, Lines: 2, Centred: true),
                null, Plate: plate, Thumb: thumb));
        }
        return listed;
    }

    // --- The pointer -----------------------------------------------------------------------------

    /// <summary>An item's row is that item, a subfolder's head the way back; anywhere else is nothing.</summary>
    public int CardTarget(Vector2 point, D2D_RECT_F box, in TileStyle style) =>
        BoardAt(point, box, style).TargetAt(point);

    /// <summary>The row under the pointer lights, as a row of the drive flyout does; the folder draws it again.</summary>
    public void CardHover(Vector2? point, D2D_RECT_F box, in TileStyle style)
    {
        var target = point is { } at ? BoardAt(at, box, style).TargetAt(at) : NoTarget;
        var name = Entry(target)?.Name ?? "";
        var let = press.Over(target);
        // The pointer moving takes the keys' ring away, as it does from a menu's row.
        var unkeyed = point is not null && keyed != NoTarget;
        if (target == hot && name == hotName && !let && !unkeyed)
        {
            return;
        }
        (hot, hotName) = (target, name);
        if (unkeyed)
        {
            (keyed, keyedName) = (NoTarget, "");
        }
        wake();
    }

    /// <summary>A press on an item's row lights it pressed; the press stays the folder's, which makes it the click (RowPress).</summary>
    public void Press(Vector2 point, D2D_RECT_F box, in TileStyle style)
    {
        var target = BoardAt(point, box, style).TargetAt(point);
        if (press.Down(target >= OpenTarget || target == BackTarget ? target : NoTarget))
        {
            wake();
        }
    }

    /// <summary>Which item's row is lit under the pointer, for tests: its target, or <see cref="NoTarget"/>.</summary>
    internal int Hot => hot;

    private static bool Inside(D2D_RECT_F rect, Vector2 point) =>
        point.X >= rect.left && point.X < rect.right && point.Y >= rect.top && point.Y < rect.bottom;

    // --- The layout's pieces ---------------------------------------------------------------------

    /// <summary>A line's layout (<see cref="Board"/>): what it is drawn from and what a press is tested against.</summary>
    internal sealed class FolderBoard
    {
        /// <summary>The folder's own row: its picture, its name and how many items it holds.</summary>
        public BoardHead? Head { get; set; }

        /// <summary>The items shown, from the first, in the order they were read.</summary>
        public List<BoardRow> Rows { get; } = [];

        /// <summary>Why there is nothing to list: empty, missing, not allowed; null where there are items.</summary>
        public BoardWords? Message { get; set; }

        /// <summary>§498: the details' column names over the items; none in any other view.</summary>
        public List<BoardWords> Columns { get; } = [];

        /// <summary>§498: the hairline under the details' column names; null in any other view.</summary>
        public D2D_RECT_F? Rule { get; set; }

        /// <summary>§498: how many items stand across: more than one in a grid, which the keys walk in two dimensions.</summary>
        public int Across { get; set; } = 1;

        /// <summary>What a press at <paramref name="point"/> is: the way back (the whole head), an item's row, or nothing of the line's own.</summary>
        public int TargetAt(Vector2 point)
        {
            if (Head is { Back: { } back } && Inside(back, point))
            {
                return BackTarget;
            }
            foreach (var row in Rows)
            {
                if (Inside(row.Box, point))
                {
                    return OpenTarget + row.Index;
                }
            }
            return NoTarget;
        }
    }

    /// <summary>
    /// The folder's own row: its picture's box and its glyph's size there, its name and its count; on a subfolder gone
    /// into, the way back — the whole row — and the back arrow's box in it.
    /// </summary>
    internal readonly record struct BoardHead(D2D_RECT_F Icon, float GlyphPx, BoardWords? Name, BoardWords? Count, D2D_RECT_F? Back = null, D2D_RECT_F? Arrow = null);

    /// <summary>
    /// One item's row: its band (LineItem), what lights and takes a press, the picture's box and the glyph's size in it, its
    /// name, its date, and a subfolder's chevron where it goes in. §498: the view's further words (the details' type and
    /// size, a tile's type and size); a grid's cell's rounded plate, what lights in place of the band; and whether it wears
    /// its thumbnail where the shell has one.
    /// </summary>
    internal readonly record struct BoardRow(int Index, D2D_RECT_F Box, D2D_RECT_F Icon, float GlyphPx, BoardWords? Name, BoardWords? Date, D2D_RECT_F? Chevron = null,
        IReadOnlyList<BoardWords>? More = null, D2D_RECT_F? Plate = null, bool Thumb = false);

    /// <summary>
    /// A line of words with its line box's top-left and the room it may take, cut with an ellipsis past it; §498: or in up
    /// to <paramref name="Lines"/> lines, each in the room's middle where <paramref name="Centred"/> (a grid's name).
    /// </summary>
    internal readonly record struct BoardWords(string Text, float Px, float Left, float Top, float Room, Weight Weight = Weight.Regular, int Lines = 1, bool Centred = false);
}
