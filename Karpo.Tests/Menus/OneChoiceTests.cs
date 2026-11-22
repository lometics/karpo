// menus: a level of one choice goes back once the choice is made (the person, 2026-10-29: "when i select
// someting in a single select menu, like a country i should be brought back after selecting and not just
// stay in the same menu"; Karpo.Dock\Flyout.Inline.cs, docs\widgets.md §392). Held here:
//
//  - **Which levels are one choice** (FlyoutItem.PickOne, set where the rows are made, OptionMenu and
//    MenuPickers): a choice's, a colour's, an icon's, a search's, a choice of the person's own things. A
//    group of switches, a level of rows, a time's parts, a line of text: not.
//  - **On a real menu** (KARPO_UI_TESTS=1): a choice picked — by the pointer, by the keys, in a long list
//    searched first — and the menu shown again for it (Refresh): the level under it, the choice's row
//    saying the choice at its end, lit where the keys picked it (§409: the pointer's, what is under it). A
//    switch flipped in a group stays on the group's level.

using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using Karpo.Tests.Localization;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Menus;

[Collection(InLanguage.Collection)]
public sealed class OneChoiceTests
{
    private const float Scale = 1.5f;

    private static readonly RECT Work = new() { right = (int)(1920 * Scale), bottom = (int)(1040 * Scale) };

    private static readonly POINT Anchor = new() { x = Work.right / 3, y = Work.bottom };

    /// <summary>A widget's menu with one choice of <paramref name="count"/> of Switzerland's cantons, as a dock shows it again after each pick.</summary>
    private sealed class Cantons
    {
        private readonly List<string> names;
        public int Chosen;

        public Cantons(int count) => names = [.. HolidayPlaces.Regions("CH").Take(count).Select(region => region.Name)];

        public List<OptionPick> Picks { get; } = [];

        public IReadOnlyList<string> Names => names;

        /// <summary>The menu's rows as they are now: the widget's buttons and the choice, the canton chosen at its end.</summary>
        public List<FlyoutItem> Rows()
        {
            Picks.Clear();
            var option = new OptionRow("region", OptionRowKind.Choice, S.SettingsHolidaysRegionHeader)
            {
                Glyph = OptionGlyphs.Place,
                Choices = names,
                Chosen = Chosen,
                Set = at => (Chosen = at) >= 0,
            };
            return [.. TheDock.WidgetButtons(), .. OptionMenu.Section([option], Picks)];
        }

        /// <summary>What the dock does with a pick: set, and the menu shown again with the setting as it is now.</summary>
        public void Picked(Flyout menu, FlyoutEvent picked)
        {
            Assert.Equal(FlyoutEventKind.Adjust, picked.Kind);
            var pick = OptionMenu.PickOf(Picks, picked.Id)!;
            Assert.Equal(OptionPickKind.Set, pick.Kind);
            Chosen = pick.Value;
            menu.Refresh(OptionMenu.Updated(menu.ShownItems, Rows()));
            WindowShots.Settle();
        }
    }

    private static Flyout Show(Gfx gfx, HINSTANCE instance, IReadOnlyList<FlyoutItem> items)
    {
        var menu = new Flyout(gfx, instance, 0) { Configures = true };
        menu.Show(gfx, AddListPictures.LookFor(true), Scale, items, FlyoutLayout.List, Anchor, Work, Edge.Bottom, null);
        WindowShots.Settle();
        return menu;
    }

    private static FlyoutEvent Key(Flyout menu, int key) => menu.Message(WM.WM_KEYDOWN, (WPARAM)(nuint)key, 0);

    private static void Type(Flyout menu, string text)
    {
        foreach (var c in text)
        {
            _ = menu.Message(WM.WM_CHAR, (WPARAM)(nuint)c, 0);
        }
    }

    private static FlyoutItem RowOf(Flyout menu) => menu.ShownItems.Single(item => item.Text == S.SettingsHolidaysRegionHeader);

