// §521, pass two of the settings panel (the person, 2026-11-07: "now go ahead and check all the settings and make sure that
// they are implemented with the proper controlls. (e.g. dropdowns, also think about how many items are realistically there
// etc., and other components as well.)"). Held here:
//
//  - **The control of every row** of every page — Karpo, Display, Layouts, Taskbar, Look, a folder's, every widget's and
//    face's of the corpus, every expander open — is the one its value calls for, read from the one row model alone: a
//    switch for on and off; a segmented control for a fixed two or three short ones; a dropdown for more, for long words,
//    for a list that grows (the layouts) and for a search (its field over its finds); a number box typed in; a text box
//    typed in place; an expander for a level and for one of the person's things with rows of its own; a button for a row
//    that acts. KARPO_AUDIT_FILE: the table of them, as §521's audit was written from it.
//  - **A number typed** is held to its row's range and steps (OptionUnits.Typed).
//  - **A text box** keeps its words on Enter and as it loses the keys, puts back what it said on Escape; a number box the
//    same (the real window, KARPO_UI_TESTS=1).
//  - **A control's list** (a ComboBox's) opens with its chosen row lit and in sight, about ten rows tall, and a letter typed
//    goes to the row it starts.

using System.Text;
using Karpo.Dock;
using Karpo.Tests.Islands;
using Karpo.Tests.Menus;
using TerraFX.Interop.Windows;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Edit;

public sealed class PanelControlsTests
{
    /// <summary>Every page the panel shows, and every settings menu of the corpus as a widget's page.</summary>
    internal static IEnumerable<PanelPageView> Pages()
    {
        foreach (var page in SettingsPanelTests.Content(widget: true).Pages)
        {
            yield return page;
        }
        foreach (var menu in MenuCorpus.All().Where(menu => menu.Settings && !MenuRulesTests.Pages.Contains(menu.Name)))
        {
            yield return PanelRows.Page(PanelPage.Widget, menu.Name, "", menu.Items);
        }
    }

    /// <summary>The control <paramref name="row"/> calls for by its value and how many it may realistically be, as §521's audit says.</summary>
    private static PanelRowKind Wanted(PanelRow row)
    {
        var item = row.Item;
        // §552: a folder's face's group is its card: no menu row's control.
        if (row.Kind == PanelRowKind.Group)
        {
            return row.Kind;
        }
        if (item.Header)
        {
            return PanelRowKind.Heading;
        }
        // §592: the Appearance's parts' levels tabs, their run a tab strip.
        if (row.Key.EndsWith("/" + PanelTabs.Key, StringComparison.Ordinal))
        {
            return PanelRowKind.Tabs;
        }
        if (PanelTabs.Is(item))
        {
            return PanelRowKind.Tab;
        }
        // §670: an inline picker's body its cells, its search and its hex boxes, in its card.
        if (row.Key.EndsWith("/" + PanelPickers.BodyKey, StringComparison.Ordinal))
        {
            return PanelRowKind.PickerBody;
        }
        if (row.Key.EndsWith("/" + PanelPickers.SearchKey, StringComparison.Ordinal) || row.Key.EndsWith("/" + PanelPickers.HexKey, StringComparison.Ordinal))
        {
            return PanelRowKind.Text;
        }
        // §536: a look's preview, at the top of the level that edits it.
        if (item.Shows is not null && row.Key.EndsWith("/" + PanelRows.PreviewKey, StringComparison.Ordinal))
        {
            return PanelRowKind.Preview;
        }
        switch (item.Control)
        {
            case FlyoutControl.Toggle or FlyoutControl.Radio:
                return PanelRowKind.Toggle;
            case FlyoutControl.Stepper:
                return PanelRowKind.Stepper;
            // §672: a row of segments its choices' inline picker.
            case FlyoutControl.Segments:
                return PanelRowKind.Picker;
            // §529: a folder's face its settings nested under it, its Remove at its end; §609 a holiday calendar its region in its row.
            case FlyoutControl.Tools when item.Nests || item.Children is [{ Beside: true }]:
                return PanelRowKind.Item;
            case FlyoutControl.Tools when item.Children is { Count: > 0 } && !item.PickOne:
                return PanelRowKind.Expander;
            case FlyoutControl.Tools:
                return PanelRowKind.Item;
            // A form's field (an account's address): a text box.
            case FlyoutField:
                return PanelRowKind.Text;
        }
        if (item.Note)
        {
            return PanelRowKind.Note;
        }
        // §529: a link with words to say (a calendar's accounts) a line of them, the link at its end.
        if (item.Opens is not null)
        {
            return item.Detail is { Length: > 0 } ? PanelRowKind.Words : PanelRowKind.Link;
        }
        if (item.Children is not { Count: > 0 } children)
        {
            // §628: one that pictures what it makes (Add a layout's presets) a tile.
            return item.Shows is not null ? PanelRowKind.Pick : PanelRowKind.Action;
        }
        var fields = children.Where(kid => kid.Control is FlyoutField).ToList();
        if (fields.Count > 0 && children.All(kid => kid.Separator || kid.Header || kid.Control is FlyoutField || kid.Note))
        {
            // §676: a lookup its search over what it finds, inline.
            return PanelPickers.Lookup(children) is not null && !item.Adds ? PanelRowKind.Picker : ((FlyoutField)fields[0].Control!).Finds ? PanelRowKind.Choice : PanelRowKind.Text;
        }
        if (item.PickOne)
        {
            if (row.Kind == PanelRowKind.Color)
            {
                return PanelRowKind.Color;
            }
            // §672: a choice of one its picker inline, on every page, however few its choices.
            return PanelPickers.Takes(item, children) ? PanelRowKind.Picker : PanelRowKind.Choice;
        }
        return PanelRowKind.Expander;
    }

