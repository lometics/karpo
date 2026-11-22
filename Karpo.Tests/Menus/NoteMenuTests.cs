// menus: a note line's note in its edit menu (docs\widgets.md §317), on a real menu on a desktop of the
// test's own (KARPO_UI_TESTS=1): the "Note" row's level in place, "New note" at its top, every note a row
// ticked where the line shows it, a click on one showing it; its pencil (or F2) opening its name's field in
// place, Enter keeping and going back onto its row, a Refresh keeping that level open; its bin (or Delete)
// saying so, for the dock to ask and delete. The picture, note-menu-level-dark.png at 150 %, in KARPO_PNG_DIR.

using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using Karpo.Tests.Localization;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Menus;

[Collection(InLanguage.Collection)]
public sealed class NoteMenuTests
{
    private const float Scale = 1.5f;

    private static LPARAM At(POINT point) => (LPARAM)(nint)((point.y << 16) | (point.x & 0xFFFF));

    private static FlyoutEvent Key(Flyout menu, int key) => menu.Message(WM.WM_KEYDOWN, (WPARAM)(nuint)key, 0);

    /// <summary>A click (down and up) at <paramref name="point"/>, after the pointer came there; what the release said.</summary>
    private static FlyoutEvent Click(Flyout menu, POINT point)
    {
        _ = menu.Message(WM.WM_MOUSEMOVE, 0, At(point));
        _ = menu.Message(WM.WM_LBUTTONDOWN, 1, At(point));
        return menu.Message(WM.WM_LBUTTONUP, 0, At(point));
    }

    /// <summary>A notebook of its own with three notes, and a note line showing the second; the menu's rows for it.</summary>
    private static void WithNotes(Action<Notebook, CellItem, Func<(List<FlyoutItem> Items, List<OptionPick> Picks)>> body)
    {
        var folder = Path.Combine(Isolation.Folder, "note-menu", Guid.NewGuid().ToString("N"));
        using var book = new Notebook(folder);
        _ = book.AddNote("Shopping", DateTimeOffset.Now);
        var ideas = book.AddNote("Ideas", DateTimeOffset.Now);
        _ = book.AddNote("Week plan", DateTimeOffset.Now);
        var line = ListKind.Note.New();
        line.Note!.Note = ideas;
        var devices = new OptionDevices { Book = book };
        body(book, line, () =>
        {
            List<OptionPick> picks = [];
            return (OptionMenu.Section(OptionRows.Of(line, devices), picks), picks);
        });
    }

    private static Flyout Show(Gfx gfx, HINSTANCE instance, IReadOnlyList<FlyoutItem> items, bool dark = true)
    {
        var work = new RECT { right = (int)(1920 * Scale), bottom = (int)(1040 * Scale) };
        var menu = new Flyout(gfx, instance, 0) { Configures = true, Configuration = true };
        menu.Show(gfx, AddListPictures.LookFor(dark), Scale, items, FlyoutLayout.List, new POINT { x = work.right / 3, y = work.bottom }, work, Edge.Bottom, null);
        WindowShots.Settle();
        return menu;
    }

