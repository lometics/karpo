// menus: text and search fields as rows of a menu (2026-10-19, the person: "inline every setting of
// every bar widget and folder widget into its edit menu, so configuring happens in the menu";
// docs\widgets.md §219). The field itself is MenuTextField's, a search's live results MenuSearch's;
// this is how a menu holds them:
//
//  - **The row** (FlyoutField) is one row of the menu, as tall as every other (§240, the person: "all
//    of them should not be bigger than a single entry"; §219 to §239 had the words over a 32 DIP box,
//    two rows), on a level of its own: Windows 11's text box fills the row, its placeholder saying what
//    to type; the row it opens from names it and says what it holds (§246, the person: "the text box
//    inputs / configuration items should have an additional level and should show the text box entry
//    on that additional level. it should autofocus when opened"; §240 to §245 had a line of text's box
//    on 55 % of the row beside its name). It lights no plate under the pointer, as a text box does not;
//    the box lightens. Greyed (a place while the device's location stands instead) it takes no keys and
//    shows no results, their room kept.
//  - **A level opened** — by a click or the keys — gives its field the keys at once, its words
//    selected, the caret at their end (FocusFieldLevel). On a line of text's level (TextLevel) Enter
//    keeps what was typed and Escape puts the setting's words back, and either shuts the level, the
//    keys back on its row (CloseLevel); a search's level stays for its results, as before.
//  - **A line of text's words are its value** (Line, §332), whatever it offers under it (§304's
//    suggestions, an amount's): Enter keeps them, empty too — empty is the setting's default —, and
//    so does leaving it any other way — another row, its level shut, the menu closed —, as the page's
//    box keeps them as it loses the focus; a suggestion picked is kept and shuts the level as Enter
//    does. Only Escape puts the setting back. A search's words (a zone, a place, a share, a feed, what
//    a pin opens) only find: left, they go back to what the setting says (LetGo). §304 to §328 had a
//    line with suggestions a search, so a label cleared and left came back.
//  - **The keys.** A click in the box puts the keys there with the caret where it landed (a drag
//    selects, a double click selects the word); the arrows bringing the keys onto the row do too, a
//    search's words selected so typing replaces them. While a field has the keys every key is its
//    own: the menu's arrows, its shortcuts and its accelerators hear none (MenuTextField.Key); Up and
//    Down go on to the rows over and under it. Escape puts the setting's words back, then lets the
//    keys go, and only then is the menu's again. Enter keeps what was typed. A field stands only in a
//    menu whose levels open in place (Flyout.Inline.cs), so the keys are always its own window's.
//  - **Typing in another script** composes in the IME's window, put at the caret, and arrives as
//    characters, as in the open folder's notes (FolderFlyout.PlaceComposition).
//  - **A search's results** are rows of the menu under its field, each a pick, its words and quieter
//    ones at its end as a row's value (§240: one row each, no line under them), the one the setting holds
//    ticked in the slot every result keeps for it (§304); a greyed row says in its first sentence what the search is doing or why there is nothing. Down goes into them, and Enter or a click picks; typing or Backspace
//    on one goes back to the field. The menu is shown again in place as they come and go (Refresh).
//  - **Every result, in one room** (§271, the person: "the lists with the x entries shown should not be
//    limited anymore, they should show all the items with the fixed height so that the list is just
//    scrollable in the end"; §228 to §270 showed five and a "Show more (N)"): a search's level is
//    its head, its field and room for ten rows under it (§286), a configuration menu's its one height (§288), the field standing still at its top, every result a scroll away (Flyout.Inline.cs); "Searching…" and "nothing found" stand in it too, and the menu keeps its
//    size as they come and go.
//  - **A filter** (FlyoutField.Filters: an icon picker's, §249, and a long list's, Flyout.Inline.cs)
//    keeps nothing: what is typed in it leaves the tiles or the rows of its level whose words hold it
//    (Flyout.FilterWords, IconSearch.Matches), without their headings and rules, a greyed row saying so
//    where none do. It has the keys as its level opens; Enter or Down go on to the first tile or row,
//    and typing or Backspace on one go on in it.
//  - **What was kept** is the menu's to tell: the root says FlyoutEventKind.Adjust for the field's
//    row, and whoever showed the menu takes the words or the pick (TakeFieldCommit) and sets them as
//    Settings sets them (Dock.MenuFields.cs), and shows the menu again with the setting as it is now.

using System.Collections.Concurrent;
using System.Numerics;
using Karpo.Core;
using Karpo.Dock.Native;
using Karpo.Dock.QuickSettings.Controls;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using Windows.UI.Composition;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

/// <summary>
/// menus: a text field a row holds under its words (Flyout.Fields.cs): <paramref name="Text"/> what
/// the setting says now, <paramref name="Placeholder"/> what it says empty, <paramref name="Longest"/>
/// the most it holds; a search field where it has a <paramref name="Finder"/>. One that
/// <paramref name="Filters"/> (an icon picker's, §249) keeps nothing: what is typed in it leaves the
/// tiles of its level whose words hold it (<see cref="FlyoutItem.Words"/>), <paramref name="NoMatch"/>
/// said where none do. §338: one <paramref name="Masked"/> is a password's (MenuTextField.Masked): bullets,
/// nothing to the clipboard, no IME, a screen reader told it is a password and never its words, and
/// what it holds wiped as it goes; its <paramref name="Text"/> is always empty.
/// </summary>
public sealed record FlyoutField(string Text, string Placeholder = "", int Longest = int.MaxValue, OptionFinder? Finder = null, bool Filters = false, string NoMatch = "", bool Masked = false) : FlyoutControl
{
    /// <summary>
    /// §411 (R6): a Find field — a search over many things (a zone, a place, a share), or a filter (an icon
    /// picker's) —: it opens empty, "Search …", with a magnifier, what it offers listed before anything is
    /// typed, the setting's own ticked at the top. Else a Type field — a line of text, what it offers only
    /// suggesting —: it opens with the setting's words, no magnifier, and Enter keeps what is typed.
    /// </summary>
    public bool Finds => Filters || Finder is { Finds: true, Suggests: false };

