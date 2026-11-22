// What apps tell the taskbar about their windows beyond the windows themselves: the progress of
// something long (ITaskbarList3::SetProgressState and SetProgressValue), an overlay on the button
// (SetOverlayIcon, with its description for whoever cannot see it), and the badge a packaged app
// sets through BadgeUpdateManager (Mail's unread count, a chat app's status).
//
// The first two reach Karpo through its own band (Taskband.cs), one window at a time; the badge is a
// row in Windows' notification store (NotificationStore.cs), one app at a time. Both end here, and a
// bar's app button asks for what its windows and its app carry (For), which is a TileSignal in the
// button's Mark: a changed signal is a changed mark, set in place like the running indicator.
//
// How a button that stands for several windows shows them is Windows' rule where Windows has one and
// Karpo's where it does not:
//   - the overlay is the one set last, and when that one is taken away, the one it covered comes
//     back while its window still has it (ITaskbarList3::SetOverlayIcon's documentation, word for
//     word: overlays are per group, "the last overlay received is the overlay shown");
//   - progress is Karpo's rule, since Windows documents none: the window that needs the person most
//     (an error, then paused, then running, then busy with no end in sight), and among equals the
//     one used last — the button's windows are in that order already.
//
// How they are drawn is Windows 11's, as its taskbar's own resources describe it (Taskbar.View's
// TaskListButton, read from this PC's resources.pri): the progress is a WinUI ProgressBar where the
// running indicator is, in the accent colour while it runs (AccentFillColorDefault — not the green of
// Windows 7 to 10), SystemFillColorCaution paused and SystemFillColorCritical on an error, over the
// ProgressBar's hairline track; the running indicator gives way to it. The badge is a pill in
// AccentFillColorDefault with TextOnAccentFillColorPrimary on it; the overlay sits at the icon's
// lower right, where every Windows since 7 has put it. SignalInk and SignalShape are those
// decisions, asked by the bar (Scene) and the offscreen tests alike.
//
// A progress with nothing done shows nothing, and the running indicator stays (ShowsProgress).
// Apps set TBPF_NORMAL, or a value of 0, long before anything is done, or while nothing is going
// on at all — Spotify's window carries one all the time it runs — because on the taskbar they
// were written for an empty fill is no fill: Windows 7 to 10 drew the share done over the button
// and nothing of an empty one, and Windows 11 still shows such an app's button with its running
// indicator alone.
// Drawn as it came, it was a bare hairline track where the indicator should be, the width of the
// button: the "1px border at the bottom" of Spotify's pin. A run without an end has nothing done by
// definition and always shows; paused and failed show from the first share done, as they did.

using System.Globalization;
using System.Numerics;
using Karpo.Core;
using TerraFX.Interop.DirectX;
using Windows.Data.Xml.Dom;
using Color = Windows.UI.Color;

namespace Karpo.Dock;

/// <summary>What a button's progress shows, from ITaskbarList3::SetProgressState's TBPF flags.</summary>
public enum ProgressKind
{
    /// <summary>TBPF_NOPROGRESS: nothing; the running indicator is back.</summary>
    None,

    /// <summary>TBPF_INDETERMINATE: busy, with no end known — a run that keeps going across the bar.</summary>
    Indeterminate,

    /// <summary>TBPF_NORMAL: part done, in the accent colour.</summary>
    Normal,

    /// <summary>TBPF_ERROR: stopped by an error, red.</summary>
    Error,

    /// <summary>TBPF_PAUSED: stopped by the person or the app, yellow.</summary>
    Paused,
}

