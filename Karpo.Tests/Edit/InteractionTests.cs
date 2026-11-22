// pins: the registries' answer to "does this widget consume drags" (Interaction, on each
// taskbar entry and list line), held against what the widgets actually do.
//
// Why this file exists, and why it asserts in both directions. Every widget can
// be dragged to a new place outside edit mode, so before the bar starts a move it
// asks the widget whether the drag is the widget's own. That used to be
// `is ISliderIsland` — an answer a kind gave by *omission*, which fails in two
// ways and is silent in both:
//
//   * A widget that declares the drag and cannot take one: the press falls
//     through to the move, so the widget is movable and its drag never happens.
//     Annoying, findable.
//   * A widget that takes drags but whose declaration forgot to say so: the bar
//     never asks, so the drag becomes a move. **That is the volume slider you
//     cannot set**, and nothing anywhere says so — not a crash, not a log, not a
//     failing test. It is the reason the second direction below is not optional.
//
// The field is `required`, so a widget added later cannot leave the question
// unanswered; these hold that the answer given is the true one. Each registry
// answers for where it stands — the taskbar asks its entry, a folder asks the
// line it made (CellIsland.Interaction) — so each is held against
// the widget its own factory makes, in one table.

using System.Runtime.InteropServices;
using Karpo.Dock;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
using IDispatcherQueueController = TerraFX.Interop.WinRT.IDispatcherQueueController;

namespace Karpo.Tests.Edit;

public sealed unsafe class InteractionTests
{
    /// <summary>
    /// What a taskbar entry or a line declares, and what the widget its factory makes can do.
    /// <see cref="Name"/> says which: "taskbar Volume", "line SoundMixer".
    /// </summary>
    private sealed record Answer(string Name, bool Declares, bool DeclaresWheel, bool Takes, bool TakesWheel, string Type);

    /// <summary>
    /// Whether a widget takes a drag of its own. Two ways, because a widget is drawn on two kinds
    /// of surface: <c>ISliderIsland</c> is the bar's (the volume slider's track), and overriding
    /// <c>CellIsland.CardPress</c> is a list line's. Either one is a widget that must be asked before
    /// the bar turns the press into a move.
    /// </summary>
    private static bool TakesDrag(object? widget) =>
        widget is ISliderIsland || Overrides(widget, "CardPress");

    /// <summary>
    /// Whether a widget takes the wheel of its own: the slider steps the level, the sound icon
    /// steps the volume with no slider (<c>IWheelIsland</c>), and a line that works with the wheel
    /// overrides <c>CellIsland.CardWheel</c> — the countdown's steppers and the sound panel's
    /// sliders, whose folder must not scroll instead of them.
    /// </summary>
    private static bool TakesWheel(object? widget) =>
        widget is ISliderIsland or IWheelIsland || Overrides(widget, "CardWheel");

    /// <summary>Whether <paramref name="widget"/>'s type overrides <paramref name="method"/> rather than inheriting CellIsland's.</summary>
    private static bool Overrides(object? widget, string method)
    {
        if (widget is null)
        {
            return false;
        }
        var found = widget.GetType().GetMethod(
            method,
            System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.Public
            | System.Reflection.BindingFlags.NonPublic);
        return found is not null && found.DeclaringType != typeof(CellIsland);
    }

    /// <summary>
    /// The widgets whose drag is not an island's, with where the dock consults each one. The tray
    /// is drawn by the scene and has no island at all, so no interface can speak for it — which is
    /// the argument for the declaration being a field on the entry. One added here without a
    /// consumer fails the test below, and the message says what is missing.
    /// </summary>
    private static readonly Dictionary<string, string> ConsultedByHand = new()
    {
        ["taskbar Tray"] = "Dock.Tray.cs (TrayGrabbed), reached through Dock.Pins.cs (TakesDragItself)",
    };

    [Fact]
    public void EveryWidgetThatDeclaresADragHasSomethingThatTakesOne()
    {
        var wrong = Answers()
            .Where(answer => answer.Declares && !answer.Takes && !ConsultedByHand.ContainsKey(answer.Name))
            .Select(answer => answer.Name)
            .ToArray();

        Assert.True(
            wrong.Length == 0,
            $"these declare Interaction.Drag and nothing takes the drag: their widget is no "
            + $"ISliderIsland and the dock consults them nowhere by hand, so a press on them falls "
            + $"through to a move and their own drag never happens. Either wire a consumer and name "
            + $"it in {nameof(ConsultedByHand)}, or say Interaction.Click: {string.Join(", ", wrong)}");
    }