    /// <summary>
    /// §523: a colour picker's hex box (MenuPickers.Mixer): a line whose words are a colour, its row's swatch the colour it
    /// says, drawn before its box; it follows the colour as the spectrum and the sliders are dragged, and keeps its level.
    /// </summary>
    public bool Hex { get; init; }
}

/// <summary>menus: what a menu's field kept (<see cref="Flyout.TakeFieldCommit"/>): the words typed, and a search's pick, proved.</summary>
public sealed record FieldCommit(uint Id, string Text, OptionFound? Found);

public sealed unsafe partial class Flyout
{
    // Posted to a menu's window by a search answering off its thread: the answers wait in fieldPosts.
    private const uint WM_KARPO_FLYOUT_FIELD = WM.WM_USER + 0x72;

    private const nuint FieldBlinkTimer = 6;
    private const nuint FieldSearchTimer = 7;

    /// <summary>A search's result rows' ids, from here on: past every command's.</summary>
    private const uint FieldResultIds = 0xFFF0_0000;

    /// <summary>A field of this showing: what it holds, its search, and where its box is drawn.</summary>
    private sealed class FieldState(MenuTextField field)
    {
        public MenuTextField Field { get; } = field;

        public MenuSearch? Search { get; set; }

        public FlyoutField Control { get; set; } = new("");

        public CompositionDrawingSurface? Surface { get; set; }

        public FieldLook Look { get; set; }

        /// <summary>The box from its row's fill: in from its left and its right, down from its top, and its size.</summary>
        public (float Left, float Right, float Top, float W, float H) InFill { get; set; }
    }

    private readonly Dictionary<uint, FieldState> fieldStates = [];
    private readonly Dictionary<uint, (uint Field, OptionFound Found)> fieldResults = [];

    private readonly ConcurrentQueue<Action> fieldPosts = new();

    // The field with the keys, by its row's id; 0 for none.
    private uint fieldKeys;

    // The root's: what the fields kept, for whoever showed the menu, each said (Adjust) once.
    private readonly List<(FieldCommit Kept, bool Told)> fieldCommits = [];

    // A drag in the box selecting, and the last click there, for a double click.
    private bool fieldSelecting;
    private long fieldClickAt;
    private POINT fieldClickPoint;

    /// <summary>menus: the field with the keys, by its row's id; 0 for none. For tests.</summary>
    internal uint FieldKeys => fieldKeys;

    /// <summary>menus, for tests: what the field of row <paramref name="id"/> holds now; null where there is none.</summary>
    internal MenuTextField? FieldOf(uint id) => fieldStates.GetValueOrDefault(id)?.Field;

    /// <summary>menus, for tests: the search of the field of row <paramref name="id"/>; null where it has none.</summary>
    internal MenuSearch? FieldSearchOf(uint id) => fieldStates.GetValueOrDefault(id)?.Search;

    /// <summary>
    /// menus: what the field of row <paramref name="id"/> kept — the words, or a search's pick —, taken
    /// once, for the <see cref="FlyoutEventKind.Adjust"/> it said; null where it kept nothing.
    /// </summary>
    public FieldCommit? TakeFieldCommit(uint id)
    {
        var commits = fieldCommits;
        var at = commits.FindIndex(commit => commit.Kept.Id == id);
        if (at < 0)
        {
            return null;
        }
        var kept = commits[at].Kept;
        commits.RemoveAt(at);
        return kept;
    }

    /// <summary>
    /// menus: what the fields kept and nobody took, the menu closed — a text field's words typed and
    /// left there as it closed by a click away, which the page's box would keep as it lost the focus —,
    /// taken once.
    /// </summary>
    public IReadOnlyList<FieldCommit> TakeFieldsLeft()
    {
        var commits = fieldCommits;
        List<FieldCommit> left = [.. commits.Select(commit => commit.Kept)];
        commits.Clear();
        return left;
    }

    // The items as last shown, a search's rows among them, for tests.
    private IReadOnlyList<FlyoutItem> fieldShowing = [];

    /// <summary>menus, for tests: what this menu shows, a search's rows and its greyed row among them.</summary>
    internal IReadOnlyList<FlyoutItem> FieldShowing => fieldShowing;

    /// <summary>menus, for tests: the middle of the field's box on row <paramref name="id"/>, in window px; null where there is none.</summary>
    internal POINT? FieldBoxCentre(uint id)
    {
        if (!IsOpen || rows.FirstOrDefault(row => row.Item.Id == id) is not { } found || FieldBox(found) is not { } box)
        {
            return null;
        }
        return new POINT((int)MathF.Round(viewport.X + box.X + (box.W / 2)), (int)MathF.Round(viewport.Y + box.Y + (box.H / 2) - scroll));
    }

    // --- Showing them ------------------------------------------------------------------------------

