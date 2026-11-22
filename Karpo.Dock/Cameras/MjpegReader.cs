// camera: an MJPEG stream's pictures out of the bytes as they come (docs\widgets.md §331).
//
// An MJPEG camera answers one HTTP request with no end: multipart/x-mixed-replace, a part per picture,
// each after a line of its boundary and a few headers, most with a Content-Length. Cameras are loose
// about it, so this reads what they send rather than what the RFC says:
//
//   * the boundary is found as "--" and its name wherever it stands, whether the camera's header named it
//     with its dashes ("boundary=--myboundary") or without, and whether a CRLF comes before it or not;
//   * a part's Content-Length is believed where it is given, and otherwise the part runs to the next
//     boundary, its trailing line ends dropped;
//   * an answer that names no boundary is read as JPEGs one after another, each from its start (FF D8)
//     to its end (FF D9).
//
// Nothing is kept but the bytes of the picture not yet whole, and never more than MostFrame of them: a
// stream that sends more without a picture is read from its next boundary on.

using System.Text;

namespace Karpo.Dock;

/// <summary>An MJPEG stream's pictures, out of its bytes as they come (see the top of the file).</summary>
internal sealed class MjpegReader
{
    /// <summary>The most bytes one picture may take: past it, what was gathered is dropped.</summary>
    public const int MostFrame = 8 << 20;

    private readonly byte[]? delimiter;
    private byte[] buffer = new byte[64 << 10];
    private int length;

    // Where the part being read starts in the buffer, past its headers, and how long it says it is; -1 for
    // none yet (seeking the boundary) and for no length given.
    private int body = -1;
    private int bodyLength = -1;

    /// <param name="boundary">The boundary the answer's content type names (<see cref="Core.Cameras.Boundary"/>); null to read JPEGs one after another.</param>
    public MjpegReader(string? boundary)
    {
        var name = boundary?.Trim().Trim('"').TrimStart('-');
        delimiter = string.IsNullOrEmpty(name) ? null : Encoding.ASCII.GetBytes("--" + name);
    }

    /// <summary>How many bytes are held, for a test: never more than a picture's.</summary>
    internal int Held => length;

    /// <summary>The pictures <paramref name="data"/> completed, in their order (usually none or one); each a whole JPEG's bytes.</summary>
    public List<byte[]> Feed(ReadOnlySpan<byte> data)
    {
        var frames = new List<byte[]>();
        while (data.Length > 0)
        {
            // Never more than a picture's worth held: what does not fit in the room left is taken in turns.
            var room = Math.Max(0, (MostFrame + (64 << 10)) - length);
            if (room == 0)
            {
                Reset();
                continue;
            }
            var take = Math.Min(room, data.Length);
            Append(data[..take]);
            data = data[take..];
            if (delimiter is null)
            {
                Jpegs(frames);
            }
            else
            {
                Parts(frames);
            }
            if (length > MostFrame)
            {
                // No picture in more than a picture's room: start again at the next boundary.
                Reset();
            }
        }
        return frames;
    }

    private void Append(ReadOnlySpan<byte> data)
    {
        if (length + data.Length > buffer.Length)
        {
            Array.Resize(ref buffer, Math.Max(buffer.Length * 2, length + data.Length));
        }
        data.CopyTo(buffer.AsSpan(length));
        length += data.Length;
    }

    private void Reset()
    {
        length = 0;
        body = -1;
        bodyLength = -1;
    }

    /// <summary>Drops the first <paramref name="count"/> bytes held.</summary>
    private void Consume(int count)
    {
        count = Math.Clamp(count, 0, length);
        Buffer.BlockCopy(buffer, count, buffer, 0, length - count);
        length -= count;
    }

    /// <summary>The parts of a multipart answer: each part's body, by its Content-Length or up to the next boundary.</summary>
    private void Parts(List<byte[]> frames)
    {
        var mark = delimiter!;
        while (true)
        {
            var held = buffer.AsSpan(0, length);
            if (body < 0)
            {
                var at = held.IndexOf(mark);
                if (at < 0)
                {
                    // Keep what could be the start of a boundary cut in two.
                    Consume(length - (mark.Length - 1));
                    return;
                }
                var after = held[(at + mark.Length)..];
                var headersEnd = HeadersEnd(after, out var endLength);
                if (headersEnd < 0)
                {
                    Consume(at);
                    return;
                }
                bodyLength = ContentLength(after[..headersEnd]);
                body = at + mark.Length + headersEnd + endLength;
                continue;
            }
            if (bodyLength >= 0)
            {
                if (bodyLength > MostFrame)
                {
                    Reset();
                    return;
                }
                if (length - body < bodyLength)
                {
                    return;
                }
                Take(frames, held.Slice(body, bodyLength));
                Consume(body + bodyLength);
                (body, bodyLength) = (-1, -1);
                continue;
            }
            var next = held[body..].IndexOf(mark);
            if (next < 0)
            {
                return;
            }
            Take(frames, held.Slice(body, next).TrimEnd("\r\n"u8));
            Consume(body + next);
            (body, bodyLength) = (-1, -1);
        }
    }

    /// <summary>JPEGs one after another, where no boundary was named: each from its start marker to its end marker.</summary>
    private void Jpegs(List<byte[]> frames)
    {
        while (true)
        {
            var held = buffer.AsSpan(0, length);
            var start = held.IndexOf((ReadOnlySpan<byte>)[0xFF, 0xD8]);
            if (start < 0)
            {
                Consume(length - 1);
                return;
            }
            var end = held[(start + 2)..].IndexOf((ReadOnlySpan<byte>)[0xFF, 0xD9]);
            if (end < 0)
            {
                Consume(start);
                return;
            }
            var stop = start + 2 + end + 2;
            Take(frames, held[start..stop]);
            Consume(stop);
        }
    }

    /// <summary>A picture's bytes, where they are one (anything but blank lines): a camera's empty part is none.</summary>
    private static void Take(List<byte[]> frames, ReadOnlySpan<byte> picture)
    {
        picture = picture.TrimStart("\r\n"u8);
        if (picture.Length > 0)
        {
            frames.Add(picture.ToArray());
        }
    }

    /// <summary>Where a part's headers end in <paramref name="after"/> (its boundary line's rest and its headers), and how long the blank line is; -1 where not yet.</summary>
    private static int HeadersEnd(ReadOnlySpan<byte> after, out int endLength)
    {
        var crlf = after.IndexOf("\r\n\r\n"u8);
        var lf = after.IndexOf("\n\n"u8);
        if (crlf >= 0 && (lf < 0 || crlf <= lf))
        {
            endLength = 4;
            return crlf;
        }
        endLength = 2;
        return lf;
    }

    /// <summary>The Content-Length a part's headers give, -1 for none.</summary>
    private static int ContentLength(ReadOnlySpan<byte> headers)
    {
        foreach (var line in Encoding.ASCII.GetString(headers).Split('\n'))
        {
            var colon = line.IndexOf(':');
            if (colon > 0 && line[..colon].Trim().Equals("Content-Length", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(line[(colon + 1)..].Trim(), out var value) && value >= 0)
            {
                return value;
            }
        }
        return -1;
    }
}
