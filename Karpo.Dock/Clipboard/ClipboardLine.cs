// clipboard: what was copied lately, as a line of a folder's list (docs\widgets.md §101). It was the
// clipboard history entry's flyout, modelled on Windows 11's own Win+V panel, until that entry
// became the clipboard preset (2026-10-10): a folder wearing the entry's glyph and holding this line,
// which does what the flyout did.
//
// **What the flyout did, the line does.** Its header: the clipboard's glyph, how many items there
// are, and "Clear all" at its end, which keeps the pinned ones as Win+V's does. While Karpo's own
// history shows rather than Windows' (ClipboardStore.Note), why, under the header, and where it is
// Windows' history being off, "Turn on", which opens Windows' clipboard settings where it is switched
// on — the flyout's footer note and link. Then a row per item, the newest first: text in its first
// three lines, a picture as its thumbnail; a pinned one wears the pin on the trailing gutter, where
// its delete stands. Under the pointer (or the keys) a row lights and shows its pin and its delete at its end — the pin
// only where the history can be pinned (Karpo's own; Windows gives a program no way to). Room for
// them is kept whether they show or not, so nothing reflows as the pointer moves.
//
// **Rows, not the flyout's cards**, as the notification line's are: the line stands on the folder's
// card already, and a folder's lines are rows that light under the pointer (LightsItsRows). **They
// stand on the list's gutter**, as the mail's and the news' do (2026-10-11: "the clipboard items have a
// little unnecessary space on the left"): the words from the gutter to the buttons on the trailing
// one, the plate reaching a little past both; the leading column stood empty before every item.
//
// **A click copies, and says so** (2026-10-11). The item goes on the clipboard (ClipboardStore.Choose)
// and the folder stays: the row wears the accent's check and "Copied" at its end for a moment
// (CopiedMs), where its pin and delete stand under the pointer, then is as it was. It pasted, as
// Win+V's click does, closing the folder first — and nothing on the screen said what had happened.
//
// **The keys are the folder's** (§471): "Clear all", "Turn on", every row and its pin and delete are its
// stops (LineFocus, from the targets laid out as one list: LineTargets), the ring the folder's round the one
// they are on, and the row they are on lit with its buttons showing (KeyedPart).
//
// **The keys paste, as Win+V's do.** Chosen with Enter or Space, the item goes on the clipboard, the
// folder closes — giving the focus back to the window in front before it opened (FlyoutHost's Menu
// mode) — and the dock then pastes it there once that window's focus has settled (ClipboardPaste,
// PasteWait, on a timer of the dock's, never waiting on its thread). That is the folder's click that
// acts (CellAct.Action, IslandView.App): the folder forwards it once it has gone, and the dock asks
// this line whether a paste is due (TakePaste). The line keeps the key until it is let go
// (CardKeyUp) — its repeats and its character are the folder's — and only then does the folder
// close, so none of it reaches the window pasted into. Pin, delete, "Clear all" and "Turn on" act in
// place, and the folder stays (Settings, opened, takes the focus and closes it).
//
// **A line of every height.** On one row: the glyph, the count and "Clear all". Taller: the header,
// the note where there is one, and every item from the top, scrolling under the header where there are
// more than the line holds (ListScroll.cs), in the item style chosen (ItemForm): their size, a text's
// further lines and a picture's thumbnail. Its height follows its items (IListContent).
//
// **What it reads.** The one store (ClipboardStore.Shared) the face hears the clipboard through; a
// test's stand-in (Stand), so no test reads or writes the person's clipboard. Windows' history is
// read afresh as the folder opens (Visible), as the flyout read it as it opened, and the line follows
// the store's news while it shows.

using System.Numerics;
using Karpo.Core;
using Karpo.Dock.QuickSettings.Controls;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;

namespace Karpo.Dock;

internal sealed unsafe class ClipboardLine : CellIsland, IListContent, IListScroll, ILinePieces, ILineTargets
{
    /// <summary>Segoe Fluent Icons: Pin, PinFill, Delete, and Photo for a picture that could not be decoded.</summary>
    internal const string PinGlyph = "";
    internal const string PinnedGlyph = "";
    internal const string DeleteGlyph = "";
    internal const string PictureGlyph = "";

    /// <summary>Segoe Fluent Icons: CheckMark, beside "Copied" on a row just copied.</summary>
    internal const string CopiedGlyph = "";

    /// <summary>The settings page where Windows' clipboard history is turned on.</summary>
    internal const string SettingsPage = "ms-settings:clipboard";

    /// <summary>The tallest a picture is drawn, in DIPs at 100 % text.</summary>
    internal const float PictureDip = 96;

    /// <summary>The most lines the note under the header takes.</summary>
    private const int NoteLines = 3;

    [ThreadStatic]
    private static ClipboardStore? stand;

