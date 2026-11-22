// A folder's face on the bar: how each face of Karpo.Core\FolderFaces.cs is measured and drawn
// (docs\widgets.md §67). The folder's button is its faces (FolderIsland), no name beside them (§427);
// the face is one of these, found by the id the file stores.
//
// A face takes the room what it shows now needs, and no more (the person, 2026-10-19: "size all
// faces to their current content"; docs\widgets.md §244, which ends §12.2's rule for faces): a
// clock is as wide as the time it reads, a headline as its words up to the face's most, cut with
// "…" past it. Figures are tabular (TextLine), so a clock's seconds tick in one width and only a
// figure more or fewer moves it. What it shows now is its Reading: the dock asks for it on its
// clock's tick (a minute, a second where a face shows seconds) and the cell is painted again in
// place when it has changed; where the face now wants another width, the bar is laid out again
// (CellIsland.Redraw, Dock.FaceTicks), once for all that changed in the tick, and glides.
//
// Adding a face: its descriptor in FolderFaces, a class here with its Measure, Paint and Reading,
// one line in All, and its words.

using System.Numerics;
using Karpo.Core;
using Karpo.Dock.Native;
using Karpo.Dock.Panels;
using Karpo.Dock.QuickSettings.Services;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock;

/// <summary>What a face is measured and drawn with.</summary>
/// <param name="Gfx">For text, measured as templates only.</param>
/// <param name="Style">The bar's numbers.</param>
/// <param name="Options">The folder's settings: its face, the face's options, what it holds.</param>
/// <param name="Folder">The folder wearing it, live, where it is drawn or the bar measures it; null where only its settings are (Settings' picture), which measures what it paints with none.</param>
/// <param name="Machine">The machine facts a face may be measured by (a battery or none); null for this PC's.</param>
/// <param name="Worn">Which of the folder's faces (FolderOptions.Faces) this is; null for its first.</param>
/// <param name="Room">The button's cell the face is drawn in, which a count on its corner keeps inside; null for a cell's square about the face's middle.</param>
internal readonly record struct FaceContext(Gfx Gfx, IslandStyle Style, FolderOptions Options, FolderIsland? Folder = null, WidgetMachine? Machine = null, FolderFace? Worn = null,
    D2D_RECT_F? Room = null)
{
    /// <summary>The face measured or drawn, and its options as the file has them.</summary>
    public FolderFace Face => Worn ?? Options.Face;

    public Fit Fit => new(Style);

    public bool Vertical => Style.Vertical;
}

/// <summary>
/// A face's size along the bar, in px, without the button's own room at its ends; and whether it
/// is a square the size of a glyph, drawn in the middle of the bar's thickness rather than across
/// all of it (FolderIsland.FaceBoxes). <paramref name="Trail"/> is how far past its end a
/// count on its corner stands, at its widest: the button keeps that room after it (before the next
/// face, the name or its own end), so the count is never cut (docs\widgets.md §169).
/// </summary>
internal readonly record struct FaceSize(float Along, bool Square, float Trail = 0);

/// <summary>How a face is measured and drawn on the bar.</summary>
internal unsafe interface IFaceDrawing
{
    /// <summary>Which face (<see cref="FaceInfo.Id"/>).</summary>
    string Id { get; }

    /// <summary>Its size: what it shows now (<see cref="FaceContext.Folder"/>'s), up to the face's most (docs\widgets.md §244).</summary>
    FaceSize Measure(in FaceContext context);

    /// <summary>Paints it into <paramref name="box"/>, which is its <see cref="Measure"/> along the bar and the cell across.</summary>
    void Paint(ID2D1DeviceContext* dc, in FaceContext context, D2D_RECT_F box);

    /// <summary>What it shows now, to tell when it must be painted again; empty for a face that never changes by itself.</summary>
    string Reading(in FaceContext context);

    /// <summary>Whether it changes every second: the dock's clock then ticks every second.</summary>
    bool Seconds(FolderFace face);

    /// <summary>What hovering it says, or null for the folder's own words.</summary>
    string? Tooltip(in FaceContext context);

    /// <summary>
    /// Why what it shows is not on this PC (no battery, no Bluetooth radio), or null where it is:
    /// it is then painted as it would be, a stand-in for the reading, greyed (<see cref="Unavailable"/>),
    /// and this is what hovering it says (docs\widgets.md §247).
    /// </summary>
    string? Unavailable(in FaceContext context) => null;

    /// <summary>
    /// Whether <paramref name="face"/> of <paramref name="folder"/> has nothing to show now, and so
    /// leaves the bar at rest: the removable devices with nothing plugged in, now playing set to hide
    /// while nothing plays, the meeting face set to show one only shortly before it (§327). Its room
    /// and the room beside it go; while the bar is arranged it shows as it is, greyed, so it can be
    /// found and set (FolderIsland.FacesOnBar, docs\widgets.md §290, §319). A folder's line has its
    /// own (<see cref="CellIsland.NothingToShow"/>).
    /// </summary>
    bool NothingToShow(FolderFace face, FolderIsland folder) => false;
}

internal static unsafe class FaceDrawings
{
    private static readonly IFaceDrawing[] All =
    [
        new IconFace(), new ClockFaceDrawing(), new CalendarFaceDrawing(), new DateTimeFaceDrawing(), new WeatherFaceDrawing(),
        new StatusFaceDrawing(FolderFaces.WifiId), new StatusFaceDrawing(FolderFaces.BluetoothId), new StatusFaceDrawing(FolderFaces.SoundId),
        new RemovableFaceDrawing(),
        new CountdownFaceDrawing(FolderFaces.TimerId), new CountdownFaceDrawing(FolderFaces.FocusId), new CountdownFaceDrawing(FolderFaces.StopwatchId),
        new HotspotFaceDrawing(), new CastFaceDrawing(), new NearbyFaceDrawing(),
        new BatteryFaceDrawing(), new PowerFaceDrawing(),
        new MailFaceDrawing(),
        new NowPlayingFaceDrawing(),
        new PerformanceFaceDrawing(FolderFaces.CpuId, Metric.Cpu), new PerformanceFaceDrawing(FolderFaces.GpuId, Metric.Gpu),
        new PerformanceFaceDrawing(FolderFaces.MemoryId, Metric.Memory), new PerformanceFaceDrawing(FolderFaces.DiskId, Metric.Disk),
        new PerformanceFaceDrawing(FolderFaces.NetworkActivityId, Metric.Network),
        new ScreenTimeFaceDrawing(),
        new MarketFaceDrawing(), new NewsFaceDrawing(),
        new DriveFaceDrawing(FolderFaces.DriveId, WidgetInfos.Drive), new DriveFaceDrawing(FolderFaces.NetworkDriveId, WidgetInfos.NetworkDrive),
        new NotificationsFaceDrawing(),
        new ClipboardFaceDrawing(),
        new ContactsFaceDrawing(),
        new WinSearchFaceDrawing(),
        new DesktopsFaceDrawing(),
        new KeyboardFaceDrawing(),
        new SwitchFaceDrawing(FolderFaces.VpnId),
        new LayoutsFaceDrawing(),
        new MeetingFaceDrawing(),
        new AirFaceDrawing(),
        new TidesFaceDrawing(),
        new Sports.SportsFaceDrawing(),
        new QuakeFaceDrawing(),
        new SunFaceDrawing(),
    ];

    /// <summary>The drawing of <paramref name="face"/>; its icon for a face this build does not know.</summary>
    public static IFaceDrawing Of(FolderFace face)
    {
        var id = face.Info.Id;
        foreach (var drawing in All)
        {
            if (drawing.Id == id)
            {
                return drawing;
            }
        }
        return All[0];
    }

    /// <summary>Every face has a drawing: what the tests hold the two registries to.</summary>
    internal static IEnumerable<string> Ids => All.Select(drawing => drawing.Id);

    /// <summary>U+2236 RATIO: the taskbar's clock sets its colons with it.</summary>
    internal const char Ratio = (char)0x2236;

    /// <summary>Between the time and what follows it, as the bar's clock has it.</summary>
    internal const string Dot = " · ";

    /// <summary>One line of text centred across <paramref name="box"/> and along it, in tabular figures.</summary>
    internal static void Line(ID2D1DeviceContext* dc, Gfx gfx, string text, float px, DXGI_RGBA color, D2D_RECT_F box, float lineHeight)
    {
        if (text.Length == 0)
        {
            return;
        }
        ID2D1SolidColorBrush* brush = null;
        var line = TextLine.Lay(gfx, text, px, Weight.Regular, MathF.Max(1, box.right - box.left));
        try
        {
            brush = Gfx.Brush(dc, color);
            line.Draw(
                dc,
                brush,
                color,
                TextLine.Round(box.left + ((box.right - box.left - line.Width) / 2)),
                TextLine.Round(box.top + ((box.bottom - box.top - lineHeight) / 2)));
        }
        finally
        {
            line.Release();
            ComUtil.Release(ref brush);
        }
    }

    /// <summary>
    /// A text's rows down a side bar, as <see cref="VerticalFit.Rows(Gfx, in Fit, int, Func{int, IEnumerable{string[]}}, Weight, float)"/>
    /// set them: one under another from <paramref name="top"/>, each centred across <paramref name="box"/>.
    /// </summary>
    internal static void Rows(ID2D1DeviceContext* dc, Gfx gfx, string[] rows, in VerticalFit.SideRows plan, DXGI_RGBA color, D2D_RECT_F box, float top)
    {
        var y = top;
        foreach (var row in rows)
        {
            Line(dc, gfx, row, plan.Px, color, new D2D_RECT_F(box.left, y, box.right, y + plan.Height), plan.Height);
            y += plan.Height;
        }
    }

    /// <summary>
    /// How long <paramref name="name"/> is after a glyph (<see cref="GlyphAndName"/>): its words as they
    /// read, up to the room of "Desktop 88" (<see cref="Switchers.NameRoom"/>), past which it is cut (§244).
    /// </summary>
    internal static float NameLength(in FaceContext context, string name)
    {
        var px = context.Fit.Primary;
        var most = TextLine.Template(context.Gfx, Switchers.NameRoom, px, Weight.Regular).Width;
        return name.Length == 0 ? 0 : MathF.Min(most, TextLine.Measure(context.Gfx, name, px, Weight.Regular, null));
    }

    /// <summary>What a face says while it has nothing to show (§388): a dash, never a sentence.</summary>
    internal const string Dash = "–";

    /// <summary>
    /// A face with nothing to show (§388; the person, 2026-10-29: "an empty face on the bar is its glyph and
    /// '–', no words like 'No calendar'"): its glyph and a dash in the secondary ink after it, in the middle of
    /// <paramref name="box"/>; the glyph alone down a bar on its side, or where the two do not fit.
    /// </summary>
    internal static void Empty(ID2D1DeviceContext* dc, Gfx gfx, in IslandStyle style, D2D_RECT_F box, string? glyph)
    {
        var width = box.right - box.left;
        var height = box.bottom - box.top;
        var fit = new Fit(style);
        var ink = new IslandInk(style.Look);
        var size = fit.Glyph();
        var dash = PopupPaint.Measure(gfx, Dash, fit.Primary, Weight.Regular);
        var withGlyph = glyph is not null;
        var withDash = !style.Vertical && (!withGlyph || size + fit.Gap + dash.X <= width + 0.5f);
        var across = (withGlyph ? size : 0) + (withGlyph && withDash ? fit.Gap : 0) + (withDash ? dash.X : 0);
        var x = box.left + MathF.Floor((width - across) / 2);
        dc->SetTextAntialiasMode(D2D1_TEXT_ANTIALIAS_MODE.D2D1_TEXT_ANTIALIAS_MODE_GRAYSCALE);
        if (withGlyph)
        {
            PopupPaint.Glyph(dc, gfx, glyph!, size, ink.Text, PopupPaint.Rect(x, box.top + MathF.Floor((height - size) / 2), size, size));
            x += size + fit.Gap;
        }
        if (withDash)
        {
            PopupPaint.Text(dc, gfx, Dash, fit.Primary, Weight.Regular, ink.Secondary, x, box.top + MathF.Round((height - dash.Y) / 2));
        }
    }

    /// <summary>How wide <see cref="Empty"/> is across a horizontal bar: the glyph, a gap and the dash.</summary>
    internal static float EmptyWidth(Gfx gfx, in Fit fit) => MathF.Ceiling(fit.Glyph() + fit.Gap + PopupPaint.Measure(gfx, Dash, fit.Primary, Weight.Regular).X);

