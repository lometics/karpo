// quick: a widget that takes the pointer itself inside a folder — the sound panel's volume row, a
// line of a list folder (ListKind.SoundVolume) — driven for real on a hidden desktop. The bug these
// hold down: the folder asked a widget for a click and nothing else, so a slider in a folder could
// be clicked but never dragged, while the same slider on the bar followed the pointer
// (Dock.Quick.cs). The folder now offers every press to the widget first (CellIsland.PressOn).
//
// Nothing here touches the PC's audio: a made-up output stands behind the line (SoundParts.Made),
// and Feedback.Play is silent instead of beeping.
//
// Like the other offscreen tests these need a window station and Direct2D, so they run only with
// KARPO_UI_TESTS=1.

using System.Numerics;
using Karpo.Core;
using Karpo.Dock;
using Karpo.Dock.Native;
using Karpo.Dock.QuickSettings.Controls;
using Karpo.Dock.QuickSettings.Services;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.DirectX.DirectX;
using static TerraFX.Interop.Windows.Windows;
using Color = Windows.UI.Color;
using IDispatcherQueueController = TerraFX.Interop.WinRT.IDispatcherQueueController;

namespace Karpo.Tests.Folders;

public sealed unsafe class FolderSliderTests
{
    private const string Owner = "layout:default";
    private const float Scale = 1f;
    private static readonly RECT Work = new() { left = 0, top = 0, right = 1920, bottom = 1040 };
    private static readonly POINT Anchor = new() { x = 1700, y = 1040 };

