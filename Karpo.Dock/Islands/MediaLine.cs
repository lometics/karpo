// What's playing as a line of a list folder at whatever height it is given (docs\widgets.md §9, "A
// line at every height"), on the kit's row (§401):
//
//   * The art is on the right and the words on the left, at every height (2026-10-11: "align the
//     image to the right side of the widget and the text on the left"): the art holds the trailing
//     gutter, the words start where every name begins and are cut where they reach what stands
//     after them.
//   * The leading column says where it plays (2026-10-30: "now playing widget should show the app
//     that is playing on the left side"), where every other line stands its glyph: the app's icon,
//     or the favicon of the site a browser plays it on (NowPlayingSource), at the kit's Row glyph
//     size on the first row's middle, at every height; the note where no icon was found and while
//     nothing plays. Under the pointer it names where it plays. It was a badge in the cover's
//     corner: the cover is the track's picture, the leading column the app's.
//   * One row is one line: the title, and after it the artist in the quiet ink where both fit, else
//     the title; then the one control a row keeps, play or pause; and the art, an icon's size, on
//     the trailing gutter. Previous and next are the widget's right-click menu's there.
//   * The row (52 DIP, what a line is where nobody set its height): the title over the artist,
//     previous, play or pause and next, subtle, and the art, 40 DIP with the kit's corner.
//   * Taller, the whole widget: the row, and under it the track's timeline — how much of it has
//     played, the time played and the whole — and the app playing it, with shuffle and repeat at the
//     end of its row where the app has them. The app's name, where two apps or more play, carries a
//     chevron and is the picker: a click opens the line's menu, the apps first, each by its name and
//     the one followed ticked (Dock.WidgetMenus.cs). Past that there is nothing more to show.
//
// Which one a line draws is what fits its height at the text size in force. Beside the art, where
// the words would be left no room to be read, previous and next give way first — the right-click
// menu has them. Nothing here reads what is playing to decide where a part goes, so the parts do
// not move when the track changes; with nothing playing the words say so, the buttons are quiet and
// the timeline is not drawn.
//
// **The timeline moves while it is shown.** Windows says where the track stood and when; where it
// stands now is arithmetic on that, done each time the line is drawn (Playing.At), as the media
// flyout does it. The open folder draws its lines again at a widget's tick, and this asks for one a
// second while it shows a timeline (IntervalMs) — only then: the bar and a shorter line show no time
// that moves. A press on the timeline seeks there where the app allows it, and a drag scrubs, as the
// flyout's scrubber does.

using System.Numerics;
using Karpo.Dock.QuickSettings.Controls;
using Karpo.Dock.QuickSettings.Services;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock;

internal sealed partial class MediaIsland
{
    /// <summary>
    /// A line of words on a line of what's playing, in the box's px: where it stands, what it says, at
    /// what size and weight, the lines it may wrap to, and whether it is in the quiet ink.
    /// </summary>
    internal readonly record struct LineWords(D2D_RECT_F Box, string Text, float Px, Weight Weight, int Lines, bool Quiet);

    /// <summary>
    /// Where a line of what's playing puts its parts at one height, in the box's px: the art, the
    /// words, the three buttons (empty where the line has not got one), and on the whole widget the
    /// timeline — its bar, and the time played and the whole under it, with what they say — and
    /// whether this is the whole widget; and the leading column's square, where the app playing stands.
    /// </summary>
    internal readonly record struct MediaLine(
        D2D_RECT_F Art,
        IReadOnlyList<LineWords> Words,
        D2D_RECT_F Previous,
        D2D_RECT_F Play,
        D2D_RECT_F Next,
        D2D_RECT_F Track,
        D2D_RECT_F Played,
        string PlayedText,
        D2D_RECT_F Length,
        string LengthText,
        float TimePx,
        bool Whole,
        D2D_RECT_F Shuffle = default,
        D2D_RECT_F Repeat = default,
        D2D_RECT_F Picker = default,
        D2D_RECT_F App = default)
    {
        /// <summary>How much the line says: its words, its buttons and its timeline.</summary>
        public int Says =>
            Words.Count
            + (Previous.right > Previous.left ? 1 : 0) + (Play.right > Play.left ? 1 : 0) + (Next.right > Next.left ? 1 : 0)
            + (Whole ? 1 : 0);
    }


    /// <summary>Whether the last line drawn showed a timeline, so the open folder ticks it a second (see the note at the top).</summary>
    private bool timelineShown;

