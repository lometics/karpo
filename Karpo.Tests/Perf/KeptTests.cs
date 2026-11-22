// The rule behind the dock's icon caches (Kept): what a rebuild stops using is
// closed one rebuild later, never while a scene may still show it, and never
// left for the GC.

using Karpo.Dock;

namespace Karpo.Tests.Perf;

public sealed class KeptTests
{
    private sealed class Surface(string name) : IDisposable
    {
        public string Name { get; } = name;

        public int Closed { get; private set; }

        public void Dispose() => Closed++;
    }

    [Fact]
    public void WhatARebuildStillUsesStaysOpen()
    {
        var kept = new Kept<string, Surface>();
        var icon = new Surface("explorer");
        kept.Add("explorer", icon);
        for (var rebuild = 0; rebuild < 5; rebuild++)
        {
            Assert.True(kept.TryGet("explorer", out var found));
            Assert.Same(icon, found);
            kept.Sweep();
        }
        Assert.Equal(0, icon.Closed);
        Assert.Equal(1, kept.Count);
    }

    [Fact]
    public void WhatGoesUnusedIsClosedOneRebuildLater()
    {
        var kept = new Kept<string, Surface>();
        var gone = new Surface("gone");
        kept.Add("gone", gone);
        // The rebuild that used it.
        Assert.Equal(0, kept.Sweep());
        // The first rebuild without it only sets it aside: a scene from before may show it.
        Assert.Equal(0, kept.Sweep());
        Assert.Equal(0, gone.Closed);
        Assert.Equal(1, kept.Count);
        // The next one closes it, once.
        Assert.Equal(1, kept.Sweep());
        Assert.Equal(1, gone.Closed);
        Assert.Equal(0, kept.Count);
        kept.Sweep();
        Assert.Equal(1, gone.Closed);
    }

    [Fact]
    public void ASetAsideValueComesBackWhenAskedFor()
    {
        var kept = new Kept<string, Surface>();
        var icon = new Surface("spotify");
        kept.Add("spotify", icon);
        kept.Sweep();
        kept.Sweep();
        // Set aside now; the app opens again before the next sweep.
        Assert.True(kept.TryGet("spotify", out var back));
        Assert.Same(icon, back);
        kept.Sweep();
        kept.Sweep();
        Assert.Equal(0, icon.Closed);
        Assert.False(kept.TryGet("missing", out _));
    }

    [Fact]
    public void ANewValueForAKeyClosesTheOldOne()
    {
        var kept = new Kept<string, Surface>();
        var first = new Surface("first");
        var second = new Surface("second");
        kept.Add("key", first);
        kept.Add("key", second);
        Assert.Equal(1, first.Closed);
        Assert.Equal(0, second.Closed);
        Assert.True(kept.TryGet("key", out var found));
        Assert.Same(second, found);
        // Adding the same value again closes nothing.
        kept.Add("key", second);
        Assert.Equal(0, second.Closed);
    }

    [Fact]
    public void ClearClosesEverythingOnce()
    {
        var kept = new Kept<string, Surface>();
        var live = new Surface("live");
        var aside = new Surface("aside");
        kept.Add("aside", aside);
        kept.Sweep();
        kept.Add("live", live);
        kept.Sweep();
        kept.Clear();
        Assert.Equal(1, live.Closed);
        Assert.Equal(1, aside.Closed);
        Assert.Equal(0, kept.Count);
    }

    [Fact]
    public void AnAppThatComesAndGoesKeepsTheCacheBounded()
    {
        // Windows opening and closing all day: at most two rebuilds' worth is ever kept.
        var kept = new Kept<int, Surface>();
        var made = new List<Surface>();
        for (var rebuild = 0; rebuild < 1000; rebuild++)
        {
            for (var app = rebuild; app < rebuild + 3; app++)
            {
                if (!kept.TryGet(app, out _))
                {
                    var surface = new Surface(app.ToString());
                    made.Add(surface);
                    kept.Add(app, surface);
                }
            }
            kept.Sweep();
            Assert.True(kept.Count <= 6, $"{kept.Count} kept after rebuild {rebuild}");
        }
        Assert.True(made.Count(surface => surface.Closed == 1) >= made.Count - 6);
        Assert.All(made, surface => Assert.True(surface.Closed <= 1));
    }
}
