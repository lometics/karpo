// edit: a folder line's height, set from its own menu (the person, 2026-10-12: "the additional menu
// options that appear when i click a widget in the folder should be moved to the context menu, the
// toggle switch and the height settings. when you change the height it should also not close the
// context menu"; Karpo.Dock\LineHeightRows.cs, Flyout.Controls.cs). Held here:
//
//  - **Two rows in the menu itself** (§411, the menu rules; §240 to §408 one row opening a level of radio
//    buttons): "Height", a choice saying its mode at its end and opening "Automatic", "Fixed", "Fill" (a kind
//    of a fixed content no Automatic, §140), a pick going back; and the rows' stepper, "Rows − 3 rows +",
//    "Max rows" where they cap an automatic height; §660 under Fill no stepper, Fill ignoring the rows; a
//    heading, a divider, a list row, a kind of one height and a folder none.
//  - **What a pick or a step asks for.** Steps inside the kind's limits, greyed at them, none under
//    Fill; the mode picked; nothing past a limit, for the mode it has or for a mode the kind has not.
//  - **The menu shown again** with the rows as they are now, the others as they were.
//  - **On a real menu.** A press on the stepper's "+" or on a mode's row is an Adjust and the menu
//    stays open; Left and Right step from the keys; Refresh shows the new value in place, a mode picked
//    back on the menu's own level. Pictures: line-height-menu-<look>-<scale>.png in KARPO_PNG_DIR, the
//    menu's foot, and the modes' level open, Automatic and Fixed.
//
// Needs Direct2D and a window station for the [UiFact]: KARPO_UI_TESTS=1.

using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Folders;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Menus;

public sealed class LineHeightRowsTests
{
    private static BarItem Folder()
    {
        var island = BarKind.FolderList.New();
        island.Id = "box";
        var heading = ListKind.Heading.New();
        (heading.Heading ??= new HeadingOptions()).Text = "Today";
        Put(island, heading, "heading");
        // A kind of a fixed content (§140): a month; the clock is its blocks' height now (§401).
        Put(island, ListKind.Month.New(), "clock");
        Put(island, ListKind.Calendar.New(), "calendar");
        return island;
    }

    private static WidgetItem Line(BarItem folder, string id) =>
        Karpo.Core.Folders.Find([folder], $"{folder.Id}/{id}") ?? throw new Xunit.Sdk.XunitException($"no line {id}");

    private static void Put(BarItem folder, CellItem widget, string id)
    {
        widget.Id = id;
        widget.View = Karpo.Core.Folders.View(folder.Kind);
        folder.Folder!.Put(widget);
    }

    /// <summary>
    /// §411: the height is two rows of the line's menu's foot, ruled off: "Height", a choice saying its mode and
    /// opening the modes, ticked, a pick going back; and "Rows", the stepper of its rows with their unit — "Max
    /// rows" under Automatic, which the rows cap. No radio buttons.
    /// </summary>
    [Fact]
    public void TheHeightIsAChoiceAndAStepperInTheMenuItself()
    {
        var choice = LineHeightRows.For(Line(Folder(), "calendar"))!.Value with { Now = LineHeight.Automatic(12) };
        Assert.True(choice.Modes);
        var foot = OptionMenu.Foot([.. LineHeightRows.Rows(choice)]);
        Assert.Equal(3, foot.Count);
        Assert.True(foot[0].Separator);
        var height = foot[1];
        Assert.Equal((LineHeightRows.HeightId, S.DockEditHeightTitle, "Automatic", true, true), (height.Id, height.Text, height.Value!.Text, height.PickOne, height.Foot));
        var modes = height.Children!;
        Assert.Equal([LineHeightRows.AutomaticId, LineHeightRows.FixedId, LineHeightRows.FillId], modes.Select(row => row.Id));
        Assert.Equal(["Automatic", "Fixed", "Fill"], modes.Select(row => row.Text));
        Assert.Equal([true, false, false], modes.Select(row => row.Checked));
        Assert.All(modes, row => Assert.True(row.Stays && row.Control is null));
        var rows = foot[2];
        var stepper = Assert.IsType<FlyoutControl.Stepper>(rows.Control);
        Assert.Equal((LineHeightRows.FewerId, "Max rows", "6 rows", true), (rows.Id, rows.Text, stepper.Value, rows.Foot));
        Assert.Equal((LineHeightRows.FewerId, LineHeightRows.MoreId), (stepper.Fewer, stepper.More));

        // Fixed: the rows are the height; the row says so.
        var fixedRows = LineHeightRows.Rows(choice with { Now = LineHeight.Exactly(6) });
        Assert.Equal(("Fixed", "Rows", "3 rows"), (fixedRows[0].Value!.Text, fixedRows[1].Text, ((FlyoutControl.Stepper)fixedRows[1].Control!).Value));
        Assert.Equal("1 row", ((FlyoutControl.Stepper)LineHeightRows.Rows(choice with { Now = LineHeight.Exactly(2) })[1].Control!).Value);
        Assert.Equal(S.SettingsWidgetSizeModeFill, LineHeightRows.Said(LineHeight.Exactly(8).As(LineHeightMode.Fill)));
    }

