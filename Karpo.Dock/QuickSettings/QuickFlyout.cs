// Karpo's quick settings: the network page (the wired connections and Windows
// 11's Wi-Fi list), Windows 11's Bluetooth and sound pages, the
// project widget's page, each a flyout of its own at
// its island, and the dialog that renames a virtual
// desktop. One popup (FlyoutHost in Menu mode: it takes the focus and a click
// elsewhere ends it) shows whichever page was asked for; they open at
// once, since nothing is started for them, and they are as tall as what they
// show up to the most every flyout has (FlyoutPlacement.MostHeight), after
// which the body scrolls. A page that changes while it stands open is fitted
// again: it grows and shrinks at once, the pointer on it or not (FlyoutHost.Fit).
//
// Each page's service listens to the system only while its page is open, and
// posts what it heard to this window (WM_KARPO_QUICK), so the page reads and
// builds again on the dock's thread, and a post the dock holds back during a
// modal loop still arrives.
//
// KARPO_QUICK_SAMPLES=1 gives every page made-up networks, devices and levels
// and touches no radio, audio device or power setting: for pictures and tests.

using System.Numerics;
using Karpo.Dock.QuickSettings.Controls;
using Karpo.Dock.QuickSettings.Services;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
using Edge = Karpo.Core.Edge;

namespace Karpo.Dock.QuickSettings;

public enum QuickFlyoutAction
{
    None,

    /// <summary>It closed by itself: Escape, a click elsewhere, another window taking the focus.</summary>
    Dismissed,

    /// <summary>A link was chosen ("Network and Internet settings"): it has closed, and the dock opens <see cref="QuickFlyoutEvent.Uri"/>.</summary>
    Launch,

    /// <summary>It closed after doing what it was for (a desktop renamed).</summary>
    Done,

    /// <summary>
    /// quick: a power command was chosen in the power widget's flyout. It has closed, and the dock
    /// runs <see cref="QuickFlyoutEvent.Link"/> through the very code its quick links run.
    /// </summary>
    Power,
}

/// <param name="ByKey">A dismissal that was Escape rather than a press somewhere else.</param>
public readonly record struct QuickFlyoutEvent(
    QuickFlyoutAction Action,
    string? Uri = null,
    QuickLink Link = default,
    bool ByKey = false);

/// <summary>Where the pages' data comes from: Windows, or made-up samples.</summary>
internal sealed class QuickSources
{
    public required Func<IWifiSource> Wifi { get; init; }

    /// <summary>
    /// The wired connections, for the network page beside Wi-Fi: none unless a set of sources says
    /// otherwise, so a test's set shows the Wi-Fi it asked for and nothing of this PC's cables.
    /// </summary>
    public Func<IWiredSource> Wired { get; init; } = static () => new SampleWired();
    public required Func<IBluetoothSource> Bluetooth { get; init; }
    public required Func<IBatterySource> Battery { get; init; }
    public required Func<IDesktopNames> Desktops { get; init; }

    /// <summary>What this PC offers, for the power commands' line: read as its folder opens, as the quick links do.</summary>
    public Func<QuickLinkMachine> Machine { get; init; } = QuickLinkMachine.Read;

    /// <summary>
    /// How the screens are used, for the project page: the samples' unless a set of sources says
    /// otherwise, so no set a test makes can change a real screen.
    /// </summary>
    public Func<IProjectSource> Project { get; init; } = static () => new SampleProjection();

    /// <summary>
    /// The wireless displays, for the cast preset's line, and (<c>false</c>: no looking for
    /// receivers) the cast face: the samples' unless a set says otherwise.
    /// </summary>
    public Func<bool, ICastSource> Cast { get; init; } = static _ => new SampleCast();

    /// <summary>
    /// Nearby sharing, for its line, its face and its switch: the samples' unless a set says
    /// otherwise, so no test changes who may share with this PC.
    /// </summary>
    public Func<INearbySource> Nearby { get; init; } = static () => new SampleNearby();

    /// <summary>
    /// The mobile hotspot, for its page and the hotspot face: the samples' unless a set says
    /// otherwise, so no test reaches the real one.
    /// </summary>
    public Func<IHotspotSource> Hotspot { get; init; } = static () => new SampleHotspot();

    /// <summary>
    /// keyboard: the layouts and the window the person types in, for the keyboard's line: the
    /// samples' unless a set says otherwise, so no test switches a real window's layout.
    /// </summary>
    public Func<IKeyboardSource> Keyboard { get; init; } = static () => new SampleKeyboard();

