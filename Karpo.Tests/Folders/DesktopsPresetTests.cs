// The desktop switcher is a preset (docs\widgets.md §116): what a click on it gave — every desktop
// with the apps open on it, the one you are on marked, a click switching, each renamed, closed and
// moved, "New desktop" under them — is a folder holding the desktops' line (DesktopsLine) and the
// desktop actions (Multitasking | Task view), under a face of the glyph and the desktop's name the
// switcher was. Reordering is a drag by a row's gripper (2026-10-11: "reordering desktops should be
// with drag and drop, not with the up and down buttons"), or Alt+Up and Alt+Down.
//
// Every desktop here is a made-up one (FakeDesktops): no test reaches Windows' desktops.
//
// Pictured beside the flyout it replaces (rendered before it went: quick-desktops-*.png):
// desktops-preset-<dark|light>.png at 150 %, and the face on the bar, desktops-face-<dark|light>.png.

using System.Numerics;
using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using static Karpo.Tests.Folders.ListFormPictures;
using static TerraFX.Interop.Windows.Windows;
using Edge = Karpo.Core.Edge;

namespace Karpo.Tests.Folders;

/// <summary>Desktops of a test's own: named, ordered, each with apps, and every change written down.</summary>
internal sealed class FakeDesktops : IDesktops
{
    public List<(string Id, string Name, int Apps)> Desks { get; } = [];

    public int Current { get; set; }

    public bool Shell { get; set; } = true;

    /// <summary>Whether a move is read back at once; false stands for Windows writing the new order down a moment later.</summary>
    public bool MovesAtOnce { get; set; } = true;

    /// <summary>What was asked of it, in order.</summary>
    public List<string> Said { get; } = [];

    public event Action? Changed;

    public static FakeDesktops Of(params string[] names)
    {
        var fake = new FakeDesktops();
        for (var index = 0; index < names.Length; index++)
        {
            fake.Desks.Add(($"id{index}", names[index], index == 0 ? 6 : index));
        }
        return fake;
    }

    public DesktopStrip Strip() => new(
        [.. Desks.Select(desk => new DesktopButton(desk.Name, [.. Enumerable.Range(0, Math.Min(desk.Apps, Switchers.RowIcons)).Select(App)], Math.Max(0, desk.Apps - Switchers.RowIcons), desk.Id))],
        Current);

    private static Item App(int index) => new() { Target = $"app{index}", Name = $"App {index}", Kind = ItemKind.Shell, Keys = [$"app{index}"] };

    public bool Add()
    {
        Said.Add("add");
        Desks.Add(($"id{Desks.Count}", "", 0));
        return true;
    }

    public bool Remove(int index)
    {
        Said.Add($"remove {index}");
        Desks.RemoveAt(index);
        Current = Math.Min(Current, Desks.Count - 1);
        return true;
    }

    public bool Move(int from, int to)
    {
        Said.Add($"move {from}->{to}");
        if (MovesAtOnce)
        {
            Apply(from, to);
        }
        return Shell;
    }

    /// <summary>Windows writes the move down: the order is the new one, and the lines are told.</summary>
    public void Apply(int from, int to)
    {
        var current = Desks[Current].Id;
        var desk = Desks[from];
        Desks.RemoveAt(from);
        Desks.Insert(to, desk);
        Current = Desks.FindIndex(each => each.Id == current);
    }

    public bool Rename(int index, string name)
    {
        Said.Add($"rename {index} {name}");
        Desks[index] = (Desks[index].Id, name, Desks[index].Apps);
        return true;
    }

    /// <summary>The dock read the desktops again.</summary>
    public void Tell() => Changed?.Invoke();
}

public sealed unsafe class DesktopsPresetTests
{
    private static readonly PresetMachine Machine = new(HasBattery: false, FixedDrives: []);
    private static readonly RECT Work = new() { left = 0, top = 0, right = 1920, bottom = 1040 };
    private static readonly POINT Anchor = new() { x = 300, y = 1040 };

    /// <summary>The desktops' preset folder, as the add list makes it.</summary>
    private static BarItem Holder() => Presets.Folder(Presets.Find(Presets.DesktopsId)!, Machine);

    /// <summary>The preset's folder over <paramref name="desktops"/>.</summary>
    private static FolderIsland Made(Gfx gfx, IDesktops desktops, string? key = null, BarItem? holder = null)
    {
        holder ??= Holder();
        DesktopsLine.Stand = desktops;
        DesktopsLine.Icons = static (_, size) => Square(size);
        try
        {
            return new FolderIsland(key ?? $"{Owner}/{holder.Id}", holder, gfx, new Config());
        }
        finally
        {
            DesktopsLine.Stand = null;
            DesktopsLine.Icons = null;
        }
    }

