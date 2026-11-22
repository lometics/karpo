// A thumbnail toolbar's pictures (Karpo.Dock\ThumbBar.cs): an icon a button sent (THB_ICON), or a
// picture of the image list its window set (THB_BITMAP), which is how Spotify draws previous, play
// and next. "for spotify for example it still does not properly show the media control arrows, it
// still just shows letters for them": image list buttons were drawn as letter tiles of their
// tooltips, the image list never read.
//
// Through a fake of Windows' drawing (IThumbPictures), so no icon or image list is made: what is
// checked is which handle is drawn, which stream is read, and which picture each button ends with.

using System.Buffers.Binary;
using Karpo.Dock;

namespace Karpo.Tests.Signals;

public sealed class ThumbPicturesTests
{
    private const nint Window = 0x3A0B12;

    /// <summary>Draws a picture per icon and per image list stream, and remembers what it was asked.</summary>
    private sealed class FakePictures(int images = 3) : IThumbPictures
    {
        public List<nint> Icons { get; } = [];

        public List<byte[]> Streams { get; } = [];

        public Dictionary<nint, Pixels> Drawn { get; } = [];

        public List<Pixels?> Pictures { get; } = [.. Enumerable.Range(0, images).Select(index => (Pixels?)Picture((byte)(100 + index)))];

        public Pixels? Icon(nint icon)
        {
            Icons.Add(icon);
            return Drawn.TryGetValue(icon, out var drawn) ? drawn : Drawn[icon] = Picture((byte)icon);
        }

        public IReadOnlyList<Pixels?> ImageList(ReadOnlySpan<byte> stream)
        {
            Streams.Add(stream.ToArray());
            return Pictures;
        }
    }

    private static Pixels Picture(byte shade) => new() { Data = [shade, shade, shade, 255], Width = 1, Height = 1 };

    /// <summary>
    /// An image list as ImageList_Write writes it, after <paramref name="prefix"/> bytes of
    /// something else: the 28-byte ILHEAD ("IL", a version, the count, the most, the grow, the
    /// pictures' size) and the start of a bitmap.
    /// </summary>
    private static byte[] Written(int prefix = 0, ushort count = 3, ushort cx = 16, ushort cy = 16)
    {
        var data = new byte[prefix + ThumbBar.ImageListHeader + 16];
        var head = data.AsSpan(prefix);
        BinaryPrimitives.WriteUInt16LittleEndian(head, ThumbBar.ImageListMagic);
        BinaryPrimitives.WriteUInt16LittleEndian(head[2..], 0x0600);
        BinaryPrimitives.WriteUInt16LittleEndian(head[4..], count);
        BinaryPrimitives.WriteUInt16LittleEndian(head[6..], count);
        BinaryPrimitives.WriteUInt16LittleEndian(head[8..], 4);
        BinaryPrimitives.WriteUInt16LittleEndian(head[10..], cx);
        BinaryPrimitives.WriteUInt16LittleEndian(head[12..], cy);
        head[ThumbBar.ImageListHeader] = (byte)'B';
        head[ThumbBar.ImageListHeader + 1] = (byte)'M';
        return data;
    }

    /// <summary>Previous, play and next as Spotify sends them: image 0, 1 and 2, tooltips and flags, no icons.</summary>
    private static ThumbButtonData[] Spotify =>
    [
        new(0x1 | 0x4 | 0x8, 1, 0, 0, "Previous", 0),
        new(0x1 | 0x4 | 0x8, 2, 1, 0, "Play", 0),
        new(0x1 | 0x4 | 0x8, 3, 2, 0, "Next", 0),
    ];

    [Fact]
    public void Only_a_button_that_sent_an_icon_has_it_drawn()
    {
        var pictures = new FakePictures();
        ThumbButtonData[] sent =
        [
            new(0x2 | 0x4, 1, 0, 0x013C0BBB, "Play", 0),
            new(0x1 | 0x4, 2, 1, 0, "Next", 0),
            // An icon the mask does not name does not count.
            new(0x4, 3, 0, 0x00220044, "Stop", 0),
        ];

        var icons = ThumbBar.Icons(sent, pictures);

        Assert.Equal([(nint)0x013C0BBB], pictures.Icons);
        Assert.NotNull(icons[0]);
        Assert.Null(icons[1]);
        Assert.Null(icons[2]);
    }

    [Fact]
    public void An_icon_handle_with_its_high_bit_set_is_drawn_sign_extended()
    {
        var pictures = new FakePictures();

        ThumbBar.Icons([new(0x2, 1, 0, 0x8001_0203, "", 0)], pictures);

        Assert.Equal([unchecked((nint)(int)0x8001_0203)], pictures.Icons);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(32)]
    public void The_image_list_is_found_where_its_header_starts(int prefix)
    {
        Assert.Equal(prefix, ThumbBar.ImageListStart(Written(prefix)));
    }

    [Fact]
    public void Data_that_is_no_image_list_holds_none()
    {
        Assert.Equal(-1, ThumbBar.ImageListStart([]));
        Assert.Equal(-1, ThumbBar.ImageListStart(new byte[64]));
        // Too short for a header.
        Assert.Equal(-1, ThumbBar.ImageListStart(Written().AsSpan(0, ThumbBar.ImageListHeader - 1)));
        // "IL" with no size an image list's pictures could have.
        Assert.Equal(-1, ThumbBar.ImageListStart(Written(cx: 0)));
        Assert.Equal(-1, ThumbBar.ImageListStart(Written(cy: 4000)));
        // Too far in to be the message's own.
        Assert.Equal(-1, ThumbBar.ImageListStart(Written(prefix: 34)));
        // A tooltip's text is no image list.
        Assert.Equal(-1, ThumbBar.ImageListStart(System.Text.Encoding.Unicode.GetBytes("ILLUSTRATION - Play next track please")));
    }

