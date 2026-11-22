// What piles up beside Karpo's files over the months (DurableFile.Tidy, SportsLogos.Sweep): the copies
// kept of a file that did not read or that an import or a reset replaced, the temporary files a crash
// left, the logos nobody draws any more — each let go of, and never the file itself, its backup, a copy
// among the newest few, a write in progress or a logo still in use.

using Karpo.Dock.Sports;

namespace Karpo.Tests.Persistence;

public sealed class TidyTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "karpo-tidy-" + Guid.NewGuid().ToString("N"));

    public TidyTests() => Directory.CreateDirectory(folder);

    public void Dispose() => Directory.Delete(folder, recursive: true);

    private string Touch(string name, TimeSpan age)
    {
        var path = Path.Combine(folder, name);
        File.WriteAllText(path, name);
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow - age);
        return path;
    }

    private string[] Left() => [.. Directory.GetFiles(folder).Select(Path.GetFileName).Order(StringComparer.Ordinal)!];

    [Fact]
    public void TheNewestFiveOfEachKindOfCopyAreKeptAndNothingElseOfTheFiles()
    {
        var file = Touch("accounts.json", TimeSpan.Zero);
        Touch("accounts.json.bak", TimeSpan.Zero);
        for (var day = 1; day <= 8; day++)
        {
            Touch($"accounts.json.broken-202610{day:00}-120000", TimeSpan.FromDays(40 - day));
            Touch($"accounts.json.before-import-202610{day:00}-120000", TimeSpan.FromDays(40 - day));
        }
        Touch("accounts.json.broken-20261008-120000-2", TimeSpan.Zero);
        Touch("notebook.json.broken-20261001-120000", TimeSpan.FromDays(40));

        DurableFile.Tidy(file);

        var left = Left();
        Assert.Contains("accounts.json", left);
        Assert.Contains("accounts.json.bak", left);
        // The newest by their times: the second kept on the 8th, the 8th, the 7th, the 6th and the 5th.
        Assert.Equal(
            ["accounts.json.broken-20261005-120000", "accounts.json.broken-20261006-120000", "accounts.json.broken-20261007-120000",
             "accounts.json.broken-20261008-120000", "accounts.json.broken-20261008-120000-2"],
            left.Where(name => name.StartsWith("accounts.json.broken-", StringComparison.Ordinal)));
        Assert.Equal(
            ["accounts.json.before-import-20261004-120000", "accounts.json.before-import-20261005-120000", "accounts.json.before-import-20261006-120000",
             "accounts.json.before-import-20261007-120000", "accounts.json.before-import-20261008-120000"],
            left.Where(name => name.StartsWith("accounts.json.before-import-", StringComparison.Ordinal)));
        // Another file's copies are its own to tidy.
        Assert.Contains("notebook.json.broken-20261001-120000", left);
    }

    [Fact]
    public void ATemporaryFileACrashLeftGoesButAWriteInProgressStays()
    {
        var file = Touch("mail-cache.json", TimeSpan.Zero);
        Touch("mail-cache.json.1234-abcd1234.tmp", TimeSpan.FromDays(3));
        Touch("mail-cache.json.5678-efef5678.tmp", TimeSpan.FromSeconds(2));

        DurableFile.Tidy(file);

        Assert.Equal(["mail-cache.json", "mail-cache.json.5678-efef5678.tmp"], Left());
    }

    [Fact]
    public void ALogoNobodyDrewForThreeMonthsGoesAndOneInUseStays()
    {
        Touch("drawn.img", TimeSpan.FromDays(20));
        Touch("refetched-monthly.img", TimeSpan.FromDays(31));
        Touch("forgotten.img", TimeSpan.FromDays(120));
        Touch("cut-short.img.tmp", TimeSpan.FromDays(2));
        var logos = new SportsLogos((_, _) => Task.FromResult<byte[]?>(null), folder);

        logos.Sweep(folder);

        Assert.Equal(["drawn.img", "refetched-monthly.img"], Left());
    }
}
