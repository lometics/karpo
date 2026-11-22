// holidays: the countries Nager.Date has holidays of, and the regions it names in them (docs\widgets.md
// §237), so that Settings and a calendar's menu offer them without asking the service.
//
// The countries are its AvailableCountries of 2026-10-19; the regions every ISO 3166-2 code its answers
// for 2026 and 2027 named in "counties", with the region's own name (a canton, a state, a province: a
// name, the same in every language, written as the region writes it). A country's name is Windows' own
// in the language Karpo speaks (ICU, as the currencies' are, CurrencyNames.cs), its code where Windows
// has none. A region the service names later and this list does not still shows: its code.

using System.Runtime.InteropServices;

namespace Karpo.Core;

/// <summary>A region of a country, as the holidays name it: its ISO 3166-2 code ("CH-ZH") and its own name ("Zürich").</summary>
public sealed record HolidayRegion(string Code, string Name);

/// <summary>The countries and regions the holidays come for; see the top of the file.</summary>
public static partial class HolidayPlaces
{
    /// <summary>The countries the service has holidays of, by code.</summary>
    public static IReadOnlyList<string> Countries { get; } =
    [
        "AD", "AG", "AI", "AL", "AM", "AO", "AR", "AT", "AU", "AW", "AX", "BA", "BB", "BD", "BE", "BF", "BG", "BH", "BI", "BJ",
        "BL", "BM", "BO", "BQ", "BR", "BS", "BW", "BY", "BZ", "CA", "CC", "CD", "CF", "CG", "CH", "CI", "CK", "CL", "CM", "CN",
        "CO", "CR", "CU", "CV", "CW", "CX", "CY", "CZ", "DE", "DJ", "DK", "DM", "DO", "DZ", "EC", "EE", "EG", "ER", "ES", "ET",
        "FI", "FK", "FM", "FO", "FR", "GA", "GB", "GD", "GE", "GF", "GG", "GH", "GI", "GL", "GM", "GN", "GP", "GQ", "GR", "GT",
        "GW", "GY", "HK", "HN", "HR", "HT", "HU", "ID", "IE", "IM", "IQ", "IS", "IT", "JE", "JM", "JP", "KE", "KH", "KI", "KM",
        "KN", "KR", "KY", "KZ", "LC", "LI", "LR", "LS", "LT", "LU", "LV", "LY", "MA", "MC", "MD", "ME", "MF", "MG", "MH", "MK",
        "ML", "MN", "MP", "MQ", "MR", "MS", "MT", "MW", "MX", "MZ", "NA", "NC", "NE", "NF", "NG", "NI", "NL", "NO", "NR", "NU",
        "NZ", "PA", "PE", "PF", "PG", "PH", "PL", "PM", "PN", "PR", "PT", "PW", "PY", "RO", "RS", "RU", "RW", "SB", "SC", "SD",
        "SE", "SG", "SH", "SI", "SJ", "SK", "SL", "SM", "SN", "SO", "SR", "SS", "ST", "SV", "SX", "SY", "SZ", "TC", "TD", "TG",
        "TK", "TN", "TO", "TR", "TT", "TV", "TZ", "UA", "UG", "US", "UY", "VA", "VC", "VE", "VG", "VI", "VN", "VU", "WF", "WS",
        "YE", "ZA", "ZM", "ZW",
    ];

    private static readonly HashSet<string> Known = new(Countries, StringComparer.Ordinal);

    /// <summary>Whether the service has holidays of <paramref name="country"/>.</summary>
    public static bool Has(string country) => Known.Contains(country);

    /// <summary>The regions of <paramref name="country"/> the holidays name, in the order of their codes; none for a country whose holidays are all the whole country's.</summary>
    public static IReadOnlyList<HolidayRegion> Regions(string country) =>
        ByCountry.TryGetValue(country, out var regions) ? regions : [];

