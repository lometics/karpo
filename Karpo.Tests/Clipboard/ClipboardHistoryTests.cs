// clipboard: what the clipboard history keeps and shows (Karpo.Dock\Clipboard; the backlog, item 13),
// held without the person's clipboard. Every test here stands a fake in for Windows
// (FakeClipboard: IClipboardSystem), so nothing is read from or written to the real clipboard or
// Windows' real history.
//
//   * the markers a program puts beside a secret keep it out, whatever else it carries, and a
//     marker whose value cannot be read counts as a no (ClipboardRules);
//   * Karpo's own history: the newest first, the same content once, 25 at most with the pinned
//     kept, "Clear all" keeping the pinned, pictures up to a 4K screenshot within a budget, text
//     up to a million characters (ClipboardRing);
//   * a copied picture made into a thumbnail: 24- and 32-bit, either way up, alpha or none
//     (DibPictures), and a text cut to the card's few lines (ClipText);
//   * which history the store shows when — Windows' while it is on and readable, Karpo's
//     otherwise, saying why — and what a click, a pin, a delete and "Clear all" ask of each.

using Karpo.Dock;

namespace Karpo.Tests.Clipboard;

public sealed class ClipboardHistoryTests
{
    // --- The markers --------------------------------------------------------------------------

    private static readonly Dictionary<string, (ClipMarkers Markers, bool Excluded)> Cases = new()
    {
        ["an ordinary copy"] = (ClipMarkers.None, false),
        ["ExcludeClipboardContentFromMonitorProcessing"] = (new ClipMarkers { ExcludeFromMonitor = true }, true),
        ["Clipboard Viewer Ignore"] = (new ClipMarkers { ViewerIgnore = true }, true),
        ["CanIncludeInClipboardHistory = 0"] = (new ClipMarkers { HasCanInclude = true, CanInclude = 0 }, true),
        ["CanIncludeInClipboardHistory = 1"] = (new ClipMarkers { HasCanInclude = true, CanInclude = 1 }, false),
        ["CanIncludeInClipboardHistory unreadable"] = (new ClipMarkers { HasCanInclude = true, CanInclude = null }, true),
        ["CanUploadToCloudClipboard = 0"] = (new ClipMarkers { HasCanUpload = true, CanUpload = 0 }, true),
        ["CanUploadToCloudClipboard = 1"] = (new ClipMarkers { HasCanUpload = true, CanUpload = 1 }, false),
        ["CanUploadToCloudClipboard unreadable"] = (new ClipMarkers { HasCanUpload = true, CanUpload = null }, true),
        ["both allowing"] = (new ClipMarkers { HasCanInclude = true, CanInclude = 1, HasCanUpload = true, CanUpload = 1 }, false),
        ["one allowing, one refusing"] = (new ClipMarkers { HasCanInclude = true, CanInclude = 1, HasCanUpload = true, CanUpload = 0 }, true),
        ["allowing, but excluded from monitors"] = (new ClipMarkers { ExcludeFromMonitor = true, HasCanInclude = true, CanInclude = 1 }, true),
    };

    public static TheoryData<string> Markers()
    {
        var data = new TheoryData<string>();
        foreach (var name in Cases.Keys)
        {
            data.Add(name);
        }
        return data;
    }

    /// <summary>
    /// The formats Windows reads to keep a secret out of a history keep it out of Karpo's: any that
    /// says no, and any that is there but cannot be read.
    /// </summary>
    [Theory]
    [MemberData(nameof(Markers))]
    public void TheMarkersKeepASecretOut(string what)
    {
        var (markers, excluded) = Cases[what];
        Assert.True(ClipboardRules.Excluded(markers) == excluded, $"{what}: excluded {ClipboardRules.Excluded(markers)}");
    }

    /// <summary>The names are Windows' own, word for word: a registered format is found by its name.</summary>
    [Fact]
    public void TheMarkersAreWindowsOwnNames()
    {
        Assert.Equal("ExcludeClipboardContentFromMonitorProcessing", ClipboardRules.ExcludeFromMonitor);
        Assert.Equal("CanIncludeInClipboardHistory", ClipboardRules.CanIncludeInHistory);
        Assert.Equal("CanUploadToCloudClipboard", ClipboardRules.CanUploadToCloud);
        Assert.Equal("Clipboard Viewer Ignore", ClipboardRules.ViewerIgnore);
    }

    /// <summary>A password copied with a marker is never kept, and leaves the history as it was.</summary>
    [Theory]
    [MemberData(nameof(Markers))]
    public void AMarkedCopyIsNeverKept(string what)
    {
        var (markers, excluded) = Cases[what];
        var ring = new ClipboardRing();
        ring.Add(Text("before"));
        var added = ring.Add(new ClipSnapshot(markers, "hunter2", Dib(2, 2, 32)));
        Assert.True(added != excluded, what);
        Assert.Equal(excluded ? ["before"] : ["hunter2", "before"], ring.Items.Select(item => item.Text));
        Assert.DoesNotContain(ring.Items, item => excluded && (item.Text == "hunter2" || item.Kind == ClipKind.Image));
    }

