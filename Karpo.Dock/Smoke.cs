// Karpo.exe --smoke: the dock's drawing, its flyouts and its stores, offscreen,
// and then out.
//
// A check that a build, the native AOT one above all, runs what trimming could
// break (Direct2D, composition, the WinRT projections, the JSON contexts), and a
// way to see what the dock costs: on a desktop of its own that nobody sees,
// with a config in a temporary folder, it makes the Direct2D device, builds a
// bar with every kind of island, opens and closes each flyout and reads the
// stores. Then it says how long that took and how much memory it holds.
//
// It never touches what a running dock owns: no appbar, no taskbar, no tray,
// no shell hook, not the dock's mutex, not the person's config or local folder
// (KARPO_CONFIG_DIR points at the temporary one), and not Windows' own
// notification database (the notifications preset's line and bell read a made-up one). Weather and calendar
// read nothing from the network: no place is set, and the calendar's events are
// a made-up list.
//
// --rounds N repeats what the dock does over and over (a rebuild of the bar,
// every flyout, the stores) N times and prints the memory every so often: a
// soak for leaks, where private bytes and the managed heap have to level off.
//
// The widgets' data runs from the start as the dock's does (DataSources): a
// source for every widget the config names. --review adds a layout no display
// shows, full of widgets as the person's review layouts are — folders holding
// every line four times — so their sources are running too. --idle N
// then sits still for N seconds, the dock's timer passing as the dock's does
// (the sources, then the bar's widgets), and says what processor time that
// took and what the process holds after it.

using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Karpo.Core;
using Karpo.Dock.Native;
using TerraFX.Interop.Windows;
using Windows.UI.Composition;
using Windows.UI.Composition.Desktop;
using static TerraFX.Interop.Windows.Windows;
using Color = Windows.UI.Color;
using ICompositorDesktopInterop = TerraFX.Interop.WinRT.ICompositorDesktopInterop;
using IDesktopWindowTarget = TerraFX.Interop.WinRT.IDesktopWindowTarget;
using IDispatcherQueueController = TerraFX.Interop.WinRT.IDispatcherQueueController;

namespace Karpo.Dock;

internal static unsafe class Smoke
{
    private const string BarClass = "karpo.smoke";
    private const uint Dpi = 144;
    private const int ScreenWidth = 2560;
    private const int ScreenHeight = 1440;

    /// <summary>The popups' own handlers, by window, for the window procedure: the dock routes them the same way.</summary>
    private static readonly List<Func<HWND, uint, WPARAM, LPARAM, bool>> Routes = [];

    /// <summary>The islands, for WM_KARPO_ISLAND: token → island.</summary>
    private static readonly Dictionary<nuint, IIsland> Tokens = [];

    private static Gfx? drawing;

