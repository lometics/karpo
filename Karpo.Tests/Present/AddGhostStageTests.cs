// §593: the add page's ghost on the real windows (the stage dock on a desktop of its own, PanelPopupPlaceTests.Drive): the
// widget or group under the pointer on the add page stands where the page adds — the edit bar's Add's own place, a bar's "+",
// a folder column's "+" — nothing written meanwhile; its click puts it in there, one step of the undo, a folder it put on the
// bar opening.
//
// Needs a window station: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using Xunit.Abstractions;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Tests.Present;

public sealed class AddGhostStageTests(ITestOutputHelper output)
{
    /// <summary>
    /// The person (2026-11-09): "when i hover over a group or widget to add it should show the whole group and widget on the
    /// taskbar as a ghost preview. when i click it should place it there and it should also open the folder (if it was a folder
    /// widget that was placed of course)"; "same behaviour should be implemented for the widgets placed inside the folder".
    /// The tile the keys are on stands for the one under the pointer here: a stage desktop has no pointer, and Windows takes the
    /// hover of a posted move back at once (WM_MOUSELEAVE); EditDeckClickTests holds the pointer's own to the same pick.
    /// The edit bar's Add: Keep awake stands on the bar as a ghost, the file as it was; the page closed, the ghost goes, nothing
    /// written; again, clicked, it is in the file, and Ctrl+Z takes it out again. A bar's last "+": the world clock, clicked: in
    /// the file, its folder open. That folder's column "+": a clock a ghost among its lines, the file as it was; clicked, in it.
    /// </summary>
    [UiFact]
    public void TheAddPagesPickStandsWhereItGoesAndItsClickPutsItThere() => PanelPopupPlaceTests.Drive(output, Edge.Bottom, null, probe =>
    {
        probe.Open();
        PanelPopupPlaceTests.Spot? Ghost() => probe.Read("stage-edit.json").FirstOrDefault(spot => spot.Hit == "Ghost");
        var add = probe.Spot(PanelHitKind.Add).Center;

        // The edit bar's Add: its own place, the bar's; the page closed, the ghost gone, nothing written.
        probe.Click(add);
        Assert.Equal(nameof(PanelPage.Add), probe.PageShown());
        var before = probe.ConfigText();
        // §617: from the page's opening, nothing under the pointer: the empty ghost on the bar, no line, nothing written.
        Empty(probe, null);
        Assert.Equal(before, probe.ConfigText());
        _ = Find(probe, "keep awake");
        var shown = Ghost();
        Assert.True(shown is not null, $"no ghost: {string.Join("; ", probe.Read("stage-edit.json").Where(spot => spot.Hit is "Page" or "EditBar"))}");
        Assert.True(shown.Rect.right > shown.Rect.left, $"the ghost {shown} stands nowhere");
        Assert.Equal(before, probe.ConfigText());
        probe.Click(add);
        Assert.Null(probe.PageShown());
        Assert.Null(Ghost());
        Assert.Equal(before, probe.ConfigText());
        // Again, and clicked.
        probe.Click(add);
        var awake = Find(probe, "keep awake");
        var ghost = Ghost();
        Assert.NotNull(ghost);
        probe.Click(awake);
        probe.Pump(900);
        Assert.Null(probe.PageShown());
        Assert.Null(Ghost());
        var added = probe.ConfigText();
        Assert.NotEqual(before, added);
        Assert.Contains("\"" + Karpo.Dock.Islands.IdOf(ghost.Key) + "\"", added, StringComparison.Ordinal);
        probe.Undo();
        probe.Pump(900);
        Assert.DoesNotContain("\"" + Karpo.Dock.Islands.IdOf(ghost.Key) + "\"", probe.ConfigText(), StringComparison.Ordinal);

        // A bar's last "+": a group, §606 its folder open as a ghost too, its lines in it, all of it faint and no folder opened
        // for real; the page closed, all of it gone, nothing written.
        var plus = probe.Read("stage-edit.json").Last(spot => spot.Hit == "BarAdd");
        probe.Click(plus.Center);
        Assert.Equal(nameof(PanelPage.Add), probe.PageShown());
        before = probe.ConfigText();
        Empty(probe, null);
        _ = Find(probe, "world clock");
        var hovered = Ghost();
        Assert.NotNull(hovered);
        var spots = probe.Read("stage-edit.json");
        var ghostFolder = spots.FirstOrDefault(spot => spot.Hit == "GhostFolder");
        Assert.True(ghostFolder is not null && ghostFolder.Key == hovered.Key, $"no ghost of its folder open: {ghostFolder}");
        Assert.True(ghostFolder.Rect.right > ghostFolder.Rect.left && ghostFolder.Rect.bottom > ghostFolder.Rect.top, $"the ghost's folder {ghostFolder} stands nowhere");
        // §625: whole, as it will open.
        Assert.Equal(100, ghostFolder.Index);
        var ghostLines = spots.Where(spot => spot.Hit == "GhostLine").ToList();
        Assert.NotEmpty(ghostLines);
        Assert.All(ghostLines, line => Assert.StartsWith(hovered.Key + "/", line.Key, StringComparison.Ordinal));
        Assert.DoesNotContain(spots, spot => spot.Hit == "Folder");
        Assert.Equal(before, probe.ConfigText());
        probe.Click(plus.Center);
        Assert.Null(probe.PageShown());
        spots = probe.Read("stage-edit.json");
        Assert.DoesNotContain(spots, spot => spot.Hit is "Ghost" or "GhostFolder" or "GhostLine" or "Folder");
        Assert.Equal(before, probe.ConfigText());

        // Again, clicked: put in, its folder the one the ghost showed, open where it stood, whole now.
        probe.Click(plus.Center);
        Assert.Equal(nameof(PanelPage.Add), probe.PageShown());
        var world = Find(probe, "world clock");
        var group = Ghost();
        Assert.NotNull(group);
        var stood = probe.Read("stage-edit.json").First(spot => spot.Hit == "GhostFolder");
        Assert.Equal(before, probe.ConfigText());
        // The ghost's folder under the page: the tile still the page's to press.
        Assert.Equal(Stratum.EditBar, probe.StratumAt(world));
        probe.Click(world);
        probe.Pump(900);
        Assert.Contains("\"" + Karpo.Dock.Islands.IdOf(group.Key) + "\"", probe.ConfigText(), StringComparison.Ordinal);
        spots = probe.Read("stage-edit.json");
        var folder = spots.FirstOrDefault(spot => spot.Hit == "Folder");
        Assert.True(folder is not null && folder.Key == group.Key, $"the folder put in did not open: {folder}");
        Assert.Equal(100, folder.Index);
        Assert.Equal(stood.Rect, folder.Rect);
        Assert.DoesNotContain(spots, spot => spot.Hit is "GhostFolder" or "GhostLine");

        // Its column's "+" (the folder's page, open with it as the selection's, closed off it first): a clock a ghost among its
        // lines, then put in there.
        probe.Close();
        var column = probe.Read("stage-edit.json").First(spot => spot.Hit == "FolderAdd");
        probe.Click(column.Center);
        Assert.Equal(nameof(PanelPage.Add), probe.PageShown());
        before = probe.ConfigText();
        // §617: one empty line among the folder's.
        Empty(probe, group.Key + "/");
        Assert.Equal(before, probe.ConfigText());
        var clock = Find(probe, "clock");
        var line = Ghost();
        Assert.NotNull(line);
        Assert.StartsWith(group.Key + "/", line.Key, StringComparison.Ordinal);
        Assert.Contains(probe.Read("stage-edit.json"), spot => spot.Hit == "Line" && spot.Key == line.Key);
        Assert.Equal(before, probe.ConfigText());
        probe.Click(clock);
        probe.Pump(900);
        Assert.Null(Ghost());
        Assert.Contains("\"" + line.Key[(group.Key.Length + 1)..] + "\"", probe.ConfigText(), StringComparison.Ordinal);
        Assert.Contains(probe.Read("stage-edit.json"), spot => spot.Hit == "Line" && spot.Key == line.Key);
    });