    // --- Karpo's own history --------------------------------------------------------------------

    private static ClipSnapshot Text(string text) => new(ClipMarkers.None, text, null);

    private static ClipSnapshot Picture(byte[] dib) => new(ClipMarkers.None, null, dib);

    [Fact]
    public void TheNewestIsFirstAndTheSameContentStandsOnce()
    {
        var ring = new ClipboardRing();
        Assert.True(ring.Add(Text("one")));
        Assert.True(ring.Add(Text("two")));
        Assert.True(ring.Add(Text("three")));
        Assert.Equal(["three", "two", "one"], ring.Items.Select(item => item.Text));

        // Copied again: to the top, the same item, not a second one.
        var id = ring.Items[2].Id;
        Assert.True(ring.Add(Text("one")));
        Assert.Equal(["one", "three", "two"], ring.Items.Select(item => item.Text));
        Assert.Equal(id, ring.Items[0].Id);

        // A picture is the same picture by its bytes.
        var dib = Dib(4, 3, 24);
        Assert.True(ring.Add(Picture(dib)));
        Assert.True(ring.Add(Text("four")));
        Assert.True(ring.Add(Picture((byte[])dib.Clone())));
        Assert.Equal(5, ring.Items.Count);
        Assert.Equal(ClipKind.Image, ring.Items[0].Kind);
    }

    /// <summary>Nothing to keep: no text and no picture, text that is only spaces, a picture past the most kept.</summary>
    [Fact]
    public void WhatIsNeitherTextNorAPictureIsNotKept()
    {
        var ring = new ClipboardRing();
        Assert.False(ring.Add(new ClipSnapshot(ClipMarkers.None, null, null)));
        Assert.False(ring.Add(Text("   \r\n\t")));
        Assert.False(ring.Add(Picture(new byte[ClipboardRules.MostPictureBytes + 1])));
        Assert.Empty(ring.Items);
        Assert.True(ring.Add(Picture(Dib(1, 1, 32))));
    }

    /// <summary>A copied picture of <paramref name="bytes"/> bytes, told apart from others by <paramref name="mark"/>.</summary>
    private static ClipSnapshot Sized(int bytes, byte mark = 1)
    {
        var dib = new byte[bytes];
        dib[0] = mark;
        return Picture(dib);
    }

    /// <summary>
    /// Regression: pictures past 4 MB were dropped, so a 1080p screenshot (about 8.3 MB as a DIB)
    /// never reached the history. Up to a 4K screenshot is kept whole; past the most, not at all.
    /// </summary>
    [Fact]
    public void ScreenshotsUpTo4KAreKept()
    {
        var ring = new ClipboardRing();
        Assert.True(ring.Add(Sized(40 + (1920 * 1080 * 4))));
        Assert.True(ring.Add(Sized(40 + (3840 * 2160 * 4), mark: 2)));
        Assert.Equal([40 + (3840 * 2160 * 4), 40 + (1920 * 1080 * 4)], ring.Items.Select(item => item.Dib!.Length));
        Assert.True(ring.Add(Sized(ClipboardRules.MostPictureBytes, mark: 3)));
        Assert.False(ring.Add(Sized(ClipboardRules.MostPictureBytes + 1, mark: 4)));
        Assert.True(ClipboardRules.MostPictureBytes >= 40 + (3840 * 2160 * 4));
    }

    /// <summary>
    /// The pictures kept stay under their budget together: past it the oldest unpinned picture goes
    /// first, a pinned one never, and the newest always stays, however large; text is not touched.
    /// </summary>
    [Fact]
    public void PicturesStayWithinTheirBudget()
    {
        var ring = new ClipboardRing(pictureBudget: 1000);
        Assert.True(ring.Add(Sized(400, mark: 1)));
        var pinned = ring.Items[0].Id;
        ring.Pin(pinned, true);
        Assert.True(ring.Add(Text("words")));
        Assert.True(ring.Add(Sized(400, mark: 2)));
        Assert.True(ring.Add(Sized(400, mark: 3)));
        // 1200 over 1000: the oldest unpinned picture (2) goes; the pinned one (1) stays.
        Assert.Equal([3, 0, 1], ring.Items.Select(item => (int)(item.Dib?[0] ?? 0)));
        Assert.Equal(800, ring.PictureBytes);

        // One larger than the whole budget is kept, the newest; every unpinned picture before it goes.
        Assert.True(ring.Add(Sized(5000, mark: 4)));
        Assert.Equal([4, 0, 1], ring.Items.Select(item => (int)(item.Dib?[0] ?? 0)));
        Assert.Contains(ring.Items, item => item.Id == pinned);
    }