    /// <summary>Runs the check; 0 when everything ran, 1 when anything failed.</summary>
    public static int Run(string[] args)
    {
        // A GUI exe has no console: the report goes to the one it was started from.
        if (GetStdHandle(STD.STD_OUTPUT_HANDLE) == HANDLE.NULL)
        {
            _ = AttachConsole(unchecked((uint)-1));
        }
        var rounds = Number(args, "--rounds", 0);
        var every = Number(args, "--every", Math.Max(1, rounds / 10));
        // --only rebuild|flyouts|stores: one of the three per round, to see which one grows;
        // --only tray: new tray pictures painted in place, as a live tray icon makes the dock do.
        var only = Array.IndexOf(args, "--only") is var at && at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
        var review = Array.IndexOf(args, "--review") >= 0;
        var idle = Number(args, "--idle", 0);
        var folder = Path.Combine(Path.GetTempPath(), "karpo-smoke-" + Guid.NewGuid().ToString("N")[..12]);
        Directory.CreateDirectory(folder);
        // Before anything asks Config where things live.
        Environment.SetEnvironmentVariable("KARPO_CONFIG_DIR", folder);
        // Quick settings on made-up data: the flyout's pages get made-up networks, devices and
        // levels. The bar's own one-button widgets are made-up for a different reason and by a
        // different seam — QuickSwitches.For gives any isolated run switches with nothing to
        // reach — so nothing this pass draws reads a radio, an audio device or a power setting.
        // Two steps below read Windows' own on purpose and read only: Bluetooth and Switches.
        Environment.SetEnvironmentVariable("KARPO_QUICK_SAMPLES", "1");
        var report = new StringBuilder();
        Exception? failed = null;
        HDESK hidden = default;
        var thread = new Thread(() =>
        {
            try
            {
                OnHiddenDesktop(desktop => hidden = desktop, () => Check(folder, rounds, every, only, review, idle, report));
            }
            catch (Exception error)
            {
                failed = error;
            }
        });
        thread.Start();
        thread.Join();
        // Closed here, once the thread on it has ended: a thread can't close its own (HiddenDesktop).
        try
        {
            HiddenDesktop.Close(hidden);
        }
        catch (NativeException error)
        {
            failed ??= error;
        }
        if (HiddenDesktop.LeftToTextServices > 0)
        {
            // Windows' text services made their threads on it, and they stay for the process's life.
            Line(report, "hidden desktop: left until exit, held by Windows' text services");
        }
        // §710: whom this copy was built for, and what that makes of it here, where it has no package: a build
        // of the source is a developer build, the Store's the free version (LicenseBasis.StoreMissing). Read as
        // the dock reads it, so a native build that lost the mark says so before it is packed.
        using (var license = StoreLicense.ForThisProcess())
        {
            Line(report, $"license: {license.Status.Tier} ({license.Status.Basis}), built for {StoreLicense.Channel() ?? "no one in particular"}");
            if (license.CanPurchase)
            {
                // §715: in its package (Karpo.exe --smoke through the package's alias) the Store is asked for Pro's
                // price as ⚙ › Karpo Pro asks it: the one call of the Store that a native build once could not make.
                var asked = license.ProPriceAsync();
                Line(report, asked.Wait(TimeSpan.FromSeconds(20))
                    ? $"store: Pro's price {asked.Result ?? "not told: the Store lists no such add-on (not published yet), or a line above says why"}"
                    : "store: Pro's price not answered in 20 s");
            }
        }
        // §713: whether this build was given Google's client secret, without which it offers no Google account.
        Line(report, $"accounts: Google sign-in {(Karpo.Accounts.AccountSignIn.CanSignIn(Karpo.Accounts.AccountKind.Google) ? "built in" : "left out")}");
        Console.Out.Write(report.ToString());
        try
        {
            Directory.Delete(folder, recursive: true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A store's file still open for a moment: the temporary folder goes later.
        }
        if (failed is not null)
        {
            Console.Out.WriteLine($"smoke: FAILED {failed}");
            return 1;
        }
        Console.Out.WriteLine("smoke: ok");
        return 0;
    }

    /// <summary>
    /// Runs <paramref name="body"/> on this thread, moved to a desktop of its own that nobody
    /// sees, with a dispatcher queue (the compositor needs one) that makes it an STA. The desktop is
    /// handed to <paramref name="made"/> as soon as it exists, for the thread that started this one
    /// to close once this one has ended (<see cref="HiddenDesktop.Close"/>): a thread can't close
    /// the desktop it is on.
    /// </summary>
    private static void OnHiddenDesktop(Action<HDESK> made, Action body)
    {
        HDESK hidden;
        fixed (char* name = "karpo-smoke-" + Guid.NewGuid().ToString("N"))
        {
            hidden = CreateDesktopW(name, null, null, 0, 0x10000000u /* GENERIC_ALL */, null);
        }
        if (hidden == HDESK.NULL)
        {
            throw new NativeException($"CreateDesktopW failed: error {Marshal.GetLastSystemError()}", 0);
        }
        made(hidden);
        // Before this thread owns a window: every window it makes is then made there.
        if (!SetThreadDesktop(hidden))
        {
            throw new NativeException($"SetThreadDesktop failed: error {Marshal.GetLastSystemError()}", 0);
        }
        var options = new DispatcherQueueOptions
        {
            dwSize = (uint)sizeof(DispatcherQueueOptions),
            threadType = DISPATCHERQUEUE_THREAD_TYPE.DQTYPE_THREAD_CURRENT,
            apartmentType = DISPATCHERQUEUE_THREAD_APARTMENTTYPE.DQTAT_COM_STA,
        };
        IDispatcherQueueController* controller = null;
        ComUtil.Check(CreateDispatcherQueueController(options, &controller));
        try
        {
            body();
        }
        finally
        {
            controller->Release();
        }
    }

    private static void Check(string folder, int rounds, int every, string? only, bool review, int idle, StringBuilder report)
    {
        var instance = (HINSTANCE)(void*)GetModuleHandleW(null);
        Register(instance);
        using var gfx = drawing = new Gfx(gpu: false);
        Line(report, $"gfx: WARP device, icon font {gfx.IconFamily}");
        DrawEverything(gfx);

        var (config, placed, pinned) = Sample();
        var look = LookFor(dark: true);
        var bar = MakeWindow(instance);
        DesktopWindowTarget? target = null;
        var islands = new IslandCollection();
        var icons = new Icons();
        Scene? scene = null;
        // A store of its own, with no accounts in it at all: --smoke promises to reach nothing,
        // and an account here would be a request to a real server with a real password.
        var calendarStore = new CalendarStore();
        var notifications = new NotificationStore(
            Database(Path.Combine(folder, "wpn", "wpndatabase.db")),
            Path.Combine(folder, "wpn-copy"),
            Path.Combine(folder, "state"),
            app => (NotificationText.FallbackName(app), Solid(64, 0xFF0F6CBD)));
        try
        {
            target = Target(gfx, bar);
            // The notifications preset's bell and its list read the made-up database, never Windows' own.
            NotificationPartTile.Stand = notifications;
            MakeIslands(gfx, config, placed, bar, islands);
            // As the dock does at its start: a source for every widget the config names, on the bar,
            // in its folders and — with --review — in two more layouts, full of widgets (DataSources).
            var wanted = Wanted(placed, review);
            DataSources.Shared.Want(wanted);
            Line(report, $"sources: {DataSources.Shared.Running.Count} for {wanted.Layouts.Sum(layout => Folders.All(layout.Islands).Count())} widgets on {wanted.Layouts.Count} layouts");
            var content = Content(pinned, islands, round: 0);
            scene = Build(gfx, target, config, placed, look, islands, icons, content, previous: null);
            Line(report, $"scene: {placed.Count} parts, {islands.Count} islands, first bar {Since():F0} ms after the process started");
            Line(report, Memory("after the first scene"));

            calendarStore.Add(new ListCalendar("smoke:events", "Smoke", default, SampleEvents()));
            calendarStore.Open();
            using var flyouts = new Flyouts(gfx, instance, islands);
            flyouts.Run(look, notifications, bar, step: what => Line(report, "  " + Memory("after " + what)));
            Settle(() => notifications.Idle().IsCompleted);
            Line(report, $"flyouts: menu, jump list, grid, strip, notifications, window list, quick settings, folder");
            Line(report, Memory("after one of each flyout"));
            Line(report, "opening, first time: " + Opening(flyouts.Opening, flyouts.Waiting));
            Opened(flyouts.Opening);
            // Once more: what every later click waits for.
            flyouts.Run(look, notifications, bar);
            Line(report, "opening, again: " + Opening(flyouts.Opening, flyouts.Waiting));
            Opened(flyouts.Opening);

            // The commit fence (Surfaces, §354): a window shown in a frame comes up only once the
            // compositor's commit has answered (RequestCommitAsync), a WinRT call this build alone can
            // refuse — then every window of Karpo's would wait for the time limit.
            Line(report, "fence: " + Fence(gfx, instance));

            // The real Bluetooth service, on this PC's own radio and devices. It is here and not
            // in a test because the thing it is here to catch only happens in this build: the
            // published bar is native AOT, and a WinRT call that a JIT-ed test makes happily can
            // fail once ILC has decided what metadata to keep. The panel's own devices are read,
            // nothing is paired, unpaired or switched, and what fails goes to the log with its
            // type and HResult (Karpo.Core\Troubles.cs).
            Line(report, "bluetooth: " + Bluetooth());

            // And the two switches that talk to Windows, which the bar's own no longer do here:
            // QuickSwitches.For hands an isolated run made-up ones, so nothing a test or this
            // pass draws can reach the radios or the network. These are built on purpose instead.
            Line(report, "switches: " + Switches());

            // And the status icons' own reading — the privacy keys, the radio service, RAS, the
            // tethering manager, the connection's cost, the update key — read once and let go.
            Line(report, "status icons: " + StatusIcons());

            // And Security Center. This one matters more than the others: the product list is
            // read through raw vtable slots, so a wrong slot is an access violation in somebody's
            // bar rather than an error code, and nothing but a published run would find it.
            Line(report, "security: " + SecurityCentre());

            // And do not disturb, Windows' own: the quiet-hours object in WpnUserService is called
            // through raw vtable slots, as Security Center's list is, and its mode's event is a WinRT
            // subscription, so this is the one place both run in the published build. Read only.
            Line(report, "do not disturb: " + Quiet());

            // And Windows' clipboard history, as the clipboard widget's flyout reads it: the WinRT
            // calls the JIT makes happily and only this build can refuse — the history's items, each
            // item's text and a picture decoded to a thumbnail. Read only, and only counted here:
            // nothing of what was copied is written anywhere.
            Line(report, "clipboard history: " + ClipboardHistory());

            // And what a sound source costs on this PC: one, and four. The four sound regions share
            // one (SoundStore) because four measured 15 ms on the dock's thread where one measured
            // 3.7; this line keeps that premise in sight on every publish. That the regions still
            // share is held by a test (SoundPartsTests.FourRegionsShareOneSourceAndTheLastToGoStopsIt).
            Line(report, "sound sources: " + SoundSources());

            if (idle > 0)
            {
                Line(report, Idle(gfx, islands, idle, review, config));
            }

            if (rounds > 0)
            {
                var (rebuild, popups, stores, pictures) = (only is null or "rebuild", only is null or "flyouts", only is null or "stores", only == "tray");
                Line(report, $"soak: {rounds} rounds of {(rebuild ? "rebuild " : "")}{(popups ? "flyouts " : "")}{(stores ? "stores " : "")}{(pictures ? "tray pictures" : "")}");
                Line(report, "  round  private MB  working set MB  managed MB  committed MB  gen0/1/2   ms/round  MB allocated/round  mapped MB");
                var clock = Stopwatch.StartNew();
                var last = 0L;
                var allocated = GC.GetTotalAllocatedBytes();
                for (var round = 1; round <= rounds; round++)
                {
                    if (rebuild)
                    {
                        // What makes the dock rebuild: tray pictures, windows, the clock.
                        content = Content(pinned, islands, round);
                        scene = Build(gfx, target, config, placed, look, islands, icons, content, scene);
                        Interact(gfx, scene);
                        foreach (var island in islands.Values)
                        {
                            island.Tick(gfx);
                            island.MinuteChanged(gfx);
                        }
                    }
                    if (popups)
                    {
                        flyouts.Run(look.Dark == (round % 2 == 0) ? LookFor(dark: false) : look, notifications, bar);
                    }
                    if (stores)
                    {
                        // A read of the database, as a write by Windows sets off, taken on this thread.
                        var version = notifications.Version;
                        notifications.Refresh();
                        calendarStore.Open();
                        Settle(() => notifications.Version != version && calendarStore.Idle().IsCompleted, 2000);
                    }
                    if (pictures && scene is not null)
                    {
                        // A live tray icon's new picture, as the dock paints it in place.
                        content = Content(pinned, islands, round);
                        scene.SetTray(gfx, content.TrayOf);
                        Pump();
                    }
                    // As the dock's loop does before it sleeps.
                    Collector.WhenDue();
                    if (round % every == 0 || round == rounds)
                    {
                        var elapsed = clock.ElapsedMilliseconds;
                        var count = Math.Min(every, round);
                        var now = GC.GetTotalAllocatedBytes();
                        Line(report, Row(round, (elapsed - last) / (double)count, Mb(now - allocated) / count));
                        (last, allocated) = (elapsed, now);
                    }
                }
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                Line(report, Memory("after the soak and a full collection"));
            }
        }
        finally
        {
            if (scene is not null)
            {
                scene.Dispose();
            }
            foreach (var island in islands.Values)
            {
                island.Dispose();
            }
            DataSources.Shared.Clear();
            target?.Dispose();
            Surfaces.Gone(bar);
            drawing = null;
        }
        Line(report, Memory("at the end"));
    }

    /// <summary>
    /// What Security Center answers here: the two healths, and who is in charge. Built on purpose,
    /// because Security.Source gives an isolated run a made-up one, and this is then the only
    /// place wscapi and the WSC product list are exercised in the published build.
    /// <para>
    /// Read-only, and it could not be otherwise: the source has no write path at all. Karpo never
    /// switches protection, starts a scan or dismisses a warning.
    /// </para>
    /// </summary>
    /// <summary>
    /// What Windows' own sound source costs here: started and read once, and then four at once —
    /// what the four sound regions would start if they did not share one (SoundStore). Built on
    /// purpose, as the others above are, because an isolated run's regions get the samples
    /// (SoundParts.Source).
    /// <para>
    /// Read-only, and it must stay so: Start, Read, Dispose. Nothing here sets a device, a level or
    /// a mute — the person's audio is theirs, and a smoke pass runs beside the dock they are using.
    /// </para>
    /// </summary>
    private static string SoundSources()
    {
        try
        {
            // The first source in a process pays for loading Core Audio; that is not what is asked.
            _ = Started(1);
            var one = Started(1);
            var four = Started(4);
            return string.Create(CultureInfo.InvariantCulture,
                $"one={one.Ms:F1} ms four={four.Ms:F1} ms devices={one.Devices} apps={one.Apps}");
        }
        catch (Exception error)
        {
            Troubles.Note("Sound: the smoke run", error);
            return "FAILED " + Troubles.Describe(error);
        }

        static (double Ms, int Devices, int Apps) Started(int count)
        {
            var made = new List<QuickSettings.Services.WindowsSound>();
            var clock = Stopwatch.StartNew();
            var state = default(QuickSettings.Services.SoundState);
            try
            {
                for (var index = 0; index < count; index++)
                {
                    var source = new QuickSettings.Services.WindowsSound();
                    made.Add(source);
                    source.Start(new QuickSettings.Services.Changes(_ => { }));
                    state = source.Read(0);
                }
                clock.Stop();
            }
            finally
            {
                foreach (var source in made)
                {
                    source.Dispose();
                }
            }
            return (clock.Elapsed.TotalMilliseconds, state?.Outputs.Count ?? 0, state?.Apps.Count ?? 0);
        }
    }

    /// <summary>
    /// What Windows' own do not disturb answers here, through the interface its notification center
    /// switches it with (QuickSettings\Services\DoNotDisturb.cs). Built on purpose, because an
    /// isolated run's service is a made-up one.
    /// <para>
    /// Read-only — Start, Read, Dispose — and it could not switch it if it tried: the Windows source
    /// refuses to in an isolated run, which this is. The person's do not disturb is theirs.
    /// </para>
    /// </summary>
    private static string Quiet()
    {
        try
        {
            using var source = new QuickSettings.Services.WindowsDoNotDisturb();
            var landed = 0;
            source.Start(() => Interlocked.Exchange(ref landed, 1));
            var until = Environment.TickCount64 + 2000;
            while (Environment.TickCount64 < until && Volatile.Read(ref landed) == 0)
            {
                Pump();
                Thread.Sleep(25);
            }
            var state = source.Read();
            return $"on={state.On?.ToString() ?? "unknown"} alarmsOnly={state.AlarmsOnly} automatic={state.Automatic} canSwitch={state.CanSwitch}";
        }
        catch (Exception error)
        {
            Troubles.Note("Do not disturb: the smoke run", error);
            return "FAILED " + Troubles.Describe(error);
        }
    }

    private static string ClipboardHistory()
    {
        try
        {
            var system = ClipboardSystem.Shared;
            if (!system.HistoryEnabled)
            {
                return "off in Windows: Karpo keeps its own";
            }
            var clock = Stopwatch.StartNew();
            var items = system.ReadHistoryAsync(ClipboardStore.ThumbnailWidth, ClipboardStore.ThumbnailHeight).GetAwaiter().GetResult();
            return items is null
                ? "on, but not given"
                : $"{items.Count} items, {items.Count(item => item.Kind == ClipKind.Image)} pictures ({items.Count(item => item.Picture is not null)} decoded), {clock.ElapsedMilliseconds} ms";
        }
        catch (Exception error)
        {
            Troubles.Note("Clipboard history: the smoke run", error);
            return "FAILED " + Troubles.Describe(error);
        }
    }

    private static string SecurityCentre()
    {
        try
        {
            using var source = new QuickSettings.Services.WindowsSecuritySource();
            var landed = 0;
            source.Start(() => Interlocked.Exchange(ref landed, 1));
            var until = Environment.TickCount64 + 2000;
            while (Environment.TickCount64 < until && Volatile.Read(ref landed) == 0)
            {
                Pump();
                Thread.Sleep(25);
            }
            var state = source.Read();
            return $"standing={state.Standing} antivirus={state.Antivirus} firewall={state.Firewall} guard=\"{state.Guard ?? "none"}\" defender={state.IsDefender} opens={state.Open ?? "none"}";
        }
        catch (Exception error)
        {
            Troubles.Note("Security: the smoke run", error);
            return "FAILED " + Troubles.Describe(error);
        }
    }

    /// <summary>
    /// What Windows' own one-button switches answer here: airplane mode, the mobile hotspot, night
    /// light, energy saver, VPN, rotation lock, spatial sound, the Wi-Fi and Bluetooth radios and the
    /// sound's and the microphone's mute, the eleven that reach out of the
    /// process — the radio management service's COM interface (or Windows.Devices.Radios where it is
    /// not there), WinRT's tethering manager, the tiles' own Settings handlers on a thread of their
    /// own, RAS, user32's rotation state, the output's spatial audio configuration, Windows.Devices.Radios
    /// and Core Audio's endpoint volume. Built on purpose,
    /// because QuickSwitches.For gives an isolated run made-up ones and this is then the only place
    /// any of them is exercised in the published build — where an AOT failure would otherwise be
    /// invisible until somebody's bar showed a dead button.
    /// <para>
    /// Read-only: Start, read, Dispose. Act is never called, so no radio, no hotspot, no night light,
    /// no energy saver, no VPN, no rotation, no spatial sound, no mute, no game mode, no color filter, no mono audio and no dark mode is switched, and nothing is left subscribed.
    /// </para>
    /// </summary>
    private static string Switches()
    {
        var said = new List<string>();
        foreach (var kind in (QuickSwitch[])[QuickSwitch.AirplaneMode, QuickSwitch.MobileHotspot, QuickSwitch.NightLight, QuickSwitch.EnergySaver, QuickSwitch.Vpn, QuickSwitch.RotationLock, QuickSwitch.SpatialSound, QuickSwitch.WifiRadio, QuickSwitch.BluetoothRadio, QuickSwitch.Sound, QuickSwitch.Microphone, QuickSwitch.GameMode, QuickSwitch.ColorFilters, QuickSwitch.MonoAudio, QuickSwitch.DarkMode])
        {
            try
            {
                using var action = QuickSettings.Services.QuickSwitches.Windows(kind);
                var landed = 0;
                action.Start(() => Interlocked.Exchange(ref landed, 1));
                // It answers off the dock's thread; a second is far longer than the 60 ms the
                // hotspot's look costs cold, and a PC that never answers is reported as such. A
                // Settings handler is given the time it takes to start up (SettingHandlers.cs).
                var until = Environment.TickCount64 + (kind is QuickSwitch.NightLight or QuickSwitch.EnergySaver or QuickSwitch.GameMode or QuickSwitch.ColorFilters or QuickSwitch.MonoAudio ? 4000 : 1000);
                while (Environment.TickCount64 < until && Volatile.Read(ref landed) == 0)
                {
                    Pump();
                    Thread.Sleep(25);
                }
                // Which road airplane mode took is part of the answer: the service's own switch, or
                // the radios one by one where its interface is not there (RadioManager.cs).
                var road = action switch
                {
                    QuickSettings.Services.AirplaneSwitch { Source: { } source } =>
                        source is QuickSettings.Services.SystemAirplane ? " via=service" : " via=radios",
                    // Night light and energy saver: the tile's own handler, or not (SettingHandlers.cs).
                    QuickSettings.Services.NightLightSwitch light => light.ThroughTile ? " via=tile" : " via=value",
                    QuickSettings.Services.EnergySaverSwitch saver => saver.ThroughTile ? " via=tile" : " via=settings",
                    QuickSettings.Services.SettingToggle setting => setting.ThroughTile ? " via=handler" : " via=settings",
                    // VPN: how many connections RAS lists, which says the phonebooks were read (Vpn.cs).
                    QuickSettings.Services.VpnSwitch vpn => $" connections={vpn.Count}",
                    // Rotation lock: whether this PC turns its screen at all (Rotation.cs).
                    QuickSettings.Services.RotationSwitch rotation => rotation.Offered ? " turns=yes" : " turns=no",
                    // Spatial sound: how many formats the output offers, which says its list was read (Spatial.cs).
                    QuickSettings.Services.SpatialSwitch spatial => $" formats={spatial.Count}",
                    _ => "",
                };
                said.Add($"{kind}={(Volatile.Read(ref landed) == 0 ? "no answer" : action.On?.ToString() ?? "unknown")}{road}");
            }
            catch (Exception error)
            {
                Troubles.Note($"Switches: {kind}", error);
                said.Add($"{kind}=FAILED {Troubles.Describe(error)}");
            }
        }
        return string.Join(" ", said);
    }

    /// <summary>
    /// What the status icons' Windows source reads here (QuickSettingsServicesIndicators.cs):
    /// which are so, by name, after it has had a moment. Read-only — Start, read, Dispose — so
    /// nothing is turned off, and nothing is left watched.
    /// </summary>
    private static string StatusIcons()
    {
        try
        {
            using var source = new QuickSettings.Services.WindowsIndicators();
            var heard = 0;
            source.Start(() => Interlocked.Increment(ref heard));
            var until = Environment.TickCount64 + 2000;
            while (Environment.TickCount64 < until)
            {
                Pump();
                Thread.Sleep(50);
            }
            var shown = QuickSettings.Services.Indicators.Of(source.State);
            return $"shown={string.Join(",", shown.Select(indicator => indicator.Kind))} posts={heard}";
        }
        catch (Exception error)
        {
            Troubles.Note("Status icons: the smoke run", error);
            return "FAILED " + Troubles.Describe(error);
        }
    }

    /// <summary>
    /// What the real Bluetooth service answers here: the radio, how many devices it found, and the
    /// first fault it hit. Read-only — Start, Read, Dispose — so a smoke run never pairs anything,
    /// never unpairs anything and never touches a radio.
    /// </summary>
    private static string Bluetooth()
    {
        try
        {
            using var source = new QuickSettings.Services.WindowsBluetooth();
            var heard = 0;
            source.Start(new QuickSettings.Services.Changes(_ => Interlocked.Increment(ref heard)));
            // As long as the panel waits before anybody has looked at it.
            var until = Environment.TickCount64 + 3000;
            while (Environment.TickCount64 < until)
            {
                Pump();
                Thread.Sleep(50);
            }
            var state = source.Read();
            var paired = state.Devices.Count(device => device.Paired);
            var battery = state.Devices.Count(device => device.Battery is not null);
            // The ones whose card offers to connect and disconnect them (BluetoothAudio).
            var audio = state.Devices.Count(device => device is { Paired: true, Audio: true });
            // The runes too: the offscreen pictures draw their made-up devices through the same
            // GlyphFor, so this is where a sheet and the real panel are seen to agree.
            var kinds = string.Join(",", state.Devices.Select(device => $"{char.ConvertToUtf32(device.Glyph, 0):X4}"));
            var said = $"radio={state.RadioOn?.ToString() ?? "none"} devices={state.Devices.Count} paired={paired} audio={audio} battery={battery} kinds={kinds} posts={heard}";
            return state.Problem is { Length: > 0 } trouble ? said + " PROBLEM " + trouble : said;
        }
        catch (Exception error)
        {
            Troubles.Note("Bluetooth: the smoke run", error);
            return "FAILED " + Troubles.Describe(error);
        }
    }

    // --- The bar ---------------------------------------------------------------

    /// <summary>
    /// The settings the bar is measured with, and one island of every kind the dock can show,
    /// with nothing that reaches the network. Never saved: the islands are handed round as they are.
    /// </summary>
    private static (Config Config, List<BarItem> Placed, List<Item> Pinned) Sample()
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        // pins: each pin is a widget of its own now; these are what the sample bar's pins open.
        string[] pins = [Path.Combine(windows, "explorer.exe"), Path.Combine(windows, "System32", "notepad.exe"), "https://example.com"];
        var config = new Config
        {
            Height = 48,
        };
        var placed = new List<BarItem>();
        foreach (var entry in BarEntries.All)
        {
            var island = entry.Kind.New();
            if (island.Pin is { } pin)
            {
                // A pin with something to open, so its icon is drawn as on a real bar.
                pin.Target = Path.Combine(windows, "System32", "cmd.exe");
            }
            if (island.Folder is { } shelf)
            {
                // A folder with widgets in it, for its flyout's lines: what a list holds
                // (ListWidgets) — rows that act and the sound panel's volume line among them.
                CellItem[] held = [ListKind.Cpu.New(), ListKind.EnergySaver.New(), ListKind.SoundVolume.New(), ListKind.Timer.New()];
                foreach (var cell in held)
                {
                    _ = Folders.Add(island.Kind, shelf, cell);
                }
            }
            placed.Add(island);
        }
        // Resolved once, as the dock resolves its pins: the shell's answers don't change per build.
        // More than the pins: open apps that come and go, a few at a time (see Content).
        string[] open = ["cmd.exe", "taskmgr.exe", "regedit.exe", "charmap.exe", "mspaint.exe", "calc.exe"];
        var pinned = pins
            .Concat(open.Select(exe => Path.Combine(windows, "System32", exe)))
            .Select(Item.Resolve)
            .OfType<Item>()
            .ToList();
        return (config, placed, pinned);
    }

