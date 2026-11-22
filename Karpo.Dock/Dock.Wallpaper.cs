// §547: the wallpaper Karpo blurs under every see-through bar (WallpaperStrips, Karpo.Core\Wallpapers.cs; docs\widgets.md
// §547): read from Windows when the dock starts, when Windows says it changed (WM_SETTINGCHANGE SPI_SETDESKWALLPAPER) and
// when the displays do; each bar's strip composed off this thread when what it is made of changes (WallpaperKey: the
// picture, the display, the scale, the bar's rectangle), posted back (WM_KARPO_WALLPAPER) and blurred on the GPU into the
// surface the bar shows under its colour (Scene.Wallpaper); blurred again alone when the Taskbar's Background blur
// changes. A bar keeps what it showed until its new strip comes. Nothing at rest.
//
// A bar shows the wallpaper of the display it stands on, under its own rectangle, whatever its kind: docked, floating
// (each dock the part under it), a place or a bar shown over the windows — what is behind those is often a window, but the
// bar is the person's look of the desktop, the same wherever it stands.

using System.Collections.Concurrent;
using Karpo.Core;
using TerraFX.Interop.Windows;
using Windows.UI.Composition;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

public sealed unsafe partial class Dock
{
    /// <summary>§547: a bar's wallpaper strip was composed off the dock's thread (<see cref="composedStrips"/>).</summary>
    internal const uint WM_KARPO_WALLPAPER = WM.WM_APP + 0x64;

    /// <summary>§547: every display's desktop as last read (<see cref="WallpaperStrips.Read"/>); null to be read again.</summary>
    private List<WallpaperStrips.Desktop>? wallpaperDesktops;

    /// <summary>§547: strips composed off the dock's thread, each with what it was made of, waiting for it.</summary>
    private readonly ConcurrentQueue<(WallpaperKey Key, Pixels? Strip)> composedStrips = new();

    /// <summary>
    /// §547: every display's desktop, read when it is first asked for after a change; on the stage its test's own picture on
    /// every display, or none (§627, <see cref="Stage.Wallpaper"/>).
    /// </summary>
    private IReadOnlyList<WallpaperStrips.Desktop> WallpaperDesktops => wallpaperDesktops ??= !Stage.On ? WallpaperStrips.Read()
        : Stage.Wallpaper is { } picture ? [.. Stage.Monitors().Select(monitor => new WallpaperStrips.Desktop(monitor.Bounds, WallpaperStrips.Source(picture, WallpaperFit.Fill, new Argb(255, 0, 0, 0))))]
        : [];

    /// <summary>§547: what the desktop of the display at <paramref name="monitor"/> shows; null for none known.</summary>
    private WallpaperSource? WallpaperOn(RectI monitor) => WallpaperStrips.Of(WallpaperDesktops, monitor);

    /// <summary>§547: the rectangle round every display, which a spanned picture covers.</summary>
    private RectI DesktopRound =>
        Wallpapers.Round(WallpaperDesktops.Count > 0 ? WallpaperDesktops.Select(desktop => desktop.Monitor) : windows.Select(window => window.Monitor.Bounds));

    /// <summary>§547: Windows said the wallpaper or the displays changed: read again, and every bar's strip made again where it changed.</summary>
    private void WallpaperChanged()
    {
        wallpaperDesktops = null;
        foreach (var window in windows)
        {
            window.Wallpaper();
        }
        // §651: and an attached folder's ground's.
        foreach (var open in openFolders)
        {
            open.Flyout.Repaper();
        }
    }

    /// <summary>§651: <paramref name="window"/>'s attached folders' grounds, their paper come.</summary>
    private void Repaper(DockWindow window)
    {
        foreach (var open in openFolders.Where(open => ReferenceEquals(open.Window, window)))
        {
            open.Flyout.Repaper();
        }
    }