    /// <summary>
    /// Every row of every page is the control its value calls for (see the top of the file); a number box's value types
    /// where its row is a number's; no row is drawn as a card that acts but a row that acts. KARPO_AUDIT_FILE: the table.
    /// </summary>
    [Fact]
    public void EveryRowIsTheControlItsValueCallsFor()
    {
        List<string> wrong = [];
        var table = new StringBuilder("page › row | value | count | control\n");
        foreach (var page in Pages())
        {
            foreach (var row in PanelRows.All(page.Rows))
            {
                var wanted = Wanted(row);
                if (row.Kind != wanted)
                {
                    wrong.Add($"[{page.Title}] {row.Key}: {row.Kind}, not {wanted}");
                }
                var count = row.Item.Children?.Count(kid => !kid.Separator && !kid.Header) ?? 0;
                var value = row.Item.Control?.GetType().Name ?? (row.Item.PickOne ? "choice" : row.Item.Children is { Count: > 0 } ? "level" : "act");
                table.AppendLine($"{page.Title} › {row.Key[(row.Key.IndexOf('/') + 1)..]} | {value}{(row.Item.Grows ? " (grows)" : "")} | {count} | {row.Kind}");
            }
        }
        if (Environment.GetEnvironmentVariable("KARPO_AUDIT_FILE") is { Length: > 0 } file)
        {
            File.WriteAllText(file, table.ToString());
        }
        Assert.True(wrong.Count == 0, string.Join("\n", wrong.Take(40)));
        // The kinds the pass added are on the pages: a text box, a number typed, a button, a thing's expander.
        var all = Pages().SelectMany(page => PanelRows.All(page.Rows)).ToList();
        Assert.Contains(all, row => row.Kind == PanelRowKind.Text && row.Field is not null);
        Assert.Contains(all, row => row.Kind == PanelRowKind.Stepper && row.Field is not null);
        Assert.Contains(all, row => row.Kind == PanelRowKind.Action);
        Assert.Contains(all, row => row.Kind == PanelRowKind.Expander && row.Tools.Count > 0);
        // §676: a search (a clock's zone) is an inline picker of its search over what it finds.
        Assert.Contains(all, row => row.Kind == PanelRowKind.Picker && PanelPickers.Lookup(row.Item.Children ?? []) is not null);
        // §672: what grows (the layouts a display shows, the fonts) an inline picker too, however few.
        Assert.All(all.Where(row => row.Item.Grows && !row.Adds), row => Assert.Contains(row.Kind, new[] { PanelRowKind.Picker, PanelRowKind.PickerBody }));
    }

    /// <summary>§521: a number typed in its box is held to its row's range and to its steps from its least; words round it no matter.</summary>
    [Theory]
    [InlineData("48 px", 48)]
    [InlineData(" 52", 52)]
    [InlineData("50", 52)]
    [InlineData("49", 48)]
    [InlineData("1000", 96)]
    [InlineData("-5", 24)]
    [InlineData("10", 24)]
    [InlineData("40", null)]
    [InlineData("abc", null)]
    [InlineData("", null)]
    public void ANumberTypedIsHeldToItsRange(string typed, int? set)
    {
        var row = new OptionRow("k", OptionRowKind.Number, "Size") { Value = 40, Least = 24, Most = 96, Step = 4, Unit = OptionUnit.Pixels };
        Assert.Equal(set, OptionUnits.Typed(row, typed));
    }

