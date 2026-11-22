// App signals: the dock's part of them (AppSignals.cs). What each app button shows beyond its
// running indicator — the progress and overlay its windows set through the taskbar list, and the
// badge its app set — worked out with the button (TilesFor), so every bar showing the app shows them.
//
// The taskbar list keeps the band it found when an app made it (Taskband.cs), so an app that made
// it before Karpo's band was there keeps talking to Explorer's, and one that made it while Karpo's
// was there keeps talking to a window that is gone once Karpo stops. Explorer's answer to the same
// problem is TaskbarButtonCreated: it sends it to a window whenever it makes the window's button,
// Explorer restarts included, and an app that uses the taskbar list makes it again then and sets
// everything again. So Karpo sends it too, to the windows it lists, whenever the band apps should
// talk to changes: when Karpo's comes, when an Explorer restart may have handed some apps the new
// Explorer's first, and when Karpo's goes.

using Karpo.Core;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

public sealed unsafe partial class Dock
{
    private const string BadgesValue = "TaskbarBadges";

    /// <summary>Windows' "Show badges on taskbar apps" (Settings, Personalization, Taskbar behaviours): on unless it says 0.</summary>
    private bool badgesShown = ReadBadgesShown();

    /// <summary>Whether the dock looks at the notification store for badges now (<see cref="SyncBadges"/>).</summary>
    private bool badgesWatched;

    /// <summary>The user the notification store counts the dock's badges as; never an island, so woken through the store's queue.</summary>
    private readonly object badgeUser = new();

    /// <summary>
    /// What <paramref name="open"/> — a button's windows, most recently used first — and its
    /// <paramref name="item"/> say on the button: the windows' progress and overlay, and the app's badge.
    /// </summary>
    private TileSignal SignalFor(Item item, IReadOnlyList<HWND> open)
    {
        var windows = new nint[open.Count];
        for (var index = 0; index < open.Count; index++)
        {
            windows[index] = (nint)open[index];
        }
        return AppSignals.Shared.For(windows, BadgeFor(item, open));
    }

    /// <summary>
    /// The badge of the app <paramref name="item"/> stands for: by the AUMID of one of its windows, or
    /// by the item's own identity, which is how a pinned app with no window open still shows one.
    /// </summary>
    private Badge? BadgeFor(Item item, IReadOnlyList<HWND> open)
    {
        if (!badgesWatched)
        {
            return null;
        }
        var badges = NotificationStore.Shared.Badges;
        if (badges.Count == 0)
        {
            return null;
        }
        foreach (var hwnd in open)
        {
            if (apps.Get(hwnd)?.Aumid is { } aumid && badges.TryGetValue(aumid, out var shown))
            {
                return shown;
            }
        }
        foreach (var (app, badge) in badges)
        {
            if (item.Matches(Items.AumidKey(app)))
            {
                return badge;
            }
        }
        return null;
    }

    /// <summary>
    /// The band posted WM_KARPO_SIGNALS: windows' signals changed, and the buttons follow, and so
    /// does an open window list — its toolbars in place, its cards again where tabs came or went.
    /// </summary>
    private void SignalsChanged()
    {
        AppSignals.Shared.Prune(hwnd => IsWindow((HWND)hwnd));
        WindowsChanged(Change.States);
        if (preview is { IsOpen: true, Tile: int tile } list && previewOwner is { } owner)
        {
            if (!WithTabs(previewWindows).SequenceEqual(list.Windows))
            {
                ShowList(owner, tile, previewWindows, previewAnchor, previewItem, previewSpan);
            }
            else
            {
                list.ThumbsChanged();
            }
        }
    }

    /// <summary>What <paramref name="window"/>'s app said of its preview: its toolbar, its tooltip and its clip.</summary>
    private static ThumbInfo? ThumbOf(HWND window) => AppSignals.Shared.ThumbOf((nint)window);