    /// <summary>The share of the track a press held on the timeline is at; null while none is held.</summary>
    private float? scrubbing;

    /// <summary>The timeline's bar the held press is on, in the card's px.</summary>
    private D2D_RECT_F scrubTrack;

    /// <summary>What a line's words say: the title — or that nothing plays — the artist, and the app playing them.</summary>
    private (string Title, string Artist, string App) Said()
    {
        if (now is not { } playing)
        {
            return (S.WidgetMediaNothingPlaying, "", "");
        }
        var title = playing.Title.Length > 0 ? playing.Title : playing.AppName;
        if (playing.Source is { Site: not null } where)
        {
            // A browser's site in the artist's place (NowPlayingSource): said once, as the site, and
            // the browser playing it under it.
            return (title, where.Name, where.App != title ? where.App : "");
        }
        // Where it plays: the app, or a browser's site and the browser (NowPlayingSource).
        var source = playing.Source?.Line is { Length: > 0 } line ? line : playing.AppName;
        var app = source.Length > 0 && source != title ? source : "";
        return (title, playing.Artist, app);
    }

    /// <summary>
    /// The line's layout in <paramref name="box"/>, the drawing's, the pointer's and a test's one
    /// question (see the note at the top): one row where the kit's row with its caption does not fit,
    /// the whole widget where its timeline and the app's row fit under that row too, else the row.
    /// </summary>
    internal MediaLine LineOf(D2D_RECT_F box, in TileStyle style)
    {
        var height = box.bottom - box.top;
        if (height + 0.5f < LineRow.Height(style, caption: true))
        {
            return OneRow(box, style);
        }
        return WholeLine(box, style) ?? Beside(box, style);
    }

    /// <summary>The art's side on the row: 40 DIP (§401), never less than the leading column.</summary>
    private float ArtSide(in TileStyle style) => MathF.Max(ListForm.Of(Gfx, style).Icon, PopupPaint.Round(40 * style.Scale * style.TextScale));

    /// <summary>A transport button's room on a row: the lines' glyph button (LineButton), square.</summary>
    private RowControl Button(in TileStyle style, string name)
    {
        var sizes = LineButton.Of(Gfx, style);
        return RowControl.Room(name, sizes.Height, sizes.Height);
    }

    /// <summary>
    /// One row: the title and the artist where both fit whole, else the title, where every name
    /// begins; play or pause after them, and the art, an icon's size, on the trailing gutter — the
    /// art is at the right of the words at every height (see the note at the top).
    /// </summary>
    private unsafe MediaLine OneRow(D2D_RECT_F box, in TileStyle style)
    {
        var form = ListForm.Of(Gfx, style);
        var height = box.bottom - box.top;
        var (title, artist, _) = Said();
        var both = artist.Length > 0 ? title + LineRow.Dot + artist : title;
        var art = MathF.Min(form.Icon, height);
        RowControl[] controls = [Button(style, "play"), RowControl.Room("art", art, art)];
        var room = TilePaint.Width(box) - form.TextLeft - form.Inset - controls.Sum(control => control.Width) - (2 * style.Px(LineSpace.GapDip));
        var said = ListTiers.Whole(Gfx, [both], form.TextPx, Weight.Regular, room) ?? title;
        var laid = LineRow.Draw(new LineSketch(null, Gfx), box.left, box.right, box.top, style, new RowSpec(said) { Glyph = NoteGlyph, Controls = controls }, height);
        var words = new List<LineWords>(2);
        if (laid.Name is { } name)
        {
            if (said == both && artist.Length > 0)
            {
                // The title in the text's ink and the artist after it in the quiet one, as the bar has them.
                var titleWidth = MathF.Min(TilePaint.Width(name), PopupPaint.Measure(Gfx, title, form.TextPx, Weight.Regular).X);
                words.Add(new LineWords(name with { right = name.left + titleWidth }, title, form.TextPx, Weight.Regular, 1, now is null));
                // To the room's end, the play button's gap before it: the two measured less the title,
                // each rounded up, can be a pixel short of the artist alone, which cut it.
                words.Add(new LineWords(name with { left = name.left + titleWidth, right = MathF.Max(name.right, laid.Controls[0].left - style.Px(LineSpace.GapDip)) },
                    LineRow.Dot + artist, form.TextPx, Weight.Regular, 1, true));
            }
            else
            {
                words.Add(new LineWords(name, said, form.TextPx, Weight.Regular, 1, now is null));
            }
        }
        return new MediaLine(laid.Controls[1], words, default, laid.Controls[0], default, default, default, "", default, "", form.SmallPx, false, App: laid.Glyph ?? default);
    }

