// edit: one keymap for edit mode, the bar's and an open folder's (Karpo.Dock\EditKeys.cs; the person,
// 2026-10-12: "i want the overall look and feel to be the same ... keyboard shortcuts"). What each key
// means, once however long held but for the arrows; Escape's order; a click's Ctrl and Shift; and
// that the bar and the folder both read their keys through it and keep no keymap of their own.

using Karpo.Dock;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Edit;

public sealed class EditKeysTests
{
    private const int KeyA = 'A';
    private const int KeyC = 'C';
    private const int KeyV = 'V';

    public static TheoryData<int, bool, bool, EditKeyAct> Map => new()
    {
        { VK.VK_ESCAPE, false, false, EditKeyAct.Escape },
        { VK.VK_TAB, false, false, EditKeyAct.Next },
        { VK.VK_TAB, false, true, EditKeyAct.Previous },
        { VK.VK_LEFT, false, false, EditKeyAct.Focus },
        { VK.VK_DOWN, false, true, EditKeyAct.Resize },
        { VK.VK_RIGHT, true, false, EditKeyAct.Move },
        { VK.VK_UP, true, false, EditKeyAct.Move },
        { VK.VK_HOME, false, false, EditKeyAct.First },
        { VK.VK_END, false, false, EditKeyAct.Last },
        { VK.VK_SPACE, false, false, EditKeyAct.Click },
        { VK.VK_RETURN, false, false, EditKeyAct.Open },
        { VK.VK_RETURN, false, true, EditKeyAct.Open },
        { VK.VK_APPS, false, false, EditKeyAct.Menu },
        { VK.VK_F10, false, true, EditKeyAct.Menu },
        { VK.VK_F10, false, false, EditKeyAct.None },
        { VK.VK_DELETE, false, false, EditKeyAct.Remove },
        { KeyC, true, false, EditKeyAct.Copy },
        { KeyV, true, false, EditKeyAct.Paste },
        { KeyA, true, false, EditKeyAct.SelectAll },
        { KeyA, false, false, EditKeyAct.None },
        { KeyC, false, false, EditKeyAct.None },
        { VK.VK_BACK, false, false, EditKeyAct.None },
        { VK.VK_F6, false, false, EditKeyAct.None },
    };

    [Theory]
    [MemberData(nameof(Map))]
    public void EachKeyDoesOneThing(int key, bool control, bool shift, EditKeyAct act) =>
        Assert.Equal(act, EditKeys.Of(key, control, shift, repeat: false).Act);

    /// <summary>An arrow carries its way, for the surface to read along its own lines.</summary>
    [Fact]
    public void AnArrowCarriesItsWay()
    {
        Assert.Equal(new EditKeyStroke(EditKeyAct.Focus, VK.VK_LEFT), EditKeys.Of(VK.VK_LEFT, control: false, shift: false, repeat: false));
        Assert.Equal(new EditKeyStroke(EditKeyAct.Move, VK.VK_DOWN), EditKeys.Of(VK.VK_DOWN, control: true, shift: false, repeat: false));
    }

    /// <summary>A key held down acts once — but for the arrows, Tab, Home and End, which walk, and Escape.</summary>
    [Fact]
    public void AKeyHeldDownActsOnceButTheArrows()
    {
        foreach (var (key, control) in new[] { (VK.VK_DELETE, false), (KeyC, true), (KeyV, true), (KeyA, true), (VK.VK_RETURN, false), (VK.VK_SPACE, false), (VK.VK_APPS, false) })
        {
            Assert.NotEqual(EditKeyAct.None, EditKeys.Of(key, control, shift: false, repeat: false).Act);
            Assert.Equal(EditKeyAct.None, EditKeys.Of(key, control, shift: false, repeat: true).Act);
        }
        foreach (var key in new[] { VK.VK_LEFT, VK.VK_RIGHT, VK.VK_TAB, VK.VK_HOME, VK.VK_END, VK.VK_ESCAPE })
        {
            Assert.Equal(EditKeys.Of(key, control: false, shift: false, repeat: false), EditKeys.Of(key, control: false, shift: false, repeat: true));
        }
    }

    /// <summary>Escape puts what is carried back first, then takes the marching ants off; only with neither is it one level out.</summary>
    [Fact]
    public void EscapeCancelsACarryBeforeItLeaves()
    {
        Assert.Equal(EditEscape.Cancel, EditKeys.Escape(carrying: true));
        Assert.Equal(EditEscape.Leave, EditKeys.Escape(carrying: false));
        Assert.Equal(EditEscape.Cancel, EditKeys.Escape(carrying: true, copied: true));
        Assert.Equal(EditEscape.Uncopy, EditKeys.Escape(carrying: false, copied: true));
    }

    /// <summary>
    /// On the bar with a folder open, Escape closes the folder first — the edit bar comes back — and
    /// only the next one leaves edit mode (the person, 2026-10-16); a carry and the ants still go first.
    /// </summary>
    [Fact]
    public void EscapeClosesAnOpenFolderBeforeItLeaves()
    {
        Assert.Equal(EditEscape.CloseFolder, EditKeys.Escape(carrying: false, folderOpen: true));
        Assert.Equal(EditEscape.Cancel, EditKeys.Escape(carrying: true, folderOpen: true));
        Assert.Equal(EditEscape.Uncopy, EditKeys.Escape(carrying: false, copied: true, folderOpen: true));
        Assert.Equal(EditEscape.Leave, EditKeys.Escape(carrying: false, folderOpen: false));
    }