    [Fact]
    public void AKindOfAFixedContentHasFixedAndFillAndTheStepperGreyedAtItsLimits()
    {
        var clock = LineHeightRows.For(Line(Folder(), "clock"), room: 6)!.Value;
        // §140: a month is Fixed only, 1 to the room's rows (§192: no most of its own); it may fill too (§168).
        Assert.False(clock.Modes);
        Assert.Equal((2, 6), (clock.Least, clock.Most));
        Assert.Equal((2, LineHeight.Most), LineHeightRows.For(Line(Folder(), "clock")) is { } any ? (any.Least, any.Most) : default);
        var rows = LineHeightRows.Rows(clock with { Now = LineHeight.Exactly(12) });
        Assert.Equal([LineHeightRows.FixedId, LineHeightRows.FillId], rows[0].Children!.Select(row => row.Id));
        Assert.Equal(true, rows[0].Children![0].Checked);
        var stepper = Assert.IsType<FlyoutControl.Stepper>(rows[1].Control);
        Assert.True(stepper.CanFewer);
        Assert.False(stepper.CanMore);
    }

    /// <summary>
    /// §660 (the person, 2026-11-10: "when fill is selected as height there should not be a height property anymore as it
    /// is ignored"): the rows' stepper only for the modes that use the rows — Automatic ("Max rows") and Fixed ("Rows") —,
    /// none under Fill, its Height row alone, ticked; a step asks for nothing. The rows are kept under Fill: Automatic or
    /// Fixed picked again finds them, the stepper back at them, for a kind of either mode and for one of Fixed alone.
    /// </summary>
    [Fact]
    public void UnderFillThereIsNoStepperAndTheRowsComeBack()
    {
        var calendar = LineHeightRows.For(Line(Folder(), "calendar"))!.Value with { Now = LineHeight.Automatic(12).As(LineHeightMode.Fill) };
        var rows = LineHeightRows.Rows(calendar);
        Assert.Equal([LineHeightRows.HeightId], rows.Select(row => row.Id));
        Assert.Equal([false, false, true], rows[0].Children!.Select(row => row.Checked));
        Assert.Equal("Fill", rows[0].Value!.Text);
        // The modes that use the rows have the stepper.
        Assert.Equal([LineHeightRows.HeightId, LineHeightRows.FewerId], LineHeightRows.Rows(calendar with { Now = LineHeight.Automatic(12) }).Select(row => row.Id));
        Assert.Equal([LineHeightRows.HeightId, LineHeightRows.FewerId], LineHeightRows.Rows(calendar with { Now = LineHeight.Exactly(12) }).Select(row => row.Id));
        var clock = LineHeightRows.For(Line(Folder(), "clock"))!.Value;
        Assert.Equal([LineHeightRows.HeightId], LineHeightRows.Rows(clock with { Now = LineHeight.Exactly(9).As(LineHeightMode.Fill) }).Select(row => row.Id));
        Assert.Equal([LineHeightRows.HeightId, LineHeightRows.FewerId], LineHeightRows.Rows(clock with { Now = LineHeight.Exactly(9) }).Select(row => row.Id));
        // A round trip through Fill: the rows as they were, the stepper showing them.
        foreach (var (start, back) in new[] { (LineHeight.Automatic(13), LineHeightRows.AutomaticId), (LineHeight.Exactly(13), LineHeightRows.FixedId) })
        {
            var fill = LineHeightRows.Act(calendar with { Now = start }, LineHeightRows.FillId)!.Value;
            Assert.Single(LineHeightRows.Rows(calendar with { Now = fill }));
            var again = LineHeightRows.Act(calendar with { Now = fill }, back);
            Assert.Equal(start, again);
            Assert.Equal("6½ rows", Assert.IsType<FlyoutControl.Stepper>(LineHeightRows.Rows(calendar with { Now = again!.Value })[1].Control).Value);
        }
        Assert.Null(LineHeightRows.Act(calendar, LineHeightRows.MoreId));
        Assert.Null(LineHeightRows.Act(calendar, LineHeightRows.FewerId));
        Assert.Null(LineHeightRows.Act(calendar, LineHeightRows.FillId));
        Assert.Equal(LineHeight.Automatic(12), LineHeightRows.Act(calendar, LineHeightRows.AutomaticId));
        Assert.Equal(LineHeight.Exactly(12), LineHeightRows.Act(calendar, LineHeightRows.FixedId));
    }