    /// <summary>§547: <paramref name="key"/>'s strip composed off the dock's thread, handed back with <see cref="WM_KARPO_WALLPAPER"/>.</summary>
    private void ComposeWallpaper(WallpaperKey key)
    {
        var to = host;
        _ = Task.Run(() =>
        {
            Pixels? strip = null;
            try
            {
                strip = WallpaperStrips.Compose(key);
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                Troubles.Note("Wallpaper", error);
            }
            composedStrips.Enqueue((key, strip));
            _ = PostMessageW(to, WM_KARPO_WALLPAPER, 0, 0);
        });
    }

    /// <summary>§547: <see cref="WM_KARPO_WALLPAPER"/>: the strips composed, each shown by the bar waiting for it.</summary>
    private void WallpapersComposed()
    {
        while (composedStrips.TryDequeue(out var done))
        {
            foreach (var window in windows)
            {
                window.Composed(done.Key, done.Strip);
            }
        }
    }

    private sealed partial class DockWindow
    {
        /// <summary>§547: what the strip this bar has was made of, and the strip (its margin round it); none yet.</summary>
        private WallpaperKey? wallpaperKey;

        private Pixels? wallpaperStrip;

        /// <summary>§547: the strip being composed for this bar; null for none.</summary>
        private WallpaperKey? wallpaperWanted;

        /// <summary>§547: the strip blurred, the bar's size, as the scene shows it, and at what standard deviation.</summary>
        private CompositionDrawingSurface? wallpaperSurface;

        private float wallpaperSigma = -1f;

        /// <summary>§704: how light the strip under the bar is (LiquidGlass.Behind), which a Liquid glass bar's tint follows; null for none.</summary>
        private double? wallpaperLight;

        /// <summary>tests: what the strip this bar shows was made of; null for none.</summary>
        internal WallpaperKey? ShownWallpaper => wallpaperKey;

        /// <summary>
        /// §547: the wallpaper under this bar as it is laid out and dressed now: where it is see-through (Translucent), the
        /// strip of its display's desktop under its panel's rectangle, blurred as its layout's Taskbar says — composed again
        /// only where what it is made of changed, blurred again only where the amount did. Called after every lay out and
        /// recolour, and when Windows says the wallpaper changed.
        /// </summary>
        public void Wallpaper()
        {
            if (scene is null || !laidOut || frame is not { } at || laidLook.Material != Material.Translucent)
            {
                return;
            }
            var bounds = monitor.Bounds;
            if (dock.WallpaperOn(bounds) is not { } source)
            {
                return;
            }
            var panel = layout.PanelRect;
            var (left, top) = (at.left + (int)MathF.Round(panel.X), at.top + (int)MathF.Round(panel.Y));
            var bar = new RectI(left, top, left + (int)MathF.Round(panel.Width), top + (int)MathF.Round(panel.Height));
            if (bar.Width <= 0 || bar.Height <= 0)
            {
                return;
            }
            var key = new WallpaperKey(source, bounds, dock.DesktopRound, bar, Wallpapers.Margin(layout.Scale));
            if (key == wallpaperKey && wallpaperStrip is { } strip)
            {
                var sigma = KarpoLook.Sigma(laidLook.Dressing.Ground.Blur ?? KarpoLook.Blur, layout.Scale);
                if (wallpaperSurface is null || sigma != wallpaperSigma)
                {
                    var made = WallpaperStrips.Blurred(dock.gfx, strip, key.Margin, sigma, wallpaperSurface);
                    if (!ReferenceEquals(made, wallpaperSurface))
                    {
                        wallpaperSurface?.Dispose();
                    }
                    (wallpaperSurface, wallpaperSigma) = (made, sigma);
                }
                scene.Wallpaper(wallpaperSurface);
                // §704: and how light it is, which Liquid glass's tint follows.
                scene.Backdrop(wallpaperLight);
                // §627: the stage says how blurred it shows it (stage-map.json's "Wallpaper").
                MapForStage();
                return;
            }
            // What it showed stays until the new strip comes.
            if (key != wallpaperWanted)
            {
                wallpaperWanted = key;
                dock.ComposeWallpaper(key);
            }
        }