    /// <summary>A widget named as consulted by hand must still declare the drag, or nobody asks it.</summary>
    [Fact]
    public void EveryWidgetConsultedByHandDeclaresItsDrag()
    {
        var answers = Answers();
        foreach (var (name, where) in ConsultedByHand)
        {
            Assert.True(
                answers.Single(answer => answer.Name == name).Declares,
                $"{name}'s drag is taken in {where}, but it does not say Interaction.Drag, so the gate "
                + $"in Dock.Pins.cs never opens and the drag moves the widget instead.");
        }
    }

    /// <summary>
    /// The loud one. A widget that takes drags and whose declaration never said so is never asked,
    /// so its drag becomes a move of the widget — the volume slider nobody can set, reported by
    /// nothing.
    /// <para>
    /// Asked of the <b>drawing class</b> rather than of each widget, because one class serves
    /// several, and several places: <c>SoundPartTile</c> draws the output, the input, the mixer
    /// and the volume row, and only two of them have sliders. So its override cannot be pinned on
    /// any one of them — which is the argument for the declaration being a field all over again —
    /// and what must be true is that somebody it draws for has declared the gesture. A class whose
    /// drag nobody declared still fails, which is the tooth this test is for.
    /// </para>
    /// </summary>
    [Fact]
    public void EveryWidgetThatTakesADragHasADeclarationThatSaysSo()
    {
        var quiet = Silent(answer => answer.Takes, answer => answer.Declares);

        Assert.True(
            quiet.Length == 0,
            $"these drawing classes take drags of their own and nothing they draw for says so, so "
            + $"the bar never asks and the drag moves the widget instead: say Interaction.Drag in "
            + $"the entry or the line: {string.Join("; ", quiet)}");
    }

    /// <summary>
    /// A wheel turn is taken by the same widgets and through the same interface, so a widget that
    /// claims the wheel must be one that can take it.
    /// </summary>
    [Fact]
    public void AWidgetThatClaimsTheWheelTakesIt()
    {
        var wrong = Answers()
            .Where(answer => answer.DeclaresWheel && !answer.TakesWheel)
            .Select(answer => answer.Name)
            .ToArray();

        Assert.True(wrong.Length == 0, $"these claim the wheel and make no widget that takes it: {string.Join(", ", wrong)}");
    }

    /// <summary>
    /// The wheel's own loud direction, grouped the same way and for the same reason: a line that
    /// works with the wheel and which nothing declared is never asked, so the wheel scrolls the
    /// folder under it and the line looks stuck.
    /// </summary>
    [Fact]
    public void EveryWidgetThatTakesTheWheelHasADeclarationThatSaysSo()
    {
        var quiet = Silent(answer => answer.TakesWheel, answer => answer.DeclaresWheel);

        Assert.True(
            quiet.Length == 0,
            $"these drawing classes take the wheel and nothing they draw for says so, so the "
            + $"surface under them scrolls instead: say Interaction.Wheel in the entry or the "
            + $"line:{string.Join("; ", quiet)}");
    }

    /// <summary>
    /// The drawing classes where <paramref name="takes"/> holds for some widget they draw and
    /// <paramref name="declared"/> holds for none of them, named with those widgets.
    /// </summary>
    private static string[] Silent(Func<Answer, bool> takes, Func<Answer, bool> declared) =>
        [.. Answers()
            .Where(takes)
            .GroupBy(answer => answer.Type)
            .Where(group => !group.Any(declared))
            .Select(group => $"{group.Key} (drawn for {string.Join(", ", group.Select(answer => answer.Name))})")];

    /// <summary>
    /// The tray is the widget that makes the case for a field rather than an interface: it takes
    /// drags, it is drawn by the scene and it has no island, so nothing about its type could ever
    /// have said so. Before this it was covered by the kind list in PinPressed, which is gone.
    /// </summary>
    [Fact]
    public void TheTrayTakesDragsAndHasNoIslandToSaySo()
    {
        var tray = Answers().Single(answer => answer.Name == "taskbar Tray");

        Assert.True(tray.Declares, "the tray's entry says Interaction.Drag");
        Assert.False(tray.Takes, "and it makes no island, let alone an ISliderIsland");
        Assert.Contains(tray.Name, ConsultedByHand.Keys);
    }

