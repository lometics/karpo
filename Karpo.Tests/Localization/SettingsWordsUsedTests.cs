// The settings app's words that are left are the dock's (docs\widgets.md §341). Its pages read most of the
// "settings.*" keys; the edit bar's menus reuse many as their rows' words (§335–§339), and when the app went
// every key nothing else read went from all the languages. This holds that: each "settings.*" key of
// Strings\en.json is read by Karpo.Dock, Karpo.Core or Karpo.Accounts through S, but the icons' names,
// which the icon search reads by the icon's id (IconSearch.Key, "settings.iconPicker.icon.<id>").

using System.Text.Json;
using System.Text.RegularExpressions;

namespace Karpo.Tests.Localization;

public sealed partial class SettingsWordsUsedTests
{
    /// <summary>Keys read by a name made at run time, not through S.</summary>
    private const string ReadById = "settings.iconPicker.icon.";

    [GeneratedRegex(@"\bS\.([A-Z][A-Za-z0-9]*)")]
    private static partial Regex Member();

    /// <summary>S's member for <paramref name="key"/>, as the strings generator names it: "settings.language.header" is SettingsLanguageHeader.</summary>
    private static string MemberOf(string key) =>
        string.Concat(key.Split('.').Where(word => word.Length > 0).Select(word => char.ToUpperInvariant(word[0]) + word[1..]));

    [Fact]
    public void EverySettingsKeyIsReadByKarpo()
    {
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (var project in new[] { "Karpo.Dock", "Karpo.Core", "Karpo.Accounts" })
        {
            foreach (var path in RepoSource.CSharp(project))
            {
                used.UnionWith(Member().Matches(File.ReadAllText(path)).Select(match => match.Groups[1].Value));
            }
        }
        using var english = JsonDocument.Parse(RepoSource.Read("Karpo.Core", "Strings", "en.json"));
        var keys = english.RootElement.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal);
        // A "one"/"other" pair is one member, its stem's.
        string Stem(string key) =>
            Regex.Match(key, @"^(.*)\.(one|other)$") is { Success: true } pair && keys.Contains(pair.Groups[1].Value + ".one") && keys.Contains(pair.Groups[1].Value + ".other")
                ? pair.Groups[1].Value
                : key;

        var unread = keys
            .Where(key => key.StartsWith("settings.", StringComparison.Ordinal) && !key.StartsWith(ReadById, StringComparison.Ordinal))
            .Where(key => !used.Contains(MemberOf(Stem(key))))
            .Order(StringComparer.Ordinal)
            .ToList();
        Assert.True(used.Count > 500, $"only {used.Count} members of S found: the scan has stopped working");
        Assert.Empty(unread);
    }
}
