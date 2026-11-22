// The pen menu, the touch keyboard and the virtual touchpad: three of Windows' own buttons, which
// its taskbar keeps in the corner beside the tray and shows only where they are of use — the pen
// menu on a PC with a pen, the touch keyboard on a touch screen with no keyboard attached, the
// virtual touchpad on a touch screen — or wherever the person turned them on in Windows' "System
// tray icons" settings (Settings > Personalization > Taskbar).
//
// The user: "implement all… orient yourself on how windows11 does it". So each is a taskbar entry
// (BarEntries) that shows where Windows would show it and takes no room on the bar where Windows
// would not, and its one option is the person's choice that settings page gives: the way Windows
// decides, or always (InputButtonOptions). Never is taking it off the bar.
//
// Except the touch keyboard, which has no option and shows wherever it is placed: "touch keyboard
// when it shows option should not be configurable. it should always show" (2026-10-08). A choice
// an older file kept for it is dropped where the file is read (WidgetItem.Normalize).
//
// A new bar has the ones Windows would show on this PC (ForNewBar): the pen menu where there is a
// pen, the touch keyboard where there is a touch screen, and not the virtual touchpad, which
// Windows leaves off until somebody turns it on.

using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace Karpo.Core;

/// <summary>When the pen menu or the virtual touchpad shows (<see cref="InputButtons"/>).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<InputButtonShow>))]
public enum InputButtonShow
{
    /// <summary>Where Windows would show it: on a PC with what it is for (<see cref="InputButtons.Shows(BarKind, InputButtonShow, InputHardware)"/>).</summary>
    [JsonStringEnumMemberName("automatic")] Automatic,

    /// <summary>On every PC, whatever it has, as Windows' "Always" and its switches turned on.</summary>
    [JsonStringEnumMemberName("always")] Always,
}

/// <summary>
/// The pen menu's and the virtual touchpad's one option: when the button shows. Absent means <see cref="InputButtonShow.Automatic"/>, and it is only written for a
/// button somebody set to Always, so a file keeps nothing it does not need.
/// </summary>
public sealed class InputButtonOptions
{
    public InputButtonShow Show { get; set; }

    /// <summary>These options, or null — the options dropped — where nothing but Automatic is chosen.</summary>
    public InputButtonOptions? Normalized() => Show == InputButtonShow.Always ? this : null;
}

/// <summary>
/// What this PC has, as far as the three input buttons ask: a pen it can be used with, a touch
/// screen, and whether it is a tablet right now — a keyboard folded away or taken off, which is
/// what Windows means by "no keyboard attached".
/// </summary>
public readonly record struct InputHardware(bool Pen, bool Touch, bool Slate)
{
    private const int SmDigitizer = 94;
    private const int SmConvertibleSlateMode = 0x2003;
    private const int IntegratedTouch = 0x01;
    private const int ExternalTouch = 0x02;
    private const int IntegratedPen = 0x04;
    private const int ExternalPen = 0x08;
    private const int Ready = 0x80;

    /// <summary>
    /// This PC as Windows describes it now (GetSystemMetrics): the digitizers that are ready, and
    /// the posture — 0 for a slate, anything else for a laptop or a PC that is not convertible.
    /// </summary>
    public static InputHardware Read()
    {
        var digitizer = GetSystemMetrics(SmDigitizer);
        var ready = (digitizer & Ready) != 0;
        return new InputHardware(
            Pen: ready && (digitizer & (IntegratedPen | ExternalPen)) != 0,
            Touch: ready && (digitizer & (IntegratedTouch | ExternalTouch)) != 0,
            Slate: GetSystemMetrics(SmConvertibleSlateMode) == 0);
    }

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);
}

/// <summary>The rules the three input buttons show by, and the words for them (see the note at the top of this file).</summary>
public static class InputButtons
{
    /// <summary>The three, in the order the taskbar keeps them.</summary>
    public static readonly BarKind[] Kinds = [BarKind.PenMenu, BarKind.TouchKeyboard, BarKind.VirtualTouchpad];

    /// <summary>Whether <paramref name="kind"/> is one of the three.</summary>
    public static bool Is(BarKind kind) => kind is BarKind.PenMenu or BarKind.TouchKeyboard or BarKind.VirtualTouchpad;

    /// <summary>
    /// Whether <paramref name="kind"/> shows where Windows decides, and so has the option: the pen
    /// menu and the virtual touchpad. The touch keyboard always shows.
    /// </summary>
    public static bool Decides(BarKind kind) => kind is BarKind.PenMenu or BarKind.VirtualTouchpad;

    /// <summary>
    /// Whether a button of <paramref name="kind"/> set to <paramref name="show"/> shows on a PC with
    /// <paramref name="here"/>: always where it is set so; else the pen menu where there is a pen
    /// and the virtual touchpad on a touch screen. Every other kind shows, the touch keyboard too:
    /// these rules are the two buttons' alone.
    /// </summary>
    public static bool Shows(BarKind kind, InputButtonShow show, InputHardware here) =>
        !Decides(kind) || show == InputButtonShow.Always || (kind == BarKind.PenMenu ? here.Pen : here.Touch);

    /// <summary>Whether <paramref name="item"/> shows on a PC with <paramref name="here"/>, by its own option.</summary>
    public static bool Shows(BarItem item, InputHardware here) =>
        Shows(item.Kind, item.InputButton?.Show ?? InputButtonShow.Automatic, here);

    /// <summary>
    /// The buttons a new bar holds on a PC with <paramref name="here"/>, each where Windows would
    /// show it: the pen menu on a PC with a pen and the touch keyboard on one with a touch screen,
    /// both as Windows decides; never the virtual touchpad, which Windows leaves off.
    /// </summary>
    public static IEnumerable<BarItem> ForNewBar(InputHardware here)
    {
        if (here.Pen)
        {
            yield return BarKind.PenMenu.New();
        }
        if (here.Touch)
        {
            yield return BarKind.TouchKeyboard.New();
        }
    }

    /// <summary>How Windows decides for <paramref name="kind"/>, in the words its page shows.</summary>
    public static string Rule(BarKind kind) => kind switch
    {
        BarKind.PenMenu => S.CoreInputRulePen,
        BarKind.VirtualTouchpad => S.CoreInputRuleTouchpad,
        _ => "",
    };

    /// <summary>
    /// What a button of <paramref name="kind"/> set to <paramref name="show"/> does on a PC with
    /// <paramref name="here"/>, in words: the rule, and whether this PC meets it now.
    /// </summary>
    public static string Says(BarKind kind, InputButtonShow show, InputHardware here)
    {
        if (!Decides(kind))
        {
            return "";
        }
        if (show == InputButtonShow.Always)
        {
            return S.CoreInputAlways;
        }
        var shows = Shows(kind, show, here);
        var why = kind switch
        {
            BarKind.PenMenu => shows ? S.CoreInputWhyPen : S.CoreInputWhyNoPen,
            _ => shows ? S.CoreInputWhyTouch : S.CoreInputWhyNoTouch,
        };
        return S.CoreInputSays(rule: Rule(kind), why: why);
    }
}
