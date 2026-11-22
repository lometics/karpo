// A click on a line of a list folder is asked of the line's own card, whatever its kind (docs\widgets.md
// §312). The person's log, 2026-10-23: "dock, caught and carried on: NotSupportedException: airQuality
// has no length along a bar: it is a component, sized by the room its folder gives it." A kind that drew
// its own card and said nothing of its targets had its click measured against the bar's cells, which a
// line has none of: the folder threw, the dock caught it, and a click on the sports line, the air
// quality's, the tides' or a network drive's did nothing at all.
//
// Held in the real folder on a hidden desktop: every kind a list holds, set up on the stand-ins the
// gutter sheets draw on (ListGutterTests.StoodIn), in a folder of its own; what the folder makes of a
// press at points all over the line is asked (FolderFlyout.PartAt), and none throws. The three that
// threw are clicked for real: the sports line hands the dock its match's page to open and the folder
// goes, as the air quality's (§614: its search) does; the tides' do nothing, and the folder stays open. No other kind's click
// is run: a graph's opens Task Manager and a switch flips this PC's (FolderClickRuleTests).

using System.Numerics;
using Karpo.Core;
using Karpo.Dock;
using Karpo.Dock.Pim;
using Karpo.Dock.Sports;
using Karpo.Dock.Weather;
using Karpo.Tests.Edit;
using Karpo.Tests.Forecasts;
using Karpo.Tests.Islands;
using Karpo.Tests.Perf;
using Karpo.Tests.Sports;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using FakeClipboard = Karpo.Tests.Clipboard.ClipboardHistoryTests.FakeClipboard;

namespace Karpo.Tests.Folders;

[Collection(SamplerState.Collection)]
public sealed class LineClickTests
{
    private const float Scale = 1.5f;

    /// <summary>How far apart the points pressed on a line are, in px.</summary>
    private const int Step = 9;

    /// <summary>The kinds whose click threw, clicked for real: what the folder does with it.</summary>
    private static readonly Dictionary<ListKind, FolderFlyoutAction> Clicked = new()
    {
        [ListKind.Sports] = FolderFlyoutAction.Forward,
        // §614: the air quality searches its place once the folder has gone.
        [ListKind.AirQuality] = FolderFlyoutAction.Forward,
        [ListKind.Tides] = FolderFlyoutAction.None,
    };

