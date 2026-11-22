// A face's source (docs\widgets.md §477; the person, 2026-11-02: "for the news face i should be able to set
// the source, as default it should be set to inherited which means it takes the source from the first folder
// widget, but you should also be able to configure it yourself without any folder widget present. same goes
// for similar widgets that inherit positions or data from the folder widgets").
//
// Some faces show what a line of their folder shows: the news face its first feed line's feed, a drive face
// its first drive line's drive (a network drive face its first network drive line's), the sports face its
// sports lines' teams and leagues (§369). Each of them has a source: the default — the line's, as it always was,
// and what a face nobody set reads as — or one of its own, kept on the face in the very record the line keeps
// it in ("rss", "drive", "sports"), so the face works with no such line in the folder at all. The dock reads
// an own source through a line of the face's own (Line), made and read as a line of the folder is.
//
// The row is the line's own picker (the feed's, the drive's, the team's search), with the default its first
// choice: "Default (BBC News)" (§518; §477 "Inherited (…)") says what it takes, and is what the row says while it is in force.

namespace Karpo.Core;

/// <summary>A face's source: Inherited from its folder's line, or its own; see the top of the file.</summary>
public static class FaceSources
{
    /// <summary>The key of a face's source row (OptionRows.OfFace).</summary>
    public const string Key = "source";

    /// <summary>The id of the line an own source is read through, which no line of a folder has: "face-news", "face-drive".</summary>
    public static string LineId(FolderFace face) => $"face-{face.Kind}";

    /// <summary>The kind of line <paramref name="face"/> takes its source from: a feed's, a drive's, a network drive's, a sports line's; null for a face that takes none.</summary>
    public static WidgetInfo? LineOf(FaceInfo face) =>
        face == FolderFaces.News ? WidgetInfos.Rss
        : FolderFaces.DriveOf(face) is { } drive ? drive
        : face == FolderFaces.Sports ? WidgetInfos.Sports
        : null;

    /// <summary>Whether <paramref name="face"/> has a source of its own; else it is Inherited.</summary>
    public static bool Owns(FolderFace face) => face.Rss is { IsSet: true } || face.Drive is { IsSet: true } || face.Sports is { IsSet: true };

    /// <summary>
    /// The line <paramref name="face"/> inherits from: <paramref name="folder"/>'s first of its kind — the sports face's
    /// first sports line of any of its four kinds (§603) —; null where there is none, or it takes no source.
    /// </summary>
    public static CellItem? Inherited(FolderOptions folder, FolderFace face) =>
        LineOf(face.Info) is { } kind ? folder.Cells.Select(cell => cell.Island).FirstOrDefault(line => Of(line, kind)) : null;

    /// <summary>Whether <paramref name="line"/> is of <paramref name="kind"/>, the sports' four kinds one (§603).</summary>
    public static bool Of(CellItem? line, WidgetInfo kind) =>
        line?.Info == kind || kind == WidgetInfos.Sports && line?.Info?.Options == WidgetOptionsKind.Sports;

    /// <summary>
    /// <paramref name="face"/>'s own source as a line of its kind holding it (<see cref="LineId"/>), what the dock reads it
    /// through as it reads a line; null where it is Inherited.
    /// </summary>
    public static CellItem? Own(FolderFace face)
    {
        if (!Owns(face) || LineOf(face.Info) is not { } kind)
        {
            return null;
        }
        var copy = face.Clone();
        var line = new CellItem { Id = LineId(face), Kind = kind.Id, View = FolderView.List, Rss = copy.Rss, Drive = copy.Drive, Sports = copy.Sports };
        line.Normalize();
        return line;
    }

    /// <summary>What <paramref name="face"/> shows: its own source, else the line it inherits from; null for neither.</summary>
    public static WidgetItem? Line(FolderOptions folder, FolderFace face) => Own(face) ?? (WidgetItem?)Inherited(folder, face);

    /// <summary>Back to Inherited: the face's own source goes. False where it had none.</summary>
    public static bool Inherit(FolderFace face)
    {
        if (!Owns(face))
        {
            return false;
        }
        (face.Rss, face.Drive, face.Sports) = (null, null, null);
        return true;
    }

    /// <summary>
    /// What the default takes, as its choice says it (§518): "Default (BBC News)" — the line's feed, drive or team —,
    /// "Default (None)" where the folder holds no such line or it is set to nothing yet.
    /// </summary>
    public static string Said(FolderOptions folder, FolderFace face) =>
        S.SettingsValueDefault(Inherited(folder, face) is { } line && NameOf(line) is { Length: > 0 } name ? name : S.SettingsWidgetActionsNone);

    /// <summary>What a line's source is called: its feed's title (its host where it has none), its drive's, its team's or league's.</summary>
    private static string NameOf(WidgetItem line) =>
        line.Rss is { IsSet: true } rss ? (rss.Title.Trim().Length > 0 ? rss.Title.Trim() : Feeds.HostOf(rss.Address))
        : line.Drive is { IsSet: true } drive ? DriveTargets.Title(drive.Label, drive.Letter)
        : line.Sports is { IsSet: true } team ? team.Name
        : "";