    /// <summary>
    /// A glyph and a name after it across a horizontal bar, as the desktops' and the layouts' faces
    /// have them: the name a gap after the glyph, cut at the room's end.
    /// </summary>
    internal static void GlyphAndName(ID2D1DeviceContext* dc, in FaceContext context, D2D_RECT_F box, string glyphText, string name, DXGI_RGBA colour)
    {
        var fit = context.Fit;
        var glyph = fit.Glyph();
        var middle = (box.top + box.bottom) / 2;
        var text = TextLine.Lay(context.Gfx, name, fit.Primary, Weight.Regular, MathF.Max(1, box.right - (box.left + glyph + fit.Gap)));
        ID2D1SolidColorBrush* brush = null;
        try
        {
            var left = box.left;
            PopupPaint.Glyph(dc, context.Gfx, glyphText, glyph, colour, PopupPaint.Rect(left, middle - (glyph / 2), glyph, glyph));
            brush = Gfx.Brush(dc, colour);
            text.Draw(dc, brush, colour, left + glyph + fit.Gap, middle - (text.Height / 2));
        }
        finally
        {
            ComUtil.Release(ref brush);
            text.Release();
        }
    }

    /// <summary>One line of text in the middle of <paramref name="box"/>'s width, its top at <paramref name="top"/>, cut to the box.</summary>
    internal static void Put(ID2D1DeviceContext* dc, Gfx gfx, string text, float px, DXGI_RGBA color, D2D_RECT_F box, float top)
    {
        if (text.Length == 0)
        {
            return;
        }
        ID2D1SolidColorBrush* brush = null;
        var room = MathF.Max(1, box.right - box.left);
        var line = TextLine.Lay(gfx, text, px, Weight.Regular, room);
        try
        {
            brush = Gfx.Brush(dc, color);
            line.Draw(dc, brush, color, TextLine.Round(box.left + MathF.Max(0, (room - line.Width) / 2)), top);
        }
        finally
        {
            line.Release();
            ComUtil.Release(ref brush);
        }
    }
}

/// <summary>Its icon: the one somebody chose, or the folder glyph.</summary>
internal sealed unsafe class IconFace : IFaceDrawing
{
    public string Id => FolderFaces.IconId;

    public FaceSize Measure(in FaceContext context) => new(context.Fit.Glyph(), Square: true);

    public void Paint(ID2D1DeviceContext* dc, in FaceContext context, D2D_RECT_F box)
    {
        var glyph = context.Face.GlyphOrDefault;
        PopupPaint.Glyph(dc, context.Gfx, glyph, box.right - box.left, new IslandInk(context.Style.Look).Text, box);
    }

    public string Reading(in FaceContext context) => "";

    public bool Seconds(FolderFace face) => false;

    public string? Tooltip(in FaceContext context) => null;
}

/// <summary>
/// The network's, Bluetooth's or the volume's glyph as Windows' icons on the taskbar show them, as
/// the network, Bluetooth and sound buttons on the bar drew them until they went (2026-10-10), with
/// their drawing (<see cref="SystemGlyph.Paint"/>), from what SystemStatus reads (<see cref="FolderIsland.Status"/>):
/// the Wi-Fi's bars with the arcs a weak signal does not reach faded, the cable or no connection,
/// and a VPN's shield; Bluetooth's rune, dimmed while the radio is off or where there is none; the
/// speaker's waves for the level, or crossed out while muted. A glyph's square and the button's ends (§275);
/// before anything is read, the face's own glyph. Its tooltip is the icon's: the network and whether
/// it reaches the internet, whether Bluetooth is on, or the output and its level.
/// </summary>
internal sealed unsafe class StatusFaceDrawing(string id) : IFaceDrawing
{
    public string Id => id;

    public FaceSize Measure(in FaceContext context) => new(context.Fit.Glyph(), Square: true);

    public void Paint(ID2D1DeviceContext* dc, in FaceContext context, D2D_RECT_F box)
    {
        var (glyph, back, dim, badge) = Glyphs(id, context.Folder?.Status);
        SystemGlyph.Paint(dc, context.Gfx, context.Style, glyph, back, dim, badge, box);
    }

    /// <summary>The glyphs and the words: a new network, a signal that fell, a radio switched, a level turned paint it again.</summary>
    public string Reading(in FaceContext context)
    {
        var (glyph, back, dim, badge) = Glyphs(id, context.Folder?.Status);
        return $"{glyph}|{back}|{dim}|{badge}|{Tooltip(context)}";
    }

    public bool Seconds(FolderFace face) => false;

    public string? Tooltip(in FaceContext context) => Tip(id, context.Folder?.Status);

    /// <summary>
    /// What the face draws for <paramref name="state"/>: the glyph, the one faded under it (the
    /// Wi-Fi's arcs), whether it is dimmed, and the VPN's shield over it. Pure: the tests ask it.
    /// </summary>
    internal static (string Glyph, string? Back, bool Dim, string? Badge) Glyphs(string id, SystemState? state)
    {
        var face = FolderFaces.Of(id);
        if (state is not { } value)
        {
            return (face.Glyph, null, false, null);
        }
        return id switch
        {
            FolderFaces.WifiId => (value.NetworkGlyph, value.NetworkBackGlyph, false, value.NetworkBadge),
            FolderFaces.SoundId => (value.VolumeGlyph, null, false, null),
            // No radio: the rune as it is, which the folder greys (Unavailable).
            _ => (value.BluetoothGlyph ?? face.Glyph, null, value.BluetoothGlyph is not null && !value.BluetoothOn, null),
        };
    }

    /// <summary>Bluetooth on a PC with no radio: greyed, and why (§247).</summary>
    public string? Unavailable(in FaceContext context) =>
        id == FolderFaces.BluetoothId && context.Folder?.Status is { BluetoothGlyph: null } ? S.WidgetSystemControlBluetoothUnavailable : null;

    /// <summary>What hovering the face says for <paramref name="state"/>; null, the folder's own words, before anything is read.</summary>
    internal static string? Tip(string id, SystemState? state) => state is not { } value
        ? null
        : id switch
        {
            FolderFaces.WifiId => value.NetworkTip.Length > 0 ? value.NetworkTip : null,
            FolderFaces.SoundId => value.VolumeTip.Length > 0 ? value.VolumeTip : null,
            _ => value.BluetoothGlyph is null ? S.WidgetSystemControlBluetoothUnavailable
                : value.BluetoothOn ? S.WidgetSystemControlBluetoothOn : S.WidgetSystemControlBluetoothOff,
        };
}

/// <summary>
/// The removable devices: the USB stick as the removable-devices button on the bar drew it until it
/// went (2026-10-10), from what the folder reads (<see cref="FolderIsland.Devices"/>). With nothing
/// plugged in it is drawn quiet rather than taken away — a zero would be noise, and dimming is what
/// Windows does for a control with nothing to act on — and from two devices a count stands on its
/// corner in an accent pill, inside the cell (one is what the glyph already says; past nine, "9+").
/// A glyph's square and its widest count's room, kept whatever it shows (§276). Its tooltip is the button's: how many
/// and their names, or that nothing is plugged in.
/// </summary>
internal sealed unsafe class RemovableFaceDrawing : IFaceDrawing
{
    /// <summary>The most a badge counts; past it the badge says "9+" and the folder has the rest.</summary>
    private const int MostCounted = 9;

    public string Id => FolderFaces.RemovableId;

    /// <summary>A glyph's square, and the room past it its widest count ("9+") stands in, kept whatever it shows (§276).</summary>
    public FaceSize Measure(in FaceContext context) =>
        new(context.Fit.Glyph(), Square: true, CountTrail(context.Gfx, context.Style, $"{MostCounted}+"));

    /// <summary>Where it is only measured (no folder), the stick as though one were plugged in.</summary>
    public void Paint(ID2D1DeviceContext* dc, in FaceContext context, D2D_RECT_F box) =>
        Paint(dc, context.Gfx, context.Style, context.Folder?.Devices.Count ?? 1, box, context.Room);

    /// <summary>How many, dimmed or not, and the names: a device plugged in or taken out paints it again.</summary>
    public string Reading(in FaceContext context) =>
        context.Folder is { } folder ? $"{folder.Devices.Count}|{Tooltip(context)}" : "";

    public bool Seconds(FolderFace face) => false;

    public string? Tooltip(in FaceContext context) => context.Folder is { } folder ? RemovableIsland.Tip(folder.Devices) : null;

    /// <summary>Nothing plugged in: off the bar at rest, the person, 2026-10-22 (§290).</summary>
    public bool NothingToShow(FolderFace face, FolderIsland folder) => folder.Devices.Count == 0;

    /// <summary>The badge's text for <paramref name="count"/> devices, or null for none: from two up. Pure: the tests ask it.</summary>
    internal static string? Badge(int count) =>
        count < 2 ? null : count > MostCounted ? $"{MostCounted}+" : count.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>The stick for <paramref name="count"/> devices in the glyph's square <paramref name="box"/>, as the button drew it.</summary>
    internal static void Paint(ID2D1DeviceContext* dc, Gfx gfx, in IslandStyle style, int count, D2D_RECT_F box, D2D_RECT_F? room = null)
    {
        var ink = new IslandInk(style.Look);
        var badge = Badge(count);
        dc->SetTextAntialiasMode(D2D1_TEXT_ANTIALIAS_MODE.D2D1_TEXT_ANTIALIAS_MODE_GRAYSCALE);
        ID2D1SolidColorBrush* brush = null;
        TextLine glyph = default;
        try
        {
            var side = box.right - box.left;
            brush = Gfx.Brush(dc, ink.Text);
            glyph = TextLine.Lay(gfx, RemovableIsland.UsbGlyph, side, Weight.Regular, null, gfx.IconFamily);
            // Quiet with nothing plugged in: the stick is there, and says there is nothing in it.
            glyph.Draw(dc, brush, count == 0 ? ink.Disabled : ink.Text,
                box.left + ((side - glyph.Width) / 2), box.top + ((box.bottom - box.top - glyph.Height) / 2));
        }
        finally
        {
            glyph.Release();
            ComUtil.Release(ref brush);
        }
        if (badge is not null)
        {
            PaintCount(dc, gfx, style, badge, box, room: room);
        }
    }

    /// <summary>
    /// How far past the glyph's square <paramref name="widest"/>'s pill stands across a bar
    /// (<see cref="FaceSize.Trail"/>); nothing down a side, where it stands across the bar's width.
    /// </summary>
    internal static float CountTrail(Gfx gfx, in IslandStyle style, string widest) =>
        style.Vertical ? 0 : MathF.Ceiling(CountPill(gfx, style, widest).Width * (1 - 0.4f));

    /// <summary>The count's size and its pill's width and height.</summary>
    private static (float Px, float Width, float Height) CountPill(Gfx gfx, in IslandStyle style, string count)
    {
        var fit = new Fit(style);
        var px = MathF.Max(10 * fit.Scale * fit.TextScale, fit.Small * 0.9f);
        var (textWidth, textHeight) = TextLine.Template(gfx, count, px, Weight.Semibold);
        var height = MathF.Ceiling(MathF.Max(textHeight + fit.Px(1), fit.Px(15)));
        return (px, MathF.Ceiling(MathF.Max(height, textWidth + fit.Px(8))), height);
    }

    /// <summary>
    /// A count in an accent pill over the corner of the glyph's square <paramref name="box"/>: the
    /// removable face's devices, the hotspot face's. The face keeps the room its widest stands past the
    /// glyph whatever it shows (<see cref="CountTrail"/>, §276), and the folder's cell holds it.
    /// <paramref name="low"/> sets it on the bottom corner instead, for a glyph whose ink leaves
    /// that corner empty; <paramref name="out"/> is how much of the pill stands past the box's edge;
    /// <paramref name="room"/> the button's cell it is held in, else a cell's square about the box's middle.
    /// </summary>
    internal static void PaintCount(ID2D1DeviceContext* dc, Gfx gfx, in IslandStyle style, string count, D2D_RECT_F box, bool low = false, float @out = 0.35f,
        D2D_RECT_F? room = null)
    {
        var fit = new Fit(style);
        var ink = new IslandInk(style.Look);
        ID2D1SolidColorBrush* brush = null;
        TextLine figures = default;
        try
        {
            var (px, pillWidth, pillHeight) = CountPill(gfx, style, count);
            // Held inside the button's cell, else a square of the bar's cell about the glyph's
            // middle, as the button held it inside its own.
            var held = room ?? GlyphBadge.Around(box, fit.Cell);
            var x = TextLine.Round(MathF.Min(box.right - (pillWidth * 0.4f), held.right - pillWidth - fit.Px(1)));
            var y = low
                ? MathF.Min(held.bottom - pillHeight - fit.Px(1), TextLine.Round(box.bottom - (pillHeight * (1 - @out))))
                : MathF.Max(held.top + fit.Px(1), TextLine.Round(box.top - (pillHeight * @out)));
            PopupPaint.Fill(dc, PopupPaint.Rect(x, y, pillWidth, pillHeight), pillHeight / 2, ink.Accent);
            var onAccent = OnAccent(style.Look);
            brush = Gfx.Brush(dc, onAccent);
            figures = TextLine.Lay(gfx, count, px, Weight.Semibold, null);
            figures.Draw(dc, brush, onAccent, x + ((pillWidth - figures.Width) / 2), y + ((pillHeight - figures.Height) / 2));
        }
        finally
        {
            figures.Release();
            ComUtil.Release(ref brush);
        }
    }