    /// <summary>
    /// The config the smoke's sources follow: the bar, and with <paramref name="review"/> one more
    /// layout full of widgets, as the person's review bars were — its list folders hold every line
    /// four times. Nothing in them is
    /// set to reach anything: no place, no symbol, no account.
    /// </summary>
    private static Config Wanted(List<BarItem> placed, bool review)
    {
        var layouts = new List<TaskbarLayout> { new() { Id = "smoke", Name = "Smoke", Islands = placed } };
        if (review)
        {
            layouts.Add(new TaskbarLayout { Id = "review-list", Name = "Review list", Islands = [.. ListWidgets.All.Select(line => Filled(BarKind.FolderList, 4, () => line.Kind.New()))] });
        }
        return new Config { Layouts = layouts };
    }

    /// <summary>A folder of <paramref name="kind"/> holding <paramref name="count"/> widgets made by <paramref name="make"/>.</summary>
    private static BarItem Filled(BarKind kind, int count, Func<CellItem> make)
    {
        var folder = kind.New();
        folder.Folder!.Lists = [[.. Enumerable.Range(0, count).Select(_ => new FolderCell { Island = make() })]];
        return folder;
    }

    /// <summary>
    /// The dock sitting still for <paramref name="seconds"/>: its timer passing at the shortest
    /// interval there is, as the dock's does — the sources whose interval has passed, then the
    /// bar's widgets whose own has — and the processor time the process took over the stretch, with
    /// the memory after it.
    /// </summary>
    private static string Idle(Gfx gfx, IslandCollection islands, int seconds, bool review, Config config)
    {
        if (review)
        {
            // The person's own shape: the bar a new config gets on screen, and the two review bars
            // beside it. The smoke's bar of every kind goes first, and its
            // sources with it, so what is measured is that shape and nothing of the smoke's own.
            Person(gfx, islands, config);
            DataSources.Shared.Want(Wanted(Sections.DefaultLayout([]), review: true));
        }
        var intervals = islands.Values.Select(island => island.IntervalMs).Append(DataSources.Shared.Period).Where(ms => ms > 0).ToList();
        var period = intervals.Count > 0 ? intervals.Min() : 1000;
        var ticked = new Dictionary<string, long>(StringComparer.Ordinal);
        // What each source costs, in this thread's processor cycles: a clock would count the time
        // the thread waited for a processor as well, and on a busy machine that is most of it.
        var spent = new Dictionary<SourceKey, ulong>();
        using var process = Process.GetCurrentProcess();
        var before = process.TotalProcessorTime;
        var cycles = Cycles();
        var clock = Stopwatch.StartNew();
        var passes = 0;
        while (clock.ElapsedMilliseconds < seconds * 1000L)
        {
            Thread.Sleep(period);
            var at = Environment.TickCount64;
            // The sources one by one, as DataSources.Tick takes them, to say which the time went to.
            foreach (var running in DataSources.Shared.Running)
            {
                if (DataSources.Shared.Find(running) is { } source)
                {
                    var started = Cycles();
                    _ = source.Catch(at, slack: period / 2);
                    spent[running] = spent.GetValueOrDefault(running) + (Cycles() - started);
                }
            }
            foreach (var (key, island) in islands)
            {
                var interval = island.IntervalMs;
                if (interval <= 0 || (ticked.TryGetValue(key, out var last) && at - last + (period / 2) < interval))
                {
                    continue;
                }
                ticked[key] = at;
                island.Tick(gfx);
            }
            Pump();
            Collector.WhenDue();
            passes++;
        }
        var looped = Cycles() - cycles;
        process.Refresh();
        var used = (process.TotalProcessorTime - before).TotalMilliseconds;
        var wall = clock.ElapsedMilliseconds;
        var sources = spent.Values.Aggregate(0UL, (sum, each) => sum + each);
        var costly = spent.Where(entry => entry.Value >= 1_000_000).OrderByDescending(entry => entry.Value).Take(6)
            .Select(entry => $"{entry.Key} {entry.Value / 1e6:F0}");
        return $"idle: {seconds} s, {passes} passes every {period} ms, {used:F0} ms of processor time "
            + $"({100 * used / wall:F2} % of one core); the timer's thread {looped / 1e6:F0} M cycles, "
            + $"{spent.Count} sources {sources / 1e6:F0} M of them ({string.Join(", ", costly)}); " + Memory("after it");
    }