    /// <summary>
    /// tests: the store a line the registry makes reads, and the clipboard face, in this thread's place
    /// of <see cref="ClipboardStore.Shared"/>; null for the shared one, which is what the dock uses.
    /// </summary>
    internal static ClipboardStore? Stand
    {
        get => stand;
        set => stand = value;
    }

    /// <summary>The store a line or a face made on this thread reads: the test's, else the dock's.</summary>
    internal static ClipboardStore StoreNow => Stand ?? ClipboardStore.Shared;

    [ThreadStatic]
    private static Action<string>? opener;

    /// <summary>What opens Windows' clipboard settings, per thread: a test's, which writes it down; null for Windows'.</summary>
    internal static Action<string>? Opener
    {
        get => opener;
        set => opener = value;
    }

    private readonly ClipboardStore store;
    private bool listening;

    // What is under the pointer and the number the folder was given for it; the ids given out.
    private Target hot;
    private int hotId;
    private readonly List<Target> given = [];

    // The key whose press chose the row the folder's keys are on (KeyedPart), held until let go; 0 for none.
    private int holding;

    // A chosen item is on the clipboard, for the dock to paste once the folder has gone.
    private bool pasteDue;

    // The target a press is down on, lit pressed; the item a click copied (its id) and until when
    // (Ticks) its row says so.
    private RowPress press;
    private string? copied;
    private long copiedUntil;

    /// <summary>How long a row says "Copied" after its click, in ms.</summary>
    internal const int CopiedMs = 1500;

    /// <summary>tests: the clock "Copied" is timed by, in ms; Windows' tick count by default.</summary>
    internal Func<long> Ticks { get; set; } = static () => Environment.TickCount64;

    /// <summary>tests: what the row's "Copied" being over does; the dock's line is drawn again.</summary>
    internal Action<Action, int> After { get; set; } = static (then, ms) =>
        _ = Task.Delay(ms).ContinueWith(
            _ => Troubles.Guard("Clipboard: copied said", then), // not-ui: a trouble's name
            CancellationToken.None,
            TaskContinuationOptions.None,
            TaskScheduler.Default);

    public ClipboardLine(string key, WidgetItem island, Gfx gfx, Config config)
        : this(key, island, gfx, config, StoreNow)
    {
    }

    /// <summary>A line on <paramref name="store"/>: the dock's is the shared one; tests bring their own.</summary>
    internal ClipboardLine(string key, WidgetItem island, Gfx gfx, Config config, ClipboardStore store)
        : base(key, island, gfx)
    {
        this.store = store;
        Configure(island, config);
        // Standing in a folder is what starts the clipboard being heard where Karpo keeps its own.
        store.Attach(this);
    }

    /// <summary>The store it reads.</summary>
    internal ClipboardStore Store => store;

    /// <summary>menus: everything but what is pinned gone, as the line's own "Clear all" (Dock.WidgetMenus.cs).</summary>
    internal void ClearAll()
    {
        store.ClearAll();
        Rest();
    }

    /// <summary>Never ticks: the store wakes it when the history has news.</summary>
    public override int IntervalMs => 0;

    protected override int Count => 1;

    public override void Configure(WidgetItem island, Config config)
    {
        Settings = island;
        Forget();
    }

    /// <summary>Nothing of its own: its rows say it all.</summary>
    public override string? Tooltip(int index) => null;

    /// <summary>Never drawn on a bar: it is a line of a list and nothing else; on the bar it is the preset's face.</summary>
    internal override void PaintCell(ID2D1DeviceContext* dc, int index, in IslandStyle style, float width, float height) =>
        throw new NotSupportedException($"{Info.Id} is a line of a folder's list, not a widget of a bar.");

    // --- The showing --------------------------------------------------------------------------------

    /// <summary>
    /// The folder opened: Windows' history is read afresh, as the flyout read it as it opened, and the
    /// line follows the store while it shows. Closed, what the pointer and the keys were on goes.
    /// </summary>
    public override void Visible(bool shown)
    {
        base.Visible(shown);
        if (shown)
        {
            if (!listening)
            {
                listening = true;
                store.Changed += Arrived;
            }
            store.Refresh();
            return;
        }
        if (listening)
        {
            listening = false;
            store.Changed -= Arrived;
        }
        hot = default;
        hotId = 0;
        holding = 0;
        _ = press.Up();
        copied = null;
    }

    public override void Dispose()
    {
        if (listening)
        {
            listening = false;
            store.Changed -= Arrived;
        }
        store.Release(this);
        base.Dispose();
    }

    /// <summary>The store posted: it raises Changed, and the folder draws the line again.</summary>
    protected override bool Sample(bool everything)
    {
        if (everything)
        {
            store.Deliver();
        }
        return false;
    }

    /// <summary>The store has news, on the dock's thread: the line is laid out again, its height with it.</summary>
    private void Arrived() => Redrawn();

    // --- Clicks -------------------------------------------------------------------------------------

    /// <summary>What a press lands on.</summary>
    internal enum Spot
    {
        None,
        Clear,
        TurnOn,
        Row,
        Pin,
        Delete,
    }

