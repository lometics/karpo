// camera: the rules of a camera line (docs\widgets.md §331) — which way a stream is read, from its address
// and from what it answers; an address Karpo asks for; a page's own stream; the line's settings as the file
// keeps them; its menu's rows and what its field offers.

using System.Text;
using System.Text.Json;

namespace Karpo.Tests.CameraLines;

public sealed class CameraRulesTests
{
    [Theory]
    [InlineData("rtsp://cam.local/stream1", CameraProtocol.Rtsp)]
    [InlineData("rtspt://192.168.1.20:554/h264", CameraProtocol.Rtsp)]
    [InlineData("https://example.com/live/master.m3u8?token=1", CameraProtocol.Hls)]
    [InlineData("https://example.com/live/Manifest.mpd", CameraProtocol.Dash)]
    [InlineData("https://example.com/clip.MP4", CameraProtocol.Video)]
    [InlineData("https://example.com/cam/current/816.jpg", CameraProtocol.Snapshot)]
    [InlineData("http://example.com/still.png", CameraProtocol.Snapshot)]
    [InlineData("http://cam.local/video.mjpg", CameraProtocol.Mjpeg)]
    [InlineData("http://cam.local/axis-cgi/mjpg/video.cgi", CameraProtocol.Mjpeg)]
    [InlineData("http://cam.local/nphMotionJpeg?Resolution=640x480", CameraProtocol.Mjpeg)]
    [InlineData("http://cam.local/videostream.cgi?user=a", CameraProtocol.Mjpeg)]
    [InlineData("http://cam.local:8080/?action=stream", CameraProtocol.Mjpeg)]
    [InlineData("http://cam.local:8080/?action=snapshot", CameraProtocol.Snapshot)]
    [InlineData("http://cam.local/axis-cgi/jpg/image.cgi", CameraProtocol.Snapshot)]
    [InlineData("http://cam.local/cgi-bin/snapshot.cgi", CameraProtocol.Snapshot)]
    [InlineData("https://www.youtube.com/watch?v=abc", null)]
    [InlineData("https://example.com/camera", null)]
    [InlineData("example.com/cam.jpg", CameraProtocol.Snapshot)]
    [InlineData("ftp://example.com/cam.jpg", null)]
    [InlineData("", null)]
    public void TheProtocolIsGuessedFromTheAddress(string address, CameraProtocol? protocol) =>
        Assert.Equal(protocol, Cameras.Guess(address));

    [Fact]
    public void TheAnswerSaysWhatItIs()
    {
        CameraAnswer Of(string? type, string head) => Cameras.FromAnswer(type, Encoding.ASCII.GetBytes(head));
        Assert.Equal(CameraProtocol.Mjpeg, Of("multipart/x-mixed-replace; boundary=--myboundary", "").Protocol);
        Assert.Equal(CameraProtocol.Snapshot, Of("image/jpeg", "").Protocol);
        Assert.Equal(CameraProtocol.Hls, Of("application/vnd.apple.mpegurl", "").Protocol);
        Assert.Equal(CameraProtocol.Hls, Of("audio/mpegurl", "").Protocol);
        Assert.Equal(CameraProtocol.Dash, Of("application/dash+xml", "").Protocol);
        Assert.Equal(CameraProtocol.Video, Of("video/mp4", "").Protocol);
        // No content type, or one that says nothing: its first bytes.
        Assert.Equal(CameraProtocol.Snapshot, Cameras.FromAnswer(null, [0xFF, 0xD8, 0xFF, 0xE0]).Protocol);
        Assert.Equal(CameraProtocol.Snapshot, Cameras.FromAnswer("application/octet-stream", [0x89, (byte)'P', (byte)'N', (byte)'G']).Protocol);
        Assert.Equal(CameraProtocol.Hls, Of("text/plain", "#EXTM3U\n#EXT-X-VERSION:3").Protocol);
        Assert.Equal(CameraProtocol.Dash, Of("text/xml", "<?xml version=\"1.0\"?><MPD xmlns=\"urn:mpeg:dash\">").Protocol);
        Assert.Equal(CameraProtocol.Mjpeg, Of(null, "--myboundary\r\nContent-Type: image/jpeg\r\n").Protocol);
        Assert.Equal(CameraProtocol.Video, Cameras.FromAnswer("application/octet-stream", [0, 0, 0, 0x18, (byte)'f', (byte)'t', (byte)'y', (byte)'p']).Protocol);
        // A page, and what is nothing Karpo reads.
        Assert.Equal(new CameraAnswer(null, true), Of("text/html; charset=utf-8", "<!doctype html><html>"));
        Assert.Equal(new CameraAnswer(null, true), Of(null, "<html><body>"));
        Assert.Equal(new CameraAnswer(null, false), Of("application/json", "{\"error\":1}"));
    }

