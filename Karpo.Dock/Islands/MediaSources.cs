// The apps playing, as a line of a list (ListKind.MediaSources): the media flyout's "Playing in"
// list, back in the media preset (2026-10-11: "the now playing folder should show the selector for
// the source again. where i can choose brave or spotify for example").
//
// A row per app with a session, in Windows' order, the list's own row each: the app's icon in the
// icon column, its name where every name of the list begins, and whether it plays at the row's end
// in the quiet ink. The app now playing follows is ticked at the row's end in the accent, as the
// picker's menu ticks it, its name in semibold — no plate at rest, which would stand on the gutter
// where the list's icon column starts (ListGutterTests). A click on another row
// follows that app; the marked one, clicked, stays as it is. With nothing playing the line says so,
// quietly, the note beside the words.
//
// **One pick for the dock** (MediaPick). The pick was each now-playing line's own; a list of its own
// has to reach the line it picks for, and a face reads its folder's line. So the pick is the dock's
// one, and every now-playing line, face and list follows it: Spotify picked here is what every
// face shows. Never written down, as before: a restart follows Windows' choice. A test's stand-in
// never touches it (MediaStand.Pick).
//
// It is the now-playing island in another mode — the sessions, their icons and the reads are the
// same — so the two cannot disagree on which apps play or which is followed.

using System.Numerics;
using Karpo.Dock.QuickSettings.Services;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock;

/// <summary>
/// Which app's session the now-playing widgets follow: the person's pick, or null while they follow
/// whichever session Windows leads with. Written on the dock's thread, read on the pool by a read
/// too, so its key is written and read whole.
/// </summary>
internal sealed class MediaPick
{
    /// <summary>The dock's one pick, which every now-playing line, face and list of the apps playing shares.</summary>
    public static readonly MediaPick Shared = new();

    private volatile string? key;

    /// <summary>The app picked (the session's source app id), or null for Windows' own choice.</summary>
    public string? Key => key;

    /// <summary>The pick changed; raised on the thread that changed it, the dock's.</summary>
    public event Action? Changed;

