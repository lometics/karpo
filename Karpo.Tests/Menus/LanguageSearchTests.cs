// §631 (the person, 2026-11-09: "you should be able to search the languages"): the General page's Language a search, as a
// time zone's is (Karpo.Core\KarpoMenu.Language.cs, LanguageFinder). Held here:
//
//  - **What finds a language**: its own name, its English name, its name in the language Karpo speaks now, its code —
//    accents and capitals aside (Languages.Rank); the code typed first, then a name starting so, then the rest.
//  - **Every language's name in every language** Karpo speaks (core.language.name.<code>, CLDR's).
//  - **The popup** (KARPO_UI_TESTS=1), as the dock opens it from the panel's box: its box's 240 DIP like every popup (§587),
//    the keys in its field, the one in force ticked, "germ" typed leaving Deutsch first, Enter picking it and the dock's
//    write making it the file's; Escape putting the words back and every language with them.

using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Folders;
using Karpo.Tests.Islands;
using Karpo.Tests.Localization;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Menus;

[Collection(InLanguage.Collection)]
public sealed class LanguageSearchTests
{
    /// <summary>What the Language row's search finds for <paramref name="query"/>, best first.</summary>
    internal static IReadOnlyList<OptionFound> Finds(OptionRow row, string query) => row.Finder!.FindAsync(query, CancellationToken.None).Result;

    /// <summary>The language <paramref name="code"/> ("" for Windows default) picked in the Language row's search, as the dock keeps a find.</summary>
    internal static bool Pick(OptionRow row, string code) => row.Pick!(Finds(row, "").Single(found => (string)found.Value! == code));

    private static OptionRow Row(string? chosen = null) =>
        Assert.Single(KarpoMenu.Language(new Config { Language = chosen }, new KarpoServices(null, () => { }) { Windows = () => ["en-US"] }));

    /// <summary>The codes <paramref name="query"/> finds, best first ("" for Windows default).</summary>
    private static List<string> Codes(string query, string? chosen = null) => [.. Finds(Row(chosen), query).Select(found => (string)found.Value!)];

    [Theory]
    // Its own name, whole or begun.
    [InlineData("Deutsch", "de")]
    [InlineData("Español", "es")]
    [InlineData("日本", "ja")]
    [InlineData("Русс", "ru")]
    // Its English name.
    [InlineData("German", "de")]
    [InlineData("japanese", "ja")]
    [InlineData("Bengali", "bn")]
    [InlineData("Swahili", "sw")]
    [InlineData("Portuguese", "pt-BR")]
    // Its code, any case.
    [InlineData("de", "de")]
    [InlineData("pt-BR", "pt-BR")]
    [InlineData("PT-br", "pt-BR")]
    [InlineData("zh-hant", "zh-Hant")]
    [InlineData("fil", "fil")]
    // Accents and capitals aside.
    [InlineData("francais", "fr")]
    [InlineData("TURKCE", "tr")]
    [InlineData("cestina", "cs")]
    [InlineData("tieng viet", "vi")]
    [InlineData("yoruba", "yo")]
    [InlineData("romana", "ro")]
    [InlineData("DEUTSCH", "de")]
    public void ALanguageIsFoundFirstByItsNamesAndItsCode(string query, string first) =>
        Assert.Equal(first, Codes(query)[0]);

    /// <summary>
    /// In another language its names there find a language too, and the English ones still do; in English the French names
    /// find nothing. Each find says the language's name in the language Karpo speaks at its end, where that is not its own.
    /// </summary>
    [Fact]
    public void ALanguageIsFoundByItsNameInTheLanguageKarpoSpeaks()
    {
        Assert.Empty(Codes("allemand"));
        Assert.Equal("German", Finds(Row(), "deutsch")[0].Detail);
        Assert.Equal("", Finds(Row(), "english")[0].Detail);
        using (InLanguage.Use("fr"))
        {
            Assert.Equal("de", Codes("allemand")[0]);
            Assert.Equal("de", Codes("german")[0]);
            Assert.Equal("ja", Codes("japonais")[0]);
            Assert.Equal("Allemand", Finds(Row(), "deutsch")[0].Detail);
            Assert.Equal("", Finds(Row(), "français")[0].Detail);
        }
        using (InLanguage.Use("de"))
        {
            Assert.Equal("fr", Codes("franzosisch")[0]);
            Assert.Equal("zh-Hant", Codes("traditionell")[0]);
        }
    }

