// menus: a row of a menu that holds a control and acts without closing the menu (the person,
// 2026-10-12: "the additional menu options that appear when i click a widget in the folder should be
// moved to the context menu, the toggle switch and the height settings. when you change the height it
// should also not close the context menu").
//
// Two controls, each at the row's end where a shortcut would stand, the row's words before it:
//
//  - **A toggle switch** (FlyoutControl.Toggle), Windows 11's: 40 × 20, a pill ringed in the
//    secondary text with its knob on the left when off, filled with the accent and its knob on the
//    right when on. A click anywhere on the row, Enter or Space flips it.
//  - **A radio button** (FlyoutControl.Radio), Windows 11's: a 20 DIP ring in the secondary text,
//    or filled with the accent round a dot of the text on it when on; one of several rows that pick
//    one of a few, as a line's height mode (§168). A click anywhere on the row, Enter or Space picks it.
//  - **A stepper** (FlyoutControl.Stepper): "− N +", two compact buttons of a control's fill and rim,
//    a button greyed where it can't step, the value between, in one slot as wide as the widest text of its
//    range (Stepper.Range, §456), so neither button moves as it steps. A press on a button steps it at once and
//    again while held (Flyout.Pickers.cs), Left and Right, or − and + on the row step it; a click
//    elsewhere on the row does nothing — but where it takes a number typed too (a bar's size, §333)
//    opens its level, its chevron after the stepper, as Enter and Space do. A switch (AM/PM) is one button of its value alone, which a
//    press on it, a click on its row, Enter, Space or the arrows flip.
//  - **Tools** (FlyoutControl.Tools): small icon buttons of no fill, a plate under the pointer as a
//    command bar's, each its own command and its tooltip its name and its shortcut, which the row
//    takes while it is lit; a grip among them is no button but the handle the row is dragged by, and
//    stands at the row's start, before its icon (Flyout.Reorder.cs, §226). A click elsewhere on the
//    row is the row's: its submenu, where it has one (a bar's layout's too, ticked or not, §336), or the
//    pick of a choice's row (a note, §317). A
//    tool with a level of its own (FlyoutTool.Level, a note's rename) opens it in place as a row's
//    submenu opens (Flyout.Inline.cs). §452: a colour's tool is a swatch of what it is set to
//    (FlyoutTool.Swatch); and tools may stand before a toggle switch (Tools.Switch, an account's
//    calendar's colour), which a click elsewhere on the row, Enter or Space flips.
//  - **Segments** (FlyoutControl.Segments, §253): a choice of a few, each an icon, picked in the row
//    itself rather than in a submenu — Windows 11's segmented control (the Community Toolkit's
//    Segmented, as quick settings' power mode has it): one track of ControlAltFill in the control
//    stroke, the one chosen raised on it in a control's fill and stroke with the accent's short pill
//    under its icon, the others lit under the pointer. Its tooltip names each. A click on one picks it;
//    with the row lit, Left and Right go along them (mirrored in a mirrored menu), lighting the one
//    they are on, and Enter or Space picks it. A click elsewhere on the row does nothing.
//
// Either way the menu stays open and says what was asked (FlyoutEventKind.Adjust); whoever showed
// it changes what the rows stand for and hands it the rows as they are now (Refresh), which it shows
// in place, the pointer's and the keys' row kept. A control may stand on a level too (a folder face's
// switches, §214), and a Refresh shows the level again in place, its lit row kept (Flyout.Inline.cs). A
// row that stays (FlyoutItem.Stays, MenuKeeps.cs) acts the same way (§219).

using System.Numerics;
using Karpo.Core;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using Windows.UI.Composition;
using static TerraFX.Interop.Windows.Windows;
using Color = Windows.UI.Color;

namespace Karpo.Dock;

/// <summary>menus: a control a row holds at its end, acting without closing the menu (Flyout.Controls.cs).</summary>
public abstract record FlyoutControl
{
    /// <summary>A toggle switch, <paramref name="On"/> or off: the row's own id flips it.</summary>
    public sealed record Toggle(bool On) : FlyoutControl
    {
        /// <summary>§528: several widgets selected, some on and some off: neither, as a check box's third state (the settings panel draws it so).</summary>
        public bool Mixed { get; init; }
    }

    /// <summary>A radio button, <paramref name="On"/> or off: the row's own id picks it.</summary>
    public sealed record Radio(bool On) : FlyoutControl;

    /// <summary>
    /// A "− N +" stepper showing <paramref name="Value"/>: <paramref name="Fewer"/> and
    /// <paramref name="More"/> are the ids its buttons send, each greyed where it can't step
    /// (<paramref name="CanFewer"/>, <paramref name="CanMore"/>); <paramref name="FewerName"/> and
    /// <paramref name="MoreName"/> what a screen reader calls them. A <paramref name="Switch"/> is a
    /// button of its value alone that sends <paramref name="Fewer"/> (AM/PM).
    /// </summary>
    public sealed record Stepper(string Value, uint Fewer, uint More, bool CanFewer, bool CanMore, string FewerName, string MoreName, bool Switch = false) : FlyoutControl
    {
        /// <summary>
        /// §456: every text its value may say over its range ("2 rows", "2½ rows", … "20 rows"; a switch's both); its
        /// value's slot is as wide as the widest of them and its value, so neither button moves as it steps. Empty for its value alone.
        /// </summary>
        public IReadOnlyList<string> Range { get; init; } = [];

