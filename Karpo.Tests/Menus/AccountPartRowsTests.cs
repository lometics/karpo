// accounts: an account's parts are the menu's normal rows (2026-10-31, the person: "when clicking into an account
// to select which items to show for the account right now displays the items large, they should be shown in the
// normal item size."; docs\widgets.md §453). Each calendar's, book's, inbox's and To Do list's switch said its
// count on a second line under its name (OptionRow.Detail), which made every one a two-line row; now the count is
// what the row says at its end (OptionRow.Summary), before its switch, and the row is as tall as every other
// switch. Held here: the rows' form, and on a real menu their height, the count between the name and the switch.
// Picture (KARPO_UI_TESTS=1, KARPO_PNG_DIR), dark, 150 %: account-parts-dark-150.png.

using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Menus;

public sealed class AccountPartRowsTests
{
    private const float Scale = 1.5f;

    private static readonly OptionAccount Ann = new("a", "ann@outlook.com", OptionAccountKind.Microsoft)
    {
        Groups =
        [
            new("Calendars",
            [
                new OptionAccountPart("calendar:work", "Work", OptionGlyphs.Month, true) { Detail = "56 events", Automatic = "#038387" },
                new OptionAccountPart("calendar:home", "Home", OptionGlyphs.Month, false) { Detail = "Not synced", Automatic = "#3A96DD" },
            ]),
            new("Mail", [new OptionAccountPart("inbox", "Inbox", OptionGlyphs.Mail, true) { Detail = "1234 messages", Automatic = "#8764B8" }]),
        ],
    };

    /// <summary>The account's level as the menu shows it, a plain switch ("Seconds") under it to measure by.</summary>
    private static List<FlyoutItem> Level()
    {
        var forms = new AccountForms((_, _) => Task.FromResult(new AccountTry("imap.example.com")), work => work(), () => { });
        var accounts = new OptionAccounts([Ann], _ => { }, _ => { }, _ => { }, forms);
        var parts = OptionAccountRows.Page(accounts).Single(row => row.Kind == OptionRowKind.Things).Owned!.Things.Single().Rows;
        return OptionMenu.Rows([.. parts, new OptionRow("plain", OptionRowKind.Toggle, "Seconds") { On = true }], []);
    }

    // A switch: a plain one, or one with a colour's swatch before it (an account's calendar, §452).
    private static bool Switched(FlyoutItem item) => item.Control is FlyoutControl.Toggle or FlyoutControl.Tools { Switch: not null };

    /// <summary>A part's switch is one line, as a plain switch: nothing under its name, its count at its end.</summary>
    [Fact]
    public void APartsSwitchIsOneLineItsCountAtItsEnd()
    {
        var level = Level();
        var switches = level.Where(Switched).ToList();
        Assert.Equal(["Work", "Home", "Inbox", "Seconds"], switches.Select(item => item.Text));
        Assert.All(switches, item => Assert.Null(item.Detail));
        Assert.Equal(["56 events", "Not synced", "1234 messages", null], switches.Select(item => item.Value?.Text));
    }

    /// <summary>On a real menu each part's row is as tall as a plain switch's, its count after its name and before its switch.</summary>
    [UiFact]
    public void APartsRowIsAsTallAsAPlainSwitch()
    {
        WindowShots.OnHiddenDesktop("account-parts", (gfx, instance) =>
        {
            var items = Level();
            var work = new RECT { right = (int)(1920 * Scale), bottom = (int)(1040 * Scale) };
            using var menu = new Flyout(gfx, instance, 0) { Configures = true };
            menu.Show(gfx, AddListPictures.LookFor(dark: true), Scale, items, FlyoutLayout.List, new POINT { x = work.right / 3, y = work.bottom }, work, Edge.Bottom, null);
            WindowShots.Settle();
            var looks = menu.Looks.ToDictionary(look => look.Id);
            var plain = looks[items.Single(item => item.Text == "Seconds").Id];
            var toggle = MathF.Round(Flyout.ToggleWidthDip * Scale);
            foreach (var part in items.Where(item => Switched(item) && item.Text != "Seconds"))
            {
                var look = looks[part.Id];
                Assert.Equal(plain.Plate.H, look.Plate.H);
                Assert.True(look.Value.W > 0, $"{part.Text} says no count");
                Assert.True(look.Value.X >= look.Words.X + look.Words.W, $"{part.Text}: count at {look.Value.X}, words end at {look.Words.X + look.Words.W}");
                Assert.True(look.Value.X + look.Value.W <= look.Plate.X + look.Plate.W - toggle, $"{part.Text}: count ends at {look.Value.X + look.Value.W}, under the switch");
            }
            Assert.True(WindowShots.SaveTogether([.. menu.OpenPanels], "account-parts-dark-150.png", (0, 0, 0)));
        });
    }
}
