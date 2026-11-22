// §525: the settings panel's popups toggle as WinUI's ComboBox does, and as the edit bar's menu buttons did (§343). The
// person, 2026-11-07: "the toggleing with dropdowns should properly work as well."
//
//  - A click on the control whose popup is open closes it and does nothing else: never closed and opened again.
//  - A click on another popup's control closes the first and opens its own, in the one click.
//  - A click elsewhere on the panel closes it and does what it does: a toggle toggles, a side item shows its page,
//    Settings ⌃ folds the panel.
//  - F4, Alt+Down, Alt+Up, Space and Enter on the control open it; F4, Alt+Up, Alt+Down and Escape in it close it, the
//    keys back on the control.
//
// Asked of the real dock, Karpo.exe --stage on a desktop of its own, driven as the mouse drives it on the person's
// screen (PanelPopupPlaceTests.Probe.Press): the hook hears the press, the open panel takes the activation from the
// menu that had it, then the press and its release come.

using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using Xunit.Abstractions;
using static Karpo.Tests.Present.PanelPopupPlaceTests;

namespace Karpo.Tests.Present;

public sealed class PanelPopupToggleTests(ITestOutputHelper output)
{
    /// <summary>A dropdown clicked open, clicked again: it closes, and stays closed — never shown again after.</summary>
    [UiFact]
    public void ADropdownClickedAgainClosesIt() => Drive(output, Edge.Bottom, null, probe =>
    {
        probe.Open();
        var language = probe.Pop("Karpo/");
        Toggles(probe, "language", language);
        // And once more: a toggle twice is the same as once.
        Toggles(probe, "language-again", language);
        probe.Finish();
    });

    /// <summary>One dropdown open, another clicked: the other's opens under its own box in the one click, the first's gone.</summary>
    [UiFact]
    public void AnotherDropdownClickedOpensItsOwnInOneClick() => Drive(output, Edge.Bottom, null, probe =>
    {
        probe.Open();
        // The first page with two dropdowns in sight as it opens.
        var pops = probe.InSight(PanelHitKind.Pop);
        foreach (var page in new[] { PanelPage.Layouts, PanelPage.Taskbar, PanelPage.Appearance })
        {
            if (pops.Count >= 2)
            {
                break;
            }
            probe.Nav(page);
            pops = probe.InSight(PanelHitKind.Pop);
        }
        Assert.True(pops.Count >= 2, $"no page with two dropdowns in sight: {string.Join("; ", pops)}");
        // One open, and another whose box its popup leaves in sight (a long list under its box covers the next ones):
        // the lowest first, its list over it or under it, then the others top down.
        Spot? first = null, second = null;
        foreach (var candidate in Enumerable.Reverse(pops))
        {
            Assert.True(Opens(probe, candidate), $"{candidate} opened nothing");
            var shown = probe.Menu()!.Rect;
            second = pops.FirstOrDefault(spot => spot != candidate && (spot.Rect.bottom <= shown.top || spot.Rect.top >= shown.bottom));
            if (second is not null)
            {
                first = candidate;
                break;
            }
            probe.Escape();
        }
        Assert.True(first is not null && second is not null, $"every dropdown's popup covers the others: {string.Join("; ", pops)}");
        var states = probe.Watch(() => probe.Press(second!.Center));
        var menus = probe.Menus();
        var menu = probe.Menu();
        if (menus.Count != 1 || menu is null)
        {
            probe.Wrong($"from {first} to {second}: {menus.Count} menus in sight at the end ({states})");
        }
        else if (Math.Abs(menu.Rect.left - second!.Rect.left) > 1 || Math.Min(Math.Abs(menu.Rect.top - second.Rect.bottom), Math.Abs(second.Rect.top - menu.Rect.bottom)) > 8)
        {
            probe.Wrong($"from {first} to {second}: the menu at the end {menu} is not the second's, under its box");
        }
        probe.Escape();
        probe.Finish();
    });

    /// <summary>
    /// A dropdown open, a click elsewhere on the panel: the toggle beside it toggles (the config changes), a side item shows
    /// its page, Settings ⌃ folds the panel — and the popup is gone each time.
    /// </summary>
    [UiFact]
    public void AClickElsewhereClosesItAndDoesWhatItDoes() => Drive(output, Edge.Bottom, null, probe =>
    {
        probe.Open();

        // A toggle: it toggles, the popup closed. §549: the Taskbar page's, a dropdown of its open (the Karpo page's one
        // toggle, Start with Windows, would write Windows' Run key).
        probe.Nav(PanelPage.Taskbar);
        // The toggle a dropdown under it: its list opens down, away from the toggle.
        var toggle = probe.InSight(PanelHitKind.Toggle, "Taskbar/").FirstOrDefault();
        Assert.True(toggle is not null, "no toggle of the Taskbar page's is in sight");
        var dropdown = probe.InSight(PanelHitKind.Pop, "Taskbar/").FirstOrDefault(spot => spot.Rect.top > toggle!.Rect.bottom);
        Assert.True(dropdown is not null, "no dropdown of the Taskbar page's under its toggle is in sight");
        Assert.True(Opens(probe, dropdown!), "the Taskbar page's dropdown opened nothing");
        var before = probe.ConfigText();
        Closes(probe, "toggle", () => probe.Press(toggle!.Center));
        var until = Environment.TickCount64 + 5000;
        while (probe.ConfigText() == before && Environment.TickCount64 < until)
        {
            probe.Pump(100);
        }
        if (probe.ConfigText() == before)
        {
            probe.Wrong($"toggle: {toggle!.Key} clicked with a dropdown's popup open did not toggle");
        }
        probe.Nav(PanelPage.Karpo);
        var language = probe.Pop("Karpo/");

        // A side item: its page, the popup closed.
        Assert.True(Opens(probe, language), "Language opened nothing");
        Closes(probe, "side-item", () => probe.Press(probe.Spot(PanelHitKind.Nav, nameof(PanelPage.Appearance)).Center));
        if (probe.PageShown() != nameof(PanelPage.Appearance))
        {
            probe.Wrong($"side-item: the panel shows {probe.PageShown()}, not Appearance");
        }
        probe.Click(probe.Spot(PanelHitKind.Nav, nameof(PanelPage.Karpo)).Center);

        // §575: its entry again: the flyout closes, the popup with it.
        Assert.True(Opens(probe, language), "Language opened nothing");
        Closes(probe, "entry", () => probe.Press(probe.Spot(PanelHitKind.Nav, nameof(PanelPage.Karpo)).Center));
        probe.Pump(800);
        if (probe.PageShown() is { } still)
        {
            probe.Wrong($"entry: the flyout still stands open on {still}");
        }
        probe.Finish();
    });