        public bool Equals(Stepper? other) =>
            other is not null && (Value, Fewer, More, CanFewer, CanMore, FewerName, MoreName, Switch) == (other.Value, other.Fewer, other.More, other.CanFewer, other.CanMore, other.FewerName, other.MoreName, other.Switch)
            && Range.SequenceEqual(other.Range);

        public override int GetHashCode() => HashCode.Combine(Value, Fewer, More, Switch, Range.Count);

        /// <summary>
        /// §456: the texts of every whole number from <paramref name="least"/> to <paramref name="most"/>, and on to
        /// <paramref name="now"/> where it stands outside them (a kind's default past its most), each as <paramref name="say"/>
        /// says it, once each: a stepper's <see cref="Range"/>. Every one, not a step apart: a number typed off its steps
        /// steps from there (a bar's size), and figures are not all as wide.
        /// </summary>
        public static IReadOnlyList<string> Over(int now, int least, int most, Func<int, string> say)
        {
            var (from, to) = (Math.Min(now, least), Math.Max(now, most));
            return [.. Enumerable.Range(from, to - from + 1).Select(say).Distinct(StringComparer.Ordinal)];
        }
    }

    /// <summary>Small icon buttons at the row's end, in their order (<see cref="FlyoutTool"/>).</summary>
    public sealed record Tools(IReadOnlyList<FlyoutTool> Buttons) : FlyoutControl
    {
        /// <summary>§452: a toggle switch after them, on or off, the row's own id flipping it as a toggle's row does; null for none.</summary>
        public bool? Switch { get; init; }

        public bool Equals(Tools? other) => other is not null && Switch == other.Switch && Buttons.SequenceEqual(other.Buttons);

        public override int GetHashCode() => Buttons.Count;
    }

    /// <summary>
    /// §253: a choice of a few in the row itself, each of <paramref name="Buttons"/> an icon that picks
    /// it (its id), <paramref name="Chosen"/> the one in force (-1 for none); see Flyout.Controls.cs.
    /// </summary>
    public sealed record Segments(IReadOnlyList<FlyoutTool> Buttons, int Chosen) : FlyoutControl
    {
        public bool Equals(Segments? other) => other is not null && Chosen == other.Chosen && Buttons.SequenceEqual(other.Buttons);

        public override int GetHashCode() => HashCode.Combine(Buttons.Count, Chosen);
    }
}

/// <summary>
/// menus: one of a row's <see cref="FlyoutControl.Tools"/>: a Segoe Fluent <paramref name="Glyph"/>
/// that sends <paramref name="Id"/>, greyed where not <paramref name="Enabled"/>, named
/// <paramref name="Name"/> in its tooltip with its <paramref name="Keys"/>, which the row takes while lit.
/// A <paramref name="Grip"/> is the six dots of a drag handle (the settings app's Gripper) and sends nothing.
/// One with a <paramref name="Level"/> sends nothing either: it opens that level in place, under its name,
/// as a row's submenu opens (§317: a note's rename, its name's field).
/// </summary>
public sealed record FlyoutTool(uint Id, string Glyph, string Name, bool Enabled = true, MenuChord? Keys = null, bool Grip = false, IReadOnlyList<FlyoutItem>? Level = null)
{
    /// <summary>§452: a swatch of this colour in its glyph's place, what a colour is set to (a holiday calendar's, an account's calendar's); null for its glyph.</summary>
    public Argb? Swatch { get; init; }

    /// <summary>What its tooltip says: its name, and its shortcut as Explorer's say theirs.</summary>
    public string Tip => Keys is { } keys ? keys.Tip(Name) : Name;
}

public sealed unsafe partial class Flyout
{
    /// <summary>menus: a toggle switch's size, in DIP (Windows 11's).</summary>
    internal const float ToggleWidthDip = 40;
    internal const float ToggleHeightDip = 20;

    /// <summary>menus: a radio button's ring and the dot in it when on, in DIP (Windows 11's).</summary>
    internal const float RadioDip = 20;
    internal const float RadioDotDip = 10;

    /// <summary>menus: a stepper's button, square, and the least room for its value, in DIP.</summary>
    internal const float StepDip = 24;
    internal const float StepValueDip = 28;

    /// <summary>The least room either side of a stepper's words between its buttons, where they are wider than <see cref="StepValueDip"/>: "Automatic", "70%" (§290).</summary>
    internal const float StepGapDip = 6;

    /// <summary>menus: a tool's button, square, its glyph, and the room between two, in DIP.</summary>
    internal const float ToolDip = 24;
    internal const float ToolGlyphDip = 14;
    internal const float ToolApartDip = 2;

    /// <summary>§452: a swatch tool's swatch (a colour's), square, in DIP: a row's own swatch's (Flyout.Pickers.cs); the room between a row's tools and its switch.</summary>
    internal const float ToolSwatchDip = 16;
    internal const float SwitchApartDip = 8;

    /// <summary>menus: a segment's width and the track's height, the plate's inset in it, the accent's pill, in DIP (§253).</summary>
    internal const float SegmentDip = 32;
    internal const float SegmentHeightDip = 24;
    internal const float SegmentInsetDip = 2;
    internal const float SegmentPillDip = 12;
    internal const float SegmentGlyphDip = 12;