    /// <summary>TextOnAccentFillColorPrimary for the Look, as the bell's badge has it.</summary>
    private static DXGI_RGBA OnAccent(in Look look)
    {
        if (look.HighContrast)
        {
            var colour = look.HcHighlightText;
            return colour.A == 0 ? new DXGI_RGBA(1, 1, 1, 1) : new DXGI_RGBA(colour.R / 255f, colour.G / 255f, colour.B / 255f, 1);
        }
        return look.Dark ? new DXGI_RGBA(0, 0, 0, 1) : new DXGI_RGBA(1, 1, 1, 1);
    }
}

/// <summary>
/// The mobile hotspot, in place of the mobile hotspot button that went (2026-10-10, docs\widgets.md
/// §93): the antenna Windows' hotspot icon on the taskbar shows, drawn as the Wi-Fi and Bluetooth faces beside it are
/// (<see cref="SystemGlyph.Paint"/>), from what the folder's own hotspot source reads
/// (<see cref="FolderIsland.Hotspot"/>). Dimmed while the hotspot is off, while Windows has not
/// answered yet, and where this PC cannot host one, as the Bluetooth face is with its radio off;
/// while it is on, how many devices are on it stands on its bottom corner, beside the mast, in the
/// removable face's accent pill, from one ("9+" past nine), since who is on it is what a hotspot
/// that is on is for. A
/// glyph's square and its widest count's room, kept whatever it shows (§276). Its tooltip says whether it is on and how many of how many devices
/// are on it, or why this PC cannot host one.
/// </summary>
internal sealed unsafe class HotspotFaceDrawing : IFaceDrawing
{
    /// <summary>The most a badge counts; past it the badge says "9+".</summary>
    private const int MostCounted = 9;

    public string Id => FolderFaces.HotspotId;

    /// <summary>A glyph's square, and the room past it its widest count ("9+") stands in, kept whatever it shows (§276).</summary>
    public FaceSize Measure(in FaceContext context) =>
        new(context.Fit.Glyph(), Square: true, RemovableFaceDrawing.CountTrail(context.Gfx, context.Style, $"{MostCounted}+"));

    /// <summary>Where it is only measured (no folder), the glyph undimmed, as the face's picture in Settings.</summary>
    public void Paint(ID2D1DeviceContext* dc, in FaceContext context, D2D_RECT_F box)
    {
        var (dim, badge) = context.Folder is { } folder ? Look(folder.Hotspot) : (false, null);
        SystemGlyph.Paint(dc, context.Gfx, context.Style, FolderFaces.Hotspot.Glyph, null, dim, null, box);
        if (badge is not null)
        {
            // On the antenna's bottom corner, beside its mast, which its ink leaves empty, rather
            // than over its arcs; by the antenna as drawn (the taskbar's icon size, smaller than the
            // square) and standing little below it, so the ink keeps to the cell's middle.
            var side = new Fit(context.Style).SystemIcon;
            var top = (box.top + box.bottom - side) / 2;
            RemovableFaceDrawing.PaintCount(dc, context.Gfx, context.Style, badge, new D2D_RECT_F(box.left, top, box.right, top + side), low: true, @out: 0.1f,
                room: context.Room);
        }
    }

    /// <summary>Dimmed or not, the count and the words: the hotspot switched, a device joining or leaving paint it again.</summary>
    public string Reading(in FaceContext context)
    {
        if (context.Folder is not { } folder)
        {
            return "";
        }
        var state = folder.Hotspot;
        var (dim, badge) = Look(state);
        return $"{dim}|{badge}|{Tip(state)}";
    }

    public bool Seconds(FolderFace face) => false;

    public string? Tooltip(in FaceContext context) => context.Folder is { } folder ? Tip(folder.Hotspot) : null;

    /// <summary>
    /// What the face draws for <paramref name="state"/>: whether the glyph is dimmed, and the count on
    /// its corner, or none. Pure: the tests ask it.
    /// </summary>
    internal static (bool Dim, string? Badge) Look(HotspotState? state) => state switch
    {
        { Can: HotspotCan.Yes, On: true } on => (false, Badge(on.Clients)),
        // No adapter to host one: the antenna as it is, which the folder greys (Unavailable).
        { Can: HotspotCan.NoAdapter } => (false, null),
        _ => (true, null),
    };

    /// <summary>A PC with no adapter that can host one: greyed, and why (§247).</summary>
    public string? Unavailable(in FaceContext context) =>
        context.Folder is { Hotspot: { Can: HotspotCan.NoAdapter } state } ? Tip(state) : null;

    /// <summary>The badge's text for <paramref name="clients"/> devices on it, or null for none: from one up. Pure.</summary>
    internal static string? Badge(int clients) =>
        clients < 1 ? null : clients > MostCounted ? $"{MostCounted}+" : clients.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// What hovering it says for <paramref name="state"/>: "Mobile hotspot: on" and how many of how
    /// many devices are on it; "Mobile hotspot: off"; why this PC cannot host one; its name alone
    /// while Windows has not said. Null, the folder's own words, where there is no source. Pure.
    /// </summary>
    internal static string? Tip(HotspotState? state)
    {
        if (state is not { } now)
        {
            return null;
        }
        var name = S.DockQuickHotspotName;
        if (HotspotText.Why(now.Can) is { } why)
        {
            return $"{why.Title}\n{why.Detail}";
        }
        return now.On switch
        {
            true => $"{S.WidgetSwitchOnTip(name)}\n{(now.MaxClients > 0 ? S.WidgetHotspotDevicesOf(Math.Max(0, now.Clients), now.MaxClients) : S.WidgetHotspotDevicesCount(Math.Max(0, now.Clients)))}",
            false => S.WidgetSwitchOffTip(name),
            _ => name,
        };
    }
}

/// <summary>
/// The battery, in place of the battery on the bar that went (2026-10-10, docs\widgets.md §94), drawn
/// as it drew itself: the level's glyph Windows' battery icon shows — in tenths, charging or in
/// energy saver (<see cref="SystemStatus.Describe"/>) — and the percentage, from what the folder reads
/// (<see cref="FolderIsland.Battery"/>). Across a horizontal bar the two a row, centred on what is
/// shown; down a vertical one the glyph over the figure. Across a bar it is as wide as the charge it
/// shows ("7%" narrower than "100%", §244). A PC without a battery shows a full one on the wall, as
/// it would be, greyed, its tooltip saying there is none (<see cref="Unavailable"/>, §247).
/// It always shows the percentage, as the battery did: it had no setting for it, so the face has
/// nothing to set — but §485 on Windows' own quick settings button, which shows it only where
/// Windows' "Battery percentage" is on (<see cref="FolderFace.NoPercent"/>, WindowsFaces): there the
/// glyph alone, the tooltip still saying it. Painted again in place as the charge moves; its tooltip
/// is the battery's.
/// </summary>
internal sealed unsafe class BatteryFaceDrawing : IFaceDrawing
{

    /// <summary>The widest the percentage ever is: a full battery.</summary>
    internal const string Widest = "100%";

    public string Id => FolderFaces.BatteryId;

    public bool Seconds(FolderFace face) => false;

    /// <summary>
    /// The charge it shows: the folder's; where it is only measured (no folder), a full battery, as
    /// the face's picture in Settings; and on a PC with none to read, the battery line's stand-in
    /// (<see cref="BatteryIsland.UnavailableCharge"/>).
    /// </summary>
    private static SystemStatus.Battery Shown(in FaceContext context) =>
        context.Folder is not { } folder ? new SystemStatus.Battery(100, false, false)
        : folder.Battery is { Percent: >= 0 } charge ? charge
        : BatteryIsland.UnavailableCharge;

    /// <summary>A folder on a PC with no battery to read: greyed, and why.</summary>
    public string? Unavailable(in FaceContext context) =>
        context.Folder is { } folder && folder.Battery is not { Percent: >= 0 }
            ? CellIsland.NotOnThisPc(WidgetInfos.Battery.Title, S.WidgetBatteryNoBattery)
            : null;

    public FaceSize Measure(in FaceContext context)
    {
        var (glyph, px, _, height) = Room(context.Gfx, context.Style);
        // The percentage it shows now, in tabular figures: "7%" is narrower than "100%" (§244); none where it says none.
        var text = Said(context);
        if (context.Vertical)
        {
            return new FaceSize(MathF.Ceiling(glyph + (text.Length > 0 ? height : 0)), Square: false);
        }
        return new FaceSize(MathF.Ceiling(glyph + (text.Length > 0 ? context.Fit.Gap + TextLine.Template(context.Gfx, text, px, Weight.Regular).Width : 0)), Square: false);
    }

    public void Paint(ID2D1DeviceContext* dc, in FaceContext context, D2D_RECT_F box)
    {
        var shown = Shown(context);
        var gfx = context.Gfx;
        var fit = context.Fit;
        var ink = new IslandInk(context.Style.Look);
        var (size, px, _, textHeight) = Room(gfx, context.Style);
        var code = SystemStatus.Describe(shown).Glyph;
        var text = Said(context);
        var width = box.right - box.left;
        var height = box.bottom - box.top;
        dc->SetTextAntialiasMode(D2D1_TEXT_ANTIALIAS_MODE.D2D1_TEXT_ANTIALIAS_MODE_GRAYSCALE);
        ID2D1SolidColorBrush* brush = null;
        TextLine glyph = default;
        TextLine value = default;
        try
        {
            // Full strength, the taskbar's icons' colour, as the status faces beside it (IslandInk.SystemIcon).
            var color = ink.SystemIcon;
            brush = Gfx.Brush(dc, color);
            glyph = TextLine.Lay(gfx, code, size, Weight.Regular, null, gfx.IconFamily);
            if (text.Length > 0)
            {
                value = TextLine.Lay(gfx, text, px, Weight.Regular, context.Vertical ? width : null);
            }
            if (context.Vertical)
            {
                var top = box.top + TextLine.Round((height - size - (text.Length > 0 ? textHeight : 0)) / 2);
                glyph.Draw(dc, brush, color, box.left + ((width - glyph.Width) / 2), top + ((size - glyph.Height) / 2));
                if (text.Length > 0)
                {
                    value.Draw(dc, brush, color, box.left + ((width - value.Width) / 2), top + size);
                }
                return;
            }
            // Centred on the charge being shown, not on the room kept for "100%", as the battery
            // was: the room stops the face moving as the battery falls, and "7%" is centred in it
            // rather than left of centre.
            var drawn = text.Length > 0 ? value.Width : 0;
            var row = size + (drawn > 0 ? fit.Gap + drawn : 0);
            var x = box.left + TextLine.Round((width - row) / 2);
            glyph.Draw(dc, brush, color, x + ((size - glyph.Width) / 2), box.top + ((height - glyph.Height) / 2));
            if (text.Length > 0)
            {
                value.Draw(dc, brush, color, x + size + fit.Gap, box.top + ((height - value.Height) / 2));
            }
        }
        finally
        {
            value.Release();
            glyph.Release();
            ComUtil.Release(ref brush);
        }
    }

    /// <summary>The glyph, the figure and the words: a charge that moved, a charger plugged in, energy saver paint it again.</summary>
    public string Reading(in FaceContext context)
    {
        if (context.Folder is not { } folder)
        {
            return "";
        }
        return $"{SystemStatus.Describe(Shown(context)).Glyph}|{Said(context)}|{Tooltip(context)}";
    }

    /// <summary>What hovering it says: the battery's own words, or that there is none (<see cref="Unavailable"/>).</summary>
    public string? Tooltip(in FaceContext context) =>
        context.Folder is null ? null : Unavailable(context) ?? SystemStatus.Describe(Shown(context)).Tip;

    /// <summary>§485: the percentage the face says: none on a face that shows the glyph alone (<see cref="FolderFace.NoPercent"/>).</summary>
    private static string Said(in FaceContext context) => context.Face.NoPercent ? "" : Text(Shown(context));

    /// <summary>The percentage it shows, held at 100; none where Windows does not know it. Pure.</summary>
    internal static string Text(SystemStatus.Battery charge) =>
        charge.Percent >= 0 ? $"{Math.Min(charge.Percent, 100).ToString(System.Globalization.CultureInfo.InvariantCulture)}%" : "";

    /// <summary>
    /// The glyph's size, the figure's size and the room a full charge's "100%" keeps, on a bar of
    /// <paramref name="style"/>: what the face is measured with and paints with, one answer for both,
    /// as the battery had it. The figure in the main text size, down a vertical bar too narrow for
    /// "100%" the largest step at which it fits the room across (<see cref="VerticalFit"/>); the glyph
    /// about as tall as the figure, on one of the icon font's sizes.
    /// </summary>
    internal static (float Glyph, float Px, float Width, float Height) Room(Gfx gfx, in IslandStyle style)
    {
        var fit = new Fit(style);
        var px = style.Vertical
            ? VerticalFit.Across(gfx, Widest, VerticalFit.Room(fit), Weight.Regular, VerticalFit.Ladder(fit)).Px
            : fit.Primary;
        var (width, height) = TextLine.Template(gfx, Widest, px, Weight.Regular);
        var tall = style.Vertical ? style.Thickness * 0.5f : MathF.Max(height, style.Thickness * 0.4f);
        return (MathF.Min(fit.GlyphFor(tall), fit.Glyph(0.6f)), px, width, height);
    }
}

