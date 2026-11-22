// folders: a list's items light edge to edge and touch (the person, 2026-10-30: "the items when hovering over them
// in a widget like the rss or folder widget should go from edge to edge and also touch each other. (the hover effect
// at least.) do this for items in other widgets as well."; docs\widgets.md §425). One sweep over every kind whose
// line is a list of items, each with items to show, drawn as the open folder draws it (CellIsland.PaintOn) and asked
// as it asks (CellIsland.TargetOn), every pixel down the line's leading edge and every third across each item:
//
//  - **Edge to edge.** Each item takes the pointer from the line's leading edge to its trailing one, and lights
//    exactly there: what changes when it is pointed at is the line's whole width, as tall as what takes it.
//  - **They touch.** Two items one after the other meet: no row of the line between them is nobody's, and none is
//    both's. Only a heading between them (a day's, the agenda's) parts them.
//  - **Every pixel of an item is somebody's**: its own, or a control on it (a Join, an Eject, a pin) — never nothing,
//    never the next item's.
//  - **From the line's edges** (the person, 2026-10-30: "the items should also go all the way to the top or bottom edge
//    if applicable. right now for some of the widgets there is a small strip on the top that is not selected"): the
//    first item, nothing drawn over it, starts at the line's top; at the height the line asks for, a strip under the
//    last one, nothing drawn in it, is no sliver of room shorter than an item.
//  - **Each as tall as the others** ("the last item should not be stretched unenccecarily though. it should still have
//    the same size as the other items"): a line taller than it asks for leaves the room under its last item unlit,
//    every item where and as tall as it was.
//
// Pictures: item-hover-<dark|light>.png in KARPO_PNG_DIR, every kind with its second item lit, in a folder's section;
// item-edges-<dark|light>.png, a few with their first and last item lit, as tall as asked for and taller.
// Needs Direct2D: KARPO_UI_TESTS=1. Nothing reaches the network.

using System.Numerics;
using Karpo.Dock;
using Karpo.Dock.Drives;
using Karpo.Dock.Pim;
using Karpo.Dock.QuickSettings;
using Karpo.Dock.QuickSettings.Services;
using Karpo.Dock.Sports;
using Karpo.Tests.Forecasts;
using Karpo.Tests.Islands;
using Karpo.Tests.Perf;
using Karpo.Tests.Pim;
using Karpo.Tests.Sports;
using TerraFX.Interop.DirectX;
using Xunit.Abstractions;
using static Karpo.Tests.Islands.Offscreen;
using FakeClipboard = Karpo.Tests.Clipboard.ClipboardHistoryTests.FakeClipboard;

namespace Karpo.Tests.Folders;

[Collection(SamplerState.Collection)]
public sealed unsafe class ItemHoverSweepTests(ITestOutputHelper output)
{
    private const string Owner = "layout:default";

    private const float Scale = 1.5f;

    private const int Step = 3;

    /// <summary>How much taller than it asks for a line is made, DIP: room for an item and a half under its last.</summary>
    private const float Room = 70;

    /// <summary>The kinds pictured with their first and last item lit, as tall as asked for and taller: item-edges-&lt;dark|light&gt;.png.</summary>
    private static readonly string[] Pictured = ["Rss", "Mail", "FolderContents", "FolderContentsSubfolder", "NotificationsList", "Wifi", "Keyboard", "Desktops", "Layouts", "Clipboard"];

    /// <summary>A kind's line with items to show; <see cref="Least"/> of them at the least reach its edge.</summary>
    private sealed record Case(string Name, CellIsland Line, int Least, Action Done);

    private static bool Item(int target) => target is not 0;

