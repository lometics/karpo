// perf: the processes busiest on the processor now, as a line of a folder's list (docs\widgets.md
// §141; 2026-10-12: "for the processor add top running processes by cpu as a widget as well") —
// Task Manager's processes sorted by CPU, in the processor's preset under its graph and its details.
//
// **A row per app**, its processes together as Task Manager's Apps group them (ProcessTop): its icon,
// its name — its file's description, "Google Chrome" — with how many processes it is, a thin bar of
// its share of the whole processor under the name, and the share at the row's end to a tenth of a
// percent, quieter, in a column as wide as "100.0%", so nothing moves as the figures change. As many rows as
// the line holds, a list row each as the item style is (32, 40 or 48 DIP); the line is as tall as
// the most it may be, since there are always more processes than rows, and never scrolls.
//
// **Read only while it shows**: every two seconds while the folder is open (IntervalMs), nothing
// while it is shut. A look is two readings apart, so the line keeps the last reading it took — any
// line's — and opened within a minute of it, shows figures at once over that span; opened later, it
// reads, and its first figures come half a second on.
//
// **A click** anywhere on it opens Task Manager, as the graph's does; Up and Down walk the rows and
// Enter is the click. **A row's right click** (Dock.WidgetMenus.cs) names the app and offers "End
// task" — after Windows' own question (Dock.EndProcesses), greyed where Karpo could end none of its
// processes (an elevated app, Windows' own) — and "Open file location".

using System.Numerics;
using Karpo.Core;
using Karpo.Dock.Performance;
using Karpo.Dock.QuickSettings.Controls;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;

namespace Karpo.Dock;

/// <summary>What an app of the processes' line is called and looks like, and where its file is (null for none).</summary>
internal sealed record ProcessName(string Name, Pixels? Logo, string? Path);

internal sealed unsafe class ProcessesLine : CellIsland, IListContent
{
    /// <summary>How often it reads while it shows, ms.</summary>
    internal const int EveryMs = 2000;

    /// <summary>How soon its first figures come where it had to take its first reading, ms.</summary>
    internal const int FirstMs = 500;

    /// <summary>The least time between two readings worth a figure, in 100 ns: less is noise.</summary>
    private const long LeastSpan = 2_500_000;

    /// <summary>How old the last reading may be and still be what a line opened now measures from, in 100 ns.</summary>
    private const long KeptSpan = 600_000_000;

    /// <summary>Segoe Fluent Icons: an app with no icon of its own.</summary>
    private const string AppGlyph = "";

    /// <summary>The most rows the line ever measures for.</summary>
    private const int MostRows = 32;

    // The last reading any line took: where a line opened soon after measures from.
    private static ProcessLook? latest;

    private readonly Dictionary<string, ProcessName> names = new(StringComparer.Ordinal);
    private ProcessLook? since;
    private List<ProcessGroup> ranked = [];

    // How many rows the line drew last: what the keys walk.
    private int drawn;

    // What is under the pointer (a row's place plus one), the press on it. The keys' row is the folder's (KeyedPart).
    private int hot;
    private RowPress press;

    public ProcessesLine(string key, WidgetItem island, Gfx gfx, Config config)
        : base(key, island, gfx)
    {
        Configure(island, config);
    }

    /// <summary>Where the readings come from: Windows', or a test's stand-in.</summary>
    internal static Func<ProcessLook?> Look { get; set; } = ProcessReader.Read;

    /// <summary>How an app is named: its file's description and icon, or a test's stand-in.</summary>
    internal static Func<ProcessGroup, ProcessName> Names { get; set; } = Resolve;

    /// <summary>What a click opens: Task Manager, through the shell; a test writes it down.</summary>
    internal static Action<string> Opener { get; set; } = target => Launch.Open(target);

    /// <summary>
    /// The app a row's menu offered to end, kept from the menu's making to its pick
    /// (Dock.WidgetMenus.cs, Dock.EndProcesses): the processes it listed then.
    /// </summary>
    internal static ProcessGroup? Asked { get; set; }

    /// <summary>Forgets the last reading every line measures from: a test's.</summary>
    internal static void ForgetLatest() => latest = null;

    /// <summary>Every two seconds while it shows; half a second while its first figures wait.</summary>
    public override int IntervalMs => ranked.Count == 0 ? FirstMs : EveryMs;

    protected override int Count => 1;

    public override void Configure(WidgetItem island, Config config) => Settings = island;

    public override string? Tooltip(int index) => null;

    internal override void PaintCell(ID2D1DeviceContext* dc, int index, in IslandStyle style, float width, float height) =>
        throw new NotSupportedException($"{Info.Id} is a line of a folder's list and nothing else.");

    /// <summary>Rows light themselves; the line does not light as a whole.</summary>
    protected internal override bool LightsItsRows => true;