/// <summary>
/// The power button's glyph, in place of the power widget on the bar that went (2026-10-10,
/// docs\widgets.md §94), at the size it had: a glyph's square, full strength. It never changes, so
/// nothing paints it again; its tooltip names it.
/// </summary>
internal sealed unsafe class PowerFaceDrawing : IFaceDrawing
{
    public string Id => FolderFaces.PowerId;

    public bool Seconds(FolderFace face) => false;

    public FaceSize Measure(in FaceContext context) => new(context.Fit.Glyph(), Square: true);

    public void Paint(ID2D1DeviceContext* dc, in FaceContext context, D2D_RECT_F box)
    {
        dc->SetTextAntialiasMode(D2D1_TEXT_ANTIALIAS_MODE.D2D1_TEXT_ANTIALIAS_MODE_GRAYSCALE);
        PopupPaint.Glyph(dc, context.Gfx, FolderFaces.Power.Glyph, box.right - box.left, new IslandInk(context.Style.Look).Text, box);
    }

    public string Reading(in FaceContext context) => "";

    public string? Tooltip(in FaceContext context) => context.Folder is null ? null : S.WidgetPowerName;
}

/// <summary>
/// The clipboard's glyph, in place of the clipboard history entry on the bar that went (2026-10-10,
/// docs\widgets.md §101), drawn as the entry drew itself: the glyph in the middle of a glyph's
/// square, full strength, with no count — the entry had none, so it never changes and nothing paints
/// it again. Its tooltip is the entry's: its name, to a screen reader (none over the bar, §367). The folder
/// wearing it is what hears the clipboard where Karpo keeps its own history (FolderIsland).
/// </summary>
internal sealed unsafe class ClipboardFaceDrawing : IFaceDrawing
{
    public string Id => FolderFaces.ClipboardId;

    public bool Seconds(FolderFace face) => false;

    public FaceSize Measure(in FaceContext context) => new(context.Fit.Glyph(), Square: true);

    public void Paint(ID2D1DeviceContext* dc, in FaceContext context, D2D_RECT_F box)
    {
        dc->SetTextAntialiasMode(D2D1_TEXT_ANTIALIAS_MODE.D2D1_TEXT_ANTIALIAS_MODE_GRAYSCALE);
        PopupPaint.Glyph(dc, context.Gfx, FolderFaces.Clipboard.Glyph, box.right - box.left, new IslandInk(context.Style.Look).Text, box);
    }

    public string Reading(in FaceContext context) => "";

    public string? Tooltip(in FaceContext context) => context.Folder is null ? null : WidgetInfos.Clipboard.Title;
}

/// <summary>
/// The contact's glyph, in place of the contacts entry on the bar that went (2026-10-11,
/// docs\widgets.md §102), drawn as the entry drew itself: the glyph in the middle of a glyph's
/// square, with no count — the entry had none, so it never changes and nothing paints it again. Full
/// strength: the entry dimmed itself only once a read had failed, and the face reads nothing (the
/// folder's line says why). Its tooltip is the entry's: its name, to a screen reader (none over the bar, §367).
/// </summary>
internal sealed unsafe class ContactsFaceDrawing : IFaceDrawing
{
    public string Id => FolderFaces.ContactsId;

    public bool Seconds(FolderFace face) => false;

    public FaceSize Measure(in FaceContext context) => new(context.Fit.Glyph(), Square: true);

    public void Paint(ID2D1DeviceContext* dc, in FaceContext context, D2D_RECT_F box)
    {
        dc->SetTextAntialiasMode(D2D1_TEXT_ANTIALIAS_MODE.D2D1_TEXT_ANTIALIAS_MODE_GRAYSCALE);
        PopupPaint.Glyph(dc, context.Gfx, FolderFaces.Contacts.Glyph, box.right - box.left, new IslandInk(context.Style.Look).Text, box);
    }

    public string Reading(in FaceContext context) => "";

    public string? Tooltip(in FaceContext context) => context.Folder is null ? null : WidgetInfos.Contacts.Title;
}

/// <summary>
/// §642: Windows search's face: Windows 11's search box as the taskbar draws it (SearchPill) — the magnifier and "Search", as
/// long as they are (§510) — along a top or bottom bar tall enough for the box, the magnifier alone in a glyph's square down a
/// side or on a thin bar. It never changes. Its tooltip: Windows search, to a screen reader.
/// </summary>
internal sealed unsafe class WinSearchFaceDrawing : IFaceDrawing
{
    public string Id => FolderFaces.WinSearchId;

    public bool Seconds(FolderFace face) => false;

    /// <summary>Whether the bar shows the box: as Windows' button shows its own (SearchPill.Fits).</summary>
    private static bool Boxed(in FaceContext context) => SearchPill.Fits(context.Vertical, context.Style.Thickness, context.Style.Scale);

    public FaceSize Measure(in FaceContext context) =>
        Boxed(context)
            ? new(SearchPill.Length(context.Style.Thickness, SearchPill.WordsPx(context.Gfx, context.Style.Thickness, context.Style.Look)), Square: false)
            : new(context.Fit.Glyph(), Square: true);

    public void Paint(ID2D1DeviceContext* dc, in FaceContext context, D2D_RECT_F box)
    {
        dc->SetTextAntialiasMode(D2D1_TEXT_ANTIALIAS_MODE.D2D1_TEXT_ANTIALIAS_MODE_GRAYSCALE);
        if (Boxed(context))
        {
            var cell = context.Style.Thickness;
            // Across the face's whole length, which is the box's own (SearchPill.Length); centred across the bar as Windows' box.
            var pill = SearchPill.In(box, cell) with { left = box.left, right = box.right };
            SearchPill.Paint(dc, context.Gfx, pill, cell, MathF.Max(1, MathF.Round(context.Style.Scale)), context.Style.Look, SearchPill.WindowsPlaceholder);
            return;
        }
        PopupPaint.Glyph(dc, context.Gfx, SearchPill.Glyph, box.right - box.left, new IslandInk(context.Style.Look).Text, box);
    }

    public string Reading(in FaceContext context) => "";

    public string? Tooltip(in FaceContext context) => context.Folder is null ? null : WidgetInfos.WinSearch.Title;
}

/// <summary>
/// The desktop you are on, in place of the desktop switcher on the bar that went (2026-10-11,
/// docs\widgets.md §116), drawn as it drew itself: the switcher's glyph and the
/// desktop's name after it, as long as the name, up to the room of Windows' "Desktop 88"
/// (<see cref="Switchers.NameRoom"/>, §244); a longer name
/// is cut, and is whole in the tooltip with where it stands among the others. Down a bar on its side
/// the glyph alone, in a square. Read from what the dock last read (<see cref="DesktopState"/>), and
/// painted again as that changes: a switch or a rename builds the bar again, and its reading is the name.
/// </summary>
internal sealed unsafe class DesktopsFaceDrawing : IFaceDrawing
{
    public string Id => FolderFaces.DesktopsId;

    public bool Seconds(FolderFace face) => false;

    public FaceSize Measure(in FaceContext context)
    {
        var fit = context.Fit;
        var glyph = fit.Glyph();
        return context.Vertical
            ? new(glyph, Square: true)
            : new(glyph + fit.Gap + FaceDrawings.NameLength(context, DesktopState.Name), Square: false);
    }

    public void Paint(ID2D1DeviceContext* dc, in FaceContext context, D2D_RECT_F box)
    {
        var fit = context.Fit;
        var glyph = fit.Glyph();
        var ink = new IslandInk(context.Style.Look).Text;
        dc->SetTextAntialiasMode(D2D1_TEXT_ANTIALIAS_MODE.D2D1_TEXT_ANTIALIAS_MODE_GRAYSCALE);
        var middle = (box.top + box.bottom) / 2;
        if (context.Vertical)
        {
            PopupPaint.Glyph(dc, context.Gfx, FolderFaces.Desktops.Glyph, glyph, ink, PopupPaint.Rect(box.left, middle - (glyph / 2), glyph, glyph));
            return;
        }
        FaceDrawings.GlyphAndName(dc, context, box, FolderFaces.Desktops.Glyph, DesktopState.Name, ink);
    }

    /// <summary>The name and where it stands: a switch or a rename paints it again.</summary>
    public string Reading(in FaceContext context) => $"{DesktopState.Current}:{DesktopState.Name}";

    /// <summary>The switcher's: the whole name, and where it stands among the desktops.</summary>
    public string? Tooltip(in FaceContext context) =>
        context.Folder is null ? null : Switchers.DesktopTooltip(DesktopState.Name, DesktopState.Current, DesktopState.Count);
}

/// <summary>
/// The layout this bar shows, in place of the layout switcher on the bar that went (2026-10-11,
/// docs\widgets.md §127), drawn as it drew itself: the layout's own glyph and its name after it, as
/// long as the name, up to the room of ten characters (<see cref="Switchers.NameRoom"/>, §244); a
/// longer name is cut and is whole in the tooltip. Dimmed where there is no other layout to switch
/// to, as the switcher was. Down a bar on its side the glyph alone, in a square. Read from the
/// folder (<see cref="FolderIsland.Shown"/>), which a switch or a rename builds again.
/// </summary>
internal sealed unsafe class LayoutsFaceDrawing : IFaceDrawing
{
    /// <summary>What it shows where it is only measured (no folder): the default layout, with another to switch to.</summary>
    internal static LayoutShown Unread => new(TaskbarLayouts.DefaultName, FolderFaces.Layouts.Glyph, 2);

    public string Id => FolderFaces.LayoutsId;

    public bool Seconds(FolderFace face) => false;

    public FaceSize Measure(in FaceContext context)
    {
        var fit = context.Fit;
        var glyph = fit.Glyph();
        return context.Vertical
            ? new(glyph, Square: true)
            : new(glyph + fit.Gap + FaceDrawings.NameLength(context, (context.Folder?.Shown ?? Unread).Name), Square: false);
    }

    public void Paint(ID2D1DeviceContext* dc, in FaceContext context, D2D_RECT_F box)
    {
        var fit = context.Fit;
        var glyph = fit.Glyph();
        var shown = context.Folder?.Shown ?? Unread;
        var ink = new IslandInk(context.Style.Look);
        var colour = shown.Choosable ? ink.Text : ink.Disabled;
        dc->SetTextAntialiasMode(D2D1_TEXT_ANTIALIAS_MODE.D2D1_TEXT_ANTIALIAS_MODE_GRAYSCALE);
        var middle = (box.top + box.bottom) / 2;
        if (context.Vertical)
        {
            PopupPaint.Glyph(dc, context.Gfx, shown.Glyph, glyph, colour, PopupPaint.Rect(box.left, middle - (glyph / 2), glyph, glyph));
            return;
        }
        FaceDrawings.GlyphAndName(dc, context, box, shown.Glyph, shown.Name, colour);
    }

    /// <summary>The name, the glyph and whether there is another: a switch, a rename or a new layout paints it again.</summary>
    public string Reading(in FaceContext context) =>
        context.Folder?.Shown is { } shown ? $"{shown.Glyph}|{shown.Count}|{shown.Name}" : "";

    /// <summary>The switcher's: the whole name, and that a click shows another, or that there is none.</summary>
    public string? Tooltip(in FaceContext context) =>
        context.Folder?.Shown is { } shown ? Switchers.LayoutTooltip(shown.Name, shown.Count) : null;
}

/// <summary>
/// A switch's glyph, in place of its entry on the bar that went (2026-10-11, docs\widgets.md §125):
/// VPN's shield, drawn as the status glyphs beside it are (<see cref="SystemGlyph.Paint"/>), full while
/// the switch is on — a connection up — and dimmed while it is off, before Windows has answered and
/// where this PC has none, as the Bluetooth and hotspot faces say on and off. Not the entry's accent
/// tile: the face is a status glyph like its neighbours. Read from the switch the folder holds for it
/// (<see cref="FolderIsland.FaceSwitch"/>), which says when it changed, and painted again in place
/// then. One glyph's square. Its tooltip is the entry's words: on as what, or off.
/// </summary>
internal sealed unsafe class SwitchFaceDrawing(string id) : IFaceDrawing
{
    public string Id => id;

    public bool Seconds(FolderFace face) => false;

    public FaceSize Measure(in FaceContext context) => new(context.Fit.Glyph(), Square: true);

    /// <summary>Where it is only measured (no folder), the glyph undimmed.</summary>
    public void Paint(ID2D1DeviceContext* dc, in FaceContext context, D2D_RECT_F box) =>
        SystemGlyph.Paint(dc, context.Gfx, context.Style, FolderFaces.Of(id).Glyph, null, context.Folder is { } folder && Dim(folder.FaceSwitch), null, box);

    /// <summary>On or off and the words: a connection made or ended paints it again.</summary>
    public string Reading(in FaceContext context) =>
        context.Folder?.FaceSwitch is { } on ? $"{Dim(on)}|{Tooltip(context)}" : "";

