// §633 (the person, 2026-11-09: "i feel like the dropdowns for the settings dont completely match the style of the settings
// pages. there are also other dropdowns in the right click menus, those you should not touch. but make the style of the ones
// that open on the topbar settings pages more similar to the settings pages themselve"): a control's popup — hung from its
// control (Flyout.Show's control, as Dock.PanelPopup hangs it) — lays its rows as the settings pages do (PanelMetrics.Popup:
// a control's 32 tall, 4 apart, 4 in, the icon and word columns at 16 and 48), marks its choice as WinUI's ComboBox does (its
// plate lit, a 3 × 16 accent pill at its start, no tick), draws the pages' glyphs (no Explorer pictures) and wears the
// flyout's rim; the same rows as a right-click menu keep Explorer's measures (MenuMetrics), its tick at the far end, its
// pictures and its light rim in the dark. Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Tests.Folders;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Edit;

public sealed class SettingsPopupStyleTests
{
    private const float Scale = 1.5f;

    private static float Px(float dip) => MathF.Round(dip * Scale, MidpointRounding.AwayFromZero);

    private static readonly RECT Work = new() { right = 2880, bottom = 1620 };

    /// <summary>A dropdown's box, high on the display, its popup's room under it.</summary>
    private static readonly RECT Box = new() { left = 900, top = 240, right = 900 + (int)Px(PanelMetrics.ControlWide), bottom = 240 + (int)Px(PanelMetrics.Control) };

    /// <summary>Explorer has a picture of Copy (ExplorerIcons): a menu draws it, a settings popup its glyph.</summary>
    private static readonly string Copy = char.ConvertFromUtf32(0xE8C8);

    /// <summary>Three choices with icons, the second the one in force.</summary>
    private static List<FlyoutItem> Choices() =>
    [
        new(100, "Off", new FlyoutIcon.Glyph(Copy), Checked: false, Stays: true),
        new(101, "Normal", new FlyoutIcon.Glyph(Copy), Checked: true, Stays: true),
        new(102, "Faster", new FlyoutIcon.Glyph(Copy), Checked: false, Stays: true),
    ];

    private static void Show(Flyout menu, Gfx gfx, bool dark, RECT? control) =>
        menu.Show(gfx, TestLooks.LookFor(dark) with { Animations = false }, Scale, Choices(), FlyoutLayout.List, new POINT { x = Box.left, y = Box.bottom }, Work, Karpo.Core.Edge.Bottom, control: control);

    [UiFact]
    public void ASettingsPopupLaysItsRowsAsThePagesDo()
    {
        var copied = File.Exists(Karpo.Core.ExplorerIcons.PathOf("windows.copy", Karpo.Core.ExplorerIcons.Tone.Dark));
        WindowShots.OnHiddenDesktop("settings-popup-style", (gfx, instance) =>
        {
            foreach (var dark in new[] { true, false })
            {
                using var menu = new Flyout(gfx, instance, 0) { Configures = true };
                Show(menu, gfx, dark, Box);
                var rows = menu.Looks.ToDictionary(look => look.Id);
                var popup = PanelMetrics.Popup;
                // A control's 32 tall, 4 apart as cards are, 4 in from the popup's sides and its top.
                Assert.All(rows.Values, row => Assert.Equal((Px(PanelMetrics.Tight), Px(PanelMetrics.Control)), (row.Plate.X, row.Plate.H)));
                Assert.Equal(Px(PanelMetrics.Tight), rows[100].Plate.Y);
                Assert.Equal(Px(PanelMetrics.Control + PanelMetrics.Tight), rows[101].Plate.Y - rows[100].Plate.Y);
                Assert.Equal(Px(PanelMetrics.ControlWide) - (2 * Px(PanelMetrics.Tight)), rows[100].Plate.W);
                // The icon in the pages' icon column, the words in their word column.
                Assert.All(rows.Values, row => Assert.Equal((Px(PanelMetrics.IconAt), Px(PanelMetrics.NameAt)), (row.Icon.X, row.Words.X)));
                Assert.Equal((popup.Fill, popup.Gap * 2, popup.Inset), (PanelMetrics.Control, PanelMetrics.Tight, PanelMetrics.Tight));
                // The choice in force: the pill at its plate's start, upright in its middle — no tick at its end; the others none.
                var pill = rows[101].Check;
                Assert.Equal((rows[101].Plate.X, Px(PanelMetrics.PillWide), Px(PanelMetrics.PillTall)), (pill.X, pill.W, pill.H));
                Assert.Equal(rows[101].Plate.Y + MathF.Round((rows[101].Plate.H - pill.H) / 2), pill.Y);
                Assert.True(pill.X < rows[101].Icon.X);
                Assert.Equal(0, rows[100].Check.W);
                // The pages' glyphs, never Explorer's pictures; the flyout's rim, never Explorer's light one.
                Assert.All(rows.Values, row => Assert.False(row.Explorer));
                Assert.NotEqual(FlyoutHost.MenuRimDark, menu.Theme.Stroke);
                menu.Hide();

                // The same rows as a right-click menu: Explorer's, as before.
                using var context = new Flyout(gfx, instance, 0);
                Show(context, gfx, dark, control: null);
                var menuRows = context.Looks.ToDictionary(look => look.Id);
                Assert.All(menuRows.Values, row => Assert.Equal((Px(MenuMetrics.Inset), Px(MenuMetrics.Fill)), (row.Plate.X, row.Plate.H)));
                Assert.Equal(Px(MenuMetrics.Fill + (2 * MenuMetrics.Gap)), menuRows[101].Plate.Y - menuRows[100].Plate.Y);
                Assert.Equal(Px(MenuMetrics.Inset) + Px(MenuMetrics.PadLeft) + Px(MenuMetrics.Column), menuRows[100].Words.X);
                // Its tick at the far end.
                Assert.True(menuRows[101].Check.X > menuRows[101].Words.X + menuRows[101].Words.W);
                Assert.Equal(Px(MenuMetrics.Glyph), menuRows[101].Check.W);
                Assert.All(menuRows.Values, row => Assert.Equal(copied, row.Explorer));
                Assert.Equal(dark ? FlyoutHost.MenuRimDark : context.Theme.Stroke, context.Theme.Stroke);
                context.Hide();
            }
        });
    }

    /// <summary>The menus' measures are Explorer's, as they were (§178): nothing of a settings popup's reaches them.</summary>
    [Fact]
    public void AMenusMeasuresAreExplorersStill()
    {
        Assert.Equal(new RowMetrics(3, 28, 2, 5, 11, 11, 28, 9, 32, 12), RowMetrics.Menu);
        // A settings popup's are the pages' own.
        Assert.Equal(
            new RowMetrics(PanelMetrics.Tight / 2, PanelMetrics.Control, PanelMetrics.Tight / 2, PanelMetrics.Tight, PanelMetrics.IconAt - PanelMetrics.Tight, PanelMetrics.Label, PanelMetrics.NameAt - PanelMetrics.IconAt, PanelMetrics.Gap, PanelMetrics.Control, PanelMetrics.Body),
            PanelMetrics.Popup);
    }
}
