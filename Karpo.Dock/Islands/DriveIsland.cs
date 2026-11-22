// One drive: the one the person picked, and never another.
//
// The user: "there should be a variant where you can select exactly one item to
// show, e.g. a network drive, disk, usb drive etc. it wont show more than this
// only one." ListKind.Drive and ListKind.NetworkDrive are the same widget
// pointed at different kinds of drive (Karpo.Core\DriveTargets.cs): a line of a
// list folder, and what the drive faces on the bar draw.
//
// On the bar (docs\widgets.md §99): the drive and network drive entries went
// (2026-10-10) into the drive and network drive presets, folders wearing the
// drive faces, which draw what the entry drew (PaintFace) from the folder's
// first drive line — the same two cells whatever the drive shows: the drive's
// own shell icon in the square every other glyph sits in, then the drive's name,
// then Windows 11's capacity bar, one row across a horizontal bar at every height
// (docs\widgets.md §12.5), the bar a pill on the row's middle line running from
// one gap after the name to the face's end. The user: "the disk progress bar
// should scale to take up the remaining space". The bar keeps its least length
// before the name has any — how full the drive is, at a glance, is what the face
// is for — the name says what fits in the rest, down to the letter, and whatever
// the name does not use is the bar's. Down a vertical bar the three stack.
//
// No figure in the row. How much is free is a reading that changes, and a row
// may not follow it: it would need room kept for its widest ("888 GB free"),
// which a small drive leaves half empty, or a bar that moved as the drive filled.
// The bar is the glance; the figures are a click away, in the folder's line.
//
// A drive that is not connected is shown broken and present — faint, with a
// warning badge and an empty bar — never dropped and never resized: the bar
// must not move because a stick was pulled out.
//
// A network drive that is not connected is shown as File Explorer shows one
// (2026-10-09: "the not connected state of a network drive widget should show
// up in the same style as win11 would show not connected"): the shell's own
// disconnected network drive, the drive with the red X; its name in the text's
// own ink, not faded; no capacity bar, the name having the bar's room; and
// "Disconnected network drive", Explorer's word for it, in the secondary ink
// where a line has room for it. Until a share's first answer is in
// (DriveWatch: never waited for on the dock's thread) the widget is pending and
// draws neither: its glyph, its name and an empty track.
//
// In a folder the line is the drive's information alone (docs\widgets.md §119,
// 2026-10-11: "it should only show the drive information and then the drive
// contents should just be shown by the normal folder widget inline"): the drive
// kit's row with its caption and the meter (§401). What the drive holds is a folder pin's to show,
// a line of its own under it pointed at the drive's root (PinTile). A click
// opens the drive in File Explorer.

using Karpo.Core;
using Karpo.Dock.Drives;
using Karpo.Dock.Native;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock;

/// <summary>
/// What a drive is drawn from wherever it is drawn: the kind of widget (which drives it may show),
/// the drive as it was read, and what the settings name, for a drive that is not there.
/// </summary>
internal readonly record struct DriveLook(WidgetInfo Info, Drive Drive, DriveTargetOptions Target)
{
    /// <summary>The drives it may point at (<see cref="WidgetInfo.Drives"/>): a local one unless it says otherwise.</summary>
    public DriveFilter Drives => Info.Drives ?? DriveFilter.Local;

    /// <summary>
    /// A network drive it names that is not connected: drawn as File Explorer draws one, the drive
    /// with the red X, no capacity bar, and its name in the text's own ink.
    /// </summary>
    public bool Disconnected => Drive is { Missing: true, Pending: false } && Drives == DriveFilter.Network && Target.IsSet;

    /// <summary>Any other drive it names and cannot find, or none chosen: faint, with the warning badge.</summary>
    public bool Broken => Drive is { Missing: true, Pending: false } && !Disconnected;

    /// <summary>"Local Disk": the drive's name without its letter, or the one the settings remembered.</summary>
    public string Label => (Drive.Missing ? Target.Label : Drive.Label) ?? "";

    /// <summary>"C:", or the letter the settings remembered; the whole name where there is no letter.</summary>
    public string Letter
    {
        get
        {
            var letter = Drive.Missing ? DriveTargets.Letter(Target.Letter) : Drive.Name;
            return letter.Length > 0 ? letter : Label;
        }
    }

    /// <summary>What it shows now, to tell when a face of it must be painted again.</summary>
    public string Reading => $"{Drive.Letter}|{Drive.Missing}|{Drive.Pending}|{Label}|{Letter}|{MathF.Round(Drive.Used * 1000)}";
}

internal sealed unsafe partial class DriveIsland : CellIsland, IListContent
{
    /// <summary>
    /// How many cells a drive face takes along the bar: two, as the entry did — one holds the icon
    /// over a bar, two hold the icon, the drive's name and the bar, which is what it is for. A
    /// constant, not a measurement, so nothing the drive shows can change the face's size
    /// (docs\widgets.md §12.2).
    /// </summary>
    internal const int WideCells = 2;

    /// <summary>Segoe Fluent Icons: Warning, the badge on a drive that isn't connected.</summary>
    private const string WarningGlyph = "";

    /// <summary>How much of a drive shows while the drive it names is not there.</summary>
    private const float BrokenOpacity = 0.45f;

    /// <summary>The name's line, measured from a shape rather than from the name it happens to show.</summary>
    private const string NameShape = "Wg";