    private static readonly string FewerGlyph = char.ConvertFromUtf32(0xE738);
    private static readonly string MoreGlyph = char.ConvertFromUtf32(0xE710);

    // What the menu was last shown with, for a Refresh in place: its items, layout, title and place.
    private (IReadOnlyList<FlyoutItem> Items, FlyoutLayout Layout, string? Title, FlyoutPlacement Placement)? shownWith;

    /// <summary>The items the menu shows now, as they were handed to it: what a Refresh changes.</summary>
    internal IReadOnlyList<FlyoutItem> ShownItems => shownWith?.Items ?? [];

    /// <summary>Tests only: the menu's window and panel on the screen, then each open submenu's, deepest last.</summary>
    internal IReadOnlyList<(HWND Window, RECT Panel)> OpenPanels
    {
        get
        {
            List<(HWND, RECT)> panels = [(Hwnd, host.PanelOnScreen)];
            for (var menu = submenu; menu is { IsOpen: true }; menu = menu.submenu)
            {
                panels.Add((menu.Hwnd, menu.host.PanelOnScreen));
            }
            return panels;
        }
    }

    /// <summary>Tests only: the id of the row lit in the open submenu of a menu that cascades; null where none is open or none is lit.</summary>
    internal uint? HotIdInSubmenu => submenu is { IsOpen: true } open ? open.HotId : null;

    /// <summary>Tests only: the id of the row lit now, by the pointer or the keys; null for none.</summary>
    internal uint? HotId => hot >= 0 && hot < rows.Count ? rows[hot].Item.Id : null;

    /// <summary>Tests only: the middle of the stepper's button on row <paramref name="id"/> — its "+" where <paramref name="more"/> — in window px; null where there is none.</summary>
    internal POINT? StepperButton(uint id, bool more)
    {
        if (!IsOpen || rows.FirstOrDefault(row => row.Item.Id == id) is not { Item.Control: FlyoutControl.Stepper } found)
        {
            return null;
        }
        var box = more ? found.More : found.Fewer;
        return new POINT((int)MathF.Round(viewport.X + box.X + (box.W / 2)), (int)MathF.Round(viewport.Y + box.Y + (box.H / 2) - scroll));
    }

    /// <summary>How wide <paramref name="control"/> stands at a row's end, in px at <paramref name="scale"/>.</summary>
    internal static float ControlWidth(FlyoutControl control, float scale, float valueWidth) => control switch
    {
        FlyoutControl.Toggle => MathF.Round(ToggleWidthDip * scale),
        FlyoutControl.Radio => MathF.Round(RadioDip * scale),
        FlyoutControl.Stepper { Switch: true } => SwitchWidth(scale, valueWidth),
        FlyoutControl.Stepper => (2 * MathF.Round(StepDip * scale)) + StepRoom(scale, valueWidth),
        FlyoutControl.Tools tools => ToolsWidth(tools, scale) + SwitchRoom(tools, scale),
        FlyoutControl.Segments segments => segments.Buttons.Count * MathF.Round(SegmentDip * scale),
        _ => 0,
    };

    // A switch's button: its words and 10 DIP either side, 40 DIP at the least.
    private static float SwitchWidth(float scale, float valueWidth) => MathF.Max(MathF.Round(40 * scale), valueWidth + (2 * MathF.Round(10 * scale)));

    // A stepper's room for its value between its buttons: its words and a gap either side, StepValueDip at the least.
    private static float StepRoom(float scale, float valueWidth) => MathF.Max(MathF.Round(StepValueDip * scale), valueWidth + (2 * MathF.Round(StepGapDip * scale)));

    // §456: the widths of a stepper's texts, measured once at their size.
    private readonly Dictionary<string, float> stepWidths = new(StringComparer.Ordinal);
    private float stepWidthsPx;

    /// <summary>
    /// §456: how wide <paramref name="stepper"/>'s value stands, in px at <paramref name="px"/>: the widest of its
    /// value and every text of its range (<see cref="FlyoutControl.Stepper.Range"/>), one width while it steps.
    /// </summary>
    private float StepValueWidth(Gfx with, FlyoutControl.Stepper stepper, float px)
    {
        if (px != stepWidthsPx || stepWidths.Count > 4096)
        {
            stepWidths.Clear();
            stepWidthsPx = px;
        }
        float Width(string text)
        {
            if (!stepWidths.TryGetValue(text, out var width))
            {
                width = MathF.Ceiling(with.Measure(text, px, Weight.Semibold).X);
                stepWidths[text] = width;
            }
            return width;
        }
        var widest = Width(stepper.Value);
        foreach (var text in stepper.Range)
        {
            widest = MathF.Max(widest, Width(text));
        }
        return widest;
    }

    // A row's tools that stand at its end: every one but a grip, which stands at its start.
    private static int Buttons(FlyoutControl.Tools tools) => tools.Buttons.Count(tool => !tool.Grip);

    // The tools at a row's end side by side, a switch's room after them aside.
    private static float ToolsWidth(FlyoutControl.Tools tools, float scale) =>
        Buttons(tools) is var count and > 0 ? (count * MathF.Round(ToolDip * scale)) + ((count - 1) * MathF.Round(ToolApartDip * scale)) : 0;

