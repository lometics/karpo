// folder contents: what one folder on this PC holds, as a line of a list (§380; the person, 2026-10-28:
// "lets split folder and pin again. so: a pin always only shows the link to a webpage, application,
// folder, etc. and opens the location on click. a folder always shows the contents of the folder and
// inherits the navigation properties etc. from the pin widget"). It draws through FolderContentsView, as
// a pin set to show its folder's contents did from §32 to §380. A line alone: its row of buttons on the
// taskbar went (§492).
//
// A file in it opens with its own program, a subfolder as its navigation says — File Explorer, or its items
// in place of the folder's, the line the same line, its whole head the way back (FolderContentsView, In
// place: 2026-10-29, the person: "the folder widget should also change the contents inline on navigation
// and not actually open the submenus on the left or the right") — and anywhere else nothing (2026-10-30,
// the person: "the folder opens in file explorer when i click somewhere inside of it. this should not be"):
// the folder shown opens in File Explorer from its right click. Going in and back opens nothing: the
// folder stays open. Whatever it opens is an app opening, as every other line's is (IslandView.App): the folder it stands in goes first, and only then is it opened
// (Dock.FolderForward, Open) — so File Explorer comes up in front, never behind the folder, which used to
// stay open over it (the person: "when i click to open a system folder in file explorer the file
// explorer opens behind the karpo folder and the karpo folder does not close").
//
// A folder of more items than its line has rows scrolls them under its own row, which stays (ListScroll.cs; the
// person, 2026-10-31: "when a folder has more items to show it should actually not say that but just scroll"):
// every item it holds is a row, up to the most a read lists (FolderReader.MostListed), and none says "N more" (§461).
// Gone into a subfolder, it stays its own folder's height (§468, 2026-11-01, the person: "it should not change its
// height dependent on level / contents except for the root level of course"): the subfolder's items from the top of
// that room, scrolling where there are more; back out, the folder as far scrolled as it was left.

using System.Numerics;
using Karpo.Core;
using Karpo.Dock.Drives;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;

namespace Karpo.Dock;

internal sealed unsafe class FolderContentsLine : CellIsland, IListContent, IListScroll
{
    private readonly FolderContentsView contents;
    private PinOptions options = new();

    // The folder its target names, as a path; empty for none.
    private string folder = "";

    // Whether its folder is on a network (read only while shown), and whether the open folder shows it.
    private bool remote;
    private bool shown;

    // What the last click asked to open, once the folder has gone (Open); null for nothing.
    private (string Path, bool Folder)? opening;

    public FolderContentsLine(string key, WidgetItem island, Gfx gfx, Config config)
        : base(key, island, gfx)
    {
        // §643: a ghost's reads the samples' made-up share as it reads any folder.
        contents = new FolderContentsView(gfx, Sampled ? Samples.Folders(FolderSource.Current) : FolderSource.Current, () => _ = Wake());
        Configure(island, config);
    }

    /// <summary>Its watcher follows the folder; the minute's tick reads one it cannot watch.</summary>
    public override int IntervalMs => FolderContentsView.IntervalMs;

    /// <summary>What it points at, resolved as a pin's target is: the name and the picture its head row wears.</summary>
    public Item Item { get; private set; } = PinItems.For(null);

    protected override int Count => 1;

    public override void Configure(WidgetItem island, Config config)
    {
        Settings = island;
        options = island.Pin ?? new PinOptions();
        Item = PinItems.For(options);
        // One on a network (a drive preset's share, §119) is never asked whether it is there on the
        // dock's thread, which would wait on the network; its listing, read off the thread, says it is
        // missing where it is, and it is never watched but read on the minute while the open folder
        // shows it, as the drive flyout read a share only while open.
        remote = PinFolders.OnNetwork(options.Target, RemoteDrive);
        folder = PinFolders.PathOf(options.Target);
        contents.Navigates = Navigation.ShowsContents();
        contents.Configure(folder, Item, watched: !remote, read: !remote || shown, view: PinFolders.ViewOf(island));
        Forget();
    }