    /// <summary>
    /// Text up to a million characters is kept whole; a longer one is not kept at all, since one cut
    /// short would paste something other than what was copied.
    /// </summary>
    [Fact]
    public void TextPastTheMostIsNotKept()
    {
        var ring = new ClipboardRing();
        Assert.True(ring.Add(Text(new string('a', ClipboardRules.MostTextChars))));
        Assert.False(ring.Add(Text(new string('b', ClipboardRules.MostTextChars + 1))));
        Assert.Equal(ClipboardRules.MostTextChars, Assert.Single(ring.Items).Text.Length);
    }

    /// <summary>Text is kept as copied, its spaces and lines and all; only what a card shows is cut.</summary>
    [Fact]
    public void TextIsKeptWhole()
    {
        var ring = new ClipboardRing();
        var text = "  first line\r\n\tsecond\r\n\r\nfourth\n" + new string('x', 2000);
        Assert.True(ring.Add(Text(text)));
        Assert.Equal(text, ring.Items[0].Text);
    }

    [Fact]
    public void TwentyFiveAtMostAndThePinnedStay()
    {
        var ring = new ClipboardRing();
        ring.Add(Text("pinned"));
        Assert.True(ring.Pin(ring.Items[0].Id, true));
        for (var i = 0; i < 40; i++)
        {
            ring.Add(Text($"item {i}"));
        }
        Assert.Equal(ClipboardRing.Capacity, ring.Items.Count);
        Assert.Equal("item 39", ring.Items[0].Text);
        // The oldest unpinned went; the pinned one, older than all of them, stays.
        Assert.Contains(ring.Items, item => item.Text == "pinned" && item.Pinned);
        Assert.DoesNotContain(ring.Items, item => item.Text == "item 0");
        Assert.Equal("item 16", ring.Items[^2].Text);
    }

    [Fact]
    public void ClearAllKeepsThePinnedAndDeleteTakesOne()
    {
        var ring = new ClipboardRing();
        foreach (var text in new[] { "a", "b", "c", "d" })
        {
            ring.Add(Text(text));
        }
        var b = ring.Items.Single(item => item.Text == "b").Id;
        var d = ring.Items.Single(item => item.Text == "d").Id;
        Assert.True(ring.Pin(b, true));
        Assert.True(ring.Delete(d));
        Assert.False(ring.Delete(d));
        Assert.Equal(["c", "b", "a"], ring.Items.Select(item => item.Text));
        ring.Clear();
        Assert.Equal(["b"], ring.Items.Select(item => item.Text));
        Assert.True(ring.Pin(b, false));
        ring.Clear();
        Assert.Empty(ring.Items);
        Assert.False(ring.Pin(b, true));
        Assert.Null(ring.Find(b));
    }

    /// <summary>Pinned, then copied again: still pinned, at the top.</summary>
    [Fact]
    public void APinnedItemCopiedAgainStaysPinned()
    {
        var ring = new ClipboardRing();
        ring.Add(Text("keep"));
        ring.Pin(ring.Items[0].Id, true);
        ring.Add(Text("other"));
        ring.Add(Text("keep"));
        Assert.Equal("keep", ring.Items[0].Text);
        Assert.True(ring.Items[0].Pinned);
        Assert.Equal(2, ring.Items.Count);
    }

    // --- A picture's thumbnail ------------------------------------------------------------------

    /// <summary>
    /// A packed DIB of <paramref name="width"/> by <paramref name="height"/> at <paramref name="bits"/>,
    /// bottom-up unless <paramref name="topDown"/>, each pixel <paramref name="colour"/> of its row
    /// and column (BGRA; alpha only at 32 bits).
    /// </summary>
    internal static byte[] Dib(int width, int height, int bits, bool topDown = false, Func<int, int, (byte B, byte G, byte R, byte A)>? colour = null)
    {
        colour ??= (x, y) => ((byte)(x * 10), (byte)(y * 10), 200, 255);
        var stride = ((width * bits) + 31) / 32 * 4;
        var dib = new byte[40 + (stride * height)];
        BitConverter.GetBytes(40).CopyTo(dib, 0);
        BitConverter.GetBytes(width).CopyTo(dib, 4);
        BitConverter.GetBytes(topDown ? -height : height).CopyTo(dib, 8);
        BitConverter.GetBytes((ushort)1).CopyTo(dib, 12);
        BitConverter.GetBytes((ushort)bits).CopyTo(dib, 14);
        for (var y = 0; y < height; y++)
        {
            var row = 40 + ((topDown ? y : height - 1 - y) * stride);
            for (var x = 0; x < width; x++)
            {
                var (b, g, r, a) = colour(x, y);
                var at = row + (x * bits / 8);
                dib[at] = b;
                dib[at + 1] = g;
                dib[at + 2] = r;
                if (bits == 32)
                {
                    dib[at + 3] = a;
                }
            }
        }
        return dib;
    }

    private static (byte B, byte G, byte R, byte A) At(Pixels pixels, int x, int y)
    {
        var at = ((y * pixels.Width) + x) * 4;
        return (pixels.Data[at], pixels.Data[at + 1], pixels.Data[at + 2], pixels.Data[at + 3]);
    }