    /// <summary>What a region is called: its own name, or for one this list does not know its code after the country's ("ZH").</summary>
    public static string RegionName(string code)
    {
        var dash = code.IndexOf('-');
        var country = dash > 0 ? code[..dash] : "";
        return Regions(country).FirstOrDefault(region => string.Equals(region.Code, code, StringComparison.OrdinalIgnoreCase))?.Name
            ?? (dash > 0 ? code[(dash + 1)..] : code);
    }

    /// <summary>
    /// What <paramref name="country"/> is called in <paramref name="language"/> (a language of Karpo's; the one
    /// it speaks where none is given), as Windows' ICU has it; the code where it has none.
    /// </summary>
    public static unsafe string Name(string country, string? language = null)
    {
        if (country.Length != 2)
        {
            return country;
        }
        try
        {
            const int room = 128;
            var name = stackalloc char[room];
            var error = 0;
            var length = uloc_getDisplayCountry("und_" + country, (language ?? Strings.Language).Replace('-', '_'), name, room, ref error);
            // ICU writes the code back for a country it has no name of.
            return error > 0 || length <= 0 || length > room ? country : new string(name, 0, length);
        }
        catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException)
        {
            return country;
        }
    }

    /// <summary>The countries by their names in <paramref name="language"/> (Karpo's where none is given), as a list offers them.</summary>
    public static IReadOnlyList<(string Code, string Name)> ByName(string? language = null) =>
        [.. Countries.Select(code => (code, Name(code, language))).OrderBy(pair => pair.Item2, StringComparer.CurrentCultureIgnoreCase)];

    private static readonly Dictionary<string, IReadOnlyList<HolidayRegion>> ByCountry = new(StringComparer.Ordinal)
    {
        ["AT"] = Of("AT", "1=Burgenland", "2=Kärnten", "3=Niederösterreich", "4=Oberösterreich", "5=Salzburg", "6=Steiermark", "7=Tirol", "8=Vorarlberg", "9=Wien"),
        ["AU"] = Of("AU", "ACT=Australian Capital Territory", "NSW=New South Wales", "NT=Northern Territory", "QLD=Queensland", "SA=South Australia", "TAS=Tasmania", "VIC=Victoria", "WA=Western Australia"), // not-ui: names
        ["BA"] = Of("BA", "BIH=Federacija Bosne i Hercegovine", "SRP=Republika Srpska"), // not-ui: names
        ["BQ"] = Of("BQ", "BO=Bonaire", "SA=Saba", "SE=Sint Eustatius"), // not-ui: names
        ["BR"] = Of("BR", "SP=São Paulo"), // not-ui: names
        ["CA"] = Of("CA", "AB=Alberta", "BC=British Columbia", "MB=Manitoba", "NB=New Brunswick", "NL=Newfoundland and Labrador", "NS=Nova Scotia", "NT=Northwest Territories", "NU=Nunavut", "ON=Ontario", "PE=Prince Edward Island", "QC=Québec", "SK=Saskatchewan", "YT=Yukon"), // not-ui: names
        ["CH"] = Of("CH", "AG=Aargau", "AI=Appenzell Innerrhoden", "AR=Appenzell Ausserrhoden", "BE=Bern", "BL=Basel-Landschaft", "BS=Basel-Stadt", "FR=Fribourg", "GE=Genève", "GL=Glarus", "GR=Graubünden", "JU=Jura", "LU=Luzern", "NE=Neuchâtel", "NW=Nidwalden", "OW=Obwalden", "SG=St. Gallen", "SH=Schaffhausen", "SO=Solothurn", "SZ=Schwyz", "TG=Thurgau", "TI=Ticino", "UR=Uri", "VD=Vaud", "VS=Valais", "ZG=Zug", "ZH=Zürich"), // not-ui: names
        ["CL"] = Of("CL", "AP=Arica y Parinacota"), // not-ui: names
        ["DE"] = Of("DE", "BB=Brandenburg", "BE=Berlin", "BW=Baden-Württemberg", "BY=Bayern", "HB=Bremen", "HE=Hessen", "HH=Hamburg", "MV=Mecklenburg-Vorpommern", "NI=Niedersachsen", "NW=Nordrhein-Westfalen", "RP=Rheinland-Pfalz", "SH=Schleswig-Holstein", "SL=Saarland", "SN=Sachsen", "ST=Sachsen-Anhalt", "TH=Thüringen"),
        ["ES"] = Of("ES", "AN=Andalucía", "AR=Aragón", "AS=Asturias", "CB=Cantabria", "CL=Castilla y León", "CM=Castilla-La Mancha", "CN=Canarias", "CT=Catalunya", "EX=Extremadura", "GA=Galicia", "IB=Illes Balears", "MC=Región de Murcia", "MD=Comunidad de Madrid", "NC=Navarra", "PV=País Vasco", "RI=La Rioja", "VC=Comunitat Valenciana"), // not-ui: names
        ["FM"] = Of("FM", "KSA=Kosrae", "PNI=Pohnpei", "TRK=Chuuk", "YAP=Yap"),
        ["GB"] = Of("GB", "ENG=England", "NIR=Northern Ireland", "SCT=Scotland", "WLS=Wales"), // not-ui: names
        ["IT"] = Of("IT", "32=Trentino-Alto Adige"), // not-ui: names
        ["NZ"] = Of("NZ", "AUK=Auckland", "BOP=Bay of Plenty", "CAN=Canterbury", "CIT=Chatham Islands", "GIS=Gisborne", "HKB=Hawke's Bay", "MBH=Marlborough", "MWT=Manawatū-Whanganui", "NSN=Nelson", "NTL=Northland", "OTA=Otago", "STL=Southland", "TKI=Taranaki", "WGN=Wellington", "WKO=Waikato", "WTC=West Coast"), // not-ui: names
        ["PT"] = Of("PT", "20=Açores", "30=Madeira"),
        ["SH"] = Of("SH", "AC=Ascension", "HL=Saint Helena", "TA=Tristan da Cunha"), // not-ui: names
        ["US"] = Of("US", "AK=Alaska", "AL=Alabama", "AZ=Arizona", "CA=California", "CO=Colorado", "CT=Connecticut", "DE=Delaware", "GA=Georgia", "HI=Hawaii", "IA=Iowa", "ID=Idaho", "IL=Illinois", "IN=Indiana", "KS=Kansas", "KY=Kentucky", "LA=Louisiana", "MA=Massachusetts", "MD=Maryland", "ME=Maine", "MI=Michigan", "MN=Minnesota", "MO=Missouri", "MS=Mississippi", "MT=Montana", "NC=North Carolina", "ND=North Dakota", "NE=Nebraska", "NH=New Hampshire", "NJ=New Jersey", "NM=New Mexico", "NY=New York", "OH=Ohio", "OK=Oklahoma", "OR=Oregon", "PA=Pennsylvania", "RI=Rhode Island", "SC=South Carolina", "SD=South Dakota", "TN=Tennessee", "TX=Texas", "UT=Utah", "VA=Virginia", "VT=Vermont", "WI=Wisconsin", "WV=West Virginia"), // not-ui: names
    };

    /// <summary><paramref name="country"/>'s regions from "code=name" pairs, the code without the country's.</summary>
    private static HolidayRegion[] Of(string country, params string[] pairs) =>
        [.. pairs.Select(pair => pair.Split('=', 2)).Select(parts => new HolidayRegion($"{country}-{parts[0]}", parts[1]))];

    [LibraryImport("icu.dll")]
    private static unsafe partial int uloc_getDisplayCountry([MarshalAs(UnmanagedType.LPStr)] string locale, [MarshalAs(UnmanagedType.LPStr)] string displayLocale, char* country, int capacity, ref int status);
}