    // §452: the switch after a row's tools and the room between them; none for none.
    private static float SwitchRoom(FlyoutControl.Tools tools, float scale) =>
        tools.Switch is null ? 0 : MathF.Round(ToggleWidthDip * scale) + (Buttons(tools) > 0 ? MathF.Round(SwitchApartDip * scale) : 0);

    /// <summary>menus: whether <paramref name="item"/> has a grip at its start (a <see cref="FlyoutTool.Grip"/>): its icon and words a column further in.</summary>
    internal static bool Leads(FlyoutItem item) => item.Control is FlyoutControl.Tools tools && tools.Buttons.Any(tool => tool.Grip);

    /// <summary>menus: whether anything of <paramref name="control"/> stands at the row's end: not where it is only a grip.</summary>
    internal static bool AtEnd(FlyoutControl control) => control is not FlyoutControl.Tools tools || Buttons(tools) > 0 || tools.Switch is not null;

    /// <summary>
    /// menus: whether <paramref name="item"/>'s control stands at the row's very end in a menu whose
    /// other rows keep a column for their ticks: a toggle, a radio button or a stepper of a row with no
    /// tick and no submenu, where it stands in every other menu (§226: it stood a tick's room in).
    /// </summary>
    internal static bool ClearsCheckSlot(FlyoutItem item) =>
        item.Checked != true && item.Children is null && item.Control is FlyoutControl.Toggle or FlyoutControl.Radio or FlyoutControl.Stepper or FlyoutControl.Segments or FlyoutControl.Tools { Switch: not null };

    /// <summary>menus: scrolled to <paramref name="value"/> as far as the rows go (a Refresh's, §219).</summary>
    private void ScrollQuietly(float value)
    {
        var next = MathF.Round(Math.Clamp(value, 0, MathF.Max(0, contentHeight - viewport.H)));
        if (next != scroll)
        {
            scroll = next;
            PlaceContent();
        }
    }

    /// <summary>
    /// What the menu was shown with, kept for <see cref="Refresh"/>: a root menu's, not beside an item,
    /// nor a level swapped in place, whose tree it keeps (Flyout.Inline.cs, §268).
    /// </summary>
    private void Remember(IReadOnlyList<FlyoutItem> items, FlyoutLayout layout, string? title, FlyoutPlacement placement, bool beside)
    {
        if (parent is null && !beside && !swapping)
        {
            shownWith = (items, layout, title, placement);
        }
    }

    /// <summary>
    /// A click on row <paramref name="index"/>'s control at <paramref name="x"/>, <paramref name="y"/>
    /// (window px): a toggle flips, a stepper's button steps; the menu stays.
    /// </summary>
    private FlyoutEvent ControlClicked(int index, int x, int y)
    {
        var row = rows[index];
        switch (row.Item.Control)
        {
            case FlyoutControl.Toggle or FlyoutControl.Radio:
                return new FlyoutEvent(FlyoutEventKind.Adjust, row.Item.Id);
            case FlyoutControl.Tools tools:
                // §452: off its tools a row with a switch flips it, as a toggle's row does.
                if (tools.Switch is not null && ToolAt(index, x, y) is null)
                {
                    return new FlyoutEvent(FlyoutEventKind.Adjust, row.Item.Id);
                }
                if (ToolAt(index, x, y) is not { Enabled: true, Grip: false } tool)
                {
                    // §317: off its tools, a choice's row (a note of a line's) is picked as any choice is.
                    return row.Item is { Checked: not null, Stays: true } choice && ToolAt(index, x, y) is null ? Picked(choice) : default;
                }
                if (tool.Level is not null)
                {
                    OpenToolLevel(tool, keys: false);
                    return default;
                }
                return new FlyoutEvent(FlyoutEventKind.Adjust, tool.Id);
            // A segment picks its choice; the one in force is picked already (§253).
            case FlyoutControl.Segments segments:
                return ToolIndexAt(index, x, y) is var at and >= 0 && at != segments.Chosen && segments.Buttons[at].Enabled ? new FlyoutEvent(FlyoutEventKind.Adjust, segments.Buttons[at].Id) : default;
            // A switch (AM/PM) flips from anywhere on its row, as a toggle's row does; a stepper's buttons
            // step as they go down (StepperPress), and a click elsewhere on its row does nothing.
            case FlyoutControl.Stepper { Switch: true } flip:
                return new FlyoutEvent(FlyoutEventKind.Adjust, flip.Fewer);
            default:
                return default;
        }
    }