    public string? Tooltip(in FaceContext context) =>
        context.Folder?.FaceSwitch is { } on ? SwitchIsland.Say(on.Name, on.Available, on.On, on.Note, on.Detail) : null;

    /// <summary>Dimmed unless it is on: off, not yet known, or not on this PC.</summary>
    internal static bool Dim(Karpo.Dock.QuickSettings.Services.IQuickSwitch? on) => on is not { Available: true, On: true };
}

/// <summary>
/// The mail, in place of the mail entry on the bar that went (2026-10-10, docs\widgets.md §95), drawn
/// as it drew itself (<see cref="GlyphBadge"/>): the envelope, and how many are unread in an accent
/// pill on its corner ("99+" past 99), from the mail store the folder reads
/// (<see cref="FolderIsland.Mail"/>). Dimmed, with no count, while an inbox cannot be read; the
/// stale mark small on its lower corner while what it shows could not be refreshed (StaleMark.cs);
/// the envelope alone with no account, as the entry showed it then. A glyph's square and its widest count's room, kept whatever it shows
/// (§276), painted again in place as the mail changes. Its first paint is the look that starts
/// the first read (docs\widgets.md §11). Its tooltip is the entry's, to a screen reader (none over the bar, §367):
/// the count or why, and from when where it could not be refreshed.
/// </summary>
internal sealed unsafe class MailFaceDrawing : IFaceDrawing
{
    public string Id => FolderFaces.MailId;

    public bool Seconds(FolderFace face) => false;

    /// <summary>A glyph's square, and the room past it its widest count ("99+") and the stale mark stand in, kept whatever it shows (§276).</summary>
    public FaceSize Measure(in FaceContext context) => new(context.Fit.Glyph(), Square: true, GlyphBadge.Trail(context.Gfx, context.Style, stale: true));

    /// <summary>Where it is only measured (no folder), the envelope with no count, as the face's picture in Settings.</summary>
    public void Paint(ID2D1DeviceContext* dc, in FaceContext context, D2D_RECT_F box)
    {
        var store = context.Folder?.Mail;
        // The look: the first paint starts the first read, and later ones cost nothing.
        store?.Refresh();
        var (dim, badge, stale) = Look(store, context.Folder?.MailShows);
        GlyphBadge.Paint(dc, context.Gfx, context.Style, FolderFaces.Mail.Glyph, badge, !dim, box, stale, room: context.Room);
    }

    /// <summary>The count, the dim, the mark and the words: mail arriving, read elsewhere or not reachable paint it again.</summary>
    public string Reading(in FaceContext context)
    {
        if (context.Folder?.Mail is not { } store)
        {
            return "";
        }
        var (dim, badge, stale) = Look(store, context.Folder!.MailShows);
        return $"{dim}|{badge}|{stale}|{Tooltip(context)}";
    }

    public string? Tooltip(in FaceContext context) =>
        context.Folder?.Mail is { } store ? Pim.MailIsland.Tip(store, Clock.Real(), store.View(context.Folder.MailShows).Unread) : null;

    /// <summary>
    /// What the face draws for <paramref name="store"/>: whether the envelope is dimmed, the count on
    /// its corner, and whether the stale mark stands on it — as the entry drew them: dimmed with no
    /// count while an inbox cannot be read; the envelope plain with no account, which the entry's
    /// "Set up" showed as its glyph alone; plain with no store (Settings' picture). §639: the count is of what
    /// <paramref name="shows"/> picks — its folder's mail line's "Show from" (<see cref="FolderIsland.MailShows"/>) —,
    /// every inbox's where it picks nothing. Pure: the tests ask it.
    /// </summary>
    internal static (bool Dim, string? Badge, bool Stale) Look(Pim.MailStore? store, SourceFilter? shows = null)
    {
        if (store is null || (!store.HasAccount && !store.Loading))
        {
            return (false, null, false);
        }
        var available = store.Problem is null;
        return (!available, available ? GlyphBadge.Text(store.View(shows ?? SourceFilter.All).Unread) : null, store.Freshness.Stale);
    }
}

/// <summary>
/// What is playing, in place of the now-playing entry on the bar that went (2026-10-10,
/// docs\widgets.md §96), drawn as it drew itself (<see cref="MediaIsland.PaintFace"/>): the art — the
/// note where there is none, a pause mark on it while the track is paused — and the title with the
/// artist, over each other on a bar thick enough for two lines and on one line where it is not;
/// turned under the art down a bar on its side; "Nothing playing", quietly, while nothing plays.
/// Read from the folder's now-playing line, so the face follows the session the line follows, or
/// from a widget of the folder's own where it holds none (<see cref="FolderIsland.NowPlaying"/>).
/// <para>
/// Its length is the width it is set to, one of three (<see cref="NowPlayingFace.Width"/>,
/// <see cref="MediaIsland.FaceLength"/>, §503), whatever plays — not the words as they read now
/// (§244 for the other faces): a long title is cut past its room and says itself whole in the bar's
/// tooltip (CutWords). It is painted again in place as the session's news arrives, never laid out
/// again for it; never on a tick: it shows no time. Its
/// tooltip is the entry's: the track and where it plays, to a screen reader (none over the bar, §367).
/// </para>
/// </summary>
internal sealed unsafe class NowPlayingFaceDrawing : IFaceDrawing
{
    public string Id => FolderFaces.NowPlayingId;

    public bool Seconds(FolderFace face) => false;

    /// <summary>Its width across a bar; down a bar on its side the art alone, a square as a glyph's is (<see cref="MediaIsland.FaceLength"/>).</summary>
    public FaceSize Measure(in FaceContext context) =>
        new(MediaIsland.FaceLength(context.Style, NowPlayingFace.WidthOf(context.Face)), Square: context.Vertical);

    /// <summary>Where it is only measured (no folder), nothing: the face is the track, which a picture has none of.</summary>
    public void Paint(ID2D1DeviceContext* dc, in FaceContext context, D2D_RECT_F box) =>
        context.Folder?.NowPlaying?.PaintFace(dc, context.Style, box, NowPlayingFace.ShowsArt(context.Face));

    /// <summary>The track, its art, whether it plays and the app: a new track, a pause or a pick paint it again.</summary>
    public string Reading(in FaceContext context) => context.Folder?.NowPlaying?.FaceReading ?? "";

    public string? Tooltip(in FaceContext context) => context.Folder?.NowPlaying?.FaceTip;

    /// <summary>Nothing plays, where it is set to "Hide it while nothing plays" (§234, §238).</summary>
    public bool NothingToShow(FolderFace face, FolderIsland folder) =>
        NowPlayingFace.HidesIdle(face) && folder.NowPlaying is { Idle: true };
}

/// <summary>
/// A performance reading's figure, in place of its entry on the bar that went (2026-10-10,
/// docs\widgets.md §97), drawn as it drew itself (<see cref="PerformanceIsland.PaintFace"/>): the
/// reading's glyph and the figure it reads now, as wide as that figure and no narrower than two
/// ("88%") or a rate's room ("8.8M", <see cref="PerformanceIsland.FaceLength"/>, §244), side by side
/// across a bar and the glyph over the figure down one; in its graph style (§295) with the room of a
/// graph between them and its history behind, at its pace, as its line draws it. Read from the source the
/// folder holds for it (<see cref="FolderIsland.Performance"/>), the one every widget of that reading
/// and device draws, and painted again in place at its pace (the folder's tick); "100%" lays the bar
/// out again. Its tooltip is the entry's: the reading, to a screen reader (none over the bar, §367).
/// </summary>
internal sealed unsafe class PerformanceFaceDrawing(string id, Metric metric) : IFaceDrawing
{
    /// <summary>What a graph draws before anything has been read, or where only the face's settings are (Settings' picture).</summary>
    private static readonly History Nothing = new(1);

    public string Id => id;

    public bool Seconds(FolderFace face) => false;

    public FaceSize Measure(in FaceContext context)
    {
        var (text, known) = Said(context);
        return new(PerformanceIsland.FaceLength(context.Gfx, metric, context.Style, text, known, PerformanceFace.Options(context.Face).ShowGraph), Square: false);
    }

    /// <summary>Where it is only measured (no folder), the dash a reading not taken yet shows, as the face's picture in Settings.</summary>
    public void Paint(ID2D1DeviceContext* dc, in FaceContext context, D2D_RECT_F box)
    {
        var (text, known) = Said(context);
        PerformanceIsland.PaintFace(dc, context.Gfx, metric, context.Style, box, text, known, Graph(context));
    }

    /// <summary>The figure it shows now and whether it is known; the dash before a reading.</summary>
    private static (string Text, bool Known) Said(in FaceContext context) =>
        context.Folder?.PerformanceOf(context.Face) is { } source ? PerformanceIsland.ReadingOf(source) : ("–", false);

    /// <summary>In its graph style, the history it draws a step every pace it is set to, as its line would; null in its figure's.</summary>
    private static Series? Graph(in FaceContext context)
    {
        var options = PerformanceFace.Options(context.Face);
        if (!options.ShowGraph)
        {
            return null;
        }
        return context.Folder?.PerformanceOf(context.Face) is { } source ? source.Paced(options.Seconds) : new Series(Nothing, 1, 0);
    }

    /// <summary>The figure, and in its graph style the steps the graph has drawn: a new one paints it again.</summary>
    public string Reading(in FaceContext context)
    {
        if (context.Folder?.PerformanceOf(context.Face) is not { } source)
        {
            return "";
        }
        var text = PerformanceIsland.ReadingOf(source).Text;
        var options = PerformanceFace.Options(context.Face);
        return options.ShowGraph ? $"{text}|{source.History.Total / options.Seconds}" : text;
    }

    public string? Tooltip(in FaceContext context) =>
        context.Folder?.PerformanceOf(context.Face) is { } source ? PerformanceIsland.Tip(source) : null;
}

/// <summary>
/// A feed's newest headline, in place of the RSS entry on the bar that went (2026-10-10,
/// docs\widgets.md §98), drawn as it drew itself (<see cref="Rss.RssIsland.PaintFace"/>): the news
/// glyph with the accent's dot while something new has come, and where the face is set to say it the
/// newest headline beside it, as long as it reads up to a room of its own, cut with "…" past it
/// (§244); the glyph alone down a bar on its
/// side. It shows the folder's first feed line (<see cref="FolderIsland.Feed"/>); opening the folder
/// is seeing what is new, and the dot goes. Its tooltip is the entry's, to a screen reader (none over the bar, §367): the feed
/// and its newest headline.
/// </summary>
internal sealed unsafe class NewsFaceDrawing : IFaceDrawing
{
    public string Id => FolderFaces.NewsId;

    public bool Seconds(FolderFace face) => false;

    public FaceSize Measure(in FaceContext context) =>
        Rss.RssIsland.FaceLength(context.Gfx, context.Style, NewsFace.ShowsHeadline(context.Face), Rss.RssIsland.FaceSaid(context.Folder?.Feed).Said);

    public void Paint(ID2D1DeviceContext* dc, in FaceContext context, D2D_RECT_F box) =>
        Rss.RssIsland.PaintFace(dc, context.Gfx, context.Style, box, context.Folder?.Feed, NewsFace.ShowsHeadline(context.Face));

    /// <summary>The newest headline and the dot: a new read paints it again.</summary>
    public string Reading(in FaceContext context) => Rss.RssIsland.FaceReading(context.Folder?.Feed);

    public string? Tooltip(in FaceContext context) => context.Folder?.Feed?.Tooltip(0);
}

/// <summary>
/// A drive, in place of the drive and network drive entries on the bar that went (2026-10-10,
/// docs\widgets.md §99), drawn as they drew themselves (<see cref="DriveIsland.PaintFace"/>): the
/// drive's own picture, its name — or its letter alone, where the face is set so — and the capacity
/// bar filling the room after it; faint with the warning badge while the drive is not there; a
/// network drive that is not connected Explorer's drive with the red X, its name and no bar; "Set
/// up" with no drive chosen. It shows the folder's first drive line of its kind
/// (<see cref="FolderIsland.DriveLine"/>). As long as what it shows, up to two cells (<see cref="DriveIsland.FaceLength"/>, §244),
/// painted again in place as the drive is read. Its tooltip is the entry's, to a screen reader (none over the bar, §367): the
/// drive and how full it is.
/// </summary>
internal sealed unsafe class DriveFaceDrawing(string id, WidgetInfo kind) : IFaceDrawing
{
    public string Id => id;

    public bool Seconds(FolderFace face) => false;

    public FaceSize Measure(in FaceContext context) =>
        new(DriveIsland.FaceLength(context.Style, context.Gfx, context.Folder?.DriveLineOf(kind), DriveFace.ShowsName(context.Face)), Square: false);

    /// <summary>Where it is only measured (no folder), its "Set up", as the face's picture in Settings.</summary>
    public void Paint(ID2D1DeviceContext* dc, in FaceContext context, D2D_RECT_F box) =>
        DriveIsland.PaintFace(dc, context.Gfx, context.Style, box, kind, context.Folder?.DriveLineOf(kind), DriveFace.ShowsName(context.Face));

