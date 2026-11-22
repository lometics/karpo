// Now playing's art on a list's line at every height, and the one store every face takes it from
// (NowPlayingArt).
//
// The person, of a folder with now playing at every size at 150 %, playing a track with its cover:
// "not all the images are shown in the now playing view". Some faces drew the grey note where the
// others drew the cover. Every face decoded the
// session's thumbnail for itself, all at once, and a face whose read found nothing kept nothing
// for the rest of the track. The art is the track's now: one decode for every face that asks, and
// a decode that finds nothing is tried again on the next read.
//
// The store is held on its own, with decodes made up for it, and through the session's own
// thumbnail — a picture in a stream, as a player hands it over, read by the decoder the faces use.
// The faces are held drawn: at every height of a list's line that has a place for the cover, the
// cover is drawn there rather than the note — each face on its own, and one at every height at once.
//
// The drawing needs Direct2D (KARPO_UI_TESTS=1); the store and the decode do not.

using Karpo.Core;
using Karpo.Dock;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Karpo.Tests.Folders;

namespace Karpo.Tests.Islands;

public sealed class NowPlayingArtTests
{
    /// <summary>The cover the tests play: a colour no card, glyph or text is drawn in.</summary>
    private static readonly (byte B, byte G, byte R) Magenta = (0xC0, 0x10, 0xE0);

    // --- The store ---------------------------------------------------------------------------

    /// <summary>
    /// The cause: a face that read the session before the player handed its thumbnail over — or whose
    /// read failed — kept "no art" for the rest of the track. A decode that finds nothing is not the
    /// track's art: the next read decodes again, and what it finds is kept.
    /// </summary>
    [Fact]
    public async Task ADecodeThatFindsNothingIsTriedAgain()
    {
        var store = new NowPlayingArt();
        var cover = Cover();
        var decodes = 0;
        Task<Pixels?> Decode() => Task.FromResult(++decodes == 1 ? null : cover);

        Assert.Null(await store.For("Now We Are Free", Decode));
        Assert.Same(cover, await store.For("Now We Are Free", Decode));
        // Found, it is the track's: no face decodes it again.
        Assert.Same(cover, await store.For("Now We Are Free", Decode));
        Assert.Equal(2, decodes);
    }

    /// <summary>A decode that throws leaves nothing behind: the next read decodes again.</summary>
    [Fact]
    public async Task ADecodeThatFailsIsTriedAgain()
    {
        var store = new NowPlayingArt();
        var cover = Cover();
        var decodes = 0;
        Task<Pixels?> Decode() => ++decodes == 1 ? Task.FromException<Pixels?>(new IOException("the player went away")) : Task.FromResult<Pixels?>(cover);

        await Assert.ThrowsAsync<IOException>(() => store.For("Now We Are Free", Decode));
        Assert.Same(cover, await store.For("Now We Are Free", Decode));
    }

    /// <summary>
    /// Sixteen faces reading the session at once — a folder with many of them — share one
    /// decode of the thumbnail, and every one of them shows what it found.
    /// </summary>
    [Fact]
    public async Task SixteenFacesAskingAtOnceShareOneDecode()
    {
        var store = new NowPlayingArt();
        var cover = Cover();
        var decodes = 0;
        var arrives = new TaskCompletionSource<Pixels?>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<Pixels?> Decode()
        {
            Interlocked.Increment(ref decodes);
            return arrives.Task;
        }

        var faces = Enumerable.Range(0, 16).Select(_ => store.For("Now We Are Free", Decode)).ToArray();
        Assert.Equal(1, decodes);
        arrives.SetResult(cover);
        Assert.All(await Task.WhenAll(faces), shown => Assert.Same(cover, shown));
        Assert.Equal(1, decodes);
    }

    /// <summary>
    /// The next track is decoded afresh, and a decode of the last one that ends after the next one
    /// began is not taken for the next one's art.
    /// </summary>
    [Fact]
    public async Task AnotherTrackIsDecodedAfresh()
    {
        var store = new NowPlayingArt();
        var (first, second) = (Cover(), Cover());
        var late = new TaskCompletionSource<Pixels?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var lastTrack = store.For("Honor Him", () => late.Task);
        Assert.Same(second, await store.For("Now We Are Free", () => Task.FromResult<Pixels?>(second)));
        late.SetResult(first);
        Assert.Same(first, await lastTrack);
        Assert.Same(second, await store.For("Now We Are Free", () => Task.FromResult<Pixels?>(first)));
    }