    /// <summary>
    /// The person (2026-11-09): "it also does not show the folder contents as a preview when adding the air quality for example
    /// (then it should also show the contents of the folder with the air qualtiy widgets inside too.)" — every ready-made folder
    /// a bar's "+" offers, the air quality's and the weather's as the world clock's (§606), stands open as a ghost over its face,
    /// its lines the preset's; nothing written.
    /// </summary>
    [UiFact]
    public void APresetsGhostOpensItsFolderWithItsLines() => PanelPopupPlaceTests.Drive(output, Edge.Bottom, null, probe =>
    {
        probe.Open();
        var plus = probe.Read("stage-edit.json").Last(spot => spot.Hit == "BarAdd");
        foreach (var (words, lines) in new[] { ("air quality", 4), ("weather", 3) })
        {
            probe.Click(plus.Center);
            Assert.Equal(nameof(PanelPage.Add), probe.PageShown());
            var before = probe.ConfigText();
            Empty(probe, null);
            // §629: the empty ghost on the bar is under nothing of the page: nothing see-through.
            Assert.DoesNotContain(probe.Read("stage-edit.json"), spot => spot.Hit == "SeeThrough");
            var tile = Find(probe, words);
            // Its folder, however tall, under the page: the tile still the page's, so the pointer stays on it.
            Assert.Equal(Stratum.EditBar, probe.StratumAt(tile));
            var spots = probe.Read("stage-edit.json");
            var ghost = spots.FirstOrDefault(spot => spot.Hit == "Ghost");
            Assert.True(ghost is not null, $"{words}: no ghost");
            var folder = spots.FirstOrDefault(spot => spot.Hit == "GhostFolder");
            Assert.True(folder is not null && folder.Key == ghost.Key, $"{words}: no ghost of its folder open: {string.Join("; ", spots.Where(spot => spot.Hit.StartsWith("Ghost", StringComparison.Ordinal)))}");
            var ghostLines = spots.Where(spot => spot.Hit == "GhostLine").ToList();
            Assert.True(ghostLines.Count == lines, $"{words}: {ghostLines.Count} lines, not {lines}");
            Assert.All(ghostLines, line => Assert.StartsWith(ghost.Key + "/", line.Key, StringComparison.Ordinal));
            Assert.Equal(before, probe.ConfigText());
            // §629: the page see-through where the folder's lines run under it, there alone, each piece over the page's own.
            _ = SeenThrough(probe, words, ghostLines.ConvertAll(line => line.Rect));
            probe.Click(plus.Center);
            Assert.Null(probe.PageShown());
            Assert.DoesNotContain(probe.Read("stage-edit.json"), spot => spot.Hit is "Ghost" or "GhostFolder" or "GhostLine" or "SeeThrough");
        }
    });