    /// <summary>The line, alone, over <paramref name="desktops"/>, shown as an opening folder shows it.</summary>
    private static DesktopsLine Line(Gfx gfx, IDesktops desktops, Action<CellItem>? set = null)
    {
        var cell = ListKind.Desktops.New();
        cell.Id = "desktops-" + Guid.NewGuid().ToString("N")[..6];
        set?.Invoke(cell);
        DesktopsLine.Icons = static (_, size) => Square(size);
        try
        {
            var line = new DesktopsLine(Config.IslandKey(Owner, cell.Id), cell, gfx, new Config(), desktops);
            line.StandIn(ListWidgets.Of(ListKind.Desktops));
            line.Visible(true);
            return line;
        }
        finally
        {
            DesktopsLine.Icons = null;
        }
    }

    /// <summary>A solid square standing in for an app's icon, so a picture is the same on every PC.</summary>
    private static Pixels Square(int size)
    {
        var data = new byte[size * size * 4];
        for (var i = 0; i < data.Length; i += 4)
        {
            data[i] = 0x10;
            data[i + 1] = 0x7C;
            data[i + 2] = 0x10;
            data[i + 3] = 0xFF;
        }
        return new Pixels { Data = data, Width = size, Height = size };
    }

    private static TileStyle StyleFor(float scale, Look look) => FolderFlyout.StyleFor(scale, look, FlyoutTheme.For(look));

    /// <summary>The line as tall as it asks to be, at the list's own width.</summary>
    private static D2D_RECT_F Whole(DesktopsLine line, in TileStyle style)
    {
        var width = FolderFlyout.ListWidth(style.Scale);
        return PopupPaint.Rect(0, 0, width, line.ListHeight(width, style));
    }

    private static IReadOnlyList<LinePiece> Pieces(DesktopsLine line, D2D_RECT_F box, in TileStyle style) =>
        ((ILinePieces)line).LinePieces(box, style);

    private static Vector2 Middle(D2D_RECT_F box) => new((box.left + box.right) / 2, (box.top + box.bottom) / 2);

    private static D2D_RECT_F Piece(DesktopsLine line, in TileStyle style, string name) =>
        Assert.Single(Pieces(line, Whole(line, style), style), piece => piece.Name == name).Box;

    /// <summary>The names the rows show, in order.</summary>
    private static List<string> Names(DesktopsLine line, in TileStyle style) =>
        [.. Pieces(line, Whole(line, style), style).Where(piece => piece.Name.StartsWith("desktop ", StringComparison.Ordinal)).Select(piece => piece.Text)];

    /// <summary>A click on <paramref name="piece"/>, as the folder gives it: the pointer onto it, the press (the folder's), then its number.</summary>
    private static IslandAction Click(DesktopsLine line, in TileStyle style, string piece)
    {
        var box = Whole(line, style);
        var at = Middle(Piece(line, style, piece));
        line.CardHover(at, box, style, WidgetSurface.List);
        Assert.False(line.PressOn(at, box, style, WidgetSurface.List), "a press beside the gripper is the folder's");
        var id = line.CardTarget(at, box, style, WidgetSurface.List);
        Assert.NotEqual(0, id);
        return line.Click(id);
    }

    /// <summary>The keys a test holds while <paramref name="body"/> runs.</summary>
    private static void Holding(Func<int, bool> keys, Action body)
    {
        var held = KeyState.Held;
        KeyState.Held = keys;
        try
        {
            body();
        }
        finally
        {
            KeyState.Held = held;
        }
    }

    private static readonly Func<int, bool> NoKeys = static _ => false;
    private static readonly Func<int, bool> Alt = static key => key == VK.VK_MENU;

    // --- The preset ------------------------------------------------------------------------------------

    /// <summary>
    /// The flyout, made of widgets: the desktops' line, as tall as there are desktops up to the most a
    /// line may be, and the desktop actions in its card (Multitasking | Task view), under the desktops
    /// face, which has nothing to set; in the add list's library with the start's groups.
    /// </summary>
    [Fact]
    public void ThePresetIsTheLineAndItsActionsUnderTheDesktopsName()
    {
        var preset = Presets.Find(Presets.DesktopsId)!;
        Assert.Equal(FolderFaces.DesktopsId, preset.Face!.Kind);
        Assert.Equal(["desktops", "actions"], preset.Items.Select(item => item.Form));
        Assert.Equal(LineHeight.Automatic(FolderOptions.MostCells), preset.Items[0].Height);
        Assert.Same(PremadeWidgets.DesktopActions, preset.Items[1].Premade);
        Assert.Equal(("multitasking", SettingsActions.TaskViewId), PremadeWidgets.DesktopActions.Actions);
        Assert.Equal("ms-settings:multitasking", SettingsActions.Find("multitasking")!.Uri);

        var folder = Holder().Folder!;
        Assert.Same(FolderFaces.Desktops, folder.Face.Info);
        Assert.Contains(FolderFaces.Desktops, FolderFaces.All);
        Assert.Empty(folder.Face.Info.Options);
        Assert.Equal(WidgetInfos.Desktops.Glyph, folder.Face.Info.Glyph);
        Assert.Contains(FolderFaces.DesktopsId, FaceDrawings.Ids);
        // With the start's groups, where the switcher stood: after the power button, before the mail.
        var all = Presets.All.ToList();
        Assert.Equal(all.FindIndex(each => each.Id == Presets.PowerId) + 1, all.IndexOf(preset));
        Assert.Equal(BarSection.Start, PresetLayout.SectionOf(preset));
    }