    [Fact]
    public void ALineWithNoHeightToSetHasNoRows()
    {
        var folder = Folder();
        Assert.Null(LineHeightRows.For(Line(folder, "heading")));
        Assert.Null(LineHeightRows.For(ListKind.Divider.New()));
        Assert.Null(LineHeightRows.For(ListKind.TaskView.New()));
        Assert.Null(LineHeightRows.For(ListKind.Actions.New()));
        Assert.Null(LineHeightRows.For(folder));
        Assert.Null(LineHeightRows.For(null));
    }

    /// <summary>
    /// §432: a kind of one form has no Height and no Size; a kind of a few has "Size" where Height stands — a
    /// choice saying its size and opening its kind's sizes, each its own glyph, ticked, a pick going back — and no
    /// rows to step. A pick sets the size; the one it has, or one the kind has not, asks for nothing.
    /// </summary>
    [Fact]
    public void ASizedLineHasASizeAndNoHeight()
    {
        foreach (var kind in Karpo.Tests.Core.LineSizeTests.OneForm)
        {
            var line = kind.New();
            line.Normalize();
            Assert.Null(LineHeightRows.For(line));
            Assert.Null(LineHeightRows.SizeOf(line));
            Assert.Empty(LineHeightRows.SizeRows(line));
        }
        var media = ListKind.Media.New();
        media.Normalize();
        Assert.Null(LineHeightRows.For(media));
        var rows = LineHeightRows.SizeRows(media);
        var size = Assert.Single(rows);
        Assert.Equal((LineHeightRows.SizeId, "Size", "Medium", true, true), (size.Id, size.Text, size.Value!.Text, size.PickOne, size.Foot));
        Assert.Equal([LineHeightRows.CompactId, LineHeightRows.MediumId, LineHeightRows.FullId], size.Children!.Select(row => row.Id));
        Assert.Equal(["Compact", "Medium", "Full"], size.Children!.Select(row => row.Text));
        Assert.Equal([false, true, false], size.Children!.Select(row => row.Checked));
        Assert.Equal(3, size.Children!.Select(row => row.Icon).Distinct().Count());
        Assert.All(size.Children!, row => Assert.True(row.Stays && row.Control is null));

        Assert.Equal(LineSize.Full, LineHeightRows.ActSize(media, LineHeightRows.FullId));
        Assert.Null(LineHeightRows.ActSize(media, LineHeightRows.MediumId));
        Assert.Null(LineHeightRows.ActSize(media, LineHeightRows.FewerId));
        Assert.True(LineHeightRows.Resize(media, LineSize.Compact));
        Assert.Equal(LineSize.Compact, media.Size);
        Assert.False(LineHeightRows.Resize(media, LineSize.Compact));
        List<FlyoutItem> items = [new(1, "Open"), .. OptionMenu.Foot([.. LineHeightRows.SizeRows(ListKind.Media.New())])];
        var now = LineHeightRows.UpdatedSize(items, media);
        Assert.Equal(items.Select(item => item.Id), now.Select(item => item.Id));
        Assert.Equal("Compact", now[^1].Value!.Text);

        // The alarm has no middle form: Compact and Full, and Medium asks for nothing.
        var alarm = ListKind.Alarm.New();
        alarm.Normalize();
        Assert.Equal([LineHeightRows.CompactId, LineHeightRows.FullId], LineHeightRows.SizeRows(alarm)[0].Children!.Select(row => row.Id));
        Assert.Null(LineHeightRows.ActSize(alarm, LineHeightRows.MediumId));
        Assert.False(LineHeightRows.Resize(alarm, LineSize.Medium));
        // A line whose content grows has its Height and no Size.
        Assert.Empty(LineHeightRows.SizeRows(Line(Folder(), "calendar")));
    }

