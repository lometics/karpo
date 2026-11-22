// §587 (the person, 2026-11-09: "the dropdowns sometimes change their width with their contents and then never change back.
// the dropdowns generally should have a fixed width"): a popup of the settings panel — a dropdown's list, a colour's picker,
// a button's level — is one width, its box's 240 DIP, as a ComboBox's list is its box's: never as wide as its rows (§517's
// FitsContent, which the panel's popups took, grew it to its widest row up to the work area), and so never kept at the widest
// it grew to while it stood open (a Refresh, a level, a search's finds took the widest of every showing). A search's popup,
// its field over its finds, is a fixed width of its own. A longer row is cut with an ellipsis, said whole in its tooltip; so
// is a dropdown's value in its closed box. The popups need Direct2D and a window station: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Tests.Folders;
using Karpo.Tests.Islands;
using Karpo.Tests.Menus;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Edit;

public sealed class DropdownWidthTests
{
    private const float Scale = 1.5f;

    private static float Px(float dip) => MathF.Round(dip * Scale, MidpointRounding.AwayFromZero);

    /// <summary>A 1920 × 1080 display at 150 %.</summary>
    private static readonly RECT Work = new() { right = 2880, bottom = 1620 };

    /// <summary>A control <paramref name="width"/> px wide, high on the display, its popup's room under it.</summary>
    private static RECT Box(float width) => new() { left = 900, top = 240, right = 900 + (int)width, bottom = 240 + (int)Px(32) };

    /// <summary>
    /// <paramref name="rows"/> in a popup of the panel hung from <paramref name="box"/>, as the dock shows one (Dock.PanelPopup):
    /// whatever else the menu is told — §230's configuration width, §517's fit to its rows — its box's popup.
    /// </summary>
    private static void Show(Flyout menu, Gfx gfx, IReadOnlyList<FlyoutItem> rows, RECT box) =>
        menu.Show(gfx, TestLooks.LookFor(true) with { Animations = false }, Scale, rows, FlyoutLayout.List, new POINT { x = box.left, y = box.bottom }, Work, Karpo.Core.Edge.Bottom, control: box);

    private static int WidthOf(Flyout menu) => menu.PanelOnScreen.right - menu.PanelOnScreen.left;

    private const string Longest = "A choice whose words run on far past the width of the box it was picked from";

    /// <summary>A choice's list: <paramref name="names"/>, the first ticked.</summary>
    private static List<FlyoutItem> Choices(params string[] names) => [.. names.Select((name, at) => new FlyoutItem(100 + (uint)at, name, Checked: at == 0, Stays: true))];

    /// <summary>
    /// The bug: a dropdown's list of long choices grew past its box to fit them, and shown again for short ones while it
    /// stood (the dock's Refresh after a pick, a search's finds as one types) kept the widest it had been. Now its box's
    /// width every time, the long choice cut with an ellipsis and whole in its tooltip.
    /// </summary>
    [UiFact]
    public void ADropdownsListIsItsBoxsWidthWhateverItsChoicesSay()
    {
        WindowShots.OnHiddenDesktop("dropdown-width", (gfx, instance) =>
        {
            var box = Box(Px(240));
            var lengthy = Choices(Longest, "Short");
            var brief = Choices("Short", "Brief");
            using var menu = new Flyout(gfx, instance, 0) { Configures = true, FitsContent = true };
            Show(menu, gfx, lengthy, box);
            Assert.Equal(Px(240), WidthOf(menu));
            Assert.Contains(Longest, menu.TipOfRow(lengthy[0].Id) ?? "");
            // Shown again where it stands for short choices (a Refresh): back to its box's width, not the widest it was.
            menu.Refresh(brief);
            Assert.Equal(Px(240), WidthOf(menu));
            menu.Refresh(lengthy);
            Assert.Equal(Px(240), WidthOf(menu));
            menu.Hide();
            // Opened afresh, short and long alike.
            Show(menu, gfx, brief, box);
            Assert.Equal(Px(240), WidthOf(menu));
            Show(menu, gfx, lengthy, box);
            Assert.Equal(Px(240), WidthOf(menu));
            menu.Hide();
        });
    }

