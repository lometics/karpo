// camera: a live webcam's or IP camera's picture as a line of a list (docs\widgets.md §331).
//
// At every height (§9, "A line at every height"):
//   one row      the newest picture small in the leading column (the camera's glyph before one comes), the
//                camera's name, and at the end how it is: connecting, why not, a snapshot's time
//   from two     the picture as its Fit says: filling the whole line edge to edge (cut at its edges, centred,
//                its shape kept; only the folder's clip rounds it, where its section is rounded, §463), or
//                whole inside it between the list's gutters, its corners a control's, on the line's own ground; the
//                name on a plate at its foot — a red dot before it while the picture moves — and how it is
//                after the name, on the gutter either way.
//                Before a picture comes, the one row at
//                the line's top (§401), and an automatic line is that row; then as tall as the picture's shape.
//
// The stream runs only while the folder shows the line (Visible): opened, it connects and the last picture
// it had shows meanwhile; closed, it stops — no request open, nothing decoded. A moving picture is drawn on
// the open folder's quick tick, only where a newer one came (Streaming, Fresh: FolderFlyout.ArmQuick); a
// snapshot and every change of how it is wake the dock as any line's news does. "Refresh" on its right
// click connects again from the start (IRefreshable, §451). A click opens the camera's page in the
// browser once the folder has gone.

using System.Numerics;
using Karpo.Core;
using Karpo.Dock.QuickSettings.Controls;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock;

/// <summary>A live camera's picture, as a line of a list (see the top of the file).</summary>
internal sealed unsafe class CameraLine : CellIsland, IRefreshable, IListContent
{
    private readonly CameraStream stream;
    private CameraOptions options = new();
    private bool showing;
    private long painted = -1;
    private string? chosen;

    // §463: a filling picture hides the folder's fill under the line, so it lights itself as the folder lights the line:
    // under the pointer, and pressed until the click (RowPress, one target: the whole line).
    private bool hot;
    private RowPress press;

    public CameraLine(string key, WidgetItem island, Gfx gfx, Config config)
        : base(key, island, gfx)
    {
        stream = new CameraStream(() => Wake());
        Configure(island, config);
    }

    /// <summary>
    /// What it shows instead of its stream's, which is then never opened: a test's, or §643 a ghost's (made showing the samples),
    /// the samples' made-up camera (<see cref="Samples.Camera"/>).
    /// </summary>
    internal Func<CameraShot>? Source { get; set; } = Samples.On ? Samples.Camera : null;

    /// <summary>Where a page opens: the browser, through the shell. A test puts its own here.</summary>
    internal Action<string> Opened { get; set; } = static link => Launch.Open(link);

    /// <summary>The stream it reads, for a test.</summary>
    internal CameraStream Stream => stream;

    /// <summary>What it shows now.</summary>
    internal CameraShot Shot => Source?.Invoke() ?? stream.Shot;

    /// <summary>The camera it shows.</summary>
    internal CameraOptions Options => options;

    /// <summary>No ticks: its stream says when there is something new.</summary>
    public override int IntervalMs => 0;

    protected override int Count => 1;

    /// <summary>Whether its picture moves in the open folder now: the folder's quick tick draws it while it does.</summary>
    internal bool Streaming => showing && Shot.Moving;

    /// <summary>Whether a picture newer than the one last drawn is there.</summary>
    internal bool Fresh => Shot.Number != painted;

    public override void Configure(WidgetItem island, Config config)
    {
        Settings = island;
        options = island.Camera ?? new CameraOptions();
        if (showing && Source is null)
        {
            stream.Start(options);
        }
        Forget();
    }

    public override void Visible(bool shown)
    {
        base.Visible(shown);
        showing = shown;
        if (!shown)
        {
            hot = false;
            _ = press.Up();
        }
        if (Source is not null)
        {
            return;
        }
        if (shown)
        {
            stream.Start(options);
        }
        else
        {
            stream.Stop();
        }
    }

    public override void Dispose()
    {
        stream.Dispose();
        base.Dispose();
    }

