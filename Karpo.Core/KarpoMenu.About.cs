// §339: "About Karpo ›", a level: the settings app's About page's name card and its credits in the edit
// bar's ⚙ (the settings-in-the-dock plan's B1 to B3), in the page's order:
//
//  - **Karpo**, its logo (the dock's picture of assets\logo, LogoPicture), the version and the runtime
//    under it: a note.
//  - **Lometics GmbH**, its mark (PublisherPicture) and one line under it, where the company is; opens lometics.com.
//
// §630 (the person, 2026-11-09: "remove open meteo callout in the about section"): no data source here. Open-Meteo's
// CC BY 4.0 credit stands where its data is set, under a weather widget's Place (OptionFields.Place).
//
// §579 (the person, 2026-11-09: "about karpo should get logos and the layout should be fixed"): a section of the Karpo page
// under its own heading, About Karpo (KarpoMenu.Rows), its rows cards as every page's — no expander, whose rows wear no icon:
// Karpo's row its logo and its name over "Version 1.4.2 · .NET 10.0.1" (one line: a card's second line is one, the version
// and the runtime on two overran it); Lometics' its mark.
//
// §703 Quit Karpo is on no settings page (KarpoMenu.Quit.cs, the taskbar menu's); the settings file is Back up and reset's.

namespace Karpo.Core;

/// <summary>
/// §339: Karpo itself, as About Karpo says it: its <paramref name="Version"/> ("1.4.2") and the
/// <paramref name="Runtime"/> it runs on (".NET 10.0.0"). Its sites open by <see cref="KarpoServices.Open"/>.
/// </summary>
public sealed record KarpoAbout(string Version, string Runtime);

/// <summary>§339: Karpo's own part of what ⚙ is given.</summary>
public sealed partial record KarpoServices
{
    /// <summary>Karpo itself (<see cref="KarpoAbout"/>); null for none, and then no About Karpo row.</summary>
    public KarpoAbout? About { get; init; }
}

public static partial class KarpoMenu
{
    /// <summary>Karpo's own row: its logo, its version.</summary>
    public const string AboutRow = "About";

    /// <summary>Lometics GmbH's row.</summary>
    public const string MakerRow = "AboutMaker";

    /// <summary>§638: Lometics' site's button, beside its row.</summary>
    public const string MakerSiteRow = "AboutMakerSite";

    /// <summary>§638: what Lometics' site's button says: where it leads, the same in every language.</summary>
    public const string MakerSiteName = "lometics.com"; // not-ui: an address


    /// <summary>What a menu draws Karpo's logo by (<see cref="OptionRow.Picture"/>): the dock's picture of assets\logo.</summary>
    public const string LogoPicture = "karpo:logo";

    /// <summary>What a menu draws Lometics' mark by: the dock's picture of assets\branding.</summary>
    public const string PublisherPicture = "karpo:publisher";

    /// <summary>Lometics' site, as the About page links it.</summary>
    public const string MakerSite = "https://lometics.com/en/";


    // Segoe Fluent Icons: InfoSolid (Info, E946, is drawn as Explorer's Properties in a menu, ExplorerIcons).
    private const string GlyphAbout = "\uF167";

    /// <summary>§579: About Karpo's heading's key: a section of the page of its own (KarpoMenu.Rows).</summary>
    public const string AboutHeading = "Heading.About";

    /// <summary>§579: About Karpo's section: Karpo and its maker (see the top of the file); none where Karpo's own isn't given.</summary>
    public static IReadOnlyList<OptionRow> About(Config config, KarpoServices services)
    {
        if (services.About is not { } about)
        {
            return [];
        }
        return
        [
            new OptionRow(AboutRow, OptionRowKind.Note, "Karpo") // not-ui: the name
            {
                Glyph = GlyphAbout,
                Picture = LogoPicture,
                Why = S.SettingsAboutVersion(about.Version) + " · " + about.Runtime, // not-ui: between the two
            },
            // §638 (the person, 2026-11-09: "the about should show the name Lometics GmbH as title over the description"): a note
            // as Karpo's is, its name over its line, its site a button beside it (Beside, in its line); §579 to §637 a row that
            // acted, its line before a button of its name.
            new OptionRow(MakerRow, OptionRowKind.Note, "Lometics GmbH") // not-ui: the name
            {
                Glyph = OptionGlyphs.Globe,
                Picture = PublisherPicture,
                Why = S.SettingsAboutMakerDescription,
            },
            new OptionRow(MakerSiteRow, OptionRowKind.Action, MakerSiteName)
            {
                Glyph = OptionGlyphs.Link,
                Beside = true,
                Set = _ =>
                {
                    services.Open(MakerSite);
                    return false;
                },
            },
        ];
    }
}