    [Theory]
    [InlineData("multipart/x-mixed-replace; boundary=--myboundary", "myboundary")]
    [InlineData("multipart/x-mixed-replace;boundary=ffmpeg", "ffmpeg")]
    [InlineData("multipart/x-mixed-replace; boundary=\"frame\"", "frame")]
    [InlineData("multipart/x-mixed-replace", null)]
    [InlineData(null, null)]
    public void TheBoundaryIsReadLoosely(string? type, string? boundary) => Assert.Equal(boundary, Cameras.Boundary(type));

    [Fact]
    public void AnAddressIsOneKarpoAsksForAndAClickOpensNoPassword()
    {
        Assert.Equal("https://example.com/cam.jpg", Cameras.Address("  example.com/cam.jpg "));
        Assert.Equal("rtsp://user:pw@10.0.0.2/stream", Cameras.Address("rtsp://user:pw@10.0.0.2/stream"));
        Assert.Null(Cameras.Address("file:///C:/cam.jpg"));
        Assert.Null(Cameras.Address("has a space"));
        Assert.Null(Cameras.Address("javascript:alert(1)"));
        // A click: a known camera's page; else its own address, without the camera's user name and password; no RTSP.
        Assert.Equal("https://www.foto-webcam.eu/webcam/pizol/", Cameras.PageOf(new CameraOptions { Url = "https://www.foto-webcam.eu/webcam/pizol/current/816.jpg" }));
        Assert.Equal("http://cam.local/video.mjpg", Cameras.PageOf(new CameraOptions { Url = "http://admin:secret@cam.local/video.mjpg" }));
        Assert.Null(Cameras.PageOf(new CameraOptions { Url = "rtsp://cam.local/live" }));
        // Its name: the one given, a known camera's, else the host, never the password.
        Assert.Equal("Pizol", Cameras.NameOf(new CameraOptions { Url = "https://www.foto-webcam.eu/webcam/pizol/current/816.jpg" }));
        Assert.Equal("cam.local", Cameras.NameOf(new CameraOptions { Url = "http://admin:secret@cam.local/video.mjpg" }));
        Assert.Equal("Garden", Cameras.NameOf(new CameraOptions { Url = "http://cam.local/video.mjpg", Name = "Garden" }));
    }

    [Fact]
    public void APageNamesItsOwnStream()
    {
        const string page = "https://example.com/cams/lake";
        Assert.Equal("https://cdn.example.com/lake/index.m3u8", Cameras.StreamIn("<meta property=\"og:video\" content=\"https://cdn.example.com/lake/index.m3u8\">", page));
        Assert.Equal("https://example.com/cams/live.mp4", Cameras.StreamIn("<meta content='live.mp4' property='og:video:url'/>", page));
        // A video page's og:video that is another page is none; a playlist written in its script's JSON is.
        Assert.Null(Cameras.StreamIn("<meta property=\"og:video\" content=\"https://www.youtube.com/embed/abc\">", page));
        Assert.Equal("https://m.example.com/a/index.m3u8?x=1&y=2",
            Cameras.StreamIn("<script>var p={\"hls\":\"https:\\/\\/m.example.com\\/a\\/index.m3u8?x=1\\u0026y=2\"};</script>", page));
        Assert.Null(Cameras.StreamIn("<html><body>No stream here</body></html>", page));
    }

