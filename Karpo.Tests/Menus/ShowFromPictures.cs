// §455: a widget's "Show from" on a real menu (KARPO_UI_TESTS=1): the level of switches under each account's name, a
// switch flipped leaving the person on the level, lit where they were. Pictures in KARPO_PNG_DIR, dark, 150 %:
// showfrom-contacts-dark.png, showfrom-mail-dark.png (an inbox and its folders, one picked), showfrom-calendar-dark.png
// (two accounts' calendars and the public holidays).

using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using Karpo.Tests.Localization;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Menus;

[Collection(InLanguage.Collection)]
public sealed class ShowFromPictures
{
    private const string Ann = "ann@example.com";

    private const string Work = "ann@contoso.com";

    private static List<SourcePart> Contacts() =>
    [
        new(WidgetSources.Key(Ann, "contacts:default"), "Contacts", "Ann Smith"),
        new(WidgetSources.Key(Ann, "contacts:family"), "Family", "Ann Smith"),
        new(WidgetSources.Key(Work, "contacts:default"), "Contacts", "Contoso"),
    ];

    private static List<SourcePart> Mail() =>
    [
        new(WidgetSources.Key(Ann, "mail"), S.CoreAccountsInbox, "Ann Smith"),
        new(WidgetSources.Key(Ann, "mail:Archive"), "Archive", "Ann Smith", Default: false),
        new(WidgetSources.Key(Ann, "mail:INBOX/Projects"), "Inbox / Projects", "Ann Smith", Default: false),
        new(WidgetSources.Key(Ann, "mail:Receipts"), "Receipts", "Ann Smith", Default: false),
        new(WidgetSources.Key(Work, "mail"), S.CoreAccountsInbox, "Contoso"),
        new(WidgetSources.Key(Work, "mail:SENT"), "Sent Items", "Contoso", Default: false),
    ];

    private static List<SourcePart> Calendars() =>
    [
        new(WidgetSources.Key(Ann, "calendar:home"), "Calendar", "Ann Smith"),
        new(WidgetSources.Key(Ann, "calendar:birthdays"), "Birthdays", "Ann Smith"),
        new(WidgetSources.Key(Work, "calendar:work"), "Calendar", "Contoso"),
        new(WidgetSources.Key(WidgetSources.HolidaysGroup, "CH"), "Switzerland", WidgetSources.HolidaysGroup),
    ];

    private static void KeysTo(Flyout menu, uint id)
    {
        for (var step = 0; step < 80 && menu.KeysOn != id; step++)
        {
            _ = menu.Message(WM.WM_KEYDOWN, VK.VK_DOWN, 0);
        }
        Assert.Equal(id, menu.KeysOn);
    }

    [UiFact]
    public void ShowFromIsALevelOfSwitchesThatStaysOnItsLevel()
    {
        WindowShots.OnHiddenDesktop("show-from", (gfx, instance) =>
        {
            const float scale = 1.5f;
            var look = AddListPictures.LookFor(dark: true);
            var work = new RECT { right = (int)(1920 * scale), bottom = (int)(1040 * scale) };
            var anchor = new POINT { x = work.right / 3, y = work.bottom };
            var place = new OptionPlace(new Config(), Dark: true);

            void Picture(WidgetItem item, List<SourcePart> parts, string name, string? flip)
            {
                var devices = new OptionDevices { Parts = parts };
                List<OptionPick> picks = [];
                List<FlyoutItem> Items() => OptionMenu.Section(OptionRows.Of(item, devices, place), picks);
                using var menu = new Flyout(gfx, instance, 0) { Configures = true };
                var items = Items();
                menu.Show(gfx, look, scale, items, FlyoutLayout.List, anchor, work, Edge.Bottom, null);
                WindowShots.Settle();
                var row = items.Single(each => each.Text == S.DockMenuShowFromTitle);
                KeysTo(menu, row.Id);
                _ = menu.Message(WM.WM_KEYDOWN, VK.VK_RIGHT, 0);
                WindowShots.Settle();
                Assert.Equal((1, S.DockMenuShowFromTitle), (menu.InlineDepth, menu.InlineTitle));
                if (flip is not null)
                {
                    // A switch flipped, as the dock does it: set, the rows read again, the menu shown again — on its level, lit where it was.
                    var target = row.Children!.First(each => each.Text == flip && each.Control is FlyoutControl.Toggle);
                    KeysTo(menu, target.Id);
                    var said = menu.Message(WM.WM_KEYDOWN, VK.VK_SPACE, 0);
                    Assert.Equal(new FlyoutEvent(FlyoutEventKind.Adjust, target.Id), said);
                    var before = item.Sources;
                    Assert.True(OptionMenu.Apply(item, OptionMenu.PickOf(picks, said.Id)!, devices, place));
                    Assert.NotEqual(before, item.Sources);
                    picks.Clear();
                    menu.Refresh(OptionMenu.Updated(menu.ShownItems, Items()));
                    WindowShots.Settle();
                    Assert.Equal((1, S.DockMenuShowFromTitle), (menu.InlineDepth, menu.InlineTitle));
                    Assert.Equal(target.Id, menu.HotId);
                }
                Assert.True(WindowShots.SaveTogether([.. menu.OpenPanels], name, ((byte)0, (byte)0, (byte)0)));
                // Back on the widget's own level: "Show from" says what it shows at its end.
                _ = menu.Message(WM.WM_KEYDOWN, VK.VK_LEFT, 0);
                WindowShots.Settle();
                Assert.Equal(0, menu.InlineDepth);
                Assert.True(WindowShots.SaveTogether([.. menu.OpenPanels], name.Replace("-dark", "-row-dark", StringComparison.Ordinal), ((byte)0, (byte)0, (byte)0)));
                menu.Hide();
            }

            Picture(ListKind.Contacts.New(), Contacts(), "showfrom-contacts-dark.png", flip: "Family");
            Picture(ListKind.Mail.New(), Mail(), "showfrom-mail-dark.png", flip: "Archive");
            Picture(ListKind.Calendar.New(), Calendars(), "showfrom-calendar-dark.png", flip: "Birthdays");
        });
    }
}
