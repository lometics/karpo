// quick: what a widget answers when the pointer is handed to it, on each surface it is drawn on —
// the island side of the contract, with no folder in sight. A folder (or anything else that draws
// widgets) hands over: the point in the box's own px, the box the widget was drawn in, the style it
// was drawn with, and which surface that is. The widget answers whether it takes the pointer; if it
// does, the moves and the release are its until it lets go.
//
// CellIsland.PressOn/DragOn/ReleaseOn are the one way in, as PaintOn and TargetOn are: a list's line
// goes to the kind's own CardPress/CardDrag/CardRelease. The volume slider is held on the bar, where
// it takes the pointer as its own.
// Nothing here touches the PC's audio (VolumeWatch.Stand).
//
// Needs Direct2D, so it runs only with KARPO_UI_TESTS=1.

using System.Numerics;
using Karpo.Core;
using Karpo.Dock;
using Karpo.Dock.Native;
using Karpo.Dock.QuickSettings.Services;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
using Color = Windows.UI.Color;
using IDispatcherQueueController = TerraFX.Interop.WinRT.IDispatcherQueueController;

namespace Karpo.Tests.Islands;

public sealed unsafe class CardPointerTests
{
    private static readonly Look Dark = new(
        Glass: false,
        Dark: true,
        Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4),
        AccentLight2: Color.FromArgb(255, 0x99, 0xEB, 0xFF),
        AccentDark1: Color.FromArgb(255, 0x00, 0x5F, 0xB8));

    private static TileStyle Style(int columns = 2, int rows = 1) =>
        new(1f, Dark, FlyoutTheme.For(Dark), columns, rows);

    private static BarItem Widget(BarKind kind)
    {
        var island = kind.New();
        island.Id = kind.ToString().ToLowerInvariant();
        return island;
    }

    /// <summary>
    /// A wheel notch, then a press on the thumb and a release that moved nothing: no tick. The notch
    /// moved the level, and that once made the next drag count as moved, so the press played a
    /// second tick after the wheel's own (Feedback.Notch). A drag that moves still ticks once.
    /// </summary>
    [UiFact]
    public void APressThatMovesNothingAfterTheWheelDoesNotTick()
    {
        OnStaThread(gfx =>
        {
            var output = new SampleVolume { Level = 50 };
            VolumeWatch.Stand = output;
            var ticks = 0;
            Feedback.Forget();
            Feedback.Play = _ => ticks++;
            Feedback.Arm = _ => { };
            try
            {
                var settings = Widget(BarKind.Volume);
                using var island = new VolumeIsland("layout:default/volume", settings, gfx, new Config());
                var bar = new IslandStyle(1f, false, 48f, 24f, Dark);
                var plan = VolumeIsland.Plan(new VolumeOptions(), bar, bar.Thickness);
                var size = new Vector2(plan.Along, plan.Across);
                var cell = new D2D_RECT_F(0, 0, size.X, size.Y);

                // The bar's slider: a notch, then a press on the thumb where the notch left it.
                Assert.True(island.Wheel(120, bar));
                Assert.Equal(52, output.Level);
                Assert.True(island.Press(0, plan.ThumbCentre(cell, output.Level), size, bar));
                Assert.Equal(52, output.Level);
                island.Release();
                Assert.Equal(0, ticks);

                // A drag that moves: one tick.
                Assert.True(island.Press(0, plan.ThumbCentre(cell, output.Level), size, bar));
                island.Drag(0, plan.ThumbCentre(cell, 80), size, bar);
                island.Release();
                Assert.Equal(1, ticks);
            }
            finally
            {
                Feedback.Play = null;
                Feedback.Arm = null;
                VolumeWatch.Stand = null;
            }
        });
    }

    /// <summary>
    /// "the sound that plays when you move the volume slider always has the same volume": a drag let
    /// go plays its one tick after the output has the new level, at that level — not the level the
    /// drag started from — and a second drag elsewhere plays at its own.
    /// </summary>
    [UiFact]
    public void ADragsTickPlaysAtTheLevelItLeft()
    {
        OnStaThread(gfx =>
        {
            var output = new SampleVolume { Level = 50 };
            VolumeWatch.Stand = output;
            var heard = new List<(int Played, int Output)>();
            Feedback.Forget();
            Feedback.Play = level => heard.Add((level, output.Level));
            try
            {
                using var island = new VolumeIsland("display:default/volume", Widget(BarKind.Volume), gfx, new Config());
                var bar = new IslandStyle(1f, false, 48f, 24f, Dark);
                var plan = VolumeIsland.Plan(new VolumeOptions(), bar, bar.Thickness);
                var size = new Vector2(plan.Along, plan.Across);
                var cell = new D2D_RECT_F(0, 0, size.X, size.Y);

                Assert.True(island.Press(0, plan.ThumbCentre(cell, output.Level), size, bar));
                island.Drag(0, plan.ThumbCentre(cell, 80), size, bar);
                Assert.Empty(heard);
                island.Release();
                var high = output.Level;
                Assert.InRange(high, 75, 85);
                Assert.Equal([(high, high)], heard);

                Feedback.Forget();
                Assert.True(island.Press(0, plan.ThumbCentre(cell, output.Level), size, bar));
                island.Drag(0, plan.ThumbCentre(cell, 20), size, bar);
                island.Release();
                var low = output.Level;
                Assert.InRange(low, 15, 25);
                Assert.Equal([(high, high), (low, low)], heard);
            }
            finally
            {
                Feedback.Play = null;
                VolumeWatch.Stand = null;
            }
        });
    }

    /// <summary>
    /// A widget that owns no pointer takes none on a list's line: its line is the click it has
    /// always been.
    /// </summary>
    [UiFact]
    public void AWidgetThatOwnsNoPointerTakesNone()
    {
        OnStaThread(gfx =>
        {
            var settings = Widget(BarKind.NightLight);
            using var island = new ClockTile("layout:default/clock", settings, gfx, new Config());
            var box = new D2D_RECT_F(0, 0, 220, 120);
            Assert.False(island.PressOn(new Vector2(60, 60), box, Style(), WidgetSurface.List));
            // The moves and the release of a press nobody took do nothing at all.
            island.DragOn(new Vector2(70, 60), box, Style(), WidgetSurface.List);
            island.ReleaseOn(WidgetSurface.List);
        });
    }

    /// <summary>
    /// Now playing with nothing playing takes no pointer on a list's line: its controls are clicks,
    /// hit-tested from the very layout they are drawn from (MediaIsland.LineParts). Its targets are
    /// cells of its own, so they differ across the line.
    /// </summary>
    [UiFact]
    public void NowPlayingWithNothingPlayingIsClicksAndTakesNoPointer()
    {
        OnStaThread(gfx =>
        {
            var settings = ListKind.Media.New();
            settings.Id = "media";
            using var island = new MediaIsland("layout:default/media", settings, gfx, new Config());
            var box = new D2D_RECT_F(0, 0, 260, 120);
            var style = Style();
            Assert.False(island.PressOn(new Vector2(130, 90), box, style, WidgetSurface.List));
            // Every point on the line answers with a target of its own; with nothing playing that
            // is the widget itself (0) everywhere, and with something playing the buttons are
            // where they are drawn.
            foreach (var point in new[] { new Vector2(10, 10), new Vector2(130, 60), new Vector2(250, 110) })
            {
                Assert.InRange(island.TargetOn(point, box, style, WidgetSurface.List), 0, 3);
            }
        });
    }

    private static void OnStaThread(Action<Gfx> body)
    {
        Exception? failed = null;
        var thread = new Thread(() =>
        {
            try
            {
                var options = new DispatcherQueueOptions
                {
                    dwSize = (uint)sizeof(DispatcherQueueOptions),
                    threadType = DISPATCHERQUEUE_THREAD_TYPE.DQTYPE_THREAD_CURRENT,
                    apartmentType = DISPATCHERQUEUE_THREAD_APARTMENTTYPE.DQTAT_COM_STA,
                };
                IDispatcherQueueController* controller = null;
                ComUtil.Check(CreateDispatcherQueueController(options, &controller));
                Clock.Now = () => new DateTime(2026, 9, 22, 14, 40, 7, DateTimeKind.Local);
                try
                {
                    using var gfx = new Gfx(gpu: false);
                    body(gfx);
                }
                finally
                {
                    controller->Release();
                }
            }
            catch (Exception error)
            {
                failed = error;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failed is not null)
        {
            throw new Xunit.Sdk.XunitException("On the offscreen thread: " + failed);
        }
    }
}
