// The time lines on the kit's blocks (docs\widgets.md §401; the proposal's time mockup): the timer, the focus
// session and the stopwatch one row each — the glyph in the list's leading column, the name where every name
// begins, the time in the Value against its buttons, the start button on the trailing gutter, nothing on a
// second row; the alarm a row with its caption (52 DIP), its switch on the trailing gutter; the clock its time
// as the Display on the gutter over the date, its face at the trailing gutter, two rows at the most. Each at
// its own (automatic) height, at 100 % and 150 %. Needs Direct2D, so it runs only with KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using static Karpo.Tests.Islands.Offscreen;

namespace Karpo.Tests.Folders;

public sealed class TimeAnatomyTests
{
    private static readonly float[] Scales = [1f, 1.5f];

    private static TileStyle StyleFor(float scale)
    {
        var look = ListFormPictures.LookFor(true);
        return FolderFlyout.StyleFor(scale, look, FlyoutTheme.For(look));
    }

    private static IIsland Made(Gfx gfx, ListKind kind)
    {
        var item = kind.New();
        item.Id = kind.Id();
        item.Normalize();
        return Karpo.Dock.Islands.CreateLine(Config.IslandKey(ListFormPictures.Owner, kind.Id()), item, kind, gfx, new Config())!;
    }

    private static D2D_RECT_F? Piece(IReadOnlyList<LinePiece> pieces, string name) =>
        pieces.FirstOrDefault(piece => piece.Name == name) is { Name: not null } found && found.Name == name ? found.Box : null;

