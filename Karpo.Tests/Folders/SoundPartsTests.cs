// The sound panel's three lines (docs\widgets.md §9, the fourth conversion): which part a kind is,
// what each shows and in what order, how much of it fits, what a press and a click do — and the
// guard that keeps a dragged slider from being pulled back by the service's own echo.
//
// A made-up machine stands behind every card (SoundParts.Made), with whatever devices and apps a
// test needs, and it records what it was asked to set. Nothing here reads or sets this PC's audio:
// on any isolated run the source is the samples anyway, and these tests bring their own on top.

using System.Numerics;
using Karpo.Dock;
using Karpo.Dock.QuickSettings;
using Karpo.Dock.QuickSettings.Controls;
using Karpo.Dock.QuickSettings.Services;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using Color = Windows.UI.Color;

namespace Karpo.Tests.Folders;

public sealed unsafe class SoundPartsTests
{
    // At a scale of one: the heading takes 8 + 36, the card keeps 8 under its last row, and a mix
    // row and a device row are each 40 with 2 of air (SoundPartTile.HeadingBottom, MixPitch,
    // DevicePitch). So a card of 52 + 42k holds exactly k rows.
    private const float Chrome = 52;
    private const float Pitch = 42;

    // A device is an item whose band is the line's foot (§436): under the last only its band's half of the 2 of air,
    // no 8 — a card of devices of 43 + 42k holds exactly k rows.
    private const float DeviceChrome = 43;

    private static float HoldingExactly(int rows, bool devices = false) => (devices ? DeviceChrome : Chrome) + (Pitch * rows);

    // --- Pure -----------------------------------------------------------------------------

    /// <summary>
    /// A run that may not reach the person's world gets the samples, whatever draws the card. The
    /// guarantee is in the thing being reached (§11): a component that set this PC's default device
    /// from a test would be setting it for real.
    /// </summary>
    [Fact]
    public void AnIsolatedRunNeverReachesThisPcsAudio()
    {
        Assert.True(Karpo.Core.Config.IsIsolated, "The tests are meant to run on a config of their own.");
        using var source = SoundParts.Source();
        Assert.IsType<SampleSound>(source);
    }

    /// <summary>
    /// The device in use comes first, and the rest keep Windows' order. The order is the one thing a
    /// component contributes to filling its room, and the row that answers "what am I listening on"
    /// is the one that must never be summarised away.
    /// </summary>
    [Fact]
    public void TheDeviceInUseComesFirst()
    {
        var devices = new[] { Device("a"), Device("b"), Device("c", inUse: true), Device("d") };
        Assert.Equal(["c", "a", "b", "d"], SoundPartTile.InUseFirst(devices).Select(device => device.Id));
        Assert.Empty(SoundPartTile.InUseFirst([]));
        // None in use (a device list read mid-change) is Windows' order, untouched.
        Assert.Equal(["a", "b"], SoundPartTile.InUseFirst([Device("a"), Device("b")]).Select(device => device.Id));
    }

    [Fact]
    public void EachPartHasThePanelsOwnHeading()
    {
        Assert.Equal("Output device", SoundPartTile.HeadingOf(SoundPart.Output));
        Assert.Equal("Input device", SoundPartTile.HeadingOf(SoundPart.Input));
        Assert.Equal("Volume mixer", SoundPartTile.HeadingOf(SoundPart.Mixer));
        Assert.Equal("Volume", SoundPartTile.HeadingOf(SoundPart.Volume));
    }

    /// <summary>
    /// A tick is nothing on a run that may not reach the person's world, unless a test hands one
    /// over to count. It was a real beep on every run, so a test that dragged a volume slider and let
    /// go played one out of the person's speakers.
    /// </summary>
    [Fact]
    public void ATickIsSilentOnAnIsolatedRun()
    {
        Assert.True(Karpo.Core.Config.IsIsolated, "The tests are meant to run on a config of their own.");
        Feedback.Play = null;
        Assert.Null(Feedback.Play);
    }