    /// <summary>§521: a number box of the panel takes a number typed (its row's field), kept as a menu's field keeps it: within its range.</summary>
    [Fact]
    public void ANumberBoxTakesANumberTyped()
    {
        var config = MenuCorpus.Fresh();
        var item = ListKind.Calendar.New();
        var place = new OptionPlace(config, Dark: true);
        List<OptionPick> picks = [];
        var items = OptionMenu.Settings(item, picks, null, place, FolderSide.Face, null);
        var row = PanelRows.All(PanelRows.Page(PanelPage.Widget, "", "", items).Rows).First(each => each.Kind == PanelRowKind.Stepper && each.Field is not null);
        var pick = OptionMenu.PickOf(picks, row.Field!.Id)!;
        Assert.Equal(OptionPickKind.Field, pick.Kind);
        var option = OptionRows.Find(OptionRows.Of(item, null, place), pick.Key)!;
        Assert.True(FieldPicks.KeepRow(option, new FieldCommit(pick.Id, "9999", null)));
        Assert.Equal(option.Most, OptionRows.Find(OptionRows.Of(item, null, place), pick.Key)!.Value);
        Assert.False(FieldPicks.KeepRow(OptionRows.Find(OptionRows.Of(item, null, place), pick.Key), new FieldCommit(pick.Id, "nothing", null)));
    }

    private static Look Still => AddListPictures.LookFor(dark: true) with { Animations = false };

    /// <summary>
    /// §521 on the real window: a layout's name typed in its box in place — Enter keeps the words (the dock asked for its
    /// field's id and them), Escape puts back what it said and asks nothing, a press elsewhere keeps them; the Taskbar page's
    /// Size typed in its number box, Enter keeping it.
    /// </summary>
    [UiFact]
    public void ATextBoxAndANumberBoxAreTypedInPlace()
    {
        WindowShots.OnHiddenDesktop("panel-typed", (gfx, instance) =>
        {
            using var deck = new EditDeck(gfx, instance, 0);
            List<EditDeckEvent> asked = [];
            deck.Invoked = asked.Add;
            var content = SettingsPanelTests.Content();
            var room = new EditDeckRoom(new RectI(0, 0, 2880, 1620), 0, 72, Work: new RectI(0, 0, 2880, 1548));
            deck.Memory = new PanelMemory();
            deck.Memory.Show(PanelPage.Layouts);
            var layouts = content.Of(PanelPage.Layouts)!;
            // §585: the first layout's expander, under the displays.
            deck.Memory.Open([layouts.Rows.First(row => row.Kind == PanelRowKind.Expander && row.Drags).Key]);
            deck.Show(gfx, Still, 1.5f, room, EditDeckState.Plain, content, PanelPage.Layouts);
            var name = deck.Layout!.Blocks.SelectMany(block => block.Lines).Single(line => line.Inner && line.Row.Kind == PanelRowKind.Text);
            var box = deck.Layout.Controls(name)[0].Hit;
            Assert.Equal(PanelHitKind.Text, box.Kind);
            Assert.Equal(EditDeckAction.None, EditDeckClickTests.Click(deck, box).Action);
            Assert.Equal(name.Row.Value, deck.Editing);
            Assert.True(deck.Typing);
            // Typed over (all its words chosen as it took the keys), Enter keeps them.
            foreach (var c in "Work desk")
            {
                _ = deck.Message(deck.Hwnd, WM.WM_CHAR, c, 0);
            }
            Assert.Equal("Work desk", deck.Editing);
            _ = deck.Message(deck.Hwnd, WM.WM_KEYDOWN, VK.VK_RETURN, 0);
            Assert.Null(deck.Editing);
            Assert.Equal((EditDeckAction.Type, box.Id, "Work desk", PanelPage.Layouts), (asked[^1].Action, asked[^1].Id, asked[^1].Text, asked[^1].Page));
            // Escape: what it said again, nothing asked.
            var before = asked.Count;
            _ = EditDeckClickTests.Click(deck, box);
            _ = deck.Message(deck.Hwnd, WM.WM_CHAR, 'x', 0);
            Assert.True(deck.ClearSearch());
            Assert.Null(deck.Editing);
            Assert.Equal(before, asked.Count);
            // A press elsewhere keeps what was typed.
            _ = EditDeckClickTests.Click(deck, box);
            _ = deck.Message(deck.Hwnd, WM.WM_CHAR, 'y', 0);
            Assert.Equal(EditDeckAction.Open, EditDeckClickTests.Click(deck, new PanelHit(PanelHitKind.Nav, Page: PanelPage.Taskbar)).Action);
            deck.Show(gfx, Still, 1.5f, room, EditDeckState.Plain, content, PanelPage.Taskbar);
            Assert.Equal(("y", PanelPage.Taskbar), (asked[^1].Text, deck.Page));
            // The Taskbar page's Size: its number typed in its box.
            var size = deck.Layout!.Blocks.SelectMany(block => block.Lines).Single(line => line.Row.Text == S.SettingsLayoutsSizeHeader);
            var number = deck.Layout.Controls(size)[0].Hit;
            Assert.Equal(PanelHitKind.Number, number.Kind);
            _ = EditDeckClickTests.Click(deck, number);
            foreach (var c in "64")
            {
                _ = deck.Message(deck.Hwnd, WM.WM_CHAR, c, 0);
            }
            _ = deck.Message(deck.Hwnd, WM.WM_KEYDOWN, VK.VK_RETURN, 0);
            Assert.Equal((EditDeckAction.Type, number.Id, "64"), (asked[^1].Action, asked[^1].Id, asked[^1].Text));
        });
    }

