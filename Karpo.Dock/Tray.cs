// Tray icons while Explorer keeps running.
//
// Apps find the taskbar by class name, so a hidden window of that class, kept
// first in z-order, receives what they send. `Shell_NotifyIcon` data is read
// and then passed on, so Explorer stays in step and nothing is lost when Karpo
// quits. Appbar messages are only passed on. None of this is documented;
// RetroBar's ManagedShell and zebar work the same way.
//
// The window procedure here never touches the dock. Karpo's own appbar calls
// come through it while the dock is busy, and must still reach Explorer.
//
// The spy also carries the TaskbandHWND property, through which the taskbar list
// (ITaskbarList3: progress, overlay icons, thumbnail buttons) finds the window it
// talks to. It names Karpo's own band (Taskband.cs), which keeps what the bars
// draw and passes every call on to Explorer's band; the Rust dock took the
// property from Explorer and passed nothing on. Only where the band can't be made
// does the spy carry Explorer's band itself, as it did before Karpo drew any of it.
// And which icons belong in the overflow comes from the choice Windows 11 keeps
// per icon (Promotions); the Rust dock showed every icon.

using System.Runtime.InteropServices;
using Microsoft.Win32;
using Karpo.Core;
using Karpo.Dock.Native;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

/// <summary>
/// One notification icon. <see cref="Hidden"/> is <b>Windows' own choice</b> for it — the taskbar
/// would keep it in the overflow — and no more than that: whether Karpo's bar shows it is
/// <c>TrayLayout.Split</c>'s answer, which weighs this against the state chosen for it. Nothing that
/// draws the bar may read it again (an icon kept on the bar against Windows' choice was dropped by
/// every such second look).
/// <see cref="Path"/> is its app's executable and <see cref="Record"/> the name of Windows' own
/// record of it (NotifyIconSettings), both written into the list of icons (TrayCatalogue).
/// <see cref="Icon"/> is a new object whenever the app changes the picture, so
/// two snapshots compare equal exactly when nothing needs drawing again.
/// <see cref="ShowTip"/> is NIF_SHOWTIP as its app last said it (<see cref="Tray.ShowsTip"/>).
/// </summary>
public sealed record TrayIcon(
    string Key,
    HWND Owner,
    uint Id,
    Guid Guid,
    uint CallbackMessage,
    uint Version,
    Pixels? Icon,
    string Tip,
    bool Hidden, string Identity = "", bool ShowTip = false, string Path = "", string Record = "")
{
    /// <summary>
    /// Whether the taskbar shows the standard tooltip over it: always before version 4; from version
    /// 4 only where its app asked for it (NIF_SHOWTIP). Without that, such an app draws a popup of its
    /// own when the pointer rests on its icon (NIN_POPUPOPEN), and a tooltip would stand over it.
    /// </summary>
    public bool StandardTip => Version < 4 || ShowTip;
}

public sealed unsafe class Tray : IDisposable
{
    /// <summary>Posted to the dock when <see cref="Icons"/> changed.</summary>
    public const uint WM_KARPO_TRAY = 0x8000 + 3;

    private const string SpyClass = "Shell_TrayWnd";
    private const string NotifyClass = "TrayNotifyWnd";
    private const string BandProperty = "TaskbandHWND";
    private const uint Signature = 0x3475_3423;
    private const uint NIM_ADD = 0;
    private const uint NIM_MODIFY = 1;
    private const uint NIM_DELETE = 2;
    private const uint NIM_SETVERSION = 4;
    private const uint NIF_MESSAGE = 0x01;
    private const uint NIF_ICON = 0x02;
    private const uint NIF_TIP = 0x04;
    private const uint NIF_STATE = 0x08;
    private const uint NIF_GUID = 0x20;
    private const uint NIF_SHOWTIP = 0x80;
    private const uint NIS_HIDDEN = 0x01;

    // What the window procedure works with. Static, not per instance: a window
    // procedure gets no context, and there is one spy at a time.
    internal static readonly List<Entry> Entries = [];
    internal static HWND target;

    // The dock's latest PublishRects, replaced whole: the spy reads it without the dock.
    private static Places? places;
    private static int iconSize = 16;
    private static bool broadcasting;

    private readonly HWND spy;
    private readonly HWND notify;
    private readonly Promotions promotions = new();

    // tray: the place each icon first came in at, so one whose app adds it again keeps its place
    // (TrayArrivals, Karpo.Core).
    private readonly TrayArrivals arrivals = new();

    // tray: a name of its own for each live icon, where two would share one (TrayNames, Karpo.Core).
    private readonly TrayNames names = new();
    private Taskband? band;
    private bool disposed;

    internal Tray(HWND spy, HWND notify)
    {
        this.spy = spy;
        this.notify = notify;
    }

    /// <summary>
    /// True while Karpo itself announces a new taskbar. Every window of this
    /// thread receives that announcement before the call returns, the dock's
    /// included, and the dock's window procedure must ignore it there, before
    /// anything else: taken for an Explorer restart it would announce again.
    /// </summary>
    public static bool Broadcasting => broadcasting;

    /// <summary>
    /// The size, in px, icons are drawn at from the next one an app sends; at least 16. The bar
    /// sets it to the size it draws them (<c>Layout.TrayIconPx</c>, an app icon's 24 DIP on the
    /// 48-DIP bar), because an app hands over one bitmap rather than a group to pick a size from:
    /// whatever it sends is drawn at this — from its own icon group where it was loaded from one,
    /// filtered up or down where not (<see cref="Capture"/>) —, and drawn at any other size
    /// afterwards it softens.
    /// <para>
    /// It takes effect from the next picture an app sends; <see cref="Resize"/> also asks every app
    /// for its picture again.
    /// </para>
    /// </summary>
    public int IconSize
    {
        get => iconSize;
        set => iconSize = Math.Max(16, value);
    }

    /// <summary>
    /// tray: the bars draw their icons at <paramref name="px"/> now (a display, its DPI or a bar's
    /// height changed): pictures are taken at that size from here on, and every app is asked for
    /// its icon again a moment later (TaskbarCreated), once the bars have settled, so the ones
    /// already on the bar are taken at it too rather than kept soft until their app changes them.
    /// Nothing when the size is the same.
    /// </summary>
    public void Resize(int px)
    {
        var next = Math.Max(16, px);
        if (next == iconSize)
        {
            return;
        }
        iconSize = next;
        if (spy != HWND.NULL)
        {
            _ = SetTimer(spy, ResizeTimer, 500, null);
        }
    }

    private const nuint ResizeTimer = 0x4B53;

    /// <summary>
    /// Null when the spy window can't be created. <paramref name="dock"/> is
    /// where <see cref="WM_KARPO_TRAY"/> goes. <paramref name="iconPx"/> is the size to capture
    /// pictures at (<see cref="IconSize"/>), set before the announcement that brings them in.
    /// </summary>
    public static Tray? Start(HWND dock, int iconPx)
    {
        var instance = (HINSTANCE)(void*)GetModuleHandleW(null);
        Register(SpyClass, &SpyProcedure, instance);
        Register(NotifyClass, &PlainProcedure, instance);
        HWND spy;
        HWND notify;
        fixed (char* spyClass = SpyClass)
        fixed (char* notifyClass = NotifyClass)
        {
            // Never shown: only its class and its place in z-order matter.
            spy = CreateWindowExW(
                (uint)(WS.WS_EX_TOPMOST | WS.WS_EX_TOOLWINDOW),
                spyClass,
                null,
                WS.WS_POPUP,
                0,
                0,
                0,
                0,
                HWND.NULL,
                HMENU.NULL,
                instance,
                null);
            if (spy == HWND.NULL)
            {
                return null;
            }
            // Some apps look for this child to place their icon's popups.
            notify = CreateWindowExW(0, notifyClass, null, (uint)WS.WS_CHILD, 0, 0, 0, 0, spy, HMENU.NULL, instance, null);
        }
        target = dock;
        spyWindow = spy;
        var tray = new Tray(spy, notify);
        // Before the announcement, not after it: that is what makes every app re-add its icon, and
        // a picture is captured the moment it arrives.
        tray.IconSize = iconPx;
        tray.band = Taskband.Start(dock, instance);
        tray.TakeBand();
        tray.Announce();
        return tray;
    }