    /// <summary>
    /// Every popup of every page of the panel (MenuCorpus, every expander open), as the dock opens it from its control
    /// (<see cref="PanelRows.PopupOf"/>): a dropdown's list, a colour's picker, a way to add one's list, a button's level —
    /// each its box's one width, 240 DIP, whatever its rows say; a search's its own, 360; §613 a colour's picker its own, 400.
    /// </summary>
    [UiFact]
    public void EveryPopupOfThePanelIsOneWidth()
    {
        var words = new SettingsPanelTests.Words();
        List<(string Where, IReadOnlyList<FlyoutItem> Rows)> popups = [];
        foreach (var corpus in MenuCorpus.All().Where(menu => menu.Settings))
        {
            var page = PanelRows.Page(PanelPage.Widget, corpus.Name, "", corpus.Items);
            var open = PanelRows.All(page.Rows).Where(row => row.Kind == PanelRowKind.Expander).Select(row => row.Key).ToHashSet();
            var layout = new PanelLayout(words, Scale, new PanelInput(0, new SettingsContent([page]), PanelPage.Widget, open), new System.Numerics.Vector2(0, 20_000));
            foreach (var line in layout.Blocks.SelectMany(block => block.Lines))
            {
                foreach (var (hit, _) in layout.Controls(line).Where(each => each.Hit.Kind == PanelHitKind.Pop))
                {
                    if (PanelRows.PopupOf(line.Row.Item, hit.Id) is { } rows)
                    {
                        popups.Add(($"[{corpus.Name}] {line.Row.Key}", rows));
                    }
                }
            }
        }
        Assert.True(popups.Count > 50, $"{popups.Count} popups found");
        // A search's too (the person, 2026-11-09: "the dropdowns should all have the same width"): its field over its finds.
        static bool Search(IReadOnlyList<FlyoutItem> rows) => rows.Any(item => item.Control is FlyoutField { Finder: not null });
        Assert.Contains(popups, popup => popup.Where.EndsWith("/" + S.SettingsWidgetClockZoneHeader, StringComparison.Ordinal) && Search(popup.Rows));
        List<string> wrong = [];
        WindowShots.OnHiddenDesktop("dropdown-widths", (gfx, instance) =>
        {
            using var menu = new Flyout(gfx, instance, 0) { Configures = true, FitsContent = true };
            foreach (var (where, rows) in popups)
            {
                // §613: a colour's picker (its swatches, its mixer) the one wider: wider and less tall.
                var one = Px(rows.Any(item => item.Control is FlyoutMix) ? PanelMetrics.PickerWide : PanelMetrics.ControlWide);
                Show(menu, gfx, rows, Box(Px(240)));
                if (WidthOf(menu) != one)
                {
                    wrong.Add($"{where}: {WidthOf(menu)} px wide, not {one}");
                }
                menu.Hide();
            }
        });
        Assert.True(wrong.Count == 0, $"{wrong.Count} of {popups.Count}:\n" + string.Join("\n", wrong.Distinct().Take(80)));
    }

    /// <summary>
    /// §599 (the person, 2026-11-09: "the dropdowns from these menus should also have a fixed width like all the others"): the
    /// list of every row that only acts (PanelRow.Foot) — the taskbar menu's Add an entry, a folder's Add a face,
    /// a toggles line's Add, Add a country — on every page the dock builds and every page of the corpus, every expander open,
    /// hung from its button where the panel lays it out, as the dock hangs it (its button the control, the flyout what it
    /// stands in): a value's box's one width, 240 DIP, under its button, flush with its right — the button at the line's end.
    /// </summary>
    [UiFact]
    public void EveryFootButtonsListIsOneWidthUnderItsButtonFlushWithItsRight()
    {
        var words = new SettingsPanelTests.Words();
        var content = SettingsPanelTests.Content(widget: true);
        IEnumerable<(string Name, PanelPage Page, SettingsContent Content)> pages =
        [
            .. content.Pages.Select(page => (page.Page.ToString(), page.Page, content)),
            .. MenuCorpus.All().Where(menu => menu.Settings).Select(menu => (menu.Name, PanelPage.Widget, new SettingsContent([PanelRows.Page(PanelPage.Widget, menu.Name, "", menu.Items)]))),
        ];
        List<(string Where, RECT Button, RECT Panel, IReadOnlyList<FlyoutItem> Rows)> feet = [];
        foreach (var (name, shown, holding) in pages)
        {
            var open = PanelRows.All(holding.Of(shown)!.Rows).Where(row => row.Kind == PanelRowKind.Expander).Select(row => row.Key).ToHashSet();
            var layout = new PanelLayout(words, Scale, new PanelInput(0, holding, shown, open), new System.Numerics.Vector2(0, 20_000));
            // The flyout on the screen at (600, 0); each button brought to 240 px down it, its popup's room under it.
            var panel = new RECT { left = 600, top = 0, right = 600 + (int)layout.FlyoutSize.X, bottom = 1600 };
            foreach (var line in layout.Blocks.SelectMany(block => block.Lines).Where(line => line.Row.Foot))
            {
                foreach (var (hit, rect) in layout.Controls(line).Where(each => each.Hit.Kind == PanelHitKind.Pop))
                {
                    var at = layout.OnPanel(rect);
                    var button = new RECT { left = panel.left + (int)at.X, top = 240, right = panel.left + (int)MathF.Ceiling(at.Right), bottom = 240 + (int)Px(32) };
                    if (PanelRows.PopupOf(line.Row.Item, hit.Id) is { } rows)
                    {
                        feet.Add(($"[{name}] {line.Row.Key}", button, panel, rows));
                    }
                }
            }
        }
        // Each of them found: Add an entry, Add a face, Add a toggle, Add a country (§628: Add a layout opens a page, no list).
        foreach (var said in new[] { S.SettingsBarMenuAdd, S.SettingsFolderFacesAddHeader, S.SettingsWidgetTogglesAdd, S.CoreHolidaysAdd })
        {
            Assert.Contains(feet, foot => foot.Where.EndsWith("/" + said, StringComparison.Ordinal));
        }
        List<string> wrong = [];
        WindowShots.OnHiddenDesktop("foot-widths", (gfx, instance) =>
        {
            using var menu = new Flyout(gfx, instance, 0) { Configures = true };
            foreach (var (where, button, panel, rows) in feet)
            {
                menu.Show(gfx, TestLooks.LookFor(true) with { Animations = false }, Scale, rows, FlyoutLayout.List, new POINT { x = button.left, y = button.bottom }, Work, Karpo.Core.Edge.Bottom, control: button, within: panel);
                var on = menu.PanelOnScreen;
                if (on.right - on.left != Px(PanelMetrics.ControlWide) || on.right != button.right || on.top != button.bottom + Px(PopupPlace.GapDip))
                {
                    wrong.Add($"{where}: its list {on.left},{on.top}..{on.right},{on.bottom} under its button {button.left},{button.top}..{button.right},{button.bottom}");
                }
                menu.Hide();
            }
        });
        Assert.True(wrong.Count == 0, $"{wrong.Count} of {feet.Count}:\n" + string.Join("\n", wrong.Distinct().Take(80)));
    }

