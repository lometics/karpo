// The dock's visual tree, drawn the way Windows 11 draws its taskbar.
//
// Nothing here runs per frame in this process, and nothing moves under the
// pointer: every element sits where Layout puts it. What does change — the
// hover highlight and the tooltip fading, an indicator changing width, a
// pressed icon shrinking and springing back, the dock sliding in and out,
// and everything gliding to its new place when a rebuild moved it — is key
// frames, springs and implicit animations that DWM runs on its own.
//
// Flat materials only. There is no gradient brush anywhere: the Rust dock's
// glass sheen, gradient tint and bright rim are what the user asked to be rid
// of. Where Windows lights an edge (the focused app's border is brighter at
// the top), a second flat hairline does it. The one exception is the search
// box's 3-DIP elevation edge, which is WinUI's own (SearchPlate); and §704 the Liquid glass palette, a material the owner
// asked for by name (2026-11-15), whose glow, lens and rim are gradients (GlassPaint.cs) — on it alone.
//
// Measured on this machine's own Shell_TrayWnd at 150 % (dark, transparency
// effects off, accent #0078D4), read-only, and converted to DIPs. The layout
// uses these sizes (Layout.cs); the colours below replace REVISION 1's table
// where they differ. Hover and pressed can't be captured without touching the
// mouse, so those are the Fluent tokens.
//
//   Thing                     Measured (px @150 %)              Used
//   ------------------------  --------------------------------  --------------------------------------
//   Bar                       72 tall, #1C1C1C solid            48 DIP (config.Height); dark #1C1C1C,
//                                                               light #EEEEEE (acrylic fallbacks)
//   Top border                2 px #404040                      1 DIP, #757575 @ 0.40
//                                                               (SurfaceStrokeColorDefault)
//   App button                66 wide, full height              44 DIP cell
//   Start button              68 wide                           45 DIP cell
//   Highlight / focused bg    60 x 60 at (3, 6), r 6, #292929   40 x 40 DIP, radius 4, white @ 0.0588
//   Focused bg border         2 px on the fill's edge: top      1 DIP ring of white @ 0.028 on the edge
//                             #383838, sides/bottom #2F2F2F     + a 1 DIP top line of white @ 0.042
//                                                               (no gradient)
//   App icon                  36 x 36 at (15, 18)               24 DIP, centred
//   Running pill              9 x 5, #989898, 1 px above        6 x 3 DIP (10 for 2+ windows),
//                             the highlight's bottom            white @ 0.5451 (ControlStrongFillColor)
//   Focused pill              24 x 5, #0793FF (= AccentLight2)  16 x 3 DIP, AccentLight2 / AccentDark1
//   Start logo                35 x 35 at (16, 19) in the        23 DIP logo in the 24-DIP icon box,
//                             button, #77F3FF top left to       at its bottom left (drawn by
//                             #0B9DFF bottom right              Gfx.WindowsLogo)
//   Search / task view        hidden on this machine            44 DIP cells, Fluent glyphs E721 / E7C4
//   Tray icon                 48 wide cell, 24 x 24 icon        32 DIP cell, 16 DIP icon
//   Tray chevron              not shown (nothing hidden)        32 DIP cell, glyph E70E at 12 DIP
//   Network / volume          42 + 42 wide, 24 px glyph boxes   widgets of their own, one glyph each
//                             at +12 and +48, #FFFFFF; the      (folders' faces: SystemGlyph)
//                             volume waves not reached drawn
//                             at about 0.2
//   Clock                     117 wide, text block 94 at +12,   a folder wearing the clock face (the
//                             lines 24 tall from y 11, right-   clock preset; Islands\FaceDrawings.cs),
//                             aligned; the colon is U+2236      U+2236 for ':'
//                             RATIO, dots centred on the
//                             figures; date ink 92, time 42
//   Show desktop              18 wide at the very end, blank    a glyph button like the others (U+E977)
//   Gaps between buttons      none                              none
//   Hover / pressed           —                                 SubtleFillColorSecondary / Tertiary:
//                                                               dark white @ 0.0588 / 0.0392,
//                                                               light black @ 0.0353 / 0.0235
//   Press feel                —                                 icon scales to 0.9, springs back
//
// Karpo's own parts: each sits on a card, 6 DIPs round on the islands look, 4 on a thin bar: §547 Karpo's solid #2B2B2B
// unless another is chosen (KarpoLook), on a bar of #202020 at 85 % over the wallpaper Karpo blurs (WallpaperStrips); §568 in
// Windows' light mode #FBFBFB on #F3F3F3 at 85 %. The measures above are Windows' own; §547 the taskbar takes nothing of
// Windows' accent (Look.KarpoBase), §568 its mode again.
//
// All sizes above are for the measured 48-DIP bar with 24-DIP icons. On other
// bars the app buttons scale with the icon and the rest (text, tray)
// with the square root of the bar's height; a thin
// bar keeps its glyphs at 16 DIPs and its text at 11 or more (Layout.cs).

using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using Karpo.Core;
using Karpo.Dock.Native;
using TerraFX.Interop.DirectX;
using Windows.UI.Composition;
using Windows.UI.Composition.Desktop;
using Color = Windows.UI.Color;
using IUnknown = TerraFX.Interop.Windows.IUnknown;

namespace Karpo.Dock;

/// <summary>
/// How Windows is dressed right now (Dock.ReadLook, <see cref="Plain"/>: what Karpo's settings wear, §526), and §547 what the
/// taskbar is drawn in, of that: Karpo's own look in its Appearance (§567, <see cref="Wearing(Appearance?)"/>,
/// <see cref="KarpoBase"/>), §568 of Windows' mode, which nothing of Windows' accent reaches.
/// </summary>
/// <param name="Glass">
/// Transparency effects on, host backdrop allowed, and not high contrast; §547 never on the taskbar, which lays what is
/// see-through over the wallpaper it blurs itself.
/// </param>
/// <param name="Dark">The Windows mode (not the app mode) is dark; once dressed, the mode its ground reads in (§546, §547).</param>
/// <param name="Accent">UIColorType.Accent.</param>
/// <param name="AccentLight2">Indicators and selection on dark; <see cref="Accent"/> when unset.</param>
/// <param name="AccentDark1">Indicators and selection on light; <see cref="Accent"/> when unset.</param>
/// <param name="AccentDark2">The bar's colour when <see cref="AccentOnSurface"/>.</param>
/// <param name="AccentOnSurface">"Show accent color on Start and taskbar", which Windows only honours in dark mode.</param>
/// <param name="HighContrast">A high contrast theme is on; the Hc colours are its system colours.</param>
/// <param name="Animations">
/// Off means every transition is instant: Windows' Animation effects off, or Karpo's Animations (§489, ⚙ › General).
/// </param>
/// <param name="TextScale">Multiplies every text size.</param>
/// <param name="Material">
/// What the bar is made of, settled from its background and its opacity (§405, §459, §546, <see cref="SurfaceStyle.Made"/>,
/// <see cref="Dressed"/>): without transparency effects a colour chosen is translucent, Windows' own solid.
/// </param>
/// <param name="Opacity">0 to 1: how much of the colour shows over the blur.</param>
/// <param name="Colors">
/// §405, §459: every colour of the style's parts this look is dressed in (<paramref name="Dressing"/>, <see cref="ToneRules.Dress"/>),
/// each over Windows' own, but the text and the accent, which are <paramref name="Ink"/> and the accent's shades; none in
/// high contrast, where Windows' system colours are the only ones. Resolved in <see cref="Scene"/>'s Theme,
/// <see cref="FlyoutTheme"/> and a folder's lines, nowhere else.
/// </param>
/// <param name="Ink">
/// §396: the text colour derived, which every quieter text is fainter of (<see cref="Scene"/>'s Theme,
/// <see cref="FlyoutTheme"/>, IslandInk); null for Windows' own. Kept apart from <paramref name="Colors"/>, as the
/// accent is, since the widgets draw it themselves (<see cref="IslandStyle"/>).
/// </param>
/// <param name="Dressing">
/// §459, §464, §507: the sections this look is dressed in (<see cref="Dressed"/>): the bar, its plates, its menus and what
/// stands in them, the Appearance's; a flyout's outline.
/// </param>
/// <param name="Worn">§567: the Appearance as drawn, which a flyout is dressed of (<see cref="Flyout"/>).</param>
/// <param name="Windows">
/// §459, §509, §568: the mode Windows is in, which every value left unset is drawn in and §568 the Appearance's colours are
/// read the half of, on the taskbar as in the settings; null where it is <see cref="Dark"/> (as read).
/// </param>
/// <param name="Speed">§489: how fast everything moves (⚙ › General › Animation speed): the one engine's factor (<see cref="Motion"/>).</param>
/// <param name="Typeface">
/// §499, §507: the font and weight what this look draws is written in — a bar's the Appearance's Taskbar widgets', a folder's
/// and Karpo's chrome the menus' (its Widgets in folders') —: what
/// <see cref="Gfx.Typeface"/> is set to while it is measured and drawn (<see cref="Gfx.Wearing"/>). In the look so that
/// another font is another look, and everything drawn in it is measured and drawn again.
/// </param>
/// <param name="MenuTypeface">
/// §507: the font Karpo's menus and flyouts opened from this look are in: the Appearance's Widgets in folders' (<see cref="Looks.MenuText"/>).
/// </param>
/// <param name="AccentLight3">§544: a link's words on dark (AccentTextFillColorPrimary); <see cref="Accent"/> when unset.</param>
/// <param name="AccentDark3">§544: a link's words under the pointer on light (AccentTextFillColorSecondary); <see cref="Accent"/> when unset.</param>
/// <remarks>
/// Whether the widgets wear cards is not here: it is the bar's look (<see cref="Layout.Cards"/>,
/// the islands look and no other), where it was a switch of its own, "Island backgrounds". A card shows where its widget
/// has a background (§246, §405: derived, ToneRules): §547 Karpo's solid one by default (<see cref="KarpoLook.Card"/>, §568 <see cref="KarpoLook.LightCard"/> in the light mode).
/// </remarks>
public readonly record struct Look(
    bool Glass,
    bool Dark,
    Color Accent,
    Color AccentLight2 = default,
    Color AccentDark1 = default,
    Color AccentDark2 = default,
    bool AccentOnSurface = false,
    bool HighContrast = false,
    Color HcWindow = default,
    Color HcText = default,
    Color HcHighlight = default,
    Color HcHighlightText = default,
    bool Animations = true,
    float TextScale = 1f,
    Material Material = Material.Acrylic,
    float Opacity = 0.7f,
    Palette Colors = default,
    Argb? Ink = null,
    Dressing Dressing = default,
    LookPaints Worn = default,
    bool? Windows = null,
    Animation Speed = Animation.Normal,
    Karpo.Core.TextStyle Typeface = default,
    Karpo.Core.TextStyle MenuTypeface = default,
    Color AccentLight3 = default,
    Color AccentDark3 = default)
{
    /// <summary>§489: Karpo's one motion as this look has it: on or off (<see cref="Animations"/>), and how fast (<see cref="Speed"/>).</summary>
    public Motion Motion => new(Animations, Speed);

    /// <summary>
    /// §489: this look of Windows' as Karpo moves: still where <paramref name="config"/>'s Animations are off (Windows' off
    /// already is), at its speed.
    /// </summary>
    public Look Moving(Config config) => this with { Animations = Animations && config.Animation != Animation.Off, Speed = config.Animation };

    /// <summary>
    /// §489, §499, §507: this look of Windows' as <paramref name="config"/> has all of Karpo look, before the Appearance is
    /// put on: its motion (<see cref="Moving"/>) and the font Karpo's own chrome is in — the menus' (<see cref="Looks.MenuText"/>),
    /// as <see cref="Typeface"/> and <see cref="MenuTypeface"/>. What the dock reads (Dock.ReadLook): a font changed is
    /// another look, and every bar is built and drawn again in it.
    /// </summary>
    public Look Configured(Config config) =>
        Moving(config) with { Typeface = Looks.MenuText(config), MenuTypeface = Looks.MenuText(config) };

    /// <summary>§567: the taskbar in <paramref name="config"/>'s Appearance (<see cref="Wearing(Appearance?)"/>).</summary>
    public Look Wearing(Config config) => Wearing(config.Appearance);

    /// <summary>
    /// §507, §547, §567: the taskbar in <paramref name="appearance"/>: Karpo's own look (<see cref="KarpoBase"/>) in it
    /// (<see cref="Looks.Of(Appearance?, bool)"/>, §568 its values of the mode Windows is in over Karpo's defaults of it, §613
    /// the mode the Appearance says, Windows' by default: <see cref="Looks.Dark"/>): its
    /// Taskbar the ground, its Taskbar widgets the plates and the text on the bar, its Widgets in folders the menus and what is
    /// in them (their font the menus') (<see cref="Menus"/>, <see cref="Dressed"/>); its Flyouts' outline a widget's open
    /// flyout's (<see cref="Flyout"/>) and §624 every menu's. No colours in high contrast, where Windows' system colours and
    /// mode are the only ones. What every bar is drawn in, and every menu, flyout and tooltip opened from it.
    /// </summary>
    public Look Wearing(Appearance? appearance)
    {
        var basis = KarpoBase(Looks.Dark(appearance, Mode));
        var paints = Looks.Of(appearance, basis.Mode);
        // §704: Liquid glass wears the system's accent, Windows' own, as Apple's glass takes the system's; every other palette
        // Karpo's own blue where it says none (§547).
        if (paints.Glass is not null && !HighContrast)
        {
            var accent = Accent;
            Color Shade(Color shade) => shade.A == 0 ? accent : shade;
            basis = basis with
            {
                Accent = Accent,
                AccentLight2 = Shade(AccentLight2),
                AccentDark1 = Shade(AccentDark1),
                AccentDark2 = Shade(AccentDark2),
                AccentLight3 = Shade(AccentLight3),
                AccentDark3 = Shade(AccentDark3),
            };
        }
        return (basis with { Worn = paints, Typeface = paints.Widgets.Type, MenuTypeface = paints.FolderWidgets.Type })
            .Dressed(new Dressing(paints.Taskbar, paints.Widgets, Menus(paints), paints.FolderWidgets));
    }

    /// <summary>
    /// §547: this look of Windows' as Karpo's taskbar takes it: what is Windows' own in it replaced by Karpo's own
    /// (<see cref="KarpoLook"/>) — the accent and its shades, no accent on the bar, and Karpo's own blur in place of Windows'
    /// (<see cref="Glass"/> off: what is see-through is laid over the wallpaper Karpo blurs, never over DWM's, whether Windows'
    /// transparency effects are on or off). What it keeps of Windows' is its mode (§568: the taskbar follows Windows' light
    /// and dark again, its Appearance's colours of that mode; §613 <paramref name="dark"/>, the mode the Appearance pins it to
    /// or Windows'), high contrast (its system colours and mode, which win over every look and every mode), its text size and
    /// its animations. Two looks of Windows' that differ in nothing else are one taskbar (Dock.LookChanged).
    /// </summary>
    public Look KarpoBase(bool dark) => HighContrast
        ? this with { Glass = false, Windows = Mode }
        : this with
        {
            Glass = false,
            Dark = dark,
            Windows = dark,
            Accent = ColorOf(KarpoLook.Accent),
            AccentLight2 = ColorOf(KarpoLook.AccentLight2),
            AccentDark1 = ColorOf(KarpoLook.AccentDark1),
            AccentDark2 = ColorOf(KarpoLook.AccentDark2),
            AccentLight3 = ColorOf(KarpoLook.AccentLight3),
            AccentDark3 = ColorOf(KarpoLook.AccentDark3),
            AccentOnSurface = false,
        };

    /// <summary>
    /// §526: this look of Windows' with nothing worn: Windows' own alone — its mode, its accent, high contrast, transparency
    /// effects —, no colour, no outline, no text colour, its default font. What the dock reads Windows' look as
    /// (Dock.ReadLook), and Karpo's settings' look (Dock.SettingsLookOf).
    /// </summary>
    public Look Plain() =>
        (this with { Worn = default, Windows = Mode, Typeface = default, MenuTypeface = default }).Dressed(default);

    private static Color ColorOf(Argb color) => Color.FromArgb(color.A, color.R, color.G, color.B);

    /// <summary>
    /// §464: what the menus and flyouts are dressed in: the Widgets in folders' background (their rows are lines too), laid
    /// over Windows' own flyout (<see cref="FlyoutTheme"/>), and §624 the Flyouts' outline — the one border the Appearance
    /// gives what opens over the desktop, a folder, a widget's flyout or a menu (§512 had a menu wear the Widgets in folders'
    /// outline, which they have no more: a folder's lines are never outlined each).
    /// </summary>
    private static PartPaints Menus(LookPaints paints) => new(paints.FolderWidgets.Background, paints.Flyouts.Outline);

    /// <summary>The mode Windows is in, which Windows' own colours follow (§509: what is set is the same in both).</summary>
    private bool Mode => Windows ?? Dark;

    /// <summary>
    /// §405, §459: this look dressed in <paramref name="dressing"/>: the bar made as its background's opacity says
    /// (<see cref="SurfaceStyle.Made"/>), every colour derived (<see cref="ToneRules.Dress"/>) and drawn in the mode its ground
    /// reads in, the text its <see cref="Ink"/>, the accent in every shade of the accent where one is picked. No colours in
    /// high contrast.
    /// </summary>
    public Look Dressed(Dressing dressing)
    {
        var made = this with
        {
            Material = SurfaceStyle.Made(dressing.Ground.Background, Glass, HighContrast),
            Opacity = SurfaceStyle.OpacityOf(dressing.Ground.Background) / 100f,
            Dressing = dressing,
            Windows = Mode,
        };
        if (HighContrast)
        {
            return made with { Colors = default, Ink = null };
        }
        var (colors, dark) = ToneRules.Dress(dressing, Mode);
        var worn = made with { Dark = dark, Colors = colors with { Text = null, Accent = null }, Ink = colors.Text };
        if (colors.Accent is { } accent)
        {
            var shade = Color.FromArgb(255, accent.R, accent.G, accent.B);
            worn = worn with { Accent = shade, AccentLight2 = shade, AccentDark1 = shade, AccentDark2 = shade, AccentLight3 = shade, AccentDark3 = shade };
        }
        return worn;
    }

    /// <summary>
    /// §507, §514: a flyout a widget opens — a folder, the tray's overflow, a widget's list —, open: one thing however it is
    /// drawn. Outlined as the Appearance's Flyouts say (§567: no widget has an outline of its own for it); no ground — it
    /// stands over the desktop, not on the bar —; what is in it, a folder's lines and a flyout's rows, the Widgets in folders,
    /// its background theirs (a folder's lines each on its own, §464), its text the menus' font. §649 a folder attached to its
    /// bar (§647) on its <paramref name="ground"/>: its lines have no card, no pattern — they stand on the ground, which their
    /// text and accent are made readable on and their highlight is of; the menus opened from it as ever.
    /// </summary>
    public Look Flyout(Argb? ground = null)
    {
        var paints = Worn;
        var lines = ground is { } under ? paints.FolderWidgets with { Background = new Karpo.Core.Paint(under), Pattern = Karpo.Core.Pattern.None } : paints.FolderWidgets;
        var dressing = new Dressing(default, lines, Menus(paints), paints.FolderWidgets);
        return (HighContrast || dressing == Dressing ? this : Dressed(dressing)) with { Typeface = paints.FolderWidgets.Type };
    }

    /// <summary>§429: the accent this look wears, opaque.</summary>
    public Argb AccentArgb => new(255, Accent.R, Accent.G, Accent.B);

    /// <summary>§704: the material this look's palette is (Liquid glass's, of its mode); none for colours alone, and in high contrast.</summary>
    public GlassLook? Liquid => HighContrast ? null : Worn.Glass;
}

/// <summary>What a tile's indicator shows.</summary>
/// <param name="Count">How many windows the tile's app has open.</param>
/// <param name="Signal">What its app says on it: progress, an overlay, a badge (AppSignals.cs).</param>
public readonly record struct Mark(int Count, bool Active, bool Attention, TileSignal Signal = default);

/// <summary>
/// tray: a tray icon being dragged, as the bar draws it (<see cref="Scene.TrayDrag"/>).
/// </summary>
/// <param name="Key">The dragged icon, when it is one this bar shows; null when it came from the overflow.</param>
/// <param name="Index">Where the gap opens among the shown icons; -1 when the icon would be hidden instead.</param>
/// <param name="Part">The tray of the bar this is about, where it holds more than one; -1 for its first.</param>
/// <param name="More">
/// The cell the drag gives the tray or takes from it (Content.TrayMore): 1 for an icon coming onto
/// it, whose gap the icons after it make by a whole cell; -1 for its own going behind the chevron,
/// which the others close up behind; 0 for neither.
/// </param>
public readonly record struct TrayDragView(string? Key, int Index, int Part = -1, int More = 0);

/// <summary>
/// What a bar in edit mode shows beyond its blocks at rest, as the dock tracks
/// the pointer and the keys (see <see cref="Scene.Edit"/>). Parts are indexes
/// into the layout's specs.
/// </summary>
/// <param name="Dragging">The part being dragged, or null.</param>
/// <param name="PointerAlong">The pointer along the bar, in window px.</param>
/// <param name="GrabOffset">Where the dragged block was taken hold of, from its start along the bar.</param>
/// <param name="DropIndex">Where the dragged block would land: its section and its index in the list (see <see cref="Layout.DropSlot"/>).</param>
/// <param name="Focused">The block with the keyboard focus, drawn with the Windows focus rectangle.</param>
/// <param name="Hovered">The block under the pointer.</param>
/// <param name="Selected">
/// edit: the block last clicked, whose page the click opened in Karpo's settings: ringed in the
/// accent, as Windows 11 marks a selected item, until another widget is clicked or edit mode ends
/// (Dock.EditPick).
/// </param>
/// <param name="DropFolder">folders: the folder files dragged over the bar would go into, lit as the target.</param>
/// <param name="Insert">
/// pins: where something dragged over a bar that is not in edit mode would go, as a line between the blocks (see
/// <see cref="Layout.InsertionRect"/>), where the bar has no room to show its cell (<paramref name="Landing"/>).
/// </param>
/// <param name="DropTile">app clicks: files over an app's button, which they would open with: that button, lit as a folder is.</param>
/// <param name="Landing">
/// pins: files over a bar that is not in edit mode: the cell the new pin would take, at its size and
/// at the very place it would land (<see cref="Layout.LandingRect"/>), drawn as the target is.
/// </param>
/// <param name="Lefts">pins: with <paramref name="Landing"/>, where each part would start once the pin is in: the room the bar opens for it.</param>
/// <param name="Away">
/// places: the block being carried (<paramref name="Dragging"/>) is over another surface of the
/// display, or a folder standing open, which has it: this bar closes up behind it and shows no ghost.
/// </param>
/// <param name="Offered">
/// places: a block carried from another surface of the display would land on this one: its ghost at
/// the size it will have here, and the room the bar opens for it (<see cref="Layout.Landing(BarSlot, Layout.Carried)"/>).
/// </param>
/// <param name="AlsoSelected">
/// edit: the other blocks selected with <paramref name="Selected"/> (Ctrl+click, Shift+click, a drag
/// over the dim; Dock.EditSelect.cs), each ringed as it is.
/// </param>
/// <param name="Group">
/// edit: this bar's blocks among several carried together (Dock.EditSelect.cs): each goes with the
/// pointer in the carry window, so here they are out of sight, and the others stand where
/// <paramref name="GroupLanding"/> says.
/// </param>
/// <param name="GroupLanding">
/// edit: with <paramref name="Group"/>, this bar as it would stand: closed up behind its carried
/// blocks, and where the group would land on it its ghost round them all and the room they take
/// (<see cref="Layout.Landing(BarSlot, IReadOnlyList{Layout.Carried})"/>, <see cref="Layout.Remaining"/>).
/// </param>
/// <param name="Copied">
/// edit: the blocks on edit mode's clipboard (Dock.EditCopy.cs), each in Excel's marching ants in
/// place of its ring (MarchingAnts.cs).
/// </param>
/// <param name="AddLit">edit: the "+" under the pointer, by its index in <see cref="Layout.Adds"/>.</param>
/// <param name="AddPressed">edit: the "+" held down, by its index in <see cref="Layout.Adds"/>.</param>
/// <param name="FolderOpen">
/// edit: a folder of this bar stands open arranged (§397): its columns' "+" are where to add, and the
/// bar's stand aside until it closes.
/// </param>
/// <param name="Ghost">
/// §593: the part the add page's ghost stands in — the widget under the pointer on the add page, laid out where it would go
/// (Dock.AddGhost.cs) —: §625 drawn whole, as it will stand, the blue ghost's outline round it.
/// </param>
/// <param name="GhostEmpty">
/// §617: <paramref name="Ghost"/> the empty one — the add page open, no tile under the pointer —: none of its drawing, the blue
/// ghost round its room alone.
/// </param>
public sealed record EditView(
    int? Dragging = null,
    float PointerAlong = 0f,
    float GrabOffset = 0f,
    BarSlot DropIndex = default,
    int? Focused = null,
    int? Hovered = null,
    int? DropFolder = null,
    BarSlot? Insert = null,
    int? DropTile = null,
    RectF? Landing = null,
    float[]? Lefts = null,
    int? Selected = null,
    bool Away = false,
    Landed? Offered = null,
    IReadOnlyCollection<int>? AlsoSelected = null,
    IReadOnlyList<int>? Group = null,
    Landed? GroupLanding = null,
    IReadOnlyCollection<int>? Copied = null,
    int? AddLit = null,
    int? AddPressed = null,
    bool FolderOpen = false,
    int? Ghost = null,
    bool GhostEmpty = false);

/// <summary>§347: one tray part of a bar as it draws: the icons its widget shows on the bar, and whether its chevron stands.</summary>
public sealed record BarTray(IReadOnlyList<TrayIcon> Shown, bool Overflow);

public sealed class Content
{
    public List<(Item Item, Mark Mark)> Tiles { get; init; } = [];

    public bool TrayOverflow { get; init; }

    /// <summary>The islands to draw, for the island parts the layout has; the scene shows each cell's <see cref="IIsland.Layers"/>.</summary>
    public IslandCollection? Islands { get; init; }

