// edit: a folder line's height, set from the line's own menu (the person, 2026-10-12: "the
// additional menu options that appear when i click a widget in the folder should be moved to the
// context menu, the toggle switch and the height settings. when you change the height it should also
// not close the context menu"). It replaces the strip the folder drew over or under the selected
// line the same day; a folder's own settings are in Settings alone.
//
//  - **When.** A right click on a folder's line whose kind has a height to set (For, Folders.HasHeight:
//    no heading, divider, list row or kind of one fixed height), arranged or at rest: the height is
//    the line's own, as its name is.
//  - **What** (Row): under the menu's other rows, ruled off and standing still at the menu's foot while
//    they scroll (FlyoutItem.Foot, OptionMenu.Foot; §276: "height should always be aligned at the bottom
//    and should also not scroll"), over "Appearance" with no rule between (§314), two rows (§411, the menu
//    rules: "height in the menu itself"; §240 to §408 had one row opening a level of radio buttons and the
//    stepper): "Height", a choice saying its mode at its end ("Automatic", "Fixed", "Fill") and opening them —
//    "Automatic" (only where the kind's content varies, ListWidget.Grows; §140), "Fixed" and "Fill" (§168) —,
//    a pick going back; and "Rows − 6 rows +", a stepper of its rows from one to as many as the open folder's
//    room holds on its screen (FolderFlyout.RoomRows; §192: no kind has a most of its own). The stepper is
//    what the rows are: "Max rows" under Automatic, which they cap ("when having automatic height the height
//    property that can still be set should be called max height"), "Rows" under Fixed. Under Fill there is none
//    (§660, the person, 2026-11-10: "when fill is selected as height there should not be a height property anymore
//    as it is ignored"): Fill takes the room its list has left, whatever the rows say; they are kept in the file,
//    and Automatic or Fixed picked again shows the stepper at them.
//  - **Changes apply at once** (Act): a mode picked, a step, saved as every change of the dock's is, the
//    line resizing behind the menu; the menu stays open and shows the height as it is now. §573: the same rows on the
//    line's page of the settings panel act the same way (Dock.LineHeightActed, from the menu and from the panel alike).
//  - **A sized line has no Height** (§432, the person, 2026-10-30: "for these widgets i would not like the user
//    to be able to change their height manually"): a kind of one form has nothing here at all; a kind of a few
//    has "Size" in the Height's place (SizeRows) — a choice saying its size at its end ("Compact", "Medium",
//    "Full") and opening the kind's sizes, a pick going back — and no rows to step: each size is its form's
//    whole rows (LineSize.cs).

using Karpo.Core;

namespace Karpo.Dock;

/// <summary>
/// A line's height as its menu offers it: <paramref name="Now"/>, whether it offers Automatic
/// (<paramref name="Modes"/>, <see cref="ListWidget.Grows"/>), and the fewest and most cells of its kind.
/// </summary>
public readonly record struct LineHeightChoice(ListKind Kind, LineHeight Now, bool Modes, int Least, int Most)
{
    /// <summary>The height once its cells are stepped by <paramref name="step"/> (a cell, half a row, a step), held to the kind's limits.</summary>
    public LineHeight Step(int step) => (Now with { Cells = Math.Clamp(Now.Cells + step, Least, Most) }).Within(Kind);

    /// <summary>The height once the mode is <paramref name="mode"/>: the same cells (Fixed, not Automatic, where the kind has no Automatic).</summary>
    public LineHeight Mode(LineHeightMode mode) => Now.As(mode == LineHeightMode.Automatic && !Modes ? LineHeightMode.Fixed : mode).Within(Kind);

    /// <summary>Whether half a row fewer is a height the kind allows; never under Fill, which has no cells.</summary>
    public bool CanFewer => !Now.Fill && Now.Cells > Least;

    /// <summary>Whether half a row more is a height the kind allows; never under Fill.</summary>
    public bool CanMore => !Now.Fill && Now.Cells < Most;
}

