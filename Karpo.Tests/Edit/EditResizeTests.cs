// edit: resizing the widget the keys are on by Shift+arrows (Karpo.Dock\EditResize.cs; docs\widgets.md §471; the
// person, 2026-11-01: "sounds good. go ahead" to "Shift+Up/Down changes its height by one cell, Shift+Left/Right its
// width where widths are adjustable ... same undo as the menu change"). Held here:
//
//  - **The keymap.** Shift+arrows are Resize, carrying the arrow; Ctrl+arrows still Move (Ctrl+Shift too), Shift+Tab
//    still the one before.
//  - **A line's height a cell** — what its menu's stepper's "−" and "+" ask (LineHeightRows.Act): in either mode, held
//    to the kind's fewest and to the folder's room, nothing under Fill.
//  - **A sized line's size** (§432): Shift+Down the next size its kind offers, Shift+Up the one before, as its "Size" row.
//  - **A bar widget's width** where a face of it has a width choice (Compact, Standard, Wide), as that row sets it.
//  - **Nothing else**: no height on the bar, no width in a folder, nothing past an end.
//  - **Through the folder's keys** ([UiFact], KARPO_UI_TESTS=1): Shift+Down on the line the keys are on in the
//    arranged folder asks the dock to resize it; written as the dock writes it, the line is a cell taller.
//    Pictures: edit-resize-keys-before.png, edit-resize-keys-after.png in KARPO_PNG_DIR.

using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using Color = Windows.UI.Color;

namespace Karpo.Tests.Edit;

public sealed class EditResizeTests
{
    private static BarItem Folder()
    {
        var island = BarKind.FolderList.New();
        island.Id = "box";
        Put(island, ListKind.Calendar.New(), "calendar");
        Put(island, ListKind.Month.New(), "month");
        Put(island, ListKind.Media.New(), "media");
        return island;
    }

    private static CellItem Line(BarItem folder, string id) =>
        Karpo.Core.Folders.Find([folder], $"{folder.Id}/{id}") as CellItem ?? throw new Xunit.Sdk.XunitException($"no line {id}");

    private static void Put(BarItem folder, CellItem widget, string id)
    {
        widget.Id = id;
        widget.View = Karpo.Core.Folders.View(folder.Kind);
        folder.Folder!.Put(widget);
    }

    private static BarItem Meeting(string width)
    {
        var folder = Presets.Folder(Presets.Find(Presets.MeetingId)!, new PresetMachine(HasBattery: false, FixedDrives: []));
        folder.Folder!.Face.Set(FolderFaces.Meeting.Option(MeetingFace.Width)!, width);
        return folder;
    }

    [Theory]
    [InlineData(VK.VK_UP)]
    [InlineData(VK.VK_DOWN)]
    [InlineData(VK.VK_LEFT)]
    [InlineData(VK.VK_RIGHT)]
    public void ShiftAndAnArrowResizesAndCtrlStillMoves(int arrow)
    {
        Assert.Equal(new EditKeyStroke(EditKeyAct.Resize, arrow), EditKeys.Of(arrow, control: false, shift: true, repeat: false));
        Assert.Equal(new EditKeyStroke(EditKeyAct.Resize, arrow), EditKeys.Of(arrow, control: false, shift: true, repeat: true));
        Assert.Equal(new EditKeyStroke(EditKeyAct.Move, arrow), EditKeys.Of(arrow, control: true, shift: true, repeat: false));
        Assert.Equal(new EditKeyStroke(EditKeyAct.Focus, arrow), EditKeys.Of(arrow, control: false, shift: false, repeat: false));
        Assert.Equal(EditKeyAct.Previous, EditKeys.Of(VK.VK_TAB, control: false, shift: true, repeat: false).Act);
    }

    /// <summary>Shift+Down a cell taller, Shift+Up a cell shorter — the menu's own "+" and "−", in either mode.</summary>
    [Fact]
    public void ALinesHeightStepsACellAsItsMenusStepperDoes()
    {
        foreach (var height in new[] { LineHeight.Automatic(9), LineHeight.Exactly(9) })
        {
            var line = Line(Folder(), "calendar");
            line.Height = height;
            var choice = LineHeightRows.For(line)!.Value;
            Assert.True(EditResize.Step(line, VK.VK_DOWN));
            Assert.Equal(LineHeightRows.Act(choice, LineHeightRows.MoreId), line.Height);
            Assert.Equal(height with { Cells = 10 }, line.Height);
            Assert.True(EditResize.Step(line, VK.VK_UP));
            Assert.True(EditResize.Step(line, VK.VK_UP));
            Assert.Equal(height with { Cells = 8 }, line.Height);
        }
    }

