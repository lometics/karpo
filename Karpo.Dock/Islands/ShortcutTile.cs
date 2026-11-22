// The Snipping Tool and Settings in a folder: a line of a list (the backlog, items 10 and 18: "the
// same one-icon design as Task view and Search, in all three views"). Phone
// Link was a third until the person asked for it gone (item 23): a pin to the app does the same.
//
// On the bar the scene draws each as one of Windows' buttons, beside Search and Task view
// (WindowsButtons); inside a folder each is a widget like the others, so it is an island of its own
// here, as Task view is (TaskViewTile). A click does what the bar's button does, through the same
// answer: WindowsButtons.Target says what the kind opens on this PC, and WindowsButtons.Press
// carries it out — a snip, Settings. That is an act, not a popup of the folder's, so
// the line declares CellAct.Action, and the folder may close as the app comes.
//
// A snip is the exception to "as the app comes": it is a picture of the screen, and the folder
// must not be in it. So the snip's click asks the folder to close (IslandView.App, which the
// folder forwards), and the dock starts the snip once the folder has gone from the screen
// (Dock.FolderForward, which waits for it to be hidden and the screen composed again).
//
// The Snipping Tool may not be installed. Then the widget says so, as a pin to an app
// that is not here does (PinTile): its glyph faint with the warning badge on its corner, its name
// in the faint ink, and "This app isn't installed on this PC" as its tooltip. A folder opens no
// popup, so a click there opens what is left to open: the app's page in the Microsoft Store.
//
// Its line is the list's own row of its glyph and its name (FolderFlyout.PaintListLine).

using System.Numerics;
using Karpo.Core;
using Karpo.Dock.Native;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock;

internal sealed unsafe class ShortcutTile : CellIsland
{
    /// <summary>Segoe Fluent Icons: Warning, the badge a broken pin wears.</summary>
    internal const string WarningGlyph = "";

    /// <summary>How faint a missing app's glyph is, as a broken pin's icon.</summary>
    internal const float MissingOpacity = 0.4f;

    public ShortcutTile(string key, WidgetItem island, Gfx gfx, Config config)
        : base(key, island, gfx)
    {
    }

    /// <summary>Nothing it shows changes on a timer: whether its app is here is asked as it is drawn.</summary>
    public override int IntervalMs => 0;

    protected override int Count => 1;

    /// <summary>The bar's button this widget is a line of: the kind its click opens as.</summary>
    internal BarKind Kind => BarEntries.Find(Info.Id)?.Kind ?? BarKind.WindowsSettings;

    /// <summary>Why it opens nothing — its app is not installed — or null where it opens its app.</summary>
    internal string? Missing => WindowsButtons.Missing(Kind);

    public override void Configure(WidgetItem island, Config config) => Settings = island;

    public override string? Tooltip(int index) => Missing is { } missing ? $"{Info.Title}\n{missing}" : Info.Title;

    /// <summary>
    /// What the bar's button opens, there and then (<see cref="WindowsButtons.Press"/>). The widget
    /// acts itself and hands the folder nothing to carry out, which leaves the folder to close as
    /// the app takes the screen.
    /// </summary>
    public override IslandAction Click(int index)
    {
        if (WindowsButtons.Target(Kind, WindowsButtons.InstalledNow) is not { } target)
        {
            return default;
        }
        if (Snips(target))
        {
            // Not yet: the folder closes first, and the dock snips once it has gone (Act).
            return new IslandAction(View: IslandView.App);
        }
        WindowsButtons.Press(target);
        return default;
    }

    /// <summary>Whether <paramref name="target"/> is a snip, which waits for the folder to be off the screen.</summary>
    internal static bool Snips(ShellTarget target) => target is { By: OpenBy.Address, Address: WindowsButtons.Snip };

    /// <summary>
    /// What the click asked to be done once the folder has gone (<see cref="Snips"/>): the snip,
    /// through the same seam as every other click. From the dock, after it has waited.
    /// </summary>
    internal void Act()
    {
        if (WindowsButtons.Target(Kind, WindowsButtons.InstalledNow) is { } target)
        {
            WindowsButtons.Press(target);
        }
    }

    protected override bool Sample(bool everything) => false;

    /// <summary>Its glyph in the middle of the cell, as the bar's own button draws it.</summary>
    internal override void PaintCell(ID2D1DeviceContext* dc, int index, in IslandStyle style, float width, float height)
    {
        var fit = new Fit(style);
        var ink = new IslandInk(style.Look);
        var glyph = fit.Glyph();
        dc->SetTextAntialiasMode(D2D1_TEXT_ANTIALIAS_MODE.D2D1_TEXT_ANTIALIAS_MODE_GRAYSCALE);
        Glyph(dc, Gfx, Info.Glyph, Missing is not null, ink.Text, ink.Secondary, PopupPaint.Rect((width - glyph) / 2, (height - glyph) / 2, glyph, glyph));
    }

    /// <summary>
    /// <paramref name="glyph"/> fitted in <paramref name="box"/>: whole, or — for an app that is not
    /// installed (<paramref name="missing"/>) — faint, with the warning badge over its bottom-right
    /// corner in <paramref name="badge"/>'s ink, as a broken pin's icon (PinTile.Icon).
    /// </summary>
    internal static void Glyph(ID2D1DeviceContext* dc, Gfx gfx, string glyph, bool missing, DXGI_RGBA text, DXGI_RGBA badge, D2D_RECT_F box)
    {
        var side = MathF.Min(box.right - box.left, box.bottom - box.top);
        PopupPaint.Glyph(dc, gfx, glyph, side, missing ? IslandInk.Faded(text, MissingOpacity) : text, box);
        if (!missing)
        {
            return;
        }
        var corner = MathF.Max(8, MathF.Round(side * 0.5f));
        PopupPaint.Glyph(dc, gfx, WarningGlyph, corner, badge, PopupPaint.Rect(box.right - (corner * 0.75f), box.bottom - (corner * 0.75f), corner, corner));
    }
}