    /// <summary>
    /// The rows say which of their levels are one choice, where they are made: a choice's, a search's, a
    /// colour's, an icon's, a choice of the person's things. A group of switches, a level of rows, the person's
    /// things of no choice, a time's parts, a line of text, a number typed: not.
    /// </summary>
    [Fact]
    public void TheLevelsOfOneChoiceSaySo()
    {
        static FlyoutItem Made(OptionRow row) => OptionMenu.Rows([row], []).Single(item => item.Children is not null);
        OptionOwned owned = new(null, [new OptionThing("Groceries", ""), new OptionThing("Work", "")]);

        Assert.True(Made(new OptionRow("unit", OptionRowKind.Choice, "Unit") { Choices = ["°C", "°F"] }).PickOne);
        Assert.True(Made(new OptionRow("zone", OptionRowKind.Search, "Time zone")).PickOne);
        Assert.True(Made(new OptionRow("tint", OptionRowKind.Color, "Color")).PickOne);
        Assert.True(Made(new OptionRow("icon", OptionRowKind.Glyph, "Icon")).PickOne);
        Assert.True(Made(new OptionRow("list", OptionRowKind.Choice, "List") { Owned = owned }).PickOne);

        Assert.False(Made(new OptionRow("lists", OptionRowKind.Things, "Lists") { Owned = owned }).PickOne);
        Assert.False(Made(new OptionRow("form", OptionRowKind.Level, "Server") { Level = [new OptionRow("on", OptionRowKind.Toggle, "On")] }).PickOne);
        Assert.False(Made(new OptionRow("at", OptionRowKind.Time, "Time")).PickOne);
        Assert.False(Made(new OptionRow("label", OptionRowKind.Text, "Label")).PickOne);
        Assert.False(Made(new OptionRow("size", OptionRowKind.Number, "Size") { SetText = _ => true }).PickOne);
        List<OptionRow> group = [.. Enumerable.Range(0, 3).Select(at => new OptionRow($"t{at}", OptionRowKind.Toggle, $"Switch {at}") { Group = "switches", GroupTitle = "Switches" })];
        Assert.False(OptionMenu.Rows(group, []).Single().PickOne);
    }

    /// <summary>
    /// A choice of five opened in place, one picked by the pointer: shown again for it, the menu is back on
    /// its own rows, the choice's row saying the canton at its end, what is under the pointer lit (§409). Then
    /// by the keys: Enter on a canton, the same, the choice's row lit.
    /// </summary>
    [UiFact]
    public void APickInAOneChoiceLevelGoesBackShowingIt()
    {
        WindowShots.OnHiddenDesktop("one-choice-back", (gfx, instance) =>
        {
            var cantons = new Cantons(5);
            using var menu = Show(gfx, instance, cantons.Rows());
            var row = RowOf(menu);
            _ = menu.Click(row.Id);
            Assert.Equal((1, row.Text), (menu.InlineDepth, menu.InlineTitle));

            var at = menu.RowMiddle(row.Children![3].Id)!.Value;
            cantons.Picked(menu, menu.Click(row.Children![3].Id));
            Assert.Equal((0, menu.IdAt(at)), (menu.InlineDepth, menu.HotId));
            Assert.Equal(cantons.Names[3], RowOf(menu).Value?.Text);

            // By the keys: down to it, Enter opens it, Down twice to the second canton, Enter picks it.
            for (var step = 0; step < 10 && menu.HotId != row.Id; step++)
            {
                _ = Key(menu, VK.VK_DOWN);
            }
            _ = Key(menu, VK.VK_RETURN);
            Assert.Equal(1, menu.InlineDepth);
            var second = RowOf(menu).Children![1];
            for (var step = 0; step < 10 && menu.HotId != second.Id; step++)
            {
                _ = Key(menu, VK.VK_DOWN);
            }
            Assert.Equal(second.Id, menu.HotId);
            cantons.Picked(menu, Key(menu, VK.VK_RETURN));
            Assert.Equal((0, row.Id), (menu.InlineDepth, menu.HotId!.Value));
            Assert.Equal(cantons.Names[1], RowOf(menu).Value?.Text);
            menu.Hide();
        });
    }