    [UiFact]
    public void EveryLineAnswersAClickFromItsOwnCard()
    {
        var failures = new List<string>();
        ListGutterTests.StoodIn<(Gfx Gfx, HINSTANCE Instance)>(
            SavedWeather.Fixed,
            body => WindowShots.OnHiddenDesktop("line-click", (gfx, instance) => body((gfx, instance))),
            context =>
            {
                var (gfx, instance) = context;
                var look = ListFormPictures.LookFor(true);
                using var flyout = new FolderFlyout(gfx, instance, 0);
                foreach (var widget in ListWidgets.All)
                {
                    var folder = Folder(gfx, widget.Kind);
                    try
                    {
                        flyout.Show(gfx, look, Scale, new POINT { x = 1700, y = 1500 }, new RECT { right = 2400, bottom = 1500 }, Edge.Bottom, folder, FlyoutAlign.End);
                        if (flyout.Plan is not { Cards.Count: > 0 } plan)
                        {
                            failures.Add($"{widget.Kind}: no line shown");
                            continue;
                        }
                        var card = plan.Cards[0];
                        var threw = Pressed(flyout, card);
                        if (threw is not null)
                        {
                            failures.Add($"{widget.Kind}: {threw}");
                        }
                        if (Clicked.TryGetValue(widget.Kind, out var wanted))
                        {
                            Click(flyout, folder, card, widget.Kind, wanted, failures);
                        }
                    }
                    finally
                    {
                        flyout.Hide();
                        folder.Dispose();
                    }
                }
            });
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    /// <summary>A list folder of one line of <paramref name="kind"/>, set up as the gutter sheets set it up, the lines that would read the network given saved answers.</summary>
    internal static FolderIsland Folder(Gfx gfx, ListKind kind)
    {
        var holder = BarKind.FolderList.New();
        var item = ListGutterTests.Made(kind);
        var markets = MarketIsland.Made;
        if (kind == ListKind.Markets)
        {
            // A ticker is one thing; the converter has parts of its own (its swap): that one, at a saved rate.
            item.Market = new MarketOptions { Symbol = "EURCHF=X", Amount = 100, Range = MarketRange.Day, Style = MarketStyle.Converter };
            MarketIsland.Made = MarketOffscreenTests.Settled(new()
            {
                ["EURCHF=X"] = MarketOffscreenTests.Answer("EURCHF=X", "EUR/CHF", "CHF", price: 0.9269, percent: -0.0205, previous: 0.927, wander: 0.001),
            });
        }
        Assert.NotNull(Karpo.Core.Folders.Add(BarKind.FolderList, holder.Folder!, item));
        // The lines that read a store as they are made read one of made-up items: never an account's or this PC's.
        MailIsland.Stand = kind == ListKind.Mail ? MailStand() : null;
        ContactsLine.Stand = kind == ListKind.Contacts ? ContactsStand() : null;
        ClipboardLine.Stand = kind == ListKind.Clipboard ? ClipboardStand() : null;
        // The lines with an event's Join: a day of meetings and events that are none, from the next hour on.
        var week = CalendarIsland.Made;
        if (kind is ListKind.Calendar or ListKind.Events or ListKind.Meeting or ListKind.DayView or ListKind.WeekView)
        {
            var now = Clock.Now();
            CalendarIsland.Made = ListFormCases.Store(kind == ListKind.Meeting ? ItemHoverSweepTests.Meetings(now) : ItemHoverSweepTests.Mixed(now));
        }
        FolderIsland folder;
        try
        {
            folder = new FolderIsland($"layout:default/{holder.Id}", holder, gfx, new Config());
        }
        finally
        {
            MailIsland.Stand = null;
            ContactsLine.Stand = null;
            ClipboardLine.Stand = null;
            CalendarIsland.Made = week;
            MarketIsland.Made = markets;
        }
        ListGutterTests.Before(folder);
        foreach (var tile in folder.Tiles)
        {
            switch (tile.Island)
            {
                case SportsTile sports:
                    sports.Source = new SportsSource { Read = () => new SportsHeld(SavedSports.BarcelonaAnswer(), SavedSports.SavedUtc, null, false) };
                    break;
                case AirQualityTile air:
                    air.Source = SavedAir.Source(SavedAir.Cache(european: 46, us: 54, pollen: "12.0"));
                    break;
                case QuakesLine quakes:
                    quakes.Source = () => QuakePictures.Week();
                    break;
                case ScreenTimeLine or ScreenTimeDonut:
                    ScreenTimeLinesTests.Feed(tile.Island, ScreenTimeLinesTests.Fortnight());
                    break;
            }
        }
        return folder;
    }

    /// <summary>An inbox of made-up messages, each with a link it opens.</summary>
    private static MailStore MailStand()
    {
        var store = new MailStore(new AccountSet([]));
        store.Answered([.. ListLinesTests.Inbox(Clock.Now()).Take(6).Select(message => message with { Link = $"https://outlook.live.com/mail/inbox/id/{message.Id}" })], unread: 3);
        return store;
    }

    /// <summary>A few made-up people, each with an address.</summary>
    private static ContactStore ContactsStand()
    {
        var store = new ContactStore(new AccountSet([]));
        store.Answered([.. Enumerable.Range(0, 4).Select(index => new Karpo.Accounts.Person($"p{index}", $"Person {index:00}", null, [$"person{index}@example.com"], []))]);
        return store;
    }

    /// <summary>
    /// A made-up clipboard with Windows' history off, so Karpo keeps its own: three texts copied, the middle one
    /// pinned — every row with its Pin and its Delete, "Clear all" and "Turn on" over them.
    /// </summary>
    private static ClipboardStore ClipboardStand()
    {
        var fake = new FakeClipboard { HistoryEnabled = false };
        var store = new ClipboardStore(fake);
        store.Listen(true);
        foreach (var text in new[] { "Copied text 1", "Copied text 2", "Copied text 3" })
        {
            fake.Copy(new ClipSnapshot(ClipMarkers.None, text, null));
            store.Idle().GetAwaiter().GetResult();
        }
        store.Refresh();
        store.Idle().GetAwaiter().GetResult();
        store.Deliver();
        store.Pin(store.Items[1], true);
        return store;
    }

    /// <summary>A point of the open panel (its px) as a mouse message's lParam, from where the panel stands.</summary>
    internal static unsafe nint Client(FolderFlyout flyout, Vector2 panel)
    {
        var on = flyout.PanelOnScreen;
        var screen = new POINT { x = on.left + (int)panel.X, y = on.top + (int)panel.Y };
        Assert.True(TerraFX.Interop.Windows.Windows.ScreenToClient(flyout.Hwnd, &screen));
        return (nint)(((screen.y & 0xFFFF) << 16) | (screen.x & 0xFFFF));
    }

    /// <summary>What the folder makes of a press at points all over <paramref name="card"/>: null, or the first that threw and why.</summary>
    private static string? Pressed(FolderFlyout flyout, D2D_RECT_F card)
    {
        for (var y = card.top + 1; y < card.bottom - 1; y += Step)
        {
            for (var x = card.left + 1; x < card.right - 1; x += Step)
            {
                try
                {
                    _ = flyout.PartAt(Client(flyout, new Vector2(x, y)));
                }
                catch (Exception error)
                {
                    return $"a press at ({x - card.left:0}, {y - card.top:0}) threw {error.GetType().Name}: {error.Message}";
                }
            }
        }
        return null;
    }

    /// <summary>
    /// A click on <paramref name="card"/>, on its leading third, as the
    /// pointer gives it: the folder does <paramref name="wanted"/> with it, from a line that is set up.
    /// </summary>
    private static void Click(FolderFlyout flyout, FolderIsland folder, D2D_RECT_F card, ListKind kind, FolderFlyoutAction wanted, List<string> failures)
    {
        var line = (CellIsland)folder.Tiles[0].Island;
        if (line.Unset is not null)
        {
            failures.Add($"{kind}: not set up, so its click is its \"Set up\"");
            return;
        }
        var at = Client(flyout, new Vector2(card.left + ((card.right - card.left) / 3), (card.top + card.bottom) / 2));
        FolderFlyoutEvent said;
        try
        {
            _ = flyout.Message(WM.WM_LBUTTONDOWN, 1u, at);
            said = flyout.Message(WM.WM_LBUTTONUP, 0u, at);
        }
        catch (Exception error)
        {
            failures.Add($"{kind}: the click threw {error.GetType().Name}: {error.Message}");
            return;
        }
        if (said.Kind != wanted)
        {
            failures.Add($"{kind}: the folder said {said.Kind}, not {wanted}");
        }
        // Forwarded, it is an app opening, and the folder has gone; else the folder stays where it is.
        if (wanted == FolderFlyoutAction.Forward && (said.Action.View != IslandView.App || flyout.IsOpen))
        {
            failures.Add($"{kind}: forwarded {said.Action.View}, the folder {(flyout.IsOpen ? "still open" : "gone")}");
        }
        if (wanted == FolderFlyoutAction.None && !flyout.IsOpen)
        {
            failures.Add($"{kind}: the folder closed");
        }
        if (line is SportsTile sports && sports.Site?.StartsWith("https://www.espn.com/", StringComparison.Ordinal) != true)
        {
            failures.Add($"{kind}: opens {sports.Site ?? "nothing"}, not the match's page or the team's");
        }
    }
}