/// <summary>
/// A badge a packaged app set (BadgeUpdateManager): a count, shown as it is up to 99 and as "99+"
/// past that, or one of Windows' status glyphs by name. Windows lets no app draw its own.
/// </summary>
/// <param name="Count">The count when the badge is a number; 0 for a glyph.</param>
/// <param name="Glyph">The status's name as the app wrote it (<c>newMessage</c>, <c>busy</c>…), or null for a count.</param>
public readonly record struct Badge(int Count, string? Glyph)
{
    /// <summary>
    /// The glyphs Windows accepts, and what Karpo draws for each: Segoe Fluent Icons code points
    /// picked for the meaning, since Windows' own badge pictures are images of its own. Keys are
    /// the XML values, compared as Windows compares them (case-insensitively).
    /// </summary>
    private static readonly Dictionary<string, (string Code, Func<string> Spoken)> Glyphs = new(StringComparer.OrdinalIgnoreCase)
    {
        ["activity"] = ("", () => S.DockBadgeActivity),
        ["alarm"] = ("", () => S.DockBadgeAlarm),
        ["alert"] = ("", () => S.DockBadgeAlert),
        ["attention"] = ("", () => S.DockBadgeAttention),
        ["available"] = ("", () => S.DockBadgeAvailable),
        ["away"] = ("", () => S.DockBadgeAway),
        ["busy"] = ("", () => S.DockBadgeBusy),
        ["error"] = ("", () => S.DockBadgeError),
        ["newMessage"] = ("", () => S.DockBadgeNewMessage),
        ["paused"] = ("", () => S.DockBadgePaused),
        ["playing"] = ("", () => S.DockBadgePlaying),
        ["unavailable"] = ("", () => S.DockBadgeUnavailable),
    };

    /// <summary>What the badge shows: the count ("99+" past 99), or the glyph's code point.</summary>
    public string Text => Glyph is { } name && Glyphs.TryGetValue(name, out var glyph)
        ? glyph.Code
        : Count > 99 ? "99+" : Count.ToString(CultureInfo.InvariantCulture);

    /// <summary>Whether <see cref="Text"/> is a glyph of the icon font rather than figures.</summary>
    public bool IsGlyph => Glyph is not null;

    /// <summary>What a screen reader says for it: "3 notifications", "busy".</summary>
    public string Spoken => Glyph is { } name && Glyphs.TryGetValue(name, out var glyph)
        ? glyph.Spoken()
        : Count > 99 ? S.DockBadgeMoreThan99 : S.DockNotificationsCount(Count);

    /// <summary>
    /// The badge a <c>&lt;badge value="…"/&gt;</c> payload sets; null for one that clears it — 0,
    /// <c>none</c>, anything Windows would not show — and for XML that is not a badge.
    /// </summary>
    public static Badge? Parse(string? xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            return null;
        }
        string? value;
        try
        {
            // Windows' own parser, as the toasts beside it in the store are read (ToastXml).
            var document = new XmlDocument();
            document.LoadXml(xml);
            if (document.DocumentElement is not { } badge || !string.Equals(badge.TagName, "badge", StringComparison.Ordinal))
            {
                return null;
            }
            value = badge.GetAttribute("value");
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            // Not XML, or not XML the system's parser takes: no badge.
            return null;
        }
        return Of(value);
    }

    /// <summary>The badge for a <c>value</c> attribute as Windows reads it; null where it shows none.</summary>
    public static Badge? Of(string? value)
    {
        var text = value?.Trim() ?? "";
        if (text.Length == 0 || text.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        if (long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var count))
        {
            return count <= 0 ? null : new Badge((int)Math.Min(count, 100), null);
        }
        return Glyphs.ContainsKey(text) ? new Badge(0, Glyphs.Keys.First(key => key.Equals(text, StringComparison.OrdinalIgnoreCase))) : null;
    }

    /// <summary>
    /// What a folder's button shows for the badges of the apps in it, so a count is seen with the
    /// folder shut: their counts added up ("99+" past 99), or, where none is a count, the first
    /// glyph; null where none has a badge.
    /// </summary>
    public static Badge? Together(IEnumerable<Badge?> badges)
    {
        var count = 0L;
        Badge? glyph = null;
        foreach (var badge in badges)
        {
            if (badge is { IsGlyph: true })
            {
                glyph ??= badge;
            }
            else if (badge is { } counted)
            {
                count += counted.Count;
            }
        }
        return count > 0 ? new Badge((int)Math.Min(count, 100), null) : glyph;
    }
}

/// <summary>
/// What an app button shows beyond its running indicator (see the top of AppSignals.cs): part of its
/// <see cref="Mark"/>, so the bar sets it in place when it changes. Equal exactly when nothing needs
/// drawing again: the overlay's picture is a new object whenever the app sends a new one.
/// </summary>
/// <param name="Progress">The progress to draw where the running indicator is; None for the indicator itself.</param>
/// <param name="Value">How much is done, 0 to 1; only drawn for Normal, Error and Paused.</param>
/// <param name="Overlay">The overlay icon on the icon's lower right; null for none.</param>
/// <param name="OverlayText">The overlay's description, for the button's accessible name; null without one.</param>
/// <param name="Badge">The app's badge, which takes the overlay's corner when both are there.</param>
public readonly record struct TileSignal(
    ProgressKind Progress = ProgressKind.None,
    float Value = 0f,
    Pixels? Overlay = null,
    string? OverlayText = null,
    Badge? Badge = null)
{
    /// <summary>Whether there is anything to draw.</summary>
    public bool Any => ShowsProgress || Overlay is not null || Badge is not null;

    /// <summary>
    /// Whether the progress shows in the indicator's place: a run without an end always, a share
    /// done once something is done. Normal, paused or failed with nothing done is an empty fill,
    /// which no taskbar draws (see the top of AppSignals.cs): the running indicator stays.
    /// </summary>
    public bool ShowsProgress => Progress switch
    {
        ProgressKind.None => false,
        ProgressKind.Indeterminate => true,
        _ => float.IsFinite(Value) && Value > 0f,
    };

    /// <summary>
    /// What a screen reader adds to the button's name: the overlay's description, the badge, and the
    /// progress, in that order ("3 unread messages, 45 % done"). Empty when there is nothing.
    /// </summary>
    public string Spoken
    {
        get
        {
            var parts = new List<string>(3);
            if (Overlay is not null && OverlayText is { Length: > 0 } text && Badge is null)
            {
                parts.Add(text);
            }
            if (Badge is { } badge)
            {
                parts.Add(badge.Spoken);
            }
            var percent = (int)MathF.Round(Math.Clamp(Value, 0f, 1f) * 100f);
            switch (ShowsProgress ? Progress : ProgressKind.None)
            {
                case ProgressKind.Indeterminate:
                    parts.Add(S.DockBadgeWorking);
                    break;
                case ProgressKind.Normal:
                    parts.Add(S.DockBadgeDone(percent));
                    break;
                case ProgressKind.Paused:
                    parts.Add(S.DockBadgePausedAt(percent));
                    break;
                case ProgressKind.Error:
                    parts.Add(S.DockBadgeStoppedAt(percent));
                    break;
            }
            return Dock.Listed(parts);
        }
    }
}

