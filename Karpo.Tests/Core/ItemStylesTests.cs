// A list line's item style (Karpo.Core\ItemStyles.cs): the size of its items and the parts they show,
// one setting for every line made of items. Optional in the file and backward compatible: a line that
// carries none draws as it always did and writes nothing; a word this build does not know reads as
// Standard; a part a kind's items do not have, or a choice that is its kind's default, is dropped.

using System.Text.Json;

namespace Karpo.Tests.Core;

public sealed class ItemStylesTests
{
    /// <summary>
    /// Every line of items offers the parts its items have, and a size where it draws its items by one
    /// (not the agenda, whose events are the kit's rows); a line of no items offers nothing, and nor do
    /// the removable devices, whose rows are the kit's whatever the size (2026-10-29).
    /// </summary>
    [Fact]
    public void EveryLineOfItemsOffersWhatItsItemsHave()
    {
        Assert.Equal(new ItemOffer(ItemParts.Preview | ItemParts.Time, ItemParts.Preview | ItemParts.Time), ItemStyles.Offer(ListKind.Mail));
        Assert.Equal(ItemParts.Pictures | ItemParts.Time, ItemStyles.Offer(ListKind.Rss)!.Value.Parts);
        Assert.Equal(ItemParts.Preview | ItemParts.Pictures | ItemParts.Time, ItemStyles.Offer(ListKind.NotificationsList)!.Value.Parts);
        Assert.Equal(ItemParts.None, ItemStyles.Offer(ListKind.Todo)!.Value.Parts);
        // A person's address under the name, their picture before it (§457): both shown unless turned off.
        Assert.Equal(new ItemOffer(ItemParts.Preview | ItemParts.Pictures, ItemParts.Preview | ItemParts.Pictures), ItemStyles.Offer(ListKind.Contacts));
        // The agenda's events always say when they are (2026-10-29: "it should not be possible to not show the
        // time in the agenda view") and are never sized: it offers nothing, and a "time": false it was given is dropped.
        Assert.Equal(new ItemOffer(ItemParts.None, ItemParts.None, Sized: false), ItemStyles.Offer(ListKind.Calendar));
        Assert.Null(new ItemStyleOptions { Time = false }.Normalized(ListKind.Calendar));
        // The day's events are the agenda's rows (§447): nothing to choose either, and a size or a "preview": false dropped.
        Assert.Equal(ItemStyles.Offer(ListKind.Calendar), ItemStyles.Offer(ListKind.Events));
        Assert.Null(new ItemStyleOptions { Size = ItemSize.Large, Preview = false }.Normalized(ListKind.Events));
        foreach (var kind in new[] { ListKind.Clipboard, ListKind.Contacts, ListKind.Events, ListKind.ScreenTimeApps })
        {
            Assert.True(ItemStyles.Has(kind), $"{kind} has items");
        }
        foreach (var kind in new[] { ListKind.Clock, ListKind.Battery, ListKind.Toggles, ListKind.Heading, ListKind.RemovableDevices })
        {
            Assert.False(ItemStyles.Has(kind), $"{kind} has no items");
        }
        // What a kind shows by default is among what it offers.
        foreach (var line in ListWidgets.All)
        {
            if (ItemStyles.Offer(line.Kind) is { } offer)
            {
                Assert.Equal(offer.Shown, offer.Shown & offer.Parts);
            }
        }
    }

    /// <summary>A line nobody set draws its kind's look; one that was set draws what it was set to, and only where its kind has the part.</summary>
    [Fact]
    public void ALinesLookIsItsOwnOverItsKinds()
    {
        var mail = ListKind.Mail.New();
        Assert.Equal(new ItemLook(ItemSize.Standard, Preview: true, Pictures: false, Time: true), ItemStyles.Of(mail));

        mail.Items = new ItemStyleOptions { Size = ItemSize.Compact, Preview = false, Pictures = true, Time = false };
        // Pictures are no part of a message: the kind's default stays.
        Assert.Equal(new ItemLook(ItemSize.Compact, Preview: false, Pictures: false, Time: false), ItemStyles.Of(mail));

        var clipboard = ListKind.Clipboard.New();
        Assert.Equal(new ItemLook(ItemSize.Standard, Preview: true, Pictures: true, Time: false), ItemStyles.Of(clipboard));
    }

    /// <summary>
    /// Normalized, a line keeps only what differs from its kind's look: a choice equal to the default
    /// goes, a part its items do not have goes, and a style left with nothing is no style — so a line set
    /// back to its defaults writes nothing. A widget that is no line of items carries none.
    /// </summary>
    [Fact]
    public void OnlyWhatDiffersIsKept()
    {
        var mail = ListKind.Mail.New();
        mail.Items = new ItemStyleOptions { Size = ItemSize.Standard, Time = true, Pictures = false };
        mail.Normalize();
        Assert.Null(mail.Items);

        mail.Items = new ItemStyleOptions { Size = ItemSize.Large, Time = true, Preview = false };
        mail.Normalize();
        Assert.NotNull(mail.Items);
        Assert.Equal(ItemSize.Large, mail.Items!.Size);
        Assert.False(mail.Items.Preview);
        Assert.Null(mail.Items.Time);

        // The agenda's size changes nothing it draws: it is dropped, and so are the removable devices' items.
        var agenda = ListKind.Calendar.New();
        agenda.Items = new ItemStyleOptions { Size = ItemSize.Large };
        agenda.Normalize();
        Assert.Null(agenda.Items);
        Assert.Equal(ItemSize.Standard, ItemStyles.Of(agenda).Size);
        var removable = ListKind.RemovableDevices.New();
        removable.Items = new ItemStyleOptions { Size = ItemSize.Compact };
        removable.Normalize();
        Assert.Null(removable.Items);

        var clock = ListKind.Clock.New();
        clock.Items = new ItemStyleOptions { Size = ItemSize.Compact };
        clock.Normalize();
        Assert.Null(clock.Items);
    }

