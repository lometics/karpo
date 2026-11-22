// The colour of each calendar, inbox and address book (SourceColours, Account.Colours, docs\accounts.md
// "Colours"): the person's pick, else the account's own, else the palette's by place — picking one
// moves no other; kept in accounts.json as a patch, a file of before read intact with nothing picked,
// a hand edit there never costing the accounts; and a pick alone a change the dock draws, not reads.

using Karpo.Accounts;
using Karpo.Dock.Pim;
using Karpo.Tests.Pim;

namespace Karpo.Tests.Accounts;

public sealed class PartColourTests : IDisposable
{
    private readonly string folder = Path.Combine(Isolation.Folder, "part-colour", Guid.NewGuid().ToString("N"));

    private string File => Path.Combine(folder, "accounts.json");

    public void Dispose()
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private static Account Microsoft(string id) => new() { Id = id, Kind = AccountKind.Microsoft, Address = id + "@outlook.com" };

    [Theory]
    [InlineData("#3a96dd", "#3A96DD")]
    [InlineData(" #F80 ", "#FF8800")]
    [InlineData("#11223344", "#112233")]
    [InlineData("blue", null)]
    [InlineData("#12345", null)]
    [InlineData("#GGGGGG", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void AColourReadsAsHashAndSixHexDigitsOrNotAtAll(string? value, string? normal) =>
        Assert.Equal(normal, SourceColours.Normal(value));

    [Fact]
    public void APartWithoutAColourTakesThePalettesFirstThatNoAccountGivesAndNoPartBeforeTook()
    {
        var blue = SourceColours.Palette[5];
        var teal = SourceColours.Palette[4];
        var purple = SourceColours.Palette[7];
        // The account gives the first one blue: the next two start after it, never blue again.
        var colours = SourceColours.Assign([(null, blue), (null, null), (null, null)]);
        Assert.Equal([blue, teal, purple], colours.Select(colour => colour.Colour));
        Assert.Equal([blue, teal, purple], colours.Select(colour => colour.Automatic));
        // Twelve parts and more: every colour once, then round again.
        var many = SourceColours.Assign([.. Enumerable.Repeat<(string?, string?)>((null, null), 14)]);
        Assert.Equal(12, many.Take(12).Select(colour => colour.Colour).Distinct().Count());
        Assert.Equal(many[0].Colour, many[12].Colour);
    }

    [Fact]
    public void PickingAColourForOnePartMovesNoOther()
    {
        var unpicked = SourceColours.Assign([(null, null), (null, null), (null, null)]);
        var picked = SourceColours.Assign([(SourceColours.Palette[4], null), (null, null), (null, null)]);
        // The first wears its pick (teal, the second's colour unpicked); its own stays its Automatic.
        Assert.Equal(SourceColours.Palette[4], picked[0].Colour);
        Assert.Equal(unpicked[0].Colour, picked[0].Automatic);
        Assert.Equal(unpicked.Skip(1), picked.Skip(1));
    }

    [Fact]
    public void AColourPickedIsKeptOnTheAccountAndAutomaticAgainLeavesNoTrace()
    {
        var account = Microsoft("a");
        Assert.Null(account.Colour(SyncKeys.Inbox));
        var picked = account.Coloured(SyncKeys.Inbox, "#d13438").Coloured(SyncKeys.Calendar("work"), "#038387");
        Assert.Equal("#D13438", picked.Colour(SyncKeys.Inbox));
        Assert.Equal("#038387", picked.Colour(SyncKeys.Calendar("work")));
        Assert.Null(picked.Colour(SyncKeys.Book("work")));
        Assert.Same(picked, picked.Coloured(SyncKeys.Inbox, "#D13438"));
        // Something that is no colour is Automatic.
        Assert.Null(picked.Coloured(SyncKeys.Inbox, "red").Colour(SyncKeys.Inbox));
        Assert.Null(picked.Coloured(SyncKeys.Inbox, null).Coloured(SyncKeys.Calendar("work"), null).Colours);
        Assert.Same(account, account.Coloured(SyncKeys.Inbox, null));
    }

    [Fact]
    public void AFileOfBeforeReadsIntactWithNothingPickedAndAPickIsAPatch()
    {
        Directory.CreateDirectory(folder);
        // accounts.json as every Karpo before 2026-10-12 wrote it: no "colours" anywhere.
        System.IO.File.WriteAllText(File, """
            { "version": 1, "accounts": [
              { "id": "a", "kind": "Microsoft", "address": "a@outlook.com", "off": [ "calendar:birthdays" ] },
              { "id": "b", "kind": "Imap", "address": "b@example.com", "host": "imap.example.com", "port": 993, "security": "Implicit" }
            ] }
            """);
        var before = AccountBook.Read(File, out var read).Accounts;
        Assert.Equal(AccountsRead.Ok, read);
        Assert.Equal(["a", "b"], before.Select(account => account.Id));
        Assert.All(before, account => Assert.Null(account.Colours));
        Assert.Equal([SyncKeys.Calendar("birthdays")], before[0].Off);

        AccountBook.SetColour(File, "a", SyncKeys.Calendar("work"), "#8764b8");
        AccountBook.SetColour(File, "b", SyncKeys.Inbox, "#C19C00");
        AccountBook.SetColour(File, "gone", SyncKeys.Inbox, "#C19C00");

        var after = AccountBook.Read(File, out _).Accounts;
        Assert.Equal(["a", "b"], after.Select(account => account.Id));
        Assert.Equal("#8764B8", after[0].Colour(SyncKeys.Calendar("work")));
        Assert.Equal("#C19C00", after[1].Colour(SyncKeys.Inbox));
        // Nothing else of the accounts moved.
        Assert.Equal([SyncKeys.Calendar("birthdays")], after[0].Off);
        Assert.Equal("imap.example.com", after[1].Host);

        AccountBook.SetColour(File, "a", SyncKeys.Calendar("work"), null);
        AccountBook.SetColour(File, "b", SyncKeys.Inbox, null);
        Assert.All(AccountBook.Read(File, out _).Accounts, account => Assert.Null(account.Colours));
        Assert.DoesNotContain("\"colours\"", System.IO.File.ReadAllText(File));
    }

    [Fact]
    public void AHandEditOfTheColoursNeverCostsTheAccounts()
    {
        Directory.CreateDirectory(folder);
        System.IO.File.WriteAllText(File, """
            { "version": 1, "accounts": [
              { "id": "a", "kind": "Microsoft", "address": "a@outlook.com", "colours": { "mail": 5, "calendar:x": "teal", "calendar:y": "#4f6bed", "z": [ 1 ] } },
              { "id": "b", "kind": "Microsoft", "address": "b@outlook.com", "colours": "blue" }
            ] }
            """);
        var accounts = AccountBook.Read(File, out var read).Accounts;
        Assert.Equal(AccountsRead.Ok, read);
        Assert.Equal(["a", "b"], accounts.Select(account => account.Id));
        Assert.Equal(["calendar:y"], accounts[0].Colours!.Keys);
        Assert.Equal("#4F6BED", accounts[0].Colour("calendar:y"));
        Assert.Null(accounts[1].Colours);
    }

    [Fact]
    public void APickAloneIsDrawnNotRead()
    {
        var reader = new FakeReader();
        var set = new AccountSet([reader]);
        var changed = 0;
        var recoloured = 0;
        set.Changed += () => changed++;
        set.Recoloured += () => recoloured++;
        Assert.Equal(AccountSet.Shape([reader.Account]), AccountSet.Shape([reader.Account.Coloured(SyncKeys.Inbox, "#D13438")]));

        set.Become([reader.Coloured(SyncKeys.Inbox, "#D13438")]);
        Assert.Equal((0, 1), (changed, recoloured));
        Assert.Equal("#D13438", set.Readers.Single().Account.Colour(SyncKeys.Inbox));

        // A part turned off as well is a change: read.
        set.Become([reader.Turned(SyncKeys.Inbox, false)]);
        Assert.Equal((1, 1), (changed, recoloured));
    }
}