    /// <summary>The picture's top row is the thumbnail's top row, whichever way up the DIB is stored.</summary>
    [Theory]
    [InlineData(24, false)]
    [InlineData(24, true)]
    [InlineData(32, false)]
    [InlineData(32, true)]
    public void APictureKeepsItsWayUp(int bits, bool topDown)
    {
        var dib = Dib(3, 2, bits, topDown, (x, y) => y == 0 ? ((byte)0, (byte)0, (byte)255, (byte)255) : ((byte)255, (byte)0, (byte)0, (byte)255));
        var pixels = DibPictures.Thumbnail(dib, 100, 100)!;
        Assert.Equal((3, 2), (pixels.Width, pixels.Height));
        Assert.Equal(((byte)0, (byte)0, (byte)255, (byte)255), At(pixels, 1, 0));
        Assert.Equal(((byte)255, (byte)0, (byte)0, (byte)255), At(pixels, 1, 1));
    }

    /// <summary>
    /// Fitted inside the room, its proportions kept and never larger than it is; each pixel the
    /// average of the block it stands for.
    /// </summary>
    [Fact]
    public void APictureIsFittedAndAveraged()
    {
        var dib = Dib(400, 100, 24, colour: (x, _) => x % 2 == 0 ? ((byte)0, (byte)0, (byte)0, (byte)255) : ((byte)200, (byte)100, (byte)50, (byte)255));
        var pixels = DibPictures.Thumbnail(dib, 200, 200)!;
        Assert.Equal((200, 50), (pixels.Width, pixels.Height));
        Assert.Equal(((byte)100, (byte)50, (byte)25, (byte)255), At(pixels, 10, 10));
        var small = DibPictures.Thumbnail(Dib(20, 10, 24), 200, 200)!;
        Assert.Equal((20, 10), (small.Width, small.Height));
    }

    /// <summary>
    /// A 32-bit picture with alpha is premultiplied as Direct2D takes it; one whose alpha is zero
    /// everywhere has none and is opaque, as Windows draws it.
    /// </summary>
    [Fact]
    public void AlphaIsPremultipliedOrAbsent()
    {
        var half = DibPictures.Thumbnail(Dib(1, 1, 32, colour: (_, _) => (200, 100, 50, 128)), 10, 10)!;
        Assert.Equal(((byte)100, (byte)50, (byte)25, (byte)128), At(half, 0, 0));
        var none = DibPictures.Thumbnail(Dib(1, 1, 32, colour: (_, _) => (200, 100, 50, 0)), 10, 10)!;
        Assert.Equal(((byte)200, (byte)100, (byte)50, (byte)255), At(none, 0, 0));
    }

    /// <summary>What cannot be read has no thumbnail, rather than a wrong one: short, palette, compressed, empty.</summary>
    [Fact]
    public void WhatCannotBeReadHasNoThumbnail()
    {
        Assert.Null(DibPictures.Thumbnail([], 10, 10));
        Assert.Null(DibPictures.Thumbnail(new byte[39], 10, 10));
        var palette = Dib(2, 2, 24);
        BitConverter.GetBytes((ushort)8).CopyTo(palette, 14);
        Assert.Null(DibPictures.Thumbnail(palette, 10, 10));
        var compressed = Dib(2, 2, 32);
        BitConverter.GetBytes(1).CopyTo(compressed, 16);
        Assert.Null(DibPictures.Thumbnail(compressed, 10, 10));
        var cut = Dib(8, 8, 32)[..60];
        Assert.Null(DibPictures.Thumbnail(cut, 10, 10));
    }

    // --- What a card shows of a text -------------------------------------------------------------

    [Theory]
    [InlineData("hello", "hello")]
    [InlineData("\r\n\r\n   hello  \r\n  world  ", "hello\n  world")]
    [InlineData("a\rb\nc\r\nd\ne", "a\nb\nc")]
    [InlineData("tab\there", "tab    here")]
    [InlineData("   ", "")]
    public void ACardShowsTheFirstLines(string text, string shown) => Assert.Equal(shown, ClipText.Shown(text));

    [Fact]
    public void ACardIsHandedNoMoreThanItCanShow() =>
        Assert.Equal(ClipText.MostChars, ClipText.Shown(new string('x', 10_000)).Length);

    /// <summary>
    /// What a card is made from is a few lines' worth from the first word on, however long the text
    /// and however many blank lines it starts with — a million characters are not split into lines
    /// to show three of them, and Windows' items hold no more than that in memory.
    /// </summary>
    [Fact]
    public void ACardIsMadeFromTheStartOfTheTextAlone()
    {
        var huge = new string('\n', 5000) + "  first\nsecond\n" + new string('y', 1_000_000);
        var held = ClipText.Held(huge);
        Assert.Equal(ClipText.HeldChars, held.Length);
        Assert.StartsWith("first\nsecond", held, StringComparison.Ordinal);
        Assert.Equal("first\nsecond\n" + new string('y', ClipText.MostChars - 13), ClipText.Shown(huge));
        Assert.Same("short", ClipText.Held("short"));
    }

