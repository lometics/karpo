// Pictures of "Combine taskbar buttons and hide labels: Never" on a wide bar (the person,
// 2026-10-05: "it shows me the three ... even though there is plenty of space"), measured for real
// and drawn from the layout the way the scene draws its buttons: Start, a pinned app with a window
// open — the button that used to go behind "…" — and the open apps, 3, 8 and 20 windows, at 100 %
// on a 2560 px bar and at 150 % on a 3840 px one, dark and light, to KARPO_PNG_DIR
// (never-<windows>-<dark|light>-<100|150>.png). None of them shows "…": three and eight stand at
// the length their titles ask, twenty narrowed alike.
//
// Nothing is shown and nothing is launched: the buttons' pictures are glyphs.

using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Edit;

public sealed unsafe class NeverCombinePictures
{
    private static readonly string PinKey = Config.IslandKey(null, "pin-mail");

    private static readonly string[] Titles =
    [
        "Inbox - Mail",
        "Quarterly report - final version (2) - Word",
        "Notes",
        "Karpo - Visual Studio Code",
        "Calculator",
        "Downloads - File Explorer",
        "Budget 2026.xlsx - Excel",
        "Settings",
        "Weekly sync - Microsoft Teams",
        "Terminal",
        "Photos",
        "README.md - Notepad",
        "Spotify Premium",
        "Calendar - Outlook",
        "Task Manager",
        "Pull request #412 - Microsoft Edge",
        "Paint",
        "Snipping Tool",
        "Letter to the landlord about the heating - Word",
        "Clock",
    ];

    /// <summary>Start, the mail pin and the open apps, at the ranges they start with.</summary>
    private static readonly PartSpec[] Specs =
    [
        new(Config.IslandKey(null, "start"), BarKind.Start, WidgetBounds.DefaultFor(BarKind.Start)),
        new(PinKey, BarKind.Pin, WidgetBounds.DefaultFor(BarKind.Pin)),
        new(Config.IslandKey(null, "apps"), BarKind.Apps, WidgetBounds.DefaultFor(BarKind.Apps)),
    ];

    /// <summary><paramref name="windows"/> windows a window each, the first the mail pin's, as the dock hands them over.</summary>
    private static Content Windows(int windows) => new()
    {
        Tiles = [.. Titles.Take(windows).Select((title, index) => (new Item
        {
            Target = $@"C:\Karpo\apps\app{index}.exe",
            Name = $"App {index}",
            Kind = ItemKind.Shell,
            Keys = [],
            Picture = "\uE737",
        }, new Mark(1, Active: index == 2, Attention: false)))],
        TileParts = [.. Titles.Take(windows).Select((_, index) => index == 0 ? PinKey : null)],
        Labels = [.. Titles.Take(windows)],
    };

    [UiFact]
    public void NeverCombineSheets()
    {
        Offscreen.OnStaThread(gfx =>
        {
            foreach (var (dpi, length, scale) in new[] { (96u, 2560, 100), (144u, 3840, 150) })
            {
                foreach (var windows in new[] { 3, 8, 20 })
                {
                    foreach (var dark in new[] { true, false })
                    {
                        var content = Windows(windows);
                        var look = AddListPictures.LookFor(dark);
                        var config = new Config { Height = 48, Style = BarStyle.Islands, DefaultLayout = { Combine = ButtonCombine.Never } };
                        var area = new RECT { left = 0, top = 0, right = length, bottom = 2160 };
                        var layout = new Layout(gfx, dpi, area, area, Edge.Bottom, config, look, Specs, content, editing: false);
                        var what = $"{windows} windows at {scale} %";

                        Assert.True(layout.Overflowed.Count == 0, $"{what}: \"…\" on a bar with room");
                        Assert.All(Enumerable.Range(0, windows), tile => Assert.True(layout.Labelled(tile), $"{what}: tile {tile} has no label"));
                        Assert.All(Enumerable.Range(0, windows), tile => Assert.True(layout.TileCell(tile).Width >= (2f * layout.Slot) - 0.5f, $"{what}: tile {tile} below two cells"));
                        Paint(gfx, layout, look, content, $"never-{windows}-{(dark ? "dark" : "light")}-{scale}.png");
                    }
                }
            }
        });
    }

    /// <summary>The bar, a card per button with its icon and its title cut to its room, and "…" where it stands.</summary>
    private static void Paint(Gfx gfx, Layout layout, Look look, Content content, string file)
    {
        var theme = FlyoutTheme.For(look);
        var panel = layout.PanelRect;
        RectF Moved(RectF rect) => rect with { X = rect.X - panel.X, Y = rect.Y - panel.Y };
        Offscreen.Bitmap(panel.Width, panel.Height, dc =>
        {
            PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, panel.Width, panel.Height), 0, PopupPaint.Ink(theme.Base));
            var start = Moved(layout.ItemRect(Hit.Start));
            if (!start.IsEmpty)
            {
                PopupPaint.Glyph(dc, gfx, "\uE782", layout.Icon, theme.Text, PopupPaint.Rect(start.X, start.Y, start.Width, start.Height));
            }
            for (var tile = 0; tile < content.Tiles.Count; tile++)
            {
                if (layout.TileCell(tile) is not { IsEmpty: false } cell)
                {
                    continue;
                }
                cell = Moved(cell);
                PopupPaint.Fill(dc, PopupPaint.Rect(cell.X, cell.Y, cell.Width, cell.Height), layout.CardRadius, PopupPaint.Ink(theme.CardBackground));
                var icon = Moved(layout.IconBox(Hit.Tile(tile), layout.Icon));
                PopupPaint.Glyph(dc, gfx, "\uE737", MathF.Round(layout.Icon * 0.8f), theme.Text, PopupPaint.Rect(icon.X, icon.Y, icon.Width, icon.Height));
                if (content.Labels?[tile] is { } title && layout.TileLabel(tile) is { IsEmpty: false } box)
                {
                    box = Moved(box);
                    var size = PopupPaint.Measure(gfx, title, layout.LabelPx, Weight.Regular, box.Width);
                    PopupPaint.Text(dc, gfx, title, layout.LabelPx, Weight.Regular, theme.Text, box.X, box.Y + MathF.Floor((box.Height - size.Y) / 2f), box.Width);
                }
            }
            if (layout.OverflowCell() is { IsEmpty: false } moreCell)
            {
                moreCell = Moved(moreCell);
                PopupPaint.Fill(dc, PopupPaint.Rect(moreCell.X, moreCell.Y, moreCell.Width, moreCell.Height), layout.CardRadius, PopupPaint.Ink(theme.CardBackground));
                var more = Moved(layout.IconBox(Hit.AppOverflow, layout.SmallIcon));
                PopupPaint.Glyph(dc, gfx, "\uE712", layout.SmallIcon, theme.Text, PopupPaint.Rect(more.X, more.Y, more.Width, more.Height));
            }
        }, bitmap => Offscreen.Save(bitmap, Path.Combine(Offscreen.Pictures, file)));
    }
}
