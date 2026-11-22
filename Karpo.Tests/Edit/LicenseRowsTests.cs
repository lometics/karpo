// The add list on a layout at the free version's limit (Karpo.Dock\LicenseRows.cs, docs\widgets.md
// §138): what would count is greyed with the limit as its reason, what doesn't count stays, and
// "Get Karpo Pro" says why at the top. LicensePictures shows it.

using Karpo.Dock;

namespace Karpo.Tests.Edit;

public sealed class LicenseRowsTests
{
    private const uint First = Karpo.Dock.Dock.CMD_ADD_WIDGET;
    private const uint GetPro = Karpo.Dock.Dock.CMD_GET_PRO;

    private static (List<FlyoutItem> Items, List<CatalogueEntry> Entries, List<Preset> Presets) Limited()
    {
        var (items, entries, presets) = AddList.Rows(WidgetType.Bar, turns: true, First);
        return (LicenseRows.Limited(items, entries, presets, First, "limit", "why", GetPro), entries, presets);
    }

    [Fact]
    public void WhatWouldCountIsGreyedWithTheLimitAndTheRestStays()
    {
        var (items, entries, presets) = Limited();
        var picks = AddList.Picks(items).Where(item => item.Id != GetPro).ToList();
        var plains = AddList.Picks(AddList.Rows(WidgetType.Bar, turns: true, First).Items).ToList();

        Assert.NotEmpty(picks);
        Assert.Equal(plains.Count, picks.Count);
        foreach (var (pick, plain) in picks.Zip(plains))
        {
            Assert.Equal(plain.Id, pick.Id);
            var at = (int)(pick.Id - First);
            if (at >= entries.Count)
            {
                // A ready-made folder is a folder on the bar.
                Assert.False(pick.Enabled, $"{pick.Text} is a folder and counts");
                Assert.Equal("limit", pick.Detail);
                continue;
            }
            var kind = entries[at].Bar!.Value;
            if (LicenseCount.Counts(kind))
            {
                Assert.False(pick.Enabled, $"{pick.Text} counts");
                Assert.Equal("limit", pick.Detail);
            }
            else
            {
                // A pin, Windows' own buttons: as the list has them anyway.
                Assert.Equal(plain with { Words = null }, pick with { Words = null });
                Assert.Equal(plain.Words, pick.Words);
            }
        }
        Assert.Contains(picks, pick => pick.Enabled);
        Assert.NotEmpty(presets);
    }

    [Fact]
    public void GetKarpoProStandsFirstUnderTheFirstHeadingSayingWhy()
    {
        var (items, _, _) = Limited();

        Assert.True(items[0].Header);
        var offer = items[1];
        Assert.Equal(GetPro, offer.Id);
        Assert.Equal(S.CoreLicenseGetPro, offer.Text);
        Assert.Equal("why", offer.Detail);
        Assert.True(offer.Enabled);
        Assert.Single(AddList.Picks(items), item => item.Id == GetPro);
    }

    [Fact]
    public void ARefusalSaysWhyAndOffersPro()
    {
        var rows = LicenseRows.Refusal(20, GetPro);

        Assert.Equal(S.DockLicenseWidgets(20), rows[0].Text);
        Assert.Equal(S.DockLicenseUsed(count: 20, limit: 20), rows[0].Detail);
        Assert.False(rows[0].Enabled);
        Assert.Equal(GetPro, rows[^1].Id);
    }
}