    /// <summary>The processor cycles this thread has run, for what a stretch of it cost.</summary>
    private static ulong Cycles()
    {
        ulong cycles;
        return QueryThreadCycleTime(GetCurrentThread(), &cycles) != 0 ? cycles : 0;
    }

    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern int QueryThreadCycleTime(HANDLE thread, ulong* cycles);

    /// <summary>
    /// The smoke's bar of every kind taken down, and the bar a new config gets made in its place: what
    /// sits on the person's own display, beside their review layouts.
    /// </summary>
    private static void Person(Gfx gfx, IslandCollection islands, Config config)
    {
        foreach (var island in islands.Values)
        {
            island.Dispose();
        }
        islands.Clear();
        foreach (var placed in Sections.DefaultLayout([]))
        {
            if (placed.Kind.IsIsland() && Islands.Create(Config.IslandKey(null, placed.Id), placed, gfx, config) is { } made)
            {
                islands[Config.IslandKey(null, placed.Id)] = made;
            }
        }
    }

    private static void MakeIslands(Gfx gfx, Config config, List<BarItem> list, HWND bar, IslandCollection islands)
    {
        var state = new SystemState(
            "\uE701", "\uE995",
            NetworkBackGlyph: null, BluetoothGlyph: "\uE702", BluetoothOn: true,
            NetworkTip: "Wi-Fi\nInternet access", VolumeTip: "Speakers: 40%");
        nuint next = 1;
        foreach (var placed in list)
        {
            if (!placed.Kind.IsIsland())
            {
                continue;
            }
            var key = Config.IslandKey(null, placed.Id);
            var made = Islands.Create(key, placed, gfx, config);
            if (made is null)
            {
                continue;
            }
            // The network's and the sound's faces on a made-up connection and level, never this PC's.
            if (made is FolderIsland { ShowsStatus: true } folder)
            {
                folder.SetStatus(state, gfx);
            }
            var token = next++;
            Tokens[token] = made;
            made.Attach((nint)bar, token);
            islands[key] = made;
        }
    }

