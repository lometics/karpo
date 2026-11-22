// pim: the people in the person's accounts, as a line of a folder's list (docs\widgets.md §102). It
// was the contacts entry's flyout (ContactsFlyout, a search box over the people) until that entry
// became the contacts preset (2026-10-11, the person: "port contacts"): a folder wearing the entry's
// glyph and holding this line, which does what the flyout did.
//
// **What the flyout did, the line does.** A search box at its top — Windows 11's, the magnifier
// before the words, the quick settings' own TextField drawn on the card as the Wi-Fi's key field is
// (QuickPageTile) — and right under it a row per person who answers what is typed (ContactSearch, over what is in memory, at once on
// every keystroke): their picture in a circle in the leading column where they have one and the item
// style shows pictures (ContactPhotos, §457), else the contact's glyph, the name, and under it the address,
// else the number, else the organization. It says neither how many there are nor whose (2026-10-31,
// §450: the count under the box, "128 contacts in a@b.com", went).
//
// **A person's details** (2026-10-11, the person: "it should actually show copy buttons for different
// things like the email, phone, etc. also a send email or call button. make sure that it does not
// overload the widget"). At rest a row is what it was — nothing more on it. Under the pointer (or the
// keys) it shows at its end at most two buttons, Email and Call, each only where the person has an
// address or a number Windows can open (ContactActs). A click on the row opens it in place, one at a
// time: each address, each number and the organization on a line of its own under the name, its kind's
// glyph in the leading column, a Copy button at its end and Email or Call before it; a click on the
// line copies it too. Copying says "Copied" on a chip where the buttons stood, as the clipboard's rows
// do, and the folder stays. Email and Call hand Windows mailto: and tel: — the default mail app and
// the default calling app (Phone Link, Teams) — once the folder has gone (CellAct.Action, FolderForward).
// The right click on a row offers the same for that person (Dock.WidgetMenus.cs).
//
// **The keys.** The box has the keys as the folder opens, so typing searches at once; it is a text box
// with the keys (CardTyping), so the folder hands it every key first. Up and Down choose a row while
// the caret stays in the box (the keys' ring marks it; neither end wraps); Enter opens or closes it;
// Ctrl+C copies its address (its number where there is none) unless text is selected in the box;
// Escape is the folder's, which closes, as it closed the flyout; Tab lets the box go and is the
// folder's. Without the box (Tab took the keys on), Up and Down walk the rows and past either end the
// keys are the folder's; Enter or Space opens; a letter goes back into the box.
//
// **A line of every height.** On one row the box alone, across the line; the accounts are asked again,
// however fresh, from "Refresh" on its right click (IRefreshable, §451). Taller: the box and, right under it, every
// person who answers, scrolling under the box where there are more than the line holds (ListScroll.cs) —
// what is typed narrows them — in the item style chosen (ItemForm): their size, and the address
// under the name. Its height follows the people it lists and the row open (IListContent), up to what
// the line may be; a row opened or chosen by the keys is scrolled into sight (ScrollReveal).
//
// **What it reads.** The one store (ContactStore.Shared) — a test's stand-in (Stand), so no test
// reaches an account — synced from the dock's start whether or not a line is placed (PimSync), and
// followed while it shows; the folder opening only starts what is due (Visible), as the mail line's does. With no account that gives contacts, the line is "Set up in Settings"
// (Unset), as the flyout's empty list was. A click is matched by the person, never by the place.

using System.Numerics;
using Karpo.Accounts;
using Karpo.Core;
using Karpo.Dock.QuickSettings.Controls;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock.Pim;

internal sealed unsafe class ContactsLine : CellIsland, IListContent, IListScroll, ILinePieces, IRefreshable
{
    /// <summary>Segoe Fluent Icons: CheckMark, beside "Copied" on what was just copied, as the clipboard's.</summary>
    internal const string CopiedGlyph = ClipboardLine.CopiedGlyph;

    /// <summary>Segoe Fluent Icons: ChevronUp, at the end of an open row's name: a click there closes it.</summary>
    internal const string CloseGlyph = "";

    /// <summary>How long a line says "Copied", in ms: the clipboard's.</summary>
    internal const int CopiedMs = ClipboardLine.CopiedMs;

    /// <summary>The most characters the box takes, as the flyout's.</summary>
    private const int MostTyped = 128;

    /// <summary>The most lines the words where nobody is listed take.</summary>
    private const int HintLines = 3;

    [ThreadStatic]
    private static ContactStore? stand;

    /// <summary>
    /// tests: the store a line the registry makes reads, in this thread's place of
    /// <see cref="ContactStore.Shared"/>; null for the shared one, which is what the dock uses.
    /// </summary>
    internal static ContactStore? Stand
    {
        get => stand;
        set => stand = value;
    }

    /// <summary>The store a line made on this thread reads: the test's, else the dock's.</summary>
    internal static ContactStore StoreNow => Stand ?? (Samples.On ? Samples.Contacts() : ContactStore.Shared);

    [ThreadStatic]
    private static Action<string>? copier;

    /// <summary>What puts a detail on the clipboard, per thread: a test's, which writes it down; null for Windows' clipboard.</summary>
    internal static Action<string>? Copier
    {
        get => copier;
        set => copier = value;
    }

    [ThreadStatic]
    private static bool? control;

    /// <summary>tests: whether Ctrl is held, per thread, in place of the keyboard's state; null for the keyboard's.</summary>
    internal static bool? ControlHeld
    {
        get => control;
        set => control = value;
    }

    private readonly ContactStore store;
    private bool listening;
    private TextField box;

    // The people who answer what is typed, and what they were worked out from.
    private IReadOnlyList<Person> matches = [];
    private IReadOnlyList<Person>? matchedFrom;
    private string matchedText = "";

    // The person the keys are on, and whether the keys chose them (they wear the ring then, not after a click).
    private Person? selected;
    private bool keyed;

    // The person whose row is open, one at a time; the one to scroll into sight at the next layout.
    private Person? open;
    private Person? reveal;

    // What is under the pointer and the number the folder was given for it; the targets given out.
    private Target hot;
    private int hotId;
    private readonly List<Target> given = [];

    // The target a press is down on, lit pressed (RowPress): the box's own press is `pressing`.
    private RowPress press;

