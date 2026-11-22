// §642: Windows search's words, in the language Karpo speaks: its groups' names (the chips', the headings', a typed
// prefix's), and what a result says under its name.

namespace Karpo.Core;

/// <summary>§642: Windows search's words.</summary>
public static class WinSearchWords
{
    /// <summary>A group's name: a heading, a chip, a prefix typed in the language Karpo speaks.</summary>
    public static string Category(WinSearchCategory category) => category switch
    {
        WinSearchCategory.Answer => S.CoreWinSearchCategoryAnswer,
        WinSearchCategory.Apps => S.CoreWinSearchCategoryApps,
        WinSearchCategory.Settings => S.CoreWinSearchCategorySettings,
        WinSearchCategory.Documents => S.CoreWinSearchCategoryDocuments,
        WinSearchCategory.Folders => S.CoreWinSearchCategoryFolders,
        WinSearchCategory.Photos => S.CoreWinSearchCategoryPhotos,
        WinSearchCategory.Videos => S.CoreWinSearchCategoryVideos,
        WinSearchCategory.Music => S.CoreWinSearchCategoryMusic,
        WinSearchCategory.Email => S.CoreWinSearchCategoryEmail,
        WinSearchCategory.Events => S.CoreWinSearchCategoryEvents,
        WinSearchCategory.People => S.CoreWinSearchCategoryPeople,
        WinSearchCategory.Notes => S.CoreWinSearchCategoryNotes,
        WinSearchCategory.Karpo => S.CoreWinSearchCategoryKarpo,
        _ => S.CoreWinSearchCategoryWeb,
    };

    /// <summary>A Segoe Fluent Icons code point for a group: its chip's and a hit's where the shell draws none.</summary>
    public static string Glyph(WinSearchCategory category) => category switch
    {
        WinSearchCategory.Answer => WinSearchCalc.Glyph,
        WinSearchCategory.Apps => "",
        WinSearchCategory.Settings => WinSearchPages.Glyph,
        WinSearchCategory.Documents => "",
        WinSearchCategory.Folders => "",
        WinSearchCategory.Photos => "",
        WinSearchCategory.Videos => "",
        WinSearchCategory.Music => "",
        WinSearchCategory.Email => OptionGlyphs.Mail,
        WinSearchCategory.Events => Calendar,
        WinSearchCategory.People => OptionGlyphs.Contact,
        WinSearchCategory.Notes => OptionGlyphs.Note,
        WinSearchCategory.Karpo => OptionGlyphs.Settings,
        _ => "",
    };

    /// <summary>Segoe Fluent Icons' calendar.</summary>
    public const string Calendar = "";

    /// <summary>§652: a source's name, its switch's under "Search in" (<see cref="WinSearchSourceInfos"/>).</summary>
    public static string Source(string id) => id switch
    {
        WinSearchSourceInfos.Apps => S.CoreWinSearchCategoryApps,
        WinSearchSourceInfos.Settings => S.CoreWinSearchSourceSettings,
        WinSearchSourceInfos.Files => S.CoreWinSearchSourceFiles,
        WinSearchSourceInfos.Contents => S.CoreWinSearchOptionContents,
        WinSearchSourceInfos.Mail => S.CoreWinSearchCategoryEmail,
        WinSearchSourceInfos.Calendar => S.CoreWinSearchCategoryEvents,
        WinSearchSourceInfos.Contacts => S.CoreWinSearchCategoryPeople,
        WinSearchSourceInfos.Notes => S.CoreWinSearchCategoryNotes,
        WinSearchSourceInfos.Karpo => S.CoreWinSearchCategoryKarpo,
        WinSearchSourceInfos.Calculator => S.CoreWinSearchSourceCalculator,
        WinSearchSourceInfos.Web => S.CoreWinSearchOptionWeb,
        _ => id,
    };

    /// <summary>§655: a home section's name, its heading's and its switch's (<see cref="WinSearchHomeInfos"/>).</summary>
    public static string Home(string id) => id switch
    {
        WinSearchHomeInfos.Searches => S.DockWinSearchRecentSearches,
        WinSearchHomeInfos.Apps => S.CoreWinSearchHomeApps,
        WinSearchHomeInfos.Pinned => S.CoreWinSearchHomePinned,
        WinSearchHomeInfos.Files => S.DockWinSearchRecentFiles,
        WinSearchHomeInfos.Folders => S.CoreWinSearchHomeFolders,
        WinSearchHomeInfos.Events => S.CoreWinSearchHomeEvents,
        WinSearchHomeInfos.Mail => S.CoreWinSearchHomeMail,
        WinSearchHomeInfos.Notes => S.CoreWinSearchHomeNotes,
        _ => id,
    };

    /// <summary>§655: the heading of the home's sections on the widget's page, the level of its menu.</summary>
    public static string HomeShown => S.CoreWinSearchHomeShown;

    /// <summary>§655: the heading of the home's sections' counts on the widget's page.</summary>
    public static string HomeCounts => S.CoreWinSearchHomeCounts;

    /// <summary>§652: the heading of the sources' switches on the widget's page, the level of its menu.</summary>
    public static string SearchIn => S.CoreWinSearchSearchIn;

    /// <summary>§652: what a page or a row of Karpo's settings says under its name.</summary>
    public static string KarpoSettings => S.CoreWinSearchKarpoSettings;

    /// <summary>The chip that lists every group.</summary>
    public static string All => S.CoreWinSearchAll;

    /// <summary>The heading over the first result.</summary>
    public static string BestMatch => S.CoreWinSearchBestMatch;

    /// <summary>What an app's row says under its name, as Windows 11's search says it.</summary>
    public static string AppDetail => S.CoreWinSearchApp;

    /// <summary>What a page of Settings' row says under its name.</summary>
    public static string SettingsDetail => S.CoreWinSearchCategorySettings;

    /// <summary>The web's row: "Search the web for “…”".</summary>
    public static string SearchTheWeb(string text) => S.CoreWinSearchSearchTheWeb(text);
}