    /// <summary>
    /// tray: the notification icons this bar draws, in order — exactly what <c>TrayLayout.Split</c>
    /// left on the bar (Dock.ReadTray), the ones behind the chevron already taken out. Their
    /// <see cref="TrayIcon.Hidden"/> is only Windows' own choice for them and says nothing about
    /// this list: an icon the person keeps on the bar that Windows would put in the overflow is on
    /// it, and is drawn like any other. Filtering here again dropped exactly those icons.
    /// </summary>
    public IReadOnlyList<TrayIcon>? Tray { get; init; }

    /// <summary>
    /// §347: each tray part's own, by its key — the icons it draws and whether it has the chevron —, as
    /// its widget's own states and order split them (Dock.ReadTray): two trays show the same icons each
    /// its own way. A tray part it doesn't name draws <see cref="Tray"/> and <see cref="TrayOverflow"/>,
    /// as a bar made in a test does; the dock names every one.
    /// </summary>
    public IReadOnlyDictionary<string, BarTray>? Trays { get; init; }

    /// <summary>The icons the tray part <paramref name="key"/> draws (<see cref="Trays"/>).</summary>
    public IReadOnlyList<TrayIcon> TrayOf(string key) =>
        Trays is { } trays && trays.TryGetValue(key, out var own) ? own.Shown : Tray ?? [];

    /// <summary>Whether the tray part <paramref name="key"/> has the chevron for icons behind it (<see cref="Trays"/>).</summary>
    public bool TrayOverflowOf(string key) =>
        Trays is { } trays && trays.TryGetValue(key, out var own) ? own.Overflow : TrayOverflow;

    /// <summary>
    /// tray: while an icon is dragged, the cells each tray part (by its key) takes beyond one per
    /// icon of <see cref="Tray"/>: one more for an icon coming onto it, one fewer for one of its own
    /// going behind the chevron (Dock.TrayRooms). Null, or a part not named, for none.
    /// </summary>
    public IReadOnlyDictionary<string, int>? TrayMore { get; init; }

    /// <summary>Pre-drawn surfaces for tiles that have no shell icon of their own, such as groups, by tile index.</summary>
    public IReadOnlyDictionary<int, CompositionDrawingSurface>? TileSurfaces { get; init; }

    /// <summary>
    /// app buttons: the part each tile stands in, by its key — a pin's own, or null for the Apps
    /// widget's —, in the tiles' order. A pin whose windows are not combined stands as one tile per
    /// window. Null for a pin one tile each and the Apps widget the rest.
    /// </summary>
    public IReadOnlyList<string?>? TileParts { get; init; }

    /// <summary>
    /// app buttons: what each tile says beside its icon — a window's title, where an app's windows
    /// are not combined —, in the tiles' order; null, or an empty one, for an icon alone.
    /// </summary>
    public IReadOnlyList<string?>? Labels { get; init; }
}

/// <summary>
/// Surfaces for icons, kept across rebuilds so a new window opening doesn't
/// send every pin back through the shell's image factory.
/// </summary>
public sealed class Icons
{
    // perf: closed a rebuild after nobody uses them (see Kept), not left to the GC.
    private readonly Kept<(string Target, string Picture, uint Size), CompositionDrawingSurface> map = new();

    /// <summary>
    /// §668: what an item's surface is kept by: its target, its picture as <see cref="Item.PictureKey"/> tells it — a
    /// pin's own picture, and broken or whole —, and the size. A pin drawn broken (dimmed, its first letter) that
    /// heals is read afresh; by its target alone it kept the letter for as long as the dock ran.
    /// </summary>
    internal static (string Target, string Picture, uint Size) KeyOf(Item item, float size) => (item.Target, item.PictureKey, (uint)size);

    public CompositionDrawingSurface Get(Gfx gfx, Item item, float size)
    {
        var key = KeyOf(item, size);
        if (map.TryGet(key, out var cached))
        {
            return cached;
        }
        var surface = item.Icon((int)size) is { } pixels ? gfx.Icon(pixels, size) : gfx.Tile(item.Name, size);
        map.Add(key, surface);
        return surface;
    }

    /// <summary>After every bar was built: closes the surfaces no build has used for a whole rebuild.</summary>
    public void Sweep() => map.Sweep();

    public void Forget() => map.Clear();
}

public sealed partial class Scene : IDisposable
{
    public const int HoverOutMs = 220;

    // Sizes in DIPs on the 48-DIP bar. The app buttons' scale with the icon
    // (Layout.UnitPx), the others' with the bar (Layout.SoftPx).
    private const float ButtonGlyphDip = 20;
    private const float ButtonGlyphBoxDip = 24;
    private const float LogoDip = 23;

    // Glyphs sized by what holds them: the chevron is 12 of a 16-DIP tray icon.
    private const float ChevronShare = 0.75f;

    // Sizes in DIPs that don't scale.
    private const float TooltipRadiusDip = 4;
    private const float TooltipTextDip = 12;
    private const float TooltipPadXDip = 9;
    private const float TooltipPadYDip = 6;
    private const float GripLineDip = 2;
    private const float CardInsetDip = 2;
    private const float MinRadiusDip = 2;

    private const float PressedScale = Motion.PressedScale;

    // Launch feedback's hold: the pressed highlight stands this long before it fades (Bounce). Feedback, not a motion: it
    // holds with motion off too.
    private const int PressHoldMs = 300;

    // Edit mode: a dragged block is a little see-through, as a folder's line carried (EditLook.cs).
    private const float DraggedOpacity = EditLook.LiftOpacity;

    // floating: how dark a panel's shadow is, on the dark look and the light one — a window
    // preview's (Preview.cs), the subtle shadow Windows 11 gives its floating surfaces.
    private const float FloatShadowDark = 0.30f;
    private const float FloatShadowLight = 0.14f;

    /// <summary>How much of a pin shows when this PC doesn't have what it opens.</summary>
    private const float BrokenOpacity = 0.4f;

    // Segoe Fluent Icons.
    /// <summary>Warning: the badge on a pin whose target isn't on this PC.</summary>
    private static readonly string WarningGlyph = char.ConvertFromUtf32(0xE7BA);

    /// <summary>Folder: shown over a widget a dragged one has rested on long enough to make a folder of the two.</summary>
    private static readonly string FolderGlyph = char.ConvertFromUtf32(0xE8B7);
    private static readonly string SearchGlyph = char.ConvertFromUtf32(0xE721);
    private static readonly string TaskViewGlyph = char.ConvertFromUtf32(0xE7C4);

    /// <summary>app buttons: Segoe Fluent Icons' More, the "…" of the buttons the bar has no room for.</summary>
    private static readonly string MoreGlyph = char.ConvertFromUtf32(0xE712);

    /// <summary>
    /// §358: the tooltips' words drawn so far, by what they say; past this many the ones not showing
    /// are let go of — a clock's tooltip says something new every minute, and the scene lives as long
    /// as its window.
    /// </summary>
    private const int TipTextsKept = 16;

    private readonly Gfx gfx;
    private readonly Compositor compositor;
    /// <summary>The window's; §536 none for a scene drawn inside another surface's tree (<see cref="Scene(Gfx, ContainerVisual)"/>).</summary>
    private readonly DesktopWindowTarget? target;

    /// <summary>What the target shows in place of the scene's root, the root inside it; null where it shows the root.</summary>
    private readonly ContainerVisual? host;

    // What the scene was last laid out as (Update): the layout, what it shows, its look and the colours
    // that look resolves to, and the icons it draws the pins with. Null until the first update.
    private Layout layout = null!;
    private Content content = new();
    private Look look;

    /// <summary>§691: the look the scene was last updated or recoloured in.</summary>
    internal Look Shown => look;
    private Tokens tokens = null!;
    private Icons icons = new();
    private bool updated;

    /// <summary>
    /// The brushes that draw a colour somebody may choose and are the scene's own rather than an
    /// element's (<see cref="Swatch"/>); an element's are its own (<see cref="Element.Swatches"/>).
    /// </summary>
    private readonly List<Swatched> swatches = [];

    /// <summary>
    /// §358: what the scene keeps for its window's whole life — its layers, the highlight, the
    /// tooltip, the brushes every element shares — closed with it. What an element makes is the
    /// element's (<see cref="Element.Bag"/>), closed when it goes.
    /// </summary>
    private readonly List<IDisposable> owned = [];
    private readonly CompositionEasingFunction linear;
    private readonly CompositionEasingFunction decelerate;
    private readonly CompositionEasingFunction accelerate;
    private readonly ContainerVisual root;
    private readonly ScalarKeyFrameAnimation quickIn;
    private readonly ScalarKeyFrameAnimation quickOut;
    private readonly Vector3KeyFrameAnimation pressIn;
    private readonly Vector3KeyFrameAnimation pressOut;
    private bool disposed;

    // Where the layout's window lies in the real one (the window stands still at its edge's most
    // thickness, §358, and the bar is laid out in it), how far out of sight the bar slides, and
    // whether the last Present showed it: an update keeps it as it is, a slide going on too.
    private Vector3 origin;
    private Vector3 hiddenOffset;
    private bool presented;

    // The layers: the panel under everything; every part has a container in the next ones (see
    // Into); edit mode puts the groups' zones under them and its "+" just over the zones, the
    // blocks' marks over everything, and a dragged block over all of that.
    private readonly ContainerVisual panels;
    private readonly ContainerVisual zones;

    /// <summary>edit: the sections' "+", a layer of their own over the zones: kept, gliding with the blocks, never faded in (§358).</summary>
    private readonly ContainerVisual adds;
    private readonly ContainerVisual backgrounds;
    private readonly ContainerVisual foreground;

    /// <summary>
    /// The cards' outlines (<see cref="Card"/>), over the highlight and what stands on the cards, so a
    /// lit, pressed or washed widget keeps its outline's colour (§325): fill, highlight, outline.
    /// </summary>
    private readonly ContainerVisual rims;
    private readonly ContainerVisual blocks;
    private readonly ContainerVisual lifted;

    /// <summary>
    /// edit: what holds <see cref="lifted"/>, and keeps it out of sight while a block is carried: the
    /// carry window shows the block at the pointer instead, drawn from <see cref="lifted"/> itself
    /// (<see cref="Carried"/>), which this one's opacity does not reach.
    /// </summary>
    private readonly ContainerVisual carryHolder;

    /// <summary>The parts' containers by their index in the layout, as the last update counted them (<see cref="partViews"/>).</summary>
    private PartVisuals?[] parts = [];

    /// <summary>§358: every part's containers by its key, kept as long as the bar has the part.</summary>
    private readonly Dictionary<string, PartVisuals> partViews = [];

    // Edit mode: its brushes (null when not editing), the empty groups' zones,
    // the groups' regions during a drag, the dragged block's shadow and which
    // part is lifted over the others.
    private EditBrushes? edit;

    /// <summary>edit: the empty blocks standing in the sections that hold nothing (null when not editing).</summary>
    private EmptyBlocks? empties;

    /// <summary>edit: the sections' "+" (<see cref="Layout.Adds"/>; null when not editing).</summary>
    private AddButtons? addButtons;

    /// <summary>
    /// edit: the blue ghost of a carried block where it would land, at its size there: in the gap a
    /// block carried along this bar opens, or where one carried from another surface would go
    /// (<see cref="EditView.Offered"/>). The block itself goes with the pointer in a window of its
    /// own (Dock.EditCarry.cs), so the bar keeps it out of sight meanwhile (<see cref="carryHolder"/>).
    /// </summary>
    private Ghosts? landingGhost;

    // floating: the docks (FloatingPanels), by section, null on a bar that does not float;
    // and what a new one is made from.
    private Dictionary<BarSection, FloatingPanel>? floating;
    private FloatingMaker? floatingMaker;

    // The key frame animations Reshape sets again for every use.
    private Vector2KeyFrameAnimation? reshapeMove;
    private Vector2KeyFrameAnimation? reshapeGrow;

    // pins: what a drag outside edit mode shows (see Drag): the target's outline and the line where a
    // drop would land. Made the first time a drag needs them.
    private Rounded? dragTarget;
    private Rounded? dragLine;

    /// <summary>pins: the cell a new pin dropped on the bar would take, at its size and place (EditView.Landing).</summary>
    private Rounded? dragLanding;
    private CompositionColorBrush? dragFill;
    private CompositionColorBrush? dragStroke;

    /// <summary>edit: the lifted block's shadow, and what it was made at; null outside edit mode or without shadows.</summary>
    private Shadow? liftShadow;
    private (float Radius, float Blur, float Drop, float Opacity) liftShadowMade;
    private readonly List<IDisposable> liftShadowBag = [];
    private int liftedPart = -1;

    // The key frame animations Slide and FadeTo set again for every use.
    private Vector3KeyFrameAnimation? slider;
    private ScalarKeyFrameAnimation? fader;

    // tray: the shown icons each tray of this bar drew, in their order, by its part, and how far each
    // has been moved to open a gap while one is dragged (TrayDrag), by TrayGlideKey.
    private readonly Dictionary<int, List<string>> trayKeys = [];
    private readonly Dictionary<string, Vector3> trayShifts = [];
    private readonly Dictionary<string, TrayGlide> trayGlides = [];

    // One highlight follows the pointer; the ghost fades out the one it left
    // and the flash shows launch feedback on a tile the pointer isn't on.
    private readonly Rounded highlight;
    private readonly Rounded ghost;
    private readonly Rounded flash;

    // keys: the Windows focus rectangle on the app button or tray icon the keyboard is on (Win+T,
    // Win+B; TaskbarKeys.cs) — 2 px outside, 1 px inside it, as edit mode rings a block.
    private readonly Rounded keyOuter;
    private readonly Rounded keyInner;
    private readonly CompositionColorBrush keyOuterInk;
    private readonly CompositionColorBrush keyInnerInk;
    private readonly CompositionColorBrush highlightFill;
    private readonly CompositionColorBrush ghostFill;
    private readonly CompositionColorBrush flashFill;
    private readonly CompositionColorBrush sliverFill;

    /// <summary>The hover's outline, drawn only where hover is an outline (high contrast, <see cref="Tokens.HoverOutline"/>).</summary>
    private readonly CompositionColorBrush outline;
    private Hit? lit;
    private bool litPressed;

    // What shrinks while each item is pressed, and what is shrunk right now.
    private readonly Dictionary<Hit, Visual> pressables = [];
    private Visual? pressedVisual;

    // The app buttons as the last update counted them, by their index: each one's icon, focused
    // plate, indicator state, indicator and where the indicator is centred.
    private readonly List<SpriteVisual> tiles = [];
    private readonly List<Back> backs = [];
    private readonly List<Mark> marks = [];
    private readonly List<SpriteVisual> pills = [];
    private readonly List<Vector2> pillCentres = [];

    // The indicators' and the focused plates' brushes: the scene's, coloured from the tokens at every update.
    private readonly CompositionColorBrush running;
    private readonly CompositionColorBrush active;
    private readonly CompositionColorBrush attention;
    private readonly CompositionColorBrush activeBack;
    private readonly CompositionColorBrush attentionBack;
    private readonly CompositionColorBrush activeStroke;
    private readonly CompositionColorBrush activeLine;

    // folders: the open folder's face, by its part's key (Open), and its plate under the highlight, in its
    // part's own colours: the focused app's button's (§376).
    private string? openKey;
    private int openPart = -1;
    private readonly Back openPlate;
    private readonly CompositionColorBrush openBack;
    private readonly CompositionColorBrush openStroke;
    private readonly CompositionColorBrush openLine;

    private readonly ContainerVisual tip;

    /// <summary>§359: what holds the tooltip in a window of its own (BarTip), where the bar's window lies; null: in the scene's own tree.</summary>
    private readonly ContainerVisual? tipHolder;
    private readonly Rounded tipBack;
    private readonly SpriteVisual tipText;
    private readonly CompositionSurfaceBrush tipTextBrush;
    private readonly CompositionColorBrush tipFill;
    private readonly CompositionColorBrush tipStroke;

    /// <summary>The tooltip's shadow and what it was made at; none without one (high contrast).</summary>
    private Shadow? tipShadow;
    private (float Radius, float Blur, float Drop, float Opacity) tipShadowMade;
    private readonly List<IDisposable> tipShadowBag = [];
    private readonly Dictionary<string, (CompositionDrawingSurface Surface, Vector2 Size)> tipTexts = [];

    /// <summary>What the tooltip's words were drawn for, which an update that changes it draws them again for.</summary>
    private (Color Ink, float Px, float Width, float Most, Karpo.Core.TextStyle Typeface) tipTextsFor;
    private bool tipShown;

    /// <summary>§358: what the tooltip stands over and its size: an update moves it with its item, never fading it in again.</summary>
    private Hit? tipOver;
    private Vector2 tipSize;

    /// <summary>§382: the part of its item it stands over (a status icon of a folder's button), in the item's px; null for the whole item.</summary>
    private RectF? tipPart;

    // perf: each drawn tray icon, by its tray's part and its index among the icons shown (SetTray).
    private readonly Dictionary<(int Part, int Index), TrayIconView> trayPictures = [];

    /// <summary>
    /// Makes the bar's tree on <paramref name="target"/>, for the window's whole life (§358): its
    /// layers, its highlight and its tooltip, empty and out of sight. <see cref="Update"/> lays it
    /// out, and again at every change, keeping what stays as it is.
    /// <paramref name="host"/>, where given, is what the target shows, the root inside it.
    /// <paramref name="tips"/>, where given, is a window of its own the tooltip is drawn in (BarTip, §359):
    /// the bar's window shows nothing past its region, the bar's panel, and the tooltip stands beside it.
    /// </summary>
    public Scene(Gfx gfx, DesktopWindowTarget target, ContainerVisual? host = null, ContainerVisual? tips = null)
    {
        this.gfx = gfx;
        this.target = target;
        this.host = host;
        sink = owned;
        swatchSink = swatches;
        var c = compositor = gfx.Compositor;
        // §489: the one motion's curves (Motion.cs).
        linear = Own(MotionEase.Of(c, MotionKind.Hover));
        decelerate = Own(MotionEase.Of(c, MotionKind.Move));
        accelerate = Own(MotionEase.Of(c, MotionKind.Hide));
        root = Own(c.CreateContainerVisual());
        root.IsVisible = false;
        root.Opacity = 0f;
        var layers = root.Children;
        ContainerVisual Layer()
        {
            var layer = Own(c.CreateContainerVisual());
            layers.InsertAtTop(layer);
            return layer;
        }
        // Each part's visuals sit in containers of their own in each layer
        // (see Into), so edit mode can move a part as one. Edit mode puts the
        // groups' zones under everything, the blocks' outlines and badges over
        // everything, and a dragged block over all of it.
        panels = Layer();
        zones = Layer();
        adds = Layer();
        backgrounds = Layer();
        var highlights = Layer();
        foreground = Layer();
        rims = Layer();
        // keys: the focus rectangle over the outlines it goes round.
        var keys = Layer();
        blocks = Layer();
        lifted = Own(c.CreateContainerVisual());
        carryHolder = Layer();
        carryHolder.Children.InsertAtTop(lifted);

        highlightFill = Own(c.CreateColorBrush());
        ghostFill = Own(c.CreateColorBrush());
        flashFill = Own(c.CreateColorBrush());
        sliverFill = Own(c.CreateColorBrush());
        outline = Own(c.CreateColorBrush());
        ghost = new Rounded(c, owned);
        highlight = new Rounded(c, owned);
        flash = new Rounded(c, owned);
        foreach (var shape in (ReadOnlySpan<Rounded>)[ghost, highlight, flash])
        {
            shape.Visual.Opacity = 0f;
            highlights.Children.InsertAtTop(shape.Visual);
        }
        // folders: the open folder's plate under the pointer's highlight, which lights over it.
        openBack = Own(c.CreateColorBrush());
        openStroke = Own(c.CreateColorBrush());
        openLine = Own(c.CreateColorBrush());
        openPlate = new Back(c, owned, default);
        openPlate.Visual.IsVisible = false;
        highlights.Children.InsertAtBottom(openPlate.Visual);
        keyOuterInk = Own(c.CreateColorBrush());
        keyInnerInk = Own(c.CreateColorBrush());
        keyOuter = new Rounded(c, owned);
        keyInner = new Rounded(c, owned);
        foreach (var ring in (ReadOnlySpan<Rounded>)[keyOuter, keyInner])
        {
            ring.Visual.IsVisible = false;
            keys.Children.InsertAtTop(ring.Visual);
        }
        running = Own(c.CreateColorBrush());
        active = Own(c.CreateColorBrush());
        attention = Own(c.CreateColorBrush());
        activeBack = Own(c.CreateColorBrush());
        attentionBack = Own(c.CreateColorBrush());
        activeStroke = Own(c.CreateColorBrush());
        activeLine = Own(c.CreateColorBrush());

        // The tooltip: a small flyout built from composition, so it takes the
        // theme's colours, over a text surface; its shadow under it where the look has one.
        if (tips is not null)
        {
            // Where the bar's window would show it, in the tooltip's own window, which stands where the bar's does.
            tipHolder = Own(c.CreateContainerVisual());
            tips.Children.InsertAtTop(tipHolder);
            tip = Own(c.CreateContainerVisual());
            tipHolder.Children.InsertAtTop(tip);
        }
        else
        {
            tip = Layer();
        }
        tip.IsVisible = false;
        tipFill = Own(c.CreateColorBrush());
        tipStroke = Own(c.CreateColorBrush());
        tipBack = new Rounded(c, owned);
        tip.Children.InsertAtTop(tipBack.Visual);
        (tipText, tipTextBrush) = TextSprite(c);
        tip.Children.InsertAtTop(tipText);

        // Their times are set as each runs, by the look's motion then (Time): its speed may have changed since.
        quickIn = Own(c.CreateScalarKeyFrameAnimation());
        quickIn.InsertKeyFrame(0f, 0f);
        quickIn.InsertKeyFrame(1f, 1f, linear);
        quickOut = Own(c.CreateScalarKeyFrameAnimation());
        quickOut.InsertKeyFrame(0f, 1f);
        quickOut.InsertKeyFrame(1f, 0f, linear);
        pressIn = Own(c.CreateVector3KeyFrameAnimation());
        pressIn.InsertKeyFrame(1f, new Vector3(PressedScale, PressedScale, 1f), decelerate);
        // §489: released, the icon settles back as it went, no overshoot (no bounce in the one motion; a spring before).
        pressOut = Own(c.CreateVector3KeyFrameAnimation());
        pressOut.InsertKeyFrame(1f, Vector3.One, decelerate);

        if (host is not null)
        {
            host.Children.InsertAtTop(root);
            if (target is not null && !ReferenceEquals(target.Root, host))
            {
                target.Root = host;
            }
        }
        else if (target is not null)
        {
            target.Root = root;
        }
    }

    /// <summary>
    /// §536: a bar's tree inside <paramref name="host"/>, in no window of its own: a look's preview in the settings panel
    /// (LookPreviews), the very bar the Appearance is drawn on, laid out again once a change.
    /// </summary>
    public Scene(Gfx gfx, ContainerVisual host)
        : this(gfx, null!, host)
    {
    }

    /// <summary>The root of the whole dock, as its window target shows it.</summary>
    public Visual Visual => root;

    /// <summary>§629: the corners' radius of the add page's ghost's rectangles (px, ShowGhost's).</summary>
    internal float GhostRadius => EditRadius;

    /// <summary>
    /// Puts the hover highlight on <paramref name="hover"/>, in its pressed
    /// shade while <paramref name="pressed"/>. Null, the bar or anything
    /// without a rectangle clears it. The old item fades out as the new one
    /// fades in, as on the taskbar.
    /// </summary>
    public void Highlight(Hit? hover, bool pressed)
    {
        if (!Ready)
        {
            return;
        }
        Hit? target = hover is { Kind: not HitKind.Panel } candidate
            && !layout.ItemRect(candidate).IsEmpty
            ? candidate
            : null;
        // search: a search box under the pointer, or pressed, takes Windows' look for it.
        LightSearch(target, pressed);
        if (target == lit)
        {
            if (target is { } same && pressed != litPressed)
            {
                litPressed = pressed;
                highlightFill.StopAnimation("Color");
                highlightFill.Color = ShadeOf(same, pressed);
            }
        }
        else
        {
            if (lit is { } old)
            {
                Dress(ghost, ghostFill, old, litPressed);
                Fade(ghost.Visual, false);
            }
            lit = target;
            litPressed = pressed;
            if (target is { } now)
            {
                Dress(highlight, highlightFill, now, pressed);
                Fade(highlight.Visual, true);
            }
            else
            {
                highlight.Visual.StopAnimation("Opacity");
                highlight.Visual.Opacity = 0f;
            }
        }
        // §506: a plate at rest lit whole is that plate drawn stronger, the highlight clear over it (ShadeOf).
        LightPlates();
    }

    /// <summary>
    /// keys: the focus rectangle on <paramref name="hit"/>, the app button or tray icon the keyboard
    /// is on; null, or anything the bar has no room for, takes it away.
    /// </summary>
    public void KeyFocus(Hit? hit)
    {
        if (!Ready)
        {
            return;
        }
        if (hit is not { } at || layout.ItemRect(at).IsEmpty)
        {
            keyOuter.Visual.IsVisible = keyInner.Visual.IsVisible = false;
            return;
        }
        var hair = layout.Stroke;
        // Round what the pointer lights, in its corners (Dress): a search box, a widget's card and its plate at rest (§506), a
        // button's cell; a connected bar's buttons are rounded too.
        var (rect, radius, _) = LitShape(at);
        keyOuter.Place(rect, radius, null, keyOuterInk, 2f * hair);
        keyInner.Place(rect, radius, null, keyInnerInk, hair, 2f * hair);
        keyOuter.Visual.IsVisible = keyInner.Visual.IsVisible = true;
    }

    /// <summary>
    /// The press feel: while <paramref name="down"/>, <paramref name="hit"/>'s icon
    /// shrinks a little; released, it springs back. Whatever was pressed before
    /// is released first. Not a hover size change: nothing moves until a press.
    /// </summary>
    public void Press(Hit? hit, bool down)
    {
        if (!Ready)
        {
            return;
        }
        if (pressedVisual is { } previous)
        {
            pressedVisual = null;
            Scale(previous, false);
        }
        if (down && hit is { } pressedHit && pressables.TryGetValue(pressedHit, out var visual))
        {
            pressedVisual = visual;
            Scale(visual, true);
        }
    }