    public void Set(string? value)
    {
        if (string.Equals(key, value, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        key = value;
        Changed?.Invoke();
    }
}

internal sealed partial class MediaIsland
{
    /// <summary>A row of the apps playing: this plus the app's place in <see cref="PlayingIn"/>.</summary>
    internal const int SourceTarget = 1_000;

    /// <summary>The source row under the pointer, or -1.</summary>
    private int sourceHot = -1;

    /// <summary>The line's button under the pointer (a <see cref="Cell"/>; Info for none), and the one a press is down on.</summary>
    private int lineHot;
    private RowPress linePress;

    /// <summary>tests: the line's button under the pointer and the one pressed (Cell's values; 0 for none).</summary>
    internal (int Hot, int Pressed) LineLit => (lineHot, linePress.Held);

    /// <summary>Segoe Fluent Icons' CheckMark: the app followed.</summary>
    private const string CheckGlyph = "";

    /// <summary>One app's row of the list: its place, its band (LineItem) lit under the pointer, the icon's box, its words, the check's box and whether it is the one followed.</summary>
    internal readonly record struct SourceRow(int Index, D2D_RECT_F Box, D2D_RECT_F Icon, D2D_RECT_F Name, D2D_RECT_F State, string StateText, D2D_RECT_F Check, bool Followed);

    /// <summary>A row of the list: the list's own row, so one app is one row of the list.</summary>
    private static float SourceRowHeight(in TileStyle style) => PopupPaint.Round(40 * style.Scale * style.TextScale);

    /// <summary>A row per app, or the one row saying nothing plays.</summary>
    private float SourcesHeight(in TileStyle style) => SourceRowHeight(style) * Math.Max(1, playingIn.Count);

    /// <summary>
    /// The rows in <paramref name="box"/>: from its top, the list's own row height each, shorter where
    /// the line is set shorter than its apps — down to the least a row reads at, past which the apps
    /// that do not fit are left out (the picker in the now-playing line's menu has them all).
    /// </summary>
    internal List<SourceRow> SourceRows(D2D_RECT_F box, in TileStyle style)
    {
        var rows = new List<SourceRow>(playingIn.Count);
        if (playingIn.Count == 0)
        {
            return rows;
        }
        var form = ListForm.Of(Gfx, style);
        var height = TilePaint.Height(box);
        var least = PopupPaint.Round(32 * style.Scale * style.TextScale);
        var shown = Math.Clamp((int)MathF.Floor((height + 0.5f) / least), 1, playingIn.Count);
        var row = MathF.Min(SourceRowHeight(style), MathF.Floor(height / shown));
        var followed = Follows;
        var textLeft = box.left + form.TextLeft;
        // The check's room at the row's end is kept on every row, so nothing moves when another is picked.
        var checkSide = form.Icon;
        var checkLeft = box.right - form.Inset - checkSide;
        var end = checkLeft - form.Gap;
        for (var index = 0; index < shown; index++)
        {
            var app = playingIn[index];
            var y = box.top + (index * row);
            var said = app.Playing ? S.WidgetMediaPlaying : S.WidgetMediaPaused;
            var stateWidth = PopupPaint.Measure(Gfx, said, form.SmallPx, Weight.Regular).X;
            // The state where it leaves the name a few characters, else the name alone.
            var keeps = stateWidth + form.Gap + (form.TextPx * 6) <= end - textLeft;
            var state = keeps ? PopupPaint.Rect(end - stateWidth, y + MathF.Round((row - form.SmallLine) / 2), stateWidth, form.SmallLine) : default;
            var nameRight = keeps ? state.left - form.Gap : end;
            rows.Add(new SourceRow(
                index,
                // The row edge to edge, each the next's edge: what lights and takes a press (LineItem).
                LineItem.Band(box, y, y + row),
                form.IconBox(box.left, y, row),
                PopupPaint.Rect(textLeft, y + MathF.Round((row - form.Line) / 2), MathF.Max(1, nameRight - textLeft), form.Line),
                state,
                keeps ? said : "",
                PopupPaint.Rect(checkLeft, y + MathF.Round((row - checkSide) / 2), checkSide, checkSide),
                followed is not null && string.Equals(app.Key, followed, StringComparison.OrdinalIgnoreCase)));
        }
        return rows;
    }

    /// <summary>The list: the rows, the one followed marked, the one under the pointer lit; or that nothing plays.</summary>
    private unsafe void PaintSources(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style)
    {
        var theme = style.Theme;
        var form = ListForm.Of(Gfx, style);
        var rows = SourceRows(box, style);
        if (rows.Count == 0)
        {
            // Nothing plays: the note and the words, quiet, on the list's edges in the first row, where
            // the first app would stand — a taller line keeps the rest empty under it, not around it.
            var first = MathF.Min(TilePaint.Height(box), SourceRowHeight(style));
            var top = box.top + MathF.Round((first - form.Line) / 2);
            PopupPaint.Glyph(dc, Gfx, NoteGlyph, form.Glyph, theme.TextTertiary, form.IconBox(box.left, box.top, first));
            PopupPaint.Text(dc, Gfx, S.WidgetMediaNothingPlaying, form.TextPx, Weight.Regular, theme.TextSecondary, box.left + form.TextLeft, top, form.Room(TilePaint.Width(box)));
            return;
        }
        foreach (var row in rows)
        {
            var app = playingIn[row.Index];
            if (row.Index == sourceHot)
            {
                LineItem.Light(dc, row.Box, theme, pressed: false);
            }
            if (row.Followed)
            {
                // The one followed: ticked in the accent, as the picker's menu ticks it.
                PopupPaint.Glyph(dc, Gfx, CheckGlyph, form.Glyph, TilePaint.Accent(style), row.Check);
            }
            if (app.Icon is { } icon)
            {
                var side = MathF.Min(form.Icon, TilePaint.Height(row.Icon));
                var square = PopupPaint.Rect(PopupPaint.Round(((row.Icon.left + row.Icon.right) / 2) - (side / 2)), PopupPaint.Round(((row.Icon.top + row.Icon.bottom) / 2) - (side / 2)), side, side);
                PopupPaint.Picture(dc, icon, square, 0);
            }
            else
            {
                PopupPaint.Glyph(dc, Gfx, NoteGlyph, form.Glyph, theme.Text, row.Icon);
            }
            PopupPaint.Text(dc, Gfx, app.App, form.TextPx, row.Followed ? Weight.Semibold : Weight.Regular, theme.Text, row.Name.left, row.Name.top, TilePaint.Width(row.Name));
            if (row.StateText.Length > 0)
            {
                PopupPaint.Text(dc, Gfx, row.StateText, form.SmallPx, Weight.Regular, theme.TextSecondary, row.State.left, row.State.top, TilePaint.Width(row.State) + 1);
            }
        }
    }

    /// <summary>The row under <paramref name="point"/>: <see cref="SourceTarget"/> plus its place, or the line as a whole.</summary>
    private int SourceTargetAt(Vector2 point, D2D_RECT_F box, in TileStyle style)
    {
        foreach (var row in SourceRows(box, style))
        {
            if (Inside(row.Box, point))
            {
                return SourceTarget + row.Index;
            }
        }
        return (int)Cell.Info;
    }

    /// <summary>A row's click: that app followed from now on; the one followed already, clicked, stays.</summary>
    private void PickSource(int target)
    {
        var at = target - SourceTarget;
        if ((uint)at >= (uint)playingIn.Count)
        {
            return;
        }
        var key = playingIn[at].Key;
        if (!string.Equals(key, Follows, StringComparison.OrdinalIgnoreCase))
        {
            Follow(key);
        }
        _ = Wake();
    }

    /// <summary>A row's tooltip: the app's name, which a narrow row may cut.</summary>
    private string? SourceTip(int target) =>
        (uint)(target - SourceTarget) < (uint)playingIn.Count ? playingIn[target - SourceTarget].App
        : playingIn.Count == 0 ? S.WidgetMediaNothingPlaying : null;

    /// <summary>The list's rows light under the pointer, never the line around them.</summary>
    protected internal override bool LightsItsRows => Sources;

    protected internal override void CardHover(Vector2? point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        if (!Sources)
        {
            // The line's own buttons light under the pointer, each on its own; the line does not.
            var on = point is { } where ? CardTarget(where, box, style, surface) : (int)Cell.Info;
            var let = linePress.Over(on);
            if (on != lineHot || let)
            {
                lineHot = on;
                _ = Wake();
            }
            return;
        }
        var hot = point is { } at && SourceTargetAt(at, box, style) is var target and >= SourceTarget ? target - SourceTarget : -1;
        if (hot != sourceHot)
        {
            sourceHot = hot;
            _ = Wake();
        }
    }
}
