// folders: a widget carried from the bar over the open folder, asked of a real folder window on a
// hidden desktop (FolderFlyout.Offer). A taskbar entry may not stand in a folder: nothing moves
// between the taskbar and a folder (the person, 2026-10-09), so nothing lights, and the folder has the
// sentence ready that says why when it is let go there. One folder stands open at a time, so nothing
// is carried in from another.
//
// And the bar's own carry, from what the bar asks of the folder standing open as it carries a block
// (Dock.Offered): over its panel it says why it won't take it, elsewhere it shows nothing, and the
// bar is left as it was. Grid folders, and with them every drop of a bar's widget into a folder, are
// gone (2026-10-09).
//
// Like the other offscreen tests these need a window station and Direct2D, so they run only with
// KARPO_UI_TESTS=1.

using System.Text.Json;
using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Dock.Native;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
using Color = Windows.UI.Color;
using IDispatcherQueueController = TerraFX.Interop.WinRT.IDispatcherQueueController;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Folders;

public sealed unsafe class FolderOfferTests
{
    private const string Owner = "layout:default";
    private const float Scale = 1f;
    private static readonly RECT Work = new() { left = 0, top = 0, right = 1920, bottom = 1040 };
    private static readonly POINT Anchor = new() { x = 1700, y = 1040 };