    /// <summary>
    /// <paramref name="line"/> in a line <paramref name="width"/> × <paramref name="height"/> px: its items down its
    /// leading edge, what each lights, and the rules above; failures said with <paramref name="name"/>.
    /// </summary>
    private static List<(int Target, int Top, int Bottom)> Sweep(string name, CellIsland line, float width, float height, TileStyle style, int least, bool natural,
        List<string> failures, List<string> seen)
    {
        var box = PopupPaint.Rect(0, 0, width, height);
        int At(float x, float y) => line.TargetOn(new Vector2(x, y), box, style, WidgetSurface.List);
        // Drawn first, as the open folder draws a line before the pointer reaches it.
        var rows = (int)MathF.Ceiling(width);
        line.HoverOn(null, box, style, WidgetSurface.List);
        // An item is a band of the line, not a card on it (the person, 2026-10-30: notifications "should be redesigned with
        // the edge to edge style as well"): nothing rounded taller than a row is drawn across the line under its items — a
        // control's plate is narrower, a field or a meter lower.
        var cards = new List<D2D_RECT_F>();
        PaintWatch.Styled = piece =>
        {
            if (piece.What is PaintedKind.Fill or PaintedKind.Outline && piece.Radius > 0 && piece.Box.right - piece.Box.left >= width * 2 / 3
                && piece.Box.bottom - piece.Box.top > LineRows.Px(1, style) + 0.5f)
            {
                cards.Add(piece.Box);
            }
        };
        byte[] rest;
        try
        {
            rest = LineHitSweepTests.Drawn(line, box, style);
        }
        finally
        {
            PaintWatch.Styled = null;
        }
        if (cards.Count > 0)
        {
            failures.Add($"{name}: {cards.Count} rounded cards drawn across the line, e.g. {cards[0].left:0}-{cards[0].right:0} × {cards[0].top:0}-{cards[0].bottom:0}");
        }
        var runs = new List<(int Target, int Top, int Bottom)>();
        for (var y = 0; y < (int)height; y++)
        {
            var target = At(0.5f, y + 0.5f);
            if (!Item(target))
            {
                continue;
            }
            if (runs.Count > 0 && runs[^1].Target == target && runs[^1].Bottom == y)
            {
                runs[^1] = runs[^1] with { Bottom = y + 1 };
            }
            else
            {
                runs.Add((target, y, y + 1));
            }
        }
        seen.Add($"{name}: {runs.Count} at the edge of {height:0} {string.Join(" ", runs.Select(run => $"{run.Target}:{run.Top}-{run.Bottom}"))}");
        if (runs.Count < least)
        {
            failures.Add($"{name}: {runs.Count} items take the pointer at the line's leading edge, not {least}");
        }
        // From the line's top when nothing is drawn over the first; at the height it asks for, to its bottom unless
        // what is under the last is drawn or is room for another item.
        if (runs.Count > 0 && runs[0].Top > 0 && !Inked(rest, rows, 0, runs[0].Top))
        {
            failures.Add($"{name}: a strip of {runs[0].Top} px over its first item, nothing drawn in it, nobody's");
        }
        if (natural && runs.Count > 0 && (int)height - runs[^1].Bottom is var under and > 0 && under < runs[^1].Bottom - runs[^1].Top
            && !Inked(rest, rows, runs[^1].Bottom, (int)height))
        {
            failures.Add($"{name}: a strip of {under} px under its last item ({runs[^1].Bottom}-{height:0}), nothing drawn in it, nobody's");
        }
        for (var index = 0; index < runs.Count; index++)
        {
            var (target, top, bottom) = runs[index];
            var others = runs.Where(run => run.Target != target).Select(run => run.Target).ToHashSet();
            // Every pixel of its band its own or a control's on it, the trailing edge included.
            int dead = 0, theirs = 0;
            Vector2? example = null;
            // Its first and last rows of pixels, where it meets its neighbours, and every third between.
            for (var y = top; y < bottom; y = y + Step < bottom - 1 || y == bottom - 1 ? y + Step : bottom - 1)
            {
                for (var x = 0.5f; x < width; x += 2 * Step)
                {
                    var at = At(x, y + 0.5f);
                    if (at == 0 || others.Contains(at))
                    {
                        dead += at == 0 ? 1 : 0;
                        theirs += at == 0 ? 0 : 1;
                        example ??= new Vector2(x, y + 0.5f);
                    }
                }
                var end = At(width - 0.5f, y + 0.5f);
                if (end == 0 || others.Contains(end))
                {
                    dead += end == 0 ? 1 : 0;
                    theirs += end == 0 ? 0 : 1;
                    example ??= new Vector2(width - 0.5f, y + 0.5f);
                }
            }
            if (dead + theirs > 0)
            {
                failures.Add($"{name}: item {target} ({top}-{bottom}) has {dead} points that do nothing and {theirs} another item's, e.g. ({example!.Value.X}, {example.Value.Y})");
            }
            // What it lights: the line's whole width, as tall as what takes it.
            line.HoverOn(new Vector2(0.5f, (top + bottom) / 2f), box, style, WidgetSurface.List);
            var lit = LineHitSweepTests.Apart(rest, LineHitSweepTests.Drawn(line, box, style), rows);
            line.HoverOn(null, box, style, WidgetSurface.List);
            if (lit is not { } found)
            {
                failures.Add($"{name}: item {target} ({top}-{bottom}) lights nothing");
            }
            else
            {
                // Its leading half, where no scrollbar stands, lit exactly as tall as it takes the pointer; its middle
                // row lit from edge to edge.
                var mask = found.Lit;
                int litTop = -1, litBottom = -1;
                for (var y = 0; y < (int)height; y++)
                {
                    for (var x = 0; x < rows / 2; x++)
                    {
                        if (mask[(y * rows) + x])
                        {
                            litTop = litTop < 0 ? y : litTop;
                            litBottom = y + 1;
                            break;
                        }
                    }
                }
                var middle = ((top + bottom) / 2) * rows;
                if (litTop != top || litBottom != bottom || !mask[middle] || !mask[middle + rows - 1])
                {
                    failures.Add($"{name}: item {target} takes 0-{width:0} × {top}-{bottom} and lights {found.Box.left:0}-{found.Box.right:0} × {litTop}-{litBottom}" +
                        $" (its middle row's ends lit: {mask[middle]}, {mask[middle + rows - 1]})");
                }
            }
            // The next one meets it, unless something stands between them (a heading): bare room is nobody's. A foot kept
            // on the line's bottom (To Do's "Add a task") has the room of a taller line over it.
            var foot = !natural && index + 2 == runs.Count && runs[^1].Bottom >= (int)height;
            if (index + 1 < runs.Count && !foot && runs[index + 1].Top - bottom is var gap and > 0 && !Inked(rest, rows, bottom, runs[index + 1].Top))
            {
                failures.Add($"{name}: items {target} and {runs[index + 1].Target} stand {gap} px apart ({bottom}-{runs[index + 1].Top}), not touching");
            }
        }
        return runs;
    }

