// "Set up in Settings": what a widget shows while it is not set up enough to show anything (the
// person, 2026-10-08: "rss feed flyout should show shortcut to settings if it is not fully
// configured to show a source, this should also be done for all the other widgets. in the same
// fashion"). A feed with no address, a stock with no symbol, the weather with neither a place nor
// this PC's location, a drive or network drive with no drive, a pin with nothing to open, a heading
// with no words (Karpo.Core\WidgetSetup.cs, the one rule of what needs setting up), and the mail,
// the calendar and the contacts with no account. One component, in one style everywhere:
//
//   in its flyout      the sentence that says what it needs, and the accent button under it, where
//                      the stock's and the weather's "Try again" stand under theirs (SetupPrompt)
//   on its line        the kit's one Status (§388): the kind's glyph, the short line that says what
//                      is missing and a standard "Set up" button, one row at the line's top; the
//                      button's words shorten to the gear alone, and where not even the gear fits the
//                      kind's own drawing stays (SetupFace)
//   in a pin's menu    the reason, and a row in the same words (Dock.BrokenRows)
//
// What it needs is a setting of the widget's own, in its menu (§228: the widgets' pages in Karpo's
// settings are gone), so the button there says "Set up" and opens its configuration menu, over the
// folder, at rest too; the three that need an account say "Set up in Settings" and open the same menu
// at its Accounts level (§340: no settings app any more). On a card or a line in a folder the whole of it is the button (FolderFlyout's Activate),
// since a folder opens no flyout (docs\widgets.md §15) and an unset widget has nothing else a click
// could do.

using Karpo.Core;
using Karpo.Dock.QuickSettings.Controls;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock;

/// <summary>
/// What a widget still needs before it shows anything, where its "Set up in Settings" goes, and
/// the words that say so: <paramref name="Short"/> on a card or a line, <paramref name="Long"/> in
/// its flyout.
/// </summary>
/// <param name="Key">The widget's key ("{owner}/{id}", a folder's widget "{owner}/{folder}/{id}"); null for an account's need.</param>
internal sealed record SetupNeed(string? Key, SetupLink Link, string Short, string Long)
{
    /// <summary>The widget keyed <paramref name="key"/>, which needs <paramref name="field"/>, in its kind's words.</summary>
    public static SetupNeed Of(string key, SetupField field) => field switch
    {
        SetupField.Target => new(key, new(field), S.DockSetupNoTarget, S.DockSetupPinText),
        SetupField.Symbol => new(key, new(field), S.WidgetMarketsNoSymbol, S.DockMarketsNoSymbolText),
        SetupField.Place => new(key, new(field), S.WidgetWeatherNoPlaceTitle, S.WidgetWeatherNoPlaceText),
        SetupField.Feed => new(key, new(field), S.WidgetRssNoFeed, S.DockRssNoFeedText),
        SetupField.Team => new(key, new(field), S.DockSportsNoTeam, S.DockSportsNoTeamText),
        SetupField.Stream => new(key, new(field), S.DockCameraNoStream, S.DockCameraNoStreamText),
        _ => new(key, new(field), S.CoreDriveNoneChosen, S.DockSetupDriveText),
    };

    /// <summary>The mail with no account that gives mail.</summary>
    public static SetupNeed Mail => new(null, SetupLink.Accounts, S.DockMailNoAccount, S.DockSetupMailText);

    /// <summary>The calendar with no account that gives calendars.</summary>
    public static SetupNeed Calendar => new(null, SetupLink.Accounts, S.DockSetupCalendarText, S.DockSetupCalendarText);

    /// <summary>The contacts with no account that gives contacts.</summary>
    public static SetupNeed Contacts => new(null, SetupLink.Accounts, S.DockSetupNoContactsAccount, S.DockSetupContactsText);
}

/// <summary>
/// "Set up in Settings" in a flyout: what the widget needs, in a sentence, and the accent button
/// under it. A flyout keeps one and places it in its body for a widget that is not set up
/// (<see cref="Place"/>); the button's press is the flyout's to answer, by closing and handing the
/// dock the need.
/// </summary>
internal sealed class SetupPrompt
{
    /// <summary>Segoe Fluent Icons: Settings, the gear Windows' own rows that open Settings wear.</summary>
    internal const string Gear = "";

    private readonly Label message = new("", LabelStyle.Secondary, lines: 6);
    private readonly Button button;

    /// <param name="pressed">What its button does: the flyout closes and says so.</param>
    public SetupPrompt(Action pressed)
    {
        button = new Button(S.DockSetupButton, ButtonStyle.Accent, Gear) { Name = S.DockSetupButton, Clicked = pressed };
    }

    /// <summary>The button, for a test to find and press.</summary>
    internal Button Button => button;