    /// <summary>
    /// A window list's windows with each window that registered tabs (ITaskbarList3::RegisterTab)
    /// standing as its tabs, in their order, titled with their own titles — as the taskbar lists a
    /// tabbed window.
    /// </summary>
    private static List<(HWND Hwnd, string Title)> WithTabs(List<(HWND Hwnd, string Title)> listed)
    {
        List<(HWND Hwnd, string Title)>? expanded = null;
        for (var index = 0; index < listed.Count; index++)
        {
            var tabs = AppSignals.Shared.TabsOf((nint)listed[index].Hwnd);
            if (tabs.Count == 0)
            {
                expanded?.Add(listed[index]);
                continue;
            }
            expanded ??= [.. listed.Take(index)];
            foreach (var tab in tabs)
            {
                if (IsWindow((HWND)tab))
                {
                    expanded.Add(((HWND)tab, TitleOf((HWND)tab)));
                }
            }
        }
        return expanded ?? listed;
    }

    /// <summary>The window a card brings forward: a tab's own window for a tab's card, else the card's.</summary>
    private static HWND TabsWindow(HWND shown) => AppSignals.Shared.MainOf((nint)shown) is var main and not 0 ? (HWND)main : shown;

    /// <summary>
    /// A tab's card was chosen: the tab hears WM_ACTIVATE, as the taskbar tells it, and its app shows
    /// that tab in the window just brought forward. Nothing for a card that is no tab's.
    /// </summary>
    private static void TabActivated(HWND shown)
    {
        if (AppSignals.Shared.MainOf((nint)shown) != 0)
        {
            _ = PostMessageW(shown, WM.WM_ACTIVATE, (nuint)WA_ACTIVE, 0);
        }
    }

    /// <summary>A window's title, as the window list shows it.</summary>
    private static string TitleOf(HWND window)
    {
        var text = stackalloc char[512];
        var length = GetWindowTextW(window, text, 512);
        return new string(text, 0, Math.Max(length, 0));
    }

    /// <summary>A window is gone, and so is everything it said on its button.</summary>
    private static void WindowGone(HWND window) => AppSignals.Shared.Forget((nint)window);

    /// <summary>
    /// Tells every window the dock lists that its taskbar button was made, as Explorer does when it
    /// makes one (see the top of the file): apps make their taskbar list again, find the band apps
    /// should talk to now, and set their progress, overlay and thumbnail buttons again.
    /// </summary>
    private void AskAppsForSignals()
    {
        uint created;
        fixed (char* name = "TaskbarButtonCreated")
        {
            created = RegisterWindowMessageW(name);
        }
        if (created == 0)
        {
            return;
        }
        foreach (var window in apps.All)
        {
            // Not waited for: an app that is slow to answer must not hold the bars up.
            _ = SendNotifyMessageW(window.Hwnd, created, default, default);
        }
    }

    /// <summary>
    /// Looks at the notification store's badges while Windows shows badges and a bar shows app
    /// buttons — on it, or pins in a folder on it, which show their badges too — and stops looking
    /// otherwise. True when that changed, and the buttons are to follow: with the services, the
    /// rebuild right after does that.
    /// </summary>
    private bool SyncBadges()
    {
        var wanted = badgesShown && Folders.All(PlacedIslands().Select(entry => entry.Island))
            .Any(entry => entry.Island is BarItem { Kind: BarKind.Pin or BarKind.Apps } || entry.Island.Info == WidgetInfos.Pin);
        if (wanted == badgesWatched)
        {
            return false;
        }
        badgesWatched = wanted;
        var store = NotificationStore.Shared;
        if (wanted)
        {
            store.Changed += BadgesChanged;
            store.Use(badgeUser);
        }
        else
        {
            store.Changed -= BadgesChanged;
            store.Release(badgeUser);
        }
        return true;
    }

    /// <summary>The store read the badges again (or anything else): the buttons show what it holds now.</summary>
    private void BadgesChanged() => WindowsChanged(Change.States);

    /// <summary>A setting changed somewhere: "Show badges on taskbar apps" may be among it.</summary>
    private void BadgeSettingChanged()
    {
        var shown = ReadBadgesShown();
        if (shown == badgesShown)
        {
            return;
        }
        badgesShown = shown;
        if (SyncBadges())
        {
            WindowsChanged(Change.States);
        }
    }

    private static bool ReadBadgesShown()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced");
            return key?.GetValue(BadgesValue) is not int shown || shown != 0;
        }
        catch (Exception error) when (error is System.Security.SecurityException or IOException or UnauthorizedAccessException)
        {
            return true;
        }
    }
}
