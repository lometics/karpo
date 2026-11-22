// §641: what the Start widgets have in common (docs\widgets.md §641): one source (StartSources), their parts as one list
// of spots — what the pointer lights, what a click reaches, what the keys walk (ILineTargets: the folder's arrows go from spot to spot, Enter clicks), what a screen reader
// is told (ILinePieces) — and what a click or a menu's row leaves for the dock to do once the folder has gone (Act: an app
// or a file opened, a folder, a power command) or does to the config (Editor: a pin, a view).
//
// Opening goes through the one launcher (Launch.cs), and is counted (IStartSource.Opened); a power
// command through the power buttons' own (PowerButtonTile.Run), which a test stands in for, so no test ever sleeps, shuts
// down, signs out or locks the PC it runs on.

using System.Numerics;
using Karpo.Core;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;

namespace Karpo.Dock.StartWidgets;

/// <summary>§641: what a Start widget's click or menu row leaves for the dock (see the top of the file).</summary>
internal enum StartActKind
{
    /// <summary>An app or a file opened, and counted.</summary>
    Open,

    /// <summary>An app's program run as administrator.</summary>
    Admin,

    /// <summary>File Explorer at an app's program or a file, it selected.</summary>
    Location,

    /// <summary>A folder beside power, or a page of Settings.</summary>
    Place,

    /// <summary>A power or account command (<see cref="QuickLink"/>), by its name.</summary>
    Power,
}

/// <summary>§641: one act (<see cref="StartActKind"/>) and what it acts on.</summary>
internal readonly record struct StartAct(StartActKind Kind, string Target)
{
    /// <summary>The separator of a menu row's value: its act, its target.</summary>
    public const char Cut = '\u001f';

    /// <summary>As a menu row carries it.</summary>
    public string Value => $"{Kind}{Cut}{Target}";

    /// <summary>A menu row's value read back; null for one that is no act.</summary>
    public static StartAct? Of(string? value) =>
        value?.Split(Cut) is [var kind, var target] && Enum.TryParse<StartActKind>(kind, out var act) ? new StartAct(act, target) : null;
}

/// <summary>§641: a row of a Start widget's own menu: its words, its glyph and what it does — an act, or a change of the config.</summary>
/// <param name="Value">An act's (<see cref="StartAct.Value"/>), or an edit's (<see cref="StartEdit.Value"/>).</param>
/// <param name="Edit">A change of the config, which leaves the folder open; else an act, which hands the screen away.</param>
internal readonly record struct StartMenuRow(string Text, string Glyph, string Value, bool Edit = false, bool? Ticked = null, bool Separator = false)
{
    /// <summary>A line between two runs of rows.</summary>
    public static StartMenuRow Line => new("", "", "", Separator: true);
}

/// <summary>§641: a change of the config a Start widget asks for: <see cref="StartEdits.Apply"/>'s words.</summary>
internal readonly record struct StartEdit(string Verb, string Target, int Index = 0)
{
    public string Value => $"{Verb}{StartAct.Cut}{Target}{StartAct.Cut}{Index}";

    public static StartEdit? Of(string? value) =>
        value?.Split(StartAct.Cut) is [var verb, var target, var index] && int.TryParse(index, out var at) ? new StartEdit(verb, target, at) : null;
}

internal abstract unsafe class StartLine : CellIsland, ILinePieces, ILineTargets
{
    /// <summary>
    /// The dock's, per thread: what changes the config for a line, by its key (Dock.Start.cs); a test's writes it down. Null
    /// where nobody listens: the change is not made.
    /// </summary>
    [ThreadStatic]
    private static Action<string, StartEdit>? editor;

    internal static Action<string, StartEdit>? Editor
    {
        get => editor;
        set => editor = value;
    }

    private int seen = -1;
    private bool listening;

    /// <summary>The spot under the pointer, by its number (0: none), and the press on one.</summary>
    protected int hot;
    protected RowPress press;

    /// <summary>What a click left for the dock to do once the folder has gone.</summary>
    private StartAct? pending;

    /// <summary>The spot whose menu a click or a right click asked for (its number), or a button's word.</summary>
    protected int menuSpot;

    protected StartLine(string key, WidgetItem island, Gfx gfx, Config config)
        : base(key, island, gfx)
    {
        // §635: a ghost on the add page reads the built-in samples, never this PC's apps or files.
        Source = Samples.On ? new StartSamples() : StartSources.Now;
        Settings = island;
    }

