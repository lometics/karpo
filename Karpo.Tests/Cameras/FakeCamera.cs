// camera: a camera of the test's own on the loopback (docs\widgets.md §331), so a camera line is read for
// real with no internet: an HTTP server on 127.0.0.1 that answers every request with what the test says —
// an MJPEG stream that goes on until it is let go, a picture, a page, an error — and counts what it was asked.

using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Karpo.Tests.CameraLines;

/// <summary>A camera on the loopback (see the top of the file).</summary>
internal sealed class FakeCamera : IDisposable
{
    private readonly TcpListener listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource stop = new();
    private readonly Func<Request, Answer> answer;
    private int asked;
    private int open;

    /// <summary>What was asked: the path, and the If-None-Match header where one came.</summary>
    public sealed record Request(string Path, string? IfNoneMatch);

    /// <summary>
    /// What it answers: a status, headers and a body; or, with <paramref name="Parts"/>, an MJPEG stream of those
    /// pictures one after another, round and round, <paramref name="Every"/> ms apart, until the client lets go.
    /// </summary>
    public sealed record Answer(int Status, string ContentType, byte[] Body, IReadOnlyList<byte[]>? Parts = null, int Every = 40, string? ETag = null);

    public FakeCamera(Func<Request, Answer> answer)
    {
        this.answer = answer;
        listener.Start();
        _ = Task.Run(Serve);
    }

    /// <summary>Its address for <paramref name="path"/>.</summary>
    public string Url(string path) => $"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}{path}";

    /// <summary>How many requests came.</summary>
    public int Asked => Volatile.Read(ref asked);

    /// <summary>How many answers are being written now: an MJPEG stream a client still reads.</summary>
    public int Open => Volatile.Read(ref open);

    private async Task Serve()
    {
        try
        {
            while (!stop.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(stop.Token);
                _ = Task.Run(() => Reply(client));
            }
        }
        catch (Exception error) when (error is OperationCanceledException or ObjectDisposedException or SocketException)
        {
        }
    }

    private async Task Reply(TcpClient client)
    {
        try
        {
            using (client)
            {
                var stream = client.GetStream();
                var head = await Head(stream);
                var lines = head.Split("\r\n");
                var path = lines[0].Split(' ')[1];
                var tag = lines.Skip(1).Select(line => line.Split(':', 2)).Where(pair => pair.Length == 2 && pair[0].Trim().Equals("If-None-Match", StringComparison.OrdinalIgnoreCase)).Select(pair => pair[1].Trim()).FirstOrDefault();
                Interlocked.Increment(ref asked);
                var said = answer(new Request(path, tag));
                if (said.Parts is { } parts)
                {
                    Interlocked.Increment(ref open);
                    try
                    {
                        await Write(stream, $"HTTP/1.1 200 OK\r\nContent-Type: multipart/x-mixed-replace; boundary=--frame\r\nConnection: close\r\n\r\n");
                        for (var at = 0; !stop.IsCancellationRequested; at++)
                        {
                            var part = parts[at % parts.Count];
                            await Write(stream, $"--frame\r\nContent-Type: image/bmp\r\nContent-Length: {part.Length}\r\n\r\n");
                            await stream.WriteAsync(part, stop.Token);
                            await Write(stream, "\r\n");
                            await Task.Delay(said.Every, stop.Token);
                        }
                    }
                    finally
                    {
                        Interlocked.Decrement(ref open);
                    }
                    return;
                }
                var reason = said.Status switch { 200 => "OK", 304 => "Not Modified", 404 => "Not Found", _ => "Status" };
                var tagLine = said.ETag is { } etag ? $"ETag: {etag}\r\n" : "";
                await Write(stream, $"HTTP/1.1 {said.Status} {reason}\r\nContent-Type: {said.ContentType}\r\nContent-Length: {said.Body.Length}\r\n{tagLine}Connection: close\r\n\r\n");
                await stream.WriteAsync(said.Body, stop.Token);
            }
        }
        catch (Exception error) when (error is IOException or SocketException or OperationCanceledException or ObjectDisposedException)
        {
            // The client let go: an MJPEG stream ends so.
        }
    }

    private async Task Write(NetworkStream stream, string text) => await stream.WriteAsync(Encoding.ASCII.GetBytes(text), stop.Token);

    private async Task<string> Head(NetworkStream stream)
    {
        var bytes = new List<byte>();
        var one = new byte[1];
        while (await stream.ReadAsync(one, stop.Token) == 1)
        {
            bytes.Add(one[0]);
            if (bytes.Count >= 4 && bytes[^1] == '\n' && bytes[^2] == '\r' && bytes[^3] == '\n' && bytes[^4] == '\r')
            {
                break;
            }
        }
        return Encoding.ASCII.GetString([.. bytes]);
    }

    public void Dispose()
    {
        stop.Cancel();
        listener.Stop();
        stop.Dispose();
    }

    /// <summary>
    /// A picture <paramref name="width"/> × <paramref name="height"/> as a BMP's bytes — a file WIC reads as it
    /// reads a JPEG, written here with nothing but its header — a sky over a hill, in the shade <paramref name="shade"/>.
    /// </summary>
    public static byte[] Picture(int width, int height, byte shade = 0)
    {
        var row = ((width * 3) + 3) & ~3;
        var size = 54 + (row * height);
        var bytes = new byte[size];
        void Int(int at, int value) => BitConverter.GetBytes(value).CopyTo(bytes, at);
        bytes[0] = (byte)'B';
        bytes[1] = (byte)'M';
        Int(2, size);
        Int(10, 54);
        Int(14, 40);
        Int(18, width);
        Int(22, height);
        BitConverter.GetBytes((short)1).CopyTo(bytes, 26);
        BitConverter.GetBytes((short)24).CopyTo(bytes, 28);
        Int(34, row * height);
        for (var y = 0; y < height; y++)
        {
            // Bottom row first, as a BMP is.
            var up = height - 1 - y;
            for (var x = 0; x < width; x++)
            {
                var hill = up > (height * 0.6) + (Math.Sin(x / (double)width * Math.PI * 2) * height * 0.12);
                var at = 54 + (y * row) + (x * 3);
                (bytes[at], bytes[at + 1], bytes[at + 2]) = hill
                    ? ((byte)(40 + shade), (byte)(120 + (up * 60 / height)), (byte)(50 + shade))
                    : ((byte)(230 - (up * 80 / height)), (byte)(170 - (up * 40 / height) + shade / 4), (byte)(90 + shade));
            }
        }
        return bytes;
    }
}
