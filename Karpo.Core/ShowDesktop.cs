// Show desktop's style (2026-10-23, docs\widgets.md §302). The person: "show desktop widget should
// get another style as well where no icon is shown. in edit mode it should still show an icon but also
// in the greyed out color".
//
// So the button is its icon, as it always was, or Windows 11's far corner of the taskbar: the same
// click target with nothing drawn on it, lit under the pointer as a button is. While the bar is
// arranged the corner shows its icon in the disabled ink, as a widget whose thing this PC hasn't got
// is greyed (§247), or it could not be found, moved or set.

using System.Text.Json.Serialization;

namespace Karpo.Core;

/// <summary>
/// What Show desktop (<see cref="BarKind.ShowDesktop"/>) draws. Absent means its icon, as before; it
/// is only written for a button somebody set to the corner, so a file keeps nothing it does not need.
/// </summary>
public sealed class ShowDesktopOptions
{
    public ShowDesktopStyle Style { get; set; }

    /// <summary>These options, or null — the options dropped — where its icon is chosen.</summary>
    public ShowDesktopOptions? Normalized() => Style == ShowDesktopStyle.Corner ? this : null;
}

/// <summary>Show desktop's two styles (see the top of the file).</summary>
[JsonConverter(typeof(TolerantEnumConverter<ShowDesktopStyle>))]
public enum ShowDesktopStyle
{
    /// <summary>Its icon, the desktop's: the default.</summary>
    [JsonStringEnumMemberName("icon")] Icon,

    /// <summary>No icon: the click target alone, Windows 11's far corner; its icon greyed while the bar is arranged.</summary>
    [JsonStringEnumMemberName("corner")] Corner,
}