    /// <summary>
    /// The line swept at the height it asks for (<paramref name="natural"/>) and taller (<paramref name="taller"/>): its
    /// last item no taller than the others, in either; alone, no taller in the taller line — never grown into the room.
    /// </summary>
    private static void Taller(string name, List<(int Target, int Top, int Bottom)> natural, List<(int Target, int Top, int Bottom)> taller, List<string> failures)
    {
        foreach (var (runs, which) in new[] { (natural, "at the height asked for"), (taller, "in a taller line") })
        {
            if (runs.Count > 1 && runs[^1].Bottom - runs[^1].Top is var last && runs.SkipLast(1).Max(run => run.Bottom - run.Top) is var most && last > most + 1)
            {
                failures.Add($"{name}: its last item {runs[^1].Target} is {last} px tall {which} ({runs[^1].Top}-{runs[^1].Bottom}), the others {most} at the most");
            }
        }
        if (natural.Count == 1 && taller.Count == 1 && taller[0].Bottom - taller[0].Top > natural[0].Bottom - natural[0].Top + 1)
        {
            failures.Add($"{name}: its one item grew from {natural[0].Top}-{natural[0].Bottom} to {taller[0].Top}-{taller[0].Bottom} in a taller line");
        }
    }

    /// <summary>Whether anything is drawn in rows <paramref name="from"/> to <paramref name="to"/> of a drawing <paramref name="width"/> px wide.</summary>
    private static bool Inked(byte[] pixels, int width, int from, int to)
    {
        for (var at = from * width * 4; at < to * width * 4 && at < pixels.Length; at += 4)
        {
            if (pixels[at + 3] > 8)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>The line drawn on a folder's section in <paramref name="style"/>, its <paramref name="lit"/> item under the pointer.</summary>
    private static void Panel(ID2D1DeviceContext* dc, CellIsland line, D2D_RECT_F box, TileStyle style, Vector2? lit)
    {
        var radius = style.Px(LineCorner.CardDip);
        PopupPaint.Fill(dc, box, radius, PopupPaint.Ink(style.Theme.Base));
        PopupPaint.Stroke(dc, box, radius, MathF.Max(1, style.Px(1)), PopupPaint.Ink(style.Theme.Stroke));
        var local = PopupPaint.Rect(0, 0, box.right - box.left, box.bottom - box.top);
        line.HoverOn(lit, local, style, WidgetSurface.List);
        PopupPaint.Clipped(dc, box, radius, () => Shifted(dc, box.left, box.top, () => line.PaintOn(dc, local, style, WidgetSurface.List)));
        line.HoverOn(null, local, style, WidgetSurface.List);
    }

    // --- The kinds, each with items to show ------------------------------------------------------

    private static (CellIsland Line, Action Done) InFolder(Gfx gfx, CellItem item, Action<FolderIsland>? before = null)
    {
        var folder = ListFormPictures.Folder(gfx, made =>
        {
            ListGutterTests.Before(made);
            before?.Invoke(made);
        }, item);
        return ((CellIsland)folder.Tiles[0].Island, () =>
        {
            folder.Closed();
            folder.Dispose();
        });
    }

    private static Case Folded(Gfx gfx, string name, CellItem item, int least, Action<FolderIsland>? before = null, Action? done = null)
    {
        var (line, close) = InFolder(gfx, item, before);
        return new Case(name, line, least, () =>
        {
            close();
            done?.Invoke();
        });
    }

    private static ulong Gb(double gigabytes) => (ulong)(gigabytes * 1024 * 1024 * 1024);

    private static Drive Stick(int letter, string label, double total, double free) => new()
    {
        Letter = letter,
        Kind = DriveKind.Removable,
        Volume = $@"\\?\Volume{{7e57{letter:D4}-0000-0000-0000-000000000000}}\",
        Label = label,
        Total = Gb(total),
        Free = Gb(free),
    };

    /// <summary>A To Do account with a few open tasks and a done one.</summary>
    private static FakeTodo Tasks(DateTime today)
    {
        var day = DateOnly.FromDateTime(today);
        return new FakeTodo().With("tasks", "Tasks", true,
            FakeTodo.Task("1", "Renew the passport", important: true, due: day.AddDays(-2), minutes: 1),
            FakeTodo.Task("2", "Call the plumber about the kitchen tap", due: day, minutes: 2),
            FakeTodo.Task("3", "Buy a birthday present for Anna", important: true, due: day.AddDays(1), minutes: 3),
            FakeTodo.Task("4", "Water the plants", minutes: 4),
            FakeTodo.Task("5", "Pay the electricity bill", done: true, minutes: 5)); // not-ui: sample tasks
    }

    /// <summary>Four meetings one after the other from an hour after <paramref name="now"/>, each with a Join.</summary>
    internal static CalendarEvent[] Meetings(DateTime now)
    {
        const string teams = "https://teams.microsoft.com/meet/2394857612345?p=AbCdEfGhIjKlMnOp";
        var start = now.Date.AddHours(now.Hour + 1);
        return [.. Enumerable.Range(0, 4).Select(index => new CalendarEvent(start.AddMinutes(30 * index), start.AddMinutes(30 * (index + 1)), false,
            $"Meeting {index + 1}", null, Join: teams))]; // not-ui: sample meetings
    }

    /// <summary>A day of events: an all-day holiday, two meetings with a Join, and between and after them events that are no meetings.</summary>
    internal static CalendarEvent[] Mixed(DateTime now)
    {
        var meetings = Meetings(now);
        var start = meetings[0].Start;
        return
        [
            new CalendarEvent(now.Date, now.Date.AddDays(1), true, "Public holiday", null), // not-ui: a sample event
            meetings[0],
            new CalendarEvent(start.AddMinutes(30), start.AddMinutes(60), false, "Focus time", null), // not-ui: a sample event
            meetings[2] with { Start = start.AddMinutes(60), End = start.AddMinutes(90) },
            new CalendarEvent(start.AddMinutes(90), start.AddMinutes(150), false, "Lunch with Anna", "Café"), // not-ui: a sample event
        ];
    }

    /// <summary>The feed with its pictures in first, so what lights is all that changes.</summary>
    private static Case Feed(Gfx gfx)
    {
        var made = Folded(gfx, "Rss", ListGutterTests.Made(ListKind.Rss), 4);
        _ = Karpo.Tests.Rss.RssSamples.Pictured((Karpo.Dock.Rss.RssIsland)made.Line);
        return made;
    }

    /// <summary>Mail's inbox, every message with a link it opens.</summary>
    private static Case Mail(Gfx gfx, DateTime now)
    {
        var store = new MailStore(new AccountSet([]));
        store.Answered([.. ListLinesTests.Inbox(now).Select(message => message with { Link = $"https://outlook.live.com/mail/inbox/id/{message.Id}" })], unread: 4);
        var cell = ListKind.Mail.New();
        cell.Id = "mail-hover";
        var mail = new MailIsland(Config.IslandKey(Owner, cell.Id), cell, gfx, new Config(), store, () => now);
        _ = mail.Refresh(gfx);
        return new Case("Mail", mail, 4, () =>
        {
            mail.Dispose();
            store.Dispose();
        });
    }

    /// <summary>The notification list showing <see cref="Sampled"/>, in a folder.</summary>
    private static Case Notifications(Gfx gfx, DateTime now, int least) =>
        Folded(gfx, "NotificationsList", ListGutterTests.Made(ListKind.NotificationsList), least,
            before: made => ((NotificationPartTile)made.Tiles[0].Island).Shown = Sampled(now));

    /// <summary>A square of one colour: an app's icon.</summary>
    private static Pixels Solid(uint argb)
    {
        var data = new byte[64 * 64 * 4];
        for (var at = 0; at < data.Length; at += 4)
        {
            data[at] = (byte)argb;
            data[at + 1] = (byte)(argb >> 8);
            data[at + 2] = (byte)(argb >> 16);
            data[at + 3] = (byte)(argb >> 24);
        }
        return new Pixels { Data = data, Width = 64, Height = 64 };
    }

    /// <summary>
    /// Notifications of two apps: chat's three, newest first — a body of one line, a long one the list cuts to two, one
    /// with two buttons — and mail's one.
    /// </summary>
    internal static List<NotificationEntry> Sampled(DateTime now)
    {
        var chat = Solid(0xFF5B5FC7);
        var mail = Solid(0xFF0F6CBD);
        var buttons = new ToastOffer("", ToastActivationKind.Foreground,
            [new ToastButton("Reply", "action=reply", ToastActivationKind.Foreground), new ToastButton("Mark as read", "action=read", ToastActivationKind.Background)], []);
        return
        [
            new(1, "chat", "Chat", chat, "Ada Lovelace", "Are the tables ready?", now.AddMinutes(-2), null),
            new(2, "chat", "Chat", chat, "Charles Babbage", "The review moved to Thursday. Bring the notes on the store and the mill, the diagrams of the "
                + "carriage, and whatever you have on the punched cards: we will go through all of it, and then lunch.", now.AddMinutes(-9), null),
            new(3, "chat", "Chat", chat, "Grace Hopper", "Found the bug in the relay.", now.AddMinutes(-25), null, buttons),
            new(4, "mail", "Mail", mail, "Weekly report", "Attached.", now.AddMinutes(-60), null),
        ];
    }

    /// <summary>Every kind's case, made in turn on the drawing thread with the gutter tests' stand-ins.</summary>
    private static IEnumerable<Func<Case>> Cases(Gfx gfx, DateTime now)
    {
        yield return () => Feed(gfx);
        yield return () =>
        {
            var folders = new FolderSourceState(FakeFolders.Projects().Source);
            return Folded(gfx, "FolderContents", FolderContentsLineTests.Showing(ListFormPictures.Line(ListKind.FolderContents), @"C:\Users\Ann\Projects"), 4, done: folders.Dispose);
        };
        yield return () =>
        {
            // A subfolder gone into: its head the way back (BackHead), from the line's top.
            var folders = new FolderSourceState(FakeFolders.Projects().Source);
            var made = Folded(gfx, "FolderContentsSubfolder", FolderContentsLineTests.Showing(ListFormPictures.Line(ListKind.FolderContents), @"C:\Users\Ann\Projects"), 3, done: folders.Dispose);
            ((FolderContentsLine)made.Line).Contents.Into(@"C:\Users\Ann\Projects\Karpo");
            return made;
        };
        yield return () => Mail(gfx, now);
        // Two apps, a heading over each: chat's three — a body of one line, one cut to two, one with buttons — and mail's one.
        yield return () => Notifications(gfx, now, 4);
        yield return () =>
        {
            var fake = new FakeClipboard
            {
                HistoryEnabled = true,
                History = [.. Enumerable.Range(0, 5).Select(index => new ClipView($"c{index}", ClipKind.Text, $"Copied text {index + 1}", null, false))],
            };
            var store = new ClipboardStore(fake);
            store.Refresh();
            store.Idle().GetAwaiter().GetResult();
            store.Deliver();
            var cell = ListKind.Clipboard.New();
            cell.Id = "clipboard-hover";
            var line = new ClipboardLine(Config.IslandKey(Owner, cell.Id), cell, gfx, new Config(), store);
            line.StandIn(ListWidgets.Of(ListKind.Clipboard));
            return new Case("Clipboard", line, 4, line.Dispose);
        };
        yield return () =>
        {
            var store = new ContactStore(new AccountSet([]));
            store.Answered([.. Enumerable.Range(0, 5).Select(index => new Karpo.Accounts.Person($"p{index}", $"Person {index:00}", null, [$"person{index}@example.com"], []))]);
            var cell = ListKind.Contacts.New();
            cell.Id = "contacts-hover";
            var line = new ContactsLine(Config.IslandKey(Owner, cell.Id), cell, gfx, new Config(), store);
            line.StandIn(ListWidgets.Of(ListKind.Contacts));
            line.Visible(true);
            return new Case("Contacts", line, 4, line.Dispose);
        };
        // Every event is an item, a meeting or not (§434): each opens in a calendar — here the calendar app, there being
        // no account with a site — and a meeting's Join is a control on its row.
        foreach (var kind in new[] { ListKind.Calendar, ListKind.Events, ListKind.Meeting })
        {
            yield return () =>
            {
                var was = CalendarIsland.Made;
                CalendarIsland.Made = ListFormCases.Store(kind == ListKind.Meeting ? Meetings(now) : Mixed(now));
                CalendarApp.Stand = () => "outlookcal:";
                return Folded(gfx, kind.ToString(), ListFormPictures.Line(kind), kind == ListKind.Meeting ? 3 : 5, done: () =>
                {
                    CalendarIsland.Made = was;
                    CalendarApp.Stand = null;
                });
            };
        }
        yield return () =>
        {
            var folder = Path.Combine(Isolation.Folder, "item-hover", Guid.NewGuid().ToString("N"));
            var book = new Karpo.Core.Notebook(folder);
            var store = new TaskStore(new AccountSet([Tasks(now)]), cache: new PimCache(folder), now: () => new DateTimeOffset(now));
            store.Refresh();
            store.Idle().GetAwaiter().GetResult();
            NotebookTile.Stand = book;
            TodoTile.TaskStand = store;
            return Folded(gfx, "Todo", ListFormPictures.Line(ListKind.Todo, line => line.Todo!.Account = "me@outlook.com"), 3, done: () =>
            {
                NotebookTile.Stand = null;
                TodoTile.TaskStand = null;
                TodoTile.Asking = null;
                store.Dispose();
                book.Dispose();
            });
        };
        yield return () =>
        {
            // §603: its fixtures and results, the list the line scrolled to.
            var item = ListKind.SportsFixtures.New();
            item.Id = "sports";
            item.Sports = SavedSports.Monza();
            var tile = (SportsTile)Karpo.Dock.Islands.CreateLine(Config.IslandKey(Owner, item.Id), item, ListKind.SportsFixtures, gfx, new Config())!;
            tile.Source = new SportsSource { Read = () => new SportsHeld(SportsPictures.MonzaWithFixtures(), SavedSports.SavedUtc, null, false), Logo = SportsPictures.Logo };
            _ = tile.Refresh(gfx);
            // The matches under their headings: a heading between each part.
            return new Case("Sports", tile, 2, tile.Dispose);
        };
        yield return () => Folded(gfx, "ScreenTimeApps", ListFormPictures.Line(ListKind.ScreenTimeApps), 4,
            before: made => ScreenTimeLinesTests.Feed(made.Tiles[0].Island, ScreenTimeLinesTests.Fortnight()));
        yield return () =>
        {
            var cell = ListKind.Desktops.New();
            cell.Id = "desktops-hover";
            var line = new DesktopsLine(Config.IslandKey(Owner, cell.Id), cell, gfx, new Config(), FakeDesktops.Of("Desktop 1", "Desktop 2", "Desktop 3", "Desktop 4")); // not-ui: sample names
            line.StandIn(ListWidgets.Of(ListKind.Desktops));
            line.Visible(true);
            return new Case("Desktops", line, 4, line.Dispose);
        };
        yield return () => Folded(gfx, "CpuProcesses", ListFormPictures.Line(ListKind.CpuProcesses, line => line.Height = LineHeight.Automatic(10)), 4);
        yield return () =>
        {
            var drives = DriveWatch.Source;
            Drive[] sticks = [Stick(4, "SANDISK", 29, 12), Stick(5, "SD Card", 64, 60), Stick(6, "Backup", 128, 40)]; // not-ui: sample drives
            DriveWatch.Source = (_, _) => sticks;
            return Folded(gfx, "RemovableDevices", ListFormPictures.Line(ListKind.RemovableDevices), 3, done: () => DriveWatch.Source = drives);
        };
        yield return () =>
        {
            MediaIsland.Stand = new MediaStand(AppName: "Spotify", Sessions:
                [new("Spotify.exe", "Spotify", true), new("msedge.exe", "Microsoft Edge", false), new("vlc.exe", "VLC", false)], Pick: new MediaPick()); // not-ui: sample apps
            return Folded(gfx, "MediaSources", ListFormPictures.Line(ListKind.MediaSources), 3, done: () => MediaIsland.Stand = new MediaStand());
        };
        yield return () =>
        {
            var config = new Config
            {
                Layouts = [.. new[] { "Work", "Home", "Gaming" }.Select((name, index) => new TaskbarLayout // not-ui: sample layouts
                {
                    Id = index == 0 ? TaskbarLayouts.DefaultId : $"L{index}",
                    Name = name,
                    Icon = TaskbarLayouts.LayoutGlyph,
                })],
                DefaultLayoutId = TaskbarLayouts.DefaultId,
            };
            var cell = ListKind.Layouts.New();
            var line = new LayoutsLine(Config.IslandKey(Owner, cell.Id), cell, gfx, config);
            line.StandIn(ListWidgets.Of(ListKind.Layouts));
            line.Visible(true);
            return new Case("Layouts", line, 3, line.Dispose);
        };
        foreach (var kind in new[] { ListKind.Wifi, ListKind.Bluetooth, ListKind.WirelessDisplays, ListKind.Keyboard, ListKind.PowerMode })
        {
            yield return () =>
            {
                QuickPageTile.Sources = new QuickSources
                {
                    Wifi = () => new SampleWifi(),
                    Bluetooth = () => new SampleBluetooth(),
                    Battery = () => new SampleBattery(),
                    Desktops = () => new SampleDesktopNames(),
                };
                var made = Folded(gfx, kind.ToString(), ListFormPictures.Line(kind), 3, done: () => QuickPageTile.Sources = null);
                Settle((QuickPageTile)made.Line);
                return made;
            };
        }
        yield return () =>
        {
            const string book = @"C:\pbk\rasphone.pbk";
            var vpn = new SampleVpn(new VpnConnection("Work", book, VpnState.Disconnected), new VpnConnection("Home", book, VpnState.Connected),
                new VpnConnection("Lab", book, VpnState.Disconnected)); // not-ui: sample connections
            QuickSwitches.Source = which => which == QuickSwitch.Vpn ? new VpnSwitch(() => vpn) : null;
            var made = Folded(gfx, "VpnConnections", ListFormPictures.Line(ListKind.VpnConnections), 3, done: () => QuickSwitches.Source = null);
            Settle((QuickPageTile)made.Line, page => page.Placed.OfType<Karpo.Dock.QuickSettings.Controls.ListRow>().Count() >= 3);
            return made;
        };
        foreach (var kind in new[] { ListKind.SoundOutput, ListKind.SoundInput })
        {
            // The device in use is the chosen one, nothing to press: the others are the items.
            yield return () => Folded(gfx, kind.ToString(), ListFormPictures.Line(kind), kind == ListKind.SoundOutput ? 2 : 1);
        }
    }

    /// <summary>A page given what it reads off the dock's thread: pumped until <paramref name="until"/>, or for a while.</summary>
    private static void Settle(QuickPageTile page, Func<QuickPageTile, bool>? until = null)
    {
        var deadline = Environment.TickCount64 + (until is null ? 400 : 5000);
        while (Environment.TickCount64 < deadline)
        {
            page.Pump();
            if (until?.Invoke(page) == true)
            {
                return;
            }
            Thread.Sleep(20);
        }
    }

    /// <summary>
    /// Every kind whose line lists items, with items to show, at the height it takes: its items reach both edges of
    /// the line, light all of it, and meet the next; every pixel of an item is its own or a control's on it.
    /// </summary>
    [UiFact]
    public void EveryListsItemsLightEdgeToEdgeAndTouch()
    {
        var failures = new List<string>();
        var seen = new List<string>();
        ListGutterTests.StoodIn<Gfx>(SavedWeather.Fixed, ScreenTimeLinesTests.Standing, gfx =>
        {
            var width = FolderFlyout.ListWidth(Scale);
            var sheets = new Dictionary<bool, List<(string Name, CellIsland Line, float Height, Vector2? Lit)>> { [true] = [], [false] = [] };
            var edges = new List<(string Name, CellIsland Line, float Height, Vector2? Lit)>();
            var made = new List<Case>();
            try
            {
                foreach (var make in Cases(gfx, Clock.Now()))
                {
                    Case kind;
                    try
                    {
                        kind = make();
                    }
                    catch (Exception error) when (error is not OutOfMemoryException)
                    {
                        failures.Add($"a case threw while it was made: {error.GetType().Name}: {error.Message}");
                        continue;
                    }
                    made.Add(kind);
                    var dark = FolderFlyout.StyleFor(Scale, ListFormPictures.LookFor(dark: true) with { Animations = false }, FlyoutTheme.For(ListFormPictures.LookFor(dark: true)));
                    var natural = kind.Line is IListContent content ? content.ListHeight(width, dark) : 0;
                    var height = MathF.Ceiling(natural > 0 ? natural : ListFormPictures.Rows(4, Scale, 1));
                    try
                    {
                        var asked = Sweep(kind.Name, kind.Line, width, height, dark, kind.Least, natural: true, failures, seen);
                        var tall = height + MathF.Ceiling(dark.Px(Room));
                        var taller = Sweep(kind.Name + " (taller)", kind.Line, width, tall, dark, kind.Least, natural: false, failures, seen);
                        Taller(kind.Name, asked, taller, failures);
                        if (Pictured.Contains(kind.Name) && asked.Count > 0 && taller.Count > 0)
                        {
                            Vector2 On((int Target, int Top, int Bottom) run) => new(width / 3, (run.Top + run.Bottom) / 2f);
                            edges.Add(($"{kind.Name}: the first", kind.Line, height, On(asked[0])));
                            edges.Add(($"{kind.Name}: the last", kind.Line, height, On(asked[^1])));
                            edges.Add(($"{kind.Name}: the last, room under it", kind.Line, tall, On(taller[^1])));
                        }
                    }
                    catch (Exception error) when (error is not OutOfMemoryException)
                    {
                        failures.Add($"{kind.Name}: threw {error.GetType().Name}: {error.Message}");
                    }
                    // Its second item lit (its first where it has one), dark and light.
                    var box = PopupPaint.Rect(0, 0, width, height);
                    Vector2? lit = null;
                    var items = 0;
                    for (var y = 0.5f; y < height; y++)
                    {
                        var target = kind.Line.TargetOn(new Vector2(0.5f, y), box, dark, WidgetSurface.List);
                        if (Item(target) && (lit is null || target != kind.Line.TargetOn(lit.Value, box, dark, WidgetSurface.List)))
                        {
                            items++;
                            lit = new Vector2(width / 3, y + 1);
                            if (items == 2)
                            {
                                break;
                            }
                        }
                    }
                    sheets[true].Add((kind.Name, kind.Line, height, lit));
                    sheets[false].Add((kind.Name, kind.Line, height, lit));
                }
                foreach (var (dark, lines) in sheets)
                {
                    Sheet(gfx, dark, width, lines, 5, $"item-hover-{(dark ? "dark" : "light")}.png");
                    Sheet(gfx, dark, width, edges, 3, $"item-edges-{(dark ? "dark" : "light")}.png");
                }
            }
            finally
            {
                foreach (var kind in made)
                {
                    kind.Done();
                }
            }
        });
        output.WriteLine(string.Join(Environment.NewLine, seen));
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures) + $"{Environment.NewLine}({failures.Count} in all)");
    }