    [Fact]
    public void TheFileKeepsOnlyWhatDiffersAndALineReadsBackWithItsNeighbours()
    {
        var line = ListKind.Camera.New();
        line.Camera = new CameraOptions { Url = " example.com/cam.jpg ", Protocol = CameraProtocol.Snapshot, Seconds = 7, Fit = CameraFit.Fill, Mute = true, Name = "  " };
        line.Normalize();
        var camera = line.Camera!;
        Assert.Equal("https://example.com/cam.jpg", camera.Url);
        Assert.Equal(CameraProtocol.Snapshot, camera.Protocol);
        Assert.Equal(5, Cameras.PaceOf(camera));
        Assert.Equal(5, camera.Seconds);
        Assert.Null(camera.Fit);
        Assert.Null(camera.Mute);
        Assert.Null(camera.Name);
        // A pace of its default, a fit of fill, muted: nothing written for them.
        var json = JsonSerializer.Serialize(line, ConfigJson.Default.CellItem);
        using (var document = JsonDocument.Parse(json))
        {
            var written = document.RootElement.GetProperty("camera");
            Assert.Equal(["url", "protocol", "seconds"], written.EnumerateObject().Select(property => property.Name));
            Assert.Equal("snapshot", written.GetProperty("protocol").GetString());
        }
        // A word a newer build wrote reads as automatic, and a camera line between two others reads back with them.
        var read = JsonSerializer.Deserialize("{\"kind\":\"camera\",\"id\":\"c1\",\"camera\":{\"url\":\"https://example.com/a.mjpg\",\"protocol\":\"webrtc\",\"fit\":\"whole\",\"mute\":false}}", ConfigJson.Default.CellItem)!;
        read.Normalize();
        Assert.Same(WidgetInfos.Camera, read.Info);
        Assert.Null(read.Camera!.Protocol);
        Assert.Equal((CameraFit?)CameraFit.Whole, read.Camera.Fit);
        Assert.False(read.Camera.Mute);
        // Another kind keeps no camera record.
        var clock = ListKind.Clock.New();
        clock.Camera = new CameraOptions { Url = "https://example.com/a.jpg" };
        clock.Normalize();
        Assert.Null(clock.Camera);
    }

    [Fact]
    public void ANewLineAsksForItsCamera()
    {
        Assert.Equal(SetupField.Stream, WidgetSetup.Needs(ListKind.Camera.New()));
        var line = ListKind.Camera.New();
        line.Camera = new CameraOptions { Url = "https://example.com/a.jpg" };
        Assert.Null(WidgetSetup.Needs(line));
    }

    // --- Its menu ----------------------------------------------------------------------------------

    private static IReadOnlyList<OptionRow> Rows(string url, CameraProtocol? protocol = null, Config? config = null)
    {
        var line = ListKind.Camera.New();
        line.Camera = new CameraOptions { Url = url, Protocol = protocol };
        line.Normalize();
        return OptionRows.Of(line, new OptionDevices { Words = WordsInUse.Of(config ?? new Config()) });
    }

    [Fact]
    public void ItsRowsAreItsCameraThenHowItIsShownThenWhatItDoes()
    {
        string[] Keys(IReadOnlyList<OptionRow> rows) => [.. rows.Where(row => row.Key.StartsWith("Camera", StringComparison.Ordinal)).Select(row => row.Key)];
        // A snapshot: its pace, and no sound.
        Assert.Equal([OptionRows.CameraUrl, OptionRows.CameraProtocol, OptionRows.CameraFit, OptionRows.CameraName, OptionRows.CameraPace], Keys(Rows("https://example.com/a.jpg")));
        // A stream Windows plays: its sound, and no pace; MJPEG neither.
        Assert.Equal([OptionRows.CameraUrl, OptionRows.CameraProtocol, OptionRows.CameraFit, OptionRows.CameraName, OptionRows.CameraMute], Keys(Rows("https://example.com/a.m3u8")));
        Assert.Equal([OptionRows.CameraUrl, OptionRows.CameraProtocol, OptionRows.CameraFit, OptionRows.CameraName], Keys(Rows("http://cam.local/video.mjpg")));
        // Nothing known of it yet: both. A protocol chosen wins over the address.
        Assert.Contains(OptionRows.CameraPace, Keys(Rows("https://example.com/camera")));
        Assert.Contains(OptionRows.CameraMute, Keys(Rows("https://example.com/camera")));
        Assert.Contains(OptionRows.CameraMute, Keys(Rows("https://example.com/a.jpg", CameraProtocol.Video)));
        var rows = Rows("https://example.com/a.jpg");
        Assert.Equal(OptionSlot.Subject, rows.Single(row => row.Key == OptionRows.CameraUrl).Slot);
        Assert.Equal(S.SettingsOnlineUpdateHeader, rows.Single(row => row.Key == OptionRows.CameraPace).Title);
        Assert.Equal(S.CoreCameraProtocolAuto, rows.Single(row => row.Key == OptionRows.CameraProtocol).Choices[0]);
    }