    /// <summary>
    /// The icons to show, in the order they first arrived (Explorer's own order is not
    /// available), dropping any whose app has gone without saying so. Icons an
    /// app hid itself, and icons without a picture, are left out altogether.
    /// An icon whose app takes it away and adds it again, or restarts, keeps the place it first
    /// came in at (<see cref="TrayArrivals"/>): it went to the end before, and moved every time.
    /// <para>
    /// One taken away, hidden by its app or left by a window that went still shows for a moment
    /// (<see cref="TrayPictures.GraceMs"/>), so a delete and an add that follow each other change
    /// nothing on the bar: WhatsApp's icon went and came back whenever a taskbar asked for the
    /// icons (the person, 2026-10-08: "the whatsapp tray icon sometimes disappears and appears
    /// again"). One its app added again under another name meanwhile (a new window) goes at once.
    /// </para>
    /// </summary>
    public IReadOnlyList<TrayIcon> Icons
    {
        get
        {
            var now = Clock();
            foreach (var entry in Entries)
            {
                if (!entry.Deleted && !IsWindow(entry.Owner))
                {
                    Gone(entry);
                }
            }
            Entries.RemoveAll(entry => entry.Deleted && entry.GoneAt is { } since && !TrayPictures.Kept(since, now));
            // tray: two icons that would share a name (two windows of one app, two GUIDs of one
            // app's family) each get one of their own (TrayNames), so each is chosen and placed alone.
            // Every icon its app still has is named, one it hides for a while too, so it keeps its name;
            // Windows' word on each is read first, which looks up the executable the name is made of.
            var promotedAll = Entries.Select(promotions.Promoted).ToList();
            var live = Entries.Where(entry => !entry.Deleted).Select(IdentityOf).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (Entries.RemoveAll(entry => entry.Deleted && live.Contains(IdentityOf(entry))) > 0)
            {
                promotedAll = Entries.Select(promotions.Promoted).ToList();
            }
            Sharpen();
            var names = this.names.Name([.. Entries.Select(entry => (entry.Key, IdentityOf(entry)))]);
            var icons = new List<TrayIcon>(Entries.Count);
            long? next = null;
            for (var at = 0; at < Entries.Count; at++)
            {
                var entry = Entries[at];
                var since = entry.Deleted ? entry.GoneAt : entry.Hidden ? entry.HiddenAt : null;
                if (since is { } from && TrayPictures.Kept(from, now))
                {
                    next = Math.Min(next ?? long.MaxValue, TrayPictures.Left(from, now));
                }
                else if (entry.Hidden)
                {
                    continue;
                }
                if (entry.Pixels is null || entry.Callback == 0)
                {
                    continue;
                }
                var promoted = promotedAll[at];
                icons.Add(new TrayIcon(
                    entry.Key,
                    entry.Owner,
                    entry.Uid,
                    entry.IconGuid ?? Guid.Empty,
                    entry.Callback,
                    entry.Version,
                    entry.Pixels,
                    entry.Tip,
                    !promoted, names[at], entry.ShowTip, entry.Path ?? "", entry.Record));
            }
            // tray: read again when the first grace still running ends, which takes its icon away.
            if (next is { } left)
            {
                Arm(left);
            }
            return arrivals.Order(icons, icon => icon.Identity);
        }
    }

    private static string IdentityOf(Entry entry) => TrayIdentity.Of(entry.Path, entry.Uid, entry.IconGuid);

    // tray: the executables' own icons, for a picture its app made smaller than the bar draws it.
    private readonly ExeIcons exeIcons = new();

    /// <summary>
    /// tray: an icon whose app made its picture smaller than the bar draws it (<see cref="Entry.Native"/>)
    /// is drawn from its executable's own icon at the bar's size where the app's picture is that
    /// icon (<see cref="ExeIcons.Sharper"/>), and stays the app's own, scaled, where it is not: a
    /// badge, a state, a picture of its own. Once per picture, on the dock's thread, never while an
    /// app waits on the spy.
    /// </summary>
    private void Sharpen()
    {
        foreach (var entry in Entries.ToArray())
        {
            if (entry.Native is not { } native || entry.Sharpened || string.IsNullOrEmpty(entry.Path))
            {
                continue;
            }
            entry.Sharpened = true;
            // Read from the shell, which may let another call of the app's in meanwhile; a picture
            // that changed since is the newer one.
            if (exeIcons.Sharper(entry.Path, native, iconSize) is { } sharp && entry.Native == native)
            {
                entry.Pixels = sharp;
            }
        }
    }

    /// <summary>
    /// tray: the order the icons stood in when Karpo last ran (the dock's tray-icons.json), so this
    /// start's adds, which come in whatever order the apps answer, don't shuffle the bar.
    /// </summary>
    public void SeedOrder(IEnumerable<string> identities) => arrivals.Seed(identities);

    /// <summary>
    /// tray: how many times Windows' records (NotifyIconSettings) have been read, for telling a
    /// change to them from an icon merely seen again (<see cref="WindowsChoices"/>).
    /// </summary>
    public int PromotionReads => promotions.Generation;

    /// <summary>
    /// tray: Windows' own record of the icons named <paramref name="identity"/> made to say what
    /// Karpo's choice says — IsPromoted 1 on the taskbar, 0 in the overflow — as dragging an icon
    /// on Windows' own taskbar writes it, so the two agree and Explorer's taskbar shows the same
    /// after Karpo. Only an existing record is written, never a new one made. True when one was.
    /// </summary>
    public bool Promote(string identity, bool shown)
    {
        var wrote = false;
        foreach (var entry in Entries)
        {
            if (string.Equals(names.Of(entry.Key) ?? TrayIdentity.Of(entry.Path, entry.Uid, entry.IconGuid), identity, StringComparison.OrdinalIgnoreCase)
                && promotions.Write(entry.Path, entry.Uid, entry.IconGuid, shown))
            {
                Console.Error.WriteLine($"Karpo tray: IsPromoted={(shown ? 1 : 0)} written for {entry.Key} ({identity})");
                wrote = true;
            }
        }
        return wrote;
    }

    /// <summary>
    /// Explorer restarted: its new taskbar sits above the spy and carries a new
    /// band. Call it on <c>TaskbarCreated</c> when <see cref="Broadcasting"/> is false.
    /// </summary>
    public void Restarted()
    {
        TakeBand();
        Announce();
    }

    /// <summary>
    /// The spy must be the first <c>Shell_TrayWnd</c> apps find. Checked when the
    /// dock shows, after an arrange and when a window comes to the front (Explorer's
    /// taskbar raises itself when shown, and whenever it lays its icons out again),
    /// rather than on a timer, so an idle dock stays asleep. Where it was not first, it
    /// is put first again; every app is asked for its icons once more only by the first
    /// look after they were asked (<see cref="TrayFirst"/>): asked each time, Steam's
    /// answer brought Explorer's taskbar in front again, and its icon came and went
    /// every three seconds.
    /// </summary>
    public void EnsureFirst()
    {
        HWND first;
        fixed (char* name = SpyClass)
        {
            first = FindWindowW(name, null);
        }
        switch (Front.Look(first == spy))
        {
            case TrayLook.Raise:
                Raise(spy);
                break;
            case TrayLook.AskAgain:
                AnnounceFrom(spy);
                break;
        }
    }

    // tray: whether the apps may still be answering the last asking (EnsureFirst); one spy at a time.
    private static readonly TrayFirst Front = new();

    /// <summary>
    /// Where the dock draws each shown icon (by <see cref="TrayIcon.Key"/>) and
    /// the overflow button, in screen px. Apps ask the tray where their icon is
    /// (Shell_NotifyIconGetRect) to place their flyout beside it; Explorer's
    /// answer would be its own hidden taskbar, so the spy answers from this. The
    /// dock publishes after every layout and before a click; the spy only reads
    /// the latest snapshot and never touches the dock.
    /// </summary>
    public void PublishRects(IReadOnlyDictionary<string, RECT> icons, RECT? overflow)
    {
        Volatile.Write(ref places, new Places(new Dictionary<string, RECT>(icons), overflow));
    }

    /// <summary>
    /// A click on the icon, sent whole as the button came up on it within the drag threshold
    /// (<see cref="TrayClicks"/>), as Windows' taskbar sends it: the down — a double click's for the
    /// second of two —, the up, and what version 3 and later expect after it (<see cref="TrayMessages"/>).
    /// Nothing goes as the button goes down, so an icon held to be dragged never opens its app.
    /// </summary>
    internal void Click(TrayClick click, HWND bar) =>
        Deliver(click.Icon, bar, Pointer(click.Icon, null), click.Right ? "click right" : click.Doubled ? "click double" : "click", click.Gestures);

    /// <summary>
    /// keys: the keyboard on the icon (Win+B, TaskbarKeys.cs): Enter or Space selects it, and the menu
    /// key or Shift+F10 asks for its menu — NIN_KEYSELECT and WM_CONTEXTMENU for version 3 and later,
    /// the clicks they stand for before that (<see cref="TrayMessages"/>), as Explorer's tray sends them.
    /// The place they carry is the icon's middle, not the pointer's, which is anywhere: an app's menu
    /// opens at its icon.
    /// </summary>
    public void Key(TrayIcon icon, bool menu, HWND bar) =>
        Deliver(icon, bar, AnchorOf(icon, null), menu ? "menu key" : "key select", menu ? TrayGesture.ContextKey : TrayGesture.KeySelect);

    /// <summary>
    /// What the pointer does over a shown icon (<see cref="TrayHover"/>), sent as the taskbar sends
    /// it, with the pointer's place for version 4. Unlike a click, nothing here opens what the person
    /// asked for, so no app is given the foreground; and only the popup's opening and closing are
    /// logged, not every move.
    /// </summary>
    internal void Hover(TrayIcon icon, TrayGesture gesture)
    {
        if (icon.CallbackMessage == 0 || !IsWindow(icon.Owner))
        {
            return;
        }
        var at = Pointer(icon, null);
        foreach (var (wParam, lParam) in TrayMessages.For(gesture, icon.Version, icon.Id, at))
        {
            bool sent = SendNotifyMessageW(icon.Owner, icon.CallbackMessage, wParam, lParam);
            if (gesture != TrayGesture.Move)
            {
                Console.Error.WriteLine(
                    $"Karpo tray: {(gesture == TrayGesture.PopupOpen ? "popup open" : "popup close")} key={icon.Key} "
                    + $"version={icon.Version} at={at.x},{at.y} {(sent ? "sent" : $"failed {GetLastError()}")}");
            }
        }
    }

