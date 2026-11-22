// The room a folder's column has (§185; the person, 2026-10-16: "the column still has a lot of space
// at the top but it already says that it does not have any space left for some widgets"). A column
// stopped at 70 % of the work area, the share every other panel stops at, and left out the lines past
// it while the top third of the screen stood empty over it: their quick settings column of twenty
// lines on a 1440p screen at 150 % left out the volume and Edit / All settings under 974 DIP of the
// 1392. Now every folder reaches the room beside the bar (FlyoutPlacement.Room), as a Fill line's
// already did (§173), less an arranged list's "+" (FolderFlyout.Reach), so that "+", the width and
// the warning stay on the screen; a line is left out only where it truly does not fit there, and
// nothing scrolls.
//
// The plan's half is pure; the open folder's needs Direct2D and a window station (KARPO_UI_TESTS=1),
// and pictures it to KARPO_PNG_DIR: folder-column-room-<rest|added|edit>.png.

using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using Color = Windows.UI.Color;

namespace Karpo.Tests.Folders;

public sealed class FolderColumnRoomTests
{
    private static readonly Look Dark = new(Glass: false, Dark: true, Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4));

    /// <summary>The person's quick settings column (their config, 2026-10-16), each line's kind and its fixed rows; 0 for one left automatic.</summary>
    private static readonly (ListKind Kind, int Rows)[] TheirColumn =
    [
        (ListKind.Clock, 3), (ListKind.Memory, 2), (ListKind.Cpu, 2), (ListKind.Network, 2),
        (ListKind.Narrator, 1), (ListKind.Magnifier, 1), (ListKind.Brightness, 1), (ListKind.Divider, 0),
        (ListKind.Media, 2), (ListKind.Divider, 0), (ListKind.WifiSwitch, 1), (ListKind.BluetoothSwitch, 1),
        (ListKind.Accessibility, 1), (ListKind.LiveCaptions, 1), (ListKind.Cast, 1), (ListKind.Project, 1),
        (ListKind.Toggles, 2), (ListKind.Brightness, 1), (ListKind.SoundVolume, 1), (ListKind.Actions, 0),
        // §442: the finer grid made their column 1248 px, under the 70 % on its own; a note of six rows takes it past.
        (ListKind.Note, 6),
    ];

    /// <summary>
    /// A line of <paramref name="kind"/>, <paramref name="rows"/> rows tall where that is more than none;
    /// one that hides while it has nothing to show set to stay, whatever this PC plays (§327).
    /// </summary>
    private static CellItem Line(ListKind kind, int rows)
    {
        var line = kind.New();
        line.HideIdle = line.IdleKey is not null ? false : null;
        if (rows > 0)
        {
            line.Height = LineHeight.Exactly(rows * LineHeight.Row);
        }
        return line;
    }

    /// <summary>Their folder: an empty first column and their column second, as their config has it.</summary>
    private static FolderOptions Theirs(params (ListKind Kind, int Rows)[] more)
    {
        var options = new FolderOptions { Lists = [[], []] };
        foreach (var (kind, rows) in TheirColumn.Concat(more))
        {
            options.Put(Line(kind, rows), 1);
        }
        return options;
    }

    /// <summary><paramref name="count"/> lines of one row each (40 px and 2 between at 100 %) in one column.</summary>
    private static FolderOptions Lines(int count)
    {
        var options = new FolderOptions { Lists = [[]] };
        for (var at = 0; at < count; at++)
        {
            options.Put(ListKind.EnergySaver.New(), 0);
        }
        return options;
    }

    private static FolderPlan Plan(FolderOptions options, float scale, float work, float room = 0, Edge edge = Edge.Bottom, bool editing = false) =>
        FolderFlyout.Build(Dark, scale, FolderView.List, options, options.Cells, work, editing: editing, edge: edge, room: room);

    /// <summary>
    /// The regression: their column on their screen — 1440p at 150 % (2160 px), a 48-DIP bar — shows
    /// every line, the volume and Edit / All settings too, where the 70 % (1461 px) left them out; and
    /// it stands under the room with its "+" still on the screen.
    /// </summary>
    [Fact]
    public void TheirColumnShowsEveryLineOnTheirScreen()
    {
        var work = 2160f - 72;
        var anchor = new POINT { x = 3400, y = 2088 };
        var room = new FlyoutPlacement(anchor, new RECT { left = 0, top = 0, right = 3840, bottom = 2088 }, Edge.Bottom).Room(1.5f);
        var plan = Plan(Theirs(), 1.5f, work, room);

        Assert.Empty(plan.Hidden);
        Assert.True(plan.Height > MathF.Floor(work * 0.7f), $"{plan.Height} px: under the 70 % it would have fitted before");
        Assert.True(plan.Height <= FolderFlyout.Reach(work, room, 1.5f), $"{plan.Height} px of {room}");
    }

    /// <summary>
    /// A line is left out only where it truly does not fit: lines up to the reach all show, the one
    /// past it goes; at 100 and 150 % and with the room a placement gives or the one drawn without.
    /// </summary>
    [Theory]
    [InlineData(1f, 1032f, 0f)]
    [InlineData(1f, 1032f, 900f)]
    [InlineData(1.5f, 2088f, 0f)]
    [InlineData(1.5f, 1548f, 1500f)]
    public void ALineGoesOnlyWhereItDoesNotFit(float scale, float work, float room)
    {
        var reach = FolderFlyout.Reach(work, room, scale);
        var step = Plan(Lines(2), scale, work, room).Cards[1].top;
        var fits = (int)MathF.Floor((reach + (step - MathF.Round(40 * scale))) / step);

        var full = Plan(Lines(fits), scale, work, room);
        Assert.Empty(full.Hidden);
        Assert.True(full.Height <= reach + 0.5f, $"{full.Height} of {reach}");
        Assert.True(reach - full.Height < step, $"{reach - full.Height} px left, a line is {step}");

        var over = Plan(Lines(fits + 1), scale, work, room);
        Assert.Equal([fits], over.Hidden);
        Assert.Equal(full.Height, over.Height, 1);
    }

    /// <summary>
    /// Arranged, a column as tall as it may be has its "+" and its warning inside the room on the
    /// bar's far side, on a bottom and a top bar — just inside its own far end, as the room keeps no
    /// row past it (§320) — and its lines stand where they stood at rest.
    /// </summary>
    [Theory]
    [InlineData(Edge.Bottom)]
    [InlineData(Edge.Top)]
    public void AFullColumnKeepsItsButtonsInTheRoom(Edge edge)
    {
        var room = 1008f;
        var rest = Plan(Lines(40), 1f, 1032, room, edge);
        var arranged = Plan(Lines(40), 1f, 1032, room, edge, editing: true);

        Assert.NotEmpty(arranged.Hidden);
        Assert.Equal(rest.Cards, arranged.Cards);
        Assert.Contains(arranged.Adds, add => add.Kind == FolderAddKind.Hidden);
        // The room where the panel stands against the bar, in panel px.
        var inside = edge == Edge.Top
            ? new TerraFX.Interop.DirectX.D2D_RECT_F { left = -400, top = -12, right = arranged.Width + 400, bottom = room }
            : new TerraFX.Interop.DirectX.D2D_RECT_F { left = -400, top = arranged.Height - room, right = arranged.Width + 400, bottom = arranged.Height + 12 };
        foreach (var add in FolderFlyout.WithSlots(arranged, 1, inside, edge).Adds.Where(add => add.List >= 0))
        {
            // The panel stands against the bar, its far end <Height> away: the buttons inside <room>.
            Assert.True(edge == Edge.Top ? add.Box.bottom <= room : add.Box.top >= arranged.Height - room, $"{add.Kind} at {add.Box.top}..{add.Box.bottom} of {arranged.Height} in {room}");
        }
    }

    /// <summary>
    /// The ends: a first line taller than the most is given the most (ListMost) and nothing is left out for
    /// it; a work area too short for one line still shows one with its padding; a work area taller
    /// than any screen (a sheet) keeps at least its 70 %, so nothing is left out there.
    /// </summary>
    [Fact]
    public void TheEnds()
    {
        var tall = new FolderOptions { Lists = [[]] };
        tall.Put(Line(ListKind.Clipboard, FolderOptions.MostCells), 0);
        var given = Plan(tall, 1f, 300);
        Assert.Empty(given.Hidden);
        Assert.Equal(FolderFlyout.ListMost(300, 0, 1), given.Cards[0].bottom - given.Cards[0].top, 1);

        var tiny = Plan(Lines(3), 1f, 60);
        Assert.True(tiny.Shows(0));
        Assert.Equal(40, tiny.Height, 1);

        var sheet = Plan(Lines(200), 1f, 100_000, room: 1);
        Assert.Empty(sheet.Hidden);
    }

    /// <summary>
    /// The open folder: their column on a 1440p screen at 150 %, at rest showing every line with the
    /// room over it, then a two-row line added at its top that fits there too, then arranged: no
    /// warning, and the "+" on the screen.
    /// </summary>
    [UiFact]
    public void TheirOpenColumnTakesAWidgetThatFits()
    {
        WindowShots.OnHiddenDesktop("folder-column-room", (gfx, instance) =>
        {
            const float scale = 1.5f;
            var work = new RECT { left = 0, top = 0, right = 3840, bottom = 2088 };
            var anchor = new POINT { x = 3400, y = 2088 };
            var holder = BarKind.FolderList.New();
            holder.Folder!.Lists = [[], []];
            foreach (var (kind, rows) in TheirColumn)
            {
                holder.Folder.Put(Line(kind, rows), 1);
            }
            var config = new Config();
            using var folder = new FolderIsland($"layout:default/{holder.Id}", holder, gfx, config);
            using var flyout = new FolderFlyout(gfx, instance, 0);
            var reach = FolderFlyout.Reach(0, new FlyoutPlacement(anchor, work, Edge.Bottom).Room(scale), scale);

            flyout.Show(gfx, TestLooks.LookFor(true), scale, anchor, work, Edge.Bottom, folder);
            WindowShots.Settle(300);
            var rest = flyout.Plan!;
            Assert.Empty(rest.Hidden);
            Assert.True(rest.Height > MathF.Floor((work.bottom - work.top) * 0.7f), $"{rest.Height} px");
            Assert.True(WindowShots.Save(flyout.Hwnd, "folder-column-room-rest.png"));

            // A widget added at the column's top, as its "+" adds on a bottom bar: it fits, so it shows.
            Assert.NotNull(Karpo.Core.Folders.InsertLine(BarKind.FolderList, holder.Folder, Line(ListKind.Gpu, 2), new FolderPlace(1, 0)));
            folder.Configure(holder, config);
            WindowShots.Settle(300);
            var added = flyout.Plan!;
            Assert.Empty(added.Hidden);
            Assert.True(added.Height > rest.Height + 1 && added.Height <= reach, $"{added.Height} after {rest.Height}, of {reach}");
            Assert.True(WindowShots.Save(flyout.Hwnd, "folder-column-room-added.png"));
            flyout.Hide();

            flyout.Show(gfx, TestLooks.LookFor(true), scale, anchor, work, Edge.Bottom, folder, editing: true);
            WindowShots.Settle(300);
            var arranged = flyout.Plan!;
            Assert.DoesNotContain(arranged.Adds, add => add.Kind == FolderAddKind.Hidden);
            Assert.True(WindowShots.Save(flyout.Hwnd, "folder-column-room-edit.png"));
            flyout.Hide();
        });
    }
}
