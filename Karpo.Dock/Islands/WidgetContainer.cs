// What widgets drawn together agree about (docs\widgets.md §9, "Channels"; Karpo.Core\Channels.cs).
//
// A month calendar and an agenda in one folder agree on "Thursday" without knowing about each
// other: the **container** holds the folder's channels and the widgets read and publish through
// this seam. The widgets stay leaves — neither knows the other exists, or that it is in a folder at
// all; which channels each publishes and follows is its list record's (ListWidget.Publishes,
// ListWidget.Follows).
//
// Four rules, and they are the whole of it:
//
//  * **A widget never needs a container.** Reading without one answers "nobody has published", and
//    publishing without one does nothing. A month on its own picks today, which is what it would
//    show anyway; that is what lets the same widget stand in a folder, in a preview, or nowhere.
//  * **The channel carries the type.** A Channel<DateOnly> can only be read as a DateOnly, so two
//    widgets cannot disagree about whether a value is a day or an hour.
//  * **The channels belong to the registry, not to the container** (Channels.All), so the container
//    knows nothing about days, and a new pairing adds a channel without touching this file.
//  * **The container redraws what the container changed.** A widget that publishes does not ask for
//    a redraw: the container draws what is in it, so it is what knows when to draw again.
//
//    A widget with pointer feedback of its own — a month lighting the day under the pointer —
//    changes nothing shared and must still be drawn again; that is the folder's job at the moment it
//    tells the widget where the pointer is (FolderFlyout.HoverOver draws the card it just told).
//
// Transient, never config: the values live in the container and die with its showing. What resets
// them is the container — a folder clears them when it closes — and a widget may not clear anybody
// else's.

using Karpo.Core;

namespace Karpo.Dock;

/// <summary>
/// What a widget can ask of whatever it is drawn in. Implemented by the folder
/// (<see cref="FolderIsland"/>); null for a widget drawn anywhere else, which is why every widget
/// asks through <see cref="CellIsland.Followed{T}"/> rather than holding one of these.
/// </summary>
internal interface IWidgetContainer
{
    /// <summary>What <paramref name="channel"/> holds, or null when nobody has published to it.</summary>
    T? Read<T>(Channel<T> channel)
        where T : struct;

    /// <summary>
    /// Publishes <paramref name="value"/>, and draws again what the container holds, because the
    /// container is what draws it. Publishing what is already there changes nothing and draws nothing.
    /// </summary>
    void Publish<T>(Channel<T> channel, T value)
        where T : struct;

    /// <summary>
    /// Takes back what <paramref name="channel"/> holds, so it reads as unset again — an app let go
    /// of, the pointer gone from an app — and draws again where that changed anything.
    /// </summary>
    void Withdraw(Channel channel);

    /// <summary>
    /// notes: a widget changed by the person's own hand (a task ticked, a letter typed) is drawn again
    /// now, and laid out again where its height changed — not a post away, which a key typed cannot
    /// wait for (<see cref="CellIsland.Redrawn"/>).
    /// </summary>
    void Redraw();
}
