// What a new widget needs before it can show anything (backlog item 24, "make adding widgets
// easier"): a pin its target, a stock its symbol, the weather's kinds their place, a folder's
// contents its folder, a news feed its address, a drive widget its drive. Added without them, each
// stood on the bar or in its folder saying it had nothing to show until somebody found its settings.
//
// A widget that still has nothing to show says so with a way there (the person, 2026-10-08: "rss
// feed flyout should show shortcut to settings if it is not fully configured to show a source, this
// should also be done for all the other widgets"): its card and its line carry "Set up", which opens
// its menu, where the field is (§228: the widgets' pages in the settings app are gone; SetupLink,
// the dock's SetupPrompt). The mail, the calendar and the contacts need an account rather than a
// field of their own, and theirs, "Set up in Settings", opens that menu's Accounts level (§340). The
// rule of what needs setting up is here, once.

namespace Karpo.Core;

/// <summary>The one field a new widget needs before it shows anything.</summary>
public enum SetupField
{
    /// <summary>A pin's target: the app, file, folder or website it opens.</summary>
    Target,

    /// <summary>A stock's symbol.</summary>
    Symbol,

    /// <summary>The weather's place, for the kinds that show the weather.</summary>
    Place,

    /// <summary>rss: a news feed's address.</summary>
    Feed,

    /// <summary>drives: the drive a drive or network drive widget shows.</summary>
    Volume,

    /// <summary>sports: the team a sports widget follows.</summary>
    Team,

    /// <summary>camera: the stream a camera line shows (§331).</summary>
    Stream,
}

/// <summary>
/// Where a widget's "Set up" goes: its own configuration menu, where the setting <see cref="Field"/> is a
/// row (§228; <see cref="WidgetSetup.Needs"/>), or, where <see cref="Field"/> is null, ⚙ › Accounts
/// (§340, §351: the accounts are Karpo's, not the widget's) — the mail, the calendar, the contacts and the
/// to-do lists, which have nothing to show until an account gives them something.
/// </summary>
public readonly record struct SetupLink(SetupField? Field)
{
    /// <summary>⚙ › Accounts: a widget that needs an account rather than a field of its own.</summary>
    public static SetupLink Accounts => new(null);

    /// <summary>Whether it opens ⚙ › Accounts rather than the widget's menu.</summary>
    public bool ToAccounts => Field is null;
}

public static class WidgetSetup
{
    /// <summary>
    /// The field <paramref name="item"/> still needs before it shows anything, or null where it has
    /// what it needs: a pin with no target, a stock with no symbol, a weather kind with neither a
    /// place nor this PC's location, a folder's contents with no folder, a feed with no address, a
    /// drive widget with no drive. Never a heading: with no words of its own it says "Heading"
    /// (HeadingTile; the person, 2026-10-11: "the heading should not show a setup button but should
    /// have a default value shown").
    /// </summary>
    public static SetupField? Needs(WidgetItem item) => item.Info?.Options switch
    {
        WidgetOptionsKind.Pin when string.IsNullOrWhiteSpace(item.Pin?.Target) => SetupField.Target,
        WidgetOptionsKind.Market when item.Market?.IsSet != true => SetupField.Symbol,
        WidgetOptionsKind.Weather when item.Weather is not { UseDeviceLocation: true } and not { Latitude: not null, Longitude: not null } => SetupField.Place,
        WidgetOptionsKind.Rss when item.Rss is not { IsSet: true } => SetupField.Feed,
        WidgetOptionsKind.Drive when item.Drive is not { IsSet: true } => SetupField.Volume,
        WidgetOptionsKind.Sports when item.Sports is not { IsSet: true } => SetupField.Team,
        WidgetOptionsKind.Camera when item.Camera is not { IsSet: true } => SetupField.Stream,
        _ => null,
    };

    /// <summary>
    /// §425: the row of a widget's menu that sets <paramref name="field"/>, whose level a menu opened to set the widget
    /// up opens on: a pin's Target (its kinds), a stock's symbol, a place, a feed's address, a drive, a team, a stream.
    /// </summary>
    public static string RowOf(SetupField field) => field switch
    {
        SetupField.Target => OptionPickers.PinTarget,
        SetupField.Symbol => MarketRows.Symbol,
        SetupField.Place => OptionRows.WeatherPlace,
        SetupField.Feed => OptionRows.RssAddress,
        SetupField.Team => OptionRows.SportsTeam,
        SetupField.Stream => OptionRows.CameraUrl,
        _ => OptionRows.Drive,
    };

}