    /// <summary>
    /// Press and release at once, for an icon picked from a menu or flyout of the
    /// dock's (the overflow), which already holds the foreground: the whole
    /// sequence a click on Explorer's overflow sends. <paramref name="at"/> is
    /// used when the pointer's place can't be read.
    /// </summary>
    public void Click(TrayIcon icon, bool right, bool doubleClick, POINT at)
    {
        var point = Pointer(icon, at);
        if (right)
        {
            Deliver(icon, HWND.NULL, point, "click right", TrayGesture.RightDown, TrayGesture.RightUp);
        }
        else
        {
            Deliver(icon, HWND.NULL, point, "click", doubleClick ? TrayGesture.LeftDoubleDown : TrayGesture.LeftDown, TrayGesture.LeftUp);
        }
    }

    /// <summary>
    /// Sends what <paramref name="gestures"/> mean for the icon (<see cref="TrayMessages.For"/>) the way the
    /// taskbar does, and logs what happened. The app is allowed to take the foreground, which it
    /// needs to show its menu or flyout. This process may only grant that while
    /// it holds the foreground or just had the click, and the permission lapses
    /// at the next input, so it is granted as the click is sent, its halves together. A
    /// no-activate bar never holds the foreground, so the bar the click was on
    /// takes it first, as Explorer's taskbar does.
    /// </summary>
    private static void Deliver(TrayIcon icon, HWND bar, POINT at, string what, params ReadOnlySpan<TrayGesture> gestures)
    {
        if (icon.CallbackMessage == 0 || !IsWindow(icon.Owner))
        {
            Console.Error.WriteLine(
                $"Karpo tray: {what} {icon.Key}: not sent, {(icon.CallbackMessage == 0 ? "no callback message" : "its window is gone")}");
            return;
        }
        uint process = 0;
        GetWindowThreadProcessId(icon.Owner, &process);
        var foreground = "ours";
        if (!OwnsForeground())
        {
            foreground = bar == HWND.NULL ? "not ours" : SetForegroundWindow(bar) ? "taken" : "refused";
        }
        bool allowed = AllowSetForegroundWindow(process);
        uint allowError = allowed ? 0 : GetLastError();
        var results = new List<string>(4);
        foreach (var gesture in gestures)
        {
            foreach (var (wParam, lParam) in TrayMessages.For(gesture, icon.Version, icon.Id, at))
            {
                bool sent = SendNotifyMessageW(icon.Owner, icon.CallbackMessage, wParam, lParam);
                var message = TrayMessages.EventOf(icon.Version, lParam);
                results.Add(sent ? $"0x{message:X}" : $"0x{message:X} failed {GetLastError()}");
            }
        }
        Console.Error.WriteLine(
            $"Karpo tray: {what} key={icon.Key} owner={(nint)icon.Owner:X} pid={process} callback=0x{icon.CallbackMessage:X} "
            + $"version={icon.Version} at={at.x},{at.y} foreground={foreground} "
            + $"allow={(allowed ? "ok" : $"failed {allowError}")} sent=[{string.Join(", ", results)}]");
    }

    /// <summary>
    /// Where a mouse event on the icon happened, in screen px, for version 4's wParam: the pointer, as
    /// Explorer passes it; the icon's middle on the dock when the pointer can't be read.
    /// </summary>
    private static POINT Pointer(TrayIcon icon, POINT? fallback)
    {
        POINT cursor = default;
        return GetCursorPos(&cursor) ? cursor : AnchorOf(icon, fallback);
    }

    private static bool OwnsForeground()
    {
        uint process = 0;
        GetWindowThreadProcessId(GetForegroundWindow(), &process);
        return process == GetCurrentProcessId();
    }

    /// <summary>The middle of the icon on the dock (or of the overflow button, for an icon in it), in screen px.</summary>
    private static POINT AnchorOf(TrayIcon icon, POINT? fallback)
    {
        if (Volatile.Read(ref places) is { } snapshot)
        {
            RECT? place = snapshot.Icons.TryGetValue(icon.Key, out var shown) ? shown : snapshot.Overflow;
            if (place is { } rect)
            {
                return new POINT { x = (rect.left + rect.right) / 2, y = (rect.top + rect.bottom) / 2 };
            }
        }
        if (fallback is { } at)
        {
            return at;
        }
        POINT cursor = default;
        GetCursorPos(&cursor);
        return cursor;
    }

    /// <summary>
    /// Shell_NotifyIconGetRect's question, WM_COPYDATA 3: where an icon is
    /// (message 1, answered MAKELONG(left, top)) or how big (message 2,
    /// MAKELONG(width, height)). Null when the dock has no answer for that icon,
    /// and Explorer is asked instead.
    /// </summary>
    internal static nint? Place(COPYDATASTRUCT* data)
    {
        if (data->lpData == null || data->cbData < 24)
        {
            return null;
        }
        IconIdentifier question = default;
        Buffer.MemoryCopy(data->lpData, &question, sizeof(IconIdentifier), Math.Min(data->cbData, (uint)sizeof(IconIdentifier)));
        if (question.Magic != Signature || question.Message is not (1 or 2) || Volatile.Read(ref places) is not { } snapshot)
        {
            return null;
        }
        var owner = (HWND)(nint)(nuint)question.Hwnd;
        uint uid = question.Uid;
        Guid? guid = question.Guid != Guid.Empty ? question.Guid : null;
        Entry? found = null;
        foreach (var entry in Entries)
        {
            if (guid is { } id ? entry.IconGuid == id : entry.Owner == owner && entry.Uid == uid)
            {
                found = entry;
                break;
            }
        }
        if (found is null)
        {
            return null;
        }
        RECT rect;
        if (snapshot.Icons.TryGetValue(found.Key, out var shown))
        {
            rect = shown;
        }
        else if (snapshot.Overflow is { } overflow)
        {
            // In the overflow: the button that opens it is where the icon is.
            rect = overflow;
        }
        else
        {
            return null;
        }
        Console.Error.WriteLine(
            $"Karpo tray: place of {found.Key} asked ({question.Message}): {rect.left},{rect.top} {rect.right - rect.left}x{rect.bottom - rect.top}");
        return question.Message == 1
            ? MakeLong(rect.left, rect.top)
            : MakeLong(rect.right - rect.left, rect.bottom - rect.top);
    }