    /// <summary>
    /// <paramref name="items"/> with each search's rows under its field, and each field's state kept
    /// from the last showing by its row's id — what is typed in it, the keys — or made; a field gone
    /// from the menu is forgotten.
    /// </summary>
    private IReadOnlyList<FlyoutItem> WithFields(IReadOnlyList<FlyoutItem> items)
    {
        fieldResults.Clear();
        // A new showing (not one shown again in place) owes nothing of the last one's.
        if (parent is null && !IsOpen)
        {
            fieldCommits.Clear();
        }
        if (!items.Any(item => item.Control is FlyoutField))
        {
            if (fieldStates.Count > 0)
            {
                EndFields();
            }
            return items;
        }
        var next = FieldResultIds;
        var seen = new HashSet<uint>();
        var shown = new List<FlyoutItem>(items.Count + 8);
        foreach (var item in items)
        {
            shown.Add(item);
            if (item.Control is not FlyoutField field)
            {
                continue;
            }
            seen.Add(item.Id);
            if (!fieldStates.TryGetValue(item.Id, out var state))
            {
                state = new FieldState(new MenuTextField(field.Text, field.Longest, field.Masked));
                fieldStates[item.Id] = state;
            }
            else
            {
                state.Field.Show(field.Text, browsing: state.Search is { Open: true } || field.Filters);
            }
            state.Control = field;
            // A search made afresh where the setting changed under it (a new finder), unless it is in use:
            // showing what was typed, asking, or proving a pick.
            if (field.Finder is { } finder && (state.Search is null || (!ReferenceEquals(state.Search.Finder, finder) && !InUse(state.Search))))
            {
                state.Search?.Close();
                state.Search = NewSearch(finder);
            }
            // §240: a field greyed (a place while the device's location stands instead) has no keys and
            // no rows; its level keeps its size as it is greyed and back (Flyout.Inline.cs, §271).
            if (!item.Enabled)
            {
                if (fieldKeys == item.Id)
                {
                    fieldKeys = 0;
                    state.Field.Blur();
                }
                state.Search?.Close();
                continue;
            }
            // §230: a search's own level shows its rows always, from the moment it opens: what the
            // finder offers for nothing typed.
            if (SearchLevel(field) && state.Search is { Open: false } closed)
            {
                closed.Opened();
            }
            if (state.Search is not { Open: true } search)
            {
                continue;
            }
            // An icon where the finder gives one (an icon search's, a pin's folders, a feed's). §304: the
            // one the setting holds ticked at the row's far end, in the slot every result keeps for it, so
            // nothing moves as the ticked one comes and goes; a search stands on a level of its own (§230).
            // §271: every one of them, the level scrolling where they pass its room.
            var group = "";
            foreach (var found in search.Rows)
            {
                // §380: a heading over each group of them where they come in groups (a pin's folders, its apps).
                if (found.Group.Length > 0 && found.Group != group)
                {
                    shown.Add(new FlyoutItem(0, found.Group, Header: true));
                }
                group = found.Group;
                // §240: one row each, its quieter words at its end as a row's value (a country, an offset).
                var id = next++;
                fieldResults[id] = (item.Id, found);
                shown.Add(new FlyoutItem(id, found.Title, FoundIcon(found), Checked: found.Chosen, Value: found.Detail.Length > 0 ? new FlyoutValue(found.Detail) : null));
            }
            if (search.Said is { Length: > 0 } said)
            {
                // Its first sentence, one row (§240): the rest says what to do, which the row shows.
                shown.Add(new FlyoutItem(0, Sentences(said).First, Enabled: false));
            }
        }
        // §249: a filter leaves the tiles or the rows under it whose words hold what is typed in it,
        // without their rules; §556 a heading stays over what it leaves under it (a font's family over its
        // weights), and goes with none; nothing matching says so in a greyed row.
        if (shown.FindIndex(item => item.Control is FlyoutField { Filters: true }) is var at and >= 0 && shown[at] is { Control: FlyoutField filter } filtering
            && fieldStates.TryGetValue(filtering.Id, out var filtered) && filtered.Field.Text.Trim() is { Length: > 0 } query)
        {
            List<FlyoutItem> left = [.. shown.Take(at + 1)];
            FlyoutItem? heading = null;
            foreach (var item in shown.Skip(at + 1))
            {
                if (item.Header)
                {
                    heading = item;
                }
                else if (Filtered(item) && IconSearch.Matches(FilterWords(item), query))
                {
                    if (heading is not null)
                    {
                        left.Add(heading);
                        heading = null;
                    }
                    left.Add(item);
                }
            }
            shown = left;
            if (shown.Count == at + 1)
            {
                shown.Add(new FlyoutItem(0, filter.NoMatch, Enabled: false));
            }
        }
        fieldShowing = shown;
        foreach (var gone in fieldStates.Keys.Where(id => !seen.Contains(id)).ToList())
        {
            // A line left with words typed in it, its level shut from a row of its suggestions: kept (§332).
            var left = fieldStates[gone];
            left.Search?.Close();
            if (Line(left.Control) && left.Field.Dirty && fieldCommits.TrueForAll(commit => commit.Kept.Id != gone))
            {
                fieldCommits.Add((new FieldCommit(gone, left.Field.Text, null), false));
                _ = PostMessageW(Hwnd, WM_KARPO_FLYOUT_FIELD, default, default);
            }
            WipeMasked([left]);
            fieldStates.Remove(gone);
        }
        if (fieldKeys != 0 && !fieldStates.ContainsKey(fieldKeys))
        {
            fieldKeys = 0;
        }
        return shown;
    }

    /// <summary>
    /// §369: where a found row's pictures come from — an address and whether the menu is dark in, the
    /// picture out, null while it comes (the action is called, off the menu's thread, once it is in):
    /// the sports logos (Dock.cs sets it). Null: no pictures, the glyphs alone.
    /// </summary>
    internal static Func<string, bool, Action, Pixels?>? Pictures { get; set; }

    /// <summary>
    /// A found row's icon: its picture where one is given and in (§369, a team's logo), else its glyph;
    /// a picture still to come shows the menu again once it is in.
    /// </summary>
    private FlyoutIcon? FoundIcon(OptionFound found)
    {
        if (found.Picture.Length > 0 && Pictures is { } pictures && pictures(found.Picture, theme.Dark, PictureCame) is { } pixels)
        {
            return new FlyoutIcon.Picture(pixels);
        }
        return found.Glyph.Length > 0 ? new FlyoutIcon.Glyph(found.Glyph) : null;
    }

    /// <summary>§369: a found row's picture came, off the menu's thread: the menu is shown again with it, as a search's answer is.</summary>
    private void PictureCame()
    {
        fieldPosts.Enqueue(() => fieldsChanged = true);
        if (Hwnd is var window && window != HWND.NULL)
        {
            _ = PostMessageW(window, WM_KARPO_FLYOUT_FIELD, default, default);
        }
    }

    /// <summary>Whether <paramref name="search"/> is in use: rows for words typed, a search asked or due, a pick being proved.</summary>
    private static bool InUse(MenuSearch search) =>
        search.Open && (search.Query.Length > 0 || search.Phase is SearchPhase.Waiting or SearchPhase.Searching or SearchPhase.Checking or SearchPhase.Refused);

    /// <summary>
    /// §230: whether <paramref name="field"/> is a search on a level of its own, as every widget's search
    /// stands (OptionMenu, Flyout.Inline.cs), which shows its rows from the moment it opens and gives its
    /// field the keys as it does (<see cref="FocusFieldLevel"/>).
    /// </summary>
    private bool SearchLevel(FlyoutField field) => OnInlineLevel && (field.Finder?.Finds == true || field.Filters);