    /// <summary>A thing a click acts on: what it is, and for a row and its buttons the item, by its id — never by its place.</summary>
    internal readonly record struct Target(Spot Spot, string Item = "");

    /// <summary>
    /// What the click the hit test found does; with no part (the keys: Enter or Space let go) the row
    /// the keys are on. A row puts its item on the clipboard and asks the dock to paste it once the
    /// folder has gone; the rest act in place.
    /// </summary>
    public override IslandAction Click(int index)
    {
        _ = press.Up();
        Target target;
        if (index == 0)
        {
            // The keys' Enter or Space let go on a row: that row.
            if (TargetOf(KeyedPart) is not { Spot: Spot.Row } row)
            {
                return default;
            }
            target = row;
        }
        else if (index == KeyedPart)
        {
            // The keys' Enter or Space on a button (the folder's stop, FolderFlyout.Keys.cs).
            target = TargetOf(index);
        }
        else
        {
            // The pointer's: what a folder being arranged takes away (it tells the line it is nowhere).
            if (index != hotId)
            {
                return default;
            }
            target = hot;
        }
        switch (target.Spot)
        {
            case Spot.Clear:
                store.ClearAll();
                Rest();
                return default;
            case Spot.TurnOn:
                Open(SettingsPage);
                return default;
        }
        if (Find(target.Item) is not { } item)
        {
            return default;
        }
        switch (target.Spot)
        {
            case Spot.Pin:
                store.Pin(item, !item.Pinned);
                Rest();
                return default;
            case Spot.Delete:
                store.Delete(item);
                Rest();
                return default;
            // The keys' choice pastes where the person was, the folder gone first.
            case Spot.Row when index == 0 && store.Choose(item):
                pasteDue = true;
                return new IslandAction(View: IslandView.App);
            // The pointer's copies, and the row says so for a moment; the folder stays.
            case Spot.Row when store.Choose(item):
                Copied(item.Id);
                return default;
            default:
                Redrawn();
                return default;
        }
    }

    /// <summary>Item <paramref name="id"/> was copied: its row says so until <see cref="CopiedMs"/> have gone, then is drawn again.</summary>
    private void Copied(string id)
    {
        copied = id;
        copiedUntil = Ticks() + CopiedMs;
        Redrawn();
        After(() => _ = Wake(), CopiedMs + 20);
    }

    /// <summary>Whether item <paramref name="id"/>'s row says "Copied" now.</summary>
    internal bool SaysCopied(string id) => copied == id && Ticks() < copiedUntil;

    /// <summary>
    /// The dock, once the folder has gone: whether a chosen item waits to be pasted where the person
    /// was — asked once, and forgotten as it is told.
    /// </summary>
    internal bool TakePaste()
    {
        var due = pasteDue;
        pasteDue = false;
        return due;
    }

    /// <summary>Opens a page of Windows' settings: it takes the focus, and the folder closes for it.</summary>
    private static void Open(string uri)
    {
        if (Opener is { } test)
        {
            test(uri);
            return;
        }
        _ = Launch.Open(uri);
    }

    /// <summary>Something went from under the pointer: nothing is lit until it moves, and the line is laid out again.</summary>
    private void Rest()
    {
        hot = default;
        hotId = 0;
        Redrawn();
    }

    /// <summary>The item <paramref name="id"/> names, where it is still there.</summary>
    private ClipView? Find(string id) => store.Items.FirstOrDefault(item => item.Id == id);

    // --- The keys -----------------------------------------------------------------------------------

    // §471: the folder's keys walk the line (LineFocus): "Clear all", "Turn on", every row and its pin and delete are
    // stops, the ring the folder's round the one they are on. The line lights the row they are on, its buttons
    // showing (KeyedPart), and keeps a row's Enter or Space until it is let go.

    /// <summary>keys: the row the folder's keys are on, or whose button they are on, is lit, its buttons showing.</summary>
    protected internal override bool LightsKeyedPart => true;

    /// <summary>The item the folder's keys are on, its row or one of its buttons; null for none.</summary>
    private string? KeyedItem => TargetOf(KeyedPart) is { Spot: Spot.Row or Spot.Pin or Spot.Delete } on ? on.Item : null;

    /// <summary>
    /// Enter or Space on a row the folder's keys are on takes the key, which chooses the row once it is let go
    /// (<see cref="CardKeyUp"/>); while it is held so, every key is kept here. On a button, the folder's: its click.
    /// </summary>
    protected internal override bool CardKey(int key, WidgetSurface surface)
    {
        if (holding != 0)
        {
            return true;
        }
        if (key is VK.VK_RETURN or VK.VK_SPACE && TargetOf(KeyedPart).Spot == Spot.Row)
        {
            holding = key;
            return true;
        }
        return false;
    }