    /// <summary>Whether the drive at <paramref name="root"/> ("X:\") is mapped to a share: the redirector's answer, never the share's.</summary>
    private static bool RemoteDrive(string root)
    {
        try
        {
            return new DriveInfo(root).DriveType == DriveType.Network;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>The folder its target names, as a path; empty for none.</summary>
    internal string Folder => folder;

    /// <summary>What the folder holds, and how it is drawn.</summary>
    internal FolderContentsView Contents => contents;

    /// <summary>What a click on a subfolder does (<see cref="PinFolders.NavigationOf"/>).</summary>
    internal PinFolderNavigation Navigation => PinFolders.NavigationOf(Settings);

    public override string? Tooltip(int index) => contents.Tooltip();

    /// <summary>
    /// An item's row opens that item — where the pointer is, and only the one it was over; with the keys, the row they
    /// are on — a file with its own program, a subfolder in File Explorer or, where the navigation shows the contents,
    /// in place of the items (FolderContentsView.Into); the head of a subfolder gone into goes back; anywhere else nothing.
    /// What opens is an app opening, which the folder carries out once it has gone (<see cref="Open"/>); going in and
    /// back is the line's own, and the folder stays open.
    /// </summary>
    public override IslandAction Click(int index)
    {
        opening = null;
        if (index == FolderContentsView.BackTarget)
        {
            if (contents.PressedBack(index))
            {
                _ = Back(keys: false);
            }
            return default;
        }
        var item = index >= FolderContentsView.OpenTarget ? contents.Pressed(index) : index == FolderContentsView.NoTarget ? contents.Keyed() : null;
        if (item is { Entry.Folder: true } subfolder && Navigation.ShowsContents())
        {
            Into(subfolder.Path, keys: index == FolderContentsView.NoTarget);
            return default;
        }
        opening = item is { } chosen ? (chosen.Path, chosen.Entry.Folder) : null;
        return opening is null ? default : new(View: IslandView.App);
    }

    /// <summary>keys: the line walks its items, into a folder and back itself (CellIsland.WalksItsOwnKeys).</summary>
    protected internal override bool WalksItsOwnKeys => true;

    /// <summary>keys: the row its keys are on.</summary>
    protected internal override int? OwnKeyedTarget => contents.KeyedTarget is var keyed && keyed != FolderContentsView.NoTarget ? keyed : 0;

    /// <summary>keys: the row its keys are on is ringed (one ring, §471).</summary>
    protected internal override bool RingsOwnKeys => contents.Keyed() is not null;

    /// <summary>keys: gone to another line, the row they were on is ringed no more.</summary>
    protected internal override void OwnKeys(bool on)
    {
        if (!on)
        {
            contents.Unkey();
        }
    }

    /// <summary>
    /// The keys, with the line's focus: Up and Down walk its rows, past either end the folder's; Enter on a subfolder
    /// that goes in goes in, and on anything else is the folder's click (<see cref="Click"/> with no part: the row the
    /// keys are on); Backspace and Left — Right where the line is mirrored — go back, as a settings menu's level does.
    /// §498: in a grid of tiles or icons, Left and Right walk the items (the other way round where mirrored, as the grid
    /// is), Up and Down to the one above and under; Backspace goes back.
    /// </summary>
    protected internal override bool CardKey(int key, WidgetSurface surface)
    {
        switch (key)
        {
            case VK.VK_DOWN or VK.VK_UP:
                return contents.Walk(0, key == VK.VK_DOWN ? 1 : -1);
            case VK.VK_LEFT or VK.VK_RIGHT when contents.Grid:
                return contents.Walk((key == VK.VK_RIGHT) != Mirror.On ? 1 : -1, 0);
            case VK.VK_RETURN or VK.VK_SPACE when contents.Keyed() is { Entry.Folder: true } subfolder && Navigation.ShowsContents():
                Into(subfolder.Path, keys: true);
                return true;
            case VK.VK_BACK:
            case VK.VK_LEFT when !Mirror.On:
            case VK.VK_RIGHT when Mirror.On:
                return Back(keys: true);
            default:
                return false;
        }
    }

    /// <summary>
    /// Into a subfolder, from its top: the line stays its own folder's height (§468), so a subfolder of more items
    /// scrolls them in it, and the folder left keeps how far it was scrolled for the way back.
    /// </summary>
    private void Into(string path, bool keys)
    {
        contents.Into(path, keys, ListScroll);
        ListScroll = 0;
        // §489: the subfolder's items come in from the end, as a menu's level does.
        Swapped(+1);
    }

    /// <summary>Back to the folder the one shown was gone into from, scrolled as it was left.</summary>
    private bool Back(bool keys)
    {
        if (!contents.Back(keys, out var scroll))
        {
            return false;
        }
        ListScroll = scroll;
        // §489: and back from the start.
        Swapped(-1);
        return true;
    }

    /// <summary>The back arrow's "Back", as a settings menu's level's head says it.</summary>
    protected internal override (string Text, D2D_RECT_F Part)? CardTip(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface) =>
        contents.Tip(point, box, style);

    /// <summary>Opens what the last click asked for (<see cref="Click"/>), once; nothing where it asked for nothing.</summary>
    internal void Open()
    {
        if (opening is { } chosen)
        {
            opening = null;
            FolderSource.Current.Open(chosen.Path, chosen.Folder);
        }
    }

    protected override bool Sample(bool everything)
    {
        // A folder on a network only while the open folder shows it: it is read again on every tick.
        if (!remote || shown)
        {
            contents.Sample(everything);
        }
        return false;
    }

    /// <summary>
    /// The open folder shows the line, or no longer does: a folder on a network is read from now on, or not; and a
    /// subfolder gone into is this look's, the line on its own folder again as its folder closes (FolderContentsView.Home).
    /// </summary>
    public override void Visible(bool shown)
    {
        this.shown = shown;
        if (!shown)
        {
            contents.Home();
        }
    }

    /// <summary>§357: the subfolder gone into, kept where its folder is handed on or left in edit mode.</summary>
    internal override object? ViewMemory() => contents.Kept();

    internal override void RecallView(object memory) => contents.Recall(memory);

    /// <summary>A line as tall as its own folder's items, at every level (§468).</summary>
    public float ListHeight(float width, in TileStyle style) => contents.ListHeight(width, style);

    /// <summary>All of the items of the folder shown, where the line has room for its own row and one of them: the rest scrolls (ListScroll.cs).</summary>
    public float ScrollHeight(float width, float height, in TileStyle style) =>
        height + 0.5f < FolderContentsView.HeadRow(style) + FolderContentsView.ItemRow(style) ? 0 : contents.ContentHeight(width, style);

    /// <summary>
    /// The folder's own row — its picture, its name and count, or the way back — stays while the items scroll; §498: and
    /// the details' column names under it.
    /// </summary>
    public float ScrollHead(float width, float content, in TileStyle style) => contents.Head(width, style);

    /// <summary>The row the keys just came to, brought into sight.</summary>
    public (float Top, float Bottom)? ScrollReveal(float width, in TileStyle style) => contents.Reveal(width, style);

    /// <summary>Only the row under the pointer lights, never the line around the rows.</summary>
    protected internal override bool LightsItsRows => true;

    public override void Dispose()
    {
        contents.Dispose();
        base.Dispose();
    }

    /// <summary>A list's alone: never on a bar (§492). Its glyph, should anything ask.</summary>
    internal override void PaintCell(ID2D1DeviceContext* dc, int index, in IslandStyle style, float width, float height)
    {
        var side = MathF.Min(style.Icon, MathF.Min(width, height));
        PopupPaint.Glyph(dc, Gfx, Info.Glyph, side, new IslandInk(style.Look).Text, PopupPaint.Rect((width - side) / 2, (height - side) / 2, side, side));
    }

    protected internal override unsafe void PaintCard(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, WidgetSurface surface) =>
        contents.PaintCard(dc, box, style, Hidden);

    protected internal override int CardTarget(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface) =>
        contents.CardTarget(point, box, style);

    /// <summary>The row under the pointer lights.</summary>
    protected internal override void CardHover(Vector2? point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface) =>
        contents.CardHover(point, box, style);

    /// <summary>A press on an item's row lights it pressed; the press stays the folder's, which makes it the click.</summary>
    protected internal override void CardDown(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface) =>
        contents.Press(point, box, style);
}
