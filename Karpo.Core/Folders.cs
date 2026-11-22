// Folders: lists of widgets behind one button on the bar. A folder is an island of kind
// FolderList whose options hold its lists, side by side, left to right; each list holds its
// widgets in their order, top to bottom, and each widget is an island of its own (its own id and
// options). The grid folder is gone (2026-10-09), and so are the columns and rows its places were
// kept in: a folder is an ordered list of ordered lists (2026-10-09, "several lists in one
// folder"), which is what is drawn, what is stored, and all the arithmetic there is.
//
// The rule a folder keeps: it has at least one list. A list may be empty — a spacer between two
// with widgets (2026-10-11: "there can be empty columns"), or a slot whose width was set — and
// nobody deletes a column (2026-10-12: "deleting a column should not even be possible anymore").
// But a move or a removal that leaves empty lists outside the first and the last with widgets drops
// them (Trim, §328): the slots there are as good, and they piled up unseen, one per far slot a widget
// went through (the person's News folder had seven).
//
// Everything here is shared by the dock (a "+" of an open folder, a line carried in it), the
// settings app and Config.Repair, which makes any file's folders keep the rule (Tidy).

using System.Text.Json.Serialization;

namespace Karpo.Core;

/// <summary>
/// How a folder shows its widgets: its kind's view (<see cref="Folders.View"/>). Never stored: the
/// folder's kind says it. A list is the one view left since the grid went (2026-10-09).
/// </summary>
public enum FolderView
{
    /// <summary>A list: each widget's icon with its name always beside it.</summary>
    [JsonStringEnumMemberName("list")] List,
}

/// <summary>
/// Where a widget stands, or is to go, in a folder: list <paramref name="List"/> (from 0, left to
/// right) at <paramref name="Index"/> (from 0, top down). With <paramref name="New"/> it is a new
/// list of its own, put in at <paramref name="List"/> — 0 before the first, the number of lists
/// after the last — and <paramref name="Index"/> is not read; <paramref name="Apart"/> empty lists
/// then stand between it and the others (<see cref="Slot"/>).
/// </summary>
public readonly record struct FolderPlace(int List, int Index, bool New = false, int Apart = 0)
{
    /// <summary>A new list of its own, put in at <paramref name="at"/> among the lists.</summary>
    public static FolderPlace NewList(int at) => new(at, 0, New: true);

    /// <summary>
    /// edit: an empty slot beside an arranged folder's lists (2026-10-12: the slots repeat to the
    /// screen's edges, the side "+" gone): a new list at <paramref name="at"/> — 0 for a slot on the
    /// left, the number of lists for one on the right — <paramref name="apart"/> slots out from the
    /// nearest, the slots between becoming empty lists, so the new one stands where its slot stood.
    /// </summary>
    public static FolderPlace Slot(int at, int apart) => new(at, 0, New: true, Apart: Math.Max(0, apart));
}

/// <summary>
/// How wide one list of a folder is: one of five, chosen beside the list's "+" while the folder is
/// arranged (2026-10-10: "you should be able to change the width of individual columns in the
/// folder view") and on the folder's Settings page. Medium is the panels' own width and what a list
/// is until somebody chooses. Presets only (2026-10-18, the person: "lets not let them decide the
/// width, just add like 2 more widths as selection"): extra wide and widest came then, after the
/// three that were, so a file of those three reads and writes as it did.
/// </summary>
[JsonConverter(typeof(TolerantEnumConverter<FolderListWidth>))]
public enum FolderListWidth
{
    [JsonStringEnumMemberName("medium")] Medium,
    [JsonStringEnumMemberName("narrow")] Narrow,
    [JsonStringEnumMemberName("wide")] Wide,
    [JsonStringEnumMemberName("extraWide")] ExtraWide,
    [JsonStringEnumMemberName("widest")] Widest,
}

/// <summary>
/// Where an open folder stands along its bar (§385; the person, 2026-10-29: "you should be able to set
/// the alignment of a folder. e.g. left right center and auto, in auto it means that the folder alignes
/// to the face that opens the folder"). Automatic, centred on its face, is where every folder stood
/// before, so a file without it reads as it always did. Left and Right are the screen's, in every
/// language; down a side bar they are its top and its bottom.
/// </summary>
[JsonConverter(typeof(TolerantEnumConverter<FolderAlign>))]
public enum FolderAlign
{
    /// <summary>Centred on the face that opened it, kept inside the screen.</summary>
    [JsonStringEnumMemberName("auto")] Auto,

    /// <summary>At the start of the work area: its left, a side bar's top.</summary>
    [JsonStringEnumMemberName("left")] Left,

    /// <summary>In the middle of the work area.</summary>
    [JsonStringEnumMemberName("center")] Center,

    /// <summary>At the end of the work area: its right, a side bar's bottom.</summary>
    [JsonStringEnumMemberName("right")] Right,
}

/// <summary>A folder's four alignments, in the order its menu offers them, with their names and icons.</summary>
public static class FolderAligns
{
    /// <summary>Automatic, then the screen's left, middle and right.</summary>
    public static IReadOnlyList<FolderAlign> All { get; } = [FolderAlign.Auto, FolderAlign.Left, FolderAlign.Center, FolderAlign.Right];

    /// <summary>
    /// What <paramref name="align"/> is called: Left and Right as a bar's edges are, the screen's; §589 along a bar on a
    /// display's <paramref name="side"/>, Top and Bottom, where the folder stands then.
    /// </summary>
    public static string Name(FolderAlign align, bool side) => align switch
    {
        FolderAlign.Left => side ? S.CoreEdgeTop : S.CoreEdgeLeft,
        FolderAlign.Center => S.CoreAlignCenter,
        FolderAlign.Right => side ? S.CoreEdgeBottom : S.CoreEdgeRight,
        _ => S.DockFolderAlignAuto,
    };