    /// <summary>The key that chose a row is let go: the folder clicks it now (<see cref="Click"/> with no part).</summary>
    protected internal override bool CardKeyUp(int key, WidgetSurface surface)
    {
        if (holding == 0 || key != holding)
        {
            return false;
        }
        holding = 0;
        return TargetOf(KeyedPart).Spot == Spot.Row;
    }

    // --- The pointer ----------------------------------------------------------------------------------

    /// <summary>Only a row and the header's buttons light under the pointer, never the line around them.</summary>
    protected internal override bool LightsItsRows => true;

    protected internal override void CardHover(Vector2? point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var target = point is { } at ? LineTarget(at, box, style) : default;
        var let = press.Over(IdOf(target));
        if (target == hot && !let)
        {
            return;
        }
        hot = target;
        hotId = IdOf(target);
        _ = Wake();
    }

    /// <summary>A press on a row or a button lights it pressed; the press stays the folder's, which makes it the click (RowPress).</summary>
    protected internal override void CardDown(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        if (press.Down(CardTarget(point, box, style, surface)))
        {
            _ = Wake();
        }
    }

    protected internal override int CardTarget(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface) =>
        IdOf(LineTarget(point, box, style));

    /// <summary>A row's buttons name themselves, being glyphs alone.</summary>
    protected internal override (string Text, D2D_RECT_F Part)? CardTip(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        foreach (var (target, at) in Targets(box, style))
        {
            if (target.Spot is Spot.Pin or Spot.Delete && LineRows.Inside(at, point))
            {
                var pinned = Find(target.Item)?.Pinned == true;
                return (target.Spot == Spot.Delete ? S.DockClipboardDelete : pinned ? S.DockClipboardUnpin : S.DockClipboardPin, at);
            }
        }
        return null;
    }

    /// <summary>The number the folder carries for <paramref name="target"/> to <see cref="Click"/>: 0 for nothing.</summary>
    private int IdOf(Target target)
    {
        if (target.Spot == Spot.None)
        {
            return 0;
        }
        var at = given.IndexOf(target);
        if (at < 0)
        {
            given.Add(target);
            at = given.Count - 1;
        }
        return at + 1;
    }

    /// <summary>tests: what a number <see cref="CardTarget"/> gave stands for.</summary>
    internal Target TargetOf(int id) => id > 0 && id <= given.Count ? given[id - 1] : default;

    /// <summary>What a click on the line lands on, where: the buttons before the rows they stand on, so the first a point is in is its target.</summary>
    private List<(Target Target, D2D_RECT_F Box)> Targets(D2D_RECT_F box, in TileStyle style)
    {
        var targets = new List<(Target Target, D2D_RECT_F Box)>();
        Lay(new LineSketch(null, Gfx), box, style, targets);
        return [.. targets.Where(target => target.Target.Spot != Spot.Row), .. targets.Where(target => target.Target.Spot == Spot.Row)];
    }

    /// <summary>What is at <paramref name="point"/> on a line: a button before the row it stands on.</summary>
    internal Target LineTarget(Vector2 point, D2D_RECT_F box, in TileStyle style) =>
        Targets(box, style).FirstOrDefault(target => LineRows.Inside(target.Box, point)).Target;

    /// <summary>keys: every button and row of the line, the folder's stops (LineFocus).</summary>
    public IReadOnlyList<(int Target, D2D_RECT_F Box)> LineTargets(D2D_RECT_F box, in TileStyle style) =>
        [.. Targets(box, style).Select(target => (IdOf(target.Target), target.Box))];

    // --- Drawn ----------------------------------------------------------------------------------------

    protected internal override void PaintCard(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var sketch = new LineSketch(dc, Gfx);
        Lay(sketch, box, style, null);
    }

    /// <summary>What a line of one row shows first: how many items there are.</summary>
    public string MainPiece => "count";

    public IReadOnlyList<LinePiece> LinePieces(D2D_RECT_F box, in TileStyle style)
    {
        var sketch = new LineSketch(null, Gfx);
        Lay(sketch, box, style, null);
        return sketch.Pieces;
    }

    /// <summary>The sizes the line's forms share, at a style.</summary>
    /// <param name="Button">Every button of the line — "Clear all", "Turn on", a row's pin and delete: the lines' one (LineButton).</param>
    private readonly record struct Sizes(ListForm Form, ItemForm Items, float HeadLine, LineButton Button, float Pad, float Above);

    private Sizes SizesOf(in TileStyle style)
    {
        var form = ListForm.Of(Gfx, style);
        var items = ItemForm.For(Gfx, style, Settings);
        var button = LineButton.Of(Gfx, style);
        return new Sizes(
            form,
            items,
            MathF.Max(form.Line, button.Height),
            button,
            items.Space(style, 6),
            items.Space(style, 4));
    }

    /// <summary>The room kept at a row's end for its buttons: the pin where the history can be pinned, and the delete.</summary>
    private float Kept(in TileStyle style, in Sizes sizes) =>
        ((store.CanPin ? 2 : 1) * sizes.Button.Height) + (store.CanPin ? sizes.Button.Gap : 0) + style.Px(8);