    /// <summary>
    /// §521 on the real window: a control's list — thirty rows, the 25th chosen — opens with the chosen row lit and in sight,
    /// about ten rows tall; "R" typed lights the first row that starts with it, typed again the next.
    /// </summary>
    [UiFact]
    public void AControlsListOpensAtItsChoiceAndJumpsToALetter()
    {
        WindowShots.OnHiddenDesktop("panel-combo", (gfx, instance) =>
        {
            string[] words = ["Alpha", "Bravo", "Charlie", "Delta", "Echo", "Foxtrot", "Golf", "Hotel", "India", "Juliett", "Kilo", "Lima", "Mike", "November",
                "Oscar", "Papa", "Quebec", "Romeo", "Rio", "Sierra", "Tango", "Uniform", "Victor", "Whiskey", "Xray", "Yankee", "Zulu", "Zero", "Zinc", "Zone"];
            List<FlyoutItem> items = [.. words.Select((word, at) => new FlyoutItem((uint)(100 + at), word, Checked: at == 24, Stays: true))];
            var work = new RECT { left = 0, top = 0, right = 2880, bottom = 1548 };
            var control = new RECT { left = 1000, top = 300, right = 1360, bottom = 348 };
            using var menu = new Flyout(gfx, instance, 0) { Configures = true };
            menu.Show(gfx, Still, 1.5f, items, FlyoutLayout.List, new POINT { x = control.left, y = control.bottom }, work, Karpo.Core.Edge.Top, null, 4, control);
            WindowShots.Settle();
            Assert.Equal(124u, menu.HotId);
            var panel = menu.PanelOnScreen;
            Assert.Equal((control.left, control.bottom + 6), (panel.left, panel.top));
            Assert.True(panel.right - panel.left >= control.right - control.left);
            // About ten rows: far short of thirty.
            Assert.InRange(panel.bottom - panel.top, (int)(8 * 32 * 1.5f), (int)(12 * 40 * 1.5f));
            _ = menu.Message(WM.WM_CHAR, 'r', 0);
            Assert.Equal(117u, menu.HotId);
            _ = menu.Message(WM.WM_CHAR, 'r', 0);
            Assert.Equal(118u, menu.HotId);
            menu.Hide();
        });
    }