    [Fact]
    public void AFlipOrAStepAsksForAHeightInsideTheKindsLimits()
    {
        var folder = Folder();
        var clock = LineHeightRows.For(Line(folder, "clock"), room: 12)!.Value;
        var top = clock with { Now = LineHeight.Exactly(12) };
        Assert.Null(LineHeightRows.Act(top, LineHeightRows.MoreId));
        // §442: a step is a cell, half a row.
        Assert.Equal(LineHeight.Exactly(11), LineHeightRows.Act(top, LineHeightRows.FewerId));
        Assert.Null(LineHeightRows.Act(clock with { Now = LineHeight.Exactly(2) }, LineHeightRows.FewerId));
        // No Automatic, the mode it has asks for nothing, and another row's id neither.
        Assert.Null(LineHeightRows.Act(clock, LineHeightRows.AutomaticId));
        Assert.Null(LineHeightRows.Act(clock, LineHeightRows.FixedId));
        Assert.Null(LineHeightRows.Act(clock, 1));
        Assert.Equal(clock.Now.As(LineHeightMode.Fill), LineHeightRows.Act(clock, LineHeightRows.FillId));

        var calendar = LineHeightRows.For(Line(folder, "calendar"))!.Value with { Now = LineHeight.Automatic(12) };
        Assert.Equal(LineHeight.Exactly(12), LineHeightRows.Act(calendar, LineHeightRows.FixedId));
        Assert.Null(LineHeightRows.Act(calendar, LineHeightRows.AutomaticId));
        Assert.Equal(LineHeight.Automatic(12), LineHeightRows.Act(calendar with { Now = LineHeight.Exactly(12) }, LineHeightRows.AutomaticId));
        Assert.Equal(LineHeight.Automatic(12).As(LineHeightMode.Fill), LineHeightRows.Act(calendar, LineHeightRows.FillId));
        Assert.Equal(LineHeight.Automatic(13), LineHeightRows.Act(calendar, LineHeightRows.MoreId));
    }

    /// <summary>§442: the stepper says rows, two cells each, and a half for an odd count: "1 row", "6 rows", "6½ rows".</summary>
    [Fact]
    public void TheStepperSaysRowsAndHalves()
    {
        Assert.Equal("1 row", LineHeightRows.Count(2));
        Assert.Equal("1½ rows", LineHeightRows.Count(3));
        Assert.Equal("6 rows", LineHeightRows.Count(12));
        Assert.Equal("6½ rows", LineHeightRows.Count(13));
        var calendar = LineHeightRows.For(Line(Folder(), "calendar"))!.Value with { Now = LineHeight.Exactly(13) };
        var stepper = Assert.IsType<FlyoutControl.Stepper>(LineHeightRows.Rows(calendar)[1].Control);
        Assert.Equal("6½ rows", stepper.Value);
    }