    /// <summary>The header's words: how many items, or why there are none to show.</summary>
    private string Head(IReadOnlyList<ClipView> items) =>
        items.Count > 0 ? S.DockClipboardCount(items.Count)
        : store.Loading ? S.DockClipboardReading
        : S.DockClipboardNone;

    /// <summary>The note's words and whether it offers "Turn on": while Karpo's own history shows, why.</summary>
    private (string Words, bool TurnOn)? Note() =>
        store.Note is { } why ? (why, store.HistoryOff) : null;

    /// <summary>The note under the header: its words, wrapped, and "Turn on" under them.</summary>
    private float NoteHeight(in TileStyle style, in Sizes sizes, float room)
    {
        if (Note() is not { } note)
        {
            return 0;
        }
        var (_, words) = PopupPaint.Fit(Gfx, note.Words, sizes.Form.SmallPx, Weight.Regular, room, NoteLines);
        return style.Px(2) + words + (note.TurnOn ? style.Px(2) + sizes.Button.Height : style.Px(6));
    }

    /// <summary>One item's row, its padding in: its text's lines or its picture.</summary>
    private float RowHeight(ClipView item, in TileStyle style, in Sizes sizes, float room) =>
        (2 * sizes.Pad) + Content(item, style, sizes.Items, room).Height;

    /// <summary>What a row shows in <paramref name="room"/> px across: its text as fitted, or its picture's size.</summary>
    private (string Text, float Width, float Height) Content(ClipView item, in TileStyle style, in ItemForm items, float room)
    {
        if (item.Kind == ClipKind.Image)
        {
            // Its thumbnail where the item style shows pictures; else the picture's glyph in a square.
            var (width, height) = items.Pictures ? PictureSize(style, item.Picture, room) : (style.Px(32), style.Px(32));
            return ("", MathF.Round(width * items.Picture), MathF.Round(height * items.Picture));
        }
        var (text, lines) = PopupPaint.Fit(Gfx, ClipText.Shown(item.Text), items.TextPx, Weight.Regular, room, Lines(items));
        return (text, room, MathF.Max(items.Line, lines));
    }

    /// <summary>How many lines of a text a row shows: its first three, as Win+V; one where the item style shows no preview.</summary>
    private static int Lines(in ItemForm items) => items.Preview ? ClipText.Lines : 1;

    /// <summary>
    /// The size a picture is drawn at: fitted inside <paramref name="room"/> across and
    /// <see cref="PictureDip"/> down, never larger than it was copied at the line's scale — a small
    /// picture stays small; a glyph's square where it could not be decoded.
    /// </summary>
    internal static (float Width, float Height) PictureSize(in TileStyle style, Pixels? picture, float room)
    {
        if (picture is null || picture.Width <= 0 || picture.Height <= 0)
        {
            return (style.Px(32), style.Px(32));
        }
        var fit = MathF.Min(MathF.Min(room / picture.Width, style.Text(PictureDip) / picture.Height), style.Scale);
        return (MathF.Max(1, MathF.Round(picture.Width * fit)), MathF.Max(1, MathF.Round(picture.Height * fit)));
    }

    /// <summary>
    /// list: as tall as it is to show every item — the header, the note, a row each — or the header
    /// alone with none (<see cref="IListContent"/>).
    /// </summary>
    public float ListHeight(float width, in TileStyle style)
    {
        var sizes = SizesOf(style);
        var form = sizes.Form;
        var room = Room(width, style, sizes);
        var look = style;
        if (store.Items.Count == 0)
        {
            // Nothing to list: the kit's one Status, one row (§388).
            return LineRows.Px(1, style);
        }
        var rows = store.Items.Sum(item => sizes.Above + RowHeight(item, look, sizes, room));
        return form.Pad + sizes.HeadLine + NoteHeight(style, sizes, form.Room(width)) + rows + LineItem.Foot(sizes.Above);
    }

    /// <summary>Every item, where the line lists them: they scroll under the header (ListScroll.cs).</summary>
    public float ScrollHeight(float width, float height, in TileStyle style)
    {
        var sizes = SizesOf(style);
        return store.Items.Count == 0 || height + 0.5f < Lists(width, style, sizes) ? 0 : ListHeight(width, style);
    }

    /// <summary>The header and the note under it stay while the items scroll.</summary>
    public float ScrollHead(float width, float content, in TileStyle style)
    {
        var sizes = SizesOf(style);
        return sizes.Form.Pad + sizes.HeadLine + NoteHeight(style, sizes, sizes.Form.Room(width));
    }

    /// <summary>The least a line lists its items at: the header, the note, and the first item's first line; under it, the line of one row.</summary>
    private float Lists(float width, in TileStyle style, in Sizes sizes) =>
        sizes.Form.Pad + sizes.HeadLine + NoteHeight(style, sizes, sizes.Form.Room(width)) + sizes.Above + sizes.Pad + sizes.Items.Line + LineItem.Foot(sizes.Above);

