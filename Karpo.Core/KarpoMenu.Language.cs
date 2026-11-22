// §335, §337: "Language ›", Karpo's display language: Languages.ChoiceNames — "Default (Deutsch)" (§518) first, null in
// the file, what Karpo does until somebody chooses —, then each language in its own words. A pick writes "language"; the
// dock speaks it at once (Dock.Apply: Languages.Apply, the bars built again in it). Windows' preferred languages come in
// KarpoServices.Windows, read where the menu is built.
//
// §631 (the person, 2026-11-09: "you should be able to search the languages"): a search, as a time zone's is (OptionRowKind.
// Search, its LanguageFinder): its list is its field over the languages, every one before anything is typed, the one in
// force ticked; typed, those whose own name, English name, name in the language Karpo speaks now or code holds the words,
// accents and capitals aside (Languages.Rank), the code typed first, then a name starting so. Enter picks the first.

namespace Karpo.Core;

/// <summary>§337: Windows' preferred display languages, most wanted first (<see cref="Languages.WindowsPreferred"/> unless given others).</summary>
public sealed partial record KarpoServices
{
    /// <summary>Windows' preferred display languages, which "Windows default" names the first Karpo has of.</summary>
    public Func<IReadOnlyList<string>> Windows { get; init; } = Languages.WindowsPreferred;
}

public static partial class KarpoMenu
{
    /// <summary>"Language"'s row.</summary>
    public const string LanguageRow = "Language";

    /// <summary>"Language ›": a search over Windows default and every language Karpo speaks in its own words, <paramref name="config"/>'s ticked (§631).</summary>
    public static IReadOnlyList<OptionRow> Language(Config config, KarpoServices services)
    {
        var chosen = config.Language;
        var finder = new LanguageFinder(chosen, services.Windows());
        return
        [
            new OptionRow(LanguageRow, OptionRowKind.Search, S.SettingsLanguageSection)
            {
                Glyph = OptionGlyphs.Language,
                Field = OptionField.Search,
                Summary = finder.Names[Languages.ChoiceIndex(chosen)],
                Placeholder = S.SettingsLanguageSearch,
                Finder = finder,
                Pick = found =>
                {
                    if (found.Value is not string code || found.Chosen)
                    {
                        return false;
                    }
                    config.Language = code.Length == 0 ? null : code;
                    return true;
                },
            },
        ];
    }

    /// <summary>
    /// §631: the languages the Language row offers (<see cref="Languages.Choices"/>), each a find whose value is its code ("" for
    /// Windows default), its name in the language Karpo speaks now at its end where that is not its own; every one for nothing
    /// typed, else those <see cref="Languages.Rank"/> finds, best first.
    /// </summary>
    internal sealed class LanguageFinder(string? chosen, IReadOnlyList<string> windows) : OptionFinder
    {
        /// <summary>What the list says for each of <see cref="Languages.Choices"/>.</summary>
        public List<string> Names { get; } = Languages.ChoiceNames(chosen, windows);

        public override int LeastLetters => 0;

        public override Task<IReadOnlyList<OptionFound>> FindAsync(string query, CancellationToken cancel) => Task.FromResult(Find(query));

        public override string NoneFound(string query) => S.DockMenuNoMatch;

        internal IReadOnlyList<OptionFound> Find(string query)
        {
            var codes = Languages.Choices(chosen);
            var at = Languages.ChoiceIndex(chosen);
            return [.. codes
                .Select((code, index) => (Rank: Languages.Rank(code, Names[index], query), Found: Found(code, Names[index], index == at)))
                .Where(each => each.Rank is not null)
                .OrderBy(each => each.Rank)
                .Select(each => each.Found)];
        }

        private static OptionFound Found(string? code, string name, bool chosen) =>
            new(name, code is not null && Languages.NameIn(code, Strings.Language) is var here && !string.Equals(here, name, StringComparison.OrdinalIgnoreCase) && here != code ? here : "", code ?? "")
            {
                Chosen = chosen,
            };
    }
}