    /// <summary>The drive, whether it is there and how full: a change paints it again.</summary>
    public string Reading(in FaceContext context) => context.Folder?.DriveLineOf(kind) is { } line ? line.Look.Reading : "";

    public string? Tooltip(in FaceContext context) => context.Folder?.DriveLineOf(kind)?.Tooltip(0);
}

/// <summary>
/// The bell, in place of the bell on the bar that went (2026-10-10, docs\widgets.md §100), drawn as
/// it drew itself (<see cref="GlyphBadge"/>): the bell, and how many notifications arrived since a
/// folder showing them was last opened in an accent pill on its corner ("99+" past 99), from the
/// store the folder uses (<see cref="FolderIsland.Notifications"/>) — none where the face is set not
/// to show it. Windows 11's bell with "zZ" while do not disturb is on, and the pill on its lower
/// corner then, where the bell has only the end of its rim; dimmed with no count while the
/// notifications cannot be read. A glyph's square and its widest count's room, kept whatever it shows (§276), painted again in place as they
/// change; its first paint is the look that starts following do not disturb (docs\widgets.md §11).
/// Its tooltip is the bell's: how many are new, over it on the bar as over Windows' (§382) and to a screen reader.
/// </summary>
internal sealed unsafe class NotificationsFaceDrawing : IFaceDrawing
{
    /// <summary>Segoe Fluent Icons: Ringer, a bell.</summary>
    private const string Bell = "\uEA8F";

    public string Id => FolderFaces.NotificationsId;

    public bool Seconds(FolderFace face) => false;

    /// <summary>A glyph's square, and the room past it its widest count ("99+") stands in where it is set to show one, kept whatever it shows (§276).</summary>
    public FaceSize Measure(in FaceContext context) =>
        new(context.Fit.Glyph(), Square: true, NotificationsFace.ShowsCount(context.Face) ? GlyphBadge.Trail(context.Gfx, context.Style) : 0);

    /// <summary>Where it is only measured (no folder), the bell with no count, as the face's picture in Settings.</summary>
    public void Paint(ID2D1DeviceContext* dc, in FaceContext context, D2D_RECT_F box)
    {
        var quiet = context.Folder is { ShowsNotifications: true } folder ? folder.LookAtQuiet().On == true : false;
        var (dim, badge) = Look(context.Folder?.Notifications, NotificationsFace.ShowsCount(context.Face));
        GlyphBadge.Paint(dc, context.Gfx, context.Style, quiet ? QuickSettings.Services.DoNotDisturb.Glyph : Bell, badge, !dim, box,
            rise: 0.35f, low: quiet, room: context.Room);
    }

    /// <summary>The count, the dim and the "zZ": a notification arriving, the list opened, do not disturb switched paint it again.</summary>
    public string Reading(in FaceContext context)
    {
        if (context.Folder?.Notifications is not { } store)
        {
            return "";
        }
        var (dim, badge) = Look(store, NotificationsFace.ShowsCount(context.Face));
        return $"{dim}|{badge}|{context.Folder.LookAtQuiet().On == true}";
    }

    public string? Tooltip(in FaceContext context) =>
        context.Folder?.Notifications is { } store ? Tip(store) : null;

    /// <summary>What the bell says: how many are new, its name with none, or why none can be read.</summary>
    internal static string Tip(NotificationStore store) =>
        !store.Available ? S.WidgetNotificationsUnavailable
        : store.Unseen == 0 ? S.WidgetNotificationsName
        : S.WidgetNotificationsNew(store.Unseen);

    /// <summary>
    /// What the face draws for <paramref name="store"/>: whether the bell is dimmed, and the count on its
    /// corner — as the bell drew them: dimmed with no count while the notifications cannot be read; no
    /// count where none are new or the face is set not to show it; plain with no store (Settings'
    /// picture). Pure: the tests ask it.
    /// </summary>
    internal static (bool Dim, string? Badge) Look(NotificationStore? store, bool count)
    {
        if (store is null)
        {
            return (false, null);
        }
        return !store.Available ? (true, null) : (false, count ? GlyphBadge.Text(store.Unseen) : null);
    }
}

/// <summary>The ink a timer, focus or stopwatch face says its figures in.</summary>
internal enum TimeFaceInk
{
    /// <summary>At rest and running: the text's.</summary>
    Text,

    /// <summary>Paused: the quieter ink.</summary>
    Paused,

    /// <summary>A countdown that ran out: the accent.</summary>
    Over,
}

/// <summary>
/// What a timer, focus or stopwatch face says now (<see cref="CountdownFaceDrawing.Read"/>): its
/// figures, their ink, and the name its line was given, which stands before them where there is room.
/// </summary>
internal readonly record struct TimeFaceReading(string Text, TimeFaceInk Ink, string Name);

/// <summary>
/// The timer, the focus session and the stopwatch: the glyph and what the line in the folder has
/// left or has counted, as their entries on the bar drew it until they went (2026-10-10,
/// docs\widgets.md §92), from that line's own countdown or stopwatch in the dock's store
/// (<see cref="FolderIsland.TimeLine"/>), never one of the face's own. A countdown at rest says the
/// length it is set to ("5m"); running, what is left in five figures ("04:59", "01:05" from an hour
/// up: Countdown.Cell); paused, the same in the quieter ink; over, "00:00" in the accent — or its
/// name in their place, where it has one and the face has no room for both. A stopwatch says what it
/// has counted in whole seconds (StopwatchText.Cell), never its tenths, quieter while it is paused.
/// Across a bar it is as wide as what it says now (§244) — its figures tabular, so a running one
/// counts in one width — and the dock's second paints it again in place while one runs
/// (<see cref="FolderIsland.FaceTick"/>), laying the bar out again as it starts or stops. Across a
/// horizontal bar the glyph and the figures side by side, a name the line was given between them;
/// down a vertical one the glyph over the figures.
/// </summary>
internal sealed unsafe class CountdownFaceDrawing(string id) : IFaceDrawing
{
    public string Id => id;

    private bool Counts => id != FolderFaces.StopwatchId;

    private CountdownKind Kind => id == FolderFaces.FocusId ? CountdownKind.Focus : CountdownKind.Timer;

    private string Glyph => FolderFaces.Of(id).Glyph;

    public bool Seconds(FolderFace face) => false;

    public FaceSize Measure(in FaceContext context)
    {
        var fit = context.Fit;
        if (context.Vertical)
        {
            // The rows it says now (§275): a running one's two figures over each other, the length
            // set at rest one row.
            var side = Side(context.Gfx, context.Style);
            return new FaceSize(side.Glyph + (SideRows(context.Gfx, ReadOf(context), side.Px, VerticalFit.Room(fit)).Length * side.Height), Square: false);
        }
        // What it says now, in tabular figures (§244): "5m" at rest is narrower than "04:59" running,
        // and a running one counts down in one width.
        var (glyph, px, room, _) = Room(context.Gfx, context.Style, Counts);
        var (beside, text) = Says(context.Gfx, ReadOf(context), vertical: false, px, room);
        var width = MathF.Min(room, TextLine.Template(context.Gfx, text, px, Weight.Regular).Width);
        if (beside is not null)
        {
            width += fit.Gap + TextLine.Template(context.Gfx, beside, px, Weight.Regular).Width;
        }
        return new FaceSize(MathF.Ceiling(glyph + fit.Gap + width), Square: false);
    }

    public void Paint(ID2D1DeviceContext* dc, in FaceContext context, D2D_RECT_F box)
    {
        var fit = context.Fit;
        var gfx = context.Gfx;
        var ink = new IslandInk(context.Style.Look);
        var reading = ReadOf(context);
        var color = reading.Ink switch
        {
            TimeFaceInk.Paused => ink.Secondary,
            TimeFaceInk.Over => ink.Accent,
            _ => ink.Text,
        };
        dc->SetTextAntialiasMode(D2D1_TEXT_ANTIALIAS_MODE.D2D1_TEXT_ANTIALIAS_MODE_GRAYSCALE);
        ID2D1SolidColorBrush* brush = null;
        TextLine glyph = default;
        TextLine line = default;
        TextLine called = default;
        try
        {
            brush = Gfx.Brush(dc, ink.Text);
            var across = box.right - box.left;
            var along = box.bottom - box.top;
            if (context.Vertical)
            {
                // The glyph over the figures, never turned: a running one's two figures over each
                // other, as the clock's hours over its minutes; the length set at rest ("25m") on a row.
                var (side, size, tall) = Side(gfx, context.Style);
                var rows = SideRows(gfx, reading, size, VerticalFit.Room(fit));
                var top = TextLine.Round(box.top + ((along - side - (rows.Length * tall)) / 2));
                glyph = TextLine.Lay(gfx, Glyph, side, Weight.Regular, null, gfx.IconFamily);
                glyph.Draw(dc, brush, ink.Text, box.left + ((across - glyph.Width) / 2), top + ((side - glyph.Height) / 2));
                top += side;
                foreach (var figures in rows)
                {
                    FaceDrawings.Line(dc, gfx, figures, size, color, new D2D_RECT_F(box.left, top, box.right, top + tall), tall);
                    top += tall;
                }
                return;
            }
            var (glyphPx, px, textWidth, textHeight) = Room(gfx, context.Style, Counts);
            var (beside, text) = Says(gfx, reading, vertical: false, px, textWidth);
            line = TextLine.Lay(gfx, text, px, Weight.Regular, MathF.Min(textWidth, across));
            var nameWidth = beside is null ? 0 : TextLine.Template(gfx, beside, px, Weight.Regular).Width;
            // The row as it reads now in the middle of the room the widest reading keeps (2026-10-11,
            // the faces of text centred): "5m" at rest stands in the middle, not at the start. A
            // running one says five figures, all as wide as the widest, so nothing moves as it counts.
            var shown = MathF.Min(line.Width, textWidth);
            var row = GlyphAndText.Row(across, fit, glyphPx, beside is not null ? nameWidth + fit.Gap + shown : shown);
            if (row.WithGlyph)
            {
                glyph = TextLine.Lay(gfx, Glyph, glyphPx, Weight.Regular, null, gfx.IconFamily);
                glyph.Draw(dc, brush, ink.Text, box.left + row.GlyphAt + ((glyphPx - glyph.Width) / 2), box.top + ((along - glyph.Height) / 2));
            }
            var x = box.left + row.TextAt;
            if (beside is not null)
            {
                called = TextLine.Lay(gfx, beside, px, Weight.Regular, null);
                called.Draw(dc, brush, ink.Text, x, box.top + ((along - textHeight) / 2));
                x += nameWidth + fit.Gap;
            }
            line.Draw(dc, brush, color, x, box.top + ((along - textHeight) / 2));
        }
        finally
        {
            called.Release();
            line.Release();
            glyph.Release();
            ComUtil.Release(ref brush);
        }
    }

    /// <summary>What it says and in which ink: each second of a running one, and a pause, paint it again.</summary>
    public string Reading(in FaceContext context)
    {
        var reading = ReadOf(context);
        return $"{reading.Text}|{reading.Ink}|{reading.Name}";
    }

    /// <summary>
    /// The line's name, or its kind's, over what it is doing: the length it is set to at rest, what
    /// is left or has been counted, and whether it is paused or over.
    /// </summary>
    public string? Tooltip(in FaceContext context)
    {
        var line = FolderIsland.FaceLine(context.Options, context.Face);
        var store = context.Folder?.Countdowns;
        if (line is null)
        {
            return null;
        }
        if (!Counts)
        {
            var state = store?.Stopwatches.Of(line.Id) ?? StopwatchState.None;
            var counted = StopwatchText.Tenths(state.Elapsed(StopwatchClock.Now()), Panels.Nls.DecimalSeparator());
            return state.Phase == StopwatchPhase.Paused ? $"{S.DockStopwatchTitle}\n{S.DockClockPaused}: {counted}" : $"{S.DockStopwatchTitle}\n{counted}";
        }
        var title = NameOf(line) is { Length: > 0 } name ? name : CountdownCard.Title(Kind);
        var countdown = store?.Of(line.Id) ?? Countdown.None;
        var now = Clock.Now();
        return countdown.Phase switch
        {
            CountdownPhase.Running => $"{title}\n{Countdown.Span(countdown.Remaining(now))}",
            CountdownPhase.Paused => $"{title}\n{countdown.Says(now)}: {Countdown.Span(countdown.Remaining(now))}",
            CountdownPhase.Done => $"{title}\n{countdown.Says(now)}",
            _ => $"{title}\n{Countdown.Words(CountdownTile.LengthOf(line, Kind))}",
        };
    }

    private TimeFaceReading ReadOf(in FaceContext context) =>
        Read(id, FolderIsland.FaceLine(context.Options, context.Face), context.Folder?.Countdowns, Clock.Now(), StopwatchClock.Now());

