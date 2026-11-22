// The Status icons entry: the status icons Windows 11's taskbar shows beside its tray while
// something is so — the microphone or the camera in use, airplane mode, a restart waiting —
// a cell each, in Windows' order (QuickSettings\Services\Indicators.cs has the set and where each
// is read). With nothing to say it has no cells, and takes no room on the bar: the one widget whose
// width is what it shows, as the tray's is how many icons there are, because that is what Windows'
// own does.
//
// A cell's hover is Windows' ("Microphone in use by:" and the apps), a click opens the page Windows
// opens for it, and a right click offers what Windows offers for it — the page, and where there is
// one, a way to turn it off (IWidgetMenu), then its "Settings", as every widget's menu ends at rest;
// Duplicate and Remove join them only while the bar is arranged.
//
// Each icon looks and answers as the network, sound and battery icons beside it do: Windows' own
// glyph for it (Indicators.Glyph, and where each comes from at the top of Indicators.cs), at the
// tray's size and placed by its ink in the middle of its square (Fit.SystemIcon), drawn in the
// taskbar's primary text colour, as Windows draws all of them (InkOf, IslandInk.SystemIcon) — the
// update icon with Windows' dot on it besides, red, orange or blue as Windows' own update icon wears
// it, in its place and size (DotOf, RestartDot) — side by side on the one card every widget wears
// (Scene.WearsCard), and lit under the pointer and pressed on its own square, not the row's
// (Scene.LightsItsOwnCell).
//
// EACH ICON HAS ONE OF THE TRAY'S THREE PLACES (Karpo.Core\StatusIcons.cs): on the bar, which is
// every icon's until somebody chooses; behind the entry's own arrow; or nowhere. The arrow is a cell
// of its own after the icons on the bar — Windows' tray chevron, E70E, turned the way its flyout opens (SystemGlyph.Chevron), at the size the tray's has
// beside its icons — and stands only while an icon behind it is so. Its click opens a small flyout
// of those icons as the tray's overflow is (the bar's squares, five to a row, the panel no larger
// than they are: IslandAction.Icons); a click on one opens its page, and its right click its own
// rows (IWidgetMenu.MenuCellOf), as on the bar. The arrow has no tooltip: its click opens Karpo's
// own flyout. An icon is moved between the two places in Settings, not by dragging.
//
// THE LAYOUT IS ITS FIRST LOOK. An entry that shows only what is so is never drawn while nothing is,
// so it would never be looked at the way other widgets are (docs\widgets.md §11). The bar measures
// it on every layout, and that measurement is where it starts following; what it starts goes to the
// pool, so the measurement does not wait.

using System.Numerics;
using Karpo.Core;
using Karpo.Dock.Native;
using Karpo.Dock.QuickSettings.Services;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;

namespace Karpo.Dock;

/// <summary>
/// A widget on the bar whose right click has rows of its own above the ones every widget has: what
/// a cell of it offers, and what a row does. The dock asks it for the cell that was right-clicked.
/// </summary>
internal interface IWidgetMenu
{
    /// <summary>The rows for cell <paramref name="index"/>, their ids at <see cref="IndicatorsIsland.MenuItem"/> and after; empty for none.</summary>
    IReadOnlyList<FlyoutItem> MenuRows(int index);

    /// <summary>A row of <see cref="MenuRows"/> was chosen.</summary>
    void MenuChosen(int index, uint id);

    /// <summary>
    /// The cell whose rows a right click on item <paramref name="id"/> of the flyout cell
    /// <paramref name="cell"/> opened stands for — a status icon behind the arrow — or -1 for none.
    /// </summary>
    int MenuCellOf(int cell, uint id) => -1;
}

internal sealed unsafe class IndicatorsIsland : CellIsland, IWidgetMenu, IIsland
{
    /// <summary>The first id of a cell's own rows: clear of the ids every widget's menu uses.</summary>
    internal const uint MenuItem = 9000;

    /// <summary>The first id of the icons in the arrow's flyout: clear of the menu's rows.</summary>
    internal const uint BehindItem = 9500;

    /// <summary>The arrow's share of the icons' size, as the tray's chevron has it; its glyph is the tray's (<see cref="SystemGlyph.Chevron"/>).</summary>
    private const float ChevronShare = 0.75f;

    private readonly IIndicatorSource source;
    private IReadOnlyList<Indicator> shown = [];
    private IReadOnlyList<Indicator> behind = [];
    private bool following;
    private DoNotDisturb? quiet;

    public IndicatorsIsland(string key, WidgetItem island, Gfx gfx, Config config)
        : base(key, island, gfx)
    {
        source = IndicatorSources.Source();
    }

    /// <summary>Nothing ticks: every source says when it changed.</summary>
    public override int IntervalMs => 0;

    /// <summary>
    /// A cell per icon on the bar that is so, and the arrow after them while an icon behind it is:
    /// none at all, and no room, while nothing is.
    /// </summary>
    protected override int Count => shown.Count + (behind.Count > 0 ? 1 : 0);