    /// <summary>
    /// The switcher is gone from the bar, its flyout with it; the line is all there is: a press on a
    /// gripper and a drag its own, a click the folder's act (the switch), and its menu Task view and a
    /// new desktop, as Task view's line has them.
    /// </summary>
    [Fact]
    public void TheSwitcherIsGoneAndTheLineSwitches()
    {
        Assert.Null(BarEntries.Find("desktops"));
        Assert.DoesNotContain("desktops", Enum.GetValues<BarKind>().Select(kind => kind.ToString().ToLowerInvariant()));
        var line = ListWidgets.Of(ListKind.Desktops);
        Assert.Same(WidgetInfos.Desktops, line.Info);
        Assert.Equal("desktops", line.Info.Id);
        Assert.Equal(CellAct.Action, line.Act);
        Assert.True(line.Interaction.HasFlag(Interaction.Drag));
        Assert.Equal(WidgetOptionsKind.Desktops, line.Info.Options);
        Assert.Equal(Dock.Dock.ActsFor(ListKind.TaskView), Dock.Dock.ActsFor(ListKind.Desktops));
    }

    // --- The line --------------------------------------------------------------------------------------

    /// <summary>
    /// A row per desktop in Windows' order, its name — Windows' own number where none was given —
    /// the one you are on marked, and "New desktop" at the foot; as tall as all of them.
    /// </summary>
    [UiFact]
    public void ARowPerDesktopAndNewDesktopAtTheFoot()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var desktops = FakeDesktops.Of("Desktop 1", "Work", "");
            desktops.Current = 1;
            using var line = Line(gfx, desktops);
            var style = StyleFor(1f, LookFor(true));

            Assert.Equal(["Desktop 1", "Work", "Desktop 3"], Names(line, style));
            Assert.Equal("Work", Assert.Single(Pieces(line, Whole(line, style), style), piece => piece.Name == "current").Text);
            var add = Piece(line, style, "add");
            Assert.True(add.top > Piece(line, style, "desktop 2").top);
            // As tall as its rows and the foot, at the list's row: 40 px each, the first from the line's top, the foot its bottom (§436).
            Assert.Equal(4 * 40, line.ListHeight(FolderFlyout.ListWidth(1f), style), 0.5f);

