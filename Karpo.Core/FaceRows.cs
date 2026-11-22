// A folder's faces as a person sets them: the rows each face has — its options, and what it has beside
// them (the icon face's icon, the weather face's place, the markets face's own record) — and adding, removing
// and moving a face among the button's faces. The folder's own menu on the arranged bar offers them
// (OptionRows, Karpo.Dock\OptionMenu.cs; docs\widgets.md §214); Settings' folder page, which drew them
// too, is gone (§228).

namespace Karpo.Core;

/// <summary>What a row of a face is: an option of the face, or one the face has beside them.</summary>
public enum FaceRowKind
{
    /// <summary>One of the face's options (<see cref="FaceRow.Option"/>).</summary>
    Option,

    /// <summary>The icon face's icon: the face's own (<see cref="FolderFace.Glyph"/>), which nothing else shows.</summary>
    Icon,

    /// <summary>The weather face's: whether the weather is for where the device is.</summary>
    Device,

    /// <summary>The weather face's place: its own (<see cref="FolderFace.Weather"/>).</summary>
    Place,

    /// <summary>The markets face's own record (<see cref="FolderFace.Market"/>): its search and the rows what it tracks has, as its line's (MarketRows).</summary>
    Market,

    /// <summary>§477: a face's source (<see cref="FaceSources"/>): Inherited from its folder's line, or its own feed, drive or team.</summary>
    Source,
}

/// <summary>A row of a face: its kind, and the option for an option's.</summary>
public readonly record struct FaceRow(FaceRowKind Kind, FaceOption? Option = null);

/// <summary>A face's rows, and a folder's faces added, taken off and moved; see the top of the file.</summary>
public static class FaceRows
{
    /// <summary>
    /// The rows of <paramref name="face"/>, every one it has, in order: the icon's picker; the
    /// weather's place, then its options; the markets face's own rows, then its options; a face that
    /// takes a line's source its source (§477), then its options; every other
    /// face its options alone. The face's alone: the folder's own rows are the folder's (§364,
    /// OptionRows' folder rows; a row of the icon face's until then).
    /// </summary>
    public static IReadOnlyList<FaceRow> Rows(FaceInfo face)
    {
        var options = face.Options.Select(option => new FaceRow(FaceRowKind.Option, option));
        return face.Id switch
        {
            FolderFaces.IconId => [new FaceRow(FaceRowKind.Icon), .. options],
            _ when FolderFaces.HasPlace(face.Id) => [new FaceRow(FaceRowKind.Device), new FaceRow(FaceRowKind.Place), .. options],
            FolderFaces.MarketsId => [new FaceRow(FaceRowKind.Market), .. options],
            _ when FaceSources.LineOf(face) is not null => [new FaceRow(FaceRowKind.Source), .. options],
            _ => [.. options],
        };
    }

    /// <summary>
    /// Whether <paramref name="row"/> is shown for <paramref name="face"/>: always, but an option whose
    /// condition does not hold.
    /// </summary>
    public static bool Shows(FaceRow row, FolderFace face) => row.Option is not { } option || face.Info.Shows(option, face);

    /// <summary>
    /// Whether <paramref name="face"/> can be added to <paramref name="folder"/>'s faces: however many
    /// it wears (§259), but a face that shows what is the folder's one of a kind only where it does not
    /// wear it yet (<see cref="FolderFaces.CanWear"/>).
    /// </summary>
    public static bool CanAdd(FolderOptions folder, FaceInfo face) => FolderFaces.CanWear(folder, face);

    /// <summary>
    /// <paramref name="face"/> put on after <paramref name="folder"/>'s other faces: one
    /// <paramref name="remembered"/> holds (a page's, from earlier), else new. False, and nothing
    /// changed, where it cannot be (<see cref="CanAdd"/>).
    /// </summary>
    public static bool AddFace(FolderOptions folder, FaceInfo face, IReadOnlyDictionary<string, FolderFace>? remembered = null)
    {
        if (!CanAdd(folder, face))
        {
            return false;
        }
        var made = remembered is not null && remembered.TryGetValue(face.Id, out var kept) ? kept.Clone() : FolderFace.Of(face.Id);
        folder.Faces.Add(made);
        return true;
    }

    /// <summary>Takes the face at <paramref name="index"/> off; never the last: a button wears one at least. False where nothing was.</summary>
    public static bool RemoveFace(FolderOptions folder, int index)
    {
        if (folder.Faces.Count <= 1 || index < 0 || index >= folder.Faces.Count)
        {
            return false;
        }
        folder.Faces.RemoveAt(index);
        return true;
    }

    /// <summary>
    /// The face at <paramref name="index"/> taken out and put back at <paramref name="to"/>, the rest
    /// in their order, as a drag in Settings' list puts it; false where either is no place of the
    /// faces, or they are the same.
    /// </summary>
    public static bool MoveFace(FolderOptions folder, int index, int to)
    {
        var faces = folder.Faces;
        if (index < 0 || index >= faces.Count || to < 0 || to >= faces.Count || index == to)
        {
            return false;
        }
        var face = faces[index];
        faces.RemoveAt(index);
        faces.Insert(to, face);
        return true;
    }

    /// <summary>
    /// The weather face's own place (<see cref="FolderFace.Weather"/>), made where a face has none
    /// yet. Only the face's: each weather line of the folder has its own, set on its own page.
    /// </summary>
    public static WeatherOptions PlaceKept(FolderFace face) => face.Weather ??= new WeatherOptions();

    /// <summary>Whether the face's weather is for where the device is.</summary>
    public static bool UsesDevice(FolderFace face) => face.Weather?.UseDeviceLocation == true;

    /// <summary>The face's weather for where the device is, or for its place.</summary>
    public static void UseDevice(FolderFace face, bool on) => PlaceKept(face).UseDeviceLocation = on;
}