    // --- The store, over a fake Windows ---------------------------------------------------------

    /// <summary>Windows as a test wants it: a history on or off, readable or not, and a clipboard of made-up snapshots.</summary>
    internal sealed class FakeClipboard : IClipboardSystem
    {
        public bool HistoryEnabled { get; set; }

        /// <summary>What Windows' history answers; null for a read Windows refused.</summary>
        public List<ClipView>? History { get; set; } = [];

        /// <summary>What the clipboard holds at the next read.</summary>
        public Queue<ClipSnapshot?> Clipboard { get; } = new();

        public List<string> Restored { get; } = [];

        public List<string> Deleted { get; } = [];

        public List<ClipEntry> Put { get; } = [];

        public int Cleared { get; private set; }

        public int Reads { get; private set; }

        public Action? Listening { get; private set; }

        public List<bool> Listens { get; } = [];

        /// <summary>Set: Windows' clipboard service never answers.</summary>
        public bool Hangs { get; set; }

        public Task<IReadOnlyList<ClipView>?> ReadHistoryAsync(int mostWidth, int mostHeight) =>
            Hangs ? new TaskCompletionSource<IReadOnlyList<ClipView>?>().Task
                : Task.FromResult<IReadOnlyList<ClipView>?>(History is null ? null : [.. History]);

        public bool Restore(string id)
        {
            Restored.Add(id);
            return true;
        }

        public bool Delete(string id)
        {
            Deleted.Add(id);
            History?.RemoveAll(item => item.Id == id);
            return true;
        }

        public bool ClearHistory()
        {
            Cleared++;
            History?.RemoveAll(item => !item.Pinned);
            return true;
        }

        public ClipSnapshot? Read()
        {
            Reads++;
            return Clipboard.TryDequeue(out var snapshot) ? snapshot : null;
        }

        bool IClipboardSystem.Put(ClipEntry entry)
        {
            Put.Add(entry);
            return true;
        }

        public void Listen(Action? changed)
        {
            Listening = changed;
            Listens.Add(changed is not null);
        }

        /// <summary>The clipboard changes to <paramref name="snapshot"/>, as WM_CLIPBOARDUPDATE says.</summary>
        public void Copy(ClipSnapshot snapshot)
        {
            Clipboard.Enqueue(snapshot);
            Listening?.Invoke();
        }
    }

    private static async Task Copied(ClipboardStore store, FakeClipboard fake, ClipSnapshot snapshot)
    {
        fake.Copy(snapshot);
        await store.Idle();
    }

    /// <summary>
    /// With Windows' history off, Karpo keeps its own: every copy heard while a widget stands, the
    /// marked ones left out, shown newest first, with a line saying why it is Karpo's.
    /// </summary>
    [Fact]
    public async Task WithWindowsHistoryOffKarpoKeepsItsOwn()
    {
        var fake = new FakeClipboard { HistoryEnabled = false };
        var store = new ClipboardStore(fake);
        store.Listen(true);
        Assert.Equal([true], fake.Listens);

        await Copied(store, fake, Text("first"));
        await Copied(store, fake, new ClipSnapshot(new ClipMarkers { HasCanInclude = true, CanInclude = 0 }, "password", null));
        await Copied(store, fake, new ClipSnapshot(new ClipMarkers { ExcludeFromMonitor = true }, "secret", null));
        await Copied(store, fake, Picture(Dib(40, 20, 32)));
        await Copied(store, fake, Text("second"));
        store.Refresh();

        Assert.Equal(ClipSource.Karpo, store.Source);
        Assert.Equal(ClipboardStore.HistoryOffWords, store.Note);
        Assert.True(store.HistoryOff);
        Assert.True(store.CanPin);
        Assert.Equal([ClipKind.Text, ClipKind.Image, ClipKind.Text], store.Items.Select(item => item.Kind));
        Assert.Equal("second", store.Items[0].Text);
        Assert.NotNull(store.Items[1].Picture);
        Assert.Equal((40, 20), (store.Items[1].Picture!.Width, store.Items[1].Picture!.Height));
        Assert.DoesNotContain(store.Kept(), item => item.Text is "password" or "secret");

        store.Listen(false);
        Assert.Equal([true, false], fake.Listens);
        Assert.Null(fake.Listening);
    }

