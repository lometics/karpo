// The mail widget's list line (Karpo.Dock\Pim\MailIsland.cs, docs\widgets.md §16): as tall as
// the messages it has to show, up to eight, over an inbox with unread mail and an empty one.
//
// The store is filled as a read of the accounts would fill it
// (MailStore.Answered), so no account is ever reached, and the clock is frozen
// so the times beside the senders do not move.

using Karpo.Accounts;
using Karpo.Core;
using Karpo.Dock;
using Karpo.Dock.Pim;
using Karpo.Tests.Folders;
using Karpo.Tests.Islands;

namespace Karpo.Tests.Pim;

public sealed unsafe class MailCardTests
{
    private static readonly DateTime Now = new(2026, 9, 26, 15, 0, 0);

    private static List<MailItem> Inbox() =>
    [
        new("1", "Sandra Gerber", "sandra@example.ch", "Lunch on Friday? The new place by the lake has a terrace", null, new DateTimeOffset(Now.AddMinutes(-4)), true),
        new("2", "GitHub", "noreply@github.com", "[karpo-app] CI passed on master", null, new DateTimeOffset(Now.AddMinutes(-52)), true),
        new("3", "Adrian Hager", "adrian.hager@buhlergroup.com", "Re: Quarterly review — slides attached", null, new DateTimeOffset(Now.AddHours(-3)), false),
        new("4", "Swiss International Air Lines", "news@swiss.com", "Your boarding pass for LX 1832", null, new DateTimeOffset(Now.AddDays(-1)), true),
        new("5", "Beat Hagenbuch", "beat@example.ch", "Photos", null, new DateTimeOffset(Now.AddDays(-3)), false),
    ];

    private static MailIsland Make(Gfx gfx, MailStore store)
    {
        var widget = ListKind.Mail.New();
        widget.Id = "mail";
        return new MailIsland(Config.IslandKey(null, widget.Id), widget, gfx, new Config(), store, () => Now);
    }

    private static List<MailItem> Linked() =>
        [.. Inbox().Select(message => message.Id == "3" ? message : message with { Link = $"https://outlook.office.com/mail/id/{message.Id}" })];

    private static System.Numerics.Vector2 Middle(TerraFX.Interop.DirectX.D2D_RECT_F box) => new((box.left + box.right) / 2, (box.top + box.bottom) / 2);

    /// <summary>
    /// Each message with a link is a row of its own (the user, 2026-10-11: "the emails in the email
    /// widget should be individually clickable / highligtable"): under the pointer it lights, a press
    /// on it lights pressed until the click, and the click is an app opening the folder forwards, which
    /// opens that message's link once. A message with none is no target, and does nothing.
    /// </summary>
    [UiFact]
    public void AMessageLightsAndItsClickOpensIt()
    {
        Offscreen.OnStaThread(gfx =>
        {
            using var store = new MailStore(new AccountSet([]));
            using var island = Make(gfx, store);
            var opened = new List<string>();
            island.Opened = opened.Add;
            var look = TestLooks.LookFor(dark: true);
            var style = FolderFlyout.StyleFor(1f, look, FlyoutTheme.For(look));
            store.Answered(Linked(), unread: 3);
            var box = PopupPaint.Rect(0, 0, 360, island.ListHeight(360, style));
            var pieces = ((ILinePieces)island).LinePieces(box, style);

            // The second message: lit under the pointer, pressed, then clicked.
            var at = Middle(Assert.Single(pieces, piece => piece.Name == "subject 1").Box);
            Assert.Equal(2, island.CardTarget(at, box, style, WidgetSurface.List));
            island.CardHover(at, box, style, WidgetSurface.List);
            Assert.Equal((2, 0), island.Lighted);
            Assert.False(island.CardPress(at, box, style, WidgetSurface.List), "the press stays the folder's, for its click");
            Assert.Equal((2, 2), island.Lighted);
            var action = island.Click(2);
            Assert.True(FolderFlyout.Forwards(action, ListWidgets.Of(ListKind.Mail).Act));
            Assert.Equal((2, 0), island.Lighted);
            Assert.Empty(opened);
            island.Open();
            island.Open();
            Assert.Equal(["https://outlook.office.com/mail/id/2"], opened);

            // A press let go elsewhere: the pointer leaving the row lets the press go.
            island.CardPress(at, box, style, WidgetSurface.List);
            island.CardHover(null, box, style, WidgetSurface.List);
            Assert.Equal((0, 0), island.Lighted);

            // The message with no link (the third) is read, not pressed.
            var unlinked = Middle(Assert.Single(pieces, piece => piece.Name == "sender 2").Box);
            Assert.Equal(0, island.CardTarget(unlinked, box, style, WidgetSurface.List));
            Assert.Equal(IslandView.None, island.Click(3).View);
            island.Open();
            Assert.Single(opened);
        });
    }

    /// <summary>
    /// The kit's header and items (§401): the messages' words where every name begins, under the header's
    /// title; how much is unread is the header's meta, at its end.
    /// </summary>
    [UiFact]
    public void TheMessagesStandWhereEveryNameBegins()
    {
        Offscreen.OnStaThread(gfx =>
        {
            using var store = new MailStore(new AccountSet([]));
            using var island = Make(gfx, store);
            var look = TestLooks.LookFor(dark: false);
            var style = FolderFlyout.StyleFor(1.5f, look, FlyoutTheme.For(look));
            store.Answered(Linked(), unread: 3);
            var width = FolderFlyout.ListWidth(style.Scale);
            var form = ListForm.Of(gfx, style);
            foreach (var height in new[] { island.ListHeight(width, style), LineRows.Px(3, style) })
            {
                var pieces = ((ILinePieces)island).LinePieces(PopupPaint.Rect(0, 0, width, height), style);
                var sender = Assert.Single(pieces, piece => piece.Name == "sender 0").Box;
                Assert.Equal(form.TextLeft, sender.left, 1);
                Assert.Equal(form.TextLeft, Assert.Single(pieces, piece => piece.Name == "title").Box.left, 1);
                var head = Assert.Single(pieces, piece => piece.Name == "head").Box;
                Assert.True(head.left > width / 2 && head.right <= width - form.Inset + 0.5f, $"the count at {head.left:0}..{head.right:0} of {width:0}");
            }
        });
    }

    [Fact]
    public void AnInboxLineIsTallerWithMoreMessagesAndStopsAtEight()
    {
        Offscreen.OnStaThread(gfx =>
        {
            using var store = new MailStore(new AccountSet([]));
            using var island = Make(gfx, store);
            var look = TestLooks.LookFor(dark: true);
            var style = FolderFlyout.StyleFor(1f, look, FlyoutTheme.For(look));

            store.Answered([], unread: 0);
            var empty = island.ListHeight(360, style);
            store.Answered(Inbox(), unread: 3);
            var five = island.ListHeight(360, style);
            store.Answered([.. Enumerable.Range(0, 20).SelectMany(_ => Inbox())], unread: 3);
            var many = island.ListHeight(360, style);

            Assert.True(five > empty);
            Assert.True(many > five);
            store.Answered([.. Enumerable.Range(0, 2).SelectMany(_ => Inbox())], unread: 3);
            Assert.Equal(many, island.ListHeight(360, style));
        });
    }
}