    /// <summary>
    /// §409: back from a level by the pointer — a choice clicked, the head clicked, the mouse's back button —
    /// what lights is what the pointer is on now, never the row the level opened from where it is not under
    /// it; with the pointer gone from the menu nothing is. By the keys, that row is lit, the pointer anywhere.
    /// (The person, 2026-10-29, of a folder's line: "when returning it is highlighted until i hover over it
    /// again, this should not be".)
    /// </summary>
    [UiFact]
    public void BackByThePointerLightsWhatThePointerIsOn()
    {
        WindowShots.OnHiddenDesktop("one-choice-pointer", (gfx, instance) =>
        {
            static LPARAM At(POINT at) => (LPARAM)(nint)((at.y << 16) | (at.x & 0xFFFF));
            var cantons = new Cantons(5);
            using var menu = Show(gfx, instance, cantons.Rows());
            var row = RowOf(menu);

            // A canton clicked: back, the pointer where the canton was.
            _ = menu.Click(row.Id);
            var canton = row.Children![3];
            var there = menu.RowMiddle(canton.Id)!.Value;
            _ = menu.Message(WM.WM_MOUSEMOVE, 0, At(there));
            cantons.Picked(menu, menu.Click(canton.Id));
            Assert.Equal(0, menu.InlineDepth);
            Assert.NotEqual(row.Id, menu.HotId);
            Assert.Equal(menu.IdAt(there), menu.HotId);

            // The head clicked: what is where the head was lights.
            _ = menu.Click(row.Id);
            var head = menu.RowMiddle(Flyout.InlineBackId)!.Value;
            _ = menu.Message(WM.WM_MOUSEMOVE, 0, At(head));
            _ = menu.Click(Flyout.InlineBackId);
            Assert.Equal(0, menu.InlineDepth);
            Assert.NotEqual(row.Id, menu.HotId);
            Assert.Equal(menu.IdAt(head), menu.HotId);

            // The pointer gone from the menu, the mouse's back button: nothing lit.
            _ = menu.Click(row.Id);
            _ = menu.Message(WM.WM_MOUSELEAVE, 0, 0);
            _ = menu.Message(WM.WM_XBUTTONUP, (WPARAM)(nuint)(XBUTTON1 << 16), At(new POINT(-1, -1)));
            Assert.Equal(0, menu.InlineDepth);
            Assert.Null(menu.HotId);

            // By the keys, the pointer elsewhere: the row it opened from lit.
            _ = menu.Click(row.Id);
            _ = menu.Message(WM.WM_MOUSEMOVE, 0, At(head));
            _ = Key(menu, VK.VK_ESCAPE);
            Assert.Equal((0, row.Id), (menu.InlineDepth, menu.HotId ?? 0));
            menu.Hide();
        });
    }

    /// <summary>A long choice searched first (§279): "gall" typed, Enter to St. Gallen, Enter picks it: back, saying it.</summary>
    [UiFact]
    public void ALongListSearchedThenPickedGoesBack()
    {
        WindowShots.OnHiddenDesktop("one-choice-searched", (gfx, instance) =>
        {
            var cantons = new Cantons(26);
            using var menu = Show(gfx, instance, cantons.Rows());
            var row = RowOf(menu);
            _ = menu.Click(row.Id);
            Assert.Equal(1, menu.InlineDepth);
            Type(menu, "gall");
            _ = Key(menu, VK.VK_RETURN);
            Assert.Equal("St. Gallen", menu.RowsShown.Single(item => item.Id == menu.HotId).Text);
            cantons.Picked(menu, Key(menu, VK.VK_RETURN));
            Assert.Equal((0, row.Id), (menu.InlineDepth, menu.HotId!.Value));
            Assert.Equal("St. Gallen", RowOf(menu).Value?.Text);
            Assert.Equal(0u, menu.FieldKeys);
            menu.Hide();
        });
    }

