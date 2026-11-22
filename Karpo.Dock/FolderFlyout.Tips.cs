// A tooltip over a part of a folder's line: the toggles' buttons say their name and state, an
// actions line's icon button its page, a month's arrows which way they step — as Windows names an
// icon-only button with a tooltip. Opt-in per widget (CellIsland.CardTip): every other line has
// none, as before.
//
// It is the edit rail's kind of tip (EditRail.ShowTip): a tip kept (TipView, §359) that takes no
// pointer, its 12 px text on 9 by 6 of padding, standing above the part with a little air, moved
// from one part to the next in place. It comes after the pointer has rested a moment on the part
// (TipDelayMs, Windows' own hover time) and goes the moment the pointer leaves it or the folder closes.

using System.Numerics;
using Karpo.Core;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using Windows.UI.Composition;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

public sealed unsafe partial class FolderFlyout
{
    private const nuint TipTimer = 2;
    private const uint TipDelayMs = 400;
    private TipView? tip;

    // What the tip says and where its part is on the screen: wanted, and shown.
    private (string Text, RECT At)? tipWanted;
    private (string Text, RECT At)? tipShown;

    /// <summary>What the folder's tooltip says now, for a test: null while none stands.</summary>
    internal string? TipShown => tipShown?.Text;

    /// <summary>The tooltip's window, for a picture of it: none before the first.</summary>
    internal HWND TipWindow => tip?.Hwnd ?? HWND.NULL;

    /// <summary>
    /// The pointer rests at <paramref name="point"/> (panel px) over card <paramref name="target"/>:
    /// the part under it that has a tooltip (CellIsland.CardTip) has it after a moment, and words drawn
    /// cut short there say themselves whole (CutWords), on the tip's first line where the part has one
    /// of its own; anything else has none.
    /// </summary>
    private void TipOver(int target, Vector2? point)
    {
        // edit: a list's warning says what the list has no room for.
        var wanted = HiddenTip(target);
        if (wanted is null && point is { } at && plan is { } shown && target >= 0 && target < tiles.Count && target < shown.Cards.Count)
        {
            var (box, local, style) = CardSpace(shown, target, at);
            (string Text, D2D_RECT_F Part)? own = null;
            if (!editing && DrawsOwnCard(target) && tiles[target].Island is CellIsland cells)
            {
                try
                {
                    own = cells.TipOn(local, box, style, WidgetSurface.List);
                }
                catch (Exception error) when (error is not OutOfMemoryException)
                {
                    Console.Error.WriteLine($"Karpo: a folder's {cells.Info.Id} tooltip failed: {error.Message}");
                }
            }
            var cut = CutWords.At(cutRuns.GetValueOrDefault(tiles[target].Island), local);
            if (CutWords.Said(cut?.Text, own?.Text) is { } said)
            {
                // Where the part stands on a mirrored line (Mirror.cs).
                var under = own?.Part ?? cut!.Value.Box;
                var part = Mirror.On ? Mirror.Back(under, box.right - box.left, Upright(target)) : under;
                var on = host.PanelOnScreen;
                var card = shown.Cards[target];
                var left = on.left + card.left;
                var top = on.top + card.top;
                wanted = (said, new RECT
                {
                    left = (int)MathF.Floor(left + part.left),
                    top = (int)MathF.Floor(top + part.top),
                    right = (int)MathF.Ceiling(left + part.right),
                    bottom = (int)MathF.Ceiling(top + part.bottom),
                });
            }
        }
        WantTip(wanted);
    }

    /// <summary>Tests only: the whole words of what line <paramref name="index"/> drew cut short at <paramref name="point"/> (its box's px); null for none.</summary>
    internal string? CutAt(int index, Vector2 point) =>
        index >= 0 && index < tiles.Count ? CutWords.At(cutRuns.GetValueOrDefault(tiles[index].Island), point)?.Text : null;

    /// <summary>The tip <paramref name="wanted"/>, after the pointer rests a moment, or none; from one part's to the next at once.</summary>
    private void WantTip((string Text, RECT At)? wanted)
    {
        if (wanted == tipWanted)
        {
            return;
        }
        tipWanted = wanted;
        if (wanted is null)
        {
            HideTip();
            return;
        }
        if (tipShown is not null)
        {
            // From one part's tip to the next: at once, as Windows moves a tooltip along a toolbar.
            ShowTip();
            return;
        }
        _ = SetTimer(Hwnd, TipTimer, TipDelayMs, null);
    }