    [UiFact]
    public void TheNotesLevelPicksRenamesAndDeletesInPlace()
    {
        WindowShots.OnHiddenDesktop("note-menu", (gfx, instance) => WithNotes((book, line, rows) =>
        {
            var (items, picks) = rows();
            var note = items.Single(item => item.Text == S.SettingsWidgetNoteNoteHeader);
            Assert.Equal("Ideas", note.Value!.Text);
            using var menu = Show(gfx, instance, items);
            _ = menu.Click(note.Id);
            Assert.Equal((1, S.SettingsWidgetNoteNoteHeader), (menu.InlineDepth, menu.InlineTitle));
            // Its head, every note, the one shown ticked, then "New note" right under them (§411).
            Assert.Equal([S.SettingsWidgetNoteNoteHeader, "Shopping", "Ideas", "Week plan", S.SettingsWidgetNotebookNewNote], menu.RowsShown.Select(item => item.Text));
            Assert.Equal([false, true, false], menu.RowsShown.Skip(1).Take(3).Select(item => item.Checked == true));

            // A click on a note's row (off its buttons) picks it, and the level stays.
            var shopping = note.Children!.Single(item => item.Text == "Shopping");
            Assert.Equal(new FlyoutEvent(FlyoutEventKind.Adjust, shopping.Id), Click(menu, menu.RowMiddle(shopping.Id)!.Value));
            Assert.Equal(OptionPickKind.Set, OptionMenu.PickOf(picks, shopping.Id)!.Kind);
            Assert.Equal(1, menu.InlineDepth);

            // Its pencil opens its name's field in place, with the keys, its words in it.
            var tools = ((FlyoutControl.Tools)shopping.Control!).Buttons;
            var field = Assert.Single(tools[0].Level!);
            Assert.Equal(default, Click(menu, menu.ToolMiddle(shopping.Id, 0)!.Value));
            Assert.Equal((2, S.SettingsWidgetNotebookRename, field.Id), (menu.InlineDepth, menu.InlineTitle, menu.FieldKeys));
            // A Refresh while it is open (the notebook changed) keeps it.
            menu.Refresh(rows().Items);
            Assert.Equal(2, menu.InlineDepth);
            foreach (var c in "Groceries")
            {
                _ = menu.Message(WM.WM_CHAR, (WPARAM)(nuint)c, 0);
            }
            _ = Key(menu, VK.VK_RETURN);
            // Kept, and back on the note's row.
            Assert.Equal((1, shopping.Id), (menu.InlineDepth, menu.HotId!.Value));
            var kept = menu.TakeFieldCommit(field.Id)!;
            Assert.Equal("Groceries", kept.Text);
            Assert.True(FieldPicks.Keep(line, OptionMenu.PickOf(picks, field.Id)!, kept, new OptionDevices { Book = book }));
            Assert.Equal("Groceries", book.Current.Notes[0].Title);

            // From the keys: F2 opens the field, Escape goes back; Enter picks; Delete is the bin's.
            Assert.Equal(FlyoutEventKind.Nothing, Key(menu, VK.VK_F2).Kind);
            Assert.Equal(2, menu.InlineDepth);
            _ = Key(menu, VK.VK_ESCAPE);
            Assert.Equal((1, shopping.Id), (menu.InlineDepth, menu.HotId!.Value));
            Assert.Equal(new FlyoutEvent(FlyoutEventKind.Adjust, shopping.Id), Key(menu, VK.VK_RETURN));
            Assert.Equal(new FlyoutEvent(FlyoutEventKind.Adjust, tools[1].Id), Key(menu, VK.VK_DELETE));
            // The bin's pick asks before it deletes (the dock's DeleteAsk), and then deletes it.
            var bin = OptionMenu.PickOf(picks, tools[1].Id)!;
            Assert.Equal(S.SettingsWidgetNotebookDeleteNoteBody("Groceries"), OptionMenu.AskOf(line, bin, new OptionDevices { Book = book })!.Content);
            Assert.True(OptionMenu.Apply(line, bin, new OptionDevices { Book = book }));
            Assert.Equal(["Ideas", "Week plan"], book.Current.Notes.Select(each => each.Title));

            // "New note" makes one and shows it: the menu shown again lists it, ticked.
            var add = note.Children![^1];
            Assert.Equal(new FlyoutEvent(FlyoutEventKind.Adjust, add.Id), Click(menu, menu.RowMiddle(add.Id)!.Value));
            Assert.True(OptionMenu.Apply(line, OptionMenu.PickOf(picks, add.Id)!, new OptionDevices { Book = book }));
            menu.Refresh(rows().Items);
            Assert.Equal(1, menu.InlineDepth);
            Assert.Equal(["Ideas", "Week plan", S.WidgetNoteNewNote, S.SettingsWidgetNotebookNewNote], menu.RowsShown.Skip(1).Select(item => item.Text));
            Assert.True(menu.RowsShown[^2].Checked);
        }));
    }

    /// <summary>The Note level of a note line's menu, dark, 150 %, the pointer on a note's row: note-menu-level-dark.png.</summary>
    [UiFact]
    public void TheNotesLevelIsPictured()
    {
        WindowShots.OnHiddenDesktop("note-menu-picture", (gfx, instance) => WithNotes((_, _, rows) =>
        {
            var (items, _) = rows();
            var note = items.Single(item => item.Text == S.SettingsWidgetNoteNoteHeader);
            using var menu = Show(gfx, instance, items);
            Assert.True(WindowShots.SaveTogether([.. menu.OpenPanels], "note-menu-top-dark.png", (0, 0, 0)));
            _ = menu.Click(note.Id);
            menu.PointTo(note.Children!.Single(item => item.Text == "Week plan").Id);
            WindowShots.Settle(300);
            Assert.True(WindowShots.SaveTogether([.. menu.OpenPanels], "note-menu-level-dark.png", (0, 0, 0)));
        }));
    }
}