    /// <summary>
    /// Launch feedback on tile <paramref name="index"/>: the pressed highlight
    /// for 300 ms, then a fade. It doesn't move the icon, as the Rust bounce did.
    /// </summary>
    public void Bounce(int index)
    {
        if (!Ready || (uint)index >= (uint)tiles.Count || !tiles[index].IsVisible)
        {
            return;
        }
        var hit = Hit.Tile(index);
        // With animations off the pressed state still holds, and then goes at once.
        var total = look.Animations ? PressHoldMs + (int)look.Motion.Ms(MotionKind.Hover) : PressHoldMs;
        var hold = look.Animations ? (float)PressHoldMs / total : 0.99f;
        if (lit == hit)
        {
            // Back to the hover the pointer still gives it, not to nothing.
            var settle = compositor.CreateColorKeyFrameAnimation();
            settle.InsertKeyFrame(0f, ShadeOf(hit, true));
            settle.InsertKeyFrame(hold, ShadeOf(hit, true));
            settle.InsertKeyFrame(1f, ShadeOf(hit, litPressed), linear);
            settle.Duration = Motion.Span(total);
            highlightFill.StartAnimation("Color", settle);
            settle.Dispose();
            return;
        }
        Dress(flash, flashFill, hit, true);
        var fade = compositor.CreateScalarKeyFrameAnimation();
        fade.InsertKeyFrame(0f, 1f);
        fade.InsertKeyFrame(hold, 1f);
        fade.InsertKeyFrame(1f, 0f, linear);
        fade.Duration = Motion.Span(total);
        flash.Visual.StartAnimation("Opacity", fade);
        fade.Dispose();
    }

    /// <summary>
    /// The tooltip with <paramref name="text"/> over <paramref name="over"/>, or none; over
    /// <paramref name="part"/> of it where given (in the item's own px), a status icon of a folder's button (§382).
    /// </summary>
    public void Tooltip(Gfx gfx, string? text, Hit? over, RectF? part = null)
    {
        if (!Ready)
        {
            return;
        }
        if (string.IsNullOrEmpty(text) || over is not { } hit || layout.ItemRect(hit).IsEmpty)
        {
            HideTip();
            return;
        }
        ShowTip(text, hit, part);
    }

    /// <summary>
    /// Slides the whole dock in or out, along its edge's direction. An update leaves it as it is, a slide
    /// going on too (§358): a bar laid out again while it comes in out of hiding goes on coming in.
    /// </summary>
    public void Present(bool shown, bool animate)
    {
        if (!Ready)
        {
            return;
        }
        presented = shown;
        if (shown)
        {
            root.IsVisible = true;
        }
        if (animate && look.Animations)
        {
            var slide = compositor.CreateVector3KeyFrameAnimation();
            slide.InsertKeyFrame(1f, shown ? origin : origin + hiddenOffset, shown ? decelerate : accelerate);
            slide.Duration = Time(shown ? MotionKind.Show : MotionKind.Hide);
            root.StartAnimation("Offset", slide);
            slide.Dispose();
            var fade = KeyTo(compositor, shown ? 1f : 0f, shown ? decelerate : accelerate, Time(shown ? MotionKind.Show : MotionKind.Hide));
            root.StartAnimation("Opacity", fade);
            fade.Dispose();
            return;
        }
        root.StopAnimation("Offset");
        root.StopAnimation("Opacity");
        root.Offset = shown ? origin : origin + hiddenOffset;
        root.Opacity = shown ? 1f : 0f;
        if (!shown)
        {
            root.IsVisible = false;
        }
    }

    /// <summary>Once slid out of sight, nothing is left for DWM to compose.</summary>
    public void Conceal()
    {
        if (!disposed)
        {
            root.IsVisible = false;
        }
    }

    /// <summary>Laid out at least once, and not let go of: what every public way in asks.</summary>
    private bool Ready => !disposed && updated;

    public void SetMark(int index, Mark mark)
    {
        if (!Ready || (uint)index >= (uint)pills.Count)
        {
            return;
        }
        marks[index] = mark;
        var (length, brush) = mark.Attention
            ? (U(16f), attention)
            : mark.Active
                ? (U(16f), active)
                : mark.Count switch
                {
                    0 => (0f, running),
                    1 => (U(6f), running),
                    _ => (U(10f), running),
                };
        var thickness = layout.Pill;
        var size = layout.Vertical ? new Vector2(thickness, length) : new Vector2(length, thickness);
        var centre = pillCentres[index];
        var pill = pills[index];
        pill.Brush = brush;
        // Offset and size animate together, so the pill stays centred while
        // it changes length; its rounded clip changes size with it.
        pill.Size = size;
        if (PillShape(pill) is { } shape)
        {
            shape.Size = size;
        }
        // Half-pixel positions round away from zero, as XAML's layout
        // rounding puts the taskbar's own pills.
        pill.Offset = new Vector3(Round(centre.X - size.X / 2f), Round(centre.Y - size.Y / 2f), 0f);

        var back = backs[index];
        var fill = mark.Attention ? (tokens.AttentionBack is null ? null : attentionBack) : mark.Active ? (tokens.ActiveBack is null ? null : activeBack) : null;
        // A background on its way out keeps its colour while it fades.
        if (fill is not null)
        {
            back.Dress(layout.Corner(U(Layout.HighlightRadiusDip)), fill, tokens.ActiveStroke is null ? null : activeStroke, tokens.ActiveLine is null ? null : activeLine, tokens.LineOnTop, layout.Border);
        }
        back.Visual.Opacity = fill is null ? 0f : 1f;
        ShowSignal(index, mark.Signal);
    }

    /// <summary>
    /// folders: the folder keyed <paramref name="key"/> stands open, its face on this bar lit until it
    /// closes (the person, 2026-10-28: "the folder that is open in karpo should be highlighted in some
    /// way"); null: none is. Windows' language for a button whose flyout is open, and the focused app's:
    /// the button's plate, SubtleFillColorSecondary with its elevation border, over the face's whole card
    /// in its corners. The pointer's highlight lights over it by the step it lights the focused app by
    /// (<see cref="ShadeOf"/>), so hover and press still read. Kept through every update; none while the
    /// bar is arranged, which has edit mode's own marks. §376.
    /// </summary>
    public void Open(string? key)
    {
        openKey = key;
        if (Ready)
        {
            PlaceOpen();
        }
    }

    /// <summary>tests: the open folder's plate, where it stands, and its fill; null where none shows.</summary>
    internal (RectF Rect, Color Fill)? OpenShown => openPlate.Visual.IsVisible
        ? (new RectF(openPlate.Visual.Offset.X, openPlate.Visual.Offset.Y, openPlate.Visual.Size.X, openPlate.Visual.Size.Y), openBack.Color)
        : null;

    /// <summary>The open folder's plate where its face stands now, in its colours now (<see cref="Open"/>).</summary>
    private void PlaceOpen()
    {
        openPart = openKey is null || layout.Editing ? -1 : layout.PartIndex(openKey);
        var rect = openPart >= 0 ? layout.CardRect(openPart) : default;
        if (openPart >= 0 && rect.IsEmpty)
        {
            rect = layout.BlockRect(openPart);
        }
        if (rect.IsEmpty)
        {
            openPart = -1;
            openPlate.Visual.IsVisible = false;
            LightPlates();
            return;
        }
        // In its part's colours (§311): a highlight of its own is its focused plate too (Theme). High
        // contrast has no plate: its ring in the highlight's colour. §506: over a plate at rest, the plate drawn
        // stronger by the open plate's fill (PlateShade), and this one its edge alone.
        var mine = tokens;
        openBack.Color = restPlates.ContainsKey(openPart) ? WithAlpha(mine.Hover, 0f) : mine.ActiveBack ?? WithAlpha(mine.Hover, 0f);
        LightPlates();
        var stroke = mine.ActiveStroke ?? (mine.HoverOutline ? mine.Hover : null);
        openStroke.Color = stroke ?? default;
        openLine.Color = mine.ActiveLine ?? default;
        openPlate.Move(rect);
        // The corners the pointer's highlight lights the face in (Dress).
        openPlate.Dress(layout.PlateRadius, openBack, stroke is null ? null : openStroke, mine.ActiveLine is null ? null : openLine, mine.LineOnTop,
            mine.HoverOutline ? 2f * layout.Border : layout.Border);
        openPlate.Visual.IsVisible = true;
    }

    /// <summary>
    /// perf: new pictures for the tray icons of a tray that lays out as the one built
    /// (<see cref="TrayShape.Same"/>): each icon whose picture changed is painted again into its
    /// own surface, and nothing is built again. A live icon (Task Manager's CPU meter, a sync
    /// animation) sends a picture every second, and a rebuild of every bar for each was most of
    /// what an idle dock cost. <paramref name="trayOf"/> is what the scene was built with, newer: each
    /// tray part's icons by its key (Content.TrayOf).
    /// </summary>
    public void SetTray(Gfx gfx, Func<string, IReadOnlyList<TrayIcon>> trayOf)
    {
        if (!Ready)
        {
            return;
        }
        // tray: every icon of a tray's list was drawn (Content.TrayOf is the split's answer), so the
        // index walks them all — skipping Windows' own hidden ones here painted the wrong surfaces —,
        // in each tray of the bar, its own.
        foreach (var part in trayKeys.Keys)
        {
            if (part < 0 || part >= layout.Specs.Count)
            {
                continue;
            }
            var index = 0;
            foreach (var icon in trayOf(layout.Specs[part].Key))
            {
                if (icon.Icon is { } pixels && trayPictures.TryGetValue((part, index), out var drawn) && !ReferenceEquals(drawn.Pixels, pixels))
                {
                    gfx.RedrawIcon(drawn.Surface, pixels);
                    drawn.Pixels = pixels;
                }
                index++;
            }
        }
    }

    /// <summary>
    /// Shows <paramref name="view"/> on a bar in edit mode (a layout made with
    /// editing on): hover, focus and badge states, and while a block is
    /// dragged, the groups' regions with the target lit, the block at the
    /// pointer over everything and the others moving aside to open a gap where
    /// it would land. Cheap: nothing is rebuilt. Null shows the blocks at rest.
    /// </summary>
    public void Edit(EditView? view)
    {
        if (Ready && layout.Editing)
        {
            Apply(view, look.Animations);
        }
    }

    /// <summary>
    /// pins: what a drag on a bar that is not in edit mode shows — a pin moved along the bar, a
    /// widget dragged out of a folder, files dropped on it: the dragged block follows the pointer
    /// and the others open a gap where it would land (as in edit mode), the widget it would go into,
    /// or make a folder with, is outlined in the accent, and <see cref="EditView.Insert"/> marks
    /// where something dropped would land. Null puts everything back.
    /// </summary>
    public void Drag(Gfx gfx, EditView? view)
    {
        if (!Ready || layout.Editing)
        {
            return;
        }
        var c = compositor;
        if (dragTarget is null)
        {
            dragFill = Own(c.CreateColorBrush(tokens.ZoneTargetFill));
            dragStroke = Own(c.CreateColorBrush(tokens.ZoneTargetStroke));
            dragTarget = new Rounded(c, owned);
            dragTarget.Visual.IsVisible = false;
            blocks.Children.InsertAtTop(dragTarget.Visual);
            dragLine = new Rounded(c, owned);
            dragLine.Visual.IsVisible = false;
            blocks.Children.InsertAtTop(dragLine.Visual);
            dragLanding = new Rounded(c, owned);
            dragLanding.Visual.IsVisible = false;
            blocks.Children.InsertAtTop(dragLanding.Visual);
        }
        var dragging = view?.Dragging is int moving && layout.Placed(moving) && moving < parts.Length && parts[moving] is not null ? moving : -1;
        var target = view?.DropTile is int onto ? layout.TileCell(onto)
            : view?.DropFolder is int into && into >= 0 && into < parts.Length && layout.Placed(into) ? layout.BlockRect(into)
            : default;
        var radius = layout.Corner(MathF.Max(layout.CardRadius, Px(MinRadiusDip)));
        dragTarget.Visual.IsVisible = !target.IsEmpty;
        if (!target.IsEmpty)
        {
            dragTarget.Place(target, radius, null, dragStroke, 2f * layout.Stroke);
        }
        var mark = view?.Insert is { } at ? layout.InsertionRect(at) : default;
        dragLine!.Visual.IsVisible = !mark.IsEmpty;
        if (!mark.IsEmpty)
        {
            dragLine.Place(mark, MathF.Min(mark.Width, mark.Height) / 2f, dragStroke, null, 0f);
        }
        // pins: files over the bar: the new pin's own cell, where it will stand and as big as it will
        // be, in the target's wash and ring — one cell — and its section opening the room it will
        // take, as it does for a block being carried.
        var landing = view?.Landing is { IsEmpty: false } cell ? cell : default;
        dragLanding!.Visual.IsVisible = !landing.IsEmpty;
        if (!landing.IsEmpty)
        {
            dragLanding.Place(landing, radius, dragFill, dragStroke, 2f * layout.Stroke);
        }
        if (dragging < 0 && !landing.IsEmpty && view!.Lefts is { } room && room.Length == parts.Length)
        {
            for (var part = 0; part < parts.Length; part++)
            {
                if (parts[part] is not null && layout.Placed(part))
                {
                    Shift(part, room[part] - layout.PartAt(part).Left, look.Animations);
                }
            }
            return;
        }
        if (dragging >= 0)
        {
            Lift(dragging);
            var lefts = layout.Arrange(dragging, view!.DropIndex);
            for (var part = 0; part < parts.Length; part++)
            {
                if (parts[part] is null || !layout.Placed(part))
                {
                    continue;
                }
                var rest = layout.PartAt(part).Left;
                if (part == dragging)
                {
                    var width = layout.PartAt(part).Width;
                    var along = Math.Clamp(view.PointerAlong - view.GrabOffset, 0f, MathF.Max(0f, layout.Length - width));
                    Shift(part, along - rest, false);
                }
                else
                {
                    Shift(part, lefts[part] - rest, look.Animations);
                }
            }
            return;
        }
        Lower();
        for (var part = 0; part < parts.Length; part++)
        {
            Shift(part, 0f, look.Animations);
        }
    }

    /// <summary>
    /// tray: a tray icon being dragged between the bar and the overflow (Dock.Tray.cs): the shown
    /// icons make room at <paramref name="view"/>'s index, and the icon taken from this bar is out of
    /// sight, carried under the pointer in a window of its own (TrayCarry). A null view puts every
    /// icon back where it stands.
    /// </summary>
    public void TrayDrag(TrayDragView? view) => TrayDrag(view is { } one ? [one] : []);

    /// <summary>
    /// tray: <see cref="TrayDrag(TrayDragView?)"/> for a bar that may hold the tray more than once:
    /// each view is for its tray (<see cref="TrayDragView.Part"/>, the first where it names none) —
    /// the one the pointer is over makes room, the others keep the dragged icon's place open —, and
    /// every tray no view names has its icons back where they stand. None puts every
    /// icon of the bar back.
    /// </summary>
    public void TrayDrag(IReadOnlyList<TrayDragView> views)
    {
        if (!Ready)
        {
            return;
        }
        var now = Environment.TickCount64;
        var first = layout.TrayParts is [var lead, ..] ? lead : -1;
        foreach (var (part, drawn) in trayKeys)
        {
            var view = views.Where(each => (each.Part < 0 ? first : each.Part) == part).Select(each => (TrayDragView?)each).FirstOrDefault();
            if (view is not { } drag)
            {
                // Every icon of this tray glides home from wherever it is by now.
                foreach (var home in drawn.Select(key => TrayGlideKey(part, key)).Where(trayGlides.ContainsKey).ToList())
                {
                    GlideTray(home, 0f, now, true, 1f);
                    trayShifts.Remove(home);
                }
                continue;
            }
            TrayDragIn(part, drawn, drag, now);
        }
    }

    /// <summary>tray: what <paramref name="drag"/> makes of tray part <paramref name="part"/>, whose icons are <paramref name="drawn"/>.</summary>
    private void TrayDragIn(int part, List<string> drawn, TrayDragView drag, long now)
    {
        if (drawn.Count == 0)
        {
            return;
        }
        // tray: one cell's start to the next, so the room a dragged icon opens is a whole slot.
        var cell = layout.TrayStep;
        var starts = new float[drawn.Count];
        for (var index = 0; index < drawn.Count; index++)
        {
            var rect = layout.TrayIconRect(index, part);
            starts[index] = layout.Vertical ? rect.Y : rect.X;
        }
        var held = drag.Key is { } key ? drawn.IndexOf(key) : -1;
        // The others close up without the one being dragged and open a gap where it would land; an
        // icon coming from the overflow — or from the bar's other tray — opens one of its own, the
        // icons after it moving on by a whole cell into the cell the block grows by (Layout.TrayMoreOf).
        // They moved half a cell either way inside a block that kept its length, so the first and the
        // last went past its ends, over the chevron and the next widget.
        var order = Enumerable.Range(0, drawn.Count).Where(index => index != held).ToList();
        var gap = drag.Index >= 0 ? Math.Clamp(drag.Index, 0, order.Count) : -1;
        if (held >= 0 && gap < 0 && drag.More >= 0)
        {
            // Not over the tray: its own place stays open, as it was — unless it is going behind the
            // chevron, which takes its cell away: the others close up.
            gap = Math.Min(held, order.Count);
        }
        // A tray of a set size keeps its length: the icons pushed past its room go behind the chevron
        // for as long as the gap stands, as the ones it has no room for do at rest.
        var holds = layout.TrayHolds(part);
        for (var slot = 0; slot < order.Count; slot++)
        {
            var index = order[slot];
            var takes = gap >= 0 && slot >= gap ? slot + 1 : slot;
            // Its place is free: the others close up, and the gap opens where it would land; one coming
            // from the overflow has no place here, so the icons after the gap move on by a cell.
            var moved = held >= 0 ? starts[Math.Min(takes, starts.Length - 1)] - starts[index]
                : takes > slot ? cell : 0f;
            GlideTray(TrayGlideKey(part, drawn[index]), moved, now, true, takes >= holds ? 0f : 1f);
        }
        if (held >= 0)
        {
            // Out of sight, in the gap it would land in: the icon goes with the pointer in a window of
            // its own, across the bar and up into the overflow (TrayCarry). It used to be held here,
            // along the bar alone. A drop there finds it in its new place; a cancel brings it home.
            var along = gap >= 0 ? starts[Math.Min(gap, starts.Length - 1)] - starts[held] : 0f;
            GlideTray(TrayGlideKey(part, drawn[held]), along, now, false, 0f);
        }
    }

    /// <summary>
    /// tray: what the icon <paramref name="icon"/> of tray part <paramref name="part"/> is known by
    /// while it glides, and its movable after <see cref="TrayPrefix"/>: its tray's key and its own,
    /// so the same icon in a bar's two trays is two things that move, each kept by its own tray from
    /// one scene to the next.
    /// </summary>
    private string TrayGlideKey(int part, string icon) => $"{layout.Specs[part].Key}|{icon}";

    /// <summary>
    /// tests: the glide (<see cref="TrayGlide"/>) of the icon <paramref name="icon"/> of tray part
    /// <paramref name="part"/> along the bar, from its place; null for an icon nothing has slid.
    /// </summary>
    internal TrayGlide? TrayGlideOf(int part, string icon) =>
        trayGlides.TryGetValue(TrayGlideKey(part, icon), out var glide) ? glide : null;

    // tray: what the chevron's movable is registered under, after its tray's key.
    private const string ChevronKey = "chevron";

    // tray: what a tray icon's movable is registered under, before its key.
    private const string TrayPrefix = "tray:";

    /// <summary>
    /// tray: where the icon <paramref name="key"/>, its offset set to <paramref name="offset"/>, is on
    /// screen at <paramref name="now"/> while a drag moves it — its glide's point, not the target the
    /// offset holds; null for an icon no drag has moved.
    /// </summary>
    private Vector3? TrayNow(string key, Vector3 offset, long now)
    {
        if (!trayGlides.TryGetValue(key, out var glide))
        {
            return null;
        }
        var home = offset - (trayShifts.TryGetValue(key, out var set) ? set : Vector3.Zero);
        var along = glide.At(now);
        return home + (layout.Vertical ? new Vector3(0f, along, 0f) : new Vector3(along, 0f, 0f));
    }

    /// <summary>
    /// tray: a scene laid out in the middle of a drag (the chevron coming as it starts, the order a
    /// drop made) takes up the icon <paramref name="key"/> at <paramref name="was"/>, where the last
    /// scene had it, gliding to its place <paramref name="home"/>: so the drag's next move goes on
    /// from there (<see cref="GlideTray"/>), where it used to start from the new place and the icon
    /// jumped to it and back once.
    /// </summary>
    private void CarryTray(string key, Vector3 was, Vector3 home, long now)
    {
        if (TrayGlide.TakenUp(was, home, layout.Vertical, now, (int)look.Motion.Ms(MotionKind.Move)) is { } glide)
        {
            trayGlides[key] = glide;
        }
    }

    /// <summary>
    /// Takes the tray icon <paramref name="key"/> to <paramref name="amount"/> px from its place: a
    /// slide from where it is by now (<see cref="TrayGlide"/>), started only when the target is a new
    /// one, so a drag's every move doesn't restart the others' slides; or at once.
    /// </summary>
    private void GlideTray(string key, float amount, long now, bool animate, float opacity)
    {
        if (!elements.TryGetValue(TrayPrefix + key, out var element))
        {
            return;
        }
        var visual = element.Visual;
        var glide = trayGlides.TryGetValue(key, out var known) ? known : default;
        var home = visual.Offset - (trayShifts.TryGetValue(key, out var set) ? set : Vector3.Zero);
        // One pushed past a tray of a set size goes behind the chevron fading as it slides, and comes
        // back the same; it used to vanish and appear at once. The one the pointer carries goes at once.
        if (visual.Opacity != opacity)
        {
            FadeTo(visual, visual.Opacity, opacity, animate);
        }
        Vector3 Shifted(float by) => layout.Vertical ? new Vector3(0f, by, 0f) : new Vector3(by, 0f, 0f);
        if (!animate || !look.Animations)
        {
            glide.Jump(amount);
            trayGlides[key] = glide;
            trayShifts[key] = Shifted(amount);
            visual.StopAnimation("Offset");
            visual.Offset = home + Shifted(amount);
            return;
        }
        var from = glide.At(now);
        if (!glide.To(amount, now, (int)look.Motion.Ms(MotionKind.Move)))
        {
            return;
        }
        trayGlides[key] = glide;
        trayShifts[key] = Shifted(amount);
        // An icon is placed by its own offset (Put), not in a container of its own as a part is, so
        // the shift goes on top of where each stands (home).
        Slide(visual, home + Shifted(from), home + Shifted(amount), true);
    }

    /// <summary>Puts <paramref name="visual"/> over its siblings.</summary>
    private static void Raise(Visual visual)
    {
        if (visual.Parent is { } parent)
        {
            parent.Children.Remove(visual);
            parent.Children.InsertAtTop(visual);
        }
    }

    /// <summary>
    /// Moves a container from <paramref name="from"/>, where it was last put,
    /// to <paramref name="to"/>: gliding with the Windows easing, or at once,
    /// as a dragged thing follows the pointer. Explicit key frames, set again
    /// on one animation (StartAnimation takes a copy): an implicit animation
    /// set off on a visual DWM hasn't shown yet never runs and leaves it where
    /// it was, which is what a rebuild in the middle of a drag would meet.
    /// </summary>
    private void Slide(Visual container, Vector3 from, Vector3 to, bool animate)
    {
        container.StopAnimation("Offset");
        container.Offset = to;
        if (!animate || !look.Animations || from == to)
        {
            return;
        }
        slider ??= Own(compositor.CreateVector3KeyFrameAnimation());
        slider.InsertKeyFrame(0f, from);
        slider.InsertKeyFrame(1f, to, decelerate);
        slider.Duration = Time(MotionKind.Move);
        container.StartAnimation("Offset", slider);
    }

    /// <summary>
    /// Fades <paramref name="visual"/> from <paramref name="from"/> to
    /// <paramref name="to"/> in a tap's time, with explicit key frames for the
    /// same reason as <see cref="Slide"/>.
    /// </summary>
    private void FadeTo(Visual visual, float from, float to, bool animate)
    {
        visual.StopAnimation("Opacity");
        visual.Opacity = to;
        if (!animate || !look.Animations || from == to)
        {
            return;
        }
        fader ??= Own(compositor.CreateScalarKeyFrameAnimation());
        fader.InsertKeyFrame(0f, from);
        fader.InsertKeyFrame(1f, to, linear);
        fader.Duration = Time(MotionKind.Hover);
        visual.StartAnimation("Opacity", fader);
    }

    /// <summary>
    /// Where part <paramref name="part"/>'s visuals go in <paramref name="layer"/>:
    /// the part's own container in that layer, made the first time the part is
    /// shown and kept while the bar has it (by its key, §358); the layer itself for no part (-1).
    /// </summary>
    private ContainerVisual Into(int part, Layer layer)
    {
        if (part < 0 || part >= parts.Length)
        {
            return layer switch
            {
                Layer.Under or Layer.Back => backgrounds,
                Layer.Front => foreground,
                Layer.RimUnder or Layer.Rim => rims,
                _ => blocks,
            };
        }
        var visuals = Part(part);
        return layer switch
        {
            Layer.Under => visuals.Under,
            Layer.Back => visuals.BackMain,
            Layer.Front => visuals.Front,
            Layer.RimUnder => visuals.RimUnder,
            Layer.Rim => visuals.RimMain,
            _ => visuals.Top,
        };
    }

    /// <summary>Part <paramref name="part"/>'s containers: the ones its key had, else new ones.</summary>
    private PartVisuals Part(int part)
    {
        if (parts[part] is { } known)
        {
            return known;
        }
        // A key two parts share (two widgets of one key) is told apart by its occurrence.
        var key = layout.Specs[part].Key;
        var twice = Enumerable.Range(0, part).Count(earlier => layout.Specs[earlier].Key == key);
        if (twice > 0)
        {
            key = $"{key}'{twice}";
        }
        if (!partViews.TryGetValue(key, out var view))
        {
            var c = compositor;
            var bag = new List<IDisposable>();
            ContainerVisual Container()
            {
                var made = c.CreateContainerVisual();
                bag.Add(made);
                return made;
            }
            view = new PartVisuals
            {
                Back = Container(),
                Under = Container(),
                BackMain = Container(),
                Front = Container(),
                Rim = Container(),
                RimUnder = Container(),
                RimMain = Container(),
                Top = Container(),
                Bag = bag,
            };
            view.Back.Children.InsertAtTop(view.Under);
            view.Back.Children.InsertAtTop(view.BackMain);
            view.Rim.Children.InsertAtTop(view.RimUnder);
            view.Rim.Children.InsertAtTop(view.RimMain);
            backgrounds.Children.InsertAtTop(view.Back);
            foreground.Children.InsertAtTop(view.Front);
            rims.Children.InsertAtTop(view.Rim);
            blocks.Children.InsertAtTop(view.Top);
            partViews[key] = view;
        }
        view.Seen = pass;
        return parts[part] = view;
    }

