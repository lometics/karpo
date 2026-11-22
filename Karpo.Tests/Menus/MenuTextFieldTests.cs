// menus: a menu's text field, its keys (Karpo.Dock\MenuTextField.cs, docs\widgets.md §219): what each
// key does to what it holds and what it tells the menu to do next. The editing itself is TextEditing's,
// held by its own tests (TextEditingTests); held here is the field's part: Enter keeps, Escape puts
// back and then lets go, Up and Down leave, every other key is the field's and changes nothing it
// shouldn't, and the setting changing under it never takes what somebody is typing.

using Karpo.Dock;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Menus;

public sealed class MenuTextFieldTests
{
    private string clipboard = "";

    private FieldKey Key(MenuTextField field, int key, bool shift = false, bool control = false) =>
        field.Key(key, shift, control, () => clipboard, text => clipboard = text);

    private static void Type(MenuTextField field, string text)
    {
        foreach (var c in text)
        {
            _ = field.Char(c);
        }
    }

    /// <summary>A field holding <paramref name="shown"/> with the keys, the caret at its end.</summary>
    private static MenuTextField Focused(string shown = "", int longest = int.MaxValue)
    {
        var field = new MenuTextField(shown, longest);
        field.Focus(selectAll: false);
        return field;
    }

    /// <summary>
    /// §338: a password's field draws a bullet for each letter, never its words; copy and cut give the
    /// clipboard nothing and take nothing out, paste puts in; wiped, it holds nothing, nor what it said.
    /// </summary>
    [Fact]
    public void AMaskedFieldDrawsBulletsAndGivesTheClipboardNothing()
    {
        var field = new MenuTextField("", masked: true);
        field.Focus(selectAll: false);
        Type(field, "hunter2");
        Assert.Equal(("hunter2", new string(MenuTextField.Bullet, 7)), (field.Text, field.Drawn));
        clipboard = "before";
        _ = Key(field, 'A', control: true);
        Assert.Equal(FieldKey.None, Key(field, 'C', control: true));
        Assert.Equal(FieldKey.None, Key(field, VK.VK_INSERT, control: true));
        Assert.Equal(FieldKey.None, Key(field, 'X', control: true));
        Assert.Equal(FieldKey.None, Key(field, VK.VK_DELETE, shift: true));
        Assert.Equal(("before", "hunter2"), (clipboard, field.Text));

        clipboard = "!";
        _ = Key(field, VK.VK_END);
        Assert.Equal(FieldKey.Edited, Key(field, 'V', control: true));
        Assert.Equal(("hunter2!", new string(MenuTextField.Bullet, 8)), (field.Text, field.Drawn));
        field.Kept();
        field.Wipe();
        Assert.Equal(("", "", false), (field.Text, field.Shown, field.Dirty));

        // An unmasked field draws its words.
        var plain = Focused("Home");
        Assert.Equal("Home", plain.Drawn);
    }

    [Fact]
    public void TypingAndTheCaretKeysEditWhatItHolds()
    {
        var field = Focused();
        Type(field, "Tokyo office");
        Assert.Equal("Tokyo office", field.Text);
        Assert.True(field.Dirty);

        Assert.Equal(FieldKey.Moved, Key(field, VK.VK_HOME));
        Assert.Equal(0, field.Editing.Caret);
        Assert.Equal(FieldKey.Moved, Key(field, VK.VK_RIGHT, control: true));
        Assert.Equal(6, field.Editing.Caret);
        Assert.Equal(FieldKey.Moved, Key(field, VK.VK_END, shift: true));
        Assert.Equal("office", field.Editing.SelectedText);
        Assert.Equal(FieldKey.Moved, Key(field, VK.VK_LEFT));
        Assert.False(field.Editing.HasSelection);

        Assert.Equal(FieldKey.Edited, Key(field, VK.VK_BACK, control: true));
        Assert.Equal("office", field.Text);
        Assert.Equal(FieldKey.Edited, Key(field, VK.VK_DELETE));
        Assert.Equal("ffice", field.Text);
        // Nothing before the caret: Backspace changes nothing.
        Assert.Equal(FieldKey.None, Key(field, VK.VK_BACK));
    }

    [Fact]
    public void CopyCutAndPasteGoThroughTheClipboard()
    {
        var field = Focused("Zürich");
        Assert.Equal(FieldKey.Moved, Key(field, 'A', control: true));
        Assert.Equal(FieldKey.None, Key(field, 'C', control: true));
        Assert.Equal("Zürich", clipboard);
        Assert.Equal(FieldKey.Edited, Key(field, 'X', control: true));
        Assert.Equal("", field.Text);
        clipboard = "Bern\r\nsecond line";
        Assert.Equal(FieldKey.Edited, Key(field, 'V', control: true));
        // A field is one line: the clipboard's first.
        Assert.Equal("Bern", field.Text);
        Assert.Equal(FieldKey.Edited, Key(field, VK.VK_INSERT, shift: true));
        Assert.Equal("BernBern", field.Text);
    }