/// <summary>
/// What apps have told the taskbar about their windows, by window. The band (Taskband.cs) writes it
/// on the dock's thread, inside the call that brought the news; the dock reads it on the same thread
/// when the band says there is news. A window that goes takes its signals with it (<see cref="Forget"/>,
/// and <see cref="Prune"/> for one that went unannounced).
/// </summary>
internal sealed class AppSignals
{
    /// <summary>The one the band writes and the bars read. Tests make their own.</summary>
    internal static AppSignals Shared { get; } = new();

    /// <summary>What SetProgressValue's lParam is when all is done: the taskbar list scales every value to it.</summary>
    internal const int ProgressScale = 0xFFFE;

    private readonly Dictionary<nint, Window> windows = [];

    // Each tab's window, by the tab (RegisterTab).
    private readonly Dictionary<nint, nint> tabs = [];

    // Counts every overlay set, so a button of several windows shows the one set last.
    private long stamp;

    /// <summary>How many windows carry anything; for the tests and the log.</summary>
    public int Count => windows.Count;

    /// <summary>
    /// SetProgressState: <paramref name="flags"/> is the TBPF value. No progress clears it; any
    /// other state keeps the value set before (a paused copy still shows how far it got). True when
    /// what the window shows changed.
    /// </summary>
    public bool ProgressState(nint hwnd, uint flags)
    {
        var kind = KindOf(flags);
        if (kind == ProgressKind.None)
        {
            if (!windows.TryGetValue(hwnd, out var gone) || gone.Progress == ProgressKind.None)
            {
                return false;
            }
            gone.Progress = ProgressKind.None;
            gone.Value = 0f;
            Tidy(hwnd, gone);
            return true;
        }
        var window = Of(hwnd);
        if (window.Progress == kind)
        {
            return false;
        }
        window.Progress = kind;
        return true;
    }

    /// <summary>
    /// SetProgressValue, as the band receives it: <paramref name="scaled"/> is the share done, 0 to
    /// <see cref="ProgressScale"/>. A value turns a window with no progress, or one busy without an
    /// end, into one making progress, as the taskbar does (an app that only ever sets values shows
    /// them); a paused or failed one stays so and shows the new value.
    /// </summary>
    public bool ProgressValue(nint hwnd, nint scaled)
    {
        var value = Math.Clamp((float)scaled / ProgressScale, 0f, 1f);
        var window = Of(hwnd);
        var kind = window.Progress is ProgressKind.None or ProgressKind.Indeterminate ? ProgressKind.Normal : window.Progress;
        if (window.Progress == kind && window.Value == value)
        {
            return false;
        }
        window.Progress = kind;
        window.Value = value;
        return true;
    }

    /// <summary>
    /// SetOverlayIcon's picture, already drawn (the app may destroy its icon once the call returns);
    /// null takes the overlay away. The description comes separately (<see cref="OverlayText"/>).
    /// </summary>
    public bool Overlay(nint hwnd, Pixels? picture)
    {
        if (picture is null)
        {
            if (!windows.TryGetValue(hwnd, out var gone) || gone.Overlay is null)
            {
                return false;
            }
            gone.Overlay = null;
            gone.OverlayText = "";
            gone.OverlayStamp = 0;
            Tidy(hwnd, gone);
            return true;
        }
        var window = Of(hwnd);
        window.Overlay = picture;
        window.OverlayStamp = ++stamp;
        return true;
    }

    /// <summary>SetOverlayIcon's description, which follows its picture: the alt text of the overlay.</summary>
    public bool OverlayText(nint hwnd, string text)
    {
        if (!windows.TryGetValue(hwnd, out var window) || window.OverlayText == text)
        {
            return false;
        }
        window.OverlayText = text;
        return true;
    }

    /// <summary>
    /// ThumbBarAddButtons (<paramref name="update"/> false) or ThumbBarUpdateButtons: the toolbar
    /// after <paramref name="sent"/>, whose icons are <paramref name="pictures"/>, drawn
    /// (<see cref="ThumbBar.Merge"/>). An update before any toolbar changes nothing.
    /// </summary>
    public bool ThumbButtons(nint hwnd, IReadOnlyList<ThumbButtonData> sent, IReadOnlyList<Pixels?> pictures, bool update)
    {
        if (update && (!windows.TryGetValue(hwnd, out var known) || known.Buttons is null))
        {
            return false;
        }
        var window = Of(hwnd);
        var buttons = ThumbBar.Merge(window.Buttons, sent, pictures, update);
        if (window.Buttons is { } before && before.SequenceEqual(buttons))
        {
            return false;
        }
        window.Buttons = buttons;
        return true;
    }