    private static nint MakeLong(int low, int high) => (int)((uint)(ushort)low | ((uint)(ushort)high << 16));

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }
        disposed = true;
        fixed (char* name = BandProperty)
        {
            RemovePropW(spy, name);
        }
        band?.Dispose();
        band = null;
        if (notify != HWND.NULL)
        {
            Surfaces.Gone(notify);
        }
        _ = KillTimer(spy, GraceTimer);
        _ = KillTimer(spy, ResizeTimer);
        Surfaces.Gone(spy);
        spyWindow = HWND.NULL;
        target = HWND.NULL;
        Volatile.Write(ref places, null);
        Entries.Clear();
        promotions.Dispose();
    }

    private static void Register(string name, delegate* unmanaged<HWND, uint, WPARAM, LPARAM, LRESULT> procedure, HINSTANCE instance)
    {
        fixed (char* className = name)
        {
            var type = new WNDCLASSW { lpfnWndProc = procedure, hInstance = instance, lpszClassName = className };
            // Fails when an earlier tray registered it, which is fine: same procedure.
            RegisterClassW(&type);
        }
    }

    /// <summary>
    /// The taskbar list interface (progress, overlay icons, thumbnail buttons,
    /// MarkFullscreenWindow) finds its window through the TaskbandHWND property
    /// of the first Shell_TrayWnd, which is now the spy: Karpo's band, which
    /// passes all of it on to Explorer's (Taskband.cs). Without a band of its own,
    /// the spy carries Explorer's band, and all of it still reaches Explorer.
    /// </summary>
    private void TakeBand()
    {
        fixed (char* name = BandProperty)
        {
            if (band is { } own)
            {
                SetPropW(spy, name, (HANDLE)own.Hwnd.Value);
                return;
            }
            var explorer = Taskbar.ExplorerTray();
            var lent = explorer == HWND.NULL ? HANDLE.NULL : GetPropW(explorer, name);
            if (lent != HANDLE.NULL)
            {
                SetPropW(spy, name, lent);
            }
            else
            {
                RemovePropW(spy, name);
            }
        }
    }

    /// <summary>
    /// Moves the spy to the front and asks every app to add its icons again,
    /// which is the only way to learn about icons added before Karpo started.
    /// </summary>
    private void Announce() => Ask(spy);

    /// <summary><see cref="AnnounceFrom"/>, the first look after it free to ask once more (<see cref="EnsureFirst"/>).</summary>
    private static void Ask(HWND spy)
    {
        Front.Asked();
        AnnounceFrom(spy);
    }

    /// <summary>The spy put first among the <c>Shell_TrayWnd</c>s, where apps look for the tray.</summary>
    private static void Raise(HWND spy) =>
        Surfaces.Topmost(spy);

    private static void AnnounceFrom(HWND spy)
    {
        // appbar: when every app was last asked to hand its icons over again (AppbarShell.Describe).
        Interlocked.Exchange(ref announcedAt, Environment.TickCount64);
        Raise(spy);
        uint created;
        fixed (char* name = "TaskbarCreated")
        {
            created = RegisterWindowMessageW(name);
        }
        broadcasting = true;
        try
        {
            SendNotifyMessageW(HWND.HWND_BROADCAST, created, default, default);
        }
        finally
        {
            broadcasting = false;
        }
    }

    [UnmanagedCallersOnly]
    private static LRESULT PlainProcedure(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam) =>
        DefWindowProcW(hwnd, msg, wParam, lParam);

    [UnmanagedCallersOnly]
    private static LRESULT SpyProcedure(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        // Every app's tray icons and appbar calls come through here (SHAppBarMessage finds this
        // window first), so an app starting is enough to run it: nothing thrown may unwind into
        // user32, where it ends the process. A message that broke answers as a lost one does.
        try
        {
            return Spy(hwnd, msg, wParam, lParam);
        }
        catch (Exception error)
        {
            Troubles.Report("Tray: a message passed on to Explorer", error);
            var data = msg == WM.WM_COPYDATA ? (COPYDATASTRUCT*)(nint)lParam : null;
            return Unreached(msg, appbar: data != null && data->dwData == 0, noExplorer: false);
        }
    }

    private static LRESULT Spy(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        switch (msg)
        {
            case WM.WM_COPYDATA:
                var data = (COPYDATASTRUCT*)(nint)lParam;
                if (data != null && data->dwData == 3)
                {
                    try
                    {
                        if (Place(data) is { } place)
                        {
                            return place;
                        }
                    }
                    catch (Exception)
                    {
                        // Then Explorer answers, as before.
                    }
                }
                if (data != null && data->dwData == 1)
                {
                    bool? answer = null;
                    try
                    {
                        answer = Read(data);
                    }
                    catch (Exception)
                    {
                        // A broken icon must not keep the call from Explorer,
                        // and nothing may unwind into user32.
                    }
                    // Explorer hears every call; where Karpo's tray has its own answer, the app gets that (Read).
                    var explorers = Forward(msg, wParam, lParam);
                    return answer is { } said ? (said ? 1 : 0) : explorers;
                }
                // appbar: dwData 0 is SHAppBarMessage — every app's, the bars' own included, since
                // the spy is the first Shell_TrayWnd it finds. Passed on with its shared memory made
                // for Explorer (ForwardAppbar); one that never reached Explorer comes back as a
                // failure, or the caller takes an ABM_NEW for accepted and a strip for held that
                // Explorer never heard of (AppbarHolder).
                if (data != null && data->dwData == 0)
                {
                    return ForwardAppbar(wParam, data);
                }
                return Forward(msg, wParam, lParam);
            case WM.WM_TIMER when (nuint)wParam == GraceTimer:
                // tray: a grace ended (Arm): the dock reads the icons again, which drops what it kept.
                _ = KillTimer(hwnd, GraceTimer);
                if (target != HWND.NULL)
                {
                    PostMessageW(target, WM_KARPO_TRAY, default, default);
                }
                return 0;
            case WM.WM_TIMER when (nuint)wParam == ResizeTimer:
                // tray: the bars' icon size changed (Resize): every app hands its picture over again.
                _ = KillTimer(hwnd, ResizeTimer);
                Ask(hwnd);
                return 0;
            case WM.WM_WINDOWPOSCHANGING:
                ((WINDOWPOS*)(nint)lParam)->flags &= ~(uint)SWP.SWP_SHOWWINDOW;
                return DefWindowProcW(hwnd, msg, wParam, lParam);
            case WM.WM_ACTIVATEAPP:
            case WM.WM_COMMAND:
                return Forward(msg, wParam, lParam);
            default:
                // Private taskbar messages; registered ones (like TaskbarCreated
                // itself) are not meant for Explorer.
                return msg is >= WM.WM_USER and < 0x8000
                    ? Forward(msg, wParam, lParam)
                    : Closing.Default(hwnd, msg, wParam, lParam);
        }
    }

    /// <summary>
    /// Hands a message to Explorer's taskbar and returns its answer. Bounded, so
    /// a hung Explorer can't hang every app that touches the tray through Karpo.
    /// Appbar messages go through <see cref="ForwardAppbar"/>.
    /// </summary>
    private static LRESULT Forward(uint msg, WPARAM wParam, LPARAM lParam)
    {
        // appbar: how much the spy has passed on, and when last — what a bar's trace says when its
        // strip is missing, to tell a flood of icons after TaskbarCreated from a quiet Explorer.
        Interlocked.Increment(ref forwarded);
        Interlocked.Exchange(ref lastForward, Environment.TickCount64);
        var explorer = Taskbar.ExplorerTray();
        if (explorer == HWND.NULL)
        {
            return Unreached(msg, appbar: false, noExplorer: true);
        }
        if (msg == WM.WM_USER + 372)
        {
            PostMessageW(explorer, msg, wParam, lParam);
            return 0;
        }
        nuint result = 0;
        var sent = SendMessageTimeoutW(explorer, msg, wParam, lParam, (uint)(SMTO_ABORTIFHUNG | SMTO_NORMAL), 4000, &result);
        if (sent.Value == 0)
        {
            return Unreached(msg, appbar: false, noExplorer: false);
        }
        return (nint)result;
    }

    /// <summary>
    /// appbar: an appbar message (WM_COPYDATA 0) passed on to Explorer, with the shared memory a
    /// position, a query or the taskbar's place answers in made again for Explorer's process
    /// (AppbarRelay). SHAppBarMessage made it for this process, whose window it found first, and
    /// passed on as it came, Explorer answered TRUE and did nothing with it: the bars' strips were
    /// asked for through here on a start and neither was taken, and any other app's appbar the same.
    /// One that could not be delivered answers 0, the failure it is: the success a lost icon call
    /// is answered with made a lost ABM_NEW look accepted, so the bars held no space on a start
    /// where Explorer was slow to answer, and nothing noticed until Karpo was started again.
    /// </summary>
    private static LRESULT ForwardAppbar(WPARAM wParam, COPYDATASTRUCT* data)
    {
        Interlocked.Increment(ref forwarded);
        Interlocked.Exchange(ref lastForward, Environment.TickCount64);
        var explorer = Taskbar.ExplorerTray();
        if (explorer == HWND.NULL)
        {
            return Unreached(WM.WM_COPYDATA, appbar: true, noExplorer: true);
        }
        uint process = 0;
        _ = GetWindowThreadProcessId(explorer, &process);
        var answer = AppbarRelay.Pass(data, GetCurrentProcessId(), process, ShellSharedMemory.Instance, passed =>
        {
            nuint result = 0;
            var sent = SendMessageTimeoutW(explorer, WM.WM_COPYDATA, wParam, (LPARAM)(nint)passed, (uint)(SMTO_ABORTIFHUNG | SMTO_NORMAL), 4000, &result);
            return sent.Value == 0 ? null : (nint)result;
        });
        return answer is { } said ? said : Unreached(WM.WM_COPYDATA, appbar: true, noExplorer: false);
    }

    // appbar: the spy's traffic, for the appbar trace (Environment.TickCount64; 0 for never).
    private static int forwarded;
    private static long lastForward;
    private static long announcedAt;

    /// <summary>
    /// appbar: messages the spy has passed on to Explorer since the dock started, when the last one
    /// went, and when every app was last asked to hand its icons over (TaskbarCreated) — in
    /// <see cref="Environment.TickCount64"/> ms, 0 for never. Read on the dock thread for its trace.
    /// </summary>
    internal static (int Forwarded, long LastForward, long AnnouncedAt) Traffic =>
        (Volatile.Read(ref forwarded), Interlocked.Read(ref lastForward), Interlocked.Read(ref announcedAt));

    /// <summary>
    /// What a message that never reached Explorer answers: an appbar message always fails (0), so
    /// its caller can see it and try again; the rest keep the answers they always had — a lost icon
    /// call claims success, since an app that thinks its icon failed tends to stop showing one.
    /// </summary>
    internal static nint Unreached(uint msg, bool appbar, bool noExplorer) =>
        appbar ? 0 : noExplorer ? 1 : msg == WM.WM_COPYDATA ? 1 : 0;

    /// <summary>
    /// Applies one <c>Shell_NotifyIcon</c> call to <see cref="Entries"/>, and says what the app is to
    /// be answered where Karpo's tray has its own answer (null: Explorer's stands).
    /// <para>
    /// A change to an icon this tray cannot click — one it never saw added, so it has no callback
    /// message — is answered as failed. Apps add an icon again where a change fails, and that add
    /// carries the callback; answered as Explorer answers (it has the icon, so the change worked),
    /// the app never adds it again. Windows Security's icon is such an app: on the TaskbarCreated
    /// Karpo announces at its start it only changes its icon, so its shield, and the "action
    /// recommended" badge on it, never showed on Karpo's bar. Explorer still hears the change.
    /// </para>
    /// <para>
    /// Only until the add has come: from then on a change is Explorer's to answer, callback or
    /// not. An app that tries again whenever a call fails, and adds its icon without a callback,
    /// would otherwise have every change refused and add it again for ever.
    /// </para>
    /// <para>
    /// An add Karpo took is answered as done, whatever Explorer says: Explorer refuses to add an
    /// icon it already has, which is exactly the add a refused change brings.
    /// </para>
    /// </summary>
    internal static bool? Read(COPYDATASTRUCT* data)
    {
        uint size = data->cbData;
        if (data->lpData == null || size < 32)
        {
            return null;
        }
        TrayData payload = default;
        Buffer.MemoryCopy(data->lpData, &payload, sizeof(TrayData), Math.Min(size, (uint)sizeof(TrayData)));
        if (payload.Signature != Signature)
        {
            return null;
        }
        return Apply(payload);
    }

    /// <summary><see cref="Read"/>, for one call's data already copied out.</summary>
    internal static bool? Apply(TrayData payload)
    {
        bool? answer = null;
        var owner = (HWND)(nint)(nuint)payload.Hwnd;
        Guid? guid = (payload.Flags & NIF_GUID) != 0 && payload.Guid != Guid.Empty ? payload.Guid : null;
        var probe = new Entry(owner, payload.Uid, guid);
        int index = Entries.FindIndex(entry => entry.Same(probe));
        // tray: an icon its app took away is kept for a moment (Gone), but it is no longer there for
        // anything but an add, which brings it back where it stood. A change to it is refused, as one
        // to an icon never added is (Answer), so the app adds it again; Explorer has none either.
        if (index >= 0 && Entries[index].Deleted && payload.Message != NIM_ADD)
        {
            return payload.Message == NIM_MODIFY ? false : null;
        }
        bool changed = false;
        switch (payload.Message)
        {
            case NIM_ADD:
            case NIM_MODIFY:
                Entry icon;
                if (index >= 0)
                {
                    icon = Entries[index];
                    if (icon.Deleted)
                    {
                        // tray: taken away and added again within the grace (H.NotifyIcon's delete and
                        // add on every TaskbarCreated): the same icon, where it stood, as a new add.
                        icon.Deleted = false;
                        icon.GoneAt = null;
                        icon.Version = 0;
                    }
                    // tray: a GUID names the icon by itself, and an app may leave the window out of
                    // every call after its add (H.NotifyIcon's, which WhatsApp's packaged app uses,
                    // changes its state, picture and tooltip by GUID alone). Only a window is taken as
                    // the icon's new one: taken from such a call, the window was none, and the icon
                    // was dropped as its app's gone (Icons) the moment it was made visible.
                    if (OwnerChanged(icon.Owner, owner)) { icon.Owner = owner; icon.Uid = payload.Uid; icon.Path = null; icon.Generation = -1; }
                }
                else if (payload.Message == NIM_MODIFY && (payload.Flags & NIF_ICON) == 0)
                {
                    // An icon from before Karpo started, changing before its app
                    // has re-added it: only worth keeping with a picture. Failed, so
                    // the app adds it again.
                    return false;
                }
                else
                {
                    icon = probe;
                    Entries.Add(icon);
                }
                if ((payload.Flags & NIF_MESSAGE) != 0)
                {
                    icon.Callback = payload.Callback;
                }
                if ((payload.Flags & NIF_ICON) != 0)
                {
                    // Drawn now: the app may destroy the icon as soon as the
                    // call returns.
                    var (picture, native) = Capture((HICON)(nint)(nuint)payload.Icon, iconSize);
                    if (picture is not null)
                    {
                        icon.Pixels = picture;
                        icon.Native = native;
                        icon.Sharpened = false;
                    }
                }
                if ((payload.Flags & NIF_TIP) != 0)
                {
                    var tip = new ReadOnlySpan<char>(payload.Tip, 128);
                    int end = tip.IndexOf('\0');
                    icon.Tip = new string(end < 0 ? tip : tip[..end]);
                }
                if (ShowsTip(payload.Message, payload.Flags) is { } showTip)
                {
                    icon.ShowTip = showTip;
                }
                if ((payload.Flags & NIF_STATE) != 0 && (payload.StateMask & NIS_HIDDEN) != 0)
                {
                    Hide(icon, (payload.State & NIS_HIDDEN) != 0);
                }
                else if (payload.Message == NIM_ADD && icon.Hidden)
                {
                    // An add without a state is a visible icon, whatever the one taken away was.
                    Hide(icon, false);
                }
                if (payload.Message == NIM_ADD)
                {
                    icon.Added = true;
                }
                changed = true;
                answer = Answer(payload.Message, icon.Added, icon.Callback != 0);
                break;
            case NIM_DELETE:
                if (index >= 0)
                {
                    // tray: kept where it stands for a moment (TrayPictures.GraceMs), so an app that
                    // takes its icon away and adds it again at once doesn't make it flicker; gone
                    // after that (Icons), when the spy's timer wakes the dock.
                    Gone(Entries[index]);
                    changed = true;
                }
                break;
            case NIM_SETVERSION:
                if (index >= 0)
                {
                    Entries[index].Version = payload.Version;
                    changed = true;
                }
                break;
        }
        if (changed && target != HWND.NULL)
        {
            PostMessageW(target, WM_KARPO_TRAY, default, default);
        }
        return answer;
    }

    /// <summary>
    /// tray: the icon's app hid it, or showed it again. One that showed and is hidden keeps showing
    /// for the grace (<see cref="TrayPictures.GraceMs"/>), as one taken away does: H.NotifyIcon adds
    /// its icon hidden and shows it right after, on every TaskbarCreated. One hidden from its add on
    /// never showed, and has nothing to keep.
    /// </summary>
    private static void Hide(Entry icon, bool hidden)
    {
        if (hidden && !icon.Hidden && icon.Added && icon.Pixels is not null && icon.Callback != 0)
        {
            icon.HiddenAt = Clock();
            Arm(TrayPictures.GraceMs);
        }
        else if (!hidden)
        {
            icon.HiddenAt = null;
        }
        icon.Hidden = hidden;
    }

    /// <summary>tray: the icon was taken away, or its window went: kept for the grace from now, then dropped (<see cref="Icons"/>).</summary>
    private static void Gone(Entry icon)
    {
        if (icon.Deleted)
        {
            return;
        }
        icon.Deleted = true;
        icon.GoneAt = Clock();
        Arm(TrayPictures.GraceMs);
    }

    /// <summary>
    /// tray: the clock the grace is measured on, in ms (<see cref="Environment.TickCount64"/>); the
    /// tests set one of their own.
    /// </summary>
    internal static Func<long> Clock = static () => Environment.TickCount64;

    /// <summary>
    /// tray: the spy's timer, so the dock reads the icons again when a grace ends
    /// (<see cref="WM_KARPO_TRAY"/>): a timer, not a wait, so nothing keeps the dock busy meanwhile.
    /// None without a spy (the tests).
    /// </summary>
    private static void Arm(long ms)
    {
        if (spyWindow != HWND.NULL)
        {
            _ = SetTimer(spyWindow, GraceTimer, (uint)Math.Clamp(ms + 20, 20, int.MaxValue), null);
        }
    }

    private const nuint GraceTimer = 0x4B54;

    // tray: the spy window, for its timer (Arm); there is one spy at a time.
    private static HWND spyWindow;

    /// <summary>
    /// tray: whether a call for a known icon moves it to another window: one that names a window, and
    /// another than it had (an app restarted, its GUID icon added from its new window). A call
    /// without one (hWnd 0, the GUID alone naming the icon) leaves it where it is. Pure.
    /// </summary>
    internal static bool OwnerChanged(HWND had, HWND said) => said != HWND.NULL && said != had;

    /// <summary>
    /// How an add or a change Karpo's tray took is answered (null: Explorer's answer stands). An add
    /// with a callback message is done, whatever Explorer says. A change to an icon that has neither
    /// been added nor given a callback (which it needs to be clicked, and shown: Icons) fails, so the
    /// app adds it; once the add has come, a change is Explorer's to answer like any other. Pure.
    /// </summary>
    internal static bool? Answer(uint message, bool added, bool hasCallback) =>
        message == NIM_ADD ? (hasCallback ? true : null)
        : !added && !hasCallback ? false
        : null;

    /// <summary>
    /// What one <c>Shell_NotifyIcon</c> call says of the standard tooltip (NIF_SHOWTIP,
    /// <see cref="TrayIcon.StandardTip"/>): an add says it whether the flag is there or not; a change
    /// says it where it is about the tooltip (NIF_TIP or NIF_SHOWTIP); a change of anything else — a
    /// new picture, a callback — says nothing (null), and the icon keeps what it had, so an app that
    /// asked for the tooltip once does not lose it by changing its picture.
    /// </summary>
    internal static bool? ShowsTip(uint message, uint flags) =>
        message is NIM_ADD || (message is NIM_MODIFY && (flags & (NIF_TIP | NIF_SHOWTIP)) != 0)
            ? (flags & NIF_SHOWTIP) != 0
            : null;

    /// <summary>
    /// Draws the icon on black and on white. Premultiplied colour is the black
    /// result, and alpha is what the white background did not add; that works the
    /// same for old masked icons and for icons with an alpha channel.
    /// </summary>
    internal static Pixels? Render(HICON icon, int size) => Capture(icon, size).Picture;

    /// <summary>
    /// tray: <paramref name="icon"/> at <paramref name="size"/> px (<see cref="Render"/>), and the
    /// app's own picture at the size it made it where that is smaller (<c>Native</c>, null otherwise),
    /// for a sharper source to be looked for later (<see cref="ExeIcons"/>).
    /// <para>
    /// An icon made at the size asked for is drawn as it is. One made at another size is looked for at
    /// that size in its app's own icon group first (<see cref="Matched"/>), the image the shell would
    /// pick. One that has no group (made in memory, as most apps' tray icons are, often the small
    /// icon for the display's DPI: 24 px at 150 %, where the bar draws 36) is drawn at its own size
    /// and scaled with a cubic filter (<see cref="TrayPictures.Resize"/>). DrawIconEx scaled it
    /// before, which copies pixels: the bar's tray icons were blocky, "drawn at very low resolution".
    /// </para>
    /// </summary>
    internal static (Pixels? Picture, Pixels? Native) Capture(HICON icon, int size)
    {
        if (icon == HICON.NULL)
        {
            return (null, null);
        }
        if (Side(icon) is not { } side || side == size || side <= 0 || side > 256)
        {
            return (Paint(icon, size), null);
        }
        var matched = Matched(icon, size);
        if (matched != HICON.NULL)
        {
            try
            {
                return (Paint(matched, size), null);
            }
            finally
            {
                DestroyIcon(matched);
            }
        }
        if (Paint(icon, side) is not { } own)
        {
            return (null, null);
        }
        var picture = new Pixels { Data = TrayPictures.Resize(own.Data, side, size), Width = size, Height = size };
        return (picture, side < size ? own : null);
    }

    private static Pixels? Paint(HICON icon, int size)
    {
        if (Draw(icon, size, 0) is not { } black || Draw(icon, size, 255) is not { } white)
        {
            return null;
        }
        var data = new byte[black.Length];
        bool any = false;
        for (int i = 0; i + 3 < data.Length; i += 4)
        {
            var alpha = (byte)(255 - Math.Clamp(white[i + 1] - black[i + 1], 0, 255));
            data[i] = Math.Min(black[i], alpha);
            data[i + 1] = Math.Min(black[i + 1], alpha);
            data[i + 2] = Math.Min(black[i + 2], alpha);
            data[i + 3] = alpha;
            any |= alpha != 0;
        }
        if (!any)
        {
            return null;
        }
        Items.Premultiply(data);
        return new Pixels { Data = data, Width = size, Height = size };
    }

    /// <summary>The side <paramref name="icon"/> was made at, in px; null where it can't be read.</summary>
    private static int? Side(HICON icon)
    {
        ICONINFO info;
        if (!GetIconInfo(icon, &info))
        {
            return null;
        }
        try
        {
            BITMAP bitmap;
            var picture = info.hbmColor != HBITMAP.NULL ? info.hbmColor : info.hbmMask;
            if (picture == HBITMAP.NULL || GetObjectW((HGDIOBJ)picture.Value, sizeof(BITMAP), &bitmap) == 0)
            {
                return null;
            }
            return bitmap.bmWidth;
        }
        finally
        {
            if (info.hbmColor != HBITMAP.NULL)
            {
                DeleteObject((HGDIOBJ)info.hbmColor.Value);
            }
            if (info.hbmMask != HBITMAP.NULL)
            {
                DeleteObject((HGDIOBJ)info.hbmMask.Value);
            }
        }
    }

    /// <summary>
    /// tray: <paramref name="icon"/> at <paramref name="size"/> px from its app's own icon group, where
    /// the app loaded it from a resource (LoadIcon, LoadIconMetric, LoadImage with LR_SHARED, which
    /// leave the file and the resource's name on the icon): the group's image nearest that size, as
    /// the shell picks one. Null for an icon made in memory, which has only the one picture. The file
    /// is opened as data only, never run, and closed again at once. Yours to destroy.
    /// </summary>
    private static HICON Matched(HICON icon, int size)
    {
        const uint ImageIcon = 1;
        const uint AsData = 0x02 | 0x20; // LOAD_LIBRARY_AS_DATAFILE | LOAD_LIBRARY_AS_IMAGE_RESOURCE
        var info = new ICONINFOEXW { cbSize = (uint)sizeof(ICONINFOEXW) };
        if (!GetIconInfoExW(icon, &info))
        {
            return HICON.NULL;
        }
        if (info.hbmColor != HBITMAP.NULL)
        {
            DeleteObject((HGDIOBJ)info.hbmColor.Value);
        }
        if (info.hbmMask != HBITMAP.NULL)
        {
            DeleteObject((HGDIOBJ)info.hbmMask.Value);
        }
        var file = (char*)&info.szModName;
        var named = (char*)&info.szResName;
        if (file[0] == '\0' || (info.wResID == 0 && named[0] == '\0'))
        {
            return HICON.NULL;
        }
        var module = LoadLibraryExW(file, HANDLE.NULL, AsData);
        if (module == HMODULE.NULL)
        {
            return HICON.NULL;
        }
        try
        {
            var resource = named[0] != '\0' ? named : (char*)(nuint)info.wResID;
            return (HICON)(nint)LoadImageW((HINSTANCE)(nint)module.Value, resource, ImageIcon, size, size, 0).Value;
        }
        finally
        {
            FreeLibrary(module);
        }
    }

    private static byte[]? Draw(HICON icon, int size, byte background)
    {
        var dc = CreateCompatibleDC(HDC.NULL);
        var header = new BITMAPINFO
        {
            bmiHeader = new BITMAPINFOHEADER
            {
                biSize = (uint)sizeof(BITMAPINFOHEADER),
                biWidth = size,
                biHeight = -size,
                biPlanes = 1,
                biBitCount = 32,
                biCompression = (uint)BI.BI_RGB,
            },
        };
        void* bits = null;
        var bitmap = CreateDIBSection(dc, &header, (uint)DIB_RGB_COLORS, &bits, HANDLE.NULL, 0);
        if (bitmap == HBITMAP.NULL || bits == null)
        {
            DeleteDC(dc);
            return null;
        }
        var pixels = new Span<byte>(bits, size * size * 4);
        pixels.Fill(background);
        var previous = SelectObject(dc, (HGDIOBJ)bitmap.Value);
        bool drawn = DrawIconEx(dc, 0, 0, icon, size, size, 0, HBRUSH.NULL, (uint)DI.DI_NORMAL);
        var copy = drawn ? pixels.ToArray() : null;
        SelectObject(dc, previous);
        DeleteObject((HGDIOBJ)bitmap.Value);
        DeleteDC(dc);
        return copy;
    }

    /// <summary>What the spy knows of one icon; changed in place as its app sends updates.</summary>
    internal sealed class Entry(HWND owner, uint uid, Guid? iconGuid)
    {
        public HWND Owner { get; set; } = owner;
        public uint Uid { get; set; } = uid;
        public Guid? IconGuid { get; } = iconGuid;
        public string Key { get; } = iconGuid is { } guid ? guid.ToString("B") : $"{(nint)owner:X}/{uid}";
        public uint Callback;
        public uint Version;
        public string Tip = "";

        /// <summary>An add for it has come (NIM_ADD), so its changes are no longer refused (<see cref="Answer"/>).</summary>
        public bool Added;

        /// <summary>NIF_SHOWTIP, as the app last said it (<see cref="ShowsTip"/>): a version 4 icon that wants the standard tooltip.</summary>
        public bool ShowTip;
        public Pixels? Pixels;

        /// <summary>NIS_HIDDEN: the app itself hid the icon, which then shows nowhere.</summary>
        public bool Hidden;

        /// <summary>tray: when its app hid it (<see cref="Clock"/>), for the moment it still shows (<see cref="Hide"/>); null when it is not hidden or never showed.</summary>
        public long? HiddenAt;

        /// <summary>tray: its app took it away (NIM_DELETE), or its window went: kept from <see cref="GoneAt"/> for the grace (<see cref="Gone"/>).</summary>
        public bool Deleted;

        public long? GoneAt;

        /// <summary>
        /// tray: the app's own picture at the size it made it, where that is smaller than the bar's and
        /// <see cref="Pixels"/> is it scaled up (<see cref="Capture"/>); null otherwise.
        /// <see cref="Sharpened"/> is whether a sharper source has been looked for since (<see cref="ExeIcons"/>).
        /// </summary>
        public Pixels? Native;

        public bool Sharpened;

        /// <summary>The owner's executable once looked up, for <see cref="Promotions"/>.</summary>
        public string? Path;

        public int Generation = -1;
        public bool Promoted;

        /// <summary>The name of Windows' own record of the icon under NotifyIconSettings, empty where it keeps none; read with <see cref="Promoted"/>.</summary>
        public string Record = "";

        public bool Same(Entry other) =>
            IconGuid is { } one && other.IconGuid is { } two
                ? one == two
                : Owner == other.Owner && Uid == other.Uid;
    }

    private sealed record Places(Dictionary<string, RECT> Icons, RECT? Overflow);

    /// <summary>
    /// tray: executables' own icons, read once per executable and size through the shell
    /// (<see cref="Items.Icon"/>, which picks the image nearest the size from the icon group), for
    /// an app that hands the tray a picture smaller than the bar draws it.
    /// </summary>
    internal sealed class ExeIcons(Func<string, int, Pixels?>? load = null)
    {
        private readonly Func<string, int, Pixels?> load = load ?? Items.Icon;
        private readonly Dictionary<(string Path, int Size), Pixels?> known = [];

        /// <summary>
        /// <paramref name="path"/>'s own icon at <paramref name="size"/> px where <paramref name="native"/>,
        /// the app's picture, is smaller and is that icon (<see cref="TrayPictures.Alike"/> with the
        /// executable's icon at the picture's own size); null where it is not, so a picture of the
        /// app's own (a badge, another state) is never swapped for its executable's.
        /// </summary>
        public Pixels? Sharper(string path, Pixels native, int size)
        {
            if (native.Width >= size || native.Width != native.Height)
            {
                return null;
            }
            if (Of(path, native.Width) is not { } same || same.Width != native.Width || same.Height != native.Height
                || !TrayPictures.Alike(same.Data, native.Data))
            {
                return null;
            }
            return Of(path, size) is { } sharp && sharp.Width == size && sharp.Height == size ? sharp : null;
        }

        private Pixels? Of(string path, int size)
        {
            if (!known.TryGetValue((path, size), out var pixels))
            {
                try
                {
                    pixels = load(path, size);
                }
                catch (Exception error) when (error is COMException or IOException or UnauthorizedAccessException or ArgumentException)
                {
                    pixels = null;
                }
                known[(path, size)] = pixels;
            }
            return pixels;
        }
    }

    /// <summary>
    /// NOTIFYICONIDENTIFIERINTERNAL, what Shell_NotifyIconGetRect sends, as
    /// measured from a 64-bit caller: 40 bytes, four DWORDs, then the window 32
    /// bits wide (as in the other tray messages), its uID and its GUID. A 64-bit
    /// window field would read the uID into the window and miss every icon.
    /// </summary>
    internal struct IconIdentifier
    {
        public uint Magic;
        public uint Message;
        public uint Size;
        public uint Padding;
        public uint Hwnd;
        public uint Uid;
        public Guid Guid;
    }

    /// <summary>
    /// What <c>Shell_NotifyIcon</c> sends: 964 bytes, as the Rust declaration.
    /// Handles are 32 bits wide even from 64-bit processes.
    /// </summary>
    internal struct TrayData
    {
        public uint Signature;
        public uint Message;
        public uint Size;
        public uint Hwnd;
        public uint Uid;
        public uint Flags;
        public uint Callback;
        public uint Icon;
        public fixed char Tip[128];
        public uint State;
        public uint StateMask;
        public fixed char Info[256];
        public uint Version;
        public fixed char InfoTitle[64];
        public uint InfoFlags;
        public Guid Guid;
        public uint BalloonIcon;
    }

    /// <summary>
    /// Which icons the user keeps on the taskbar rather than in the overflow
    /// (Settings, Personalization, Taskbar, Other system tray icons). Windows 11
    /// records that per icon under HKCU\Control Panel\NotifyIconSettings: the
    /// icon's executable, where a known folder's GUID may stand for the start of
    /// the path, its uID or GUID, and IsPromoted = 1 when it is shown. No record,
    /// or no IsPromoted, means the overflow, which is where Windows puts a new
    /// icon. The key is watched, so a change in Settings moves the icon at once.
    /// <para>
    /// Windows keys those records by the executable's full path, so a packaged app
    /// that updates (Spotify 1.300.277.0 becoming 1.301.234.0) is a new icon to it
    /// and drops back into the overflow, whatever the person chose before. Karpo
    /// looks the app up by its family as well (<see cref="TrayIdentity"/>): the
    /// record for the exact path wins where it says anything, and where it says
    /// nothing an earlier version of the same app is asked (<see cref="Decide"/>).
    /// </para>
    /// </summary>
    internal sealed class Promotions : IDisposable
    {
        private const string KeyName = @"Control Panel\NotifyIconSettings"; // not-ui: a registry key
        private readonly Dictionary<Guid, string?> folders = [];
        private readonly AutoResetEvent changed = new(false);
        private readonly List<Record> records = [];
        private RegistryKey? key;
        private RegisteredWaitHandle? waiting;
        private int generation;
        private bool loaded;
        private bool missing;

        // Set on a pool thread when the key changed.
        private int stale;

        /// <summary>
        /// One NotifyIconSettings key. <paramref name="Promoted"/> is null when it holds no IsPromoted at all;
        /// <paramref name="Name"/> is the key's own name under NotifyIconSettings, for writing it.
        /// </summary>
        internal readonly record struct Record(string Path, uint? Uid, Guid? Guid, bool? Promoted, string Name = "");

        /// <summary>How many times the records have been read: one more after every change to the key.</summary>
        public int Generation
        {
            get
            {
                Refresh();
                return generation;
            }
        }

        public bool Promoted(Entry entry)
        {
            Refresh();
            if (entry.Generation != generation)
            {
                entry.Generation = generation;
                // tray: a GUID icon's executable too: its name depends on it where the app gives
                // each build a GUID of its own (TrayIdentity.Of), and so does its family's promotion.
                if (entry.Path is null)
                {
                    uint process = 0;
                    GetWindowThreadProcessId(entry.Owner, &process);
                    entry.Path = Apps.ImagePath(process);
                }
                entry.Promoted = Lookup(entry.Path, entry.Uid, entry.IconGuid);
                // tray: Windows' own record of the icon, written into the list of icons (TrayCatalogue).
                var at = RecordFor(records, entry.Path, entry.Uid, entry.IconGuid);
                entry.Record = at >= 0 ? records[at].Name : "";
            }
            return entry.Promoted;
        }

        /// <summary>
        /// Whether Windows keeps this icon on the taskbar (<see cref="Decide"/>).
        /// Without the key at all (before Windows 11) every icon counts as shown.
        /// </summary>
        internal bool Lookup(string? path, uint uid, Guid? guid)
        {
            Refresh();
            return missing || Decide(records, path, uid, guid);
        }

        /// <summary>
        /// Whether <paramref name="records"/> keep this icon on the taskbar. A GUID icon is found by
        /// its GUID, any other by its executable and uID: the record for that exact path decides
        /// where it holds an IsPromoted at all, and where none does, a record of the same app under
        /// another version answers instead (a promotion made before the app updated). Nothing about
        /// the icon at all means the overflow, as on Windows.
        /// </summary>
        internal static bool Decide(IReadOnlyList<Record> records, string? path, uint uid, Guid? guid)
        {
            bool? family = null;
            foreach (var record in records)
            {
                if (guid is { } id)
                {
                    if (record.Guid == id)
                    {
                        if (record.Promoted is { } exact)
                        {
                            return exact;
                        }
                        continue;
                    }
                    // tray: an app that gives each build a GUID of its own (WhatsApp) leaves a record
                    // per build, and the new one says nothing: the GUID icon of another build of the
                    // same app answers, as a versioned path's does below. Only another build — a
                    // path with no version in it is one app with several icons, each its own.
                    if (record.Guid is not null && record.Promoted is { } built && path is not null
                        && !string.Equals(record.Path, path, StringComparison.OrdinalIgnoreCase)
                        && TrayIdentity.SameApp(record.Path, path))
                    {
                        family = (family ?? false) || built;
                    }
                    continue;
                }
                if (record.Uid != uid || path is null || record.Guid is not null)
                {
                    continue;
                }
                if (string.Equals(record.Path, path, StringComparison.OrdinalIgnoreCase))
                {
                    if (record.Promoted is { } exact)
                    {
                        return exact;
                    }
                }
                else if (record.Promoted is { } older && TrayIdentity.SameApp(record.Path, path))
                {
                    // Any version of the app that was kept on the taskbar keeps it there.
                    family = (family ?? false) || older;
                }
            }
            return family ?? false;
        }

        /// <summary>
        /// The record Windows keeps for exactly this icon — by its GUID, or by its executable's full
        /// path and its uID — or -1 where there is none. Another build's record is never it: writing
        /// there would change what Windows says of a build that no longer runs.
        /// </summary>
        internal static int RecordFor(IReadOnlyList<Record> records, string? path, uint uid, Guid? guid)
        {
            for (var at = 0; at < records.Count; at++)
            {
                var record = records[at];
                if (guid is { } id ? record.Guid == id
                    : record.Guid is null && record.Uid == uid && path is not null
                        && string.Equals(record.Path, path, StringComparison.OrdinalIgnoreCase))
                {
                    return at;
                }
            }
            return -1;
        }

        /// <summary>
        /// IsPromoted written into this icon's own record (<see cref="RecordFor"/>), as Settings and
        /// a drag on Windows' taskbar write it. Nothing where there is no record or it already says
        /// so. True when it was written; the key's watch then reads it back.
        /// </summary>
        public bool Write(string? path, uint uid, Guid? guid, bool promoted)
        {
            Refresh();
            var at = RecordFor(records, path, uid, guid);
            if (at < 0 || records[at].Promoted == promoted || records[at].Name.Length == 0)
            {
                return false;
            }
            try
            {
                using var icon = Registry.CurrentUser.OpenSubKey(KeyName + "\\" + records[at].Name, writable: true);
                if (icon is null)
                {
                    return false;
                }
                icon.SetValue("IsPromoted", promoted ? 1 : 0, RegistryValueKind.DWord);
                records[at] = records[at] with { Promoted = promoted };
                return true;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                Console.Error.WriteLine($"Karpo tray: IsPromoted not written: {error.Message}");
                return false;
            }
        }

        public void Dispose()
        {
            waiting?.Unregister(null);
            waiting = null;
            // Closing the key also ends a notification still pending on it.
            key?.Dispose();
            key = null;
            changed.Dispose();
        }

        private void Refresh()
        {
            if (loaded && Interlocked.Exchange(ref stale, 0) == 0)
            {
                return;
            }
            loaded = true;
            generation++;
            records.Clear();
            try
            {
                key ??= Registry.CurrentUser.OpenSubKey(KeyName, writable: false);
                missing = key is null;
                if (key is null)
                {
                    return;
                }
                foreach (var name in key.GetSubKeyNames())
                {
                    using var icon = key.OpenSubKey(name, writable: false);
                    if (icon is null)
                    {
                        continue;
                    }
                    var path = Expand(icon.GetValue("ExecutablePath") as string);
                    uint? uid = icon.GetValue("UID") is int number ? unchecked((uint)number) : null;
                    Guid? guid = icon.GetValue("IconGuid") is string text && Guid.TryParse(text, out var parsed) ? parsed : null;
                    // No IsPromoted at all is not the same as IsPromoted = 0: a record Windows made
                    // for a path it has never been told about leaves the app's other versions to
                    // answer (see Decide).
                    bool? promoted = icon.GetValue("IsPromoted") is int flag ? flag == 1 : null;
                    records.Add(new Record(path ?? "", uid, guid, promoted, name));
                }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                // Whatever was read stands; the watch below still catches the next change.
            }
            Watch();
        }

        private void Watch()
        {
            if (key is null)
            {
                return;
            }
            waiting?.Unregister(null);
            waiting = null;
            const uint filter = (uint)(REG.REG_NOTIFY_CHANGE_NAME | REG.REG_NOTIFY_CHANGE_LAST_SET | REG.REG_NOTIFY_THREAD_AGNOSTIC);
            var handle = (HKEY)key.Handle.DangerousGetHandle();
            var signal = (HANDLE)changed.SafeWaitHandle.DangerousGetHandle();
            if (RegNotifyChangeKeyValue(handle, BOOL.TRUE, filter, signal, BOOL.TRUE) != NO_ERROR)
            {
                return;
            }
            waiting = ThreadPool.RegisterWaitForSingleObject(
                changed,
                static (state, _) => ((Promotions)state!).Changed(),
                this,
                Timeout.Infinite,
                executeOnlyOnce: true);
        }

        /// <summary>On a pool thread: the next <see cref="Icons"/> reads the key again.</summary>
        private void Changed()
        {
            Interlocked.Exchange(ref stale, 1);
            var window = target;
            if (window != HWND.NULL)
            {
                PostMessageW(window, WM_KARPO_TRAY, default, default);
            }
        }

        /// <summary><c>{GUID}\rest</c>, where the GUID names a known folder, to a full path.</summary>
        private string? Expand(string? path)
        {
            if (path is null || path.Length < 40 || path[0] != '{' || path[37] != '}' || path[38] != '\\'
                || !Guid.TryParse(path.AsSpan(0, 38), out var id))
            {
                return path;
            }
            if (!folders.TryGetValue(id, out var folder))
            {
                char* raw = null;
                var found = SHGetKnownFolderPath(&id, 0, HANDLE.NULL, &raw);
                // The buffer is the caller's to free even when the call fails.
                var text = ComUtil.FromTaskMem(raw);
                folder = found.SUCCEEDED ? text : null;
                folders[id] = folder;
            }
            return folder is null ? path : string.Concat(folder, path.AsSpan(38));
        }
    }
}