    /// <summary>Each of a part's containers with the layer it belongs in.</summary>
    private (ContainerVisual Container, ContainerVisual Layer)[] LayersOf(PartVisuals visuals) =>
        [(visuals.Back, backgrounds), (visuals.Front, foreground), (visuals.Rim, rims), (visuals.Top, blocks)];

    /// <summary>
    /// tests (§370): part <paramref name="part"/>'s drawing out of sight, or back: what a picture of the bar
    /// loses with it is what the part draws, which a test holds to where the part takes the pointer.
    /// </summary>
    internal void Hide(int part, bool hidden)
    {
        if (part >= 0 && part < parts.Length && parts[part] is { } visuals)
        {
            foreach (var (container, _) in LayersOf(visuals))
            {
                container.IsVisible = !hidden;
            }
        }
    }

    /// <summary>
    /// edit: where along the bar the block being carried shows in the carry window, from its own
    /// place: what the next update glides it into its slot from once it is let go, as a folder's line
    /// settles from where it was let go (FolderFlyout.Settle) — not from the place it left. Its
    /// visuals here stay at their place, out of sight: the carry window draws them from there.
    /// </summary>
    private Vector3? liftedAt;

    /// <summary>An edit block's corners: a card's, and never square.</summary>
    private float EditRadius => layout.Corner(MathF.Max(layout.CardRadius, Px(MinRadiusDip)));

    /// <summary>
    /// edit: the "+" as <paramref name="view"/> has them — each at rest, under the pointer or pressed —
    /// shown with the bar at rest and out of sight while anything is carried over it or onto it, whose
    /// sections then stand otherwise, or a folder of it stands open (§397); the room they take stays, so
    /// nothing moves as they go and come.
    /// </summary>
    private void ShowAdds(EditView? view, bool shown)
    {
        if (addButtons is not { } buttons)
        {
            return;
        }
        for (var index = 0; index < buttons.Visuals.Count; index++)
        {
            var sprite = buttons.Visuals[index];
            sprite.IsVisible = shown;
            sprite.Brush = view?.AddPressed == index ? buttons.Pressed
                : view?.AddLit == index ? buttons.Hover
                : buttons.Rest;
        }
    }

    /// <summary>
    /// edit: the empty blocks over <paramref name="rooms"/>, the sections that hold nothing as the bar
    /// stands — at rest, or with a carried block's section opened for it. A block already shown moves
    /// and resizes to its new place, gliding as the blocks beside it glide; one that is new appears
    /// where it is, and one that is gone is hidden.
    /// </summary>
    private void ShowEmpty(IReadOnlyList<RectF> rooms, bool animate)
    {
        if (empties is not { } shown || edit is not { } brushes)
        {
            return;
        }
        var c = compositor;
        for (var index = 0; index < rooms.Count; index++)
        {
            var fresh = index >= shown.Rooms.Count;
            if (fresh)
            {
                // The rooms are the empty blocks' own, made as they are first asked for and kept with them.
                var shape = c.CreateRoundedRectangleGeometry();
                var sprite = c.CreateSpriteShape(shape);
                shown.Bag.AddRange([shape, sprite]);
                shown.Holder.Shapes.Add(sprite);
                shown.Rooms.Add((sprite, shape));
            }
            var (room, outline) = shown.Rooms[index];
            var wasShown = room.FillBrush is not null && !fresh;
            room.FillBrush = brushes.Fill;
            room.StrokeBrush = brushes.Outline;
            room.StrokeThickness = layout.Stroke;
            outline.CornerRadius = new Vector2(EditRadius, EditRadius);
            // The stroke lies inside the room, as a block's outline does.
            var half = layout.Stroke / 2f;
            var rect = rooms[index];
            Reshape(outline, new Vector2(rect.X + half, rect.Y + half), new Vector2(MathF.Max(0f, rect.Width - layout.Stroke), MathF.Max(0f, rect.Height - layout.Stroke)), animate && wasShown);
        }
        for (var index = rooms.Count; index < shown.Rooms.Count; index++)
        {
            shown.Rooms[index].Shape.FillBrush = null;
            shown.Rooms[index].Shape.StrokeBrush = null;
        }
    }

    /// <summary>
    /// edit: an empty block's shape to <paramref name="offset"/> and <paramref name="size"/>,
    /// gliding there with the Windows easing in a move's time, as <see cref="Slide"/> moves a part.
    /// </summary>
    private void Reshape(CompositionRoundedRectangleGeometry shape, Vector2 offset, Vector2 size, bool animate)
    {
        // The values last set, not where a glide has got to: a pointer move that changes nothing
        // must not cut a glide short.
        var (fromOffset, fromSize) = (shape.Offset, shape.Size);
        if (fromOffset == offset && fromSize == size)
        {
            return;
        }
        shape.StopAnimation("Offset");
        shape.StopAnimation("Size");
        shape.Offset = offset;
        shape.Size = size;
        if (!animate || !look.Animations)
        {
            return;
        }
        // One animation each, set again for every use (StartAnimation takes a copy), as Slide's.
        var move = reshapeMove ??= Own(compositor.CreateVector2KeyFrameAnimation());
        move.InsertKeyFrame(0f, fromOffset);
        move.InsertKeyFrame(1f, offset, decelerate);
        move.Duration = Time(MotionKind.Move);
        shape.StartAnimation("Offset", move);
        var grow = reshapeGrow ??= Own(compositor.CreateVector2KeyFrameAnimation());
        grow.InsertKeyFrame(0f, fromSize);
        grow.InsertKeyFrame(1f, size, decelerate);
        grow.Duration = Time(MotionKind.Move);
        shape.StartAnimation("Size", grow);
    }

    /// <summary>
    /// Shows <paramref name="view"/>: which block is hovered, focused or has
    /// its badge under the pointer; while a block is dragged, the groups'
    /// regions with the target lit, the block at the pointer over everything
    /// and the others moved to open a gap where it would land.
    /// </summary>
    private void Apply(EditView? view, bool animate)
    {
        if (edit is not { } brushes)
        {
            return;
        }
        view ??= new EditView();
        var dragging = view.Dragging is int moving && layout.Placed(moving) && moving < parts.Length && parts[moving] is not null ? moving : -1;
        // edit: this bar's blocks among several carried together, each placed one with visuals; none
        // on a surface the group goes to without having come from it.
        var group = view.Group is { } carried && view.GroupLanding is not null
            ? carried.Where(part => part >= 0 && part < parts.Length && parts[part] is not null && layout.Placed(part)).ToList()
            : [];
        // edit: the "+" stand while nothing is carried over the bar or onto it, and no folder of it stands open (§397).
        ShowAdds(view, dragging < 0 && (view.Group is null || view.GroupLanding is null) && view.Offered is null && !view.FolderOpen);
        var hair = layout.Stroke;
        for (var part = 0; part < parts.Length; part++)
        {
            if (parts[part] is not { Outline: { } outline } visuals)
            {
                continue;
            }
            // folders: a folder the dragged block would go into is lit as the target.
            var lit = part == dragging || (dragging < 0 && view.Hovered == part) || (dragging >= 0 && view.DropFolder == part);
            if (lit != visuals.Lit)
            {
                FadeTo(visuals.Hover!.Visual, visuals.Lit ? 1f : 0f, lit ? 1f : 0f, animate);
                visuals.Lit = lit;
            }
            outline.Stroke(dragging >= 0 && view.DropFolder == part ? brushes.TargetStroke
                : lit ? brushes.OutlineStrong
                : brushes.Outline);
            // edit: the selected blocks keep their ring, unless they are being carried; a block on the
            // clipboard wears the marching ants instead (MarchingAnts.cs).
            var copied = part != dragging && !group.Contains(part) && view.Copied?.Contains(part) == true;
            visuals.Selected!.Visual.IsVisible = part != dragging && !group.Contains(part) && !copied
                && (view.Selected == part || view.AlsoSelected?.Contains(part) == true);
            visuals.Ants?.Show(copied, new Vector2(visuals.Block.X, visuals.Block.Y), visuals.Block.Size, visuals.Selected.Radius, EditLook.Ring(hair));
            var focused = dragging < 0 && view.Focused == part;
            visuals.FocusOuter!.Visual.IsVisible = focused;
            visuals.FocusInner!.Visual.IsVisible = focused;
            // §593: the add page's ghost, its blue ghost round it (below): fading in, a new part as it always is. §625 whole,
            // exactly as it will stand — faint, the dim under the bar showed through it. §617 the empty ghost none of its
            // drawing: its room alone.
            var ghosted = view.Ghost == part;
            var faint = ghosted && view.GhostEmpty ? 0f : 1f;
            if (ghosted != visuals.Ghosted || faint != visuals.Faint)
            {
                var from = visuals.Ghosted ? visuals.Faint : 0f;
                visuals.Ghosted = ghosted;
                visuals.Faint = faint;
                foreach (var (container, _) in LayersOf(visuals))
                {
                    FadeTo(container, from, faint, animate);
                }
            }
        }
        if (view.Group is not null && view.GroupLanding is { } together)
        {
            // edit: several carried together: each goes with the pointer in the carry window, drawn
            // from where it stands here, so here they stay at their own places, out of sight; the
            // others close up behind them, and where the group would land its ghost stands round them
            // all, the room they take opened (Dock.EditSelect.cs).
            if (group.Count > 0)
            {
                LiftAll(group);
            }
            else
            {
                Lower();
            }
            for (var part = 0; part < parts.Length; part++)
            {
                // A rebuild may have made this bar anew under a drag: what the group's view does not name stays put.
                if (parts[part] is null || !layout.Placed(part) || part >= together.Lefts.Length)
                {
                    continue;
                }
                Shift(part, group.Contains(part) ? 0f : together.Lefts[part] - layout.PartAt(part).Left, animate && !group.Contains(part));
            }
            ShowGhost([together.Rect]);
            ShowEmpty(together.Rooms, animate);
            ShowPanels(together.Panels, animate);
            return;
        }
        if (dragging >= 0)
        {
            // edit: the carried block goes with the pointer in the carry window (Dock.EditCarry.cs),
            // drawn from where it stands here, so here it stays at its own place, out of sight.
            Lift(dragging);
            Shift(dragging, 0f, false);
            if (layout.Placed(dragging))
            {
                var held = layout.PartAt(dragging);
                var along = Math.Clamp(view.PointerAlong - view.GrabOffset, 0f, MathF.Max(0f, layout.Length - held.Width)) - held.Left;
                liftedAt = layout.Vertical ? new Vector3(0f, along, 0f) : new Vector3(along, 0f, 0f);
            }
            if (view.Away)
            {
                // places: over another surface of the display, or a folder standing open, which has
                // the block and shows where it would land: this bar closes up behind it, and shows
                // no ghost.
                ShowGhost([]);
                var closed = layout.Closed(dragging);
                for (var part = 0; part < parts.Length; part++)
                {
                    if (part != dragging && parts[part] is not null && layout.Placed(part))
                    {
                        Shift(part, closed.Lefts[part] - layout.PartAt(part).Left, animate);
                    }
                }
                ShowEmpty(closed.Rooms, animate);
                ShowPanels(closed.Panels, animate);
                return;
            }
            var lefts = layout.Arrange(dragging, view.DropIndex);
            for (var part = 0; part < parts.Length; part++)
            {
                if (part != dragging && parts[part] is not null && layout.Placed(part))
                {
                    Shift(part, lefts[part] - layout.PartAt(part).Left, animate);
                }
            }
            // edit: the ghost in the gap the others open, the room the carried block takes, gliding with them.
            var block = layout.BlockRect(dragging);
            var moved = lefts[dragging] - layout.PartAt(dragging).Left;
            ShowGhost([layout.Vertical ? block with { Y = block.Y + moved } : block with { X = block.X + moved }]);
            // edit: the empty rooms follow the sections as they open and close for the carried block,
            // and so do a floating bar's panels.
            ShowEmpty(layout.EmptyRooms(dragging, view.DropIndex), animate);
            ShowPanels(layout.SectionPanels(dragging, view.DropIndex), animate);
            return;
        }
        Lower();
        if (view.Offered is { } landed)
        {
            // places: a block carried from another surface of the display would land here: the others
            // open the room it will take, and its ghost stands in it at the size it will have here.
            for (var part = 0; part < parts.Length; part++)
            {
                if (parts[part] is not null && layout.Placed(part))
                {
                    Shift(part, landed.Lefts[part] - layout.PartAt(part).Left, animate);
                }
            }
            ShowGhost([landed.Rect]);
            ShowEmpty(landed.Rooms, animate);
            ShowPanels(landed.Panels, animate);
            return;
        }
        // §593: the add page's ghost, laid out where it would go: the blue ghost round its block, as a carried block's; §621 round
        // each of its cards where it wears several, never one round them all. §625 a pick's its outline alone, the widget in it
        // as it will look; the empty one's room washed.
        ShowGhost(view.Ghost is int ghost && ghost < parts.Length && parts[ghost] is not null ? layout.GhostRects(ghost) : [], wash: view.GhostEmpty);
        for (var part = 0; part < parts.Length; part++)
        {
            Shift(part, 0f, animate);
        }
        ShowEmpty(layout.EmptyRooms(), animate);
        ShowPanels(layout.SectionPanels(), animate);
    }

    /// <summary>
    /// edit: the blue ghost of a carried block at <paramref name="rects"/>, the room it takes where it would land
    /// (<see cref="Layout.BlockRect"/>); none for none. §621 the add page's ghost a rectangle a card of what it holds
    /// (<see cref="Layout.GhostRects"/>): each widget outlined on its own, the gaps between them clear. §625 <paramref name="wash"/>:
    /// the accent's wash in it — a room with nothing in it yet; none over a widget, which shows as it will.
    /// </summary>
    private void ShowGhost(IReadOnlyList<RectF> rects, bool wash = true)
    {
        if (landingGhost is not { } shown || edit is not { } brushes)
        {
            return;
        }
        RectF[] now = [.. rects.Where(rect => !rect.IsEmpty)];
        var was = shown.Holder.IsVisible ? shown.Stood : [];
        var ring = EditLook.Ring(layout.Stroke);
        for (var index = 0; index < now.Length; index++)
        {
            if (index >= shown.Boxes.Count)
            {
                // The ghost's rectangles are its own, made as they are first asked for and kept with it.
                var geometry = compositor.CreateRoundedRectangleGeometry();
                var sprite = compositor.CreateSpriteShape(geometry);
                shown.Bag.AddRange([geometry, sprite]);
                shown.Holder.Shapes.Add(sprite);
                shown.Boxes.Add((sprite, geometry));
            }
            var (box, outline) = shown.Boxes[index];
            box.FillBrush = wash ? brushes.TargetFill : null;
            box.StrokeBrush = brushes.TargetStroke;
            box.StrokeThickness = ring;
            // edit: shown already, it glides to its new gap with the blocks giving way, as a folder's ghost does (a move's,
            // Motion.cs); come anew, it is put there. §617 and grows to its new size — the add page's empty ghost into the
            // pick's —, never jumping to it; §621 the one rectangle splitting into a pick's several (EditLook.GhostFrom).
            var from = EditLook.GhostFrom(was, now.Length, index);
            if (from is { } start && was.Length != now.Length)
            {
                Outline(outline, start, animate: false);
            }
            Outline(outline, now[index], animate: from is not null);
        }
        for (var index = now.Length; index < shown.Boxes.Count; index++)
        {
            shown.Boxes[index].Shape.FillBrush = null;
            shown.Boxes[index].Shape.StrokeBrush = null;
        }
        shown.Holder.Size = new Vector2(layout.Width, layout.Height);
        shown.Holder.IsVisible = now.Length > 0;
        shown.Stood = now;

        // The stroke all inside the rectangle, as a block's outline (EditLook.Stroke); the wash to its middle, under it.
        void Outline(CompositionRoundedRectangleGeometry outline, RectF rect, bool animate)
        {
            var (at, size, corner) = EditLook.Stroke(rect.Size, EditRadius, ring);
            Reshape(outline, new Vector2(rect.X, rect.Y) + at, size, animate);
            outline.CornerRadius = new Vector2(corner, corner);
        }
    }

    /// <summary>Moves part <paramref name="part"/> by <paramref name="amount"/> px along the bar from its place.</summary>
    private void Shift(int part, float amount, bool animate)
    {
        if (parts[part] is not { } visuals)
        {
            return;
        }
        var offset = layout.Vertical ? new Vector3(0f, amount, 0f) : new Vector3(amount, 0f, 0f);
        var from = visuals.Shift;
        visuals.Shift = offset;
        foreach (var (container, _) in LayersOf(visuals))
        {
            Slide(container, from, offset, animate);
        }
    }

    /// <summary>
    /// edit: several parts carried together, all lifted over everything as <see cref="Lift"/> lifts
    /// one, the first on the shadow; the carry window draws each (<see cref="CarriedGroup"/>).
    /// </summary>
    private void LiftAll(IReadOnlyList<int> group)
    {
        if (liftedPart == group[0] && liftedMore.SequenceEqual(group.Skip(1)))
        {
            return;
        }
        Lift(group[0]);
        foreach (var part in group.Skip(1))
        {
            if (parts[part] is not { } visuals)
            {
                continue;
            }
            liftedMore.Add(part);
            Ground(visuals, liftedMore.Count);
            foreach (var (container, layer) in LayersOf(visuals))
            {
                layer.Children.Remove(container);
                lifted.Children.InsertAtTop(container);
            }
        }
    }

    /// <summary>
    /// §625 (the person, 2026-11-09: "when having the light system some colors are not actually updated like the ghost preview
    /// background color for example. (or when dragging a widget)"): a block carried in edit mode leaves its bar — the carry
    /// window draws it over the display's dim, black in every mode —, so it takes its bar's ground with it, plate
    /// <paramref name="index"/> of <see cref="liftPlates"/> in the bar's own colour (<see cref="EditBrushes.Ground"/>, dyed with
    /// every update from the bar's look) under its block: on the dim alone its light mode's dark text stood on black. None at
    /// rest, where it is carried along its own bar.
    /// </summary>
    private void Ground(PartVisuals visuals, int index)
    {
        if (!layout.Editing || edit is not { } brushes)
        {
            return;
        }
        while (liftPlates.Count <= index)
        {
            liftPlates.Add(new Rounded(compositor, liftPlateBag));
        }
        var plate = liftPlates[index];
        plate.Place(visuals.Block, EditRadius, brushes.Ground, null, 0f);
        plate.Visual.Parent?.Children.Remove(plate.Visual);
        visuals.Back.Children.InsertAtBottom(plate.Visual);
    }

    /// <summary>§625: the carried blocks' grounds (<see cref="Ground"/>), made as first asked for, kept for the next carry.</summary>
    private readonly List<Rounded> liftPlates = [];
    private readonly List<IDisposable> liftPlateBag = [];

    /// <summary>§625: every carried block's ground taken from under it.</summary>
    private void Unground()
    {
        foreach (var plate in liftPlates)
        {
            plate.Visual.Parent?.Children.Remove(plate.Visual);
        }
    }

    /// <summary>tests: §625 the ground under the carried block, as it is dyed now; null where it stands on none.</summary>
    internal Color? CarriedGround => liftPlates is [{ Visual.Parent: not null }, ..] && edit is { } brushes ? brushes.Ground.Color : null;

    /// <summary>tests: §625 how much of part <paramref name="part"/>'s drawing shows (an add page's ghost whole, the empty one none).</summary>
    internal float Shows(int part) => parts[part] is { } visuals ? visuals.Back.Opacity : 0f;

    /// <summary>tests: §625 the wash of the blue ghost's first rectangle; null for none (a pick's ghost, its outline alone).</summary>
    internal Color? GhostWash => landingGhost is { Holder.IsVisible: true, Boxes: [var (shape, _), ..] } ? (shape.FillBrush as CompositionColorBrush)?.Color : null;

    /// <summary>edit: the parts lifted with <see cref="liftedPart"/>, carried together with it (<see cref="LiftAll"/>).</summary>
    private readonly List<int> liftedMore = [];

    /// <summary>
    /// Puts the dragged part over everything, a little see-through and on a
    /// shadow like a flyout's, following the pointer without easing.
    /// </summary>
    private void Lift(int part)
    {
        if (liftedPart == part && liftedMore.Count == 0 || parts[part] is not { } visuals)
        {
            return;
        }
        Lower();
        liftedPart = part;
        foreach (var (container, layer) in LayersOf(visuals))
        {
            layer.Children.Remove(container);
            lifted.Children.InsertAtTop(container);
        }
        // edit: the carry window shows it at the pointer; the bar keeps it out of sight meanwhile.
        if (layout.Editing)
        {
            carryHolder.Opacity = 0f;
        }
        // §625: on its bar's ground, the shadow under that.
        Ground(visuals, 0);
        if (liftShadow is { } shadow)
        {
            shadow.Resize(visuals.Block.Size);
            shadow.Visual.Offset = new Vector3(visuals.Block.X, visuals.Block.Y, 0f);
            visuals.Back.Children.InsertAtBottom(shadow.Visual);
        }
        lifted.Opacity = DraggedOpacity;
    }

    /// <summary>The lifted part goes back among the others, and glides back into place from where it is.</summary>
    private void Lower()
    {
        Unground();
        // edit: those carried with it first.
        foreach (var part in liftedMore)
        {
            if (parts[part] is not { } more)
            {
                continue;
            }
            foreach (var (container, layer) in LayersOf(more))
            {
                lifted.Children.Remove(container);
                layer.Children.InsertAtTop(container);
            }
        }
        liftedMore.Clear();
        liftedAt = null;
        if (liftedPart < 0 || parts[liftedPart] is not { } visuals)
        {
            liftedPart = -1;
            return;
        }
        liftedPart = -1;
        if (liftShadow is { } shadow)
        {
            visuals.Back.Children.Remove(shadow.Visual);
        }
        foreach (var (container, layer) in LayersOf(visuals))
        {
            lifted.Children.Remove(container);
            layer.Children.InsertAtTop(container);
        }
        lifted.Opacity = 1f;
        carryHolder.Opacity = 1f;
    }

