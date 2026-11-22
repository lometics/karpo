// §629 (the person, 2026-11-09: "the add widget modal should be semi transparent in case the ghost preview is directly behind it.
// but it should only be semi transparent for the ghost preview and not for everything actually."): the add page over its ghost,
// a display 1920 × 1080 DIP at 150 % in edit mode as AddGhostSheets has it. To KARPO_PNG_DIR as seethrough-<scene>-<dark|light>.png:
//
//  - air: the air quality under the pointer on a bar's "+"'s page: its folder a ghost open over its face, its top lines under
//    the page — shown through the page there, half, the tiles over them as they are;
//  - line: a single widget, a clock, a ghost among the world clock's lines, the page over the folder (no room kept clear of it):
//    its line shown through the page.
//
// Each with the page captured as it is without the ghost and with it (-off, -on): outside the ghost's rectangles not a px of it
// changed; inside, the ghost there. A tile or an expander's head over the ghost still takes the pointer and the click.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using System.Numerics;
using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    [UiFact]
    public void TheAddPageShowsItsGhostThroughItselfThereAlone()
    {
        var tilesOver = 0;
        WindowShots.OnHiddenDesktop("see-through", (gfx, instance) =>
        {
            const uint dpi = 144;
            var dips = new Vector2(1920f, 1080f);
            var machine = new PresetMachine(HasBattery: false, FixedDrives: []);
            var clock = Catalogue.On(WidgetType.List).Single(entry => entry.List == ListKind.Clock);
            var world = Presets.Find(Presets.WorldClockId)!;
            var air = Presets.Find(Presets.AirQualityId)!;
            foreach (var dark in new[] { true, false })
            {
                var look = AddListPictures.LookFor(dark) with { Animations = false };
                var mode = dark ? "dark" : "light";

                var airs = Presets.Folder(air, machine);
                EditedDisplay(gfx, instance, dpi, new Config(), look, With(airs), $"seethrough-air-{mode}.png", dips: dips,
                    panel: deck => AddPage(gfx, dpi, deck, look, WidgetType.Bar, air.Title),
                    marks: layout => Ghosted(layout, airs), folder: true, folderFace: airs.Folder!.Face.Kind, folderGhost: true,
                    inFolder: (open, deck) => tilesOver += SeenThrough(deck!, open, $"seethrough-air-{mode}"));

                var holder = Presets.Folder(world, machine);
                var inside = With(holder);
                var path = AddList.Add(inside.Islands, AddSpot.InFolder("pictures", holder.Id, new FolderPlace(0, 0)), clock)!;
                EditedDisplay(gfx, instance, dpi, new Config(), look, inside, $"seethrough-line-{mode}.png", dips: dips, folder: true, folderFace: holder.Folder!.Face.Kind,
                    inFolder: (open, deck) =>
                    {
                        open.Ghost = open.Keys.Single(each => each.EndsWith(path[path.IndexOf('/')..], StringComparison.Ordinal));
                        // The page over the folder, as where too little room is left beside it (EditDeck.AddLeastDip).
                        AddPage(gfx, dpi, deck!, look, WidgetType.List, clock.Title, PictureState(folder: S.CoreWidgetFolderListTitle));
                        tilesOver += SeenThrough(deck!, open, $"seethrough-line-{mode}");
                    });
            }
        });
        Assert.True(tilesOver > 0, "no tile stood over the ghost to take the pointer");
    }

    /// <summary>
    /// The add page of <paramref name="deck"/> over <paramref name="open"/>'s ghost (the add page's): without it and with it, the
    /// pieces it shows through its ghost's rectangles ∩ the page alone, not a px of the page changed outside them, the ghost there
    /// inside; what stands over it taking the pointer and the click. How many of it stood over it.
    /// </summary>
    private static int SeenThrough(EditDeck deck, FolderFlyout open, string name)
    {
        var sight = open.GhostSight ?? throw new InvalidOperationException($"{name}: no ghost in the folder");
        var page = deck.FlyoutOnScreen;
        var hovered = deck.Proposed;
        deck.SeeThrough([]);
        Assert.Empty(deck.SeeThroughOnScreen);
        var off = Captured(deck, $"{name}-off.png");
        deck.SeeThrough([sight]);
        List<RECT> pieces = [.. SeeThroughPlan.Of(page, [sight]).Select(cut => new RECT
        {
            left = page.left + (int)MathF.Floor(cut.At.X),
            top = page.top + (int)MathF.Floor(cut.At.Y),
            right = page.left + (int)MathF.Ceiling(cut.At.Right),
            bottom = page.top + (int)MathF.Ceiling(cut.At.Bottom),
        })];
        Assert.NotEmpty(pieces);
        Assert.Equal(pieces, deck.SeeThroughOnScreen);
        // Each the part in the page of one of the ghost's rectangles.
        foreach (var piece in pieces)
        {
            Assert.True(piece.left >= page.left && piece.top >= page.top && piece.right <= page.right && piece.bottom <= page.bottom, $"{name}: a piece past the page");
            Assert.Contains(sight.Rects, rect => sight.Origin.x + rect.X <= piece.left + 1 && sight.Origin.y + rect.Y <= piece.top + 1
                && sight.Origin.x + rect.Right >= piece.right - 1 && sight.Origin.y + rect.Bottom >= piece.bottom - 1);
        }
        var on = Captured(deck, $"{name}-on.png");
        Assert.Equal(off.Pixels.Length, on.Pixels.Length);
        var (inside, outside) = (0, 0);
        List<(int X, int Y, int Step)> changed = [];
        for (var y = 0; y < page.bottom - page.top; y++)
        {
            for (var x = 0; x < off.Width; x++)
            {
                var at = ((y * off.Width) + x) * 4;
                // The same but for a step of rounding: the page's rounded corners' antialiasing is the compositor's, a step either
                // way as its tree changes.
                var step = Enumerable.Range(0, 3).Max(channel => Math.Abs(off.Pixels[at + channel] - on.Pixels[at + channel]));
                var same = step <= Rounding;
                var (sx, sy) = (page.left + x, page.top + y);
                if (pieces.Exists(piece => sx >= piece.left - 1 && sx < piece.right + 1 && sy >= piece.top - 1 && sy < piece.bottom + 1))
                {
                    inside += same ? 0 : 1;
                }
                else if (!same)
                {
                    outside++;
                    changed.Add((x, y, step));
                }
            }
        }
        Assert.True(outside == 0, $"{name}: {outside} px of the page changed outside its ghost, at {string.Join(" ", changed.Take(12))} (page px)");
        Assert.True(inside > 0, $"{name}: the ghost does not show through the page");
        // What stands over the ghost takes the pointer as anywhere on the page: a tile under it the page's pick; an expander's
        // head clicked opens, clicked again closes — where it runs over the ghost (the middle of its part over a piece).
        Vector2? Over(RectF line)
        {
            var on = deck.Layout!.OnPanel(line);
            foreach (var piece in pieces)
            {
                var left = MathF.Max(page.left + on.X, piece.left);
                var top = MathF.Max(page.top + on.Y, MathF.Max(piece.top, page.top));
                var right = MathF.Min(page.left + on.Right, piece.right);
                var bottom = MathF.Min(page.top + on.Bottom, piece.bottom);
                if (right - left >= 4 && bottom - top >= 4)
                {
                    return new Vector2(((left + right) / 2) - page.left, ((top + bottom) / 2) - page.top);
                }
            }
            return null;
        }
        int Lines() => deck.Layout!.Blocks.Sum(block => block.Lines.Count);
        var tiles = 0;
        var over = deck.Layout!.Blocks.SelectMany(block => block.Lines).Select(line => (Line: line, At: Over(line.Rect))).Where(each => each.At is not null).ToList();
        foreach (var (tile, at) in over.Where(each => each.Line.Row is { Kind: PanelRowKind.Pick, Greyed: false }))
        {
            _ = deck.Message(deck.FlyoutHwnd, WM.WM_MOUSEMOVE, 0, EditDeckClickTests.At(deck, at!.Value, flyout: true));
            Assert.Equal(tile.Row.Item.Id, deck.Proposed);
            tiles++;
        }
        if (over.FirstOrDefault(each => each.Line.Row.Kind == PanelRowKind.Expander) is ({ } head, { } middle))
        {
            var before = Lines();
            for (var press = 0; press < 2; press++)
            {
                _ = deck.Message(deck.FlyoutHwnd, WM.WM_LBUTTONDOWN, 1, EditDeckClickTests.At(deck, middle, flyout: true));
                _ = deck.Message(deck.FlyoutHwnd, WM.WM_LBUTTONUP, 0, EditDeckClickTests.At(deck, middle, flyout: true));
                Assert.True(press == 0 ? Lines() > before : Lines() == before, $"{head.Row.Text} over the ghost, clicked {press + 1}: {Lines()} lines, {before} before");
            }
            tiles++;
        }
        // The pick the picture shows under the pointer again.
        if (deck.Layout.Blocks.SelectMany(block => block.Lines).FirstOrDefault(line => line.Row.Kind == PanelRowKind.Pick && line.Row.Item.Id == hovered) is { } back)
        {
            _ = deck.Message(deck.FlyoutHwnd, WM.WM_MOUSEMOVE, 0, EditDeckClickTests.At(deck, deck.Layout.OnPanel(back.Rect).Center, flyout: true));
        }
        return tiles;
    }

    /// <summary>How far a px of the page may differ and be the same: the compositor's rounding at its rounded corners.</summary>
    private const int Rounding = 1;

    /// <summary>The add page of <paramref name="deck"/> as it shows now, saved to <paramref name="file"/>: its pixels (BGRA) and width.</summary>
    private static (byte[] Pixels, int Width) Captured(EditDeck deck, string file)
    {
        WindowShots.Settle(300);
        var page = deck.FlyoutOnScreen;
        var extent = EditDeckClickTests.WindowOf(deck.FlyoutHwnd);
        (byte[] Pixels, int Width) seen = ([], 0);
        var part = new RECT { left = page.left - extent.left, top = page.top - extent.top, right = page.right - extent.left, bottom = page.bottom - extent.top };
        Assert.True(WindowShots.Save(deck.FlyoutHwnd, file, part, (pixels, width) => seen = (pixels, width)), $"{file} came back blank");
        return seen;
    }
}