    [Fact]
    public void The_stream_read_is_the_image_list_from_its_header_on()
    {
        var pictures = new FakePictures();
        var data = Written(prefix: 8);

        var images = ThumbBar.Images(data, pictures);

        Assert.Equal(pictures.Pictures, images);
        Assert.Equal(data[8..], Assert.Single(pictures.Streams));
    }

    [Fact]
    public void Data_without_an_image_list_reads_nothing()
    {
        var pictures = new FakePictures();

        Assert.Empty(ThumbBar.Images(new byte[40], pictures));
        Assert.Empty(pictures.Streams);
    }

    [Fact]
    public void Image_list_buttons_take_their_pictures_by_index()
    {
        var pictures = new FakePictures();
        var buttons = ThumbBar.Merge(null, Spotify, ThumbBar.Icons(Spotify, pictures), update: false);
        // Before the image list: nothing to draw, so letters.
        Assert.All(buttons, button => Assert.Null(button.Picture));

        var drawn = ThumbBar.WithImages(buttons, pictures.Pictures);

        Assert.Equal([pictures.Pictures[0], pictures.Pictures[1], pictures.Pictures[2]], drawn.Select(button => button.Picture));
        Assert.Equal(["Previous", "Play", "Next"], drawn.Select(button => button.Tip));
    }

    [Fact]
    public void An_icon_is_drawn_over_an_image_lists_picture()
    {
        var pictures = new FakePictures();
        ThumbButtonData[] sent = [new(0x1 | 0x2, 1, 0, 0x0505, "", 0), new(0x1, 2, 1, 0, "", 0)];
        var buttons = ThumbBar.Merge(null, sent, ThumbBar.Icons(sent, pictures), update: false);

        var drawn = ThumbBar.WithImages(buttons, pictures.Pictures);

        Assert.Same(pictures.Drawn[0x0505], drawn[0].Picture);
        Assert.Same(pictures.Pictures[1], drawn[1].Picture);
    }

    [Fact]
    public void An_index_the_image_list_has_no_picture_for_leaves_the_letter()
    {
        var pictures = new FakePictures(images: 2);
        ThumbButtonData[] sent = [new(0x1, 1, 5, 0, "Far", 0), new(0x4, 2, 0, 0, "None", 0), new(0x1, 3, 1, 0, "Last", 0)];
        var buttons = ThumbBar.Merge(null, sent, [], update: false);
        pictures.Pictures[1] = null;

        var drawn = ThumbBar.WithImages(buttons, pictures.Pictures);

        // Image 5 of two; no image at all (index -1); a picture that could not be drawn.
        Assert.All(drawn, button => Assert.Null(button.Picture));
        Assert.Same(buttons, ThumbBar.WithImages(buttons, null));
        Assert.Same(buttons, ThumbBar.WithImages(buttons, []));
    }

    [Fact]
    public void The_image_list_set_before_the_toolbar_draws_it()
    {
        // ThumbBarSetImageList comes first, as ThumbBarAddButtons' documentation asks.
        var signals = new AppSignals();
        var pictures = new FakePictures();

        Assert.True(signals.ThumbImages(Window, ThumbBar.Images(Written(), pictures)));
        Assert.Null(signals.ThumbOf(Window));
        Assert.True(signals.ThumbButtons(Window, Spotify, ThumbBar.Icons(Spotify, pictures), update: false));

        Assert.Equal(pictures.Pictures, signals.ThumbOf(Window)!.Buttons.Select(button => button.Picture));
    }

    [Fact]
    public void The_image_list_set_after_the_toolbar_draws_it_and_play_turning_pause_follows()
    {
        var signals = new AppSignals();
        var pictures = new FakePictures();
        signals.ThumbButtons(Window, Spotify, ThumbBar.Icons(Spotify, pictures), update: false);
        Assert.All(signals.ThumbOf(Window)!.Buttons, button => Assert.Null(button.Picture));

        Assert.True(signals.ThumbImages(Window, ThumbBar.Images(Written(), pictures)));
        Assert.Same(pictures.Pictures[1], signals.ThumbOf(Window)!.Buttons[1].Picture);

        // Playing: the middle button takes image 2, the pause picture in this list.
        Assert.True(signals.ThumbButtons(Window, [new(0x1 | 0x4, 2, 2, 0, "Pause", 0)], [], update: true));
        var playing = signals.ThumbOf(Window)!.Buttons[1];
        Assert.Equal("Pause", playing.Tip);
        Assert.Same(pictures.Pictures[2], playing.Picture);
    }

    [Fact]
    public void An_image_list_taken_away_leaves_letters_and_nothing_kept()
    {
        var signals = new AppSignals();
        var pictures = new FakePictures();
        signals.ThumbImages(Window, ThumbBar.Images(Written(), pictures));
        signals.ThumbButtons(Window, Spotify, [], update: false);

        Assert.True(signals.ThumbImages(Window, []));
        Assert.All(signals.ThumbOf(Window)!.Buttons, button => Assert.Null(button.Picture));
        Assert.False(signals.ThumbImages(Window, []));

        // A list alone, taken away, leaves nothing of the window behind.
        var other = new AppSignals();
        other.ThumbImages(Window, pictures.Pictures);
        other.ThumbImages(Window, []);
        Assert.Equal(0, other.Count);
    }
}
