// The face gallery's groups, search and what it lets be picked (Karpo.Core\FaceGallery.cs).

using Karpo.Core;
using Karpo.Tests.Localization;

namespace Karpo.Tests.Core;

public sealed class FaceGalleryTests
{
    /// <summary>
    /// Every face stands in one group of its kind, none in "Other" (a new face is given its group on
    /// purpose); with nothing typed the gallery shows every face once, the groups in their order and
    /// each group's faces in the order the faces are kept.
    /// </summary>
    [Fact]
    public void EveryFaceIsInAGroupOnce()
    {
        Assert.All(FolderFaces.All, face => Assert.NotEqual(FaceGroup.Other, FaceGallery.GroupOf(face)));
        var found = FaceGallery.Search(new FolderOptions(), null, "");
        Assert.Equal(FolderFaces.All.OrderBy(face => FaceGallery.GroupOf(face)).ThenBy(face => FolderFaces.All.ToList().IndexOf(face)),
            found.SelectMany(group => group.Tiles).Select(tile => tile.Face));
        Assert.Equal(found.Select(group => group.Group).Order(), found.Select(group => group.Group));
        Assert.Equal(FaceGroup.Folder, found[0].Group);
        // The folder itself is its icon alone: what is inside is no face any more (§254), and not offered.
        Assert.Equal([FolderFaces.Icon], found[0].Tiles.Select(tile => tile.Face));
        Assert.DoesNotContain(found.SelectMany(group => group.Tiles), tile => tile.Face.Id == "content");
        Assert.Null(FolderFaces.Find("content"));
        Assert.All(Enum.GetValues<FaceGroup>(), group => Assert.False(FaceGallery.Title(group).StartsWith("settings.", StringComparison.Ordinal)));
    }

    /// <summary>
    /// The search finds a face by its name, by what it shows, by its group's name and by the words
    /// of the widget it shows; every word typed must be found; a group with nothing found is left
    /// out, and nothing found is no group at all.
    /// </summary>
    [Fact]
    public void TheSearchFindsByNameBlurbGroupAndTheWidgetsWords()
    {
        var folder = new FolderOptions();
        IReadOnlyList<FaceInfo> Found(string query) => [.. FaceGallery.Search(folder, null, query).SelectMany(group => group.Tiles).Select(tile => tile.Face)];

        Assert.Contains(FolderFaces.Wifi, Found("wi-fi"));
        Assert.Contains(FolderFaces.Wifi, Found("WIFI"));
        Assert.Equal([FolderFaces.Markets], Found("ticker"));
        Assert.Contains(FolderFaces.Battery, Found("percentage"));
        Assert.Equal(
            [FolderFaces.Battery, FolderFaces.Power, FolderFaces.Cpu, FolderFaces.Gpu, FolderFaces.Memory, FolderFaces.Disk, FolderFaces.NetworkActivity, FolderFaces.ScreenTime],
            Found("performance"));
        Assert.Equal([FolderFaces.Clock, FolderFaces.DateTime], Found("time clock").Where(face => face == FolderFaces.Clock || face == FolderFaces.DateTime));
        Assert.DoesNotContain(FolderFaces.Wifi, Found("wifi stopwatch"));
        Assert.Empty(FaceGallery.Search(folder, null, "zzzqqq"));
        Assert.Single(FaceGallery.Search(folder, null, "stopwatch"));
    }

    /// <summary>
    /// Adding: a face the folder wears already and shows once is greyed with why, one that repeats
    /// is not; there is no most (§259). Changing: the face in that place is
    /// marked and can be picked, another worn elsewhere is greyed, the same face worn in that place
    /// only is offered.
    /// </summary>
    [Fact]
    public void WhatCannotBeWornIsGreyedWithWhy()
    {
        var folder = new FolderOptions { Face = FolderFace.Of(FolderFaces.WifiId) };
        folder.Faces.Add(FolderFace.Of(FolderFaces.ClockId));

        var wifi = FaceGallery.Tile(folder, null, FolderFaces.Wifi);
        Assert.False(wifi.Available);
        Assert.Equal(S.SettingsFolderFacesWorn, wifi.Reason);
        Assert.True(FaceGallery.Tile(folder, null, FolderFaces.Clock).Available);
        Assert.True(FaceGallery.Tile(folder, null, FolderFaces.Sound).Available);
        Assert.Equal("", FaceGallery.Tile(folder, null, FolderFaces.Sound).Reason);

        var here = FaceGallery.Tile(folder, 0, FolderFaces.Wifi);
        Assert.True(here.Available);
        Assert.True(here.Current);
        Assert.False(FaceGallery.Tile(folder, 1, FolderFaces.Wifi).Available);
        Assert.False(FaceGallery.Tile(folder, 1, FolderFaces.Sound).Current);
        Assert.True(FaceGallery.Tile(folder, 1, FolderFaces.Clock).Current);

        // However many it wears, a face it may wear again is offered, and one it shows once still greyed.
        while (folder.Faces.Count < 20)
        {
            folder.Faces.Add(FolderFace.Of(FolderFaces.CpuId));
        }
        Assert.True(FaceGallery.Tile(folder, null, FolderFaces.Cpu).Available);
        Assert.True(FaceGallery.Tile(folder, null, FolderFaces.Icon).Available);
        Assert.True(FaceGallery.Tile(folder, null, FolderFaces.Sound).Available);
        Assert.Equal(S.SettingsFolderFacesWorn, FaceGallery.Tile(folder, null, FolderFaces.Wifi).Reason);
        Assert.True(FaceGallery.Tile(folder, 2, FolderFaces.Sound).Available);
    }

    /// <summary>Enter in the search box picks the one face found that can be picked, and nothing where there are more or none.</summary>
    [Fact]
    public void EnterPicksTheOnlyFace()
    {
        var folder = new FolderOptions { Face = FolderFace.Of(FolderFaces.WifiId) };
        Assert.Equal(FolderFaces.Stopwatch, FaceGallery.Only(folder, null, "what it has counted"));
        Assert.Null(FaceGallery.Only(folder, null, "drive"));
        Assert.Null(FaceGallery.Only(folder, null, "zzzqqq"));
        // The Wi-Fi face is found but already worn: nothing to pick.
        Assert.Null(FaceGallery.Only(folder, null, "wi-fi"));
    }
}

/// <summary>The gallery's group names and its search in another language.</summary>
[Collection(InLanguage.Collection)]
public sealed class FaceGalleryLanguageTests
{
    /// <summary>In German the groups have German names, and a face is found by its German name and by its English one.</summary>
    [Fact]
    public void InGermanTheGroupsAreGermanAndEnglishStillFinds()
    {
        using var _ = InLanguage.Use("de");
        Assert.Equal("Zeit und Kalender", FaceGallery.Title(FaceGroup.Time));
        var folder = new FolderOptions();
        Assert.Contains(FolderFaces.Stopwatch, FaceGallery.Search(folder, null, "Stoppuhr").SelectMany(group => group.Tiles).Select(tile => tile.Face));
        Assert.Contains(FolderFaces.Stopwatch, FaceGallery.Search(folder, null, "stopwatch").SelectMany(group => group.Tiles).Select(tile => tile.Face));
    }
}