    /// <summary>
    /// ThumbBarSetImageList: the pictures of the image list the window's THB_BITMAP buttons take
    /// theirs from, drawn, by index; empty for none. Kept whether or not the toolbar came yet:
    /// apps set the list first, as ThumbBarAddButtons' documentation asks.
    /// </summary>
    public bool ThumbImages(nint hwnd, IReadOnlyList<Pixels?> images)
    {
        var list = images.Count == 0 ? null : images;
        if (list is null && !windows.ContainsKey(hwnd))
        {
            return false;
        }
        var window = Of(hwnd);
        if (window.Images is null && list is null)
        {
            Tidy(hwnd, window);
            return false;
        }
        window.Images = list;
        Tidy(hwnd, window);
        return true;
    }

    /// <summary>SetThumbnailTooltip: what the window's preview says in place of its title; null for its title again.</summary>
    public bool ThumbnailTooltip(nint hwnd, string? text)
    {
        var tip = string.IsNullOrEmpty(text) ? null : text;
        if (tip is null && !windows.ContainsKey(hwnd))
        {
            return false;
        }
        var window = Of(hwnd);
        if (window.Tooltip == tip)
        {
            Tidy(hwnd, window);
            return false;
        }
        window.Tooltip = tip;
        Tidy(hwnd, window);
        return true;
    }

    /// <summary>SetThumbnailClip: the part of the window its preview shows; null for all of it.</summary>
    public bool ThumbnailClip(nint hwnd, ThumbClip? clip)
    {
        if (clip is null && !windows.ContainsKey(hwnd))
        {
            return false;
        }
        var window = Of(hwnd);
        if (window.Clip == clip)
        {
            Tidy(hwnd, window);
            return false;
        }
        window.Clip = clip;
        Tidy(hwnd, window);
        return true;
    }

    /// <summary>What <paramref name="hwnd"/>'s preview shows beyond the window; null when it said nothing of it.</summary>
    public ThumbInfo? ThumbOf(nint hwnd) =>
        windows.TryGetValue(hwnd, out var window) && (window.Buttons is { Count: > 0 } || window.Tooltip is not null || window.Clip is not null)
            ? new ThumbInfo(ThumbBar.WithImages(window.Buttons ?? [], window.Images), window.Tooltip, window.Clip)
            : null;

    // --- Tabs: windows standing in for a window's tabs (RegisterTab and the rest) ---------------

    /// <summary>
    /// RegisterTab: <paramref name="tab"/> — a window of the app's standing in for one of its tabs —
    /// belongs to <paramref name="main"/>, after its other tabs. Its preview is the tab's, not the
    /// main window's.
    /// </summary>
    public bool RegisterTab(nint tab, nint main)
    {
        if (tab == 0 || main == 0 || tab == main)
        {
            return false;
        }
        if (tabs.TryGetValue(tab, out var was))
        {
            if (was == main)
            {
                return false;
            }
            UnregisterTab(tab);
        }
        tabs[tab] = main;
        (Of(main).Tabs ??= []).Add(tab);
        return true;
    }

    /// <summary>UnregisterTab: the tab is gone from its window's previews.</summary>
    public bool UnregisterTab(nint tab)
    {
        if (!tabs.Remove(tab, out var main))
        {
            return false;
        }
        if (windows.TryGetValue(main, out var window) && window.Tabs is { } list)
        {
            list.Remove(tab);
            if (list.Count == 0)
            {
                window.Tabs = null;
            }
            Tidy(main, window);
        }
        return true;
    }

    /// <summary>SetTabOrder: <paramref name="tab"/> moves before <paramref name="before"/>, or to the end for none.</summary>
    public bool TabOrder(nint tab, nint before)
    {
        if (!tabs.TryGetValue(tab, out var main) || !windows.TryGetValue(main, out var window) || window.Tabs is not { } list)
        {
            return false;
        }
        var was = list.IndexOf(tab);
        list.Remove(tab);
        var at = before == 0 ? -1 : list.IndexOf(before);
        list.Insert(at < 0 ? list.Count : at, tab);
        return list.IndexOf(tab) != was;
    }

    /// <summary>The windows standing in for <paramref name="main"/>'s tabs, in their order; none for a window without.</summary>
    public IReadOnlyList<nint> TabsOf(nint main) =>
        windows.TryGetValue(main, out var window) && window.Tabs is { } list ? [.. list] : [];

    /// <summary>The window a tab belongs to; 0 for a window that is no tab.</summary>
    public nint MainOf(nint tab) => tabs.GetValueOrDefault(tab);