    /// <summary>
    /// Its icon: Automatic's, and Word's three, never mirrored — they are the screen's sides; §589 along a bar on a display's
    /// <paramref name="side"/>, the three a quarter turn (<see cref="OptionGlyphs.AlignTop"/>).
    /// </summary>
    public static string Glyph(FolderAlign align, bool side) => align switch
    {
        FolderAlign.Left => side ? OptionGlyphs.AlignTop : OptionGlyphs.AlignLeft,
        FolderAlign.Center => side ? OptionGlyphs.AlignMiddle : OptionGlyphs.AlignCenter,
        FolderAlign.Right => side ? OptionGlyphs.AlignBottom : OptionGlyphs.AlignRight,
        _ => OptionGlyphs.Automatic,
    };
}

/// <summary>
/// §647: how an open folder stands beside its bar (the person, 2026-11-09: "for every folder you should be able to also set a
/// folder style. right now how it is should be the default one"). A file without it reads as Cards, as every folder stood.
/// </summary>
[JsonConverter(typeof(TolerantEnumConverter<FolderStyle>))]
public enum FolderStyle
{
    /// <summary>Its lists cards of their own, floating off the bar by the flyouts' gap: the folder as it always was.</summary>
    [JsonStringEnumMemberName("cards")] Cards,

    /// <summary>
    /// Its lists on a solid ground in the bar's colour that adjoins the bar: down a side bar each column the work area's
    /// whole height, along a bottom or top bar the folder's width up to its farthest line; the bar seems to grow into it.
    /// </summary>
    [JsonStringEnumMemberName("attached")] Attached,
}

/// <summary>§647: a folder's two styles, in the order its row offers them, with their names and icons.</summary>
public static class FolderStyles
{
    /// <summary>Cards, then Attached.</summary>
    public static IReadOnlyList<FolderStyle> All { get; } = [FolderStyle.Cards, FolderStyle.Attached];

    /// <summary>What <paramref name="style"/> is called.</summary>
    public static string Name(FolderStyle style) => style == FolderStyle.Attached ? S.DockFolderStyleAttached : S.DockFolderStyleCards;

    /// <summary>Its icon: cards apart, or the taskbar the folder joins.</summary>
    public static string Glyph(FolderStyle style) => style == FolderStyle.Attached ? OptionGlyphs.Taskbar : OptionGlyphs.Tiles;

    /// <summary>
    /// §656 (the person, 2026-11-09: "in dock mode you should not be able to have the folder docked too"): whether a folder on a
    /// bar of <paramref name="layout"/> has a style to choose: on a taskbar yes, on a dock (<see cref="LayoutType.Dock"/>) no.
    /// </summary>
    public static bool Offered(TaskbarLayout? layout) => layout?.Type != LayoutType.Dock;

    /// <summary>
    /// §656: the style a folder of <paramref name="style"/> opens in beside its bar: its own beside a taskbar, Cards beside a
    /// <paramref name="dock"/> (the bar's <c>Layout.Floating</c>, a <see cref="LayoutType.Dock"/> layout's). Its own is kept: the
    /// layout a taskbar again, it is worn again.
    /// </summary>
    public static FolderStyle Worn(FolderStyle style, bool dock) => dock ? FolderStyle.Cards : style;
}

/// <summary>The five list widths in DIPs, and the order a menu offers them in.</summary>
public static class FolderListWidths
{
    /// <summary>
    /// Narrow, in DIPs: the narrowest the lines' own designs keep whole — the month's seven columns,
    /// a row of three switches with their names, screen time's bars and their hours.
    /// </summary>
    public const int NarrowDip = 280;

    /// <summary>
    /// Medium, in DIPs: the width every panel of an island's content is (Flyout.PanelWidthDip), so a
    /// panel converted into a list folder is the width of the panel it replaces.
    /// </summary>
    public const int MediumDip = 360;

    /// <summary>Wide, in DIPs: a third wider than medium, room for a long agenda line or a note.</summary>
    public const int WideDip = 480;

    /// <summary>Extra wide, in DIPs: a third wider than wide again — a week's days with their words, a long agenda.</summary>
    public const int ExtraWideDip = 640;

    /// <summary>
    /// Widest, in DIPs: a quarter wider than extra wide. Wider than a small display has room for
    /// beside another list; there the folder runs past the screen's edge and says so, and its width
    /// menu says so before (Karpo.Dock, FolderFlyout.OffScreen.cs; §320, §328).
    /// </summary>
    public const int WidestDip = 800;

    /// <summary>Narrow to widest: the order the menu beside a list's "+" and Settings offer them in.</summary>
    public static IReadOnlyList<FolderListWidth> All { get; } =
        [FolderListWidth.Narrow, FolderListWidth.Medium, FolderListWidth.Wide, FolderListWidth.ExtraWide, FolderListWidth.Widest];

    /// <summary>How wide <paramref name="width"/> is in DIPs; one this build does not know is medium.</summary>
    public static int Dip(FolderListWidth width) => width switch
    {
        FolderListWidth.Narrow => NarrowDip,
        FolderListWidth.Wide => WideDip,
        FolderListWidth.ExtraWide => ExtraWideDip,
        FolderListWidth.Widest => WidestDip,
        _ => MediumDip,
    };

    /// <summary>What <paramref name="width"/> is called, in the width menu and on the folder's Settings page alike.</summary>
    public static string Name(FolderListWidth width) => width switch
    {
        FolderListWidth.Narrow => S.DockFolderWidthNarrow,
        FolderListWidth.Wide => S.DockFolderWidthWide,
        FolderListWidth.ExtraWide => S.DockFolderWidthExtraWide,
        FolderListWidth.Widest => S.DockFolderWidthWidest,
        _ => S.DockFolderWidthMedium,
    };
}

