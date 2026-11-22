// The sizes a list line's items are drawn at, for the item style a person chose (Karpo.Core\ItemStyles.cs).
//
// One answer for every line of items, so "Compact" means the same thing in the mail as in the
// clipboard: the items' text a size smaller, the room in and between them halved, their pictures
// three quarters as large; "Large" a size larger, half as much room again, a third larger pictures.
// Standard is exactly the list form's own sizes (ListForm), which is what every line drew before
// there was a choice. The line's head — its count, its title, its buttons — is not an item and keeps
// the list's sizes, so lines side by side keep one header whatever their items are set to.

using Karpo.Core;

namespace Karpo.Dock;

/// <summary>What a line's items are drawn with: their text sizes, their spacing and their pictures, and which parts show.</summary>
internal readonly record struct ItemForm
{
    /// <summary>The style chosen.</summary>
    public required ItemLook Look { get; init; }

    /// <summary>An item's first line: its sender, its title, its name.</summary>
    public required float TextPx { get; init; }

    /// <summary>An item's further lines: its subject, its source, its detail, its time.</summary>
    public required float SmallPx { get; init; }

    /// <summary>A line of <see cref="TextPx"/>, measured.</summary>
    public required float Line { get; init; }

    /// <summary>A line of <see cref="SmallPx"/>, measured.</summary>
    public required float SmallLine { get; init; }

    /// <summary>How much of the room in and between items this size keeps: ½, 1 or 1½.</summary>
    public required float Room { get; init; }

    /// <summary>How large a picture is against Standard: ¾, 1 or 1⅓.</summary>
    public required float Picture { get; init; }

    /// <summary>Whether the items show a preview (<see cref="ItemParts.Preview"/>).</summary>
    public bool Preview => Look.Preview;

    /// <summary>Whether the items show their pictures (<see cref="ItemParts.Pictures"/>).</summary>
    public bool Pictures => Look.Pictures;

    /// <summary>Whether the items show their time (<see cref="ItemParts.Time"/>).</summary>
    public bool Time => Look.Time;

    /// <summary>The sizes for <paramref name="look"/> in <paramref name="style"/>.</summary>
    public static ItemForm Of(Gfx gfx, in TileStyle style, ItemLook look)
    {
        var (text, small, room, picture) = look.Size switch
        {
            ItemSize.Compact => (13f, 11f, 0.5f, 0.75f),
            ItemSize.Large => (16f, 13f, 1.5f, 4f / 3f),
            _ => (14f, 12f, 1f, 1f),
        };
        return new ItemForm
        {
            Look = look,
            TextPx = style.Text(text),
            SmallPx = style.Text(small),
            Line = TilePaint.LineHeight(gfx, style.Text(text)),
            SmallLine = TilePaint.LineHeight(gfx, style.Text(small)),
            Room = room,
            Picture = picture,
        };
    }

    /// <summary>The sizes <paramref name="island"/>'s items are drawn with: its own item style (ItemStyles.Of).</summary>
    public static ItemForm For(Gfx gfx, in TileStyle style, WidgetItem island) => Of(gfx, style, ItemStyles.Of(island));

    /// <summary><paramref name="dip"/> DIPs of room in or between items, at this size: whole px, never under one where it was some.</summary>
    public float Space(in TileStyle style, float dip) => dip <= 0 ? 0 : MathF.Max(1, MathF.Round(dip * Room * style.Scale, MidpointRounding.AwayFromZero));

    /// <summary>A picture <paramref name="px"/> px across at Standard, at this size.</summary>
    public float Side(float px) => MathF.Round(px * Picture);
}