    /// <summary>
    /// A key on the lit row where it holds a control: Enter and Space flip a toggle or pick a radio button; Left, Right, −
    /// and + step a stepper (Enter and Space on it do nothing, or open its level where it has one, §333). Null for any other key or row.
    /// </summary>
    private FlyoutEvent? ControlKey(int key)
    {
        if (hot < 0 || hot >= rows.Count)
        {
            return null;
        }
        // Ctrl+Up and Ctrl+Down move a row that is dragged into order among its own (Flyout.Reorder.cs).
        if (ReorderKey(key) is { } moved)
        {
            return moved;
        }
        var control = KeyState.Down(VK.VK_CONTROL);
        var alt = KeyState.Down(VK.VK_MENU);
        // §523: a part of a colour's picker has the arrows, Page Up and Page Down, Home and End, Enter and Space (Flyout.Mixer.cs).
        if (rows[hot] is { Item.Control: FlyoutMix mix } mixRow)
        {
            return MixKey(mixRow, mix, key);
        }
        switch (rows[hot].Item.Control, key)
        {
            // A tool's shortcut, while its row is lit; Enter and Space are the row's, its submenu where it has one.
            case (FlyoutControl.Tools tools, _) when tools.Buttons.FirstOrDefault(tool => !tool.Grip && tool.Keys is { } keys && keys.Is(key, control, alt, KeyState.Down(VK.VK_SHIFT))) is { } tool:
                if (tool.Enabled && tool.Level is not null)
                {
                    OpenToolLevel(tool, keys: true);
                    return new FlyoutEvent(FlyoutEventKind.Nothing);
                }
                return tool.Enabled ? new FlyoutEvent(FlyoutEventKind.Adjust, tool.Id) : new FlyoutEvent(FlyoutEventKind.Nothing);
            // §452: a row's switch after its tools flips by Enter and Space, as a toggle's row does.
            case (FlyoutControl.Tools { Switch: not null }, VK.VK_RETURN or VK.VK_SPACE) when rows[hot].Item.Children is null:
                return new FlyoutEvent(FlyoutEventKind.Adjust, rows[hot].Item.Id);
            // §317: a choice's row with tools (a note of a line's) is picked by Enter and Space as any choice is.
            case (FlyoutControl.Tools, VK.VK_RETURN or VK.VK_SPACE) when rows[hot].Item.Children is null:
                return rows[hot].Item is { Checked: not null, Stays: true } choice ? Picked(choice) : new FlyoutEvent(FlyoutEventKind.Nothing);
            case (FlyoutControl.Toggle or FlyoutControl.Radio, VK.VK_RETURN or VK.VK_SPACE):
                return new FlyoutEvent(FlyoutEventKind.Adjust, rows[hot].Item.Id);
            // A switch (AM/PM) flips from any of its keys.
            case (FlyoutControl.Stepper { Switch: true } flip, VK.VK_RETURN or VK.VK_SPACE or VK.VK_LEFT or VK.VK_RIGHT or VK.VK_SUBTRACT or VK.VK_OEM_MINUS or VK.VK_ADD or VK.VK_OEM_PLUS):
                return new FlyoutEvent(FlyoutEventKind.Adjust, flip.Fewer);
            // §333: one with a level of its own (a number typed: a bar's size) opens it, as any row's submenu.
            case (FlyoutControl.Stepper, VK.VK_RETURN or VK.VK_SPACE):
                return rows[hot].Item.Children is null ? new FlyoutEvent(FlyoutEventKind.Nothing) : null;
            case (FlyoutControl.Stepper stepper, VK.VK_LEFT or VK.VK_SUBTRACT or VK.VK_OEM_MINUS):
                return stepper.CanFewer ? new FlyoutEvent(FlyoutEventKind.Adjust, stepper.Fewer) : new FlyoutEvent(FlyoutEventKind.Nothing);
            case (FlyoutControl.Stepper stepper, VK.VK_RIGHT or VK.VK_ADD or VK.VK_OEM_PLUS):
                return stepper.CanMore ? new FlyoutEvent(FlyoutEventKind.Adjust, stepper.More) : new FlyoutEvent(FlyoutEventKind.Nothing);
            // Segments (§253): Left and Right go along them as they stand, Enter and Space pick the one lit.
            case (FlyoutControl.Segments segments, VK.VK_LEFT or VK.VK_RIGHT):
                keyedSegment = SegmentStep(KeyedSegment(segments), segments.Buttons.Count, key == VK.VK_RIGHT, mirrored);
                SetHotTool(hot, keyedSegment);
                return new FlyoutEvent(FlyoutEventKind.Nothing);
            case (FlyoutControl.Segments segments, VK.VK_RETURN or VK.VK_SPACE):
                return KeyedSegment(segments) is var at and >= 0 && at != segments.Chosen && segments.Buttons[at].Enabled
                    ? new FlyoutEvent(FlyoutEventKind.Adjust, segments.Buttons[at].Id)
                    : new FlyoutEvent(FlyoutEventKind.Nothing);
            default:
                return null;
        }
    }

    /// <summary>menus: the segment the keys are on in the lit row (§253); -1 for the one in force, until Left or Right moves them.</summary>
    private int keyedSegment = -1;

    /// <summary>menus: which of <paramref name="segments"/> the keys are on: the one they moved to, else the one in force, else the first.</summary>
    private int KeyedSegment(FlyoutControl.Segments segments) =>
        keyedSegment >= 0 && keyedSegment < segments.Buttons.Count ? keyedSegment : Math.Max(0, Math.Min(segments.Chosen, segments.Buttons.Count - 1));

    /// <summary>
    /// menus: the segment Left or Right (<paramref name="right"/>) goes to from <paramref name="from"/>
    /// of <paramref name="count"/>: the next one the way the key points, the first where they stand
    /// mirrored (<paramref name="mirrored"/>) on the right; at either end it stays.
    /// </summary>
    internal static int SegmentStep(int from, int count, bool right, bool mirrored) =>
        Math.Clamp(from + (right != mirrored ? 1 : -1), 0, Math.Max(0, count - 1));