    /// <summary>
    /// Four regions on one store: one source, started once; a change is read once however many
    /// regions are drawn from it, and every region is told to redraw; and the last to go stops it,
    /// so nothing listens to Windows for nobody. Before it was shared, opening the folder started
    /// four sources — 15 ms on the dock's thread where the flyout paid 3.7, in a published build.
    /// </summary>
    [Fact]
    public void FourRegionsShareOneSourceAndTheLastToGoStopsIt()
    {
        var machine = new Machine();
        var made = 0;
        var store = new SoundStore(() =>
        {
            made++;
            return machine;
        });
        var woken = new int[4];
        var wakes = Enumerable.Range(0, 4).Select(index => (Action)(() => woken[index]++)).ToArray();

        foreach (var wake in wakes)
        {
            store.Watch(wake);
        }
        Assert.Equal(1, made);
        Assert.Equal(1, machine.Starts);
        Assert.Equal(1, machine.Reads);

        machine.Tell();
        Assert.All(woken, count => Assert.Equal(1, count));
        foreach (var _ in wakes)
        {
            store.Take();
        }
        Assert.Equal(2, machine.Reads);

        foreach (var wake in wakes.Take(3))
        {
            store.Unwatch(wake);
        }
        Assert.False(machine.Disposed);
        store.Unwatch(wakes[3]);
        Assert.True(machine.Disposed);
        Assert.Equal(0, store.Watching);

        // And a region looking again afterwards starts a fresh one.
        store.Watch(wakes[0]);
        Assert.Equal(2, made);
    }

    // --- The cards ------------------------------------------------------------------------

    /// <summary>Each kind is its own part of the one tile, and a card is made without asking Windows anything.</summary>
    [UiFact]
    public void EachKindIsItsPartAndMakingOneAsksNothing()
    {
        Offscreen.OnStaThread(gfx =>
        {
            foreach (var (kind, part) in new[]
            {
                (ListKind.SoundOutput, SoundPart.Output), (ListKind.SoundInput, SoundPart.Input),
                (ListKind.SoundMixer, SoundPart.Mixer), (ListKind.SoundVolume, SoundPart.Volume),
            })
            {
                var machine = new Machine();
                using var tile = Tile(gfx, kind, machine);
                Assert.Equal(part, tile.Region);
                Assert.Equal(0, machine.Starts);
                // The first look starts it, and only the first.
                tile.LayOut(Card(200), Style());
                tile.LayOut(Card(200), Style());
                Assert.Equal(1, machine.Starts);
            }
        });
    }

    /// <summary>
    /// The mixer's room, at every edge it has: none; exactly enough under its heading; one over, where
    /// the heading gives way before a row does and the rows are drawn closer, so all four stand; and far
    /// over, where it shows what fits and scrolls to the rest (ListScroll.cs) rather than counting them.
    /// </summary>
    [UiFact]
    public void TheMixerFillsItsRoomAndScrollsToTheRest()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var machine = new Machine();
            using var tile = Tile(gfx, ListKind.SoundMixer, machine);
            var box = Card(HoldingExactly(3));

            tile.LayOut(box, Style());
            Assert.Empty(Sliders(tile));
            Assert.Equal("No apps are playing sound.", Caption(tile));

            machine.Apps = [App("a"), App("b"), App("c")];
            machine.Tell();
            tile.LayOut(box, Style());
            Assert.Equal(["a", "b", "c"], Sliders(tile).Select(slider => slider.Name));
            Assert.Null(Caption(tile));
            Assert.True(Headed(tile));

            machine.Apps = [App("a"), App("b"), App("c"), App("d")];
            machine.Tell();
            tile.LayOut(box, Style());
            Assert.Equal(["a", "b", "c", "d"], Sliders(tile).Select(slider => slider.Name));
            Assert.Null(Caption(tile));
            Assert.False(Headed(tile));