    /// <summary>
    /// With Windows' history on, Windows has every copy and Karpo reads none of them; the flyout
    /// shows Windows' own, read when it opens, with no pin, which Windows gives a program no way to.
    /// </summary>
    [Fact]
    public async Task WithWindowsHistoryOnTheFlyoutShowsWindowsOwn()
    {
        var fake = new FakeClipboard
        {
            HistoryEnabled = true,
            History = [new ClipView("w1", ClipKind.Text, "from Windows", null, false), new ClipView("w2", ClipKind.Image, "", null, false)],
        };
        var store = new ClipboardStore(fake);
        store.Listen(true);
        await Copied(store, fake, Text("not read"));
        Assert.Equal(0, fake.Reads);
        Assert.Empty(store.Kept());

        store.Refresh();
        Assert.True(store.Loading);
        await store.Idle();
        Assert.False(store.Loading);
        Assert.Equal(ClipSource.Windows, store.Source);
        Assert.Null(store.Note);
        Assert.False(store.CanPin);
        Assert.Equal(["w1", "w2"], store.Items.Select(item => item.Id));

        // A click puts that item back through Windows' own history.
        Assert.True(store.Choose(store.Items[1]));
        Assert.Equal(["w2"], fake.Restored);
        Assert.Empty(fake.Put);

        // Delete takes it out of Windows' history, and out of what shows.
        store.Delete(store.Items[0]);
        Assert.Equal(["w1"], fake.Deleted);
        Assert.Equal(["w2"], store.Items.Select(item => item.Id));

        // Pin does nothing to Windows' history.
        store.Pin(store.Items[0], true);
        Assert.False(store.Items[0].Pinned);

        // Clear all is Windows' own, and the history is read again after it.
        store.ClearAll();
        await store.Idle();
        Assert.Equal(1, fake.Cleared);
        Assert.Empty(store.Items);
    }

    /// <summary>
    /// Windows' history on but refused: Karpo's own shows, saying so, and from then on Karpo keeps
    /// what it hears, until Windows' can be read again.
    /// </summary>
    [Fact]
    public async Task WhenWindowsWillNotGiveItsHistoryKarpoShowsItsOwn()
    {
        var fake = new FakeClipboard { HistoryEnabled = true, History = null };
        var store = new ClipboardStore(fake);
        store.Listen(true);
        store.Refresh();
        await store.Idle();
        Assert.Equal(ClipSource.Karpo, store.Source);
        Assert.Equal(ClipboardStore.HistoryUnread, store.Note);

        await Copied(store, fake, Text("kept now"));
        Assert.Equal(["kept now"], store.Items.Select(item => item.Text));

        fake.History = [new ClipView("w9", ClipKind.Text, "Windows again", null, false)];
        store.Refresh();
        await store.Idle();
        Assert.Equal(ClipSource.Windows, store.Source);
        Assert.Equal(["w9"], store.Items.Select(item => item.Id));
    }

    /// <summary>
    /// Regression: reading Windows' history had no time limit, so a service that never answered left
    /// the flyout "Reading…" for ever. Past the wait, Karpo's own shows, saying Windows' could not be read.
    /// </summary>
    [Fact]
    public async Task WhenWindowsNeverAnswersKarpoShowsItsOwn()
    {
        var fake = new FakeClipboard { HistoryEnabled = true, Hangs = true };
        var store = new ClipboardStore(fake) { HistoryWait = TimeSpan.FromMilliseconds(100) };
        store.Listen(true);
        store.Refresh();
        Assert.True(store.Loading);
        await store.Idle().WaitAsync(TimeSpan.FromSeconds(10));
        Assert.False(store.Loading);
        Assert.Equal(ClipSource.Karpo, store.Source);
        Assert.Equal(ClipboardStore.HistoryUnread, store.Note);

        // Heard meanwhile, as where Windows refused it.
        await Copied(store, fake, Text("kept now"));
        Assert.Equal(["kept now"], store.Items.Select(item => item.Text));
    }

    // --- Pasting a chosen item -----------------------------------------------------------------

    /// <summary>
    /// A chosen item is pasted, as Win+V pastes it, only into a window that is still there, in front
    /// again, not Karpo's, not the shell's, not a console's, not an elevated program's and not
    /// focused on a password box; anywhere else it is left on the clipboard alone.
    /// </summary>
    [Fact]
    public void AChosenItemIsPastedOnlyWhereItIsSafe()
    {
        var notepad = new PasteTarget(Exists: true, Ours: false, Shell: false, Password: false, InFront: true);
        Assert.True(ClipboardPaste.Judge(notepad));
        Assert.False(ClipboardPaste.Judge(default));
        Assert.False(ClipboardPaste.Judge(notepad with { Exists = false }));
        Assert.False(ClipboardPaste.Judge(notepad with { Ours = true }));
        Assert.False(ClipboardPaste.Judge(notepad with { Shell = true }));
        Assert.False(ClipboardPaste.Judge(notepad with { Password = true }));
        Assert.False(ClipboardPaste.Judge(notepad with { InFront = false }));
        Assert.False(ClipboardPaste.Judge(notepad with { Console = true }));
        Assert.False(ClipboardPaste.Judge(notepad with { Higher = true }));
    }