    /// <summary>
    /// <paramref name="control"/> drawn into <paramref name="into"/> at the end of <paramref name="row"/>,
    /// its right edge at <paramref name="right"/> (content px), upright in the middle of the row's fill;
    /// a stepper's buttons kept on the row for the pointer. Returns its width.
    /// </summary>
    private float DrawControl(Compositor c, Func<IDisposable, IDisposable> keep, ContainerVisual into, Row row, FlyoutControl control, float right, float textPx)
    {
        T Keep<T>(T value)
            where T : IDisposable => (T)keep(value);
        float Px(float dip) => MathF.Round(dip * scale);
        var hairline = Px(1);
        var fill = row.Bounds;
        float Middle(float size) => fill.Y + MathF.Round((fill.H - size) / 2);
        ShapeVisual Shape(Vector2 at, Vector2 size, float radius, Color? body, Color? rim)
        {
            var visual = Keep(c.CreateShapeVisual());
            visual.Size = size;
            visual.Offset = new Vector3(at, 0);
            var box = Keep(c.CreateRoundedRectangleGeometry());
            box.Offset = new Vector2(hairline / 2);
            box.Size = size - new Vector2(hairline);
            box.CornerRadius = new Vector2(radius);
            var shape = Keep(c.CreateSpriteShape(box));
            if (body is { } inside)
            {
                shape.FillBrush = Keep(c.CreateColorBrush(inside));
            }
            if (rim is { } edge)
            {
                shape.StrokeBrush = Keep(c.CreateColorBrush(edge));
                shape.StrokeThickness = hairline;
            }
            visual.Shapes.Add(shape);
            into.Children.InsertAtTop(visual);
            row.Parts.Add((visual, visual.Offset));
            return visual;
        }
        SpriteVisual Sprite(CompositionDrawingSurface surface, Vector2 size, Vector2 at)
        {
            var sprite = Keep(c.CreateSpriteVisual());
            sprite.Brush = Keep(c.CreateSurfaceBrush(surface));
            sprite.Size = size;
            sprite.Offset = new Vector3(at, 0);
            into.Children.InsertAtTop(sprite);
            row.Parts.Add((sprite, sprite.Offset));
            return sprite;
        }
        // A toggle switch, its right edge at `end`: its width.
        float Switch(bool on, float end)
        {
            var size = new Vector2(Px(ToggleWidthDip), Px(ToggleHeightDip));
            var at = new Vector2(end - size.X, Middle(size.Y));
            var knob = Px(12);
            var secondary = Colour(theme.TextSecondary);
            // §544: off, ToggleSwitchFillOff (ControlAltFillColorSecondary) in ToggleSwitchStrokeOff (ControlStrongStrokeColorDefault).
            _ = Shape(at, size, size.Y / 2, on ? theme.Accent : theme.ControlAltFill, on ? null : theme.ControlStrongStroke);
            var knobAt = new Vector2(on ? at.X + size.X - Px(4) - knob : at.X + Px(4), at.Y + MathF.Round((size.Y - knob) / 2));
            var dot = Keep(c.CreateShapeVisual());
            dot.Size = new Vector2(knob);
            dot.Offset = new Vector3(knobAt, 0);
            var round = Keep(c.CreateEllipseGeometry());
            round.Center = new Vector2(knob / 2);
            round.Radius = new Vector2(knob / 2);
            var disc = Keep(c.CreateSpriteShape(round));
            disc.FillBrush = Keep(c.CreateColorBrush(on ? Colour(theme.TextOnAccent) : secondary));
            dot.Shapes.Add(disc);
            into.Children.InsertAtTop(dot);
            row.Parts.Add((dot, dot.Offset));
            return size.X;
        }
        switch (control)
        {
            case FlyoutControl.Toggle toggle:
                return Switch(toggle.On, right);
            case FlyoutControl.Radio radio:
            {
                var side = Px(RadioDip);
                var at = new Vector2(right - side, Middle(side));
                // §544: off, RadioButtonOuterEllipseFill (ControlAltFillColorSecondary) in its stroke (ControlStrongStrokeColorDefault).
                _ = Shape(at, new Vector2(side), side / 2, radio.On ? theme.Accent : theme.ControlAltFill, radio.On ? null : theme.ControlStrongStroke);
                if (radio.On)
                {
                    var dotSide = Px(RadioDotDip);
                    var dot = Keep(c.CreateShapeVisual());
                    dot.Size = new Vector2(dotSide);
                    dot.Offset = new Vector3(at.X + MathF.Round((side - dotSide) / 2), at.Y + MathF.Round((side - dotSide) / 2), 0);
                    var round = Keep(c.CreateEllipseGeometry());
                    round.Center = new Vector2(dotSide / 2);
                    round.Radius = new Vector2(dotSide / 2);
                    var disc = Keep(c.CreateSpriteShape(round));
                    disc.FillBrush = Keep(c.CreateColorBrush(Colour(theme.TextOnAccent)));
                    dot.Shapes.Add(disc);
                    into.Children.InsertAtTop(dot);
                    row.Parts.Add((dot, dot.Offset));
                }
                return side;
            }
            case FlyoutControl.Stepper { Switch: true } flip:
            {
                // AM/PM: a button of its words alone, a control's fill and rim, its ends 4 DIP round.
                var value = gfx!.Text(flip.Value, textPx, Weight.Semibold, theme.Text, Px(80));
                _ = keep(value.Surface);
                var side = Px(StepDip);
                var wide = SwitchWidth(scale, StepValueWidth(gfx!, flip, textPx));
                var at = new Vector2(right - wide, Middle(side));
                _ = Shape(at, new Vector2(wide, side), Px(4), theme.ControlFill, theme.ControlStroke);
                _ = Sprite(value.Surface, value.Size, new Vector2(at.X + MathF.Round((wide - value.Size.X) / 2), Middle(value.Size.Y)));
                row.Fewer = new Area(at.X, at.Y, wide, side);
                row.More = default;
                return wide;
            }
            case FlyoutControl.Stepper stepper:
            {
                var value = gfx!.Text(stepper.Value, textPx, Weight.Semibold, theme.Text, Px(80));
                _ = keep(value.Surface);
                var side = Px(StepDip);
                // §456: one room for its whole range, so neither button moves as it steps.
                var room = StepRoom(scale, StepValueWidth(gfx!, stepper, textPx));
                var top = Middle(side);
                var more = new Vector2(right - side, top);
                var number = new Vector2(more.X - room, top);
                var fewer = new Vector2(number.X - side, top);
                Button(fewer, FewerGlyph, stepper.CanFewer);
                Button(more, MoreGlyph, stepper.CanMore);
                _ = Sprite(value.Surface, value.Size, new Vector2(number.X + MathF.Round((room - value.Size.X) / 2), Middle(value.Size.Y)));
                row.Fewer = new Area(fewer.X, fewer.Y, side, side);
                row.More = new Area(more.X, more.Y, side, side);
                return right - fewer.X;

                void Button(Vector2 at, string glyph, bool enabled)
                {
                    _ = Shape(at, new Vector2(side), Px(4), theme.ControlFill, theme.ControlStroke);
                    var mark = Keep(gfx!.Glyph(glyph, 12 * scale, enabled ? theme.Text : theme.TextDisabled, side));
                    _ = Sprite(mark, new Vector2(side), at);
                }
            }
            case FlyoutControl.Tools tools:
            {
                // §452: a switch at the row's very end, the tools before it.
                if (tools.Switch is { } on)
                {
                    _ = Switch(on, right);
                }
                // From the row's end back, the first tool furthest in, as their order reads.
                var side = Px(ToolDip);
                var apart = Px(ToolApartDip);
                var top = Middle(side);
                var left = right - ControlWidth(tools, scale, 0);
                var at = 0;
                foreach (var tool in tools.Buttons.Where(tool => !tool.Grip))
                {
                    var box = new Vector2(left + (at++ * (side + apart)), top);
                    // The plate under the pointer, a command bar button's; out of sight until then.
                    var plate = Shape(box, new Vector2(side), Px(4), theme.SubtlePressed, null);
                    plate.Opacity = 0f;
                    if (tool.Swatch is { } colour)
                    {
                        // §452: what a colour is set to, as a row's own swatch is drawn (Flyout.Pickers.cs).
                        var swatch = Px(ToolSwatchDip);
                        var inset = MathF.Round((side - swatch) / 2);
                        var drawn = Shape(box + new Vector2(inset), new Vector2(swatch), Px(3), Ink(colour), theme.ControlStroke);
                        drawn.Opacity = tool.Enabled ? 1f : 0.4f;
                    }
                    else
                    {
                        var mark = Keep(gfx!.Glyph(tool.Glyph, ToolGlyphDip * scale, tool.Enabled ? theme.Text : theme.TextDisabled, side));
                        _ = Sprite(mark, new Vector2(side), box);
                    }
                    row.Tools.Add((new Area(box.X, box.Y, side, side), tool, plate));
                }
                return right - left;
            }
            case FlyoutControl.Segments segments:
            {
                // The track, then each segment from its start: a plate lit under the pointer or the
                // keys, the one in force raised over it with the accent's pill, its icon on top.
                var wide = Px(SegmentDip);
                var tall = Px(SegmentHeightDip);
                var inset = Px(SegmentInsetDip);
                var top = Middle(tall);
                var left = right - ControlWidth(segments, scale, 0);
                var radius = Px(4);
                _ = Shape(new Vector2(left, top), new Vector2(right - left, tall), radius, theme.ControlAltFill, theme.ControlStroke);
                var thick = MathF.Max(2f, Px(2));
                var pill = Px(SegmentPillDip);
                var plateSize = new Vector2(wide - (2 * inset), tall - (2 * inset));
                var glyphSide = Px(SegmentGlyphDip);
                // The icons stand in the room over the pill, every segment's alike.
                var glyphTop = top + inset + MathF.Round((plateSize.Y - thick - Px(1) - glyphSide) / 2);
                for (var at = 0; at < segments.Buttons.Count; at++)
                {
                    var segment = segments.Buttons[at];
                    var box = new Vector2(left + (at * wide), top);
                    var plateAt = box + new Vector2(inset);
                    var plate = Shape(plateAt, plateSize, Px(3), theme.SubtleHover, null);
                    plate.Opacity = 0f;
                    var chosen = at == segments.Chosen;
                    if (chosen)
                    {
                        _ = Shape(plateAt, plateSize, Px(3), theme.ControlFill, theme.ControlStroke);
                        _ = Shape(new Vector2(box.X + MathF.Round((wide - pill) / 2), plateAt.Y + plateSize.Y - thick - Px(1)), new Vector2(pill, thick), thick / 2, segment.Enabled ? theme.Accent : Colour(theme.TextDisabled), null);
                    }
                    var mark = Keep(gfx!.Glyph(segment.Glyph, SegmentGlyphDip * scale, segment.Enabled ? theme.Text : theme.TextDisabled, glyphSide));
                    _ = Sprite(mark, new Vector2(glyphSide), new Vector2(box.X + MathF.Round((wide - glyphSide) / 2), glyphTop));
                    row.Tools.Add((new Area(box.X, box.Y, wide, tall), segment, plate));
                }
                return right - left;
            }
            default:
                return 0;
        }
    }