    /// <summary>
    /// §246: whether <paramref name="field"/> is a line of text on a level of its own — a name, a label,
    /// an amount —, which Enter and Escape shut (<see cref="CloseLevel"/>); a search keeps its level open
    /// for its results, and an icon picker's filter its tiles (§249). §411: a form's fields, several on its level,
    /// keep it open: Enter keeps the words and the form stands for the next field. §523: a colour's hex box keeps its
    /// picker open, the colour typed shown in it.
    /// </summary>
    private bool TextLevel(FlyoutField field) => OnInlineLevel && Line(field) && !field.Hex && rows.Count(row => row.Item.Control is FlyoutField) == 1;

    /// <summary>
    /// §332: whether <paramref name="field"/> is a line of text, whose words are its value — with nothing
    /// under it, or what it offers only suggesting (<see cref="OptionFinder.Suggests"/>) —, rather than a
    /// search's words, which only find, or a filter's.
    /// </summary>
    private static bool Line(FlyoutField field) => !field.Filters && field.Finder?.Suggests != false;

    /// <summary>
    /// §332: the field of row <paramref name="id"/> is left — the keys elsewhere, its rows gone —: a line's
    /// words typed are kept, as the page's box keeps them as it loses the focus; a search's go back to
    /// what the setting says; a filter's stay in it, filtering (§249).
    /// </summary>
    private void LetGo(uint id, FieldState state)
    {
        if (state.Control.Filters)
        {
            return;
        }
        if (!Line(state.Control))
        {
            state.Field.Revert();
        }
        else if (state.Field.Dirty)
        {
            Kept(id, state, null);
        }
    }

    /// <summary>
    /// §230, §246 (the person: "if someone opens the submenu it should focus the textbox automatically";
    /// "it should autofocus when opened"): a field's level just opened — by a click or the keys — gives
    /// its field the keys, its words selected so typing replaces them, as the keys onto its row would;
    /// Down goes on into a search's results. The keys only: the menu takes no activation it did not have.
    /// Whether it had one. An icon picker's too, its filter over its tiles (§249: §228 to §248 started
    /// the keys on its default); no other level with tiles (a colour's).
    /// </summary>
    private bool FocusFieldLevel()
    {
        var at = rows.FindIndex(row => row.Item.Control is FlyoutField && row.Item.Enabled && fieldStates.ContainsKey(row.Item.Id));
        if (at < 0 || (rows.Exists(row => IsTile(row.Item)) && rows[at].Item.Control is not FlyoutField { Filters: true }))
        {
            return false;
        }
        Focus(at);
        return fieldKeys == rows[at].Item.Id;
    }

    /// <summary>A search of this menu, its answers brought back to this menu's window.</summary>
    private MenuSearch NewSearch(OptionFinder finder) => new(
        finder,
        answer =>
        {
            // The window as it is when the answer comes: a submenu's is made as it is first shown.
            fieldPosts.Enqueue(answer);
            if (Hwnd is var window && window != HWND.NULL)
            {
                _ = PostMessageW(window, WM_KARPO_FLYOUT_FIELD, default, default);
            }
        },
        () => fieldsChanged = true);

    // An answer came in: the menu is shown again once every waiting answer is taken.
    private bool fieldsChanged;

    /// <summary>"No place called “x” was found. Pick another." → its first sentence, and the rest (null for none).</summary>
    internal static (string First, string? Then) Sentences(string said)
    {
        foreach (var end in (string[])[". ", "。", "۔ ", "। "])
        {
            var at = said.IndexOf(end, StringComparison.Ordinal);
            if (at > 0 && at + end.Length < said.Length)
            {
                return (said[..(at + end.Length)].TrimEnd(), said[(at + end.Length)..].TrimStart());
            }
        }
        return (said, null);
    }

    /// <summary>
    /// §246: whether <paramref name="item"/> is a field's row, which its box fills, no name beside it: the
    /// row its level opens from names it, and its placeholder says what to type.
    /// </summary>
    private static bool FieldFillsRow(FlyoutItem item) => item.Control is FlyoutField;

    /// <summary>
    /// The field of <paramref name="row"/> drawn into <paramref name="into"/>, one row tall (§240, the
    /// person: "all of them should not be bigger than a single entry"), across the row's fill (§246);
    /// greyed where the row is.
    /// </summary>
    private void DrawField(Compositor c, ContainerVisual into, Row row, float textPx, float tall)
    {
        if (!fieldStates.TryGetValue(row.Item.Id, out var state) || gfx is not { } g)
        {
            return;
        }
        var fill = row.Bounds;
        // §415: the box the row's top, <paramref name="tall"/>, where a line under it says what it needs.
        var box = fill with { H = tall };
        // §523: a colour's hex box: its swatch first, the colour its words say, the box after it to the picker's end (Flyout.Mixer.cs).
        if (state.Control.Hex)
        {
            var swatch = MixBox(null, fill, scale);
            DrawHexSwatch(c, into, row, swatch, ColorMix.ParseHex(state.Field.Text, MixAlpha) ?? MixSaid ?? default);
            var left = swatch.X + swatch.W + MathF.Round(HexGapDip * scale);
            box = new Area(left, fill.Y, fill.X + fill.W - MathF.Round(MixInsetDip * scale) - left, tall);
        }
        state.InFill = (box.X - fill.X, fill.X + fill.W - (box.X + box.W), box.Y - fill.Y, box.W, box.H);
        state.Look = new FieldLook(new Vector2(box.W, box.H), scale, textPx, state.Control.Placeholder, mirrored, state.Control.Finds);
        if (state.Field.Focused)
        {
            state.Field.KeepCaretInView(g, state.Look);
        }
        var surface = Own(g.Draw(box.W, box.H, dc => PaintField(state, dc)));
        var sprite = Own(c.CreateSpriteVisual());
        sprite.Brush = Own(c.CreateSurfaceBrush(surface));
        sprite.Size = new Vector2(box.W, box.H);
        sprite.Offset = new Vector3(box.X, box.Y, 0);
        if (!row.Item.Enabled)
        {
            sprite.Opacity = 0.4f;
        }
        into.Children.InsertAtTop(sprite);
        row.Parts.Add((sprite, sprite.Offset));
        state.Surface = surface;
    }

    private void PaintField(FieldState state, ID2D1DeviceContext* dc)
    {
        if (gfx is { } g)
        {
            state.Field.Paint(dc, g, QuickTheme.For(look), state.Look);
        }
    }

