// What a row of the sound panel is, once: its geometry, what its controls say and do, and the one
// guard that keeps a drag smooth. The sound flyout placed these rows (SoundPage, gone with the
// flyout when the sound button became the sound preset, 2026-10-10) and a folder's cards place the
// very same rows in a card (SoundPartTile) — the quick settings' own controls, told the same things
// by the same code (docs\widgets.md §9, the sound conversion).
//
// This is seam 1 for a retained-control page. A painter panel splits its painters; this page has
// none — every control paints itself — so what splits is the placement: the page used to work out
// each row's boxes and each control's words inline, and now asks here. The page draws exactly what
// it drew before, pixel for pixel, because the arithmetic moved rather than changed.

using Karpo.Dock.QuickSettings.Controls;
using Karpo.Dock.QuickSettings.Services;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock.QuickSettings;

internal static class SoundRows
{
    // Segoe Fluent Icons: the volume levels, muted, a speaker, an app, and a microphone with its
    // muted twin.
    internal static readonly string[] Levels = [Glyph(0xE992), Glyph(0xE993), Glyph(0xE994), Glyph(0xE995)];
    internal static readonly string Muted = Glyph(0xE74F);
    internal static readonly string Speaker = Glyph(0xE7F5);
    internal static readonly string AppGlyph = Glyph(0xECAA);
    internal static readonly string Mic = Glyph(0xE720);
    internal static readonly string MicMuted = Glyph(0xEC54);

    /// <summary>Where the volume row's three controls go, and the y its row ends at.</summary>
    internal readonly record struct LineBoxes(D2D_RECT_F Button, D2D_RECT_F Slider, D2D_RECT_F Number, float Bottom);

    /// <summary>
    /// The volume row at <paramref name="y"/>, <paramref name="height"/> tall: the mute button at the
    /// row's start, the slider filling the middle, the level at the end. The controls are centred
    /// in the height they are given, so a card's line and the flyout's row draw the same row.
    /// </summary>
    internal static LineBoxes Line(QuickContext context, float rowLeft, float rowRight, float y, float height)
    {
        var buttonSize = context.D(40);
        var left = rowLeft + context.D(4);
        var button = PopupPaint.Rect(left, y + PopupPaint.Round((height - context.D(36)) / 2), buttonSize, context.D(36));
        var numberWidth = context.T(36);
        var numberRight = rowRight - context.D(12);
        // A button's gap from the button to where the thumb can reach (§449).
        var sliderLeft = left + buttonSize + context.D(LineButton.GapDip);
        var sliderRight = numberRight - numberWidth - context.D(8);
        return new LineBoxes(
            button,
            PopupPaint.Rect(sliderLeft, y, MathF.Max(1, sliderRight - sliderLeft), height),
            new D2D_RECT_F(numberRight - numberWidth, y, numberRight, y + height),
            y + height);
    }

    /// <summary>
    /// Tells the volume row's controls what the output is doing: the speaker glyph for the level
    /// (a wave behind it while it plays), and the level on the slider and its number — under the
    /// same guard as every slider here (<see cref="Show"/>).
    /// </summary>
    internal static void Master(Button button, Slider slider, Label number, float level, bool muted)
    {
        var percent = Percent(level);
        button.Glyph = LevelGlyph(percent, muted);
        button.GlyphBack = muted ? null : Levels[3];
        Show(slider, number, level);
    }

    /// <summary>Where one mix's three controls go, and the y its row ends at.</summary>
    internal readonly record struct MixBoxes(D2D_RECT_F Icon, D2D_RECT_F Slider, D2D_RECT_F Number, float Bottom);

    /// <summary>A mix row's height: as tall as a list's rows, so the mixer reads as a list.</summary>
    internal static float MixHeight(QuickContext context) => context.T(40);