    /// <summary>
    /// ITaskbarList::ActivateTab, SetActiveAlt: the window an app marked as the active one without
    /// activating it — its button shows as active (a tab's, its window's) until another window is
    /// activated (<see cref="ActiveAltEnds"/>); 0 for none.
    /// </summary>
    public nint ActiveAlt { get; private set; }

    /// <summary>ActivateTab or SetActiveAlt on <paramref name="hwnd"/>: it, or the window it is a tab of, is marked active. True when that changed.</summary>
    public bool ActiveTab(nint hwnd)
    {
        var marked = MainOf(hwnd) is var main and not 0 ? main : hwnd;
        if (marked == ActiveAlt)
        {
            return false;
        }
        ActiveAlt = marked;
        return true;
    }

    /// <summary>
    /// §619: a window was activated — standing for the button of <paramref name="button"/> (itself, or the
    /// owner of its dialog: <see cref="WindowOwners.ButtonOf(nint, Func{nint, nint}, Func{nint, bool})"/>),
    /// which is a button of its own where <paramref name="listed"/> —: the mark an app made ends, as
    /// Windows' taskbar marks the activated window's button active from then on. It stays where that is
    /// the marked window (its tab's, its dialog's), or where the activated window has no button and
    /// is the marking app's own (<paramref name="sameApp"/>: its full screen). Another window of the
    /// same app with a button ends it — every store app's frame is ApplicationFrameHost's, one process,
    /// and a mark one of them made held every other one's button dark and the marked one lit. True when
    /// it ended.
    /// </summary>
    public bool ActiveAltEnds(nint button, bool listed, bool sameApp)
    {
        if (ActiveAlt == 0 || button == ActiveAlt || (!listed && sameApp))
        {
            return false;
        }
        ActiveAlt = 0;
        return true;
    }

    /// <summary>The window is gone: everything it said goes with it, and as a tab it leaves its window. True when it had said anything.</summary>
    public bool Forget(nint hwnd)
    {
        var tab = UnregisterTab(hwnd);
        if (windows.Remove(hwnd, out var gone) && gone.Tabs is { } owned)
        {
            foreach (var its in owned)
            {
                tabs.Remove(its);
            }
            return true;
        }
        return tab || gone is not null;
    }

    /// <summary>
    /// Drops the windows <paramref name="alive"/> says are gone, for the ones whose end was never
    /// announced. True when any went.
    /// </summary>
    public bool Prune(Func<nint, bool> alive)
    {
        var gone = windows.Keys.Concat(tabs.Keys).Where(hwnd => !alive(hwnd)).Distinct().ToList();
        foreach (var hwnd in gone)
        {
            Forget(hwnd);
        }
        return gone.Count > 0;
    }

    /// <summary>
    /// What a button standing for <paramref name="open"/> — its windows, most recently used first —
    /// shows, with <paramref name="badge"/> being its app's badge (see the top of the file for how
    /// several windows combine).
    /// </summary>
    public TileSignal For(IReadOnlyList<nint> open, Badge? badge = null)
    {
        Window? progress = null;
        Window? overlay = null;
        foreach (var hwnd in open)
        {
            if (!windows.TryGetValue(hwnd, out var window))
            {
                continue;
            }
            // A window with nothing done shows nothing, and does not hide another's progress either.
            if (Shows(window) && (progress is null || Urgency(window.Progress) > Urgency(progress.Progress)))
            {
                progress = window;
            }
            if (window.Overlay is not null && (overlay is null || window.OverlayStamp > overlay.OverlayStamp))
            {
                overlay = window;
            }
        }
        return new TileSignal(
            progress?.Progress ?? ProgressKind.None,
            progress?.Value ?? 0f,
            overlay?.Overlay,
            overlay is { OverlayText.Length: > 0 } described ? described.OverlayText : null,
            badge);
    }

    /// <summary>A TBPF value as the taskbar draws it; of several flags at once, the most urgent.</summary>
    internal static ProgressKind KindOf(uint flags) =>
        (flags & 0x4) != 0 ? ProgressKind.Error
        : (flags & 0x8) != 0 ? ProgressKind.Paused
        : (flags & 0x2) != 0 ? ProgressKind.Normal
        : (flags & 0x1) != 0 ? ProgressKind.Indeterminate
        : ProgressKind.None;

    /// <summary>Whether a window's progress shows at all (<see cref="TileSignal.ShowsProgress"/>).</summary>
    private static bool Shows(Window window) => new TileSignal(window.Progress, window.Value).ShowsProgress;

    /// <summary>Which of a button's windows' progress it shows: the higher, the more it needs the person.</summary>
    private static int Urgency(ProgressKind kind) => kind switch
    {
        ProgressKind.Error => 4,
        ProgressKind.Paused => 3,
        ProgressKind.Normal => 2,
        ProgressKind.Indeterminate => 1,
        _ => 0,
    };

    private Window Of(nint hwnd)
    {
        if (!windows.TryGetValue(hwnd, out var window))
        {
            window = new Window();
            windows[hwnd] = window;
        }
        return window;
    }