    /// <summary>Set with KARPO_QUICK_SAMPLES=1.</summary>
    public static bool UseSamples => Environment.GetEnvironmentVariable("KARPO_QUICK_SAMPLES") == "1";

    public static QuickSources Windows { get; } = new()
    {
        // Off the dock's thread: the WLAN service and the Network List Manager answer only when
        // they are done, a second and more while a network is joined (OffThread.cs).
        Wifi = static () => new OffThreadWifi(new WindowsWifi()),
        Wired = static () => new OffThreadWired(new WindowsWired()),
        Bluetooth = static () => new WindowsBluetooth(),
        Battery = static () => new WindowsBattery(),
        Desktops = static () => new WindowsDesktopNames(),
        Project = static () => new WindowsProjection(),
        Cast = static discover => new WindowsCast(discover),
        Nearby = static () => new WindowsNearby(),
        Hotspot = static () => new WindowsHotspot(),
        Keyboard = static () => new WindowsKeyboard(),
    };

    public static QuickSources Samples { get; } = new()
    {
        Wifi = static () => new SampleWifi(),
        Wired = static () => new SampleWired(SampleWired.Office),
        Bluetooth = static () => new SampleBluetooth(),
        Battery = static () => new SampleBattery(),
        Desktops = static () => new SampleDesktopNames(),
        // A made-up PC: it sleeps and hibernates, so every power line shows in the pictures.
        Machine = static () => new QuickLinkMachine(Battery: true, WindowsTerminal: true, Sleep: true, Hibernate: true),
    };
}

public sealed unsafe class QuickFlyout : IDisposable, IQuickHost
{
    /// <summary>
    /// Posted to the flyout's window by a page's service: wParam is what happened (the page's own
    /// numbers), lParam the showing it belongs to.
    /// </summary>
    internal const uint WM_KARPO_QUICK = WM.WM_USER + 0x70;

    /// <summary>The page's own timer on the flyout's window (<see cref="IQuickHost.Wake"/>); the panel's caret has its own.</summary>
    internal const nuint WakeTimer = 0x52;

    /// <summary>
    /// Turns the busy rings while something on the page is busy: 30 a second, started when a build
    /// leaves something busy and stopped the moment nothing is, so a quiet flyout wakes nothing.
    /// </summary>
    internal const nuint SpinTimer = 0x53;

    private readonly FlyoutHost host;
    private readonly QuickSources sources;
    private QuickPanel? panel;
    private QuickPage? page;
    private Gfx? gfx;
    private Look look;
    private float scale = 1;
    private FlyoutPlacement placement;
    private QuickContext? context;
    private Widget? focusNext;
    private bool building;
    private bool ticking;
    private bool disposed;

    // The showing that posts belong to; a post from an earlier one is dropped.
    private int generation;

    /// <param name="dock">Goes into the window's GWLP_USERDATA, so the dock's window procedure routes its messages here.</param>
    public QuickFlyout(Gfx gfx, HINSTANCE instance, nint dock)
        : this(gfx, instance, dock, QuickSources.UseSamples ? QuickSources.Samples : QuickSources.Windows)
    {
    }

    /// <param name="className">The window class; a test registers one with a procedure of its own.</param>
    internal QuickFlyout(Gfx gfx, HINSTANCE instance, nint dock, QuickSources sources, string className = Flyout.ClassName)
    {
        this.sources = sources;
        host = new FlyoutHost(gfx, instance, dock, FlyoutMode.Menu, className) { Taskbar = true, Stratum = Stratum.Popup };
    }

    /// <summary>The window; a new one after Alt+F4 destroyed the last, so read it afresh.</summary>
    public HWND Hwnd => host.Hwnd;

    public bool IsOpen => host.IsOpen;

    /// <summary>The page showing, or null when closed.</summary>
    public QuickKind? Showing => IsOpen ? page?.Kind : null;

    /// <summary>
    /// Builds the open page again: what it shows has changed under it, and it has no service of its
    /// own that would have heard (a switcher's list, whose desktops the dock watches).
    /// </summary>
    public void Refresh() => ((IQuickHost)this).Rebuild();

    /// <summary>What shows, for offscreen pictures and tests.</summary>
    internal QuickPanel? Panel => panel;