    /// <summary>
    /// §411 (R5): "Add a toggle" goes back once one is picked: the menu shown again for it (as the dock shows it, the
    /// new row lit, OptionMenu.ListRowOf) is on the list's level, the toggle just added lit among the others.
    /// </summary>
    [UiFact]
    public void AddATogglesPickGoesBackToTheListTheNewRowLit()
    {
        WindowShots.OnHiddenDesktop("one-choice-add", (gfx, instance) =>
        {
            var toggles = ListKind.Toggles.New();
            toggles.Toggles = new TogglesOptions { Shown = [Toggles.All[0].Toggle] };
            List<OptionPick> picks = [];
            List<FlyoutItem> Rows()
            {
                picks.Clear();
                return [.. TheDock.WidgetButtons(), .. OptionMenu.Section(OptionRows.Of(toggles), picks)];
            }
            using var menu = Show(gfx, instance, Rows());
            var add = menu.ShownItems.Single(item => item.Text == S.SettingsWidgetTogglesAdd);
            _ = menu.Click(add.Id);
            Assert.Equal(1, menu.InlineDepth);
            var picked = menu.Click(add.Children![0].Id);
            Assert.Equal(FlyoutEventKind.Adjust, picked.Kind);
            var pick = OptionMenu.PickOf(picks, picked.Id)!;
            Assert.True(OptionMenu.Apply(toggles, pick));
            menu.Refresh(OptionMenu.Updated(menu.ShownItems, Rows()), OptionMenu.ListRowOf(picks, pick.Key));
            Assert.Equal(0, menu.InlineDepth);
            Assert.Equal(Toggles.All[1].Name, menu.ShownItems.Single(item => item.Id == menu.HotId).Text);
            menu.Hide();
        });
    }

    /// <summary>
    /// §589: the settings panel's popup closes on the flyout's own rule for one choice (Flyout.OneChoice, Dock.PanelDropped):
    /// Add a face's every face and Add a toggle's every toggle are one choice of their list, as a choice's ticked row is; a
    /// switch, a stepper, a field are not. Until §589 the popup asked the pick's kind (a Set, a Field with a find), which knew
    /// no Add's row: the person, 2026-11-09, "when adding a face the add face dropdown does not close after adding".
    /// </summary>
    [Fact]
    public void AnAddsRowIsOneChoiceOfItsList()
    {
        var folder = BarKind.FolderList.New();
        folder.Folder!.Faces = [FolderFace.Of(FolderFaces.ClockId)];
        var addFace = OptionMenu.FaceSection(folder.Folder!, []).Single(item => item.Adds);
        var faces = addFace.Children!.Where(item => item.Id != 0 && item.Control is null).ToList();
        Assert.True(faces.Count > 10);
        Assert.All(faces, face => Assert.True(Flyout.OneChoice(face, addFace.Children!), face.Text));
        // Its Find field is no choice.
        Assert.False(Flyout.OneChoice(addFace.Children!.Single(item => item.Control is FlyoutField), addFace.Children!));

        var toggles = ListKind.Toggles.New();
        toggles.Toggles = new TogglesOptions { Shown = [Toggles.All[0].Toggle] };
        var rows = OptionMenu.Rows(OptionRows.Of(toggles), []);
        var addToggle = rows.Single(item => item.Adds);
        Assert.All(addToggle.Children!, toggle => Assert.True(Flyout.OneChoice(toggle, addToggle.Children!), toggle.Text));
        // The list's own rows (a grip, a Remove) are none.
        Assert.All(rows.Where(row => row.Reorder != 0), row => Assert.False(Flyout.OneChoice(row, rows)));

        var choice = OptionMenu.Rows([new OptionRow("unit", OptionRowKind.Choice, "Unit") { Choices = ["°C", "°F"] }], []).Single();
        Assert.All(choice.Children!, unit => Assert.True(Flyout.OneChoice(unit, choice.Children!)));
        var level = OptionMenu.Rows(
            [new OptionRow("on", OptionRowKind.Toggle, "On"), new OptionRow("size", OptionRowKind.Number, "Size") { Least = 1, Most = 9, Value = 3, Set = _ => true }], []);
        Assert.All(level, row => Assert.False(Flyout.OneChoice(row, level), row.Text));
    }