    /// <summary>
    /// The source row of <paramref name="folder"/>'s <paramref name="face"/>: the line's own picker for it — the feed's, the
    /// drive's (among the drives <paramref name="devices"/> list), the team's — writing the face's own record, Inherited its
    /// first choice; null for a face that takes no source.
    /// </summary>
    public static OptionRow? Row(FolderOptions folder, FolderFace face, OptionDevices? devices = null)
    {
        var kind = LineOf(face.Info);
        var picker = kind is null ? null
            : kind == WidgetInfos.Rss ? OptionFields.OfFeed(face.Rss, () => face.Rss ??= new RssOptions(), devices?.Words)
            : kind == WidgetInfos.Sports ? OptionFields.OfSportsTeam(face.Sports, () => face.Sports ??= new SportsOptions())
            // Where the drives are not listed, Inherited and its own drive alone: the row is there whatever is attached.
            : OptionRows.DriveRow(kind.Drives ?? DriveFilter.Local, face.Drive, () => face.Drive ??= new DriveTargetOptions(), devices?.Drives ?? []);
        return picker is null ? null : Inheriting(picker with { Key = Key }, Said(folder, face), Owns(face), () => Inherit(face));
    }

    /// <summary>
    /// <paramref name="row"/>, a line's picker, with Inherited (<paramref name="said"/>) its first choice — in force where the
    /// face <paramref name="owns"/> nothing — which <paramref name="inherit"/> goes back to: a choice's first, a search's
    /// first row offered whatever is typed, and what the row says while it holds nothing.
    /// </summary>
    private static OptionRow Inheriting(OptionRow row, string said, bool owns, Func<bool> inherit)
    {
        if (row.Kind == OptionRowKind.Choice)
        {
            var set = row.Set;
            return row with
            {
                Choices = [said, .. row.Choices],
                // §411 (R13): the folder's icon for what comes from the folder; the drives' after it as they were.
                ChoiceGlyphs = [OptionGlyphs.Folder, .. row.ChoiceGlyphs],
                Chosen = owns ? (row.Chosen >= 0 ? row.Chosen + 1 : -1) : 0,
                Set = at => at == 0 ? inherit() : set is not null && set(at - 1),
            };
        }
        var pick = row.Pick;
        return row with
        {
            Text = owns ? row.Text : "",
            Summary = owns ? row.Summary : null,
            Empty = said,
            Finder = row.Finder is { } finder ? new InheritingFinder(finder, new OptionFound(said, "", Mark) { Chosen = !owns, Glyph = OptionGlyphs.Folder }) : null,
            Pick = found => ReferenceEquals(found.Value, Mark) ? inherit() : pick is not null && pick(found),
        };
    }

    /// <summary>What Inherited's row in a search stands for.</summary>
    private static readonly object Mark = new();

    /// <summary>A line's finder with Inherited offered first, whatever is typed, and kept as it is: nothing to prove.</summary>
    private sealed class InheritingFinder(OptionFinder inner, OptionFound inherited) : OptionFinder
    {
        public override int LeastLetters => inner.LeastLetters;

        public override TimeSpan Wait => inner.Wait;

        public override bool Remote => inner.Remote;

        public override bool Finds => inner.Finds;

        public override bool Suggests => inner.Suggests;

        public override IReadOnlyList<OptionFound> Offered => [inherited, .. inner.Offered];

        public override IReadOnlyList<OptionFound> Suggested => inner.Suggested;

        public override Task<IReadOnlyList<OptionFound>> FindAsync(string query, CancellationToken cancel) => inner.FindAsync(query, cancel);

        public override OptionChecked? Typed(string text) => inner.Typed(text);

        public override string? Checking(OptionFound found) => ReferenceEquals(found.Value, Mark) ? null : inner.Checking(found);

        public override Task<OptionChecked> CheckAsync(OptionFound found, CancellationToken cancel) =>
            ReferenceEquals(found.Value, Mark) ? Task.FromResult(new OptionChecked(found)) : inner.CheckAsync(found, cancel);

        public override string NoneFound(string query) => inner.NoneFound(query);

        public override string Failed => inner.Failed;

        public override bool Expected(Exception error) => inner.Expected(error);
    }

    /// <summary>
    /// A face's own source in place as a file may hold it: only on the face that takes one of its kind, and only where it
    /// is one (a feed's address, a drive, a team or a league); anything else is Inherited and not written.
    /// </summary>
    internal static void Normalize(FolderFace face)
    {
        var kind = LineOf(face.Info);
        face.Sports?.Normalize();
        face.Rss = kind == WidgetInfos.Rss && face.Rss is { IsSet: true } ? face.Rss : null;
        face.Drive = kind is not null && FolderFaces.DriveOf(face.Info) == kind && face.Drive is { IsSet: true } ? face.Drive : null;
        face.Sports = kind == WidgetInfos.Sports && face.Sports is { IsSet: true } ? face.Sports : null;
    }
}