    /// <summary>§575: the edit bar's Add: its list's flyout, clicked again closed; from a dropdown to it in one click, the dropdown closing.</summary>
    [UiFact]
    public void TheHeadersAddTogglesItsListToo() => Drive(output, Edge.Bottom, null, probe =>
    {
        probe.Open();
        var add = probe.Spot(PanelHitKind.Add);
        // §575: Add's list a flyout of the edit bar's, in the Karpo flyout's place; Add again closes it.
        probe.Press(add.Center);
        probe.Pump(800);
        if (probe.PageShown() != nameof(PanelPage.Add))
        {
            probe.Wrong($"add: Add opened {probe.PageShown() ?? "nothing"}");
        }
        probe.Press(add.Center);
        probe.Pump(800);
        if (probe.PageShown() is { } still)
        {
            probe.Wrong($"add: Add pressed again left {still} open");
        }
        probe.Nav(PanelPage.Karpo);
        probe.Pump(800);
        var language = probe.Pop("Karpo/");
        Assert.True(Opens(probe, language), "Language opened nothing");
        probe.Press(add.Center);
        probe.Pump(800);
        // The add list alone at the end, not Language's popup under its box.
        if (probe.Menus().Count != 0 || probe.PageShown() != nameof(PanelPage.Add))
        {
            probe.Wrong($"from Language to Add: not the add list alone at the end ({probe.Menus().Count} menus, {probe.PageShown()})");
        }
        probe.Close();
        probe.Finish();
    });

    /// <summary>
    /// The keys, as a ComboBox's: a dropdown clicked has them once its popup closes (Escape); F4, Alt+Down, Space and Enter
    /// on it open its popup; F4, Alt+Up and Escape in the popup close it again.
    /// </summary>
    [UiFact]
    public void ItsKeysOpenAndCloseIt() => Drive(output, Edge.Bottom, null, probe =>
    {
        probe.Open();
        var language = probe.Pop("Karpo/");
        Assert.True(Opens(probe, language), "Language opened nothing");
        void Step(string name, HWND on, int key, bool alt, bool open)
        {
            probe.Key(on, key, alt);
            if (!probe.WaitMenu(open))
            {
                probe.Wrong($"{name}: the popup is {(open ? "not open" : "open still")}");
            }
        }
        HWND Menu() => probe.Menus() is [var one, ..] ? one : HWND.NULL;
        Step("escape", Menu(), VK.VK_ESCAPE, alt: false, open: false);
        Step("f4-opens", probe.Deck(), VK.VK_F4, alt: false, open: true);
        Step("f4-closes", Menu(), VK.VK_F4, alt: false, open: false);
        Step("alt-down-opens", probe.Deck(), VK.VK_DOWN, alt: true, open: true);
        Step("alt-up-closes", Menu(), VK.VK_UP, alt: true, open: false);
        Step("space-opens", probe.Deck(), VK.VK_SPACE, alt: false, open: true);
        Step("alt-down-closes", Menu(), VK.VK_DOWN, alt: true, open: false);
        Step("enter-opens", probe.Deck(), VK.VK_RETURN, alt: false, open: true);
        Step("escape-again", Menu(), VK.VK_ESCAPE, alt: false, open: false);
        probe.Finish();
    });

    /// <summary><paramref name="control"/> clicked: a menu in sight.</summary>
    private static bool Opens(Probe probe, Spot control)
    {
        probe.Press(control.Center);
        if (probe.WaitMenu(open: true))
        {
            return true;
        }
        // A dock still settling may let a click go (InputReachesTheBarsTests): asked once more.
        probe.Press(control.Center);
        return probe.WaitMenu(open: true);
    }

    /// <summary><paramref name="control"/> clicked open, then clicked again, watched: it closes and stays closed.</summary>
    private static void Toggles(Probe probe, string name, Spot control)
    {
        if (!Opens(probe, control))
        {
            probe.Wrong($"{name}: {control} opened nothing");
            return;
        }
        Closes(probe, name, () => probe.Press(control.Center));
    }

    /// <summary><paramref name="act"/> with a menu open, watched: the menu closes and never shows again (o open, c closed).</summary>
    private static void Closes(Probe probe, string name, Action act)
    {
        var states = probe.Watch(act);
        var closedAt = states.IndexOf('c');
        if (closedAt < 0)
        {
            probe.Wrong($"{name}: clicked, the popup is open still — closed and opened again in the one click: {states}");
        }
        else if (states.IndexOf('o', closedAt) >= 0)
        {
            probe.Wrong($"{name}: clicked, the popup closed and opened again: {states}");
        }
        probe.Pump(300);
        if (probe.Menu() is { } left)
        {
            probe.Wrong($"{name}: the dock has a menu open still: {left}");
        }
    }
}