    /// <summary>
    /// §589: on a real flyout of a popup's rows (the panel's dropdown shows its level's rows as the menu's own, Dock.PanelPopup):
    /// a face of Add a face clicked is a choice made — what the dock asks before it closes the popup —, taken once; a switch
    /// flipped is none.
    /// </summary>
    [UiFact]
    public void AFacePickedInAddAFacesPopupIsAChoiceMade()
    {
        WindowShots.OnHiddenDesktop("one-choice-popup", (gfx, instance) =>
        {
            var folder = BarKind.FolderList.New();
            folder.Folder!.Faces = [FolderFace.Of(FolderFaces.ClockId)];
            List<OptionPick> picks = [];
            var gallery = OptionMenu.FaceSection(folder.Folder!, picks).Single(item => item.Adds).Children!;
            using (var menu = Show(gfx, instance, gallery))
            {
                var face = gallery.First(item => item.Id != 0 && item.Control is null && item.Enabled);
                var picked = menu.Click(face.Id);
                Assert.Equal(FlyoutEventKind.Adjust, picked.Kind);
                Assert.Equal(OptionPickKind.AddFace, OptionMenu.PickOf(picks, picked.Id)!.Kind);
                Assert.True(menu.TakeChoseOne());
                Assert.False(menu.TakeChoseOne());
                menu.Hide();
            }

            var on = false;
            List<OptionRow> switches = [new OptionRow("on", OptionRowKind.Toggle, "On") { Set = value => (on = value != 0) || true }];
            var rows = OptionMenu.Rows(switches, []);
            using (var menu = Show(gfx, instance, rows))
            {
                Assert.Equal(FlyoutEventKind.Adjust, menu.Click(rows[0].Id).Kind);
                Assert.False(menu.TakeChoseOne());
                menu.Hide();
            }
        });
    }

    /// <summary>A group of switches is no choice of one: a switch flipped, the menu shown again, still on the group's level.</summary>
    [UiFact]
    public void AGroupOfSwitchesStaysOnItsLevel()
    {
        WindowShots.OnHiddenDesktop("one-choice-many", (gfx, instance) =>
        {
            var on = new bool[3];
            List<OptionPick> picks = [];
            List<FlyoutItem> Rows()
            {
                picks.Clear();
                List<OptionRow> group = [.. Enumerable.Range(0, 3).Select(at => new OptionRow($"t{at}", OptionRowKind.Toggle, $"Switch {at}") { On = on[at], Group = "switches", GroupTitle = "Switches", Set = value => (on[at] = value != 0) || true })];
                return [.. TheDock.WidgetButtons(), .. OptionMenu.Section(group, picks)];
            }
            using var menu = Show(gfx, instance, Rows());
            var row = menu.ShownItems.Single(item => item.Text == "Switches");
            _ = menu.Click(row.Id);
            Assert.Equal(1, menu.InlineDepth);
            var flipped = menu.Click(row.Children![1].Id);
            Assert.Equal(FlyoutEventKind.Adjust, flipped.Kind);
            on[1] = true;
            menu.Refresh(OptionMenu.Updated(menu.ShownItems, Rows()));
            Assert.Equal(("Switches", 1), (menu.InlineTitle, menu.InlineDepth));
            menu.Hide();
        });
    }
}
