// A folder's flyout, offscreen: its list drawn into PNGs with the painting its
// composition surfaces get (FolderPictures), and the real flyout
// shown on a hidden desktop, taking the pointer, the wheel and the keys, a
// widget's click, Escape, a change of its folder and the folder
// going away (FolderRuns). Nothing ever shows on the person's desktop, and the
// widgets used only read (the clock, the battery, a weather line without a
// place, which fetches nothing, a heading); none of them launches anything.
//
// Like the other offscreen tests these need a window station and Direct2D, so
// they only run with KARPO_UI_TESTS=1; the pictures go to KARPO_PNG_DIR.

using System.Numerics;
using Karpo.Dock;
using Karpo.Dock.Native;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.DirectX.DirectX;
using static TerraFX.Interop.Windows.Windows;
using Color = Windows.UI.Color;
using IDispatcherQueueController = TerraFX.Interop.WinRT.IDispatcherQueueController;

namespace Karpo.Tests.Folders;

public sealed unsafe class FolderOffscreenTests
{
    private const string Owner = "layout:default";

    private static string Pictures
    {
        get
        {
            var folder = Environment.GetEnvironmentVariable("KARPO_PNG_DIR") is { Length: > 0 } named
                ? named
                : Path.Combine(Path.GetTempPath(), "karpo-tests", "pictures");
            Directory.CreateDirectory(folder);
            return folder;
        }
    }