    /// <summary>
    /// §629 (the person, 2026-11-09: "the add widget modal should be semi transparent in case the ghost preview is directly behind
    /// it. but it should only be semi transparent for the ghost preview and not for everything actually."): a bar's "+"'s page
    /// whole (nothing typed, as tall as its room), the keys along Popular's first tiles — the ready-made groups among them a ghost
    /// folder each, reaching up under the page —: at each the page see-through where the ghost's lines run under it and there
    /// alone, following the keys from one to the next; the page closed, nothing see-through, nothing written.
    /// </summary>
    [UiFact]
    public void TheAddPageShowsItsGhostThroughItselfAsItGoes() => PanelPopupPlaceTests.Drive(output, Edge.Bottom, null, probe =>
    {
        probe.Open();
        var plus = probe.Read("stage-edit.json").Last(spot => spot.Hit == "BarAdd");
        probe.Click(plus.Center);
        Assert.Equal(nameof(PanelPage.Add), probe.PageShown());
        var before = probe.ConfigText();
        Assert.DoesNotContain(probe.Read("stage-edit.json"), spot => spot.Hit == "SeeThrough");
        probe.Key(probe.Deck(), VK.VK_DOWN);
        probe.Pump(900);
        List<int> seen = [];
        List<string> said = [];
        for (var tile = 0; tile < 8; tile++)
        {
            var spots = probe.Read("stage-edit.json");
            var ghost = spots.FirstOrDefault(spot => spot.Hit == "Ghost");
            Assert.True(ghost is { Index: 0 }, $"tile {tile}: no pick's ghost");
            // The ghost's rectangles: a ghost folder's lines, else its block on the bar.
            List<RECT> rects = [.. spots.Where(spot => spot.Hit == "GhostLine").Select(spot => spot.Rect)];
            seen.Add(SeenThrough(probe, $"tile {tile}", rects.Count > 0 ? rects : [ghost.Rect]));
            var page = spots.First(spot => spot.Hit == "Page").Rect;
            said.Add($"{ghost.Key} {rects.Count} lines (page {page.left},{page.top}..{page.right},{page.bottom}; ghost {ghost.Rect.left},{ghost.Rect.top}..{ghost.Rect.right},{ghost.Rect.bottom}): {seen[^1]}");
            probe.Key(probe.Deck(), VK.VK_TAB);
            probe.Pump(600);
        }
        Assert.True(seen.Exists(count => count > 0), $"no ghost under the page: {string.Join("; ", said)}");
        Assert.True(seen.Exists(count => count == 0), $"every ghost under the page: {string.Join("; ", said)}");
        probe.Click(plus.Center);
        Assert.Null(probe.PageShown());
        Assert.DoesNotContain(probe.Read("stage-edit.json"), spot => spot.Hit is "Ghost" or "GhostFolder" or "SeeThrough");
        Assert.Equal(before, probe.ConfigText());
    });

