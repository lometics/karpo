// The media is a preset (docs\widgets.md §96): what a click on the now-playing entry gave — its
// flyout: where it plays, the art, the track and the artist, the timeline, previous, play or pause
// and next, shuffle and repeat, the apps playing to pick from — is the media preset's folder: the
// now-playing line, which has all of it, alone (the person's, 2026-10-11), under the now-playing face:
// the art and the track the entry was, painted again in place as the session's news arrives.
//
// Every track here is a stand-in (MediaIsland.Stand): no session of this PC's is read, and nothing
// is ever played, paused, skipped, shuffled or repeated.
//
// Pictured beside the flyout it replaces (rendered before it went: quick-media-*.png, and the
// entry's media-bars-*.png): media-preset-<dark|light>.png at 150 %, the face on the bar,
// media-face-<dark|light>.png, and the line with shuffle, repeat and the picker,
// list-media-source-modes-*.png (ListLinesControlsTests).

using System.Numerics;
using System.Text.Json;
using Karpo.Dock;
using Karpo.Dock.QuickSettings;
using Karpo.Dock.QuickSettings.Services;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using Karpo.Tests.QuickSettings;
using TerraFX.Interop.DirectX;
using static Karpo.Tests.Folders.ListFormPictures;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Folders;

public sealed unsafe class MediaPresetTests
{
    private static readonly PresetMachine Machine = new(HasBattery: false, FixedDrives: []);

    /// <summary>The media's preset folder, as the add list makes it.</summary>
    private static BarItem Holder() => Presets.Folder(Presets.Find(Presets.MediaId)!, Machine);

    /// <summary>The preset's folder set to stay while nothing plays: its face drawn paused and idle at rest (§319).</summary>
    private static BarItem Kept()
    {
        var holder = Holder();
        holder.Folder!.Face.Set(FolderFaces.NowPlaying.Option(NowPlayingFace.HideIdle)!, false);
        return holder;
    }

    private static readonly MediaEntry[] TwoApps =
    [
        new("Spotify.exe", "Spotify", true),
        new("msedge.exe", "Microsoft Edge", false),
    ];

    /// <summary>A track whose app has shuffle and repeat, and another app playing beside it.</summary>
    private static MediaStand Full(bool playing = true) => new(
        AppName: "Spotify", IsPlaying: playing, Art: QuickFlyoutOffscreenTests.Cover(),
        Position: TimeSpan.FromSeconds(123), Duration: TimeSpan.FromSeconds(272), CanSeek: true,
        CanShuffle: true, CanRepeat: true, Sessions: TwoApps);

    /// <summary>The preset's folder over <paramref name="stand"/>, never this PC's sessions.</summary>
    private static FolderIsland Made(Gfx gfx, MediaStand stand, BarItem? holder = null, string? key = null)
    {
        holder ??= Holder();
        MediaIsland.Stand = stand;
        try
        {
            return new FolderIsland(key ?? Config.IslandKey(null, holder.Id), holder, gfx, new Config());
        }
        finally
        {
            MediaIsland.Stand = null;
        }
    }

    /// <summary>A now-playing line on its own over <paramref name="stand"/>; <paramref name="stays"/>: set to stay while nothing plays (§327).</summary>
    private static MediaIsland Line(Gfx gfx, MediaStand stand, bool stays = false)
    {
        var item = ListKind.Media.New();
        item.Id = "media";
        // Its whole widget (§432), so a box of any height up to it is drawn and hit as given.
        item.Size = LineSize.Full;
        item.HideIdle = stays ? false : null;
        MediaIsland.Stand = stand;
        try
        {
            return new MediaIsland(Config.IslandKey(null, item.Id), item, gfx, new Config());
        }
        finally
        {
            MediaIsland.Stand = null;
        }
    }

    private static TileStyle LineStyle(bool dark = true)
    {
        var look = LookFor(dark);
        return FolderFlyout.StyleFor(1.5f, look, FlyoutTheme.For(look));
    }

    private static D2D_RECT_F LineBox(int rows, in TileStyle style) => PopupPaint.Rect(0, 0, FolderFlyout.ListWidth(1.5f), LineRows.Px(rows, style));

    private static Vector2 Middle(D2D_RECT_F box) => new((box.left + box.right) / 2, (box.top + box.bottom) / 2);

    private static IslandStyle BarStyle(int height, bool vertical = false, uint dpi = 96, bool dark = true, float text = 1f)
    {
        var config = new Config { Height = height };
        return new IslandStyle(dpi / 96f, vertical, Layout.CellPx(dpi, config), Layout.IconPx(dpi, config), LookFor(dark, text));
    }

    // --- The preset ------------------------------------------------------------------------------------

    /// <summary>
    /// The quick settings' media group is the media preset: the now-playing line at four rows — the
    /// whole widget, the flyout's timeline, shuffle, repeat and picker on it — under the now-playing
    /// face, whose options are the entry's.
    /// </summary>
    [Fact]
    public void ThePresetIsTheNowPlayingLineUnderTheFace()
    {
        var preset = Presets.Find(Presets.MediaId)!;
        Assert.Equal("mediaGroup", Presets.MediaId);
        Assert.Equal(FolderFaces.NowPlayingId, preset.Face!.Kind);
        Assert.Equal("media", preset.Items[0].Kind);
        Assert.Equal(((LineHeight?)null, (LineSize?)LineSize.Full), (preset.Items[0].Height, preset.Items[0].Size));

        var folder = Holder().Folder!;
        Assert.Same(FolderFaces.NowPlaying, folder.Face.Info);
        Assert.Equal(WidgetInfos.Media.Glyph, folder.Face.Info.Glyph);
        Assert.Equal([NowPlayingFace.Art, NowPlayingFace.Width, NowPlayingFace.HideIdle], folder.Face.Info.Options.Select(option => option.Key));
        // The defaults: the art shown, Medium (§503), and the face off the bar while nothing plays (§319).
        Assert.True(NowPlayingFace.ShowsArt(folder.Face));
        Assert.Equal(NowPlayingFace.Medium, NowPlayingFace.WidthOf(folder.Face));
        Assert.True(NowPlayingFace.HidesIdle(folder.Face));
        Assert.Equal(ListKind.Media, folder.Cells[0].Island.List);
    }

    /// <summary>
    /// "Hide it while nothing plays" is on where a file says nothing — every face from before the
    /// default changed (§319) — and off only where a file says false; nothing is written for a face
    /// never set, so no file is changed by reading it.
    /// </summary>
    [Fact]
    public void HidingWhileNothingPlaysIsOnUnlessAFileSaysOff()
    {
        FolderFace Read(string options) =>
            JsonSerializer.Deserialize<FolderFace>($$"""{ "kind": "nowPlaying", "options": {{options}} }""", ConfigJson.Default.FolderFace)!;
        Assert.True(NowPlayingFace.HidesIdle(Read("{}")));
        Assert.True(NowPlayingFace.HidesIdle(Read("""{ "hideIdle": true }""")));
        Assert.False(NowPlayingFace.HidesIdle(Read("""{ "hideIdle": false }""")));
        var unset = Read("""{ "art": false }""");
        Assert.DoesNotContain(NowPlayingFace.HideIdle, JsonSerializer.Serialize(unset, ConfigJson.Default.FolderFace));
    }