    /// <summary>
    /// In the file it is <c>"items": { "size": "compact", "preview": false }</c>, and round-trips; a file
    /// from before it has none and reads with everything else as it was; a size this build does not know
    /// reads as Standard.
    /// </summary>
    [Fact]
    public void TheFileSaysItAndOlderFilesReadAsTheyWere()
    {
        var mail = ListKind.Mail.New();
        mail.Id = "m";
        mail.Height = LineHeight.Exactly(8);
        mail.Items = new ItemStyleOptions { Size = ItemSize.Compact, Preview = false };
        mail.Normalize();
        var json = JsonSerializer.Serialize(mail, ConfigJson.Default.CellItem);
        Assert.Contains("\"items\"", json, StringComparison.Ordinal);
        Assert.Contains("\"preview\"", json, StringComparison.Ordinal);
        Assert.Contains("\"compact\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"pictures\"", json, StringComparison.Ordinal);
        var back = JsonSerializer.Deserialize(json, ConfigJson.Default.CellItem)!;
        back.Normalize();
        Assert.Equal(ItemStyles.Of(mail), ItemStyles.Of(back));

        // From before item styles: no "items", and nothing else changes.
        var older = JsonSerializer.Deserialize(json.Replace("\"items\"", "\"itemsFromTheFuture\"", StringComparison.Ordinal), ConfigJson.Default.CellItem)!;
        older.Normalize();
        Assert.Null(older.Items);
        Assert.Equal(LineHeight.Exactly(8), older.Height);
        Assert.Equal(new ItemLook(ItemSize.Standard, true, false, true), ItemStyles.Of(older));

        // A size from a newer build reads as Standard, and the preview chosen stays.
        var newer = JsonSerializer.Deserialize(json.Replace("\"compact\"", "\"huge\"", StringComparison.Ordinal), ConfigJson.Default.CellItem)!;
        newer.Normalize();
        Assert.Equal(ItemSize.Standard, ItemStyles.Of(newer).Size);
        Assert.False(ItemStyles.Of(newer).Preview);

        // A line nobody set writes nothing at all.
        var plain = ListKind.Mail.New();
        plain.Normalize();
        Assert.DoesNotContain("\"items\"", JsonSerializer.Serialize(plain, ConfigJson.Default.CellItem), StringComparison.Ordinal);
    }

    /// <summary>
    /// The mail shows its previews by default (2026-10-11: "the email widget as a default should show
    /// preview text"). A file from before never wrote a preview left off — off was the default then,
    /// and a default is never written — so every mail line that wrote none, whatever else it set, shows
    /// them now; one that wrote "preview": true still does and writes nothing for it any more; one that
    /// wrote "preview": false keeps it off, read, saved and read again.
    /// </summary>
    [Fact]
    public void TheMailShowsPreviewsByDefaultAndAPreviewTurnedOffStaysOff()
    {
        static CellItem Read(string items) =>
            JsonSerializer.Deserialize($$"""{ "id": "m", "kind": "mail"{{items}} }""", ConfigJson.Default.CellItem)!;

        var lines = new[]
        {
            Read(""),
            Read(""", "items": { "size": "compact" }"""),
            Read(""", "items": { "preview": true }"""),
            Read(""", "items": { "preview": false }"""),
            Read(""", "items": { "size": "large", "preview": false, "time": false }"""),
        };
        var config = Configs.RoundTrip(Configs.With(Configs.Holding(lines)), out _);
        var back = config.Layouts[0].Islands[0].Folder!.Cells.Select(cell => cell!.Island).ToList();

        Assert.Equal(
            [
                new ItemLook(ItemSize.Standard, Preview: true, Pictures: false, Time: true),
                new ItemLook(ItemSize.Compact, Preview: true, Pictures: false, Time: true),
                new ItemLook(ItemSize.Standard, Preview: true, Pictures: false, Time: true),
                new ItemLook(ItemSize.Standard, Preview: false, Pictures: false, Time: true),
                new ItemLook(ItemSize.Large, Preview: false, Pictures: false, Time: false),
            ],
            back.Select(line => ItemStyles.Of(line!)));
        // The preview turned on is the default now: nothing written; the two turned off are written.
        Assert.Null(back[2]!.Items);
        Assert.False(back[3]!.Items!.Preview);
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(config.ToJson(), @"""preview"":\s*false").Count);
    }
}