/// <summary>A folder: its name and face on the bar, its lists and how wide each is.</summary>
public sealed class FolderOptions
{
    /// <summary>Segoe Fluent Icons: Folder.</summary>
    public const string FolderGlyph = "\uE8B7";

    /// <summary>
    /// The most rows a line of a list is measured in: the sanity bound a hand-edited file can't get
    /// past, not a limit anybody meets.
    /// </summary>
    public const int Most = 64;

    /// <summary>
    /// The most cells a component's line of a list may start with (<see cref="ListWidget.Range"/>):
    /// twelve rows, 480 DIP, about a panel.
    /// </summary>
    public const int MostCells = 24;

    /// <summary>
    /// How tall one cell of a list's lines is, in DIPs at the list's text: half the list's row (§442), so every line
    /// is whole cells and n cells are n times one, with no gap between lines or cells. Times the scale and the text
    /// scale, on whole px: 20 lands on them at every scale Windows offers (20, 25, 30, 35, 40 px …). Fixed, not a
    /// setting.
    /// </summary>
    public const int CellDip = 20;

    /// <summary>
    /// What its button on the bar shows, side by side in this order (2026-10-11, the person: "for a
    /// folder you should be actually able to give him multiple faces, so that i can also combine
    /// widgets as i want"): each a face (<see cref="FolderFaces"/>) — its icon, a clock, the date,
    /// the weather … — with its own options. At least one; its icon until somebody chooses.
    /// <para>
    /// The file keeps its first as "face", as it always did, and all of them as "faces" only where
    /// there are more than one; reading, "faces" wins. A file of one face is the file it was, and an
    /// older build reading one of several wears the first.
    /// </para>
    /// </summary>
    [JsonIgnore]
    public List<FolderFace> Faces
    {
        get => faces;
        set => faces = value is { Count: > 0 } ? value : [new FolderFace()];
    }

    private List<FolderFace> faces = [new FolderFace()];

    // Whether the file had "faces", which a "face" read after it must not undo.
    private bool facesRead;

    /// <summary>
    /// Its first face (<see cref="Faces"/>): the only one of most folders, and the one a face's
    /// rows on the bar and a new folder's choice are about.
    /// </summary>
    [JsonIgnore]
    public FolderFace Face
    {
        get => faces[0];
        set => faces[0] = value ?? new FolderFace();
    }

    /// <summary>"face" in the file: the first face, read where the file has no "faces".</summary>
    [JsonInclude]
    [JsonPropertyName("face")]
    internal FolderFace? FirstFace
    {
        get => faces[0];
        set
        {
            if (value is not null && !facesRead)
            {
                faces = [value];
            }
        }
    }

    /// <summary>"faces" in the file: every face, written only where there are more than one.</summary>
    [JsonInclude]
    [JsonPropertyName("faces")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    internal List<FolderFace>? AllFaces
    {
        get => faces.Count > 1 ? faces : null;
        set
        {
            if (value is { Count: > 0 })
            {
                faces = value;
                facesRead = true;
            }
        }
    }

    /// <summary>Its first face of <paramref name="kind"/> (<see cref="FaceInfo.Id"/>), or null where it wears none.</summary>
    public FolderFace? FaceOf(string kind) => faces.FirstOrDefault(face => face?.Kind == kind);

    /// <summary>Whether it wears <paramref name="face"/> among its faces.</summary>
    public bool Wears(FaceInfo face) => faces.Any(worn => worn?.Info == face);

    /// <summary>
    /// The lists, left to right, each its widgets top to bottom. At least one; any may be empty, a
    /// column kept for widgets to come, or a spacer (<see cref="Folders.Tidy"/>).
    /// </summary>
    public List<List<FolderCell>> Lists { get; set; } = [[]];

    /// <summary>
    /// How wide each list is (<see cref="FolderListWidth"/>), beside <see cref="Lists"/>: the first
    /// for the first list, and so on. A list with no entry, or with one this build does not know, is
    /// <see cref="FolderListWidth.Medium"/> (<see cref="WidthOf"/>), so a file without it reads as
    /// every list the width it always was. Null, and not written, until a list is given a width of
    /// its own. Beside the lists rather than in them, so a list stays the array of lines it is in
    /// the file; <see cref="Folders"/> moves an entry with its list.
    /// <para>
    /// There is no height to set (2026-10-09: "folder height = automatic only"): a list is as tall
    /// as what is in it, the folder as tall as its tallest list, and a list taller than a panel may
    /// be scrolls on its own.
    /// </para>
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<FolderListWidth>? Widths { get; set; }

    /// <summary>
    /// §385: where it stands along its bar when it opens (<see cref="FolderAlign"/>). Automatic, and not
    /// written, until somebody chooses; a word the file has that this build does not know is Automatic.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public FolderAlign Align { get; set; }

    /// <summary>
    /// §647: how it stands beside its bar when it opens (<see cref="FolderStyle"/>). Cards, and not written, until somebody
    /// chooses; a word the file has that this build does not know is Cards.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public FolderStyle Style { get; set; }

    /// <summary>How wide list <paramref name="list"/> is: its entry in <see cref="Widths"/>, else medium.</summary>
    public FolderListWidth WidthOf(int list) =>
        Widths is { } widths && list >= 0 && list < widths.Count && Enum.IsDefined(widths[list]) ? widths[list] : FolderListWidth.Medium;

    /// <summary>
    /// Every widget of the folder in reading order — the first list's top to bottom, then the next
    /// list's — which is the order the keys walk and a 2×2 face shows. A copy: what a folder holds
    /// changes through <see cref="Lists"/>, by <see cref="Folders"/>.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<FolderCell> Cells =>
        [.. (Lists ?? []).Where(list => list is not null).SelectMany(list => list).Where(cell => cell?.Island is not null)];

    /// <summary>Its faces, its widgets' look, a width per list, and lists that keep the rule (<see cref="Folders.Tidy"/>).</summary>
    internal void Normalize()
    {
        // A face this build does not know is kept, and read as its icon (FolderFaces.Of); a
        // hand-written list of none is its icon. As many as there are: a button has no most (§259).
        Faces = [.. faces.Where(face => face is not null)];
        facesRead = false;
        foreach (var face in faces)
        {
            face.Normalize();
        }
        Align = Enum.IsDefined(Align) ? Align : FolderAlign.Auto;
        Style = Enum.IsDefined(Style) ? Style : FolderStyle.Cards;
        _ = Folders.Tidy(this);
    }
}

/// <summary>One widget of a folder: a line of its list.</summary>
public sealed class FolderCell
{
    /// <summary>
    /// The widget in it, a line of the list (<see cref="CellItem.View"/>). Written as "island", as
    /// it always was.
    /// </summary>
    public CellItem Island { get; set; } = new();
}

/// <summary>What a folder allows, and the edits the dock and the settings app make to it.</summary>
public static class Folders
{
    /// <summary>
    /// The view a folder of <paramref name="kind"/> opens in (<see cref="BarEntry.Opens"/>); a list
    /// for anything else, since the callers ask about a folder and a list is the one there is.
    /// </summary>
    public static FolderView View(BarKind kind) => BarEntries.Find(kind)?.Opens ?? FolderView.List;

