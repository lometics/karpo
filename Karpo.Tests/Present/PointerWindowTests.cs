// What goes with the pointer moves in the commit of what it belongs to (docs\widgets.md §363; PointerWindow): a
// block carried (EditCarry), the selection rectangle (EditBand), a tray icon dragged (TrayCarry). Each moved and
// sized its window at once, a frame ahead of its own content and of the bar's ghost, the selection's marks, the
// tray's gap — which reach the screen with the commit. Now the window stands still over every display and what it
// shows moves in composition: filmed as the dock's handler runs a pointer move — the carry moved and its bar
// changed in one frame — every frame is the before or the end (tier 1, animations off, the frames between the
// commit and its fence held open and filmed too), and the window is placed once, not at every move.

using System.Numerics;
using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using Color = Windows.UI.Color;
using Film = Karpo.Tests.Present.FrameFilm.Film;
using Plain = Karpo.Tests.Present.FlickerFilmTests.Plain;

namespace Karpo.Tests.Present;

public sealed class PointerWindowTests
{
    /// <summary>What the films look at: the bar along the bottom and the room over it the pointer moves in.</summary>
    private static readonly RECT Area = new() { left = 200, top = 360, right = 1080, bottom = 720 };

    private static readonly RECT Strip = new() { left = 0, top = 648, right = 1280, bottom = 720 };

    private static readonly Color Rest = Color.FromArgb(255, 0x20, 0x40, 0x80);

    private static readonly Color Moved = Color.FromArgb(255, 0x30, 0x90, 0x50);

    private static readonly Color Block = Color.FromArgb(255, 0xE0, 0xA0, 0x20);

    private static readonly Color Accent = Color.FromArgb(255, 0x00, 0x78, 0xD4);

    /// <summary>
    /// The frame over the displays: a px in from every outer side of them all, so no display is wholly covered (Windows
    /// takes a window covering a display for an app in full screen) and nothing it shows is cut where one display ends.
    /// </summary>
    [Fact]
    public void ItsFrameSpansEveryDisplayAndCoversNoneWhole()
    {
        RECT[] displays =
        [
            new RECT { left = -1920, top = 120, right = 0, bottom = 1200 },
            new RECT { left = 0, top = 0, right = 2560, bottom = 1440 },
        ];
        var frame = PointerWindow.FrameOver(displays);
        Assert.Equal(new RECT { left = -1919, top = 1, right = 2559, bottom = 1439 }, frame);
        foreach (var display in displays)
        {
            var covered = frame.left <= display.left && frame.top <= display.top && frame.right >= display.right && frame.bottom >= display.bottom;
            Assert.False(covered, $"the frame covers the display at {display.left},{display.top} whole");
        }
        Assert.Equal(new RECT { left = 1, top = 1, right = 1279, bottom = 719 }, PointerWindow.FrameOver([new RECT { right = 1280, bottom = 720 }]));
    }

    /// <summary>
    /// A block carried (Dock.EditCarry) moved to the pointer while its bar moves its ghost (here: the bar's colour), in
    /// one handler: every frame the before or the end, the carry never a frame ahead of its bar; its window placed once.
    /// As before §363 (a window moved at once), the film sees a frame of neither.
    /// </summary>
    [UiFact]
    public void ACarriedBlockMovesInTheCommitOfItsBar()
    {
        OnHiddenDesktop("pointer-carry", (gfx, instance) =>
        {
            using var bar = Bar(gfx, instance);
            var lifted = Lifted(gfx, bar);
            using var carry = new EditCarry(gfx, instance);
            using (Surfaces.Begin(gfx.Compositor))
            {
                carry.Show(lifted, new RectF(0, 0, 96, 48), new POINT { x = 300, y = 420 });
            }
            FrameFilm.Settle();
            var placed = carry.Window.FramesPlaced;
            var film = FrameFilm.Shoot("pointer-carry-move", gfx, Area, () => [bar.Hwnd, carry.Hwnd], () =>
            {
                carry.Show(lifted, new RectF(0, 0, 96, 48), new POINT { x = 760, y = 520 });
                bar.Paint(Moved);
            });
            film.Holds(everyFrame: true);
            Assert.Equal(placed, carry.Window.FramesPlaced);
            // Several carried together: the row grows in the same commit, the window where it stands.
            var together = FrameFilm.Shoot("pointer-carry-group", gfx, Area, () => [bar.Hwnd, carry.Hwnd], () =>
            {
                carry.Show([(lifted, new RectF(0, 0, 96, 48)), (lifted, new RectF(0, 0, 96, 48))], vertical: false, held: 1, gap: 8f, new POINT { x = 640, y = 440 });
                bar.Paint(Rest);
            });
            together.Holds(everyFrame: true);
            Assert.Equal(placed, carry.Window.FramesPlaced);
            Gone(carry.Hwnd, () => carry.Hide());
        });
    }

    /// <summary>
    /// The way it was: a window moved at once as its content changes in the commit (Surfaces.Place in sight) shows a
    /// frame of neither — what the films above would see of a carry or a band that moved its window.
    /// </summary>
    [UiFact]
    public void AWindowMovedAtOnceIsAFrameOutOfStep()
    {
        OnHiddenDesktop("pointer-old", (gfx, instance) =>
        {
            using var bar = Bar(gfx, instance);
            using var carried = new Plain(gfx, instance, new RECT { left = 300, top = 420, right = 396, bottom = 468 }, Block);
            Surfaces.Enlist(carried.Hwnd, Stratum.Carry);
            FrameFilm.Settle();
            var film = FrameFilm.Shoot("pointer-old-move", gfx, Area, () => [bar.Hwnd, carried.Hwnd], () =>
            {
                _ = Surfaces.Place(carried.Hwnd, 760, 520, 96, 48);
                bar.Paint(Moved);
            });
            Assert.NotEmpty(film.Between(everyFrame: true));
        });
    }