    /// <summary>
    /// The kit's row (§401): the title over the artist where every name begins, previous, play or pause
    /// and next, and the art, 40 DIP, on the trailing gutter. Previous and next give way first where the
    /// words would be left less than about eight characters.
    /// </summary>
    private MediaLine Beside(D2D_RECT_F box, in TileStyle style)
    {
        var (laid, words, three) = Row(box, style);
        return three
            ? new MediaLine(laid.Controls[3], words, laid.Controls[0], laid.Controls[1], laid.Controls[2], default, default, "", default, "", style.Text(LineType.CaptionDip), false, App: laid.Glyph ?? default)
            : new MediaLine(laid.Controls[1], words, default, laid.Controls[0], default, default, default, "", default, "", style.Text(LineType.CaptionDip), false, App: laid.Glyph ?? default);
    }

    /// <summary>
    /// The row of <see cref="Beside"/> from the line's top, three cells (60 DIP): where its parts stand, its words, and
    /// whether it has the three buttons; <paramref name="under"/> px kept under the artist in its block (the timeline's).
    /// </summary>
    private unsafe (LineRow.Laid Laid, List<LineWords> Words, bool Three) Row(D2D_RECT_F box, in TileStyle style, float under = 0)
    {
        var form = ListForm.Of(Gfx, style);
        var (title, artist, _) = Said();
        var side = ArtSide(style);
        var gap = style.Px(LineSpace.GapDip);
        RowControl[] three = [Button(style, "previous"), Button(style, "play"), Button(style, "next"), RowControl.Room("art", side, side)];
        RowControl[] one = [Button(style, "play"), RowControl.Room("art", side, side)];
        float Room(RowControl[] controls) => TilePaint.Width(box) - form.TextLeft - form.Inset - controls.Sum(control => control.Width) - (controls.Length * gap);
        var all = Room(three) >= form.TextPx * 8;
        var spec = new RowSpec(title) { Glyph = NoteGlyph, Caption = artist.Length > 0 ? artist : null, Controls = all ? three : one, Under = under };
        var laid = LineRow.Draw(new LineSketch(null, Gfx), box.left, box.right, box.top, style, spec);
        var words = new List<LineWords>(2);
        if (laid.Name is { } name)
        {
            words.Add(new LineWords(name, title, form.TextPx, Weight.Regular, 1, now is null));
        }
        if (laid.Caption is { } caption)
        {
            words.Add(new LineWords(caption, artist, style.Text(LineType.CaptionDip), Weight.Regular, 1, true));
        }
        return (laid, words, all);
    }