    public override string? Tooltip(int index) => null;

    protected override bool Sample(bool everything) => false;

    // --- Refresh (IRefreshable) -----------------------------------------------------------------

    public bool CanRefresh => options.IsSet;

    /// <summary>Connecting: no "Refresh" meanwhile.</summary>
    public bool Refreshing => Shot.State == CameraState.Connecting;

    public DateTime? UpdatedUtc => Shot.FrameUtc;

    string? IRefreshable.Problem => Shot.State == CameraState.Failed ? Shot.Problem : null;

    /// <summary>Connects again from the start.</summary>
    public void RefreshNow()
    {
        if (Source is null && showing)
        {
            stream.Restart(options);
        }
        Redrawn();
    }

    /// <summary>
    /// How tall an automatic line is (§401): its picture at the list's width as the picture's own shape (one row's
    /// less at the least); one row, the kit's row of its name and how it is, while no picture has come.
    /// </summary>
    public float ListHeight(float width, in TileStyle style)
    {
        if (Shot.Frame is not { Width: > 0, Height: > 0 } frame)
        {
            return LineRows.Px(1, style);
        }
        // Filling, the line is the picture: its shape at the line's width. Whole, its room between the gutters and the air
        // over and under it.
        var room = Room(PopupPaint.Rect(0, 0, width, 0), style);
        var air = -TilePaint.Height(room);
        return MathF.Max(LineRows.Px(2, style), PopupPaint.Round((TilePaint.Width(room) * frame.Height / frame.Width) + air));
    }

    // --- Clicks ----------------------------------------------------------------------------------

    /// <summary>A click opens the camera's page once the folder has gone; one with none (an RTSP camera) does nothing.</summary>
    public override IslandAction Click(int index)
    {
        if (press.Up())
        {
            _ = Wake();
        }
        chosen = Cameras.PageOf(options);
        return chosen is null ? default : new IslandAction(View: IslandView.App);
    }

    /// <summary>The page "Open website" and a click open: the camera's own, or its address.</summary>
    internal string? Site => Cameras.PageOf(options);

    /// <summary>The dock, once the folder has gone: opens the page the click chose, once.</summary>
    internal void Open()
    {
        if (chosen is { } link)
        {
            chosen = null;
            Opened(link);
        }
    }

    /// <summary>Never drawn on a bar: a camera is a line of a folder's list.</summary>
    internal override void PaintCell(ID2D1DeviceContext* dc, int index, in IslandStyle style, float width, float height) =>
        throw new NotSupportedException($"{Info.Id} is a line of a folder's list, not a widget of a bar.");

    // --- Where things are ------------------------------------------------------------------------

    /// <summary>Whether a line <paramref name="box"/> tall is one row: too short for a picture worth the name.</summary>
    internal bool One(D2D_RECT_F box, in TileStyle style) => TilePaint.Height(box) + 0.5f < LineRows.Px(2, style);

    /// <summary>
    /// The room between the list's gutters (§9, "One gutter"), a little air over and under it: where a picture shown
    /// whole stands, and where the name's plate stands on any picture.
    /// </summary>
    internal D2D_RECT_F PictureBox(D2D_RECT_F box, in TileStyle style)
    {
        var form = ListForm.Of(Gfx, style);
        var air = style.Px(4);
        return new D2D_RECT_F(box.left + form.Inset, box.top + air, box.right - form.Inset, box.bottom - air);
    }

    /// <summary>Whether its picture fills its line (Fit: Fill), rather than standing whole inside it.</summary>
    internal bool Fills => options.Fit != CameraFit.Whole;

    /// <summary>
    /// Where its picture is drawn (§463): filling, the whole line — no gutter, no air, the folder's clip rounding it
    /// where its section is rounded; whole, <see cref="PictureBox"/>.
    /// </summary>
    internal D2D_RECT_F Room(D2D_RECT_F box, in TileStyle style) => Fills ? box : PictureBox(box, style);