    /// <summary>
    /// §192: the stepper goes up to as many rows as the folder's room holds on its screen and no
    /// further, for a kind that had a most of its own (a clock's six) as for one that had none; the
    /// line at those rows is given all of them, one row more would not fit. A height set past the
    /// room elsewhere steps down into it.
    /// </summary>
    [Theory]
    [InlineData(1040f, 1f)]
    [InlineData(1040f, 1.5f)]
    [InlineData(2112f, 1f)]
    public void TheStepperGoesUpToTheRoomAndNoFurther(float work, float scale)
    {
        var px = work * scale;
        var room = FolderFlyout.RoomCells(px, 0, scale, 1);
        var most = FolderFlyout.ListMost(px, 0, scale);
        float RowsPx(int cells) => cells * FolderFlyout.CellPx(scale, 1);
        Assert.True(room > 6, $"{room} rows");
        Assert.True(RowsPx(room) <= most + 0.5f && RowsPx(room + 1) > most, $"{room} rows in {most} px");

        foreach (var id in new[] { "clock", "calendar" })
        {
            var folder = Folder();
            var choice = LineHeightRows.For(Line(folder, id), room)!.Value with { Now = LineHeight.Exactly(room - 1) };
            Assert.Equal(room, choice.Most);
            var up = LineHeightRows.Act(choice, LineHeightRows.MoreId);
            Assert.Equal(LineHeight.Exactly(room), up);
            Assert.Null(LineHeightRows.Act(choice with { Now = up!.Value }, LineHeightRows.MoreId));
            Assert.Equal(LineHeight.Exactly(room), LineHeightRows.Act(choice with { Now = LineHeight.Exactly(room + 20) }, LineHeightRows.FewerId));

            var options = new FolderOptions();
            var line = (id == "clock" ? ListKind.Month : ListKind.Calendar).New();
            line.Height = up;
            options.Put(line);
            var plan = FolderFlyout.Build(new Look(Glass: false, Dark: true, Accent: default), scale, FolderView.List, options, options.Cells, px);
            Assert.Equal(RowsPx(room), plan.Cards[0].bottom - plan.Cards[0].top, 1);
            Assert.Empty(plan.Lists[0].Hidden);
        }
    }

    [Fact]
    public void ResizeWritesTheHeightHeldToTheKind()
    {
        var folder = Folder();
        var clock = Line(folder, "clock");
        Assert.True(LineHeightRows.Resize(clock, LineHeight.Exactly(8)));
        Assert.Equal(LineHeight.Exactly(8), clock.Height);
        Assert.False(LineHeightRows.Resize(clock, LineHeight.Exactly(8)));
        // No most of its own (§192): only a number past any screen is held.
        Assert.True(LineHeightRows.Resize(clock, LineHeight.Exactly(40)));
        Assert.Equal(40, clock.Height!.Value.Cells);
        Assert.True(LineHeightRows.Resize(clock, LineHeight.Exactly(LineHeight.Most + 50)));
        Assert.Equal(LineHeight.Most, clock.Height!.Value.Cells);
        Assert.False(LineHeightRows.Resize(Line(folder, "heading"), LineHeight.Exactly(6)));
    }

    /// <summary>The menu shown again: the height row and its level as they are now, the rows before and after it as they were.</summary>
    [Fact]
    public void TheMenuIsShownAgainWithTheRowsAsTheyAreNow()
    {
        var calendar = LineHeightRows.For(Line(Folder(), "calendar"))!.Value with { Now = LineHeight.Automatic(12) };
        List<FlyoutItem> items = [new(1, "Settings", Primary: true), new(2, "Open"), .. OptionMenu.Foot([.. LineHeightRows.Rows(calendar)]), new(3, "After")];
        var now = LineHeightRows.Updated(items, calendar with { Now = LineHeight.Exactly(14) });
        Assert.Equal(items.Count, now.Count);
        Assert.Equal([1u, 2u, 0u, LineHeightRows.HeightId, LineHeightRows.FewerId, 3u], now.Select(item => item.Id));
        Assert.Equal("Fixed", now[3].Value!.Text);
        Assert.Equal(true, now[3].Children![1].Checked);
        Assert.Equal("7 rows", Assert.IsType<FlyoutControl.Stepper>(now[4].Control).Value);
        Assert.Equal(items, LineHeightRows.Updated(items.Take(2).ToList(), calendar).Concat(items.Skip(2)));
        // §660: Fill picked, the stepper goes, the rows after it stay; Fixed picked again, it is back at the rows kept.
        var fill = LineHeightRows.Updated(now, calendar with { Now = LineHeight.Exactly(14).As(LineHeightMode.Fill) });
        Assert.Equal([1u, 2u, 0u, LineHeightRows.HeightId, 3u], fill.Select(item => item.Id));
        var back = LineHeightRows.Updated(fill, calendar with { Now = LineHeight.Exactly(14) });
        Assert.Equal([1u, 2u, 0u, LineHeightRows.HeightId, LineHeightRows.FewerId, 3u], back.Select(item => item.Id));
        Assert.Equal("7 rows", Assert.IsType<FlyoutControl.Stepper>(back[4].Control).Value);
    }