/// <summary>The rules of a folder line's height rows in its menu; see the top of the file.</summary>
public static class LineHeightRows
{
    /// <summary>The menu's ids of the rows: the three modes, the stepper's two buttons, and the row opening them.</summary>
    public const uint FixedId = 5870;
    public const uint FewerId = 5871;
    public const uint MoreId = 5872;
    public const uint AutomaticId = 5873;
    public const uint FillId = 5874;
    public const uint HeightId = 5875;

    /// <summary>§432: the ids of a sized line's "Size" row and of its three sizes.</summary>
    public const uint SizeId = 5876;
    public const uint CompactId = 5877;
    public const uint MediumId = 5878;
    public const uint FullId = 5879;

    /// <summary>Whether <paramref name="id"/> is one of the height rows' or the size rows'.</summary>
    public static bool Holds(uint id) => id is FixedId or FewerId or MoreId or AutomaticId or FillId or HeightId or SizeId or CompactId or MediumId or FullId;

    /// <summary>
    /// §432: the size <paramref name="item"/>'s line shows, where its kind offers a choice of sizes; null for any
    /// other — a kind of one form, a line whose content grows (it has a Height, <see cref="For"/>), a folder.
    /// </summary>
    public static LineSize? SizeOf(WidgetItem? item) =>
        item is CellItem && item.Info?.Id is { } id && ListWidgets.Find(id) is { } line && line.Kind.Chooses() ? LineSizes.Of(item) : null;

    /// <summary>
    /// §432: the "Size" row for <paramref name="item"/>, standing still at the menu's foot where "Height" stands
    /// for a line whose content grows: a choice saying its size at its end and opening its kind's sizes, a pick
    /// going back. None where the line offers no size (<see cref="SizeOf"/>).
    /// </summary>
    public static List<FlyoutItem> SizeRows(WidgetItem? item)
    {
        if (SizeOf(item) is not { } now || ListWidgets.Find(item!.Info?.Id) is not { } line)
        {
            return [];
        }
        List<FlyoutItem> sizes = [.. line.Kind.Offered().Select(size =>
            new FlyoutItem(IdOf(size), Said(size), new FlyoutIcon.Glyph(GlyphOf(size)), Checked: size == now, Stays: true))];
        return [new(SizeId, S.DockEditSizeTitle, new FlyoutIcon.Glyph(OptionGlyphs.Height), Children: sizes, Value: new FlyoutValue(Said(now)), Foot: true, PickOne: true) { Setting = SizeSetting }];
    }

    /// <summary>§432: the size row <paramref name="id"/> picks for <paramref name="item"/>; null where it picks none, or the one it has.</summary>
    public static LineSize? ActSize(WidgetItem? item, uint id)
    {
        LineSize? picked = id switch
        {
            CompactId => LineSize.Compact,
            MediumId => LineSize.Medium,
            FullId => LineSize.Full,
            _ => null,
        };
        return SizeOf(item) is { } now && picked is { } size && size != now && ListWidgets.Find(item!.Info?.Id) is { } line && line.Kind.Offered().Contains(size) ? size : null;
    }

    /// <summary>§432: gives <paramref name="item"/> the size <paramref name="size"/>; false where it offers none, or nothing changed.</summary>
    public static bool Resize(WidgetItem item, LineSize size)
    {
        if (SizeOf(item) is not { } now || now == size || ListWidgets.Find(item.Info?.Id) is not { } line || !line.Kind.Offered().Contains(size))
        {
            return false;
        }
        item.Size = size;
        return true;
    }

    /// <summary>What the "Size" row says a size is: "Compact", "Medium", "Full".</summary>
    public static string Said(LineSize size) => size switch
    {
        LineSize.Compact => S.DockEditSizeCompact,
        LineSize.Medium => S.DockEditSizeMedium,
        _ => S.DockEditSizeFull,
    };

