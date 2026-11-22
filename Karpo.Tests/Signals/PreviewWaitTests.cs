// The window list never waits on DWM (Karpo.Dock\PreviewThumbnails.cs). The regression: every
// window's DwmRegisterThumbnail ran on the dock's thread inside Show, and DWM, which answers on its
// next frame, took 18 seconds for one window in a published smoke — the list and the bar waited.
//
// Here DWM is a stand-in that answers only when the test lets it: Show must return while it has not,
// the previews must be placed once it has, and a list let go must leave nothing registered, even a
// preview DWM answered for after it went.

using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
using Color = Windows.UI.Color;

namespace Karpo.Tests.Signals;

public sealed unsafe class PreviewWaitTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    [Fact]
    public void ABatchReturnsAtOnceAndPlacesEachPreviewWhenDwmAnswers()
    {
        var dwm = new SlowDwm();
        var placed = new List<SIZE>();

        var batch = new ThumbnailBatch(dwm, (HWND)0x10, [Request(0x20, placed), Request(0x30, placed)]);

        // DWM has not answered, and nothing waited for it.
        Assert.False(batch.Done.IsCompleted);
        dwm.Answer();
        Assert.True(batch.Done.Wait(Patience));
        Assert.Equal([(nint)0x20, 0x30], dwm.Registered.Select(pair => pair.Source));
        Assert.Equal(2, dwm.Updated.Count);
        // Each placed for its own source's size.
        Assert.Equal([0x20, 0x30], placed.Select(size => size.cx));
    }

    [Fact]
    public void LettingGoUnregistersEveryPreview()
    {
        var dwm = new SlowDwm();
        dwm.Answer();
        var batch = new ThumbnailBatch(dwm, (HWND)0x10, [Request(0x20, []), Request(0x30, [])]);
        Assert.True(batch.Done.Wait(Patience));

        batch.Release();

        Assert.True(SpinWait.SpinUntil(() => dwm.Unregistered.Count == 2, Patience));
        Assert.Equal(dwm.Registered.Select(pair => pair.Thumbnail).Order(), dwm.Unregistered.Order());
    }

    [Fact]
    public void APreviewDwmAnswersForAfterTheListWentIsUnregisteredAndNeverPlaced()
    {
        var dwm = new SlowDwm();
        var batch = new ThumbnailBatch(dwm, (HWND)0x10, [Request(0x20, []), Request(0x30, [])]);
        Assert.True(SpinWait.SpinUntil(() => dwm.Asked > 0, Patience), "DWM was never asked");

        batch.Release();
        dwm.Answer();

        Assert.True(batch.Done.Wait(Patience));
        // The one DWM was answering: unregistered, never placed; the second never asked for.
        Assert.Single(dwm.Registered);
        Assert.Equal([dwm.Registered[0].Thumbnail], dwm.Unregistered);
        Assert.Empty(dwm.Updated);
    }

    [Fact]
    public void APreviewDwmRefusesOrThatFailsCostsOnlyItself()
    {
        var dwm = new SlowDwm { Refuse = 0x20, Throw = 0x30 };
        dwm.Answer();

        var batch = new ThumbnailBatch(dwm, (HWND)0x10, [Request(0x20, []), Request(0x30, []), Request(0x40, [])]);

        Assert.True(batch.Done.Wait(Patience));
        Assert.Equal([(nint)0x40], dwm.Registered.Select(pair => pair.Source));
        Assert.Single(dwm.Updated);
    }

    [UiFact]
    public void TheWindowListOpensWithoutWaitingOnDwm()
    {
        PreviewToolbarTests.OnAHiddenDesktop(gfx =>
        {
            var instance = (HINSTANCE)(void*)GetModuleHandleW(null);
            PreviewToolbarTests.Register(Preview.ClassName, instance);
            PreviewToolbarTests.Register("karpo.tests.preview.app", instance);
            var windows = Enumerable.Range(0, 12).Select(_ => PreviewToolbarTests.Window(instance)).ToList();
            var dwm = new SlowDwm();
            using var preview = new Preview(gfx, instance, 0, dwm);
            try
            {
                var started = DateTime.UtcNow;
                preview.Show(
                    gfx,
                    new Look(Glass: false, Dark: true, Accent: Color.FromArgb(255, 0, 0x78, 0xD4), Animations: false),
                    1f,
                    0,
                    [.. windows.Select((hwnd, index) => (hwnd, $"Window {index}"))],
                    new POINT { x = 400, y = 1000 },
                    new RECT { right = 1920, bottom = 1040 });
                var took = DateTime.UtcNow - started;

                // Open, with DWM not having answered once, and in well under the smoke's second.
                Assert.True(preview.IsOpen);
                Assert.False(preview.Previews.IsCompleted);
                Assert.True(took < TimeSpan.FromMilliseconds(Smoke.SlowestOpeningMs), $"the list took {took.TotalMilliseconds:F0} ms to open");

                // Let go before DWM answered: nothing it answers afterwards stays registered.
                preview.Hide();
                dwm.Answer();
                Assert.True(SpinWait.SpinUntil(() => preview.Previews.IsCompleted, Patience));
                Assert.True(SpinWait.SpinUntil(() => dwm.Unregistered.Count == dwm.Registered.Count, Patience));
                Assert.Empty(dwm.Updated);
            }
            finally
            {
                dwm.Answer();
                foreach (var window in windows)
                {
                    _ = DestroyWindow(window);
                }
            }
        });
    }

    private static ThumbnailBatch.Request Request(nint source, List<SIZE> placed) =>
        new((HWND)source, size =>
        {
            lock (placed)
            {
                placed.Add(size);
            }
            return new DWM_THUMBNAIL_PROPERTIES { dwFlags = DWM.DWM_TNP_VISIBLE, fVisible = BOOL.TRUE };
        });

    /// <summary>DWM as a busy compositor is: registering waits until <see cref="Answer"/>.</summary>
    private sealed class SlowDwm : IDwmThumbnails
    {
        private readonly ManualResetEventSlim answered = new(false);
        private readonly Lock gate = new();
        private int asked;
        private nint next = 0x1000;

        public List<(nint Source, nint Thumbnail)> Registered { get; } = [];

        public List<nint> Updated { get; } = [];

        public List<nint> Unregistered { get; } = [];

        public nint Refuse { get; init; }

        public nint Throw { get; init; }

        public int Asked => Volatile.Read(ref asked);

        public void Answer() => answered.Set();

        public nint Register(HWND destination, HWND source)
        {
            Interlocked.Increment(ref asked);
            answered.Wait();
            if ((nint)source == Refuse)
            {
                return 0;
            }
            if ((nint)source == Throw)
            {
                throw new InvalidOperationException("DWM fell over");
            }
            lock (gate)
            {
                var made = next++;
                Registered.Add(((nint)source, made));
                return made;
            }
        }

        public SIZE SourceSize(nint thumbnail)
        {
            lock (gate)
            {
                var source = Registered.First(pair => pair.Thumbnail == thumbnail).Source;
                return new SIZE { cx = (int)source, cy = 100 };
            }
        }

        public void Update(nint thumbnail, DWM_THUMBNAIL_PROPERTIES properties)
        {
            lock (gate)
            {
                Updated.Add(thumbnail);
            }
        }

        public void Unregister(nint thumbnail)
        {
            lock (gate)
            {
                Unregistered.Add(thumbnail);
            }
        }
    }
}
