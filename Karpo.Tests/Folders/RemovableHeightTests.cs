// The removable devices' line has the height every folder widget has (docs\widgets.md §76, §91): the
// person, 2026-10-11, "the normal height setting like other folder widgets". Automatic grows a row per
// device up to its most and scrolls past it; Fixed is that many rows whatever is plugged in, the
// devices sharing them and scrolling past them (ListScroll.cs); nothing plugged in is the kit's one
// Status row at the line's top (§401). Settings shows "Height in the folder" for it, and a file's height reads as it was.
//
// Made-up devices throughout (DriveWatch.Source): nothing reaches a real volume. The sheet,
// removable-heights-<dark|light>.png, goes to KARPO_PNG_DIR.

using Karpo.Dock;
using Karpo.Dock.Drives;
using Karpo.Tests.Islands;
using Karpo.Tests.Storage;
using static Karpo.Tests.Folders.ListFormPictures;

namespace Karpo.Tests.Folders;

[Collection(AttachedDrivesCollection.Collection)]
public sealed class RemovableHeightTests
{
    private const float Scale = 1.5f;

    private static Drive Device(int letter) => new()
    {
        Letter = letter,
        Kind = DriveKind.Removable,
        Volume = $@"\\?\Volume{{7e57{letter:D4}-0000-0000-0000-000000000000}}\",
        Label = $"Stick {letter - 3}",
        Total = 32UL << 30,
        Free = 12UL << 30,
    };

    private static Drive[] Devices(int count) => [.. Enumerable.Range(4, count).Select(Device)];

    /// <summary>Stands in for what is plugged in and the devices' pictures while it lives.</summary>
    private sealed class Plugged : IDisposable
    {
        private readonly Func<bool, bool, IReadOnlyList<Drive>>? drives = DriveWatch.Source;
        private readonly Func<string, int, Pixels?>? icons = DriveIcons.Source;

        public Plugged(Drive[] devices)
        {
            DriveWatch.Source = (_, _) => devices;
            DriveIcons.Source = (_, _) => null;
            DriveWatch.Forget();
        }

        public void Dispose()
        {
            DriveWatch.Source = drives;
            DriveIcons.Source = icons;
            DriveWatch.Forget();
        }
    }

    // --- The setting ----------------------------------------------------------------------------

    /// <summary>
    /// The line has the height card, as every folder widget whose line is its own card: it is not a
    /// line of a fixed height, it may be more than one row, and a new one is automatic up to its most.
    /// </summary>
    [Fact]
    public void TheLineHasTheHeightCard()
    {
        var line = ListWidgets.Of(ListKind.RemovableDevices);
        Assert.False(line.FixedHeight);
        Assert.True(line.Range.Most > 1);
        Assert.True(ListKind.RemovableDevices.HasHeight());
        Assert.NotNull(LineHeightRows.For(ListKind.RemovableDevices.New()));
        Assert.Equal(LineHeight.Automatic(line.Range.Most), ListKind.RemovableDevices.New().Height);
        Assert.Equal(LineHeight.Exactly(6), LineHeight.For(new CellItem { Kind = "removableDevices", Height = LineHeight.Exactly(6) }));
    }

    /// <summary>
    /// A file's removable-devices line reads as it was written — automatic, fixed, none at all, or
    /// words this build does not know — with nothing else of the folder lost, and
    /// reads back the same once saved.
    /// </summary>
    [Theory]
    [InlineData("\"height\": { \"fixed\": false, \"cells\": 24 }", false, 24)]
    [InlineData("\"height\": { \"fixed\": true, \"cells\": 7 }", true, 7)]
    [InlineData("\"height\": { \"fixed\": true, \"cells\": 198 }", true, 198)]
    [InlineData("\"height\": { \"fixed\": true, \"cells\": 1998 }", true, LineHeight.Most)]
    [InlineData("\"height\": { \"fixed\": true, \"cells\": 8, \"tall\": \"yes\" }, \"what\": 1", true, 8)]
    [InlineData("\"other\": true", null, null)]
    public void AFilesHeightReadsAsItWas(string said, bool? fixedHeight, int? rows)
    {
        var config = Karpo.Tests.Core.Configs.Repaired($$"""
            { "version": {{Config.Shape}}, "layouts": [ { "id": "default", "name": "Default", "islands": [
              { "id": "r", "kind": "folderList",
                "folder": { "face": { "kind": "removable", "options": {} }, "lists": [ [
                  { "island": { "id": "a", "kind": "removableDevices", {{said}} } },
                  { "island": { "id": "d", "kind": "divider" } },
                  { "island": { "id": "c", "kind": "calendar", "height": { "fixed": true, "cells": 10 } } } ] ] } } ] } ] }
            """);
        var folder = config.Layouts[0].Islands.Single(island => island.Id == "r").Folder!;
        Assert.Equal(FolderFaces.RemovableId, folder.Face.Kind);
        Assert.Equal(["a", "d", "c"], folder.Cells.Select(cell => cell.Island.Id));
        var line = folder.Cells[0].Island;
        var expected = fixedHeight is { } mode ? new LineHeight(mode, rows!.Value) : LineHeight.StartFor(ListKind.RemovableDevices);
        Assert.Equal(expected, LineHeight.For(line));
        Assert.Equal(LineHeight.Exactly(10), folder.Cells[2].Island.Height);

        var again = Karpo.Tests.Core.Configs.RoundTrip(config, out var changed);
        Assert.False(changed);
        Assert.Equal(expected, LineHeight.For(again.Layouts[0].Islands.Single(island => island.Id == "r").Folder!.Cells[0].Island));
    }