    /// <summary>
    /// Nothing typed: Windows default, then every language, the one in force ticked. Typed: only what holds the words, the
    /// code typed before a name holding it ("en": English, not French or Bengali), a name starting so before one holding it.
    /// Nothing found: nothing, "No matches" said.
    /// </summary>
    [Fact]
    public void TheCodeTypedComesFirstThenANameStartingSo()
    {
        var all = Finds(Row("fr"), "");
        Assert.Equal(["", .. Languages.Offered], all.Select(found => (string)found.Value!));
        Assert.Equal("fr", (string)Assert.Single(all, found => found.Chosen).Value!);
        var en = Codes("en");
        Assert.Equal("en", en[0]);
        Assert.Contains("fr", en);
        Assert.Contains("bn", en);
        // "deu": Deutsch starts so; Windows default, "Default (Deutsch)" on a German Windows, only holds it.
        var german = Assert.Single(KarpoMenu.Language(new Config(), new KarpoServices(null, () => { }) { Windows = () => ["de-CH"] }));
        Assert.Equal(["de", ""], Finds(german, "deu").Select(found => (string)found.Value!));
        Assert.Empty(Codes("xyzzy"));
        Assert.Equal(S.DockMenuNoMatch, Row().Finder!.NoneFound("xyzzy"));
        // Words typed each found, anywhere among the names: "chinese trad" is one language.
        Assert.Equal(["zh-Hant"], Codes("chinese trad"));
    }

    /// <summary>A pick writes the code, Windows default nothing; the one in force writes nothing again.</summary>
    [Fact]
    public void APickWritesTheCodeTheDefaultNone()
    {
        var config = new Config { Language = "de" };
        var row = Assert.Single(KarpoMenu.Language(config, new KarpoServices(null, () => { }) { Windows = () => ["en-US"] }));
        Assert.False(Pick(row, "de"));
        Assert.True(Pick(row, "pt-BR"));
        Assert.Equal("pt-BR", config.Language);
        Assert.True(Pick(row, ""));
        Assert.Null(config.Language);
    }

    /// <summary>Every language Karpo speaks has every language's name, none of them its key or empty; English's are English's.</summary>
    [Fact]
    public void EveryLanguageNamesEveryLanguage()
    {
        foreach (var language in Languages.Offered)
        {
            foreach (var code in Languages.Offered)
            {
                var name = Languages.NameIn(code, language);
                Assert.False(string.IsNullOrWhiteSpace(name) || name.StartsWith("core.", StringComparison.Ordinal), $"{code} in {language}");
            }
        }
        Assert.Equal(["English", "German", "French", "Spanish", "Chinese (Simplified)", "Portuguese (Brazil)"], Languages.Offered.Take(6).Select(code => Languages.NameIn(code, "en")));
        Assert.Equal(["Bengali", "Swahili"], new[] { "bn", "sw" }.Select(code => Languages.NameIn(code, "en")));
        // One a newer Karpo offers: its code, the one thing this build knows of it.
        Assert.Equal(["xx"], Languages.Words("xx"));
    }

    // --- On a real popup --------------------------------------------------------------------------

    private const float Scale = 1.5f;

    private static float Px(float dip) => MathF.Round(dip * Scale, MidpointRounding.AwayFromZero);

    private static readonly RECT Work = new() { right = 2880, bottom = 1620 };

    private static readonly RECT Box = new() { left = 900, top = 240, right = 900 + (int)Px(240), bottom = 240 + (int)Px(32) };

    private static FlyoutEvent Key(Flyout menu, int key) => menu.Message(WM.WM_KEYDOWN, (WPARAM)(nuint)key, 0);

    private static void Type(Flyout menu, string text)
    {
        foreach (var c in text)
        {
            _ = menu.Message(WM.WM_CHAR, (WPARAM)(nuint)c, 0);
        }
    }