    /// <summary>Held to a row at the least and to what the folder's room holds at the most; nothing under Fill.</summary>
    [Fact]
    public void ALinesHeightStopsAtItsLimitsAndUnderFill()
    {
        var line = Line(Folder(), "calendar");
        line.Height = LineHeight.Exactly(LineHeight.Fewest);
        Assert.False(EditResize.Step(line, VK.VK_UP));
        Assert.Equal(LineHeight.Exactly(LineHeight.Fewest), line.Height);
        line.Height = LineHeight.Exactly(7);
        Assert.False(EditResize.Step(line, VK.VK_DOWN, room: 7));
        Assert.True(EditResize.Step(line, VK.VK_DOWN, room: 8));
        line.Height = LineHeight.Exactly(7).As(LineHeightMode.Fill);
        Assert.False(EditResize.Step(line, VK.VK_DOWN));
        Assert.False(EditResize.Step(line, VK.VK_UP));
    }

    /// <summary>A kind whose content does not vary stays Fixed as it steps, as its menu has no Automatic.</summary>
    [Fact]
    public void AFixedKindStaysFixed()
    {
        var line = Line(Folder(), "month");
        var was = LineHeight.For(line);
        Assert.True(was.Fixed);
        Assert.True(EditResize.Step(line, VK.VK_DOWN));
        Assert.Equal(LineHeight.Exactly(was.Cells + 1), line.Height);
    }

    /// <summary>§432: a sized line has no rows: Shift+Down the next size its kind offers, Shift+Up the one before, none past them.</summary>
    [Fact]
    public void ASizedLineStepsThroughItsSizes()
    {
        var line = Line(Folder(), "media");
        var offered = ListKind.Media.Offered();
        Assert.True(offered.Count > 1);
        line.Size = offered[0];
        Assert.False(EditResize.Step(line, VK.VK_UP));
        for (var at = 1; at < offered.Count; at++)
        {
            Assert.True(EditResize.Step(line, VK.VK_DOWN));
            Assert.Equal(offered[at], line.Size);
        }
        Assert.False(EditResize.Step(line, VK.VK_DOWN));
        Assert.True(EditResize.Step(line, VK.VK_UP));
        Assert.Equal(offered[^2], line.Size);
    }

    /// <summary>A line of a folder has no width of its own (its list has one): Shift+Left and Right do nothing.</summary>
    [Fact]
    public void ALineHasNoWidth()
    {
        var line = Line(Folder(), "calendar");
        var was = line.Height;
        Assert.False(EditResize.Step(line, VK.VK_LEFT));
        Assert.False(EditResize.Step(line, VK.VK_RIGHT));
        Assert.Equal(was, line.Height);
    }

    /// <summary>A bar widget whose face has a width choice: Shift+Right the wider, Shift+Left the narrower, none past them.</summary>
    [Fact]
    public void ABarWidgetsWidthStepsThroughItsFacesWidths()
    {
        var bar = Meeting(MeetingFace.Standard);
        var face = bar.Folder!.Face;
        Assert.True(EditResize.Step(bar, VK.VK_RIGHT));
        Assert.Equal(MeetingFace.Wide, MeetingFace.WidthOf(face));
        Assert.False(EditResize.Step(bar, VK.VK_RIGHT));
        Assert.True(EditResize.Step(bar, VK.VK_LEFT));
        Assert.True(EditResize.Step(bar, VK.VK_LEFT));
        Assert.Equal(MeetingFace.Compact, MeetingFace.WidthOf(face));
        Assert.False(EditResize.Step(bar, VK.VK_LEFT));
        // No height on the bar.
        Assert.False(EditResize.Step(bar, VK.VK_DOWN));
        Assert.False(EditResize.Step(bar, VK.VK_UP));
    }

    /// <summary>A bar widget with no width choice (the clock) is not resized by the keys.</summary>
    [Fact]
    public void ABarWidgetWithNoWidthChoiceStays()
    {
        var clock = Presets.Folder(Presets.Find(Presets.ClockId)!, new PresetMachine(HasBattery: false, FixedDrives: []));
        Assert.False(EditResize.Step(clock, VK.VK_RIGHT));
        Assert.False(EditResize.Step(clock, VK.VK_LEFT));
    }

