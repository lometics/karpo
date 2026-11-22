// A menu's check marks at the far end of their rows (Flyout.cs; the person, 2026-10-16: "move that tick
// symbol all the way to the right of the context menu"), in the slot a submenu's chevron has: a checked
// row's icon in the icons' column, the words where they always are, the shortcuts one column in from
// the check marks; at the far left in Arabic. Pictures to KARPO_PNG_DIR as menu-check-<menu>-<look>-<scale>.png.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using Karpo.Tests.Localization;
using TerraFX.Interop.Windows;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Menus;

[Collection(InLanguage.Collection)]
public sealed class MenuCheckTests
{
    private static float Px(float dip, float scale) => MathF.Round(dip * scale, MidpointRounding.AwayFromZero);

    /// <summary>Checked rows of every kind: with a shortcut, without an icon, with a submenu; and rows that are not.</summary>
    private static List<FlyoutItem> Checks() =>
    [
        new(1, "Open", new FlyoutIcon.Glyph(""), Accelerator: "Enter"),
        new(2, "Show seconds", new FlyoutIcon.Glyph(""), Checked: true, Accelerator: "Ctrl+S"),
        new(3, "Compact", Checked: false),
        new(4, "Sound", Checked: true),
        new(5, "Layouts", new FlyoutIcon.Glyph(""), Checked: true, Children: [new(51, "Default", Checked: true), new(52, "Work")]),
        new(0, "", Separator: true),
        new(6, "Properties", new FlyoutIcon.Glyph("")),
    ];

    private static Dictionary<uint, MenuRowLook> Shown(Flyout menu, Gfx gfx, float scale, List<FlyoutItem> rows, bool dark = true)
    {
        var work = new RECT { right = (int)(1920 * scale), bottom = (int)(1040 * scale) };
        menu.Show(gfx, AddListPictures.LookFor(dark), scale, rows, FlyoutLayout.List, new POINT { x = work.right / 2, y = 0 }, work, Edge.Top, null);
        WindowShots.Settle(30);
        return menu.Looks.ToDictionary(look => look.Id);
    }

    /// <summary>
    /// The check mark's middle 16 DIP in from the plate's end, where a chevron's is; one slot further in
    /// on a row with a submenu; nothing on a row not checked. The icons and the words keep their
    /// columns (a checked row without an icon leaves its column empty); the shortcuts stand one column,
    /// in from the check marks' slot, a glyph and 24 DIP.
    /// </summary>
    [UiFact]
    public void ACheckMarkStandsAtTheRowsFarEnd()
    {
        WindowShots.OnHiddenDesktop("menu-check", (gfx, instance) =>
        {
            foreach (var scale in new[] { 1f, 1.5f })
            {
                using var menu = new Flyout(gfx, instance, 0);
                var looks = Shown(menu, gfx, scale, Checks());
                var width = menu.PanelSize.X;
                var slot = width - Px(5, scale) - Px(16, scale);
                float Middle(Flyout.Area part) => part.X + (part.W / 2);

                Assert.Equal(slot, Middle(looks[2].Check), 1f);
                Assert.Equal(slot, Middle(looks[4].Check), 1f);
                Assert.Equal(0, looks[1].Check.W);
                Assert.Equal(0, looks[3].Check.W);
                Assert.Equal(0, looks[6].Check.W);
                Assert.Equal(slot - Px(16, scale) - Px(24, scale), Middle(looks[5].Check), 1f);

                // The icons' column and the words', as in a menu without check marks.
                Assert.Equal(Px(5, scale) + Px(11, scale), looks[2].Icon.X);
                Assert.Equal(0, looks[4].Icon.W);
                Assert.All(looks.Values, look => Assert.Equal(Px(5, scale) + Px(11, scale) + Px(28, scale), look.Words.X));

                // The shortcuts, checked or not, one column ending a glyph and 24 DIP in from the slot.
                Assert.Equal(looks[1].Keys.X, looks[2].Keys.X);
                var keysEnd = MathF.Max(looks[1].Keys.X + looks[1].Keys.W, looks[2].Keys.X + looks[2].Keys.W);
                Assert.Equal(width - Px(5, scale) - Px(11, scale) - Px(16, scale) - Px(24, scale), keysEnd, 1f);
                Assert.True(keysEnd < looks[2].Check.X);
                menu.Hide();
            }
        });
    }

