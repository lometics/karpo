// folders: the pressed look of a row on a line whose rows light themselves (CellIsland.LightsItsRows).
//
// The folder keeps such a line as it is at rest under the pointer and pressed, so a row that lights
// under the pointer has to light pressed itself, as a Windows 11 list item does: the subtle pressed
// fill while the button is down on it. The line does not take the pointer for that — the press stays
// the folder's, which makes it the click it always was — so it is told of the press (CardPress, which
// answers false) and never of the release: the click that follows lets it go (Click), and so does the
// pointer leaving the row or the folder closing (CardHover, Visible).

namespace Karpo.Dock;

/// <summary>Which of a line's targets a press is down on: 0 for none.</summary>
internal struct RowPress
{
    private int held;

    /// <summary>The target the press is down on; 0 for none.</summary>
    public readonly int Held => held;

    /// <summary>Whether target <paramref name="id"/> wears the pressed fill.</summary>
    public readonly bool On(int id) => held != 0 && held == id;

    /// <summary>A press went down on target <paramref name="id"/> (0: nothing to press): true where what is lit changed.</summary>
    public bool Down(int id)
    {
        var was = held;
        held = id;
        return was != id;
    }

    /// <summary>The pointer is over target <paramref name="id"/> now: a press moved off its row lets it go. True where that changed anything.</summary>
    public bool Over(int id)
    {
        if (held == 0 || held == id)
        {
            return false;
        }
        held = 0;
        return true;
    }

    /// <summary>The click came, or the showing ended: nothing is pressed. True where something was.</summary>
    public bool Up()
    {
        var was = held;
        held = 0;
        return was != 0;
    }
}
