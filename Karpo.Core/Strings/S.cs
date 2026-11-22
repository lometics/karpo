// S: Karpo's words, one member per key of Strings\en.json (generated, Karpo.Strings.Generator).
//
// It lives in the namespace Karpo rather than Karpo.Core, on purpose. TerraFX, which most of the
// dock imports, has a class S of its own (S.S_OK), and a type of the namespace Karpo is found from
// Karpo.Dock, Karpo.Settings and every other Karpo.* namespace before anything a using brings in.
// So S.DockVolumeLevel(level) needs no using and is never ambiguous; TerraFX's is written out
// where it is meant (TerraFX.Interop.Windows.S.S_OK). In XAML: xmlns:s="using:Karpo", then
// {x:Bind s:S.SettingsLanguageHeader}.

using Karpo.Core;

namespace Karpo;

public static partial class S
{
    /// <summary>
    /// What <paramref name="key"/> says in the language now, by its key ("settings.language.header")
    /// or S's name for it ("SettingsLanguageHeader"): for what finds a text by name, such as the
    /// settings search reading <c>{x:Bind s:S.Name}</c> in a page's markup. Code that knows the text
    /// it wants uses S's member, which the build checks. Null for a key there is none of.
    /// </summary>
    public static string? Lookup(string key) => Strings.Lookup(key);

    /// <summary>The same in <paramref name="language"/>: English, for a search that finds a setting by its English words too.</summary>
    public static string? Lookup(string key, string language) => Strings.Lookup(key, language);
}