    /// <summary>What the dock would show: pins and open apps, and tray icons (new pictures each round).</summary>
    private static Content Content(List<Item> known, IslandCollection islands, int round)
    {
        // Four apps, another set each round, as windows open and close: icons come and go.
        var apps = known.Count == 0 ? [] : Enumerable.Range(0, Math.Min(4, known.Count)).Select(index => known[(round + index) % known.Count]).ToList();
        var tiles = apps.Select(item => (item, new Mark(round % 3, round % 3 == 1, round % 7 == 3))).ToList();
        var tray = new List<TrayIcon>();
        for (var index = 0; index < 6; index++)
        {
            var colour = 0xFF000000u | (uint)((index * 0x3F2A17 + round * 0x0B0D0F) & 0xFFFFFF);
            tray.Add(new TrayIcon($"smoke/{index}", HWND.NULL, (uint)index, Guid.Empty, 0, 4, Solid(24, colour), $"Icon {index}", index >= 4));
        }
        return new Content
        {
            Tiles = tiles,
            TrayOverflow = true,
            Islands = islands,
            // tray: Content.Tray is what the bar shows, the split done (the last two stay behind
            // the chevron, which TrayOverflow above draws).
            Tray = [.. tray.Where(icon => !icon.Hidden)],
        };
    }

    /// <summary>
    /// What Dock.Rebuild and DockWindow.Build do for one bar: the islands keep their surfaces (§359), a new
    /// layout, the bar's one scene laid out again with it (§358), and the icons nobody uses any more go.
    /// </summary>
    private static Scene Build(Gfx gfx, DesktopWindowTarget target, Config config, List<BarItem> placed, Look look, IslandCollection islands, Icons icons, Content content, Scene? previous)
    {
        foreach (var island in islands.Values)
        {
            island.Rebuilding();
        }
        var monitor = new RECT { left = 0, top = 0, right = ScreenWidth, bottom = ScreenHeight };
        var specs = placed
            .Select(island => PartSpec.For(Config.IslandKey(null, island.Id), island))
            .ToList();
        var layout = new Layout(gfx, Dpi, monitor, monitor, Edge.Bottom, config, look, specs, content);
        var scene = previous ?? new Scene(gfx, target);
        scene.Update(layout, content, look, icons, shown: true);
        foreach (var island in islands.Values)
        {
            island.Rebuilt();
        }
        icons.Sweep();
        return scene;
    }