    /// <summary>Ctrl+click toggles, Shift+click selects the run, Ctrl first where both are held; plain, a click.</summary>
    [Fact]
    public void AClicksKeysPickTheSame()
    {
        Assert.Equal(EditClickPick.Only, EditKeys.Click(control: false, shift: false));
        Assert.Equal(EditClickPick.Toggle, EditKeys.Click(control: true, shift: false));
        Assert.Equal(EditClickPick.Range, EditKeys.Click(control: false, shift: true));
        Assert.Equal(EditClickPick.Toggle, EditKeys.Click(control: true, shift: true));
    }

    /// <summary>The click rule's keys (EditClicks.OnKey) are the keymap's.</summary>
    [Fact]
    public void TheClickRulesKeysAreTheKeymaps()
    {
        Assert.Equal(EditKeyPress.Click, EditClicks.OnKey(VK.VK_SPACE, shift: false, repeat: false));
        Assert.Equal(EditKeyPress.Open, EditClicks.OnKey(VK.VK_RETURN, shift: false, repeat: false));
        Assert.Equal(EditKeyPress.Menu, EditClicks.OnKey(VK.VK_APPS, shift: false, repeat: false));
        Assert.Equal(EditKeyPress.Nothing, EditClicks.OnKey(VK.VK_DELETE, shift: false, repeat: false));
    }

    // --- Both read it -----------------------------------------------------------------------------

    /// <summary>
    /// The bar (Dock.EditKey), an open folder (FolderFlyout.EditKey) and Escape heard by edit mode's
    /// hook (Dock.EscapeHeard) read their keys through the one keymap, and a click's Ctrl and Shift
    /// through the one rule (Dock.EditClickWith) — none matches an edit key of its own, so the two
    /// can't drift apart again.
    /// </summary>
    [Fact]
    public void TheBarAndTheFolderBothReadTheOneKeymap()
    {
        if (EditSource.Root() is not { } repo)
        {
            return;
        }
        var bar = EditSource.Body(repo, "Dock.cs", "private bool EditKey(");
        var folder = EditSource.Body(repo, "FolderFlyout.cs", "private FolderFlyoutEvent? EditKey(");
        var hook = EditSource.Body(repo, "Dock.EditFollow.cs", "private void EscapeHeard(");
        Assert.Contains("EditKeys.Of(", bar);
        Assert.Contains("EditKeys.Of(", folder);
        // §520: both through the one step, which asks the panel too (Dock.EscapeStep).
        Assert.Contains("EscapeStep(", bar);
        Assert.Contains("EscapeStep(", hook);
        Assert.Contains("EditKeys.Escape(", EditSource.Body(repo, "Dock.EditFollow.cs", "private EditEscape EscapeStep("));
        Assert.Contains("EditKeys.Escape(", File.ReadAllText(Path.Combine(repo, "Karpo.Dock", "FolderFlyout.cs")));
        foreach (var body in new[] { bar, folder })
        {
            foreach (var own in new[] { "case VK.VK_DELETE", "case 'C'", "case 'V'", "case VK.VK_ESCAPE", "case VK.VK_RETURN", "case VK.VK_TAB" })
            {
                Assert.DoesNotContain(own, body, StringComparison.Ordinal);
            }
        }
        // A click's Ctrl and Shift: one rule, on the bar and in a folder.
        var dock = string.Join("\n", Directory.GetFiles(Path.Combine(repo, "Karpo.Dock"), "*.cs").Select(File.ReadAllText));
        Assert.Contains("at is not null && EditClickWith(key)", dock);
        Assert.Contains("EditClickWith(key, opened.Flyout.LineKeys)", dock);
    }
}

/// <summary>The dock's own source, for the tests that hold both edit modes to one rule.</summary>
internal static class EditSource
{
    /// <summary>The repository's root, found up from the tests' folder; null where the source isn't there (a test run from a copy).</summary>
    public static string? Root()
    {
        for (var at = new DirectoryInfo(AppContext.BaseDirectory); at is not null; at = at.Parent)
        {
            if (File.Exists(Path.Combine(at.FullName, "Karpo.slnx")))
            {
                return at.FullName;
            }
        }
        return null;
    }

    /// <summary>The body of the member of Karpo.Dock\<paramref name="file"/> that starts with <paramref name="start"/>, up to the next doc comment.</summary>
    public static string Body(string repo, string file, string start)
    {
        var code = File.ReadAllText(Path.Combine(repo, "Karpo.Dock", file));
        var at = code.IndexOf(start, StringComparison.Ordinal);
        Assert.True(at > 0, $"{start} is not in {file}");
        var end = code.IndexOf("\n    /// <summary>", at, StringComparison.Ordinal);
        return end < 0 ? code[at..] : code[at..end];
    }
}