/// <summary>What the pointer or the keys did to a tray icon.</summary>
internal enum TrayGesture
{
    LeftDown,

    /// <summary>A second left press within the double-click time.</summary>
    LeftDoubleDown,

    LeftUp,
    RightDown,
    RightUp,

    /// <summary>Enter or Space on an icon with the keyboard focus.</summary>
    KeySelect,

    /// <summary>Shift+F10 or the menu key on an icon with the keyboard focus.</summary>
    ContextKey,

    /// <summary>The pointer moved over the icon.</summary>
    Move,

    /// <summary>The pointer came to rest on the icon: its app may show a popup of its own in place of the tooltip.</summary>
    PopupOpen,

    /// <summary>The pointer left the icon, or pressed it, after <see cref="PopupOpen"/>.</summary>
    PopupClose,
}

/// <summary>
/// The callback messages Explorer sends a notification icon's app, by the version the app asked
/// for (NIM_SETVERSION). Every version gets the mouse messages, WM_MOUSEMOVE included while the
/// pointer moves over the icon. From version 3 (NOTIFYICON_VERSION) a left button's release is
/// followed by NIN_SELECT and a right one's by WM_CONTEXTMENU, and the keyboard sends NIN_KEYSELECT
/// and WM_CONTEXTMENU instead of mouse messages; version 3 is documented to differ from 4 here, but
/// Explorer treats both alike (as ManagedShell, which RetroBar uses, does too). Only version 4 hears
/// NIN_POPUPOPEN when the pointer rests on its icon and NIN_POPUPCLOSE when it goes: the popup such
/// an app draws in place of the standard tooltip. Version 4 (NOTIFYICON_VERSION_4) packs the event
/// into lParam's low word and the icon's id into its high word, and puts the event's point on the
/// screen into wParam (x in the low word); before it, wParam is the id and lParam the event.
/// </summary>
internal static class TrayMessages
{
    /// <summary>NIN_SELECT (WM_USER), and NIN_KEYSELECT (NIN_SELECT | NINF_KEY).</summary>
    public const uint NinSelect = 0x0400;