    /// <summary>
    /// The grips of <paramref name="tools"/> drawn into <paramref name="into"/> at the start of
    /// <paramref name="row"/>, the first's box from <paramref name="left"/> (content px), upright in the
    /// middle of its fill: the settings app's Gripper, two columns of three dots, 3 DIP each, 7 and 6.5
    /// DIP apart, in the secondary text. Each is one of the row's tools, for its tooltip.
    /// </summary>
    private void DrawGrips(Compositor c, Func<IDisposable, IDisposable> keep, ContainerVisual into, Row row, FlyoutControl.Tools tools, float left)
    {
        T Keep<T>(T value)
            where T : IDisposable => (T)keep(value);
        float Px(float dip) => MathF.Round(dip * scale);
        var side = Px(ToolDip);
        var fill = row.Bounds;
        var top = fill.Y + MathF.Round((fill.H - side) / 2);
        var dot = MathF.Max(2f, Px(3));
        var across = Px(7);
        var down = MathF.Round(6.5f * scale);
        foreach (var tool in tools.Buttons.Where(tool => tool.Grip))
        {
            var ink = Colour(tool.Enabled ? theme.TextSecondary : theme.TextDisabled);
            var x0 = left + MathF.Round((side - across - dot) / 2);
            var y0 = top + MathF.Round((side - (2 * down) - dot) / 2);
            for (var column = 0; column < 2; column++)
            {
                for (var line = 0; line < 3; line++)
                {
                    var visual = Keep(c.CreateShapeVisual());
                    visual.Size = new Vector2(dot);
                    visual.Offset = new Vector3(x0 + (column * across), y0 + (line * down), 0);
                    var round = Keep(c.CreateEllipseGeometry());
                    round.Center = new Vector2(dot / 2);
                    round.Radius = new Vector2(dot / 2);
                    var disc = Keep(c.CreateSpriteShape(round));
                    disc.FillBrush = Keep(c.CreateColorBrush(ink));
                    visual.Shapes.Add(disc);
                    into.Children.InsertAtTop(visual);
                    row.Parts.Add((visual, visual.Offset));
                }
            }
            // No plate: a grip is no button; its box is its tooltip's.
            var plate = Keep(c.CreateShapeVisual());
            plate.Opacity = 0f;
            row.Tools.Add((new Area(left, top, side, side), tool, plate));
            left += side + Px(ToolApartDip);
        }
    }