    /// <summary>A row's room for its text or picture on a line <paramref name="width"/> px wide.</summary>
    private float Room(float width, in TileStyle style, in Sizes sizes) =>
        MathF.Max(1, width - (2 * sizes.Form.Inset) - Kept(style, sizes));

    /// <summary>
    /// The line, at whatever height it was given (docs\widgets.md §9), drawn where the sketch draws:
    /// <list type="bullet">
    /// <item>One row: the glyph in the list's leading column, how many items there are, and "Clear
    /// all" against the trailing gutter.</item>
    /// <item>Taller: that header at the list's figure size, the note under it where there is one, and
    /// the items a row each from the top, as many whole as the height holds; given less than they
    /// take, the line is laid out as tall as all of them and scrolled (ListScroll.cs).</item>
    /// </list>
    /// With <paramref name="targets"/> it writes down where each click lands.
    /// </summary>
    private void Lay(LineSketch sketch, D2D_RECT_F box, TileStyle style, List<(Target Target, D2D_RECT_F Box)>? targets)
    {
        var sizes = SizesOf(style);
        var form = sizes.Form;
        var theme = style.Theme;
        var width = TilePaint.Width(box);
        var height = box.bottom - box.top;
        var left = box.left + form.TextLeft;
        var right = box.right - form.Inset;
        var items = store.Items;
        var room = Room(width, style, sizes);
        var head = Head(items);
        var glyphInk = items.Count > 0 ? TilePaint.Accent(style) : theme.TextSecondary;
        var heights = items.Select(item => sizes.Above + RowHeight(item, style, sizes, room)).ToArray();
        var noteHeight = NoteHeight(style, sizes, form.Room(width));
        var compact = items.Count > 0 ? Lists(width, style, sizes) : form.Pad + sizes.HeadLine + noteHeight + form.Pad;
        var look = sizes.Items;
        var hotNow = hot;
        var hotIdNow = hotId;

        // "Clear all" at the trailing gutter: there while there are items, dimmed with nothing but
        // pinned ones to clear. Where the count and it do not both fit (a large text size) it gives
        // way: the count is what a glance at the line reads, and the menu's Settings is a click away.
        var clearable = items.Any(item => !item.Pinned);
        var buttons = sizes.Button;
        var buttonWidth = buttons.Width(Gfx, S.DockClipboardClearAll);
        var one = height + 0.5f < compact;
        var countWidth = PopupPaint.Measure(Gfx, head, form.TextPx, Weight.Semibold).X;
        var showClear = items.Count > 0 && countWidth <= right - left - style.Gap - buttonWidth;

        float Controls(float centre)
        {
            if (!showClear)
            {
                return right;
            }
            var button = buttons.At(right - buttonWidth, centre, buttonWidth);
            var lit = clearable && hotNow.Spot == Spot.Clear;
            sketch.Button("clear all", buttons, theme, button, S.DockClipboardClearAll, null, ButtonStyle.Subtle, lit, lit && press.On(hotIdNow), clearable);
            if (clearable)
            {
                targets?.Add((new Target(Spot.Clear), buttons.Hit(button)));
            }
            return button.left - style.Gap;
        }

        if (items.Count == 0)
        {
            // Nothing copied, or still reading: the kit's one Status, with "Turn on" where Windows' history is off (§388).
            var turnOn = Note() is { TurnOn: true };
            var lit = hotNow.Spot == Spot.TurnOn;
            var actions = new List<(string Text, string? Glyph)>();
            if (turnOn)
            {
                actions.Add((S.DockClipboardTurnOn, null));
            }
            var status = LineStatus.Draw(sketch, box, style, Info.Glyph, head, actions, 0, lit, lit && press.On(hotIdNow));
            if (status.Button is { } action)
            {
                targets?.Add((new Target(Spot.TurnOn), buttons.Hit(action)));
            }
            return;
        }
        if (one)
        {
            var row = ListTiers.Row(Gfx, box, style, [head], Weight.Semibold, 0, (showClear ? buttonWidth : 0) + style.Gap);
            sketch.Glyph("glyph", Info.Glyph, form.Glyph, glyphInk, form.IconBox(box.left, row.TextTop, form.Line));
            sketch.Text("count", row.Name ?? "", form.TextPx, Weight.Semibold, theme.Text, row.NameLeft, row.TextTop, row.NameRoom);
            _ = Controls(row.TextTop + (form.Line / 2));
            return;
        }

        // As many whole items as the box holds, from the top: all of them where the line scrolls. Under the last, its
        // band's half of the room (LineItem.Foot).
        var inner = height - form.Pad - LineItem.Foot(sizes.Above);
        var fill = WidgetFill.Of(heights, MathF.Max(0, inner - sizes.HeadLine - noteHeight));
        var y = box.top + form.Pad;

        // The header, the kit's one (§388): the glyph, the count in the Body's strong weight, "Clear all".
        sketch.Glyph("glyph", Info.Glyph, form.Glyph, glyphInk, form.IconBox(box.left, y, sizes.HeadLine));
        var end = Controls(y + (sizes.HeadLine / 2));
        sketch.Text("count", head, form.TextPx, Weight.Semibold, theme.Text, left, y + PopupPaint.Round((sizes.HeadLine - form.Line) / 2), MathF.Max(1, end - left));
        y += sizes.HeadLine;

        // The note: why Karpo's own history shows, and the way to turn Windows' on.
        if (Note() is { } note)
        {
            y += style.Px(2);
            var said = sketch.Wrapped("note", note.Words, form.SmallPx, Weight.Regular, theme.TextSecondary, left, y, form.Room(width), NoteLines);
            y = said.bottom;
            if (note.TurnOn)
            {
                y += style.Px(2);
                // A standard button on the text's column: no links in lines (§388).
                var button = PopupPaint.Rect(left, y, buttons.Width(Gfx, S.DockClipboardTurnOn), buttons.Height);
                var lit = hotNow.Spot == Spot.TurnOn;
                sketch.Button("turn on", buttons, theme, button, S.DockClipboardTurnOn, null, ButtonStyle.Standard, lit, lit && press.On(hotIdNow));
                targets?.Add((new Target(Spot.TurnOn), button));
                y += buttons.Height;
            }
            else
            {
                y += style.Px(6);
            }
        }

        // The rows, newest first.
        var keyedItem = KeyedItem;
        for (var index = 0; index < fill.Shown; index++)
        {
            var item = items[index];
            // Out of sight in a scrolled line: not drawn (CellIsland.Hidden).
            if (sketch.Draws && Hidden(y, y + heights[index]))
            {
                y += heights[index];
                continue;
            }
            y += sizes.Above;
            var rowHeight = heights[index] - sizes.Above;
            // On the gutter, as the mail's rows are: the words from it, the plate a little past both.
            var rowLeft = box.left + form.Inset;
            // The item with half the room over it and half the room under it, edge to edge: the next one's begins
            // where it ends (LineItem).
            var band = LineItem.Around(box, y, y + rowHeight, sizes.Above);
            // The pointer's row lit, else the keys' (the folder's ring round the row or the button they are on).
            var lit = (hotNow.Spot is Spot.Row or Spot.Pin or Spot.Delete ? hotNow.Item : keyedItem) == item.Id;
            var pressed = hotNow == new Target(Spot.Row, item.Id) && press.On(hotIdNow);
            var radius = style.Px(4);
            targets?.Add((new Target(Spot.Row, item.Id), band));
            if (lit)
            {
                LineItem.Light(sketch, band, theme, pressed);
            }
            var top = y + sizes.Pad;
            var content = Content(item, style, look, room);
            if (item.Kind == ClipKind.Image)
            {
                var at = PopupPaint.Rect(rowLeft, top, content.Width, content.Height);
                if (look.Pictures && item.Picture is { } picture)
                {
                    sketch.Picture($"picture {index}", at, (dc, square) =>
                    {
                        // Drawn into a square as large as its longer side, so it fits that box exactly.
                        var side = MathF.Max(square.right - square.left, square.bottom - square.top);
                        PopupPaint.Clipped(dc, square, radius, () => Gfx.DrawPixels(dc, picture,
                            square.left - ((side - (square.right - square.left)) / 2), square.top - ((side - (square.bottom - square.top)) / 2), side));
                        PopupPaint.Stroke(dc, square, radius, MathF.Max(1, style.Px(1)), PopupPaint.Ink(theme.CardStroke));
                    });
                }
                else
                {
                    sketch.Glyph($"picture {index}", PictureGlyph, style.Px(24), theme.TextSecondary, at);
                }
            }
            else
            {
                sketch.Wrapped($"text {index}", content.Text, look.TextPx, Weight.Regular, theme.Text, rowLeft, top, room, Lines(look));
            }

            // Its buttons at its end, on its first line, under the pointer or the keys: the delete at
            // the end, the pin before it where the history can be pinned.
            var side = sizes.Button.Height;
            var buttonTop = top + PopupPaint.Round((look.Line - side) / 2);
            if (SaysCopied(item.Id))
            {
                // Just copied: the accent's check and "Copied" where its buttons stand, on a chip of
                // its own over the end of its first line, until CopiedMs have gone.
                Chip(sketch, index, right, buttonTop, sizes, style);
                y += rowHeight;
                continue;
            }
            // The delete's box on the trailing gutter, as every line's last button stands.
            var delete = new D2D_RECT_F(right - side, buttonTop, right, buttonTop + side);
            var pin = PopupPaint.Rect(delete.left - sizes.Button.Gap - side, buttonTop, side, side);
            targets?.Add((new Target(Spot.Delete, item.Id), delete));
            if (store.CanPin)
            {
                targets?.Add((new Target(Spot.Pin, item.Id), pin));
            }
            // A pinned item wears the pin at its end, quiet, until its buttons show there.
            if (item.Pinned && !lit)
            {
                sketch.Glyph($"pinned {index}", PinnedGlyph, sizes.Button.GlyphPx, theme.TextSecondary, delete);
            }
            if (lit)
            {
                var onDelete = hotNow == new Target(Spot.Delete, item.Id);
                sketch.Button($"delete {index}", sizes.Button, theme, delete, "", DeleteGlyph, ButtonStyle.Subtle, onDelete, onDelete && press.On(hotIdNow));
                if (store.CanPin)
                {
                    var onPin = hotNow == new Target(Spot.Pin, item.Id);
                    sketch.Button($"pin {index}", sizes.Button, theme, pin, "", item.Pinned ? PinnedGlyph : PinGlyph, ButtonStyle.Subtle, onPin, onPin && press.On(hotIdNow));
                }
            }
            y += rowHeight;
        }
    }