    public const uint NinKeySelect = 0x0401;

    /// <summary>NIN_POPUPOPEN (WM_USER + 6) and NIN_POPUPCLOSE (WM_USER + 7), for version 4 only.</summary>
    public const uint NinPopupOpen = 0x0406;

    public const uint NinPopupClose = 0x0407;

    /// <summary>The (wParam, lParam) pairs <paramref name="gesture"/> sends, in order.</summary>
    public static List<(nuint WParam, nint LParam)> For(TrayGesture gesture, uint version, uint id, POINT at)
    {
        bool modern = version >= 3;
        uint[] events = gesture switch
        {
            TrayGesture.LeftDown => [WM.WM_LBUTTONDOWN],
            TrayGesture.LeftDoubleDown => [WM.WM_LBUTTONDBLCLK],
            TrayGesture.LeftUp => modern ? [WM.WM_LBUTTONUP, NinSelect] : [WM.WM_LBUTTONUP],
            TrayGesture.RightDown => [WM.WM_RBUTTONDOWN],
            TrayGesture.RightUp => modern ? [WM.WM_RBUTTONUP, WM.WM_CONTEXTMENU] : [WM.WM_RBUTTONUP],
            TrayGesture.KeySelect => modern ? [NinKeySelect] : [WM.WM_LBUTTONDOWN, WM.WM_LBUTTONUP],
            TrayGesture.ContextKey => modern ? [WM.WM_CONTEXTMENU] : [WM.WM_RBUTTONDOWN, WM.WM_RBUTTONUP],
            TrayGesture.Move => [WM.WM_MOUSEMOVE],
            TrayGesture.PopupOpen => version >= 4 ? [NinPopupOpen] : [],
            TrayGesture.PopupClose => version >= 4 ? [NinPopupClose] : [],
            _ => [],
        };
        var messages = new List<(nuint, nint)>(events.Length);
        foreach (var message in events)
        {
            messages.Add(Encode(message, version, id, at));
        }
        return messages;
    }