        /// <summary>
        /// §651: the wallpaper under an attached folder's ground as this bar shows it under its own panel: what its strip was
        /// made of, the strip, the one being composed, the strip blurred and the brush the ground's paper is.
        /// </summary>
        private WallpaperKey? paperKey;

        private Pixels? paperStrip;
        private WallpaperKey? paperWanted;
        private CompositionDrawingSurface? paperSurface;
        private float paperSigma = -1f;
        private CompositionSurfaceBrush? paperBrush;

        /// <summary>
        /// §651 (the person: "it looks like it is part of it"): what an attached folder's ground of this bar stands on, for its
        /// rectangle on the screen (<paramref name="ground"/>): the wallpaper of the bar's display under it, blurred as the bar's
        /// own (§547) — the strip composed off the dock's thread the first time, as a bar's, and kept; blurred again only where
        /// the amount changed. Until it comes, what it showed before; none before anything: the ground solid
        /// (FlyoutHost.Repaper).
        /// </summary>
        public CompositionBrush? Paper(RECT ground)
        {
            if (scene is null || !laidOut || ground.right <= ground.left || ground.bottom <= ground.top || dock.WallpaperOn(monitor.Bounds) is not { } source)
            {
                return null;
            }
            var bounds = monitor.Bounds;
            var key = new WallpaperKey(source, bounds, dock.DesktopRound, new RectI(ground.left, ground.top, ground.right, ground.bottom), Wallpapers.Margin(layout.Scale));
            if (key == paperKey)
            {
                if (paperStrip is not { } strip)
                {
                    return null;
                }
                var sigma = KarpoLook.Sigma(laidLook.Dressing.Ground.Blur ?? KarpoLook.Blur, layout.Scale);
                if (paperSurface is null || sigma != paperSigma)
                {
                    var made = WallpaperStrips.Blurred(dock.gfx, strip, key.Margin, sigma, paperSurface);
                    if (!ReferenceEquals(made, paperSurface))
                    {
                        paperSurface?.Dispose();
                    }
                    (paperSurface, paperSigma) = (made, sigma);
                }
                paperBrush ??= dock.gfx.Compositor.CreateSurfaceBrush();
                paperBrush.Stretch = CompositionStretch.Fill;
                paperBrush.Surface = paperSurface;
                return paperBrush;
            }
            if (key != paperWanted)
            {
                paperWanted = key;
                dock.ComposeWallpaper(key);
            }
            return paperSurface is not null ? paperBrush : null;
        }

        /// <summary>§547: a strip was composed (<paramref name="strip"/>; null where it could not be): this bar's, where it waits for it.</summary>
        public void Composed(WallpaperKey key, Pixels? strip)
        {
            // §651: an attached folder's ground's, its paper.
            if (key == paperWanted)
            {
                (paperWanted, paperKey, paperStrip, paperSigma) = (null, key, strip, -1f);
                dock.Repaper(this);
            }
            if (key != wallpaperWanted)
            {
                return;
            }
            (wallpaperWanted, wallpaperKey, wallpaperStrip, wallpaperSigma) = (null, key, strip, -1f);
            // §704: the light of what is under the bar, read once a strip.
            wallpaperLight = strip is null ? null : LiquidGlass.Behind(strip.Data, strip.Width, strip.Height, key.Margin);
            if (strip is null)
            {
                scene?.Wallpaper(null);
                wallpaperSurface?.Dispose();
                wallpaperSurface = null;
                return;
            }
            Wallpaper();
        }

        /// <summary>§547: the strip's surface closed with the bar.</summary>
        private void ForgetWallpaper()
        {
            wallpaperSurface?.Dispose();
            (wallpaperSurface, wallpaperStrip, wallpaperKey, wallpaperWanted) = (null, null, null, null);
            paperSurface?.Dispose();
            paperBrush?.Dispose();
            (paperSurface, paperBrush, paperStrip, paperKey, paperWanted) = (null, null, null, null, null);
        }
    }
}