    /// <summary>The apps as last measured, busiest first.</summary>
    internal IReadOnlyList<ProcessGroup> Ranked => ranked;

    /// <summary>
    /// Shown: measured from the last reading where it is recent, else from one taken now — whose
    /// figures come at the next tick. Hidden: nothing is read until it shows again.
    /// </summary>
    public override void Visible(bool shown)
    {
        base.Visible(shown);
        if (!shown)
        {
            since = null;
            hot = 0;
            _ = press.Up();
            return;
        }
        var now = Look();
        if (latest is { } last && now is not null && now.At - last.At <= KeptSpan)
        {
            since = last;
            Measure(now);
        }
        else
        {
            ranked = [];
            since = now;
            latest = now;
        }
    }

    /// <summary>A reading, where the last is far enough back to be worth one; the apps measured over the two.</summary>
    protected override bool Sample(bool everything)
    {
        if (since is null)
        {
            since = latest = Look();
            return false;
        }
        var now = Look();
        if (now is null || now.At - since.At < LeastSpan)
        {
            return false;
        }
        Measure(now);
        return false;
    }

    private void Measure(ProcessLook now)
    {
        if (since is not { } before || now.At - before.At < LeastSpan)
        {
            return;
        }
        ranked = ProcessTop.Rank(before, now);
        since = now;
        latest = now;
    }

    /// <summary>What <paramref name="group"/> is called and looks like: looked up once per app.</summary>
    internal ProcessName NameOf(ProcessGroup group)
    {
        if (names.TryGetValue(group.Key, out var known))
        {
            return known;
        }
        ProcessName made;
        try
        {
            made = Names(group);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            made = new ProcessName(ProcessTop.StemOf(group.Image), null, null);
        }
        if (names.Count >= 256)
        {
            names.Clear();
        }
        names[group.Key] = made;
        return made;
    }

    /// <summary>Windows' answer: the file's description and icon where its path is known; its image's name else.</summary>
    private static ProcessName Resolve(ProcessGroup group)
    {
        var fallback = ProcessTop.StemOf(group.Image);
        if (ProcessReader.PathOf(group) is not { } path)
        {
            return new ProcessName(fallback.Length > 0 ? fallback : group.Image, null, null);
        }
        var info = ScreenTimeNames.Of(path);
        return new ProcessName(string.IsNullOrWhiteSpace(info.Name) ? fallback : info.Name, info.Logo, path);
    }

    /// <summary>A row's words: the app's name, with how many processes it is where it is more than one.</summary>
    internal string Title(ProcessGroup group)
    {
        var name = NameOf(group).Name;
        return group.Count > 1 ? S.WidgetProcessesNamed(name: name, count: group.Count) : name;
    }

    /// <summary>The app under the pointer, or under the keys: what a right click acts on; null for none.</summary>
    internal ProcessGroup? MenuGroup =>
        (hot > 0 ? hot - 1 : KeyedPart - 1) is var at && at >= 0 && at < Math.Min(drawn, ranked.Count) ? ranked[at] : null;

    // --- Laid out -----------------------------------------------------------------------------------

    /// <summary>A list row — 32, 40 or 48 DIP as the item style is: one app's.</summary>
    private float RowHeight(in TileStyle style) =>
        PopupPaint.Round(ItemStyles.Of(Settings).Size switch { ItemSize.Compact => 32, ItemSize.Large => 48, _ => 40 } * style.Scale * style.TextScale);

    /// <summary>As tall as the most a line may be: there are always more apps than rows.</summary>
    public float ListHeight(float width, in TileStyle style) => MostRows * RowHeight(style);

    /// <summary>How many rows <paramref name="height"/> holds: whole rows, one at the least.</summary>
    internal int RowsIn(float height, in TileStyle style)
    {
        var row = RowHeight(style);
        return Math.Max(1, (int)MathF.Floor((height + 0.5f) / row));
    }

    /// <summary>One app's row as laid out: <see cref="Box"/> its band (LineItem), what lights and takes a press.</summary>
    internal readonly record struct Row(int Index, ProcessGroup Group, D2D_RECT_F Box, D2D_RECT_F Icon, string Name, float NameTop, float NameRoom, string Figure, float FigureTop, D2D_RECT_F Bar);