    /// <summary>
    /// The chip a row wears while it says it was copied, ending at <paramref name="end"/> on its first
    /// line: the accent's check and "Copied" on a control's fill over the flyout's own solid colour, so
    /// the words under it never show through.
    /// </summary>
    private void Chip(LineSketch sketch, int index, float end, float top, in Sizes sizes, in TileStyle style)
    {
        var form = sizes.Form;
        var theme = style.Theme;
        // In the place and the size of the buttons it stands for (LineButton).
        var buttons = sizes.Button;
        var inset = buttons.Pad;
        var glyph = buttons.GlyphPx;
        var words = PopupPaint.Measure(Gfx, S.DockClipboardCopied, buttons.TextPx, Weight.Regular);
        var width = inset + glyph + buttons.GlyphGap + words.X + inset;
        var chip = new D2D_RECT_F(end - width, top, end, top + buttons.Height);
        var radius = style.Px(4);
        if (sketch.Draws)
        {
            sketch.Plate($"copied {index} base", chip, radius, PopupPaint.Ink(theme.Base));
            sketch.Plate($"copied {index} plate", chip, radius, PopupPaint.Ink(theme.ControlFill));
            PopupPaint.Stroke(sketch.Device, chip, radius, MathF.Max(1, style.Px(1)), PopupPaint.Ink(theme.ControlStroke));
        }
        sketch.Glyph($"copied {index} check", CopiedGlyph, glyph, TilePaint.Accent(style),
            PopupPaint.Rect(chip.left + inset, top + PopupPaint.Round((buttons.Height - glyph) / 2), glyph, glyph));
        sketch.Text($"copied {index}", S.DockClipboardCopied, buttons.TextPx, Weight.Regular, theme.Text,
            chip.left + inset + glyph + buttons.GlyphGap, top + PopupPaint.Round((buttons.Height - words.Y) / 2), words.X + 1);

    }
}

