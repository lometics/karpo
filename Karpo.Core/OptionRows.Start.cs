// §641: a Start widget's settings on its page (docs\widgets.md §641): All apps' view, what Recommended lists, the folders
// beside power. The pins are set where they stand — pinned, unpinned, dragged — not here.

namespace Karpo.Core;

public static partial class OptionRows
{
    public const string StartViewRow = "StartView";
    public const string StartFiles = "StartFiles";
    public const string StartNewApps = "StartNewApps";

    /// <summary>A folder beside power, then the place's word (<see cref="StartPlace"/>).</summary>
    public const string StartPlacePrefix = "StartPlace.";

    /// <summary>The rows of a Start widget of <paramref name="item"/>'s kind.</summary>
    private static void StartRows(WidgetItem item, List<OptionRow> rows)
    {
        if (ListWidgets.Find(item.Info?.Id) is not { } line)
        {
            return;
        }
        var kind = line.Kind;
        bool Write(Action<StartOptions> change)
        {
            var options = item.Start ?? new StartOptions();
            change(options);
            item.Start = options.Normalized(kind);
            return true;
        }
        switch (kind)
        {
            case ListKind.StartApps:
            {
                StartView[] views = [StartView.Category, StartView.Grid, StartView.List];
                var shown = item.Start?.ViewShown ?? StartView.Category;
                rows.Add(new OptionRow(StartViewRow, OptionRowKind.Choice, S.SettingsPinViewHeader)
                {
                    Glyph = OptionGlyphs.Size,
                    Slot = OptionSlot.Look,
                    Choices = [.. views.Select(StartViewName)],
                    Chosen = Array.IndexOf(views, shown),
                    Set = at => at >= 0 && at < views.Length && views[at] != shown && Write(options => options.View = views[at]),
                });
                break;
            }
            case ListKind.StartRecommended:
                rows.Add(Toggled(StartFiles, OptionSlot.Subject, WidgetInfos.FolderContents.Glyph, S.SettingsWidgetStartFiles, item.Start?.Files != false,
                    on => Write(options => options.Files = on ? null : false)));
                rows.Add(Toggled(StartNewApps, OptionSlot.Subject, WidgetInfos.StartApps.Glyph, S.SettingsWidgetStartNewApps, item.Start?.NewApps != false,
                    on => Write(options => options.NewApps = on ? null : false)));
                break;
            case ListKind.StartProfile:
            {
                // A switch a folder, in Windows' order, as Start › Folders lists them.
                var shown = StartPlaces.Of(item.Start);
                foreach (var place in Enum.GetValues<StartPlace>())
                {
                    rows.Add(Toggled(StartPlacePrefix + place, OptionSlot.Subject, StartPlaces.Glyph(place), StartPlaceName(place), shown.Contains(place),
                        on => Write(options => options.Places = [.. StartPlaces.Of(options).Where(each => each != place), .. on ? [place] : Array.Empty<StartPlace>()])));
                }
                break;
            }
        }
    }

    /// <summary>§641: All apps' view, as Start's View dropdown names it.</summary>
    public static string StartViewName(StartView view) => view switch
    {
        StartView.Grid => S.SettingsAppearanceStylesPatternGrid,
        StartView.List => S.SettingsPinViewList,
        _ => S.DockStartViewCategory,
    };

    /// <summary>§641: a folder beside power, as Windows' Start › Folders names it.</summary>
    public static string StartPlaceName(StartPlace place) => place switch
    {
        StartPlace.Settings => S.CoreBarMenuWindowsSettings,
        StartPlace.FileExplorer => S.CoreBarMenuFileExplorer,
        StartPlace.Documents => S.CoreStartPlaceDocuments,
        StartPlace.Downloads => S.CoreStartPlaceDownloads,
        StartPlace.Music => S.CoreStartPlaceMusic,
        StartPlace.Pictures => S.CoreStartPlacePictures,
        StartPlace.Videos => S.CoreStartPlaceVideos,
        StartPlace.Network => S.CoreStartPlaceNetwork,
        _ => S.CoreStartPlacePersonal,
    };
}