    /// <summary>The entry is gone from the bar, its flyout and its options with it: the line is all there is.</summary>
    [Fact]
    public void TheEntryAndItsFlyoutAreGone()
    {
        Assert.Null(BarEntries.Find("media"));
        Assert.NotNull(ListWidgets.Find("media"));
        Assert.DoesNotContain("Media", Enum.GetNames<BarKind>());
        Assert.DoesNotContain("Media", Enum.GetNames<QuickKind>());
        Assert.DoesNotContain("Media", Enum.GetNames<WidgetOptionsKind>());
        Assert.True(Config.Shape > 30);
    }

    /// <summary>
    /// The face reads the folder's now-playing line, so the face and the line follow one session; a
    /// folder without the line reads a widget of its own; another face reads none, and lets it go.
    /// </summary>
    [UiFact]
    public void TheFaceReadsTheFoldersLine()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var holder = Holder();
            using var island = Made(gfx, new MediaStand(), holder);
            var line = island.Tiles.Select(tile => tile.Island).OfType<MediaIsland>().Single(media => !media.Sources);
            Assert.Same(line, island.NowPlaying);
            Assert.Same(line, island.FaceWidget);

            // Without the line: a widget of the face's own, reading the same.
            holder.Folder!.Lists = [[.. holder.Folder.Cells.Skip(1)]];
            MediaIsland.Stand = new MediaStand(Title: "Hey");
            try
            {
                island.Configure(holder, new Config());
            }
            finally
            {
                MediaIsland.Stand = null;
            }
            Assert.DoesNotContain(island.Tiles, tile => tile.Island is MediaIsland { Sources: false });
            Assert.NotNull(island.NowPlaying);
            Assert.StartsWith("Hey|", island.NowPlaying!.FaceReading, StringComparison.Ordinal);