    /// <summary>
    /// The whole widget, six cells (§442): the row (<see cref="Beside"/>) with the track's timeline under the artist in
    /// its block, from where every name begins to the trailing gutter, as a meter stands in the battery's row; the
    /// time played and the whole in the middle of the cell under it, where the track has one; and the app playing it —
    /// which carries the picker — with shuffle and repeat at the end of its row. Null where they do not all fit the
    /// line's height: the row it is then.
    /// </summary>
    private unsafe MediaLine? WholeLine(D2D_RECT_F box, in TileStyle style)
    {
        var form = ListForm.Of(Gfx, style);
        var said = Said();
        var tight = style.Px(LineSpace.TightDip);
        var (laid, words, three) = Row(box, style, LineMeter.Room(style));
        var captionPx = style.Text(LineType.CaptionDip);
        var captionLine = TilePaint.LineHeight(Gfx, captionPx);
        var cell = LineRows.Cell(style);
        var appRow = LineRows.Px(1, style);
        // Where each part goes is the line's alone, never the track's: the timeline's room is kept whether
        // or not the track has one, and the app's row whether or not it is said.
        if (!three || laid.Bottom + cell + appRow > box.bottom + 0.5f)
        {
            return null;
        }
        var (played, whole) = Times();
        var left = box.left + form.TextLeft;
        var right = box.right - form.Inset;
        // Under the words, the kit's meter short of the transport and the art beside them (§442, §449).
        var track = LineMeter.Box(Gfx, box.left, box.right, laid.Under + tight, style, laid.Controls);
        var timesTop = laid.Bottom + PopupPaint.Round((cell - captionLine) / 2);
        var playedBox = PopupPaint.Rect(left, timesTop, MathF.Min(right - left, PopupPaint.Measure(Gfx, played, captionPx, Weight.Regular).X), captionLine);
        var wholeWidth = MathF.Min(right - left, PopupPaint.Measure(Gfx, whole, captionPx, Weight.Regular).X);
        var wholeBox = PopupPaint.Rect(track.right - wholeWidth, timesTop, wholeWidth, captionLine);
        var app = LineRow.Draw(new LineSketch(null, Gfx), box.left, box.right, laid.Bottom + cell, style, new RowSpec(said.App.Length > 0 ? said.App : " ")
        {
            Controls = [Button(style, "shuffle"), Button(style, "repeat")],
        }, appRow);
        var picker = default(D2D_RECT_F);
        if (said.App.Length > 0 && app.Name is { } name)
        {
            words.Add(new LineWords(PopupPaint.Rect(name.left, PopupPaint.Round(app.Middle - (captionLine / 2)), MathF.Max(1, app.Controls[0].left - style.Px(LineSpace.GapDip) - name.left), captionLine),
                said.App, captionPx, Weight.Regular, 1, true));
            picker = Picker(words, said.App, style);
        }
        return new MediaLine(laid.Controls[3], words, laid.Controls[0], laid.Controls[1], laid.Controls[2], track, playedBox, played, wholeBox, whole, captionPx, true,
            app.Controls[0], app.Controls[1], picker, laid.Glyph ?? default);
    }

    /// <summary>
    /// The time played and the whole, as a player writes them — or nothing, where the track has no
    /// timeline. While a press on the timeline is held, the time is where it would seek to.
    /// </summary>
    private (string Played, string Whole) Times()
    {
        if (now is not { Duration: { Ticks: > 0 } whole } playing)
        {
            return ("", "");
        }
        var at = scrubbing is { } share ? TimeSpan.FromTicks((long)(whole.Ticks * (double)share)) : playing.At(Clock.RealOffsetUtc());
        return (MediaModes.Clock(at), MediaModes.Clock(whole));
    }

    /// <summary>
    /// The picker on the app's name, where the line says it (the last of <paramref name="words"/>):
    /// the name, kept short of the chevron after it, and the chevron — the room for it kept whether
    /// or not two apps play, so the name does not move as an app starts or stops. Nothing where the
    /// line does not say the app.
    /// </summary>
    private D2D_RECT_F Picker(List<LineWords> words, string app, in TileStyle style)
    {
        if (app.Length == 0 || words.Count == 0 || words[^1].Text != app || words[^1].Lines != 1)
        {
            return default;
        }
        var name = words[^1];
        var chevron = MathF.Round(name.Px * 0.8f);
        var room = MathF.Max(1, TilePaint.Width(name.Box) - chevron - style.Px(4));
        var shown = MathF.Min(room, PopupPaint.Measure(Gfx, app, name.Px, name.Weight).X);
        words[^1] = name with { Box = name.Box with { right = name.Box.left + room } };
        return name.Box with { right = name.Box.left + shown + style.Px(4) + chevron };
    }