    // --- On the bar's folder ----------------------------------------------------------------------

    /// <summary>
    /// Automatic up to twelve: a row for nothing plugged in, a device's row (the kit's row with its
    /// caption, 52 DIP, §401) for one device, five for five, every device shown and nothing to scroll.
    /// </summary>
    [UiTheory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    public void AutomaticGrowsARowPerDevice(int count)
    {
        using var plugged = new Plugged(Devices(count));
        Offscreen.OnStaThread(gfx =>
        {
            var (given, board, scrolls) = Laid(gfx, LineHeight.Automatic(24));
            Assert.Equal(count == 0 ? Rows(1, Scale, 1f) : count * 3 * FolderFlyout.CellPx(Scale, 1f), given, 1f);
            Assert.Equal(count, board.Rows.Count);
            Assert.False(scrolls);
            Assert.Equal(count == 0, board.Empty is not null);
        });
    }

    /// <summary>Automatic up to three with five plugged in: three rows, and the five scroll through them, each with its button.</summary>
    [UiFact]
    public void AutomaticStopsAtItsMostAndScrolls()
    {
        using var plugged = new Plugged(Devices(5));
        Offscreen.OnStaThread(gfx =>
        {
            var (given, board, scrolls) = Laid(gfx, LineHeight.Automatic(6));
            Assert.Equal(Rows(3, Scale, 1f), given, 0.5f);
            Assert.True(scrolls);
            Assert.Equal(5, board.Rows.Count);
            Assert.All(board.Rows, row => Assert.True(TilePaint.Width(row.Eject) > 0));
        });
    }

    /// <summary>
    /// Fixed at three: three rows whatever is plugged in — nothing said in the middle of them, one
    /// device in them, five scrolling through them.
    /// </summary>
    [UiTheory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(5, true)]
    public void FixedIsThatManyRowsWhateverIsPluggedIn(int count, bool scrolling)
    {
        using var plugged = new Plugged(Devices(count));
        Offscreen.OnStaThread(gfx =>
        {
            var (given, board, scrolls) = Laid(gfx, LineHeight.Exactly(6));
            Assert.Equal(Rows(3, Scale, 1f), given, 0.5f);
            Assert.Equal(count, board.Rows.Count);
            Assert.Equal(scrolling, scrolls);
        });
    }

    /// <summary>Fixed at five with five plugged in: each its row, nothing to scroll.</summary>
    [UiFact]
    public void FixedAtAsManyAsThereAreShowsThemAll()
    {
        using var plugged = new Plugged(Devices(5));
        Offscreen.OnStaThread(gfx =>
        {
            var (_, board, scrolls) = Laid(gfx, LineHeight.Exactly(10));
            Assert.Equal(5, board.Rows.Count);
            Assert.False(scrolls);
        });
    }

    /// <summary>
    /// Nothing plugged in is the kit's one Status row (§401) at the line's top, never centred: the glyph in
    /// the list's leading column, the words where every name begins, both on the top row's middle — at
    /// one row and at four, at 100 % and 225 % text.
    /// </summary>
    [UiFact]
    public void NothingPluggedInIsOneStatusRowAtTheTop()
    {
        using var plugged = new Plugged([]);
        Offscreen.OnStaThread(gfx =>
        {
            foreach (var text in TextScales)
            {
                foreach (var height in new[] { LineHeight.Exactly(2), LineHeight.Exactly(8), LineHeight.Automatic(24) })
                {
                    var look = LookFor(dark: true, text);
                    var folder = Folder(gfx, Line(ListKind.RemovableDevices, island => island.Height = height));
                    try
                    {
                        var (_, plan) = Plan(folder, look, Scale);
                        var box = PopupPaint.Rect(0, 0, plan.Cards[0].right - plan.Cards[0].left, Given(plan, 0));
                        var line = Assert.IsType<RemovableIsland>(folder.Tiles[0].Island);
                        var style = FolderFlyout.StyleFor(Scale, look, FlyoutTheme.For(look));
                        var form = ListForm.Of(gfx, style);
                        var empty = line.Board(box, style).Empty!.Value;
                        var words = Assert.Single(empty.Words);
                        var where = $"{height}, text {text * 100:0} %";
                        var middle = MathF.Min(TilePaint.Height(box), LineRows.Px(1, style)) / 2;
                        Assert.True(MathF.Abs(empty.Glyph.left - form.Inset) <= 1, $"{where}: the glyph is not in the leading column");
                        Assert.True(MathF.Abs(words.Left - form.TextLeft) <= 1, $"{where}: the words do not begin where every name does");
                        Assert.True(MathF.Abs(((empty.Glyph.top + empty.Glyph.bottom) / 2) - middle) <= 1, $"{where}: not on the top row's middle");
                    }
                    finally
                    {
                        folder.Closed();
                        folder.Dispose();
                    }
                }
            }
        });
    }

    /// <summary>
    /// Automatic up to twelve and up to three, and fixed at three, with nothing, one and five plugged
    /// in: removable-heights-&lt;dark|light&gt;.png.
    /// </summary>
    [UiFact]
    public void TheHeightsArePictured()
    {
        var plugged = Devices(0);
        var drives = DriveWatch.Source;
        var icons = DriveIcons.Source;
        DriveWatch.Source = (_, _) => plugged;
        DriveIcons.Source = (_, _) => null;
        try
        {
            foreach (var dark in new[] { true, false })
            {
                Offscreen.OnStaThread(gfx =>
                {
                    var panels = new List<(string, Look, FolderIsland, FolderPlan)>();
                    var folders = new List<FolderIsland>();
                    try
                    {
                        foreach (var (caption, height) in new[] { ("Automatic, up to 12", LineHeight.Automatic(24)), ("Automatic, up to 3", LineHeight.Automatic(6)), ("Fixed, 3 rows", LineHeight.Exactly(6)) })
                        {
                            foreach (var count in new[] { 0, 1, 5 })
                            {
                                plugged = Devices(count);
                                DriveWatch.Forget();
                                var look = LookFor(dark);
                                var folder = Folder(gfx, Line(ListKind.RemovableDevices, island => island.Height = height));
                                folders.Add(folder);
                                panels.Add(($"{caption}: {count} plugged in", look, folder, Plan(folder, look, Scale).Plan));
                            }
                        }
                        Sheet(gfx, $"removable-heights-{(dark ? "dark" : "light")}.png", Scale, panels);
                    }
                    finally
                    {
                        foreach (var folder in folders)
                        {
                            folder.Closed();
                            folder.Dispose();
                        }
                    }
                });
            }
        }
        finally
        {
            DriveWatch.Source = drives;
            DriveIcons.Source = icons;
            DriveWatch.Forget();
        }
    }

    /// <summary>The line alone in a list folder at <paramref name="height"/>: the height the plan gave it, and its devices laid out in it.</summary>
    /// <summary>The line in the height the plan gives it: that height, its devices as laid out — all of them where it scrolls — and whether it scrolls.</summary>
    private static (float Given, RemovableIsland.DeviceBoard Board, bool Scrolls) Laid(Gfx gfx, LineHeight height)
    {
        var look = LookFor(dark: true);
        var folder = Folder(gfx, Line(ListKind.RemovableDevices, island => island.Height = height));
        try
        {
            var (_, plan) = Plan(folder, look, Scale);
            var box = PopupPaint.Rect(0, 0, plan.Cards[0].right - plan.Cards[0].left, Given(plan, 0));
            var line = Assert.IsType<RemovableIsland>(folder.Tiles[0].Island);
            var style = FolderFlyout.StyleFor(Scale, look, FlyoutTheme.For(look));
            var laid = line.Scrolled(box, style, WidgetSurface.List);
            return (Given(plan, 0), line.Board(laid.Moved, style), laid.Scrolls);
        }
        finally
        {
            folder.Closed();
            folder.Dispose();
        }
    }
}