    /// <summary>
    /// Why a widget whose word is <paramref name="kind"/> cannot be added to a folder of
    /// <paramref name="folderKind"/>; empty when it can. One reason: a list has no line of it
    /// (<see cref="Holds"/>). The catalogue greys the entry and puts this on hover, so "not here"
    /// and "not at all" are told apart, and a refused add says something rather than nothing.
    /// </summary>
    public static string WhyNotAdded(BarKind folderKind, string kind) =>
        Holds(View(folderKind), kind) ? "" : S.CoreFolderNotInViewList(WidgetInfos.Find(kind)?.Title ?? kind);

    /// <summary>
    /// Whether a widget of <paramref name="kind"/> may go into a folder of
    /// <paramref name="folderKind"/>: one its view's registry has (<see cref="ListWidgets"/>, asked
    /// through <see cref="Holds"/>), as often as you like. A folder has no limit.
    /// </summary>
    public static bool CanAdd(BarKind folderKind, string kind) => Holds(View(folderKind), kind);

    /// <summary>
    /// Whether a folder of <paramref name="view"/> has a form of <paramref name="kind"/> (a widget's
    /// word, "clock"): a line in a list (<see cref="ListWidgets"/>).
    /// </summary>
    public static bool Holds(FolderView view, string kind) =>
        view == FolderView.List && ListWidgets.Find(kind) is not null;

    /// <summary>
    /// Puts <paramref name="island"/> into <paramref name="folder"/> at the end of its last list, the
    /// end of its reading order. Null, and nothing changed, when the kind can't go into a folder of
    /// <paramref name="folderKind"/> (<see cref="CanAdd"/>).
    /// </summary>
    public static FolderCell? Add(BarKind folderKind, FolderOptions folder, CellItem island)
    {
        Tidy(folder);
        var last = folder.Lists.Count - 1;
        return InsertLine(folderKind, folder, island, new FolderPlace(last, folder.Lists[last].Count));
    }

    /// <summary>
    /// <paramref name="island"/> put into <paramref name="folder"/> at <paramref name="at"/>: a place
    /// of a list, where the line there and those after it move down one, or a new list of its own
    /// (<see cref="FolderPlace.New"/>). A place past a list's end is its end, and a list past the
    /// last is the last. The widget then stands in a list (<see cref="CellItem.View"/>). Null, and
    /// nothing changed, when the kind has no line (<see cref="CanAdd"/>).
    /// </summary>
    public static FolderCell? InsertLine(BarKind folderKind, FolderOptions folder, CellItem island, FolderPlace at)
    {
        if (!CanAdd(folderKind, island.Kind))
        {
            return null;
        }
        var cell = new FolderCell { Island = island };
        Put(folder, cell, at);
        island.View = View(folderKind);
        return cell;
    }

    /// <summary>
    /// <paramref name="cell"/> put into <paramref name="folder"/> at <paramref name="at"/>, held to
    /// the lists there are (<see cref="InsertLine"/>).
    /// </summary>
    private static void Put(FolderOptions folder, FolderCell cell, FolderPlace at)
    {
        Tidy(folder);
        var lists = folder.Lists;
        if (at.New)
        {
            InsertNew(folder, Math.Clamp(at.List, 0, lists.Count), at.Apart, [cell]);
            return;
        }
        var list = lists[Math.Clamp(at.List, 0, lists.Count - 1)];
        list.Insert(Math.Clamp(at.Index, 0, list.Count), cell);
    }

    /// <summary>Where <paramref name="cell"/> stands in <paramref name="folder"/>, or null where it is none of its.</summary>
    public static FolderPlace? PlaceOf(FolderOptions folder, FolderCell cell)
    {
        for (var list = 0; list < (folder.Lists?.Count ?? 0); list++)
        {
            var at = folder.Lists![list]?.IndexOf(cell) ?? -1;
            if (at >= 0)
            {
                return new FolderPlace(list, at);
            }
        }
        return null;
    }