    /// <summary>One event as the version wants it (see <see cref="TrayMessages"/>).</summary>
    public static (nuint WParam, nint LParam) Encode(uint message, uint version, uint id, POINT at) =>
        version >= 4
            ? (((nuint)(ushort)at.y << 16) | (ushort)at.x, ((nint)(id & 0xFFFF) << 16) | (nint)(message & 0xFFFF))
            : (id, (nint)message);

    /// <summary>The event a lParam carries, for the log.</summary>
    public static uint EventOf(uint version, nint lParam) => version >= 4 ? (uint)(lParam & 0xFFFF) : (uint)lParam;
}

/// <summary>
/// The pointer over one bar's tray icons, as Explorer reports it to their apps: every move over an
/// icon is a <see cref="TrayGesture.Move"/>; coming to rest on one is a
/// <see cref="TrayGesture.PopupOpen"/>, once per visit; and moving off it, leaving the bar or
/// pressing it is a <see cref="TrayGesture.PopupClose"/> for the icon whose popup opened. After a
/// press no popup opens until the pointer has left the icon, as a tooltip doesn't come back after a
/// click. Which messages each version hears is <see cref="TrayMessages.For"/>'s: this only keeps
/// the state, and the dock sends what it returns. Icons are told apart by
/// <see cref="TrayIcon.Key"/>, so an icon that changes its picture is still the same icon.
/// </summary>
internal sealed class TrayHover
{
    private TrayIcon? over;
    private TrayIcon? open;
    private bool pressed;

