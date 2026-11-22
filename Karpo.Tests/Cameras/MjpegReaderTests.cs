// camera: an MJPEG stream's pictures out of its bytes, however they come (docs\widgets.md §331): parts with a
// Content-Length and without, a boundary named with its dashes and without, the bytes in one piece and one
// byte at a time, a stream that names no boundary, and one that sends more than a picture without one.

using System.Text;
using Karpo.Dock;

namespace Karpo.Tests.CameraLines;

public sealed class MjpegReaderTests
{
    /// <summary>A JPEG's bytes as far as the reader cares: its start, something, its end.</summary>
    private static byte[] Jpeg(byte mark) => [0xFF, 0xD8, 0xFF, 0xE0, mark, mark, 0x0D, 0x0A, mark, 0xFF, 0xD9];

    private static byte[] Ascii(string text) => Encoding.ASCII.GetBytes(text);

    /// <summary>A multipart stream of <paramref name="pictures"/>, each under <paramref name="delimiter"/>, with its Content-Length or without.</summary>
    private static byte[] Stream(string delimiter, bool lengths, bool crlfBefore, params byte[][] pictures)
    {
        var bytes = new List<byte>();
        foreach (var picture in pictures)
        {
            if (crlfBefore)
            {
                bytes.AddRange(Ascii("\r\n"));
            }
            bytes.AddRange(Ascii($"{delimiter}\r\nContent-Type: image/jpeg\r\n"));
            if (lengths)
            {
                bytes.AddRange(Ascii($"Content-Length: {picture.Length}\r\n"));
            }
            bytes.AddRange(Ascii("\r\n"));
            bytes.AddRange(picture);
        }
        // The next part's boundary closes the last one where no length says where it ends.
        bytes.AddRange(Ascii($"\r\n{delimiter}\r\n"));
        return [.. bytes];
    }

    [Theory]
    // Axis's: its header names it without dashes, parts carry their length.
    [InlineData("myboundary", "--myboundary", true, true)]
    // Panasonic's and many others': the header names it with its dashes, as the parts do.
    [InlineData("--myboundary", "--myboundary", true, false)]
    // No lengths: each part runs to the next boundary.
    [InlineData("frame", "--frame", false, true)]
    [InlineData("--frame", "--frame", false, false)]
    // ffmpeg's mpjpeg: "boundary=ffmpeg", parts "--ffmpeg".
    [InlineData("ffmpeg", "--ffmpeg", true, true)]
    public void EveryPartIsAPictureHoweverTheBytesCome(string named, string delimiter, bool lengths, bool crlfBefore)
    {
        byte[][] pictures = [Jpeg(1), Jpeg(2), Jpeg(3)];
        var stream = Stream(delimiter, lengths, crlfBefore, pictures);
        // In one piece.
        var whole = new MjpegReader(named).Feed(stream);
        Assert.Equal(pictures, whole);
        // A byte at a time: the same pictures, in their order, and never more than a picture held.
        var reader = new MjpegReader(named);
        var one = new List<byte[]>();
        foreach (var b in stream)
        {
            one.AddRange(reader.Feed([b]));
            Assert.True(reader.Held < 200, $"{reader.Held} bytes held");
        }
        Assert.Equal(pictures, one);
        // In odd pieces.
        reader = new MjpegReader(named);
        var odd = new List<byte[]>();
        for (var at = 0; at < stream.Length; at += 7)
        {
            odd.AddRange(reader.Feed(stream.AsSpan(at, Math.Min(7, stream.Length - at))));
        }
        Assert.Equal(pictures, odd);
    }

    [Fact]
    public void WithNoBoundaryTheJpegsAreReadByTheirMarkers()
    {
        var stream = (byte[])[.. Ascii("junk"), .. Jpeg(4), .. Ascii("\r\n"), .. Jpeg(5), 0xFF];
        var reader = new MjpegReader(null);
        var frames = new List<byte[]>();
        foreach (var chunk in stream.Chunk(3))
        {
            frames.AddRange(reader.Feed(chunk));
        }
        Assert.Equal([Jpeg(4), Jpeg(5)], frames);
    }

    [Fact]
    public void AStreamWithoutAPictureIsNotHeldPastAPicturesRoom()
    {
        var reader = new MjpegReader("frame");
        var noise = new byte[1 << 20];
        new Random(4).NextBytes(noise);
        for (var at = 0; at < 12; at++)
        {
            Assert.Empty(reader.Feed(noise.Where(b => b != '-').ToArray()));
            Assert.True(reader.Held <= MjpegReader.MostFrame);
        }
        // And a picture after it is read.
        Assert.Equal([Jpeg(6)], reader.Feed(Stream("--frame", true, true, Jpeg(6))));
        // A part that says it is longer than a picture may be is dropped, and the next read.
        var liar = new MjpegReader("frame");
        Assert.Empty(liar.Feed(Ascii($"--frame\r\nContent-Length: {MjpegReader.MostFrame + 1}\r\n\r\n")));
        Assert.Equal([Jpeg(7)], liar.Feed(Stream("--frame", true, true, Jpeg(7))));
    }
}
