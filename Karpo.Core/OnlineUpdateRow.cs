// "Update online widgets": the one interval everything online updates at (OnlineUpdates.cs). What it
// offers, in words, and where a value in the file stands among them: the settings app's General page
// (SettingsModel.Online.cs) and the edit bar's ⚙ › Online › (KarpoMenu.Online.cs, §337) alike.
//
// The person (2026-10-05): "there should be only one counter for online updates". So the weather's
// and the stocks' own "Update" rows went, and this one took their choices — the two lists together,
// 5 minutes to 3 hours — so whatever somebody had chosen on either is still one of them.

namespace Karpo.Core;

/// <summary>What the online interval's row offers, and how it says each.</summary>
public static class OnlineUpdateRow
{
    /// <summary>The intervals offered, shortest first, in minutes (<see cref="OnlineUpdates.Choices"/>).</summary>
    public static IReadOnlyList<int> Minutes => OnlineUpdates.Choices;

    /// <summary>
    /// §337: the choices with <paramref name="own"/> — the file's — in its place where it is none of them
    /// (a hand edit), so the edit bar's menu still shows it ticked rather than nothing.
    /// </summary>
    public static IReadOnlyList<int> MinutesWith(int own) => Minutes.Contains(own) ? Minutes : [.. Minutes.Append(own).Order()];

    /// <summary>Each choice in words, in the language shown now: made each time, never kept.</summary>
    public static IReadOnlyList<string> Labels => [.. Minutes.Select(Say)];

    /// <summary>"Every 15 minutes", "Every hour", "Every 2 hours".</summary>
    public static string Say(int minutes) => minutes switch
    {
        60 => S.SettingsOnlineUpdateEveryHour,
        > 60 when minutes % 60 == 0 => S.SettingsOnlineUpdateEveryHours(minutes / 60),
        _ => S.SettingsOnlineUpdateEveryMinutes(minutes),
    };

    /// <summary>
    /// Where <paramref name="minutes"/> — the file's value — stands among the choices; -1 for one that
    /// is none of them (a hand edit), which the box shows as its placeholder rather than rounding it
    /// to a choice and saving that behind the person's back.
    /// </summary>
    public static int IndexOf(int minutes)
    {
        for (var index = 0; index < Minutes.Count; index++)
        {
            if (Minutes[index] == minutes)
            {
                return index;
            }
        }
        return -1;
    }
}