    /// <summary>A search's popup (a time zone's): its box's width like every other, before anything is typed and as its finds change.</summary>
    [UiFact]
    public void ASearchsPopupIsItsBoxsWidthToo()
    {
        var zone = MenuCorpus.All().SelectMany(menu => Flat(menu.Items)).First(item => item.Text == S.SettingsWidgetClockZoneHeader && item.Children is { Count: > 0 });
        WindowShots.OnHiddenDesktop("search-width", (gfx, instance) =>
        {
            using var menu = new Flyout(gfx, instance, 0) { Configures = true, FitsContent = true };
            Show(menu, gfx, zone.Children!, Box(Px(240)));
            Assert.Equal(Px(PanelMetrics.ControlWide), WidthOf(menu));
            foreach (var c in "zur")
            {
                _ = menu.Message(WM.WM_CHAR, (WPARAM)(nuint)c, 0);
                Assert.Equal(Px(PanelMetrics.ControlWide), WidthOf(menu));
            }
            menu.Hide();
        });
    }

    /// <summary>
    /// §672: a choice's inline picker's head says what it is set to in at most a control's width: a long value cut short there,
    /// whole in its tooltip; a short one has none.
    /// </summary>
    [Fact]
    public void ADropdownsValueCutShortInItsBoxIsWholeInItsTooltip()
    {
        FlyoutItem Dropdown(uint id, string name, string value, FlyoutIcon? icon = null) =>
            new(id, name, Value: new FlyoutValue(value), PickOne: true,
                Children: [new FlyoutItem(id * 10, value, icon, Checked: true), new FlyoutItem((id * 10) + 1, "Other", icon, Checked: false), new FlyoutItem((id * 10) + 2, "Third", icon, Checked: false), new FlyoutItem((id * 10) + 3, "Fourth", icon, Checked: false)]);
        var page = PanelRows.Page(PanelPage.Widget, "", "", [Dropdown(1, "Long", Longest), Dropdown(2, "Short", "Short"), Dropdown(3, "Pictured", Longest, new FlyoutIcon.Glyph(""))]);
        var layout = new PanelLayout(new SettingsPanelTests.Words(), Scale, new PanelInput(0, new SettingsContent([page]), PanelPage.Widget), PanelLayout.SizeOf(Scale, 1548));
        var lines = layout.Blocks.SelectMany(block => block.Lines).ToList();
        Assert.All(lines, line => Assert.Equal(PanelRowKind.Picker, line.Row.Kind));
        PanelHit Head(PanelLine line) => layout.Controls(line).Single(each => each.Hit.Kind == PanelHitKind.Head).Hit;
        Assert.Equal(Longest, layout.TipOf(Head(lines[0])));
        Assert.Null(layout.TipOf(Head(lines[1])));
        Assert.Equal(Longest, layout.TipOf(Head(lines[2])));
        // Its words at most a control's width; its choice's icon 8 before them where it wears one.
        Assert.Equal(Px(240), layout.PickerSaid(lines[0].Row));
        Assert.Equal(Px(240) + Px(8) + Px(20), layout.PickerSaid(lines[2].Row));
    }

    private static IEnumerable<FlyoutItem> Flat(IEnumerable<FlyoutItem> items) =>
        items.SelectMany(item => (IEnumerable<FlyoutItem>)[item, .. Flat(item.Children ?? [])]);
}
