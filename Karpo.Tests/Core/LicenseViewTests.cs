// What the licence says for each licence (Karpo.Core\LicenseView.cs, docs\widgets.md §138): the License
// page's words, and ⚙ › Karpo Pro's (§339).

namespace Karpo.Tests.Core;

public sealed class LicenseViewTests
{
    private static string Date(DateTimeOffset day) => day.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    [Fact]
    public void EveryBasisHasItsOwnWords()
    {
        foreach (var basis in Enum.GetValues<LicenseBasis>())
        {
            var tier = basis is LicenseBasis.StorePro or LicenseBasis.OfflineGrace ? LicenseTier.Pro
                : basis == LicenseBasis.DeveloperBuild ? LicenseTier.Unrestricted
                : LicenseTier.Free;
            var status = new LicenseStatus(tier, basis, basis == LicenseBasis.OfflineGrace ? new DateTimeOffset(2026, 10, 18, 0, 0, 0, TimeSpan.Zero) : null);
            Assert.False(string.IsNullOrWhiteSpace(LicenseView.Title(status)), $"{basis} has no title");
            Assert.False(string.IsNullOrWhiteSpace(LicenseView.Detail(status, Date)), $"{basis} has no detail");
        }
        var grace = new LicenseStatus(LicenseTier.Pro, LicenseBasis.OfflineGrace, new DateTimeOffset(2026, 10, 18, 0, 0, 0, TimeSpan.Zero));
        Assert.Contains("2026-10-18", LicenseView.Detail(grace, Date), StringComparison.Ordinal);
    }

    /// <summary>The usage and the offer only where they apply: a developer build shows what it is and nothing to buy.</summary>
    [Fact]
    public void ADeveloperBuildShowsItsStatusAlone()
    {
        Assert.False(LicenseView.ShowsUsage(LicenseStatus.Developer));
        Assert.False(LicenseView.ShowsStore(LicenseStatus.Developer));

        var free = new LicenseStatus(LicenseTier.Free, LicenseBasis.DeveloperFree);
        Assert.True(LicenseView.ShowsUsage(free));
        Assert.True(LicenseView.ShowsStore(free));
        Assert.True(LicenseView.OffersPro(free));
        Assert.False(LicenseView.OffersPro(new LicenseStatus(LicenseTier.Pro, LicenseBasis.StorePro)));
    }

    [Fact]
    public void TheUsageIsEachLayoutsCount()
    {
        var config = new Config
        {
            Layouts =
            [
                new TaskbarLayout { Name = "Work", Islands = [new BarItem { Kind = BarKind.Volume }, new BarItem { Kind = BarKind.Pin }] },
                new TaskbarLayout { Name = "Play", Islands = [] },
            ],
        };

        Assert.Equal([("Work", 1), ("Play", 0)], LicenseView.Usage(config));
        Assert.Equal(S.SettingsLicenseUsageOf(count: 12, limit: 20), LicenseView.Of(12, 20));
    }

    [Fact]
    public void ThePitchSaysWhyProCanNotBeBought()
    {
        Assert.Equal(S.SettingsLicensePitch, LicenseView.Pitch(canPurchase: true, hasStore: true));
        Assert.Equal(S.SettingsLicenseNotOnSale, LicenseView.Pitch(canPurchase: false, hasStore: true));
        Assert.Equal(S.SettingsLicenseStoreMissing, LicenseView.Pitch(canPurchase: false, hasStore: false));
    }
}
