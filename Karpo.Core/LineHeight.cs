// How tall a folder's widget is down its list (docs\widgets.md §76).
//
// The person, 2026-10-09: "either the height is automatic, then you can just set a max height, or
// the height is fixed, then you just set the height", for every widget shown in a folder, and not
// for the folder, which "should always just grow to its contents". So a line's height is one of
// two things:
//
//   * Automatic, up to N rows: the content decides (IListContent, or what its kind declares where
//     nothing measures), never under one row and never over N. What the content does not fit in N
//     rows: a line of items scrolls them (ListScroll.cs, 2026-10-11), a widget of tiers shows a
//     smaller one, anything else is cut at the line's edge.
//   * Fixed, N rows: exactly N, whatever the content says. Room left over is the content's to fill
//     or leave (a tier taller, more items); content that does not fit is shortened in the same way.
//
// Not every kind has both (2026-10-11, §138): a kind whose content does not vary (ListWidget.Grows
// false: a clock, a graph, a switch) is Fixed only, an automatic height of such a kind read as fixed
// at the kind's rows, or fewer where it said fewer (Within(ListKind)). No kind has a most of its own
// any more (2026-10-16, "remove the max height for widgets. because then they cannot actually grow to
// the full height of the page", §192): a line may be as tall as its folder's room on its screen
// (FolderFlyout.Reach), which the folder's menu holds it to and the folder gives it at the most;
// the number here, Most, is only past any screen, wherever a height is made (Limits, §460); the charts that stretched to
// a most of their own (§432) are sized since §603. No kind has a fewest
// of its own any more (2026-10-13, "remove the min size constraints on the widgets too", §166): a
// month in one row is what its drawing makes of one row, clipped to its box, never over a neighbour.
//
// What it replaces: a least and a most, 1 to 32 each, "the content decides between them" (§12.3).
// A least other than one asked for empty room under a short content and was never what anybody set
// it for; the two numbers were two fields where the person wanted one and a mode. A file that still
// has them is read tolerantly (Read): the same two numbers are Fixed at that count, any other pair
// is Automatic up to its most, which is what the line was given for any content not under the least.
//
// The unit is the folder's cell, half a row (FolderOptions.CellDip, 20 DIP at 100 % and the list's text; §442): every
// line of a list is a whole number of them, a row of the list two, and n cells are n times a cell at every scale and
// text size, with no gap between lines or cells — so any two lists whose lines add up to the same cells end level, a
// line of four rows is two lines of two, and a count of cells says the same at every text size and scale. The file
// keeps cells ("cells": 13); the menu says rows, halves and all ("6½ rows"). Pixels would have made "4" mean nothing in
// particular and a line two pixels off its neighbour easy; whole rows made a caption's 52 DIP 77 tall, a band of air
// under it (the person, 2026-10-30: "why is that here that it looks so airy in between?").
//
// A line of items scrolls inside itself where it is given less than its items take (the person,
// 2026-10-11, ListScroll.cs): the wheel moves its items to their end and then the folder, as Windows
// chains a list in a scrolling page. Any other line too short for its content is drawn shortened and
// clipped to its own box (FolderFlyout clips every line), so it never draws over its neighbours.
//
// A third mode, Fill (the person, 2026-10-14: "a new size that you can give a widget which is grow /
// take up remaining space ... only in the folder i meant"; §168): the line is its content's height,
// held to one row and its kind's most, and then takes the room its list has left beside the folder's
// tallest list, shared with the list's other Fill lines (FolderFlyout.Build); in a list past the
// folder's most it is what gives way first, down to one row, before a line is left out (§171). It is
// a flag over the other two, "fill" in the file and absent where it is false, so a file from before
// reads as it did and a build from before reads a Fill line as the mode and rows it keeps under it;
// going back to Automatic or Fixed from Fill finds the rows it had.

using System.Text.Json.Serialization;

namespace Karpo.Core;

/// <summary>The three ways a folder line's height is given (<see cref="LineHeight.Mode"/>).</summary>
public enum LineHeightMode
{
    /// <summary>Its content's height, up to its cells.</summary>
    Automatic,

    /// <summary>Exactly its cells.</summary>
    Fixed,

    /// <summary>The room its list has left, shared with the list's other Fill lines (§168).</summary>
    Fill,
}

