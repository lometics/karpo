// §622 (the person, 2026-11-09: "when hovering over an entry in the palette it should apply it / preview it with the whole
// taskbar and also in the preview itself. the hovering should also work in other selectors like the font selector or color
// selectors"): which choices are tried on (LookRows.TriedOn: every value of the Appearance, nothing else), what in a menu
// takes a choice tried on off (Dock.Settles), and, on a real menu (KARPO_UI_TESTS=1), the row the pointer or the keys rest
// on told a moment later (Flyout.TellsHover, FlyoutEventKind.Hover), none once they rest on none. The dock wearing it is the
// stage's (HoverTryOnStageTests).

using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using Karpo.Tests.Localization;
using TerraFX.Interop.Windows;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Menus;

[Collection(InLanguage.Collection)]
public sealed class HoverTryOnTests
{
    private const float Scale = 1.5f;

    private static readonly RECT Work = new() { right = (int)(1920 * Scale), bottom = (int)(1040 * Scale) };

    private static readonly POINT Anchor = new() { x = Work.right / 3, y = Work.bottom };

    /// <summary>
    /// §684 (the person, 2026-11-12: "this should be done for other settings too, this should be a general design rule"): a
    /// choice of how a thing is drawn, the bar's kind and how its buttons combine are tried on as the panel's pointer rests on
    /// them; its edge, how it shows, a language, Animations are not.
    /// </summary>
    [Fact]
    public void EveryChoiceOfALookIsTriedOnOnThePanel()
    {
        var config = MenuCorpus.Fresh();
        var layout = config.ActiveLayout(MenuCorpus.Display);
        var bar = new BarSpot(MenuCorpus.Display, "One");
        var taskbar = BarLooks.TaskbarRows(config, bar);
        Assert.True(TheDock.TriesOn(OptionRows.Find(taskbar, BarLooks.TypeRow)!));
        Assert.True(TheDock.TriesOn(OptionRows.Find(taskbar, BarLooks.CombineRow)!));
        Assert.False(TheDock.TriesOn(OptionRows.Find(taskbar, BarLooks.ShowRow)!));
        Assert.False(TheDock.TriesOn(BarLooks.Position(config, bar)));
        Assert.False(TheDock.TriesOn(KarpoMenu.AnimationRow(config)));
        Assert.True(TheDock.TriesOn(OptionRows.Find(LookRows.Rows(config, layout, windows: true), LookRows.PaletteRow)!));
        var styled = layout.Islands.SelectMany(island => OptionRows.Of(island)).FirstOrDefault(row => row.Slot == OptionSlot.Style && row.Kind == OptionRowKind.Choice);
        Assert.True(styled is null || TheDock.TriesOn(styled));
    }

    /// <summary>
    /// Every choice the Appearance page offers — the palette, each part's font and pattern, every colour's swatches and its
    /// "Default (…)" — is a choice set (a Set pick) tried on; its resets are actions, never tried on; a widget's own choices
    /// (a pin's) are not the Appearance's.
    /// </summary>
    [Fact]
    public void EveryChoiceOfTheAppearanceIsTriedOnAndNothingElse()
    {
        var config = MenuCorpus.Fresh();
        // One value changed, so a reset stands beside it.
        var graphite = LookPalettes.Of("graphite")!;
        Assert.True(LookPalettes.Wear(config, graphite));
        List<OptionPick> picks = [];
        _ = OptionMenu.Rows(LookRows.Rows(config, config.DefaultLayout, windows: true), picks);
        var sets = picks.Where(pick => pick.Kind == OptionPickKind.Set).ToList();
        Assert.Contains(sets, pick => pick.Key == LookRows.PaletteRow);
        foreach (var part in LookParts.All)
        {
            var key = LookRows.SectionKey(part);
            Assert.Contains(sets, pick => pick.Key == LookRows.ColourKey(key, LookParts.TonesOf(part)[0]));
            if (LookParts.HasText(part))
            {
                Assert.Contains(sets, pick => pick.Key == key + ".font");
                // §638: its weight beside it too.
                Assert.Contains(sets, pick => pick.Key == key + ".weight");
            }
            if (LookParts.HasPattern(part))
            {
                Assert.Contains(sets, pick => pick.Key == key + ".pattern");
            }
        }
        // §682: Animations stands on the page too, how Karpo moves: no look of the bar's to try on.
        Assert.All(sets.Where(pick => pick.Key != KarpoMenu.AnimationsRow), pick => Assert.True(LookRows.TriedOn(pick.Key), pick.Key));
        Assert.DoesNotContain(picks, pick => pick.Key.EndsWith(LookRows.ResetEnd, StringComparison.Ordinal) && pick.Kind == OptionPickKind.Set);

        List<OptionPick> clocks = [];
        _ = OptionMenu.Rows(OptionRows.Of(BarKind.Pin.New()), clocks);
        Assert.NotEmpty(clocks);
        Assert.DoesNotContain(clocks, pick => LookRows.TriedOn(pick.Key));
    }