    private static readonly Look Dark = new(
        Glass: false,
        Dark: true,
        Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4),
        AccentLight2: Color.FromArgb(255, 0x99, 0xEB, 0xFF),
        AccentDark1: Color.FromArgb(255, 0x00, 0x5F, 0xB8));

    /// <summary>A list folder of the volume row, two rows tall, over a battery's line, whose click does nothing.</summary>
    private static BarItem Folder()
    {
        var folder = new FolderOptions();
        var volume = ListKind.SoundVolume.New();
        volume.Id = "volume";
        folder.Put(volume);
        var battery = ListKind.Battery.New();
        battery.Id = "battery";
        folder.Put(battery);
        var island = BarKind.FolderList.New();
        island.Id = "sound";
        island.Folder = folder;
        return island;
    }

    private static BarItem Widget(string id, BarKind kind)
    {
        var island = kind.New();
        island.Id = id;
        return island;
    }

    private static FolderCell Cell(BarItem island) => new() { Island = island.AsCell() };

    /// <summary>
    /// The folder of <see cref="Folder"/> open over <paramref name="machine"/>, at rest. The line is
    /// made over the machine while the folder makes its widgets, and only then.
    /// </summary>
    private static FolderIsland Open(Gfx gfx, Machine machine)
    {
        var island = Folder();
        SoundParts.Made = () => machine;
        try
        {
            return new FolderIsland($"{Owner}/{island.Id}", island, gfx, new Config());
        }
        finally
        {
            SoundParts.Made = null;
        }
    }

    /// <summary>
    /// A press, the moves and the release on the slider of the volume row in a list: the level
    /// follows the pointer all the way, not only where it was let go, and the line is not a click
    /// besides.
    /// <para>
    /// The levels are read as an <b>order</b> rather than as numbers in a band: what the test is
    /// for is that the thumb follows the pointer, and that survives every height a line can have.
    /// </para>
    /// </summary>
    [UiFact]
    public void ASliderInAListLineFollowsTheDrag()
    {
        OnHiddenDesktop((gfx, instance) =>
        {
            var output = new Machine { Level = 0.5f };
            Feedback.Forget();
            Feedback.Play = _ => { };
            try
            {
                var folder = Open(gfx, output);
                using var flyout = new FolderFlyout(gfx, instance, 0);
                flyout.Show(gfx, Dark, Scale, Anchor, Work, Edge.Bottom, folder, FlyoutAlign.End);
                Assert.True(flyout.IsOpen);
                var track = Track(flyout);

                // A press near the quiet end of the track, then a drag to the loud end and back.
                Assert.Equal(default, Press(flyout, OnTrack(track, 0.1f)));
                var quiet = output.Level;
                Assert.True(quiet < 0.5f, $"a press near the quiet end lowered the level, not {quiet}");
                Assert.Equal(default, Move(flyout, OnTrack(track, 0.9f)));
                var loud = output.Level;
                Assert.True(loud > quiet, $"the drag along raised it, not {loud}");
                Assert.Equal(default, Move(flyout, OnTrack(track, 0.5f)));
                var middle = output.Level;
                Assert.True(middle < loud && middle > quiet, $"the drag back landed between them, not {middle}");
                // The release ends the drag where it was, and the click it would have been is the
                // drag's: the folder stays open and forwards nothing.
                Assert.Equal(default, Up(flyout, OnTrack(track, 0.5f)));
                Assert.True(flyout.IsOpen);
                Assert.Equal(middle, output.Level);
                Assert.False(output.Muted, "a drag on the slider must never mute");

                // A line that is not a slider is a click, and in a folder a click opens nothing
                // (docs\widgets.md §15): the battery's own flyout stays shut and the folder stays
                // open. The slider above is not a click on the widget — it is the widget's own
                // control — which is why it is untouched by the rule.
                var battery = Middle(Card(flyout, 1));
                Assert.Equal(default, Press(flyout, battery));
                Assert.Equal(FolderFlyoutAction.None, Up(flyout, battery).Kind);
                Assert.True(flyout.IsOpen, "a refused click closed the folder");
                folder.Dispose();
            }
            finally
            {
                Feedback.Play = null;
            }
        });
    }

    /// <summary>
    /// A drag on the slider is the slider's, however far it goes: it never turns edit mode on, as a
    /// line dragged elsewhere in a folder at rest does (FolderFlyout.ArrangeFromDrag, FolderDragEditTests),
    /// and as the bar's slider never starts a move (Dock.Pins.cs, TakesDragItself). The level follows
    /// the pointer, the dock is never asked, and the folder stays at rest and open.
    /// </summary>
    [UiFact]
    public void ADragOnTheSliderDoesNotTurnEditModeOn()
    {
        OnHiddenDesktop((gfx, instance) =>
        {
            var output = new Machine { Level = 0.5f };
            Feedback.Forget();
            Feedback.Play = _ => { };
            try
            {
                var folder = Open(gfx, output);
                using var flyout = new FolderFlyout(gfx, instance, 0);
                var asked = 0;
                flyout.ArrangeFromDrag = () => { asked++; return true; };
                flyout.Show(gfx, Dark, Scale, Anchor, Work, Edge.Bottom, folder, FlyoutAlign.End);
                var track = Track(flyout);

                // From near the quiet end to near the loud end: far past the drag threshold.
                Assert.Equal(default, Press(flyout, OnTrack(track, 0.1f)));
                var quiet = output.Level;
                Assert.Equal(default, Move(flyout, OnTrack(track, 0.9f)));
                Assert.True(output.Level > quiet, $"the drag along the slider did not raise the level, {output.Level}");
                Assert.Equal(0, asked);
                Assert.False(flyout.Arranged, "a drag on the slider turned edit mode on");
                Assert.Equal(default, Up(flyout, OnTrack(track, 0.9f)));
                Assert.Equal(0, asked);
                Assert.True(flyout.IsOpen);
                Assert.False(flyout.Arranged);
                folder.Dispose();
            }
            finally
            {
                Feedback.Play = null;
            }
        });
    }

    /// <summary>
    /// §663 ("dragging contents inside of widgets around should still work normally"): on a locked bar the slider is
    /// still the slider's — the level follows the pointer there and back — while the line it stands in is carried nowhere.
    /// </summary>
    [UiFact]
    public void LockedTheSliderStillFollowsTheDrag()
    {
        OnHiddenDesktop((gfx, instance) =>
        {
            var output = new Machine { Level = 0.5f };
            Feedback.Forget();
            Feedback.Play = _ => { };
            try
            {
                var folder = Open(gfx, output);
                using var flyout = new FolderFlyout(gfx, instance, 0);
                var asked = 0;
                flyout.ArrangeFromDrag = () => { asked++; return true; };
                flyout.Locked = () => true;
                flyout.Show(gfx, Dark, Scale, Anchor, Work, Edge.Bottom, folder, FlyoutAlign.End);
                var track = Track(flyout);

                Assert.Equal(default, Press(flyout, OnTrack(track, 0.1f)));
                var quiet = output.Level;
                Assert.Equal(default, Move(flyout, OnTrack(track, 0.9f)));
                var loud = output.Level;
                Assert.True(loud > quiet, $"locked, the drag along the slider did not raise the level, {loud}");
                Assert.Equal(default, Move(flyout, OnTrack(track, 0.5f)));
                Assert.True(output.Level < loud && output.Level > quiet, $"locked, the drag back did not follow, {output.Level}");
                Assert.Equal(default, Up(flyout, OnTrack(track, 0.5f)));
                Assert.Equal(0, asked);
                Assert.True(flyout.IsOpen);
                Assert.False(flyout.Arranged);
                folder.Dispose();
            }
            finally
            {
                Feedback.Play = null;
            }
        });
    }

    /// <summary>
    /// The volume slider of the taskbar is no line of a list — a list would draw it as its glyph and
    /// its name, a slider nobody can drag — and a list refuses it. A list's own volume line is the
    /// sound panel's volume row (SoundVolume), a card whose slider is its own (2026-09-26), which the
    /// list's library calls "Volume".
    /// </summary>
    [Fact]
    public void TheVolumeLineOfAListIsTheSoundPanelsVolumeRow()
    {
        Assert.Null(ListWidgets.Find(BarKind.Volume.Id()));
        var list = new FolderOptions();
        Assert.Null(Karpo.Core.Folders.Add(BarKind.FolderList, list, Widget("volume", BarKind.Volume).AsCell()));
        Assert.Empty(list.Cells);

        var row = ListKind.SoundVolume.New();
        Assert.Equal("Volume", row.Info!.Title);
        Assert.NotNull(Karpo.Core.Folders.Add(BarKind.FolderList, list, row));
    }

    /// <summary>
    /// A list folder draws a widget that is there for its click as a name and a glyph
    /// (FolderFlyout.PaintListLine), so no widget owns the pointer there: a press takes nothing
    /// and the line is the widget's own click — for a switch, the switch.
    /// </summary>
    [UiFact]
    public void AListLineIsTheWidgetsOwnClick()
    {
        OnHiddenDesktop((gfx, instance) =>
        {
            var switched = new RecordedSwitch();
            QuickSwitches.Source = _ => switched;
            try
            {
                var list = new FolderOptions();
                list.Lists = [[Cell(Widget("airplane", BarKind.AirplaneMode)), Cell(Widget("night", BarKind.NightLight))]];
                var island = Widget("switches", BarKind.FolderList);
                island.Folder = list;
                var folder = new FolderIsland($"{Owner}/{island.Id}", island, gfx, new Config());
                using var flyout = new FolderFlyout(gfx, instance, 0);
                flyout.Show(gfx, Dark, Scale, Anchor, Work, Edge.Bottom, folder, FlyoutAlign.End);
                var line = Card(flyout, 0);
                var at = Along(line, 0.75f);
                Assert.Equal(default, Press(flyout, at));
                // Nothing took the pointer: a small move is the folder's own (a longer one would
                // start carrying the widget out) and does nothing yet.
                Assert.Equal(default, Move(flyout, at with { X = at.X - 3 }));
                Assert.Equal(0, switched.Acts);
                Assert.Equal(default, Up(flyout, at));
                Assert.Equal(1, switched.Acts);
                Assert.True(flyout.IsOpen);
            }
            finally
            {
                QuickSwitches.Source = null;
            }
        });
    }

    /// <summary>A switch that counts what it was asked to do, and does none of it.</summary>
    private sealed class RecordedSwitch : IQuickSwitch
    {
        public int Acts { get; private set; }

        public string Name => "Recorded";

        public bool Available => true;

        public bool? On => null;

        public string? Note => null;

        public void Start(Action changed)
        {
        }

        public void Act() => Acts++;

        public void Dispose()
        {
        }
    }

    /// <summary>
    /// A made-up output with a level and a mute, which records what it is asked to set and says it
    /// changed, as Windows does, so the line reads the level back.
    /// </summary>
    private sealed class Machine : ISoundSource
    {
        private Changes? news;

        public float Level { get; set; } = 0.5f;

        public bool Muted { get; private set; }

        public void Start(Changes changes) => news = changes;

        public SoundState Read(int changes) =>
            new(true, Level, Muted, [new AudioOutput("speakers", "Speakers (Realtek(R) Audio)", WindowsSound.GlyphFor(1), true)], []);

        public void SetVolume(float value)
        {
            Level = value;
            news?.Add(SoundNews.Volume);
        }

        public void SetMuted(bool muted)
        {
            Muted = muted;
            news?.Add(SoundNews.Volume);
        }

        public void SetDefault(string id)
        {
        }

        public void SetDefaultInput(string id)
        {
        }

        public void SetInputMuted(bool muted)
        {
        }

        public void SetAppVolume(string key, float value)
        {
        }

        public void SetAppMuted(string key, bool muted)
        {
        }

        public void Dispose()
        {
        }
    }

    /// <summary>
    /// The line drawn at three moments of one real drag — where the press took the thumb, halfway
    /// across, and where it was let go — so the thumb, the fill and the number can be seen to
    /// follow the pointer. The drag is the flyout's own; nothing here sets a level by hand.
    /// </summary>
    [UiFact]
    public void ASliderMidDragIsDrawn()
    {
        OnHiddenDesktop((gfx, instance) =>
        {
            Feedback.Forget();
            Feedback.Play = _ => { };
            try
            {
                var folder = Open(gfx, new Machine { Level = 0.5f });
                using var flyout = new FolderFlyout(gfx, instance, 0);
                foreach (var dark in new[] { true, false })
                {
                    var look = dark ? Dark : Dark with { Dark = false };
                    flyout.Show(gfx, look, Scale, Anchor, Work, Edge.Bottom, folder, FlyoutAlign.End);
                    var theme = FlyoutTheme.For(look);
                    var card = Card(flyout, 0);
                    var (width, height) = (card.right - card.left, card.bottom - card.top);
                    var style = FolderFlyout.StyleFor(Scale, look, theme);
                    var widget = folder.Tiles[0].Island;
                    var track = Track(flyout);
                    const float gap = 12;
                    Assert.Equal(default, Press(flyout, OnTrack(track, 0.2f)));
                    Picture(3 * width + 2 * gap, height, Scale, theme, $"folder-slider-drag-{(dark ? "dark" : "light")}.png", dc =>
                    {
                        var at = 0f;
                        foreach (var fraction in new[] { 0.2f, 0.55f, 0.9f })
                        {
                            Assert.Equal(default, Move(flyout, OnTrack(track, fraction)));
                            var left = at;
                            var box = PopupPaint.Rect(0, 0, width, height);
                            Shifted(dc, left, 0, () => FolderFlyout.PaintContent(dc, widget, box, style));
                            at += width + gap;
                        }
                    });
                    Assert.Equal(default, Up(flyout, OnTrack(track, 0.9f)));
                    flyout.Hide();
                }
                folder.Dispose();
            }
            finally
            {
                Feedback.Play = null;
            }
        });
    }

    // --- Driving the flyout ----------------------------------------------------------

    private static FolderFlyoutEvent Press(FolderFlyout flyout, Vector2 panel) =>
        flyout.Message(WM.WM_LBUTTONDOWN, 1, Point(flyout, panel));

    private static FolderFlyoutEvent Move(FolderFlyout flyout, Vector2 panel) =>
        flyout.Message(WM.WM_MOUSEMOVE, 1, Point(flyout, panel));

    private static FolderFlyoutEvent Up(FolderFlyout flyout, Vector2 panel) =>
        flyout.Message(WM.WM_LBUTTONUP, 0, Point(flyout, panel));

    /// <summary>The open folder laid out as the flyout lays it out, each line measured as the panel measures it.</summary>
    private static FolderPlan Plan(FolderFlyout flyout)
    {
        var folder = flyout.Folder!;
        return FolderFlyout.Build(
            Dark,
            Scale,
            folder.View,
            folder.Options,
            folder.Tiles.Select(tile => tile.Cell).ToList(),
            Work.bottom - Work.top,
            natural: FolderFlyout.Natural(folder.Tiles, folder.Options, Scale, Dark));
    }

    /// <summary>Line <paramref name="index"/>'s rectangle in panel px.</summary>
    private static D2D_RECT_F Card(FolderFlyout flyout, int index) => Plan(flyout).Cards[index];

    private static Vector2 Middle(D2D_RECT_F box) => new((box.left + box.right) / 2, (box.top + box.bottom) / 2);

    /// <summary>A point <paramref name="fraction"/> along a line, in the middle across it (panel px).</summary>
    private static Vector2 Along(D2D_RECT_F card, float fraction) =>
        new(card.left + (card.right - card.left) * fraction, (card.top + card.bottom) / 2);

    /// <summary>
    /// The volume row's track in panel px — from just inside its start to just inside its end, at
    /// its middle across — taken from the very slider the line presses against, so the test aims
    /// where the widget listens. Read once, before a press: laying the line out again mid-drag is
    /// not what the flyout does.
    /// </summary>
    private static (float From, float To, float Y) Track(FolderFlyout flyout)
    {
        var card = Card(flyout, 0);
        var box = PopupPaint.Rect(0, 0, card.right - card.left, card.bottom - card.top);
        var line = Assert.IsType<SoundPartTile>(flyout.Folder!.Tiles[0].Island);
        line.LayOut(box, FolderFlyout.StyleFor(Scale, Dark, FlyoutTheme.For(Dark)));
        var slider = Assert.Single(line.Placed.OfType<Slider>()).Bounds;
        return (card.left + slider.left + 2, card.left + slider.right - 1, card.top + ((slider.top + slider.bottom) / 2));
    }

    /// <summary>A point <paramref name="fraction"/> along <paramref name="track"/> (panel px).</summary>
    private static Vector2 OnTrack((float From, float To, float Y) track, float fraction) =>
        new(track.From + ((track.To - track.From) * fraction), track.Y);

    /// <summary>
    /// A point of the panel in the window's client px, as an lParam: over a bottom bar the panel
    /// sits as far into its window from the left as from the top, the room its shadow has.
    /// </summary>
    private static nint Point(FolderFlyout flyout, Vector2 panel)
    {
        var bounds = WindowShots.Extent(flyout.Hwnd);
        var room = (bounds.right - bounds.left - Plan(flyout).Width) / 2;
        var at = WindowShots.Client(flyout.Hwnd, (int)MathF.Round(room + panel.X), (int)MathF.Round(room + panel.Y));
        return (nint)(((at.y & 0xFFFF) << 16) | (at.x & 0xFFFF));
    }

    // --- Pictures --------------------------------------------------------------------

    private static string Pictures
    {
        get
        {
            var folder = Environment.GetEnvironmentVariable("KARPO_PNG_DIR") is { Length: > 0 } named
                ? named
                : Path.Combine(Path.GetTempPath(), "karpo-tests", "pictures");
            Directory.CreateDirectory(folder);
            return folder;
        }
    }

    private delegate void Painter(ID2D1DeviceContext* dc);

    /// <summary>The cards on a neutral backdrop, in the panel's own base and stroke.</summary>
    private static void Picture(float width, float height, float scale, FlyoutTheme theme, string file, Painter content)
    {
        const int margin = 16;
        var w = (uint)MathF.Ceiling(width) + (2 * margin);
        var h = (uint)MathF.Ceiling(height) + (2 * margin);
        ID2D1Factory1* factory = null;
        IWICImagingFactory* wic = null;
        IWICBitmap* bitmap = null;
        ID2D1RenderTarget* target = null;
        ID2D1DeviceContext* dc = null;
        try
        {
            ComUtil.Check(D2D1CreateFactory(D2D1_FACTORY_TYPE.D2D1_FACTORY_TYPE_SINGLE_THREADED, __uuidof<ID2D1Factory1>(), (void**)&factory));
            var clsid = CLSID.CLSID_WICImagingFactory2;
            ComUtil.Check(CoCreateInstance(&clsid, null, (uint)CLSCTX.CLSCTX_INPROC_SERVER, __uuidof<IWICImagingFactory>(), (void**)&wic));
            var format = GUID.GUID_WICPixelFormat32bppPBGRA;
            ComUtil.Check(wic->CreateBitmap(w, h, &format, WICBitmapCreateCacheOption.WICBitmapCacheOnLoad, &bitmap));
            var properties = new D2D1_RENDER_TARGET_PROPERTIES
            {
                type = D2D1_RENDER_TARGET_TYPE.D2D1_RENDER_TARGET_TYPE_SOFTWARE,
                pixelFormat = new D2D1_PIXEL_FORMAT { format = DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM, alphaMode = D2D1_ALPHA_MODE.D2D1_ALPHA_MODE_PREMULTIPLIED },
                dpiX = 96,
                dpiY = 96,
            };
            ComUtil.Check(((ID2D1Factory*)factory)->CreateWicBitmapRenderTarget(bitmap, &properties, &target));
            ComUtil.Check(target->QueryInterface(__uuidof<ID2D1DeviceContext>(), (void**)&dc));
            dc->BeginDraw();
            var backdrop = theme.Dark ? new DXGI_RGBA(0.20f, 0.24f, 0.30f, 1) : new DXGI_RGBA(0.72f, 0.78f, 0.86f, 1);
            dc->Clear(&backdrop);
            var panel = new D2D_RECT_F(margin, margin, margin + width, margin + height);
            PopupPaint.Fill(dc, panel, 8 * scale, PopupPaint.Ink(theme.Base));
            PopupPaint.Stroke(dc, panel, 8 * scale, MathF.Max(1, MathF.Round(scale)), PopupPaint.Ink(theme.Stroke));
            var context = dc;
            Shifted(context, margin, margin, () => content(context));
            ComUtil.Check(dc->EndDraw(null, null));
            Save(wic, bitmap, Path.Combine(Pictures, file));
        }
        finally
        {
            ComUtil.Release(ref dc);
            ComUtil.Release(ref target);
            ComUtil.Release(ref bitmap);
            ComUtil.Release(ref wic);
            ComUtil.Release(ref factory);
        }
    }

    private static void Shifted(ID2D1DeviceContext* dc, float x, float y, Action paint)
    {
        D2D_MATRIX_3X2_F before;
        dc->GetTransform(&before);
        var moved = before;
        moved._31 = (x * before._11) + (y * before._21) + before._31;
        moved._32 = (x * before._12) + (y * before._22) + before._32;
        dc->SetTransform(&moved);
        try
        {
            paint();
        }
        finally
        {
            dc->SetTransform(&before);
        }
    }

    private static void Save(IWICImagingFactory* wic, IWICBitmap* bitmap, string path)
    {
        IWICStream* stream = null;
        IWICBitmapEncoder* encoder = null;
        IWICBitmapFrameEncode* frame = null;
        try
        {
            ComUtil.Check(wic->CreateStream(&stream));
            fixed (char* name = path)
            {
                ComUtil.Check(stream->InitializeFromFilename(name, GENERIC_WRITE));
            }
            var png = GUID.GUID_ContainerFormatPng;
            ComUtil.Check(wic->CreateEncoder(&png, null, &encoder));
            ComUtil.Check(encoder->Initialize((IStream*)stream, WICBitmapEncoderCacheOption.WICBitmapEncoderNoCache));
            ComUtil.Check(encoder->CreateNewFrame(&frame, null));
            ComUtil.Check(frame->Initialize(null));
            uint width;
            uint height;
            ComUtil.Check(bitmap->GetSize(&width, &height));
            ComUtil.Check(frame->SetSize(width, height));
            var format = GUID.GUID_WICPixelFormat32bppPBGRA;
            ComUtil.Check(frame->SetPixelFormat(&format));
            ComUtil.Check(frame->WriteSource((IWICBitmapSource*)bitmap, null));
            ComUtil.Check(frame->Commit());
            ComUtil.Check(encoder->Commit());
        }
        finally
        {
            ComUtil.Release(ref frame);
            ComUtil.Release(ref encoder);
            ComUtil.Release(ref stream);
        }
    }

    private static void OnHiddenDesktop(Action<Gfx, HINSTANCE> body)
    {
        Exception? failed = null;
        HDESK hidden = default;
        var thread = new Thread(() =>
        {
            try
            {
                fixed (char* name = "karpo-slider-test-" + Guid.NewGuid().ToString("N"))
                {
                    hidden = CreateDesktopW(name, null, null, 0, 0x10000000u /* GENERIC_ALL */, null);
                }
                Assert.True(hidden != HDESK.NULL, "no hidden desktop");
                // Before this thread owns any window: its windows are then made there.
                Assert.True(SetThreadDesktop(hidden));
                var options = new DispatcherQueueOptions
                {
                    dwSize = (uint)sizeof(DispatcherQueueOptions),
                    threadType = DISPATCHERQUEUE_THREAD_TYPE.DQTYPE_THREAD_CURRENT,
                    apartmentType = DISPATCHERQUEUE_THREAD_APARTMENTTYPE.DQTAT_COM_STA,
                };
                IDispatcherQueueController* controller = null;
                ComUtil.Check(CreateDispatcherQueueController(options, &controller));
                // A folder holds a clock, which would otherwise read this PC's own.
                Clock.Now = () => new DateTime(2026, 9, 22, 14, 40, 7, DateTimeKind.Local);
                try
                {
                    using var gfx = new Gfx(gpu: false);
                    var instance = (HINSTANCE)(void*)GetModuleHandleW(null);
                    FlyoutOffscreenTests.Register(instance);
                    body(gfx, instance);
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
        thread.Start();
        thread.Join();
        HiddenDesktop.Close(hidden);
        if (failed is not null)
        {
            throw new Xunit.Sdk.XunitException("On the offscreen thread: " + failed);
        }
    }
}
