// §485 (the person, 2026-11-02, of §481's "Show names in the top row" at the foot of ⚙ › Styles: "no, put it somewhere
// else, this is more of a karpo preset"): "General ›", ⚙'s first row, how Karpo behaves and looks as an app, whatever bar,
// layout or widget: "Start with Windows" (KarpoMenu.Startup.cs, §337, its first row until now). One row of ⚙'s first level
// in place of Start with Windows', so ⚙ still never scrolls (R2: it holds no row more). Its "Show names in the top row"
// (§481) is gone again (the person, 2026-11-03: "remove the text under the menu row icons again, it should also not be
// configurable to show again"): the top row is icons alone, their names in their tooltips.
//
// §489 (the person, 2026-11-02: "only one default animation for everything in karpo ... it should be able to be turned off
// so that there are no animations at all and the speed should be selectable as well between three levels"): "Animations", a
// switch writing "animations", and "Animation speed" — Slower, Normal, Faster — writing "animationSpeed": every motion of
// Karpo's, one engine's (Karpo.Dock\Motion.cs), off at once or every duration scaled by one factor. Windows' Animation
// effects off turns them off whatever these say.
//
// §549 (the person, 2026-11-07: "animations and animation speed can be combined into one setting"): one row, "Animations" —
// Off, Slower, Normal, Faster —, a dropdown (four choices, §521), writing "animation": Off what the switch off was, the speeds
// what they were (Karpo.Dock\Motion.cs: Slower 1.5, Normal 1, Faster ⅔ the time), Normal the default, left out of the file.

using System.Text.Json.Serialization;

namespace Karpo.Core;

/// <summary>
/// §489, §549: whether every motion of Karpo's runs, and how fast (<see cref="Config.Animation"/>): off, every surface in its
/// final state at once; else every duration of the one motion engine scaled by one factor (Karpo.Dock's Motion.Factor). A
/// word this build does not know reads as a value outside the enum, which <see cref="Config.Repair"/> makes <see cref="Normal"/>.
/// </summary>
[JsonConverter(typeof(TolerantEnumConverter<Animation>))]
public enum Animation
{
    [JsonStringEnumMemberName("normal")] Normal,

    [JsonStringEnumMemberName("slower")] Slower,

    [JsonStringEnumMemberName("faster")] Faster,

    [JsonStringEnumMemberName("off")] Off,
}

public sealed partial class Config
{
    /// <summary>
    /// §549: Karpo's animations: <see cref="Core.Animation.Off"/>, nothing moves; else at that speed. Normal for new configs,
    /// and for files that do not have it (left out of the file while it is). Windows' Animation effects off is off whatever
    /// this says.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Animation Animation { get; set; }
}

public static partial class KarpoMenu
{
    /// <summary>§489, §549: "Animations"' row.</summary>
    public const string AnimationsRow = "Animations";

    /// <summary>§549: the choices in the order the row lists them: off, then slowest first.</summary>
    public static readonly Animation[] Animations = [Animation.Off, Animation.Slower, Animation.Normal, Animation.Faster];

    /// <summary>
    /// Karpo's own ways under ⚙'s "General" heading — Start with Windows (§682 Animations the Appearance's now; §517: on the page, §485 to §513 a
    /// level of their own); see the top of the file.
    /// </summary>
    public static IReadOnlyList<OptionRow> General(Config config, KarpoServices services) => [.. Startup(config, services)];

    /// <summary>§549: "Animations", a choice of Off and the three speeds going back (R3, R5).</summary>
    public static OptionRow AnimationRow(Config config) =>
        new(AnimationsRow, OptionRowKind.Choice, S.SettingsGeneralAnimationsHeader)
        {
            Glyph = OptionGlyphs.Animations,
            Slot = OptionSlot.Look,
            Choices = [.. Animations.Select(AnimationName)],
            // §578: off, then the speeds by their icons.
            ChoiceGlyphs = [OptionGlyphs.None, OptionGlyphs.Slow, OptionGlyphs.Steady, OptionGlyphs.Fast],
            Chosen = Array.IndexOf(Animations, config.Animation),
            Set = at =>
            {
                if (at < 0 || at >= Animations.Length || config.Animation == Animations[at])
                {
                    return false;
                }
                config.Animation = Animations[at];
                return true;
            },
        };

    /// <summary>§549: what a choice is called: "Off", "Slower", "Normal", "Faster".</summary>
    public static string AnimationName(Animation animation) => animation switch
    {
        Animation.Off => S.WidgetSwitchOff,
        Animation.Slower => S.SettingsGeneralAnimationSpeedSlower,
        Animation.Faster => S.SettingsGeneralAnimationSpeedFaster,
        _ => S.SettingsGeneralAnimationSpeedNormal,
    };
}