    /// <summary>The pointer over every part and a press, as the dock does between builds.</summary>
    private static void Interact(Gfx gfx, Scene scene)
    {
        foreach (var hit in new[] { Hit.Start, Hit.Tile(0), Hit.Tile(1), Hit.Tray(0), Hit.TrayOverflow })
        {
            scene.Highlight(hit, pressed: false);
            scene.Tooltip(gfx, $"Smoke tooltip {hit.Kind}", hit);
        }
        scene.Press(Hit.Tile(0), true);
        scene.Press(Hit.Tile(0), false);
        scene.Bounce(1);
        scene.Tooltip(gfx, null, null);
        scene.Highlight(null, false);
        Pump();
    }

    // --- The flyouts -------------------------------------------------------------

    /// <summary>Every popup of the dock, made once and shown and hidden per round, as the dock keeps them.</summary>
    private sealed class Flyouts : IDisposable
    {
        private readonly Gfx gfx;
        private readonly Flyout menu;
        private readonly Flyout hover;
        private readonly Preview preview;
        private readonly QuickSettings.QuickFlyout quick;
        private readonly FolderFlyout folder;
        private readonly IslandCollection islands;
        private readonly RECT work = new() { left = 0, top = 0, right = ScreenWidth, bottom = ScreenHeight - 72 };
        private readonly POINT anchor = new() { x = ScreenWidth - 200, y = ScreenHeight - 72 };

        public Flyouts(Gfx gfx, HINSTANCE instance, IslandCollection islands)
        {
            this.gfx = gfx;
            this.islands = islands;
            menu = new Flyout(gfx, instance, 0);
            hover = new Flyout(gfx, instance, 0, FlyoutMode.Hover) { GroupContent = true, StripIconSize = 36 };
            preview = new Preview(gfx, instance, 0);
            quick = new QuickSettings.QuickFlyout(gfx, instance, 0);
            folder = new FolderFlyout(gfx, instance, 0);
            Routes.Add((hwnd, msg, wParam, lParam) => hwnd == menu.Hwnd && Handled(menu.Message(msg, wParam, lParam)));
            Routes.Add((hwnd, msg, wParam, lParam) => hwnd == hover.Hwnd && Handled(hover.Message(msg, wParam, lParam)));
            Routes.Add((hwnd, msg, wParam, lParam) => hwnd == quick.Hwnd && Handled(quick.Message(msg, wParam, lParam)));
            Routes.Add((hwnd, msg, wParam, lParam) => hwnd == folder.Hwnd && Handled(folder.Message(msg, wParam, lParam)));
            Routes.Add((hwnd, msg, wParam, lParam) =>
            {
                if (hwnd != preview.Hwnd || msg != WM.WM_TIMER)
                {
                    return false;
                }
                preview.Timer((nuint)wParam);
                return true;
            });
        }

        /// <summary>How long each popup's Show took the last time round, in ms: what a click waits for before its popup is drawn.</summary>
        public Dictionary<string, double> Opening { get; } = [];

        /// <summary>
        /// How long a popup's own DWM work took after it opened, in ms — the window list, whose
        /// previews DWM registers on its next frame, off the dock's thread (ThumbnailBatch). Said
        /// apart, so a busy compositor doesn't read as Karpo getting slower (Smoke.Opening).
        /// </summary>
        public Dictionary<string, double> Waiting { get; } = [];

        /// <summary>Each popup shown and hidden once, in the look given.</summary>
        public void Run(Look look, NotificationStore notifications, HWND bar, Action<string>? step = null)
        {
            const float scale = Dpi / 96f;
            void Time(string name, Action show)
            {
                var started = Stopwatch.GetTimestamp();
                show();
                Opening[name] = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            }
            var glyph = new FlyoutIcon.Glyph("\uE713");
            var picture = new FlyoutIcon.Picture(Solid(24, 0xFF3478F6));
            List<FlyoutItem> items =
            [
                new(0, "Recent", Header: true),
                new(100, "A document with a long name that has to be cut somewhere", picture, Detail: "C:\\Users\\Someone\\Documents"),
                new(101, "Another one", picture),
                new(0, "", Separator: true),
                new(1, "Pin to taskbar", new FlyoutIcon.Glyph("\uE718")),
                new(2, "Options", glyph, Children: [new(20, "One", Checked: true), new(21, "Two", Checked: false)]),
                new(3, "Close window", new FlyoutIcon.Glyph("\uE8BB"), Accelerator: "Alt+F4"),
            ];
            Time("menu", () => menu.Show(gfx, look, scale, items, FlyoutLayout.List, anchor, work, Edge.Bottom, "Smoke"));
            Pump();
            menu.Hide();
            Time("grid", () => menu.Show(gfx, look, scale, items.Where(item => !item.Header && !item.Separator).ToList(), FlyoutLayout.Grid, anchor, work, Edge.Bottom));
            Pump();
            menu.Hide();
            Time("strip", () => hover.Show(gfx, look, scale, [new(1000, "One", picture), new(1001, "Two", picture), new(1002, "Three", picture)], FlyoutLayout.Strip, anchor, work, Edge.Bottom));
            Pump();
            hover.Hide();
            step?.Invoke("the menus");

            // The notifications preset's folder: the bell's list, grouped, on the made-up database.
            if (islands.Values.OfType<FolderIsland>().FirstOrDefault(each => each.ShowsNotifications) is { } bell)
            {
                notifications.Prepare();
                Time("notifications", () => folder.Show(gfx, look, scale, anchor, work, Edge.Bottom, bell, FlyoutAlign.End));
                Pump();
                folder.Hide();
                Settle(() => false, 150);
            }
            step?.Invoke("the notification list");

            // The bar's own window is the one window here to list: its thumbnail, from this hidden desktop.
            Time("window list", () => preview.Show(gfx, look, scale, 0, [(bar, "Karpo smoke")], anchor, work, Edge.Bottom));
            // The previews arrive off the dock's thread (ThumbnailBatch): how long DWM took is said
            // apart, and waited for here only to say it — the list was open before.
            _ = preview.Previews.Wait(TimeSpan.FromSeconds(30));
            Waiting["window list"] = preview.DwmMs;
            // A preview DWM would not register is a list of empty boxes: said, and failed.
            if (preview.Previews.IsCompleted && preview.PreviewsShown == 0)
            {
                throw new InvalidOperationException("DWM registered none of the window list's previews");
            }
            Pump();
            preview.Hide();
            step?.Invoke("the window list");

            // Quick settings on made-up networks, devices and levels (KARPO_QUICK_SAMPLES, set in Run).
            quick.Show(gfx, look, scale, anchor, work, Edge.Bottom, QuickSettings.QuickKind.RenameDesktop, desktop: Guid.NewGuid());
            Pump();
            quick.Hide();
            step?.Invoke("the quick settings");

            if (islands.Values.OfType<FolderIsland>().FirstOrDefault() is { } shelf)
            {
                Time("folder", () => folder.Show(gfx, look, scale, anchor, work, Edge.Bottom, shelf, FlyoutAlign.End));
                Pump();
                folder.Hide();
            }
            // The hide fades end on the popups' timers.
            Settle(() => false, 150);
            step?.Invoke("the folder");
        }

        public void Dispose()
        {
            Routes.Clear();
            folder.Dispose();
            quick.Dispose();
            preview.Dispose();
            hover.Dispose();
            menu.Dispose();
        }

