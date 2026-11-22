// stage: where a bar's entries are, in screen px, for the script that records a stage run (Stage.cs);
// §361: and what edit mode shows besides the bars, for a test to press on.

using System.Globalization;
using System.Text;
using Karpo.Core;
using TerraFX.Interop.Windows;

namespace Karpo.Dock;

public sealed unsafe partial class Dock
{
    /// <summary>
    /// §361: in a stage run, what edit mode shows besides the bars: each display's edit bar's buttons in
    /// sight (its crumbs, Add, Undo, Redo, ⚙, Done, by their parts) and the arranged folder's panel, lines
    /// (by their widgets' keys) and "+" (§397), in screen px; each bar's "+" in sight (§387) and the menu open
    /// (§386); nothing out of edit mode. As each of the dock's frames ends.
    /// </summary>
    private void MapEditForStage()
    {
        if (!Stage.On)
        {
            return;
        }
        static string Entry(string hit, int index, string key, RECT rect) => string.Create(CultureInfo.InvariantCulture,
            $"{{\"hit\":\"{hit}\",\"index\":{index},\"key\":{Stage.Json(key)},\"x\":{rect.left},\"y\":{rect.top},\"w\":{rect.right - rect.left},\"h\":{rect.bottom - rect.top}}}");
        var entries = new List<string>();
        if (editor is not null)
        {
            foreach (var (id, chrome) in chromes)
            {
                // §531: the edit bar itself, and the work area its popups stay in, its index the display's DPI.
                entries.Add(Entry("EditBar", 0, id, chrome.Deck.PanelOnScreen));
                if (EditedDisplays.Find(display => string.Equals(display.Id, id, StringComparison.OrdinalIgnoreCase)) is { } edited)
                {
                    entries.Add(Entry("Work", (int)edited.Dpi, id, BarOf(id)?.WorkArea() ?? Rect(edited.Work)));
                }
                // §520: the bar's entries and buttons, §575 and its flyout's controls, each by what it is (its index the kind, its
                // key the kind and the row's key, an entry's page: "Nav:Appearance"), and the page its flyout shows ("Page", the
                // flyout itself; "Flyout" whatever it shows).
                if (chrome.Deck.Layout is { } shown)
                {
                    // §592: every tab of a strip, the chosen one's the keys' stop alone.
                    List<PanelHit> stops = [.. shown.Header.Select(PanelLayout.HitOf), .. shown.Stops(),
                        .. shown.Blocks.SelectMany(block => block.Lines).Where(line => line.Row.Kind == PanelRowKind.Tabs).SelectMany(line => shown.Controls(line).Select(each => each.Hit))];
                    foreach (var stop in stops.Distinct())
                    {
                        if (chrome.Deck.OnScreen(stop) is var button && button.right > button.left)
                        {
                            entries.Add(Entry("Deck", (int)stop.Kind, $"{stop.Kind}:{(stop.Kind == PanelHitKind.Nav ? stop.Page.ToString() : stop.Key)}{(stop.Index >= 0 ? "#" + stop.Index.ToString(CultureInfo.InvariantCulture) : "")}", button));
                        }
                    }
                    if (chrome.Deck.Page is { } page)
                    {
                        entries.Add(Entry("Page", (int)page, page.ToString(), chrome.Deck.FlyoutOnScreen));
                    }
                    if (chrome.Deck.FlyoutOpen)
                    {
                        entries.Add(Entry("Flyout", 0, id, chrome.Deck.FlyoutOnScreen));
                    }
                    // §526: what it is drawn in, its tokens and its font, for a test to set against Windows' own.
                    entries.Add(Entry("DeckWorn", 0, Worn(chrome.Deck.Theme, chrome.Deck.Font), default));
                }
            }
            if (ArrangedFolder() is { } open)
            {
                // §606: how much of it shows, in hundredths, as its index (100: whole, 0: aside for a ghost's).
                entries.Add(Entry("Folder", (int)MathF.Round(open.Flyout.Shows * 100), open.Key, open.Flyout.PanelOnScreen));
                var keys = open.Flyout.LineKeys;
                for (var line = 0; line < keys.Count; line++)
                {
                    if (open.Flyout.LineRect(keys[line]) is { } rect)
                    {
                        entries.Add(Entry("Line", line, keys[line], rect));
                    }
                }
                // §397: its columns' "+", each by its middle.
                var adds = open.Flyout.AddsOnScreen;
                for (var add = 0; add < adds.Count; add++)
                {
                    entries.Add(Entry("FolderAdd", add, open.Key, new RECT { left = adds[add].x, top = adds[add].y, right = adds[add].x + 1, bottom = adds[add].y + 1 }));
                }
            }
            // §387: each bar's "+" in sight, by its bar's display.
            foreach (var window in windows)
            {
                var adds = window.Scene.AddsShown();
                var (x, y) = (window.Layout.X, window.Layout.Y);
                for (var add = 0; add < adds.Count; add++)
                {
                    var (rect, _) = adds[add];
                    entries.Add(Entry("BarAdd", add, window.Id, new RECT { left = x + (int)rect.X, top = y + (int)rect.Y, right = x + (int)rect.Right, bottom = y + (int)rect.Bottom }));
                }
            }
            // §612: the dress the bars wear (its index the EditDress), whether each bar is laid out in edit mode's dress, each
            // display's dim (dimmed 1 or clear 0, as it ends), the folder open arranged's dress.
            entries.Add(Entry("Dress", (int)laidDress, laidDress.ToString(), default));
            foreach (var window in windows)
            {
                entries.Add(Entry("Dressed", window.Layout.Editing ? 1 : 0, window.Id, default));
            }
            foreach (var (id, chrome) in chromes)
            {
                entries.Add(Entry("Dim", chrome.Shade.Opacity >= 1 ? 1 : 0, id, default));
            }
            if (ArrangedFolder() is { } dressed)
            {
                entries.Add(Entry("FolderDressed", dressed.Flyout.DressWorn ? 1 : 0, dressed.Key, default));
            }
            // §527: what is selected, by key.
            for (var picked = 0; picked < editSelection.Keys.Count; picked++)
            {
                entries.Add(Entry("Selected", picked, editSelection.Keys[picked], default));
            }
            // §593: the add page's ghost, by its key, where it stands: on a bar, or among the open folder's lines; §617 its index 1 for
            // the empty one.
            if (Ghost is { } ghost)
            {
                var at = ArrangedFolder()?.Flyout.LineRect(ghost.Key) ?? default;
                foreach (var window in windows)
                {
                    if (window.Layout.PartIndex(ghost.Key) is var part and >= 0 && window.Layout.Placed(part))
                    {
                        at = window.OnScreen(window.Layout.BlockRect(part));
                    }
                }
                entries.Add(Entry("Ghost", ghost.Empty ? 1 : 0, ghost.Key, at));
            }
            // §606: a ghost's folder standing open, by its key, and its lines; how much of it shows, in hundredths, as its index.
            if (ghostFolder is { } ghosted && folderViews.TryGetValue(ghosted.Key, out var view) && view.IsOpen)
            {
                entries.Add(Entry("GhostFolder", (int)MathF.Round(view.Shows * 100), ghosted.Key, view.PanelOnScreen));
                foreach (var key in view.LineKeys)
                {
                    if (view.LineRect(key) is { } rect)
                    {
                        entries.Add(Entry("GhostLine", 0, key, rect));
                    }
                }
            }
            // §629: where each edit bar's add page shows its ghost through itself, a piece each, by its display.
            foreach (var (id, chrome) in chromes)
            {
                var pieces = chrome.Deck.SeeThroughOnScreen;
                for (var piece = 0; piece < pieces.Count; piece++)
                {
                    entries.Add(Entry("SeeThrough", piece, id, pieces[piece]));
                }
            }
            // §606: a folder standing aside for it.
            if (ghostAside is { } aside)
            {
                entries.Add(Entry("Aside", (int)MathF.Round(aside.Flyout.Shows * 100), aside.Key, default));
            }
            // §394: the errors Karpo knows of, by what each is of, newest first.
            for (var fault = 0; fault < faults.All.Count; fault++)
            {
                entries.Add(Entry("Fault", fault, faults.All[fault].Of, default));
            }
            // §386: the menu open, whose it is and where it stands.
            if (FlyoutOpen && flyout is { } menu)
            {
                entries.Add(Entry("Menu", 0, pending?.Target ?? "", menu.PanelOnScreen));
                entries.Add(Entry("MenuWorn", 0, Worn(menu.Theme, menu.Font), default));
                // §523: a colour's picker in it: its spectrum, its sliders and its hex box, each by its part.
                foreach (var (part, box) in menu.MixOnScreen)
                {
                    entries.Add(Entry("Mix", part is { } kind ? (int)kind : -1, part?.ToString() ?? "Hex", box));
                }
            }
            // §622: the Appearance the bars wear, in words (the config's, a copy worn a moment over the person's: 1).
            entries.Add(Entry("Worn", wornOver is null ? 0 : 1, new LookShown(config, config.DefaultLayout).Values(), default));
        }
        // §623: each folder open, by its key, where its panel stands; at rest too.
        foreach (var open in openFolders.Where(open => open.Flyout.IsOpen))
        {
            entries.Add(Entry("FolderOpen", 0, open.Key, open.Flyout.PanelOnScreen));
            // §664: its lines at rest too, by their widgets' keys, for a test to right-click one.
            foreach (var key in open.Flyout.LineKeys)
            {
                if (open.Flyout.LineRect(key) is { } rect)
                {
                    entries.Add(Entry("FolderLine", 0, key, rect));
                }
            }
        }
        // §664: the menu open's rows, at rest too, by their ids and words, each by its middle, for a test to pick one.
        if (FlyoutOpen && flyout is { } rows)
        {
            foreach (var row in rows.RowsShown.Where(row => !row.Separator && !row.Header && row.Id != 0))
            {
                var (across, along) = (rows.ItemAnchor(row.Id, Edge.Bottom), rows.ItemAnchor(row.Id, Edge.Left));
                entries.Add(Entry("MenuRow", (int)row.Id, row.Text, new RECT { left = across.x, top = along.y, right = across.x + 1, bottom = along.y + 1 }));
            }
        }
        // §623: each display's backdrop layer made, its window's handle, where it was laid, its colour #AARRGGBB and whether it
        // faded: its index 100 where it is coming or standing, 0 where it is going or gone (its window out of sight then).
        foreach (var (id, shade) in backdrops)
        {
            var (fading, to) = shade.Faded;
            var shows = global::TerraFX.Interop.Windows.Windows.IsWindowVisible(shade.Hwnd) != BOOL.FALSE;
            var tinted = string.Create(CultureInfo.InvariantCulture, $"#{shade.Tinted.A:X2}{shade.Tinted.R:X2}{shade.Tinted.G:X2}{shade.Tinted.B:X2}");
            entries.Add(Entry("Backdrop", (int)MathF.Round(to * 100), $"{id}|{tinted}|{(fading ? "faded" : "at once")}|{(shows ? "shown" : "hidden")}|{(nint)shade.Hwnd}", shade.Placed));
        }
        Stage.Edited(entries);
    }

    /// <summary>§526: a theme's tokens a surface is drawn in, and its font, as words: the same words, the same look.</summary>
    private static string Worn(FlyoutTheme theme, TextStyle font)
    {
        static string Hex(Windows.UI.Color color) => string.Create(CultureInfo.InvariantCulture, $"{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}");
        static string Ink(TerraFX.Interop.DirectX.DXGI_RGBA ink) => string.Create(CultureInfo.InvariantCulture, $"{ink.r:0.000},{ink.g:0.000},{ink.b:0.000},{ink.a:0.000}");
        return string.Join(' ',
            theme.Dark, theme.HighContrast, theme.Glass, Hex(theme.Base), Hex(theme.Tint), Hex(theme.Stroke), Ink(theme.Text), Ink(theme.TextSecondary),
            Hex(theme.SubtleHover), Hex(theme.SubtlePressed), Hex(theme.ControlFill), Hex(theme.CardBackground), Hex(theme.Accent), font.Text);
    }

    private sealed partial class DockWindow
    {
        /// <summary>
        /// Every entry along the bar, found by hit-testing its panel end to end across its middle:
        /// what it is, the part it belongs to, and its rectangle on the screen.
        /// </summary>
        private void MapForStage()
        {
            if (!Stage.On)
            {
                return;
            }
            var panel = layout.ItemRect(Hit.Panel);
            var entries = new List<string>();
            if (!panel.IsEmpty)
            {
                var across = panel.Width >= panel.Height;
                var seen = new HashSet<Hit>();
                var length = across ? panel.Width : panel.Height;
                for (var along = 0f; along < length; along += 2f)
                {
                    var x = across ? panel.X + along : panel.X + (panel.Width / 2f);
                    var y = across ? panel.Y + (panel.Height / 2f) : panel.Y + along;
                    if (layout.HitTest(x, y) is not { } hit || hit.Kind is HitKind.Panel or HitKind.ResizeGrip || !seen.Add(hit))
                    {
                        continue;
                    }
                    var rect = layout.ItemRect(hit);
                    if (rect.IsEmpty)
                    {
                        continue;
                    }
                    var part = layout.PartOf(hit);
                    var spec = part >= 0 && part < layout.Specs.Count ? layout.Specs[part] : default;
                    entries.Add(string.Create(CultureInfo.InvariantCulture,
                        $"{{\"hit\":\"{hit.Kind}\",\"index\":{hit.Index},\"part\":{part},\"key\":{Stage.Json(spec.Key ?? "")},\"kind\":\"{spec.Kind}\","
                        + $"\"x\":{layout.X + rect.X:0},\"y\":{layout.Y + rect.Y:0},\"w\":{rect.Width:0},\"h\":{rect.Height:0}}}"));
                }
            }
            // §627: the wallpaper under the bar, where it shows one: its blur's standard deviation in hundredths of a px as its index.
            if (wallpaperSurface is not null && laidLook.Material == Material.Translucent && !panel.IsEmpty)
            {
                entries.Add(string.Create(CultureInfo.InvariantCulture,
                    $"{{\"hit\":\"Wallpaper\",\"index\":{(int)MathF.Round(wallpaperSigma * 100)},\"part\":-1,\"key\":\"\",\"kind\":\"\",\"x\":{layout.X + panel.X:0},\"y\":{layout.Y + panel.Y:0},\"w\":{panel.Width:0},\"h\":{panel.Height:0}}}"));
            }
            var text = new StringBuilder();
            text.AppendJoin(",\n", entries);
            Stage.Mapped((nint)Hwnd, text.ToString());
        }
    }
}
