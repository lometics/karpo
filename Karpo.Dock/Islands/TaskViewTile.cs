// Task view in a folder: a line of a list (the person, 2026-09-27: "add a
// shortcut widget to the windows windows and desktop overview as well. to the taskbar and to the
// folder views. (same design as the other one icon designs.)").
//
// On the bar the scene draws Task view itself, as one of Windows' own glyph buttons beside Start's
// and search's; inside a folder it is a widget like the others, so it is an island of its own here,
// as a pin is. A click on it does what the bar's button does, through the same seam: it presses
// Win+Tab (Desktops.TaskView). That is an act, not a popup of the folder's — Task view takes the
// whole screen and is where a window or a desktop is chosen — so the line declares
// CellAct.Action, and the folder may close as Task view takes the screen (docs\widgets.md §15.1).
//
// Its line is the list's own row of its glyph and its name (FolderFlyout.PaintListLine).

using System.Numerics;
using Karpo.Core;
using Karpo.Dock.Native;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock;

internal sealed unsafe class TaskViewTile : CellIsland
{
    public TaskViewTile(string key, WidgetItem island, Gfx gfx, Config config)
        : base(key, island, gfx)
    {
    }

    /// <summary>Nothing it shows changes.</summary>
    public override int IntervalMs => 0;

    protected override int Count => 1;

    public override void Configure(WidgetItem island, Config config) => Settings = island;

    public override string? Tooltip(int index) => Info.Title;

    /// <summary>
    /// Task view, as the bar's button opens it: Win+Tab, through the one seam every Task view button
    /// presses (<see cref="Desktops.TaskView"/>). The widget acts itself and hands the folder nothing
    /// to carry out, which leaves the folder to close as Task view takes the screen.
    /// </summary>
    public override IslandAction Click(int index)
    {
        Desktops.TaskView();
        return default;
    }

    protected override bool Sample(bool everything) => false;

    /// <summary>
    /// Its glyph in the middle of the cell, as the bar's own button draws it.
    /// </summary>
    internal override void PaintCell(ID2D1DeviceContext* dc, int index, in IslandStyle style, float width, float height)
    {
        var fit = new Fit(style);
        var ink = new IslandInk(style.Look);
        var glyph = fit.Glyph();
        dc->SetTextAntialiasMode(D2D1_TEXT_ANTIALIAS_MODE.D2D1_TEXT_ANTIALIAS_MODE_GRAYSCALE);
        PopupPaint.Glyph(dc, Gfx, Info.Glyph, glyph, ink.Text, PopupPaint.Rect((width - glyph) / 2, (height - glyph) / 2, glyph, glyph));
    }
}