    [UiFact]
    public void TheTimersTheFocusAndTheStopwatchAreOneRowEach()
    {
        var failures = new List<string>();
        OnStaThread(gfx =>
        {
            foreach (var scale in Scales)
            {
                var style = StyleFor(scale);
                var form = ListForm.Of(gfx, style);
                var width = FolderFlyout.ListWidth(scale);
                var valueLine = TilePaint.LineHeight(gfx, style.Text(LineType.ValueDip), Weight.Semibold);
                foreach (var kind in new[] { ListKind.Timer, ListKind.Focus, ListKind.Stopwatch })
                {
                    var at = $"{kind} at {scale * 100:0} %";
                    var tile = Made(gfx, kind);
                    try
                    {
                        var height = ((IListContent)tile).ListHeight(width, style);
                        if (MathF.Abs(height - LineRows.Px(1, style)) > 0.5f)
                        {
                            failures.Add($"{at}: {height:0.#} px tall, not one row ({LineRows.Px(1, style):0.#})");
                        }
                        var pieces = ((ILinePieces)tile).LinePieces(PopupPaint.Rect(0, 0, width, height), style);
                        Row(failures, at, pieces, form, width, height, valueLine, "start");
                        if (kind != ListKind.Stopwatch && (Piece(pieces, "minus") is null || Piece(pieces, "plus") is null))
                        {
                            failures.Add($"{at}: no minus and plus on its row");
                        }
                        if (kind == ListKind.Stopwatch && (Piece(pieces, "lap") is null || Piece(pieces, "reset") is null))
                        {
                            failures.Add($"{at}: no lap and reset on its row");
                        }
                    }
                    finally
                    {
                        tile.Dispose();
                    }
                }
            }
        });
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [UiFact]
    public void TheAlarmIsARowWithItsCaption()
    {
        var failures = new List<string>();
        OnStaThread(gfx =>
        {
            foreach (var scale in Scales)
            {
                var style = StyleFor(scale);
                var form = ListForm.Of(gfx, style);
                var width = FolderFlyout.ListWidth(scale);
                var at = $"Alarm at {scale * 100:0} %";
                var tile = Made(gfx, ListKind.Alarm);
                try
                {
                    var height = ((IListContent)tile).ListHeight(width, style);
                    if (MathF.Abs(height - LineRow.Height(style, caption: true)) > 0.5f)
                    {
                        failures.Add($"{at}: {height:0.#} px tall, not a row with its caption ({LineRow.Height(style, true):0.#})");
                    }
                    var pieces = ((ILinePieces)tile).LinePieces(PopupPaint.Rect(0, 0, width, height), style);
                    Row(failures, at, pieces, form, width, height, TilePaint.LineHeight(gfx, style.Text(LineType.ValueDip), Weight.Semibold), "switch");
                    if (Piece(pieces, "hint") is not { } hint || MathF.Abs(hint.left - form.TextLeft) > 1 || Piece(pieces, "name") is not { } name || hint.top < name.bottom - 0.5f)
                    {
                        failures.Add($"{at}: no caption under its name at the text edge");
                    }
                }
                finally
                {
                    tile.Dispose();
                }
            }
        });
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [UiFact]
    public void TheClocksTimeIsTheDisplayOnTheGutter()
    {
        var failures = new List<string>();
        OnStaThread(gfx =>
        {
            Clock.Now = () => new DateTime(2026, 9, 23, 14, 20, 0);
            try
            {
                foreach (var scale in Scales)
                {
                    var style = StyleFor(scale);
                    var form = ListForm.Of(gfx, style);
                    var width = FolderFlyout.ListWidth(scale);
                    var at = $"Clock at {scale * 100:0} %";
                    var tile = Made(gfx, ListKind.Clock);
                    try
                    {
                        // §432, §442: its Full form, the whole cells that hold its words, five, the face filling them.
                        var height = ((IListContent)tile).ListHeight(width, style);
                        if (MathF.Abs(height - LineRows.Cells(5, style)) > 0.5f)
                        {
                            failures.Add($"{at}: {height:0.#} px tall, not five cells ({LineRows.Cells(5, style):0.#})");
                        }
                        var pieces = ((ILinePieces)tile).LinePieces(PopupPaint.Rect(0, 0, width, height), style);
                        if (Piece(pieces, "glyph") is not null)
                        {
                            failures.Add($"{at}: a glyph beside its time");
                        }
                        var display = TilePaint.LineHeight(gfx, style.Text(LineType.DisplayDip), Weight.Semibold);
                        if (Piece(pieces, "time") is not { } time || MathF.Abs(time.left - form.Inset) > 1 || MathF.Abs((time.bottom - time.top) - display) > 1)
                        {
                            failures.Add($"{at}: its time is not the Display on the gutter ({Piece(pieces, "time")?.left:0.#}, {(Piece(pieces, "time") is { } t ? t.bottom - t.top : 0):0.#} tall, not {display:0.#})");
                        }
                        if (Piece(pieces, "date") is not { } date || MathF.Abs(date.left - form.Inset) > 1)
                        {
                            failures.Add($"{at}: its date is not on the gutter under its time");
                        }
                        if (Piece(pieces, "dial") is not { } dial || MathF.Abs(dial.right - (width - form.Inset)) > 1 || dial.bottom > height + 0.5f)
                        {
                            failures.Add($"{at}: no face on the trailing gutter, inside the line");
                        }
                    }
                    finally
                    {
                        tile.Dispose();
                    }
                }
            }
            finally
            {
                Clock.Now = Clock.Real;
            }
        });
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    /// <summary>A row of the kit: the glyph in the leading column, the name at the text edge, the time in the Value before <paramref name="end"/>, which ends on the trailing gutter; nothing under the line.</summary>
    private static void Row(List<string> failures, string at, IReadOnlyList<LinePiece> pieces, ListForm form, float width, float height, float valueLine, string end)
    {
        if (Piece(pieces, "glyph") is not { } glyph || (glyph.left + glyph.right) / 2 < form.Inset - 1 || (glyph.left + glyph.right) / 2 > form.Inset + form.Icon + 1)
        {
            failures.Add($"{at}: its glyph is not in the leading column");
        }
        if (Piece(pieces, "name") is not { } name || MathF.Abs(name.left - form.TextLeft) > 1)
        {
            failures.Add($"{at}: its name does not begin at the text edge ({Piece(pieces, "name")?.left:0.#}, not {form.TextLeft:0.#})");
        }
        var trailing = Piece(pieces, end);
        if (trailing is not { } last || MathF.Abs(last.right - (width - form.Inset)) > 1)
        {
            failures.Add($"{at}: its {end} does not end on the trailing gutter ({trailing?.right:0.#}, not {width - form.Inset:0.#})");
        }
        if (Piece(pieces, "time") is not { } time || MathF.Abs((time.bottom - time.top) - valueLine) > 1)
        {
            failures.Add($"{at}: its time is not one line of the Value ({(Piece(pieces, "time") is { } t ? t.bottom - t.top : 0):0.#} tall, not {valueLine:0.#})");
        }
        else if (pieces.Where(piece => piece.Name is "minus" or "plus" or "lap" or "reset" or "start" or "switch" && piece.Box.right > piece.Box.left)
            .Any(control => control.Box.left < time.right - 0.5f))
        {
            failures.Add($"{at}: a control stands before its time's end");
        }
        foreach (var piece in pieces.Where(piece => piece.Box.bottom > height + 0.5f))
        {
            failures.Add($"{at}: its {piece.Name} stands under the line ({piece.Box.bottom:0.#} of {height:0.#})");
        }
    }
}