    /// <summary>
    /// Takes <paramref name="cell"/> out of <paramref name="folder"/>, and the empty lists that leaves
    /// outside the first and the last with widgets (<see cref="Trim"/>); a list between them stays,
    /// empty where it was the last there. Where it stood, for putting it back
    /// (<see cref="InsertLine"/>) — a new list there where its list went — or null where it was none
    /// of the folder's.
    /// </summary>
    public static FolderPlace? Take(FolderOptions folder, FolderCell cell)
    {
        Tidy(folder);
        if (Lift(folder, cell) is not { } at)
        {
            return null;
        }
        var (from, to) = Kept(folder);
        _ = Trim(folder);
        return at.List < from ? FolderPlace.NewList(0)
            : at.List > to ? FolderPlace.NewList(to - from + 1)
            : at with { List = at.List - from };
    }

    /// <summary><paramref name="cell"/> out of its list, every list kept: where it stood, or null where it is none of the folder's.</summary>
    private static FolderPlace? Lift(FolderOptions folder, FolderCell cell)
    {
        if (PlaceOf(folder, cell) is not { } at)
        {
            return null;
        }
        folder.Lists[at.List].RemoveAt(at.Index);
        return at;
    }

    /// <summary>
    /// The lists <see cref="Trim"/> keeps: from the first with widgets to the last; the first alone
    /// where none has any.
    /// </summary>
    private static (int From, int To) Kept(FolderOptions folder)
    {
        var first = folder.Lists.FindIndex(list => list is { Count: > 0 });
        return first < 0 ? (0, 0) : (first, folder.Lists.FindLastIndex(list => list is { Count: > 0 }));
    }

    /// <summary>
    /// Drops <paramref name="folder"/>'s empty lists before its first list with widgets and after
    /// its last, each with its width (§328); one between two with widgets stays, a spacer, its width
    /// kept. A folder with no widget keeps its first list. What a move (<see cref="MoveGroup"/>) and a
    /// removal (<see cref="Take"/>) leave. A slot's width (<see cref="SetSlotWidth"/>) and a move into a far
    /// slot still make the lists between them and the others. True when any went.
    /// </summary>
    public static bool Trim(FolderOptions folder)
    {
        Tidy(folder);
        var (from, to) = Kept(folder);
        var changed = false;
        for (var at = folder.Lists.Count - 1; at > to; at--)
        {
            CutList(folder, at);
            changed = true;
        }
        for (var at = from - 1; at >= 0; at--)
        {
            CutList(folder, at);
            changed = true;
        }
        return changed;
    }

    /// <summary>
    /// Which list the empty slot <paramref name="slot"/> (<see cref="FolderPlace.Slot"/>) of a folder
    /// of <paramref name="lists"/> lists becomes when a width or a widget makes it one: the first
    /// for a slot on the left, the slots between it and the lists empty ones after it; on the right
    /// the one past those (<see cref="InsertNew"/>).
    /// </summary>
    public static int ListOfSlot(int lists, FolderPlace slot)
    {
        var at = Math.Clamp(slot.List, 0, Math.Max(0, lists));
        return at == 0 && lists > 0 ? 0 : at + Math.Max(0, slot.Apart);
    }

    /// <summary>
    /// Moves <paramref name="cell"/> to <paramref name="to"/>, read as the folder stands <b>before</b>
    /// the move: a place of a list is the gap above the line that stands there now (the list's count
    /// for below the last), so a line carried down its own list and let go above another lands just
    /// above that one; a new list (<see cref="FolderPlace.New"/>) goes in between the lists it was
    /// asked between. A list the move empties stays while the move is read, an empty column — so a new
    /// list is always a move, beside the line's own list too (2026-10-12: "not every column in the
    /// folder edit view is a drop target") — and goes after it where it is outside the first and the
    /// last with widgets (<see cref="Trim"/>, §328). False, and nothing changed, where the cell is none
    /// of the folder's or the move would leave everything as it is: just above or below itself in its
    /// own list. The move of one line is the move of several (<see cref="MoveGroup"/>) with one in it:
    /// one rule for both.
    /// </summary>
    public static bool Move(FolderOptions folder, FolderCell cell, FolderPlace to) => MoveGroup(folder, [cell], to);

    /// <summary>
    /// <see cref="MoveKeepingLists"/>, and then the empty lists it left outside the first and the last
    /// with widgets dropped (<see cref="Trim"/>, §328): what letting go of carried lines does.
    /// </summary>
    public static bool MoveGroup(FolderOptions folder, IReadOnlyList<FolderCell> cells, FolderPlace to)
    {
        if (!MoveKeepingLists(folder, cells, to))
        {
            return false;
        }
        _ = Trim(folder);
        return true;
    }

    /// <summary>
    /// edit: several lines carried together (the person, 2026-10-12: "dragging multiple widgets in
    /// folder edit mode should work"): <paramref name="cells"/> moved to <paramref name="to"/>, one
    /// under the other in the order given, read as <see cref="Move"/> reads a place — the gap above
    /// the line standing there now that is none of them, the list's end where none is; a new list
    /// (<see cref="FolderPlace.New"/>) of them all, always a move (<see cref="Move"/>). Every list
    /// stays, one the move empties too: what a drag shows while the lines are carried, the lists
    /// where they stand (Karpo.Dock, FolderDrag.Moved); letting go trims (<see cref="MoveGroup"/>).
    /// Cells the folder does not have are passed over. False, and nothing changed, where none of them
    /// is the folder's or they would stand exactly where they stand.
    /// </summary>
    public static bool MoveKeepingLists(FolderOptions folder, IReadOnlyList<FolderCell> cells, FolderPlace to)
    {
        Tidy(folder);
        var moving = cells.Where(cell => cell is not null && PlaceOf(folder, cell) is not null).Distinct().ToList();
        if (moving.Count == 0)
        {
            return false;
        }
        var lists = folder.Lists;
        if (to.New)
        {
            var at = Math.Clamp(to.List, 0, lists.Count);
            // The lists they leave stay, so a new list is a move wherever it goes (Move).
            foreach (var cell in moving)
            {
                _ = Lift(folder, cell);
            }
            InsertNew(folder, at, to.Apart, moving);
            return true;
        }
        var target = Math.Clamp(to.List, 0, lists.Count - 1);
        var into = lists[target];
        var gap = Math.Clamp(to.Index, 0, into.Count);
        // Above the first line at or after the gap that stays; the list's end where none does.
        var anchor = into.Skip(gap).FirstOrDefault(cell => !moving.Contains(cell));
        var before = lists.Select(list => list.ToList()).ToList();
        foreach (var cell in moving)
        {
            _ = Lift(folder, cell);
        }
        into.InsertRange(anchor is null ? into.Count : into.IndexOf(anchor), moving);
        return !lists.Select((list, index) => list.SequenceEqual(before[index])).All(same => same);
    }