    /// <summary>A session that hands over no thumbnail has no art, and nothing is decoded.</summary>
    [Fact]
    public async Task NoThumbnailIsNoArt()
    {
        Assert.Null(await new NowPlayingArt().For("Now We Are Free", null));
    }

    // --- The session's thumbnail -------------------------------------------------------------

    /// <summary>
    /// The art is read from the thumbnail as a player hands it over — a picture in a stream — scaled
    /// down to at most 192 px a side, the same for every face.
    /// </summary>
    [Fact]
    public async Task TheArtIsReadFromTheSessionsThumbnail()
    {
        var art = await MediaIsland.ArtOf(Track(), await Png(300));
        Assert.NotNull(art);
        Assert.Equal(192, art.Width);
        Assert.Equal(192, art.Height);
        Assert.Equal(192 * 192 * 4, art.Data.Length);
    }

    /// <summary>
    /// The regression, through the session's own thumbnail: read before the player has put anything
    /// in it, the thumbnail gives no art — and read again, once it has, the same track's art is there.
    /// A face used to keep the first answer until the track changed.
    /// </summary>
    [Fact]
    public async Task AThumbnailReadBeforeItHoldsAPictureIsReadAgain()
    {
        var track = Track();
        Assert.Null(await MediaIsland.ArtOf(track, RandomAccessStreamReference.CreateFromStream(new InMemoryRandomAccessStream())));
        Assert.NotNull(await MediaIsland.ArtOf(track, await Png(96)));
    }

    // --- The faces ---------------------------------------------------------------------------

    /// <summary>The heights a line of now playing may be set to, in rows: one to its most (ListWidgets).</summary>
    private static IEnumerable<int> Heights() => Enumerable.Range(1, ListKind.Media.ListRange().Most);

    /// <summary>
    /// With a track that has its art, every height of a list's line with a place for the cover draws
    /// the cover there: the art's own colour in the middle of the place, and no note glyph in it.
    /// </summary>
    [UiFact]
    public void EveryHeightDrawsTheCoverInItsPlace()
    {
        var wrong = new List<string>();
        var placed = 0;
        Offscreen.OnStaThread(gfx =>
        {
            foreach (var scale in new[] { 1f, 1.5f, 2f })
            {
                foreach (var rows in Heights())
                {
                    using var face = Face(gfx);
                    var (hasPlace, fault) = Check(gfx, face, rows, scale);
                    if (fault is not null)
                    {
                        wrong.Add($"{rows} rows at {scale * 100:0} %: {fault}");
                    }
                    else if (hasPlace)
                    {
                        placed++;
                    }
                }
            }
        });
        Assert.True(wrong.Count == 0, string.Join("\n", wrong));
        Assert.True(placed > 0, "no height of the line has a place for the cover");
    }

    /// <summary>
    /// The same with a line at every height showing at once, as a folder shows them: each draws the
    /// cover in its place.
    /// </summary>
    [UiFact]
    public void EveryHeightAtOnceDrawsTheCover()
    {
        var wrong = new List<string>();
        Offscreen.OnStaThread(gfx =>
        {
            var heights = Heights().ToList();
            var faces = heights.Select(_ => Face(gfx)).ToList();
            try
            {
                for (var index = 0; index < heights.Count; index++)
                {
                    if (Check(gfx, faces[index], heights[index], 1.5f).Fault is { } fault)
                    {
                        wrong.Add($"{heights[index]} rows: {fault}");
                    }
                }
            }
            finally
            {
                foreach (var face in faces)
                {
                    face.Dispose();
                }
            }
        });
        Assert.True(wrong.Count == 0, string.Join("\n", wrong));
    }