    /// <summary>
    /// How large a picture is decoded: its longer side as it is drawn in <paramref name="room"/> — covering it, the
    /// room's longer side or more where the picture's shape is not the room's — never past three times that side.
    /// </summary>
    private int MostFor(D2D_RECT_F room, Pixels? frame)
    {
        var (width, height) = (TilePaint.Width(room), TilePaint.Height(room));
        var longer = MathF.Max(width, height);
        if (Fills && frame is { Width: > 0, Height: > 0 } shape)
        {
            var cover = MathF.Max(width / shape.Width, height / shape.Height);
            longer = MathF.Min(3 * longer, MathF.Max(shape.Width, shape.Height) * cover);
        }
        return (int)MathF.Ceiling(MathF.Max(64, longer));
    }

    protected internal override void CardHover(Vector2? point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var on = point is not null;
        var let = press.Over(on ? 1 : 0);
        if (on == hot && !let)
        {
            return;
        }
        hot = on;
        _ = Wake();
    }

    /// <summary>A press lights it pressed; the press stays the folder's, which makes it the click (RowPress).</summary>
    protected internal override void CardDown(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        if (press.Down(1))
        {
            _ = Wake();
        }
    }

    /// <summary>How it is, in words: connecting, why not, a snapshot's time; nothing while a stream moves.</summary>
    internal string Status(CameraShot shot) => shot.State switch
    {
        CameraState.Failed => shot.Problem ?? S.DockCameraUnreadable,
        CameraState.Connecting => S.DockCameraConnecting,
        // Shown before its stream has started: it starts as it shows.
        CameraState.Idle when shot.Frame is null => S.DockCameraConnecting,
        _ when shot.Protocol == CameraProtocol.Snapshot && shot.FrameUtc is { } at => Panels.Nls.Time(at.ToLocalTime()),
        _ => "",
    };

    // --- Drawn -----------------------------------------------------------------------------------

    protected internal override void PaintCard(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var shot = Shot;
        painted = shot.Number;
        // Pictures decoded no larger than they are drawn here.
        stream.Most = MostFor(Room(box, style), shot.Frame);
        if (One(box, style) || shot.Frame is not { } frame)
        {
            // One row, or no picture yet: the kit's row at the line's top (§401), never a block in its middle.
            Row(dc, box with { bottom = MathF.Min(box.bottom, box.top + LineRows.Px(1, style)) }, style, shot);
            return;
        }
        var theme = style.Theme;
        var picture = PictureBox(box, style);
        if (Fills)
        {
            // Filling the whole line, edge to edge: square, the folder's clip rounds it where its section is rounded. It
            // covers the folder's fill under the line, so it wears that fill over itself: under the pointer, pressed.
            PopupPaint.Covered(dc, frame, box, 0);
            if (press.Held != 0 || hot)
            {
                LineItem.Light(dc, box, theme, pressed: press.Held != 0);
            }
        }
        else
        {
            // Whole inside the line: the room it leaves is the line's own ground, nothing laid under the picture (§475).
            var radius = style.Px(PopupPaint.ControlRadiusDip);
            PopupPaint.Covered(dc, frame, picture, radius, whole: true);
        }
        // The name's plate on the gutter, whichever way the picture stands.
        Caption(dc, picture, style, shot);
    }

    /// <summary>The plates over the picture: the list's own ground, nearly solid, so their words read on any picture.</summary>
    private static DXGI_RGBA Plate(in TileStyle style) => IslandInk.Faded(PopupPaint.Ink(style.Theme.Base), 0.86f);