    private static readonly Look Dark = new(
        Glass: false,
        Dark: true,
        Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4),
        AccentLight2: Color.FromArgb(255, 0x99, 0xEB, 0xFF),
        AccentDark1: Color.FromArgb(255, 0x00, 0x5F, 0xB8));

    /// <summary>A list folder of one line: its panel what takes a carried widget (an empty one has none arranged, §297).</summary>
    private static BarItem Folder()
    {
        var island = BarKind.FolderList.New();
        island.Id = "box";
        island.Folder.Put(ListKind.AirplaneMode.New(), 0);
        return island;
    }

    /// <summary>
    /// The screen point of the middle of the list's first place (panel px from the plan the folder
    /// shows: its first line, or an empty list's room, which arranged has no panel), from where the
    /// panel stands on the screen: arranged, its window reaches the work area's edges.
    /// </summary>
    private static POINT OverFirstPlace(FolderFlyout flyout)
    {
        var plan = flyout.Plan!;
        var place = plan.Cards.Count > 0 ? plan.Cards[0] : plan.Lists[0].Box;
        var on = flyout.PanelOnScreen;
        return new POINT
        {
            x = on.left + (int)MathF.Round((place.left + place.right) / 2),
            y = on.top + (int)MathF.Round((place.top + place.bottom) / 2),
        };
    }

    /// <summary>
    /// A taskbar entry, the keyboard layout or the weather, carried over the folder lights nothing
    /// — the folder offers no place — and the folder has the sentence ready for when it is let go
    /// there; carried away again, there is nothing to say.
    /// </summary>
    [UiFact]
    public void ATaskbarEntryLightsNothingAndTheFolderSaysWhy()
    {
        OnHiddenDesktop((gfx, instance) =>
        {
            var folder = new FolderIsland($"{Owner}/box", Folder(), gfx, new Config());
            using var flyout = new FolderFlyout(gfx, instance, 0);
            flyout.Show(gfx, Dark, Scale, Anchor, Work, Edge.Bottom, folder, FlyoutAlign.End, editing: true);
            Assert.True(flyout.IsOpen);
            var over = OverFirstPlace(flyout);

            Assert.True(flyout.Offer(over, BarKind.NightLight.New()), "the keyboard is over the panel");
            Assert.Equal("Night light can't be moved from the taskbar into a folder. Moving widgets between the taskbar and folders isn't supported.", flyout.Refusal);
            Assert.Equal("Night light can't be moved from the taskbar into a folder. Moving widgets between the taskbar and folders isn't supported.", flyout.Said);
            // The folder has no title row to say it on: it stands in a tip over the folder.
            Assert.Equal(flyout.Said, flyout.NoteShown);

            var weather = BarKind.WidgetsBoard.New();
            Assert.True(flyout.Offer(over, weather));
            Assert.Equal(Partners.WhyNot(weather, WidgetType.List), flyout.Refusal);

            // Carried away again: nothing is offered, and there is nothing to say.
            flyout.NoOffer();
            Assert.Equal("", flyout.Refusal);
            Assert.Equal("", flyout.Said);
            Assert.Equal("", flyout.NoteShown);

            // The dock's own note (a widget let go where it may not stand) stands a moment, and
            // goes with the folder.
            flyout.Say("No room");
            Assert.Equal("No room", flyout.NoteShown);
            flyout.Hide();
            Assert.Equal("", flyout.NoteShown);
        });
    }

    // --- The bar's own carry -------------------------------------------------------------------

    /// <summary>
    /// A bar of one layout: a calendar, a battery, the keyboard layout and the bell, then two list
    /// folders, one with a clock's line and one with a battery's.
    /// </summary>
    private static List<BarItem> Bar()
    {
        return [Entry(BarKind.NightLight, "c"), Entry(BarKind.EnergySaver, "b"), Entry(BarKind.NightLight, "k"), Entry(BarKind.DoNotDisturb, "n"),
            List("l", "List", ListKind.Clock, "in-list"), List("m", "More", ListKind.Battery, "in-more")];

        static BarItem List(string id, string name, ListKind kind, string lineId)
        {
            var list = BarKind.FolderList.New();
            list.Id = id;
            var line = kind.New();
            line.Id = lineId;
            list.Folder.Put(line);
            return list;
        }
    }

    private static BarItem Entry(BarKind kind, string id)
    {
        var entry = kind.New();
        entry.Id = id;
        return entry;
    }

    private static BarItem Of(List<BarItem> bar, string id) => bar.Single(entry => entry.Id == id);

    /// <summary>The bar as the file writes it: what a refused carry has to leave exactly as it was.</summary>
    private static string File(List<BarItem> bar) =>
        string.Join("\n", bar.Select(entry => JsonSerializer.Serialize(entry, ConfigJson.Default.BarItem)));

    /// <summary>
    /// A widget carried from the bar over the open folder is refused there: it lights nothing and
    /// says why in the sentence other refusals say (Partners.WhyNot), and the bar is left exactly as
    /// it was. Away from the folder it offers nothing.
    /// </summary>
    [UiFact]
    public void AWidgetFromTheBarGoesIntoNoFolderAndTheFolderSaysWhy()
    {
        OnHiddenDesktop((gfx, instance) =>
        {
            var bar = Bar();
            var before = File(bar);
            using var flyout = new FolderFlyout(gfx, instance, 0);
            flyout.Show(gfx, Dark, Scale, Anchor, Work, Edge.Bottom, new FolderIsland($"{Owner}/l", Of(bar, "l"), gfx, new Config()), FlyoutAlign.End, editing: true);
            Assert.True(flyout.IsOpen);
            FolderFlyout[] open = [flyout];

            Assert.Equal(0, TheDock.Offered(open, flyout.ScreenOfCard(0), Of(bar, "c")));
            Assert.Equal(Partners.WhyNot(Of(bar, "c"), WidgetType.List), flyout.Refusal);
            Assert.Equal(before, File(bar));

            Assert.Equal(0, TheDock.Offered(open, flyout.ScreenOfCard(0), Of(bar, "n")));
            Assert.Equal(Partners.WhyNot(Of(bar, "n"), WidgetType.List), flyout.Refusal);
            Assert.Equal(before, File(bar));

            Assert.Equal(-1, TheDock.Offered(open, new POINT { x = 960, y = 4 }, Of(bar, "c")));
            Assert.Equal("", flyout.Refusal);
        });
    }

    /// <summary>
    /// The folder has a widget carried from the bar over its panel, and not over the room its window
    /// keeps around the panel for the shadow, which on the bar's side reaches down to the bar. There
    /// the bar has it still (Dock.EditView, BarCarry): the open folder offers nothing and says
    /// nothing, and the bar goes on showing where along it the widget would land, as the tray's
    /// overflow counts only its panel too (Flyout.OnPanel).
    /// </summary>
    [UiFact]
    public void OnlyThePanelTakesAWidgetCarriedFromTheBarNotItsShadow()
    {
        OnHiddenDesktop((gfx, instance) =>
        {
            var folder = new FolderIsland($"{Owner}/box", Folder(), gfx, new Config());
            using var flyout = new FolderFlyout(gfx, instance, 0);
            flyout.Show(gfx, Dark, Scale, Anchor, Work, Edge.Bottom, folder, FlyoutAlign.End, editing: true);
            Assert.True(flyout.IsOpen);
            var plan = flyout.Plan!;
            var on = flyout.PanelOnScreen;
            FolderFlyout[] open = [flyout];
            var battery = BarKind.EnergySaver.New();
            var why = Partners.WhyNot(battery, WidgetType.List);

            // Just inside the panel, half way across: over the folder, which says why it won't take it.
            var middle = on.left + (int)MathF.Round(plan.Width / 2);
            var onPanel = new POINT { x = middle, y = on.top + 2 };
            Assert.Equal(0, TheDock.Offered(open, onPanel, battery));
            Assert.Equal(why, flyout.Refusal);

            // The room round it: above the panel, and below it between the panel and the bar,
            // where the window reaches down to the anchor. Still the folder's window, but not
            // the folder — the bar's.
            RECT window;
            window = WindowShots.Extent(flyout.Hwnd);
            List<POINT> shadow = [new POINT { x = middle, y = on.top - 4 }];
            if (window.bottom - 1 > on.bottom)
            {
                shadow.Add(new POINT { x = middle, y = window.bottom - 1 });
            }
            foreach (var inShadow in shadow)
            {
                Assert.True(inShadow.y >= window.top && inShadow.y < window.bottom, "the point is inside the folder's window");
                Assert.Equal(-1, TheDock.Offered(open, inShadow, battery));
                Assert.Equal("", flyout.Refusal);

                // And onto the panel again: the folder has it once more.
                Assert.Equal(0, TheDock.Offered(open, onPanel, battery));
                Assert.Equal(why, flyout.Refusal);
            }
            flyout.NoOffer();
        });
    }

    private static void OnHiddenDesktop(Action<Gfx, HINSTANCE> body)
    {
        Exception? failed = null;
        HDESK hidden = default;
        var thread = new Thread(() =>
        {
            try
            {
                fixed (char* name = "karpo-offer-test-" + Guid.NewGuid().ToString("N"))
                {
                    hidden = CreateDesktopW(name, null, null, 0, 0x10000000u /* GENERIC_ALL */, null);
                }
                Assert.True(hidden != HDESK.NULL, "no hidden desktop");
                // Before this thread owns any window: its windows are then made there.
                Assert.True(SetThreadDesktop(hidden));
                var options = new DispatcherQueueOptions
                {
                    dwSize = (uint)sizeof(DispatcherQueueOptions),
                    threadType = DISPATCHERQUEUE_THREAD_TYPE.DQTYPE_THREAD_CURRENT,
                    apartmentType = DISPATCHERQUEUE_THREAD_APARTMENTTYPE.DQTAT_COM_STA,
                };
                IDispatcherQueueController* controller = null;
                ComUtil.Check(CreateDispatcherQueueController(options, &controller));
                try
                {
                    using var gfx = new Gfx(gpu: false);
                    var instance = (HINSTANCE)(void*)GetModuleHandleW(null);
                    FlyoutOffscreenTests.Register(instance);
                    body(gfx, instance);
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