    /// <summary>
    /// The widest a drive letter can be, which is the one piece of the name that must fit: across
    /// a vertical bar the text size steps down until this does (<see cref="VerticalFit"/>).
    /// </summary>
    private const string LetterShape = "W:";

    /// <summary>
    /// Tests only, on the thread that makes the line: where the drive is opened, and what the
    /// volumes say of themselves, instead of this PC's, so nothing a test does reads a disk.
    /// </summary>
    [ThreadStatic]
    internal static FolderSource? FolderStand;

    /// <inheritdoc cref="FolderStand"/>
    [ThreadStatic]
    internal static Func<string, DriveFacts>? FactsStand;

    // Where the drive is opened and the volume's facts asked: this PC's, or a test's.
    private readonly FolderSource folders = FolderStand ?? FolderSource.Current;
    private readonly Func<string, DriveFacts> volumes = FactsStand ?? DriveReader.Facts;

    private DriveTargetOptions target = new();
    private Drive drive;

    // Whether the open folder shows the line: the volume's facts are read only then.
    private bool shown;

    // The volume's file system and BitLocker state, read off the dock's thread for the header while
    // the folder shows the line, and the root they were read for.
    private readonly Lock gate = new();
    private string factsRoot = "";
    private DriveFacts facts;

    // What a click asked to open, which the folder opens once it has gone (Dock.FolderForward).
    private (string Path, bool Folder)? opening;

    public DriveIsland(string key, WidgetItem island, Gfx gfx, Config config)
        : base(key, island, gfx)
    {
        drive = Drive.Absent(target);
        Configure(island, config);
    }

    /// <summary>A minute: the drive's figures.</summary>
    public override int IntervalMs => 60_000;

    /// <summary>One, always: the widget shows one drive, and stays where it is when it has none.</summary>
    protected override int Count => 1;

    /// <summary>The drive it shows; never null.</summary>
    public Drive At(int index) => drive;

    /// <summary>What the widget is pointed at, for the words when the drive isn't there.</summary>
    public DriveTargetOptions Target => target;

    /// <summary>The drive as it is drawn, here and on a face that shows this line.</summary>
    internal DriveLook Look => new(Info, drive, target);

    /// <summary>The drives it may point at (<see cref="WidgetInfo.Drives"/>): a local one unless it says otherwise.</summary>
    private DriveFilter Drives => Look.Drives;

    /// <inheritdoc cref="DriveLook.Disconnected"/>
    internal bool Disconnected => Look.Disconnected;

    /// <inheritdoc cref="DriveLook.Broken"/>
    private bool Broken => Look.Broken;

    /// <summary>The volume's file system and BitLocker state as last read for the header; empty before.</summary>
    internal DriveFacts Facts
    {
        get
        {
            lock (gate)
            {
                return facts;
            }
        }
    }

    public override void Configure(WidgetItem island, Config config)
    {
        // Its size is worked out from these (WidgetShapes), so they are kept in step.
        Settings = island;
        target = island.Drive ?? new DriveTargetOptions();
        drive = Read(labels: true);
        Forget();
    }

    /// <summary>
    /// The drive it names as the drives are now (<see cref="DriveWatch.For"/>). §643: a ghost's (<see cref="CellIsland.Sampled"/>)
    /// that this PC does not give — the made-up share, or a drive not there — is the samples' (<see cref="Samples.DriveOf"/>),
    /// and a network drive's is never asked of the network.
    /// </summary>
    private Drive Read(bool labels) =>
        !Sampled ? DriveWatch.For(Drives, target, labels, Announce)
        : Drives != DriveFilter.Network && DriveWatch.For(Drives, target, labels) is { Missing: false } found ? found
        : Samples.DriveOf(target);

    public override string? Tooltip(int index)
    {
        if (Disconnected)
        {
            return $"{DriveTargets.MissingTitle(target)}\n{S.CoreDriveTypeDisconnected}";
        }
        if (drive.Pending)
        {
            return DriveTargets.MissingTitle(target);
        }
        if (drive.Missing)
        {
            return $"{DriveTargets.MissingTitle(target)}\n{DriveTargets.MissingLine(Drives, target)}";
        }
        if (drive.Total == 0)
        {
            return $"{drive.Title}\n{DriveListing.NotReadyLine(drive.Kind)}";
        }
        var used = drive.Total - Math.Min(drive.Free, drive.Total);
        return $"{drive.Title}\n{S.WidgetDriveUsedOf(amount: Units.Of(used, drive.Total), percent: Units.Percent(drive.Used))}\n{S.WidgetDriveFreeSize(Units.Size(drive.Free))}";
    }

    /// <summary>
    /// In a folder: the drive in File Explorer, as the flyout's header and its footer did; an app
    /// opening, which the folder carries out once it has gone (<see cref="Open"/>). A drive that is
    /// not there opens nothing.
    /// </summary>
    public override IslandAction Click(int index)
    {
        opening = drive is { Missing: false, Root.Length: > 0 } ? (drive.Root, true) : null;
        return opening is null ? default : new(View: IslandView.App);
    }

    /// <summary>Opens what the last click asked for (<see cref="Click"/>), once; nothing where it asked for nothing.</summary>
    internal void Open()
    {
        if (opening is { } chosen)
        {
            opening = null;
            folders.Open(chosen.Path, chosen.Folder);
        }
    }

    // The widget is the same size in every state it can be in, and what varies — the name, how
    // full the drive is, whether it is there at all — is drawn into the room that size gives.

