// camera: the public cameras the field offers, asked for for real (docs\widgets.md §331). Off the network in
// every run but one asked for: KARPO_CAMERA_LIVE=1 reads each known camera — and KARPO_CAMERA_URLS, any
// addresses separated by spaces (a local HLS, DASH, MP4 or RTSP stream to try Windows' media stack on) —
// through the line's own stream and waits for its first picture. What each did is written to the test's
// output; one that shows nothing in 40 s fails.

using Xunit.Abstractions;

namespace Karpo.Tests.CameraLines;

public sealed class CameraLiveTests(ITestOutputHelper output)
{
    [Fact]
    public void EveryKnownCameraShowsAPicture()
    {
        if (Environment.GetEnvironmentVariable("KARPO_CAMERA_LIVE") != "1")
        {
            return;
        }
        var urls = KnownCameras.All.Select(known => known.Url)
            .Concat((Environment.GetEnvironmentVariable("KARPO_CAMERA_URLS") ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .ToList();
        var failed = new List<string>();
        Parallel.ForEach(urls, url =>
        {
            using var stream = new Karpo.Dock.CameraStream(() => { });
            stream.Start(new CameraOptions { Url = url });
            var started = Environment.TickCount64;
            while (stream.Shot.Frame is null && Environment.TickCount64 - started < 40_000)
            {
                Thread.Sleep(200);
            }
            var shot = stream.Shot;
            var said = $"{url}: {shot.State} {shot.Protocol} {shot.Frame?.Width}x{shot.Frame?.Height} {shot.Problem} in {(Environment.TickCount64 - started) / 1000.0:0.0} s";
            lock (failed)
            {
                output.WriteLine(said);
                if (shot.Frame is null)
                {
                    failed.Add(said);
                }
            }
        });
        Assert.True(failed.Count == 0, string.Join("\n", failed));
    }
}