    /// <summary>
    /// What the face <paramref name="face"/> says for <paramref name="line"/>, the timer, focus
    /// session or stopwatch in its folder, as <paramref name="store"/> has it at <paramref name="now"/>
    /// (a countdown's wall clock) and <paramref name="counted"/> (a stopwatch's). With no line or no
    /// store, what one at rest says. Pure: the tests ask it.
    /// </summary>
    internal static TimeFaceReading Read(string face, CellItem? line, Countdowns? store, DateTime now, TimeSpan counted)
    {
        if (face == FolderFaces.StopwatchId)
        {
            var state = line is null ? StopwatchState.None : store?.Stopwatches.Of(line.Id) ?? StopwatchState.None;
            return new TimeFaceReading(StopwatchText.Cell(state.Elapsed(counted)), state.Phase == StopwatchPhase.Paused ? TimeFaceInk.Paused : TimeFaceInk.Text, "");
        }
        var kind = face == FolderFaces.FocusId ? CountdownKind.Focus : CountdownKind.Timer;
        var countdown = line is null ? Countdown.None : store?.Of(line.Id) ?? Countdown.None;
        var text = countdown.Idle
            ? S.WidgetCountdownBarMinutes((int)Countdown.Clamp(line is null ? CountdownSource.Minutes(null, kind == CountdownKind.Focus ? CountdownOptions.FocusMinutes : CountdownOptions.TimerMinutes) : CountdownTile.LengthOf(line, kind)).TotalMinutes)
            : countdown.Cell(now);
        var ink = countdown.Phase switch
        {
            CountdownPhase.Paused => TimeFaceInk.Paused,
            CountdownPhase.Done => TimeFaceInk.Over,
            _ => TimeFaceInk.Text,
        };
        return new TimeFaceReading(text, ink, NameOf(line));
    }

    /// <summary>The name a timer or a focus session was given, or empty.</summary>
    private static string NameOf(CellItem? line) => line?.Countdown?.Name?.Trim() ?? "";

    /// <summary>
    /// What the face says in its room: the name beside the figures where it has one, across a
    /// horizontal bar (its room is measured for both); else the figures alone — but once over, the
    /// name in their place where it fits there, so what rang says which one it is.
    /// </summary>
    private static (string? Beside, string Text) Says(Gfx gfx, TimeFaceReading reading, bool vertical, float px, float room)
    {
        if (reading.Name.Length == 0)
        {
            return (null, reading.Text);
        }
        if (!vertical)
        {
            return (reading.Name, reading.Text);
        }
        return reading.Ink == TimeFaceInk.Over && TextLine.Template(gfx, reading.Name, px, Weight.Regular).Width <= room
            ? (null, reading.Name)
            : (null, reading.Text);
    }

    /// <summary>Down a side bar, the rows under the glyph: what it says, its figures split where they have a colon.</summary>
    private static string[] SideRows(Gfx gfx, TimeFaceReading reading, float px, float room)
    {
        var says = Says(gfx, reading, vertical: true, px, room).Text;
        return says.Contains(':', StringComparison.Ordinal) ? VerticalFit.Parts(says, words: false) : [says];
    }

    /// <summary>
    /// The glyph's size, the figures' size and the room the widest reading keeps, on a bar of
    /// <paramref name="style"/>: what the face is measured with and paints with, one answer for both,
    /// as the entries had it. The glyph about as tall as the text, larger on a thick bar; down a
    /// vertical bar too narrow for the reading, the text a step smaller.
    /// </summary>
    internal static (float Glyph, float Px, float Width, float Height) Room(Gfx gfx, in IslandStyle style, bool counts)
    {
        var fit = new Fit(style);
        var px = fit.Primary;
        var (width, height) = Widest(gfx, px, counts);
        var tall = MathF.Max(height, style.Thickness * 0.4f);
        return (MathF.Min(fit.GlyphFor(tall), fit.Glyph(0.6f)), px, width, height);
    }

    /// <summary>
    /// Down a side bar: the glyph's size, and the one size and row height of the figures — the
    /// largest of the ladder at which two figures ("04", a row of a running one) and the longest
    /// length set ("888m") fit across the bar's room.
    /// </summary>
    private (float Glyph, float Px, float Height) Side(Gfx gfx, in IslandStyle style)
    {
        var fit = new Fit(style);
        string[] shapes = Counts ? ["88", S.WidgetCountdownBarMinutes(888)] : ["88"];
        var (px, height) = VerticalFit.AcrossAll(gfx, shapes, VerticalFit.Room(fit), Weight.Regular, VerticalFit.Ladder(fit));
        return (MathF.Min(fit.GlyphFor(style.Thickness * 0.5f), fit.Glyph(0.6f)), px, height);
    }

    /// <summary>The widest it says: five figures, and a countdown's length with its "m".</summary>
    private static (float Width, float Height) Widest(Gfx gfx, float px, bool counts)
    {
        var (width, height) = TextLine.Template(gfx, "88:88", px, Weight.Regular);
        if (counts)
        {
            var (across, down) = TextLine.Template(gfx, S.WidgetCountdownBarMinutes(888), px, Weight.Regular);
            (width, height) = (MathF.Max(width, across), MathF.Max(height, down));
        }
        return (width, height);
    }
}

//// <summary>
/// The clock down a bar on its side (docs\widgets.md §161): the time's parts one over another — the
/// hours over the minutes, the seconds and a 12-hour clock's AM or PM each a row of their own — and
/// under them, a little apart, the label in whole words on as many rows as it takes and what a timer
/// in the folder has left. Never turned (the person, 2026-10-13: "if possible dont try to flip
/// text"). From the settings and templates alone, so a minute, a running timer or the hour going
/// from 9 to 10 never moves the bar.
/// </summary>
/// <param name="TimePx">The time's rows' size: the largest of the ladder its widest part fits the bar at.</param>
/// <param name="TimeHeight">One of the time's rows.</param>
/// <param name="TimeRows">How many parts the time has (VerticalFit.Parts).</param>
/// <param name="TailPx">The label's and the countdown's size: never more than the time's.</param>
/// <param name="TailHeight">One of their rows.</param>
/// <param name="Label">The label's rows.</param>
/// <param name="CountRows">The countdown's room: none, one row ("04:59"), or two where five figures do not fit across.</param>
/// <param name="Gap">Between the time and what follows it.</param>
internal readonly record struct SideClockPlan(float TimePx, float TimeHeight, int TimeRows, float TailPx, float TailHeight, IReadOnlyList<string> Label, int CountRows, float Gap)
{
    /// <summary>The face's length down the bar.</summary>
    public float Along => (TimeRows * TimeHeight) + (Label.Count + CountRows > 0 ? Gap + ((Label.Count + CountRows) * TailHeight) : 0);
}

/// <summary>
/// The clock: the time as the clock on the taskbar draws it (docs\widgets.md §12.5), as a face, and
/// only the clock — the date is the calendar face's. Across a horizontal bar one row at every height
/// — the time, then "· label" or what a timer or focus session in the folder has left — as wide as
/// it reads now (§244). Down a vertical bar the time's parts one over another and what follows
/// it under them (<see cref="SideClockPlan"/>).
/// </summary>
internal sealed unsafe class ClockFaceDrawing : IFaceDrawing
{
    public string Id => FolderFaces.ClockId;

    public bool Seconds(FolderFace face) => ClockFace.Read(face).Seconds;

    /// <summary>A five-figure countdown at its widest (Countdown.Cell).</summary>
    private const string CountdownShape = "00:00";

    public FaceSize Measure(in FaceContext context)
    {
        if (context.Vertical)
        {
            return new FaceSize(Side(context).Along, Square: false);
        }
        // The line it reads now (§244), as a template: its figures are tabular, so the seconds and
        // the minutes tick in one width, and only a figure more ("9:59" to "10:00"), AM to PM or a
        // countdown starting or ending moves the bar.
        var settings = ClockFace.Read(context.Face);
        var line = Join(Time(settings, DateTime.MinValue), settings, context.Folder);
        return new FaceSize(TextLine.Template(context.Gfx, line, context.Fit.Primary, Weight.Regular).Width, Square: false);
    }

    public void Paint(ID2D1DeviceContext* dc, in FaceContext context, D2D_RECT_F box)
    {
        var settings = ClockFace.Read(context.Face);
        var ink = new IslandInk(context.Style.Look);
        var gfx = context.Gfx;
        var time = Time(settings, DateTime.MinValue);
        if (!context.Vertical)
        {
            var px = context.Fit.Primary;
            var (_, lineHeight) = TextLine.Template(gfx, "8", px, Weight.Regular);
            FaceDrawings.Line(dc, gfx, Join(time, settings, context.Folder), px, ink.Text, box, lineHeight);
            return;
        }
        var plan = Side(context);
        var left = settings.Timer && context.Folder is { } folder ? folder.CountdownLeft() : "";
        // What a countdown has left, where one is running: on one row, or its two figures over each
        // other where five do not fit across.
        string[] counting = left.Length == 0 || plan.CountRows == 0 ? [] : plan.CountRows == 1 ? [left] : VerticalFit.Parts(left, words: false);
        var rows = VerticalFit.Parts(time);
        var tail = plan.Label.Count + counting.Length;
        // The block as it reads now in the middle of the room kept for it: a countdown not running
        // leaves its room above and below, as across a bar it leaves it at both ends.
        var block = (rows.Length * plan.TimeHeight) + (tail > 0 ? plan.Gap + (tail * plan.TailHeight) : 0);
        var y = TextLine.Round(box.top + ((box.bottom - box.top - block) / 2));
        foreach (var row in rows)
        {
            FaceDrawings.Line(dc, gfx, row, plan.TimePx, ink.Text, new D2D_RECT_F(box.left, y, box.right, y + plan.TimeHeight), plan.TimeHeight);
            y += plan.TimeHeight;
        }
        if (tail == 0)
        {
            return;
        }
        y += plan.Gap;
        foreach (var row in plan.Label)
        {
            FaceDrawings.Line(dc, gfx, row, plan.TailPx, ink.Secondary, new D2D_RECT_F(box.left, y, box.right, y + plan.TailHeight), plan.TailHeight);
            y += plan.TailHeight;
        }
        foreach (var row in counting)
        {
            FaceDrawings.Line(dc, gfx, row, plan.TailPx, ink.Text, new D2D_RECT_F(box.left, y, box.right, y + plan.TailHeight), plan.TailHeight);
            y += plan.TailHeight;
        }
    }

    public string Reading(in FaceContext context)
    {
        var settings = ClockFace.Read(context.Face);
        return Join(Time(settings, DateTime.MinValue), settings, context.Folder);
    }

    public string? Tooltip(in FaceContext context)
    {
        var settings = ClockFace.Read(context.Face);
        var now = Clock.Read(settings.Zone, seconds: false);
        return now.Zone is { Length: > 0 } zone ? $"{now.Long}\n{zone}" : now.Long;
    }

    /// <summary>
    /// The time's rows down a side bar for <paramref name="settings"/>: one size for all of them, the
    /// largest of the ladder (VerticalFit.Ladder) at which the widest of the morning's and the
    /// evening's parts fits the bar's room — a 12-hour clock's two markers differ — and how many.
    /// The time-and-date face sets its time with these too.
    /// </summary>
    internal static (float Px, float Height, int Rows) SideTime(Gfx gfx, in ClockFaceSettings settings, in Fit fit)
    {
        var day = new DateTime(2026, 12, 30);
        var evening = VerticalFit.Parts(Time(settings, day.AddHours(23).AddMinutes(59).AddSeconds(59)));
        var morning = VerticalFit.Parts(Time(settings, day.AddHours(11).AddMinutes(59).AddSeconds(59)));
        var (px, height) = VerticalFit.AcrossAll(gfx, [.. evening, .. morning], VerticalFit.Room(fit), Weight.Regular, VerticalFit.Ladder(fit));
        return (px, height, Math.Max(evening.Length, morning.Length));
    }