    /// <summary>What is shown on the bar now: for the tests.</summary>
    internal IReadOnlyList<Indicator> Shown => shown;

    /// <summary>What is behind the arrow now: for the tests.</summary>
    internal IReadOnlyList<Indicator> Behind => behind;

    /// <summary>The arrow's cell, after the icons on the bar; -1 while nothing is behind it.</summary>
    internal int Chevron => behind.Count > 0 ? shown.Count : -1;

    /// <summary>Where each icon shows, as the entry's options say (Karpo.Core\StatusIcons.cs).</summary>
    private StatusIconOptions? Places => (Settings as BarItem)?.StatusIcons;

    /// <summary>New options may put icons elsewhere: read again where it follows already.</summary>
    public override void Configure(WidgetItem island, Config config)
    {
        Settings = island;
        if (following)
        {
            Read();
        }
    }

    /// <summary>The icon cell <paramref name="index"/> stands for: one on the bar, one behind the arrow (after it), or null.</summary>
    private Indicator? At(int index)
    {
        if (index >= 0 && index < shown.Count)
        {
            return shown[index];
        }
        var back = index - shown.Count - 1;
        return behind.Count > 0 && back >= 0 && back < behind.Count ? behind[back] : null;
    }

    /// <summary>Starts following, once: on the layout's first measurement (see the top of this file).</summary>
    private void Follow()
    {
        if (following)
        {
            return;
        }
        following = true;
        source.Start(Post);
        // Do not disturb is Windows' own, shared with the bell, and says so on this thread.
        quiet = DoNotDisturb.Shared;
        quiet.Changed += Post;
        quiet.Look();
        Read();
    }

    /// <summary>The layout measuring it: its first look (see the top of this file).</summary>
    IslandCells IIsland.Measure(IslandStyle style)
    {
        Follow();
        return Measure(style);
    }

    /// <summary>What is so now; true when the number of cells changed, which only a new layout gives room for.</summary>
    protected override bool Sample(bool everything)
    {
        Follow();
        return Read();
    }

    private bool Read()
    {
        var state = source.State with { DoNotDisturb = source.State.DoNotDisturb || quiet?.On == true };
        var before = Count;
        (shown, behind) = Indicators.Split(state, Places);
        return Count != before;
    }

    public override void System(uint msg, WPARAM wParam, LPARAM lParam)
    {
        if (msg == WM.WM_POWERBROADCAST)
        {
            source.PowerChanged();
        }
    }

    /// <summary>What Windows' hover says for the icon; none for the arrow, whose click opens Karpo's own flyout.</summary>
    public override string? Tooltip(int index)
    {
        Follow();
        return index >= 0 && index < shown.Count ? shown[index].Tooltip : null;
    }

    /// <summary>Opens the page Windows opens for the icon; the arrow opens the flyout of the icons behind it.</summary>
    public override IslandAction Click(int index)
    {
        Follow();
        if (index >= 0 && index < shown.Count)
        {
            Opened(shown[index].Uri);
        }
        else if (index == Chevron)
        {
            return new IslandAction(Items: BehindItems(), Layout: FlyoutLayout.Grid, Icons: true);
        }
        return default;
    }

    /// <summary>The arrow's flyout: each icon behind it, its glyph, and its hover's first line for its name.</summary>
    internal IReadOnlyList<FlyoutItem> BehindItems() =>
        [.. behind.Select((icon, at) => new FlyoutItem(BehindItem + (uint)at, icon.Tooltip.Split('\n', 2)[0].TrimEnd(':', ' '), new FlyoutIcon.Glyph(icon.Glyph)))];

    /// <summary>An icon of the arrow's flyout was clicked: its page opens, as on the bar.</summary>
    public override void Invoke(int index, uint id)
    {
        if (id >= BehindItem && id - BehindItem < (uint)behind.Count)
        {
            Opened(behind[(int)(id - BehindItem)].Uri);
            return;
        }
        base.Invoke(index, id);
    }

    /// <summary>What a click or a row opens: Windows' page. A seam for the tests, which open nothing.</summary>
    internal Action<string> Opened { get; init; } = static uri => Launch.Open(uri);

    /// <summary>An icon's rows, on the bar or behind the arrow (<see cref="MenuCellOf"/>); none for the arrow, whose menu is the widget's.</summary>
    public IReadOnlyList<FlyoutItem> MenuRows(int index)
    {
        if (At(index) is not { } icon)
        {
            return [];
        }
        return [.. icon.Menu.Select((row, at) => new FlyoutItem(MenuItem + (uint)at, row.Text, new FlyoutIcon.Glyph(row.Glyph)))];
    }

    /// <summary>An icon of the arrow's flyout stands for the cell after the arrow at its place among them.</summary>
    public int MenuCellOf(int cell, uint id) =>
        cell == Chevron && id >= BehindItem && id - BehindItem < (uint)behind.Count ? Chevron + 1 + (int)(id - BehindItem) : -1;