    // What was copied last, whose, and until when (Ticks) it says so.
    private (Person Person, string Value)? copied;
    private long copiedUntil;

    // The person the last right click was about (MenuContact), and the address a click handed the
    // dock to open once the folder has gone (Open).
    private Person? menuPerson;
    private string? opening;

    // The people the last layout showed as rows, in order: what the arrows walk.
    private readonly List<Person> shown = [];

    // The box as the last layout placed it, in the card's own px from its box's corner; whether the
    // pointer holds it, and the presses that make a double click.
    private D2D_RECT_F boxAt;
    private bool pressing;
    private long lastPress;
    private Vector2 lastPressAt;
    private int clicks;

    public ContactsLine(string key, WidgetItem island, Gfx gfx, Config config)
        : this(key, island, gfx, config, StoreNow)
    {
    }

    /// <summary>A line on <paramref name="store"/>: the dock's is the shared one; tests bring their own.</summary>
    internal ContactsLine(string key, WidgetItem island, Gfx gfx, Config config, ContactStore store)
        : base(key, island, gfx)
    {
        this.store = store;
        box = NewBox();
        Configure(island, config);
        // Attaching reads nothing: the store is synced from the dock's start (PimSync).
        store.Attach(this);
    }

    /// <summary>The search box, named in the language in force: a widget's name is set once, so a new language is a new box.</summary>
    private TextField NewBox() => new(maxLength: MostTyped)
    {
        Name = S.DockContactsSearch,
        Glyph = SearchPill.Glyph,
        SearchBox = true,
        Placeholder = S.DockContactsSearch,
        TextChanged = Typed,
        Submitted = () => Toggle(selected),
    };

    /// <summary>The caret blinks while the box has the keys; nothing ticks otherwise.</summary>
    public override int IntervalMs => box.Focused ? 500 : 0;

    protected override int Count => 1;

    public override void Configure(WidgetItem island, Config config)
    {
        Settings = island;
        Forget();
    }

    /// <summary>No account gives contacts, as the last read found: "Set up in Settings", for the Accounts page (SetupPrompt.cs).</summary>
    internal override SetupNeed? Unset => !store.HasAccount && !store.Loading ? SetupNeed.Contacts : null;

    /// <summary>Nothing of its own: its rows and the box say it all.</summary>
    public override string? Tooltip(int index) => null;

    /// <summary>Never drawn on a bar: it is a line of a list and nothing else; on the bar it is the preset's face.</summary>
    internal override void PaintCell(ID2D1DeviceContext* dc, int index, in IslandStyle style, float width, float height) =>
        throw new NotSupportedException($"{Info.Id} is a line of a folder's list, not a widget of a bar.");

    /// <summary>An account that gives contacts ("Refresh", IRefreshable).</summary>
    public bool CanRefresh => store.HasAccount;

    /// <summary>The accounts are being read.</summary>
    public bool Refreshing => store.Reading;

    /// <summary>The store keeps no time of its read: "Refresh" says nothing under it.</summary>
    public DateTime? UpdatedUtc => null;

    /// <summary>Why the accounts' people could not be read.</summary>
    string? IRefreshable.Problem => store.Problem;

    /// <summary>"Refresh": the accounts asked again, however fresh the last read.</summary>
    public void RefreshNow() => store.Reread();

    /// <summary>tests: the clock "Copied" is timed by, in ms; Windows' tick count by default.</summary>
    internal Func<long> Ticks { get; set; } = static () => Environment.TickCount64;

    /// <summary>tests: what "Copied" being over does; the dock's line is drawn again.</summary>
    internal Action<Action, int> After { get; set; } = static (then, ms) =>
        _ = Task.Delay(ms).ContinueWith(
            _ => Troubles.Guard("Contacts: copied said", then), // not-ui: a trouble's name
            CancellationToken.None,
            TaskContinuationOptions.None,
            TaskScheduler.Default);

    /// <summary>
    /// Where Email and Call go: Windows' handler for mailto: and tel:, through the shell, once the
    /// folder has gone. A test puts its own here and writes down what it was handed.
    /// </summary>
    internal Action<string> Opened { get; set; } = static uri => Launch.Open(uri);

    // --- The showing --------------------------------------------------------------------------------