    /// <summary>The plan down a side bar (<see cref="SideClockPlan"/>), from the settings and templates alone.</summary>
    internal static SideClockPlan Side(in FaceContext context)
    {
        var settings = ClockFace.Read(context.Face);
        var fit = context.Fit;
        var gfx = context.Gfx;
        var (px, height, rows) = SideTime(gfx, settings, fit);
        var room = VerticalFit.Room(fit);
        var counts = settings.Timer && FolderIsland.HoldsCountdown(context.Options);
        // The label's every word and the countdown's five figures, at the largest size, no larger
        // than the time's, at which each fits across — a label somebody chose is set smaller, never
        // shortened (§12.4); only a word wider than the bar at the smallest is cut, by its line.
        var shapes = new List<string>(settings.Label.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (counts)
        {
            shapes.Add(CountdownShape);
        }
        var ladder = VerticalFit.Ladder(fit).Where(size => size <= px).ToArray();
        var (tailPx, tailHeight) = shapes.Count == 0 ? (px, height) : VerticalFit.AcrossAll(gfx, shapes, room, Weight.Regular, ladder.Length > 0 ? ladder : [px]);
        List<string> label = settings.Label.Length > 0 ? VerticalFit.Wrap(gfx, settings.Label, tailPx, room, Weight.Regular) : [];
        var countRows = !counts ? 0 : TextLine.Template(gfx, CountdownShape, tailPx, Weight.Regular).Width <= room ? 1 : 2;
        return new SideClockPlan(px, height, rows, tailPx, tailHeight, label, countRows, MathF.Max(fit.Px(2), 2 * fit.Leading));
    }

    /// <summary>The time now in the face's zone and clock, as the taskbar sets it; at <paramref name="at"/> unless that is MinValue.</summary>
    internal static string Time(in ClockFaceSettings settings, DateTime at)
    {
        string text;
        if (settings.Hours is ClockFace.Twelve or ClockFace.TwentyFour)
        {
            var when = at == DateTime.MinValue ? Clock.NowIn(settings.Zone) : at;
            text = RegionFormats.Time(when, Pattern(settings)) ?? Clock.Read(settings.Zone, settings.Seconds).Time;
        }
        else
        {
            text = at == DateTime.MinValue
                ? Clock.Read(settings.Zone, settings.Seconds).Time
                : Clock.At(at.Hour, at.Minute, at.Second, settings.Seconds);
        }
        return text.Replace(':', FaceDrawings.Ratio);
    }

    /// <summary>The pattern a 12- or 24-hour face writes the time with: the region's (or the one set in Karpo), its hours changed.</summary>
    internal static string Pattern(in ClockFaceSettings settings)
    {
        var region = Nls.TimePattern(seconds: true) ?? RegionFormats.TimeFormats().FirstOrDefault() ?? "HH:mm:ss";
        var pattern = ClockFace.Pattern(region, settings.Hours);
        return settings.Seconds ? pattern : Nls.WithoutSeconds(pattern);
    }

    /// <summary>
    /// The widest the time reads in the face's format: the morning's last second and the evening's,
    /// whichever is wider (a 12-hour clock's two markers differ). The figures are tabular, so only
    /// how many there are counts, and that is the format's, not the hour's.
    /// </summary>
    internal static string WidestTime(Gfx gfx, in ClockFaceSettings settings, float px)
    {
        var day = new DateTime(2026, 12, 30);
        var evening = Time(settings, day.AddHours(23).AddMinutes(59).AddSeconds(59));
        var morning = Time(settings, day.AddHours(11).AddMinutes(59).AddSeconds(59));
        return TextLine.Template(gfx, morning, px, Weight.Regular).Width > TextLine.Template(gfx, evening, px, Weight.Regular).Width ? morning : evening;
    }

    /// <summary>What follows the time now: the label, and what a running countdown in the folder has left.</summary>
    private static string Tail(in ClockFaceSettings settings, FolderIsland? folder)
    {
        var left = settings.Timer && folder is not null ? folder.CountdownLeft() : "";
        return left.Length == 0 ? settings.Label
            : settings.Label.Length == 0 ? left
            : $"{settings.Label} {left}";
    }

    /// <summary>The one line across a horizontal bar.</summary>
    private static string Join(string time, in ClockFaceSettings settings, FolderIsland? folder)
    {
        var line = time;
        if (Tail(settings, folder) is { Length: > 0 } tail)
        {
            line += FaceDrawings.Dot + tail;
        }
        return line;
    }
}

/// <summary>
/// The calendar: today as text — the short date as Windows writes it under its clock, or the day's
/// name and the date as the calendar's own taskbar entry has them — or a calendar page with the
/// day's number on it. As wide as today's date reads (§244).
/// </summary>
internal sealed unsafe class CalendarFaceDrawing : IFaceDrawing
{
    public string Id => FolderFaces.CalendarId;

    public bool Seconds(FolderFace face) => false;

    /// <summary>The share of the cell a calendar page takes: larger than a glyph, so its number reads.</summary>
    internal const float PageShare = 0.7f;

    /// <summary>Today in the face's zone (§230): this PC's unless it is set to another.</summary>
    internal static DateOnly Today(FolderFace face) => DateOnly.FromDateTime(Clock.NowIn(CalendarFace.ZoneOf(face)));

    public FaceSize Measure(in FaceContext context)
    {
        var fit = context.Fit;
        if (CalendarFace.IsPage(context.Face))
        {
            return new FaceSize(PageSide(fit), Square: true);
        }
        if (context.Vertical)
        {
            return new FaceSize(DateText.SideNames(context.Gfx, fit).Length + DateText.SideDate(context.Gfx, fit, Year(context.Face)).Length, Square: false);
        }
        // Today's date as it reads (§244): a new day may move the bar, never a minute.
        return new FaceSize(TextLine.Template(context.Gfx, Text(context.Face, Today(context.Face)), fit.Primary, Weight.Regular).Width, Square: false);
    }

    public void Paint(ID2D1DeviceContext* dc, in FaceContext context, D2D_RECT_F box)
    {
        var fit = context.Fit;
        var ink = new IslandInk(context.Style.Look);
        var gfx = context.Gfx;
        var today = Today(context.Face);
        if (CalendarFace.IsPage(context.Face))
        {
            PaintPage(dc, gfx, fit, ink, box, today.Day);
            return;
        }
        if (!context.Vertical)
        {
            var (_, lineHeight) = TextLine.Template(gfx, "8", fit.Primary, Weight.Regular);
            FaceDrawings.Line(dc, gfx, Text(context.Face, today), fit.Primary, ink.Text, box, lineHeight);
            return;
        }
        // Down a vertical bar, the calendar entry's own pair: the day's name over the date, the date
        // on as many rows as the bar asks, with its year where the face says it across a bar (§598).
        var names = DateText.SideNames(gfx, fit);
        var year = Year(context.Face);
        var date = DateText.SideDate(gfx, fit, year);
        var top = TextLine.Round(box.top + ((box.bottom - box.top - names.Length - date.Length) / 2));
        FaceDrawings.Rows(dc, gfx, [DateText.SideName(today.DayOfWeek, names.Form)], names, ink.Secondary, box, top);
        FaceDrawings.Rows(dc, gfx, DateText.SideForms(today, year)[date.Form], date, ink.Text, box, top + names.Length);
    }

    /// <summary>Whether the face writes the year: the short date does, the day and the date do not (<see cref="Text"/>).</summary>
    private static bool Year(FolderFace face) => !CalendarFace.WithDay(face);

    /// <summary>Today, and what holiday it is: a new day, or the holidays arriving, paints it again.</summary>
    public string Reading(in FaceContext context) =>
        Today(context.Face).ToString("O", System.Globalization.CultureInfo.InvariantCulture) + "|" + string.Join('|', Holidays(Today(context.Face)));

    /// <summary>The long date, its zone's name where it has one, and what holiday the day is where it is one (§237).</summary>
    public string? Tooltip(in FaceContext context) =>
        string.Join('\n', [Zoned(Nls.LongDate(Today(context.Face)), CalendarFace.ZoneOf(context.Face)), .. Holidays(Today(context.Face))]);

    /// <summary>holidays: what <paramref name="day"/> is a public holiday as, while they are on; none on a test's or the smoke's store.</summary>
    internal static IReadOnlyList<string> Holidays(DateOnly day) => CalendarStore.Shared.Holidays?.NamesOn(day) ?? [];

    /// <summary><paramref name="date"/>, and under it the zone's name where it is not this PC's, as the clock face's hover says it.</summary>
    internal static string Zoned(string date, string zone) =>
        zone.Length > 0 && Clock.Read(zone, seconds: false).Zone is { Length: > 0 } name ? $"{date}\n{name}" : date;

    /// <summary>The date as the face writes it across a horizontal bar.</summary>
    internal static string Text(FolderFace face, DateOnly day) =>
        CalendarFace.WithDay(face) ? Nls.DayAndShortDate(day, year: false) : Nls.ShortDate(day);

    /// <summary>The widest the text is over a year: the 28th of every month, every day of a week.</summary>
    internal static float Widest(Gfx gfx, FolderFace face, float px)
    {
        if (CalendarFace.WithDay(face))
        {
            return DateText.Widest(gfx, px, year: false);
        }
        var most = 0f;
        for (var month = 1; month <= 12; month++)
        {
            most = MathF.Max(most, TextLine.Template(gfx, Nls.ShortDate(new DateOnly(2026, month, 28)), px, Weight.Regular).Width);
        }
        return most;
    }

    /// <summary>A calendar page's side, in px: <see cref="PageShare"/> of the cell, on whole pixels, never under the glyph.</summary>
    internal static float PageSide(in Fit fit) => MathF.Max(fit.Glyph(), MathF.Round(fit.Cell * PageShare));

    /// <summary>
    /// A page torn from a calendar: an outline with its corners rounded, a band of the accent along
    /// its top, and the day's number in the middle of what is left, as large as the room holds.
    /// </summary>
    internal static void PaintPage(ID2D1DeviceContext* dc, Gfx gfx, in Fit fit, in IslandInk ink, D2D_RECT_F box, int day)
    {
        var side = MathF.Min(box.right - box.left, box.bottom - box.top);
        var left = TextLine.Round(box.left + ((box.right - box.left - side) / 2));
        var top = TextLine.Round(box.top + ((box.bottom - box.top - side) / 2));
        var page = PopupPaint.Rect(left, top, side, side);
        // The outline grows with the page, as a glyph's strokes grow with the glyph: one px on a
        // page of 18, two from about 27 up; whole px, so it lands crisply on the pixel grid.
        var stroke = MathF.Max(1, MathF.Round(side / 18));
        var radius = MathF.Max(2, MathF.Round(side * 0.16f));
        var band = MathF.Max(2, MathF.Round(side * 0.24f));
        // The band: the page's top, its own corners rounded and its bottom edge straight.
        var clip = PopupPaint.Rect(left, top, side, band);
        dc->PushAxisAlignedClip(&clip, D2D1_ANTIALIAS_MODE.D2D1_ANTIALIAS_MODE_ALIASED);
        try
        {
            PopupPaint.Fill(dc, page, radius, ink.Accent);
        }
        finally
        {
            dc->PopAxisAlignedClip();
        }
        var inset = stroke / 2;
        PopupPaint.Stroke(dc, PopupPaint.Rect(left + inset, top + inset, side - stroke, side - stroke), MathF.Max(1, radius - inset), stroke, ink.Text);
        var body = PopupPaint.Rect(left, top + band, side, side - band);
        // The number: two figures fill most of the page's width and its body's height.
        var px = MathF.Max(6, MathF.Floor(MathF.Min((side - band) * 0.78f, side * 0.62f)));
        PopupPaint.Centred(dc, gfx, day.ToString(System.Globalization.CultureInfo.InvariantCulture), px, Weight.Semibold, ink.Text, body, ink: true);
    }
}

/// <summary>
/// The input indicator, in place of the keyboard entry on the bar that went (2026-10-11,
/// docs\widgets.md §124), drawn as it drew itself (<see cref="KeyboardIsland.PaintFace"/>): the
/// language over the region of the layout in use where two lines fit the bar, the language alone
/// where they do not, at the bar's text size. Read from the folder's own indicator
/// (<see cref="FolderIsland.Keyboard"/>), which the dock tells when another window comes to the
/// front or a layout changes, and painted again in place then.
/// <para>
/// Its length is the entry's (<see cref="KeyboardIsland.Room"/>): across a bar the codes it shows now
/// (§244) and the ends a text keeps, never less than a square; down one this PC's installed layouts'
/// codes' room and its ends. The folder adds its ends
/// to the face (WidgetShapes.Folder), so this is the entry's length less them. Its tooltip is the
/// entry's: the language and the layout's keyboard.
/// </para>
/// </summary>
internal sealed unsafe class KeyboardFaceDrawing : IFaceDrawing
{
    public string Id => FolderFaces.KeyboardId;

    public bool Seconds(FolderFace face) => false;

    public FaceSize Measure(in FaceContext context)
    {
        var fit = context.Fit;
        // Across a bar the codes it shows now (§244); down one, and with nothing read, this PC's layouts'.
        var codes = !context.Vertical && context.Folder?.Keyboard is { } keyboard ? new[] { keyboard.Codes } : null;
        var (width, _, _) = KeyboardIsland.Room(context.Gfx, context.Style, withRegion: true, codes);
        var pad = MathF.Max(fit.Edge, fit.Px(6));
        // The entry was its codes and a text's ends, square at the least, across a bar, and its
        // codes and the wider ends down one; the folder puts its own ends around the face.
        return context.Vertical
            ? new FaceSize((2 * pad) + width - (2 * fit.Edge), Square: false)
            : new FaceSize(MathF.Max(fit.Cell, (2 * fit.Edge) + width) - (2 * pad), Square: false);
    }

    /// <summary>Where it is only measured (no folder), nothing: the face is the layout in use, which a picture has none of.</summary>
    public void Paint(ID2D1DeviceContext* dc, in FaceContext context, D2D_RECT_F box) =>
        context.Folder?.Keyboard?.PaintFace(dc, context.Style, box);

    /// <summary>The codes: a layout switched, or another window in front with another, paints it again.</summary>
    public string Reading(in FaceContext context) => context.Folder?.Keyboard?.Reading ?? "";

    public string? Tooltip(in FaceContext context) => context.Folder?.Keyboard?.Tooltip(0);
}