    /// <summary>
    /// The line (<see cref="LineOf"/>): where it plays in the leading column; the art, or the note where
    /// there is none; the words, cut with an ellipsis where the room runs out; the buttons, quiet where
    /// the app does not allow them; and on the whole widget the timeline, where the track has one.
    /// </summary>
    private unsafe void PaintLine(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style)
    {
        var line = LineOf(box, style);
        var theme = style.Theme;
        var playing = now;
        // The folder ticks the line a second while it shows a timeline (see the note at the top).
        timelineShown = line.Whole && line.LengthText.Length > 0;
        Where(dc, line.App, playing, style);
        var art = line.Art;
        var side = MathF.Min(TilePaint.Width(art), TilePaint.Height(art));
        if (side > 0)
        {
            if (playing?.Art is { } pixels)
            {
                PopupPaint.Covered(dc, pixels, art, style.Px(LineCorner.ControlDip));
            }
            else if (side < style.Px(28))
            {
                // The icon column: the note alone, at the list's glyph size, as a kind's glyph stands there.
                PopupPaint.Glyph(dc, Gfx, NoteGlyph, ListForm.Of(Gfx, style).Glyph, theme.TextSecondary, art);
            }
            else
            {
                // A plate with the kit's corner and the note on it: the Row glyph on the row's art, the
                // Feature glyph on a larger one (§388, §401).
                PopupPaint.Fill(dc, art, style.Px(LineCorner.ControlDip), PopupPaint.Ink(theme.CardBackground));
                PopupPaint.Glyph(dc, Gfx, NoteGlyph, side >= style.Px(56) ? LineGlyph.Feature(style) : LineGlyph.Row(style), theme.TextSecondary, art);
            }
        }
        // The picker's plate under the app's name, before the name is drawn on it.
        if (playingIn.Count > 1 && TilePaint.Width(line.Picker) > 0)
        {
            ButtonPlate(dc, Outset(line.Picker, style.Px(4)), style, Cell.Picker);
        }
        foreach (var words in line.Words)
        {
            var ink = words.Quiet ? theme.TextSecondary : theme.Text;
            if (words.Lines > 1)
            {
                PopupPaint.Wrapped(dc, Gfx, words.Text, words.Px, words.Weight, ink, words.Box.left, words.Box.top, TilePaint.Width(words.Box), words.Lines, false);
            }
            else
            {
                PopupPaint.Text(dc, Gfx, words.Text, words.Px, words.Weight, ink, words.Box.left, words.Box.top, TilePaint.Width(words.Box));
            }
        }
        if (line.Whole && playing is { Duration: { Ticks: > 0 } } && TilePaint.Width(line.Track) > 0)
        {
            var share = scrubbing ?? playing.Fraction(Clock.RealOffsetUtc());
            TilePaint.Bar(dc, line.Track, share, TilePaint.Track(style), TilePaint.Accent(style));
            if (playing.CanSeek)
            {
                // A thumb where the app lets the timeline be pressed, inside the bar's ends.
                var radius = TilePaint.Height(line.Track) * 1.5f;
                var x = line.Track.left + radius + ((TilePaint.Width(line.Track) - (2 * radius)) * share);
                var y = (line.Track.top + line.Track.bottom) / 2;
                PopupPaint.Fill(dc, PopupPaint.Rect(x - radius, y - radius, 2 * radius, 2 * radius), radius, TilePaint.Accent(style));
            }
            if (line.PlayedText.Length > 0)
            {
                PopupPaint.Text(dc, Gfx, line.PlayedText, line.TimePx, Weight.Regular, theme.TextSecondary, line.Played.left, line.Played.top, TilePaint.Width(line.Played) + 1);
            }
            if (line.LengthText.Length > 0)
            {
                PopupPaint.Text(dc, Gfx, line.LengthText, line.TimePx, Weight.Regular, theme.TextSecondary, line.Length.left, line.Length.top, TilePaint.Width(line.Length) + 1);
            }
        }
        // The lines' one button (LineButton), subtle. Previous, play and next keep their order in every
        // language: upright in a mirrored line (Mirror.cs).
        var sizes = LineButton.Of(Gfx, style);
        var transport = Span(line.Previous, line.Play, line.Next);
        var kept = Mirror.Keep(dc, transport);
        try
        {
            Transport(dc, sizes, line.Previous, PreviousGlyph, playing?.CanPrevious ?? false, ButtonStyle.Subtle, style, Cell.Previous);
            Transport(dc, sizes, line.Play, playing is { IsPlaying: true } ? PauseGlyph : PlayGlyph, playing?.CanToggle ?? false, ButtonStyle.Subtle, style, Cell.PlayPause);
            Transport(dc, sizes, line.Next, NextGlyph, playing?.CanNext ?? false, ButtonStyle.Subtle, style, Cell.Next);
        }
        finally
        {
            Mirror.Resume(dc, kept);
        }
        // Shuffle and repeat only where the app has them: toggles, the accent's while on.
        if (playing is { CanShuffle: true })
        {
            Transport(dc, sizes, line.Shuffle, ShuffleGlyph, true, playing.Shuffle ? ButtonStyle.Accent : ButtonStyle.Subtle, style, Cell.Shuffle);
        }
        if (playing is { CanRepeat: true })
        {
            Transport(dc, sizes, line.Repeat, playing.Repeat == MediaRepeat.Track ? RepeatOneGlyph : RepeatAllGlyph, true,
                playing.Repeat != MediaRepeat.None ? ButtonStyle.Accent : ButtonStyle.Subtle, style, Cell.Repeat);
        }
        // The picker's chevron after the app's name, while there is more than one app to pick.
        if (playingIn.Count > 1 && TilePaint.Width(line.Picker) > 0 && line.Words.Count > 0)
        {
            var px = line.Words[^1].Px;
            var chevron = MathF.Round(px * 0.8f);
            PopupPaint.Glyph(dc, Gfx, ChevronGlyph, chevron, theme.TextSecondary,
                PopupPaint.Rect(line.Picker.right - chevron, line.Picker.top, chevron, TilePaint.Height(line.Picker)));
        }
    }