    /// <summary>A window that says nothing any more is not kept.</summary>
    private void Tidy(nint hwnd, Window window)
    {
        if (window.Progress == ProgressKind.None
            && window.Overlay is null
            && window.Buttons is null
            && window.Images is null
            && window.Tooltip is null
            && window.Clip is null
            && window.Tabs is null)
        {
            windows.Remove(hwnd);
        }
    }

    private sealed class Window
    {
        public ProgressKind Progress;
        public float Value;
        public Pixels? Overlay;
        public string OverlayText = "";
        public long OverlayStamp;
        public IReadOnlyList<ThumbButton>? Buttons;
        public IReadOnlyList<Pixels?>? Images;
        public string? Tooltip;
        public ThumbClip? Clip;
        public List<nint>? Tabs;
    }
}

/// <summary>The colours of an app button's signals, per look (see the top of AppSignals.cs).</summary>
internal static class SignalInk
{
    // SystemFillColorCaution and SystemFillColorCritical, dark and light; the bar's own attention
    // yellow and amber are the caution pair already.
    private static readonly Color CautionDark = Color.FromArgb(255, 0xFC, 0xE1, 0x00);
    private static readonly Color CautionLight = Color.FromArgb(255, 0x9D, 0x5D, 0x00);
    private static readonly Color CriticalDark = Color.FromArgb(255, 0xFF, 0x99, 0xA4);
    private static readonly Color CriticalLight = Color.FromArgb(255, 0xC4, 0x2B, 0x1C);

    // ControlStrongStrokeColorDefault, the ProgressBar's track: the running indicator's own grey.
    private static readonly Color TrackDark = Color.FromArgb(139, 255, 255, 255);
    private static readonly Color TrackLight = Color.FromArgb(114, 0, 0, 0);

    /// <summary>
    /// AccentFillColorDefault, as the bar's indicators use it: AccentLight2 on dark, AccentDark1 on
    /// light, the accent itself where those are unset; high contrast's highlight colour.
    /// </summary>
    public static Color Accent(in Look look) =>
        look.HighContrast ? Opaque(look.HcHighlight, Color.FromArgb(255, 0x1A, 0xEB, 0xFF))
        : look.Dark ? Pick(look.AccentLight2, Pick(look.Accent, Color.FromArgb(255, 0x99, 0xEB, 0xFF)))
        : Pick(look.AccentDark1, Pick(look.Accent, Color.FromArgb(255, 0x00, 0x5F, 0xB8)));

    /// <summary>What the progress is drawn in: the accent while it runs, caution paused, critical on an error. High contrast has one colour for all.</summary>
    public static Color Progress(ProgressKind kind, in Look look) => look.HighContrast
        ? Accent(look)
        : kind switch
        {
            ProgressKind.Paused => look.Dark ? CautionDark : CautionLight,
            ProgressKind.Error => look.Dark ? CriticalDark : CriticalLight,
            _ => Accent(look),
        };

    /// <summary>The hairline the progress runs along.</summary>
    public static Color Track(in Look look) =>
        look.HighContrast ? Opaque(look.HcText, Color.FromArgb(255, 255, 255, 255)) : look.Dark ? TrackDark : TrackLight;

    /// <summary>The badge's pill: the accent.</summary>
    public static Color BadgeFill(in Look look) => Accent(look);

    /// <summary>The badge's figures or glyph: TextOnAccentFillColorPrimary, black on dark and white on light.</summary>
    public static Color BadgeText(in Look look) =>
        look.HighContrast ? Opaque(look.HcHighlightText, Color.FromArgb(255, 0, 0, 0))
        : look.Dark ? Color.FromArgb(255, 0, 0, 0) : Color.FromArgb(255, 255, 255, 255);

    private static Color Pick(Color preferred, Color fallback) => preferred.A == 0 ? fallback : preferred;

    private static Color Opaque(Color color, Color fallback) =>
        color == default ? fallback : Color.FromArgb(255, color.R, color.G, color.B);
}

/// <summary>
/// Where an app button's signals go, in window px, from the button's highlight and icon as the
/// layout places them (see the top of AppSignals.cs). Every rectangle lands on whole pixels.
/// </summary>
internal static class SignalShape
{
    /// <summary>The overlay's and the badge's height against the icon's: 16 DIPs on the taskbar's 24.</summary>
    public const float CornerShare = 16f / 24f;

    /// <summary>How far past the icon's corner the overlay and the badge reach, in their own heights.</summary>
    public const float Overhang = 1f / 8f;

    /// <summary>An indeterminate run's moving part, in shares of the whole run.</summary>
    public const float RunnerShare = 0.4f;