    /// <summary>
    /// The notification list beside mail and the feed, dark and light (the person, 2026-10-30: "they should fit in with the
    /// other widgets in terms of style"): at rest, a notification lit (the one cut to two lines), and the × of the one
    /// with buttons under the pointer — notifications-beside-&lt;dark|light&gt;.png in KARPO_PNG_DIR.
    /// </summary>
    [UiFact]
    public void NotificationsBesideMailAndTheFeedPictures()
    {
        ListGutterTests.StoodIn<Gfx>(SavedWeather.Fixed, ScreenTimeLinesTests.Standing, gfx =>
        {
            var width = FolderFlyout.ListWidth(Scale);
            var now = Clock.Now();
            var made = new List<Case>();
            try
            {
                made.Add(Mail(gfx, now));
                made.Add(Feed(gfx));
                made.Add(Notifications(gfx, now, 4));
                var dark = FolderFlyout.StyleFor(Scale, ListFormPictures.LookFor(dark: true) with { Animations = false }, FlyoutTheme.For(ListFormPictures.LookFor(dark: true)));
                float Height(CellIsland line) => MathF.Ceiling(((IListContent)line).ListHeight(width, dark));
                var list = made[2].Line;
                var box = PopupPaint.Rect(0, 0, width, Height(list));
                Vector2 On(string piece)
                {
                    var found = ((ILinePieces)list).LinePieces(box, dark).Single(each => each.Name == piece).Box;
                    return new Vector2((found.left + found.right) / 2, (found.top + found.bottom) / 2);
                }
                var item = On("title 1");
                list.HoverOn(On("title 2"), box, dark, WidgetSurface.List);
                var cross = On("close 2");
                list.HoverOn(null, box, dark, WidgetSurface.List);
                var lines = new List<(string Name, CellIsland Line, float Height, Vector2? Lit)>
                {
                    ("Mail", made[0].Line, Height(made[0].Line), null),
                    ("Feed", made[1].Line, Height(made[1].Line), null),
                    ("Notifications", list, box.bottom, null),
                    ("A notification lit", list, box.bottom, item),
                    ("Its × lit", list, box.bottom, cross),
                };
                foreach (var shade in new[] { true, false })
                {
                    Sheet(gfx, shade, width, lines, lines.Count, $"notifications-beside-{(shade ? "dark" : "light")}.png");
                }
            }
            finally
            {
                foreach (var kind in made)
                {
                    kind.Done();
                }
            }
        });
    }