    /// <summary>
    /// Closes every composition object this scene made, its elements' too (§358: the scene goes with
    /// its window). The icons, island layers and tile surfaces it was handed belong to others and stay.
    /// </summary>
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }
        // The tooltip's window goes with the bar's, told nothing more (BarTip).
        TipPlaced = null;
        HideTip();
        disposed = true;
        if (tipHolder?.Parent is { } tips)
        {
            tips.Children.Remove(tipHolder);
        }
        // A window still showing this scene shows nothing rather than a closed tree.
        if (target is not null && ReferenceEquals(target.Root, root))
        {
            target.Root = null;
        }
        else if (host is not null && ReferenceEquals(root.Parent, host))
        {
            host.Children.Remove(root);
        }
        // §612: what fades out with edit mode's dress, let go of with the rest.
        LetFadedGo(all: true);
        foreach (var element in elements.Values)
        {
            Close(element.Bag);
        }
        elements.Clear();
        foreach (var view in partViews.Values)
        {
            Close(view.Bag);
        }
        partViews.Clear();
        foreach (var signal in signals.Values)
        {
            Close(signal.Bag);
        }
        signals.Clear();
        addButtons?.Dispose();
        retiredLooks?.Dispose();
        edit?.Dispose();
        Close(liftShadowBag);
        Close(liftPlateBag);
        Close(tipShadowBag);
        // §637: what a morph made, the old scene's whole.
        Close(morphMade);
        foreach (var (surface, _) in tipTexts.Values)
        {
            surface.Dispose();
        }
        tipTexts.Clear();
        wallpaper?.Dispose();
        wallpaper = null;
        cutShape?.Dispose();
        cutShape = null;
        cutHoles = null;
        cutting = false;
        Close(owned);
        trayPictures.Clear();
        trayKeys.Clear();
        trayGlides.Clear();
        pressables.Clear();
    }

    public static Color Rgba(byte r, byte g, byte b, float a) =>
        Color.FromArgb(Alpha(a), r, g, b);

    /// <summary>§489: how long <paramref name="kind"/> takes now, by the bar's look's motion (Motion.cs): what every animation of the bar runs for.</summary>
    private TimeSpan Time(MotionKind kind) => look.Motion.Time(kind);

    /// <summary>
    /// The tooltip with <paramref name="text"/> over <paramref name="over"/>: faded in only when it was
    /// not up, else its words and place changed where it stands; an update moves it with its item (§358).
    /// </summary>
    private void ShowTip(string text, Hit over, RectF? part)
    {
        if (!Ready || layout.ItemRect(over) is not { IsEmpty: false } whole)
        {
            return;
        }
        var padX = Px(TooltipPadXDip);
        var padY = Px(TooltipPadYDip);
        var px = TextPx(TooltipTextDip);
        // The words drawn so far are for this ink, size and room; another look or scale draws them again.
        var drawnFor = (tokens.TipText, px, layout.TooltipMaxWidth, layout.TooltipMostHeight, gfx.Typeface);
        if (drawnFor != tipTextsFor || tipTexts.Count >= TipTextsKept && !tipTexts.ContainsKey(text))
        {
            foreach (var (said, (surface, _)) in tipTexts.ToList())
            {
                if (tipShown && ReferenceEquals(tipTextBrush.Surface, surface) && drawnFor == tipTextsFor)
                {
                    continue;
                }
                if (ReferenceEquals(tipTextBrush.Surface, surface))
                {
                    tipTextBrush.Surface = null;
                }
                surface.Dispose();
                tipTexts.Remove(said);
            }
            tipTextsFor = drawnFor;
        }
        if (!tipTexts.TryGetValue(text, out var words))
        {
            // As many of its lines as the room beside the bar holds (Layout.TooltipMostHeight).
            var shown = BarTooltips.Fitted(text, layout.TooltipMostHeight - 2f * padY,
                lines => PopupPaint.Measure(gfx, lines, px, Weight.Regular, layout.TooltipMaxWidth).Y);
            words = gfx.Text(shown, px, Weight.Regular, Ink(tokens.TipText), layout.TooltipMaxWidth);
            drawn++;
            tipTexts[text] = words;
        }
        var size = new Vector2(words.Size.X + 2f * padX, words.Size.Y + 2f * padY);
        (tipOver, tipSize, tipPart) = (over, size, part);
        var item = Within(whole, part);
        tipTextBrush.Surface = words.Surface;
        Put(tipText, new RectF(padX, padY, words.Size.X, words.Size.Y));
        Put(tip, layout.TooltipAnchor(item, size));
        tipBack.Place(new RectF(0f, 0f, size.X, size.Y), Px(TooltipRadiusDip), tipFill, tipStroke, tokens.Line);
        // Its shadow, where the look has one, made again only for another scale or look.
        var shadowFor = (Px(TooltipRadiusDip), 8f * layout.Scale, 2f * layout.Scale, tokens.TipShadow);
        if (tokens.TipShadow <= 0f || tipShadowMade != shadowFor)
        {
            tipShadow?.Visual.Parent?.Children.Remove(tipShadow.Visual);
            tipShadow = null;
            Close(tipShadowBag);
        }
        if (tokens.TipShadow > 0f && tipShadow is null)
        {
            tipShadow = new Shadow(compositor, tipShadowBag, shadowFor.Item1, shadowFor.Item2, shadowFor.Item3, shadowFor.TipShadow);
            tipShadowMade = shadowFor;
            tip.Children.InsertAtBottom(tipShadow.Visual);
        }
        tipShadow?.Resize(size);
        if (!tipShown)
        {
            tipShown = true;
            tip.IsVisible = true;
            Fade(tip, true);
        }
        PlaceTip();
    }

    /// <summary><paramref name="part"/> of <paramref name="item"/>, given in the item's own px; the whole item for none.</summary>
    private static RectF Within(RectF item, RectF? part) =>
        part is { } inside ? new RectF(item.X + inside.X, item.Y + inside.Y, inside.Width, inside.Height) : item;

    private void HideTip()
    {
        if (disposed)
        {
            return;
        }
        var was = tipShown;
        tipShown = false;
        tipOver = null;
        tipPart = null;
        tip.StopAnimation("Opacity");
        tip.IsVisible = false;
        if (was)
        {
            PlaceTip();
        }
    }

    /// <summary>
    /// §359, §360: where the tooltip stands in its own window (BarTip), which stands where the bar's does:
    /// the tooltip and its shadow, in that window's px, as it comes, moves and goes (null: gone). It is the
    /// window's region — all the window shows and all of it that takes the pointer —, so the window,
    /// which spans the bar's, never takes a press meant for the bar or for what is beside it.
    /// </summary>
    internal Action<RectF?>? TipPlaced { get; set; }

    /// <summary>
    /// Tells <see cref="TipPlaced"/> where the tooltip stands now: round it as far as its shadow reaches,
    /// and never over the bar's panel, which is the bar's to take (the shadow cut there falls on the panel).
    /// </summary>
    private void PlaceTip()
    {
        if (TipPlaced is not { } placed)
        {
            return;
        }
        if (!tipShown)
        {
            placed(null);
            return;
        }
        var spread = tipShadow is null ? 0f : MathF.Ceiling(tipShadowMade.Blur + MathF.Abs(tipShadowMade.Drop));
        var (left, top) = (origin.X + tip.Offset.X - spread, origin.Y + tip.Offset.Y - spread);
        var (right, bottom) = (origin.X + tip.Offset.X + tipSize.X + spread, origin.Y + tip.Offset.Y + tipSize.Y + spread);
        switch (layout.Edge)
        {
            case Edge.Top:
                top = MathF.Max(top, origin.Y + layout.PanelBottom);
                break;
            case Edge.Left:
                left = MathF.Max(left, origin.X + layout.PanelRight);
                break;
            case Edge.Right:
                right = MathF.Min(right, origin.X + layout.PanelLeft);
                break;
            default:
                bottom = MathF.Min(bottom, origin.Y + layout.PanelTop);
                break;
        }
        placed(new RectF(left, top, MathF.Max(0f, right - left), MathF.Max(0f, bottom - top)));
    }

    /// <summary>Tests only: where the tooltip stands, in the layout's px; null while none does.</summary>
    internal RectF? TipRect => tipShown ? new RectF(tip.Offset.X, tip.Offset.Y, tipSize.X, tipSize.Y) : null;

    /// <summary>
    /// A cell of an island <paramref name="size"/> big, of <paramref name="layers"/>: a clipped container
    /// holding the layers the island keeps redrawing, some of them gliding on the island's own animation.
    /// </summary>
    private ContainerVisual IslandCell(Compositor c, IReadOnlyList<IslandLayer> layers, Vector2 size)
    {
        var cell = Own(c.CreateContainerVisual());
        cell.Size = size;
        cell.Clip = Own(c.CreateInsetClip());
        foreach (var layer in layers)
        {
            // A layer shows its own rectangle only. Its surface, which may be larger (a graph's box
            // and one sample step), is drawn 1:1 from the rectangle's top left: a surface a fraction
            // of a pixel off would otherwise be stretched and blurred.
            var holder = Own(c.CreateContainerVisual());
            holder.Offset = new Vector3(layer.Offset, 0f);
            holder.Size = layer.Size;
            holder.Clip = Own(c.CreateInsetClip());
            var brush = Own(c.CreateSurfaceBrush(layer.Surface));
            brush.Stretch = CompositionStretch.None;
            brush.HorizontalAlignmentRatio = 0f;
            brush.VerticalAlignmentRatio = 0f;
            var sprite = Own(c.CreateSpriteVisual());
            sprite.Brush = brush;
            var surfaceSize = layer.Surface.SizeInt32;
            sprite.Size = new Vector2(surfaceSize.Width, surfaceSize.Height);
            if (layer.Scroll is { } scroll)
            {
                Glide(sprite, scroll);
            }
            holder.Children.InsertAtTop(sprite);
            cell.Children.InsertAtTop(holder);
        }
        return cell;
    }

    /// <summary>
    /// Binds a layer's surface to the island's scroll: it moves left inside
    /// its rectangle by the property set's "x" without this process drawing a
    /// frame. A set without "x" leaves the surface where it is.
    /// </summary>
    private void Glide(SpriteVisual sprite, CompositionPropertySet scroll)
    {
        var glide = Own(compositor.CreateExpressionAnimation("Vector3(-scroll.x, 0, 0)")); // not-ui: an expression
        glide.SetReferenceParameter("scroll", scroll);
        try
        {
            sprite.StartAnimation("Offset", glide);
        }
        catch (ArgumentException)
        {
        }
    }

    /// <summary>The Start button <paramref name="hit"/> names: Windows' logo in its well, pressable and kept; nothing where the bar placed none.</summary>
    private void StartLogo(Hit hit, string key)
    {
        if (layout.IconBox(hit, U(ButtonGlyphBoxDip)) is not { IsEmpty: false } startBox)
        {
            return;
        }
        // Gfx draws the logo at half its surface, centred. The taskbar's
        // sits at the bottom left of its icon box, where a half pixel
        // falls, so it is centred rounding left and down.
        var size = Round(2f * LogoDip * layout.Unit);
        var dark = look.Dark;
        var logo = Keep(key, Into(layout.PartOf(hit), Layer.Front), new Sig("start", size, dark),
            () =>
            {
                var sprite = Own(compositor.CreateSpriteVisual());
                sprite.Brush = Own(compositor.CreateSurfaceBrush(Drew(gfx.WindowsLogo(size, dark))));
                return sprite;
            },
            sprite => Put(sprite, new RectF(
                startBox.X + MathF.Floor((startBox.Width - size) / 2f),
                startBox.Y + MathF.Ceiling((startBox.Height - size) / 2f),
                size,
                size)));
        Pressable(hit, logo, key);
    }

    /// <summary>
    /// §497: the glyph of one of the bar's glyph buttons (<see cref="BarEntries.IsWindowsButton"/>: the Snipping Tool,
    /// Settings, the pen menu, the touch keyboard, the virtual touchpad), px: a face's icon (<see cref="Fit.Glyph()"/>,
    /// which a folder's icon face draws its glyph at), in a box its own size — so a glyph button stands among the faces
    /// at their size, not at the 20 DIP in 24 of search's and Task view's.
    /// </summary>
    internal static float FaceGlyph(Layout layout) => Fit.SystemIconFor(layout.CellSide, layout.Scale);

    /// <summary>
    /// One of Windows' buttons: its glyph in its well, <paramref name="glyph"/> px in a box its own size (search's and
    /// Task view's 20 DIP in 24 where 0), in the words' ink or <paramref name="ink"/>, pressable and kept; null where the
    /// bar placed none.
    /// </summary>
    private SpriteVisual? GlyphButton(Hit hit, string code, string key, float glyph = 0f, Color? ink = null)
    {
        if (layout.IconBox(hit, glyph > 0f ? glyph : U(ButtonGlyphBoxDip)) is not { IsEmpty: false } box)
        {
            return null;
        }
        var sprite = GlyphSprite(key, Into(layout.PartOf(hit), Layer.Front), box, code, glyph > 0f ? glyph : U(ButtonGlyphDip), ink ?? tokens.Text);
        Pressable(hit, sprite, key);
        return sprite;
    }

    /// <summary>
    /// search: Windows' search button as Windows 11's search box (SearchPill): the box with the
    /// magnifier and "Search" in it, drawn once into a surface the size of the box, and moved as the
    /// button's glyph is; on its part's plate (<see cref="SearchPlateFor"/>).
    /// </summary>
    private unsafe void SearchBox(Hit hit, string key)
    {
        if (layout.ItemRect(hit) is not { IsEmpty: false } box)
        {
            return;
        }
        var part = layout.PartOf(hit);
        var plate = SearchPlateFor(part, box);
        var cell = layout.CellSide;
        var hairline = layout.Stroke;
        var shown = look with { Colors = default, Animations = false };
        var words = Keep(key, Into(part, Layer.Front), new Sig("search", box.Width, box.Height, cell, hairline, shown),
            () =>
            {
                var (sprite, brush) = TextSprite(compositor);
                ICompositionSurface Words(SearchBoxState state) => Drew(gfx.Draw(box.Width, box.Height, dc =>
                    SearchPill.Paint(dc, gfx, new D2D_RECT_F(0, 0, box.Width, box.Height), cell, hairline, shown, SearchPill.WindowsPlaceholder, state, plate: false)));
                return new SearchWords(sprite, brush, Words(SearchBoxState.Rest), Words(SearchBoxState.Pressed));
            },
            made => Put(made.Sprite, box));
        plate.Words = words;
        DressPlate(part, plate, animate: false);
        // Not pressable: Windows' search box does not shrink under a press, its fill changes.
        Known(hit, key);
    }

    /// <summary>search: a search box's words, at rest and pressed, and the sprite that shows one or the other.</summary>
    private sealed record SearchWords(SpriteVisual Sprite, CompositionSurfaceBrush Brush, ICompositionSurface Rest, ICompositionSurface Pressed) : IShown
    {
        public Visual Visual => Sprite;
    }

    /// <summary>
    /// A glyph in a sprite at <paramref name="box"/>, kept as <paramref name="key"/> in <paramref name="into"/>
    /// while its glyph, size and ink are the same.
    /// </summary>
    private SpriteVisual GlyphSprite(string key, ContainerVisual into, RectF box, string code, float px, Color color) =>
        Keep(key, into, new Sig("glyph", code, px, color, box.Width),
            () =>
            {
                var (sprite, brush) = TextSprite(compositor);
                brush.Surface = Drew(gfx.Glyph(code, px, Ink(color), box.Width));
                return sprite;
            },
            sprite => Put(sprite, box));

    /// <summary>A sprite over a surface brush that draws its surface 1:1.</summary>
    private (SpriteVisual Sprite, CompositionSurfaceBrush Brush) TextSprite(Compositor c)
    {
        var brush = Own(c.CreateSurfaceBrush());
        brush.Stretch = CompositionStretch.None;
        var sprite = Own(c.CreateSpriteVisual());
        sprite.Brush = brush;
        return (sprite, brush);
    }

    /// <summary>tests: the corners of the pointer's highlight and of the keyboard's ring, as last placed.</summary>
    internal (float Highlight, float Focus) Corners => (highlight.Radius, keyOuter.Radius);

    /// <summary>
    /// tests: the empty blocks shown right now, in window pixels — each shown room's shape where it
    /// was last put, its outline's half-stroke inset given back — and none for a bar at rest.
    /// </summary>
    internal IReadOnlyList<RectF> EmptyBlocksShown() => empties is not { } shown
        ? []
        : [.. shown.Rooms
            .Where(room => room.Shape.FillBrush is not null)
            .Select(room => new RectF(
                room.Geometry.Offset.X - layout.Stroke / 2f,
                room.Geometry.Offset.Y - layout.Stroke / 2f,
                room.Geometry.Size.X + layout.Stroke,
                room.Geometry.Size.Y + layout.Stroke))];

    /// <summary>
    /// tests: the "+" shown right now, each where its button stands in window px and how it looks —
    /// 0 at rest, 1 under the pointer, 2 pressed — and none at rest or while anything is carried.
    /// </summary>
    internal IReadOnlyList<(RectF Rect, int Look)> AddsShown() => addButtons is not { } buttons
        ? []
        : [.. buttons.Visuals
            .Where(sprite => sprite.IsVisible)
            .Select(sprite => (
                new RectF(sprite.Offset.X, sprite.Offset.Y, sprite.Size.X, sprite.Size.Y),
                sprite.Brush == buttons.Pressed ? 2 : sprite.Brush == buttons.Hover ? 1 : 0))];

    /// <summary>tests: where the blue ghost of a carried block stands (§621 the room its rectangles take), or null where none shows.</summary>
    internal RectF? GhostShown => GhostsShown is { Count: > 0 } shown ? EditLook.Around(shown) : null;

    /// <summary>tests: §621 each rectangle of the blue ghost, where it stands; none where none shows.</summary>
    internal IReadOnlyList<RectF> GhostsShown => landingGhost is { Holder.IsVisible: true } shown ? shown.Stood : [];

    /// <summary>
    /// edit: the block being carried, for the carry window to draw at the pointer (Dock.EditCarry.cs):
    /// the visual it is lifted into and its block there, in window pixels. Null when none is carried.
    /// The carry window draws it through a visual surface, which the bar's own hiding of it does not reach.
    /// </summary>
    public (Visual Source, RectF Block)? Carried() =>
        !disposed && liftedPart >= 0 && parts[liftedPart] is { } visuals ? (lifted, visuals.Block) : null;

    /// <summary>
    /// edit: the blocks being carried together from this bar (<see cref="EditView.Group"/>), by their
    /// keys, for the carry window to draw each at the pointer: the visual they are lifted into and
    /// each one's block there, in window pixels. Empty when none is.
    /// </summary>
    public IReadOnlyList<(string Key, Visual Source, RectF Block)> CarriedGroup()
    {
        if (disposed || liftedPart < 0)
        {
            return [];
        }
        var found = new List<(string, Visual, RectF)>();
        foreach (var part in liftedMore.Prepend(liftedPart))
        {
            if (parts[part] is { } visuals)
            {
                found.Add((layout.Specs[part].Key, lifted, visuals.Block));
            }
        }
        return found;
    }


    /// <summary>
    /// The colours somebody chose that the bar's widgets are drawn in, each Automatic one null — §567 the Appearance's, every
    /// widget's alike —; none in high contrast. What a card's pattern and a search box's plate (<see cref="PlateInks"/>) read.
    /// </summary>
    private Palette Chosen => look.HighContrast ? default : look.Colors;


    /// <summary>
    /// Whether the pointer over a cell of <paramref name="kind"/> lights that cell alone rather than
    /// the widget's whole card. The status icons: each is a button in its own right, standing where
    /// Windows' own sit among the network, sound and battery icons, and each of those lights its own
    /// square, so a row of status icons lighting up together under one icon was the odd one out. A
    /// press already shrinks only the cell under it (<see cref="Pressable"/>).
    /// </summary>
    internal static bool LightsItsOwnCell(BarKind kind) => kind is BarKind.Indicators;


    /// <summary>
    /// Part <paramref name="part"/>'s card (<see cref="PlaceCard"/>), kept as <paramref name="key"/>: its
    /// fill in the part's back layer and its outline, the same shape, in its rim layer over the highlight
    /// and what stands on the card (<see cref="rims"/>, §325), so a lit or pressed widget keeps its
    /// outline's colour. Every card is drawn so, a search box's plate too (<see cref="SearchPlateFor"/>).
    /// Its colours are swatches of the part's tokens, set again in place; a fill or an outline coming or
    /// going makes it again. §462, §512, §515: over its fill the part's pattern, inside its own card, a sheet of its own laid
    /// from the card's corner.
    /// </summary>
    private void Card(int part, string key, RectF rect, bool filled, bool stroked, float radius, GlassPart? joined = null)
    {
        var c = compositor;
        var pattern = Chosen.CardPattern;
        // §704: a Liquid glass card its glow and lens over its tint, and its rim round it, whatever outline it says; cards that
        // meet one glass shape (LiquidGlass.Join): its light and its rim round the whole of it, cut to each card, a hairline
        // between two.
        var glass = filled ? look.Liquid : null;
        (Vector2 At, Vector2 Size)? body = glass is not null && joined is { Body: var whole }
            && (MathF.Abs((float)whole.Width - rect.Width) > 0.5f || MathF.Abs((float)whole.Height - rect.Height) > 0.5f)
            ? (new Vector2((float)whole.Left - rect.X, (float)whole.Top - rect.Y), new Vector2((float)whole.Width, (float)whole.Height))
            : null;
        (Vector2 At, Vector2 Size)? divider = glass is not null && joined is { Divider: { } line }
            ? (new Vector2((float)line.Left - rect.X, (float)line.Top - rect.Y), new Vector2((float)line.Width, (float)line.Height))
            : null;
        var card = Keep(key, Into(part, Layer.Back), new Sig("card", filled, glass),
            () => new Shaped(new Rounded(c, sink), filled ? Swatch(t => t.CardFill ?? default) : null),
            made =>
            {
                made.Shape.Joined(c, sink, body);
                PlaceCard(made.Shape, rect, made.Fill, null, radius);
                made.Shape.Pattern(c, sink, gfx, pattern, layout.Scale, Vector2.Zero);
                if (glass is not null)
                {
                    made.Shape.Glazed(c, sink, glass, layout.Scale);
                }
            },
            glides: false);
        GlideCard(key, card, rect);
        // §464: each filled card its own surface over the desktop (DressCards): no bar under it, §547 the wallpaper blurred where it is see-through.
        if (filled)
        {
            placedCards.Add(new PlacedCard(part, card.Shape, rect, radius));
        }
        if (glass is not null)
        {
            var rim = Keep($"{key}:rim", Into(part, Layer.Rim), new Sig("glass-rim", glass),
                () => new Shaped(new Rounded(c, sink), null, Glaze.Over(c, sink, glass)),
                made =>
                {
                    made.Shape.Joined(c, sink, body);
                    PlaceCard(made.Shape, rect, null, made.Stroke, radius);
                    made.Shape.Rimmed((CompositionLinearGradientBrush)made.Stroke!);
                    made.Shape.Divided(c, sink, GlassPaint.DividerInk(glass), divider);
                },
                glides: false);
            GlideCard($"{key}:rim", rim, rect);
        }
        else if (stroked)
        {
            var rim = Keep($"{key}:rim", Into(part, Layer.Rim), new Sig("rim"),
                () => new Shaped(new Rounded(c, sink), null, Swatch(t => t.CardStroke ?? default)),
                made => PlaceCard(made.Shape, rect, null, made.Stroke, radius),
                glides: false);
            GlideCard($"{key}:rim", rim, rect);
        }
    }

    /// <summary>
    /// A card kept by this update glides from where it showed to where it shows now, as its part moved and grew.
    /// </summary>
    private void GlideCard(string key, Shaped card, RectF shows)
    {
        var was = card.Shows;
        card.Shows = shows;
        if (!glide || !Kept(key) || was.IsEmpty || !card.Visual.IsVisible)
        {
            return;
        }
        var visual = card.Visual;
        var moved = new Vector3(shows.X - was.X, shows.Y - was.Y, 0f);
        if (moved != Vector3.Zero)
        {
            Slide(visual, visual.Offset - moved, visual.Offset, true);
            glides[key] = (visual.Offset - moved, visual.Offset);
        }
        var grown = shows.Size - was.Size;
        if (grown != Vector2.Zero)
        {
            var from = visual.Size - grown;
            var grow = compositor.CreateVector2KeyFrameAnimation();
            grow.InsertKeyFrame(0f, from);
            grow.InsertKeyFrame(1f, visual.Size, decelerate);
            grow.Duration = Time(MotionKind.Move);
            visual.StartAnimation("Size", grow);
            grow.Dispose();
            card.Shape.GrowFrom(compositor, from, decelerate, Time(MotionKind.Move));
        }
    }

    /// <summary>
    /// A part's card in its own rectangle, rounded all round (§512: every widget its own card, its outline all the way round
    /// it — the person, 2026-11-05: "the outline goes around the group and not the individual widgets"). Each stays the
    /// part's own visual, so edit mode moves it with the part.
    /// </summary>
    private void PlaceCard(Rounded card, RectF rect, CompositionBrush? fill, CompositionBrush? stroke, float radius) =>
        card.Place(rect, radius, fill, stroke, layout.Stroke);

    /// <summary>What is made now, kept for as long as what it is made for: the element being made, else the scene.</summary>
    private T Own<T>(T item)
        where T : class, IDisposable
    {
        sink.Add(item);
        return item;
    }

    private void Scale(Visual visual, bool down)
    {
        if (look.Animations)
        {
            var press = down ? pressIn : pressOut;
            press.Duration = Time(MotionKind.Press);
            visual.StartAnimation("Scale", press);
            return;
        }
        visual.StopAnimation("Scale");
        visual.Scale = down ? new Vector3(PressedScale, PressedScale, 1f) : Vector3.One;
    }

    /// <summary>The hover's outline where hover is an outline (high contrast); none elsewhere.</summary>
    private CompositionColorBrush? HoverRing => tokens.HoverOutline ? outline : null;

    private void Dress(Rounded shape, CompositionColorBrush fill, Hit hit, bool pressed)
    {
        fill.StopAnimation("Color");
        fill.Color = ShadeOf(hit, pressed);
        if (hit.Kind == HitKind.ResizeGrip)
        {
            // Windows shows no grip; a thin line on the inner edge says the
            // bar can be dragged there.
            var rect = layout.ItemRect(hit);
            var thick = MathF.Min(Px(GripLineDip), MathF.Min(rect.Width, rect.Height));
            var edge = layout.Edge switch
            {
                Edge.Top => rect with { Y = rect.Bottom - thick, Height = thick },
                Edge.Left => rect with { X = rect.Right - thick, Width = thick },
                Edge.Right => rect with { Width = thick },
                _ => rect with { Height = thick },
            };
            shape.Place(edge, 0f, sliverFill, null, 0f);
            return;
        }
        var lights = LitShape(hit);
        shape.Place(lights.Rect, lights.Radius, fill, HoverRing, tokens.Line);
    }

    /// <summary>
    /// What lighting <paramref name="hit"/> lights (<see cref="Dress"/>), the keys' focus rectangle going round the same
    /// (<see cref="KeyFocus"/>): where, in which corners, and whether it is its part's whole card — which, where the part
    /// wears a plate at rest, is that plate (§506, <see cref="LightPlates"/>).
    /// </summary>
    private (RectF Rect, float Radius, bool Whole) LitShape(Hit hit)
    {
        var rect = layout.ItemRect(hit);
        // Which part the hit is in, and its kind, for the cases that light the
        // card up rather than the thing on it.
        var hitPart = layout.PartOf(hit);
        var hitKind = hitPart >= 0 && hitPart < layout.Parts.Count ? layout.Parts[hitPart].Kind : (BarKind?)null;
        // Only a bar whose widgets wear a card each lights the card. A connected bar (and a floating
        // one's panels) has no card to light, so it lights what Windows' flat taskbar lights: the
        // button's own plate, corners and all. Lighting the cell edge to edge with square corners
        // made every hover a hole cut in the band.
        var cards = layout.Cards;
        // search: a search box lights up as itself, its corners its own, in either look, as Windows 11's does.
        if (layout.SearchBoxRect(hit) is { IsEmpty: false } pill)
        {
            return (pill, layout.SearchBoxRadius, false);
        }
        switch (hit.Kind)
        {
            // status icons: each icon is a button of its own, so its own square lights up, corners and
            // all — the square the network icon's card is beside it — and not the whole row's card.
            case HitKind.Island when cards && hitKind is { } own && LightsItsOwnCell(own):
                return (rect, layout.CardRadius, false);
            case HitKind.Island when cards && hitPart >= 0:
            case HitKind.Start when cards:
            case HitKind.Search when cards:
            case HitKind.TaskView when cards:
            case HitKind.WindowsButton when cards:
            case HitKind.ShowDesktop when cards:
                return hitPart >= 0
                    ? (layout.CardRect(hitPart) is { IsEmpty: false } card ? card : layout.BlockRect(hitPart), layout.CardRadius, true)
                    : (rect, layout.CardRadius, false);
            // app buttons: each button's own cell lights up, corners and all, on the card its part
            // wears — a pin alone that is its whole card, as a one-cell
            // widget's; one of a row of buttons its own square, as a tray icon its own cell.
            // The focused app's plate and the running indicator stay the taskbar's, inside it.
            case HitKind.Tile when cards && layout.TileCell(hit.Index) is { IsEmpty: false } tileCell:
                return (tileCell, layout.CardRadius, false);
            case HitKind.AppOverflow when cards && layout.OverflowCell() is { IsEmpty: false } moreCell:
                return (moreCell, layout.CardRadius, false);
            // tray: the icon's own cell is what lights up, corners and all, on the card under the row, as a status icon's does.
            case HitKind.Tray when cards:
            case HitKind.TrayOverflow when cards:
                return (rect, layout.CardRadius, false);
        }
        // App buttons' corners grow with the icon, the rest with the bar; rounded in both looks, as
        // Windows 11's taskbar rounds every button's plate on its flat band; a flush bar's are square.
        var radius = layout.Corner(hit.Kind switch
        {
            HitKind.Tile or HitKind.AppOverflow or HitKind.Start or HitKind.Search or HitKind.TaskView or HitKind.WindowsButton => U(Layout.HighlightRadiusDip),
            _ => layout.SoftPx(Layout.HighlightRadiusDip),
        });
        return (rect, radius, false);
    }

    /// <summary>
    /// The highlight's fill: what lighting <paramref name="hit"/> lays over what it stands on (<see cref="LitOver"/>); none
    /// over a plate at rest it lights whole, which lights itself instead (§506, <see cref="LightPlates"/>).
    /// </summary>
    private Color ShadeOf(Hit hit, bool pressed) =>
        PlateLit(hit) >= 0 ? WithAlpha(tokens.Hover, 0f) : LitOver(hit, pressed);

    /// <summary>
    /// What lighting <paramref name="hit"/> lays over what it stands on. In high contrast hovering is the outline alone and
    /// pressing fills it. Over the focused app, which already has a subtle
    /// background, the hover adds only what lifts it to the next Fluent step
    /// (ControlFillColorSecondary) and a press adds nothing: its shrinking icon
    /// says it.
    /// </summary>
    private Color LitOver(Hit hit, bool pressed)
    {
        // §567: in the bar's colours: no widget has its own.
        if (tokens.HoverOutline)
        {
            return pressed ? tokens.Pressed : WithAlpha(tokens.Pressed, 0f);
        }
        // The focused app's button, and the open folder's face (§376), lit over their plate.
        var lifted = (hit.Kind == HitKind.Tile
            && hit.Index >= 0
            && hit.Index < marks.Count
            && (marks[hit.Index].Active || marks[hit.Index].Attention))
            || (openPart >= 0 && hit.Kind == HitKind.Island && layout.PartOf(hit) == openPart);
        if (lifted)
        {
            return pressed ? WithAlpha(tokens.Hover, 0f) : tokens.HoverOnActive;
        }
        // search: a search box lights up as Windows' does, by its own fill and edge
        // (SearchPlate), with no shade under it; with a card or a highlight chosen, as a card does (§325).
        if (!layout.SearchBoxRect(hit).IsEmpty && LightsAsWindows(layout.PartOf(hit)))
        {
            return WithAlpha(tokens.Hover, 0f);
        }
        return pressed ? tokens.Pressed : tokens.Hover;
    }

    /// <summary>
    /// §506: how strong a widget's plate at rest is, of its text's colour: about Windows' hover (SubtleFillColorSecondary), so a
    /// widget on a bar whose style draws no card stands apart from its neighbours as a button does under the pointer, but faint.
    /// </summary>
    internal const float RestPlateAlpha = 0.09f;

    /// <summary>§506: this update's plates at rest, by part (<see cref="RestPlate"/>).</summary>
    private readonly Dictionary<int, List<Shaped>> restPlates = [];

    /// <summary>tests: part <paramref name="part"/>'s (first) plate at rest, where it stands and the colour it is given now; null where it wears none.</summary>
    internal (RectF Rect, Color Fill)? RestPlateOf(int part) => restPlates.TryGetValue(part, out var plates) ? (plates[0].Shows, plates[0].Shade) : null;

    /// <summary>tests: every plate at rest part <paramref name="part"/> wears (§602: an app button's part one a button), where each stands.</summary>
    internal IReadOnlyList<RectF> RestPlatesOf(int part) => restPlates.TryGetValue(part, out var plates) ? [.. plates.Select(plate => plate.Shows)] : [];

    /// <summary>§506: the part whose plate at rest lighting <paramref name="hit"/> lights whole (<see cref="LitShape"/>); −1 for none.</summary>
    private int PlateLit(Hit hit) => layout.PartOf(hit) is var part && restPlates.ContainsKey(part) && LitShape(hit).Whole ? part : -1;

    /// <summary>
    /// §506: part <paramref name="part"/>'s plate's colour now: at rest its text's colour at <see cref="RestPlateAlpha"/>; its folder
    /// open, with the open plate's fill (<see cref="PlaceOpen"/>) laid in; lit whole, with the highlight's shade
    /// (<see cref="LitOver"/>) laid in, pressed or not. One surface drawn stronger, never a second one over it.
    /// </summary>
    private Color PlateShade(int part)
    {
        var mine = tokens;
        var shade = WithAlpha(mine.Text, RestPlateAlpha);
        if (part == openPart && mine.ActiveBack is { } open)
        {
            shade = FolderFlyout.Blend(shade, open);
        }
        if (lit is { } hit && PlateLit(hit) == part)
        {
            shade = FolderFlyout.Blend(shade, LitOver(hit, litPressed));
        }
        return shade;
    }

    /// <summary>
    /// §506: every plate at rest in its colour now (<see cref="PlateShade"/>): a new one at once, a kept one over Windows' brush
    /// transition, as the highlight fades — so the pointer, a press or an open folder draws its plate stronger, in place.
    /// </summary>
    private void LightPlates()
    {
        foreach (var (part, plates) in restPlates)
        {
            var shade = PlateShade(part);
            foreach (var plate in plates)
            {
                if (shade == plate.Shade || plate.Fill is not CompositionColorBrush brush)
                {
                    continue;
                }
                var fresh = plate.Shade == default;
                plate.Shade = shade;
                if (fresh)
                {
                    brush.StopAnimation("Color");
                    brush.Color = shade;
                    continue;
                }
                Recolour(brush, shade);
            }
        }
    }

    private void Fade(Visual visual, bool shown)
    {
        fadesIn += shown ? 1 : 0;
        if (look.Animations)
        {
            var fade = shown ? quickIn : quickOut;
            fade.Duration = Time(MotionKind.Hover);
            visual.StartAnimation("Opacity", fade);
            return;
        }
        visual.StopAnimation("Opacity");
        visual.Opacity = shown ? 1f : 0f;
    }

    /// <summary>
    /// Where a tile's running indicator is centred: on the edge side, its outer
    /// border a hairline inside the highlight's, as the taskbar places it.
    /// </summary>
    private Vector2 PillCentre(RectF tile)
    {
        var inset = layout.Stroke + layout.Pill / 2f;
        var centre = tile.Center;
        return layout.Edge switch
        {
            Edge.Top => new Vector2(centre.X, tile.Y + inset),
            Edge.Left => new Vector2(tile.X + inset, centre.Y),
            Edge.Right => new Vector2(tile.Right - inset, centre.Y),
            _ => new Vector2(centre.X, tile.Bottom - inset),
        };
    }

    /// <summary>
    /// A running indicator: a sprite under a rounded clip, both sized by
    /// <see cref="SetMark"/> (not bound by an expression, for the reason given
    /// at <see cref="Rounded"/>). Once the scene attaches the implicit
    /// animations, the two change size in step.
    /// </summary>
    private SpriteVisual Pill(Compositor c, float thickness)
    {
        var dot = Own(c.CreateSpriteVisual());
        var shape = Own(c.CreateRoundedRectangleGeometry());
        shape.CornerRadius = new Vector2(thickness / 2f, thickness / 2f);
        dot.Clip = Own(c.CreateGeometricClip(shape));
        return dot;
    }

    /// <summary>The rounded shape a pill is clipped to.</summary>
    private static CompositionRoundedRectangleGeometry? PillShape(SpriteVisual pill) =>
        pill.Clip is CompositionGeometricClip { Geometry: CompositionRoundedRectangleGeometry shape } ? shape : null;

    /// <summary>
    /// The bar: its material, and a border on the inner side only; the other
    /// sides are screen edges. Translucent is its colour at the bar's opacity over the wallpaper Karpo blurs (§547,
    /// <see cref="Behind"/>); Clear has neither (its pattern and outline are still drawn, §512); Opaque is the colour
    /// solid. Made for its kind (<see cref="ComposePanel"/>), placed again at every update (<see cref="PlacePanel"/>).
    /// </summary>
    private PanelView Panel(Compositor c)
    {
        // §512: Clear (a background of None or 0 %) is a ground of nothing, the bar's pattern and outline still drawn.
        var panel = new PanelView(Own(c.CreateContainerVisual()));
        if (layout.Floating)
        {
            FloatingPanels(c, panel);
            return panel;
        }
        // §547: the wallpaper blurred under all of it, the cards' holes too: what a see-through card stands on.
        if (Behind(c) is { } behind)
        {
            panel.Container.Children.InsertAtTop(behind);
        }
        // §464: the colour drawn only where no card stands (DressCards cuts the cards out of it).
        panel.Cut = Fill(c);
        panel.Container.Children.InsertAtTop(panel.Cut);
        panel.Cut.Children.InsertAtTop(Ground(c));
        // §462: the bar's pattern over its colour, one sheet across the bar; §464: cut where the cards stand with it.
        panel.Pattern = Own(new PatternLayer(c));
        panel.Cut.Children.InsertAtTop(panel.Pattern.Visual);
        panel.Border = Own(c.CreateSpriteVisual());
        panel.BorderInk = Own(c.CreateColorBrush());
        panel.Border.Brush = panel.BorderInk;
        // §704: a Liquid glass bar its glow falling in at its top over its tint (cut where the cards stand, which have their
        // own), its lens's bands and its rim along its inner edge (the others are the screen's), each its light down the bar.
        if (look.Liquid is { } glass)
        {
            var glow = Fill(c);
            panel.Glow = Relative(c, glass.Glow, new Vector2(0f, 1f));
            glow.Brush = panel.Glow;
            panel.Cut.Children.InsertAtTop(glow);
            for (var band = 0; band < glass.LensBands.Count; band++)
            {
                var sprite = Own(c.CreateSpriteVisual());
                var brush = Relative(c, glass.LensStops(band), new Vector2(0f, 1f));
                sprite.Brush = brush;
                panel.Container.Children.InsertAtTop(sprite);
                panel.Bands.Add((sprite, brush));
            }
            panel.Rim = Relative(c, glass.Rim, new Vector2(0f, 1f));
            panel.Border.Brush = panel.Rim;
        }
        panel.Container.Children.InsertAtTop(panel.Border);
        return panel;
    }

    /// <summary>The panel where the layout has it: its strip, its border on the inner side, a floating bar's strip and docks sized to it.</summary>
    private void PlacePanel(PanelView panel)
    {
        Put(panel.Container, layout.PanelRect);
        var size = layout.PanelRect.Size;
        panel.Pattern?.Show(gfx, look.Colors.TaskbarPattern, layout.Scale, size, Vector2.Zero);
        if (panel.Border is { } border)
        {
            // §512: the Taskbar's outline, where one is set, on the edge facing the desktop — the others are the screen's; Windows'
            // own docked bar has none. High contrast's border always.
            // §704: Liquid glass's rim there too.
            var width = look.HighContrast || look.Colors.TaskbarOutline is { A: > 0 } || look.Liquid is not null ? tokens.BorderWidth : 0;
            panel.BorderInk!.Color = tokens.Border;
            RectF Inner(float from, float wide) => layout.Edge switch
            {
                Edge.Top => new RectF(0f, size.Y - from - wide, size.X, wide),
                Edge.Left => new RectF(size.X - from - wide, 0f, wide, size.Y),
                Edge.Right => new RectF(from, 0f, wide, size.Y),
                _ => new RectF(0f, from, size.X, wide),
            };
            var edge = Inner(0f, width);
            Put(border, edge);
            // §704: Liquid glass's light where the bar stands now: the glow GlowDip deep from its top, the rim and the lens's
            // bands each the piece of the light down the bar where it stands.
            if (look.Liquid is { } glass)
            {
                if (panel.Glow is { } glow)
                {
                    (glow.StartPoint, glow.EndPoint) = (Vector2.Zero, new Vector2(0f, MathF.Max(1f, Px(glass.GlowDip)) / MathF.Max(1f, size.Y)));
                }
                if (panel.Rim is { } rim)
                {
                    DownTheBar(rim, edge, size.Y);
                }
                var bandPx = MathF.Max(1f, Px(glass.LensBandDip));
                for (var band = 0; band < panel.Bands.Count; band++)
                {
                    var (sprite, brush) = panel.Bands[band];
                    var at = Inner(width + (band * bandPx), bandPx);
                    Put(sprite, at);
                    DownTheBar(brush, at, size.Y);
                }
            }
        }
        if (panel.Maker is { } maker)
        {
            foreach (var dock in panel.Floating!.Values)
            {
                dock.Resize(size);
            }
        }
    }

    /// <summary>
    /// §704: <paramref name="brush"/> (relative to the sprite it fills, <paramref name="rect"/> in the bar) aimed so the sprite
    /// shows the piece of its gradient where it stands down a bar <paramref name="tall"/> px: a line along the top the light at
    /// the gradient's start, one along the foot at its end, one down a side all of it.
    /// </summary>
    private static void DownTheBar(CompositionLinearGradientBrush brush, RectF rect, float tall)
    {
        var h = MathF.Max(1f, rect.Height);
        (brush.StartPoint, brush.EndPoint) = (new Vector2(0f, -rect.Y / h), new Vector2(0f, (tall - rect.Y) / h));
    }

    /// <summary>The bar's panel: its strip, and its border, or a floating bar's docks.</summary>
    private sealed class PanelView(ContainerVisual container) : IShown
    {
        public ContainerVisual Container { get; } = container;

        /// <summary>§704: a docked Liquid glass bar's glow, its rim's gradient and its lens's bands; none for another look.</summary>
        public CompositionLinearGradientBrush? Glow { get; set; }

        public CompositionLinearGradientBrush? Rim { get; set; }

        public List<(SpriteVisual Sprite, CompositionLinearGradientBrush Brush)> Bands { get; } = [];

        /// <summary>§464: what holds the bar's colour, cut where the cards stand; a floating bar's are its docks'.</summary>
        public ContainerVisual? Cut { get; set; }

        public SpriteVisual? Border { get; set; }

        public CompositionColorBrush? BorderInk { get; set; }

        public Dictionary<BarSection, FloatingPanel>? Floating { get; set; }

        public FloatingMaker? Maker { get; set; }

        /// <summary>§462: the bar's pattern, its one sheet: over its colour, a floating bar's over each dock's (<see cref="PatternLayer.Also"/>).</summary>
        public PatternLayer? Pattern { get; set; }

        public Visual Visual => Container;
    }

    /// <summary>
    /// §547: under a see-through bar (Translucent), the wallpaper as Windows lays it out under the bar's strip, blurred as
    /// the layout's Taskbar says — Karpo's own blur, whatever Windows' transparency effects —, filling what it is put in;
    /// null for the other materials: a solid bar, and one of nothing (None, 0 %), which shows the desktop itself. Its
    /// picture is the dock's (<see cref="Wallpaper"/>), set in place; none yet, nothing: the colour over the desktop, as §546.
    /// </summary>
    private SpriteVisual? Behind(Compositor c)
    {
        if (tokens.Material != Material.Translucent)
        {
            return null;
        }
        var fill = Fill(c);
        fill.Brush = wallpaper ??= NewWallpaper();
        return fill;
    }

    /// <summary>
    /// §547: the wallpaper Karpo blurred for this bar's strip (Karpo.Dock's WallpaperStrips), its size the bar's panel's in px;
    /// null for none. Set in place: nothing is built again, every see-through panel and dock of the bar shows it at once.
    /// </summary>
    public void Wallpaper(CompositionDrawingSurface? strip)
    {
        if (wallpaper is null && strip is null)
        {
            return;
        }
        wallpaper ??= NewWallpaper();
        if (!ReferenceEquals(wallpaper.Surface, strip))
        {
            wallpaper.Surface = strip;
        }
    }

    /// <summary>
    /// §704: how light the wallpaper under this bar is (relative luminance, 0 to 1: LiquidGlass.Behind of the strip it blurs),
    /// null for not known: Liquid glass's tint as clear as its text lets it be over it (<see cref="Glassed"/>), its colours set
    /// again in place where it changed. Nothing for another look.
    /// </summary>
    public void Backdrop(double? light)
    {
        if (light == behind)
        {
            return;
        }
        behind = light;
        if (Ready && look.Liquid is not null)
        {
            tokens = Glassed(Theme(look, layout), look, behind);
            Dye();
        }
    }

    /// <summary>§704: the light of the wallpaper under the bar (<see cref="Backdrop"/>); null while not known.</summary>
    private double? behind;

    /// <summary>tests: the light of the wallpaper under the bar as last told; null for none.</summary>
    internal double? BackdropLight => behind;

    /// <summary>
    /// §704: <paramref name="tokens"/> of <paramref name="look"/> with Liquid glass's tint (the bar's and its cards') as clear as
    /// its text lets it be over a wallpaper of light <paramref name="light"/> (GlassLook.Tinted: its most where that is not
    /// known); a colour somebody chose in its place as chosen. As they are for another look.
    /// </summary>
    internal static Tokens Glassed(Tokens tokens, Look look, double? light)
    {
        if (look.Liquid is not { } glass)
        {
            return tokens;
        }
        if (tokens.Material == Material.Translucent && look.Colors.Taskbar is { } bar)
        {
            tokens.Opacity = glass.Tinted(bar, light).A / 255f;
        }
        if (look.Colors.Card is { } card && tokens.CardFill is not null)
        {
            var tinted = glass.Tinted(card, light);
            tokens.CardFill = Color.FromArgb(tinted.A, tinted.R, tinted.G, tinted.B);
        }
        return tokens;
    }

    /// <summary>tests: the picture under the bar now; null for none.</summary>
    internal ICompositionSurface? WallpaperShown => wallpaper?.Surface;

    private CompositionSurfaceBrush NewWallpaper()
    {
        var brush = compositor.CreateSurfaceBrush();
        brush.Stretch = CompositionStretch.Fill;
        return brush;
    }

    /// <summary>§547: the brush of the wallpaper under the bar, the window's for its life, closed with the scene.</summary>
    private CompositionSurfaceBrush? wallpaper;

    /// <summary>§464: a card placed this update (<see cref="Card"/>), which <see cref="DressCards"/> cuts out of the bar.</summary>
    private readonly record struct PlacedCard(int Part, Rounded Shape, RectF Rect, float Radius);

    /// <summary>§464: the cards placed this update, filled ones (a colour or None): each a surface of its own.</summary>
    private readonly List<PlacedCard> placedCards = [];

    /// <summary>§464: the bar's background less its cards, in the strip's px; closed with the scene.</summary>
    private CompositionPathGeometry? cutShape;

    /// <summary>§464: the bar's panel as this update keeps it, whose colour <see cref="Cut"/> cuts.</summary>
    private PanelView? panelView;

    /// <summary>§464: how far inside a card the bar is cut, in px: the card's smoothed edge stands on the bar.</summary>
    private const float CutInset = 1f;

    /// <summary>§464: the holes the cut was last made with, so an update that moves no card makes none.</summary>
    private List<Hole>? cutHoles;

    /// <summary>
    /// §476: the bar's background is cut where its cards stand now (<see cref="cutShape"/> holds their path); whole where
    /// there are none, or Direct2D made no path.
    /// </summary>
    private bool cutting;

    /// <summary>§476: <see cref="cutting"/>, for the tests.</summary>
    internal bool Cuts => cutting;

    /// <summary>
    /// §464 (the person: "i also dont want that items stack their background … it should show the desktop directly"):
    /// every filled card is a surface of its own straight over the desktop. Its see-through colour stands on what the
    /// desktop is under the bar — §547 the wallpaper Karpo blurs where the bar is see-through (<see cref="Behind"/>), the
    /// desktop itself otherwise —, a colour of 0 % or None on nothing; and the bar's colour is drawn only where no card
    /// stands: cut out under each (<see cref="CutPath"/>), so a clear card shows the desktop, not the bar. In place, at every
    /// update and recolour.
    /// </summary>
    private void DressCards()
    {
        var holes = new List<Hole>(placedCards.Count);
        var (left, top) = (layout.PanelRect.X, layout.PanelRect.Y);
        foreach (var card in placedCards)
        {
            // A px inside the card all round: its smoothed edge stands on the bar as it always did, not half on nothing (a
            // seam of the desktop round every card).
            var inside = new RectF(card.Rect.X - left + CutInset, card.Rect.Y - top + CutInset, card.Rect.Width - (2f * CutInset), card.Rect.Height - (2f * CutInset));
            holes.Add(new Hole(inside, MathF.Max(0f, card.Radius - CutInset)));
        }
        Cut(holes);
    }

    /// <summary>§464: the bar's background cut where <paramref name="holes"/> are, in every container that holds it; whole where there are none.</summary>
    private void Cut(List<Hole> holes)
    {
        List<ContainerVisual> views = [];
        if (panelView is { } panel)
        {
            if (panel.Cut is { } cut)
            {
                views.Add(cut);
            }
            views.AddRange(panel.Floating?.Values.Select(dock => dock.Cut) ?? []);
        }
        if (cutHoles is not { } was || !was.SequenceEqual(holes))
        {
            cutHoles = holes;
            var path = holes.Count > 0 ? CutPath.Of(layout.PanelRect.Width, layout.PanelRect.Height, holes) : null;
            // §476: never a Path of null: a path geometry that had one and is set to none crashes in dcomp.dll (an access
            // violation, 2026-11-01 and 11-02). With no card the geometry keeps its last path, unused: no clip holds it.
            if (path is not null)
            {
                cutShape ??= compositor.CreatePathGeometry();
                cutShape.Path = path;
            }
            cutting = path is not null;
        }
        foreach (var view in views)
        {
            if (!cutting)
            {
                view.Clip = null;
            }
            else if (view.Clip is not CompositionGeometricClip { Geometry: var shape } || shape != cutShape)
            {
                view.Clip = Own(compositor.CreateGeometricClip(cutShape));
            }
        }
    }

    /// <summary>The material's own colour, filling what it is put in (<see cref="GroundOf"/>).</summary>
    private SpriteVisual Ground(Compositor c)
    {
        var ground = Fill(c);
        ground.Brush = Swatch(GroundOf);
        return ground;
    }

    /// <summary>
    /// The colour the bar's ground is painted in <paramref name="tokens"/>, over what is behind it (acrylic's blur; §546 the
    /// desktop itself where it is translucent): solid when opaque, the tint at the bar's opacity otherwise.
    /// </summary>
    internal static Color GroundOf(Tokens tokens) => tokens.Material == Material.Opaque ? tokens.Base : WithAlpha(tokens.Tint, tokens.Opacity);

    /// <summary>
    /// A brush in a colour somebody may choose: <paramref name="pick"/> read from part
    /// <paramref name="part"/>'s tokens now (the bar's for -1), and again from the new ones at every
    /// update and recolour (<see cref="Dye"/>), the part found again by its key.
    /// </summary>
    private CompositionColorBrush Swatch(Func<Tokens, Color> pick)
    {
        var brush = Own(compositor.CreateColorBrush(pick(tokens)));
        swatchSink.Add(new Swatched(brush, pick));
        return brush;
    }

    /// <summary>
    /// The colours somebody chose changed and nothing else did (<paramref name="next"/> is this
    /// scene's look with other <see cref="Look.Colors"/>): every brush that draws one is set again
    /// in place, and the highlight under the pointer with them. Nothing is built, moved or redrawn,
    /// so a colour being dragged through a picker follows it on the bar as it goes.
    /// </summary>
    public void Recolour(Look next)
    {
        if (!Ready || next == look)
        {
            return;
        }
        look = next;
        tokens = Glassed(Theme(next, layout), next, behind);
        Dye();
        DressCards();
        PlaceOpen();
    }

    /// <summary>tests: the colour each brush <see cref="Recolour"/> sets again has now, the scene's first, then each element's, in the order they were made.</summary>
    internal IReadOnlyList<Color> SwatchColors =>
        [.. swatches.Concat(elements.Values.SelectMany(element => element.Swatches)).Select(swatch => swatch.Brush.Color)];

    /// <summary>tests: the colour the highlight under the pointer has now (§311: its part's own).</summary>
    internal Color LitColor => highlightFill.Color;

    /// <summary>
    /// floating: a panel of the bar's material for every dock (<see cref="Layout.SectionPanels()"/>) — a
    /// section that holds a widget —, in the strip
    /// <paramref name="panel"/> covers, each Windows 11's floating surface: rounded 8 DIPs, a
    /// hairline round it in the bar's border colour, and a soft shadow under it. The desktop shows
    /// between and around them: the window draws nothing there. One for each dock, made when it
    /// first shows and kept, shown or hidden and reshaped as the docks change (<see cref="ShowPanels"/>),
    /// so a panel glides with the widgets while a block is carried in edit mode.
    /// <para>
    /// The shadow sits under the panel; a panel covers it — §547 the wallpaper Karpo blurs under a see-through one, or
    /// Opaque —, so every panel but one of nothing (Clear) casts one. The outline is the only edge a
    /// see-through one has.
    /// </para>
    /// </summary>
    private void FloatingPanels(Compositor c, PanelView panel)
    {
        var strip = panel.Container;
        // Its docks are the panel's, made into its own bag as they come (NewFloatingPanel).
        panel.Floating = [];
        panel.Pattern = Own(new PatternLayer(c));
        panel.Maker = new FloatingMaker(
            strip,
            Own(c.CreateColorBrush(tokens.Border)),
            look.HighContrast ? tokens.BorderWidth : layout.Stroke,
            tokens.Material is not Material.Clear && !look.HighContrast,
            sink,
            swatchSink,
            panel.Pattern);
    }

    /// <summary>
    /// floating: a new dock's panel in the strip, hidden: its material, its hairline and, where the
    /// panel casts one, its shadow — the shadow under every panel, the hairline over its material.
    /// The bar's panel's own, closed with it.
    /// </summary>
    private FloatingPanel NewFloatingPanel(FloatingMaker maker)
    {
        using var making = Making(maker.Bag, maker.Swatches);
        var c = maker.Strip.Compositor;
        var size = layout.PanelRect.Size;
        var radius = new Vector2(layout.PanelRadius, layout.PanelRadius);
        CompositionRoundedRectangleGeometry Shape()
        {
            var shape = Own(c.CreateRoundedRectangleGeometry());
            shape.CornerRadius = radius;
            return shape;
        }
        var panel = new FloatingPanel { Inset = maker.Hairline / 2f };
        // The material, clipped to the panel's rounded shape.
        panel.Body = Own(c.CreateContainerVisual());
        panel.Shape = Shape();
        panel.Body.Clip = Own(c.CreateGeometricClip(panel.Shape));
        // §547: the wallpaper blurred under it, the strip's size, so the docks show it where it is under each.
        if (Behind(c) is { } behind)
        {
            panel.Body.Children.InsertAtTop(behind);
        }
        // §464: drawn only where no card stands (DressCards).
        panel.Cut = Fill(c);
        panel.Body.Children.InsertAtTop(panel.Cut);
        panel.Cut.Children.InsertAtTop(Ground(c));
        // §462: the bar's pattern over it, the strip's one sheet: the docks run on from one to the next as one bar would; §464:
        // cut where the cards stand with it.
        panel.Cut.Children.InsertAtTop(maker.Sheet.Also(c));
        // The hairline, inside the shape.
        panel.Ring = Shape();
        panel.Ring.CornerRadius = new Vector2(MathF.Max(0f, layout.PanelRadius - panel.Inset));
        var ring = Own(c.CreateSpriteShape(panel.Ring));
        ring.StrokeBrush = maker.Edge;
        ring.StrokeThickness = maker.Hairline;
        panel.Outline = Own(c.CreateShapeVisual());
        // §704: a Liquid glass panel its glow and lens over its tint, its rim the glass's in place of its hairline.
        if (look.Liquid is { } glass)
        {
            panel.Glaze = new Glaze(c, sink, glass, layout.Scale);
            foreach (var shape in panel.Glaze.Shapes)
            {
                panel.Outline.Shapes.Add(shape);
            }
            panel.Rim = Glaze.Over(c, sink, glass);
            ring.StrokeBrush = panel.Rim;
            ring.StrokeThickness = MathF.Max(1f, MathF.Round(glass.RimDip * layout.Scale));
        }
        panel.Outline.Shapes.Add(ring);
        if (maker.Casts)
        {
            // The shadow: the panel's shape, black, as the mask of a drop shadow over the whole
            // strip, so reshaping the one shape moves the shadow with the panel.
            panel.Cast = Shape();
            var black = Own(c.CreateSpriteShape(panel.Cast));
            black.FillBrush = Own(c.CreateColorBrush(Hex(0x000000)));
            panel.CastSource = Own(c.CreateShapeVisual());
            panel.CastSource.Shapes.Add(black);
            panel.CastSurface = Own(c.CreateVisualSurface());
            panel.CastSurface.SourceVisual = panel.CastSource;
            var shadow = Own(c.CreateDropShadow());
            shadow.Mask = Own(c.CreateSurfaceBrush(panel.CastSurface));
            shadow.BlurRadius = layout.PanelShadowBlur;
            shadow.Offset = new Vector3(0f, layout.PanelShadowDrop, 0f);
            shadow.Color = Hex(0x000000);
            shadow.Opacity = look.Dark ? FloatShadowDark : FloatShadowLight;
            // §704: Liquid glass's lift where the panel stands on the frosted wallpaper: large and very faint, out to ShadowBlurDip
            // and ShadowOffsetDip down. The compositor's own soft shadow (it renders here, in the pictures too): a blurred shape is
            // half as dark at its edge as inside, so it casts at twice the lift's edge darkness to be GlassLook.Lift(0) there,
            // falling off to nothing at the blur's end; under the panel its frost covers it.
            if (look.Liquid is { } lifted && tokens.Material == Material.Translucent)
            {
                shadow.BlurRadius = MathF.Round(lifted.ShadowBlurDip * layout.Scale);
                shadow.Offset = new Vector3(0f, MathF.Round(lifted.ShadowOffsetDip * layout.Scale), 0f);
                shadow.Opacity = (float)Math.Min(1, 2 * lifted.Lift(0));
            }
            panel.Shadow = Own(c.CreateSpriteVisual());
            panel.Shadow.Shadow = shadow;
            panel.Shadow.IsVisible = false;
            maker.Strip.Children.InsertAtBottom(panel.Shadow);
        }
        panel.Resize(size);
        panel.Body.IsVisible = false;
        panel.Outline.IsVisible = false;
        maker.Strip.Children.InsertAtTop(panel.Body);
        maker.Strip.Children.InsertAtTop(panel.Outline);
        return panel;
    }

    /// <summary>
    /// floating: the docks where <paramref name="panels"/> says, by section (<see cref="Layout.SectionPanels()"/>),
    /// in window px; one empty there hidden. A panel already shown glides there with the widgets beside it; a dock
    /// new to the bar — a block dropped into an empty section — gets a panel of its own, shown where it stands.
    /// </summary>
    private void ShowPanels(IReadOnlyList<RectF> panels, bool animate)
    {
        if (floating is not { } shown || floatingMaker is not { } maker)
        {
            return;
        }
        var wanted = new Dictionary<BarSection, RectF>();
        for (var section = 0; section < panels.Count; section++)
        {
            if (panels[section].IsEmpty)
            {
                continue;
            }
            wanted[(BarSection)section] = panels[section];
            if (!shown.ContainsKey((BarSection)section))
            {
                shown[(BarSection)section] = NewFloatingPanel(maker);
            }
        }
        var origin = new Vector2(layout.PanelRect.X, layout.PanelRect.Y);
        foreach (var (key, panel) in shown)
        {
            var rect = wanted.GetValueOrDefault(key);
            var visible = !rect.IsEmpty;
            var glide = animate && visible && panel.Body.IsVisible;
            panel.Body.IsVisible = visible;
            panel.Outline.IsVisible = visible;
            if (panel.Shadow is not null)
            {
                panel.Shadow.IsVisible = visible;
            }
            if (!visible)
            {
                continue;
            }
            var offset = new Vector2(rect.X, rect.Y) - origin;
            Reshape(panel.Shape, offset, rect.Size, glide);
            var inset = new Vector2(panel.Inset, panel.Inset);
            Reshape(panel.Ring, offset + inset, Vector2.Max(Vector2.Zero, rect.Size - (2f * inset)), glide);
            if (panel.Cast is not null)
            {
                Reshape(panel.Cast, offset, rect.Size, glide);
            }
            // §704: Liquid glass's light where the panel stands now (at once: a gradient does not glide).
            panel.Glaze?.Place(rect.Size, layout.PanelRadius, offset);
            if (panel.Rim is { } rim)
            {
                GlassPaint.Down(rim, rect.Size, offset);
            }
        }
    }

    /// <summary>floating: the docks as they are shown now, in window px, by section. For the tests.</summary>
    internal IReadOnlyList<RectF> FloatingPanelRects()
    {
        if (floating is not { } shown)
        {
            return [];
        }
        var origin = new Vector2(layout.PanelRect.X, layout.PanelRect.Y);
        return [.. shown.Where(pair => pair.Value.Body.IsVisible)
            .OrderBy(pair => pair.Key)
            .Select(pair => new RectF(origin.X + pair.Value.Shape.Offset.X, origin.Y + pair.Value.Shape.Offset.Y, pair.Value.Shape.Size.X, pair.Value.Shape.Size.Y))];
    }

    private SpriteVisual Fill(Compositor c)
    {
        var sprite = Own(c.CreateSpriteVisual());
        sprite.RelativeSizeAdjustment = new Vector2(1f, 1f);
        return sprite;
    }

    private float Px(float dip) => Round(dip * layout.Scale);

    /// <summary>§704: white at <paramref name="stops"/>' alphas across the sprite it fills, from its top left to <paramref name="end"/> (of its size).</summary>
    private CompositionLinearGradientBrush Relative(Compositor c, IReadOnlyList<GlassStop> stops, Vector2 end)
    {
        var brush = GlassPaint.Brush(c, stops, sink);
        brush.MappingMode = CompositionMappingMode.Relative;
        (brush.StartPoint, brush.EndPoint) = (Vector2.Zero, end);
        return brush;
    }

    private float U(float dip) => layout.UnitPx(dip);

    private float TextPx(float dip) => dip * layout.TextScale * layout.Scale;

    /// <summary>Whether a scene for <paramref name="now"/> can glide things from where <paramref name="before"/> had them.</summary>
    private static bool Comparable(Layout before, Layout now) =>
        before.Edge == now.Edge
        && before.Scale == now.Scale
        && before.Thickness == now.Thickness
        && before.Icon == now.Icon
        && before.Width == now.Width
        && before.Height == now.Height
        && before.Style == now.Style;

    /// <summary>
    /// The one place the bar's colours are decided: Windows' own for the look (<see cref="Automatic"/>),
    /// then each colour somebody chose over it (<see cref="Look.Colors"/>, which high contrast never
    /// has). Every brush that draws a chosen colour is made through <see cref="Swatch"/> from what this
    /// returns, so <see cref="Recolour"/> can set them again in place.
    /// </summary>
    private static Tokens Theme(Look look, Layout layout) => Theme(look, layout.Stroke, layout.Border);

    /// <summary>The same for a bar whose hairline is <paramref name="stroke"/> px and its border <paramref name="border"/>.</summary>
    internal static Tokens Theme(Look look, float stroke, float border)
    {
        var tokens = Automatic(look, stroke, border);
        if (look.HighContrast)
        {
            return tokens;
        }
        // §396: the text the layout's colours set, the quieter ones it, fainter by Windows' own steps; the running
        // indicator with them, as Windows draws it in the text's colour.
        if (look.Ink is { } ink)
        {
            var text = ColorOf(ink);
            (tokens.Text, tokens.TipText) = (text, text);
            tokens.SecondaryText = WithAlpha(text, ink.A / 255f * 0.7725f);
            tokens.TertiaryText = WithAlpha(text, ink.A / 255f * 0.5442f);
            tokens.Running = WithAlpha(text, ink.A / 255f * 0.5451f);
        }
        var chosen = look.Colors;
        if (chosen.Taskbar is { } bar)
        {
            // §405: the bar's colour, solid as it is, over the blur at the bar's opacity (Ground). Clear draws no bar.
            var color = WithAlpha(ColorOf(bar), 1f);
            (tokens.Base, tokens.Tint) = (color, color);
        }
        if (chosen.TaskbarOutline is { } rim)
        {
            // §459: the bar's outline, the Taskbar's; None none at all.
            tokens.Border = ColorOf(rim);
        }
        if (chosen.Card is { } card)
        {
            // §464: a see-through card stands on the blur (DressCards); §546 without transparency effects at its opacity all
            // the same, straight over the desktop, as the bar is (SurfaceStyle.Made).
            tokens.CardFill = ColorOf(card);
        }
        if (chosen.CardOutline is { } cardOutline)
        {
            tokens.CardStroke = ColorOf(cardOutline);
        }
        if (chosen.Highlight is { } hover)
        {
            // The highlight, and the open app's button, which Windows lights with the same fill; a
            // press and the lift over that button are the Fluent steps under it, as the automatic
            // ones are (SubtleFillColorTertiary is two thirds of Secondary).
            var color = ColorOf(hover);
            tokens.Hover = color;
            tokens.Pressed = WithAlpha(color, hover.A / 255f * (2f / 3f));
            tokens.HoverOnActive = WithAlpha(color, hover.A / 255f * 0.42f);
            tokens.ActiveBack = color;
        }
        return tokens;
    }

    private static Color ColorOf(Argb color) => Color.FromArgb(color.A, color.R, color.G, color.B);

    /// <summary>
    /// §405: the material a surface is drawn in, <paramref name="material"/> settled against what Windows allows: acrylic
    /// needs the blur only transparency effects allow (<paramref name="glass"/>), so it is solid without them, as Windows
    /// draws its own surfaces then; high contrast is always solid.
    /// </summary>
    internal static Material Settle(Material material, bool glass, bool high) =>
        high || (material == Material.Acrylic && !glass) ? Material.Opaque : material;

    /// <summary>Windows' own colours for <paramref name="look"/>, as its taskbar and WinUI draw them.</summary>
    private static Tokens Automatic(Look look, float stroke, float border)
    {
        var hairline = stroke;
        if (look.HighContrast)
        {
            var window = Opaque(look.HcWindow, Hex(0x000000));
            var text = Opaque(look.HcText, Hex(0xFFFFFF));
            var selected = Opaque(look.HcHighlight, Hex(0x1AEBFF));
            var selectedText = Opaque(look.HcHighlightText, Hex(0x000000));
            return new Tokens
            {
                Material = Material.Opaque,
                Base = window,
                Tint = window,
                Border = text,
                BorderWidth = 2f * border,
                Line = 2f * hairline,
                Hover = selected,
                Pressed = selected,
                HoverOnActive = selected,
                HoverOutline = true,
                Running = text,
                Active = selected,
                Attention = selected,
                Text = text,
                SecondaryText = text,
                DeskIdleStroke = text,
                DeskCurrent = selected,
                DeskCurrentText = selectedText,
                TipFill = window,
                TipStroke = text,
                TipText = text,
                CardFill = window,
                CardStroke = text,
                EditFill = WithAlpha(window, 0f),
                EditHover = WithAlpha(selected, 0.35f),
                EditOutline = text,
                EditOutlineStrong = selected,
                FocusOuter = text,
                FocusInner = window,
                ZoneStroke = text,
                ZoneHover = WithAlpha(selected, 0.35f),
                ZoneTargetFill = WithAlpha(selected, 0.35f),
                ZoneTargetStroke = selected,
                TertiaryText = text,
            };
        }
        var material = Settle(look.Material, look.Glass, high: false);
        var opacity = float.IsFinite(look.Opacity) ? Math.Clamp(look.Opacity, 0f, 1f) : 0.7f;
        if (look.Dark)
        {
            var accent = Pick(look.AccentLight2, Pick(look.Accent, Hex(0x99EBFF)));
            var onSurface = look.AccentOnSurface ? Pick(look.AccentDark2, default) : default;
            var yellow = Hex(0xFCE100);
            return new Tokens
            {
                Material = material,
                Opacity = opacity,
                Base = onSurface.A != 0 ? onSurface : Hex(0x1C1C1C),
                Tint = onSurface.A != 0 ? onSurface : Hex(0x202020),
                Border = Rgba(0x75, 0x75, 0x75, 0.40f),
                BorderWidth = border,
                Line = hairline,
                Hover = Rgba(255, 255, 255, 0.0588f),
                Pressed = Rgba(255, 255, 255, 0.0392f),
                HoverOnActive = Rgba(255, 255, 255, 0.0247f),
                ActiveBack = Rgba(255, 255, 255, 0.0588f),
                ActiveStroke = Rgba(255, 255, 255, 0.028f),
                ActiveLine = Rgba(255, 255, 255, 0.042f),
                LineOnTop = true,
                AttentionBack = WithAlpha(yellow, 0.14f),
                Running = Rgba(255, 255, 255, 0.5451f),
                Active = accent,
                Attention = yellow,
                Text = Hex(0xFFFFFF),
                // TextFillColorSecondary.
                SecondaryText = Rgba(255, 255, 255, 0.7725f),
                DeskIdleFill = Rgba(255, 255, 255, 0.0588f),
                DeskCurrent = accent,
                DeskCurrentText = Hex(0x000000),
                TipFill = Hex(0x2C2C2C),
                TipStroke = Rgba(0, 0, 0, 0.20f),
                TipText = Hex(0xFFFFFF),
                TipShadow = 0.26f,
                // cards: none unless chosen (§246): the widgets stand on the bar's own material; a
                // colour of one's own (Settings, Widget cards) puts a card back.
                CardFill = null,
                CardStroke = null,
                // SubtleFillColorSecondary and Tertiary; outlines between
                // ControlStrokeColorDefault and ControlStrongStrokeColorDefault,
                // so a block reads as one on acrylic; FocusStrokeColorOuter and
                // Inner; ControlSolidFillColorDefault for the badge.
                EditFill = Rgba(255, 255, 255, 0.0588f),
                EditHover = Rgba(255, 255, 255, 0.0588f),
                EditOutline = Rgba(255, 255, 255, EditLook.OutlineAlpha(dark: true)),
                EditOutlineStrong = Rgba(255, 255, 255, EditLook.OutlineStrongAlpha(dark: true)),
                FocusOuter = Hex(0xFFFFFF),
                FocusInner = Rgba(0, 0, 0, 0.70f),
                ZoneStroke = Rgba(255, 255, 255, 0.36f),
                ZoneHover = Rgba(255, 255, 255, 0.0588f),
                ZoneTargetFill = WithAlpha(accent, EditLook.GhostAlpha(dark: true)),
                ZoneTargetStroke = accent,
                TertiaryText = Rgba(255, 255, 255, 0.5442f),
            };
        }
        var lightAccent = Pick(look.AccentDark1, Pick(look.Accent, Hex(0x005FB8)));
        var amber = Hex(0x9D5D00);
        return new Tokens
        {
            Material = material,
            Opacity = opacity,
            Base = Hex(0xEEEEEE),
            Tint = Hex(0xF3F3F3),
            Border = Rgba(0x75, 0x75, 0x75, 0.40f),
            BorderWidth = border,
            Line = hairline,
            Hover = Rgba(0, 0, 0, 0.0353f),
            Pressed = Rgba(0, 0, 0, 0.0235f),
            HoverOnActive = Rgba(0, 0, 0, 0.0235f),
            ActiveBack = Rgba(0, 0, 0, 0.0353f),
            ActiveStroke = Rgba(0, 0, 0, 0.0588f),
            // Light elevation borders are darker at the bottom.
            ActiveLine = Rgba(0, 0, 0, 0.10f),
            LineOnTop = false,
            AttentionBack = WithAlpha(amber, 0.14f),
            Running = Rgba(0, 0, 0, 0.4471f),
            Active = lightAccent,
            Attention = amber,
            Text = Rgba(0, 0, 0, 0.8941f),
            SecondaryText = Rgba(0, 0, 0, 0.6196f),
            DeskIdleFill = Rgba(0, 0, 0, 0.0353f),
            DeskCurrent = lightAccent,
            DeskCurrentText = Hex(0xFFFFFF),
            TipFill = Hex(0xF9F9F9),
            TipStroke = Rgba(0, 0, 0, 0.06f),
            TipText = Rgba(0, 0, 0, 0.8941f),
            TipShadow = 0.14f,
            // cards: none unless chosen (§246), as in the dark look.
            CardFill = null,
            CardStroke = null,
            EditFill = Rgba(0, 0, 0, 0.0353f),
            EditHover = Rgba(0, 0, 0, 0.0353f),
            EditOutline = Rgba(0, 0, 0, EditLook.OutlineAlpha(dark: false)),
            EditOutlineStrong = Rgba(0, 0, 0, EditLook.OutlineStrongAlpha(dark: false)),
            FocusOuter = Rgba(0, 0, 0, 0.8941f),
            FocusInner = Rgba(255, 255, 255, 0.70f),
            ZoneStroke = Rgba(0, 0, 0, 0.32f),
            ZoneHover = Rgba(0, 0, 0, 0.0353f),
            ZoneTargetFill = WithAlpha(lightAccent, EditLook.GhostAlpha(dark: false)),
            ZoneTargetStroke = lightAccent,
            TertiaryText = Rgba(0, 0, 0, 0.4459f),
        };
    }

    private static T Implicit<T>(T animation, string property, CompositionEasingFunction easing, TimeSpan time)
        where T : KeyFrameAnimation
    {
        animation.Target = property;
        animation.InsertExpressionKeyFrame(1f, "this.FinalValue", easing);
        animation.Duration = time;
        return animation;
    }

    /// <summary>A key frame animation to <paramref name="value"/>, starting wherever the property is.</summary>
    private static ScalarKeyFrameAnimation KeyTo(Compositor c, float value, CompositionEasingFunction easing, TimeSpan time)
    {
        var animation = c.CreateScalarKeyFrameAnimation();
        animation.InsertKeyFrame(1f, value, easing);
        animation.Duration = time;
        return animation;
    }

    private static void Put(Visual visual, RectF rect)
    {
        visual.Offset = new Vector3(rect.X, rect.Y, 0f);
        visual.Size = rect.Size;
    }

    /// <summary><paramref name="size"/> centred in <paramref name="outer"/>, on whole pixels.</summary>
    private static RectF Centred(RectF outer, Vector2 size) => new(
        outer.X + MathF.Floor((outer.Width - size.X) / 2f),
        outer.Y + MathF.Floor((outer.Height - size.Y) / 2f),
        size.X,
        size.Y);

    private static DXGI_RGBA Ink(Color color) =>
        Gfx.Ink(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);

    private static Color Hex(uint rgb) =>
        Color.FromArgb(255, (byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);

    private static Color WithAlpha(Color color, float alpha) =>
        Color.FromArgb(Alpha(alpha), color.R, color.G, color.B);

    /// <summary><paramref name="preferred"/>, unless the caller left it unset.</summary>
    private static Color Pick(Color preferred, Color fallback) => preferred.A == 0 ? fallback : preferred;

    /// <summary>
    /// A system colour, which has no alpha of its own: forced opaque, and the
    /// default high contrast theme's colour when the caller left it unset.
    /// </summary>
    private static Color Opaque(Color color, Color fallback) =>
        color == default ? fallback : Color.FromArgb(255, color.R, color.G, color.B);

    private static byte Alpha(float a) => (byte)Round(Math.Clamp(a, 0f, 1f) * 255f);

    /// <summary>Half away from zero, as Rust's <c>f32::round</c>; MathF.Round alone rounds halves to even.</summary>
    private static float Round(float value) => MathF.Round(value, MidpointRounding.AwayFromZero);

    /// <summary>The colours and widths for one look; nullable ones are not drawn at all.</summary>
    internal sealed class Tokens
    {
        public Material Material;
        public float Opacity;
        public Color Base;
        public Color Tint;
        public Color Border;
        public float BorderWidth;
        public float Line;
        public Color Hover;
        public Color Pressed;
        public Color HoverOnActive;
        public bool HoverOutline;
        public Color? ActiveBack;
        public Color? ActiveStroke;
        public Color? ActiveLine;
        public bool LineOnTop;
        public Color? AttentionBack;
        public Color Running;
        public Color Active;
        public Color Attention;
        public Color Text;
        public Color SecondaryText;
        public Color? DeskIdleFill;
        public Color? DeskIdleStroke;
        public Color DeskCurrent;
        public Color DeskCurrentText;
        public Color TipFill;
        public Color TipStroke;
        public Color TipText;
        public float TipShadow;
        public Color? CardFill;
        public Color? CardStroke;

        // Edit mode: a block's fill where it has no card, the hover over
        // it, its outline (stronger when hovered or dragged), the Windows
        // focus rectangle, the remove badge, and the groups' zones, the
        // target one in the accent.
        public Color EditFill;
        public Color EditHover;
        public Color EditOutline;
        public Color EditOutlineStrong;

        public Color FocusOuter;
        public Color FocusInner;
        public Color ZoneStroke;
        public Color ZoneHover;
        public Color ZoneTargetFill;
        public Color ZoneTargetStroke;
        public Color TertiaryText;
    }

    /// <summary>
    /// The layers a part has a container in: see <see cref="Into"/>. Edit mode's wash under what stands
    /// in the back layer, and its hover and hairline under the cards' outlines in the rim layer.
    /// </summary>
    private enum Layer
    {
        Under,
        Back,
        Front,
        RimUnder,
        Rim,
        Top,
    }

    /// <summary>
    /// One part's containers, a layer each, holding everything the part shows,
    /// so the part moves as one; how far it is moved; and in edit mode its
    /// block and the block's marks. Kept by the part's key as long as the bar has it (§358).
    /// </summary>
    private sealed class PartVisuals
    {
        public required ContainerVisual Back;
        public required ContainerVisual Under;
        public required ContainerVisual BackMain;
        public required ContainerVisual Front;
        public required ContainerVisual Rim;
        public required ContainerVisual RimUnder;
        public required ContainerVisual RimMain;
        public required ContainerVisual Top;

        /// <summary>The containers, closed when the part goes.</summary>
        public required List<IDisposable> Bag;

        /// <summary>The update that last showed the part.</summary>
        public uint Seen;
        public Vector3 Shift;
        public RectF Block;
        public bool Lit;
        public Rounded? Fill;
        public Rounded? Hover;
        public Rounded? Outline;
        public Rounded? Selected;

        /// <summary>edit: the marching ants it wears while it is on edit mode's clipboard.</summary>
        public MarchingAnts? Ants;
        public Rounded? FocusOuter;
        public Rounded? FocusInner;

        /// <summary>§593: drawn as the add page's ghost, faint (<see cref="EditView.Ghost"/>).</summary>
        public bool Ghosted;

        /// <summary>§617: how much of its drawing shows: whole, a ghost's faintness, none for the empty ghost.</summary>
        public float Faint = 1f;

        /// <summary>Its edit marks forgotten as an update begins: the update finds them again, kept, where the part still has them.</summary>
        public void Unmarked() => (Fill, Hover, Outline, Selected, Ants, FocusOuter, FocusInner) = (null, null, null, null, null, null, null);
    }

    /// <summary>
    /// edit: the sections' "+" (<see cref="ComposeAdds"/>): a sprite each, in <see cref="Layout.Adds"/>' order
    /// as the last update had them, and the three looks they wear, drawn for <see cref="For"/>.
    /// </summary>
    private sealed class AddButtons : IDisposable
    {
        public readonly List<SpriteVisual> Visuals = [];
        public required CompositionSurfaceBrush Rest;
        public required CompositionSurfaceBrush Hover;
        public required CompositionSurfaceBrush Pressed;

        /// <summary>The size and the accent the looks were drawn in.</summary>
        public required Sig For;

        /// <summary>The looks' surfaces and brushes.</summary>
        public required List<IDisposable> Bag;

        public void Dispose() => Close(Bag);
    }

    /// <summary>
    /// edit: the empty blocks of the sections that hold nothing (<see cref="ComposeEmpty"/>): one shape
    /// visual over the bar, a rounded shape in it per room; one not shown has no brushes. Its rooms are
    /// made into its element's bag as they are first asked for.
    /// </summary>
    /// <summary>
    /// edit: the blue ghost (<see cref="ShowGhost"/>): a rectangle each of what it holds — a carried block one, §621 the add
    /// page's pick one a card — in a visual as big as the bar, and where they last stood, which the next grow from.
    /// </summary>
    private sealed class Ghosts : IShown
    {
        public required ShapeVisual Holder;
        public required List<IDisposable> Bag;
        public readonly List<(CompositionSpriteShape Shape, CompositionRoundedRectangleGeometry Geometry)> Boxes = [];
        public RectF[] Stood = [];

        public Visual Visual => Holder;
    }

    private sealed class EmptyBlocks : IShown
    {
        public required ShapeVisual Holder;
        public required List<IDisposable> Bag;
        public readonly List<(CompositionSpriteShape Shape, CompositionRoundedRectangleGeometry Geometry)> Rooms = [];

        public Visual Visual => Holder;
    }

    /// <summary>
    /// floating: what every dock's panel is made with (<see cref="NewFloatingPanel"/>): the strip it stands
    /// in, its hairline's brush and width, whether it casts a shadow, and the bar's panel's bag and
    /// swatches, which a dock's are made into.
    /// </summary>
    private sealed record FloatingMaker(ContainerVisual Strip, CompositionColorBrush Edge, float Hairline, bool Casts, List<IDisposable> Bag, List<Swatched> Swatches, PatternLayer Sheet);

    /// <summary>floating: one dock's panel (<see cref="FloatingPanels"/>): its material, its hairline and its shadow, each from a shape of its own.</summary>
    private sealed class FloatingPanel
    {
        public ContainerVisual Body = null!;

        /// <summary>§464: its blur and colour, cut where the cards stand (DressCards).</summary>
        public ContainerVisual Cut = null!;
        public CompositionRoundedRectangleGeometry Shape = null!;
        public ShapeVisual Outline = null!;
        public CompositionRoundedRectangleGeometry Ring = null!;

        /// <summary>How far the hairline's middle sits inside the shape, so all of it lands inside.</summary>
        public float Inset;

        /// <summary>§704: Liquid glass's glow and lens over its tint, and its rim's gradient on the hairline; none for another look.</summary>
        public Glaze? Glaze;
        public CompositionLinearGradientBrush? Rim;

        /// <summary>The shadow, its shape and what draws its mask, where the panel casts one.</summary>
        public SpriteVisual? Shadow;
        public CompositionRoundedRectangleGeometry? Cast;
        public ShapeVisual? CastSource;
        public CompositionVisualSurface? CastSurface;

        /// <summary>Its visuals the strip's size, which its shapes are placed in.</summary>
        public void Resize(Vector2 size)
        {
            Body.Size = size;
            Outline.Size = size;
            if (Shadow is not null)
            {
                Shadow.Size = size;
            }
            if (CastSource is not null)
            {
                CastSource.Size = size;
            }
            if (CastSurface is not null)
            {
                CastSurface.SourceSize = size;
            }
        }
    }

    /// <summary>Edit mode's brushes, made as it begins, coloured from the tokens at every update, closed as it ends.</summary>
    private sealed class EditBrushes(Compositor c) : IDisposable
    {
        public readonly CompositionColorBrush Fill = c.CreateColorBrush();
        public readonly CompositionColorBrush Hover = c.CreateColorBrush();
        public readonly CompositionColorBrush Outline = c.CreateColorBrush();
        public readonly CompositionColorBrush OutlineStrong = c.CreateColorBrush();
        public readonly CompositionColorBrush FocusOuter = c.CreateColorBrush();
        public readonly CompositionColorBrush FocusInner = c.CreateColorBrush();
        public readonly CompositionColorBrush Zone = c.CreateColorBrush();
        public readonly CompositionColorBrush ZoneHover = c.CreateColorBrush();
        public readonly CompositionColorBrush TargetFill = c.CreateColorBrush();
        public readonly CompositionColorBrush TargetStroke = c.CreateColorBrush();

        /// <summary>§625: the bar's own ground, under a block carried off it (<see cref="Lift"/>).</summary>
        public readonly CompositionColorBrush Ground = c.CreateColorBrush();

        public void Dye(Tokens tokens)
        {
            Fill.Color = tokens.EditFill;
            Hover.Color = tokens.EditHover;
            Outline.Color = tokens.EditOutline;
            OutlineStrong.Color = tokens.EditOutlineStrong;
            FocusOuter.Color = tokens.FocusOuter;
            FocusInner.Color = tokens.FocusInner;
            Zone.Color = tokens.ZoneStroke;
            ZoneHover.Color = tokens.ZoneHover;
            TargetFill.Color = tokens.ZoneTargetFill;
            TargetStroke.Color = tokens.ZoneTargetStroke;
            // A clear bar has none: its widgets stand on the desktop there too.
            Ground.Color = tokens.Material is Material.Clear ? default : tokens.Base;
        }

        public void Dispose()
        {
            foreach (var brush in (ReadOnlySpan<CompositionColorBrush>)[Fill, Hover, Outline, OutlineStrong, FocusOuter, FocusInner, Zone, ZoneHover, TargetFill, TargetStroke, Ground])
            {
                brush.Dispose();
            }
        }
    }

    /// <summary>A rounded shape with the brushes made for it (a card's swatches).</summary>
    private sealed class Shaped(Rounded shape, CompositionBrush? fill = null, CompositionBrush? stroke = null) : IShown
    {
        public Rounded Shape { get; } = shape;

        public CompositionBrush? Fill { get; } = fill;

        public CompositionBrush? Stroke { get; } = stroke;

        /// <summary>A card's: the part of it that shows, where it was last placed (<see cref="Card"/>).</summary>
        public RectF Shows { get; set; }

        /// <summary>§506: a plate at rest's: the colour its fill was last given (<see cref="LightPlates"/>); none before it was.</summary>
        public Color Shade { get; set; }

        public Visual Visual => Shape.Visual;
    }

    /// <summary>
    /// The focused app's background, placed once over its tile: a subtle
    /// rounded fill with a hairline border, and the border's lit edge as a
    /// second flat hairline.
    /// </summary>
    private sealed class Back : IShown
    {
        private readonly Rounded shape;
        private readonly ContainerVisual edge;
        private readonly InsetClip edgeClip;
        private readonly Rounded edgeRing;
        private Vector2 size;

        public Back(Compositor c, List<IDisposable> owned, RectF rect)
        {
            Visual = c.CreateContainerVisual();
            shape = new Rounded(c, owned);
            // The lit edge is the ring again, in the lighter colour, cut to the
            // top (or bottom) band, so it follows the corners as the taskbar's does.
            edge = c.CreateContainerVisual();
            edgeClip = c.CreateInsetClip();
            edge.Clip = edgeClip;
            edge.IsVisible = false;
            edgeRing = new Rounded(c, owned);
            edge.Children.InsertAtTop(edgeRing.Visual);
            owned.AddRange([Visual, edge, edgeClip]);
            Visual.Children.InsertAtTop(shape.Visual);
            Visual.Children.InsertAtTop(edge);
            Put(Visual, rect);
            Put(edge, new RectF(0f, 0f, rect.Width, rect.Height));
            size = rect.Size;
        }

        public ContainerVisual Visual { get; }

        Visual IShown.Visual => Visual;

        /// <summary>Stood at <paramref name="rect"/> from now on; dressed again after (<see cref="Dress"/>).</summary>
        public void Move(RectF rect)
        {
            Put(Visual, rect);
            Put(edge, new RectF(0f, 0f, rect.Width, rect.Height));
            size = rect.Size;
        }

        public void Dress(
            float radius,
            CompositionBrush fill,
            CompositionBrush? stroke,
            CompositionBrush? line,
            bool lineOnTop,
            float width)
        {
            // The taskbar draws this border a DIP wide (2 px at 150 %) on the
            // fill's own edge, over the fill: its ring is the fill plus the stroke.
            var all = new RectF(0f, 0f, size.X, size.Y);
            shape.Place(all, radius, fill, stroke, width);
            edge.IsVisible = line is not null && size.X > 0f && size.Y > 0f;
            if (line is null)
            {
                return;
            }
            edgeRing.Place(all, radius, null, line, width);
            edgeClip.TopInset = lineOnTop ? 0f : MathF.Max(0f, size.Y - width);
            edgeClip.BottomInset = lineOnTop ? MathF.Max(0f, size.Y - width) : 0f;
        }
    }

    /// <summary>
    /// search: a search box's fill and edge on the bar (SearchPill.cs): a plate under its magnifier
    /// and words, recoloured as the pointer comes and goes and the box is pressed, as Windows' are,
    /// rather than lit by a shade under it. The edge is WinUI's ControlElevationBorderBrush, the one
    /// gradient on the bar: its bottom 3 DIPs shade to the darker stroke. Windows' own search button
    /// also swaps its words for the pressed ones (<see cref="Words"/>). The part's card, kept with its
    /// brushes; its outline a card's rim of its own.
    /// </summary>
    private sealed class SearchPlate : IShown
    {
        public required Rounded Shape;
        public required CompositionColorBrush Fill;
        public required CompositionLinearGradientBrush EdgeBrush;
        public required CompositionColorGradientStop Edge;
        public required CompositionColorGradientStop Bottom;
        public SearchWords? Words;
        public SearchBoxState State;

        public Visual Visual => Shape.Visual;
    }

    /// <summary>search: the search boxes' plates, by part, as the last update counted them.</summary>
    private readonly Dictionary<int, SearchPlate> searchPlates = [];

    /// <summary>
    /// search: the plate of part <paramref name="part"/>'s search box at <paramref name="pill"/>, in
    /// the part's back layer, over its card and under its words; kept, as it stands (lit or not).
    /// </summary>
    private SearchPlate SearchPlateFor(int part, RectF pill)
    {
        var c = compositor;
        var key = $"search-plate:{(part >= 0 && part < layout.Specs.Count ? layout.Specs[part].Key : "")}";
        var radius = layout.SearchBoxRadius;
        var elevation = SearchPill.ElevationPx(layout.CellSide);
        var plate = Keep(key, Into(part, Layer.Back), new Sig("plate"),
            () =>
            {
                var (fillInk, edgeInk, bottomInk) = PlateInks(part, SearchBoxState.Rest);
                var edge = Own(c.CreateLinearGradientBrush());
                // From the bottom edge up the elevation band, in the shape's own px; past it, the edge's colour.
                edge.MappingMode = CompositionMappingMode.Absolute;
                var bottom = Own(c.CreateColorGradientStop(1f / 3f, bottomInk));
                var top = Own(c.CreateColorGradientStop(1f, edgeInk));
                edge.ColorStops.Add(bottom);
                edge.ColorStops.Add(top);
                return new SearchPlate { Shape = new Rounded(c, sink), Fill = Own(c.CreateColorBrush(fillInk)), EdgeBrush = edge, Edge = top, Bottom = bottom };
            },
            made =>
            {
                made.EdgeBrush.StartPoint = new Vector2(0f, pill.Height);
                made.EdgeBrush.EndPoint = new Vector2(0f, pill.Height - elevation);
                PlaceCard(made.Shape, pill, made.Fill, null, radius);
                // §512: the Taskbar widgets' pattern on it as on every card, a sheet of its own.
                made.Shape.Pattern(c, sink, gfx, Chosen.CardPattern, layout.Scale, Vector2.Zero);
            });
        // The part's card's outline, its edge over the highlight as every card's (§325).
        Keep($"{key}:rim", Into(part, Layer.Rim), new Sig("plate-rim", plate),
            () => new Shaped(new Rounded(c, sink)),
            rim => PlaceCard(rim.Shape, pill, null, plate.EdgeBrush, radius));
        searchPlates[part] = plate;
        return plate;
    }

    /// <summary>
    /// search: the search box <paramref name="target"/> is on lit as Windows lights it — under the
    /// pointer, or pressed — and every other one at rest.
    /// </summary>
    private void LightSearch(Hit? target, bool pressed)
    {
        if (searchPlates.Count == 0)
        {
            return;
        }
        var lit = target is { } at && !layout.SearchBoxRect(at).IsEmpty ? layout.PartOf(at) : -1;
        foreach (var (part, plate) in searchPlates)
        {
            var state = part != lit ? SearchBoxState.Rest : pressed ? SearchBoxState.Pressed : SearchBoxState.Hover;
            if (state == plate.State)
            {
                continue;
            }
            plate.State = state;
            DressPlate(part, plate, animate: true);
        }
    }

    /// <summary>search: <paramref name="plate"/>'s colours in its state (<see cref="PlateInks"/>), and its words.</summary>
    private void DressPlate(int part, SearchPlate plate, bool animate)
    {
        var (fill, edge, bottom) = PlateInks(part, plate.State);
        if (animate)
        {
            Recolour(plate.Fill, fill);
        }
        else
        {
            plate.Fill.StopAnimation("Color");
            plate.Fill.Color = fill;
        }
        plate.Edge.Color = edge;
        plate.Bottom.Color = bottom;
        if (plate.Words is { } words)
        {
            words.Brush.Surface = plate.State == SearchBoxState.Pressed ? words.Pressed : words.Rest;
        }
    }

    /// <summary>
    /// search: part <paramref name="part"/>'s search box's fill, edge and edge's bottom in
    /// <paramref name="state"/>. It is the part's card (Layout.SearchBoxed), so on every bar (§512)
    /// it takes the card and the card outline somebody chose, the bar's or its
    /// widget's own (<see cref="Chosen"/>, §325); each left Automatic is Windows' text box's
    /// (<see cref="SearchPill.Inks"/>). It changes with the pointer as Windows' does only while it has
    /// Windows' fill and highlight (<see cref="LightsAsWindows"/>); else it stays at rest and the
    /// highlight lights it as it does any card (<see cref="ShadeOf"/>).
    /// </summary>
    private (Color Fill, Color Edge, Color Bottom) PlateInks(int part, SearchBoxState state)
    {
        var chosen = Chosen;
        var inks = SearchPill.Inks(look, LightsAsWindows(part) ? state : SearchBoxState.Rest);
        return (chosen.Card is { } card ? ColorOf(card) : ColorOf(inks.Fill),
            chosen.CardOutline is { } outline ? ColorOf(outline) : ColorOf(inks.Stroke),
            chosen.CardOutline is { } rim ? ColorOf(rim) : ColorOf(inks.StrokeBottom));
    }

    /// <summary>search: whether part <paramref name="part"/>'s search box lights as Windows' does, by its own fill: neither its card nor its highlight chosen.</summary>
    private bool LightsAsWindows(int part) =>
        Chosen is var chosen && chosen.Highlight is null && chosen.Card is null;

    /// <summary>
    /// search: a plate's fill to <paramref name="color"/>, over Windows' brush transition (a hover's, §489) where
    /// animations are on.
    /// </summary>
    private void Recolour(CompositionColorBrush brush, Color color)
    {
        brush.StopAnimation("Color");
        if (!look.Animations)
        {
            brush.Color = color;
            return;
        }
        var change = compositor.CreateColorKeyFrameAnimation();
        change.InsertKeyFrame(1f, color, linear);
        change.Duration = Time(MotionKind.Hover);
        brush.StartAnimation("Color", change);
        change.Dispose();
    }


    private static Color ColorOf(TerraFX.Interop.DirectX.DXGI_RGBA ink) => Color.FromArgb(
        (byte)MathF.Round(Math.Clamp(ink.a, 0f, 1f) * 255f),
        (byte)MathF.Round(Math.Clamp(ink.r, 0f, 1f) * 255f),
        (byte)MathF.Round(Math.Clamp(ink.g, 0f, 1f) * 255f),
        (byte)MathF.Round(Math.Clamp(ink.b, 0f, 1f) * 255f));

    private sealed class Rounded : IShown
    {
        private readonly CompositionSpriteShape body;
        private readonly CompositionSpriteShape ring;
        private readonly CompositionRoundedRectangleGeometry bodyShape;
        private readonly CompositionRoundedRectangleGeometry ringShape;

        /// <summary>The width <see cref="Place"/> was last given, which a later <see cref="Stroke"/> draws in.</summary>
        private float ringWidth;

        public Rounded(Compositor c, List<IDisposable> owned)
        {
            Visual = c.CreateShapeVisual();
            bodyShape = c.CreateRoundedRectangleGeometry();
            ringShape = c.CreateRoundedRectangleGeometry();
            body = c.CreateSpriteShape(bodyShape);
            ring = c.CreateSpriteShape(ringShape);
            Visual.Shapes.Add(body);
            Visual.Shapes.Add(ring);
            owned.AddRange([Visual, bodyShape, ringShape, body, ring]);
        }

        public ShapeVisual Visual { get; }

        Visual IShown.Visual => Visual;

        /// <summary>The corners <see cref="Place"/> last gave the shape.</summary>
        public float Radius => bodyShape.CornerRadius.X;

        /// <param name="inward">
        /// How far the outline's outer edge sits inside the rectangle: 0 puts it
        /// on the edge.
        /// </param>
        public void Place(
            RectF rect,
            float radius,
            CompositionBrush? fill,
            CompositionBrush? stroke,
            float strokeWidth,
            float inward = 0f)
        {
            Put(Visual, rect);
            bodyShape.Size = rect.Size;
            bodyShape.CornerRadius = new Vector2(radius, radius);
            Fill(fill);

            // A stroke is centred on its outline: the outline sits half the
            // width inside the edge, so all of the stroke lands inside the
            // rectangle and on whole pixels.
            // edit: the arithmetic an open folder's rings share (EditLook.Stroke).
            var (at, size, corner) = EditLook.Stroke(rect.Size, radius, strokeWidth, inward);
            ringShape.Offset = at;
            ringShape.Size = size;
            ringShape.CornerRadius = new Vector2(corner, corner);
            ringWidth = strokeWidth;
            ring.StrokeBrush = stroke;
            ring.StrokeThickness = stroke is null ? 0f : strokeWidth;
            // §704: Liquid glass's light with it, down the glass shape it is part of.
            glaze?.Place(rect.Size, radius, default, glassBody);
            if (rimGradient is { } rim && ReferenceEquals(stroke, rim))
            {
                AimRim(rim);
            }
        }

        /// <summary>A new fill, the shape staying as it is.</summary>
        public void Fill(CompositionBrush? fill) => body.FillBrush = fill;

        /// <summary>§704: Liquid glass's glow and lens over the fill, made the first time; null for none.</summary>
        private Glaze? glaze;

        /// <summary>
        /// §704: the whole glass shape this one is part of where it is joined to its mates (LiquidGlass.Join), from the shape's
        /// own corner: its glass light and its rim drawn round that, cut to the shape (its visual's clip); null for its own.
        /// </summary>
        private (Vector2 At, Vector2 Size)? glassBody;

        /// <summary>§704: the clip that cuts a joined shape's glass to its own box; made the first time it is joined.</summary>
        private InsetClip? glassClip;

        /// <summary>
        /// §704: the glass shape this one is part of (<see cref="glassBody"/>), null for its own; its visual clipped to its box
        /// while joined. Called before <see cref="Place"/>.
        /// </summary>
        public void Joined(Compositor c, List<IDisposable> owned, (Vector2 At, Vector2 Size)? body)
        {
            glassBody = body;
            if (body is not null && glassClip is null)
            {
                glassClip = c.CreateInsetClip();
                owned.Add(glassClip);
            }
            Visual.Clip = body is null ? null : glassClip;
        }

        /// <summary>§704: the rim's gradient aimed down the glass shape, and its ring round that shape where it is joined.</summary>
        private void AimRim(CompositionLinearGradientBrush rim)
        {
            var (at, size) = glassBody ?? (Vector2.Zero, bodyShape.Size);
            GlassPaint.Down(rim, size, at);
            if (glassBody is { } body)
            {
                var (offset, ringSize, corner) = EditLook.Stroke(body.Size, Radius, ringWidth, 0f);
                ringShape.Offset = body.At + offset;
                ringShape.Size = ringSize;
                ringShape.CornerRadius = new Vector2(corner, corner);
            }
        }

        /// <summary>§704: the rim's gradient where the outline is Liquid glass's (<see cref="Rimmed"/>), aimed at the shape at every place.</summary>
        private CompositionLinearGradientBrush? rimGradient;

        /// <summary>
        /// §704: <paramref name="glass"/>'s glow and lens over the fill, under the outline (and what stands on the shape), at
        /// <paramref name="scale"/>; placed with it from now on. Called after <see cref="Place"/>.
        /// </summary>
        public void Glazed(Compositor c, List<IDisposable> owned, GlassLook glass, float scale)
        {
            if (glaze is null)
            {
                glaze = new Glaze(c, owned, glass, scale);
                for (var at = 0; at < glaze.Shapes.Count; at++)
                {
                    Visual.Shapes.Insert(1 + at, glaze.Shapes[at]);
                }
            }
            glaze.Place(bodyShape.Size, Radius, default, glassBody);
        }

        /// <summary>§704: the outline stroked in Liquid glass's rim, <paramref name="rim"/> (Glaze.Over), its light down the glass shape.</summary>
        public void Rimmed(CompositionLinearGradientBrush rim)
        {
            rimGradient = rim;
            Stroke(rim);
            AimRim(rim);
        }

        /// <summary>§704: the hairline toward the mate before it in a joined glass (LiquidGlass.Join), from its own corner; none for null.</summary>
        public void Divided(Compositor c, List<IDisposable> owned, Color ink, (Vector2 At, Vector2 Size)? line)
        {
            if (line is null && dividerShape is null)
            {
                return;
            }
            if (dividerShape is null)
            {
                dividerShape = c.CreateRectangleGeometry();
                dividerInk = c.CreateColorBrush();
                var sprite = c.CreateSpriteShape(dividerShape);
                sprite.FillBrush = dividerInk;
                Visual.Shapes.Insert(0, sprite);
                owned.AddRange([dividerShape, dividerInk, sprite]);
            }
            dividerInk!.Color = ink;
            (dividerShape.Offset, dividerShape.Size) = line is { } at ? (at.At, at.Size) : (Vector2.Zero, Vector2.Zero);
        }

        /// <summary>§704: the divider's shape and ink (<see cref="Divided"/>); null before it ever had one.</summary>
        private CompositionRectangleGeometry? dividerShape;
        private CompositionColorBrush? dividerInk;

        /// <summary>§462: the pattern over the fill, in its shape, made the first time there is one.</summary>
        private PatternLayer? pattern;

        /// <summary>tests: its pattern's layer; null before it ever had one.</summary>
        internal PatternLayer? PatternLayer => pattern;

        /// <summary>
        /// §462: <paramref name="patterned"/> over the fill, in its shape as <see cref="Place"/> placed it, its corner standing at
        /// <paramref name="at"/> on its sheet; none for null. Called after <see cref="Place"/>.
        /// </summary>
        public void Pattern(Compositor c, List<IDisposable> owned, Gfx gfx, Patterned? patterned, float scale, Vector2 at)
        {
            if (patterned is null && pattern is null)
            {
                return;
            }
            if (pattern is null)
            {
                pattern = new PatternLayer(c);
                var clip = c.CreateGeometricClip(bodyShape);
                pattern.Visual.Clip = clip;
                Visual.Children.InsertAtTop(pattern.Visual);
                owned.AddRange([pattern, clip]);
            }
            pattern.Show(gfx, patterned, scale, bodyShape.Size, at);
        }

        /// <summary>
        /// glide: the shape grows (or shrinks) to where <see cref="Place"/> put it from a rectangle
        /// <paramref name="was"/> long, its outline keeping its inset all the way.
        /// </summary>
        public void GrowFrom(Compositor c, Vector2 was, CompositionEasingFunction ease, TimeSpan time)
        {
            var less = bodyShape.Size - ringShape.Size;
            Grow(bodyShape, was, bodyShape.Size);
            Grow(ringShape, Vector2.Max(was - less, Vector2.Zero), ringShape.Size);

            void Grow(CompositionRoundedRectangleGeometry geometry, Vector2 from, Vector2 to)
            {
                if (from == to)
                {
                    return;
                }
                var grow = c.CreateVector2KeyFrameAnimation();
                grow.InsertKeyFrame(0f, from);
                grow.InsertKeyFrame(1f, to, ease);
                grow.Duration = time;
                geometry.StartAnimation("Size", grow);
                grow.Dispose();
            }
        }

        /// <summary>
        /// A new outline colour, the shape and its width staying as they are. No colour means no
        /// outline, and the width the shape was placed with comes back when one is given again: a
        /// brush on a ring left nought wide would have shown nothing.
        /// </summary>
        public void Stroke(CompositionBrush? stroke)
        {
            ring.StrokeBrush = stroke;
            ring.StrokeThickness = stroke is null ? 0f : ringWidth;
        }

        /// <summary>
        /// Dashes the outline: <paramref name="dash"/> on, <paramref name="gap"/>
        /// off, in stroke widths as Direct2D counts them; a 0 dash draws it solid.
        /// </summary>
        public void Dash(float dash, float gap)
        {
            var array = ring.StrokeDashArray;
            if (dash > 0f && array.Count == 2 && array[0] == dash && array[1] == gap)
            {
                return;
            }
            array.Clear();
            if (dash > 0f)
            {
                array.Add(dash);
                array.Add(gap);
            }
        }
    }

    /// <summary>
    /// A drop shadow in the shape of a rounded rectangle. The sprite has no
    /// brush, so only its shadow shows; the mask is a composition-rendered
    /// rounded rectangle, which keeps the shadow off the corners.
    /// </summary>
    private sealed class Shadow
    {
        private readonly ShapeVisual source;
        private readonly CompositionRoundedRectangleGeometry geometry;
        private readonly CompositionVisualSurface surface;

        public Shadow(Compositor c, List<IDisposable> owned, float radius, float blur, float offsetY, float opacity)
        {
            geometry = c.CreateRoundedRectangleGeometry();
            geometry.CornerRadius = new Vector2(radius, radius);
            var shape = c.CreateSpriteShape(geometry);
            var black = c.CreateColorBrush(Hex(0x000000));
            shape.FillBrush = black;
            source = c.CreateShapeVisual();
            source.Shapes.Add(shape);
            surface = c.CreateVisualSurface();
            surface.SourceVisual = source;
            var mask = c.CreateSurfaceBrush(surface);
            var shadow = c.CreateDropShadow();
            shadow.Mask = mask;
            shadow.BlurRadius = blur;
            shadow.Offset = new Vector3(0f, offsetY, 0f);
            shadow.Color = Hex(0x000000);
            shadow.Opacity = opacity;
            Visual = c.CreateSpriteVisual();
            Visual.Shadow = shadow;
            owned.AddRange([geometry, shape, black, source, surface, mask, shadow, Visual]);
        }

        public SpriteVisual Visual { get; }

        public void Resize(Vector2 size)
        {
            Visual.Size = size;
            source.Size = size;
            geometry.Size = size;
            surface.SourceSize = size;
        }
    }
}
