// sports §369: the logos — fetched once from ESPN's picture server alone and kept, in memory and on disk,
// so a second draw and a restart ask nothing; a dark ground's drawing where ESPN has one, else the usual;
// one that could not be had left alone for a while, not asked on every draw; whoever asked told once
// it is in, and nobody blocked meanwhile.

using Karpo.Dock;
using Karpo.Dock.Sports;

namespace Karpo.Tests.Sports;

public sealed class SportsLogosTests : IDisposable
{
    private const string Barcelona = "https://a.espncdn.com/i/teamlogos/soccer/500/83.png";

    private readonly string folder = Path.Combine(Path.GetTempPath(), "karpo-tests", "sports-logos-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private sealed class Fake
    {
        public readonly List<string> Asked = [];
        public Func<string, byte[]?> Answer = _ => [1, 2, 3];
        public DateTime Now = new(2026, 10, 26, 12, 0, 0, DateTimeKind.Utc);

        public SportsLogos Logos(string? folder) => new(
            (address, _) =>
            {
                lock (Asked)
                {
                    Asked.Add(address);
                }
                return Task.FromResult(Answer(address));
            },
            folder,
            bytes => new Pixels { Data = bytes, Width = 1, Height = 1 },
            () => Now);
    }

    /// <summary>The logo once it is in: asked, waited for through its arrival.</summary>
    private static Pixels? Came(SportsLogos logos, string address, bool dark = false)
    {
        using var came = new ManualResetEventSlim();
        var first = logos.Get(address, dark, came.Set);
        if (first is not null)
        {
            return first;
        }
        came.Wait(TimeSpan.FromSeconds(5));
        return logos.Get(address, dark, null);
    }

    [Fact]
    public void ALogoIsFetchedOnceAndKept()
    {
        var fake = new Fake();
        var logos = fake.Logos(null);

        Assert.NotNull(Came(logos, Barcelona));
        Assert.NotNull(logos.Get(Barcelona, false, null));
        Assert.Equal(1, logos.Fetched);
        // Small, through ESPN's resizer.
        Assert.Equal(["https://a.espncdn.com/combiner/i?img=/i/teamlogos/soccer/500/83.png&w=64&h=64"], fake.Asked);
    }

    [Fact]
    public void ARestartReadsItFromDiskAndAMonthLaterItIsFetchedAgain()
    {
        var fake = new Fake();
        Assert.NotNull(Came(fake.Logos(folder), Barcelona));
        Assert.Single(Directory.GetFiles(folder));

        var again = new Fake { Answer = _ => null };
        Assert.NotNull(Came(again.Logos(folder), Barcelona));
        Assert.Empty(again.Asked);

        // A month on, it is asked again; offline, the old one still stands.
        var later = new Fake { Answer = _ => null, Now = DateTime.UtcNow.AddDays(31) };
        Assert.NotNull(Came(later.Logos(folder), Barcelona));
        Assert.Single(later.Asked);
    }

    [Fact]
    public void ADarkGroundsDrawingFallsBackToTheUsualOne()
    {
        var fake = new Fake { Answer = address => address.Contains("500-dark", StringComparison.Ordinal) ? null : [9] };
        var logos = fake.Logos(null);

        Assert.NotNull(Came(logos, Barcelona, dark: true));
        Assert.Equal(2, fake.Asked.Count);
        Assert.Contains("/500-dark/", fake.Asked[0], StringComparison.Ordinal);
        Assert.DoesNotContain("/500-dark/", fake.Asked[1], StringComparison.Ordinal);
    }

    [Fact]
    public void OneThatCouldNotBeHadIsLeftAloneAWhileAndAnotherHostIsNeverAsked()
    {
        var fake = new Fake { Answer = _ => null };
        var logos = fake.Logos(null);

        Assert.Null(Came(logos, Barcelona));
        Assert.Null(logos.Get(Barcelona, false, null));
        Assert.Equal(1, logos.Fetched);
        fake.Now += SportsLogos.Retry + TimeSpan.FromSeconds(1);
        _ = logos.Get(Barcelona, false, null);
        Assert.Equal(2, logos.Fetched);

        Assert.Null(logos.Get("https://example.com/83.png", false, null));
        Assert.Null(logos.Get("", false, null));
        Assert.Equal(2, logos.Fetched);
    }

    [Fact]
    public void EveryoneWhoAskedIsToldOnceItIsIn()
    {
        var gate = new TaskCompletionSource<byte[]?>();
        var told = 0;
        var logos = new SportsLogos((_, _) => gate.Task, null, bytes => new Pixels { Data = bytes, Width = 1, Height = 1 });

        Assert.Null(logos.Get(Barcelona, false, () => Interlocked.Increment(ref told)));
        Assert.Null(logos.Get(Barcelona, false, () => Interlocked.Increment(ref told)));
        Assert.Equal(1, logos.Fetched);
        gate.SetResult([1]);
        Assert.True(Patience.Until(() => Volatile.Read(ref told) == 2));
        Assert.NotNull(logos.Get(Barcelona, false, null));
    }

    [Fact]
    public void ItsFileIsNamedByAHashSoNoAddressNamesAPath()
    {
        var name = SportsLogos.FileOf("https://a.espncdn.com/../../evil");
        Assert.Matches("^[0-9a-f]{24}\\.img$", name);
        Assert.NotEqual(name, SportsLogos.FileOf("dark|https://a.espncdn.com/../../evil"));
    }
}
