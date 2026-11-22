// A folder line of a kind that has one form, or a few, and nothing more (docs\widgets.md §432).
//
// The person, 2026-10-30: "a lot of widgets just have one height like the now playing widget or also the
// alarm one. (or maybe just a second height where they actually fix their contents properly). for these
// widgets i would not like the user to be able to change their height manually. it should basically just be
// a fixed height." And: "try to design on the cell height as well, so that it is possible to actually make
// designs where the widgets combined give the same folder height."
//
// So a list line is one of three:
//
//   * **One form** (a timer, the date's header, a toggles row, Win+P's page): no height to set at all. The
//     line is its content, rounded up to whole cells of the folder's.
//   * **A few forms** (now playing, the alarm, a switch, the battery, the clock): a size to choose — Compact,
//     Medium where the kind has a distinct middle form, Full — never a count of rows. Each size is its form,
//     measured, rounded up to whole cells.
//   * **Content that grows** (mail, a list of devices, a chart that stretches): the Height it always had,
//     Automatic, Fixed or Fill and its cells (LineHeight).
//
// **Whole cells, always** (§442). Every size of a sized kind is a whole number of the folder's cells, half a row
// each (20, 40, 60, 80 … DIP at 100 % and the list's text), at every scale and text size, and each form is designed
// to fill its cells: the kit's row two, a row with its caption three (60), the meter's row three. A form that
// measures less than its cells stands in their middle, the little left over shared over and under it. So lines
// stacked in two lists add up to the same height cell for cell, whatever they are.
//
// In the file a sized line keeps its size, `"size": "compact"`, where its kind offers a choice; a kind of
// one form keeps nothing; neither keeps a height.

using System.Text.Json.Serialization;

namespace Karpo.Core;

/// <summary>Which of its forms a sized folder line shows (§432).</summary>
[JsonConverter(typeof(TolerantEnumConverter<LineSize>))]
public enum LineSize
{
    /// <summary>One row: the kit's row, the least the kind says.</summary>
    [JsonStringEnumMemberName("compact")] Compact,

    /// <summary>The middle form, where the kind has one (now playing's row with its caption, the battery's meter).</summary>
    [JsonStringEnumMemberName("medium")] Medium,

    /// <summary>Everything the kind shows.</summary>
    [JsonStringEnumMemberName("full")] Full,
}

/// <summary>
/// One size a sized kind comes in, and the cells its form is where nothing has measured it (a test, a
/// picture without a device): what the measured form rounds up to.
/// </summary>
public readonly record struct LineForm(LineSize Size, int Cells);

/// <summary>The sizes of a sized folder line (see the top of the file).</summary>
public static class LineSizes
{
    /// <summary>Whether a line of <paramref name="kind"/> is its form or forms, with no Height (<see cref="ListWidget.Forms"/>).</summary>
    public static bool Sized(this ListKind kind) => ListWidgets.Find(kind) is { Forms: not null };

    /// <summary>Whether a line of <paramref name="kind"/> offers a size to choose: two forms or more.</summary>
    public static bool Chooses(this ListKind kind) => ListWidgets.Find(kind) is { Forms.Count: > 1 };

    /// <summary>The sizes <paramref name="kind"/> offers, smallest first; none for a kind whose content grows.</summary>
    public static IReadOnlyList<LineSize> Offered(this ListKind kind) =>
        ListWidgets.Find(kind)?.Forms is { } forms ? [.. forms.Select(form => form.Size)] : [];

    /// <summary>
    /// The size <paramref name="item"/>'s line shows: the one it carries where its kind offers it, else the one
    /// its kind starts at; <see cref="LineSize.Full"/> for anything that is not a sized line.
    /// </summary>
    public static LineSize Of(WidgetItem item) => Of(ListWidgets.Find(item.Info?.Id), item.Size);

    /// <summary><paramref name="size"/> as a line of <paramref name="line"/> may be: one of its sizes, else the one it starts at.</summary>
    public static LineSize Of(ListWidget? line, LineSize? size) =>
        line?.Forms is not { } forms ? LineSize.Full
        : size is { } chosen && forms.Any(form => form.Size == chosen) ? chosen
        : line.Starts;

    /// <summary>The cells <paramref name="item"/>'s form is where nothing has measured it; a row for anything that is not a sized line.</summary>
    public static int CellsOf(WidgetItem item) => CellsOf(ListWidgets.Find(item.Info?.Id), Of(item));

    /// <summary>The cells a line of <paramref name="line"/> in <paramref name="size"/> is where nothing has measured it.</summary>
    public static int CellsOf(ListWidget? line, LineSize size) =>
        line?.Forms?.FirstOrDefault(form => form.Size == size) is { Cells: > 0 } form ? form.Cells : LineHeight.Row;
}
