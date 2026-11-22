// A divider in a list folder: where a list splits in two (2026-10-05: "the divider in between widgets
// should in list view should not be there per default, you have to add it deliberately and can also
// move it around then").
//
// A list used to draw a hairline between every two of its lines, whatever they were. It draws none of
// its own now, as the bar draws no separator of its own: a split between two widgets is one somebody
// put there, from the list's "+" ("Divider", under Folders, with the heading), and it is a line of the
// list like any other — carried to another place in edit mode, selected, removed with Delete or its
// menu, kept in the file in its order.
//
// It is a folder separator, and **empty space** (2026-10-09: "a divider inside a list splits that list's
// panel background into separate cards with a gap (like a taskbar separator splits widget groups). NO
// line any more"; §505, 2026-11-05: "the spacers should always just be empty space and not a line or dot
// or anything" — §307 to §502 it could be a line or a dot too): the lines over it stand on one card of
// the list's material and the lines under it on another, its room apart (FolderFlyout.SeparatorRoom),
// or it grows (§435). The folder lays that out (FolderFlyout.Build, FolderListPlan.Runs); the host draws
// the cards (FlyoutHost.Pieces). The tile itself draws nothing; edit mode stripes its whole room, moving
// (§524: FolderPlan.Bands, SpacerStripes), taken hold of over a grab at least as tall. A click on it does nothing and it never lights
// under the pointer (FolderFlyout.Quiet): it is not a thing to press.

using Karpo.Core;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock;

internal sealed unsafe class DividerTile : CellIsland
{
    public DividerTile(string key, WidgetItem island, Gfx gfx, Config config)
        : base(key, island, gfx)
    {
        Configure(island, config);
    }

    /// <summary>Nothing it shows ever changes.</summary>
    public override int IntervalMs => 0;

    protected override int Count => 1;

    public override void Configure(WidgetItem island, Config config)
    {
        Settings = island;
        Forget();
    }

    public override string? Tooltip(int index) => null;

    /// <summary>A divider is not a thing to press: a click on it does nothing, and the folder stays as it is.</summary>
    public override IslandAction Click(int index) => default;

    protected override bool Sample(bool everything) => false;

    /// <summary>A gap draws nothing.</summary>
    internal override void PaintCell(ID2D1DeviceContext* dc, int index, in IslandStyle style, float width, float height)
    {
    }

    /// <summary>A gap draws nothing: the cards either side of it are the folder's.</summary>
    protected internal override void PaintCard(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
    }
}
