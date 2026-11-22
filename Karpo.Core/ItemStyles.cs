// How a list line's items look (the person, 2026-10-11: "there should be different item styles that
// you should be able to select, for example the size of how the items are shown, if they have
// preview text, etc.").
//
// One setting for every line made of items — the mail, a feed, the notifications, the clipboard,
// the events, the contacts, the to-dos, the agenda, screen time's apps, the desktops and the top processes —
// rather than one per widget: a size for the items (Compact, Standard, Large: their text, the room
// between them and their pictures) and the parts a kind's items have that a person may leave out
// or add — a preview of what the item says, its picture, its time. A kind offers only the parts its
// items have, and the size only where it draws its items by it (ItemStyles.Offer): the agenda's
// events are the kit's rows (§401), whatever the size, so it offers their time alone. Its defaults are the look it had before there was a choice, so a
// file that never set one draws as it always did, and nothing is written for it — but for the
// mail's preview, which shows by default since the person asked for it (2026-10-11).
//
// In the file: "items": { "size": "compact", "preview": true } — every field optional, and a word
// this build does not know reads as Standard (TolerantEnumConverter).

using System.Text.Json.Serialization;

namespace Karpo.Core;

/// <summary>How large a list line's items are drawn: their text, the room between them, their pictures.</summary>
[JsonConverter(typeof(TolerantEnumConverter<ItemSize>))]
public enum ItemSize
{
    /// <summary>The look every line had before there was a choice.</summary>
    [JsonStringEnumMemberName("standard")] Standard,

    /// <summary>Smaller text, tighter rows, smaller pictures: more items in the same height.</summary>
    [JsonStringEnumMemberName("compact")] Compact,

    /// <summary>Larger text, roomier rows, larger pictures.</summary>
    [JsonStringEnumMemberName("large")] Large,
}

/// <summary>The parts of an item a person may show or leave out.</summary>
[Flags]
public enum ItemParts
{
    None = 0,

    /// <summary>A preview of what the item says: a message's first words, a notification's body, a copied text's further lines, a person's email, an event's place.</summary>
    Preview = 1,

    /// <summary>Its picture: a headline's, a notification's app icon and its own, a copied picture's thumbnail, a person's photo.</summary>
    Pictures = 2,

    /// <summary>When it came or is: a message's, a headline's age, a notification's, an event's time.</summary>
    Time = 4,
}

/// <summary>
/// A line's item style as the file has it: every field optional, null meaning the kind's default
/// (<see cref="ItemStyles.Of"/>). A line nobody set carries none.
/// </summary>
public sealed class ItemStyleOptions
{
    /// <summary>How large the items are.</summary>
    public ItemSize Size { get; set; }

    /// <summary>Whether the items show a preview; null for the kind's default.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Preview { get; set; }

    /// <summary>Whether the items show their pictures; null for the kind's default.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Pictures { get; set; }

    /// <summary>Whether the items show their time; null for the kind's default.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Time { get; set; }

    /// <summary>
    /// These options for a line of <paramref name="kind"/>: a part its items do not have dropped, a
    /// choice equal to the kind's default dropped, and none at all (null) where nothing is left —
    /// so a line set back to its defaults writes nothing.
    /// </summary>
    public ItemStyleOptions? Normalized(ListKind kind)
    {
        if (ItemStyles.Offer(kind) is not { } offer)
        {
            return null;
        }
        bool? Kept(bool? value, ItemParts part) =>
            value is { } set && offer.Parts.HasFlag(part) && set != offer.Shown.HasFlag(part) ? set : null;
        Preview = Kept(Preview, ItemParts.Preview);
        Pictures = Kept(Pictures, ItemParts.Pictures);
        Time = Kept(Time, ItemParts.Time);
        Size = offer.Sized && Enum.IsDefined(Size) ? Size : ItemSize.Standard;
        return Size == ItemSize.Standard && Preview is null && Pictures is null && Time is null ? null : this;
    }
}

/// <summary>
/// What a kind's items have that a person may choose (<see cref="Parts"/>), which of them show by default
/// (<see cref="Shown"/>), and whether their size is chosen too (<see cref="Sized"/>): only where it changes
/// how they are drawn.
/// </summary>
public readonly record struct ItemOffer(ItemParts Parts, ItemParts Shown, bool Sized = true);