    /// <summary>Each size its own glyph on its level (§411, R13): smaller, the size, larger.</summary>
    private static string GlyphOf(LineSize size) => size switch
    {
        LineSize.Compact => OptionGlyphs.Smaller,
        LineSize.Medium => OptionGlyphs.Size,
        _ => OptionGlyphs.Larger,
    };

    /// <summary>The menu's id of a size's row.</summary>
    private static uint IdOf(LineSize size) => size switch
    {
        LineSize.Compact => CompactId,
        LineSize.Medium => MediumId,
        _ => FullId,
    };

    /// <summary>
    /// The height <paramref name="item"/>'s line offers to set: only a line whose kind has a height (<see cref="Folders.HasHeight(ListKind)"/>);
    /// null for any other — a heading, a divider, a list row, a kind of one fixed height, a folder.
    /// Its cells go up to <paramref name="room"/>, the most the open folder's room holds on its screen
    /// (<see cref="FolderFlyout.RoomCells()"/>), and no further (§192).
    /// </summary>
    public static LineHeightChoice? For(WidgetItem? item, int room = LineHeight.Most)
    {
        if (item is null || item is BarItem { Folder: not null } || ListWidgets.Find(item.Info?.Id) is not { } line || !line.Kind.HasHeight())
        {
            return null;
        }
        var (least, most) = LineHeight.Limits(line.Kind);
        return new LineHeightChoice(line.Kind, LineHeight.For(item), line.Grows, least, Math.Clamp(room, least, most));
    }

    /// <summary>
    /// Gives <paramref name="item"/> the height <paramref name="height"/>, as its kind allows it
    /// (<see cref="LineHeight.Within(ListKind)"/>). False where it has no height, or nothing changed.
    /// </summary>
    public static bool Resize(WidgetItem item, LineHeight height)
    {
        if (For(item) is not { } choice)
        {
            return false;
        }
        var held = height.Within(choice.Kind);
        if (item.Height == held)
        {
            return false;
        }
        item.Height = held;
        return true;
    }

    /// <summary>
    /// The height row <paramref name="id"/> asks for: a mode picked, a step; null where it asks for
    /// none — the mode it has, a step past the kind's limits or under Fill, a mode the kind has not,
    /// another row's id.
    /// </summary>
    public static LineHeight? Act(LineHeightChoice choice, uint id)
    {
        LineHeight? next = id switch
        {
            FewerId when choice.CanFewer => choice.Step(-1),
            MoreId when choice.CanMore => choice.Step(1),
            AutomaticId when choice.Modes => choice.Mode(LineHeightMode.Automatic),
            FixedId => choice.Mode(LineHeightMode.Fixed),
            FillId => choice.Mode(LineHeightMode.Fill),
            _ => null,
        };
        return next == choice.Now ? null : next;
    }