    /// <summary>What it shows.</summary>
    internal IStartSource Source { get; }

    /// <summary>The source's now: a test's fixed one, else the clock's.</summary>
    protected DateTime NowUtc => Source is StartSamples ? StartSamples.Now : Clock.RealUtc();

    /// <summary>Nothing ticks: the source says when it has news.</summary>
    public override int IntervalMs => 0;

    protected override int Count => 1;

    public override void Configure(WidgetItem island, Config config)
    {
        Settings = island;
        Configured(config);
        Forget();
    }

    /// <summary>What a line reads of the config beyond its own record.</summary>
    protected virtual void Configured(Config config)
    {
    }

    public override string? Tooltip(int index) => null;

    /// <summary>A line of a list folder and nothing else.</summary>
    internal override void PaintCell(ID2D1DeviceContext* dc, int index, in IslandStyle style, float width, float height) =>
        throw new NotSupportedException($"{Info.Id} is a line of a folder's list.");

    /// <summary>The source has news: draw again.</summary>
    protected override bool Sample(bool everything)
    {
        var now = Source.Version;
        if (now == seen && !everything)
        {
            return false;
        }
        seen = now;
        return true;
    }

    /// <summary>Shown: what is stale is read again, and the line follows the source while it shows.</summary>
    public override void Visible(bool shown)
    {
        base.Visible(shown);
        if (shown)
        {
            if (!listening)
            {
                listening = true;
                Source.Changed += Arrived;
            }
            Source.Refresh();
            return;
        }
        Stop();
        hot = 0;
        menuSpot = 0;
        _ = press.Up();
    }

    private void Arrived() => _ = Wake();

    private void Stop()
    {
        if (listening)
        {
            listening = false;
            Source.Changed -= Arrived;
        }
    }

    public override void Dispose()
    {
        Stop();
        base.Dispose();
    }

    /// <summary>Its spots light themselves; the line does not light as a whole.</summary>
    protected internal override bool LightsItsRows => true;

    // --- The spots -----------------------------------------------------------------------------------

    /// <summary>
    /// One part of the line as it is laid out: its number (0: nothing to press), where it stands, its piece's name, what it
    /// says, and what it is of — an app, a file or a folder (<paramref name="Target"/>).
    /// </summary>
    protected internal readonly record struct Spot(int Id, D2D_RECT_F Box, string Name, string Said, StartApp? App = null, string? Target = null, bool Header = false);

    /// <summary>The line laid out in <paramref name="box"/> (its whole content where it scrolls).</summary>
    protected abstract List<Spot> Lay(D2D_RECT_F box, in TileStyle style);

    /// <summary>The spot of number <paramref name="id"/> in the last layout; null for none.</summary>
    protected Spot? SpotOf(int id) => id > 0 && laid.FirstOrDefault(spot => spot.Id == id) is { Id: > 0 } spot ? spot : null;

    private List<Spot> laid = [];

    /// <summary>The line laid out, and kept for what a click and the menu ask after.</summary>
    protected List<Spot> Laid(D2D_RECT_F box, in TileStyle style)
    {
        using var typeface = Gfx.Wearing(style.Look.Typeface);
        laid = Lay(box, style);
        return laid;
    }

    public IReadOnlyList<(int Target, D2D_RECT_F Box)> LineTargets(D2D_RECT_F box, in TileStyle style) =>
        [.. Laid(box, style).Where(spot => spot.Id > 0).Select(spot => (spot.Id, spot.Box))];

    protected internal override int CardTarget(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface) =>
        Laid(box, style).FirstOrDefault(spot => spot.Id > 0 && LineRows.Inside(spot.Box, point)).Id;

    protected internal override void CardHover(Vector2? point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var target = point is { } at ? CardTarget(at, box, style, surface) : 0;
        var let = press.Over(target);
        if (target == hot && !let)
        {
            return;
        }
        hot = target;
        // The pointer on another spot: a right click there is that spot's.
        menuSpot = target;
        _ = Wake();
    }

    protected internal override void CardDown(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        if (press.Down(CardTarget(point, box, style, surface)))
        {
            _ = Wake();
        }
    }