    /// <summary>
    /// The folder opened: the box is empty and has the keys, the store starts whatever read is due
    /// (nothing while its sync is on time), and the line follows the store while it shows. Closed,
    /// what was typed, chosen, opened and copied goes.
    /// </summary>
    public override void Visible(bool shown)
    {
        base.Visible(shown);
        if (shown)
        {
            if (box.Name != S.DockContactsSearch)
            {
                box = NewBox();
            }
            Rest();
            Focus(true);
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
        Focus(false);
        Rest();
    }

    /// <summary>Nothing typed, chosen, open, copied or under the pointer.</summary>
    private void Rest()
    {
        box.Text = "";
        selected = null;
        keyed = false;
        open = null;
        reveal = null;
        copied = null;
        hot = default;
        hotId = 0;
        pressing = false;
        _ = press.Up();
        box.Pressed = false;
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

    /// <summary>The store posted: it raises Changed. A tick: the caret blinks.</summary>
    protected override bool Sample(bool everything)
    {
        if (everything)
        {
            store.Deliver();
        }
        else if (box.Focused)
        {
            _ = box.Blink();
        }
        return false;
    }

    /// <summary>The store has news, on the dock's thread: the line is laid out again, its height with it.</summary>
    private void Arrived() => Redrawn();

    /// <summary>The box changed: the rows follow at once, over what is already in memory; nothing is chosen.</summary>
    private void Typed()
    {
        selected = null;
        keyed = false;
        Redrawn();
    }

    /// <summary>§455: everyone of the address books this line shows from ("Show from"; every book where it picked none).</summary>
    private IReadOnlyList<Person> People => store.View(SourceFilter.Of(Settings));

    /// <summary>The people who answer what is typed, worked out again only where the store or the text changed.</summary>
    internal IReadOnlyList<Person> Matches
    {
        get
        {
            var all = People;
            if (!ReferenceEquals(all, matchedFrom) || matchedText != box.Text)
            {
                matchedFrom = all;
                matchedText = box.Text;
                matches = ContactSearch.Match(all, box.Text);
            }
            return matches;
        }
    }

    /// <summary>tests: the box.</summary>
    internal TextField Box => box;

    /// <summary>tests: the person the keys are on.</summary>
    internal Person? Selected => selected;

    /// <summary>tests: the person whose row is open.</summary>
    internal Person? Open => open;

    /// <summary>
    /// <paramref name="person"/>'s row opens, closing the one that was; theirs closes where it was
    /// open. Opened, it is scrolled into sight.
    /// </summary>
    private void Toggle(Person? person)
    {
        if (person is null)
        {
            return;
        }
        selected = person;
        open = ReferenceEquals(open, person) ? null : person;
        reveal = person;
        Redrawn();
    }

    /// <summary><paramref name="value"/>, one of <paramref name="person"/>'s details, goes on the clipboard, and says so. The folder stays.</summary>
    private void Copy(Person person, string value)
    {
        if (Copier is { } test)
        {
            test(value);
        }
        else
        {
            Clipboard.SetText(GetActiveWindow(), value);
        }
        copied = (person, value);
        copiedUntil = Ticks() + CopiedMs;
        Redrawn();
        After(() => _ = Wake(), CopiedMs + 20);
    }

    /// <summary>Whether <paramref name="person"/>'s <paramref name="value"/> (any of theirs, for null) says "Copied" now.</summary>
    internal bool SaysCopied(Person person, string? value = null) =>
        copied is { } was && ReferenceEquals(was.Person, person) && (value is null || was.Value == value) && Ticks() < copiedUntil;

    /// <summary>Whether what of <paramref name="person"/>'s says "Copied" is one of their details, which says it on its own line.</summary>
    private bool CopiedADetail(Person person) =>
        copied is { } was && ContactActs.Details(person).Any(one => one.Value == was.Value);

    /// <summary>The address or the number a person's row copies with Ctrl+C: their first address, else their first number.</summary>
    private void CopyPrimary(Person? person)
    {
        if (person is not null && ContactActs.Primary(person) is { } value)
        {
            Copy(person, value);
        }
    }

    // --- Clicks -------------------------------------------------------------------------------------

    /// <summary>What a press lands on.</summary>
    internal enum Spot
    {
        None,

        /// <summary>A person's row, or an open row's name: opens or closes it.</summary>
        Row,

        /// <summary>One detail's line of an open row: copies it.</summary>
        Detail,

        /// <summary>A detail's Copy button.</summary>
        Copy,

        /// <summary>An Email button: the row's, for the first address; a line's, for its own.</summary>
        Email,

        /// <summary>A Call button.</summary>
        Call,

        /// <summary>A person's whole row, open or not: what is scrolled into sight; never hit.</summary>
        Block,
    }

    /// <summary>A thing a click acts on: what it is, whose — the person, never the place — and the detail it is about.</summary>
    internal readonly record struct Target(Spot Spot, Person? Person = null, string Value = "", DetailKind Kind = DetailKind.Email);

    /// <summary>
    /// What the click the hit test found does; with no part (the keys: Enter or Space without the
    /// box) the row the keys are on, opened or closed. A row opens or closes; a line or Copy copies,
    /// in place; Email and Call leave the address for the dock to open once the folder has gone.
    /// </summary>
    public override IslandAction Click(int index)
    {
        if (press.Up())
        {
            _ = Wake();
        }
        if (index == 0)
        {
            Toggle(selected);
            return default;
        }
        // The pointer's: what a folder being arranged takes away (it tells the line it is nowhere).
        if (index != hotId || hot.Person is not { } person)
        {
            return default;
        }
        keyed = false;
        switch (hot.Spot)
        {
            case Spot.Row:
                Toggle(person);
                return default;
            case Spot.Detail or Spot.Copy:
                selected = person;
                Copy(person, hot.Value);
                return default;
            case Spot.Email or Spot.Call when (hot.Spot == Spot.Email ? ContactActs.MailUri(hot.Value) : ContactActs.TelUri(hot.Value)) is { } uri:
                opening = uri;
                return new IslandAction(View: IslandView.App);
            default:
                return default;
        }
    }

    /// <summary>The dock, once the folder has gone: opens the mailto: or tel: an Email or a Call button left, once.</summary>
    internal void OpenChosen()
    {
        if (opening is { } uri)
        {
            opening = null;
            Opened(uri);
        }
    }

    /// <summary>tests: the address an Email or a Call button left for the dock.</summary>
    internal string? Opening => opening;

    // --- The menu -----------------------------------------------------------------------------------

    /// <summary>
    /// folders: whom the right click is about, and what of theirs it offers — the person under the
    /// pointer, else the one the keys are on; the address or number under the pointer where it is on
    /// one of an open row's lines, else their first. Null over no person. Remembered, so a copy picked
    /// from the menu says "Copied" on their row.
    /// </summary>
    internal (Person Person, string? Email, string? Phone)? MenuContact()
    {
        menuPerson = hot.Person ?? selected;
        if (menuPerson is not { } person)
        {
            return null;
        }
        // On one of an open row's lines (or its buttons): that address or that number.
        var onLine = ReferenceEquals(hot.Person, person) && hot.Spot is Spot.Detail or Spot.Copy or Spot.Email or Spot.Call;
        var email = onLine && hot.Kind == DetailKind.Email && ContactActs.MailUri(hot.Value) is not null ? hot.Value : ContactActs.FirstEmail(person);
        var phone = onLine && hot.Kind == DetailKind.Phone && ContactActs.TelUri(hot.Value) is not null ? hot.Value : ContactActs.FirstPhone(person);
        return (person, email, phone);
    }

    /// <summary>folders: the menu's Copy picked: <paramref name="text"/> goes on the clipboard, said on the row of the person it was about.</summary>
    internal void CopyFromMenu(string text)
    {
        if (menuPerson is { } person && text.Length > 0)
        {
            Copy(person, text);
        }
    }

    // --- The keys -----------------------------------------------------------------------------------

    /// <summary>The box has the keys: the folder hands this line every key first, and every character.</summary>
    protected internal override bool CardTyping => box.Focused;

    /// <summary>A press elsewhere in the folder: the box lets the keys go, keeping what was typed.</summary>
    protected internal override void CardStopTyping()
    {
        Focus(false);
        _ = Wake();
    }

    /// <summary>Gives the box the keys, or takes them from it.</summary>
    private void Focus(bool on)
    {
        if (box.Focused == on)
        {
            return;
        }
        box.Focused = on;
        box.FocusVisible = false;
        box.FocusChanged();
    }

    /// <summary>keys: the line walks its search box and people itself (CellIsland.WalksItsOwnKeys).</summary>
    protected internal override bool WalksItsOwnKeys => true;

    /// <summary>keys: the person's row its keys are on.</summary>
    protected internal override int? OwnKeyedTarget => keyed && selected is { } person ? IdOf(new Target(Spot.Row, person)) : 0;

    /// <summary>keys: the row its keys are on is ringed (one ring, §471).</summary>
    protected internal override bool RingsOwnKeys => keyed && selected is not null;

    /// <summary>keys: gone to another line, the row they were on is ringed no more.</summary>
    protected internal override void OwnKeys(bool on)
    {
        if (!on && keyed)
        {
            keyed = false;
            Redrawn();
        }
    }

    /// <summary>
    /// A key (see the top). With the box: Up and Down choose a row, Enter opens or closes it, Ctrl+C
    /// copies its address unless text is selected in the box, Escape and Tab are the folder's (Tab
    /// taking the keys from the box), and every other key is the box's. Without it: Up and Down walk
    /// the rows, past either end the folder's; Enter or Space opens or closes; Ctrl+C copies.
    /// </summary>
    protected internal override bool CardKey(int key, WidgetSurface surface)
    {
        var rows = Rows();
        var copying = key == 'C' && (ControlHeld ?? KeyState.Down(VK.VK_CONTROL)) && selected is not null;
        if (box.Focused)
        {
            switch (key)
            {
                case VK.VK_ESCAPE:
                    return false;
                case VK.VK_TAB:
                    Focus(false);
                    _ = Wake();
                    return false;
                case VK.VK_DOWN or VK.VK_UP:
                    Pick(Move(rows.IndexOf(selected!), rows.Count, key == VK.VK_DOWN), rows);
                    return true;
                case VK.VK_RETURN:
                    Toggle(selected);
                    return true;
                case 'C' when copying && !box.HasSelection:
                    CopyPrimary(selected);
                    return true;
                default:
                    // The keys that type come as characters (CardChar): none of them is the folder's.
                    _ = box.Key(key, Held());
                    Redrawn();
                    return true;
            }
        }
        var at = selected is { } chosen ? rows.IndexOf(chosen) : -1;
        switch (key)
        {
            case VK.VK_DOWN or VK.VK_UP:
            {
                var next = at < 0 ? (key == VK.VK_DOWN ? 0 : rows.Count - 1) : at + (key == VK.VK_DOWN ? 1 : -1);
                if (next < 0 || next >= rows.Count)
                {
                    Pick(-1, rows);
                    return false;
                }
                Pick(next, rows);
                return true;
            }
            case VK.VK_RETURN or VK.VK_SPACE when at >= 0:
                Toggle(selected);
                return true;
            case 'C' when copying && at >= 0:
                CopyPrimary(selected);
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// A character: the box's. A letter typed while the line has the folder's focus but not the
    /// box gives the box the keys again and types it, as a letter starts a to-do's task.
    /// </summary>
    protected internal override bool CardChar(char c, WidgetSurface surface)
    {
        if (!box.Focused)
        {
            if (char.IsControl(c))
            {
                return false;
            }
            Focus(true);
        }
        _ = box.Character(c);
        Redrawn();
        return true;
    }

    /// <summary>The people the arrows walk: those the last layout showed, else every one that answers.</summary>
    private List<Person> Rows() => shown.Count > 0 ? [.. shown] : [.. Matches];

    /// <summary>Row <paramref name="index"/> of <paramref name="rows"/> is the one the keys are on, scrolled into sight; -1 for none.</summary>
    private void Pick(int index, List<Person> rows)
    {
        selected = index >= 0 && index < rows.Count ? rows[index] : null;
        keyed = selected is not null;
        reveal = selected;
        Redrawn();
    }

    /// <summary>
    /// The next row in that direction. Nothing chosen starts at the first going down and the last
    /// going up, and neither end wraps: a list of several hundred that jumped from the last name to
    /// the first would lose the person's place.
    /// </summary>
    internal static int Move(int selected, int count, bool down)
    {
        if (count == 0)
        {
            return -1;
        }
        if (selected < 0)
        {
            return down ? 0 : count - 1;
        }
        return Math.Clamp(selected + (down ? 1 : -1), 0, count - 1);
    }

    private static Mods Held() =>
        (KeyState.Down(VK.VK_SHIFT) ? Mods.Shift : Mods.None)
        | ((ControlHeld ?? KeyState.Down(VK.VK_CONTROL)) ? Mods.Control : Mods.None);

    // --- The pointer ----------------------------------------------------------------------------------

    /// <summary>Only a row, its buttons and the box light under the pointer, never the line around them.</summary>
    protected internal override bool LightsItsRows => true;

    /// <summary>A press on the box is the line's: the caret goes there, a drag selects, and the box has the keys. Elsewhere it is the folder's.</summary>
    protected internal override bool CardPress(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        Lay(new LineSketch(null, Gfx), box, style, null);
        var local = InBox(point, box);
        if (!this.box.Contains(local))
        {
            // A row's press stays the folder's, which makes it the click; the row lights pressed meanwhile.
            if (press.Down(IdOf(LineTarget(point, box, style))))
            {
                _ = Wake();
            }
            return false;
        }
        var now = Environment.TickCount64;
        clicks = now - lastPress <= GetDoubleClickTime() && Vector2.Distance(point, lastPressAt) <= GetSystemMetrics(SM.SM_CXDOUBLECLK) ? clicks + 1 : 1;
        lastPress = now;
        lastPressAt = point;
        Focus(true);
        pressing = true;
        this.box.Pressed = true;
        _ = this.box.PointerPressed(local, Held(), clicks);
        _ = Wake();
        return true;
    }

    protected internal override void CardDrag(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        if (!pressing)
        {
            return;
        }
        this.box.PointerMoved(InBox(point, box));
        _ = Wake();
    }

    protected internal override void CardRelease()
    {
        if (!pressing)
        {
            return;
        }
        pressing = false;
        box.Pressed = false;
        box.PointerReleased(default, inside: true);
        _ = Wake();
    }

    /// <summary><paramref name="point"/> in the search box's own px, which the box's hit testing takes.</summary>
    private Vector2 InBox(Vector2 point, D2D_RECT_F card) => new(point.X - card.left - boxAt.left, point.Y - card.top - boxAt.top);

    protected internal override void CardHover(Vector2? point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var under = point is { } at ? LineTarget(at, box, style) : default;
        var overBox = point is { } over && this.box.Contains(InBox(over, box));
        if (this.box.Hot != overBox)
        {
            this.box.Hot = overBox;
            _ = Wake();
        }
        if (point is not null && under.Spot != Spot.None)
        {
            // The pointer and the keys light one row at a time.
            keyed = false;
        }
        var let = press.Over(IdOf(under));
        if (under == hot && !let)
        {
            return;
        }
        hot = under;
        hotId = IdOf(under);
        _ = Wake();
    }

    protected internal override int CardTarget(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface) =>
        IdOf(LineTarget(point, box, style));

    /// <summary>The buttons name themselves, being glyphs alone.</summary>
    protected internal override (string Text, D2D_RECT_F Part)? CardTip(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var targets = new List<(Target Target, D2D_RECT_F Box)>();
        Lay(new LineSketch(null, Gfx), box, style, targets);
        foreach (var (target, at) in targets)
        {
            if (target.Spot is Spot.Copy or Spot.Email or Spot.Call && LineRows.Inside(at, point))
            {
                return (target.Spot switch
                {
                    Spot.Copy => S.DockContactsCopy,
                    Spot.Email => S.DockContactsSendEmail,
                    _ => S.DockContactsCall,
                }, at);
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

    /// <summary>What is at <paramref name="point"/>: a button before the line or the row it stands on.</summary>
    internal Target LineTarget(Vector2 point, D2D_RECT_F box, in TileStyle style)
    {
        var targets = new List<(Target Target, D2D_RECT_F Box)>();
        Lay(new LineSketch(null, Gfx), box, style, targets);
        foreach (var precise in new[] { true, false })
        {
            foreach (var (target, at) in targets)
            {
                if (target.Spot != Spot.Block && (target.Spot is Spot.Copy or Spot.Email or Spot.Call) == precise && LineRows.Inside(at, point))
                {
                    return target;
                }
            }
        }
        return default;
    }

    /// <summary>The person whose row is at <paramref name="point"/>, or null.</summary>
    internal Person? RowAt(Vector2 point, D2D_RECT_F box, in TileStyle style) => LineTarget(point, box, style).Person;

    // --- Scrolled into sight ----------------------------------------------------------------------------

    /// <summary>The row just opened or chosen by the keys, from the content's top, once; then nothing until the next.</summary>
    public (float Top, float Bottom)? ScrollReveal(float width, in TileStyle style)
    {
        if (reveal is not { } person)
        {
            return null;
        }
        reveal = null;
        var targets = new List<(Target Target, D2D_RECT_F Box)>();
        Lay(new LineSketch(null, Gfx), PopupPaint.Rect(0, 0, width, ListHeight(width, style)), style, targets);
        foreach (var (target, at) in targets)
        {
            if (target.Spot == Spot.Block && ReferenceEquals(target.Person, person))
            {
                // With room for the keys' ring around it.
                var ring = style.Px(4);
                return (at.top - ring, at.bottom + ring);
            }
        }
        return null;
    }

    // --- Drawn ----------------------------------------------------------------------------------------

    protected internal override void PaintCard(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        Lay(new LineSketch(dc, Gfx), box, style, null);
        // Whatever was to be scrolled into sight has been, or there was nothing to scroll.
        reveal = null;
    }

    /// <summary>What a line of one row shows: the box.</summary>
    public string MainPiece => "search";

    public IReadOnlyList<LinePiece> LinePieces(D2D_RECT_F box, in TileStyle style)
    {
        var sketch = new LineSketch(null, Gfx);
        Lay(sketch, box, style, null);
        return sketch.Pieces;
    }

    /// <summary>The sizes the line is laid out on, at a style.</summary>
    /// <param name="Side">A glyph button's side: the lines' one button's (<paramref name="Button"/>, LineButton).</param>
    /// <param name="Detail">One detail's line of an open row: its words, or its buttons where they are taller.</param>
    private readonly record struct Sizes(ListForm Form, ItemForm Items, float Box, float Pad, float Above, float Side, float Detail, LineButton Button);

    private Sizes SizesOf(in TileStyle style)
    {
        var form = ListForm.Of(Gfx, style);
        var items = ItemForm.For(Gfx, style, Settings);
        var button = LineButton.Of(Gfx, style);
        return new Sizes(
            form,
            items,
            MathF.Round(style.Text(32)),
            items.Space(style, 6),
            items.Space(style, 2),
            button.Height,
            MathF.Max(items.Line + items.Space(style, 4), button.Height),
            button);
    }

    /// <summary>
    /// One person's row, its padding in: the name, and the address under it where there is one and
    /// the item style shows it; open, the name and a line for each detail under it.
    /// </summary>
    private float RowHeight(Person person, in Sizes sizes)
    {
        if (ReferenceEquals(person, open))
        {
            return (2 * sizes.Pad) + sizes.Items.Line + sizes.Above + (ContactActs.Details(person).Count * sizes.Detail);
        }
        return (2 * sizes.Pad) + sizes.Items.Line + (Detailed(person, sizes) ? sizes.Items.SmallLine : 0);
    }

    /// <summary>Whether a person's row says their address, number or organization under the name.</summary>
    private static bool Detailed(Person person, in Sizes sizes) => sizes.Items.Preview && person.Detail is { Length: > 0 };

    /// <summary>What the line says where it lists nobody, which is not always the same nobody; empty where it lists somebody.</summary>
    internal string Nothing()
    {
        if (Matches.Count > 0)
        {
            return "";
        }
        if (store.Problem is { } problem)
        {
            return problem;
        }
        if (store.Loading)
        {
            return S.DockContactsReading;
        }
        var typed = box.Text.Trim();
        return typed.Length > 0 ? S.DockContactsNoMatch(typed) : S.DockContactsNone;
    }

    /// <summary>
    /// list: as tall as it is to show every person who answers — the box, a row each, the open one with its details — or the box and why nobody is listed (<see cref="IListContent"/>).
    /// </summary>
    public float ListHeight(float width, in TileStyle style)
    {
        var sizes = SizesOf(style);
        var form = sizes.Form;
        var rows = 0f;
        foreach (var person in Matches)
        {
            rows += sizes.Above + RowHeight(person, sizes);
        }
        return form.Pad + sizes.Box + Under(style, sizes, form.Room(width)) + rows + (Matches.Count > 0 ? LineItem.Foot(sizes.Above) : form.Pad);
    }

    /// <summary>Everyone who answers, where the line lists them: they scroll under the box (ListScroll.cs).</summary>
    public float ScrollHeight(float width, float height, in TileStyle style)
    {
        var sizes = SizesOf(style);
        return Matches.Count == 0 || height + 0.5f < Lists(width, style, sizes) ? 0 : ListHeight(width, style);
    }

    /// <summary>The box stays while the people scroll.</summary>
    public float ScrollHead(float width, float content, in TileStyle style)
    {
        var sizes = SizesOf(style);
        return sizes.Form.Pad + sizes.Box + Under(style, sizes, sizes.Form.Room(width));
    }

    /// <summary>The least a line lists the people at: the box and the first name; under it, the box alone.</summary>
    private float Lists(float width, in TileStyle style, in Sizes sizes) =>
        sizes.Form.Pad + sizes.Box + Under(style, sizes, sizes.Form.Room(width)) + sizes.Above + sizes.Pad + sizes.Items.Line + LineItem.Foot(sizes.Above);

    /// <summary>How tall what stands under the box is: why nobody is listed, else a little room before the first row.</summary>
    private float Under(in TileStyle style, in Sizes sizes, float room)
    {
        if (Nothing() is { Length: > 0 } why)
        {
            var (_, words) = PopupPaint.Fit(Gfx, why, sizes.Form.SmallPx, Weight.Regular, room, HintLines);
            return style.Px(8) + words + style.Px(6);
        }
        return style.Px(4);
    }

    /// <summary>
    /// The line, at whatever height it was given (docs\widgets.md §9), drawn where the sketch draws:
    /// <list type="bullet">
    /// <item>One row: the search box alone, in the middle of it.</item>
    /// <item>Taller: the box (under it why nobody is listed, where nobody is), and the people a row each, the open one with a line per detail; given less than they take, the line is laid out
    /// as tall as all of them and scrolled (ListScroll.cs).</item>
    /// </list>
    /// With <paramref name="targets"/> it writes down where each click lands, and each row's whole box (<see cref="Spot.Block"/>).
    /// </summary>
    private void Lay(LineSketch sketch, D2D_RECT_F card, TileStyle style, List<(Target Target, D2D_RECT_F Box)>? targets)
    {
        var sizes = SizesOf(style);
        var form = sizes.Form;
        var theme = style.Theme;
        var width = TilePaint.Width(card);
        var height = card.bottom - card.top;
        var left = card.left + form.TextLeft;
        var right = card.right - form.Inset;
        var people = Matches;
        var under = Under(style, sizes, form.Room(width));
        var compact = form.Pad + sizes.Box + under + form.Pad;
        var one = height + 0.5f < compact;

        // The box: across the line between its gutters, where the rows' plates stand.
        // Under the last person, its band's half of the room (LineItem.Foot); under the words saying why nobody is, the pad.
        var inner = height - form.Pad - (people.Count > 0 ? LineItem.Foot(sizes.Above) : form.Pad);
        var heights = new float[people.Count];
        for (var index = 0; index < people.Count; index++)
        {
            heights[index] = sizes.Above + RowHeight(people[index], sizes);
        }
        var fill = one ? default : WidgetFill.Of(heights, MathF.Max(0, inner - sizes.Box - under));
        // The box stays at the top however few answer, so it never moves under the caret as the rows
        // come and go; on one row it stands in the middle.
        var y = one ? PopupPaint.Round(card.top + MathF.Max(0, (height - sizes.Box) / 2)) : card.top + form.Pad;
        boxAt = new D2D_RECT_F(form.Inset, y - card.top, width - form.Inset, y - card.top + sizes.Box);
        box.Bounds = boxAt;
        var context = new QuickContext
        {
            Gfx = Gfx,
            Theme = QuickTheme.For(theme),
            Scale = style.Scale,
            TextScale = style.TextScale,
        };
        // The box measures its caret and its hit tests against the card's scale.
        box.Host = context;
        sketch.Placed("search", new D2D_RECT_F(card.left + boxAt.left, card.top + boxAt.top, card.left + boxAt.right, card.top + boxAt.bottom), box.Text);
        var dc = sketch.Device;
        if (dc != null)
        {
            var field = box;
            TilePaint.Placed(dc, card.left + boxAt.left, card.top + boxAt.top, 1, () => field.Paint(dc, context));
        }
        shown.Clear();
        if (one)
        {
            return;
        }
        y += sizes.Box;

        // Under the box: why nobody is listed, where nobody is.
        if (Nothing() is { Length: > 0 } why)
        {
            y += style.Px(8);
            var said = sketch.Wrapped("nothing", why, form.SmallPx, Weight.Regular, theme.TextSecondary, left, y, form.Room(width), HintLines);
            y = said.bottom + style.Px(6);
        }
        else
        {
            y += style.Px(4);
        }

        // The people, a row each: the glyph, the name, the address or the number under it; the open
        // one its details a line each.
        var row = new RowPaint(this, sketch, targets, sizes, style, context, card, left, right);
        for (var index = 0; index < fill.Shown; index++)
        {
            var person = people[index];
            shown.Add(person);
            // Out of sight in a scrolled line: not drawn (CellIsland.Hidden).
            if (sketch.Draws && targets is null && Hidden(y, y + heights[index]))
            {
                y += heights[index];
                continue;
            }
            y += sizes.Above;
            var rowHeight = heights[index] - sizes.Above;
            row.Row(index, person, y, rowHeight);
            y += rowHeight;
        }
        if (open is { } opened && !people.Any(one => ReferenceEquals(one, opened)))
        {
            // Narrowed away: closed, so it does not come back open when the search is undone.
            open = null;
        }
    }

    /// <summary>One layout's way of laying out a person's row: what every row shares, worked out once.</summary>
    private readonly ref struct RowPaint(
        ContactsLine line,
        LineSketch sketch,
        List<(Target Target, D2D_RECT_F Box)>? targets,
        Sizes sizes,
        TileStyle style,
        QuickContext context,
        D2D_RECT_F card,
        float left,
        float right)
    {
        private readonly float cardLeft = card.left;
        private readonly ListForm form = sizes.Form;
        private readonly ItemForm look = sizes.Items;
        private readonly FlyoutTheme theme = style.Theme;
        private readonly float radius = style.Px(4);

        /// <summary>Person <paramref name="index"/>'s row, from <paramref name="top"/>, <paramref name="height"/> px tall.</summary>
        public void Row(int index, Person person, float top, float height)
        {
            // The person with half the room over and half the room under, edge to edge: the next one's begins where it
            // ends (LineItem).
            var rowBox = LineItem.Around(card, top, top + height, sizes.Above);
            targets?.Add((new Target(Spot.Block, person), rowBox));
            var isOpen = ReferenceEquals(person, line.open);
            var hot = line.hot;
            var hotHere = ReferenceEquals(hot.Person, person);
            var ringed = line.keyed && ReferenceEquals(person, line.selected);
            var text = top + sizes.Pad;
            // Open, its name's row down to its first detail.
            var head = isOpen ? rowBox with { bottom = text + look.Line + sizes.Above } : rowBox;
            targets?.Add((new Target(Spot.Row, person), head));

            if (sketch.Draws && isOpen)
            {
                // An open row is a block of its own, as a Windows 11 expander's content: a control's fill, the name
                // its header, as wide as an item.
                sketch.Plate($"open {index}", rowBox, 0, PopupPaint.Ink(theme.ControlFill));
            }
            var lit = hotHere && hot.Spot is Spot.Row || (!isOpen && (hotHere || ringed));
            if (lit)
            {
                LineItem.Light(sketch, head, theme, hotHere && hot.Spot == Spot.Row && line.press.On(line.hotId));
            }
            if (sketch.Draws && ringed && sketch.Device != null)
            {
                // The row the keys are on wears the keys' ring, as a row the keys are on in every line.
                TogglesTile.FocusRing(sketch.Device, new D2D_RECT_F(cardLeft + form.Inset - style.Px(6), top, right + style.Px(6), top + height), radius, style, context.Theme);
            }

            if (look.Pictures && line.store.Photo(person) is { } photo)
            {
                // Their picture, a circle in the leading column as the feed's pictures stand there (the kit's
                // Feature size at the item style's size), on the name and the line under it — never larger than
                // leaves the kit's gap before the name, so Large items' pictures do not touch it.
                var across = MathF.Min(look.Side(LineGlyph.Feature(style)), form.TextLeft - form.Inset - style.Px(LineSpace.GapDip));
                var block = !isOpen && Detailed(person, sizes) ? look.Line + look.SmallLine : look.Line;
                var lead = ListTiers.GlyphBox(form, cardLeft, text, block, across);
                var circle = PopupPaint.Rect(PopupPaint.Round(((lead.left + lead.right) / 2) - (across / 2)), PopupPaint.Round(text + ((block - across) / 2)), across, across);
                sketch.Picture($"photo {index}", circle, (dc, at) => PopupPaint.Covered(dc, photo, at, (at.right - at.left) / 2));
            }
            else
            {
                // People of more than one address book: the glyph in their book's colour (Person.Colour), so
                // two books tell apart without a column more; one book's, and high contrast, in the text's.
                var glyphInk = person.Colour is { } colour && !theme.HighContrast ? CalendarPainter.Solid(CalendarColours.Of(colour), theme) : theme.Text;
                sketch.Glyph($"glyph {index}", line.Info.Glyph, look.Side(form.Glyph), glyphInk, form.IconBox(cardLeft, text, look.Line));
            }
            if (isOpen)
            {
                // The name, and the chevron that closes it at its end; then a line per detail.
                var chevron = PopupPaint.Rect(right - style.Px(4) - sizes.Side, PopupPaint.Round(text + ((look.Line - sizes.Side) / 2)), sizes.Side, sizes.Side);
                sketch.Glyph($"close {index}", CloseGlyph, style.Text(12), theme.TextSecondary, chevron);
                var said = line.SaysCopied(person) && !line.CopiedADetail(person);
                sketch.Text($"name {index}", person.DisplayName, look.TextPx, Weight.Semibold, theme.Text, left, text,
                    MathF.Max(1, chevron.left - left - style.Px(4) - (said ? line.ChipWidth(form, style) : 0)));
                if (said)
                {
                    line.Chip(sketch, $"copied {index}", chevron.left - style.Px(2), PopupPaint.Round(text + ((look.Line - sizes.Side) / 2)), sizes.Side, form, style);
                }
                var y = text + look.Line + sizes.Pad + sizes.Above - sizes.Pad;
                var details = ContactActs.Details(person);
                for (var at = 0; at < details.Count; at++)
                {
                    Detail(index, at, person, details[at], y);
                    y += sizes.Detail;
                }
                return;
            }

            // At rest the name and the detail under it; under the pointer or the keys, Email and Call at
            // its end, each only where the person has one Windows opens; "Copied" there once copied.
            var email = ContactActs.FirstEmail(person);
            var phone = ContactActs.FirstPhone(person);
            var side = MathF.Min(sizes.Side, height);
            var buttonTop = PopupPaint.Round(top + ((height - side) / 2));
            var end = right - style.Px(4);
            var room = right - left - style.Px(8);
            if (line.SaysCopied(person))
            {
                var chip = line.Chip(sketch, $"copied {index}", end, buttonTop, side, form, style);
                room = chip.left - left - style.Px(4);
            }
            else if (hotHere || ringed)
            {
                if (phone is not null)
                {
                    var call = PopupPaint.Rect(end - side, buttonTop, side, side);
                    Button($"call {index}", new Target(Spot.Call, person, phone, DetailKind.Phone), call, ContactActs.PhoneGlyph, true);
                    end = call.left - sizes.Button.Gap;
                }
                if (email is not null)
                {
                    var mail = PopupPaint.Rect(end - side, buttonTop, side, side);
                    Button($"email {index}", new Target(Spot.Email, person, email, DetailKind.Email), mail, ContactActs.MailGlyph, true);
                    end = mail.left - sizes.Button.Gap;
                }
                room = end - left - style.Px(4);
            }
            room = MathF.Max(1, room);
            sketch.Text($"name {index}", person.DisplayName, look.TextPx, Weight.Regular, theme.Text, left, text, room);
            if (Detailed(person, sizes) && person.Detail is { } detail)
            {
                sketch.Text($"detail {index}", detail, look.SmallPx, Weight.Regular, theme.TextSecondary, left, text + look.Line, room);
            }
        }

        /// <summary>
        /// Detail <paramref name="at"/> of an open row: its kind's glyph in the leading column, its value,
        /// and at its end Email or Call where it is one Windows opens, and Copy — or "Copied", once it was.
        /// </summary>
        private void Detail(int index, int at, Person person, ContactDetail detail, float top)
        {
            var name = $"{index}.{at}";
            var lineBox = new D2D_RECT_F(cardLeft + form.Inset + style.Px(4), top, right - style.Px(4), top + sizes.Detail - look.Space(style, 2));
            var target = new Target(Spot.Detail, person, detail.Value, detail.Kind);
            // A detail is an item of the open person's: edge to edge, each the next's edge (LineItem).
            var band = LineItem.Band(card, top, top + sizes.Detail);
            targets?.Add((target, band));
            var hot = line.hot;
            var hotHere = ReferenceEquals(hot.Person, person) && hot.Value == detail.Value && hot.Kind == detail.Kind && hot.Spot is Spot.Detail or Spot.Copy or Spot.Email or Spot.Call;
            if (hotHere && hot.Spot == Spot.Detail)
            {
                LineItem.Light(sketch, band, theme, line.press.On(line.hotId));
            }
            var middle = (lineBox.top + lineBox.bottom) / 2;
            var words = PopupPaint.Round(middle - (look.Line / 2));
            sketch.Glyph($"kind {name}", detail.Glyph, PopupPaint.Round(look.Side(form.Glyph) * 0.75f), theme.TextSecondary, form.IconBox(cardLeft, words, look.Line));
            var side = MathF.Min(sizes.Side, lineBox.bottom - lineBox.top);
            var buttonTop = PopupPaint.Round(middle - (side / 2));
            var end = lineBox.right - style.Px(2);
            if (line.SaysCopied(person, detail.Value))
            {
                end = line.Chip(sketch, $"copied {name}", end, buttonTop, side, form, style).left - style.Px(4);
            }
            else
            {
                var copy = PopupPaint.Rect(end - side, buttonTop, side, side);
                Button($"copy {name}", target with { Spot = Spot.Copy }, copy, ContactActs.CopyGlyph, hotHere);
                end = copy.left - sizes.Button.Gap;
                // Email or Call before it — where the words keep room to be read: at a large text
                // size a line too narrow for both keeps Copy, and the row's menu still writes or calls.
                var cramped = end - side - left < MathF.Min(style.Text(140), PopupPaint.Measure(line.Gfx, detail.Value, look.TextPx, Weight.Regular).X);
                if (detail.Uri is not null && !cramped)
                {
                    var open = PopupPaint.Rect(end - side, buttonTop, side, side);
                    var phone = detail.Kind == DetailKind.Phone;
                    Button($"{(phone ? "call" : "email")} {name}", target with { Spot = phone ? Spot.Call : Spot.Email }, open, phone ? ContactActs.PhoneGlyph : ContactActs.MailGlyph, hotHere);
                    end = open.left - sizes.Button.Gap;
                }
                end -= style.Px(2);
            }
            sketch.Text($"value {name}", detail.Value, look.TextPx, Weight.Regular, theme.Text, left, words, MathF.Max(1, end - left));
        }

        /// <summary>A subtle glyph button (LineButton): lit under the pointer, pressed while held; its glyph full strength where its line is lit, quieter elsewhere.</summary>
        private void Button(string name, Target target, D2D_RECT_F at, string glyph, bool strong)
        {
            targets?.Add((target, at));
            var lit = line.hot == target;
            sketch.Button(name, sizes.Button, theme, at, "", glyph, ButtonStyle.Subtle, lit, lit && line.press.On(line.hotId), dimmed: !strong && !lit);
        }

    }

    /// <summary>How wide the "Copied" chip is.</summary>
    private float ChipWidth(in ListForm form, in TileStyle style) =>
        style.Px(8) + style.Text(14) + style.Px(6) + PopupPaint.Measure(Gfx, S.DockClipboardCopied, form.TextPx, Weight.Regular).X + style.Px(8);

    /// <summary>
    /// The chip a line wears while it says it was copied, ending at <paramref name="end"/>: the
    /// accent's check and "Copied" on a control's fill over the flyout's own solid colour, as the
    /// clipboard's rows wear it. Its box.
    /// </summary>
    private D2D_RECT_F Chip(LineSketch sketch, string name, float end, float top, float side, in ListForm form, in TileStyle style)
    {
        var theme = style.Theme;
        var inset = style.Px(8);
        var glyph = style.Text(14);
        var words = PopupPaint.Measure(Gfx, S.DockClipboardCopied, form.TextPx, Weight.Regular);
        var chip = new D2D_RECT_F(end - ChipWidth(form, style), top, end, top + side);
        var radius = style.Px(4);
        if (sketch.Draws)
        {
            sketch.Plate($"{name} base", chip, radius, PopupPaint.Ink(theme.Base));
            sketch.Plate($"{name} plate", chip, radius, PopupPaint.Ink(theme.ControlFill));
            PopupPaint.Stroke(sketch.Device, chip, radius, MathF.Max(1, style.Px(1)), PopupPaint.Ink(theme.ControlStroke));
        }
        sketch.Glyph($"{name} check", CopiedGlyph, glyph, TilePaint.Accent(style), PopupPaint.Rect(chip.left + inset, top + PopupPaint.Round((side - glyph) / 2), glyph, glyph));
        sketch.Text(name, S.DockClipboardCopied, form.TextPx, Weight.Regular, theme.Text, chip.left + inset + glyph + style.Px(6), top + PopupPaint.Round((side - form.Line) / 2), words.X + 1);
        return chip;
    }
}