    public void MenuChosen(int index, uint id)
    {
        if (At(index) is not { } icon || id < MenuItem || id - MenuItem >= icon.Menu.Count)
        {
            return;
        }
        var row = icon.Menu[(int)(id - MenuItem)];
        switch (row.Act)
        {
            case IndicatorAct.Open when row.Uri is { } uri:
                Opened(uri);
                break;
            case IndicatorAct.TurnOffDoNotDisturb:
                quiet?.Set(false);
                break;
            case IndicatorAct.Open:
                break;
            default:
                source.Act(row.Act);
                break;
        }
        Post();
    }

    public override void Dispose()
    {
        if (quiet is not null)
        {
            quiet.Changed -= Post;
        }
        source.Dispose();
        base.Dispose();
    }

    /// <summary>A cell is the icon font's glyph in its square, as the Wi-Fi and sound faces are.</summary>
    protected override float Length(int index, in IslandStyle style)
    {
        var fit = new Fit(style);
        return GlyphAndText.Square(style, fit, fit.SystemIcon);
    }

    /// <summary>
    /// The colour <paramref name="kind"/>'s icon is drawn in: the taskbar's icons' own, the network's,
    /// the sound's and the battery's beside it (<see cref="IslandInk.SystemIcon"/>). Windows draws
    /// every one of these in its primary text colour — the microphone and the location as much as
    /// the rest, not in the accent — and none of them in a warning colour of its own: energy saver's
    /// yellow is the battery's, not this icon's. So no kind is excepted; the kind is asked for so a
    /// test holds each one to it.
    /// </summary>
    internal static DXGI_RGBA InkOf(IndicatorKind kind, in IslandInk ink) => ink.SystemIcon;

    /// <summary>
    /// The colour of the dot <paramref name="indicator"/> wears, or null for none: the update icon
    /// alone, as Windows' own update icons are the arrows with a dot — red for a restart for Windows
    /// (WindowsUpdateImportant.ico, in Windows' critical red), orange for one for drivers alone,
    /// blue for updates waiting or under way (<see cref="RestartDot"/>, <see cref="WindowsUpdate.Badge"/>).
    /// The glyph itself keeps <see cref="InkOf"/>.
    /// </summary>
    internal static DXGI_RGBA? DotOf(Indicator indicator, in IslandInk ink) =>
        indicator.Kind == IndicatorKind.WindowsUpdate ? RestartDot.Ink(indicator.Badge, ink) : null;

    internal override void PaintCell(ID2D1DeviceContext* dc, int index, in IslandStyle style, float width, float height)
    {
        var fit = new Fit(style);
        var ink = new IslandInk(style.Look);
        if (index == Chevron)
        {
            // The arrow: the tray's chevron, in the icons' colour, three quarters of their size.
            // A style that says it is vertical and no edge is a side bar's of a test: the left's.
            PaintGlyph(dc, SystemGlyph.Chevron(style.Vertical && !style.Edge.IsVertical() ? Edge.Left : style.Edge), MathF.Round(fit.SystemIcon * ChevronShare), ink.SystemIcon, width, height);
            return;
        }
        if (index < 0 || index >= shown.Count)
        {
            return;
        }
        var color = InkOf(shown[index].Kind, ink);
        if (DotOf(shown[index], ink) is { } dot)
        {
            dc->SetTextAntialiasMode(D2D1_TEXT_ANTIALIAS_MODE.D2D1_TEXT_ANTIALIAS_MODE_GRAYSCALE);
            // Windows' own icon for it: the arrows, and its dot in the tray icon's square (RestartDot).
            RestartDot.Paint(dc, Gfx, RestartDot.Centred(width, height, fit.SystemIcon), shown[index].Glyph, color, dot);
            return;
        }
        PaintGlyph(dc, shown[index].Glyph, fit.SystemIcon, color, width, height);
    }

    /// <summary><paramref name="glyph"/> at <paramref name="size"/> px, by its ink in the middle of the cell.</summary>
    private void PaintGlyph(ID2D1DeviceContext* dc, string glyph, float size, DXGI_RGBA color, float width, float height)
    {
        dc->SetTextAntialiasMode(D2D1_TEXT_ANTIALIAS_MODE.D2D1_TEXT_ANTIALIAS_MODE_GRAYSCALE);
        ID2D1SolidColorBrush* brush = null;
        var line = TextLine.Lay(Gfx, glyph, size, Weight.Regular, null, Gfx.IconFamily);
        try
        {
            brush = Gfx.Brush(dc, color);
            // By the ink, as every glyph of Karpo's is placed (Gfx.InkOrigin).
            var origin = Karpo.Dock.Gfx.InkOrigin(line.Layout, width, height);
            line.Draw(dc, brush, color, origin.x, origin.y);
        }
        finally
        {
            line.Release();
            ComUtil.Release(ref brush);
        }
    }
}