    /// <summary>What a name cut short says whole, under the pointer.</summary>
    protected internal override (string Text, D2D_RECT_F Part)? CardTip(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface) =>
        Laid(box, style).FirstOrDefault(spot => spot.Id > 0 && LineRows.Inside(spot.Box, point)) is { Id: > 0, Said.Length: > 0 } spot ? (spot.Said, spot.Box) : null;

    public abstract string MainPiece { get; }

    public IReadOnlyList<LinePiece> LinePieces(D2D_RECT_F box, in TileStyle style) =>
        [.. Laid(box, style).Select(spot => new LinePiece(spot.Name, spot.Box, spot.Said))];

    // --- What a click does ---------------------------------------------------------------------------

    /// <summary>Leaves <paramref name="act"/> for the dock, the folder closing first (IslandView.App).</summary>
    protected IslandAction Leave(StartAct act)
    {
        pending = act;
        return new IslandAction(View: IslandView.App);
    }

    /// <summary>The spot whose menu opens now, over the folder (IslandView.Menu): a button's own menu.</summary>
    protected IslandAction MenuOf(int spot)
    {
        menuSpot = spot;
        return new IslandAction(View: IslandView.Menu);
    }

    /// <summary>The dock, once the folder has gone: does what the click left, once.</summary>
    internal void Act()
    {
        var act = pending;
        pending = null;
        if (act is { } doing)
        {
            Run(doing);
        }
    }

    /// <summary>Tests: what a click left, not yet done.</summary>
    internal StartAct? Pending => pending;

    /// <summary>Does <paramref name="act"/>: through the one launcher, or the power buttons' own runner.</summary>
    internal void Run(StartAct act)
    {
        switch (act.Kind)
        {
            case StartActKind.Open:
                Source.Opened(act.Target);
                _ = Launch.Open(act.Target);
                break;
            case StartActKind.Admin:
                Source.Opened(act.Target);
                Launch.Elevated(StartApps.ProgramOf(act.Target) ?? act.Target);
                break;
            case StartActKind.Location:
                Launch.Select(StartApps.ProgramOf(act.Target) ?? act.Target);
                break;
            case StartActKind.Place:
                _ = Launch.Open(act.Target);
                break;
            case StartActKind.Power when Enum.TryParse<QuickLink>(act.Target, out var link) && QuickLinks.IsPower(link):
                PowerButtonTile.Run(link);
                break;
        }
    }

    /// <summary>Asks the dock to change the config for this line (<see cref="Editor"/>).</summary>
    protected void Edit(StartEdit edit) => Editor?.Invoke(Key, edit);

    // --- Its own menu --------------------------------------------------------------------------------

    /// <summary>The rows of its menu for the spot it is for (<see cref="menuSpot"/>, or the keys'), none for none.</summary>
    internal abstract IReadOnlyList<StartMenuRow> MenuRows();

    /// <summary>The spot the menu is for: the one asked for, else the one under the pointer.</summary>
    protected Spot? MenuSpot => SpotOf(menuSpot != 0 ? menuSpot : hot);

    /// <summary>A menu's row picked (Dock.Start.cs): an act left and done at once, or a change asked for.</summary>
    internal void MenuPicked(string value)
    {
        if (StartAct.Of(value) is { } act)
        {
            Run(act);
        }
        else if (StartEdit.Of(value) is { } edit)
        {
            Edit(edit);
        }
    }

    /// <summary>An app's rows, as Start's right click has them: open, run as administrator, its file's place; then the pins'.</summary>
    protected List<StartMenuRow> AppRows(StartApp app, bool pinned, bool pinnable, bool moves)
    {
        List<StartMenuRow> rows = [new(S.DockWidgetMenuOpen, Glyphs.Open, new StartAct(StartActKind.Open, app.Target).Value)];
        if (app.Program is not null)
        {
            rows.Add(new(S.DockWidgetMenuRunAsAdministrator, Glyphs.Admin, new StartAct(StartActKind.Admin, app.Target).Value));
            rows.Add(new(S.DockWidgetMenuOpenFileLocation, Glyphs.Location, new StartAct(StartActKind.Location, app.Target).Value));
        }
        if (pinnable || pinned)
        {
            rows.Add(StartMenuRow.Line);
            rows.Add(pinned
                ? new(S.DockStartUnpin, Glyphs.Unpin, new StartEdit(StartEdits.Unpin, app.Target).Value, Edit: true)
                : new(S.DockStartPin, Glyphs.Pin, new StartEdit(StartEdits.Pin, app.Target).Value, Edit: true));
            if (pinned && moves)
            {
                rows.Add(new(S.DockStartToFront, Glyphs.Front, new StartEdit(StartEdits.Front, app.Target).Value, Edit: true));
            }
        }
        rows.Add(StartMenuRow.Line);
        // Windows' own uninstall: Settings' Installed apps, where every app is removed the one way.
        rows.Add(new(S.DockStartUninstall, Glyphs.Uninstall, new StartAct(StartActKind.Place, "ms-settings:appsfeatures").Value));
        return rows;
    }