    /// <summary>
    /// §551 (the person, 2026-11-07: "use icons for remove or delete for example and not just the text, apply this to similar
    /// buttons too"): what acts on one thing of a list — Remove, Delete, Duplicate, Rename, Sign in again — is an icon button
    /// at its row's end, 32 DIP square, its name its tooltip and what a screen reader says; no row of words does it. What makes
    /// something, or acts on a page as a whole (Add a layout, Add an entry, Reset to default), stays a button of
    /// words.
    /// </summary>
    [Fact]
    public void AThingsOwnActionsAreIconButtonsAndAPagesAreWords()
    {
        string[] thingActs =
        [
            S.DockMenuRemove, S.SettingsCommonRemove, S.SettingsLayoutsDelete, S.SettingsLayoutsDuplicate, S.SettingsWidgetNotebookRename,
            S.SettingsWidgetNotebookDelete, S.DockMenuAccountsSignInAgain, S.SettingsWidgetTogglesRemove,
        ];
        var rows = Pages().Concat(SettingsPanelTests.Content(widget: true, kind: ListKind.Toggles, edit: SceneTreeTests.PersonsBarMenu).Pages)
            .SelectMany(page => PanelRows.All(page.Rows)).ToList();
        Assert.DoesNotContain(rows, row => row.Kind == PanelRowKind.Action && thingActs.Contains(row.Text));
        var tools = rows.SelectMany(row => row.Tools).ToList();
        Assert.All(tools, tool => Assert.True(tool.Swatch is not null || tool.Glyph.Length > 0, tool.Name));
        foreach (var (name, glyph) in new[]
        {
            (S.SettingsLayoutsDuplicate, OptionGlyphs.Copy), (S.SettingsLayoutsDelete, OptionGlyphs.Delete), (S.DockMenuRemove, OptionGlyphs.Delete),
            (S.SettingsCommonRemove, OptionGlyphs.Delete), (S.DockMenuAccountsSignInAgain, OptionGlyphs.SignIn),
        })
        {
            Assert.Contains(tools, tool => tool.Name == name && tool.Glyph == glyph);
        }

        // Laid out, every page with every expander open: each a square, its tooltip its name, a screen reader's name its name
        // and its row's; the page's own buttons words, wider than a square.
        const float scale = 1.5f;
        float Px(float dip) => MathF.Round(dip * scale, MidpointRounding.AwayFromZero);
        var content = SettingsPanelTests.Content(widget: true, edit: SceneTreeTests.PersonsBarMenu);
        var open = content.Pages.SelectMany(page => PanelRows.All(page.Rows)).Where(row => row.Kind == PanelRowKind.Expander).Select(row => row.Key).ToHashSet();
        var met = new HashSet<string>(StringComparer.Ordinal);
        foreach (var page in content.Pages)
        {
            var layout = new PanelLayout(new SettingsPanelTests.Words(), scale, new PanelInput(0, content, page.Page, open), PanelLayout.SizeOf(scale, 1548));
            var said = DeckUia.Things(layout, bar: false);
            foreach (var line in layout.Blocks.SelectMany(block => block.Lines))
            {
                foreach (var (hit, rect) in layout.Controls(line))
                {
                    if (hit.Kind == PanelHitKind.Tool && PanelSwatch.Of(line.Row.Tools[hit.Index]) is null)
                    {
                        var tool = line.Row.Tools[hit.Index];
                        Assert.Equal((Px(32), Px(32)), (rect.Width, rect.Height));
                        Assert.Equal(tool.Name, layout.TipOf(hit));
                        Assert.Equal(tool.Name + " · " + line.Row.Text, said.Single(thing => thing.Hit == hit).Name);
                        met.Add(tool.Name);
                    }
                    // §628: a way to add one leading to its page (Add a layout) a link, its words its button's as a list's is.
                    if (hit.Kind == PanelHitKind.Act || (hit.Kind is PanelHitKind.Pop or PanelHitKind.Link && line.Row.Adds))
                    {
                        // §610: a button beside a dropdown (the palette's reset) its whole name its tooltip: "Reset Karpo to default".
                        var own = line.Row.Own(hit.Key);
                        // §673: the palette's reset at the tabs' end its words where every tab stands whole beside them, else its icon.
                        Assert.True(rect.Width > Px(32) || (line.Row.Kind == PanelRowKind.Tabs && rect.Width == layout.BesideWide(line)), own.Text);
                        Assert.Equal(ReferenceEquals(own, line.Row) ? null : own.Detail, layout.TipOf(hit));
                        met.Add(own.Text);
                    }
                }
            }
        }
        Assert.Superset(new HashSet<string>([S.SettingsLayoutsDuplicate, S.SettingsLayoutsDelete, S.DockMenuRemove, S.SettingsCommonRemove, S.DockMenuAccountsSignInAgain,
            S.SettingsLayoutsAdd, S.SettingsBarMenuAdd, S.SettingsBarMenuReset]), met);
    }
}