    /// <summary>
    /// The line a progress runs along: the edge side of <paramref name="highlight"/>, where the
    /// running indicator is (its centre a hairline plus half a thickness in), as long as the
    /// highlight less its corner radius at each end, so it stays inside the rounded highlight; and
    /// <paramref name="thickness"/> across, the indicator's own.
    /// </summary>
    public static RectF Run(RectF highlight, Edge edge, float thickness, float stroke, float radius)
    {
        if (highlight.IsEmpty)
        {
            return default;
        }
        var inset = stroke + thickness / 2f;
        var vertical = edge.IsVertical();
        var length = MathF.Max(thickness, (vertical ? highlight.Height : highlight.Width) - 2f * radius);
        var start = (vertical ? highlight.Y : highlight.X) + radius;
        var across = edge switch
        {
            Edge.Top => highlight.Y + inset,
            Edge.Left => highlight.X + inset,
            Edge.Right => highlight.Right - inset,
            _ => highlight.Bottom - inset,
        };
        var near = Round(across - thickness / 2f);
        return vertical
            ? new RectF(near, Round(start), thickness, Round(length))
            : new RectF(Round(start), near, Round(length), thickness);
    }

    /// <summary>
    /// The hairline track under a run: <paramref name="stroke"/> thick, centred across the run and
    /// as long as it, as a WinUI ProgressBar draws its background.
    /// </summary>
    public static RectF Track(RectF run, bool vertical, float stroke)
    {
        if (run.IsEmpty)
        {
            return default;
        }
        return vertical
            ? new RectF(run.X + Round((run.Width - stroke) / 2f), run.Y, stroke, run.Height)
            : new RectF(run.X, run.Y + Round((run.Height - stroke) / 2f), run.Width, stroke);
    }

    /// <summary>
    /// The part of <paramref name="run"/> done at <paramref name="value"/>: from its start along a
    /// horizontal bar, and from its bottom up a vertical one — the bottom being where text turned
    /// down a vertical bar begins.
    /// </summary>
    public static RectF Done(RectF run, bool vertical, float value)
    {
        if (run.IsEmpty)
        {
            return default;
        }
        var share = Math.Clamp(float.IsFinite(value) ? value : 0f, 0f, 1f);
        if (vertical)
        {
            var length = Round(run.Height * share);
            return new RectF(run.X, run.Bottom - length, run.Width, length);
        }
        return new RectF(run.X, run.Y, Round(run.Width * share), run.Height);
    }

    /// <summary>
    /// Whether <paramref name="signal"/>'s progress takes the running indicator's place along
    /// <paramref name="run"/>: as <see cref="TileSignal.ShowsProgress"/> says, and a share done only
    /// once it fills a pixel of the run — less is an empty fill on screen too, and a bare track
    /// would stand where the indicator should.
    /// </summary>
    public static bool Shows(TileSignal signal, RectF run, bool vertical) =>
        signal.ShowsProgress
        && !run.IsEmpty
        && (signal.Progress == ProgressKind.Indeterminate || !Done(new RectF(0f, 0f, run.Width, run.Height), vertical, signal.Value).IsEmpty);

    /// <summary>The moving part of an indeterminate run: <see cref="RunnerShare"/> of it.</summary>
    public static float RunnerLength(RectF run, bool vertical) =>
        MathF.Max(1f, Round((vertical ? run.Height : run.Width) * RunnerShare));

    /// <summary>The side of the overlay and the height of the badge for an <paramref name="icon"/> px icon.</summary>
    public static float CornerSide(float icon) => MathF.Max(8f, Round(icon * CornerShare));

    /// <summary>
    /// Where something <paramref name="size"/> big sits on <paramref name="iconBox"/>'s lower right
    /// corner, reaching past it by <see cref="Overhang"/> of its height; a wide badge grows to the left.
    /// </summary>
    public static RectF Corner(RectF iconBox, Vector2 size)
    {
        if (iconBox.IsEmpty)
        {
            return default;
        }
        var reach = Round(size.Y * Overhang);
        return new RectF(Round(iconBox.Right + reach - size.X), Round(iconBox.Bottom + reach - size.Y), size.X, size.Y);
    }

    /// <summary>
    /// A badge's pill for text <paramref name="textWidth"/> px wide at <paramref name="side"/> px
    /// high: a circle for one figure or a glyph, longer for more, with a quarter of its height of room
    /// either side of the text.
    /// </summary>
    public static Vector2 BadgeSize(float side, float textWidth) =>
        new(MathF.Max(side, MathF.Ceiling(textWidth + side / 2f)), side);

    private static float Round(float value) => MathF.Round(value, MidpointRounding.AwayFromZero);
}

/// <summary>
/// A badge drawn into its own surface: the accent pill and, on it, the count in semibold figures or
/// the status's glyph from the icon font, centred — the look of Windows 11's taskbar badge, and of
/// Karpo's own count badges (GlyphBadge).
/// </summary>
internal static unsafe class SignalPaint
{
    /// <summary>The figures' size against the pill's height: 10 px on the 16 a 24-px icon gets.</summary>
    public const float TextShare = 10f / 16f;

    /// <summary>A badge laid out: its pill's size in px, and its text's size.</summary>
    public readonly record struct BadgePlan(Vector2 Size, float TextPx);