    /// <summary>
    /// Where it plays (NowPlayingSource) in the leading column <paramref name="column"/>: the app's icon,
    /// or the site's favicon, at the kit's Row glyph size on the column's middle, as a kind's glyph
    /// stands there; the note where no icon was found, quiet while nothing plays.
    /// </summary>
    private unsafe void Where(ID2D1DeviceContext* dc, D2D_RECT_F column, Playing? playing, in TileStyle style)
    {
        if (TilePaint.Width(column) <= 0)
        {
            return;
        }
        var side = ListForm.Of(Gfx, style).Glyph;
        if (playing?.Source?.Icon is { } icon)
        {
            var left = PopupPaint.Round(((column.left + column.right) / 2) - (side / 2));
            var top = PopupPaint.Round(((column.top + column.bottom) / 2) - (side / 2));
            PopupPaint.Picture(dc, icon, PopupPaint.Rect(left, top, side, side), 0);
            return;
        }
        PopupPaint.Glyph(dc, Gfx, NoteGlyph, side, playing is null ? style.Theme.TextSecondary : style.Theme.Text, column);
    }

    /// <summary>The leading column's tooltip: where the track plays, the site and the browser or the app, which the icon alone does not say.</summary>
    protected internal override (string Text, D2D_RECT_F Part)? CardTip(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        if (Sources || now is not { } playing)
        {
            return null;
        }
        var column = LineOf(box, style).App;
        var said = playing.Source?.Line is { Length: > 0 } line ? line : playing.AppName;
        return said.Length > 0 && Inside(column, point) ? (said, column) : null;
    }

    /// <summary>
    /// A transport button of the line (LineButton), lit where the pointer is on it and pressed while a
    /// press is down on it, as the media flyout's buttons light; nothing where the line has not got it.
    /// </summary>
    private unsafe void Transport(ID2D1DeviceContext* dc, in LineButton sizes, D2D_RECT_F rect, string glyph, bool enabled, ButtonStyle look, in TileStyle style, Cell cell)
    {
        var lit = enabled && lineHot == (int)cell;
        sizes.Paint(dc, Gfx, style.Theme, rect, "", glyph, look, lit, lit && linePress.On((int)cell), enabled: enabled);
    }


    /// <summary>
    /// The subtle button's plate under the control of the line the pointer is on, pressed while a
    /// press is down on it, as the media flyout's buttons light; the line itself never lights
    /// (FolderFlyout.LightsWhole).
    /// </summary>
    private unsafe void ButtonPlate(ID2D1DeviceContext* dc, D2D_RECT_F rect, in TileStyle style, Cell cell)
    {
        if (lineHot != (int)cell || TilePaint.Width(rect) <= 0)
        {
            return;
        }
        var ink = linePress.On((int)cell) ? style.Theme.SubtlePressed : style.Theme.SubtleHover;
        PopupPaint.Fill(dc, rect, style.Px(4), PopupPaint.Ink(ink));
    }

    /// <summary>The span across the buttons the line has room for, from the first's left to the last's right; empty for none.</summary>
    private static D2D_RECT_F Span(params D2D_RECT_F[] parts)
    {
        var shown = parts.Where(part => part.right > part.left).ToList();
        return shown.Count == 0
            ? default
            : new D2D_RECT_F(shown.Min(part => part.left), shown.Min(part => part.top), shown.Max(part => part.right), shown.Max(part => part.bottom));
    }

    private static D2D_RECT_F Outset(D2D_RECT_F rect, float by) => new(rect.left - by, rect.top - by, rect.right + by, rect.bottom + by);

