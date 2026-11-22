// camera: a camera line's stream read for real from a camera of the test's own on the loopback (docs\widgets.md
// §331; FakeCamera): an MJPEG stream's pictures come and move; a snapshot is asked for again at its pace, and one
// that did not change is not sent again; what goes wrong is said and tried again; and nothing is read while the
// line does not show — opened, it connects; closed, the request is let go.

using Karpo.Dock;

namespace Karpo.Tests.CameraLines;

public sealed class CameraStreamTests
{
    /// <summary>Waits up to 10 s for <paramref name="done"/>.</summary>
    private static void Until(Func<bool> done, string what)
    {
        var started = Environment.TickCount64;
        while (!done())
        {
            Assert.True(Environment.TickCount64 - started < 10_000, $"never: {what}");
            Thread.Sleep(20);
        }
    }

    [Fact]
    public void AnMjpegStreamsPicturesComeAndMove()
    {
        using var camera = new FakeCamera(_ => new FakeCamera.Answer(200, "", [], Parts: [FakeCamera.Picture(320, 180), FakeCamera.Picture(320, 180, 60)]));
        var told = 0;
        using var stream = new CameraStream(() => Interlocked.Increment(ref told)) { Most = 160 };
        // Its address says nothing: what it answers does.
        stream.Start(new CameraOptions { Url = camera.Url("/live") });
        Until(() => stream.Shot.Number >= 5, "five pictures");
        var shot = stream.Shot;
        Assert.Equal(CameraState.Live, shot.State);
        Assert.Equal(CameraProtocol.Mjpeg, shot.Protocol);
        Assert.True(shot.Moving);
        // Decoded no larger than the line draws it, its shape kept.
        Assert.Equal((160, 90), (shot.Frame!.Width, shot.Frame.Height));
        // Said to the line as it connected and as its first picture came; the rest are only taken.
        Assert.True(Volatile.Read(ref told) <= 3, $"told {told} times");
        stream.Stop();
        Assert.Equal(CameraState.Idle, stream.Shot.State);
        Until(() => camera.Open == 0, "the stream let go");
        // The last picture stays to show at once next time.
        Assert.NotNull(stream.Shot.Frame);
    }

    [Fact]
    public void ASnapshotIsAskedForAgainAtItsPaceAndOnlySentWhereItChanged()
    {
        var version = 1;
        using var camera = new FakeCamera(request =>
            request.IfNoneMatch == $"\"v{Volatile.Read(ref version)}\""
                ? new FakeCamera.Answer(304, "image/bmp", [])
                : new FakeCamera.Answer(200, "image/bmp", FakeCamera.Picture(64, 48, (byte)(Volatile.Read(ref version) * 20)), ETag: $"\"v{Volatile.Read(ref version)}\""));
        var told = 0;
        using var stream = new CameraStream(() => Interlocked.Increment(ref told)) { PaceMs = 50 };
        stream.Start(new CameraOptions { Url = camera.Url("/cam/current.bmp") });
        Until(() => stream.Shot.Number == 1, "the first picture");
        Assert.Equal(CameraProtocol.Snapshot, stream.Shot.Protocol);
        Assert.False(stream.Shot.Moving);
        // Asked again and again, and no new picture while it did not change.
        Until(() => camera.Asked >= 4, "asked again");
        Assert.Equal(1, stream.Shot.Number);
        Volatile.Write(ref version, 2);
        Until(() => stream.Shot.Number == 2, "the changed picture");
        // Each picture is said to the line: a snapshot is drawn by the dock's wake, not the folder's quick tick.
        Assert.True(Volatile.Read(ref told) >= 2);
    }

    [Fact]
    public void WhatGoesWrongIsSaidAndTriedAgain()
    {
        var found = false;
        using var camera = new FakeCamera(request => request.Path switch
        {
            "/page" => new FakeCamera.Answer(200, "text/html", "<html><body>A camera's page</body></html>"u8.ToArray()),
            _ when !Volatile.Read(ref found) => new FakeCamera.Answer(404, "text/plain", "gone"u8.ToArray()),
            _ => new FakeCamera.Answer(200, "image/bmp", FakeCamera.Picture(32, 32)),
        });
        using var missing = new CameraStream(() => { }) { Wait = _ => 30 };
        missing.Start(new CameraOptions { Url = camera.Url("/cam.jpg") });
        Until(() => missing.Shot.State == CameraState.Failed, "failed");
        Assert.Equal(S.DockCameraRefused(404), missing.Shot.Problem);
        // Tried again: the camera back, its picture comes.
        Volatile.Write(ref found, true);
        Until(() => missing.Shot.Frame is not null, "the picture after trying again");
        Assert.Equal(CameraState.Live, missing.Shot.State);
        Assert.Null(missing.Shot.Problem);

        using var page = new CameraStream(() => { }) { Wait = _ => 10_000 };
        page.Start(new CameraOptions { Url = camera.Url("/page") });
        Until(() => page.Shot.State == CameraState.Failed, "a page");
        Assert.Equal(S.DockCameraPage, page.Shot.Problem);

        using var nobody = new CameraStream(() => { }) { Wait = _ => 10_000 };
        nobody.Start(new CameraOptions { Url = "http://127.0.0.1:9/cam.jpg" });
        Until(() => nobody.Shot.State == CameraState.Failed, "nobody there");
        Assert.Equal(S.DockCameraUnreachable, nobody.Shot.Problem);
    }

    [Fact]
    public void ALineReadsItsCameraOnlyWhileItShows()
    {
        using var camera = new FakeCamera(_ => new FakeCamera.Answer(200, "", [], Parts: [FakeCamera.Picture(64, 36)]));
        var item = ListKind.Camera.New();
        item.Id = "camera";
        item.Camera = new CameraOptions { Url = camera.Url("/video.mjpg") };
        using var line = new CameraLine("layout:default/camera", item, null!, new Config());
        // Made and configured, but not shown: nothing asked.
        Thread.Sleep(200);
        Assert.Equal(0, camera.Asked);
        Assert.False(line.Stream.Running);
        Assert.False(line.Streaming);
        line.Visible(true);
        Until(() => line.Shot.Frame is not null, "its picture");
        Assert.True(line.Streaming);
        Assert.True(line.Fresh);
        line.Visible(false);
        Assert.False(line.Stream.Running);
        Assert.False(line.Streaming);
        Until(() => camera.Open == 0, "the request let go");
        var asked = camera.Asked;
        Thread.Sleep(200);
        Assert.Equal(asked, camera.Asked);
        // Shown again: connects again, the last picture meanwhile.
        line.Visible(true);
        Assert.NotNull(line.Shot.Frame);
        Until(() => camera.Asked > asked, "asked again");
        // Another camera set while it shows: read from now.
        using var other = new FakeCamera(_ => new FakeCamera.Answer(200, "image/bmp", FakeCamera.Picture(40, 30)));
        item.Camera = new CameraOptions { Url = other.Url("/still.bmp") };
        line.Configure(item, new Config());
        Until(() => line.Shot.Protocol == CameraProtocol.Snapshot, "the other camera");
        Assert.Equal(40, line.Shot.Frame!.Width);
        line.Visible(false);
    }
}