    /// <summary>The name on its plate at the picture's foot, a red dot before it while it moves, how it is after it.</summary>
    private void Caption(ID2D1DeviceContext* dc, D2D_RECT_F picture, in TileStyle style, CameraShot shot)
    {
        var theme = style.Theme;
        var form = ListForm.Of(Gfx, style);
        var name = Cameras.NameOf(options);
        var status = Status(shot);
        var pad = style.Px(8);
        var dot = shot.Moving ? style.Px(8) : 0;
        var height = form.SmallLine + style.Px(8);
        var room = TilePaint.Width(picture) - (2 * style.Px(6)) - (2 * pad) - (dot > 0 ? dot + style.Px(6) : 0);
        if (room < style.Px(24) || TilePaint.Height(picture) < height + style.Px(12))
        {
            return;
        }
        var nameWidth = name.Length == 0 ? 0 : PopupPaint.Measure(Gfx, name, form.SmallPx, Weight.Semibold).X;
        var said = status.Length == 0 ? "" : (name.Length > 0 ? FaceDrawings.Dot : "") + status;
        var saidWidth = said.Length == 0 ? 0 : PopupPaint.Measure(Gfx, said, form.SmallPx, Weight.Regular).X;
        var nameRoom = MathF.Min(nameWidth, room);
        var saidRoom = MathF.Min(saidWidth, MathF.Max(0, room - nameRoom));
        if (nameRoom + saidRoom <= 0)
        {
            return;
        }
        var width = (2 * pad) + (dot > 0 ? dot + style.Px(6) : 0) + nameRoom + saidRoom;
        var plate = PopupPaint.Rect(picture.left + style.Px(6), picture.bottom - style.Px(6) - height, PopupPaint.Round(width), height);
        PopupPaint.Fill(dc, plate, style.Px(4), Plate(style));
        var x = plate.left + pad;
        var middle = (plate.top + plate.bottom) / 2;
        if (dot > 0)
        {
            PopupPaint.Fill(dc, PopupPaint.Rect(x, PopupPaint.Round(middle - (dot / 2)), dot, dot), dot / 2, new IslandInk(style.Look).Critical);
            x += dot + style.Px(6);
        }
        var top = PopupPaint.Round(middle - (form.SmallLine / 2));
        if (nameRoom > 0)
        {
            TilePaint.Line(dc, Gfx, name, form.SmallPx, Weight.Semibold, theme.Text, x, top, nameRoom + 1);
            x += nameRoom;
        }
        if (saidRoom > 0)
        {
            TilePaint.Line(dc, Gfx, said, form.SmallPx, Weight.Regular, theme.TextSecondary, x, top, saidRoom + 1);
        }
    }

    /// <summary>One row: the picture small in the leading column (or the glyph), the name, how it is at the end.</summary>
    private void Row(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, CameraShot shot)
    {
        var theme = style.Theme;
        var status = Status(shot);
        var form = ListForm.Of(Gfx, style);
        var field = status.Length > 0 ? MathF.Min(PopupPaint.Measure(Gfx, status, form.SmallPx, Weight.Regular).X + 1, TilePaint.Width(box) / 2) : 0;
        var line = ListTiers.Row(Gfx, box, style, [Cameras.NameOf(options)], Weight.Regular, field);
        var icon = form.IconBox(box.left, line.Middle - (form.Line / 2), form.Line);
        if (shot.Frame is { } frame)
        {
            PopupPaint.Covered(dc, frame, icon, style.Px(2));
        }
        else
        {
            PopupPaint.Glyph(dc, Gfx, Info.Glyph, form.Glyph, theme.TextSecondary, icon);
        }
        if (line.Name is { } name)
        {
            TilePaint.Line(dc, Gfx, name, form.TextPx, Weight.Regular, theme.Text, line.NameLeft, line.TextTop, line.NameRoom);
        }
        if (field > 0)
        {
            TilePaint.LineRight(dc, Gfx, status, form.SmallPx, Weight.Regular, theme.TextSecondary, line.FieldRight, PopupPaint.Round(line.Middle - (form.SmallLine / 2)), line.Field);
        }
        else if (shot.Moving)
        {
            // A moving picture says so with the red dot where its time would be.
            var dot = style.Px(8);
            PopupPaint.Fill(dc, PopupPaint.Rect(line.FieldRight - dot, PopupPaint.Round(line.Middle - (dot / 2)), dot, dot), dot / 2, new IslandInk(style.Look).Critical);
        }
    }
}