    /// <summary>What Narrator is told: the rows the stepper would say, the size or the width the row would say.</summary>
    [Fact]
    public void TheNewSizeIsSaidAsTheMenuSaysIt()
    {
        var line = Line(Folder(), "calendar");
        line.Height = LineHeight.Exactly(6);
        Assert.Equal(LineHeightRows.Count(6), EditResize.Said(line, VK.VK_DOWN));
        var media = Line(Folder(), "media");
        media.Size = LineSize.Full;
        Assert.Equal(LineHeightRows.Said(LineSize.Full), EditResize.Said(media, VK.VK_UP));
        var bar = Meeting(MeetingFace.Wide);
        Assert.Equal(FolderFaces.Meeting.ChoiceTitle(FolderFaces.Meeting.Option(MeetingFace.Width)!, MeetingFace.Wide), EditResize.Said(bar, VK.VK_RIGHT));
    }

    // --- Through the folder's keys ----------------------------------------------------------------

    private static readonly string Owner = Config.LayoutOwner("only");

    /// <summary>
    /// Shift+Down on the line the keys are on in the arranged folder: the folder asks the dock to resize it
    /// (FolderFlyoutAction.Resize, the arrow), and written as the dock writes it (EditResize.Step, saved by Apply as
    /// the menu's change is) the line stands a cell taller; Ctrl+Shift+Down still moves it, never resizes.
    /// </summary>
    [UiFact]
    public void ShiftDownInTheArrangedFolderMakesTheLineACellTaller()
    {
        WindowShots.OnHiddenDesktop("edit-resize-keys", (gfx, instance) =>
        {
            var config = new Config();
            config.Layouts.Clear();
            var box = Folder();
            Line(box, "calendar").Height = LineHeight.Exactly(6);
            config.Layouts.Add(new TaskbarLayout { Id = "only", Name = "Default", Islands = [box] });
            var settings = (BarItem)config.FindIsland(Owner, box.Id)!;
            var folder = new FolderIsland(Config.IslandKey(Owner, box.Id), settings, gfx, config);
            using var flyout = new FolderFlyout(gfx, instance, 0);
            var held = KeyState.Held;
            try
            {
                flyout.Show(gfx, new Look(Glass: false, Dark: true, Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4)), 1f,
                    new POINT { x = 960, y = 1040 }, new RECT { right = 1920, bottom = 1040 }, Edge.Bottom, folder, editing: true);
                KeyState.Held = key => key == VK.VK_SHIFT;
                _ = flyout.Message(WM.WM_KEYDOWN, (WPARAM)(nuint)VK.VK_HOME, 0);
                var before = flyout.Plan!.Cards[0];
                WindowShots.Settle();
                _ = WindowShots.Save(flyout.Hwnd, "edit-resize-keys-before.png");

                var asked = flyout.Message(WM.WM_KEYDOWN, (WPARAM)(nuint)VK.VK_DOWN, 0);
                Assert.Equal(FolderFlyoutAction.Resize, asked.Kind);
                Assert.Equal((folder.Tiles[0].Island.Key, VK.VK_DOWN), (asked.Widget?.Key, asked.Part));
                var item = config.FindIsland(Owner, Karpo.Core.Folders.Path(box.Id, "calendar"))!;
                Assert.True(EditResize.Step(item, asked.Part, flyout.RoomCells()));
                Assert.Equal(LineHeight.Exactly(7), item.Height);
                folder.Configure(config.FindIsland(Owner, box.Id)!, config);
                var after = flyout.Plan!.Cards[0];
                WindowShots.Settle();
                _ = WindowShots.Save(flyout.Hwnd, "edit-resize-keys-after.png");
                var grown = (after.bottom - after.top) - (before.bottom - before.top);
                Assert.InRange(grown, 15f, 30f);

                KeyState.Held = key => key is VK.VK_SHIFT or VK.VK_CONTROL;
                Assert.NotEqual(FolderFlyoutAction.Resize, flyout.Message(WM.WM_KEYDOWN, (WPARAM)(nuint)VK.VK_DOWN, 0).Kind);
            }
            finally
            {
                KeyState.Held = held;
                flyout.Hide();
            }
        });
    }
}
