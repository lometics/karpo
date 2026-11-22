// tray: what Karpo's tray does when its window is no longer the first Shell_TrayWnd (Tray.EnsureFirst).
//
// Apps find the tray by the first window of that class in z-order. Explorer's hidden taskbar steps in
// front of Karpo's whenever it lays itself out again — an icon it takes in or lets go, its taskbar
// shown —, and the tray takes the first place back. It used to ask every app for its icons again each
// time as well (TaskbarCreated, which Explorer says only when its taskbar was made again), and apps
// answer that by taking icons away and adding them again: Explorer's taskbar laid itself out for those,
// stepped in front, and was asked for again, for ever. Steam takes its icon away the moment it is asked
// and adds it again about three seconds later, which kept the loop going every three seconds and its
// icon on the bar half of the time (docs/widgets.md §273).
//
// So the place is taken back without a word, and the apps are asked once more only by the first look
// after they were asked: their answers may have gone to Explorer alone while its taskbar stood in front
// (the takeover hides it just after the start's asking). Never twice for one asking, so the asking
// can't feed itself.

namespace Karpo.Core;

/// <summary>What a look at the tray's place comes to (<see cref="TrayFirst.Look"/>).</summary>
public enum TrayLook
{
    /// <summary>The tray's window is first: nothing to do.</summary>
    Nothing,

    /// <summary>Put first again, the apps not asked.</summary>
    Raise,

    /// <summary>Put first again and every app asked for its icons once more (TaskbarCreated).</summary>
    AskAgain,
}

/// <summary>Whether the apps may still be answering an asking; pure.</summary>
public sealed class TrayFirst
{
    private bool answering;

    /// <summary>Every app was just asked for its icons (Karpo's start, Explorer's taskbar made again, a new icon size).</summary>
    public void Asked() => answering = true;

    /// <summary>
    /// What a look finds to do, <paramref name="first"/> whether the tray's window is first. Only the
    /// first look after an asking may ask again; any look ends the answering.
    /// </summary>
    public TrayLook Look(bool first)
    {
        var asked = answering;
        answering = false;
        return first ? TrayLook.Nothing : asked ? TrayLook.AskAgain : TrayLook.Raise;
    }
}