    /// <summary>
    /// List <paramref name="list"/> of <paramref name="folder"/> is <paramref name="width"/> wide
    /// from now on (<see cref="FolderOptions.Widths"/>). False, and nothing changed, for a list the
    /// folder does not have or a width it is already.
    /// </summary>
    public static bool SetWidth(FolderOptions folder, int list, FolderListWidth width)
    {
        Tidy(folder);
        if (list < 0 || list >= folder.Lists.Count || !Enum.IsDefined(width) || folder.WidthOf(list) == width)
        {
            return false;
        }
        WidthsUpTo(folder)[list] = width;
        return true;
    }

    /// <summary><paramref name="folder"/>'s widths with an entry for every list at least, the missing ones medium, as they read.</summary>
    private static List<FolderListWidth> WidthsUpTo(FolderOptions folder)
    {
        folder.Widths ??= [];
        while (folder.Widths.Count < folder.Lists.Count)
        {
            folder.Widths.Add(FolderListWidth.Medium);
        }
        return folder.Widths;
    }

    /// <summary>
    /// The empty slot <paramref name="slot"/> (<see cref="FolderPlace.Slot"/>) of an arranged folder
    /// made an empty list <paramref name="width"/> wide where it stood (the person, 2026-10-12:
    /// "every column that you see on the screen, even if empty you should be able to change its
    /// width"), the slots between it and the lists empty medium ones, as adding into it does. False,
    /// and nothing changed, for a place that is no slot or for medium — a slot's own width, which
    /// changes nothing on the screen.
    /// </summary>
    public static bool SetSlotWidth(FolderOptions folder, FolderPlace slot, FolderListWidth width)
    {
        Tidy(folder);
        if (!slot.New || !Enum.IsDefined(width) || width == FolderListWidth.Medium)
        {
            return false;
        }
        InsertNew(folder, Math.Clamp(slot.List, 0, folder.Lists.Count), slot.Apart, [], width);
        return true;
    }

    /// <summary>
    /// A new list of <paramref name="cells"/> (one widget, or none) put in at <paramref name="at"/>,
    /// <paramref name="width"/> wide, with <paramref name="apart"/> empty lists between it and the
    /// others (<see cref="FolderPlace.Slot"/>): after it where it goes first, before it anywhere else.
    /// Those are medium wide, a slot's width.
    /// </summary>
    private static void InsertNew(FolderOptions folder, int at, int apart, List<FolderCell> cells, FolderListWidth width = FolderListWidth.Medium)
    {
        var to = ListOfSlot(folder.Lists.Count, FolderPlace.Slot(at, apart));
        for (var each = 0; each < apart; each++)
        {
            InsertList(folder, at, [], FolderListWidth.Medium);
        }
        InsertList(folder, to, cells, width);
    }

    /// <summary>A list put in among <paramref name="folder"/>'s at <paramref name="at"/>, <paramref name="width"/> wide: its width beside it.</summary>
    private static void InsertList(FolderOptions folder, int at, List<FolderCell> list, FolderListWidth width)
    {
        // A folder whose lists are all medium keeps writing no widths.
        if (folder.Widths is not null || width != FolderListWidth.Medium)
        {
            WidthsUpTo(folder).Insert(at, width);
        }
        folder.Lists.Insert(at, list);
    }

    /// <summary>List <paramref name="at"/> of <paramref name="folder"/> taken out, and its width with it.</summary>
    private static void CutList(FolderOptions folder, int at)
    {
        if (folder.Widths is { } widths && at < widths.Count)
        {
            widths.RemoveAt(at);
        }
        folder.Lists.RemoveAt(at);
    }

    /// <summary>
    /// Makes <paramref name="folder"/>'s lists keep the rule: no missing list, no line without a
    /// widget, and one list at least — a folder of none is one empty list. An empty list stays: it
    /// is a column kept for widgets to come (2026-10-11). A missing list that goes takes its width
    /// (<see cref="FolderOptions.Widths"/>) with it, and no width is kept past the last list. True
    /// when anything changed.
    /// </summary>
    public static bool Tidy(FolderOptions folder)
    {
        var changed = false;
        if (folder.Lists is null)
        {
            folder.Lists = [];
            changed = true;
        }
        foreach (var list in folder.Lists)
        {
            changed |= list?.RemoveAll(cell => cell?.Island is null) > 0;
        }
        for (var at = folder.Lists.Count - 1; at >= 0; at--)
        {
            if (folder.Lists[at] is null)
            {
                CutList(folder, at);
                changed = true;
            }
        }
        if (folder.Lists.Count == 0)
        {
            folder.Lists = [[]];
            changed = true;
        }
        if (folder.Widths is { } widths && widths.Count > folder.Lists.Count)
        {
            widths.RemoveRange(folder.Lists.Count, widths.Count - folder.Lists.Count);
            changed = true;
        }
        return changed;
    }