        private static bool Handled<T>(T _) => false;
    }

    // --- Drawing ------------------------------------------------------------------

    /// <summary>Every kind of surface Gfx draws, once.</summary>
    private static void DrawEverything(Gfx gfx)
    {
        var made = new List<CompositionDrawingSurface>
        {
            gfx.Icon(Solid(32, 0xFFE3008C), 36),
            gfx.GroupIcon([Solid(16, 0xFF0F6CBD), null, Solid(16, 0xFF107C10)], 36, dark: true),
            gfx.Tile("Karpo", 36),
            gfx.WindowsLogo(36, dark: false),
            gfx.Pill("Smoke", 1.5f).Surface,
            gfx.Lines([("12:34", 18, Weight.Regular), ("22.09.2026", 18, Weight.Regular)], Gfx.Ink(1, 1, 1, 1), null).Surface,
            gfx.Text("A line of text cut short", 18, Weight.Semibold, Gfx.Ink(1, 1, 1, 1), 60).Surface,
            gfx.Glyph("\uE713", 24, Gfx.Ink(1, 1, 1, 1), 36),
        };
        gfx.RedrawGlyph(made[^1], "\uE714", 24, Gfx.Ink(0, 0, 0, 1));
        _ = gfx.Measure("Measure me", 18, Weight.Regular);
        foreach (var surface in made)
        {
            surface.Dispose();
        }
    }