    private void RedrawField(FieldState state)
    {
        if (state.Surface is { } surface && gfx is { } g && IsOpen)
        {
            Try(() => g.Redraw(surface, dc => PaintField(state, dc)));
            // §523: a hex box's swatch shows what its words say as they are typed.
            HexTyped(state);
        }
    }

    /// <summary>The field's box on <paramref name="row"/>, in content px, turned with the row in a mirrored menu; null for a row with none.</summary>
    private Area? FieldBox(Row row)
    {
        if (!fieldStates.TryGetValue(row.Item.Id, out var state) || state.InFill.W <= 0)
        {
            return null;
        }
        var (left, right, top, w, h) = state.InFill;
        return new Area(row.Bounds.X + (mirrored ? right : left), row.Bounds.Y + top, w, h);
    }

    /// <summary>The rows are in: the caret blinks where a field has the keys.</summary>
    private void FieldsShown()
    {
        if (fieldKeys != 0)
        {
            FieldBlinking(true);
        }
    }

    /// <summary>§338: a password's fields of <paramref name="states"/> going, what they hold wiped once what they kept is the menu's.</summary>
    private static void WipeMasked(IEnumerable<FieldState> states)
    {
        foreach (var state in states)
        {
            if (state.Field.Masked)
            {
                state.Field.Wipe();
            }
        }
    }

    /// <summary>
    /// §338: the IME on this menu's window, or off while a password's field has the keys, as Windows'
    /// PasswordBox has none: its composition would show the password in its own window.
    /// </summary>
    private void Composing(bool on)
    {
        if (Hwnd != HWND.NULL)
        {
            _ = ImmAssociateContextEx(Hwnd, HIMC.NULL, on ? IACE_DEFAULT : 0u);
        }
    }

    /// <summary>The showing is over: no field, no search, no blink.</summary>
    private void EndFields()
    {
        foreach (var (id, state) in fieldStates)
        {
            state.Search?.Close();
            // A line's words typed and left there: kept, as the page's box keeps them as it loses the focus
            // (§332: one with suggestions too); a search's and a filter's are nobody's (§249).
            if (Line(state.Control) && state.Field.Dirty && fieldCommits.TrueForAll(commit => commit.Kept.Id != id))
            {
                fieldCommits.Add((new FieldCommit(id, state.Field.Text, null), true));
            }
        }
        WipeMasked(fieldStates.Values);
        fieldStates.Clear();
        fieldResults.Clear();
        fieldKeys = 0;
        fieldSelecting = false;
        if (Hwnd != HWND.NULL)
        {
            _ = KillTimer(Hwnd, FieldBlinkTimer);
            _ = KillTimer(Hwnd, FieldSearchTimer);
        }
        while (fieldPosts.TryDequeue(out _))
        {
        }
    }

    /// <summary>
    /// The menu shown again in place for its fields' rows, with the rows whoever showed it last handed it
    /// (Refresh); row <paramref name="lit"/> lit, where its words changed.
    /// </summary>
    private void RelayoutFields(uint? lit = null)
    {
        if (IsOpen && gfx is not null && shownWith is { } shown)
        {
            Reshow(shown.Items, lit, picked: false);
        }
    }

    // --- The keys -----------------------------------------------------------------------------------

    /// <summary>The field takes the keys (see the top of the file), from any other field of the menu.</summary>
    private void FocusField(uint id, bool selectAll)
    {
        if (!fieldStates.TryGetValue(id, out var state))
        {
            return;
        }
        if (fieldKeys == id)
        {
            return;
        }
        if (fieldKeys != 0)
        {
            BlurField(closeSearch: false);
        }
        fieldKeys = id;
        state.Field.Focus(selectAll);
        Composing(on: !state.Field.Masked);
        if (!selectAll)
        {
            state.Field.Editing.End(extend: false);
        }
        // Every other search's rows go; this one's show.
        var changed = CloseSearches(but: id);
        if (state.Search is { Open: false } search)
        {
            search.Opened();
            changed = true;
        }
        CaretMoved(state);
        if (GetForegroundWindow() == Hwnd)
        {
            Uia.FocusMoved(Hwnd);
        }
        if (changed)
        {
            RelayoutFields();
        }
    }

    /// <summary>Every search's rows in this menu gone but the field <paramref name="but"/>'s; whether any went.</summary>
    private bool CloseSearches(uint but)
    {
        var closed = false;
        foreach (var (id, state) in fieldStates)
        {
            if (id != but && state.Search is { Open: true } search)
            {
                // Left without a pick: a search's words go back, a line's are kept (LetGo).
                search.Close();
                LetGo(id, state);
                closed = true;
            }
        }
        return closed;
    }

    /// <summary>The field with the keys lets them go: its caret and selection gone, and its search's rows where <paramref name="closeSearch"/>.</summary>
    private void BlurField(bool closeSearch)
    {
        if (!fieldStates.TryGetValue(fieldKeys, out var state))
        {
            fieldKeys = 0;
            return;
        }
        var id = fieldKeys;
        fieldKeys = 0;
        state.Field.Blur();
        Composing(on: true);
        FieldBlinking(false);
        // Left, where it has no rows to go on into or they go too: a line's words kept, a search's back (LetGo).
        var rowsGo = closeSearch && state.Search is { Open: true };
        if (closeSearch || state.Search is null)
        {
            state.Search?.Close();
            LetGo(id, state);
        }
        RedrawField(state);
        if (rowsGo)
        {
            RelayoutFields();
        }
    }