    /// <summary>
    /// edit: the tooltip of a list's warning button (<see cref="FolderAddKind.Hidden"/>), on the
    /// screen, as the edit bar's "not shown" button says it: the widget by name, or how many; null
    /// for any other target.
    /// </summary>
    private (string Text, RECT At)? HiddenTip(int target)
    {
        if (plan is not { } shown || target < AddTarget || target - AddTarget >= shown.Adds.Count
            || shown.Adds[target - AddTarget] is not { Kind: FolderAddKind.Hidden } warning
            || HiddenIn(shown, warning.List) is not { Count: > 0 } hidden)
        {
            return null;
        }
        var box = AddBox(shown, target - AddTarget);
        var on = host.PanelOnScreen;
        return (HiddenTitle(hidden), new RECT
        {
            left = (int)MathF.Floor(on.left + box.left),
            top = (int)MathF.Floor(on.top + box.top),
            right = (int)MathF.Ceiling(on.left + box.right),
            bottom = (int)MathF.Ceiling(on.top + box.bottom),
        });
    }

    /// <summary>The tip the pointer rested on, now: above its part, centred on it.</summary>
    private void ShowTip()
    {
        _ = KillTimer(Hwnd, TipTimer);
        if (!IsOpen || gfx is null || tipWanted is not { } wanted)
        {
            return;
        }
        tip ??= new TipView(gfx, tipInstance, tipDock);
        // Over the part as a flyout stands over a bottom bar: above it, and under it where there is
        // no room above (the host flips it).
        var at = wanted.At;
        Pop(tip, gfx, wanted.Text, new POINT { x = (at.left + at.right) / 2, y = at.top }, Edge.Bottom, TipView.GapDip);
        tipShown = wanted;
    }

    /// <summary>
    /// <paramref name="text"/> in the tip <paramref name="into"/>, standing off <paramref name="anchor"/>
    /// (screen px) as a flyout stands off a bar on <paramref name="edge"/>, <paramref name="gapDip"/> apart,
    /// inside the room the folder itself was placed in (§580: its display's work area less every bar in sight, its
    /// own too, FolderWork — not the bare work area, which holds a bar that shows over the windows). It takes no pointer.
    /// </summary>
    private void Pop(TipView into, Gfx drawer, string text, POINT anchor, Edge edge, float gapDip) =>
        into.Show(drawer, look, scale, text, anchor, host.Work, edge, gapDip);

    // --- The note -----------------------------------------------------------------

    // edit: the note's own tip — a refusal, said while the folder is arranged — and what it says now.
    private const nuint NoteTimer = 4;

    /// <summary>edit: how long a note the dock set stands, in ms: long enough to read a sentence.</summary>
    internal const uint NoteMs = 4000;

    private TipView? noteTip;
    private string noteShown = "";

    /// <summary>What the folder's note says now, for a test: empty while none stands.</summary>
    internal string NoteShown => noteShown;

    /// <summary>
    /// edit: the folder's note (<see cref="Say"/>) in a tip of its own over the folder, centred on
    /// it, on its side away from the bar and past the "+" buttons there; none at rest or for an
    /// empty note. Where the title row said it until the person took the names away (2026-10-10).
    /// </summary>
    private void ShowNote()
    {
        var wanted = IsOpen && Dressed && gfx is not null ? note : "";
        if (wanted == noteShown)
        {
            return;
        }
        noteShown = wanted;
        if (wanted.Length == 0)
        {
            noteTip?.Hide();
            return;
        }
        noteTip ??= new TipView(gfx!, tipInstance, tipDock);
        var on = host.PanelOnScreen;
        var middle = (on.left + on.right) / 2;
        var (anchor, edge) = shownEdge == Edge.Top
            ? (new POINT { x = middle, y = on.bottom }, Edge.Top)
            : (new POINT { x = middle, y = on.top }, Edge.Bottom);
        Pop(noteTip, gfx!, wanted, anchor, edge, OutsideRoomDip);
    }

    /// <summary>No tip: the pointer left its part, or the folder closes.</summary>
    private void HideTip()
    {
        if (Hwnd != HWND.NULL)
        {
            _ = KillTimer(Hwnd, TipTimer);
        }
        tipWanted = null;
        if (tipShown is null)
        {
            return;
        }
        tipShown = null;
        tip?.Hide();
    }
}