            machine.Apps = [.. Enumerable.Range(0, 30).Select(index => App($"app {index:00}"))];
            machine.Tell();
            tile.LayOut(box, Style());
            // The slot a summary took is a row's now: as many whole rows as the room holds.
            Assert.Equal(5, Sliders(tile).Count());
            Assert.Null(Caption(tile));
            var laid = tile.Scrolled(box, Style(), WidgetSurface.List);
            Assert.True(laid.Scrolls);
            tile.LayOut(laid.Moved, Style());
            Assert.Equal(30, Sliders(tile).Count());
            Assert.True(Headed(tile), "scrolled, the rows stand under the heading, which stays");
        });
    }

    /// <summary>
    /// A device list with more than fits keeps the one in use first whatever else is out of sight: a
    /// line of two rows shows it at its top, and scrolls to the others (ListScroll.cs).
    /// </summary>
    [UiFact]
    public void AShortDeviceListScrollsFromTheOneInUse()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var machine = new Machine
            {
                Outputs = [Device("a"), Device("b"), Device("c"), Device("d"), Device("e", inUse: true)],
            };
            using var tile = Tile(gfx, ListKind.SoundOutput, machine);
            var box = Card(ListFormPictures.Rows(2, 1f, 1f));
            tile.LayOut(box, Style());
            var shown = Rows(tile).ToList();
            Assert.NotEmpty(shown);
            Assert.Equal("device e", shown[0].Title);
            Assert.True(shown[0].Selected);
            Assert.Null(Caption(tile));
            var laid = tile.Scrolled(box, Style(), WidgetSurface.List);
            Assert.True(laid.Scrolls);
            tile.LayOut(laid.Moved, Style());
            Assert.Equal(["device e", "device a", "device b", "device c", "device d"], Rows(tile).Select(row => row.Title));
        });
    }

    /// <summary>
    /// A click on another device makes it the default, through the part's own call — the output
    /// sets the output, the input the input — and the device in use takes no click at all.
    /// </summary>
    [UiFact]
    public void AClickOnADeviceChoosesItAndTheOneInUseTakesNone()
    {
        Offscreen.OnStaThread(gfx =>
        {
            foreach (var kind in new[] { ListKind.SoundOutput, ListKind.SoundInput })
            {
                var machine = new Machine
                {
                    Outputs = [Device("speakers", inUse: true), Device("headphones")],
                    Inputs = [Device("array", inUse: true), Device("headset")],
                };
                using var tile = Tile(gfx, kind, machine);
                var box = Card(200);
                tile.LayOut(box, Style());
                var rows = Rows(tile).ToList();
                Assert.Equal(0, tile.CardTarget(Centre(rows[0]), box, Style(), WidgetSurface.List));
                var other = tile.CardTarget(Centre(rows[1]), box, Style(), WidgetSurface.List);
                Assert.NotEqual(0, other);
                _ = tile.Click(other);
                if (kind == ListKind.SoundOutput)
                {
                    Assert.Equal("headphones", machine.Chosen);
                    Assert.Null(machine.ChosenInput);
                }
                else
                {
                    Assert.Equal("headset", machine.ChosenInput);
                    Assert.Null(machine.Chosen);
                }
            }
        });
    }

    /// <summary>The microphones' mute sits on their heading line and mutes them; the outputs have none.</summary>
    [UiFact]
    public void TheMicrophonesMuteIsTheirsAlone()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var machine = new Machine { Outputs = [Device("speakers", inUse: true)], Inputs = [Device("array", inUse: true)] };
            using var input = Tile(gfx, ListKind.SoundInput, machine);
            var box = Card(200);
            input.LayOut(box, Style());
            var mute = Assert.Single(input.Placed.OfType<Button>(), button => button.Name == "Mute microphone");
            _ = input.Click(input.CardTarget(Centre(mute), box, Style(), WidgetSurface.List));
            Assert.True(machine.InputMuted);

            using var output = Tile(gfx, ListKind.SoundOutput, machine);
            output.LayOut(box, Style());
            Assert.DoesNotContain(output.Placed, control => control.Name == "Mute microphone");
        });
    }

    /// <summary>
    /// The anti-clobber guard, held by a test because it is exactly the line somebody simplifies
    /// away having never seen it matter. While a slider is held, a refresh that reads a different
    /// level back from Windows must not move the thumb; once it is let go, the next refresh must.
    /// </summary>
    [UiFact]
    public void AHeldSliderIsNotPulledBackByARefresh()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var machine = new Machine { Apps = [App("a", 0.3f), App("b", 0.6f)] };
            using var tile = Tile(gfx, ListKind.SoundMixer, machine);
            var box = Card(HoldingExactly(3));
            tile.LayOut(box, Style());
            var slider = Sliders(tile).First(each => each.Name == "a");
            Assert.Equal(30, slider.Value);

            // A press two thirds along the track takes it there, and holds it.
            var at = new Vector2(slider.Bounds.left + ((slider.Bounds.right - slider.Bounds.left) * 2 / 3), Centre(slider).Y);
            Assert.True(tile.CardPress(at, box, Style(), WidgetSurface.List));
            Assert.True(slider.Dragging);
            var dragged = slider.Value;
            Assert.NotEqual(30, dragged);
            Assert.Equal(dragged / 100f, machine.Levels["a"], 2);

            // Windows reports a level of its own while the thumb is held: the thumb stays put.
            machine.Apps = [App("a", 0.9f), App("b", 0.6f)];
            machine.Tell();
            tile.LayOut(box, Style());
            Assert.Same(slider, Sliders(tile).First(each => each.Name == "a"));
            Assert.Equal(dragged, slider.Value);

            // Let go, and the next refresh shows what Windows says.
            tile.CardRelease();
            Assert.False(slider.Dragging);
            machine.Tell();
            tile.LayOut(box, Style());
            Assert.Equal(90, slider.Value);
        });
    }

    /// <summary>
    /// Apps come and go while somebody is looking, and one may go while its own slider is held. The
    /// others keep their controls; the held one lets go of the pointer rather than holding it for
    /// nobody; and one arriving takes its place in the order.
    /// </summary>
    [UiFact]
    public void AppsArriveAndLeaveWhileTheCardIsLookedAt()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var machine = new Machine { Apps = [App("a"), App("b"), App("c")] };
            using var tile = Tile(gfx, ListKind.SoundMixer, machine);
            var box = Card(HoldingExactly(4));
            tile.LayOut(box, Style());
            var held = Sliders(tile).First(each => each.Name == "b");
            Assert.True(tile.CardPress(Centre(held), box, Style(), WidgetSurface.List));

            // Another leaves: the held one is still held, the same control.
            machine.Apps = [App("b"), App("c")];
            machine.Tell();
            tile.LayOut(box, Style());
            Assert.Same(held, Sliders(tile).First(each => each.Name == "b"));
            Assert.True(held.Dragging);

            // The held one itself leaves: it lets go, and a release afterwards is harmless.
            machine.Apps = [App("c")];
            machine.Tell();
            tile.LayOut(box, Style());
            Assert.False(held.Dragging);
            tile.CardRelease();

            // One arrives, and stands in the order the service gives.
            machine.Apps = [App("a new one"), App("c")];
            machine.Tell();
            tile.LayOut(box, Style());
            Assert.Equal(["a new one", "c"], Sliders(tile).Select(slider => slider.Name));
        });
    }

    /// <summary>
    /// A press is the card's only on a slider. On an app's icon it is left to the folder, which
    /// makes it a click — and the click mutes that app, as it does in the flyout.
    /// </summary>
    [UiFact]
    public void OnlyASliderTakesThePressAndAnIconIsAClick()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var machine = new Machine { Apps = [App("a")] };
            using var tile = Tile(gfx, ListKind.SoundMixer, machine);
            var box = Card(HoldingExactly(2));
            tile.LayOut(box, Style());
            var icon = Assert.Single(tile.Placed.OfType<Button>());
            Assert.False(tile.CardPress(Centre(icon), box, Style(), WidgetSurface.List));
            _ = tile.Click(tile.CardTarget(Centre(icon), box, Style(), WidgetSurface.List));
            Assert.Contains("a", machine.MutedApps);
            // Nothing under the pointer is nothing at all.
            Assert.False(tile.CardPress(new Vector2(1, 1), box, Style(), WidgetSurface.List));
            Assert.Equal(0, tile.CardTarget(new Vector2(1, 1), box, Style(), WidgetSurface.List));
        });
    }

    /// <summary>
    /// The volume row sets the output's level by a drag and mutes it by a click on its speaker, as
    /// the flyout's does — and a drag settles once, which is the one tick the flyout plays.
    /// </summary>
    [UiFact]
    public void TheVolumeRowSetsTheOutputAndTicksOnceWhenItSettles()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var ticks = 0;
            Feedback.Play = _ => ticks++;
            try
            {
                var machine = new Machine { Outputs = [Device("speakers", inUse: true)], Level = 0.2f };
                using var tile = Tile(gfx, ListKind.SoundVolume, machine);
                var box = Card(40);
                tile.LayOut(box, Style());
                var slider = Assert.Single(Sliders(tile));
                Assert.Equal(20, slider.Value);
                // No heading: the flyout's volume row has none above it.
                Assert.DoesNotContain(tile.Placed.OfType<Label>(), label => label.Text == "Volume");

                var start = new Vector2(slider.Bounds.left + 30, Centre(slider).Y);
                Assert.True(tile.CardPress(start, box, Style(), WidgetSurface.List));
                tile.CardDrag(start with { X = slider.Bounds.right - 20 }, box, Style(), WidgetSurface.List);
                tile.CardDrag(start with { X = slider.Bounds.right - 10 }, box, Style(), WidgetSurface.List);
                Assert.True(machine.Level > 0.8f, $"the level is {machine.Level}");
                Assert.Equal(0, ticks);
                tile.CardRelease();
                Assert.Equal(1, ticks);

                var mute = Assert.Single(tile.Placed.OfType<Button>());
                _ = tile.Click(tile.CardTarget(Centre(mute), box, Style(), WidgetSurface.List));
                Assert.True(machine.Muted);
            }
            finally
            {
                Feedback.Play = null;
            }
        });
    }

    /// <summary>
    /// A PC with no output still shows the volume's row, at half, greyed and taking no pointer, its
    /// tooltip saying why (§247); the output's menu keeps its heading over a greyed row saying there
    /// is none, rather than leaving the choice out.
    /// </summary>
    [UiFact]
    public void AVolumeRowWithNoOutputIsGreyed()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var machine = new Machine { Present = false };
            using var tile = Tile(gfx, ListKind.SoundVolume, machine);
            var box = Card(40);
            tile.LayOut(box, Style());
            var slider = Assert.Single(Sliders(tile));
            Assert.Equal(VolumeIsland.UnavailableLevel, slider.Value);
            Assert.Null(Caption(tile));
            Assert.NotNull(tile.UnavailableOn(0));
            Assert.False(tile.PressOn(Centre(slider), box, Style(), WidgetSurface.List));
            Assert.Equal(0, tile.TargetOn(Centre(slider), box, Style(), WidgetSurface.List));
            Assert.Equal(tile.UnavailableOn(0), tile.TipOn(Centre(slider), box, Style(), WidgetSurface.List)?.Text);

            using var output = Tile(gfx, ListKind.SoundOutput, machine);
            Assert.Null(output.UnavailableOn(0));
            var (items, _) = Karpo.Dock.Dock.ActRows([WidgetAct.ChooseDevice], output);
            Assert.Equal(SoundPartTile.HeadingOf(SoundPart.Output), items[0].Text);
            Assert.Equal(S.WidgetSoundNoOutputDevice, items[1].Text);
            Assert.False(items[1].Enabled);
        });
    }

    /// <summary>The wheel over a mix's slider steps that app; anywhere else it is left to the folder.</summary>
    [UiFact]
    public void TheWheelStepsASliderAndOnlyASlider()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var machine = new Machine { Apps = [App("a", 0.5f)] };
            using var tile = Tile(gfx, ListKind.SoundMixer, machine);
            var box = Card(HoldingExactly(2));
            tile.LayOut(box, Style());
            var slider = Assert.Single(Sliders(tile));
            Assert.True(tile.CardWheel(Centre(slider), box, Style(), WidgetSurface.List, 1));
            Assert.True(slider.Value > 50);
            Assert.False(tile.CardWheel(new Vector2(1, 1), box, Style(), WidgetSurface.List, 1));
        });
    }

    /// <summary>
    /// A card of each part in every state it has, light and dark — and nothing on any of them
    /// reaches the card's edges, because an edge-to-edge line inside a card is the folder's own
    /// word for "two widgets" (§9), and these rows keep the page's gutters.
    /// </summary>
    [UiFact]
    public void APictureOfEveryStateAndNothingReachesTheEdge()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var full = new Machine
            {
                Outputs = [Device("speakers", inUse: true), Device("headphones"), Device("display")],
                Inputs = [Device("array", inUse: true), Device("headset")],
                Apps = [App("System sounds", 1f, system: true), App("Browser", 0.8f), App("Music", 0.35f, muted: true), App("Game", 0.6f)],
            };
            var many = new Machine
            {
                Outputs = [.. Enumerable.Range(0, 6).Select(index => Device($"out {index}", inUse: index == 4))],
                Inputs = [],
                Apps = [.. Enumerable.Range(0, 12).Select(index => App($"App {index:00}", index / 12f))],
            };
            var none = new Machine();
            foreach (var dark in new[] { true, false })
            {
                foreach (var (name, machine) in new[] { ("full", full), ("many", many), ("none", none) })
                {
                    foreach (var (kind, height) in new[]
                    {
                        // The heights each line is given in a folder at a scale of one: five rows, four,
                        // one and eight (Sections.ListCellsOf; FolderFlyout's list case).
                        (ListKind.SoundOutput, 200f), (ListKind.SoundInput, 159f), (ListKind.SoundVolume, 40f), (ListKind.SoundMixer, 323f),
                    })
                    {
                        using var tile = Tile(gfx, kind, machine);
                        var style = Style(dark);
                        var box = Card(height);
                        var file = $"sound-part-{kind.ToString().ToLowerInvariant()}-{name}-{(dark ? "dark" : "light")}.png";
                        const float width = 360;
                        // On nothing first, so every pixel with ink on it is the card's own.
                        (float Left, float Top, float Right, float Bottom) ink = default;
                        Offscreen.Bitmap(width, height, dc => tile.PaintOn(dc, box, style, WidgetSurface.List),
                            bitmap => ink = Offscreen.InkOf(bitmap, (uint)width, (uint)height));
                        Assert.True(ink.Right > ink.Left, $"{file}: the card drew nothing");
                        Assert.True(ink.Left >= Edge && ink.Right <= width - Edge,
                            $"{file}: ink from {ink.Left} to {ink.Right} reaches within {Edge} px of the card's edge");
                        // Then on the folder's own colour, for the eye.
                        Offscreen.Bitmap(width, height, dc =>
                        {
                            var back = dark ? new DXGI_RGBA(0.17f, 0.17f, 0.17f, 1) : new DXGI_RGBA(0.98f, 0.98f, 0.98f, 1);
                            dc->Clear(&back);
                            tile.PaintOn(dc, box, style, WidgetSurface.List);
                        },
                        bitmap => Offscreen.Save(bitmap, Path.Combine(Offscreen.Pictures, file)));
                    }
                }
            }
        });
    }

    // --- How tall a line is (IListContent) -------------------------------------------------------

    /// <summary>
    /// A list line is as tall as what its region has — and exactly that: at its own height the
    /// layout shows its heading and every device or app with nothing summarised, and one pixel less
    /// the heading gives way before a row does. So the number and the layout cannot drift apart, and
    /// the line has no room nobody asked for, which was the complaint every region here was designed
    /// against.
    /// </summary>
    [UiFact]
    public void ALineIsExactlyAsTallAsItsRows()
    {
        Offscreen.OnStaThread(gfx =>
        {
            foreach (var (kind, count) in new[] { (ListKind.SoundOutput, 3), (ListKind.SoundInput, 2), (ListKind.SoundMixer, 4) })
            {
                var machine = new Machine
                {
                    Outputs = [.. Enumerable.Range(0, count).Select(index => Device($"o{index}", inUse: index == 0))],
                    Inputs = [.. Enumerable.Range(0, count).Select(index => Device($"i{index}", inUse: index == 0))],
                    Apps = [.. Enumerable.Range(0, count).Select(index => App($"app {index}"))],
                };
                using var tile = Tile(gfx, kind, machine);
                var height = tile.ListHeight(360, Style());

                // The arithmetic the header of this file states: 52 of chrome and 42 a row.
                Assert.Equal(HoldingExactly(count, devices: kind != ListKind.SoundMixer), height, 1);

                tile.LayOut(Card(height), Style());
                var shown = kind == ListKind.SoundMixer ? Sliders(tile).Count() : Rows(tile).Count();
                Assert.True(count == shown, $"{kind} at its own height showed {shown} of {count}");
                Assert.Null(Caption(tile));
                Assert.True(Headed(tile), $"{kind} at its own height has no heading");

                tile.LayOut(Card(height - 1), Style());
                var closer = kind == ListKind.SoundMixer ? Sliders(tile).Count() : Rows(tile).Count();
                Assert.True(count == closer, $"{kind} a pixel short showed {closer} of {count}");
                Assert.Null(Caption(tile));
                Assert.False(Headed(tile), $"{kind} a pixel short kept its heading");
            }
        });
    }

    /// <summary>
    /// Nothing to list is the heading and the one caption saying so; the volume is its size's form, as the
    /// brightness's line beside it is (SliderLine, §432): Compact one row.
    /// </summary>
    [UiFact]
    public void AnEmptyRegionAndTheVolumeAreTheirOwnHeight()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var nothing = new Machine();
            foreach (var kind in new[] { ListKind.SoundOutput, ListKind.SoundInput, ListKind.SoundMixer })
            {
                using var tile = Tile(gfx, kind, nothing);
                // 8 above the heading, the heading's 36, the caption's 28 and 8 under it.
                Assert.Equal(80, tile.ListHeight(360, Style()), 1);
            }
            using var volume = Tile(gfx, ListKind.SoundVolume, new Machine());
            Assert.Equal(LineRows.Px(1, Style()), volume.ListHeight(360, Style()), 1);
        });
    }

    /// <summary>
    /// The folder opening is the look that counts: the source starts when the folder shows the
    /// region, before the folder measures its lines, so the first measurement is the devices there
    /// are. Started at the first draw instead, the line was measured with none, and the panel
    /// opened short and grew. And making the region still starts nothing (§11).
    /// </summary>
    [UiFact]
    public void TheSourceStartsWhenTheFolderOpensNotWhenItFirstDraws()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var machine = new Machine { Outputs = [Device("a", inUse: true), Device("b"), Device("c")] };
            using var tile = Tile(gfx, ListKind.SoundOutput, machine);
            Assert.Equal(0, machine.Starts);

            tile.Visible(true);

            Assert.Equal(1, machine.Starts);
            Assert.Equal(3, tile.State.Outputs.Count);
            Assert.Equal(HoldingExactly(3, devices: true), tile.ListHeight(360, Style()), 1);
            // Looking again starts nothing more: one source, started once.
            tile.Visible(true);
            Assert.Equal(1, machine.Starts);
        });
    }

    // --- Hidden devices (docs\widgets.md §159) ---------------------------------------------------

    /// <summary>
    /// At rest a hidden device is not listed: not as a row, not in the line's height, not in its
    /// menu — and a line whose every device is hidden says it has none.
    /// </summary>
    [UiFact]
    public void AHiddenDeviceIsNotListedAtRest()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var machine = new Machine { Outputs = [Device("speakers", inUse: true), Device("headphones"), Device("display")] };
            using var tile = Tile(gfx, ListKind.SoundOutput, machine);
            tile.Settings.HiddenDevices = ["headphones"];
            tile.LayOut(Card(HoldingExactly(3)), Style());

            Assert.Equal(["device speakers", "device display"], Rows(tile).Select(row => row.Title));
            Assert.Equal(HoldingExactly(2, devices: true), tile.ListHeight(360, Style()), 1);
            Assert.Equal(["speakers", "display"], tile.MenuDevices.Select(device => device.Id));
            // Nothing to edit at rest: a click is the row's own.
            Assert.Equal(0, tile.CardEditTarget(Centre(Rows(tile).First()), Card(HoldingExactly(3)), Style(), WidgetSurface.List));

            tile.Settings.HiddenDevices = ["speakers", "headphones", "display"];
            tile.LayOut(Card(HoldingExactly(3)), Style());
            Assert.Empty(Rows(tile));
            Assert.Equal(S.WidgetSoundNoOutputDevice, Caption(tile));
        });
    }

    /// <summary>
    /// Arranged, the line lists every device, a hidden one greyed where it stands, and a click on
    /// a device hides it — or shows it again where it was hidden — the in-use one included.
    /// </summary>
    [UiFact]
    public void InEditModeEveryDeviceIsListedAndAClickHidesOrShowsIt()
    {
        Offscreen.OnStaThread(gfx =>
        {
            foreach (var kind in new[] { ListKind.SoundOutput, ListKind.SoundInput })
            {
                var machine = new Machine
                {
                    Outputs = [Device("speakers", inUse: true), Device("headphones")],
                    Inputs = [Device("array", inUse: true), Device("headset")],
                };
                using var tile = Tile(gfx, kind, machine);
                var (inUse, other) = kind == ListKind.SoundOutput ? ("speakers", "headphones") : ("array", "headset");
                tile.Settings.HiddenDevices = [other];
                tile.Arranged = true;
                var box = Card(HoldingExactly(2));
                tile.LayOut(box, Style());

                var rows = Rows(tile).ToList();
                Assert.Equal(2, rows.Count);
                Assert.True(rows[0].Enabled);
                Assert.False(rows[1].Enabled);
                Assert.Equal(HoldingExactly(2, devices: true), tile.ListHeight(360, Style()), 1);

                // The hidden one shown again: the field goes, nothing being hidden.
                var item = tile.Settings.Clone();
                var shown = tile.CardEditTarget(Centre(rows[1]), box, Style(), WidgetSurface.List);
                Assert.NotEqual(0, shown);
                Assert.True(tile.EditClick(shown, item));
                Assert.Null(item.HiddenDevices);

                // The one in use hidden, the other still hidden beside it.
                item = tile.Settings.Clone();
                Assert.True(tile.EditClick(tile.CardEditTarget(Centre(rows[0]), box, Style(), WidgetSurface.List), item));
                Assert.Equal([other, inUse], item.HiddenDevices!);
                // Choosing is not edit mode's: nothing was set in Windows.
                Assert.Null(machine.Chosen);
                Assert.Null(machine.ChosenInput);
            }
        });
    }

    /// <summary>Hidden devices are kept on the two device lines alone, each once, and never written empty.</summary>
    [Fact]
    public void HiddenDevicesAreTheDeviceLinesAlone()
    {
        var output = ListKind.SoundOutput.New();
        output.HiddenDevices = ["a", "a", " ", "b"];
        output.Normalize();
        Assert.Equal(["a", "b"], output.HiddenDevices!);
        output.SetDeviceHidden("a", hidden: false);
        output.SetDeviceHidden("b", hidden: false);
        Assert.Null(output.HiddenDevices);

        // The mixer is a style of the volume widget, which has no device styles since §270 (the
        // devices are widgets of their own), so it keeps none; nor does any other line.
        var mixer = ListKind.SoundMixer.New();
        mixer.HiddenDevices = ["a"];
        mixer.Normalize();
        Assert.Null(mixer.HiddenDevices);
        var battery = ListKind.Battery.New();
        battery.HiddenDevices = ["a"];
        battery.Normalize();
        Assert.Null(battery.HiddenDevices);
    }

    // --- The made-up machine, and the cards it stands behind -------------------------------------

    private sealed class Machine : ISoundSource
    {
        public int Starts { get; private set; }
        public List<AudioOutput> Outputs { get; set; } = [];
        public List<AudioOutput> Inputs { get; set; } = [];
        public List<AudioApp> Apps { get; set; } = [];
        public bool InputMuted { get; private set; }
        public bool Present { get; set; } = true;
        public float Level { get; set; } = 0.5f;
        public bool Muted { get; private set; }
        public string? Chosen { get; private set; }
        public string? ChosenInput { get; private set; }
        public Dictionary<string, float> Levels { get; } = [];
        public HashSet<string> MutedApps { get; } = [];
        private Changes? news;

        public void Start(Changes changes)
        {
            Starts++;
            news = changes;
        }

        /// <summary>The machine changed: it says so, as the service does, and the card reads it on its next look.</summary>
        public void Tell() => news?.Add(SoundNews.Devices | SoundNews.Sessions);

        public int Reads { get; private set; }
        public bool Disposed { get; private set; }

        public SoundState Read(int changes)
        {
            Reads++;
            return new(Present, Level, Muted, [.. Outputs], [.. Apps], [.. Inputs], InputMuted);
        }

        public void SetVolume(float value) => Level = value;

        public void SetMuted(bool muted) => Muted = muted;

        public void SetDefault(string id) => Chosen = id;

        public void SetDefaultInput(string id) => ChosenInput = id;

        public void SetInputMuted(bool muted) => InputMuted = muted;

        public void SetAppVolume(string key, float value) => Levels[key] = value;

        public void SetAppMuted(string key, bool muted)
        {
            if (muted)
            {
                MutedApps.Add(key);
            }
            else
            {
                MutedApps.Remove(key);
            }
        }

        public void Dispose() => Disposed = true;
    }

    private static AudioOutput Device(string id, bool inUse = false) =>
        new(id, $"device {id}", char.ConvertFromUtf32(0xE7F5), inUse);

    // No executable and no app id: the icon is the app glyph, so nothing loads in the background
    // and the card is the same on every run.
    private static AudioApp App(string name, float level = 0.5f, bool muted = false, bool system = false) =>
        new(name, name, level, muted, system, null, null);

    private static SoundPartTile Tile(Gfx gfx, ListKind kind, Machine machine)
    {
        var widget = kind.New();
        SoundParts.Made = () => machine;
        try
        {
            return new SoundPartTile(Karpo.Core.Config.IslandKey(null, widget.Id), widget, gfx, new Karpo.Core.Config());
        }
        finally
        {
            SoundParts.Made = null;
        }
    }

    private static Look LookFor(bool dark) => new(
        Glass: false,
        Dark: dark,
        Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4),
        AccentLight2: Color.FromArgb(255, 0x99, 0xEB, 0xFF),
        AccentDark1: Color.FromArgb(255, 0x00, 0x5F, 0xB8));

    private static TileStyle Style(bool dark = true) => new(1f, LookFor(dark), FlyoutTheme.For(LookFor(dark)), 1, 1);

    private static D2D_RECT_F Card(float height) => new(0, 0, 360, height);

    private static Vector2 Centre(Widget control) =>
        new((control.Bounds.left + control.Bounds.right) / 2, (control.Bounds.top + control.Bounds.bottom) / 2);

    private static IEnumerable<Slider> Sliders(SoundPartTile tile) => tile.Placed.OfType<Slider>();

    private static IEnumerable<ListRow> Rows(SoundPartTile tile) => tile.Placed.OfType<ListRow>();

    /// <summary>Whether the card's last layout put its heading over its rows.</summary>
    private static bool Headed(SoundPartTile tile) =>
        tile.Placed.OfType<Label>().Any(label => label.Name == SoundPartTile.HeadingOf(tile.Region));

    /// <summary>The caption the card shows under its rows — the summary or the empty state — or null.</summary>
    private static string? Caption(SoundPartTile tile) =>
        tile.Placed.OfType<Label>().Where(label => label.Name != SoundPartTile.HeadingOf(tile.Region) && label.Text is { Length: > 0 } text && !int.TryParse(text, out _))
            .Select(label => label.Text).LastOrDefault();

    /// <summary>
    /// How near a card's edge its ink may come, in px. The rows keep the page's gutter of 8 and the
    /// ListRow's selection and hover plates start there, so 4 is room for antialiasing and no more.
    /// </summary>
    private const float Edge = 4;
}