    /// <summary>
    /// A key at the root, for a field (see the top of the file): the field with the keys takes it;
    /// on a field's row without them, Enter, Space and F2 give them to it; on a search's result, Escape
    /// goes back to its field, and Backspace to its field and on in it. Null where the menu takes it.
    /// </summary>
    private FlyoutEvent? FieldKeyDown(int key)
    {
        if (fieldKeys == 0)
        {
            var lit = hot >= 0 && hot < rows.Count ? rows[hot].Item : null;
            if (lit?.Control is FlyoutField && key is VK.VK_RETURN or VK.VK_SPACE or VK.VK_F2)
            {
                FocusField(lit.Id, selectAll: true);
                return default(FlyoutEvent);
            }
            // A tile's Escape is the menu's: it shuts the picker (§249).
            if (lit is not null && ResultsField(lit.Id) is var resultField and not 0 && (key == VK.VK_BACK || (key == VK.VK_ESCAPE && !IsTile(lit))))
            {
                FocusField(resultField, selectAll: false);
                if (key == VK.VK_ESCAPE)
                {
                    return default(FlyoutEvent);
                }
            }
            if (fieldKeys == 0)
            {
                return null;
            }
        }
        if (!fieldStates.TryGetValue(fieldKeys, out var state))
        {
            return null;
        }
        var window = Hwnd;
        var did = state.Field.Key(key, KeyState.Down(VK.VK_SHIFT), KeyState.Down(VK.VK_CONTROL), () => Clipboard.GetText(window), text => Clipboard.SetText(window, text));
        FieldDid(state, did);
        return default(FlyoutEvent);
    }

    /// <summary>A character typed at the root: into the field with the keys, or the one whose result row is lit, which takes them back; null for none.</summary>
    private FlyoutEvent? FieldChar(char c)
    {
        // On a search's result the keys go back to its field, and on in it; a field's row itself is
        // typed in only once it has the keys (a click, the arrows, Enter), never under a pointer alone.
        if (fieldKeys == 0 && !char.IsControl(c)
            && hot >= 0 && hot < rows.Count
            && ResultsField(rows[hot].Item.Id) is var resultField and not 0)
        {
            FocusField(resultField, selectAll: false);
        }
        if (fieldKeys == 0 || !fieldStates.TryGetValue(fieldKeys, out var state))
        {
            return null;
        }
        FieldDid(state, state.Field.Char(c));
        return default(FlyoutEvent);
    }

    /// <summary>What the menu does after a key did <paramref name="did"/> to <paramref name="state"/>'s field (MenuTextField.Key).</summary>
    private void FieldDid(FieldState state, FieldKey did)
    {
        var id = fieldStates.FirstOrDefault(pair => pair.Value == state).Key;
        switch (did)
        {
            case FieldKey.Moved:
                CaretMoved(state);
                break;
            case FieldKey.Edited:
                CaretMoved(state);
                if (state.Search is { } search)
                {
                    if (search.Typed(state.Field.Text, Environment.TickCount64))
                    {
                        _ = SetTimer(Hwnd, FieldSearchTimer, Math.Max(1, search.WaitMs), null);
                    }
                    RelayoutFields();
                }
                // §249: a filter's tiles as it holds now.
                else if (state.Control.Filters)
                {
                    RelayoutFields();
                }
                // §271: what the words find now, from its first.
                ScrollQuietly(0);
                break;
            case FieldKey.Commit:
                Commit(id, state);
                break;
            // §246: Escape on a line of text's level puts its words back and shuts it, at once.
            case FieldKey.Revert or FieldKey.Blur when TextLevel(state.Control):
                BlurField(closeSearch: true);
                CloseLevel();
                break;
            case FieldKey.Revert:
                CaretMoved(state);
                if (state.Search is { } reverted)
                {
                    reverted.Close();
                    reverted.Opened();
                    RelayoutFields();
                }
                else if (state.Control.Filters)
                {
                    RelayoutFields();
                }
                break;
            case FieldKey.Blur:
                BlurField(closeSearch: true);
                break;
            case FieldKey.Up or FieldKey.Down:
                Leave(id, state, down: did == FieldKey.Down);
                break;
        }
    }

    /// <summary>
    /// Enter: a text field keeps what was typed (nothing where it is what the setting says); a search
    /// keeps the words or the first found, proved, or searches at once (MenuSearch.Enter).
    /// </summary>
    private void Commit(uint id, FieldState state)
    {
        // §249: a filter's Enter goes on to its first tile, which Enter again picks.
        if (state.Control.Filters)
        {
            Leave(id, state, down: true);
            return;
        }
        // §246: a line of text's level shuts once what was typed is kept. A search keeps what it finds — a
        // feed's address once it is proved one —; a line its words, whatever it offers (§332).
        var shuts = TextLevel(state.Control);
        if (state.Search is { } search && !Line(state.Control))
        {
            search.Enter(state.Field.Text, found =>
            {
                Kept(id, state, found);
                if (shuts)
                {
                    CloseLevel();
                }
            });
            RelayoutFields();
            return;
        }
        // Letting the keys go keeps what was typed (BlurField), empty too; nothing changed, nothing kept.
        BlurField(closeSearch: true);
        if (shuts)
        {
            CloseLevel();
        }
    }

    /// <summary>
    /// §246: this level shuts, as Enter or Escape in a line of text's field shuts it: the level under it
    /// shows again, the keys back on the row it opened from, lit (Flyout.Inline.cs).
    /// </summary>
    private void CloseLevel() => BackInPlace();

    /// <summary>
    /// A field kept <paramref name="found"/> (null for its words): the root keeps it for whoever showed
    /// the menu and says so (<see cref="FlyoutEventKind.Adjust"/>), the field letting the keys go.
    /// </summary>
    private void Kept(uint id, FieldState state, OptionFound? found)
    {
        fieldCommits.RemoveAll(commit => commit.Kept.Id == id);
        fieldCommits.Add((new FieldCommit(id, state.Field.Text, found), false));
        // What it holds is what was kept, until the menu is shown again with the setting: kept once.
        state.Field.Kept();
        state.Search?.Close();
        if (fieldKeys == id)
        {
            BlurField(closeSearch: false);
        }
        // Said once the menu has been shown again for it (the showing's number changes then).
        _ = PostMessageW(Hwnd, WM_KARPO_FLYOUT_FIELD, default, default);
    }


    /// <summary>§523: a screen reader set row <paramref name="id"/>'s field's words to <paramref name="text"/>: kept, as Enter keeps them.</summary>
    private void FieldSet(uint id, string text)
    {
        if (!fieldStates.TryGetValue(id, out var state) || state.Field.Masked)
        {
            return;
        }
        state.Field.Editing.SetText(text);
        RedrawField(state);
        Kept(id, state, null);
    }