    /// <summary>The lines in <paramref name="columns"/> columns, each on a folder's section under its name, saved as <paramref name="file"/>.</summary>
    private static void Sheet(Gfx gfx, bool dark, float width, List<(string Name, CellIsland Line, float Height, Vector2? Lit)> lines, int columns, string file)
    {
        const float margin = 24;
        var look = ListFormPictures.LookFor(dark) with { Animations = false };
        var style = FolderFlyout.StyleFor(Scale, look, FlyoutTheme.For(look));
        var caption = PopupPaint.Round(14 * Scale);
        var band = caption * 2;
        var columnsOf = Enumerable.Range(0, columns).Select(column => lines.Where((_, index) => index % columns == column).ToList()).ToList();
        var tallest = columnsOf.Max(column => column.Sum(line => band + line.Height + margin));
        Bitmap(margin + (columns * (width + margin)), margin + tallest, dc =>
        {
            var backdrop = dark ? new DXGI_RGBA(0.20f, 0.24f, 0.30f, 1) : new DXGI_RGBA(0.72f, 0.78f, 0.86f, 1);
            dc->Clear(&backdrop);
            var ink = dark ? new DXGI_RGBA(1, 1, 1, 0.9f) : new DXGI_RGBA(0, 0, 0, 0.9f);
            for (var column = 0; column < columns; column++)
            {
                var left = margin + (column * (width + margin));
                var top = margin;
                foreach (var (name, line, height, lit) in columnsOf[column])
                {
                    PopupPaint.Text(dc, gfx, name, caption, Weight.Semibold, ink, left, top, width);
                    top += band;
                    Panel(dc, line, PopupPaint.Rect(left, top, width, height), style, lit);
                    top += height + margin;
                }
            }
        }, bitmap => Save(bitmap, Path.Combine(Pictures, file)));
    }
}