    /// <summary>
    /// §411: the rows for <paramref name="choice"/>, standing still at the menu's foot (<see cref="FlyoutItem.Foot"/>,
    /// §276), over "Appearance" (OptionMenu.Foot, §314): "Height", a choice saying its mode at its end and opening
    /// them — Automatic where the kind has it, Fixed, Fill —, a pick going back; then "Rows", the stepper of its
    /// rows in the menu itself, "Max rows" where they cap an automatic height; §660 none under Fill, which ignores them.
    /// </summary>
    public static List<FlyoutItem> Rows(LineHeightChoice choice)
    {
        var mode = choice.Now.Mode;
        List<FlyoutItem> modes = [];
        if (choice.Modes)
        {
            modes.Add(new FlyoutItem(AutomaticId, S.DockEditHeightAutomatic, new FlyoutIcon.Glyph(OptionGlyphs.Automatic), Checked: mode == LineHeightMode.Automatic, Stays: true));
        }
        modes.Add(new FlyoutItem(FixedId, S.DockEditHeightFixedHeight, new FlyoutIcon.Glyph(OptionGlyphs.Ruler), Checked: mode == LineHeightMode.Fixed, Stays: true));
        modes.Add(new FlyoutItem(FillId, S.SettingsWidgetSizeModeFill, new FlyoutIcon.Glyph(OptionGlyphs.Fill), Checked: mode == LineHeightMode.Fill, Stays: true));
        FlyoutItem height = new(HeightId, S.DockEditHeightTitle, new FlyoutIcon.Glyph(OptionGlyphs.Height), Children: modes, Value: new FlyoutValue(Said(choice.Now)), Foot: true, PickOne: true) { Setting = HeightSetting };
        // §660: Fill ignores the rows, so it has no stepper; its cells are kept for the mode picked next.
        if (choice.Now.Fill)
        {
            return [height];
        }
        return
        [
            height,
            new(
                FewerId,
                StepperTitle(choice.Now),
                new FlyoutIcon.Glyph(OptionGlyphs.Rows),
                // §456: its value's slot as wide as its widest count, so its "−" stays put every half step.
                Control: new FlyoutControl.Stepper(Count(choice.Now.Cells), FewerId, MoreId, choice.CanFewer, choice.CanMore, S.DockEditHeightFewer, S.DockEditHeightMore)
                {
                    Range = FlyoutControl.Stepper.Over(choice.Now.Cells, choice.Least, choice.Most, Count),
                },
                // §529: what the rows are is the mode's: the stepper stands under Height.
                Foot: true) { Setting = RowsSetting, Under = HeightSetting },
        ];
    }

    /// <summary>§529: the Height row's and the rows' stepper's settings (FlyoutItem.Setting), the stepper under Height.</summary>
    public const string HeightSetting = "LineHeight";

    public const string RowsSetting = "LineHeightRows";

    /// <summary>§529: a sized line's Size row's setting (FlyoutItem.Setting).</summary>
    public const string SizeSetting = "LineSize";

    /// <summary>
    /// What the rows' stepper is: the most an automatic height may be ("Max rows"), or the rows a fixed one
    /// is ("Rows").
    /// </summary>
    public static string StepperTitle(LineHeight height) => height.Fixed ? S.DockEditHeightRows : S.DockEditHeightMax;

    /// <summary>
    /// §442: what the stepper says <paramref name="cells"/> are, in the list's rows, two cells each: "4 rows", and
    /// "6½ rows" for an odd count.
    /// </summary>
    public static string Count(int cells) =>
        cells % LineHeight.Row == 0 ? S.DockEditHeightRowCount(cells / LineHeight.Row) : S.DockEditHeightRowsAndAHalf(cells / LineHeight.Row);

    /// <summary>What the "Height" row says it is: "Automatic", "Fixed", "Fill".</summary>
    public static string Said(LineHeight height) => height.Mode switch
    {
        LineHeightMode.Fill => S.SettingsWidgetSizeModeFill,
        LineHeightMode.Fixed => S.DockEditHeightFixedHeight,
        _ => S.DockEditHeightAutomatic,
    };

    /// <summary>
    /// <paramref name="items"/> — a menu showing the height rows — with those rows as
    /// <paramref name="choice"/> has them now, and every other row as it is.
    /// </summary>
    public static List<FlyoutItem> Updated(IReadOnlyList<FlyoutItem> items, LineHeightChoice choice)
    {
        var at = items.ToList().FindIndex(item => item.Id == HeightId);
        if (at < 0)
        {
            return [.. items];
        }
        return [.. items.Take(at), .. Rows(choice), .. items.Skip(at + 1).Where(item => item.Id != FewerId)];
    }

    /// <summary>§432: <paramref name="items"/> — a menu showing the "Size" row — with it as <paramref name="item"/> has it now.</summary>
    public static List<FlyoutItem> UpdatedSize(IReadOnlyList<FlyoutItem> items, WidgetItem item)
    {
        var at = items.ToList().FindIndex(each => each.Id == SizeId);
        return at < 0 ? [.. items] : [.. items.Take(at), .. SizeRows(item), .. items.Skip(at + 1)];
    }
}