    /// <summary>Up or Down out of a field: to the row over or under it — a search's first result — which takes the keys.</summary>
    private void Leave(uint id, FieldState state, bool down)
    {
        var at = rows.FindIndex(row => row.Item.Id == id);
        if (at < 0)
        {
            return;
        }
        var to = Nearest(at, down);
        if (to == at)
        {
            return;
        }
        var target = rows[to].Item.Id;
        // Into its own results the rows stay; anywhere else they go, and the menu closes up.
        var ownResult = ResultsField(target) == id;
        BlurField(closeSearch: !ownResult);
        // Focus closes the searches the row it goes to is no result of (FieldFocusFollows).
        if (rows.FindIndex(row => row.Item.Id == target) is var again and >= 0)
        {
            Focus(again);
        }
    }

    /// <summary>
    /// The field whose search row <paramref name="id"/> is — a result —, or for a tile the filter over it
    /// (§249); 0 for none.
    /// </summary>
    private uint ResultsField(uint id) =>
        fieldResults.TryGetValue(id, out var result) ? result.Field
        : rows.Exists(row => row.Item.Id == id && Filtered(row.Item)) ? fieldStates.FirstOrDefault(pair => pair.Value.Control.Filters).Key
        : 0;

    /// <summary>Whether a filter leaves or hides <paramref name="item"/>: a tile, or a row of a list (<see cref="Listed"/>).</summary>
    private static bool Filtered(FlyoutItem item) => IsTile(item) || Listed(item);

    /// <summary>The keys came onto row <paramref name="index"/> (Focus): a field's takes them; any other row takes them from a field, which keeps its rows only for its own results.</summary>
    private void FieldFocusFollows(int index)
    {
        if (index < 0 || index >= rows.Count)
        {
            return;
        }
        var item = rows[index].Item;
        if (item.Control is FlyoutField)
        {
            FocusField(item.Id, selectAll: true);
            return;
        }
        // A result keeps its own search's rows; any other row none.
        var keep = ResultsField(item.Id);
        if (fieldKeys != 0)
        {
            BlurField(closeSearch: false);
        }
        if (CloseSearches(but: keep))
        {
            RelayoutFields();
        }
    }

    /// <summary>
    /// A row chosen (Enter, a click): a search's result is picked — proved and kept, the menu staying —
    /// rather than chosen. Whether <paramref name="id"/> was one.
    /// </summary>
    private bool PickFieldResult(uint id)
    {
        if (!fieldResults.TryGetValue(id, out var result) || !fieldStates.TryGetValue(result.Field, out var state) || state.Search is not { } search)
        {
            return false;
        }
        search.Pick(result.Found, found =>
        {
            Kept(result.Field, state, found);
            // §332: a line's suggestion picked is its value, kept as Enter keeps the words: its level shuts; §589 a choice
            // all the same (a popup of the panel closes on it).
            if (TextLevel(state.Control))
            {
                choseOne = true;
                CloseLevel();
            }
            // §392: a search's result is a choice: its level of one choice goes back as the menu is shown again.
            else
            {
                PickedOne();
            }
        });
        RelayoutFields();
        return true;
    }

    /// <summary>The caret or the selection moved: it shows (a blink restarts lit), in view.</summary>
    private void CaretMoved(FieldState state)
    {
        state.Field.CaretOn = true;
        FieldBlinking(state.Field.Focused);
        if (gfx is { } g && state.Look.Size.X > 0)
        {
            state.Field.KeepCaretInView(g, state.Look);
        }
        RedrawField(state);
    }

    /// <summary>The caret blinks while a field has the keys, at Windows' own rate; it stands still where Windows says it shouldn't blink.</summary>
    private void FieldBlinking(bool on)
    {
        if (Hwnd == HWND.NULL)
        {
            return;
        }
        _ = KillTimer(Hwnd, FieldBlinkTimer);
        var rate = GetCaretBlinkTime();
        if (on && fieldKeys != 0 && rate is > 0 and < uint.MaxValue)
        {
            _ = SetTimer(Hwnd, FieldBlinkTimer, rate, null);
        }
    }

    /// <summary>A timer of the fields' (<paramref name="id"/>): the caret blinks, or a search's wait is over. Whether it was one.</summary>
    private bool FieldTimer(nuint id)
    {
        if (id == FieldBlinkTimer)
        {
            if (!IsOpen || !fieldStates.TryGetValue(fieldKeys, out var state))
            {
                _ = KillTimer(Hwnd, FieldBlinkTimer);
                return true;
            }
            state.Field.CaretOn = !state.Field.CaretOn;
            RedrawField(state);
            return true;
        }
        if (id == FieldSearchTimer)
        {
            _ = KillTimer(Hwnd, FieldSearchTimer);
            var changed = false;
            foreach (var state in fieldStates.Values)
            {
                if (state.Search is { Phase: SearchPhase.Waiting } search)
                {
                    search.Due(long.MaxValue);
                    changed = true;
                }
            }
            if (changed)
            {
                RelayoutFields();
            }
            return true;
        }
        return false;
    }

    /// <summary>A search's answers came back to this menu's window: each taken, and the menu shown again where any changed its rows.</summary>
    private void FieldAnswers()
    {
        fieldsChanged = false;
        while (fieldPosts.TryDequeue(out var answer))
        {
            answer();
        }
        if (fieldsChanged)
        {
            fieldsChanged = false;
            RelayoutFields();
        }
    }

    /// <summary>An IME's composition starts: its window at the caret of the field with the keys, its candidates kept clear of the box.</summary>
    private void PlaceFieldComposition()
    {
        if (fieldKeys == 0 || !fieldStates.TryGetValue(fieldKeys, out var state) || gfx is not { } g
            || rows.FirstOrDefault(row => row.Item.Id == fieldKeys) is not { } found || FieldBox(found) is not { } box)
        {
            return;
        }
        var x = viewport.X + box.X + state.Field.CaretX(g, state.Look);
        var top = viewport.Y + box.Y - scroll;
        var at = new POINT((int)x, (int)(top + Round(6 * scale)));
        var under = new POINT((int)x, (int)MathF.Ceiling(top + box.H));
        var context = ImmGetContext(Hwnd);
        if (context == HIMC.NULL)
        {
            return;
        }
        try
        {
            var composition = new COMPOSITIONFORM { dwStyle = CFS_POINT, ptCurrentPos = at };
            _ = ImmSetCompositionWindow(context, &composition);
            var candidates = new CANDIDATEFORM
            {
                dwIndex = 0,
                dwStyle = CFS_EXCLUDE,
                ptCurrentPos = under,
                rcArea = new RECT { left = at.x, top = at.y, right = at.x + 1, bottom = under.y },
            };
            _ = ImmSetCandidateWindow(context, &candidates);
        }
        finally
        {
            _ = ImmReleaseContext(Hwnd, context);
        }
    }