            desktops.Desks.Add(("id9", "Games", 0));
            desktops.Tell();
            Assert.Equal(["Desktop 1", "Work", "Desktop 3", "Games"], Names(line, style));
        });
    }

    /// <summary>
    /// A click on a row is the folder's act: the switch, asked of the dock once the folder has gone
    /// (TakeSwitch), by the desktop's place in Windows' order — and asked once.
    /// </summary>
    [UiFact]
    public void AClickOnARowSwitchesOnceTheFolderHasGone()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var desktops = FakeDesktops.Of("Desktop 1", "Work", "Games");
            using var line = Line(gfx, desktops);
            var style = StyleFor(1f, LookFor(true));

            var act = Click(line, style, "desktop 2");
            Assert.Equal(IslandView.Desktops, act.View);
            Assert.True(FolderFlyout.Forwards(act, CellAct.Action));
            Assert.Equal(2, line.TakeSwitch());
            Assert.Null(line.TakeSwitch());
            Assert.Empty(desktops.Said);
        });
    }

    /// <summary>
    /// The pencil renames in place: the name all chosen in a box on the row, typed over, Enter keeps
    /// it; Escape does not. The cross closes the desktop; "New desktop" makes one. Each acts in place
    /// and the folder stays.
    /// </summary>
    [UiFact]
    public void ARowsButtonsRenameAndCloseAndTheFootMakesOne()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var desktops = FakeDesktops.Of("Desktop 1", "Work", "Games");
            using var line = Line(gfx, desktops);
            var style = StyleFor(1f, LookFor(true));
            Holding(NoKeys, () =>
            {
                Assert.Equal(default, Click(line, style, "rename 1"));
                Assert.Equal("id1", line.Renaming);
                Assert.True(line.TypingOn);
                Assert.Equal("Work", line.Editor.Text);
                foreach (var c in "Office")
                {
                    Assert.True(line.CharOn(c, WidgetSurface.List));
                }
                Assert.True(line.KeyOn(VK.VK_RETURN, WidgetSurface.List));
                Assert.Null(line.Renaming);
                Assert.Equal(["rename 1 Office"], desktops.Said);

                // Escape lets the name go.
                _ = Click(line, style, "rename 2");
                Assert.True(line.CharOn('X', WidgetSurface.List));
                Assert.True(line.KeyOn(VK.VK_ESCAPE, WidgetSurface.List));
                Assert.Null(line.Renaming);
                Assert.Equal(["rename 1 Office"], desktops.Said);

                // A press elsewhere keeps what was typed, as Task View's box does.
                _ = Click(line, style, "rename 2");
                Assert.True(line.CharOn('Y', WidgetSurface.List));
                line.StopTypingOn();
                Assert.Equal(["rename 1 Office", "rename 2 Y"], desktops.Said);

                Assert.Equal(default, Click(line, style, "delete 0"));
                Assert.Equal(default, Click(line, style, "add"));
            });
            Assert.Equal(["rename 1 Office", "rename 2 Y", "remove 0", "add"], desktops.Said);
            Assert.Equal(["Office", "Y", "Desktop 3"], Names(line, style));
        });
    }

    /// <summary>
    /// Without the shell's own calls nothing is renamed or moved, and only the desktop you are on can
    /// be closed: the buttons say why rather than doing nothing, and the gripper takes no press.
    /// </summary>
    [UiFact]
    public void WithoutTheShellTheRowsSayWhyNot()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var desktops = FakeDesktops.Of("Desktop 1", "Work");
            desktops.Shell = false;
            using var line = Line(gfx, desktops);
            var style = StyleFor(1f, LookFor(true));
            var box = Whole(line, style);

            _ = Click(line, style, "rename 1");
            Assert.Null(line.Renaming);
            _ = Click(line, style, "delete 1");
            Assert.Empty(desktops.Said);
            line.CardHover(Middle(Piece(line, style, "rename 1")), box, style, WidgetSurface.List);
            Assert.Equal(Switchers.NoShellRename, line.TipOn(Middle(Piece(line, style, "rename 1")), box, style, WidgetSurface.List)?.Text);
            Assert.Equal(Switchers.OnlyCurrentDeletes, line.TipOn(Middle(Piece(line, style, "delete 1")), box, style, WidgetSurface.List)?.Text);
            Assert.Equal(Switchers.NoShellMove, line.TipOn(Middle(Piece(line, style, "grip 1")), box, style, WidgetSurface.List)?.Text);
            Assert.False(line.PressOn(Middle(Piece(line, style, "grip 1")), box, style, WidgetSurface.List));
            Assert.False(line.GripOn(Middle(Piece(line, style, "grip 1")), box, style));
        });
    }

    // --- Dragging ----------------------------------------------------------------------------------------

    /// <summary>
    /// The gripper is the handle: the pointer over it is the move cursor, a press on it is the line's,
    /// and past the drag threshold the row lifts and follows the pointer while the rows it passes make
    /// room — sliding, not jumping. Let go over the third slot, the desktop is moved there through the
    /// shell, once, and the line shows the new order at once, before Windows has written it down.
    /// </summary>
    [UiFact]
    public void ARowDraggedByItsGripperIsMovedWhereItIsLetGo()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var desktops = FakeDesktops.Of("One", "Two", "Three", "Four");
            desktops.MovesAtOnce = false;
            using var line = Line(gfx, desktops);
            var now = 1000L;
            line.Ticks = () => now;
            var style = StyleFor(1f, LookFor(true));
            var box = Whole(line, style);
            var grip = Middle(Piece(line, style, "grip 0"));

            Assert.True(line.GripOn(grip, box, style), "the gripper shows no move cursor");
            Assert.False(line.GripOn(Middle(Piece(line, style, "desktop 0")), box, style));
            Assert.True(line.PressOn(grip, box, style, WidgetSurface.List), "a press on the gripper is not the line's");
            // Under the drag threshold nothing lifts.
            line.DragOn(grip + new Vector2(0, 1), box, style, WidgetSurface.List);
            Assert.False(line.Gripping && line.Animating);
            // Two rows down: the row follows, the two it passed slide up a row.
            line.DragOn(grip + new Vector2(0, 80), box, style, WidgetSurface.List);
            Assert.True(line.Gripping);
            Assert.True(line.Animating, "the rows making room do not slide");
            now += line.Ms;
            Assert.Equal(-40, line.Offset("id1", now), 0.5f);
            Assert.Equal(-40, line.Offset("id2", now), 0.5f);
            Assert.Equal(0, line.Offset("id3", now), 0.5f);
            // Halfway through a slide it is between the two.
            line.DragOn(grip + new Vector2(0, 40), box, style, WidgetSurface.List);
            var halfway = line.Offset("id2", now + (line.Ms / 2));
            Assert.InRange(halfway, -39, -1);

            line.DragOn(grip + new Vector2(0, 80), box, style, WidgetSurface.List);
            line.ReleaseOn(WidgetSurface.List);
            Assert.Equal(["move 0->2"], desktops.Said);
            Assert.False(line.Gripping);
            // Windows has not written it down yet: the line shows the order asked for.
            Assert.Equal(["Two", "Three", "One", "Four"], Names(line, style));
            desktops.Apply(0, 2);
            desktops.Tell();
            Assert.Equal(["Two", "Three", "One", "Four"], Names(line, style));
            // The row settles into its slot, and then nothing slides.
            now += line.Ms + 1;
            PaintOnce(line, box, style);
            Assert.False(line.Animating, "a slide that ended still draws on the quick tick");
        });
    }

    /// <summary>Escape while a row is lifted puts it back: nothing is moved, and the rest of the drag is nobody's.</summary>
    [UiFact]
    public void EscapePutsTheRowBack()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var desktops = FakeDesktops.Of("One", "Two", "Three");
            using var line = Line(gfx, desktops);
            var style = StyleFor(1f, LookFor(true));
            var box = Whole(line, style);
            var grip = Middle(Piece(line, style, "grip 2"));

            Assert.False(line.EscapeOn(), "Escape with nothing lifted is the folder's");
            Assert.True(line.PressOn(grip, box, style, WidgetSurface.List));
            line.DragOn(grip - new Vector2(0, 80), box, style, WidgetSurface.List);
            Assert.True(line.EscapeOn());
            Assert.False(line.Gripping);
            line.DragOn(grip - new Vector2(0, 40), box, style, WidgetSurface.List);
            line.ReleaseOn(WidgetSurface.List);
            Assert.Empty(desktops.Said);
            Assert.Equal(["One", "Two", "Three"], Names(line, style));
        });
    }

    /// <summary>A drag let go on its own slot moves nothing; nor does a press on the gripper that never lifted.</summary>
    [UiFact]
    public void ADragBackToItsOwnSlotMovesNothing()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var desktops = FakeDesktops.Of("One", "Two", "Three");
            using var line = Line(gfx, desktops);
            var style = StyleFor(1f, LookFor(true));
            var box = Whole(line, style);
            var grip = Middle(Piece(line, style, "grip 1"));

            Assert.True(line.PressOn(grip, box, style, WidgetSurface.List));
            line.DragOn(grip + new Vector2(0, 30), box, style, WidgetSurface.List);
            line.DragOn(grip + new Vector2(0, 10), box, style, WidgetSurface.List);
            line.ReleaseOn(WidgetSurface.List);
            Assert.True(line.PressOn(grip, box, style, WidgetSurface.List));
            line.ReleaseOn(WidgetSurface.List);
            Assert.Empty(desktops.Said);
        });
    }

    /// <summary>Where a lifted row lands, how far each other row stands off its slot meanwhile, and the order a move makes.</summary>
    [Theory]
    [InlineData(0f, 0)]
    [InlineData(19f, 0)]
    [InlineData(21f, 1)]
    [InlineData(-500f, 0)]
    [InlineData(500f, 3)]
    public void TheNearestSlotIsWhereARowLands(float top, int slot) =>
        Assert.Equal(slot, DesktopsLine.DropIndex(top, 0, 40, 4));

    [Theory]
    [InlineData(1, 0, 2, -40f)]
    [InlineData(2, 0, 2, -40f)]
    [InlineData(3, 0, 2, 0f)]
    [InlineData(0, 3, 1, 0f)]
    [InlineData(1, 3, 1, 40f)]
    [InlineData(2, 3, 1, 40f)]
    [InlineData(1, 1, 1, 0f)]
    public void TheRowsBetweenMakeRoom(int index, int from, int to, float shift) =>
        Assert.Equal(shift, DesktopsLine.Shift(index, from, to, 40));

    [Fact]
    public void AMoveTakesTheRowOutAndPutsItIn()
    {
        Assert.Equal(["b", "c", "a", "d"], DesktopsLine.Reordered(["a", "b", "c", "d"], 0, 2));
        Assert.Equal(["d", "a", "b", "c"], DesktopsLine.Reordered(["a", "b", "c", "d"], 3, 0));
    }

    // --- The keys ----------------------------------------------------------------------------------------

    /// <summary>
    /// Up and Down walk the rows and "New desktop", past either end the folder's; Alt+Up and Alt+Down
    /// move the row the keys are on a place, the keys staying with it; F2 renames, Delete closes, and
    /// Enter is the folder's, which clicks the row: the switch.
    /// </summary>
    [UiFact]
    public void TheKeysWalkMoveRenameAndSwitch()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var desktops = FakeDesktops.Of("One", "Two", "Three");
            using var line = Line(gfx, desktops);
            var style = StyleFor(1f, LookFor(true));
            PaintOnce(line, Whole(line, style), style);
            Holding(NoKeys, () =>
            {
                Assert.True(line.KeyOn(VK.VK_DOWN, WidgetSurface.List));
                Assert.Equal("id0", line.Keyed);
                Assert.True(line.KeyOn(VK.VK_DOWN, WidgetSurface.List));
                Assert.Equal("id1", line.Keyed);
            });
            Holding(Alt, () =>
            {
                Assert.True(line.KeyOn(VK.VK_DOWN, WidgetSurface.List));
                Assert.Equal(["move 1->2"], desktops.Said);
                Assert.Equal("id1", line.Keyed);
                // The last has nowhere further: taken, and nothing moves.
                Assert.True(line.KeyOn(VK.VK_DOWN, WidgetSurface.List));
                Assert.Equal(["move 1->2"], desktops.Said);
                Assert.True(line.KeyOn(VK.VK_UP, WidgetSurface.List));
                Assert.Equal(["move 1->2", "move 2->1"], desktops.Said);
            });
            Holding(NoKeys, () =>
            {
                Assert.True(line.KeyOn(VK.VK_F2, WidgetSurface.List));
                Assert.Equal("id1", line.Renaming);
                Assert.True(line.KeyOn(VK.VK_ESCAPE, WidgetSurface.List));
                // Enter is the folder's: it clicks the line with no part, the row the keys are on.
                Assert.False(line.KeyOn(VK.VK_RETURN, WidgetSurface.List));
                Assert.Equal(IslandView.Desktops, line.Click(0).View);
                Assert.Equal(1, line.TakeSwitch());
                // Down past "New desktop" is the folder's again.
                Assert.True(line.KeyOn(VK.VK_DOWN, WidgetSurface.List));
                Assert.True(line.KeyOn(VK.VK_DOWN, WidgetSurface.List));
                Assert.Equal(DesktopsLine.AddKey, line.Keyed);
                Assert.Equal(default, line.Click(0));
                Assert.Contains("add", desktops.Said);
                Assert.True(line.KeyOn(VK.VK_DOWN, WidgetSurface.List) is false);
            });
        });
    }

    /// <summary>Draws the line once, as the folder does: what the keys and the slides read.</summary>
    private static void PaintOnce(DesktopsLine line, D2D_RECT_F box, in TileStyle style)
    {
        var look = style;
        Offscreen.Bitmap(box.right - box.left, box.bottom - box.top, dc => line.PaintOn(dc, box, look, WidgetSurface.List), _ => { });
    }

    // --- In the open folder ------------------------------------------------------------------------------

    /// <summary>
    /// In a real folder window: a drag by the gripper slides the rows on the folder's quick tick, and
    /// Escape puts the row back without closing the folder; let go, the desktop is moved. A click on a
    /// row closes the folder — without handing the focus back, the window that had it being on the
    /// desktop left — and hands the switch to the dock.
    /// </summary>
    [UiFact]
    public void InTheOpenFolderADragSlidesAndAClickSwitches()
    {
        WindowShots.OnHiddenDesktop("desktops-folder", (gfx, instance) =>
        {
            var desktops = FakeDesktops.Of("One", "Two", "Three");
            using var folder = Made(gfx, desktops);
            using var flyout = new FolderFlyout(gfx, instance, 0);
            flyout.Show(gfx, LookFor(true), 1f, Anchor, Work, Edge.Bottom, folder, FlyoutAlign.Start);
            var line = flyout.LineOnScreen(0);
            LPARAM At(int x, int y)
            {
                var point = new POINT { x = line.left + x, y = line.top + y };
                Assert.True(ScreenToClient(flyout.Hwnd, &point));
                return (LPARAM)(nint)((point.y << 16) | (point.x & 0xFFFF));
            }
            // The first row's gripper: the leading column, the row's middle.
            var gripX = (int)(FolderFlyout.RowInset(14) + (FolderFlyout.RowIcon(14) / 2));
            _ = flyout.Message(WM.WM_MOUSEMOVE, 0, At(gripX, 24));
            _ = flyout.Message(WM.WM_LBUTTONDOWN, 1, At(gripX, 24));
            _ = flyout.Message(WM.WM_MOUSEMOVE, 1, At(gripX, 70));
            Assert.True(flyout.Quick, "the rows making room are not drawn on the quick tick");
            var escape = flyout.Message(WM.WM_KEYDOWN, VK.VK_ESCAPE, 0);
            Assert.Equal(FolderFlyoutAction.None, escape.Kind);
            Assert.True(flyout.IsOpen, "Escape during a drag closed the folder");
            _ = flyout.Message(WM.WM_LBUTTONUP, 0, At(gripX, 70));
            Assert.Empty(desktops.Said);

            _ = flyout.Message(WM.WM_LBUTTONDOWN, 1, At(gripX, 24));
            _ = flyout.Message(WM.WM_MOUSEMOVE, 1, At(gripX, 70));
            _ = flyout.Message(WM.WM_LBUTTONUP, 0, At(gripX, 70));
            Assert.Equal(["move 0->1"], desktops.Said);
            Assert.True(flyout.IsOpen);

            // Alt+Down comes as a system key: the line the keys are on still moves its row. Regression:
            // the folder heard WM_KEYDOWN alone, so Alt+Up and Alt+Down never reached a line (a to-do's
            // neither).
            desktops.Said.Clear();
            Holding(NoKeys, () =>
            {
                _ = flyout.Message(WM.WM_KEYDOWN, VK.VK_DOWN, 0);
                _ = flyout.Message(WM.WM_KEYDOWN, VK.VK_DOWN, 0);
            });
            var keyed = ((DesktopsLine)folder.Tiles[0].Island).Keyed;
            var from = desktops.Desks.FindIndex(desk => desk.Id == keyed);
            Holding(Alt, () => _ = flyout.Message(WM.WM_SYSKEYDOWN, VK.VK_DOWN, 0));
            Assert.Equal([$"move {from}->{from + 1}"], desktops.Said);
            Assert.True(flyout.IsOpen);

            // A click on the third row's name.
            _ = flyout.Message(WM.WM_MOUSEMOVE, 0, At(120, 104));
            _ = flyout.Message(WM.WM_LBUTTONDOWN, 1, At(120, 104));
            var clicked = flyout.Message(WM.WM_LBUTTONUP, 0, At(120, 104));
            Assert.Equal(FolderFlyoutAction.Forward, clicked.Kind);
            Assert.Equal(IslandView.Desktops, clicked.Action.View);
            Assert.False(flyout.IsOpen);
            Assert.Equal(2, ((DesktopsLine)clicked.Widget!).TakeSwitch());
        });
    }

    // --- The face ----------------------------------------------------------------------------------------

    /// <summary>
    /// The face is the switcher's button: its glyph and the desktop's name across a bar, as long as the
    /// name up to the room of "Desktop 88", past which it is cut (docs\widgets.md §244) — "X" a short
    /// face — and the glyph alone in a square down one on its side. Its tooltip the whole name and
    /// where it stands.
    /// </summary>
    [UiFact]
    public void TheFaceIsAsLongAsTheNameUpToTheSwitchersRoom()
    {
        Offscreen.OnStaThread(gfx =>
        {
            using var island = Made(gfx, FakeDesktops.Of("One"));
            foreach (var height in new[] { 32, 48 })
            {
                var config = new Config { Height = height };
                var style = new IslandStyle(1.5f, false, Layout.CellPx(144, config), Layout.IconPx(144, config), LookFor(dark: true));
                var fit = new Fit(style);
                // The button's ends: a text widget's, never under 8 DIP (§506).
                var pad = 2 * MathF.Max(fit.Edge, fit.Px(8));
                var glyph = fit.Glyph() + fit.Gap;
                var room = pad + glyph + TextLine.Template(gfx, Switchers.NameRoom, fit.Primary, Weight.Regular).Width;
                _ = DesktopState.Set(["A desktop with a very long name"], 0);
                island.Forget();
                var (wide, _) = island.CellSize(0, style);
                _ = DesktopState.Set(["X"], 0);
                island.Forget();
                var (narrow, _) = island.CellSize(0, style);
                var x = pad + glyph + TextLine.Measure(gfx, "X", fit.Primary, Weight.Regular, null);
                Assert.Equal(MathF.Ceiling(room), wide, 1f);
                Assert.Equal(MathF.Ceiling(x), narrow, 1f);
                var down = style with { Vertical = true };
                var (across, along) = island.CellSize(0, down);
                Assert.Equal(FaceWidthTests.GlyphButton(down), along, 0.5f);
            }
            _ = DesktopState.Set(["Work", "Games"], 1);
            Assert.Equal(Switchers.DesktopTooltip("Games", 1, 2), island.Tooltip(0));
        });
    }

    // --- Pictures ----------------------------------------------------------------------------------------

    /// <summary>
    /// The preset as it opens at 150 %, to stand beside quick-desktops-*.png: at rest, a row under the
    /// pointer with its buttons, the keys on a row, a row lifted half way, a name typed, without the
    /// shell, held short, and at 225 % text.
    /// </summary>
    [UiFact]
    public void DesktopsPresetPictures()
    {
        foreach (var dark in new[] { true, false })
        {
            Offscreen.OnStaThread(gfx =>
            {
                var folders = new List<FolderIsland>();
                var held = KeyState.Held;
                KeyState.Held = NoKeys;
                try
                {
                    var panels = new List<(string, Look, FolderIsland, FolderPlan)>();
                    void Add(string caption, Action<FakeDesktops>? setUp = null, Action<DesktopsLine, TileStyle>? set = null, float text = 1f, int? rows = null)
                    {
                        var look = LookFor(dark, text);
                        var desktops = FakeDesktops.Of("Desktop 1", "Work", "A desktop with a very long name indeed", "Desktop 4");
                        desktops.Current = 1;
                        setUp?.Invoke(desktops);
                        var holder = Holder();
                        if (rows is { } short_)
                        {
                            holder.Folder!.Lists[0][0].Island.Height = LineHeight.Exactly(short_ * LineHeight.Row);
                        }
                        var island = Made(gfx, desktops, $"{Owner}/{holder.Id}-{panels.Count}", holder);
                        folders.Add(island);
                        island.Opened(gfx);
                        var line = (DesktopsLine)island.Tiles[0].Island;
                        var style = StyleFor(1.5f, look);
                        line.Ticks = static () => 0;
                        set?.Invoke(line, style);
                        panels.Add((caption, look, island, Plan(island, look, 1.5f).Plan));
                    }

                    Add("At rest");
                    Add("A row under the pointer", set: (line, style) =>
                    {
                        var box = Whole(line, style);
                        line.CardHover(Middle(Piece(line, style, "desktop 2")), box, style, WidgetSurface.List);
                    });
                    Add("The keys on a row", set: (line, _) =>
                    {
                        line.KeyOn(VK.VK_DOWN, WidgetSurface.List);
                        line.KeyOn(VK.VK_DOWN, WidgetSurface.List);
                    });
                    Add("Lifted, two rows down", set: (line, style) =>
                    {
                        var box = Whole(line, style);
                        var grip = Middle(Piece(line, style, "grip 0"));
                        line.CardHover(grip, box, style, WidgetSurface.List);
                        line.PressOn(grip, box, style, WidgetSurface.List);
                        // Past the slide's end: the rows have made room.
                        line.DragOn(grip + new Vector2(0, 2.2f * 60), box, style, WidgetSurface.List);
                        line.Ticks = static () => 1000;
                    });
                    Add("Renaming", set: (line, style) =>
                    {
                        _ = Click(line, style, "rename 1");
                    });
                    Add("Without the shell", setUp: desktops => desktops.Shell = false, set: (line, style) =>
                    {
                        var box = Whole(line, style);
                        line.CardHover(Middle(Piece(line, style, "desktop 0")), box, style, WidgetSurface.List);
                    });
                    Add("Twelve held to six rows", setUp: desktops =>
                    {
                        for (var i = 5; i <= 12; i++)
                        {
                            desktops.Desks.Add(($"id{i}", "", 0));
                        }
                    }, rows: 6);
                    Add("225 % text", text: 2.25f);
                    Sheet(gfx, $"desktops-preset-{(dark ? "dark" : "light")}.png", 1.5f, panels);
                }
                finally
                {
                    KeyState.Held = held;
                    foreach (var island in folders)
                    {
                        island.Closed();
                        island.Dispose();
                    }
                }
            });
        }
    }

    /// <summary>The face on bars of 40 and 48 DIP and at 150 % — desktops-face-&lt;dark|light&gt;.png.</summary>
    [UiFact]
    public void DesktopsFacePictures()
    {
        foreach (var dark in new[] { true, false })
        {
            Offscreen.OnStaThread(gfx =>
            {
                var look = LookFor(dark);
                var theme = FlyoutTheme.For(look);
                _ = DesktopState.Set(["Desktop 1", "Work", "A desktop with a very long name"], 1);
                using var island = Made(gfx, FakeDesktops.Of("One"));
                var styles = new[] { (40, 96u, false), (48, 96u, false), (48, 144u, false), (48, 144u, true) }
                    .Select(each => new IslandStyle(each.Item2 / 96f, each.Item3, Layout.CellPx(each.Item2, new Config { Height = each.Item1 }), Layout.IconPx(each.Item2, new Config { Height = each.Item1 }), look))
                    .ToList();
                const float gap = 12;
                var width = gap + styles.Sum(style => island.CellSize(0, style).Width + gap);
                var height = gap + styles.Max(style => island.CellSize(0, style).Height) + gap;
                Offscreen.Bitmap(width, height, dc =>
                {
                    PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, width, height), 0, PopupPaint.Ink(theme.Base));
                    var x = gap;
                    foreach (var style in styles)
                    {
                        var (w, h) = island.CellSize(0, style);
                        PopupPaint.Fill(dc, PopupPaint.Rect(x, gap, w, h), 4 * style.Scale, PopupPaint.Ink(theme.CardBackground));
                        Offscreen.Shifted(dc, x, gap, () => island.PaintCell(dc, 0, style, w, h));
                        x += w + gap;
                    }
                }, bitmap => Offscreen.Save(bitmap, Path.Combine(Offscreen.Pictures, $"desktops-face-{(dark ? "dark" : "light")}.png")));
            });
        }
    }
}