    /// <summary>
    /// The test would pass on an empty table if nothing were ever asked, which is the one way it
    /// could be green for no reason: at least one widget takes a drag, and it is the volume slider
    /// — on the taskbar, and as the sound panel's volume row down a list.
    /// </summary>
    [Fact]
    public void TheVolumeSliderIsAWidgetThatTakesADrag()
    {
        var answers = Answers();
        foreach (var name in new[] { "taskbar Volume", "line SoundVolume" })
        {
            var volume = answers.Single(answer => answer.Name == name);
            Assert.True(volume.Declares, $"{name} says Interaction.Drag");
            Assert.True(volume.Takes, $"and its widget takes one");
            Assert.True(volume.DeclaresWheel, $"and a wheel turn over {name} steps the level");
        }
    }

    /// <summary>
    /// A widget with no drag of its own answers Click, and that is what leaves a press free to
    /// become a move. Nearly every one is, so the feature is the rule and not the exception.
    /// <para>
    /// The few that are not are <b>named rather than counted</b>: one joins this list because
    /// something on it is worked with the pointer held down — the slider's track, the tray's icons
    /// being rearranged, the countdown's steppers repeating while they are held on its line, the
    /// sound panel's mixer and volume row, what's playing's timeline on its line —
    /// and that is a decision somebody made, not a number to keep in step.
    /// A count would be re-numbered by whoever broke it; a list has to be argued with.
    /// </para>
    /// </summary>
    [Fact]
    public void NearlyEveryWidgetLeavesAPressFreeToBecomeAMove()
    {
        var answers = Answers();

        // Both sides ordered the same way, so the list can be written in the order somebody would
        // say it rather than in the order the registries happen to be in.
        Assert.Equal(
            new[]
            {
                "taskbar Tray", "taskbar Volume", "line Timer", "line Focus", "line Media", "line SoundMixer", "line SoundVolume",
                "taskbar Brightness", "line Brightness",
                // notes: a task dragged into another place, a note's text selected by a drag.
                "line Todo", "line Note",
                // quick: a page's controls take their presses, a key's field a drag to select.
                "line Wifi", "line Bluetooth", "line Hotspot", "line PowerMode",
                "line WirelessDisplays", "line Projection", "line NearbySharing",
                // notifications: a reply box's words selected by a drag (§100).
                "line NotificationsList",
                // pim: the contacts' search box, its words selected by a drag (§102).
                "line Contacts",
                "line WinSearch",
                // desktops: a row dragged into another place by its gripper (§116).
                "line Desktops",
                // markets: a converter's two amounts' words selected by a drag (§246).
                "line Markets",
                // §641: a pinned app dragged into another place.
                "line StartPins",
            }.Order(),
            answers.Where(answer => answer.Declares).Select(answer => answer.Name).Order());
    }

    /// <summary>
    /// Every taskbar entry and list line: what each declares, and what the widget its
    /// own factory makes can do.
    /// </summary>
    private static List<Answer> Answers()
    {
        var answers = new List<Answer>();
        OnStaThread(gfx =>
        {
            var config = new Config();
            // Qualified: Karpo.Tests.Islands is a namespace of this project, and it shadows the
            // dock's Islands class from inside Karpo.Tests.
            foreach (var entry in BarEntries.All)
            {
                Add($"taskbar {entry.Kind}", () => entry.Kind.New(), entry.Interaction,
                    (key, widget) => Karpo.Dock.Islands.Create(key, (BarItem)widget, gfx, config));
            }
            foreach (var line in ListWidgets.All)
            {
                Add($"line {line.Kind}", () => line.Kind.New(), line.Interaction,
                    (key, widget) => Karpo.Dock.Islands.CreateLine(key, (CellItem)widget, line.Kind, gfx, config));
            }

            void Add(string name, Func<WidgetItem> fresh, Interaction declared, Func<string, WidgetItem, IIsland?> make)
            {
                var widget = fresh();
                widget.Id = "probe";
                var made = make(Config.IslandKey(null, "probe"), widget);
                answers.Add(new Answer(
                    name,
                    declared.HasFlag(Interaction.Drag),
                    declared.HasFlag(Interaction.Wheel),
                    TakesDrag(made),
                    TakesWheel(made),
                    made?.GetType().Name ?? "-"));
                made?.Dispose();
            }
        });
        Assert.NotEmpty(answers);
        return answers;
    }

    /// <summary>
    /// A Gfx needs a dispatcher queue on the thread that makes it, and xunit's threads have none —
    /// so the body runs on an STA thread of this test's own, as the dock's does. No window and no
    /// window station: the islands are made and asked what they are, never drawn.
    /// </summary>
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
                var made = CreateDispatcherQueueController(options, &controller);
                if (made.FAILED)
                {
                    throw new InvalidOperationException($"CreateDispatcherQueueController failed: 0x{made.Value:X8}");
                }
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
            throw new Xunit.Sdk.XunitException("On the island thread: " + failed);
        }
    }
}