    /// <summary>The tool of row <paramref name="index"/> at <paramref name="x"/>, <paramref name="y"/> (window px); null for none.</summary>
    private FlyoutTool? ToolAt(int index, int x, int y) => ToolIndexAt(index, x, y) is var at and >= 0 ? rows[index].Tools[at].Tool : null;

    /// <summary>Which of row <paramref name="index"/>'s tools is at <paramref name="x"/>, <paramref name="y"/> (window px); -1 for none.</summary>
    private int ToolIndexAt(int index, int x, int y)
    {
        if (index < 0 || index >= rows.Count)
        {
            return -1;
        }
        var cx = x - viewport.X;
        var cy = y - viewport.Y + scroll;
        return rows[index].Tools.FindIndex(tool => tool.Box.Contains(cx, cy));
    }

    /// <summary>The row and the tool of it the pointer is on, whose plate shows; -1 for none.</summary>
    private (int Row, int Tool) hotTool = (-1, -1);

    /// <summary>
    /// The pointer is on tool <paramref name="tool"/> of row <paramref name="index"/> (-1 for none):
    /// its plate shows, and its tooltip after the hover time, or at once where another stands.
    /// </summary>
    private void SetHotTool(int index, int tool)
    {
        if (hotTool == (index, tool))
        {
            return;
        }
        if (hotTool.Row >= 0 && hotTool.Row < rows.Count && hotTool.Tool < rows[hotTool.Row].Tools.Count && hotTool.Tool >= 0)
        {
            var old = rows[hotTool.Row].Tools[hotTool.Tool].Plate;
            Try(() => old.Opacity = 0f);
        }
        hotTool = (index, tool);
        if (index >= 0 && index < rows.Count && tool >= 0 && tool < rows[index].Tools.Count)
        {
            var (_, button, plate) = rows[index].Tools[tool];
            if (button.Enabled && !button.Grip)
            {
                Try(() => plate.Opacity = 1f);
            }
            ToolTipFor();
            return;
        }
        if (hot < 0 || hot >= rows.Count || !rows[hot].Item.Primary)
        {
            HideTip();
        }
    }

    private static Color Colour(DXGI_RGBA ink) =>
        Color.FromArgb((byte)MathF.Round(ink.a * 255), (byte)MathF.Round(ink.r * 255), (byte)MathF.Round(ink.g * 255), (byte)MathF.Round(ink.b * 255));
}
