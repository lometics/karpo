// edit, §686, §695 (the person, 2026-11-12: "when hovering over a style it should not show the blue selection box even though i
// have not selected it", "the reset button also appears when i am just hovering over a color value. this should obviously also
// not be"): while a choice is tried on, the page is the person's own config's — its marks, its values, its resets —, and only
// its pictures of the whole look the config worn.

using Karpo.Dock;

namespace Karpo.Tests.Edit;

public sealed class TriedOnPreviewsTests
{
    /// <summary>The Appearance page of the person's config: no reset for a colour only tried on; its preview the worn config.</summary>
    [Fact]
    public void ATriedColourHasNoResetAndThePreviewWearsIt()
    {
        var persons = new Config();
        var worn = persons.Clone();
        var background = LookRows.ColourKey(LookRows.SectionKey(LookPart.Taskbar), Tone.Background);
        Assert.True(Looks.WritePaint(Looks.Section(worn, LookPart.Taskbar), Tone.Background, dark: true, Karpo.Core.Paint.Parse("#FFFFFF")));
        // Made of the worn config, the reset stood beside the colour tried on: what was shown.
        Assert.NotNull(OptionRows.Find(LookRows.Rows(worn, worn.DefaultLayout, windows: true), background + LookRows.ResetEnd));
        // Made of the person's, none.
        var rows = LookRows.Rows(persons, persons.DefaultLayout, windows: true);
        Assert.Null(OptionRows.Find(rows, background + LookRows.ResetEnd));
        var items = OptionMenu.Rows(rows, []);
        var shown = TriedOnPreviews.Of(items, persons, worn);
        var looks = Flat(shown).Where(item => item.Shows is not null).ToList();
        Assert.NotEmpty(looks);
        Assert.All(looks, item => Assert.Same(worn, item.Shows!.Config));
        Assert.Equal(Flat(items).Select(item => (item.Id, item.Text, item.Checked)), Flat(shown).Select(item => (item.Id, item.Text, item.Checked)));
    }

    /// <summary>A picture of a choice (its own copy of the config, a choice set on it) left as it is.</summary>
    [Fact]
    public void AChoicesOwnPictureIsLeftAsItIs()
    {
        var persons = new Config();
        var worn = persons.Clone();
        var own = persons.Clone();
        var choice = new FlyoutItem(2, "Choice", Checked: true, Shows: new LookShown(own, own.DefaultLayout));
        var shown = TriedOnPreviews.Of([new FlyoutItem(1, "Row", Children: [choice], PickOne: true)], persons, worn);
        Assert.Same(own, shown[0].Children![0].Shows!.Config);
    }

    private static IEnumerable<FlyoutItem> Flat(IEnumerable<FlyoutItem> items) =>
        items.SelectMany(item => (IEnumerable<FlyoutItem>)[item, .. Flat(item.Children ?? [])]);
}