    /// <summary>
    /// §629: the add page shows its ghost through itself where <paramref name="ghost"/>'s rectangles (screen px) run under it,
    /// there alone: each piece the stage map has inside one of them and the page, none where none runs under it; the
    /// page still the window there, to take the pointer. How many pieces.
    /// </summary>
    private static int SeenThrough(PanelPopupPlaceTests.Probe probe, string words, List<RECT> ghost)
    {
        var spots = probe.Read("stage-edit.json");
        var page = spots.First(spot => spot.Hit == "Page").Rect;
        var pieces = spots.Where(spot => spot.Hit == "SeeThrough").Select(spot => spot.Rect).ToList();
        static RECT Both(RECT a, RECT b) => new() { left = Math.Max(a.left, b.left), top = Math.Max(a.top, b.top), right = Math.Min(a.right, b.right), bottom = Math.Min(a.bottom, b.bottom) };
        List<RECT> under = [.. ghost.Select(rect => Both(rect, page)).Where(rect => rect.right - rect.left > 2 && rect.bottom - rect.top > 2)];
        // A line's card (the ghost's outline) stands inside its room (the stage map's line), a divider's a few px of it.
        Assert.True(pieces.Count <= under.Count && (pieces.Count > 0) == under.Exists(rect => rect.bottom - rect.top >= 24), $"{words}: {pieces.Count} pieces see-through, {under.Count} lines under the page");
        foreach (var piece in pieces)
        {
            Assert.True(piece.left >= page.left && piece.top >= page.top && piece.right <= page.right && piece.bottom <= page.bottom
                && under.Exists(rect => piece.left >= rect.left - 2 && piece.top >= rect.top - 2 && piece.right <= rect.right + 2 && piece.bottom <= rect.bottom + 2),
                $"{words}: a piece {piece.left},{piece.top}..{piece.right},{piece.bottom} is no line under the page {page.left},{page.top}..{page.right},{page.bottom}: {string.Join(" ", under.Select(rect => $"{rect.left},{rect.top}..{rect.right},{rect.bottom}"))} of {string.Join(" ", ghost.Select(rect => $"{rect.left},{rect.top}..{rect.right},{rect.bottom}"))}");
            Assert.Equal(Stratum.EditBar, probe.StratumAt(new POINT { x = (piece.left + piece.right) / 2, y = (piece.top + piece.bottom) / 2 }));
        }
        return pieces.Count;
    }

    /// <summary>
    /// §617: the add page just opened, nothing under the pointer or the keys: the empty ghost where it adds (the stage map's
    /// "Ghost", index 1), standing somewhere — under <paramref name="within"/> (a folder's key and "/") among a folder's lines,
    /// else on a bar.
    /// </summary>
    private static void Empty(PanelPopupPlaceTests.Probe probe, string? within)
    {
        var spots = probe.Read("stage-edit.json");
        var empty = spots.FirstOrDefault(spot => spot.Hit == "Ghost");
        Assert.True(empty is { Index: 1 }, $"no empty ghost as the page opened: {empty}");
        Assert.True(empty.Rect.right > empty.Rect.left && empty.Rect.bottom > empty.Rect.top, $"the empty ghost {empty} stands nowhere");
        if (within is not null)
        {
            Assert.StartsWith(within, empty.Key, StringComparison.Ordinal);
            Assert.Contains(spots, spot => spot.Hit == "Line" && spot.Key == empty.Key);
        }
        else
        {
            Assert.DoesNotContain(spots, spot => spot.Hit == "Line" && spot.Key == empty.Key);
        }
    }

    /// <summary>
    /// The add page's first tile for <paramref name="words"/> typed in its search, the keys on it (Down from the search, §575),
    /// where it stands on the screen.
    /// </summary>
    private static POINT Find(PanelPopupPlaceTests.Probe probe, string words)
    {
        foreach (var c in words)
        {
            _ = PostMessageW(probe.Deck(), WM.WM_CHAR, c, 0);
        }
        probe.Pump(900);
        probe.Key(probe.Deck(), VK.VK_DOWN);
        probe.Pump(900);
        var spots = probe.Read("stage-edit.json");
        var page = spots.First(spot => spot.Hit == "Page").Rect;
        var tiles = spots.Where(spot => spot.Hit == "Deck" && spot.Index == (int)PanelHitKind.Act && spot.Key.StartsWith($"{PanelHitKind.Act}:{PanelPage.Add}", StringComparison.Ordinal)
            && spot.Rect.top >= page.top && spot.Rect.bottom <= page.bottom).OrderBy(spot => spot.Rect.top).ThenBy(spot => spot.Rect.left).ToList();
        Assert.True(tiles.Count > 0, $"nothing found for \"{words}\"");
        return tiles[0].Center;
    }
}