/// <summary>What of a copied text a row shows: its first few lines, without the blank ones it starts with.</summary>
internal static class ClipText
{
    /// <summary>How many lines of an item's text a row shows, as Win+V shows a few.</summary>
    internal const int Lines = 3;

    /// <summary>The most characters a row is handed: more than three lines ever hold.</summary>
    internal const int MostChars = 600;

    /// <summary>
    /// <paramref name="text"/> as a row shows it: line breaks as one kind, tabs as spaces, the blank
    /// lines and the spaces it starts with left out, its trailing spaces each line, and no more than
    /// the first <see cref="Lines"/> lines and <see cref="MostChars"/> characters.
    /// </summary>
    public static string Shown(string text)
    {
        text = Held(text);
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Replace("\t", "    ", StringComparison.Ordinal).Split('\n');
        var shown = new List<string>();
        foreach (var line in lines)
        {
            var trimmed = line.TrimEnd();
            if (shown.Count == 0 && trimmed.Trim().Length == 0)
            {
                continue;
            }
            shown.Add(shown.Count == 0 ? trimmed.TrimStart() : trimmed);
            if (shown.Count == Lines)
            {
                break;
            }
        }
        var joined = string.Join('\n', shown);
        return joined.Length > MostChars ? joined[..MostChars] : joined;
    }

    /// <summary>
    /// What of <paramref name="text"/> a row ever needs, from its first character that is not
    /// white space: a few times <see cref="MostChars"/>, so a text of a million characters is not
    /// split into lines to show three of them.
    /// </summary>
    public static string Held(string text)
    {
        var start = 0;
        while (start < text.Length && char.IsWhiteSpace(text[start]))
        {
            start++;
        }
        var length = Math.Min(text.Length - start, HeldChars);
        return start == 0 && length == text.Length ? text : text.Substring(start, length);
    }

    /// <summary>What <see cref="Held"/> keeps at most.</summary>
    internal const int HeldChars = 4 * MostChars;
}
