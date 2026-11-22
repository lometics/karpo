// edit, §689 (the person, 2026-11-12: "the errors flyout should be styled the same as the other configuration pages"): the
// errors a page of the edit bar's flyout, hung from their indicator — each a card, its icon, what failed, why under it, since
// when and a chevron at its end; no entry of its own on the bar.

using System.Numerics;
using Karpo.Dock;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Edit;

public sealed class ErrorsPageTests
{
    private static readonly DateTime Now = new(2026, 11, 12, 18, 0, 0, DateTimeKind.Local);

    private static List<Fault> Two() =>
    [
        new("weather", FaultKind.Widget, "Weather", OptionGlyphs.Cloud, "The weather service didn't answer", "bar/1", Now.AddMinutes(-5).ToUniversalTime()),
        new("account", FaultKind.Account, "me@example.com", OptionGlyphs.Accounts, "Sign in again to sync", "me@example.com", Now.AddDays(-1).ToUniversalTime()),
    ];

    /// <summary>Each error a link row: its name, its why its second line, since when at its end, its icon.</summary>
    [Fact]
    public void EachErrorIsACardSayingWhyAndSinceWhen()
    {
        var page = PanelRows.Page(PanelPage.Errors, S.DockEditErrors(2), PanelRows.GlyphOf(PanelPage.Errors), TheDock.FaultItems(Two(), Now));
        Assert.Equal(2, page.Rows.Count);
        Assert.All(page.Rows, row => Assert.Equal(PanelRowKind.Link, row.Kind));
        Assert.Equal(["Weather", "me@example.com"], page.Rows.Select(row => row.Text));
        Assert.Equal(["The weather service didn't answer", "Sign in again to sync"], page.Rows.Select(row => row.Second));
        Assert.Equal(TheDock.FaultSince(Two()[0].SinceUtc, Now), page.Rows[0].Value);
        Assert.All(page.Rows, row => Assert.True(row.Tall));
    }

    /// <summary>The page has no entry of its own on the bar; the errors' indicator stands selected while it is open.</summary>
    [Fact]
    public void ItHangsFromTheIndicatorWithNoEntryOfItsOwn()
    {
        var errors = PanelRows.Page(PanelPage.Errors, S.DockEditErrors(2), PanelRows.GlyphOf(PanelPage.Errors), TheDock.FaultItems(Two(), Now));
        var content = new SettingsContent([.. SettingsPanelTests.Content().Pages, errors]);
        var layout = new PanelLayout(new SettingsPanelTests.Words(), 1.5f, new PanelInput(2, content, PanelPage.Errors), new Vector2(1600, 1500));
        Assert.DoesNotContain(layout.Header, part => part.Kind == PanelHitKind.Nav && part.Page == PanelPage.Errors);
        Assert.True(layout.Opened(new PanelHit(PanelHitKind.Errors)));
        Assert.Equal(PanelPage.Errors, PanelRows.EntryOf(PanelPage.Errors));
    }
}