    /// <summary>
    /// Puts <paramref name="text"/> and the button into <paramref name="body"/>, on the panel's
    /// padding, from <paramref name="y"/> down in a body <paramref name="width"/> px wide; the y under them.
    /// </summary>
    public float Place(QuickContext c, List<Widget> body, string text, float width, float y)
    {
        message.Text = text;
        var room = width - 2 * c.D(16);
        var height = MathF.Ceiling(Label.Measure(c, message.Text, message.Style, room, message.Lines));
        message.Bounds = PopupPaint.Rect(c.D(16), y + c.D(4), room, height);
        body.Add(message);
        y += c.D(4) + height + c.D(12);
        button.Text = S.DockSetupButton;
        button.Bounds = PopupPaint.Rect(c.D(16), y, MathF.Min(button.NaturalWidth(c), room), c.T(32));
        body.Add(button);
        return y + c.T(32) + c.D(12);
    }
}

/// <summary>
/// "Set up" on a line of a list (<see cref="CellIsland.PaintOn"/>): the kit's one Status (§388; it was
/// centred in the box with an accent button, 2026-10-10). One row at the line's top — the kind's glyph in
/// the list's leading column in the tertiary ink, the short line that says what is missing where every
/// name begins, in the secondary ink, and a standard button against the trailing gutter — never centred,
/// never large, never the accent's. One plan (<see cref="Lay"/>) for every kind, which the painting draws
/// and a test reads. The words are cut with an ellipsis where the row is short, then left out; the
/// button's words shorten to the gear alone, and where not even the gear fits nothing is drawn and the
/// kind's own drawing stays.
/// </summary>
internal static unsafe class SetupFace
{
    /// <summary>The button's three forms, longest first; a line says "Set up" or the gear (§388).</summary>
    internal enum Form
    {
        Words,
        Short,
        Gear,
    }

    private static readonly Form[] Forms = [Form.Short, Form.Gear];

    /// <summary>
    /// Where each piece of the prompt goes in its box (px): the glyph's square and the words' box,
    /// either absent, and the button in its <see cref="Form"/>. Never <see cref="Stacked"/> now: one row.
    /// </summary>
    internal readonly record struct Plan(D2D_RECT_F? Glyph, float GlyphPx, D2D_RECT_F? Words, D2D_RECT_F Button, Form Form, bool Stacked)
    {
        /// <summary>The smallest box around every piece.</summary>
        public D2D_RECT_F Bounds
        {
            get
            {
                var all = new[] { Glyph, Words, Button }.OfType<D2D_RECT_F>().ToList();
                return new D2D_RECT_F(all.Min(r => r.left), all.Min(r => r.top), all.Max(r => r.right), all.Max(r => r.bottom));
            }
        }
    }

    /// <summary>
    /// Draws the Status of a widget with <paramref name="glyph"/> that is missing what
    /// <paramref name="words"/> say, in <paramref name="box"/> (px); false, with nothing drawn, where the
    /// room has no place for even the gear. A null <paramref name="dc"/> only asks; a null
    /// <paramref name="glyph"/> is a widget that leads with its words (a heading). Whether what it needs is
    /// an account in Karpo's settings or a setting of its own, the button says "Set up" (§388).
    /// </summary>
    public static bool Paint(ID2D1DeviceContext* dc, Gfx gfx, D2D_RECT_F box, in TileStyle style, string? glyph, string words, bool settings = false)
    {
        if (Lay(gfx, box, style, glyph, words, settings) is null)
        {
            return false;
        }
        if (dc != null)
        {
            LineStatus.Draw(new LineSketch(dc, gfx), box, style, glyph, words, [(Said(Form.Short), null), (Said(Form.Gear), SetupPrompt.Gear)]);
        }
        return true;
    }
    /// <summary>The plan for <paramref name="box"/>, as <see cref="Paint"/> draws it (<see cref="LineStatus"/>); null where not even the gear fits.</summary>
    internal static Plan? Lay(Gfx gfx, D2D_RECT_F box, in TileStyle style, string? glyph, string words, bool settings = false)
    {
        _ = settings;
        if (LineButton.Of(gfx, style).Height > TilePaint.Height(box))
        {
            return null;
        }
        var laid = LineStatus.Lay(gfx, box, style, glyph is not null, words, [(Said(Form.Short), null), (Said(Form.Gear), SetupPrompt.Gear)]);
        return laid.Button is { } button
            ? new Plan(laid.Glyph, ListForm.Of(gfx, style).Glyph, laid.Words, button, Forms[laid.Action], Stacked: false)
            : null;
    }
    /// <summary>
    /// The same on a taskbar entry (<see cref="CellIsland"/>'s bar cell), which has no room for a
    /// button and needs none, since a click on it opens its flyout with the button in it: the
    /// kind's empty face, its glyph and a dash (§388, <see cref="FaceDrawings.Empty"/>), never words. The cell keeps the size its
    /// settings give it (WidgetShapes), so nothing moves when it is set up. Where the box is
    /// <paramref name="width"/> × <paramref name="height"/> px, at its top left.
    /// </summary>
    public static void OnBar(ID2D1DeviceContext* dc, Gfx gfx, in IslandStyle style, float width, float height, string? glyph) =>
        FaceDrawings.Empty(dc, gfx, style, PopupPaint.Rect(0, 0, width, height), glyph);

    /// <summary>What the button in <paramref name="form"/> says after its gear: never pressed itself (the line is).</summary>
    private static string Said(Form form) => form switch
    {
        Form.Short => S.DockSetupShort,
        _ => "",
    };
}