    /// <summary>
    /// One mix at <paramref name="y"/> in a row <paramref name="height"/> px tall (<see cref="MixHeight"/>
    /// where nothing presses it): the app's icon where a row's icon sits, the slider filling the rest,
    /// and the level at the end, centred in the height given. <paramref name="textLeft"/> is where the
    /// host's text starts — the icon hangs a little to the left of it, the way a list row's icon lines
    /// up with the rows above it — and <paramref name="rowRight"/> is where the row ends.
    /// </summary>
    internal static MixBoxes MixIn(QuickContext context, float textLeft, float rowRight, float y, float height)
    {
        var box = MathF.Min(context.D(32), height);
        var left = textLeft - context.D(6);
        var icon = PopupPaint.Rect(left, y + PopupPaint.Round((height - box) / 2), box, box);
        var numberWidth = context.T(30);
        var numberRight = rowRight - context.D(12);
        var sliderLeft = left + context.D(32) + context.D(LineButton.GapDip);
        var sliderRight = numberRight - numberWidth - context.D(4);
        return new MixBoxes(
            icon,
            PopupPaint.Rect(sliderLeft, y, MathF.Max(1, sliderRight - sliderLeft), height),
            new D2D_RECT_F(numberRight - numberWidth, y, numberRight, y + height),
            y + height);
    }

    /// <summary>
    /// Tells one mix's controls who they are for: the icon is the app's own (or a glyph where it has
    /// none), a click on it mutes and unmutes, and the slider sets the app's level. The name is the
    /// row's name for the keyboard and for anything reading the panel, not a line of its own.
    /// </summary>
    internal static void App(
        AudioApp app, Button icon, Slider slider, Label number, ISoundSource source, Changes changes, QuickContext context)
    {
        var key = app.Key;
        var muted = app.Muted;
        icon.Picture = app.System ? null : AppLooks.IconOf(key, app.Aumid, app.Exe, (int)context.D(20), changes);
        icon.Glyph = icon.Picture is null ? app.System ? Speaker : AppGlyph : null;
        icon.Badge = muted ? Muted : null;
        icon.Dimmed = muted;
        icon.Clicked = () =>
        {
            source.SetAppMuted(key, !muted);
            changes.Add(SoundNews.SessionChanged);
        };
        slider.Changed = level =>
        {
            source.SetAppVolume(key, level / 100f);
            number.Text = Number(level);
        };
    }

    /// <summary>
    /// Shows <paramref name="level"/> on a slider and the number beside it — unless the slider is
    /// being dragged, in which case it shows what the drag says and leaves the slider alone.
    /// <para>
    /// <b>This guard looks redundant and is the only thing keeping a drag smooth.</b> A drag is news:
    /// every value it passes through is set, Windows reports each one back, and the host rebuilds
    /// from what it reads. Without the guard that rebuild writes the level Windows had got to into
    /// the slider under the pointer, and the thumb jumps back towards it — a thumb that fights the
    /// hand, and only while somebody is dragging, which is exactly when nobody is looking at the
    /// code. It came across from the panel unchanged, and it lives here so that no host of these
    /// rows can place a slider without it.
    /// </para>
    /// </summary>
    internal static void Show(Slider slider, Label number, float level)
    {
        if (!slider.Dragging)
        {
            slider.Value = Percent(level);
        }
        number.Text = Number(slider.Value);
    }

    /// <summary>
    /// Tells a row which device it is: its name and glyph, whether it is the one in use, and what
    /// a click on it does. The device in use takes no click — it is already chosen.
    /// </summary>
    internal static void Device(ListRow row, AudioOutput device, Action<string> choose)
    {
        var id = device.Id;
        row.Title = device.Name;
        row.Glyph = device.Glyph;
        row.Selected = device.Default;
        row.Clicked = device.Default ? null : () => choose(id);
    }

    /// <summary>The speaker glyph for a level, as the taskbar shows it: muted, silent, then a wave a third.</summary>
    internal static string LevelGlyph(int percent, bool muted) =>
        muted ? Muted : Levels[percent switch
        {
            <= 0 => 0,
            <= 33 => 1,
            <= 66 => 2,
            _ => 3,
        }];

    internal static string Number(int level) => level.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>A level of 0 to 1 as whole percent, halves away from zero as everywhere in Karpo.</summary>
    internal static int Percent(float level) => (int)MathF.Round(Math.Clamp(level, 0, 1) * 100, MidpointRounding.AwayFromZero);

    private static string Glyph(int code) => char.ConvertFromUtf32(code);
}