    /// <summary>How big <paramref name="badge"/> is on a pill <paramref name="side"/> px high.</summary>
    public static BadgePlan Plan(Gfx gfx, Badge badge, float side)
    {
        var px = MathF.Max(6f, MathF.Round(side * TextShare, MidpointRounding.AwayFromZero));
        var width = badge.IsGlyph ? px : TextLine.Template(gfx, badge.Text, px, Weight.Semibold).Width;
        return new BadgePlan(SignalShape.BadgeSize(side, width), px);
    }

    /// <summary>
    /// Where the corner of an icon drawn in <paramref name="icon"/> shows <paramref name="signal"/>'s
    /// badge or, without one, its overlay, as the bar's buttons place it (<see cref="SignalShape.Corner"/>);
    /// empty for neither. The badge's plan comes with it.
    /// </summary>
    public static (RectF Box, BadgePlan? Plan) CornerOf(Gfx gfx, TileSignal signal, RectF icon)
    {
        var side = SignalShape.CornerSide(MathF.Min(icon.Width, icon.Height));
        if (signal.Badge is { } badge)
        {
            var plan = Plan(gfx, badge, side);
            return (SignalShape.Corner(icon, plan.Size), plan);
        }
        return signal.Overlay is null ? (default, null) : (SignalShape.Corner(icon, new Vector2(side, side)), null);
    }

    /// <summary>
    /// Paints <paramref name="signal"/>'s corner on an icon drawn in <paramref name="icon"/>, for what
    /// draws itself rather than through the bar's scene (a pin in a folder, a folder's button): the
    /// same badge or overlay, in the same place, as the bar's buttons show.
    /// </summary>
    public static void Corner(ID2D1DeviceContext* dc, Gfx gfx, TileSignal signal, RectF icon, BadgeInk ink)
    {
        var (box, plan) = CornerOf(gfx, signal, icon);
        if (box.IsEmpty)
        {
            return;
        }
        if (signal.Overlay is { } overlay && plan is null)
        {
            Gfx.DrawPixels(dc, overlay, box.X, box.Y, box.Width);
            return;
        }
        D2D_MATRIX_3X2_F before;
        dc->GetTransform(&before);
        var moved = before;
        moved._31 = (box.X * before._11) + (box.Y * before._21) + before._31;
        moved._32 = (box.X * before._12) + (box.Y * before._22) + before._32;
        dc->SetTransform(&moved);
        try
        {
            Badge(dc, gfx, signal.Badge!.Value, plan!.Value, ink);
        }
        finally
        {
            dc->SetTransform(&before);
        }
    }

    /// <summary>Paints <paramref name="badge"/> as <paramref name="plan"/> lays it out, from the surface's top left.</summary>
    public static void Badge(ID2D1DeviceContext* dc, Gfx gfx, Badge badge, BadgePlan plan, in Look look) =>
        Badge(dc, gfx, badge, plan, BadgeInk.Of(look));

    /// <summary>A badge's pill and what is on it: the accent and the text on it (<see cref="SignalInk"/>).</summary>
    public readonly record struct BadgeInk(DXGI_RGBA Fill, DXGI_RGBA Text)
    {
        /// <summary>On the bar, from its look.</summary>
        public static BadgeInk Of(in Look look) => new(PopupPaint.Ink(SignalInk.BadgeFill(look)), PopupPaint.Ink(SignalInk.BadgeText(look)));

        /// <summary>In a popup, from its theme: the accent and the text on it, as that popup has them.</summary>
        public static BadgeInk Of(FlyoutTheme theme) => new(PopupPaint.Ink(theme.Accent), theme.TextOnAccent);
    }

    private static void Badge(ID2D1DeviceContext* dc, Gfx gfx, Badge badge, BadgePlan plan, BadgeInk inks)
    {
        var size = plan.Size;
        PopupPaint.Fill(dc, PopupPaint.Rect(0f, 0f, size.X, size.Y), size.Y / 2f, inks.Fill);
        var ink = inks.Text;
        dc->SetTextAntialiasMode(D2D1_TEXT_ANTIALIAS_MODE.D2D1_TEXT_ANTIALIAS_MODE_GRAYSCALE);
        ID2D1SolidColorBrush* brush = null;
        TextLine line = default;
        try
        {
            brush = Gfx.Brush(dc, ink);
            if (badge.IsGlyph)
            {
                // A glyph is centred by its ink: the icon font's boxes are not centred on their shapes.
                var layout = gfx.TextLayout(badge.Text, plan.TextPx, Weight.Regular, null, gfx.IconFamily);
                try
                {
                    dc->DrawTextLayout(Gfx.InkOrigin(layout, size.X, size.Y), layout, (ID2D1Brush*)brush, D2D1_DRAW_TEXT_OPTIONS.D2D1_DRAW_TEXT_OPTIONS_NONE);
                }
                finally
                {
                    layout->Release();
                }
                return;
            }
            line = TextLine.Lay(gfx, badge.Text, plan.TextPx, Weight.Semibold, null);
            line.Draw(dc, brush, ink, (size.X - line.Width) / 2f, (size.Y - line.Height) / 2f);
        }
        finally
        {
            line.Release();
            Karpo.Dock.Native.ComUtil.Release(ref brush);
        }
    }
}