    /// <summary>
    /// Reads the drive again, and while the folder shows it, its volume's facts. A drive coming or
    /// going redraws the line and the face, and nothing on the bar moves.
    /// </summary>
    protected override bool Sample(bool everything)
    {
        var next = Read(labels: everything);
        // A share asked again whose answer is not in yet keeps what was drawn: only a first
        // answer is ever pending.
        drive = next.Pending && !drive.Missing ? drive : next;
        if (shown)
        {
            ReadFacts();
        }
        return false;
    }

    /// <summary>
    /// A drive face reads its line on the folder's own tick and as the dock reads every drive again
    /// (FolderIsland.Sample), whether or not the folder is open: what the drive shows, never its
    /// volume's facts, which are read only while the line shows.
    /// </summary>
    internal void Follow(bool everything)
    {
        var next = Read(labels: everything);
        drive = next.Pending && !drive.Missing ? drive : next;
    }

    /// <summary>The open folder shows the line, or no longer does: its volume's facts are read from now on, or not.</summary>
    public override void Visible(bool shown) => this.shown = shown;

    /// <summary>
    /// The volume's file system and BitLocker state for the header, as the flyout said them: read
    /// once per root on a thread of its own (a share that has gone waits there, not here: the
    /// folders' own, FolderSource.Run), and the line drawn again when they come.
    /// </summary>
    private void ReadFacts()
    {
        var root = drive is { Missing: false, Pending: false } ? drive.Root : "";
        lock (gate)
        {
            if (string.Equals(root, factsRoot, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            factsRoot = root;
            facts = default;
        }
        if (root.Length == 0)
        {
            return;
        }
        var asked = volumes;
        folders.Run(() =>
        {
            var read = asked(root);
            lock (gate)
            {
                if (!string.Equals(root, factsRoot, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
                facts = read;
            }
            _ = Wake();
        });
    }

    /// <summary>Never drawn on a bar: a drive is a line of a folder, and the drive faces draw what the entry drew (<see cref="PaintFace"/>).</summary>
    internal override void PaintCell(ID2D1DeviceContext* dc, int index, in IslandStyle style, float width, float height) =>
        throw new NotSupportedException($"{Info.Id} is a line of a folder's list, not a widget of a bar.");

    // --- As a folder's face ------------------------------------------------------------
    //
    // What the drive entry on the bar drew until it went (2026-10-10, docs\widgets.md §99): two
    // cells, the drive's picture, its name and the capacity bar after it, moved here as it was.

    /// <summary>
    /// A drive face's length along the bar, without the button's own room at its ends: what it shows
    /// now and no more (docs\widgets.md §244, §275). Across a bar, for <paramref name="line"/>'s drive,
    /// the icon, the name as it reads in the most room (<see cref="Name"/>) and the capacity bar's
    /// least length after it, so "C:" is a shorter face than "Local Disk (C:)", the button at most the
    /// two cells the entry was; down a bar on its side, the icon over its capacity bar, its name the
    /// tooltip's as a side bar's other faces' words are. Before a drive is chosen, the kind's glyph
    /// and its words (<see cref="SetupFace.OnBar"/>), the glyph alone down a side.
    /// </summary>
    internal static float FaceLength(in IslandStyle style, Gfx gfx, DriveIsland? line, bool showName = true)
    {
        var fit = new Fit(style);
        if (line is null || line.Unset is not null)
        {
            return style.Vertical
                ? fit.Glyph()
                : FaceDrawings.EmptyWidth(gfx, fit);
        }
        var look = line.Look;
        var height = MathF.Max(1, MathF.Ceiling(style.Thickness));
        if (style.Vertical)
        {
            // The rows of a cell the bar's width across and the face's length down, with no name.
            var rows = Rows(fit, height, height, allowName: false, capacity: !look.Disconnected);
            return (rows.Icon.bottom - rows.Icon.top) + (rows.WithBar ? MathF.Max(fit.Px(2), fit.Leading) + rows.Bar : 0);
        }
        // The cell as the face draws it at its most (the button's two cells less its ends, and the
        // drive's own Side round it), and from its rows how much of it is used.
        var most = MathF.Max(1, (WideCells * fit.Cell) - (2 * FolderIsland.Ends(fit)));
        var label = LabelFit(gfx, fit);
        var letter = label.Px > 0 ? TextLine.Template(gfx, LetterShape, label.Px, Weight.Regular).Width : 0;
        var widest = Rows(fit, most + (2 * fit.Side), height, label.Height, allowName: label.Px > 0, leastName: letter, capacity: !look.Disconnected);
        if (!widest.WithName && !widest.WithBar)
        {
            // The icon alone.
            return widest.Icon.right - widest.Icon.left;
        }
        // From the start of what follows the icon: the name, then a gap and the bar's least.
        var used = widest.Left;
        if (widest.WithName)
        {
            var room = MathF.Max(1, widest.NameRight - widest.Left);
            // Never under the letter's shape, which the row asks of a name before it has one.
            used += MathF.Max(letter, TextLine.Measure(gfx, Name(gfx, look, showName, label.Px, room), label.Px, Weight.Regular, room));
            used += widest.WithBar ? fit.Px(6) : 0;
        }
        used += widest.WithBar ? LeastBar(fit) : 0;
        return MathF.Min(most, MathF.Max(1, MathF.Ceiling(used - fit.Side)));
    }

    /// <summary>
    /// The face in <paramref name="box"/> (its <see cref="FaceLength"/> along the bar and the cell
    /// across), drawn as the entry drew its cell: the cell being the box and the drive's own Side round
    /// it, so its icon starts and its bar ends at the box's ends. <paramref name="line"/> is the
    /// folder's drive line it shows; with none, or with no drive chosen on it, its "Set up" as the
    /// entry had it, in a cell the box and the setup's Edge round it.
    /// </summary>
    internal static void PaintFace(ID2D1DeviceContext* dc, Gfx gfx, in IslandStyle style, D2D_RECT_F box, WidgetInfo info, DriveIsland? line, bool showName)
    {
        var fit = new Fit(style);
        var round = line is null || line.Unset is not null ? fit.Edge : fit.Side;
        var cell = style.Vertical
            ? new D2D_RECT_F(box.left, box.top - round, box.right, box.bottom + round)
            : new D2D_RECT_F(box.left - round, box.top, box.right + round, box.bottom);
        D2D_MATRIX_3X2_F before;
        dc->GetTransform(&before);
        // Drawn at the cell's own origin, as the entry's cell was.
        var moved = before with
        {
            _31 = before._31 + (cell.left * before._11) + (cell.top * before._21),
            _32 = before._32 + (cell.left * before._12) + (cell.top * before._22),
        };
        dc->SetTransform(&moved);
        try
        {
            var width = cell.right - cell.left;
            var height = cell.bottom - cell.top;
            if (line is null || line.Unset is { } need)
            {
                SetupFace.OnBar(dc, gfx, style, width, height, info.Glyph);
                return;
            }
            // Down a bar on its side the icon over its bar: the name is the tooltip's (FaceLength).
            Cell(dc, gfx, style, width, height, line.Look, showName, line.Announce, named: !style.Vertical);
        }
        finally
        {
            dc->SetTransform(&before);
        }
    }

    /// <summary>The drive's cell, as the entry drew it, at the origin, <paramref name="width"/> by <paramref name="height"/>; without its name where not <paramref name="named"/>.</summary>
    private static void Cell(ID2D1DeviceContext* dc, Gfx gfx, in IslandStyle style, float width, float height, DriveLook look, bool showName, Action announce, bool named)
    {
        var fit = new Fit(style);
        var ink = new IslandInk(style.Look);
        var faint = look.Broken;
        var withBar = !look.Disconnected;
        var text = faint ? IslandInk.Faded(ink.Text, BrokenOpacity) : ink.Text;

        dc->SetTextAntialiasMode(D2D1_TEXT_ANTIALIAS_MODE.D2D1_TEXT_ANTIALIAS_MODE_GRAYSCALE);
        var cell = new D2D_RECT_F(0, 0, width, height);
        dc->PushAxisAlignedClip(&cell, D2D1_ANTIALIAS_MODE.D2D1_ANTIALIAS_MODE_ALIASED);
        ID2D1SolidColorBrush* brush = null;
        TextLine name = default;
        try
        {
            brush = Gfx.Brush(dc, text);
            var label = LabelFit(gfx, fit);
            // A row has a name wherever the letter fits in what the bar leaves: measured from the
            // letter's shape, so whether there is one is a question of room and never of which
            // drive is showing.
            var letter = label.Px > 0 ? TextLine.Template(gfx, LetterShape, label.Px, Weight.Regular).Width : 0;
            var rows = Rows(fit, width, height, label.Height, allowName: named && label.Px > 0, leastName: letter, capacity: withBar);
            if (rows.WithName)
            {
                var room = MathF.Max(1, rows.NameRight - rows.Left);
                name = TextLine.Lay(gfx, Name(gfx, look, showName, label.Px, room), label.Px, Weight.Regular, room);
                // The name as it is drawn says where the bar starts in a row: one gap after it.
                rows = Rows(fit, width, height, label.Height, allowName: name.Width > 0, nameWidth: name.Width, leastName: letter, capacity: withBar);
            }
            Face(dc, gfx, rows.Icon, faint ? IslandInk.Faded(ink.Text, BrokenOpacity) : ink.Text, ink.Secondary, fit.Scale, look, announce);
            PaintWatch.Shape(rows.Icon);
            if (rows.WithName)
            {
                // In the row the name starts after the icon; stacked, over the bar, it is centred
                // on the bar's own edges.
                var room = MathF.Max(1, rows.NameRight - rows.Left);
                var left = rows.Stacked ? rows.Left + TextLine.Round((room - name.Width) / 2) : rows.Left;
                name.Draw(dc, brush, text, left, rows.NameTop);
            }
            if (rows.WithBar)
            {
                Bar(dc, brush, ink, look.Drive.Missing ? 0 : look.Drive.Used, rows.BarLeft, rows.Right, rows.BarTop, rows.Bar, faint);
            }
        }
        finally
        {
            name.Release();
            ComUtil.Release(ref brush);
            dc->PopAxisAlignedClip();
        }
    }

    /// <summary>
    /// What the cell calls the drive in the room it has: its whole name with its letter, else
    /// its name alone, else its letter, which always fits — or its letter alone where the face is
    /// set so. Choosing among them is drawing, not measuring — the room is the same either way — so
    /// a wider face says more and a narrow one still says which drive it is instead of "Local Dis…".
    /// </summary>
    private static string Name(Gfx gfx, DriveLook look, bool showName, float px, float room) =>
        showName
            ? DriveTargets.CellName(look.Label, look.Letter, room, text => TextLine.Measure(gfx, text, px, Weight.Regular, null))
            : look.Letter;

    /// <summary>
    /// How large the name is drawn, and how tall its line is; a size of 0 means there is no room
    /// for a name at all and the cell shows the icon and the bar instead.
    ///
    /// Along a horizontal bar the name has the cell's length to spread into, so it takes the
    /// widget text size like every other widget. Across a <b>vertical</b> bar all the room a line
    /// has is the bar's own width, and a size measured for a horizontal bar is simply cut — which
    /// is what the vertical audit found here: a blank line at 28 DIP and at large text. So the
    /// size steps down until the drive letter's own shape fits across the bar
    /// (<see cref="VerticalFit"/>), and where even the smallest will not fit, the name goes and
    /// the icon and the capacity bar carry the cell. The letter is two characters, so it has a
    /// size problem, not a length problem, and stepping down answers it; rotating is for text that
    /// needs more length than the bar has width.
    ///
    /// A template is measured, never the name being shown, so the size is a function of the
    /// settings and no state can change it — or the face's own size.
    /// </summary>
    internal static (float Px, float Height) LabelFit(Gfx gfx, in Fit fit)
    {
        if (!fit.Vertical)
        {
            return (fit.Primary, TextLine.Template(gfx, NameShape, fit.Primary, Weight.Regular).Height);
        }
        var room = VerticalFit.Room(fit);
        var smallest = MathF.Max(1, MathF.Min(fit.Small, fit.Px(9) * fit.TextScale));
        if (!VerticalFit.Fits(gfx, LetterShape, room, Weight.Regular, smallest))
        {
            return (0, 0);
        }
        var (px, _, lineHeight) = VerticalFit.Across(
            gfx, LetterShape, room, Weight.Regular, [fit.Primary, fit.Secondary, fit.Small, smallest]);
        return (px, lineHeight);
    }

    /// <summary>
    /// The drive's own picture in <paramref name="box"/>, or the kind's glyph until the shell has
    /// answered (and for a drive that isn't connected, which has no path to ask about). A drive
    /// that isn't there carries the warning badge pins use for a target this PC hasn't got; a
    /// network drive that isn't connected is Explorer's own picture of one, the drive with the red
    /// X (<see cref="DriveIcons.Disconnected"/>). One method for every surface the drive is drawn
    /// on, so its icon cannot differ between them.
    /// </summary>
    private static void Face(ID2D1DeviceContext* dc, Gfx gfx, D2D_RECT_F box, DXGI_RGBA text, DXGI_RGBA secondary, float scale, DriveLook look, Action announce)
    {
        var faint = look.Broken;
        var side = MathF.Min(box.right - box.left, box.bottom - box.top);
        if (side < 1)
        {
            return;
        }
        if (look.Disconnected)
        {
            if (DriveIcons.Disconnected((int)MathF.Ceiling(side), announce) is { } cut)
            {
                Gfx.DrawPixels(dc, cut, box.left, box.top, side, 1f);
            }
            else
            {
                // Until the shell's picture is read, or where it has none: the kind's glyph with
                // the red X drawn on as Explorer's picture has it.
                PopupPaint.Glyph(dc, gfx, look.Info.Glyph, side, text, box);
                RedX(dc, box, side);
            }
            return;
        }
        var picture = look.Drive.Missing ? null : DriveIcons.For(look.Drive.Volume, look.Drive.Root, (int)MathF.Ceiling(side), announce);
        if (picture is not null)
        {
            Gfx.DrawPixels(dc, picture, box.left, box.top, side, 1f);
        }
        else
        {
            PopupPaint.Glyph(dc, gfx, look.Info.Glyph, side, text, box);
        }
        if (!faint)
        {
            return;
        }
        var badge = MathF.Max(8 * scale, side * 0.5f);
        PopupPaint.Glyph(dc, gfx, WarningGlyph, badge, secondary,
            PopupPaint.Rect(box.right - badge * 0.75f, box.bottom - badge * 0.75f, badge, badge));
    }

    /// <summary>The drive's picture in <paramref name="box"/> on a line, as the face draws it.</summary>
    private void Face(ID2D1DeviceContext* dc, D2D_RECT_F box, DXGI_RGBA text, DXGI_RGBA secondary, float scale) =>
        Face(dc, Gfx, box, text, secondary, scale, Look, Announce);

    /// <summary>
    /// The red X Explorer's disconnected network drive carries, drawn on: a red disc with a white
    /// cross at the picture's foot on its right, half its size — in every theme, as the shell's
    /// picture is the same in every theme.
    /// </summary>
    private static void RedX(ID2D1DeviceContext* dc, D2D_RECT_F box, float side)
    {
        var badge = MathF.Max(6, MathF.Round(side * 0.5f));
        var radius = badge / 2;
        var middle = new D2D_POINT_2F(box.left + side - radius, box.top + side - radius);
        var arm = badge * 0.2f;
        ID2D1SolidColorBrush* brush = null;
        try
        {
            brush = Gfx.Brush(dc, Gfx.Ink(0.91f, 0.067f, 0.137f, 1f));
            var disc = new D2D1_ELLIPSE { point = middle, radiusX = radius, radiusY = radius };
            dc->FillEllipse(&disc, (ID2D1Brush*)brush);
            var white = Gfx.Ink(1f, 1f, 1f, 1f);
            brush->SetColor(&white);
            var stroke = MathF.Max(1, badge * 0.12f);
            dc->DrawLine(new D2D_POINT_2F(middle.x - arm, middle.y - arm), new D2D_POINT_2F(middle.x + arm, middle.y + arm), (ID2D1Brush*)brush, stroke, null);
            dc->DrawLine(new D2D_POINT_2F(middle.x - arm, middle.y + arm), new D2D_POINT_2F(middle.x + arm, middle.y - arm), (ID2D1Brush*)brush, stroke, null);
        }
        finally
        {
            ComUtil.Release(ref brush);
        }
    }

    /// <summary>The shell answered on its own thread: the dock redraws the line, and the face that shows it.</summary>
    private void Announce() => Wake();

    /// <summary>
    /// Windows 11's capacity bar: the track, and the used part in the accent, or in the alarm
    /// colour once the drive is nearly full. A drive that isn't there shows the track alone.
    /// </summary>
    private static void Bar(ID2D1DeviceContext* dc, ID2D1SolidColorBrush* brush, in IslandInk ink, float full, float left, float right, float top, float thickness, bool faint)
    {
        if (right - left < 1)
        {
            return;
        }
        var track = new D2D1_ROUNDED_RECT
        {
            rect = new D2D_RECT_F(left, top, right, top + thickness),
            radiusX = thickness / 2,
            radiusY = thickness / 2,
        };
        PaintWatch.Shape(track.rect);
        var colour = faint ? IslandInk.Faded(ink.Track, BrokenOpacity) : ink.Track;
        brush->SetColor(&colour);
        dc->FillRoundedRectangle(&track, (ID2D1Brush*)brush);
        if (full <= 0)
        {
            return;
        }
        // At least as long as the bar is thick, so a nearly empty drive still shows a round
        // dot rather than a sliver.
        var fill = track;
        fill.rect.right = left + MathF.Max((right - left) * full, thickness);
        colour = full > DriveListing.FullAt ? ink.Alarm : ink.Accent;
        brush->SetColor(&colour);
        dc->FillRoundedRectangle(&fill, (ID2D1Brush*)brush);
    }

    /// <summary>
    /// Where the cell's pieces go, in px from its top left.
    ///
    /// Across a horizontal bar, <b>one row at every height</b> (docs\widgets.md §12.5): the icon in
    /// a square at the content's start, then the drive's name, then the capacity bar to the content's
    /// end, all three on the cell's middle line. The bar's least length (<see cref="LeastBar"/>) is
    /// kept first and the name has what is left, down to the drive's letter
    /// (<see cref="DriveTargets.CellName"/>); the bar then starts one gap after the name as it is
    /// drawn (<paramref name="nameWidth"/>), so whatever the name does not use is the bar's. Where
    /// not even a letter's room is left — one cell of width — the bar runs from the icon to the end
    /// instead, and where there is no room for a bar either, the icon has the cell.
    ///
    /// Down a vertical bar, the one place the pieces stack: a cell with room across for a column
    /// has the icon and beside it the name over the bar, both running between the same two edges,
    /// which is what the old widget got wrong; a narrow one has the icon, then as much of the name
    /// and the bar as the height holds. The block is centred in the cell both ways.
    ///
    /// Pure, so the tests measure the arrangement instead of reading it off a picture.
    /// </summary>
    /// <param name="nameHeight">The name's line height; estimated from the text size when 0.</param>
    /// <param name="allowName">
    /// False where the name cannot be read in the room the cell has at all — across a narrow
    /// vertical bar (see <see cref="LabelFit"/>) — and the icon and the bar carry the cell.
    /// </param>
    /// <param name="nameWidth">
    /// How wide the name is as it will be drawn, which in a row is where the bar starts; 0 where it
    /// is not known yet, and the bar then starts where its least length does.
    /// </param>
    /// <param name="leastName">
    /// The room a name needs in a row at all: the drive letter's, measured from its shape. A row
    /// with that much left after the bar's least length has a name. 0 for the 22 DIP a name was
    /// given before it was measured, which dropped the letter from a two-cell widget on a 36 DIP bar
    /// with room for it.
    /// </param>
    /// <param name="capacity">
    /// False for a drive with no capacity bar at all — a network drive that is not connected, as
    /// Explorer shows one — whose name then has the bar's room too.
    /// </param>
    internal static DriveRows Rows(in Fit fit, float width, float height, float nameHeight = 0, bool allowName = true, float nameWidth = 0, float leastName = 0, bool capacity = true)
    {
        var sliver = MathF.Max(1, fit.Px(1));
        var edge = fit.Side;
        var left = MathF.Min(edge, MathF.Floor(width / 2f));
        var right = MathF.Max(left, width - edge);
        var icon = MathF.Max(1, MathF.Min(fit.Glyph(0.44f), MathF.Min(right - left, height - 2 * sliver)));
        var gap = fit.Px(6);
        var vgap = MathF.Max(fit.Px(2), fit.Leading);
        var bar = fit.Bar;
        var row = MathF.Min(nameHeight > 0 ? nameHeight : MathF.Ceiling(fit.Primary * 1.35f), height);

        if (!fit.Vertical)
        {
            // One row: the icon, the name, the bar, each centred on the cell's middle line.
            var after = left + icon + gap;
            var least = MathF.Min(LeastBar(fit), MathF.Max(0, right - after));
            var withBar = capacity && least >= fit.Px(8);
            var nameRight = withBar ? right - least - gap : right;
            var named = allowName && nameRight - after >= (leastName > 0 ? leastName : fit.Px(22)) && row + 2 * sliver <= height;
            var alone = !named && !withBar;
            return new DriveRows(
                PopupPaint.Rect(alone ? left + MathF.Floor((right - left - icon) / 2f) : left, TextLine.Round((height - icon) / 2f), icon, icon),
                after,
                right,
                named ? nameRight : after,
                TextLine.Round((height - row) / 2f),
                row,
                // One gap after the name, never later than where the bar's least length starts;
                // without a name the bar runs on from the icon: it is all the row has to say.
                !named ? after : nameWidth > 0 ? MathF.Min(after + nameWidth + gap, right - least) : right - least,
                TextLine.Round((height - bar) / 2f),
                bar,
                WithName: named,
                WithBar: withBar,
                Stacked: false);
        }

        // Down a vertical bar. Room beside the icon for a name worth reading?
        if (allowName && right - left - icon - gap >= fit.Px(22) && row + 2 * sliver <= height)
        {
            var textLeft = left + icon + gap;
            var withBar = capacity && row + vgap + bar + 2 * sliver <= height;
            var block = withBar ? row + vgap + bar : row;
            var top = TextLine.Round((height - block) / 2f);
            return new DriveRows(
                PopupPaint.Rect(left, TextLine.Round((height - icon) / 2f), icon, icon),
                textLeft,
                right,
                right,
                top,
                row,
                textLeft,
                top + row + vgap,
                bar,
                WithName: true,
                WithBar: withBar,
                Stacked: false);
        }

        // Narrow: the icon on top, and under it what the height allows. A name goes under it
        // only where the cell is a good deal taller than it is wide — a vertical bar's widget,
        // or a tall one. On a square cell a name would be two ellipsised letters, and the icon
        // over the bar says more.
        var withName = allowName
            && height - (right - left) >= row + vgap
            && icon + vgap + row + vgap + bar + 2 * sliver <= height;
        var underBar = capacity && icon + vgap + bar + 2 * sliver <= height;
        var stack = icon + (withName ? vgap + row : 0) + (underBar ? vgap + bar : 0);
        var stackTop = TextLine.Round((height - stack) / 2f);
        var iconLeft = left + MathF.Floor(MathF.Max(0, (right - left - icon) / 2f));
        return new DriveRows(
            PopupPaint.Rect(iconLeft, stackTop, icon, icon),
            left,
            right,
            right,
            stackTop + icon + vgap,
            row,
            left,
            stackTop + icon + (withName ? vgap + row : 0) + vgap,
            bar,
            WithName: withName,
            WithBar: underBar,
            Stacked: true);
    }

    /// <summary>
    /// The least length the capacity bar has in a horizontal row, kept before the name has any: a
    /// share of the cell, so it grows with the bar as the text does, from 12 DIP on the thinnest bar
    /// to 48 on a tall one — long enough to read as how full the drive is, however tight the widget.
    /// It is the least and not the length: the bar runs on to the widget's end, so a wider widget is
    /// a longer bar. It was the length, and a widget given more than two cells showed the rest as a
    /// blank stretch before a short bar at its far end.
    /// </summary>
    internal static float LeastBar(in Fit fit) => fit.Px(Math.Clamp(fit.Thick * 0.5f, 12, 48));

    /// <summary>Where a drive cell's pieces go, in px from the cell's top left.</summary>
    /// <param name="Icon">The square the drive's picture or glyph is drawn in.</param>
    /// <param name="Left">Where the pieces after the icon start: the name, or the bar where there is no name.</param>
    /// <param name="Right">Where they end: the bar's end.</param>
    /// <param name="NameRight">Where the name's room ends: before the bar's least length in a row, at <paramref name="Right"/> stacked.</param>
    /// <param name="NameTop">The name's top.</param>
    /// <param name="Row">How tall the name's line is, which is the room it was given.</param>
    /// <param name="BarLeft">
    /// Where the bar starts: one gap after the name in a row, or after the icon where there is no
    /// name; at <paramref name="Left"/> stacked. It ends at <paramref name="Right"/> either way.
    /// </param>
    /// <param name="BarTop">The capacity bar's top.</param>
    /// <param name="Bar">How thick the bar is.</param>
    /// <param name="Stacked">
    /// The icon is above the name rather than beside it, which only happens down a vertical bar;
    /// the name is then centred.
    /// </param>
    internal readonly record struct DriveRows(
        D2D_RECT_F Icon,
        float Left,
        float Right,
        float NameRight,
        float NameTop,
        float Row,
        float BarLeft,
        float BarTop,
        float Bar,
        bool WithName,
        bool WithBar,
        bool Stacked)
    {
        /// <summary>The top of everything the cell draws: what the block is centred by.</summary>
        public float Top => MathF.Min(Icon.top, WithName ? NameTop : Icon.top);

        /// <summary>The bottom of everything the cell draws.</summary>
        public float Bottom => MathF.Max(
            Icon.bottom,
            MathF.Max(WithName ? NameTop + Row : Icon.bottom, WithBar ? BarTop + Bar : Icon.bottom));
    }

    // --- As a line of a list ------------------------------------------------------
    // A drive's line in a list folder is the kit's row with its caption and the meter under it (§401): the
    // drive's picture in the list's leading column, its name where every name begins, what kind of drive
    // it is and how much of it is used under that, how much is free against the trailing gutter, and the
    // capacity meter from the text edge, in the alarm colour once nearly nothing is left. Taller, the file
    // system and the BitLocker state as the drives flyout said them (read off the dock's thread while the
    // folder shows it). What the drive holds is not the line's (docs\widgets.md §119): a folder pin to its
    // root shows it, a line of its own.

    /// <summary>
    /// Its form for its size (§432, §442): the row with the meter in it (Compact, two cells), or the row with its caption
    /// and the meter under the caption, one block in three cells (Full).
    /// </summary>
    public float ListHeight(float width, in TileStyle style) => LineRow.Height(style, caption: LineSizes.Of(Settings) != LineSize.Compact);

    /// <summary>
    /// The line, at whatever height it was given (docs\widgets.md §9, "A line at every height"), on the kit's
    /// blocks (§401), the same pieces in the same places at every height:
    /// <list type="bullet">
    /// <item>One row: the drive's picture, its name, the capacity bar filling the room after the name, and
    /// how much is free against the trailing gutter — the bar first and the figure after it where the row is
    /// short of room.</item>
    /// <item>From a row with its caption and the meter: what kind of drive it is and how much is used under
    /// the name, the meter under the row.</item>
    /// <item>Taller: its file system and BitLocker state under the meter, once read.</item>
    /// </list>
    /// A drive that is not there, or not ready, says what is the matter where its figure would be.
    /// </summary>
    private void Line(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style)
    {
        var sketch = new LineSketch(dc, Gfx);
        var form = ListForm.Of(Gfx, style);
        var theme = style.Theme;
        var height = TilePaint.Height(box);
        var meter = LineMeter.Thick(style);
        var missing = drive.Missing;
        var cut = Disconnected;
        var known = !missing && drive.Total > 0;
        var ink = Broken ? theme.TextTertiary : theme.Text;
        var secondary = theme.TextSecondary;
        var kind = cut ? S.CoreDriveTypeDisconnected : DriveTargets.TypeName(missing ? (Drives == DriveFilter.Network ? DriveKind.Remote : DriveKind.Fixed) : drive.Kind);
        // What is the matter where the figure would be: a disconnected share says so as Explorer does; one
        // whose first answer is not in says nothing yet.
        var matter = cut ? S.WidgetDriveDisconnected
            : drive.Pending ? ""
            : missing ? (target.IsSet ? S.WidgetDriveNotConnected : S.WidgetDriveNotSet) : S.WidgetDriveNotReady;
        var fill = drive.Used > DriveListing.FullAt ? Alarm(theme) : TilePaint.Accent(style);
        var name = missing ? DriveTargets.MissingTitle(target) : drive.Title;
        var full = height + 0.5f >= LineRow.Height(style, caption: true);
        var row = new RowSpec(name)
        {
            NameInk = ink,
            Caption = full ? (known ? LineRow.Join(kind, DriveListing.UsedOfTotal(drive.Total, drive.Free)) : kind) : null,
            Reading = known ? DriveListing.FreeSpace(drive.Total, drive.Free) : matter,
            Look = RowReading.Body,
            ReadingInk = known ? theme.Text : secondary,
            Under = full && known ? LineMeter.Room(style) : 0,
        };
        var laid = LineRow.Draw(sketch, box.left, box.right, box.top, style, row, full ? 0 : MathF.Min(height, LineRows.Px(1, style)));
        Face(dc, Square(form.IconBox(box.left, laid.Top, laid.Bottom - laid.Top), form.Glyph), ink, secondary, style.Scale);
        if (!known)
        {
            return;
        }
        if (!full)
        {
            // One row: the bar in the room between the name and the figure.
            var start = (laid.Name?.right ?? box.left + form.TextLeft) + style.Px(LineSpace.GapDip);
            var end = (laid.Reading?.left ?? box.right - form.Inset) - style.Px(LineSpace.GapDip);
            if (end - start >= MathF.Min(ListTiers.LeastPicture(style), style.Px(24)))
            {
                TilePaint.Bar(dc, PopupPaint.Rect(start, PopupPaint.Round(laid.Middle - (meter / 2)), end - start, meter), drive.Used, TilePaint.Track(style), fill);
            }
            return;
        }
        LineMeter.Under(sketch, laid, box.left, box.right, style, drive.Used, fill);
        var y = laid.Bottom;
        var read = Facts;
        var more = LineRow.Join(read.FileSystem, read.BitLocker);
        var captionPx = style.Text(LineType.CaptionDip);
        if (more.Length > 0 && y + TilePaint.LineHeight(Gfx, captionPx) <= box.bottom + 0.5f)
        {
            sketch.Text("facts", more, captionPx, Weight.Regular, secondary, box.left + form.TextLeft, y, form.Room(TilePaint.Width(box)));
        }
    }

    /// <summary>A square <paramref name="side"/> px across, centred in <paramref name="box"/>.</summary>
    private static D2D_RECT_F Square(D2D_RECT_F box, float side) =>
        PopupPaint.Rect(
            PopupPaint.Round(box.left + ((box.right - box.left - side) / 2)),
            PopupPaint.Round(box.top + ((box.bottom - box.top - side) / 2)),
            side,
            side);

    // --- In a folder ------------------------------------------------------------

    protected internal override unsafe void PaintCard(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, WidgetSurface surface) =>
        Line(dc, box, style);

    /// <summary>A nearly full drive's bar, as the island's own.</summary>
    private static DXGI_RGBA Alarm(FlyoutTheme theme) =>
        theme.HighContrast ? PopupPaint.Ink(theme.Accent) : theme.Dark ? Gfx.Ink(1f, 0.6f, 0.643f, 1f) : Gfx.Ink(0.769f, 0.169f, 0.11f, 1f);
}