    /// <summary>
    /// Consoles are known by their window's class: conhost's, Windows Terminal's, and the terminals
    /// people use instead. There Ctrl+V is as often the program's own key as a paste, so the item is
    /// left on the clipboard.
    /// </summary>
    [Theory]
    [InlineData("ConsoleWindowClass", true)]
    [InlineData("CASCADIA_HOSTING_WINDOW_CLASS", true)]
    [InlineData("mintty", true)]
    [InlineData("PuTTY", true)]
    [InlineData("VirtualConsoleClass", true)]
    [InlineData("Notepad", false)]
    [InlineData("Chrome_WidgetWin_1", false)]
    [InlineData("", false)]
    public void ConsolesAreKnownByTheirClass(string className, bool console) =>
        Assert.Equal(console, ClipboardPaste.IsConsole(className));

    /// <summary>
    /// An elevated program hears no input of a Karpo that is not: Windows drops it without a word. A
    /// program whose integrity cannot be read counts as above, unless Karpo is elevated itself.
    /// </summary>
    [Theory]
    [InlineData(0x2000u, 0x2000u, false)]
    [InlineData(0x1000u, 0x2000u, false)]
    [InlineData(0x3000u, 0x2000u, true)]
    [InlineData(0x4000u, 0x2000u, true)]
    [InlineData(0x3000u, 0x3000u, false)]
    [InlineData(0x4000u, 0x3000u, true)]
    [InlineData(null, 0x2000u, true)]
    [InlineData(null, 0x3000u, false)]
    public void AnElevatedProgramIsAbove(uint? target, uint own, bool above) =>
        Assert.Equal(above, ClipboardPaste.Above(target, own));

    /// <summary>
    /// The paste waits for the window's focus to settle: only two reads in a row that find it in front
    /// with the same control focused paste — a password box that takes the focus a moment after the
    /// window came back is seen, and nothing is pasted into it.
    /// </summary>
    [Fact]
    public void ThePasteWaitsForTheFocusToSettle()
    {
        var front = new PasteTarget(Exists: true, Ours: false, Shell: false, Password: false, InFront: true, Focus: 0x100);

        var wait = new PasteWait();
        Assert.Equal(PasteStep.Wait, wait.Next(front with { InFront = false, Focus = 0 }));
        Assert.Equal(PasteStep.Wait, wait.Next(front with { Focus = 0 }));
        Assert.Equal(PasteStep.Wait, wait.Next(front));
        Assert.Equal(PasteStep.Paste, wait.Next(front));

        // The window itself has the focus first, then its password box takes it.
        var login = new PasteWait();
        Assert.Equal(PasteStep.Wait, login.Next(front with { Focus = 0x100 }));
        Assert.Equal(PasteStep.Wait, login.Next(front with { Focus = 0x200, Password = true }));
        Assert.Equal(PasteStep.Leave, login.Next(front with { Focus = 0x200, Password = true }));

        // A focus that moves on every read never settles: given up after the most reads.
        var restless = new PasteWait();
        var steps = Enumerable.Range(1, PasteWait.MostReads).Select(read => restless.Next(front with { Focus = read })).ToList();
        Assert.All(steps.Take(PasteWait.MostReads - 1), step => Assert.Equal(PasteStep.Wait, step));
        Assert.Equal(PasteStep.Leave, steps[^1]);

        // Out of the front again before it settled: it waits on, and the focus counts afresh.
        var away = new PasteWait();
        Assert.Equal(PasteStep.Wait, away.Next(front));
        Assert.Equal(PasteStep.Wait, away.Next(front with { InFront = false }));
        Assert.Equal(PasteStep.Wait, away.Next(front));
        Assert.Equal(PasteStep.Paste, away.Next(front));
    }