    /// <summary>
    /// On a real menu: the menu's foot and the modes' level open (the pictures, Automatic and Fixed); the
    /// stepper's "+" pressed and a mode's row clicked are Adjusts and the menu stays; Enter on Fill picks it;
    /// Right on the stepper's row steps; a Refresh shows the new value where the menu stands; §660 under Fill no
    /// stepper, back in its place when Fixed is picked again.
    /// </summary>
    [UiFact]
    public void TheRowsActAndTheMenuStaysOpen()
    {
        WindowShots.OnHiddenDesktop("line-height-menu", (gfx, instance) =>
        {
            foreach (var dark in new[] { true, false })
            {
                foreach (var scale in new[] { 1f, 1.5f })
                {
                    var calendar = LineHeightRows.For(Line(Folder(), "calendar"))!.Value with { Now = LineHeight.Automatic(12) };
                    var work = new RECT { right = (int)(1920 * scale), bottom = (int)(1040 * scale) };
                    var name = $"{(dark ? "dark" : "light")}-{(int)(scale * 100)}";
                    var background = dark ? ((byte)0, (byte)0, (byte)0) : ((byte)243, (byte)243, (byte)243);
                    // The line's menu, its height's level open beside its row: Automatic, then Fixed.
                    foreach (var (now, mode) in new[] { (LineHeight.Automatic(12), "auto"), (LineHeight.Exactly(6), "fixed") })
                    {
                        List<FlyoutItem> line = [new(2, "Open calendar", new FlyoutIcon.Glyph("\uE787")), new(3, "Settings", new FlyoutIcon.Glyph("\uE713")), .. OptionMenu.Foot([.. LineHeightRows.Rows(calendar with { Now = now })])];
                        using var shown = new Flyout(gfx, instance, 0) { Configures = true };
                        shown.Show(gfx, AddListPictures.LookFor(dark), scale, line, FlyoutLayout.List, new POINT { x = work.right / 2, y = work.bottom }, work, Edge.Bottom, null);
                        WindowShots.Settle();
                        for (var step = 0; step < 10 && shown.KeysOn != LineHeightRows.HeightId; step++)
                        {
                            _ = shown.Message(WM.WM_KEYDOWN, VK.VK_DOWN, 0);
                        }
                        _ = shown.Message(WM.WM_KEYDOWN, VK.VK_RIGHT, 0);
                        WindowShots.Settle();
                        Assert.Equal(1, shown.InlineDepth);
                        Assert.True(WindowShots.SaveTogether([.. shown.OpenPanels], $"line-height-menu-{name}-{mode}.png", background));
                        _ = shown.Message(WM.WM_KEYDOWN, VK.VK_LEFT, 0);
                        WindowShots.Settle();
                        Assert.True(WindowShots.SaveTogether([.. shown.OpenPanels], $"line-height-menu-{name}-{mode}-foot.png", background));
                        shown.Hide();
                    }

                    // The level's rows, as a menu of their own, to act on.
                    static List<FlyoutItem> Flat(LineHeightChoice choice) => [.. LineHeightRows.Rows(choice)[0].Children!, .. LineHeightRows.Rows(choice).Skip(1).Select(row => row with { Foot = false })];
                    List<FlyoutItem> items = Flat(calendar);
                    using var menu = new Flyout(gfx, instance, 0);
                    menu.Show(gfx, AddListPictures.LookFor(dark), scale, items, FlyoutLayout.List, new POINT { x = work.right / 2, y = work.bottom }, work, Edge.Bottom, null);
                    WindowShots.Settle();

                    // A press on "+" steps as it goes down (Flyout.Pickers.cs); its release does nothing more.
                    var plus = menu.StepperButton(LineHeightRows.FewerId, more: true)!.Value;
                    var at = (LPARAM)(nint)((plus.y << 16) | (plus.x & 0xFFFF));
                    Assert.Equal(new FlyoutEvent(FlyoutEventKind.Adjust, LineHeightRows.MoreId), menu.Message(WM.WM_LBUTTONDOWN, 1, at));
                    Assert.Equal(default, menu.Message(WM.WM_LBUTTONUP, 0, at));
                    Assert.True(menu.IsOpen);

                    var row = menu.ItemOnScreen(LineHeightRows.FixedId)!.Value;
                    var middle = new POINT { x = (row.left + row.right) / 2, y = (row.top + row.bottom) / 2 };
                    unsafe
                    {
                        _ = TerraFX.Interop.Windows.Windows.ScreenToClient(menu.Hwnd, &middle);
                    }
                    var onRow = (LPARAM)(nint)((middle.y << 16) | (middle.x & 0xFFFF));
                    _ = menu.Message(WM.WM_LBUTTONDOWN, 1, onRow);
                    Assert.Equal(new FlyoutEvent(FlyoutEventKind.Adjust, LineHeightRows.FixedId), menu.Message(WM.WM_LBUTTONUP, 0, onRow));
                    Assert.True(menu.IsOpen);

                    // Shown again for the height as it is now: in place, the value 7.
                    var before = menu.ItemOnScreen(LineHeightRows.FewerId);
                    menu.Refresh(Flat(calendar with { Now = LineHeight.Exactly(14) }));
                    Assert.True(menu.IsOpen);
                    Assert.Equal(before, menu.ItemOnScreen(LineHeightRows.FewerId));
                    Assert.Equal("7 rows", Assert.IsType<FlyoutControl.Stepper>(menu.ShownItems.First(item => item.Id == LineHeightRows.FewerId).Control).Value);
                    WindowShots.Settle();
                    Assert.True(WindowShots.Save(menu.Hwnd, $"line-height-menu-{name}-fixed-7.png"));

                    // Fill picked from the keys, then shown under Fill: §660 no stepper; Fixed again, it is back where it stood.
                    for (var step = 0; step < 10 && menu.HotId != LineHeightRows.FillId; step++)
                    {
                        _ = menu.Message(WM.WM_KEYDOWN, VK.VK_DOWN, 0);
                    }
                    Assert.Equal(new FlyoutEvent(FlyoutEventKind.Adjust, LineHeightRows.FillId), menu.Message(WM.WM_KEYDOWN, VK.VK_RETURN, 0));
                    menu.Refresh(Flat(calendar with { Now = LineHeight.Exactly(14).As(LineHeightMode.Fill) }));
                    Assert.Null(menu.ItemOnScreen(LineHeightRows.FewerId));
                    WindowShots.Settle();
                    Assert.True(WindowShots.Save(menu.Hwnd, $"line-height-menu-{name}-fill.png"));
                    menu.Refresh(Flat(calendar with { Now = LineHeight.Exactly(14) }));
                    Assert.Equal(before, menu.ItemOnScreen(LineHeightRows.FewerId));

                    // The keys: down to the stepper's row, then Right steps it.
                    for (var step = 0; step < 10 && menu.HotId != LineHeightRows.FewerId; step++)
                    {
                        _ = menu.Message(WM.WM_KEYDOWN, VK.VK_DOWN, 0);
                    }
                    Assert.Equal(LineHeightRows.FewerId, menu.HotId);
                    Assert.Equal(new FlyoutEvent(FlyoutEventKind.Adjust, LineHeightRows.MoreId), menu.Message(WM.WM_KEYDOWN, VK.VK_RIGHT, 0));
                    Assert.Equal(new FlyoutEvent(FlyoutEventKind.Adjust, LineHeightRows.FewerId), menu.Message(WM.WM_KEYDOWN, VK.VK_LEFT, 0));
                    Assert.True(menu.IsOpen);
                    menu.Hide();
                }
            }
        });
    }
}