    private static Look LookFor(bool dark) => new(
        Glass: false,
        Dark: dark,
        Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4),
        AccentLight2: Color.FromArgb(255, 0x99, 0xEB, 0xFF),
        AccentDark1: Color.FromArgb(255, 0x00, 0x5F, 0xB8));

    private static BarItem Widget(string id, BarKind kind, Action<BarItem>? set = null)
    {
        var island = kind.New();
        island.Id = id;
        set?.Invoke(island);
        return island;
    }

    /// <summary>A line of <paramref name="kind"/>, for a list folder or a piece drawn on its own.</summary>
    private static CellItem Widget(string id, ListKind kind)
    {
        var line = kind.New();
        line.Id = id;
        return line;
    }

    /// <summary>
    /// A list folder of what a list takes of a glance at things — a pin, three switches and a
    /// graph, each the list's own row (ListWidgets).
    /// </summary>
    private static BarItem Folder()
    {
        var folder = new FolderOptions();
        var island = Widget("glance", BarKind.FolderList);
        island.Folder = folder;
        folder.Put(Widget("pin", ListKind.Pin));
        folder.Put(Widget("airplane", ListKind.AirplaneMode));
        folder.Put(Widget("night", ListKind.NightLight));
        folder.Put(Widget("saver", ListKind.EnergySaver));
        folder.Put(Widget("memory", ListKind.Memory));
        return island;
    }

    /// <summary>
    /// A list folder of widgets that only read and whose click does nothing — a clock, the battery,
    /// a weather line without a place, which fetches nothing, and a heading — the clock first.
    /// </summary>
    private static BarItem Readings()
    {
        var folder = new FolderOptions();
        var island = Widget("glance", BarKind.FolderList);
        island.Folder = folder;
        folder.Put(Widget("clock", ListKind.Clock));
        folder.Put(Widget("battery", ListKind.Battery));
        folder.Put(Widget("weather", ListKind.WeatherNow));
        var heading = Widget("notes", ListKind.Heading);
        heading.Heading = new HeadingOptions { Text = "Notes" };
        folder.Put(heading);
        return island;
    }

    [UiFact]
    public void FolderPictures()
    {
        OnStaThread(desktop: false, gfx =>
        {
            var config = new Config();
            foreach (var (dark, editing, file) in new[]
            {
                (true, false, "folder-list-dark.png"),
                (false, false, "folder-list-light.png"),
                // edit: the folder being arranged — the place after the last widget offers a "+",
                // and the one the catalogue is open for stands in the accent.
                (true, true, "folder-edit-list-dark.png"),
                (false, true, "folder-edit-list-light.png"),
            })
            {
                var island = Folder();
                using var folder = new FolderIsland($"{Owner}/{island.Id}", island, gfx, config);
                folder.Opened(gfx);
                var look = LookFor(dark);
                var theme = FlyoutTheme.For(look);
                var scale = 1.5f;
                var plan = FolderFlyout.Build(look, scale, folder.View, folder.Options, folder.Tiles.Select(tile => tile.Cell).ToList(), 1040, editing: editing);
                Assert.Equal(folder.Tiles.Count, plan.Cards.Count);
                Assert.Equal(editing && plan.Adds.Count > 0, editing);
                Picture(plan.Width, plan.Height, scale, theme, file, dc =>
                {
                    FolderFlyout.PaintEmpty(dc, gfx, plan, theme);
                    for (var index = 0; index < plan.Cards.Count; index++)
                    {
                        if (!plan.Shows(index))
                        {
                            // A widget a fixed height had no room for: the folder draws nothing
                            // for it (FolderFlyout.DrawCard), so neither does the sheet.
                            continue;
                        }
                        var card = plan.Cards[index];
                        var tile = folder.Tiles[index];
                        // **A list has no plate at rest** (FolderFlyout's own rule, ShowStates): its
                        // line is its own furniture, and what divides one line from the next is a
                        // divider somebody put there (DividerTile). The sheet used to plate every
                        // list row, which is why a hairline drawn between two of them was invisible
                        // in the picture and visible in the product. A hover on the first line.
                        if (index == 0)
                        {
                            PopupPaint.Fill(dc, card, plan.Radius, PopupPaint.Ink(theme.SubtleHover));
                        }
                        var box = PopupPaint.Rect(0, 0, card.right - card.left, card.bottom - card.top);
                        Shifted(dc, card.left, card.top, () =>
                        {
                            // edit: an arranged folder's lines wear no outline; only the selected one its ring.
                            FolderFlyout.PaintListLine(dc, gfx, tile.Island, tile.Cell.Island, box, plan, theme, scale);
                        });
                    }
                });
                folder.Closed();
            }
        });
    }

    /// <summary>
    /// The notifications folder against the list's new edges. Its component insets everything by
    /// the notification panel's own 12 DIP pad, so nothing of it reaches a border and nothing
    /// meets a rounded corner — but its 48 DIP band is now flush with the panel's top, and whether
    /// a folder that opens with that band alone up there reads as the panel's own or as a widget
    /// jammed against the edge is a judgement somebody has to look at.
    /// <para>
    /// The notifications are <b>made up</b> (<see cref="NotificationPartTile.Shown"/>), not the
    /// machine's own. A sheet that draws whatever the store happens to hold shows three cards and
    /// a summary on one machine and the empty state on the next, and two runs that cannot be
    /// compared are not a guard — which is what this sheet exists to be for the next edge change.
    /// Eight of them, which is more than the line holds, so the picture always carries the band,
    /// whole cards and the summary row that stands for the rest.
    /// </para>
    /// </summary>
    [UiFact]
    public void NotificationsFolderPictures()
    {
        OnStaThread(desktop: false, gfx =>
        {
            var config = new Config();
            // The clock is pinned as well as the notifications, so the times read as they do in
            // earnest ("3 m", "1 h") and read the same on every machine. A fixture dated in the
            // past without this shows eight identical absolute dates, which is deterministic and
            // says nothing about the drawing anybody actually sees.
            var was = Clock.Now;
            var at = new DateTime(2026, 9, 22, 18, 0, 0);
            Clock.Now = () => at;
            var said = new[]
            {
                ("Mail", "Vera Holm", "Re: the roof — I've asked them for a second quote, and they say Thursday at the earliest."),
                ("Calendar", "Standup in 10 minutes", "Daily · Team room"),
                ("Messages", "Dad", "Landed! Getting a coffee then I'll find the taxi rank."),
                ("Photos", "3 years ago today", "A morning at the lake."),
                ("Mail", "Nils Ahlberg", "Invoice 2214 attached."),
                ("Updates", "Restart to finish updating", "Karpo will reopen where you left it."),
                ("Messages", "Ida", "are you coming tonight?"),
                ("Mail", "Building management", "Water off 09:00–12:00 on Tuesday."),
            };
            var made = said.Select((line, index) => new NotificationEntry(
                index + 1, line.Item1.ToLowerInvariant(), line.Item1, null, line.Item2, line.Item3,
                at.AddMinutes(-7 * index * index), null)).ToList();

            try
            {
                foreach (var dark in new[] { true, false })
                {
                    var island = PanelFolders.Notifications();
                    using var folder = new FolderIsland($"{Owner}/{island.Id}", island, gfx, config);
                    foreach (var tile in folder.Tiles)
                    {
                        if (tile.Island is NotificationPartTile card)
                        {
                            card.Shown = made;
                        }
                    }
                    folder.Opened(gfx);
                    var look = LookFor(dark);
                    var theme = FlyoutTheme.For(look);
                    var scale = 1.5f;
                    var plan = FolderFlyout.Build(look, scale, folder.View, folder.Options, folder.Tiles.Select(tile => tile.Cell).ToList(), 1040);

                    // The list's own edges, which is what this sheet is here to show against.
                    Assert.All(plan.Cards, card => Assert.Equal(0, card.left, 1));
                    Assert.Equal(0, plan.Cards[0].top, 1);

                    Picture(plan.Width, plan.Height, scale, theme, $"folder-notifications-{(dark ? "dark" : "light")}.png",
                        dc => Panel(dc, gfx, folder, plan, look, theme, scale));
                    folder.Closed();
                }
            }
            finally
            {
                Clock.Now = was;
            }
        });
    }

    /// <summary>
    /// A list folder's items reach the panel's own edge: there is no inset between a card and the
    /// border, and a divider somebody put between two of them runs the full width with them. The
    /// folder shown holds both sorts of item, because they reach that edge by different paths — a
    /// plain widget draws the folder's own row (its icon and name inset inside the card) and a
    /// component draws its own card — and one divider, under the pin; the timer and airplane mode
    /// have none between them, as a list draws none of its own.
    /// </summary>
    [UiFact]
    public void ListEdgePictures()
    {
        OnStaThread(desktop: false, gfx =>
        {
            var config = new Config();
            foreach (var dark in new[] { true, false })
            {
                var options = new FolderOptions();
                foreach (var (kind, row) in new[] { (ListKind.Pin, 0), (ListKind.Divider, 1), (ListKind.Timer, 2), (ListKind.AirplaneMode, 3) })
                {
                    var widget = kind.New();
                    widget.Id = kind.ToString().ToLowerInvariant();
                    options.Put(widget);
                }
                var island = BarKind.FolderList.New();
                island.Id = "listedge";
                island.Folder = options;
                using var folder = new FolderIsland($"{Owner}/{island.Id}", island, gfx, config);
                folder.Opened(gfx);
                var look = LookFor(dark);
                var theme = FlyoutTheme.For(look);
                var scale = 1.5f;
                var plan = FolderFlyout.Build(look, scale, folder.View, folder.Options, folder.Tiles.Select(tile => tile.Cell).ToList(), 1040);

                // The measurement the picture is of: a card's own left edge, in panel px.
                Assert.All(plan.Cards, card => Assert.Equal(0, card.left, 1));
                Assert.All(plan.Cards, card => Assert.Equal(plan.Width, card.right, 1));

                Picture(plan.Width, plan.Height, scale, theme, $"folder-listedge-{(dark ? "dark" : "light")}.png",
                    dc => Panel(dc, gfx, folder, plan, look, theme, scale));

                // Scrolled, with the card that lands under the panel's top corner hovered: the
                // question is what a card whose own top is off the panel does at that corner.
                // It takes the panel's curve, because the panel clips its content to that shape —
                // and it has to, since the alternative is a square corner poking into the curve.
                // It reads as the content meeting the panel's edge and not as a card with a
                // rounded top, because the panel's outline is visibly rounded in the same place.
                var scrolled = plan.Side * 1.5f;
                Picture(plan.Width, plan.Height, scale, theme, $"folder-listedge-scrolled-{(dark ? "dark" : "light")}.png", dc =>
                {
                    // Only the content scrolls, under the list's own viewport.
                    PopupPaint.Clipped(dc, plan.Lists[0].Box, 0, () =>
                        Shifted(dc, 0, -scrolled, () =>
                        {
                            // The hovered card is the one the corner cuts, so the plate shows it.
                            PopupPaint.Fill(dc, plan.Cards[2], plan.Radius, PopupPaint.Ink(theme.SubtleHover));
                            Panel(dc, gfx, folder, plan, look, theme, scale);
                        }));
                });
                folder.Closed();
            }
        });
    }

    /// <summary>
    /// weather: §9's second conversion, stage 1. The weather panel's conditions as a line — the
    /// conditions now with its refresh and its age line — drawn by the very control that draws it
    /// in the panel, at the line's own width. The hours and the week were lines too until the
    /// person took them out as widgets (2026-09-26).
    /// <para>
    /// The arrangement is the panel's own (<see cref="PanelFolders.Weather"/>): a <b>list</b> folder
    /// whose height follows its content. What the sheet is for is the conditions side by side with
    /// the panel's own sheets.
    /// </para>
    /// </summary>
    [UiFact]
    public void WeatherComponentPictures()
    {
        OnStaThread(desktop: false, gfx => Forecasts.SavedWeather.AtAFixedTime(() =>
        {
            foreach (var (dark, stale) in new[] { (true, false), (false, false), (true, true) })
            {
                // The panel's own arrangement, as every weather sheet has it (PanelFolders). Each
                // card carries its own place (WeatherOptions on the row).
                var island = PanelFolders.Weather();
                island.Id = "wf";
                foreach (var cell in island.Folder!.Cells)
                {
                    cell.Island.Weather = Forecasts.SavedWeather.Options();
                }
                var config = Core.Configs.With(island);
                using var folder = new FolderIsland($"{Owner}/{island.Id}", island, gfx, config);
                // One source for every card, which is what the store does for them in earnest: one
                // place, one Entry, one fetch, one cache. Nothing is downloaded here.
                //
                // Stale is three of the place's own refreshes ago (WeatherAge.StaleAfterIntervals
                // at WeatherReading.Refresh, the online interval, 15 minutes here): the age line goes critical
                // and the trend arrow goes, because a reading nobody has refreshed is no reading of
                // "now".
                var fetched = Clock.Now().ToUniversalTime().AddMinutes(stale ? -200 : -15);
                var source = Forecasts.SavedWeather.Source(Forecasts.SavedWeather.Cache(fetched));
                foreach (var tile in folder.Tiles)
                {
                    if (tile.Island is Karpo.Dock.Weather.WeatherPartTile card)
                    {
                        card.Source = source;
                    }
                }
                folder.Opened(gfx);
                var look = LookFor(dark);
                var theme = FlyoutTheme.For(look);
                var scale = 1.5f;
                // A work area tall enough to hold the whole of it, so the sheet is the whole folder
                // and not the top of it. A folder is never more than 70 % of the work
                // area and shows no line past that.
                var plan = FolderFlyout.Build(look, scale, folder.View, folder.Options, folder.Tiles.Select(tile => tile.Cell).ToList(), 1600);
                Assert.Single(plan.Cards);
                Assert.Empty(plan.Hidden);
                // The width the panel it replaces was, which is the point of the list's own width.
                Assert.Equal(PopupPaint.Round(FolderListWidths.MediumDip * scale), plan.Width, 1);
                var name = stale ? "stale-" : "";
                Picture(plan.Width, plan.Height, scale, theme, $"weather-folder-{name}{(dark ? "dark" : "light")}.png",
                    dc => Panel(dc, gfx, folder, plan, look, theme, scale));
                folder.Closed();
            }
        }));
    }

    /// <summary>The folder's button: its icon, and a clock; no name beside either (§427).</summary>
    [UiFact]
    public void FolderButtonPictures()
    {
        OnStaThread(desktop: false, gfx =>
        {
            var config = new Config { Height = 48 };
            foreach (var dark in new[] { true, false })
            {
                var look = LookFor(dark);
                var style = new IslandStyle(1f, false, Layout.CellPx(96, config), Layout.IconPx(96, config), look);
                var theme = FlyoutTheme.For(look);
                foreach (var (name, button) in new (string Name, string Button)[] { ("icon", FolderFaces.IconId), ("clock", FolderFaces.ClockId) })
                {
                    var island = Button(button);
                    using var folder = new FolderIsland(Config.IslandKey(Owner, island.Id), island, gfx, config);
                    var shape = Shape(gfx, style, island);
                    var width = shape.Cells[0];
                    var height = style.Thickness;
                    Picture(width, height, 1f, theme, $"folder-button-{name}-{(dark ? "dark" : "light")}.png",
                        dc => folder.PaintCell(dc, 0, style, width, height));
                }
            }
        });
    }

    /// <summary>A list folder with two widgets in it whose button shows <paramref name="button"/>.</summary>
    private static BarItem Button(string button)
    {
        var island = Widget("glance", BarKind.FolderList);
        island.Folder = new FolderOptions { Face = FolderFace.Of(button) };
        island.Folder.Put(Widget("clock", ListKind.Clock));
        island.Folder.Put(Widget("battery", ListKind.Battery));
        island.Normalize();
        return island;
    }

    private static WidgetShape Shape(Gfx gfx, in IslandStyle style, BarItem folder) =>
        WidgetShapes.For(WidgetSource.Folder)!(new ShapeContext(folder, WidgetInfos.FolderList, style, gfx));

    /// <summary>The folder as it stands: its empty line, and its lines and their content.</summary>
    private static void Panel(ID2D1DeviceContext* dc, Gfx gfx, FolderIsland folder, FolderPlan plan, Look look, FlyoutTheme theme, float scale)
    {
        FolderFlyout.PaintEmpty(dc, gfx, plan, theme);
        for (var index = 0; index < plan.Cards.Count && index < folder.Tiles.Count; index++)
        {
            if (!plan.Shows(index))
            {
                // list: a widget a fixed height had no room for. FolderFlyout.DrawCard skips it,
                // so a sheet that drew it anyway would show three names stacked on one line — and
                // a picture of a folder nobody will see is worse than no picture.
                continue;
            }
            var card = plan.Cards[index];
            // A list has no plate at rest (FolderFlyout's own rule, ShowStates). This sheet used to
            // plate a list's lines and so showed a folder nobody would see.
            var box = PopupPaint.Rect(0, 0, card.right - card.left, card.bottom - card.top);
            var style = FolderFlyout.StyleFor(scale, look, theme);
            var tile = folder.Tiles[index];
            Shifted(dc, card.left, card.top, () =>
            {
                // The same branch FolderFlyout.DrawCard takes, and the same question
                // (Sections.CardInList): in a list a component draws its own card and everything
                // else draws the list's row of an icon and a name. This sheet once drew every list
                // row as a card, so a clock came out as a centred "14:40" rather than as the line it
                // really is; then it copied the old height-based test and drew the day switcher as
                // "Day switcher" with a glyph. A sheet that asks its own version of the product's
                // question will keep being wrong in a new way every time the question changes.
                if (tile.Cell.Island.List?.CardInList() != true)
                {
                    FolderFlyout.PaintListLine(dc, gfx, tile.Island, tile.Cell.Island, box, plan, theme, scale);
                }
                else
                {
                    FolderFlyout.PaintContent(dc, tile.Island, box, style);
                }
            });
        }
    }

    [UiFact]
    public void FolderRuns()
    {
        OnStaThread(desktop: true, gfx =>
        {
            var instance = (HINSTANCE)(void*)GetModuleHandleW(null);
            // The popups' class, registered once for every offscreen test; the messages are handed
            // to the flyout here, as the dock's window procedure hands them.
            FlyoutOffscreenTests.Register(instance);
            var work = new RECT { left = 0, top = 0, right = 1920, bottom = 1040 };
            var anchor = new POINT { x = 1700, y = 1040 };
            var config = new Config();
            // Only widgets that read: a click on a drive would open Explorer.
            var island = Readings();
            var folder = new FolderIsland($"{Owner}/{island.Id}", island, gfx, config);
            using var flyout = new FolderFlyout(gfx, instance, 0);
            foreach (var dark in new[] { true, false })
            {
                flyout.Show(gfx, LookFor(dark), 1.5f, anchor, work, Karpo.Core.Edge.Bottom, folder, FlyoutAlign.End);
                Assert.True(flyout.IsOpen);
                Assert.True(folder.Showing);
                Assert.Same(folder, flyout.Folder);
                Hover(flyout);
                Keys(flyout, [VK.VK_RIGHT, VK.VK_DOWN, VK.VK_LEFT, VK.VK_UP, VK.VK_TAB, VK.VK_END, VK.VK_HOME, VK.VK_NEXT, VK.VK_PRIOR]);
                Assert.True(flyout.IsOpen);
                Pump(flyout);
                flyout.Hide();
                Assert.False(flyout.IsOpen);
                Assert.False(folder.Showing);
            }

            // The folder rule (docs\widgets.md §15): a click on the battery in a folder opens nothing.
            // On the bar it opens the battery's flyout; here it would have been a popup opening out of
            // a popup, which is what the user asked never to happen. And the folder stays open — it
            // hides itself before it forwards anything, so a rule asked only downstream would have
            // shut it on the way to nothing. (The clock was this line until its click searched the
            // time, §377.)
            flyout.Show(gfx, LookFor(true), 1f, anchor, work, Karpo.Core.Edge.Bottom, folder, FlyoutAlign.End);
            var battery = Centre(flyout, 1);
            Assert.Equal(default, flyout.Message(WM.WM_LBUTTONDOWN, 1, battery));
            Assert.Equal(FolderFlyoutAction.None, flyout.Message(WM.WM_LBUTTONUP, 0, battery).Kind);
            Assert.True(flyout.IsOpen, "a refused click closed the folder on its way to nothing");

            // Enter on the focused line is its click, and is refused the same way: Home is the clock,
            // and the battery under it.
            Assert.Equal(default, flyout.Message(WM.WM_KEYDOWN, (WPARAM)(nuint)VK.VK_HOME, 0));
            Assert.Equal(default, flyout.Message(WM.WM_KEYDOWN, (WPARAM)(nuint)VK.VK_DOWN, 0));
            Assert.Equal(FolderFlyoutAction.None, flyout.Message(WM.WM_KEYDOWN, (WPARAM)(nuint)VK.VK_RETURN, 0).Kind);
            Assert.True(flyout.IsOpen);

            // The clock's click acts: the folder forwards it, for the dock to search the time (§377).
            var clock = Centre(flyout, 0);
            Assert.Equal(default, flyout.Message(WM.WM_LBUTTONDOWN, 1, clock));
            Assert.Equal(FolderFlyoutAction.Forward, flyout.Message(WM.WM_LBUTTONUP, 0, clock).Kind);
            flyout.Hide();

            // Escape closes it by itself and says so.
            flyout.Show(gfx, LookFor(true), 1f, anchor, work, Karpo.Core.Edge.Bottom, folder, FlyoutAlign.End);
            Assert.Equal(FolderFlyoutAction.Dismissed, flyout.Message(WM.WM_KEYDOWN, (WPARAM)(nuint)VK.VK_ESCAPE, 0).Kind);
            Assert.False(flyout.IsOpen);

            // The folder changes while open: a widget less; laid out again where it is. The panel
            // follows its list down its height, and holds its width, which is the list's own
            // setting and not its contents'.
            flyout.Show(gfx, LookFor(true), 1f, anchor, work, Karpo.Core.Edge.Bottom, folder, FlyoutAlign.End);
            var before = Size(flyout.Hwnd);
            var changed = Readings();
            changed.Folder!.Lists[0].RemoveAt(0);
            folder.Configure(changed, config);
            Pump(flyout);
            Assert.True(flyout.IsOpen);
            Assert.Equal(3, folder.Tiles.Count);
            var after = Size(flyout.Hwnd);
            Assert.True(after.Height < before.Height, $"a line fewer made the panel shorter: {before.Height} to {after.Height}");
            Assert.Equal(before.Width, after.Width);
            Hover(flyout);

            // edit: while the bar is being arranged the folder does not light-dismiss — that is what
            // lets several stand open at once and a widget be carried from one into another. Escape
            // still closes the one with the keys.
            flyout.Show(gfx, LookFor(true), 1f, anchor, work, Karpo.Core.Edge.Bottom, folder, FlyoutAlign.End, editing: true);
            Assert.True(flyout.IsOpen);
            // The one rule a press on the bar reads, whatever the press was on: empty space, another
            // widget, another folder's button. Its own button is no press on the bar's popups at
            // all, and goes its own way.
            Assert.True(flyout.StaysOpen);
            Assert.Equal(FolderFlyoutAction.None, flyout.Message(WM.WM_ACTIVATE, 0, 0).Kind);
            Assert.True(flyout.IsOpen);
            Assert.Equal(FolderFlyoutAction.None, flyout.Message(WM.WM_CANCELMODE, 0, 0).Kind);
            Assert.True(flyout.IsOpen);
            Assert.Equal(FolderFlyoutAction.Dismissed, flyout.Message(WM.WM_KEYDOWN, (WPARAM)(nuint)VK.VK_ESCAPE, 0).Kind);
            Assert.False(flyout.IsOpen);

            // Outside edit mode the same messages close it, as every other popup.
            flyout.Show(gfx, LookFor(true), 1f, anchor, work, Karpo.Core.Edge.Bottom, folder, FlyoutAlign.End);
            Assert.True(flyout.IsOpen);
            Assert.False(flyout.StaysOpen);
            Assert.Equal(FolderFlyoutAction.Dismissed, flyout.Message(WM.WM_ACTIVATE, 0, 0).Kind);
            Assert.False(flyout.IsOpen);

            // edit: arranged, the folder has no close control: its own button on the bar, Escape and
            // edit mode ending close it. Nor do the keys reach one: the last stop of the tab order
            // is a widget or a "+", and Enter on it opens its page or the add list, never closing
            // the folder.
            flyout.Show(gfx, LookFor(true), 1f, anchor, work, Karpo.Core.Edge.Bottom, folder, FlyoutAlign.End, editing: true);
            Assert.Equal(default, flyout.Message(WM.WM_KEYDOWN, (WPARAM)(nuint)VK.VK_END, 0));
            Assert.NotEqual(FolderFlyoutAction.Dismissed, flyout.Message(WM.WM_KEYDOWN, (WPARAM)(nuint)VK.VK_RETURN, 0).Kind);
            Assert.True(flyout.IsOpen);
            flyout.Hide();

            // edit: a widget being carried and let go beside the list, in the folder's window, keeps
            // the panel open.
            flyout.Show(gfx, LookFor(true), 1f, anchor, work, Karpo.Core.Edge.Bottom, folder, FlyoutAlign.End, editing: true);
            Assert.Equal(default, flyout.Message(WM.WM_LBUTTONDOWN, 1, Centre(flyout, 0)));
            Assert.Equal(0, flyout.Pressed);
            flyout.Message(WM.WM_LBUTTONUP, 0, Point(flyout, new Vector2(-4, 4)));
            Assert.True(flyout.IsOpen);
            flyout.Hide();

            // The folder leaves the bar while open: the flyout closes as a light dismiss would.
            flyout.Show(gfx, LookFor(true), 1f, anchor, work, Karpo.Core.Edge.Bottom, folder, FlyoutAlign.End);
            folder.Dispose();
            Assert.True(folder.Gone);
            var gone = Pump(flyout);
            Assert.Equal(FolderFlyoutAction.Dismissed, gone.Kind);
            Assert.False(flyout.IsOpen);
        });
    }

    /// <summary>
    /// The person: "when deleting a folder when it is open in edit mode it deletes it but its contents
    /// are still open and still show until i click away." Arranged, the folder holds itself open
    /// through presses elsewhere (StaysOpen), and the dismissal its leaving posted was held off by
    /// that too. A folder that left the bar closes at once, arranged or not, and its hold with it.
    /// </summary>
    [UiFact]
    public void AFolderRemovedWhileArrangedClosesAtOnce()
    {
        OnStaThread(desktop: true, gfx =>
        {
            var instance = (HINSTANCE)(void*)GetModuleHandleW(null);
            FlyoutOffscreenTests.Register(instance);
            var work = new RECT { left = 0, top = 0, right = 1920, bottom = 1040 };
            var anchor = new POINT { x = 1700, y = 1040 };
            var island = Folder();
            var folder = new FolderIsland($"{Owner}/{island.Id}", island, gfx, new Config());
            using var flyout = new FolderFlyout(gfx, instance, 0);
            flyout.Show(gfx, LookFor(true), 1f, anchor, work, Karpo.Core.Edge.Bottom, folder, FlyoutAlign.End, editing: true);
            Assert.True(flyout.IsOpen);
            Assert.True(flyout.StaysOpen);
            folder.Dispose();
            var gone = Pump(flyout);
            Assert.Equal(FolderFlyoutAction.Dismissed, gone.Kind);
            Assert.False(flyout.IsOpen);
            Assert.False(flyout.StaysOpen);
        });
    }

    /// <summary>
    /// A list folder follows what is in it at once with the pointer on it (the person: "it should
    /// always update the layout. not holding back when i am there with my mouse"): lines taken
    /// away make it shorter there and then, and the pointer leaving changes nothing. It used to
    /// hold the height it had until the pointer left. A press under way stays on the widget it
    /// went down on wherever the new layout puts it, and on nothing once that widget has gone.
    /// Headings only: a click on one does nothing, whatever a test gets wrong.
    /// </summary>
    [UiFact]
    public void AListFolderFollowsWhatIsInItWithThePointerOnIt()
    {
        OnStaThread(desktop: true, gfx =>
        {
            var instance = (HINSTANCE)(void*)GetModuleHandleW(null);
            FlyoutOffscreenTests.Register(instance);
            var work = new RECT { left = 0, top = 0, right = 1920, bottom = 1040 };
            var anchor = new POINT { x = 1700, y = 1040 };
            var config = new Config();
            BarItem Headings(params string[] names)
            {
                var island = Widget("titles", BarKind.FolderList);
                var options = new FolderOptions();
                for (var i = 0; i < names.Length; i++)
                {
                    options.Put(Widget(names[i], ListKind.Heading));
                }
                island.Folder = options;
                return island;
            }
            var folder = new FolderIsland($"{Owner}/titles", Headings("a", "b", "c", "d", "e", "f"), gfx, config);
            using var flyout = new FolderFlyout(gfx, instance, 0);
            flyout.Show(gfx, LookFor(true), 1f, anchor, work, Karpo.Core.Edge.Bottom, folder, FlyoutAlign.End);
            var six = Size(flyout.Hwnd);
            var widgets = folder.Tiles.Select(tile => tile.Island).ToList();

            // The pointer on the third line, and the button down on it.
            var third = Centre(flyout, 2);
            Assert.Equal(default, flyout.Message(WM.WM_MOUSEMOVE, 0, third));
            Assert.Equal(default, flyout.Message(WM.WM_LBUTTONDOWN, 1, third));
            Assert.Equal(2, flyout.Pressed);

            // The first and the last go while the pointer is there: shorter at once, and the press
            // is the third widget's still, which is the second line now.
            folder.Configure(Headings("b", "c", "d", "e"), config);
            Pump(flyout);
            var four = Size(flyout.Hwnd);
            Assert.True(four.Height < six.Height, $"four lines are {four.Height} px, six {six.Height}");
            Assert.Equal(4, folder.Tiles.Count);
            Assert.Same(widgets[2], folder.Tiles[1].Island);
            Assert.Equal(1, flyout.Pressed);

            // Its widget goes too: nothing is pressed, so the release clicks nothing.
            folder.Configure(Headings("b", "d"), config);
            Pump(flyout);
            Assert.Equal(-1, flyout.Pressed);
            Assert.Equal(FolderFlyoutAction.None, flyout.Message(WM.WM_LBUTTONUP, 0, third).Kind);
            var two = Size(flyout.Hwnd);
            Assert.True(two.Height < four.Height, $"two lines are {two.Height} px, four {four.Height}");

            // The pointer leaving changes nothing: nothing waited for it.
            Assert.Equal(default, flyout.Message(WM.WM_MOUSELEAVE, 0, 0));
            Pump(flyout);
            Assert.Equal(two, Size(flyout.Hwnd));
            Assert.True(flyout.IsOpen);
            flyout.Hide();
            folder.Dispose();
        });
    }

    /// <summary>The pointer over the whole panel, then off it.</summary>
    private static void Hover(FolderFlyout flyout)
    {
        var (width, height) = Size(flyout.Hwnd);
        for (var y = 0; y < height; y += 17)
        {
            for (var x = 0; x < width; x += 23)
            {
                Assert.Equal(default, flyout.Message(WM.WM_MOUSEMOVE, 0, MakePoint(x, y)));
            }
        }
        Assert.Equal(default, flyout.Message(WM.WM_MOUSELEAVE, 0, 0));
        foreach (var delta in new[] { -120, -240, 360 })
        {
            Assert.Equal(default, flyout.Message(WM.WM_MOUSEWHEEL, (WPARAM)(nuint)(uint)(delta << 16), 0));
        }
    }

    private static void Keys(FolderFlyout flyout, int[] keys)
    {
        foreach (var key in keys)
        {
            Assert.Equal(default, flyout.Message(WM.WM_KEYDOWN, (WPARAM)(nuint)key, 0));
        }
    }

    /// <summary>Hands the flyout what was posted to its window (its timer, a dismissal), as the dock does; the last event.</summary>
    private static FolderFlyoutEvent Pump(FolderFlyout flyout)
    {
        var last = default(FolderFlyoutEvent);
        MSG message;
        while (PeekMessageW(&message, HWND.NULL, 0, 0, PM.PM_REMOVE))
        {
            if (message.hwnd == flyout.Hwnd && message.message != WM.WM_PAINT)
            {
                var happened = flyout.Message(message.message, message.wParam, message.lParam);
                if (happened.Kind != FolderFlyoutAction.None)
                {
                    last = happened;
                }
            }
            _ = TranslateMessage(&message);
            _ = DispatchMessageW(&message);
        }
        return last;
    }

    /// <summary>The flyout's size in px: its box, the window it had before §355.</summary>
    private static (int Width, int Height) Size(HWND window)
    {
        var bounds = Karpo.Tests.Edit.WindowShots.Extent(window);
        return (bounds.right - bounds.left, bounds.bottom - bounds.top);
    }

    /// <summary>
    /// Line <paramref name="index"/>'s middle as a mouse message's lParam, where the open flyout
    /// draws it (<see cref="FolderFlyout.ScreenOfCard"/>).
    /// </summary>
    private static nint Centre(FolderFlyout flyout, int index) => Client(flyout, flyout.ScreenOfCard(index));

    /// <summary>
    /// A point of the panel (its px) in the window's client px, as an lParam, from where the open
    /// panel stands (<see cref="FolderFlyout.PanelOnScreen"/>). Not from a plan of the test's own:
    /// arranged, the window reaches the screen's edges for its guide lines and slots, so the panel
    /// is no longer centred in it, and a point worked out as if it were lands in a slot's room.
    /// </summary>
    private static nint Point(FolderFlyout flyout, Vector2 panel)
    {
        var on = flyout.PanelOnScreen;
        return Client(flyout, new POINT { x = on.left + (int)panel.X, y = on.top + (int)panel.Y });
    }

    private static nint Client(FolderFlyout flyout, POINT screen)
    {
        Assert.True(ScreenToClient(flyout.Hwnd, &screen));
        return MakePoint(screen.x, screen.y);
    }

    private static nint MakePoint(int x, int y) => (nint)(((y & 0xFFFF) << 16) | (x & 0xFFFF));

    // --- Pictures ------------------------------------------------------------------

    private delegate void Painter(ID2D1DeviceContext* dc);

    /// <summary>The panel on a neutral backdrop, with its chrome (solid base, stroke, radius 8), then the content.</summary>
    private static void Picture(float width, float height, float scale, FlyoutTheme theme, string file, Painter content)
    {
        const int margin = 16;
        var w = (uint)MathF.Ceiling(width) + 2 * margin;
        var h = (uint)MathF.Ceiling(height) + 2 * margin;
        ID2D1Factory1* factory = null;
        IWICImagingFactory* wic = null;
        IWICBitmap* bitmap = null;
        ID2D1RenderTarget* target = null;
        ID2D1DeviceContext* dc = null;
        try
        {
            ComUtil.Check(D2D1CreateFactory(D2D1_FACTORY_TYPE.D2D1_FACTORY_TYPE_SINGLE_THREADED, __uuidof<ID2D1Factory1>(), (void**)&factory));
            var clsid = CLSID.CLSID_WICImagingFactory2;
            ComUtil.Check(CoCreateInstance(&clsid, null, (uint)CLSCTX.CLSCTX_INPROC_SERVER, __uuidof<IWICImagingFactory>(), (void**)&wic));
            var format = GUID.GUID_WICPixelFormat32bppPBGRA;
            ComUtil.Check(wic->CreateBitmap(w, h, &format, WICBitmapCreateCacheOption.WICBitmapCacheOnLoad, &bitmap));
            var properties = new D2D1_RENDER_TARGET_PROPERTIES
            {
                type = D2D1_RENDER_TARGET_TYPE.D2D1_RENDER_TARGET_TYPE_SOFTWARE,
                pixelFormat = new D2D1_PIXEL_FORMAT { format = DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM, alphaMode = D2D1_ALPHA_MODE.D2D1_ALPHA_MODE_PREMULTIPLIED },
                dpiX = 96,
                dpiY = 96,
            };
            ComUtil.Check(((ID2D1Factory*)factory)->CreateWicBitmapRenderTarget(bitmap, &properties, &target));
            ComUtil.Check(target->QueryInterface(__uuidof<ID2D1DeviceContext>(), (void**)&dc));
            dc->BeginDraw();
            var backdrop = theme.Dark ? new DXGI_RGBA(0.20f, 0.24f, 0.30f, 1) : new DXGI_RGBA(0.72f, 0.78f, 0.86f, 1);
            dc->Clear(&backdrop);
            var panel = new D2D_RECT_F(margin, margin, margin + width, margin + height);
            PopupPaint.Fill(dc, panel, 8 * scale, PopupPaint.Ink(theme.Base));
            PopupPaint.Stroke(dc, panel, 8 * scale, MathF.Max(1, MathF.Round(scale)), PopupPaint.Ink(theme.Stroke));
            var context = dc;
            // Clipped to the panel's rounded shape, as the real one is: FlyoutHost gives the
            // panel's body a geometric clip of its own rounded rectangle, and everything a folder
            // draws is inside it. The sheet drew into a plain bitmap and so showed square corners
            // where the product shows the panel's curve — which mattered the moment a list's cards
            // reached the border, because whether a card flush with the top is cut or rounded is
            // the whole question. A sheet that cannot answer it is a sheet that invites a guess.
            PopupPaint.Clipped(dc, panel, 8 * scale, () => Shifted(context, margin, margin, () => content(context)));
            ComUtil.Check(dc->EndDraw(null, null));
            Save(wic, bitmap, Path.Combine(Pictures, file));
        }
        finally
        {
            ComUtil.Release(ref dc);
            ComUtil.Release(ref target);
            ComUtil.Release(ref bitmap);
            ComUtil.Release(ref wic);
            ComUtil.Release(ref factory);
        }
    }

    private static void Shifted(ID2D1DeviceContext* dc, float x, float y, Action paint)
    {
        D2D_MATRIX_3X2_F before;
        dc->GetTransform(&before);
        var moved = before;
        moved._31 = x * before._11 + y * before._21 + before._31;
        moved._32 = x * before._12 + y * before._22 + before._32;
        dc->SetTransform(&moved);
        try
        {
            paint();
        }
        finally
        {
            dc->SetTransform(&before);
        }
    }

    private static void Save(IWICImagingFactory* wic, IWICBitmap* bitmap, string path)
    {
        IWICStream* stream = null;
        IWICBitmapEncoder* encoder = null;
        IWICBitmapFrameEncode* frame = null;
        try
        {
            ComUtil.Check(wic->CreateStream(&stream));
            fixed (char* name = path)
            {
                ComUtil.Check(stream->InitializeFromFilename(name, GENERIC_WRITE));
            }
            var png = GUID.GUID_ContainerFormatPng;
            ComUtil.Check(wic->CreateEncoder(&png, null, &encoder));
            ComUtil.Check(encoder->Initialize((IStream*)stream, WICBitmapEncoderCacheOption.WICBitmapEncoderNoCache));
            ComUtil.Check(encoder->CreateNewFrame(&frame, null));
            ComUtil.Check(frame->Initialize(null));
            uint width;
            uint height;
            ComUtil.Check(bitmap->GetSize(&width, &height));
            ComUtil.Check(frame->SetSize(width, height));
            var format = GUID.GUID_WICPixelFormat32bppPBGRA;
            ComUtil.Check(frame->SetPixelFormat(&format));
            ComUtil.Check(frame->WriteSource((IWICBitmapSource*)bitmap, null));
            ComUtil.Check(frame->Commit());
            ComUtil.Check(encoder->Commit());
        }
        finally
        {
            ComUtil.Release(ref frame);
            ComUtil.Release(ref encoder);
            ComUtil.Release(ref stream);
        }
    }

    // --- The thread both run on ----------------------------------------------------

    /// <summary>
    /// Runs <paramref name="body"/> on a new thread made an STA by its dispatcher queue (the compositor needs
    /// one) and a Gfx; with <paramref name="desktop"/>, on a desktop of its own that nobody sees.
    /// </summary>
    private static void OnStaThread(bool desktop, Action<Gfx> body)
    {
        Exception? failed = null;
        HDESK hidden = default;
        var thread = new Thread(() =>
        {
            try
            {
                if (desktop)
                {
                    fixed (char* name = "karpo-folders-test-" + Guid.NewGuid().ToString("N"))
                    {
                        hidden = CreateDesktopW(name, null, null, 0, 0x10000000u /* GENERIC_ALL */, null);
                    }
                    Assert.True(hidden != HDESK.NULL, "no hidden desktop");
                    // Before this thread owns any window: its windows are then made there.
                    Assert.True(SetThreadDesktop(hidden));
                }
                var options = new DispatcherQueueOptions
                {
                    dwSize = (uint)sizeof(DispatcherQueueOptions),
                    threadType = DISPATCHERQUEUE_THREAD_TYPE.DQTYPE_THREAD_CURRENT,
                    apartmentType = DISPATCHERQUEUE_THREAD_APARTMENTTYPE.DQTAT_COM_STA,
                };
                IDispatcherQueueController* controller = null;
                ComUtil.Check(CreateDispatcherQueueController(options, &controller));
                // calendar: a folder holds a clock, which read this PC's clock while the sheet was
                // drawn, so the folder sheets came out different every run. Frozen on
                // this thread only (Clock.Now is [ThreadStatic]); nothing here reads the real time.
                Clock.Now = () => new DateTime(2026, 9, 22, 14, 40, 7, DateTimeKind.Local);
                try
                {
                    using var gfx = new Gfx(gpu: false);
                    body(gfx);
                }
                finally
                {
                    controller->Release();
                }
            }
            catch (Exception error)
            {
                failed = error;
            }
        });
        thread.Start();
        thread.Join();
        HiddenDesktop.Close(hidden);
        if (failed is not null)
        {
            throw new Xunit.Sdk.XunitException("On the offscreen thread: " + failed);
        }
    }
}