    /// <summary>The menu's own posted messages and timers handed to it for <paramref name="ms"/>: what it said.</summary>
    private static unsafe List<FlyoutEvent> Pump(Flyout menu, int ms = 300)
    {
        List<FlyoutEvent> said = [];
        var until = Environment.TickCount64 + ms;
        MSG msg;
        while (Environment.TickCount64 < until)
        {
            for (var left = 64; left > 0 && PeekMessageW(&msg, HWND.NULL, 0, 0, PM.PM_REMOVE); left--)
            {
                if (msg.hwnd == menu.Hwnd && (msg.message is >= WM.WM_USER and <= WM.WM_USER + 0xFF || msg.message == WM.WM_TIMER))
                {
                    said.Add(menu.Message(msg.message, msg.wParam, msg.lParam));
                    continue;
                }
                _ = TranslateMessage(&msg);
                _ = DispatchMessageW(&msg);
            }
            Thread.Sleep(10);
        }
        return [.. said.Where(happened => happened.Kind != FlyoutEventKind.Nothing)];
    }

    /// <summary>The finds the popup lists under its field now.</summary>
    private static List<FlyoutItem> Listed(Flyout menu) =>
        [.. menu.FieldShowing.SkipWhile(item => item.Control is not FlyoutField).Skip(1)];

    /// <summary>
    /// The General page's Language box opened as the dock opens it (Dock.PanelPopup: the row's level, hung from its box): its
    /// box's 240 DIP whatever is typed, the keys in its field, every language listed with the one in force ticked; "germ"
    /// leaves Deutsch first, Enter picks it, and the dock's write (Dock.SettingsKept) makes it the file's. Escape puts the
    /// words typed back, every language listed again.
    /// </summary>
    [UiFact]
    public void TheLanguagePopupIsSearchedAndEnterPicksTheFirstFind()
    {
        var config = MenuCorpus.Fresh();
        config.Language = "fr";
        var services = MenuCorpus.Services() with { Windows = () => ["en-US"] };
        List<OptionPick> picks = [];
        var holder = TheDock.SettingsItems(config, services, picks).Single(item => item.Text == S.SettingsLanguageSection);
        var rows = PanelRows.PopupOf(holder, 0)!;
        var field = rows.Single(item => item.Control is FlyoutField);
        Assert.Equal(S.SettingsLanguageSearch, ((FlyoutField)field.Control!).Placeholder);
        WindowShots.OnHiddenDesktop("language-search", (gfx, instance) =>
        {
            using var menu = new Flyout(gfx, instance, 0) { Configures = true, FitsContent = true };
            menu.Show(gfx, TestLooks.LookFor(true) with { Animations = false }, Scale, rows, FlyoutLayout.List, new POINT { x = Box.left, y = Box.bottom }, Work, Karpo.Core.Edge.Bottom, control: Box);
            _ = Pump(menu);
            Assert.Equal(field.Id, menu.FieldKeys);
            Assert.Equal(Px(PanelMetrics.ControlWide), menu.PanelOnScreen.right - menu.PanelOnScreen.left);
            Assert.Equal(Languages.Offered.Count + 1, Listed(menu).Count);
            Assert.Equal("Français", Assert.Single(Listed(menu), item => item.Checked == true).Text);

            Type(menu, "zzz");
            _ = Pump(menu);
            Assert.DoesNotContain(Listed(menu), item => item.Enabled);
            Key(menu, VK.VK_ESCAPE);
            _ = Pump(menu);
            Assert.Equal(("", field.Id), (menu.FieldOf(field.Id)!.Text, menu.FieldKeys));
            Assert.Equal(Languages.Offered.Count + 1, Listed(menu).Count);

            Type(menu, "germ");
            _ = Pump(menu);
            Assert.Equal("Deutsch", Listed(menu)[0].Text);
            Assert.Equal(Px(PanelMetrics.ControlWide), menu.PanelOnScreen.right - menu.PanelOnScreen.left);
            _ = Key(menu, VK.VK_RETURN);
            Assert.Contains(new FlyoutEvent(FlyoutEventKind.Adjust, field.Id), Pump(menu));
            var kept = menu.TakeFieldCommit(field.Id)!;
            Assert.Equal("de", kept.Found?.Value);
            menu.Hide();

            var pick = OptionMenu.PickOf(picks, field.Id)!;
            Assert.True(FieldPicks.KeepRow(OptionRows.Find(KarpoMenu.Rows(config, services), pick.Key), kept));
            Assert.Equal("de", config.Language);
        });
    }
}
