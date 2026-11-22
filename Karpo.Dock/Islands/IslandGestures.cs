// What a taskbar widget does with the pointer besides its click, where it is not a slider: Windows
// 11's sound icon steps the volume under the wheel and mutes on a middle click. A slider takes the
// wheel through ISliderIsland; these are for a widget with no track to drag.
//
// The wheel is still gated by the entry's declaration (Interaction.Wheel, BarEntries.TakesWheel):
// the bar asks a widget only where its entry says the wheel is its own, so the rest of the bar
// keeps the wheel for whatever it did before. A folder's entry says so for its face, and only the
// sound face (the sound preset's, which the sound button became) takes it (FolderIsland.Wheel).

namespace Karpo.Dock;

/// <summary>A widget the wheel works on without a slider: the sound icon steps the volume.</summary>
internal interface IWheelIsland
{
    /// <summary>A wheel turn of <paramref name="delta"/> (120 a notch) over it; true when it used it.</summary>
    bool Wheel(int delta);
}

/// <summary>A widget whose middle click is its own rather than its click: the sound icon mutes.</summary>
internal interface IMiddleClickIsland
{
    /// <summary>A middle click on cell <paramref name="index"/>; true when it used it, false to take it as a click.</summary>
    bool MiddleClick(int index);
}

/// <summary>
/// A widget on the bar with a button of its own inside its cell, which a click there presses instead
/// of the widget's click: the next meeting's Join on its folder's face (docs\widgets.md §205).
/// </summary>
internal interface IPointClickIsland
{
    /// <summary>
    /// A click at <paramref name="point"/> in cell <paramref name="index"/>, <paramref name="size"/> px
    /// large, on a bar drawn in <paramref name="style"/>; true when a button of its own took it, false to
    /// take it as the widget's click.
    /// </summary>
    bool ClickAt(int index, System.Numerics.Vector2 point, System.Numerics.Vector2 size, in IslandStyle style);
}
