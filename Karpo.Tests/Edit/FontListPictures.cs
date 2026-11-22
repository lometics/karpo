// edit, §556: a look's one Font, its family and its weight as one (the person, 2026-11-07: "font and weight should also be
// combined into one setting"), with this PC's families as DirectWrite has them (SystemFonts). To KARPO_PNG_DIR:
//
//  - font-list-<dark|light>.png: the Font's list as the settings panel opens it under its box — its filter at its top,
//    then (§574: no "Default (…)"; §638 the weights a dropdown of their own) each family in the face it comes to, the
//    Widgets in folders' Georgia ticked, in Bold, and in sight;
//  - font-list-filtered-dark.png: "geo" typed into its filter, the families whose names hold it;
//  - font-appearance-<dark|light>.png: the Appearance page (§567), its Widgets in folders open, its Font
//    in Georgia Bold — its box in it.
//
// And what the panel draws is in that family and weight (PaintWatch), a row of the list laid out in its own; a family not
// installed is laid out in Karpo's own at its weight.
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using System.Diagnostics;
using System.Numerics;
using Karpo.Dock;
using Karpo.Tests.Folders;
using Karpo.Tests.Islands;
using Karpo.Tests.Menus;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    private static readonly TextStyle GeorgiaBold = new("Georgia", 700); // not-ui: a font

    /// <summary><paramref name="config"/> with its Widgets in folders in Georgia Bold, their text amber.</summary>
    private static Config BoldLines(Config config)
    {
        Assert.True(Looks.Write(Looks.Section(config, LookPart.FolderWidgets), look => (look.Font, look.Text) = (GeorgiaBold.Text, LookShorthand.Pair("#FFB900"))));
        return config;
    }

    [UiFact]
    public void TheFontListPictures()
    {
        var failures = new List<string>();
        var was = InstalledFonts.Reader;
        InstalledFonts.Reader = SystemFonts.Read;
        try
        {
            var reading = Stopwatch.StartNew();
            var families = InstalledFonts.All;
            var read = reading.ElapsedMilliseconds;
            if (families.Count < 20 || !families.Any(font => font.Family == "Georgia" && font.Weights.Contains(700)))
            {
                failures.Add($"this PC's families read as {families.Count}, Georgia Bold not among them");
            }
            if (families.Any(font => font.Family is "Marlett" or "Wingdings" or "Segoe Fluent Icons" or "Segoe MDL2 Assets"))
            {
                failures.Add("a symbol font listed");
            }
            WindowShots.OnHiddenDesktop("font-list", (gfx, instance) =>
            {
                foreach (var dark in new[] { true, false })
                {
                    var config = BoldLines(new Config());
                    var place = new OptionPlace(config, dark) { Layout = config.DefaultLayout };
                    List<OptionPick> picks = [];
                    var font = OptionMenu.Rows(LookRows.Editor(place, Looks.Section(config, LookPart.FolderWidgets), "k"), picks).Single(item => item.Text == S.SettingsAppearanceStylesFont);
                    var list = font.Children!.ToList();
                    // Its filter first; then (§574: no "Default (…)"; §638 no headings, the weights apart) each family in its face.
                    if (list[0].Control is not FlyoutField { Filters: true } || list[1].Text != families[0].Family)
                    {
                        failures.Add($"{(dark ? "dark" : "light")}: the list starts {string.Join(", ", list.Take(3).Select(item => item.Text))}");
                    }
                    var georgia = list.Single(item => item.Text == "Georgia");
                    if (georgia is not { Checked: true } || georgia.Face != GeorgiaBold)
                    {
                        failures.Add($"Georgia reads in {georgia.Face}, ticked {georgia.Checked}");
                    }
                    if (list.Any(item => item.Header) || list.Count(item => item.Face is not null) != families.Count)
                    {
                        failures.Add($"{list.Count(item => item.Face is not null)} rows for {families.Count} families");
                    }

                    var theme = (TestLooks.LookFor(dark) with { Animations = false });
                    using var menu = new Flyout(gfx, instance, 0);
                    var work = new RECT { right = 2900, bottom = 1500 };
                    var box = new RECT { left = 400, top = 160, right = 760, bottom = 208 };
                    var opening = Stopwatch.StartNew();
                    menu.Show(gfx, theme, 1.5f, list, FlyoutLayout.List, new POINT { x = box.left, y = box.bottom }, work, Edge.Bottom, null, control: box);
                    var opened = opening.ElapsedMilliseconds;
                    // Fast as a ComboBox's: here 180 families, 317 fonts, read in 80 ms and opened in 80 to 190 ms (2026-11-07).
                    if (read + opened > 2000)
                    {
                        failures.Add($"{families.Count} families, {families.Sum(each => each.Weights.Count)} fonts, read in {read} ms; the list opened in {opened} ms ({(dark ? "dark" : "light")})");
                    }
                    // Its rows are laid out in their faces: what a row wears is the family the line is laid out in.
                    using (gfx.Wearing(georgia.Face!.Value))
                    {
                        if (gfx.TextFamily != "Georgia" || gfx.Typeface.Weights.Body != 700)
                        {
                            failures.Add($"Georgia Bold's row laid out in {gfx.TextFamily} at {gfx.Typeface.Weights.Body}");
                        }
                    }
                    WindowShots.Settle(350);
                    Assert.True(WindowShots.SaveTogether([.. menu.OpenPanels], $"font-list-{(dark ? "dark" : "light")}.png", ((byte)0x60, (byte)0x60, (byte)0x60)));
                    if (dark)
                    {
                        foreach (var c in "geo")
                        {
                            _ = menu.Message(WM.WM_CHAR, (WPARAM)(nuint)c, 0);
                        }
                        WindowShots.Settle(350);
                        var left = menu.FieldShowing;
                        // §638: the families whose names hold "geo", Georgia among them.
                        if (!left.Any(item => item.Text == "Georgia") || left.Skip(1).Any(item => item.Face is { } face && !face.Family.Contains("geo", StringComparison.OrdinalIgnoreCase)))
                        {
                            failures.Add($"filtered by geo: {string.Join(", ", left.Select(item => item.Text))}");
                        }
                        Assert.True(WindowShots.SaveTogether([.. menu.OpenPanels], "font-list-filtered-dark.png", ((byte)0x60, (byte)0x60, (byte)0x60)));
                    }
                }

                // A family not installed here: laid out in Karpo's own, at its weight.
                using (gfx.Wearing(new TextStyle("Nowhere Sans", 700)))
                {
                    if (gfx.TextFamily != TextStyle.KarpoFamily || gfx.Typeface.Weights.Body != 700)
                    {
                        failures.Add($"a family not installed laid out in {gfx.TextFamily} at {gfx.Typeface.Weights.Body}");
                    }
                }
            });

            // The Appearance page, its Widgets in folders tab chosen: the Widgets in folders' Font's box in Georgia Bold.
            WindowShots.OnHiddenDesktop("font-appearance", (gfx, instance) =>
            {
                const uint dpi = 144;
                foreach (var dark in new[] { true, false })
                {
                    var theme = AddListPictures.LookFor(dark) with { Animations = false };
                    var config = BoldLines(MenuCorpus.Fresh());
                    List<(string Family, int Weight)> drawn = [];
                    var file = $"font-appearance-{(dark ? "dark" : "light")}.png";
                    PaintWatch.Styled = piece => _ = piece.What == PaintedKind.Text && Add(drawn, (piece.Family, piece.Weight));
                    try
                    {
                        EditedDisplay(gfx, instance, dpi, new Config(), theme, PictureLayout(LayoutType.Taskbar, Edge.Bottom), file, dips: new Vector2(1920f, 1080f), panel: deck =>
                        {
                            var shown = PanelRows.Page(PanelPage.Appearance, S.SettingsAppearanceHeader, PanelRows.GlyphOf(PanelPage.Appearance),
                                Karpo.Dock.Dock.LooksItems(config, new BarSpot(MenuCorpus.Display, "One"), dark, [], LooksScope.Appearance));
                            // §592: landed on the Widgets in folders tab's Font, the tab chosen.
                            var folders = PanelRows.All(shown.Rows).Single(row => row.Key == "Appearance/" + PanelTabs.Mark + LookParts.Name(LookPart.FolderWidgets) + "/" + S.SettingsAppearanceStylesFont);

                            var (content, page) = (new SettingsContent([shown]), PanelPage.Appearance);
                            deck.Memory = new PanelMemory();
                            deck.Memory.Show(page);
                            deck.Show(gfx, theme, dpi / 96f, deck.Room!, deck.State ?? PictureState(), content);
                            deck.Land(page, folders.Key, light: false);
                        });
                    }
                    finally
                    {
                        PaintWatch.Styled = null;
                    }
                    // The box's words: Georgia, its body Bold.
                    if (!drawn.Contains(("Georgia", 700)))
                    {
                        failures.Add($"{file}: nothing drawn in Georgia Bold: {string.Join(", ", drawn.Distinct().Where(each => each.Family != TextStyle.KarpoFamily))}");
                    }
                }
            });
        }
        finally
        {
            InstalledFonts.Reader = was;
        }
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    private static bool Add(List<(string Family, int Weight)> into, (string Family, int Weight) what)
    {
        into.Add(what);
        return true;
    }
}