    /// <summary>
    /// A Store app — Calculator, Settings — is a frame in front and the app's CoreWindow inside it, on
    /// another thread: the frame's thread reads no focus at all, which used to leave the item only
    /// copied. Its focus is the CoreWindow's thread's, else the CoreWindow itself while it or its
    /// frame is in front; and two reads of that settle, so it pastes.
    /// </summary>
    [Fact]
    public void AStoreAppsFocusIsItsCoreWindow()
    {
        const nint frame = 0x10, core = 0x20, inner = 0x21, other = 0x99, own = 0x11;
        // The window tree: the frame holds its own control and the CoreWindow; the CoreWindow holds one.
        static bool Within(nint parent, nint child) =>
            (parent, child) is (frame, own) or (frame, core) or (frame, inner) or (core, inner);

        // Any other window: its own control, or nothing; never another window's.
        Assert.Equal(own, ClipboardPaste.FocusOf(frame, own, 0, 0, frame, Within));
        Assert.Equal(0, ClipboardPaste.FocusOf(frame, 0, 0, 0, frame, Within));
        Assert.Equal(0, ClipboardPaste.FocusOf(frame, other, 0, 0, frame, Within));
        // A Store app: the frame's thread reads none, the CoreWindow's thread reads its focus.
        Assert.Equal(core, ClipboardPaste.FocusOf(frame, 0, core, core, frame, Within));
        Assert.Equal(inner, ClipboardPaste.FocusOf(frame, 0, core, inner, frame, Within));
        // Neither thread reads one: the CoreWindow, while it or its frame is in front, and none otherwise.
        Assert.Equal(core, ClipboardPaste.FocusOf(frame, 0, core, 0, frame, Within));
        Assert.Equal(core, ClipboardPaste.FocusOf(frame, 0, core, 0, core, Within));
        Assert.Equal(0, ClipboardPaste.FocusOf(frame, 0, core, 0, other, Within));
        // Another window's control read on the CoreWindow's thread is not the app's.
        Assert.Equal(core, ClipboardPaste.FocusOf(frame, 0, core, other, frame, Within));

        // And the wait pastes on the second read of it, as for any window — no 300 ms given up.
        var calculator = new PasteTarget(Exists: true, Ours: false, Shell: false, Password: false, InFront: true,
            Focus: ClipboardPaste.FocusOf(frame, 0, core, 0, frame, Within));
        var wait = new PasteWait();
        Assert.Equal(PasteStep.Wait, wait.Next(calculator));
        Assert.Equal(PasteStep.Paste, wait.Next(calculator));
        // The exclusions stand: a password box that says so, an elevated frame, are never pasted into.
        var guarded = new PasteWait();
        Assert.Equal(PasteStep.Wait, guarded.Next(calculator with { Password = true }));
        Assert.Equal(PasteStep.Leave, guarded.Next(calculator with { Password = true }));
        Assert.Equal(PasteStep.Leave, new PasteWait().Next(calculator with { Higher = true }));
    }

    /// <summary>A console, an elevated program, Karpo, the shell, a window gone: left at once, whatever the focus does.</summary>
    [Fact]
    public void SomeWindowsAreNeverWaitedFor()
    {
        var front = new PasteTarget(Exists: true, Ours: false, Shell: false, Password: false, InFront: true, Focus: 0x100);

        Assert.Equal(PasteStep.Leave, new PasteWait().Next(front with { Console = true }));
        Assert.Equal(PasteStep.Leave, new PasteWait().Next(front with { Higher = true }));
        Assert.Equal(PasteStep.Leave, new PasteWait().Next(front with { Ours = true }));
        Assert.Equal(PasteStep.Leave, new PasteWait().Next(front with { Shell = true }));
        Assert.Equal(PasteStep.Leave, new PasteWait().Next(default));
    }

    [Theory]
    [InlineData(@"C:\Program Files\Karpo\Karpo.exe", true)]
    [InlineData(@"C:\Users\me\AppData\Local\Karpo\KarpoSettings.exe", true)]
    [InlineData("karpo.EXE", true)]
    [InlineData(@"C:\Windows\notepad.exe", false)]
    [InlineData(@"C:\Games\Karpov.exe", false)]
    [InlineData(null, false)]
    public void KarposOwnProgramsAreKnownByTheirExe(string? exe, bool karpo) =>
        Assert.Equal(karpo, ClipboardPaste.IsKarpo(exe));

    /// <summary>
    /// Karpo's own: a click puts the item itself back on the clipboard — its text, or its picture's
    /// bytes — and pin, delete and "Clear all" act on Karpo's history alone.
    /// </summary>
    [Fact]
    public async Task KarposOwnIsChosenPinnedDeletedAndCleared()
    {
        var fake = new FakeClipboard { HistoryEnabled = false };
        var store = new ClipboardStore(fake);
        store.Listen(true);
        var dib = Dib(8, 8, 24);
        await Copied(store, fake, Picture(dib));
        await Copied(store, fake, Text("words"));
        await Copied(store, fake, Text("more"));
        store.Refresh();

        Assert.True(store.Choose(store.Items[2]));
        Assert.True(store.Choose(store.Items[1]));
        Assert.Equal([ClipKind.Image, ClipKind.Text], fake.Put.Select(entry => entry.Kind));
        Assert.Equal(dib, fake.Put[0].Dib);
        Assert.Equal("words", fake.Put[1].Text);
        Assert.Empty(fake.Restored);

        store.Pin(store.Items[1], true);
        Assert.True(store.Items[1].Pinned);
        store.Delete(store.Items[0]);
        Assert.Equal(["words", ""], store.Items.Select(item => item.Text));
        store.ClearAll();
        Assert.Equal(["words"], store.Items.Select(item => item.Text));
        Assert.Equal(0, fake.Cleared);
        Assert.Empty(fake.Deleted);
    }

    /// <summary>A clipboard that could not be opened is no copy: nothing kept, nothing shown.</summary>
    [Fact]
    public async Task AClipboardThatCannotBeOpenedKeepsNothing()
    {
        var fake = new FakeClipboard { HistoryEnabled = false };
        var store = new ClipboardStore(fake);
        store.Listen(true);
        fake.Clipboard.Enqueue(null);
        fake.Listening!.Invoke();
        await store.Idle();
        store.Refresh();
        Assert.Empty(store.Items);
    }
}
