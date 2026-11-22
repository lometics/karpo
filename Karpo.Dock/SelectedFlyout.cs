// edit: the selected widget's own flyout, standing open while it is the one selected (the person,
// 2026-10-09: "when clicking on a widget it should actually also open its flyout when it is selected
// as individual widget", and of the clock: "it should open the settings but it should also open the
// flyout. the flyout should basically be kept open as long as the widget is selected"). The rule,
// apart from any window, so it can be tested; the dock acts on it (Dock.FlyoutPreview.cs), opening
// the flyout as the settings app's look at a height does: no focus taken, standing through presses
// elsewhere and the settings app having the keys (FlyoutHost.Previewing).
//
//  - One widget selected, on a bar or a place, whose kind has a flyout of Karpo's own: its flyout
//    stands open, from where it stands.
//  - Another selected alone instead: that one's flyout, or none where it has none.
//  - Several selected, none, one carried, or edit mode over: none.
//  - Never one of Windows' own panels (Start, Search, task view...) nor an act (a pin, a switch that
//    flips): those are no flyout of Karpo's. Nor the kinds in Never, whose flyout is wrong to open
//    for a selection: a folder, which a click opens arranged instead (§253) — and so
//    the notifications preset, whose folder is what the bell's list was (§100): a folder opened
//    arranged marks no notification seen, as the bell's list never opened for a selection, because
//    its opening marks every notification seen; the tray, whose click is one of its icons', which
//    acts.

using Karpo.Core;

namespace Karpo.Dock;

/// <summary>Whose flyout stands open for the selection in edit mode (see the top of the file).</summary>
public static class SelectedFlyout
{
    /// <summary>The kinds with a flyout of Karpo's own that a selection never opens.</summary>
    public static IReadOnlyCollection<BarKind> Never { get; } = [BarKind.FolderList, BarKind.Tray];

    /// <summary>
    /// Whether a widget of <paramref name="kind"/> opens its flyout when it is the one selected:
    /// <paramref name="own"/>, it has a flyout of Karpo's own (the dock's table, or a list of choices
    /// of its own), and it is none of <see cref="Never"/>.
    /// </summary>
    public static bool Opens(BarKind kind, bool own) => own && !Never.Contains(kind);

    /// <summary>
    /// The key of the widget whose flyout stands open: the one of <paramref name="selected"/>, alone,
    /// where <paramref name="opens"/> says it opens one, while edit mode is on
    /// (<paramref name="editing"/>) and nothing is carried (<paramref name="carrying"/>); else none.
    /// </summary>
    public static string? For(bool editing, bool carrying, IReadOnlyList<string> selected, Func<string, bool> opens) =>
        editing && !carrying && selected.Count == 1 && opens(selected[0]) ? selected[0] : null;
}