    /// <summary>A menu with no checkable row keeps the whole width for its shortcuts, their end 16 DIP in.</summary>
    [UiFact]
    public void AMenuWithoutChecksKeepsItsShortcutsAtTheEnd()
    {
        WindowShots.OnHiddenDesktop("menu-check-none", (gfx, instance) =>
        {
            using var menu = new Flyout(gfx, instance, 0);
            var looks = Shown(menu, gfx, 1f, [new(1, "Open", new FlyoutIcon.Glyph(""), Accelerator: "Enter"), new(2, "Properties")]);
            Assert.Equal(menu.PanelSize.X - 16, looks[1].Keys.X + looks[1].Keys.W, 1f);
            Assert.All(looks.Values, look => Assert.Equal(0, look.Check.W));
        });
    }

    /// <summary>In Arabic the row turns: the check mark at the far left, 16 DIP in, the icon at the right.</summary>
    [UiFact]
    public void ACheckMarkStandsAtTheFarLeftInArabic()
    {
        WindowShots.OnHiddenDesktop("menu-check-rtl", (gfx, instance) =>
        {
            using var speaking = InLanguage.Use("ar");
            using var menu = new Flyout(gfx, instance, 0);
            var looks = Shown(menu, gfx, 1f, Checks());
            var width = menu.PanelSize.X;
            Assert.Equal(5 + 16, looks[2].Check.X + (looks[2].Check.W / 2), 1f);
            Assert.Equal(width - 5 - 11, looks[2].Icon.X + looks[2].Icon.W, 1f);
            Assert.True(looks[2].Keys.X > looks[2].Check.X + looks[2].Check.W);
        });
    }

    [UiFact]
    public void MenuCheckSheets()
    {
        var menus = new (string Name, List<FlyoutItem> Rows)[]
        {
            ("rows", Checks()),
            ("bar", TheDock.BarRows(BarMenus.Default(), [new(5600, "Work", new FlyoutIcon.Glyph(""), Checked: true), new(5601, "Play", new FlyoutIcon.Glyph(""), Checked: false)], windowsTerminal: true, ShortcutLooks.None).Items),
        };
        WindowShots.OnHiddenDesktop("menu-check-sheets", (gfx, instance) =>
        {
            foreach (var language in new[] { "en", "ar" })
            {
                using var speaking = InLanguage.Use(language);
                foreach (var dark in new[] { true, false })
                {
                    foreach (var scale in new[] { 1f, 1.5f })
                    {
                        foreach (var (name, rows) in menus)
                        {
                            using var menu = new Flyout(gfx, instance, 0);
                            _ = Shown(menu, gfx, scale, rows, dark);
                            var file = $"menu-check-{name}{(language == "en" ? "" : "-" + language)}-{(dark ? "dark" : "light")}-{(int)(scale * 100)}.png";
                            Assert.True(WindowShots.Save(menu.Hwnd, file), $"{file} came back blank");
                            menu.Hide();
                        }
                    }
                }
            }
        });
    }

    /// <summary>
    /// A toggle, a radio button or a stepper stands at its row's very end in a menu whose other rows
    /// keep the check marks' slot, as in a menu with no ticks (§226: it stood a tick's room in).
    /// </summary>
    [UiFact]
    public void AControlStandsAtTheRowsEndBesideTicks()
    {
        WindowShots.OnHiddenDesktop("menu-check-control", (gfx, instance) =>
        {
            const float scale = 1.5f;
            var stepper = new FlyoutControl.Stepper("3", 81, 82, true, true, "Less", "More");
            float? MoreAt(bool ticks)
            {
                using var menu = new Flyout(gfx, instance, 0);
                _ = Shown(menu, gfx, scale, [new(1, "Compact", Checked: ticks ? true : null), new(81, "Years", Control: stepper), new(9, "Seconds", Control: new FlyoutControl.Toggle(true))]);
                return menu.StepperButton(81, more: true)?.x;
            }
            Assert.NotNull(MoreAt(ticks: false));
            Assert.Equal(MoreAt(ticks: false), MoreAt(ticks: true));
            Assert.True(Flyout.ClearsCheckSlot(new FlyoutItem(9, "Seconds", Control: new FlyoutControl.Toggle(true))));
            Assert.False(Flyout.ClearsCheckSlot(new FlyoutItem(9, "Seconds", Checked: true, Control: new FlyoutControl.Toggle(true))));
            Assert.False(Flyout.ClearsCheckSlot(new FlyoutItem(9, "Seconds")));
        });
    }
}