    /// <summary>
    /// Whether <paramref name="face"/>'s line of <paramref name="rows"/> rows, at the width every
    /// panel has, has a place for the cover, and what is wrong with it, or null: drawn there as the
    /// art rather than the note.
    /// </summary>
    private static unsafe (bool Placed, string? Fault) Check(Gfx gfx, MediaIsland face, int rows, float scale)
    {
        var look = TestLooks.LookFor(dark: true);
        var box = new D2D_RECT_F(0, 0, FolderListWidths.MediumDip * scale, rows * LineHeight.Row * FolderOptions.CellDip * scale);
        var style = FolderFlyout.StyleFor(scale, look, FlyoutTheme.For(look));
        var place = face.CardParts(box, style).Art;
        if (place.right - place.left <= 0)
        {
            return (false, null);
        }
        var glyphs = new List<D2D_RECT_F>();
        var before = PaintWatch.Watching;
        PaintWatch.Watching = (at, what, color) =>
        {
            if (what == Painted.Glyph)
            {
                glyphs.Add(at);
            }
            return color;
        };
        (byte B, byte G, byte R, byte A) middle = default;
        try
        {
            Offscreen.Bitmap(
                TilePaint.Width(box),
                TilePaint.Height(box),
                dc => face.PaintOn(dc, box, style, WidgetSurface.List),
                bitmap => middle = PixelAt(bitmap, (uint)MathF.Ceiling(TilePaint.Width(box)), (uint)MathF.Ceiling(TilePaint.Height(box)), (int)((place.left + place.right) / 2), (int)((place.top + place.bottom) / 2)));
        }
        finally
        {
            PaintWatch.Watching = before;
        }
        var centre = ((place.left + place.right) / 2, (place.top + place.bottom) / 2);
        if (glyphs.Any(glyph => glyph.left <= centre.Item1 && centre.Item1 <= glyph.right && glyph.top <= centre.Item2 && centre.Item2 <= glyph.bottom))
        {
            return (true, "the note stands where the cover goes");
        }
        return middle.A == 255 && Near(middle.B, Magenta.B) && Near(middle.G, Magenta.G) && Near(middle.R, Magenta.R)
            ? (true, null)
            : (true, $"the middle of its place is ({middle.R}, {middle.G}, {middle.B}, {middle.A}), not the cover");
    }

    private static bool Near(byte drawn, byte wanted) => Math.Abs(drawn - wanted) <= 2;

    /// <summary>A face made as a list makes its line, playing a track with its cover (<see cref="MediaIsland.Stand"/>).</summary>
    private static MediaIsland Face(Gfx gfx)
    {
        var settings = ListKind.Media.New();
        settings.Id = "media";
        MediaIsland.Stand = new MediaStand(Title: "Now We Are Free", Artist: "Hans Zimmer", Art: Cover());
        try
        {
            return new MediaIsland("layout:default/media", settings, gfx, new Config());
        }
        finally
        {
            MediaIsland.Stand = null;
        }
    }

    /// <summary>A cover of one colour, as a thumbnail decodes to.</summary>
    private static Pixels Cover(int side = 64)
    {
        var data = new byte[side * side * 4];
        for (var at = 0; at < data.Length; at += 4)
        {
            (data[at], data[at + 1], data[at + 2], data[at + 3]) = (Magenta.B, Magenta.G, Magenta.R, 255);
        }
        return new Pixels { Data = data, Width = side, Height = side };
    }

    /// <summary>A track of this test's own, so no other read of the shared store is taken for it.</summary>
    private static string Track() => "test\n" + Guid.NewGuid().ToString("N");

    /// <summary>A thumbnail as a player hands it over: a PNG of <paramref name="side"/> px a side in a stream.</summary>
    private static async Task<IRandomAccessStreamReference> Png(int side)
    {
        var stream = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, (uint)side, (uint)side, 96, 96, Cover(side).Data);
        await encoder.FlushAsync();
        stream.Seek(0);
        return RandomAccessStreamReference.CreateFromStream(stream);
    }

    /// <summary>The pixel at (<paramref name="x"/>, <paramref name="y"/>): premultiplied blue, green, red and alpha.</summary>
    private static unsafe (byte B, byte G, byte R, byte A) PixelAt(IWICBitmap* bitmap, uint width, uint height, int x, int y)
    {
        IWICBitmapLock* locked = null;
        try
        {
            var all = new WICRect { X = 0, Y = 0, Width = (int)width, Height = (int)height };
            TerraFX.Interop.Windows.Windows.ThrowIfFailed(bitmap->Lock(&all, (uint)WICBitmapLockFlags.WICBitmapLockRead, &locked));
            uint stride = 0;
            TerraFX.Interop.Windows.Windows.ThrowIfFailed(locked->GetStride(&stride));
            uint size = 0;
            byte* data = null;
            TerraFX.Interop.Windows.Windows.ThrowIfFailed(locked->GetDataPointer(&size, &data));
            var at = (y * stride) + (x * 4);
            return (data[at], data[at + 1], data[at + 2], data[at + 3]);
        }
        finally
        {
            if (locked != null)
            {
                _ = locked->Release();
            }
        }
    }
}