    // --- Drawing --------------------------------------------------------------------------------------

    /// <summary><paramref name="target"/>'s icon in <paramref name="box"/>, else <paramref name="glyph"/>, faint while it comes.</summary>
    protected void Icon(ID2D1DeviceContext* dc, string target, string glyph, D2D_RECT_F box, in TileStyle style)
    {
        var side = MathF.Min(box.right - box.left, box.bottom - box.top);
        if (Source.Icon(target, side, Arrived) is { } pixels)
        {
            Gfx.DrawPixels(dc, pixels, PopupPaint.Round(box.left + (((box.right - box.left) - side) / 2)), PopupPaint.Round(box.top + (((box.bottom - box.top) - side) / 2)), side);
            return;
        }
        PopupPaint.Glyph(dc, Gfx, glyph, MathF.Min(box.right - box.left, box.bottom - box.top) * 0.75f, style.Theme.TextSecondary, box);
    }

    /// <summary>A section's heading ("Pinned", "Recommended"): Body Strong at the line's gutter.</summary>
    protected void Heading(ID2D1DeviceContext* dc, string text, D2D_RECT_F band, in TileStyle style, float room)
    {
        var px = style.Text(14);
        var tall = TilePaint.LineHeight(Gfx, px, Weight.Semibold);
        _ = TilePaint.Line(dc, Gfx, text, px, Weight.Semibold, style.Theme.Text, band.left, PopupPaint.Round(band.top + ((band.bottom - band.top - tall) / 2)), room);
    }

    /// <summary>
    /// A spot lit: the subtle fill under the pointer, pressed while held. An item — a tile, a row, the account — is the kit's
    /// (§428, §661): its band, square, edge to edge as the line laid it; a <paramref name="button"/> its own rounded plate.
    /// </summary>
    protected void Plate(ID2D1DeviceContext* dc, in Spot spot, in TileStyle style, bool button = false)
    {
        if (spot.Id == 0 || spot.Id != hot)
        {
            return;
        }
        if (button)
        {
            PopupPaint.Fill(dc, spot.Box, style.Px(4), PopupPaint.Ink(press.On(spot.Id) ? style.Theme.SubtlePressed : style.Theme.SubtleHover));
            return;
        }
        LineItem.Light(dc, spot.Box, style.Theme, press.On(spot.Id));
    }

    /// <summary>Words a line says where it has nothing to show, in the secondary text, in its middle.</summary>
    protected void Nothing(ID2D1DeviceContext* dc, string text, D2D_RECT_F band, in TileStyle style)
    {
        var px = style.Text(12);
        var tall = TilePaint.LineHeight(Gfx, px);
        _ = TilePaint.Centred(dc, Gfx, text, px, Weight.Regular, style.Theme.TextSecondary, band.left, PopupPaint.Round(band.top + ((band.bottom - band.top - tall) / 2)), band.right - band.left);
    }

    /// <summary>The Segoe Fluent Icons glyphs of Start's menus and buttons.</summary>
    internal static class Glyphs
    {
        public const string Open = "";
        public const string Admin = "";
        public const string Location = "";
        public const string Pin = "";
        public const string Unpin = "";
        public const string Front = "";
        public const string Uninstall = "";
        public const string App = "";
        public const string File = "";
        public const string Power = "";
        public const string Person = "";
        public const string Lock = "";
        public const string SignOut = "";
        public const string SwitchUser = "";
        public const string Account = "";
        public const string Sleep = "";
        public const string Hibernate = "";
        public const string Restart = "";
        public const string Update = "";
        public const string Chevron = "";
        public const string View = "";
        public const string Category = "";
        public const string Grid = "";
        public const string List = "";
    }
}

/// <summary>§641: an app's program, where the Apps folder names one by a path.</summary>
internal static class StartApps
{
    public static string? ProgramOf(string target) => new StartApp("", target).Program;
}
