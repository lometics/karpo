// narrator: what an open folder tells a screen reader (Accessibility\Uia.cs, §471). The folder is a pane of
// its lines, in the order the keys walk them; a line is a group named by its widget, and its stops (LineFocus)
// are its buttons — each named by its tooltip where it has one, else by what it says (its pieces, ILinePieces),
// else by its line. A line the keys stop at whole (one that walks its own keys, or has no targets) is one
// button itself. The stop the keys are on is the focused element, and UI Automation is told as they move, so
// Narrator follows a folder's keys as it follows the bar's and a menu's. Pressed by a screen reader, a stop is
// what Enter on it is (posted as the key, so it goes the keys' one way); focused, the keys go onto it.
//
// Not while the folder is arranged: edit mode's keys walk the lines whole, and the tree is the lines alone.

using System.Numerics;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

public sealed unsafe partial class FolderFlyout
{
    // narrator: an element's id is its line's place plus one, shifted, and its stop's place plus one under it (0: the
    // line whole) — the same for the same stop while the folder shows, and within UI Automation's 24 bits.
    private const int LineShift = 12;
    private const int StopMask = (1 << LineShift) - 1;

    /// <summary>narrator: the element id of stop <paramref name="stop"/> (-1: the line whole) of line <paramref name="line"/>.</summary>
    private static int UiaId(int line, int stop) => ((line + 1) << LineShift) | (stop + 1);

    /// <summary>narrator: the folder's elements come from it (see the top of the file); its window's, as it shows.</summary>
    private void RegisterUia() => Uia.Register(host.Hwnd, new FolderAccess(this));

    /// <summary>narrator: the keys moved: a screen reader listening is told where they are now.</summary>
    private void UiaFocusMoved()
    {
        if (keyboard && host.Hwnd != HWND.NULL)
        {
            Uia.FocusMoved(host.Hwnd);
        }
    }

    /// <summary>Tests: the tree a screen reader is given now.</summary>
    internal UiaTree UiaTreeNow() => new FolderAccess(this).Build();

    /// <summary>
    /// narrator: what each of <paramref name="stops"/> of line <paramref name="index"/> is called: its tooltip, else
    /// what the line says inside it, else the line's widget.
    /// </summary>
    private List<string> StopNames(int index, List<FocusPart> stops)
    {
        var line = (CellIsland)tiles[index].Island;
        var names = stops.Select(_ => line.Info.Title).ToList();
        if (StopSpace(index) is not { } at)
        {
            return names;
        }
        try
        {
            var laid = line.Scrolled(at.Box, at.Style, WidgetSurface.List);
            var pieces = line is ILinePieces pieced ? pieced.LinePieces(laid.Scrolls ? laid.Still : laid.Box, at.Style) : [];
            for (var stop = 0; stop < stops.Count; stop++)
            {
                var part = stops[stop];
                if (part.Target == 0)
                {
                    continue;
                }
                var shown = LineFocus.Shown(at.Line, part, at.Box, at.Style, WidgetSurface.List);
                var middle = new Vector2((shown.left + shown.right) / 2, (shown.top + shown.bottom) / 2);
                if (line.TipOn(middle, at.Box, at.Style, WidgetSurface.List) is { Text.Length: > 0 } tip)
                {
                    names[stop] = tip.Text;
                    continue;
                }
                var said = pieces
                    .Where(piece => piece.Text.Trim().Length > 0 && LineRows.Inside(part.Box, new Vector2((piece.Box.left + piece.Box.right) / 2, (piece.Box.top + piece.Box.bottom) / 2)))
                    .Select(piece => piece.Text.Trim())
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
                if (said.Count > 0)
                {
                    names[stop] = string.Join(", ", said);
                }
            }
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Console.Error.WriteLine($"Karpo: a folder's {line.Info.Id} stop names failed: {error.Message}");
        }
        return names;
    }

    /// <summary>narrator: the folder's tree, and what a screen reader asks of its stops (see the top of the file).</summary>
    private sealed class FolderAccess(FolderFlyout flyout) : IUiaSource
    {
        public UiaTree Build()
        {
            if (!flyout.IsOpen || flyout.plan is not { } shown)
            {
                return UiaTree.Empty("Karpo", UiaRole.Pane);
            }
            var lines = new List<UiaNode>();
            var focusedId = -1;
            foreach (var index in flyout.Order().Where(index => index >= 0 && index < flyout.tiles.Count && index < shown.Cards.Count))
            {
                var island = flyout.tiles[index].Island;
                var card = flyout.CardOnScreen(shown.Cards[index]);
                var stops = flyout.StopsOf(index);
                if (stops.Count == 0)
                {
                    lines.Add(new UiaNode(UiaId(index, -1), island.Info.Title, UiaRole.Button, card) { Pattern = UiaPattern.Invoke, Focusable = true });
                    focusedId = index == flyout.focused ? UiaId(index, -1) : focusedId;
                    continue;
                }
                var buttons = new List<UiaNode>();
                var names = flyout.StopNames(index, stops);
                for (var stop = 0; stop < stops.Count; stop++)
                {
                    buttons.Add(new UiaNode(UiaId(index, stop), names[stop], UiaRole.Button, flyout.CardOnScreen(flyout.PartOnPanel(index, stops[stop])))
                    {
                        Pattern = UiaPattern.Invoke,
                        Focusable = true,
                    });
                    if (index == flyout.focused && flyout.KeyPart?.Target == stops[stop].Target)
                    {
                        focusedId = UiaId(index, stop);
                    }
                }
                lines.Add(new UiaNode(UiaId(index, -1), island.Info.Title, UiaRole.Group, card) { Children = buttons });
            }
            var name = flyout.folder?.Info.Title is { Length: > 0 } title ? title : "Karpo";
            return new UiaTree(new UiaNode(0, name, UiaRole.Pane, flyout.host.PanelOnScreen) { Children = lines, AutomationId = "Karpo.Folder" }, focusedId);
        }

        public void Act(UiaAct act, int id, double value)
        {
            var (index, stop) = ((id >> LineShift) - 1, (id & StopMask) - 1);
            if (!flyout.IsOpen || flyout.editing || index < 0 || index >= flyout.tiles.Count || act is not (UiaAct.Invoke or UiaAct.Focus))
            {
                return;
            }
            var stops = flyout.StopsOf(index);
            flyout.KeysTo(index, stop >= 0 && stop < stops.Count ? stops[stop] : null);
            flyout.keyboard = true;
            flyout.ShowStates();
            flyout.UiaFocusMoved();
            if (act == UiaAct.Invoke)
            {
                // Pressed as the keys press it: Enter, down and up, through the folder's own way.
                _ = PostMessageW(flyout.host.Hwnd, WM.WM_KEYDOWN, VK.VK_RETURN, 0);
                _ = PostMessageW(flyout.host.Hwnd, WM.WM_KEYUP, VK.VK_RETURN, unchecked((nint)0xC0000001));
            }
        }
    }
}