    /// <summary>The rows the line holds, from the top: as many apps as there are, up to whole rows of it.</summary>
    internal List<Row> Plan(D2D_RECT_F box, in TileStyle style)
    {
        var rows = new List<Row>();
        if (ranked.Count == 0)
        {
            return rows;
        }
        var form = ListForm.Of(Gfx, style);
        var items = ItemForm.For(Gfx, style, Settings);
        var row = RowHeight(style);
        var count = Math.Min(RowsIn(TilePaint.Height(box), style), ranked.Count);
        var left = box.left + form.TextLeft;
        var right = box.right - form.Inset;
        var field = PopupPaint.Measure(Gfx, ProcessTop.PercentRoom, items.SmallPx, Weight.Regular).X;
        var textRight = right - field - style.Gap;
        var bar = MathF.Max(2, style.Px(3));
        var air = style.Px(4);
        var logo = MathF.Min(form.Icon, PopupPaint.Round(24 * style.Scale * style.TextScale));
        var withBar = items.Line + air + bar <= row - (2 * style.Px(4));
        var block = items.Line + (withBar ? air + bar : 0);
        for (var index = 0; index < count; index++)
        {
            var group = ranked[index];
            var y = box.top + (index * row);
            var top = y + PopupPaint.Round((row - block) / 2);
            var icon = form.IconBox(box.left, y, row);
            rows.Add(new Row(
                index,
                group,
                LineItem.Band(box, y, y + row),
                PopupPaint.Rect(icon.left + PopupPaint.Round((icon.right - icon.left - logo) / 2), y + PopupPaint.Round((row - logo) / 2), logo, logo),
                Title(group),
                top,
                MathF.Max(1, textRight - left),
                ProcessTop.Percent(group.Share),
                top + PopupPaint.Round((items.Line - items.SmallLine) / 2),
                withBar ? PopupPaint.Rect(left, top + items.Line + air, MathF.Max(1, textRight - left), bar) : default));
        }
        return rows;
    }

    // --- Clicks -------------------------------------------------------------------------------------

    /// <summary>The row at <paramref name="point"/>: its place plus one, 0 for none.</summary>
    protected internal override int CardTarget(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        foreach (var row in Plan(box, style))
        {
            if (LineRows.Inside(row.Box, point))
            {
                return row.Index + 1;
            }
        }
        return 0;
    }

    /// <summary>Task Manager, wherever the click was: the processes are its to show in full.</summary>
    public override IslandAction Click(int index)
    {
        _ = press.Up();
        Opener(SettingsActions.TaskManagerTarget);
        return default;
    }

    // --- The pointer --------------------------------------------------------------------------------

    protected internal override void CardHover(Vector2? point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var target = point is { } at ? CardTarget(at, box, style, surface) : 0;
        var let = press.Over(target);
        if (target == hot && !let)
        {
            return;
        }
        hot = target;
        _ = Wake();
    }

    /// <summary>What the pointer is over now (a row's place plus one), for a test.</summary>
    internal int Hot => hot;

    /// <summary>A press on a row lights it pressed; the press stays the folder's, which makes it the click.</summary>
    protected internal override void CardDown(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        if (press.Down(CardTarget(point, box, style, surface)))
        {
            _ = Wake();
        }
    }

    // --- Drawn --------------------------------------------------------------------------------------

    protected internal override void PaintCard(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var theme = QuickTheme.For(style.Theme);
        var form = ListForm.Of(Gfx, style);
        var items = ItemForm.For(Gfx, style, Settings);
        var rows = Plan(box, style);
        drawn = rows.Count;
        var radius = style.Px(4);
        var track = TilePaint.Track(style);
        var accent = TilePaint.Accent(style);
        foreach (var row in rows)
        {
            var lit = hot == row.Index + 1;
            if (lit)
            {
                LineItem.Light(dc, row.Box, theme, press.On(row.Index + 1));
            }
            if (NameOf(row.Group).Logo is { } logo)
            {
                PopupPaint.Picture(dc, logo, row.Icon, 0);
            }
            else
            {
                PopupPaint.Glyph(dc, Gfx, AppGlyph, form.Glyph, theme.TextSecondary, form.IconBox(row.Box.left, row.Box.top, row.Box.bottom - row.Box.top));
            }
            var left = row.Box.left + form.TextLeft;
            TilePaint.Line(dc, Gfx, row.Name, items.TextPx, Weight.Regular, theme.Text, left, row.NameTop, row.NameRoom);
            TilePaint.LineRight(dc, Gfx, row.Figure, items.SmallPx, Weight.Regular, theme.TextSecondary, row.Box.right - form.Inset, row.FigureTop,
                PopupPaint.Measure(Gfx, ProcessTop.PercentRoom, items.SmallPx, Weight.Regular).X);
            if (row.Bar.right > row.Bar.left)
            {
                TilePaint.Bar(dc, row.Bar, row.Group.Share, track, accent);
            }
        }
    }

    // --- Words for the pointer ----------------------------------------------------------------------

    /// <summary>A name cut at the row's end, whole.</summary>
    protected internal override (string Text, D2D_RECT_F Part)? CardTip(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        foreach (var row in Plan(box, style))
        {
            if (LineRows.Inside(row.Box, point))
            {
                var px = ItemForm.For(Gfx, style, Settings).TextPx;
                return PopupPaint.Measure(Gfx, row.Name, px, Weight.Regular).X > row.NameRoom ? (row.Name, row.Box) : null;
            }
        }
        return null;
    }
}
