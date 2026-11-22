// A face's source (docs\widgets.md §477; the person, 2026-11-02: "for the news face i should be able to set the
// source, as default it should be set to inherited which means it takes the source from the first folder widget,
// but you should also be able to configure it yourself without any folder widget present"). Every face that takes
// a line's source — found from the registry, not listed by hand — is Inherited unless set: it shows its folder's
// line's, as it always did. Its own, picked with the line's own picker, it shows with no such line in the folder;
// picked back to Inherited it follows the line again; and the file keeps it in the line's own record. Nothing is
// fetched: the feeds are RssSamples', the teams SavedSports', the drives made up.

using System.Text.Json;
using Karpo.Dock;
using Karpo.Dock.Rss;
using Karpo.Dock.Sports;
using Karpo.Tests.Core;
using Karpo.Tests.Islands;
using Karpo.Tests.Localization;
using Karpo.Tests.Rss;
using Karpo.Tests.Sports;
using Karpo.Tests.Storage;

namespace Karpo.Tests.Folders;

[Collection(InLanguage.Collection)]
public sealed class FaceSourceTests
{
    private static readonly DateTime Now = new(2026, 9, 26, 15, 0, 0);

    private static readonly OptionDevices Devices = new([], [])
    {
        Drives =
        [
            new DriveVolume(@"\\?\Volume{1}\", "C:", "Local Disk", DriveKind.Fixed, 100, 50),
            new DriveVolume(@"\\?\Volume{2}\", "D:", "Data", DriveKind.Fixed, 100, 50),
        ],
    };

    private static RssOptions SampleFeed() => new() { Address = RssSamples.Address, Title = "Example News" };

    private static RssOptions OtherFeed() => new() { Address = "https://other.example.org/feed.xml", Title = "Other News" };

    /// <summary>A folder wearing <paramref name="face"/>, holding <paramref name="lines"/>.</summary>
    private static BarItem Folder(string face, params CellItem[] lines)
    {
        var holder = Configs.Holding(lines);
        holder.Id = face;
        holder.Folder!.Faces = [FolderFace.Of(face)];
        holder.Normalize();
        return holder;
    }

    private static CellItem FeedLine(RssOptions? feed = null)
    {
        var line = ListKind.Rss.New();
        line.Id = "feed";
        line.Rss = feed ?? SampleFeed();
        return line;
    }

    private static CellItem DriveLine(string letter = "C:", string label = "Local Disk")
    {
        var line = ListKind.Drive.New();
        line.Id = "drive";
        line.Drive = new DriveTargetOptions { Volume = $@"\\?\Volume{{{(letter == "C:" ? 1 : 2)}}}\", Letter = letter, Label = label };
        return line;
    }

    private static CellItem SportsLine(SportsOptions team)
    {
        var line = ListKind.Sports.New();
        line.Id = "sports";
        line.Sports = team;
        return line;
    }

    // --- Which faces --------------------------------------------------------------------------------

    /// <summary>
    /// Every face of the registry that reads a line of its folder is one with a source: the news face a feed's, the drive
    /// faces a drive's of their kind, the sports face the teams'. The rest take none, and have no source row.
    /// </summary>
    [Fact]
    public void TheFacesThatReadALineEachHaveASource()
    {
        var taking = FolderFaces.All.Where(face => FaceSources.LineOf(face) is not null).ToDictionary(face => face.Id, face => FaceSources.LineOf(face)!);
        Assert.Equal(
            new Dictionary<string, WidgetInfo>
            {
                [FolderFaces.NewsId] = WidgetInfos.Rss,
                [FolderFaces.DriveId] = WidgetInfos.Drive,
                [FolderFaces.NetworkDriveId] = WidgetInfos.NetworkDrive,
                [FolderFaces.SportsId] = WidgetInfos.Sports,
            },
            taking);
        foreach (var face in FolderFaces.All)
        {
            var holder = Folder(face.Id);
            var hasRow = OptionRows.Find(OptionRows.OfFace(holder.Folder!, 0, Devices), FaceSources.Key) is not null;
            Assert.True(hasRow == taking.ContainsKey(face.Id), $"{face.Id}: a source row {hasRow}");
        }
    }

    // --- News ---------------------------------------------------------------------------------------

    /// <summary>
    /// A news face nobody set is Inherited: it shows the folder's first feed line, and its row says so — "Default (Example
    /// News)", greyed at its end, the first row of its search, ticked.
    /// </summary>
    [Fact]
    public void ANewsFaceInheritsItsFoldersFirstFeed()
    {
        var holder = Folder(FolderFaces.NewsId, FeedLine(), FeedLine(OtherFeed()));
        var folder = holder.Folder!;
        var face = folder.Faces[0];
        Assert.False(FaceSources.Owns(face));
        Assert.Same(folder.Cells[0].Island, FaceSources.Line(folder, face));
        Assert.Equal(S.SettingsValueDefault("Example News"), FaceSources.Said(folder, face));

        var row = OptionRows.Find(OptionRows.OfFace(folder, 0, Devices), FaceSources.Key)!;
        Assert.Equal(OptionRowKind.Search, row.Kind);
        Assert.Equal(S.SettingsRssAddressHeader, row.Title);
        Assert.Equal("", row.Text);
        Assert.Equal(S.SettingsValueDefault("Example News"), row.Empty);
        var inherited = row.Finder!.Offered[0];
        Assert.Equal((S.SettingsValueDefault("Example News"), true), (inherited.Title, inherited.Chosen));
        // Picked while it is in force, nothing changes.
        Assert.False(row.Pick!(inherited));

        // A folder with no feed line: Inherited alone.
        var bare = Folder(FolderFaces.NewsId);
        Assert.Null(FaceSources.Line(bare.Folder!, bare.Folder!.Faces[0]));
        Assert.Equal(S.SettingsValueDefault(S.SettingsWidgetActionsNone), FaceSources.Said(bare.Folder!, bare.Folder!.Faces[0]));
    }

    /// <summary>
    /// A feed of its own, picked with the feed line's own picker, is the face's with no feed line in the folder at all:
    /// kept in the face's "rss" record, read through a line of the face's own; Inherited again goes back to the line.
    /// </summary>
    [Fact]
    public void ANewsFacesOwnFeedNeedsNoLineAndInheritedFollowsTheLineAgain()
    {
        var holder = Folder(FolderFaces.NewsId);
        var folder = holder.Folder!;
        var face = folder.Faces[0];
        var row = OptionRows.Find(OptionRows.OfFace(folder, 0, Devices), FaceSources.Key)!;
        // The line's picker: what is offered after Inherited is the feed line's own (the well-known feeds).
        var line = OptionFields.OfFeed(null, () => new RssOptions());
        Assert.Equal(line.Finder!.Suggested.Select(found => found.Title), row.Finder!.Suggested.Select(found => found.Title));
        // A feed proved (the field reads it before it keeps it), kept as the line keeps one.
        Assert.True(row.Pick!(new OptionFound("Example News", "", new ProvedFeed(RssSamples.Address, "Example News"))));

        Assert.True(FaceSources.Owns(face));
        Assert.Equal(RssSamples.Address, face.Rss!.Address);
        var own = Assert.IsType<CellItem>(FaceSources.Line(folder, face));
        Assert.Equal((FaceSources.LineId(face), WidgetInfos.Rss, RssSamples.Address), (own.Id, own.Info, own.Rss!.Address));

        // Its row says the feed, Inherited no longer ticked.
        row = OptionRows.Find(OptionRows.OfFace(folder, 0, Devices), FaceSources.Key)!;
        Assert.Equal(RssSamples.Address, row.Text);
        Assert.False(row.Finder!.Offered[0].Chosen);

        // A feed line comes: the face keeps its own.
        folder.Put(FeedLine(OtherFeed()));
        Assert.Equal(RssSamples.Address, FaceSources.Line(folder, face)!.Rss!.Address);

        // Inherited, picked: the line's again, and nothing of its own left to write.
        Assert.True(row.Pick!(row.Finder.Offered[0]));
        Assert.False(FaceSources.Owns(face));
        Assert.Null(face.Rss);
        Assert.Equal(OtherFeed().Address, FaceSources.Line(folder, face)!.Rss!.Address);
    }

    // --- Drives -------------------------------------------------------------------------------------

    /// <summary>
    /// A drive face's source is the drive line's own choice of the drives, "Default (Local Disk (C:))" its first: picked,
    /// a drive of its own with no drive line; Inherited again, the line's.
    /// </summary>
    [Fact]
    public void ADriveFacesSourceIsTheDriveLinesChoiceWithInheritedFirst()
    {
        var holder = Folder(FolderFaces.DriveId, DriveLine());
        var folder = holder.Folder!;
        var face = folder.Faces[0];
        var row = OptionRows.Find(OptionRows.OfFace(folder, 0, Devices), FaceSources.Key)!;
        Assert.Equal(OptionRowKind.Choice, row.Kind);
        Assert.Equal(S.SettingsWidgetDriveHeader, row.Title);
        var said = S.SettingsValueDefault(DriveTargets.Title("Local Disk", "C:"));
        Assert.Equal([said, DriveTargets.Title("Local Disk", "C:"), DriveTargets.Title("Data", "D:")], row.Choices);
        Assert.Equal(0, row.Chosen);

        Assert.True(row.Set!(2));
        Assert.True(FaceSources.Owns(face));
        Assert.Equal("D:", face.Drive!.Letter);
        Assert.Equal("D:", FaceSources.Line(folder, face)!.Drive!.Letter);
        row = OptionRows.Find(OptionRows.OfFace(folder, 0, Devices), FaceSources.Key)!;
        Assert.Equal(2, row.Chosen);

        // With no drive line at all.
        folder.Lists = [[]];
        Assert.Equal("D:", FaceSources.Line(folder, face)!.Drive!.Letter);

        Assert.True(row.Set!(0));
        Assert.False(FaceSources.Owns(face));
        Assert.Null(FaceSources.Line(folder, face));
    }

    // --- Sports -------------------------------------------------------------------------------------

    /// <summary>
    /// The sports face follows its folder's sports lines while Inherited (§369: all of them), a team of its own once one is
    /// picked with the line's search — with no sports line — and the lines again once Inherited is.
    /// </summary>
    [Fact]
    public void ASportsFaceFollowsItsOwnTeamOrItsLines()
    {
        var holder = Folder(FolderFaces.SportsId, SportsLine(SavedSports.Barcelona()));
        var folder = holder.Folder!;
        var face = folder.Faces[0];
        Assert.Equal(["soccer/esp.1/83"], FolderIsland.SportsAll(folder).Select(team => team.Key));
        var row = OptionRows.Find(OptionRows.OfFace(folder, 0, Devices), FaceSources.Key)!;
        Assert.Equal((S.SettingsWidgetSportsTeamHeader, S.SettingsValueDefault("Barcelona")), (row.Title, row.Empty));

        Assert.True(row.Pick!(new OptionFound("Bayern Munich", "", new SportsTeam("soccer", "ger.1", "132", "Bayern Munich", "MUN", ""))));
        folder.Lists = [[]];
        Assert.Equal(["soccer/ger.1/132"], FolderIsland.SportsAll(folder).Select(team => team.Key));
        Assert.Equal("soccer/ger.1/132", FolderIsland.SportsOf(folder)!.Key);

        folder.Put(SportsLine(SavedSports.Barcelona()));
        Assert.Equal(["soccer/ger.1/132"], FolderIsland.SportsAll(folder).Select(team => team.Key));
        row = OptionRows.Find(OptionRows.OfFace(folder, 0, Devices), FaceSources.Key)!;
        Assert.True(row.Pick!(row.Finder!.Offered[0]));
        Assert.Equal(["soccer/esp.1/83"], FolderIsland.SportsAll(folder).Select(team => team.Key));
    }

    // --- The file -----------------------------------------------------------------------------------

    /// <summary>
    /// An own source is written in the line's own record on the face — "rss", "drive", "sports" — and read back as written;
    /// Inherited writes nothing; a record on a face that takes no such source, or one that is no source, is Inherited.
    /// </summary>
    [Fact]
    public void AnOwnSourceIsWrittenAsTheLineKeepsItAndInheritedIsNothing()
    {
        var news = Folder(FolderFaces.NewsId);
        news.Folder!.Faces[0].Rss = SampleFeed();
        var drive = Folder(FolderFaces.NetworkDriveId);
        drive.Folder!.Faces[0].Drive = new DriveTargetOptions { Volume = @"\\server\share", Letter = "Z:", Label = "share" };
        var sports = Folder(FolderFaces.SportsId);
        sports.Folder!.Faces[0].Sports = SavedSports.Bayern();
        var inherited = Folder(FolderFaces.NewsId, FeedLine());
        inherited.Id = "inherited";
        var config = Configs.With(news, drive, sports, inherited);

        using (var written = JsonDocument.Parse(config.ToJson()))
        {
            var saved = written.RootElement.GetProperty("layouts")[0].GetProperty("islands");
            JsonElement FaceOf(int at) => saved[at].GetProperty("folder").GetProperty("face");
            Assert.Equal(RssSamples.Address, FaceOf(0).GetProperty("rss").GetProperty("address").GetString());
            Assert.Equal("Z:", FaceOf(1).GetProperty("drive").GetProperty("letter").GetString());
            Assert.Equal("132", FaceOf(2).GetProperty("sports").GetProperty("team").GetString());
            Assert.False(FaceOf(3).TryGetProperty("rss", out _));
        }
        var back = Configs.RoundTrip(config, out var changed);
        Assert.False(changed);
        var islands = Configs.Islands(back);
        Assert.Equal(RssSamples.Address, islands[0].Folder!.Faces[0].Rss!.Address);
        Assert.Equal(@"\\server\share", islands[1].Folder!.Faces[0].Drive!.Volume);
        Assert.Equal("soccer/ger.1/132", islands[2].Folder!.Faces[0].Sports!.Key);
        Assert.False(FaceSources.Owns(islands[3].Folder!.Faces[0]));

        // A clock face carrying a feed, a news face carrying an address that is none: Inherited, and not written.
        var odd = Folder(FolderFaces.ClockId);
        odd.Folder!.Faces[0].Rss = SampleFeed();
        var none = Folder(FolderFaces.NewsId);
        none.Folder!.Faces[0].Rss = new RssOptions { Address = "not a feed" };
        odd.Normalize();
        none.Normalize();
        Assert.Null(odd.Folder.Faces[0].Rss);
        Assert.Null(none.Folder.Faces[0].Rss);
    }

    internal static BarItem FolderOf(string face, params CellItem[] lines) => Folder(face, lines);

    internal static CellItem FeedOf() => FeedLine();

    internal static RssOptions Sample() => SampleFeed();
}

/// <summary>§477 on the bar: a face's own source read through a line of its own, made as a folder's lines are.</summary>
[Collection(AttachedDrivesCollection.Collection)]
public sealed class FaceSourceBarTests
{
    private static readonly DateTime Now = new(2026, 9, 26, 15, 0, 0);

    private static BarItem Folder(string face, params CellItem[] lines) => FaceSourceTests.FolderOf(face, lines);

    private static CellItem FeedLine() => FaceSourceTests.FeedOf();

    private static RssOptions SampleFeed() => FaceSourceTests.Sample();

    /// <summary>
    /// On the bar: a news face with a feed of its own and no feed line draws its feed's newest headline, read through a
    /// line of its own (RssSamples' store, nothing fetched); Inherited, it follows the folder's feed line again.
    /// </summary>
    [UiFact]
    public void OnTheBarANewsFacesOwnFeedIsReadWithNoLine()
    {
        Offscreen.OnStaThread(gfx =>
        {
            Clock.Now = () => Now;
            var holder = Folder(FolderFaces.NewsId);
            holder.Folder!.Faces[0].Rss = SampleFeed();
            holder.Normalize();
            var key = Config.IslandKey(null, holder.Id);
            RssIsland.Made = RssSamples.Settled(Now);
            try
            {
                using var folder = new FolderIsland(key, holder, gfx, new Config());
                _ = ((IIsland)folder).Refresh(gfx);
                Assert.Empty(folder.Tiles);
                var own = Assert.IsType<RssIsland>(folder.Feed);
                Assert.Same(folder.Widget($"{key}/{FaceSources.LineId(holder.Folder.Faces[0])}"), own);
                Assert.NotEqual("", RssIsland.FaceReading(own));
                Assert.Same(own, folder.FaceWidget);

                // Inherited, with a feed line: the line's.
                var inherited = Folder(FolderFaces.NewsId, FeedLine());
                inherited.Id = holder.Id;
                folder.Configure(inherited, new Config());
                _ = ((IIsland)folder).Refresh(gfx);
                Assert.Same(folder.Tiles.Single().Island, folder.Feed);
                Assert.Null(folder.Widget($"{key}/{FaceSources.LineId(holder.Folder.Faces[0])}"));
            }
            finally
            {
                RssIsland.Made = null;
            }
        });
    }

    /// <summary>A drive face's own drive is read through a drive line of its own, with none in the folder.</summary>
    [UiFact]
    public void OnTheBarADriveFacesOwnDriveIsReadWithNoLine()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var before = DriveWatch.Source;
            DriveWatch.Source = (_, _) => [];
            try
            {
                var holder = Folder(FolderFaces.DriveId);
                holder.Folder!.Faces[0].Drive = new DriveTargetOptions { Volume = @"\\?\Volume{2}\", Letter = "D:", Label = "Data" };
                holder.Normalize();
                var key = Config.IslandKey(null, holder.Id);
                using var folder = new FolderIsland(key, holder, gfx, new Config());
                Assert.Empty(folder.Tiles);
                var own = Assert.IsType<DriveIsland>(folder.DriveLine);
                Assert.Same(folder.Widget($"{key}/{FaceSources.LineId(holder.Folder.Faces[0])}"), own);
            }
            finally
            {
                DriveWatch.Source = before;
            }
        });
    }

    /// <summary>A sports face's own team is what it shows with no sports line, from the store's answer (a stand-in, nothing fetched).</summary>
    [UiFact]
    public void OnTheBarASportsFacesOwnTeamIsShownWithNoLine()
    {
        Offscreen.OnStaThread(gfx =>
        {
            Clock.Now = () => Now;
            var holder = Folder(FolderFaces.SportsId);
            holder.Folder!.Faces[0].Sports = SavedSports.Barcelona();
            holder.Normalize();
            FolderIsland.SportsStand = new SportsHeld(SavedSports.BarcelonaAnswer(), Now, null, false);
            try
            {
                using var folder = new FolderIsland(Config.IslandKey(null, holder.Id), holder, gfx, new Config());
                var shown = folder.SportsNow();
                Assert.NotNull(shown);
            }
            finally
            {
                FolderIsland.SportsStand = null;
            }
        });
    }
}
