// menus: a widget's menu one width, whatever it says (2026-10-19, the person: "these configuration
// menus should always have the same fixed width regardless of contents"; docs\widgets.md §230). §225
// had a widget's menu grow to its longest row, from 256 to 480 DIP, so each of its levels was a width
// of its own and a level's width changed as a setting's value did. Now a menu that Configures — a
// widget's, its settings and its faces — and every level of it shown in place, a search's among them, are
// SettingsWidthDip: wider than Windows' 256 DIP menus, so a setting's name and what it is set to fit
// side by side, and with Windows' text size only. A longer row is cut with an ellipsis, its name
// and its value sharing it (Flyout.Values.cs, §315). Other menus keep Windows' one width (a jump list, the
// bar's menu). And a menu never begins with a rule: a folder's faces, ruled off from the rows over
// them, where none stand over them.
//
// And its height (Flyout.Inline.cs): a configuration menu — edit mode's menu of a widget, a folder or a
// folder's line, its settings and its faces (Dock.WidgetMenu) — is one height on every level and for
// every widget, SettingsHeightDip, to the most the room on its side of the screen holds (§276; §288, the
// person: "the ones that are opened by left click and show the widget configuration or show the folder
// configuration or the taskbar widget configuration should have a fixed height"); every other menu, a
// widget's at rest among them, is as tall as its rows (§286).
//
// §517: the edit bar's menus (⚙ Karpo, Display, Taskbar, Look) are no widget's: as tall as their rows and as wide as their
// widest row needs, from SettingsWidthDip up to the work area (FitsContent, FitWidth), so a title is never cut ("Language
// and regi…" in the settings-menu study, at ⚙'s one height and width).

namespace Karpo.Dock;

public sealed unsafe partial class Flyout
{
    /// <summary>A widget's settings menu's one width, and its levels', in DIP at a text size of 100 % (§230).</summary>
    public const float SettingsWidthDip = 320;

    /// <summary>The width in px of a menu that <see cref="Configures"/>, at <paramref name="scale"/> and Windows' <paramref name="textScale"/>.</summary>
    internal static float SettingsWidth(float scale, float textScale) => Round(SettingsWidthDip * scale * MathF.Max(1, textScale));

    /// <summary>
    /// A configuration menu's one height, and its levels', in DIP at a text size of 100 % (§276, §288): a row of buttons, a
    /// rule and twelve rows; ⚙'s first level holds Styles (§467) and still never scrolls (R2), the taskbar's row gone (§465).
    /// </summary>
    public const float SettingsHeightDip = 440;

    /// <summary>The height in px of a <see cref="Configuration"/> menu, at <paramref name="scale"/> and Windows' <paramref name="textScale"/>.</summary>
    internal static float SettingsHeight(float scale, float textScale) => Round(SettingsHeightDip * scale * MathF.Max(1, textScale));

    /// <summary>
    /// §517: how wide <paramref name="items"/> and every level under them need a menu to be (px) for each row's words and
    /// what it is set to to stand whole: from the row's start (<paramref name="lead"/>), its icon's column, its words, the
    /// gap and its value, then what stands at its end — a chevron or a tick (<paramref name="after"/> and
    /// <paramref name="glyphBox"/>), a switch, a stepper, buttons — and the room at the menu's side (<paramref name="trail"/>).
    /// </summary>
    private float FitWidth(Gfx gfx, IReadOnlyList<FlyoutItem> items, float scale, float textPx, float column, float after, float glyphBox, float lead, float trail)
    {
        float Px(float dip) => MathF.Round(dip * scale, MidpointRounding.AwayFromZero);
        float widest = 0;
        foreach (var item in items)
        {
            if (!item.Separator && !item.Header && !item.Primary && !IsTile(item) && !FieldFillsRow(item) && !IsMix(item) && item.Text.Length > 0)
            {
                var need = lead + column + MathF.Ceiling(gfx.Measure(item.Text, textPx, Weight.Regular).X) + trail;
                if (item.Value is { } value)
                {
                    need += Px(ValueGapDip) + (value.Text.Length > 0 ? MathF.Ceiling(gfx.Measure(value.Text, textPx, Weight.Regular).X) : 0)
                        + (value.Icon is not null ? Px(16) + (value.Text.Length > 0 ? Px(ValueGapDip) : 0) : 0);
                }
                if (item.Children is not null || item.Checked is not null)
                {
                    need += after + glyphBox;
                }
                if (item.Children is not null && item.Checked is not null)
                {
                    need += after + glyphBox;
                }
                if (item.Control is { } held && AtEnd(held))
                {
                    need += after + ControlWidth(held, scale, held is FlyoutControl.Stepper stepper ? StepValueWidth(gfx, stepper, textPx) : 0);
                }
                if (Leads(item))
                {
                    need += column;
                }
                widest = MathF.Max(widest, need);
            }
            if (item.Children is { Count: > 0 } children)
            {
                widest = MathF.Max(widest, FitWidth(gfx, children, scale, textPx, column, after, glyphBox, lead, trail));
            }
        }
        return widest;
    }

    /// <summary><paramref name="items"/> without the rules they begin with: a menu's first row is never a rule.</summary>
    internal static IReadOnlyList<FlyoutItem> WithoutLeadingRules(IReadOnlyList<FlyoutItem> items)
    {
        var first = 0;
        while (first < items.Count && items[first].Separator)
        {
            first++;
        }
        return first == 0 ? items : [.. items.Skip(first)];
    }
}