/// <summary>A line's item style, resolved: what its items are drawn with.</summary>
public readonly record struct ItemLook(ItemSize Size, bool Preview, bool Pictures, bool Time)
{
    /// <summary>The look of a line that offers no choice, or that nobody set: Standard, everything its kind shows by default.</summary>
    public static ItemLook Default(ItemOffer offer) =>
        new(ItemSize.Standard, offer.Shown.HasFlag(ItemParts.Preview), offer.Shown.HasFlag(ItemParts.Pictures), offer.Shown.HasFlag(ItemParts.Time));
}

/// <summary>Which lines have an item style, and what each offers (see the top of the file).</summary>
public static class ItemStyles
{
    /// <summary>
    /// What a line of <paramref name="kind"/> offers: the parts its items have, and which of them it
    /// showed before there was a choice; null for a line that is not made of items.
    /// </summary>
    public static ItemOffer? Offer(ListKind kind) => kind switch
    {
        // Sender over subject and when it came, and the message's first words under them: the one
        // default that is not the look from before there was a choice (2026-10-11: "the email widget
        // as a default should show preview text"). A file never wrote a preview left off, which was
        // the default then, so every line that wrote none shows it now; one turned off since writes
        // "preview": false.
        ListKind.Mail => new(ItemParts.Preview | ItemParts.Time, ItemParts.Preview | ItemParts.Time),
        // A headline over where it is from and its age, its picture at the end.
        ListKind.Rss => new(ItemParts.Pictures | ItemParts.Time, ItemParts.Pictures | ItemParts.Time),
        // The app's icon, its title over its body, when it came.
        ListKind.NotificationsList => new(ItemParts.Preview | ItemParts.Pictures | ItemParts.Time, ItemParts.Preview | ItemParts.Pictures | ItemParts.Time),
        // A copied text's first three lines, a copied picture's thumbnail.
        ListKind.Clipboard => new(ItemParts.Preview | ItemParts.Pictures, ItemParts.Preview | ItemParts.Pictures),
        // A person's name over their email or phone, their picture before it (§457).
        ListKind.Contacts => new(ItemParts.Preview | ItemParts.Pictures, ItemParts.Preview | ItemParts.Pictures),
        // The agenda's events are the kit's rows (§401), never sized (§406), and always say when they are
        // (2026-10-29: "it should not be possible to not show the time in the agenda view", §408): nothing to choose.
        // The day's events are the agenda's rows (§447): nothing to choose either.
        ListKind.Calendar or ListKind.Events => new(ItemParts.None, ItemParts.None, Sized: false),
        // Items with nothing to leave out: their size only.
        ListKind.Todo or ListKind.ScreenTimeApps or ListKind.Desktops or ListKind.CpuProcesses => new(ItemParts.None, ItemParts.None),
        _ => null,
    };

    /// <summary>Whether a line of <paramref name="kind"/> has an item style.</summary>
    public static bool Has(ListKind kind) => Offer(kind) is not null;

    /// <summary>The kind <paramref name="item"/> is as a line of a list, where it is one.</summary>
    public static ListKind? KindOf(WidgetItem item) => item is CellItem { List: { } list } ? list : ListWidgets.Find(item.Info?.Id)?.Kind;

    /// <summary>
    /// The item style <paramref name="item"/>'s line is drawn with: what it carries over its kind's
    /// defaults; the defaults (Standard, what the kind always showed) where it carries none or is no
    /// line of items.
    /// </summary>
    public static ItemLook Of(WidgetItem item)
    {
        var offer = KindOf(item) is { } kind && Offer(kind) is { } offered ? offered : new ItemOffer(ItemParts.None, ItemParts.None);
        var look = ItemLook.Default(offer);
        if (item.Items is not { } set)
        {
            return look;
        }
        return new ItemLook(
            offer.Sized && Enum.IsDefined(set.Size) ? set.Size : ItemSize.Standard,
            offer.Parts.HasFlag(ItemParts.Preview) ? set.Preview ?? look.Preview : look.Preview,
            offer.Parts.HasFlag(ItemParts.Pictures) ? set.Pictures ?? look.Pictures : look.Pictures,
            offer.Parts.HasFlag(ItemParts.Time) ? set.Time ?? look.Time : look.Time);
    }
}