    // --- The pointer --------------------------------------------------------------------------------

    /// <summary>
    /// A press at <paramref name="x"/>, <paramref name="y"/> (window px) on a field's row: the field takes
    /// the keys; in its box the caret goes where it landed (Shift: the selection grows to it; a second
    /// click: the word), and a drag selects; on its clear button it is emptied. Whether it was on one.
    /// </summary>
    private bool FieldPressed(int x, int y)
    {
        var at = RowAt(x, y);
        if (at < 0 || rows[at].Item.Control is not FlyoutField || !fieldStates.TryGetValue(rows[at].Item.Id, out var state) || gfx is not { } g)
        {
            return false;
        }
        var id = rows[at].Item.Id;
        SetHot(at);
        FocusField(id, selectAll: false);
        if (FieldBox(rows[at]) is not { } box || !box.Contains(x - viewport.X, y - viewport.Y + scroll))
        {
            return true;
        }
        var inX = x - viewport.X - box.X;
        var inY = y - viewport.Y + scroll - box.Y;
        if (state.Field.OnClear(state.Look, inX, inY))
        {
            if (state.Field.Clear())
            {
                FieldDid(state, FieldKey.Edited);
            }
            return true;
        }
        var index = state.Field.IndexAt(g, state.Look, inX);
        var now = Environment.TickCount64;
        var twice = now - fieldClickAt <= GetDoubleClickTime()
            && Math.Abs(x - fieldClickPoint.x) <= GetSystemMetrics(SM.SM_CXDOUBLECLK) / 2
            && Math.Abs(y - fieldClickPoint.y) <= GetSystemMetrics(SM.SM_CYDOUBLECLK) / 2;
        fieldClickAt = twice ? 0 : now;
        fieldClickPoint = new POINT(x, y);
        // §338: a password's selects all of it: a word would say where its words end.
        if (twice && state.Field.Masked)
        {
            state.Field.Editing.SelectAll();
        }
        else if (twice)
        {
            state.Field.Editing.SelectWordAt(index);
        }
        else
        {
            state.Field.Editing.MoveTo(index, KeyState.Down(VK.VK_SHIFT));
            fieldSelecting = true;
            _ = SetCapture(Hwnd);
        }
        CaretMoved(state);
        return true;
    }

    /// <summary>
    /// The pointer at <paramref name="x"/>, <paramref name="y"/> (window px; -1 for gone): a drag that
    /// began in a box selects on (true: the menu hears nothing more of it); over a box it lightens,
    /// the text cursor over its words.
    /// </summary>
    private bool FieldMoved(int x, int y)
    {
        if (fieldSelecting && fieldStates.TryGetValue(fieldKeys, out var held) && gfx is { } g
            && rows.FirstOrDefault(row => row.Item.Id == fieldKeys) is { } heldRow && FieldBox(heldRow) is { } heldBox)
        {
            held.Field.Editing.MoveTo(held.Field.IndexAt(g, held.Look, x - viewport.X - heldBox.X), extend: true);
            CaretMoved(held);
            return true;
        }
        foreach (var row in rows)
        {
            if (!fieldStates.TryGetValue(row.Item.Id, out var state) || FieldBox(row) is not { } box)
            {
                continue;
            }
            var inX = x - viewport.X - box.X;
            var inY = y - viewport.Y + scroll - box.Y;
            var over = x >= 0 && viewport.Contains(x, y) && inX >= 0 && inX < box.W && inY >= 0 && inY < box.H;
            var onClear = over && state.Field.OnClear(state.Look, inX, inY);
            if (over && !onClear)
            {
                _ = SetCursor(LoadCursorW(HINSTANCE.NULL, IDC.IDC_IBEAM));
            }
            if (over != state.Field.Hot || onClear != state.Field.ClearHot)
            {
                state.Field.Hot = over;
                state.Field.ClearHot = onClear;
                RedrawField(state);
            }
        }
        return false;
    }

    /// <summary>The button came up: a drag selecting in a box is over. Whether there was one.</summary>
    private bool FieldReleased()
    {
        if (!fieldSelecting)
        {
            return false;
        }
        fieldSelecting = false;
        if (GetCapture() == Hwnd)
        {
            _ = ReleaseCapture();
        }
        return true;
    }

    /// <summary>
    /// A message for the fields, before the menu's own handling: a search's answers, an IME starting
    /// to compose (placed, and then Windows' own handling as ever), the caret's and a search's
    /// timers, and the pointer in a box. Null where the menu goes on with it.
    /// </summary>
    private FlyoutEvent? FieldMessage(uint msg, WPARAM wParam, LPARAM lParam)
    {
        switch (msg)
        {
            case WM_KARPO_FLYOUT_FIELD:
                FieldAnswers();
                // What a field kept, said by the root, which whoever showed the menu takes (TakeFieldCommit).
                if (parent is null && IsOpen && fieldCommits.FindIndex(commit => !commit.Told) is var at and >= 0)
                {
                    fieldCommits[at] = (fieldCommits[at].Kept, true);
                    return new FlyoutEvent(FlyoutEventKind.Adjust, fieldCommits[at].Kept.Id);
                }
                return default(FlyoutEvent);
            case WM.WM_IME_STARTCOMPOSITION when IsOpen && parent is null:
                PlaceFieldComposition();
                return null;
            case WM.WM_TIMER when FieldTimer((nuint)wParam):
                return default(FlyoutEvent);
            case WM.WM_LBUTTONUP when FieldReleased():
                return default(FlyoutEvent);
            case WM.WM_MOUSEMOVE when IsOpen && FieldMoved(X(lParam), Y(lParam)):
                return default(FlyoutEvent);
            case WM.WM_MOUSELEAVE when IsOpen:
                _ = FieldMoved(-1, -1);
                return null;
            default:
                return null;
        }
    }
}