/// <summary>
/// A folder widget's height down its list: <see cref="Fixed"/> at exactly <see cref="Cells"/>, or
/// automatic — its content's height, up to <see cref="Cells"/>. In the file
/// <c>"height": { "fixed": false, "cells": 12 }</c>.
/// </summary>
/// <param name="Fixed">Exactly <paramref name="Cells"/> tall; else the content decides, up to them.</param>
/// <param name="Cells">The folder's cells, half a row each, <see cref="Fewest"/> to <see cref="Most"/>: the height, or the most it may be.</param>
public readonly record struct LineHeight(bool Fixed, int Cells)
{
    /// <summary>
    /// Takes the room its list has left (§168), over <see cref="Fixed"/> and <see cref="Cells"/>, which
    /// it keeps for going back. Not written where false, so a file from before is unchanged.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Fill { get; init; }

    /// <summary>
    /// A page's line that grows with its content and, where its list is past
    /// <see cref="GivingMost"/> of the work area or the folder's room, gives way first and scrolls
    /// its items (the sound page's mixer, FolderPages; §200). Never in a file: a page is made where
    /// it opens, and nothing of it is written.
    /// </summary>
    [JsonIgnore]
    public bool GivesWay { get; init; }

    /// <summary>
    /// The share of the work area a list with a <see cref="GivesWay"/> line stops at, under the
    /// folder's own room where that is less (FolderFlyout.Build).
    /// </summary>
    public const float GivingMost = 0.8f;

    /// <summary>Which of the three this is: Fill over the other two.</summary>
    [JsonIgnore]
    public LineHeightMode Mode => Fill ? LineHeightMode.Fill : Fixed ? LineHeightMode.Fixed : LineHeightMode.Automatic;

    /// <summary>This in <paramref name="mode"/>: the same cells, so going back to a mode finds them.</summary>
    public LineHeight As(LineHeightMode mode) => mode switch
    {
        LineHeightMode.Fill => this with { Fill = true },
        LineHeightMode.Fixed => this with { Fixed = true, Fill = false },
        _ => this with { Fixed = false, Fill = false },
    };

    /// <summary>The cells in a row of the list: a line of one row, the kit's 40 DIP.</summary>
    public const int Row = 2;

    /// <summary>The fewest cells either mode takes: a row, a line of the list.</summary>
    public const int Fewest = Row;

    /// <summary>
    /// The most cells either mode takes: past any screen's room, so the room is the real most (§192).
    /// A cell is 20 DIP; the tallest screen Windows drives, 8K on its side at 100 %, is 7680 DIP, 384 cells.
    /// A number past this in a file is a typo and reads as it.
    /// </summary>
    public const int Most = 400;

    /// <summary>As tall as its content, up to <paramref name="most"/> cells.</summary>
    public static LineHeight Automatic(int most) => new(false, Clamp(most));

    /// <summary>Exactly <paramref name="cells"/> cells, whatever its content says.</summary>
    public static LineHeight Exactly(int cells) => new(true, Clamp(cells));

    /// <summary>This, its cells held to <see cref="Fewest"/> to <see cref="Most"/>.</summary>
    public LineHeight Within() => new(Fixed, Clamp(Cells)) { Fill = Fill, GivesWay = GivesWay };

    /// <summary>"fixed 8", "automatic up to 12", "fill" (cells): what a line's plan and a test's message say.</summary>
    public override string ToString() => Fill ? "fill" : Fixed ? $"fixed {Cells}" : $"automatic up to {Cells}";

    /// <summary>
    /// The fewest whole cells <paramref name="px"/> of content takes, a cell being <paramref name="cell"/> px:
    /// <see cref="Fewest"/> at the least, <see cref="Most"/> at the most. What a line's measured content is rounded
    /// up to, so every line of a folder is whole cells (§442).
    /// </summary>
    public static int CellsFor(float px, float cell) =>
        cell <= 0 ? Fewest : Clamp((int)MathF.Ceiling((px - 0.5f) / cell));

    /// <summary>
    /// The height a line is given, in px, whole cells of <paramref name="cell"/> px: exactly its cells where it is
    /// <see cref="Fixed"/>, else <paramref name="content"/> rounded up to whole cells, held to <see cref="Fewest"/> at
    /// the least and its cells at the most; where it is <see cref="Fill"/>, its content held to <see cref="Fewest"/>
    /// and <paramref name="most"/> cells, before the room its list has left is shared out (FolderFlyout.Build).
    /// </summary>
    public float Given(float content, float cell, int most = Most)
    {
        var held = Within();
        var top = held.Fill ? Clamp(most) : held.Cells;
        return cell * (held.Fixed && !held.Fill ? held.Cells : Math.Min(CellsFor(content, cell), Math.Max(Fewest, top)));
    }

    /// <summary>
    /// The height a line of <paramref name="island"/> is given: its own, where its line is a card
    /// with a height (<see cref="Folders.HasHeight(ListKind)"/>) — the one it carries, or where its
    /// kind starts where it carries none; one row, fixed, for the list's own row of an icon and a
    /// name, which is one line whatever anybody asks; and its rows, fixed, for a kind whose line is
    /// always them (<see cref="ListWidget.FixedHeight"/>), whatever height it carries; and for a kind of one
    /// form or a few (§432), its size's cells where nothing has measured it, fixed — the folder gives such a
    /// line its form's whole cells (<see cref="LineSizes"/>), whatever this says.
    /// </summary>
    public static LineHeight For(WidgetItem island) =>
        ListWidgets.Find(island.Info?.Id) is not { } line ? Exactly(Row)
        : line.Forms is not null ? Exactly(LineSizes.CellsOf(island))
        : line.Kind.HasHeight() ? island.Height is { Cells: >= Fewest } own ? own.Within(line.Kind) : StartFor(line.Kind)
        : StartFor(line.Kind);

    /// <summary>
    /// Where a new line of <paramref name="kind"/> starts: automatic, up to the most its kind's
    /// measurement gives it (<see cref="Folders.ListRange(ListKind)"/>), so the content decides, for a
    /// kind whose content varies (<see cref="ListWidget.Grows"/>); its rows, fixed, for one whose
    /// content does not and for a kind of a fixed height; one row, fixed, for the list's own row.
    /// </summary>
    public static LineHeight StartFor(ListKind kind)
    {
        var line = ListWidgets.Of(kind);
        return line.Line != ListLine.Card ? Exactly(Row)
            : line.Forms is not null ? Exactly(LineSizes.CellsOf(line, line.Starts))
            : kind.HasHeight() && line.Grows ? Automatic(kind.ListRange().Most).Within(kind)
            : Exactly(line.Cells).Within(kind);
    }

    /// <summary>
    /// The cells a line of <paramref name="kind"/> may be given, in either mode: <see cref="Fewest"/> to <see cref="Most"/>,
    /// for every kind (§192; the charts that stretched to a most of their own, §432, are sized since §603). One most
    /// wherever a height is made: where a new line starts, a file read, the menu's stepper (§460). On a screen, the
    /// folder's room holds it to fewer (<see cref="Room"/>).
    /// </summary>
    public static (int Least, int Most) Limits(ListKind kind) => (Fewest, Most);

    /// <summary>
    /// The most whole cells <paramref name="px"/> of room holds, a cell being <paramref name="cell"/> px;
    /// <see cref="Fewest"/> at the least, <see cref="Most"/> at the most. What the folder's menu lets a line be set to
    /// on its screen (§192).
    /// </summary>
    public static int Room(float px, float cell) =>
        cell <= 0 ? Most : Clamp((int)MathF.Floor((px + 0.5f) / cell));

    /// <summary>
    /// This, as a line of <paramref name="kind"/> may be: its cells held to the kind's
    /// (<see cref="Limits"/>), and fixed where the kind's content does not vary
    /// (<see cref="ListWidget.Grows"/>) — an automatic height from before (a file, an older build)
    /// is then the kind's own cells, or fewer where its most was fewer, which is what the content was
    /// given. A file's height is read through this, so nothing outside the rules is ever drawn.
    /// </summary>
    public LineHeight Within(ListKind kind)
    {
        var line = ListWidgets.Of(kind);
        var (least, most) = Limits(kind);
        var held = Fixed || line.Grows ? this : Exactly(Math.Min(Cells, line.Cells));
        return new(held.Fixed, Math.Clamp(held.Cells, least, most)) { Fill = Fill, GivesWay = GivesWay };
    }

    private static int Clamp(int cells) => Math.Clamp(cells, Fewest, Most);
}