    /// <summary>
    /// The parts of the line as a card's parts, for what asks a card where its buttons are: the art,
    /// the room the words take, and the three buttons.
    /// </summary>
    private TileLayout LineParts(D2D_RECT_F box, in TileStyle style)
    {
        var line = LineOf(box, style);
        var text = line.Words.Count == 0 ? default : new D2D_RECT_F(
            line.Words.Min(words => words.Box.left),
            line.Words.Min(words => words.Box.top),
            line.Words.Max(words => words.Box.right),
            line.Words.Max(words => words.Box.bottom));
        return new TileLayout(line.Art, text, line.Previous, line.Play, line.Next, line.Shuffle, line.Repeat, line.Picker);
    }

    // --- The timeline under the pointer ------------------------------------------------------------

    /// <summary>
    /// A press on the whole widget's timeline, where the app lets its track be sought: the card's
    /// until it is let go, the time under it shown where the track would go. A press anywhere else is
    /// left to the folder, which makes it a click (CardTarget, Click).
    /// </summary>
    protected internal override bool CardPress(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        // A press on a button lights it pressed; the press stays the folder's, which makes it the click (RowPress).
        if (!Sources && linePress.Down(CardTarget(point, box, style, surface)))
        {
            _ = Wake();
        }
        if (Sources || now is not { CanSeek: true, Duration: { Ticks: > 0 } })
        {
            return false;
        }
        var line = LineOf(box, style);
        var reach = style.Px(8);
        if (!line.Whole || TilePaint.Width(line.Track) < 1
            || point.X < line.Track.left || point.X > line.Track.right
            || point.Y < line.Track.top - reach || point.Y > line.Track.bottom + reach)
        {
            return false;
        }
        scrubTrack = line.Track;
        scrubbing = ShareAt(point.X);
        _ = Wake();
        return true;
    }

    protected internal override void CardDrag(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        if (scrubbing is null)
        {
            return;
        }
        scrubbing = ShareAt(point.X);
        _ = Wake();
    }

    /// <summary>The press let go: the track goes where it was let go, once.</summary>
    protected internal override void CardRelease()
    {
        if (scrubbing is not { } share)
        {
            return;
        }
        scrubbing = null;
        Seek(share);
        _ = Wake();
    }

    /// <summary>The share of the held timeline at <paramref name="x"/>, 0 to 1.</summary>
    private float ShareAt(float x) => Math.Clamp((x - scrubTrack.left) / MathF.Max(1, TilePaint.Width(scrubTrack)), 0, 1);

    /// <summary>
    /// The playing track to <paramref name="share"/> of its length, where the app allows it: asked of
    /// the app, and shown at once, until the app says where it went.
    /// </summary>
    private void Seek(float share)
    {
        if (now is not { Session: { } session, Duration: { Ticks: > 0 } whole } playing || !playing.CanSeek)
        {
            return;
        }
        var target = TimeSpan.FromTicks((long)(whole.Ticks * (double)share));
        now = playing with { Position = target, Taken = Clock.RealOffsetUtc() };
        try
        {
            _ = session.TryChangePlaybackPositionAsync((playing.Start + target).Ticks);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            // The app went away between the event and the press.
            Console.Error.WriteLine($"Karpo: media seek failed: {error.GetType().Name}");
        }
    }

    /// <summary>
    /// The timeline as its menu says it (Dock.WidgetMenus.cs), for a line too short to show it: where
    /// the track stands of how long it is ("2:03 of 4:32"), empty without a timeline, and whether a
    /// step through it would act.
    /// </summary>
    internal (string Said, bool CanSeek) Timeline =>
        now is { Duration: { Ticks: > 0 } whole } playing
            ? (S.WidgetMediaTimeline(at: MediaModes.Clock(playing.At(Clock.RealOffsetUtc())), length: MediaModes.Clock(whole)), playing.CanSeek)
            : ("", false);

    /// <summary>The track moved <paramref name="by"/> from where it stands, from its menu: a seek, as a press on the timeline does.</summary>
    internal void SeekBy(TimeSpan by)
    {
        if (now is not { Duration: { Ticks: > 0 } whole } playing)
        {
            return;
        }
        var to = playing.At(Clock.RealOffsetUtc()) + by;
        Seek((float)Math.Clamp(to.TotalSeconds / whole.TotalSeconds, 0, 1));
        _ = Wake();
    }

    /// <summary>The folder closing: nothing shows a timeline, and nothing is held.</summary>
    public override void Visible(bool shown)
    {
        if (!shown)
        {
            timelineShown = false;
            scrubbing = null;
            lineHot = (int)Cell.Info;
            _ = linePress.Up();
        }
    }
}