    /// <summary>
    /// The selection rectangle (EditBand, drawn by its display's chrome) grown, shrunk and moved up and left as the
    /// pointer goes, with what it selects (here: the bar's colour) in one handler: every frame the before or the end.
    /// </summary>
    [UiFact]
    public void TheSelectionRectangleChangesInTheCommitOfWhatItSelects()
    {
        OnHiddenDesktop("pointer-band", (gfx, instance) =>
        {
            using var bar = Bar(gfx, instance);
            using var band = new EditBand(gfx, instance);
            using (Surfaces.Begin(gfx.Compositor))
            {
                band.Show(new RECT { left = 500, top = 420, right = 700, bottom = 560 }, Accent, 1f);
            }
            FrameFilm.Settle();
            var placed = band.Window.FramesPlaced;
            var grown = FrameFilm.Shoot("pointer-band-grow", gfx, Area, () => [bar.Hwnd, band.Hwnd], () =>
            {
                band.Show(new RECT { left = 500, top = 420, right = 900, bottom = 700 }, Accent, 1f);
                bar.Paint(Moved);
            });
            var back = FrameFilm.Shoot("pointer-band-back", gfx, Area, () => [bar.Hwnd, band.Hwnd], () =>
            {
                // The pointer gone up and left of where the drag began: the rectangle moves its corner.
                band.Show(new RECT { left = 300, top = 380, right = 500, bottom = 420 }, Accent, 1f);
                bar.Paint(Rest);
            });
            grown.Holds(everyFrame: true);
            back.Holds(everyFrame: true);
            Assert.Equal(placed, band.Window.FramesPlaced);
            Gone(band.Hwnd, band.Hide);
        });
    }

    /// <summary>A tray icon dragged (TrayCarry) moved with the tray's gap (here: the bar's colour) in one handler: every frame the before or the end.</summary>
    [UiFact]
    public void ATrayIconCarriedMovesInTheCommitOfItsGap()
    {
        OnHiddenDesktop("pointer-tray", (gfx, instance) =>
        {
            using var bar = Bar(gfx, instance);
            using var carry = new TrayCarry(gfx, instance);
            var icon = Solid(96);
            using (Surfaces.Begin(gfx.Compositor))
            {
                carry.Show(icon, 96, new POINT { x = 400, y = 600 });
            }
            FrameFilm.Settle();
            var film = FrameFilm.Shoot("pointer-tray-move", gfx, Area, () => [bar.Hwnd, carry.Hwnd], () =>
            {
                carry.Show(icon, 96, new POINT { x = 900, y = 500 });
                bar.Paint(Moved);
            });
            film.Holds(everyFrame: true);
            Gone(carry.Hwnd, carry.Hide);
        });
    }

    // --- Helpers ---------------------------------------------------------------------------------

    /// <summary>A bar's stand-in along the bottom, in the bars' stratum: its colour is what the pointer's move changes on it.</summary>
    private static Plain Bar(Gfx gfx, HINSTANCE instance)
    {
        var bar = new Plain(gfx, instance, Strip, Rest);
        Surfaces.Enlist(bar.Hwnd, Stratum.Bar);
        FrameFilm.Settle();
        return bar;
    }

    /// <summary>A block lifted off the bar: drawn where nothing of the bar's window shows it, the carry drawing it from there.</summary>
    private static Windows.UI.Composition.SpriteVisual Lifted(Gfx gfx, Plain bar)
    {
        var c = gfx.Compositor;
        var lifted = c.CreateSpriteVisual();
        lifted.Brush = c.CreateColorBrush(Block);
        lifted.Size = new Vector2(96, 48);
        lifted.Offset = new Vector3(-400, 0, 0);
        bar.Root.Children.InsertAtTop(lifted);
        return lifted;
    }

    /// <summary>An icon of <paramref name="size"/> px, opaque orange.</summary>
    private static Pixels Solid(int size)
    {
        var data = new byte[size * size * 4];
        for (var at = 0; at < data.Length; at += 4)
        {
            (data[at], data[at + 1], data[at + 2], data[at + 3]) = (0x20, 0xA0, 0xE0, 0xFF);
        }
        return new Pixels { Data = data, Width = size, Height = size };
    }

    /// <summary>Out of sight, in a frame, as the dock's handler hides it.</summary>
    private static void Gone(HWND window, Action hide)
    {
        using (Surfaces.Begin(null))
        {
            hide();
        }
        FrameFilm.Settle();
        Assert.False(TerraFX.Interop.Windows.Windows.IsWindowVisible(window));
    }

    /// <summary>On a hidden desktop of its own, the time between a commit and its fence held open, as edit mode's films hold it.</summary>
    private static void OnHiddenDesktop(string what, Action<Gfx, HINSTANCE> body) =>
        FlickerFilmTests.OnHiddenDesktop(what, (gfx, instance) =>
        {
            Surfaces.FenceHoldMs = 120;
            try
            {
                body(gfx, instance);
            }
            finally
            {
                Surfaces.FenceHoldMs = 0;
            }
        });
}