    [Fact]
    public void EnterKeepsAndEscapePutsBackBeforeItLetsGo()
    {
        var field = Focused("Home");
        Assert.Equal(FieldKey.Commit, Key(field, VK.VK_RETURN));
        Type(field, " office");
        // Escape once: what the setting says is back, and the field keeps the keys (the menu stays).
        Assert.Equal(FieldKey.Revert, Key(field, VK.VK_ESCAPE));
        Assert.Equal("Home", field.Text);
        Assert.False(field.Dirty);
        Assert.True(field.Focused);
        // Escape again, nothing typed: the keys go back to the menu.
        Assert.Equal(FieldKey.Blur, Key(field, VK.VK_ESCAPE));
    }

    [Fact]
    public void UpDownAndTabLeaveTheField()
    {
        var field = Focused("x");
        Assert.Equal(FieldKey.Up, Key(field, VK.VK_UP));
        Assert.Equal(FieldKey.Down, Key(field, VK.VK_DOWN));
        Assert.Equal(FieldKey.Down, Key(field, VK.VK_TAB));
        Assert.Equal(FieldKey.Up, Key(field, VK.VK_TAB, shift: true));
    }

    /// <summary>The keys a menu takes for itself — its shortcuts, letters, F-keys — are the field's while it has the keys, and change nothing.</summary>
    [Fact]
    public void EveryOtherKeyIsTheFieldsAndDoesNothing()
    {
        var field = Focused("abc");
        foreach (var key in new[] { 'D', 'R', VK.VK_F2, VK.VK_F10, VK.VK_APPS, VK.VK_PRIOR, VK.VK_NEXT, VK.VK_SPACE })
        {
            Assert.Equal(FieldKey.None, Key(field, key));
        }
        Assert.Equal(FieldKey.None, Key(field, 'D', control: true));
        Assert.Equal("abc", field.Text);
        // Letters and a space come as characters; control characters (Ctrl+A's 0x01, Enter's \r) are keys.
        Assert.Equal(FieldKey.Edited, field.Char(' '));
        Assert.Equal(FieldKey.None, field.Char('\u0001'));
        Assert.Equal(FieldKey.None, field.Char('\r'));
        Assert.Equal(FieldKey.None, field.Char('\u001b'));
        Assert.Equal("abc ", field.Text);
    }

    [Fact]
    public void ASurrogatePairInTwoMessagesIsOneCharacter()
    {
        var field = Focused();
        var smile = "😀";
        Assert.Equal(FieldKey.None, field.Char(smile[0]));
        Assert.Equal(FieldKey.Edited, field.Char(smile[1]));
        Assert.Equal(smile, field.Text);
        // A low half alone is nothing.
        Assert.Equal(FieldKey.None, field.Char(smile[1]));
        Assert.Equal(smile, field.Text);
    }

    [Fact]
    public void ItHoldsNoMoreThanThePagesBoxAllows()
    {
        var field = Focused("", longest: 12);
        Type(field, "A very long name indeed");
        Assert.Equal("A very long ", field.Text);
    }

    /// <summary>A search's words are selected as it takes the keys, so typing replaces them; a text field's caret goes to the end.</summary>
    [Fact]
    public void TakingTheKeysSelectsWhatItSaysWhereAsked()
    {
        var search = new MenuTextField("(UTC+01:00) Bern");
        search.Focus(selectAll: true);
        Assert.Equal("(UTC+01:00) Bern", search.Editing.SelectedText);
        Assert.Equal(FieldKey.Edited, search.Char('t'));
        Assert.Equal("t", search.Text);
        search.Blur();
        Assert.False(search.Focused);
        Assert.False(search.Editing.HasSelection);
    }

    /// <summary>The setting changing under the field (the menu shown again) shows in it, but never over what somebody is typing.</summary>
    [Fact]
    public void TheSettingChangingUnderItNeverTakesWhatIsTyped()
    {
        var field = new MenuTextField("Home");
        field.Show("Office");
        Assert.Equal(("Office", "Office"), (field.Text, field.Shown));

        field.Focus(selectAll: false);
        Type(field, " 2");
        field.Show("Elsewhere");
        Assert.Equal("Office 2", field.Text);
        // Escape puts back what the setting says now.
        Assert.Equal(FieldKey.Revert, field.Key(VK.VK_ESCAPE, false, false, () => null, _ => { }));
        Assert.Equal("Elsewhere", field.Text);
    }

    [Fact]
    public void TheClearButtonEmptiesIt()
    {
        var field = Focused("Label");
        Assert.True(field.Clears);
        Assert.True(field.Clear());
        Assert.Equal("", field.Text);
        Assert.False(field.Clears);
        Assert.False(field.Clear());
        // At rest it shows no clear button.
        var resting = new MenuTextField("Label");
        Assert.False(resting.Clears);
    }
}