    /// <summary>
    /// Opens the page for <paramref name="kind"/> beside <paramref name="anchor"/> (the middle of the island
    /// cell's inner side), away from <paramref name="edge"/>, inside <paramref name="work"/>.
    /// <paramref name="desktop"/> is the desktop to rename, for <see cref="QuickKind.RenameDesktop"/>.
    /// </summary>
    public void Show(Gfx gfx, Look look, float scale, POINT anchor, RECT work, Edge edge, QuickKind kind,
        FlyoutAlign align = FlyoutAlign.Center, Guid desktop = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        // Shown again while open: the last page ends first.
        Closed();
        this.gfx = gfx;
        this.look = look;
        this.scale = scale;
        placement = new FlyoutPlacement(anchor, work, edge, Align: align);
        generation++;
        var next = new QuickContext
        {
            Gfx = gfx,
            Theme = QuickTheme.For(look),
            Scale = scale,
            TextScale = Math.Clamp(look.TextScale > 0 ? look.TextScale : 1, 1, 2.25f),
        };
        // §363: the panel kept through its showings — its layers, and the surfaces of what stays — where it draws as
        // it did; another scale or look, or a window made again after Alt+F4, a panel of its own.
        if (panel is { } kept && (!kept.OnItsWindow || !kept.Context.Same(next)))
        {
            kept.Release();
            panel = null;
        }
        context = panel?.Context ?? next;
        page = kind switch
        {
            // The network, Bluetooth, mobile hotspot, power mode and power commands pages are lines
            // of a folder (QuickPageTile), never flyouts; the sound flyout's regions are lines of the
            // sound preset's (SoundPartTile), and the battery is its own line (BatteryIsland). What
            // is playing is the now-playing line of the media preset (MediaIsland). Cast's, Win+P's
            // as a line and nearby sharing's are lines of the cast and nearby sharing presets.
            QuickKind.RenameDesktop => new RenameDesktopPage(this, sources.Desktops(), desktop),
            // No fall-back: a kind added without a page here would show the rename box instead.
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "No quick-settings page is named for this kind."),
        };
        var fresh = panel ??= new QuickPanel(host, context);
        try
        {
            fresh.Set(Build());
            host.Show(gfx, look, scale, fresh.Size, placement);
            fresh.Attach();
            ApplyFocus();
            fresh.Flush();
            page.Start();
            Turning(fresh.Spinning);
        }
        catch
        {
            Hide();
            throw;
        }
    }

    public void Hide()
    {
        host.Hide();
        Closed();
    }

    /// <summary>
    /// What a message to the flyout's window means. The dock passes every one of them here, then its
    /// default handling; an action other than None means the flyout has closed.
    /// </summary>
    public QuickFlyoutEvent Message(uint msg, WPARAM wParam, LPARAM lParam)
    {
        if (msg == WM_KARPO_QUICK)
        {
            if (IsOpen && page is { } current && (int)(nint)lParam == generation)
            {
                current.Notified((int)(nuint)wParam);
            }
            return Outcome();
        }
        if (msg == WM.WM_TIMER && (nuint)wParam == SpinTimer)
        {
            // One step of every busy ring: only those widgets are painted again.
            if (!IsOpen || panel is not { } turning || !turning.Spin())
            {
                Turning(false);
            }
            return Outcome();
        }
        if (msg == WM.WM_TIMER && (nuint)wParam == WakeTimer)
        {
            // What the page asked to be woken for (a scan that may never report back).
            _ = KillTimer(host.Hwnd, WakeTimer);
            if (IsOpen && page is { } waiting)
            {
                waiting.Notified(0);
            }
            return Outcome();
        }
        var happened = host.Message(msg, wParam, lParam);
        if (happened.Kind == FlyoutHostEventKind.Dismissed)
        {
            Closed();
            // Escape and a press somewhere else both end a popup, and they do not mean the same
            // thing: one is "not this", the other is "none of this". A page that was opened from
            // another page (a rename from a row of the switcher's list) comes back to it for the
            // first and goes quietly for the second, so the two are told apart here rather than
            // guessed at by the dock.
            return new QuickFlyoutEvent(QuickFlyoutAction.Dismissed, ByKey: msg == WM.WM_KEYDOWN);
        }
        if (!IsOpen || panel is null)
        {
            return default;
        }
        panel.Message(msg, wParam, lParam);
        return Outcome();
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }
        disposed = true;
        Closed();
        panel?.Release();
        panel = null;
        host.Dispose();
    }

    // --- What the page asks for --------------------------------------------------

    private QuickFlyoutEvent pendingOutcome;

    /// <summary>What the page's last action came to: a link to open, or done.</summary>
    private QuickFlyoutEvent Outcome()
    {
        var outcome = pendingOutcome;
        pendingOutcome = default;
        return outcome;
    }

    void IQuickHost.Notify(int what)
    {
        var window = host.Hwnd;
        if (window != HWND.NULL)
        {
            _ = PostMessageW(window, WM_KARPO_QUICK, (WPARAM)(nuint)(uint)what, (LPARAM)(nint)generation);
        }
    }

    void IQuickHost.Rebuild()
    {
        if (building || panel is null || !IsOpen)
        {
            return;
        }
        panel.Set(Build());
        panel.Fit();
        ApplyFocus();
        panel.Flush();
        Turning(panel.Spinning);
    }

    /// <summary>Starts or stops the ring timer; nothing ticks while nothing is busy or nothing shows.</summary>
    private void Turning(bool on)
    {
        var window = host.Hwnd;
        if (window == HWND.NULL)
        {
            ticking = false;
            return;
        }
        if (on && IsOpen)
        {
            _ = SetTimer(window, SpinTimer, Spinner.TickMs, null);
            ticking = true;
            return;
        }
        _ = KillTimer(window, SpinTimer);
        ticking = false;
    }

    void IQuickHost.Launch(string uri)
    {
        Hide();
        pendingOutcome = new QuickFlyoutEvent(QuickFlyoutAction.Launch, uri);
    }

    void IQuickHost.Close()
    {
        Hide();
        pendingOutcome = new QuickFlyoutEvent(QuickFlyoutAction.Done);
    }

    void IQuickHost.Power(QuickLink link)
    {
        // The flyout goes first: nothing of Karpo's is left on screen while Windows takes over.
        Hide();
        pendingOutcome = new QuickFlyoutEvent(QuickFlyoutAction.Power, Link: link);
    }

    void IQuickHost.Wake(int milliseconds)
    {
        var window = host.Hwnd;
        if (IsOpen && window != HWND.NULL)
        {
            _ = SetTimer(window, WakeTimer, (uint)Math.Clamp(milliseconds, 50, 60_000), null);
        }
    }

    void IQuickHost.Focus(Widget widget) => focusNext = widget;

    private PanelContent Build()
    {
        building = true;
        try
        {
            var layout = new PageLayout(context!, placement.MostHeight(scale), panel?.Scroll ?? 0);
            page!.Build(layout);
            return layout.Finish();
        }
        finally
        {
            building = false;
        }
    }

    private void ApplyFocus()
    {
        if (focusNext is { } widget && panel is not null && widget.Panel == panel)
        {
            focusNext = null;
            panel.Focus(widget, visible: false);
        }
    }

    /// <summary>The showing ended: the page stops its service, the panel lets go of its surfaces.</summary>
    private void Closed()
    {
        generation++;
        focusNext = null;
        if (host.Hwnd != HWND.NULL)
        {
            // Nothing of a page's waiting or turning outlives its showing.
            _ = KillTimer(host.Hwnd, WakeTimer);
            _ = KillTimer(host.Hwnd, SpinTimer);
        }
        ticking = false;
        // Nothing holds the panel open past its own showing: a menu that was up over it has no
        // panel to hold open any more, and the next showing starts closing for a click elsewhere.
        host.StaysOpen = false;
        var ending = page;
        page = null;
        // §363: kept for the next showing, as it was; its page's widgets go with the next page's.
        panel?.Rest();
        if (ending is not null)
        {
            try
            {
                ending.Dispose();
            }
            catch (Exception error)
            {
                Console.Error.WriteLine($"Karpo: quick settings: {error.Message}");
            }
        }
    }

    /// <summary>The page showing, for tests: what it laid out, and what it was given.</summary>
    internal QuickPage? Page => page;

    /// <summary>The panel's size in px, for tests.</summary>
    internal Vector2 PanelSize => host.PanelSize;

    /// <summary>Whether the ring timer is running, and where the rings stand. For tests.</summary>
    internal bool Ticking => ticking;

    internal float Phase => context?.Spin ?? 0;

    /// <summary>The panel's top-left corner in the window, in px: panel px plus this are client px. For tests.</summary>
    internal Vector2 PanelOrigin => new(host.Panel.X, host.Panel.Y);

    /// <summary>Where the panel itself is on the screen (without the shadow's room around it), for tests.</summary>
    internal RECT PanelOnScreen
    {
        get
        {
            var origin = host.Origin;
            var panelArea = host.Panel;
            var left = origin.x + (int)panelArea.X;
            var top = origin.y + (int)panelArea.Y;
            return new RECT { left = left, top = top, right = left + (int)panelArea.W, bottom = top + (int)panelArea.H };
        }
    }
}