    [Fact]
    public void ARowSetsWhatItSays()
    {
        var line = ListKind.Camera.New();
        line.Normalize();
        Assert.Null(line.Camera!.Protocol);
        OptionRow Row(string key) => OptionRows.Of(line, new OptionDevices()).Single(row => row.Key == key);
        Assert.True(Row(OptionRows.CameraUrl).Pick!(new OptionFound("Pizol", "", "https://www.foto-webcam.eu/webcam/pizol/current/816.jpg")));
        Assert.Equal("https://www.foto-webcam.eu/webcam/pizol/current/816.jpg", line.Camera.Url);
        var protocol = Row(OptionRows.CameraProtocol);
        Assert.True(protocol.Set!(1 + Array.IndexOf(Cameras.Protocols, CameraProtocol.Mjpeg)));
        Assert.Equal(CameraProtocol.Mjpeg, line.Camera.Protocol);
        // Another camera picked: read automatically again.
        Assert.True(Row(OptionRows.CameraUrl).Pick!(new OptionFound("x", "", "https://example.com/a.m3u8")));
        Assert.Null(line.Camera.Protocol);
        Assert.True(Row(OptionRows.CameraFit).Set!(1));
        Assert.Equal((CameraFit?)CameraFit.Whole, line.Camera.Fit);
        Assert.True(Row(OptionRows.CameraMute).Set!(0));
        Assert.False(line.Camera.Mute);
        Assert.True(Row(OptionRows.CameraName).SetText!("  Lake  "));
        Assert.Equal("Lake", line.Camera.Name);
        line.Camera.Url = "https://example.com/a.jpg";
        var pace = Row(OptionRows.CameraPace);
        Assert.True(pace.Set!(Array.IndexOf(Cameras.Paces, 10)));
        Assert.Equal(10, line.Camera.Seconds);
        Assert.True(Row(OptionRows.CameraPace).Set!(Array.IndexOf(Cameras.Paces, Cameras.DefaultSeconds)));
        Assert.Null(line.Camera.Seconds);
    }

    [Fact]
    public void TheFieldOffersTheOtherCamerasThenPublicOnesAndKeepsAnAddressTyped()
    {
        var other = ListKind.Camera.New();
        other.Camera = new CameraOptions { Url = "http://garden.local/video.mjpg", Name = "Garden" };
        var folder = BarKind.FolderList.New();
        CellItems.Put(folder.Folder!, other);
        var config = new Config { Layouts = [new TaskbarLayout { Islands = [folder] }] };
        var row = Rows("", config: config).Single(each => each.Key == OptionRows.CameraUrl);
        var finder = row.Finder!;
        var offered = finder.Suggested;
        Assert.Equal("http://garden.local/video.mjpg", offered[0].Value);
        Assert.Equal("Garden", offered[0].Title);
        Assert.Equal(KnownCameras.All.Count + 1, offered.Count);
        Assert.All(KnownCameras.All, known => Assert.Contains(offered, found => (string?)found.Value == known.Url));
        // Typed: an address its own row first; words find a camera by its name or place.
        var typed = finder.FindAsync("cam.example.com/live.m3u8", default).Result;
        Assert.Equal("https://cam.example.com/live.m3u8", typed[0].Value);
        Assert.Contains(finder.FindAsync("pizol", default).Result, found => found.Title == "Pizol");
        Assert.Equal("https://cam.example.com/x.jpg", finder.Typed("cam.example.com/x.jpg")!.Value.Found!.Value);
        Assert.Equal(S.CoreCameraUrlNotAddress, finder.Typed("not an address")!.Value.Said);
        // The one it shows is ticked.
        var own = Rows("https://www.foto-webcam.eu/webcam/wien/current/816.jpg").Single(each => each.Key == OptionRows.CameraUrl).Finder!.Suggested;
        Assert.Single(own, found => found.Chosen);
        Assert.Equal("Wien", own.Single(found => found.Chosen).Title);
    }

    [Fact]
    public void SwitzerlandsCamerasComeFirstThere()
    {
        Assert.Equal("CH", KnownCameras.All[0].Region);
        var austria = KnownCameras.For("AT");
        Assert.Equal(["Wien", "Innsbruck"], austria.Take(2).Select(found => found.Title));
        Assert.Equal(KnownCameras.All.Count, austria.Count);
        // Every one is a camera Karpo reads, and its protocol is known from its address.
        Assert.All(KnownCameras.All, known => Assert.NotNull(Cameras.Guess(known.Url)));
        Assert.Contains(KnownCameras.All, known => Cameras.Guess(known.Url) == CameraProtocol.Hls);
        Assert.Contains(KnownCameras.All, known => Cameras.Guess(known.Url) == CameraProtocol.Dash);
    }
}