    /// <summary>The icon under the pointer, if any.</summary>
    public TrayIcon? Over => over;

    /// <summary>
    /// The pointer moved, onto <paramref name="icon"/> or off every icon (null).
    /// <paramref name="entered"/> is true when it came onto an icon it wasn't on, which is when the
    /// dock starts waiting for it to rest.
    /// </summary>
    public IReadOnlyList<(TrayIcon Icon, TrayGesture Gesture)> Moved(TrayIcon? icon, out bool entered)
    {
        entered = false;
        if (icon is null && over is null && open is null)
        {
            return [];
        }
        var sends = new List<(TrayIcon Icon, TrayGesture Gesture)>(2);
        if (icon?.Key != over?.Key)
        {
            Close(sends);
            pressed = false;
            entered = icon is not null;
        }
        // The newest record of the same icon: its window and callback are what the next message goes to.
        over = icon;
        if (icon is not null)
        {
            sends.Add((icon, TrayGesture.Move));
        }
        return sends;
    }

    /// <summary>The pointer came to rest: the icon under it hears <see cref="TrayGesture.PopupOpen"/>, once per visit.</summary>
    public IReadOnlyList<(TrayIcon Icon, TrayGesture Gesture)> Rested()
    {
        if (over is not { } icon || open is not null || pressed)
        {
            return [];
        }
        open = icon;
        return [(icon, TrayGesture.PopupOpen)];
    }

    /// <summary>A button went down on the icon under the pointer: its popup closes, and stays closed while the pointer is on it.</summary>
    public IReadOnlyList<(TrayIcon Icon, TrayGesture Gesture)> Pressed()
    {
        pressed = over is not null;
        if (open is null)
        {
            return [];
        }
        var sends = new List<(TrayIcon Icon, TrayGesture Gesture)>(1);
        Close(sends);
        return sends;
    }

    /// <summary>The pointer left the bar, or the bar stopped reporting it (edit mode, a full screen app).</summary>
    public IReadOnlyList<(TrayIcon Icon, TrayGesture Gesture)> Left() => Moved(null, out _);

    private void Close(List<(TrayIcon Icon, TrayGesture Gesture)> sends)
    {
        if (open is { } shown)
        {
            sends.Add((shown, TrayGesture.PopupClose));
            open = null;
        }
    }
}