            // Another face: nothing playing is read for it.
            holder.Folder.Face = FolderFace.Of(FolderFaces.IconId);
            island.Configure(holder, new Config());
            Assert.Null(island.NowPlaying);
            Assert.Null(island.FaceWidget);
        });
    }

    /// <summary>
    /// §503: the face across a bar is the width it is set to, Medium by default, whatever plays — a track,
    /// "Hey" alone, nothing, a paused one — at 150 %; a long title is cut inside it rather than growing it.
    /// Down a bar on its side it is the art alone, whatever plays.
    /// </summary>
    [UiFact]
    public void TheFaceIsItsWidthWhateverPlays()
    {
        Offscreen.OnStaThread(gfx =>
        {
            foreach (var height in new[] { 28, 40, 48, 72 })
            {
                foreach (var vertical in new[] { false, true })
                {
                    var style = BarStyle(height, vertical, dpi: 144);
                    var sizes = new List<(float Width, float Height)>();
                    foreach (var stand in new[] { new MediaStand(), new MediaStand(Title: "Hey", Artist: ""), new MediaStand(Nothing: true), Full(playing: false) })
                    {
                        using var island = Made(gfx, stand, Kept());
                        sizes.Add(island.CellSize(0, style));
                    }
                    var where = $"{height} DIP{(vertical ? ", vertical" : "")}: {string.Join(", ", sizes)}";
                    Assert.True(sizes.Distinct().Count() == 1, where);
                    var along = vertical ? sizes[0].Height : sizes[0].Width;
                    Assert.Equal(FaceWidthTests.Button(style, MediaIsland.FaceLength(style, NowPlayingFace.Medium)), along, 0.5f);
                    Assert.Equal(vertical ? MediaIsland.ArtSize(style) : MathF.Round(192 * 1.5f), MediaIsland.FaceLength(style, NowPlayingFace.Medium), 0.5f);
                }
            }
        });
    }

    /// <summary>
    /// The art switched off takes the art away and nothing else: the face is the words alone, and a
    /// paused track says so in the quiet ink instead of the art's mark.
    /// </summary>
    [UiFact]
    public void WithoutTheArtTheFaceIsItsWords()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var holder = Holder();
            holder.Folder!.Face.Set(FolderFaces.NowPlaying.Option(NowPlayingFace.Art)!, false);
            using var island = Made(gfx, new MediaStand(), holder);
            var style = BarStyle(48);
            var pieces = Watched(island, style);
            Assert.DoesNotContain(pieces, piece => piece.What is Painted.Glyph or Painted.Shape);
            Assert.Equal(2, pieces.Count(piece => piece.What == Painted.Text));
        });
    }

    /// <summary>
    /// The play state, as the brief asks of the face: paused, a pause mark on the art — one glyph more
    /// than the note — and nothing while it plays.
    /// </summary>
    [UiFact]
    public void APausedTrackWearsThePauseMark()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var style = BarStyle(48);
            using var playing = Made(gfx, new MediaStand());
            using var paused = Made(gfx, new MediaStand(IsPlaying: false), Kept());
            Assert.Equal(1, Watched(playing, style).Count(piece => piece.What == Painted.Glyph));
            Assert.Equal(2, Watched(paused, style).Count(piece => piece.What == Painted.Glyph));
            Assert.NotEqual(playing.NowPlaying!.FaceReading, paused.NowPlaying!.FaceReading);
        });
    }

    /// <summary>
    /// The face is painted again in place when what it shows changes — another app picked, the track
    /// paused — never a rebuild, and it keeps its size.
    /// </summary>
    [UiFact]
    public void TheFacePaintsAgainInPlace()
    {
        Offscreen.OnStaThread(gfx =>
        {
            using var island = Made(gfx, Full());
            var style = BarStyle(48);
            var size = island.CellSize(0, style);
            _ = island.Measure(style);
            _ = island.Surface(0, style);
            var before = island.FaceShown;
            Assert.Contains("Spotify", before, StringComparison.Ordinal);

            island.NowPlaying!.Follow("msedge.exe");
            Assert.False(((IIsland)island).Refresh(gfx));
            Assert.NotEqual(before, island.FaceShown);
            Assert.Contains("msedge.exe", island.FaceShown, StringComparison.Ordinal);
            Assert.Equal(size, island.CellSize(0, style));
            Assert.Equal(0, island.IntervalMs);
            Assert.False(island.FaceSeconds);
        });
    }

    /// <summary>
    /// "Hide it while nothing plays", the entry's option, takes the folder off the bar while nothing
    /// plays and puts it back when something does — the default since §319; set off, it stays.
    /// </summary>
    [UiFact]
    public void HidingWhileNothingPlaysIsTheFacesOption()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var keeps = Holder();
            keeps.Folder!.Face.Set(FolderFaces.NowPlaying.Option(NowPlayingFace.HideIdle)!, false);
            using (var kept = Made(gfx, new MediaStand(Nothing: true), keeps))
            {
                Assert.Equal(1, kept.CellCount);
            }
            var holder = Holder();
            using (var hidden = Made(gfx, new MediaStand(Nothing: true), holder))
            {
                Assert.Equal(0, hidden.CellCount);
            }
            using (var shown = Made(gfx, new MediaStand(), holder))
            {
                Assert.Equal(1, shown.CellCount);
            }
            // Paused is nothing playing too: a paused player keeps its session as long as it runs,
            // which is why the folder never left the bar (§230).
            using (var paused = Made(gfx, new MediaStand(IsPlaying: false), holder))
            {
                Assert.Equal(0, paused.CellCount);
            }
        });
    }

    /// <summary>
    /// While the bar is arranged the face is on it whatever plays (§291), and while nothing plays it is
    /// greyed, as every face there only because the bar is arranged (§319): fainter than the same face
    /// set to stay, idle; playing, the two are drawn alike. Pictured at 150 % in each theme —
    /// now-playing-idle-&lt;dark|light&gt;.png: at rest playing, at rest idle (no cell), arranged idle
    /// greyed, arranged idle set to stay.
    /// </summary>
    [UiFact]
    public void ArrangedTheFaceIsThereGreyedWhileNothingPlays()
    {
        foreach (var dark in new[] { true, false })
        {
            Offscreen.OnStaThread(gfx =>
            {
                var style = BarStyle(48, dpi: 144, dark: dark);
                var keeps = Holder();
                keeps.Folder!.Face.Set(FolderFaces.NowPlaying.Option(NowPlayingFace.HideIdle)!, false);
                var cases = new (BarItem Holder, MediaStand Stand, bool Arranged)[]
                {
                    (Holder(), new MediaStand(Title: "Clair de lune", Artist: "Claude Debussy", Art: QuickFlyoutOffscreenTests.Cover()), false),
                    (Holder(), new MediaStand(Nothing: true), false),
                    (Holder(), new MediaStand(Nothing: true), true),
                    (keeps, new MediaStand(Nothing: true), true),
                    (Holder(), new MediaStand(Title: "Clair de lune", Artist: "Claude Debussy"), true),
                    (keeps, new MediaStand(Title: "Clair de lune", Artist: "Claude Debussy"), true),
                };
                var folders = cases.Select(one =>
                {
                    var folder = Made(gfx, one.Stand, one.Holder);
                    folder.Arranged = one.Arranged;
                    return folder;
                }).ToList();
                try
                {
                    var counts = folders.Select(folder => folder.Measure(style).Count).ToList();
                    Assert.Equal([1, 0, 1, 1, 1, 1], counts);
                    var light = folders.Select(folder => counts[folders.IndexOf(folder)] == 0 ? 0 : StandsOut(folder, style, dark)).ToList();
                    Assert.True(light[2] < light[3] * 0.8f, $"arranged and idle, the face is not greyed: {light[2]} against {light[3]} set to stay");
                    Assert.True(MathF.Abs(light[4] - light[5]) <= light[5] * 0.02f, $"playing, the two are drawn apart: {light[4]} against {light[5]}");

                    // The sheet: the first four, each on the bar's colour.
                    var theme = FlyoutTheme.For(style.Look);
                    const float gap = 18;
                    var shown = folders.Take(4).ToList();
                    var sizes = shown.Select((folder, at) => counts[at] == 0 ? (Width: 40f, Height: folder.CellSize(0, style).Height) : folder.CellSize(0, style)).ToList();
                    var width = gap + sizes.Sum(size => size.Width + gap);
                    var height = (2 * gap) + sizes.Max(size => size.Height);
                    Offscreen.Bitmap(width, height, dc =>
                    {
                        PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, width, height), 0, PopupPaint.Ink(theme.Base));
                        var x = gap;
                        for (var at = 0; at < shown.Count; at++)
                        {
                            var (w, h) = sizes[at];
                            PopupPaint.Fill(dc, PopupPaint.Rect(x, gap, w, h), 4 * style.Scale, PopupPaint.Ink(theme.CardBackground));
                            if (counts[at] > 0)
                            {
                                var folder = shown[at];
                                Offscreen.Shifted(dc, x, gap, () => folder.PaintCell(dc, 0, style, w, h));
                            }
                            x += w + gap;
                        }
                    }, bitmap => Offscreen.Save(bitmap, Path.Combine(Offscreen.Pictures, $"now-playing-idle-{(dark ? "dark" : "light")}.png")));
                }
                finally
                {
                    foreach (var folder in folders)
                    {
                        folder.Dispose();
                    }
                }
            });
        }

    }

    /// <summary>How far a folder's cell on the bar stands out (<see cref="StandsOut(float, float, bool, Offscreen.Painter)"/>).</summary>
    internal static float StandsOut(FolderIsland folder, IslandStyle style, bool dark)
    {
        var (w, h) = folder.CellSize(0, style);
        return StandsOut(w, h, dark, dc => folder.PaintCell(dc, 0, style, w, h));
    }

    /// <summary>
    /// How far a drawing <paramref name="w"/> by <paramref name="h"/> px stands out, on nothing: its light
    /// on a dark look, its dark on a light one, each pixel's by its cover — what the greying keeps
    /// (Unavailable.Matrix).
    /// </summary>
    internal static float StandsOut(float w, float h, bool dark, Offscreen.Painter paint)
    {
        var sum = 0f;
        Offscreen.Bitmap(w, h, paint, bitmap =>
        {
            var (width, height) = ((uint)MathF.Ceiling(w), (uint)MathF.Ceiling(h));
            var data = new byte[width * height * 4];
            fixed (byte* into = data)
            {
                Karpo.Dock.Native.ComUtil.Check(bitmap->CopyPixels(null, width * 4, (uint)data.Length, into));
            }
            // Premultiplied: the colour bytes are already by their cover.
            for (var at = 0; at < data.Length; at += 4)
            {
                var lit = (0.0722f * data[at]) + (0.7152f * data[at + 1]) + (0.2126f * data[at + 2]);
                sum += dark ? lit : data[at + 3] - lit;
            }
        });
        return sum;
    }

    /// <summary>
    /// With "Hide it while nothing plays" on, the bar is laid out again as the track stops and starts:
    /// a refresh after the news says the folder's cells changed, which is what makes the dock rebuild
    /// (Dock.IslandReady). A stop counts only after its grace, so a pause and play again a moment
    /// later, or a change of track, moves nothing.
    /// </summary>
    [UiFact]
    public void TheBarIsLaidOutAgainAsATrackStopsAndStarts()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var style = BarStyle(48);
            var holder = Holder();
            holder.Folder!.Face.Set(FolderFaces.NowPlaying.Option(NowPlayingFace.HideIdle)!, true);
            // No grace: a stop is nothing at once.
            using (var folder = Made(gfx, new MediaStand(QuietGrace: TimeSpan.Zero), holder))
            {
                var playing = folder.NowPlaying!;
                Assert.Equal(1, folder.Measure(style).Count);
                playing.Hear(new MediaStand(IsPlaying: false));
                Assert.True(((IIsland)folder).Refresh(gfx));
                Assert.Equal(0, folder.Measure(style).Count);
                // The app closed: still nothing, nothing to lay out again.
                playing.Hear(new MediaStand(Nothing: true));
                Assert.False(((IIsland)folder).Refresh(gfx));
                playing.Hear(new MediaStand());
                Assert.True(((IIsland)folder).Refresh(gfx));
                Assert.Equal(1, folder.Measure(style).Count);
            }

            // Its own grace: paused a moment, it stays, and the bar is not laid out again.
            using (var folder = Made(gfx, new MediaStand(), holder))
            {
                _ = folder.Measure(style);
                folder.NowPlaying!.Hear(new MediaStand(IsPlaying: false));
                Assert.False(((IIsland)folder).Refresh(gfx));
                Assert.Equal(1, folder.CellCount);
            }

            // A short grace runs out by itself: the folder is woken then, and leaves the bar.
            using (var folder = Made(gfx, new MediaStand(QuietGrace: TimeSpan.FromMilliseconds(100)), holder))
            {
                _ = folder.Measure(style);
                folder.NowPlaying!.Hear(new MediaStand(IsPlaying: false));
                Assert.False(((IIsland)folder).Refresh(gfx));
                Thread.Sleep(250);
                Assert.True(((IIsland)folder).Refresh(gfx));
                Assert.Equal(0, folder.CellCount);
            }
        });
    }

    /// <summary>
    /// Beside other faces, as on the person's quick settings button (Wi-Fi, sound and now playing set
    /// to hide while nothing plays), only the now-playing face goes while nothing plays (§237): the
    /// others stay, in their order, the button is as long as one of the two alone — no room left
    /// between where it stood — and the bar is laid out again; it is back when a track plays.
    /// </summary>
    [UiFact]
    public void BesideOtherFacesOnlyTheNowPlayingFaceGoesWhileNothingPlays()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var style = BarStyle(48);
            var holder = Holder();
            var media = holder.Folder!.Face;
            media.Set(FolderFaces.NowPlaying.Option(NowPlayingFace.HideIdle)!, true);
            holder.Folder.Faces = [new FolderFace { Kind = FolderFaces.WifiId }, new FolderFace { Kind = FolderFaces.SoundId }, media];
            var two = Holder();
            two.Folder!.Faces = [new FolderFace { Kind = FolderFaces.WifiId }, new FolderFace { Kind = FolderFaces.SoundId }];
            using var others = Made(gfx, new MediaStand(), two);
            _ = others.Measure(style);
            var alone = others.CellSize(0, style);

            using var folder = Made(gfx, new MediaStand(QuietGrace: TimeSpan.Zero), holder);
            Assert.Equal(1, folder.Measure(style).Count);
            Assert.Equal(3, folder.FacesOnBar.Count);
            var playing = folder.CellSize(0, style);
            Assert.True(playing.Width > alone.Width, "the now-playing face takes no room while it plays");

            folder.NowPlaying!.Hear(new MediaStand(IsPlaying: false));
            Assert.True(((IIsland)folder).Refresh(gfx));
            Assert.Equal(1, folder.Measure(style).Count);
            Assert.Equal([FolderFaces.Wifi, FolderFaces.Sound], folder.FacesOnBar.Select(face => face.Info));
            Assert.Equal(alone, folder.CellSize(0, style));
            var boxes = FolderIsland.FaceBoxes(gfx, holder.Folder, style, alone.Width, alone.Height, folder.FacesOnBar);
            Assert.Equal(FolderIsland.FaceBoxes(gfx, two.Folder, style, alone.Width, alone.Height), boxes);

            folder.NowPlaying!.Hear(new MediaStand());
            Assert.True(((IIsland)folder).Refresh(gfx));
            Assert.Equal(3, folder.FacesOnBar.Count);
            Assert.Equal(playing, folder.CellSize(0, style));
        });
    }

    /// <summary>
    /// The hover is the entry's: nothing over the bar; to a screen reader the track and where it
    /// plays, or that nothing plays.
    /// </summary>
    [UiFact]
    public void TheHoverIsTheEntrys()
    {
        Offscreen.OnStaThread(gfx =>
        {
            using var playing = Made(gfx, new MediaStand(AppName: "Spotify"));
            Assert.Equal("Air on the G String — Johann Sebastian Bach · Spotify", playing.Tooltip(0));
            using var quiet = Made(gfx, new MediaStand(Nothing: true), Kept());
            Assert.Equal(S.WidgetMediaNothingPlaying, quiet.Tooltip(0));
        });
    }

    /// <summary>
    /// The face's right click has play or pause, previous and next and the player, worked on what the
    /// face shows — each only where its button would do something (§308) — and with the line gone from
    /// the folder, the same.
    /// </summary>
    [UiFact]
    public void TheFacesRightClickPlaysPausesAndSkips()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var holder = Holder();
            using var island = Made(gfx, new MediaStand(IsPlaying: false, CanPrevious: false), holder);
            var (items, acts) = TheDock.BarMenuRows(holder, island);
            Assert.Equal([WidgetAct.PlayPause, WidgetAct.Next], acts.Select(act => act.Act));
            Assert.Same(island.FaceWidget, acts[0].Works);
            Assert.Equal(S.DockWidgetMenuPlay, items[0].Text);
            Assert.All(items, item => Assert.True(item.Enabled));
            // A stand-in has no session: a pick reaches no app, and opens nothing.
            TheDock.RunAct(acts[0], acts[0].Works, Never);

            holder.Folder!.Lists = [[.. holder.Folder.Cells.Skip(1)]];
            MediaIsland.Stand = new MediaStand();
            try
            {
                island.Configure(holder, new Config());
            }
            finally
            {
                MediaIsland.Stand = null;
            }
            // A stand-in has no session: no player to bring forward.
            Assert.Equal([WidgetAct.PlayPause, WidgetAct.Previous, WidgetAct.Next], TheDock.BarMenuRows(holder, island).Acts.Select(act => act.Act));
        });

        static void Never(string target, string? verb) => Assert.Fail("the now-playing face's menu opened something");
    }

    // --- The line ------------------------------------------------------------------------------------

    /// <summary>
    /// Shuffle and repeat end the whole widget's last row, the app's, under the transport, on the trailing
    /// gutter (§401), where the app lets them be set: a click on one sets it (shown at once, the accent while on),
    /// and repeat goes round off, this track, all. Where the app has neither, the ends are no target.
    /// </summary>
    [UiFact]
    public void ShuffleAndRepeatAreTheLinesWhereTheAppHasThem()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var style = LineStyle();
            var box = LineBox(4, style);
            using var line = Line(gfx, Full());
            var laid = line.LineOf(box, style);
            Assert.True(laid.Whole);
            Assert.True(TilePaint.Width(laid.Shuffle) > 0 && TilePaint.Width(laid.Repeat) > 0, "the whole widget has no ends for shuffle and repeat");
            Assert.True(laid.Shuffle.top >= laid.Next.bottom && laid.Shuffle.right <= laid.Repeat.left
                && MathF.Abs(laid.Repeat.right - (box.right - ListForm.Of(gfx, style).Inset)) <= 1, "shuffle and repeat do not end the app's row under the transport");

            var shuffle = line.TargetOn(Middle(laid.Shuffle), box, style, WidgetSurface.List);
            var repeat = line.TargetOn(Middle(laid.Repeat), box, style, WidgetSurface.List);
            Assert.False(line.Modes.Shuffle);
            _ = line.Click(shuffle);
            Assert.True(line.Modes.Shuffle);
            _ = line.Click(shuffle);
            Assert.False(line.Modes.Shuffle);
            Assert.Equal(MediaRepeat.None, line.Modes.Repeat);
            _ = line.Click(repeat);
            Assert.Equal(MediaRepeat.Track, line.Modes.Repeat);
            _ = line.Click(repeat);
            Assert.Equal(MediaRepeat.List, line.Modes.Repeat);
            _ = line.Click(repeat);
            Assert.Equal(MediaRepeat.None, line.Modes.Repeat);
            Assert.Equal(S.WidgetMediaShuffle, line.Tooltip(shuffle));

            // An app with neither: the same places, and nothing there to press.
            using var plain = Line(gfx, new MediaStand());
            var parts = plain.LineOf(box, style);
            Assert.Equal(laid.Shuffle, parts.Shuffle);
            Assert.Equal(0, plain.TargetOn(Middle(parts.Shuffle), box, style, WidgetSurface.List));
            Assert.Equal(0, plain.TargetOn(Middle(parts.Repeat), box, style, WidgetSurface.List));
        });
    }

    /// <summary>
    /// The art on the right and the words on the left (2026-10-11): at every height and width — a
    /// narrow list too, which once stacked them in a centred column — the art holds the trailing
    /// gutter and starts after the words and the buttons beside it, the words start where every name
    /// begins (the kit's row, §401), and a track without art keeps the same places.
    /// </summary>
    [UiFact]
    public void TheLinesArtIsRightOfItsWords()
    {
        Offscreen.OnStaThread(gfx =>
        {
            using var covered = Line(gfx, Full());
            using var bare = Line(gfx, Full() with { Art = null });
            foreach (var text in new[] { 1f, 1.5f })
            {
                var look = LookFor(true, text);
                var style = FolderFlyout.StyleFor(1.5f, look, FlyoutTheme.For(look));
                var gutter = ListForm.Of(gfx, style).Inset;
                var textEdge = ListForm.Of(gfx, style).TextLeft;
                for (var rows = 1; rows <= 6; rows++)
                foreach (var narrow in new[] { false, true })
                {
                    var box = LineBox(rows, style);
                    if (narrow)
                    {
                        box.right = box.left + style.Px(240);
                    }
                    var laid = covered.LineOf(box, style);
                    var where = $"{rows} rows at {text:0.##}x text{(narrow ? ", narrow" : "")}";
                    Assert.Equal(laid.Art, bare.LineOf(box, style).Art);
                    Assert.True(TilePaint.Width(laid.Art) > 0, $"{where}: no art");
                    Assert.True(MathF.Abs(box.right - gutter - laid.Art.right) <= 1, $"{where}: the art ends {box.right - laid.Art.right:0} px from the line's end, and the gutter is {gutter:0}");
                    bool Beside(D2D_RECT_F part) => part.top < laid.Art.bottom && part.bottom > laid.Art.top;
                    Assert.All(laid.Words.Where(words => Beside(words.Box)), words => Assert.True(words.Box.right <= laid.Art.left, $"{where}: \"{words.Text}\" reaches {words.Box.right:0}, past the art at {laid.Art.left:0}"));
                    Assert.True(MathF.Abs(laid.Words.Min(words => words.Box.left) - box.left - textEdge) <= 1, $"{where}: the words do not start where every name begins");
                    foreach (var button in new[] { laid.Previous, laid.Play, laid.Next, laid.Shuffle, laid.Repeat })
                    {
                        Assert.True(TilePaint.Width(button) <= 0 || !Beside(button) || button.right <= laid.Art.left, $"{where}: a button reaches {button.right:0}, past the art at {laid.Art.left:0}");
                    }
                }
            }
        });
    }

    /// <summary>The face's words lead and its art follows them, playing and idle, on every bar.</summary>
    [UiFact]
    public void TheFacesArtIsRightOfItsWords()
    {
        Offscreen.OnStaThread(gfx =>
        {
            foreach (var stand in new[] { new MediaStand(), new MediaStand(Title: "Hey", Artist: "Pixies"), new MediaStand(Nothing: true) })
            {
                using var island = Made(gfx, stand, Kept());
                foreach (var height in new[] { 28, 40, 48, 72 })
                {
                    var pieces = Watched(island, BarStyle(height, dpi: 144));
                    var art = Assert.Single(pieces, piece => piece.What == Painted.Shape).Box;
                    var words = pieces.Where(piece => piece.What == Painted.Text).ToList();
                    Assert.NotEmpty(words);
                    Assert.All(words, piece => Assert.True(piece.Box.right <= art.left, $"{height} DIP: words reach {piece.Box.right:0}, past the art at {art.left:0}"));
                }
            }
        });
    }

    /// <summary>
    /// A short line has no room for shuffle and repeat: its menu has them — ticked while on, saying
    /// how repeat is set — and leaves them out where the app has neither, as the flyout did.
    /// </summary>
    [UiFact]
    public void AShortLinesMenuHasShuffleAndRepeat()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var style = LineStyle();
            using var line = Line(gfx, Full() with { Shuffle = true, Repeat = MediaRepeat.List });
            var laid = line.LineOf(LineBox(2, style), style);
            Assert.Equal(0, TilePaint.Width(laid.Shuffle));
            var (items, acts) = TheDock.ActRows(TheDock.ActsFor(ListKind.Media), line);
            var shuffle = acts.FindIndex(act => act.Act == WidgetAct.Shuffle);
            var repeat = acts.FindIndex(act => act.Act == WidgetAct.Repeat);
            var rows = items.Where(item => item.Id != 0).ToList();
            Assert.Equal(S.WidgetMediaShuffle, rows[shuffle].Text);
            Assert.True(rows[shuffle].Checked);
            Assert.Equal(S.WidgetMediaRepeatAll, rows[repeat].Text);
            Assert.True(rows[repeat].Checked);
            TheDock.RunAct(acts[repeat], line, (_, _) => { });
            Assert.Equal(MediaRepeat.None, line.Modes.Repeat);

            using var plain = Line(gfx, new MediaStand());
            var (_, none) = TheDock.ActRows(TheDock.ActsFor(ListKind.Media), plain);
            Assert.DoesNotContain(none, act => act.Act is WidgetAct.Shuffle or WidgetAct.Repeat or WidgetAct.FollowSession);
        });
    }

    /// <summary>
    /// Several apps playing: the app's name on the whole widget carries the picker, whose click asks
    /// for the line's own menu, the apps first, each under its name, the one followed ticked; a pick
    /// follows it, and the picked one picked again goes back to Windows' own choice. One app alone
    /// has no picker to press, and no rows to pick from.
    /// </summary>
    [UiFact]
    public void ThePickerChoosesWhichAppTheLineFollows()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var style = LineStyle();
            var box = LineBox(4, style);
            using var line = Line(gfx, Full());
            var laid = line.LineOf(box, style);
            Assert.True(TilePaint.Width(laid.Picker) > 0, "the whole widget names no app to pick another from");
            Assert.Equal("Spotify", laid.Words[^1].Text);
            var picker = line.TargetOn(Middle(laid.Picker), box, style, WidgetSurface.List);
            Assert.Equal(IslandView.Menu, line.Click(picker).View);
            Assert.Equal(S.WidgetMediaPlayingIn, line.Tooltip(picker));

            var (items, acts) = TheDock.ActRows(TheDock.ActsFor(ListKind.Media), line);
            Assert.Equal(S.WidgetMediaPlayingIn, items[0].Text);
            Assert.True(items[0].Header);
            var apps = acts.Where(act => act.Act == WidgetAct.FollowSession).ToList();
            Assert.Equal(["Spotify.exe", "msedge.exe"], apps.Select(act => act.On));
            Assert.Equal([true, false], items.Skip(1).Take(2).Select(item => item.Checked == true));
            Assert.False(line.Picked);
            Assert.Equal("Spotify.exe", line.Follows);

            TheDock.RunAct(apps[1], line, (_, _) => { });
            Assert.True(line.Picked);
            Assert.Equal("msedge.exe", line.Follows);
            Assert.Equal("Microsoft Edge", line.Controls.App);
            Assert.Equal([false, true], TheDock.ActRows(TheDock.ActsFor(ListKind.Media), line).Items.Skip(1).Take(2).Select(item => item.Checked == true));

            // Picked again: back to Windows' own choice.
            TheDock.RunAct(apps[1], line, (_, _) => { });
            Assert.False(line.Picked);
            Assert.Equal("Spotify", line.Controls.App);

            // One app alone: the name is there, the picker does nothing, and the menu has no apps.
            using var alone = Line(gfx, new MediaStand(AppName: "Spotify", Art: QuickFlyoutOffscreenTests.Cover(), Position: TimeSpan.FromSeconds(1), Duration: TimeSpan.FromSeconds(9)));
            var single = alone.LineOf(box, style);
            Assert.Equal(default, alone.Click(alone.TargetOn(Middle(single.Picker), box, style, WidgetSurface.List)));
            Assert.DoesNotContain(TheDock.ActRows(TheDock.ActsFor(ListKind.Media), alone).Rows, act => act.Act == WidgetAct.FollowSession);
        });
    }

    /// <summary>The timeline's second tick is the open line's while the track plays, and none while it is paused.</summary>
    [UiFact]
    public void TheTimelineTicksOnlyWhilePlaying()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var style = LineStyle();
            var box = LineBox(4, style);
            foreach (var playing in new[] { true, false })
            {
                using var line = Line(gfx, Full(playing));
                Assert.Equal(0, line.IntervalMs);
                Offscreen.Bitmap(TilePaint.Width(box), TilePaint.Height(box), dc => line.PaintCard(dc, box, style, WidgetSurface.List), _ => { });
                Assert.Equal(playing ? 1000 : 0, line.IntervalMs);
                line.Visible(false);
                Assert.Equal(0, line.IntervalMs);
            }
        });
    }

    // --- The line while nothing plays (§327) ----------------------------------------------------------

    /// <summary>
    /// The line's "Hide it while nothing plays", in the face's words: on where a file says nothing, off
    /// only where it says false, and written only then, so no file changes by being read; a line that
    /// never hides keeps none. Its menu has it, a switch among its behaviour.
    /// </summary>
    [Fact]
    public void TheLineHidesWhileNothingPlaysUnlessAFileSaysOff()
    {
        static CellItem Read(string json)
        {
            var item = JsonSerializer.Deserialize(json, ConfigJson.Default.CellItem)!;
            item.View = FolderView.List;
            item.Normalize();
            return item;
        }
        var unset = Read("""{ "kind": "media" }""");
        Assert.True(unset.HidesIdle);
        Assert.Null(unset.HideIdle);
        Assert.DoesNotContain("hideIdle", JsonSerializer.Serialize(unset, ConfigJson.Default.CellItem));
        Assert.Null(Read("""{ "kind": "media", "hideIdle": true }""").HideIdle);
        var off = Read("""{ "kind": "media", "hideIdle": false }""");
        Assert.False(off.HidesIdle);
        Assert.False(Read(JsonSerializer.Serialize(off, ConfigJson.Default.CellItem)).HidesIdle);
        var clock = Read("""{ "kind": "clock", "hideIdle": false }""");
        Assert.Null(clock.HideIdle);
        Assert.False(clock.HidesIdle);

        var row = Assert.Single(OptionRows.Of(unset), each => each.Key == OptionRows.HideIdle);
        Assert.Equal(FolderFaces.NowPlaying.TitleOf(FolderFaces.NowPlaying.Option(NowPlayingFace.HideIdle)!), row.Title);
        Assert.Equal(OptionSlot.Behaviour, row.Slot);
        Assert.True(row.On);
        Assert.True(row.Set!(0));
        Assert.False(unset.HideIdle);
        Assert.DoesNotContain(OptionRows.Of(clock), each => each.Key == OptionRows.HideIdle);
    }

    /// <summary>
    /// At rest a now-playing line takes no room while nothing plays (§327): the line under it closes
    /// up, a column it was all of goes, and one set to stay is there. Arranged every line stands where
    /// it stands, the idle ones greyed — fainter than one set to stay — so nothing moves as edit mode
    /// comes. A track starting puts it back at rest. Pictured at 150 %, dark:
    /// now-playing-line-rest.png, now-playing-line-arranged.png, now-playing-line-greyed.png.
    /// </summary>
    [UiFact]
    public void TheLineIsGoneAtRestWhileNothingPlaysAndGreyedArranged()
    {
        WindowShots.OnHiddenDesktop("now-playing-line", (gfx, instance) =>
        {
            var island = BarKind.FolderList.New();
            island.Id = "idle";
            island.Folder!.Lists = [];
            CellItem Item(ListKind kind, string id, bool stays = false)
            {
                var item = kind.New();
                item.Id = id;
                item.HideIdle = stays ? false : null;
                return item;
            }
            island.Folder.Put(Item(ListKind.Media, "first"), 0);
            island.Folder.Put(Item(ListKind.Clock, "clock"), 0);
            island.Folder.Put(Item(ListKind.Cpu, "cpu"), 1);
            island.Folder.Put(Item(ListKind.Media, "kept", stays: true), 1);
            island.Folder.Put(Item(ListKind.Media, "alone"), 2);
            FolderIsland folder;
            MediaIsland.Stand = new MediaStand(Nothing: true);
            try
            {
                folder = new FolderIsland(Config.IslandKey("layout:default", island.Id), island, gfx, new Config());
            }
            finally
            {
                MediaIsland.Stand = null;
            }
            using var made = folder;
            int At(string id) => folder.Tiles.Select(tile => tile.Cell.Island.Id).ToList().IndexOf(id);
            var (first, clock, kept, alone) = (At("first"), At("clock"), At("kept"), At("alone"));
            Assert.Equal(new[] { first, alone }, folder.Resting.Order());
            Assert.False(folder.Empty);

            var look = LookFor(dark: true);
            var work = new TerraFX.Interop.Windows.RECT { right = 2880, bottom = 1560 };
            var anchor = new TerraFX.Interop.Windows.POINT { x = 1440, y = 1560 };
            using var flyout = new FolderFlyout(gfx, instance, 0);
            flyout.Show(gfx, look, 1.5f, anchor, work, Edge.Bottom, folder);
            var rest = flyout.Plan!;
            Assert.False(rest.Shows(first));
            Assert.False(rest.Shows(alone));
            Assert.True(rest.Shows(kept));
            Assert.Equal(rest.Lists[0].Box.top, rest.Cards[clock].top);
            Assert.Equal(0, rest.Lists[2].Width);
            Assert.Equal(rest.Lists[1].Box.right, rest.Width, 1);
            WindowShots.Settle(250);
            Assert.True(WindowShots.Save(flyout.Hwnd, "now-playing-line-rest.png"), "the folder at rest came back blank");
            flyout.Hide();

            flyout.Show(gfx, look, 1.5f, anchor, work, Edge.Bottom, folder, FlyoutAlign.Center, editing: true);
            var arranged = flyout.Plan!;
            Assert.True(arranged.Shows(first) && arranged.Shows(alone) && arranged.Shows(kept));
            Assert.True(arranged.Lists[2].Width > 0);
            Assert.True(arranged.Cards[clock].top > arranged.Cards[first].top, "arranged, the idle line is not where it stands");
            WindowShots.Settle(250);
            Assert.True(WindowShots.Save(flyout.Hwnd, "now-playing-line-arranged.png"), "the arranged folder came back blank");
            flyout.Hide();

            // A track starting puts the line back at rest.
            ((MediaIsland)folder.Tiles[first].Island).Hear(new MediaStand());
            _ = ((IIsland)folder).Refresh(gfx);
            Assert.Equal(new[] { alone }, folder.Resting);
        });

        // Greyed: an idle line drawn fainter than one set to stay; playing, the two alike.
        Offscreen.OnStaThread(gfx =>
        {
            var style = LineStyle();
            var box = LineBox(2, style);
            float Light(MediaStand stand, bool stays)
            {
                using var line = Line(gfx, stand, stays);
                return StandsOut(box.right, box.bottom, dark: true, dc => line.PaintOn(dc, box, style, WidgetSurface.List));
            }
            var greyed = Light(new MediaStand(Nothing: true), stays: false);
            var staying = Light(new MediaStand(Nothing: true), stays: true);
            Assert.True(greyed < staying * 0.8f, $"arranged and idle, the line is not greyed: {greyed} against {staying} set to stay");
            var playing = Light(new MediaStand(Title: "Clair de lune", Artist: "Claude Debussy"), stays: false);
            var playingStays = Light(new MediaStand(Title: "Clair de lune", Artist: "Claude Debussy"), stays: true);
            Assert.True(MathF.Abs(playing - playingStays) <= playingStays * 0.02f, $"playing, the two are drawn apart: {playing} against {playingStays}");

            using var idle = Line(gfx, new MediaStand(Nothing: true));
            using var kept = Line(gfx, new MediaStand(Nothing: true), stays: true);
            const float gap = 18;
            var (w, h) = (box.right, box.bottom);
            var theme = FlyoutTheme.For(style.Look);
            Offscreen.Bitmap((2 * w) + (3 * gap), h + (2 * gap), dc =>
            {
                PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, (2 * w) + (3 * gap), h + (2 * gap)), 0, PopupPaint.Ink(theme.Base));
                Offscreen.Shifted(dc, gap, gap, () => idle.PaintOn(dc, box, style, WidgetSurface.List));
                Offscreen.Shifted(dc, w + (2 * gap), gap, () => kept.PaintOn(dc, box, style, WidgetSurface.List));
            }, bitmap => Offscreen.Save(bitmap, Path.Combine(Offscreen.Pictures, "now-playing-line-greyed.png")));
        });
    }

    /// <summary>
    /// A folder whose every line is gone at rest is an empty folder there (§280, §327): its face on the
    /// bar as set, but a click opens nothing and does not press; arranged it opens. Playing, or with
    /// its line set to stay, it is a folder with a widget in it.
    /// </summary>
    [UiFact]
    public void AFolderWithEveryLineGoneIsEmptyAtRest()
    {
        Offscreen.OnStaThread(gfx =>
        {
            using (var idle = Made(gfx, new MediaStand(Nothing: true), Kept()))
            {
                Assert.Equal(1, idle.CellCount);
                Assert.True(idle.Empty);
                Assert.False(idle.ClickDoes(0));
                Assert.False(OneFolder.Opens(idle.Empty, arranging: false));
                Assert.True(OneFolder.Opens(idle.Empty, arranging: true));
            }
            using (var playing = Made(gfx, new MediaStand(), Kept()))
            {
                Assert.False(playing.Empty);
                Assert.True(playing.ClickDoes(0));
            }
            var stays = Kept();
            stays.Folder!.Cells[0].Island.HideIdle = false;
            using (var kept = Made(gfx, new MediaStand(Nothing: true), stays))
            {
                Assert.False(kept.Empty);
                Assert.Empty(kept.Resting);
            }
        });
    }

    /// <summary>
    /// §440: quick settings' now-playing line gone at rest takes the divider under it along: the folder
    /// starts at Wi-Fi, flush with its top, as Windows' panel does with nothing playing. Before, the
    /// divider stood alone at the list's top, a bare gap over Wi-Fi (at 150 %: Wi-Fi 9 px down). A
    /// divider between two lines that stay stays, one the hidden line leaves right after another goes,
    /// one the person put at an end beside no line gone stays; arranged every line and divider stands.
    /// </summary>
    [Fact]
    public void ADividerTheHiddenLineLeavesAtAnEndGoesWithIt()
    {
        var look = TestLooks.LookFor(dark: true);
        const float scale = 1.5f;
        var quick = Presets.Folder(Presets.Find(Presets.QuickPanelId)!, Machine).Folder!;
        var cells = quick.Cells;
        var media = cells.ToList().FindIndex(cell => cell.Island.Kind == "media");
        var wifi = cells.ToList().FindIndex(cell => cell.Island.Kind == "wifiSwitch");
        Assert.Equal((0, 2), (media, wifi));
        Assert.Equal(WidgetInfos.Divider.Id, cells[1].Island.Kind);
        FolderPlan Plan(bool editing) => FolderFlyout.Build(look, scale, FolderView.List, quick, cells, 1040 * scale, editing, resting: [media]);
        var rest = Plan(editing: false);
        Assert.Equal(0, rest.Cards[media].bottom - rest.Cards[media].top);
        Assert.Equal(0, rest.Cards[1].bottom - rest.Cards[1].top);
        Assert.Equal(rest.Lists[0].Box.top, rest.Cards[wifi].top);
        Assert.Equal(rest.Lists[0].Runs[0].top, rest.Cards[wifi].top);
        var arranged = Plan(editing: true);
        Assert.True(arranged.Cards[1].bottom > arranged.Cards[1].top);
        Assert.True(arranged.Cards[wifi].top > arranged.Cards[media].bottom);

        // The rule by itself: 1 and 3 dividers.
        static bool Divides(int index) => index is 1 or 3;
        Assert.Equal([2, 3], FolderFlyout.GoneAtRest([0, 1, 2, 3, 4], [2], Divides).Order());
        Assert.Equal([0, 1], FolderFlyout.GoneAtRest([0, 1, 2], [0], Divides).Order());
        Assert.Equal([2, 3], FolderFlyout.GoneAtRest([0, 2, 3], [2], Divides).Order());
        Assert.Equal([0], FolderFlyout.GoneAtRest([0, 2, 3], [0], Divides).Order());
        Assert.Empty(FolderFlyout.GoneAtRest([0, 1, 2], [5], Divides));
    }

    // --- The apps playing --------------------------------------------------------------------------------

    /// <summary>A list of the apps playing on its own over <paramref name="stand"/>.</summary>
    private static MediaIsland Sources(Gfx gfx, MediaStand stand)
    {
        var item = ListKind.MediaSources.New();
        item.Id = "sources";
        MediaIsland.Stand = stand;
        try
        {
            return new MediaIsland(Config.IslandKey(null, item.Id), item, gfx, new Config());
        }
        finally
        {
            MediaIsland.Stand = null;
        }
    }

    /// <summary>
    /// The preset is the now-playing line alone, its whole widget (four rows fixed before §432), as the person made it in their own
    /// layout (2026-10-11: "take mine in the: now playing widget"): no list of the apps playing (the
    /// line's picker is on the app's name), no divider, no volume and no output device.
    /// </summary>
    [Fact]
    public void ThePresetIsTheNowPlayingLineAlone()
    {
        Assert.Equal(["media"], Presets.Find(Presets.MediaId)!.Items.Select(item => item.Form));
        var line = Assert.Single(Holder().Folder!.Cells).Island;
        Assert.Equal(ListKind.Media, line.List);
        Assert.Equal(((LineHeight?)null, (LineSize?)LineSize.Full), (line.Height, line.Size));
    }

    /// <summary>
    /// A row per app, the one followed marked; a click on another follows it — in the now-playing line
    /// too, which shares the pick — and a click on the one followed keeps it.
    /// </summary>
    [UiFact]
    public void TheSourcesPickWhatTheLineFollows()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var stand = Full() with { Pick = new MediaPick() };
            using var line = Line(gfx, stand);
            using var sources = Sources(gfx, stand);
            Assert.True(sources.Sources);
            Assert.False(line.Sources);
            var style = LineStyle();
            var box = PopupPaint.Rect(0, 0, FolderFlyout.ListWidth(1.5f), sources.ListHeight(FolderFlyout.ListWidth(1.5f), style));
            var rows = sources.SourceRows(box, style);
            Assert.Equal(["Spotify", "Microsoft Edge"], rows.Select(row => sources.PlayingIn[row.Index].App));
            Assert.Equal([true, false], rows.Select(row => row.Followed));
            Assert.All(rows, row => Assert.True(row.Box.bottom <= box.bottom + 0.5f));

            var edge = sources.TargetOn(Middle(rows[1].Box), box, style, WidgetSurface.List);
            Assert.Equal(MediaIsland.SourceTarget + 1, edge);
            _ = sources.Click(edge);
            Assert.Equal("msedge.exe", line.Follows, StringComparer.OrdinalIgnoreCase);
            Assert.Equal([false, true], sources.SourceRows(box, style).Select(row => row.Followed));
            // The one followed, clicked again, stays followed.
            _ = sources.Click(edge);
            Assert.Equal("msedge.exe", line.Follows, StringComparer.OrdinalIgnoreCase);
            // And the line's own picker moves the list's mark.
            line.Follow("Spotify.exe");
            Assert.True(sources.SourceRows(box, style)[0].Followed);
        });
    }

    /// <summary>Nothing playing: one row, saying so; a click on it does nothing.</summary>
    [UiFact]
    public void TheSourcesSayWhenNothingPlays()
    {
        Offscreen.OnStaThread(gfx =>
        {
            using var sources = Sources(gfx, new MediaStand(Nothing: true));
            var style = LineStyle();
            var width = FolderFlyout.ListWidth(1.5f);
            var one = sources.ListHeight(width, style);
            Assert.Empty(sources.SourceRows(PopupPaint.Rect(0, 0, width, one), style));
            Assert.Equal(S.WidgetMediaNothingPlaying, sources.Tooltip(0));
            Assert.Equal(default, sources.Click(0));
        });
    }

    // --- Pictures --------------------------------------------------------------------------------------

    /// <summary>
    /// The preset as it opens: a track with shuffle, repeat and two apps; paused; one with nothing
    /// but play; nothing playing; and at 225 % text — at 150 %, to stand beside quick-media-*.png.
    /// </summary>
    [UiFact]
    public void MediaPresetPictures()
    {
        foreach (var dark in new[] { true, false })
        {
            Offscreen.OnStaThread(gfx =>
            {
                var folders = new List<FolderIsland>();
                try
                {
                    var panels = new List<(string, Look, FolderIsland, FolderPlan)>();
                    void Add(string caption, MediaStand stand, float text = 1f)
                    {
                        var look = LookFor(dark, text);
                        var folder = Made(gfx, stand, key: $"{Owner}/{Holder().Id}-{panels.Count}");
                        folders.Add(folder);
                        folder.Opened(gfx);
                        panels.Add((caption, look, folder, Plan(folder, look, 1.5f).Plan));
                    }

                    Add("Playing, two apps", Full() with { Shuffle = true, Repeat = MediaRepeat.Track });
                    Add("Paused", Full(playing: false));
                    Add("One app, no modes", new MediaStand(AppName: "Spotify", Art: QuickFlyoutOffscreenTests.Cover(), Position: TimeSpan.FromSeconds(62), Duration: TimeSpan.FromSeconds(214)));
                    Add("Nothing playing", new MediaStand(Nothing: true));
                    Add("Playing: 225 % text", Full(), 2.25f);
                    Sheet(gfx, $"media-preset-{(dark ? "dark" : "light")}.png", 1.5f, panels);
                }
                finally
                {
                    foreach (var folder in folders)
                    {
                        folder.Closed();
                        folder.Dispose();
                    }
                }
            });
        }
    }

    /// <summary>
    /// The face on bars of 28, 40, 48 and 72 DIP at 150 %: playing with art, paused, without art,
    /// a short track, nothing playing — media-face-&lt;dark|light&gt;.png, beside the entry's
    /// media-bars-*.png before it went — and down a 48 DIP bar on its side.
    /// </summary>
    [UiFact]
    public void MediaFacePictures()
    {
        foreach (var dark in new[] { true, false })
        {
            Offscreen.OnStaThread(gfx =>
            {
                var look = LookFor(dark);
                var theme = FlyoutTheme.For(look);
                var plain = Holder();
                var bare = Holder();
                bare.Folder!.Face.Set(FolderFaces.NowPlaying.Option(NowPlayingFace.Art)!, false);
                var cover = QuickFlyoutOffscreenTests.Cover();
                var islands = new List<FolderIsland>
                {
                    Made(gfx, new MediaStand(Art: cover), plain),
                    Made(gfx, new MediaStand(Art: cover, IsPlaying: false), Kept()),
                    Made(gfx, new MediaStand(), Holder()),
                    Made(gfx, new MediaStand(Title: "Hey", Artist: "Pixies", Art: cover), Holder()),
                    Made(gfx, new MediaStand(Nothing: true), Kept()),
                    Made(gfx, new MediaStand(Art: cover), bare),
                };
                var styles = new[] { 28, 40, 48, 72 }.Select(height => BarStyle(height, dpi: 144, dark: dark)).ToList();
                var down = BarStyle(48, vertical: true, dpi: 144, dark: dark);
                try
                {
                    const float gap = 12;
                    var width = gap + islands.Max(island => island.CellSize(0, styles[^1]).Width) + gap + (islands.Count * (down.Thickness + gap));
                    var height = gap + styles.Sum(style => islands.Count * (style.Thickness + gap));
                    height = MathF.Max(height, gap + islands.Max(island => island.CellSize(0, down).Height) + gap);
                    Offscreen.Bitmap(width, height, dc =>
                    {
                        PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, width, height), 0, PopupPaint.Ink(theme.Base));
                        var y = gap;
                        foreach (var style in styles)
                        {
                            foreach (var island in islands)
                            {
                                var (w, h) = island.CellSize(0, style);
                                PopupPaint.Fill(dc, PopupPaint.Rect(gap, y, w, h), 4 * style.Scale, PopupPaint.Ink(theme.CardBackground));
                                Offscreen.Shifted(dc, gap, y, () => island.PaintCell(dc, 0, style, w, h));
                                y += h + gap;
                            }
                        }
                        var x = gap + islands.Max(island => island.CellSize(0, styles[^1]).Width) + gap;
                        foreach (var island in islands)
                        {
                            var (w, h) = island.CellSize(0, down);
                            PopupPaint.Fill(dc, PopupPaint.Rect(x, gap, w, h), 4 * down.Scale, PopupPaint.Ink(theme.CardBackground));
                            Offscreen.Shifted(dc, x, gap, () => island.PaintCell(dc, 0, down, w, h));
                            x += w + gap;
                        }
                    }, bitmap => Offscreen.Save(bitmap, Path.Combine(Offscreen.Pictures, $"media-face-{(dark ? "dark" : "light")}.png")));
                }
                finally
                {
                    foreach (var island in islands)
                    {
                        island.Dispose();
                    }
                }
            });
        }
    }

    /// <summary>What the face's one cell draws, piece by piece (PaintWatch), drawn as the bar would.</summary>
    private static List<(D2D_RECT_F Box, Painted What)> Watched(FolderIsland island, IslandStyle style)
    {
        var (width, height) = island.CellSize(0, style);
        var pieces = new List<(D2D_RECT_F, Painted)>();
        PaintWatch.Watching = (box, what, color) =>
        {
            pieces.Add((box, what));
            return color;
        };
        try
        {
            Offscreen.Bitmap(width, height, dc => island.PaintCell(dc, 0, style, width, height), _ => { });
        }
        finally
        {
            PaintWatch.Watching = null;
        }
        return pieces;
    }
}