    /// <summary>
    /// The island <paramref name="path"/> names in <paramref name="list"/>: an island's id, or
    /// "&lt;folder id&gt;/&lt;widget id&gt;" for a widget in a folder of the list. Ids compare
    /// without case, as everywhere in the config.
    /// </summary>
    public static WidgetItem? Find(IEnumerable<BarItem> list, string path)
    {
        var cut = path.IndexOf('/');
        if (cut < 0)
        {
            return list.FirstOrDefault(island => island is not null && string.Equals(island.Id, path, StringComparison.OrdinalIgnoreCase));
        }
        return FolderIn(list, path[..cut]) is { } folder ? CellOf(folder, path[(cut + 1)..])?.Island : null;
    }

    /// <summary>
    /// list: how many of the folder's cells a line of <paramref name="kind"/> is tall where nothing
    /// has measured its content (<see cref="ListWidget.Cells"/>): a row for the list's own row, and
    /// what a component lifted out of a panel says it needs (docs\widgets.md §9). A list is one
    /// column, so a line has no columns to declare.
    /// <para>
    /// It answers <b>how tall</b> and nothing else. <see cref="CardInList(ListKind)"/> answers which
    /// drawing a line takes, and it used to be this number — a line of one row drew the list's own
    /// icon-and-name row — which meant a component could not ask for a short line without losing
    /// its drawing, and so the day switcher was padded to twice the height its content wanted.
    /// </para>
    /// </summary>
    public static int ListCellsOf(this ListKind kind) => Math.Clamp(ListWidgets.Of(kind).Cells, LineHeight.Fewest, LineHeight.Most);

    /// <summary>
    /// list: the cells a new line of <paramref name="kind"/> starts with in a list folder — the
    /// widget's measurement on that surface (<see cref="ListWidget.Range"/>), and only where a new
    /// line starts (<see cref="WidgetBounds.DefaultFor"/>): one row to its most, so the content
    /// decides everything in between. A person may set either end anywhere from 1 to
    /// <see cref="LineHeight.Most"/>, and is not held to this. <see cref="ListCellsOf(ListKind)"/>
    /// is what the line is when nothing has measured its content, and it lies inside this.
    /// <para>
    /// The list's own row of an icon and a name is one line whatever anybody asks for: one row to
    /// one row, nothing to choose.
    /// </para>
    /// </summary>
    public static WidgetBounds ListRange(this ListKind kind)
    {
        var widget = ListWidgets.Of(kind);
        if (widget.Line != ListLine.Card)
        {
            return new WidgetBounds(LineHeight.Row, LineHeight.Row);
        }
        var most = Math.Clamp(widget.Range.Most, LineHeight.Fewest, LineHeight.Most);
        return new WidgetBounds(Math.Clamp(widget.Range.Least, LineHeight.Fewest, most), most);
    }

    /// <summary>
    /// list: whether a line of <paramref name="kind"/> is <b>its own card</b>, rather than the
    /// list's own row of an icon and a name (<see cref="ListWidget.Line"/>).
    /// <para>
    /// A component's is — a piece of a panel has nothing else it could be drawn as: the
    /// icon-and-name row is the folder's drawing of somebody else's widget, and a day switcher
    /// rendered as "Day switcher" with a calendar glyph is not a day switcher. A widget that is in a
    /// list for its click gets the row, which is what a list has always been.
    /// </para>
    /// <para>
    /// <b>Which drawing a line takes is a question about the widget; how tall the line is is a
    /// question about its content.</b> This answers the first and <see cref="ListCellsOf(ListKind)"/>
    /// the second. They used to be one expression, <c>ListCellsOf(kind) &gt; 1</c>, so a component
    /// could keep its own drawing <i>only</i> by claiming two rows — and the day switcher, which is
    /// one line of chrome, was padded to twice the height of anything in it.
    /// </para>
    /// </summary>
    public static bool CardInList(this ListKind kind) => ListWidgets.Of(kind).Line == ListLine.Card;

    /// <summary>
    /// list: whether a line of <paramref name="kind"/> has a height somebody can choose: a line that
    /// is the widget's own card (<see cref="CardInList"/>), but a divider, whose line is its hairline
    /// and the air round it whatever it is given, so a height to choose would be a setting that does
    /// nothing; but a heading, which is a section heading's height and no count of rows (the person,
    /// 2026-10-11: "the heading should not have a configurable size"), a height a file still holds for
    /// one dropped as it is read; and but a kind whose line is always its rows
    /// (<see cref="ListWidget.FixedHeight"/>); and but a kind of one form or a few (<see cref="ListWidget.Forms"/>, §432),
    /// which is its form's whole rows and offers a size, if anything. Every other card may be one row to the folder's room (§192).
    /// </summary>
    public static bool HasHeight(this ListKind kind) =>
        kind.CardInList() && kind is not (ListKind.Divider or ListKind.Heading) && ListWidgets.Of(kind) is { FixedHeight: false, Forms: null };

    /// <summary>The options of the folder <paramref name="id"/> in <paramref name="list"/>, or null.</summary>
    public static FolderOptions? FolderIn(IEnumerable<BarItem> list, string id) =>
        list.FirstOrDefault(island => island is { Folder: not null } && string.Equals(island.Id, id, StringComparison.OrdinalIgnoreCase))?.Folder;

    /// <summary>
    /// The projection (S5): stands every widget of <paramref name="folder"/> in
    /// <paramref name="view"/> (<see cref="CellItem.View"/>), which is what the view's registry has
    /// of it (<see cref="ListWidgets"/>), and hands back the cells whose
    /// widget that view has no form of — the ones a folder read from a file drops. Which view a
    /// widget stands in is not in the file, so this is where every folder that is read gets it.
    /// </summary>
    public static List<FolderCell> Project(FolderOptions folder, FolderView view)
    {
        var misses = new List<FolderCell>();
        foreach (var cell in folder.Cells ?? [])
        {
            if (cell?.Island is not { } island)
            {
                continue;
            }
            if (Holds(view, island.Kind))
            {
                island.View = view;
            }
            else
            {
                misses.Add(cell);
            }
        }
        return misses;
    }