    /// <summary>A pick, a close, a step, a key passed on take a choice tried on off first; the pointer's rest and a colour mixed do not.</summary>
    [Fact]
    public void WhatIsDoneInAMenuTakesAChoiceTriedOnOff()
    {
        Assert.True(TheDock.Settles(FlyoutEventKind.Invoke));
        Assert.True(TheDock.Settles(FlyoutEventKind.Adjust));
        Assert.True(TheDock.Settles(FlyoutEventKind.Dismissed));
        Assert.True(TheDock.Settles(FlyoutEventKind.PassedKey));
        Assert.False(TheDock.Settles(FlyoutEventKind.Nothing));
        Assert.False(TheDock.Settles(FlyoutEventKind.Hover));
        Assert.False(TheDock.Settles(FlyoutEventKind.Mix));
        Assert.False(TheDock.Settles(FlyoutEventKind.MixKept));
        Assert.False(TheDock.Settles(FlyoutEventKind.MixUndone));
    }

    /// <summary>
    /// On a real menu of Palette's choices, as the panel's dropdown shows them: telling, the row the pointer comes to is told
    /// once it rests (the timer's tick), none once it leaves the menu, the row the keys come to too; not telling, nothing.
    /// </summary>
    [UiFact]
    public void AMenuTellsTheRowThePointerOrTheKeysRestOn()
    {
        WindowShots.OnHiddenDesktop("hover-try-on", (gfx, instance) =>
        {
            var config = MenuCorpus.Fresh();
            List<OptionPick> picks = [];
            var palette = OptionMenu.Rows([OptionRows.Find(LookRows.Rows(config, config.DefaultLayout, windows: true), LookRows.PaletteRow)!], picks)
                .Single(item => item.Children is { Count: > 1 }).Children!;
            var choices = palette.Where(item => OptionMenu.PickOf(picks, item.Id) is { Kind: OptionPickKind.Set }).ToList();
            Assert.True(choices.Count > 3);

            using (var menu = Show(gfx, instance, palette, tells: true))
            {
                // The pointer on the third palette: told as it rests there.
                _ = menu.Message(WM.WM_MOUSEMOVE, 0, At(menu.RowMiddle(choices[2].Id)!.Value));
                Assert.Equal(choices[2].Id, menu.HotId);
                Assert.Equal((FlyoutEventKind.Hover, choices[2].Id), Told(menu));
                // Out of the menu: none.
                _ = menu.Message(WM.WM_MOUSELEAVE, 0, 0);
                Assert.Equal((FlyoutEventKind.Hover, 0u), Told(menu));
                // The keys: the row they come to.
                _ = menu.Message(WM.WM_KEYDOWN, (WPARAM)(nuint)VK.VK_DOWN, 0);
                Assert.NotNull(menu.HotId);
                Assert.Equal((FlyoutEventKind.Hover, menu.HotId!.Value), Told(menu));
                menu.Hide();
            }
            using (var menu = Show(gfx, instance, palette, tells: false))
            {
                _ = menu.Message(WM.WM_MOUSEMOVE, 0, At(menu.RowMiddle(choices[2].Id)!.Value));
                Assert.Equal(FlyoutEventKind.Nothing, Told(menu).Kind);
                menu.Hide();
            }
        });
    }

    /// <summary>The menu's hover timer's tick, as Windows sends it once the pointer or the keys have rested <see cref="Flyout.HoverMs"/>.</summary>
    private static (FlyoutEventKind Kind, uint Id) Told(Flyout menu)
    {
        var told = menu.Message(WM.WM_TIMER, 2, 0);
        return (told.Kind, told.Id);
    }

    private static Flyout Show(Gfx gfx, HINSTANCE instance, IReadOnlyList<FlyoutItem> items, bool tells)
    {
        var menu = new Flyout(gfx, instance, 0) { Configures = true, TellsHover = tells };
        menu.Show(gfx, AddListPictures.LookFor(true), Scale, items, FlyoutLayout.List, Anchor, Work, Edge.Bottom, null);
        WindowShots.Settle();
        return menu;
    }

    private static LPARAM At(POINT point) => (LPARAM)(nint)((point.y << 16) | (point.x & 0xFFFF));
}