    private static Look LookFor(bool dark) => new(
        Glass: false,
        Dark: dark,
        Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4),
        AccentLight2: Color.FromArgb(255, 0x99, 0xEB, 0xFF),
        AccentDark1: Color.FromArgb(255, 0x00, 0x5F, 0xB8),
        AccentDark2: Color.FromArgb(255, 0x00, 0x4C, 0x87),
        Material: Material.Acrylic);

    /// <summary>A solid square of premultiplied BGRA, standing in for an app's icon.</summary>
    private static Pixels Solid(int size, uint argb)
    {
        size = Math.Max(size, 1);
        var data = new byte[size * size * 4];
        for (var i = 0; i < data.Length; i += 4)
        {
            data[i] = (byte)argb;
            data[i + 1] = (byte)(argb >> 8);
            data[i + 2] = (byte)(argb >> 16);
            data[i + 3] = (byte)(argb >> 24);
        }
        return new Pixels { Data = data, Width = size, Height = size };
    }

    // --- Made-up stores -------------------------------------------------------------

    /// <summary>A notification database of Windows' shape with a dozen toasts, at <paramref name="path"/>.</summary>
    private static string Database(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (Sqlite.Open(path, Sqlite.OPEN_READWRITE | Sqlite.OPEN_CREATE, out var db) != Sqlite.OK)
        {
            throw new InvalidOperationException("the made-up notification database could not be made");
        }
        try
        {
            var now = Clock.RealUtc().ToFileTimeUtc();
            var sql = new StringBuilder(
                "CREATE TABLE Notification (\"Order\" INTEGER PRIMARY KEY AUTOINCREMENT, Id INTEGER NOT NULL, HandlerId INTEGER, Type TEXT NOT NULL, "
                + "Payload BLOB, ExpiryTime INT64, ArrivalTime INT64, PayloadType TEXT NOT NULL);"
                + "CREATE TABLE NotificationHandler (RecordId INTEGER PRIMARY KEY, PrimaryId TEXT NOT NULL);"
                + "INSERT INTO NotificationHandler VALUES (1, 'Smoke.Mail'), (2, 'Smoke.Chat');");
            for (var i = 1; i <= 12; i++)
            {
                var toast = $"<toast><visual><binding template=\"ToastGeneric\"><text>Message {i}</text><text>Body of message {i}, long enough to wrap onto a second line in the list.</text></binding></visual></toast>";
                sql.Append(CultureInfo.InvariantCulture,
                    $"INSERT INTO Notification (Id, HandlerId, Type, Payload, ExpiryTime, ArrivalTime, PayloadType) VALUES ({i}, {1 + i % 2}, 'toast', X'{Convert.ToHexString(Encoding.UTF8.GetBytes(toast))}', 0, {now - i * TimeSpan.TicksPerMinute * 7}, 'Xml');");
            }
            if (Sqlite.Execute(db, sql.ToString()) != Sqlite.OK)
            {
                throw new InvalidOperationException("the made-up notification database could not be filled");
            }
        }
        finally
        {
            Sqlite.Close(db);
        }
        return path;
    }

    /// <summary>A calendar with an event every few days around today, and an all-day one.</summary>
    private static List<CalendarEvent> SampleEvents()
    {
        var events = new List<CalendarEvent>();
        var day = Clock.Real().Date;
        for (var i = -10; i < 40; i += 3)
        {
            var start = day.AddDays(i).AddHours(9);
            events.Add(new CalendarEvent(
                start, start.AddHours(1), false,
                string.Create(CultureInfo.InvariantCulture, $"Event {i}"),
                string.Create(CultureInfo.InvariantCulture, $"Room {i}")));
        }
        events.Add(new CalendarEvent(day, day.AddDays(2), true, "Holiday", null));
        return events;
    }

    // --- Windows and the loop --------------------------------------------------------

    private static void Register(HINSTANCE instance)
    {
        var cursor = LoadCursorW(HINSTANCE.NULL, IDC.IDC_ARROW);
        foreach (var name in new[] { BarClass, Flyout.ClassName, Preview.ClassName })
        {
            fixed (char* className = name)
            {
                var type = new WNDCLASSW { lpfnWndProc = &Procedure, hInstance = instance, lpszClassName = className, hCursor = cursor };
                _ = RegisterClassW(&type);
            }
        }
    }

    /// <summary>The bar's window: what DockWindow makes, never shown (it is on the hidden desktop anyway).</summary>
    private static HWND MakeWindow(HINSTANCE instance)
    {
        HWND hwnd;
        fixed (char* className = BarClass)
        fixed (char* title = "Karpo smoke")
        {
            hwnd = CreateWindowExW(
                WS.WS_EX_TOOLWINDOW | WS.WS_EX_NOACTIVATE | WS.WS_EX_NOREDIRECTIONBITMAP,
                className,
                title,
                WS.WS_POPUP,
                0,
                ScreenHeight - 72,
                ScreenWidth,
                72,
                HWND.NULL,
                HMENU.NULL,
                instance,
                null);
        }
        if (hwnd == HWND.NULL)
        {
            throw new NativeException($"CreateWindowExW failed: error {Marshal.GetLastSystemError()}", 0);
        }
        return hwnd;
    }

    private static DesktopWindowTarget Target(Gfx gfx, HWND hwnd)
    {
        var desktop = ComUtil.Query<ICompositorDesktopInterop>(gfx.Compositor);
        try
        {
            IDesktopWindowTarget* raw = null;
            ComUtil.Check(desktop->CreateDesktopWindowTarget(hwnd, BOOL.FALSE, &raw));
            return ComUtil.Adopt<DesktopWindowTarget>(raw);
        }
        finally
        {
            desktop->Release();
        }
    }

    [UnmanagedCallersOnly]
    private static LRESULT Procedure(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        try
        {
            if (msg == Islands.WM_KARPO_ISLAND)
            {
                if (Tokens.TryGetValue((nuint)wParam, out var island) && drawing is { } gfx)
                {
                    _ = island.Refresh(gfx);
                }
                return 0;
            }
            foreach (var route in Routes.ToArray())
            {
                if (route(hwnd, msg, wParam, lParam))
                {
                    return 0;
                }
            }
        }
        catch (Exception error)
        {
            // Nothing may unwind into user32; the report says what went wrong.
            Console.Error.WriteLine($"smoke: {error}");
        }
        return DefWindowProcW(hwnd, msg, wParam, lParam);
    }

    /// <summary>
    /// A window shown in a frame: hidden until the frame's commit answers, then shown, the fence landed
    /// by the commit rather than the time limit. Throws where it is not so: the published bar would then
    /// show every window of its a quarter of a second late, or never by its commit.
    /// </summary>
    private static string Fence(Gfx gfx, HINSTANCE instance)
    {
        var window = MakeWindow(instance);
        var target = Target(gfx, window);
        var fill = gfx.Compositor.CreateSpriteVisual();
        fill.Brush = gfx.Compositor.CreateColorBrush(Color.FromArgb(255, 0x20, 0x40, 0x80));
        fill.Size = new Vector2(200, 72);
        target.Root = fill;
        try
        {
            var before = Surfaces.Fences;
            using (Surfaces.Begin(gfx.Compositor))
            {
                Surfaces.Show(window);
            }
            if (IsWindowVisible(window))
            {
                throw new InvalidOperationException("fence: the window came up before its commit");
            }
            Settle(() => Surfaces.Settled, 2000);
            var after = Surfaces.Fences;
            if (!IsWindowVisible(window) || after.Committed <= before.Committed || after.TimedOut != before.TimedOut)
            {
                throw new InvalidOperationException($"fence: not landed by its commit ({after.Way}: {after.Committed - before.Committed} landed, {after.TimedOut - before.TimedOut} timed out)");
            }
            return $"{(after.Way == "commit" ? "RequestCommitAsync" : after.Way)}, landed by its commit in {after.LastMs:F0} ms";
        }
        finally
        {
            Surfaces.Destroy(window, () =>
            {
                target.Root = null;
                fill.Brush?.Dispose();
                fill.Dispose();
                target.Dispose();
            });
        }
    }

    /// <summary>What is waiting in the queue, handled now.</summary>
    private static void Pump()
    {
        MSG message;
        while (PeekMessageW(&message, HWND.NULL, 0, 0, PM.PM_REMOVE))
        {
            _ = TranslateMessage(&message);
            _ = DispatchMessageW(&message);
        }
    }

    /// <summary>Pumps until <paramref name="done"/> says so or <paramref name="most"/> ms have passed: timers, posts and pool work land meanwhile.</summary>
    private static void Settle(Func<bool> done, int most = 5000)
    {
        var until = Environment.TickCount64 + most;
        while (true)
        {
            Pump();
            var left = until - Environment.TickCount64;
            if (done() || left <= 0)
            {
                return;
            }
            _ = MsgWaitForMultipleObjectsEx(0, null, (uint)Math.Min(left, 20), QS.QS_ALLINPUT, MWMO.MWMO_INPUTAVAILABLE);
        }
    }

    // --- The report ----------------------------------------------------------------

    private static void Line(StringBuilder report, string text) => report.Append("smoke: ").AppendLine(text);

    /// <summary>Milliseconds since the process started.</summary>
    private static double Since()
    {
        using var process = Process.GetCurrentProcess();
        return (Clock.Real() - process.StartTime).TotalMilliseconds;
    }

    /// <summary>
    /// "menu 1, grid 1, …, window list 1 (its previews + DWM 17 after), … ms": each popup's opening,
    /// in the order they were shown, and for the window list how long DWM then took over its
    /// previews (<paramref name="waiting"/>), which the opening no longer waits for.
    /// </summary>
    private static string Opening(Dictionary<string, double> times, Dictionary<string, double> waiting) =>
        string.Join(", ", times.Select(pair => waiting.TryGetValue(pair.Key, out var dwm)
            ? string.Create(CultureInfo.InvariantCulture, $"{pair.Key} {pair.Value:F0} (its previews + DWM {dwm:F0} after)")
            : string.Create(CultureInfo.InvariantCulture, $"{pair.Key} {pair.Value:F0}"))) + " ms";

    /// <summary>The longest any popup may take to open before the smoke calls it a failure.</summary>
    internal const double SlowestOpeningMs = 1000;

    /// <summary>
    /// Fails the smoke when a popup took <see cref="SlowestOpeningMs"/> or more to open: the window
    /// list once waited on DWM for every window it listed, 18 seconds on a busy compositor, and
    /// nothing said so but a number in a line (ThumbnailBatch). DWM's own time is not in the
    /// opening any more, so a second is room enough for any popup on any machine.
    /// </summary>
    internal static void Opened(Dictionary<string, double> times)
    {
        foreach (var (name, ms) in times)
        {
            if (ms >= SlowestOpeningMs)
            {
                throw new InvalidOperationException(string.Create(CultureInfo.InvariantCulture, $"the {name} took {ms:F0} ms to open; a popup has {SlowestOpeningMs:F0}"));
            }
        }
    }

    private static string Memory(string when)
    {
        var (privateBytes, workingSet) = Counters();
        var (_, mapped, _) = Regions();
        var info = GC.GetGCMemoryInfo();
        return string.Create(CultureInfo.InvariantCulture,
            $"memory {when}: {Mb(privateBytes):F1} MB private, {Mb(workingSet):F1} MB working set, {Mb(mapped):F1} MB mapped, "
            + $"{Mb(GC.GetTotalMemory(false)):F1} MB managed, {Mb(info.TotalCommittedBytes):F1} MB committed by the GC, "
            + $"collections {GC.CollectionCount(0)}/{GC.CollectionCount(1)}/{GC.CollectionCount(2)}");
    }

    private static string Row(int round, double msPerRound, double mbPerRound)
    {
        var (privateBytes, workingSet) = Counters();
        var (_, mapped, _) = Regions();
        return string.Create(CultureInfo.InvariantCulture,
            $"  {round,5}  {Mb(privateBytes),10:F1}  {Mb(workingSet),14:F1}  {Mb(GC.GetTotalMemory(false)),10:F1}  {Mb(GC.GetGCMemoryInfo().TotalCommittedBytes),12:F1}  "
            + $"{GC.CollectionCount(0),3}/{GC.CollectionCount(1)}/{GC.CollectionCount(2)}  {msPerRound,9:F1}  {mbPerRound,18:F2}  {Mb(mapped),9:F1}");
    }

    /// <summary>Committed address space by kind: private allocations, mapped views (shared memory, such as surfaces DWM reads), and images.</summary>
    private static (long Private, long Mapped, long Image) Regions()
    {
        long privateBytes = 0, mapped = 0, image = 0;
        MEMORY_BASIC_INFORMATION info;
        byte* at = null;
        while (VirtualQuery(at, &info, (nuint)sizeof(MEMORY_BASIC_INFORMATION)) != 0)
        {
            if (info.State == MEM.MEM_COMMIT)
            {
                var size = (long)info.RegionSize;
                switch (info.Type)
                {
                    case MEM.MEM_PRIVATE:
                        privateBytes += size;
                        break;
                    case MEM.MEM_MAPPED:
                        mapped += size;
                        break;
                    case MEM.MEM_IMAGE:
                        image += size;
                        break;
                }
            }
            var next = (byte*)info.BaseAddress + info.RegionSize;
            if (next <= at)
            {
                break;
            }
            at = next;
        }
        return (privateBytes, mapped, image);
    }

    private static (long Private, long WorkingSet) Counters()
    {
        var counters = new PROCESS_MEMORY_COUNTERS_EX { cb = (uint)sizeof(PROCESS_MEMORY_COUNTERS_EX) };
        return K32GetProcessMemoryInfo(GetCurrentProcess(), (PROCESS_MEMORY_COUNTERS*)&counters, counters.cb)
            ? ((long)counters.PrivateUsage, (long)counters.WorkingSetSize)
            : (0, 0);
    }

    private static double Mb(long bytes) => bytes / (1024.0 * 1024.0);

    private static int Number(string[] args, string name, int fallback)
    {
        var at = Array.IndexOf(args, name);
        return at >= 0 && at + 1 < args.Length && int.TryParse(args[at + 1], NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;
    }
}