    /// <summary>The cell of <paramref name="folder"/> holding the widget <paramref name="id"/>, or null.</summary>
    public static FolderCell? CellOf(FolderOptions folder, string id) =>
        folder.Cells.FirstOrDefault(cell => cell?.Island is not null && string.Equals(cell.Island.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The folder's widgets in reading order, list by list and each top to bottom
    /// (<see cref="FolderOptions.Cells"/>): the order the keys walk and a 2×2 face shows.
    /// </summary>
    public static IReadOnlyList<FolderCell> ReadingOrder(FolderOptions folder) => folder.Cells;

    /// <summary>
    /// A new widget whose word is <paramref name="kind"/> in <paramref name="holder"/>'s folder, made
    /// as the folder's view has it, at <paramref name="at"/> when one was asked for (a "+" of the
    /// folder was pressed: a place of a list, or a new list) and else at the end of its last list.
    /// Its path, or null when the folder's view has no such widget. The one body the dock's add list
    /// adds into a folder with.
    /// <para>
    /// Its id is one no island of <paramref name="list"/> or its folders has (<see cref="FreeId"/>).
    /// </para>
    /// </summary>
    public static string? AddNew(IEnumerable<BarItem> list, BarItem holder, string kind, FolderPlace? at = null) =>
        // Made for the folder's type: a widget made straight into a list starts where its line
        // starts, which for a taskbar entry is not the width it starts at on the bar.
        ListWidgets.Find(kind)?.Kind.New() is { } island ? AddNew(list, holder, island, at) : null;

    /// <summary>
    /// <see cref="AddNew(IEnumerable{BarItem}, BarItem, string, FolderPlace?)"/> for a widget made
    /// already: a premade widget, set up as it says (<see cref="PremadeWidget.New"/>).
    /// </summary>
    public static string? AddNew(IEnumerable<BarItem> list, BarItem holder, CellItem island, FolderPlace? at = null)
    {
        if (holder.Folder is not { } folder || !holder.Kind.IsFolder())
        {
            return null;
        }
        island.Id = FreeId(list, island.Id);
        var added = at is { } place ? InsertLine(holder.Kind, folder, island, place) : Add(holder.Kind, folder, island);
        return added is null ? null : Path(holder.Id, island.Id);
    }

    /// <summary>"&lt;folder id&gt;/&lt;widget id&gt;": how <see cref="Find"/> and the settings app's links name a widget in a folder.</summary>
    public static string Path(string folderId, string widgetId) => $"{folderId}/{widgetId}";

    /// <summary>
    /// Takes the widget <paramref name="path"/> names out of <paramref name="list"/> (off the bar,
    /// or out of its folder, <see cref="Take"/>).
    /// The island that was taken out, or null when there was none.
    /// </summary>
    public static WidgetItem? Remove(List<BarItem> list, string path)
    {
        var cut = path.IndexOf('/');
        if (cut < 0)
        {
            var index = list.FindIndex(island => island is not null && string.Equals(island.Id, path, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                return null;
            }
            var island = list[index];
            list.RemoveAt(index);
            return island;
        }
        if (FolderIn(list, path[..cut]) is not { } folder || CellOf(folder, path[(cut + 1)..]) is not { } cell)
        {
            return null;
        }
        _ = Take(folder, cell);
        return cell.Island;
    }

    /// <summary>
    /// Every island of <paramref name="list"/> with the path <see cref="Find"/> takes: those on
    /// the bar, then each folder's widgets. For looking one up, and for ids that must be unique.
    /// </summary>
    public static IEnumerable<(string Path, WidgetItem Island)> All(IEnumerable<BarItem> list)
    {
        foreach (var island in list)
        {
            if (island is null)
            {
                continue;
            }
            yield return (island.Id, island);
            foreach (var cell in island.Folder?.Cells ?? [])
            {
                if (cell?.Island is { } inside)
                {
                    yield return (Path(island.Id, inside.Id), inside);
                }
            }
        }
    }

    /// <summary>
    /// A new id for a widget going into <paramref name="list"/>: <paramref name="id"/> when no
    /// island of the list or its folders has it, else a new one.
    /// </summary>
    public static string FreeId(IEnumerable<BarItem> list, string id)
    {
        var taken = Ids(list);
        while (string.IsNullOrWhiteSpace(id) || id.Contains('/') || taken.Contains(id))
        {
            id = WidgetItem.NewId();
        }
        return id;
    }

    /// <summary>
    /// The ids <paramref name="list"/> and its folders hold, told apart as a widget's id is (case
    /// aside). Of a whole config (<see cref="Config.AllIslands"/>) they are the ids no widget put in may have:
    /// an id is unique across every layout (<see cref="Config.Repair"/>).
    /// </summary>
    public static HashSet<string> Ids(IEnumerable<BarItem> list) =>
        All(list).Select(entry => entry.Island.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// §481: which of a folder's two menus a setting of it stands in (the person, 2026-11-02: "it should open the configuration
/// menu like it does for the other taskbar widgets and then it should show only the faces and face appearance settings ...
/// then when i open the folder it should show the folder options with the menu on top like it does already").
/// </summary>
public enum FolderSide
{
    /// <summary>
    /// What reaches the bar, its face's menu, a click on it in edit mode: its faces, each face's own rows (§567: no look of
    /// its own).
    /// </summary>
    Face,

    /// <summary>What reaches the folder opened, its panel's menu, Folder ▾: its own rows (where it stands as it opens, …).</summary>
    Panel,
}